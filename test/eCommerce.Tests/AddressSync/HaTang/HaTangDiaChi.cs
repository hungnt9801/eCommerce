using System.Linq.Expressions;
using eCommerce.Address;
using eCommerce.Address.Caching;
using eCommerce.Address.Carrier;
using eCommerce.Address.Events;
using eCommerce.Address.Sync;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace eCommerce.Tests.AddressSync.HaTang;

/// <summary>
/// **HẠ TẦNG GIẢ IN-MEMORY** cho test tầng APP SERVICE của `ADDR-001` (`W18-4` / `N80`).
///
/// 🎯 Khuôn `D-229` (`test/eCommerce.Tests/Pricing/HaTang`): dựng `AdministrativeUnitAppService` bằng `new`,
/// gán tay `LazyServiceProvider`, **KHÔNG** host ABP, **KHÔNG** PostgreSQL/Redis. Mọi thành viên KHÔNG đi qua
/// đường test **NÉM** để lỗi thiết kế nổi ngay thay vì lặng lẽ đi qua.
///
/// 🔒 Điểm khác `D-229`: repo ở đây hiện thực **thêm** `InsertManyAsync`/`UpdateManyAsync` và UoW ghi lại
/// `requiresNew`/`isTransactional` — đây chính là thứ NC-1 (arch §6.1) đột biến và test phải bắt được:
/// *"dòng lô từ chối ghi ở UoW RIÊNG (`requiresNew: true`) rồi mới ném"* (`D-78`, BR-009).
/// </summary>
internal static class HaTangDiaChi
{
    /// <summary>Tạo bộ app service `ADDR-001` với toàn bộ phụ thuộc giả + trả về bộ đo để khẳng định.</summary>
    public static (AdministrativeUnitAppService Sut, BoDoDiaChi Bo) Dung(
        Dictionary<string, string?>? settings = null,
        Dictionary<string, string?>? cauHinh = null,
        bool luonBanKhoa = false,
        DateTime? now = null)
    {
        var bo = new BoDoDiaChi(settings, cauHinh, luonBanKhoa, now);

        var sut = new AdministrativeUnitAppService(
            bo.DonVi,
            bo.LoNap,
            bo.Uow,
            bo.Khoa,
            bo.DongHo,
            bo.Cache,
            bo.Bus,
            NullLogger<AdministrativeUnitAppService>.Instance,
            metrics: null);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGuidGenerator>(new GuidGia());
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton<ICurrentUser>(bo.NguoiDung);
        services.AddSingleton<ICurrentTenant>(bo.Tenant);

        sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());

        return (sut, bo);
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}

/// <summary>Bộ phụ thuộc giả + số đo của một lượt dựng app service `ADDR-001`.</summary>
internal sealed class BoDoDiaChi
{
    public RepoDiaChiGia<AdministrativeUnit> DonVi { get; } = new();
    public RepoDiaChiGia<CatalogSyncRun> LoNap { get; } = new();
    public UowManagerGia Uow { get; }
    public KhoaPhanTanGia Khoa { get; }
    public CachePhanTanGia CachePhanTan { get; }
    public SettingDiaChiGia Settings { get; }
    public CauHinhGia CauHinh { get; }
    public LoggerGia<AddressCatalogCache> LogCache { get; } = new();
    public EventBusGia Bus { get; } = new();
    public DongHoDiaChiGia DongHo { get; }
    public NguoiDungDiaChiGia NguoiDung { get; } = new();
    public TenantDiaChiGia Tenant { get; } = new();

    public AddressCatalogCache Cache { get; }

    public BoDoDiaChi(
        Dictionary<string, string?>? settings,
        Dictionary<string, string?>? cauHinh,
        bool luonBanKhoa,
        DateTime? now)
    {
        Uow = new UowManagerGia();
        Khoa = new KhoaPhanTanGia { LuonBan = luonBanKhoa };
        CachePhanTan = new CachePhanTanGia();
        Settings = new SettingDiaChiGia(settings);
        CauHinh = new CauHinhGia(cauHinh);
        DongHo = new DongHoDiaChiGia(now ?? new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc));

        Cache = new AddressCatalogCache(CachePhanTan, Settings, CauHinh, LogCache, metrics: null);
    }
}

