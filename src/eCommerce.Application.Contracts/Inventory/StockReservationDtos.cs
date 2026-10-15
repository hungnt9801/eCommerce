using System.ComponentModel.DataAnnotations;
using eCommerce.Inventory;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

public class StockReservationDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
}

public class GiuChoInput
{
    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải ≥ 1.")]
    public int Quantity { get; set; } = 1;

    /// <summary>TTL (phút). Để trống ⇒ **15 phút** (`INV-002`). ⚠️ Giữ chỗ **vô thời hạn** là **khoá hàng vĩnh viễn**.</summary>
    [Range(1, 1440)]
    public int? TtlPhut { get; set; }

    /// <summary>Nguồn giữ chỗ — ví dụ `Cart:{cartId}` hoặc `Order:{orderId}` (để truy vết).</summary>
    [StringLength(64)]
    public string? ReferenceType { get; set; }

    [StringLength(128)]
    public string? ReferenceId { get; set; }

    public DateTime? At { get; set; }
}

/// <summary>Kết quả **chốt giữ chỗ** (`SAL-003`) — số lượt đã chốt (0 ⇒ nguồn này không còn gì đang giữ).</summary>
public class KetQuaChotDto
{
    public string ReferenceId { get; set; } = default!;
    public int SoLuotDaChot { get; set; }
}

/// <summary>Kết quả **nhả chỗ quá hạn** (job) — số lượt đã nhả.</summary>
public class KetQuaNhaChoHetHanDto
{
    public int SoLuotDaNha { get; set; }
}

/// <summary>
/// **API GIỮ CHỖ** (`INV-002`) — mắt xích cho **checkout**: giữ hàng trong lúc khách điền địa chỉ/thanh toán,
/// để **hai khách không cùng mua được món cuối cùng**.
/// </summary>
public interface IStockReservationAppService : IApplicationService
{
    /// <summary>
    /// **Giữ chỗ.** **NÉM `eCommerce:Inventory:0001`** nếu không đủ hàng (kèm `onHand`/`reserved`/`coTheBan`
    /// để client hiển thị *"còn lại N"*).
    /// </summary>
    Task<StockReservationDto> GiuChoAsync(GiuChoInput input);

    Task<StockReservationDto> GetAsync(Guid id);

    Task<List<StockReservationDto>> GetDanhSachAsync(ReservationStatus? status = null);

    /// <summary>**Nhả chỗ** theo yêu cầu (khách huỷ / đơn huỷ). **NÉM `0030`** nếu lượt giữ chỗ không còn `Held`.</summary>
    Task<StockReservationDto> NhaChoAsync(Guid id, DateTime? at = null);

    /// <summary>**Job**: nhả mọi lượt giữ chỗ **quá hạn** (đánh dấu `Expired`, phân biệt với nhả do huỷ).</summary>
    Task<KetQuaNhaChoHetHanDto> NhaChoHetHanAsync(DateTime? at = null);

    /// <summary>
    /// 🚨 **CHỐT GIỮ CHỖ → XUẤT KHO** (`SAL-003`): mọi lượt `Held` của **một nguồn** (ví dụ `Checkout:{khoá}`)
    /// chuyển thành **hàng ra khỏi kho**. Bất biến: `OnHand` và `Reserved` **cùng giảm** ⇒ `Available` **không đổi**.
    /// **NÉM `0031`** nếu có lượt **quá hạn** (*hàng đã được trả về cho người khác* ⇒ xuất tiếp là **bán hai lần**).
    /// </summary>
    Task<KetQuaChotDto> ChotTheoNguonAsync(string referenceId, DateTime? at = null);
}
