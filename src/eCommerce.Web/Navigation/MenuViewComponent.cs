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
