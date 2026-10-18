using System.Security.Cryptography;
using System.Text;
using eCommerce.ChannelHub.Connectors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eCommerce.ChannelHub.Connectors;

/// <summary>
/// Connector Shopee — bản khung.
///
/// ✅ ĐÃ CÓ THẬT: thuật toán ký request (đã khảo sát và xác minh từ tài liệu/SDK).
/// ⏳ TODO: phần gọi HTTP thật (đăng ký HttpClient, endpoint sản phẩm/đơn, xử lý webhook).
///    Trong PoC, <see cref="ShopeeOptions.EnableLiveCalls"/> = false nên connector trả kết quả
///    rỗng để toàn bộ luồng worker/lock/event chạy được mà không cần credential của sàn.
///
/// ⚠️ Các ràng buộc của Shopee phải được tôn trọng trong implementation thật:
///   • access_token sống 4 giờ; refresh_token 30 ngày VÀ CÓ ROTATE → refresh phải nằm trong lock.
///   • api_path trong chuỗi ký CHỈ là path (không host, không query string).
///   • timestamp trong chuỗi ký PHẢI trùng timestamp gửi trên URL; server cần NTP đúng giờ.
///   • nhiều endpoint phải gọi bằng POST dù tên giống GET → luôn kiểm tra spec trước khi code.
///   • webhook là at-least-once → xử lý đơn phải idempotent theo order_sn / package_number.
/// </summary>
public class ShopeeConnector : IChannelConnector
{
    public const string Code = "shopee";

    private readonly ShopeeOptions _options;
    private readonly ILogger<ShopeeConnector> _logger;

    public ShopeeConnector(IOptions<ChannelHubOptions> options, ILogger<ShopeeConnector> logger)
    {
        _options = options.Value.Shopee;
        _logger = logger;
    }

    public string ChannelCode => Code;

    /// <summary>
    /// Ký request Shopee.
    /// Công thức (đã xác minh):
    ///   sign = hex( HMAC-SHA256( partner_key, baseString ) )
    /// trong đó baseString là CONCAT KHÔNG DẤU PHÂN CÁCH, khác nhau theo loại API:
    ///   • Public (auth link, token get/refresh): partner_id + api_path + timestamp
    ///   • Shop API:   partner_id + api_path + timestamp + access_token + shop_id
    ///   • Merchant:   partner_id + api_path + timestamp + access_token + merchant_id
    /// </summary>
    public string Sign(string apiPath, long timestamp, string? accessToken = null, string? shopId = null)
    {
        var baseString = BuildBaseString(apiPath, timestamp, accessToken, shopId);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.PartnerKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Chuỗi dùng để ký (baseString). Tách riêng khỏi <see cref="Sign"/> để có thể test
    /// ĐÚNG THỨ TỰ và việc KHÔNG có dấu phân cách — đây là chi tiết dễ viết sai nhất và
    /// sai thì mọi request đều bị Shopee từ chối.
    /// </summary>
    public string BuildBaseString(string apiPath, long timestamp, string? accessToken = null, string? shopId = null)
        => string.Concat(
            _options.PartnerId,
            apiPath,
            timestamp.ToString(),
            accessToken ?? string.Empty,
            shopId ?? string.Empty);

    /// <summary>URL uỷ quyền để người bán cấp quyền cho app (OAuth bước 1).</summary>
    public string BuildAuthorizationUrl(string redirectUrl, long timestamp)
    {
        const string path = "/api/v2/shop/auth_partner";
        var sign = Sign(path, timestamp);
        return $"{_options.BaseUrl}{path}?partner_id={_options.PartnerId}&timestamp={timestamp}&sign={sign}&redirect={Uri.EscapeDataString(redirectUrl)}";
    }

    public Task<PullOrdersResult> PullOrdersAsync(ChannelShopContext shop, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveCalls)
        {
            var soDonGia = _options.PocOrdersPerPull;

            _logger.LogInformation(
                "[PoC] ShopeeConnector.PullOrdersAsync (shop {ShopId}) — live calls đang TẮT, trả {SoDon} đơn giả. " +
                "Luồng worker/lock/event/bàn giao đơn vẫn chạy đầy đủ.",
                shop.ExternalShopId, soDonGia);

            return Task.FromResult(new PullOrdersResult
            {
                Orders = TaoDonGia(shop, soDonGia),
                NewSyncCursorUtc = DateTime.UtcNow
            });
        }

        // TODO(tích hợp thật):
        //   1) POST /api/v2/order/get_order_list  (lọc theo time_range_field=update_time, from=con trỏ)
        //   2) POST /api/v2/order/get_order_detail cho từng order_sn
        //   3) Map sang ChannelOrderModel, KHÔNG tự tạo Order ở đây (app sẽ làm qua event)
        //   4) Nếu HTTP 429 → trả RateLimited = true để supervisor backoff
        throw new NotImplementedException(
            "Gọi API Shopee thật chưa được cài đặt trong khung này. Xem comment TODO trong ShopeeConnector.");
    }

