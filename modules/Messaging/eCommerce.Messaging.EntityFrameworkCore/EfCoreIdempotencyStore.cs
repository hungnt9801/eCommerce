using eCommerce.Messaging.Idempotency;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Messaging.EntityFrameworkCore;

/// <summary>
/// Bản lưu idempotency **thật** trên PostgreSQL — thay cho <see cref="InMemoryIdempotencyStore"/>
/// (bản in-memory chỉ dùng cho test; nhiều instance sẽ có bộ nhớ riêng ⇒ **chống trùng mất tác dụng
/// mà không có lỗi nào**).
///
/// 🔒 Hàng rào cuối là **ràng buộc unique `NULLS NOT DISTINCT`** trên
/// `(TenantId, Scope, KeyValue)`: kể cả khi khoá phân tán bị mất (Redis hỏng), DB vẫn **không cho**
/// tạo hai bản ghi cho cùng một khoá.
/// </summary>
public class EfCoreIdempotencyStore : IIdempotencyStore, ITransientDependency
{
    private readonly IRepository<IdempotencyRecord, Guid> _repository;

    public EfCoreIdempotencyStore(IRepository<IdempotencyRecord, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<IdempotencyRecord?> TimAsync(
        IdempotencyKey key,
        CancellationToken cancellationToken = default)
    {
        // Bộ lọc tenant do ABP áp tự động (entity IMultiTenant) ⇒ không cần lọc tay.
        var query = await _repository.GetQueryableAsync();

        return await query
            .OrderByDescending(x => x.CreationTime)
            .FirstOrDefaultAsync(
                x => x.Scope == key.Scope && x.KeyValue == key.Value,
                cancellationToken);
    }

    public async Task ThemAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        // ⚠️ autoSave: true — nếu để false thì bản ghi `DangXuLy` không được ghi ngay, và trong lúc
        // xử lý (có thể lâu) một yêu cầu khác sẽ không thấy nó ⇒ mất tác dụng chống trùng.
        await _repository.InsertAsync(record, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task CapNhatAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task<int> XoaHetHanAsync(
        DateTime nowUtc,
        int toiDa,
        CancellationToken cancellationToken = default)
    {
        var query = await _repository.GetQueryableAsync();

        // Lấy ID trước rồi xoá theo lô nhỏ — tránh một câu DELETE khổng lồ làm nghẽn DB.
        var ids = await query
            .Where(x => x.ExpiresAtUtc < nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => x.Id)
            .Take(toiDa)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        await _repository.DeleteManyAsync(ids, autoSave: true, cancellationToken: cancellationToken);

        return ids.Count;
    }
}
