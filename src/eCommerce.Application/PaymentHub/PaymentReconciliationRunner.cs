using eCommerce.PaymentHub.Events;
using eCommerce.PaymentHub.Payments;
using eCommerce.Reconciliation;
using eCommerce.Reconciliation.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace eCommerce.Application.PaymentHub;

/// <summary>
/// 🆕 **`S15` — BỘ CHẠY ĐỐI SOÁT THANH TOÁN: MỘT hiện thực, HAI đường vào** (`REC-001` §6.1 `TriggerRunAsync`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **LỚP NÀY ĐƯỢC TÁCH RA TỪ `PaymentReconciliationWorker` (28/10/2026) VÌ LÝ DO CỤ THỂ:**
/// trước đó vòng quét + ghi lịch sử nằm **trong** worker ⇒ API *"chạy tay"* (`TriggerRunAsync`) **không thể**
/// dùng lại mà **không** chép 250 dòng luật sang app service. Hai bản luật tiền = lệch âm thầm
/// (`XC-003` F2), và hai khoá phân tán khác tên = hai đường **chạy chồng**.
/// ✅ Nay: **khoá + vòng quét + ghi lịch sử** ở đây; worker chỉ **gọi theo nhịp**, app service chỉ **gọi khi
/// người dùng bấm** ⇒ cùng một khoá `payment:reconciliation-rec001`, cùng một luật.
///
/// 🚨 **BỐN BẤT BIẾN ĐƯỢC GIỮ NGUYÊN TỪ `S5` (đừng sửa khi thêm đường vào thứ hai):**
///   1. **MỖI giao dịch MỘT UoW** (`F-PAY-7`): lỗi ở một giao dịch **không** rollback bằng chứng của 49
///      giao dịch khác (quota cổng đã tiêu — mất bằng chứng là mất vĩnh viễn nếu cổng chết).
///   2. **Đọc giá trị `bool` trả về** của `MarkSucceeded`/`MarkFailed` (`F-PAY-9`): `false` = **không đổi
///      trạng thái** ⇒ **không** đếm, **không** phát sự kiện.
///   3. **Ghi lịch sử SAU khi đã ghi tiền**, trong UoW riêng: lỗi ghi lịch sử **không** được rollback tiền.
///   4. **Cấm** gọi app service có `[Authorize]` từ worker (bài học `SAL-005`: job chết giữa đường mà log
///      chỉ có neo mở đầu) — lớp này **không** mang `[Authorize]` và **không** gọi app service.
///
/// 📌 **`S15` bổ sung:** phân loại `StalePendingTransaction` (mức Thấp) cho giao dịch `Initiated/Pending`
/// quá `Reconciliation:StalePendingHours` **và** cổng xác nhận **không có** giao dịch (`vnp_ResponseCode = 91`).
/// Luật nằm ở `LuatStalePending` (thuần, có test) — **không** viết lại ở đây.
/// </summary>
public class PaymentReconciliationRunner : IPaymentReconciliationRunner, ITransientDependency
{
    /// <summary>
    /// Tên khoá phân tán (**không** tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm).
    /// 🔒 **Một** tên cho **cả hai** đường vào — đổi tên ở đây là đổi cho cả job lẫn chạy tay.
    /// </summary>
    public const string TenKhoa = "payment:reconciliation-rec001";

    /// <summary>
    /// Trần số giao dịch **một lượt**. 📌 Mỗi giao dịch là **một lời gọi HTTP ra ngoài** ⇒ trần để một tồn
    /// đọng lớn không làm lượt chạy hàng giờ và không tiêu hết quota cổng.
    /// </summary>
    public const int TranMotLuot = 50;

    /// <summary>Khoá giữ tối đa **120 s** — đủ cho một lượt 50 giao dịch; hết hạn thì lượt sau tiếp tục.</summary>
    private static readonly TimeSpan ThoiGianGiuKhoa = TimeSpan.FromSeconds(120);

    private readonly IRepository<PaymentTransaction, Guid> _giaoDich;
    private readonly IPaymentProvider _cong;
    private readonly IPaymentEventPublisher _suKien;
    private readonly IRepository<PaymentReconciliationRun, Guid> _runs;
    private readonly IRepository<PaymentReconciliationLine, Guid> _lines;
    private readonly IRepository<PaymentDiscrepancy, Guid> _discrepancies;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IConfiguration _cauHinh;
    private readonly ILogger<PaymentReconciliationRunner> _logger;
    private readonly eCommerce.Kernel.Observability.eCommerceMetrics? _metrics;

