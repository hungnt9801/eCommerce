namespace eCommerce;

/// <summary>
/// Tên permission, tên policy và mã lỗi dùng chung.
/// Permission của ABP cần khai báo tập trung để UI/role đọc được.
/// </summary>
public static class eCommerceConsts
{
    public const string DbTablePrefix = "App";
    public const string? DbSchema = null;

    /// <summary>Tên connection string mặc định (xem appsettings.json).</summary>
    public const string DefaultConnectionStringName = "Default";

    public static class Permissions
    {
        public const string GroupName = "eCommerce";

        public static class Products
        {
            public const string Default = GroupName + ".Products";
            /// <summary>Xem danh mục sản phẩm (`CAT-001` D10 — F9: trước đây **thiếu**, dẫn tới không phân biệt được người chỉ-đọc).</summary>
            public const string View = Default + ".View";
            public const string Create = Default + ".Create";
            public const string Edit = Default + ".Edit";
            public const string Delete = Default + ".Delete";
            /// <summary>
            /// Bật/tắt hiển thị sản phẩm (`CAT-001` D10 — F9).
            /// 🚨 Tách khỏi `Edit` **có chủ ý**: publish là hành động **ra ngoài thị trường** (khách nhìn thấy),
            /// khác sửa mô tả nội bộ ⇒ quyền phải tách để giao cho vai trò khác nhau.
            /// </summary>
            public const string Publish = Default + ".Publish";
        }

        public static class Inventory
        {
            public const string Default = GroupName + ".Inventory";
            public const string Adjust = Default + ".Adjust";
        }

        public static class Channels
        {
            public const string Default = GroupName + ".Channels";
            /// <summary>Kết nối / ngắt kết nối một shop trên sàn (OAuth).</summary>
            public const string Manage = Default + ".Manage";
            /// <summary>Kích hoạt đồng bộ thủ công.</summary>
            public const string Sync = Default + ".Sync";
        }

        public static class Orders
        {
            public const string Default = GroupName + ".Orders";
            public const string Manage = Default + ".Manage";
        }

        public static class Payments
        {
            public const string Default = GroupName + ".Payments";
            public const string Refund = Default + ".Refund";
        }

        /// <summary>
        /// Quyền của module <b>Tax</b> (XC-006). Ba việc tách biệt có chủ ý:
        /// kế toán nhập liệu — kế toán trưởng duyệt — kế toán trưởng xuất Mẫu 01.
        /// </summary>
        public static class TaxClassification
        {
            public const string Default = GroupName + ".Tax.Classification";
            public const string View = Default + ".View";
            public const string Manage = Default + ".Manage";
            public const string Approve = Default + ".Approve";
            public const string Import = Default + ".Import";
            public const string ExportMau01 = Default + ".ExportMau01";
        }

        /// <summary>Đổi khung hiệu lực mức thuế suất — theo Q6 đã chốt, thuộc KẾ TOÁN TRƯỞNG, có duyệt + audit.</summary>
        public static class TaxRateSchedule
        {
            public const string Default = GroupName + ".Tax.RateSchedule";
            public const string Manage = Default + ".Manage";
        }
    }

    /// <summary>Mã lỗi nghiệp vụ (dùng với BusinessException).</summary>
    public static class ErrorCodes
    {
        public const string InsufficientStock = "eCommerce:Inventory:0001";
        // 🚨 F2 của XC-003 — ĐÃ XOÁ 3 mã lỗi miền KÊNH khỏi đây (13/10/2026):
        // chúng trùng khái niệm với ChannelHubErrorCodes nhưng KHÁC định dạng
        // ("eCommerce:Channel:*" vs "ChannelHub:*") ⇒ hai nguồn sự thật cho cùng một mã lỗi.
        // Theo quy ước "module sở hữu hằng số của mình", mã lỗi miền kênh nay CHỈ nằm ở
        // ChannelHubConsts.cs, đã chuẩn hoá về dạng "eCommerce:Channel:*".
        public const string PaymentSignatureInvalid = "eCommerce:Payment:0001";

        /// <summary>
        /// Mã lỗi miền **Catalog** (`CAT-001` §6.3, dải `eCommerce:Catalog:00xx`).
        /// 📌 Ở app (không phải module) vì entity Catalog hiện vẫn nằm ở `src/` — `CAT-001` **D1**:
        /// tách thành `modules/Catalog` khi `CAT-002`/`CAT-003` thêm entity, lúc đó dời cả file này.
        /// </summary>
        public static class Catalog
        {
            /// <summary>Sản phẩm không tồn tại.</summary>
            public const string KhongTimThaySanPham = "eCommerce:Catalog:0001";

