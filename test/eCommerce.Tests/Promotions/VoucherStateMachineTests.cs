using Volo.Abp;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **MÁY TRẠNG THÁI NÉM** (`PRO-001-arch` §3.2/§3.3 · BR-10) — mọi chuyển sai **NÉM**, không no-op im lặng:
/// mã `VoucherCode` (`Generated→Issued→Revoked`) ⇒ `0006`; lượt `PromotionRedemption`
/// (`Reserved→Applied`, `→Released`, `→Reversed`) ⇒ `0105`.
/// Kèm `DoiGioiHan` (ngoại lệ `BR-18`) ⇒ `0116` khi `≤ 0`.
/// </summary>
public class VoucherStateMachineTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    private static VoucherCode Ma(VoucherCodeStatus status = VoucherCodeStatus.Generated, int maxUsage = 5)
        => VoucherCode.TaoMoi(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ABC", maxUsage, status);

    private static PromotionRedemption Luot(
        PromotionRedemptionStatus status = PromotionRedemptionStatus.Reserved,
        DateTime? expiresAtUtc = null)
        => PromotionRedemption.TaoMoi(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            status, expiresAtUtc ?? T0.AddMinutes(15), 1, Guid.NewGuid(),
            "khoa", 10_000m, "VND", "{}", 1, T0);

    private static void Nem(Action chuyen, string ma)
    {
        var ex = Assert.Throws<BusinessException>(chuyen);
        Assert.Equal(ma, ex.Code);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  VoucherCode — Generated → Issued → Revoked
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Ma_Generated__Issue__thanh_Issued()
    {
        var ma = Ma(VoucherCodeStatus.Generated);
        ma.Issue(T0);
        Assert.Equal(VoucherCodeStatus.Issued, ma.Status);
    }

    [Fact]
    public void Ma_da_Issued__Issue_lai__nem_0006()
    {
        var ma = Ma(VoucherCodeStatus.Issued);
        Nem(() => ma.Issue(T0), PromotionErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void Ma_Generated__Revoke__nem_0006_khong_nhay_cap()
    {
        var ma = Ma(VoucherCodeStatus.Generated);
        Nem(() => ma.Revoke(T0), PromotionErrorCodes.InvalidStatusTransition);
        Assert.Equal(VoucherCodeStatus.Generated, ma.Status);
    }

    [Fact]
    public void Ma_Issued__Revoke__thanh_Revoked()
    {
        var ma = Ma(VoucherCodeStatus.Issued);
        ma.Revoke(T0);
        Assert.Equal(VoucherCodeStatus.Revoked, ma.Status);
    }

    [Fact]
    public void Ma_da_Revoked__Revoke_lai__nem_0006()
    {
        var ma = Ma(VoucherCodeStatus.Revoked);
        Nem(() => ma.Revoke(T0), PromotionErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void Ma_da_Revoked__Issue__nem_0006()
    {
        var ma = Ma(VoucherCodeStatus.Revoked);
        Nem(() => ma.Issue(T0), PromotionErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void DoiGioiHan_nho_hon_1__nem_0116()
    {
        var ma = Ma();
        Nem(() => ma.DoiGioiHan(0), PromotionErrorCodes.OverrideLimitTooLow);
        Nem(() => ma.DoiGioiHan(-5), PromotionErrorCodes.OverrideLimitTooLow);
    }

    [Fact]
    public void DoiGioiHan_hop_le__doi_MaxUsage()
    {
        var ma = Ma(maxUsage: 5);
        ma.DoiGioiHan(10);
        Assert.Equal(10, ma.MaxUsage);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  PromotionRedemption — Reserved → Applied / Released / Reversed
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Luot_Reserved_con_han__ChotApplied__thanh_Applied()
    {
        var luot = Luot();
        var orderId = Guid.NewGuid();

        luot.ChotApplied(orderId, T0);

        Assert.Equal(PromotionRedemptionStatus.Applied, luot.Status);
        Assert.Equal(orderId, luot.OrderId);
        Assert.Equal(T0, luot.AppliedAtUtc);
    }

    [Fact]
    public void Luot_het_TTL__ChotApplied__nem_0105_giu_Reserved()
    {
        var luot = Luot(expiresAtUtc: T0.AddMinutes(-1));

        Nem(() => luot.ChotApplied(Guid.NewGuid(), T0), PromotionErrorCodes.RedemptionNotReserved);
        Assert.Equal(PromotionRedemptionStatus.Reserved, luot.Status);
    }

    [Fact]
    public void Luot_da_Released__ChotApplied__nem_0105()
    {
        var luot = Luot(PromotionRedemptionStatus.Released);
        Nem(() => luot.ChotApplied(Guid.NewGuid(), T0), PromotionErrorCodes.RedemptionNotReserved);
    }

    [Fact]
    public void Luot_da_Applied__ChotApplied_lan_hai__nem_0105()
    {
        var luot = Luot(PromotionRedemptionStatus.Applied);
        Nem(() => luot.ChotApplied(Guid.NewGuid(), T0), PromotionErrorCodes.RedemptionNotReserved);
    }

    [Fact]
    public void Luot_Reserved__NhaReleased__thanh_Released_reason_mac_dinh_MANUAL()
    {
        var luot = Luot();
        luot.NhaReleased(T0, reasonCode: " ");
        Assert.Equal(PromotionRedemptionStatus.Released, luot.Status);
        Assert.Equal("MANUAL", luot.ReasonCode);
        Assert.Equal(T0, luot.ReleasedAtUtc);
    }

    [Fact]
    public void Luot_Applied__NhaReleased__duoc_phep_huy_don_D148()
    {
        var luot = Luot(PromotionRedemptionStatus.Applied);
        luot.NhaReleased(T0, "ORDER_CANCELLED");
        Assert.Equal(PromotionRedemptionStatus.Released, luot.Status);
        Assert.Equal("ORDER_CANCELLED", luot.ReasonCode);
    }

    [Theory]
    [InlineData(PromotionRedemptionStatus.Released)]
    [InlineData(PromotionRedemptionStatus.Reversed)]
    public void Luot_Released_hoac_Reversed__NhaReleased__nem_0105(PromotionRedemptionStatus status)
    {
        var luot = Luot(status);
        Nem(() => luot.NhaReleased(T0, "MANUAL"), PromotionErrorCodes.RedemptionNotReserved);
    }

    [Fact]
    public void Luot_Applied__DaoReversed__thanh_Reversed()
    {
        var luot = Luot(PromotionRedemptionStatus.Applied);
        luot.DaoReversed(T0, reasonCode: " ");
        Assert.Equal(PromotionRedemptionStatus.Reversed, luot.Status);
        Assert.Equal("ORDER_RETURNED", luot.ReasonCode);
        Assert.Equal(T0, luot.ReversedAtUtc);
    }

    [Theory]
    [InlineData(PromotionRedemptionStatus.Reserved)]
    [InlineData(PromotionRedemptionStatus.Released)]
    public void Luot_khong_Applied__DaoReversed__nem_0105(PromotionRedemptionStatus status)
    {
        var luot = Luot(status);
        Nem(() => luot.DaoReversed(T0, "ORDER_RETURNED"), PromotionErrorCodes.RedemptionNotReserved);
    }

    [Fact]
    public void Luot_da_Reversed__DaoReversed_lan_hai__nem_0105()
    {
        var luot = Luot(PromotionRedemptionStatus.Reversed);
        Nem(() => luot.DaoReversed(T0, "ORDER_RETURNED"), PromotionErrorCodes.RedemptionNotReserved);
    }
}
