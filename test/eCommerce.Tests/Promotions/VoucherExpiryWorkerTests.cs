using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Workers;
using eCommerce.Tests.AddressSync.HaTang;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **JOB `voucher-expiry`** (`PRO-001-arch` §6.4/§6.5/§8, `D-306`) — phần **đơn vị hoá được**.
///
/// Chạy `DoWorkAsync` **THẬT** của worker (không host ABP, không PostgreSQL — lớp con phơi `DoWorkAsync`,
/// khuôn `CarrierAddressSyncWorkerTests`/`D-248`). `IRepository` giả **đếm** `GetListAsync`/`GetDbContextAsync`
/// và **mô phỏng** `40P01` bằng exception cùng *tên kiểu* + thuộc tính `SqlState` (đúng cách `LaDeadlock` dò).
///
/// ⚠️ **Để `W25-4` integration**: `UPDATE`/bộ đếm thật trên PostgreSQL (TC-05/TC-18/TC-60), phát metric lúc
/// chạy (được chứng minh ở đây bằng **quét mã nguồn**), và rollback/deadlock khi UoW thật.
/// </summary>
public class VoucherExpiryWorkerTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime T0 = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Exception giả **cùng tên** `PostgresException` + `SqlState` để test `LaDeadlock` không phụ thuộc Npgsql.</summary>
    internal sealed class PostgresException : Exception
    {
        public string SqlState { get; }

        public PostgresException(string sqlState) : base($"fake PostgreSQL error {sqlState}")
            => SqlState = sqlState;
    }

    private sealed class Nom : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class DataFilterGia : IDataFilter
    {
        public int SoLanTat { get; private set; }
        public IDisposable Enable<TFilter>() where TFilter : class => new Nom();
        public IDisposable Disable<TFilter>() where TFilter : class
        {
            SoLanTat++;
            return new Nom();
        }
        public bool IsEnabled<TFilter>() where TFilter : class => true;
    }

    /// <summary>Repository giả: đếm đường đi + `GetDbContextAsync` điều khiển được (ném `40P01`/lỗi khác).</summary>
    internal sealed class RepoWorkerGia<T> : IRepository<T, Guid>, IEfCoreRepository<T>
        where T : class, IEntity<Guid>
    {
        private readonly Dictionary<Guid, T> _duLieu = [];

        public int SoLanGetList { get; private set; }
        public int SoLanGetDb { get; private set; }

        /// <summary>Hành vi của `GetDbContextAsync` — test ném `40P01`/lỗi khác để quan sát vòng thử.</summary>
        public Func<Task<DbContext>> CachGetDb { get; set; } =
            () => Task.FromException<DbContext>(new InvalidOperationException("Chưa cấu hình GetDbContextAsync."));

        public void Them(T entity) => _duLieu[entity.Id] = entity;

        public string? EntityName { get; set; } = typeof(T).Name;
        public string ProviderName => "Test";
        public bool? IsChangeTrackingEnabled => false;
        public IAsyncQueryableExecuter AsyncExecuter { get; }
            = new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>());

        public Task<T?> FindAsync(Guid id, bool includeDetails = true, CancellationToken ct = default)
            => Task.FromResult(_duLieu.TryGetValue(id, out var e) ? e : null);

        public Task<T> GetAsync(Guid id, bool includeDetails = true, CancellationToken ct = default)
            => _duLieu.TryGetValue(id, out var e) ? Task.FromResult(e) : throw new EntityNotFoundException(typeof(T), id);

        public Task<T?> FindAsync(Expression<Func<T, bool>> predicate, bool includeDetails = true, CancellationToken ct = default)
            => Task.FromResult(_duLieu.Values.FirstOrDefault(predicate.Compile()));

        public Task<List<T>> GetListAsync(bool includeDetails = false, CancellationToken ct = default)
        {
            SoLanGetList++;
            return Task.FromResult(_duLieu.Values.ToList());
        }

        public Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate, bool includeDetails = false, CancellationToken ct = default)
        {
            SoLanGetList++;
            return Task.FromResult(_duLieu.Values.Where(predicate.Compile()).ToList());
        }

        public Task<long> GetCountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => Task.FromResult((long)_duLieu.Values.Count(predicate.Compile()));

        public Task<T> InsertAsync(T entity, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(InsertAsync));

        public Task<T> UpdateAsync(T entity, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(UpdateAsync));

        public IQueryable<T> GetQueryable() => _duLieu.Values.AsQueryable();
        public Task<IQueryable<T>> GetQueryableAsync() => Task.FromResult<IQueryable<T>>(_duLieu.Values.AsQueryable());

        public Task<long> GetCountAsync(CancellationToken ct = default) => throw Nem(nameof(GetCountAsync));
        public Task<T> GetAsync(Expression<Func<T, bool>> predicate, bool includeDetails = true, CancellationToken ct = default)
            => throw Nem(nameof(GetAsync));
        public Task InsertManyAsync(IEnumerable<T> entities, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(InsertManyAsync));
        public Task UpdateManyAsync(IEnumerable<T> entities, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(UpdateManyAsync));
        public Task DeleteAsync(T entity, bool autoSave = false, CancellationToken ct = default) => throw Nem(nameof(DeleteAsync));
        public Task DeleteAsync(Guid id, bool autoSave = false, CancellationToken ct = default) => throw Nem(nameof(DeleteAsync));
        public Task DeleteAsync(Expression<Func<T, bool>> predicate, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(DeleteAsync));
        public Task DeleteManyAsync(IEnumerable<T> entities, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(DeleteManyAsync));
        public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave = false, CancellationToken ct = default)
            => throw Nem(nameof(DeleteManyAsync));
        public Task DeleteDirectAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => throw Nem(nameof(DeleteDirectAsync));
        public Task<IQueryable<T>> WithDetailsAsync() => throw Nem(nameof(WithDetailsAsync));
        public Task<IQueryable<T>> WithDetailsAsync(Expression<Func<T, object>> propertySelector) => throw Nem(nameof(WithDetailsAsync));
        public Task<IQueryable<T>> WithDetailsAsync(params Expression<Func<T, object>>[] propertySelectors) => throw Nem(nameof(WithDetailsAsync));
        public IQueryable<T> WithDetails() => throw Nem(nameof(WithDetails));
        public IQueryable<T> WithDetails(params Expression<Func<T, object>>[] propertySelectors) => throw Nem(nameof(WithDetails));
        public Task<List<T>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting, bool includeDetails = false, CancellationToken ct = default)
            => throw Nem(nameof(GetPagedListAsync));

        // ── IEfCoreRepository<T> — điểm điều khiển/deadlock ────────────────────────────────────
        public DbContext DbContext => throw Nem(nameof(DbContext));
        public DbSet<T> DbSet => throw Nem(nameof(DbSet));
        public Task<DbSet<T>> GetDbSetAsync() => throw Nem(nameof(GetDbSetAsync));
        public Task<DbContext> GetDbContextAsync()
        {
            SoLanGetDb++;
            return CachGetDb();
        }

        private static Exception Nem(string thanhVien)
            => new NotSupportedException($"RepoWorkerGia<{typeof(T).Name}>.{thanhVien} không dùng trong luồng này.");
    }

    /// <summary>Lớp con PHƠI `DoWorkAsync` (protected) — khuôn `WorkerDiaChiGia` (`D-248`).</summary>
    private sealed class WorkerGia : VoucherExpiryWorker
    {
        public WorkerGia(AbpAsyncTimer timer, IServiceScopeFactory f, IOptions<PromotionOptions> o)
            : base(timer, f, o) { }

        public Task ChayAsync(PeriodicBackgroundWorkerContext context) => DoWorkAsync(context);
    }

    private sealed class Bo
    {
        public RepoWorkerGia<PromotionRedemption> Redemptions { get; } = new();
        public RepoWorkerGia<Promotion> Promotions { get; } = new();
        public UowManagerGia Uow { get; } = new();
        public KhoaPhanTanGia Khoa { get; } = new();
        public SettingDiaChiGia Settings { get; }
        public TenantDiaChiGia Tenant { get; } = new() { Id = TenantId };
        public DataFilterGia DataFilter { get; } = new();
        public WorkerGia Worker { get; }
        public PeriodicBackgroundWorkerContext Context { get; }
        public eCommerceMetrics Metrics { get; }

        public Bo(bool bat, Func<Task<DbContext>> getDb)
        {
            Settings = new SettingDiaChiGia(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [PromotionContractConsts.EnabledSettingName] = bat ? "true" : "false"
            });
            Redemptions.CachGetDb = getDb;
            Metrics = new eCommerceMetrics(new Meter("promo-expiry-test-" + Guid.NewGuid()));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ISettingProvider>(Settings);
            services.AddSingleton<IRepository<PromotionRedemption, Guid>>(Redemptions);
            services.AddSingleton<IRepository<Promotion, Guid>>(Promotions);
            services.AddSingleton<IDataFilter>(DataFilter);
            services.AddSingleton<ICurrentTenant>(Tenant);
            services.AddSingleton<IAbpDistributedLock>(Khoa);
            services.AddSingleton<IUnitOfWorkManager>(Uow);
            services.AddSingleton<IClock>(new DongHoGia(T0));
            services.AddSingleton(Metrics);

            var sp = services.BuildServiceProvider();

            Worker = new WorkerGia(
                new AbpAsyncTimer(), sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new PromotionOptions()));
            Worker.LazyServiceProvider = new AbpLazyServiceProvider(sp);
            Context = new PeriodicBackgroundWorkerContext(sp);
        }
    }

    private static PromotionRedemption Luot(PromotionRedemptionStatus status, DateTime? expiresAtUtc)
        => PromotionRedemption.TaoMoi(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), null,
            status == PromotionRedemptionStatus.Applied ? Guid.NewGuid() : null,
            status, expiresAtUtc, 1, Guid.NewGuid(),
            Guid.NewGuid().ToString(), 1_000m, "VND", "{}", 1, T0);

    private static Task<DbContext> NemDeadlock()
        => Task.FromException<DbContext>(new PostgresException("40P01"));

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Cờ tắt — D-306 ①(1)/①(5)
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D306_co_tat__0_lan_quet_va_0_lan_ghi()
    {
        var bo = new Bo(bat: false, NemDeadlock);
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reserved, T0.AddMinutes(5)));

        await bo.Worker.ChayAsync(bo.Context);

        Assert.Equal(1, bo.Settings.SoLanDocCua(PromotionContractConsts.EnabledSettingName));
        Assert.Equal(0, bo.Redemptions.SoLanGetList);
        Assert.Equal(0, bo.Redemptions.SoLanGetDb);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Lọc: CHỈ `Reserved` + `ExpiresAtUtc <= now`
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D306_bo_qua_Applied_Released_Reversed_future_va_null_expiry()
    {
        var bo = new Bo(bat: true, () => throw new InvalidOperationException("Không được gọi GetDbContextAsync."));

        // Có `Reserved` để danh sách tenant khác rỗng, nhưng KHÔNG lượt nào đủ điều kiện nhả.
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reserved, T0.AddMinutes(10)));  // còn hạn
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Applied, T0.AddMinutes(-10)));   // đã chốt
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Released, T0.AddMinutes(-10)));   // đã nhả
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reversed, T0.AddMinutes(-10)));   // đã đảo
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reserved, expiresAtUtc: null));  // không hạn

        await bo.Worker.ChayAsync(bo.Context);

        // Lô đọc (Reserved + hết hạn) rỗng ⇒ KHÔNG vào NhaMotLo (0 lần GetDbContextAsync).
        Assert.True(bo.Redemptions.SoLanGetList >= 2); // tenant list + batch read
        Assert.Equal(0, bo.Redemptions.SoLanGetDb);
    }

    [Fact]
    public async Task D306_luot_Reserved_het_han__duoc_chon_va_thu_lai_40P01_dung_3_lan()
    {
        var bo = new Bo(bat: true, NemDeadlock);
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reserved, T0.AddMinutes(-1)));

        await bo.Worker.ChayAsync(bo.Context);

        // 1 ghi + 2 thử lại = 3 lần; lần thứ 4 KHÔNG xảy ra (`SoLanThuDeadlock = 3`).
        Assert.Equal(3, bo.Redemptions.SoLanGetDb);
    }

    [Fact]
    public async Task D306_loi_KHONG_phai_40P01__nem_ngay_khong_thu_lai()
    {
        var bo = new Bo(bat: true,
            () => Task.FromException<DbContext>(new InvalidOperationException("lỗi khác")));
        bo.Redemptions.Them(Luot(PromotionRedemptionStatus.Reserved, T0.AddMinutes(-1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => bo.Worker.ChayAsync(bo.Context));

        Assert.Equal(1, bo.Redemptions.SoLanGetDb);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Hằng số & khoá — D-306
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void D306_hang_so_lo_500_thu_deadlock_3_nhip_5_phut()
    {
        Assert.Equal(500, VoucherExpiryWorker.LoToiDa);
        Assert.Equal(3, VoucherExpiryWorker.SoLanThuDeadlock);
        Assert.Equal(5, VoucherExpiryWorker.NhipMacDinhPhut);
    }

    [Fact]
    public void D306_khoa_job_ten_RIENG__khac_expiry_cu()
    {
        var khoaMoi = PromotionLockKeys.Job("voucher-expiry", TenantId);
        var khoaCu = PromotionLockKeys.Job("expiry", TenantId);

        Assert.Contains("voucher-expiry", khoaMoi, StringComparison.Ordinal);
        Assert.NotEqual(khoaMoi, khoaCu);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  Metric — D-306 ①(2)/(vii)/§8
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void D306_reservation_expired_KHONG_con_trong_ChuaCoNoiPhat__applied_van_con()
    {
        Assert.DoesNotContain(eCommerceMetricNames.PromotionVoucherReservationExpiredTotal,
            eCommerceMetricNames.ChuaCoNoiPhat.Keys);
        Assert.Contains(eCommerceMetricNames.PromotionAppliedTotal,
            eCommerceMetricNames.ChuaCoNoiPhat.Keys);
    }

    // ── Quét mã nguồn (phần phát metric / thứ tự cập nhật chỉ chạy được với DB thật ⇒ W25-4) ─────

    [Fact]
    public void D306_worker_PHAT_2_counter_dung_nhan()
    {
        var ma = DocWorker();

        Assert.Contains("PromotionVoucherReservationExpired.Add(", ma, StringComparison.Ordinal);
        Assert.Contains("PromotionVoucherReleased.Add(", ma, StringComparison.Ordinal);
        Assert.Contains("\"TTL_EXPIRED\"", ma, StringComparison.Ordinal);
        Assert.Contains("eCommerceMetricTags.Kind, \"voucher\"", ma, StringComparison.Ordinal);
        // Phát SAU commit, chỉ khi có dòng thực sự được nhả.
        Assert.Contains("if (daNha > 0)", ma, StringComparison.Ordinal);
    }

    [Fact]
    public void D306_chi_nha_Reserved__SQL_guard_Status_0_va_log_Debug_khi_0_dong()
    {
        var ma = DocWorker();

        Assert.Contains("x.Status == PromotionRedemptionStatus.Reserved", ma, StringComparison.Ordinal);
        Assert.Contains("x.ExpiresAtUtc <= now", ma, StringComparison.Ordinal);
        Assert.Contains("AND \"\"Status\"\" = 0", ma, StringComparison.Ordinal); // guard UPDATE
        Assert.Contains("if (soDong == 0)", ma, StringComparison.Ordinal);
        Assert.Contains("Logger.LogDebug", ma, StringComparison.Ordinal);
        Assert.Contains("continue;", ma, StringComparison.Ordinal);
    }

    [Fact]
    public void D306_thu_tu_cap_nhat__MA_truoc_PROMOTION_sau()
    {
        var ma = DocWorker();
        var dau = ma.IndexOf("private async Task<int> CapNhatLoAsync", StringComparison.Ordinal);
        var cuoi = ma.IndexOf("private static bool LaDeadlock", StringComparison.Ordinal);
        Assert.True(dau >= 0 && cuoi > dau, "Không tìm thấy vùng CapNhatLoAsync.");

        var than = ma[dau..cuoi];
        var viTriMa = than.IndexOf("ProVoucherCodes", StringComparison.Ordinal);
        var viTriPromotion = than.IndexOf("ProPromotions", StringComparison.Ordinal);

        Assert.True(viTriMa >= 0, "CapNhatLoAsync phải cập nhật ProVoucherCodes.");
        Assert.True(viTriPromotion >= 0, "CapNhatLoAsync phải cập nhật ProPromotions.");
        Assert.True(viTriMa < viTriPromotion, "Thứ tự khoá phải là MÃ trước, PROMOTION sau (tránh 40P01).");
        Assert.Contains("\"\"ReservedCount\"\" = \"\"ReservedCount\"\" - 1", than, StringComparison.Ordinal);
    }

    private static string DocWorker()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "eCommerce.slnx")))
        {
            d = d.Parent;
        }

        return File.ReadAllText(Path.Combine(
            d!.FullName, "modules/Promotions/eCommerce.Promotions.Application/Workers/VoucherExpiryWorker.cs"));
    }
}
