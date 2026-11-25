using System.Diagnostics;
using eCommerce.Address;
using eCommerce.Address.Caching;
using eCommerce.Tests.AddressSync.HaTang;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` BR-014 + `D-242` + TC-14** — cache tra cứu danh mục ĐVHC:
///   • khoá là `(schemaVersion, parentCode)`, **không** phải `parentCode` trần;
///   • TTL mặc định **86400 s**; Setting rỗng/`"0"`/`"-5"`/`"abc"` ⇒ **86400 + `LogWarning`**;
///   • `Address:Cache:Enabled=false` ⇒ **đọc DB** (không chạm cache);
///   • **xoá cache khi nạp lô** — kể cả parent **vừa biến mất** khỏi danh mục (phần tử `null` = danh sách tỉnh).
///
/// 📌 Dùng fake `IDistributedCache` **có đếm** (ghi lại TTL + khoá đã xoá) thay vì Redis thật — cách đo được
/// và tất định trong CI, ghi rõ ở testplan (`W18-4` cho phép "fake có đếm"). Luồng Redis/ETO thật đo ở
/// integration `TC-11`; ở đây kiểm **hợp đồng** của lớp cache.
/// </summary>
public class AddressCatalogCacheTests
{
    private readonly ITestOutputHelper _output;

    public AddressCatalogCacheTests(ITestOutputHelper output) => _output = output;

    private static AdministrativeUnitDto DonVi(string ma, string? cha = null, int cap = 2)
        => new(Guid.NewGuid(), 2, (AdministrativeLevel)cap, ma, "Tên " + ma, cha, true);

    private static IReadOnlyList<AdministrativeUnitDto> Ds(params AdministrativeUnitDto[] ds) => ds;

    // ── Khoá cache ──────────────────────────────────────────────────────────

    [Fact]
    public void BR014_khoa_cache_la_schemaVersion_va_parentCode__khong_tien_to()
    {
        Assert.Equal("v2:*", AddressCatalogCache.Khoa(2, null));
        Assert.Equal("v2:*", AddressCatalogCache.Khoa(2, "   "));
        Assert.Equal("v1:01", AddressCatalogCache.Khoa(1, " 01 "));

        // 🔒 Không tiền tố `ecommerce:` — Host ghép qua `AbpDistributedCacheOptions.KeyPrefix` (D-242).
        Assert.DoesNotContain("ecommerce:", AddressCatalogCache.Khoa(2, "01"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BR014_cache_HIT_thi_KHONG_goi_DB_va_tra_dung_du_lieu()
    {
        var (cache, bo) = TaoCache();
        var daCo = new AddressChildrenCacheItem { DonVis = [DonVi("00004", "01")] };
        bo.CachePhanTan.DuLieu["v2:01"] = daCo;

        var soLanDb = 0;
        var kq = await cache.LayConAsync(2, "01", _ =>
        {
            soLanDb++;
            return Task.FromResult(Ds());
        });

        Assert.Equal(0, soLanDb);
        Assert.Same(daCo.DonVis, kq);
        Assert.Equal(1, bo.CachePhanTan.SoLanDoc);
    }

    [Fact]
    public async Task BR014_cache_MISS_thi_goi_DB_roi_ghi_cache()
    {
        var (cache, bo) = TaoCache();

        var kq = await cache.LayConAsync(2, "01", _ => Task.FromResult(Ds(DonVi("00004", "01"))));

        Assert.Single(kq);
        Assert.Single(bo.CachePhanTan.LanSet);
        Assert.Equal("v2:01", bo.CachePhanTan.LanSet[0].Khoa);
        Assert.Equal(TimeSpan.FromSeconds(86_400), bo.CachePhanTan.LanSet[0].Ttl);
    }

    // ── D-242: TTL mặc định + giá trị sai ⇒ 86400 + LogWarning ───────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("12.5")]
    public async Task D242_TTL_rong_hoac_sai_thi_dung_86400_va_LogWarning(string? giaTri)
    {
        var (cache, bo) = TaoCache(settings: new Dictionary<string, string?>
        {
            [AddressSettingNames.CacheTtlSeconds] = giaTri
        });

        await cache.LayConAsync(2, null, _ => Task.FromResult(Ds(DonVi("01"))));

        Assert.Equal(TimeSpan.FromSeconds(86_400), bo.CachePhanTan.LanSet.Single().Ttl);
        Assert.Contains(
            bo.LogCache.BanGhi,
            g => g.Level == LogLevel.Warning && g.Message.Contains(AddressSettingNames.CacheTtlSeconds, StringComparison.Ordinal));
    }

    [Fact]
    public async Task D242_TTL_hop_le_thi_dung_dung_gia_tri_Setting()
    {
        var (cache, bo) = TaoCache(settings: new Dictionary<string, string?>
        {
            [AddressSettingNames.CacheTtlSeconds] = "3600"
        });

        await cache.LayConAsync(2, null, _ => Task.FromResult(Ds(DonVi("01"))));

        Assert.Equal(TimeSpan.FromSeconds(3600), bo.CachePhanTan.LanSet.Single().Ttl);
        Assert.DoesNotContain(bo.LogCache.BanGhi, g => g.Level == LogLevel.Warning);
    }

    // ── D-242: cờ Enabled=false ⇒ đọc DB, không chạm cache ───────────────────

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public async Task D242_cache_TAT_thi_Luon_doc_DB_va_KHONG_cham_cache(string giaTri)
    {
        var (cache, bo) = TaoCache(cauHinh: new Dictionary<string, string?>
        {
            [AddressCatalogCache.KhoaCauHinhBat] = giaTri
        });

        var soLanDb = 0;
        var kq = await cache.LayConAsync(2, null, _ =>
        {
            soLanDb++;
            return Task.FromResult(Ds(DonVi("01")));
        });

        Assert.False(cache.DangBat);
        Assert.Equal(1, soLanDb);
        Assert.Single(kq);
        Assert.Equal(0, bo.CachePhanTan.SoLanDoc);
        Assert.Empty(bo.CachePhanTan.LanSet);
    }

    [Theory]
    [InlineData(null)]      // không cấu hình ⇒ bật (mặc định an toàn có lưới TTL)
    [InlineData("true")]
    [InlineData("rac")]     // không parse được ⇒ bật (khuôn D-224: cấu hình hỏng không tắt âm thầm)
    public async Task D242_cache_BAT_khi_khong_cau_hinh_hoac_true_hoac_khong_parse_duoc(string? giaTri)
    {
        var (cache, bo) = TaoCache(cauHinh: new Dictionary<string, string?>
        {
            [AddressCatalogCache.KhoaCauHinhBat] = giaTri
        });

        await cache.LayConAsync(2, null, _ => Task.FromResult(Ds(DonVi("01"))));

        Assert.True(cache.DangBat);
        Assert.Single(bo.CachePhanTan.LanSet);
    }

    // ── TC-14: xoá cache khi nạp lô — KỂ CẢ parent vừa biến mất ──────────────

    [Fact]
    public async Task TC14_XoaAsync_xoa_ca_parent_VUA_BIEN_MAT_va_danh_sach_tinh()
    {
        var (cache, bo) = TaoCache();

        // Cache đang giữ 3 khoá: danh sách tỉnh, xã của tỉnh 01, xã của tỉnh 02 (tỉnh 02 SẮP biến mất).
        bo.CachePhanTan.DuLieu["v2:*"] = new AddressChildrenCacheItem { DonVis = [DonVi("01")] };
        bo.CachePhanTan.DuLieu["v2:01"] = new AddressChildrenCacheItem { DonVis = [DonVi("00004", "01")] };
        bo.CachePhanTan.DuLieu["v2:02"] = new AddressChildrenCacheItem { DonVis = [DonVi("00009", "02")] };
        bo.CachePhanTan.DuLieu["v2:99"] = new AddressChildrenCacheItem { DonVis = [DonVi("00099", "99")] };

        // Tập xoá mà `ImportDatasetAsync` phát = hợp {null} ∪ parent mới ∪ parent hiện có TRƯỚC commit.
        await cache.XoaAsync(2, [null, "01", "02"]);

        Assert.DoesNotContain("v2:*", bo.CachePhanTan.DuLieu.Keys);
        Assert.DoesNotContain("v2:01", bo.CachePhanTan.DuLieu.Keys);
        Assert.DoesNotContain("v2:02", bo.CachePhanTan.DuLieu.Keys);   // 🔑 tỉnh vừa biến mất KHÔNG được giữ cache con
        Assert.Contains("v2:99", bo.CachePhanTan.DuLieu.Keys);          // không nằm trong tập xoá ⇒ giữ nguyên
    }

    [Fact]
    public async Task TC14_XoaAsync_khu_trung_va_bo_qua_khi_cache_tat()
    {
        var (cache, bo) = TaoCache(cauHinh: new Dictionary<string, string?>
        {
            [AddressCatalogCache.KhoaCauHinhBat] = "false"
        });

        bo.CachePhanTan.DuLieu["v2:*"] = new AddressChildrenCacheItem();

        await cache.XoaAsync(2, [null, "01"]);

        // Cache đang TẮT ⇒ không xoá (đường lùi: đọc DB như trước), dữ liệu cũ để nguyên.
        Assert.Empty(bo.CachePhanTan.LanXoa);
        Assert.Contains("v2:*", bo.CachePhanTan.DuLieu.Keys);
    }

    [Fact]
    public async Task TC14_XoaAsync_khu_trung_lap_va_bo_qua_parent_rong_quy_ve_danh_sach_tinh()
    {
        var (cache, bo) = TaoCache();

        bo.CachePhanTan.DuLieu["v2:*"] = new AddressChildrenCacheItem();
        bo.CachePhanTan.DuLieu["v2:01"] = new AddressChildrenCacheItem();

        await cache.XoaAsync(2, [null, "01", "01", "01"]);

        // Trùng lặp ⇒ 1 danh sách khoá duy nhất (không gọi RemoveMany nhiều lần).
        var lan = Assert.Single(bo.CachePhanTan.LanXoa);
        Assert.Equal(2, lan.Count);
        Assert.Contains("v2:*", lan);
        Assert.Contains("v2:01", lan);
    }

    // ── NFR-001: p95 tra xã NÓNG (cache hit) < 10 ms ─────────────────────────
    /// <summary>
    /// Đo p95 đường **cache HIT** (nóng) qua `AddressCatalogCache.LayConAsync`. 📌 Đây là **số đo tay** trên
    /// fake in-memory (KHÔNG có RTT Redis) ⇒ **cận dưới**; ghi vào testplan là *bằng chứng đo*, không phải cổng
    /// cứng của CI. Nguội (DB) đo ở `PostgreSqlAddressSyncTests.Nfr001_...` (PostgreSQL thật).
    /// </summary>
    [Fact]
    public async Task Nfr001_p95_tra_xa_NONG_cache_duoi_10ms()
    {
        var (cache, bo) = TaoCache();

        // Tỉnh lớn nhất: 200 xã/phường trong CÙNG một item cache (đúng hình dạng giá trị cache).
        var ds = Enumerable.Range(0, 200)
            .Select(i => DonVi((i + 1).ToString("D5"), "01"))
            .ToList();
        bo.CachePhanTan.DuLieu["v2:01"] = new AddressChildrenCacheItem { DonVis = ds };

        // 20 lượt khởi động (JIT/warm-up), 500 lượt đo.
        for (var i = 0; i < 20; i++)
        {
            await cache.LayConAsync(2, "01", _ => throw new InvalidOperationException("HIT phải KHÔNG gọi DB."));
        }

        var mau = new List<double>(500);
        for (var i = 0; i < 500; i++)
        {
            var sw = Stopwatch.StartNew();
            await cache.LayConAsync(2, "01", _ => throw new InvalidOperationException("HIT phải KHÔNG gọi DB."));
            sw.Stop();
            mau.Add(sw.Elapsed.TotalMilliseconds);
        }

        var p95 = P95(mau);
        _output.WriteLine(
            $"NFR-001 NÓNG (cache hit, fake in-memory, 200 xã × 500 lượt): p95 = {p95:F3} ms");

        Assert.True(p95 < 10.0, $"p95 nóng {p95:F3} ms ≥ 10 ms (NFR-001).");
    }

    private static double P95(List<double> mau)
    {
        mau.Sort();
        var chiSo = (int)Math.Ceiling(0.95 * mau.Count) - 1;
        return mau[Math.Clamp(chiSo, 0, mau.Count - 1)];
    }

    private static (AddressCatalogCache Cache, BoDoDiaChi Bo) TaoCache(
        Dictionary<string, string?>? settings = null,
        Dictionary<string, string?>? cauHinh = null)
    {
        var bo = new BoDoDiaChi(settings, cauHinh, luonBanKhoa: false, now: null);
        return (bo.Cache, bo);
    }
}
