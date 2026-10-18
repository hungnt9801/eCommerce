namespace eCommerce.PaymentHub.Payments;

/// <summary>Kết quả **đối chiếu** số tiền của cổng với số tiền đã lập giao dịch (`S4`).</summary>
public enum KetQuaDoiChieu
{
    /// <summary>Số tiền **và** tiền tệ khớp ⇒ được phép chuyển đơn sang `Paid`.</summary>
    Khop = 0,

    /// <summary>🚨 Số tiền **KHÁC** số tiền đã lập giao dịch ⇒ **KHÔNG** được đánh dấu đơn đã trả.</summary>
    LechSoTien = 1,

    /// <summary>🚨 Tiền tệ khác (VND vs USD) ⇒ so sánh số tiền trở nên **vô nghĩa**.</summary>
    LechTienTe = 2
}

/// <summary>
/// 🚨 **LUẬT CẦU NỐI `PaymentTransaction` → `Order.PaymentStatus`** (`S4`) — hàm **THUẦN**, không I/O.
///
/// **VÌ SAO CẦN LUẬT NÀY (nó đóng một phát hiện đã ghi, không phải "cho đủ"):**
/// `PAY-001` **F-PAY-2** — `WebhookVerificationResult.Amount` được **parse** nhưng **không** so với
/// `PaymentTransaction.Amount` ở **bất kỳ đâu**; và `SAL-003` **#12** yêu cầu *"khớp số tiền (+ cùng tiền tệ)
/// mới được `Paid`"*. Chữ ký HMAC bảo vệ payload **không bị sửa**, nhưng nó **không** bảo đảm payload đó thuộc
/// **đúng đơn** này với **đúng số tiền**: `HashSecret` lệch giữa hai môi trường, hoặc một IPN của đơn khác mang
/// cùng `MerchantReference` (do lỗi sinh mã) ⇒ *"khách trả 100k mà đơn 10 triệu vẫn được coi là đã trả"* —
/// **không có lỗi nào nổi lên**.
///
/// 🔒 **BẤT BIẾN #3 của dự án được tôn trọng ở đây:** *cấm so tiền bằng `==`* (`XC-001` **BR-003**) —
/// `decimal.Compare` chứ không `a == b`, và tiền tệ so **không phân biệt hoa/thường**.
///
/// 📌 **Vì sao đặt ở `PaymentHub.Domain` (không ở tầng app, không ở chỗ gọi):** luật này được dùng bởi
/// **cầu nối** (handler ETO, ở `eCommerce.Application`) **và** sẽ được dùng bởi endpoint IPN (`S5`) — hai chỗ
/// khác tầng. Hai bản chép tay sẽ **lệch** khi một bên thêm kiểm tra (đúng họ lỗi *"hai nguồn sự thật"*).
/// </summary>
public static class LuatCauNoiThanhToan
{
    /// <summary>Tiền tệ mặc định khi bên gửi **không** khai (VNPay: `vnp_CurrCode` luôn có, nhưng provider khác thì không).</summary>
    public const string TienTeMacDinh = "VND";

    /// <summary>
    /// Đối chiếu **số tiền + tiền tệ** của cổng với số tiền **đã lập** cho giao dịch.
    /// </summary>
    /// <param name="soTienCong">Số tiền cổng báo (đã chia 100 nếu cổng nhân 100 — tức là **đơn vị tiền thật**).</param>
    /// <param name="tienTeCong">Tiền tệ cổng báo (rỗng/null ⇒ hiểu là <see cref="TienTeMacDinh"/>).</param>
    /// <param name="soTienGiaoDich">`PaymentTransaction.Amount` — số tiền **đã lập** với cổng.</param>
    /// <param name="tienTeGiaoDich">`PaymentTransaction.Currency`.</param>
    public static KetQuaDoiChieu DoiChieu(
        decimal soTienCong,
        string? tienTeCong,
        decimal soTienGiaoDich,
        string? tienTeGiaoDich)
    {
        // 🔒 Tiền tệ TRƯỚC số tiền: so 100 USD với 100 VND rồi báo "khớp" là sai **bản chất**, không chỉ sai số.
        var cuaCong = ChuanHoaTienTe(tienTeCong);
        var cuaTa = ChuanHoaTienTe(tienTeGiaoDich);

        if (!string.Equals(cuaCong, cuaTa, StringComparison.OrdinalIgnoreCase))
        {
            return KetQuaDoiChieu.LechTienTe;
        }

        // 🚨 KHÔNG dùng `==` cho tiền (`XC-001` BR-003).
        return decimal.Compare(soTienCong, soTienGiaoDich) == 0
            ? KetQuaDoiChieu.Khop
            : KetQuaDoiChieu.LechSoTien;
    }

    private static string ChuanHoaTienTe(string? tienTe)
        => string.IsNullOrWhiteSpace(tienTe) ? TienTeMacDinh : tienTe.Trim();
}
