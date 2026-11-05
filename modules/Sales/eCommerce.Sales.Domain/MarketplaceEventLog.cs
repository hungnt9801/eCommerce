using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// **KẾT CỤC của một sự kiện đơn sàn khi nó tới Sales** (`SAL-005` §3.2/§3.3).
///
/// 🔑 Mỗi sự kiện **NHẬN ĐƯỢC** phải có **đúng một** giá trị trong danh sách này — kể cả sự kiện
/// **bị bỏ qua**. 📌 Không có nó thì nhánh *"bỏ qua"* (`Late`/`Duplicate`) **không để lại dấu vết**,
/// và câu hỏi vận hành *"sàn có gửi sự kiện này không, gửi lúc nào, hệ thống đã làm gì?"* **không trả lời được**.
/// </summary>
public enum MarketplaceIntakeOutcome
{
    /// <summary>✅ **ÁP DỤNG** — watermark mới hơn bản đã áp (hoặc là sự kiện đầu tiên của đơn).</summary>
    Applied = 0,

    /// <summary>🔁 **TRÙNG** — cùng phiên bản **và** cùng `PayloadHash` (sàn gửi lại / worker pull lại).
    /// 📌 Đây là ca **BÌNH THƯỜNG NHẤT**, **không** phải lỗi ⇒ `Information`, không DLQ.</summary>
    Duplicate = 1,

    /// <summary>⏳ **ĐẾN MUỘN** — phiên bản **cũ hơn** bản đã áp ⇒ **KHÔNG ghi đè** (bất biến watermark).
    /// 📌 Cũng là ca bình thường (webhook đến sau nhịp pull) ⇒ không ném lỗi; DLQ chỉ để **có bằng chứng**.</summary>
    Late = 2,

    /// <summary>🚨 **XUNG ĐỘT CÙNG MỐC** — cùng phiên bản nhưng `PayloadHash` **khác** ⇒ **KHÔNG đoán**
    /// bản nào đúng (`D6`) ⇒ ghi vết + DLQ chờ người xử lý.</summary>
    Conflict = 3,

    /// <summary>🚩 **NGHI NGỜ THIẾU BẢN TRUNG GIAN** — **vẫn áp dụng** bản mới nhưng nhảy mốc vượt ngưỡng
    /// (`BR-009`) ⇒ đánh cờ `GapSuspected` + ghi vết để job `D12` đối soát.
    /// 📌 Cố ý **tách** khỏi <see cref="Applied"/>: nếu gộp thì *"có bao nhiêu lần nghi mất bản"*
    /// chỉ còn đọc được bằng cách **quét cột cờ** — và cột cờ bị Ops hạ sau khi đối soát ⇒ **con số biến mất**.</summary>
    GapSuspected = 4,

    /// <summary>💥 **THẤT BẠI** — xử lý sự kiện **ném lỗi** (hạ tầng, dữ liệu sai). `FailureCode`/
    /// `FailureMessage` cho biết **vì sao**. 🔒 Sự kiện hỏng **vẫn phải có dòng** — nếu không,
    /// *"không có dòng nào"* và *"chưa từng nhận"* **trông giống hệt nhau**.</summary>
    Failed = 5,

    /// <summary>🚫 **THIẾU WATERMARK** — sự kiện **không** mang `AggregateVersion` (connector chưa trả
    /// `update_time`) ⇒ **không thể** xét thứ tự ⇒ giữ bản cũ + ghi vết.
    ///
    /// 📌 **Vì sao cần giá trị RIÊNG (thay vì dùng `Late`):** `Late` nghĩa là *"đã xét và bản này cũ hơn"* —
    /// một kết luận **dựa trên dữ liệu**. Ở ca này ta **không có** dữ liệu để kết luận. 🔑 Gộp hai thứ đó
    /// lại là đúng loại "hai nguyên nhân khác nhau, một mã" mà dự án đã trả giá (`kiem-moc-build.ps1`).
    /// ⚠️ Đây là ca **nợ kỹ thuật của CONNECTOR** (`SAL-005` §17.6 #7) — đo được ngay ở cột `Outcome`.</summary>
    ThieuWatermark = 6
}

