# Kiến trúc — eCommerce đa kênh

Tài liệu này giải thích **thiết kế và lý do**, không lặp lại hướng dẫn chạy (xem `README.md`).

---

## 1. Bức tranh tổng thể

```
                        ┌──────────────────────────────────────────────┐
   Shopee ──push/webhook──►                                          │
   Lazada ──push/webhook──►   ChannelHub (module)                    │
   TikTok ──push/webhook──►   • Supervisor worker (cố định)          │
                        │    • Dynamic worker / shop (tự đăng ký)  │
                        │    • ChannelTokenManager (lock + rotate) │
                        │    • IChannelConnector → Shopee/Lazada…  │
                        └───────────────┬──────────────────────────────┘
                                        │ ChannelOrderPulledEto (distributed event bus)
                                        ▼
   Website ──────────►  ┌──────────────────────────────────────────────┐
                        │  Commerce core (app)                         │
                        │  Catalog · Inventory (append-only ledger)    │
                        │  Orders · Pricing · Promotions               │
                        └───────────────┬──────────────────────────────┘
                                        │ PaymentTransaction
                                        ▼
                        ┌──────────────────────────────────────────────┐
                        │  PaymentHub (module)                         │
                        │  IPaymentProvider → VNPay/MoMo/payOS/SePay…  │
                        └──────────────────────────────────────────────┘

   Hạ tầng dùng chung: PostgreSQL (nguồn sự thật) · Redis (cache + distributed lock)
```

**Nguyên tắc bất di bất dịch:** sàn và cổng thanh toán chỉ được biết qua **interface của module**.
App không bao giờ `using` SDK của Shopee hay gọi thẳng API VNPay.

---

## 2. Vì sao chọn ABP Modular Monolith

| Yêu cầu của dự án | ABP cho sẵn | Ghi chú |
|---|---|---|
| Worker đồng bộ **theo từng shop**, đăng ký lúc chạy | `IDynamicBackgroundWorkerManager` | Không phải tự viết scheduler |
| Chống mất token khi refresh (Shopee rotate token) | `IAbpDistributedLock` (Redis) | Đây là lý do **bắt buộc** có Redis, không chỉ cache |
| Tách module → microservice về sau | Modularity + `[DependsOn]` | ChannelHub đã sẵn sàng tách |
| Cách ly dữ liệu theo tenant | `IMultiTenant` + data filter | Entity đã implement; UI/auto-migrate là module PRO |
| Nhiều DBMS, không bị khoá NoSQL | EF Core / MongoDB / Dapper đều chính thức | Chọn PostgreSQL để có SQL + transaction |
| Mở rộng entity của module mà không fork | `ObjectExtensionManager` | Khác hẳn nopCommerce/GrandNode (phải fork) |

**Đánh đổi đã chấp nhận:** ABP **không có nghiệp vụ commerce nào** (kể cả bản trả phí), nên toàn bộ
catalog/giỏ/đơn/khuyến mãi/thuế/vận chuyển/storefront là phần phải tự viết. Đổi lại: kiểm soát 100%
mô hình dữ liệu — điều kiện cần để đồng bộ đa kênh không phải "đánh vật" với extension point.

---

## 3. `ChannelHub` — thiết kế chi tiết

### 3.1 Vòng đời một shop

```
1. Người bán cấp quyền (OAuth) trên sàn
2. POST /api/app/channel (ChannelAppService.ConnectAsync)
   → token được MÃ HOÁ bằng IStringEncryptionService rồi lưu vào ChnChannelShops
3. Supervisor (mỗi 60s) đọc shop IsActive=true
   → IDynamicBackgroundWorkerManager.AddAsync("channel-sync:shopee:123456", Period=300s, handler)
4. Mỗi nhịp, handler gọi ChannelShopSyncer.SyncAsync(shopId)
   → ChannelTokenManager.GetValidContextAsync()  ← refresh trong lock nếu cần
   → IChannelConnector.PullOrdersAsync()
   → publish ChannelOrderPulledEto cho từng đơn
   → chỉ khi phát sự kiện thành công mới tiến con trỏ LastSyncedAtUtc
5. Shop bị tắt (IsActive=false) → nhịp sau supervisor gọi RemoveAsync()
```

