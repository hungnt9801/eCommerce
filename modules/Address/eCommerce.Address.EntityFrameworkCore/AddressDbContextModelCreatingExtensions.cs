using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Address.EntityFrameworkCore;

/// <summary>
/// Cấu hình model cho module `Address` — gọi từ **DbContext hợp nhất** (`eCommerceDbContext`).
/// </summary>
public static class AddressDbContextModelCreatingExtensions
{
    /// <summary>Tên bảng vật lý (tiền tố <c>Addr</c> để phân biệt module trong một DB dùng chung).</summary>
    public const string TenBang = "AddrAdministrativeUnits";

    /// <summary>Tên index unique — migration thay bằng SQL thô để có `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUnique = "IX_AddrAdministrativeUnits_TenantId_SchemaVersion_Level_Code";

    public static void ConfigureAddress(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AdministrativeUnit>(b =>
        {
            b.ToTable(TenBang);

            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(5);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ParentCode).HasMaxLength(5);
            b.Property(x => x.LegalBasis).HasMaxLength(512);

            // 🔒 RÀNG BUỘC UNIQUE — khoá thật của danh mục là (PHIÊN BẢN, CẤP, MÃ), KHÔNG phải mã trần.
            // ⚠️ EF không diễn đạt được `NULLS NOT DISTINCT` (PostgreSQL mặc định coi NULL là KHÁC NHAU)
            // ⇒ khi `TenantId` NULL thì ràng buộc MẤT TÁC DỤNG — đúng lỗi C4/F1 đã chứng minh bằng chạy thật.
            // Migration thay index này bằng SQL thô:
            //   CREATE UNIQUE INDEX ... ("TenantId","SchemaVersion","Level","Code") NULLS NOT DISTINCT
            b.HasIndex(x => new { x.TenantId, x.SchemaVersion, x.Level, x.Code })
                .IsUnique()
                .HasDatabaseName(TenIndexUnique);

            // Tra theo cấp dưới: lấy tất cả xã của một tỉnh (dùng cho dropdown + kiểm tra fail-closed).
            b.HasIndex(x => new { x.SchemaVersion, x.Level, x.ParentCode })
                .HasDatabaseName("IX_AddrAdministrativeUnits_SchemaVersion_Level_ParentCode");

            // Tra theo tên (tìm kiếm gần đúng / đối chiếu dữ liệu).
            b.HasIndex(x => new { x.SchemaVersion, x.Name })
                .HasDatabaseName("IX_AddrAdministrativeUnits_SchemaVersion_Name");

            // Hàng rào DB cho bất biến: chỉ có 2 phiên bản schema được hỗ trợ (1 = 3 cấp, 2 = 2 cấp).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAdministrativeUnits_SchemaVersion",
                "\"SchemaVersion\" in (1, 2)"));
        });
    }
}
