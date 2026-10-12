using eCommerce.ChannelHub.Channels;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.ChannelHub.EntityFrameworkCore;

/// <summary>
/// Cấu hình bảng + index cho entity của ChannelHub.
/// Gọi từ <c>eCommerceDbContext.OnModelCreating</c>: <c>builder.ConfigureChannelHub();</c>
/// </summary>
public static class ChannelHubDbContextModelCreatingExtensions
{
    public static void ConfigureChannelHub(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<Channel>(b =>
        {
            b.ToTable("ChnChannels");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);

            // Mã kênh là duy nhất toàn hệ thống (không theo tenant: đây là dữ liệu cấu hình).
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<ChannelShop>(b =>
        {
            b.ToTable("ChnChannelShops");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExternalShopId).IsRequired().HasMaxLength(64);
            b.Property(x => x.ShopName).IsRequired().HasMaxLength(256);
            b.Property(x => x.Region).HasMaxLength(16);

            // Token mã hoá có thể dài; dùng text thay vì varchar giới hạn.
            b.Property(x => x.AccessTokenEncrypted).HasColumnType("text");
            b.Property(x => x.RefreshTokenEncrypted).HasColumnType("text");

            b.Property(x => x.PushStatus).HasMaxLength(32);
            b.Property(x => x.LastError).HasMaxLength(2048);

            // Một shop trên một kênh chỉ được kết nối một lần trong một tenant.
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.ExternalShopId }).IsUnique();

            // Worker quét shop đang hoạt động theo index này.
            b.HasIndex(x => new { x.IsActive, x.ChannelCode });
        });

        builder.Entity<ChannelListing>(b =>
        {
            b.ToTable("ChnChannelListings");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.InternalSku).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExternalItemId).HasMaxLength(64);
            b.Property(x => x.ExternalSku).HasMaxLength(64);
            b.Property(x => x.LastSyncError).HasMaxLength(2048);

            // decimal(18,4): tiền tệ luôn là decimal, không bao giờ double.
            b.Property(x => x.ChannelPrice).HasPrecision(18, 4);

            // Một biến thể chỉ map một lần cho một shop.
            b.HasIndex(x => new { x.TenantId, x.ChannelShopId, x.ProductVariantId }).IsUnique();

            // Tra cứu ngược từ item của sàn về SKU nội bộ khi nhận webhook đơn hàng.
            b.HasIndex(x => new { x.ChannelShopId, x.ExternalItemId });
        });
    }
}
