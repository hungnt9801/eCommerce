using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`S12` — GIÁ VỐN** (`PRC-001` §5.2, `PrcCostPrices`): bất biến của entity + máy duyệt.
///
/// 🚨 **VÌ SAO BỘ TEST NÀY QUAN TRỌNG (dù `S12` chưa mở đường ghi):** dòng giá vốn **sai** không làm hỏng
/// màn hình nào — nó chỉ làm **lãi gộp sai**. Ba lỗi dưới đây **không có lỗi biên dịch**, **không exception**
/// ở đường đọc, và chỉ lộ ra khi kế toán đối chiếu tay:
///   1. giá vốn **âm** ⇒ lãi gộp **cao hơn** sự thật (báo cáo đẹp, quyết định sai);
///   2. **quên `CurrencyCode`** (tự gán `VND`) ⇒ một dòng **USD** bị đọc thành **VND** — sai 25.000 lần;
///   3. **kỳ hiệu lực ngược** ⇒ dòng giá vốn **không bao giờ** được chọn ⇒ SKU đó **không có giá vốn** mà
///      không ai biết (báo cáo thiếu dòng, không phải báo lỗi).
///
/// 📌 Test ở đây chạy **không cần PostgreSQL** — đó là chủ ý: luật nằm trong Domain, DB chỉ là hàng rào
/// thứ hai (xem `PostgreSqlSchemaTests` cho các `CHECK`/unique index thật).
/// </summary>
public class CostPriceTests
{
    private static readonly DateOnly Tu = new(2026, 1, 1);

    private static CostPrice Tao(
        string sku = "SKU-1",
        decimal tien = 100_000m,
        Currency? tienTe = null,
        DateOnly? tu = null,
        DateOnly? den = null)
        => new(
            Guid.NewGuid(),
            sku,
            Money.Cua(tien, tienTe ?? Currency.Vnd),
            tu ?? Tu,
            den);

    // ── 1. Dựng hợp lệ ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tao_hop_le__luu_dung_truong_va_chua_duyet()
    {
        var g = Tao(den: new DateOnly(2026, 3, 31));

        Assert.Equal("SKU-1", g.Sku);
        Assert.Equal(100_000m, g.AmountValue);
        Assert.Equal("VND", g.CurrencyCode);
        Assert.Equal(Tu, g.EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 3, 31), g.EffectiveTo);
        Assert.False(g.DaDuyet);

