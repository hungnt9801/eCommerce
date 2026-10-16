namespace eCommerce.Customers;

/// <summary>
/// Hằng số của module **Customers** (`CUS-001`).
///
/// 🚨 **VÌ SAO KHOÁ PHÂN TÁN PHẢI Ở ĐÂY, KHÔNG Ở `eCommerceConsts` (`ADR-08` — đã trả giá THẬT):**
/// tài liệu §0 ghi rõ bài học #2: *"khoá phân tán có 2 nguồn sự thật ⇒ khoá **không bảo vệ gì**
/// mà **không có lỗi biên dịch**"* — bằng chứng: `src/eCommerce.Domain.Shared/eCommerceConsts.cs`
/// **vs** `modules/ChannelHub/.../ChannelHubConsts.cs` (`XC-003` §15.2 **F1**).
/// ⇒ **CẤM** viết literal khoá trong app service; **CẤM** khai lại ở `eCommerceConsts`.
/// 🛡️ `CustomersConstsTests` khoá bất biến này trong CI.
///
/// ⚠️ **KHÔNG** thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` **đã** thêm
/// (`XC-003` §5.4 hệ quả 2: ghép **không** dấu phân cách ⇒ thành `ecommerce:ecommerce:…` và khoá
/// **không bảo vệ gì**).
/// </summary>
public static class CustomersConsts
{
    public static class LockKeys
    {
        /// <summary>Khoá theo **liên hệ** (email/SĐT) — tuần tự hoá *"kiểm trùng rồi ghi"* cho toàn tenant.</summary>
        public static string TheoLienHe(string? email, string? phone)
        {
            var e = string.IsNullOrWhiteSpace(email) ? "-" : email.Trim().ToLowerInvariant();
            var p = string.IsNullOrWhiteSpace(phone) ? "-" : phone.Trim();

            return $"customers:contact:{e}:{p}";
        }

        /// <summary>Khoá theo **id khách** — dùng cho nâng cấp (claim) và mọi thao tác đổi trạng thái.</summary>
        public static string TheoKhach(Guid customerId) => $"customers:customer:{customerId}";

        /// <summary>Khoá theo **tài khoản đăng nhập** — chống hai hồ sơ khách cùng nhận một tài khoản.</summary>
        public static string TheoTaiKhoan(Guid identityUserId) => $"customers:identity:{identityUserId}";
    }

    /// <summary>Thời gian chờ giành khoá mặc định (giây).</summary>
    public const int ChoKhoaToiDaGiay = 10;
}
