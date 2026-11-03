using System.ComponentModel.DataAnnotations;
using eCommerce.Inventory;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

public class StockDocumentDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }

    // 🆕 `WP-04` — trường của phiếu kho kiểu Nhanh (hiển thị trên DS phiếu + chi tiết phiếu).
    /// <summary>`"In"` / `"Out"` / `null` (kiểm kê) — **chuỗi** để client không phải biết số enum.</summary>
    public string? Direction { get; set; }

    /// <summary>`"Supplier"`… (tên `StockDocumentMode`) hoặc `null`.</summary>
    public string? Mode { get; set; }

    public string? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string? RefType { get; set; }
    public string? RefId { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? IdempotencyKey { get; set; }

    /// <summary>Số dòng **đếm từ DB** (không phải thuộc tính trên entity — bài học `PRC-001` §17.2 **L1**).</summary>
    public int LineCount { get; set; }

    // 🆕 `WP-04` (việc 8) — tổng tiền của phiếu (số **suy ra** từ các dòng, tính bằng `TinhTienChungTu`).
    /// <summary>
    /// Tổng tiền hàng = Σ thành tiền dòng. `null` khi **có dòng chưa khai giá** (D-37: `0` sẽ là lời nói dối —
    /// "miễn phí" khác "chưa khai"), hoặc phiếu chưa có dòng.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>Tổng VAT = Σ tiền VAT dòng; `null` khi **không dòng nào** khai %VAT.</summary>
    public decimal? TotalVat { get; set; }

    /// <summary>Tổng thanh toán = `TotalAmount + TotalVat − DiscountAmount`; `null` khi `TotalAmount` là `null`.</summary>
    public decimal? TotalPayment { get; set; }
}

public class StockDocumentLineDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = default!;
    public decimal Quantity { get; set; }

    // 🆕 `WP-04`
    public Guid? UnitId { get; set; }
    public decimal? VatPercent { get; set; }
    public Guid? BatchId { get; set; }
    public string? ImeiSerials { get; set; }

    // 🆕 `WP-04` (việc 8) — tiền của dòng. 📌 `Amount`/`VatAmount` là **số SUY RA** (tính bằng
    // `TinhTienChungTu`) ⇒ client **không** tự nhân lại, tránh hai nơi ra hai kết quả.
    /// <summary>Đơn giá tại thời điểm lập phiếu (`null` = chưa khai giá).</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Thành tiền dòng = `Quantity × UnitPrice`; `null` nếu dòng không khai giá.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Tiền VAT dòng = `Amount × VatPercent / 100`; `null` nếu thiếu giá hoặc thiếu %VAT.</summary>
    public decimal? VatAmount { get; set; }
}

/// <summary>
/// 🆕 **`WP-04`** — một dòng **tồn kho 9 chiều** (cột tồn của màn DS phiếu &amp; màn kho).
/// 📌 Có cả **10 số lưu** (`OnHand`…`WaitingInbound`) và **2 số suy ra** (`InWarehouse`, `Available`) để client
/// **không tự tính lại** — hai nơi cùng tính một công thức là mầm lệch số.
/// </summary>
public class StockBalanceDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = default!;
    public string WarehouseName { get; set; } = default!;
    public string Sku { get; set; } = default!;

    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Transferring { get; set; }
    public decimal Shipping { get; set; }
    public decimal Holding { get; set; }
    public decimal Damaged { get; set; }
    public decimal Warranty { get; set; }
    public decimal WarrantyHolding { get; set; }
    public decimal PreOrder { get; set; }
    public decimal WaitingInbound { get; set; }

    /// <summary>Tồn trong kho = `OnHand − Transferring`.</summary>
    public decimal InWarehouse { get; set; }

    /// <summary>Có thể bán = `OnHand − Reserved − Transferring − Holding − Damaged`.</summary>
    public decimal Available { get; set; }
}

public class CreateStockDocumentDto
{
    [Required]
    [StringLength(64)]
    public string Number { get; set; } = default!;

    public StockDocumentType Type { get; set; } = StockDocumentType.GoodsReceipt;

