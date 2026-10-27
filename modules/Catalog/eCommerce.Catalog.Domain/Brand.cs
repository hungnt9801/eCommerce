using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **THƯƠNG HIỆU** (`CatBrands`) — chốt `CAT-002` **Q3**: Brand là **entity riêng**, không phải chuỗi trên
/// `Product` (27/10/2026).
///
/// 🔑 **VÌ SAO ENTITY RIÊNG (thay vì `string Brand` trên Product):**
/// <list type="number">
///   <item>**Một thương hiệu ⇒ một slug/logo/mô tả** — dùng lại cho trang thương hiệu, lọc, SEO; chuỗi tự do
///     trên từng sản phẩm thì mỗi sản phẩm gõ một kiểu (`Samsung` / `samsung` / `SS`) và **không gộp được**;</item>
///   <item>**Đổi tên/logo một lần** áp cho mọi sản phẩm; chuỗi thì phải sửa từng dòng (và quên là lệch);</item>
///   <item>`Product.BrandId` **đã tồn tại từ trước** nhưng **mồ côi** (không bảng, không FK) — thêm bảng là
///     cách đóng đúng khoảng trống thay vì xoá cột (xoá cột sẽ mất dữ liệu người dùng đã nhập).</item>
/// </list>
///
/// 🔒 **BẤT BIẾN:** `Code` và `Slug` **duy nhất trong tenant** (unique index `NULLS NOT DISTINCT` ở DB);
/// `Code` **bất biến sau khi tạo** (dùng cho import/báo cáo — đổi mã làm lệch đối soát), còn `Name`/`Slug`/
/// mô tả/logo đổi được.
///
/// 📌 Không có navigation `Products` (mỗi sản phẩm là aggregate riêng) — tránh nạp cả danh mục sản phẩm
/// chỉ để đổi tên thương hiệu.
/// </summary>
public class Brand : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiCodeToiDa = 64;
    public const int DoDaiTenToiDa = 256;
    public const int DoDaiSlugToiDa = 256;
    public const int DoDaiMoTaToiDa = 2048;
    public const int DoDaiLogoUrlToiDa = 1024;

    public Guid? TenantId { get; protected set; }

    /// <summary>Mã nghiệp vụ, **duy nhất trong tenant**; 🔒 **bất biến** sau khi tạo.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Slug công khai (SEO) — duy nhất trong tenant.</summary>
    public string Slug { get; protected set; } = default!;

    public string? Description { get; protected set; }

    public string? LogoUrl { get; protected set; }

    /// <summary>Ngừng dùng thì **không** cho gán vào sản phẩm mới (kiểm ở app service), nhưng **không** xoá
    /// liên kết cũ — lịch sử bán vẫn phải đọc được thương hiệu.</summary>
    public bool IsActive { get; protected set; } = true;

    public int SortOrder { get; protected set; }

    protected Brand()
    {
    }

    public Brand(
        Guid id,
        string code,
        string name,
        string? slug = null,
        string? description = null,
        string? logoUrl = null,
        int sortOrder = 0) : base(id)
    {
        Code = ChuanHoaCode(code);
        Name = ChuanHoaTen(name);
        Slug = ChuanHoaSlug(slug, Name);
        Description = ChuanHoaMoTa(description);
        LogoUrl = ChuanHoaLogo(logoUrl);
        SortOrder = sortOrder;
        IsActive = true;
    }

    /// <summary>Đổi thông tin hiển thị. 🔒 **Không** đổi `Code` (bất biến) và **không** đổi `IsActive`.</summary>
    public void CapNhat(
        string name,
        string? slug,
        string? description,
        string? logoUrl,
        int sortOrder)
    {
        Name = ChuanHoaTen(name);
        Slug = ChuanHoaSlug(slug, Name);
        Description = ChuanHoaMoTa(description);
        LogoUrl = ChuanHoaLogo(logoUrl);
        SortOrder = sortOrder;
    }

    public void NgungHoatDong() => IsActive = false;

    public void KichHoat() => IsActive = true;

    private static string ChuanHoaCode(string? code)
    {
        var ma = (code ?? string.Empty).Trim();

        if (ma.Length == 0 || ma.Length > DoDaiCodeToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(Code))
                .WithData("lyDo", $"Mã thương hiệu rỗng hoặc quá {DoDaiCodeToiDa} ký tự.");
        }

        return ma;
    }

    private static string ChuanHoaTen(string? name)
    {
        var ten = (name ?? string.Empty).Trim();

        if (ten.Length == 0 || ten.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(Name))
                .WithData("lyDo", $"Tên thương hiệu rỗng hoặc quá {DoDaiTenToiDa} ký tự.");
        }

        return ten;
    }

    private static string ChuanHoaSlug(string? slug, string ten)
    {
        var s = string.IsNullOrWhiteSpace(slug) ? SinhSlug.Tu(ten) : SinhSlug.Tu(slug);

        if (s.Length == 0)
        {
            // 🔒 KHÔNG bịa `brand-{guid}`: Guid không có giá trị SEO và che dữ liệu vào vô nghĩa
            // (cùng lý do `CAT-003` ADR-04 đã chọn cho sản phẩm).
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongSinhDuocSlug)
                .WithData("ten", ten);
        }

        if (s.Length > DoDaiSlugToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThuongHieuKhongHopLe)
                .WithData("truong", nameof(Slug))
                .WithData("lyDo", $"Slug quá {DoDaiSlugToiDa} ký tự.");
        }

        return s;
    }

    private static string? ChuanHoaMoTa(string? description)
    {
        var moTa = description?.Trim();

        if (string.IsNullOrEmpty(moTa))
        {
            return null;
        }

        if (moTa.Length > DoDaiMoTaToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThuongHieuKhongHopLe)
                .WithData("truong", nameof(Description))
                .WithData("lyDo", $"Mô tả quá {DoDaiMoTaToiDa} ký tự.");
        }

        return moTa;
    }

    private static string? ChuanHoaLogo(string? logoUrl)
    {
        var logo = logoUrl?.Trim();

        if (string.IsNullOrEmpty(logo))
        {
            return null;
        }

        if (logo.Length > DoDaiLogoUrlToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThuongHieuKhongHopLe)
                .WithData("truong", nameof(LogoUrl))
                .WithData("lyDo", $"LogoUrl quá {DoDaiLogoUrlToiDa} ký tự.");
        }

        return logo;
    }
}
