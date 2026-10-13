using System.Diagnostics.Metrics;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Observability;

/// <summary>
/// **BẢNG ĐĂNG KÝ TÊN METRIC** — hợp đồng dùng chung, khớp 1-1 với `docs/features/XC-005-observability-log-metric.md` §5.1.
///
/// 🎯 VÌ SAO CHỈ LÀ HẰNG SỐ CHUỖI MÀ KHÔNG PHẢI ENUM:
///   có test tự động (`TC-06`) đối chiếu **tên phát ra thật** với danh sách này ⇒ không thể lệch.
///   Nếu tên nằm rải rác ở nơi gọi thì không kiểm được (đây chính là bài học từ bảng đăng ký khoá ở `XC-003`).
/// </summary>
public static class eCommerceMetricNames
{
    // ---- Messaging (XC-002) ----
    public const string MessagingOutboxPending = "messaging_outbox_pending";
    public const string MessagingInboxFailedTotal = "messaging_inbox_failed_total";
    public const string MessagingDlqSize = "messaging_dlq_size";
    public const string MessagingDlqReplayedTotal = "messaging_dlq_replayed_total";

    // ---- Distributed lock (XC-003) ----
    public const string LockTimeoutTotal = "lock_timeout_total";
    public const string LockSkippedTotal = "lock_skipped_total";
    public const string LockTtlExceededTotal = "lock_ttl_exceeded_total";

    // ---- HTTP / tích hợp ----
    public const string HttpRequestDurationSeconds = "http_request_duration_seconds";
    public const string PaymentWebhookRejectedTotal = "payment_webhook_rejected_total";
    public const string ChannelSyncFailedTotal = "channel_sync_failed_total";

    // ---- Nghiệp vụ thuế (XC-006) ----
    public const string TaxUnclassifiedSkus = "tax_unclassified_skus";

    // ---- Tự quan sát ----
    public const string LoggingFailuresTotal = "logging_failures_total";

    /// <summary>Toàn bộ tên metric — nguồn sự thật duy nhất cho test `TC-06`.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        MessagingOutboxPending,
        MessagingInboxFailedTotal,
        MessagingDlqSize,
        MessagingDlqReplayedTotal,
        LockTimeoutTotal,
        LockSkippedTotal,
        LockTtlExceededTotal,
        HttpRequestDurationSeconds,
        PaymentWebhookRejectedTotal,
        ChannelSyncFailedTotal,
        TaxUnclassifiedSkus,
        LoggingFailuresTotal
    ];
}

/// <summary>
/// Tên **nhãn (tag)** được phép — mọi nhãn phải nằm ở đây.
///
/// 🚨 `XC-005` BR-009: **CẤM** nhãn có cardinality cao. Mỗi giá trị nhãn tạo **một chuỗi thời gian riêng**
/// trong hệ thống metric ⇒ gắn `orderId`/`sku`/`userId`/`correlationId` sẽ **nổ** bộ nhớ hệ thống metric.
/// Cần truy vết theo các giá trị đó ⇒ dùng **log** (đã có `CorrelationId`).
/// </summary>
public static class eCommerceMetricTags
{
    public const string Channel = "channel";
    public const string Provider = "provider";
    public const string EventName = "eventName";
    public const string Status = "status";
    public const string Reason = "reason";
    public const string Kind = "kind";
    public const string Module = "module";
    public const string Result = "result";
    public const string Method = "method";
    public const string Route = "route";

    public static readonly IReadOnlyList<string> Allowed =
    [
        Channel, Provider, EventName, Status, Reason, Kind, Module, Result, Method, Route
    ];

    /// <summary>Nhãn **BỊ CẤM** vì cardinality cao — test `TC-07` chặn nếu chúng lọt vào danh sách cho phép.</summary>
    public static readonly IReadOnlyList<string> Forbidden =
    [
        "orderId", "orderNumber", "sku", "userId", "correlationId", "email", "phone", "lockKey"
    ];
}

/// <summary>
/// Phát **12 metric** bằng `System.Diagnostics.Metrics` **có sẵn trong .NET** — **KHÔNG thêm gói NuGet nào**.
///
/// 🎯 VÌ SAO CÁCH NÀY (chốt Q1 của `XC-005` theo hướng thực dụng):
///   `Meter`/`Counter`/`Histogram`/`ObservableGauge` nằm trong BCL (.NET 6+). Chọn công cụ thu thập
///   (OpenTelemetry → Prometheus/Grafana…) là việc của **tầng xuất**, có thể thêm **sau** mà **không phải
///   sửa nơi phát metric**. Nhờ vậy 12 metric đang bị "nợ" nay **đã có nơi phát thật** mà không kéo thêm
///   dependency nào vào dự án.
///
/// 📌 Gauge dùng cơ chế "giá trị do nơi phát cập nhật" (`Set…`), vì ta không có nguồn pull định kỳ.
/// </summary>
public sealed class eCommerceMetrics : ISingletonDependency
{
    public const string MeterName = "eCommerce";
    public const string MeterVersion = "1.0.0";

