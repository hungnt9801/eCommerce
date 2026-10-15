using eCommerce.Kernel.Money;
using Volo.Abp;

namespace eCommerce.Pricing;

/// <summary>Cấp thắng khi giải giá cơ sở (`PRC-001` **D7**) — trả ra để **giải trình được** "vì sao giá này".</summary>
public enum CapGia
{
    /// <summary>Cấp 1: `ChannelPrice` khớp **cả** `ChannelCode` **và** `ChannelShopId`.</summary>
    GiaTheoShop = 1,

    /// <summary>Cấp 2: `ChannelPrice` khớp **chỉ** `ChannelCode`.</summary>
    GiaTheoKenh = 2,

    /// <summary>Cấp 3: `PriceListItem` của bảng giá **đang hiệu lực**.</summary>
    BangGia = 3,

    /// <summary>Cấp 4: **giá niêm yết** truyền vào (từ Catalog).</summary>
    GiaNiemYet = 4
}

/// <summary>Một **ứng viên giá** đã được nạp sẵn — đầu vào của hàm thuần.</summary>
public sealed record UngVienGia(CapGia Cap, Money Amount, int Priority, string Nguon);

/// <summary>Kết quả giải giá: giá thắng + cấp + **toàn bộ ứng viên** (ADR-03: phải kiểm chứng được).</summary>
public sealed record KetQuaGia(Money Amount, CapGia Cap, string Nguon, IReadOnlyList<UngVienGia> TatCaUngVien);

/// <summary>
/// **Chính sách khi KHÔNG tìm thấy giá** (`PRC-001` **D14**/E3).
///
/// 🚨 **Mặc định là `NemLoi`** — *"bán với giá đoán mò là lỗi **không sửa được về sau**"*.
/// Nếu mặc định là "dùng giá niêm yết" thì hệ thống **tự bán** ở một mức giá chưa ai duyệt, và **không ai biết**
/// cho tới lúc đối soát doanh thu.
/// </summary>
public enum ChinhSachThieuGia
{
    /// <summary>Ném `eCommerce:Pricing:0004` kèm **SKU + kênh + ngày** để vận hành bổ sung ngay (mặc định).</summary>
    NemLoi = 0,

    /// <summary>Rơi xuống **giá niêm yết** (dùng có ý thức, có cấu hình).</summary>
    DungGiaNiemYet = 1
}

/// <summary>
/// **HÀM THUẦN giải giá cơ sở theo 4 CẤP** (`PRC-001` **D7**/BR-006/BR-009) — `PRC-002` **ADR-04**
/// (pipeline thuần, service mới nạp DB). Tiền lệ trong repo: `TaxRateResolver` thuần + `…ResolverService` nạp.
///
/// 🚨 **BA LUẬT KHÔNG ĐƯỢC VI PHẠM:**
///
/// 1. **THỨ TỰ CẤP LÀ CỐ ĐỊNH** (D7): `Shop` → `Kênh` → `Bảng giá` → `Niêm yết`. **CẤM** "cái nào **mới nhất**
///    thắng": khi hai bản ghi cùng ngày thì kết quả **không xác định** ⇒ cùng một giỏ hàng, hai lần tính,
///    **hai giá khác nhau** — mà **không có lỗi nào**. Trong cùng một cấp mới so `Priority`.
///
/// 2. **CÙNG CẤP + CÙNG `Priority` ⇒ NÉM LỖI** (`eCommerce:Pricing:0001`, `PRC-001` **A2**): *"cấm chọn tuỳ tiện"*.
///    Chọn "cái đầu tiên gặp" làm kết quả **phụ thuộc thứ tự duyệt** ⇒ **không test được** (đúng họ lỗi `XC-003` F1).
///
/// 3. **SO SÁNH TIỀN QUA `Money`, KHÔNG QUA `decimal`** (BR-002) — và **CẤM** trộn loại tiền (BR-010):
///    hàm này **kiểm tra tường minh** loại tiền của mọi ứng viên trước khi so, để lỗi ra dưới **mã của Pricing**
///    (`0005`) thay vì mã của Kernel.
/// </summary>
public static class GiaCoSoResolver
{
    /// <summary>
    /// Giải giá cơ sở. ⚠️ <paramref name="tatCaUngVien"/> **phải** đã được lọc theo `(sku, kênh, ngày hiệu lực, quantity)`
    /// ở tầng service (nạp DB) — hàm này **không** biết DB và **không** đọc đồng hồ hệ thống (`at` truyền vào).
    /// </summary>
    public static KetQuaGia Giai(
        IReadOnlyList<UngVienGia> tatCaUngVien,
        Currency loaiTien,
        ChinhSachThieuGia chinhSach = ChinhSachThieuGia.NemLoi,
        string? sku = null,
        string? channelCode = null,
        DateTime? at = null)
    {
        ArgumentNullException.ThrowIfNull(tatCaUngVien);

        // 🚨 Kiểm tra loại tiền TRƯỚC khi so sánh — để lỗi mang MÃ CỦA PRICING (`0005`).
        foreach (var uv in tatCaUngVien)
        {
            if (uv.Amount.Currency != loaiTien)
            {
                throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                    .WithData("nguon", uv.Nguon)
                    .WithData("mongDoi", loaiTien.Code)
                    .WithData("thucTe", uv.Amount.Currency.Code)
                    .WithData("lyDo", "CẤM tự quy đổi tiền tệ (PRC-001 BR-010).");
            }
        }

        // Xét theo CẤP tăng dần: cấp thấp hơn (1) THẮNG. Trong cùng cấp ⇒ `Priority` LỚN HƠN thắng.
        foreach (var cap in new[] { CapGia.GiaTheoShop, CapGia.GiaTheoKenh, CapGia.BangGia, CapGia.GiaNiemYet })
        {
            var cungCap = tatCaUngVien.Where(x => x.Cap == cap).ToList();

            if (cungCap.Count == 0)
            {
                continue;
            }

            var uuTienCaoNhat = cungCap.Max(x => x.Priority);

            var thang = cungCap.Where(x => x.Priority == uuTienCaoNhat).ToList();

            if (thang.Count > 1)
            {
                // 🚨 CÙNG cấp + CÙNG `Priority` ⇒ **dữ liệu mâu thuẫn** ⇒ NÉM, không chọn tuỳ tiện (A2).
                throw new BusinessException(PricingErrorCodes.UngVienCungCapXungDot)
                    .WithData("cap", cap.ToString())
                    .WithData("priority", uuTienCaoNhat)
                    .WithData("sku", sku ?? "(không rõ)")
                    .WithData("channelCode", channelCode ?? "(không rõ)")
                    .WithData("nguon", string.Join(" | ", thang.Select(x => x.Nguon)))
                    .WithData("lyDo", "Nhiều ứng viên CÙNG CẤP và CÙNG Priority ⇒ không xác định được giá đúng.");
            }

            var ket = thang[0];

            return new KetQuaGia(ket.Amount, ket.Cap, ket.Nguon, tatCaUngVien);
        }

        // Không có ứng viên nào.
        if (chinhSach == ChinhSachThieuGia.NemLoi)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("sku", sku ?? "(không rõ)")
                .WithData("channelCode", channelCode ?? "(không rõ)")
                .WithData("at", at ?? default)
                .WithData("lyDo", "Không có giá nào khớp ⇒ theo chính sách mặc định là NÉM LỖI " +
                                  "(bán với giá đoán mò là lỗi không sửa được về sau).");
        }

