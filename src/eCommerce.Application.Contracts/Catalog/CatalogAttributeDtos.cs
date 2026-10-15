using System.ComponentModel.DataAnnotations;

namespace eCommerce.Catalog.Dtos;

public class CatalogAttributeDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>`Specification` hoặc `Variant` — **chuỗi** để client không phụ thuộc số enum.</summary>
    public string Usage { get; set; } = default!;

    public bool IsActive { get; set; }

    public List<CatalogAttributeValueDto> Values { get; set; } = new();
}

public class CatalogAttributeValueDto
{
    public Guid Id { get; set; }

    public Guid AttributeId { get; set; }

    public string Value { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}

public class CreateCatalogAttributeDto
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>`Specification` (chỉ mô tả) hoặc `Variant` (tham gia sinh tổ hợp) — BR-008.</summary>
    public CatalogAttributeUsage Usage { get; set; } = CatalogAttributeUsage.Variant;
}

public class AddAttributeValueDto
{
    [Required]
    [StringLength(256)]
    public string Value { get; set; } = default!;

    public int SortOrder { get; set; }
}

/// <summary>Chọn một giá trị thuộc tính cho sản phẩm (đầu vào của bước sinh tổ hợp).</summary>
public class SelectAttributeValueDto
{
    [Required]
    public Guid AttributeValueId { get; set; }

    public int SortOrder { get; set; }
}

public class ProductAttributeSelectionDto
{
    public Guid AttributeId { get; set; }

    public string AttributeCode { get; set; } = default!;

    public Guid AttributeValueId { get; set; }

    public string Value { get; set; } = default!;
}

public class GenerateVariantsDto
{
    /// <summary>
    /// Giá gán cho biến thể **mới**. 🚨 **BẮT BUỘC** khi sản phẩm **chưa có** biến thể nào —
    /// nếu không, biến thể sinh ra sẽ có **giá 0** và **BÁN ĐƯỢC với giá 0**. Khi đã có biến thể,
    /// để trống ⇒ kế thừa giá của biến thể đang hoạt động **rẻ nhất**.
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Giá mặc định phải lớn hơn 0.")]
    public decimal? DefaultPrice { get; set; }

    /// <summary>`MaxVariantCombinations` — để trống ⇒ dùng mặc định 200 (D6).</summary>
    public int? MaxCombinations { get; set; }
}

/// <summary>Kết quả sinh tổ hợp (`CAT-002` §4.1).</summary>
public class GenerateVariantsResultDto
{
    public int Created { get; set; }

    public int Existing { get; set; }

    /// <summary>Số biến thể **NGỪNG BÁN** vì tổ hợp không còn trong tập mong muốn (**BR-012** — **không** xoá).</summary>
    public int Deactivated { get; set; }

    /// <summary>Số biến thể **mở bán lại** vì tổ hợp đã quay lại (giữ nguyên `Id`/`Sku` — BR-013).</summary>
    public int Reactivated { get; set; }

    public List<string> Skus { get; set; } = new();
}
