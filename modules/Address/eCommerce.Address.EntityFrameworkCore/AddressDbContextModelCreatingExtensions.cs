using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Address.EntityFrameworkCore;

/// <summary>
/// Cấu hình model cho module `Address` — gọi từ **DbContext hợp nhất** (`eCommerceDbContext`).
/// </summary>
public static class AddressDbContextModelCreatingExtensions
{
    /// <summary>Tên bảng vật lý (tiền tố <c>Addr</c> để phân biệt module trong một DB dùng chung).</summary>
    public const string TenBang = "AddrAdministrativeUnits";

    /// <summary>Tên index unique — migration thay bằng SQL thô để có `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUnique = "IX_AddrAdministrativeUnits_TenantId_SchemaVersion_Level_Code";

    // ── ADDR-002: ánh xạ mã địa chỉ theo hãng vận chuyển ──
    public const string TenBangMaHang = "AddrCarrierMappings";

    /// <summary>Tên index unique cho ánh xạ mã hãng — cũng cần `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUniqueMaHang = "IX_AddrCarrierMappings_TenantId_CarrierCode_CarrierAddressCode";

    // ── ADDR-004: SỔ ĐỊA CHỈ — entity `Address` (địa chỉ VẬT LÝ, dùng chung trong tenant) ──
    public const string TenBangDiaChi = "AddrAddresses";

    /// <summary>🚨 Khoá tự nhiên `(TenantId, ContentHash)` — migration thay bằng SQL thô để có `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUniqueDiaChi = "IX_AddrAddresses_TenantId_ContentHash";

    /// <summary>Hàng đợi vận hành: đếm `ChuaChuanHoa`/`NghiNgo` theo tenant.</summary>
    public const string TenIndexHangDoi = "IX_AddrAddresses_TenantId_TrangThai";

    /// <summary>*"Địa chỉ nào thuộc xã vừa bị sáp nhập"* — job đối chiếu (`ADDR-004` §3.1 #5).</summary>
    public const string TenIndexTheoXa = "IX_AddrAddresses_TenantId_TinhCode_XaCode";