/// <summary>
/// Repository giả in-memory (khuôn `RepoGia` của `D-229`) nhưng mở **thêm** `InsertManyAsync`/`UpdateManyAsync`
/// — đường mà `ImportDatasetAsync` dùng (`autoSave: false`). Đếm số lần chèn/sửa để khẳng định "không ghi nửa".
/// </summary>
internal sealed class RepoDiaChiGia<T> : IRepository<T, Guid> where T : class, IEntity<Guid>
{
    private readonly Dictionary<Guid, T> _duLieu = [];

    public int SoLanChen { get; private set; }
    public int SoLanSua { get; private set; }

    /// <summary>
    /// Số lần ghi **đã thực sự áp** vào kho (sau `CompleteAsync` của UoW, hoặc ngay khi không có UoW).
    /// 🔑 Đây là số đo của **TC-07/NC-2**: rollback đúng ⇒ lượt `UpdateManyAsync` bị bỏ ⇒ **0**.
    /// </summary>
    public int SoLanApDung { get; private set; }

    /// <summary>
    /// `IUnitOfWorkManager` của lượt chạy — khi đặt, mọi ghi đi vào **vùng chờ** của UoW hiện hành và
    /// chỉ áp khi `CompleteAsync`; `Dispose` không `Complete` ⇒ bỏ. Không đặt ⇒ ghi ngay (như ngoài transaction).
    /// </summary>
    public UowManagerGia? UowManager { get; set; }

    /// <summary>Ép mọi `InsertAsync`/`InsertManyAsync` NÉM (để test "ghi dòng lô ném ⇒ rollback").</summary>
    public bool NemKhiChen { get; set; }

    /// <summary>Ép `GetListAsync` ném lỗi "không mong đợi" (để test nhánh `Outcome = Loi`).</summary>
    public bool HongGetList { get; set; }

    public IReadOnlyCollection<T> TatCa => _duLieu.Values;

    public void Them(T entity) => _duLieu[entity.Id] = entity;

    public string? EntityName { get; set; } = typeof(T).Name;
    public string ProviderName => "Test";
    public bool? IsChangeTrackingEnabled => false;