    /// <summary>
    /// Sinh đơn GIẢ cho PoC. ID **ổn định theo con trỏ đồng bộ**: khi con trỏ KHÔNG tiến (ca fail-closed
    /// — chưa có nơi nhận đơn) thì nhịp sau kéo lại **đúng những đơn đó**, đúng như sàn gửi lại thật.
    /// </summary>
    private static List<ChannelOrderModel> TaoDonGia(ChannelShopContext shop, int soDon)
    {
        if (soDon <= 0)
        {
            return [];
        }

        var conTro = shop.LastSyncedAtUtc?.Ticks ?? 0;
        var danhSach = new List<ChannelOrderModel>(soDon);

        for (var i = 0; i < soDon; i++)
        {
            // 🔒 `PlacedAtUtc` và `UpdatedAtUtc` phải **TẤT ĐỊNH** theo (shop, con trỏ, chỉ số) —
            // **KHÔNG** dùng `DateTime.UtcNow`.
            // 🚨 Vì sao: khi con trỏ không tiến (đúng ca fail-closed) hoặc khi sàn **gửi lại** cùng một đơn,
            //    connector phải trả về **CÙNG nội dung**. Nếu mốc đổi mỗi lần kéo thì "cùng một đơn"
            //    lại có payload khác ⇒ tầng chống trùng báo **XUNG ĐỘT PAYLOAD** (đúng theo thiết kế) và
            //    **không thể kiểm chứng** đường "sàn gửi lại" — đã gặp thật khi kiểm chứng E2E 15/10/2026.
            var placedAt = new DateTime(conTro, DateTimeKind.Utc).AddMinutes(i);

            danhSach.Add(new ChannelOrderModel
            {
                ExternalOrderId = $"POC-{shop.ExternalShopId}-{conTro}-{i}",
                PlacedAtUtc = placedAt,
                Currency = "VND",
                ShippingFee = 30_000m,
                ChannelDiscount = 0m,
                BuyerNote = "Đơn GIẢ do PoC sinh ra (ChannelHub:Shopee:PocOrdersPerPull).",

                // ══════════════════════════════════════════════════════════════════════════════════════════
                // 🆕 `S6` — **`update_time` CỦA SÀN** (`SAL-005` **F3** / nợ §17.6 #7). ĐÂY LÀ ĐIỀU KIỆN ĐỂ
                // TOÀN BỘ LUẬT WATERMARK **CHẠY ĐƯỢC TRÊN DỮ LIỆU THẬT**:
                // trước lượt này connector **không** trả `UpdatedAtUtc` ⇒ `AggregateVersion = null` ở **mọi**
                // ETO ⇒ nhánh `ApDung`/`Trung`/`XungDot`/`DenMuon` **chưa từng** chạy ở runtime, và handler
                // rơi vào nhánh *"thiếu phiên bản"* ⇒ **sàn có đổi đơn thì hệ thống cũng không biết**.
                //
                // 📌 `update_time` **LUÔN ≥** `placed_at` (đơn được sửa **sau** khi đặt) — ở đây +1 phút.
                // 🔒 Vẫn **tất định** như `PlacedAtUtc` ⇒ kéo lại cùng con trỏ cho ra **cùng** phiên bản
                // ⇒ nhánh `Trung` (trùng) kiểm được **thật**, không thành `XungDot` giả.
                // ⚠️ Cố ý **KHÔNG** dùng `DateTime.UtcNow`: nó làm mỗi lần kéo ra một phiên bản mới ⇒ mọi
                //    lần kéo đều thành "cập nhật mới" ⇒ **không** kiểm được ca then chốt *"sàn gửi lại"*.
                // ══════════════════════════════════════════════════════════════════════════════════════════
                UpdatedAtUtc = placedAt.AddMinutes(1),

                // Trạng thái **THÔ** của sàn — bắt buộc có để handler ghi được `MarketplaceStatusRaw`
                // (điều tra/đối soát). 🔒 Ánh xạ sang trạng thái nội bộ thuộc `SAL-003`, KHÔNG ở đây.
                StatusRaw = "READY_TO_SHIP",
                EventKind = "Created",

                Lines =
                [
                    new ChannelOrderLineModel
                    {
                        ExternalLineId = $"POC-LINE-{shop.ExternalShopId}-{conTro}-{i}-0",
                        Sku = $"POC-SKU-{i}",
                        Quantity = 1,
                        UnitPrice = 100_000m,
                        DiscountAmount = 0m
                    }
                ]
            });
        }

        return danhSach;
    }

    public Task<PushListingResult> PushListingAsync(ChannelShopContext shop, ChannelListingPushModel model, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveCalls)
        {
            _logger.LogInformation("[PoC] ShopeeConnector.PushListingAsync({Sku}) — mô phỏng thành công.", model.InternalSku);
            return Task.FromResult(new PushListingResult
            {
                InternalSku = model.InternalSku,
                Success = true,
                ExternalItemId = model.ExternalItemId ?? $"poc-item-{model.InternalSku}"
            });
        }

        // TODO: /api/v2/product/update_item , /api/v2/product/update_stock , /api/v2/product/update_price
        throw new NotImplementedException("Đẩy sản phẩm/tồn kho lên Shopee chưa được cài đặt.");
    }

    public Task<RefreshTokenResult> RefreshTokenAsync(ChannelShopContext shop, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveCalls)
        {
            _logger.LogInformation("[PoC] ShopeeConnector.RefreshTokenAsync(shop {ShopId}) — mô phỏng token mới.", shop.ExternalShopId);
            return Task.FromResult(new RefreshTokenResult
            {
                Success = true,
                AccessToken = $"poc-access-{Guid.NewGuid():N}",
                RefreshToken = $"poc-refresh-{Guid.NewGuid():N}",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(4),
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            });
        }

        // TODO: POST /api/v2/auth/access_token/get  (public API → baseString không có access_token)
        // ⚠️ Refresh token CÓ ROTATE: phải ghi lại CẢ access_token MỚI và refresh_token MỚI,
        //    trong cùng một transaction, khi đang giữ distributed lock per-shop.
        throw new NotImplementedException("Refresh token Shopee chưa được cài đặt.");
    }

    public Task<bool> PingAsync(ChannelShopContext shop, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLiveCalls)
        {
            return Task.FromResult(true);
        }

        // TODO: GET /api/v2/shop/get_shop_info
        throw new NotImplementedException("Ping Shopee chưa được cài đặt.");
    }
}
