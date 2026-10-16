using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Catalog.Dtos;

/// <summary>Hiển thị của một sản phẩm trên một kênh — **đọc**.</summary>
public class HienThiKenhDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }

    public string ChannelCode { get; set; } = default!;

    /// <summary>Tên hiển thị của kênh (tra qua `IChannelDirectory`) — 📌 để UI **không** phải gọi thêm một API.</summary>
    public string? TenKenh { get; set; }

    /// <summary>Trạng thái — giá trị là **tên enum** (`KeThua`/`HienThi`/`An`/`NgungBan`), đọc được.</summary>
    public string TrangThai { get; set; } = default!;

    /// <summary>Lý do — 🔒 **có** khi trạng thái là `An` (ẩn **bắt buộc** có lý do).</summary>
    public string? LyDo { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? LastModificationTime { get; set; }

    /// <summary>Kênh còn hoạt động không (tra qua `IChannelDirectory`).
    /// ⚠️ `false` ⇒ bản ghi này **không còn hiệu lực** ở sàn, cần người vận hành xử lý (nợ `F10`).</summary>
    public bool KenhConHoatDong { get; set; }
}

/// <summary>Lọc danh sách hiển thị theo kênh của **một** sản phẩm.</summary>
public class LocHienThiKenhInput : PagedAndSortedResultRequestDto
{
    public Guid ProductId { get; set; }
}

/// <summary>
/// 🚨 **ĐẶT TRẠNG THÁI HIỂN THỊ TRÊN MỘT KÊNH**.
///
/// 🔑 **QUY ƯỚC QUAN TRỌNG NHẤT — `TrangThai = KeThua` NGHĨA LÀ "XOÁ BẢN GHI ĐỂ QUAY VỀ KẾ THỪA":**
/// 📌 Vì sao **không** phải "đặt trạng thái thành `KeThua`": `KeThua` **chính là** *"không có bản ghi"*
/// (`ADR-08`) — hai cách diễn đạt **cùng một sự thật**. Nếu ta **lưu** một bản ghi mang giá trị `KeThua`
/// thì:
/// <list type="bullet">
///   <item>bảng có **hai** cách biểu diễn **cùng một trạng thái** ⇒ mọi truy vấn `LEFT JOIN` phải xử lý **cả hai**
///     (và chỗ nào quên thì **sai im lặng**);</item>
///   <item>`ProductChannelVisibility.ChuyenSang(KeThua, …)` **NÉM** (`0205`) — mà quy ước này **không** đi qua
///     `ChuyenSang` ⇒ không mâu thuẫn, nhưng **phải** nói rõ ở đây để người đọc không tưởng là thiếu guard.</item>
/// </list>
/// 🔒 Vậy: `KeThua` ⇒ **XOÁ** bản ghi (nếu có); mọi giá trị khác ⇒ **tạo hoặc cập nhật** rồi đi qua **bảng chuyển**
/// ở entity (`0205` nếu cặp chuyển không hợp lệ).
/// </summary>
public class DatHienThiKenhInput
{
    [Required]
    [StringLength(eCommerceConsts.CatalogLimits.MaKenhHienThiToiDa)]
    public string ChannelCode { get; set; } = default!;

    /// <summary>Trạng thái đích — xem **quy ước** ở chú thích lớp.
    /// 🔒 Nhận **tên enum** (chuỗi) để API **tự mô tả** và client không phải biết số; sai tên ⇒ `0205`.</summary>
    [Required]
    public string TrangThai { get; set; } = default!;

    /// <summary>Lý do — 🔒 **BẮT BUỘC** khi `TrangThai = An` (entity **NÉM** nếu thiếu).</summary>
    [StringLength(eCommerceConsts.CatalogLimits.LyDoAnToiDa)]
    public string? LyDo { get; set; }
}

/// <summary>
/// 🚨 **HỢP ĐỒNG TRA KÊNH** — để `Catalog` kiểm *"kênh có tồn tại **và** đang hoạt động không"* (`CAT-003` `0207`)
/// mà **không** phải tham chiếu entity `Channel` của `ChannelHub`.
/// 📌 Khuôn **giống hệt** `ICustomerDirectory` (module `Customers`): hợp đồng **chỉ đọc** ở `Application.Contracts`,
/// hiện thực ở `Application`. 🔒 **Chỉ đọc** — module khác **không** được đổi trạng thái kênh; đó là việc của
/// người vận hành qua app service riêng (có quyền + có audit).
/// </summary>
public interface IChannelDirectory
{
    /// <summary>
    /// Kênh có **tồn tại** và **đang hoạt động** không (`IsActive = true`).
    /// 📌 Trả `bool` (không ném) để chỗ gọi dùng được ở **điều kiện** thay vì phải bọc `try/catch` rồi đoán
    /// mã lỗi nào là *"kênh sai"*.
    /// </summary>
    Task<bool> KenhHoatDongAsync(string channelCode);

    /// <summary>Tên hiển thị của kênh; `null` nếu không có (hoặc đã xoá mềm).</summary>
    Task<string?> TenKenhAsync(string channelCode);

    /// <summary>Mã các kênh **đang hoạt động** — dùng cho UI chọn kênh và cho job đối chiếu (`F10`).</summary>
    Task<IReadOnlyList<string>> DanhSachKenhHoatDongAsync();
}

/// <summary>
/// 🚨 **QUẢN LÝ HIỂN THỊ SẢN PHẨM THEO KÊNH** (`CAT-003` `ADR-08`) — app service.
/// 🔐 Quyền: `Products.Edit` — 📌 **VÌ SAO KHÔNG phải quyền riêng:** đặt hiển thị theo kênh là **một phần của
/// việc sửa sản phẩm** (khác hẳn **tải tệp lên** — đó là **đường vào từ Internet** nên cần `Products.ManageMedia`).
/// ⚠️ Chưa có quyền riêng ⇒ nếu sau này cần tách (ví dụ *"chỉ marketing được ẩn/hiện trên kênh"*) thì phải
/// thêm **cả** hằng số **và** provider (thiếu một nơi ⇒ `403` cho **mọi** người kể cả admin — `XC-004` D2/E3).
/// </summary>
public interface IHienThiKenhAppService
{
    /// <summary>Danh sách cấu hình hiển thị của một sản phẩm (**chỉ** các kênh **có** bản ghi tường minh).</summary>
    Task<PagedResultDto<HienThiKenhDto>> GetDanhSachAsync(LocHienThiKenhInput input);

    /// <summary>Đặt trạng thái hiển thị trên một kênh — xem **quy ước** ở `DatHienThiKenhInput`.</summary>
    Task<HienThiKenhDto?> PostDatAsync(Guid productId, DatHienThiKenhInput input);
}
