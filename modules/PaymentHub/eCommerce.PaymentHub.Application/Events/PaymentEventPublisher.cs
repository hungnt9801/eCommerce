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
///
/// 🆕 **`S5` — LỚP NÀY NAY HIỆN THỰC <see cref="IPaymentEventPublisher"/>** (hợp đồng ở
/// `Application.Contracts`) để đường **thứ ba** — job `PaymentReconciliationWorker` ở
/// `src/eCommerce.Application` — cũng phát được **cùng** sự kiện. 🔒 Trước `S5`, `eCommerce.Application`
/// **không tham chiếu được** lớp này (luật phân tầng #8) ⇒ job ghi `Succeeded` mà **không** phát sự kiện ⇒
/// *"lưới an toàn cứu được TIỀN, không cứu được ĐƠN"*. 📌 Đây là phát hiện **mới của `S5`**, ghi ở `PAY-001`
/// §16.6 #1 — và nó **không** làm đỏ cổng nào trước đó, đúng họ lỗi *"hỏng âm thầm"*.
/// </summary>
public class PaymentEventPublisher : IPaymentEventPublisher, ITransientDependency
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

    /// <summary>
    /// Phát `PaymentRefundedEto` (`S5`) — **chỉ** gọi khi `RegisterRefund` **đã** chạy (tiền **thật sự** ra và
    /// sổ **đã** ghi).
    ///
    /// 🔒 **CẤM gọi hàm này cho một dòng hoàn tiền ở `ChoXacNhan`/`KhongXacDinh`:** sự kiện này làm **đơn**
    /// chuyển sang `PartiallyRefunded`/`Refunded` ⇒ phát sớm nghĩa là *"đơn đã hoàn tiền"* trong khi khách
    /// **chưa** nhận được đồng nào — 📌 đúng lỗi *"báo thành công khi chưa làm xong"* mà `RefundResult` sinh ra
    /// để chống. 🔑 **Điều kiện gọi:** `LuatGhiNhanHoanTien.PhaiGhiSo(...)` là `true` **và** `RegisterRefund`
    /// đã chạy **không ném** (`RegisterRefund` trả `void`; nó ném khi hoàn vượt ⇒ khi đó **không** phát).
    /// </summary>
    public Task PhatDaHoanTienAsync(PaymentTransaction giaoDich, PaymentRefund dongHoan)
        => _eventBus.PublishAsync(new PaymentRefundedEto
        {
            TenantId = giaoDich.TenantId,
            AggregateId = giaoDich.Id,
            PaymentTransactionId = giaoDich.Id,
            PaymentRefundId = dongHoan.Id,
            OrderId = giaoDich.OrderId,
            ProviderCode = giaoDich.ProviderCode,
            MerchantReference = giaoDich.MerchantReference,
            ProviderTransactionId = giaoDich.ProviderTransactionId,
            Amount = giaoDich.Amount,
            RefundedAmount = giaoDich.RefundedAmount,
            SoTienLanNay = dongHoan.Amount,
            Currency = giaoDich.Currency,
            RefundReason = dongHoan.Reason,
            NguoiThucHien = dongHoan.PerformedBy,
            RefundRequestId = dongHoan.RefundRequestId
        });
}
