using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Customers.EntityFrameworkCore;

/// <summary>
/// Ánh xạ EF cho **`WP-07`** (khách mở rộng + cấp khách + chăm sóc + cấu hình tổ chức).
///
/// 📌 Tách khỏi `CustomersDbContextModelCreatingExtensions` (tệp `CUS-001` cũ) để **hai** giai đoạn tài liệu
/// không trộn vào một tệp 200 dòng — và để người đọc `WP-07` thấy đúng phần mình cần.
/// 🚨 **MỌI unique index có `TenantId` Ở ĐÂY đều phải là `NULLS NOT DISTINCT`** — EF **không** sinh được
/// mệnh đề đó, nên migration **viết tay** bằng `CREATE UNIQUE INDEX … NULLS NOT DISTINCT` (xem
/// `20261104…_Wp07_KhachHang_NhanVien`). 📌 Thiếu nó thì với `TenantId = NULL` (đang chạy **một** doanh
/// nghiệp) index **mất tác dụng im lặng** — đúng lỗi đã trả giá ở `XC-003` F1.
/// </summary>
public static class KhachHangWp07ModelCreating
{
    public static void ConfigureKhachHangWp07(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        // ── KHÁCH HÀNG: CỘT CỦA `WP-07` ───────────────────────────────────────────────────────────
        // 📌 Không khai lại bảng/thuộc tính cũ — chỉ bổ sung cột mới (migration **ADDITIVE**, `07 §8`).
        builder.Entity<Customer>(b =>
        {
            b.Property(x => x.Type).HasConversion<int>();

            // 🔑 Điểm: 3 số lẻ (khách mua hàng cân đo vẫn tích điểm lẻ — cùng lý do `D-31`).
            b.Property(x => x.Points).HasPrecision(18, 3);

            // 🔑 Tiền: 2 số lẻ (`decimal(18,2)`) — cùng quy ước `LifetimeValueAmount`/`StockDocumentLine.UnitPrice`.
            b.Property(x => x.AccumulatedAmount).HasPrecision(18, 2);
            b.Property(x => x.DebtLimit).HasPrecision(18, 2);
            b.Property(x => x.DebtBalance).HasPrecision(18, 2);

            b.Property(x => x.TaxCode).HasMaxLength(Customer.DoDaiMaSoThueToiDa);

            // 📌 `date` chứ không `timestamp`: ngày sinh không có giờ; lưu timestamp sẽ sinh lệch
            // *"hôm nay là sinh nhật không"* do múi giờ.
            b.Property(x => x.Birthday).HasColumnType("date");

            // 📌 Hai index phục vụ hai câu hỏi THẬT của màn DS khách (5 tab phân khúc):
            //   • "khách cấp X" (lọc phân khúc) ⇒ `(TenantId, LevelId)`;
            //   • "khách lẻ/buôn/đại lý" ⇒ `(TenantId, Type)`.
            b.HasIndex(x => new { x.TenantId, x.LevelId })
                .HasDatabaseName("IX_CusCustomers_TenantId_LevelId");

            b.HasIndex(x => new { x.TenantId, x.Type })
                .HasDatabaseName("IX_CusCustomers_TenantId_Type");

            // 🔒 Hàng rào DB: số dư âm là **không thể** (điểm/tiền tích luỹ/nợ/hạn mức).
            // 📌 Domain đã chặn; CHECK này chặn đường ghi tắt (SQL tay/import) — và biến một lỗi *"âm thầm"*
            // thành một lỗi **ồn ào** (đúng nguyên tắc: bất biến phải có ràng buộc DB).
            b.ToTable(t => t.HasCheckConstraint("CK_CusCustomers_SoDu_KhongAm",
                "\"Points\" >= 0 AND \"AccumulatedAmount\" >= 0 AND \"DebtBalance\" >= 0 AND \"DebtLimit\" >= 0"));
        });

        // ── CẤP KHÁCH ────────────────────────────────────────────────────────────────────────────
        builder.Entity<CustomerLevel>(b =>
        {
            b.ToTable("CusLevels");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(CustomerLevel.DoDaiMaToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(CustomerLevel.DoDaiTenToiDa);
            b.Property(x => x.Note).HasMaxLength(CustomerLevel.DoDaiGhiChuToiDa);
            b.Property(x => x.MinAccumulatedAmount).HasPrecision(18, 2);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);

            // 🚨 `NULLS NOT DISTINCT` — viết tay trong migration.
            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_CusLevels_TenantId_Code");

            // 📌 Chọn cấp đọc **mọi** cấp của tenant (bảng nhỏ) — index theo ngưỡng giúp`ORDER BY` tất định.
            b.HasIndex(x => new { x.TenantId, x.MinAccumulatedAmount })
                .HasDatabaseName("IX_CusLevels_TenantId_MinAccumulatedAmount");

            b.ToTable(t => t.HasCheckConstraint("CK_CusLevels_Nguong_KhongAm", "\"MinAccumulatedAmount\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_CusLevels_ChietKhau_0_100",
                "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100"));
        });

        // ── CHĂM SÓC KHÁCH ──────────────────────────────────────────────────────────────────────
        builder.Entity<CustomerCare>(b =>
        {
            b.ToTable("CusCares");
            b.ConfigureByConvention();

            b.Property(x => x.Content).IsRequired().HasMaxLength(CustomerCare.DoDaiNoiDungToiDa);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.OccurredAtUtc).IsRequired();

            // 🔑 Câu hỏi nóng nhất của màn chi tiết khách: *"đã chăm sóc gì, khi nào"* ⇒ index ghép
            // `(khách, lúc)` để trang chi tiết không quét cả bảng.
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.OccurredAtUtc })
                .HasDatabaseName("IX_CusCares_TenantId_CustomerId_OccurredAtUtc");

