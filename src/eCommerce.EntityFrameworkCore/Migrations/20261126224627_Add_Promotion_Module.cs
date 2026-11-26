using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Promotion_Module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProPromotionRuleCosts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CostKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeclaredCostFactor = table.Column<int>(type: "integer", nullable: false),
                    RequiresVerifiedCod = table.Column<bool>(type: "boolean", nullable: false),
                    RetiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProPromotionRuleCosts", x => x.Id);
                    table.CheckConstraint("CK_ProPromotionRuleCosts_DeclaredCostFactor", "\"DeclaredCostFactor\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "ProPromotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsCumulative = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ChannelScope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TreeVersion = table.Column<int>(type: "integer", nullable: false),
                    MaxUsageTotal = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false),
                    MaxUsagePerCustomer = table.Column<int>(type: "integer", nullable: true),
                    DiscountKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
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
                    table.PrimaryKey("PK_ProPromotions", x => x.Id);
                    table.CheckConstraint("CK_ProPromotions_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_ProPromotions_DiscountKind_NotFreeShipping", "\"DiscountKind\" <> 'FreeShipping'");
                    table.CheckConstraint("CK_ProPromotions_DiscountValue", "\"DiscountValue\" >= 0");
                    table.CheckConstraint("CK_ProPromotions_Kind_NotFlashSale", "\"Kind\" <> 'FlashSale'");
                    table.CheckConstraint("CK_ProPromotions_Percentage", "\"DiscountKind\" <> 'Percentage' OR (\"DiscountValue\" <= 100 AND \"MaxDiscountAmount\" IS NOT NULL)");
                    table.CheckConstraint("CK_ProPromotions_Usage", "\"UsedCount\" >= 0 AND \"ReservedCount\" >= 0 AND (\"MaxUsageTotal\" IS NULL OR \"UsedCount\" + \"ReservedCount\" <= \"MaxUsageTotal\")");
                    table.CheckConstraint("CK_ProPromotions_Window", "\"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "ProFlashSaleSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    QuantityLimit = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    SoldCount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProFlashSaleSlots", x => x.Id);
                    table.CheckConstraint("CK_ProFlashSaleSlots_Slot", "\"SalePrice\" >= 0 AND \"SoldCount\" >= 0 AND \"SoldCount\" <= \"QuantityLimit\"");
                    table.ForeignKey(
                        name: "FK_ProFlashSaleSlots_ProPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "ProPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProPromotionConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    InteractionType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RuleSystemName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProPromotionConditions", x => x.Id);
                    table.CheckConstraint("CK_ProPromotionConditions_Depth", "\"Depth\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_ProPromotionConditions_InteractionType", "NOT \"IsGroup\" OR \"InteractionType\" IN ('And','Or')");
                    table.CheckConstraint("CK_ProPromotionConditions_NodeLai", "(\"IsGroup\" AND \"InteractionType\" IS NOT NULL AND \"RuleSystemName\" IS NULL) OR (NOT \"IsGroup\" AND \"InteractionType\" IS NULL AND \"RuleSystemName\" IS NOT NULL)");
                    table.CheckConstraint("CK_ProPromotionConditions_ParentKhacId", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
                    table.CheckConstraint("CK_ProPromotionConditions_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProPromotionConditions_ProPromotionConditions_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ProPromotionConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProPromotionConditions_ProPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "ProPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProPromotionMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProPromotionMappings", x => x.Id);
                    table.CheckConstraint("CK_ProPromotionMappings_TargetType", "\"TargetType\" NOT IN ('Sku','Category','Brand')");
                    table.ForeignKey(
                        name: "FK_ProPromotionMappings_ProPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "ProPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProVoucherCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaxUsage = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProVoucherCodes", x => x.Id);
                    table.CheckConstraint("CK_ProVoucherCodes_Usage", "\"UsedCount\" >= 0 AND \"ReservedCount\" >= 0 AND \"UsedCount\" + \"ReservedCount\" <= \"MaxUsage\"");
                    table.ForeignKey(
                        name: "FK_ProVoucherCodes_ProPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "ProPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProPromotionRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageSequence = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    BreakdownJson = table.Column<string>(type: "jsonb", nullable: false),
                    TreeVersion = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProPromotionRedemptions", x => x.Id);
                    table.CheckConstraint("CK_ProPromotionRedemptions_AppliedAt", "\"Status\" <> 1 OR \"AppliedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_ProPromotionRedemptions_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_ProPromotionRedemptions_DiscountAmount", "\"DiscountAmount\" >= 0");
                    table.CheckConstraint("CK_ProPromotionRedemptions_ReasonCode", "\"Status\" NOT IN (2, 3) OR \"ReasonCode\" IS NOT NULL");
                    table.CheckConstraint("CK_ProPromotionRedemptions_ReleasedAt", "\"Status\" <> 2 OR \"ReleasedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_ProPromotionRedemptions_ReservedExpires", "\"Status\" <> 0 OR \"ExpiresAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_ProPromotionRedemptions_ReversedAt", "\"Status\" <> 3 OR \"ReversedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_ProPromotionRedemptions_UsageSequence", "\"UsageSequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_ProPromotionRedemptions_ProPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "ProPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProPromotionRedemptions_ProVoucherCodes_VoucherCodeId",
                        column: x => x.VoucherCodeId,
                        principalTable: "ProVoucherCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProFlashSaleSlots_PromotionId",
                table: "ProFlashSaleSlots",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProFlashSaleSlots_TenantId_PromotionId",
                table: "ProFlashSaleSlots",
                columns: new[] { "TenantId", "PromotionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionConditions_MotGoc",
                table: "ProPromotionConditions",
                columns: new[] { "TenantId", "PromotionId" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionConditions_ParentId",
                table: "ProPromotionConditions",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionConditions_PromotionId",
                table: "ProPromotionConditions",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort",
                table: "ProPromotionConditions",
                columns: new[] { "TenantId", "PromotionId", "ParentId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionMappings_PromotionId",
                table: "ProPromotionMappings",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionMappings_TenantId_PromotionId",
                table: "ProPromotionMappings",
                columns: new[] { "TenantId", "PromotionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_PromotionId",
                table: "ProPromotionRedemptions",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_TenantId_IdempotencyKey",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_TenantId_OrderId_Voucher",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "OrderId" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL AND \"VoucherCodeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "PromotionId", "OrderId" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL AND \"VoucherCodeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_TenantId_Status_ExpiresAtUtc",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotionRedemptions_VoucherCodeId",
                table: "ProPromotionRedemptions",
                column: "VoucherCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromRedemptions_Tenant_Prom_Cust_Seq",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "PromotionId", "CustomerId", "UsageSequence" },
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL AND \"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromRedemptions_Tenant_Prom_Order_Voucher",
                table: "ProPromotionRedemptions",
                columns: new[] { "TenantId", "PromotionId", "OrderId", "VoucherCodeId" },
                unique: true,
                filter: "\"VoucherCodeId\" IS NOT NULL AND \"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotions_TenantId_Code",
                table: "ProPromotions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProPromotions_TenantId_Status_StartsAtUtc_EndsAtUtc",
                table: "ProPromotions",
                columns: new[] { "TenantId", "Status", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProVoucherCodes_PromotionId",
                table: "ProVoucherCodes",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProVoucherCodes_TenantId_Code",
                table: "ProVoucherCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 SQL THÔ — `NULLS NOT DISTINCT` + các partial unique chống trùng (`PRO-002-arch` §5.5).
            // EF Core KHÔNG mô hình hoá `NULLS NOT DISTINCT`; nếu để nguyên index EF sinh thì PostgreSQL coi
            // `NULL` là khác nhau ⇒ ràng buộc MẤT TÁC DỤNG IM LẶNG. Vì vậy DROP rồi CREATE lại theo khuôn
            // `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs`.
            // ⚠️ Nếu sau này EF sinh LẠI index (đổi model), nó sẽ **MẤT** thuộc tính NND — phải bổ sung lại
            // bằng SQL trong migration mới.
            // 🚨 **CẢ 4 index chống trùng `ProPromotionRedemptions` PHẢI `UNIQUE` + `NULLS NOT DISTINCT`**:
            // thiếu `UNIQUE` ⇒ hai dòng `(PromotionId, OrderId)` trùng được ghi ⇒ `UsedCount` +2 im lặng.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ProPromotions_TenantId_Code";
                CREATE UNIQUE INDEX "IX_ProPromotions_TenantId_Code"
                    ON "ProPromotions" ("TenantId", "Code") NULLS NOT DISTINCT;

                DROP INDEX IF EXISTS "IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort";
                CREATE UNIQUE INDEX "IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort"
                    ON "ProPromotionConditions" ("TenantId", "PromotionId", "ParentId", "SortOrder") NULLS NOT DISTINCT;

                DROP INDEX IF EXISTS "IX_ProPromotionConditions_MotGoc";
                CREATE UNIQUE INDEX "IX_ProPromotionConditions_MotGoc"
                    ON "ProPromotionConditions" ("TenantId", "PromotionId")
                    NULLS NOT DISTINCT
                    WHERE "ParentId" IS NULL;

                DROP INDEX IF EXISTS "IX_ProVoucherCodes_TenantId_Code";
                CREATE UNIQUE INDEX "IX_ProVoucherCodes_TenantId_Code"
                    ON "ProVoucherCodes" ("TenantId", "Code") NULLS NOT DISTINCT;

                DROP INDEX IF EXISTS "IX_ProPromotionRedemptions_TenantId_IdempotencyKey";
                CREATE UNIQUE INDEX "IX_ProPromotionRedemptions_TenantId_IdempotencyKey"
                    ON "ProPromotionRedemptions" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT;

                DROP INDEX IF EXISTS "IX_ProPromRedemptions_Tenant_Prom_Order_Voucher";
                CREATE UNIQUE INDEX "IX_ProPromRedemptions_Tenant_Prom_Order_Voucher"
                    ON "ProPromotionRedemptions" ("TenantId", "PromotionId", "OrderId", "VoucherCodeId")
                    NULLS NOT DISTINCT
                    WHERE "VoucherCodeId" IS NOT NULL AND "OrderId" IS NOT NULL;

                DROP INDEX IF EXISTS "IX_ProPromRedemptions_Tenant_Prom_Cust_Seq";
                CREATE UNIQUE INDEX "IX_ProPromRedemptions_Tenant_Prom_Cust_Seq"
                    ON "ProPromotionRedemptions" ("TenantId", "PromotionId", "CustomerId", "UsageSequence")
                    NULLS NOT DISTINCT
                    WHERE "CustomerId" IS NOT NULL AND "Status" <> 2;

                DROP INDEX IF EXISTS "IX_ProPromotionRedemptions_TenantId_OrderId_Voucher";
                CREATE UNIQUE INDEX "IX_ProPromotionRedemptions_TenantId_OrderId_Voucher"
                    ON "ProPromotionRedemptions" ("TenantId", "OrderId")
                    NULLS NOT DISTINCT
                    WHERE "OrderId" IS NOT NULL AND "VoucherCodeId" IS NOT NULL;

                DROP INDEX IF EXISTS "IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong";
                CREATE UNIQUE INDEX "IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong"
                    ON "ProPromotionRedemptions" ("TenantId", "PromotionId", "OrderId")
                    NULLS NOT DISTINCT
                    WHERE "OrderId" IS NOT NULL AND "VoucherCodeId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProFlashSaleSlots");

            migrationBuilder.DropTable(
                name: "ProPromotionConditions");

            migrationBuilder.DropTable(
                name: "ProPromotionMappings");

            migrationBuilder.DropTable(
                name: "ProPromotionRedemptions");

            migrationBuilder.DropTable(
                name: "ProPromotionRuleCosts");

            migrationBuilder.DropTable(
                name: "ProVoucherCodes");

            migrationBuilder.DropTable(
                name: "ProPromotions");
        }
    }
}
