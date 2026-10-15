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

/// <summary>
/// **Đầu vào trả hàng** (`SAL-004`) — gói **TƯỜNG MINH** thay vì nhiều tham số rời.
///
/// 🚨 **VÌ SAO ĐỔI SANG DTO (bài học vòng 40–41):** chữ ký cũ
/// `TraHangAsync(Guid id, Guid warehouseId, List&lt;TraHangLineDto&gt;? lines, DateTime? at)` khiến `lines` — một
/// **tham số phức tạp duy nhất** — bị ASP.NET Core bind **cả body** theo quy tắc **ngầm**; client gửi
/// `{ "lines": [ … ] }` nhận **`400`** mà **không rõ vì sao** (đã đo: `inv-001` §32.3).
/// Gói vào **một DTO** thì hình dạng body **hiển nhiên** — *hợp đồng API rõ hơn là phụ thuộc quy tắc bind ngầm*.
/// </summary>
public class TraHangInput
{
    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>
    /// **Để trống** ⇒ trả **phần CÒN LẠI** của đơn; **có giá trị** ⇒ chỉ trả các dòng đó.
    /// 🚨 Luôn bị kiểm **LUỸ KẾ**: `đã trả (các lần trước) + lần này ≤ đã mua`.
    /// </summary>
    public List<TraHangLineDto>? Lines { get; set; }

    public DateTime? At { get; set; }
}

/// <summary>
/// 🚨 **PHÂN BỔ TỒN THEO KÊNH** (`INV-003` — **bước 1: NHÌN THẤY**, chưa áp hạn mức).
/// Một dòng cho mỗi **cặp (kênh, SKU)**: *kênh này đã bán bao nhiêu, đang giữ bao nhiêu, đã trả bao nhiêu*.
///
/// 📌 **VÌ SAO PHẢI "NHÌN THẤY" TRƯỚC KHI "ÁP HẠN MỨC":** không đo được hiện trạng thì **không thể** chọn hạn mức
/// (bao nhiêu là hợp lý?) và **không thể** kiểm chứng rằng hạn mức có tác dụng. Đặt hạn mức mù ⇒ hoặc **chặn nhầm**
/// đơn hợp lệ (mất doanh thu) hoặc **không chặn gì** (hạn mức vô nghĩa).
/// 📌 **VÌ SAO SUY RA TỪ ĐƠN, KHÔNG THÊM CỘT:** `InvStockReservations` **không có** `ChannelCode`; nhưng mỗi lượt
/// giữ chỗ trỏ tới nguồn `Checkout:{khoá}` và **đơn giữ chính khoá đó** (`CheckoutIdempotencyKey`) + `ChannelCode`
/// ⇒ **suy ra được** mà **không cần migration**. Thêm cột sẽ là lựa chọn **khác** (cần người quyết) và chỉ nên làm
/// khi cần **hạn mức cứng**.
/// </summary>
public class PhanBoKenhDto
{
    public string ChannelCode { get; set; } = default!;

    public string Sku { get; set; } = default!;

    /// <summary>Số lượng **đã bán** (đơn **không** ở `Pending`/`Cancelled`).</summary>
    public int DaBan { get; set; }

    /// <summary>Số lượng **đang giữ chỗ** (`Held`) của các đơn thuộc kênh này.</summary>
    public int DangGiu { get; set; }

    /// <summary>Số lượng **đã trả** (từ các phiếu `CustomerReturn` của đơn thuộc kênh).</summary>
    public int DaTra { get; set; }
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
    Task<DonHangDto> TraHangAsync(Guid id, TraHangInput input);

    /// <summary>🚨 **PHÂN BỔ TỒN THEO KÊNH** (`INV-003` bước 1 — chỉ đọc): mỗi cặp (kênh, SKU) đã bán / đang giữ / đã trả.</summary>
    Task<List<PhanBoKenhDto>> GetPhanBoTheoKenhAsync();

    Task<DonHangDto> GetAsync(Guid id);
}
