using System.Reflection;
using eCommerce.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace eCommerce.Tests.EfCoreRegistration;

// 🚨 BÀI HỌC NHỎ NHƯNG THẬT (15/10/2026): ban đầu namespace của file này là
// `eCommerce.Tests.EntityFrameworkCore`. Nó **CHE KHUẤT** namespace production `eCommerce.EntityFrameworkCore`
// khi resolve từ trong `eCommerce.Tests.*` (C# tìm theo namespace bao ngoài trước) ⇒ các test khác
// (ví dụ `eCommerce.Tests.Marketplace`) không còn thấy `eCommerce.EntityFrameworkCore.eCommerceDbContext`
// và **build ĐỎ** ở file KHÔNG LIÊN QUAN. ⇒ Đặt tên namespace test **tránh trùng** namespace production.

/// <summary>
/// 🛡️ **BẤT BIẾN: mọi entity `eCommerce.*` trong model PHẢI có `DbSet&lt;T&gt;` trên DbContext.**
///
/// 🚨 VÌ SAO (lỗi thật gặp 15/10/2026, `CAT-002`): ABP đăng ký `IRepository&lt;T&gt;` bằng cách **quét các
/// thuộc tính `DbSet&lt;&gt;`** trên DbContext, **KHÔNG** quét model. Tôi đã khai
/// `builder.Entity&lt;Category&gt;(...)` trong `OnModelCreating` mà **quên** `DbSet` ⇒ **không có**
/// `IRepository&lt;Category, Guid&gt;` ⇒ app service **NỔ khi DI** ở request đầu tiên:
/// <i>"None of the constructors found … Cannot resolve parameter IRepository`2[eCommerce.Catalog.Category …]"</i>
/// ⇒ **HTTP 500**, và chỉ lộ ra khi **gọi API thật** (unit test + build đều xanh!).
///
/// ⇒ Test này biến lỗi **chỉ-lộ-khi-chạy** thành lỗi **lộ-ngay-trong-CI**, cho **mọi** entity thêm sau này.
///
/// 📌 Phạm vi: chỉ entity có namespace `eCommerce` (do dự án sở hữu). Entity của ABP/OpenIddict/Identity
/// nằm trong DbContext **của chính chúng** nên không xét ở đây.
/// </summary>
public class DbSetRegistrationTests
{
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
    public void MOI_entity_eCommerce_trong_model_phai_co_DbSet()
    {
        using var db = TaoDbContext();

        var coDbSet = CacKieuCoDbSet();

        var thieu = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("eCommerce", StringComparison.Ordinal))
            .Distinct()
            .Where(t => !coDbSet.Contains(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.True(thieu.Count == 0,
            "🚨 Các entity sau có trong model nhưng THIẾU `DbSet<T>` trên `eCommerceDbContext` ⇒ " +
            "ABP sẽ KHÔNG đăng ký `IRepository<T>` ⇒ DI nổ (HTTP 500) khi gọi API:\n  " +
            string.Join("\n  ", thieu.Select(t => t.FullName)));
    }

    [Fact]
    public void Test_nay_thuc_su_thay_duoc_entity__co_entity_eCommerce_trong_model()
    {
        // 🚨 Chống "test rỗng": nếu cách quét model sai (ví dụ không có entity nào khớp) thì test trên
        // sẽ XANH một cách VÔ NGHĨA. Khẳng định có entity thật + có `DbSet` thật.
        using var db = TaoDbContext();

        var kieuECommerce = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("eCommerce", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(kieuECommerce.Count >= 10,
            $"Chỉ quét được {kieuECommerce.Count} entity `eCommerce.*` — có vẻ cách quét model đã SAI.");

        // Và `Category` (entity vừa gây lỗi DI thật) phải nằm trong danh sách có `DbSet`.
        Assert.Contains(typeof(eCommerce.Catalog.Category), CacKieuCoDbSet());
    }

    [Fact]
    public void Category_phai_co_DbSet_va_duoc_map_vao_bang_CatCategories()
    {
        using var db = TaoDbContext();

        var entity = db.Model.FindEntityType(typeof(eCommerce.Catalog.Category));

        Assert.NotNull(entity);
        Assert.Equal("CatCategories", entity!.GetTableName());
    }

    [Fact]
    public void Category_phai_co_CHECK_constraint_o_tang_model()
    {
        // ⚠️ Khẳng định **CẤU HÌNH ở model** (không chỉ ở DB thật): ai bỏ `CHECK` trong `OnModelCreating`
        // thì migration sau sẽ **mất ràng buộc** — test này chặn ngay, nhanh hơn integration test.
        //
        // 📌 Cách làm: đọc **SQL mà EF sẽ sinh** (`GenerateCreateScript`) thay vì gọi
        // `entity.GetCheckConstraints()` — API đó **NÉM lỗi** trên model chỉ-đọc (đã gặp thật khi viết test này).
        // Cách này còn mạnh hơn: nó khẳng định **đúng thứ sẽ được tạo ra ở DB**.
        using var db = TaoDbContext();

        var sql = db.Database.GenerateCreateScript();

        Assert.Contains("CK_CatCategories_Path_DinhDang", sql);
        Assert.Contains("CK_CatCategories_Level_Khoang", sql);
        Assert.Contains("CREATE TABLE \"CatCategories\"", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
    }
}
