using eCommerce.Application.Contracts.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **QUẢN LÝ ĐƠN TRÊN UI** (`WEB-001` · Phụ lục E.2 #1) — xem một đơn và **xác nhận / huỷ**.
///
/// 📌 **VÌ SAO LÁT CẮT NÀY RỦI RO THẤP (Phụ lục E.4):** cả **BA** chuyển trạng thái đã **đo** là chạy đúng trên đơn có
/// **định dạng số đơn `WEB-…`** — `XÁC NHẬN` (§65) · `HUỶ` (§69) · `TRẢ HÀNG` (§69) ⇒ phần còn lại là **nối nút bấm**.
///
/// 📌 **BÀI HỌC VÒNG 86 ĐÃ ÁP DỤNG:** lần đầu tôi render HTML **trong C#** và **quên token chống giả mạo**
/// ⇒ `POST` trả **`400`** ⇒ phải **gỡ tệp**. Nay dùng **`.cshtml`** + **`@Html.AntiForgeryToken()`** — đúng khuôn
/// đã chạy ở `Views/DatHang/Index.cshtml` (xem `tools/README-probe.md` **Phần E**).
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4). ⚠️ Không phải bảo mật dữ liệu: app service vẫn tự kiểm quyền.
/// </summary>
[Authorize]
public class DonHangController : Controller
{
    private readonly IDonHangAppService _donHang;
    private readonly IOrderStatusAppService _trangThai;
    private readonly IAuthorizationService _quyen;

    public DonHangController(
        IDonHangAppService donHang,
        IOrderStatusAppService trangThai,
        IAuthorizationService quyen)
    {
        _donHang = donHang;
        _trangThai = trangThai;
        _quyen = quyen;
    }

    /// <summary>`GET /DonHang/ChiTiet/{id}` — chi tiết đơn + hai nút. Đơn không tồn tại ⇒ **`404`** (ABP
    /// chuyển `EntityNotFoundException`), **không** phải `500`.
    /// 📌 `404` ở đây là **phép kiểm rẻ mà mạnh**: nó chứng minh **route + xác thực + DI + app service** đều chạy.</summary>
    public async Task<IActionResult> ChiTiet(Guid id)
    {
        var don = await _donHang.GetAsync(id);

        // 🆕 `SAL-003` #7 — **lịch sử trạng thái** trên UI. 🔒 Kiểm quyền **trước** khi gọi: người chỉ có
        // quyền xem đơn (không có `History.View`) vẫn xem được trang, chỉ **không thấy** lịch sử
        // — thay vì cả trang `403` vì một khối phụ.
        var coQuyenLichSu = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Orders.HistoryView);

        ViewBag.CoQuyenLichSu = coQuyenLichSu;

        ViewBag.LichSu = coQuyenLichSu
            ? (await _trangThai.GetStatusHistoryAsync(id, new PagedResultRequestDto { MaxResultCount = 100 })).Items
            : Array.Empty<OrderStatusHistoryDto>();

        return View(don);
    }

    /// <summary>`POST /DonHang/XacNhan/{id}` ⇒ `XacNhanAsync` (**chốt giữ chỗ ⇒ xuất kho**) ⇒ quay lại chi tiết.</summary>
    [HttpPost]
    public async Task<IActionResult> XacNhan(Guid id)
    {
        await _donHang.XacNhanAsync(id);

        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    /// <summary>`POST /DonHang/TraHang/{id}` ⇒ `TraHangAsync` (**nhập hàng trả về kho** + phiếu `CustomerReturn`)
    /// ⇒ quay lại chi tiết. 📌 API này **đã đo** chạy đúng trên đơn định dạng `WEB-…` (`web-001` §69), gồm cả
    /// công thức số phiếu `TH-{Number}-{n}` và tra **luỹ kế** đã trả.
    /// ⚠️ Cần **kho nhận hàng trả** — nên view có ô nhập `khoId` (khác hai nút kia, không cần tham số).</summary>
    [HttpPost]
    public async Task<IActionResult> TraHang(Guid id, Guid khoId)
    {
        await _donHang.TraHangAsync(id, new TraHangInput { WarehouseId = khoId });

        return RedirectToAction(nameof(ChiTiet), new { id });
    }
    /// <summary>`POST /DonHang/Huy/{id}` ⇒ `HuyDonAsync` (**nhả chỗ ngay**) ⇒ quay lại chi tiết.</summary>
    [HttpPost]
    public async Task<IActionResult> Huy(Guid id)
    {
        await _donHang.HuyDonAsync(id);

        return RedirectToAction(nameof(ChiTiet), new { id });
    }
}
