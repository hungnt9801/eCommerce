using eCommerce.Application.Contracts.Checkout;
using eCommerce.Application.Contracts.Inventory;
using eCommerce.Application.Contracts.Sales;
using eCommerce.Customers;
using eCommerce.PaymentHub;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **GIỎ HÀNG → ĐẶT HÀNG TRÊN WEB** (`WEB-001` — mảnh cuối của GĐ1 về phía người dùng).
///
/// **LUỒNG:** `GET /DatHang` (biểu mẫu) ⇒ `POST /DatHang` ⇒
/// `ICheckoutAppService.TinhVaGiuChoAsync` (**giữ chỗ**) ⇒ `IDonHangAppService.TaoTuCheckoutAsync` (**tạo đơn**)
/// ⇒ hiện **kết quả** (số đơn · trạng thái · **tổng tiền từ sổ giải trình**).
///
/// 📌 **Đây là các app service ĐÃ kiểm chứng ở tầng API** (`SAL-001` §24–§25 · `SAL-002`) ⇒ trang này **không**
/// thêm nghiệp vụ mới, chỉ **mở đường cho người dùng**. Nhờ vậy nếu sai thì lỗi nằm ở **tầng UI**, không phải ở kho/pricing.
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4). ⚠️ Không phải bảo mật dữ liệu: app service vẫn tự kiểm quyền.
///
/// 🚨 **BẤT BIẾN QUAN TRỌNG NHẤT CỦA TRANG NÀY — KHOÁ CHECKOUT SINH Ở `GET`, MANG THEO TRONG FORM:**
/// nếu khoá được sinh **lại ở `POST`**, thì khách **bấm hai lần** (hoặc mạng chập chờn rồi bấm lại) ⇒ **HAI ĐƠN**
/// cho **một** lần mua ⇒ **giao hàng hai lần / thu tiền hai lần**.
/// 📌 Sinh ở `GET` + mang theo trong form ⇒ lần gửi thứ hai mang **cùng khoá** ⇒ `SAL-001` chặn bằng **unique index**
/// (`IX_OrdOrders_TenantId_CheckoutIdempotencyKey`, `Orders:0002`).
/// </summary>
[Authorize]
public class DatHangController : Controller
{
    private readonly ICheckoutAppService _checkout;
    private readonly IDonHangAppService _donHang;
    private readonly IWarehouseAppService _kho;

    /// <summary>
    /// 🚨 **CHỌN KHÁCH CHO ĐƠN** (`CUS-001` `ADR-04` — `S3`) — trước `S3` đường web **không** hề đặt
    /// `CustomerId` cho đơn ⇒ 📌 trên thực tế *"đơn web không thuộc về ai"*, và cổng *"khách bị chặn không được
    /// mua"* **không thể** phát huy tác dụng ở đường này (không có khách để kiểm).
    /// 🔑 Nối khách vào đơn là điều kiện **cần**; cổng ở `CuaKiemKhachGiaoDich` là điều kiện **đủ**.
    /// </summary>
    private readonly CustomerAppService _khachHang;

    /// <summary>
    /// 🔐 **KIỂM QUYỀN ĐỌC DANH SÁCH KHÁCH** — ⚠️ **KHÔNG** dùng `try/catch` quanh lời gọi app service để
    /// *"thử xem có quyền không"*: 📌 một lần ném bị nuốt ở đây sẽ **che** cả lỗi thật khác.
    /// 🔑 Hỏi thẳng `IPermissionChecker` là hỏi **đúng câu hỏi** (*"người này có được xem hồ sơ khách không"*),
    /// và nếu **không** ⇒ trang vẫn chạy ở chế độ **khách vãng lai** thay vì `403` cả trang đặt hàng.
    /// 📌 Lý do phải cẩn thận: `/DatHang` là màn hình của người **bán hàng** (quyền `Orders.Manage`), còn hồ sơ
    /// khách là **dữ liệu cá nhân** (quyền `Customers.Default`) — hai quyền **khác nhau**, và không được giả định
    /// người bán hàng có quyền đọc PII.
    /// </summary>
    private readonly IPermissionChecker _quyen;

    /// <summary>
    /// 🚨 **THANH TOÁN TRÊN UI** (`S8` — nợ §16.9 #8 của `PAY-001`): sau khi đơn được tạo, tạo **giao dịch**
    /// qua **đúng** app service đã kiểm chứng (`TaoGiaoDichAsync`, đường vào **duy nhất** của `PayTransactions`)
    /// rồi chuyển khách sang **cổng**.
    /// 📌 Tầng UI **không** tính tiền và **không** quyết định trạng thái: `SoTien` lấy **nguyên văn** từ đơn
    /// (`don.GrandTotal` — chính con số đã đối chiếu với sổ giải trình ở trên).
    /// </summary>
    private readonly IPaymentAppService _thanhToan;

    /// <summary>URL IPN cho đường dev — đọc từ **cấu hình** (`PaymentHub:Fake:IpnBaseUrl`), **không** hard-code:
    /// một URL cứng sai cổng cho ra IPN gửi tới chỗ không có gì lắng nghe ⇒ đơn **không bao giờ** thành đã trả.</summary>
    private readonly IConfiguration _cauHinh;

    public DatHangController(
        ICheckoutAppService checkout,
        IDonHangAppService donHang,
        IWarehouseAppService kho,
        CustomerAppService khachHang,
        IPermissionChecker quyen,
        IPaymentAppService thanhToan,
        IConfiguration cauHinh)
    {
        _checkout = checkout;
        _donHang = donHang;
        _kho = kho;
        _khachHang = khachHang;
        _quyen = quyen;
        _thanhToan = thanhToan;
        _cauHinh = cauHinh;
    }

