using eCommerce.ChannelHub;
using eCommerce.ChannelHub.Connectors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eCommerce.Tests.ChannelHub;

/// <summary>
/// Test cho thuật toán ký Shopee — phần dễ sai nhất và nếu sai thì MỌI call API đều bị từ chối.
///
/// Phạm vi kiểm chứng trung thực:
///   ✅ Chuỗi ký (baseString) ĐÚNG THỨ TỰ và không có dấu phân cách — đây là thứ tài liệu
///      mô tả và cũng là thứ dễ viết sai nhất.
///   ✅ Chữ ký có định dạng đúng (hex thường, 64 ký tự = SHA-256) và ổn định.
///   ⚠️ KHÔNG kiểm chứng được với server Shopee: cần partner_id/partner_key thật + sandbox.
///      Khi có credential, hãy gọi /api/v2/shop/auth_partner và xác nhận không trả lỗi chữ ký.
/// </summary>
public class ShopeeSigningTests
{
    private const string PartnerId = "1234567";
    private const string PartnerKey = "test_partner_key_khong_dung_cho_production";

    private static ShopeeConnector CreateConnector() => new(
        Options.Create(new ChannelHubOptions
        {
            Shopee = new ShopeeOptions
            {
                PartnerId = PartnerId,
                PartnerKey = PartnerKey,
                EnableLiveCalls = false
            }
        }),
        NullLogger<ShopeeConnector>.Instance);

    [Fact]
    public void BuildBaseString_public_api_khong_co_access_token_va_shop_id()
    {
        var connector = CreateConnector();

        var baseString = connector.BuildBaseString("/api/v2/shop/auth_partner", 1700000000);

        Assert.Equal("1234567/api/v2/shop/auth_partner1700000000", baseString);
    }

    [Fact]
    public void BuildBaseString_shop_api_noi_them_access_token_va_shop_id()
    {
        var connector = CreateConnector();

        var baseString = connector.BuildBaseString(
            "/api/v2/order/get_order_list",
            1700000000,
            accessToken: "access-abc",
            shopId: "998877");

        // Đúng thứ tự: partner_id + api_path + timestamp + access_token + shop_id, KHÔNG dấu phân cách.
        Assert.Equal("1234567/api/v2/order/get_order_list1700000000access-abc998877", baseString);
    }

    [Fact]
    public void Sign_tra_ve_hex_thuong_64_ky_tu_va_on_dinh()
    {
        var connector = CreateConnector();

        var sign1 = connector.Sign("/api/v2/shop/auth_partner", 1700000000);
        var sign2 = connector.Sign("/api/v2/shop/auth_partner", 1700000000);

        Assert.Equal(64, sign1.Length);
        Assert.Matches("^[0-9a-f]{64}$", sign1);
        Assert.Equal(sign1, sign2); // cùng input ⇒ cùng chữ ký
    }

    [Fact]
    public void Sign_thay_doi_khi_bat_ky_thanh_phan_nao_thay_doi()
    {
        var connector = CreateConnector();

        var baseline = connector.Sign("/api/v2/order/get_order_list", 1700000000, "access-abc", "998877");

        // Đổi timestamp (đây là lỗi hay gặp: ký một timestamp nhưng gửi timestamp khác)
        Assert.NotEqual(baseline, connector.Sign("/api/v2/order/get_order_list", 1700000001, "access-abc", "998877"));

        // Đổi api_path
        Assert.NotEqual(baseline, connector.Sign("/api/v2/order/get_order_detail", 1700000000, "access-abc", "998877"));

        // Đổi access token
        Assert.NotEqual(baseline, connector.Sign("/api/v2/order/get_order_list", 1700000000, "access-xyz", "998877"));

        // Đổi shop_id
        Assert.NotEqual(baseline, connector.Sign("/api/v2/order/get_order_list", 1700000000, "access-abc", "998878"));
    }

    [Fact]
    public void Authorization_url_chua_du_partner_id_timestamp_va_sign()
    {
        var connector = CreateConnector();

        var url = connector.BuildAuthorizationUrl("https://shop.example.com/callback", 1700000000);

        Assert.Contains($"partner_id={PartnerId}", url);
        Assert.Contains("timestamp=1700000000", url);
        Assert.Contains("sign=", url);
        Assert.Contains("redirect=", url);
        Assert.StartsWith("https://partner.test-stable.shopeemobile.com/api/v2/shop/auth_partner?", url);
    }
}
