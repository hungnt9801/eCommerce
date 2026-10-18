using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Customers.EntityFrameworkCore;

/// <summary>
/// Ánh xạ EF Core của module **Customers** (`CUS-001`) — gọi từ DbContext hợp nhất.
/// 📌 Giữ ở **module** (không viết trong `eCommerceDbContext`) theo quy ước: mỗi module tự cung cấp hàm mapping.
/// </summary>
public static class CustomersDbContextModelCreatingExtensions
{
    public static void ConfigureCustomers(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        // ── NHÓM KHÁCH ──────────────────────────────────────────────────────────────────────────
        builder.Entity<CustomerGroup>(b =>
        {
            b.ToTable("CusGroups");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Description).HasMaxLength(512);

            // 🚨 Unique index CÓ `TenantId` ⇒ **BẮT BUỘC** `NULLS NOT DISTINCT` (SQL thô trong migration).
            // Thiếu nó: hệ thống chạy **1 tenant** (`TenantId = NULL`) ⇒ PostgreSQL coi NULL là KHÁC NHAU
            // ⇒ hai nhóm cùng `Code` **LỌT** ⇒ bảng giá (`PRC-001` Q5) nhận hai "nhóm VIP" khác nhau.
            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_CusGroups_TenantId_Code");
        });

        // ── KHÁCH HÀNG ──────────────────────────────────────────────────────────────────────────
        builder.Entity<Customer>(b =>
        {
            b.ToTable("CusCustomers");
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(Customer.DoDaiTenToiDa);
            b.Property(x => x.Email).HasMaxLength(Customer.DoDaiEmailToiDa);
            b.Property(x => x.Phone).HasMaxLength(Customer.DoDaiDienThoaiToiDa);
            b.Property(x => x.IdempotencyKey).HasMaxLength(Customer.DoDaiKhoaIdempotencyToiDa);
            b.Property(x => x.Note).HasMaxLength(1024);

            // 🔒 `Status` lưu **số** (đọc bằng mắt trong DB là số ⇒ ghi rõ để không ai "sửa tay" sai).
            b.Property(x => x.Status).HasConversion<int>();

            // 📌 `LifetimeValue` là **CỘT RỜI** (`ADR-10`): nullable complex type vướng bug EF #37559
            // ⇒ `XC-001` đã chốt cột rời trên EF 10 cho MỌI miền có tiền.
            b.Property(x => x.LifetimeValueAmount).HasPrecision(18, 2);
            b.Property(x => x.LifetimeValueCurrency).HasMaxLength(3);

            // 🚨 **KHOÁ IDEMPOTENCY** (`ADR-05`) — chống **tạo hai khách cho một lần gọi** khi retry.
            // Có `TenantId` ⇒ **BẮT BUỘC** `NULLS NOT DISTINCT` (SQL thô): thiếu nó thì `IdempotencyKey = NULL`
            // của **mọi** khách "không dùng idempotency" bị coi là khác nhau — điều đó ĐÚNG về mặt dữ liệu,
            // nhưng bộ lọc `IS NOT NULL` là thứ quyết định, xem migration.
            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("IX_CusCustomers_TenantId_IdempotencyKey");

            // 🚨🚨 **BẤT BIẾN `ADR-07`: MỘT TÀI KHOẢN ⇒ TỐI ĐA MỘT HỒ SƠ KHÁCH.**
            // Nếu KHÔNG có ràng buộc này: hai hồ sơ khách cùng trỏ một `IdentityUserId` ⇒
            // *"khách này là ai"* có **HAI câu trả lời**, và lịch sử mua/điểm thưởng **phân mảnh**
            // theo hồ sơ nào được đọc trước — **không lỗi nào nổi lên** (`XC-003` F1).
            // ⚠️ Bộ lọc `IS NOT NULL` BẮT BUỘC (khách vãng lai có `IdentityUserId = NULL` — **đa số**),
            // và cần **SQL thô** vì EF không sinh được `NULLS NOT DISTINCT` **kèm** bộ lọc.
            b.HasIndex(x => new { x.TenantId, x.IdentityUserId })
                .IsUnique()
                .HasFilter("\"IdentityUserId\" IS NOT NULL")
                .HasDatabaseName("IX_CusCustomers_TenantId_IdentityUserId");

            // 📌 Index tra cứu cho CSKH (`ChoPhepGiaoDichAsync` tra theo Id — đã có PK; tra theo liên hệ
            // là `Contains` nên **không** dùng index này; giữ index theo `Status` để lọc danh sách bị chặn).
            b.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("IX_CusCustomers_TenantId_Status");
        });

