using eCommerce.Messaging;
using eCommerce.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Application.Marketplace;

/// <summary>
/// 🚨 **JOB ĐỐI SOÁT `D12` + CẢNH BÁO DLQ** (`SAL-005` · `XC-002`) — **hai nợ chung một đường gọi**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO JOB NÀY TỒN TẠI — đọc kỹ, vì có một điều nó CỐ Ý KHÔNG LÀM:**
///
/// <b>(1) Cờ `GapSuspected` ĐANG TỒN ĐỌNG.</b> Luật watermark (`SAL-005` `BR-009`) **BẬT** cờ này khi một bản
/// cập nhật nhảy quá xa (`> 3600 s`), nghĩa là *"có thể đã mất một bản trung gian"*. `DanhDauDaDoiSoat`
/// **đã có** — nhưng **chưa ai gọi nó** ⇒ 🔑 một **dấu vết cảnh báo** được tạo ra rồi **không bao giờ được
/// đọc**. 📌 Cùng loại hỏng mà cả dự án xoay quanh: *"đo mà không ai xem kết quả"*.
///
/// <b>(2) DLQ ĐÃ CÓ BẰNG CHỨNG NHƯNG CHƯA AI ĐỌC.</b> `XungDot`/`DenMuon` **đẩy DLQ** (lượt trước), và DLQ
/// **đã có 4 endpoint** (lượt trước nữa) — ⚠️ nhưng vẫn **chỉ đọc được khi có người mở trang**. 🔑 Một hàng đợi
/// cần **ai đó nhìn nó định kỳ**, nếu không thì *"còn 40 dòng từ ba tuần"* **không** thành cảnh báo nào.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **ĐIỀU JOB NÀY CỐ Ý *KHÔNG* LÀM — VÀ ĐÓ LÀ QUYẾT ĐỊNH QUAN TRỌNG NHẤT CỦA LỚP NÀY:**
///
/// **Nó KHÔNG tự hạ cờ `GapSuspected`.**
///
/// 📌 Lý do: `DanhDauDaDoiSoat` **BẮT BUỘC có lý do** vì hạ cờ là **khẳng định** *"đã kiểm và dữ liệu khớp"*.
/// Một job **không thể** khẳng định điều đó — nó **không** gọi được sàn (`EnableLiveCalls = false`, không có
/// credential sandbox). 🔒 Nếu job tự hạ cờ, nó **xoá mất bằng chứng** về khả năng mất dữ liệu **mà không
/// hề kiểm gì** ⇒ biến dấu vết cảnh báo thành **dấu vết giả**. 📌 Tệ hơn: sau khi bị hạ, **không ai** còn
/// biết đơn đó từng bị nghi ngờ.
///
/// 🔑 **Vậy job làm gì?** Nó biến *"cờ tồn đọng im lặng"* thành **DANH SÁCH VIỆC ĐỌC ĐƯỢC + ĐẾM ĐƯỢC +
/// CÓ TUỔI**: đếm bao nhiêu, **cũ nhất từ bao giờ**, và log **khoá nghiệp vụ** của các đơn quá hạn.
/// ⇒ Người vận hành có **danh sách cụ thể** để đối soát, thay vì phải tự đi tìm.
/// ⚠️ Đây là **đường gọi**, không phải **tự động sửa** — và sự khác biệt đó là **cố ý**.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 📌 **Theo đúng khuôn worker ĐÃ ĐƯỢC CƠ CHẾ HOÁ** (`BackgroundWorkerCoCheTests` bắt buộc):
///   • 🚨 **câu lệnh ĐẦU TIÊN của `DoWorkAsync` PHẢI là `Logger.LogInformation`** (§vòng 53) ⇒ *"im lặng"* **luôn**
///     nghĩa là *"không chạy"*;
///   • **bọc trong `UnitOfWork` + scope** (dịch vụ miền giữ `DbContext` của scope khác ⇒ `ObjectDisposedException`);
///   • **khoá phân tán** (⚠️ **không** thêm tiền tố `ecommerce:` — `KeyPrefix` đã thêm);
///   • **`Timer.RunOnStart = true`** ⇒ có số liệu **ngay sau restart**, không chờ hết nhịp.
/// </summary>
public class MarketplaceReconciliationWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>
    /// Nhịp chạy: **10 phút**. 📌 Cân nhắc: job **chỉ đọc** (hai truy vấn có index) nên **rẻ**; nhưng nhịp
    /// dày hơn **không** làm cảnh báo tốt hơn — ⚠️ điều quyết định là **con người có đọc hay không**.
    /// 🔑 10 phút là đủ để *"biết trong ngày"* mà **không** làm ngập log.
    /// </summary>
    public const int NhipMacDinhGiay = 600;

    /// <summary>
    /// 🚨 **NGƯỠNG CẢNH BÁO TUỔI**: một dòng DLQ **chưa xử lý** quá **24 giờ** ⇒ **cảnh báo mức `Warning`**.
    /// 📌 **VÌ SAO CẦN NGƯỠNG TUỔI (không chỉ đếm số lượng):** 5 dòng mới trong 1 giờ là **bình thường**
    /// (sàn vừa gửi lệch thứ tự), còn 5 dòng từ **3 tuần** là **báo động** — cùng con số, **hai ý nghĩa
    /// trái ngược**. 🔒 Một hàng đợi chỉ có *"số lượng"* mà không có *"tuổi"* thì **không** phân biệt được hai ca đó.
    /// </summary>
    public const int NguongTuoiCanhBaoGio = 24;

    /// <summary>
    /// 🚨 **SỐ DÒNG TỐI ĐA IN RA** khi log danh sách cần đối soát. 📌 Log là để **con người đọc**; in 10 000
    /// dòng thì **không ai đọc** ⇒ phải có trần. 🔒 Và khi chạm trần, log **NÓI RÕ** là đã cắt bớt
    /// (⚠️ im lặng cắt bớt sẽ khiến người đọc tưởng **đã** thấy hết).
    /// </summary>
    public const int TranDongInRa = 20;

    /// <summary>Tên khoá phân tán (**không** tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm).</summary>
    public const string TenKhoa = "marketplace:reconciliation-d12";

    private readonly IAsyncQueryableExecuter _thucThi;

    public MarketplaceReconciliationWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IAsyncQueryableExecuter thucThi)
        : base(timer, serviceScopeFactory)
    {
        // 🚨 **TIÊM `IAsyncQueryableExecuter`** — 📌 lớp này **KHÔNG** kế thừa `ApplicationService` nên
        // **KHÔNG** có thuộc tính `AsyncExecuter` (dùng nó là **`CS0103`** — lỗi tôi **đã vấp 2 lần**:
        // `ChannelDirectory` và `HienThiKenhAppService` ở `CAT-003` §24.4). 🔑 Lần này tiêm **ngay từ đầu**.
        _thucThi = thucThi;
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 Chạy NGAY lần đầu: chờ hết nhịp 10 phút thì trong 10 phút đầu sau **mỗi lần deploy**, cờ
        // `GapSuspected` và DLQ **không có số liệu nào** — đúng khoảng trống job này sinh ra để bịt.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 🚨 **CÂU LỆNH ĐẦU TIÊN — BẮT BUỘC** (`BackgroundWorkerCoCheTests`, vòng 53): không có dòng này thì
        // *"job im lặng"* **không phân biệt được** với *"job không chạy"*.
        Logger.LogInformation("JOB D12: bắt đầu lượt đối soát sàn + kiểm DLQ (mốc {Moc}). [D12-JOB-BAT-DAU]", DateTime.UtcNow);

        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(60));

        if (handle is null)
        {
            // `Debug` (không `Warning`): instance khác đang chạy là **bình thường**, không phải sự cố.
            Logger.LogDebug("JOB D12: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        // 📌 **`isTransactional: false`** — job này **CHỈ ĐỌC** (và đặt một con số metric). 🔑 Mở transaction
        // chỉ-đọc là **lãng phí** và giữ kết nối DB lâu hơn cần thiết. ⚠️ Nếu sau này job **ghi** (ví dụ tự
        // hạ cờ — điều tôi **cố ý** không làm), **phải** đổi thành `true` để ghi là nguyên tử.
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);

        await KiemDlqAsync(scope.ServiceProvider);

        await KiemGapSuspectedAsync(scope.ServiceProvider);

        await uow.CompleteAsync();

        // 🚨 **NEO KET THUC** — 🔑 bat buoc de phan biet *"job chay XONG"* voi *"job chay roi NEM"*.
        // 📌 Bai hoc **DO DUOC ngay luot nay**: lan chay dau chi co neo `[D12-JOB-BAT-DAU]` ma **khong** co neo
        // ket thuc ⇒ job **nem giua duong** (`AbpAuthorizationException`), va 🔒 neu **khong** co neo nay thi
        // khong co cach nao phan biet hai ca do tu log.
        Logger.LogInformation("JOB D12: xong luot (moc {Moc}). [D12-JOB-XONG]", DateTime.UtcNow);
    }

    /// <summary>
    /// **PHẦN 1 — DLQ CÓ ĐANG TỒN ĐỌNG KHÔNG?** 📌 Đặt **một con số** vào metric `messaging_dlq_size`
    /// (đã có sẵn trong `eCommerceMetrics`) ⇒ biểu đồ có dữ liệu **kể cả khi không ai mở trang**.
    /// </summary>
    private async Task KiemDlqAsync(IServiceProvider services)
    {
        // 🚨 **PHAI DOC REPOSITORY TRUC TIEP — KHONG GOI `IDeadLetterAppService`** (va day la bai hoc
        // **DO DUOC** ngay luot nay): lan chay dau, worker goi `dlq.GetThongKeAsync()` va **NEM**
        // `AbpAuthorizationException` ⇒ job **chet giua duong**, chi con neo `[D12-JOB-BAT-DAU]`.
        //
        // 🔑 **VI SAO:** `DeadLetterAppService` mang `[Authorize(...)]` o **cap class** (quy uoc #4, bat buoc
        // boi `BatBienPhanQuyenTests.TC01`) ⇒ interceptor cua ABP kiem quyen **NGAY CA KHI GOI TRONG TIEN TRINH**
        // va worker **KHONG CO principal** ⇒ bi tu choi.
        // 📌 Day la **cung ho** voi loi da gap o `CatMediaController` (`CAT-003` §22): *"`[AllowAnonymous]` o
        // controller goi app service `[Authorize]`"* ⇒ interceptor chan **trong tien trinh**.
        // ⚠️ Va no **nguy hiem** hon ve mat van hanh: job lai **KHONG BAO GIO** chay xong, nhung **khong** co
        // thong bao nao cho nguoi van hanh — 🔑 chi co duong doc log moi thay.
        // 🔒 Cach dung: job doc **repository** (khong qua tang phan quyen); con nguoi dung di qua app service.
        // 📌 Cung quy tac da ap dung cho `CatMediaController` — va **nhat quan** thi de kiem hon.
        var kho = services.GetRequiredService<IRepository<Messaging.DeadLetter.DeadLetterMessage, Guid>>();
        var metric = services.GetRequiredService<eCommerce.Kernel.Observability.eCommerceMetrics>();

        var truyVanDlq = await kho.GetQueryableAsync();

        var soChuaXuLy = await _thucThi.CountAsync(truyVanDlq.Where(x => !x.DaXuLy));
        var soDaXuLy = await _thucThi.CountAsync(truyVanDlq.Where(x => x.DaXuLy));
        var cuNhatChuaXuLy = await _thucThi.FirstOrDefaultAsync(
            truyVanDlq.Where(x => !x.DaXuLy).OrderBy(x => x.VaoDlqLucUtc).Select(x => (DateTime?)x.VaoDlqLucUtc));
        var capLyDo = await _thucThi.ToListAsync(truyVanDlq.Where(x => !x.DaXuLy).Select(x => x.LyDo));
        var theoLyDo = capLyDo.GroupBy(x => x.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var thongKe = new ThongKeDlqDto
        {
            SoChuaXuLy = soChuaXuLy,
            SoDaXuLy = soDaXuLy,
            CuNhatChuaXuLyUtc = cuNhatChuaXuLy,
            TheoLyDo = theoLyDo
        };

        metric.SetMessagingDlqSize(thongKe.SoChuaXuLy);

        // 📌 Log **vô điều kiện** cho cả hai ca — kể cả khi **rỗng**. 🔑 *"0 dòng"* phải đọc được là
        // *"đã kiểm và rỗng"*, **không** phải *"chưa kiểm"*. 📌 Đây **chính là** bài học `REC-001` §vòng 34:
        // metric `0` **trở thành lời nói dối** nếu không ai chạy phép đo.
        if (thongKe.SoChuaXuLy == 0)
        {
            Logger.LogInformation(
                "JOB D12: DLQ RỖNG (đã xử lý {DaXuLy} dòng). [D12-DLQ-RONG]",
                thongKe.SoDaXuLy);
            return;
        }

        var tuoiGio = thongKe.CuNhatChuaXuLyUtc is { } cuNhat
            ? (DateTime.UtcNow - cuNhat).TotalHours
            : 0;

        // 🚨 **NGƯỠNG TUỔI** — xem chú thích `NguongTuoiCanhBaoGio` để biết vì sao số lượng **không đủ**.
        if (tuoiGio >= NguongTuoiCanhBaoGio)
        {
            Logger.LogWarning(
                "🚨 JOB D12: DLQ TỒN ĐỌNG {So} dòng chưa xử lý; dòng CŨ NHẤT từ {CuNhat} ({TuoiGio:F1} giờ trước, "
                + "ngưỡng {Nguong} giờ). Theo nguyên nhân: {TheoLyDo}. [D12-DLQ-TON-DONG]",
                thongKe.SoChuaXuLy, thongKe.CuNhatChuaXuLyUtc, tuoiGio, NguongTuoiCanhBaoGio,
                string.Join(", ", thongKe.TheoLyDo.Select(x => $"{x.Key}={x.Value}")));
        }
        else
        {
            // 📌 `Information` (không `Warning`): dòng **mới** là **bình thường** — sàn gửi lệch thứ tự là
            // chuyện xảy ra. ⚠️ Cảnh báo ở đây sẽ **làm ngập log** và rồi **không ai đọc cảnh báo thật**.
            Logger.LogInformation(
                "JOB D12: DLQ có {So} dòng chưa xử lý nhưng còn MỚI (cũ nhất {TuoiGio:F1} giờ). [D12-DLQ-MOI]",
                thongKe.SoChuaXuLy, tuoiGio);
        }
    }

    /// <summary>
    /// **PHẦN 2 — CỜ `GapSuspected` ĐANG TỒN BAO NHIÊU, VÀ CŨ NHẤT TỪ BAO GIỜ?**
    /// 🚨 **CHỈ ĐỌC + LOG.** 📌 Nó **KHÔNG** hạ cờ — xem chú thích đầu lớp để biết vì sao đó là **quyết định**,
    /// không phải thiếu sót.
    /// </summary>
    private async Task KiemGapSuspectedAsync(IServiceProvider services)
    {
        var intakeRepository = services.GetRequiredService<IRepository<MarketplaceOrderIntake, Guid>>();

        var truyVan = await intakeRepository.GetQueryableAsync();

        // 📌 `GapSuspected` là cột **có index** (`IX_SalMarketplaceOrderIntakes_TenantId_GapSuspected`, `SAL-005` §17).
        var gap = truyVan.Where(x => x.GapSuspected);

        var soGap = await _thucThi.CountAsync(gap);

        if (soGap == 0)
        {
            // 🔑 Log cả ca RỖNG: *"0 đơn nghi ngờ"* phải đọc được là *"đã kiểm"*.
            Logger.LogInformation("JOB D12: KHÔNG có đơn nào bị nghi ngờ thiếu bản trung gian (đã kiểm). [D12-GAP-RONG]");
            return;
        }

        var danhSach = await _thucThi.ToListAsync(
            gap.OrderBy(x => x.ReceivedAtUtc)
                .Take(TranDongInRa)
                .Select(x => new { x.ChannelCode, x.ExternalOrderId, x.ReceivedAtUtc, x.AppliedSourceVersion }));

        var cuNhat = danhSach.Count > 0 ? danhSach[0].ReceivedAtUtc : (DateTime?)null;
        var tuoiGio = cuNhat is { } m ? (DateTime.UtcNow - m).TotalHours : 0;

        // 🔑 **In KHOÁ NGHIỆP VỤ của từng đơn** — 📌 để người vận hành có **danh sách cụ thể** phải đối soát.
        // ⚠️ Nếu chỉ log *"có 37 đơn nghi ngờ"* thì người trực vẫn phải **tự đi tìm** — và với 37 đơn thì
        // việc đó **không ai làm**. 🔒 Đây là khác biệt giữa **cảnh báo** và **việc làm được**.
        var moTa = string.Join("; ", danhSach.Select(x => $"{x.ChannelCode}/{x.ExternalOrderId}(v={x.AppliedSourceVersion?.ToString() ?? "?"})"));

        Logger.LogWarning(
            "🚨 JOB D12: {So} đơn sàn bị NGHI NGỜ THIẾU BẢN TRUNG GIAN (cờ GapSuspected); cũ nhất {CuNhat} "
            + "({TuoiGio:F1} giờ trước). {SoIn}/{So} đơn cũ nhất: {DanhSach}{CatBot}. "
            + "CẦN ĐỐI SOÁT THỦ CÔNG rồi gọi `DanhDauDaDoiSoat` (job này CỐ Ý không tự hạ cờ — hạ cờ là "
            + "KHẲNG ĐỊNH đã kiểm khớp, mà job không gọi được sàn). [D12-GAP-TON-DONG]",
            soGap, cuNhat, tuoiGio, danhSach.Count, soGap, moTa,
            soGap > danhSach.Count ? $" … (đã CẮT BỚT, chỉ in {TranDongInRa} dòng đầu)" : string.Empty);
    }
}