    /// <summary>🆕 `WP-04` — kiểu phiếu Nhanh. `null` = phiếu kiểu cũ (bút toán suy từ `Type`).</summary>
    public StockDocumentMode? Mode { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>Chỉ dùng cho `StockTransfer` — **bắt buộc** khi loại là chuyển kho.</summary>
    public Guid? ToWarehouseId { get; set; }

    public DateTime? OccurredAtUtc { get; set; }

    [StringLength(2048)]
    public string? Note { get; set; }

    // 🆕 `WP-04` — đối tượng / nguồn / tiền / khoá chống trùng.
    public StockCounterpartyType? CounterpartyType { get; set; }
    public Guid? CounterpartyId { get; set; }

    [StringLength(64)]
    public string? RefType { get; set; }

    [StringLength(128)]
    public string? RefId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Chiết khấu không được âm.")]
    public decimal? DiscountAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Số đã trả không được âm.")]
    public decimal? PaidAmount { get; set; }

    /// <summary>
    /// Khoá chống tạo trùng (`ClientBillId` của POS — D-04). Cùng khoá gửi hai lần ⇒ **DB chặn** (unique index
    /// `IX_InvStockDocuments_TenantId_IdempotencyKey`), API trả **409**, **không** tạo phiếu thứ hai.
    /// </summary>
    [StringLength(128)]
    public string? IdempotencyKey { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Chứng từ phải có ít nhất một dòng.")]
    public List<CreateStockDocumentLineDto> Lines { get; set; } = new();
}

public class CreateStockDocumentLineDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    /// <summary>
    /// ⚠️ **KIỂM KÊ:** đây là **SỐ ĐẾM ĐƯỢC (số thực tế)**, **KHÔNG** phải "số lệch" — phần lệch do hệ thống tính.
    /// 📌 Diễn giải này **cần chốt với BA** (xem `inv-001` §18.5): hai cách hiểu cho hai kết quả khác nhau.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải ≥ 1.")]
    public decimal Quantity { get; set; }

    // 🆕 `WP-04` — ĐVT / VAT / lô / serial của dòng.
    public Guid? UnitId { get; set; }

    [Range(0, 100, ErrorMessage = "% VAT phải trong khoảng 0..100.")]
    public decimal? VatPercent { get; set; }

    public Guid? BatchId { get; set; }

    /// <summary>Serial/IMEI, phân tách bằng dấu phẩy. **Số serial PHẢI bằng `Quantity`** nếu khai.</summary>
    [StringLength(4000)]
    public string? ImeiSerials { get; set; }

    /// <summary>
    /// 🆕 **`WP-04` (việc 8)** — **Đơn giá** của dòng (VND). `null` = không khai giá (hàng biếu tặng/kiểm kê).
    /// 📌 **>= 0** — tiền âm không có nghĩa; chiết khấu là `DiscountAmount` **của phiếu** (`D-37`).
    /// 📌 Giới hạn trên `9999999999999999` = biên của `numeric(18,2)` ⇒ chặn ở DTO thay vì để DB ném `22003`.
    /// </summary>
    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Đơn giá phải từ 0 đến 9999999999999999.")]
    public decimal? UnitPrice { get; set; }
}

/// <summary>Kết quả ghi sổ — cho biết **bao nhiêu bút toán** đã sinh (0 ⇒ kiểm kê khớp, không có gì để ghi).</summary>
public class PostStockDocumentResultDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int PostedEntries { get; set; }
}

/// <summary>
/// **API KHO** (`INV-001`) — tạo chứng từ (nhập/chuyển/kiểm kê) và **ghi sổ**.
/// 🔐 Quyền gắn ở **lớp hiện thực** (Contracts không tham chiếu MVC — giữ layering).
/// </summary>
public interface IStockDocumentAppService : IApplicationService
{
    Task<List<StockDocumentDto>> GetListAsync(StockDocumentType? type = null);

    /// <summary>
    /// 🆕 **`WP-04`** — DS phiếu **có phân trang + lọc** cho màn #7 (`04 §5`): chiều (Nhập/Xuất), trạng thái,
    /// kiểu phiếu, khoảng ngày. 📌 Lọc ở **server** (20.000 phiếu không thể tải hết rồi lọc ở client).
    /// </summary>
    Task<PagedResultDto<StockDocumentDto>> GetPagedAsync(
        StockDocumentDirection? direction = null,
        StockDocumentStatus? status = null,
        StockDocumentMode? mode = null,
        Guid? warehouseId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skipCount = 0,
        int maxResultCount = 50);

    Task<StockDocumentDto> GetAsync(Guid id);

    Task<List<StockDocumentLineDto>> GetLinesAsync(Guid id);

    /// <summary>
    /// Tạo chứng từ ở trạng thái **`Draft`** (**CHƯA** ảnh hưởng tồn kho — `INV-001` **ADR-02**).
    /// **NÉM** nếu kho không tồn tại / đã ngừng dùng, hoặc chuyển kho mà thiếu/trùng kho đích.
    /// 🆕 `WP-04`: cũng **NÉM** khi `Mode` mâu thuẫn `Type`, khi đối tác/nguồn thiếu vế, hoặc tiền âm.
    /// </summary>
    Task<StockDocumentDto> CreateAsync(CreateStockDocumentDto input);

    /// <summary>
    /// 🆕 **`WP-04`** — **HUỶ** phiếu còn `Draft`. **NÉM** nếu phiếu đã ghi sổ (sổ cái append-only ⇒ muốn đảo
    /// phải tạo phiếu ĐẢO, không xoá bút toán).
    /// </summary>
    Task<StockDocumentDto> CancelAsync(Guid id);

    /// <summary>
    /// 🚨 **GHI SỔ** — biến chứng từ thành **bút toán sổ cái** + **thay đổi tồn kho** (`INV-001` ADR-08/09).
    /// 📌 Tên hàm là `GhiSoAsync` (không phải `PostAsync`) **có chủ ý**: ABP **bỏ** tên hàm khi nó trùng động từ HTTP
    /// ⇒ `PostAsync` cho route mơ hồ `POST /stock-document/{id}` (đọc không biết là "ghi sổ" hay "sửa"); `GhiSoAsync`
    /// cho route **tường minh** `POST /stock-document/{id}/ghi-so`.
    /// **NÉM** nếu chứng từ không còn ở `Draft` (**chống ghi sổ hai lần**), hoặc tồn kho không đủ.
    /// </summary>
    Task<PostStockDocumentResultDto> GhiSoAsync(Guid id, DateTime? at = null);
}
