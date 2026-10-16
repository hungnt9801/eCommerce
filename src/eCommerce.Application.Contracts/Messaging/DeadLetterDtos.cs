using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Messaging;

/// <summary>
/// 🚨 **ĐỌC + XỬ LÝ HÀNG ĐỢI THƯ CHẾT** (`XC-002`) — hợp đồng cho người vận hành.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO LỚP NÀY TỒN TẠI — và vì sao "đã có DLQ" là CHƯA ĐỦ:**
///
/// Lượt trước đã làm **DLQ ghi được** (bảng + entity + writer). 🚨 **Nhưng một hàng đợi KHÔNG BAO GIỜ ĐƯỢC ĐỌC
/// thì TỆ NHƯ KHÔNG CÓ** — nó chỉ tốn chỗ và tạo **cảm giác** rằng sự kiện đã được giữ.
/// 📌 Cụ thể: `XungDot`/`DenMuon` **đẩy DLQ** và **dừng** — nếu không ai đọc thì kết quả **giống hệt** việc
/// nuốt im lặng (*"mất dữ liệu nhưng có vẻ như đã xử lý"*), chỉ khác là **tốn thêm đĩa**.
///
/// ⇒ Lớp này là **đường gọi** biến DLQ từ *"có ghi"* thành *"có đọc"*. 🔑 Cùng bài học `CAT-003` §26.6:
/// **khi một ca không đo được vì THIẾU ĐƯỜNG GỌI, việc đúng là LÀM ĐƯỜNG GỌI** — và đường gọi đó
/// **thường có ích thật** (người trực **cần** nó), **không** phải mã chỉ để test.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public interface IDeadLetterAppService
{
    /// <summary>Danh sách DLQ — 🔒 **mặc định chỉ trả dòng CHƯA xử lý** (đó là *"còn việc gì"*).</summary>
    Task<PagedResultDto<DlqDto>> GetDanhSachAsync(LocDlqInput input);

    /// <summary>Một dòng DLQ theo `Id` — kèm **payload gốc** (để điều tra / phát lại thủ công).</summary>
    Task<ChiTietDlqDto> GetChiTietAsync(Guid id);

    /// <summary>
    /// 🚨 **ĐÁNH DẤU ĐÃ XỬ LÝ** — 🔒 **BẮT BUỘC có lý do** (audit: sự kiện này **từng hỏng**).
    /// 📌 Cố ý **KHÔNG** có "phát lại tự động": phát lại một sự kiện đã hỏng **có thể gây hỏng lần nữa**,
    /// và nó **phải** là thao tác **có người** quyết định (kèm tên người + lý do).
    /// </summary>
    Task<DlqDto> PostDanhDauDaXuLyAsync(Guid id, DanhDauXuLyDlqInput input);

    /// <summary>Đếm nhanh theo lý do — 🔒 dùng cho **bảng điều khiển**: *"còn tồn bao nhiêu, vì sao"*.</summary>
    Task<ThongKeDlqDto> GetThongKeAsync();
}

/// <summary>Bộ lọc danh sách DLQ.</summary>
public class LocDlqInput : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// 🔑 `null` (mặc định) ⇒ **CHỈ dòng CHƯA xử lý**. `true` ⇒ **CHỈ dòng ĐÃ xử lý**. `false` ⇒ **TẤT CẢ**.
    /// 📌 **VÌ SAO MẶC ĐỊNH CHỈ CHƯA XỬ LÝ:** câu hỏi vận hành thật là *"còn việc gì"*. ⚠️ Nếu mặc định trả
    /// **tất cả**, thì sau vài tuần phần lớn dòng **đã xử lý** ⇒ danh sách **đầy dòng cũ** ⇒ người trực
    /// **phải lọc tay mỗi lần**, và rồi **thôi không xem nữa**. 🔑 Đây là cùng lý do đã dùng cho
    /// `IX_MsgDeadLetters_TenantId_ChuaXuLy` (**index có bộ lọc**) ở `sal-005` §18.
    /// </summary>
    public bool? DaXuLy { get; set; }

    /// <summary>
    /// 🚨 **TÊN lý do** (ví dụ `XungDotPayload` · `HaWatermark` · `LoiXuLy`), **KHÔNG** phải kiểu enum.
    ///
    /// 📌 **VÌ SAO LÀ CHUỖI (không phải enum) — 🚨 LỖI TÔI ĐÃ VẤP Ở CHÍNH LƯỢT NÀY:**
    /// `LyDoVaoDlq` nằm ở assembly **`eCommerce.Messaging.Domain`**, mà `eCommerce.Application.Contracts`
    /// **KHÔNG** tham chiếu module nào ⇒ dùng enum ở đây là **`CS0234`**.
    /// 🔑 Đây là **lần THỨ BA** tôi vấp cùng một luật layering trong dự án này
    /// (`ProductMediaDtos` §20.3, `HienThiKenhDtos` §24.4 — và nay là đây).
    /// ⚠️ **Quy tắc đã thành khuôn:** DTO ở `Application.Contracts` **CHỈ** được dùng kiểu từ
    /// `Domain.Shared` / `Kernel` / `Application.Contracts`. 🔒 Muốn dùng enum của module ⇒ **hoặc** khai lại
    /// ở `Domain.Shared` (**hai** nguồn sự thật — **sai**), **hoặc** để **chuỗi** ở DTO rồi **parse + kiểm ở
    /// app service** (chọn cách này: **một** nguồn sự thật, và chuỗi sai trả **mã lỗi rõ ràng**).
    /// </summary>
    [StringLength(64)]
    public string? LyDo { get; set; }

    /// <summary>Tra theo **khoá nghiệp vụ** (khớp một phần) — câu hỏi *"đơn này có vấn đề gì?"*.</summary>
    [StringLength(256)]
    public string? KhoaNghiepVu { get; set; }

    /// <summary>Tra theo **tên loại sự kiện** (khớp một phần).</summary>
    [StringLength(512)]
    public string? TenSuKien { get; set; }
}