### 3.2 Vì sao supervisor + dynamic worker (không phải một worker quét tất cả)

- **Quota theo shop**: Shopee/Lazada giới hạn theo từng shop ⇒ một vòng lặp chung sẽ vi phạm quota.
- **Cô lập lỗi**: một shop lỗi/timeout không chặn các shop khác.
- **Không cần deploy lại** khi thêm shop.
- **An toàn sau restart**: handler của dynamic worker chỉ nằm trong bộ nhớ, nhưng vì supervisor chạy
  định kỳ và `AddAsync` ghi đè theo **tên worker xác định**, việc đăng ký lại là tự động.

### 3.3 Refresh token — chỗ dễ mất tiền nhất

```csharp
await using var handle = await _distributedLock.TryAcquireAsync(
    ChannelHubConsts.LockKeys.ChannelTokenRefresh(channelCode, shopId), ...);

if (handle is null) return (ToContext(shop), false);   // worker khác đang refresh → bỏ lượt

var fresh = await _channelShopRepository.GetAsync(shop.Id, ...);
if (!fresh.NeedsTokenRefresh(now, margin)) return (ToContext(fresh), false);  // double-check
```

**Lý do:** Shopee **rotate refresh token**. Nếu hai worker refresh song song, request thứ hai dùng
refresh token cũ đã bị vô hiệu ⇒ **shop mất kết nối vĩnh viễn, phải OAuth lại bằng tay**.
`Handle is null` **không phải** lỗi — đó là hành vi đúng.

Lưu ý thực tế đã khảo sát: access token Shopee sống **4 giờ**, refresh token **30 ngày**,
`partner_key` **hết hạn sau 180 ngày và không tự gia hạn** ⇒ cần nhắc lịch gia hạn.

### 3.4 Interface connector

```csharp
public interface IChannelConnector
{
    string ChannelCode { get; }
    Task<PullOrdersResult> PullOrdersAsync(ChannelShopContext shop, CancellationToken ct = default);
    Task<PushListingResult> PushListingAsync(ChannelShopContext shop, ChannelListingPushModel model, CancellationToken ct = default);
    Task<RefreshTokenResult> RefreshTokenAsync(ChannelShopContext shop, CancellationToken ct = default);
    Task<bool> PingAsync(ChannelShopContext shop, CancellationToken ct = default);
}
```

Thêm sàn mới = **một class + một dòng** trong `ChannelConnectorFactory`. Tầng đơn hàng/tồn kho không đổi.

### 3.5 Đặc tả ký/xác thực đã xác minh (được khoá bằng test)

**Shopee** — `sign = hex(HMAC-SHA256(partner_key, baseString))`, baseString **nối không dấu phân cách**:
- Public API (auth link, token get/refresh): `partner_id + api_path + timestamp`
- Shop API: `partner_id + api_path + timestamp + access_token + shop_id`
- `api_path` **chỉ là path** (không host, không query); `timestamp` trên URL phải **trùng** timestamp đã ký.

**VNPay** — ký `HMAC-SHA512` trên tham số **sắp xếp tăng dần theo tên**, nối `&` dạng `key=value` (đã URL-encode),
**loại bỏ** `vnp_SecureHash*`. Thành công chỉ khi `vnp_ResponseCode == "00"` **VÀ** `vnp_TransactionStatus == "00"`.
IPN phải trả `{"RspCode":"00","Message":"Confirm Success"}` (sai chữ ký ⇒ `97`). `vnp_TxnRef` không được trùng trong ngày.

---

## 4. Chống bán vượt tồn kho (oversell) — 3 lớp

| Lớp | Cơ chế | Chống được gì |
|---|---|---|
| 1 | `StockLedgerEntry` append-only + `InventoryItem.ApplyMovement` ném lỗi nếu âm | Sai logic nội bộ |
| 2 | Distributed lock `InventorySku(warehouseId, sku)` + unique index `(TenantId, WarehouseId, Sku)` | Hai request đồng thời cùng trừ một SKU |
| 3 | `ChannelListing.AllottedQuantity` (channel allotment) | **Độ trễ cập nhật tồn kho của sàn** — nguyên nhân phổ biến nhất gây oversell khi bán đa kênh |

