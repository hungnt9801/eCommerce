namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`S14`** — **HỢP ĐỒNG NHẬP KHO HÀNG KHÁCH TRẢ** (module `Inventory` cho module `Sales` gọi).
///
/// 🔑 **VÌ SAO KHÔNG DÙNG TRỰC TIẾP `IStockDocumentAppService`:** app service đó mang
/// `[Authorize(Inventory.Default)]` ở **cấp class** (quy ước #4) ⇒ Sales gọi trong tiến trình với principal
/// của **Sales** (không có quyền Inventory) ⇒ **`AbpAuthorizationException`** — đúng lỗi đã đo ở `SAL-005`
/// với `PaymentAppService`. 📌 Hợp đồng này **không** phải endpoint, **không** kiểm quyền: quyền đã được kiểm
/// ở app service RMA (`Orders.Manage` cho thao tác nhận hàng).
/// 📌 Cùng khuôn với `IStockReservationGateway` (đã có tiền lệ, `P0 #1`).
///
/// 🔒 **Chứng từ `CustomerReturn` + GHI SỔ NGAY** (không để `Draft`): hàng đã nằm trong kho thật khi nhân viên
/// đã đếm — để `Draft` nghĩa là tồn kho **không** phản ánh thực tế và phải có người nhớ bấm "ghi sổ".
/// </summary>
public interface ICustomerReturnGateway
{
    /// <summary>
    /// Tạo + ghi sổ **một** chứng từ `CustomerReturn` cho hàng khách trả. Trả **`Id` chứng từ**.
    /// 🔒 **Idempotent theo `number`**: gọi lại cùng số chứng từ trả về chứng từ **đã có**, không nhập kho lần hai
    /// (nhập hai lần = **thừa hàng ảo**, và sổ cái append-only ⇒ sửa phải tạo chứng từ đảo).
    /// </summary>
    Task<Guid> NhapHangKhachTraAsync(
        Guid warehouseId,
        string number,
        IReadOnlyCollection<(string Sku, int Quantity)> lines,
        DateTime atUtc,
        string? note = null);
}
