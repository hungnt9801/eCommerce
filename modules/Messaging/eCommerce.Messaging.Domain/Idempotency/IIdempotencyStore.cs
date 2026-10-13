namespace eCommerce.Messaging.Idempotency;

/// <summary>
/// **Cổng lưu trữ** cho idempotency. Tầng Domain chỉ biết giao diện này;
/// việc lưu bằng EF Core/PostgreSQL là chi tiết của tầng hạ tầng (module `Messaging.EntityFrameworkCore`).
///
/// 🎯 Nhờ tách cổng, thuật toán <see cref="IdempotencyGuard"/> **test được không cần DB** — và
/// bản lưu trong bộ nhớ dưới đây phục vụ đúng mục đích đó.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> TimAsync(IdempotencyKey key, CancellationToken cancellationToken = default);

    Task ThemAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

    Task CapNhatAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

    /// <summary>Xoá bản ghi đã hết hạn (job dọn dẹp). Trả về số bản ghi đã xoá.</summary>
    Task<int> XoaHetHanAsync(DateTime nowUtc, int toiDa, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bản lưu **trong bộ nhớ** — ⚠️ **CHỈ dùng cho test và chạy cục bộ một tiến trình**.
///
/// 🚨 KHÔNG dùng cho production: nhiều instance sẽ có bộ nhớ riêng ⇒ chống trùng **mất tác dụng** và
/// **không có lỗi nào** để biết. Production phải dùng bản lưu trên PostgreSQL với ràng buộc unique
/// `NULLS NOT DISTINCT` — bản ghi chống trùng **bắt buộc** phải nằm ở nơi dùng chung.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _theoKhoa = new(StringComparer.Ordinal);
    private readonly Lock _cong = new();

    private static string KhoaNoiBo(IdempotencyKey key, Guid? tenantId)
        => $"{tenantId?.ToString() ?? "host"}|{key.Scope}|{key.Value}";

    public Task<IdempotencyRecord?> TimAsync(IdempotencyKey key, CancellationToken cancellationToken = default)
    {
        lock (_cong)
        {
            // Tra theo cả tenant: cùng khoá ở hai tenant là HAI bản ghi khác nhau.
            var timThay = _theoKhoa.Values.FirstOrDefault(r =>
                r.Scope == key.Scope && r.KeyValue == key.Value);

            return Task.FromResult(timThay);
        }
    }

    public Task ThemAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        lock (_cong)
        {
            var khoa = KhoaNoiBo(record.Khoa(), record.TenantId);

            // Mô phỏng ràng buộc unique: thêm trùng khoá ⇒ NÉM (giống PostgreSQL sẽ ném 23505).
            if (_theoKhoa.ContainsKey(khoa))
            {
                throw new InvalidOperationException(
                    $"Đã tồn tại bản ghi idempotency cho khoá '{record.Khoa()}' (mô phỏng unique constraint).");
            }

            _theoKhoa[khoa] = record;
            return Task.CompletedTask;
        }
    }

    public Task CapNhatAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        lock (_cong)
        {
            _theoKhoa[KhoaNoiBo(record.Khoa(), record.TenantId)] = record;
            return Task.CompletedTask;
        }
    }

    public Task<int> XoaHetHanAsync(DateTime nowUtc, int toiDa, CancellationToken cancellationToken = default)
    {
        lock (_cong)
        {
            var hetHan = _theoKhoa
                .Where(kv => !kv.Value.ConHieuLuc(nowUtc))
                .Take(toiDa)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var k in hetHan)
            {
                _theoKhoa.Remove(k);
            }

            return Task.FromResult(hetHan.Count);
        }
    }
}
