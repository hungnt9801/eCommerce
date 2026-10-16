namespace eCommerce.Web.Navigation;

/// <summary>
/// 🚨 **KHO KHAI BÁO MENU ĐIỀU HƯỚNG** của `eCommerce.Web` (`WEB-001`).
///
/// **VÌ SAO KHAI Ở MỘT CHỖ (không rải trong từng `.cshtml`):**
/// *"màn hình nào tồn tại và ai được thấy nó"* là **một** câu hỏi. Rải ra nhiều view ⇒ thêm màn hình mới
/// là **phải nhớ** sửa menu của **mọi** trang, và chỗ quên thì màn hình đó **vô hình** với người dùng
/// (không có lối vào — đúng nợ đã ghi ở `web-001` §81.2 #7: *"chưa có lối vào từ UI — hiện truy cập
/// bằng URL"*). Khai một chỗ ⇒ có **một** nơi để rà, và có **test** cho nó.
///
/// 🔒 **Quy tắc khai quyền (bắt buộc):** mỗi mục **phải** khai quyền mà **trang đích** thực sự cần —
/// lấy từ `[Authorize(...)]` của **app service** mà trang đó gọi (app service mới là nơi cưỡng chế;
/// `[Authorize]` trên controller chỉ là **xác thực**). 🛡️ `BatBienDieuHuongTests` kiểm chéo điều này.
/// </summary>
public static class MenuDieuHuong
{
    /// <summary>
    /// Toàn bộ mục menu, **theo thứ tự hiển thị**.
    ///
    /// 📌 **Thứ tự có chủ ý = thứ tự công việc hằng ngày:** đơn hàng (việc chính) → phân bổ tồn (đối chiếu)
    /// → sản phẩm (tra cứu) → khách hàng → thanh toán/hoàn tiền.
    /// </summary>
    public static IReadOnlyList<MucDieuHuong> TatCa { get; } =
    [
        new()
        {
            Nhan = "Đơn hàng",
            Url = "/DonHang",
            // `DonHangController` gọi `IDonHangAppService` (đã `[Authorize(Orders.Default)]`) để đọc,
            // và các hành động xác nhận/huỷ/trả gọi `[Authorize(Orders.Manage)]`.
            // ⇒ Menu chỉ cần quyền **đọc** (mở được danh sách); nút hành động vẫn tự chặn nếu thiếu `Manage`.
            QuyenCanCo = eCommerceConsts.Permissions.Orders.Default,
            MoTa = "Danh sách đơn, xác nhận/huỷ/trả hàng"
        },
        new()
        {
            Nhan = "Phân bổ tồn theo kênh",
            Url = "/PhanBo",
            // `PhanBoController` gọi `IDonHangAppService.GetPhanBoTheoKenhAsync()` ⇒ cùng quyền đọc đơn.
            QuyenCanCo = eCommerceConsts.Permissions.Orders.Default,
            MoTa = "Đã bán / đang giữ / đã trả theo từng kênh"
        },
        new()
        {
            Nhan = "Sản phẩm",
            Url = "/SanPham",
            // `SanPhamController` gọi `IPriceListAppService` (`Pricing.Default`) + `IProductAppService` (`Products.Default`).
            // 🔒 Chọn quyền **hẹp hơn về mặt hiển thị** trong hai quyền: trang **không dựng được** nếu thiếu
            // `Pricing.Default` (bảng giá là nguồn của danh sách) ⇒ khai `Pricing.Default`.
            // ⚠️ Nếu chỉ khai `Products.Default`, người dùng có `Products` mà **không** có `Pricing` sẽ thấy
            // mục menu rồi nhận **trang lỗi DI/quyền** — đúng ca "menu nói dối" mà lượt này muốn chặn.
            // ⚠️ Quyền của `Pricing` **KHÔNG** nằm ở `eCommerceConsts.Permissions` — nó ở
            // `eCommerce.Pricing.PricingPermissions` (module sở hữu hằng số của mình — quy ước #2).
            // 📌 Tôi đã viết `eCommerceConsts.Permissions.Pricing.Default` và **build đỏ** ngay:
            // đó là ích lợi của việc để hằng số ở nơi có kiểu — **không** phải chuỗi trần.
            QuyenCanCo = eCommerce.Pricing.PricingPermissions.Default,
            MoTa = "Danh mục sản phẩm kèm giá niêm yết"
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // ⏳ **BA MỤC BỊ CẮT — CỐ Ý, KHÔNG PHẢI QUÊN** (xem `web-001` §95.4):
        //
        // Tôi đã định khai thêm `/KhachHang`, `/ThanhToan`, `/HanMucKenh` (app service + API của chúng
        // **đã có thật**). Nhưng kiểm `src/eCommerce.Web/Controllers/` thì **BA TRANG ĐÓ CHƯA TỒN TẠI**,
        // và host `eCommerce.Web` cũng **chưa** nạp module `Customers`/`PaymentHub`/`ChannelLimits`.
        //
        // 🚨 **VÌ SAO KHÔNG KHAI BỪA (đây là quyết định, không phải bỏ sót):**
        // một mục menu trỏ tới route **không tồn tại** biến lỗi **404** thành **"tính năng trông như đã có"**.
        // Người dùng bấm vào, thấy lỗi, và **không ai** biết đó là *"chưa làm"* hay *"hệ thống hỏng"*.
        // 📌 Đúng cùng họ lỗi mà cả dự án đang chống: *"báo có trong khi chưa có"*.
        // 🛡️ `BatBienDieuHuongTests` khoá bất biến: **mọi `Url` trong menu PHẢI có controller thật** —
        // nên nếu ai thêm lại ba mục đó mà không tạo trang, **test sẽ đỏ**.
        //
        // ⇒ Đường đúng: làm **trang** trước (controller + view), **rồi** thêm mục menu.
        // ══════════════════════════════════════════════════════════════════════════════════════════════
    ];
}
