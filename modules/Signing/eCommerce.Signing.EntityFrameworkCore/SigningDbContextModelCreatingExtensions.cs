using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Signing.EntityFrameworkCore;

/// <summary>Cấu hình model cho module `Signing` trên **DbContext hợp nhất**.</summary>
public static class SigningDbContextModelCreatingExtensions
{
    public const string TenBangYeuCauKy = "SigSigningRequests";
    public const string TenBangChungThu = "SigCertificates";

    public static void ConfigureSigning(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<SigningRequest>(b =>
        {
            b.ToTable(TenBangYeuCauKy);
            b.ConfigureByConvention();

            b.Property(x => x.MaTaiLieu).IsRequired().HasMaxLength(128);
            b.Property(x => x.HashTaiLieu).IsRequired().HasMaxLength(128);
            b.Property(x => x.ThuatToanBam).IsRequired().HasMaxLength(32);
            b.Property(x => x.ChuKyDaKy).HasColumnType("text");
            b.Property(x => x.NguoiKy).HasMaxLength(256);
            b.Property(x => x.LyDoHuy).HasMaxLength(1024);

            // 🔒 MỖI tài liệu chỉ có MỘT yêu cầu ký đang tồn tại — tránh hai luồng cùng yêu cầu ký một
            // hoá đơn (hai chữ ký trên cùng tài liệu ⇒ không biết bản nào là bản có hiệu lực).
            // ⚠️ Cần `NULLS NOT DISTINCT` (bài học C4/F1) ⇒ migration dùng SQL thô.
            b.HasIndex(x => new { x.TenantId, x.MaTaiLieu })
                .IsUnique()
                .HasDatabaseName("IX_SigSigningRequests_TenantId_MaTaiLieu");

            b.HasIndex(x => x.TrangThai).HasDatabaseName("IX_SigSigningRequests_TrangThai");

            // Hàng rào DB cho bất biến MẤU CHỐT: trạng thái `DaKy` (1) **BẮT BUỘC** có đủ chữ ký,
            // thời điểm ký, người ký và chứng thư. Không thể "đánh dấu đã ký" mà thiếu bằng chứng.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_SigSigningRequests_DaKyPhaiCoDuBangChung",
                "\"TrangThai\" <> 1 OR (\"ChuKyDaKy\" IS NOT NULL AND \"ThoiDiemKy\" IS NOT NULL "
                + "AND \"NguoiKy\" IS NOT NULL AND \"ChungThuSoId\" IS NOT NULL)"));
        });

        builder.Entity<SigningCertificate>(b =>
        {
            b.ToTable(TenBangChungThu);
            b.ConfigureByConvention();

            b.Property(x => x.SoSerial).IsRequired().HasMaxLength(128);
            b.Property(x => x.ChuSoHuu).IsRequired().HasMaxLength(512);
            b.Property(x => x.DonViPhatHanh).IsRequired().HasMaxLength(256);

            // Số serial chứng thư là duy nhất (do CA cấp).
            b.HasIndex(x => new { x.TenantId, x.SoSerial })
                .IsUnique()
                .HasDatabaseName("IX_SigCertificates_TenantId_SoSerial");

            // Hàng rào DB: hiệu lực phải là một KHOẢNG hợp lệ.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_SigCertificates_HieuLuc",
                "\"HieuLucDen\" > \"HieuLucTu\""));
        });
    }
}
