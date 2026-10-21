using System.Diagnostics.Metrics;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Kernel.Observability;

/// <summary>
/// **BẢNG ĐĂNG KÝ TÊN METRIC** — hợp đồng dùng chung, khớp 1-1 với `docs/features/cross-cutting/XC-005-observability-log-metric.md` §5.1.
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

    // ---- Nghiệp vụ KHO (INV-001 ADR-10) ---- 🚨 trước 15/10/2026 KHÔNG có metric nào cho kho
    /// <summary>Số cặp `(kho, SKU)` có `OnHand` **LỆCH** với `SUM(sổ cái)` — đối soát định kỳ (INV-001 ADR-10).</summary>
    public const string InventoryReconMismatchSkus = "inventory_recon_mismatch_skus";

    // ---- Nghiệp vụ thuế (XC-006) ----
    public const string TaxUnclassifiedSkus = "tax_unclassified_skus";

    // ---- Nghiệp vụ THANH TOÁN / đối soát (`REC-001` mục 7, 17/10/2026) ----
    /// <summary>
    /// 🚨 **SỐ GIAO DỊCH ĐỐI SOÁT MÀ CỔNG KHÔNG XÁC NHẬN ĐƯỢC** — `Pending`/lỗi hạ tầng/**cổng chưa cấu hình**.
    ///
    /// 🔑 **VÌ SAO CẦN METRIC NÀY (không đủ log):** job đối soát đã **phát hiện** và **log** ca này
    /// (`[PAY-JOB-KHONG-XAC-DINH]`), ⚠️ nhưng 📌 *"job phát hiện"* **KHÔNG PHẢI** *"có người biết"* —
    /// log nằm trong tệp, còn metric mới **vẽ được biểu đồ** và **gắn được cảnh báo**.
    ///
    /// ⚠️ **VÀ ĐỌC CON SỐ NÀY CHO ĐÚNG — đây là lý do nó là GAUGE chứ không phải counter:**
    /// 📌 Hậu tố `_total` là **quy ước đặt tên của dự án** (giống `payment_webhook_rejected_total` — cũng là
    /// counter/gauge đếm sự kiện), **không** phải lời khai rằng nó là counter. 🔑 Công cụ thật là **kiểu
    /// instrument** (`ObservableGauge`), không phải cái tên.
    /// giá trị **BẰNG** số giao dịch cần đối soát nghĩa là lượt quét **KHÔNG mang lại thông tin nào**
    /// ⇒ 🔑 **cấu hình sai hoặc cổng chết**, **KHÔNG PHẢI** *"hệ thống khoẻ"*.
    /// 📌 Nếu để nó thành counter thì con số **tăng đều** trông như *"hoạt động bình thường"*
    /// ⇒ đúng loại nói dối mà dự án đang chống.
    /// </summary>
    public const string PaymentReconUnresolved = "payment_recon_unresolved_total";

    /// <summary>
    /// Số lần lượt đối soát **ĐỔI trạng thái** một giao dịch — nhãn <c>result</c> cho biết **đổi thành gì**
    /// (<c>succeeded</c> = tiền **đã vào** mà sổ chưa biết ⇒ 🔑 **lưới an toàn cho IPN mất**).
    ///
    /// 🔑 **VÌ SAO CẦN TÁCH KHỎI GAUGE TRÊN:** gauge trả lời *"còn bao nhiêu ca chưa ngã ngũ"*, còn counter
    /// này trả lời *"đã **phát hiện và sửa** bao nhiêu ca"*. 📌 Hai câu hỏi **khác nhau**; gộp lại thì
    /// *"không kiểm được"* và *"đã kiểm và sửa"* trông **giống hệt nhau**.
    /// </summary>
    public const string PaymentReconChangedTotal = "payment_recon_changed_total";

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

        // 🚨 **ĐÃ TỪNG THIẾU Ở ĐÂY** (phát hiện 17/10/2026): hằng số `InventoryReconMismatchSkus` **có**
        // nhưng **không** nằm trong danh sách ⇒ 🔒 danh sách này **không còn** là *"nguồn sự thật duy nhất"*
        // như chú thích của nó nói. ⚠️ Và **không có test nào bắt** điều đó (test chỉ kiểm chiều
        // *"mọi tên trong `All` đều hợp lệ"*, **không** kiểm chiều *"mọi hằng số đều có trong `All`"*).
        // 📌 Đây là **nợ thật** đã ghi ở §26.
        InventoryReconMismatchSkus,

        TaxUnclassifiedSkus,
        PaymentReconUnresolved,
        PaymentReconChangedTotal,
        LoggingFailuresTotal
    ];

    /// <summary>
    /// 🚨 **BẢNG "KÝ HIỆU NÀO ĐƯỢC GỌI Ở NƠI PHÁT" — có đủ cho MỌI tên đã đăng ký, kể cả tên CHƯA có nơi phát.**
    ///
    /// 🔑 **VÌ SAO CẦN (đo được, `S10` 21/10/2026):** từ 13/10/2026 tài liệu nói *"12 metric ĐÃ CÓ NƠI PHÁT"*,
    /// ⚠️ nhưng **chưa bao giờ** có phép kiểm nào đối chiếu *"đã đăng ký"* với *"có chỗ gọi thật"*.
    /// 📌 Đo lại bằng cách quét `src/` + `modules/` + `shared/`: **11/15** tên có điểm phát, **4/15** **KHÔNG**
    /// (`messaging_outbox_pending` · `messaging_dlq_replayed_total` · `lock_ttl_exceeded_total` ·
    /// `logging_failures_total`). ⇒ 🔒 Con số *"12/12"* trong tài liệu là **nói quá**, và nó tồn tại được
    /// **chính vì** không có phép kiểm nào.
    ///
    /// ✅ **CÁCH ĐÓNG NỢ (không phải bằng cách thêm chữ vào tài liệu):** mỗi tên metric có **ký hiệu** để tìm,
    /// và `MetricEmitterTests` khẳng định **CẢ HAI CHIỀU** bằng cách quét mã nguồn:
    /// <list type="number">
    ///   <item>tên **KHÔNG** nằm trong <see cref="ChuaCoNoiPhat"/> ⇒ **phải** tìm thấy lời gọi thật
    ///     (`.<ký hiệu>.Add(`, `.<ký hiệu>.Record(`, hoặc `.<ký hiệu>(`) ở `src`/`modules`/`shared`;</item>
    ///   <item>tên **nằm trong** <see cref="ChuaCoNoiPhat"/> ⇒ **KHÔNG** được có lời gọi nào (nếu có ⇒ khai báo
    ///     **đã cũ** ⇒ phải xoá khỏi danh sách nợ);</item>
    ///   <item>bảng này phủ **đúng** `All` — không thiếu, không thừa.</item>
    /// </list>
    /// 🔑 Nhờ vậy con số *"có nơi phát"* **không thể trôi** nữa: nó là **kết quả của một phép đo trong CI**,
    /// không phải một câu trong tài liệu.
    ///
    /// ⚠️ **Ký hiệu dùng `nameof(...)`** ⇒ 🔒 đổi tên thành viên trong `eCommerceMetrics` là **lỗi build**,
    /// không phải một bảng lặng lẽ sai (bảng viết bằng chuỗi thì `nameof` mới là nguồn sự thật).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KyHieuMetric =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingOutboxPending] = nameof(eCommerceMetrics.SetMessagingOutboxPending),
            [MessagingInboxFailedTotal] = nameof(eCommerceMetrics.MessagingInboxFailed),
            [MessagingDlqSize] = nameof(eCommerceMetrics.SetMessagingDlqSize),
            [MessagingDlqReplayedTotal] = nameof(eCommerceMetrics.MessagingDlqReplayed),
            [LockTimeoutTotal] = nameof(eCommerceMetrics.LockTimeout),
            [LockSkippedTotal] = nameof(eCommerceMetrics.LockSkipped),
            [LockTtlExceededTotal] = nameof(eCommerceMetrics.LockTtlExceeded),
            [HttpRequestDurationSeconds] = nameof(eCommerceMetrics.HttpRequestDurationSeconds),
            [PaymentWebhookRejectedTotal] = nameof(eCommerceMetrics.PaymentWebhookRejected),
            [ChannelSyncFailedTotal] = nameof(eCommerceMetrics.ChannelSyncFailed),
            [InventoryReconMismatchSkus] = nameof(eCommerceMetrics.SetInventoryReconMismatchSkus),
            [TaxUnclassifiedSkus] = nameof(eCommerceMetrics.SetTaxUnclassifiedSkus),
            [PaymentReconUnresolved] = nameof(eCommerceMetrics.SetPaymentReconUnresolved),
            [PaymentReconChangedTotal] = nameof(eCommerceMetrics.PaymentReconChanged),
            [LoggingFailuresTotal] = nameof(eCommerceMetrics.LoggingFailures)
        };

    /// <summary>
    /// 🚨 **CÁC METRIC ĐÃ ĐĂNG KÝ NHƯNG CHƯA CÓ ĐIỂM PHÁT** — mỗi dòng ghi **vì sao** và **cần gì** để đóng.
    ///
    /// 📌 **Đây là nợ THẬT, không phải danh sách cho vui:** metric đã khai báo mà không ai phát là
    /// **nói dối** (`coding-conventions`: *"Mọi metric phải có nơi phát"*). 🔑 Danh sách này tồn tại
    /// **để con số không bị nói quá** — và `MetricEmitterTests` khoá nó **hai chiều**.
    ///
    /// 🔒 **Đây là TẬP CON của <see cref="KyHieuMetric"/>** (bảng ký hiệu phủ **mọi** tên, kể cả tên chưa
    /// có nơi phát — vì vẫn cần ký hiệu để **chứng minh** là chúng chưa hề được gọi).
    ///
    /// ⚠️ **KHÔNG thêm tên vào đây để làm test xanh** khi lẽ ra chỉ cần thêm **một dòng gọi**:
    /// mỗi mục ở đây phải có **hạ tầng còn thiếu** nêu rõ (nếu không có lý do đó, hãy **phát metric**).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ChuaCoNoiPhat =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingOutboxPending] =
                "CAN HA TANG: chua co outbox/relay. Su kien hien di in-process (ABP local event bus), "
                + "khong co bang outbox nao de dem ⇒ moi con so se la BIA. Cho `XC-002` (outbox + relay): "
                + "relay dem so ban ghi ton dong roi goi `SetMessagingOutboxPending`.",

            [MessagingDlqReplayedTotal] =
                "CAN TINH NANG: DLQ co chu y KHONG co phat lai TU DONG (`DeadLetterAppService` chi doc + "
                + "danh dau). Danh dau 'da xu ly' KHONG phai 'da phat lai' ⇒ phat metric nay luc danh dau la "
                + "NOI DOI. Cho luong phat lai thu cong co that (roi moi dem tai noi phat lai).",

            [LockTtlExceededTotal] =
                "CAN NOI PHAT HIEN: khoa di qua `IAbpDistributedLock` cua ABP (Redis), TTL do thu vien giu va "
                + "KHONG lo ra ngoai ⇒ khong co cho nao biet 'thao tac vuot TTL'. Cho wrapper lock co doc TTL.",

            [LoggingFailuresTotal] =
                "CAN HA TANG: chua co logger provider boc sink de bat loi ghi log. Logging hien dung provider "
                + "mac dinh cua ABP/ASP.NET ⇒ khong co diem bat 'logger/sink hong'."
        };
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

    /// <summary>`REC-001` mục 7 — số lần lượt đối soát **đổi** trạng thái giao dịch (nhãn `result`).</summary>
    public Counter<long> PaymentReconChanged { get; }

    // ---- Histogram ----
    public Histogram<double> HttpRequestDurationSeconds { get; }

    // ---- Gauge (giá trị do nơi phát cập nhật) ----
    private long _messagingOutboxPending;
    private long _messagingDlqSize;
    private long _inventoryReconMismatchSkus;
    private long _taxUnclassifiedSkus;

    /// <summary>`REC-001` mục 7 — số giao dịch đối soát **cổng không xác nhận được** ở lượt gần nhất.</summary>
    private long _paymentReconUnresolved;

    public void SetMessagingOutboxPending(long value) => Interlocked.Exchange(ref _messagingOutboxPending, value);
    public void SetMessagingDlqSize(long value) => Interlocked.Exchange(ref _messagingDlqSize, value);
    public void SetTaxUnclassifiedSkus(long value) => Interlocked.Exchange(ref _taxUnclassifiedSkus, value);

    /// <summary>
    /// Ghi số giao dịch đối soát **cổng không xác nhận được** (`REC-001` mục 7).
    /// 🚨 **ĐỌC CHO ĐÚNG:** bằng **số giao dịch cần đối soát** ⇒ lượt quét **không mang lại thông tin nào**
    /// ⇒ 🔑 **cấu hình sai hoặc cổng chết**, **KHÔNG PHẢI** *"hệ thống khoẻ"*.
    /// </summary>
    public void SetPaymentReconUnresolved(long value) => Interlocked.Exchange(ref _paymentReconUnresolved, value);

    /// <summary>Ghi số cặp `(kho, SKU)` đang LỆCH khi đối soát kho (`INV-001` ADR-10).</summary>
    public void SetInventoryReconMismatchSkus(long value) => Interlocked.Exchange(ref _inventoryReconMismatchSkus, value);

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
        PaymentReconChanged = _meter.CreateCounter<long>(eCommerceMetricNames.PaymentReconChangedTotal,
            unit: "{transaction}", description: "Số lần đối soát thanh toán ĐỔI trạng thái giao dịch (REC-001).");

        HttpRequestDurationSeconds = _meter.CreateHistogram<double>(eCommerceMetricNames.HttpRequestDurationSeconds,
            unit: "s", description: "Độ trễ xử lý request HTTP.");

        _meter.CreateObservableGauge(eCommerceMetricNames.MessagingOutboxPending,
            () => Interlocked.Read(ref _messagingOutboxPending),
            unit: "{event}", description: "Số bản ghi outbox đang tồn đọng (XC-002 NFR-003).");
        _meter.CreateObservableGauge(eCommerceMetricNames.MessagingDlqSize,
            () => Interlocked.Read(ref _messagingDlqSize),
            unit: "{event}", description: "Kích thước DLQ — > 0 thì phải cảnh báo (XC-002).");
        // 🚨 **GAUGE NÀY ĐÃ TỪNG BỊ THIẾU HOÀN TOÀN** (phát hiện 17/10/2026 khi rà bảng đăng ký).
        // 📌 Triệu chứng: `SetInventoryReconMismatchSkus(value)` **CÓ** `Interlocked.Exchange` vào biến
        // `_inventoryReconMismatchSkus`, ⚠️ nhưng **KHÔNG có** `CreateObservableGauge` nào đọc biến đó
        // ⇒ 🔒 giá trị ghi vào **MẤT**, và metric `inventory_recon_mismatch_skus` **KHÔNG BAO GIỜ phát**.
        // 🚨 **Và đây là loại hỏng TỆ NHẤT:** job đối soát kho **vẫn chạy**, **vẫn ghi** giá trị, **không có
        // lỗi nào** — chỉ là **không ai** nhận được con số. 🔑 Một gauge *"0 lệch"* trên biểu đồ khi ấy là
        // **LỜI NÓI DỐI**: `0` vì **chưa ai ĐO**, không phải vì **không có lệch** — đúng cái mà chú thích
        // `InventoryReconciliationWorker` nói nó sinh ra để chống.
        // 📌 `TC06_phat_ra_DUNG_..._metric_da_dang_ky` **BẮT ĐƯỢC** điều này ngay khi tôi thêm hằng số vào
        // `All` ⇒ lưới an toàn cũ **có** tác dụng, ⚠️ chỉ là trước đó **không ai** đưa hằng số vào `All`
        // nên **cả hai** chiều đều không được kiểm (xem ca `MOI_hang_so_metric_PHAI_co_trong_All`).
        _meter.CreateObservableGauge(eCommerceMetricNames.InventoryReconMismatchSkus,
            () => Interlocked.Read(ref _inventoryReconMismatchSkus),
            unit: "{pair}",
            description: "Số cặp (kho, SKU) có OnHand LỆCH với SUM(sổ cái) — đối soát định kỳ (INV-001 ADR-10).");
        _meter.CreateObservableGauge(eCommerceMetricNames.TaxUnclassifiedSkus,
            () => Interlocked.Read(ref _taxUnclassifiedSkus),
            unit: "{sku}", description: "Số SKU chưa phân loại thuế — kế toán phải xử lý (XC-006).");
        _meter.CreateObservableGauge(eCommerceMetricNames.PaymentReconUnresolved,
            () => Interlocked.Read(ref _paymentReconUnresolved),
            unit: "{transaction}",
            description: "Số giao dịch đối soát CỔNG KHÔNG XÁC NHẬN ĐƯỢC. Bằng số giao dịch cần đối soát " +
                "nghĩa là lượt quét KHÔNG mang lại thông tin (cấu hình sai / cổng chết) — KHÔNG PHẢI 'khoẻ' (REC-001).");
    }
}
