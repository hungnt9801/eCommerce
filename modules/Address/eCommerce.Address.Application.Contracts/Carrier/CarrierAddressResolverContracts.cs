namespace eCommerce.Address.Carrier;

/// <summary>
/// 🚨 **HỢP ĐỒNG HẸP, CHỈ ĐỌC** cho bên tiêu thụ (`SHP-001`/`SHP-004`) tra **ngược** đơn vị của ta → mã địa
/// chỉ hãng (`ADDR-002` §3.1, **`D-232`**).
///
/// 🔒 **VÌ SAO CHỈ MỘT METHOD:** `D-85` cấm dựng interface phản chiếu toàn bộ `CarrierAddressMappingAppService`
/// (mở cả đường ghi/đồng bộ cho module khác). Module khác **chỉ** cần một câu hỏi: *"đơn vị này gửi hàng qua
/// hãng này thì dùng mã nào?"*.
///
/// 🔑 **TÁCH LỚP HIỆN THỰC KHỎI APP SERVICE** (`D-232` ①(4)(5)): nếu `CarrierAddressMappingAppService` tự
/// hiện thực interface in-process này, ABP có thể sinh HTTP route cho nó (bài học `CustomerDirectory`) —
/// hiện thực nằm ở lớp riêng `CarrierAddressResolver : ITransientDependency` trong `Address.Application`.
///
/// 📌 **KHÔNG** đặt `GetUnmappedCodesAsync`/`XacMinhAsync` ở đây: chỉ FE + Host dùng, ở lại `Application`.
/// 📌 Project `Application.Contracts` **KHÔNG** tham chiếu `Address.Domain` (`D-232` ②) ⇒ mọi giá trị enum ở
/// biên là **`int`**.
/// </summary>
public interface ICarrierAddressResolver
{
    /// <summary>
    /// Tra **ngược**: (mã hãng, đơn vị của ta) → mã địa chỉ hãng dùng cho vận đơn.
    /// 🚨 **FAIL-CLOSED** — ném lỗi thay vì trả `null`/chọn bừa (mã `0501`/`0502`/`0504`/`0505`/`0510`).
    /// </summary>
    Task<CarrierAddressCodeResultDto> ResolveCarrierAddressCodeAsync(ResolveCarrierAddressCodeRequest request);
}

/// <summary>Đầu vào tra ngược (`D-89`).</summary>
public sealed record ResolveCarrierAddressCodeRequest(string CarrierCode, Guid OurAdministrativeUnitId);

/// <summary>
/// Kết quả tra ngược — **giữ hình dạng `Mapping` + `Unit` của `D-89`** (`D-232` ①(1)); app service trả
/// **chính** kiểu này (một DTO, không chép hai bản).
/// </summary>
public sealed record CarrierAddressCodeResultDto(
    CarrierAddressMappingContractDto Mapping,
    AdministrativeUnitContractDto Unit);

/// <summary>
/// Bản sao **biên hợp đồng** của `CarrierAddressMappingDto` (9 trường, trùng **tên**), **không** logic.
/// 🔒 `MatchMethod`/`Status` là **`int`** — `Application.Contracts` không được tham chiếu `Address.Domain`
/// (`D-232` ②).
/// </summary>
public sealed record CarrierAddressMappingContractDto(
    Guid Id,
    string CarrierCode,
    string CarrierLevel,
    string CarrierAddressCode,
    string CarrierAddressName,
    Guid OurAdministrativeUnitId,
    int MatchMethod,
    int Status,
    DateTime SyncedAtUtc);

/// <summary>
/// Bản sao **biên hợp đồng** của `AdministrativeUnitDto` (7 trường, trùng **tên**); `Level` là **`int`**.
/// </summary>
public sealed record AdministrativeUnitContractDto(
    Guid Id,
    int SchemaVersion,
    int Level,
    string Code,
    string Name,
    string? ParentCode,
    bool IsActive);
