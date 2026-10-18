using eCommerce.PaymentHub.Events;
using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
namespace eCommerce.PaymentHub;

/// <summary>
/// 🚨 **GHI NHẬN KẾT QUẢ CỦA CỔNG** (`S4`) — nơi duy nhất biến *"cổng nói đã thu tiền"* thành *"sổ sách ghi đã
/// thu tiền"* **và** phát sự kiện cho đơn.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **NĂM BẤT BIẾN ĐƯỢC CƯỠNG CHẾ Ở ĐÂY:**
/// <list type="number">
///   <item>🚨 **FAIL-CLOSED THEO CHỮ KÝ** — `SignatureValid = false` ⇒ **NÉM** `0008`, **không** đổi gì. Đây là
///     hàng rào chống **IPN spoofing**: ai tự gửi `vnp_ResponseCode=00` cũng thành *"đơn đã trả tiền"*
///     ⇒ **giao hàng miễn phí** (`PAY-001` §7.7.4 #1). 🔒 Không có cờ cấu hình nào tắt được (D3).</item>
///   <item>**Mã tham chiếu rỗng là LỖI DỮ LIỆU** (đóng `F-PAY-3`): `VerifyWebhookAsync` trả `""` khi IPN thiếu
///     `vnp_TxnRef` (`VnPayPaymentProvider.cs:170`). Tra DB bằng `""` có thể **khớp nhầm** giao dịch khác
///     (hoặc không khớp) — cả hai đều tệ.</item>
///   <item>**ĐỐI CHIẾU SỐ TIỀN** (đóng `F-PAY-2`): `LuatCauNoiThanhToan.DoiChieu` — số tiền + tiền tệ của cổng
///     **phải** khớp số đã lập. ⚠️ Lệch ⇒ **NÉM** `0009` **TRƯỚC** mọi thao tác ghi: ghi `Succeeded` cho một
///     giao dịch có số tiền khác là **sổ sách nói dối**, và `SAL-003` **#12** cấm.</item>
///   <item>**IDEMPOTENT** — IPN là **at-least-once**: lần thứ 2..n **không** đổi trạng thái, chỉ tăng
///     `WebhookReceivedCount` (`MarkSucceeded` trả `false`) ⇒ **không** phát sự kiện lần hai. 🔑 Con số
///     `WebhookReceivedCount` là **bằng chứng** để phân biệt *"cổng gửi trùng"* với *"IPN mất"*.</item>
///   <item>**CHỈ PHÁT SỰ KIỆN KHI THẬT SỰ ĐỔI TRẠNG THÁI** — phát cho một chuyển dịch **không xảy ra** sẽ đẩy
///     đơn sang `Paid` hai lần (thông báo trùng, audit trùng), 🔑 và che mất con số *"đã ghi thật"*.</item>
/// </list>
///
/// 🔒 **CỐ Ý KHÔNG PHẢI `ApplicationService`** (và **không** phải `IApplicationService`) ⇒ ABP **không** sinh
/// controller cho nó ⇒ **không** có endpoint HTTP nào đánh dấu *"đã trả tiền"* được gọi bằng tay. 📌 Endpoint
/// IPN thật (`S5`) gọi vào lớp này **sau khi** đã xác thực chữ ký — xem `IPaymentProvider.VerifyWebhookAsync`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **`S5` — `[ExposeServices(typeof(IXuLyKetQuaCong))]` LÀ BẮT BUỘC, VÀ ĐÂY LÀ MỘT LỖI THẬT ĐÃ ĐO ĐƯỢC.**
///
/// **Chuyện gì xảy ra:** `S4` khai hợp đồng `IXuLyKetQuaCong` và viết lớp này, nhưng **không ai resolve
/// `IXuLyKetQuaCong`** trong suốt `S4` ⇒ lỗi **không lộ ra**. Khi `S5` dựng endpoint IPN và **inject** hợp đồng
/// đó, request IPN đầu tiên trả **`HTTP 500`** với thông điệp:
/// <c>Cannot resolve parameter 'eCommerce.PaymentHub.IXuLyKetQuaCong xuLy' of constructor '…VnPayIpnController'</c>
///
/// **Nguyên nhân (quy ước ĐẶT TÊN của ABP, không phải thiếu đăng ký):** `ITransientDependency` khiến ABP đăng ký
/// lớp theo **tên của chính nó** — `XuLyKetQuaCongService` ⇒ `IXuLyKetQuaCongService` — **không** phải
/// `IXuLyKetQuaCong`. Tên hợp đồng **không** theo quy ước `I<TênLớp>` nên **không** được ánh xạ tự động.
///
/// 📌 **Đây đúng họ bẫy #4 của dự án** (*"thiếu đăng ký ⇒ DI nổ lúc chạy ⇒ `HTTP 500`, triệu chứng không trỏ
/// tới chỗ sai"*), chỉ khác lớp: lần này là **tên interface**, không phải `DbSet`. 🔑 Và nó **cũng không** làm
/// đỏ một cổng nào trước `S5` vì **chưa có ai gọi**. ✅ Nay có test khoá lại
/// (`HoanTienS5Tests.IXuLyKetQuaCong_PHAI_duoc_ExposeServices`) để lần sau đổi tên/đổi lớp là **đỏ ngay**.
/// </summary>
[ExposeServices(typeof(IXuLyKetQuaCong))]
[UnitOfWork]
public class XuLyKetQuaCongService : IXuLyKetQuaCong, ITransientDependency
{
    private readonly IRepository<PaymentTransaction, Guid> _giaoDich;
    private readonly PaymentEventPublisher _suKien;
    private readonly ILogger<XuLyKetQuaCongService> _logger;

