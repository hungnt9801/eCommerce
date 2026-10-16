using Volo.Abp.Application.Dtos;

namespace eCommerce.Inventory;

/// <summary>
/// **MỘT LƯỢT ĐỐI SOÁT TỒN KHO** trả ra cho người vận hành (`REC-001`).
/// 📌 `NguongHopLe` và **ảnh chụp chính sách** (`DonViNguong`/`GiaTriNguong`/`HanhDong`) được trả **cùng**
/// lượt: đổi chính sách về sau **không** viết lại lịch sử, nên người đọc cần biết **lượt đó đã chấm theo
/// ngưỡng nào** — nếu không, một lượt cũ *"0 lệch vượt ngưỡng"* trông như *"kho sạch"* trong khi thật ra
/// **ngưỡng đặt quá rộng**.
/// </summary>
public class LuotDoiSoatDto
{
    public Guid Id { get; set; }
    public DateTime BatDauUtc { get; set; }
    public DateTime KetThucUtc { get; set; }

    /// <summary>Thời gian quét (giây) — suy ra, để người đọc biết lượt có **chạy bất thường lâu** không.</summary>
    public double SoGiayQuet { get; set; }

    public int SoCapDaDo { get; set; }
    public int SoLech { get; set; }
    public int SoLechVuotNguong { get; set; }
    public bool CoVuotNguong { get; set; }

    /// <summary>Ảnh chụp chính sách tại thời điểm quét — xem chú thích ở lớp.</summary>
    public string DonViNguong { get; set; } = default!;
    public decimal GiaTriNguong { get; set; }
    public string HanhDong { get; set; } = default!;

    /// <summary>⚠️ `false` = **ngưỡng KHÔNG hợp lệ** tại thời điểm quét (ví dụ giá trị âm) ⇒
    /// **mọi** dòng lệch bị coi là vượt ngưỡng. 📌 Đây là ca **cấu hình sai**, không phải *"kho lệch nhiều"* —
    /// trả cờ ra để người đọc **không** đi tìm nguyên nhân ở kho.
    /// </summary>
    public bool NguongHopLe { get; set; }

    /// <summary>`Clean` · `DiscrepancyWithinThreshold` · `DiscrepancyExceeded` (xem `KetLuanDoiSoat`).</summary>
    public string KetLuan { get; set; } = default!;

    /// <summary>Số dòng lệch **CHƯA xử lý** của lượt — 🔒 con số **hành động được**: nó giảm khi người
    /// vận hành xử lý. 📌 Nếu chỉ trả `SoLech` (tổng), danh sách **mãi** hiện như cũ và **không ai đọc nữa**.
    /// </summary>
    public int SoLechChuaXuLy { get; set; }
}

/// <summary>**MỘT DÒNG LỆCH** `(kho, SKU)` kèm trạng thái xử lý (`REC-001`).</summary>
public class DongLechDto
{
    public Guid Id { get; set; }
    public Guid ReconRunId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Mã kho (người đọc hiểu) — tra thêm để UI **không** phải hiện GUID.</summary>
    public string? WarehouseCode { get; set; }

    public string Sku { get; set; } = default!;

    /// <summary>Ảnh chụp `OnHand` lúc quét.</summary>
    public int OnHand { get; set; }

    /// <summary>Tổng **có dấu** của sổ cái (nguồn sự thật) lúc quét.</summary>
    public int TongSoCai { get; set; }

    /// <summary>`OnHand − TongSoCai` — dấu giữ nguyên để biết **lệch về phía nào**
    /// (dương = hệ thống ghi **nhiều hơn** thực tế · âm = **thiếu** so với sổ).</summary>
    public int ChenhLech { get; set; }

    public bool VuotNguong { get; set; }

    /// <summary>🔒 `false` = **chưa ai xử lý** ⇒ UI lọc theo cờ này để chỉ hiện việc **cần làm**.</summary>
    public bool DaXuLy { get; set; }

    public DateTime? XuLyLucUtc { get; set; }
    public string? NguoiXuLy { get; set; }
    public string? LyDoXuLy { get; set; }
}

