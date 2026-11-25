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

/// <summary>
/// Một dòng của danh mục nạp qua đường file (`WP-18`, `D-78`/`D-93`).
/// 🔒 **Không** có trường `IsActive`: danh mục nạp = **tập đơn vị đang hiệu lực**; vắng mặt ⇒ `Dong`.
/// Cho phép truyền `IsActive` là mở đường "đóng/mở tuỳ ý qua file" ⇒ **bỏ**.
/// </summary>
public sealed record ImportUnitRow(AdministrativeLevel Level, string Code, string Name, string? ParentCode);

/// <summary>
/// Input nạp danh mục — nhận <see cref="Rows"/> **đã tách khỏi định dạng file**
/// (parser CSV/XML là WP sau, chờ input thật — `D-93`). `SourceHash` do **server** tính, không nhận từ client.
/// </summary>
public sealed record ImportDatasetInput(
    int SchemaVersion,
    string SourceName,
    IReadOnlyList<ImportUnitRow> Rows);

/// <summary>
/// Dòng lô nạp trả về cho lượt import **thành công** (`D-241`).
/// 📌 Giàu hơn `{ Added, Renamed, Reopened, Closed }` của SRS §6.1 (thêm trigger/outcome/sourceHash/thời điểm)
/// vì dòng lô là hợp đồng của BR-009 và UI cần hiển thị cả lượt **bị từ chối** — đã ghi `docDeviation` cho BA.
/// 🔒 Không chứa message/stack (BR-011).
/// </summary>
public sealed record CatalogSyncRunDto(
    Guid Id,
    int SchemaVersion,
    CatalogSyncTrigger Trigger,
    string? SourceName,
    string? SourceHash,
    DateTime StartedAtUtc,
    DateTime ImportedAtUtc,
    string ImportedBy,
    CatalogSyncOutcome Outcome,
    string? ErrorCode,
    int SoThemMoi,
    int SoDoiTen,
    int SoMoLai,
    int SoDong);
