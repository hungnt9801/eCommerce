using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Gd1S12_CostPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrcCostPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PrcCostPrices", x => x.Id);
                    table.CheckConstraint("CK_PrcCostPrices_Amount_KhongAm", "\"AmountValue\" >= 0");
                    table.CheckConstraint("CK_PrcCostPrices_Currency_HopLe", "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_PrcCostPrices_EffectiveRange", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrcCostPrices_TenantId_Sku_EffectiveFrom",
                table: "PrcCostPrices",
                columns: new[] { "TenantId", "Sku", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcCostPrices_TenantId_Sku_EffectiveTo",
                table: "PrcCostPrices",
                columns: new[] { "TenantId", "Sku", "EffectiveTo" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** + `XC-004` **BR-009**).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là
            // DISTINCT** ⇒ khi `TenantId = NULL` (ĐÚNG tình trạng hiện nay — hệ thống **1 tenant**), index
            // dưới đây **mất tác dụng IM LẶNG**:
            //   • hai dòng giá vốn cho **cùng một SKU, cùng một ngày bắt đầu** đều **lọt** vào DB;
            //   • ⇒ câu hỏi *"giá vốn của SKU này ngày 20/10 là bao nhiêu"* có **hai đáp án**;
            //   • ⇒ lãi gộp tính bằng con số **không tất định** (phụ thuộc thứ tự đọc của DB), và 📌
            //     **không có lỗi nào** để lần ra. Báo cáo lãi/lỗ vì thế **sai mà vẫn trông hợp lý**.
            // 📌 Đây đúng khuôn đã dùng cho `PrcPriceLists`/`PrcPriceListItems`/`PrcChannelPrices`
            // (`20261015101649_Pricing_PriceList_And_ChannelPrice`), và số index `NULLS NOT DISTINCT` được
            // `PostgreSqlSchemaTests` đếm lại (57 → **58**) ⇒ quên bước này là **ĐỎ cổng**, không phải im lặng.
            migrationBuilder.Sql("DROP INDEX \"IX_PrcCostPrices_TenantId_Sku_EffectiveFrom\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcCostPrices_TenantId_Sku_EffectiveFrom\" ON \"PrcCostPrices\" " +
                "(\"TenantId\", \"Sku\", \"EffectiveFrom\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrcCostPrices");
        }
    }
}
