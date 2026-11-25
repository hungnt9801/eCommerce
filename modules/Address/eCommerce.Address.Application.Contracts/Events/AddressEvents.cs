namespace eCommerce.Address.Events;

/// <summary>
/// Sự kiện: **trạng thái/nội dung của một `Address` đã đổi** (`ADDR-004` §7.1).
///
/// 🚨 **CẤM TUYỆT ĐỐI** chứa `SoNhaDuong`, `DiaChiDayDu`, họ tên/SĐT người nhận (`XC-002` **D11**):
/// ① PII trong event bus là PII **khó xoá nhất** (đã fan-out tới nhiều consumer, và
///    `MsgIdempotencyRecords` **giữ payload hash** ⇒ muốn xoá phải xoá cả bản ghi đó);
/// ② consumer nhận text địa chỉ sẽ **dùng text đó** thay vì `AddressId` ⇒ sinh **bản sao thứ hai**
///    của địa chỉ mà **không ai quản lý** (`ADR-10`).
/// 🔑 Consumer cần nội dung ⇒ gọi `IAddressDirectory` (một nguồn sự thật).
/// </summary>
[Serializable]
public class AddressChangedEto
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    public Guid? TenantId { get; set; }

    /// <summary>Khoá của `Address` — 🔑 trường **duy nhất** consumer cần để trỏ tới địa chỉ.</summary>
    public Guid AddressId { get; set; }

    /// <summary>`AddressId` (giữ tên chuẩn của `XC-002` để handler dùng chung khoá chống trùng).</summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// ⚠️ ABP **không** cấp sẵn trường này: dùng `ConcurrencyStamp` (đổi sau **mỗi** lần ghi) làm giá trị
    /// chống áp dụng bản cũ. Giữ **tên** `AggregateVersion` để không lệch `XC-002` **D6/BR-014**.
    /// </summary>
    public string? AggregateVersion { get; set; }

    /// <summary>`ChuaChuanHoa` / `DaChuanHoa` / `DaXacMinh` / `NghiNgo`.</summary>
    public string TrangThai { get; set; } = default!;

    public int SchemaVersion { get; set; }

    public string? TinhCode { get; set; }

    public string? HuyenCode { get; set; }

    public string? XaCode { get; set; }

    /// <summary>Mã tương quan để nối log giữa các service (`XC-005`).</summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Sự kiện: **cache tra cứu danh mục ĐVHC cần bị xoá** (`ADDR-001` BR-014, `D-242`).
///
/// 🔑 Mang <see cref="ParentCodes"/> **cần để hành động** ⇒ phát qua **outbox** trong **cùng UoW** với
/// dòng lô `DaApDung` (nguyên tử với dữ liệu). Mất sự kiện ⇒ chỉ trễ xoá cache (TTL 24 h chặn) ⇒ **không**
/// phải dữ liệu nghiệp vụ, nhưng vẫn dùng outbox để không có trạng thái "lô đã áp, instance khác giữ cache cũ".
///
/// 📌 Phần tử `null` = danh sách **tỉnh** (con của `parentCode = null`).
/// 🔒 KHÔNG chứa nội dung địa chỉ/PII — chỉ khoá cache.
/// </summary>
[Serializable]
public class AddressCacheInvalidatedEto
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    public Guid? TenantId { get; set; }

    public int SchemaVersion { get; set; }

    /// <summary>
    /// Danh sách `parentCode` cần xoá (hợp của danh mục **mới** và danh mục **hiện có trước commit**);
    /// `null` = danh sách tỉnh. Thiếu vế "hiện có" ⇒ tỉnh vừa biến mất giữ cache con tới hết TTL.
    /// </summary>
    public List<string?> ParentCodes { get; set; } = [];
}
///
/// 📌 Consumer: `Sales` (gợi ý địa chỉ ở checkout), `WEB-*` (xoá cache), `NOT-*`.
/// 🔒 **KHÔNG** chứa nội dung địa chỉ — cùng lý do như <see cref="AddressChangedEto"/>.
/// </summary>
[Serializable]
public class CustomerDefaultAddressChangedEto
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    public Guid? TenantId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>`Shipping` / `Billing` — mặc định tính **theo từng loại** (`BR-ADDR-004-006`).</summary>
    public string AddressType { get; set; } = default!;

    public Guid AddressId { get; set; }

    public Guid LinkId { get; set; }

    public Guid AggregateId { get; set; }

    /// <summary>`ConcurrencyStamp` của liên kết vừa đổi — xem chú thích ở <see cref="AddressChangedEto"/>.</summary>
    public string? AggregateVersion { get; set; }

    /// <summary>`true` = mục vừa được **đặt** làm mặc định · `false` = mục **mất** cờ (có mục khác thay).</summary>
    public bool LaMacDinh { get; set; }

    public string? CorrelationId { get; set; }
}