        // 📌 `Amount` là `Money` — kiểm cả hai vế (số tiền + loại tiền), đúng `BR-002` (cấm so `decimal` trần).
        Assert.Equal(Money.Vnd(100_000), g.Amount);
    }

    [Fact]
    public void CurrencyCode_lay_TU_Money__khong_tu_gan_VND()
    {
        // 🚨 Đây là ca "sai 25.000 lần mà không có lỗi": nếu entity tự gán `VND` cho mọi dòng thì một dòng
        // giá vốn **USD** sẽ được đọc thành **VND** — lãi gộp của SKU đó **sai toàn bộ**.
        var g = Tao(tien: 4.5m, tienTe: Currency.Usd);

        Assert.Equal("USD", g.CurrencyCode);
        Assert.Equal(Money.Usd(4.5m), g.Amount);
        Assert.NotEqual(Money.Vnd(4.5m), g.Amount);
    }

    // ── 2. Hiệu lực theo NGÀY (BR-003/D4) ─────────────────────────────────────────────────────

    [Fact]
    public void CoHieuLucTai__dung_ca_HAI_bien_va_ben_ngoai()
    {
        var g = Tao(tu: new DateOnly(2026, 2, 1), den: new DateOnly(2026, 2, 28));

        Assert.False(g.CoHieuLucTai(new DateOnly(2026, 1, 31)));   // trước
        Assert.True(g.CoHieuLucTai(new DateOnly(2026, 2, 1)));     // ĐÚNG biên đầu (bao gồm)
        Assert.True(g.CoHieuLucTai(new DateOnly(2026, 2, 28)));    // ĐÚNG biên cuối (bao gồm)
        Assert.False(g.CoHieuLucTai(new DateOnly(2026, 3, 1)));    // sau
    }

    [Fact]
    public void EffectiveTo_NULL__la_vo_thoi_han()
    {
        var g = Tao();

        Assert.Null(g.EffectiveTo);
        Assert.True(g.CoHieuLucTai(new DateOnly(2030, 12, 31)));
    }

    // ── 3. Guard: dữ liệu giá vốn sai ⇒ NÉM (D12) ─────────────────────────────────────────────

    [Fact]
    public void Gia_von_AM__NEM_0009()
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(tien: -1m));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
        Assert.Equal(-1m, loi.Data["amount"]);
    }

    [Fact]
    public void Gia_von_BANG_0__la_HOP_LE()
    {
        // 📌 Cùng quyết định với `CHECK (Amount >= 0)` của `PRC-001` §5.2: hàng tặng/hàng mẫu có giá vốn 0.
        // ⚠️ Nếu ai đổi guard thành `> 0` thì test này ĐỎ — và đó là điều mong muốn: `0` là hợp lệ.
        var g = Tao(tien: 0m);

        Assert.Equal(0m, g.AmountValue);
        Assert.True(g.Amount.IsZero);
    }

    [Fact]
    public void EffectiveTo_BANG_EffectiveFrom__NEM_0009()
    {
        // Kỳ "từ 1/1 đến 1/1" là **không có kỳ nào** — nhận nó nghĩa là dòng giá vốn **không bao giờ** hiệu lực.
        var loi = Assert.Throws<BusinessException>(
            () => Tao(tu: new DateOnly(2026, 1, 1), den: new DateOnly(2026, 1, 1)));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
    }

    [Fact]
    public void EffectiveTo_TRUOC_EffectiveFrom__NEM_0009()
    {
        var loi = Assert.Throws<BusinessException>(
            () => Tao(tu: new DateOnly(2026, 5, 1), den: new DateOnly(2026, 4, 30)));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sku_rong__NEM_0009(string sku)
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(sku: sku));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
        Assert.Equal("sku", loi.Data["truong"]);
    }

    [Fact]
    public void Sku_duoc_TRIM__khong_luu_khoang_trang()
    {
        var g = Tao(sku: "  SKU-X  ");

        Assert.Equal("SKU-X", g.Sku);
    }

    // ── 4. Duyệt giá vốn (kế toán) ────────────────────────────────────────────────────────────

    [Fact]
    public void Duyet__ghi_nguoi_duyet_va_thoi_diem()
    {
        var g = Tao();
        var nguoiDuyet = Guid.NewGuid();
        var luc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        g.Duyet(nguoiDuyet, luc);

        Assert.True(g.DaDuyet);
        Assert.Equal(nguoiDuyet, g.ApprovedBy);
        Assert.Equal(luc, g.ApprovedAtUtc);
    }

    [Fact]
    public void Duyet_HAI_LAN__NEM__khong_no_op_im_lang()
    {
        // 🚨 `D12`: duyệt lại **không** được im lặng — lần thứ hai ghi đè người duyệt đầu tiên và **xoá dấu
        // vết ai chịu trách nhiệm** về con số giá vốn đó.
        var g = Tao();
        g.Duyet(Guid.NewGuid(), DateTime.UtcNow);

        var loi = Assert.Throws<BusinessException>(() => g.Duyet(Guid.NewGuid(), DateTime.UtcNow));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
        Assert.True(g.DaDuyet);
    }

    [Fact]
    public void Duyet_thieu_nguoi_duyet__NEM_0009()
    {
        var loi = Assert.Throws<BusinessException>(() => Tao().Duyet(Guid.Empty, DateTime.UtcNow));

        Assert.Equal(PricingErrorCodes.GiaVonKhongHopLe, loi.Code);
    }

    // ── 5. Neo bảo vệ BR-013 (dữ liệu nội bộ) ────────────────────────────────────────────────

    [Fact]
    public void Ma_loi_gia_von_KHONG_trung_ma_da_dung()
    {
        // 🔒 Sổ mã lỗi của Pricing là **nguồn sự thật**; `0009` mới ⇒ phải KHÔNG trùng bất kỳ mã nào khác
        // (nếu trùng, client sẽ nhận một mã và **hiểu sai nghĩa** — đúng lỗi "hai nguồn sự thật").
        var cacMa = typeof(PricingErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Contains(PricingErrorCodes.GiaVonKhongHopLe, cacMa);
        Assert.Equal(cacMa.Count, cacMa.Distinct(StringComparer.Ordinal).Count());
        Assert.StartsWith("eCommerce:Pricing:0009", PricingErrorCodes.GiaVonKhongHopLe, StringComparison.Ordinal);
    }
}
