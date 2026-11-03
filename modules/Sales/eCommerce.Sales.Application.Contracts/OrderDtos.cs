using System.ComponentModel.DataAnnotations;
using eCommerce.Orders;

namespace eCommerce.Application.Contracts.Sales;

/// <summary>
/// 🆕 **`WP-05`** — **HỢP ĐỒNG DỮ LIỆU của màn ĐƠN HÀNG** (`07 §WP-05` việc 6, `04 §5` màn #10/#11).
///
/// 📌 **VÌ SAO DTO RIÊNG (không dùng lại `DonHangDto`):** `DonHangDto` là kết quả của **luồng checkout**
/// (`SAL-001`) — trả cả `TotalTuSoGiaiTrinh` (bằng chứng giá) và chỉ có một dòng `Status` suy ra. Màn danh sách
/// cần **tab đếm 14 nhóm**, `StatusLabel`, loại/nguồn đơn, vận chuyển, COD — thứ `DonHangDto` **không** có.
/// Nhồi thêm vào `DonHangDto` sẽ làm **một DTO hai mục đích** ⇒ checkout phải trả những trường nó không dùng.
/// </summary>
public class OrderListItemDto
{
    public Guid Id { get; set; }

    public string Number { get; set; } = default!;

    public string ChannelCode { get; set; } = default!;

    /// <summary>Loại đơn (Nhanh `type`) — 10 giá trị đo được.</summary>
    public OrderType Type { get; set; }

    /// <summary>Nguồn đơn (Nhanh `saleChannel`) — 13 giá trị đo được.</summary>
    public OrderSource Source { get; set; }

