using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using eCommerce.Promotions.Rules;
using eCommerce.Promotions.Validation;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace eCommerce.Promotions;

/// <summary>
/// **App service quản trị cây khuyến mãi** (`PRO-002` §6.2, allowlist 10 method `TC-31`).
///
/// 🔐 `[Authorize]` ở **CẤP CLASS** (`Promotions.Default`) + method hẹp (`View`/`Manage`/`Approve`/`Import`).
/// 🚫 KHÔNG có `UpdateAsync` (`D-150`).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Promotions.Default)]
public class PromotionAppService : ApplicationService, IPromotionAppService
{
    private readonly IRepository<Promotion, Guid> _promotions;
    private readonly IRepository<PromotionCondition, Guid> _conditions;
    private readonly IRepository<PromotionRuleCost, string> _ruleCosts;
    private readonly IRepository<PromotionRedemption, Guid> _redemptions;
    private readonly IPromotionEngine _engine;
    private readonly PromotionRuleRegistry _registry;
    private readonly IAbpDistributedLock _lock;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;

    public PromotionAppService(
        IRepository<Promotion, Guid> promotions,
        IRepository<PromotionCondition, Guid> conditions,
        IRepository<PromotionRuleCost, string> ruleCosts,
        IRepository<PromotionRedemption, Guid> redemptions,
        IPromotionEngine engine,
        PromotionRuleRegistry registry,
        IAbpDistributedLock lockService,
        IGuidGenerator guid,
        IClock clock)
    {
        _promotions = promotions;
        _conditions = conditions;
        _ruleCosts = ruleCosts;
        _redemptions = redemptions;
        _engine = engine;
        _registry = registry;
        _lock = lockService;
        _guid = guid;
        _clock = clock;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PromotionDetailDto> GetAsync(Guid id)
    {
        var p = await _promotions.GetAsync(id);
        var dto = new PromotionDetailDto();
        Map(p, dto);

        // `D-288`: trần cây theo tenant — MỘT cửa đọc setting (`DocLimitsAsync`), additive trên DTO.
        var limits = await DocLimitsAsync();
        dto.Limits = new PromotionTreeLimitsDto
        {
            MaxNodes = limits.MaxNodes,
            MaxDepth = limits.MaxDepth,
            MaxLeaves = limits.MaxLeaves,
            MaxDeclaredCombinations = limits.MaxDeclaredCombinations
        };

        var nodes = await _conditions.GetListAsync(x => x.PromotionId == id);
        dto.Nodes = nodes
            .OrderBy(x => x.Depth).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new PromotionTreeNodeDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                IsGroup = x.IsGroup,
                InteractionType = x.InteractionType,
                RuleSystemName = x.RuleSystemName,
                ConfigJson = x.ConfigJson,
                SortOrder = x.SortOrder,
                Depth = x.Depth
            })
            .ToList();
        return dto;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PagedResultDto<PromotionListItemDto>> GetListAsync(GetPromotionListInput input)
    {
        var ds = await _promotions.GetListAsync(x =>
            (input.Status == null || x.Status == input.Status) && (input.Kind == null || x.Kind == input.Kind));

        var total = ds.Count;
        var items = SapXep(ds, input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(x =>
            {
                var dto = new PromotionListItemDto();
                Map(x, dto);
                return dto;
            })
            .ToList();

        return new PagedResultDto<PromotionListItemDto>(total, items);
    }

    /// <summary>
    /// `D-291` ③ P3 #6 — `?sort=` chỉ nhận **whitelist** trường an toàn (không nội suy SQL thô; dữ liệu đang
    /// phân trang trong bộ nhớ). Cú pháp ABP `"Field [asc|desc]"`; rỗng/trường lạ ⇒ mặc định `CreationTime desc`.
    /// Tie-break `Id` để **tất định** với trường trùng.
    /// </summary>
    private static IEnumerable<Promotion> SapXep(IReadOnlyList<Promotion> ds, string? sorting)
    {
        var (field, desc) = ParseSorting(sorting);
        IOrderedEnumerable<Promotion> ordered = field switch
        {
            "code" => desc ? ds.OrderByDescending(x => x.Code) : ds.OrderBy(x => x.Code),
            "name" => desc ? ds.OrderByDescending(x => x.Name) : ds.OrderBy(x => x.Name),
            "status" => desc ? ds.OrderByDescending(x => x.Status) : ds.OrderBy(x => x.Status),
            "kind" => desc ? ds.OrderByDescending(x => x.Kind) : ds.OrderBy(x => x.Kind),
            "priority" => desc ? ds.OrderByDescending(x => x.Priority) : ds.OrderBy(x => x.Priority),
            "startsatutc" => desc ? ds.OrderByDescending(x => x.StartsAtUtc) : ds.OrderBy(x => x.StartsAtUtc),
            "endsatutc" => desc ? ds.OrderByDescending(x => x.EndsAtUtc) : ds.OrderBy(x => x.EndsAtUtc),
            "creationtime" => desc ? ds.OrderByDescending(x => x.CreationTime) : ds.OrderBy(x => x.CreationTime),
            _ => ds.OrderByDescending(x => x.CreationTime)
        };
        return ordered.ThenBy(x => x.Id);
    }

    private static (string field, bool desc) ParseSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return ("creationtime", true);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
        return (field, desc);
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public async Task<PromotionDto> CreateAsync(CreatePromotionDto input)
    {
        var p = Promotion.TaoMoi(
            _guid.Create(),
            CurrentTenant.Id,
            input.Code,
            input.Name,
            input.Kind,
            EnsureUtc(input.StartsAtUtc),
            input.EndsAtUtc is null ? null : EnsureUtc(input.EndsAtUtc.Value),
            input.Priority,
            input.IsCumulative,
            input.ChannelScope,
            input.DiscountKind,
            input.DiscountValue,
            input.MaxDiscountAmount,
            input.MinOrderAmount,
            input.Currency,
            input.MaxUsageTotal,
            input.MaxUsagePerCustomer);

        await _promotions.InsertAsync(p, autoSave: true);

        var dto = new PromotionDto();
        Map(p, dto);
        return dto;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PromotionEvaluationResultDto> EvaluateAsync(PromotionEvaluateInput input, CancellationToken cancellationToken = default)
    {
        var request = new PromotionEvaluationRequest(
            input.CheckoutSessionId,
            input.Lines,
            input.ChannelCode,
            input.Currency,
            input.Subtotal,
            input.CustomerId,
            input.CustomerGroupId,
            input.IsFirstOrder,
            input.VoucherCode,
            string.IsNullOrWhiteSpace(input.VoucherCode) ? PromotionApplyMode.AutoApply : PromotionApplyMode.CustomerRequested);

        var kq = await _engine.EvaluateAsync(request, cancellationToken);
        return Map(kq);
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public async Task<PromotionTreeValidationResultDto> SaveTreeAsync(Guid id, PromotionTreeInput input)
    {
        // 🚨 `TenantId` của entity PHẢI **khớp** promotion (cột `uuid NULL`): host ⇒ `null`, KHÔNG `Guid.Empty`.
        //    `Guid.Empty` ở host làm ABP filter (`TenantId IS NULL`) **ẩn** node ⇒ `ApproveAsync`/`GetAsync`
        //    đọc cây rỗng ⇒ `0004` dù `save-tree` trả 200 (đã đo bằng probe Host thật N97). Lock key vẫn dùng
        //    giá trị không-null (`?? Guid.Empty`) chỉ để khoá phân tán.
        var tenantId = CurrentTenant.Id;

        await using var giuKhoa = await _lock.TryAcquireAsync(PromotionLockKeys.Tree(tenantId ?? Guid.Empty, id), TimeSpan.FromSeconds(30));
        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("lockKey", PromotionLockKeys.Tree(tenantId ?? Guid.Empty, id));
        }

        var p = await _promotions.GetAsync(id);

        // Cây CHỈ sửa được khi Draft (D-159) — trạng thái khác ⇒ 0006.
        p.TangTreeVersionKhiLuuCay();

        var (nodes, finals) = BuildNodes(input.Nodes, tenantId, id);

        var tenRule = nodes.Where(x => !x.IsGroup).Select(x => x.RuleSystemName!).Distinct(StringComparer.Ordinal).ToList();
        var knownRows = await LoadKnownRowsAsync(tenRule);
        var costs = await LoadCostsAsync(tenRule);

        var limits = await DocLimitsAsync();
        var kq = PromotionTreeValidator.Validate(nodes, limits, _registry.SystemNames, knownRows, costs);

        // Xoá cũ TRƯỚC (sâu nhất trước — FK tự thân Restrict), thêm mới SAU, cùng transaction.
        var cu = await _conditions.GetListAsync(x => x.PromotionId == id);
        foreach (var c in cu.OrderByDescending(x => x.Depth).ThenByDescending(x => x.SortOrder))
        {
            await _conditions.DeleteAsync(c, autoSave: true);
        }

        foreach (var n in finals.OrderBy(x => x.Depth).ThenBy(x => x.SortOrder))
        {
            await _conditions.InsertAsync(n, autoSave: true);
        }

        await _promotions.UpdateAsync(p, autoSave: true);

        return new PromotionTreeValidationResultDto
        {
            NodeCount = kq.NodeCount,
            Depth = kq.Depth,
            LeafCount = kq.LeafCount,
            DeclaredCost = kq.DeclaredCost,
            MaxDeclaredCombinations = kq.MaxDeclaredCombinations,
            AlreadySaved = false
        };
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.Approve)]
    public async Task<PromotionDto> ApproveAsync(Guid id)
    {
        var p = await _promotions.GetAsync(id);

        if (p.Status != PromotionStatus.Draft)
        {
            throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
                .WithData("from", p.Status.ToString())
                .WithData("to", PromotionStatus.Approved.ToString());
        }

        var nodes = await LoadNodesAsync(id);
        var tenRule = nodes.Where(x => !x.IsGroup).Select(x => x.RuleSystemName!).Distinct(StringComparer.Ordinal).ToList();
        var knownRows = await LoadKnownRowsAsync(tenRule);
        var costs = await LoadCostsAsync(tenRule);
        var limits = await DocLimitsAsync();

        PromotionTreeValidator.Validate(nodes, limits, _registry.SystemNames, knownRows, costs);

        p.Approve();
        await _promotions.UpdateAsync(p, autoSave: true);

        var dto = new PromotionDto();
        Map(p, dto);
        return dto;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public async Task<PromotionDto> SetStatusAsync(Guid id, SetPromotionStatusInput input)
    {
        // Chỉ nhận Paused/Active/Cancelled. Approved/Expired ⇒ 0006 (chỉ ApproveAsync/job).
        if (input.Status is PromotionStatus.Approved or PromotionStatus.Expired)
        {
            throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
                .WithData("to", input.Status.ToString())
                .WithData("lyDo", "Chỉ ApproveAsync (Approved) / job (Expired) mới chuyển tới trạng thái này.");
        }

        var p = await _promotions.GetAsync(id);
        var now = _clock.Now.ToUniversalTime();

        switch (input.Status)
        {
            case PromotionStatus.Paused:
                p.Pause();
                break;

            case PromotionStatus.Active:
                if (p.Status == PromotionStatus.Approved)
                {
                    p.ActivateManual(now);
                }
                else if (p.Status == PromotionStatus.Paused)
                {
                    p.Resume(now);
                }
                else
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
                        .WithData("from", p.Status.ToString())
                        .WithData("to", PromotionStatus.Active.ToString());
                }

                break;

            case PromotionStatus.Cancelled:
                p.Cancel();
                break;

            default:
                throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
                    .WithData("to", input.Status.ToString());
        }

        await _promotions.UpdateAsync(p, autoSave: true);

        var dto = new PromotionDto();
        Map(p, dto);
        return dto;
    }

    /// <summary>
    /// Không side effect (không trừ lượt). **KHÔNG** cho phép ẩn danh (Q7 — `D-146`).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<VoucherValidationDto> ValidateVoucherAsync(ValidateVoucherInput input, CancellationToken cancellationToken = default)
    {
        var request = new PromotionEvaluationRequest(
            input.CheckoutSessionId,
            input.Lines,
            input.ChannelCode,
            input.Currency,
            input.Subtotal,
            input.CustomerId,
            input.CustomerGroupId,
            input.IsFirstOrder,
            input.Code,
            PromotionApplyMode.CustomerRequested);

        PromotionEvaluationResult kq;
        try
        {
            kq = await _engine.EvaluateAsync(request, cancellationToken);
        }
        catch (BusinessException ex) when (ex.Code == PromotionErrorCodes.VoucherNotFound)
        {
            // `D-291` ⑥ — mã **không tồn tại** ⇒ `IsValid=false` (không ném ra biên validate), kể cả khi có
            // một khuyến mãi tự động khác khớp.
            return new VoucherValidationDto
            {
                IsValid = false,
                PromotionId = null,
                DiscountTotal = 0m,
                Currency = input.Currency,
                RejectionReason = PromotionRejectionReason.NoCandidate
            };
        }

        // `D-291` ② P2 #3 — chỉ đọc kết quả RIÊNG của voucher (`VoucherOutcome`), KHÔNG lấy
        // `Breakdown.FirstOrDefault(Source=="Internal")` (có thể trỏ item tự động/bị cắt). `IsValid` true
        // **chỉ khi** chính voucher được nhận.
        var voucher = kq.VoucherOutcome;
        var voucherDuocNhan = voucher is { Applied: true };
        return new VoucherValidationDto
        {
            IsValid = voucherDuocNhan,
            PromotionId = voucherDuocNhan ? voucher!.PromotionId : null,
            DiscountTotal = voucherDuocNhan ? voucher!.DiscountTotal : 0m,
            Currency = kq.Currency,
            RejectionReason = voucherDuocNhan
                ? null
                : (voucher?.RejectionReason ?? kq.RejectionReason ?? PromotionRejectionReason.NoCandidate)
        };
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PagedResultDto<RedemptionDto>> GetRedemptionsAsync(Guid orderId)
    {
        var ds = await _redemptions.GetListAsync(x => x.OrderId == orderId);
        var items = ds
            .OrderBy(x => x.EvaluatedAtUtc)
            .Select(x => new RedemptionDto
            {
                Id = x.Id,
                PromotionId = x.PromotionId,
                VoucherCodeId = x.VoucherCodeId,
                OrderId = x.OrderId,
                Status = x.Status,
                UsageSequence = x.UsageSequence,
                ReasonCode = x.ReasonCode,
                CustomerId = x.CustomerId,
                DiscountAmount = x.DiscountAmount,
                Currency = x.Currency,
                TreeVersion = x.TreeVersion,
                EvaluatedAtUtc = x.EvaluatedAtUtc
            })
            .ToList();

        return new PagedResultDto<RedemptionDto>(items.Count, items);
    }

    /// <summary>
    /// Import bảng chi phí rule — **idempotent** (`D-154`): chỉ **cập nhật** dòng đã có; tên ∉ registry ⇒ `0004`;
    /// **không** tạo/xoá dòng. Định dạng CSV: `RuleSystemName,CostKind,DeclaredCostFactor,RequiresVerifiedCod`.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Promotions.Import)]
    public async Task<ImportResultDto> ImportRuleCostsAsync(ImportRuleCostsInput input)
    {
        var tenantId = CurrentTenant.Id ?? Guid.Empty;

        await using var giuKhoa = await _lock.TryAcquireAsync(PromotionLockKeys.Import(tenantId), TimeSpan.FromSeconds(120));
        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("lockKey", PromotionLockKeys.Import(tenantId));
        }

        var daCapNhat = 0;
        var boQua = 0;

        var dong = (input.FileContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var raw in dong)
        {
            if (raw.StartsWith("RuleSystemName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cot = raw.Split(',', StringSplitOptions.TrimEntries);
            if (cot.Length < 4)
            {
                boQua++;
                continue;
            }

            var ten = cot[0];
            if (!_registry.Contains(ten))
            {
                throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                    .WithData("SystemName", ten)
                    .WithData("lyDo", "Tên rule trong tệp KHÔNG có trong registry.");
            }

            if (!int.TryParse(cot[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var factor) || factor < 1)
            {
                boQua++;
                continue;
            }

            var existing = await _ruleCosts.FindAsync(ten);
            if (existing is null)
            {
                // D-154: import KHÔNG tạo dòng cho tên chưa có — bỏ qua.
                boQua++;
                continue;
            }

            existing.CapNhatTheoRegistry(cot[1], factor, bool.TryParse(cot[3], out var cod) && cod);
            await _ruleCosts.UpdateAsync(existing, autoSave: true);
            daCapNhat++;
        }

        return new ImportResultDto { CapNhat = daCapNhat, BoQua = boQua, AlreadyImported = false };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private (List<PromotionConditionNode> Nodes, List<PromotionCondition> Finals) BuildNodes(
        IReadOnlyList<PromotionTreeNodeInput> input, Guid? tenantId, Guid promotionId)
    {
        var finals = new List<Guid>(input.Count);
        var providedToFinal = new Dictionary<Guid, Guid>();

        for (var i = 0; i < input.Count; i++)
        {
            var id = input[i].Id ?? _guid.Create();
            finals.Add(id);
            if (input[i].Id is Guid pid)
            {
                providedToFinal[pid] = id;
            }
        }

        var cha = new Guid?[input.Count];
        for (var i = 0; i < input.Count; i++)
        {
            var parent = input[i].ParentId;
            cha[i] = parent is null
                ? null
                : providedToFinal.TryGetValue(parent.Value, out var f) ? f : parent.Value;
        }

        var indexTheoId = finals.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        var sau = new int[input.Count];
        for (var i = 0; i < input.Count; i++)
        {
            var depth = 1;
            var buoc = 0;
            var cur = cha[i];
            while (cur is not null)
            {
                depth++;
                if (++buoc > input.Count) // chu trình — validator sẽ ném 0003
                {
                    break;
                }

                cur = indexTheoId.TryGetValue(cur.Value, out var idx) ? cha[idx] : null;
            }

            sau[i] = depth;
        }

        var nodes = new List<PromotionConditionNode>(input.Count);
        var entities = new List<PromotionCondition>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            var n = input[i];
            nodes.Add(new PromotionConditionNode(
                finals[i], cha[i], n.IsGroup, n.InteractionType, n.RuleSystemName, n.SortOrder, sau[i]));

            entities.Add(PromotionCondition.TaoMoi(
                finals[i], tenantId, promotionId, cha[i], n.IsGroup, n.InteractionType, n.RuleSystemName, n.ConfigJson, n.SortOrder, sau[i]));
        }

        return (nodes, entities);
    }

    private async Task<List<PromotionConditionNode>> LoadNodesAsync(Guid promotionId)
    {
        var ds = await _conditions.GetListAsync(x => x.PromotionId == promotionId);
        return ds.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new PromotionConditionNode(x.Id, x.ParentId, x.IsGroup, x.InteractionType, x.RuleSystemName, x.SortOrder, x.Depth))
            .ToList();
    }

    private async Task<IReadOnlySet<string>> LoadKnownRowsAsync(IReadOnlyCollection<string> names)
    {
        if (names.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ds = await _ruleCosts.GetListAsync(x => names.Contains(x.Id));
        return ds.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadCostsAsync(IReadOnlyCollection<string> names)
    {
        if (names.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var ds = await _ruleCosts.GetListAsync(x => names.Contains(x.Id));
        return ds.ToDictionary(x => x.Id, x => x.DeclaredCostFactor, StringComparer.Ordinal);
    }

    private async Task<PromotionLimits> DocLimitsAsync()
    {
        var limits = PromotionSettings.Doc(
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxNodes),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxDepth),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxLeaves),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxDeclaredCombinations),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxCandidatesPerCart),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.LeafVisitBudget),
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.ComparisonBudget),
            out _);

