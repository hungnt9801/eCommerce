using eCommerce.Address;
using Volo.Abp.Application.Services;

namespace eCommerce.Customers;

/// <summary>
/// Đầu vào **thêm một địa chỉ vào sổ** của khách (`ADDR-004` §6.1).
///
/// 🔒 **KHÔNG** có ô huyện: địa chỉ MỚI là **2 cấp** (`Luật 72/2025/QH15`). Nếu UI hiện ô huyện, người dùng
/// sẽ nhập và nhận `eCommerce:Address:0004` — đó là lỗi UX do thiết kế, không phải lỗi người dùng.
/// 📌 Địa chỉ 3 cấp **cũ** chỉ để **đọc** (hiện nhãn *"địa chỉ 3 cấp trước 01/7/2025"*).
/// </summary>
public class ThemDiaChiInput
{
    public int SchemaVersion { get; set; } = 2;

    public string TinhCode { get; set; } = default!;

    public string XaCode { get; set; } = default!;

    public string? SoNhaDuong { get; set; }

    /// <summary>Nhãn hiển thị — *"Nhà riêng"*, *"Văn phòng"* (tối đa 128 ký tự).</summary>
    public string? Label { get; set; }

    /// <summary>`Shipping` (mặc định) · `Billing`.</summary>
    public string AddressType { get; set; } = "Shipping";

    /// <summary>Đặt làm mặc định cho loại này (thay thế mặc định cũ — `ADR-06`).</summary>
    public bool LaMacDinh { get; set; }
}

/// <summary>
/// Một mục trong **sổ địa chỉ**. 🔒 `DiaChi` là **snapshot** (`DiaChiSnapshotDto`) — consumer **không** được
/// sửa địa chỉ của người khác, và `DiaChiDayDu` là chữ **đã chốt** để in chứng từ (`ADR-10`).
/// </summary>
public class DiaChiTrongSoDto
{
    public Guid LinkId { get; set; }
    public Guid AddressId { get; set; }
    public string AddressType { get; set; } = default!;
    public string? Label { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    /// <summary>🔑 `false` ⇒ địa chỉ **chưa chuẩn hoá** (dùng được để hiển thị, **không** để lập vận đơn).</summary>
    public bool CoDungDuoc { get; set; }

    public DiaChiSnapshotDto DiaChi { get; set; } = default!;
}

/// <summary>
/// **Sổ địa chỉ** của một khách.
/// ⚠️ `SoMacDinhGiaoHang`/`SoMacDinhHoaDon` là **số đếm THEO LOẠI** — 🔑 không được cộng chúng thành
/// *"tổng số địa chỉ"* (loại lỗi *"số tổng ≠ số dòng"* rất khó phát hiện).
/// </summary>
public class SoDiaChiDto
{
    public Guid CustomerId { get; set; }

    public List<DiaChiTrongSoDto> CacDiaChi { get; set; } = [];

    public int SoMacDinhGiaoHang { get; set; }

    public int SoMacDinhHoaDon { get; set; }
}

/// <summary>
/// **Sổ địa chỉ của khách** (`ADDR-004` §6.1) — phần B.
///
/// 🔐 **Phân quyền:** `[Authorize]` ở **cấp class** + permission `ManageBook` cho các method **ghi**
/// (`Them`/`DatMacDinh`/`Go`). 🔒 `ManageBook` **thấp hơn** `Manage` một bậc một cách có chủ ý: nó chỉ ảnh
/// hưởng **một** khách, còn `Manage` sửa `Address` **dùng chung** trong tenant (ảnh hưởng khách khác).
/// </summary>
public interface ICustomerAddressAppService : IApplicationService
{
    /// <summary>Đọc sổ địa chỉ. ⚠️ Tên `Get…` ⇒ ABP sinh **`GET`** (bài học `CUS-001` §16.2).</summary>
    Task<SoDiaChiDto> GetSoDiaChiAsync(Guid customerId);

    /// <summary>
    /// Thêm địa chỉ vào sổ: **tạo hoặc dùng lại** `Address` theo nội dung (`ADR-04`), rồi tạo **liên kết**.
    /// 🚨 Vượt trần (mặc định **20**, là **cấu hình**) ⇒ `eCommerce:Customers:0015`.
    /// </summary>
    Task<DiaChiTrongSoDto> ThemDiaChiAsync(Guid customerId, ThemDiaChiInput input);

    /// <summary>
    /// Đặt mặc định — 🚨 **khoá phân tán theo khách** (bao ngoài UoW) + partial unique index làm **chốt cuối**
    /// (`ADR-06`): hai luồng cùng đặt mặc định ⇒ **đúng một** thắng, luồng kia `Lock:0001` hoặc `0012`.
    /// </summary>
    Task<DiaChiTrongSoDto> DatMacDinhAsync(Guid customerId, Guid linkId);

    /// <summary>
    /// Gỡ địa chỉ = **xoá mềm** (append-only). 🔑 Nếu mục bị gỡ đang là **mặc định** ⇒ chọn mục thay thế
    /// **trong cùng** giao dịch (`BR-009` — *"gỡ mặc định ⇒ luôn có mặc định thay thế nếu còn mục khác"*).
    /// </summary>
    Task GoDiaChiAsync(Guid customerId, Guid linkId);
}