    public XuLyKetQuaCongService(
        IRepository<PaymentTransaction, Guid> giaoDich,
        PaymentEventPublisher suKien,
        ILogger<XuLyKetQuaCongService> logger)
    {
        _giaoDich = giaoDich;
        _suKien = suKien;
        _logger = logger;
    }

    public async Task<KetQuaGhiNhanDto> GhiNhanAsync(
        WebhookVerificationResult ketQua,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ketQua);

        // ── ① CHỮ KÝ — hàng rào ĐẦU TIÊN và không thể tắt ───────────────────────────────────────────────
        if (!ketQua.SignatureValid)
        {
            // 🔒 Không đổi trạng thái, không ghi gì: payload chưa được xác thực thì **không có ý nghĩa** gì.
            // 📌 Dùng mã **có sẵn** `eCommerce:Payment:0001` (`PaymentSignatureInvalid`) — 🔑 mã này đã được khai
            // từ Phase 0 nhưng **chưa từng dùng ở đâu** (grep = 1, chính chỗ khai báo). Khai thêm mã riêng
            // trong `PaymentErrorCodes` là **hai nguồn sự thật** cho cùng một nghĩa (`XC-003` F2).
            throw new BusinessException(eCommerceConsts.ErrorCodes.PaymentSignatureInvalid)
                .WithData("merchantReference", ketQua.MerchantReference)
                .WithData("lyDo", "IPN/webhook CHƯA qua xác thực chữ ký ⇒ từ chối (chống IPN giả).");
        }

        // ── ② MÃ THAM CHIẾU — rỗng là lỗi dữ liệu (F-PAY-3) ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(ketQua.MerchantReference))
        {
            throw new BusinessException(PaymentErrorCodes.MaThamChieuKhongHopLe)
                .WithData("lyDo", "IPN thiếu `vnp_TxnRef` ⇒ không biết giao dịch nào (KHÔNG tra DB bằng chuỗi rỗng).");
        }

        var ma = ketQua.MerchantReference.Trim();

