using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using eCommerce.Address.Carrier;

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
    private readonly IRepository<CarrierAddressMapping, Guid> _anhXa;
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;

    public CarrierAddressMappingAppService(
        IRepository<CarrierAddressMapping, Guid> anhXa,
        IRepository<AdministrativeUnit, Guid> donVi)
    {
        _anhXa = anhXa;
        _donVi = donVi;
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

    /// <summary>Xác minh một ánh xạ ⇒ từ đó mới dùng được để tạo vận đơn.</summary>
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

    private static CarrierAddressMappingDto Chuyen(CarrierAddressMapping m)
        => new(m.Id, m.CarrierCode, m.CarrierLevel, m.CarrierAddressCode, m.CarrierAddressName,
            m.OurAdministrativeUnitId, m.MatchMethod, m.Status, m.SyncedAtUtc);

    private static AdministrativeUnitDto ChuyenDonVi(AdministrativeUnit u)
        => new(u.Id, u.SchemaVersion, u.Level, u.Code, u.Name, u.ParentCode, u.IsActive);
}