    /// <summary>
    /// 🔑 **NẠP DANH SÁCH KHÁCH CHO Ô CHỌN** — chỉ khi người dùng có `Customers.Default`.
    /// ⚠️ **Ô CHỌN KHÁCH KHÔNG PHẢI LÀ CỔNG:** khách `Blocked` **vẫn hiện** trong danh sách (có nhãn *"đang chặn"*)
    /// và 🔒 nếu ai đó gửi thẳng `KhachId` của khách bị chặn thì **app service vẫn ném** `0005`.
    /// 📌 Lọc khách bị chặn **ở UI** sẽ là *"cổng đặt ở chỗ dễ đi vòng nhất"* — đúng lỗi quy ước #7 cảnh báo.
    /// </summary>
    private async Task NapKhachAsync(DatHangTrangDto trang)
    {
        trang.CoQuyenChonKhach = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Customers.Default);

        // 🔑 `S8` — quyền **TẠO GIAO DỊCH** tách khỏi quyền **ĐẶT HÀNG**: ô "Thanh toán ngay" chỉ hiện khi
        // người dùng thật sự tạo được giao dịch. 📌 Nếu hiện mà không có quyền thì đơn **đã tạo xong** mới
        // nhận `403` ⇒ khách hiểu là *"đặt hàng thất bại"* trong khi hàng đã bị giữ chỗ.
        trang.CoQuyenThanhToan = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Payments.Default);

        if (!trang.CoQuyenChonKhach)
        {
            return;
        }

        // 📌 `maxResultCount: 100` — ô chọn là để **tra nhanh**, không phải màn hình quản trị: một danh sách
        // 5.000 dòng trong `<select>` là vô dụng. ⚠️ Muốn tìm khách thứ 101 trở đi thì dùng `/KhachHang` (có
        // phân trang + lọc) rồi quay lại — 📌 ghi rõ giới hạn thay vì im lặng cắt bớt.
        var ds = await _khachHang.GetDanhSachPhanTrangAsync(maxResultCount: 100);

        trang.KhachList = ds.Items.Select(x => new KhachChonDto
        {
            Id = x.Id,
            Ten = x.Name,
            DungDuoc = x.DungDuoc,
            TrangThai = x.Status
        }).ToList();
        // ⚠️ `PagedResultDto.TotalCount` là `long` ⇒ phải ép **tường minh**. 📌 Nếu để `long` chảy vào
        // `ViewBag` thì `ViewBag.TongSo is int` trong view sẽ là `false` ⇒ view hiện **0 khách** trong khi
        // thật ra có — 🔑 một lỗi im lặng đúng kiểu "phép đo sai, không phải sản phẩm sai".
        trang.TongSoKhach = (int)ds.TotalCount;
    }

    /// <summary>🚨 **GIỎ GIỮ QUA PHIÊN** (`web-001` §86) — ghi dòng hàng vào cookie `gio-web` (HttpOnly, 2 giờ).
    /// 📌 Gọi ở **ĐẦU** POST ⇒ **mọi** đường render lại (kể cả đường **LỖI**) đều giữ giỏ; đường **THÀNH CÔNG** thì **xoá** cookie.
    /// 📌 Chọn **cookie** thay vì `ISession` để **không** phải đổi cấu hình module — và **probe HTTP kiểm được** hành vi.</summary>
    /// <summary>🚨 §87 — mô tả cookie `gio-web` thành chuỗi người đọc được (vd `A × 2 · B × 3`).
    /// 🔒 Cookie sửa được ⇒ dữ liệu sai định dạng thì **BỎ QUA** phần đó (chỉ để HIỂN THỊ, không dùng để tính tiền).</summary>
    private static string MoTaGio(string? gio)
    {
        if (string.IsNullOrWhiteSpace(gio)) { return string.Empty; }

        var phan = new List<string>();
        foreach (var mot in gio.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var hai = mot.Split(':');
            if (hai.Length != 2) { continue; }
            if (!int.TryParse(hai[1], out var soLuong) || soLuong < 1) { continue; }

            var sku = hai[0].Trim();
            if (sku.Length == 0) { continue; }

            phan.Add($"{sku} × {soLuong}");
        }

        return string.Join(" · ", phan);
    }

    /// <summary>🚨 §87 — `GET /DatHang/XoaGio`: **XOÁ giỏ đang lưu** (cookie `gio-web`) rồi quay lại biểu mẫu.
    /// ⚠️ Đây là **GET có tác dụng phụ** (xoá cookie) — chấp nhận được vì: cookie **chỉ chứa SKU:SL** (không giá, không khoá,
    /// không tổng tiền) nên kẻ tấn công **không thu được gì** ngoài việc làm rỗng giỏ của chính người dùng.</summary>
    [HttpGet]
    public IActionResult XoaGio()
    {
        Response.Cookies.Delete("gio-web");

        return RedirectToAction(nameof(Index));
    }
    private static void LuuGio(HttpResponse traLoi, DatHangInput form)
    {
        var phan = new List<string> { $"{form.Sku}:{form.SoLuong}" };
        if (!string.IsNullOrWhiteSpace(form.Sku2)) { phan.Add($"{form.Sku2}:{form.SoLuong2}"); }
        if (!string.IsNullOrWhiteSpace(form.Sku3)) { phan.Add($"{form.Sku3}:{form.SoLuong3}"); }
        // 🚨 §88: dòng 4/5 cũng được lưu ⇒ giỏ 5 dòng vẫn sống qua phiên.
        if (!string.IsNullOrWhiteSpace(form.Sku4)) { phan.Add($"{form.Sku4}:{form.SoLuong4}"); }
        if (!string.IsNullOrWhiteSpace(form.Sku5)) { phan.Add($"{form.Sku5}:{form.SoLuong5}"); }

        traLoi.Cookies.Append("gio-web", string.Join(',', phan), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(2)
        });
    }

    /// <summary>Khôi phục tối đa **3 dòng** từ cookie `gio-web` (dạng `SKU:SL,SKU:SL`).
    /// 🔒 Cookie do người dùng sửa được ⇒ dữ liệu hỏng thì **BỎ QUA**, không ném (không có gì để tin ở đây).</summary>
    private static void KhoiPhucGio(DatHangTrangDto trang, string? gio)
    {
        if (string.IsNullOrWhiteSpace(gio)) { return; }

        var dong = gio.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dem = 0;
        foreach (var mot in dong)
        {
            var phan = mot.Split(':');
            if (phan.Length != 2) { continue; }
            if (!int.TryParse(phan[1], out var soLuong) || soLuong < 1) { continue; }

            var sku = phan[0].Trim();
            if (sku.Length == 0) { continue; }

            if (dem == 0) { trang.Sku = sku; trang.SoLuong = soLuong; }
            else if (dem == 1) { trang.Sku2 = sku; trang.SoLuong2 = soLuong; }
            else if (dem == 2) { trang.Sku3 = sku; trang.SoLuong3 = soLuong; }
            else if (dem == 3) { trang.Sku4 = sku; trang.SoLuong4 = soLuong; }
            else if (dem == 4) { trang.Sku5 = sku; trang.SoLuong5 = soLuong; }
            dem++;
        }
    }

    /// <summary>`GET /DatHang` — biểu mẫu. Khoá checkout sinh **tại đây** (xem bất biến ở doc của lớp).
    /// 🚨 **GIỎ GIỮ QUA PHIÊN** (§86): có cookie `gio-web` ⇒ **khôi phục** dòng đã nhập ⇒ **tải lại trang không mất giỏ**.
    /// ⚠️ Khoá checkout **vẫn sinh MỚI mỗi lần GET** (bất biến §56 — chống trùng đơn) — cookie chỉ mang **dòng hàng**, **không** mang khoá.</summary>
    public async Task<IActionResult> Index(int? soDong)
    {
        var khoList = await _kho.GetListAsync(chiHoatDong: true);

        var trang = new DatHangTrangDto
        {
            KhoList = khoList.Select(x => new KhoChonDto { Id = x.Id, Code = x.Code, Name = x.Name }).ToList(),
            Kenh = "web",
            Sku = string.Empty,
            SoLuong = 1,
            Thue = "0.1",
            KhoaCheckout = $"WEB-{Guid.NewGuid():N}"[..20],
            TokenChongGiaMa = Guid.NewGuid().ToString("N")
        };

        // 🚨 §90: SỐ DÒNG — ưu tiên `?soDong`, nếu không có thì đọc **COOKIE `so-dong`** (nhớ qua phiên), rồi mới mặc định 3.
        // 🔒 Cookie do người dùng sửa được ⇒ ép kiểu AN TOÀN: hỏng / ngoài biên ⇒ **KHÔNG ném**, dùng đường mặc định.
        var soDongChon = soDong;
        if (soDongChon is null && int.TryParse(Request.Cookies["so-dong"], out var tuCookie)) { soDongChon = tuCookie; }
        trang.SoDong = Math.Clamp(soDongChon ?? 3, 1, 5);

        // 📌 GHI lại cookie mỗi lần GET ⇒ lựa chọn số dòng **sống qua phiên** (trước §90, một GET mới trở về 3 — nợ §89.3).
        Response.Cookies.Append("so-dong", trang.SoDong.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(2)
        });
        KhoiPhucGio(trang, Request.Cookies["gio-web"]);
        // 🚨 §87: mô tả giỏ đang lưu để view hiển thị kèm nút "Xoá giỏ" (rỗng ⇒ không hiện).
        trang.TomTatGio = MoTaGio(Request.Cookies["gio-web"]);

        // 🚨 `S3` — nạp danh sách khách cho ô chọn (chỉ khi có `Customers.Default`; xem `NapKhachAsync`).
        await NapKhachAsync(trang);

        return View(trang);
    }

    /// <summary>
    /// `POST /DatHang` — **một** SKU (giỏ tối thiểu). **Giữ chỗ trước, tạo đơn sau** — đúng thứ tự đã kiểm chứng:
    /// tạo đơn **đòi hỏi** đã có lượt giữ chỗ (`SAL-001` **NÉM** nếu không còn `Held`).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Index(DatHangInput form)
    {
        var khoList = await _kho.GetListAsync(chiHoatDong: true);

        var trang = new DatHangTrangDto
        {
            KhoList = khoList.Select(x => new KhoChonDto { Id = x.Id, Code = x.Code, Name = x.Name }).ToList(),
            Kenh = form.Kenh,
            Sku = form.Sku,
            SoLuong = form.SoLuong,
            Thue = form.Thue,
            SoDong = form.SoDong,   // 🚨 §89: giữ SỐ DÒNG người dùng đang thấy qua các lần render lại
            KhoaCheckout = form.KhoaCheckout,   // 🚨 GIỮ NGUYÊN khoá của form (không sinh lại) — xem bất biến ở doc lớp
            TokenChongGiaMa = Guid.NewGuid().ToString("N"),
            // 🚨 `S3`: GIỮ khách đã chọn qua mọi lần render lại (kể cả đường LỖI) — 📌 nếu mất, người dùng
            // tưởng mình đặt cho khách vãng lai và **đơn gắn sai người** (không có lỗi nào báo).
            KhachId = form.KhachId
        };

        // 🚨 `S3` — nạp danh sách khách TRƯỚC mọi nhánh return (kể cả nhánh lỗi) ⇒ ô chọn luôn có dữ liệu.
        await NapKhachAsync(trang);

        // 🚨 GIỎ GIỮ QUA PHIÊN (§86): ghi giỏ NGAY TẠI ĐÂY ⇒ mọi đường render lại (kể cả đường LỖI) đều còn dòng đã nhập.
        LuuGio(Response, form);
        // 🚨 §90: giữ luôn SỐ DÒNG người dùng đang thấy (kẹp 1..5) ⇒ GET sau (không `?soDong`) vẫn đúng số ô.
        Response.Cookies.Append("so-dong", Math.Clamp(form.SoDong, 1, 5).ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(2)
        });

        if (string.IsNullOrWhiteSpace(form.Sku) || form.SoLuong < 1 || form.KhoId == Guid.Empty)
        {
            trang.Loi = "Thiếu kho / SKU / số lượng (số lượng phải ≥ 1).";
            return View(trang);
        }

        if (string.IsNullOrWhiteSpace(form.KhoaCheckout))
        {
            // 🚨 Không có khoá ⇒ **KHÔNG** tự sinh khoá mới (sẽ phá tính idempotent) ⇒ từ chối và nói rõ.
            trang.Loi = "Thiếu khoá checkout — tải lại trang rồi thử lại (khoá sinh ở bước mở biểu mẫu).";
            return View(trang);
        }

        var thue = decimal.TryParse(form.Thue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0.1m;

        try
        {
            // 🚨 GIỎ NHIỀU Ô NHẬP (§82): ghép dòng 2 và 3 vào CHUỖI của dòng 1 rồi đưa vào CÙNG bộ tách đã kiểm chứng.
        // 📌 Ghép ở SERVER (không JS) ⇒ probe HTTP kiểm được. Dòng TRỐNG bị BỎ QUA (không sinh "SKU rỗng").
        // ⚠️ Đặt TRƯỚC **cả câu lệnh** `var ketQua = ...` — KHÔNG chèn vào giữa `new CheckoutInput { ... }` (lỗi §97/H2).
        var phanBoSung = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(form.Sku2)) { phanBoSung.Append(',').Append(form.Sku2.Trim()).Append(':').Append(form.SoLuong2); }
        if (!string.IsNullOrWhiteSpace(form.Sku3)) { phanBoSung.Append(',').Append(form.Sku3.Trim()).Append(':').Append(form.SoLuong3); }
            // 🚨 §88: dòng 4/5 — cùng nguyên tắc: bỏ qua dòng TRỐNG, ghép vào CÙNG chuỗi ⇒ GỘP SKU TRÙNG xuyên MỌI dòng.
            if (!string.IsNullOrWhiteSpace(form.Sku4)) { phanBoSung.Append(',').Append(form.Sku4.Trim()).Append(':').Append(form.SoLuong4); }
            if (!string.IsNullOrWhiteSpace(form.Sku5)) { phanBoSung.Append(',').Append(form.Sku5.Trim()).Append(':').Append(form.SoLuong5); }
        if (phanBoSung.Length > 0) { form.Sku = form.Sku + phanBoSung.ToString(); }
        // 🚨 GIỮ CÁC DÒNG ĐÃ NHẬP (§84): mang dòng 2/3 TRẢ LẠI form để người dùng KHÔNG phải gõ lại.
        // ⚠️ ĐẶT Ở ĐÂY — TRƯỚC lời gọi checkout — để CẢ HAI đường (LỖI và THÀNH CÔNG) đều render với dữ liệu đã nhập.
        // 📌 Vòng 100 đặt 4 phép gán này SAU `trang.DonId = don.Id;` (nhánh THÀNH CÔNG) ⇒ đường LỖI bị XOÁ TRẮNG ô
        //    ⇒ tính năng "cứu khi có lỗi" lại chỉ chạy khi KHÔNG có lỗi (đo được ở §85). Neo duy nhất, đếm = 1.
        trang.Sku2 = form.Sku2 ?? string.Empty;
        trang.SoLuong2 = form.SoLuong2;
        trang.Sku3 = form.Sku3 ?? string.Empty;
        trang.SoLuong3 = form.SoLuong3;
        var ketQua = await _checkout.TinhVaGiuChoAsync(new CheckoutInput
            {
                ChannelCode = string.IsNullOrWhiteSpace(form.Kenh) ? "web" : form.Kenh,
                WarehouseId = form.KhoId,
                // 🚨 `S3` — **KHÁCH CỦA ĐƠN** (rỗng = khách vãng lai). 🔒 Đây là đầu vào của **cổng**
                // `CuaKiemKhachGiaoDich`: khách `Blocked` ⇒ checkout ném `eCommerce:Customers:0005`
                // **TRƯỚC** khi giữ chỗ ⇒ không có hàng nào bị giam.
                CustomerId = form.KhachId,
                OrderDiscount = 0,
                OrderFee = 0,
                IdempotencyKey = form.KhoaCheckout,
                // 🚨 **GIỎ NHIỀU DÒNG (vòng 72)** — `Sku` nhận **danh sách** `SKU:SL,SKU:SL`.
                // 📌 **Tương thích ngược:** một SKU trần (không `:`) ⇒ dùng `SoLuong` như trước.
                // 📌 **Vì sao KHÔNG sửa view ở bước này:** đổi view + controller **cùng lúc** làm phép kiểm chứng
                // khó **tách bạch** nguyên nhân khi sai. Bước này chỉ **nới dữ liệu vào** ⇒ nếu hỏng thì hỏng ở **một chỗ**.
                // ⏳ Việc còn lại: **giao diện nhiều dòng thật** (nhiều ô nhập) — lát cắt sau.
                Items = GioHelper.PhanTichGio(form.Sku, form.SoLuong, thue)
            });

            var don = await _donHang.TaoTuCheckoutAsync(new TaoDonTuCheckoutInput
            {
                CheckoutIdempotencyKey = form.KhoaCheckout,
                Number = $"WEB-{form.KhoaCheckout}",
                ChannelCode = string.IsNullOrWhiteSpace(form.Kenh) ? "web" : form.Kenh,
                // 🚨 `S3` — **ĐƠN PHẢI BIẾT CỦA KHÁCH NÀO**: trước `S3` đường web **không** đặt trường này
                // ⇒ đơn web tồn tại mà **không thuộc về ai** (bản xuất dữ liệu cá nhân của khách **thiếu** đúng
                // những đơn đó — lỗ hổng pháp lý, không chỉ là thiếu tính năng).
                CustomerId = form.KhachId
            });

            trang.DaDat = true;
            trang.SoDon = don.Number;
            trang.TrangThai = don.Status.ToString();
            trang.DonId = don.Id;
            // ✅ ĐẶT HÀNG XONG ⇒ **XOÁ GIỎ** (cookie) — tải lại trang là biểu mẫu trắng, không còn dòng cũ.
            Response.Cookies.Delete("gio-web");
            trang.TongTien = don.GrandTotal;
            trang.TienTe = ketQua.Tien?.CurrencyCode ?? "VND";

            // 📌 `ketQua.Tien.Total` là con số **từ sổ giải trình** — dùng để **đối chiếu** với `don.GrandTotal`
            // (hai công thức độc lập). Lệch ⇒ hiện **cảnh báo** thay vì im lặng (tripwire của `SAL-001` đã chặn ở tầng API;
            // đây là **thêm một lớp nhìn thấy được** ở tầng UI).
            if (ketQua.Tien is not null && ketQua.Tien.Total != don.GrandTotal)
            {
                trang.Loi = $"CẢNH BÁO: tổng từ sổ giải trình ({ketQua.Tien.Total:N0}) KHÁC tổng của đơn ({don.GrandTotal:N0}).";
            }

            // ── 🚨 `S8`: TẠO GIAO DỊCH + CHUYỂN CỔNG (nợ §16.9 #8 của `PAY-001`) ─────────────────────────
            // 🔑 **THỨ TỰ KHÔNG ĐƯỢC ĐẢO:** đơn phải **đã có** (và đã giữ chỗ) rồi mới tạo giao dịch — ngược lại
            // thì khách bị thu tiền cho một đơn **không tồn tại** (§12.2 `PAY-001`: *"NCC timeout không được
            // rollback dữ liệu đã ghi"* ⇒ giao dịch là **bằng chứng ý định thu tiền**, phải giữ).
            // 📌 Chỉ chuyển cổng khi **KHÔNG có cảnh báo lệch tiền**: chuyển sang cổng với một con số đang
            // nghi ngờ là cách nhanh nhất để thu **sai tiền**.
            if (form.ThanhToanNgay && trang.CoQuyenThanhToan && trang.Loi is null)
            {
                var giaoDich = await _thanhToan.TaoGiaoDichAsync(new TaoGiaoDichInput
                {
                    OrderId = don.Id,
                    SoDon = don.Number,
                    // 🔒 Số tiền lấy **nguyên văn** từ đơn đã ghi — không tính lại ở tầng UI.
                    SoTien = don.GrandTotal,
                    TienTe = string.IsNullOrWhiteSpace(ketQua.Tien?.CurrencyCode) ? "VND" : ketQua.Tien!.CurrencyCode,
                    ReturnUrl = $"{Request.Scheme}://{Request.Host}/DatHang/KetQua",
                    IpnUrl = IpnUrlCuaTienTrinh(),
                    ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    NoiDung = $"Thanh toan don {don.Number}"
                });

                trang.MaThamChieu = giaoDich.MerchantReference;

                if (!string.IsNullOrWhiteSpace(giaoDich.CheckoutUrl))
                {
                    // 🔑 **PRG** (`Post/Redirect/Get`): chuyển sang **cổng**, không render HTML của trang đặt hàng
                    // ⇒ tải lại trang (F5) **không** gửi lại form (nếu không: đơn thứ hai hoặc lỗi ảo).
                    // 📌 Cookie giỏ đã bị xoá ở trên (đơn đã tạo) ⇒ quay lại không còn dòng cũ.
                    return Redirect(giaoDich.CheckoutUrl!);
                }

                // ⚠️ **KHÔNG ném lỗi và KHÔNG nói "đặt hàng thất bại"**: đơn **đã tạo**, giao dịch **đã tạo**.
                // Chỉ **chưa lấy được URL** thanh toán (thiếu cấu hình cổng / cổng lỗi) ⇒ nói đúng chuyện đó,
                // và đưa đường tra trạng thái. 📌 `LaGiaoDichDaCo` được hiện để người dùng biết bấm lại
                // không sinh giao dịch thứ hai.
                trang.Loi = "Đơn ĐÃ được tạo nhưng CHƯA lấy được URL thanh toán: "
                            + (string.IsNullOrWhiteSpace(giaoDich.LoiTaoUrl) ? "cổng không trả URL." : giaoDich.LoiTaoUrl)
                            + (giaoDich.LaGiaoDichDaCo ? " (dùng lại giao dịch đã có — KHÔNG tạo thêm)" : string.Empty)
                            + " — tra trạng thái ở mục \"Kết quả thanh toán\".";
            }
        }
        catch (Volo.Abp.Authorization.AbpAuthorizationException)
        {
            // 🚨 `S8` — quyền **tạo giao dịch** thiếu (chỉ xảy ra khi form bị gửi tay vì ô chọn đã bị ẩn).
            // 🔑 Nói **đúng** chuyện gì đã xảy ra: **đơn đã tạo**, chỉ giao dịch là chưa. Nếu hiện câu chung
            // *"không đặt được"* thì người dùng sẽ đặt lại ⇒ **đơn trùng** (khoá checkout sinh mới ở GET).
            trang.Loi = "ĐƠN ĐÃ ĐƯỢC TẠO, nhưng bạn KHÔNG có quyền tạo giao dịch thanh toán "
                        + "(cần quyền `eCommerce.Payments.Default`). Đừng đặt lại — vào quản lý đơn để xử lý tiếp.";
        }
        catch (Volo.Abp.BusinessException be)
        {
            // 🚨 **SỬA LỖI THẬT PHÁT HIỆN Ở VÒNG 67:** `BusinessException.Message` trả **chuỗi vô nghĩa**
            // `"Exception of type 'Volo.Abp.BusinessException' was thrown."` — vì ABP **không** dùng `Message`
            // mà dùng **MÃ LỖI** (`Code`) + `Data`. ⇒ người dùng nhận **chỉ dẫn vô dụng** (đã ĐO được).
            // 📌 Hiện **mã nghiệp vụ** (tra được trong `InventoryErrorCodes`/`eCommerceConsts.ErrorCodes`) + **dữ liệu**
            // (sku, kho, số lượng…) ⇒ người dùng/support biết **chính xác** chuyện gì.
            // 📌 Hiện **MÃ NGHIỆP VỤ** — tra được ngay trong `InventoryErrorCodes`/`eCommerceConsts.ErrorCodes`.
            // 📌 Kèm **DỮ LIỆU** (`sku`, `kho`, số lượng…) mà app service đã gắn vào lỗi — nhờ đó người dùng/support
            // biết **chính xác** bản ghi nào có vấn đề, không phải mò.
            // ⚠️ `be.Data` khai báo là `object` ở phiên bản ABP này ⇒ dùng giao diện **KHÔNG GENERIC** `IDictionary`
            // để không phụ thuộc kiểu generic cụ thể; nếu không ép được thì **vẫn** hiện mã lỗi (không vỡ).
            var thongDiep = new System.Text.StringBuilder($"Mã lỗi: {be.Code}");

            if (be.Data is System.Collections.IDictionary duLieu)
            {
                foreach (System.Collections.DictionaryEntry muc in duLieu)
                {
                    thongDiep.Append(" — ").Append(muc.Key).Append('=').Append(muc.Value);
                }
            }

            trang.Loi = thongDiep.ToString();
        }
        catch (Exception ex)
        {
            // ⚠️ Hiện **thông điệp** thay vì trang lỗi 500: người dùng cần biết **vì sao** không đặt được
            // (hết hàng, chưa có giá, kho ngừng hoạt động…) — đúng tinh thần "lỗi nghiệp vụ phải nói được".
            trang.Loi = ex.Message;
        }

        return View(trang);
    }

    /// <summary>
    /// 🚨 **TRANG KẾT QUẢ THANH TOÁN** (`S8`) — `GET /DatHang/KetQua?ma={mã tham chiếu}`.
    ///
    /// 🔒 **BẤT BIẾN QUAN TRỌNG NHẤT: TRẠNG THÁI ĐỌC TỪ DB, KHÔNG TỪ URL.**
    /// `returnUrl` (và mọi tham số `vnp_*` mà cổng gắn vào) **không phải** bằng chứng thanh toán — `PAY-001`
    /// **D2**. 📌 Nếu trang này tin `?vnp_ResponseCode=00` thì **bất kỳ ai** gõ URL cũng tự đánh dấu đơn đã trả
    /// tiền trong mắt người dùng (và tệ hơn: người vận hành **giao hàng**). Tham số `ma` chỉ là **khoá tra cứu**.
    /// ✅ Ca E2E `S8-P6` đo **chiều ngược**: một giao dịch CHƯA trả + `?vnp_ResponseCode=00` ⇒ trang **vẫn**
    /// hiện *"chưa thanh toán"*.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> KetQua(string? ma)
    {
        var trang = new DatHangKetQuaDto { MaThamChieu = ma?.Trim() };

        if (string.IsNullOrWhiteSpace(ma))
        {
            trang.Loi = "Thiếu mã tham chiếu giao dịch (`?ma=`).";
            return View(trang);
        }

        try
        {
            var giaoDich = await _thanhToan.GetTheoMaAsync(ma.Trim());

            if (giaoDich is null)
            {
                // ⚠️ Không tìm thấy là ca **bình thường** (gõ sai mã / giao dịch của tenant khác) ⇒ thông báo,
                // **không** phải `500` và cũng **không** được hiện *"đã thanh toán"*.
                trang.Loi = "Không tìm thấy giao dịch với mã này.";
                return View(trang);
            }

            trang.TimThay = true;
            trang.TrangThai = giaoDich.Status;
            trang.DaThanhToan = giaoDich.DaThuTien;
            trang.SoTien = giaoDich.Amount;
            trang.TienTe = giaoDich.Currency;
            trang.DonId = giaoDich.OrderId;
            trang.LanNhanWebhook = giaoDich.WebhookReceivedCount;
            trang.ProviderTransactionId = giaoDich.ProviderTransactionId;
        }
        catch (Volo.Abp.Authorization.AbpAuthorizationException)
        {
            trang.Loi = "Bạn không có quyền xem trạng thái thanh toán (cần `eCommerce.Payments.Default`).";
        }

        return View(trang);
    }

    /// <summary>
    /// URL **IPN** của tiến trình API — 📌 đọc từ **cấu hình** (`PaymentHub:Fake:IpnBaseUrl`), vì IPN nằm ở
    /// **tiến trình khác** (`HttpApi.Host`, cổng 5000) chứ không ở tiến trình Web này.
    /// ⚠️ Nếu thiếu cấu hình: dùng chính host hiện tại — sai trong dev, **nhưng** không im lặng: giá trị được
    /// hiện ngay trên trang kết quả khi IPN không tới (nút "tra trạng thái" ở `/DoiSoatThanhToan`).
    /// </summary>
    private string IpnUrlCuaTienTrinh()
    {
        var goc = (_cauHinh["PaymentHub:Fake:IpnBaseUrl"] ?? string.Empty).Trim().TrimEnd('/');

        return string.IsNullOrWhiteSpace(goc)
            ? $"{Request.Scheme}://{Request.Host}/api/webhooks/vnpay"
            : goc + "/api/webhooks/vnpay";
    }
}

