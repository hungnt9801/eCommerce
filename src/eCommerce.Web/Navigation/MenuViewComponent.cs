using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Extensions;

namespace eCommerce.Web.Navigation;

/// <summary>
/// 🚨 **VIEW COMPONENT MENU ĐIỀU HƯỚNG — LỌC THEO QUYỀN** (`WEB-001`).
///
/// **VÌ SAO LÀ VIEW COMPONENT (C#) CHỨ KHÔNG PHẢI PARTIAL `.cshtml` — đã ĐO ĐƯỢC, không phải sở thích:**
/// phiên bản đầu dùng `Views/Shared/_Menu.cshtml` + `&lt;partial name="_Menu" /&gt;`. Kết quả **đo trên
/// HTML thật**: trang chủ trả **`200`**, log **`0` lỗi**, nhưng **không có menu nào** và **không có cảnh báo**.
/// 📌 `&lt;partial&gt;` **im lặng render rỗng** khi không tìm thấy view, còn view `.cshtml` trong dự án này
/// **không** nằm trong thư mục output (`bin/`) — nên câu hỏi *"Razor có biên dịch tệp mới không"* trở thành
/// **điểm mù**: sai thì **không có triệu chứng nào** ngoài "menu biến mất".
/// 🔒 ViewComponent là **mã C#** ⇒ **biên dịch là biết**, không có điểm mù. Trả `IHtmlContent` dựng bằng
/// `TagBuilder` ⇒ **không** phụ thuộc thêm một tệp `.cshtml` nào. 📌 Đây cũng **đúng khuôn ABP**: theme
/// dựng menu bằng view component (`Menu_`), ta chỉ thay phần **quyết định hiện/ẩn**.
///
/// 🔑 **Quyết định hiện/ẩn nằm ở <see cref="BoLocDieuHuong.Loc"/> (hàm THUẦN, đã có unit test)** — lớp này
/// **chỉ** dựng HTML.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **BÀI HỌC LỚN NHẤT CỦA LỚP NÀY — VÀ LÝ DO NÓ ĐƯỢC TÁCH THÀNH HÀM THUẦN (`DungHtml`):**
///
/// Ở **lượt 19**, `MenuDieuHuong` **đã** khai mục con **và** `BoLocDieuHuong` **đã** lọc quyền cho con —
/// nhưng lớp này **chỉ render `muc.Url`** ⇒ 🚨 **mục con KHÔNG BAO GIỜ hiện**.
///
/// 📌 **Triệu chứng (đo được ở lượt 20, khi có phiên đăng nhập thật):** menu render, nhóm **"Vận hành"**
/// **có mặt**, nhưng nó là **`href="#"`** — một **liên kết CHẾT** bấm vào không đi đâu ⇒ 🔒 người dùng
/// **KHÔNG có lối vào** `/HanMucKenh` và `/DoiSoatThanhToan`.
/// ⚠️ **Và không có lỗi nào:** trang `200`, menu render, log sạch — chỉ là **thiếu** hai mục.
///
/// 🔑 **VÌ SAO NÓ LỌT QUA 1101 UNIT TEST + 11 CA PROBE:** không test nào **chạm tới lớp render** — mọi phép
/// kiểm chỉ soi **dữ liệu menu** (`MenuDieuHuong`, `BoLocDieuHuong`) chứ không soi **HTML sinh ra**.
/// ⇒ ✅ **Cách chặn tái diễn:** tách việc dựng HTML thành **hàm THUẦN** `DungHtml` để **unit test gọi trực
/// tiếp**, và mỗi **mục con** + mỗi **liên kết chết** đều có ca kiểm khoá lại.
/// 📌 Đây đúng khuôn đã trả giá ở `CAT-003` (`SinhSlug`) và `SAL-005` (`LuatWatermark`):
/// **tách lõi quyết định ra hàm thuần ⇒ có test cho MỌI nhánh.**
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class MenuViewComponent : ViewComponent
{
    private readonly DichVuDieuHuong _dieuHuong;

    public MenuViewComponent(DichVuDieuHuong dieuHuong)
    {
        _dieuHuong = dieuHuong;
    }

    public IViewComponentResult Invoke()
    {
        var mucMenu = _dieuHuong.LayMenuAsync();

        // 🔒 Không có mục nào ⇒ trả **RỖNG THẬT** (`HtmlString.Empty`), không phải khung rỗng.
        // 📌 Người chưa đăng nhập / không có quyền nào **không được** thấy một thanh điều hướng trống —
        // trông như "hệ thống thiếu menu" và làm người ta đi tìm lỗi không tồn tại.
        if (mucMenu.Count == 0)
        {
            return new HtmlContentViewComponentResult(HtmlString.Empty);
        }

        return new HtmlContentViewComponentResult(DungHtml(mucMenu));
    }

    /// <summary>
    /// 🔑 **DỰNG HTML MENU — HÀM THUẦN, KHÔNG PHỤ THUỘC DI/HTTP** ⇒ **unit test gọi trực tiếp được**.
    ///
    /// 📌 **VÌ SAO TÁCH RA (đây là điều chặn lỗi tái diễn, không phải chi tiết kỹ thuật):** ở lượt 19, lỗi
    /// *"mục con không bao giờ render"* sống sót qua **1101 unit test** vì **không** test nào gọi được
    /// lớp render (nó là `ViewComponent`, cần `ViewComponentContext`). 🔒 Hàm thuần này phá bỏ rào đó:
    /// test **gọi thẳng**, khẳng định **HTML thật**, không cần host.
    /// </summary>
    /// <param name="mucMenu">Danh sách **ĐÃ LỌC QUYỀN** (từ <see cref="BoLocDieuHuong.Loc"/>).</param>
    public static IHtmlContent DungHtml(IReadOnlyList<MucDieuHuong> mucMenu)
    {
        ArgumentNullException.ThrowIfNull(mucMenu);

        // 🔒 **DANH SÁCH RỖNG ⇒ TRẢ RỖNG THẬT** — và đây là **bất biến của CHÍNH hàm thuần**, không chỉ
        // của `Invoke()`. 📌 Lý do: nếu `DungHtml([])` vẫn dựng `<nav>` rỗng thì **hai** đường gọi cho **hai**
        // kết quả khác nhau cho cùng một đầu vào — 🔑 và chỗ gọi mới (không qua `Invoke()`) sẽ **âm thầm**
        // tạo ra thanh điều hướng rỗng. ⚠️ Thanh rỗng làm người dùng tưởng *"hệ thống thiếu menu"*.
        // (Ca `Menu_RONG__khong_render_gi` trong `MenuRenderTests` đã **bắt** sự bất nhất quán này.)
        if (mucMenu.Count == 0)
        {
            return HtmlString.Empty;
        }

        // 🔒 `aria-label` là **neo đo được** cho probe: nhờ nó mà phép kiểm HTTP phân biệt được
        // *"menu có render"* với *"trang chủ tình cờ chứa chữ Đơn hàng ở chỗ khác"*.
        var nav = new TagBuilder("nav");
        nav.Attributes["class"] = "mb-3";
        nav.Attributes["aria-label"] = "Điều hướng quản trị";

        var ul = new TagBuilder("ul");
        ul.Attributes["class"] = "nav nav-pills flex-wrap gap-1";

        foreach (var muc in mucMenu)
        {
            var li = new TagBuilder("li");
            li.Attributes["class"] = "nav-item";

            // 🚨 **MỤC CHA (NHÓM) — RENDER HAI TẦNG.**
            // 📌 Nhóm **KHÔNG** phải liên kết ⇒ dùng `span` (⚠️ **KHÔNG** `a href="#"`: đó là **liên kết chết**,
            // bấm vào nhảy về đầu trang và trông như hỏng).
            if (muc.LaNhom)
            {
                var span = new TagBuilder("span");
                span.Attributes["class"] = "nav-link fw-semibold disabled";
                span.Attributes["role"] = "heading";
                span.Attributes["aria-level"] = "2";

                if (!string.IsNullOrWhiteSpace(muc.MoTa))
                {
                    span.Attributes["title"] = muc.MoTa;
                }

                span.InnerHtml.Append(muc.Nhan);
                li.InnerHtml.AppendHtml(span);

                var ulCon = new TagBuilder("ul");
                ulCon.Attributes["class"] = "nav flex-column ms-3";

                foreach (var con in muc.Con)
                {
                    var liCon = new TagBuilder("li");
                    liCon.Attributes["class"] = "nav-item";

                    var aCon = new TagBuilder("a");
                    aCon.Attributes["class"] = "nav-link";
                    aCon.Attributes["href"] = con.Url;

                    if (!string.IsNullOrWhiteSpace(con.MoTa))
                    {
                        aCon.Attributes["title"] = con.MoTa;
                    }

                    aCon.InnerHtml.Append(con.Nhan);
                    liCon.InnerHtml.AppendHtml(aCon);
                    ulCon.InnerHtml.AppendHtml(liCon);
                }

                li.InnerHtml.AppendHtml(ulCon);
                ul.InnerHtml.AppendHtml(li);
                continue;
            }

            var a = new TagBuilder("a");
            a.Attributes["class"] = "nav-link";
            a.Attributes["href"] = muc.Url;

            if (!string.IsNullOrWhiteSpace(muc.MoTa))
            {
                a.Attributes["title"] = muc.MoTa;
            }

            a.InnerHtml.Append(muc.Nhan);
            li.InnerHtml.AppendHtml(a);
            ul.InnerHtml.AppendHtml(li);
        }

        nav.InnerHtml.AppendHtml(ul);

        return nav;
    }
}
