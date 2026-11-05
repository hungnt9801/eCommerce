using Volo.Abp.Application.Dtos;

namespace eCommerce.Application.Contracts.Pos;

/// <summary>
/// **API HOÁ ĐƠN BÁN LẺ** (`WP-06`) — bề mặt `/api/v1/pos/bills` (màn #12/#13 `04 §5`).
///
/// 🔐 **Quyền tách theo VIỆC** (`eCommerceConsts.Permissions.Pos`):
/// <list type="bullet">
///   <item>đọc (DS/chi tiết) ⇒ `Pos.Default`;</item>
///   <item>tạo hoá đơn ⇒ `Pos.Sell`;</item>
///   <item>**huỷ** ⇒ `Pos.Void` — đảo tồn **và** đảo tiền/điểm ⇒ **không** gộp vào `Sell`.</item>
/// </list>
/// </summary>
public interface IRetailBillAppService
{
    Task<PagedResultDto<RetailBillDto>> GetListAsync(RetailBillListInput input);

    Task<RetailBillDto> GetAsync(Guid id);

    /// <summary>
    /// **BÁN** — tạo hoá đơn + đẩy tồn kho + ghi tiền trong **một transaction**.
    /// 🚨 **Idempotent theo `ClientBillId`**: gửi lại ⇒ trả hoá đơn **đã có** (`alreadyExisted = true`),
    /// không đẩy tồn lần hai. Cùng khoá nhưng **khác nội dung** ⇒ `0016`.
    /// </summary>
    Task<CreateRetailBillResultDto> CreateAsync(CreateRetailBillDto input);

    /// <summary>**HUỶ (ĐẢO)** — nhập lại kho + đảo công nợ/điểm. Hoá đơn của ca đã kết ⇒ `0010`.</summary>
    Task<VoidRetailBillResultDto> VoidAsync(Guid id, VoidRetailBillDto input);
}

/// <summary>Kết quả tạo hoá đơn — kèm cờ **đã tồn tại** để UI offline biết mình vừa đồng bộ lại.</summary>
public class CreateRetailBillResultDto
{
    public RetailBillDto Bill { get; set; } = default!;

    /// <summary>`true` = hoá đơn đã tồn tại với **cùng** `ClientBillId`; **không** tạo mới, **không** đẩy tồn.</summary>
    public bool AlreadyExisted { get; set; }
}

/// <summary>**API CA THU NGÂN** (`WP-06`, màn kết ca — `nhanh-pos-module-map.md` §5.3).</summary>
public interface ICashSessionAppService
{
    /// <summary>Ca **đang mở** của quầy; `null` khi chưa mở ca (UI phải gọi `Open`).</summary>
    Task<CashSessionDto?> GetCurrentAsync(Guid warehouseId);

    Task<CashSessionDto> GetAsync(Guid id);

    Task<PagedResultDto<CashSessionDto>> GetListAsync(Guid? warehouseId, DateTime? fromUtc, DateTime? toUtc,
        int skipCount = 0, int maxResultCount = 50);

    /// <summary>Mở ca. Đã có ca mở ở quầy đó ⇒ `0012` (một quầy một ca).</summary>
    Task<CashSessionDto> OpenAsync(OpenCashSessionDto input);

    /// <summary>Kết ca — chốt `ExpectedCash` + `Variance`. Đã kết ⇒ `0010` (không quay ngược).</summary>
    Task<CashSessionDto> CloseAsync(Guid id, CloseCashSessionDto input);
}

/// <summary>**API TRẢ HÀNG** (`WP-06`, màn #14 `pos/return`).</summary>
public interface IRetailReturnAppService
{
    Task<PagedResultDto<RetailReturnDto>> GetListAsync(RetailReturnListInput input);

    Task<RetailReturnDto> GetAsync(Guid id);

    /// <summary>Tạo phiếu trả — nhập lại kho + trừ công nợ trước, hoàn tiền sau.</summary>
    Task<RetailReturnDto> CreateAsync(CreateRetailReturnDto input);
}
