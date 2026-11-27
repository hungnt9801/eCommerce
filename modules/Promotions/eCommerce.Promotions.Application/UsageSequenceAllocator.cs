using Volo.Abp.DependencyInjection;

namespace eCommerce.Promotions;

/// <summary>
/// Cấp `UsageSequence` theo **`D-295`** (`PRO-001-arch` §6.2) — tách **vòng thử** ra khỏi DB để unit-test được.
///
/// 🚨 Ngữ nghĩa **bắt buộc**:
/// <list type="bullet">
///   <item>lần thử = **1 ghi + 4 thử lại** (`PromotionOptions.MaxUsageSequenceAttempts = 5`); **0 dòng** = trùng số
///     ⇒ **thử tiếp** số kế;</item>
///   <item>`23505` từ index **KHÁC** ⇒ <c>thuChen</c> **NÉM** ⇒ allocator **KHÔNG** bắt (không thử tiếp) — nơi gọi
///     xử lý trong `SAVEPOINT`, tránh `25P02`;</item>
///   <item>hết trần ⇒ trả `null` (nơi gọi ném `0104` + metric `usage_sequence_exhausted`).</item>
/// </list>
/// </summary>
public interface IUsageSequenceAllocator
{
    Task<int?> ThuAsync(
        int soBatDau,
        int? tranTren,
        int soLanThu,
        Func<int, CancellationToken, Task<bool>> thuChen,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class UsageSequenceAllocator : IUsageSequenceAllocator, ITransientDependency
{
    public async Task<int?> ThuAsync(
        int soBatDau,
        int? tranTren,
        int soLanThu,
        Func<int, CancellationToken, Task<bool>> thuChen,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(thuChen);

        var lanToiDa = soLanThu < 1 ? 1 : soLanThu;
        var so = soBatDau;

        for (var lan = 0; lan < lanToiDa; lan++)
        {
            if (tranTren is int max && so > max)
            {
                return null;
            }

            // 🚨 KHÔNG bọc try/catch: `23505` index khác phải NỔI LÊN (không thử tiếp, không nuốt).
            if (await thuChen(so, cancellationToken))
            {
                return so;
            }

            so++;
        }

        return null;
    }

    /// <summary>
    /// Hàm **THUẦN** chọn số bắt đầu = **số nhỏ nhất chưa bị lượt còn hiệu lực giữ**.
    /// `tranKhach` có giá trị ⇒ tìm trong `[1, tranKhach]`, hết số ⇒ `null` (**NÉM** `0104` **ngay**, không thử).
    /// `tranKhach = null` ⇒ tìm trong `[1, soLuongHieuLuc + 1]` — **CẤM** gán `1` cho mọi lượt (`BR-04`).
    /// </summary>
    public static int? TimSoBatDau(IReadOnlyCollection<int> daDung, int? tranKhach, int soLuongHieuLuc)
    {
        var tap = daDung as ISet<int> ?? new HashSet<int>(daDung);
        var tran = tranKhach ?? Math.Max(1, soLuongHieuLuc + 1);

        for (var so = 1; so <= tran; so++)
        {
            if (!tap.Contains(so))
            {
                return so;
            }
        }

        return null;
    }
}
