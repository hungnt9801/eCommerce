using eCommerce.PaymentHub.Payments;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// Test cho **B(c)** — che dữ liệu nhạy cảm trong payload webhook (phát hiện F2 của `XC-005`).
///
/// 🎯 Vì sao phải có test: đây là biện pháp **giảm rò rỉ dữ liệu** (số thẻ/token nằm trong DB và
/// rất dễ bị ghi tiếp ra log). Nếu ai sửa hàm che mà không có test bắt, lỗi sẽ **im lặng** —
/// payload nhạy cảm lại được lưu nguyên.
/// </summary>
public class WebhookMaskingTests
{
    [Fact]
    public void Che_cac_truong_co_ten_nhay_cam()
    {
        var payload = """{"vnp_ResponseCode":"00","password":"BiMat123","cardNumber":"9704229200000001","token":"abcxyz"}""";

        var kq = PaymentTransaction.MaskSensitiveData(payload);

        Assert.DoesNotContain("BiMat123", kq);
        Assert.DoesNotContain("9704229200000001", kq);
        Assert.DoesNotContain("abcxyz", kq);
        // Vẫn giữ phần đối soát được.
        Assert.Contains("vnp_ResponseCode", kq);
    }

    [Fact]
    public void Che_so_the_12_19_chu_so_va_giu_4_so_cuoi()
    {
        var payload = """{"note":"the 9704 2292 0000 0001 cua khach"}""";

        var kq = PaymentTransaction.MaskSensitiveData(payload);

        Assert.DoesNotContain("9704 2292 0000 0001", kq);
        Assert.Contains("0001", kq);   // 4 số cuối giữ lại để đối soát
        Assert.Contains("***", kq);
    }

    [Fact]
    public void Payload_khong_nhay_cam_thi_giu_nguyen()
    {
        var payload = """{"vnp_ResponseCode":"00","vnp_Amount":"10000000"}""";

        var kq = PaymentTransaction.MaskSensitiveData(payload);

        Assert.Equal(payload, kq);
    }

    [Fact]
    public void Payload_rong_hoac_null_khong_lam_vo()
    {
        Assert.Null(PaymentTransaction.MaskSensitiveData(null));
        Assert.Equal(string.Empty, PaymentTransaction.MaskSensitiveData(string.Empty));
    }

    [Fact]
    public void MarkSucceeded_luu_payload_DA_CHE()
    {
        var gd = new PaymentTransaction(Guid.NewGuid(), "vnpay", "REF-1", 100m);

        gd.MarkSucceeded("PT-1", "IDEM-1", """{"cardNumber":"9704229200000001","secret":"abc"}""", null);

        Assert.NotNull(gd.LastWebhookPayload);
        Assert.DoesNotContain("9704229200000001", gd.LastWebhookPayload);
        Assert.DoesNotContain("abc", gd.LastWebhookPayload);
    }
}
