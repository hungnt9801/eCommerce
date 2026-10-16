using eCommerce.Application.Contracts.Checkout;
using eCommerce.Application.Contracts.Inventory;
using eCommerce.Application.Contracts.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public DatHangController(
        ICheckoutAppService checkout,
        IDonHangAppService donHang,
        IWarehouseAppService kho)
    {
        _checkout = checkout;
        _donHang = donHang;
        _kho = kho;
    }

    /// <summary>🚨 **GIỎ GIỮ QUA PHIÊN** (`web-001` §86) — ghi dòng hàng vào cookie `gio-web` (HttpOnly, 2 giờ).
    /// 📌 Gọi ở **ĐẦU** POST ⇒ **mọi** đường render lại (kể cả đường **LỖI**) đều giữ giỏ; đường **THÀNH CÔNG** thì **xoá** cookie.
    /// 📌 Chọn **cookie** thay vì `ISession` để **không** phải đổi cấu hình module — và **probe HTTP kiểm được** hành vi.</summary>
    private static void LuuGio(HttpResponse traLoi, DatHangInput form)
    {
        var phan = new List<string> { $"{form.Sku}:{form.SoLuong}" };
        if (!string.IsNullOrWhiteSpace(form.Sku2)) { phan.Add($"{form.Sku2}:{form.SoLuong2}"); }
        if (!string.IsNullOrWhiteSpace(form.Sku3)) { phan.Add($"{form.Sku3}:{form.SoLuong3}"); }

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
            dem++;
        }
    }

    /// <summary>`GET /DatHang` — biểu mẫu. Khoá checkout sinh **tại đây** (xem bất biến ở doc của lớp).
    /// 🚨 **GIỎ GIỮ QUA PHIÊN** (§86): có cookie `gio-web` ⇒ **khôi phục** dòng đã nhập ⇒ **tải lại trang không mất giỏ**.
    /// ⚠️ Khoá checkout **vẫn sinh MỚI mỗi lần GET** (bất biến §56 — chống trùng đơn) — cookie chỉ mang **dòng hàng**, **không** mang khoá.</summary>
    public async Task<IActionResult> Index()
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

        KhoiPhucGio(trang, Request.Cookies["gio-web"]);

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
            KhoaCheckout = form.KhoaCheckout,   // 🚨 GIỮ NGUYÊN khoá của form (không sinh lại) — xem bất biến ở doc lớp
            TokenChongGiaMa = Guid.NewGuid().ToString("N")
        };

        // 🚨 GIỎ GIỮ QUA PHIÊN (§86): ghi giỏ NGAY TẠI ĐÂY ⇒ mọi đường render lại (kể cả đường LỖI) đều còn dòng đã nhập.
        LuuGio(Response, form);

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
                ChannelCode = string.IsNullOrWhiteSpace(form.Kenh) ? "web" : form.Kenh
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

    public string? TienTe { get; set; }

    public string? Loi { get; set; }
}

public class KhoChonDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;
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

    public string Kenh { get; set; } = "web";

    public string Sku { get; set; } = string.Empty;

    public int SoLuong { get; set; } = 1;

    public string Thue { get; set; } = "0.1";

    public string KhoaCheckout { get; set; } = string.Empty;
}