/// <summary>
/// **BỘ LỌC danh sách lượt đối soát** (`REC-001`).
/// ⚠️ `DenUtc` **bao gồm** cả ngày đó (so `< DenUtc.AddDays(1)`): 📌 nếu dùng `<=` trần thì lượt quét lúc
/// `23:59:59.xxx` của ngày cuối **bị bỏ sót** — và người dùng thấy *"thiếu lượt"* mà không hiểu vì sao.
/// </summary>
public class LocLuotDoiSoatInput
{
    public DateTime? TuUtc { get; set; }
    public DateTime? DenUtc { get; set; }

    /// <summary>`true` = chỉ lượt **có** vượt ngưỡng (việc cần xem). `null` = tất cả.</summary>
    public bool? ChiVuotNguong { get; set; }

    /// <summary>`true` = chỉ lượt **còn** dòng lệch chưa xử lý. `null` = tất cả.</summary>
    public bool? ChiConViecChuaXuLy { get; set; }

    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 20;
}

/// <summary>**BỘ LỌC danh sách dòng lệch** của một lượt.</summary>
public class LocDongLechInput
{
    public Guid ReconRunId { get; set; }

    /// <summary>`true` = chỉ dòng **vượt ngưỡng**. `null` = tất cả.</summary>
    public bool? ChiVuotNguong { get; set; }

    /// <summary>`true` = chỉ dòng **chưa xử lý** (mặc định của UI vận hành). `null` = tất cả.</summary>
    public bool? ChiChuaXuLy { get; set; }

    /// <summary>Lọc theo kho (`null` = mọi kho).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Tìm theo **một phần** SKU (`null` = mọi SKU).</summary>
    public string? Sku { get; set; }

    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 50;
}

/// <summary>
/// **ĐÁNH DẤU ĐÃ XỬ LÝ một dòng lệch** (`REC-001`).
/// 🔒 **Lý do và người xử lý BẮT BUỘC** — xem `ReconRunMismatch.DanhDauDaXuLy` (chênh lệch **tài sản**, cần audit).
/// </summary>
public class XuLyLechInput
{
    public string NguoiXuLy { get; set; } = default!;
    public string LyDo { get; set; } = default!;
}

/// <summary>
/// 🚨 **ĐƯỜNG ĐỌC + XỬ LÝ LỆCH CỦA `REC-001`** — app service cho **người vận hành**.
///
/// **VÌ SAO PHẢI CÓ (nợ đã ghi ở `rec-001` §16.8):** entity `ReconRun`/`ReconRunMismatch` + service đối soát
/// + worker **đã có và đã chạy**, nhưng **không có đường đọc nào** ⇒ dữ liệu lệch nằm trong DB mà
/// **không ai xem được** ⇒ nó là **"ghi cho có"**. 📌 Cùng họ với `SAL-006` (hoàn tiền *đã cài* nhưng
/// **không có endpoint nào gọi tới**).
/// </summary>
public interface IDoiSoatAppService
{
    /// <summary>Lịch sử các lượt quét (mới nhất trước) — có lọc + phân trang.</summary>
    Task<PagedResultDto<LuotDoiSoatDto>> GetDanhSachLuotAsync(LocLuotDoiSoatInput input);

    /// <summary>Chi tiết một lượt (kèm ảnh chụp chính sách + số dòng lệch **chưa xử lý**).</summary>
    Task<LuotDoiSoatDto> GetLuotAsync(Guid reconRunId);

    /// <summary>Các dòng lệch của một lượt — có lọc + phân trang.</summary>
    Task<PagedResultDto<DongLechDto>> GetDanhSachDongLechAsync(LocDongLechInput input);

    /// <summary>
    /// 🚨 **Đánh dấu một dòng lệch ĐÃ XỬ LÝ** (kèm lý do + người xử lý).
    /// 📌 Đây là thao tác **duy nhất** được ghi lên bảng lệch (bảng vốn append-only) — và nó **chỉ** ghi
    /// trạng thái xử lý, **không** sửa số liệu đã đo (`OnHand`/`TongSoCai`/`ChenhLech`): sửa số liệu đo được
    /// là **phá bằng chứng**, biến lịch sử đối soát thành vô nghĩa.
    /// </summary>
    Task<DongLechDto> PostXuLyLechAsync(Guid dongLechId, XuLyLechInput input);
}
