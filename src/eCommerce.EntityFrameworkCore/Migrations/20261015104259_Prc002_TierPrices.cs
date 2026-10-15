using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Prc002_TierPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrcTierPriceSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PrcTierPriceSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrcTierPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TierPriceSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    AmountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_PrcTierPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrcTierPrices_PrcTierPriceSets_TierPriceSetId",
                        column: x => x.TierPriceSetId,
                        principalTable: "PrcTierPriceSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrcTierPrices_TenantId_SetId_Sku_MinQty",
                table: "PrcTierPrices",
                columns: new[] { "TenantId", "TierPriceSetId", "Sku", "MinQuantity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcTierPrices_TierPriceSetId",
                table: "PrcTierPrices",
                column: "TierPriceSetId");

            migrationBuilder.CreateIndex(
                name: "IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom",
                table: "PrcTierPriceSets",
                columns: new[] { "TenantId", "Code", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcTierPriceSets_TenantId_Status_Scope_ChannelCode",
                table: "PrcTierPriceSets",
                columns: new[] { "TenantId", "Status", "Scope", "ChannelCode" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** / `XC-004` **BR-009** cho mọi index mới).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`; PostgreSQL coi **NULL là DISTINCT**
            // ⇒ khi `TenantId = NULL` (hệ thống **1 tenant** hiện nay) thì **cả hai** unique index **mất tác dụng**:
            //   • hai bộ bậc thang cùng `(Code, EffectiveFrom)` **lọt**; và
            //   • 🚨 tệ hơn: **hai bậc cùng `MinQuantity`** trên cùng SKU **lọt** ⇒ `ChonBacThang` **NÉM `0008`**
            //     ⇒ **checkout CHẾT cho SKU đó**. Đây chính là hàng rào DB cho `PRC-002` **ADR-02**.
            migrationBuilder.Sql("DROP INDEX \"IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom\" ON \"PrcTierPriceSets\" " +
                "(\"TenantId\", \"Code\", \"EffectiveFrom\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_PrcTierPrices_TenantId_SetId_Sku_MinQty\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcTierPrices_TenantId_SetId_Sku_MinQty\" ON \"PrcTierPrices\" " +
                "(\"TenantId\", \"TierPriceSetId\", \"Sku\", \"MinQuantity\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrcTierPrices");

            migrationBuilder.DropTable(
                name: "PrcTierPriceSets");
        }
    }
}
