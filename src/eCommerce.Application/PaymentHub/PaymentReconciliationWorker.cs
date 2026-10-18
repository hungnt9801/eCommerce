using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Application.PaymentHub;

/// <summary>
/// 🚨 **JOB ĐỐI SOÁT THANH TOÁN ĐỊNH KỲ** (`REC-001` mục 7) — biến một endpoint **phải bấm tay** thành
/// **cơ chế tự chạy**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO CẦN JOB NÀY (không đủ endpoint `doi-soat-loat`):**
///
/// Ở lượt trước tôi đã thêm `IPaymentAppService.PostDoiSoatLoatAsync` — **đường gọi** đã có. ⚠️ Nhưng
/// **vẫn phải có NGƯỜI bấm**. 📌 Và đối soát là công việc **ĐỊNH KỲ**, không phải việc ai đó **nhớ ra**.
/// 🚨 Hệ quả nếu chỉ có endpoint: **IPN mất** ⇒ giao dịch kẹt `Pending` ⇒ 🔑 **tiền đã vào mà hệ thống không
/// biết** — cho tới khi **tình cờ** có người mở trang và bấm. **Không có lỗi nào** để nhắc họ.
///
/// 🚨 **VÀ BÀI HỌC QUYẾT ĐỊNH CỦA LƯỢT NÀY — vì sao lớp này KHÔNG gọi `IPaymentAppService`:**
/// `PaymentAppService` mang **`[Authorize]` ở cấp class** (quy ước #4 **bắt buộc** cho mọi app service) ⇒
/// interceptor của ABP kiểm quyền **NGAY CẢ KHI GỌI TRONG TIẾN TRÌNH**, mà worker **KHÔNG có principal**
/// ⇒ **`AbpAuthorizationException`**.
/// 📌 **Lỗi này ĐÃ XẢY RA THẬT** ở `SAL-005`: `MarketplaceReconciliationWorker` gọi `IDeadLetterAppService`
/// ⇒ job **chết giữa đường**, log chỉ có neo **mở đầu** và **không** có neo kết thúc — 🔒 và tệ hơn: các ca
/// kiểm *"job không ghi gì"* **vẫn XANH** vì job **chết**, không phải vì job **cố ý** không ghi.
///
/// ⚠️ **VÀ LỐI SAI CÒN LẠI — chép luật sang đây:** nếu lớp này tự viết lại *"khi nào đổi trạng thái"* thì luật
/// *"CHỈ TIẾN, KHÔNG LÙI"* có **HAI** định nghĩa ⇒ 🔒 chúng sẽ **lệch** khi ai đó sửa một chỗ.
/// ✅ **LỐI ĐÚNG (lượt này):** cả app service **và** job gọi **cùng một hàm THUẦN** ở tầng Domain —
/// <see cref="LuatDoiSoatThanhToan.Quyet"/> — 🔑 **một định nghĩa, hai chỗ gọi**.
///
/// 📌 **Và lớp này đọc `IRepository<PaymentTransaction, Guid>` TRỰC TIẾP** (không qua app service) — cùng
/// khuôn `MarketplaceReconciliationWorker` ở `SAL-005` §19.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class PaymentReconciliationWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>
    /// Nhịp chạy: **15 phút**. 📌 Cân nhắc: đối soát **gọi HTTP ra cổng** cho **từng** giao dịch ⇒ 🔒 **không**
    /// được dày như `MarketplaceReconciliationWorker` (10 phút, chỉ **đọc** DB). ⚠️ Nhịp dày hơn **không**
    /// làm cảnh báo tốt hơn — 📌 điều quyết định là **con người có đọc log hay không** — mà lại **tiêu quota**
    /// của cổng (tài nguyên **có giới hạn**).
    /// </summary>
    public const int NhipMacDinhGiay = 900;

    /// <summary>
    /// Trần số giao dịch **một lượt**. 📌 Mỗi giao dịch là **một lời gọi HTTP ra ngoài**: đặt trần để một
    /// tồn đọng lớn **không** làm lượt quét chạy hàng giờ và **không** tiêu hết quota trong một lần.
    /// 🔑 Giao dịch còn lại sẽ được xử lý ở **lượt sau** (truy vấn sắp xếp theo `CreationTime` **cũ nhất trước**).
    /// </summary>
    public const int TranMotLuot = 50;

    /// <summary>Tên khoá phân tán (**không** tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm).</summary>
    public const string TenKhoa = "payment:reconciliation-rec001";

    /// <summary>
    /// 📊 `REC-001` mục 7 — metric đối soát. ⚠️ **TUỲ CHỌN** (`= null`) cùng khuôn `VnPayPaymentProvider`:
    /// 🔒 nếu bắt buộc thì thiếu đăng ký metric sẽ **NỔ** ngay lúc **khởi động Host** ⇒ job **không bao giờ
    /// chạy** mà lỗi lại trỏ vào chỗ **không liên quan**.
    /// </summary>
    private readonly eCommerce.Kernel.Observability.eCommerceMetrics? _metrics;

    public PaymentReconciliationWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
        : base(timer, serviceScopeFactory)
    {
        _metrics = metrics;

        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 Chạy NGAY lần đầu: chờ hết nhịp 15 phút thì trong 15 phút đầu sau **mỗi lần deploy**, giao dịch
        // kẹt `Pending` **không có số liệu nào** — đúng khoảng trống job này sinh ra để bịt.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 🚨 **CÂU LỆNH ĐẦU TIÊN — BẮT BUỘC** (`BackgroundWorkerCoCheTests`): không có dòng này thì
        // *"job im lặng"* **không phân biệt được** với *"job không chạy"*.
        Logger.LogInformation("JOB REC-001: bắt đầu lượt đối soát thanh toán (mốc {Moc}). [PAY-JOB-BAT-DAU]", DateTime.UtcNow);

        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(120));

        if (handle is null)
        {
            // `Debug` (không `Warning`): instance khác đang chạy là **bình thường**, không phải sự cố.
            Logger.LogDebug("JOB REC-001: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        using var scope = workerContext.ServiceProvider.CreateScope();

        // 🚨 **`S5` — VÁ `F-PAY-7`: BỎ UoW BAO CẢ LƯỢT.**
        //
        // **Lỗi cũ (mức 🔴 Cao):** `uowManager.Begin(requiresNew: true, isTransactional: true)` bọc **toàn bộ**
        // lượt quét ⇒ một giao dịch ném (`23505`/`23514`/`DbUpdateException`) ở giữa ⇒ **rollback cả 50** lần ghi
        // đã xong, trong khi **quota cổng đã tiêu** ⇒ *"đã xác nhận với cổng mà sổ không biết"*. 📌 Kết quả đối
        // soát là **bằng chứng về tiền**; mất nó là mất bằng chứng (và nếu cổng chết hẳn thì mất **vĩnh viễn**).
        //
        // ✅ **Nay:** mỗi giao dịch tự mở UoW **riêng** trong <see cref="DoiSoatAsync"/> ⇒ *"đã ghi thì còn"*.
        // ⚠️ Vì vậy chỗ này **không** mở UoW nữa — mở ở đây là quay lại đúng lỗi vừa vá.
        await DoiSoatAsync(scope.ServiceProvider);

        // 🚨 **NEO KẾT THÚC** — 🔑 bắt buộc để phân biệt *"job chạy XONG"* với *"job chạy rồi NÉM"*.
        // 📌 Bài học **ĐO ĐƯỢC** ở `SAL-005`: lượt chạy đầu chỉ có neo mở đầu mà **không** có neo kết thúc
        // ⇒ job **ném giữa đường**, và 🔒 nếu không có neo này thì **không có cách nào** phân biệt hai ca đó từ log.
        Logger.LogInformation("JOB REC-001: xong lượt (mốc {Moc}). [PAY-JOB-XONG]", DateTime.UtcNow);
    }

    /// <summary>
    /// **MỘT LƯỢT ĐỐI SOÁT** — 🔑 **cùng luật** với `IPaymentAppService.PostTruyVanTrangThaiAsync`, nhưng
    /// **không** đi qua app service (xem chú thích lớp để biết vì sao).
    /// </summary>
    private async Task DoiSoatAsync(IServiceProvider services)
    {
        var giaoDich = services.GetRequiredService<IRepository<PaymentTransaction, Guid>>();
        var cong = services.GetRequiredService<IPaymentProvider>();
        var uowManager = services.GetRequiredService<IUnitOfWorkManager>();

        // 🆕 `S5` — **CẦU NỐI SANG ĐƠN** (`IPaymentEventPublisher` ở `Application.Contracts`, hiện thực ở
        // `PaymentHub.Application`). 🔑 Trước `S5`, job này **không** phát sự kiện (không với tới được lớp phát
        // vì luật phân tầng) ⇒ job phát hiện *"khách đã trả"*, ghi `Succeeded`, mà **đơn vẫn `Unpaid`**.
        // 📌 Không phải app service ⇒ **không** có `[Authorize]` ⇒ gọi an toàn từ worker (bài học `SAL-005`).
        var suKien = services.GetRequiredService<eCommerce.PaymentHub.Events.IPaymentEventPublisher>();

        // 🔒 **CHỈ xét giao dịch CHƯA ở trạng thái CUỐI.** ⚠️ Gọi cổng cho giao dịch `Succeeded`/`Refunded`
        // là **lãng phí quota** — tài nguyên **có giới hạn** của cổng.
        // ⚠️ **KIỂU:** `PaymentTransaction.Status` là **ENUM** (entity) — 📌 **KHÁC** `PaymentTransactionDto.Status`
        // (là **CHUỖI**). 🔑 Đây đúng chỗ tôi đã vấp ở lượt trước (`CS1503`/`CS0019`).
        var trangThaiCoTheDoiSoat = new[]
        {
            PaymentTransactionStatus.Initiated,
            PaymentTransactionStatus.Pending,
            PaymentTransactionStatus.Failed
        };

        var danhSach = await giaoDich.GetListAsync(x => trangThaiCoTheDoiSoat.Contains(x.Status));

        // 📌 **CŨ NHẤT TRƯỚC** rồi mới cắt trần: giao dịch kẹt **lâu nhất** là giao dịch **đáng lo nhất**
        // (📌 nếu lấy mới nhất trước thì một tồn đọng lớn sẽ khiến giao dịch cũ **không bao giờ** được xét →
        // 🔒 đó là *"đói"* (starvation) và nó **im lặng**).
        var canXet = danhSach
            .OrderBy(x => x.CreationTime)
            .Take(TranMotLuot)
            .ToList();

        if (canXet.Count == 0)
        {
            Logger.LogInformation("JOB REC-001: không có giao dịch nào cần đối soát. [PAY-JOB-RONG]");
            return;
        }

        if (danhSach.Count > TranMotLuot)
        {
            // 🚨 **NÓI RÕ LÀ ĐÃ CẮT BỚT** — ⚠️ im lặng cắt bớt sẽ khiến người đọc tưởng **đã** xét hết.
            Logger.LogWarning(
                "JOB REC-001: có {Tong} giao dịch cần đối soát nhưng trần một lượt là {Tran} ⇒ lượt này xét {Xet} " +
                "(cũ nhất trước), phần còn lại ở lượt sau. [PAY-JOB-CAT-BOT]",
                danhSach.Count, TranMotLuot, canXet.Count);
        }

        var soDoi = 0;
        var soThuDuoc = 0;
        var soKhongXacDinh = 0;
        var soLoi = 0;

        foreach (var gd in canXet)
        {
            var tuCong = await cong.QueryStatusAsync(gd.MerchantReference);

            // 🚨🔑 **LUẬT Ở MỘT CHỖ DUY NHẤT** — cùng hàm mà app service gọi.
            var quyetDinh = LuatDoiSoatThanhToan.Quyet(gd.Status, tuCong);

            // 📊 **PHAT METRIC** — 🔑 dung **CHUNG** ham voi app service (`GhiMetricDoiSoat`): mot dinh nghia,
            // hai cho goi. ⚠️ Neu chep thi metric se LECH khi ai do sua mot cho - va lech o metric thi AM THAM
            // (bieu do van ve, chi SAI SO).
            MetricDoiSoatThanhToan.Ghi(_metrics, quyetDinh);

            // ══════════════════════════════════════════════════════════════════════════════════════════════
            // 🚨🚨 **`S5` — MỖI GIAO DỊCH MỘT UoW (`F-PAY-7`) + ĐỌC GIÁ TRỊ `bool` (`F-PAY-9` đóng NỐT).**
            //
            // ① **Một UoW cho TỪNG giao dịch:** lỗi ở giao dịch này không rollback 49 giao dịch đã ghi.
            // ② 🚨 **`MarkSucceeded`/`MarkFailed` TRẢ `bool`** (từ `S4`) — trước `S5` chỗ này **bỏ qua** giá trị
            //    đó và `soDoi++` **vô điều kiện** ⇒ job báo *"đã đổi 5 trạng thái"* trong khi DB **không đổi
            //    dòng nào** (ca thật: cổng trả `Failed` **lặp lại** cho một giao dịch đã `Failed` — `MarkFailed`
            //    trả `false` nhưng vẫn bị đếm). 📌 Con số đó là thứ người vận hành dùng để quyết định *"có sự cố
            //    không"*, nên sai ở đây là **báo cáo sai**, đúng `F-PAY-9`.
            // ══════════════════════════════════════════════════════════════════════════════════════════════
            try
            {
                using var uowTungGiaoDich = uowManager.Begin(requiresNew: true, isTransactional: true);

                switch (quyetDinh)
                {
                    case LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien:
                        var daGhiThu = gd.MarkSucceeded(null, null, "{\"nguon\":\"querydr-job\"}", null);
                        await giaoDich.UpdateAsync(gd, autoSave: true);

                        if (daGhiThu)
                        {
                            soDoi++;
                            soThuDuoc++;

                            // 🔗 **`S5`** — phát sự kiện **trong cùng UoW** với lần ghi ⇒ *"không ghi được đơn
                            // thì cũng không phát"* (nhất quán), và đơn **được** đánh dấu đã trả.
                            await suKien.PhatDaThuTienAsync(gd);
                        }

                        // `Warning` (không `Information`): đây là **bất thường thật** — IPN đã mất, hoặc cổng
                        // chậm. 🔑 Một dòng log ở mức `Information` giữa hàng nghìn dòng khác thì **không ai thấy**.
                        Logger.LogWarning(
                            "JOB REC-001: PHÁT HIỆN giao dịch ĐÃ THU TIỀN nhưng chưa ghi nhận (IPN có thể đã mất): " +
                            "giao dịch={MerchantReference} · số tiền={SoTien} · đã ghi={DaGhi}. [PAY-JOB-THU-DUOC]",
                            gd.MerchantReference, gd.Amount, daGhiThu);
                        break;

                    case LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai:
                        var daGhiHong = gd.MarkFailed(tuCong == PaymentTransactionStatus.Cancelled
                            ? "Cổng xác nhận khách ĐÃ HUỶ giao dịch (nguồn: job đối soát)."
                            : "Cổng xác nhận giao dịch không thành công (nguồn: job đối soát).");
                        await giaoDich.UpdateAsync(gd, autoSave: true);

                        // 🔒 `S5`: **chỉ** đếm + phát khi **thật sự** đổi trạng thái. 📌 Cổng trả `Failed` lặp lại
                        // là ca **có thật** (IPN retry, hoặc giao dịch `Failed` từ lâu vẫn nằm trong lô đối soát).
                        if (daGhiHong)
                        {
                            soDoi++;
                            await suKien.PhatThatBaiAsync(gd, tuCong.ToString());
                        }

                        break;

                    default:
                        // 🔒 `KhongDoi` gồm **HAI** ca **rất khác nhau** — phải **phân biệt**:
                        //   (a) đã ở trạng thái cuối ⇒ **KHÔNG CẦN** kiểm (bình thường);
                        //   (b) cổng nói *"chưa biết"* ⇒ **KHÔNG KIỂM ĐƯỢC** (⚠️ **KHÔNG** phải "đã kiểm và ổn").
                        if (!LuatDoiSoatThanhToan.DaKetThuc(gd.Status))
                        {
                            soKhongXacDinh++;
                        }

                        break;
                }

                await uowTungGiaoDich.CompleteAsync();
            }
            catch (Exception ex)
            {
                // ⚠️ Bắt **mọi** exception là **có chủ ý** (`F-PAY-7`: một giao dịch hỏng không được làm mất bằng
                // chứng của các giao dịch khác) — nhưng **không nuốt**: log `Error` + đếm, để người vận hành thấy.
                soLoi++;

                Logger.LogError(ex,
                    "JOB REC-001: LỖI khi đối soát giao dịch {MerchantReference} ({Id}) ⇒ bỏ qua giao dịch này, " +
                    "các giao dịch khác trong lượt VẪN được ghi. [PAY-JOB-LOI]",
                    gd.MerchantReference, gd.Id);
            }
        }

        // 🚨 **CẢNH BÁO KHI LƯỢT QUÉT KHÔNG MANG LẠI THÔNG TIN NÀO** — 🔑 đây là dấu hiệu **cấu hình sai**
        // (hoặc cổng chết), **KHÔNG** phải *"hệ thống khoẻ"*. 📌 Nếu không log ca này thì người vận hành đọc
        // *"đã xét N giao dịch, 0 thay đổi"* và **tin** rằng mọi thứ ổn.
        // 📊 **GAUGE "KHONG KIEM DUOC" cho CA LUOT** — 🔑 con so co y nghia VAN HANH.
        // ⚠️ DOC CHO DUNG: bang so giao dich can doi soat nghia la luot quet KHONG mang lai thong tin nao
        // => CAU HINH SAI hoac CONG CHET, KHONG PHAI "he thong khoe".
        _metrics?.SetPaymentReconUnresolved(soKhongXacDinh);

        if (soKhongXacDinh == canXet.Count)
        {
            Logger.LogWarning(
                "JOB REC-001: KHÔNG xác định được giao dịch nào trong {So} giao dịch ⇒ lượt quét KHÔNG mang lại " +
                "thông tin. Nguyên nhân thường gặp: cổng CHƯA cấu hình (`PaymentHub:VnPay:TmnCode`/`HashSecret` " +
                "rỗng) hoặc cổng không trả lời. [PAY-JOB-KHONG-XAC-DINH]",
                canXet.Count);
        }
        else
        {
            Logger.LogInformation(
                "JOB REC-001: xét {Xet} · đổi trạng thái {Doi} · thu được {Thu} · không xác định {KXD} · lỗi {Loi}. " +
                "[PAY-JOB-KET-QUA]",
                canXet.Count, soDoi, soThuDuoc, soKhongXacDinh, soLoi);
        }

        // 🚨 **`S5` — CÓ GIAO DỊCH XỬ LÝ LỖI THÌ PHẢI LA.** 📌 Trước `S5`, một giao dịch hỏng làm **rollback cả
        // lượt** ⇒ triệu chứng là *"job chạy xong mà không đổi gì"* — **không ai** biết đã mất 49 lần ghi.
        // Nay lỗi **cô lập** (đúng `F-PAY-7`) ⇒ nhưng nếu **không đếm/không log** thì nó lại **im lặng**:
        // lượt vẫn "xong", chỉ có ít thay đổi hơn mong đợi. 🔒 Neo ASCII để tìm được trong log (bài học `I4`).
        if (soLoi > 0)
        {
            Logger.LogError(
                "JOB REC-001: {Loi}/{Xet} giao dịch XỬ LÝ LỖI ⇒ các giao dịch đó KHÔNG được đối soát lượt này " +
                "(các giao dịch khác ĐÃ được ghi). Xem dòng `[PAY-JOB-LOI]` để biết từng mã. [PAY-JOB-CO-LOI]",
                soLoi, canXet.Count);
        }
    }
}
