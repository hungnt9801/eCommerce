using eCommerce.Kernel.Money;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test **hàm đọc chính sách THUẦN** của Pricing (`PRC-002` `-arch` §6.3 ca 1a/1b).
///
/// 🔒 Luật cần khoá: *"dữ liệu cấu hình cũng là dữ liệu vào"* — giá trị **rỗng/lạ/không phải số**
/// KHÔNG được làm chết đường tính giá; hàm đọc trả **mặc định an toàn** + `hopLe = false` (để chỗ gọi
/// ghi cảnh báo). Đồng thời giá trị **hợp lệ** phải được nhận đúng + `hopLe = true`.
///
/// 📌 **Vì sao test riêng, không cần DB:** đây là hàm thuần không I/O ⇒ kiểm được ở tầng đơn vị; nếu
/// phải dựng Host/DB mới kiểm được thì đúng loại luật "không được kiểm thường xuyên".
/// </summary>
public class PricingChinhSachTests
{
    // ── `Pricing:RoundingPolicyCode` (1a/1b) ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rounding__rong__mac_dinh_half_away_from_zero_NHUNG_bao_KHONG_hop_le(string? tho)
    {
        // 📌 Rỗng chỉ xảy ra khi ĐỊNH NGHĨA setting biến mất (provider luôn trả DefaultValue khi chưa đặt)
        // ⇒ là ca cấu hình CÓ VẤN ĐỀ ⇒ dùng mặc định nhưng phải báo `hopLe = false`.
        var policy = PricingChinhSach.DocChinhSachLamTron(tho, out var hopLe);

        Assert.Equal("half-away-from-zero", policy.Ten);
        Assert.IsType<RoundHalfAwayFromZeroPolicy>(policy);
        Assert.False(hopLe);
    }

    [Fact]
    public void Rounding__half_away_from_zero__hop_le_va_dung_policy_1b()
    {
        var policy = PricingChinhSach.DocChinhSachLamTron("half-away-from-zero", out var hopLe);

        Assert.IsType<RoundHalfAwayFromZeroPolicy>(policy);
        Assert.Equal("half-away-from-zero", policy.Ten);
        Assert.True(hopLe);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("none")]   // AQ1: allowlist CHỈ có `half-away-from-zero` ⇒ `none` KHÔNG hợp lệ
    public void Rounding__gia_tri_la__mac_dinh_va_KHONG_hop_le_1b(string tho)
    {
        var policy = PricingChinhSach.DocChinhSachLamTron(tho, out var hopLe);

        Assert.Equal("half-away-from-zero", policy.Ten);
        Assert.False(hopLe);
    }

    // ── `Pricing:MissingPricePolicy` ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void ThieuGia__rong_hoac_la__mac_dinh_NemLoi_va_KHONG_hop_le(string? tho)
    {
        var chinhSach = PricingChinhSach.DocThieuGia(tho, out var hopLe);

        Assert.Equal(ChinhSachThieuGia.NemLoi, chinhSach);
        Assert.False(hopLe);
    }

    [Theory]
    [InlineData("NemLoi", ChinhSachThieuGia.NemLoi)]
    [InlineData("DungGiaNiemYet", ChinhSachThieuGia.DungGiaNiemYet)]
    public void ThieuGia__gia_tri_hop_le__hopLe_true(string tho, ChinhSachThieuGia mongDoi)
    {
        var chinhSach = PricingChinhSach.DocThieuGia(tho, out var hopLe);

        Assert.Equal(mongDoi, chinhSach);
        Assert.True(hopLe);
    }

    // ── `Pricing:MaxTiersPerSet` (`D-224`/AQ4: 0/âm/không phải số ⇒ mặc định 20) ───────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]      // 0 KHÔNG phải "tắt" — không có công tắc tắt (AQ4)
    [InlineData("-5")]
    [InlineData("abc")]
    public void SoBacToiDa__rong_0_am_khong_phai_so__mac_dinh_20_va_KHONG_hop_le(string? tho)
    {
        var so = PricingChinhSach.DocSoBacToiDa(tho, out var hopLe);

        Assert.Equal(PricingChinhSach.SoBacToiDaMacDinh, so);
        Assert.Equal(20, so);
        Assert.False(hopLe);
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("20", 20)]
    [InlineData("100", 100)]
    public void SoBacToiDa__gia_tri_hop_le__doc_dung_va_hopLe_true(string tho, int mongDoi)
    {
        var so = PricingChinhSach.DocSoBacToiDa(tho, out var hopLe);

        Assert.Equal(mongDoi, so);
        Assert.True(hopLe);
    }

    // ── `Pricing:OrderTotalNegativePolicy` (`D-219`) ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void TongDonAm__rong_hoac_la__mac_dinh_Reject_va_KHONG_hop_le(string? tho)
    {
        // 🚨 Mặc định PHẢI là `Reject` (fail-closed) — "âm im lặng" là lỗi tiền không sửa được về sau.
        var chinhSach = PricingChinhSach.DocTongDonAm(tho, out var hopLe);

        Assert.Equal(ChinhSachTongDonAm.Reject, chinhSach);
        Assert.False(hopLe);
    }

    [Theory]
    [InlineData("Reject", ChinhSachTongDonAm.Reject)]
    [InlineData("Clamp", ChinhSachTongDonAm.Clamp)]
    public void TongDonAm__gia_tri_hop_le__hopLe_true(string tho, ChinhSachTongDonAm mongDoi)
    {
        var chinhSach = PricingChinhSach.DocTongDonAm(tho, out var hopLe);

        Assert.Equal(mongDoi, chinhSach);
        Assert.True(hopLe);
    }

    // ── Allowlist ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Allowlist_lam_tron_CHI_co_half_away_from_zero__AQ1()
    {
        Assert.Equal(["half-away-from-zero"], PricingChinhSach.ChinhSachLamTronHopLe);
    }
}
