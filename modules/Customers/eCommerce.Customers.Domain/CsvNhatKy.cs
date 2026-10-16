using System.Globalization;
using System.Text;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **GHI TỆP CSV CHO NHẬT KÝ KIỂM TOÁN** (`CUS-001` §25).
///
/// 🔑 **VÌ SAO CÓ LỚP NÀY (không `string.Join(",")` trong controller):**
///
/// Một tệp CSV **trông đơn giản** nhưng có **ba cái bẫy** mà mỗi cái đều tạo ra một tệp **ĐỌC SAI** —
/// ⚠️ và 🔒 **một tệp bằng chứng đọc sai còn tệ hơn KHÔNG CÓ tệp**: 📌 nó khiến người ta **tin** rằng
/// mình đã kiểm.
///
/// <list type="number">
///   <item><b>BOM UTF-8</b> — 🔑 Excel (bản phổ biến ở VN) **không** tự nhận UTF-8 nếu thiếu BOM ⇒ tiếng Việt
///     thành **rác** (`Nhật` → `Nh?t` / `Nháºt`). 📌 Và tệp đó là thứ được **gửi cho cơ quan kiểm toán**.</item>
///   <item><b>Dấu phẩy trong dữ liệu</b> — ⚠️ một lý do như *"theo yêu cầu, qua điện thoại"* có **dấu phẩy**
///     ⇒ nếu không bọc trong `"…"` thì nó **tách thành hai cột** ⇒ 🔒 **toàn bộ các cột sau bị lệch**,
///     và 📌 người đọc **không hề biết** (bảng vẫn trông hợp lệ).</item>
///   <item><b>Dấu `"` trong dữ liệu</b> — ⚠️ theo **RFC 4180** phải **nhân đôi** (`"` → `""`) — 📌 nếu không
///     thì dấu `"` **đóng trường sớm** và phần còn lại **tràn ra ngoài**.</item>
/// </list>
///
/// ➕ **Và `\r\n` (không phải `\n`)**: 🔒 RFC 4180 quy định **CRLF**; ⚠️ một số trình đọc (kể cả Excel trong
/// vài ngữ cảnh) coi `\n` trần là **ký tự trong ô** ⇒ 📌 **toàn bộ tệp thành MỘT ô**.
///
/// 🔑 **LỚP NÀY LÀ HÀM THUẦN** (không phụ thuộc HTTP, DB, hay ABP) ⇒ ✅ **test được bằng unit test nhanh**,
/// 📌 và đó chính là thứ tôi còn **nợ** ở §24.6 #1 (*"chưa có test ở tầng unit"*).
/// </summary>
public static class CsvNhatKy
{
    /// <summary>🔒 **BOM UTF-8** — xem lý do (1) ở trên. ⚠️ **KHÔNG** bỏ: thiếu nó thì tiếng Việt hỏng trong Excel.</summary>
    public static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Dấu phân cách **CRLF** theo RFC 4180 — xem lý do ở trên.</summary>
    public const string XuongDong = "\r\n";

    /// <summary>
    /// 🔒 **BỌC MOT MOT TRUONG THEO RFC 4180** — dấu `"` thanh `""`, va **LUON** boc trong `"…"`.
    ///
    /// 📌 **VI SAO LUON BOC (ke ca khi khong can):** 🔑 neu chi boc khi *"thay dau phay"* thi mot gia tri
    /// **chua xuong dong** hoac **chua `"`** se bi xu ly **khac nhau tuy du lieu** — ⚠️ va do la loai loi
    /// **chi xuat hien voi mot so dong**, 📌 tuc la **khong tai hien duoc** o ca kiem binh thuong.
    /// ✅ Luon boc ⇒ **mot quy tac duy nhat**, de kiem va de tin.
    /// </summary>
    public static string Boc(string? giaTri)
    {
        if (giaTri is null)
        {
            // ⚠️ `null` ghi thanh **o trong** (khong phai chu "null") — 🔑 neu ghi "null" thi nguoi doc
            // **khong phan biet duoc** *"khong co du lieu"* voi *"du lieu la chu null"*.
            return "\"\"";
        }

        return "\"" + giaTri.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// 🔑 **DUNG MOT DONG CSV** tu cac truong — dung `Boc` cho **MOI** truong.
    /// ⚠️ **KHONG** tu ghep chuoi o noi goi: 📌 do la cach mot truong bi bo quen boc.
    /// </summary>
    public static string Dong(params string?[] cacTruong)
        => string.Join(",", cacTruong.Select(Boc));

    /// <summary>
    /// ⛔ **DONG CANH BAO KHI BI CAT** — 🔑 **BAT BUOC** phai nam **TRONG TEP** (khong chi tren man hinh).
    ///
    /// 🔒 **VI SAO:** tep CSV la thu **roi khoi he thong** — no duoc **gui, luu, va dinh kem bao cao**. ⚠️ Mot
    /// tep **im lang cat bot** se duoc doc o **mot noi khac, vao mot thoi diem khac, boi mot nguoi khac** —
    /// 📌 va nguoi do **khong co cach nao** biet rang bang chung da bi cat.
    /// 🔑 Do la **chinh xac** kieu **ket luan sai ve bang chung** ma ca §22–§24 dang chong.
    /// </summary>
    public static string DongCanhBaoCat(int dangHienThi, int tongSo, DateTime xuatLucUtc)
        => Dong(
            "!!! CANH BAO: TEP NAY DA BI CAT BOT !!!",
            $"Chi chua {dangHienThi} tren tong {tongSo} lan xuat.",
            $"Xuat luc (UTC): {xuatLucUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
            "Thu hep bo loc (ngay / nguoi xuat) roi xuat lai de lay phan con lai.");

    /// <summary>
    /// 🔑 **DONG TIEN DE** — ai xuat tep, luc nao, va **TEP KHONG CHUA PII**.
    /// 📌 Cung ly do nhu dong canh bao: tep **roi khoi he thong** ⇒ 🔒 phai **tu mo ta** duoc no la gi.
    /// </summary>
    public static string DongTienDe(string nguoiXuat, DateTime xuatLucUtc, int dangHienThi, int tongSo)
        => Dong(
            "NHAT KY KIEM TOAN XUAT DU LIEU CA NHAN (Luat 91/2025/QH15)",
            $"Nguoi xuat tep: {nguoiXuat}",
            $"Xuat luc (UTC): {xuatLucUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
            $"Dang hien thi: {dangHienThi} / tong {tongSo} lan xuat",
            "Tep nay CHI ghi SO LIEU (so dong ho so, so don) - KHONG chua noi dung du lieu ca nhan.");
}
