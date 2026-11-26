using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;

namespace eCommerce.Promotions;

/// <summary>
/// **Aggregate root `Promotion`** (`PRO-002` §3.2, bảng `ProPromotions`).
///
/// 🔒 Bất biến:
///   • **Máy trạng thái NÉM `0006`** ở mọi chuyển sai (`BR-15`, `D-6`); hai hàm `*ByJob` **khoan dung** theo
///     SRS §3.2 (chưa tới giờ ⇒ **giữ nguyên**, không phải lỗi).
///   • **Không có đường sửa** trường giá trị/giới hạn (`D-150`): chỉ đặt ở factory; `IPromotionAppService`
///     **không** có `UpdateAsync`.
///   • **Cây bất biến từ `Approved`** (`D-159`): `TangTreeVersionDraft` chỉ chạy ở `Draft`.
///   • Tiền là `decimal` + `Currency`; **cấm** `double`/`float`.
/// </summary>
public class Promotion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    private static readonly Regex MaTienTe = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);

    public Guid? TenantId { get; protected set; }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public PromotionKind Kind { get; private set; }
    public PromotionStatus Status { get; private set; }
    public bool IsCumulative { get; private set; }
    public int Priority { get; private set; }
    public string? ChannelScope { get; private set; }

    public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }

    public int TreeVersion { get; private set; }
    public int? MaxUsageTotal { get; private set; }
    public int UsedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public int? MaxUsagePerCustomer { get; private set; }

    public PromotionDiscountKind DiscountKind { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal? MaxDiscountAmount { get; private set; }
    public decimal? MinOrderAmount { get; private set; }
    public string Currency { get; private set; } = default!;

    /// <summary>Khoá lạc quan (`xmin` của PostgreSQL — không tạo cột thật).</summary>
    public byte[]? RowVersion { get; private set; }

    protected Promotion()
    {
    }

    /// <summary>
    /// Tạo promotion ở `Draft`. Vi phạm `CHECK` §5.3 (`FlashSale`, `FreeShipping`, `%` &gt; 100 / thiếu trần,
    /// `Currency` sai, `Ends ≤ Starts`) ⇒ **validation 400**, 0 mã lỗi mới (`D-156`/`D-158`).
    /// </summary>
    public static Promotion TaoMoi(
        Guid id,
        Guid? tenantId,
        string code,
        string name,
        PromotionKind kind,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        int priority,
        bool isCumulative,
        string? channelScope,
        PromotionDiscountKind discountKind,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal? minOrderAmount,
        string currency,
        int? maxUsageTotal,
        int? maxUsagePerCustomer)
    {
        var loi = new List<ValidationResult>();

        if (kind == PromotionKind.FlashSale)
        {
            loi.Add(new ValidationResult("FlashSale chưa hỗ trợ ở v1 (CR-P2)."));
        }

        if (discountKind == PromotionDiscountKind.FreeShipping)
        {
            loi.Add(new ValidationResult("FreeShipping chưa hỗ trợ ở v1 (CR-S2)."));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > PromotionConsts.CodeToiDa)
        {
            loi.Add(new ValidationResult($"Mã khuyến mãi bắt buộc, tối đa {PromotionConsts.CodeToiDa} ký tự."));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > PromotionConsts.NameToiDa)
        {
            loi.Add(new ValidationResult($"Tên bắt buộc, tối đa {PromotionConsts.NameToiDa} ký tự."));
        }

        if (string.IsNullOrWhiteSpace(currency) || !MaTienTe.IsMatch(currency))
        {
            loi.Add(new ValidationResult("Currency phải là 3 chữ HOA (ISO-4217)."));
        }

        if (discountValue < 0m)
        {
            loi.Add(new ValidationResult("DiscountValue phải ≥ 0."));
        }

        if (discountKind == PromotionDiscountKind.Percentage
            && (discountValue > 100m || maxDiscountAmount is null))
        {
            loi.Add(new ValidationResult("Percentage phải ≤ 100 và BẮT BUỘC có MaxDiscountAmount."));
        }

        if (endsAtUtc is not null && endsAtUtc <= startsAtUtc)
        {
            loi.Add(new ValidationResult("EndsAtUtc phải SAU StartsAtUtc."));
        }

        if (maxUsageTotal is < 0 || maxUsagePerCustomer is < 0)
        {
            loi.Add(new ValidationResult("Trần lượt phải ≥ 0."));
        }

        if (loi.Count > 0)
        {
            throw new AbpValidationException(loi);
        }

        return new Promotion
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            Kind = kind,
            Status = PromotionStatus.Draft,
            IsCumulative = isCumulative,
            Priority = priority,
            ChannelScope = string.IsNullOrWhiteSpace(channelScope) ? null : channelScope.Trim(),
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            TreeVersion = 0,
            MaxUsageTotal = maxUsageTotal,
            UsedCount = 0,
            ReservedCount = 0,
            MaxUsagePerCustomer = maxUsagePerCustomer,
            DiscountKind = discountKind,
            DiscountValue = discountValue,
            MaxDiscountAmount = maxDiscountAmount,
            MinOrderAmount = minOrderAmount,
            Currency = currency
        };
    }

    // ── Máy trạng thái ────────────────────────────────────────────────────────────────────────────

    /// <summary>`Draft → Approved` — chỉ qua `ApproveAsync` (guard cây đã chạy trước đó). Sai ⇒ `0006`.</summary>
    public void Approve()
    {
        if (Status != PromotionStatus.Draft)
        {
            NemChuyenSai(PromotionStatus.Approved);
        }

        Status = PromotionStatus.Approved;
    }

    /// <summary>
    /// `Approved → Active` **kích hoạt TAY** (`SetStatusAsync`). Phải `Now ≥ StartsAtUtc` và chưa quá `EndsAtUtc`;
    /// sai ⇒ **NÉM** `0006` (`TC-50` (vi)) — **cấm** bỏ qua im lặng.
    /// </summary>
    public void ActivateManual(DateTime nowUtc)
    {
        if (Status != PromotionStatus.Approved)
        {
            NemChuyenSai(PromotionStatus.Active);
        }

        if (nowUtc < StartsAtUtc.ToUniversalTime()
            || (EndsAtUtc is not null && nowUtc >= EndsAtUtc.Value.ToUniversalTime()))
        {
            NemChuyenSai(PromotionStatus.Active, "Ngoài khung hiệu lực (chưa tới StartsAtUtc hoặc đã quá EndsAtUtc).");
        }

        Status = PromotionStatus.Active;
    }

    /// <summary>
    /// **JOB** `Approved → Active` — khoan dung theo SRS §3.2: trạng thái khác hoặc chưa tới giờ ⇒ **không đổi**
    /// (trả `false`), **không** phải lỗi (`TC-50` (i)/(iii)/(vii)).
    /// </summary>
    public bool ActivateByJob(DateTime nowUtc)
    {
        if (Status != PromotionStatus.Approved)
        {
            return false;
        }

        var now = nowUtc.ToUniversalTime();
        if (now < StartsAtUtc.ToUniversalTime()
            || (EndsAtUtc is not null && now >= EndsAtUtc.Value.ToUniversalTime()))
        {
            return false;
        }

        Status = PromotionStatus.Active;
        return true;
    }

    /// <summary>`Active → Paused`. Sai ⇒ `0006`.</summary>
    public void Pause()
    {
        if (Status != PromotionStatus.Active)
        {
            NemChuyenSai(PromotionStatus.Paused);
        }

        Status = PromotionStatus.Paused;
    }

    /// <summary>`Paused → Active` (chưa quá `EndsAtUtc`). Sai ⇒ `0006`.</summary>
    public void Resume(DateTime nowUtc)
    {
        if (Status != PromotionStatus.Paused)
        {
            NemChuyenSai(PromotionStatus.Active);
        }

        if (EndsAtUtc is not null && nowUtc.ToUniversalTime() >= EndsAtUtc.Value.ToUniversalTime())
        {
            NemChuyenSai(PromotionStatus.Active, "Đã quá EndsAtUtc — không thể tiếp tục.");
        }

        Status = PromotionStatus.Active;
    }

    /// <summary>
    /// **JOB** `Active`/`Paused → Expired` khi `Now > EndsAtUtc`; chưa tới hạn ⇒ **giữ nguyên** (trả `false`).
    /// </summary>
    public bool ExpireByJob(DateTime nowUtc)
    {
        if (Status is not (PromotionStatus.Active or PromotionStatus.Paused))
        {
            return false;
        }

        if (EndsAtUtc is null || nowUtc.ToUniversalTime() <= EndsAtUtc.Value.ToUniversalTime())
        {
            return false;
        }

        Status = PromotionStatus.Expired;
        return true;
    }

    /// <summary>`Draft`/`Approved`/`Active → Cancelled`. `Expired`/`Cancelled` là **hấp thụ** ⇒ `0006`.</summary>
    public void Cancel()
    {
        if (Status is not (PromotionStatus.Draft or PromotionStatus.Approved or PromotionStatus.Active))
        {
            NemChuyenSai(PromotionStatus.Cancelled);
        }

        Status = PromotionStatus.Cancelled;
    }

    /// <summary>
    /// Đánh dấu lưu cây thành công ở `Draft` ⇒ `TreeVersion++`. Trạng thái khác ⇒ **NÉM** `0006`
    /// (`D-159`: cây bất biến từ `Approved`; `TC-25` (e)).
    /// </summary>
    public void TangTreeVersionKhiLuuCay()
    {
        if (Status != PromotionStatus.Draft)
        {
            throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
                .WithData("from", Status.ToString())
                .WithData("to", "SaveTree")
                .WithData("lyDo", "Chỉ được sửa cây khi Draft (cây bất biến từ Approved).");
        }

        TreeVersion++;
    }

    private void NemChuyenSai(PromotionStatus den, string? lyDo = null)
    {
        var loi = new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
            .WithData("from", Status.ToString())
            .WithData("to", den.ToString());

        if (lyDo is not null)
        {
            loi.WithData("lyDo", lyDo);
        }

        throw loi;
    }
}
