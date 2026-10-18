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

            // 🚨 `S5` — **RÀNG BUỘC TIỀN Ở TẦNG DB** (`PAY-001` §5.3 + §11 #2). Trước `S5`, bất biến
            // *"tổng hoàn ≤ đã thu"* **chỉ** sống ở tầng domain (`RegisterRefund`); 📌 nghĩa là **đường ghi
            // tắt** (job/seed/SQL tay/migration sau này) **không bị chặn** — và đây là bất biến **TIỀN**, thứ
            // xứng đáng có hàng rào cuối. `CK_…_KhongAm` chặn luôn ca `RefundedAmount` âm (số âm **giảm** tổng
            // đã hoàn ⇒ **che mất** một lần hoàn đã xảy ra — xem chú thích `RegisterRefund`).
            b.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PayTransactions_RefundedAmount_KhongAm",
                    "\"RefundedAmount\" >= 0");

                t.HasCheckConstraint(
                    "CK_PayTransactions_RefundedAmount_KhongVuotAmount",
                    "\"RefundedAmount\" <= \"Amount\"");
            });
        });

        // 🚨 `S5` — **DÒNG HOÀN TIỀN** (`PAY-001` §5.6 #1): trả lời *"hoàn lúc nào, bao nhiêu, vì sao, ai duyệt"*
        // bằng **DỮ LIỆU** thay vì bằng **log**. 🔒 Tổng `RefundedAmount` một mình không trả lời được cả bốn câu.
        builder.Entity<PaymentRefund>(b =>
        {
            b.ToTable("PayRefunds");
            b.ConfigureByConvention();

            b.Property(x => x.RefundRequestId).IsRequired().HasMaxLength(PaymentRefund.DoDaiMaYeuCau);
            b.Property(x => x.Reason).IsRequired().HasMaxLength(PaymentRefund.DoDaiLyDo);
            b.Property(x => x.PerformedBy).IsRequired().HasMaxLength(PaymentRefund.DoDaiNguoiThucHien);
            b.Property(x => x.ProviderResponseCode).HasMaxLength(PaymentRefund.DoDaiMaPhanHoi);
            b.Property(x => x.ProviderRefundTransactionId).HasMaxLength(PaymentRefund.DoDaiMaGiaoDichHoan);
            b.Property(x => x.FailureReason).HasMaxLength(PaymentRefund.DoDaiLyDo);

            b.Property(x => x.Amount).HasPrecision(18, 4);

            // 🔒 **Chống ghi trùng MỘT lần hoàn** — khoá là *số thứ tự lần hoàn* (`RF001`, `RF002`…), **không**
            // phải `Guid` ngẫu nhiên (`Guid` làm index thành **trang trí**: mỗi dòng một giá trị mới ⇒ không
            // bao giờ trùng ⇒ mất tác dụng im lặng). 📌 `NULLS NOT DISTINCT` do migration viết **SQL thô**
            // (EF không mô hình hoá thuộc tính này — `PAY-001` §5.4).
            b.HasIndex(x => new { x.TenantId, x.PaymentTransactionId, x.RefundRequestId }).IsUnique();

            // Tra *"giao dịch này đã hoàn mấy lần"* + *"lần hoàn thứ n"* — không có index thì mỗi lần hoàn lại
            // quét cả bảng, và `RefundRequestId` được sinh từ **số lần đã hoàn** nên đường ghi phụ thuộc nó.
            b.HasIndex(x => new { x.PaymentTransactionId });

            b.ToTable(t => t.HasCheckConstraint("CK_PayRefunds_Amount_Duong", "\"Amount\" > 0"));
        });
    }
}
