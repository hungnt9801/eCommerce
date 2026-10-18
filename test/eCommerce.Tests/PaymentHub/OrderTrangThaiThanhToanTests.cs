using eCommerce.Orders;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **LUẬT HẤP THỤ CỦA TRỤC TÀI CHÍNH TRÊN ĐƠN** (`S4`) — `Order.DatTrangThaiThanhToan`.
///
/// **VÌ SAO CẦN (đây là lỗi **sẽ** xảy ra, không phải giả định):** IPN là **at-least-once** và hai luồng có thể
/// cùng ghi. Nếu trục tài chính của đơn là setter công khai (như trước `S4`) thì một IPN **thất bại đến muộn**
/// sẽ **hạ** đơn từ `Paid` về `Failed` ⇒ hệ thống tưởng khách **chưa** trả ⇒ **thu tiền lần hai** hoặc
/// **không giao hàng**. 📌 Đây đúng lỗi mà phía **giao dịch** đã phải sửa ngày 16/10/2026
/// (`PaymentTransaction.MarkSucceeded`/`MarkFailed`); lệch luật giữa hai phía là mầm sổ sách tự mâu thuẫn.
/// 🔒 Mỗi ca dưới đây kiểm **cả hai chiều**: trạng thái **sau** hàm và **giá trị trả về** (vì chỗ gọi dựa vào
/// giá trị trả về để quyết định có ghi DB hay không — đúng bài học `F-PAY-9`).
/// </summary>
public class OrderTrangThaiThanhToanTests
{
    private static Order DonMoi() => new(Guid.NewGuid(), "WEB-DH-001", "web");

    [Fact]
    public void Don_moi__mac_dinh_Unpaid()
    {
        Assert.Equal(PaymentStatus.Unpaid, DonMoi().PaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    public void Chua_co_tien__di_duoc_moi_huong(PaymentStatus moi)
    {
        var don = DonMoi();

        Assert.True(don.DatTrangThaiThanhToan(moi));
        Assert.Equal(moi, don.PaymentStatus);
    }

    [Fact]
    public void Failed__sang_Paid__DUOC__luoi_an_toan_querydr()
    {
        // 🔑 Ca sống còn: IPN mất ⇒ `querydr` phát hiện tiền **đã** vào ⇒ `Failed → Paid` **phải** đi được,
        // nếu không thì giao dịch đã thu tiền mà đơn **vĩnh viễn** `Failed`.
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Failed);

        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.Paid));
        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
    }

    [Fact]
    public void Paid__sang_Failed__BI_CHAN__va_tra_FALSE()
    {
        // 🚨 Ca nguy hiểm nhất: IPN thất bại đến sau IPN thành công (gửi trùng / sai thứ tự).
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);

        Assert.False(don.DatTrangThaiThanhToan(PaymentStatus.Failed));
        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Unpaid)]
    [InlineData(PaymentStatus.Pending)]
    public void Da_thu_tien__KHONG_ha_ve_trang_thai_chua_tra(PaymentStatus moi)
    {
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);

        Assert.False(don.DatTrangThaiThanhToan(moi));
        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
    }

    [Fact]
    public void Paid__sang_PartiallyRefunded_va_Refunded__DUOC__tien_ve_phia_hoan()
    {
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);

        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.PartiallyRefunded));
        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.Refunded));
        Assert.Equal(PaymentStatus.Refunded, don.PaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.PartiallyRefunded)]
    [InlineData(PaymentStatus.Unpaid)]
    public void Refunded__HAP_THU__khong_di_dau_duoc(PaymentStatus moi)
    {
        // 🔒 "Đã hoàn đủ" là trạng thái cuối: hồi sinh về `Paid` sẽ làm sổ sách nói *"khách đã trả"* trong khi
        // tiền **đã trả lại** ⇒ có thể **thu thêm** hoặc **giao hàng** cho đơn đã hoàn.
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);
        don.DatTrangThaiThanhToan(PaymentStatus.Refunded);

        Assert.False(don.DatTrangThaiThanhToan(moi));
        Assert.Equal(PaymentStatus.Refunded, don.PaymentStatus);
    }

    [Fact]
    public void Dat_lai_DUNG_trang_thai_hien_tai__tra_FALSE__khong_phai_ghi()
    {
        // 🔑 Giá trị trả về là thứ chỗ gọi dùng để **quyết định có `UpdateAsync` hay không**; trả `true` cho một
        // chuyển dịch không xảy ra sẽ sinh `UPDATE` rỗng + publish sự kiện trùng.
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);

        Assert.False(don.DatTrangThaiThanhToan(PaymentStatus.Paid));
    }
}
