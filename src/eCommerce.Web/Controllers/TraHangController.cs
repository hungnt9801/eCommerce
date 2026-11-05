using System.Globalization;
using eCommerce.Application.Contracts.Sales;
using eCommerce.Sales;
using eCommerce.Sales.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🆕 **`S14` PHẦN E — MÀN HÌNH PHIẾU TRẢ HÀNG (RMA)** (`/TraHang`, `SAL-006` §32).
///
/// 🔑 **VÌ SAO TRANG NÀY LÀ ĐIỀU KIỆN ĐỂ `S14` CÓ NGHĨA:** `IOrderReturnAppService` **đã có** từ phần B và
/// đã có test + chứng từ kho từ phần C, nhưng **chưa có màn hình nào gọi nó** ⇒ người vận hành không có
/// cách duyệt/nhận hàng/đóng phiếu ⇒ quy trình RMA **không bao giờ** chạy được trên sản phẩm. 📌 Đúng loại
/// nợ đã gặp ở `INV-003` §16.6 / `REC-001` §18 / `S13`: **đường ghi đúng mà không có lối vào thì không
/// giải quyết được vấn đề nó sinh ra để giải quyết.**
///
/// 🔐 **KHÔNG thêm quyền mới.** `[Authorize]` ở đây chỉ là **xác thực**; lớp cưỡng chế thật là
/// `OrderReturnAppService` (`Orders` cho đọc · `Orders.Return.Approve` cho duyệt/từ chối/hoàn tiền ·
/// `Orders.Manage` cho nhận/đóng). 📌 Nút nào người dùng **không** có quyền thì **không render**, nhưng
/// quyền vẫn do app service quyết — ẩn nút **không phải** là bảo vệ.
///
/// 🚨 **BẮT `BusinessException` ĐỂ HIỆN ĐÚNG MÃ** (`eCommerce:Sales:0010`/`0011`, `eCommerce:Lock:0001`)
/// thay vì `500` chung. 🔒 Bắt rồi **DỪNG** — **không** nuốt guard: không có nhánh nào bắt lỗi rồi ghi tiếp.
/// </summary>
[Authorize]
public class TraHangController : Controller
{
    private readonly IOrderReturnAppService _phieuTra;
    private readonly IDonHangAppService _donHang;
    private readonly IAuthorizationService _quyen;

    public TraHangController(
        IOrderReturnAppService phieuTra,
        IDonHangAppService donHang,
        IAuthorizationService quyen)
    {
        _phieuTra = phieuTra;
        _donHang = donHang;
        _quyen = quyen;
    }

