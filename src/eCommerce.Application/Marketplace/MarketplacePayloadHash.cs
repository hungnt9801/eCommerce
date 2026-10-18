using eCommerce.ChannelHub.Events;
using eCommerce.Messaging;

namespace eCommerce.Marketplace;

/// <summary>
/// 🚨 **HASH "NỘI DUNG NGHIỆP VỤ" CỦA MỘT SỰ KIỆN ĐƠN SÀN** — `SAL-005` **BR-SAL-005-004** / §17.3 **bẫy #1**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO PHẢI CÓ LỚP NÀY (đây là lỗi THẬT, đo được ở `S6`):**
///
/// Handler nhận đơn từng băm **`JsonSerializer.Serialize(eventData)`** — tức băm **cả ETO**, gồm cả các
/// trường **NGỮ CẢNH VẬN CHUYỂN**:
/// <list type="bullet">
///   <item>`CorrelationId` — **một giá trị mới cho MỖI LẦN KÉO** (`ChannelShopSyncer`),</item>
///   <item>`TenantId`, `ChannelShopId` — ngữ cảnh shop/tenant, không phải nội dung đơn,</item>
///   <item>`IsFromWebhook` — đường vào (pull hay webhook),</item>
///   <item>`PayloadHash` — chính là hash (băm cả hash của chính nó là vòng tự tham chiếu).</item>
/// </list>
///
/// ⇒ **Hệ quả đo được:** cùng **một** đơn, cùng **một** nội dung, nhưng lần kéo sau có `CorrelationId` khác
/// ⇒ hash khác ⇒ tầng chống trùng kết luận **XUNG ĐỘT PAYLOAD** ⇒ **DLQ GIẢ** + `SkipReason` bị ghi đè
/// ở **mọi** lần kéo lại. 📌 Đúng thứ `PayloadCanonicalizer` đã cảnh báo: *"sau vài lần DLQ giả, người trực
/// bắt đầu bỏ qua cảnh báo — và đó là lúc xung đột THẬT bị bỏ qua"*.
///
/// 🔒 **LUẬT:** hash chỉ tính trên **NỘI DUNG NGHIỆP VỤ của đơn** — đúng tập trường mà connector trả về
/// (`ChannelOrderModel`), để **cùng một đơn ⇒ cùng một hash** bất kể nó đến bằng đường nào, lần thứ mấy.
/// Đây cũng chính là tập trường `ChannelShopSyncer` băm ở phía phát (cùng hàm `PayloadCanonicalizer`) ⇒
/// hai đầu nói **cùng một ngôn ngữ**; `MarketplacePayloadHashTests` khoá sự tương đương đó.
///
/// ⚠️ **KHÔNG** thêm vào đây: `TenantId`, `ChannelShopId`, `CorrelationId`, `IsFromWebhook`, `PayloadHash`.
/// 📌 Thêm lại một trong số đó là **tái sinh đúng lỗi này** — và nó **không** làm đỏ test nào khác.
/// </summary>
public static class MarketplacePayloadHash
{
    /// <summary>
    /// **Hình dạng nội dung nghiệp vụ** của sự kiện — trùng **từng trường** với `ChannelOrderModel` mà
    /// connector trả về (kể cả **tên** trường, vì canonical JSON dùng tên làm khoá).
    /// </summary>
    public static object NoiDung(ChannelOrderPulledEto suKien)
    {
        ArgumentNullException.ThrowIfNull(suKien);

        return new
        {
            suKien.ExternalOrderId,
            suKien.PlacedAtUtc,
            suKien.BuyerNote,
            suKien.Currency,
            suKien.ShippingFee,
            suKien.ChannelDiscount,
            Lines = (suKien.Lines ?? []).Select(l => new
            {
                l.ExternalLineId,
                l.Sku,
                l.Quantity,
                l.UnitPrice,
                l.DiscountAmount
            }).ToList(),

            // 📌 Tên phải là `UpdatedAtUtc`/`StatusRaw` — **tên của `ChannelOrderModel`** (nguồn của hash ở
            // phía phát). Đổi tên ở đây là **đổi hash**, tức là âm thầm tạo "xung đột" cho dữ liệu cũ.
            UpdatedAtUtc = suKien.SourceUpdatedAtUtc,
            StatusRaw = suKien.MarketplaceStatusRaw,
            suKien.EventKind
        };
    }

    /// <summary>
    /// Chuỗi **canonical JSON** của nội dung nghiệp vụ — dùng làm `payloadJson` cho tầng chống trùng
    /// (`IIdempotencyGuard`), nhờ vậy khoá idempotency so **nội dung**, không so ngữ cảnh vận chuyển.
    /// </summary>
    public static string ChuoiChuanHoa(ChannelOrderPulledEto suKien) => PayloadCanonicalizer.ChuanHoa(NoiDung(suKien));

    /// <summary>SHA-256 hex (64 ký tự, chữ thường) của <see cref="ChuoiChuanHoa"/>.</summary>
    public static string Tinh(ChannelOrderPulledEto suKien) => PayloadCanonicalizer.Bam(NoiDung(suKien));
}