            // 🔑 Câu hỏi của quản lý: *"hôm nay ai cần gọi lại"* ⇒ index theo lịch hẹn (bỏ qua NULL).
            b.HasIndex(x => new { x.TenantId, x.NextFollowUpAtUtc })
                .HasFilter("\"NextFollowUpAtUtc\" IS NOT NULL")
                .HasDatabaseName("IX_CusCares_TenantId_NextFollowUpAtUtc");
        });

        // ── CỬA HÀNG ────────────────────────────────────────────────────────────────────────────
        builder.Entity<Store>(b =>
        {
            b.ToTable("SetStores");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(Store.DoDaiMaToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(Store.DoDaiTenToiDa);
            b.Property(x => x.AddressText).HasMaxLength(512);
            b.Property(x => x.Phone).HasMaxLength(32);

            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_SetStores_TenantId_Code");
        });

        // ── PHÒNG BAN ───────────────────────────────────────────────────────────────────────────
        builder.Entity<Department>(b =>
        {
            b.ToTable("SetDepartments");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(Department.DoDaiMaToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(Department.DoDaiTenToiDa);

            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_SetDepartments_TenantId_Code");

            b.HasIndex(x => new { x.TenantId, x.StoreId })
                .HasDatabaseName("IX_SetDepartments_TenantId_StoreId");
        });

        // ── NHÂN VIÊN ───────────────────────────────────────────────────────────────────────────
        builder.Entity<Staff>(b =>
        {
            b.ToTable("SetStaffs");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(Staff.DoDaiMaToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(Staff.DoDaiTenToiDa);
            b.Property(x => x.RoleName).HasMaxLength(RoleDataScope.DoDaiVaiTroToiDa);
            b.Property(x => x.Phone).HasMaxLength(32);
            b.Property(x => x.Note).HasMaxLength(1024);

            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_SetStaffs_TenantId_Code");

            // 🚨 MỘT TÀI KHOẢN ⇒ MỘT NHÂN VIÊN (xem chú thích `Staff`). Bộ lọc `IS NOT NULL` BẮT BUỘC:
            // nhân viên chưa cấp tài khoản là **đa số** lúc mới nhập danh mục.
            b.HasIndex(x => new { x.TenantId, x.IdentityUserId })
                .IsUnique()
                .HasFilter("\"IdentityUserId\" IS NOT NULL")
                .HasDatabaseName("IX_SetStaffs_TenantId_IdentityUserId");

            b.HasIndex(x => new { x.TenantId, x.StoreId })
                .HasDatabaseName("IX_SetStaffs_TenantId_StoreId");
        });

        // ── PHẠM VI DỮ LIỆU THEO VAI TRÒ ────────────────────────────────────────────────────────
        builder.Entity<RoleDataScope>(b =>
        {
            b.ToTable("SetRoleDataScopes");
            b.ConfigureByConvention();

            b.Property(x => x.RoleName).IsRequired().HasMaxLength(RoleDataScope.DoDaiVaiTroToiDa);
            b.Property(x => x.Scope).HasConversion<int>();

            b.HasIndex(x => new { x.TenantId, x.RoleName })
                .IsUnique()
                .HasDatabaseName("IX_SetRoleDataScopes_TenantId_RoleName");

            b.ToTable(t => t.HasCheckConstraint("CK_SetRoleDataScopes_PhamVi_HopLe",
                "\"Scope\" in (0, 1, 2)"));
        });
    }
}
