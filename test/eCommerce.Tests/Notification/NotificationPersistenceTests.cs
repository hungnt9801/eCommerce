using System.Reflection;
using eCommerce.EntityFrameworkCore;
using eCommerce.Notification;
using Microsoft.EntityFrameworkCore;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-39` — `BR-NOT-001-017`: **5 `DbSet`** trên `eCommerceDbContext` hợp nhất **và** module cung cấp
/// `ConfigureNotification(...)` được gọi trong `OnModelCreating` (`SRS §11`, `ARCH §5.6`).
///
/// 🚨 VÌ SAO: ABP đăng ký `IRepository&lt;T&gt;` bằng cách quét thuộc tính `DbSet&lt;&gt;` trên DbContext,
/// **KHÔNG** quét model. Thiếu `DbSet` ⇒ DI nổ (HTTP 500) ở request đầu — lỗi **chỉ lộ khi chạy**.
/// Thiếu `ConfigureNotification` ⇒ model không có 5 bảng ⇒ migration/đọc ghi sai — im lặng.
///
/// 📌 Dựng model **không cần kết nối** (khuôn `EfCoreRegistration/DbSetRegistrationTests`).
/// </summary>
public class NotificationPersistenceTests
{
    private static readonly Type[] NamEntity =
    [
        typeof(NotificationTemplate),
        typeof(NotificationTemplateVersion),
        typeof(NotificationBatch),
        typeof(NotificationQueueItem),
        typeof(NotificationQueueEvent)
    ];

    private static readonly (Type Kieu, string Bang)[] NamBang =
    [
        (typeof(NotificationTemplate), "NotificationTemplates"),
        (typeof(NotificationTemplateVersion), "NotificationTemplateVersions"),
        (typeof(NotificationBatch), "NotificationBatches"),
        (typeof(NotificationQueueItem), "NotificationQueueItems"),
        (typeof(NotificationQueueEvent), "NotificationQueueEvents")
    ];

    private static eCommerceDbContext TaoDbContext()
    {
        // Không cần kết nối: chỉ DỰNG MODEL. Chuỗi kết nối chỉ để provider biết phương ngữ.
        var options = new DbContextOptionsBuilder<eCommerceDbContext>()
            .UseNpgsql("Host=khong-ket-noi;Database=khong-ket-noi")
            .Options;

        return new eCommerceDbContext(options);
    }

    private static HashSet<Type> CacKieuCoDbSet()
    {
        var kieuDbSet = typeof(DbSet<>);

        return typeof(eCommerceDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                        && p.PropertyType.GetGenericTypeDefinition() == kieuDbSet)
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();
    }

    [Fact]
    public void TC39_nam_entity_Notification_deu_co_DbSet_rieng()
    {
        var coDbSet = CacKieuCoDbSet();

        var thieu = NamEntity.Where(t => !coDbSet.Contains(t)).Select(t => t.Name).ToList();

        Assert.True(thieu.Count == 0,
            "🚨 Entity `Notification` THIẾU `DbSet<T>` trên `eCommerceDbContext` ⇒ ABP không đăng ký " +
            "`IRepository<T>` ⇒ DI nổ (HTTP 500):\n  " + string.Join("\n  ", thieu));
    }

    [Fact]
    public void TC39_ConfigureNotification_duoc_goi_nam_bang_duoc_map()
    {
        using var db = TaoDbContext();

        var sai = new List<string>();
        foreach (var (kieu, bang) in NamBang)
        {
            var entity = db.Model.FindEntityType(kieu);
            var tenBang = entity?.GetTableName();
            if (entity is null || tenBang != bang)
            {
                sai.Add($"{kieu.Name}: mong đợi `{bang}`, nhận `{tenBang ?? "<chưa map>"}`");
            }
        }

        Assert.True(sai.Count == 0,
            "🚨 `ConfigureNotification(...)` CHƯA được gọi trong `OnModelCreating` (hoặc map sai bảng):\n  "
            + string.Join("\n  ", sai));
    }

    [Fact]
    public void TC39_script_tao_bang_sinh_ra_du_nam_bang_Notification()
    {
        // Mạnh hơn "có trong model": khẳng định ĐÚNG SQL mà EF sẽ tạo ở DB thật.
        using var db = TaoDbContext();
        var sql = db.Database.GenerateCreateScript();

        foreach (var (_, bang) in NamBang)
        {
            Assert.Contains($"CREATE TABLE \"{bang}\"", sql);
        }

        // Sổ append-only có CHECK chặn `Enqueued` mang FromStatus (hàng 1 §3) — cấu hình model, không chỉ DB.
        Assert.Contains("CK_NotificationQueueEvents_FromStatus", sql);
        // Unique index chống trùng gửi — hàng rào thật (BR-002).
        Assert.Contains("IX_NotificationQueueItems_Tenant_IdempotencyKey", sql);
    }
}
