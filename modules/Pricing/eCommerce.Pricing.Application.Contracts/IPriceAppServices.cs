using eCommerce.Pricing.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Pricing;

/// <summary>
/// Hợp đồng app service **quản lý bảng giá** (`PRC-001` §6.1).
/// 🔐 Quyền gắn ở **lớp hiện thực** (Contracts không tham chiếu MVC — giữ layering).
/// </summary>
public interface IPriceListAppService : IApplicationService
{
    Task<List<PriceListDto>> GetListAsync();

    Task<PriceListDto> GetAsync(Guid id);

    /// <summary>Tạo bảng giá ⇒ trạng thái `Draft` (`PRC-001` Basic Flow bước 1).</summary>
    Task<PriceListDto> CreateAsync(CreatePriceListDto input);

    Task<List<PriceListItemDto>> GetItemsAsync(Guid priceListId);

    /// <summary>
    /// Thêm/đổi một dòng giá. 🚨 **NÉM** `Pricing:0003` nếu bảng giá đã/đang hiệu lực
    /// (**append-only** — BR-004: sửa giá trong bảng đang hiệu lực làm đơn/báo cáo cũ trỏ tới con số đã biến mất).
    /// </summary>
    Task<PriceListItemDto> AddItemAsync(Guid priceListId, AddPriceListItemDto input);

    /// <summary>`Draft → Approved`; **NÉM** `Pricing:0002` nếu bảng giá **chưa có dòng** (E1).</summary>
    Task<PriceListDto> PublishAsync(Guid priceListId);

    /// <summary>`Approved → Active` **nếu đã tới ngày** (chưa tới ngày ⇒ **không** đổi, **không** ném — ADR-11).</summary>
    Task<PriceListDto> ActivateAsync(Guid priceListId, DateTime? at = null);
}

/// <summary>
/// 🚨 **BỘ TRA GIÁ** — điểm vào **DUY NHẤT** để lấy giá bán (`PRC-001` §6.1; `SAL-002` gọi khi dựng giỏ/checkout).
///
/// 📌 **TÁCH 2 TẦNG CÓ CHỦ Ý** (`PRC-002` **ADR-04**): lớp này **nạp DB** rồi giao cho
/// `GiaCoSoResolver` — một **hàm THUẦN** (không DB, không đọc đồng hồ). Nhờ vậy luật tiền **test được**
/// không cần PostgreSQL, và không trộn I/O với luật.
/// </summary>
public interface IPriceResolverAppService : IApplicationService
{
    /// <summary>
    /// Tra giá cho **một** SKU. **NÉM** `Pricing:0004` khi không có giá (mặc định) —
    /// bán với giá đoán mò là lỗi **không sửa được về sau** (D14).
    /// </summary>
    Task<ResolvedPriceDto> ResolveAsync(ResolvePriceInput input);

    /// <summary>Tra giá cho **nhiều** dòng (giỏ hàng) trong **một** lần nạp — tránh N+1 (NFR-002).</summary>
    Task<List<ResolvedPriceDto>> ResolveManyAsync(ResolveManyInput input);
}

/// <summary>
/// 🚨 **TÍNH TIỀN CHO CẢ GIỎ** (`PRC-002` §4.2 — pipeline **B1→B8**) — điểm vào mà **checkout** (`SAL-002`) gọi.
///
/// 📌 **KHÁC `IPriceResolverAppService` ở đâu:** `IPriceResolverAppService` chỉ trả **đơn giá** (B2, giá cơ sở).
/// Interface **này** chạy **hết pipeline**: giá cơ sở → **bậc thang** (B3) → chọn đơn giá (B5) → **làm tròn R1**
/// → **phân bổ giảm giá/phí** (R2/R3) → **thuế từng dòng** (R4) → **tổng = Σ dòng**.
/// ⇒ Dùng **cái này** cho giỏ/checkout; dùng cái kia khi chỉ cần **tra một đơn giá** (ví dụ hiển thị "từ … đồng").
/// </summary>
public interface IPriceCalculationAppService : IApplicationService
{
    /// <summary>
    /// Tính tiền cả giỏ. **NÉM** `Pricing:0004` khi một dòng **không có giá nào** (kể cả giá niêm yết) —
    /// **không** bao giờ trả giá 0 thay cho "không biết giá" (D14).
    /// </summary>
    Task<GioTinhTienDto> TinhGioAsync(TinhGioInput input);
}
