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
    /// 🔒 **DẤU PHÂN CÁCH MẶC ĐỊNH: `,`** — theo chuẩn **RFC 4180**.
    /// 📌 Đây là giá trị **cũ**, giữ nguyên để không đổi ngầm định dạng của mọi tệp đã/đang xuất.
    /// </summary>
    public const char PhanCachPhay = ',';

    /// <summary>
    /// 🚨 **DẤU PHÂN CÁCH THAY THẾ: `;`** — cho **Excel theo locale coi `,` là dấu THẬP PHÂN**
    /// (tiếng Việt, tiếng Đức, tiếng Pháp…).
    ///
    /// 🔑 **VÌ SAO PHẢI CÓ (nợ THẬT, đóng ở `S10`):** ở các locale đó, Excel dùng `;` làm **dấu ngăn
    /// cách danh sách** ⇒ 🔒 một tệp phân cách bằng `,` được mở ra thành **ĐÚNG MỘT CỘT** (toàn bộ dòng
    /// nằm trong một ô). ⚠️ Và **BOM không cứu được** điều này — nó chỉ giải quyết **bảng mã**, không
    /// giải quyết **dấu phân cách**. 📌 Ở tệp **bằng chứng kiểm toán**, hậu quả là người đọc **kết luận
    /// sai về bằng chứng** (đúng loại lỗi mà cả `CUS-001` §22–§27 đang chống).
    ///
    /// ⚠️ **VÌ SAO KHÔNG ĐỔI NGẦM ĐỊNH THÀNH `;`:** 🔒 đổi ngầm định là **phá** mọi trình đọc đang đúng
    /// (RFC 4180, `CsvNhatKyTests`, các ca E2E `D15*`) và **đổi nghĩa** những tệp đã xuất trước đó.
    /// ✅ Cách đúng: **người dùng CHỌN** — màn hình có **hai** liên kết, và **tên tệp ghi rõ** bản nào.
    /// </summary>
    public const char PhanCachChamPhay = ';';

    /// <summary>
    /// Giá trị tham số `phanCach` trên URL cho **dấu phẩy**.
    /// 📌 Dùng **chuỗi chữ** (không dùng chính ký tự `;`) — ⚠️ `;` trong query string là thứ một số
    /// tầng (proxy, bộ định tuyến) cắt thành **dấu ngăn tham số** ⇒ 🔒 nguy cơ tham số bị **mất im lặng**
    /// (bẫy quen thuộc của dự án: `?` và `&` trong PowerShell/URL).
    /// </summary>
    public const string ThamSoPhay = "phay";

    /// <summary>Giá trị tham số `phanCach` trên URL cho **dấu chấm phẩy**.</summary>
    public const string ThamSoChamPhay = "cham-phay";

    /// <summary>
    /// Đọc tham số `phanCach` từ URL ⇒ dấu phân cách.
    /// 🚨 **Trả `null` khi giá trị KHÔNG hợp lệ** (⚠️ **không** im lặng rơi về dấu phẩy): 🔒 rơi im lặng
    /// nghĩa là một lần gõ sai (`?phanCach=champhay`) tạo ra **tệp SAI** mà **không ai biết** — 📌 và
    /// người kiểm toán sẽ **tin** rằng họ đang cầm bản `;`.
    /// 📌 `null`/rỗng = **không truyền** ⇒ dấu phẩy (hành vi cũ, tương thích ngược).
    /// </summary>
    public static char? DocPhanCach(string? thamSo)
    {
        if (string.IsNullOrWhiteSpace(thamSo))
        {
            return PhanCachPhay;
        }

        var giaTri = thamSo.Trim();

        if (string.Equals(giaTri, ThamSoPhay, StringComparison.OrdinalIgnoreCase))
        {
            return PhanCachPhay;
        }

        if (string.Equals(giaTri, ThamSoChamPhay, StringComparison.OrdinalIgnoreCase))
        {
            return PhanCachChamPhay;
        }

        return null;
    }

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
    /// 🔑 **DỰNG MỘT DÒNG CSV** từ các trường — dùng `Boc` cho **MỌI** trường, phân cách bằng **dấu phẩy**
    /// (RFC 4180, hành vi **cũ**).
    /// ⚠️ **KHÔNG** tự ghép chuỗi ở nơi gọi: 📌 đó là cách một trường bị bỏ quên bọc.
    /// </summary>
    public static string Dong(params string?[] cacTruong) => DongVoi(PhanCachPhay, cacTruong);

    /// <summary>
    /// 🔑 **DỰNG MỘT DÒNG CSV VỚI DẤU PHÂN CÁCH CHỌN ĐƯỢC** — `,` (RFC 4180) hoặc `;` (Excel locale VN).
    /// 🔒 **Chỉ có MỘT quy tắc bọc** (`Boc` luôn bọc) cho **cả hai** dấu phân cách: ⚠️ nếu mỗi chế độ có
    /// cách bọc riêng thì *"luật chỉ một chỗ"* của lớp này **mất hiệu lực**, 📌 và lỗi chỉ hiện ở **một**
    /// trong hai chế độ — đúng loại lỗi không tái hiện được.
    /// </summary>
    public static string DongVoi(char phanCach, params string?[] cacTruong)
    {
        // ⚠️ Chặn dấu phân cách nằm ngoài hai giá trị đã biết: 🔒 một dấu lạ (vd. `|`) làm tệp **hợp lệ
        // về cú pháp** nhưng **không trình đọc nào** hiểu ⇒ 📌 cùng họ với "tệp bằng chứng đọc sai".
        if (phanCach != PhanCachPhay && phanCach != PhanCachChamPhay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phanCach), phanCach, "Dau phan cach chi duoc la ',' (RFC 4180) hoac ';' (Excel locale VN).");
        }

        return string.Join(phanCach, cacTruong.Select(Boc));
    }

    /// <summary>
    /// ⛔ **DONG CANH BAO KHI BI CAT** — 🔑 **BAT BUOC** phai nam **TRONG TEP** (khong chi tren man hinh).
    ///
    /// 🔒 **VI SAO:** tep CSV la thu **roi khoi he thong** — no duoc **gui, luu, va dinh kem bao cao**. ⚠️ Mot
    /// tep **im lang cat bot** se duoc doc o **mot noi khac, vao mot thoi diem khac, boi mot nguoi khac** —
    /// 📌 va nguoi do **khong co cach nao** biet rang bang chung da bi cat.
    /// 🔑 Do la **chinh xac** kieu **ket luan sai ve bang chung** ma ca §22–§24 dang chong.
    /// </summary>
    public static string DongCanhBaoCat(int dangHienThi, int tongSo, DateTime xuatLucUtc, char phanCach = PhanCachPhay)
        => DongVoi(
            phanCach,
            "!!! CANH BAO: TEP NAY DA BI CAT BOT !!!",
            $"Chi chua {dangHienThi} tren tong {tongSo} lan xuat.",
            $"Xuat luc (UTC): {xuatLucUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
            "Thu hep bo loc (ngay / nguoi xuat) roi xuat lai de lay phan con lai.");

    /// <summary>
    /// 🔑 **DONG TIEN DE** — ai xuat tep, luc nao, va **TEP KHONG CHUA PII**.
    /// 📌 Cung ly do nhu dong canh bao: tep **roi khoi he thong** ⇒ 🔒 phai **tu mo ta** duoc no la gi.
    /// </summary>
    public static string DongTienDe(string nguoiXuat, DateTime xuatLucUtc, int dangHienThi, int tongSo, char phanCach = PhanCachPhay)
        => DongVoi(
            phanCach,
            "NHAT KY KIEM TOAN XUAT DU LIEU CA NHAN (Luat 91/2025/QH15)",
            $"Nguoi xuat tep: {nguoiXuat}",
            $"Xuat luc (UTC): {xuatLucUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
            $"Dang hien thi: {dangHienThi} / tong {tongSo} lan xuat",
            // 🔑 **TỰ KHAI DẤU PHÂN CÁCH** — 📌 tệp **rời khỏi hệ thống** ⇒ người nhận phải biết đọc nó
            // bằng dấu nào; ⚠️ đoán sai `,` ↔ `;` là **toàn bộ tệp thành MỘT cột** (nợ `S10`).
            $"Dau phan cach (delimiter): {MoTaPhanCach(phanCach)}",
            "Tep nay CHI ghi SO LIEU (so dong ho so, so don) - KHONG chua noi dung du lieu ca nhan.");

    /// <summary>Mô tả **đọc được** của một dấu phân cách — dùng trong dòng tiền đề và tên tệp.</summary>
    public static string MoTaPhanCach(char phanCach) => phanCach == PhanCachChamPhay
        ? "cham-phay (;) - dung cho Excel locale dung ',' lam dau thap phan (vd tieng Viet)"
        : "dau-phay (,) - chuan RFC 4180";

    /// <summary>
    /// Hậu tố **tên tệp** theo dấu phân cách — 🔒 rỗng cho dấu phẩy (giữ nguyên tên tệp **cũ**), và
    /// `-cham-phay` cho dấu chấm phẩy.
    /// 🔑 **VÌ SAO PHẢI CÓ:** hai tệp **cùng nội dung khác dấu phân cách** mà **cùng tên** thì khi được
    /// **tải về cùng một thư mục** trình duyệt thêm `(1)` ⇒ 📌 không còn cách nào biết **bản nào là bản nào**
    /// — đúng loại lỗi bằng chứng mà §22–§27 đang chống.
    /// </summary>
    public static string HauToTenTep(char phanCach) => phanCach == PhanCachChamPhay ? "-cham-phay" : string.Empty;
}
