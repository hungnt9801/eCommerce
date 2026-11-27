using System.Linq.Expressions;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Rules;
using eCommerce.Promotions.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `TC-71` (+ `TC-70(d)`, `D-289`, `D-290`) — service ĐỌC `rule-catalog`.
///
/// 🔒 Ba bất biến:
///   • (i) bảng `ProPromotionRuleCosts` **rỗng** ⇒ trả **đủ** `SystemNames`, mỗi dòng `DeclaredCostFactor = 1`,
///     `RequiresVerifiedCod = false`, `IsSeeded = false` — **KHÔNG** ném `0010`, **KHÔNG** bỏ tên nào (`D-289`).
///   • (ii) seed đủ ⇒ `IsSeeded = true` + giá trị **theo dòng** (kể cả `RetiredAtUtc`).
///   • (iii)/(v) `Label` **luôn lấy từ bảng code** `PromotionRuleCostCatalog` (không đọc DB — `D-290`); mọi
///     `SystemName` có `Label` **khác rỗng VÀ khác `SystemName`**; tên ngoài bảng ⇒ `Label = SystemName`.
///
/// 📌 Dựng bằng `new` + repo in-memory khoá CHUỖI (`RepoKhoaChuoiGia`), `HaTang.Registry()`, gán tay
/// `LazyServiceProvider` (khuôn `D-229`) — KHÔNG host ABP, KHÔNG PostgreSQL.
/// </summary>
public class PromotionRuleCatalogTests
{
    /// <summary>Nhãn tiếng Việt v1 đúng §10.1 SRS (nguồn: `PromotionRuleCostCatalog.V1`).</summary>
    private static readonly Dictionary<string, string> NhanV1 = new(StringComparer.Ordinal)
    {
        [MinOrderAmountRule.Ten] = "Giá trị đơn tối thiểu",
        [ChannelScopeRule.Ten] = "Kênh bán",
        [CustomerGroupRule.Ten] = "Nhóm khách hàng",
        [FirstOrderOnlyRule.Ten] = "Khách mua lần đầu",
    };

    private static PromotionRuleCatalogAppService TaoAppService(
        PromotionRuleRegistry registry, params PromotionRuleCost[] dong)
    {
        var repo = new RepoKhoaChuoiGia<PromotionRuleCost>();
        foreach (var d in dong)
        {
            repo.Them(d);
        }

        var sut = new PromotionRuleCatalogAppService(
            repo, registry, NullLogger<PromotionRuleCatalogAppService>.Instance);

        // `ApplicationService` lấy Logger/GuidGenerator qua `LazyServiceProvider` (property injection).
        var services = new ServiceCollection();
        services.AddLogging();
        sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());

