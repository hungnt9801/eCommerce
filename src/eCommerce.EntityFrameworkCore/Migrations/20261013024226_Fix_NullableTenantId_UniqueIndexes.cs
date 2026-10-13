using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <summary>
    /// 🚨 SỬA LỖI TOÀN VẸN DỮ LIỆU — phát hiện **F1** của tài liệu `XC-004`.
    ///
    /// VẤN ĐỀ: mọi unique index dạng `("TenantId", …)` do EF sinh bằng `unique: true` **thường**.
    /// PostgreSQL mặc định là **NULLS DISTINCT** ⇒ hai dòng có `TenantId IS NULL` và cùng khoá
    /// được coi là **khác nhau** ⇒ **index KHÔNG chặn trùng**.
    ///
    /// ĐÃ CHỨNG MINH BẰNG POSTGRESQL THẬT (13/10/2026):
    ///   • chèn 2 dòng `TaxRateSchedules` cùng `Code='DUP_TEST'`, `TenantId = NULL` ⇒ **INSERT 0 2 — THÀNH CÔNG**
    ///   • chèn 2 dòng cùng `Code` với `TenantId` **có giá trị** ⇒ **BỊ CHẶN** (`duplicate key value violates unique constraint`)
    /// ⇒ ở chế độ 1 tenant (`TenantId = NULL`) ta có thể tạo **trùng `Code` / `Sku` / `Number` / `MerchantReference`**.
    ///
    /// CÁCH SỬA: tạo lại index với **`NULLS NOT DISTINCT`** (PostgreSQL **15+**; dự án dùng **17**).
    /// EF Core **không** mô hình hoá thuộc tính này ⇒ phải dùng **SQL thô**
    /// (giống ràng buộc `EXCLUDE` trong `Add_Tax_Module`).
    ///
    /// ⚠️ RỦI RO CẦN BIẾT: vì EF không mô hình hoá `NULLS NOT DISTINCT`, nếu một migration **do EF sinh
    /// tự động** sau này tạo lại một trong các index dưới đây, thuộc tính sẽ **mất** ⇒ migration đó phải
    /// chạy lại SQL này. Đã ghi chú ở `XC-004` §5.2.
    ///
    /// 📌 KHÔNG sửa 2 index của bảng ABP (`AbpPermissionGrants`, `AbpResourcePermissionGrants`):
    /// schema đó do ABP sở hữu, migration ABP sau này có thể tạo lại; thêm nữa grant trùng cho cùng
    /// vai trò **không** gây sai dữ liệu nghiệp vụ. Đã ghi nhận trong `XC-004`.
    ///
    /// 📌 Migration này do EF sinh (để có `.Designer.cs` cung cấp `TargetModel` — **thiếu nó EF sẽ không
    /// nhận diện migration**; đã thử viết tay và bị bỏ qua hoàn toàn), sau đó phần `Up`/`Down` được thay bằng SQL thô.
    /// </summary>
    public partial class Fix_NullableTenantId_UniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "IX_CatProducts_TenantId_Code";
                CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Code"
                    ON "CatProducts" ("TenantId", "Code") NULLS NOT DISTINCT;

                DROP INDEX "IX_CatProductVariants_TenantId_Sku";
                CREATE UNIQUE INDEX "IX_CatProductVariants_TenantId_Sku"
                    ON "CatProductVariants" ("TenantId", "Sku") NULLS NOT DISTINCT;

                DROP INDEX "IX_InvItems_TenantId_WarehouseId_Sku";
                CREATE UNIQUE INDEX "IX_InvItems_TenantId_WarehouseId_Sku"
                    ON "InvItems" ("TenantId", "WarehouseId", "Sku") NULLS NOT DISTINCT;

                DROP INDEX "IX_OrdOrders_TenantId_Number";
                CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_Number"
                    ON "OrdOrders" ("TenantId", "Number") NULLS NOT DISTINCT;

                DROP INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId";
                CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId"
                    ON "OrdOrders" ("TenantId", "ChannelCode", "ExternalOrderId") NULLS NOT DISTINCT
                    WHERE ("ExternalOrderId" IS NOT NULL);

                DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference";
                CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference"
                    ON "PayTransactions" ("TenantId", "ProviderCode", "MerchantReference") NULLS NOT DISTINCT;

                DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey";
                CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey"
                    ON "PayTransactions" ("TenantId", "ProviderCode", "IdempotencyKey") NULLS NOT DISTINCT
                    WHERE ("IdempotencyKey" IS NOT NULL);

                DROP INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId";
                CREATE UNIQUE INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId"
                    ON "ChnChannelShops" ("TenantId", "ChannelCode", "ExternalShopId") NULLS NOT DISTINCT;

                DROP INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId";
                CREATE UNIQUE INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId"
                    ON "ChnChannelListings" ("TenantId", "ChannelShopId", "ProductVariantId") NULLS NOT DISTINCT;

                DROP INDEX "IX_TaxRateSchedules_TenantId_Code";
                CREATE UNIQUE INDEX "IX_TaxRateSchedules_TenantId_Code"
                    ON "TaxRateSchedules" ("TenantId", "Code") NULLS NOT DISTINCT;

                -- ⚠️ Tên index này bị EF CẮT ở 63 ký tự và kết thúc bằng '~' — đúng tên thật trong DB.
                DROP INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~";
                CREATE UNIQUE INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~"
                    ON "TaxClassifications" ("TenantId", "TargetType", "TargetId", "EffectiveFrom") NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "IX_CatProducts_TenantId_Code";
                CREATE UNIQUE INDEX "IX_CatProducts_TenantId_Code"
                    ON "CatProducts" ("TenantId", "Code");

                DROP INDEX "IX_CatProductVariants_TenantId_Sku";
                CREATE UNIQUE INDEX "IX_CatProductVariants_TenantId_Sku"
                    ON "CatProductVariants" ("TenantId", "Sku");

                DROP INDEX "IX_InvItems_TenantId_WarehouseId_Sku";
                CREATE UNIQUE INDEX "IX_InvItems_TenantId_WarehouseId_Sku"
                    ON "InvItems" ("TenantId", "WarehouseId", "Sku");

                DROP INDEX "IX_OrdOrders_TenantId_Number";
                CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_Number"
                    ON "OrdOrders" ("TenantId", "Number");

                DROP INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId";
                CREATE UNIQUE INDEX "IX_OrdOrders_TenantId_ChannelCode_ExternalOrderId"
                    ON "OrdOrders" ("TenantId", "ChannelCode", "ExternalOrderId")
                    WHERE ("ExternalOrderId" IS NOT NULL);

                DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference";
                CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference"
                    ON "PayTransactions" ("TenantId", "ProviderCode", "MerchantReference");

                DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey";
                CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey"
                    ON "PayTransactions" ("TenantId", "ProviderCode", "IdempotencyKey")
                    WHERE ("IdempotencyKey" IS NOT NULL);

                DROP INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId";
                CREATE UNIQUE INDEX "IX_ChnChannelShops_TenantId_ChannelCode_ExternalShopId"
                    ON "ChnChannelShops" ("TenantId", "ChannelCode", "ExternalShopId");

                DROP INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId";
                CREATE UNIQUE INDEX "IX_ChnChannelListings_TenantId_ChannelShopId_ProductVariantId"
                    ON "ChnChannelListings" ("TenantId", "ChannelShopId", "ProductVariantId");

                DROP INDEX "IX_TaxRateSchedules_TenantId_Code";
                CREATE UNIQUE INDEX "IX_TaxRateSchedules_TenantId_Code"
                    ON "TaxRateSchedules" ("TenantId", "Code");

                DROP INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~";
                CREATE UNIQUE INDEX "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~"
                    ON "TaxClassifications" ("TenantId", "TargetType", "TargetId", "EffectiveFrom");
                """);
        }
    }
}
