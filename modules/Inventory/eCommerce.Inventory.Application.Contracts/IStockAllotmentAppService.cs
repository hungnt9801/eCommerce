using eCommerce.Inventory.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`S13` — HỢP ĐỒNG NGÂN SÁCH TỒN THEO KÊNH** (`INV-003`).
///
/// 🔐 Quyền ở **lớp hiện thực**: đọc = `eCommerce.Inventory` · cấp/sửa = `…Allotment.Manage` ·
/// đóng băng/nhả = `…Allotment.Release` (tách vì nhả ngân sách **làm kênh hết hàng**).
/// 📌 **Chưa có UI** ở lát cắt này (`S18` làm màn hình) — API có trước, đúng thứ tự dự án:
/// *"làm trang TRƯỚC khi khai menu"*, và API trước UI.
/// </summary>
public interface IStockAllotmentAppService : IApplicationService
{
    Task<List<StockAllotmentDto>> GetListAsync(GetStockAllotmentsInput input);

    Task<StockAllotmentDto> GetAsync(Guid id);

    /// <summary>
    /// Cấp/sửa ngân sách cho `(kho, SKU, kênh)`. **NÉM** `Inventory:0047` khi **tổng** ngân sách các kênh
    /// vượt `OnHand`; **NÉM** `Inventory:0049` khi số mới **nhỏ hơn** phần đang giữ chỗ.
    /// </summary>
    Task<StockAllotmentDto> DatNganSachAsync(DatNganSachInput input);

    /// <summary>`Active → Frozen` — ngừng bán qua kênh nhưng **không** nhả ngân sách.</summary>
    Task<StockAllotmentDto> DongBangAsync(Guid id, LyDoNganSachInput input);

    /// <summary>`Frozen → Active`.</summary>
    Task<StockAllotmentDto> MoDongBangAsync(Guid id, LyDoNganSachInput input);

    /// <summary>`→ Released` — nhả ngân sách; **NÉM** nếu còn phần đang giữ chỗ.</summary>
    Task<StockAllotmentDto> NhaAsync(Guid id, LyDoNganSachInput input);
}