        return sut;
    }

    [Fact]
    public async Task TC71i_bang_chi_phi_RONG__tra_du_SystemNames_mac_dinh_IsSeeded_false_khong_nem_0010()
    {
        var registry = HaTang.Registry();
        var sut = TaoAppService(registry);   // bảng rỗng

        var ds = await sut.GetRulesAsync();

        // Đủ MỌI SystemName của registry (không rơi tên nào) — thứ tự ordinal tất định.
        Assert.Equal(registry.SystemNames, ds.Select(x => x.SystemName).ToList());
        Assert.Equal(registry.SystemNames.Count, ds.Count);

        foreach (var dong in ds)
        {
            Assert.Equal(1, dong.DeclaredCostFactor);
            Assert.False(dong.RequiresVerifiedCod);
            Assert.False(dong.IsSeeded);
            Assert.Null(dong.RetiredAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(dong.Label));
        }
    }

    [Fact]
    public async Task TC71ii_seed_du__IsSeeded_true_va_gia_tri_theo_dong_ke_ca_RetiredAtUtc()
    {
        var registry = HaTang.Registry();
        var retired = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        // MinOrderAmount: có dòng sống, giá trị KHÁC mặc định (CHỨNG MINH lấy theo dòng, không lấy mặc định).
        var song = PromotionRuleCost.TaoMoi(MinOrderAmountRule.Ten, "O2", 3, requiresVerifiedCod: true);
        // ChannelScope: dòng đã GỠ ⇒ RetiredAtUtc có giá trị.
        var daGo = PromotionRuleCost.TaoMoi(ChannelScopeRule.Ten, "O1", 1, requiresVerifiedCod: false);
        daGo.DanhDauGo(retired);

        var sut = TaoAppService(registry, song, daGo);

        var ds = (await sut.GetRulesAsync()).ToDictionary(x => x.SystemName, StringComparer.Ordinal);

        var m = ds[MinOrderAmountRule.Ten];
        Assert.True(m.IsSeeded);
        Assert.Equal(3, m.DeclaredCostFactor);
        Assert.True(m.RequiresVerifiedCod);
        Assert.Null(m.RetiredAtUtc);

        var c = ds[ChannelScopeRule.Ten];
        Assert.True(c.IsSeeded);
        Assert.Equal(retired, c.RetiredAtUtc);

        // Hai rule KHÔNG có dòng ⇒ mặc định + IsSeeded = false.
        foreach (var ten in new[] { CustomerGroupRule.Ten, FirstOrderOnlyRule.Ten })
        {
            Assert.False(ds[ten].IsSeeded);
            Assert.Equal(1, ds[ten].DeclaredCostFactor);
            Assert.Null(ds[ten].RetiredAtUtc);
        }
    }

    [Fact]
    public async Task TC71iii_Label_lay_tu_bang_CODE_khong_doc_DB()
    {
        // Dòng DB KHÔNG có cột `Label` ⇒ nhãn CHỈ có thể đến từ `PromotionRuleCostCatalog` (code).
        // Đổi metadata chi phí trong DB KHÔNG được làm đổi nhãn.
        var registry = HaTang.Registry();
        var la = PromotionRuleCost.TaoMoi(MinOrderAmountRule.Ten, "O9", 7, requiresVerifiedCod: true);
        var sut = TaoAppService(registry, la);

        var dong = (await sut.GetRulesAsync()).Single(x => x.SystemName == MinOrderAmountRule.Ten);

        Assert.Equal(NhanV1[MinOrderAmountRule.Ten], dong.Label);
    }

    [Fact]
    public async Task TC70d_TC71v_moi_SystemName_co_Label_khac_rong_va_khac_SystemName__4_nhan_v1_dung()
    {
        var registry = HaTang.Registry();
        var sut = TaoAppService(registry);

        var ds = await sut.GetRulesAsync();

        foreach (var dong in ds)
        {
            Assert.False(string.IsNullOrWhiteSpace(dong.Label));
            Assert.NotEqual(dong.SystemName, dong.Label);   // §10.1: KHÔNG hiện `SystemName` thô
        }

        foreach (var (ten, nhan) in NhanV1)
        {
            Assert.Equal(nhan, ds.Single(x => x.SystemName == ten).Label);
        }
    }

    [Fact]
    public async Task TC71v_ten_ngoai_bang__Label_bang_dung_SystemName()
    {
        // Registry có một rule KHÔNG nằm trong `PromotionRuleCostCatalog.V1` ⇒ `Label = SystemName` (hiển thị thô,
        // `D-290`). Không rỗng, không ném. `D-289`: vẫn trả đủ, mặc định 1/false/IsSeeded=false.
        const string ruleNgoai = "RuleNgoaiBang";
        var registry = new PromotionRuleRegistry(
        [
            new MinOrderAmountRule(),
            new ChannelScopeRule(),
            new CustomerGroupRule(),
            new FirstOrderOnlyRule(),
            new RuleNgoaiBangGia(),
        ]);

        var sut = TaoAppService(registry);
        var ds = await sut.GetRulesAsync();

        var dong = ds.Single(x => x.SystemName == ruleNgoai);
        Assert.Equal(ruleNgoai, dong.Label);
        Assert.Equal(1, dong.DeclaredCostFactor);
        Assert.False(dong.RequiresVerifiedCod);
        Assert.False(dong.IsSeeded);
    }

    private sealed class RuleNgoaiBangGia : IPromotionConditionRule
    {
        public string SystemName => "RuleNgoaiBang";

        public bool IsSatisfied(PromotionRuleContext context) => true;
    }
}

