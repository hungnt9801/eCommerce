using System.Text.RegularExpressions;

namespace eCommerce.Tests.Web;

/// <summary>
/// 🚨 **STOREFRONT CÔNG KHAI (`WEB-002` · `S8`) — BA BẤT BIẾN KHÔNG ĐƯỢC HỎNG ÂM THẦM.**
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO FILE NÀY TỒN TẠI — ba lỗi đều *"trông như đã xong"* và đều KHÔNG có lỗi biên dịch:**
///
/// ① 🚨 **CÔNG KHAI MÀ GỌI VÀO CHỖ CẦN QUYỀN** — `SitemapController` từng gọi `IPriceListAppService`
///   (app service mang `[Authorize(Pricing.Default)]`) ⇒ khách **chưa đăng nhập** nhận
///   `AbpAuthorizationException` ⇒ endpoint *"công khai"* trả **`HTTP 500`**. 📌 Đây là lỗi **thiết kế**,
///   không phải trục trặc: *mở cửa công khai* và *gọi vào chỗ cần quyền* là **hai điều mâu thuẫn**.
///   🔒 Nếu ai thêm lại một lời gọi như vậy vào trang công khai thì **phải đỏ ở đây**, vì phép đo duy nhất
///   phát hiện được nó là **gọi HTTP thật bằng phiên ẨN DANH** — thứ mà unit test của tầng trong **không** có.
///
/// ② 🚨 **LỘ GIÁ VỐN** — `CostPrice` là dữ liệu **nội bộ**; nó xuất hiện trong HTML công khai nghĩa là
///   đối thủ đọc được biên lợi nhuận của ta bằng một lệnh `curl`. 📌 Cột này **tồn tại** trong `CatProductVariants`
///   nên *"chỉ cần ai đó thêm một trường vào DTO"* là lộ — mà **không có** bước kiểm nào khác chặn việc đó.
///
/// ③ 🚨 **HIỆN HÀNG CHƯA BÁN** — thiếu bộ lọc `Status == Published` thì sản phẩm `Draft`/`Unpublished`
///   (hàng chưa ra mắt, hàng ngừng bán) **lên Internet**; gỡ xuống rồi vẫn còn trong cache của Google.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **CÁCH LÀM — QUÉT NGUỒN, đúng khuôn `AllowlistCongKhaiTests`/`MenuDieuHuongTests`:**
/// `eCommerce.Tests` **cố ý không** tham chiếu `eCommerce.Web` (kéo theo `FrameworkReference` ⇒ `NU1510`
/// cho hai gói bảo mật đã ghim, và `TreatWarningsAsErrors` chặn build — xem chú thích dài ở
/// `AllowlistCongKhaiTests`). ⇒ Quét **tệp nguồn**.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (ghi rõ, không giấu):** test quét **văn bản mã nguồn**, nên nó bảo đảm
/// *"không có chuỗi `CostPrice`/app service cần quyền trong hai tệp này"*, **không** bảo đảm *"hành vi đúng"*.
/// 📌 Việc thứ hai do **E2E** đo trên **HTML thật** (`S8-W1…W5`): khách ẩn danh `200`, `Unpublished` `404`,
/// giá vốn **có giá trị lạ** mà **không** xuất hiện trong HTML.
/// </summary>
public class StorefrontCongKhaiTests
{
    /// <summary>Tệp nguồn quyết định — **đường dẫn tương đối từ gốc repo**.</summary>
    private const string TepDocCongKhai = "src/eCommerce.Web/Storefront/DocSanPhamCongKhai.cs";

    private const string TepSanPham = "src/eCommerce.Web/Controllers/SanPhamController.cs";

    private const string TepSitemap = "src/eCommerce.Web/Controllers/SitemapController.cs";

