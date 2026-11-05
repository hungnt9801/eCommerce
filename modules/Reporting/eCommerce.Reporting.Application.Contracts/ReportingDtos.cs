namespace eCommerce.Reporting;

/// <summary>Đầu vào báo cáo doanh thu.</summary>
public class DoanhThuInput
{
    /// <summary>Ngày bắt đầu (bao gồm). `null` = theo mốc đã tổng hợp / mặc định 30 ngày.</summary>
    public DateOnly? TuNgay { get; set; }

    /// <summary>Ngày kết thúc (bao gồm). `null` = hôm nay.</summary>
    public DateOnly? DenNgay { get; set; }

    /// <summary>Chỉ lấy MỘT nguồn; `null` = cả hai (bán lẻ + đơn hàng).</summary>
    public NguonBaoCao? Nguon { get; set; }

    /// <summary>Gom theo **tháng** thay vì theo ngày (biểu đồ 12 tháng).</summary>
    public bool TheoThang { get; set; }
}

/// <summary>Một điểm trên biểu đồ doanh thu (một ngày hoặc một tháng).</summary>
public class DiemDoanhThuDto
{
    /// <summary>Nhãn hiển thị (`2026-11-04` hoặc `2026-11`).</summary>
    public string Nhan { get; set; } = default!;

    public DateOnly TuNgay { get; set; }

    public DateOnly DenNgay { get; set; }

    public decimal DoanhThuBanLe { get; set; }

    public decimal DoanhThuDonHang { get; set; }

    public decimal TongDoanhThu { get; set; }

    public decimal TienVat { get; set; }

    public int SoChungTu { get; set; }
}

/// <summary>
/// Kết quả báo cáo doanh thu — **đọc từ bảng tổng hợp** (`RptAggDailySales`), không đọc chứng từ sống.
/// 📌 `DinhNghiaTien` là **hợp đồng hiển thị**: UI in nguyên câu này để người dùng biết con số nghĩa là gì.
/// </summary>
public class BaoCaoDoanhThuDto
{
    public DateOnly TuNgay { get; set; }

    public DateOnly DenNgay { get; set; }

    public decimal TongDoanhThu { get; set; }

    public decimal DoanhThuBanLe { get; set; }

    public decimal DoanhThuDonHang { get; set; }

    public decimal TongTienVat { get; set; }

    public int TongSoChungTu { get; set; }

    /// <summary>`false` ⇒ khoảng này **chưa** có lượt tổng hợp nào (báo cáo rỗng, KHÔNG phải "0 đồng").</summary>
    public bool DaTongHop { get; set; }

    /// <summary>Ngày lớn nhất đã tổng hợp (từ mốc) — để người dùng biết số liệu mới tới đâu.</summary>
    public DateOnly? NgayTongHopMoiNhat { get; set; }

    /// <summary>Câu giải thích công thức tiền (in thẳng lên UI, không viết lại ở FE).</summary>
    public string DinhNghiaTien { get; set; } = default!;

    public List<DiemDoanhThuDto> Diem { get; set; } = new();
}

/// <summary>Đầu vào tổng quan đơn hàng.</summary>
public class TongQuanDonInput
{
    public DateOnly? TuNgay { get; set; }

    public DateOnly? DenNgay { get; set; }
}

/// <summary>Một nhóm trạng thái của đơn.</summary>
public class NhomTrangThaiDonDto
{
    /// <summary>Mã trạng thái trục thực hiện (`Pending`, `Delivered`, `Packed`…) — **số** được gửi về dạng chuỗi để UI tra nhãn.</summary>
    public string TrangThai { get; set; } = default!;

    public int SoDon { get; set; }

    public decimal GiaTriDon { get; set; }
}

/// <summary>
/// Tổng quan **đơn hàng** trong kỳ — đọc **trực tiếp** `OrdOrders` (không qua bảng tổng hợp) vì cần **cả**
/// đơn đã huỷ: bảng sự kiện doanh thu **cố ý** loại đơn huỷ (chúng không phải doanh thu), nên đếm đơn huỷ
/// từ đó sẽ luôn ra `0` — sai mà không có lỗi nào.
/// </summary>
public class TongQuanDonHangDto
{
    public DateOnly TuNgay { get; set; }

    public DateOnly DenNgay { get; set; }

    public int TongDon { get; set; }

    public int SoDonHuy { get; set; }

