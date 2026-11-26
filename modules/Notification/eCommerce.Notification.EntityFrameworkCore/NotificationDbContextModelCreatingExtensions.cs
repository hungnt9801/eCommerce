using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Notification.EntityFrameworkCore;

/// <summary>
/// Mapping cho 5 bảng của `NOT-001` — gọi **MỘT LẦN** từ `eCommerceDbContext.OnModelCreating`
/// (`D-197`). 7 unique index có `NULLS NOT DISTINCT` được tạo bằng SQL thô ở migration
/// (EF Core không mô hình hoá thuộc tính này).
/// </summary>
public static class NotificationDbContextModelCreatingExtensions
{
    public const string BangTemplate = "NotificationTemplates";
    public const string BangVersion = "NotificationTemplateVersions";
    public const string BangBatch = "NotificationBatches";
    public const string BangQueueItem = "NotificationQueueItems";
    public const string BangQueueEvent = "NotificationQueueEvents";

    // 7 unique index — tên đích danh (≤ 63 byte) mà `PostgreSqlSchemaTests` kiểm.
    public const string IxTemplateTenantCodeChannelLocale = "IX_NotificationTemplates_Tenant_Code_Channel_Locale";
    public const string IxVersionTenantTemplateVersion = "IX_NotificationTemplateVersions_Tenant_Template_Version";
    public const string IxVersionTenantTemplateHash = "IX_NotificationTemplateVersions_Tenant_Template_Hash";
    public const string IxBatchTenantIdempotency = "IX_NotificationBatches_Tenant_IdempotencyKey";
    public const string IxBatchTenantCode = "IX_NotificationBatches_Tenant_Code";
    public const string IxItemTenantIdempotency = "IX_NotificationQueueItems_Tenant_IdempotencyKey";
    public const string IxItemTenantRecipientFingerprintBucket = "IX_NotificationQueueItems_Tenant_Recipient_Fingerprint_Bucket";

    public static void ConfigureNotification(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<NotificationTemplate>(b =>
        {
            b.ToTable(BangTemplate);
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(NotificationConsts.CodeToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(NotificationConsts.NameToiDa);
            b.Property(x => x.Locale).IsRequired().HasMaxLength(NotificationConsts.LocaleToiDa);
            b.Property(x => x.ChannelKind).HasConversion<int>();
            b.Property(x => x.Purpose).HasConversion<int>();
            b.Property(x => x.Status).HasConversion<int>();

            b.HasIndex(x => new { x.TenantId, x.Code, x.ChannelKind, x.Locale })
                .IsUnique()
                .HasDatabaseName(IxTemplateTenantCodeChannelLocale);

            b.HasIndex(x => new { x.TenantId, x.Status, x.ChannelKind })
                .HasDatabaseName("IX_NotificationTemplates_Tenant_Status_Channel");

            b.ToTable(t => t.HasCheckConstraint("CK_NotificationTemplates_ChannelKind", "\"ChannelKind\" in (0, 1, 2)"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationTemplates_Purpose", "\"Purpose\" in (0, 1, 2)"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationTemplates_CoalesceWindow", "\"CoalesceWindowSeconds\" >= 0"));
            // Bất biến ADR-12 ở tầng DB: chỉ Marketing mới được bật khử trùng.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_NotificationTemplates_ChiMarketingKhuTrung",
                "\"Purpose\" = 1 OR \"CoalesceWindowSeconds\" = 0"));
        });

        builder.Entity<NotificationTemplateVersion>(b =>
        {
            b.ToTable(BangVersion);
            b.ConfigureByConvention();

            b.Property(x => x.Subject).HasMaxLength(NotificationConsts.SubjectToiDa);
            b.Property(x => x.BodyText).IsRequired().HasColumnType("text");
            b.Property(x => x.BodyFormat).HasConversion<int>();

            // Whitelist placeholder dạng jsonb `[{name,kind,required}]`.
            b.Property(x => x.PlaceholdersJson).IsRequired().HasColumnName("Placeholders").HasColumnType("jsonb");

            b.Property(x => x.ContentHash).IsRequired().HasMaxLength(NotificationConsts.HashHexLength).IsFixedLength();

            // 🚨 `xmin` của PostgreSQL — concurrency token, KHÔNG tạo cột thật.
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasOne<NotificationTemplate>()
                .WithMany()
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.TemplateId, x.Version })
                .IsUnique()
                .HasDatabaseName(IxVersionTenantTemplateVersion);

            b.HasIndex(x => new { x.TenantId, x.TemplateId, x.ContentHash })
                .IsUnique()
                .HasDatabaseName(IxVersionTenantTemplateHash);

            b.HasIndex(x => new { x.TemplateId, x.Version })
                .IsDescending(false, true)
                .HasDatabaseName("IX_NotificationTemplateVersions_Template_Version");