        var gd = await _giaoDich.FirstOrDefaultAsync(x => x.MerchantReference == ma, cancellationToken: cancellationToken)
            ?? throw new BusinessException(PaymentErrorCodes.KhongTimThayGiaoDich)
                .WithData("merchantReference", ma);

        // ── ③ ĐỐI CHIẾU SỐ TIỀN (F-PAY-2) — TRƯỚC khi ghi bất cứ thứ gì ────────────────────────────────
        var doiChieu = LuatCauNoiThanhToan.DoiChieu(ketQua.Amount, ketQua.Currency, gd.Amount, gd.Currency);

        if (doiChieu != KetQuaDoiChieu.Khop)
        {
            throw new BusinessException(PaymentErrorCodes.LechSoTienVoiDon)
                .WithData("merchantReference", ma)
                .WithData("soTienCong", ketQua.Amount)
                .WithData("tienTeCong", ketQua.Currency)
                .WithData("soTienGiaoDich", gd.Amount)
                .WithData("tienTeGiaoDich", gd.Currency)
                .WithData("ketQuaDoiChieu", doiChieu.ToString())
                .WithData("lyDo",
                    "Số tiền của cổng KHÁC số tiền đã lập giao dịch ⇒ KHÔNG ghi nhận (đối chiếu sao kê trước).");
        }

        var daGhi = false;

        if (ketQua.IsSuccess)
        {
            // ── ④ ĐÃ THU TIỀN ─────────────────────────────────────────────────────────────────────────
            daGhi = gd.MarkSucceeded(
                ketQua.ProviderTransactionId,
                ketQua.IdempotencyKey ?? ketQua.ProviderTransactionId,
                ketQua.RawPayload,
                ketQua.PaidAtUtc);
        }
        else
        {
            // ── ⑤ THẤT BẠI / KHÁCH HUỶ — `MarkFailed` KHÔNG ghi đè giao dịch đã có tiền (§3.2) ────────────
            daGhi = gd.MarkFailed(
                ketQua.FailureReason ?? "Cổng báo giao dịch không thành công (không có lý do chi tiết).",
                ketQua.RawPayload);
        }

        await _giaoDich.UpdateAsync(gd, autoSave: true, cancellationToken: cancellationToken);

        // ── ⑥ PHÁT SỰ KIỆN — CHỈ khi thật sự đổi trạng thái ────────────────────────────────────────────
        var daPhat = false;

        if (daGhi)
        {
            if (ketQua.IsSuccess)
            {
                await _suKien.PhatDaThuTienAsync(gd);
            }
            else
            {
                await _suKien.PhatThatBaiAsync(gd, ketQua.FailureReason);
            }

            daPhat = true;

            _logger.LogWarning(
                "GHI NHẬN KẾT QUẢ CỔNG: giao dịch {Ma} ⇒ {TrangThai} · số tiền {SoTien} · đã phát sự kiện. [PAY-CONG-DA-GHI]",
                gd.MerchantReference, gd.Status, gd.Amount);
        }
        else
        {
            // 🔑 Đây **KHÔNG** phải lỗi: IPN gửi trùng là bình thường. Nhưng phải **đếm** được.
            _logger.LogInformation(
                "IPN GỬI TRÙNG cho giao dịch {Ma}: giữ nguyên {TrangThai}, bộ đếm = {SoLan}. [PAY-IPN-TRUNG]",
                gd.MerchantReference, gd.Status, gd.WebhookReceivedCount);
        }

        return new KetQuaGhiNhanDto
        {
            PaymentTransactionId = gd.Id,
            MerchantReference = gd.MerchantReference,
            TrangThaiSau = gd.Status.ToString(),
            DaGhiSo = daGhi,
            DaPhatSuKien = daPhat,
            SoLanNhanWebhook = gd.WebhookReceivedCount,
            DaThuTien = gd.Status is PaymentTransactionStatus.Succeeded
                or PaymentTransactionStatus.PartiallyRefunded
                or PaymentTransactionStatus.Refunded
        };
    }
}
