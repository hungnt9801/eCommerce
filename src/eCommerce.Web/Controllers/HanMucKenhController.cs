using eCommerce.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **HẠN MỨC PHÂN BỔ THEO KÊNH TRÊN UI** (`INV-003`) — **ĐÓNG MỤC (1) CỦA GĐ1**:
/// *"đường GHI chuẩn cho admin + API quản lý hạn mức kênh `InvChannelLimits`"*.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO TRANG NÀY QUAN TRỌNG HƠN VẺ NGOÀI CỦA NÓ:**
///
/// `ChinhSachHanMucAppService` **đã có và đã đo** (`INV-003` §17). 📌 Nhưng trước lượt này **chưa có
/// màn hình nào gọi nó** ⇒ đường ghi chuẩn **tồn tại mà không ai dùng được**. 🔒 Hệ quả **đã đo được**
/// (`inv-003` §16.6): người vận hành **phải** ghi SQL tay rồi **xoá cache tay** — và cache setting của ABP
/// có **HAI tầng** (Redis + bộ nhớ tiến trình) với mẫu key dễ sai ⇒ **4 phép đo cho kết quả SAI**.
/// ⇒ Một app service đúng mà **không có lối vào** thì **không** giải quyết được vấn đề nó sinh ra để giải quyết.
///
/// 📌 **VÌ SAO CHỌN TRANG NÀY TRƯỚC `/KhachHang` VÀ `/ThanhToan`** (quyết định có lý do, không phải tuỳ tiện):
/// `IChinhSachHanMucAppService` nằm ở **`eCommerceApplicationModule`** — mà `eCommerce.Web` **đã** nạp.
/// ⚠️ Còn `ICustomerAppService` và `IPaymentAppService` nằm ở **module riêng** (`CustomersApplicationModule`,
/// `PaymentHubApplicationModule`) mà Web **CHƯA** nạp ⇒ hai trang đó cần **thêm `ProjectReference` + `DependsOn`**
/// trước. 🔑 Làm trang **không cần wiring mới** trước ⇒ có **giá trị chạy được ngay**, và **đo được** bằng route + HTTP.
///
/// 🔐 `[Authorize]` **cấp class** (quy ước #4). ⚠️ Đây **không** phải lớp bảo vệ dữ liệu:
/// `ChinhSachHanMucAppService` **vẫn** tự kiểm `ChannelLimits.Default` (đọc) và `ChannelLimits.Manage` (ghi)
/// ⇒ người đã đăng nhập mà thiếu quyền vẫn nhận `403` **từ app service**.
/// 📌 Khai đúng ở **menu** (`MenuDieuHuong`) là `ChannelLimits.Default` — vì mục menu chỉ cần **mở được** trang;
/// nút ghi vẫn tự chặn nếu thiếu `Manage`. 🔑 Đây là quy tắc đã ghi trong `web-001` §95.5.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
[Authorize]
public class HanMucKenhController : Controller
{
    private readonly IChinhSachHanMucAppService _hanMuc;

    public HanMucKenhController(IChinhSachHanMucAppService hanMuc)
    {
        _hanMuc = hanMuc;
    }

