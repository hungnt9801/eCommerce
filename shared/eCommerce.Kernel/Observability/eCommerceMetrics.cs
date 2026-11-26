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

    // ---- Nghiệp vụ ĐƠN HÀNG (`SAL-003` NFR-012, 24/10/2026) ----    /// <summary>
    /// **SỐ LẦN CHUYỂN TRẠNG THÁI ĐƠN** — nhãn `axis`/`from`/`to` (giá trị **hữu hạn**: 4 trục × ~10 giá trị).
    ///
    /// 🔑 **VÌ SAO PHẢI CÓ:** `SAL-003` NFR-012 yêu cầu *"mọi chuyển trạng thái có metric"*. 📌 Không có nó thì
    /// câu hỏi vận hành *"đơn bị kẹt ở bước nào"* chỉ trả lời được bằng cách quét `OrdStatusHistory` — chậm và
    /// không vẽ được biểu đồ. 🚨 **CẤM** gắn nhãn `orderId` (`XC-005` BR-009 — cardinality cao).
    /// </summary>
    public const string SalesOrderTransitionTotal = "sales_order_transition_total";

    // ---- Nghiệp vụ ĐỊA CHỈ / danh mục ĐVHC (`ADDR-001` NFR-008/009, WP-18) ----
    /// <summary>
    /// Số lượt nạp danh mục ĐVHC — nhãn <c>trigger</c> (`job`/`import`) và <c>outcome</c>
    /// (`applied`/`blocked`/`error`/`skipped`). Cardinality ≤ 8 (`D-78` §5.1).
    /// 🚨 **CẤM** gắn nhãn mã lỗi/tỉnh (`XC-005` BR-009).
    /// </summary>
    public const string AddressCatalogSyncTotal = "address_catalog_sync_total";

    /// <summary>Số lần đọc cache danh mục **trúng** — chỉ để chẩn đoán cache có tác dụng (NFR-008).</summary>
    public const string AddressCatalogCacheHitsTotal = "address_catalog_cache_hits_total";

    /// <summary>Số lần đọc cache danh mục **trượt** — đi kèm ca trúng để suy ra tỉ lệ.</summary>
    public const string AddressCatalogCacheMissesTotal = "address_catalog_cache_misses_total";

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

    // ---- Nghiệp vụ THÔNG BÁO (`NOT-001`, WP-22) ----
    /// <summary>Số tin được xếp hàng — nhãn `channel` (`email`/`sms`/`zalo_oa`).</summary>
    public const string NotificationQueuedTotal = "notification_queued_total";

    /// <summary>Số tin đã bàn giao — nhãn `channel`, `result` (`accepted`/`already_sent`).</summary>
    public const string NotificationDispatchedTotal = "notification_dispatched_total";

    /// <summary>Số tin lỗi/hết hạn — nhãn `channel`, `reason` (mã lỗi hữu hạn).</summary>
    public const string NotificationQueueFailedTotal = "notification_queue_failed_total";

    /// <summary>Số lần render thất bại — nhãn `reason` (mã lỗi).</summary>
    public const string NotificationRenderFailedTotal = "notification_render_failed_total";

    /// <summary>Tồn đọng hàng đợi (`ChoGui`) — gauge, không nhãn.</summary>
    public const string NotificationQueueBacklog = "notification_queue_backlog";

    /// <summary>Số lần lease bàn giao hết hạn được khôi phục — không nhãn.</summary>
    public const string NotificationDispatchLeaseExpiredTotal = "notification_dispatch_lease_expired_total";

    /// <summary>Số tin bị khử trùng cửa sổ — nhãn `channel`.</summary>
    public const string NotificationWindowSuppressedTotal = "notification_window_suppressed_total";

    // ---- Read-model BÁO CÁO (`REP-001` / `WP-26`) ----
    /// <summary>Tuổi dữ liệu tổng hợp (`now − LastRunAtUtc`) — Gauge, nhãn `projection` (NFR-003).</summary>
    public const string ReportingWatermarkLagSeconds = "reporting_watermark_lag_seconds";

    /// <summary>Số lần cửa sổ lỗi / đối chiếu phát hiện lệch — nhãn `projection`, `reason`.</summary>
    public const string ReportingProjectionFailedTotal = "reporting_projection_failed_total";

    /// <summary>Số dòng **đã ghi** (thay thế) mỗi cửa sổ — nhãn `projection`.</summary>
    public const string ReportingRowsWrittenTotal = "reporting_rows_written_total";

    /// <summary>Số lần chạm thay thế (mỗi cửa sổ ghi lại) — nhãn `projection`.</summary>
    public const string ReportingReplaceConflictTotal = "reporting_replace_conflict_total";

    // ---- KHUYẾN MÃI (`PRO-002` §8 — 9 metric = 6 của PRO-002 + 3 của PRO-001) ----
    /// <summary>Độ trễ một lượt đánh giá — Histogram, nhãn `kind`.</summary>
    public const string PromotionEvalDurationSeconds = "promotion_eval_duration_seconds";

    /// <summary>Vượt ngân sách chạy — Counter, nhãn `kind`.</summary>
    public const string PromotionEvalBudgetExceededTotal = "promotion_eval_budget_exceeded_total";

    /// <summary>Candidate bị loại — Counter, nhãn `kind`, `reason` (12 giá trị enum).</summary>
    public const string PromotionConditionRejectedTotal = "promotion_condition_rejected_total";

    /// <summary>Khuyến mãi được áp (sau commit) — Counter, nhãn `kind`, `channel`.</summary>
    public const string PromotionAppliedTotal = "promotion_applied_total";

    /// <summary>Chạm trần lượt — Counter, nhãn `kind`.</summary>
    public const string PromotionUsageLimitHitTotal = "promotion_usage_limit_hit_total";

    /// <summary>Cắt candidate vượt `MaxCandidatesPerCart` — Counter, nhãn `kind`.</summary>
    public const string PromotionCandidatesTruncatedTotal = "promotion_candidates_truncated_total";

    /// <summary>Giữ lượt voucher (`PRO-001`) — Counter, nhãn `kind`, `result`.</summary>
    public const string PromotionVoucherReservedTotal = "promotion_voucher_reserved_total";

    /// <summary>Nhả lượt voucher (`PRO-001`) — Counter, nhãn `reason`.</summary>
    public const string PromotionVoucherReleasedTotal = "promotion_voucher_released_total";

    /// <summary>Lượt giữ voucher hết TTL (`PRO-001`) — Counter, nhãn `kind`.</summary>
    public const string PromotionVoucherReservationExpiredTotal = "promotion_voucher_reservation_expired_total";

    /// <summary>Toàn bộ tên metric — nguồn sự thật duy nhất cho test `TC-06`.</summary>
    public static readonly IReadOnlyList<string> All =    [
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
        LoggingFailuresTotal,
        // 🆕 `SAL-003` NFR-012 (24/10/2026)
        SalesOrderTransitionTotal,
        // 🆕 `WP-18` — `ADDR-001` NFR-008/009 (26/11/2026)
        AddressCatalogSyncTotal,
        AddressCatalogCacheHitsTotal,
        AddressCatalogCacheMissesTotal,
        // 🆕 `WP-22` — `NOT-001` (26/11/2026): 7 metric thông báo → 19→26.
        NotificationQueuedTotal,
        NotificationDispatchedTotal,
        NotificationQueueFailedTotal,
        NotificationRenderFailedTotal,
        NotificationQueueBacklog,
        NotificationDispatchLeaseExpiredTotal,
        NotificationWindowSuppressedTotal,
        // 🆕 `REP-001` / `WP-26` (27/11/2026): 4 metric read-model → 26→30.
        ReportingWatermarkLagSeconds,
        ReportingProjectionFailedTotal,
        ReportingRowsWrittenTotal,
        ReportingReplaceConflictTotal,
        // 🆕 `PRO-002` (`WP-24`) — 9 metric khuyến mãi.
        PromotionEvalDurationSeconds,
        PromotionEvalBudgetExceededTotal,
        PromotionConditionRejectedTotal,
        PromotionAppliedTotal,
        PromotionUsageLimitHitTotal,
        PromotionCandidatesTruncatedTotal,
        PromotionVoucherReservedTotal,
        PromotionVoucherReleasedTotal,
        PromotionVoucherReservationExpiredTotal
    ];

    /// <summary>
    /// 🚨 **BẢNG "KÝ HIỆU NÀO ĐƯỢC GỌI Ở NƠI PHÁT" — có đủ cho MỌI tên đã đăng ký, kể cả tên CHƯA có nơi phát.**
    ///
    /// 🔑 **VÌ SAO CẦN (đo được, `S10` 21/10/2026):** từ 13/10/2026 tài liệu nói *"12 metric ĐÃ CÓ NƠI PHÁT"*,
    /// ⚠️ nhưng **chưa bao giờ** có phép kiểm nào đối chiếu *"đã đăng ký"* với *"có chỗ gọi thật"*.
    /// 📌 Đo lại bằng cách quét `src/` + `modules/` + `shared/` (`S16`, 29/10/2026): **15/16** tên có điểm phát,
    /// **1/16 KHÔNG** (`messaging_dlq_replayed_total` — chưa có luồng phát lại thật). ⇒ 🔒 Con số *"12/12"*
    /// trong tài liệu cũ là **nói quá**, và nó tồn tại được **chính vì** không có phép kiểm nào.
    /// ✅ **`S16b` (29/10/2026): 16/16** — `DeadLetterAppService.PhatLaiAsync` phát lại event thật lên bus
    /// (qua outbox) và **chỉ** tăng counter **sau khi** commit; `ChuaCoNoiPhat` nay **rỗng**.
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
            [LoggingFailuresTotal] = nameof(eCommerceMetrics.LoggingFailures),
            [SalesOrderTransitionTotal] = nameof(eCommerceMetrics.SalesOrderTransition),
            [AddressCatalogSyncTotal] = nameof(eCommerceMetrics.AddressCatalogSync),
            [AddressCatalogCacheHitsTotal] = nameof(eCommerceMetrics.AddressCatalogCacheHits),
            [AddressCatalogCacheMissesTotal] = nameof(eCommerceMetrics.AddressCatalogCacheMisses),
            // 🆕 `WP-22` (`NOT-001`)
            [NotificationQueuedTotal] = nameof(eCommerceMetrics.NotificationQueued),
            [NotificationDispatchedTotal] = nameof(eCommerceMetrics.NotificationDispatched),
            [NotificationQueueFailedTotal] = nameof(eCommerceMetrics.NotificationQueueFailed),
            [NotificationRenderFailedTotal] = nameof(eCommerceMetrics.NotificationRenderFailed),
            [NotificationQueueBacklog] = nameof(eCommerceMetrics.SetNotificationQueueBacklog),
            [NotificationDispatchLeaseExpiredTotal] = nameof(eCommerceMetrics.NotificationDispatchLeaseExpired),
            [NotificationWindowSuppressedTotal] = nameof(eCommerceMetrics.NotificationWindowSuppressed),
            // 🆕 `REP-001` / `WP-26`
            [ReportingWatermarkLagSeconds] = nameof(eCommerceMetrics.SetReportingWatermarkLagSeconds),
            [ReportingProjectionFailedTotal] = nameof(eCommerceMetrics.ReportingProjectionFailed),
            [ReportingRowsWrittenTotal] = nameof(eCommerceMetrics.ReportingRowsWritten),
            [ReportingReplaceConflictTotal] = nameof(eCommerceMetrics.ReportingReplaceConflict),
            // 🆕 `PRO-002` (`WP-24`)
            [PromotionEvalDurationSeconds] = nameof(eCommerceMetrics.PromotionEvalDurationSeconds),
            [PromotionEvalBudgetExceededTotal] = nameof(eCommerceMetrics.PromotionEvalBudgetExceeded),
            [PromotionConditionRejectedTotal] = nameof(eCommerceMetrics.PromotionConditionRejected),
            [PromotionAppliedTotal] = nameof(eCommerceMetrics.PromotionApplied),
            [PromotionUsageLimitHitTotal] = nameof(eCommerceMetrics.PromotionUsageLimitHit),
            [PromotionCandidatesTruncatedTotal] = nameof(eCommerceMetrics.PromotionCandidatesTruncated),
            [PromotionVoucherReservedTotal] = nameof(eCommerceMetrics.PromotionVoucherReserved),
            [PromotionVoucherReleasedTotal] = nameof(eCommerceMetrics.PromotionVoucherReleased),
            [PromotionVoucherReservationExpiredTotal] = nameof(eCommerceMetrics.PromotionVoucherReservationExpired)
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
            // ✅ **RỖNG TỪ 29/10/2026 (`S16b`)** — không còn metric nào đã đăng ký mà chưa có điểm phát.
            // 📌 `MessagingDlqReplayedTotal` **ĐÃ ĐÓNG NỢ**: `DeadLetterAppService.PhatLaiAsync` **publish lại
            // event gốc** lên `IDistributedEventBus` (`useOutbox: true`, cùng UoW với dấu `ReplayedAtUtc`) rồi
            // mới tăng counter. 🔑 Điều kiện để xoá khỏi danh sách này **không** phải "đã có luồng đánh dấu" —
            // mà là **đã có luồng PHÁT LẠI THẬT**; đếm *"đánh dấu đã xử lý"* thành *"đã phát lại"* là **nói dối**
            // (`XC-005` §19.3/§20.3).
            // 🔒 KHÔNG được thêm lại tên nào vào đây để làm test xanh — `MetricEmitterTests` kiểm **hai chiều**
            // và sẽ ĐỎ nếu khai "chưa có" trong khi mã đã có lời gọi.
            //
            // 🆕 `PRO-002` (`WP-24`) — 3 metric GIỮ/NHẢ LƯỢT VOUCHER thuộc đường ghi của **`PRO-001`**
            // (`ReserveAsync`/`CommitAsync`/`ReleaseAsync`), **chưa** có code ở WP-24. Đăng ký tên theo §8
            // nhưng **chưa có điểm phát** — nợ có chủ, đóng khi `PRO-001` hiện thực đường ghi lượt.
            [PromotionVoucherReservedTotal] = "PRO-001 chưa hiện thực đường giữ lượt voucher ở WP-24.",
            [PromotionVoucherReleasedTotal] = "PRO-001 chưa hiện thực đường nhả lượt voucher ở WP-24.",
            [PromotionVoucherReservationExpiredTotal] = "PRO-001 chưa hiện thực job hết TTL lượt voucher ở WP-24.",
            // `promotion_applied_total` phát **sau commit** ở `Sales` khi TẠO ĐƠN (`SAL-001`); WP-24 chỉ giữ chỗ,
            // chưa tạo `Order` ⇒ chưa có điểm phát thật. Đóng khi `Sales` tạo đơn + áp khuyến mãi.
            [PromotionAppliedTotal] = "Sales chưa tạo Order ở WP-24 (SAL-001) — chưa có mốc 'sau commit'."
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

    /// <summary>`SAL-003` NFR-012 — nhãn **trục** (`Fulfillment`/`Payment`/`Reconciliation`/`Closure`).</summary>
    public const string Axis = "axis";

    /// <summary>`SAL-003` NFR-012 — trạng thái **trước** (số enum của trục; hữu hạn).</summary>
    public const string FromStatus = "from";

    /// <summary>`SAL-003` NFR-012 — trạng thái **sau** (số enum của trục; hữu hạn).</summary>
    public const string ToStatus = "to";

    /// <summary>`ADDR-001` — đường nạp danh mục: `job` / `import` (hữu hạn).</summary>
    public const string Trigger = "trigger";

    /// <summary>`ADDR-001` — kết cục lượt nạp: `applied` / `blocked` / `error` / `skipped` (hữu hạn).</summary>
    public const string Outcome = "outcome";

    /// <summary>`REP-001` / `WP-26` — phép chiếu read-model: `ban-hang` / `ton-kho` (hữu hạn).</summary>
    public const string Projection = "projection";

    public static readonly IReadOnlyList<string> Allowed =
    [
        Channel, Provider, EventName, Status, Reason, Kind, Module, Result, Method, Route,
        Axis, FromStatus, ToStatus, Trigger, Outcome, Projection
    ];

    /// <summary>Nhãn **BỊ CẤM** vì cardinality cao — test `TC-07` chặn nếu chúng lọt vào danh sách cho phép.</summary>
    public static readonly IReadOnlyList<string> Forbidden =
    [
        "orderId", "orderNumber", "sku", "userId", "correlationId", "email", "phone", "lockKey",
        // 🆕 `PRO-002` §8 — nhãn định danh cardinality cao, chỉ dùng ở log.
        "promotionId", "voucherCode", "customerId"
    ];
}

