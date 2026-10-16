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
        // về một trang. 📌 Người vận hành cần **tra cứu**, không cần **xuất toàn bộ** — muốn xuất thì phải
        // đi qua **một hồ sơ cụ thể** (xem `XuatDuLieu` ngay dưới): 🔑 xuất **từng người** là việc luật cho
        // phép và có kiểm soát; xuất **cả bảng** thì **không** ai kiểm soát được.
        var ds = await _khachHang.GetDanhSachAsync(
            keyword: string.IsNullOrWhiteSpace(tuKhoa) ? null : tuKhoa.Trim(),
            toiDa: 100);

        ViewBag.TuKhoa = tuKhoa;

        return View(ds);
    }

    /// <summary>
    /// `GET /KhachHang/XuatDuLieu/{id}` — 🚨 **XUẤT DỮ LIỆU CÁ NHÂN CỦA MỘT KHÁCH** (Luat **91/2025/QH15**).
    ///
    /// **VÌ SAO LÀ `GET` NHƯNG VẪN AN TOÀN:** đây là thao tác **CHỈ ĐỌC** (không đổi trạng thái) ⇒ 📌 `GET` là
    /// đúng ngữ nghĩa HTTP. 🔒 **Và quyền nằm ở `CustomerAppService.XuatDuLieuCaNhanAsync`** (`Customers.Manage`)
    /// — lớp bảo vệ **thật**, không phải ở đây.
    ///
    /// ⚠️ **TẢI VỀ DƯỚI DẠNG TỆP JSON** (`File`) chứ **không** render vào trang: 🔑 một trang HTML chứa PII sẽ
    /// bị **cache**, **in**, **chụp màn hình**, và **ở lại** trong lịch sử trình duyệt. 📌 Một tệp tải về thì
    /// người dùng **biết mình đang giữ gì** — và 🔒 đó đúng là điều luật muốn (chủ thể **kiểm soát** bản sao).
    /// ➕ Tên tệp có **mã khách** để phân biệt khi xuất nhiều người.
    /// </summary>
    /// <param name="id">Khách cần xuất.</param>
    public async Task<IActionResult> XuatDuLieu(Guid id)
    {
        var duLieu = await _khachHang.XuatDuLieuCaNhanAsync(id);

        // 🔑 `JsonSerializerOptions` có `WriteIndented` ⇒ 🔒 tệp **đọc được bằng mắt** (chu the du lieu khong
        // phai lap trinh vien). ⚠️ Va `Encoder` mac dinh se escape tieng Viet thanh `\uXXXX` ⇒ 📌 kho doc;
        // dung `UnsafeRelaxedJsonEscaping` de **giu nguyen dau** (day la TEP TAI VE, khong phai HTML ⇒ an toan).
        var json = System.Text.Json.JsonSerializer.Serialize(duLieu, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // 📌 Tien to `xuat-du-lieu-ca-nhan-` de nguoi dung **nhan ra ngay** day la tai lieu mat.
        var tenTep = $"xuat-du-lieu-ca-nhan-{id:N}.json";

        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", tenTep);
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
