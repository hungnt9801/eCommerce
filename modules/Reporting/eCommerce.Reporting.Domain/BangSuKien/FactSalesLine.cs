using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **DÒNG DOANH THU ĐÃ TỔNG HỢP** (`RptFactSalesLines`) — `07 §WP-08`: `FactSalesLine`.
///
/// 🔑 **GRAIN: MỘT DÒNG CHỨNG TỪ NGUỒN** ⇒ unique `(TenantId, Nguon, DongNguonId)` ở DB là **hàng rào
/// chống nhân đôi** khi job chạy lại / hai lượt chồng nhau. 🚨 Đây là bất biến **tầng ứng dụng không đủ
/// sức giữ**: hai lượt chạy đồng thời đều *"thấy chưa có"* rồi cùng `INSERT`.
///
/// 📌 **MỌI SỐ TIỀN LÀ ẢNH CHỤP** (không join lại nguồn khi đọc): báo cáo của kỳ đã chốt **không được**
/// đổi khi giá bán hôm nay đổi — cùng bất biến chứng từ của `WP-06`.
/// </summary>
public class RptFactSalesLine : CreationAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiSoChungTuToiDa = 64;
    public const int DoDaiSkuToiDa = 64;
    public const int DoDaiTenSanPhamToiDa = 256;

    public Guid? TenantId { get; protected set; }

    public DateOnly BusinessDate { get; protected set; }

    /// <summary>Nguồn — **luôn** `BanLe` hoặc `DonHang` (xem chú thích <see cref="NguonBaoCao"/>).</summary>
    public NguonBaoCao Nguon { get; protected set; }

    public Guid DocumentId { get; protected set; }

    /// <summary>Khoá tự nhiên của dòng trong chứng từ nguồn (`PosRetailBillLines.Id` / `OrdOrderLines.Id`).</summary>
    public Guid SourceLineId { get; protected set; }

    public string DocumentNumber { get; protected set; } = default!;

    public Guid? WarehouseId { get; protected set; }

    public Guid? StaffId { get; protected set; }

    public Guid? CustomerId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public string? ProductName { get; protected set; }

    public decimal Quantity { get; protected set; }

    public decimal UnitPrice { get; protected set; }

    public decimal LineDiscount { get; protected set; }

    /// <summary>Thành tiền dòng **chưa** gồm VAT (do domain nguồn tính; xem `TongHopDoanhThu`).</summary>
    public decimal LineAmount { get; protected set; }

    public decimal? VatPercent { get; protected set; }

    public decimal VatAmount { get; protected set; }

    /// <summary>Mốc phát sinh **theo UTC** (giữ nguyên để đối chiếu với chứng từ).</summary>
    public DateTime OccurredAtUtc { get; protected set; }

    protected RptFactSalesLine() { }

    /// <summary>
    /// Dựng dòng sự kiện từ kết quả tổng hợp — **đường ghi duy nhất**; kiểm bất biến ngay tại đây để
    /// không có dòng rác nào vào read-model (read-model sai thì **mọi** báo cáo sai, không lỗi nào nổi lên).
    /// </summary>
    public static RptFactSalesLine TuSuKien(Guid id, DongSuKienBanHang suKien)
    {
        ArgumentNullException.ThrowIfNull(suKien);

        if (suKien.Nguon == NguonBaoCao.TonKho)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("nguon", suKien.Nguon.ToString())
                .WithData("lyDo", "`TonKho` KHÔNG phải nguồn của dòng doanh thu.");
        }

        if (string.IsNullOrWhiteSpace(suKien.SoChungTu))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("lyDo", "Dòng doanh thu phải có SỐ CHỨNG TỪ.");
        }

        if (suKien.SoChungTu.Trim().Length > DoDaiSoChungTuToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("truong", nameof(suKien.SoChungTu))
                .WithData("toiDa", DoDaiSoChungTuToiDa);
        }

        if (string.IsNullOrWhiteSpace(suKien.Sku))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("lyDo", "Dòng doanh thu phải có SKU.");
        }

        if (suKien.Sku.Trim().Length > DoDaiSkuToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("truong", nameof(suKien.Sku))
                .WithData("toiDa", DoDaiSkuToiDa);
        }

        // 📌 Số lượng = 0 xảy ra thật: dòng đơn bị trả HẾT vẫn còn trong chứng từ (Quantity không đổi).
        // ⇒ KHÔNG chặn ở đây; chỉ chặn ÂM (dấu sai). Tiền cũng vậy — 0 là hợp lệ (hàng tặng/khuyến mại).
        if (suKien.SoLuong < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("sku", suKien.Sku)
                .WithData("soLuong", suKien.SoLuong)
                .WithData("lyDo", "Số lượng dòng KHÔNG được âm.");
        }

        if (suKien.ThanhTienDong < 0m || suKien.DonGia < 0m || suKien.ChietKhauDong < 0m || suKien.TienVat < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("donGia", suKien.DonGia)
                .WithData("chietKhauDong", suKien.ChietKhauDong)
                .WithData("thanhTienDong", suKien.ThanhTienDong)
                .WithData("tienVat", suKien.TienVat)
                .WithData("lyDo", "Tiền của dòng doanh thu KHÔNG được âm.");
        }

        var ten = string.IsNullOrWhiteSpace(suKien.TenSanPham) ? null : suKien.TenSanPham.Trim();

        if (ten is { Length: > DoDaiTenSanPhamToiDa })
        {
            ten = ten[..DoDaiTenSanPhamToiDa];
        }

        return new RptFactSalesLine
        {
            Id = id,
            BusinessDate = suKien.NgayKinhDoanh,
            Nguon = suKien.Nguon,
            DocumentId = suKien.ChungTuId,
            SourceLineId = suKien.DongNguonId,
            DocumentNumber = suKien.SoChungTu.Trim(),
            WarehouseId = suKien.KhoId,
            StaffId = suKien.NhanVienId,
            CustomerId = suKien.KhachId,
            Sku = suKien.Sku.Trim(),
            ProductName = ten,
            Quantity = suKien.SoLuong,
            UnitPrice = suKien.DonGia,
            LineDiscount = suKien.ChietKhauDong,
            LineAmount = suKien.ThanhTienDong,
            VatPercent = suKien.PhanTramVat,
            VatAmount = suKien.TienVat,
            OccurredAtUtc = suKien.XayRaLucUtc
        };
    }
}
