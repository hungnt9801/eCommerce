using System.ComponentModel.DataAnnotations;
using eCommerce.Inventory;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

// ══════════════════════════════════════════════════════════════════════════════════════════════
// 🚨 `INV-003` — **ĐƯỜNG GHI CHUẨN CHO ADMIN** (mục 5 việc 4 của gói bàn giao).
//
// **VÌ SAO ĐỢI TỚI NAY MỚI CÓ:** bảng `InvChannelLimits` (16/10/2026) đã chạy thật, nhưng **chưa có đường ghi
// nào ngoài SQL tay**. Mà ghi SQL tay + xoá cache tay chính là cái bẫy đã làm **4 phép đo cho kết quả SAI**
// (`inv-003` §16.6): cache setting của ABP có **2 tầng** (Redis + **bộ nhớ tiến trình**) và mẫu key dễ sai.
// ⇒ App service dưới đây dùng `ISettingManager` (**tự xoá cache cả 2 tầng**) và repository của ABP — tức là
// **không** để người vận hành phải chạm vào `AbpSettings`/Redis nữa.
// ══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Chính sách "cho phép chọn" đang áp cho **hạn mức theo kênh** (`Inv.ChannelLimit.*`).</summary>
public class ChinhSachHanMucDto
{
    /// <summary>`Percent` hoặc `Absolute` — 🚨 **ĐANG LÀ SETTING DÙNG CHUNG** (không phải thuộc tính từng dòng).</summary>
    public string DonVi { get; set; } = default!;

    /// <summary>Việc cần làm khi vượt hạn mức: `Block` (chặn) hoặc `Warn` (cho qua + cảnh báo).</summary>
    public string HanhDong { get; set; } = default!;

    /// <summary>`Channel` (chỉ xét hạn mức cả kênh) hoặc `ChannelWarehouse` (xét cả hạn mức theo kho).</summary>
    public string PhamVi { get; set; } = default!;

    /// <summary>Giá trị hạn mức cho kênh **chưa** cấu hình riêng (`0` = **TẮT**).</summary>
    public decimal GiaTriMacDinh { get; set; }

    /// <summary>Chính sách **đối soát** đang áp (`Recon.*`) — đọc chung ở đây để màn hình cấu hình có **một** nguồn.</summary>
    public string NguongDoiSoatDonVi { get; set; } = default!;
    public decimal NguongDoiSoatGiaTri { get; set; }
    public string NguongDoiSoatHanhDong { get; set; } = default!;

    /// <summary>
    /// 🚨 **Tên setting nào đang bị giá trị LẠ** (rỗng ⇒ tất cả hợp lệ).
    /// 📌 Vì sao trả ra: hàm đọc chính sách **không ném** khi gặp giá trị lạ — nó dùng **mặc định an toàn**
    /// để không làm chết đường đặt hàng. Nhưng nếu không **nói ra** thì người vận hành **tin** cấu hình của
    /// mình có hiệu lực (đúng loại "tắt im lặng" các cơ chế của dự án tồn tại để chặn).
    /// </summary>
    public List<string> SettingKhongHopLe { get; set; } = [];

    /// <summary>Ghi chú **giới hạn đã biết** để màn hình admin không hứa quá (nợ đang mở của `INV-003`).</summary>
    public List<string> GhiChu { get; set; } = [];

    /// <summary>
    /// 🆕 `S7` — **SỐ NGÀY LƯU TRỮ dòng lệch đã xử lý** (`Recon.Retention.Days`, `REC-001` §17.8 #1).
    /// 🔒 **`0` = TẮT** (giữ tất cả). 📌 Hiện ở **cùng** màn hình với các chính sách khác vì đây là
    /// *"chính sách cho phép chọn"* — và vì tách màn hình thì người vận hành **không biết** lịch sử đang
    /// bị dọn theo mức nào.
    /// </summary>
    public int LuuTruDoiSoatSoNgay { get; set; }

    /// <summary>`false` = giá trị setting **không hợp lệ** ⇒ hệ thống đang dùng **mặc định an toàn**
    /// (xem `SettingKhongHopLe`).</summary>
    public bool LuuTruDoiSoatHopLe { get; set; }
}

/// <summary>Đổi chính sách (`Inv.ChannelLimit.*` / `Recon.*`) — ghi qua `ISettingManager` (tự xoá cache 2 tầng).</summary>
public class DoiChinhSachInput
{
    /// <summary>`Percent` | `Absolute` | `null`/rỗng = **không đổi**.</summary>
    [StringLength(32)]
    public string? DonViHanMuc { get; set; }

    /// <summary>`Block` | `Warn`.</summary>
    [StringLength(32)]
    public string? HanhDongHanMuc { get; set; }

    /// <summary>`Channel` | `ChannelWarehouse`.</summary>
    [StringLength(32)]
    public string? PhamViHanMuc { get; set; }

    /// <summary>Giá trị hạn mức mặc định (`0` = TẮT). Gửi `null` = **không đổi**.</summary>
    [Range(0, 1_000_000)]
    public decimal? GiaTriMacDinh { get; set; }

