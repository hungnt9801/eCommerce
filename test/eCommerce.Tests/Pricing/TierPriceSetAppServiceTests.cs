using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Tests.Pricing.HaTang;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// **TẦNG APP SERVICE — `TierPriceSetAppService.AddTierAsync`** (`PRC-002` `-arch` §6.3 ca **2a/2b**;
/// `D-229` ①(A5)).
///
/// 🚨 Vì sao cần ca này: `W16-2` thêm guard *"số bậc mỗi bộ ≤ `Pricing:MaxTiersPerSet`"* (`D-218` ①(2),
/// `D-224`). Guard này **chỉ** sống ở tầng app service (`TierPriceSetAppService.cs:114-128`) — test domain
/// **không** chạm tới. Nếu guard bị gỡ, hoặc đọc setting sai/không đọc, thì không có gì đỏ.
///
/// 🔒 Hai bất biến được đo **bằng số** (`D-229` ② T3 — không chỉ "không ném"):
///   1. **Không ghi nửa**: khi vượt ngưỡng, `InsertAsync` **chưa hề** được gọi (`SoLanChen = 0`) và
///      `Count` của repository **trước = sau**.
///   2. **Đọc setting có thật**: cùng dữ liệu, đổi `Pricing:MaxTiersPerSet` ⇒ kết quả đổi theo (`2b`).
///
/// 🔒 BR được neo: **BR-PRC-002-005** (trùng `MinQuantity`), **BR-PRC-002-014** (bậc là DỮ LIỆU, ngưỡng là
/// CẤU HÌNH — không hard-code), **BR-PRC-002-015** (guard sai ⇒ NÉM có mã, không no-op).
/// </summary>
public class TierPriceSetAppServiceTests
{
    private sealed record BoGia(
        TierPriceSetAppService App,
        TierPriceSet Bo,
        RepoGia<TierPrice> Bac,
        SettingGia Setting);

    private static BoGia Tao(int soBacSanCo, string? soBacToiDa = null)
    {
        var bo = new TierPriceSet(
            Guid.NewGuid(), "BT-1", "Bộ bậc 1", PriceListScope.All, Currency.Vnd,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var boRepo = new RepoGia<TierPriceSet>();
        boRepo.Them(bo);

        var bacRepo = new RepoGia<TierPrice>();

        for (var minQuantity = 1; minQuantity <= soBacSanCo; minQuantity++)
        {
            bacRepo.Them(new TierPrice(
                Guid.NewGuid(), bo.Id, "SP-1", minQuantity, Money.Vnd(1_000m), Currency.Vnd));
        }

        var setting = new SettingGia(soBacToiDa is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [PRC002SettingTenKhoa.SoBacToiDa] = soBacToiDa });

        var app = HaTangAbp.Noi(new TierPriceSetAppService(boRepo, bacRepo, setting));

        return new BoGia(app, bo, bacRepo, setting);
    }

    private static AddTierPriceDto BacMoi() => new()
    {
        Sku = "SP-1",
        MinQuantity = 999,   // không trùng `MinQuantity` nào đang có
        Amount = 1_000m,
    };

    // ── 2a — mặc định 20: bậc 20 ĐƯỢC thêm ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddTier__duoi_nguong_20__them_duoc_va_chen_DUNG_MOT_LAN_2a()
    {
        var bo = Tao(soBacSanCo: 19);

        var dto = await bo.App.AddTierAsync(bo.Bo.Id, BacMoi());

        Assert.Equal(999, dto.MinQuantity);
        Assert.Equal(1, bo.Bac.SoLanChen);
        Assert.Equal(20, bo.Bac.TatCa.Count);
    }

    // ── 2a — mặc định 20: bậc 21 NÉM 0011, KHÔNG ghi nửa ───────────────────────────────────────

    [Fact]
    public async Task AddTier__bac_thu_21__nem_0011_va_KHONG_ghi_nua_2a()
    {
        var bo = Tao(soBacSanCo: 20);
        var demTruoc = bo.Bac.TatCa.Count;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.App.AddTierAsync(bo.Bo.Id, BacMoi()));

