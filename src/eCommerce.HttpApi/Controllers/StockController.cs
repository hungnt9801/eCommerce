using eCommerce.Application.Contracts.Inventory;
using eCommerce.HttpApi.Dtos;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **KHO — bề mặt `/api/v1/stock`** (`WP-04`, `07` §WP-04 việc 6) cho Web Admin màn #7/#8.
///
/// 🔐 Quyền: class dùng `Inventory.Default` (quyền **xem**); mọi đường **ghi** khai `Inventory.Adjust` ở cấp method
/// — đúng mẫu `[Authorize]` cấp class đã chốt ở `XC-004` và mẫu của `ProductsController`.
///
/// 📌 **VÌ SAO ĐẶT Ở `eCommerce.HttpApi` (không ở Host):** D-14 — controller `/api/v1/**` nằm trong project
/// riêng; Host chỉ nạp module. Nhờ vậy bề mặt v1 **không** trộn với `/api/app/**` (ABP conventional, D-13).
///
/// 🚨 **MỌI ĐƯỜNG GHI ĐỀU ĐI QUA APP SERVICE** (không đụng repository/DbContext): ghi sổ kho là chỗ **append-only**
/// + khoá phân tán + idempotency; một controller ghi tắt sẽ **bỏ qua** cả ba.
/// </summary>
[ApiController]
[Route("api/v1/stock")]
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class StockController : AbpControllerBase
{
    private const int PageSizeToiDa = 200;

    private readonly ITonKhoAppService _tonKho;
    private readonly IStockDocumentAppService _phieu;

    public StockController(ITonKhoAppService tonKho, IStockDocumentAppService phieu)
    {
        _tonKho = tonKho;
        _phieu = phieu;
    }

    /// <summary>
    /// **Tồn kho 9 chiều** có phân trang — cột tồn của màn DS phiếu kho và màn chi tiết sản phẩm.
    /// </summary>
    [HttpGet("balances")]
    public async Task<ApiPage<StockBalanceDto>> GetBalancesAsync(
        [FromQuery] Guid? warehouseId, [FromQuery] string? sku,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (trang, co) = ChuanHoaPhanTrang(page, pageSize);

        var result = await _tonKho.GetBalancesAsync(warehouseId, sku, (trang - 1) * co, co);

        return new ApiPage<StockBalanceDto>(result.Items, result.TotalCount, trang, co);
    }

    /// <summary>
    /// **DS phiếu kho** có phân trang + lọc (chiều/trạng thái/kiểu phiếu/kho/khoảng ngày).
    /// 📌 Enum nhận **tên** (`In`, `Draft`, `Supplier`) **hoặc** số, không phân biệt hoa thường — ASP.NET tự bind.
    /// </summary>
    [HttpGet("bills")]
    public async Task<ApiPage<StockDocumentDto>> GetBillsAsync(
        [FromQuery] StockDocumentDirection? direction,
        [FromQuery] StockDocumentStatus? status,
        [FromQuery] StockDocumentMode? mode,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (trang, co) = ChuanHoaPhanTrang(page, pageSize);

        var result = await _phieu.GetPagedAsync(
            direction, status, mode, warehouseId, fromUtc, toUtc, (trang - 1) * co, co);

        return new ApiPage<StockDocumentDto>(result.Items, result.TotalCount, trang, co);
    }

    [HttpGet("bills/{id:guid}")]
    public Task<StockDocumentDto> GetBillAsync(Guid id) => _phieu.GetAsync(id);

    /// <summary>Dòng của một phiếu — màn chi tiết phiếu (#8).</summary>
    [HttpGet("bills/{id:guid}/lines")]
    public Task<List<StockDocumentLineDto>> GetBillLinesAsync(Guid id) => _phieu.GetLinesAsync(id);

    /// <summary>
    /// **Tạo phiếu kho** (luôn `Draft` — **chưa** đổi tồn kho; phải `POST …/post` mới ghi sổ).
    /// 🔐 `Inventory.Adjust`.
    /// 📌 **201** + `Location` tới `/api/v1/stock/bills/{id}` để client mở chi tiết ngay.
    /// 📌 Gửi lại cùng `idempotencyKey` ⇒ **409/403 kèm `eCommerce:Inventory:0035`**, **không** tạo phiếu thứ hai.
    /// </summary>
    [HttpPost("bills")]
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<ActionResult<StockDocumentDto>> CreateBillAsync([FromBody] CreateStockDocumentDto input)
    {
        var dto = await _phieu.CreateAsync(input);

        return CreatedAtAction(nameof(GetBillAsync), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// **GHI SỔ** phiếu — sinh bút toán sổ cái + đổi tồn kho (`INV-001` ADR-08/09).
    /// 📌 Ghi sổ **lần hai** ⇒ lỗi nghiệp vụ `eCommerce:Inventory:0010`, tồn kho **không đổi** (không có bút toán thứ hai).
    /// </summary>
    [HttpPost("bills/{id:guid}/post")]
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public Task<PostStockDocumentResultDto> PostBillAsync(Guid id) => _phieu.GhiSoAsync(id);

    /// <summary>**HUỶ** phiếu còn `Draft`. Phiếu đã ghi sổ **KHÔNG** huỷ được (phải tạo phiếu ĐẢO).</summary>
    [HttpPost("bills/{id:guid}/cancel")]
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public Task<StockDocumentDto> CancelBillAsync(Guid id) => _phieu.CancelAsync(id);

    /// <summary>Chuẩn hoá phân trang: `page ≥ 1`, `1 ≤ pageSize ≤ 200` (mặc định 50).</summary>
    private static (int Trang, int Co) ChuanHoaPhanTrang(int page, int pageSize)
    {
        var trang = page < 1 ? 1 : page;
        var co = pageSize is < 1 or > PageSizeToiDa ? 50 : pageSize;

        return (trang, co);
    }
}
