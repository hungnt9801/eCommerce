namespace eCommerce.Promotions;

/// <summary>
/// Hằng số độ dài/giới hạn của module (`PRO-002` §5.2). Cột DB dùng các độ dài này.
/// </summary>
public static class PromotionConsts
{
    public const int CodeToiDa = 64;
    public const int NameToiDa = 256;
    public const int KindToiDa = 32;
    public const int StatusToiDa = 32;
    public const int DiscountKindToiDa = 16;
    public const int ChannelScopeToiDa = 64;
    public const int InteractionTypeToiDa = 8;
    public const int RuleSystemNameToiDa = 128;
    public const int CostKindToiDa = 16;
    public const int VoucherStatusToiDa = 16;
    public const int TargetTypeToiDa = 16;
    public const int ModeToiDa = 16;
    public const int ReasonCodeToiDa = 32;
    public const int IdempotencyKeyToiDa = 256;
    public const int CurrencyToiDa = 3;
    public const int SkuToiDa = 64;

    /// <summary>Trần **cứng** của độ sâu (khớp `CHECK ("Depth" BETWEEN 1 AND 8)` — `ADR-03`).</summary>
    public const int DoSauCung = 8;
}
