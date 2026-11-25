using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Address.Carrier;

/// <summary>
/// **HIỆN THỰC HỢP ĐỒNG HẸP `ICarrierAddressResolver`** (`ADDR-002` §3.1, **`D-232`**).
///
/// 🔒 **VÌ SAO LÀ LỚP RIÊNG, KHÔNG phải app service:**
///   `CarrierAddressMappingAppService` **không** kế thừa/không hiện thực `ICarrierAddressResolver`.
///   Nếu app service hiện thực một interface thì ABP có thể sinh HTTP route cho method **đọc** này, và
///   `SHP-*` sẽ có **hai** đường vào cùng một logic (in-process + HTTP). Lớp `ITransientDependency` **không**
///   phải `IApplicationService` ⇒ **0** endpoint sinh ra, chỉ gọi in-process (khuôn `AddressDirectory`).
///
/// 🎯 **MỘT nguồn logic:** toàn bộ luật nằm ở hàm thuần
/// <see cref="CarrierAddressMappingResolver.TraNguoc"/>; lớp này chỉ đọc repo và map DTO — app service uỷ
/// quyền xuống đây (`D-232` ①(5)), **không** tự viết luật thứ hai.
/// </summary>
public class CarrierAddressResolver : ICarrierAddressResolver, ITransientDependency
{
    private readonly IRepository<CarrierAddressMapping, Guid> _anhXa;
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;

    public CarrierAddressResolver(
        IRepository<CarrierAddressMapping, Guid> anhXa,
        IRepository<AdministrativeUnit, Guid> donVi)
    {
        _anhXa = anhXa;
        _donVi = donVi;
    }

    public async Task<CarrierAddressCodeResultDto> ResolveCarrierAddressCodeAsync(
        ResolveCarrierAddressCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tatCaAnhXa = await _anhXa.GetListAsync();
        var tatCaDonVi = await _donVi.GetListAsync();

        var kq = CarrierAddressMappingResolver.TraNguoc(
            tatCaAnhXa, tatCaDonVi, request.CarrierCode, request.OurAdministrativeUnitId);

        return new CarrierAddressCodeResultDto(ChuyenMapping(kq.AnhXa), ChuyenDonVi(kq.DonVi));
    }

    /// <summary>Mapper **dùng chung** cho biên hợp đồng — không logic thứ hai (`D-232` ①(4)).</summary>
    internal static CarrierAddressMappingContractDto ChuyenMapping(CarrierAddressMapping m)
        => new(
            m.Id,
            m.CarrierCode,
            m.CarrierLevel,
            m.CarrierAddressCode,
            m.CarrierAddressName,
            m.OurAdministrativeUnitId,
            (int)m.MatchMethod,
            (int)m.Status,
            m.SyncedAtUtc);

    /// <summary>Mapper **dùng chung** cho biên hợp đồng — `Level` là **`int`** (`D-232` ②).</summary>
    internal static AdministrativeUnitContractDto ChuyenDonVi(AdministrativeUnit u)
        => new(u.Id, u.SchemaVersion, (int)u.Level, u.Code, u.Name, u.ParentCode, u.IsActive);
}
