using eCommerce.PaymentHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **ĐỐI SOÁT THANH TOÁN TRÊN UI** (`REC-001` mục 7 của GĐ1) — tra cứu giao dịch + **truy vấn trạng thái**
/// từ cổng thanh toán (`querydr`).
///
/// 🔑 **VÌ SAO TRANG NÀY LÀ ĐIỀU KIỆN ĐỂ "ĐỐI SOÁT" CÓ NGHĨA:** `IPaymentAppService` **đã có**
/// (`GetDanhSachAsync`, `GetTheoMaAsync`, `PostTruyVanTrangThaiAsync`, `HoanTienAsync`) và **module đã nạp ở
/// Host**. ⚠️ Nhưng **chưa có màn hình nào gọi nó** ⇒ người vận hành **không có cách nào** trả lời câu hỏi
/// *"giao dịch này cổng nói gì?"* ngoài việc gọi API bằng tay. 📌 Cùng loại nợ đã gặp ở `INV-003` §16.6:
/// **đường ghi/đường đọc đúng mà KHÔNG có lối vào thì không giải quyết được vấn đề nó sinh ra để giải quyết.**
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4); lớp bảo vệ dữ liệu thật là `[Authorize(Payments.Default)]`
/// trên `IPaymentAppService` ⇒ người thiếu quyền vẫn nhận `403` **từ app service**.
/// </summary>
[Authorize]
public class DoiSoatThanhToanController : Controller
{
    private readonly IPaymentAppService _thanhToan;

    public DoiSoatThanhToanController(IPaymentAppService thanhToan)
    {
        _thanhToan = thanhToan;
    }

    /// <summary>
    /// `GET /DoiSoatThanhToan` — danh sách giao dịch, lọc **tuỳ chọn** theo mã tham chiếu và trạng thái.
    ///
    /// 🔒 **KHÔNG lọc gì ⇒ hiện tối đa 50 giao dịch gần nhất** (mặc định của app service). 📌 Đây là **cố ý**:
    /// một trang **rỗng** khi mới mở khiến người dùng tưởng **tính năng hỏng**, còn một trang **quá dài**
    /// thì **không ai đọc**. ⚠️ Và **KHÔNG** tự đặt `trangThai = Pending`: mặc định lọc sẽ **ẩn** đúng những
    /// giao dịch bất thường mà người vận hành đang tìm.
    /// </summary>
    public async Task<IActionResult> Index(string? maThamChieu = null, string? trangThai = null)
    {
        // 📌 Truyền `null` (không phải chuỗi rỗng) khi người dùng để trống — nếu truyền `""` thì bộ lọc
        // `LIKE '%%'` vẫn khớp mọi thứ **nhưng** ý định "không lọc" trở nên **mơ hồ** trong log/DB.
        var ds = await _thanhToan.GetDanhSachAsync(
            merchantReference: string.IsNullOrWhiteSpace(maThamChieu) ? null : maThamChieu.Trim(),
            trangThai: string.IsNullOrWhiteSpace(trangThai) ? null : trangThai.Trim(),
            toiDa: 50);

        ViewBag.MaThamChieu = maThamChieu;
        ViewBag.TrangThai = trangThai;

        return View(ds);
    }

    /// <summary>
    /// `POST /DoiSoatThanhToan/TruyVanTrangThai` — 🚨 **gọi cổng thanh toán** để lấy trạng thái thật (`querydr`).
    ///
    /// 🔑 **VÌ SAO ĐÂY LÀ HÀNH ĐỘNG QUAN TRỌNG NHẤT CỦA TRANG:** đối soát nội bộ **chỉ** so được **hai nguồn
    /// của chính mình** (DB vs sổ cái). 🔒 Chỉ `querydr` mới trả lời được câu hỏi **duy nhất quan trọng**:
    /// *"cổng thanh toán nói gì?"* — mà đó là **nguồn sự thật** về việc tiền đã vào hay chưa.
    /// ⚠️ Nếu không có nút này thì mọi "đối soát" chỉ là **tự đối chiếu với chính mình**.
    ///
    /// 🚨 `BusinessException` **được bắt ở đây** — **KHÔNG** phải nuốt guard: `PostTruyVanTrangThaiAsync` **NÉM**
    /// khi cổng không trả lời / không cấu hình credential (đúng quy ước #11). Ở **UI MVC**, để exception nổi lên
    /// ⇒ `AbpExceptionFilter` biến thành **`HTTP 500` + trang lỗi chung** ⇒ người vận hành **không biết** là
    /// *"cổng chưa cấu hình"* hay *"hệ thống hỏng"*. 📌 Ca này **đã đo được** ở `REC-001` §18.
    /// 🔒 Bắt rồi **hiện mã + thông điệp** rồi **dừng** (không ghi gì) — khác hẳn **nuốt** rồi đi tiếp như thành công.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TruyVanTrangThai(Guid id)
    {
        try
        {
            await _thanhToan.PostTruyVanTrangThaiAsync(id);
            TempData["ThongBaoThanhToan"] = "Đã truy vấn trạng thái từ cổng thanh toán.";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            // ⚠️ Thông điệp mặc định của ABP khi filter che mã là "An internal error occurred…" ⇒ thay bằng
            // chỉ dẫn **cụ thể** (thường là: cổng chưa cấu hình credential / chưa bật gọi thật).
            TempData["LoiThanhToan"] = $"Không truy vấn được: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Cổng thanh toán chưa cấu hình hoặc không trả lời (kiểm `PaymentHub` trong appsettings)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index), new { maThamChieu = (string?)null });
    }
}
