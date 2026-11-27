using eCommerce.Kernel.Observability;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace eCommerce.Notification;

/// <summary>
/// Hiện thực **DUY NHẤT** của <see cref="INotificationSender"/> ở production (thay `ChuaCoKenhGuiSender`
/// — `D-315` AQ6). Trung lập nhà cung cấp: đi qua <see cref="INotificationChannelFactory"/> + adapter.
///
/// 🚨 Fail-closed `eCommerce:Notification:0003` TRƯỚC mọi lời gọi mạng: thiếu cấu hình / adapter / credential /
/// hạn credential / khoá Settings bắt buộc ⇒ **NÉM** (không trả `IsTransient` ⇒ không `DaBanGiao` giả, `D-263`).
/// KHÔNG DB/UoW/ETO — kết quả đi **đồng bộ** về job qua <see cref="NotificationSendResult"/>.
/// </summary>
public sealed class NotificationSender : INotificationSender, ITransientDependency
{
    private static readonly IReadOnlyDictionary<string, string> KhongCoCauHinh =
        new Dictionary<string, string>();

    private readonly INotificationChannelConfigProvider _configProvider;
    private readonly INotificationChannelFactory _factory;
    private readonly IClock _clock;
    private readonly eCommerceMetrics _metrics;
    private readonly ILogger<NotificationSender> _logger;

