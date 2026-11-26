namespace eCommerce.Reporting;

/// <summary>
/// **KHOÁ PHÂN TÁN CỦA MIỀN BÁO CÁO** — một chỗ duy nhất (`REP-001` §3.7).
///
/// 🚨 **CẤM tự thêm tiền tố `ecommerce:`** — `AbpDistributedLockOptions.KeyPrefix` đã thêm (bài học
/// C11/C13: tiền tố bị nhân đôi). `TongHop` giữ **nguyên** giá trị as-built `report:tong-hop`.
/// </summary>
public static class ReportingLockKeys
{
    /// <summary>Lượt tổng hợp định kỳ toàn cục — giữ as-built (`TryAcquire` 2 s ở `TongHopBaoCaoService`).</summary>
    public const string TongHop = "report:tong-hop";

    /// <summary>Khoá rebuild theo `(tenant, projection)` — worker giữ 30 s; KHÔNG chặn job toàn cục.</summary>
    public static string Rebuild(Guid? tenantId, RepProjection projection)
        => $"report:rebuild:{Ma(tenantId)}:{projection.MaChuoi()}";

    /// <summary>Khoá chốt/mở kỳ theo tenant.</summary>
    public static string PeriodClose(Guid? tenantId)
        => $"report:period-close:{Ma(tenantId)}";

    /// <summary>Khoá đối chiếu theo tenant.</summary>
    public static string Reconciliation(Guid? tenantId)
        => $"report:reconciliation:{Ma(tenantId)}";

    /// <summary>Host dùng `"host"` — KHÔNG nối `null` thành chuỗi rỗng gây va khoá giữa host và tenant.</summary>
    private static string Ma(Guid? tenantId) => tenantId?.ToString() ?? "host";
}