    public PaymentReconciliationRunner(
        IRepository<PaymentTransaction, Guid> giaoDich,
        IPaymentProvider cong,
        IPaymentEventPublisher suKien,
        IRepository<PaymentReconciliationRun, Guid> runs,
        IRepository<PaymentReconciliationLine, Guid> lines,
        IRepository<PaymentDiscrepancy, Guid> discrepancies,
        IUnitOfWorkManager uowManager,
        IAbpDistributedLock distributedLock,
        IConfiguration cauHinh,
        ILogger<PaymentReconciliationRunner> logger,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
    {
        _giaoDich = giaoDich;
        _cong = cong;
        _suKien = suKien;
        _runs = runs;
        _lines = lines;
        _discrepancies = discrepancies;
        _uowManager = uowManager;
        _distributedLock = distributedLock;
        _cauHinh = cauHinh;
        _logger = logger;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public async Task<PaymentReconciliationRunDto?> ChayAsync(
        PaymentReconciliationTrigger trigger,
        Guid? nguoiDungId = null,
        CancellationToken cancellationToken = default)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(TenKhoa, ThoiGianGiuKhoa);

        if (handle is null)
        {
            // 🔒 **KHÔNG** tự quyết định cách báo: job bỏ nhịp (bình thường), chạy tay ⇒ `Lock:0001`.
            return null;
        }

        var nguongGio = LuatStalePending.DocNguong(_cauHinh["Reconciliation:StalePendingHours"]);

        var trangThaiCoTheDoiSoat = new[]
        {
            PaymentTransactionStatus.Initiated,
            PaymentTransactionStatus.Pending,
            PaymentTransactionStatus.Failed
        };

        var danhSach = await _giaoDich.GetListAsync(x => trangThaiCoTheDoiSoat.Contains(x.Status));

        // 📌 **CŨ NHẤT TRƯỚC** rồi mới cắt trần: giao dịch kẹt **lâu nhất** là giao dịch **đáng lo nhất**
        // (lấy mới nhất trước thì một tồn đọng lớn khiến giao dịch cũ **không bao giờ** được xét — đói, im lặng).
        var canXet = danhSach
            .OrderBy(x => x.CreationTime)
            .Take(TranMotLuot)
            .ToList();

        var ketQua = new List<KetQuaMotGiaoDich>();
        var soDoi = 0;
        var soThuDuoc = 0;
        var soKhongXacDinh = 0;
        var soLoi = 0;

        foreach (var gd in canXet)
        {
            var tuCong = await _cong.QueryStatusDetailAsync(gd.MerchantReference, cancellationToken);

            var quyetDinh = LuatDoiSoatThanhToan.Quyet(gd.Status, tuCong.Status);

            MetricDoiSoatThanhToan.Ghi(_metrics, quyetDinh);

            try
            {
                var daGhiNhan = false;
                using var uowTungGiaoDich = _uowManager.Begin(requiresNew: true, isTransactional: true);

                switch (quyetDinh)
                {
                    case LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien:
                        var daGhiThu = gd.MarkSucceeded(null, null, "{\"nguon\":\"querydr-job\"}", null);
                        await _giaoDich.UpdateAsync(gd, autoSave: true);
                        daGhiNhan = daGhiThu;

                        if (daGhiThu)
                        {
                            soDoi++;
                            soThuDuoc++;
                            await _suKien.PhatDaThuTienAsync(gd);
                        }

                        break;

                    case LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai:
                        var daGhiHong = gd.MarkFailed(tuCong.Status == PaymentTransactionStatus.Cancelled
                            ? "Cổng xác nhận khách ĐÃ HUỶ giao dịch (nguồn: job đối soát)."
                            : "Cổng xác nhận giao dịch không thành công (nguồn: job đối soát).");
                        await _giaoDich.UpdateAsync(gd, autoSave: true);
                        daGhiNhan = daGhiHong;

                        if (daGhiHong)
                        {
                            soDoi++;
                            await _suKien.PhatThatBaiAsync(gd, tuCong.Status.ToString());
                        }

                        break;

                    default:
                        // 🔒 `KhongDoi` gồm **HAI** ca rất khác nhau: (a) đã ở trạng thái cuối — bình thường;
                        // (b) cổng nói "chưa biết" — **không kiểm được** (KHÔNG phải "đã kiểm và ổn").
                        if (!LuatDoiSoatThanhToan.DaKetThuc(gd.Status))
                        {
                            soKhongXacDinh++;
                        }

                        break;
                }

                await uowTungGiaoDich.CompleteAsync();

                ketQua.Add(new KetQuaMotGiaoDich(gd, daGhiNhan, Loi: false, LoiMsg: null, tuCong));
            }
            catch (Exception ex)
            {
                // ⚠️ Bắt **mọi** exception là CÓ CHỦ Ý (`F-PAY-7`) — nhưng **không nuốt**: đếm + log để người
                // vận hành thấy. `DaGhi: false` vì UoW đã rollback.
                soLoi++;
                ketQua.Add(new KetQuaMotGiaoDich(gd, DaGhi: false, Loi: true, LoiMsg: ex.Message, Cong: null));

                LoggerError(ex, gd);
            }
        }

        if (danhSach.Count > TranMotLuot)
        {
            // 🚨 NÓI RÕ LÀ ĐÃ CẮT BỚT — im lặng cắt bớt khiến người đọc tưởng **đã** xét hết.
            LogWarningCatBot(danhSach.Count, canXet.Count);
        }

        // 🚨 **KHÔNG GHI LỊCH SỬ KHI JOB KHÔNG CÓ GÌ ĐỂ LÀM.** 📌 Đây là bất biến chống **phình bảng**:
        // job chạy mỗi 15 phút ⇒ nếu lượt rỗng cũng ghi `Run`, bảng có **96 dòng/ngày** toàn số 0, và
        // *"lượt gần nhất có 0 lệch"* trở thành câu trả lời vô nghĩa (người vận hành không phân biệt được
        // *"job chạy, không có gì"* với *"job chết từ hôm qua"* — 📌 đúng thứ `[PAY-JOB-RONG]` trong log để làm).
        // ✅ **Chạy TAY thì VẪN ghi lượt rỗng**: người bấm cần **bằng chứng** là lượt của mình đã chạy (và ai chạy).
        // 📌 Trước `S15`, worker không ghi gì khi rỗng — giữ nguyên hành vi đó cho đường **job**.
        if (ketQua.Count == 0 && trigger == PaymentReconciliationTrigger.Schedule)
        {
            LogEmpty();
            return new PaymentReconciliationRunDto
            {
                Status = PaymentReconciliationRunStatus.HoanTat,
                TriggeredBy = trigger,
                TriggeredByUserId = nguoiDungId,
                PeriodFromUtc = canXet.Count > 0 ? canXet.Min(x => x.CreationTime) : DateTime.UtcNow,
                PeriodToUtc = DateTime.UtcNow,
                StartedAtUtc = DateTime.UtcNow
            };
        }

        // 🆕 `REC-001` 2b — ghi lịch sử lượt/dòng/lệch. 🔒 Lỗi ghi lịch sử KHÔNG được làm job tiền chết.
        PaymentReconciliationRun? run = null;

        try
        {
            run = await GhiLichSuAsync(canXet, ketQua, trigger, nguoiDungId, nguongGio, cancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorLichSu(ex);
        }

        _metrics?.SetPaymentReconUnresolved(soKhongXacDinh);

        LogKetQua(canXet.Count, soDoi, soThuDuoc, soKhongXacDinh, soLoi);

        if (soLoi > 0)
        {
            LogErrorCoLoi(soLoi, canXet.Count);
        }

        return run is null ? null : ToDto(run);
    }

    /// <summary>
    /// Ghi **một** `Run` + mỗi giao dịch một `Line`; khi phát hiện lệch thì sinh `Discrepancy`.
    /// 🔒 Chạy **sau** khi đã ghi tiền, trong UoW riêng (bất biến #3 ở chú thích lớp).
    /// </summary>
    private async Task<PaymentReconciliationRun?> GhiLichSuAsync(
        List<PaymentTransaction> canXet,
        List<KetQuaMotGiaoDich> ketQua,
        PaymentReconciliationTrigger trigger,
        Guid? nguoiDungId,
        int nguongGio,
        CancellationToken cancellationToken)
    {
        // 📌 Không có giao dịch nào ⇒ vẫn ghi một lượt **rỗng**: *"đã chạy, không có gì để làm"* khác
        // *"không chạy"* — người vận hành cần phân biệt (đặc biệt khi chạy tay).
        var tuUtc = canXet.Count > 0 ? canXet.Min(x => x.CreationTime) : DateTime.UtcNow.AddMinutes(-1);
        var denUtc = DateTime.UtcNow;
        var run = new PaymentReconciliationRun(Guid.NewGuid(), tuUtc, denUtc, trigger, nguoiDungId);

        using var uow = _uowManager.Begin(requiresNew: true, isTransactional: true);

        await _runs.InsertAsync(run, autoSave: false);

        var soKhop = 0;
        var soLech = 0;

        foreach (var k in ketQua)
        {
            var gd = k.Gd;

            var line = new PaymentReconciliationLine(
                Guid.NewGuid(),
                run.Id,
                gd.Id,
                gd.ProviderCode,
                gd.MerchantReference,
                ("RC" + gd.Id.ToString("N"))[..32],
                (int)gd.Status,
                (long)(gd.Amount * 100m));

            if (k.Loi)
            {
                line.LoiNhaCungCap("ProviderUnavailable", k.LoiMsg);
            }
            else
            {
                var localMinor = (long)(gd.Amount * 100m);
                var dsLech = new List<PaymentDiscrepancy>();

                void Them(PaymentDiscrepancyKind kind, PaymentDiscrepancySeverity severity, long? providerMinor = null)
                    => dsLech.Add(new PaymentDiscrepancy(
                        Guid.NewGuid(), run.Id, line.Id, gd.Id, gd.OrderId, gd.ProviderCode, gd.MerchantReference,
                        kind, severity, localAmountMinorUnits: localMinor,
                        providerAmountMinorUnits: providerMinor, currency: gd.Currency));

                if (k.Cong is { } cong)
                {
                    line.GhiPhanHoiCong(
                        cong.ResponseCode, cong.TransactionStatus, cong.AmountMinorUnits, cong.TransactionNo,
                        cong.PayDateUtc, cong.PayDateGmt7, cong.TransactionType);

                    if (cong.ResponseCode is null && cong.Status == PaymentTransactionStatus.Pending)
                    {
                        Them(PaymentDiscrepancyKind.ProviderUnreachable, PaymentDiscrepancySeverity.TrungBinh);
                    }

                    if (cong.ResponseCode == "91")
                    {
                        Them(PaymentDiscrepancyKind.ProviderTransactionNotFound, PaymentDiscrepancySeverity.TrungBinh);
                    }

                    if (cong.AmountMinorUnits is { } providerMinor && providerMinor != localMinor)
                    {
                        Them(PaymentDiscrepancyKind.AmountMismatch, PaymentDiscrepancySeverity.Cao, providerMinor);
                    }

                    if (cong.TransactionStatus is "04" or "07")
                    {
                        Them(PaymentDiscrepancyKind.ProviderFlagged, PaymentDiscrepancySeverity.Cao);
                    }

                    if (cong.TransactionNo is { } soCong && gd.ProviderTransactionId is { } soDaLuu && soCong != soDaLuu)
                    {
                        Them(PaymentDiscrepancyKind.ProviderTxnIdMismatch, PaymentDiscrepancySeverity.TrungBinh);
                    }

                    if (cong.TransactionType is "02" or "03"
                        && cong.TransactionStatus is "05" or "06" or "09"
                        && gd.Status is not (PaymentTransactionStatus.Refunded or PaymentTransactionStatus.PartiallyRefunded))
                    {
                        Them(PaymentDiscrepancyKind.RefundNotRecorded, PaymentDiscrepancySeverity.TrungBinh);
                    }

                    if (gd.Status == PaymentTransactionStatus.Succeeded
                        && cong.TransactionStatus is not null and not "00")
                    {
                        Them(PaymentDiscrepancyKind.RecordedButMoneyNotReceived, PaymentDiscrepancySeverity.Cao);
                    }

                    if (string.IsNullOrWhiteSpace(gd.ProviderCreateDateGmt7))
                    {
                        Them(PaymentDiscrepancyKind.MissingProviderCreateDate, PaymentDiscrepancySeverity.TrungBinh);
                    }

                    // 🆕 `S15` — **GIAO DỊCH TREO QUÁ HẠN** (`REC-001` §5.4). Luật ở `LuatStalePending`:
                    // `Initiated/Pending` + tuổi ≥ ngưỡng + cổng xác nhận KHÔNG có giao dịch (`91`).
                    // 📌 Thêm **SAU** `ProviderTransactionNotFound` là **có chủ ý**: hai cờ này trả lời hai câu
                    // khác nhau (*"cổng không có giao dịch"* vs *"và nó đã treo quá lâu"*) ⇒ người vận hành
                    // thấy cả hai dòng nếu cả hai đúng. Gộp chúng lại sẽ **mất** mức độ (Trung binh vs Thap).
                    if (LuatStalePending.LaStale(
                            LuatDoiSoatThanhToan.DaKetThuc(gd.Status),
                            gd.CreationTime,
                            DateTime.UtcNow,
                            cong.ResponseCode,
                            nguongGio))
                    {
                        Them(PaymentDiscrepancyKind.StalePendingTransaction, PaymentDiscrepancySeverity.Thap);
                    }
                }

                if (k.DaGhi)
                {
                    // 🎯 Đúng ca DoD: cổng nói ĐÃ THU TIỀN mà sổ chưa ghi ⇒ bằng chứng phải nằm lại, không chỉ log.
                    Them(PaymentDiscrepancyKind.MoneyReceivedButNotRecorded, PaymentDiscrepancySeverity.Cao);
                }

                if (dsLech.Count == 0)
                {
                    line.Khop();
                    soKhop++;
                }
                else
                {
                    foreach (var lech in dsLech)
                    {
                        await _discrepancies.InsertAsync(lech, autoSave: false);
                    }

                    line.Lech(dsLech[0].Id);
                    soLech++;
                }
            }

            await _lines.InsertAsync(line, autoSave: false);
        }

        run.KetThuc(ketQua.Count, soKhop, lineSkipped: 0, discrepancyCount: soLech);
        await _runs.UpdateAsync(run, autoSave: true);

        await uow.CompleteAsync();

        return run;
    }

    private static PaymentReconciliationRunDto ToDto(PaymentReconciliationRun x) => new()
    {
        Id = x.Id,
        Status = x.Status,
        TriggeredBy = x.TriggeredBy,
        TriggeredByUserId = x.TriggeredByUserId,
        PeriodFromUtc = x.PeriodFromUtc,
        PeriodToUtc = x.PeriodToUtc,
        StartedAtUtc = x.StartedAtUtc,
        FinishedAtUtc = x.FinishedAtUtc,
        LineTotal = x.LineTotal,
        LineMatched = x.LineMatched,
        LineSkipped = x.LineSkipped,
        DiscrepancyCount = x.DiscrepancyCount,
        FailureCode = x.FailureCode,
        CorrelationId = x.CorrelationId
    };

    // ── LOG ────────────────────────────────────────────────────────────────────────────────────────
    // 📌 Log tách thành hàm nhỏ để **worker** và **chạy tay** dùng cùng câu chữ; và để **neo ASCII**
    // (`[PAY-JOB-*]`) nằm ở **một chỗ** — neo là thứ con người grep khi sự cố (bài học `I4`).
    // ⚠️ Lớp này **không** kế thừa `ApplicationService` nên tự lấy logger qua `ILogger`? — xem chú thích dưới.

    private void LogWarningCatBot(int tong, int xet)
        => _logger.LogWarning(
            "JOB REC-001: có {Tong} giao dịch cần đối soát nhưng trần một lượt là {Tran} ⇒ lượt này xét {Xet} " +
            "(cũ nhất trước), phần còn lại ở lượt sau. [PAY-JOB-CAT-BOT]", tong, TranMotLuot, xet);

    private void LogEmpty()
        => _logger.LogInformation("JOB REC-001: không có giao dịch nào cần đối soát. [PAY-JOB-RONG]");

    private void LogErrorLichSu(Exception ex)
        => _logger.LogError(ex,
            "JOB REC-001: KHÔNG ghi được lịch sử đối soát (lượt/dòng/lệch) — kết quả tiền vẫn đã ghi. [PAY-JOB-LICHSU-LOI]");

    private void LogKetQua(int xet, int doi, int thu, int khongXacDinh, int loi)
        => _logger.LogInformation(
            "JOB REC-001: xét {Xet} · đổi trạng thái {Doi} · thu được {Thu} · không xác định {KXD} · lỗi {Loi}. " +
            "[PAY-JOB-KET-QUA]", xet, doi, thu, khongXacDinh, loi);

    private void LogErrorCoLoi(int loi, int xet)
        => _logger.LogError(
            "JOB REC-001: {Loi}/{Xet} giao dịch XỬ LÝ LỖI ⇒ các giao dịch đó KHÔNG được đối soát lượt này " +
            "(các giao dịch khác ĐÃ được ghi). Xem dòng `[PAY-JOB-LOI]` để biết từng mã. [PAY-JOB-CO-LOI]", loi, xet);

    private void LoggerError(Exception ex, PaymentTransaction gd)
        => _logger.LogError(ex,
            "JOB REC-001: LỖI khi đối soát giao dịch {MerchantReference} ({Id}) ⇒ bỏ qua giao dịch này, " +
            "các giao dịch khác trong lượt VẪN được ghi. [PAY-JOB-LOI]", gd.MerchantReference, gd.Id);

    private readonly record struct KetQuaMotGiaoDich(
        PaymentTransaction Gd, bool DaGhi, bool Loi, string? LoiMsg, PaymentStatusQueryResult? Cong);
}
