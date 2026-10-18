using Volo.Abp.Application.Services;

namespace eCommerce.Address;

/// <summary>
/// Hằng số của **sổ địa chỉ** (`ADDR-004`) — dùng chung cho app service, tài liệu và test.
/// </summary>
public static class AddressConsts
{
    /// <summary>
    /// Khoá **CẤU HÌNH** cho trần số địa chỉ của MỘT khách (`ADDR-004` `Q1`).
    /// 🔒 Là **cấu hình** (không phải setting trong DB): đổi trần là đổi cấu hình, **không** cần migration.
    /// </summary>
    public const string TenCauHinhTranDiaChiMotKhach = "Addresses:MaxPerCustomer";

    /// <summary>Giá trị mặc định của trần — **20** (`Q1`). Dùng khi cấu hình không có.</summary>
    public const int TranDiaChiMotKhachMacDinh = 20;

    /// <summary>Độ dài tối đa của `Label` (nhãn) — khớp `CHECK char_length("Label") &lt;= 128`.</summary>
    public const int DoDaiNhanToiDa = 128;
}

/// <summary>
/// 🚨 **SNAPSHOT ĐỊA CHỈ** — hợp đồng đọc cho **module KHÁC** (`ADDR-004` §6.1).
///
/// 🔒 **VÌ SAO KHÔNG TRẢ ENTITY:** entity có setter; consumer (Sales/Shipping) sẽ *"vô tình"* sửa
/// `SoNhaDuong` của một địa chỉ mà **khách khác** cũng đang dùng (địa chỉ dùng chung trong tenant —
/// `ADR-03`). 📌 Đó là lỗi **không thể phát hiện qua unit test**.
///
/// 🔑 Consumer cần **bản sao bất biến** cho chứng từ (`ADR-05`/`ADR-10`): `DiaChiDayDu` là **text tại
/// thời điểm tạo** — **CẤM** join `AddrAdministrativeUnits` để in, vì `DoiTen` sửa **tại chỗ**.
/// </summary>
/// <param name="AddressId">Khoá của `Address` (dùng để trỏ tới, không nhúng nội dung vào chứng từ khác).</param>
/// <param name="TrangThai">`ChuaChuanHoa` / `DaChuanHoa` / `DaXacMinh` / `NghiNgo`.</param>
/// <param name="SchemaVersion">1 = 3 cấp (lịch sử) · 2 = 2 cấp (hiện hành).</param>
/// <param name="TenTinh">Tên tỉnh **HIỆN TẠI** của danh mục — ⚠️ có thể khác tên trong `DiaChiDayDu` (§5.3).</param>
/// <param name="TenHuyen">Tên huyện hiện tại — chỉ có ở `SchemaVersion = 1`.</param>
/// <param name="TenXa">Tên xã hiện tại của danh mục.</param>
/// <param name="DiaChiDayDu">🔒 **SNAPSHOT CHỮ** để in chứng từ — bất biến sau khi chuẩn hoá.</param>
/// <param name="CoDungDuoc">`false` ⇔ địa chỉ **chưa chuẩn hoá** ⇒ không dùng được cho vận đơn/hoá đơn.</param>
public sealed record DiaChiSnapshotDto(
    Guid AddressId,
    string TrangThai,
    int SchemaVersion,
    string? TinhCode,
    string? TenTinh,
    string? HuyenCode,
    string? TenHuyen,
    string? XaCode,
    string? TenXa,
    string? SoNhaDuong,
    string DiaChiDayDu,
    DateTime? NormalizedAtUtc,
    bool CoDungDuoc);

/// <summary>
/// Địa chỉ đầy đủ cho mặt API quản trị: snapshot + các trường **kỹ thuật** (`ContentHash`, mốc xác minh,
/// nguồn tạo). 📌 Chỉ API nội bộ thấy — **không** đưa `ContentHash` ra ngoài luồng chứng từ.
/// </summary>
public class DiaChiDto
{
    public Guid AddressId { get; set; }
    public string TrangThai { get; set; } = default!;
    public int SchemaVersion { get; set; }
    public string? TinhCode { get; set; }
    public string? TenTinh { get; set; }
    public string? HuyenCode { get; set; }
    public string? TenHuyen { get; set; }
    public string? XaCode { get; set; }
    public string? TenXa { get; set; }
    public string? SoNhaDuong { get; set; }
    public string DiaChiDayDu { get; set; } = default!;

    /// <summary>🔒 SHA-256 hex của chuỗi canonical — **khoá tự nhiên** (`ADR-04`). Chỉ để tra cứu/đối soát.</summary>
    public string ContentHash { get; set; } = default!;

    public string NguonTao { get; set; } = default!;
    public DateTime? NormalizedAtUtc { get; set; }
    public DateTime? XacMinhLucUtc { get; set; }
    public DateTime CreationTime { get; set; }
    public bool CoDungDuoc { get; set; }
}

