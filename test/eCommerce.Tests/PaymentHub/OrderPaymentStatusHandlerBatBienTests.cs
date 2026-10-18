using System.Reflection;
using eCommerce.Application.Sales;
using eCommerce.Kernel.Events;
using eCommerce.Orders;
using eCommerce.PaymentHub;
using eCommerce.PaymentHub.Events;
using Volo.Abp.Application.Services;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **BẤT BIẾN CỦA CẦU NỐI `Payment` → `Order`** (`S4`) — kiểm bằng **reflection**, vì test ở dự án này là
/// **test thuần** (không dựng DB/host), nhưng *"có người nhận sự kiện hay không"* **kiểm được mà không cần chạy**.
///
/// **VÌ SAO ĐÂY KHÔNG PHẢI "TEST HÌNH THỨC":** dự án đã trả giá đúng loại lỗi này **một lần** — lỗi #22:
/// `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn** mà **không ai nhận**, rồi vẫn tiến con
/// trỏ đồng bộ ⇒ **đơn mất vĩnh viễn, không có lỗi nào** (`XC-002` §0, `test/eCommerce.Tests/ChannelHubSync/`).
/// 🔑 Cùng hình dạng ở đây: phát `PaymentSucceededEto` mà **không** có handler ⇒ **tiền vào, đơn vẫn `Unpaid`**.
/// ⇒ Test khoá lại **ba** điều: (a) **có** người nhận, (b) sự kiện mang **đủ** trường để người nhận làm việc,
/// (c) **không** có đường ghi trục tài chính nào khác (setter kín + không có endpoint HTTP).
/// </summary>
public class OrderPaymentStatusHandlerBatBienTests
{
    private static readonly Assembly AssemblyApp = typeof(OrderPaymentStatusHandler).Assembly;
    private static readonly Assembly AssemblyPayment = typeof(PaymentSucceededEto).Assembly;

    // ── (1) CÓ NGƯỜI NHẬN ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PHAI_co_handler_nhan_PaymentSucceededEto()
    {
        var kieuNhan = typeof(IDistributedEventHandler<PaymentSucceededEto>);

        Assert.True(kieuNhan.IsAssignableFrom(typeof(OrderPaymentStatusHandler)),
            "🚨 KHÔNG có handler nhận `PaymentSucceededEto` ⇒ tiền vào mà đơn vẫn `Unpaid` — đúng lỗi #22 " +
            "nhưng ở dòng tiền (`PAY-001` §3.5).");
    }

    [Fact]
    public void PHAI_co_handler_nhan_PaymentFailedEto()
    {
        var kieuNhan = typeof(IDistributedEventHandler<PaymentFailedEto>);

        Assert.True(kieuNhan.IsAssignableFrom(typeof(OrderPaymentStatusHandler)),
            "🚨 KHÔNG có handler nhận `PaymentFailedEto` ⇒ đơn của giao dịch thất bại mãi ở `Unpaid` " +
            "(không phân biệt được *'chưa ai trả'* với *'đã thử và thất bại'*).");
    }

    /// <summary>
    /// Hai sự kiện này **MANG DỮ LIỆU** (không gắn `INotificationEvent`) ⇒ `EventCoverageChecker` báo **Error**
    /// khi không có người nhận. 📌 Nếu ai đó gắn marker vào, cảnh báo tự **hạ cấp thành Warning** ⇒ lỗi #22
    /// quay lại **mà không ai thấy**.
    /// </summary>
    [Fact]
    public void Su_kien_thanh_toan_KHONG_phai_su_kien_thong_bao()
    {
        Assert.False(typeof(INotificationEvent).IsAssignableFrom(typeof(PaymentSucceededEto)));
        Assert.False(typeof(INotificationEvent).IsAssignableFrom(typeof(PaymentFailedEto)));

        // 🔑 Chiều NGƯỢC: chứng minh phép kiểm trên **có tác dụng** — một ETO thông báo thật phải trả `true`.
        Assert.True(typeof(INotificationEvent)
            .IsAssignableFrom(typeof(eCommerce.Catalog.Events.ProductPublishedEto)));
    }