    /// <summary>`GET /TraHang?donId=` — danh sách phiếu trả (lọc theo đơn là **tuỳ chọn**).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? donId = null)
    {
        var danhSach = await _phieuTra.GetListAsync(donId);

        // 🔑 Hiện **SỐ ĐƠN** chứ không chỉ GUID: người vận hành nhận phiếu từ khách theo *số đơn*, và một
        // danh sách toàn GUID là màn hình **không dùng được**. 📌 Đọc qua `IDonHangAppService` (cùng quyền
        // `Orders` với danh sách phiếu) — không thêm endpoint thứ hai cho cùng một câu hỏi.
        ViewBag.SoDon = await LaySoDonAsync(danhSach.Select(x => x.OrderId));

        // 📌 Ẩn nút theo quyền = **thông tin cho người dùng**, KHÔNG phải lớp bảo vệ (xem chú thích lớp).
        ViewBag.CoQuyenDuyet = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Orders.ReturnApprove);
        ViewBag.CoQuyenNhan = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Orders.Manage);

        // 🆕 `S14G` — HUỶ phiếu dùng **đúng quyền của người yêu cầu trả** (`Orders.ReturnRequest`), không phải
        // quyền duyệt: huỷ là *"rút yêu cầu của mình"*, không phải *"quyết định thay khách"*.
        // 🔒 Chỉ để **ẩn/hiện** nút — lớp cưỡng chế vẫn là `OrderReturnAppService.HuyAsync`.
        ViewBag.CoQuyenHuy = await _quyen.IsGrantedAsync(eCommerceConsts.Permissions.Orders.ReturnRequest);

        ViewBag.DonId = donId;
        ViewBag.ThongBao = TempData["ThongBaoTraHang"] as string;
        ViewBag.Loi = TempData["LoiTraHang"] as string;

        return View(danhSach);
    }

    /// <summary>`POST /TraHang/Duyet` ⇒ `DuyetAsync` (`Requested → Approved`).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duyet(Guid id, Guid? donId)
    {
        try
        {
            var kq = await _phieuTra.DuyetAsync(id);

            TempData["ThongBaoTraHang"] = $"Đã DUYỆT phiếu {kq.ReturnNumber} (đơn chuyển sang ReturnApproved).";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "duyệt");
        }

        return QuayLai(donId);
    }

    /// <summary>`POST /TraHang/TuChoi` ⇒ `TuChoiAsync`. 🔒 **Lý do BẮT BUỘC** (`BR-008`).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoi(Guid id, string? lyDo, Guid? donId)
    {
        // 📌 Kiểm **cùng điều kiện** với `LyDoTraHangInput` ở biên UI ⇒ người dùng nhận câu giải thích,
        // không nhận `500` từ validation interceptor. 🔒 Guard thật vẫn ở entity (`TuChoi` ném nếu rỗng).
        var lyDoSach = (lyDo ?? string.Empty).Trim();

        if (lyDoSach.Length < 3 || lyDoSach.Length > 512)
        {
            TempData["LoiTraHang"] = "Lý do từ chối là BẮT BUỘC và phải từ 3 đến 512 ký tự (BR-008).";
            return QuayLai(donId);
        }

        try
        {
            var kq = await _phieuTra.TuChoiAsync(id, new LyDoTraHangInput { LyDo = lyDoSach });

            TempData["ThongBaoTraHang"] = $"Đã TỪ CHỐI phiếu {kq.ReturnNumber} và trả lại phần giữ chỗ trả.";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "từ chối");
        }

        return QuayLai(donId);
    }

    /// <summary>
    /// `POST /TraHang/Huy` ⇒ `HuyAsync` (`Requested|Approved → Cancelled`).
    ///
    /// 🆕 **`S14G` — ĐÓNG NỢ §32.5 #3:** `HuyAsync` đã có trong hợp đồng từ `S14B` nhưng **không có lối vào**
    /// ⇒ người vận hành không huỷ được phiếu khi khách đổi ý, dù quy trình *"yêu cầu trả"* **bắt buộc** phải
    /// rút lại được. 📌 Đúng loại nợ đã gặp ở phần E: **đường ghi đúng mà không có lối vào thì không giải
    /// quyết được vấn đề nó sinh ra để giải quyết.**
    ///
    /// 🔒 **Guard thật KHÔNG ở đây:** `OrderReturn.Huy` chỉ cho huỷ khi phiếu **chưa kết thúc** và
    /// **chưa nhận hàng** (`GoodsStatus` ∉ `Received|PartiallyReceived`), đồng thời **bắt buộc** có lý do
    /// (`BR-008`). View chỉ **ẩn nút** theo trạng thái — ẩn nút **không phải** là bảo vệ.
    ///
    /// 🔑 **Đối xứng `GiamTra`:** huỷ đi qua `OrderReturnService.TraLaiDaGiuAsync` ⇒ `OrderLine.GiamTra` trả lại
    /// phần **giữ chỗ trả** đã `TangTra` lúc tạo phiếu. 📌 Nếu controller chỉ đổi `Status` mà bỏ qua đường đó
    /// thì hàng bị **khoá vĩnh viễn** — đúng thứ mà ca E2E `S14-E18` đo bằng DB.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Huy(Guid id, string? lyDo, Guid? donId)
    {
        // 📌 Kiểm **cùng ngưỡng** với `LyDoTraHangInput` (`[Required]` + `[StringLength(512, MinimumLength = 3)]`)
        // ⇒ người dùng nhận câu giải thích thay vì `500` của validation interceptor.
        // 🔒 Guard thật vẫn ở entity: `Huy` ném `Sales:0010` khi lý do rỗng.
        var lyDoSach = (lyDo ?? string.Empty).Trim();

        if (lyDoSach.Length < 3 || lyDoSach.Length > 512)
        {
            TempData["LoiTraHang"] = "Lý do huỷ là BẮT BUỘC và phải từ 3 đến 512 ký tự (BR-008).";
            return QuayLai(donId);
        }

        try
        {
            var kq = await _phieuTra.HuyAsync(id, new LyDoTraHangInput { LyDo = lyDoSach });

            TempData["ThongBaoTraHang"] = $"Đã HUỶ phiếu {kq.ReturnNumber} và trả lại phần giữ chỗ trả.";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "huỷ");
        }

        return QuayLai(donId);
    }

    /// <summary>
    /// `POST /TraHang/NhanHang` ⇒ `NhanHangAsync` (mỗi dòng: số nhận + `Restock|Damaged|ReturnToSupplier`).
    ///
    /// 📌 Màn hình gửi **cả phiếu** trong một lần (mỗi dòng đều có số nhận ≥ 1). Từ **`S14F`**, `GoodsStatus`
    /// được tính trên **toàn bộ** dòng của phiếu ở tầng dịch vụ (`daNhanDuTatCaDong`) ⇒ gọi lẻ từng dòng **cũng**
    /// không thể kết luận *"đã nhận đủ"* khi còn dòng chưa về (`SAL-006` §33.1).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NhanHang(
        Guid id, Guid? donId,
        List<Guid>? dongTraId, List<int>? soLuongNhan, List<string>? xuLy)
    {
        var dsDong = new List<ReceiveOrderReturnLineInput>();

        for (var i = 0; i < (dongTraId?.Count ?? 0); i++)
        {
            var soLuong = i < (soLuongNhan?.Count ?? 0) ? soLuongNhan![i] : 0;
            var cachXuLy = i < (xuLy?.Count ?? 0) ? xuLy![i] : string.Empty;

            // 📌 Chặn sớm ở biên để người dùng thấy câu giải thích ngay tại chỗ; 🚨 từ **`S14F`** tầng domain
            // **cũng** ném `eCommerce:Sales:0010` cho `0` + disposition (`SAL-006` §33.2) ⇒ đây không còn là
            // hàng rào duy nhất, và sẽ không thể lọt xuống `CHECK` của DB để thành `500`.
            if (soLuong < 1)
            {
                TempData["LoiTraHang"] =
                    "Số lượng nhận mỗi dòng phải ≥ 1 (dòng không nhận thì không đưa vào phiếu trả).";
                return QuayLai(donId);
            }

            dsDong.Add(new ReceiveOrderReturnLineInput
            {
                ReturnLineId = dongTraId![i],
                ReceivedQuantity = soLuong,
                Disposition = cachXuLy
            });
        }

        if (dsDong.Count == 0)
        {
            TempData["LoiTraHang"] = "Không có dòng nào để ghi nhận hàng hoàn.";
            return QuayLai(donId);
        }

        try
        {
            var kq = await _phieuTra.NhanHangAsync(id, new ReceiveOrderReturnInput { Lines = dsDong });

            TempData["ThongBaoTraHang"] =
                $"Đã NHẬN HÀNG phiếu {kq.ReturnNumber}: {kq.Status} · hàng {kq.GoodsStatus}.";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "nhận hàng");
        }

        return QuayLai(donId);
    }

    /// <summary>`POST /TraHang/Dong` ⇒ `DongAsync` (chỉ khi hàng đã về đủ — guard ở entity).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dong(Guid id, Guid? donId)
    {
        try
        {
            var kq = await _phieuTra.DongAsync(id);

            TempData["ThongBaoTraHang"] = $"Đã ĐÓNG phiếu {kq.ReturnNumber}.";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "đóng");
        }

        return QuayLai(donId);
    }

    /// <summary>
    /// `POST /TraHang/GhiNhanHoanTien` ⇒ `GhiNhanHoanTienAsync` (`Q4`: **chỉ ghi nhận**, không gọi cổng).
    ///
    /// 📌 Nhận `string` rồi **tự parse** theo `InvariantCulture`: dùng `decimal` trực tiếp thì cách hiểu dấu
    /// thập phân phụ thuộc **culture của máy chủ** ⇒ cùng một số do trình duyệt gửi (`100.50`) có thể bind
    /// hỏng ở máy `vi-VN` và thành `0` — một lỗi **chỉ hiện trên môi trường khác**.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhiNhanHoanTien(Guid id, string? soTien, Guid? donId)
    {
        // 🚨 Giữ **cùng ngưỡng** với `GhiNhanHoanTienInput.SoTien` (`[Range(0.01, …)]`) ⇒ không để
        // validation interceptor biến một con số gõ sai thành `500`.
        if (!decimal.TryParse((soTien ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var tien)
            || tien < 0.01m)
        {
            TempData["LoiTraHang"] = "Số tiền hoàn phải là số > 0 (đơn vị tiền của phiếu).";
            return QuayLai(donId);
        }

        try
        {
            var kq = await _phieuTra.GhiNhanHoanTienAsync(id, new GhiNhanHoanTienInput { SoTien = tien });

            TempData["ThongBaoTraHang"] =
                $"Đã GHI NHẬN hoàn tiền phiếu {kq.ReturnNumber}: {tien:N0} {kq.RefundCurrency} " +
                $"(luỹ kế {kq.RefundedAmount:N0}).";
        }
        catch (BusinessException ex)
        {
            GhiLoi(ex, "ghi nhận hoàn tiền");
        }

        return QuayLai(donId);
    }

    /// <summary>
    /// 🚨 Hiện **ĐÚNG MÃ** nghiệp vụ (`eCommerce:Sales:0010` …) + lý do, thay vì `500`.
    /// 📌 ABP trả message chung (`An internal error occurred…`) khi không có bản dịch ⇒ thay bằng hướng dẫn
    /// đọc được, **nhưng mã lỗi luôn được in** — mã là thứ dùng để tra tài liệu/đối chiếu log.
    /// </summary>
    private void GhiLoi(BusinessException ex, string hanhDong)
    {
        var chiTiet = string.IsNullOrWhiteSpace(ex.Message)
            || ex.Message == "An internal error occurred during your request!"
                ? "Kiểm lại trạng thái hiện tại của phiếu (và quyền của bạn) rồi thử lại."
                : ex.Message;

        TempData["LoiTraHang"] = $"Không {hanhDong} được phiếu: [{ex.Code}] {chiTiet}";
    }

    private IActionResult QuayLai(Guid? donId)
        => donId is { } id
            ? RedirectToAction(nameof(Index), new { donId = id })
            : RedirectToAction(nameof(Index));

    /// <summary>`OrderId → Number`. 📌 Đơn đã bị xoá cứng (không có đường xoá trên sản phẩm) ⇒ hiện 8 ký tự
    /// đầu của GUID thay vì làm **cả trang** `404` vì một dòng.</summary>
    private async Task<Dictionary<Guid, string>> LaySoDonAsync(IEnumerable<Guid> orderIds)
    {
        var ketQua = new Dictionary<Guid, string>();

        foreach (var orderId in orderIds.Distinct())
        {
            try
            {
                ketQua[orderId] = (await _donHang.GetAsync(orderId)).Number;
            }
            catch (EntityNotFoundException)
            {
                ketQua[orderId] = orderId.ToString()[..8];
            }
        }

        return ketQua;
    }
}
