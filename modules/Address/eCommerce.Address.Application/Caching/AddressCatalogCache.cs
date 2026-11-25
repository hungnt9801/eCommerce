using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace eCommerce.Address.Caching;

/// <summary>Giá trị cache: **mọi con đang `IsActive`** của một `(schemaVersion, parentCode)`.</summary>
[CacheName("AddressAdministrativeUnitList")]
public class AddressChildrenCacheItem
{
    public List<AdministrativeUnitDto> DonVis { get; set; } = [];
}

/// <summary>
/// **Cache tra cứu danh mục ĐVHC** (`ADDR-001` BR-014, `D-242`).
///
/// 🔒 KHOÁ: <c>v{schemaVersion}:{parentCode}</c> (<c>parentCode = null</c> ⇒ <c>*</c> = danh sách tỉnh).
///     **KHÔNG** có tiền tố `ecommerce:` — `AbpDistributedCacheOptions.KeyPrefix` của Host đã ghép.
/// 🔒 GIÁ TRỊ: mọi con **đang hoạt động** của `parentCode`; method lọc `Level` trong bộ nhớ ⇒ một item dùng
///     cho cả `GetProvinces`/`GetDistricts`/`GetWardsByDistrict`/`LayXaTheoTinh`.
/// 🔒 TTL (`D-242`): ABP Setting `Address:Cache:TtlSeconds` mặc định **86 400** (24 h). Giá trị
///     rỗng/`0`/âm/không phải số/không đọc được ⇒ **dùng 86400 + `LogWarning`** (cấu hình sai không được
///     làm hệ thống chạy sai im lặng — khuôn `D-224`). Cờ `Address:Cache:Enabled=false` tắt hẳn cache.
/// 🔒 XOÁ KHI NẠP LÔ là **cơ chế chính** (`D-242`); TTL chỉ là lưới cho instance lỡ mất ETO + reader-race.
/// </summary>
public class AddressCatalogCache : ITransientDependency
{
    public const string KhoaCauHinhBat = "Address:Cache:Enabled";
    public const int TtlMacDinhGiay = 86_400;

    private static readonly TimeSpan TtlMacDinh = TimeSpan.FromSeconds(TtlMacDinhGiay);

    private readonly IDistributedCache<AddressChildrenCacheItem, string> _cache;
    private readonly ISettingProvider _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AddressCatalogCache> _logger;
    private readonly Kernel.Observability.eCommerceMetrics? _metrics;

    public AddressCatalogCache(
        IDistributedCache<AddressChildrenCacheItem, string> cache,
        ISettingProvider settings,
        IConfiguration configuration,
        ILogger<AddressCatalogCache> logger,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
    {
        _cache = cache;
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>Khoá cache của một `(schemaVersion, parentCode)` — `parentCode` rỗng ⇒ danh sách tỉnh.</summary>
    public static string Khoa(int schemaVersion, string? parentCode)
        => $"v{schemaVersion}:{(string.IsNullOrWhiteSpace(parentCode) ? "*" : parentCode.Trim())}";

    public bool DangBat
    {
        get
        {
            var raw = _configuration[KhoaCauHinhBat];
            return string.IsNullOrWhiteSpace(raw) || !bool.TryParse(raw, out var bat) || bat;
        }
    }

    /// <summary>
    /// Đọc danh sách con từ cache; trượt (hoặc cache tắt) ⇒ gọi <paramref name="taiTuDb"/> rồi ghi cache.
    /// </summary>
    public async Task<IReadOnlyList<AdministrativeUnitDto>> LayConAsync(
        int schemaVersion,
        string? parentCode,
        Func<CancellationToken, Task<IReadOnlyList<AdministrativeUnitDto>>> taiTuDb,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taiTuDb);

        if (!DangBat)
        {
            return await taiTuDb(cancellationToken);
        }

        var khoa = Khoa(schemaVersion, parentCode);

        var co = await _cache.GetAsync(khoa, token: cancellationToken);

        if (co is not null)
        {
            _metrics?.AddressCatalogCacheHits.Add(1);
            return co.DonVis;
        }

        _metrics?.AddressCatalogCacheMisses.Add(1);

        var ds = await taiTuDb(cancellationToken);
        var ttl = await DocTtlAsync(cancellationToken);

        await _cache.SetAsync(
            khoa,
            new AddressChildrenCacheItem { DonVis = ds.ToList() },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            token: cancellationToken);

        return ds;
    }

    /// <summary>Xoá cache cho danh sách `parentCode` (phần tử `null` = danh sách tỉnh).</summary>
    public async Task XoaAsync(
        int schemaVersion,
        IEnumerable<string?> parentCodes,
        CancellationToken cancellationToken = default)
    {
        if (!DangBat)
        {
            return;
        }

        var khoas = parentCodes
            .Select(p => Khoa(schemaVersion, p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (khoas.Count == 0)
        {
            return;
        }

        await _cache.RemoveManyAsync(khoas, token: cancellationToken);
    }

    private async Task<TimeSpan> DocTtlAsync(CancellationToken cancellationToken)
    {
        var raw = await _settings.GetOrNullAsync(AddressSettingNames.CacheTtlSeconds);

        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "Setting {Setting} rỗng/không đọc được ⇒ dùng TTL mặc định {Ttl} giây (D-242).",
                AddressSettingNames.CacheTtlSeconds, TtlMacDinhGiay);
            return TtlMacDinh;
        }

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var giay) || giay <= 0)
        {
            _logger.LogWarning(
                "Setting {Setting} = '{GiaTri}' không hợp lệ (phải là số > 0) ⇒ dùng TTL mặc định {Ttl} giây (D-242).",
                AddressSettingNames.CacheTtlSeconds, raw, TtlMacDinhGiay);
            return TtlMacDinh;
        }

        return TimeSpan.FromSeconds(giay);
    }
}
