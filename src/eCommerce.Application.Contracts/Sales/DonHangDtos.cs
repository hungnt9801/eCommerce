using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Sales;

/// <summary>
/// 🚨 **TẠO ĐƠN TỪ MỘT LẦN CHECKOUT ĐÃ HOÀN TẤT** (`SAL-001`).
///
/// **BẤT BIẾN QUAN TRỌNG NHẤT:** số tiền trên đơn lấy **NGUYÊN VĂN** từ **sổ giải trình** của `SAL-002`
/// (`PrcPriceCalculationRecords`) — **KHÔNG tính lại**.
/// 📌 **Vì sao KHÔNG tính lại:** nếu tính lại, giữa hai thời điểm **giá có thể đã đổi** (bảng giá mới được kích hoạt,
/// khuyến mãi hết hạn) ⇒ **khách đã nhìn thấy một số, đơn ghi một số khác** ⇒ tranh chấp không có cách giải quyết,
/// vì cả hai bên đều "đúng" theo dữ liệu của mình. Sổ giải trình là **bằng chứng** của con số đã báo khách.
/// </summary>
public class TaoDonTuCheckoutInput
{
    /// <summary>Khoá idempotency của lần checkout (`SAL-002`) — **khoá nối** giữa thanh toán-giỏ và đơn hàng.</summary>
    [Required]
    [StringLength(128)]
    public string CheckoutIdempotencyKey { get; set; } = default!;

    /// <summary>Số đơn hiển thị cho khách (duy nhất theo tenant).</summary>
    [Required]
    [StringLength(64)]
    public string Number { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string ChannelCode { get; set; } = default!;

    public Guid? ChannelShopId { get; set; }

    public Guid? CustomerId { get; set; }

    [StringLength(2048)]
    public string? Note { get; set; }
}

/// <summary>Một dòng khách trả: SKU + số lượng (phải ≥ 1 và **không vượt** số còn lại của dòng đơn).</summary>
public class TraHangLineDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng trả phải ≥ 1.")]
    public int Quantity { get; set; }
}

public class DonHangDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public string ChannelCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int LineCount { get; set; }

    /// <summary>Tổng **từ sổ giải trình** tại thời điểm tạo đơn — để **đối chiếu** với `GrandTotal`.</summary>
    public decimal TotalTuSoGiaiTrinh { get; set; }

    /// <summary>Khoá checkout đã sinh ra đơn này (truy vết ngược về bằng chứng giá).</summary>
    public string CheckoutIdempotencyKey { get; set; } = default!;
}

/// <summary>
/// **API ĐƠN HÀNG** (`SAL-001`) — hiện có **tạo đơn từ checkout** (và trả về số tiền **đã đối chiếu** với sổ giải trình).
/// </summary>
public interface IDonHangAppService : IApplicationService
{
    Task<DonHangDto> TaoTuCheckoutAsync(TaoDonTuCheckoutInput input);

    /// <summary>
    /// 🚨 **XÁC NHẬN ĐƠN** ⇒ chốt giữ chỗ ⇒ **xuất kho**. **NÉM** nếu đơn không ở `Pending`,
    /// hoặc lượt giữ chỗ **đã quá hạn** (`0031` — *hàng đã được trả về cho người khác*).
    /// </summary>
    Task<DonHangDto> XacNhanAsync(Guid id, DateTime? at = null);

    /// <summary>
    /// 🚨 **HUỶ ĐƠN** ⇒ **nhả chỗ NGAY** (không chờ job hết TTL) ⇒ hàng trở lại kho để người khác mua.
    /// **NÉM** nếu đơn **đã xác nhận** (đã xuất kho) — khi đó phải **trả hàng**, không phải huỷ.
    /// </summary>
    Task<DonHangDto> HuyDonAsync(Guid id, DateTime? at = null);

    /// <summary>
    /// 🚨 **TRẢ HÀNG** cho đơn **đã xác nhận** ⇒ sinh **phiếu NHẬP kho** (hàng về kho thật) ⇒ `Returned`.
    /// 📌 **VÌ SAO KHÔNG xoá/đảo bút toán cũ:** sổ cái là **append-only** (`BR-004` tinh thần) ⇒ muốn "trả lại hàng"
    /// thì phải **ghi một bút toán mới** (`Receipt`/`CustomerReturn`), **không** sửa quá khứ.
    /// 📌 `WarehouseId` phải **do bên gọi cấp**: `Order` **không** lưu kho (một đơn có thể giao từ nhiều kho —
    /// `INV-003`), nên không thể suy ra. **NÉM** nếu đơn không ở trạng thái cho phép trả hàng.
    /// </summary>
    /// <summary>
    /// **Trả hàng MỘT PHẦN**: `lines` **để trống** ⇒ trả **toàn bộ** đơn còn lại; có `lines` ⇒ chỉ trả các dòng đó.
    /// 🚨 **NÉM** nếu **luỹ kế** số đã trả (các lần trước **cộng lại**) **vượt** số đã mua — *trả hai lần cho cùng
    /// một món* là lỗi **không sửa được** vì sổ cái **append-only** (phải tạo chứng từ đảo).
    /// </summary>
    Task<DonHangDto> TraHangAsync(Guid id, Guid warehouseId, List<TraHangLineDto>? lines = null, DateTime? at = null);

    Task<DonHangDto> GetAsync(Guid id);
}
