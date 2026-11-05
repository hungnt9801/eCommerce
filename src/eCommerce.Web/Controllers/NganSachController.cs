using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using eCommerce.Inventory.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🆕 **`S18` — MÀN HÌNH NGÂN SÁCH TỒN THEO KÊNH** (`/NganSach`, `INV-003` §21).
///
/// 🔑 **VÌ SAO TRANG NÀY LÀ ĐIỀU KIỆN ĐỂ `S13` CÓ NGHĨA:** `IStockAllotmentAppService` **đã có** và đã có test,
/// nhưng **chưa có màn hình nào gọi nó** ⇒ người vận hành không có cách cấp ngân sách ⇒ *"tuỳ chọn theo dòng"*
/// (quyết định §21.2) **không bao giờ** được bật. 📌 Cùng loại nợ đã gặp ở `INV-003` §16.6 và `REC-001` §18:
/// **đường ghi đúng mà không có lối vào thì không giải quyết được vấn đề nó sinh ra để giải quyết.**
///
/// 🔐 `[Authorize]` **cấp class**; lớp bảo vệ dữ liệu thật là `[Authorize]` trên app service
/// (`eCommerce.Inventory` cho đọc, `…Allotment.Manage`/`.Release` cho ghi) ⇒ thiếu quyền vẫn `403` từ đó.
/// </summary>
[Authorize]
public class NganSachController : Controller
{
    private readonly IStockAllotmentAppService _nganSach;
    private readonly IChinhSachHanMucAppService _chinhSach;

    public NganSachController(
        IStockAllotmentAppService nganSach,
        IChinhSachHanMucAppService chinhSach)
    {
        _nganSach = nganSach;
        _chinhSach = chinhSach;
    }

    /// <summary>`GET /NganSach?kenh=&amp;sku=` — danh sách ngân sách (lọc tuỳ chọn).</summary>
    public async Task<IActionResult> Index(string? kenh = null, string? sku = null)
    {
        var ds = await _nganSach.GetListAsync(new GetStockAllotmentsInput
        {
            ChannelCode = string.IsNullOrWhiteSpace(kenh) ? null : kenh.Trim(),
            Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim()
        });

        // 🆕 `S18` — danh mục **kho có thật** cho ô chọn (không bắt người dùng dán GUID).
        // 📌 Dùng lại `IChinhSachHanMucAppService.GetKhoKhaDungAsync` — **một** nguồn danh mục kho cho cả
        // `/HanMucKenh` và `/NganSach`; nếu làm endpoint thứ hai thì hai màn hình có thể lệch danh sách.
        ViewBag.KhoKhaDung = await _chinhSach.GetKhoKhaDungAsync();

        ViewBag.KenhDangLoc = kenh;
        ViewBag.SkuDangLoc = sku;
        ViewBag.ThongBao = TempData["ThongBaoNganSach"] as string;
        ViewBag.Loi = TempData["LoiNganSach"] as string;

        return View(ds);
    }

    /// <summary>
    /// `POST /NganSach/Dat` — cấp/sửa ngân sách. 🚨 **`BusinessException` được bắt** để hiện đúng mã
    /// (`Inventory:0047` vượt tồn · `0049` sai guard) thay vì `500` chung — **không** nuốt guard
    /// (bắt rồi **dừng**, không ghi gì; khác hẳn bắt rồi đi tiếp như thành công).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dat(Guid warehouseId, string sku, string channelCode, int soLuong, string? idempotencyKey)
    {
        try
        {
            var kq = await _nganSach.DatNganSachAsync(new DatNganSachInput
            {
                WarehouseId = warehouseId,
                Sku = sku,
                ChannelCode = channelCode,
                SoLuong = soLuong,
                // 📌 Khoá idempotency **do UI sinh** khi người dùng bấm: bấm hai lần cùng form ⇒ không nhân đôi
                // ngân sách (nếu khoá được truyền lại). Để trống ⇒ mỗi lần bấm là một ý định mới (hợp lệ).
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
            });

            TempData["ThongBaoNganSach"] =
                $"Đã đặt ngân sách {kq.ChannelCode} · {kq.Sku} = {kq.AllottedQuantity} (ATP {kq.AvailableToPromise}).";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiNganSach"] = $"Không đặt được ngân sách: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Kiểm lại tồn kho và số lượng (tổng ngân sách không được vượt tồn thực)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index), new { kenh = channelCode });
    }

    /// <summary>`POST /NganSach/DoiTrangThai` — đóng băng / mở / nhả ngân sách. **`lyDo` bắt buộc** (`BR-12`).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiTrangThai(Guid id, string hanhDong, string lyDo)
    {
        try
        {
            var input = new LyDoNganSachInput { LyDo = lyDo };

            switch (hanhDong)
            {
                case "dong-bang":
                    await _nganSach.DongBangAsync(id, input);
                    TempData["ThongBaoNganSach"] = "Đã ĐÓNG BĂNG ngân sách (kênh ngừng bán, vẫn giữ ngân sách).";
                    break;

                case "mo-dong-bang":
                    await _nganSach.MoDongBangAsync(id, input);
                    TempData["ThongBaoNganSach"] = "Đã MỞ ĐÓNG BĂNG ngân sách.";
                    break;

                case "nha":
                    await _nganSach.NhaAsync(id, input);
                    TempData["ThongBaoNganSach"] = "Đã NHẢ ngân sách (kênh hết hàng theo ngân sách).";
                    break;

                default:
                    TempData["LoiNganSach"] = $"Hành động '{hanhDong}' không hợp lệ.";
                    break;
            }
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiNganSach"] = $"Không đổi được trạng thái: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Kiểm lại trạng thái hiện tại và lý do." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index));
    }
}

