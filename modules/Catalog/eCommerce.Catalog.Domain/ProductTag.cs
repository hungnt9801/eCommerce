using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **NHÃN (TAG) GẮN TRÊN SẢN PHẨM** — WP-03 (`03 §3.1`).
///
/// 📌 VÌ SAO TÁCH BẢNG (không thêm cột `Tags` dạng CSV vào `CatProducts`): người dùng lọc **theo nhãn**
/// ("hàng mới về", "bán chạy") trên danh sách 20.000 sản phẩm; CSV thì `LIKE '%…%'` không dùng được index và
/// **gộp nhầm** `sale` với `sale-2026`, còn sửa một nhãn phải ghi lại cả chuỗi (dễ mất nhãn khác).
/// 🔒 `(ProductId, Name)` duy nhất trong tenant — cùng một sản phẩm không gắn trùng nhãn.
/// </summary>
public class ProductTag : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của nhãn — khớp `HasMaxLength(64)` ở DbContext.</summary>
    public const int DoDaiTenToiDa = 64;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    public string Name { get; protected set; } = default!;

    protected ProductTag() { }

    public ProductTag(Guid id, Guid productId, string name) : base(id)
    {
        ProductId = productId;
        Name = ChuanHoaTen(name);
    }

    /// <summary>Chuẩn hoá nhãn: trim, không rỗng, không quá 64 ký tự.</summary>
    public static string ChuanHoaTen(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Name")
                .WithData("lyDo", "Nhãn rỗng");
        }

        var s = name.Trim();

        if (s.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Name")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiTenToiDa);
        }

        return s;
    }
}
