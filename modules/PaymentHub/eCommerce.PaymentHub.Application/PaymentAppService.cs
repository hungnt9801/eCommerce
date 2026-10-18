using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.PaymentHub;

/// <summary>
/// 🚨 **APP SERVICE THANH TOÁN — VẬN HÀNH** (`SAL-006`) — đường **DUY NHẤT** để hoàn tiền.
///
/// **VÌ SAO LỚP NÀY TỒN TẠI (và vì sao nó quan trọng hơn vẻ ngoài của nó):**
/// trước 16/10/2026, `PaymentTransaction` + `IPaymentProvider` **chỉ** sống trong module: **không** endpoint,
/// **không** app service ⇒ `RefundAsync` (dù đã cài hay chưa) **không có đường nào gọi tới**.
/// Nghĩa là *"hoàn tiền"* — **cổng kiểm soát "KHÔNG MẤT TIỀN"** của GĐ1 — **chưa hề tồn tại** ở mức hệ thống.
///
/// 🔒 **BỐN BẤT BIẾN ĐƯỢC CƯỠNG CHẾ Ở ĐÂY (mỗi cái đều có thể gây MẤT TIỀN THẬT nếu bỏ):**
/// <list type="number">
///   <item><b>CHỈ GHI SỔ KHI TIỀN ĐÃ RA</b> — gọi <c>RegisterRefund</c> **chỉ** khi
///     <c>RefundResult.DaHoanThat == true</c>. Mã `00` từ cổng nghĩa là *"đã nhận yêu cầu"*,
///     **không** phải *"tiền đã ra"* (hoàn tiền VN là **bất đồng bộ**).</item>
///   <item><b>LỖI HẠ TẦNG ⇒ KHÔNG RETRY MÙ</b> — timeout nghĩa là **KHÔNG BIẾT** cổng có nhận hay không;
///     trả <c>LoiHaTang</c> + <c>ChoXacNhan = true</c>. Người gọi **phải** tra `querydr` trước.</item>
///   <item><b>KHOÁ PHÂN TÁN THEO GIAO DỊCH</b> — hai request cùng lúc (hoặc double-click) không được
///     gọi cổng **hai lần**: lần thứ hai có thể hoàn **thêm** tiền.</item>
///   <item><b>LÝ DO BẮT BUỘC</b> — hoàn tiền không có lý do thì 6 tháng sau **không ai** giải thích được
///     vì sao tiền ra (và đó cũng là `vnp_OrderInfo` mà cổng yêu cầu).</item>
/// </list>
///
/// 🔐 **`[Authorize]` ở CẤP CLASS** (quy ước #4): thiếu nó thì các method không gắn gì trở thành
/// **API công khai** — đã xảy ra thật ở Phase 0. Quyền hoàn tiền **tách khỏi** quyền xem giao dịch.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Payments.Default)]
public class PaymentAppService : ApplicationService, IPaymentAppService
{
    private readonly IRepository<Payments.PaymentTransaction, Guid> _giaoDich;
    private readonly Payments.IPaymentProvider _cong;
    private readonly Volo.Abp.DistributedLocking.IAbpDistributedLock _khoa;

    /// <summary>🔗 **CẦU NỐI SANG ĐƠN** (`S4`) — phát `PaymentSucceededEto`/`PaymentFailedEto`.
    /// 📌 Gom một chỗ (`PaymentEventPublisher`) để **mọi** đường ghi đều phát **giống nhau** — thiếu một đường
    /// là đơn đã trả **vẫn** `Unpaid` (`PAY-001` §3.5), đúng lỗi mà `S4` sinh ra để đóng.</summary>
    private readonly Events.PaymentEventPublisher _suKien;

    /// <summary>
    /// 📊 `REC-001` mục 7 — metric đối soát. ⚠️ **TUỲ CHỌN** (`= null`) cùng khuôn `VnPayPaymentProvider`:
    /// 🔒 nếu bắt buộc thì thiếu đăng ký metric sẽ **NỔ** ở một chỗ **không liên quan** — đúng loại
    /// `NullReferenceException` bị nuốt thành *"hoàn tiền thất bại"* chung chung rồi mất hàng giờ điều tra sai hướng.
    /// </summary>
    private readonly eCommerce.Kernel.Observability.eCommerceMetrics? _metrics;

    public PaymentAppService(
        IRepository<Payments.PaymentTransaction, Guid> giaoDich,
        Payments.IPaymentProvider cong,
        Volo.Abp.DistributedLocking.IAbpDistributedLock khoa,
        Events.PaymentEventPublisher suKien,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
    {
        _giaoDich = giaoDich;
        _cong = cong;
        _khoa = khoa;
        _suKien = suKien;
        _metrics = metrics;
    }

    // ── ĐỌC ─────────────────────────────────────────────────────────────────────────────────────

    public async Task<PaymentTransactionDto?> GetTheoMaAsync(string merchantReference)
    {
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            return null;
        }

        var ma = merchantReference.Trim();
        var gd = await _giaoDich.FirstOrDefaultAsync(x => x.MerchantReference == ma);

        return gd is null ? null : ChuyenDto(gd);
    }

