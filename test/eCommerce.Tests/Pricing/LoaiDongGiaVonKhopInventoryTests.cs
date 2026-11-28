using eCommerce.Inventory;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — KHOÁ ĐẲNG THỨC SỐ CỦA CẶP ENUM ĐỊNH GIÁ** (P2 #4 review `PRC-001`).
///
/// `CostEngineService.AnhXa` đổi `LoaiChuyenDongGiaVon` (Inventory Contracts) → `LoaiDongGiaVon` (Domain Pricing)
/// bằng **cast số**: `(LoaiDongGiaVon)(int)dto.Loai`. Cast này đúng **chỉ khi** hai enum trùng số từng giá trị.
/// Không có test ⇒ ai chèn/đổi số giữa dải Inventory làm Pricing diễn giải sai loại ⇒ **sai bình quân im lặng**
/// (chú thích `TinhBinhQuanGiaQuyenDiDong.cs` nói *"adapter kiểm bằng test"* — đây là test đó).
/// </summary>
public class LoaiDongGiaVonKhopInventoryTests
{
    /// <summary>
    /// Mọi giá trị `LoaiDongGiaVon` phải có **cùng tên** ở `LoaiChuyenDongGiaVon` và **cùng số**; thêm giá trị
    /// ở một bên mà quên bên kia ⇒ test đỏ ngay (không đợi tới lúc tính ra số sai).
    /// </summary>
    [Fact]
    public void LoaiDongGiaVon_khop_1_1_so_voi_LoaiChuyenDongGiaVon_cua_Inventory()
    {
        var benPricing = Enum.GetValues<LoaiDongGiaVon>();
        var benInventory = Enum.GetValues<LoaiChuyenDongGiaVon>();

        Assert.Equal(benInventory.Length, benPricing.Length);

        foreach (var loaiPricing in benPricing)
        {
            // `Enum.Parse` theo TÊN: thiếu tên ở Inventory ⇒ ném ngay (không bỏ qua âm thầm).
            var loaiInventory = Enum.Parse<LoaiChuyenDongGiaVon>(loaiPricing.ToString());

            Assert.Equal((int)loaiPricing, (int)loaiInventory);

            // Đúng phép cast của `CostEngineService.AnhXa` — khoá cả hai chiều.
            Assert.Equal(loaiPricing, (LoaiDongGiaVon)(int)loaiInventory);
            Assert.Equal(loaiInventory, (LoaiChuyenDongGiaVon)(int)loaiPricing);
        }
    }
}
