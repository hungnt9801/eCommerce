using eCommerce.Application.Contracts.Pos;
using eCommerce.HttpApi.Dtos;
using eCommerce.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// 🆕 **`WP-06`** — **QUẦY BÁN LẺ (POS), bề mặt `/api/v1/pos`** cho Web Admin màn #12/#13/#14 (`04 §5`).
///
/// 🔐 **Quyền tách theo VIỆC, không theo controller:**
/// <list type="bullet">
///   <item>`GET` (DS/chi tiết/ca) ⇒ `eCommerce.Pos` — xem không cần quyền ghi;</item>
///   <item>`POST /bills` ⇒ `eCommerce.Pos.Sell` (bán);</item>
///   <item>`POST /bills/{id}/void` ⇒ `eCommerce.Pos.Void` (**huỷ** — đảo tồn **và** đảo tiền);</item>
///   <item>`POST /sessions/**` ⇒ `eCommerce.Pos.Session.Manage` (biên bản tiền mặt);</item>
///   <item>`POST /returns` ⇒ `eCommerce.Pos.Return` (trả hàng).</item>
/// </list>
///
/// 📌 **VÌ SAO MỌI ĐƯỜNG GHI ĐI QUA APP SERVICE:** tiền do `TinhTienHoaDon` tính + tồn kho do
/// `IPosStockGateway` ghi + idempotency theo `ClientBillId` nằm ở app service. Controller ghi tắt
/// (đụng repository) sẽ **bỏ qua cả ba** — và đó là hoá đơn "bán mà không trừ kho".
///
/// 🚨 **`Idempotency-Key` = `ClientBillId`** (`03 §5.2`, `D-42`): header **chỉ là bản sao dễ đọc** của khoá
/// trong body. 📌 Server dùng **body** làm nguồn sự thật (header do proxy có thể bị strip) — nhưng nếu cả hai
/// cùng có mà **lệch nhau** thì từ chối: đó là dấu hiệu client gửi nhầm hoá đơn.
/// </summary>
[ApiController]
[Route("api/v1/pos")]
[Authorize(eCommerceConsts.Permissions.Pos.Default)]
public class PosController : AbpControllerBase
{
    private const int PageSizeToiDa = 200;

    private readonly IRetailBillAppService _hoaDon;
    private readonly ICashSessionAppService _ca;
    private readonly IRetailReturnAppService _traHang;