    public async Task<List<PaymentTransactionDto>> GetDanhSachAsync(
        string? merchantReference = null,
        string? trangThai = null,
        int toiDa = 50)
    {
        var soLuong = Math.Clamp(toiDa, 1, 200);
        var truyVan = await _giaoDich.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(merchantReference))
        {
            var ma = merchantReference.Trim();
            truyVan = truyVan.Where(x => x.MerchantReference.Contains(ma));
        }

        if (!string.IsNullOrWhiteSpace(trangThai)
            && Enum.TryParse<Payments.PaymentTransactionStatus>(trangThai.Trim(), ignoreCase: true, out var tt))
        {
            truyVan = truyVan.Where(x => x.Status == tt);
        }

        var ds = await AsyncExecuter.ToListAsync(
            truyVan.OrderByDescending(x => x.CreationTime).Take(soLuong));

        return ds.Select(ChuyenDto).ToList();
    }

    // ── GHI: TẠO GIAO DỊCH ──────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TaoGiaoDichKetQuaDto> TaoGiaoDichAsync(TaoGiaoDichInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // ── ⓪ KIỂM ĐẦU VÀO — TRƯỚC khi chạm DB/khoá (`PAY-001` §12: *kiểm đầu vào trước, diễn giải sau*) ──
        if (input.SoTien <= 0m)
        {
            throw new BusinessException(Payments.PaymentErrorCodes.SoTienGiaoDichKhongHopLe)
                .WithData("soTien", input.SoTien)
                .WithData("soDon", input.SoDon)
                .WithData("lyDo", "Số tiền phải LỚN HƠN 0.");
        }

        if (string.IsNullOrWhiteSpace(input.SoDon))
        {
            throw new BusinessException(Payments.PaymentErrorCodes.MaThamChieuKhongHopLe)
                .WithData("lyDo", "Thiếu số đơn ⇒ không sinh được mã tham chiếu ỔN ĐỊNH (BR-PAY-002).");
        }

        if (string.IsNullOrWhiteSpace(input.ReturnUrl) || string.IsNullOrWhiteSpace(input.IpnUrl))
        {
            throw new BusinessException(Payments.PaymentErrorCodes.ThieuThamSoTaoGiaoDich)
                .WithData("coReturnUrl", !string.IsNullOrWhiteSpace(input.ReturnUrl))
                .WithData("coIpnUrl", !string.IsNullOrWhiteSpace(input.IpnUrl))
                .WithData("lyDo", "Cổng BẮT BUỘC `ReturnUrl` và `IpnUrl` (VNPay: IPN phải HTTPS).");
        }

        // 🔒 **KHOÁ PHÂN TÁN THEO ĐƠN** — hai request đồng thời (double-click / retry) **KHÔNG** cùng tạo giao dịch.
        // 📌 Vì sao cần khoá dù DB đã có unique `(TenantId, ProviderCode, MerchantReference)`: nếu chỉ dựa vào DB
        // thì request thứ hai nhận `23505` — một lỗi **hạ tầng** khó hiểu với người dùng, trong khi sự thật là
        // *"có người vừa tạo giao dịch cho đơn này"*. Khoá biến nó thành `eCommerce:Lock:0001` (`409`-nghĩa).
        // ⚠️ Tên khoá theo quy ước `XC-003` §5: module `PaymentHub` ⇒ đoạn `payment`, **có** định danh đối tượng.
        var khoaThe = input.OrderId?.ToString("N") ?? input.SoDon.Trim();

        await using var giuCho = await _khoa.TryAcquireAsync(
            $"payment:create:{khoaThe}", TimeSpan.FromSeconds(10))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "payment:create").WithData("choGiay", 10);

        var thoiDiem = DateTime.UtcNow;

        // ── ① DÙNG LẠI giao dịch ĐANG MỞ của đơn (idempotent theo Ý ĐỊNH thu tiền) ───────────────────────
        // 🚨 ĐÂY LÀ BẤT BIẾN `BR-PAY-002` Ở DẠNG THỰC THI: retry của **cùng một ý định** phải trả **cùng**
        // `MerchantReference`. Nếu tạo giao dịch mới mỗi lần, khách bấm hai lần ⇒ **hai** `vnp_TxnRef` ⇒ hệ thống
        // không nhận ra chúng là một ⇒ nguy cơ **thu hai lần**, và VNPay có thể từ chối mã trùng trong ngày.
        if (input.OrderId is { } orderId)
        {
            var dangMo = await _giaoDich.FirstOrDefaultAsync(x => x.OrderId == orderId
                && (x.Status == Payments.PaymentTransactionStatus.Initiated
                    || x.Status == Payments.PaymentTransactionStatus.Pending));

            if (dangMo is not null)
            {
                // ⚠️ Nếu số tiền **khác** thì KHÔNG dùng lại: đơn đã đổi tiền (thêm/bớt dòng) ⇒ giao dịch cũ là
                // bằng chứng của một ý định **khác** — tạo lần thử mới, và ghi log để người vận hành thấy.
                if (decimal.Compare(dangMo.Amount, input.SoTien) == 0)
                {
                    Logger.LogInformation(
                        "TẠO GIAO DỊCH: đơn {OrderId} đã có giao dịch ĐANG MỞ {Ma} ⇒ DÙNG LẠI (không tạo mới). [PAY-TAO-DUNG-LAI]",
                        orderId, dangMo.MerchantReference);

                    return await DungKetQuaAsync(dangMo, laDaCo: true, input);
                }

                Logger.LogWarning(
                    "TẠO GIAO DỊCH: đơn {OrderId} đang có giao dịch mở {Ma} với số tiền {Cu}, nhưng yêu cầu là {Moi} " +
                    "⇒ tạo LẦN THỬ MỚI. [PAY-TAO-LECH-TIEN]",
                    orderId, dangMo.MerchantReference, dangMo.Amount, input.SoTien);
            }
        }

        // ── ② SINH MÃ ỔN ĐỊNH theo (số đơn, ngày GMT+7, số lần thử) ─────────────────────────────────────
        // 📌 Số lần thử = số giao dịch **đã có cùng gốc** + 1. Không cần cột mới: mã đã mang gốc ổn định
        // (`SinhMaThamChieu.Goc`), và unique index `(TenantId, ProviderCode, MerchantReference)` là hàng rào cuối.
        var goc = Payments.SinhMaThamChieu.Goc(input.SoDon, thoiDiem);
        // ⚠️ Đếm bằng `AsyncExecuter.CountAsync(IQueryable)` — ⚠️ **KHÔNG** dùng `IRepository.GetCountAsync(predicate)`
        // ở đây: `IRepository<TEntity, TKey>` của ABP **không** có overload nhận predicate (lỗi `CS1660` đã gặp
        // ngay lượt này: lambda bị hiểu là `CancellationToken`).
        var truyVanGoc = await _giaoDich.GetQueryableAsync();
        var soLanThu = (int)await AsyncExecuter.CountAsync(
            truyVanGoc.Where(x => x.MerchantReference.StartsWith(goc))) + 1;
        var maThamChieu = Payments.SinhMaThamChieu.Sinh(input.SoDon, thoiDiem, soLanThu);

        var giaoDich = new Payments.PaymentTransaction(
            GuidGenerator.Create(), _cong.ProviderCode, maThamChieu, input.SoTien)
        {
            OrderId = input.OrderId,
            Currency = string.IsNullOrWhiteSpace(input.TienTe) ? "VND" : input.TienTe.Trim()
        };

        await _giaoDich.InsertAsync(giaoDich, autoSave: true);

        Logger.LogInformation(
            "TẠO GIAO DỊCH {Ma}: đơn {SoDon} · {SoTien} {TienTe} · lần thử {LanThu} · cổng {Cong}. [PAY-TAO-MOI]",
            maThamChieu, input.SoDon, giaoDich.Amount, giaoDich.Currency, soLanThu, giaoDich.ProviderCode);

        return await DungKetQuaAsync(giaoDich, laDaCo: false, input);
    }

    /// <summary>
    /// Dựng URL thanh toán cho một giao dịch **đã nằm trong DB** và gói kết quả trả ra.
    /// 🔑 Tách riêng để đường *"tạo mới"* và đường *"dùng lại"* trả về **cùng một hình dạng** — hai bản viết tay
    /// sẽ lệch nhau đúng ở trường mà người dùng đọc (`LaGiaoDichDaCo`).
    /// </summary>
    private async Task<TaoGiaoDichKetQuaDto> DungKetQuaAsync(
        Payments.PaymentTransaction giaoDich,
        bool laDaCo,
        TaoGiaoDichInput input)
    {
        var ketQua = new TaoGiaoDichKetQuaDto
        {
            PaymentTransactionId = giaoDich.Id,
            MerchantReference = giaoDich.MerchantReference,
            ProviderCode = giaoDich.ProviderCode,
            Amount = giaoDich.Amount,
            Currency = giaoDich.Currency,
            TrangThai = giaoDich.Status.ToString(),
            LaGiaoDichDaCo = laDaCo
        };

        var kq = await _cong.CreatePaymentAsync(new Payments.CreatePaymentRequest
        {
            PaymentTransactionId = giaoDich.Id,
            MerchantReference = giaoDich.MerchantReference,
            Amount = giaoDich.Amount,
            Currency = giaoDich.Currency,
            OrderInfo = string.IsNullOrWhiteSpace(input.NoiDung) ? $"Thanh toan don {input.SoDon}" : input.NoiDung!,
            ReturnUrl = input.ReturnUrl.Trim(),
            IpnUrl = input.IpnUrl.Trim(),
            ClientIp = input.ClientIp
        });

        ketQua.CheckoutUrl = kq.CheckoutUrl;

        if (!kq.Success)
        {
            // ⚠️ **Không** ném lỗi và **không** xoá giao dịch: giao dịch đã tạo là **bằng chứng ý định thu tiền**
            // (và là thứ IPN sẽ tra). Gọi lại API sau khi sửa cấu hình sẽ **dùng lại** đúng giao dịch đó
            // (`LaGiaoDichDaCo = true`).
            ketQua.LoiTaoUrl = kq.Error ?? "Cổng không trả URL thanh toán.";

            Logger.LogWarning(
                "TẠO URL THANH TOÁN THẤT BẠI cho giao dịch {Ma}: {Loi} — giao dịch VẪN tồn tại. [PAY-TAO-URL-LOI]",
                giaoDich.MerchantReference, ketQua.LoiTaoUrl);
        }

        return ketQua;
    }

    // ── GHI: HOÀN TIỀN ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Payments.Refund)]
    public async Task<HoanTienKetQuaDto> HoanTienAsync(Guid paymentTransactionId, HoanTienInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.LyDo))
        {
            throw new BusinessException(Payments.PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("lyDo", "Hoàn tiền BẮT BUỘC có lý do (audit + `vnp_OrderInfo`).");
        }

        if (string.IsNullOrWhiteSpace(input.NguoiThucHien))
        {
            throw new BusinessException(Payments.PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("lyDo", "Thiếu `NguoiThucHien` — cần biết AI yêu cầu hoàn tiền (audit).");
        }

        // 🔒 **KHOÁ PHÂN TÁN THEO GIAO DỊCH** — chống hai request cùng lúc gọi cổng **HAI LẦN**.
        // ⚠️ Vì sao vẫn cần khoá dù entity đã có bất biến: khoá chặn ở **tầng gọi cổng** (side effect
        // **không** rollback được), còn bất biến chỉ chặn ở tầng **ghi sổ**. Hai request cùng qua được
        // kiểm tra `ConHoanDuoc` rồi **cả hai** gọi cổng ⇒ hoàn **gấp đôi** — và khi đó bất biến chỉ
        // *phát hiện* (ném ở lần ghi thứ hai) **SAU KHI** tiền đã ra.
        await using var giuCho = await _khoa.TryAcquireAsync(
            $"payments:refund:{paymentTransactionId}", TimeSpan.FromSeconds(15))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "payments:refund").WithData("choGiay", 15);

        var gd = await _giaoDich.FindAsync(paymentTransactionId)
            ?? throw new BusinessException(Payments.PaymentErrorCodes.KhongTimThayGiaoDich)
                .WithData("paymentTransactionId", paymentTransactionId);

        // ⚠️ Kiểm TRƯỚC khi gọi cổng: nếu chắc chắn không hợp lệ thì **không** làm phiền cổng
        // (mỗi lời gọi là một giao dịch thật ở phía đối tác, và có thể bị tính phí).
        if (input.SoTien <= 0m)
        {
            throw new BusinessException(Payments.PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("soTien", input.SoTien);
        }

        if (input.SoTien > gd.ConHoanDuoc)
        {
            throw new BusinessException(Payments.PaymentErrorCodes.HoanVuotSoDaThu)
                .WithData("soDaThu", gd.Amount)
                .WithData("daHoan", gd.RefundedAmount)
                .WithData("conHoanDuoc", gd.ConHoanDuoc)
                .WithData("soTienYeuCau", input.SoTien);
        }

        if (!gd.DungDuocDeHoan)
        {
            // `Initiated`/`Failed` ⇒ **chưa từng thu tiền** ⇒ hoàn là **chi tiền chưa nhận**.
            throw new BusinessException(Payments.PaymentErrorCodes.ChuaThuDuocTienKhongHoanDuoc)
                .WithData("trangThai", gd.Status.ToString())
                .WithData("merchantReference", gd.MerchantReference);
        }

        // 🌐 GỌI CỔNG — dùng `ProviderTransactionId` (từ IPN đã xác thực) để tra giao dịch gốc **tất định**.
        var kq = await _cong.RefundAsync(
            gd.MerchantReference, input.SoTien, input.LyDo, gd.ProviderTransactionId);

        var dto = new HoanTienKetQuaDto
        {
            PaymentTransactionId = gd.Id,
            MerchantReference = gd.MerchantReference,
            DaNhanYeuCau = kq.DaNhanYeuCau,
            ChoXacNhan = kq.ChoXacNhan,
            MaPhanHoi = kq.MaPhanHoi,
            ThongDiep = kq.ThongDiep,
            MaGiaoDichHoan = kq.MaGiaoDichHoan,
            LyDoThatBai = kq.LyDoThatBai,
            LoiHaTang = kq.LoiHaTang
        };

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨🚨 **ĐIỂM QUYẾT ĐỊNH CỦA CẢ TÍNH NĂNG — GHI SỔ HAY KHÔNG.**
        //
        // **CHỈ** gọi `RegisterRefund` khi cổng nói **tiền ĐÃ RA** (`DaHoanThat`). Mã `00` của VNPay
        // nghĩa là *"đã nhận yêu cầu"* — tiền **chưa** ra (xử lý bất đồng bộ ở phía ngân hàng).
        //
        // 📌 Nếu ở đây ghi sổ theo `DaNhanYeuCau` (điều rất dễ làm, vì nó "có vẻ thành công"):
        //   • hệ thống ghi `Refunded` trong khi khách **chưa** nhận tiền ⇒ **sổ sách nói dối**;
        //   • **không ai** theo dõi bước xác nhận (vì mọi thứ trông đã xong);
        //   • khi ngân hàng từ chối, hệ thống **không còn** trạng thái nào để phát hiện ⇒ mất tiền/thất tín.
        // ⇒ Khi `ChoXacNhan`, trả về **nguyên trạng** và **KHÔNG** tăng `RefundedAmount`.
        //    Hành vi này là **ĐÚNG**, không phải "chưa xong": đường xác nhận là `PostTruyVanTrangThaiAsync`.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        if (kq.DaHoanThat)
        {
            gd.RegisterRefund(input.SoTien);   // ⇐ entity **tự** chặn hoàn vượt (bất biến tầng domain)
            await _giaoDich.UpdateAsync(gd, autoSave: true);

            dto.DaHoanThat = true;
            dto.ChoXacNhan = false;

            Logger.LogWarning(
                "ĐÃ HOÀN TIỀN và ĐÃ GHI SỔ: giao dịch={MerchantReference} · số tiền={SoTien} · đã hoàn tổng={DaHoan} " +
                "· trạng thái={TrangThai} · người yêu cầu={NguoiThucHien} · lý do={LyDo}. " +
                "[PAY-DA-HOAN]",
                gd.MerchantReference, input.SoTien, gd.RefundedAmount, gd.Status, input.NguoiThucHien, input.LyDo);
        }
        else if (kq.DaNhanYeuCau)
        {
            // 🔔 Yêu cầu **đã gửi** nhưng **chưa** xác nhận tiền ra ⇒ đây là ca **BÌNH THƯỜNG** của VNPay.
            // Ghi log mức **Warning** (không phải Information) vì cần người/job **theo dõi** — nếu không ai
            // đối soát thì tiền **không** bao giờ được xác nhận.
            Logger.LogWarning(
                "CỔNG ĐÃ NHẬN yêu cầu hoàn tiền nhưng CHƯA xác nhận tiền ra: giao dịch={MerchantReference} · " +
                "số tiền={SoTien} · mã phản hồi={MaPhanHoi} · mã giao dịch hoàn={MaHoan} · người yêu cầu={NguoiThucHien}. " +
                "⇒ KHÔNG ghi sổ; PHẢI đối soát bằng `querydr` trước khi coi là đã hoàn. [PAY-CHO-XAC-NHAN]",
                gd.MerchantReference, input.SoTien, kq.MaPhanHoi, kq.MaGiaoDichHoan, input.NguoiThucHien);
        }
        else
        {
            // 🚨 **PHÂN BIỆT HAI CA KHÁC HẲN NHAU (đã ĐO được — xem chú thích ở `DocPhanHoi`):**
            //   • **LỖI HẠ TẦNG / KHÔNG ĐỌC ĐƯỢC PHẢN HỒI** ⇒ **KHÔNG BIẾT** cổng có **đã nhận** yêu cầu hay chưa.
            //     Nếu trả `ChoXacNhan = false`, tầng gọi **hiểu là** *"chắc chắn chưa hoàn"* ⇒ có thể **thử lại**
            //     ⇒ **hoàn HAI LẦN** (mất tiền thật). ⇒ **PHẢI** bật `ChoXacNhan` để buộc đối soát trước.
            //   • **CỔNG TỪ CHỐI** (đọc được phản hồi, mã khác `00`) ⇒ **biết chắc** cổng đã xử lý và không hoàn
            //     ⇒ `ChoXacNhan = false` là **đúng** (không cần đối soát; người dùng sửa tham số rồi gửi lại).
            // 📌 `LoiHaTang` chính là cờ phân biệt hai ca này — nó **không** chỉ để ghi log.
            if (!string.IsNullOrWhiteSpace(kq.LoiHaTang))
            {
                dto.ChoXacNhan = true;

                Logger.LogError(
                    "KHÔNG XÁC ĐỊNH ĐƯỢC kết quả yêu cầu hoàn tiền (lỗi hạ tầng / không đọc được phản hồi): " +
                    "giao dịch={MerchantReference} · số tiền={SoTien} · lỗi={LoiHaTang} · chi tiết={LyDo}. " +
                    "🚨 KHÔNG BIẾT cổng đã nhận yêu cầu hay chưa ⇒ CẤM thử lại mù (có thể hoàn HAI LẦN); " +
                    "PHẢI tra `querydr` trước. [PAY-KHONG-XAC-DINH]",
                    gd.MerchantReference, input.SoTien, kq.LoiHaTang, kq.LyDoThatBai);
            }
            else
            {
                Logger.LogError(
                    "CỔNG TỪ CHỐI yêu cầu hoàn tiền: giao dịch={MerchantReference} · số tiền={SoTien} · " +
                    "mã phản hồi={MaPhanHoi} · lý do={LyDo}. [PAY-TU-CHOI-HOAN]",
                    gd.MerchantReference, input.SoTien, kq.MaPhanHoi, kq.LyDoThatBai);
            }
        }

        dto.DaHoanTong = gd.RefundedAmount;
        dto.ConHoanDuoc = gd.ConHoanDuoc;
        // 🔒 Đọc trạng thái **từ entity sau thao tác** (không suy diễn từ phản hồi cổng).
        dto.TrangThaiSau = gd.Status.ToString();

        return dto;
    }

    // ── GHI: TRUY VẤN (`querydr`) ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Payments.Refund)]
    public async Task<PaymentTransactionDto> PostTruyVanTrangThaiAsync(Guid paymentTransactionId)
    {
        var gd = await _giaoDich.FindAsync(paymentTransactionId)
            ?? throw new BusinessException(Payments.PaymentErrorCodes.KhongTimThayGiaoDich)
                .WithData("paymentTransactionId", paymentTransactionId);

        var trangThaiTuCong = await _cong.QueryStatusAsync(gd.MerchantReference);

        // 🚨🔑 **LUẬT NẰM Ở MỘT CHỖ DUY NHẤT** — `LuatDoiSoatThanhToan.Quyet` (tầng **Domain**, hàm **THUẦN**).
        // 📌 **VÌ SAO KHÔNG VIẾT LẠI Ở ĐÂY:** job định kỳ (`PaymentReconciliationWorker`) **KHÔNG** gọi được
        // lớp này — `PaymentAppService` mang `[Authorize]` ở **cấp class** (quy ước #4) ⇒ interceptor của ABP
        // kiểm quyền **ngay cả khi gọi trong tiến trình**, mà worker **không có principal** ⇒
        // **`AbpAuthorizationException`** (🔒 lỗi **ĐÃ ĐO ĐƯỢC** ở `SAL-005`).
        // ⚠️ Nếu job **chép** luật sang chỗ nó thì luật *"CHỈ TIẾN, KHÔNG LÙI"* có **HAI** định nghĩa ⇒
        // chúng sẽ **lệch** khi ai đó sửa một chỗ. ✅ Nên cả hai chỗ **gọi cùng một hàm**.
        var quyetDinh = Payments.LuatDoiSoatThanhToan.Quyet(gd.Status, trangThaiTuCong);

        // 📊 **PHÁT METRIC** — dùng **CHUNG** hàm với job (`REC-001` mục 7). 📌 Đặt **trước** `switch` để
        // **mọi** nhánh đều được đếm (⚠️ kể cả `KhongDoi` — hàm tự quyết định có phát hay không).
        Payments.MetricDoiSoatThanhToan.Ghi(_metrics, quyetDinh);

        switch (quyetDinh)
        {
            case Payments.LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien:
                // Cổng xác nhận đã thu tiền ⇒ đây **chính là** lưới an toàn khi IPN **mất** (`REC-001`).
                var daGhiThu = gd.MarkSucceeded(null, null, "{\"nguon\":\"querydr\"}", null);
                await _giaoDich.UpdateAsync(gd, autoSave: true);

                // 🔗 `S4` — **CẦU NỐI SANG ĐƠN**: `querydr` cũng phải phát sự kiện, nếu không thì đơn của đúng những
                // giao dịch **bị mất IPN** (lý do tồn tại của `querydr`) sẽ **vĩnh viễn** `Unpaid` — tức lưới an
                // toàn cứu được **tiền** nhưng không cứu được **đơn**.
                if (daGhiThu)
                {
                    await _suKien.PhatDaThuTienAsync(gd);
                }

                Logger.LogWarning(
                    "ĐỐI SOÁT `querydr` PHÁT HIỆN giao dịch ĐÃ THU TIỀN nhưng chưa được ghi nhận (IPN có thể đã mất): " +
                    "giao dịch={MerchantReference} · số tiền={SoTien} · đã phát sự kiện={DaPhat}. [PAY-QUERYDR-THU-DUOC]",
                    gd.MerchantReference, gd.Amount, daGhiThu);
                break;

            case Payments.LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai:
                // 🚨 `MarkFailed` nay **trả `bool`** (`S4`, đóng một phần `F-PAY-9`) ⇒ đếm/sự kiện chỉ khi **thật sự**
                // đổi trạng thái; trước đây hàm `void` nên chỗ gọi **không thể biết** đã ghi hay bị bỏ.
                var daGhiHong = gd.MarkFailed(trangThaiTuCong == Payments.PaymentTransactionStatus.Cancelled
                    ? "Cổng xác nhận khách ĐÃ HUỶ giao dịch (nguồn: querydr)."
                    : "Cổng xác nhận giao dịch không thành công (nguồn: querydr).");
                await _giaoDich.UpdateAsync(gd, autoSave: true);

                if (daGhiHong)
                {
                    await _suKien.PhatThatBaiAsync(gd, trangThaiTuCong.ToString());
                }
                break;

            default:
                // 🔒 `KhongDoi` gồm **HAI** ca **rất khác nhau** — và 📌 chỗ gọi **phải** phân biệt:
                //   (a) giao dịch **đã ở trạng thái cuối** ⇒ **KHÔNG CẦN** kiểm (bình thường);
                //   (b) cổng nói *"chưa biết"* ⇒ **KHÔNG KIỂM ĐƯỢC** (⚠️ **KHÔNG** phải "đã kiểm và ổn").
                if (Payments.LuatDoiSoatThanhToan.DaKetThuc(gd.Status))
                {
                    Logger.LogDebug(
                        "ĐỐI SOÁT `querydr`: giao dịch {MerchantReference} đã ở trạng thái CUỐI ({TrangThai}) ⇒ không đụng.",
                        gd.MerchantReference, gd.Status);
                }
                else
                {
                    // 🚨 **GAUGE "KHÔNG KIỂM ĐƯỢC"** — 🔑 con số mà biểu đồ dùng để **gắn cảnh báo**.
                    // ⚠️ Với hàm MỘT (tra từng giao dịch) con số này là **1** hoặc **0**; 📌 với **job**
                    // (`REC-001`) nó là **tổng của cả lượt** — và đó mới là con số có ý nghĩa **vận hành**.
                    // 🔒 Vẫn ghi ở đây để **đường tra tay** không thành **điểm mù** về metric.
                    _metrics?.SetPaymentReconUnresolved(1);

                    Logger.LogInformation(
                        "ĐỐI SOÁT `querydr`: cổng CHƯA xác nhận được giao dịch {MerchantReference} (trạng thái cổng={TuCong}) " +
                        "⇒ giữ nguyên {TrangThai}, sẽ thử lại lượt sau. [PAY-QUERYDR-CHUA-BIET]",
                        gd.MerchantReference, trangThaiTuCong, gd.Status);
                }

                break;
        }

        return ChuyenDto(gd);
    }

    /// <inheritdoc />
    public async Task<KetQuaDoiSoatThanhToanDto> PostDoiSoatLoatAsync(int toiDa = 50)
    {
        // 📌 Trần: mỗi giao dịch là **một lời gọi HTTP ra ngoài** ⇒ quét vô hạn sẽ **treo** request và
        // **tiêu quota** của cổng. 🔒 Trần là hằng số **hữu hạn cứng** (không nhận số client gửi thẳng).
        var soLuong = Math.Clamp(toiDa, 1, 100);

        var truyVan = await _giaoDich.GetQueryableAsync();

        // 🔒 **CHỈ xét giao dịch CHƯA ở trạng thái CUỐI.** ⚠️ Gọi cổng cho giao dịch `Succeeded`/`Refunded`
        // là **lãng phí quota** (tài nguyên **có giới hạn** của cổng) mà **không** đổi được gì.
        //
        // 🚨 **BÀI HỌC KIỂU (đã vấp ngay trong lượt này):** `x.Status` ở đây là **ENUM** (entity
        // `PaymentTransaction`), ⚠️ **KHÔNG** phải chuỗi như `PaymentTransactionDto.Status`.
        // 📌 Tôi viết `canDoiSoat` là **mảng chuỗi** ⇒ **`CS1503`** (*"cannot convert from
        // `PaymentTransactionStatus` to `string`"*). 🔑 **DTO và ENTITY có thể đặt CÙNG tên trường với
        // KIỂU KHÁC NHAU** — phải đọc kiểu **từ entity**, không suy từ DTO.
        var canDoiSoat = new[]
        {
            Payments.PaymentTransactionStatus.Initiated,
            Payments.PaymentTransactionStatus.Pending,
            Payments.PaymentTransactionStatus.Failed
        };

        var ds = await AsyncExecuter.ToListAsync(
            truyVan.Where(x => canDoiSoat.Contains(x.Status))
                .OrderBy(x => x.CreationTime)
                .Take(soLuong));

        var ketQua = new KetQuaDoiSoatThanhToanDto { SoDaXet = ds.Count };

        foreach (var gd in ds)
        {
            // ⚠️ **KIỂU KHÁC NHAU — và đây là chỗ dễ sai nhất của hàm này:**
            //   • `gd.Status` (ENTITY `PaymentTransaction`) là **ENUM**;
            //   • `PostTruyVanTrangThaiAsync(...).Status` (DTO `PaymentTransactionDto`) là **CHUỖI**
            //     (`ChuyenDto` làm `gd.Status.ToString()`).
            // 📌 Nên **KHÔNG** so sánh trực tiếp hai giá trị đó — 🔒 phải đưa về **cùng một kiểu**.
            // 🔑 Tôi đã vấp đúng chỗ này trong lượt: viết `sau == truoc` ⇒ **`CS0019`**. 📌 DTO và ENTITY
            // có thể đặt **cùng tên trường** với **kiểu khác nhau** ⇒ phải đọc kiểu **từ entity**.
            var truoc = gd.Status.ToString();

            // 🔑 **GỌI LẠI CHÍNH HÀM MỘT — KHÔNG tự cập nhật DB ở đây.**
            // 📌 Lý do: hàm một đã bảo đảm bất biến **CHỈ TIẾN, KHÔNG LÙI** (không hạ trạng thái khi cổng nói
            // *"chưa biết"*). ⚠️ Nếu hàm lô tự viết lại việc cập nhật thì bất biến đó có **HAI** chỗ phải
            // đúng — và chỗ thứ hai sẽ **lệch** khi ai đó sửa chỗ thứ nhất.
            var sau = (await PostTruyVanTrangThaiAsync(gd.Id)).Status;

            if (string.Equals(sau, truoc, StringComparison.Ordinal))
            {
                // ⚠️ **KHÔNG xác định được** — cổng nói *"chưa biết"*, lỗi hạ tầng, **hoặc chưa cấu hình**:
                // 📌 ba ca đó **giống nhau ở đây** vì `VnPayPaymentProvider.QueryStatusAsync` trả `Pending`
                // cho cả ba (nó **không** ném khi `HttpClient` rỗng). 🔒 Nên con số này nghĩa là
                // *"KHÔNG KIỂM ĐƯỢC"*, **KHÔNG** phải *"đã kiểm và ổn"*.
                ketQua.SoKhongXacDinh++;

                continue;
            }

            ketQua.SoDoiTrangThai++;
            ketQua.MaDaDoi.Add(gd.MerchantReference);

            if (string.Equals(sau, nameof(Payments.PaymentTransactionStatus.Succeeded), StringComparison.Ordinal))
            {
                ketQua.SoThuDuoc++;
            }
        }

        // 🚨 **CẢNH BÁO KHI LƯỢT QUÉT KHÔNG MANG LẠI THÔNG TIN NÀO** — 🔑 đây là dấu hiệu **cấu hình sai**
        // (hoặc cổng chết), **KHÔNG** phải *"hệ thống khoẻ"*. 📌 Nếu không log ca này thì người vận hành đọc
        // *"đã đối soát 50 giao dịch, 0 thay đổi"* và **tin** rằng mọi thứ ổn.
        if (ketQua.SoDaXet > 0 && ketQua.SoKhongXacDinh == ketQua.SoDaXet)
        {
            Logger.LogWarning(
                "ĐỐI SOÁT LÔ: KHÔNG xác định được giao dịch nào trong {So} giao dịch ⇒ lượt quét KHÔNG mang lại " +
                "thông tin. Nguyên nhân thường gặp: cổng CHƯA cấu hình (`PaymentHub:VnPay:TmnCode`/`HashSecret` " +
                "rỗng) hoặc cổng không trả lời. [PAY-DOISOAT-KHONG-XAC-DINH]",
                ketQua.SoDaXet);
        }
        else
        {
            Logger.LogInformation(
                "ĐỐI SOÁT LÔ: xét {So} · đổi trạng thái {Doi} · thu được {Thu} · không xác định {KXD}. [PAY-DOISOAT-XONG]",
                ketQua.SoDaXet, ketQua.SoDoiTrangThai, ketQua.SoThuDuoc, ketQua.SoKhongXacDinh);
        }

        // 📊 **GAUGE "KHÔNG KIỂM ĐƯỢC"** cho CẢ LƯỢT — 🔑 đây là con số có ý nghĩa **vận hành** (hàm một chỉ
        // ghi 1/0). ⚠️ **ĐỌC CHO ĐÚNG:** bằng `SoDaXet` nghĩa là lượt quét **KHÔNG mang lại thông tin nào**
        // ⇒ **cấu hình sai hoặc cổng chết**, **KHÔNG PHẢI** *"hệ thống khoẻ"*.
        _metrics?.SetPaymentReconUnresolved(ketQua.SoKhongXacDinh);

        return ketQua;
    }

    // ── CHUYỂN ĐỔI ──────────────────────────────────────────────────────────────────────────────

    private static PaymentTransactionDto ChuyenDto(Payments.PaymentTransaction gd) => new()
    {
        Id = gd.Id,
        ProviderCode = gd.ProviderCode,
        OrderId = gd.OrderId,
        MerchantReference = gd.MerchantReference,
        ProviderTransactionId = gd.ProviderTransactionId,
        Amount = gd.Amount,
        RefundedAmount = gd.RefundedAmount,
        ConHoanDuoc = gd.ConHoanDuoc,
        Currency = gd.Currency,
        Status = gd.Status.ToString(),
        PaidAtUtc = gd.PaidAtUtc,
        FailureReason = gd.FailureReason,
        WebhookReceivedCount = gd.WebhookReceivedCount,
        CreationTime = gd.CreationTime,
        DaThuTien = gd.Status is Payments.PaymentTransactionStatus.Succeeded
            or Payments.PaymentTransactionStatus.PartiallyRefunded
            or Payments.PaymentTransactionStatus.Refunded
    };
}