    public IAsyncQueryableExecuter AsyncExecuter { get; }
        = new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>());

    public Task<T?> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.TryGetValue(id, out var entity) ? entity : null);

    public Task<T> GetAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
        => _duLieu.TryGetValue(id, out var entity)
            ? Task.FromResult(entity)
            : throw new EntityNotFoundException(typeof(T), id);

    public Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.Values.FirstOrDefault(predicate.Compile()));

    public Task<List<T>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.Values.ToList());

    public Task<List<T>> GetListAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = false,
        CancellationToken cancellationToken = default)
        => HongGetList
            ? throw new NotSupportedException("Lỗi không mong đợi (HongGetList) để test nhánh Loi.")
            : Task.FromResult(_duLieu.Values.Where(predicate.Compile()).ToList());

    public Task<long> GetCountAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult((long)_duLieu.Values.Count(predicate.Compile()));

    public Task<T> InsertAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanChen++;

        if (NemKhiChen)
        {
            throw new InvalidOperationException(
                "Lỗi chèn giả lập (NemKhiChen): dùng cho ca 'ghi dòng lô ném ⇒ rollback' (TC-07).");
        }

        ThucHien(() => _duLieu[entity.Id] = entity);
        return Task.FromResult(entity);
    }

    public Task InsertManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var ds = entities.ToList();
        SoLanChen += ds.Count;

        if (NemKhiChen)
        {
            throw new InvalidOperationException(
                "Lỗi chèn giả lập (NemKhiChen): dùng cho ca 'ghi dòng lô ném ⇒ rollback' (TC-07).");
        }

        ThucHien(() =>
        {
            foreach (var e in ds)
            {
                _duLieu[e.Id] = e;
            }
        });
        return Task.CompletedTask;
    }

    public Task<T> UpdateAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanSua++;
        ThucHien(() => _duLieu[entity.Id] = entity);
        return Task.FromResult(entity);
    }

    public Task UpdateManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var ds = entities.ToList();
        SoLanSua += ds.Count;

        ThucHien(() =>
        {
            foreach (var e in ds)
            {
                _duLieu[e.Id] = e;
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Áp thay đổi theo ngữ nghĩa UoW: có UoW hiện hành ⇒ vào **vùng chờ** (áp khi `CompleteAsync`);
    /// không có ⇒ áp ngay (ngoài transaction mỗi lệnh tự lưu). Tăng `SoLanApDung` **tại lúc áp thật**.
    /// </summary>
    private void ThucHien(Action apDung)
    {
        var uow = UowManager?.Current as UowGia;

        if (uow is null)
        {
            apDung();
            SoLanApDung++;
            return;
        }

        uow.DangKyThayDoi(() =>
        {
            apDung();
            SoLanApDung++;
        });
    }

    public IQueryable<T> GetQueryable() => _duLieu.Values.AsQueryable();

    public Task<IQueryable<T>> GetQueryableAsync()
        => Task.FromResult<IQueryable<T>>(_duLieu.Values.AsQueryable());

    public Task<long> GetCountAsync(CancellationToken cancellationToken = default) => throw Nem(nameof(GetCountAsync));

    public Task<T> GetAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default) => throw Nem(nameof(GetAsync));

    public Task DeleteAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(Guid id, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(
        Expression<Func<T, bool>> predicate, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteAsync));

    public Task DeleteManyAsync(
        IEnumerable<T> entities, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteManyAsync));

    public Task DeleteManyAsync(
        IEnumerable<Guid> ids, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteManyAsync));

    public Task DeleteDirectAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteDirectAsync));

    public Task<IQueryable<T>> WithDetailsAsync() => throw Nem(nameof(WithDetailsAsync));

    public Task<IQueryable<T>> WithDetailsAsync(Expression<Func<T, object>> propertySelector)
        => throw Nem(nameof(WithDetailsAsync));

    public Task<IQueryable<T>> WithDetailsAsync(params Expression<Func<T, object>>[] propertySelectors)
        => throw Nem(nameof(WithDetailsAsync));

    public IQueryable<T> WithDetails() => throw Nem(nameof(WithDetails));

    public IQueryable<T> WithDetails(params Expression<Func<T, object>>[] propertySelectors)
        => throw Nem(nameof(WithDetails));

    public Task<List<T>> GetPagedListAsync(
        int skipCount, int maxResultCount, string sorting, bool includeDetails = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(GetPagedListAsync));

    private static Exception Nem(string thanhVien)
        => new NotSupportedException(
            $"`RepoDiaChiGia<{typeof(T).Name}>.{thanhVien}` không được dùng trong luồng này (W18-4: fake chỉ mở đường đi).");
}

/// <summary>
/// `IUnitOfWorkManager` giả — **ghi lại** mỗi lượt `Begin(requiresNew, isTransactional)` và mỗi `CompleteAsync`.
/// 🔑 Điểm đo của NC-1: đột biến `requiresNew: true` → `false` phải làm test đỏ.
///
/// 📌 **Ngữ nghĩa UoW** (mô hình hoá, `D-248`): `requiresNew: true` ⇒ UoW **riêng** (vùng chờ riêng);
/// `requiresNew: false` **và** đang có UoW hiện hành ⇒ **con dùng chung vùng chờ của UoW ngoài** (giống
/// `DbContext` dùng chung) — vì vậy bỏ `requiresNew` làm thay đổi của worker **rò rỉ** sang UoW ngoài và
/// `CompleteAsync` của UoW ngoài sẽ **commit** chúng dù worker đã thất bại. Đó đúng là lỗi NC-2 chặn.
/// </summary>
internal sealed class UowManagerGia : IUnitOfWorkManager
{
    public IUnitOfWork? Current { get; set; }

    public List<(bool RequiresNew, bool IsTransactional)> LanBegin { get; } = [];

    public List<UowGia> Uows { get; } = [];

    public IUnitOfWork Begin(AbpUnitOfWorkOptions options, bool requiresNew = false)
    {
        LanBegin.Add((requiresNew, options.IsTransactional));

        var cha = Current as UowGia;

        if (!requiresNew && cha is not null && !cha.IsDisposed && !cha.IsCompleted)
        {
            // `requiresNew: false` + có UoW hiện hành ⇒ con DÙNG CHUNG vùng chờ (không rollback riêng).
            var con = new UowGia(options, cha.Kho, soHuuKho: false) { Manager = this };
            con.SetOuter(cha);
            Current = con;
            Uows.Add(con);
            return con;
        }

        var uow = new UowGia(options, new List<Action>(), soHuuKho: true) { Manager = this };
        if (cha is not null)
        {
            uow.SetOuter(cha);
        }

        Current = uow;
        Uows.Add(uow);
        return uow;
    }

    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false)
        => throw new NotSupportedException("Reserve không dùng trong luồng ADDR-001.");

    public void BeginReserved(string reservationName, AbpUnitOfWorkOptions options)
        => throw new NotSupportedException("BeginReserved không dùng trong luồng ADDR-001.");

    public bool TryBeginReserved(string reservationName, AbpUnitOfWorkOptions options)
        => throw new NotSupportedException("TryBeginReserved không dùng trong luồng ADDR-001.");
}

