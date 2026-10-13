namespace eCommerce.Tax;

/// <summary>
/// Mã lỗi nghiệp vụ của module <b>Tax</b>.
///
/// 📌 Vì sao khai báo ở module chứ không dùng <c>eCommerceConsts.ErrorCodes</c> của app:
/// module KHÔNG được tham chiếu project <c>src/</c> (quy ước bất di bất dịch #3).
/// Module tự sở hữu hằng số của mình — giống cách <c>ChannelHubConsts</c> tự sở hữu lock key.
/// </summary>
public static class TaxErrorCodes
{
    /// <summary>Khung hiệu lực của phân loại bị chồng với bản ghi đã có.</summary>
    public const string OverlappingEffectiveRange = "eCommerce:Tax:0001";

    /// <summary>Không tìm thấy mức thuế suất nào có hiệu lực tại thời điểm cần tính.</summary>
    public const string NoEffectiveTaxRate = "eCommerce:Tax:0002";

    /// <summary>SKU chưa được phân loại thuế ⇒ chặn phát hành hoá đơn (đã chốt: vẫn CHO BÁN).</summary>
    public const string SkuNotClassified = "eCommerce:Tax:0003";

    /// <summary>File import sai định dạng / thiếu cột bắt buộc.</summary>
    public const string InvalidImportFile = "eCommerce:Tax:0004";

    /// <summary>Bản ghi thay thế chưa được duyệt.</summary>
    public const string ReplacementNotApproved = "eCommerce:Tax:0005";

    /// <summary>Không được sửa/xoá bản ghi đã có hiệu lực (phải tạo bản ghi mới).</summary>
    public const string CannotModifyActive = "eCommerce:Tax:0006";

    /// <summary>Đặt hiệu lực lùi về quá khứ trong khi đã có chứng từ phát sinh (BR-011).</summary>
    public const string BackdatedEffectiveDate = "eCommerce:Tax:0007";

    /// <summary>Chuyển trạng thái không hợp lệ.</summary>
    public const string InvalidStatusTransition = "eCommerce:Tax:0008";
}

/// <summary>Tên khoá distributed lock của module Tax.</summary>
public static class TaxLockKeys
{
    /// <summary>
    /// Khoá khi import/sửa phân loại của một đối tượng (SKU hoặc danh mục).
    /// Vì sao cần: 2 kế toán cùng import cho cùng SKU có thể tạo 2 bản ghi chồng khung.
    /// Ràng buộc EXCLUDE ở DB vẫn là chốt cuối, nhưng lock giúp tránh lỗi khó hiểu cho người dùng.
    /// </summary>
    public static string Classification(Guid tenantId, Guid targetId)
        => $"ecommerce:tax:classification:{tenantId}:{targetId}";

    /// <summary>Khoá khi đổi khung hiệu lực của bảng mức thuế suất (ảnh hưởng toàn hệ thống).</summary>
    public static string RateSchedule(Guid tenantId)
        => $"ecommerce:tax:rate-schedule:{tenantId}";
}
