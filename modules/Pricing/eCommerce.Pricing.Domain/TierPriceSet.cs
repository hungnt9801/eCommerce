using eCommerce.Kernel.Money;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// **Hàm THUẦN chọn bậc thang** (`PRC-002` **ADR-02**) — trái tim của giá bậc thang.
///
/// 🚨 **LUẬT:** bậc thắng = bậc có `MinQuantity` **LỚN NHẤT** mà **≤ `quantity`**.
/// **HAI bậc cùng `MinQuantity` ⇒ NÉM `eCommerce:Pricing:0008`** — **CẤM** "lấy bậc đầu tiên khớp".
///
/// 📌 **VÌ SAO PHẢI NÉM (không chọn bừa):** lấy "bậc đầu tiên khớp theo thứ tự duyệt" làm kết quả
/// **phụ thuộc thứ tự mà DB trả về** ⇒ **cùng một giỏ hàng, hai lần tính, hai giá khác nhau** — mà **không có
/// lỗi nào**. Đúng họ lỗi `XC-003` F1 (`PRC-002` ADR-02 dẫn lại). Tinh thần `XC-001` Q6:
/// *"từ chối rõ ràng tốt hơn hỗ trợ nửa vời"*.
///
/// 📌 **Hàng rào DB:** unique `(TenantId, TierPriceSetId, Sku, MinQuantity)` ⇒ hai bậc trùng `MinQuantity`
/// **không lọt được vào DB**; hàm này là **lớp trong** để lỗi ra dưới **mã nghiệp vụ** thay vì lỗi unique khó hiểu.
/// </summary>
public static class ChonBacThang
{
    /// <summary>Một bậc thang: mua từ <paramref name="MinQuantity"/> trở lên thì đơn giá là <paramref name="DonGia"/>.</summary>
    public sealed record Bac(int MinQuantity, Money DonGia, string Nguon);

    /// <summary>
    /// Chọn bậc cho <paramref name="quantity"/>.
    /// Trả về `null` khi **không** bậc nào đủ điều kiện (⇒ người gọi rơi về giá cơ sở — **không phải lỗi**).
    /// </summary>
    public static Bac? Chon(int quantity, IReadOnlyList<Bac> cacBac, string? sku = null)
    {
        ArgumentNullException.ThrowIfNull(cacBac);

        if (quantity < 1)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "`quantity` phải ≥ 1.")
                .WithData("quantity", quantity);
        }

        var duDieuKien = cacBac.Where(b => b.MinQuantity <= quantity).ToList();

        if (duDieuKien.Count == 0)
        {
            return null;
        }

        var minLonNhat = duDieuKien.Max(b => b.MinQuantity);

        var cungBac = duDieuKien.Where(b => b.MinQuantity == minLonNhat).ToList();

        if (cungBac.Count > 1)
        {
            // 🚨 CÙNG `MinQuantity` ⇒ dữ liệu mâu thuẫn ⇒ NÉM, KHÔNG chọn tuỳ tiện (ADR-02).
            throw new BusinessException(PricingErrorCodes.BacThangTrungMinQuantity)
                .WithData("sku", sku ?? "(không rõ)")
                .WithData("minQuantity", minLonNhat)
                .WithData("soBacTrung", cungBac.Count)
                .WithData("nguon", string.Join(" | ", cungBac.Select(b => b.Nguon)))
                .WithData("lyDo", "Nhiều bậc thang CÙNG `MinQuantity` ⇒ không xác định được đơn giá đúng.");
        }

        return cungBac[0];
    }

    /// <summary>
    /// Kiểm tra **tính hợp lệ của cả bộ bậc** (dùng khi phát hành): không trùng `MinQuantity`, mọi bậc **cùng
    /// loại tiền**, `MinQuantity ≥ 1`, giá **không âm**.
    /// ⚠️ Trả về **danh sách lỗi** (không ném) để tầng gọi gộp thành **một** thông báo cho người nhập liệu.
    /// </summary>
    public static IReadOnlyList<string> KiemTraBoBac(IReadOnlyList<Bac> cacBac, Currency loaiTien)
    {
        ArgumentNullException.ThrowIfNull(cacBac);

        var loi = new List<string>();

        foreach (var b in cacBac)
        {
            if (b.MinQuantity < 1)
            {
                loi.Add($"Bậc có `MinQuantity` = {b.MinQuantity} (< 1).");
            }

            if (b.DonGia.Currency != loaiTien)
            {
                loi.Add($"Bậc `MinQuantity` = {b.MinQuantity} lệch loại tiền " +
                        $"({b.DonGia.Currency.Code} ≠ {loaiTien.Code}) — CẤM tự quy đổi (BR-010).");
            }

            if (b.DonGia.IsNegative)
            {
                loi.Add($"Bậc `MinQuantity` = {b.MinQuantity} có giá ÂM.");
            }
        }

        var trung = cacBac
            .GroupBy(b => b.MinQuantity)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        foreach (var m in trung)
        {
            loi.Add($"Có NHIỀU bậc cùng `MinQuantity` = {m} (PRC-002 ADR-02 ⇒ phải sửa dữ liệu).");
        }

        return loi;
    }
}