    private readonly Meter _meter;

    // ---- Counter ----
    public Counter<long> MessagingInboxFailed { get; }
    public Counter<long> MessagingDlqReplayed { get; }
    public Counter<long> LockTimeout { get; }
    public Counter<long> LockSkipped { get; }
    public Counter<long> LockTtlExceeded { get; }
    public Counter<long> PaymentWebhookRejected { get; }
    public Counter<long> ChannelSyncFailed { get; }
    public Counter<long> LoggingFailures { get; }

    // ---- Histogram ----
    public Histogram<double> HttpRequestDurationSeconds { get; }

    // ---- Gauge (giá trị do nơi phát cập nhật) ----
    private long _messagingOutboxPending;
    private long _messagingDlqSize;
    private long _taxUnclassifiedSkus;

    public void SetMessagingOutboxPending(long value) => Interlocked.Exchange(ref _messagingOutboxPending, value);
    public void SetMessagingDlqSize(long value) => Interlocked.Exchange(ref _messagingDlqSize, value);
    public void SetTaxUnclassifiedSkus(long value) => Interlocked.Exchange(ref _taxUnclassifiedSkus, value);

    public eCommerceMetrics()
        : this(new Meter(MeterName, MeterVersion))
    {
    }

    /// <summary>Dùng cho test: cho phép truyền <see cref="Meter"/> riêng để không đụng meter toàn cục.</summary>
    public eCommerceMetrics(Meter meter)
    {
        _meter = meter;

        MessagingInboxFailed = _meter.CreateCounter<long>(eCommerceMetricNames.MessagingInboxFailedTotal,
            unit: "{event}", description: "Số lần xử lý inbox thất bại (XC-002).");
        MessagingDlqReplayed = _meter.CreateCounter<long>(eCommerceMetricNames.MessagingDlqReplayedTotal,
            unit: "{event}", description: "Số lần phát lại DLQ (XC-002).");
        LockTimeout = _meter.CreateCounter<long>(eCommerceMetricNames.LockTimeoutTotal,
            unit: "{attempt}", description: "Số lần không giành được khoá phân tán (XC-003).");
        LockSkipped = _meter.CreateCounter<long>(eCommerceMetricNames.LockSkippedTotal,
            unit: "{attempt}", description: "Job định kỳ bỏ qua lượt vì khoá đang bận (XC-003).");
        LockTtlExceeded = _meter.CreateCounter<long>(eCommerceMetricNames.LockTtlExceededTotal,
            unit: "{occurrence}", description: "Số lần thao tác VƯỢT TTL của khoá — kỳ vọng 0 (XC-003).");
        PaymentWebhookRejected = _meter.CreateCounter<long>(eCommerceMetricNames.PaymentWebhookRejectedTotal,
            unit: "{webhook}", description: "Webhook thanh toán bị từ chối (sai chữ ký, sai số tiền).");
        ChannelSyncFailed = _meter.CreateCounter<long>(eCommerceMetricNames.ChannelSyncFailedTotal,
            unit: "{sync}", description: "Đồng bộ sàn thất bại.");
        LoggingFailures = _meter.CreateCounter<long>(eCommerceMetricNames.LoggingFailuresTotal,
            unit: "{failure}", description: "Ghi log thất bại (logger/sink hỏng) — log là best-effort.");

        HttpRequestDurationSeconds = _meter.CreateHistogram<double>(eCommerceMetricNames.HttpRequestDurationSeconds,
            unit: "s", description: "Độ trễ xử lý request HTTP.");

        _meter.CreateObservableGauge(eCommerceMetricNames.MessagingOutboxPending,
            () => Interlocked.Read(ref _messagingOutboxPending),
            unit: "{event}", description: "Số bản ghi outbox đang tồn đọng (XC-002 NFR-003).");
        _meter.CreateObservableGauge(eCommerceMetricNames.MessagingDlqSize,
            () => Interlocked.Read(ref _messagingDlqSize),
            unit: "{event}", description: "Kích thước DLQ — > 0 thì phải cảnh báo (XC-002).");
        _meter.CreateObservableGauge(eCommerceMetricNames.TaxUnclassifiedSkus,
            () => Interlocked.Read(ref _taxUnclassifiedSkus),
            unit: "{sku}", description: "Số SKU chưa phân loại thuế — kế toán phải xử lý (XC-006).");
    }
}
