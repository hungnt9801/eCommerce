using System.Linq.Expressions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Tests.Pricing.HaTang;

/// <summary>
/// **REPOSITORY GIẢ IN-MEMORY** cho test tầng app service của `PRC-002` (`D-229` ①(A2)).
///
/// 🎯 Chỉ hiện thực đúng các thành viên mà luồng được test **thật sự đi qua** — và **ĐẾM** lần chèn/sửa để
/// khẳng định *"không ghi nửa"* bằng số đo, không chỉ bằng "không ném" (`D-229` ② T3).
///
/// 🔒 **MỌI thành viên còn lại NÉM `NotSupportedException`** (khuôn `RepoGia` ở
/// `Messaging/DeadLetterPhatLaiTests.cs:190,219-220`). Nếu app service lỡ gọi một đường chưa ai thiết kế
/// (ví dụ `DeleteAsync` trên sổ giải trình append-only) thì test nổ **NGAY**, thay vì lặng lẽ đi qua.
///
/// 📌 `FirstOrDefaultAsync`/`CountAsync` của ABP là **extension** ⇒ phải hiện thực `FindAsync(predicate)` và
/// `GetCountAsync(predicate)` (đúng ghi chú `D-229` ①(A2)).
/// </summary>
public sealed class RepoGia<T> : IRepository<T, Guid> where T : class, IEntity<Guid>
{
    private readonly Dictionary<Guid, T> _duLieu = [];

    /// <summary>Số lần `InsertAsync` **thật sự** được gọi — dùng để khẳng định không ghi nửa khi guard ném.</summary>
    public int SoLanChen { get; private set; }

    /// <summary>Số lần `UpdateAsync` được gọi.</summary>
    public int SoLanSua { get; private set; }

    /// <summary>Toàn bộ bản ghi đang giữ (đọc để khẳng định `Count` trước = sau).</summary>
    public IReadOnlyCollection<T> TatCa => _duLieu.Values;

    /// <summary>Nạp sẵn dữ liệu (không tính là một lần chèn của app service).</summary>
    public void Them(T entity) => _duLieu[entity.Id] = entity;

    public string? EntityName { get; set; } = typeof(T).Name;

    public string ProviderName => "Test";

    public bool? IsChangeTrackingEnabled => false;

    // 📌 ABP 10.7: `FirstOrDefaultAsync`/`CountAsync` extension dùng `AsyncExecuter` (đo bằng stack trace
    //    thật) ⇒ cấp bộ thực thi thật của ABP, KHÔNG viết lại LINQ async.
    public IAsyncQueryableExecuter AsyncExecuter { get; } = new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>());

    // ── Các thành viên ĐƯỜNG ĐI dùng ─────────────────────────────────────────────────────────

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

    public Task<T> InsertAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanChen++;
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<T> UpdateAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        SoLanSua++;
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    // ── MỌI thành viên còn lại ⇒ NÉM (không im lặng) ─────────────────────────────────────────

    // 📌 ABP 10.7: `FirstOrDefaultAsync`/`CountAsync` của ABP là extension đi qua `GetQueryableAsync`
    //    (đo bằng stack trace thật khi chạy ca 2a/2b của `D-229`) ⇒ đây LÀ đường đi, phải mở.
    public IQueryable<T> GetQueryable() => _duLieu.Values.AsQueryable();

    public Task<IQueryable<T>> GetQueryableAsync()
        => Task.FromResult<IQueryable<T>>(_duLieu.Values.AsQueryable());

    public Task<long> GetCountAsync(CancellationToken cancellationToken = default) => throw Nem(nameof(GetCountAsync));

    public Task<T> GetAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = default) => throw Nem(nameof(GetAsync));

    public Task InsertManyAsync(
        IEnumerable<T> entities, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(InsertManyAsync));

    public Task UpdateManyAsync(
        IEnumerable<T> entities, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(UpdateManyAsync));

    public Task DeleteAsync(
        T entity, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(
        Guid id, bool autoSave = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(DeleteAsync));

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
            $"`RepoGia<{typeof(T).Name}>.{thanhVien}` không được dùng trong luồng này (D-229: fake chỉ mở đường đi).");


}