/// <summary>Dữ liệu trang `/DatHang` — phục vụ **cả** biểu mẫu **và** kết quả (một view, một đường render).</summary>
public class DatHangTrangDto
{
    public List<KhoChonDto> KhoList { get; set; } = new();

    public string Kenh { get; set; } = "web";

    public string Sku { get; set; } = string.Empty;

    public int SoLuong { get; set; } = 1;

    public string Thue { get; set; } = "0.1";

    /// <summary>🚨 Sinh ở `GET`, **mang theo** trong form — xem bất biến ở doc của `DatHangController`.</summary>
    public string KhoaCheckout { get; set; } = string.Empty;

    /// <summary>
    /// 🚨 **KHÁCH CỦA ĐƠN** (`S3`) — `null` = **khách vãng lai** (`CUS-001` `ADR-03`).
    /// 📌 Một `Guid?` chứ không phải chuỗi: ô chọn gửi `Id` của khách, và giá trị rỗng (`""`) bind thành `null`
    /// ⇒ *"khách vãng lai"* là **trạng thái mặc định**, không phải một lựa chọn đặc biệt.
    /// </summary>
    public Guid? KhachId { get; set; }

    /// <summary>
    /// Danh sách khách cho ô chọn — 🔑 chỉ nạp khi người dùng có `Customers.Default`.
    /// ⚠️ **Rỗng KHÔNG có nghĩa "không có khách"**: nó có thể là *"không có quyền xem"* ⇒ view phải nói rõ
    /// trạng thái (`CoQuyenChonKhach`), 📌 không để người dùng hiểu nhầm hệ thống **không có khách nào**.
    /// </summary>
    public List<KhachChonDto> KhachList { get; set; } = new();

