using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **`/health-status` CỦA TIẾN TRÌNH WEB** — để **THỐNG NHẤT ĐIỂM ĐO** giữa Host và Web.
///
/// **VÌ SAO CẦN (lỗi thật đã trả giá — `web-001` §70):** `tools/wait-host.ps1` và mọi `ChoHost` trong probe
/// kiểm `<Url>/health-status` = `200`. Endpoint đó **chỉ có trên Host** ⇒ khi chờ **Web** thì phép chờ
/// **hỏi một điểm đo KHÔNG TỒN TẠI** ⇒ **luôn hết hạn** dù Web đã lên (log chứng minh:
/// `Now listening on: http://localhost:5100` + `Application started`).
/// 📌 Hậu quả đã xảy ra: **hai vòng** (79 · 83) bị chặn, và tài liệu ghi **sai** là *"Web không lên"*.
/// 📌 **Bài học #14:** *kiểm ĐIỂM ĐO có tồn tại trước khi tin nó* — và cách sửa rẻ nhất là **một điểm đo
/// cho mọi tiến trình**, thay vì mỗi probe phải nhớ *"Web thì kiểm đường dẫn khác"*.
///
/// 🔒 **QUY TẮC `XC-005` BR-012:** endpoint này **KHÔNG** trả chi tiết lỗi (không nêu tên bảng/chuỗi kết nối/stack).
/// Nó chỉ nói **tiến trình còn sống và phục vụ được HTTP** — đúng mức cần cho phép chờ.
/// 🔐 `[AllowAnonymous]`: probe phải kiểm được **trước khi đăng nhập** (nếu cần đăng nhập thì không dùng làm điểm chờ được).
/// </summary>
[AllowAnonymous]
[Route("health-status")]
public class HealthStatusController : Controller
{
    /// <summary>`GET /health-status` ⇒ **`200`** + `{"status":"Healthy"}`. Không đọc DB, không lộ chi tiết.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy" });
}
