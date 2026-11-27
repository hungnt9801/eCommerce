namespace eCommerce.Promotions;

/// <summary>
/// **MÃ LỖI CỦA MIỀN KHUYẾN MÃI** (`PRO-002` §6.4, dải `eCommerce:Promotion:0001…0099` theo `D-144`).
///
/// 📌 Module **tự sở hữu** hằng số của mình (mẫu `TaxErrorCodes`): KHÔNG thêm `eCommerce:Promotion:*` vào
/// `eCommerceConsts.ErrorCodes`. Mọi mã **phải** được map sang HTTP ở Host — nếu không ABP trả `403` và
/// client đọc thành *"tôi không có quyền"* trong khi lỗi thật là *"cây sai"* / *"hết lượt"*.
/// </summary>
public static class PromotionErrorCodes
{
    /// <summary>Vượt trần **node / độ sâu / lá**. HTTP `400`.</summary>
    public const string TreeTooLarge = "eCommerce:Promotion:0001";

    /// <summary>Vượt `MaxDeclaredCombinations`. HTTP `400`.</summary>
    public const string CombinationExplosion = "eCommerce:Promotion:0002";

    /// <summary>`ParentId` tạo chu trình. HTTP `400`.</summary>
    public const string ConditionCycleDetected = "eCommerce:Promotion:0003";

    /// <summary>Node lai · gốc ≠ 1 · `SystemName` **chưa từng đăng ký** (không có dòng ở `ProPromotionRuleCosts`). HTTP `400`.</summary>
    public const string InvalidConditionNode = "eCommerce:Promotion:0004";

    /// <summary>Vượt ngân sách chạy ở candidate của **mã khách nhập**. HTTP `422`.</summary>
    public const string EvaluationBudgetExceeded = "eCommerce:Promotion:0005";

    /// <summary>Chuyển trạng thái sai (`BR-15`). HTTP `409`.</summary>
    public const string InvalidStatusTransition = "eCommerce:Promotion:0006";

    /// <summary>Hết lượt **mức mã** (`ProVoucherCodes`). HTTP `409`.</summary>
    public const string UsageLimitExceeded = "eCommerce:Promotion:0007";

    /// <summary>Rule cần COD **đã đối soát** nhưng chưa có. HTTP `422`.</summary>
    public const string UnverifiedCodCondition = "eCommerce:Promotion:0008";

    /// <summary>Mã khách nhập làm `DiscountTotal` &gt; `Subtotal`. HTTP `400`.</summary>
    public const string DiscountExceedsOrder = "eCommerce:Promotion:0009";

    /// <summary>`SystemName` **có dòng** ở `ProPromotionRuleCosts` nhưng không còn trong registry. HTTP `400`.</summary>
    public const string RuleNotRegistered = "eCommerce:Promotion:0010";

    // ── Mã thuộc dải `01xx` của `PRO-001` (`D-144`/`D-147`), dùng ở ENGINE `PRO-002` khi nguồn candidate là
    //    MÃ khách nhập (`D-291` ② P2 #4: "hết lượt ⇒ NÉM đúng mã đã cấp; cấm đặt mã mới"). Chỉ KHAI hằng ở
    //    class MỘT của miền; map HTTP để `PRO-001` đặt ở WP của nó (không nhân đôi tại `PRO-002`).

    /// <summary>Mã không tồn tại (mã `PRO-001` `0101`, `D-144`). HTTP `404`.</summary>
    public const string VoucherNotFound = "eCommerce:Promotion:0101";

    /// <summary>Vượt `MaxUsagePerCustomer` của mã (mã `PRO-001` `0104`, `D-144`). HTTP `409`.</summary>
    public const string CustomerUsageLimitExceeded = "eCommerce:Promotion:0104";

    /// <summary>Hết lượt **mức promotion** (`MaxUsageTotal`; mã `PRO-001` `0119`, `D-147`). HTTP `409`.</summary>
    public const string PromotionUsageLimitExceeded = "eCommerce:Promotion:0119";

    // ── `W25-2` (`N101`) — 14 hằng còn thiếu của dải `01xx` (`PRO-001-arch` §1.2). `0109`/`0113` **giữ số,
    //    không hằng** (`D-296`/`D-158`). Thêm **chỉ ở cuối dải** (`D-144`).

    /// <summary>Mã ngoài cửa sổ hiệu lực của promotion (`0102`). HTTP `400`.</summary>
    public const string VoucherOutOfWindow = "eCommerce:Promotion:0102";

    /// <summary>Mã đã bị thu hồi (`0103`). HTTP `409`.</summary>
    public const string VoucherRevoked = "eCommerce:Promotion:0103";

    /// <summary>Lượt không còn `Reserved` khi chốt/nhả/đảo (`0105`). HTTP `409`.</summary>
    public const string RedemptionNotReserved = "eCommerce:Promotion:0105";

    /// <summary>Mã đã phát hành — không sửa giá trị/giới hạn (`0106`, v1 chưa có đường ném). HTTP `409`.</summary>
    public const string VoucherImmutable = "eCommerce:Promotion:0106";

    /// <summary>`Code` sai định dạng sau chuẩn hoá (`0107`). HTTP `400`.</summary>
    public const string VoucherCodeInvalid = "eCommerce:Promotion:0107";

    /// <summary>Lô sinh vượt `MaxBatchSize` (`0108`). HTTP `400`.</summary>
    public const string BatchSizeExceeded = "eCommerce:Promotion:0108";

    /// <summary>Áp voucher nội bộ cho đơn không thuộc kênh `web` (`0110`). HTTP `400`.</summary>
    public const string VoucherChannelNotAllowed = "eCommerce:Promotion:0110";

    /// <summary>`Σ DiscountAmount` không khớp `Order.DiscountTotal` (`0111`). HTTP `500`.</summary>
    public const string RedemptionSumMismatch = "eCommerce:Promotion:0111";

    /// <summary>Thiếu ngữ cảnh tenant (`Guid.Empty`) (`0112`). HTTP `400`.</summary>
    public const string MissingTenantContext = "eCommerce:Promotion:0112";

    /// <summary>Tạo/phát hành mã cho promotion chưa `Approved` (`0114`). HTTP `409`.</summary>
    public const string PromotionNotApproved = "eCommerce:Promotion:0114";

    /// <summary>Chốt mã thứ hai cho cùng một đơn (`0115`). HTTP `409`.</summary>
    public const string SecondVoucherOnOrder = "eCommerce:Promotion:0115";

    /// <summary>`OverrideLimit` đặt trần mới thấp hơn `UsedCount + ReservedCount` (`0116`). HTTP `409`.</summary>
    public const string OverrideLimitTooLow = "eCommerce:Promotion:0116";

    /// <summary>Khoá idempotency trùng với lượt đã `Released` (`0117`). HTTP `409`.</summary>
    public const string ReservationReleasedReplay = "eCommerce:Promotion:0117";

    /// <summary>`ReserveAsync` với `CustomerId = NULL` — khách vãng lai (`0118`). HTTP `400`.</summary>
    public const string CustomerRequiredForVoucher = "eCommerce:Promotion:0118";
}