/// <summary>Một dòng DLQ (rút gọn — **không** kèm payload).</summary>
public class DlqDto : EntityDto<Guid>
{
    public string TenSuKien { get; set; } = default!;

    public string KhoaNghiepVu { get; set; } = default!;

    /// <summary>Lý do vào DLQ — 🔒 **phân biệt được NGUYÊN NHÂN**, vì ba nguyên nhân cần **ba cách xử lý khác nhau**
    /// (`sal-005` §18.3).</summary>
    public string LyDo { get; set; } = default!;

    public string MoTaLyDo { get; set; } = default!;

    public string? Loi { get; set; }

    public DateTime VaoDlqLucUtc { get; set; }

    public string? CorrelationId { get; set; }

    public bool DaXuLy { get; set; }

    public DateTime? XuLyLucUtc { get; set; }

    public string? NguoiXuLy { get; set; }

    public string? LyDoXuLy { get; set; }

    /// <summary>Băm payload — 📌 để **so nhanh** với bản đã nhận mà **không** phải kéo payload nặng về.</summary>
    public string PayloadHash { get; set; } = default!;
}

/// <summary>Chi tiết một dòng DLQ — **kèm payload gốc** để điều tra / phát lại thủ công.</summary>
public class ChiTietDlqDto : DlqDto
{
    /// <summary>
    /// 🚨 **Payload GỐC, nguyên vẹn** (cùng giá trị đã lưu khi vào DLQ — `sal-005` §18.3 bất biến (1)).
    /// 📌 Chỉ trả ở **chi tiết** (không trả ở danh sách): payload có thể vài chục KB, và một trang 200 dòng
    /// sẽ kéo về vài MB **không cần thiết**.
    /// </summary>
    public string PayloadJson { get; set; } = default!;
}

/// <summary>Input đánh dấu đã xử lý.</summary>
public class DanhDauXuLyDlqInput
{
    /// <summary>🔒 **BẮT BUỘC.** Lý do xử lý — đây là **bằng chứng** ai đã quyết định gì với một sự kiện hỏng.</summary>
    [Required]
    [StringLength(eCommerceConsts.OperationsLimits.LyDoXuLyDlqToiDa)]
    public string LyDoXuLy { get; set; } = default!;
}

/// <summary>Thống kê DLQ — 🔒 dùng cho bảng điều khiển vận hành.</summary>
public class ThongKeDlqDto
{
    /// <summary>Tổng số dòng **chưa xử lý** — 📌 con số mà người trực cần thấy **đầu tiên**.</summary>
    public int SoChuaXuLy { get; set; }

    /// <summary>Tổng số dòng **đã xử lý**.</summary>
    public int SoDaXuLy { get; set; }

    /// <summary>Mốc **cũ nhất** trong các dòng chưa xử lý (`null` nếu không còn dòng nào).
    /// 🚨 **Đây là con số quan trọng nhất**: một dòng nằm đó **ba tuần** là dấu hiệu **không ai đọc DLQ** —
    /// mà đó chính là thất bại mà cả `XC-002` tồn tại để tránh.</summary>
    public DateTime? CuNhatChuaXuLyUtc { get; set; }

    /// <summary>Số dòng **chưa xử lý** theo từng lý do.</summary>
    public Dictionary<string, int> TheoLyDo { get; set; } = [];
}
