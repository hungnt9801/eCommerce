using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp06_Pos_BanLe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosCashSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CashRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashRefund = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherExpense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HandoverCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Variance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_PosCashSessions", x => x.Id);
                    table.CheckConstraint("CK_PosCashSessions_KetCa_CoMoc", "\"Status\" <> 1 OR (\"ClosedAtUtc\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL)");
                    table.CheckConstraint("CK_PosCashSessions_Tien_KhongAm", "\"OpeningCash\" >= 0 AND \"CashRevenue\" >= 0 AND \"CashRefund\" >= 0 AND \"OtherExpense\" >= 0 AND \"ExpectedCash\" >= 0 AND \"HandoverCash\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PosRetailBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SaleStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineDiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerGivenAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PointsUsed = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PointsUsedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PointsEarned = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DebtAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StockDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VoidStockDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_PosRetailBills", x => x.Id);
                    table.CheckConstraint("CK_PosRetailBills_ChietKhau_MotTang", "NOT (\"DiscountTotal\" > 0 AND \"LineDiscountTotal\" > 0)");
                    table.CheckConstraint("CK_PosRetailBills_Huy_CoMoc", "\"Status\" <> 1 OR (\"VoidedAtUtc\" IS NOT NULL AND \"VoidReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_PosRetailBills_No_CoHan", "\"DebtAmount\" = 0 OR \"DueDateUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_PosRetailBills_Tien_KhongAm", "\"Subtotal\" >= 0 AND \"LineDiscountTotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"VatTotal\" >= 0 AND \"PointsUsed\" >= 0 AND \"PointsUsedAmount\" >= 0 AND \"PointsEarned\" >= 0 AND \"TotalPayment\" >= 0 AND \"PaidAmount\" >= 0 AND \"ChangeAmount\" >= 0 AND \"DebtAmount\" >= 0 AND \"CustomerGivenAmount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PosRetailReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RetailBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GoodsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DebtReductionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundMethod = table.Column<int>(type: "integer", nullable: false),
                    PointsReversed = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    StockDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_PosRetailReturns", x => x.Id);
                    table.CheckConstraint("CK_PosRetailReturns_Tien_CanBang", "\"GoodsAmount\" = \"DebtReductionAmount\" + \"RefundAmount\" + \"ReturnFeeAmount\"");
                    table.CheckConstraint("CK_PosRetailReturns_Tien_KhongAm", "\"GoodsAmount\" >= 0 AND \"ReturnFeeAmount\" >= 0 AND \"DebtReductionAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"PointsReversed\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PosRetailBillLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetailBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_PosRetailBillLines", x => x.Id);
                    table.CheckConstraint("CK_PosRetailBillLines_DaTra_HopLe", "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("CK_PosRetailBillLines_SoLuong_Duong", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_PosRetailBillLines_Tien_KhongAm", "\"UnitPrice\" >= 0 AND \"DiscountAmount\" >= 0 AND \"Amount\" >= 0 AND (\"VatAmount\" IS NULL OR \"VatAmount\" >= 0)");
                    table.ForeignKey(
                        name: "FK_PosRetailBillLines_PosRetailBills_RetailBillId",
                        column: x => x.RetailBillId,
                        principalTable: "PosRetailBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosRetailBillPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetailBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_PosRetailBillPayments", x => x.Id);
                    table.CheckConstraint("CK_PosRetailBillPayments_Tien_KhongAm", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_PosRetailBillPayments_PosRetailBills_RetailBillId",
                        column: x => x.RetailBillId,
                        principalTable: "PosRetailBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosRetailReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetailReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_PosRetailReturnLines", x => x.Id);
                    table.CheckConstraint("CK_PosRetailReturnLines_SoLuong_Duong", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_PosRetailReturnLines_Tien_KhongAm", "\"UnitPriceSnapshot\" >= 0 AND \"RefundAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_PosRetailReturnLines_PosRetailReturns_RetailReturnId",
                        column: x => x.RetailReturnId,
                        principalTable: "PosRetailReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 🚨 **`NULLS NOT DISTINCT` — VIẾT TAY, EF KHÔNG SINH ĐƯỢC** (bài học `XC-003` F1/C4).
            // 📌 PostgreSQL coi hai `NULL` là **KHÁC nhau** ⇒ với `TenantId = NULL` (đúng tình trạng 1 tenant
            // hiện nay) unique index **mất tác dụng IM LẶNG**:
            //   • `Number` trùng ⇒ hai ca cùng số;
            //   • `ClientBillId` trùng ⇒ **POS offline gửi lại tạo hoá đơn thứ hai** ⇒ khách bị tính tiền hai lần;
            //   • ca mở trùng ⇒ hai ca trên một quầy ⇒ `ExpectedCash` của cả hai đều sai.
            // ⇒ Đổi từ `CreateIndex` sang SQL thô (giữ **đúng tên** index như EF sinh, để `PostgreSqlSchemaTests`
            // đối chiếu đích danh được).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PosCashSessions_TenantId_Number\" "
                + "ON \"PosCashSessions\" (\"TenantId\", \"Number\") NULLS NOT DISTINCT;");

            // 🚨 **MỘT QUẦY MỘT CA MỞ** — unique **có bộ lọc** + `NULLS NOT DISTINCT`: EF không sinh được cả hai
            // cùng lúc ⇒ SQL thô. Thiếu nó thì hai ca mở đồng thời trên cùng quầy **không tách được tiền mặt**.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PosCashSessions_TenantId_WarehouseId_DangMo\" "
                + "ON \"PosCashSessions\" (\"TenantId\", \"WarehouseId\") NULLS NOT DISTINCT "
                + "WHERE \"Status\" = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_PosCashSessions_TenantId_WarehouseId_OpenedAtUtc",
                table: "PosCashSessions",
                columns: new[] { "TenantId", "WarehouseId", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillLines_RetailBillId",
                table: "PosRetailBillLines",
                column: "RetailBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillLines_TenantId_RetailBillId",
                table: "PosRetailBillLines",
                columns: new[] { "TenantId", "RetailBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillLines_TenantId_Sku",
                table: "PosRetailBillLines",
                columns: new[] { "TenantId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillPayments_RetailBillId",
                table: "PosRetailBillPayments",
                column: "RetailBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillPayments_TenantId_ReferenceCode",
                table: "PosRetailBillPayments",
                columns: new[] { "TenantId", "ReferenceCode" },
                filter: "\"ReferenceCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBillPayments_TenantId_RetailBillId",
                table: "PosRetailBillPayments",
                columns: new[] { "TenantId", "RetailBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBills_TenantId_CashSessionId",
                table: "PosRetailBills",
                columns: new[] { "TenantId", "CashSessionId" });

            // 🚨 **IDEMPOTENCY CỦA POS OFFLINE** — `NULLS NOT DISTINCT` viết tay (xem khối đầu phương thức).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PosRetailBills_TenantId_ClientBillId\" "
                + "ON \"PosRetailBills\" (\"TenantId\", \"ClientBillId\") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBills_TenantId_CustomerId_DebtAmount",
                table: "PosRetailBills",
                columns: new[] { "TenantId", "CustomerId", "DebtAmount" });

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PosRetailBills_TenantId_Number\" "
                + "ON \"PosRetailBills\" (\"TenantId\", \"Number\") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailBills_TenantId_WarehouseId_CreationTime",
                table: "PosRetailBills",
                columns: new[] { "TenantId", "WarehouseId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailReturnLines_RetailReturnId",
                table: "PosRetailReturnLines",
                column: "RetailReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailReturnLines_TenantId_RetailReturnId",
                table: "PosRetailReturnLines",
                columns: new[] { "TenantId", "RetailReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailReturns_TenantId_CashSessionId",
                table: "PosRetailReturns",
                columns: new[] { "TenantId", "CashSessionId" });

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PosRetailReturns_TenantId_Number\" "
                + "ON \"PosRetailReturns\" (\"TenantId\", \"Number\") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_PosRetailReturns_TenantId_RetailBillId",
                table: "PosRetailReturns",
                columns: new[] { "TenantId", "RetailBillId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosCashSessions");

            migrationBuilder.DropTable(
                name: "PosRetailBillLines");

            migrationBuilder.DropTable(
                name: "PosRetailBillPayments");

            migrationBuilder.DropTable(
                name: "PosRetailReturnLines");

            migrationBuilder.DropTable(
                name: "PosRetailBills");

            migrationBuilder.DropTable(
                name: "PosRetailReturns");
        }
    }
}
