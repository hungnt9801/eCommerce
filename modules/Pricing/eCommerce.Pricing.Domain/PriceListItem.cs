using eCommerce.Kernel.Money;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// **Một dòng giá trong bảng giá** (`PrcPriceListItems`) — `PRC-001` BR-008.
///
/// 🚨 **BR-001**: mọi khoản tiền **phải** là `Money` **có loại tiền**; **CẤM** lưu `decimal` tiền mà
/// **không** có cột tiền tệ đi kèm. Ở đây tiền tệ lấy từ **bảng giá cha** (`PriceList.Currency`) và
/// **mọi dòng phải cùng loại tiền với cha** — nếu cho phép mỗi dòng một loại tiền thì bảng giá trở thành
/// nơi **trộn tiền tệ**, và mọi phép cộng/so sánh sau đó **sai mà không có lỗi nào**.
/// </summary>
public class PriceListItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid PriceListId { get; protected set; }

    /// <summary>
    /// SKU — **chuỗi**, KHÔNG phải FK tới `ProductVariant` (`PRC-001` §15.1 Q1: Pricing **không** tham chiếu Catalog).
    /// </summary>
    public string Sku { get; protected set; } = default!;

    /// <summary>
    /// 🚨 **Số tiền lưu dạng `decimal` — nhưng LUÔN đi kèm cột <see cref="CurrencyCode"/>** (`PRC-001` BR-001:
    /// *"cấm lưu `decimal` tiền mà **không** có cột tiền tệ đi kèm"*).
    /// 📌 **Vì sao không map thẳng `Money`:** `Money` là record **ctor private** và EF **không** value-convert
    /// sạch được; lưu **2 cột** vẫn thoả BR-001 **và** đọc lại đúng loại tiền (convert sang `decimal` rồi tự
    /// gán `Money.Vnd` sẽ biến một dòng **USD** thành **VND** ⇒ sai 25.000 lần **mà không có lỗi nào**).
    /// API miền vẫn là <see cref="Amount"/> (kiểu `Money`) — chỗ dùng không thấy chi tiết lưu trữ.
    /// </summary>
    public decimal AmountValue { get; protected set; }

    /// <summary>Mã ISO của loại tiền (`VND`, `USD`…) — cột **bắt buộc** đi kèm mọi cột tiền (BR-001).</summary>
    public string CurrencyCode { get; protected set; } = "VND";

    /// <summary>Tiền dưới dạng `Money` (computed) — mọi phép so sánh/cộng **phải** đi qua đây, không qua `decimal`.</summary>
    public Money Amount => Money.Cua(AmountValue, Currency.FromCode(CurrencyCode));

    /// <summary>Giá so sánh (giá "gạch") — để hiển thị khuyến mãi. Không dùng để tính tiền.</summary>
    public Money? CompareAtAmount { get; protected set; }

    protected PriceListItem() { }

    public PriceListItem(Guid id, Guid priceListId, string sku, Money amount, Currency currencyCuaBangGia) : base(id)
    {
        if (priceListId == Guid.Empty)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("truong", nameof(priceListId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("truong", nameof(sku));
        }

        // 🚨 `PRC-001` BR-010: **CẤM** lệch loại tiền — **không** tự quy đổi.
        YeuCauCungLoaiTien(amount, currencyCuaBangGia, "PriceListItem.Amount");

        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Giá KHÔNG được âm.")
                .WithData("sku", sku)
                .WithData("amount", amount.Amount);
        }

        // 🚨🚨 **PHẢI GÁN `PriceListId`** — phiên bản đầu CHỈ KIỂM TRA rồi **QUÊN GÁN** ⇒ mọi dòng giá
        // được ghi với `PriceListId = Guid.Empty` (đã bắt thật 15/10/2026: EF gửi
        // `Key (PriceListId)=(00000000-…-000000000000)` ⇒ vi phạm FK `23503`).
        // 📌 Nếu DB **không** có FK thì đây là lỗi **hỏng dữ liệu IM LẶNG**: dòng giá không thuộc bảng giá nào
        // ⇒ tra giá **không bao giờ** thấy nó ⇒ "SKU thiếu giá" mà không ai hiểu vì sao.
        // ⇒ Hàng rào FK ở DB đã biến một lỗi im lặng thành lỗi **ồn ào**. Kiểm tra mà **không gán** thì
        //   kiểm tra **không có giá trị gì**.
        PriceListId = priceListId;

        Sku = sku.Trim();
        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
    }

    public static void YeuCauCungLoaiTien(Money tien, Currency mongDoi, string truong)
    {
        if (tien.Currency != mongDoi)
        {
            throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                .WithData("truong", truong)
                .WithData("cuaBangGia", mongDoi.Code)
                .WithData("cuaDong", tien.Currency.Code)
                .WithData("lyDo", "CẤM tự quy đổi tiền tệ (PRC-001 BR-010).");
        }
    }

    public void DoiGia(Money amount, Currency currencyCuaBangGia)
    {
        YeuCauCungLoaiTien(amount, currencyCuaBangGia, nameof(Amount));

        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("lyDo", "Giá KHÔNG được âm.");
        }

        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
    }
}

