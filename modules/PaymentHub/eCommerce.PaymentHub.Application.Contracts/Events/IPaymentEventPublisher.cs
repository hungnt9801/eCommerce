using eCommerce.PaymentHub.Payments;

namespace eCommerce.PaymentHub.Events;

/// <summary>
/// 🚨 **PHÁT SỰ KIỆN THANH TOÁN — HỢP ĐỒNG** (`S5`) — để **mọi** đường ghi phát sự kiện **giống nhau**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI CÓ INTERFACE NÀY (không dùng thẳng lớp `PaymentEventPublisher`):**
/// lớp hiện thực nằm ở `eCommerce.PaymentHub.Application`, mà `src/eCommerce.Application` **KHÔNG được**
/// tham chiếu nó (luật phân tầng #8: *"`eCommerce.Application` **KHÔNG** tham chiếu `PaymentHub.Application`"*).
/// ⚠️ Hệ quả **thật** trước `S5`: `PaymentReconciliationWorker` (job đối soát, sống ở `src/eCommerce.Application`)
/// đọc/ghi `PaymentTransaction` **trực tiếp** qua repository — đúng quy ước — nhưng vì **không** với tới được
/// lớp phát sự kiện, nó **KHÔNG PHÁT** `PaymentSucceededEto`.
/// 📌 Nghĩa là: **lưới an toàn cứu được TIỀN mà không cứu được ĐƠN** — job phát hiện *"khách đã trả"*,
/// ghi `Succeeded`, rồi **đơn vẫn `Unpaid`** ⇒ giao hàng sai. 🔑 Đây đúng câu §7.1 của `PAY-001` cảnh báo
/// (*"`querydr` là lưới an toàn duy nhất"*), chỉ khác: **đường job** của chính lưới đó bị hở.
///
/// ✅ **Cách sửa (cùng khuôn `IXuLyKetQuaCong`):** **hợp đồng** ở `Application.Contracts` (tầng thấp, ai cũng
/// tham chiếu được), **hiện thực** ở `Application`. 🔒 Không phải app service ⇒ **không** có `[Authorize]` ⇒
/// job gọi được mà **không** bị `AbpAuthorizationException` (lỗi **đã xảy ra thật** ở `SAL-005`).
///
/// 🔒 **Điều kiện gọi — quan trọng hơn cả chữ ký hàm:** chỉ phát khi **trạng thái THẬT SỰ đã đổi**
/// (`MarkSucceeded`/`MarkFailed` trả `true`) hoặc khi `RegisterRefund` **đã chạy**. 📌 Phát cho một chuyển dịch
/// **không xảy ra** làm đơn nhận *"đã trả tiền"* hai lần (thông báo trùng, audit trùng) và che mất con số
/// *"đã ghi thật"* — đúng lỗi `F-PAY-9`.
/// </summary>
public interface IPaymentEventPublisher
{
    /// <summary>Phát `PaymentSucceededEto` — **chỉ** khi giao dịch vừa **thực sự** chuyển sang đã thu tiền.</summary>
    Task PhatDaThuTienAsync(PaymentTransaction giaoDich);

    /// <summary>Phát `PaymentFailedEto` — **chỉ** khi `MarkFailed` trả `true`.</summary>
    Task PhatThatBaiAsync(PaymentTransaction giaoDich, string? maPhanHoiCuaCong = null);

    /// <summary>Phát `PaymentRefundedEto` — **chỉ** khi `RegisterRefund` đã chạy (tiền đã ra, sổ đã ghi).</summary>
    Task PhatDaHoanTienAsync(PaymentTransaction giaoDich, PaymentRefund dongHoan);
}
