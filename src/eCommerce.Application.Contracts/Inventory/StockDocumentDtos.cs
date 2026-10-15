using System.ComponentModel.DataAnnotations;
using eCommerce.Inventory;
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

    /// <summary>Số dòng **đếm từ DB** (không phải thuộc tính trên entity — bài học `PRC-001` §17.2 **L1**).</summary>
    public int LineCount { get; set; }
}

public class StockDocumentLineDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
}

public class CreateStockDocumentDto
{
    [Required]
    [StringLength(64)]
    public string Number { get; set; } = default!;

    public StockDocumentType Type { get; set; } = StockDocumentType.GoodsReceipt;

    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>Chỉ dùng cho `StockTransfer` — **bắt buộc** khi loại là chuyển kho.</summary>
    public Guid? ToWarehouseId { get; set; }

    public DateTime? OccurredAtUtc { get; set; }

    [StringLength(2048)]
    public string? Note { get; set; }

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
    public int Quantity { get; set; }
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

    Task<StockDocumentDto> GetAsync(Guid id);

    Task<List<StockDocumentLineDto>> GetLinesAsync(Guid id);

    /// <summary>
    /// Tạo chứng từ ở trạng thái **`Draft`** (**CHƯA** ảnh hưởng tồn kho — `INV-001` **ADR-02**).
    /// **NÉM** nếu kho không tồn tại / đã ngừng dùng, hoặc chuyển kho mà thiếu/trùng kho đích.
    /// </summary>
    Task<StockDocumentDto> CreateAsync(CreateStockDocumentDto input);

    /// <summary>
    /// 🚨 **GHI SỔ** — biến chứng từ thành **bút toán sổ cái** + **thay đổi tồn kho** (`INV-001` ADR-08/09).
    /// 📌 Tên hàm là `GhiSoAsync` (không phải `PostAsync`) **có chủ ý**: ABP **bỏ** tên hàm khi nó trùng động từ HTTP
    /// ⇒ `PostAsync` cho route mơ hồ `POST /stock-document/{id}` (đọc không biết là "ghi sổ" hay "sửa"); `GhiSoAsync`
    /// cho route **tường minh** `POST /stock-document/{id}/ghi-so`.
    /// **NÉM** nếu chứng từ không còn ở `Draft` (**chống ghi sổ hai lần**), hoặc tồn kho không đủ.
    /// </summary>
    Task<PostStockDocumentResultDto> GhiSoAsync(Guid id, DateTime? at = null);
}
