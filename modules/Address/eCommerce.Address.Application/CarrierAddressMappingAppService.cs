using eCommerce.Address.Carrier;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Address;

// ─────────────────────────── DTO ───────────────────────────

public sealed record CarrierAddressMappingDto(
    Guid Id,
    string CarrierCode,
    string CarrierLevel,
    string CarrierAddressCode,
    string CarrierAddressName,
    Guid OurAdministrativeUnitId,
    CarrierMatchMethod MatchMethod,
    CarrierMappingStatus Status,
    DateTime SyncedAtUtc);

public sealed record TraMaHangRequest(string CarrierCode, string CarrierAddressCode);

public sealed record KetQuaTraMaHangDto(
    CarrierAddressMappingDto AnhXa,
    AdministrativeUnitDto DonVi);

public sealed record PhatHienSaiLechRequest(string CarrierCode, Dictionary<string, string> DanhMucMoi);

public sealed record SaiLechDto(LoaiSaiLech Loai, string CarrierAddressCode, string? MoTa);

/// <summary>
/// Đầu vào **danh sách mã hãng chưa được ánh xạ** của lượt `DaApDung` gần nhất (`D-88`).
/// 📌 Record riêng (không `PagedResultRequestDto`) để route là `GET` với query đơn giản, không kéo `Sorting`
/// không dùng (`ADDR-002` §3.2) — `D-88` chỉ ràng buộc "phân trang + `TotalCount`".
/// </summary>
public sealed record GetUnmappedCodesRequest(string CarrierCode, int SkipCount = 0, int MaxResultCount = 100);

/// <summary>Một mã hãng **chưa** có ánh xạ (cấp + mã + tên phía hãng — `D-88`).</summary>
public sealed record UnmappedCarrierCodeDto(string CarrierLevel, string CarrierAddressCode, string CarrierAddressName);

/// <summary>
/// Kết quả danh sách mã chưa ánh xạ. 🔒 `SyncRunId = null` **và** `LatestRunOutcome = null` nghĩa là
/// **"chưa có lượt đồng bộ"** (`D-236`) — trạng thái **cấu hình**, **không** phải lỗi ⇒ **không** ném.
/// `TotalCount` là số **sau** khi trừ ánh xạ hiện có (có thể **nhỏ hơn** `SoCanNguoiAnhXa` của lượt).
/// </summary>
public sealed record UnmappedCodesResultDto(
    IReadOnlyList<UnmappedCarrierCodeDto> Items,
    int TotalCount,
    Guid? SyncRunId,
    DateTime? FinishedAtUtc,
    CarrierAddressSyncOutcome? LatestRunOutcome);

// ─────────────────────────── App service ───────────────────────────

/// <summary>
/// **API vận hành cho `ADDR-002`** — tra mã địa chỉ của hãng, xem danh sách **chưa xác minh**,
/// xác minh, và đối chiếu khi hãng đồng bộ lại danh mục.
///
/// 🔒 `[Authorize]` ở CẤP CLASS (quy ước #6).
///
/// 🎯 VÌ SAO MÀN HÌNH NÀY LÀ BẮT BUỘC (không phải "tiện ích"): ánh xạ **chưa xác minh** thì
/// **không dùng được** (fail-closed). Nếu không có chỗ để con người **xác minh**, thì toàn bộ ánh xạ sẽ
/// **mãi mãi ở trạng thái chưa xác minh** ⇒ không tạo được vận đơn nào. Tức quy tắc an toàn **phải đi kèm
/// công cụ vận hành**, nếu không nó biến thành **chặn cứng**.
/// </summary>
[Authorize]
public class CarrierAddressMappingAppService : ApplicationService
{
    /// <summary>Trần `MaxResultCount` của `GetUnmappedCodesAsync` (`D-88` ③).</summary>
    private const int MaxResultCountToiDa = 1000;

    private readonly IRepository<CarrierAddressMapping, Guid> _anhXa;
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;
    private readonly IRepository<CarrierAddressSyncRun, Guid> _loDongBo;
    private readonly IRepository<CarrierAddressSyncRunUnmappedCode, Guid> _dongCon;
    private readonly ICarrierAddressResolver _resolver;

