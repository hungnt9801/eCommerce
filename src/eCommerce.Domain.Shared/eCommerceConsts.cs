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

            /// <summary>
            /// 🚨 **Tải lên / sửa / xoá ảnh sản phẩm** (`CAT-003` — `Products.ManageMedia` theo §6 Hợp đồng API).
            /// 📌 **VÌ SAO TÁCH KHỎI `Edit`:** tải tệp lên là **đường vào từ Internet** — cần kiểm **nội dung nhị
            /// phân** (`ADR-13`), và là bề mặt tấn công (`XSS` lưu trữ, `DoS` dung lượng). Người *"sửa mô tả sản phẩm"*
            /// **không** đương nhiên được **đẩy tệp lên máy chủ**: hai việc có **rủi ro hoàn toàn khác nhau**.
            /// 🔒 Gộp quyền ⇒ mọi nhân viên sửa sản phẩm đều tải được tệp lên.
            /// ⚠️ Không khai ở `eCommercePermissionDefinitionProvider` ⇒ `403` cho **mọi** người (kể cả admin).
            /// </summary>
            public const string ManageMedia = Default + ".ManageMedia";
        }

        /// <summary>
        /// Quyền của **cây danh mục** (`CAT-002` §9.2). 🚨 `Move`/`Deactivate` là hành động **đổi cấu trúc
        /// hiển thị ra ngoài** (menu storefront) nên gộp vào `Edit` **có chủ ý**, tách khỏi `Create`/`Delete`.
        /// </summary>
        public static class Categories
        {
            public const string Default = GroupName + ".Categories";
            public const string Create = Default + ".Create";
            public const string Edit = Default + ".Edit";
            public const string Delete = Default + ".Delete";
        }

        /// <summary>
        /// <summary>Quyền của **định nghĩa thuộc tính** (`CAT-002` §9.2) — gồm cả sinh tổ hợp biến thể.</summary>
        public static class CatalogAttributes
        {
            public const string Default = GroupName + ".CatalogAttributes";
            public const string Manage = Default + ".Manage";
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

        /// <summary>
        /// 🚨 **`INV-003` / `REC-001` — CHÍNH SÁCH "CHO PHÉP CHỌN" + HẠN MỨC RIÊNG TỪNG KÊNH.**
        ///
        /// 📌 **VÌ SAO CẦN PERMISSION RIÊNG (không mượn `Inventory.Adjust`):** hạn mức phân bổ **không phải**
        /// nghiệp vụ kho (`Adjust` = sửa tồn) và **không phải** cấu hình hạ tầng (nhóm `SettingManagement`).
        /// Đây là **quyết định kinh doanh** (*"kênh nào được bán bao nhiêu"*) ⇒ người được cấp quyền sửa tồn
        /// **không đương nhiên** được đổi trần bán của kênh (bài học `0040`/`0042`: mã/quyền đúng kỹ thuật
        /// nhưng **sai nghiệp vụ** ⇒ người nhận **chỉ dẫn sai**).
        ///
        /// ⚠️ **THIẾU ĐỊNH NGHĨA ⇒ `403` CHO MỌI NGƯỜI** (kể cả `admin`) — đúng lỗi `F-403`/`XC-004` D2.
        /// Vì vậy hằng số ở đây **phải** được khai trong `eCommercePermissionDefinitionProvider`.
        /// </summary>
        public static class ChannelLimits
        {
            /// <summary>Xem hạn mức theo kênh + chính sách đang áp.</summary>
            public const string Default = GroupName + ".ChannelLimits";

            /// <summary>Thêm/sửa/xoá hạn mức theo kênh và đổi chính sách (`Inv.ChannelLimit.*`, `Recon.*`).</summary>
            public const string Manage = Default + ".Manage";
        }

        /// <summary>
        /// 🚨 **`CUS-001` — HỒ SƠ KHÁCH HÀNG.**
        ///
        /// 📌 **VÌ SAO TÁCH `View` VÀ `Manage` (không gộp một quyền):** hồ sơ khách là **dữ liệu CÁ NHÂN**
        /// (Luật BV dữ liệu cá nhân **91/2025/QH15**). Người **đọc** để chăm sóc khách (**CSKH**) **không**
        /// đương nhiên được **chặn/gỡ chặn/nâng cấp** hồ sơ (đổi quyền mua hàng + đổi chủ sở hữu) —
        /// hai việc có **mức rủi ro khác hẳn nhau**.
        /// 🔒 `Manage` còn là quyền **bắt buộc** cho `Chan`/`MoChan` (`ADR-04`: cần lý do + người chịu trách nhiệm).
        /// ⚠️ **THIẾU ĐỊNH NGHĨA ⇒ `403` CHO MỌI NGƯỜI** (kể cả `admin`) — đúng lỗi `F-403`/`XC-004` D2.
        /// </summary>
        public static class Customers
        {
            /// <summary>Xem hồ sơ khách (đã có kiểm PII khi log — `ADR-11`).</summary>
            public const string Default = GroupName + ".Customers";

            /// <summary>Tạo/nâng cấp/chặn/gỡ chặn hồ sơ khách.</summary>
            public const string Manage = Default + ".Manage";
        }
    }

    /// <summary>
    /// Giới hạn độ dài của miền **Catalog** — đặt ở `Domain.Shared` để **entity VÀ DTO dùng CHUNG một nguồn**
    /// (`CAT-001` D11). ⚠️ Tầng `Application.Contracts` **không** được tham chiếu `Domain` (layering) ⇒
    /// nếu để hằng số trong entity thì DTO buộc phải **chép lại số** ⇒ hai nguồn sự thật, lệch lúc nào không biết.
    /// </summary>
    public static class CatalogLimits
    {
        /// <summary>Độ dài tối đa của mã sản phẩm (`Code`) — khớp `HasMaxLength(64)` ở DbContext.</summary>
        public const int MaToiDa = 64;

        /// <summary>Độ dài tối đa của SKU — khớp `HasMaxLength(64)` ở DbContext.</summary>
        public const int SkuToiDa = 64;
    }

    /// <summary>
    /// Giới hạn độ dài của miền **Kho / kênh** (`INV-003`) — đặt ở `Domain.Shared` để **entity VÀ DTO dùng CHUNG
    /// một nguồn**. ⚠️ Lý do y hệt `CatalogLimits`: tầng `Application.Contracts` **không** được tham chiếu `Domain`
    /// ⇒ để hằng số trong entity thì DTO buộc phải **chép lại số** ⇒ hai nguồn sự thật, lệch lúc nào không biết
    /// (build đã **bắt được thật**: `CS0103: The name 'InvChannelLimit' does not exist`).
    /// </summary>
    public static class InventoryLimits
    {
        /// <summary>Độ dài tối đa của mã kênh — khớp `HasMaxLength(32)` của `InvChannelLimits.ChannelCode`
        /// **và** `Order.ChannelCode` / `StockLedgerEntry.ChannelCode`.</summary>
        public const int MaKenhToiDa = 32;
    }

    /// <summary>Mã lỗi nghiệp vụ (dùng với BusinessException).</summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// 🚨 **ĐÃ GỘP VỀ MỘT NGUỒN DUY NHẤT (15/10/2026, `INV-001`)** — trước đây mã này khai **ở đây**
        /// trong khi miền kho cần **cả dải** `eCommerce:Inventory:*` ⇒ dễ sinh **hai nguồn sự thật**
        /// (đúng họ lỗi `XC-003` **F2**). Nay bản chuẩn là <see cref="InventoryErrorCodes.KhongDuTon"/>;
        /// tên cũ **giữ lại làm bí danh** để không phá mã đang dùng.
        /// </summary>
        public const string InsufficientStock = eCommerce.Inventory.InventoryErrorCodes.KhongDuTon;
        // 🚨 F2 của XC-003 — ĐÃ XOÁ 3 mã lỗi miền KÊNH khỏi đây (13/10/2026):
        // chúng trùng khái niệm với ChannelHubErrorCodes nhưng KHÁC định dạng
        // ("eCommerce:Channel:*" vs "ChannelHub:*") ⇒ hai nguồn sự thật cho cùng một mã lỗi.
        // Theo quy ước "module sở hữu hằng số của mình", mã lỗi miền kênh nay CHỈ nằm ở
        // ChannelHubConsts.cs, đã chuẩn hoá về dạng "eCommerce:Channel:*".
        public const string PaymentSignatureInvalid = "eCommerce:Payment:0001";

        /// <summary>
        /// 🚨 **WATERMARK CỦA ĐƠN SÀN KHÔNG TIẾN** (`SAL-005` **BR-SAL-005-008** / nhánh `E4`).
        ///
        /// **NGHĨA CHÍNH XÁC:** một luồng khác **đã** áp một bản **mới hơn** (hoặc bằng) cho **cùng** đơn sàn
        /// ⇒ luồng này **THUA** và **phải không ghi gì thêm**.
        ///
        /// 🔑 **VÌ SAO MÃ RIÊNG (không mượn mã nào khác):** đây **không** phải lỗi hệ thống, **không** phải
        /// lỗi dữ liệu vào, và **không** đáng để người trực bị đánh thức — nó là **kết quả ĐÚNG của một cuộc
        /// đua** giữa hai đường vào (webhook + pull, `D2`). Nhưng nó **tuyệt đối không được nuốt**: coi là
        /// *"thành công"* thì luồng thua **vẫn ghi tiếp** ⇒ hai luồng cùng ghi ⇒ trạng thái cuối **phụ thuộc
        /// thứ tự đến** — đúng thứ `D6` cấm.
        /// 📌 Đây là cùng khuôn với `INV-003` **`0043`** (thiếu mã kênh): mã **hẹp** cho một ca **hẹp**,
        /// để `WithData` nói được **ai thắng** (`phienBanHienTai` vs `phienBanMoi`).
        /// </summary>
        public const string SalesWatermarkKhongTien = "eCommerce:Sales:0003";

        // 📌 CÁC MÃ HOÀN TIỀN (`0002`/`0003`/`0004`) **CỐ Ý KHÔNG Ở ĐÂY** — chúng nằm ở
        // `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/PaymentErrorCodes.cs`.
        // VÌ SAO: `PaymentHub.Domain` **KHÔNG** tham chiếu `eCommerce.Domain.Shared` (module tự chứa — quy ước #2
        // "module sở hữu hằng số của mình"), mà entity `PaymentTransaction` **cần** ném các mã đó.
        // ⇒ Để bản chuẩn trong module; nếu để thêm ở đây nữa là **HAI nguồn sự thật** cho cùng một mã
        // (đúng họ lỗi `XC-003` F2 mà dự án đã trả giá).

        /// <summary>
        /// 🚨 **MÃ LỖI DÙNG CHUNG CHO KHOÁ PHÂN TÁN** — **KHÔNG** thuộc dải của module nào.
        ///
        /// Vì sao **không** nằm trong dải `eCommerce:Catalog:*`: không giành được khoá là **cơ chế hạ tầng**,
        /// không phải lỗi nghiệp vụ của Catalog. `XC-003` **D10** quy định **một mã chung** cho mọi module
        /// (kèm HTTP `409` và thông điệp chuẩn).
        ///
        /// 🚨 **BÀI HỌC THẬT (15/10/2026):** lúc đầu tôi tự chế `eCommerce:Catalog:0011` cho ca này
        /// ⇒ **vi phạm** `XC-003` D10 và tạo **hai nguồn sự thật** cho cùng một nguyên nhân: Catalog ném
        /// `Catalog:0011` còn mọi module khác ném `Lock:0001`. Đã sửa: dùng **mã chung** dưới đây.
        /// </summary>
        public const string KhongGianhDuocKhoa = "eCommerce:Lock:0001";

        /// <summary>
        /// 🚨 **Giá trị setting KHÔNG hợp lệ** (đường GHI cấu hình — `INV-003`/`REC-001`).
        ///
        /// 📌 **VÌ SAO CẦN (và vì sao khác hẳn đường ĐỌC):** hàm đọc chính sách (`ChinhSach.Doc*`) **KHÔNG ném**
        /// khi gặp giá trị lạ — nó trả **mặc định an toàn** + cờ `hopLe = false` để chỗ gọi **ghi cảnh báo**.
        /// Thiết kế đó là **cố ý** cho đường giữ chỗ (một giá trị gõ sai **không** được làm chết việc bán hàng).
        /// 🔒 Nhưng ở **đường GHI của admin** thì ngược lại: chấp nhận giá trị lạ nghĩa là lưu vào DB một cấu hình
        /// **sẽ bị bỏ qua lúc chạy** ⇒ người vận hành **tin** đã đổi mà thực tế không —— đúng loại "tắt im lặng".
        /// ⇒ Ở đường ghi: **NÉM**, kèm **danh sách giá trị hợp lệ** để người dùng sửa được ngay.
        /// </summary>
        public const string GiaTriSettingKhongHopLe = "eCommerce:Settings:0001";

        /// <summary>
        /// <summary>
        /// 🚨 **SỔ ĐĂNG KÝ MÃ LỖI MIỀN CATALOG — NGUỒN SỰ THẬT DUY NHẤT.**
        ///
        /// ⚠️ **ĐỌC TRƯỚC KHI THÊM MÃ MỚI:** hai tài liệu `CAT-001` (§6.3) và `CAT-002` (§12) từng gán
        /// **CÙNG dải số** `eCommerce:Catalog:0001…0009` cho **NGHĨA KHÁC NHAU** — đo được 15/10/2026:
        ///
        /// | Mã | `CAT-001` §6.3 | `CAT-002` §12 |
        /// |---|---|---|
        /// | `0001` | Code sản phẩm trùng | **chu trình cây danh mục** |
        /// | `0002` | SKU trùng | **xoá danh mục còn con** |
        /// | `0003` | Publish thiếu biến thể | **vượt trần tổ hợp** |
        ///
        /// ⇒ Cứ thế triển khai thì **cùng một mã mang hai nghĩa** tuỳ tài liệu được đọc — đúng họ lỗi
        /// *"hai nguồn sự thật"* mà `XC-003` F2/F5 đã gặp. **ĐÃ CHỐT:** lấy `CAT-001` §6.3 làm chuẩn cho
        /// dải thấp (đã triển khai + kiểm chứng thật), `CAT-002` **đánh số lại** sang dải **`0020+`**.
        ///
        /// 📌 Ở `src/` (không phải module) vì entity Catalog hiện vẫn nằm ở `src/` — `CAT-001` **D1**:
        /// tách thành `modules/Catalog` khi `CAT-002`/`CAT-003` thêm entity, lúc đó dời cả khối này.
        ///
        /// 🛡️ Bất biến được khoá bằng test `CatalogErrorCodeRegistryTests` (bắt trùng giá trị + sai vị trí).
        /// </summary>
        public static class Catalog
        {
            // ── Dải 0001–0009: `CAT-001` §6.3 (đã triển khai + kiểm chứng) ──

            /// <summary>`Code` sản phẩm đã tồn tại trong tenant (`CAT-001` §6.3).</summary>
            public const string MaSanPhamDaTonTai = "eCommerce:Catalog:0001";

            /// <summary>SKU đã tồn tại trong tenant (kể cả khi va vào unique index — `CAT-001` D3/F6).</summary>
            public const string SkuDaTonTai = "eCommerce:Catalog:0002";

            /// <summary>Publish khi sản phẩm **không có biến thể đang hoạt động** (`CAT-001` D9).</summary>
            public const string KhongCoBienTheHoatDong = "eCommerce:Catalog:0003";

            /// <summary>Chuyển trạng thái không hợp lệ (state machine phải NÉM, không im lặng — `CAT-001` D5).</summary>
            public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Catalog:0004";

            /// <summary>Đổi `SKU` đã phát sinh giao dịch (`CAT-001` **D2**/BR-003).</summary>
            public const string DaPhatSinhGiaoDich = "eCommerce:Catalog:0005";

            /// <summary>Xoá sản phẩm còn được tham chiếu — chỉ cho **xoá mềm** (`CAT-001` D4/F7).</summary>
            public const string SanPhamConThamChieu = "eCommerce:Catalog:0006";

            /// <summary>`Price`/`CompareAtPrice` âm (`CAT-001` BR-011).</summary>
            public const string GiaKhongHopLe = "eCommerce:Catalog:0007";

            /// <summary>`SKU` không hợp lệ sau chuẩn hoá (rỗng/ký tự lạ/quá dài — `CAT-001` BR-002).</summary>
            public const string SkuKhongHopLe = "eCommerce:Catalog:0008";

            /// <summary>Vượt trần tổ hợp biến thể (`CAT-001` BR-007 / `CAT-002` D6) — **dành cho `CAT-002`**.</summary>
            public const string VuotTranToHopBienThe = "eCommerce:Catalog:0009";

            // ── Dải 0010+: BỔ SUNG ngoài §6.3 (ghi rõ để không ai tưởng là có trong tài liệu gốc) ──

            /// <summary>Không tìm thấy sản phẩm. ⚠️ **BỔ SUNG** — `CAT-001` §6.3 **không** có mã này.</summary>
            public const string KhongTimThaySanPham = "eCommerce:Catalog:0010";

            /// <summary>Thiếu thông tin bắt buộc (ví dụ tên sản phẩm rỗng). ⚠️ **BỔ SUNG**.</summary>
            public const string ThieuThongTinBatBuoc = "eCommerce:Catalog:0011";

            /// <summary>`Code` sản phẩm không hợp lệ (rỗng/quá dài). ⚠️ **BỔ SUNG** — KHÁC mã của SKU (0008).</summary>
            public const string MaSanPhamKhongHopLe = "eCommerce:Catalog:0012";

            // ── Dải 0020+: `CAT-002` (đánh số LẠI — §12 của tài liệu đó đã được cập nhật) ──

            /// <summary>Di chuyển danh mục vào **cây con của chính nó** ⇒ tạo chu trình (`CAT-002` D8/§12).</summary>
            public const string ChuTrinhDanhMuc = "eCommerce:Catalog:0020";

            /// <summary>Xoá/gán danh mục còn **con** hoặc còn **sản phẩm**, hoặc danh mục đã ngừng dùng (`CAT-002` D9).</summary>
            public const string DanhMucConRangBuoc = "eCommerce:Catalog:0021";

            /// <summary>Va chạm tổ hợp thuộc tính khi đã kiểm tra (race) (`CAT-002` D7).</summary>
            public const string TrungToHopThuocTinh = "eCommerce:Catalog:0022";

            /// <summary>Thuộc tính `Variant` đang được biến thể dùng ⇒ chỉ cho `IsActive = false` (`CAT-002` §12).</summary>
            public const string ThuocTinhDangDuocDung = "eCommerce:Catalog:0023";

            /// <summary>Vượt độ sâu tối đa của cây danh mục (`CAT-002` §8.1 #3).</summary>
            public const string VuotDoSauDanhMuc = "eCommerce:Catalog:0024";

            /// <summary>
            /// `Code` **danh mục** không hợp lệ (rỗng/quá dài).
            /// ⚠️ **KHÁC** `0012` (`MaSanPhamKhongHopLe` — mã của **sản phẩm**): dùng lẫn mã của hai loại thực thể
            /// thì client/đối soát không biết lỗi nằm ở **sản phẩm** hay ở **danh mục**.
            /// </summary>
            public const string MaDanhMucKhongHopLe = "eCommerce:Catalog:0025";

            /// <summary>
            /// 🚨 **SLUG TRÙNG trong tenant** (`CAT-003` **ADR-03**) — hàng rào DB
            /// `IX_CatProducts_TenantId_Slug` (`NULLS NOT DISTINCT`, filter `Slug IS NOT NULL`).
            /// 📌 **VÌ SAO LÀ LỖI NGHIỆP VỤ, KHÔNG PHẢI `500`:** slug là **khoá định danh công khai trên URL**;
            /// hai sản phẩm cùng slug ⇒ **một** trong hai URL **không bao giờ** truy cập được, và **máy tìm kiếm
            /// đánh chỉ mục sai sản phẩm**. Client cần biết *"slug này đã dùng"* để **chọn slug khác**, chứ
        /// không phải "hệ thống hỏng" (đúng họ lỗi Phụ lục B: ràng buộc chặn hành vi **hợp lệ về nghiệp vụ**).
            /// </summary>
            public const string SlugDaTonTai = "eCommerce:Catalog:0026";

            /// <summary>
            /// **Không sinh được slug** từ tên sản phẩm (`CAT-003` **ADR-04**).
            /// 📌 Hàm sinh slug **thuần** trả **chuỗi rỗng** khi tên không còn ký tự dùng được (ví dụ toàn
            /// ký tự đặc biệt / emoji) — nó **cố ý KHÔNG bịa** ra `slug-{guid}` vì `Guid` **không có giá trị SEO**
            /// (ADR-04) và sẽ **che** dữ liệu vào vô nghĩa. ⇒ Chỗ gọi phải **quyết định**: hoặc người dùng
            /// **tự nhập slug**, hoặc sản phẩm **chưa có URL công khai**. Trả mã riêng để client biết cần nhập tay.
            /// </summary>
            public const string KhongSinhDuocSlug = "eCommerce:Catalog:0027";

            // ── Media (`CAT-003`) — dải 0028–0031 ──

            /// <summary>🚨 **TỆP TẢI LÊN KHÔNG PHẢI ẢNH** (magic bytes không khớp) — `ADR-13`.
            /// 📌 **VÌ SAO MÃ RIÊNG (không dùng mã chung "dữ liệu vào không hợp lệ"):** chỉ dẫn cho người
            /// dùng **rất cụ thể** — *"tệp này không phải ảnh, hãy chọn tệp khác"* — và cho **nhật ký bảo mật**
            /// biết đây **có thể là ý đồ tấn công** (đổi đuôi/tên tệp), **khác hẳn** *"thiếu trường bắt buộc"*.
            /// 🔒 Trả mã riêng còn giúp **phân biệt** khi điều tra: một chuỗi `0028` tăng vọt là **dấu hiệu**.</summary>
            public const string TepKhongPhaiAnh = "eCommerce:Catalog:0028";

            /// <summary>**ẢNH VƯỢT KÍCH THƯỚC TỐI ĐA** cho phép.</summary>
            public const string AnhQuaKichThuoc = "eCommerce:Catalog:0029";

            /// <summary>**SẢN PHẨM ĐÃ CÓ ẢNH CHÍNH** (`ADR-12`: tối đa MỘT `IsPrimary` cho mỗi sản phẩm).
            /// 📌 Hướng dẫn: *"bỏ đánh dấu ảnh chính hiện tại trước"* — khác hẳn *"tệp không hợp lệ"*.</summary>
            public const string DaCoAnhChinh = "eCommerce:Catalog:0030";

            /// <summary>**KHÔNG TÌM THẤY** ảnh theo Id (hoặc ảnh thuộc sản phẩm khác).</summary>
            public const string KhongTimThayAnh = "eCommerce:Catalog:0031";
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

        /// <summary>
        /// 🚨 **Khoá TOÀN TENANT cho cây danh mục** (`CAT-002` **D8**) — dùng cho **mọi** thao tác có thể
        /// tạo **chu trình** hoặc làm `Path` lệch cây thật (di chuyển nhánh, xoá nút).
        ///
        /// **Vì sao khoá theo TENANT chứ không theo NÚT** (`CAT-002` §8.2): hai người cùng di chuyển **hai
        /// nhánh khác nhau** vẫn có thể tạo chu trình (A→B và B→A) — khoá theo nút **không** chặn được ca đó.
        /// Đánh đổi: cây thành tài nguyên **tuần tự hoá toàn tenant**, chấp nhận được vì di chuyển cây rất ít.
        ///
        /// ⚠️ Bất biến *"cây không có chu trình"* là bất biến **DB KHÔNG bảo vệ được** (`CAT-002` §8.1 #1)
        /// ⇒ khoá này **không** phải "tối ưu hiệu năng" mà là **hàng rào đúng nghĩa**.
        ///
        /// ⚠️ Tên khoá **KHÔNG** chứa tiền tố `ecommerce:` (tiền tố do `AbpDistributedLockOptions.KeyPrefix`
        /// cấp — `XC-003` §5.4; tự thêm sẽ thành **tiền tố đôi**, đúng lỗi C13 đã gặp).
        /// </summary>
        public static string CategoryTree(Guid? tenantId)
            => $"catalog:category-tree:{tenantId?.ToString() ?? "null"}";

        /// <summary>
        /// 🚨 **Khoá theo SẢN PHẨM cho bước SINH TỔ HỢP biến thể** (`CAT-002` D7/BR-015, §8.2).
        /// Khoá theo **sản phẩm** (không theo tenant) **có chủ ý**: hai sản phẩm khác nhau vẫn sinh **song song**;
        /// chỉ hai request trên **cùng một** sản phẩm mới phải tuần tự — nếu không, cả hai cùng chèn
        /// `AttributeHash` giống nhau ⇒ một request nhận **lỗi unique khó hiểu** từ DB.
        /// ⚠️ Tên khoá **KHÔNG** chứa tiền tố `ecommerce:` (`XC-003` §5.4 — tránh **tiền tố đôi**).
        /// </summary>
        public static string VariantCombination(Guid? tenantId, Guid productId)
            => $"catalog:variant-combination:{tenantId?.ToString() ?? "null"}:{productId}";

        /// <summary>Sửa/xoá **định nghĩa thuộc tính** trong khi có người đang sinh tổ hợp (`CAT-002` §8.2).</summary>
        public static string AttributeDefinition(Guid? tenantId)
            => $"catalog:attribute-definition:{tenantId?.ToString() ?? "null"}";

        // 📌 Khoá liên quan tới kênh bán (refresh token, đẩy tồn kho) do CHÍNH module ChannelHub
        // sở hữu — xem modules/ChannelHub/ChannelHub.Domain/ChannelHubConsts.cs.
        // Không khai báo lại ở đây để tránh hai nguồn sự thật cho cùng một khoá lock.
    }
}