/// <summary>
/// **Bộ bậc thang giá** (`PrcTierPriceSets`) — `PRC-002` ADR-01/ADR-10.
///
/// 🚨 **VÌ SAO LÀ BẢNG RIÊNG (không nhét cột vào `PriceListItem`):** `PRC-001` **BR-008** đã chốt
/// `PriceListItem` **duy nhất theo `(TenantId, PriceListId, Sku)`** ⇒ **một SKU chỉ một dòng** ⇒ **không có chỗ**
/// cho nhiều bậc. Nhét mảng bậc vào `ChannelListing` (hiện trạng cũ) cũng sai: nó chỉ có **một** `ChannelPrice`.
///
/// 📌 **Vòng đời tái dùng mô hình đã kiểm chứng** của `TaxClassification` (ADR-10):
/// `Draft → Approved → Active → Expired/Superseded/Cancelled`; **append-only khi `Active`**.
/// **Guard do THỜI GIAN ⇒ trả `false`**, **guard do NGƯỜI ⇒ NÉM** (ADR-11).
/// </summary>
public class TierPriceSet : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    public PriceListScope Scope { get; protected set; }

    public string? ChannelCode { get; protected set; }

    public Currency Currency { get; protected set; } = Currency.Vnd;

    public DateTime EffectiveFrom { get; protected set; }

    public DateTime? EffectiveTo { get; protected set; }

    public int Priority { get; set; }

    public PriceListStatus Status { get; protected set; } = PriceListStatus.Draft;

    protected TierPriceSet() { }

    public TierPriceSet(
        Guid id, string code, string name, PriceListScope scope, Currency currency,
        DateTime effectiveFrom, string? channelCode = null, DateTime? effectiveTo = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("truong", string.IsNullOrWhiteSpace(code) ? nameof(code) : nameof(name));
        }

        // Bất biến KHÔNG được DB bảo vệ: phạm vi theo kênh mà thiếu kênh ⇒ **không bao giờ khớp** ⇒
        // âm thầm rơi về giá cơ sở ⇒ bán sai giá mà không có lỗi nào (cùng bài học `PRC-001` §16.1 #4).
        if (scope != PriceListScope.All && string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Phạm vi theo kênh nhưng THIẾU `ChannelCode` ⇒ bộ bậc thang sẽ không bao giờ khớp.")
                .WithData("scope", scope.ToString());
        }

        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("lyDo", "`EffectiveTo` phải SAU `EffectiveFrom`.");
        }

        Code = code.Trim();
        Name = name.Trim();
        Scope = scope;
        Currency = currency;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        ChannelCode = string.IsNullOrWhiteSpace(channelCode) ? null : channelCode.Trim();
    }

    public bool CoHieuLucTai(DateTime at)
        => at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);

    /// <summary>`Draft → Approved`. **NÉM** khi không ở `Draft` hoặc **chưa có bậc nào**.</summary>
    public void PhatHanh(int soBacThucTe)
    {
        if (Status != PriceListStatus.Draft)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bộ bậc thang ở `Draft` mới phát hành được.")
                .WithData("status", Status.ToString());
        }

        // Số bậc TRUYỀN VÀO (đọc từ DB) — bài học `PRC-001` §17.2 **L1**: guard đọc trạng thái **không được
        // lưu** thì **đảo chiều im lặng** sau một lần nạp lại.
        if (soBacThucTe <= 0)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("lyDo", "Bộ bậc thang CHƯA CÓ BẬC nào ⇒ không phát hành được.")
                .WithData("code", Code);
        }

        Status = PriceListStatus.Approved;
    }

    /// <summary>`Approved → Active` **nếu đã tới ngày**; chưa tới ngày ⇒ **`false`**, **không** ném (ADR-11).</summary>
    public bool KichHoatNeuToiNgay(DateTime at)
    {
        if (Status == PriceListStatus.Active)
        {
            return false;
        }

        if (Status != PriceListStatus.Approved)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bộ bậc thang `Approved` mới kích hoạt được.")
                .WithData("status", Status.ToString());
        }

        if (at < EffectiveFrom)
        {
            return false;
        }

        Status = PriceListStatus.Active;
        return true;
    }

    /// <summary>`Active → Expired` nếu đã quá hạn; chưa quá hạn ⇒ `false` (ADR-11).</summary>
    public bool HetHanNeuQuaNgay(DateTime at)
    {
        if (Status != PriceListStatus.Active)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bộ bậc thang `Active` mới hết hạn được.")
                .WithData("status", Status.ToString());
        }

        if (EffectiveTo is null || at <= EffectiveTo.Value)
        {
            return false;
        }

        Status = PriceListStatus.Expired;
        return true;
    }

    public void BiThayThe()
    {
        if (Status != PriceListStatus.Active)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bộ bậc thang `Active` mới bị thay thế được.")
                .WithData("status", Status.ToString());
        }

        Status = PriceListStatus.Superseded;
    }

    /// <summary>🚨 **Append-only** (`PRC-001` BR-004 áp cho cả bộ bậc): đã/đang hiệu lực ⇒ **cấm sửa nội dung**.</summary>
    public void YeuCauChoPhepSuaNoiDung()
    {
        if (Status is PriceListStatus.Active or PriceListStatus.Expired or PriceListStatus.Superseded)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Bộ bậc thang đã/đang hiệu lực là APPEND-ONLY ⇒ tạo bộ MỚI thay vì sửa.")
                .WithData("status", Status.ToString())
                .WithData("code", Code);
        }
    }
}