    public CarrierAddressMappingAppService(
        IRepository<CarrierAddressMapping, Guid> anhXa,
        IRepository<AdministrativeUnit, Guid> donVi,
        IRepository<CarrierAddressSyncRun, Guid> loDongBo,
        IRepository<CarrierAddressSyncRunUnmappedCode, Guid> dongCon,
        ICarrierAddressResolver resolver)
    {
        _anhXa = anhXa;
        _donVi = donVi;
        _loDongBo = loDongBo;
        _dongCon = dongCon;
        _resolver = resolver;
    }

    /// <summary>
    /// Tra mã địa chỉ của hãng → đơn vị hành chính của ta.
    /// 🚨 Ném lỗi nếu chưa xác minh (`0502`), không tìm thấy (`0501`), trùng lặp (`0503`),
    /// hoặc đơn vị đích đã đóng (`0504`) — **fail-closed**, không đoán.
    /// </summary>
    public async Task<KetQuaTraMaHangDto> TraMaHangAsync(TraMaHangRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var tatCaAnhXa = await _anhXa.GetListAsync();
        var tatCaDonVi = await _donVi.GetListAsync();

        var kq = CarrierAddressMappingResolver.Tra(
            tatCaAnhXa, tatCaDonVi, yeuCau.CarrierCode, yeuCau.CarrierAddressCode);

        return new KetQuaTraMaHangDto(Chuyen(kq.AnhXa), ChuyenDonVi(kq.DonVi));
    }

    /// <summary>Danh sách ánh xạ **cần người xử lý** (chưa xác minh hoặc nghi ngờ) — việc hằng ngày của vận hành.</summary>
    public async Task<IReadOnlyList<CarrierAddressMappingDto>> LayChuaXacMinhAsync(string? carrierCode = null)
    {
        var tatCa = await _anhXa.GetListAsync();

        return CarrierAddressMappingResolver
            .LayChuaXacMinh(tatCa, carrierCode)
            .Select(Chuyen)
            .ToList();
    }

    /// <summary>
    /// Xác minh một ánh xạ ⇒ từ đó mới dùng được để tạo vận đơn.
    /// 🔒 Cần permission riêng `Addresses.CarrierMapping.Verify` (`D-85`): xác minh ánh xạ **mở khoá vận đơn
    /// cho cả tenant** ⇒ không dùng chung `[Authorize]` cấp class.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.CarrierMappingVerify)]
    public async Task<CarrierAddressMappingDto> XacMinhAsync(Guid id, CarrierMatchMethod? cachKhop = null)
    {
        var anhXa = await _anhXa.GetAsync(id);

        anhXa.XacMinh(cachKhop);
        await _anhXa.UpdateAsync(anhXa, autoSave: true);

        return Chuyen(anhXa);
    }

    /// <summary>
    /// **Đối chiếu khi hãng đồng bộ lại danh mục**: mã biến mất · tên đổi · mã mới.
    /// Dùng để phát hiện ánh xạ cũ có thể trỏ **sai** sau khi hãng sáp nhập.
    /// </summary>
    public async Task<IReadOnlyList<SaiLechDto>> PhatHienSaiLechAsync(PhatHienSaiLechRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var tatCa = await _anhXa.GetListAsync();

        return CarrierAddressMappingResolver
            .PhatHienSaiLech(tatCa, yeuCau.CarrierCode, yeuCau.DanhMucMoi)
            .Select(s => new SaiLechDto(s.Loai, s.CarrierAddressCode, s.MoTa))
            .ToList();
    }

    /// <summary>
    /// **Tra NGƯỢC** đơn vị của ta → mã hãng, cho `SHP-001`/`SHP-004` (`D-89`, `D-232`).
    /// 🔒 **UỶ QUYỀN** cho <see cref="ICarrierAddressResolver"/> — **không** tự đọc repo, **không** logic thứ hai.
    /// Fail-closed: ném `0505`/`0501`/`0504`/`0502`/**`0510`** thay vì trả `null`.
    /// </summary>
    public Task<CarrierAddressCodeResultDto> ResolveCarrierAddressCodeAsync(
        ResolveCarrierAddressCodeRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        return _resolver.ResolveCarrierAddressCodeAsync(yeuCau);
    }

