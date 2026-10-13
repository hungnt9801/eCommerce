using eCommerce.Tax.RateSchedules;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;

namespace eCommerce.Tax.Classifications;

/// <summary>
/// Cài đặt <see cref="ITaxRateResolver"/>: nạp dữ liệu hiệu lực từ DB rồi giao lại cho
/// <see cref="TaxRateResolver"/> (hàm thuần) tính toán.
///
/// 📌 Vì sao dùng <see cref="IRepository{TEntity,TKey}"/> chung của ABP thay vì tự viết repository:
/// ABP chỉ đăng ký <c>IRepository&lt;T&gt;</c> cho entity **có <c>DbSet</c> trên DbContext hợp nhất**.
/// Module không được tham chiếu DbContext của app, nên viết repository EF ở đây là không thể —
/// và cũng không cần: <c>GetQueryableAsync()</c> + <c>IAsyncQueryableExecuter</c> là đủ.
/// </summary>
public class TaxRateResolverService : DomainService, ITaxRateResolver
{
    private readonly IRepository<TaxClassification, Guid> _classificationRepository;
    private readonly IRepository<TaxRateSchedule, Guid> _scheduleRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public TaxRateResolverService(
        IRepository<TaxClassification, Guid> classificationRepository,
        IRepository<TaxRateSchedule, Guid> scheduleRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _classificationRepository = classificationRepository;
        _scheduleRepository = scheduleRepository;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<TaxRateResolution> ResolveEffectiveRateAsync(
        Guid productVariantId,
        IReadOnlyList<Guid> categoryIdsMostSpecificFirst,
        DateOnly at,
        CancellationToken cancellationToken = default)
    {
        var targetIds = new List<Guid>(categoryIdsMostSpecificFirst.Count + 1) { productVariantId };
        targetIds.AddRange(categoryIdsMostSpecificFirst);

        // Chỉ nạp bản ghi ĐANG ảnh hưởng tính thuế (Approved/Active) và có khung chứa ngày `at`.
        var classificationQuery = await _classificationRepository.GetQueryableAsync();
        var classifications = await _asyncExecuter.ToListAsync(
            classificationQuery.Where(c =>
                targetIds.Contains(c.TargetId)
                && (c.Status == TaxClassificationStatus.Approved || c.Status == TaxClassificationStatus.Active)
                && c.EffectiveFrom <= at
                && (c.EffectiveTo == null || c.EffectiveTo >= at)),
            cancellationToken);

        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var schedules = await _asyncExecuter.ToListAsync(
            scheduleQuery.Where(s => s.EffectiveFrom <= at && (s.EffectiveTo == null || s.EffectiveTo >= at)),
            cancellationToken);

        return TaxRateResolver.Resolve(
            productVariantId,
            categoryIdsMostSpecificFirst,
            classifications,
            schedules,
            at);
    }
}
