using eCommerce.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// 🆕 **`WP-08`** — **BÁO CÁO**, bề mặt `/api/v1/reports` cho Dashboard + 2 báo cáo (`04 §5`, `07 §WP-08`).
///
/// 🔐 **Quyền tách theo VIỆC:** mọi `GET` ⇒ `eCommerce.Reports` (xem); `POST /aggregate` ⇒
/// `eCommerce.Reports.Aggregate` (**ghi** read-model). 📌 Gộp lại thì người chỉ cần **xem** báo cáo có thể
/// **viết lại** số liệu của một kỳ — và báo cáo là thứ dùng để ra quyết định, nên đó là quyền **ghi**.
///
/// 🚨 **`POST /aggregate` KHÔNG nhận `tenantId`/`nguồn tuỳ ý`:** nguồn do server quyết (bán lẻ + đơn hàng).
/// Nhận nguồn từ client là mở đường cho *"chỉ tổng hợp doanh thu bán lẻ"* rồi đọc báo cáo như số liệu đầy
/// đủ — sai lệch không có dấu hiệu nào trên UI.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(eCommerceConsts.Permissions.Reports.Default)]
public class ReportsController : AbpControllerBase
{
    private readonly IReportAppService _baoCao;

    public ReportsController(IReportAppService baoCao)
    {
        _baoCao = baoCao;
    }

    /// <summary>
    /// **DOANH THU** theo ngày/tháng (`?tuNgay=&amp;denNgay=&amp;theoThang=&amp;nguon=`).
    /// 📌 Kết quả **rỗng** trả `daTongHop = false` — *"chưa tổng hợp"*, **không** phải *"0 đồng"*.
    /// </summary>
    [HttpGet("revenue")]
    public Task<BaoCaoDoanhThuDto> GetDoanhThuAsync([FromQuery] DoanhThuInput input)
        => _baoCao.GetDoanhThuAsync(input);

    /// <summary>**TỔNG QUAN ĐƠN HÀNG** — số đơn, đơn huỷ, giá trị, phân bố theo trạng thái thực hiện.</summary>
    [HttpGet("orders/summary")]
    public Task<TongQuanDonHangDto> GetTongQuanDonHangAsync([FromQuery] TongQuanDonInput input)
        => _baoCao.GetTongQuanDonHangAsync(input);

    /// <summary>
    /// **TỔNG QUAN TỒN KHO** từ **ảnh chụp mới nhất** (`?ngay=`).
    /// 🚨 `daCoDuLieu = false` ⇒ chưa chụp lần nào — UI hiện *"chưa có dữ liệu"*, không hiện `0`.
    /// </summary>
    [HttpGet("inventory/summary")]
    public Task<TongQuanTonKhoDto> GetTongQuanTonKhoAsync([FromQuery] TongQuanTonKhoInput input)
        => _baoCao.GetTongQuanTonKhoAsync(input);

    /// <summary>**4 KPI của Dashboard** — doanh thu hôm nay · hoá đơn · đơn hàng · tồn khả dụng.</summary>
    [HttpGet("dashboard")]
    public Task<TongQuanDashboardDto> GetDashboardAsync()
        => _baoCao.GetDashboardAsync();

    /// <summary>
    /// **CHẠY TỔNG HỢP / BACKFILL** — `{ "tuNgay": "2026-11-01", "denNgay": "2026-11-04" }`.
    /// 🔐 `Reports.Aggregate`. 📌 Chạy **lại** cùng khoảng là **an toàn** (thay thế theo ngày, không cộng dồn).
    /// 📌 Không giành được khoá (một lượt khác đang chạy) ⇒ **409** `eCommerce:Lock:0001`.
    /// </summary>
    [HttpPost("aggregate")]
    [Authorize(eCommerceConsts.Permissions.Reports.Aggregate)]
    public Task<KetQuaTongHopDto> ChayTongHopAsync([FromBody] ChayTongHopInput input)
        => _baoCao.ChayTongHopAsync(input);
}
