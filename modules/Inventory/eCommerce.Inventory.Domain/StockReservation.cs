using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// **LƯỢT GIỮ CHỖ** (`InvStockReservations`) — `INV-002`.
///
/// 🚨 **VÌ SAO PHẢI GIỮ CHỖ (không chỉ "kiểm tra còn hàng rồi thôi"):** giữa lúc **kiểm tra còn hàng** và lúc
/// **trừ kho thật** (khi đơn được xác nhận) có một khoảng thời gian — khách đang điền địa chỉ, đang thanh toán.
/// Nếu chỉ kiểm tra: **hai khách cùng thấy "còn 1"** và **cả hai** đặt được ⇒ **bán thứ không có**. Giữ chỗ
/// biến khoảng thời gian đó thành **cam kết có thời hạn** (`ExpiresAtUtc`) ⇒ người thứ hai thấy **hết hàng**.
///
/// 📌 **Phân biệt `Released` và `Expired` có chủ ý:** cả hai đều trả hàng về `Available`, nhưng **lý do khác
/// nhau** — `Released` là *khách/đơn huỷ*, `Expired` là *hết TTL*. Gộp làm một thì không trả lời được
/// *"bao nhiêu đơn mất vì khách bỏ ngang?"* (chỉ số vận hành quan trọng).
/// </summary>
public class StockReservation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public int Quantity { get; protected set; }

    public ReservationStatus Status { get; protected set; } = ReservationStatus.Held;

    /// <summary>Hạn giữ chỗ — **bắt buộc**: giữ chỗ **vô thời hạn** là **khoá hàng vĩnh viễn** (khách bỏ ngang ⇒ hàng không ai mua được).</summary>
    public DateTime ExpiresAtUtc { get; protected set; }

    /// <summary>Nguồn giữ chỗ (giỏ hàng/đơn) — để truy vết *"vì sao hàng này bị giữ"*.</summary>
    public string? ReferenceType { get; protected set; }

    public string? ReferenceId { get; protected set; }

    public DateTime? ReleasedAtUtc { get; protected set; }

    protected StockReservation() { }

    public StockReservation(
        Guid id, Guid warehouseId, string sku, int quantity, DateTime expiresAtUtc,
        string? referenceType = null, string? referenceId = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("`Sku` là BẮT BUỘC.", nameof(sku));
        }

        if (quantity < 1)
        {
            throw new BusinessException(InventoryErrorCodes.SoLuongPhaiDuong)
                .WithData("sku", sku).WithData("quantity", quantity);
        }

        WarehouseId = warehouseId;
        Sku = sku.Trim();
        Quantity = quantity;
        ExpiresAtUtc = expiresAtUtc;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
    }

    /// <summary>Đã quá hạn tại <paramref name="at"/>?</summary>
    public bool DaHetHan(DateTime at) => at >= ExpiresAtUtc;

    /// <summary>
    /// Nhả hàng về `Available`.
    /// **NÉM** nếu lượt giữ chỗ **không còn `Held`** ⇒ chống **nhả hai lần** (nhả hai lần = cộng `Available`
    /// **gấp đôi** ⇒ hệ thống tưởng còn nhiều hàng hơn thực tế ⇒ **bán thứ không có**).
    /// </summary>
    public void Nha(ReservationStatus lyDo, DateTime at)
    {
        if (Status != ReservationStatus.Held)
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("sku", Sku)
                .WithData("status", Status.ToString())
                .WithData("lyDo", "Lượt giữ chỗ KHÔNG còn ở trạng thái `Held` ⇒ không nhả được lần nữa.");
        }

        if (lyDo is not (ReservationStatus.Released or ReservationStatus.Expired))
        {
            throw new ArgumentOutOfRangeException(nameof(lyDo), lyDo, "Nhả chỉ nhận `Released` hoặc `Expired`.");
        }

        Status = lyDo;
        ReleasedAtUtc = at;
    }

    /// <summary>`Held → Committed` (đơn được xác nhận ⇒ phần giữ chỗ chuyển thành **xuất kho thật**).</summary>
    public void Chot(DateTime at)
    {
        if (Status != ReservationStatus.Held)
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("sku", Sku).WithData("status", Status.ToString());
        }

        // ⚠️ Giữ chỗ **đã hết hạn** mà vẫn chốt được ⇒ hệ thống bán hàng **đã trả về `Available`** cho khách khác
        // ⇒ **bán hai lần cùng một món**. Kiểm ở đây (lớp trong); job nhả chỗ là lớp ngoài.
        if (DaHetHan(at))
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoDaHetHan)
                .WithData("sku", Sku)
                .WithData("expiresAtUtc", ExpiresAtUtc)
                .WithData("at", at);
        }

        Status = ReservationStatus.Committed;
    }
}

/// <summary>
/// **HÀM THUẦN: có giữ được chỗ hay không** (`INV-002`) — tách khỏi I/O để test được **không cần DB**.
/// 🚨 Đây là chỗ **chống bán vượt tồn** — sai ở đây là **bán thứ không có**.
/// </summary>
public static class QuyetDinhGiuCho
{
    /// <summary>Hàng **có thể bán** = `OnHand − Reserved`. Âm ⇒ dữ liệu đã sai (xem `CHECK` `ADR-03`).</summary>
    public static int CoTheBan(int onHand, int reserved) => onHand - reserved;

    /// <summary>
    /// Kiểm tra **một lượt** giữ chỗ. Trả **`null`** nếu được phép; ngược lại trả **lý do** (để tầng gọi quyết định
    /// ném lỗi hay trả `409` — `INV-002` chưa chốt HTTP, nên hàm này **không** ném).
    /// ⚠️ Hàm **không** tự đọc DB: `onHand`/`reserved` do tầng gọi cung cấp **sau khi đã giành khoá**
    /// (đọc trước khi khoá là **vô nghĩa**: giá trị có thể đã đổi).
    /// </summary>
    public static string? KiemTra(int onHand, int reserved, int yeuCau)
    {
        if (yeuCau < 1)
        {
            return "Số lượng giữ chỗ phải ≥ 1.";
        }

        var coTheBan = CoTheBan(onHand, reserved);

        if (coTheBan < 0)
        {
            return $"Dữ liệu tồn kho KHÔNG hợp lệ: OnHand={onHand} < Reserved={reserved} (Available âm).";
        }

        return yeuCau > coTheBan
            ? $"Không đủ hàng: yêu cầu {yeuCau}, có thể bán {coTheBan} (OnHand={onHand}, Reserved={reserved})."
            : null;
    }
}