    /// <summary>
    /// 🛡️ **LƯỚI AN TOÀN CHO CHÍNH PHÉP QUÉT:** nếu tệp bị chuyển chỗ/đổi tên thì mọi khẳng định bên dưới
    /// sẽ **xanh vô nghĩa** (quét một tệp không tồn tại = không tìm thấy chuỗi cấm = "đạt"). 📌 Đúng loại
    /// *"cảm giác an toàn giả"* mà `AllowlistCongKhaiTests` đã cảnh báo bằng ca `Phep_quet_KHONG_rong`.
    /// </summary>
    [Fact]
    public void Phep_quet_KHONG_rong__va_tep_phai_ton_tai()
    {
        var goc = TimGocRepo();

        foreach (var tep in new[] { TepDocCongKhai, TepSanPham, TepSitemap })
        {
            var duongDan = Path.Combine(goc, tep.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(duongDan), $"KHÔNG TÌM THẤY `{tep}` ⇒ phép quét VO NGHIA (đổi tên/chuyển chỗ?).");
            Assert.True(new FileInfo(duongDan).Length > 500, $"`{tep}` quá ngắn ⇒ phép quét VO NGHIA.");
        }

        // 🔒 Neo nội dung: hai tệp phải **thật sự** là thứ ta nghĩ (không phải tệp rỗng cùng tên).
        Assert.Contains("class DocSanPhamCongKhai", Doc(goc, TepDocCongKhai));
        Assert.Contains("[AllowAnonymous]", Doc(goc, TepSanPham));
    }

    /// <summary>
    /// 🚨 **KHÔNG ĐƯỢC LỘ GIÁ VỐN / TỒN KHO** trong đường đọc công khai **và** trong controller công khai.
    /// </summary>
    /// <remarks>
    /// 🔑 Vì sao kiểm **cả hai tệp**: một trường có thể lọt vào HTML bằng hai đường — (a) thêm vào DTO công khai,
    /// (b) controller đọc thẳng entity rồi in ra. 📌 Kiểm một chỗ là bỏ lọt đường còn lại.
    /// ✅ **Chiều ngược (chống "cấm cho có"):** khẳng định tệp CÓ `ProductStatus.Published` — nếu ai đó
    /// xoá bộ lọc thì test này **không** được xanh.
    /// </remarks>
    [Fact]
    public void Duong_doc_cong_khai_KHONG_doc_gia_von_va_ton_kho__va_CO_loc_Published()
    {
        var goc = TimGocRepo();
        var chuoiCam = new[]
        {
            "CostPrice",   // giá vốn — dữ liệu nội bộ
            "OnHand",      // tồn thực tế
            "Reserved",    // tồn đang giữ
            "TonKho",      // tên tiếng Việt của tồn kho (phòng khi ai đó đổi tên kiểu khác)
            "InvItems"     // bảng tồn kho — không có lý do gì để đường đọc công khai chạm tới
        };

        foreach (var tep in new[] { TepDocCongKhai, TepSanPham })
        {
            var noiDung = Doc(goc, tep);

            foreach (var cam in chuoiCam)
            {
                Assert.False(
                    noiDung.Contains(cam, StringComparison.Ordinal),
                    $"🚨 `{tep}` chứa `{cam}` ⇒ DỮ LIỆU NỘI BỘ có thể đã ra trang CÔNG KHAI.\n"
                    + "📌 Nếu thật sự cần hiển thị: (a) chứng minh nó ĐÃ công khai ở nơi khác, hoặc (b) bỏ nó đi.\n"
                    + "🔒 Giá vốn mà lộ ra là biên lợi nhuận của công ty bị đọc bằng một lệnh curl.");
            }
        }

        // ✅ CHIỀU NGƯỢC: bộ lọc "chỉ hàng đang bán" PHẢI CÓ — nếu không, test trên đạt vì lý do SAI
        // (một tệp rỗng hay một tệp chỉ-đọc-giá cũng "không chứa CostPrice").
        //
        // 🚨 **VÀ PHẢI CÓ ĐÚNG MỘT CHỖ ĐỊNH NGHĨA NÓ.** 📌 Đây là bài học **ĐO ĐƯỢC** ngay trong `S8`:
        // bản đầu viết `Status == ProductStatus.Published` ở **ba** truy vấn; phép tiêm *"bỏ bộ lọc"* vào
        // **một** chỗ **không** làm test đỏ, vì hai chỗ kia vẫn còn chuỗi mà test tìm ⇒ 🔑 test đang kiểm
        // *"chữ có tồn tại"*, trong khi câu hỏi thật là *"mọi truy vấn đều lọc"*.
        // ✅ Nay điều kiện được gom vào **một** biểu thức `ChiHangDangBan`; đếm **= 1** + dùng lại **≥ 2 lần**
        // ⇒ tiêm vào định nghĩa **chắc chắn** làm đỏ, và không có chỗ thứ hai để lách.
        var maDoc = Doc(goc, TepDocCongKhai);
        var soLanDinhNghia = Regex.Matches(maDoc, @"Status\s*==\s*ProductStatus\.Published").Count;

        Assert.True(
            soLanDinhNghia == 1,
            $"🚨 Bộ lọc `Status == Published` xuất hiện **{soLanDinhNghia}** lần (mong đợi ĐÚNG 1).\n"
            + "📌 0 lần ⇒ hàng CHƯA BÁN có thể lên Internet. >1 lần ⇒ có chỗ thứ hai để bỏ sót (và test này"
            + " không còn chứng minh được gì). 🔒 Gom về MỘT biểu thức `ChiHangDangBan` dùng chung.");

        Assert.True(
            Regex.Matches(maDoc, @"\bChiHangDangBan\b").Count >= 2,
            "🚨 `ChiHangDangBan` phải được ĐỊNH NGHĨA **và** DÙNG (mong đợi ≥ 2 lần xuất hiện trong mã).");

        // 🔒 Và đường TRA MỘT SẢN PHẨM phải kiểm trạng thái bằng `IsPublished` ở **cả hai** nhánh
        // (slug và SKU) — thiếu một nhánh ⇒ hàng `Unpublished` vẫn xem được qua URL cũ.
        Assert.True(
            Regex.Matches(maDoc, @"\bIsPublished\b").Count >= 2,
            "🚨 Đường tra theo slug/SKU phải kiểm `IsPublished` ở CẢ HAI nhánh (đếm được < 2).");
    }

