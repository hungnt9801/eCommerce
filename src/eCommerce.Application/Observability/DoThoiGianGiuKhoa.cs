using System.Collections.Frozen;
using eCommerce.Kernel.Observability;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.DistributedLocking;

namespace eCommerce.Observability;

/// <summary>
/// **DECORATOR `IAbpDistributedLock` — PHÁT `lock_ttl_exceeded_total` KHI KHOÁ PHÂN TÁN BỊ MẤT THẬT.**
///
/// 🚨 **VÌ SAO KHÔNG SO THỜI GIAN GIỮ VỚI THAM SỐ `timeout`:** trong ABP 10.7,
/// <see cref="IAbpDistributedLock.TryAcquireAsync"/> nhận <c>timeout</c> là **thời gian CHỜ giành khoá**
/// (truyền xuống `IDistributedLockProvider.TryAcquireLockAsync`), **KHÔNG** phải TTL của lease Redis.
/// 📌 Nếu lấy tham số đó làm "TTL" thì một job quét hợp pháp giữ khoá 60 giây sẽ **báo động giả** — đúng
/// loại metric nói dối mà `XC-005` cấm. TTL thật do Medallion giữ và **tự động gia hạn** khi handle còn sống.
///
/// ✅ **CÁCH ĐO THẬT:** Medallion phơi `IDistributedSynchronizationHandle.HandleLostToken` — token này
/// **chỉ bị huỷ khi lease đã mất** (hết TTL mà không gia hạn được: tiến trình treo, mất Redis, tranh chấp).
/// ABP có hàm mở khoá handle: <see cref="AbpDistributedLockHandleExtensions.ToDistributedSynchronizationHandle"/>.
/// Wrapper đăng ký token đó và đếm **đúng một lần** cho mỗi handle bị mất. Nhờ vậy:
/// <list type="bullet">
///   <item>không cần biết TTL (và không đoán nó);</item>
///   <item>không đổi hành vi khoá — mọi lời gọi/huỷ vẫn chuyển tiếp nguyên vẹn;</item>
///   <item>không đếm "giữ lâu" (bình thường) thành "mất khoá" (bất thường).</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN:** nếu nhà cung cấp khoá không phải Medallion (ABP hiện dùng Medallion + Redis), wrapper
/// **không quan sát được** token mất khoá ⇒ vẫn chuyển tiếp handle như cũ, chỉ **không phát metric**.
/// Đây là lý do wrapper nằm ở host, nơi `IDistributedLockProvider` Medallion được đăng ký tường minh.
/// </summary>
public sealed class DoThoiGianGiuKhoa : IAbpDistributedLock
{
    private static readonly FrozenSet<string> TienToModule = new[]
    {
        "address", "catalog", "channel", "customers", "einvoice", "inventory",
        "marketplace", "payment", "pricing", "reconciliation", "sales", "tax"
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly IAbpDistributedLock _benTrong;
    private readonly eCommerceMetrics _metrics;

    public DoThoiGianGiuKhoa(IAbpDistributedLock benTrong, eCommerceMetrics metrics)
    {
        _benTrong = benTrong;
        _metrics = metrics;
    }

    public async Task<IAbpDistributedLockHandle?> TryAcquireAsync(
        string name,
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        var handle = await _benTrong.TryAcquireAsync(name, timeout, cancellationToken);

        return handle is null
            ? null
            : new Handle(handle, _metrics, ModuleCuaKhoa(name));
    }

    /// <summary>
    /// Nhãn `module` lấy từ **tiền tố hữu hạn** của tên khoá (quy ước `XC-003` §5.4).
    /// 🚨 Không gắn cả tên khoá: nó chứa tenant/SKU/shopId ⇒ cardinality cao (`XC-005` BR-009).
    /// </summary>
    internal static string ModuleCuaKhoa(string name)
    {
        var viTri = name.IndexOf(':', StringComparison.Ordinal);
        var tienTo = viTri > 0 ? name[..viTri] : name;

        // `payments:refund:...` là tiền tố cũ của PaymentHub — quy về cùng một nhãn module.
        if (string.Equals(tienTo, "payments", StringComparison.Ordinal))
        {
            return "payment";
        }

        return TienToModule.Contains(tienTo) ? tienTo : "other";
    }

    private sealed class Handle : IAbpDistributedLockHandle
    {
        private readonly IAbpDistributedLockHandle _benTrong;
        private readonly eCommerceMetrics _metrics;
        private readonly string _module;
        private readonly CancellationTokenRegistration _dangKyMatKhoa;
        private int _daDem;

        public Handle(IAbpDistributedLockHandle benTrong, eCommerceMetrics metrics, string module)
        {
            _benTrong = benTrong;
            _metrics = metrics;
            _module = module;

            try
            {
                // 🔑 Chỉ đường này mới cho biết lease ĐÃ MẤT; tham số timeout không phải TTL.
                var theoDoi = benTrong.ToDistributedSynchronizationHandle();
                _dangKyMatKhoa = theoDoi.HandleLostToken.Register(
                    static state => ((Handle)state!).DemMotLan(), this);
            }
            catch (Exception)
            {
                // ⚠️ Provider không phải Medallion ⇒ không có token mất khoá. KHÔNG làm hỏng đường khoá:
                // handle vẫn được dùng/bỏ đúng như trước; chỉ metric này không quan sát được.
                _dangKyMatKhoa = default;
            }
        }

        private void DemMotLan()
        {
            if (Interlocked.Exchange(ref _daDem, 1) == 0)
            {
                _metrics.LockTtlExceeded.Add(
                    1,
                    new KeyValuePair<string, object?>(eCommerceMetricTags.Module, _module));
            }
        }

        public ValueTask DisposeAsync()
        {
            // Ngắt đăng ký trước khi trả handle: mất khoá SAU khi đã trả không phải "thao tác vượt TTL".
            _dangKyMatKhoa.Dispose();
            return _benTrong.DisposeAsync();
        }
    }
}

/// <summary>
/// Đăng ký decorator khoá **một lần** ở host (sau khi module ABP đã đăng ký `IAbpDistributedLock`).
///
/// 🚨 Vì sao phải thay descriptor thay vì `AddTransient<IAbpDistributedLock, ...>`: đăng ký thêm sẽ để
/// **hai** hiện thực trong container; resolve đơn lẻ lấy cái cuối, nhưng resolve `IEnumerable`/kiểm tra DI
/// thấy hai nguồn sự thật. Ở đây bỏ descriptor cũ rồi bọc **chính** hiện thực đó.
/// </summary>
public static class DangKyDoThoiGianGiuKhoa
{
    public static IServiceCollection ThemDoThoiGianGiuKhoa(this IServiceCollection services)
    {
        var goc = services.LastOrDefault(d => d.ServiceType == typeof(IAbpDistributedLock))
            ?? throw new InvalidOperationException(
                "Khong tim thay dang ky IAbpDistributedLock de boc lock_ttl_exceeded_total. " +
                "Goi ham nay SAU AbpDistributedLockingModule.ConfigureServices.");

        services.RemoveAll<IAbpDistributedLock>();

        services.AddTransient<IAbpDistributedLock>(sp =>
            new DoThoiGianGiuKhoa(TaoBenTrong(sp, goc), sp.GetRequiredService<eCommerceMetrics>()));

        return services;
    }

    private static IAbpDistributedLock TaoBenTrong(IServiceProvider sp, ServiceDescriptor moTa)
    {
        if (moTa.ImplementationInstance is IAbpDistributedLock instance)
        {
            return instance;
        }

        if (moTa.ImplementationFactory is not null)
        {
            return (IAbpDistributedLock)moTa.ImplementationFactory(sp);
        }

        if (moTa.ImplementationType is not null)
        {
            return (IAbpDistributedLock)ActivatorUtilities.CreateInstance(sp, moTa.ImplementationType);
        }

        throw new InvalidOperationException(
            "Dang ky IAbpDistributedLock khong co ImplementationType/Factory/Instance de boc.");
    }
}
