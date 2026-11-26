namespace eCommerce.Notification;

/// <summary>
/// Mã lỗi của module `Notification` — **MỘT** tệp (`D-197` ⑥).
/// `NOT-001` dùng dải **`0010–0020`**; `NOT-002` dùng dải **`0001–0009`** (thêm cùng tệp ở WP-23).
/// 🚨 Dải mã là **hợp đồng** (ADR-14): **CẤM** đặt lại `0001–0009` cho mục đích khác của `NOT-001`.
/// </summary>
public static class NotificationErrorCodes
{
    /// <summary>Trùng khoá idempotency **nhưng payload khác**, hoặc thiếu khoá ở API thủ công.</summary>
    public const string KhoaXungDot = "eCommerce:Notification:0010";

    /// <summary>Chuyển trạng thái mục hàng đợi không hợp lệ (kể cả lùi trạng thái).</summary>
    public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Notification:0011";

    /// <summary>Template không tồn tại hoặc chưa `Published` cho `(Code, ChannelKind, Locale)`.</summary>
    public const string TemplateChuaSanSang = "eCommerce:Notification:0012";

    /// <summary>Thiếu trường bắt buộc của template (render dry-run thất bại).</summary>
    public const string ThieuTruongBatBuoc = "eCommerce:Notification:0013";

    /// <summary>Cố bàn giao mục đã ở trạng thái hấp thụ.</summary>
    public const string DaBanGiaoRoi = "eCommerce:Notification:0014";

    /// <summary>Placeholder không khai báo trong whitelist của template.</summary>
    public const string PlaceholderNgoaiWhitelist = "eCommerce:Notification:0015";

    /// <summary>Cố sửa nội dung của version đã `Published`.</summary>
    public const string PhienBanBatBien = "eCommerce:Notification:0016";

    /// <summary>Không tìm thấy template/mục/lô (kể cả của tenant khác).</summary>
    public const string KhongTimThay = "eCommerce:Notification:0017";

    /// <summary>Bàn giao mục thuộc lô tồn tại nhưng chưa được duyệt.</summary>
    public const string LoChuaDuyet = "eCommerce:Notification:0018";

    /// <summary>Bị khử trùng cửa sổ (ADR-12).</summary>
    public const string BiKhudupCuaSo = "eCommerce:Notification:0019";

    /// <summary>`BodyFormat` khác `Text` ở GĐ3 (`Html = 1`) — NÉM trước mọi ghi (`D-199`).</summary>
    public const string BodyFormatKhongHoTro = "eCommerce:Notification:0020";
}

/// <summary>Giới hạn độ dài — **một nguồn** giữa entity, EF mapping và DTO.</summary>
public static class NotificationConsts
{
    public const int CodeToiDa = 64;
    public const int NameToiDa = 256;
    public const int LocaleToiDa = 8;
    public const int IdempotencyKeyToiDa = 256;
    public const int SubjectToiDa = 512;
    public const int ReasonToiDa = 512;
    public const int CorrelationIdToiDa = 64;
    public const int ErrorCodeToiDa = 64;
    public const int SourceEventTypeToiDa = 256;
    public const int SourceAggregateIdToiDa = 128;
    public const int HashHexLength = 64;

    /// <summary>Tiền tố mã lỗi — dùng cho test `TC-44`.</summary>
    public const string ErrorCodePrefix = "eCommerce:Notification:";
}

/// <summary>
/// Khoá phân tán của miền thông báo — **MỘT** lớp (`D-197`).
/// 🚨 Tên khoá do module sinh **TUYỆT ĐỐI KHÔNG** chứa tiền tố `ecommerce:` (bẫy tiền tố đôi, `XC-003`).
/// </summary>
public static class NotificationLockKeys
{
    public static string QueueDispatch(Guid? tenantId)
        => $"notification:queue-dispatch:{Tenant(tenantId)}";

    public static string Template(Guid templateId)
        => $"notification:template:{templateId}";

    public static string Batch(Guid batchId)
        => $"notification:batch:{batchId}";

    public static string QueueReplay(Guid? tenantId)
        => $"notification:queue-replay:{Tenant(tenantId)}";

    private static string Tenant(Guid? tenantId) => tenantId?.ToString() ?? "host";
}