    /// <summary>
    /// `GET /HanMucKenh` — **chính sách đang áp** + **bảng hạn mức** + **catalog kênh** + **catalog kho**.
    ///
    /// 📌 **VÌ SAO TRẢ CẢ CHÍNH SÁCH LẪN BẢNG TRÊN MỘT TRANG:** hạn mức của một dòng **không có nghĩa**
    /// nếu không biết **đơn vị** (`Percent` hay `Absolute`) — 📌 *"100"* là **100%** hay **100 chiếc**?
    /// 🔒 Tách hai trang ⇒ người dùng đọc một con số **mà không biết đơn vị** ⇒ **hiểu sai** cấu hình của chính mình.
    ///
    /// 🆕 `S7` — **LỌC THEO KHO + PHÂN TRANG** (bàn giao §7 việc 6). 🔒 `trang` là **1-based** ở URL;
    /// `SkipCount` tính ở đây (📌 để view không phải biết API dùng 0-based — chỗ dễ lệch một đơn vị).
    /// 🆕 **`sua`** = Id dòng cần **ĐIỀN SẴN** vào form (nút *"Sửa"* trong bảng, đóng nợ §18.7 #3).
    /// 🔒 Nó **chỉ điền sẵn** — đường ghi vẫn là **một** (`POST DatHanMuc`, upsert theo `(kênh, kho)`)
    /// ⇒ không sinh ra "hai đường ghi" cho cùng một khoá nghiệp vụ.
    /// </summary>
    public async Task<IActionResult> Index(string? kenh = null, string? kho = null, int trang = 1, Guid? sua = null)
    {
        const int soMoiTrang = 20;

        // ⚠️ Gọi **tuần tự, KHÔNG `Task.WhenAll`**: hai lời gọi dùng **cùng `DbContext` scoped** của request
        // ⇒ chạy song song trên một `DbContext` là **lỗi** (`A second operation started on this context`).
        // 📌 Đây là bẫy đã gặp thật trong dự án (đã ghi ở `tools\README-probe.md`), nên ghi lại ở đây.
        var chinhSach = await _hanMuc.GetChinhSachAsync();

        Guid? khoId = null;
        if (!string.IsNullOrWhiteSpace(kho) && !string.Equals(kho, "ca-kenh", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(kho, out var parsed))
            {
                // 📌 Tham số lạ trên URL **không** được biến thành 500: bỏ lọc và **nói ra** (view đọc cờ này).
                ViewBag.LocKhoSai = true;
            }
            else
            {
                khoId = parsed;
            }
        }
        else if (string.Equals(kho, "ca-kenh", StringComparison.OrdinalIgnoreCase))
        {
            // 🔑 `Guid.Empty` = **chỉ những dòng KHÔNG gắn kho** (`WarehouseId IS NULL`) — một lựa chọn THẬT.
            khoId = Guid.Empty;
        }

        var trangHopLe = Math.Max(1, trang);

        var danhSach = await _hanMuc.GetDanhSachPhanTrangAsync(new LocHanMucKenhInput
        {
            ChannelCode = kenh,
            WarehouseId = khoId,
            SkipCount = (trangHopLe - 1) * soMoiTrang,
            MaxResultCount = soMoiTrang
        });

        var ken = await _hanMuc.GetKenhKhaDungAsync();
        var cacKho = await _hanMuc.GetKhoKhaDungAsync();

        ViewBag.KenhDangLoc = kenh;
        ViewBag.KhoDangLoc = kho;
        ViewBag.KenhKhaDung = ken;
        ViewBag.KhoKhaDung = cacKho;
        ViewBag.Trang = trangHopLe;
        ViewBag.SoMoiTrang = soMoiTrang;
        ViewBag.SuaId = sua;

        return View(new HanMucKenhManHinhDto
        {
            ChinhSach = chinhSach,
            DanhSach = danhSach
        });
    }

    /// <summary>
    /// `POST /HanMucKenh/DatHanMuc` — thêm **hoặc** cập nhật hạn mức cho `(kênh, kho)`.
    ///
    /// 🚨 **`BusinessException` ĐƯỢC BẮT Ở ĐÂY — và đây KHÔNG phải "nuốt guard" (đọc kỹ):**
    /// `DatHanMucAsync` **NÉM** `eCommerce:Inventory:0046` khi **mã kênh không tồn tại** — **đúng** quy ước #11.
    /// Nhưng ở **UI MVC**, để exception nổi lên ⇒ `AbpExceptionFilter` biến thành **`HTTP 500` + trang lỗi chung**
    /// ⇒ người dùng **KHÔNG BIẾT** mình gõ sai mã kênh. 📌 Ca này **đã đo được** ở `REC-001` §18
    /// (`POST` thiếu lý do trả `500`, log có mã nhưng thông điệp là *"An internal error occurred…"*).
    ///
    /// 🔒 **Phân biệt hai việc:** **guard im lặng** (SAI) = bắt rồi **đi tiếp như thành công**;
    /// **hiện lỗi cho người dùng** (ĐÚNG, ở đây) = bắt rồi **dừng**, **không ghi gì**, và **hiện đúng mã + thông điệp**.
    /// 📌 Chỉ bắt **`BusinessException`**; **KHÔNG** bắt `Exception` chung — lỗi hạ tầng **phải** nổi lên `500`.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DatHanMuc(string channelCode, Guid? warehouseId, decimal giaTri, string? hanhDong, string? donVi)
    {
        try
        {
            await _hanMuc.DatHanMucAsync(new DatHanMucKenhInput
            {
                ChannelCode = channelCode,
                WarehouseId = warehouseId,
                GiaTri = giaTri,
                HanhDong = hanhDong,

                // 🔑 `null`/rong = **ke thua** setting chung (KHONG phai mot gia tri thu ba).
                DonVi = donVi
            });

            TempData["ThongBaoHanMuc"] = $"Đã lưu hạn mức cho kênh '{channelCode}'.";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            // 🔒 Hiện **mã lỗi** + thông điệp ⇒ người dùng biết phải sửa gì (chứ không phải "500 chung chung").
            // ⚠️ KHÔNG nuốt: KHÔNG đặt `ThongBaoHanMuc` ở nhánh này ⇒ không có gì bị ghi nhầm là thành công.
            TempData["LoiHanMuc"] = $"Không lưu được hạn mức: [{ex.Code}] " +
                $"{(ex.Message is "An internal error occurred during your request!" or null ? "Kiểm lại mã kênh (phải tồn tại trong danh mục kênh)." : ex.Message)}";
        }

        return RedirectToAction(nameof(Index), new { kenh = channelCode });
    }