    // ── (2) SỰ KIỆN MANG ĐỦ TRƯỜNG ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(PaymentSucceededEto.TenantId))]
    [InlineData(nameof(PaymentSucceededEto.AggregateId))]
    [InlineData(nameof(PaymentSucceededEto.PaymentTransactionId))]
    [InlineData(nameof(PaymentSucceededEto.OrderId))]
    [InlineData(nameof(PaymentSucceededEto.MerchantReference))]
    [InlineData(nameof(PaymentSucceededEto.Amount))]
    [InlineData(nameof(PaymentSucceededEto.Currency))]
    public void PaymentSucceededEto_CO_du_truong_handler_can(string tenTruong)
    {
        var thuocTinh = typeof(PaymentSucceededEto).GetProperty(tenTruong, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(thuocTinh);
        // `TenantId`/`OrderId` là nullable ⇒ kiểu `Guid?`; `Amount` phải là `decimal` (cấm `double` cho tiền).
        if (tenTruong == nameof(PaymentSucceededEto.Amount))
        {
            Assert.Equal(typeof(decimal), thuocTinh!.PropertyType);
        }
    }

    [Theory]
    [InlineData(nameof(PaymentFailedEto.TenantId))]
    [InlineData(nameof(PaymentFailedEto.AggregateId))]
    [InlineData(nameof(PaymentFailedEto.PaymentTransactionId))]
    [InlineData(nameof(PaymentFailedEto.OrderId))]
    [InlineData(nameof(PaymentFailedEto.Amount))]
    [InlineData(nameof(PaymentFailedEto.Currency))]
    public void PaymentFailedEto_CO_du_truong_handler_can(string tenTruong)
    {
        Assert.NotNull(typeof(PaymentFailedEto).GetProperty(tenTruong, BindingFlags.Public | BindingFlags.Instance));
    }

    // ── (3) KHÔNG CÓ ĐƯỜNG GHI TRỤC TÀI CHÍNH NÀO KHÁC ────────────────────────────────────────

    [Fact]
    public void Order_PaymentStatus_KHONG_co_setter_cong_khai()
    {
        // Trước `S4` trường này có setter công khai và **không ai gán** ⇒ nợ #1 của `PAY-001`. Nay đường ghi duy
        // nhất là `DatTrangThaiThanhToan` (có luật hấp thụ). 📌 Nếu ai đó mở lại setter, luật hấp thụ **bị đi
        // vòng** mà **không có lỗi biên dịch** — đúng loại lỗi mà test này chặn.
        var thuocTinh = typeof(Order).GetProperty(nameof(Order.PaymentStatus))!;

        Assert.True(
            thuocTinh.SetMethod is null || !thuocTinh.SetMethod.IsPublic,
            "🚨 `Order.PaymentStatus` KHÔNG được có setter công khai: trục tài chính chỉ ghi qua " +
            "`DatTrangThaiThanhToan` (`SAL-003` BR-013 — chỉ `PaymentHub`/IPN được ghi).");

        // Chiều ngược: hàm ghi **phải** tồn tại và công khai.
        Assert.NotNull(typeof(Order).GetMethod(nameof(Order.DatTrangThaiThanhToan), BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void XuLyKetQuaCong_KHONG_phai_app_service__nen_KHONG_co_endpoint_HTTP()
    {
        // 🚨 Đây là bất biến **bảo mật**: một endpoint HTTP *"đánh dấu đã trả tiền"* chính là cửa mở mà
        // `PAY-001` D3/§7.7.4 #1 cấm (ai cũng tự gọi được ⇒ giao hàng miễn phí). ABP chỉ sinh controller cho
        // `IApplicationService` ⇒ kiểm đúng điều kiện đó.
        Assert.True(typeof(IXuLyKetQuaCong).IsAssignableFrom(typeof(XuLyKetQuaCongService)));

        Assert.False(typeof(IApplicationService).IsAssignableFrom(typeof(XuLyKetQuaCongService)),
            "🚨 `XuLyKetQuaCongService` KHÔNG được là `IApplicationService` (ABP sẽ sinh controller ⇒ có endpoint " +
            "đánh dấu 'đã trả tiền' không cần chữ ký).");

        Assert.False(typeof(IApplicationService).IsAssignableFrom(typeof(IXuLyKetQuaCong)));
    }

    // ── (4) PHÂN TẦNG — quy tắc #8 của `handover` §4.2 ────────────────────────────────────────

    [Fact]
    public void Tang_app_KHONG_tham_chieu_PaymentHub_Application__chi_tham_chieu_hop_dong()
    {
        // 🔒 `eCommerce.Application` → `PaymentHub.Application` là **NGƯỢC TẦNG** (đã ghi ở `handover` §4.2 và
        // `PAY-001` Q1). 📌 Đi qua `Application.Contracts` (hợp đồng sự kiện) là đường duy nhất **không** tạo
        // tham chiếu vòng — nếu ai đó đổi sang gọi thẳng app service của module, sự kiện sẽ **biến mất** và
        // tính idempotent của sự kiện cũng mất.
        var tenThamChieu = AssemblyApp.GetReferencedAssemblies().Select(x => x.Name).ToList();

        Assert.DoesNotContain("eCommerce.PaymentHub.Application", tenThamChieu);
        Assert.Contains("eCommerce.PaymentHub.Application.Contracts", tenThamChieu);
    }

    [Fact]
    public void Module_thanh_toan_KHONG_tham_chieu_eCommerce_Domain()
    {
        // 🔑 Nếu module thanh toán thấy được `Order` thì *"cầu nối"* sẽ bị viết thẳng vào module ⇒ module sở hữu
        // **hai** trục trạng thái (tiền + đơn) và `Order` có **hai** nơi ghi. Test này giữ đúng hướng phụ thuộc.
        var tenThamChieu = AssemblyPayment.GetReferencedAssemblies().Select(x => x.Name).ToList();

        Assert.DoesNotContain("eCommerce.Domain", tenThamChieu);
    }
}
