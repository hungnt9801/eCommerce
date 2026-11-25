using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace eCommerce.Tests.Pricing.HaTang;

/// <summary>
/// **Hạ tầng tối thiểu** để dựng app service bằng `new` rồi gán tay `LazyServiceProvider` (`D-229` ①(A4)).
///
/// 🎯 `ApplicationService` lấy `Logger` / `GuidGenerator` **qua `LazyServiceProvider`** (ABP property
/// injection). Test dựng `new` nên ABP **không** tiêm được ⇒ phải gán tay một provider trên
/// `ServiceCollection` tối thiểu. **KHÔNG** dùng DI container của app, **KHÔNG** khởi động Host (`D-229`).
///
/// 📌 `D-229` nêu `SimpleGuidGenerator.Instance`; lớp đó nằm ở package `Volo.Abp.Guids` **ngoài** đồ thị
/// package của project test ⇒ dùng `GuidGia` tương đương (`Guid.NewGuid()`, đúng hành vi `SimpleGuidGenerator`)
/// để **không** thêm package (M11/`D-229` ①: 0 package).
/// </summary>
public static class HaTangAbp
{
    public static TDich Noi<TDich>(TDich dich) where TDich : ApplicationService
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IGuidGenerator>(new GuidGia());
        services.AddSingleton<ILogger>(NullLogger.Instance);

        dich.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());

        return dich;
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}
