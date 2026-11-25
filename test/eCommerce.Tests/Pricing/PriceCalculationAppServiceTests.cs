using System.Text.Json;
using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Tests.Pricing.HaTang;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// **TẦNG APP SERVICE — `PriceCalculationAppService`** (`PRC-002` `-arch` §6.3 ca **1c**; `D-229` ①(A5)).
///
/// 🚨 Vì sao cần ca này: `W16-2` đã bỏ **hai chính sách cứng trong code** (làm tròn + thiếu giá) và thay bằng
/// snapshot đọc từ `ISettingProvider` ở **B1**. Nhưng nếu **không** có test tầng app service thì:
///   • snapshot có thể vẫn cứng `ChinhSachThieuGia = NemLoi` mà **mọi test domain vẫn xanh**;
///   • ai đó có thể đọc lại setting giữa pipeline (vi phạm **ADR-08**/*BR-PRC-002-010*) mà không ai thấy;
///   • `RoundingPolicyCode` trong kết quả có thể không phải mã policy **đã đọc được**.
///
/// 🎯 Cách dựng: 6 repository giả **rỗng** + `ISettingProvider` giả (khuôn `D-229`). `TinhGioInput.At` **khác
/// `null`** để không chạm `Clock` — app service lấy `at ?? Clock.Now`.
///
/// 🔒 BR được neo ở đây: **BR-PRC-002-006** (breakdown phải có), **BR-PRC-002-010** (chính sách đọc đúng
/// **một lần** + mã policy lưu vào kết quả), **BR-PRC-002-016** (idempotency), **BR-PRC-002-020** (sổ
/// append-only — chỉ chèn, không sửa/xoá).
/// </summary>
public class PriceCalculationAppServiceTests
{
    private static readonly DateTime NgayTinh = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    private sealed record BoGia(
        PriceCalculationAppService App,
        RepoGia<PriceCalculationRecord> SoGiaiTrinh,
        SettingGia Setting);

    private static BoGia Tao(string? thieuGia = nameof(ChinhSachThieuGia.NemLoi))
    {
        var soGiaiTrinh = new RepoGia<PriceCalculationRecord>();

        var setting = new SettingGia(new Dictionary<string, string?>
        {
            [PRC002SettingTenKhoa.ChinhSachLamTron] = "half-away-from-zero",
            [PRC002SettingTenKhoa.ThieuGia] = thieuGia,
            [PRC002SettingTenKhoa.TongDonAm] = nameof(ChinhSachTongDonAm.Reject),
        });

        var app = HaTangAbp.Noi(new PriceCalculationAppService(
            new RepoGia<ChannelPrice>(),      // giá theo kênh
            new RepoGia<PriceList>(),         // bảng giá
            new RepoGia<PriceListItem>(),     // dòng giá
            new RepoGia<TierPriceSet>(),      // bộ bậc thang
            new RepoGia<TierPrice>(),         // bậc thang
            soGiaiTrinh,
            setting));

        return new BoGia(app, soGiaiTrinh, setting);
    }

    private static TinhGioInput Gio(decimal? giaNiemYet, string khoa = "checkout:1") => new()
    {
        ChannelCode = "web",
        At = NgayTinh,
        IdempotencyKey = khoa,
        Items =
        [
            new DongGioInput
            {
                Sku = "SP-1",
                Quantity = 2,
                ListPrice = giaNiemYet,
            }
        ]
    };

    // ── 1c — `MissingPricePolicy = DungGiaNiemYet` ⇒ dùng giá niêm yết (BR-006, BR-010) ─────────

    [Fact]
    public async Task TinhGio__DungGiaNiemYet__tra_gia_niem_yet_va_ghi_ro_breakdown_1c()
    {
        var bo = Tao(nameof(ChinhSachThieuGia.DungGiaNiemYet));

        var kq = await bo.App.TinhGioAsync(Gio(giaNiemYet: 189_000m));

        var dong = Assert.Single(kq.Lines);
        Assert.Equal("GiaNiemYet", dong.BaseLevel);                    // BR-006: breakdown chỉ rõ cấp thắng
        Assert.Equal("GiaNiemYet", dong.WinningLevel);
        Assert.Equal(189_000m, dong.UnitPrice);
        Assert.Equal("VND", kq.CurrencyCode);

        // BR-010: mã policy trả ra là mã **đã đọc được** từ setting (chìa khoá tái lập).
        Assert.Equal("half-away-from-zero", kq.RoundingPolicyCode);

        // BR-020: mỗi lần tính ghi ĐÚNG MỘT bản ghi giải trình.
        Assert.Equal(1, bo.SoGiaiTrinh.SoLanChen);
    }