        // ── SỔ ĐỊA CHỈ CỦA KHÁCH (`ADDR-004` phần B) ──────────────────────────────────────────────
        // 🚨 `AddressId` là **tham chiếu MỀM** (KHÔNG FK): `Address` thuộc module `Address`.
        // 🔒 HAI unique index dưới đây **phải** có `NULLS NOT DISTINCT` ⇒ migration thay bằng SQL thô
        //    (EF không mô hình hoá được mệnh đề này). Tên index đặt TƯỜNG MINH vì tên EF tự sinh dài
        //    quá 63 ký tự sẽ bị PostgreSQL **CẮT** (`PostgreSqlSchemaTests` ghi lỗi này ở `:185`).
        builder.Entity<CustomerAddressLink>(b =>
        {
            b.ToTable("CusAddressLinks");
            b.ConfigureByConvention();

            b.Property(x => x.Label).HasMaxLength(CustomerAddressLink.DoDaiNhanToiDa);
            b.Property(x => x.AddressType).HasConversion<int>();

            // 🚨 BẤT BIẾN "MỘT MẶC ĐỊNH / (khách, loại)" — partial unique (BR-ADDR-004-006).
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.AddressType })
                .IsUnique()
                .HasFilter("\"IsDefault\" AND NOT \"IsDeleted\"")
                .HasDatabaseName("IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh");

            // 🚨 CHỐNG LIÊN KẾT TRÙNG — lưới an toàn cho hai luồng cùng thêm (`TC-10`).
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.AddressId, x.AddressType })
                .IsUnique()
                .HasFilter("NOT \"IsDeleted\"")
                .HasDatabaseName("IX_CusAddressLinks_TenantId_CustomerId_AddressId_AddressType");

            // 📌 Index đọc sổ (nóng nhất): theo khách + loại + còn hiệu lực.
            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.IsDeleted, x.AddressType })
                .HasDatabaseName("IX_CusAddressLinks_TenantId_CustomerId_IsDeleted_AddressType");

            // 📌 *"địa chỉ này đang được khách nào dùng"* — cần khi ẩn danh hoá (§3.1 #7) và khi debug.
            b.HasIndex(x => new { x.TenantId, x.AddressId })
                .HasDatabaseName("IX_CusAddressLinks_TenantId_AddressId");

            // 🔒 Hàng rào DB cho dữ liệu vào từ SQL tay/import (không qua entity).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_CusAddressLinks_AddressType",
                "\"AddressType\" in (0, 1)"));

            b.ToTable(t => t.HasCheckConstraint(
                "CK_CusAddressLinks_NhanDoDai",
                "char_length(\"Label\") <= 128"));
        });

        // ── NHẬT KÝ KIỂM TOÁN XUẤT DỮ LIỆU CÁ NHÂN (`CUS-001` §22) ────────────────────────────────
        // 🚨 Bảng này trả lời **AI / KHI NÀO / BAO NHIÊU LẦN** đã xuất PII của một khách — 🔒 ba câu hỏi
        // mà **log KHÔNG trả lời được** (log bị xoay vòng, không tra cứu được, và nằm ngoài giao dịch).
        // Xem `CustomerDataExportLog` để biết vì sao nó **không** soft-delete và **không** có aggregate.
        builder.Entity<CustomerDataExportLog>(b =>
        {
            b.ToTable("CusDataExportLogs");
            b.ConfigureByConvention();

            b.Property(x => x.NguoiXuat).IsRequired().HasMaxLength(CustomerDataExportLog.DoDaiNguoiXuatToiDa);
            b.Property(x => x.LyDo).HasMaxLength(CustomerDataExportLog.DoDaiLyDoToiDa);
            b.Property(x => x.XuatLucUtc).IsRequired();

            // ⚠️ **KHÔNG unique index** — 📌 cố ý: nhiều lần xuất cùng một khách trong cùng một giây là
            // **hợp lệ**, và 🔑 chính sự **lặp lại** mới là thứ cần **phát hiện**; một unique index sẽ **che**
            // dấu hiệu đó. ➕ Vì thế **NND vẫn = 48** (không phát sinh index cần `NULLS NOT DISTINCT`).
            //
            // 📌 Index tra cứu theo **khách** — 🔑 đó là câu hỏi thật: *"dữ liệu của TÔI bị xuất khi nào"*.
            b.HasIndex(x => new { x.TenantId, x.CustomerId })
                .HasDatabaseName("IX_CusDataExportLogs_TenantId_CustomerId");

            // 📌 Index theo **thời gian** — 🔑 cho câu hỏi của người kiểm toán: *"tuần này AI đã xuất gì"*.
            b.HasIndex(x => new { x.TenantId, x.XuatLucUtc })
                .HasDatabaseName("IX_CusDataExportLogs_TenantId_XuatLucUtc");
        });
    }
}
