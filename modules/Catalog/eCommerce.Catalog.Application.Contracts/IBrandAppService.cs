using eCommerce.Catalog.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Catalog;

/// <summary>
/// Hợp đồng quản trị **thương hiệu** (`CatBrands`) — chốt `CAT-002` **Q3** (27/10/2026).
///
/// 🔐 Dùng quyền `Catalog.Products` (quản trị danh mục sản phẩm) cho GĐ1 — **chưa** tách quyền riêng
/// `Brands.*`; tách quyền là thay đổi **hợp đồng phân quyền** (seed + UI + test) nên để lát cắt riêng.
/// 📌 Ghi rõ để người sau không tưởng đây là quyền tối thiểu đã cân nhắc.
/// </summary>
public interface IBrandAppService : IApplicationService
{
    Task<PagedResultDto<BrandDto>> GetListAsync(GetBrandsInput input);

    Task<BrandDto> GetAsync(Guid id);

    Task<BrandDto> CreateAsync(CreateBrandDto input);

    /// <summary>Đổi tên/slug/mô tả/logo/thứ tự. 🔒 **Không** đổi `Code`.</summary>
    Task<BrandDto> UpdateAsync(Guid id, UpdateBrandDto input);

    /// <summary>Ngừng dùng (không xoá cứng — sản phẩm cũ vẫn phải đọc được thương hiệu).</summary>
    Task DeactivateAsync(Guid id);

    Task ActivateAsync(Guid id);
}
