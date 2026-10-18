using System.Globalization;
using System.Text;
using Volo.Abp;

namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// 🚨 **HÀM SINH `MerchantReference` (`vnp_TxnRef`) — ỔN ĐỊNH, TẤT ĐỊNH, KHÔNG NGẪU NHIÊN** (`S4`).
///
/// **VÌ SAO CẦN HÀM RIÊNG (nợ #1 của `PAY-001` §7.5):** trước `S4`, `new PaymentTransaction(...)` **chỉ** có
/// trong test ⇒ `MerchantReference` **không có chỗ nào sinh** ở đường sản phẩm. Cách "tiện" nhất — và **SAI
/// KÉP** — là `Guid.NewGuid()` mỗi lần gọi cổng:
/// <list type="number">
///   <item>**mất idempotency**: cùng một ý định thu tiền sinh **N** mã khác nhau ⇒ hệ thống **không** nhận ra
///     *"đây là lần thử lại của cùng một giao dịch"* ⇒ khách bị **thu hai lần** mà đối soát **không** phát hiện
///     (`XC-002` **BR-007**);</item>
///   <item>**vi phạm yêu cầu cổng**: VNPay yêu cầu `vnp_TxnRef` **không trùng trong một ngày**
///     (`VnPayPaymentProvider.cs:42`) ⇒ mã ngẫu nhiên vừa **không** ổn định, vừa **có nguy cơ trùng**.</item>
/// </list>
///
/// 🔑 **Hai tính chất phải giữ (và đều có test):**
///   (1) **TẤT ĐỊNH** — cùng `(số đơn, ngày, lần thử)` ⇒ **cùng một mã**. Không đọc DB, không đọc `Clock`,
///       không dùng `Random`/`Guid` ⇒ gọi 2 lần trong hai tiến trình vẫn ra một mã;
///       (2) **≤ 64 ký tự** — đúng `HasMaxLength(64)` của cột `PayTransactions.MerchantReference`
///       (`PayHubDbContextModelCreatingExtensions`), vì VNPay cho tới 100 nhưng **cột của ta** chỉ 64.
///
/// 📌 **Tiền tố NGÀY là bắt buộc** (quy tắc *"không trùng trong ngày"*), và ngày lấy theo **GMT+7** — cùng
/// múi giờ mà VNPay dùng để ký (`VnPayPaymentProvider.cs:96`). ⚠️ Lấy ngày UTC sẽ lệch **7 tiếng**: giao dịch
/// tạo lúc 20:00 GMT+7 ngày 18 sẽ mang ngày **17** ⇒ *"ngày"* của ta khác *"ngày"* của cổng.
///
/// ⚠️ **Hàm THUẦN (không I/O)** — cùng khuôn `LuatDoiSoatThanhToan.Quyet`: luật tiền tệ phải **test được**
/// mà không cần DB, cổng, hay `Clock`.
/// </summary>
public static class SinhMaThamChieu
{
    /// <summary>Độ dài tối đa — **khớp** `HasMaxLength(64)` của `MerchantReference` trong mapping EF.</summary>
    public const int DoDaiToiDa = 64;

    /// <summary>
    /// Trần độ dài phần **thân** (số đơn đã chuẩn hoá) — 🔑 **CỐ ĐỊNH 40**, không phụ thuộc số lần thử.
    ///
    /// **VÌ SAO PHẢI CỐ ĐỊNH (không phải "tính theo chỗ còn lại"):** hệ thống cần **đếm số lần thử đã có**
    /// bằng `MerchantReference.StartsWith(gốc)` (§ hàm <see cref="Goc"/>). Nếu độ dài thân đổi theo số lần thử
    /// (lần 1 ngắn, lần 10 dài) thì **gốc không còn là tiền tố** của mã cũ ⇒ phép đếm trả `0` ⇒ lần thử lại
    /// **trùng mã** ⇒ đụng unique index (`23505`) thay vì tạo lần thử mới.
    /// 📌 Cố định 40: `PAY`(3) + ngày(8) + `-`(1) + thân(40) + `-`(1) + số thử(≤ 11) ≤ **64**.
    /// </summary>
    public const int DoDaiThanToiDa = 40;

    /// <summary>Tiền tố cố định — nhìn mã là biết đây là giao dịch thanh toán (không lẫn mã đơn/kho).</summary>
    public const string TienTo = "PAY";

    /// <summary>Bộ ký tự được phép: chữ, số và `-`. 🔒 Cấm ký tự có nghĩa trong query string (`&amp;`, `=`, `?`).</summary>
    private const string KyTuChoPhep = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-";

    /// <summary>
    /// **GỐC của mã** = `PAY{yyyyMMdd}-{thân}` (chưa có số lần thử). 🔑 Đây là **tiền tố ỔN ĐỊNH** của mọi lần
    /// thử trong cùng ngày cho cùng một số đơn ⇒ dùng để **đếm số lần thử đã có** (`StartsWith`) mà không cần
    /// thêm cột.
    /// ⚠️ Mã **cuối cùng** luôn là <see cref="Sinh"/> = `gốc + "-" + số lần thử`; **không** dùng `Goc` để gửi cổng.
    /// </summary>
    public static string Goc(string soDon, DateTime thoiDiemUtc)
    {
        var than = ChuanHoa(soDon);

        if (than.Length == 0)
        {
            throw new BusinessException(PaymentErrorCodes.MaThamChieuKhongHopLe)
                .WithData("soDon", soDon)
                .WithData("lyDo", "Số đơn rỗng hoặc chỉ gồm ký tự không dùng được trong mã tham chiếu.");
        }

        var thanCat = than.Length <= DoDaiThanToiDa ? than : than[..DoDaiThanToiDa];

        return $"{TienTo}{NgayGmt7(thoiDiemUtc)}-{thanCat}";
    }

