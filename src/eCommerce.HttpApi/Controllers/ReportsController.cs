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
    private readonly IReportAdminAppService _vanHanh;

    public ReportsController(IReportAppService baoCao, IReportAdminAppService vanHanh)
    {
        _baoCao = baoCao;
        _vanHanh = vanHanh;
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

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 `WP-26` — VẬN HÀNH GĐ3: rebuild · chốt kỳ · đối chiếu (`REP-001` §4.2).
    // 🔐 Class `Reports.Default`; method GHI thêm quyền hẹp (AND-combination).
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>**REBUILD** một khoảng (xếp hàng `Queued`; worker xử lý). `Reason` ≥ 10 ký tự.</summary>
    [HttpPost("rebuild")]
    [Authorize(eCommerceConsts.Permissions.Reports.Rebuild)]
    [ProducesResponseType(202)]
    public Task<LanChayDto> StartRebuildAsync([FromBody] StartRebuildInput input)
        => _vanHanh.StartRebuildAsync(input);

    /// <summary>**CHẠY TIẾP** một lượt `PartiallySucceeded`/`Failed` từ checkpoint (0 cửa sổ cũ chạy lại).</summary>
    [HttpPost("rebuild/{runId:guid}/resume")]
    [Authorize(eCommerceConsts.Permissions.Reports.Rebuild)]
    [ProducesResponseType(202)]
    public Task<LanChayDto> ResumeRebuildAsync([FromRoute] Guid runId)
        => _vanHanh.ResumeRebuildAsync(runId);

    /// <summary>Đọc một lượt chạy.</summary>
    [HttpGet("rebuild/{runId:guid}")]
    public Task<LanChayDto> GetLanChayAsync([FromRoute] Guid runId)
        => _vanHanh.GetLanChayAsync(runId);

    /// <summary>Danh sách lượt chạy (phân trang + lọc).</summary>
    [HttpGet("rebuilds")]
    public Task<LanChayPageDto> GetLanChaysAsync([FromQuery] GetLanChayInput input)
        => _vanHanh.GetLanChaysAsync(input);

    /// <summary>**CHỐT KỲ** — từ chối khi còn dòng lệch mở (`0014`); `Reason` ≥ 10.</summary>
    [HttpPost("period-close")]
    [Authorize(eCommerceConsts.Permissions.Reports.PeriodClose)]
    public Task<KyChotDto> ClosePeriodAsync([FromBody] ClosePeriodInput input)
        => _vanHanh.ClosePeriodAsync(input);

    /// <summary>**MỞ LẠI KỲ** đã chốt — `Reason` ≥ 20 ký tự.</summary>
    [HttpPost("period-close/{id:guid}/reopen")]
    [Authorize(eCommerceConsts.Permissions.Reports.PeriodReopen)]
    public Task<KyChotDto> ReopenPeriodAsync([FromRoute] Guid id, [FromBody] ReopenPeriodInput input)
    {
        input.PeriodId = id;
        return _vanHanh.ReopenPeriodAsync(input);
    }

    /// <summary>Đọc một kỳ chốt.</summary>
    [HttpGet("period-close/{id:guid}")]
    public Task<KyChotDto> GetKyChotAsync([FromRoute] Guid id)
        => _vanHanh.GetKyChotAsync(id);

    /// <summary>Danh sách kỳ chốt (phân trang + lọc).</summary>
    [HttpGet("period-closes")]
    public Task<KyChotPageDto> GetKyChotsAsync([FromQuery] GetKyChotInput input)
        => _vanHanh.GetKyChotsAsync(input);

    /// <summary>**ĐỐI CHIẾU** nguồn ↔ read-model, ghi nhận dòng lệch (KHÔNG sửa số).</summary>
    [HttpPost("reconciliation")]
    [Authorize(eCommerceConsts.Permissions.Reports.Reconcile)]
    public Task<KetQuaDoiChieuDto> RunReconciliationAsync([FromBody] RunReconciliationInput input)
        => _vanHanh.RunReconciliationAsync(input);

    /// <summary>Danh sách dòng lệch số (phân trang + lọc).</summary>
    [HttpGet("consistency-checks")]
    public Task<LechSoPageDto> GetLechSoAsync([FromQuery] GetLechSoInput input)
        => _vanHanh.GetLechSoAsync(input);
}
