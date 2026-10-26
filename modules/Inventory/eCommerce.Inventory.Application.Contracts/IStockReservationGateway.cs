using eCommerce.Application.Contracts.Inventory;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **HỢP ĐỒNG GIỮ CHỖ CHO MODULE KHÁC** (`P0 #1`, 26/10/2026) — gỡ nợ *"Sales tham chiếu
/// `Inventory.Domain`"*.
///
/// 🔑 **VÌ SAO KHÔNG DÙNG `IStockReservationAppService`:** app service đó có `[Authorize(Inventory.Default)]`
/// ở cấp class. Sales gọi nó trong tiến trình với principal của **Sales** (không có quyền Inventory) ⇒
/// `AbpAuthorizationException` — đúng lỗi đã đo được ở `SAL-005` với `PaymentAppService`.
/// 📌 Hợp đồng này **không** phải endpoint, **không** kiểm quyền; quyền đã được kiểm ở app service **gọi nó**
/// (Sales) và ở chính API giữ chỗ của Inventory.
/// 🔒 Chỉ các thao tác **hợp lệ cho người gọi khác module**: đọc lượt đang giữ + nhả/chốt theo nguồn.
/// **Không** mở `GiuChoAsync` ở đây — giữ chỗ là use case của Inventory (qua API/`IStockReservationAppService`).
/// </summary>
public interface IStockReservationGateway
{
    /// <summary>Lượt giữ chỗ của **một** nguồn (mặc định chỉ `Held`), sắp xếp ổn định.</summary>
    Task<List<StockReservationDto>> LayTheoNguonAsync(string referenceId, bool chiDangGiu = true);

    /// <summary>Toàn bộ lượt đang `Held` — dùng cho báo cáo phân bổ theo kênh.</summary>
    Task<List<StockReservationDto>> LayDangGiuAsync();

    /// <summary>Nhả mọi lượt `Held` của nguồn; trả **số lượt đã nhả**. Dùng khi huỷ đơn.</summary>
    Task<int> NhaTheoNguonAsync(string referenceId, DateTime at, ReservationStatus lyDo);

    /// <summary>Chốt (chuyển `Held → Committed`) mọi lượt của nguồn; trả **số lượt đã chốt**. Dùng khi tạo đơn.</summary>
    Task<int> ChotTheoNguonAsync(string referenceId, DateTime at);
}