    public static void ConfigureAddress(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AdministrativeUnit>(b =>
        {
            b.ToTable(TenBang);

            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(5);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ParentCode).HasMaxLength(5);
            b.Property(x => x.LegalBasis).HasMaxLength(512);

            // 🔒 RÀNG BUỘC UNIQUE — khoá thật của danh mục là (PHIÊN BẢN, CẤP, MÃ), KHÔNG phải mã trần.
            // ⚠️ EF không diễn đạt được `NULLS NOT DISTINCT` (PostgreSQL mặc định coi NULL là KHÁC NHAU)
            // ⇒ khi `TenantId` NULL thì ràng buộc MẤT TÁC DỤNG — đúng lỗi C4/F1 đã chứng minh bằng chạy thật.
            // Migration thay index này bằng SQL thô:
            //   CREATE UNIQUE INDEX ... ("TenantId","SchemaVersion","Level","Code") NULLS NOT DISTINCT
            b.HasIndex(x => new { x.TenantId, x.SchemaVersion, x.Level, x.Code })
                .IsUnique()
                .HasDatabaseName(TenIndexUnique);

            // Tra theo cấp dưới: lấy tất cả xã của một tỉnh (dùng cho dropdown + kiểm tra fail-closed).
            b.HasIndex(x => new { x.SchemaVersion, x.Level, x.ParentCode })
                .HasDatabaseName("IX_AddrAdministrativeUnits_SchemaVersion_Level_ParentCode");

            // Tra theo tên (tìm kiếm gần đúng / đối chiếu dữ liệu).
            b.HasIndex(x => new { x.SchemaVersion, x.Name })
                .HasDatabaseName("IX_AddrAdministrativeUnits_SchemaVersion_Name");

            // Hàng rào DB cho bất biến: chỉ có 2 phiên bản schema được hỗ trợ (1 = 3 cấp, 2 = 2 cấp).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAdministrativeUnits_SchemaVersion",
                "\"SchemaVersion\" in (1, 2)"));
        });

        // ── ADDR-002: ánh xạ mã địa chỉ theo HÃNG VẬN CHUYỂN ────────────────────
        builder.Entity<Carrier.CarrierAddressMapping>(b =>
        {
            b.ToTable(TenBangMaHang);
            b.ConfigureByConvention();

            b.Property(x => x.CarrierCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.CarrierLevel).IsRequired().HasMaxLength(32);
            b.Property(x => x.CarrierAddressCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.CarrierAddressName).IsRequired().HasMaxLength(256);
            b.Property(x => x.Source).HasMaxLength(512);

            // 🔒 MỘT mã hãng chỉ được ánh xạ MỘT lần, và phải SCOPE THEO HÃNG (cùng mã có thể là
            // hai địa chỉ khác nhau ở hai hãng). Đây là hàng rào cuối cho bất biến 0503.
            // ⚠️ Cần `NULLS NOT DISTINCT` (bài học C4/F1) ⇒ migration dùng SQL thô.
            b.HasIndex(x => new { x.TenantId, x.CarrierCode, x.CarrierAddressCode })
                .IsUnique()
                .HasDatabaseName(TenIndexUniqueMaHang);

            // Tra nhanh "còn mã nào chưa xác minh" cho màn hình vận hành.
            b.HasIndex(x => new { x.Status, x.CarrierCode })
                .HasDatabaseName("IX_AddrCarrierMappings_Status_CarrierCode");

            // Hàng rào DB: trạng thái chỉ nhận 3 giá trị (0 ChuaXacMinh · 1 DaXacMinh · 2 NghiNgo).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrCarrierMappings_Status",
                "\"Status\" in (0, 1, 2)"));
        });

        // ── ADDR-004: SỔ ĐỊA CHỈ — `Address` (địa chỉ VẬT LÝ dùng chung trong tenant) ──
        builder.Entity<Address>(b =>
        {
            b.ToTable(TenBangDiaChi);
            b.ConfigureByConvention();

            b.Property(x => x.DiaChiDayDu).IsRequired().HasMaxLength(Address.DoDaiDiaChiDayDu);
            b.Property(x => x.SoNhaDuong).HasMaxLength(Address.DoDaiSoNhaDuong);

            // Mã ĐVHC: tỉnh 2, huyện 3, xã 5 ký tự — cùng giới hạn `AdministrativeSchemaVersion.DoDaiMa`.
            b.Property(x => x.TinhCode).HasMaxLength(2).IsFixedLength();
            b.Property(x => x.HuyenCode).HasMaxLength(3).IsFixedLength();
            b.Property(x => x.XaCode).HasMaxLength(5).IsFixedLength();

            // `char(64)` hex thường — có CHECK định dạng bên dưới.
            b.Property(x => x.ContentHash).IsRequired().HasMaxLength(DiaChiCanonical.DoDaiHash).IsFixedLength();

            // 🔒 KHOÁ TỰ NHIÊN (ADR-04): một NỘI DUNG ⇒ một bản ghi.
            // ⚠️ EF KHÔNG diễn đạt được `NULLS NOT DISTINCT` (PostgreSQL coi NULL là KHÁC NHAU) ⇒ hiện
            //    chạy 1 tenant (`TenantId = NULL`) thì index này MẤT TÁC DỤNG hoàn toàn nếu thiếu mệnh đề.
            //    Migration `Addr004_SoDiaChi_Address` thay bằng SQL thô:
            //      CREATE UNIQUE INDEX ... NULLS NOT DISTINCT WHERE NOT "IsDeleted"
            b.HasIndex(x => new { x.TenantId, x.ContentHash })
                .IsUnique()
                .HasDatabaseName(TenIndexUniqueDiaChi);

            // Hàng đợi vận hành (địa chỉ thô chưa chuẩn hoá / đang nghi ngờ).
            b.HasIndex(x => new { x.TenantId, x.TrangThai })
                .HasDatabaseName(TenIndexHangDoi);

            // Đối chiếu khi một xã bị sáp nhập: tìm mọi địa chỉ đang trỏ tới xã đó.
            b.HasIndex(x => new { x.TenantId, x.TinhCode, x.XaCode })
                .HasDatabaseName(TenIndexTheoXa);

            // 🚨 BẢY hàng rào DB — mỗi cái khoá một bất biến mà code KHÔNG đủ sức giữ
            //    (dữ liệu vào từ webhook sàn / import / SQL tay). Xem `ADDR-004` §5.2.
            // ① Trạng thái chỉ nhận 4 giá trị.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_TrangThai",
                "\"TrangThai\" in (0, 1, 2, 3)"));

            // ② Địa chỉ THÔ ⇒ KHÔNG được có mã (chống trạng thái "nửa vời").
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_ThoKhongCoMa",
                "(\"TrangThai\" <> 0) OR (\"TinhCode\" IS NULL AND \"HuyenCode\" IS NULL AND \"XaCode\" IS NULL AND \"NormalizedAtUtc\" IS NULL)"));

            // ③ 🚨 ĐÃ CHUẨN HOÁ ⇒ BẮT BUỘC có mã + BẰNG CHỨNG (đây là hàng rào của fail-closed).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_DaChuanHoaCoMa",
                "(\"TrangThai\" = 0) OR (\"TinhCode\" IS NOT NULL AND \"XaCode\" IS NOT NULL AND \"NormalizedAtUtc\" IS NOT NULL)"));

            // ④ Phiên bản schema chỉ 1 hoặc 2.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_SchemaVersion",
                "\"SchemaVersion\" in (1, 2)"));

            // ⑤ 🚨 v2 KHÔNG được có huyện — quy tắc PHÁP LÝ (Luật 72/2025/QH15), không phải lựa chọn kỹ thuật.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_V2KhongCoHuyen",
                "\"SchemaVersion\" <> 2 OR \"HuyenCode\" IS NULL"));

            // ⑥ Độ dài + chỉ chữ số của mã theo cấp.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_DinhDangMa",
                "(\"TinhCode\" IS NULL OR \"TinhCode\" ~ '^[0-9]{2}$') AND (\"XaCode\" IS NULL OR \"XaCode\" ~ '^[0-9]{5}$') AND (\"HuyenCode\" IS NULL OR \"HuyenCode\" ~ '^[0-9]{3}$')"));

            // ⑦ Hash PHẢI là hex thường 64 ký tự (chặn lưu base64 / bytea / chữ HOA).
            b.ToTable(t => t.HasCheckConstraint(
                "CK_AddrAddresses_ContentHashHex",
                "\"ContentHash\" ~ '^[0-9a-f]{64}$'"));
        });
    }
}
