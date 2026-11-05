using eCommerce.Inventory.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`S13` — APP SERVICE NGÂN SÁCH TỒN THEO KÊNH** (`INV-003`). 🔒 **Không** chứa nghiệp vụ: mọi
/// bất biến (`Σ Allotted ≤ OnHand`, `Reserved ≤ Allotted`, guard trạng thái, sổ cái) nằm ở
/// <see cref="StockAllotmentService"/> (**một** định nghĩa cho cả app service lẫn luồng giữ chỗ).
///
/// 🚨 **`[Authorize]` cấp CLASS** (quy ước #4) + **quyền hẹp hơn** ở từng method ghi.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class StockAllotmentAppService : ApplicationService, IStockAllotmentAppService
{
    private readonly IRepository<StockAllotment, Guid> _nganSachRepository;
    private readonly StockAllotmentService _nganSach;

    public StockAllotmentAppService(
        IRepository<StockAllotment, Guid> nganSachRepository,
        StockAllotmentService nganSach)
    {
        _nganSachRepository = nganSachRepository;
        _nganSach = nganSach;
    }

    public async Task<List<StockAllotmentDto>> GetListAsync(GetStockAllotmentsInput input)
    {
        // 📌 Lọc ở bộ nhớ: bảng ngân sách nhỏ (một dòng cho mỗi kho × SKU × kênh), và cách này **không**
        // phụ thuộc khả năng dịch biểu thức của provider. ⚠️ Nếu bảng lớn (hàng chục nghìn dòng) thì chuyển
        // sang `GetQueryableAsync()` và lọc ở SQL — ghi rõ để người sau biết **điều kiện** của lựa chọn.
        var danhSach = await _nganSachRepository.GetListAsync();

        if (input.WarehouseId is { } khoId)
        {
            danhSach = danhSach.Where(x => x.WarehouseId == khoId).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.Sku))
        {
            var sku = input.Sku.Trim();
            danhSach = danhSach.Where(x => x.Sku == sku).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.ChannelCode))
        {
            var kenh = InvChannelLimit.ChuẩnHoáKenh(input.ChannelCode);
            danhSach = danhSach.Where(x => x.ChannelCode == kenh).ToList();
        }

        return danhSach
            .OrderBy(x => x.ChannelCode, StringComparer.Ordinal)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    public async Task<StockAllotmentDto> GetAsync(Guid id)
        => ToDto(await _nganSachRepository.GetAsync(id));

    [Authorize(eCommerceConsts.Permissions.Inventory.AllotmentManage)]
    public async Task<StockAllotmentDto> DatNganSachAsync(DatNganSachInput input)
    {
        var ns = await _nganSach.DatNganSachAsync(
            input.WarehouseId, input.Sku, input.ChannelCode, input.SoLuong,
            input.ReferenceType, input.ReferenceId, input.IdempotencyKey, CurrentUser.Id);

        Logger.LogInformation(
            "NGÂN SÁCH TỒN: cấp/sửa {Kenh} · {Sku} · kho {Kho} ⇒ {SoLuong}. [S13-DAT-NGAN-SACH]",
            ns.ChannelCode, ns.Sku, ns.WarehouseId, ns.AllottedQuantity);

        return ToDto(ns);
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.AllotmentRelease)]
    public async Task<StockAllotmentDto> DongBangAsync(Guid id, LyDoNganSachInput input)
        => ToDto(await _nganSach.DongBangAsync(id, input.LyDo, CurrentUser.Id));

    [Authorize(eCommerceConsts.Permissions.Inventory.AllotmentRelease)]
    public async Task<StockAllotmentDto> MoDongBangAsync(Guid id, LyDoNganSachInput input)
        => ToDto(await _nganSach.MoDongBangAsync(id, input.LyDo, CurrentUser.Id));

    [Authorize(eCommerceConsts.Permissions.Inventory.AllotmentRelease)]
    public async Task<StockAllotmentDto> NhaAsync(Guid id, LyDoNganSachInput input)
    {
        var ns = await _nganSach.NhaAsync(id, input.LyDo, CurrentUser.Id);

        Logger.LogWarning(
            "NGÂN SÁCH TỒN: NHẢ ngân sách {Kenh} · {Sku} · kho {Kho} — lý do: {LyDo}. [S13-NHA-NGAN-SACH]",
            ns.ChannelCode, ns.Sku, ns.WarehouseId, input.LyDo);

        return ToDto(ns);
    }

    private static StockAllotmentDto ToDto(StockAllotment x) => new()
    {
        Id = x.Id,
        WarehouseId = x.WarehouseId,
        Sku = x.Sku,
        ChannelCode = x.ChannelCode,
        AllottedQuantity = x.AllottedQuantity,
        ReservedQuantity = x.ReservedQuantity,
        AvailableToPromise = x.AvailableToPromise,
        Priority = x.Priority,
        Status = x.Status.ToString(),
        FrozenAtUtc = x.FrozenAtUtc,
        ReleaseReason = x.ReleaseReason
    };
}
