using eCommerce.Tax.Classifications;
using eCommerce.Tax.RateSchedules;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Tax.EntityFrameworkCore;

public static class TaxDbContextModelCreatingExtensions
{
    /// <summary>
    /// Mapping bảng của module Tax. Được gọi từ DbContext hợp nhất của app.
    /// </summary>
    public static void ConfigureTax(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<TaxRateSchedule>(b =>
        {
            b.ToTable("TaxRateSchedules", t =>
            {
                t.HasCheckConstraint("CK_TaxRateSchedules_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 100");
                t.HasCheckConstraint(
                    "CK_TaxRateSchedules_EffectiveRange",
                    "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);

            // Thuế suất là PHẦN TRĂM ⇒ 9,4 là thừa sức; decimal, KHÔNG BAO GIỜ double.
            b.Property(x => x.Rate).HasPrecision(9, 4);

            b.Property(x => x.ReducedFromCode).HasMaxLength(32);
            b.Property(x => x.LegalBasis).HasMaxLength(512);

            // Một mã mức thuế chỉ định nghĩa một lần cho mỗi tenant.
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            // Truy vấn nóng nhất: "mức nào đang hiệu lực tại ngày X".
            b.HasIndex(x => new { x.TenantId, x.EffectiveFrom, x.EffectiveTo });
        });

        builder.Entity<TaxClassification>(b =>
        {
            b.ToTable("TaxClassifications", t =>
            {
                t.HasCheckConstraint(
                    "CK_TaxClassifications_EffectiveRange",
                    "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
            b.ConfigureByConvention();

            b.Property(x => x.BaseRateCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.LegalBasisNote).HasMaxLength(512);

            // Chống trùng khung: cùng (tenant, đối tượng) không thể có 2 bản ghi cùng ngày bắt đầu.
            b.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId, x.EffectiveFrom }).IsUnique();

            // Truy vấn nóng nhất của TaxRateResolverService.
            b.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId, x.Status });

            // 🚨 CHỐNG CHỒNG KHUNG HIỆU LỰC Ở TẦNG DB (BR-XC-006-009):
            // Index ở trên KHÔNG đủ để chặn hai khung giao nhau (ví dụ 01/01–30/06 và 01/03–31/12).
            // Ràng buộc loại trừ dùng PostgreSQL `EXCLUDE USING gist` — EF Core KHÔNG có API khai báo
            // ⇒ được thêm bằng SQL thô trong migration (kèm `CREATE EXTENSION btree_gist`).
            // Xem migration Add_Tax_Module và ghi chú "EXCLUDE CONSTRAINT" trong đó.
        });
    }
}