    public FulfillmentStatus FulfillmentStatus { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public ReconciliationStatus ReconciliationStatus { get; set; }

    public ReturnStatus ReturnStatus { get; set; }

    /// <summary>Giá trị **suy ra** (cột `Status`) — để tương thích với UI cũ; UI mới đọc `StatusLabel`.</summary>
    public OrderStatus DerivedStatus { get; set; }

    /// <summary>**Một nhãn** hiển thị (`03 §3.5`).</summary>
    public string StatusLabel { get; set; } = default!;

    public bool IsCancelled { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal? CodAmount { get; set; }

    public int LineCount { get; set; }

    public Guid? WarehouseId { get; set; }

    public string? CarrierCode { get; set; }

    public string? TrackingNumber { get; set; }

    /// <summary>Lý do nghiệp vụ (24 giá trị Nhanh) — `null` = chưa gắn.</summary>
    public OrderReasonCode? ReasonCode { get; set; }

    public DateTime CreationTime { get; set; }
}

/// <summary>Một **tab** của màn DS đơn — **có badge số** (pattern Nhanh: danh sách là *hàng đợi việc*).</summary>
public class OrderTabDto
{
    /// <summary>Khoá tab (`all`, `can-xu-ly`, `delivered`, `pending`…).</summary>
    public string Key { get; set; } = default!;

    public string Label { get; set; } = default!;

    public int Count { get; set; }
}

/// <summary>**Bộ đếm 14 tab** — một lời gọi, không 14 lời gọi (tránh N+1 từ UI).</summary>
public class OrderTabCountsDto
{
    public int Total { get; set; }

    public List<OrderTabDto> Tabs { get; set; } = new();
}

/// <summary>Một dòng của đơn trong màn chi tiết.</summary>
public class OrderLineItemDto
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = default!;

    public Guid? ProductId { get; set; }

    public int Quantity { get; set; }

    public int ReturnedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>
/// **Chi tiết đơn** — màn #11 (`04 §5`). Gồm: thông tin chung · dòng hàng · **các bước đi tiếp được**
/// (để UI chỉ hiện nút hợp lệ — *"trạng thái điều khiển UI"* `README §3.3`).
/// </summary>
public class OrderDetailDto : OrderListItemDto
{
    public string Currency { get; set; } = default!;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal ShippingTotal { get; set; }

    public decimal? ShippingFeeCustomer { get; set; }

    public decimal? ShippingFeeActual { get; set; }

    public int? WeightGram { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? SaleStaffId { get; set; }

    public Guid? PackerId { get; set; }

    public string? ExternalOrderId { get; set; }

    public string? CheckoutIdempotencyKey { get; set; }

    public string? Note { get; set; }

    /// <summary>`RowVersion` — UI gửi lại khi thao tác (chống ghi đè, `BR-010`).</summary>
    public long RowVersion { get; set; }

    public long FulfillmentVersion { get; set; }

    public long PaymentVersion { get; set; }

    public long ReconciliationVersion { get; set; }

    public long ClosureVersion { get; set; }

    /// <summary>
    /// 🆕 **Các trạng thái thực hiện ĐI TIẾP ĐƯỢC** từ trạng thái hiện tại (suy từ **bảng cạnh** của domain —
    /// một định nghĩa, không chép tay ở UI). 📌 UI render **đúng** danh sách này ⇒ người dùng **không** bấm
    /// được bước mà state machine sẽ ném `0001`.
    /// </summary>
    public List<FulfillmentStatus> AllowedTransitions { get; set; } = new();

    /// <summary>`true` = còn huỷ được (hàng chưa rời kho) — UI hiện nút Huỷ + 3 lựa chọn *ai huỷ*.</summary>
    public bool CanCancel { get; set; }

    public List<OrderLineItemDto> Lines { get; set; } = new();
}

/// <summary>
/// **Bộ lọc danh sách đơn** (`GET /api/v1/orders`) — gói trong DTO để **không** phụ thuộc quy tắc bind ngầm
/// của ASP.NET (bài học `TraHangInput`, `SAL-004` §40–41: body lồng ⇒ `400` mà không rõ vì sao).
/// </summary>
public class OrderListInput
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>Trang **1-based** (mặc định 1).</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Cỡ trang — **trần 200** (bảo vệ DB khỏi `pageSize=100000`).</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > 200 ? 20 : value;
    }

    /// <summary>`page` → `SkipCount` ở **một** chỗ (không để mỗi method tự nhân/rồi quên `-1`).</summary>
    public int SkipCount => (Page - 1) * PageSize;

    /// <summary>Khoá tab (`can-xu-ly`, `delivered`, `pending`…) — `null`/rỗng = tất cả.</summary>
    [StringLength(32)]
    public string? Tab { get; set; }

    /// <summary>Tìm theo **số đơn** hoặc **mã vận đơn** (chứa, không phân biệt hoa thường).</summary>
    [StringLength(64)]
    public string? Q { get; set; }

    public FulfillmentStatus? FulfillmentStatus { get; set; }

    public OrderType? Type { get; set; }

    public OrderSource? Source { get; set; }

    public Guid? WarehouseId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

/// <summary>
/// **API ĐƠN HÀNG** của Web Admin (`WP-05`) — chỉ **đọc** + **chuyển trạng thái qua `IOrderStatusAppService`**.
/// 🚨 **CẤM** khai ở đây đường ghi **trục tài chính** (BR-013): tiền chỉ do `PaymentHub`/IPN ghi.
/// </summary>
public interface IOrderAppService : Volo.Abp.Application.Services.IApplicationService
{
    /// <summary>DS đơn có phân trang + lọc (server-side).</summary>
    Task<Volo.Abp.Application.Dtos.PagedResultDto<OrderListItemDto>> GetListAsync(OrderListInput input);

    /// <summary>**Bộ đếm 14 tab** (tab "Cần xử lý" + 13 nhóm trạng thái).</summary>
    Task<OrderTabCountsDto> GetTabCountsAsync(OrderListInput input);

    /// <summary>Chi tiết đơn — gồm dòng hàng + các bước đi tiếp được.</summary>
    Task<OrderDetailDto> GetAsync(Guid id);
}
