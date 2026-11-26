using eCommerce.Inventory;
using eCommerce.Kernel.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Reporting;

/// <summary>
/// **APP SERVICE VẬN HÀNH GĐ3** (`WP-26`, `REP-001` §6) — rebuild · chốt kỳ · đối chiếu.
///
/// 🔐 `[Authorize]` **cấp class** = `eCommerce.Reports`; các method **ghi** gắn thêm quyền hẹp (AND-combination).
/// 🚨 **Idempotency ở mọi biên vào** (`D-274`/`§5.6`): rebuild cùng khoảng+cùng lý do ⇒ trả bản ghi cũ
/// (`AlreadyRequested`); dòng `Reconcile` dùng khoá theo lượt (`…:{runId}`) ⇒ chạy lại cùng khoảng **không**
/// `unique_violation`. Không nhận `tenantId` từ client — server lấy `ICurrentTenant`.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Reports.Default)]
public class ReportAdminAppService : ApplicationService, IReportAdminAppService
{
    private readonly IRepository<RepRunLog, Guid> _lanChay;
    private readonly IRepository<RepPeriodClose, Guid> _kyChot;
    private readonly IRepository<RepConsistencyCheck, Guid> _lech;
    private readonly IRepository<RptAggDailySales, Guid> _agg;
    private readonly IRepository<RptFactSalesLine, Guid> _fact;
    private readonly IRepository<RptFactInventorySnapshot, Guid> _anhTonKho;
    private readonly IRepository<InventoryItem, Guid> _tonKho;
    private readonly TongHopBaoCaoService _tongHop;
    private readonly IAbpDistributedLock _khoa;
    private readonly IClock _dongHo;
    private readonly ICurrentTenant _tenant;
    private readonly IGuidGenerator _guid;
    private readonly IOptions<ReportingOptions> _tuyChon;
    private readonly eCommerceMetrics _metrics;

