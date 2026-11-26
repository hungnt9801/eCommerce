namespace eCommerce.Promotions;

/// <summary>
/// Enum của module <b>Promotions</b> (`PRO-002` §3.1).
///
/// 🚨 **Enum lưu DB chỉ được THÊM Ở CUỐI dải** (`D-17`). `PromotionKind`/`PromotionStatus`/`PromotionDiscountKind`/
/// `PromotionConditionGroupType` được map sang **chuỗi** ở EF (xem `PRO-002-arch` §12.1-D1): `CHECK` đọc được
/// giá trị và DB giữ đúng tên nghiệp vụ.
/// </summary>
public enum PromotionKind
{
    /// <summary>Chỉ vào engine qua `VoucherCode` — KHÔNG bao giờ là candidate tự động.</summary>
    Voucher = 0,

    /// <summary>Candidate tự động (`Status = Active`).</summary>
    Discount = 1,

    /// <summary>Tồn tại trong enum nhưng bị `CHECK` chặn tạo ở v1 (`D-156`, nợ `CR-P2`).</summary>
    FlashSale = 2
}

/// <summary>Máy trạng thái `Promotion` — mọi chuyển sai **NÉM** `0006` (`BR-15`).</summary>
public enum PromotionStatus
{
    Draft = 0,
    Approved = 1,
    Active = 2,
    Paused = 3,
    Expired = 4,
    Cancelled = 5
}

/// <summary>Kiểu giảm giá (`D-151`). `FreeShipping` bị `CHECK` chặn ở v1 (`D-158`, nợ `CR-S2`).</summary>
public enum PromotionDiscountKind
{
    Percentage = 0,
    FixedAmount = 1,
    FreeShipping = 2
}

/// <summary>Kiểu node nhóm trong cây AND/OR.</summary>
public enum PromotionConditionGroupType
{
    And = 0,
    Or = 1
}

/// <summary>Trạng thái một dòng sổ lượt `ProPromotionRedemptions` (`CR-5`) — **ghim ordinal**.</summary>
public enum PromotionRedemptionStatus
{
    Reserved = 0,
    Applied = 1,
    Released = 2,
    Reversed = 3
}

/// <summary>Trạng thái mã (`CR-2`). CẤM chứa `Expired`/`Exhausted` (suy ra từ bộ đếm).</summary>
public enum VoucherCodeStatus
{
    Generated = 0,
    Issued = 1,
    Revoked = 2
}
