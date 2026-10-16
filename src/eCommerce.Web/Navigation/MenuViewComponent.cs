using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;

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
/// 🔑 **Quyết định hiện/ẩn nằm ở <see cref="BoLocDieuHuong.Loc"/> (hàm THUẦN, đã có unit test)** —
/// lớp này **chỉ** dựng HTML.
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

            // 🚨 **MỤC CHA (NHÓM) — RENDER HAI TẦNG** (`WEB-001`, lượt 20).
            //
            // 📌 **LỖI SẢN PHẨM ĐÃ ĐO ĐƯỢC:** trước lượt này, `MenuDieuHuong` **có** khai mục con và
            // `BoLocDieuHuong` **có** lọc theo quyền cho con — nhưng **lớp này chỉ render `muc.Url`**
            // ⇒ mục con **KHÔNG BAO GIỜ hiện**. 🔒 Và triệu chứng là: nhóm "Vận hành" render thành
            // một liên kết `href="#"` **bấm vào không đi đâu** ⇒ người dùng **KHÔNG có lối vào**
            // `/HanMucKenh` và `/DoiSoatThanhToan` — đúng nợ *"màn hình vô hình vì thiếu lối vào"*.
            // ⚠️ Nó **không có lỗi nào**: trang `200`, menu render, chỉ là **thiếu** hai mục.
            // 🔑 Đây là lý do phép kiểm `E3`/`E4` tồn tại: *"menu có nhóm"* **không** suy ra *"nhóm có mục con"*.
            if (muc.LaNhom)
            {
                // Nhóm **KHÔNG** phải liên kết ⇒ dùng `span` (không phải `a href="#"`).
                // 📌 `href="#"` là **liên kết chết**: bấm vào nhảy về đầu trang và trông như hỏng.
                var span = new TagBuilder("span");
                span.Attributes["class"] = "nav-link fw-semibold disabled";
                span.Attributes["role"] = "heading";
                span.Attributes["aria-level"] = "2";
                span.Attributes["title"] = muc.MoTa ?? string.Empty;

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

        return new HtmlContentViewComponentResult(nav);
    }
}