            /// <summary>SKU đã tồn tại trong tenant (xung đột ở tầng DB — `CAT-001` D3/F6).</summary>
            public const string SkuDaTonTai = "eCommerce:Catalog:0002";

            /// <summary>Publish khi sản phẩm **không có biến thể đang hoạt động** (`CAT-001` D9).</summary>
            public const string KhongCoBienTheHoatDong = "eCommerce:Catalog:0003";

            /// <summary>Chuyển trạng thái không hợp lệ (state machine phải NÉM, không im lặng).</summary>
            public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Catalog:0004";

            /// <summary>Mã sản phẩm/SKU đã tồn tại (kiểm tra trước khi ghi).</summary>
            public const string MaDaTonTai = "eCommerce:Catalog:0005";

            /// <summary>SKU không hợp lệ sau khi chuẩn hoá (rỗng/ký tự không cho phép/quá dài).</summary>
            public const string SkuKhongHopLe = "eCommerce:Catalog:0006";

            /// <summary>Giá không hợp lệ (âm).</summary>
            public const string GiaKhongHopLe = "eCommerce:Catalog:0007";

            /// <summary>Thao tác bị cấm khi SKU/sản phẩm đã phát sinh giao dịch.</summary>
            public const string DaPhatSinhGiaoDich = "eCommerce:Catalog:0008";

            /// <summary>Không được xoá cứng — chỉ xoá mềm (`CAT-001` D4/F7).</summary>
            public const string CamXoaCung = "eCommerce:Catalog:0009";

            /// <summary>Thiếu thông tin bắt buộc (ví dụ tên sản phẩm rỗng).</summary>
            public const string ThieuThongTinBatBuoc = "eCommerce:Catalog:0010";
        }
    }

    /// <summary>Tên khoá distributed lock — mọi nơi phải dùng chung hàm này để không lệch khoá.</summary>
    public static class LockKeys
    {
        // 🚨 F1 — XC-003 §15.2: khoá của miền KÊNH đã được XOÁ khỏi đây (13/10/2026).
        // Trước đó chúng tồn tại ở CẢ HAI nơi với CHUỖI KHÁC NHAU:
        //   • ở đây: `ecommerce:channel:{channelCode}:{shopId}:token-refresh`
        //   • ở module: `channel:{channelCode}:{shopId}:token-refresh`  (thiếu tiền tố `ecommerce:`)
        // ⇒ Nếu hai đoạn code dùng hai bản khác nhau thì khoá KHÔNG bảo vệ gì, và KHÔNG có lỗi biên dịch nào.
        // Khoá của miền kênh do CHÍNH module ChannelHub sở hữu — xem ChannelHubConsts.LockKeys.

        /// <summary>Khoá khi trừ tồn kho của một SKU trong một kho.</summary>
        /// <remarks>
        /// 📌 F4 — XC-003 §15.2: khoá này còn nằm ở app vì entity Inventory hiện ở <c>src/</c>.
        /// Khi tách module Inventory (GĐ1) thì chuyển khoá này về module đó (quy ước D3 của XC-003).
        /// </remarks>
        public static string InventorySku(string warehouseId, string sku)
            => $"inventory:{warehouseId}:{sku}";

        /// <summary>
        /// Khoá khi **giành quyền tạo** một SKU trong tenant (`CAT-001` **D6/D7**) — chống hai request
        /// đồng thời cùng vượt qua bước "kiểm tra chưa tồn tại" rồi cùng ghi.
        ///
        /// 🚨 VÌ SAO VẪN CẦN KHOÁ KHI DB ĐÃ CÓ UNIQUE INDEX: unique index là **hàng rào cuối** (đúng, và phải
        /// giữ), nhưng nếu chỉ dựa vào nó thì request thua cuộc nhận `DbUpdateException` **khó hiểu** ở tầng DB
        /// thay vì một mã lỗi nghiệp vụ rõ ràng. Khoá ⇒ tuần tự hoá ⇒ kiểm tra trong bộ nhớ **có hiệu lực**.
        ///
        /// ⚠️ Tên khoá **KHÔNG** chứa tiền tố `ecommerce:` — tiền tố do `AbpDistributedLockOptions.KeyPrefix`
        /// cấp (`XC-003` §5.4; tự thêm vào sẽ thành **tiền tố đôi** — đúng lỗi C13 đã gặp).
        /// </remarks>
        public static string CatalogSku(string sku)
            => $"catalog:sku:{sku}";

        // 📌 Khoá liên quan tới kênh bán (refresh token, đẩy tồn kho) do CHÍNH module ChannelHub
        // sở hữu — xem modules/ChannelHub/ChannelHub.Domain/ChannelHubConsts.cs.
        // Không khai báo lại ở đây để tránh hai nguồn sự thật cho cùng một khoá lock.
    }
}