/// <summary>
/// Phát **16 metric** bằng `System.Diagnostics.Metrics` **có sẵn trong .NET** — **KHÔNG thêm gói NuGet nào**.
///
/// 🎯 VÌ SAO CÁCH NÀY (chốt Q1 của `XC-005` theo hướng thực dụng):
///   `Meter`/`Counter`/`Histogram`/`ObservableGauge` nằm trong BCL (.NET 6+). Chọn công cụ thu thập
///   (OpenTelemetry → Prometheus/Grafana…) là việc của **tầng xuất**, có thể thêm **sau** mà **không phải
///   sửa nơi phát metric**. 📌 `S16` (29/10/2026): **15/16** metric có điểm phát thật; `S16b` (cùng ngày):
///   **16/16** — `messaging_dlq_replayed_total` có điểm phát ở `DeadLetterAppService.PhatLaiAsync`
///   (xem `ChuaCoNoiPhat`, nay rỗng).
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

    /// <summary>
    /// 🚨 **SỐ LẦN PHÁT LẠI DLQ THÀNH CÔNG** (`XC-002` NFR-009).
    /// 🔒 Chỉ tăng **sau khi** UoW (dấu `ReplayedAtUtc` + dòng outbox) đã **commit** ⇒ không đếm một lần
    /// phát lại có thể bị rollback. 📌 Không tăng khi: dòng đã phát lại trước đó (idempotent), `TenSuKien`
    /// không ánh xạ được (fail-closed), payload không deserialize được, hoặc publish/commit ném.
    /// Nhãn: `eventName` (hữu hạn — số loại sự kiện) · `result` = `ok` (`XC-005` §5.1).
    /// </summary>
    public Counter<long> MessagingDlqReplayed { get; }

    public Counter<long> LockTimeout { get; }
    public Counter<long> LockSkipped { get; }
    public Counter<long> LockTtlExceeded { get; }
    public Counter<long> PaymentWebhookRejected { get; }
    public Counter<long> ChannelSyncFailed { get; }
    public Counter<long> LoggingFailures { get; }

    /// <summary>`REC-001` mục 7 — số lần lượt đối soát **đổi** trạng thái giao dịch (nhãn `result`).</summary>
    public Counter<long> PaymentReconChanged { get; }

    /// <summary>`SAL-003` NFR-012 — số lần chuyển trạng thái đơn, nhãn `axis`/`from`/`to`.</summary>
    public Counter<long> SalesOrderTransition { get; }

    /// <summary>`ADDR-001` NFR-008 — số lượt nạp danh mục ĐVHC, nhãn `trigger`/`outcome`.</summary>
    public Counter<long> AddressCatalogSync { get; }

    /// <summary>`ADDR-001` NFR-008 — số lần cache danh mục **trúng** (chẩn đoán cache có tác dụng).</summary>
    public Counter<long> AddressCatalogCacheHits { get; }

    /// <summary>`ADDR-001` NFR-008 — số lần cache danh mục **trượt**.</summary>
    public Counter<long> AddressCatalogCacheMisses { get; }

    // ---- Nghiệp vụ THÔNG BÁO (`NOT-001`) ----
    public Counter<long> NotificationQueued { get; }
    public Counter<long> NotificationDispatched { get; }
    public Counter<long> NotificationQueueFailed { get; }
    public Counter<long> NotificationRenderFailed { get; }
    public Counter<long> NotificationDispatchLeaseExpired { get; }
    public Counter<long> NotificationWindowSuppressed { get; }

    // ---- Read-model BÁO CÁO (`REP-001` / `WP-26`) ----
    /// <summary>Cửa sổ lỗi / đối chiếu phát hiện lệch — nhãn `projection`, `reason`.</summary>
    public Counter<long> ReportingProjectionFailed { get; }

    /// <summary>Số dòng đã ghi (thay thế) mỗi cửa sổ — nhãn `projection`.</summary>
    public Counter<long> ReportingRowsWritten { get; }

    /// <summary>Số lần chạm thay thế (mỗi cửa sổ ghi lại) — nhãn `projection`.</summary>
    public Counter<long> ReportingReplaceConflict { get; }

    // ---- KHUYẾN MÃI (`PRO-002`) ----
    public Counter<long> PromotionEvalBudgetExceeded { get; }
    public Counter<long> PromotionConditionRejected { get; }
    public Counter<long> PromotionApplied { get; }
    public Counter<long> PromotionUsageLimitHit { get; }
    public Counter<long> PromotionCandidatesTruncated { get; }
    public Counter<long> PromotionVoucherReserved { get; }
    public Counter<long> PromotionVoucherReleased { get; }
    public Counter<long> PromotionVoucherReservationExpired { get; }

    // ---- Histogram ----
    public Histogram<double> HttpRequestDurationSeconds { get; }

    /// <summary>`PRO-002` — độ trễ một lượt đánh giá khuyến mãi, nhãn `kind`.</summary>
    public Histogram<double> PromotionEvalDurationSeconds { get; }

    // ---- Gauge (giá trị do nơi phát cập nhật) ----
    private long _messagingOutboxPending;
    private long _messagingDlqSize;
    private long _inventoryReconMismatchSkus;
    private long _taxUnclassifiedSkus;

    /// <summary>`REC-001` mục 7 — số giao dịch đối soát **cổng không xác nhận được** ở lượt gần nhất.</summary>
    private long _paymentReconUnresolved;

    /// <summary>`NOT-001` — tồn đọng hàng đợi (`ChoGui`) ở lượt gần nhất.</summary>
    private long _notificationQueueBacklog;

    /// <summary>`WP-26` — tuổi dữ liệu (giây) theo `projection`; Gauge đa nhãn cập nhật qua dictionary.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _reportingWatermarkLag =
        new(StringComparer.Ordinal);

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

    /// <summary>`NOT-001` — ghi tồn đọng hàng đợi (`ChoGui`).</summary>
    public void SetNotificationQueueBacklog(long value) => Interlocked.Exchange(ref _notificationQueueBacklog, value);

    /// <summary>
    /// `WP-26` — ghi tuổi dữ liệu (giây) cho một `projection` (`ban-hang`/`ton-kho`). Gauge đa nhãn: mỗi
    /// giá trị là một chuỗi thời gian riêng, tập nhãn **hữu hạn** (2 giá trị) — không vi phạm `XC-005` BR-009.
    /// </summary>
    public void SetReportingWatermarkLagSeconds(long value, string projection)
        => _reportingWatermarkLag[projection] = value;

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
            unit: "{occurrence}",
            description: "Số lần LEASE khoá phân tán bị MẤT (HandleLostToken) — tức vượt TTL, kỳ vọng 0 (XC-003).");
        PaymentWebhookRejected = _meter.CreateCounter<long>(eCommerceMetricNames.PaymentWebhookRejectedTotal,
            unit: "{webhook}", description: "Webhook thanh toán bị từ chối (sai chữ ký, sai số tiền).");
        ChannelSyncFailed = _meter.CreateCounter<long>(eCommerceMetricNames.ChannelSyncFailedTotal,
            unit: "{sync}", description: "Đồng bộ sàn thất bại.");
        LoggingFailures = _meter.CreateCounter<long>(eCommerceMetricNames.LoggingFailuresTotal,
            unit: "{failure}", description: "Ghi log thất bại (logger/sink hỏng) — log là best-effort.");
        PaymentReconChanged = _meter.CreateCounter<long>(eCommerceMetricNames.PaymentReconChangedTotal,
            unit: "{transaction}", description: "Số lần đối soát thanh toán ĐỔI trạng thái giao dịch (REC-001).");
        SalesOrderTransition = _meter.CreateCounter<long>(eCommerceMetricNames.SalesOrderTransitionTotal,
            unit: "{transition}",
            description: "Số lần chuyển trạng thái đơn (SAL-003 NFR-012) — nhãn axis/from/to, CẤM orderId.");

        AddressCatalogSync = _meter.CreateCounter<long>(eCommerceMetricNames.AddressCatalogSyncTotal,
            unit: "{run}",
            description: "Số lượt nạp danh mục ĐVHC (ADDR-001) — nhãn trigger/outcome, CẤM mã/tỉnh.");
        AddressCatalogCacheHits = _meter.CreateCounter<long>(eCommerceMetricNames.AddressCatalogCacheHitsTotal,
            unit: "{hit}", description: "Số lần cache danh mục ĐVHC trúng (ADDR-001).");
        AddressCatalogCacheMisses = _meter.CreateCounter<long>(eCommerceMetricNames.AddressCatalogCacheMissesTotal,
            unit: "{miss}", description: "Số lần cache danh mục ĐVHC trượt (ADDR-001).");

        // 🆕 `NOT-001` (WP-22)
        NotificationQueued = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationQueuedTotal,
            unit: "{item}", description: "Số tin được xếp hàng (NOT-001) — nhãn channel, CẤM email/phone.");
        NotificationDispatched = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationDispatchedTotal,
            unit: "{item}", description: "Số tin đã bàn giao (NOT-001) — nhãn channel/result.");
        NotificationQueueFailed = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationQueueFailedTotal,
            unit: "{item}", description: "Số tin lỗi/hết hạn (NOT-001) — nhãn channel/reason.");
        NotificationRenderFailed = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationRenderFailedTotal,
            unit: "{failure}", description: "Số lần render mẫu tin thất bại (NOT-001) — nhãn reason.");
        NotificationDispatchLeaseExpired = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationDispatchLeaseExpiredTotal,
            unit: "{occurrence}", description: "Số lần lease bàn giao hết hạn được khôi phục (NOT-001).");
        NotificationWindowSuppressed = _meter.CreateCounter<long>(eCommerceMetricNames.NotificationWindowSuppressedTotal,
            unit: "{item}", description: "Số tin bị khử trùng cửa sổ (NOT-001) — nhãn channel.");

        // 🆕 `REP-001` / `WP-26`
        ReportingProjectionFailed = _meter.CreateCounter<long>(eCommerceMetricNames.ReportingProjectionFailedTotal,
            unit: "{failure}",
            description: "Cửa sổ tổng hợp lỗi / đối chiếu phát hiện lệch (REP-001) — nhãn projection, reason.");
        ReportingRowsWritten = _meter.CreateCounter<long>(eCommerceMetricNames.ReportingRowsWrittenTotal,
            unit: "{row}", description: "Số dòng đã ghi (thay thế) mỗi cửa sổ (REP-001) — nhãn projection.");
        ReportingReplaceConflict = _meter.CreateCounter<long>(eCommerceMetricNames.ReportingReplaceConflictTotal,
            unit: "{replace}", description: "Số lần chạm thay thế mỗi cửa sổ (REP-001) — nhãn projection.");

        // 🆕 `PRO-002` (`WP-24`) — 9 metric khuyến mãi.
        PromotionEvalBudgetExceeded = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionEvalBudgetExceededTotal,
            unit: "{eval}", description: "Số lượt đánh giá vượt ngân sách chạy (PRO-002) — nhãn kind.");
        PromotionConditionRejected = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionConditionRejectedTotal,
            unit: "{candidate}", description: "Số candidate bị loại (PRO-002) — nhãn kind, reason (hữu hạn).");
        PromotionApplied = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionAppliedTotal,
            unit: "{order}", description: "Số lượt khuyến mãi được áp (PRO-002) — nhãn kind, channel.");
        PromotionUsageLimitHit = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionUsageLimitHitTotal,
            unit: "{candidate}", description: "Số candidate chạm trần lượt (PRO-002) — nhãn kind.");
        PromotionCandidatesTruncated = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionCandidatesTruncatedTotal,
            unit: "{candidate}", description: "Số candidate bị cắt do MaxCandidatesPerCart (PRO-002) — nhãn kind.");
        PromotionVoucherReserved = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionVoucherReservedTotal,
            unit: "{reservation}", description: "Số lượt giữ voucher (PRO-001) — nhãn kind, result.");
        PromotionVoucherReleased = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionVoucherReleasedTotal,
            unit: "{reservation}", description: "Số lượt nhả voucher (PRO-001) — nhãn reason.");
        PromotionVoucherReservationExpired = _meter.CreateCounter<long>(eCommerceMetricNames.PromotionVoucherReservationExpiredTotal,
            unit: "{reservation}", description: "Số lượt giữ voucher hết TTL (PRO-001) — nhãn kind.");

        HttpRequestDurationSeconds = _meter.CreateHistogram<double>(eCommerceMetricNames.HttpRequestDurationSeconds,
            unit: "s", description: "Độ trễ xử lý request HTTP.");
        PromotionEvalDurationSeconds = _meter.CreateHistogram<double>(eCommerceMetricNames.PromotionEvalDurationSeconds,
            unit: "s", description: "Độ trễ một lượt đánh giá khuyến mãi (PRO-002) — nhãn kind.");

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

        _meter.CreateObservableGauge(eCommerceMetricNames.NotificationQueueBacklog,
            () => Interlocked.Read(ref _notificationQueueBacklog),
            unit: "{item}", description: "Số tin tồn đọng ở trạng thái ChoGui (NOT-001).");

        // 🆕 `REP-001` / `WP-26` — Gauge đa nhãn theo `projection` (tập hữu hạn: ban-hang/ton-kho).
        _meter.CreateObservableGauge(eCommerceMetricNames.ReportingWatermarkLagSeconds,
            () => _reportingWatermarkLag.Select(kv => new Measurement<long>(
                kv.Value,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Projection, kv.Key))),
            unit: "s", description: "Tuổi dữ liệu tổng hợp (now − LastRunAtUtc) theo projection (REP-001 NFR-003).");
    }
}
