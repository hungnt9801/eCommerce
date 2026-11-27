using System.ComponentModel.DataAnnotations;
using System.Reflection;
using eCommerce.Promotions.Contracts;
using Volo.Abp;
using Volo.Abp.Validation;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **GUARD DTO TƯỜNG MINH** của đường voucher (`PRO-001-arch` §6.1 bước 1 — `D-298`/`D-302`/`D-303`/`D-304`/`D-305`).
///
/// `VoucherRedemptionService` **không** phải `IApplicationService` ⇒ interceptor DataAnnotations của ABP
/// **không tự chạy** ⇒ <see cref="VoucherRequestValidator"/> phải kiểm tay và ném `AbpValidationException`
/// (HTTP **400**, 0 mã lỗi mới, 0 dòng ghi). Test **gọi thẳng** validator (pure) — KHÔNG host ABP, KHÔNG DB.
/// </summary>
public class VoucherRequestValidatorTests
{
    // ── Dựng DTO hợp lệ rồi `with` để đột biến đúng MỘT trường ────────────────────────────────────

    private static ReserveVoucherDto Reserve() => new(
        VoucherCode: "ABC",
        CheckoutSessionId: "session-1",
        CustomerId: Guid.NewGuid(),
        ChannelCode: "web",
        Lines: Array.Empty<PromotionCartLine>(),
        Subtotal: 100_000m,
        Currency: "VND",
        TtlSeconds: 900);

    private static CommitVoucherDto Commit() => new(
        RedemptionId: Guid.NewGuid(),
        OrderId: Guid.NewGuid(),
        VoucherCode: "ABC",
        CheckoutSessionId: "session-1",
        OrderDiscountTotal: 10_000m);

    private static void NemKhiReserve(ReserveVoucherDto dto)
        => Assert.Throws<AbpValidationException>(() => VoucherRequestValidator.KiemTraReserve(dto));

    private static void NemKhiCommit(CommitVoucherDto dto)
        => Assert.Throws<AbpValidationException>(() => VoucherRequestValidator.KiemTraCommit(dto));

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  ReserveVoucherDto — D-303/D-304
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reserve_hop_le__KHONG_nem()
        => VoucherRequestValidator.KiemTraReserve(Reserve()); // không ném là đạt

    // BR-06/D-304: `Subtotal ∈ [0.01, 99999999999999.9999]` — 0/âm/thiếu ⇒ NÉM.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(0.009)]
    public void D304_Reserve_Subtotal_duoi_san__nem(decimal subtotal)
        => NemKhiReserve(Reserve() with { Subtotal = subtotal });

