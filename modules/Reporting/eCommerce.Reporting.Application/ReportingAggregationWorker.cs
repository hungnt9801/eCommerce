using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Reporting;

/// <summary>
/// **JOB TỔNG HỢP READ-MODEL ĐỊNH KỲ** (`WP-08`, `07 §WP-08`: *"Job tổng hợp theo watermark"*).
///
/// 🔑 **VÌ SAO LÀ JOB (không tính lúc mở báo cáo):** tổng hợp là việc **đọc toàn bộ chứng từ của một ngày**.
/// Tính trong request ⇒ mỗi lần mở Dashboard là một lần quét bảng chứng từ, chậm dần theo dữ liệu, và
/// **không** có chỗ nào trả lời *"số liệu mới tới ngày nào"*.
///
/// 🚨 **BA BẪY ĐÃ TRẢ GIÁ (được `BackgroundWorkerCoCheTests` chặn):**
/// <list type="number">
///   <item>**Đăng ký** (`AddBackgroundWorkerAsync`) — thiếu là job **không bao giờ chạy**, không lỗi nào.</item>
///   <item>**Log VÔ ĐIỀU KIỆN ở câu lệnh ĐẦU TIÊN** — thiếu là *"im lặng"* không phân biệt được *"job không
///     chạy"* với *"chạy mà không có việc"*.</item>
///   <item>**Worker KHÔNG gọi `*AppService`** (có `[Authorize]`): worker không có principal ⇒
///     `AbpAuthorizationException` ⇒ job **chết giữa đường** mà chỉ có log mới thấy. Worker gọi
///     <see cref="TongHopBaoCaoService"/> (dịch vụ miền, có `UnitOfWork` bọc ngoài).</item>
/// </list>
/// 📌 `RunOnStart = true`: sau mỗi lần deploy, số liệu được cập nhật **ngay** thay vì trễ một nhịp.
/// </summary>
public class ReportingAggregationWorker : AsyncPeriodicBackgroundWorkerBase
{
    public const int NhipMacDinhPhut = 15;

    public ReportingAggregationWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ReportingOptions> tuyChon)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        var phut = tuyChon.Value.NhipJobPhut <= 0 ? NhipMacDinhPhut : tuyChon.Value.NhipJobPhut;

        Timer.Period = phut * 60 * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — BẮT BUỘC cho mọi job (`inv-001` §27.3 + §43.4).
        Logger.LogInformation("JOB ReportingAggregationWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: true);

        using (uow)
        {
            try
            {
                var tongHop = scope.ServiceProvider.GetRequiredService<TongHopBaoCaoService>();

                var ketQua = await tongHop.ChayAsync(null, null, ghiAnhTonKho: true);

                await uow.CompleteAsync();

                Logger.LogInformation(
                    "JOB ReportingAggregationWorker: xong — {SoNgay} ngày · {SoDongSuKien} dòng sự kiện · " +
                    "{SoDongTongHop} dòng tổng hợp · {SoAnh} ảnh tồn kho · rỗng={Rong} · mốc {MocBanLe}/{MocDonHang}.",
                    ketQua.SoNgay, ketQua.SoDongSuKien, ketQua.SoDongTongHop, ketQua.SoAnhTonKho,
                    ketQua.Rong, ketQua.MocBanLe, ketQua.MocDonHang);
            }
            catch (BusinessException ex) when (ex.Code == eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
            {
                // 📌 Một lượt khác đang chạy: **bỏ lượt này**, KHÔNG phải lỗi. Ghi `Warning` để vẫn nhìn thấy
                // (nếu lặp liên tục nghĩa là nhịp quá ngắn so với thời gian tổng hợp).
                Logger.LogWarning(
                    "JOB ReportingAggregationWorker: bỏ lượt — một lượt tổng hợp khác đang chạy (khoá {Khoa}).",
                    TongHopBaoCaoService.TenKhoaPhanTan);
            }
            catch (Exception ex)
            {
                // 🚨 KHÔNG nuốt lỗi thành "xong": nếu tổng hợp hỏng, read-model đứng ở ngày cũ và
                // **mọi báo cáo** sẽ nói số liệu của hôm qua như hôm nay — không có dấu hiệu nào trên UI.
                Logger.LogError(ex,
                    "JOB ReportingAggregationWorker: THẤT BẠI — read-model KHÔNG được cập nhật (báo cáo đang " +
                    "dừng ở mốc cũ). Xem log để sửa rồi chạy lại `POST /api/v1/reports/aggregate`.");
            }
        }
    }
}
