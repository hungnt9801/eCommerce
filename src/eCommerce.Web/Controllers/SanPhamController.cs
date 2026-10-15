using eCommerce.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG HÀNG ĐANG BÁN + ĐƠN GIÁ** (`WEB-001` — bước **2** của chuỗi mặt tiền, sau `GET /PhanBo`).
///
/// **VÌ SAO TRANG NÀY:** đây là **điểm vào** của việc mua hàng — khách cần thấy *"có hàng gì, giá bao nhiêu"*
/// **trước khi** giỏ hàng/đặt hàng có nghĩa. 📌 Dữ liệu lấy từ **cùng `IPriceListAppService`** mà API dùng
/// ⇒ **một nguồn sự thật** (không tự tính giá ở tầng UI — giá **phải** đến từ `Pricing`, `PRC-001` Q1).
///
/// 🔐 `[Authorize]` **cấp class** — quy ước #4. ⚠️ **Không** phải bảo mật dữ liệu: app service vẫn tự kiểm quyền.
///
/// 📌 **`Pricing` đã được nạp ở Web** (vòng 58 — bài học *"dùng app service của module nào ⇒ phải nạp module đó ở Web"*)
/// nên `IPriceListAppService` **dựng được** ở đây. Đây là **lần đầu quy tắc đó được áp dụng có chủ đích** thay vì
/// phát hiện sau khi `HTTP 500`.
/// </summary>
[Authorize]
public class SanPhamController : Controller
{
    private readonly IPriceListAppService _bangGia;

    public SanPhamController(IPriceListAppService bangGia)
    {
        _bangGia = bangGia;
    }

    /// <summary>`GET /SanPham` — bảng **(danh sách giá × SKU × đơn giá)**.</summary>
    public async Task<IActionResult> Index()
    {
        var bangGiaList = await _bangGia.GetListAsync();

        var dong = new List<SanPhamDongDto>();

        foreach (var bg in bangGiaList.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            var items = await _bangGia.GetItemsAsync(bg.Id);

            dong.AddRange(items
                .OrderBy(x => x.Sku, StringComparer.Ordinal)
                .Select(x => new SanPhamDongDto
                {
                    MaBangGia = bg.Code,
                    TenBangGia = bg.Name,
                    Sku = x.Sku,
                    DonGia = x.Amount
                }));
        }

        return View(dong);
    }
}

/// <summary>Một dòng hiển thị: **danh sách giá** nào đang niêm yết **SKU** này với **đơn giá** bao nhiêu.</summary>
public class SanPhamDongDto
{
    public string MaBangGia { get; set; } = default!;

    public string TenBangGia { get; set; } = default!;

    public string Sku { get; set; } = default!;

    public decimal DonGia { get; set; }
}