    [Theory]
    [InlineData(0.01)]
    [InlineData(100_000)]
    public void D304_Reserve_Subtotal_trong_dai__KHONG_nem(decimal subtotal)
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { Subtotal = subtotal });

    [Fact]
    public void D304_Reserve_Subtotal_dung_tran__KHONG_nem()
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { Subtotal = 99999999999999.9999m });

    [Fact]
    public void D304_Reserve_Subtotal_vuot_tran__nem()
        => NemKhiReserve(Reserve() with { Subtotal = 100000000000000m });

    // D-304: `Currency !~ ^[A-Z]{3}$` (rỗng/sai case/độ dài sai) ⇒ NÉM; KHÔNG tự .ToUpper().
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("vnd")]
    [InlineData("VnD")]
    [InlineData("VN")]
    [InlineData("VNDD")]
    [InlineData("V1D")]
    public void D304_Reserve_Currency_sai__nem(string currency)
        => NemKhiReserve(Reserve() with { Currency = currency });

    [Theory]
    [InlineData("VND")]
    [InlineData("USD")]
    public void D304_Reserve_Currency_hop_le__KHONG_nem(string currency)
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { Currency = currency });

    // D-302/D-304: `TtlSeconds ∈ [60, 86400]`; {≤0, 59, 86401} ⇒ NÉM.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(59)]
    [InlineData(86401)]
    public void D304_Reserve_TtlSeconds_ngoai_dai__nem(int ttl)
        => NemKhiReserve(Reserve() with { TtlSeconds = ttl });

    [Theory]
    [InlineData(60)]
    [InlineData(900)]
    [InlineData(86400)]
    public void D304_Reserve_TtlSeconds_trong_dai__KHONG_nem(int ttl)
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { TtlSeconds = ttl });

    // D-303: `CheckoutSessionId` bắt buộc, 1…128 ký tự (là `string`, KHÔNG `Guid`).
    [Theory]
    [InlineData("")]
    public void D303_Reserve_CheckoutSessionId_rong__nem(string session)
        => NemKhiReserve(Reserve() with { CheckoutSessionId = session });

    [Fact]
    public void D303_Reserve_CheckoutSessionId_129_ky_tu__nem()
        => NemKhiReserve(Reserve() with { CheckoutSessionId = new string('x', 129) });

    [Theory]
    [InlineData("x")]
    [InlineData("session-1")]
    public void D303_Reserve_CheckoutSessionId_hop_le__KHONG_nem(string session)
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { CheckoutSessionId = session });

    [Fact]
    public void D303_Reserve_CheckoutSessionId_dung_128_ky_tu__KHONG_nem()
        => VoucherRequestValidator.KiemTraReserve(Reserve() with { CheckoutSessionId = new string('x', 128) });

    [Fact]
    public void D304_Reserve_nhieu_loi__nem_mot_lan_voi_nhieu_ValidationResult()
    {
        var ex = Assert.Throws<AbpValidationException>(() => VoucherRequestValidator.KiemTraReserve(
            Reserve() with { Subtotal = 0m, Currency = "vnd", TtlSeconds = 10 }));

        Assert.True(ex.ValidationErrors.Count >= 3,
            $"Phải gom mọi lỗi (Subtotal/Currency/TtlSeconds) — nhận {ex.ValidationErrors.Count}.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  CommitVoucherDto — D-305 (trường thứ 5 `OrderDiscountTotal`)
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Commit_hop_le__KHONG_nem()
        => VoucherRequestValidator.KiemTraCommit(Commit()); // không ném là đạt

    [Theory]
    [InlineData("")]
    public void D303_Commit_CheckoutSessionId_rong__nem(string session)
        => NemKhiCommit(Commit() with { CheckoutSessionId = session });

    [Fact]
    public void D303_Commit_CheckoutSessionId_129_ky_tu__nem()
        => NemKhiCommit(Commit() with { CheckoutSessionId = new string('x', 129) });

    [Fact]
    public void D303_Commit_CheckoutSessionId_dung_128_ky_tu__KHONG_nem()
        => VoucherRequestValidator.KiemTraCommit(Commit() with { CheckoutSessionId = new string('x', 128) });

    [Fact]
    public void D305_Commit_OrderDiscountTotal_am__nem()
        => NemKhiCommit(Commit() with { OrderDiscountTotal = -0.0001m });

    [Fact]
    public void D305_Commit_OrderDiscountTotal_bang_0__KHONG_nem()
        => VoucherRequestValidator.KiemTraCommit(Commit() with { OrderDiscountTotal = 0m });

    [Fact]
    public void D305_Commit_OrderDiscountTotal_vuot_tran__nem()
        => NemKhiCommit(Commit() with { OrderDiscountTotal = 100000000000000m });

    // ── D-303(v)/D-305(i) — phản chiếu: kiểu & số trường của record ────────────────────────────

    [Fact]
    public void D303_CheckoutSessionId_la_string_o_CA_HAI_record_khong_phai_Guid()
    {
        Assert.Equal(typeof(string), typeof(ReserveVoucherDto)
            .GetProperty(nameof(ReserveVoucherDto.CheckoutSessionId))!.PropertyType);
        Assert.Equal(typeof(string), typeof(CommitVoucherDto)
            .GetProperty(nameof(CommitVoucherDto.CheckoutSessionId))!.PropertyType);
    }

    [Fact]
    public void D305_CommitVoucherDto_co_DUNG_5_truong__truong_cuoi_decimal_OrderDiscountTotal()
    {
        var thamSo = typeof(CommitVoucherDto).GetConstructors()
            .Single(c => c.GetParameters().Length == 5)
            .GetParameters();

        Assert.Equal(
            new[] { "RedemptionId", "OrderId", "VoucherCode", "CheckoutSessionId", "OrderDiscountTotal" },
            thamSo.Select(p => p.Name).ToArray());
        Assert.Equal(typeof(decimal), thamSo[^1].ParameterType);
    }

    [Fact]
    public void D305_OrderDiscountTotal_co_Range_tu_0_00__cho_phep_0()
    {
        var prop = typeof(CommitVoucherDto).GetProperty(nameof(CommitVoucherDto.OrderDiscountTotal))!;
        var range = prop.GetCustomAttribute<RangeAttribute>(inherit: true);

        Assert.NotNull(range);
        Assert.Equal(0m, Convert.ToDecimal(range!.Minimum, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(99999999999999.9999m,
            Convert.ToDecimal(range.Maximum, System.Globalization.CultureInfo.InvariantCulture));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    //  KiemTraTenant — D-297/§7.3
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Tenant_GuidEmpty__nem_0112()
    {
        var ex = Assert.Throws<BusinessException>(() => VoucherRequestValidator.KiemTraTenant(Guid.Empty));
        Assert.Equal(PromotionErrorCodes.MissingTenantContext, ex.Code);
    }

    [Theory]
    [InlineData(null)] // 1-tenant hợp lệ
    public void Tenant_null__KHONG_nem(Guid? tenantId)
        => VoucherRequestValidator.KiemTraTenant(tenantId);

    [Fact]
    public void Tenant_co_guid__KHONG_nem()
        => VoucherRequestValidator.KiemTraTenant(Guid.NewGuid());
}
