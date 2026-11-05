using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Pos.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Pos`** (`WP-06`) — 5 bảng: hoá đơn · dòng · thanh toán · ca · trả hàng (+2 bảng dòng).
///
/// 🚨 **MỌI RÀNG BUỘC Ở ĐÂY LÀ HÀNG RÀO CUỐI, KHÔNG PHẢI "CHO ĐẸP":** domain đã kiểm trước, nhưng đường
/// ghi tắt (job, import Nhanh, SQL tay, đồng bộ offline sai) không đi qua domain. Thiếu `CHECK` thì
/// *"hoá đơn nợ 500k mà không có khách"* **vẫn lưu được** và **không có lỗi nào** để lần ra.
/// 📌 Mỗi `CHECK`/unique index **phải** được ánh xạ trong `ViPhamRangBuocDb` (hoặc khai báo có lý do ở
/// `Data/RangBuocChuaAnhXa.txt`) — nếu không `RangBuocDbChuaAnhXaTests` **ĐỎ**.
/// </summary>
public static class PosDbContextModelCreatingExtensions
{
    public static void ConfigurePos(this ModelBuilder builder)
    {
        // ══════════════════════════════════════════════════════════════════════════════════════════
        // HOÁ ĐƠN BÁN LẺ
        // ══════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RetailBill>(b =>
        {
            b.ToTable("PosRetailBills", t =>
            {
                // 🚨 Tiền KHÔNG được âm ở BẤT KỲ cột nào — âm nghĩa là "đã tính sai dấu", không có nghiệp vụ nào.
                t.HasCheckConstraint(
                    "CK_PosRetailBills_Tien_KhongAm",
                    "\"Subtotal\" >= 0 AND \"LineDiscountTotal\" >= 0 AND \"DiscountTotal\" >= 0 " +
                    "AND \"VatTotal\" >= 0 AND \"PointsUsed\" >= 0 AND \"PointsUsedAmount\" >= 0 " +
                    "AND \"PointsEarned\" >= 0 AND \"TotalPayment\" >= 0 AND \"PaidAmount\" >= 0 " +
                    "AND \"ChangeAmount\" >= 0 AND \"DebtAmount\" >= 0 AND \"CustomerGivenAmount\" >= 0");

                // 🚨 LUẬT NHANH (§5.1): chiết khấu dòng **HOẶC** tổng, KHÔNG cả hai.
                t.HasCheckConstraint(
                    "CK_PosRetailBills_ChietKhau_MotTang",
                    "NOT (\"DiscountTotal\" > 0 AND \"LineDiscountTotal\" > 0)");

                // 🚨 Nợ mà không có HẠN ⇒ khoản phải thu không đòi được.
                t.HasCheckConstraint(
                    "CK_PosRetailBills_No_CoHan",
                    "\"DebtAmount\" = 0 OR \"DueDateUtc\" IS NOT NULL");

                // 🚨 Huỷ phải có MỐC + LÝ DO (audit + đối chiếu két).
                t.HasCheckConstraint(
                    "CK_PosRetailBills_Huy_CoMoc",
                    "\"Status\" <> 1 OR (\"VoidedAtUtc\" IS NOT NULL AND \"VoidReason\" IS NOT NULL)");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Number).IsRequired().HasMaxLength(RetailBill.DoDaiSoHoaDonToiDa);
            b.Property(x => x.Note).HasMaxLength(RetailBill.DoDaiGhiChuToiDa);
            b.Property(x => x.VoidReason).HasMaxLength(RetailBill.DoDaiLyDoHuyToiDa);
            b.Property(x => x.Status).HasConversion<int>();

            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.LineDiscountTotal).HasPrecision(18, 2);
            b.Property(x => x.DiscountTotal).HasPrecision(18, 2);
            b.Property(x => x.VatTotal).HasPrecision(18, 2);
            b.Property(x => x.PointsUsed).HasPrecision(18, 3);
            b.Property(x => x.PointsUsedAmount).HasPrecision(18, 2);
            b.Property(x => x.PointsEarned).HasPrecision(18, 3);
            b.Property(x => x.TotalPayment).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.ChangeAmount).HasPrecision(18, 2);
            b.Property(x => x.DebtAmount).HasPrecision(18, 2);
            b.Property(x => x.CustomerGivenAmount).HasPrecision(18, 2);

            // 🚨 **IDEMPOTENCY của POS OFFLINE** (`D-42`, `03 §7`): mất mạng rồi gửi lại ⇒ **một** hoá đơn.
            // ⚠️ EF **không** sinh được `NULLS NOT DISTINCT` ⇒ migration sửa TAY (bài học `XC-003` F1/C4:
            // thiếu nó thì `TenantId = NULL` làm index **mất tác dụng im lặng** và khách bị tính tiền hai lần).
            b.HasIndex(x => new { x.TenantId, x.ClientBillId })
                .IsUnique()
                .HasDatabaseName("IX_PosRetailBills_TenantId_ClientBillId");

            b.HasIndex(x => new { x.TenantId, x.Number })
                .IsUnique()
                .HasDatabaseName("IX_PosRetailBills_TenantId_Number");

            // 📌 Truy vấn NÓNG: DS hoá đơn theo quầy/ngày (màn #13) và DS hoá đơn của MỘT ca (kết ca).
            b.HasIndex(x => new { x.TenantId, x.WarehouseId, x.CreationTime })
                .HasDatabaseName("IX_PosRetailBills_TenantId_WarehouseId_CreationTime");

            b.HasIndex(x => new { x.TenantId, x.CashSessionId })
                .HasDatabaseName("IX_PosRetailBills_TenantId_CashSessionId");

            // 📌 Công nợ quầy: *"khách này còn nợ bao nhiêu"* — `DebtAmount > 0` là tập **nhỏ** dần.
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.DebtAmount })
                .HasDatabaseName("IX_PosRetailBills_TenantId_CustomerId_DebtAmount");

            // 🚨 **NAVIGATION `Lines`/`Payments` BỊ `Ignore` CÓ CHỦ Ý — ĐÂY LÀ BÀI HỌC ĐÃ TRẢ GIÁ Ở `WP-06`:**
            // nếu để EF quản lý `HasMany(x => x.Lines)`, thì khi `InsertAsync(bill)` EF **cascade insert** các
            // dòng đã gắn vào navigation; app service sau đó `InsertManyAsync(dong)` **lần nữa** ⇒
            // `23505 PK_PosRetailBillLines` (probe đo được: hoá đơn **không lưu được** dù mọi test domain xanh).
            // ⇒ Dòng/thanh toán được ghi **tường minh** ở app service (cùng khuôn `OrderReturnLine` của `S14`);
            // FK vẫn do DB bảo đảm qua `HasOne<RetailBill>().WithMany()` trên entity con.
            b.Ignore(x => x.Lines);
            b.Ignore(x => x.Payments);
        });

        builder.Entity<RetailBillLine>(b =>
        {
            b.ToTable("PosRetailBillLines", t =>
            {
                t.HasCheckConstraint("CK_PosRetailBillLines_SoLuong_Duong", "\"Quantity\" > 0");

                // 🚨 HÀNG RÀO CUỐI chống TRẢ VƯỢT: domain (`CongTra`) kiểm trước, nhưng hai phiếu trả
                // **đồng thời** cùng đọc "chưa trả" ⇒ cả hai cộng ⇒ vượt. `CHECK` chặn được ca đó.
                t.HasCheckConstraint(
                    "CK_PosRetailBillLines_DaTra_HopLe",
                    "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");

                t.HasCheckConstraint(
                    "CK_PosRetailBillLines_Tien_KhongAm",
                    "\"UnitPrice\" >= 0 AND \"DiscountAmount\" >= 0 AND \"Amount\" >= 0 " +
                    "AND (\"VatAmount\" IS NULL OR \"VatAmount\" >= 0)");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(RetailBillLine.DoDaiSkuToiDa);
            b.Property(x => x.ProductName).HasMaxLength(256);
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.VatPercent).HasPrecision(18, 2);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.VatAmount).HasPrecision(18, 2);
            b.Property(x => x.ReturnedQuantity).HasPrecision(18, 3);

            // 📌 KHÔNG unique `(RetailBillId, Sku)`: bán cùng một SKU thành **nhiều dòng** (hai đơn giá,
            // hai lần quét) là hợp lệ — unique sẽ chặn oan và đẩy UI về phía cộng dồn sai.
            b.HasIndex(x => new { x.TenantId, x.RetailBillId })
                .HasDatabaseName("IX_PosRetailBillLines_TenantId_RetailBillId");

            b.HasIndex(x => new { x.TenantId, x.Sku })
                .HasDatabaseName("IX_PosRetailBillLines_TenantId_Sku");

            // 📌 FK tường minh tới hoá đơn (navigation trên aggregate bị `Ignore` — xem chú thích ở trên).
            b.HasOne<RetailBill>()
                .WithMany()
                .HasForeignKey(x => x.RetailBillId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RetailBillPayment>(b =>
        {
            b.ToTable("PosRetailBillPayments", t =>
            {
                t.HasCheckConstraint(
                    "CK_PosRetailBillPayments_Tien_KhongAm", "\"Amount\" >= 0");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Method).HasConversion<int>();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.ReferenceCode).HasMaxLength(RetailBillPayment.DoDaiMaThamChieuToiDa);
            b.Property(x => x.Note).HasMaxLength(256);

            b.HasIndex(x => new { x.TenantId, x.RetailBillId })
                .HasDatabaseName("IX_PosRetailBillPayments_TenantId_RetailBillId");

            // 📌 Đối soát chuyển khoản theo **mã giao dịch** — tra một mã ra hoá đơn (khi khách khiếu nại).
            b.HasIndex(x => new { x.TenantId, x.ReferenceCode })
                .HasFilter("\"ReferenceCode\" IS NOT NULL")
                .HasDatabaseName("IX_PosRetailBillPayments_TenantId_ReferenceCode");

            b.HasOne<RetailBill>()
                .WithMany()
                .HasForeignKey(x => x.RetailBillId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // CA THU NGÂN
        // ══════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<CashSession>(b =>
        {
            b.ToTable("PosCashSessions", t =>
            {
                t.HasCheckConstraint(
                    "CK_PosCashSessions_Tien_KhongAm",
                    "\"OpeningCash\" >= 0 AND \"CashRevenue\" >= 0 AND \"CashRefund\" >= 0 " +
                    "AND \"OtherExpense\" >= 0 AND \"ExpectedCash\" >= 0 AND \"HandoverCash\" >= 0");

                // 📌 `Variance` **được phép ÂM** (thiếu tiền) — cố ý không đưa vào CHECK trên.
                t.HasCheckConstraint(
                    "CK_PosCashSessions_KetCa_CoMoc",
                    "\"Status\" <> 1 OR (\"ClosedAtUtc\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL)");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Number).IsRequired().HasMaxLength(CashSession.DoDaiSoCaToiDa);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.Note).HasMaxLength(CashSession.DoDaiGhiChuToiDa);

            b.Property(x => x.OpeningCash).HasPrecision(18, 2);
            b.Property(x => x.CashRevenue).HasPrecision(18, 2);
            b.Property(x => x.CashRefund).HasPrecision(18, 2);
            b.Property(x => x.OtherExpense).HasPrecision(18, 2);
            b.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            b.Property(x => x.HandoverCash).HasPrecision(18, 2);
            b.Property(x => x.Variance).HasPrecision(18, 2);

            b.HasIndex(x => new { x.TenantId, x.Number })
                .IsUnique()
                .HasDatabaseName("IX_PosCashSessions_TenantId_Number");

            // 🚨 **MỘT QUẦY MỘT CA MỞ** — unique **có bộ lọc** (`Status = 0 Open`). Thiếu nó thì hai ca mở
            // đồng thời trên cùng quầy ⇒ tiền mặt của hai ca **không tách được** khi đếm két, và `ExpectedCash`
            // của cả hai đều sai. ⚠️ Migration sửa TAY (`NULLS NOT DISTINCT` + bộ lọc — EF không sinh được).
            b.HasIndex(x => new { x.TenantId, x.WarehouseId })
                .IsUnique()
                .HasFilter("\"Status\" = 0")
                .HasDatabaseName("IX_PosCashSessions_TenantId_WarehouseId_DangMo");

            b.HasIndex(x => new { x.TenantId, x.WarehouseId, x.OpenedAtUtc })
                .HasDatabaseName("IX_PosCashSessions_TenantId_WarehouseId_OpenedAtUtc");
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // TRẢ HÀNG
        // ══════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RetailReturn>(b =>
        {
            b.ToTable("PosRetailReturns", t =>
            {
                t.HasCheckConstraint(
                    "CK_PosRetailReturns_Tien_KhongAm",
                    "\"GoodsAmount\" >= 0 AND \"ReturnFeeAmount\" >= 0 AND \"DebtReductionAmount\" >= 0 " +
                    "AND \"RefundAmount\" >= 0 AND \"PointsReversed\" >= 0");

                // 🚨 Bất biến §5.4: `hàng trả = giảm nợ + hoàn tiền + phí` — sai một đồng là **hai sổ lệch nhau**
                // (kho nhập đủ hàng, tiền chỉ trả một phần) mà không có lỗi nào khi đọc.
                t.HasCheckConstraint(
                    "CK_PosRetailReturns_Tien_CanBang",
                    "\"GoodsAmount\" = \"DebtReductionAmount\" + \"RefundAmount\" + \"ReturnFeeAmount\"");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Number).IsRequired().HasMaxLength(RetailReturn.DoDaiSoPhieuToiDa);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.RefundMethod).HasConversion<int>();
            b.Property(x => x.Note).HasMaxLength(RetailReturn.DoDaiGhiChuToiDa);

            b.Property(x => x.GoodsAmount).HasPrecision(18, 2);
            b.Property(x => x.ReturnFeeAmount).HasPrecision(18, 2);
            b.Property(x => x.DebtReductionAmount).HasPrecision(18, 2);
            b.Property(x => x.RefundAmount).HasPrecision(18, 2);
            b.Property(x => x.PointsReversed).HasPrecision(18, 3);

            b.HasIndex(x => new { x.TenantId, x.Number })
                .IsUnique()
                .HasDatabaseName("IX_PosRetailReturns_TenantId_Number");

            b.HasIndex(x => new { x.TenantId, x.RetailBillId })
                .HasDatabaseName("IX_PosRetailReturns_TenantId_RetailBillId");

            b.HasIndex(x => new { x.TenantId, x.CashSessionId })
                .HasDatabaseName("IX_PosRetailReturns_TenantId_CashSessionId");

            // 📌 Cùng lý do với `RetailBill`: navigation bị `Ignore` để app service ghi dòng **tường minh**
            // (tránh cascade insert + `InsertManyAsync` ⇒ PK trùng).
            b.Ignore(x => x.Lines);
        });

        builder.Entity<RetailReturnLine>(b =>
        {
            b.ToTable("PosRetailReturnLines", t =>
            {
                t.HasCheckConstraint(
                    "CK_PosRetailReturnLines_SoLuong_Duong", "\"Quantity\" > 0");

                t.HasCheckConstraint(
                    "CK_PosRetailReturnLines_Tien_KhongAm",
                    "\"UnitPriceSnapshot\" >= 0 AND \"RefundAmount\" >= 0");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(RetailReturnLine.DoDaiSkuToiDa);
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
            b.Property(x => x.RefundAmount).HasPrecision(18, 2);

            b.HasIndex(x => new { x.TenantId, x.RetailReturnId })
                .HasDatabaseName("IX_PosRetailReturnLines_TenantId_RetailReturnId");

            b.HasOne<RetailReturn>()
                .WithMany()
                .HasForeignKey(x => x.RetailReturnId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
