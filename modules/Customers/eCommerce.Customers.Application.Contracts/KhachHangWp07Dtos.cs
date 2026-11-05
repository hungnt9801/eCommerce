using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Customers;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// `WP-07` — DTO của màn KHÁCH HÀNG (DS 5 tab phân khúc + chi tiết 5 tab).
//
// 🔑 **VÌ SAO DTO TÁCH KHỎI ENTITY:** `CustomerDto` đang được `ICustomerDirectory` (hợp đồng đọc của module
// khác) trả ra. Thêm **số dư điểm/nợ** vào đó là **mở rộng hợp đồng** — module khác sẽ thấy & phụ thuộc
// vào nó. 📌 Nên: `CustomerDto` chỉ **thêm trường đọc** (không phá vỡ ai), còn màn chi tiết dùng
// `CustomerDetailDto` **riêng** (có thống kê + nợ chi tiết).
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Tham số lọc **DS khách 5 tab phân khúc** (`07 §WP-07`).
/// 📌 Tab là **DỮ LIỆU** (`CustomerTabs`) — cùng nguyên tắc `D-41` của đơn hàng: đếm và lọc dùng **một**
/// định nghĩa, nếu không thì *"tab đếm 7, bấm vào ra 5"* và người dùng **mất tin** vào badge.</summary>
public class CustomerListInput : PagedResultRequestDto
{
    /// <summary>Tìm theo tên/email/SĐT/mã số thuế — ⚠️ **KHÔNG** log từ khoá (có thể là PII).</summary>
    public string? Q { get; set; }

    /// <summary>Tab phân khúc: `all` · `retail` · `wholesale` · `agency` · `debt`.</summary>
    public string? Tab { get; set; }

    public CustomerType? Type { get; set; }

    public Guid? LevelId { get; set; }

    /// <summary>`true` = **chỉ khách còn nợ** (lọc theo `DebtBalance > 0`).</summary>
    public bool? ConNo { get; set; }
}

public class CustomerTabCountsDto
{
    public int Total { get; set; }

    public List<CustomerTabDto> Tabs { get; set; } = new();
}

public class CustomerTabDto
{
    /// <summary>Khoá URL của tab (`all`, `retail`, …).</summary>
    public string Key { get; set; } = default!;

    public string Label { get; set; } = default!;

    public int Count { get; set; }
}

/// <summary>
/// **CHI TIẾT KHÁCH** — dùng cho màn `/{business}/customer/[id]` (5 tab).
/// 📌 Kèm **thống kê suy ra** (số đơn, tổng đã mua) để màn chi tiết **không** phải gọi 3 API rồi tự cộng
/// (tự cộng ở FE là **định nghĩa thứ hai** của "đã mua bao nhiêu" — và nó sẽ lệch với server).
/// </summary>
public class CustomerDetailDto : CustomerDto
{
    /// <summary>% chiết khấu của cấp — 📌 chỉ **thông tin** ở `WP-07` (chưa nối bảng giá).
    /// 📌 `LevelName` **không** khai lại ở đây: nó đã ở `CustomerDto` (khai lại ⇒ warning `CS0108` che
    /// thành viên — và hai trường cùng tên là mầm của *"hai giá trị khác nhau cho một cột"*).</summary>
    public decimal? LevelDiscountPercent { get; set; }

    /// <summary>Số đơn online của khách (`OrdOrders`).</summary>
    public int SoDonHang { get; set; }

    /// <summary>Số hoá đơn bán lẻ (`PosRetailBills`, **không** tính hoá đơn đã huỷ).</summary>
    public int SoHoaDonBanLe { get; set; }

    /// <summary>Σ giá trị hoá đơn bán lẻ **không huỷ** — 🔑 đối chiếu với `AccumulatedAmount`.</summary>
    public decimal TongBanLe { get; set; }

    /// <summary>Σ hàng khách đã trả (`PosRetailReturns.GoodsAmount`).</summary>
    public decimal TongDaTraHang { get; set; }

    /// <summary>Nợ quá hạn (có hoá đơn `DueDateUtc &lt; now` và `DebtAmount &gt; 0`).</summary>
    public bool CoNoQuaHan { get; set; }

    /// <summary>Các hoá đơn **còn nợ** (tab "Công nợ") — tối đa 50, mới nhất trước.</summary>
    public List<CongNoHoaDonDto> CongNo { get; set; } = new();
}