        Assert.Equal(PricingErrorCodes.VuotSoBacToiDa, ex.Code);
        Assert.Equal(20, ex.Data["soBacHienTai"]);
        Assert.Equal(20, ex.Data["toiDa"]);
        Assert.Equal(bo.Bo.Id, ex.Data["tierPriceSetId"]);

        // 🚨 "Không ghi nửa" đo bằng SỐ, không chỉ bằng "không ném".
        Assert.Equal(0, bo.Bac.SoLanChen);
        Assert.Equal(demTruoc, bo.Bac.TatCa.Count);
    }

    // ── 2b — Setting đổi ⇒ ngưỡng đổi theo ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddTier__MaxTiersPerSet_bang_3__bac_thu_4_bi_chan_2b()
    {
        var bo = Tao(soBacSanCo: 3, soBacToiDa: "3");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.App.AddTierAsync(bo.Bo.Id, BacMoi()));

        Assert.Equal(PricingErrorCodes.VuotSoBacToiDa, ex.Code);
        Assert.Equal(3, ex.Data["soBacHienTai"]);
        Assert.Equal(3, ex.Data["toiDa"]);
        Assert.Equal(0, bo.Bac.SoLanChen);
    }

    [Fact]
    public async Task AddTier__MaxTiersPerSet_bang_3__bac_thu_3_VAN_them_duoc_2b()
    {
        // Neo dương: ngưỡng `3` không được chặn sớm hơn (bậc 3 vẫn OK).
        var bo = Tao(soBacSanCo: 2, soBacToiDa: "3");

        await bo.App.AddTierAsync(bo.Bo.Id, BacMoi());

        Assert.Equal(1, bo.Bac.SoLanChen);
        Assert.Equal(3, bo.Bac.TatCa.Count);
    }

    [Theory]
    [InlineData("abc")]   // không phải số
    [InlineData("")]      // rỗng
    [InlineData("0")]     // `D-224`/AQ4: 0 KHÔNG phải "tắt", phải rơi về 20
    public async Task AddTier__MaxTiersPerSet_KHONG_hop_le__dung_mac_dinh_20__bac_21_moi_nem_2b(string giaTri)
    {
        var bo = Tao(soBacSanCo: 20, soBacToiDa: giaTri);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.App.AddTierAsync(bo.Bo.Id, BacMoi()));

        Assert.Equal(PricingErrorCodes.VuotSoBacToiDa, ex.Code);
        Assert.Equal(20, ex.Data["toiDa"]);          // fallback về mặc định, KHÔNG dùng 0/rác
        Assert.Equal(0, bo.Bac.SoLanChen);
    }

    [Fact]
    public async Task AddTier__MaxTiersPerSet_KHONG_hop_le__van_cho_them_khi_chua_toi_20_2b()
    {
        // Neo dương cho fallback: `"abc"` ⇒ 20 ⇒ bộ 3 bậc vẫn thêm được bậc 4 (không bị chặn bởi giá trị rác).
        var bo = Tao(soBacSanCo: 3, soBacToiDa: "abc");

        await bo.App.AddTierAsync(bo.Bo.Id, BacMoi());

        Assert.Equal(1, bo.Bac.SoLanChen);
        Assert.Equal(4, bo.Bac.TatCa.Count);
    }

    // ── BR-005 — trùng `MinQuantity` vẫn NÉM 0008 trước khi chạm ngưỡng ─────────────────────────

    [Fact]
    public async Task AddTier__trung_MinQuantity__nem_0008_va_khong_chen_BR005()
    {
        var bo = Tao(soBacSanCo: 3);

        var trung = new AddTierPriceDto { Sku = "SP-1", MinQuantity = 2, Amount = 1_000m };

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.App.AddTierAsync(bo.Bo.Id, trung));

        Assert.Equal(PricingErrorCodes.BacThangTrungMinQuantity, ex.Code);
        Assert.Equal(0, bo.Bac.SoLanChen);
    }
}
