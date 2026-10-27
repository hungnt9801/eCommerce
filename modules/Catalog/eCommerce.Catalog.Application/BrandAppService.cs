using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Catalog;

/// <summary>
/// Quản trị **thương hiệu** (`CatBrands`, 27/10/2026 — `CAT-002` Q3).
///
/// 🔐 Dùng `Catalog.Products` cho GĐ1 (xem ghi chú ở hợp đồng): tách quyền `Brands.*` là lát cắt riêng.
/// 🔒 **Kiểm trùng trước khi ghi** (mã/slug) để trả mã nghiệp vụ rõ; DB unique index là **hàng rào cuối**
/// và được ánh xạ qua `ViPhamRangBuocDb` (hai request đồng thời vẫn không tạo bản ghi trùng).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Products.Default)]
public class BrandAppService : ApplicationService, IBrandAppService
{
    private readonly IRepository<Brand, Guid> _thuongHieu;
    private readonly IAsyncQueryableExecuter _thucThi;

    public BrandAppService(IRepository<Brand, Guid> thuongHieu, IAsyncQueryableExecuter thucThi)
    {
        _thuongHieu = thuongHieu;
        _thucThi = thucThi;
    }

    public async Task<PagedResultDto<BrandDto>> GetListAsync(GetBrandsInput input)
    {
        var truyVan = await _thuongHieu.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var tuKhoa = input.Filter.Trim();
            truyVan = truyVan.Where(x =>
                x.Code.Contains(tuKhoa) || x.Name.Contains(tuKhoa) || x.Slug.Contains(tuKhoa));
        }

        if (input.IsActive is { } hoatDong)
        {
            truyVan = truyVan.Where(x => x.IsActive == hoatDong);
        }

        var tong = await _thucThi.CountAsync(truyVan);

        var ds = await _thucThi.ToListAsync(
            truyVan.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<BrandDto>(tong, ds.Select(ToDto).ToList());
    }

    public async Task<BrandDto> GetAsync(Guid id) => ToDto(await _thuongHieu.GetAsync(id));

    public async Task<BrandDto> CreateAsync(CreateBrandDto input)
    {
        var moi = new Brand(
            GuidGenerator.Create(), input.Code, input.Name, input.Slug, input.Description, input.LogoUrl, input.SortOrder);

        await KiemTraTrungAsync(moi.Code, moi.Slug, id: null);

        await _thuongHieu.InsertAsync(moi);

        return ToDto(moi);
    }

    public async Task<BrandDto> UpdateAsync(Guid id, UpdateBrandDto input)
    {
        var thuongHieu = await _thuongHieu.GetAsync(id);

        thuongHieu.CapNhat(input.Name, input.Slug, input.Description, input.LogoUrl, input.SortOrder);

        await KiemTraTrungAsync(thuongHieu.Code, thuongHieu.Slug, id);

        await _thuongHieu.UpdateAsync(thuongHieu);

        return ToDto(thuongHieu);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var thuongHieu = await _thuongHieu.GetAsync(id);
        thuongHieu.NgungHoatDong();
        await _thuongHieu.UpdateAsync(thuongHieu);
    }

    public async Task ActivateAsync(Guid id)
    {
        var thuongHieu = await _thuongHieu.GetAsync(id);
        thuongHieu.KichHoat();
        await _thuongHieu.UpdateAsync(thuongHieu);
    }

    private async Task KiemTraTrungAsync(string code, string slug, Guid? id)
    {
        var trungCode = await _thuongHieu.AnyAsync(x => x.Code == code && (id == null || x.Id != id));
        if (trungCode)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThuongHieuDaTonTai)
                .WithData("code", code);
        }

        var trungSlug = await _thuongHieu.AnyAsync(x => x.Slug == slug && (id == null || x.Id != id));
        if (trungSlug)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SlugThuongHieuDaTonTai)
                .WithData("slug", slug);
        }
    }

    private static BrandDto ToDto(Brand x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Slug = x.Slug,
        Description = x.Description,
        LogoUrl = x.LogoUrl,
        IsActive = x.IsActive,
        SortOrder = x.SortOrder
    };
}
