using eCommerce.Address.Caching;
using eCommerce.Address.Events;
using eCommerce.Address.Sync;
using eCommerce.Kernel.Locking;
using eCommerce.Kernel.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Address;

/// <summary>
/// **API tra cứu &amp; xác minh địa chỉ** (tầng Application của `ADDR-001`).
///
/// 🔒 QUY ƯỚC BẮT BUỘC: `[Authorize]` ở **CẤP CLASS** (quy ước #6, đã bị test reflection `TC-01` của
/// `XC-004` chặn nếu thiếu).
///
/// 🎯 VÌ SAO ĐÁNG LÀM ĐẦU TIÊN TRONG TẦNG APPLICATION: đây là **điểm nghẽn dữ liệu đầu vào** — mọi đơn
/// hàng, vận đơn và hoá đơn đều đi qua việc xác minh địa chỉ. Nếu tầng này trả `null`/mặc định khi
/// không tìm thấy thì **địa chỉ sai sẽ lan ra toàn hệ thống**. Vì vậy mọi nhánh đều **fail-closed** và
/// **không bao giờ** trả về đơn vị "gần đúng".
/// </summary>
[Authorize]
public class AdministrativeUnitAppService : ApplicationService
{
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;
    private readonly IRepository<CatalogSyncRun, Guid> _loNap;
    private readonly IUnitOfWorkManager _uow;
    private readonly IAbpDistributedLock _khoa;
    private readonly IClock _clock;
    private readonly AddressCatalogCache _cache;
    private readonly IDistributedEventBus _bus;
    private readonly ILogger<AdministrativeUnitAppService> _logger;
    private readonly eCommerceMetrics? _metrics;

    public AdministrativeUnitAppService(
        IRepository<AdministrativeUnit, Guid> donVi,
        IRepository<CatalogSyncRun, Guid> loNap,
        IUnitOfWorkManager uow,
        IAbpDistributedLock khoa,
        IClock clock,
        AddressCatalogCache cache,
        IDistributedEventBus bus,
        ILogger<AdministrativeUnitAppService> logger,
        eCommerceMetrics? metrics = null)
    {
        _donVi = donVi;
        _loNap = loNap;
        _uow = uow;
        _khoa = khoa;
        _clock = clock;
        _cache = cache;
        _bus = bus;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>Tra một đơn vị theo (phiên bản, cấp, mã). Ném lỗi nếu không tìm thấy.</summary>
    public async Task<AdministrativeUnitDto> TraDonViAsync(
        TraDonViRequest yeuCau,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        // Chuẩn hoá + xác thực (0002/0005) TRƯỚC truy vấn — giữ đúng hành vi trim của resolver.
        AdministrativeSchemaVersion.TheoSo(yeuCau.SchemaVersion);
        var ma = AdministrativeUnit.ChuanHoaMa(yeuCau.Level, yeuCau.Code);

        // Dùng lại ĐÚNG logic đã kiểm chứng ở tầng Domain (kèm phiên bản ⇒ không trả sai đơn vị).
        // 📌 Truy vấn CÓ ĐIỀU KIỆN (dùng tiền tố unique index) thay vì đọc cả bảng — NFR-001.
        var ds = await _donVi.GetListAsync(
            x => x.SchemaVersion == yeuCau.SchemaVersion
                 && x.Level == yeuCau.Level
                 && x.Code == ma,
            cancellationToken: cancellationToken);

        var donVi = AdministrativeUnitResolver.TraTheoMa(
            ds, yeuCau.SchemaVersion, yeuCau.Level, ma);

        return Chuyen(donVi);
    }

    /// <summary>
    /// Xác minh địa chỉ 2 cấp cho đơn **MỚI**.
    /// 🔒 **FAIL-CLOSED**: mọi nhánh không chắc chắn đều trả `HopLe = false` (không ném lỗi, để giao diện
    /// hiển thị lý do cho người dùng).
    /// </summary>
    public async Task<XacMinhDiaChiResult> XacMinhDiaChiAsync(
        XacMinhDiaChiRequest yeuCau,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        AdministrativeSchemaVersion.TheoSo(yeuCau.SchemaVersion);

        var tinhCode = yeuCau.TinhCode?.Trim() ?? string.Empty;
        var xaCode = yeuCau.XaCode?.Trim() ?? string.Empty;

        // Chỉ đọc 2 mã cần kiểm (không đọc cả bảng). `ThuTra` bên trong resolver vẫn FAIL-CLOSED.
        var ds = await _donVi.GetListAsync(
            x => x.SchemaVersion == yeuCau.SchemaVersion
                 && ((x.Level == AdministrativeLevel.Tinh && x.Code == tinhCode)
                     || (x.Level == AdministrativeLevel.Xa && x.Code == xaCode)),
            cancellationToken: cancellationToken);

        var kq = AdministrativeUnitResolver.KiemTraHaiCap(
            ds, yeuCau.SchemaVersion, tinhCode, xaCode);

        return new XacMinhDiaChiResult(
            kq.HopLe,
            kq.ErrorCode,
            kq.ThongDiep,
            kq.Tinh is null ? null : Chuyen(kq.Tinh),
            kq.Xa is null ? null : Chuyen(kq.Xa));
    }

    /// <summary>Lấy danh sách xã/phường của một tỉnh (cho dropdown chọn địa chỉ).</summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> LayXaTheoTinhAsync(
        int schemaVersion,
        string tinhCode,
        CancellationToken cancellationToken = default)
    {
        AdministrativeSchemaVersion.TheoSo(schemaVersion);
        var maTinh = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Tinh, tinhCode);

