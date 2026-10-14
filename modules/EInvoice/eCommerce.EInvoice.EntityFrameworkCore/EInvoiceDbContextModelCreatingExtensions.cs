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

    /// <summary>EINV-003: mot ban THAY THE chi gan voi MOT goc (migration thay bang SQL tho de co NULLS NOT DISTINCT).</summary>
    public const string TenIndexUniqueThayThe = "IX_EInvInvoices_TenantId_HoaDonBiThayTheId";

    /// <summary>EINV-003: mot ban DIEU CHINH chi gan voi MOT goc.</summary>
    public const string TenIndexUniqueDieuChinh = "IX_EInvInvoices_TenantId_HoaDonBiDieuChinhId";

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

            // Tra hoá đơn thay thế/điều chỉnh (tra một chiều theo Id liên kết).
            b.HasIndex(x => x.HoaDonBiThayTheId).HasDatabaseName("IX_EInvInvoices_HoaDonBiThayTheId");
            b.HasIndex(x => x.HoaDonBiDieuChinhId).HasDatabaseName("IX_EInvInvoices_HoaDonBiDieuChinhId");

            // 🔒 `EINV-003` — **MỘT bản thay thế/điều chỉnh chỉ được gắn với MỘT hoá đơn gốc**.
            //
            // 🚨 VÌ SAO CẦN RÀNG BUỘC NÀY (lỗ hổng thật phát hiện 14/10/2026):
            //   Chiều liên kết là **GỐC → BẢN THAY THẾ** (xem doc `EInvoice.HoaDonBiThayTheId`).
            //   Máy trạng thái đã chặn việc **một gốc bị thay thế hai lần** (lần 2 ném lỗi vì gốc không còn
            //   `DaPhatHanh`). NHƯNG **chiều ngược lại thì KHÔNG ai chặn**: hai gốc **khác nhau** có thể cùng
            //   trỏ tới **một** bản thay thế ⇒ một chứng từ "thay thế" hai hoá đơn khác nhau — **vô lý về
            //   pháp lý** (bản thay thế chỉ có một nội dung) và làm sổ sách không đối chiếu được.
            //
            // 📌 Index MỘT PHẦN (`WHERE ... IS NOT NULL`) nên hoá đơn bình thường (không liên kết) không bị ảnh hưởng.
            // ⚠️ Cần **`NULLS NOT DISTINCT`** (bài học C4/F1): thiếu thì khi `TenantId` NULL, hai dòng cùng
            //    liên kết bị coi là **khác nhau** ⇒ ràng buộc **mất tác dụng trong im lặng**.
            //    EF Core không diễn đạt được `NULLS NOT DISTINCT` ⇒ migration thay bằng SQL thô.
            b.HasIndex(x => new { x.TenantId, x.HoaDonBiThayTheId })
                .IsUnique()
                .HasFilter("\"HoaDonBiThayTheId\" IS NOT NULL AND NOT \"IsDeleted\"")
                .HasDatabaseName(TenIndexUniqueThayThe);

            b.HasIndex(x => new { x.TenantId, x.HoaDonBiDieuChinhId })
                .IsUnique()
                .HasFilter("\"HoaDonBiDieuChinhId\" IS NOT NULL AND NOT \"IsDeleted\"")
                .HasDatabaseName(TenIndexUniqueDieuChinh);

            // Hàng rào DB cho bất biến MẤU CHỐT: **không thể phát hành hoá đơn chưa ký**.
            // Trạng thái: 0 Nhap · 1 DaKy · 2 DaPhatHanh · 3 DaDieuChinh · 4 DaThayThe · 5 DaHuy
            // ⇒ mọi trạng thái "đã phát hành trở lên" đều phải có XML đã ký.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_EInvInvoices_PhaiKyTruocKhiPhatHanh",
                "\"Status\" = 0 OR \"Status\" = 5 OR \"XmlDaKy\" IS NOT NULL"));
        });
    }
}
