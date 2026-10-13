using eCommerce.ChannelHub;
using eCommerce.Tax;

namespace eCommerce.Tests.DistributedLocking;

/// <summary>
/// 🚨 TEST BẤT BIẾN cho **tiền tố khoá phân tán** — chống tái phát lỗi **C13** (tiền tố ĐÔI).
///
/// BỐI CẢNH ĐÃ KIỂM CHỨNG BẰNG CHẠY THẬT (13/10/2026):
///   • `AbpDistributedLockOptions.KeyPrefix = "ecommerce:"` (đặt ở `eCommerceHttpApiHostModule`).
///   • ABP ghép tiền tố với tên khoá theo công thức `KeyPrefix + name` — **KHÔNG tự thêm dấu phân cách**.
///   • ⇒ Nếu hàm sinh khoá của module **cũng** chứa `ecommerce:` thì khoá thật trong Redis thành
///     **`ecommerce:ecommerce:tax:…`** (tiền tố đôi). Đây là **lỗi im lặng**: code vẫn chạy, lock vẫn
///     hoạt động, chỉ là **tên khoá sai quy ước** và rất dễ gỡ nhầm khi vận hành.
///
/// 🔒 BẤT BIẾN (`XC-003` §5.4): **tên khoá do module sinh KHÔNG được chứa tiền tố** — tiền tố là việc
/// của `KeyPrefix` (một nguồn sự thật duy nhất).
/// </summary>
public class LockKeyPrefixTests
{
    private const string TienTo = "ecommerce:";

    private static void KhongDuocChuaTienTo(string khoa, string tenHam)
    {
        Assert.False(
            khoa.StartsWith(TienTo, StringComparison.OrdinalIgnoreCase),
            $"{tenHam} trả về '{khoa}' — CÓ tiền tố '{TienTo}'. " +
            $"Vì KeyPrefix đã là '{TienTo}', khoá thật sẽ thành '{TienTo}{khoa}' (TIỀN TỐ ĐÔI — lỗi C13). " +
            "Hàm sinh khoá của module KHÔNG được chứa tiền tố.");
    }

    [Fact]
    public void TaxLockKeys_khong_chua_tien_to()
    {
        var tenant = Guid.NewGuid();
        var target = Guid.NewGuid();

        KhongDuocChuaTienTo(TaxLockKeys.Classification(tenant, target), "TaxLockKeys.Classification");
        KhongDuocChuaTienTo(TaxLockKeys.RateSchedule(tenant), "TaxLockKeys.RateSchedule");
    }

    [Fact]
    public void ChannelLockKeys_khong_chua_tien_to()
    {
        KhongDuocChuaTienTo(
            ChannelHubConsts.LockKeys.ChannelTokenRefresh("shopee", "SHOP-1"),
            "ChannelHubConsts.LockKeys.ChannelTokenRefresh");

        KhongDuocChuaTienTo(
            ChannelHubConsts.LockKeys.ChannelStockPush("shopee", "SHOP-1"),
            "ChannelHubConsts.LockKeys.ChannelStockPush");

        KhongDuocChuaTienTo(
            ChannelHubConsts.LockKeys.ChannelOrderPull("shopee", "SHOP-1"),
            "ChannelHubConsts.LockKeys.ChannelOrderPull");
    }

    [Fact]
    public void AppLockKeys_khong_chua_tien_to()
    {
        KhongDuocChuaTienTo(
            eCommerceConsts.LockKeys.InventorySku("WH-1", "SKU-1"),
            "eCommerceConsts.LockKeys.InventorySku");
    }

    [Fact]
    public void Khoa_van_giu_dung_dinh_dang_sau_khi_bo_tien_to()
    {
        // Bỏ tiền tố KHÔNG được làm mất phần phân biệt nghiệp vụ.
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var target = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal($"tax:classification:{tenant}:{target}", TaxLockKeys.Classification(tenant, target));
        Assert.Equal($"tax:rate-schedule:{tenant}", TaxLockKeys.RateSchedule(tenant));
        Assert.Equal("channel:shopee:SHOP-1:token-refresh",
            ChannelHubConsts.LockKeys.ChannelTokenRefresh("shopee", "SHOP-1"));
    }
}
