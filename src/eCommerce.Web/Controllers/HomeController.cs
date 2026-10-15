using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// Trang chủ khu vực quản trị.
///
/// 🔐 `[Authorize]` ở **cấp CLASS** — đúng quy ước #4 của dự án. Nhờ đó người chưa đăng nhập bị
/// **chuyển hướng về `/Account/Login`** thay vì nhìn thấy trang.
/// ⚠️ Đây **không** phải biện pháp bảo mật cho dữ liệu: mọi app service phía sau vẫn tự kiểm quyền
/// (ẩn nút trên UI **không** được coi là bảo mật — `XC-004` D6).
/// </summary>
[Authorize]
public class HomeController : Controller
{
    public IActionResult Index() => View();
}
