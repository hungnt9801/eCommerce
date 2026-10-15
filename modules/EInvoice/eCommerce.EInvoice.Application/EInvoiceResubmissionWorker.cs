using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace eCommerce.EInvoice.Application;

/// <summary>
/// **Job gửi lại cơ quan thuế** cho hoá đơn đã bị từ chối (`EINV-002` + mục (4) của GĐ0).
///
/// 🎯 VIỆC NÓ LÀM — và **KHÔNG** làm (ghi rõ để không hiểu nhầm):
///   • **LÀM**: mỗi nhịp quét hoá đơn `BiTuChoi`, áp <see cref="EInvoiceResubmissionPolicy"/>, và nếu
///     đủ điều kiện thì **đưa hoá đơn về `ChuaGui`** để lần gửi kế tiếp thực hiện.
///   • **KHÔNG LÀM**: **không tự gọi HTTP** tới cơ quan thuế. Việc gọi nằm ở `TaxAuthorityClient`
///     (tầng Host) và **hiện chưa gọi được CQT thật** (thiếu endpoint + chứng thư sandbox).
///     Tách như vậy để job **test được** và để việc "đánh dấu đủ điều kiện" không phụ thuộc mạng.
///
/// 🔒 BÀI HỌC ĐÃ ÁP DỤNG (C10): worker này là **worker CỐ ĐỊNH** (không phải dynamic worker bị
/// supervisor thay thế mỗi nhịp) ⇒ không có nguy cơ "không bao giờ chạy" như lỗi C10 đã gặp.
/// </summary>
public class EInvoiceResubmissionWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp quét. 1 phút — đủ nhanh để hoá đơn lỗi tạm thời được gửi lại sớm.</summary>
    public const int NhipMacDinhGiay = 60;

    public EInvoiceResubmissionWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại thuộc tính `Logger`: lớp cơ sở `BackgroundWorkerBase` **ĐÃ có** `Logger`
        // ⇒ khai lại gây CS0108 "hides inherited member" (đã sửa 14/10/2026). Dùng luôn của lớp cơ sở.
        // 🚨 **CHẠY NGAY LẦN ĐẦU** (bài học `CHN-002`) — TRƯỚC vòng 51 worker này **THIẾU** dòng này:
        // chu kỳ đầu **không chạy cho tới khi hết một nhịp** ⇒ sau **mỗi lần deploy**, việc của job
        // **trễ tối đa một nhịp**. Với **hoá đơn điện tử**/giám sát kênh, trễ một nhịp có thể nghĩa là
        // **lỡ hạn pháp lý** hoặc **không phát hiện kênh đứng** — mà **không có lỗi nào** để lần ra.
        Timer.RunOnStart = true;
        Timer.Period = NhipMacDinhGiay * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var hoaDonRepo = workerContext.ServiceProvider.GetRequiredService<IRepository<EInvoice, Guid>>();
        var dongHo = workerContext.ServiceProvider.GetRequiredService<IClock>();

        var biTuChoi = await hoaDonRepo.GetListAsync(x => x.TaxAuthorityStatus == TaxAuthorityStatus.BiTuChoi);

        if (biTuChoi.Count == 0)
        {
            return;
        }

        var now = dongHo.Now;
        var soDaMoLai = 0;

        foreach (var hoaDon in biTuChoi)
        {
            // Toàn bộ phán đoán nằm ở chính sách ĐÃ CÓ TEST (không nhồi luật vào worker).
            var quyetDinh = EInvoiceResubmissionPolicy.DanhGia(
                hoaDon.TaxAuthorityStatus,
                hoaDon.LoiCQT,
                hoaDon.SoLanGuiCQT,
                hoaDon.LanGuiCuoiUtc,
                now);

            if (!quyetDinh.NenGuiLai)
            {
                if (quyetDinh.LyDo == EInvoiceResubmissionPolicy.LyDo.HetLuotThu
                    || quyetDinh.LyDo == EInvoiceResubmissionPolicy.LyDo.LoiVinhVien)
                {
                    // Ghi log ở mức Warning: đây là việc CẦN NGƯỜI XỬ LÝ, không phải lỗi tạm thời.
                    Logger.LogWarning(
                        "Hoá đơn {SoHoaDon} cần người xử lý: {LyDo} — {ThongDiep}",
                        hoaDon.SoHoaDon, quyetDinh.LyDo, quyetDinh.ThongDiep);
                }

                continue;
            }

            hoaDon.ChoPhepGuiLaiCQT();          // về ChuaGui; lần gửi kế tiếp do tầng gọi thực hiện
            await hoaDonRepo.UpdateAsync(hoaDon, autoSave: true);
            soDaMoLai++;

            Logger.LogInformation(
                "Hoá đơn {SoHoaDon} đã được mở lại để gửi CQT: {ThongDiep}",
                hoaDon.SoHoaDon, quyetDinh.ThongDiep);
        }

        if (soDaMoLai > 0)
        {
            Logger.LogInformation("Job gửi lại CQT: đã mở lại {SoLuong} hoá đơn.", soDaMoLai);
        }
    }
}