    /// <summary>
    /// Sinh mã tham chiếu **ổn định**: `PAY{yyyyMMdd}-{sốĐơnĐãChuẩnHoá}-{lầnThử}`.
    /// </summary>
    /// <param name="soDon">Số đơn hiển thị (`Order.Number`, ví dụ `WEB-WEB-0123456789abcdef`). Được **chuẩn hoá**
    /// (bỏ ký tự lạ) và **cắt ngắn** cho vừa 64 ký tự.</param>
    /// <param name="thoiDiemUtc">Thời điểm tạo giao dịch (**phải là UTC**; `DateTimeKind.Unspecified` được
    /// **coi là UTC** — 🚨 **KHÔNG** gọi `ToUniversalTime()` lên nó, đúng bẫy #6 của `handover` §5.1:
    /// `Unspecified` bị hiểu là giờ địa phương ⇒ lệch 7 tiếng).</param>
    /// <param name="lanThu">1 = lần đầu; 2, 3… = **lần thử MỚI** sau khi lần trước đã `Failed`/`Cancelled`.
    /// ⚠️ **Retry của CÙNG một lần thử KHÔNG** được tăng số này — đó là toàn bộ ý nghĩa của *"ỔN ĐỊNH"*.</param>
    /// <exception cref="BusinessException">`eCommerce:Payment:0006` khi số đơn rỗng / không còn ký tự dùng được,
    /// hoặc `lanThu &lt; 1`.</exception>
    public static string Sinh(string soDon, DateTime thoiDiemUtc, int lanThu = 1)
    {
        if (lanThu < 1)
        {
            throw new BusinessException(PaymentErrorCodes.MaThamChieuKhongHopLe)
                .WithData("lanThu", lanThu)
                .WithData("lyDo", "Lần thử phải >= 1 (0 hoặc âm là lỗi lập trình, không phải ca nghiệp vụ).");
        }

        var ma = $"{Goc(soDon, thoiDiemUtc)}-{lanThu.ToString(CultureInfo.InvariantCulture)}";

        // 🔒 Hàng rào cuối: mã dài quá cột sẽ bị PostgreSQL **cắt cụt im lặng** (hoặc ném ở tầng ghi) ⇒ đếm được
        // thì phải chặn ở đây, không để lỗi lộ ra ở một chỗ không liên quan.
        if (ma.Length > DoDaiToiDa)
        {
            throw new BusinessException(PaymentErrorCodes.MaThamChieuKhongHopLe)
                .WithData("lanThu", lanThu)
                .WithData("doDai", ma.Length)
                .WithData("lyDo", $"Mã tham chiếu dài {ma.Length} > {DoDaiToiDa} ký tự (giới hạn của cột).");
        }

        return ma;
    }

    /// <summary>
    /// Ngày **GMT+7** dạng `yyyyMMdd`.
    /// 🚨 `DateTimeKind.Unspecified` ⇒ **coi là UTC** (`SpecifyKind`), **KHÔNG** `ToUniversalTime()`: hàm sau
    /// sẽ trừ offset của máy ⇒ lệch 7 tiếng (bẫy đã cắn dự án, `handover` §5.1 #6).
    /// </summary>
    private static string NgayGmt7(DateTime thoiDiem)
    {
        var utc = thoiDiem.Kind switch
        {
            DateTimeKind.Utc => thoiDiem,
            DateTimeKind.Local => thoiDiem.ToUniversalTime(),
            _ => DateTime.SpecifyKind(thoiDiem, DateTimeKind.Utc)
        };

        return utc.AddHours(7).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Chuẩn hoá số đơn về bộ ký tự an toàn — 🔑 **BỎ DẤU tiếng Việt, không XOÁ ký tự**.
    ///
    /// **VÌ SAO KHÔNG ĐƯỢC XOÁ (lỗi thật do test bắt được ngay lượt đầu):** bản đầu chỉ *"bỏ ký tự không hợp lệ"*
    /// ⇒ `ĐƠN-001` thành **`N-001`** (mất `ĐƠ`, còn `N`) ⇒ hai đơn khác nhau (`ĐƠN-001`, `N-001`) **ra cùng một
    /// mã** ⇒ unique index chặn ⇒ *"không tạo được giao dịch"* ở một đơn hoàn toàn bình thường, và **lý do không
    /// ai đoán ra**. Dùng `FormD` + bỏ dấu kết hợp + `Đ→D` cho ra `DON-001` — đúng điều người viết số đơn muốn.
    /// 📌 Ký tự **không phải chữ Latin** (CJK…) vẫn bị bỏ; nếu **toàn bộ** số đơn là như vậy thì hàm **NÉM**
    /// (`0006`) thay vì trả một mã rỗng nghĩa.
    /// </summary>
    private static string ChuanHoa(string? giaTri)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
        {
            return string.Empty;
        }

        // `FormD` tách dấu ra thành ký tự kết hợp (combining mark) ⇒ bỏ chúng là "bỏ dấu"; giữ nguyên chữ cái gốc.
        var daTach = giaTri.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(daTach.Length);

        foreach (var c in daTach)
        {
            if (KyTuChoPhep.IndexOf(c) >= 0)
            {
                sb.Append(c);
                continue;
            }

            // `Đ/đ` KHÔNG tách được bằng `FormD` (nó là chữ cái riêng, không phải D + dấu) ⇒ ánh xạ tay.
            if (c is 'Đ')
            {
                sb.Append('D');
                continue;
            }

            if (c is 'đ')
            {
                sb.Append('d');
                continue;
            }

            // Dấu kết hợp (combining mark) và mọi ký tự khác: bỏ (kể cả khoảng trắng, `&`, `=`, `?`).
        }

        return sb.ToString();
    }
}
