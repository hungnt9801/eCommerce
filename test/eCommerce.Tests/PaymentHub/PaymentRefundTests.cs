using eCommerce.PaymentHub.Payments;
using Volo.Abp;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **BẤT BIẾN CỦA VIỆC HOÀN TIỀN** (`SAL-006`) — chỗ **dễ mất tiền thật** nhất của miền thanh toán.
///
/// **VÌ SAO CẦN TEST NÀY (lỗi THẬT đã có trong mã, không phải phòng xa):** phiên bản trước của
/// `RegisterRefund` là <c>RefundedAmount += amount;</c> — **không kiểm gì**. Hệ quả:
/// <list type="bullet">
///   <item>hoàn **vượt** số đã thu **LỌT** ⇒ sổ ghi *"đã hoàn nhiều hơn đã thu"* mà **không lỗi nào**;</item>
///   <item>status nhảy sang `Refunded` ⇒ **kế toán tin là xong**;</item>
///   <item>một lỗi gọi lặp/double-click ⇒ hoàn **hai lần** ⇒ **tiền ra khỏi tài khoản** mà sổ nội bộ không thấy.</item>
/// </list>
/// 📌 Đây đúng họ lỗi dự án đã trả giá **hai lần** (`SAL-004` `0040` · `INV-003` `0042`):
/// **kiểm tra thiếu ở tầng domain thì DB không cứu được** (không có `CHECK` nào nói *"hoàn ≤ thu"*
/// vì nó là bất biến **liên cột theo thời gian**, không phải một hàng).
/// </summary>
public class PaymentRefundTests
{
    private static PaymentTransaction DaThu(decimal amount = 500_000m, string ma = "MR-001")
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", ma, amount);
        gd.MarkSucceeded("PN-1", "key-1", "{}", DateTime.UtcNow);
        return gd;
    }

    // ── (1) HOÀN VƯỢT SỐ ĐÃ THU — ca quan trọng nhất ────────────────────────────────────────────

    [Fact]
    public void Hoan_VUOT_so_da_thu__NEM__va_KHONG_doi_trang_thai()
    {
        var gd = DaThu(500_000m);

        var loi = Assert.Throws<BusinessException>(() => gd.RegisterRefund(600_000m));

        Assert.Equal(PaymentErrorCodes.HoanVuotSoDaThu, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": entity PHẢI nguyên vẹn sau khi ném, nếu không thì một
        // `SaveChanges` vì việc khác sẽ ghi số sai xuống DB.
        Assert.Equal(0m, gd.RefundedAmount);
        Assert.Equal(PaymentTransactionStatus.Succeeded, gd.Status);
    }

    [Fact]
    public void Hoan_NHIEU_LAN_cong_don_VUOT__lan_cuoi_NEM__cac_lan_truoc_giu_nguyen()
    {
        var gd = DaThu(500_000m);

        gd.RegisterRefund(200_000m);
        gd.RegisterRefund(200_000m);

        Assert.Equal(400_000m, gd.RefundedAmount);
        Assert.Equal(PaymentTransactionStatus.PartiallyRefunded, gd.Status);

        // Lần 3: 400k + 200k = 600k > 500k ⇒ NÉM. 📌 Ca này mô phỏng **double-click/gọi lặp** — đúng thứ
        // làm tiền ra khỏi tài khoản hai lần nếu không có bất biến cộng dồn.
        var loi = Assert.Throws<BusinessException>(() => gd.RegisterRefund(200_000m));

        Assert.Equal(PaymentErrorCodes.HoanVuotSoDaThu, loi.Code);
        Assert.Equal(400_000m, gd.RefundedAmount);   // KHÔNG bị cộng thêm
        Assert.Equal(PaymentTransactionStatus.PartiallyRefunded, gd.Status);
    }

    [Fact]
    public void Hoan_DUNG_BANG_so_da_thu__DUOC__va_chuyen_Refunded()
    {
        var gd = DaThu(500_000m);

        gd.RegisterRefund(500_000m);

        Assert.Equal(500_000m, gd.RefundedAmount);
        Assert.Equal(PaymentTransactionStatus.Refunded, gd.Status);
        Assert.Equal(0m, gd.ConHoanDuoc);
    }

    [Fact]
    public void Hoan_MOT_PHAN__chuyen_PartiallyRefunded__va_ConHoanDuoc_giam_dung()
    {
        var gd = DaThu(500_000m);

        gd.RegisterRefund(150_000m);

        Assert.Equal(PaymentTransactionStatus.PartiallyRefunded, gd.Status);
        Assert.Equal(350_000m, gd.ConHoanDuoc);
    }

    // ── (2) TRẠNG THÁI: không hoàn được tiền CHƯA THU ───────────────────────────────────────────

    [Theory]
    [InlineData(PaymentTransactionStatus.Initiated)]
    [InlineData(PaymentTransactionStatus.Pending)]
    [InlineData(PaymentTransactionStatus.Failed)]
    public void Chua_thu_duoc_tien__KHONG_hoan_duoc(PaymentTransactionStatus trangThai)
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", "MR-002", 500_000m);

        // ⚠️ Đường DUY NHẤT để entity thành `Failed` là `MarkFailed`. `Initiated` là trạng thái ban đầu
        // (không cần gọi gì). `Pending` hiện **chưa có method setter** ⇒ không đặt được từ ngoài.
        if (trangThai == PaymentTransactionStatus.Failed)
        {
            gd.MarkFailed("cổng từ chối");
        }

        // 🚨 Hoàn cho giao dịch CHƯA thu = **chi tiền chưa từng nhận**. Nếu ca này KHÔNG ném thì
        // hệ thống có đường "cho tiền ra" mà không cần tiền vào — mất tiền thật, không log.
        var loi = Assert.Throws<BusinessException>(() => gd.RegisterRefund(100_000m));

        Assert.Equal(PaymentErrorCodes.ChuaThuDuocTienKhongHoanDuoc, loi.Code);
        Assert.Equal(0m, gd.RefundedAmount);
    }

    // ── (3) SỐ TIỀN KHÔNG HỢP LỆ ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500_000)]
    public void So_tien_hoan_KHONG_DUONG__NEM(decimal soTien)
    {
        var gd = DaThu(500_000m);

        var loi = Assert.Throws<BusinessException>(() => gd.RegisterRefund(soTien));

        Assert.Equal(PaymentErrorCodes.SoTienHoanKhongHopLe, loi.Code);
        Assert.Equal(0m, gd.RefundedAmount);
    }

    // ── (4) Ba mã lỗi phải KHÁC NHAU (chống "một mã cho mọi ca") ────────────────────────────────

    [Fact]
    public void Ba_ma_loi_PHAI_KHAC_NHAU__vi_moi_ca_can_mot_chi_dan_khac()
    {
        // 📌 Bài học `INV-003` `0042`: mượn mã khác nghĩa ⇒ CSKH nhận **chỉ dẫn sai**.
        // Ba ca dưới đây cần ba hành động KHÁC NHAU: kiểm số tiền · kiểm TRẠNG THÁI · sửa tham số.
        var ba = new[] { PaymentErrorCodes.HoanVuotSoDaThu, PaymentErrorCodes.ChuaThuDuocTienKhongHoanDuoc, PaymentErrorCodes.SoTienHoanKhongHopLe };

        Assert.Equal(3, ba.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ba, x => Assert.StartsWith("eCommerce:Payment:", x, StringComparison.Ordinal));
        Assert.DoesNotContain(ba, x => x == "eCommerce:Payment:0001");   // 0001 = chữ ký sai, KHÁC nghĩa
    }

    // ── (5) Idempotent webhook vẫn đúng sau khi thêm guard ──────────────────────────────────────

    [Fact]
    public void Webhook_gui_lai_sau_khi_da_Refunded__KHONG_doi_trang_thai_ve_Succeeded()
    {
        var gd = DaThu(500_000m);
        gd.RegisterRefund(500_000m);
        Assert.Equal(PaymentTransactionStatus.Refunded, gd.Status);

        var coDoi = gd.MarkSucceeded("PN-1", "key-1", "{}", DateTime.UtcNow);

        // 🔒 MarkSucceeded hiện coi `Succeeded` là trạng thái cuối; `Refunded` **cũng** phải là cuối —
        // nếu không, webhook gửi lại sẽ "hồi sinh" một giao dịch ĐÃ HOÀN TIỀN ⇒ sổ sách mâu thuẫn.
        Assert.False(coDoi);
        Assert.Equal(PaymentTransactionStatus.Refunded, gd.Status);
        Assert.Equal(500_000m, gd.RefundedAmount);
    }
}