    /// <summary>`POST /HanMucKenh/XoaHanMuc` — xoá **mềm** một dòng hạn mức (quay về giá trị dùng chung).
    /// 🔒 Xoá **mềm**: giữ **vết cấu hình** để lịch sử chặn đơn (`Inventory:0042`) còn **đối chiếu được**.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaHanMuc(Guid id, string? kenh)
    {
        try
        {
            await _hanMuc.XoaHanMucAsync(id);
            TempData["ThongBaoHanMuc"] = "Đã xoá hạn mức (kênh trở về dùng giá trị dùng chung).";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiHanMuc"] = $"Không xoá được hạn mức: [{ex.Code}] {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { kenh });
    }

    /// <summary>
    /// `POST /HanMucKenh/DoiChinhSach` — đổi **chính sách** (`Inv.ChannelLimit.*` / `Recon.*`).
    /// 🔒 Đi qua `ISettingManager` ⇒ **cache HAI TẦNG tự xoá** (đây là **lý do** đường ghi chuẩn tồn tại —
    /// xem chú thích đầu lớp).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiChinhSach(
        string? donViHanMuc,
        string? hanhDongHanMuc,
        string? phamViHanMuc,
        decimal? giaTriMacDinh,
        string? nguongDoiSoatDonVi,
        decimal? nguongDoiSoatGiaTri,
        string? nguongDoiSoatHanhDong,
        int? luuTruDoiSoatSoNgay)
    {
        try
        {
            await _hanMuc.DoiChinhSachAsync(new DoiChinhSachInput
            {
                DonViHanMuc = donViHanMuc,
                HanhDongHanMuc = hanhDongHanMuc,
                PhamViHanMuc = phamViHanMuc,
                GiaTriMacDinh = giaTriMacDinh,
                NguongDoiSoatDonVi = nguongDoiSoatDonVi,
                NguongDoiSoatGiaTri = nguongDoiSoatGiaTri,
                NguongDoiSoatHanhDong = nguongDoiSoatHanhDong,
                LuuTruDoiSoatSoNgay = luuTruDoiSoatSoNgay
            });

            TempData["ThongBaoHanMuc"] = "Đã đổi chính sách (cache setting đã tự xoá).";
        }
        catch (Volo.Abp.BusinessException ex)
        {
            TempData["LoiHanMuc"] = $"Không đổi được chính sách: [{ex.Code}] {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}

/// <summary>Mô hình cho màn hình hạn mức — gộp hai lời gọi app service (chính sách + danh sách).</summary>
public class HanMucKenhManHinhDto
{
    public ChinhSachHanMucDto ChinhSach { get; set; } = default!;

    /// <summary>🆕 `S7` — danh sách nay **phân trang** (giữ `TotalCount` thật để view in *"trang x/y"*).</summary>
    public Volo.Abp.Application.Dtos.PagedResultDto<HanMucKenhDto> DanhSach { get; set; } = default!;
}
