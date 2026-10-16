using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **ĐỐI SOÁT TỒN KHO TRÊN UI** (`REC-001`) — lịch sử lượt quét, dòng lệch, và **xử lý lệch**.
///
/// **VÌ SAO LÁT CẮT NÀY RỦI RO THẤP:** `DoiSoatAppService` (**4 endpoint**, `REC-001` §17) **đã có và đã đo**
/// (19/19 ca HTTP thật). Web chạy **in-process** và nạp `eCommerceApplicationModule` ⇒ app service **dùng được
/// ngay**, không cần module mới. 📌 Phần còn lại là **nối màn hình vào API đã kiểm**.
///
/// 📌 **BÀI HỌC VÒNG 86 ĐÃ ÁP DỤNG (từ `DonHangController`):** form `POST` **phải** có
/// `@Html.AntiForgeryToken()` — lần trước render HTML **trong C#** và **quên token** ⇒ `POST` trả **`400`**
/// ⇒ phải **gỡ cả tệp**. Nay dùng `.cshtml` + token trong **mọi** form.
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4). ⚠️ Không phải bảo mật dữ liệu: `DoiSoatAppService` **vẫn**
/// tự kiểm quyền `eCommerce.Inventory` (nên người có đăng nhập mà thiếu quyền ⇒ `403` từ app service).
/// </summary>
[Authorize]
public class DoiSoatController : Controller
{
    private readonly IDoiSoatAppService _doiSoat;

    public DoiSoatController(IDoiSoatAppService doiSoat)
    {
        _doiSoat = doiSoat;
    }

    /// <summary>
    /// `GET /DoiSoat` — **lịch sử các lượt quét** (mới nhất trước).
    /// 🔒 Mặc định lọc **`ChiConViecChuaXuLy = false`** (hiện **tất cả**), KHÔNG mặc định lọc *"còn việc"*:
    /// 📌 Người mới mở màn hình cần thấy **bức tranh đầy đủ** trước (kể cả lượt sạch) để biết hệ thống **có**
    /// đang chạy hay không. Nếu mặc định chỉ hiện *"còn việc"* thì một hệ thống **khoẻ mạnh** sẽ hiện
    /// **danh sách rỗng** — và người dùng tưởng **tính năng hỏng**.
    /// </summary>
    public async Task<IActionResult> Index(bool chiConViec = false, int trang = 1)
    {
        var soMoiTrang = 20;

        var ketQua = await _doiSoat.GetDanhSachLuotAsync(new LocLuotDoiSoatInput
        {
            ChiConViecChuaXuLy = chiConViec ? true : null,
            SkipCount = Math.Max(0, trang - 1) * soMoiTrang,
            MaxResultCount = soMoiTrang
        });

        ViewBag.ChiConViec = chiConViec;
        ViewBag.Trang = trang;

        return View(ketQua);
    }

    /// <summary>`GET /DoiSoat/ChiTiet/{id}` — các dòng lệch của **một** lượt, kèm ô nhập để **xử lý lệch**.</summary>
    public async Task<IActionResult> ChiTiet(Guid id, bool chiChuaXuLy = true)
    {
        // ⚠️ `GetLuotAsync` ném `Recon:0003` nếu không có ⇒ ABP chuyển thành lỗi nghiệp vụ (không phải `500`).
        var luot = await _doiSoat.GetLuotAsync(id);

        var dongLech = await _doiSoat.GetDanhSachDongLechAsync(new LocDongLechInput
        {
            ReconRunId = id,
            // 🔒 Mặc định Ở TRANG CHI TIẾT là **chỉ hiện dòng CHƯA xử lý** — ngược với trang danh sách.
            // 📌 Lý do khác nhau là **mục đích khác nhau**: trang danh sách để **nhìn tổng thể**;
            // trang chi tiết để **làm việc**. Hiện cả dòng đã xong sẽ chôn việc cần làm.
            ChiChuaXuLy = chiChuaXuLy ? true : null,
            MaxResultCount = 200
        });

        ViewBag.ChiChuaXuLy = chiChuaXuLy;

        return View(new DoiSoatChiTietDto { Luot = luot, DongLech = dongLech });
    }