    public ReportAdminAppService(
        IRepository<RepRunLog, Guid> lanChay,
        IRepository<RepPeriodClose, Guid> kyChot,
        IRepository<RepConsistencyCheck, Guid> lech,
        IRepository<RptAggDailySales, Guid> agg,
        IRepository<RptFactSalesLine, Guid> fact,
        IRepository<RptFactInventorySnapshot, Guid> anhTonKho,
        IRepository<InventoryItem, Guid> tonKho,
        TongHopBaoCaoService tongHop,
        IAbpDistributedLock khoa,
        IClock dongHo,
        ICurrentTenant tenant,
        IGuidGenerator guid,
        IOptions<ReportingOptions> tuyChon,
        eCommerceMetrics metrics)
    {
        _lanChay = lanChay;
        _kyChot = kyChot;
        _lech = lech;
        _agg = agg;
        _fact = fact;
        _anhTonKho = anhTonKho;
        _tonKho = tonKho;
        _tongHop = tongHop;
        _khoa = khoa;
        _dongHo = dongHo;
        _tenant = tenant;
        _guid = guid;
        _tuyChon = tuyChon;
        _metrics = metrics;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // REBUILD
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Reports.Rebuild)]
    public async Task<LanChayDto> StartRebuildAsync(StartRebuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        KiemHopLeKhoang(input.FromDate, input.ToDate);

        if (!Enum.IsDefined(input.Projection))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("projection", input.Projection);
        }

        var lyDo = KiemLyDo(input.Reason, RepPeriodClose.NguongLyDoToiThieu);

        KhoangChot.KiemRebuildTonKho(input.Projection, input.FromDate, input.ToDate, NgayHomNay());

        await ChanNeuGiaoKyDaChotAsync(input.FromDate, input.ToDate);

        var key = RepIdempotency.Rebuild(input.Projection, _tenant.Id, input.FromDate, input.ToDate, lyDo);

        var hienCo = await _lanChay.FirstOrDefaultAsync(x => x.IdempotencyKey == key);

        if (hienCo is not null)
        {
            return ChuyenLanChay(hienCo, alreadyRequested: true);
        }

        var soNgay = input.Projection == RepProjection.TonKho
            ? 1
            : input.ToDate.DayNumber - input.FromDate.DayNumber + 1;

        var run = new RepRunLog(
            _guid.Create(),
            RepRunLog.TenJob.Rebuild,
            input.Projection,
            input.FromDate,
            input.ToDate,
            soNgay,
            lyDo,
            CurrentUser.Id,
            key);

        await _lanChay.InsertAsync(run);

        return ChuyenLanChay(run, alreadyRequested: false);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Reports.Rebuild)]
    public async Task<LanChayDto> ResumeRebuildAsync(Guid runId)
    {
        var run = await LayLanChayAsync(runId);
        run.YeuCauChayLai();
        await _lanChay.UpdateAsync(run);
        return ChuyenLanChay(run, alreadyRequested: false);
    }

    /// <inheritdoc />
    public async Task<LanChayDto> GetLanChayAsync(Guid runId)
        => ChuyenLanChay(await LayLanChayAsync(runId), alreadyRequested: false);

    /// <inheritdoc />
    public async Task<LanChayPageDto> GetLanChaysAsync(GetLanChayInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var q = await _lanChay.GetQueryableAsync();

        if (input.Projection is { } projection)
        {
            q = q.Where(x => x.Projection == projection);
        }

        if (input.Status is { } status)
        {
            q = q.Where(x => x.Status == status);
        }

        var total = await AsyncExecuter.CountAsync(q);

        var items = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.CreationTime)
             .Skip(Math.Max(0, input.SkipCount))
             .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));

        return new LanChayPageDto
        {
            TotalCount = total,
            Items = items.Select(x => ChuyenLanChay(x, alreadyRequested: false)).ToList()
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // CHỐT KỲ
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    [UnitOfWork(isTransactional: true)]
    [Authorize(eCommerceConsts.Permissions.Reports.PeriodClose)]
    public async Task<KyChotDto> ClosePeriodAsync(ClosePeriodInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        KiemHopLeKhoang(input.FromDate, input.ToDate);
        var lyDo = KiemLyDo(input.Reason, RepPeriodClose.NguongLyDoToiThieu);

        var tenant = _tenant.Id;

        await using var giuKhoa = await _khoa.TryAcquireAsync(
            ReportingLockKeys.PeriodClose(tenant), TimeSpan.FromSeconds(2));

        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", ReportingLockKeys.PeriodClose(tenant))
                .WithData("lyDo", "Một lượt chốt kỳ KHÁC đang chạy — chờ rồi thử lại.");
        }

        var key = RepIdempotency.PeriodClose(tenant, input.FromDate, input.ToDate);

        var ky = await _kyChot.FirstOrDefaultAsync(x => x.IdempotencyKey == key);

        if (ky is not null && ky.Status == RepPeriodStatus.DaChot)
        {
            return ChuyenKyChot(ky, alreadyClosed: true);
        }

        // 🚨 Hai kỳ `DaChot` chồng nhau ⇒ 0012 (kiểm TRƯỚC; EXCLUDE ở DB là hàng rào cuối).
        var q = await _kyChot.GetQueryableAsync();
        var kyKhac = ky?.Id ?? Guid.Empty;

        var trung = await AsyncExecuter.CountAsync(q.Where(x =>
            x.Status == RepPeriodStatus.DaChot
            && x.Id != kyKhac
            && x.FromDate <= input.ToDate
            && x.ToDate >= input.FromDate));

        if (trung > 0)
        {
            throw new BusinessException(ReportingErrorCodes.KyChongLan)
                .WithData("tu", input.FromDate)
                .WithData("den", input.ToDate)
                .WithData("lyDo", "Khoảng ngày giao với một kỳ ĐÃ CHỐT khác.");
        }

        if (ky is null)
        {
            ky = new RepPeriodClose(_guid.Create(), input.FromDate, input.ToDate, lyDo, key);
            await _kyChot.InsertAsync(ky);
        }

        // Đối chiếu lại toàn khoảng (KHÔNG giành khoá lần hai, không tạo RepRunLog) — §6.2 bước 4.
        await DoiChieuKhoangAsync(input.FromDate, input.ToDate, null, ghiRunLog: false, requestedBy: null);

        var moQ = await _lech.GetQueryableAsync();

        var conMo = await AsyncExecuter.CountAsync(moQ.Where(x =>
            x.ResolvedAtUtc == null && x.FactDate >= input.FromDate && x.FactDate <= input.ToDate));

        KhoangChot.KiemConLechChuaXuLy(conMo);

        var hash = await TinhHashKyAsync(input.FromDate, input.ToDate);
        var by = CurrentUser.Id;
        var luc = _dongHo.Now.ToUniversalTime();

        ky.BatDauChot(by, lyDo);
        ky.Chot(hash, by, luc);
        await _kyChot.UpdateAsync(ky);

        return ChuyenKyChot(ky, alreadyClosed: false);
    }

    /// <inheritdoc />
    [UnitOfWork(isTransactional: true)]
    [Authorize(eCommerceConsts.Permissions.Reports.PeriodReopen)]
    public async Task<KyChotDto> ReopenPeriodAsync(ReopenPeriodInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ky = await _kyChot.FirstOrDefaultAsync(x => x.Id == input.PeriodId)
                 ?? throw new BusinessException(ReportingErrorCodes.LanChayKhongTonTai)
                     .WithData("periodId", input.PeriodId);

        var tenant = _tenant.Id;

        await using var giuKhoa = await _khoa.TryAcquireAsync(
            ReportingLockKeys.PeriodClose(tenant), TimeSpan.FromSeconds(2));

        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", ReportingLockKeys.PeriodClose(tenant));
        }

        ky.MoLai(CurrentUser.Id, input.Reason, _dongHo.Now.ToUniversalTime());
        await _kyChot.UpdateAsync(ky);

        return ChuyenKyChot(ky, alreadyClosed: false);
    }

    /// <inheritdoc />
    public async Task<KyChotDto> GetKyChotAsync(Guid id)
    {
        var ky = await _kyChot.FirstOrDefaultAsync(x => x.Id == id)
                 ?? throw new BusinessException(ReportingErrorCodes.LanChayKhongTonTai).WithData("periodId", id);

        return ChuyenKyChot(ky, alreadyClosed: false);
    }

    /// <inheritdoc />
    public async Task<KyChotPageDto> GetKyChotsAsync(GetKyChotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var q = await _kyChot.GetQueryableAsync();

        if (input.Status is { } status)
        {
            q = q.Where(x => x.Status == status);
        }

        var total = await AsyncExecuter.CountAsync(q);

        var items = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.FromDate)
             .Skip(Math.Max(0, input.SkipCount))
             .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));

        return new KyChotPageDto
        {
            TotalCount = total,
            Items = items.Select(x => ChuyenKyChot(x, alreadyClosed: false)).ToList()
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ĐỐI CHIẾU
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Reports.Reconcile)]
    public async Task<KetQuaDoiChieuDto> RunReconciliationAsync(RunReconciliationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        KiemHopLeKhoang(input.FromDate, input.ToDate);

        var tenant = _tenant.Id;

        await using var giuKhoa = await _khoa.TryAcquireAsync(
            ReportingLockKeys.Reconciliation(tenant), TimeSpan.FromSeconds(2));

        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", ReportingLockKeys.Reconciliation(tenant));
        }

        return await DoiChieuKhoangAsync(
            input.FromDate, input.ToDate, input.Projection, ghiRunLog: true, requestedBy: CurrentUser.Id);
    }

    /// <inheritdoc />
    public async Task<LechSoPageDto> GetLechSoAsync(GetLechSoInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var q = await _lech.GetQueryableAsync();

        if (input.Projection is { } projection)
        {
            q = q.Where(x => x.Projection == projection);
        }

        if (input.FromDate is { } tu)
        {
            q = q.Where(x => x.FactDate >= tu);
        }

        if (input.ToDate is { } den)
        {
            q = q.Where(x => x.FactDate <= den);
        }

        if (input.DaXuLy is { } daXuLy)
        {
            q = daXuLy
                ? q.Where(x => x.ResolvedAtUtc != null)
                : q.Where(x => x.ResolvedAtUtc == null);
        }

        var total = await AsyncExecuter.CountAsync(q);

        var items = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.FactDate)
             .Skip(Math.Max(0, input.SkipCount))
             .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));

        return new LechSoPageDto
        {
            TotalCount = total,
            Items = items.Select(ChuyenLechSo).ToList()
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ĐỐI CHIẾU — LÕI DÙNG CHUNG
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private async Task<KetQuaDoiChieuDto> DoiChieuKhoangAsync(
        DateOnly from,
        DateOnly to,
        RepProjection? projection,
        bool ghiRunLog,
        Guid? requestedBy)
    {
        var dsProjection = projection.HasValue
            ? new[] { projection.Value }
            : new[] { RepProjection.BanHang, RepProjection.TonKho };

        var luc = _dongHo.Now.ToUniversalTime();
        var soNgayKhoang = to.DayNumber - from.DayNumber + 1;

        var soNgayDaSo = 0;
        var soDongLech = 0;
        var soDongLechMoi = 0;

        foreach (var p in dsProjection)
        {
            RepRunLog? run = null;

            if (ghiRunLog)
            {
                var runId = _guid.Create();

                run = new RepRunLog(
                    runId,
                    RepRunLog.TenJob.Reconcile,
                    p,
                    from,
                    to,
                    soNgayKhoang,
                    RepRunLog.LyDoHeThong.DoiChieu,
                    requestedBy,
                    RepIdempotency.Reconcile(_tenant.Id, p, runId));

                run.BatDau(luc, null);
                await _lanChay.InsertAsync(run);
            }

            for (var ngay = from; ngay <= to; ngay = ngay.AddDays(1))
            {
                var (soLech, soMoi) = await DoiChieuMotNgayAsync(p, ngay, run?.Id);

                soNgayDaSo++;
                soDongLech += soLech;
                soDongLechMoi += soMoi;

                if (run is not null)
                {
                    run.GhiCuaSo(ngay, soLech);
                }
            }

            if (run is not null)
            {
                run.HoanTat(luc);
                await _lanChay.UpdateAsync(run);
            }
        }

        return new KetQuaDoiChieuDto
        {
            SoNgayDaSo = soNgayDaSo,
            SoDongLech = soDongLech,
            SoDongLechMoi = soDongLechMoi
        };
    }

    private async Task<(int SoLech, int SoMoi)> DoiChieuMotNgayAsync(RepProjection projection, DateOnly ngay, Guid? runId)
    {
        var lech = new List<LechSo>();

        if (projection.LaDoanhThu())
        {
            var nguon = await _tongHop.TinhNgayNguonAsync(ngay);

            var agg = await _agg.GetListAsync(x => x.BusinessDate == ngay);

            var factQ = await _fact.GetQueryableAsync();
            var soDongReadModel = await AsyncExecuter.CountAsync(factQ.Where(x => x.BusinessDate == ngay));

            lech.AddRange(SoDoiChieu.KiemNgay(
                projection,
                ngay,
                nguon.TongHop.Sum(x => x.DoanhThu),
                agg.Sum(x => x.Revenue),
                nguon.Dong.Count,
                soDongReadModel,
                nguon.TongHop.Sum(x => x.SoChungTu),
                agg.Sum(x => x.DocumentCount)));
        }
        else if (ngay == NgayHomNay())
        {
            // 📌 Ảnh tồn kho là trạng thái HIỆN TẠI ⇒ chỉ so được hôm nay (D-47 ⑦).
            var anhQ = await _anhTonKho.GetQueryableAsync();
            var soAnhReadModel = await AsyncExecuter.CountAsync(anhQ.Where(x => x.SnapshotDate == ngay));

            var tonQ = await _tonKho.GetQueryableAsync();
            var soAnhNguon = await AsyncExecuter.CountAsync(tonQ);

            lech.AddRange(SoDoiChieu.KiemNgay(
                projection, ngay, 0m, 0m, soAnhNguon, soAnhReadModel, 0, 0));
        }

        if (lech.Count == 0)
        {
            return (0, 0);
        }

        var dangMo = await _lech.GetListAsync(x =>
            x.FactDate == ngay && x.Projection == projection && x.ResolvedAtUtc == null);

        var khoaDangMo = dangMo.Select(x => (x.MismatchKind, x.FactKey)).ToHashSet();

        var luc = _dongHo.Now.ToUniversalTime();
        var soMoi = 0;

        foreach (var l in lech)
        {
            if (!khoaDangMo.Add((l.Kind, l.FactKey)))
            {
                // 📌 Đã có dòng lệch MỞ ⇒ KHÔNG tạo thêm (unique partial chống trùng) — không phải "mới".
                continue;
            }

            soMoi++;

            var dong = RepConsistencyCheck.GhiNhan(
                _guid.Create(), ngay, projection, l.Kind, l.FactKey, l.ExpectedValue, l.ActualValue, luc, runId);

            await _lech.InsertAsync(dong);

            _metrics.ReportingProjectionFailed.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Projection, projection.MaChuoi()),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, "mismatch"));
        }

        return (lech.Count, soMoi);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TIỆN ÍCH
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private async Task<string> TinhHashKyAsync(DateOnly from, DateOnly to)
    {
        var aggQ = await _agg.GetQueryableAsync();
        var dong = await AsyncExecuter.ToListAsync(aggQ.Where(x => x.BusinessDate >= from && x.BusinessDate <= to));

        var dongKy = dong
            .Select(x => new DongKy(x.BusinessDate, x.Nguon, x.DocumentCount, x.LineCount, x.Quantity, x.Revenue))
            .ToList();

        var factQ = await _fact.GetQueryableAsync();
        var soFact = await AsyncExecuter.CountAsync(factQ.Where(x => x.BusinessDate >= from && x.BusinessDate <= to));

        return BamKyChot.Bam(dongKy, soFact);
    }

    private async Task<RepRunLog> LayLanChayAsync(Guid runId)
        => await _lanChay.FirstOrDefaultAsync(x => x.Id == runId)
           ?? throw new BusinessException(ReportingErrorCodes.LanChayKhongTonTai).WithData("runId", runId);

    private async Task ChanNeuGiaoKyDaChotAsync(DateOnly from, DateOnly to)
    {
        var q = await _kyChot.GetQueryableAsync();

        var trung = await AsyncExecuter.CountAsync(q.Where(x =>
            x.Status == RepPeriodStatus.DaChot && x.FromDate <= to && x.ToDate >= from));

        if (trung > 0)
        {
            throw new BusinessException(ReportingErrorCodes.KyDaChot)
                .WithData("tu", from)
                .WithData("den", to)
                .WithData("lyDo", "Khoảng ngày giao với một kỳ ĐÃ CHỐT — mở kỳ trước khi rebuild.");
        }
    }

    private void KiemHopLeKhoang(DateOnly from, DateOnly to) => KhoangChot.Kiem(from, to);

    private static string KiemLyDo(string? lyDo, int toiThieu)
    {
        var sach = lyDo?.Trim() ?? string.Empty;

        if (sach.Length < toiThieu)
        {
            throw new BusinessException(ReportingErrorCodes.ThieuLyDo)
                .WithData("doDai", sach.Length)
                .WithData("toiThieu", toiThieu);
        }

        return sach;
    }

    private DateOnly NgayHomNay() => NgayKinhDoanh.TuUtc(_dongHo.Now, _tuyChon.Value.GioLechUtc);

    private static LanChayDto ChuyenLanChay(RepRunLog x, bool alreadyRequested) => new()
    {
        Id = x.Id,
        JobName = x.JobName,
        Projection = x.Projection,
        Status = x.Status,
        FromDate = x.FromDate,
        ToDate = x.ToDate,
        WindowsTotal = x.WindowsTotal,
        WindowsDone = x.WindowsDone,
        CheckpointDate = x.CheckpointDate,
        RowsWritten = x.RowsWritten,
        Reason = x.Reason,
        RequestedBy = x.RequestedBy,
        StartedAtUtc = x.StartedAtUtc,
        FinishedAtUtc = x.FinishedAtUtc,
        HostName = x.HostName,
        LastErrorCode = x.LastErrorCode,
        AlreadyRequested = alreadyRequested
    };

    private static KyChotDto ChuyenKyChot(RepPeriodClose x, bool alreadyClosed) => new()
    {
        Id = x.Id,
        FromDate = x.FromDate,
        ToDate = x.ToDate,
        Status = x.Status,
        SnapshotHash = x.SnapshotHash,
        ClosedBy = x.ClosedBy,
        ClosedAtUtc = x.ClosedAtUtc,
        CloseReason = x.CloseReason,
        ReopenedBy = x.ReopenedBy,
        ReopenReason = x.ReopenReason,
        ReopenedAtUtc = x.ReopenedAtUtc,
        AlreadyClosed = alreadyClosed
    };

    private static LechSoDto ChuyenLechSo(RepConsistencyCheck x) => new()
    {
        Id = x.Id,
        FactDate = x.FactDate,
        Projection = x.Projection,
        MismatchKind = x.MismatchKind,
        FactKey = x.FactKey,
        ExpectedValue = x.ExpectedValue,
        ActualValue = x.ActualValue,
        Delta = x.Delta,
        AutoCorrected = x.AutoCorrected,
        DetectedAtUtc = x.DetectedAtUtc,
        ResolvedAtUtc = x.ResolvedAtUtc
    };
}