    public bool CoQuyenChonKhach { get; set; }

    /// <summary>
    /// 🚨 `S8` — có quyền **TẠO GIAO DỊCH** thanh toán không (`eCommerce.Payments.Default`).
    /// 📌 Khác quyền đặt hàng: quyền đọc/tạo thanh toán là **quyền về TIỀN**, không phải quyền bán hàng.
    /// ⚠️ Ô chọn "Thanh toán ngay" chỉ hiện khi có quyền — nhưng 🔒 **cổng thật** vẫn ở app service
    /// (`[Authorize]` cấp class): ẩn ô chọn là **tiện nghi**, không phải hàng rào.
    /// </summary>
    public bool CoQuyenThanhToan { get; set; }

    /// <summary>`S8` — giữ trạng thái ô "Thanh toán ngay" qua các lần render lại (đường LỖI).</summary>
    public bool ThanhToanNgay { get; set; }

    /// <summary>
    /// 🚨 `S8` — **mã tham chiếu giao dịch** đã tạo (nếu có) — để trang kết quả tra được trạng thái.
    /// 📌 Có mã này ngay cả khi **chưa** lấy được URL cổng: giao dịch **đã tồn tại** trong sổ, và người dùng
    /// cần một đường tra cứu thay vì đặt lại đơn.
    /// </summary>
    public string? MaThamChieu { get; set; }