        var ds = await LayConAsync(schemaVersion, maTinh, cancellationToken);

        return ds.Where(x => x.Level == AdministrativeLevel.Xa)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 🚩 **Soi bẫy mã trùng giữa hai phiên bản** — dùng cho báo cáo kiểm tra dữ liệu.
    /// Trả về đơn vị xã tương ứng ở **cả hai** phiên bản, để thấy rõ chúng có thể **KHÁC NHAU**.
    /// 📌 Đây là **ngoại lệ có lý do** duy nhất của `TC-05` (`D-79`): nhận mã trần nhưng trả DTO có
    /// `SchemaVersion` cho **mỗi** dòng.
    /// </summary>
    public async Task<IReadOnlyList<MaXaHaiPhienBanDto>> SoSanhMaXaAsync(
        string maXa,
        CancellationToken cancellationToken = default)
    {
        var ma = maXa?.Trim() ?? string.Empty;

        var ds = await _donVi.GetListAsync(
            x => x.Level == AdministrativeLevel.Xa && x.Code == ma,
            cancellationToken: cancellationToken);

        var soSanh = AdministrativeUnitResolver.SoSanhMaXaGiuaCacPhienBan(ds, ma);

        return soSanh
            .OrderBy(kv => kv.Key)
            .Select(kv => new MaXaHaiPhienBanDto(kv.Key, kv.Value is null ? null : Chuyen(kv.Value)))
            .ToList();
    }