    public NotificationSender(
        INotificationChannelConfigProvider configProvider,
        INotificationChannelFactory factory,
        IClock clock,
        eCommerceMetrics metrics,
        ILogger<NotificationSender> logger)
    {
        _configProvider = configProvider;
        _factory = factory;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<NotificationSendResult> SendAsync(NotificationSendRequest request, CancellationToken ct = default)
    {
        // (1) Cấu hình kênh: `null` ⇒ chưa cấu hình ⇒ fail-closed.
        var config = await _configProvider.FindAsync(request.ChannelKind, ct);
        if (config is null)
        {
            throw KenhChuaSanSang(request.QueueItemId, request.ChannelKind, null);
        }

        // (2) Adapter khớp `(ChannelKind, ProviderCode)` — không fallback kênh khác.
        var adapter = _factory.Find(config.ChannelKind, config.ProviderCode);
        if (adapter is null)
        {
            throw KenhChuaSanSang(request.QueueItemId, config.ChannelKind, config.ProviderCode);
        }

        // (3) Kênh phải bật + có credential hợp lệ + đủ khoá Settings bắt buộc.
        if (!config.IsEnabled)
        {
            throw KenhChuaSanSang(request.QueueItemId, config.ChannelKind, config.ProviderCode);
        }

        if (string.IsNullOrWhiteSpace(config.CredentialEncrypted))
        {
            throw KenhChuaSanSang(request.QueueItemId, config.ChannelKind, config.ProviderCode);
        }

        if (config.CredentialExpiresAtUtc is { } hetHanUtc && hetHanUtc <= _clock.Now)
        {
            throw KenhChuaSanSang(request.QueueItemId, config.ChannelKind, config.ProviderCode);
        }

        var settings = config.Settings ?? KhongCoCauHinh;
        foreach (var khoa in adapter.RequiredSettingsKeys)
        {
            if (!settings.ContainsKey(khoa))
            {
                throw KenhChuaSanSang(request.QueueItemId, config.ChannelKind, config.ProviderCode);
            }
        }

        // (4) Dựng lệnh: giữ NGUYÊN ciphertext người nhận/credential + Subject/Body đã render + mask.
        var command = new SendCommand(
            request.TenantId,
            request.QueueItemId,
            request.IdempotencyKey,
            request.ChannelKind,
            config.ProviderCode,
            config.ChannelAccountId,
            config.CredentialEncrypted,
            request.RecipientEncrypted,
            request.Subject,
            request.Body,
            request.Priority,
            request.CorrelationId,
            settings);

        // (5) ĐIỂM DUY NHẤT có thể gọi mạng.
        var outcome = await adapter.SendAsync(command, ct);

        // (6) Map `SendOutcome` → canonical `NotificationSendResult` (7 hàng §4.5).
        return ChuyenKetQua(outcome, command);
    }

    private NotificationSendResult ChuyenKetQua(SendOutcome outcome, SendCommand command)
    {
        // Vi phạm hợp đồng `SendOutcome`: `Accepted` và `FailureKind` mâu thuẫn ⇒ lỗi lập trình.
        if (outcome.Accepted && outcome.FailureKind != NotificationFailureKind.None)
        {
            throw ViPhamHopDong(command, "Accepted=true nhưng FailureKind khác None");
        }

        if (!outcome.Accepted && outcome.FailureKind == NotificationFailureKind.None)
        {
            throw ViPhamHopDong(command, "Accepted=false nhưng FailureKind=None");
        }

        if (outcome.Accepted)
        {
            // Hàng 2: NCC nhận nhưng không có ProviderMessageId ⇒ coi là lỗi `0006` (KHÔNG ném).
            if (string.IsNullOrWhiteSpace(outcome.ProviderMessageId))
            {
                return new NotificationSendResult(
                    Accepted: false,
                    AlreadySent: false,
                    IsTransient: false,
                    ErrorCode: NotificationErrorCodes.KhongCoProviderMessageId);
            }

            // Hàng 1: thành công.
            return new NotificationSendResult(Accepted: true, AlreadySent: false, IsTransient: false, ErrorCode: null);
        }

        switch (outcome.FailureKind)
        {
            // Hàng 3: lỗi tạm thời ⇒ job hoàn lại chờ gửi.
            case NotificationFailureKind.Transient:
                return new NotificationSendResult(false, false, true, "loi-tam-thoi");

            // Hàng 4: hết hạn mức NCC ⇒ thử lại cửa sổ sau.
            case NotificationFailureKind.QuotaExceeded:
                return new NotificationSendResult(false, false, true, "quota");

            // Hàng 6: lỗi vĩnh viễn ⇒ job đánh dấu `Loi`.
            case NotificationFailureKind.Permanent:
                return new NotificationSendResult(false, false, false, NotificationErrorCodes.NguoiNhanKhongHopLe);

            // Hàng 5: credential bị NCC từ chối ⇒ NÉM `0003` (một ngữ nghĩa cho mọi lỗi credential).
            case NotificationFailureKind.Unauthenticated:
                throw KenhChuaSanSang(command.QueueItemId, command.ChannelKind, command.ProviderCode);

            // Hàng 7: `None` đã bị chặn ở guard trên; nhánh còn lại là vi phạm hợp đồng.
            default:
                throw ViPhamHopDong(command, $"FailureKind không hợp lệ: {outcome.FailureKind}");
        }
    }

    private static InvalidOperationException ViPhamHopDong(SendCommand command, string lyDo)
        => new($"{lyDo} — QueueItemId={command.QueueItemId}, ChannelKind={command.ChannelKind}, ProviderCode={command.ProviderCode}");

    /// <summary>
    /// Dựng `BusinessException(0003)` sau khi đã tăng metric `notification_queue_failed_total{channel,reason=0003}`
    /// và log **một** dòng `Warning` (CHỈ mã/kênh/mục — **0** recipient/credential/subject/body).
    /// Caller `throw` kết quả.
    /// </summary>
    private BusinessException KenhChuaSanSang(Guid queueItemId, ChannelKind channel, string? providerCode)
    {
        var kenh = NotificationQueueAppender.KenhNhan(channel);

        _metrics.NotificationQueueFailed.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, kenh),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, NotificationErrorCodes.KenhGuiChuaSanSang));

        _logger.LogWarning(
            "[NOT-CHANNEL] Kênh chưa sẵn sàng — ErrorCode={ErrorCode} QueueItemId={QueueItemId} ChannelKind={ChannelKind} ProviderCode={ProviderCode}",
            NotificationErrorCodes.KenhGuiChuaSanSang,
            queueItemId,
            kenh,
            providerCode ?? string.Empty);

        return new BusinessException(NotificationErrorCodes.KenhGuiChuaSanSang)
            .WithData("channel", kenh)
            .WithData("provider", providerCode ?? string.Empty);
    }
}