    /// <summary>
    /// 🚨 **TRANG CÔNG KHAI KHÔNG ĐƯỢC GỌI APP SERVICE CÓ `[Authorize]`.**
    /// </summary>
    /// <remarks>
    /// 📌 Hai app service bị cấm ở đây là **đúng hai** cái đã được dùng trước `S8`:
    /// `IPriceListAppService` (giá — `Pricing.Default`) và `IProductAppService` (sản phẩm — `Products.Default`).
    /// 🔑 Cả hai đều **chạy đúng** khi có phiên đăng nhập — nên lỗi **chỉ** lộ khi khách ẩn danh vào trang,
    /// và triệu chứng là `500` (không phải "thiếu dữ liệu"), tức là *"trang hỏng"* chứ không phải *"trang thiếu"*.
    /// </remarks>
    [Fact]
    public void Trang_cong_khai_KHONG_goi_app_service_can_quyen()
    {
        var goc = TimGocRepo();

        foreach (var tep in new[] { TepSanPham, TepSitemap })
        {
            var noiDung = Doc(goc, tep);

            foreach (var cam in new[] { "IPriceListAppService", "IProductAppService", "PriceListAppService" })
            {
                Assert.False(
                    noiDung.Contains(cam, StringComparison.Ordinal),
                    $"🚨 `{tep}` gọi `{cam}` — app service đó mang `[Authorize]` ⇒ khách CHƯA đăng nhập nhận\n"
                    + "`AbpAuthorizationException` ⇒ `HTTP 500` ở đúng trang cần CÔNG KHAI (lỗi đã xảy ra với `/sitemap.xml`).\n"
                    + "🔒 Phải đọc dữ liệu công khai qua `DocSanPhamCongKhai` (repository, không quyền).");
            }
        }

        // ✅ Neo dương: trang công khai phải dùng **đúng** lớp đọc công khai (nếu không, nó đang đọc thứ khác).
        Assert.Contains("DocSanPhamCongKhai", Doc(goc, TepSanPham));
        Assert.Contains("DocSanPhamCongKhai", Doc(goc, TepSitemap));
    }