/// <summary>
/// **Repository giả IN-MEMORY khoá CHUỖI** (`IRepository&lt;T, string&gt;`) — cho `ProPromotionRuleCosts` (PK = `RuleSystemName`).
/// 🎯 Chỉ mở đúng đường đi được test (GetListAsync/GetAsync/FindAsync/Insert/Update); **mọi** thành viên còn lại
/// NÉM `NotSupportedException` (khuôn `Pricing/HaTang/RepoGia` — `D-229` ①(A2)). Chia sẻ cho `PromotionGetLimitsTests`.
/// </summary>
internal sealed class RepoKhoaChuoiGia<T> : IRepository<T, string> where T : class, IEntity<string>
{
    private readonly Dictionary<string, T> _duLieu = new(StringComparer.Ordinal);

    public IReadOnlyCollection<T> TatCa => _duLieu.Values;

    public void Them(T entity) => _duLieu[entity.Id] = entity;

    public string? EntityName { get; set; } = typeof(T).Name;

    public string ProviderName => "Test";

    public bool? IsChangeTrackingEnabled => false;

    public IAsyncQueryableExecuter AsyncExecuter { get; } =
        new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>());

    public Task<T?> FindAsync(string id, bool includeDetails = true, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.TryGetValue(id, out var e) ? e : null);

    public Task<T> GetAsync(string id, bool includeDetails = true, CancellationToken cancellationToken = default)
        => _duLieu.TryGetValue(id, out var e)
            ? Task.FromResult(e)
            : throw new EntityNotFoundException(typeof(T), id);

    public Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.Values.FirstOrDefault(predicate.Compile()));

    public Task<List<T>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.Values.ToList());

    public Task<List<T>> GetListAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
        => Task.FromResult(_duLieu.Values.Where(predicate.Compile()).ToList());

    public Task<long> GetCountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult((long)_duLieu.Values.Count(predicate.Compile()));

    public Task<T> InsertAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<T> UpdateAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        _duLieu[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public IQueryable<T> GetQueryable() => _duLieu.Values.AsQueryable();

    public Task<IQueryable<T>> GetQueryableAsync() => Task.FromResult<IQueryable<T>>(_duLieu.Values.AsQueryable());

    public Task<long> GetCountAsync(CancellationToken cancellationToken = default) => throw Nem(nameof(GetCountAsync));

    public Task<T> GetAsync(
        Expression<Func<T, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default)
        => throw Nem(nameof(GetAsync));

    public Task InsertManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(InsertManyAsync));

    public Task UpdateManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(UpdateManyAsync));

    public Task DeleteAsync(T entity, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(string id, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteAsync));

    public Task DeleteAsync(
        Expression<Func<T, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteAsync));

    public Task DeleteManyAsync(
        IEnumerable<T> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteManyAsync));

    public Task DeleteManyAsync(
        IEnumerable<string> ids, bool autoSave = false, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteManyAsync));

    public Task DeleteDirectAsync(
        Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => throw Nem(nameof(DeleteDirectAsync));

    public Task<IQueryable<T>> WithDetailsAsync() => throw Nem(nameof(WithDetailsAsync));

    public Task<IQueryable<T>> WithDetailsAsync(Expression<Func<T, object>> propertySelector)
        => throw Nem(nameof(WithDetailsAsync));

    public Task<IQueryable<T>> WithDetailsAsync(params Expression<Func<T, object>>[] propertySelectors)
        => throw Nem(nameof(WithDetailsAsync));

    public IQueryable<T> WithDetails() => throw Nem(nameof(WithDetails));

    public IQueryable<T> WithDetails(params Expression<Func<T, object>>[] propertySelectors)
        => throw Nem(nameof(WithDetails));

    public Task<List<T>> GetPagedListAsync(
        int skipCount, int maxResultCount, string sorting, bool includeDetails = false,
        CancellationToken cancellationToken = default) => throw Nem(nameof(GetPagedListAsync));

    private static Exception Nem(string thanhVien)
        => new NotSupportedException(
            $"`RepoKhoaChuoiGia<{typeof(T).Name}>.{thanhVien}` không được dùng trong luồng này (D-229: fake chỉ mở đường đi).");
}
