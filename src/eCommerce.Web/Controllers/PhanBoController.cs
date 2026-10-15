using eCommerce.Application.Contracts.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG MẶT TIỀN ĐẦU TIÊN — PHÂN BỔ TỒN THEO KÊNH** (`INV-003` · `WEB-001`).
///
/// **VÌ SAO CHỌN TRANG NÀY LÀM TRANG ĐẦU TIÊN:**
///   • dữ liệu của nó **đã được kiểm chứng ở tầng API** (`inv-001` §40 · §42) ⇒ nếu trang hiển thị **sai**,
///     thì lỗi **chắc chắn** nằm ở **tầng hiển thị**, không phải ở nghiệp vụ — **phép đo tách bạch** được;
///   • nó trả lời câu hỏi **kinh doanh thật**: *"kênh nào đang bán gì, đang giữ bao nhiêu, đã trả bao nhiêu,
///     và **giữ ở kho nào**"* — thứ mà trước vòng 40 **không nhìn thấy được ở đâu**;
///   • nó **có ca rỗng** (chưa có đơn nào) ⇒ buộc trang phải xử lý **trạng thái không có dữ liệu**
///     (*một trang chỉ đúng khi có dữ liệu là một trang **chưa** kiểm chứng*).
///
/// 🔐 `[Authorize]` ở **cấp class** — đúng quy ước #4 (giống `HomeController`): người chưa đăng nhập bị chuyển hướng
/// về `/Account/Login` **thay vì** nhìn thấy dữ liệu. ⚠️ **KHÔNG** coi đây là bảo mật dữ liệu: app service phía sau
/// vẫn **tự kiểm quyền** (`XC-004` D6 — *ẩn nút trên UI không phải là bảo mật*).
/// </summary>
[Authorize]
public class PhanBoController : Controller
{
    private readonly IDonHangAppService _donHang;

    public PhanBoController(IDonHangAppService donHang)
    {
        _donHang = donHang;
    }

    /// <summary>
    /// `GET /PhanBo` — bảng phân bổ tồn theo **kênh × SKU**.
    /// 📌 Trang **chỉ đọc**; mọi con số lấy từ **cùng một app service** mà API dùng ⇒ **một nguồn sự thật**.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var duLieu = await _donHang.GetPhanBoTheoKenhAsync();

        return View(duLieu);
    }
}
