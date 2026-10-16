using eCommerce.ChannelHub.Channels;
using eCommerce.Kernel.Events;

namespace eCommerce.ChannelHub.Events;

/// <summary>
/// Sự kiện: đã kéo được đơn từ sàn.
///
/// 🎯 Đây là điểm nối giữa ChannelHub và nghiệp vụ đơn hàng của app, và cũng là lý do
/// ChannelHub KHÔNG cần tham chiếu domain của app:
///   ChannelHub chỉ phát sự kiện → app subscribe và tạo Order của mình.
/// Nhờ vậy ChannelHub tách thành microservice được mà không phải sửa app.
///
/// ⚠️ Sự kiện này đi qua distributed event bus ⇒ handler phải IDEMPOTENT
/// (sàn gửi trùng là bình thường; xem <see cref="ChannelOrderPulledEto.ExternalOrderId"/>).
/// </summary>
[Serializable]
public class ChannelOrderPulledEto
{
    public Guid? TenantId { get; set; }
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public string ExternalOrderId { get; set; } = default!;
    public DateTime? PlacedAtUtc { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal ShippingFee { get; set; }
    public decimal ChannelDiscount { get; set; }
    public string? BuyerNote { get; set; }
    public List<ChannelOrderPulledLineEto> Lines { get; set; } = new();

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 `SAL-005` **§7.1** — **TRƯỜNG WATERMARK + TRUY VẾT** (bổ sung 16/10/2026).
    //
    // **VÌ SAO PHẢI CÓ (không phải "cho đủ bộ"):** đường pull hiện là **TẠO-MỘT-LẦN** — đơn đã nhận thì
    // **không sửa** ⇒ nếu sàn đổi địa chỉ/trạng thái, hệ thống giữ bản **CŨ** và **không có gì** cho biết
    // bản nào mới hơn. Bốn trường dưới đây là **điều kiện DUY NHẤT** để áp **luật watermark 4 nhánh**
    // (`SAL-005` §3.3, quyết định `D5`/`D6`). Không có chúng thì *"bản này có mới hơn bản kia không?"* —
    // câu hỏi **duy nhất** cần trả lời — **không trả lời được**.
    //
    // ⚠️ **BA TRƯỜNG BẮT BUỘC** (`AggregateVersion`, `PayloadHash`, `TenantId`): thiếu ⇒ consumer **NÉM**
    // `eCommerce:Sales:0001` + DLQ `PoisonMessage` (E1 của `SAL-005`). Lý do: một ETO **không** mang
    // watermark **không thể** được xử lý **đúng** — nhận nó vào là **ghi dữ liệu dựa trên suy đoán**.
    // 📌 Cố ý để kiểu **nullable** ở tầng ETO (để bắt được ca thiếu và **NÉM có mã**) thay vì để kiểu
    // không-nullable (sẽ thành `0`/`null` **im lặng** và handler không phân biệt được "thiếu" với "giá trị 0").
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⭐ **WATERMARK** — epoch **giây** UTC của `update_time` của sàn (`D5`).
    /// 🔒 Vì sao là **thời gian của SÀN** chứ không phải bộ đếm nội bộ: ta **không** kiểm soát bộ đếm của sàn,
    /// và *"tôi nhận được lúc nào"* **khác** *"sàn cập nhật lúc nào"* (webhook và pull là **hai** đường vào — `D2`).
    /// 📌 Đặt tên `AggregateVersion` để thoả `XC-002` BR-020, nhưng **ngữ nghĩa là watermark** (xem **F5** §15.2).
    /// </summary>
    public long? AggregateVersion { get; set; }

    /// <summary>Mốc cập nhật của sàn ở dạng người đọc hiểu được — **song song** với
    /// <see cref="AggregateVersion"/> (dùng cho log/UI; **không** dùng để so sánh).</summary>
    public DateTime? SourceUpdatedAtUtc { get; set; }

    /// <summary>
    /// **SHA-256 hex của canonical JSON** — phân biệt nhánh **TRÙNG** (`v` bằng + hash khớp) với nhánh
    /// **XUNG ĐỘT** (`v` bằng + hash khác).
    /// 🚨 **BẮT BUỘC tính qua `PayloadCanonicalizer`** (thứ tự khoá xác định, không khoảng trắng, UTC).
    /// Nếu tự băm chuỗi thô thì cùng một payload có thể cho **hash khác nhau ở hai instance** ⇒
    /// *"trùng"* bị nhận nhầm thành *"xung đột"* ⇒ **DLQ GIẢ**; và tệ hơn: hai instance **kết luận khác nhau**
    /// cho **cùng** dữ liệu (đúng loại "không tất định" mà `D6` cấm).
    /// </summary>
    public string? PayloadHash { get; set; }

    /// <summary>**`XC-005`/`D11`** — mã tương quan xuyên suốt ingestion (webhook ⇒ ETO ⇒ lịch sử trạng thái).
    /// 📌 Không có nó thì khi điều tra **không nối được** ba mảnh dấu vết lại với nhau.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// **`true` = đến từ WEBHOOK, `false` = đến từ PULL** (`SAL-005` §7.1).
    /// 🎯 Dùng để **đo hiệu quả** webhook (giảm độ trễ bao nhiêu) và **phát hiện dual-path**
    /// (`CHN-002` §6.5: cùng một đơn đến **cả hai** đường — nếu hai đường dùng **hai khoá chống trùng khác
    /// nhau** thì tạo **HAI đơn**). 📌 Không có cờ này thì ca dual-path **không thể** phân biệt với
    /// *"webhook đến hai lần"*.
    /// </summary>
    public bool IsFromWebhook { get; set; }

    /// <summary>Trạng thái **THÔ** của sàn (ví dụ `"CANCELLED"`, `"READY_TO_SHIP"`).
    /// 🔒 Giữ **nguyên văn** để điều tra; **ánh xạ** sang trạng thái nội bộ thuộc `SAL-003`
    /// (`BR-010` **cấm** định nghĩa lại bảng chuyển trạng thái ở `SAL-005`).</summary>
    public string? MarketplaceStatusRaw { get; set; }

    /// <summary>`Created`/`Updated`/`Cancelled` (**nếu** sàn cung cấp).
    /// ⚠️ Sàn **không** đảm bảo có trường này cho mọi loại thông báo ⇒ **nullable**, và **cấm** suy ra
    /// watermark từ nó.</summary>
    public string? EventKind { get; set; }
}

[Serializable]
public class ChannelOrderPulledLineEto
{
    public string? ExternalLineId { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}

/// <summary>
/// Sự kiện: token của shop đã được refresh (để audit + cảnh báo vận hành).
///
/// 📌 **ĐÂY LÀ SỰ KIỆN *THÔNG BÁO*** (gắn <see cref="INotificationEvent"/>) — trả lời câu hỏi
/// *"nếu sự kiện này biến mất, tôi có mất thông tin mà DB **không** có không?"* ⇒ **KHÔNG**:
/// token mới đã được ghi vào `ChnChannelShops` (`SetTokens` + `UpdateAsync`) **trước** khi publish.
/// Mất sự kiện chỉ khiến việc *thông báo* không xảy ra (ví dụ không bắn cảnh báo vận hành).
///
/// ⇒ Nhờ marker này, `EventCoverageChecker` xếp nó vào nhóm **Warning** thay vì **Error**
/// (trước 15/10/2026 nó bị báo **Error** *"DỮ LIỆU SẼ MẤT IM LẶNG"* ở **mọi lần boot** — một
/// **dương tính giả** đúng loại làm người đọc bỏ qua cảnh báo thật).
/// </summary>
[Serializable]
public class ChannelShopTokenRefreshedEto : INotificationEvent
{
    public Guid? TenantId { get; set; }
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
