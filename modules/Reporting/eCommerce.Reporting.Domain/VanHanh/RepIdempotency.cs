using System.Security.Cryptography;
using System.Text;

namespace eCommerce.Reporting;

/// <summary>
/// **KHOÁ IDEMPOTENCY CỦA VẬN HÀNH** (`REP-001` §5.6) — một chỗ duy nhất để test được **thuần**.
///
/// 🚨 `tenantId` host ⇒ `"host"` (KHÔNG nối `null` thành chuỗi rỗng gây va khoá giữa host và tenant).
/// `sha256` = `SHA256.HashData(UTF8(reason.Trim()))` → hex thường (BCL, 0 gói mới).
/// </summary>
public static class RepIdempotency
{
    /// <summary>Rebuild do người dùng: cùng khoảng + cùng lý do ⇒ **cùng** khoá.</summary>
    public static string Rebuild(RepProjection projection, Guid? tenantId, DateOnly from, DateOnly to, string reason)
        => $"Report:Rebuild:{projection.MaChuoi()}:{Ma(tenantId)}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{Bam(reason.Trim())}";

    /// <summary>Chốt kỳ: cùng khoảng ⇒ **cùng** khoá (tái dùng dòng khi mở lại rồi chốt lại).</summary>
    public static string PeriodClose(Guid? tenantId, DateOnly from, DateOnly to)
        => $"Report:PeriodClose:{Ma(tenantId)}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}";

    /// <summary>Đối chiếu: khoá theo **lượt** (`runId`) ⇒ chạy lại cùng khoảng **không** `unique_violation`.</summary>
    public static string Reconcile(Guid? tenantId, RepProjection projection, Guid runId)
        => $"Report:Reconcile:{Ma(tenantId)}:{projection.MaChuoi()}:{runId}";

    /// <summary>Job tổng hợp định kỳ: khoá theo **lượt** (`runId`), mỗi projection một dòng.</summary>
    public static string AggregateRun(Guid? tenantId, RepProjection projection, Guid runId)
        => $"Report:AggregateRun:{Ma(tenantId)}:{projection.MaChuoi()}:{runId}";

    private static string Ma(Guid? tenantId) => tenantId?.ToString() ?? "host";

    private static string Bam(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
