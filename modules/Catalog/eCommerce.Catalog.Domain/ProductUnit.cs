using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **ĐƠN VỊ TÍNH CỦA SẢN PHẨM** — WP-03 (`03 §3.1`, D-07).
///
/// 🚨 VÌ SAO ĐÂY LÀ NƠI GIỮ GIÁ MỚI (thay `ProductVariant`): Nhanh bán **một sản phẩm bằng nhiều ĐVT**
/// (`lon` = 1, `thùng` = 24) với **giá riêng từng ĐVT** và **barcode riêng từng ĐVT**. Nếu vẫn để giá ở
/// `ProductVariant` thì mỗi ĐVT phải giả thành một "biến thể" ⇒ tồn kho/IMEI/lô bị nhân bản sai tầng
/// (`02 §3.1`). Vì vậy: **sản phẩm là một**, ĐVT chỉ là **cách đóng gói + giá**.
///
/// 🔒 BẤT BIẾN:
/// <list type="bullet">
///   <item>`ConversionFactor > 0` — hệ số quy đổi về ĐVT gốc (1 = gốc). `0`/âm ⇒ chia cho 0 hoặc trừ tồn sai.</item>
///   <item>`SalePrice >= 0` — tiền **không âm** (cùng quy ước `Product.KiemTraGia`).</item>
///   <item>Barcode **duy nhất trong tenant** khi có (DB unique index) + **duy nhất trong sản phẩm** (domain).</item>
///   <item>`IsBase` **đúng một** cho mỗi sản phẩm (domain `Product.AddUnit` + unique index có filter ở DB).</item>
/// </list>
/// </summary>
public class ProductUnit : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của tên ĐVT — khớp `HasMaxLength(64)` ở DbContext.</summary>
    public const int DoDaiTenToiDa = 64;

    /// <summary>Độ dài tối đa của barcode — khớp `HasMaxLength(64)` ở DbContext.</summary>
    public const int DoDaiBarcodeToiDa = 64;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>Tên ĐVT hiển thị trên hoá đơn ("lon", "thùng", "kg"…).</summary>
    public string Name { get; protected set; } = default!;

    /// <summary>
    /// Hệ số quy đổi về ĐVT gốc (`1` = gốc, `24` = một thùng bằng 24 lon). **Luôn `> 0`**.
    /// 🚨 Dùng `decimal`, **không** `double` (quy ước #1) — cân đo cần số lẻ (`0.5 kg`).
    /// </summary>
    public decimal ConversionFactor { get; protected set; }

    /// <summary>Mã vạch của riêng ĐVT này (`null` = không có). Duy nhất trong tenant khi có.</summary>
    public string? Barcode { get; protected set; }

    /// <summary>ĐVT gốc — dùng để quy đổi tồn kho và làm đơn vị "1" khi tính giá.</summary>
    public bool IsBase { get; protected set; }

    /// <summary>Giá bán lẻ của ĐVT này (`decimal(18,4)`, **cấm** `double`).</summary>
    public decimal SalePrice { get; protected set; }

    /// <summary>Giá nhập gần nhất (tham khảo; giá vốn thật do module `Pricing`/`Inventory` quyết).</summary>
    public decimal? ImportPrice { get; internal set; }

    /// <summary>Giá buôn (bán sỉ) của ĐVT này.</summary>
    public decimal? WholesalePrice { get; internal set; }

    /// <summary>Giá cũ — dùng để gạch ngang khi hiển thị khuyến mại.</summary>
    public decimal? OldPrice { get; internal set; }

    protected ProductUnit() { }

    public ProductUnit(
        Guid id,
        Guid productId,
        string name,
        decimal conversionFactor,
        bool isBase,
        decimal salePrice,
        string? barcode = null) : base(id)
    {
        Name = ChuanHoaTen(name);

        if (conversionFactor <= 0m)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe)
                .WithData("truong", nameof(conversionFactor))
                .WithData("conversionFactor", conversionFactor)
                .WithData("lyDo", "Hệ số quy đổi phải LỚN HƠN 0.");
        }

        Product.KiemTraGia(salePrice);

        ProductId = productId;
        ConversionFactor = conversionFactor;
        IsBase = isBase;
        SalePrice = salePrice;
        Barcode = ChuanHoaBarcode(barcode);
    }

    /// <summary>Đổi giá bán lẻ — **NÉM LỖI** nếu âm (cùng guard với `Product.KiemTraGia`).</summary>
    public void DoiGiaBan(decimal giaMoi)
    {
        Product.KiemTraGia(giaMoi);
        SalePrice = giaMoi;
    }

    /// <summary>Đánh dấu là ĐVT gốc — 🔒 **chỉ** `Product.AddUnit` gọi (để giữ bất biến "đúng một gốc").</summary>
    internal void DatLamDonViGoc() => IsBase = true;

    /// <summary>Chuẩn hoá tên ĐVT: trim, không rỗng, không quá 64 ký tự.</summary>
    public static string ChuanHoaTen(string? ten)
    {
        if (string.IsNullOrWhiteSpace(ten))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe)
                .WithData("truong", "Name")
                .WithData("lyDo", "rỗng");
        }

        var s = ten.Trim();

        if (s.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe)
                .WithData("truong", "Name")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiTenToiDa);
        }

        return s;
    }

    /// <summary>
    /// Chuẩn hoá barcode: chuỗi rỗng/toàn khoảng trắng ⇒ `null` (**không** lưu chuỗi rỗng — nếu lưu, unique
    /// index coi mọi "không có mã" là **cùng một giá trị** và chặn nhầm sản phẩm thứ hai).
    /// </summary>
    public static string? ChuanHoaBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var s = barcode.Trim();

        if (s.Length > DoDaiBarcodeToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe)
                .WithData("truong", "Barcode")
                .WithData("doDai", s.Length)
                .WithData("toiDa", DoDaiBarcodeToiDa);
        }

        return s;
    }
}