    /// <summary>`Percent` | `Absolute` cho **ngưỡng đối soát**.</summary>
    [StringLength(32)]
    public string? NguongDoiSoatDonVi { get; set; }

    /// <summary>Giá trị ngưỡng đối soát.</summary>
    [Range(0, 1_000_000)]
    public decimal? NguongDoiSoatGiaTri { get; set; }

    /// <summary>`Warn` | `RequireConfirmation`.</summary>
    [StringLength(32)]
    public string? NguongDoiSoatHanhDong { get; set; }

    /// <summary>
    /// 🆕 `S7` — số ngày lưu trữ dòng lệch **đã xử lý** (`Recon.Retention.Days`).
    /// 🔒 Gửi `0` = **TẮT** retention (hợp lệ, có chủ ý). ⚠️ Còn **đường DỌN** nhận `0` thì **NÉM** —
    /// hai ngữ nghĩa khác nhau: `0` ở *cấu hình* là *"không dọn"*, `0` ở *lệnh dọn* là *"xoá hết"* (vô nghĩa).
    /// </summary>
    [Range(0, 36_500)]
    public int? LuuTruDoiSoatSoNgay { get; set; }
}

/// <summary>Một dòng hạn mức phân bổ của kênh (`InvChannelLimits`).</summary>
public class HanMucKenhDto
{
    public Guid Id { get; set; }
    public string ChannelCode { get; set; } = default!;

    /// <summary>`null` = **hạn mức CẢ KÊNH**; có giá trị = hạn mức **của kênh trong kho đó**.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Tên kho (đọc kèm để màn hình không phải hiện GUID "trần") — `null` khi là hạn mức cả kênh.</summary>
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }

    public decimal GiaTri { get; set; }

    /// <summary>`Block` | `Warn` — **hành động riêng của dòng này**.</summary>
    public string HanhDong { get; set; } = default!;

    /// <summary>
    /// 🚨 **ĐƠN VỊ HIỆU LỰC CỦA DÒNG NÀY** (`INV-003` mục 5 việc 2) — `Percent` | `Absolute` |
    /// <c>null</c> = **kế thừa** setting dùng chung `Inv.ChannelLimit.Unit`.
    ///
    /// 🔑 **VÌ SAO TRẢ RA Ở ĐÂY, KHÔNG ĐỂ CLIENT TỰ TRA SETTING:** `GiaTri` **không có nghĩa** nếu thiếu đơn vị —
    /// 📌 `100` là **100%** hay **100 chiếc**? ⚠️ Nếu màn hình hiện `GiaTri` mà **không** hiện đơn vị thì
    /// người vận hành **đọc sai cấu hình của chính mình** — và đó là loại sai **không có triệu chứng**.
    /// 🔒 Vì vậy app service trả **đơn vị hiệu lực** (đã trộn dòng + setting) chứ không trả "đơn vị thô của dòng".
    /// </summary>
    public string? DonVi { get; set; }

    /// <summary>`0` ⇒ dòng này **TẮT** (quy ước toàn hệ thống) — trả cờ sẵn để UI không phải tự suy.</summary>
    public bool DangTat => GiaTri <= 0m;

    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>Thêm/cập nhật **một** hạn mức kênh (khoá nghiệp vụ `(kênh, kho)` — `kho` rỗng = cả kênh).</summary>
public class DatHanMucKenhInput
{
    /// <summary>Mã kênh — **BẮT BUỘC** và **phải tồn tại** trong danh mục `ChnChannels`.</summary>
    [Required]
    [StringLength(eCommerceConsts.InventoryLimits.MaKenhToiDa)]
    public string ChannelCode { get; set; } = default!;

    /// <summary>Để trống = hạn mức **cả kênh**.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Giá trị hạn mức (`0` = TẮT).</summary>
    [Range(0, 1_000_000)]
    public decimal GiaTri { get; set; }

    /// <summary>`Block` | `Warn` — để trống = `Block` (**fail-closed**: không bán quá hạn mức).</summary>
    [StringLength(32)]
    public string? HanhDong { get; set; }

    /// <summary>
    /// `Percent` | `Absolute` — 🔑 **đơn vị của RIÊNG dòng này**. Để trống = **kế thừa** setting
    /// `Inv.ChannelLimit.Unit` (⚠️ **không** gửi `null` để "xoá override" — xem `GhiChu` của `XoaHanMucAsync`).
    /// </summary>
    [StringLength(32)]
    public string? DonVi { get; set; }
}

/// <summary>Một kênh **có thật** trong danh mục — dùng để màn hình admin chọn thay vì gõ tay (chống gõ sai mã).</summary>
public class KenhKhaDungDto
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Kênh này có **chiếm tồn kho** không (`Channel.HoldsInventory`) — kênh chỉ-hiển-thị thì hạn mức ít nghĩa.</summary>
    public bool HoldsInventory { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Số dòng hạn mức đã cấu hình cho kênh này (0 ⇒ đang dùng **giá trị dùng chung**).</summary>
    public int SoDongHanMuc { get; set; }
}