/// <summary>
/// Đầu vào **tạo hoặc lấy** một `Address` — **idempotent theo NỘI DUNG** (`ADR-04`).
///
/// 🔒 **KHÔNG** có header `Idempotency-Key`: khoá chống trùng là `ContentHash` của canonical.
/// 📌 Có mã tỉnh+xã ⇒ tạo địa chỉ **đã chuẩn hoá**; chỉ có chữ ⇒ tạo địa chỉ **thô** (`ChuaChuanHoa`).
/// </summary>
public class TaoDiaChiInput
{
    /// <summary>1 = 3 cấp (chỉ đọc lịch sử) · 2 = 2 cấp (mặc định, `Luật 72/2025/QH15`).</summary>
    public int SchemaVersion { get; set; } = 2;

    public string? TinhCode { get; set; }
    public string? XaCode { get; set; }

    /// <summary>Chỉ dùng cho schema 3 cấp (địa chỉ lịch sử).</summary>
    public string? HuyenCode { get; set; }

    /// <summary>Số nhà/đường — chữ tự do, **không** chuẩn hoá được.</summary>
    public string? SoNhaDuong { get; set; }

    /// <summary>Địa chỉ **thô** dạng chữ (dùng khi chưa xác định được ĐVHC — webhook sàn, chữ tự do).</summary>
    public string? DiaChiDayDuTho { get; set; }

    /// <summary>`Web` · `San` · `NhanVien` · `Import` (mặc định `Web`). Chỉ để **truy vết**.</summary>
    public string? NguonTao { get; set; }
}

/// <summary>Đầu vào **chuẩn hoá lại** một địa chỉ `ChuaChuanHoa` — chỉ chạy được **MỘT lần** (`ADR-05`).</summary>
public class ChuanHoaInput
{
    public int SchemaVersion { get; set; } = 2;
    public string TinhCode { get; set; } = default!;
    public string XaCode { get; set; } = default!;
    public string? HuyenCode { get; set; }
    public string? SoNhaDuong { get; set; }
}

/// <summary>Một dòng trong **hàng đợi chuẩn hoá** (vận hành — `ADDR-004` §3.1 #5).</summary>
public sealed record DiaChiChoDuyetDto(
    Guid AddressId,
    string TrangThai,
    int SchemaVersion,
    string DiaChiDayDu,
    DateTime CreationTime);

/// <summary>
/// Mặt API của **miền địa chỉ** (`ADDR-004` §6.1).
/// 🔒 `[Authorize]` ở **CẤP CLASS** + permission cụ thể ở từng method (quy ước #6 — thiếu ⇒ 403 cho mọi người).
/// </summary>
public interface IAddressAppService : IApplicationService
{
    /// <summary>Tạo **hoặc** lấy `Address` theo nội dung (`ADR-04`) — gửi lại **không** tạo bản ghi thứ hai.</summary>
    Task<DiaChiDto> TaoHoacLayDiaChiAsync(TaoDiaChiInput input);

    /// <summary>Đọc một địa chỉ. ⚠️ Route phải là **`GET`** — khai `[HttpGet]` tường minh (`CUS-001` §16.2).</summary>
    Task<DiaChiDto> GetAsync(Guid addressId);

    /// <summary>Chuẩn hoá địa chỉ thô — chỉ từ `ChuaChuanHoa`, **một lần** (`ADR-05`).</summary>
    Task<DiaChiDto> ChuanHoaLaiAsync(Guid addressId, ChuanHoaInput input);

    /// <summary>Xác minh — **bắt buộc** có căn cứ (`0106`).</summary>
    Task<DiaChiDto> DanhDauDaXacMinhAsync(Guid addressId, string canCu);

    /// <summary>Hàng đợi vận hành: `ChuaChuanHoa` + `NghiNgo`, cũ nhất trước.</summary>
    Task<List<DiaChiChoDuyetDto>> GetHangDoiChuanHoaAsync(int toiDa = 50);
}

/// <summary>
/// **Cổng nội bộ** để module khác tạo/lấy/đọc **snapshot** địa chỉ (`ADDR-004` §6.1).
/// 🔒 **CẤM** module khác đọc bảng `Addr*` trực tiếp: nội dung địa lý có luật bất biến (`ADR-05`) và
/// ảnh hưởng **khách khác** trong tenant — chỉ miền `Address` được ghi.
/// 📌 Giống `ICustomerDirectory`: **interface nội bộ**, không phải mặt HTTP (không sinh endpoint).
/// </summary>
public interface IAddressDirectory
{
    /// <summary>Tạo **hoặc** lấy theo nội dung, rồi trả snapshot. Gửi lại ⇒ **cùng** `AddressId`.</summary>
    Task<DiaChiSnapshotDto> TaoHoacLayAsync(TaoDiaChiInput input, CancellationToken ct = default);

    /// <summary>Đọc snapshot theo id — `null` = không tồn tại / khác tenant / đã ẩn.</summary>
    Task<DiaChiSnapshotDto?> TimTheoIdAsync(Guid addressId, CancellationToken ct = default);

    /// <summary>Đọc nhiều snapshot (một truy vấn) — dùng khi dựng sổ địa chỉ.</summary>
    Task<IReadOnlyList<DiaChiSnapshotDto>> TimNhieuTheoIdAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
