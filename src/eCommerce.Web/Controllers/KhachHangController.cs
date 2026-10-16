using eCommerce.Customers;
using System.Globalization;
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
    /// `GET /KhachHang/NhatKy` — 🚨 **MÀN HÌNH NHẬT KÝ KIỂM TOÁN XUẤT DỮ LIỆU CÁ NHÂN** (`CUS-001` §23).
    ///
    /// 🔑 **VÌ SAO CẦN MÀN HÌNH NÀY:** ở §22 tôi đã xây bảng `CusDataExportLogs` — 🔒 nhưng một **bằng chứng
    /// không ai đọc được** thì **không phục vụ** câu hỏi nào. 📌 Nợ đã ghi ở §22.6 #1 (*"người kiểm toán phải
    /// TRUY VẤN SQL"*) — controller này đóng nợ đó.
    ///
    /// ⚠️ **KHÔNG PARSE NGÀY Ở ĐÂY:** 🔑 tham số nhận **CHUỖI** rồi để **model binder + `TryParse`** xử lý.
    /// 📌 Nếu nhận `DateTime?` thẳng thì một giá trị rác trên URL sẽ làm **`ModelState` không hợp lệ** và
    /// ⚠️ trang trả **`400`** — tức người kiểm toán **gõ sai một ký tự** là **mất cả màn hình**, thay vì
    /// chỉ **bỏ qua bộ lọc**. 🔒 Với một màn hình **kiểm toán**, *"hiện nhiều hơn yêu cầu"* **an toàn hơn**
    /// *"không hiện gì"*.
    /// </summary>
    public async Task<IActionResult> NhatKy(Guid? customerId = null, string? tuNgay = null, string? denNgay = null, string? nguoiXuat = null)
    {

        // 🚨 **NGAY TREN FORM LA NGAY UTC — KHONG DUOC `ToUniversalTime()`.**
        // (Loi THẬT, đo được ở `CUS-001` §24.5.)

        // ⚠️ **CHUYEN GI XAY RA:** `DateTime.TryParse("2020-01-02")` cho `Kind = Unspecified`, va
        // `ToUniversalTime()` tren `Unspecified` **coi no la GIO DIA PHUONG** roi **TRU offset**.
        // 📌 May chu o `SE Asia Standard Time` (UTC+7) ⇒ `2020-01-02 00:00` thanh **`2020-01-01 17:00` UTC**
        // ⇒ 🔒 **bo loc dich SOM 7 TIENG**:
        // <list type="bullet">
        // <item>`tuNgay` **rong hon** ⇒ **LOT** nhung dong le ra phai bi **loai**;</item>
        // <item>`denNgay` **hep hon** ⇒ **MAT** nhung dong le ra phai **hien**.</item>
        // </list>
        // 🔑 **Va day la loai loi TE NHAT voi mot man hinh KIEM TOAN:** no **khong bao loi**, ket qua
        // **trong nhu hop ly**, va 📌 nguoi kiem toan **ket luan sai ve bang chung** (⚠️ *"khong ai xuat
        // trong ngay do"* khi thuc te **CO**).

        // ✅ **CACH DUNG:** nhan ngay **nhu ngay UTC** — 🔑 nhan (label) da ghi ro *"(UTC)"*, nen
        // 📌 **khong** co gi phai chuyen doi. ➕ Va nhat quan voi `XuatLucUtc` (cung UTC).

        // 📌 `DateTimeStyles.AssumeUniversal | AdjustToUniversal` lam dieu do **tuong minh** — ⚠️ chu
        // **khong** de `Kind = Unspecified` roi **tin** rang no dung: 🔒 `Unspecified` la nguon cua
        // chinh loi nay.

        const DateTimeStyles KieuUtc = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        var tu = DateTime.TryParse(tuNgay, CultureInfo.InvariantCulture, KieuUtc, out var t) ? t : (DateTime?)null;
        var den = DateTime.TryParse(denNgay, CultureInfo.InvariantCulture, KieuUtc, out var d) ? d : (DateTime?)null;

        var ketQua = await _khachHang.GetNhatKyXuatDuLieuAsync(
            customerId: customerId,
            // 📌 **KHONG** `ToUniversalTime()` o day — xem chu thich `KieuUtc` o tren.
            tuNgayUtc: tu,
            denNgayUtc: den,
            nguoiXuat: nguoiXuat);

        ViewBag.TuNgay = tuNgay;
        ViewBag.DenNgay = denNgay;
        ViewBag.NguoiXuat = nguoiXuat;
        ViewBag.CustomerId = customerId;

        // 🔑 **DANH SACH NGUOI XUAT THAT** — 📌 de man hinh dung **dropdown** thay vi o nhap tu do.
        // ⚠️ O nhap tu do se khien nguoi kiem toan **go sai mot ky tu** va 🔒 nhan duoc **0 dong** —
        // 📌 ma voi man hinh **kiem toan** thi *"0 dong"* bi doc thanh *"khong ai xuat"*.
        // 🔑 Day cung la ly do toi chon **khop CHINH XAC** o app service: gia tri den tu **du lieu that**,
        // chu khong phai tu tri nho nguoi dung.
        ViewBag.CacNguoiXuat = await _khachHang.GetDanhSachNguoiXuatNhatKyAsync(customerId);

        return View(ketQua);
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
