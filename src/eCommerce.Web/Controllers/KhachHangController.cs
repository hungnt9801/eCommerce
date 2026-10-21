using eCommerce.Customers;
using System.Globalization;
using System.Text;
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

    /// <summary>
    /// `GET /KhachHang` — danh sách hồ sơ, lọc **tuỳ chọn** theo từ khoá, **có phân trang** (`S3`).
    ///
    /// 🔑 **VÌ SAO PHÂN TRANG LÀ VIỆC CỦA DỮ LIỆU CÁ NHÂN, KHÔNG CHỈ CỦA HIỆU NĂNG:** trước `S3`, trang này
    /// hiện **top 100** và **không có tổng số** ⇒ 📌 người vận hành **không biết** còn hồ sơ nào nữa; cách duy
    /// nhất để chắc là **nâng trần** (kéo cả bảng PII về một trang). ✅ Nay mỗi trang **20** hồ sơ và có
    /// *"trang X / Y · tổng N"* ⇒ câu hỏi *"còn ai không"* có **một con số** trả lời.
    ///
    /// 🔒 **`trang` được KẸP, không ném:** một URL sửa tay (`?trang=0`, `?trang=-5`, `?trang=99999`) phải
    /// cho ra **một trang hợp lệ**, ⚠️ không phải `500`. 📌 Riêng `trang` vượt quá số trang: trả **trang cuối**
    /// chứ không trả bảng rỗng — một bảng rỗng trông y như *"đã xoá hết khách"*.
    /// </summary>
    public async Task<IActionResult> Index(string? tuKhoa = null, int trang = 1)
    {
        // 📌 20 dòng/trang: đủ để **rà** mà không kéo cả bảng. ⚠️ Đây là **trần hiển thị**, không phải trần API
        // (`CustomerAppService.GetDanhSachPhanTrangAsync` tự kẹp `1..200`).
        const int SoDongMotTrang = 20;

        var tuKhoaDaChuanHoa = string.IsNullOrWhiteSpace(tuKhoa) ? null : tuKhoa.Trim();

        // 🔑 Lần đọc ĐẦU để biết `totalCount` ⇒ mới biết số trang ⇒ mới kẹp được `trang`.
        var ketQua = await _khachHang.GetDanhSachPhanTrangAsync(
            keyword: tuKhoaDaChuanHoa,
            skipCount: 0,
            maxResultCount: SoDongMotTrang);

        var soTrang = ketQua.TotalCount == 0
            ? 1
            // ⚠️ `Math.Max(1, …)`: `TotalCount > 0` mà phép chia ra 0 thì không xảy ra, nhưng giữ `Max` để
            // **không** bao giờ có *"trang 0"* trong HTML (một liên kết `?trang=0` là liên kết sai).
            : Math.Max(1, (int)Math.Ceiling(ketQua.TotalCount / (double)SoDongMotTrang));

        var trangHienTai = Math.Clamp(trang, 1, soTrang);

        if (trangHienTai != 1)
        {
            ketQua = await _khachHang.GetDanhSachPhanTrangAsync(
                keyword: tuKhoaDaChuanHoa,
                skipCount: (trangHienTai - 1) * SoDongMotTrang,
                maxResultCount: SoDongMotTrang);
        }

        ViewBag.TuKhoa = tuKhoa;
        ViewBag.Trang = trangHienTai;
        ViewBag.SoTrang = soTrang;
        // ⚠️ `PagedResultDto.TotalCount` là `long` ⇒ ép **tường minh** xuống `int`: 📌 nếu để `long`, phép
        // `ViewBag.TongSo is int` trong view trả `false` ⇒ màn hình hiện **0 hồ sơ** (lỗi im lặng).
        ViewBag.TongSo = (int)ketQua.TotalCount;
        ViewBag.SoDongMotTrang = SoDongMotTrang;

        return View(ketQua.Items);
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
    /// <param name="lyDo">
    /// 🚨 **LY DO XUAT — BAT BUOC** (`CUS-001` §26). 🔑 *"Vi sao du lieu cua toi bi xuat"* la cau hoi luat.
    /// ⚠️ **KHONG** kiem o day: 🔒 rang buoc o controller la **tien nghi**, khong phai **guard** —
    /// 📌 guard **that** nam o `CustomerAppService.XuatDuLieuCaNhanAsync` (moi duong goi deu di qua do).
    /// </param>
    public async Task<IActionResult> XuatDuLieu(Guid id, string? lyDo = null)
    {
        var duLieu = await _khachHang.XuatDuLieuCaNhanAsync(id, lyDo);

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
    /// `GET /KhachHang/NhatKyCsv` — 🔑 **XUẤT NHẬT KÝ KIỂM TOÁN RA TỆP CSV** (`CUS-001` §25).
    ///
    /// 🔑 **VÌ SAO CẦN (không chỉ "tiện"):** người kiểm toán **phải đính kèm bằng chứng** vào báo cáo.
    /// ⚠️ Không có tệp thì họ **chép tay** — 📌 và dữ liệu **chép tay** thì **không kiểm chứng được**
    /// (🔒 không ai biết nó có khớp với hệ thống hay không).
    ///
    /// 🔒 **BA ĐIỀU BẮT BUỘC, VÀ CẢ BA ĐỀU NẰM TRONG CHÍNH TỆP** (xem `CsvNhatKy`):
    /// <list type="number">
    ///   <item>**BOM UTF-8** — ⚠️ thiếu nó thì Excel đọc tiếng Việt thành **rác**, 🔑 và tệp đó là thứ
    ///     **gửi cho cơ quan kiểm toán**;</item>
    ///   <item>**Dòng tiền đề** — ai xuất, lúc nào, và **tệp KHÔNG chứa PII** (🔑 tệp **rời khỏi hệ thống**
    ///     ⇒ 📌 nó phải **tự mô tả** được);</item>
    ///   <item>⛔ **Dòng cảnh báo khi bị cắt** — 🔒 tệp được đọc ở **nơi khác, lúc khác, bởi người khác**
    ///     ⇒ 📌 người đó **không có cách nào** biết bằng chứng đã bị cắt.</item>
    /// </list>
    ///
    /// ⚠️ **TÁI DÙNG ĐÚNG BỘ LỌC CỦA MÀN HÌNH** (cùng tham số + cùng `KieuUtc`) — 📌 nếu lọc khác nhau thì
    /// *"cái tôi thấy"* và *"cái tôi gửi đi"* **không khớp**, 🔑 và đó là điều tệ nhất với một bằng chứng.
    /// </summary>
    /// <param name="toiDa">
    /// ⚠️ **Tran so dong** — 🔑 de **che do kiem thu** co the ep tep **bi cat** va 📌 nho vay kiem duoc
    /// **dong canh bao** (⚠️ neu khong co duong nay thi dong canh bao **khong bao gio** duoc do ⇒
    /// 🔒 mot phep kiem co the **luon xanh** vi no **khong bao gio** chay).
    /// 📌 App service da `Clamp(1..500)` ⇒ 🔑 duong nay **khong** mo them kha nang lay qua nhieu.
    /// </param>
    /// <param name="phanCach">
    /// 🔑 **Dấu phân cách của tệp** (`S10`, đóng nợ `CUS-001` §27.6 #5): `phay` (mặc định, **RFC 4180**) hoặc
    /// `cham-phay` (**Excel locale dùng `,` làm dấu thập phân**, vd tiếng Việt).
    /// 🚨 **VÌ SAO PHẢI CHỌN ĐƯỢC (không phải tiện nghi):** ở locale đó Excel đọc tệp `,` thành **ĐÚNG MỘT
    /// CỘT** — ⚠️ và BOM **không cứu được** (BOM là bảng mã, không phải dấu phân cách). 📌 Với tệp **bằng
    /// chứng kiểm toán**, "thành một cột" nghĩa là người đọc **kết luận sai về bằng chứng**.
    /// 🔒 **KHÔNG đổi ngầm định:** đổi ngầm định sẽ phá mọi trình đọc đang đúng ⇒ người dùng **chọn**, và
    /// **tên tệp + dòng tiền đề** ghi rõ bản nào.
    /// 🚨 Giá trị **lạ** ⇒ **`400`** (⚠️ **không** im lặng rơi về dấu phẩy: gõ sai một lần mà nhận tệp **SAI**
    /// thì không ai biết — đúng loại lỗi của tệp bằng chứng).
    /// </param>
    public async Task<IActionResult> NhatKyCsv(Guid? customerId = null, string? tuNgay = null, string? denNgay = null, string? nguoiXuat = null, int toiDa = 100, string? phanCach = null)
    {
        // 🔑 Cung `KieuUtc` nhu `NhatKy` — ⚠️ lech mot lan nua o day thi tep va man hinh **khong khop**.
        const DateTimeStyles KieuUtc = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        // 🚨 **CHỌN DẤU PHÂN CÁCH TRƯỚC MỌI VIỆC KHÁC** — 🔒 để một giá trị sai **không** tiêu tốn một lượt
        // truy vấn DB, và (quan trọng hơn) **không** tạo ra tệp nào.
        var dauPhanCach = CsvNhatKy.DocPhanCach(phanCach);

        if (dauPhanCach is null)
        {
            // 📌 Thông điệp **liệt kê giá trị hợp lệ** — người dùng sửa được ngay, không phải đoán.
            return BadRequest(
                "Tham so 'phanCach' khong hop le. Gia tri hop le: '"
                + CsvNhatKy.ThamSoPhay + "' (mac dinh, RFC 4180) hoac '"
                + CsvNhatKy.ThamSoChamPhay + "' (Excel locale dung ',' lam dau thap phan).");
        }

        var phanCachDung = dauPhanCach.Value;

        var tu = DateTime.TryParse(tuNgay, CultureInfo.InvariantCulture, KieuUtc, out var t) ? t : (DateTime?)null;
        var den = DateTime.TryParse(denNgay, CultureInfo.InvariantCulture, KieuUtc, out var d) ? d : (DateTime?)null;

        var ketQua = await _khachHang.GetNhatKyXuatDuLieuAsync(
            customerId: customerId,
            tuNgayUtc: tu,
            denNgayUtc: den,
            nguoiXuat: nguoiXuat,
            toiDa: toiDa);

        var luc = DateTime.UtcNow;
                // 🔑 **NGUOI XUAT TEP** — 📌 de tep CSV **tu mo ta** duoc *"ai da xuat no"*.
        // ⚠️ **VI SAO KHONG DUNG `CurrentUser`:** 📌 `Controller` **khong** ke thua `ApplicationService`
        // ⇒ thuoc tinh do **khong ton tai** (loi build that: `CS0103`); va `ICurrentUser` cung **khong**
        // resolve duoc trong du an Web nay (thieu tham chieu goi).
        // ✅ Dung thang **`HttpContext.User`** — 🔑 khong them phu thuoc nao, va 📌 day la **nguon that**
        // ma moi lop khac cung doc tu do (cookie auth cua Web).
        // 🔒 Luu y: day la **nguoi xuat TEP**, KHAC voi **nguoi xuat du lieu** o tung dong nhat ky
        // (`NguoiXuat`) — 📌 hai cau hoi khac nhau, nen tep phai co **ca hai**.
        var nguoiXuatTep = HttpContext?.User?.Identity?.Name ?? "(khong xac dinh)";

        var sb = new StringBuilder();

        // 🔑 Dong tien de (kem dong canh bao NGAY DUOI neu bi cat) — 📌 de nguoi doc **thay ngay**.
        sb.Append(CsvNhatKy.DongTienDe(nguoiXuatTep, luc, ketQua.CacDong.Count, ketQua.TongSo, phanCachDung)).Append(CsvNhatKy.XuongDong);

        if (ketQua.BiCat)
        {
            // ⛔ **BAT BUOC** — xem ly do (3) o tren.
            sb.Append(CsvNhatKy.DongCanhBaoCat(ketQua.CacDong.Count, ketQua.TongSo, luc, phanCachDung)).Append(CsvNhatKy.XuongDong);
        }

        sb.Append(CsvNhatKy.XuongDong);

        // 📌 Tieu de cot — 🔑 ASCII de **moi trinh doc** deu xu ly duoc, ⚠️ va de script kiem co neo on dinh.
        sb.Append(CsvNhatKy.DongVoi(phanCachDung,
            "LucUtc", "NguoiXuat", "CustomerId", "TenKhach", "SoDongHoSo", "SoDonHang", "DonHangBiCat", "LyDo"))
          .Append(CsvNhatKy.XuongDong);

        foreach (var nk in ketQua.CacDong)
        {
            sb.Append(CsvNhatKy.DongVoi(phanCachDung,
                nk.XuatLucUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                nk.NguoiXuat,
                nk.CustomerId.ToString(),
                // ⚠️ Khach da bi xoa ⇒ `null` ⇒ 🔑 ghi ro chu **KHONG** de trong (📌 de trong trong nhu loi hien thi).
                nk.TenKhach ?? "(ho so da bi xoa)",
                nk.SoDongHoSo.ToString(CultureInfo.InvariantCulture),
                nk.SoDonHang.ToString(CultureInfo.InvariantCulture),
                nk.DonHangBiCat ? "CO" : "khong",
                nk.LyDo ?? "(khong ghi ly do)")).Append(CsvNhatKy.XuongDong);
        }

        // 🔒 **BOM + UTF-8** — xem ly do (1). ⚠️ `Encoding.UTF8` **KHONG** tu them BOM khi `GetBytes`
        // ⇒ 📌 phai ghep **thu cong** (day la bay da gap that).
        var than = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[CsvNhatKy.Bom.Length + than.Length];
        CsvNhatKy.Bom.CopyTo(bytes, 0);
        than.CopyTo(bytes, CsvNhatKy.Bom.Length);

        // 📌 Ten tep co **moc thoi gian UTC** ⇒ 🔑 xuat nhieu lan thi khong **ghi de** nhau, va
        // 📌 nguoi doc biet **ban nao la ban nao**.
        // ➕ `S10`: **hau to theo dau phan cach** — 🔒 hai ban khac dau phan cach ma cung ten thi khi tai ve
        // cung mot thu muc trinh duyet them `(1)` ⇒ 📌 khong con biet **ban nao la ban nao**.
        var tenTep = $"nhat-ky-xuat-du-lieu-ca-nhan-{luc:yyyyMMdd-HHmmss}{CsvNhatKy.HauToTenTep(phanCachDung)}.csv";

        return File(bytes, "text/csv", tenTep);
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
