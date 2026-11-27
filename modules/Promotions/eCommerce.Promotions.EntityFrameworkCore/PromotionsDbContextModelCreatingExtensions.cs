using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Promotions.EntityFrameworkCore;

/// <summary>
/// Mapping **7 bảng `Pro*`** của `PRO-002` (`PRO-002-arch` §5.4) — gọi **MỘT LẦN** từ
/// `eCommerceDbContext.OnModelCreating`. Các thuộc tính EF **không** mô hình hoá (`NULLS NOT DISTINCT`) được
/// tạo bằng **SQL thô** ở migration `Add_Promotion_Module` (⚠️ EF sinh lại index sẽ **mất** NND — xem ghi chú migration).
/// </summary>
public static class PromotionsDbContextModelCreatingExtensions
{
    public const string BangPromotion = "ProPromotions";
    public const string BangCondition = "ProPromotionConditions";
    public const string BangRuleCost = "ProPromotionRuleCosts";
    public const string BangVoucher = "ProVoucherCodes";
    public const string BangMapping = "ProPromotionMappings";
    public const string BangFlashSaleSlot = "ProFlashSaleSlots";
    public const string BangRedemption = "ProPromotionRedemptions";

    // 🚨 Tên index ĐÍCH DANH. Hai tên gốc trong `PRO-002-arch` §5.5 dài hơn 63 byte ⇒ PostgreSQL cắt âm thầm;
    // ở đây dùng tên ≤ 63 byte (đồng dạng) — ghi trong `docDeviation`.
    public const string IxPromotionTenantCode = "IX_ProPromotions_TenantId_Code";
    public const string IxPromotionCandidate = "IX_ProPromotions_TenantId_Status_StartsAtUtc_EndsAtUtc";
    public const string IxConditionThuTu = "IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort";
    public const string IxConditionMotGoc = "IX_ProPromotionConditions_MotGoc";
    public const string IxVoucherTenantCode = "IX_ProVoucherCodes_TenantId_Code";
    public const string IxRedemptionTenantIdem = "IX_ProPromotionRedemptions_TenantId_IdempotencyKey";
    public const string IxRedemptionVoucherDon = "IX_ProPromRedemptions_Tenant_Prom_Order_Voucher";
    public const string IxRedemptionKhachTran = "IX_ProPromRedemptions_Tenant_Prom_Cust_Seq";
    public const string IxRedemptionMotVoucherDon = "IX_ProPromotionRedemptions_TenantId_OrderId_Voucher";
    public const string IxRedemptionMotTuDongDon = "IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong";
    public const string IxRedemptionStatusExpires = "IX_ProPromotionRedemptions_TenantId_Status_ExpiresAtUtc";

