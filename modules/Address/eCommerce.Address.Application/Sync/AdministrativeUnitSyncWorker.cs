using eCommerce.Address.Caching;
using eCommerce.Address.Events;
using eCommerce.Address.Sync;
using eCommerce.Kernel.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Address;

/// <summary>
/// **Job đồng bộ danh mục ĐVHC** từ nguồn chính thức (`ADDR-001`).
///
/// 🚨 MẶC ĐỊNH **TẮT** (`Address:Sync:Enabled = false`).
///   Lý do: job này **gọi API bên ngoài** rồi **sửa danh mục địa chỉ đang dùng**. Bật mặc định nghĩa là
///   một dịch vụ ngoài (hoặc mạng) có thể **thay đổi dữ liệu vận hành** mà không ai chủ động quyết định.
///   Bật bằng cấu hình ⇒ người vận hành **chủ động**, và có thể tắt ngay khi nguồn có sự cố.
///   🔒 Production: guard khởi động (`D-78`) chặn nguồn công khai ngoài `{Development, Testing}`.
///
/// 🔒 HÀNG RÀO (đã có test ở `AdministrativeUnitSynchronizer`): rỗng · quá ít · thiếu cấp tỉnh ·
///   **đổi `ParentCode`** (hàng rào 5) · **đóng vượt 20%** · nguồn sai định dạng. Khi hàng rào chặn, job
///   **KHÔNG áp dụng gì**, ghi **dòng lô `BiChan`** + log `Error`.
///
/// 📌 VỊ TRÍ: worker ở tầng app của module Address; toàn bộ luật nằm ở
///   <see cref="ProvincesOpenApiParser"/> + <see cref="AdministrativeUnitSynchronizer"/> (đều thuần, đã test).
/// </summary>
public class AdministrativeUnitSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp đồng bộ. 1 ngày — danh mục ĐVHC thay đổi rất chậm, không cần nhanh hơn.</summary>
    public const int NhipMacDinhGiay = 86_400;

    /// <summary>
    /// Tên khoá phân tán — **trỏ vào hằng của module** (`D-82`). ⚠️ KHÔNG thêm tiền tố `ecommerce:` —
    /// `AbpDistributedLockOptions.KeyPrefix` đã thêm.
    /// </summary>
    public const string TenKhoa = AddressLockKeys.CatalogImport;

    /// <summary>Giá trị `ImportedBy` của đường job.</summary>
    public const string ImportedByJob = "job:AdministrativeUnitSyncWorker";

    // ── Khoá cấu hình ──
    public const string CauHinhBat = "Address:Sync:Enabled";
    public const string CauHinhPhienBan = "Address:Sync:SchemaVersion";

    /// <summary>Nhịp đồng bộ (giây) đọc từ cấu hình — để vận hành chỉnh được mà không phải build lại.</summary>
    public const string CauHinhNhip = "Address:Sync:PeriodSeconds";

    private readonly eCommerceMetrics? _metrics;

    public AdministrativeUnitSyncWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        eCommerceMetrics? metrics = null)
        : base(timer, serviceScopeFactory)
    {
        _metrics = metrics;

        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 LỖI THẬT ĐÃ SỬA (14/10/2026, do kiểm chứng end-to-end bắt được):
        //   Ban đầu tôi KHÔNG đặt `RunOnStart` và định đọc nhịp từ cấu hình **bên trong** `DoWorkAsync`.
        //   Nhưng `DoWorkAsync` chỉ chạy **SAU KHI hết nhịp đầu tiên** (mặc định 1 NGÀY) ⇒ cấu hình
        //   `Address:Sync:PeriodSeconds` **không bao giờ có tác dụng ở lượt đầu**, và tệ hơn: worker
        //   **im lặng không chạy gì** trong suốt ngày đầu sau mỗi lần deploy.
        //
        //   Nay đặt `RunOnStart = true`: lượt đầu chạy NGAY ⇒ đọc cấu hình ngay và áp nhịp ngay.
        //   Điều này AN TOÀN vì job **mặc định TẮT** (`Address:Sync:Enabled = false`): lượt chạy ngay
        //   chỉ đọc cấu hình rồi thoát, **không gọi API ngoài**. Khi người vận hành chủ động bật thì
        //   đồng bộ ngay lúc khởi động là hành vi MONG MUỐN (dữ liệu tươi ngay sau deploy).
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — 🚨 **BẮT BUỘC cho mọi job** (`inv-001` §27.3 + §43.4):
        // nếu chỉ log khi *"có việc"* thì *"im lặng"* **KHÔNG** phân biệt được *"job không chạy"* với
        // *"job chạy mà không có việc"* ⇒ **mọi phép kiểm chứng job mất giá trị**.
        Logger.LogInformation("JOB AdministrativeUnitSyncWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

        var cauHinh = workerContext.ServiceProvider.GetRequiredService<IConfiguration>();

        // Nhịp đọc từ cấu hình (mặc định 1 ngày). Đặt ở đây vì `AbpAsyncTimer.Period` đổi được lúc chạy.
        var nhipGiay = cauHinh.GetValue(CauHinhNhip, NhipMacDinhGiay);

        if (nhipGiay > 0 && Timer.Period != nhipGiay * 1000)
        {
            Timer.Period = nhipGiay * 1000;
        }

        if (!cauHinh.GetValue(CauHinhBat, defaultValue: false))
        {
            Logger.LogDebug(
                "Đồng bộ danh mục ĐVHC đang TẮT ({CauHinh} = false). Bật bằng cấu hình khi muốn đồng bộ.",
                CauHinhBat);
            return;
        }

        var khoa = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await khoa.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            // D-82: job không có client để trả 409 ⇒ bỏ lượt + Warning + metric. **0** dòng lô:
            // không giành được quyền ghi thì chưa có "lượt". Đây là **chủ đích**, không phải mất dấu vết im lặng.
            Logger.LogWarning("Không giành được khoá {TenKhoa}; bỏ qua lượt đồng bộ danh mục ĐVHC.", TenKhoa);
            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "job"), Tag("outcome", "skipped"));
            return;
        }

        await DongBoAsync(workerContext);
    }

    private async Task DongBoAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var sp = workerContext.ServiceProvider;
        var cauHinh = sp.GetRequiredService<IConfiguration>();

        var phienBan = cauHinh.GetValue(CauHinhPhienBan, defaultValue: 2);
        var nguon = sp.GetRequiredService<IKhungDonViHanhChinhSource>();
        var repo = sp.GetRequiredService<IRepository<AdministrativeUnit, Guid>>();
        var loNap = sp.GetRequiredService<IRepository<CatalogSyncRun, Guid>>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var bus = sp.GetRequiredService<IDistributedEventBus>();
        var cache = sp.GetRequiredService<AddressCatalogCache>();
        var dongHo = sp.GetRequiredService<IClock>();

        // 🔒 `startedAtUtc` lấy TRƯỚC adapter (D-78 §5.1): nếu adapter treo/lỗi, dòng lô vẫn có mốc bắt đầu.
        var startedAtUtc = dongHo.Now;

        DanhMucNguon? danhMuc = null;
        string? sourceHash = null;

        try
        {
            danhMuc = await nguon.TaiDanhMucAsync(phienBan);

            KeHoachDongBo keHoach;
            IReadOnlyList<AdministrativeUnit> deThem;
            List<AdministrativeUnit> daSua;
            List<string?> parentCodes;
            CatalogSyncRun lo;

            // ── UoW #1: lô + dòng lô `DaApDung` + ETO **nguyên tử** (một transaction) ──
            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                var hienCo = await repo.GetListAsync(x => x.SchemaVersion == phienBan);

                sourceHash = AddressSourceHash.Tinh(phienBan, danhMuc.Nguon, danhMuc.DonVis);

                // Hàng rào 0b/1..5 ở đây: ném ⇒ `using` bỏ UoW (rollback) ⇒ KHÔNG áp dụng gì.
                keHoach = AdministrativeUnitSynchronizer.LapKeHoach(phienBan, hienCo, danhMuc);
                deThem = AdministrativeUnitSynchronizer.ApDung(keHoach, danhMuc.Nguon);

                if (deThem.Count > 0)
                {
                    await repo.InsertManyAsync(deThem, autoSave: false);
                }

                // Các entity bị đổi tên / mở lại / đóng đã được sửa tại chỗ ⇒ cần cập nhật.
                daSua = keHoach.ThayDois
                    .Where(x => x is { Loai: not LoaiThayDoi.ThemMoi, HienCo: not null })
                    .Select(x => x.HienCo!)
                    .Distinct()
                    .ToList();

                if (daSua.Count > 0)
                {
                    await repo.UpdateManyAsync(daSua, autoSave: false);
                }

                // BR-009: **mọi** lượt nạp — kể cả 0 thay đổi — đều ghi dòng lô (bỏ early-return cũ).
                lo = CatalogSyncRun.Ghi(
                    phienBan,
                    CatalogSyncTrigger.Job,
                    danhMuc.Nguon,
                    sourceHash,
                    startedAtUtc,
                    dongHo.Now,
                    ImportedByJob,
                    CatalogSyncOutcome.DaApDung,
                    errorCode: null,
                    errorType: null,
                    keHoach.SoThemMoi,
                    keHoach.SoDoiTen,
                    keHoach.SoMoLai,
                    keHoach.SoDong,
                    tenantId: null);

                await loNap.InsertAsync(lo, autoSave: false);

                parentCodes = TapParentCode(danhMuc.DonVis, hienCo);

                await bus.PublishAsync(
                    new AddressCacheInvalidatedEto
                    {
                        TenantId = null,
                        SchemaVersion = phienBan,
                        ParentCodes = parentCodes
                    },
                    useOutbox: true);

                await uow.CompleteAsync();
            }

            // ── Sau commit: xoá cache cục bộ + metric ──
            try
            {
                await cache.XoaAsync(phienBan, parentCodes, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Đã nạp danh mục ĐVHC nhưng KHÔNG xoá được cache cục bộ — dữ liệu cũ có thể còn tới hết TTL.");
            }

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "job"), Tag("outcome", "applied"));

            Logger.LogInformation(
                "Đồng bộ danh mục ĐVHC xong (nguồn {Nguon}, phiên bản {PhienBan}, hash {Hash}): " +
                "+{Them} mới, ~{DoiTen} đổi tên, ~{MoLai} mở lại, -{Dong} đóng.",
                danhMuc.Nguon, phienBan, sourceHash,
                keHoach.SoThemMoi, keHoach.SoDoiTen, keHoach.SoMoLai, keHoach.SoDong);
        }
        catch (BusinessException ex) when (AdministrativeUnitSynchronizer.HangRaoChan.Contains(ex.Code))
        {
            // 🚨 HÀNG RÀO ĐÃ CHẶN. KHÔNG áp dụng gì; ghi dòng lô `BiChan` ở UoW riêng rồi log ồn ào.
            await GhiLoThatBaiAsync(
                uowManager, loNap, dongHo, phienBan, CatalogSyncOutcome.BiChan,
                ex.Code, errorType: null, danhMuc?.Nguon, sourceHash, startedAtUtc);

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "job"), Tag("outcome", "blocked"));

            Logger.LogError(
                "🚨 Đồng bộ danh mục ĐVHC BỊ TỪ CHỐI (mã {Ma}). KHÔNG áp dụng thay đổi nào — danh mục giữ nguyên. Lý do: {DuLieu}",
                ex.Code, string.Join("; ", ex.Data.Keys.Cast<object>().Select(k => $"{k}={ex.Data[k]}")));
        }
        catch (Exception ex)
        {
            await GhiLoThatBaiAsync(
                uowManager, loNap, dongHo, phienBan, CatalogSyncOutcome.Loi,
                errorCode: null, ex.GetType().FullName, danhMuc?.Nguon, sourceHash, startedAtUtc);

            _metrics?.AddressCatalogSync.Add(1, Tag("trigger", "job"), Tag("outcome", "error"));

            Logger.LogError(ex, "Đồng bộ danh mục ĐVHC thất bại (lỗi không mong đợi). Không áp dụng thay đổi nào.");
        }
    }

    /// <summary>
    /// Ghi dòng lô `BiChan`/`Loi` trong UoW **riêng** (`requiresNew`) — lượt bị từ chối/lỗi vẫn phải để lại
    /// dấu vết (BR-009). Lỗi khi ghi dòng lô **không che** exception gốc (chỉ log riêng).
    /// </summary>
    private async Task GhiLoThatBaiAsync(
        IUnitOfWorkManager uowManager,
        IRepository<CatalogSyncRun, Guid> loNap,
        IClock dongHo,
        int phienBan,
        CatalogSyncOutcome outcome,
        string? errorCode,
        string? errorType,
        string? sourceName,
        string? sourceHash,
        DateTime startedAtUtc)
    {
        try
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

            var lo = CatalogSyncRun.Ghi(
                phienBan,
                CatalogSyncTrigger.Job,
                sourceName,
                sourceHash,
                startedAtUtc,
                dongHo.Now,
                ImportedByJob,
                outcome,
                errorCode,
                errorType,
                soThemMoi: 0,
                soDoiTen: 0,
                soMoLai: 0,
                soDong: 0,
                tenantId: null);

            await loNap.InsertAsync(lo, autoSave: true, cancellationToken: CancellationToken.None);
            await uow.CompleteAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "KHÔNG ghi được dòng lô nạp danh mục ĐVHC cho lượt {Outcome} — mất dấu vết lượt nạp (BR-009).",
                outcome);
        }
    }

    /// <summary>Hợp `parentCode` của danh mục mới và danh mục hiện có (phần tử `null` = danh sách tỉnh).</summary>
    private static List<string?> TapParentCode(
        IEnumerable<DonViNguon> moi,
        IEnumerable<AdministrativeUnit> hienCo)
    {
        var tap = new List<string?> { null };
        var daCo = new HashSet<string?>(StringComparer.Ordinal) { null };

        void Them(string? parentCode)
        {
            if (string.IsNullOrWhiteSpace(parentCode))
            {
                return;
            }

            var chuan = parentCode.Trim();

            if (daCo.Add(chuan))
            {
                tap.Add(chuan);
            }
        }

        foreach (var dv in moi)
        {
            Them(dv.ParentCode);
        }

        foreach (var dv in hienCo)
        {
            Them(dv.ParentCode);
        }

        return tap;
    }

    private static KeyValuePair<string, object?> Tag(string ten, object? giaTri)
        => new(ten, giaTri);
}