/// <summary>
/// **Giá ghi đè theo kênh (hoặc theo shop)** (`PrcChannelPrices`) — `PRC-001` D7 **cấp 1 và cấp 2**.
///
/// 🚨 **ĐÂY LÀ NGUỒN "BÁN SAI GIÁ" NGUY HIỂM NHẤT CỦA PRICING**: `ChannelPrice` **thắng** bảng giá và
/// giá niêm yết. Nếu bản ghi này **lệch loại tiền**, **âm**, hoặc **không kèm ngày hiệu lực** thì kết quả
/// là bán sai — mà luồng vẫn **200 OK**.
/// </summary>
public class ChannelPrice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public string ChannelCode { get; protected set; } = default!;

    /// <summary>`NULL` = áp cho **cả kênh**; có giá trị = áp cho **một shop** (`PRC-001` D7 phân biệt cấp 1/2).</summary>
    public Guid? ChannelShopId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    /// <summary>Bậc số lượng: giá áp cho `quantity ≥ MinQuantity`. `1` = không chia bậc.</summary>
    public int MinQuantity { get; protected set; } = 1;

    /// <summary>Số tiền (`decimal`) — **luôn** đi kèm <see cref="CurrencyCode"/> (`PRC-001` BR-001).</summary>
    public decimal AmountValue { get; protected set; }

    /// <summary>Mã ISO loại tiền — cột **bắt buộc** đi kèm mọi cột tiền.</summary>
    public string CurrencyCode { get; protected set; } = "VND";

    /// <summary>Tiền dạng `Money` (computed) — so sánh/cộng **phải** qua đây.</summary>
    public Money Amount => Money.Cua(AmountValue, Currency.FromCode(CurrencyCode));

    public DateTime EffectiveFrom { get; protected set; }

    public DateTime? EffectiveTo { get; protected set; }

    public int Priority { get; protected set; }

    protected ChannelPrice() { }

    public ChannelPrice(
        Guid id,
        string channelCode,
        string sku,
        Money amount,
        DateTime effectiveFrom,
        Guid? channelShopId = null,
        int minQuantity = 1,
        DateTime? effectiveTo = null,
        int priority = 0) : base(id)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("truong", nameof(channelCode));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("truong", nameof(sku));
        }

        if (minQuantity < 1)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "`MinQuantity` phải ≥ 1.")
                .WithData("minQuantity", minQuantity);
        }

        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Giá KHÔNG được âm.")
                .WithData("sku", sku);
        }

        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "`EffectiveTo` phải SAU `EffectiveFrom`.");
        }

        ChannelCode = channelCode.Trim();
        ChannelShopId = channelShopId;
        Sku = sku.Trim();
        MinQuantity = minQuantity;
        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Priority = priority;
    }

    /// <summary>Giá này áp cho shop cụ thể hay cho **cả kênh** (cấp 2)?</summary>
    public bool LaTheoShop => ChannelShopId is not null;

    public bool CoHieuLucTai(DateTime at)
        => at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);
}
