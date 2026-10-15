using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Inv001_ReferenceLineId_Checks_Unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceLineId",
                table: "InvStockLedger",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger",
                columns: new[] { "TenantId", "ReferenceLineId" },
                unique: true,
                filter: "\"ReferenceLineId\" IS NOT NULL");

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** / `XC-004` **BR-009**).
            // EF KHÔNG sinh được `NULLS NOT DISTINCT` **kèm** bộ lọc `WHERE`. Thiếu nó thì khi `TenantId = NULL`
            // (hệ thống **1 tenant** hiện nay) PostgreSQL coi NULL là DISTINCT ⇒ **hàng rào chống ghi trùng
            // MẤT TÁC DỤNG** ⇒ ghi sổ hai lần ⇒ **tồn kho tăng gấp đôi** mà không có lỗi nào.
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockLedger_TenantId_ReferenceLineId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockLedger_TenantId_ReferenceLineId\" ON \"InvStockLedger\" " +
                "(\"TenantId\", \"ReferenceLineId\") NULLS NOT DISTINCT WHERE \"ReferenceLineId\" IS NOT NULL;");

            // ── `INV-001` ADR-03: **RÀNG BUỘC DB LÀ CHỐT CUỐI** cho tồn kho ───────────────────────────
            // 📌 Trước `INV-001`, kiểm âm kho **CHỈ** ở C# (`StockLedger.cs`) ⇒ mọi đường ghi **không** đi qua
            // entity (SQL tay, migration dữ liệu, sửa trực tiếp, bug ở tầng khác) đều **lọt**: `OnHand` âm nghĩa
            // là **bán thứ không có**; `Reserved > OnHand` nghĩa là `Available` **ÂM** ⇒ hệ thống tưởng còn hàng.
            // ⚠️ Kiểm tra trước khi thêm: dữ liệu hiện có **phải** đã thoả, nếu không migration **ĐỎ** (đúng ý —
            // thà đỏ lúc migrate còn hơn im lặng giữ dữ liệu sai).
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" ADD CONSTRAINT \"CK_InvItems_OnHand_KhongAm\" CHECK (\"OnHand\" >= 0);");
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" ADD CONSTRAINT \"CK_InvItems_Reserved_KhongAm\" CHECK (\"Reserved\" >= 0);");
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" ADD CONSTRAINT \"CK_InvItems_Reserved_KhongVuotOnHand\" CHECK (\"Reserved\" <= \"OnHand\");");
            migrationBuilder.Sql("ALTER TABLE \"InvStockLedger\" ADD CONSTRAINT \"CK_InvStockLedger_Balance_KhongAm\" CHECK (\"BalanceAfter\" >= 0);");
            // Bút toán **0** là nhiễu vô nghĩa nhưng vẫn chiếm một dòng sổ cái ⇒ cấm.
            migrationBuilder.Sql("ALTER TABLE \"InvStockLedger\" ADD CONSTRAINT \"CK_InvStockLedger_Delta_KhacKhong\" CHECK (\"QuantityDelta\" <> 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"InvStockLedger\" DROP CONSTRAINT IF EXISTS \"CK_InvStockLedger_Delta_KhacKhong\";");
            migrationBuilder.Sql("ALTER TABLE \"InvStockLedger\" DROP CONSTRAINT IF EXISTS \"CK_InvStockLedger_Balance_KhongAm\";");
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" DROP CONSTRAINT IF EXISTS \"CK_InvItems_Reserved_KhongVuotOnHand\";");
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" DROP CONSTRAINT IF EXISTS \"CK_InvItems_Reserved_KhongAm\";");
            migrationBuilder.Sql("ALTER TABLE \"InvItems\" DROP CONSTRAINT IF EXISTS \"CK_InvItems_OnHand_KhongAm\";");

            migrationBuilder.DropIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger");

            migrationBuilder.DropColumn(
                name: "ReferenceLineId",
                table: "InvStockLedger");
        }
    }
}