    /// <summary>Tổng số khách trong hệ thống — 📌 để nói rõ khi danh sách ô chọn bị giới hạn 100 dòng.</summary>
    public int TongSoKhach { get; set; }

    public string TokenChongGiaMa { get; set; } = string.Empty;

    public bool DaDat { get; set; }

    public string? SoDon { get; set; }

    public string? TrangThai { get; set; }

    /// <summary>Id đơn đã tạo — để trang kết quả **liên kết tới /DonHang/ChiTiet/{id}** (lối vào quản lý đơn).</summary>
    public Guid? DonId { get; set; }

    public decimal TongTien { get; set; }
    /// <summary>🚨 **GIỮ CÁC DÒNG ĐÃ NHẬP** (`web-001` §84): dòng 2/3 của giỏ được **mang trả lại** vào form
    /// khi trang render lại (sau lỗi **hoặc** sau khi đặt xong) ⇒ người dùng **không phải gõ lại**.
    /// 📌 Trước vòng 100, hai ô này **mất trắng** sau mỗi lần POST — dễ thấy khi có lỗi ở dòng 2/3.</summary>
    public string Sku2 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku2" />
    public int SoLuong2 { get; set; } = 1;

    /// <inheritdoc cref="Sku2" />
    public string Sku3 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku3" />
    public int SoLuong3 { get; set; } = 1;
    /// <summary>🚨 **SỐ DÒNG GIỎ** (`web-001` §88) — 1..**5**; dòng 4/5 dùng ``Sku4``/``Sku5`` (dòng 1..3 GIỮ NGUYÊN tên
    /// ``Sku``/``Sku2``/``Sku3`` ⇒ **không phá** mọi probe và phép kiểm đã có).</summary>
    public int SoDong { get; set; } = 3;

