namespace eCommerce.Notification;

/// <summary>
/// Hợp đồng MỘT kênh gửi (ADR-01). CẤM app service/job gọi SDK/NCC trực tiếp (BR-001).
/// Mỗi nhà cung cấp = một class hiện thực interface này; registry ráp chúng lại (<see cref="INotificationChannelFactory"/>).
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>Mã nhà cung cấp — khớp <c>ProviderCode</c> trong cấu hình kênh (vd `smtp`, `esms`, `zalo-oa`).</summary>
    string ProviderCode { get; }

    ChannelKind ChannelKind { get; }

    /// <summary>
    /// Khoá Settings BẮT BUỘC của adapter (vd `apiBaseUrl`; SMS/Zalo: `unitCostPerMessage` + `currency`).
    /// Thiếu bất kỳ khoá nào ⇒ `eCommerce:Notification:0003` TRƯỚC khi gọi mạng (BR-024).
    /// </summary>
    IReadOnlyCollection<string> RequiredSettingsKeys { get; }

    Task<SendOutcome> SendAsync(SendCommand command, CancellationToken ct = default);
}

/// <summary>
/// Lệnh gửi canonical của WP-23. KHÔNG có `TemplateCode`/`Data` (`NOT-002` không render — ADR-06/ADR-09).
/// `Recipient` và credential ở dạng **ĐÃ MÃ HOÁ**; adapter giải mã ở biên cuối, KHÔNG log/DTO/ETO.
/// Adapter KHÔNG đọc lại <see cref="INotificationChannelConfigProvider"/> — mọi thứ cần dùng nằm trong lệnh này.
/// </summary>
/// <param name="CredentialEncrypted">CHỈ ciphertext (`AQ8`); adapter giải mã MỘT lần bằng `IStringEncryptionService`.</param>
public sealed record SendCommand(
    Guid? TenantId,
    Guid QueueItemId,
    string IdempotencyKey,
    ChannelKind ChannelKind,
    string ProviderCode,
    Guid? ChannelAccountId,
    string? CredentialEncrypted,
    string RecipientEncrypted,
    string Subject,
    string Body,
    int Priority,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// Kết quả adapter. `Reason` là token NGẮN do adapter tự đặt (≤ 64, KHÔNG chứa payload/PII của NCC).
/// `RecipientRefused` GIỮ CHỖ cho `D-207` — WP-23 KHÔNG xử lý (opt-out ngoài phạm vi).
/// </summary>
public sealed record SendOutcome(
    bool Accepted,
    string? ProviderMessageId,
    NotificationFailureKind FailureKind,
    string? Reason,
    bool RecipientRefused = false);

/// <summary>
/// Registry kênh gửi (một cửa, `D-200`). `Find` KHÔNG fallback sang kênh khác (cấm hạ cấp ZNS → CSKH).
/// Hiện thực dựng từ điển `(ChannelKind, ProviderCode)` một lần; trùng khoá ⇒ NÉM khi khởi tạo.
/// </summary>
public interface INotificationChannelFactory
{
    IReadOnlyCollection<INotificationChannelSender> All { get; }

    INotificationChannelSender? Find(ChannelKind channel, string providerCode);
}

/// <summary>
/// Cấu hình kênh ở chế độ **đọc** — đầu vào của `NotificationSender` (cổng `AQ1`, `D-314`).
/// `Settings` đã parse (jsonb) và KHÔNG chứa secret (`D-202`). Credential là **bản mã hoá**.
/// </summary>
public sealed record NotificationChannelConfig(
    Guid? ChannelAccountId,
    ChannelKind ChannelKind,
    string ProviderCode,
    bool IsEnabled,
    string? CredentialEncrypted,
    DateTime? CredentialExpiresAtUtc,
    IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// Seam để kiểm fail-closed `0003` khi `WP-23` chưa có bảng `NotificationChannelAccounts` (`D-314`).
/// Tenant lấy từ DI scope hiện tại (ABP `ICurrentTenant`); KHÔNG nhận `TenantId` từ caller
/// (chống đọc chéo tenant — `NFR-NOT-002-009`).
/// </summary>
public interface INotificationChannelConfigProvider
{
    Task<NotificationChannelConfig?> FindAsync(ChannelKind channel, CancellationToken ct = default);
}
