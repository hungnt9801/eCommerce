using eCommerce.PaymentHub.Payments;
using Volo.Abp;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **BẤT BIẾN KHI TẠO GIAO DỊCH + KIỂU TRẢ VỀ CỦA `MarkFailed`** (`S4`).
///
/// **HAI NỢ ĐÃ GHI ĐƯỢC ĐÓNG Ở ĐÂY:**
/// <list type="number">
///   <item>`PAY-001` §3.1 dòng 1 — *"constructor **không** kiểm ⇒ `amount ≤ 0` hoặc mã rỗng **lọt**"*.
///     📌 Test dưới đây khoá lại bằng **chiều ngược**: giá trị **hợp lệ** phải tạo được (nếu không thì "chặn
///     tất cả" cũng qua được test một chiều);</item>
///   <item>`PAY-001` §3.2 / `F-PAY-9` — `MarkFailed` trả **`void`** nên chỗ gọi **không thể biết** đã ghi hay bị
///     bỏ ⇒ job đối soát **đếm sai** *"đã đổi trạng thái"*. Nay trả `bool`; test khoá **cả hai** giá trị.</item>
/// </list>
/// </summary>
public class PaymentTransactionTaoMoiTests
{
    private const string Ma = "PAY20261018-DH001-1";

    // ── (1) SỐ TIỀN ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-250_000)]
    public void So_tien_khong_duong__NEM__0007(decimal soTien)
    {
        var loi = Assert.Throws<BusinessException>(
            () => new PaymentTransaction(Guid.NewGuid(), "vnpay", Ma, soTien));

        Assert.Equal(PaymentErrorCodes.SoTienGiaoDichKhongHopLe, loi.Code);
    }

    [Fact]
    public void So_tien_hop_le__TAO_DUOC__va_o_trang_thai_Initiated()
    {
        // 🔑 Chiều NGƯỢC của test trên: nếu constructor chặn **mọi** giá trị thì test "ném" vẫn xanh —
        // tức là một cổng luôn đỏ/ luôn xanh đều qua được phép kiểm một chiều.
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", Ma, 0.01m);

        Assert.Equal(0.01m, gd.Amount);
        Assert.Equal(PaymentTransactionStatus.Initiated, gd.Status);
        Assert.Equal(Ma, gd.MerchantReference);
    }

    // ── (2) MÃ THAM CHIẾU / MÃ CỔNG ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ma_tham_chieu_rong__NEM__0006(string ma)
    {
        var loi = Assert.Throws<BusinessException>(
            () => new PaymentTransaction(Guid.NewGuid(), "vnpay", ma, 100_000m));

        Assert.Equal(PaymentErrorCodes.MaThamChieuKhongHopLe, loi.Code);
    }

    [Fact]
    public void Ma_cong_rong__NEM__0006()
    {
        var loi = Assert.Throws<BusinessException>(
            () => new PaymentTransaction(Guid.NewGuid(), "  ", Ma, 100_000m));

        Assert.Equal(PaymentErrorCodes.MaThamChieuKhongHopLe, loi.Code);
    }

    [Fact]
    public void Ma_duoc_TRIM__khong_luu_khoang_trang_thua()
    {
        // Khoảng trắng thừa trong `MerchantReference` là lỗi **im lặng**: unique index vẫn chặn được, nhưng IPN
        // của cổng (`vnp_TxnRef` không có khoảng trắng) sẽ **không tra thấy** giao dịch.
        var gd = new PaymentTransaction(Guid.NewGuid(), " vnpay ", $" {Ma} ", 100_000m);

        Assert.Equal("vnpay", gd.ProviderCode);
        Assert.Equal(Ma, gd.MerchantReference);
    }

    // ── (3) `MarkFailed` TRẢ `bool` — đóng F-PAY-9 ─────────────────────────────────────────────

    [Fact]
    public void MarkFailed__lan_dau_TRA_TRUE__lan_hai_TRA_FALSE()
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", Ma, 100_000m);

        Assert.True(gd.MarkFailed("Cổng báo lỗi thẻ.", "{}"));
        Assert.Equal(PaymentTransactionStatus.Failed, gd.Status);
        Assert.Equal("Cổng báo lỗi thẻ.", gd.FailureReason);

        // ⚠️ Lần hai **vẫn** đếm webhook (đó là bằng chứng IPN bị lặp) và **vẫn** cập nhật lý do (lý do MỚI NHẤT
        // là lý do CSKH cần đọc) — nhưng **trạng thái không đổi** ⇒ trả `false`.
        // 🔑 Nếu trả `true` ở đây thì con số *"đã đổi trạng thái"* của job đối soát lại sai — đúng `F-PAY-9`,
        // chỉ ở dạng tinh vi hơn (*"ghi lại cùng một giá trị"* trông y như một chuyển dịch).
        Assert.False(gd.MarkFailed("Gửi lại: khách huỷ ở cổng.", "{}"));
        Assert.Equal(2, gd.WebhookReceivedCount);
        Assert.Equal("Gửi lại: khách huỷ ở cổng.", gd.FailureReason);
        Assert.Equal(PaymentTransactionStatus.Failed, gd.Status);
    }

    [Fact]
    public void MarkFailed__tren_giao_dich_DA_THU_TIEN__TRA_FALSE__va_KHONG_doi_gi()
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", Ma, 100_000m);
        gd.MarkSucceeded("PN-1", "key-1", "{}", DateTime.UtcNow);

        Assert.False(gd.MarkFailed("Cổng báo lỗi đến MUỘN."));

        // 🔒 Nếu `MarkFailed` ghi đè thì `RefundedAmount`/`PaidAtUtc` vẫn còn trong khi `Status` nói "chưa từng
        // thu" ⇒ kế toán không đối chiếu được (`PAY-001` §3.2).
        Assert.Equal(PaymentTransactionStatus.Succeeded, gd.Status);
        Assert.Null(gd.FailureReason);
    }

    [Fact]
    public void MarkSucceeded__lan_hai_TRA_FALSE__nhung_VAN_dem_webhook()
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", Ma, 100_000m);

        Assert.True(gd.MarkSucceeded("PN-1", "key-1", "{}", null));
        Assert.False(gd.MarkSucceeded("PN-1", "key-1", "{}", null));

        // 🔑 Con số này là **dữ liệu** (không phải log) để trả lời *"IPN có bị lặp không"* bằng SQL.
        Assert.Equal(2, gd.WebhookReceivedCount);
    }
}