public class CongNoHoaDonDto
{
    public Guid BillId { get; set; }
    public string Number { get; set; } = default!;
    public decimal TotalPayment { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public DateTime? DueDateUtc { get; set; }

    /// <summary>`true` = quá hạn (tính ở **server** theo `Clock.Now` — không tin đồng hồ máy khách).</summary>
    public bool QuaHan { get; set; }

    public DateTime CreationTime { get; set; }
}

/// <summary>Một lần **chăm sóc khách** (tab "Chăm sóc" của màn chi tiết).</summary>
public class CustomerCareDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>`Call` / `Message` / `Email` / `Meeting` / `Other`.</summary>
    public string Type { get; set; } = default!;

    public string Content { get; set; } = default!;
    public Guid? StaffId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }
    public DateTime CreationTime { get; set; }
}

public class TaoChamSocInput
{
    [Required]
    public CustomerCareType Type { get; set; }

    [Required]
    [StringLength(CustomerCare.DoDaiNoiDungToiDa)]
    public string Content { get; set; } = default!;

    /// <summary>Lúc xảy ra (UTC) — `null` = **bây giờ**. 📌 Cho phép nhập lại cuộc gọi hôm qua.</summary>
    public DateTime? OccurredAtUtc { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime? NextFollowUpAtUtc { get; set; }
}

/// <summary>**CẬP NHẬT PHÂN KHÁCH** — loại/cấp/hạn mức nợ/nhân viên/người giới thiệu/MST/ngày sinh.</summary>
public class CapNhatPhanKhachInput
{
    public CustomerType Type { get; set; } = CustomerType.Retail;

    public Guid? LevelId { get; set; }

    /// <summary>`0` = **KHÔNG đặt hạn mức** (không chặn nợ) — xem `NghiepVuKhach.NoSauGiaoDich`.</summary>
    public decimal DebtLimit { get; set; }

    public Guid? SaleStaffId { get; set; }

    public Guid? ReferrerId { get; set; }

    [StringLength(Customer.DoDaiMaSoThueToiDa)]
    public string? TaxCode { get; set; }

    public DateTime? Birthday { get; set; }
}

/// <summary>**CẤP KHÁCH** — dữ liệu cấu hình (ngưỡng tiền tích luỹ).</summary>
public class CustomerLevelDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal MinAccumulatedAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Note { get; set; }
}

public class TaoCapKhachInput
{
    [Required]
    [StringLength(CustomerLevel.DoDaiMaToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(CustomerLevel.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    public decimal MinAccumulatedAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public int SortOrder { get; set; }

    [StringLength(CustomerLevel.DoDaiGhiChuToiDa)]
    public string? Note { get; set; }
}

/// <summary>
/// 🧮 **ĐỐI CHIẾU SỔ ĐIỂM/CÔNG NỢ CỦA MỘT KHÁCH** — 🔑 trả lời câu hỏi *"vì sao số dư này lại như vậy"*.
///
/// 📌 **VÌ SAO CẦN (không phải màn hình cho đẹp):** `Points`/`DebtBalance` là **số dư cộng dồn**; khi khách
/// khiếu nại *"tôi còn 120 điểm cơ mà"* thì nhân viên **phải** chỉ ra được từng chứng từ đã cộng/trừ.
/// 🔒 Không có đường đối chiếu nghĩa là **không trả lời được** — và khi đó người ta sẽ **sửa tay** số dư,
/// làm mất luôn khả năng đối chiếu về sau.
/// </summary>
public class DoiChieuSoDuDto
{
    public Guid CustomerId { get; set; }

    public decimal Points { get; set; }

    public decimal AccumulatedAmount { get; set; }

    public decimal DebtLimit { get; set; }

    public decimal DebtBalance { get; set; }

    /// <summary>Σ **giá trị hàng** của hoá đơn **không huỷ** (`Subtotal − DiscountTotal`) — cùng công thức
    /// với sổ khách (`D-45`). 📌 KHÔNG dùng `TotalPayment` (có VAT/điểm) — dùng sai thì cờ `TienTichLuyKhop`
    /// **luôn** sai và người vận hành sẽ đi tìm một lỗi không tồn tại.</summary>
    public decimal TongHoaDon { get; set; }

    /// <summary>Σ hàng đã trả (`PosRetailReturns.GoodsAmount`).</summary>
    public decimal TongTraHang { get; set; }

    /// <summary>Σ nợ còn lại trên các hoá đơn chưa huỷ.</summary>
    public decimal TongNoHoaDon { get; set; }

    /// <summary>`AccumulatedAmount` **phải** bằng `TongHoaDon − TongTraHang` (theo công thức `D-45`).</summary>
    public bool TienTichLuyKhop { get; set; }

    /// <summary>`DebtBalance` **phải** bằng `TongNoHoaDon`.</summary>
    public bool CongNoKhop { get; set; }
}
