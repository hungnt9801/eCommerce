using eCommerce.PaymentHub.Events;
using eCommerce.PaymentHub.Payments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.PaymentHub.Events;

/// <summary>
/// 🚨 **PHÁT SỰ KIỆN THANH TOÁN — MỘT CHỖ DUY NHẤT** (`S4`).
///
/// **VÌ SAO PHẢI GOM VỀ MỘT CHỖ:** sự kiện `PaymentSucceededEto` là thứ **duy nhất** làm `Order.PaymentStatus`
/// chuyển sang `Paid`. Nếu mỗi đường ghi (IPN `S5`, `querydr` tay, job đối soát, lần tạo giao dịch) tự dựng và
/// phát sự kiện theo cách riêng thì:
/// <list type="bullet">
///   <item>một đường quên phát ⇒ **đơn đã trả vẫn `Unpaid`** — và đó **chính là lỗi** mà `PAY-001` §3.5 đã ghi
///     (*"hôm nay khách trả tiền xong thì đơn vẫn `Unpaid`"*), chỉ là đổi chỗ xuất hiện;</item>
///   <item>các đường phát **thiếu trường** (ví dụ quên `TenantId`) ⇒ consumer chạy sai tenant / sai dữ liệu.</item>
/// </list>
/// 📌 Ở lượt `S4`, **hai** đường gọi vào đây: <see cref="XuLyKetQuaCongService"/> (IPN đã xác thực, `S5` nối
/// endpoint) và `PaymentAppService.PostTruyVanTrangThaiAsync` (`querydr`) — 🔑 đường thứ hai **bắt buộc** phải
/// phát: `querydr` là **lưới an toàn khi IPN mất**; nếu nó ghi `Succeeded` mà **không** phát sự kiện thì đơn của
/// đúng những giao dịch bị mất IPN sẽ **vĩnh viễn** `Unpaid`.
/// </summary>
public class PaymentEventPublisher : ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;

    public PaymentEventPublisher(IDistributedEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>Phát `PaymentSucceededEto` — **chỉ** gọi khi entity vừa **thực sự** chuyển sang đã thu tiền.</summary>
    public Task PhatDaThuTienAsync(PaymentTransaction giaoDich)
        => _eventBus.PublishAsync(new PaymentSucceededEto
        {
            TenantId = giaoDich.TenantId,
            AggregateId = giaoDich.Id,
            PaymentTransactionId = giaoDich.Id,
            OrderId = giaoDich.OrderId,
            ProviderCode = giaoDich.ProviderCode,
            MerchantReference = giaoDich.MerchantReference,
            ProviderTransactionId = giaoDich.ProviderTransactionId,
            Amount = giaoDich.Amount,
            Currency = giaoDich.Currency,
            PaidAtUtc = giaoDich.PaidAtUtc
        });

    /// <summary>Phát `PaymentFailedEto` — **chỉ** gọi khi `MarkFailed` trả `true` (tức **thật sự** đổi trạng thái).</summary>
    public Task PhatThatBaiAsync(PaymentTransaction giaoDich, string? maPhanHoiCuaCong = null)
        => _eventBus.PublishAsync(new PaymentFailedEto
        {
            TenantId = giaoDich.TenantId,
            AggregateId = giaoDich.Id,
            PaymentTransactionId = giaoDich.Id,
            OrderId = giaoDich.OrderId,
            ProviderCode = giaoDich.ProviderCode,
            MerchantReference = giaoDich.MerchantReference,
            ProviderTransactionId = giaoDich.ProviderTransactionId,
            Amount = giaoDich.Amount,
            Currency = giaoDich.Currency,
            FailureReason = giaoDich.FailureReason,
            ProviderResponseCode = maPhanHoiCuaCong
        });
}
