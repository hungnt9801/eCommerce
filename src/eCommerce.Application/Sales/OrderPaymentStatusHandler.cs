using eCommerce.Orders;
using eCommerce.PaymentHub.Events;
using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🚨 **CẦU NỐI `PaymentTransaction` → `Order.PaymentStatus`** (`S4`) — **CHỖ DUY NHẤT** trục tài chính của đơn
/// được ghi.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **TRƯỚC `S4` (đây là lỗi thật, không phải giả định):** `Order.PaymentStatus` tồn tại, có setter công khai,
/// mặc định `Unpaid`, và **không một dòng nào trong repo gán nó** — grep chỉ thấy *đọc để hiển thị*. Hệ quả:
/// khách **đã** trả tiền nhưng đơn vẫn `Unpaid` ⇒ giao hàng/không giao sai, **và không có lỗi nào nổi lên**
/// (`PAY-001` §3.5, `SAL-003` **BR-013**).
///
/// **VÌ SAO ĐI QUA SỰ KIỆN (trả lời `Q1` của `PAY-001`):** `PaymentHub` **không** tham chiếu `Order` (module tự
/// chứa), còn `eCommerce.Application` **không được** tham chiếu `PaymentHub.Application` (ngược tầng). ⇒ Nơi phát
/// (`PaymentHub.Application`, có entity giao dịch) và nơi ghi (`eCommerce.Application`, có `IRepository<Order>`)
/// **bắt buộc** phải nối qua **hợp đồng sự kiện** (`PaymentHub.Application.Contracts`). 📌 Đây **không** phải
/// "cho kiến trúc đẹp": nó là cách duy nhất để *"chỉ `PaymentHub`/IPN được ghi trục tài chính"* mà **không** tạo
/// tham chiếu vòng.
///
/// 🔒 **BỐN QUY TẮC CỦA HANDLER:**
/// <list type="number">
///   <item>**`OrderId` NULL ⇒ BỎ QUA, không phải lỗi** — giao dịch nạp ví / không gắn đơn là ca hợp lệ
///     (`PAY-001` §5.1). Ném lỗi ở đây sẽ làm hỏng cả lượt ghi thanh toán vì một ca bình thường.</item>
///   <item>**KHÔNG THẤY ĐƠN ⇒ LOG WARNING, KHÔNG NÉM.** Ném thì `S5`/IPN sẽ trả lỗi cho **cổng** (⇒ cổng retry
///     vô ích) trong khi tiền **đã** vào; và điều tệ hơn: rollback mất bản ghi giao dịch = **mất bằng chứng**.
///     Neo ASCII `[PAY-CAU-NOI-KHONG-THAP]` để cơ chế kiểm log tìm được (bài học `I4` của `tools/README-probe.md`).</item>
///   <item>**ĐỐI CHIẾU SỐ TIỀN + TIỀN TỆ** (`LuatCauNoiThanhToan`) — lệch ⇒ **KHÔNG** đánh dấu đơn, log `Error`.
///     🔑 Đây là chỗ đóng `F-PAY-2` ở phía **đơn**: chữ ký HMAC chỉ bảo đảm payload không bị sửa, **không** bảo
///     đảm payload thuộc đúng đơn này với đúng số tiền.</item>
///   <item>**ĐI QUA `Order.DatTrangThaiThanhToan`, KHÔNG gán thẳng** — hàm đó giữ luật hấp thụ (một IPN thất bại
///     đến muộn **không** được hạ đơn đã `Paid`).</item>
/// </list>
///
/// ⚠️ **CHƯA QUA OUTBOX BỀN VỮNG:** hiện sự kiện được phát **in-process, trong cùng UoW/transaction** ⇒ *"không
/// ghi được đơn thì thanh toán cũng rollback"* (nhất quán), nhưng **chưa** chống mất sự kiện nếu tiến trình chết
/// **sau** commit (`XC-002` chưa cài — nợ có mã, xem `PAY-001` §16.4).
/// </summary>
public class OrderPaymentStatusHandler :
    IDistributedEventHandler<PaymentSucceededEto>,
    IDistributedEventHandler<PaymentFailedEto>,
    ITransientDependency
{
    private readonly IRepository<Order, Guid> _donHang;
    private readonly ICurrentTenant _tenantHienTai;
    private readonly ILogger<OrderPaymentStatusHandler> _logger;

    public OrderPaymentStatusHandler(
        IRepository<Order, Guid> donHang,
        ICurrentTenant tenantHienTai,
        ILogger<OrderPaymentStatusHandler> logger)
    {
        _donHang = donHang;
        _tenantHienTai = tenantHienTai;
        _logger = logger;
    }

    /// <summary>Giao dịch **đã thu tiền** (IPN/`querydr` đã xác thực) ⇒ đơn sang `Paid`.</summary>
    public Task HandleEventAsync(PaymentSucceededEto suKien) => GhiNhanAsync(
        suKien.TenantId,
        suKien.OrderId,
        suKien.MerchantReference,
        suKien.PaymentTransactionId,
        suKien.Amount,
        suKien.Currency,
        PaymentStatus.Paid,
        "Succeeded");

    /// <summary>Giao dịch **thất bại / khách huỷ** ⇒ đơn sang `Failed` (⚠️ **không** hạ đơn đã `Paid`).</summary>
    public Task HandleEventAsync(PaymentFailedEto suKien) => GhiNhanAsync(
        suKien.TenantId,
        suKien.OrderId,
        suKien.MerchantReference,
        suKien.PaymentTransactionId,
        suKien.Amount,
        suKien.Currency,
        PaymentStatus.Failed,
        "Failed");

    private async Task GhiNhanAsync(
        Guid? tenantId,
        Guid? orderId,
        string merchantReference,
        Guid paymentTransactionId,
        decimal soTien,
        string? tienTe,
        PaymentStatus trangThaiMoi,
        string nguonTrangThai)
    {
        // ① Giao dịch KHÔNG gắn đơn (nạp ví) ⇒ không có gì để nối. Đây là ca **hợp lệ**, không phải lỗi.
        if (orderId is null)
        {
            _logger.LogDebug(
                "CẦU NỐI THANH TOÁN: giao dịch {Ma} không gắn đơn ⇒ bỏ qua (nạp ví / giao dịch độc lập).",
                merchantReference);

            return;
        }

        // 🔒 Đổi ngữ cảnh tenant theo **chính sự kiện** — hôm nay 1 tenant nên gần như vô hại, nhưng nếu mai có
        // nhiều tenant mà sự kiện được xử lý ngoài UoW phát (outbox/relay) thì thiếu dòng này = **ghi nhầm tenant**.
        using var _ = _tenantHienTai.Change(tenantId);

        var don = await _donHang.FindAsync(orderId.Value);

        // ② Không thấy đơn ⇒ log WARNING, KHÔNG ném (xem quy tắc 2 ở mô tả lớp).
        if (don is null)
        {
            _logger.LogWarning(
                "CẦU NỐI THANH TOÁN KHÔNG THẤP ĐƯỢC ĐƠN: giao dịch {Ma} trỏ tới đơn {OrderId} nhưng KHÔNG tìm thấy ⇒ " +
                "đơn KHÔNG được đánh dấu {TrangThai}. Cần người vận hành kiểm. [PAY-CAU-NOI-KHONG-THAP]",
                merchantReference, orderId, trangThaiMoi);

            return;
        }

        // ③ ĐỐI CHIẾU SỐ TIỀN + TIỀN TỆ (F-PAY-2) — TRƯỚC khi ghi.
        var doiChieu = LuatCauNoiThanhToan.DoiChieu(soTien, tienTe, don.GrandTotal, don.Currency);

        if (doiChieu != KetQuaDoiChieu.Khop)
        {
            // 🚨 KHÔNG ném: bản ghi thanh toán là **bằng chứng tiền đã vào**. Ném ở đây sẽ rollback cả lượt ghi
            // của `S5`/IPN ⇒ mất dấu vết dòng tiền, và cổng sẽ retry vô ích. Người vận hành phải thấy **log**
            // và đối chiếu sao kê — đó là hành động đúng, không phải "thử lại".
            _logger.LogError(
                "CẦU NỐI THANH TOÁN LỆCH TIỀN: giao dịch {Ma} (Id {PaymentTransactionId}) báo {SoTienCong} {TienTeCong}, " +
                "còn đơn {SoDon} là {SoTienDon} {TienTeDon} — {LyDo} ⇒ KHÔNG đánh dấu đơn {TrangThai}. [PAY-CAU-NOI-LECH-TIEN]",
                merchantReference,
                paymentTransactionId,
                soTien,
                tienTe ?? LuatCauNoiThanhToan.TienTeMacDinh,
                don.Number,
                don.GrandTotal,
                don.Currency,
                doiChieu,
                trangThaiMoi);

            return;
        }

        // ④ ĐI QUA LUẬT HẤP THỤ của entity — không gán thẳng.
        if (!don.DatTrangThaiThanhToan(trangThaiMoi))
        {
            // ⚠️ **Không phải lỗi**: (a) đã ở đúng trạng thái đó; (b) bị hấp thụ (đơn đã `Paid`/`Refunded` mà
            // sự kiện nói `Failed`). Ghi `Debug` để không ngập log, nhưng **có** dấu vết khi cần tra.
            _logger.LogDebug(
                "CẦU NỐI THANH TOÁN: đơn {SoDon} KHÔNG đổi trạng thái theo {Nguon} (hiện {HienTai}, yêu cầu {YeuCau} " +
                "⇒ đã đúng hoặc bị luật hấp thụ chặn). [PAY-CAU-NOI-BO-QUA]",
                don.Number, nguonTrangThai, don.PaymentStatus, trangThaiMoi);

            return;
        }

        await _donHang.UpdateAsync(don, autoSave: true);

        _logger.LogInformation(
            "CẦU NỐI THANH TOÁN: đơn {SoDon} ⇒ {TrangThai} (nguồn {Nguon} · giao dịch {Ma}). [PAY-CAU-NOI-DA-GHI]",
            don.Number, don.PaymentStatus, nguonTrangThai, merchantReference);
    }
}
