using eCommerce.PaymentHub.Payments;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.PaymentHub.EntityFrameworkCore;

public static class PaymentHubDbContextModelCreatingExtensions
{
    public static void ConfigurePaymentHub(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<PaymentTransaction>(b =>
        {
            b.ToTable("PayTransactions");
            b.ConfigureByConvention();

            b.Property(x => x.ProviderCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.MerchantReference).IsRequired().HasMaxLength(64);
            b.Property(x => x.ProviderTransactionId).HasMaxLength(128);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
            b.Property(x => x.IdempotencyKey).HasMaxLength(256);
            b.Property(x => x.FailureReason).HasMaxLength(2000);
            b.Property(x => x.LastWebhookPayload).HasColumnType("text");

            b.Property(x => x.Amount).HasPrecision(18, 4);
            b.Property(x => x.RefundedAmount).HasPrecision(18, 4);

            // Mã giao dịch phía ta không được trùng trong cùng tenant + cổng.
            // VNPay còn yêu cầu vnp_TxnRef không trùng trong NGÀY ⇒ nên sinh mã có ngày ở tiền tố.
            b.HasIndex(x => new { x.TenantId, x.ProviderCode, x.MerchantReference }).IsUnique();

            // 🎯 IDEMPOTENCY của webhook: cùng một giao dịch của cổng không thể ghi hai lần.
            b.HasIndex(x => new { x.TenantId, x.ProviderCode, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");

            b.HasIndex(x => new { x.OrderId });
        });
    }
}