Lớp 3 là điểm khác biệt so với nền tảng chỉ có multi-store: khi đẩy tồn kho lên Shopee, phải đẩy
**số đã trừ buffer**, không đẩy tồn kho thật.

---

## 5. Idempotency — bắt buộc, không phải tuỳ chọn

Webhook của sàn và IPN của cổng đều là **at-least-once**. Do đó:

| Bảng | Index unique | Bảo vệ |
|---|---|---|
| `OrdOrders` | `(TenantId, ChannelCode, ExternalOrderId)` WHERE ExternalOrderId NOT NULL | Không tạo hai đơn từ một webhook |
| `OrdOrderLines` | `(OrderId, ExternalLineId)` WHERE ExternalLineId NOT NULL | Không nhân đôi dòng đơn |
| `PayTransactions` | `(TenantId, ProviderCode, IdempotencyKey)` WHERE IdempotencyKey NOT NULL | Không ghi nhận hai lần một giao dịch |
| `InvItems` | `(TenantId, WarehouseId, Sku)` | Hàng rào cuối chống ghi trùng tồn kho |
| `ChnChannelShops` | `(TenantId, ChannelCode, ExternalShopId)` | Không kết nối trùng shop |
| `ChnChannelListings` | `(TenantId, ChannelShopId, ProductVariantId)` | Không map trùng biến thể lên một shop |

Trạng thái cuối của giao dịch **không thể bị ghi đè** (đã có test): `MarkSucceeded` lần hai trả `false`,
`MarkFailed` không hạ được giao dịch đã `Succeeded`.

---

## 6. Mô hình dữ liệu (rút gọn)

```
CatProducts ──1..n── CatProductVariants ──1..n── ChnChannelListings ──n..1── ChnChannelShops
                            │                                                  │
                            │                                                  └── ChnChannels (cấu hình kênh)
                            ▼
                     InvItems (OnHand, Reserved)  ◄── InvStockLedger (append-only)
                            ▲
                            │
OrdOrders ──1..n── OrdOrderLines
     │
     └── PayTransactions (số tiền, trạng thái, IdempotencyKey)

Mọi bảng nghiệp vụ có `TenantId` + `IMultiTenant`.
```

---

## 7. Đường tách microservice (khi cần, không phải bây giờ)

Vì `ChannelHub` không tham chiếu app, việc tách gồm:

1. Đổi `ChannelHub.EntityFrameworkCore` thành DbContext riêng + migration riêng (`AddAbpDbContext<ChannelHubDbContext>()`).
2. Đổi `IDistributedEventBus` từ in-process sang **RabbitMQ** (chỉ đổi đăng ký DI — code phát sự kiện không đổi).
3. Bật **outbox** để bảo đảm không mất sự kiện khi mạng chập chờn.

Đó là lý do ngay từ đầu đã dùng **distributed event bus** thay vì gọi trực tiếp giữa các lớp.

---

## 8. Những gì học được từ các nền tảng đã khảo sát

| Nền tảng | Điều đáng học | Điều cố tình tránh |
|---|---|---|
| **nopCommerce** | Cấu trúc plugin, storefront hoàn chỉnh | Không dùng: AGPL + **bắt buộc "powered by nopCommerce"**, release chậm |
| **GrandNode 2** | Kiến trúc sạch, có sẵn commerce | Không dùng: **tiền lưu bằng `double`**, không NuGet plugin (phải fork), GPL-3.0 không mua được để thoát, bus factor 1 người |
| **ABP** | Hạ tầng enterprise: worker động, lock, event bus + outbox, tenant, `ObjectExtensionManager` | Không dùng: kỳ vọng có sẵn commerce (không có), và module PRO SaaS/Payment |
| **Orchard Core** | Multi-tenancy, workflow, content/SEO | Không dùng: commerce quá mỏng cho nghiệp vụ đa kênh |

Bài học xuyên suốt: **không nền tảng nào có connector Shopee/Lazada hay cổng VN** ⇒ quyết định
nền tảng **không** quyết định tiến độ; **lớp đồng bộ kênh** mới quyết định. Vì vậy lớp đó được
thiết kế trước và tách riêng thành module.
