using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG LỖI TỰ LÀM CHO `403` VÀ `404`** (`WEB-001`) — bịt một **điểm mù quan sát** thật của dự án.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO CẦN — và đây là lý do *thật*, không phải "cho đẹp":**
///
/// Mặc định của ABP/ASP.NET Core cho `403` là một trang **trắng** (hoặc trang lỗi chung chung).
/// 📌 Hậu quả **đã đo được** trong dự án này (lỗi `F-403`): người dùng bấm một mục menu, thấy **trang trắng**,
/// và **không ai** — kể cả người dùng, kể cả người vận hành — phân biệt được ba khả năng **rất khác nhau**:
/// <list type="number">
///   <item>họ **thiếu quyền** (cần cấp quyền);</item>
///   <item>màn hình **chưa làm** (cần làm, hoặc menu khai sai);</item>
///   <item>hệ thống **hỏng** (cần sửa mã).</item>
/// </list>
/// 🔒 Và cả ba đều dẫn tới **cùng một triệu chứng**: *"bấm vào không thấy gì"* ⇒ người ta **đi tìm lỗi
/// ở sai chỗ**, hoặc **bỏ qua** một lỗi thật.
///
/// 🚨 **Điều quan trọng nhất của lớp này: nó NÓI RA mã trạng thái và NÓI RÕ ai chịu trách nhiệm.**
/// 📌 Một trang `403` nói *"bạn thiếu quyền X"* thì người dùng **biết phải xin gì**.
/// ⚠️ Nhưng nó **KHÔNG** được tiết lộ **chi tiết nội bộ** (stack trace, tên bảng, `Id`) — 🔒 trang lỗi là
/// chỗ **dễ rò rỉ** nhất vì nó hiện cho **người chưa được phép**.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// ⚠️ **`[AllowAnonymous]` là BẮT BUỘC, và đây là lý do:** chính người **CHƯA** đăng nhập mới là người nhận
/// `401`/`403` ⇒ nếu trang lỗi cũng đòi đăng nhập thì nó **tự chặn chính nó** ⇒ người dùng quay lại **trang trắng**
/// — 📌 tức là **bịt điểm mù này bằng một điểm mù khác**.
/// 🔑 Vì vậy lớp này **CHỈ** trả view tĩnh, **KHÔNG** gọi app service nào ⇒ không có gì để phân quyền.
/// </summary>
[AllowAnonymous]
public class LoiController : Controller
{
    /// <summary>
    /// `GET /Loi/ChanQuyen` (`403`) — **đã xác thực nhưng KHÔNG đủ quyền**.
    /// 📌 Phân biệt rõ với `401`: `403` nghĩa là *"hệ thống BIẾT bạn là ai, nhưng bạn không được phép"* ⇒
    /// việc cần làm là **xin cấp quyền**, **không** phải đăng nhập lại. 🔒 Nhầm hai ca này làm người dùng
    /// **đăng nhập lại vô ích** rồi **vẫn** bị chặn.
    /// </summary>
    public IActionResult ChanQuyen(string? duongDan = null)
    {
        ViewBag.DuongDan = duongDan;

        // 🔒 Đặt mã trạng thái **THẬT** (`403`): một trang `403` trả `200` sẽ làm **mọi** phép kiểm HTTP
        // (và mọi công cụ giám sát) hiểu sai. 📌 Đây là dạng "nói dối" mà chính dự án đang chống.
        Response.StatusCode = StatusCodes.Status403Forbidden;

        return View();
    }

    /// <summary>`GET /Loi/KhongTimThay` (`404`) — **không có màn hình ở địa chỉ này**.
    /// 📌 Việc cần làm: kiểm lại đường dẫn, hoặc (nếu là mục menu) **màn hình chưa được làm** — và
    /// `MenuDieuHuongTests` tồn tại để **không** có mục menu nào trỏ tới trang chưa làm.</summary>
    public IActionResult KhongTimThay(string? duongDan = null)
    {
        ViewBag.DuongDan = duongDan;

        Response.StatusCode = StatusCodes.Status404NotFound;

        return View();
    }
}
