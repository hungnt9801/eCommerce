using System.Text.RegularExpressions;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **CHE DỮ LIỆU CÁ NHÂN (PII) TRƯỚC KHI VÀO LOG** — `CUS-001` **ADR-11**. Hàm **THUẦN**.
///
/// **VÌ SAO PHẢI LÀ HÀM + CÓ TEST QUÉT (không chỉ "nhắc nhau trong review"):**
/// PII của khách (email, SĐT) **sẽ** lọt vào log nếu chỉ dựa vào kỷ luật — và **log là nơi KHÓ XOÁ NHẤT**
/// khi chủ thể dữ liệu yêu cầu xoá (Luật BV dữ liệu cá nhân **91/2025/QH15**, hiệu lực 01/01/2026).
/// 📌 Tiền lệ **đã có trong repo**: `PaymentTransaction.MaskSensitiveData` (che số thẻ/PAN) — cùng lý do.
/// ⇒ Đây là **đường DUY NHẤT** để đưa PII vào log; `TC-11` của `CUS-001` quét mã nguồn để chặn đường vòng.
///
/// 🔒 **Nguyên tắc che: GIỮ ĐỦ ĐỂ ĐỐI CHIẾU, KHÔNG ĐỦ ĐỂ DÙNG.**
/// Nếu che **sạch** (`***`) thì CSKH **không** tra được *"log này của khách nào"* ⇒ họ sẽ tìm cách khác
/// (chụp màn hình, xuất Excel) ⇒ **PII rò rỉ ra ngoài log** — tệ hơn. Vì vậy giữ **vài ký tự đầu/cuối**:
/// <list type="bullet">
///   <item>email: `nguyen.van.a@example.com` → `ngu***@exa***` (giữ phần đầu tên + 3 ký tự miền);</item>
///   <item>SĐT: `0912345678` → `091***678` (giữ 3 đầu + 3 cuối — đủ để khách đọc lại xác nhận).</item>
/// </list>
/// ⚠️ **Đây là GIẢM THIỂU, không phải bảo đảm.** Không bao giờ coi giá trị đã che là *"an toàn để in"*
/// ở mức cho phép suy ngược ra PII đầy đủ (ví dụ email rất ngắn).
/// </summary>
public static class PiiMasker
{
    private const string Che = "***";

    /// <summary>
    /// Che **email**: giữ tối đa **3** ký tự đầu của phần tên + **3** ký tự đầu của miền.
    /// `null`/rỗng ⇒ trả `"(không có)"` (để log **nói ra** là thiếu, thay vì in khoảng trắng khó thấy).
    /// ⚠️ Chuỗi **không** có `@` (dữ liệu bẩn) ⇒ vẫn che như một chuỗi bí ẩn — **không** in nguyên văn.
    /// </summary>
    public static string CheEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "(không có)";
        }

        var s = email.Trim();
        var viTri = s.IndexOf('@');

        if (viTri <= 0)
        {
            // Không phải email hợp lệ ⇒ **KHÔNG** in nguyên văn (đề phòng người dùng gõ SĐT/CCCD vào ô email).
            return LayDau(s, 3) + Che;
        }

        var ten = s[..viTri];
        var mien = s[(viTri + 1)..];

        return LayDau(ten, 3) + Che + "@" + LayDau(mien, 3) + Che;
    }

    /// <summary>
    /// Che **số điện thoại**: giữ **3** chữ số đầu + **3** chữ số cuối (`091***678`).
    /// 📌 Vì sao giữ **cả hai đầu**: CSKH đối chiếu với khách qua **đuôi số** (khách đọc đuôi dễ hơn đầu),
    /// còn **đầu số** cho biết nhà mạng — hữu ích khi tra sự cố gửi tin.
    /// ⚠️ SĐT **ngắn hơn 7 chữ số** ⇒ che **toàn bộ** (không đủ chỗ để giữ mà vẫn vô danh).
    /// </summary>
    public static string CheDienThoai(string? dienThoai)
    {
        if (string.IsNullOrWhiteSpace(dienThoai))
        {
            return "(không có)";
        }

        var s = dienThoai.Trim();
        var chiSo = new string(s.Where(char.IsDigit).ToArray());

        if (chiSo.Length < 7)
        {
            return Che;
        }

        return chiSo[..3] + Che + chiSo[^3..];
    }

    /// <summary>
    /// Che **mọi** email/SĐT tìm thấy trong một **chuỗi bất kỳ** (payload thô, message lỗi, tên tệp…).
    /// 🎯 Dùng cho chỗ **không biết trước** dữ liệu sẽ chứa gì — ví dụ `rawPayload` của webhook sàn,
    /// nơi PII nằm lẫn trong JSON mà ta **không** kiểm soát hình dạng.
    /// 🔒 Hàm **không** ném và **không** trả `null`: đầu vào rỗng ⇒ trả nguyên đầu vào.
    /// </summary>
    public static string? CheTrongChuoi(string? chuoi)
    {
        if (string.IsNullOrEmpty(chuoi))
        {
            return chuoi;
        }

        // ① Email trước (để `@` không bị luật SĐT cắt vào giữa).
        var ketQua = KhuonEmailTrongChuoi.Replace(chuoi, m => CheEmail(m.Value));

        // ② Rồi tới dãy số dài (≥ 8 chữ số, có thể có `+`/khoảng trắng/gạch) — coi là SĐT.
        //    ⚠️ Chỉ thay khi chuỗi **không** nằm trong một email vừa che (email đã thành `***@***` nên không còn `@`).
        ketQua = KhuonSoDienThoaiTrongChuoi.Replace(ketQua, m => CheDienThoai(m.Value));

        return ketQua;
    }

    private static string LayDau(string s, int n) => s.Length <= n ? s : s[..n];

    private static readonly Regex KhuonEmailTrongChuoi =
        new(@"[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.Compiled);

    // 🔒 Đòi **ít nhất 8 chữ số** để tránh cắt vào số tiền/mã đơn (ví dụ `12345678` là mã, không phải SĐT).
    //    📌 Đánh đổi ĐÃ BIẾT: số 8 chữ số là **mã** sẽ bị che ⇒ log khó đọc hơn một chút. Chọn hướng này vì
    //    hậu quả của việc **lọt PII** nặng hơn hậu quả của việc **che nhầm một mã**.
    private static readonly Regex KhuonSoDienThoaiTrongChuoi =
        new(@"\+?\d[\d\s\-]{6,}\d", RegexOptions.Compiled);
}