    public PosController(
        IRetailBillAppService hoaDon,
        ICashSessionAppService ca,
        IRetailReturnAppService traHang)
    {
        _hoaDon = hoaDon;
        _ca = ca;
        _traHang = traHang;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // HOÁ ĐƠN
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>DS hoá đơn bán lẻ (màn #13) — lọc server-side theo kho/ca/khách/trạng thái/khoảng ngày.</summary>
    [HttpGet("bills")]
    public Task<PagedResultDto<RetailBillDto>> GetBillsAsync([FromQuery] RetailBillListInput input)
        => _hoaDon.GetListAsync(input);

    [HttpGet("bills/{id:guid}")]
    public Task<RetailBillDto> GetBillAsync(Guid id) => _hoaDon.GetAsync(id);

    /// <summary>
    /// **BÁN** — tạo hoá đơn + đẩy tồn kho + ghi tiền **trong một transaction**.
    /// 📌 Gửi lại cùng `ClientBillId` ⇒ **200** + `alreadyExisted = true` (KHÔNG tạo hoá đơn thứ hai).
    /// 📌 Thiếu tồn ⇒ **403** `eCommerce:Inventory:0001` (UI hiện *"không đủ tồn"*, không phải "hệ thống lỗi").
    /// 🔐 `Pos.Sell`.
    /// </summary>
    [HttpPost("bills")]
    [Authorize(eCommerceConsts.Permissions.Pos.Sell)]
    public Task<CreateRetailBillResultDto> CreateBillAsync([FromBody] CreateRetailBillDto input)
    {
        // 🔒 Header `Idempotency-Key` (nếu có) PHẢI trùng `ClientBillId` — lệch = client gửi nhầm hoá đơn.
        var khoaHeader = Request.Headers["Idempotency-Key"].ToString();

        if (!string.IsNullOrWhiteSpace(khoaHeader)
            && Guid.TryParse(khoaHeader, out var khoa)
            && khoa != input.ClientBillId)
        {
            throw new BusinessException(PosErrorCodes.TrungKhoaKhacNoiDung)
                .WithData("header", khoa)
                .WithData("body", input.ClientBillId)
                .WithData("lyDo", "`Idempotency-Key` (header) KHÁC `ClientBillId` (body).");
        }

        return _hoaDon.CreateAsync(input);
    }

    /// <summary>**HUỶ (ĐẢO)** hoá đơn — nhập lại kho + đảo công nợ. 🔐 `Pos.Void`.</summary>
    [HttpPost("bills/{id:guid}/void")]
    [Authorize(eCommerceConsts.Permissions.Pos.Void)]
    public Task<VoidRetailBillResultDto> VoidBillAsync(Guid id, [FromBody] VoidRetailBillDto input)
        => _hoaDon.VoidAsync(id, input);

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // CA THU NGÂN
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ca **đang mở** của quầy — `204 No Content` khi chưa mở ca (§5.3: phải mở ca trước khi bán).
    /// 📌 Trả `204` (không phải `404`): *"chưa mở ca"* là **trạng thái bình thường**, không phải lỗi.
    /// </summary>
    [HttpGet("sessions/current")]
    public async Task<ActionResult<CashSessionDto>> GetCurrentSessionAsync([FromQuery] Guid warehouseId)
    {
        var ca = await _ca.GetCurrentAsync(warehouseId);

        return ca is null ? NoContent() : Ok(ca);
    }

    [HttpGet("sessions")]
    public async Task<ApiPage<CashSessionDto>> GetSessionsAsync(
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (trang, co) = ChuanHoaPhanTrang(page, pageSize);

        var result = await _ca.GetListAsync(warehouseId, fromUtc, toUtc, (trang - 1) * co, co);

        return new ApiPage<CashSessionDto>(result.Items, result.TotalCount, trang, co);
    }

    [HttpGet("sessions/{id:guid}")]
    public Task<CashSessionDto> GetSessionAsync(Guid id) => _ca.GetAsync(id);

    /// <summary>**MỞ CA** — một quầy một ca (mở chồng ⇒ `eCommerce:Pos:0012`). 🔐 `Pos.Session.Manage`.</summary>
    [HttpPost("sessions/open")]
    [Authorize(eCommerceConsts.Permissions.Pos.SessionManage)]
    public Task<CashSessionDto> OpenSessionAsync([FromBody] OpenCashSessionDto input) => _ca.OpenAsync(input);

    /// <summary>
    /// **KẾT CA** — chốt `ExpectedCash` + `Variance` (Nhanh: *"đã kết ca là không quay ngược được"*).
    /// 🔐 `Pos.Session.Manage`.
    /// </summary>
    [HttpPost("sessions/{id:guid}/close")]
    [Authorize(eCommerceConsts.Permissions.Pos.SessionManage)]
    public Task<CashSessionDto> CloseSessionAsync(Guid id, [FromBody] CloseCashSessionDto input)
        => _ca.CloseAsync(id, input);

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // TRẢ HÀNG
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [HttpGet("returns")]
    public Task<PagedResultDto<RetailReturnDto>> GetReturnsAsync([FromQuery] RetailReturnListInput input)
        => _traHang.GetListAsync(input);

    [HttpGet("returns/{id:guid}")]
    public Task<RetailReturnDto> GetReturnAsync(Guid id) => _traHang.GetAsync(id);

    /// <summary>**TẠO PHIẾU TRẢ** — nhập lại kho + trừ công nợ trước, hoàn tiền sau. 🔐 `Pos.Return`.</summary>
    [HttpPost("returns")]
    [Authorize(eCommerceConsts.Permissions.Pos.Return)]
    public async Task<ActionResult<RetailReturnDto>> CreateReturnAsync([FromBody] CreateRetailReturnDto input)
    {
        var dto = await _traHang.CreateAsync(input);

        return CreatedAtAction(nameof(GetReturnAsync), new { id = dto.Id }, dto);
    }

    /// <summary>Chuẩn hoá phân trang: `page ≥ 1`, `1 ≤ pageSize ≤ 200` (mặc định 50).</summary>
    private static (int Trang, int Co) ChuanHoaPhanTrang(int page, int pageSize)
    {
        var trang = page < 1 ? 1 : page;
        var co = pageSize is < 1 or > PageSizeToiDa ? 50 : pageSize;

        return (trang, co);
    }
}
