using System.Reflection;
using eCommerce.ChannelHub.Events;
using eCommerce.Marketplace;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Tests.Marketplace;

/// <summary>
/// 🚨 **Bất biến "PHẢI CÓ NƠI NHẬN ĐƠN"** — chặn **lỗi #22** tái phát ngay trong CI.
///
/// VÌ SAO CẦN: lỗi #22 tồn tại được vì `ChannelShopSyncer` publish `ChannelOrderPulledEto` mà
/// **không ai nhận**, rồi vẫn **tiến con trỏ đồng bộ** ⇒ đơn mất vĩnh viễn, **không lỗi nào được ghi**.
/// Cảnh báo lúc khởi động (`EventCoverageChecker`) chỉ **nhìn thấy** lỗi khi app chạy — còn test này
/// **chặn ngay ở CI**: ai xoá/đổi tên handler (hoặc đổi kiểu sự kiện) thì test **đỏ**.
///
/// ⚠️ Giới hạn đã biết (nói thẳng): test khẳng định **có** một handler phù hợp trong assembly; nó **không**
/// chứng minh handler đó được **đăng ký** trong đồ thị module lúc chạy — việc đó do `EventCoverageProbe`
/// kiểm tra lúc khởi động (đã kiểm chứng bằng log thật).
/// </summary>
public class MarketplaceOrderIntakeBatBienTests
{
    private static readonly Assembly AssemblyUngDung = typeof(MarketplaceOrderIntakeHandler).Assembly;

    private static IReadOnlyList<Type> CacKieu()
    {
        try
        {
            return AssemblyUngDung.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!).ToList();
        }
    }

    [Fact]
    public void PHAI_co_handler_nhan_ChannelOrderPulledEto()
    {
        var kieuNhan = typeof(IDistributedEventHandler<ChannelOrderPulledEto>);

        var cacHandler = CacKieu()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(kieuNhan.IsAssignableFrom)
            .ToList();

        Assert.True(
            cacHandler.Count >= 1,
            "🚨 KHÔNG có handler nào nhận `ChannelOrderPulledEto` ⇒ lỗi #22 quay lại: ChannelShopSyncer " +
            "publish cho KHÔNG AI rồi tiến con trỏ đồng bộ ⇒ ĐƠN MẤT VĨNH VIỄN. " +
            "Cổng fail-closed sẽ chặn (không tiến con trỏ), nhưng đơn cũng KHÔNG vào được hệ thống.");
    }

    [Fact]
    public void Handler_nhan_don_phai_dang_ky_duoc_theo_QUY_UOC_cua_ABP()
    {
        // 🚨 ABP đăng ký handler theo QUY ƯỚC (marker interface). Thiếu marker ⇒ class vẫn biên dịch,
        // mọi test khác vẫn xanh, nhưng handler **không bao giờ được gọi** — lỗi im lặng đúng họ #22.
        var handler = typeof(MarketplaceOrderIntakeHandler);

        var coMarker = handler.GetInterfaces().Any(i =>
            i == typeof(ITransientDependency)
            || i == typeof(IScopedDependency)
            || i == typeof(ISingletonDependency));

        Assert.True(
            coMarker,
            "🚨 `MarketplaceOrderIntakeHandler` THIẾU marker đăng ký DI (`ITransientDependency`/…) " +
            "⇒ ABP KHÔNG đăng ký nó ⇒ đơn sàn không có nơi nhận ⇒ lỗi #22.");
    }

    [Fact]
    public void Bang_nhat_ky_nhan_don_phai_co_DbSet_tren_DbContext_hop_nhat()
    {
        // 🚨 Quy ước #3: entity KHÔNG có `DbSet` trên DbContext hợp nhất ⇒ `IRepository<T>` **không được
        // đăng ký** ⇒ handler không resolve được ⇒ đơn mất. Lỗi này ĐÃ xảy ra thật ở Phase 0.
        var dbContextType = typeof(EntityFrameworkCore.eCommerceDbContext);

        var coDbSet = dbContextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.PropertyType.IsGenericType
                      && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>)
                      && p.PropertyType.GetGenericArguments()[0] == typeof(Orders.MarketplaceOrderIntake));

        Assert.True(
            coDbSet,
            "🚨 `MarketplaceOrderIntake` KHÔNG có `DbSet` trên `eCommerceDbContext` ⇒ " +
            "`IRepository<MarketplaceOrderIntake, Guid>` không được đăng ký ⇒ handler nhận đơn không chạy.");
    }

    [Fact]
    public void DbSet_cua_don_hang_va_dong_don_van_con()
    {
        // Bất biến nền: 2 `DbSet` này là điều kiện để handler GHI được đơn xuống DB.
        var dbContextType = typeof(EntityFrameworkCore.eCommerceDbContext);
        var kieuDbSet = typeof(Microsoft.EntityFrameworkCore.DbSet<>);

        var co = dbContextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == kieuDbSet)
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToList();

        Assert.Contains(typeof(Orders.Order), co);
        Assert.Contains(typeof(Orders.OrderLine), co);
    }
}
