using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **DOANH THU TỔNG HỢP THEO NGÀY + NGUỒN** (`RptAggDailySales`) — `07 §WP-08`: `AggDaily*`.
///
/// 🔑 **VÌ SAO GIỮ BẢNG NÀY khi đã có dòng sự kiện:** biểu đồ doanh thu 30/90 ngày đọc **30–90 dòng**
/// thay vì **hàng trăm nghìn** dòng sự kiện. 📌 Nhưng nó **không** phải nguồn sự thật thứ hai:
/// `DoanhThu` được tính **một lần** từ `TongHopDoanhThu.TinhDoanhThuChungTu` rồi ghi xuống, và bảng này
/// **được xoá + ghi lại** cùng lượt với bảng sự kiện (không có đường ghi riêng).
///
/// 🚨 **`Revenue = LineAmountTotal − DocumentDiscountTotal`** — cùng công thức với dòng sự kiện. Nếu ai
/// đó "sửa" công thức ở một trong hai nơi thì báo cáo ngày và báo cáo dòng sẽ **lệch nhau**, và không ai
/// biết cái nào đúng. Test `Wp08BaoCaoTests` khoá đẳng thức này.
/// </summary>
public class RptAggDailySales : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public DateOnly BusinessDate { get; protected set; }

    public NguonBaoCao Nguon { get; protected set; }

    /// <summary>Số **chứng từ** có ít nhất một dòng trong ngày (không đếm chứng từ 0 dòng).</summary>
    public int DocumentCount { get; protected set; }

    public int LineCount { get; protected set; }

    public decimal Quantity { get; protected set; }

    /// <summary>Σ thành tiền dòng (**chưa** trừ chiết khấu chứng từ, **chưa** gồm VAT).</summary>
    public decimal LineAmountTotal { get; protected set; }

    public decimal DocumentDiscountTotal { get; protected set; }

    /// <summary>**Doanh thu** = `LineAmountTotal − DocumentDiscountTotal` (công thức duy nhất).</summary>
    public decimal Revenue { get; protected set; }

    public decimal VatTotal { get; protected set; }

    public DateTime AggregatedAtUtc { get; protected set; }

    protected RptAggDailySales() { }

    /// <summary>Dựng dòng tổng hợp từ kết quả lõi — 📌 **không** nhận số đã tính sẵn từ ngoài.</summary>
    public static RptAggDailySales TuTongHop(Guid id, DongTongHopNgay dong, DateTime aggregatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(dong);

        if (dong.Nguon == NguonBaoCao.TonKho)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("nguon", dong.Nguon.ToString())
                .WithData("lyDo", "`TonKho` KHÔNG phải nguồn doanh thu.");
        }

        if (dong.DoanhThu < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("doanhThu", dong.DoanhThu)
                .WithData("lyDo", "Doanh thu tổng hợp KHÔNG được âm.");
        }

        if (dong.SoChungTu < 0 || dong.SoDong < 0 || dong.SoLuong < 0m || dong.TongTienDong < 0m
            || dong.ChietKhauChungTu < 0m || dong.TienVat < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("ngay", dong.NgayKinhDoanh)
                .WithData("lyDo", "Số đếm/số tiền của dòng tổng hợp KHÔNG được âm.");
        }

        return new RptAggDailySales
        {
            Id = id,
            BusinessDate = dong.NgayKinhDoanh,
            Nguon = dong.Nguon,
            DocumentCount = dong.SoChungTu,
            LineCount = dong.SoDong,
            Quantity = dong.SoLuong,
            LineAmountTotal = dong.TongTienDong,
            DocumentDiscountTotal = dong.ChietKhauChungTu,
            Revenue = dong.DoanhThu,
            VatTotal = dong.TienVat,
            AggregatedAtUtc = aggregatedAtUtc
        };
    }
}

/// <summary>
/// **MỐC TỔNG HỢP (WATERMARK)** (`RptWatermarks`) — `07 §WP-08`: *"Job tổng hợp theo watermark"*.
///
/// 🔑 **VÌ SAO PHẢI LƯU MỐC (không suy từ `MAX(BusinessDate)` của bảng sự kiện):** một ngày **không có
/// chứng từ** sẽ không xuất hiện trong bảng sự kiện ⇒ `MAX()` đứng yên ⇒ job chạy lại **mãi** ngày cũ,
/// hoặc bỏ qua ngày đó mãi mãi. 📌 Mốc là *"đã xử lý tới ngày nào"*, không phải *"dữ liệu mới nhất"*.
///
/// 🚨 **MỐC CHỈ TIẾN, KHÔNG LÙI:** backfill một khoảng cũ **không** được hạ mốc — nếu hạ, lượt chạy định
/// kỳ sau sẽ làm lại một khoảng đã xong (tốn kém) và tệ hơn: nếu ai đó backfill nhầm về quá khứ xa, mốc
/// tụt về đó và mỗi lượt job chỉ tiến thêm một ngày.
/// </summary>
public class RptWatermark : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public NguonBaoCao Nguon { get; protected set; }

    /// <summary>Ngày kinh doanh lớn nhất **đã** tổng hợp; `null` = chưa từng chạy.</summary>
    public DateOnly? LastAggregatedDate { get; protected set; }

    public DateTime? LastRunAtUtc { get; protected set; }

    /// <summary>Số dòng sự kiện ghi ở lượt gần nhất — 📌 để phân biệt *"chạy mà không có việc"* với *"không chạy"*.</summary>
    public long LastRowCount { get; protected set; }

    protected RptWatermark() { }

    public RptWatermark(Guid id, NguonBaoCao nguon) : base(id)
    {
        if (nguon is not (NguonBaoCao.BanLe or NguonBaoCao.DonHang or NguonBaoCao.TonKho))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("nguon", nguon.ToString());
        }

        Nguon = nguon;
    }

    /// <summary>
    /// Ghi nhận một lượt đã xử lý **tới** `denNgay`.
    /// 🔒 Mốc chỉ **tiến**: `denNgay` cũ hơn mốc hiện tại thì mốc **giữ nguyên** (chỉ cập nhật thời điểm/số dòng).
    /// </summary>
    public void GhiNhan(DateOnly denNgay, DateTime lucUtc, long soDong)
    {
        if (soDong < 0)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe)
                .WithData("soDong", soDong)
                .WithData("lyDo", "Số dòng của một lượt tổng hợp KHÔNG được âm.");
        }

        if (LastAggregatedDate is null || denNgay > LastAggregatedDate.Value)
        {
            LastAggregatedDate = denNgay;
        }

        LastRunAtUtc = lucUtc;
        LastRowCount = soDong;
    }
}
