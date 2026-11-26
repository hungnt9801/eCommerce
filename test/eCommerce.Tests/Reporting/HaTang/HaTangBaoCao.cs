using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace eCommerce.Tests.Reporting.HaTang;

/// <summary>
/// **REPOSITORY GIẢ IN-MEMORY** cho tầng app service của `REP-001` (`WP-26`, khuôn `D-229`/`D-248`).
///
/// Mở **đúng** các thành viên mà luồng vận hành đi qua (đọc theo predicate, chèn, sửa, xoá theo predicate —
/// `TongHopBaoCaoService` dùng `DeleteAsync(predicate)` để thay thế theo ngày). Mọi thành viên khác **NÉM**
/// để lời gọi ngoài thiết kế nổ ngay, thay vì im lặng.
/// </summary>
public sealed class RepoBaoCaoGia<T> : IRepository<T, Guid> where T : class, IEntity<Guid>
{
    private readonly Dictionary<Guid, T> _duLieu = [];

    public int SoLanChen { get; private set; }
    public int SoLanSua { get; private set; }
    public int SoLanXoa { get; private set; }

    /// <summary>Số **lời gọi** `DeleteAsync(predicate)` (kể cả khi 0 dòng khớp).</summary>
    public int SoLanGoiXoa { get; private set; }

    public IReadOnlyCollection<T> TatCa => _duLieu.Values;

    /// <summary>Nạp sẵn dữ liệu — KHÔNG tính là một lần chèn của app service.</summary>
    public void Them(T entity) => _duLieu[entity.Id] = entity;

    public string? EntityName { get; set; } = typeof(T).Name;
    public string ProviderName => "Test";
    public bool? IsChangeTrackingEnabled => false;

    // 📌 `FirstOrDefaultAsync`/`CountAsync` của ABP là extension ⇒ đi qua `GetQueryableAsync` + `AsyncExecuter`.
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
        => Task.FromResult(_duLieu.Values.Where(predicate.Compile()).ToList());

    public Task<long> GetCountAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult((long)_duLieu.Values.Count(predicate.Compile()));

    public IQueryable<T> GetQueryable() => _duLieu.Values.AsQueryable();

    public Task<IQueryable<T>> GetQueryableAsync()
        => Task.FromResult<IQueryable<T>>(_duLieu.Values.AsQueryable());

    public Task<T> InsertAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanChen++;
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task InsertManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            SoLanChen++;
            _duLieu[entity.Id] = entity;
        }

        return Task.CompletedTask;
    }

    public Task<T> UpdateAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanSua++;
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(
        Expression<Func<T, bool>> predicate, bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        SoLanGoiXoa++;

        foreach (var id in _duLieu.Values.Where(predicate.Compile()).Select(x => x.Id).ToList())
        {
            _duLieu.Remove(id);
            SoLanXoa++;
        }

        return Task.CompletedTask;
    }

    // ── Mọi thành viên còn lại ⇒ NÉM (không im lặng) ──────────────────────────────────────────────

    public Task<long> GetCountAsync(CancellationToken cancellationToken = default) => throw Nem(nameof(GetCountAsync));

    public Task<T> GetAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default) => throw Nem(nameof(GetAsync));

    public Task UpdateManyAsync(
        IEnumerable<T> entities, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(UpdateManyAsync));

    public Task DeleteAsync(
        T entity, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(
        Guid id, bool autoSave = false,
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
            $"`RepoBaoCaoGia<{typeof(T).Name}>.{thanhVien}` không được dùng trong luồng này (D-229: fake chỉ mở đường đi).");
}

/// <summary>Khoá phân tán giả — bật/tắt giành được khoá, ghi lại tên khoá đã thử.</summary>
public sealed class KhoaBaoCaoGia : IAbpDistributedLock
{
    public bool ChoGiu { get; set; } = true;

    public int SoLanThu { get; private set; }

    public string? KhoaCuoi { get; private set; }

    public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
        string name, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        SoLanThu++;
        KhoaCuoi = name;
        return Task.FromResult<IAbpDistributedLockHandle?>(ChoGiu ? new Giu() : null);
    }

    private sealed class Giu : IAbpDistributedLockHandle
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Đồng hồ giả — mốc UTC cố định để ngày kinh doanh tất định.</summary>
public sealed class DongHoBaoCaoGia : IClock
{
    public DongHoBaoCaoGia(DateTime now) => Now = now;

    public DateTime Now { get; set; }

    public DateTimeKind Kind => DateTimeKind.Utc;

    public bool SupportsMultipleTimezone => false;

    public DateTime Normalize(DateTime dateTime) => dateTime;

    public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;

    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;

    public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
}

/// <summary>`ICurrentTenant` giả — chỉ cần `Id`.</summary>
public sealed class TenantBaoCaoGia : ICurrentTenant
{
    public bool IsAvailable => true;

    public Guid? Id { get; set; }

    public string? Name => null;

    public IDisposable Change(Guid? id, string? name = null)
    {
        Id = id;
        return new DoiTen();
    }

    private sealed class DoiTen : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>`ICurrentUser` giả — chỉ cần `Id` (dùng cho `RequestedBy`/`ClosedBy`).</summary>
public sealed class NguoiDungBaoCaoGia : ICurrentUser
{
    public bool IsAuthenticated => Id is not null;

    public Guid? Id { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

public sealed class GuidBaoCaoGia : IGuidGenerator
{
    public Guid Create() => Guid.NewGuid();
}

/// <summary>
/// **HẠ TẦNG TỐI THIỂU** để dựng app service bằng `new` rồi gán `LazyServiceProvider` (`D-229` ①(A4)).
/// `ApplicationService` lấy `Logger`/`Clock`/`AsyncExecuter`/`CurrentUser` **qua property injection** ⇒ test
/// phải cấp một `ServiceProvider` tối thiểu. KHÔNG host ABP, KHÔNG SQLite/EF thật.
/// </summary>
public static class HaTangBaoCao
{
    public static TDich Noi<TDich>(TDich dich, IClock clock, ICurrentUser user, ICurrentTenant tenant)
        where TDich : ApplicationService
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IGuidGenerator>(new GuidBaoCaoGia());
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddSingleton<IAsyncQueryableExecuter>(
            new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>()));

        dich.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());

        return dich;
    }
}
