using System.Runtime.ExceptionServices;
using eCommerce.Inventory;
using eCommerce.Kernel.Locking;
using eCommerce.Kernel.Money;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — ĐỘNG CƠ BÌNH QUÂN GIA QUYỀN DI ĐỘNG: ORCHESTRATION** (`PRC-001-arch` §5.2/§5.6; `D-323`).
///
/// 🔑 **VÌ SAO LÀ DỊCH VỤ MIỀN (KHÔNG phải app service):** worker nền **KHÔNG có principal** ⇒ gọi app service
/// `[Authorize]` sẽ NÉM `AbpAuthorizationException` (bài học `SAL-005`). Worker resolve **lớp này** trực tiếp;
/// endpoint command (`CostEngineAppService`) cũng uỷ quyền cho nó ⇒ **một** logic, hai biên.
///
/// 📌 **Hàm tính là THUẦN ở Domain** (`TinhBinhQuanGiaQuyenDiDong`); lớp này chỉ: đọc Setting (1 lần) →
/// fail-closed → khoá theo SKU → nạp chuyển động (port Inventory) → đối soát `PrcCostPrices` → cập nhật run.
/// 🚨 **KHÔNG BAO GIỜ UPDATE dòng `DaDuyet = true`** (`D-177` ③/§5.4): `ChoNhapLuiNgay = false` ⇒ bỏ qua + đếm;
/// `= true` mà giá trị sẽ đổi ⇒ **NÉM `0015`**.
///
/// 🆕 **`D-324` — BA PHA UoW TÁCH RỜI (sổ run phải BỀN qua rollback UoW ngoài — K06-08b):**
///   • **P0** kiểm tham số + đọc 4 Setting + fail-closed + `GiaVonDongCo = false ⇒ null` — **TRƯỚC MỌI GHI**;
///   • **P1** sổ run (`PrcCostEngineRuns`) trong UoW **riêng** + **COMMIT trước khi tính** ⇒ trạng thái
///     `DangChay`/`Loi` không bị rollback cùng lượt tính;
///   • **P2** tính + ghi `PrcCostPrices` trong UoW **riêng**; lỗi ⇒ **KHÔNG** commit (bỏ phần dở), đi tiếp P3;
///   • **P3** chốt run trong UoW **MỚI** (`GetAsync` nạp lại — KHÔNG dùng instance cũ đã detached) ⇒ rồi mới NÉM.
/// </summary>
public class CostEngineService : ITransientDependency
{
    private static readonly TimeSpan ChoKhoaToiDa = TimeSpan.FromSeconds(30);

    private readonly IRepository<CostEngineRun, Guid> _runs;
    private readonly IRepository<CostPrice, Guid> _giaVon;
    private readonly ICostFeedLookup _feed;
    private readonly ISettingProvider _settings;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ILogger<CostEngineService> _logger;

    public CostEngineService(
        IRepository<CostEngineRun, Guid> runs,
        IRepository<CostPrice, Guid> giaVon,
        ICostFeedLookup feed,
        ISettingProvider settings,
        IAbpDistributedLock distributedLock,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager uowManager,
        ILogger<CostEngineService> logger)
    {
        _runs = runs;
        _giaVon = giaVon;
        _feed = feed;
        _settings = settings;
        _distributedLock = distributedLock;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _uowManager = uowManager;
        _logger = logger;
    }

