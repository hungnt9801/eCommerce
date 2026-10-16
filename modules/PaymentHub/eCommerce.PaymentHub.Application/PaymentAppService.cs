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

    public PaymentAppService(
        IRepository<Payments.PaymentTransaction, Guid> giaoDich,
        Payments.IPaymentProvider cong,
        Volo.Abp.DistributedLocking.IAbpDistributedLock khoa)
    {
        _giaoDich = giaoDich;
        _cong = cong;
        _khoa = khoa;
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

        switch (quyetDinh)
        {
            case Payments.LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien:
                // Cổng xác nhận đã thu tiền ⇒ đây **chính là** lưới an toàn khi IPN **mất** (`REC-001`).
                gd.MarkSucceeded(null, null, "{\"nguon\":\"querydr\"}", null);
                await _giaoDich.UpdateAsync(gd, autoSave: true);

                Logger.LogWarning(
                    "ĐỐI SOÁT `querydr` PHÁT HIỆN giao dịch ĐÃ THU TIỀN nhưng chưa được ghi nhận (IPN có thể đã mất): " +
                    "giao dịch={MerchantReference} · số tiền={SoTien}. [PAY-QUERYDR-THU-DUOC]",
                    gd.MerchantReference, gd.Amount);
                break;

            case Payments.LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai:
                gd.MarkFailed(trangThaiTuCong == Payments.PaymentTransactionStatus.Cancelled
                    ? "Cổng xác nhận khách ĐÃ HUỶ giao dịch (nguồn: querydr)."
                    : "Cổng xác nhận giao dịch không thành công (nguồn: querydr).");
                await _giaoDich.UpdateAsync(gd, autoSave: true);
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
