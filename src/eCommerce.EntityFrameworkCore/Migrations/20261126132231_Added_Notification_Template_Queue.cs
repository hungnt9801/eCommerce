using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Added_Notification_Template_Queue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChannelKind = table.Column<int>(type: "integer", nullable: false),
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    CoalesceWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    DefaultExpiresAfterSeconds = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentPublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                    table.CheckConstraint("CK_NotificationTemplates_ChannelKind", "\"ChannelKind\" in (0, 1, 2)");
                    table.CheckConstraint("CK_NotificationTemplates_ChiMarketingKhuTrung", "\"Purpose\" = 1 OR \"CoalesceWindowSeconds\" = 0");
                    table.CheckConstraint("CK_NotificationTemplates_CoalesceWindow", "\"CoalesceWindowSeconds\" >= 0");
                    table.CheckConstraint("CK_NotificationTemplates_Purpose", "\"Purpose\" in (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodyFormat = table.Column<int>(type: "integer", nullable: false),
                    Placeholders = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplateVersions", x => x.Id);
                    table.CheckConstraint("CK_NotificationTemplateVersions_BodyFormat", "\"BodyFormat\" in (0, 1)");
                    table.CheckConstraint("CK_NotificationTemplateVersions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_NotificationTemplateVersions_NotificationTemplates_Template~",
                        column: x => x.TemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationBatches", x => x.Id);
                    table.CheckConstraint("CK_NotificationBatches_CoNguoiDuyet", "\"ApprovedAtUtc\" IS NULL OR \"ApprovedByUserId\" IS NOT NULL");
                    table.CheckConstraint("CK_NotificationBatches_Status", "\"Status\" in (0, 1, 2, 3)");
                    table.CheckConstraint("CK_NotificationBatches_TotalItems", "\"TotalItems\" >= 0");
                    table.ForeignKey(
                        name: "FK_NotificationBatches_NotificationTemplateVersions_TemplateVe~",
                        column: x => x.TemplateVersionId,
                        principalTable: "NotificationTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationBatches_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RootItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceEventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceAggregateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChannelKind = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEncrypted = table.Column<string>(type: "text", nullable: false),
                    RecipientHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RenderedSubject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RenderedBodyMasked = table.Column<string>(type: "text", nullable: true),
                    ContentFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CoalesceBucketStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchLeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastReasonMasked = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationQueueItems", x => x.Id);
                    table.CheckConstraint("CK_NotificationQueueItems_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_NotificationQueueItems_ChannelKind", "\"ChannelKind\" in (0, 1, 2)");
                    table.CheckConstraint("CK_NotificationQueueItems_HanSauLich", "\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"ScheduledAtUtc\"");
                    table.CheckConstraint("CK_NotificationQueueItems_Priority", "\"Priority\" in (0, 1, 2, 3)");
                    table.CheckConstraint("CK_NotificationQueueItems_Status", "\"Status\" in (0, 1, 2, 3, 4, 5, 6, 7)");
                    table.ForeignKey(
                        name: "FK_NotificationQueueItems_NotificationBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "NotificationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationQueueItems_NotificationTemplateVersions_Templat~",
                        column: x => x.TemplateVersionId,
                        principalTable: "NotificationTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationQueueItems_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationQueueEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    ReasonMasked = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationQueueEvents", x => x.Id);
                    table.CheckConstraint("CK_NotificationQueueEvents_EventType", "\"EventType\" in (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
                    table.CheckConstraint("CK_NotificationQueueEvents_FromStatus", "(\"EventType\" = 0 AND \"FromStatus\" IS NULL) OR (\"EventType\" <> 0 AND \"FromStatus\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NotificationQueueEvents_NotificationQueueItems_QueueItemId",
                        column: x => x.QueueItemId,
                        principalTable: "NotificationQueueItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationBatches_TemplateId",
                table: "NotificationBatches",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationBatches_TemplateVersionId",
                table: "NotificationBatches",
                column: "TemplateVersionId");

            // 🚨 2 UNIQUE của NotificationBatches tạo bằng SQL thô có `NULLS NOT DISTINCT` (xem cuối Up).
            migrationBuilder.CreateIndex(
                name: "IX_NotificationBatches_Tenant_Status_CreationTime",
                table: "NotificationBatches",
                columns: new[] { "TenantId", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueEvents_QueueItemId",
                table: "NotificationQueueEvents",
                column: "QueueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueEvents_Tenant_Item_Time",
                table: "NotificationQueueEvents",
                columns: new[] { "TenantId", "QueueItemId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueEvents_Tenant_Type_Time",
                table: "NotificationQueueEvents",
                columns: new[] { "TenantId", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_BatchId",
                table: "NotificationQueueItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_TemplateId",
                table: "NotificationQueueItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_TemplateVersionId",
                table: "NotificationQueueItems",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_Tenant_Batch",
                table: "NotificationQueueItems",
                columns: new[] { "TenantId", "BatchId" });

            // 🚨 2 UNIQUE của NotificationQueueItems tạo bằng SQL thô có `NULLS NOT DISTINCT` (xem cuối Up).
            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_Tenant_Source",
                table: "NotificationQueueItems",
                columns: new[] { "TenantId", "SourceEventType", "SourceAggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_Tenant_Status_Lease",
                table: "NotificationQueueItems",
                columns: new[] { "TenantId", "Status", "DispatchLeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueueItems_Tenant_Status_Scheduled",
                table: "NotificationQueueItems",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc" },
                filter: "\"Status\" IN (0, 1, 2)");

            // 🚨 UNIQUE của NotificationTemplates tạo bằng SQL thô có `NULLS NOT DISTINCT` (xem cuối Up).
            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Tenant_Status_Channel",
                table: "NotificationTemplates",
                columns: new[] { "TenantId", "Status", "ChannelKind" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplateVersions_Template_Version",
                table: "NotificationTemplateVersions",
                columns: new[] { "TemplateId", "Version" },
                descending: new[] { false, true });

            // 🚨 7 UNIQUE index có `NULLS NOT DISTINCT` (PostgreSQL coi NULL là KHÁC NHAU ⇒ mất ràng buộc
            // khi `TenantId = NULL` — lỗi C4/F1). EF Core không mô hình hoá thuộc tính này ⇒ SQL thô.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_NotificationTemplates_Tenant_Code_Channel_Locale"
                    ON "NotificationTemplates" ("TenantId", "Code", "ChannelKind", "Locale") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationTemplateVersions_Tenant_Template_Version"
                    ON "NotificationTemplateVersions" ("TenantId", "TemplateId", "Version") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationTemplateVersions_Tenant_Template_Hash"
                    ON "NotificationTemplateVersions" ("TenantId", "TemplateId", "ContentHash") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationBatches_Tenant_IdempotencyKey"
                    ON "NotificationBatches" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationBatches_Tenant_Code"
                    ON "NotificationBatches" ("TenantId", "Code") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationQueueItems_Tenant_IdempotencyKey"
                    ON "NotificationQueueItems" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX "IX_NotificationQueueItems_Tenant_Recipient_Fingerprint_Bucket"
                    ON "NotificationQueueItems" ("TenantId", "RecipientHash", "ChannelKind", "ContentFingerprint", "CoalesceBucketStartUtc")
                    NULLS NOT DISTINCT
                    WHERE "CoalesceBucketStartUtc" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationQueueEvents");

            migrationBuilder.DropTable(
                name: "NotificationQueueItems");

            migrationBuilder.DropTable(
                name: "NotificationBatches");

            migrationBuilder.DropTable(
                name: "NotificationTemplateVersions");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");
        }
    }
}
