namespace eCommerce.Tax;

/// <summary>
/// Loại mức thuế suất.
/// <c>Standard</c> = mức theo quy định (0/5/10%) · <c>Reduced</c> = mức sau khi giảm (8%).
/// </summary>
public enum TaxRateKind
{
    Standard = 0,
    Reduced = 1
}

/// <summary>Đối tượng được phân loại thuế.</summary>
public enum TaxClassificationTargetType
{
    /// <summary>Phân loại ở cấp SKU — ưu tiên cao nhất (đã chốt ở Q1).</summary>
    Sku = 0,

    /// <summary>Phân loại ở cấp danh mục — dùng cho nhóm lớn để giảm khối lượng nhập liệu.</summary>
    Category = 1
}

/// <summary>
/// Vòng đời bản ghi phân loại (xem state machine §3 của tài liệu thiết kế).
///
/// CHỈ <see cref="Approved"/> và <see cref="Active"/> mới ảnh hưởng tới việc tính thuế.
/// </summary>
public enum TaxClassificationStatus
{
    /// <summary>Kế toán đang soạn — CHƯA ảnh hưởng tính thuế.</summary>
    Draft = 0,

    /// <summary>Kế toán trưởng đã duyệt, chờ tới ngày hiệu lực.</summary>
    Approved = 1,

    /// <summary>Đang có hiệu lực.</summary>
    Active = 2,

    /// <summary>Đã bị bản ghi mới thay thế.</summary>
    Superseded = 3,

    /// <summary>Đã hết khung hiệu lực.</summary>
    Expired = 4,

    /// <summary>Đã huỷ (chỉ huỷ được khi còn Draft).</summary>
    Cancelled = 5
}

/// <summary>
/// Nguồn mà hệ thống đã dùng để chọn mức thuế — ghi rõ để **giải trình với cơ quan thuế**.
/// </summary>
public enum TaxClassificationSource
{
    /// <summary>Lấy từ phân loại cấp SKU.</summary>
    Sku = 0,

    /// <summary>Lấy từ phân loại cấp danh mục.</summary>
    Category = 1,

    /// <summary>Không có phân loại nào ⇒ dùng mức mặc định của tenant (KHÔNG được giảm).</summary>
    TenantDefault = 2,

    /// <summary>SKU chưa phân loại ⇒ cờ <c>NeedsClassification = true</c> và CHẶN phát hành hoá đơn.</summary>
    Unclassified = 3
}