    /// <inheritdoc cref="SoDong" />
    public string Sku4 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku4" />
    public int SoLuong4 { get; set; } = 1;

    /// <inheritdoc cref="Sku4" />
    public string Sku5 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku5" />
    public int SoLuong5 { get; set; } = 1;

    public string? TienTe { get; set; }

    public string? Loi { get; set; }
    /// <summary>🚨 **MÔ TẢ GIỎ ĐANG LƯU** (`web-001` §87) — chuỗi người đọc được (vd `A × 2 · B × 3`) khi cookie `gio-web` có dữ liệu.
    /// 📌 Để người dùng **thấy** giỏ còn giữ gì, và có nút **Xoá giỏ** ngay cạnh. Rỗng ⇒ view **không** hiện khối này.</summary>
    public string TomTatGio { get; set; } = string.Empty;
}

/// <summary>
/// 🚨 **DỮ LIỆU TRANG KẾT QUẢ THANH TOÁN** (`S8`) — `GET /DatHang/KetQua?ma=…`.
///
/// 🔒 **Mọi trường trạng thái ở đây đến từ DB** (`IPaymentAppService.GetTheoMaAsync`), KHÔNG từ query string.
/// 📌 Vì vậy DTO **không có** trường nào kiểu `VnpResponseCode`: một trường như thế sẽ mời người sau
/// *"tin tham số URL"* — đúng thứ `PAY-001` **D2** cấm.
/// </summary>
public class DatHangKetQuaDto
{
    /// <summary>Mã tham chiếu (khoá tra cứu) — chỉ để **tra**, không phải bằng chứng.</summary>
    public string? MaThamChieu { get; set; }