        return limits;
    }

    private static void Map(Promotion p, PromotionDto dto)
    {
        dto.Id = p.Id;
        dto.Code = p.Code;
        dto.Name = p.Name;
        dto.Kind = p.Kind;
        dto.Status = p.Status;
        dto.IsCumulative = p.IsCumulative;
        dto.Priority = p.Priority;
        dto.ChannelScope = p.ChannelScope;
        dto.StartsAtUtc = p.StartsAtUtc;
        dto.EndsAtUtc = p.EndsAtUtc;
        dto.TreeVersion = p.TreeVersion;
        dto.MaxUsageTotal = p.MaxUsageTotal;
        dto.UsedCount = p.UsedCount;
        dto.ReservedCount = p.ReservedCount;
        dto.MaxUsagePerCustomer = p.MaxUsagePerCustomer;
        dto.DiscountKind = p.DiscountKind;
        dto.DiscountValue = p.DiscountValue;
        dto.MaxDiscountAmount = p.MaxDiscountAmount;
        dto.MinOrderAmount = p.MinOrderAmount;
        dto.Currency = p.Currency;
    }

    private static PromotionEvaluationResultDto Map(PromotionEvaluationResult r)
        => new()
        {
            Applied = r.Applied,
            DiscountTotal = r.DiscountTotal,
            Currency = r.Currency,
            Breakdown = r.Breakdown.ToList(),
            RejectionReason = r.RejectionReason,
            LeafVisits = r.LeafVisits,
            Comparisons = r.Comparisons
        };
}