    /// <summary>
    /// **Danh sách tỉnh/thành** của một phiên bản schema (`D-79`, tên Anh). Rỗng ⇒ `[]` + log `Warning`
    /// (bảng chưa nạp **không phải** lỗi — SRS §12).
    /// </summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> GetProvincesAsync(
        int schemaVersion,
        CancellationToken cancellationToken = default)
    {
        AdministrativeSchemaVersion.TheoSo(schemaVersion);

        var ds = await LayConAsync(schemaVersion, null, cancellationToken);

        var tinh = ds.Where(x => x.Level == AdministrativeLevel.Tinh)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        if (tinh.Count == 0)
        {
            _logger.LogWarning(
                "Danh sách tỉnh của phiên bản {PhienBan} RỖNG — danh mục chưa được nạp? (ADDR-001 §12)",
                schemaVersion);
        }

        return tinh;
    }

    /// <summary>
    /// **Danh sách quận/huyện của một tỉnh** — **chỉ schema 3 cấp** (`v1`). `v2` ⇒ NÉM `0004`
    /// (cấp huyện đã bị bỏ từ 01/7/2025).
    /// </summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> GetDistrictsAsync(
        int schemaVersion,
        string provinceCode,
        CancellationToken cancellationToken = default)
    {
        var phienBan = AdministrativeSchemaVersion.TheoSo(schemaVersion);

        if (!phienBan.ChoPhepCapHuyen)
        {
            throw new BusinessException(AddressErrorCodes.HuyenKhongDuocPhepOCap2)
                .WithData("phienBan", schemaVersion);
        }

        var maTinh = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Tinh, provinceCode);
        var ds = await LayConAsync(schemaVersion, maTinh, cancellationToken);

        var huyen = ds.Where(x => x.Level == AdministrativeLevel.QuanHuyen)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        if (huyen.Count == 0)
        {
            _logger.LogWarning(
                "Tỉnh {MaTinh} (phiên bản {PhienBan}) không có quận/huyện nào — dữ liệu có thể thiếu (ADDR-001 §12).",
                maTinh, schemaVersion);
        }

        return huyen;
    }

    /// <summary>
    /// **Danh sách xã/phường của một quận/huyện** — **chỉ schema 3 cấp** (`v1`). `v2` ⇒ NÉM `0004`.
    /// </summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> GetWardsByDistrictAsync(
        int schemaVersion,
        string districtCode,
        CancellationToken cancellationToken = default)
    {
        var phienBan = AdministrativeSchemaVersion.TheoSo(schemaVersion);

        if (!phienBan.ChoPhepCapHuyen)
        {
            throw new BusinessException(AddressErrorCodes.HuyenKhongDuocPhepOCap2)
                .WithData("phienBan", schemaVersion);
        }

        var maHuyen = AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.QuanHuyen, districtCode);
        var ds = await LayConAsync(schemaVersion, maHuyen, cancellationToken);

        var xa = ds.Where(x => x.Level == AdministrativeLevel.Xa)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        if (xa.Count == 0)
        {
            _logger.LogWarning(
                "Huyện {MaHuyen} (phiên bản {PhienBan}) không có xã/phường nào — dữ liệu có thể thiếu (ADDR-001 §12).",
                maHuyen, schemaVersion);
        }

        return xa;
    }

    /// <summary>
    /// **NẠP DANH MỤC ĐVHC theo LÔ** (`D-78`, BR-009). Nhận `<see cref="ImportDatasetInput.Rows"/>` **đã tách
    /// khỏi định dạng file** — parser/tải tệp là WP sau (`D-93`), **không** suy đoán định dạng.
    ///
    /// 🔒 Quyền **riêng** `Addresses.Catalog.Import` (`D-238`) — không dùng `Addresses.Manage` của `ADDR-004`,
    /// **không** `[AllowAnonymous]` (NFR-010).
    /// 🔒 Validate **trước** khoá (`0005`/`0002` ⇒ 0 dòng lô); khoá **chung** `AddressLockKeys.CatalogImport`
    /// với job (`D-82`), bận ⇒ NÉM `eCommerce:Lock:0001` (`409`), **không** im lặng.
    /// 🔒 Lượt thành công ghi trong **UoW của lời gọi** (lô + dòng lô + ETO nguyên tử); lượt **bị chặn/lỗi**
    /// ghi dòng lô trong **UoW riêng `requiresNew`** và commit **trước khi** ném (BR-009, `D-78`).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.CatalogImport)]
    public async Task<CatalogSyncRunDto> ImportDatasetAsync(
        ImportDatasetInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // ── Validate TRƯỚC khoá ⇒ lượt sai không chiếm khoá và KHÔNG để lại dòng lô (TC-21 ca a) ──
        AdministrativeSchemaVersion.TheoSo(input.SchemaVersion);

        if (string.IsNullOrWhiteSpace(input.SourceName))
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe)
                .WithData("truong", nameof(input.SourceName))
                .WithData("lyDo", "SourceName rỗng");
        }

        // Cột `SourceName` là `varchar(512)` ⇒ chặn > 512 TRƯỚC khoá để trả 400 (không 500 lúc commit UoW)
        // và không để lại dòng lô rác; hàng rào chung ở `CatalogSyncRun.Ghi` cho nhánh worker.
        if (input.SourceName.Trim().Length > CatalogSyncRun.DoDaiSourceNameToiDa)
        {
            throw new BusinessException(AddressErrorCodes.MaKhongHopLe)
                .WithData("truong", nameof(input.SourceName))
                .WithData("lyDo", "SourceName vượt 512 ký tự");
        }

        var rows = (input.Rows ?? [])
            .Select(r => new DonViNguon(
                input.SchemaVersion,
                r.Level,
                r.Code ?? string.Empty,
                r.Name ?? string.Empty,
                r.ParentCode))
            .ToList();

        var danhMuc = new DanhMucNguon(rows, input.SourceName.Trim(), _clock.Now);

        // ── Khoá CHUNG với job (D-82); bận ⇒ 409 `eCommerce:Lock:0001`, KHÔNG `TimeoutException` ──
        await using var handle = await _khoa.TryAcquireAsync(
            AddressLockKeys.CatalogImport, TimeSpan.FromSeconds(5), cancellationToken);

        if (handle is null)
        {
            throw new BusinessException(LockErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", AddressLockKeys.CatalogImport);
        }

        var startedAtUtc = _clock.Now;
        var sourceHash = AddressSourceHash.Tinh(input.SchemaVersion, danhMuc.Nguon, rows);
        var importedBy = CurrentUser.Id is { } userId ? $"user:{userId}" : "user:unknown";

        try
        {
            var hienCo = await _donVi.GetListAsync(
                x => x.SchemaVersion == input.SchemaVersion,
                cancellationToken: cancellationToken);

            // Hàng rào 0b/1..5 DÙNG CHUNG với job — ném ⇒ UoW của lời gọi rollback, không áp gì.
            var keHoach = AdministrativeUnitSynchronizer.LapKeHoach(input.SchemaVersion, hienCo, danhMuc);
            var deThem = AdministrativeUnitSynchronizer.ApDung(
                keHoach, danhMuc.Nguon, tenantId: CurrentTenant.Id);

            if (deThem.Count > 0)
            {
                await _donVi.InsertManyAsync(deThem, autoSave: false, cancellationToken: cancellationToken);
            }

            var daSua = keHoach.ThayDois
                .Where(x => x is { Loai: not LoaiThayDoi.ThemMoi, HienCo: not null })
                .Select(x => x.HienCo!)
                .Distinct()
                .ToList();

            if (daSua.Count > 0)
            {
                await _donVi.UpdateManyAsync(daSua, autoSave: false, cancellationToken: cancellationToken);
            }

            var importedAtUtc = _clock.Now;

            var lo = CatalogSyncRun.Ghi(
                input.SchemaVersion,
                CatalogSyncTrigger.Import,
                danhMuc.Nguon,
                sourceHash,
                startedAtUtc,
                importedAtUtc,
                importedBy,
                CatalogSyncOutcome.DaApDung,
                errorCode: null,
                errorType: null,
                keHoach.SoThemMoi,
                keHoach.SoDoiTen,
                keHoach.SoMoLai,
                keHoach.SoDong,
                tenantId: CurrentTenant.Id);

            await _loNap.InsertAsync(lo, autoSave: false, cancellationToken: cancellationToken);

            // ParentCodes = HỢP danh mục MỚI và danh mục HIỆN CÓ trước commit (tỉnh vừa biến mất vẫn phải xoá).
            var parentCodes = TapParentCode(rows, hienCo);

            await _bus.PublishAsync(
                new AddressCacheInvalidatedEto
                {
                    TenantId = CurrentTenant.Id,
                    SchemaVersion = input.SchemaVersion,
                    ParentCodes = parentCodes
                },
                useOutbox: true);

            await DangKySauCommitAsync(input.SchemaVersion, parentCodes, "import", "applied");

            return Chuyen(lo);
        }
        catch (BusinessException ex) when (AdministrativeUnitSynchronizer.HangRaoChan.Contains(ex.Code))
        {
            // Dòng lô TỪ CHỐI phải nằm ở UoW RIÊNG và commit TRƯỚC khi ném, nếu không UoW bao lô rollback xoá nó.
            await GhiLoThatBaiAsync(
                input.SchemaVersion, CatalogSyncOutcome.BiChan, ex.Code, errorType: null,
                danhMuc.Nguon, sourceHash, startedAtUtc, importedBy, CurrentTenant.Id);

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "import"), Tag("outcome", "blocked"));

            _logger.LogError(
                "Import danh mục ĐVHC BỊ TỪ CHỐI (mã {Ma}). KHÔNG áp dụng thay đổi nào. Dữ liệu: {DuLieu}",
                ex.Code,
                string.Join("; ", ex.Data.Keys.Cast<object>().Select(k => $"{k}={ex.Data[k]}")));

            throw;
        }
        catch (Exception ex)
        {
            await GhiLoThatBaiAsync(
                input.SchemaVersion, CatalogSyncOutcome.Loi, errorCode: null, ex.GetType().FullName,
                danhMuc.Nguon, sourceHash, startedAtUtc, importedBy, CurrentTenant.Id);

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "import"), Tag("outcome", "error"));

            _logger.LogError(ex, "Import danh mục ĐVHC thất bại (lỗi không mong đợi). Không áp dụng thay đổi nào.");

            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc danh sách con qua cache; `parentCode = null` ⇒ danh sách tỉnh.</summary>
    private async Task<IReadOnlyList<AdministrativeUnitDto>> LayConAsync(
        int schemaVersion,
        string? parentCode,
        CancellationToken cancellationToken)
    {
        return await _cache.LayConAsync(
            schemaVersion,
            parentCode,
            async token =>
            {
                var ds = await _donVi.GetListAsync(
                    x => x.SchemaVersion == schemaVersion
                         && x.ParentCode == parentCode
                         && x.IsActive,
                    cancellationToken: token);

                return ds.OrderBy(x => x.Code, StringComparer.Ordinal)
                    .Select(Chuyen)
                    .ToList();
            },
            cancellationToken);
    }

    /// <summary>Hợp `parentCode` của danh mục mới và danh mục hiện có (phần tử `null` = danh sách tỉnh).</summary>
    private static List<string?> TapParentCode(
        IEnumerable<DonViNguon> moi,
        IEnumerable<AdministrativeUnit> hienCo)
    {
        var tap = new List<string?> { null };
        var daCo = new HashSet<string?>(StringComparer.Ordinal) { null };

        void Them(string? parentCode)
        {
            if (string.IsNullOrWhiteSpace(parentCode))
            {
                return;   // `null` đã có trong tập ⇒ danh sách tỉnh
            }

            var chuan = parentCode.Trim();

            if (daCo.Add(chuan))
            {
                tap.Add(chuan);
            }
        }

        foreach (var dv in moi)
        {
            Them(dv.ParentCode);
        }

        foreach (var dv in hienCo)
        {
            Them(dv.ParentCode);
        }

        return tap;
    }

    private async Task DangKySauCommitAsync(
        int schemaVersion,
        IReadOnlyList<string?> parentCodes,
        string trigger,
        string outcome)
    {
        async Task XuLyAsync()
        {
            try
            {
                await _cache.XoaAsync(schemaVersion, parentCodes, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Xoá cache cục bộ lỗi KHÔNG làm hỏng lô (dữ liệu đã commit); TTL 24 h là lưới chặn.
                _logger.LogError(ex,
                    "Đã nạp danh mục ĐVHC nhưng KHÔNG xoá được cache cục bộ — dữ liệu cũ có thể còn tới hết TTL.");
            }

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", trigger), Tag("outcome", outcome));
        }

        var uow = _uow.Current;

        if (uow is not null)
        {
            // Chạy SAU commit: cache không được xoá trước khi dữ liệu mới thực sự nhìn thấy được.
            uow.OnCompleted(XuLyAsync);
            return;
        }

        await XuLyAsync();
    }

    /// <summary>
    /// Ghi dòng lô cho lượt **bị chặn/lỗi** trong UoW **riêng** `requiresNew: true`, commit **trước khi** ném.
    /// Ghi lỗi ở đây **không che** exception gốc (chỉ log riêng, khuôn `D-78`).
    /// </summary>
    private async Task GhiLoThatBaiAsync(
        int schemaVersion,
        CatalogSyncOutcome outcome,
        string? errorCode,
        string? errorType,
        string sourceName,
        string? sourceHash,
        DateTime startedAtUtc,
        string importedBy,
        Guid? tenantId)
    {
        try
        {
            using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

            var lo = CatalogSyncRun.Ghi(
                schemaVersion,
                CatalogSyncTrigger.Import,
                sourceName,
                sourceHash,
                startedAtUtc,
                _clock.Now,
                importedBy,
                outcome,
                errorCode,
                errorType,
                soThemMoi: 0,
                soDoiTen: 0,
                soMoLai: 0,
                soDong: 0,
                tenantId: tenantId);

            await _loNap.InsertAsync(lo, autoSave: true, cancellationToken: CancellationToken.None);
            await uow.CompleteAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "KHÔNG ghi được dòng lô nạp danh mục ĐVHC cho lượt {Outcome} — mất dấu vết lượt nạp (BR-009).",
                outcome);
        }
    }

    private static AdministrativeUnitDto Chuyen(AdministrativeUnit u)
        => new(u.Id, u.SchemaVersion, u.Level, u.Code, u.Name, u.ParentCode, u.IsActive);

    private static CatalogSyncRunDto Chuyen(CatalogSyncRun lo)
        => new(
            lo.Id,
            lo.SchemaVersion,
            lo.Trigger,
            lo.SourceName,
            lo.SourceHash,
            lo.StartedAtUtc,
            lo.ImportedAtUtc,
            lo.ImportedBy,
            lo.Outcome,
            lo.ErrorCode,
            lo.SoThemMoi,
            lo.SoDoiTen,
            lo.SoMoLai,
            lo.SoDong);

    private static KeyValuePair<string, object?> Tag(string ten, object? giaTri)
        => new(ten, giaTri);
}