    public bool TimThay { get; set; }

    /// <summary>Trạng thái **trong sổ** (`Initiated`/`Pending`/`Succeeded`/`Failed`/`Cancelled`…).</summary>
    public string? TrangThai { get; set; }

    /// <summary>`true` = **đã thu tiền** (do IPN đã xác thực chữ ký ghi) — không phải "cổng nói thành công".</summary>
    public bool DaThanhToan { get; set; }

    public decimal SoTien { get; set; }

    public string? TienTe { get; set; }

    /// <summary>Đơn liên quan (nếu có) ⇒ link sang `/DonHang/ChiTiet/{id}`.</summary>
    public Guid? DonId { get; set; }

    /// <summary>🔑 **Bằng chứng IPN** — đếm **mọi** lần nhận (kể cả lần bị bỏ qua). `0` nghĩa là
    /// **chưa từng** có IPN nào tới ⇒ nếu trang nói "đã thanh toán" mà số này là `0` thì có gì đó sai.</summary>
    public int LanNhanWebhook { get; set; }

    public string? ProviderTransactionId { get; set; }

    public string? Loi { get; set; }
}

public class KhoChonDto
{    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;
}

/// <summary>
/// Một khách trong **ô chọn khách** của `/DatHang` (`S3`).
/// 🔑 Có `DungDuoc` để nhãn nói rõ *"đang chặn"* — 📌 nhưng khách **vẫn chọn được**: việc chặn là của
/// **app service** (guard thật), không phải của `disabled` trong HTML (thuộc tính đó sửa được bằng tay).
/// </summary>
public class KhachChonDto
{
    public Guid Id { get; set; }