            b.ToTable(t => t.HasCheckConstraint("CK_NotificationTemplateVersions_Version", "\"Version\" >= 1"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationTemplateVersions_BodyFormat", "\"BodyFormat\" in (0, 1)"));
        });

        builder.Entity<NotificationBatch>(b =>
        {
            b.ToTable(BangBatch);
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(NotificationConsts.CodeToiDa);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(NotificationConsts.IdempotencyKeyToiDa);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(NotificationConsts.CorrelationIdToiDa);
            b.Property(x => x.Status).HasConversion<int>();

            b.HasOne<NotificationTemplate>()
                .WithMany()
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<NotificationTemplateVersion>()
                .WithMany()
                .HasForeignKey(x => x.TemplateVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName(IxBatchTenantIdempotency);

            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName(IxBatchTenantCode);

            b.HasIndex(x => new { x.TenantId, x.Status, x.CreationTime })
                .HasDatabaseName("IX_NotificationBatches_Tenant_Status_CreationTime");

            b.ToTable(t => t.HasCheckConstraint("CK_NotificationBatches_TotalItems", "\"TotalItems\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationBatches_Status", "\"Status\" in (0, 1, 2, 3)"));
            b.ToTable(t => t.HasCheckConstraint(
                "CK_NotificationBatches_CoNguoiDuyet",
                "\"ApprovedAtUtc\" IS NULL OR \"ApprovedByUserId\" IS NOT NULL"));
        });

        builder.Entity<NotificationQueueItem>(b =>
        {
            b.ToTable(BangQueueItem);
            b.ConfigureByConvention();

            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(NotificationConsts.IdempotencyKeyToiDa);
            b.Property(x => x.SourceEventType).HasMaxLength(NotificationConsts.SourceEventTypeToiDa);
            b.Property(x => x.SourceAggregateId).HasMaxLength(NotificationConsts.SourceAggregateIdToiDa);
            b.Property(x => x.ChannelKind).HasConversion<int>();
            b.Property(x => x.Status).HasConversion<int>();

            b.Property(x => x.RecipientEncrypted).IsRequired().HasColumnType("text");
            b.Property(x => x.RecipientHash).IsRequired().HasMaxLength(NotificationConsts.HashHexLength).IsFixedLength();
            b.Property(x => x.RenderedSubject).HasMaxLength(NotificationConsts.SubjectToiDa);
            b.Property(x => x.RenderedBodyMasked).HasColumnType("text");
            b.Property(x => x.ContentFingerprint).IsRequired().HasMaxLength(NotificationConsts.HashHexLength).IsFixedLength();
            b.Property(x => x.LastErrorCode).HasMaxLength(NotificationConsts.ErrorCodeToiDa);
            b.Property(x => x.LastReasonMasked).HasMaxLength(NotificationConsts.ReasonToiDa);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(NotificationConsts.CorrelationIdToiDa);

            // 🚨 `xmin` của PostgreSQL.
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasOne<NotificationBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<NotificationTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<NotificationTemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);

            // 🚨 Hàng rào cuối chống gửi trùng (L2).
            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName(IxItemTenantIdempotency);

            // 🚨 Khử trùng cửa sổ (L5) — partial UNIQUE; migration thêm `NULLS NOT DISTINCT`.
            b.HasIndex(x => new { x.TenantId, x.RecipientHash, x.ChannelKind, x.ContentFingerprint, x.CoalesceBucketStartUtc })
                .IsUnique()
                .HasFilter("\"CoalesceBucketStartUtc\" IS NOT NULL")
                .HasDatabaseName(IxItemTenantRecipientFingerprintBucket);

            // Job lấy việc theo index (NFR-008).
            b.HasIndex(x => new { x.TenantId, x.Status, x.ScheduledAtUtc })
                .HasFilter("\"Status\" IN (0, 1, 2)")
                .HasDatabaseName("IX_NotificationQueueItems_Tenant_Status_Scheduled");

            b.HasIndex(x => new { x.TenantId, x.BatchId })
                .HasDatabaseName("IX_NotificationQueueItems_Tenant_Batch");

            b.HasIndex(x => new { x.TenantId, x.SourceEventType, x.SourceAggregateId })
                .HasDatabaseName("IX_NotificationQueueItems_Tenant_Source");

            b.HasIndex(x => new { x.TenantId, x.Status, x.DispatchLeaseExpiresAtUtc })
                .HasDatabaseName("IX_NotificationQueueItems_Tenant_Status_Lease");

            b.ToTable(t => t.HasCheckConstraint("CK_NotificationQueueItems_ChannelKind", "\"ChannelKind\" in (0, 1, 2)"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationQueueItems_Priority", "\"Priority\" in (0, 1, 2, 3)"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationQueueItems_AttemptCount", "\"AttemptCount\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_NotificationQueueItems_Status", "\"Status\" in (0, 1, 2, 3, 4, 5, 6, 7)"));
            b.ToTable(t => t.HasCheckConstraint(
                "CK_NotificationQueueItems_HanSauLich",
                "\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"ScheduledAtUtc\""));
        });

        builder.Entity<NotificationQueueEvent>(b =>
        {
            b.ToTable(BangQueueEvent);
            b.ConfigureByConvention();

            b.Property(x => x.EventType).HasConversion<int>();
            b.Property(x => x.FromStatus).HasConversion<int?>();
            b.Property(x => x.ToStatus).HasConversion<int?>();
            b.Property(x => x.ReasonMasked).HasMaxLength(NotificationConsts.ReasonToiDa);
            b.Property(x => x.ErrorCode).HasMaxLength(NotificationConsts.ErrorCodeToiDa);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(NotificationConsts.CorrelationIdToiDa);

            b.HasOne<NotificationQueueItem>().WithMany().HasForeignKey(x => x.QueueItemId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TenantId, x.QueueItemId, x.OccurredAtUtc })
                .HasDatabaseName("IX_NotificationQueueEvents_Tenant_Item_Time");

            b.HasIndex(x => new { x.TenantId, x.EventType, x.OccurredAtUtc })
                .HasDatabaseName("IX_NotificationQueueEvents_Tenant_Type_Time");

            b.ToTable(t => t.HasCheckConstraint("CK_NotificationQueueEvents_EventType", "\"EventType\" in (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)"));
            // `Enqueued` (0) không có trạng thái nguồn; event khác thì `FromStatus` NOT NULL.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_NotificationQueueEvents_FromStatus",
                "(\"EventType\" = 0 AND \"FromStatus\" IS NULL) OR (\"EventType\" <> 0 AND \"FromStatus\" IS NOT NULL)"));
        });
    }
}
