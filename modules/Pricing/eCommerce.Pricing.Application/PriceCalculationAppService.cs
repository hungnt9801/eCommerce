using System.Text.Json;
using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **TÍNH TIỀN CHO CẢ GIỎ** (`PRC-002` §4.2 — lắp **B1→B8**). Đây là **điểm vào** mà `SAL-002`/checkout gọi.
///
/// **Pipeline (thứ tự BẮT BUỘC — ADR-03: chạy hết rồi mới so sánh, CẤM áp mù):**
///   • **B1** snapshot chính sách (làm tròn · thiếu giá · `at`) — đọc **một lần** (ADR-08)
///   • **B2** giá cơ sở **4 cấp** (`GiaCoSoResolver` — `PRC-001` D7, đã kiểm chứng)
///   • **B3** thu thập **ứng viên BẬC THANG** (`ChonBacThang` — `PRC-002` ADR-02; trùng `MinQuantity` ⇒ NÉM `0008`)
///   • **B4** điều chỉnh/khuyến mãi — **chưa có** (chờ `PRO-*`); ghi rõ ở `prc-002` §18.3
///   • **B5** chọn **đơn giá cuối** (`ChonDonGiaCuoi`: bậc thang thắng giá cơ sở khi hiệu lực)
///   • **B6–B8** tính tiền: **R1** tròn đơn giá → **R2/R3** phân bổ giảm giá/phí → **R4** thuế từng dòng → **tổng = Σ dòng**
///
/// ⚠️ **Nạp theo LÔ (NFR-002)**: 5 truy vấn cho **cả giỏ** (ChannelPrice theo tập SKU · bảng giá `Active` ·
/// dòng giá · bộ bậc thang `Active` · bậc thang theo tập SKU) — **không** truy vấn trong vòng lặp.
/// 🔍 Marker `TINH-GIO-BAT-DAU`/`TINH-GIO-KET-THUC` để **đếm được** số lệnh EF thật.
/// </summary>
[Authorize(PricingPermissions.Default)]
public class PriceCalculationAppService : ApplicationService, IPriceCalculationAppService
{
    private readonly IRepository<ChannelPrice, Guid> _giaTheoKenhRepository;
    private readonly IRepository<PriceList, Guid> _bangGiaRepository;
    private readonly IRepository<PriceListItem, Guid> _dongGiaRepository;
    private readonly IRepository<TierPriceSet, Guid> _boBacRepository;
    private readonly IRepository<TierPrice, Guid> _bacRepository;
    private readonly IRepository<PriceCalculationRecord, Guid> _soGiaiTrinhRepository;

    public PriceCalculationAppService(
        IRepository<ChannelPrice, Guid> giaTheoKenhRepository,
        IRepository<PriceList, Guid> bangGiaRepository,
        IRepository<PriceListItem, Guid> dongGiaRepository,
        IRepository<TierPriceSet, Guid> boBacRepository,
        IRepository<TierPrice, Guid> bacRepository,
        IRepository<PriceCalculationRecord, Guid> soGiaiTrinhRepository)
    {
        _giaTheoKenhRepository = giaTheoKenhRepository;
        _bangGiaRepository = bangGiaRepository;
        _dongGiaRepository = dongGiaRepository;
        _boBacRepository = boBacRepository;
        _bacRepository = bacRepository;
        _soGiaiTrinhRepository = soGiaiTrinhRepository;
    }

    public async Task<GioTinhTienDto> TinhGioAsync(TinhGioInput input)
    {
        if (input.Items.Count == 0)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Giỏ phải có ít nhất một dòng.");
        }

        var khoa = input.IdempotencyKey.Trim();

        // ── 🔒 IDEMPOTENCY (ADR-13): gọi lại cùng khoá ⇒ trả ĐÚNG kết quả ĐÃ LƯU, KHÔNG tính lại ──────────
        // 📌 Vì sao tra TRƯỚC khi tính: nếu tính rồi mới tra thì lần gọi lại vẫn **tính lại** (tốn kém), và tệ hơn —
        // nếu bảng giá/khuyến mãi đổi ở giữa thì **hai kết quả khác nhau cho cùng một đơn** ⇒ đơn **không giải
        // trình được** và không biết số nào là số đã báo khách.
        var daCo = await _soGiaiTrinhRepository.FirstOrDefaultAsync(x => x.IdempotencyKey == khoa);