    // ── 1c — `MissingPricePolicy = NemLoi` + không có giá nào ⇒ NÉM 0004 ───────────────────────

    [Fact]
    public async Task TinhGio__NemLoi_va_khong_co_gia_nao__nem_0004_1c()
    {
        var bo = Tao(nameof(ChinhSachThieuGia.NemLoi));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.App.TinhGioAsync(Gio(giaNiemYet: null)));

        Assert.Equal(PricingErrorCodes.KhongTimThayGia, ex.Code);

        // 🚨 Ném TRƯỚC khi ghi sổ ⇒ không có bản ghi rác (đúng thứ tự B1–B8 rồi mới ghi).
        Assert.Equal(0, bo.SoGiaiTrinh.SoLanChen);
    }

    // ── BR-010/ADR-08 — mỗi khoá policy đọc ĐÚNG MỘT LẦN cho một lần tính ──────────────────────

    [Fact]
    public async Task TinhGio__moi_khoa_chinh_sach_doc_DUNG_MOT_LAN_cho_mot_lan_tinh_1c()
    {
        var bo = Tao(nameof(ChinhSachThieuGia.DungGiaNiemYet));

        await bo.App.TinhGioAsync(Gio(giaNiemYet: 189_000m));

        // ADR-08/BR-PRC-002-010: snapshot ở B1 đọc mỗi khoá đúng 1 lần; đọc lại giữa pipeline ⇒ hai dòng
        // cùng hoá đơn có thể dùng hai chính sách.
        Assert.Equal(1, bo.Setting.SoLanDocCua(PRC002SettingTenKhoa.ChinhSachLamTron));
        Assert.Equal(1, bo.Setting.SoLanDocCua(PRC002SettingTenKhoa.ThieuGia));
        Assert.Equal(1, bo.Setting.SoLanDocCua(PRC002SettingTenKhoa.TongDonAm));

        // Ngưỡng số bậc là guard của TierPriceSetAppService, KHÔNG thuộc pipeline tính giá ⇒ 0 lượt đọc.
        Assert.Equal(0, bo.Setting.SoLanDocCua(PRC002SettingTenKhoa.SoBacToiDa));
    }

    // ── BR-016/BR-020 — idempotency: cùng khoá ⇒ trả kết quả ĐÃ LƯU, không tính lại, không ghi thêm ──

    [Fact]
    public async Task TinhGio__cung_IdempotencyKey__tra_ket_qua_da_luu_va_KHONG_ghi_them_1c()
    {
        var bo = Tao(nameof(ChinhSachThieuGia.DungGiaNiemYet));

        var daLuu = new GioTinhTienDto
        {
            CurrencyCode = "VND",
            RoundingPolicyCode = "half-away-from-zero",
            Subtotal = 378_000m,
            Total = 378_000m,
            Lines =
            [
                new ChiTietDongDto { Sku = "SP-1", Quantity = 2, UnitPrice = 189_000m, LineTotal = 378_000m }
            ]
        };

        bo.SoGiaiTrinh.Them(new PriceCalculationRecord(
            Guid.NewGuid(), "checkout:1", "web", null, NgayTinh,
            daLuu.RoundingPolicyCode, daLuu.CurrencyCode,
            daLuu.Subtotal, 0m, 0m, 0m, daLuu.Total,
            JsonSerializer.Serialize(daLuu)));

        var kq = await bo.App.TinhGioAsync(Gio(giaNiemYet: 189_000m, khoa: "checkout:1"));

        Assert.Equal(378_000m, kq.Total);
        Assert.Equal("SP-1", Assert.Single(kq.Lines).Sku);

        // Không tính lại ⇒ không đọc setting; không ghi thêm bản ghi.
        Assert.Equal(0, bo.SoGiaiTrinh.SoLanChen);
        Assert.Empty(bo.Setting.SoLanDoc);

        // Sổ giữ nguyên đúng 1 bản ghi đã có.
        Assert.Single(bo.SoGiaiTrinh.TatCa);
    }
}
