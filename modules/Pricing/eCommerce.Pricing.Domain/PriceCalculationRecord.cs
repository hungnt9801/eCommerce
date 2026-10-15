using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **SỔ GIẢI TRÌNH TÍNH GIÁ** (`PrcPriceCalculationRecords`) — `PRC-002` **ADR-13**.
///
/// **VÌ SAO PHẢI CÓ (không phải "nice to have"):** khi khách/kế toán hỏi *"vì sao đơn này giá này?"*, hệ thống
/// **phải trả lời được bằng DỮ LIỆU**, không phải bằng log (log bị xoay vòng, không tra theo đơn được).
/// Đây cũng là thứ **đối soát** và **khiếu nại** cần. Không có nó thì mọi tranh chấp về giá đều **không có bằng chứng**.
///
/// 🔒 **APPEND-ONLY — KHÔNG SỬA, KHÔNG XOÁ.** Bản ghi là **bằng chứng**; sửa được thì **hết giá trị làm bằng chứng**
/// (và `IsDeleted`/audit ở lớp cha vẫn cho phép xoá mềm ⇒ quy ước: **cấm** gọi `DeleteAsync` trên entity này).
///
/// **Idempotency (ADR-13):** `IdempotencyKey` **bắt buộc** + **unique `(TenantId, IdempotencyKey)`**.
/// Gọi lại **cùng khoá** ⇒ trả **đúng kết quả đã lưu**, **không** tính lại. Không có khoá ⇒ hai lần gọi cho cùng
/// một đơn có thể ra hai kết quả khác nhau (nếu bảng giá/khuyến mãi đổi ở giữa) ⇒ **đơn không giải trình được**.
/// </summary>
public class PriceCalculationRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Khoá idempotency do **bên gọi** cấp (ví dụ `checkout:{orderId}`) — duy nhất trong một tenant.</summary>
    public string IdempotencyKey { get; protected set; } = default!;

    public string ChannelCode { get; protected set; } = default!;

    public Guid? ChannelShopId { get; protected set; }

    /// <summary>Thời điểm **tính giá** (do bên gọi truyền) — KHÁC `CreationTime` (thời điểm ghi sổ).</summary>
    public DateTime CalculatedAt { get; protected set; }

    /// <summary>Mã chính sách làm tròn đã dùng (ADR-08) — để tái lập được kết quả.</summary>
    public string RoundingPolicyCode { get; protected set; } = default!;

    /// <summary>Mã ISO loại tiền — cột **bắt buộc** đi kèm mọi cột tiền (`PRC-001` BR-001).</summary>
    public string CurrencyCode { get; protected set; } = "VND";

    public decimal Subtotal { get; protected set; }
    public decimal TotalDiscount { get; protected set; }
    public decimal TotalFee { get; protected set; }
    public decimal TotalTax { get; protected set; }

    /// <summary>Tổng thanh toán — **Σ dòng đã làm tròn** (ADR-06).</summary>
    public decimal Total { get; protected set; }

    /// <summary>Payload **đầy đủ** (`JSON`) — chi tiết từng dòng + ứng viên + cấp thắng ⇒ giải trình được từng đồng.</summary>
    public string Payload { get; protected set; } = default!;

    protected PriceCalculationRecord() { }

    public PriceCalculationRecord(
        Guid id, string idempotencyKey, string channelCode, Guid? channelShopId, DateTime calculatedAt,
        string roundingPolicyCode, string currencyCode,
        decimal subtotal, decimal totalDiscount, decimal totalFee, decimal totalTax, decimal total,
        string payload) : base(id)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("`IdempotencyKey` là BẮT BUỘC (PRC-002 ADR-13).", nameof(idempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("`Payload` rỗng ⇒ bản ghi KHÔNG giải trình được gì.", nameof(payload));
        }

        // 🚨 **PHẢI GÁN** mọi trường — bài học `PRC-001` §17.2 **L2** (constructor chỉ kiểm tra mà quên gán
        // ⇒ ghi ra rỗng/Guid.Empty ⇒ mất bằng chứng).
        IdempotencyKey = idempotencyKey.Trim();
        ChannelCode = channelCode.Trim();
        ChannelShopId = channelShopId;
        CalculatedAt = calculatedAt;
        RoundingPolicyCode = roundingPolicyCode;
        CurrencyCode = currencyCode;
        Subtotal = subtotal;
        TotalDiscount = totalDiscount;
        TotalFee = totalFee;
        TotalTax = totalTax;
        Total = total;
        Payload = payload;
    }
}
