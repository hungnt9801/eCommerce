using System.ComponentModel.DataAnnotations;
using eCommerce.Pricing.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Checkout;

/// <summary>Một dòng khách đặt: SKU + số lượng + thuế suất (do bên gọi cấp — Pricing **không** đọc module `Tax`).</summary>
public class DongCheckoutDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải ≥ 1.")]
    public int Quantity { get; set; } = 1;

    [Range(0, 1)]
    public decimal TaxRate { get; set; }

    /// <summary>Giá niêm yết (cấp 4) — Pricing **không** đọc Catalog nên bên gọi phải cấp (`PRC-001` Q1).</summary>
    [Range(0, double.MaxValue)]
    public decimal? ListPrice { get; set; }
}

/// <summary>Đầu vào **checkout một bước**: tính tiền **và** giữ chỗ, hoặc **không làm gì cả**.</summary>
public class CheckoutInput
{
    [Required]
    [StringLength(64)]
    public string ChannelCode { get; set; } = default!;

    public Guid? ChannelShopId { get; set; }

    /// <summary>
    /// 🚨 **KHÁCH MUA** — `null` = **khách vãng lai** (`CUS-001` `ADR-03`: vãng lai là **hạng nhất**, được mua).
    /// 🔒 **CỔNG NGHIỆP VỤ:** khách đang bị **chặn** (`Blocked`) ⇒ `TinhVaGiuChoAsync` **NÉM**
    /// `eCommerce:Customers:0005` **TRƯỚC** khi tính tiền và **trước** khi giữ chỗ (`CUS-001` §28).
    /// 📌 Vì sao phải kiểm **ở đây** (không chỉ ở lúc tạo đơn): bước này **giữ hàng trong kho** — nếu để
    /// khách bị chặn đi qua, đơn không được tạo nhưng **hàng vẫn bị giữ** (hết TTL mới nhả), tức
    /// *"cổng chặn đơn"* lại **làm hại kho**.
    /// </summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Kho để giữ chỗ. ⚠️ Hiện **một** kho cho cả đơn; phân bổ nhiều kho là việc của `INV-003`.</summary>
    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Giỏ phải có ít nhất một dòng.")]
    public List<DongCheckoutDto> Items { get; set; } = new();

    [Range(0, double.MaxValue)]
    public decimal OrderDiscount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OrderFee { get; set; }

    /// <summary>TTL giữ chỗ (phút). Để trống ⇒ **15 phút**.</summary>
    [Range(1, 1440)]
    public int? TtlPhut { get; set; }

    /// <summary>
    /// 🚨 **KHOÁ IDEMPOTENCY — BẮT BUỘC**. Gọi lại **cùng khoá** ⇒ trả **đúng kết quả đã có**
    /// (không tính lại, **không** giữ chỗ thêm). Đây là thứ chống *"khách bấm Đặt hàng hai lần"* và
    /// *"client retry sau timeout"* — **cả hai đều rất hay xảy ra** ở checkout.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string IdempotencyKey { get; set; } = default!;

    public DateTime? At { get; set; }
}

/// <summary>Kết quả checkout: **số tiền** (kèm giải trình) + **các lượt giữ chỗ** đã tạo.</summary>
public class KetQuaCheckoutDto
{
    /// <summary>Kết quả tính tiền đầy đủ (từng dòng + cấp thắng + thuế + tổng) — xem `PRC-002`.</summary>
    public GioTinhTienDto Tien { get; set; } = default!;

    /// <summary>Id các lượt giữ chỗ theo **thứ tự dòng** của `Items`.</summary>
    public List<Guid> ReservationIds { get; set; } = new();

    /// <summary>Hạn giữ chỗ chung (lấy theo lượt giữ chỗ **sớm hết hạn nhất**).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>`true` ⇒ đây là **kết quả trả lại** của một lần gọi trước (idempotent replay), **không** ghi gì mới.</summary>
    public bool LaKetQuaDaCo { get; set; }
}

/// <summary>
/// 🚨 **CHECKOUT MỘT BƯỚC** (`SAL-002`) — ghép **Pricing** (số tiền đúng) + **Inventory** (giữ chỗ chống bán vượt tồn)
/// thành **một** thao tác **nguyên tử**.
///
/// **BA BẤT BIẾN (mỗi cái đều có phép đo):**
/// 1. **FAIL-CLOSED:** nếu **bất kỳ** dòng nào không giữ được chỗ ⇒ **KHÔNG** có gì được ghi: không lượt giữ chỗ nào,
///    **và cả bản ghi sổ giải trình tính giá cũng bị rollback**. 📌 Đây là điều khác biệt giữa *"giữ được 2/3 món"*
///    (khách tưởng đã đặt, hàng thì không) và **từ chối sạch sẽ**.
/// 2. **IDEMPOTENT:** gọi lại cùng `IdempotencyKey` ⇒ trả **đúng** kết quả cũ, **không** sinh lượt giữ chỗ thứ hai.
/// 3. **KHÔNG tự bịa giá:** giá lấy từ pipeline (`PRC-001`/`PRC-002`); không có giá ⇒ `Pricing:0004`, **không** bán giá 0.
/// </summary>
public interface ICheckoutAppService : IApplicationService
{
    Task<KetQuaCheckoutDto> TinhVaGiuChoAsync(CheckoutInput input);

    /// <summary>Tra kết quả checkout theo `IdempotencyKey` (**không** ghi gì) — dùng khi client mất kết nối.</summary>
    Task<KetQuaCheckoutDto> GetTheoKhoaAsync(string idempotencyKey);
}