/// <summary>Unit of Work giả — đủ để `using`/`CompleteAsync`/`OnCompleted` chạy; ghi lại trạng thái.</summary>
internal sealed class UowGia : IUnitOfWork
{
    private readonly List<Func<Task>> _sauCommit = [];

    public UowGia(IAbpUnitOfWorkOptions options)
        : this(options, new List<Action>(), soHuuKho: true)
    {
    }

    internal UowGia(IAbpUnitOfWorkOptions options, List<Action> kho, bool soHuuKho)
    {
        Options = options;
        Kho = kho;
        SoHuuKho = soHuuKho;
    }

    /// <summary>Vùng chờ thay đổi (áp khi `CompleteAsync` của UoW **sở hữu**).</summary>
    public List<Action> Kho { get; }

    /// <summary>`true` cho UoW gốc/riêng; `false` cho UoW con dùng chung vùng chờ của UoW ngoài.</summary>
    public bool SoHuuKho { get; }

    public UowManagerGia? Manager { get; set; }

    public Guid Id { get; } = Guid.NewGuid();
    public Dictionary<string, object> Items { get; } = [];
    public IAbpUnitOfWorkOptions Options { get; }
    public IUnitOfWork? Outer { get; private set; }
    public bool IsReserved => false;
    public bool IsDisposed { get; private set; }
    public bool IsCompleted { get; private set; }
    public string? ReservationName => null;
    public IServiceProvider ServiceProvider { get; set; } = new ServiceCollection().BuildServiceProvider();

    public event EventHandler<UnitOfWorkFailedEventArgs>? Failed
    {
        add { }
        remove { }
    }
    public event EventHandler<UnitOfWorkEventArgs>? Disposed;

    public void SetOuter(IUnitOfWork? outer) => Outer = outer;

    public void Initialize(AbpUnitOfWorkOptions options)
    {
    }

    public void Reserve(string reservationName) => throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Đăng ký một thay đổi vào vùng chờ — chỉ áp khi UoW **sở hữu** `CompleteAsync`.</summary>
    public void DangKyThayDoi(Action thayDoi) => Kho.Add(thayDoi);

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;

        foreach (var action in _sauCommit)
        {
            await action();
        }

        if (SoHuuKho)
        {
            foreach (var apDung in Kho.ToList())
            {
                apDung();
            }

            Kho.Clear();
        }
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void OnCompleted(Func<Task> handler) => _sauCommit.Add(handler);

    public void AddOrReplaceLocalEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementPredicate = null)
    {
    }

    public void AddOrReplaceDistributedEvent(UnitOfWorkEventRecord eventRecord, Predicate<UnitOfWorkEventRecord>? replacementPredicate = null)
    {
    }

    public IDatabaseApi? FindDatabaseApi(string key) => null;

    public void AddDatabaseApi(string key, IDatabaseApi api) => throw new NotSupportedException();

    public IDatabaseApi GetOrAddDatabaseApi(string key, Func<IDatabaseApi> factory) => throw new NotSupportedException();

    public ITransactionApi? FindTransactionApi(string key) => null;

    public void AddTransactionApi(string key, ITransactionApi api) => throw new NotSupportedException();

    public ITransactionApi GetOrAddTransactionApi(string key, Func<ITransactionApi> factory) => throw new NotSupportedException();

    public void Dispose()
    {
        IsDisposed = true;

        // Không `Complete` ⇒ bỏ vùng chờ (rollback). UoW con dùng chung kho của UoW ngoài ⇒ KHÔNG xoá.
        if (SoHuuKho && !IsCompleted)
        {
            Kho.Clear();
        }

        if (ReferenceEquals(Manager?.Current, this))
        {
            Manager!.Current = Outer;
        }

        Disposed?.Invoke(this, new UnitOfWorkEventArgs(this));
        GC.SuppressFinalize(this);
    }
}

