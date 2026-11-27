namespace eCommerce.Promotions;

/// <summary>
/// **KHOÁ PHÂN TÁN CỦA MIỀN KHUYẾN MÃI** (`PRO-002` §3.7 / §8.2) — một chỗ duy nhất.
///
/// 🚨 **CẤM tự thêm tiền tố `ecommerce:`** — `AbpDistributedLockOptions.KeyPrefix` đã thêm (bài học C11/C13).
/// Tên khoá do **module** sở hữu (quy ước `XC-003`), dạng **không tiền tố** `promotion:…`.
/// </summary>
public static class PromotionLockKeys
{
    /// <summary>2 quản trị cùng sửa cây của một promotion — TTL 30 s.</summary>
    public static string Tree(Guid tenantId, Guid promotionId)
        => $"promotion:tree:{tenantId}:{promotionId}";

    /// <summary>Giảm tranh chấp khi nhiều người dùng một mã "hot" — TTL 10 s (DB vẫn là chốt cuối).</summary>
    public static string VoucherUsage(Guid tenantId, string code)
        => $"promotion:voucher:{tenantId}:{code}";

    /// <summary>Job kích hoạt + hết hạn chạy chồng trên nhiều instance — TTL 300 s.</summary>
    public static string Job(string jobName, Guid? tenantId)
        => $"promotion:job:{jobName}:{Host(tenantId)}";

    /// <summary>2 người cùng import bảng chi phí rule — TTL 120 s.</summary>
    public static string Import(Guid tenantId)
        => $"promotion:import:{tenantId}";

    /// <summary>Host dùng `"host"` — KHÔNG nối `null` thành chuỗi rỗng gây va khoá giữa host và tenant.</summary>
    private static string Host(Guid? tenantId) => tenantId?.ToString() ?? "host";
}

/// <summary>
/// **KHOÁ IDEMPOTENCY** của các biên vào **có ghi** (`PRO-002` §7.2). `evaluate` **không** có khoá (chỉ đọc, `D-159`).
/// </summary>
public static class PromotionIdempotencyKeys
{
    /// <summary>`PromotionTree:{tenantId}:{promotionId}:{TreeVersion}:{sha256(treeJson)}` (§7.2 #2).</summary>
    public static string Tree(Guid? tenantId, Guid promotionId, int treeVersion, string treeSha256)
        => $"PromotionTree:{Host(tenantId)}:{promotionId}:{treeVersion}:{treeSha256}";

    /// <summary>`Import:{tenantId}:{sha256(fileContent)}` (`XC-002` §7.3).</summary>
    public static string Import(Guid? tenantId, string contentSha256)
        => $"Import:{Host(tenantId)}:{contentSha256}";

    /// <summary>`PromotionAuto:{tenantId}:{orderId}:{promotionId}` (§7.2 #1b).</summary>
    public static string Auto(Guid? tenantId, Guid orderId, Guid promotionId)
        => $"PromotionAuto:{Host(tenantId)}:{orderId}:{promotionId}";

    /// <summary>`PromotionRefund:{tenantId}:{orderId}:{promotionId}` (§7.2 #4).</summary>
    public static string Refund(Guid? tenantId, Guid orderId, Guid promotionId)
        => $"PromotionRefund:{Host(tenantId)}:{orderId}:{promotionId}";

    /// <summary>`Checkout:{tenantId}:{checkoutSessionId}:{code}` — chỉ `ReserveAsync` dùng (`D-159`, `PRO-001` §3.5).</summary>
    public static string Checkout(Guid? tenantId, string checkoutSessionId, string code)
        => $"Checkout:{Host(tenantId)}:{checkoutSessionId}:{code}";

    /// <summary>`PromotionCommit:{tenantId}:{orderId}:{code}` — chốt lượt trong UoW tạo đơn (`PRO-001` §3.5).</summary>
    public static string Commit(Guid? tenantId, Guid orderId, string code)
        => $"PromotionCommit:{Host(tenantId)}:{orderId}:{code}";

    /// <summary>`PromotionReverse:{tenantId}:{orderId}:{promotionId}` — trả hàng (`PRO-001` §3.5, `D-148`).</summary>
    public static string Reverse(Guid? tenantId, Guid orderId, Guid promotionId)
        => $"PromotionReverse:{Host(tenantId)}:{orderId}:{promotionId}";

    private static string Host(Guid? tenantId) => tenantId?.ToString() ?? "host";
}