    /// <summary>
    /// `POST /DoiSoat/XuLyLech` — đánh dấu **một dòng lệch đã xử lý** (kèm lý do + người xử lý) ⇒ quay lại chi tiết.
    ///
    /// 🚨 **`BusinessException` ĐƯỢC BẮT Ở ĐÂY — và đây KHÔNG phải "nuốt guard" (quan trọng, đọc kỹ):**
    /// `ReconRunMismatch.DanhDauDaXuLy` **NÉM** `Recon:0002` khi thiếu lý do/người xử lý — **đúng** quy ước #11
    /// (*guard phải NÉM, không im lặng*). Nhưng ở **UI MVC**, để exception nổi lên ⇒ `AbpExceptionFilter`
    /// biến thành **`HTTP 500` + trang lỗi chung** (*"An internal error occurred…"*) ⇒ người dùng
    /// **KHÔNG BIẾT** mình thiếu gì để sửa. 📌 Đây là ca **ĐÃ ĐO ĐƯỢC** (probe `rec-001` §18):
    /// `POST` thiếu lý do trả **`500`**, log có `"code": "eCommerce:Recon:0002"` nhưng thông điệp là
    /// *"An internal error occurred during your request!"*.
    ///
    /// 🔒 **Phân biệt hai việc (đây là mấu chốt):**
    /// <list type="bullet">
    ///   <item><b>Guard im lặng</b> (SAI) = bắt exception rồi **đi tiếp như thành công** ⇒ người dùng tưởng đã xong;</item>
    ///   <item><b>Hiện lỗi cho người dùng</b> (ĐÚNG, ở đây) = bắt exception rồi **dừng lại**, **không ghi gì**, và
    ///     **hiện đúng mã + thông điệp** của lỗi. Bằng chứng **vẫn** được giữ nguyên (entity **không** bị đụng
    ///     tới vì exception ném **trước** mọi phép gán — xem `DanhDauDaXuLy`).</item>
    /// </list>
    /// 📌 Chỉ bắt **`BusinessException`** (lỗi nghiệp vụ có mã); **KHÔNG** bắt `Exception` chung —
    /// lỗi hạ tầng/lập trình **phải** nổi lên thành `500` để **không** bị che.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLyLech(Guid dongLechId, Guid reconRunId, string nguoiXuLy, string lyDo)
    {
        try
        {
            await _doiSoat.PostXuLyLechAsync(dongLechId, new XuLyLechInput
            {
                NguoiXuLy = nguoiXuLy,
                LyDo = lyDo
            });
        }
        catch (Volo.Abp.BusinessException ex)
        {
            // 🔒 Hiện **mã lỗi** + thông điệp trên chính trang chi tiết ⇒ người dùng biết phải sửa gì.
            // ⚠️ KHÔNG nuốt: KHÔNG có `RedirectToAction` ở nhánh này ⇒ không có gì bị ghi nhầm là thành công.
            TempData["LoiXuLyLech"] = $"Không xử lý được lệch: [{ex.Code}] {(ex.Message is "An internal error occurred during your request!" or null ? "Thiếu lý do hoặc người xử lý." : ex.Message)}";
        }

        return RedirectToAction(nameof(ChiTiet), new { id = reconRunId });
    }
}

/// <summary>Mô hình cho view chi tiết lượt đối soát (gộp hai lời gọi app service).</summary>
public class DoiSoatChiTietDto
{
    public LuotDoiSoatDto Luot { get; set; } = default!;
    public Volo.Abp.Application.Dtos.PagedResultDto<DongLechDto> DongLech { get; set; } = default!;
}
