using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Pricing.EntityFrameworkCore;

/// <summary>
/// Mapping của module **Pricing** (`PRC-001` §5.2 + `PRC-002` §5.2) — gọi từ `eCommerceDbContext`.
///
/// 🚨 **BA ĐIỀU BẮT BUỘC Ở ĐÂY:**
///   1. **Mọi unique index có `TenantId` PHẢI là `NULLS NOT DISTINCT`** (`XC-004` BR-009 / `PRC-001` BR-011).
///      EF **không** sinh được ⇒ **migration phải sửa tay** (SQL thô), và test tích hợp đếm lại số index.
///   2. **Tên index đặt TƯỜNG MINH**: tên EF tự sinh dài > 63 ký tự sẽ bị PostgreSQL **cắt cụt** (lỗi đã gặp thật).
///   3. **`decimal(18,4)`** cho tiền — khớp `OrdOrderLines` và `CatProductVariants` (nhất quán toàn hệ thống).
/// </summary>
public static class PricingDbContextModelCreatingExtensions
{
    public static void ConfigurePricing(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<PriceList>(b =>
        {
            b.ToTable("PrcPriceLists");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ChannelCode).HasMaxLength(64);
            b.Property(x => x.Scope).HasConversion<int>();
            b.Property(x => x.Status).HasConversion<int>();

            // Tiền tệ lưu bằng **mã ISO** (`VND`) — cột có thật, KHÔNG suy ra từ nơi khác (BR-001).
            b.Property(x => x.Currency).HasConversion(c => c.Code, s => CurrencyTuMa(s)).IsRequired().HasMaxLength(8);

            b.Ignore(x => x.SoDong);

            // `PRC-001` **D8**: chống chồng khung Ở TẦNG ỨNG DỤNG + unique `(TenantId, Code, EffectiveFrom)`;
            // **cố ý KHÔNG** dùng `EXCLUDE USING gist` (nó chỉ chặn được chồng khung **trong cùng `Code`** —
            // hai `Code` khác nhau cùng phạm vi vẫn chồng ⇒ cảm giác an toàn giả).
            b.HasIndex(x => new { x.TenantId, x.Code, x.EffectiveFrom })
                .IsUnique()
                .HasDatabaseName("IX_PrcPriceLists_TenantId_Code_EffectiveFrom");

            b.HasIndex(x => new { x.TenantId, x.Status, x.Scope, x.ChannelCode })
                .HasDatabaseName("IX_PrcPriceLists_TenantId_Status_Scope_ChannelCode");
        });

        builder.Entity<PriceListItem>(b =>
        {
            b.ToTable("PrcPriceListItems");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // 🚨 Cột tiền + cột tiền tệ đi KÈM (PRC-001 BR-001) — không bao giờ chỉ có `decimal` trần.
            b.Property(x => x.AmountValue).HasPrecision(18, 4);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
            b.Ignore(x => x.Amount);
            b.Ignore(x => x.CompareAtAmount);

            // `PRC-001` **BR-008**: một SKU chỉ **một** dòng trong một bảng giá (đây là lý do bảng riêng cho bậc thang — `PRC-002` ADR-01).
            b.HasIndex(x => new { x.TenantId, x.PriceListId, x.Sku })
                .IsUnique()
                .HasDatabaseName("IX_PrcPriceListItems_TenantId_PriceListId_Sku");

            b.HasOne<PriceList>()
                .WithMany()
                .HasForeignKey(x => x.PriceListId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChannelPrice>(b =>
        {
            b.ToTable("PrcChannelPrices");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // 🚨 Cột tiền + cột tiền tệ đi KÈM (PRC-001 BR-001).
            b.Property(x => x.AmountValue).HasPrecision(18, 4);
            b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
            b.Ignore(x => x.Amount);

            // `PRC-001` **BR-008**: duy nhất theo `(TenantId, ChannelCode, ChannelShopId, Sku, MinQuantity)`.
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.ChannelShopId, x.Sku, x.MinQuantity })
                .IsUnique()
                .HasDatabaseName("IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty");

            b.HasIndex(x => new { x.TenantId, x.Sku, x.ChannelCode })
                .HasDatabaseName("IX_PrcChannelPrices_TenantId_Sku_ChannelCode");
        });
    }

    /// <summary>
    /// Chuyển mã ISO trong DB về `Currency`. ⚠️ Mã **lạ** ⇒ **NÉM** (không im lặng rơi về VND):
    /// rơi về VND nghĩa là một dòng giá USD bị đọc thành VND ⇒ **sai 25.000 lần** mà không có lỗi nào.
    /// </summary>
    private static Currency CurrencyTuMa(string ma)
    {
        if (Currency.TryFromCode(ma, out var c) && c is not null)
        {
            return c;
        }

        throw new BusinessException(PricingErrorCodes.LechLoaiTien)
            .WithData("ma", ma)
            .WithData("lyDo", "Mã tiền tệ trong DB không được đăng ký trong `Currency`.");
    }
}
