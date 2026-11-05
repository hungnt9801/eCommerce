using Volo.Abp.Application.Services;

namespace eCommerce.Reporting;

/// <summary>
/// **API BÁO CÁO** (`WP-08`) — `/api/v1/reports/**`.
///
/// 🔐 Quyền: `GET` ⇒ `eCommerce.Reports` (xem); `POST /aggregate` ⇒ `eCommerce.Reports.Aggregate`
/// (**chạy tổng hợp** — ghi read-model, tốn tài nguyên và ảnh hưởng số liệu của mọi người).
/// 📌 Tách hai quyền vì *"xem báo cáo"* và *"làm lại số liệu"* là hai việc khác nhau: gộp lại thì người
/// xem báo cáo có thể **ghi đè** read-model (hoặc xoá sạch rồi chạy lại sai khoảng).
/// </summary>
public interface IReportAppService : IApplicationService
{
    /// <summary>Báo cáo doanh thu theo ngày/tháng từ bảng tổng hợp.</summary>
    Task<BaoCaoDoanhThuDto> GetDoanhThuAsync(DoanhThuInput input);

    /// <summary>Tổng quan đơn hàng trong kỳ (kể cả đơn huỷ).</summary>
    Task<TongQuanDonHangDto> GetTongQuanDonHangAsync(TongQuanDonInput input);

    /// <summary>Tổng quan tồn kho từ ảnh chụp mới nhất.</summary>
    Task<TongQuanTonKhoDto> GetTongQuanTonKhoAsync(TongQuanTonKhoInput input);

    /// <summary>4 KPI cho Dashboard.</summary>
    Task<TongQuanDashboardDto> GetDashboardAsync();

    /// <summary>Chạy tổng hợp (job định kỳ **hoặc** backfill theo khoảng).</summary>
    Task<KetQuaTongHopDto> ChayTongHopAsync(ChayTongHopInput input);
}