    /// <summary>
    /// Chạy động cơ cho khoảng ngày kinh doanh `[from, to]` (UTC+7).
    /// Trả `null` khi động cơ **đang tắt** (`Pricing:GiaVonDongCo = false`); cấu hình sai ⇒ **NÉM `0014`**.
    /// </summary>
    public async Task<CostEngineRun?> ChayAsync(DateOnly from, DateOnly to, string trigger)
    {
        // ═══════════════ P0 — CHỈ ĐỌC/KIỂM, KHÔNG GHI ═══════════════
        if (to < from)
        {
            throw LoiCauHinh("`to` phải ≥ `from`.", "from", from, "to", to);
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw LoiCauHinh("`trigger` là BẮT BUỘC.");
        }

        // Đọc 4 setting ĐÚNG MỘT LẦN (`PRC-001-arch` §5.2).
        var phuongPhap = PricingChinhSach.DocPhuongPhapGiaVon(
            await _settings.GetOrNullAsync(PRC001SettingTenKhoa.PhuongPhapGiaVon), out var phuongPhapHopLe);
        var theoKho = PricingChinhSach.DocBool(
            await _settings.GetOrNullAsync(PRC001SettingTenKhoa.GiaVonTheoKho), false, out var theoKhoHopLe);
        var choNhapLuiNgay = PricingChinhSach.DocBool(
            await _settings.GetOrNullAsync(PRC001SettingTenKhoa.ChoNhapLuiNgay), false, out var choNhapLuiHopLe);
        var dongCo = PricingChinhSach.DocBool(
            await _settings.GetOrNullAsync(PRC001SettingTenKhoa.GiaVonDongCo), false, out var dongCoHopLe);

        var chinhSachLamTron = PricingChinhSach.DocChinhSachLamTron(
            await _settings.GetOrNullAsync(PRC002SettingTenKhoa.ChinhSachLamTron), out _);

        // FAIL-CLOSED (`D-177` ②/`D-318` C9/K06-11).
        if (!phuongPhapHopLe
            || !string.Equals(phuongPhap, PricingChinhSach.PhuongPhapBinhQuanGiaQuyenDiDong, StringComparison.OrdinalIgnoreCase))
        {
            throw LoiCauHinh(
                "`Pricing:PhuongPhapGiaVon` phải là `BinhQuanGiaQuyenDiDong` ở v1.",
                "phuongPhap", phuongPhap);
        }

        if (!theoKhoHopLe)
        {
            throw LoiCauHinh("`Pricing:GiaVonTheoKho` phải là `true`/`false`.");
        }

        if (theoKho)
        {
            throw LoiCauHinh("v1 CHỈ hỗ trợ grain SKU toàn hệ thống ⇒ `Pricing:GiaVonTheoKho` phải `false` (`D-318` C9).");
        }

        if (!choNhapLuiHopLe || !dongCoHopLe)
        {
            throw LoiCauHinh(
                "`Pricing:ChoNhapLuiNgay`/`Pricing:GiaVonDongCo` phải là `true`/`false` (cấu hình sai — không đoán).");
        }

        // Đường lùi rẻ nhất: TẮT động cơ ⇒ 0 dòng mới, dữ liệu cũ nguyên vẹn (`§4.3`).
        if (!dongCo)
        {
            _logger.LogInformation("Động cơ giá vốn ĐANG TẮT (`Pricing:GiaVonDongCo = false`) ⇒ bỏ qua.");

            return null;
        }

        // ═══════════════ P1 — SỔ RUN (UoW RIÊNG, COMMIT TRƯỚC KHI TÍNH) ═══════════════
        var runKey = TaoRunKey(from, to, trigger);

        Guid runId;

        using (var uowRun = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            var runHienCo = await _runs.FirstOrDefaultAsync(x => x.RunKey == runKey);

            if (runHienCo is not null)
            {
                if (runHienCo.TrangThai == TrangThaiChayDongCo.ThanhCong)
                {
                    // K06-08: cùng khoảng + cùng trigger đã xong ⇒ TRẢ run cũ; KHÔNG tính lại/ghi thêm.
                    await uowRun.CompleteAsync();

                    return runHienCo;
                }

                if (runHienCo.TrangThai == TrangThaiChayDongCo.DangChay)
                {
                    // K06-08b: không trả run chưa xong, không chạy chồng ⇒ 409/bận (UoW bỏ, không ghi gì).
                    throw new BusinessException(LockErrorCodes.KhongGianhDuocKhoa)
                        .WithData("runKey", runKey)
                        .WithData("lyDo", "Một lượt động cơ giá vốn cùng `RunKey` đang chạy ⇒ bận (không chạy chồng).");
                }

                // Trạng thái `Loi` ⇒ chạy lại attempt MỚI trên CÙNG dòng (K06-08b).
                runHienCo.BatDauLai(DateTime.UtcNow);
                await _runs.UpdateAsync(runHienCo, autoSave: false);
                runId = runHienCo.Id;
            }
            else
            {
                var moi = new CostEngineRun(
                    _guidGenerator.Create(), runKey, from, to, trigger, DateTime.UtcNow);

                await _runs.InsertAsync(moi, autoSave: false);
                runId = moi.Id;
            }

            // 🚨 COMMIT sổ run TRƯỚC khi chạy P2 ⇒ dù P2 rollback, dấu `DangChay`/`Loi` VẪN BỀN (D-324).
            await uowRun.CompleteAsync();
        }

        // ═══════════════ P2 — TÍNH + GHI GIÁ VỐN (UoW RIÊNG) ═══════════════
        ThongKe? ketQua = null;
        Exception? loi = null;

        using (var uowTinh = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            try
            {
                ketQua = await XuLyAsync(runId, from, to, choNhapLuiNgay, chinhSachLamTron);

                await uowTinh.CompleteAsync();
            }
            catch (Exception ex)
            {
                // KHÔNG `CompleteAsync` ⇒ rollback phần dở; KHÔNG đọc/ghi bảng run ở đây.
                loi = ex;
            }
        }

        // ═══════════════ P3 — CHỐT SỔ RUN (UoW MỚI, NẠP LẠI) ═══════════════
        CostEngineRun runChot;

        using (var uowChot = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            // 📌 Nạp LẠI từ DB — instance ở P1 đã detached sau khi UoW đó commit.
            runChot = await _runs.GetAsync(runId);

            if (loi is null && ketQua is not null)
            {
                runChot.HoanTat(
                    DateTime.UtcNow, ketQua.Upserted, ketQua.SkippedApproved, ketQua.FlaggedMissing);
            }
            else
            {
                runChot.ThatBai(DateTime.UtcNow, loi?.Message ?? "Lỗi không xác định.");
            }

            await _runs.UpdateAsync(runChot, autoSave: false);
            await uowChot.CompleteAsync();
        }

        if (loi is not null)
        {
            // Giữ nguyên stack trace gốc; endpoint/worker thấy đúng lỗi fail-closed.
            ExceptionDispatchInfo.Capture(loi).Throw();
        }

        return runChot;
    }

