using System.Reflection;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Validation;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `PRO-001` `W25-4` — hai luật domain còn thiếu ca: **`BR-19`** (`Percentage` phải `≤ 100` **và** bắt buộc
/// `MaxDiscountAmount`, `D-296`) và **`BR-09`/`ADR-09`** (`Exhausted`/`Expired` là **suy ra**, enum `Status` của mã
/// **cấm** chứa chúng).
///
/// 🔒 Đột biến thật làm đỏ: bỏ `maxDiscountAmount is null` trong `Promotion.TaoMoi` ⇒ `BR19_Percentage_thieu_tran__nem`;
/// thêm `Expired`/`Exhausted` vào `VoucherCodeStatus` ⇒ `BR09_enum_Status_ma_KHONG_chua_trang_thai_suy_ra`.
/// </summary>
public class VoucherDomainRulesTests
{
    private static readonly DateTime T0 = new(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);

    private static Promotion Tao(
        PromotionDiscountKind kind = PromotionDiscountKind.FixedAmount,
        decimal value = 1000m,
        decimal? maxDiscount = null)
        => Promotion.TaoMoi(
            Guid.NewGuid(), null, "BR19", "Khuyến mãi test", PromotionKind.Voucher,
            T0, null, 0, false, null, kind, value, maxDiscount, null, "VND", null, null);

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(101)]
    public void BR19_Percentage_ngoai_0_100__nem_AbpValidationException(decimal giaTri)
    {
        // `BR-19` (`D-296`): `Percentage` ngoài `(0, 100]` ⇒ `AbpValidationException` (400, KHÔNG `Code`).
        var ex = Assert.Throws<AbpValidationException>(
            () => Tao(PromotionDiscountKind.Percentage, giaTri, maxDiscount: 50_000m));

        Assert.NotEmpty(ex.ValidationErrors);
    }

    [Fact]
    public void BR19_Percentage_thieu_tran__nem_AbpValidationException()
    {
        // 🚨 "giảm 50 %" không trần là RỦI RO TIỀN ⇒ thiếu `MaxDiscountAmount` phải NÉM, không có mặc định ngầm.
        var ex = Assert.Throws<AbpValidationException>(
            () => Tao(PromotionDiscountKind.Percentage, 50m, maxDiscount: null));

        Assert.NotEmpty(ex.ValidationErrors);
    }

    [Fact]
    public void BR19_Percentage_hop_le_co_tran__KHONG_nem()
    {
        var p = Tao(PromotionDiscountKind.Percentage, 10m, maxDiscount: 50_000m);

        Assert.Equal(PromotionDiscountKind.Percentage, p.DiscountKind);
        Assert.Equal(10m, p.DiscountValue);
        Assert.Equal(50_000m, p.MaxDiscountAmount);
        Assert.Equal(PromotionStatus.Draft, p.Status);
    }

    [Fact]
    public void BR19_Percentage_bang_0_co_tran__v1_as_built_CHAP_NHAN__noi_0109()
    {
        // `TC-21b` (`D-296`): v1 as-built chấp nhận 0 % (0 đồng, không rủi ro tiền); `BR-19` giữ yêu cầu `0 <`
        // ⇒ nợ có chủ `PRO-002` thêm guard `discountValue <= 0` khi đổi `0109`. Ca này KHOÁ hiện trạng để đổi phải có CR.
        var p = Tao(PromotionDiscountKind.Percentage, 0m, maxDiscount: 50_000m);
        Assert.Equal(0m, p.DiscountValue);
    }

    [Fact]
    public void BR09_enum_Status_ma_KHONG_chua_trang_thai_suy_ra()
    {
        // `BR-09` / `ADR-09`: `Exhausted`/`Expired` SUY RA khi đọc; ghi vào cột `Status` ⇒ hai nguồn sự thật.
        // Enum chỉ 3 trạng thái THẬT (`VoucherCodeStatus`: Generated/Issued/Revoked) + `CHECK` DB (`D-300`).
        var ten = Enum.GetNames<VoucherCodeStatus>();

        Assert.Equal(["Generated", "Issued", "Revoked"], ten);
        Assert.DoesNotContain("Expired", ten);
        Assert.DoesNotContain("Exhausted", ten);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  Quét mã nguồn module (đọc tệp, không host) — BR-11/BR-13/BR-22
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private static string Goc()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "eCommerce.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("Không tìm thấy gốc repo (eCommerce.slnx).");
    }

    /// <summary>Mọi tệp `.cs` của module `Promotions` (bỏ `bin/`/`obj/`).</summary>
    private static List<string> TepNguonModule(string? thuMucCon = null)
    {
        var goc = Path.Combine(Goc(), "modules/Promotions", thuMucCon ?? string.Empty);
        return Directory.EnumerateFiles(goc, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void BR13_KHONG_autoSave_true_trong_duong_ghi_voucher__ngoai_no_cu_PromotionAppService()
    {
        // `BR-13`/`NFR-09`: mọi thao tác ghi qua Unit of Work — `autoSave: true` là tiền lệ SAI (`ChannelAppService`).
        // 🚨 NỢ CŨ (không phải `PRO-001`): `PromotionAppService.cs` (`PRO-002`/W24) còn 7 chỗ `autoSave: true` ⇒
        //    allowlist DƯỚI ĐÂY chỉ để KHÔNG che lỗi mới; sửa nợ cũ thuộc `rev`/`PRO-002` (testplan §5).
        var mau = new Regex(@"autoSave\s*:\s*true", RegexOptions.CultureInvariant);
        var choPhep = new[] { "PromotionAppService.cs" };

        var viPham = TepNguonModule("eCommerce.Promotions.Application")
            .Where(f => mau.IsMatch(File.ReadAllText(f)))
            .ToList();

        // ① Mọi vi phạm còn lại PHẢI nằm trong allowlist nợ cũ — thêm `autoSave: true` ở tệp khác ⇒ ĐỎ.
        var ngoaiAllowlist = viPham
            .Where(f => !choPhep.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(ngoaiAllowlist);

        // ② Đường ghi voucher của `PRO-001` (W25-2) phải SẠCH tuyệt đối.
        foreach (var ten in new[] { "VoucherAdminAppService.cs", "VoucherRedemptionService.cs", "VoucherExpiryWorker.cs" })
        {
            Assert.DoesNotContain(viPham, f => Path.GetFileName(f) == ten);
        }
    }

    [Fact]
    public void BR11_so_luot_append_only__cot_bat_bien_khong_setter_cong_khai__khong_co_duong_xoa()
    {
        // (a) `BR-11`: các cột **bất biến** của `ProPromotionRedemptions` chỉ được gán lúc INSERT — không public setter.
        string[] batBien =
        [
            "DiscountAmount", "IdempotencyKey", "VoucherCodeId", "PromotionId", "CustomerId", "UsageSequence", "OrderId",
        ];

        foreach (var ten in batBien)
        {
            var p = typeof(PromotionRedemption).GetProperty(ten, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(p is not null, $"Thiếu thuộc tính {ten} trên PromotionRedemption.");
            Assert.Null(p!.GetSetMethod(nonPublic: false));
        }

        // (b) Không có đường XOÁ sổ lượt: 0 `DELETE FROM "ProPromotionRedemptions"`, 0 `<repo lượt>.DeleteAsync`/`.Remove(`.
        var mauXoa = new Regex(@"(?i)(_redemptions\s*\.\s*(DeleteAsync|Remove)|DELETE\s+FROM\s+""ProPromotionRedemptions"")",
            RegexOptions.CultureInvariant);

        var viPham = TepNguonModule()
            .Where(f => mauXoa.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(viPham);
    }

    [Fact]
    public void BR22_log_KHONG_mang_ma_voucher_gan_khach__va_KHONG_seed_voucher_nghiep_vu()
    {
        // (a) `BR-22` + `D-299` ①(3): log chỉ `Id`/số đếm — CẤM `Code`/`VoucherCode`/`CustomerId`/`Reason` thô.
        string[] cam =
        [
            "{Code}", "{VoucherCode}", "{CustomerId}", "{Reason}",
            "voucher.Code", "input.Reason", "promotion.Code",
        ];

        var mauLog = new Regex(@"Logger\.Log(?:Information|Warning|Error|Debug)\((?<noiDung>[\s\S]*?)\);",
            RegexOptions.CultureInvariant);

        foreach (var f in TepNguonModule("eCommerce.Promotions.Application"))
        {
            var noiDung = File.ReadAllText(f);
            foreach (Match m in mauLog.Matches(noiDung))
            {
                foreach (var c in cam)
                {
                    Assert.DoesNotContain(c, m.Groups["noiDung"].Value, StringComparison.Ordinal);
                }
            }
        }

        // (b) Không seed voucher NGHIỆP VỤ: seeder module chỉ seed bảng chi phí rule (`PromotionRuleCostSeedContributor`),
        //     và `VoucherCode.TaoMoi` chỉ xuất hiện ở đường phát hành runtime (`VoucherAdminAppService`).
        foreach (var f in TepNguonModule())
        {
            var noiDung = File.ReadAllText(f);
            if (noiDung.Contains("IDataSeedContributor", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("VoucherCode", noiDung, StringComparison.Ordinal);
            }

            if (noiDung.Contains("VoucherCode.TaoMoi(", StringComparison.Ordinal))
            {
                Assert.Equal("VoucherAdminAppService.cs", Path.GetFileName(f));
            }
        }
    }
}