    /// <summary>Σ `Subtotal − DiscountTotal` của đơn **không huỷ** (cùng định nghĩa tiền với báo cáo doanh thu).</summary>
    public decimal GiaTriDon { get; set; }

    public List<NhomTrangThaiDonDto> TheoTrangThai { get; set; } = new();
}

/// <summary>Đầu vào tổng quan tồn kho.</summary>
public class TongQuanTonKhoInput
{
    /// <summary>Ngày chụp; `null` = ảnh **mới nhất** có trong read-model.</summary>
    public DateOnly? Ngay { get; set; }
}

/// <summary>Một dòng tồn kho theo kho.</summary>
public class DongTonKhoDto
{
    public Guid WarehouseId { get; set; }

    public string? Kho { get; set; }

    public int SoSku { get; set; }

    public decimal OnHand { get; set; }

    public decimal KhaDung { get; set; }

    public decimal TamGiu { get; set; }
}

/// <summary>Mặt hàng sắp hết (khả dụng ≤ ngưỡng).</summary>
public class MatHangSapHetDto
{
    public Guid WarehouseId { get; set; }

    public string? Kho { get; set; }

    public string Sku { get; set; } = default!;

    public decimal OnHand { get; set; }

    public decimal KhaDung { get; set; }
}

/// <summary>
/// Tổng quan **tồn kho** từ ảnh chụp mới nhất.
/// 🚨 `DaCoDuLieu = false` ⇒ **chưa chụp lần nào**; UI phải hiện *"chưa có dữ liệu"*, **không** hiện `0`.
/// </summary>
public class TongQuanTonKhoDto
{
    public DateOnly? NgayChup { get; set; }

    public bool DaCoDuLieu { get; set; }

    public int SoKho { get; set; }

    public int SoSku { get; set; }

    public decimal TongOnHand { get; set; }

    public decimal TongKhaDung { get; set; }

    public decimal TongTamGiu { get; set; }

    public List<DongTonKhoDto> TheoKho { get; set; } = new();

    public List<MatHangSapHetDto> SapHet { get; set; } = new();
}

/// <summary>
/// **4 KPI của Dashboard** (`07 §WP-08`). 📌 Mọi con số đọc từ read-model; `NgayTongHopMoiNhat` cho người
/// dùng biết *"số liệu mới tới ngày nào"* thay vì đoán.
/// </summary>
public class TongQuanDashboardDto
{
    public DateOnly Ngay { get; set; }

    public decimal DoanhThuHomNay { get; set; }

    public decimal DoanhThuBanLeHomNay { get; set; }

    public decimal DoanhThuDonHangHomNay { get; set; }

    public int SoHoaDonBanLeHomNay { get; set; }

    public int SoDonHangHomNay { get; set; }

    /// <summary>Σ khả dụng của ảnh tồn kho mới nhất (0 khi chưa có ảnh — kèm `NgayTonKhoMoiNhat = null`).</summary>
    public decimal TongKhaDung { get; set; }

    public int SoSkuSapHet { get; set; }

    public DateOnly? NgayTonKhoMoiNhat { get; set; }

    public DateOnly? NgayTongHopMoiNhat { get; set; }

    public string DinhNghiaTien { get; set; } = default!;
}

/// <summary>Đầu vào lượt tổng hợp (job định kỳ hoặc backfill theo khoảng).</summary>
public class ChayTongHopInput
{
    /// <summary>`null` = chạy tiếp từ mốc (job định kỳ).</summary>
    public DateOnly? TuNgay { get; set; }

    /// <summary>`null` = hôm nay.</summary>
    public DateOnly? DenNgay { get; set; }

    /// <summary>Chụp ảnh tồn kho ở lượt này (mặc định `true`).</summary>
    public bool GhiAnhTonKho { get; set; } = true;
}

/// <summary>Kết quả một lượt tổng hợp — **số đo được**, không phải lời khẳng định "đã chạy".</summary>
public class KetQuaTongHopDto
{
    public DateOnly TuNgay { get; set; }

    public DateOnly DenNgay { get; set; }

    public int SoNgay { get; set; }

    /// <summary>`true` ⇒ không có việc (mốc đã ở cuối khoảng) — **không** phải lỗi.</summary>
    public bool Rong { get; set; }

    public int SoDongSuKien { get; set; }

    public int SoDongTongHop { get; set; }

    public int SoAnhTonKho { get; set; }

    public DateOnly? MocBanLe { get; set; }

    public DateOnly? MocDonHang { get; set; }

    public DateOnly? MocTonKho { get; set; }
}
