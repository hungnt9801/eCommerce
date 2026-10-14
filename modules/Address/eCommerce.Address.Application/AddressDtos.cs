namespace eCommerce.Address;

/// <summary>DTO trả về một đơn vị hành chính.</summary>
public sealed record AdministrativeUnitDto(
    Guid Id,
    int SchemaVersion,
    AdministrativeLevel Level,
    string Code,
    string Name,
    string? ParentCode,
    bool IsActive);

/// <summary>Yêu cầu tra một đơn vị hành chính theo (phiên bản, cấp, mã).</summary>
public sealed record TraDonViRequest(int SchemaVersion, AdministrativeLevel Level, string Code);

/// <summary>Yêu cầu xác minh một địa chỉ dùng cho đơn MỚI (2 cấp).</summary>
public sealed record XacMinhDiaChiRequest(int SchemaVersion, string TinhCode, string XaCode);

/// <summary>
/// Kết quả xác minh địa chỉ trả cho tầng API.
/// 📌 `HopLe = false` **KHÔNG** phải lỗi hệ thống mà là **kết luận nghiệp vụ** — API trả `200` kèm
/// `HopLe = false` + `ErrorCode`, để giao diện hiển thị lý do cho người dùng.
/// </summary>
public sealed record XacMinhDiaChiResult(
    bool HopLe,
    string? ErrorCode,
    string? ThongDiep,
    AdministrativeUnitDto? Tinh,
    AdministrativeUnitDto? Xa);

/// <summary>DTO cho báo cáo bất thường dữ liệu địa chỉ (cùng mã khác phiên bản).</summary>
public sealed record MaXaHaiPhienBanDto(int SchemaVersion, AdministrativeUnitDto? DonVi);