/// <summary>
/// **Một bậc thang** (`PrcTierPrices`) — từ `MinQuantity` trở lên thì đơn giá là `Amount`.
/// ⚠️ Duy nhất theo `(TenantId, TierPriceSetId, Sku, MinQuantity)` — **hàng rào DB** cho ADR-02.
/// </summary>
public class TierPrice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid TierPriceSetId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public int MinQuantity { get; protected set; }

    public decimal AmountValue { get; protected set; }

    /// <summary>Mã ISO loại tiền — cột **bắt buộc** đi kèm mọi cột tiền (`PRC-001` BR-001).</summary>
    public string CurrencyCode { get; protected set; } = "VND";

    public Money Amount => Money.Cua(AmountValue, Currency.FromCode(CurrencyCode));

    protected TierPrice() { }

    public TierPrice(Guid id, Guid tierPriceSetId, string sku, int minQuantity, Money amount, Currency currencyCuaBo) : base(id)
    {
        if (tierPriceSetId == Guid.Empty)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong).WithData("truong", nameof(tierPriceSetId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong).WithData("truong", nameof(sku));
        }

        if (minQuantity < 1)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "`MinQuantity` phải ≥ 1.")
                .WithData("minQuantity", minQuantity);
        }

        PriceListItem.YeuCauCungLoaiTien(amount, currencyCuaBo, "TierPrice.Amount");

        if (amount.IsNegative)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia).WithData("lyDo", "Giá KHÔNG được âm.");
        }

        // 🚨 **PHẢI GÁN** — bài học `PRC-001` §17.2 **L2** (constructor chỉ KIỂM TRA mà quên GÁN ⇒ FK nổ).
        TierPriceSetId = tierPriceSetId;
        Sku = sku.Trim();
        MinQuantity = minQuantity;
        AmountValue = amount.Amount;
        CurrencyCode = amount.Currency.Code;
    }
}
