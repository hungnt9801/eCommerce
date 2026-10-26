using System.Diagnostics;
using eCommerce.Kernel.Observability;
using eCommerce.Orders;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Application.Sales;

/// <summary>
/// **PHÁT METRIC `sales_order_transition_total`** (`SAL-003` NFR-012) — nghe `OrderStatusChangedEto`.
///
/// 🔑 **VÌ SAO NGHE EVENT, KHÔNG PHÁT TRONG APP SERVICE:** trạng thái đơn đổi ở **nhiều đường** (API nhân
/// viên · IPN thanh toán · webhook sàn · job). Nếu mỗi app service tự đếm thì **đường nào quên** là metric
/// **thiếu** — và thiếu thì biểu đồ vẫn vẽ, chỉ là **sai**. 📌 Sự kiện `OrderStatusChangedEto` được phát
/// **trong chính** `Order.Chuyen` (một cửa duy nhất) ⇒ nghe nó là **phủ mọi đường**, kể cả đường thêm sau này.
///
/// ⚠️ **NHÃN HỮU HẠN:** `axis`/`from`/`to` — 4 trục × ~10 giá trị. 🚨 **CẤM** `orderId` (`XC-005` BR-009).
/// 📌 Event đi qua **outbox** ⇒ metric trễ ≤2 s so với giao dịch (đúng thiết kế, `XC-002` §17.3).
/// </summary>
public class OrderStatusMetricHandler : IDistributedEventHandler<OrderStatusChangedEto>, ITransientDependency
{
    private readonly eCommerceMetrics? _metrics;

    public OrderStatusMetricHandler(eCommerceMetrics? metrics = null)
    {
        _metrics = metrics;
    }

    public Task HandleEventAsync(OrderStatusChangedEto suKien)
    {
        // 📌 `null` khi test/không đăng ký metric → bỏ qua, **không** ném: thiếu metric không được làm chết
        // consumer (cùng khuôn `VnPayPaymentProvider`/`PaymentReconciliationWorker`).
        _metrics?.SalesOrderTransition.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Axis, suKien.Axis),
            new KeyValuePair<string, object?>(eCommerceMetricTags.FromStatus, suKien.FromStatus),
            new KeyValuePair<string, object?>(eCommerceMetricTags.ToStatus, suKien.ToStatus));

        // 📌 `Activity` hiện tại để log/ETO nối được với metric trong hệ thống thật (không tạo activity mới).
        _ = Activity.Current;

        return Task.CompletedTask;
    }
}
