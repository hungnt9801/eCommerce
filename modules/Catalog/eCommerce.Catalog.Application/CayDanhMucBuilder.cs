using eCommerce.Catalog.Dtos;

namespace eCommerce.Catalog;

/// <summary>
/// **Dựng cây danh mục từ danh sách PHẲNG** — hàm **THUẦN** (không DB, không DI) nên test được trực tiếp.
///
/// 🚨 **HAI CÁI BẪY ĐÃ XỬ LÝ CÓ CHỦ Ý:**
///
/// 1. **KHÔNG dùng thứ tự của `Path` để sắp xếp hiển thị.** `Path` chứa `Guid` (**D2**), mà `Guid` sắp xếp
///    theo **giá trị byte = ngẫu nhiên** ⇒ menu sẽ hiện theo thứ tự **vô nghĩa với người dùng** — và đây là
///    loại lỗi **trông vẫn "chạy được"** nên rất dễ bị bỏ qua. Thứ tự hiển thị là `SortOrder`, rồi tới `Name`.
///
/// 2. **Nút cha KHÔNG có trong danh sách ⇒ NÉM LỖI, KHÔNG im lặng bỏ qua.** Nếu ai đó truyền vào một danh
///    sách **đã lọc** (ví dụ chỉ nút đang hoạt động) thì các nút con sẽ **mất tích khỏi menu** mà **không có
///    lỗi nào**. Trong dữ liệu hợp lệ, nút cha **luôn** tồn tại: `CategoryTreeService.XoaMemAsync` **chặn**
///    xoá nút còn con (`CAT-002` D9) ⇒ không thể có nút mồ côi. Vậy thấy mồ côi nghĩa là **dữ liệu hỏng**
///    hoặc **danh sách bị lọc** — cả hai đều phải **lộ ra**, không được che.
/// </summary>
public static class CayDanhMucBuilder
{
    /// <summary>
    /// Dựng cây lồng nhau. ⚠️ <paramref name="phang"/> **PHẢI là toàn bộ** nút của tenant (xem bẫy #2).
    /// </summary>
    public static List<CategoryDto> DungCay(IReadOnlyList<Category> phang)
    {
        var theoId = phang.ToDictionary(x => x.Id, ToDto);

        // Thứ tự hiển thị: `SortOrder` rồi `Name` (KHÔNG theo `Path` — xem bẫy #1).
        var daSapXep = phang
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var goc = new List<CategoryDto>();

        foreach (var nut in daSapXep)
        {
            var dto = theoId[nut.Id];

            if (nut.ParentId is not { } chaId)
            {
                goc.Add(dto);
                continue;
            }

            if (!theoId.TryGetValue(chaId, out var cha))
            {
                throw new InvalidOperationException(
                    $"Nút danh mục '{nut.Code}' ({nut.Id}) trỏ tới cha {chaId} nhưng cha KHÔNG có trong danh sách. " +
                    "Danh sách đưa vào phải là TOÀN BỘ nút của tenant (nút mồ côi = dữ liệu hỏng, " +
                    "vì CategoryTreeService chặn xoá nút còn con).");
            }

            cha.Children.Add(dto);
        }

        return goc;
    }

    /// <summary>Map một nút sang DTO **phẳng** (không có con).</summary>
    public static CategoryDto ToDto(Category nut) => new()
    {
        Id = nut.Id,
        ParentId = nut.ParentId,
        Code = nut.Code,
        Name = nut.Name,
        Slug = nut.Slug,
        Path = nut.Path,
        Level = nut.Level,
        SortOrder = nut.SortOrder,
        IsActive = nut.IsActive,
        MaxVariantCombinations = nut.MaxVariantCombinations,
        Children = new List<CategoryDto>()
    };
}