/// <summary>
/// 🆕 `S7` — **MỘT KHO CÓ THẬT** trong danh mục — cho ô chọn *"kho"* ở màn hình hạn mức.
/// 📌 Trước lượt này màn hình **không chọn được kho** ⇒ hạn mức theo kho chỉ đặt được bằng API/SQL, và
/// người vận hành **không thấy được** dòng nào đang áp cho kho nào (nợ đã ghi ở `INV-003` §18.7 + bàn giao §7 việc 6).
/// </summary>
public class KhoKhaDungDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Kho **ngừng dùng** — vẫn cho chọn (dòng hạn mức cũ còn đó) nhưng UI phải **nói rõ**.</summary>
    public bool IsActive { get; set; }

    /// <summary>Số dòng hạn mức đang gắn với kho này (để UI gợi ý *"kho chưa cấu hình"*).</summary>
    public int SoDongHanMuc { get; set; }
}

/// <summary>
/// 🆕 `S7` — **BỘ LỌC + PHÂN TRANG danh sách hạn mức** (`INV-003`).
///
/// 🔒 **HAI LÝ DO PHẢI CÓ (đều là nợ đã ghi, không phải "cho đẹp"):**
/// <list type="number">
///   <item><b>Lọc theo KHO</b> — bàn giao §7 việc 6: *"`/HanMucKenh` chọn kho"*. Trước lượt này màn hình
///     chỉ lọc theo **kênh** ⇒ muốn biết *"kho Hà Nội đang bị siết bao nhiêu"* phải đọc cả bảng;</item>
///   <item><b>Phân trang</b> — bảng hạn mức dài theo **số kênh × số kho**. 📌 Không phân trang thì màn hình
///     **tải hết** rồi người dùng cuộn — và đó là cách một bảng cấu hình trở nên **không ai dùng**.</item>
/// </list>
/// </summary>
public class LocHanMucKenhInput
{
    /// <summary>Mã kênh (`null` = mọi kênh) — chuẩn hoá **cùng hàm** với lúc ghi (`InvChannelLimit.ChuẩnHoáKenh`).</summary>
    public string? ChannelCode { get; set; }

    /// <summary>
    /// `null` = **mọi kho** · `Guid.Empty` = **chỉ dòng "cả kênh"** (`WarehouseId IS NULL`) ·
    /// một `Guid` cụ thể = **chỉ dòng của kho đó**.
    /// 📌 `Guid.Empty` là một lựa chọn **thật** cần diễn đạt được: *"những hạn mức không gắn kho"*.
    /// </summary>
    public Guid? WarehouseId { get; set; }

    public int SkipCount { get; set; }

    /// <summary>Số dòng một trang (mặc định 20; app service **chặn trần**).</summary>
    public int MaxResultCount { get; set; } = 20;
}

/// <summary>Đọc/ghi chính sách + hạn mức phân bổ theo kênh (`INV-003` · `REC-001`).</summary>
public interface IChinhSachHanMucAppService : IApplicationService
{
    /// <summary>Chính sách "cho phép chọn" đang áp (kèm danh sách setting bị giá trị lạ).</summary>
    Task<ChinhSachHanMucDto> GetChinhSachAsync();

    /// <summary>Đổi chính sách. 🔒 Ghi qua `ISettingManager` ⇒ **cache tự xoá** (không cần sửa DB + xoá cache tay).</summary>
    Task<ChinhSachHanMucDto> DoiChinhSachAsync(DoiChinhSachInput input);

    /// <summary>Danh sách hạn mức (lọc theo kênh nếu truyền). 📌 Giữ nguyên chữ ký cũ cho tương thích;
    /// màn hình dùng bản **có phân trang** bên dưới.</summary>
    Task<List<HanMucKenhDto>> GetDanhSachAsync(string? channelCode = null);

    /// <summary>🆕 `S7` — danh sách hạn mức **lọc theo kênh + kho** và **phân trang** (trả `TotalCount` thật).</summary>
    Task<Volo.Abp.Application.Dtos.PagedResultDto<HanMucKenhDto>> GetDanhSachPhanTrangAsync(LocHanMucKenhInput input);

    /// <summary>Danh mục kênh **có thật** + số dòng hạn mức đã cấu hình.</summary>
    Task<List<KenhKhaDungDto>> GetKenhKhaDungAsync();

    /// <summary>🆕 `S7` — danh mục **kho** có thật (cho ô chọn kho) + số dòng hạn mức mỗi kho.</summary>
    Task<List<KhoKhaDungDto>> GetKhoKhaDungAsync();

    /// <summary>
    /// Thêm **hoặc** cập nhật hạn mức cho `(kênh, kho)`.
    /// 🔒 **NÉM `eCommerce:Inventory:0046`** nếu mã kênh **không tồn tại** — nếu im lặng thì hạn mức
    /// **không bao giờ khớp** lúc giữ chỗ (đúng loại "tắt im lặng": người vận hành **tin** đã siết kênh).
    /// </summary>
    Task<HanMucKenhDto> DatHanMucAsync(DatHanMucKenhInput input);

    /// <summary>Xoá **mềm** một dòng hạn mức (giữ vết cấu hình — lịch sử chặn đơn `0042` còn đối chiếu được).</summary>
    Task XoaHanMucAsync(Guid id);
}
