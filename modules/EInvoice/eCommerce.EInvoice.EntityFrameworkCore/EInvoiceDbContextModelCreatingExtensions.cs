using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.EInvoice.EntityFrameworkCore;

/// <summary>Cấu hình model cho module `EInvoice` trên **DbContext hợp nhất**.</summary>
public static class EInvoiceDbContextModelCreatingExtensions
{
    public const string TenBang = "EInvInvoices";

    /// <summary>Tên index unique — migration thay bằng SQL thô để có `NULLS NOT DISTINCT`.</summary>
    public const string TenIndexUnique = "IX_EInvInvoices_TenantId_KyHieuMauSo_SoHoaDon";

    public static void ConfigureEInvoice(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<EInvoice>(b =>
        {
            b.ToTable(TenBang);
            b.ConfigureByConvention();

            b.Property(x => x.KyHieuMauSo).IsRequired().HasMaxLength(32);
            b.Property(x => x.SoHoaDon).IsRequired().HasMaxLength(EInvoiceNumberIssuer.DoDaiSo);
            b.Property(x => x.MaCQT).HasMaxLength(64);
            b.Property(x => x.LyDo).HasMaxLength(1024);
            b.Property(x => x.LoiCQT).HasMaxLength(2048);
            b.Property(x => x.XmlDaKy).HasColumnType("text");

            // 🔒 SỐ HOÁ ĐƠN KHÔNG ĐƯỢC TRÙNG trong cùng ký hiệu mẫu số — hàng rào cuối ngoài khoá phân tán.
            // ⚠️ Cần `NULLS NOT DISTINCT` (bài học C4/F1): thiếu thì khi `TenantId` NULL ràng buộc mất tác dụng
            // ⇒ hai hoá đơn cùng số lọt vào DB ⇒ vi phạm quy định về số hoá đơn.
            b.HasIndex(x => new { x.TenantId, x.KyHieuMauSo, x.SoHoaDon })
                .IsUnique()
                .HasDatabaseName(TenIndexUnique);

            // Tra hoá đơn theo trạng thái (báo cáo, job gửi lại CQT).
            b.HasIndex(x => new { x.Status, x.TaxAuthorityStatus })
                .HasDatabaseName("IX_EInvInvoices_Status_TaxAuthorityStatus");

            // Tra hoá đơn thay thế/điều chỉnh.
            b.HasIndex(x => x.HoaDonBiThayTheId).HasDatabaseName("IX_EInvInvoices_HoaDonBiThayTheId");
            b.HasIndex(x => x.HoaDonBiDieuChinhId).HasDatabaseName("IX_EInvInvoices_HoaDonBiDieuChinhId");

            // Hàng rào DB cho bất biến MẤU CHỐT: **không thể phát hành hoá đơn chưa ký**.
            // Trạng thái: 0 Nhap · 1 DaKy · 2 DaPhatHanh · 3 DaDieuChinh · 4 DaThayThe · 5 DaHuy
            // ⇒ mọi trạng thái "đã phát hành trở lên" đều phải có XML đã ký.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_EInvInvoices_PhaiKyTruocKhiPhatHanh",
                "\"Status\" = 0 OR \"Status\" = 5 OR \"XmlDaKy\" IS NOT NULL"));
        });
    }
}