    /// <summary>
    /// **Danh sách ĐẦY ĐỦ mã hãng chưa được ánh xạ** của lượt `DaApDung` **gần nhất** (`D-88`).
    /// 🔒 Chỉ đọc: **0** UPDATE/DELETE, **0** gọi adapter hãng. Trừ ánh xạ đã có **tại lúc đọc**.
    /// Chưa có lượt `DaApDung` ⇒ danh sách rỗng + `SyncRunId = null` (**không** ném — `D-236`).
    /// </summary>
    public async Task<UnmappedCodesResultDto> GetUnmappedCodesAsync(GetUnmappedCodesRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var hang = (yeuCau.CarrierCode ?? string.Empty).Trim().ToUpperInvariant();

        if (hang.Length == 0)
        {
            // Nhất quán `Tra` (`0505`) — tránh "rỗng trả cả bảng".
            throw new BusinessException(CarrierAddressErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(yeuCau.CarrierCode));
        }

        var cacLo = await _loDongBo.GetListAsync(x => x.CarrierCode == hang);

        var moiNhat = SapGiamDan(cacLo).FirstOrDefault();

        var loNguon = SapGiamDan(cacLo.Where(x => x.Outcome == CarrierAddressSyncOutcome.DaApDung))
            .FirstOrDefault();

        if (loNguon is null)
        {
            // "Chưa có lượt đồng bộ" / "chưa lượt nào áp dụng" ⇒ rỗng, KHÔNG ném (`D-236`).
            return new UnmappedCodesResultDto([], 0, null, null, moiNhat?.Outcome);
        }

        var dongCon = await _dongCon.GetListAsync(x => x.SyncRunId == loNguon.Id);

        // Trừ ánh xạ **tại lúc đọc** (ánh xạ có thể được tạo SAU lượt — `D-88` ③).
        var daAnhXa = (await _anhXa.GetListAsync(x => x.CarrierCode == hang))
            .Select(x => x.CarrierAddressCode.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var conLai = dongCon
            .Where(x => !daAnhXa.Contains(x.CarrierAddressCode.Trim()))
            .OrderBy(x => x.CarrierLevel, StringComparer.Ordinal)
            .ThenBy(x => x.CarrierAddressCode, StringComparer.Ordinal)
            .ToList();

        var tong = conLai.Count;
        var max = yeuCau.MaxResultCount <= 0
            ? 100
            : Math.Min(yeuCau.MaxResultCount, MaxResultCountToiDa);
        var skip = Math.Max(0, yeuCau.SkipCount);

        var items = conLai
            .Skip(skip)
            .Take(max)
            .Select(x => new UnmappedCarrierCodeDto(x.CarrierLevel, x.CarrierAddressCode, x.CarrierAddressName))
            .ToList();

        return new UnmappedCodesResultDto(items, tong, loNguon.Id, loNguon.FinishedAtUtc, moiNhat?.Outcome);
    }

    private static IOrderedEnumerable<CarrierAddressSyncRun> SapGiamDan(IEnumerable<CarrierAddressSyncRun> cacLo)
        => cacLo
            .OrderByDescending(x => x.FinishedAtUtc)
            .ThenByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id);

    private static CarrierAddressMappingDto Chuyen(CarrierAddressMapping m)
        => new(m.Id, m.CarrierCode, m.CarrierLevel, m.CarrierAddressCode, m.CarrierAddressName,
            m.OurAdministrativeUnitId, m.MatchMethod, m.Status, m.SyncedAtUtc);

    private static AdministrativeUnitDto ChuyenDonVi(AdministrativeUnit u)
        => new(u.Id, u.SchemaVersion, u.Level, u.Code, u.Name, u.ParentCode, u.IsActive);
}