    public static void ConfigurePromotions(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<Promotion>(b =>
        {
            b.ToTable(BangPromotion);
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(PromotionConsts.CodeToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(PromotionConsts.NameToiDa);
            b.Property(x => x.Kind).HasConversion<string>().IsRequired().HasMaxLength(PromotionConsts.KindToiDa);
            b.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(PromotionConsts.StatusToiDa);
            b.Property(x => x.DiscountKind).HasConversion<string>().IsRequired().HasMaxLength(PromotionConsts.DiscountKindToiDa);
            b.Property(x => x.ChannelScope).HasMaxLength(PromotionConsts.ChannelScopeToiDa);

            b.Property(x => x.DiscountValue).HasPrecision(18, 4);
            b.Property(x => x.MaxDiscountAmount).HasPrecision(18, 4);
            b.Property(x => x.MinOrderAmount).HasPrecision(18, 4);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();

            // `xmin` của PostgreSQL — concurrency token, KHÔNG tạo cột thật.
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName(IxPromotionTenantCode);
            b.HasIndex(x => new { x.TenantId, x.Status, x.StartsAtUtc, x.EndsAtUtc }).HasDatabaseName(IxPromotionCandidate);

            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProPromotions_Kind_NotFlashSale", "\"Kind\" <> 'FlashSale'");
                t.HasCheckConstraint("CK_ProPromotions_DiscountKind_NotFreeShipping", "\"DiscountKind\" <> 'FreeShipping'");
                t.HasCheckConstraint("CK_ProPromotions_DiscountValue", "\"DiscountValue\" >= 0");
                t.HasCheckConstraint("CK_ProPromotions_Percentage",
                    "\"DiscountKind\" <> 'Percentage' OR (\"DiscountValue\" <= 100 AND \"MaxDiscountAmount\" IS NOT NULL)");
                t.HasCheckConstraint("CK_ProPromotions_Usage",
                    "\"UsedCount\" >= 0 AND \"ReservedCount\" >= 0 AND (\"MaxUsageTotal\" IS NULL OR \"UsedCount\" + \"ReservedCount\" <= \"MaxUsageTotal\")");
                t.HasCheckConstraint("CK_ProPromotions_Window", "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
                t.HasCheckConstraint("CK_ProPromotions_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            });
        });

        builder.Entity<PromotionCondition>(b =>
        {
            b.ToTable(BangCondition);
            b.ConfigureByConvention();

            b.Property(x => x.InteractionType).HasConversion<string>().HasMaxLength(PromotionConsts.InteractionTypeToiDa);
            b.Property(x => x.RuleSystemName).HasMaxLength(PromotionConsts.RuleSystemNameToiDa);
            b.Property(x => x.ConfigJson).HasColumnType("jsonb");

            b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<PromotionCondition>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.PromotionId, x.ParentId, x.SortOrder })
                .IsUnique()
                .HasDatabaseName(IxConditionThuTu);
            // Partial unique một gốc (BR-03) — filter; NND bổ sung bằng SQL thô ở migration.
            b.HasIndex(x => new { x.TenantId, x.PromotionId })
                .IsUnique()
                .HasFilter("\"ParentId\" IS NULL")
                .HasDatabaseName(IxConditionMotGoc);

            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProPromotionConditions_NodeLai",
                    "(\"IsGroup\" AND \"InteractionType\" IS NOT NULL AND \"RuleSystemName\" IS NULL) OR " +
                    "(NOT \"IsGroup\" AND \"InteractionType\" IS NULL AND \"RuleSystemName\" IS NOT NULL)");
                t.HasCheckConstraint("CK_ProPromotionConditions_InteractionType",
                    "NOT \"IsGroup\" OR \"InteractionType\" IN ('And','Or')");
                t.HasCheckConstraint("CK_ProPromotionConditions_ParentKhacId",
                    "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
                t.HasCheckConstraint("CK_ProPromotionConditions_Depth", "\"Depth\" BETWEEN 1 AND 8");
                t.HasCheckConstraint("CK_ProPromotionConditions_SortOrder", "\"SortOrder\" >= 0");
            });
        });

        builder.Entity<PromotionRuleCost>(b =>
        {
            b.ToTable(BangRuleCost);
            b.ConfigureByConvention();

            b.Property(x => x.Id).HasMaxLength(PromotionConsts.RuleSystemNameToiDa);
            b.Property(x => x.CostKind).IsRequired().HasMaxLength(PromotionConsts.CostKindToiDa);
            b.Property(x => x.DeclaredCostFactor).IsRequired();
            b.Property(x => x.RequiresVerifiedCod).IsRequired();
            b.Property(x => x.RetiredAtUtc);

            b.ToTable(t => t.HasCheckConstraint("CK_ProPromotionRuleCosts_DeclaredCostFactor", "\"DeclaredCostFactor\" >= 1"));
        });

        builder.Entity<VoucherCode>(b =>
        {
            b.ToTable(BangVoucher);
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(PromotionConsts.CodeToiDa);
            b.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(PromotionConsts.VoucherStatusToiDa);
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName(IxVoucherTenantCode);

            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProVoucherCodes_Usage",
                    "\"UsedCount\" >= 0 AND \"ReservedCount\" >= 0 AND \"UsedCount\" + \"ReservedCount\" <= \"MaxUsage\"");
                // `D-300` (`W25-2`) — miền `Status` (gia cố `BR-09`/`ADR-09`; enum CẤM chứa Expired/Exhausted).
                t.HasCheckConstraint("CK_ProVoucherCodes_Status",
                    "\"Status\" IN ('Generated','Issued','Revoked')");
            });
        });

        builder.Entity<PromotionMapping>(b =>
        {
            b.ToTable(BangMapping);
            b.ConfigureByConvention();

            b.Property(x => x.TargetType).IsRequired().HasMaxLength(PromotionConsts.TargetTypeToiDa);
            b.Property(x => x.Mode).IsRequired().HasMaxLength(PromotionConsts.ModeToiDa);

            b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.PromotionId }).HasDatabaseName("IX_ProPromotionMappings_TenantId_PromotionId");

            b.ToTable(t => t.HasCheckConstraint("CK_ProPromotionMappings_TargetType",
                "\"TargetType\" NOT IN ('Sku','Category','Brand')"));
        });

        builder.Entity<FlashSaleSlot>(b =>
        {
            b.ToTable(BangFlashSaleSlot);
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(PromotionConsts.SkuToiDa);
            b.Property(x => x.SalePrice).HasPrecision(18, 4);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.QuantityLimit).HasPrecision(18, 3);
            b.Property(x => x.SoldCount).HasPrecision(18, 3);

            b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.PromotionId }).HasDatabaseName("IX_ProFlashSaleSlots_TenantId_PromotionId");

            b.ToTable(t => t.HasCheckConstraint("CK_ProFlashSaleSlots_Slot",
                "\"SalePrice\" >= 0 AND \"SoldCount\" >= 0 AND \"SoldCount\" <= \"QuantityLimit\""));
        });

        builder.Entity<PromotionRedemption>(b =>
        {
            b.ToTable(BangRedemption);
            b.ConfigureByConvention();

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ReasonCode).HasMaxLength(PromotionConsts.ReasonCodeToiDa);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(PromotionConsts.IdempotencyKeyToiDa);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 4);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.BreakdownJson).IsRequired().HasColumnType("jsonb");

            b.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VoucherCode>().WithMany().HasForeignKey(x => x.VoucherCodeId).OnDelete(DeleteBehavior.Restrict);

            // ── Index chống trùng (một phần) — filter + UNIQUE; NND bổ sung bằng SQL thô ở migration ──
            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasDatabaseName(IxRedemptionTenantIdem);
            b.HasIndex(x => new { x.TenantId, x.PromotionId, x.OrderId, x.VoucherCodeId })
                .IsUnique()
                .HasFilter("\"VoucherCodeId\" IS NOT NULL AND \"OrderId\" IS NOT NULL")
                .HasDatabaseName(IxRedemptionVoucherDon);
            b.HasIndex(x => new { x.TenantId, x.PromotionId, x.CustomerId, x.UsageSequence })
                .IsUnique()
                .HasFilter("\"CustomerId\" IS NOT NULL AND \"Status\" <> 2")
                .HasDatabaseName(IxRedemptionKhachTran);
            b.HasIndex(x => new { x.TenantId, x.OrderId })
                .IsUnique()
                .HasFilter("\"OrderId\" IS NOT NULL AND \"VoucherCodeId\" IS NOT NULL")
                .HasDatabaseName(IxRedemptionMotVoucherDon);
            b.HasIndex(x => new { x.TenantId, x.PromotionId, x.OrderId })
                .IsUnique()
                .HasFilter("\"OrderId\" IS NOT NULL AND \"VoucherCodeId\" IS NULL")
                .HasDatabaseName(IxRedemptionMotTuDongDon);
            b.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAtUtc }).HasDatabaseName(IxRedemptionStatusExpires);

            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProPromotionRedemptions_UsageSequence", "\"UsageSequence\" >= 1");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_DiscountAmount", "\"DiscountAmount\" >= 0");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_ReservedExpires",
                    "\"Status\" <> 0 OR \"ExpiresAtUtc\" IS NOT NULL");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_AppliedAt", "\"Status\" <> 1 OR \"AppliedAtUtc\" IS NOT NULL");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_ReleasedAt", "\"Status\" <> 2 OR \"ReleasedAtUtc\" IS NOT NULL");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_ReversedAt", "\"Status\" <> 3 OR \"ReversedAtUtc\" IS NOT NULL");
                t.HasCheckConstraint("CK_ProPromotionRedemptions_ReasonCode",
                    "\"Status\" NOT IN (2, 3) OR \"ReasonCode\" IS NOT NULL");
                // `D-300` (`W25-2`) — miền `Status` (0..3) bằng `CHECK` ở DB.
                t.HasCheckConstraint("CK_ProPromotionRedemptions_Status", "\"Status\" BETWEEN 0 AND 3");
            });
        });
    }
}