    public string Ten { get; set; } = default!;

    /// <summary>`true` = khách **KHÔNG** bị chặn (`ADR-04`).</summary>
    public bool DungDuoc { get; set; }

    /// <summary>`Inactive` / `Active` / `Blocked` — để nhãn hiển thị đúng trạng thái thật.</summary>
    public string TrangThai { get; set; } = default!;
}

/// <summary>
/// 🚨 **PHÂN TÍCH GIỎ NHIỀU DÒNG** — nhận `SKU:SL,SKU:SL` (hoặc `SKU;SL`), và **một SKU trần** ⇒ dùng `soLuongMacDinh`.
/// 📌 **VÌ SAO TÁCH THÀNH HÀM THUẦN:** nó là **logic dễ sai** (tách chuỗi, trùng SKU, số âm, khoảng trắng) mà
/// **không** cần DB ⇒ tách ra để **đọc và kiểm** được độc lập. 📌 **GỘP SKU TRÙNG** thay vì tạo hai dòng cùng SKU:
/// hai dòng cùng SKU trong **một** chứng từ sẽ **phá `CHECK`/unique** của kho (`SkuDaCoTrongChungTu`) — đã đo ở `INV-001`.
/// </summary>
internal static class GioHelper
{
    public static List<DongCheckoutDto> PhanTichGio(string? sku, int soLuongMacDinh, decimal thue)
    {
    var ket = new List<DongCheckoutDto>();

    foreach (var phan in (sku ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
    {
        var muc = phan.Trim();
        if (muc.Length == 0) { continue; }

        var phanTach = muc.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var maSku = phanTach[0].Trim();
        if (maSku.Length == 0) { continue; }

        var soLuong = phanTach.Length > 1 && int.TryParse(phanTach[1].Trim(), out var sl) ? sl : soLuongMacDinh;
        if (soLuong < 1) { soLuong = soLuongMacDinh > 0 ? soLuongMacDinh : 1; }

        var daCo = ket.FirstOrDefault(x => x.Sku == maSku);
        if (daCo is not null) { daCo.Quantity += soLuong; }           // gộp SKU trùng
        else { ket.Add(new DongCheckoutDto { Sku = maSku, Quantity = soLuong, TaxRate = thue }); }
    }

        return ket;
    }
}

/// <summary>Dữ liệu **POST** từ biểu mẫu (tên trường khớp `name=` trong view).</summary>
public class DatHangInput
{
    public Guid KhoId { get; set; }
    /// <summary>🚨 **GIỎ NHIỀU Ô NHẬP** (`web-001` §82): dòng 2 và 3 của giỏ — để **trống** nếu không dùng.
    /// 📌 **KHÔNG dùng JavaScript**: giá trị được **ghép ở SERVER** rồi đưa vào **cùng** bộ tách `GioHelper.PhanTichGio`
    /// (đã kiểm chứng cả **ca biên** ở §72) ⇒ **kiểm chứng được bằng probe HTTP** thay vì phải kiểm hành vi trình duyệt.
    /// 🔁 **Tương thích ngược:** dòng 1 vẫn là `Sku`/`SoLuong` ⇒ mọi lời gọi cũ **không đổi**.</summary>
    public string Sku2 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku2" />
    public int SoLuong2 { get; set; } = 1;

    /// <inheritdoc cref="Sku2" />
    public string Sku3 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku3" />
    public int SoLuong3 { get; set; } = 1;
    /// <summary>🚨 **SỐ DÒNG GIỎ** (`web-001` §88) — 1..**5**; dòng 4/5 dùng ``Sku4``/``Sku5`` (dòng 1..3 GIỮ NGUYÊN tên
    /// ``Sku``/``Sku2``/``Sku3`` ⇒ **không phá** mọi probe và phép kiểm đã có).</summary>
    public int SoDong { get; set; } = 3;

    /// <inheritdoc cref="SoDong" />
    public string Sku4 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku4" />
    public int SoLuong4 { get; set; } = 1;

    /// <inheritdoc cref="Sku4" />
    public string Sku5 { get; set; } = string.Empty;

    /// <inheritdoc cref="Sku5" />
    public int SoLuong5 { get; set; } = 1;

    public string Kenh { get; set; } = "web";

    public string Sku { get; set; } = string.Empty;

    public int SoLuong { get; set; } = 1;

    public string Thue { get; set; } = "0.1";

    public string KhoaCheckout { get; set; } = string.Empty;

    /// <summary>
    /// 🚨 `S8` — **THANH TOÁN NGAY**: `true` ⇒ sau khi tạo đơn, tạo giao dịch và **chuyển sang cổng**.
    /// 📌 Mặc định **`false`**: giữ nguyên hành vi "chỉ đặt đơn" cho mọi lời gọi đã có (E2E/probe cũ), và
    /// người dùng **chủ động** chọn trả tiền — 🔑 không ai bị đẩy sang cổng thanh toán mà không biết.
    /// ⚠️ Một checkbox HTML không gửi giá trị khi **không** được tick ⇒ `false` là giá trị **mặc định** đúng.
    /// </summary>
    public bool ThanhToanNgay { get; set; }

    /// <summary>
    /// 🚨 **KHÁCH CỦA ĐƠN** (`S3`) — tên trường `<select name="KhachId">` trong view.
    /// 📌 **Để trống = khách vãng lai.** ⚠️ Bind `Guid?` (không phải `Guid`): một `""` từ ô chọn rỗng phải
    /// thành `null`, 🔑 nếu bind `Guid` thì model binder cho ra `Guid.Empty` ⇒ mọi đơn vãng lai sẽ gắn
    /// **một id khách không tồn tại** ⇒ `0005` chặn **tất cả** đơn vãng lai (lỗi im lặng, rất khó tìm).
    /// </summary>
    public Guid? KhachId { get; set; }
}