    private async Task<ThongKe> XuLyAsync(
        Guid runId, DateOnly from, DateOnly to, bool choNhapLuiNgay, IRoundingPolicy chinhSachLamTron)
    {
        var chuyenDongInventory = await _feed.LayChuyenDongAsync(from, to);

        // Ánh xạ DTO (Contracts) → kiểu GIÁ TRỊ nội bộ Domain Pricing (KHÔNG kéo entity Inventory).
        var chuyenDong = chuyenDongInventory.Select(AnhXa).ToList();

        var thongKe = new ThongKe();

        foreach (var nhom in chuyenDong
                     .GroupBy(x => x.Sku, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var sku = nhom.Key;

            await using var khoa = await _distributedLock.TryAcquireAsync(
                PricingLockKeys.GiaVonTheoSku(_currentTenant.Id, sku), ChoKhoaToiDa);

            if (khoa is null)
            {
                throw new BusinessException(LockErrorCodes.KhongGianhDuocKhoa)
                    .WithData("sku", sku)
                    .WithData("lyDo", "Không giành được khoá tính giá vốn theo SKU.");
            }

            var ketQua = TinhBinhQuanGiaQuyenDiDong.Tinh(nhom.ToList(), Currency.Vnd);

            if (ketQua.SkuThieuGia.Contains(sku))
            {
                thongKe.FlaggedMissing++;
            }

            await DoiSoatAsync(runId, sku, ketQua, choNhapLuiNgay, chinhSachLamTron, thongKe);
        }

        return thongKe;
    }

    private async Task DoiSoatAsync(
        Guid runId,
        string sku,
        KetQuaTinhGiaVon ketQua,
        bool choNhapLuiNgay,
        IRoundingPolicy chinhSachLamTron,
        ThongKe thongKe)
    {
        var hienCo = await _giaVon.GetListAsync(x => x.Sku == sku);

        foreach (var ky in ketQua.CacKy)
        {
            // 🔒 Làm tròn CHỈ ở biên ra (`XC-001`/`IRoundingPolicy`) — hàm thuần giữ số chính xác.
            var giaTri = chinhSachLamTron.Round(ky.GiaBinhQuan);

            var dong = hienCo.FirstOrDefault(x => x.EffectiveFrom == ky.EffectiveFrom);

            if (dong is null)
            {
                var moi = new CostPrice(
                    _guidGenerator.Create(), sku, giaTri, ky.EffectiveFrom, ky.EffectiveTo);

                moi.DanhDauDongCo(runId, DateTime.UtcNow);

                await _giaVon.InsertAsync(moi, autoSave: false);
                thongKe.Upserted++;

                continue;
            }

            if (!dong.DaDuyet)
            {
                // Kỳ CHƯA duyệt ⇒ cập nhật (idempotent). Không đụng dòng đã duyệt.
                dong.CapNhatGiaVonChuaDuyet(giaTri, ky.EffectiveTo, runId, DateTime.UtcNow);

                await _giaVon.UpdateAsync(dong, autoSave: false);
                thongKe.Upserted++;

                continue;
            }

            // ── Dòng ĐÃ DUYỆT: KHÔNG BAO GIỜ UPDATE (§5.4) ─────────────────────────────────────────
            // 📌 Chỉ quan tâm GIÁ TRỊ (không đụng `EffectiveTo` của dòng đã duyệt).
            if (dong.Amount.CompareTo(giaTri) == 0)
            {
                continue;
            }

            if (!choNhapLuiNgay)
            {
                // `ChoNhapLuiNgay = false` (mặc định): nhập lùi bị BỎ QUA cho định giá; số đã duyệt giữ nguyên.
                thongKe.SkippedApproved++;

                _logger.LogWarning(
                    "GIÁ VỐN: bỏ qua kỳ ĐÃ DUYỆT {Sku} từ {Tu} (nhập lùi — `Pricing:ChoNhapLuiNgay = false`).",
                    sku, ky.EffectiveFrom);

                continue;
            }

            // `ChoNhapLuiNgay = true` mà giá trị sẽ ĐỔI ⇒ NÉM, KHÔNG ghi đè.
            throw new BusinessException(PricingErrorCodes.GiaVonKhongTheTinhLai)
                .WithData("sku", sku)
                .WithData("effectiveFrom", ky.EffectiveFrom)
                .WithData("lyDo", "Tính lại đụng dòng ĐÃ DUYỆT và giá trị sẽ đổi ⇒ từ chối (D-318` C6).");
        }
    }

    private ChuyenDongGiaVon AnhXa(CostMovementDto dto)
    {
        Money? donGia = null;

        if (dto.DonGiaNhap is { } giaTri)
        {
            // As-built `UnitPrice` KHÔNG có cột tiền tệ và KHÔNG có FX (`D-321`): `null` = VND tenant.
            // Có tiền tệ khác ⇒ CHƯA hỗ trợ (không tự quy đổi — `XC-001`) ⇒ fail-closed.
            if (!string.IsNullOrWhiteSpace(dto.TienTeNhap)
                && !string.Equals(dto.TienTeNhap, Currency.Vnd.Code, StringComparison.OrdinalIgnoreCase))
            {
                throw LoiCauHinh(
                    "v1 chỉ hỗ trợ VND cho đơn giá nhập; tiền tệ khác chưa có nguồn/FX (D-321).",
                    "sku", dto.Sku, "tienTe", dto.TienTeNhap);
            }

            donGia = Money.Cua(giaTri, Currency.Vnd);
        }

        return new ChuyenDongGiaVon(
            dto.Sku,
            dto.NgayKinhDoanh,
            dto.OccurredAtUtc,
            (LoaiDongGiaVon)(int)dto.Loai,
            dto.SoLuongDelta,
            donGia,
            dto.ReferenceLineId);
    }

    private string TaoRunKey(DateOnly from, DateOnly to, string trigger)
        => $"cost:{_currentTenant.Id?.ToString() ?? "null"}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{trigger.Trim()}";

    private static BusinessException LoiCauHinh(string lyDo, params object[] duLieu)
    {
        var loi = new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe).WithData("lyDo", lyDo);

        for (var i = 0; i + 1 < duLieu.Length; i += 2)
        {
            loi = loi.WithData(duLieu[i].ToString()!, duLieu[i + 1]);
        }

        return loi;
    }

    private sealed class ThongKe
    {
        public int Upserted { get; set; }

        public int SkippedApproved { get; set; }

        public int FlaggedMissing { get; set; }
    }
}
