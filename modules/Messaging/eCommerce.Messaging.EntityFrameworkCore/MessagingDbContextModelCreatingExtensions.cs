using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;
using eCommerce.Messaging.Idempotency;

namespace eCommerce.Messaging.EntityFrameworkCore;

/// <summary>
/// Cấu hình model cho module `Messaging` — được gọi từ **DbContext hợp nhất** (`eCommerceDbContext`).
/// </summary>
public static class MessagingDbContextModelCreatingExtensions
{
    /// <summary>Tên bảng vật lý (tiền tố <c>Msg</c> để phân biệt module trong một DB dùng chung).</summary>
    public const string TenBang = "MsgIdempotencyRecords";

    /// <summary>Tên index unique — migration thay bằng SQL thô để có `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUnique = "IX_MsgIdempotencyRecords_TenantId_Scope_KeyValue";

    public static void ConfigureMessaging(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable(TenBang);

            b.ConfigureByConvention();

            b.Property(x => x.Scope).IsRequired().HasMaxLength(IdempotencyKey.DoDaiScopeToiDa);
            b.Property(x => x.KeyValue).IsRequired().HasMaxLength(IdempotencyKey.DoDaiGiaTriToiDa);

            // SHA-256 hex = 64 ký tự.
            b.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.Result).HasColumnType("text");
            b.Property(x => x.ErrorCode).HasMaxLength(64);
            b.Property(x => x.ErrorMessage).HasMaxLength(1024);

            // 🔒 RÀNG BUỘC UNIQUE — hàng rào cuối chống trùng.
            // ⚠️ EF **không** diễn đạt được `NULLS NOT DISTINCT`, mà PostgreSQL mặc định coi NULL là
            // KHÁC NHAU ⇒ khi `TenantId` NULL thì ràng buộc **mất tác dụng** (đúng lỗi C4/F1 đã kiểm chứng
            // bằng chạy thật: `INSERT 0 2` cho hai dòng cùng khoá).
            // ⇒ Migration VẪN tạo index ở đây, nhưng bước sau của migration thay bằng SQL thô:
            //      CREATE UNIQUE INDEX ... NULLS NOT DISTINCT
            // Test tích hợp `PostgreSqlSchemaTests` kiểm **đích danh** index này để không bị mất.
            b.HasIndex(x => new { x.TenantId, x.Scope, x.KeyValue })
                .IsUnique()
                .HasDatabaseName(TenIndexUnique);

            // Tra cứu bản ghi hết hạn để dọn dẹp (job quét).
            b.HasIndex(x => x.ExpiresAtUtc)
                .HasDatabaseName("IX_MsgIdempotencyRecords_ExpiresAtUtc");

            // Hàng rào DB cho bất biến tối thiểu: đã nhận thì phải có ít nhất 1 lần thử.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_MsgIdempotencyRecords_Attempts",
                "\"Attempts\" >= 1"));
        });
    }
}
