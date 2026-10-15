using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// **Nhật ký nhận đơn từ sàn** (`SAL-005` D7) — bản ghi cho MỘT đơn của sàn mà ta đã nhận được.
///
/// 🚨 VÌ SAO BẢNG NÀY TỒN TẠI (đây là điều kiện để **lỗi #22 không quay lại**):
/// `ChannelShopSyncer` chỉ tiến **con trỏ đồng bộ** khi biết chắc có **nơi nhận** đơn. Nơi nhận đó phải
/// **ghi được đơn xuống đĩa** trước khi trả về thành công — nếu chỉ "nhận rồi xử lý trong bộ nhớ" thì
/// mất điện/exception giữa chừng là **mất đơn im lặng**, đúng loại lỗi #22.
///
/// 📌 VAI TRÒ:
///   • **chống trùng ở mức ĐƠN** (unique index `(TenantId, ChannelCode, ExternalOrderId)` — `NULLS NOT DISTINCT`),
///   • **giữ vết** đơn đã nhận kể cả khi việc tạo `Order` thất bại ⇒ luôn truy được "đã nhận gì, lúc nào",
///   • nơi neo **watermark của sàn** về sau (`SAL-005` D5).
///
/// ⚠️ **CHƯA làm ở lượt này (ghi rõ, không giả vờ):** xử lý **cập nhật** đơn (so watermark theo `update_time`);
/// hiện theo hướng **tạo-một-lần** — đơn đã có thì **không** sửa nữa. Xem ghi chú ở
/// <see cref="MarketplaceOrderIntakeHandler"/> và `docs/features/sal-005-webhook-don-tu-san.md` §16.
/// </summary>
public class MarketplaceOrderIntake : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã kênh: "shopee", "lazada"… (trùng <c>Channel.Code</c>).</summary>
    public string ChannelCode { get; protected set; } = default!;

    public string ExternalShopId { get; protected set; } = default!;

    /// <summary>ID đơn do sàn cấp (`order_sn`) — **khoá đối chiếu quan trọng nhất**.</summary>
    public string ExternalOrderId { get; protected set; } = default!;

    /// <summary>Đơn nội bộ đã tạo từ bản ghi này (<c>null</c> nếu chưa tạo được).</summary>
    public Guid? OrderId { get; protected set; }

    /// <summary>
    /// Mốc **đặt hàng trên sàn**. 📌 Giữ ở đây vì `Order` **không** có trường này
    /// (`SAL-005` **F8**) — mất nó thì báo cáo theo kênh và đối soát với sàn **thiếu mốc nghiệp vụ**.
    /// </summary>
    public DateTime? MarketplacePlacedAtUtc { get; protected set; }

    /// <summary>Băm nội dung payload đã nhận (canonical JSON) — phát hiện **cùng đơn nhưng nội dung khác**.</summary>
    public string PayloadHash { get; protected set; } = default!;

    public DateTime ReceivedAtUtc { get; protected set; }

    /// <summary>`CorrelationId` của lần nhận (`SAL-005` D11) — nối log ↔ ETO ↔ đơn.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Lý do bản ghi **không** dẫn tới đơn (nếu có) — để không bao giờ "bỏ qua im lặng".</summary>
    public string? SkipReason { get; protected set; }

    protected MarketplaceOrderIntake() { }

    public MarketplaceOrderIntake(
        Guid id,
        string channelCode,
        string externalShopId,
        string externalOrderId,
        string payloadHash,
        DateTime receivedAtUtc,
        DateTime? marketplacePlacedAtUtc,
        Guid? tenantId = null)
        : base(id)
    {
        ChannelCode = channelCode;
        ExternalShopId = externalShopId;
        ExternalOrderId = externalOrderId;
        PayloadHash = payloadHash;
        ReceivedAtUtc = receivedAtUtc;
        MarketplacePlacedAtUtc = marketplacePlacedAtUtc;
        TenantId = tenantId;
    }

    /// <summary>Gắn đơn nội bộ vừa tạo vào bản ghi nhận.</summary>
    public void GanOrder(Guid orderId)
    {
        OrderId = orderId;
        SkipReason = null;
    }

    /// <summary>
    /// Đánh dấu **cố ý không tạo đơn** — 🚨 **bắt buộc kèm lý do**: nhánh bỏ qua mà không có lý do là
    /// đúng dạng "im lặng" mà dự án đã trả giá nhiều lần.
    /// </summary>
    public void DanhDauBoQua(string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new ArgumentException("Lý do bỏ qua không được rỗng (cấm bỏ qua im lặng).", nameof(lyDo));
        }

        SkipReason = lyDo.Length > 500 ? lyDo[..500] : lyDo;
    }
}
