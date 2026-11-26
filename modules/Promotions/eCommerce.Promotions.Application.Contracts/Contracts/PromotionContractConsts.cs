namespace eCommerce.Promotions.Contracts;

/// <summary>
/// Hằng số hợp đồng dùng chung cho bên gọi (`Sales`) — để `Sales` **không** phải tham chiếu
/// `Promotions.Domain` (ranh giới M2).
/// </summary>
public static class PromotionContractConsts
{
    /// <summary>Tên setting bật/tắt module ở đường `Sales` (`Promotions:Enabled`). Cờ tắt ⇒ không gọi engine.</summary>
    public const string EnabledSettingName = "Promotions:Enabled";

    /// <summary>Nhãn `Source` của breakdown ở v1 (chỉ khuyến mãi của ta — `Q9`).</summary>
    public const string SourceInternal = "Internal";
}
