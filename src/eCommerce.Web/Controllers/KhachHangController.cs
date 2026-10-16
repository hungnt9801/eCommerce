using eCommerce.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **HỒ SƠ KHÁCH HÀNG TRÊN UI** (`CUS-001` mục 5 của GĐ1) — danh sách + **thao tác quản trị**
/// (tạo, nâng cấp khách vãng lai, **chặn / gỡ chặn**).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔒 **ĐÂY LÀ DỮ LIỆU CÁ NHÂN — và đó là điều quyết định thiết kế của trang này:**
///
/// Hồ sơ khách là **dữ liệu cá nhân** (Luật BV dữ liệu cá nhân **91/2025/QH15**). ⇒ Trang **CHỈ** hiện những
/// trường **cần cho công việc** (mã, tên, email, điện thoại, trạng thái chặn) và 🔑 **KHÔNG** hiện:
/// địa chỉ đầy đủ, lịch sử đơn, hay bất kỳ trường nào **không** phục vụ câu hỏi *"khách này là ai, có đang bị chặn không"*.
/// 📌 **Nguyên tắc:** một màn hình hiện **nhiều hơn** mức cần thiết là một **rò rỉ** — kể cả khi người xem
/// **có** quyền. 🔑 Và nó **không** có triệu chứng nào: không lỗi, không log, chỉ là dữ liệu ở nơi không cần.
///
/// ⚠️ **NỢ ĐÃ GHI (`CUS-001`):** app service **CHƯA** có **xuất dữ liệu cá nhân** cho chính chủ
/// (quyền truy cập/xoá theo Luật 91/2025/QH15) và **chưa** có **sổ địa chỉ** (`Address` **không tồn tại**
/// trong `AddressHub`). 📌 Trang này **không** giả vờ có hai thứ đó.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4). 🔒 Lớp bảo vệ thật: `CustomerAppService` mang
/// `[Authorize(Customers.Default)]` ở **cấp class** và `[Authorize(Customers.Manage)]` trên **từng** method ghi
/// ⇒ người **chỉ đọc** mở được trang nhưng **không** chặn/gỡ chặn được. 📌 Đó là **lý do** hai quyền tách nhau
/// (xem chú thích `Permissions.Customers`): rủi ro của *"đọc hồ sơ"* và *"đổi quyền mua hàng của người ta"*
/// **khác hẳn nhau**.
/// </summary>
[Authorize]
public class KhachHangController : Controller
{
    private readonly CustomerAppService _khachHang;

    public KhachHangController(CustomerAppService khachHang)
    {
        _khachHang = khachHang;
    }

    /// <summary>`GET /KhachHang` — danh sách hồ sơ, lọc **tuỳ chọn** theo từ khoá.</summary>
    public async Task<IActionResult> Index(string? tuKhoa = null)
    {
        // ⚠️ `toiDa: 100` là **trần có chủ ý**: hồ sơ khách là dữ liệu cá nhân ⇒ 🔒 **không** kéo cả bảng
        // về một trang. 📌 Người vận hành cần **tra cứu**, không cần **xuất toàn bộ** — và nếu cần xuất thì
        // đó phải là một **tính năng riêng có kiểm soát** (chưa làm — nợ đã ghi).
        var ds = await _khachHang.GetDanhSachAsync(
            keyword: string.IsNullOrWhiteSpace(tuKhoa) ? null : tuKhoa.Trim(),
            toiDa: 100);

        ViewBag.TuKhoa = tuKhoa;

        return View(ds);
    }

    /// <summary>
    /// `POST /KhachHang/Chan` — **chặn** một khách (kèm **lý do BẮT BUỘC**).
    /// 🔒 Lý do bắt buộc là **bất biến ở entity** (`ADR-04`): 6 tháng sau phải biết **vì sao** khách bị chặn.
    /// 🚨 `BusinessException` **được bắt ở đây** để **hiện mã + thông điệp** thay vì `500` chung chung
    /// (xem chú thích dài ở `DoiSoatController.XuLyLech` — **KHÔNG** phải nuốt guard).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chan(Guid id, string lyDo)
    {
        try
        {
            await _khachHang.ChanKhachAsync(id, lyDo);
            TempData["ThongBaoKhach"] = "Đã chặn khách.";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiKhach"] = $"Không chặn được: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Lý do chặn là BẮT BUỘC (audit)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>`POST /KhachHang/MoChan` — **gỡ chặn** (kèm **lý do BẮT BUỘC** — cũng là audit).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoChan(Guid id, string lyDo)
    {
        try
        {
            await _khachHang.MoChanKhachAsync(id, lyDo);
            TempData["ThongBaoKhach"] = "Đã gỡ chặn khách.";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiKhach"] = $"Không gỡ chặn được: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Lý do gỡ chặn là BẮT BUỘC (audit)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// `POST /KhachHang/TaoKhach` — tạo hồ sơ khách mới (cần `Customers.Manage`).
    ///
    /// 🚨 **KHÔNG gửi `IdempotencyKey` — và đó là QUYẾT ĐỊNH, không phải thiếu sót.**
    /// `TaoKhachInput.IdempotencyKey` được mô tả là **BẮT BUỘC ở biên vào** (webhook/hàng đợi) vì ở đó
    /// *"mỗi lần retry là một khách mới"* nếu khoá ngẫu nhiên (`XC-002` **D7**).
    /// 📌 Nhưng đây là **đường tạo TAY của nhân viên**: đã có **người xác nhận** trước khi bấm, và hai lần bấm
    /// là hai **ý định** khác nhau (ví dụ: tạo hai khách cùng tên là **hợp lệ**).
    /// 🔒 Sinh `Guid.NewGuid()` ở đây sẽ **vô hiệu hoá** tác dụng của khoá (mỗi lần là một khoá mới) mà vẫn
    /// **trông như** đã dùng idempotency — 📌 đúng loại *"cơ chế trông có mà thực tế không"*.
    /// ⚠️ Nếu sau này màn hình có **nhập hàng loạt** (import) thì đường đó **phải** sinh khoá **từ dữ liệu
    /// nghiệp vụ** (ổn định, ví dụ `import:<mã dòng>`), **không** ngẫu nhiên.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoKhach(string ten, string? email, string? dienThoai)
    {
        try
        {
            await _khachHang.TaoKhachAsync(new TaoKhachInput
            {
                Name = ten,
                Email = email,
                Phone = dienThoai
                // `IdempotencyKey`: xem chú thích ở trên — cố ý để trống.
            });

            TempData["ThongBaoKhach"] = $"Đã tạo hồ sơ khách '{ten}'.";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiKhach"] = $"Không tạo được hồ sơ: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Kiểm lại các trường bắt buộc (tên khách)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index));
    }
}