/// <summary>Khoá phân tán giả (khuôn `KhoaPhanTanGia` của `IdempotencyGuardTests`).</summary>
internal sealed class KhoaPhanTanGia : IAbpDistributedLock
{
    private readonly HashSet<string> _dangGiu = new(StringComparer.Ordinal);
    private readonly Lock _cong = new();

    public bool LuonBan { get; set; }

    public List<string> TenDaXin { get; } = [];

    public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
        string name, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        lock (_cong)
        {
            TenDaXin.Add(name);

            if (LuonBan || !_dangGiu.Add(name))
            {
                return Task.FromResult<IAbpDistributedLockHandle?>(null);
            }
        }

        return Task.FromResult<IAbpDistributedLockHandle?>(new Handle(() =>
        {
            lock (_cong)
            {
                _dangGiu.Remove(name);
            }
        }));
    }

    private sealed class Handle : IAbpDistributedLockHandle
    {
        private readonly Action _giaiPhong;

        public Handle(Action giaiPhong) => _giaiPhong = giaiPhong;

        public ValueTask DisposeAsync()
        {
            _giaiPhong();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// `IDistributedCache&lt;AddressChildrenCacheItem, string&gt;` giả — lưu trong `Dictionary`, **ghi lại** lần
/// `SetAsync` (kèm TTL) và các khoá đã `RemoveManyAsync`. Chỉ hiện thực 3 thành viên đường đi; còn lại NÉM.
/// </summary>
internal sealed class CachePhanTanGia : IDistributedCache<AddressChildrenCacheItem, string>
{
    public Dictionary<string, AddressChildrenCacheItem> DuLieu { get; } = new(StringComparer.Ordinal);

    public List<(string Khoa, TimeSpan? Ttl)> LanSet { get; } = [];

    public List<List<string>> LanXoa { get; } = [];

    public int SoLanDoc { get; private set; }

    public Task<AddressChildrenCacheItem?> GetAsync(
        string key, bool? hideInactive = null, bool considerUow = false,
        CancellationToken token = default)
    {
        SoLanDoc++;
        return Task.FromResult(DuLieu.TryGetValue(key, out var item) ? item : null);
    }

    public Task SetAsync(
        string key, AddressChildrenCacheItem value, DistributedCacheEntryOptions? options = null,
        bool? hideInactive = null, bool considerUow = false, CancellationToken token = default)
    {
        DuLieu[key] = value;
        LanSet.Add((key, options?.AbsoluteExpirationRelativeToNow));
        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(
        IEnumerable<string> keys, bool? hideInactive = null, bool considerUow = false,
        CancellationToken token = default)
    {
        var ds = keys.ToList();
        LanXoa.Add(ds);

        foreach (var k in ds)
        {
            DuLieu.Remove(k);
        }

        return Task.CompletedTask;
    }

    public AddressChildrenCacheItem? Get(string key, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public KeyValuePair<string, AddressChildrenCacheItem?>[] GetMany(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public Task<KeyValuePair<string, AddressChildrenCacheItem?>[]> GetManyAsync(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
        => throw Nem();
    public AddressChildrenCacheItem? GetOrAdd(string key, Func<AddressChildrenCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public Task<AddressChildrenCacheItem?> GetOrAddAsync(string key, Func<Task<AddressChildrenCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
        => throw Nem();
    public KeyValuePair<string, AddressChildrenCacheItem?>[] GetOrAddMany(IEnumerable<string> keys, Func<IEnumerable<string>, List<KeyValuePair<string, AddressChildrenCacheItem>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public Task<KeyValuePair<string, AddressChildrenCacheItem?>[]> GetOrAddManyAsync(IEnumerable<string> keys, Func<IEnumerable<string>, Task<List<KeyValuePair<string, AddressChildrenCacheItem>>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
        => throw Nem();
    public void Set(string key, AddressChildrenCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public void SetMany(IEnumerable<KeyValuePair<string, AddressChildrenCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
        => throw Nem();
    public Task SetManyAsync(IEnumerable<KeyValuePair<string, AddressChildrenCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
        => throw Nem();
    public void Refresh(string key, bool? hideErrors = null) => throw Nem();
    public Task RefreshAsync(string key, bool? hideErrors = null, CancellationToken token = default) => throw Nem();
    public void RefreshMany(IEnumerable<string> keys, bool? hideErrors = null) => throw Nem();
    public Task RefreshManyAsync(IEnumerable<string> keys, bool? hideErrors = null, CancellationToken token = default) => throw Nem();
    public void Remove(string key, bool? hideErrors = null, bool considerUow = false) => throw Nem();
    public Task RemoveAsync(string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default) => throw Nem();
    public void RemoveMany(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false) => throw Nem();

    private static Exception Nem() => new NotSupportedException("CachePhanTanGia chỉ mở GetAsync/SetAsync/RemoveManyAsync.");
}

/// <summary>`ISettingProvider` giả — trả từ `Dictionary`, đếm lượt đọc theo khoá.</summary>
internal sealed class SettingDiaChiGia : ISettingProvider
{
    private readonly Dictionary<string, string?> _giaTri;

    public SettingDiaChiGia(Dictionary<string, string?>? giaTri = null)
        => _giaTri = giaTri ?? new Dictionary<string, string?>(StringComparer.Ordinal);

    public Dictionary<string, int> SoLanDoc { get; } = new(StringComparer.Ordinal);

    public int SoLanDocCua(string tenKhoa) => SoLanDoc.TryGetValue(tenKhoa, out var n) ? n : 0;

    public Task<string?> GetOrNullAsync(string name)
    {
        SoLanDoc[name] = SoLanDocCua(name) + 1;
        return Task.FromResult(_giaTri.TryGetValue(name, out var v) ? v : null);
    }

    public Task<List<SettingValue>> GetAllAsync(string[] names)
        => Task.FromResult(names.Select(n => new SettingValue(n, _giaTri.TryGetValue(n, out var v) ? v : null)).ToList());

    public Task<List<SettingValue>> GetAllAsync()
        => Task.FromResult(_giaTri.Select(kv => new SettingValue(kv.Key, kv.Value)).ToList());
}

/// <summary>`IConfiguration` giả tối thiểu — chỉ indexer (thứ `AddressCatalogCache` đọc).</summary>
internal sealed class CauHinhGia : IConfiguration
{
    private readonly Dictionary<string, string?> _giaTri;

    public CauHinhGia(Dictionary<string, string?>? giaTri = null)
        => _giaTri = giaTri ?? new Dictionary<string, string?>(StringComparer.Ordinal);

    public string? this[string key]
    {
        get => _giaTri.TryGetValue(key, out var v) ? v : null;
        set => _giaTri[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
    public IConfigurationSection GetSection(string key) => new SectionGia(key, this);
}

internal sealed class SectionGia : IConfigurationSection
{
    private readonly string _khoa;
    private readonly IConfiguration _goc;

    public SectionGia(string khoa, IConfiguration goc)
    {
        _khoa = khoa;
        _goc = goc;
    }

    public string Key => _khoa;
    public string Path => _khoa;
    public string? Value { get => _goc[_khoa]; set => _goc[_khoa] = value; }
    public string? this[string key] { get => _goc[$"{_khoa}:{key}"]; set => _goc[$"{_khoa}:{key}"] = value; }
    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
    public IConfigurationSection GetSection(string key) => new SectionGia($"{_khoa}:{key}", _goc);
}

/// <summary>Logger giả — ghi lại các lượt log (dùng để khẳng định `LogWarning` của `D-242`).</summary>
internal sealed class LoggerGia<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> BanGhi { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => BanGhi.Add((logLevel, formatter(state, exception)));
}

/// <summary>`IDistributedEventBus` giả — ghi lại ETO đã phát (kèm cờ `useOutbox`).</summary>
internal sealed class EventBusGia : IDistributedEventBus
{
    public List<(object DuLieu, bool UseOutbox)> DaPhat { get; } = [];

    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
        where TEvent : class
    {
        DaPhat.Add((eventData, useOutbox));
        return Task.CompletedTask;
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
    {
        DaPhat.Add((eventData, useOutbox));
        return Task.CompletedTask;
    }

    public Task PublishAsync(string eventName, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
        => throw new NotSupportedException("Chỉ phát theo kiểu ETO trong luồng ADDR-001.");

    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
        => throw new NotSupportedException();

    public IDisposable Subscribe(string eventName, IDistributedEventHandler<DynamicEventData> handler)
        => throw new NotSupportedException();

    // ── IEventBus (IDistributedEventBus kế thừa) — không dùng trong luồng này ⇒ NÉM ──
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class
        => throw new NotSupportedException();
    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
        => throw new NotSupportedException();
    public Task PublishAsync(string eventName, object eventData, bool onUnitOfWorkComplete = true)
        => throw new NotSupportedException();
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
        => throw new NotSupportedException();
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
        => throw new NotSupportedException();
    public IDisposable Subscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();
    public IDisposable Subscribe(string eventName, IEventHandler handler) => throw new NotSupportedException();
    public IDisposable Subscribe(string eventName, IEventHandlerFactory factory) => throw new NotSupportedException();
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class => throw new NotSupportedException();
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class => throw new NotSupportedException();
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class => throw new NotSupportedException();
    public void Unsubscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class => throw new NotSupportedException();
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();
    public void Unsubscribe(string eventName, IEventHandlerFactory factory) => throw new NotSupportedException();
    public void Unsubscribe(string eventName, IEventHandler handler) => throw new NotSupportedException();
    public void UnsubscribeAll<TEvent>() where TEvent : class => throw new NotSupportedException();
    public void UnsubscribeAll(Type eventType) => throw new NotSupportedException();
    public void UnsubscribeAll(string eventName) => throw new NotSupportedException();
}

/// <summary>`IClock` giả — UTC, giờ cố định (khuôn `DongHoGia` của `IdempotencyGuardTests`).</summary>
internal sealed class DongHoDiaChiGia : IClock
{
    public DongHoDiaChiGia(DateTime now) => Now = now;

    public DateTime Now { get; set; }
    public DateTimeKind Kind => DateTimeKind.Utc;
    public bool SupportsMultipleTimezone => false;
    public DateTime Normalize(DateTime dateTime) => dateTime;
    public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;
    public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
}

/// <summary>`ICurrentUser` giả — chỉ cần `Id` (để `ImportedBy = user:{id}`).</summary>
internal sealed class NguoiDungDiaChiGia : ICurrentUser
{
    public bool IsAuthenticated => Id is not null;
    public Guid? Id { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string? UserName => "qa";
    public string? Name => "QA";
    public string? SurName => null;
    public string? Email => null;
    public string? PhoneNumber => null;
    public string[] Roles => [];
    public Guid? TenantId => null;
    public bool PhoneNumberVerified => false;
    public bool EmailVerified => false;
    public bool IsInRole(string role) => false;
    public string? FindClaimValue(string claimType) => null;
    public System.Security.Claims.Claim? FindClaim(string claimType) => null;
    public System.Security.Claims.Claim[] FindClaims(string claimType) => [];
    public System.Security.Claims.Claim[] GetAllClaims() => [];
}

/// <summary>`ICurrentTenant` giả — host (`TenantId = null`).</summary>
internal sealed class TenantDiaChiGia : ICurrentTenant
{
    public bool IsAvailable => true;
    public Guid? Id { get; set; }
    public string? Name => null;
    public IDisposable Change(Guid? id, string? name = null) => new DoiTen();

    private sealed class DoiTen : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

// ─────────────────────────── HARNESS WORKER `ADDR-002` (`D-248`) ───────────────────────────

/// <summary>
/// Lớp con PHƠI `DoWorkAsync(PeriodicBackgroundWorkerContext)` (protected ở lớp cơ sở ABP) để test gọi
/// **đúng** mã của `CarrierAddressSyncWorker` mà **không** sửa `modules/**` (`D-248`).
///
/// 📌 Metadata đã kiểm 26/11/2026 (`Volo.Abp.BackgroundWorkers` 10.7.0, net10.0):
/// `PeriodicBackgroundWorkerContext` có **ctor PUBLIC** `(IServiceProvider serviceProvider)`; `AbpAsyncTimer`
/// có ctor public không tham số. Không cần reflection để dựng.
/// </summary>
internal sealed class WorkerDiaChiGia : CarrierAddressSyncWorker
{
    public WorkerDiaChiGia(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
    }

    /// <summary>Chạy một lượt `DoWorkAsync` thật của worker với context cho trước.</summary>
    public Task ChayAsync(PeriodicBackgroundWorkerContext workerContext) => DoWorkAsync(workerContext);
}

/// <summary>Bộ phụ thuộc giả của một lượt chạy `CarrierAddressSyncWorker` + số đo.</summary>
internal sealed class BoWorkerDiaChi
{
    public RepoDiaChiGia<CarrierAddressMapping> AnhXa { get; } = new();
    public RepoDiaChiGia<CarrierAddressSyncRun> Lo { get; } = new();
    public RepoDiaChiGia<CarrierAddressSyncRunUnmappedCode> DongCon { get; } = new();
    public UowManagerGia Uow { get; } = new();
    public KhoaPhanTanGia Khoa { get; }
    public DongHoDiaChiGia DongHo { get; } = new(new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc));
    public CauHinhGia CauHinh { get; }

    public BoWorkerDiaChi(bool luonBanKhoa, Dictionary<string, string?> cauHinh)
    {
        Khoa = new KhoaPhanTanGia { LuonBan = luonBanKhoa };
        CauHinh = new CauHinhGia(cauHinh);

        AnhXa.UowManager = Uow;
        Lo.UowManager = Uow;
        DongCon.UowManager = Uow;
    }
}

/// <summary>
/// Dựng `CarrierAddressSyncWorker` **THẬT** (không host ABP, không PostgreSQL — khuôn `D-229`/`D-248`).
/// `ServiceProvider` của `PeriodicBackgroundWorkerContext` trả đúng bộ service worker cần; `LazyServiceProvider`
/// cấp `ILoggerFactory` cho `Logger` của lớp cơ sở ABP.
/// </summary>
internal static class HaTangWorkerDiaChi
{
    public static (WorkerDiaChiGia Worker, PeriodicBackgroundWorkerContext Context, BoWorkerDiaChi Bo) Dung(
        IKhungDiaChiHangSource? adapter = null,
        string carriers = "GHN",
        bool bat = true,
        bool luonBanKhoa = false)
    {
        var bo = new BoWorkerDiaChi(
            luonBanKhoa,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [CarrierAddressSyncWorker.CauHinhBat] = bat ? "true" : "false",
                [CarrierAddressSyncWorker.CauHinhHang] = carriers
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(bo.CauHinh);
        services.AddSingleton<IAbpDistributedLock>(bo.Khoa);
        services.AddSingleton<IUnitOfWorkManager>(bo.Uow);
        services.AddSingleton<IClock>(bo.DongHo);
        services.AddSingleton<IRepository<CarrierAddressMapping, Guid>>(bo.AnhXa);
        services.AddSingleton<IRepository<CarrierAddressSyncRun, Guid>>(bo.Lo);
        services.AddSingleton<IRepository<CarrierAddressSyncRunUnmappedCode, Guid>>(bo.DongCon);

        if (adapter is not null)
        {
            services.AddSingleton<IKhungDiaChiHangSource>(adapter);
        }

        var sp = services.BuildServiceProvider();

        var worker = new WorkerDiaChiGia(
            new AbpAsyncTimer(),
            sp.GetRequiredService<IServiceScopeFactory>());

        worker.LazyServiceProvider = new AbpLazyServiceProvider(sp);

        return (worker, new PeriodicBackgroundWorkerContext(sp), bo);
    }
}

/// <summary>Adapter danh mục hãng giả — trả danh mục hoặc ném exception điều khiển được.</summary>
internal sealed class KhungDiaChiHangGia : IKhungDiaChiHangSource
{
    private readonly Func<Task<DanhMucDiaChiHang>> _tai;

    private KhungDiaChiHangGia(string carrierCode, Func<Task<DanhMucDiaChiHang>> tai)
    {
        CarrierCode = carrierCode;
        _tai = tai;
    }

    public string CarrierCode { get; }

    public Task<DanhMucDiaChiHang> TaiDanhMucAsync(CancellationToken cancellationToken = default) => _tai();

    public static KhungDiaChiHangGia Tra(string carrierCode, DanhMucDiaChiHang danhMuc)
        => new(carrierCode, () => Task.FromResult(danhMuc));

    public static KhungDiaChiHangGia Nem(string carrierCode, Exception ngoaiLe)
        => new(carrierCode, () => Task.FromException<DanhMucDiaChiHang>(ngoaiLe));
}