        if (daCo is not null)
        {
            Logger.LogInformation(
                "IDEMPOTENT: khoá {Khoa} đã có bản ghi {Id} (tính lúc {At}, tổng {Tong}) ⇒ trả lại kết quả đã lưu.",
                khoa, daCo.Id, daCo.CalculatedAt, daCo.Total);

            return JsonSerializer.Deserialize<GioTinhTienDto>(daCo.Payload)!;
        }

        return await TinhMoiAsync(input, khoa);
    }

    private async Task<GioTinhTienDto> TinhMoiAsync(TinhGioInput input, string khoa)
    {
        // ── B1: SNAPSHOT chính sách (một lần cho cả lần tính — ADR-08) ─────────────────────────
        var chinhSach = new ChinhSachSnapshot(
            MaChinhSachLamTron: ChinhSachLamTronMacDinh.Ten,
            ChinhSachLamTron: ChinhSachLamTronMacDinh,
            ChinhSachThieuGia: ChinhSachThieuGia.NemLoi,
            At: input.At ?? Clock.Now);

        var tapSku = input.Items.Select(x => x.Sku.Trim()).Distinct(StringComparer.Ordinal).ToList();

        Logger.LogInformation("TINH-GIO-BAT-DAU: {SoDong} dòng, {SoSku} SKU, tại {At}.",
            input.Items.Count, tapSku.Count, chinhSach.At);

        // ── NẠP THEO LÔ (5 truy vấn cho cả giỏ) ────────────────────────────────────────────────
        var giaTheoKenh = await _giaTheoKenhRepository.GetListAsync(x => tapSku.Contains(x.Sku));

        var bangGiaHieuLuc = (await _bangGiaRepository.GetListAsync(x => x.Status == PriceListStatus.Active))
            .Where(x => x.CoHieuLucTai(chinhSach.At)).ToList();

        var idBangGia = bangGiaHieuLuc.Select(x => x.Id).ToList();
        var dongGia = idBangGia.Count == 0
            ? []
            : await _dongGiaRepository.GetListAsync(x => idBangGia.Contains(x.PriceListId) && tapSku.Contains(x.Sku));

        var boBacHieuLuc = (await _boBacRepository.GetListAsync(x => x.Status == PriceListStatus.Active))
            .Where(x => x.CoHieuLucTai(chinhSach.At)).ToList();

        var idBoBac = boBacHieuLuc.Select(x => x.Id).ToList();
        var cacBac = idBoBac.Count == 0
            ? []
            : await _bacRepository.GetListAsync(x => idBoBac.Contains(x.TierPriceSetId) && tapSku.Contains(x.Sku));

        Logger.LogInformation(
            "TINH-GIO-KET-THUC: {SoGiaKenh} giá kênh, {SoBangGia} bảng giá, {SoDongGia} dòng giá, " +
            "{SoBoBac} bộ bậc thang, {SoBac} bậc.",
            giaTheoKenh.Count, bangGiaHieuLuc.Count, dongGia.Count, boBacHieuLuc.Count, cacBac.Count);

        var cacDongCanTinh = new List<DongCanTinh>(input.Items.Count);
        var chiTiet = new List<ChiTietDongDto>(input.Items.Count);

        foreach (var item in input.Items)
        {
            var sku = item.Sku.Trim();
            var kenh = input.ChannelCode.Trim();

            // ── B2: giá cơ sở 4 cấp ────────────────────────────────────────────────────────────
            var ungVien = new List<UngVienGia>();

            foreach (var gp in giaTheoKenh.Where(x => x.Sku == sku && x.ChannelCode == kenh
                                                      && x.CoHieuLucTai(chinhSach.At)
                                                      && x.MinQuantity <= item.Quantity))
            {
                if (gp.LaTheoShop && gp.ChannelShopId == input.ChannelShopId)
                {
                    ungVien.Add(new UngVienGia(CapGia.GiaTheoShop, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
                }
                else if (!gp.LaTheoShop)
                {
                    ungVien.Add(new UngVienGia(CapGia.GiaTheoKenh, gp.Amount, gp.Priority, $"ChannelPrice:{gp.Id}"));
                }
            }

            var bangGiaKhop = bangGiaHieuLuc
                .Where(x => x.Scope == PriceListScope.All
                            || (x.Scope == PriceListScope.Channel && x.ChannelCode == kenh)
                            || (x.Scope == PriceListScope.ChannelShop && x.ChannelCode == kenh
                                && x.ChannelShopId == input.ChannelShopId))
                .ToDictionary(x => x.Id, x => (x.Priority, x.Code));

            foreach (var dong in dongGia.Where(x => x.Sku == sku && bangGiaKhop.ContainsKey(x.PriceListId)))
            {
                var (uuTien, maBangGia) = bangGiaKhop[dong.PriceListId];
                ungVien.Add(new UngVienGia(CapGia.BangGia, dong.Amount, uuTien, $"PriceList:{maBangGia}#{dong.Sku}"));
            }

            var loaiTien = item.ListPriceCurrency is not null
                ? Currency.FromCode(item.ListPriceCurrency)
                : bangGiaHieuLuc.Count > 0 ? bangGiaHieuLuc[0].Currency : Currency.Vnd;

            if (item.ListPrice is { } giaNiemYet)
            {
                ungVien.Add(new UngVienGia(CapGia.GiaNiemYet, Money.Cua(giaNiemYet, loaiTien), 0, "ListPrice:caller"));
            }

            var giaCoSo = GiaCoSoResolver.Giai(
                ungVien, loaiTien, chinhSach.ChinhSachThieuGia, sku, kenh, chinhSach.At);

            // ── B3: ứng viên BẬC THANG (theo phạm vi: shop > kênh > mọi kênh) ───────────────────
            var bacThang = ChonBacThangTheoPhamVi(
                sku, item.Quantity, kenh, input.ChannelShopId, boBacHieuLuc, cacBac);

            // ── B5: chọn đơn giá cuối ──────────────────────────────────────────────────────────
            var donGiaCuoi = ChonDonGiaCuoi.Chon(
                giaCoSo.Amount, giaCoSo.Nguon, giaCoSo.Cap.ToString(), bacThang);

            cacDongCanTinh.Add(new DongCanTinh(sku, item.Quantity, donGiaCuoi.DonGia, item.TaxRate));

            chiTiet.Add(new ChiTietDongDto
            {
                Sku = sku,
                Quantity = item.Quantity,
                BaseLevel = giaCoSo.Cap.ToString(),
                BaseSource = giaCoSo.Nguon,
                UnitPriceBeforeRounding = donGiaCuoi.DonGia.Amount,
                WinningLevel = donGiaCuoi.Cap,
                WinningSource = donGiaCuoi.Nguon,
                TierApplied = bacThang is not null,
                AllCandidates = giaCoSo.TatCaUngVien
                    .Select(x => new ResolvedPriceCandidateDto
                    {
                        Level = x.Cap.ToString(), Amount = x.Amount.Amount, Priority = x.Priority, Source = x.Nguon
                    })
                    .ToList()
            });
        }

        // ── B6–B8: tính tiền (4 điểm làm tròn) ────────────────────────────────────────────────
        var tien = TinhTienDon.Tinh(
            cacDongCanTinh,
            Money.Cua(input.OrderDiscount, LoaiTien(cacDongCanTinh)),
            Money.Cua(input.OrderFee, LoaiTien(cacDongCanTinh)),
            chinhSach.ChinhSachLamTron);

        for (var i = 0; i < chiTiet.Count; i++)
        {
            var d = tien.CacDong[i];
            chiTiet[i].UnitPrice = d.DonGiaSauLamTron.Amount;
            chiTiet[i].LineSubtotal = d.ThanhTienTruocGiam.Amount;
            chiTiet[i].AllocatedDiscount = d.GiamGiaDuocPhanBo.Amount;
            chiTiet[i].AllocatedFee = d.PhiDuocPhanBo.Amount;
            chiTiet[i].Tax = d.Thue.Amount;
            chiTiet[i].LineTotal = (d.ThanhTienSauGiam + d.Thue).Amount;
        }

        Logger.LogInformation(
            "Tính giỏ xong: tổng trước giảm {Truoc}, giảm {Giam}, phí {Phi}, thuế {Thue}, THANH TOÁN {Tong} " +
            "(chính sách làm tròn: {CS}).",
            tien.TongTruocGiam.Amount, tien.TongGiamGia.Amount, tien.TongPhi.Amount, tien.TongThue.Amount,
            tien.TongThanhToan.Amount, tien.MaChinhSachLamTron);

        var ketQua = new GioTinhTienDto
        {
            CurrencyCode = tien.TongThanhToan.Currency.Code,
            RoundingPolicyCode = tien.MaChinhSachLamTron,
            Subtotal = tien.TongTruocGiam.Amount,
            TotalDiscount = tien.TongGiamGia.Amount,
            TotalFee = tien.TongPhi.Amount,
            TotalTax = tien.TongThue.Amount,
            Total = tien.TongThanhToan.Amount,
            Lines = chiTiet
        };

        // ── 🔒 GHI SỔ GIẢI TRÌNH (append-only, ADR-13) ────────────────────────────────────────
        // 📌 Ghi **sau khi** tính xong và **trước khi** trả: nếu ghi sau khi trả mà request đứt thì đơn có giá
        // nhưng **không có bằng chứng** vì sao. Bản ghi chứa **payload đầy đủ** (từng dòng + ứng viên + cấp thắng).
        var soGiaiTrinh = new PriceCalculationRecord(
            GuidGenerator.Create(),
            khoa,
            input.ChannelCode.Trim(),
            input.ChannelShopId,
            chinhSach.At,
            ketQua.RoundingPolicyCode,
            ketQua.CurrencyCode,
            ketQua.Subtotal, ketQua.TotalDiscount, ketQua.TotalFee, ketQua.TotalTax, ketQua.Total,
            JsonSerializer.Serialize(ketQua));

        await _soGiaiTrinhRepository.InsertAsync(soGiaiTrinh, autoSave: true);

        Logger.LogInformation("Đã GHI SỔ GIẢI TRÌNH {Id} cho khoá {Khoa} (append-only — không sửa, không xoá).",
            soGiaiTrinh.Id, khoa);

        return ketQua;
    }

    /// <summary>Chính sách làm tròn mặc định (VND = 0 chữ số) — cấu hình được là việc của D14/§18.3.</summary>
    private static readonly IRoundingPolicy ChinhSachLamTronMacDinh = new RoundHalfAwayFromZeroPolicy();

    private static Currency LoaiTien(IReadOnlyList<DongCanTinh> cacDong)
        => cacDong.Count > 0 ? cacDong[0].DonGiaChuaLamTron.Currency : Currency.Vnd;

    /// <summary>
    /// **B3** — chọn bậc thang theo **phạm vi**: ưu tiên bộ khớp **shop** → **kênh** → **mọi kênh**.
    /// ⚠️ Nếu **nhiều bộ cùng phạm vi** cùng hiệu lực, lấy bộ **`Priority` lớn nhất**; nếu **bằng nhau** ⇒ chọn
    /// theo `Id` (ổn định) — **không** để kết quả phụ thuộc thứ tự DB trả về. Ghi rõ ở `prc-002` §18.3.
    /// </summary>
    private static ChonBacThang.Bac? ChonBacThangTheoPhamVi(
        string sku, int quantity, string kenh, Guid? shopId,
        IReadOnlyList<TierPriceSet> boBacHieuLuc, IReadOnlyList<TierPrice> cacBac)
    {
        // 📌 `ChannelShopId` đã có từ lát cắt này ⇒ bộ bậc thang theo SHOP được phân biệt **đúng** (§18.3 #2 đã đóng).
        // Không tự "đoán" shop: đoán sai ⇒ áp bậc thang của shop A cho shop B ⇒ **bán sai giá**.
        var khop = boBacHieuLuc
            .Where(b => b.Scope == PriceListScope.All
                        || (b.Scope == PriceListScope.Channel && b.ChannelCode == kenh)
                        || (b.Scope == PriceListScope.ChannelShop && b.ChannelCode == kenh
                            && b.ChannelShopId == shopId))
            .OrderBy(b => b.Scope == PriceListScope.ChannelShop ? 0 : b.Scope == PriceListScope.Channel ? 1 : 2)
            .ThenByDescending(b => b.Priority)
            .ThenBy(b => b.Id)
            .ToList();

        foreach (var bo in khop)
        {
            var bacCuaBo = cacBac
                .Where(x => x.TierPriceSetId == bo.Id && x.Sku == sku)
                .Select(x => new ChonBacThang.Bac(x.MinQuantity, x.Amount, $"TierPriceSet:{bo.Code}#{x.MinQuantity}"))
                .ToList();

            if (bacCuaBo.Count == 0)
            {
                continue;
            }

            var chon = ChonBacThang.Chon(quantity, bacCuaBo, sku);

            if (chon is not null)
            {
                return chon;
            }
        }

        return null;
    }

    /// <summary>
    /// 🔍 **TRA SỔ GIẢI TRÌNH** theo `IdempotencyKey` — trả **đúng kết quả đã tính** (payload đầy đủ).
    /// 📌 Đây là đường trả lời *"vì sao đơn này giá này?"* bằng **DỮ LIỆU**. Nếu không có đường tra thì sổ
    /// ghi ra **không ai đọc được** ⇒ bằng chứng tồn tại mà **không dùng được**.
    /// </summary>
    public async Task<GioTinhTienDto> GetSoGiaiTrinhAsync(string idempotencyKey)
    {
        var khoa = (idempotencyKey ?? string.Empty).Trim();

        var banGhi = await _soGiaiTrinhRepository.FirstOrDefaultAsync(x => x.IdempotencyKey == khoa)
            ?? throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Không có bản ghi sổ giải trình cho khoá này.")
                .WithData("idempotencyKey", khoa);

        Logger.LogInformation("Tra sổ giải trình khoá {Khoa}: bản ghi {Id}, tổng {Tong}, tính lúc {At}.",
            khoa, banGhi.Id, banGhi.Total, banGhi.CalculatedAt);

        return JsonSerializer.Deserialize<GioTinhTienDto>(banGhi.Payload)!;
    }

    /// <summary>Liệt kê sổ giải trình (tóm tắt, mới nhất trước) — để đối soát. (Tên bắt đầu bằng `Get` ⇒ ABP map thành **GET** — đúng quy ước: tra cứu KHÔNG được là POST.)</summary>
    public async Task<List<PriceCalculationRecordDto>> GetLichSuGiaiTrinhAsync(DateTime? tuNgay = null, DateTime? denNgay = null, int toiDa = 50)
    {
        var soLuong = Math.Clamp(toiDa, 1, 200);

        var ds = await _soGiaiTrinhRepository.GetListAsync();

        return ds
            .Where(x => tuNgay is null || x.CalculatedAt >= tuNgay.Value)
            .Where(x => denNgay is null || x.CalculatedAt <= denNgay.Value)
            .OrderByDescending(x => x.CalculatedAt)
            .ThenBy(x => x.Id)
            .Take(soLuong)
            .Select(x => new PriceCalculationRecordDto
            {
                Id = x.Id,
                IdempotencyKey = x.IdempotencyKey,
                ChannelCode = x.ChannelCode,
                ChannelShopId = x.ChannelShopId,
                CalculatedAt = x.CalculatedAt,
                RoundingPolicyCode = x.RoundingPolicyCode,
                CurrencyCode = x.CurrencyCode,
                Subtotal = x.Subtotal,
                TotalDiscount = x.TotalDiscount,
                TotalFee = x.TotalFee,
                TotalTax = x.TotalTax,
                Total = x.Total,
                CreationTime = x.CreationTime
            })
            .ToList();
    }
}
