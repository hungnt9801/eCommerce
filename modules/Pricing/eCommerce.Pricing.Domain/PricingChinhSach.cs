using eCommerce.Kernel.Money;
using eCommerce.Settings;

namespace eCommerce.Pricing;

/// <summary>
/// **TÊN KHOÁ SETTING CỦA MODULE PRICING** (`PRC-002` / `D-218`).
///
/// 📌 Đặt ở **module** (không nhét vào `TenChinhSach` của Kernel): hàm đọc giá trị phải trả kiểu **của
/// Pricing** (`ChinhSachThieuGia`, `ChinhSachTongDonAm`, `IRoundingPolicy`) ⇒ đặt ở Kernel sẽ **đảo chiều
/// phụ thuộc** (`-arch` §2.2). Bốn khoá dưới đây do BA giữ đồng bộ với SRS §9.
/// </summary>
public static class PRC002SettingTenKhoa
{
    public const string ChinhSachLamTron = "Pricing:RoundingPolicyCode";
    public const string ThieuGia = "Pricing:MissingPricePolicy";
    public const string SoBacToiDa = "Pricing:MaxTiersPerSet";
    public const string TongDonAm = "Pricing:OrderTotalNegativePolicy";
}

/// <summary>
/// **Chính sách khi tổng đơn âm** (`PRC-002` `D-219`). 🚨 Enum lưu DB (nếu có) chỉ được **thêm ở CUỐI dải**.
/// </summary>
public enum ChinhSachTongDonAm
{
    /// <summary>Mặc định: **NÉM** `eCommerce:Pricing:0010` khi tổng `&lt; 0` (fail-closed).</summary>
    Reject = 0,

    /// <summary>Kẹp giảm giá cấp đơn về **không vượt thành tiền** ⇒ tổng `≥ 0`; kết quả mang `BiKep/SoKep`.</summary>
    Clamp = 1
}

/// <summary>
/// 🚨 **ĐỌC CHÍNH SÁCH — HÀM THUẦN** (không I/O, không ném) ⇒ unit test được không cần DB.
///
/// 🔒 **Nguyên tắc "dữ liệu cấu hình cũng là dữ liệu vào"** (tiền lệ `eCommerce.Settings.ChinhSach`):
/// giá trị lạ/rỗng/âm ⇒ trả **mặc định an toàn** + `hopLe = false` để chỗ gọi **ghi cảnh báo**; **không** ném.
/// 📌 Bốn hàm dưới đây **không** nhận/không lộ `ISettingProvider`/`ISettingManager` — chính sách chỉ đi vào
/// qua **tham số** (`-arch` §3.1, test kiến trúc 3d).
/// </summary>
public static class PricingChinhSach
{
    /// <summary>Số bậc tối đa mặc định mỗi bộ bậc thang (`D-224`).</summary>
    public const int SoBacToiDaMacDinh = 20;

    /// <summary>Allowlist mã chính sách làm tròn (`D-221`/AQ1: **chỉ** `half-away-from-zero`).</summary>
    public static readonly string[] ChinhSachLamTronHopLe = { "half-away-from-zero" };

    /// <summary>Allowlist chính sách thiếu giá.</summary>
    public static readonly string[] ThieuGiaHopLe =
        { nameof(ChinhSachThieuGia.NemLoi), nameof(ChinhSachThieuGia.DungGiaNiemYet) };

    /// <summary>Allowlist chính sách tổng đơn âm.</summary>
    public static readonly string[] TongDonAmHopLe =
        { nameof(ChinhSachTongDonAm.Reject), nameof(ChinhSachTongDonAm.Clamp) };

    /// <summary>
    /// Đọc `Pricing:RoundingPolicyCode`. `"half-away-from-zero"` ⇒ `RoundHalfAwayFromZeroPolicy` + `hopLe = true`;
    /// rỗng/lạ/`"none"` ⇒ **mặc định** `half-away-from-zero` + `hopLe = false` (`D-221`).
    /// </summary>
    public static IRoundingPolicy DocChinhSachLamTron(string? tho, out bool hopLe)
    {
        var giaTri = (tho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "half-away-from-zero", StringComparison.OrdinalIgnoreCase))
        {
            hopLe = true;
            return new RoundHalfAwayFromZeroPolicy();
        }

        hopLe = false;
        return new RoundHalfAwayFromZeroPolicy();
    }

    /// <summary>
    /// Đọc `Pricing:MissingPricePolicy` (`NemLoi|DungGiaNiemYet`). Rỗng/lạ ⇒ **mặc định** `NemLoi` + `hopLe = false`.
    /// </summary>
    public static ChinhSachThieuGia DocThieuGia(string? tho, out bool hopLe)
    {
        var giaTri = (tho ?? string.Empty).Trim();

        if (string.Equals(giaTri, nameof(ChinhSachThieuGia.DungGiaNiemYet), StringComparison.OrdinalIgnoreCase))
        {
            hopLe = true;
            return ChinhSachThieuGia.DungGiaNiemYet;
        }

        if (string.Equals(giaTri, nameof(ChinhSachThieuGia.NemLoi), StringComparison.OrdinalIgnoreCase))
        {
            hopLe = true;
            return ChinhSachThieuGia.NemLoi;
        }

        hopLe = false;
        return ChinhSachThieuGia.NemLoi;
    }

    /// <summary>
    /// Đọc `Pricing:MaxTiersPerSet`. Dùng `ChinhSach.DocSo` của Kernel (**chỉ dấu CHẤM**, không dấu phẩy).
    /// `0`/âm/không phải số ⇒ **mặc định 20** + `hopLe = false` (`D-224`/AQ4: không có công tắc tắt).
    /// </summary>
    public static int DocSoBacToiDa(string? tho, out bool hopLe)
    {
        var so = ChinhSach.DocSo(tho, SoBacToiDaMacDinh, out hopLe);

        // `DocSo` đã loại số âm/không phải số; riêng `0` vẫn "hợp lệ" ở đó nhưng vô nghĩa với ngưỡng số bậc.
        if (!hopLe || so < 1m)
        {
            hopLe = false;
            return SoBacToiDaMacDinh;
        }

        return (int)Math.Floor(so);
    }

    /// <summary>
    /// Đọc `Pricing:OrderTotalNegativePolicy` (`Reject|Clamp`). Rỗng/lạ ⇒ **mặc định** `Reject` + `hopLe = false`.
    /// </summary>
    public static ChinhSachTongDonAm DocTongDonAm(string? tho, out bool hopLe)
    {
        var giaTri = (tho ?? string.Empty).Trim();

        if (string.Equals(giaTri, nameof(ChinhSachTongDonAm.Clamp), StringComparison.OrdinalIgnoreCase))
        {
            hopLe = true;
            return ChinhSachTongDonAm.Clamp;
        }

        if (string.Equals(giaTri, nameof(ChinhSachTongDonAm.Reject), StringComparison.OrdinalIgnoreCase))
        {
            hopLe = true;
            return ChinhSachTongDonAm.Reject;
        }

        hopLe = false;
        return ChinhSachTongDonAm.Reject;
    }
}