    /// <summary>
    /// 🚨 **SITEMAP PHẢI DÙNG SLUG LÀM URL** (`WEB-002` ADR-03 · nợ `CAT-003` §16.8).
    /// </summary>
    /// <remarks>
    /// 🔑 Vì sao đây là bất biến chứ không phải chi tiết: sitemap là **lời khai với máy tìm kiếm** về URL
    /// **chuẩn**. Trỏ vào dạng SKU trong khi `canonical` của trang chi tiết trỏ về dạng slug ⇒ bot nhận
    /// **hai** URL cho **một** sản phẩm, dồn tín hiệu vào chỗ khác với `canonical` — tệ hơn là không có sitemap.
    /// 📌 Trước `S8`, sitemap dùng SKU **và** còn rơi về *"chỉ trang chủ"* khi khách ẩn danh.
    /// </remarks>
    [Fact]
    public void Sitemap_dung_SLUG__khong_dung_SKU_lam_URL()
    {
        var goc = TimGocRepo();
        var sitemap = Doc(goc, TepSitemap);

        Assert.Contains("SinhSlug.Tu", sitemap, StringComparison.Ordinal);
        Assert.Contains("/SanPham/ChiTiet/", sitemap, StringComparison.Ordinal);

        // ⚠️ Không được quay lại dùng SKU làm định danh URL trong sitemap.
        Assert.DoesNotContain(".Sku", sitemap, StringComparison.Ordinal);
    }

    /// <summary>Đọc tệp nguồn **đã bỏ chú thích** — chỉ còn MÃ, đúng thứ chạy thật.</summary>
    /// <remarks>
    /// 🚨 **VÌ SAO PHẢI BỎ CHÚ THÍCH (bài học đo được ngay ở lượt viết test này):** chính các tệp này có
    /// **chú thích** giải thích *"lớp này KHÔNG đọc `CostPrice`/`InvItems`"* — quét thô thì chú thích
    /// **tự tố cáo** chính nó, và ta sẽ **xoá đúng phần tài liệu quan trọng nhất** để làm test xanh.
    /// 📌 Đó là *"test đỏ vì lý do sai"* đổi thành *"tài liệu bị xoá vì lý do sai"* — cả hai đều tệ.
    /// 🔒 Quét **mã** (sau khi bỏ `//`, `///`, `/* … */`) là đúng ngữ nghĩa: chú thích không chạy.
    /// </remarks>
    private static string Doc(string goc, string tep)
    {
        var tho = File.ReadAllText(Path.Combine(goc, tep.Replace('/', Path.DirectorySeparatorChar)));

        // ① Bỏ khối `/* … */` (non-greedy + Singleline — C# không cho lồng nhau).
        var khongKhoi = Regex.Replace(tho, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // ② Bỏ mọi dòng chú thích `//` / `///` (kèm cả `//!`), giữ nguyên số dòng để dễ đọc khi báo lỗi.
        return Regex.Replace(khongKhoi, @"(?m)^[ \t]*//.*$", string.Empty);
    }

    /// <summary>Đi ngược từ thư mục build tới gốc repo (test chạy từ `bin/…`).</summary>
    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (Directory.Exists(Path.Combine(thuMuc.FullName, "src", "eCommerce.Web")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy `src/eCommerce.Web` khi đi lên từ " + AppContext.BaseDirectory);
    }
}
