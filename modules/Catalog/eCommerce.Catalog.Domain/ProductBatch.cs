using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **LÔ HÀNG** của một sản phẩm loại <see cref="ProductType.BatchTracked"/> (`03 §3.1`).
///
/// 🚨 VÌ SAO LÔ PHẢI LÀ ENTITY: hàng theo lô có **hạn dùng riêng từng lô** và giá vốn **đích danh**; nếu chỉ
/// ghi "số lô" dạng chuỗi trên dòng phiếu thì (a) hai lô cùng mã ở hai lần nhập bị gộp, (b) **không** cảnh báo
/// được hạn dùng theo từng lô, (c) bán ra không biết trừ lô nào ⇒ sai giá vốn.
/// 🔒 `Code` duy nhất trong **(tenant, sản phẩm)** — cùng mã lô ở hai sản phẩm khác nhau là hợp lệ.
/// </summary>
public class ProductBatch : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của mã lô — khớp `HasMaxLength(64)` ở DbContext.</summary>
    public const int DoDaiCodeToiDa = 64;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>Mã lô (do người nhập hoặc NCC in trên bao bì).</summary>
    public string Code { get; protected set; } = default!;

    /// <summary>Ngày sản xuất (nếu có).</summary>
    public DateTime? ManufactureDate { get; set; }

    /// <summary>Hạn dùng (nếu có) — nguồn cho cảnh báo "sắp hết hạn".</summary>
    public DateTime? ExpiredDate { get; set; }

    /// <summary>Cảnh báo trước bao nhiêu ngày (Nhanh cho cấu hình theo lô/phiếu nhập).</summary>
    public int? PriorWarningDays { get; set; }

    /// <summary>`false` = ngừng dùng lô (không xoá — giữ lịch sử xuất/nhập đã tham chiếu).</summary>
    public bool IsActive { get; protected set; } = true;

    protected ProductBatch() { }

    public ProductBatch(Guid id, Guid productId, string code) : base(id)
    {
        ProductId = productId;
        Code = ChuanHoaCode(code);
    }

    /// <summary>Chuẩn hoá mã lô: trim, không rỗng, không quá 64 ký tự.</summary>
    public static string ChuanHoaCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Code")
                .WithData("lyDo", "Mã lô rỗng");
        }

        var s = code.Trim();

        if (s.Length > DoDaiCodeToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "Code")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiCodeToiDa);
        }

        return s;
    }

    /// <summary>Ngừng dùng lô — **không** xoá cứng (lịch sử phiếu nhập/xuất còn tham chiếu).</summary>
    public void NgungDung() => IsActive = false;
}