        // `DungGiaNiemYet` nhưng **cũng không có** giá niêm yết ⇒ vẫn không có gì để bán ⇒ NÉM.
        throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
            .WithData("sku", sku ?? "(không rõ)")
            .WithData("channelCode", channelCode ?? "(không rõ)")
            .WithData("lyDo", "Chính sách cho phép dùng giá niêm yết nhưng KHÔNG có giá niêm yết nào.");
    }

    /// <summary>
    /// Lọc **theo cấp số lượng** (`PRC-001` **A3**): chọn `MinQuantity` **lớn nhất** ≤ `quantity`.
    ///
    /// 🚨 **HAI BẬC CÙNG `MinQuantity` ⇒ NÉM** (`eCommerce:Pricing:0008`, `PRC-002` **ADR-02**):
    /// nếu lấy "bậc đầu tiên khớp" thì kết quả **phụ thuộc thứ tự duyệt** ⇒ cùng giỏ hàng, hai lần tính,
    /// **hai giá** — không test được.
    /// </summary>
    public static IReadOnlyList<UngVienGia> LocTheoBacSoLuong(
        IReadOnlyList<(int MinQuantity, UngVienGia UngVien)> theoBac,
        int quantity,
        string? sku = null)
    {
        ArgumentNullException.ThrowIfNull(theoBac);

        var duDieuKien = theoBac.Where(x => x.MinQuantity <= quantity).ToList();

        if (duDieuKien.Count == 0)
        {
            return Array.Empty<UngVienGia>();
        }

        var minLonNhat = duDieuKien.Max(x => x.MinQuantity);

        var cungBac = duDieuKien.Where(x => x.MinQuantity == minLonNhat).ToList();

        // Trùng `MinQuantity` ở **cùng cấp** ⇒ mâu thuẫn dữ liệu. Ở đây chỉ kiểm khi cùng `Cap` + `Priority`,
        // vì khác cấp thì thứ tự cấp đã quyết định (hàm `Giai` xử lý).
        var nhomXungDot = cungBac
            .GroupBy(x => (x.UngVien.Cap, x.UngVien.Priority))
            .Where(g => g.Count() > 1)
            .ToList();

        if (nhomXungDot.Count > 0)
        {
            var g = nhomXungDot[0];

            throw new BusinessException(PricingErrorCodes.BacThangTrungMinQuantity)
                .WithData("sku", sku ?? "(không rõ)")
                .WithData("minQuantity", minLonNhat)
                .WithData("cap", g.Key.Cap.ToString())
                .WithData("priority", g.Key.Priority)
                .WithData("nguon", string.Join(" | ", g.Select(x => x.UngVien.Nguon)))
                .WithData("lyDo", "Hai bậc thang CÙNG MinQuantity trên cùng SKU/cấp/priority ⇒ không xác định được giá.");
        }

        return cungBac.Select(x => x.UngVien).ToList();
    }
}
