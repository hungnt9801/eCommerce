using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Catalog.Dtos;

/// <summary>
/// Nút danh mục (`CAT-002` §6.1). Dùng **CÙNG** một DTO cho cả cây lồng nhau và danh sách phẳng
/// (khi phẳng thì <see cref="Children"/> rỗng) — tránh hai DTO gần giống nhau rồi lệch nhau.
/// </summary>
public class CategoryDto
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Slug { get; set; }

    /// <summary>
    /// Đường dẫn vật chất hoá `/guid/guid/`.
    /// ⚠️ Trả ra cho **admin** để nhìn được hình dạng cây thật khi truy lỗi — **KHÔNG** dùng làm
    /// thứ tự hiển thị (thứ tự theo `Guid` là **ngẫu nhiên**; xem ghi chú ở `CayDanhMucBuilder`).
    /// </summary>
    public string Path { get; set; } = default!;

    public int Level { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int? MaxVariantCombinations { get; set; }

    /// <summary>Chỉ có dữ liệu khi trả về **cây**; danh sách phẳng để rỗng.</summary>
    public List<CategoryDto> Children { get; set; } = new();
}

public class GetCategoriesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Tìm theo **mã hoặc tên**.</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái dùng/ngừng dùng (`null` = tất cả).</summary>
    public bool? IsActive { get; set; }

    /// <summary>Lọc theo nút cha (`null` = tất cả; truyền `Guid.Empty` = chỉ lấy NÚT GỐC).</summary>
    public Guid? ParentId { get; set; }
}

public class CreateCategoryDto
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    [StringLength(256)]
    public string? Slug { get; set; }

    /// <summary>`null` = tạo **nút gốc**.</summary>
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public int? MaxVariantCombinations { get; set; }
}

/// <summary>
/// Cập nhật nút danh mục. 🚨 **KHÔNG có `Code` và KHÔNG có `ParentId`**:
/// `Code` là khoá nghiệp vụ dùng cho import/báo cáo; `ParentId` chỉ đổi được qua <c>MoveAsync</c>
/// (đường **duy nhất** có khoá + chống chu trình — `CAT-002` D8).
/// </summary>
public class UpdateCategoryDto
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    [StringLength(256)]
    public string? Slug { get; set; }

    public int SortOrder { get; set; }

    public int? MaxVariantCombinations { get; set; }
}

/// <summary>Di chuyển nhánh. `ParentId = null` ⇒ thành **nút gốc**.</summary>
public class MoveCategoryDto
{
    public Guid? ParentId { get; set; }
}