/// <summary>
/// 🚨 **NHẬT KÝ SỰ KIỆN ĐƠN SÀN — APPEND-ONLY** (`SAL-005` **D7**, bảng `SalMarketplaceEventLogs`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO BẢNG NÀY TỒN TẠI — và vì sao `SalMarketplaceOrderIntakes` KHÔNG thay thế được nó:**
///
/// `MarketplaceOrderIntake` giữ **MỘT dòng cho MỘT đơn sàn** — nó trả lời *"đơn này đang ở đâu"*.
/// Bảng này giữ **MỘT dòng cho MỘT SỰ KIỆN NHẬN ĐƯỢC** — nó trả lời câu hỏi **khác**:
/// *"sàn đã gửi những gì, lúc nào, và mỗi lần ta đã làm gì?"*.
///
/// 📌 Khác biệt đó **không phải** chi tiết: một đơn có thể có **20 sự kiện**, trong đó **3 trùng**,
/// **2 đến muộn**, **1 xung đột**. Nhật ký theo đơn **chỉ** nhớ được *"lần nhận gần nhất"* (và có thể bị
/// ghi đè) ⇒ **mọi nhánh bỏ qua đều biến mất**. 🔒 Đúng loại lỗi im lặng mà `XC-002` D4/D5 cảnh báo:
/// *"lỗi luôn để lại dấu vết"* — mà *"bỏ qua vì trùng"* **cũng** là một kết cục cần dấu vết.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔒 **BA BẤT BIẾN:**
/// <list type="number">
///   <item>**APPEND-ONLY** — entity **không có** phương thức sửa/xoá; không ai được `Update`/`Delete`
///     trên bảng này (`TC-20` kiểm bằng reflection). 📌 Một nhật ký sửa được **không phải** nhật ký;</item>
///   <item>**ĐÚNG MỘT dòng cho MỘT sự kiện** — khoá tự nhiên
///     `(TenantId, ChannelCode, ExternalOrderId, SourceVersion, PayloadHash)` là **hàng rào cuối**:
///     hai luồng song song cùng ghi một sự kiện ⇒ DB chặn (đúng tinh thần `XC-003` D2:
///     *"bỏ lock đi, DB có chặn không?"* ⇒ **CÓ**);</item>
///   <item>**KHÔNG FK** tới `OrdOrders`/`SalMarketplaceOrderIntakes` — nhật ký phải **sống được** kể cả khi
///     đơn/intake chưa tồn tại (ví dụ sự kiện `Failed` đầu tiên, hoặc đơn bị xoá). 📌 FK ở đây sẽ khiến
///     **không ghi được dấu vết đúng lúc cần nhất**.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (nói thẳng):** append-only ở **tầng ứng dụng** KHÔNG chặn `UPDATE`/`DELETE`
/// bằng SQL tay. Muốn chặn ở DB thì cần `REVOKE UPDATE, DELETE` — câu hỏi mở **`Q5`** của `SAL-005`.
/// </summary>
public class MarketplaceEventLog : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    // 📌 Hằng số độ dài để **entity + cấu hình EF + test** dùng CÙNG một nguồn (quy ước dự án).
    public const int DoDaiChannelCode = 32;
    public const int DoDaiExternalOrderId = 64;
    public const int DoDaiPayloadHash = 64;
    public const int DoDaiEventKind = 24;
    public const int DoDaiTrangThaiTho = 64;
    public const int DoDaiFailureCode = 64;
    public const int DoDaiFailureMessage = 2000;
    public const int DoDaiCorrelationId = 64;

    public Guid? TenantId { get; protected set; }

    /// <summary>Kênh bán (`shopee`, `lazada`…).</summary>
    public string ChannelCode { get; protected set; } = default!;

    /// <summary>Shop trên kênh — **tham chiếu MỀM** (cấm FK cứng qua ranh giới module, `SAL-005` D1).</summary>
    public Guid? ChannelShopId { get; protected set; }

    /// <summary>Mã đơn của sàn (`order_sn`).</summary>
    public string ExternalOrderId { get; protected set; } = default!;

    /// <summary>
    /// ⭐ Phiên bản/watermark **của chính sự kiện này** = epoch **giây** UTC của `update_time` sàn.
    /// ⚠️ **Nullable** — sự kiện có thể **không** mang watermark (connector chưa trả `update_time`);
    /// khi đó `Outcome = ThieuWatermark` và `SourceVersion IS NULL`. 📌 Cố ý **không** thay bằng `0`:
    /// `0` là một mốc **thật** (1970-01-01) ⇒ không phân biệt được *"thiếu"* với *"mốc 0"*.
    /// </summary>
    public long? SourceVersion { get; protected set; }

    /// <summary>SHA-256 hex (64 ký tự, chữ thường) của payload sự kiện — nằm trong **khoá unique**.</summary>
    public string PayloadHash { get; protected set; } = default!;

    /// <summary>Kết cục xử lý — xem <see cref="MarketplaceIntakeOutcome"/>.</summary>
    public MarketplaceIntakeOutcome Outcome { get; protected set; }

    /// <summary>`Created`/`Updated`/`Cancelled` **nếu** sàn cung cấp (`null` = sàn không phân loại).</summary>
    public string? EventKind { get; protected set; }

    /// <summary>Trạng thái **THÔ** của sàn (`order_status`) — giữ **nguyên văn** để điều tra.
    /// 🔒 Ánh xạ sang trạng thái nội bộ thuộc `SAL-003`; **cấm** diễn giải ở tầng này.</summary>
    public string? MarketplaceStatusRaw { get; protected set; }

    /// <summary>`true` = đường **webhook**, `false` = đường **pull** (đo hiệu quả + phát hiện dual-path).</summary>
    public bool IsFromWebhook { get; protected set; }

    /// <summary>Mốc cập nhật của sàn ở dạng người đọc hiểu được (song song `SourceVersion`).</summary>
    public DateTime? SourceUpdatedAtUtc { get; protected set; }

    /// <summary>Thời điểm **hệ thống nhận** — 🔒 **KHÔNG** dùng làm nguồn sự thật về thứ tự.</summary>
    public DateTime ReceivedAtUtc { get; protected set; }

    /// <summary>Mã lỗi nghiệp vụ (`eCommerce:…`) khi `Outcome = Failed`.</summary>
    public string? FailureCode { get; protected set; }

    /// <summary>Thông điệp lỗi **đã cắt ngắn** (≤ 2000) — 🔒 không chứa PII/không chứa payload đầy đủ.</summary>
    public string? FailureMessage { get; protected set; }

    /// <summary>`CorrelationId` để nối log ↔ ETO ↔ lịch sử trạng thái (`D11`, `XC-005`).</summary>
    public string? CorrelationId { get; protected set; }

    protected MarketplaceEventLog() { }

    public MarketplaceEventLog(
        Guid id,
        string channelCode,
        string externalOrderId,
        string payloadHash,
        MarketplaceIntakeOutcome outcome,
        DateTime receivedAtUtc,
        long? sourceVersion = null,
        DateTime? sourceUpdatedAtUtc = null,
        Guid? channelShopId = null,
        Guid? tenantId = null,
        string? eventKind = null,
        string? marketplaceStatusRaw = null,
        bool isFromWebhook = false,
        string? correlationId = null,
        string? failureCode = null,
        string? failureMessage = null)
        : base(id)
    {
        // 🔒 Chặn dữ liệu vô nghĩa NGAY Ở TẦNG ENTITY (không đợi DB): một dòng không có kênh hoặc không có
        // mã đơn sàn thì **không tra cứu được** ⇒ nhật ký mất giá trị đúng lúc cần đọc nó.
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw new ArgumentException("Nhật ký sự kiện phải có mã kênh.", nameof(channelCode));
        }

        if (string.IsNullOrWhiteSpace(externalOrderId))
        {
            throw new ArgumentException("Nhật ký sự kiện phải có mã đơn sàn.", nameof(externalOrderId));
        }

        if (string.IsNullOrWhiteSpace(payloadHash))
        {
            throw new ArgumentException("Nhật ký sự kiện phải có PayloadHash.", nameof(payloadHash));
        }

        // 📌 `SourceVersion` là epoch giây ⇒ **luôn dương**. Giá trị ≤ 0 nghĩa là map sai (ví dụ map số dòng
        // hoặc `PlacedAtUtc` vào watermark) — bắt ngay ở đây thay vì để CHECK của DB ném ở tầng khó đọc.
        if (sourceVersion is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVersion),
                sourceVersion,
                "`SourceVersion` là epoch giây của `update_time` sàn ⇒ phải > 0 (≤ 0 là dấu hiệu map SAI).");
        }

        ChannelCode = channelCode.Trim();
        ExternalOrderId = externalOrderId.Trim();
        PayloadHash = payloadHash.Trim();
        Outcome = outcome;
        ReceivedAtUtc = receivedAtUtc;
        SourceVersion = sourceVersion;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        ChannelShopId = channelShopId;
        TenantId = tenantId;
        EventKind = Cat(eventKind, DoDaiEventKind);
        MarketplaceStatusRaw = Cat(marketplaceStatusRaw, DoDaiTrangThaiTho);
        IsFromWebhook = isFromWebhook;
        CorrelationId = Cat(correlationId, DoDaiCorrelationId);
        FailureCode = Cat(failureCode, DoDaiFailureCode);

        // 🔒 Thông điệp lỗi bị **CẮT NGẮN** có chủ ý (`XC-002` BR-021): đây là chỗ PII và payload dễ lọt vào
        // nhất (exception message của serializer có thể chứa cả object).
        FailureMessage = Cat(failureMessage, DoDaiFailureMessage);
    }

    private static string? Cat(string? giaTri, int toiDa)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
        {
            return null;
        }

        var sach = giaTri.Trim();
        return sach.Length > toiDa ? sach[..toiDa] : sach;
    }
}
