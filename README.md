# eCommerce — khung solution đa kênh (.NET 10 + ABP Modular Monolith + PostgreSQL)

Khung solution cho hệ thống bán hàng **đa kênh** (website riêng + Shopee/Lazada/TikTok Shop) có
**quản trị tập trung tồn kho** và **thanh toán Việt Nam**, dựng theo kiến trúc đã chốt:

> **ABP Framework 10.7 · Modular Monolith · PostgreSQL qua EF Core · `ChannelHub` là module riêng**

---

## 1. Trạng thái kiểm chứng (trung thực)

| Hạng mục | Kết quả | Bằng chứng |
|---|:--:|---|
| **Biên dịch toàn solution (15 project)** | ✅ **Thành công — 0 lỗi, 0 warning** | `dotnet build eCommerce.slnx` |
| **Sinh migration EF Core cho PostgreSQL** | ✅ **Thành công** | `src/eCommerce.EntityFrameworkCore/Migrations/20261009070214_Initial.cs` — 10 bảng + 3 index unique có filter |
| **Test nghiệp vụ tự động** | ✅ **20/20 pass** | `dotnet test` — xem mục 5 |
| **Chạy end-to-end thật (DB + Redis + Host)** | 🟡 **Một phần** | Host **khởi động được** (`Initialized all ABP modules`), Swagger 200, `/connect/token` trả lỗi OAuth chuẩn. Các luồng cần PostgreSQL/Redis **chưa chạy được** vì Docker daemon chưa bật |

### 🔐 Phase 0 (xác thực) — ĐÃ THỰC THI

8 module ABP đã được thêm: **Identity, PermissionManagement, SettingManagement, FeatureManagement,
AuditLogging, BackgroundJobs, OpenIddict, Account** (+ `Volo.Abp.OpenIddict.AspNetCore` cho Host).
`AddAlwaysAllowAuthorization()` **đã bị xoá** — API giờ đi qua OpenIddict + permission của ABP.

👉 **Đọc [docs/phase-0-execution-report.md](docs/phase-0-execution-report.md)** — báo cáo ghi lại
**6 lỗi chỉ lộ khi chạy thật** (thiếu `AbpDbContextOptions`, thiếu `DbSet` cho entity module,
thiếu `[Authorize]` cấp class, Redis làm sập khởi động, 2 bug của ABP CLI) và cách kiểm chứng phần còn lại.

> ⚠️ **Nói rõ để không hiểu nhầm:** khung này **biên dịch, sinh migration và pass test**, nhưng
> **chưa từng chạy với database thật** trong môi trường dựng khung. Bước 4 là việc cần làm đầu tiên
> trên máy bạn.

Trong quá trình dựng, **test đã bắt được 1 lỗi thật**: `BaseUrl` của Shopee chứa `/api/v2` trong khi
`api_path` cũng có `/api/v2` ⇒ URL bị lặp `/api/v2/api/v2`. Đã sửa tại `ChannelHubOptions.cs`.
Đây là lý do phải viết test cho phần ký/xác thực chữ ký.

---

## 2. Cấu trúc solution

```
eCommerce.slnx                        (15 project)
├─ global.json                        SDK 10.0.400
├─ Directory.Build.props              net10.0, nullable, implicit usings
├─ Directory.Packages.props           ABP 10.7.0 (central package management)
├─ docker-compose.yml                 PostgreSQL 17 + Redis 7
│
├─ src/                               ỨNG DỤNG (sở hữu domain commerce)
│  ├─ eCommerce.Domain.Shared         enum, mã lỗi, khoá distributed lock
│  ├─ eCommerce.Domain                Product/ProductVariant, InventoryItem, StockLedgerEntry, Order/OrderLine
│  ├─ eCommerce.Application.Contracts DTO + IChannelAppService
│  ├─ eCommerce.Application           ChannelAppService
│  ├─ eCommerce.EntityFrameworkCore   MỘT DbContext + mapping commerce + gọi mapping của 2 module
│  ├─ eCommerce.HttpApi.Host          host API (Swagger), Redis cache + Redis distributed lock
│  └─ eCommerce.DbMigrator            chạy migration + seed kênh bán
│
├─ modules/
│  ├─ ChannelHub/                     🎯 LỚP ĐỒNG BỘ ĐA KÊNH (module riêng, tách microservice được)
│  │  ├─ ChannelHub.Domain            Channel, ChannelShop (token mã hoá), ChannelListing + hằng số lock
│  │  ├─ ChannelHub.Application.Contracts  IChannelConnector, model trung lập, ETO
│  │  ├─ ChannelHub.Application       ShopeeConnector (đã có thuật toán ký), supervisor + worker động, token manager
│  │  └─ ChannelHub.EntityFrameworkCore    ConfigureChannelHub()
│  └─ PaymentHub/                     💳 CỔNG THANH TOÁN (tự làm, không phụ thuộc module PRO)
│     ├─ PaymentHub.Domain            PaymentTransaction, IPaymentProvider
│     ├─ PaymentHub.Application       VnPayPaymentProvider (đã có ký + xác thực IPN)
│     └─ PaymentHub.EntityFrameworkCore    ConfigurePaymentHub()
│
└─ test/eCommerce.Tests               20 test: ký Shopee, IPN VNPay, bất biến tồn kho/đơn/thanh toán
```

---

## 3. Quyết định kiến trúc quan trọng (kèm lý do)

| # | Quyết định | Lý do |
|---|---|---|
| 1 | **`ChannelHub` sở hữu entity của chính nó**, không tham chiếu domain của app | Module → app là phụ thuộc ngược. Nhờ vậy ChannelHub **tách thành microservice được** mà không viết lại. Khi build, lỗi `eCommerceConsts does not exist` chính là ranh giới này hoạt động đúng. |
| 2 | **ChannelHub không tự tạo đơn**, chỉ phát `ChannelOrderPulledEto` | Giữ module độc lập; đơn hàng do app tạo và **idempotent theo `ExternalOrderId`**. |
| 3 | **Tiền là `decimal(18,4)`, tuyệt đối không `double`** | GrandNode 2 lưu tiền bằng `double` — đó là lỗi chúng ta không lặp lại. Test `Tinh_tien_dung_voi_decimal_khong_sai_so` khoá hành vi này. |
| 4 | **Tồn kho là sổ cái append-only** (`StockLedgerEntry`), `InventoryItem` chỉ là ảnh chụp | Truy vết + đối soát đa kênh; không UPDATE số lượng trực tiếp. Domain ném lỗi nếu biến động làm âm kho. |
| 5 | **Chống oversell bằng 3 lớp**: unique index `(TenantId, WarehouseId, Sku)`, khoá lock `InventorySku`, và channel allotment trên `ChannelListing` | Sàn cập nhật tồn kho có độ trễ ⇒ cần buffer riêng cho từng kênh. |
| 6 | **Chống trùng webhook bằng index unique có filter** | Webhook sàn là *at-least-once*. Migration có `filter: "ExternalOrderId" IS NOT NULL`, `"ExternalLineId" IS NOT NULL`, `"IdempotencyKey" IS NOT NULL`. |
| 7 | **Mỗi shop = một dynamic background worker**, do supervisor tự đăng ký/huỷ | Nhịp và quota khác nhau theo shop; một shop lỗi không chặn shop khác; shop mới không cần deploy lại. |
| 8 | **Refresh token bắt buộc nằm trong distributed lock per-shop + double-check** | Shopee **rotate refresh token**: hai worker refresh song song sẽ làm mất token vĩnh viễn. Có test/logic tương ứng trong `ChannelTokenManager`. |
| 9 | **Token mã hoá bằng `IStringEncryptionService`**, DTO chỉ trả cờ `HasRefreshToken` | Không rò rỉ token qua API/log. |
| 10 | **Không dùng AutoMapper** | Bỏ `Volo.Abp.AutoMapper` cũng loại luôn cảnh báo bảo mật **NU1903 (GHSA-rvv3-g6hj-g44x)** của AutoMapper 14.0.0. Map tay chỉ vài dòng. |
| 11 | **`PaymentHub` tự làm thay vì `Volo.Payment` (PRO)** | `Volo.Payment` cần license **Team+**, chỉ có Stripe/PayPal/2Checkout/PayU/Iyzico/Alipay (**không có cổng VN**), và luồng `StartAsync` của nó **không khớp SePay/Casso** (webhook-first, đối soát VietQR). |
| 12 | **ReturnUrl không phải bằng chứng thanh toán** | Chỉ IPN/webhook **đã xác thực chữ ký + khớp số tiền** mới được chuyển sang `Succeeded`. Có test cho cả trường hợp chữ ký đúng nhưng giao dịch thất bại. |

---

## 4. Chạy thử (cần làm đầu tiên trên máy bạn)

**Yêu cầu:** .NET SDK 10, Docker Desktop **đang chạy**, `dotnet-ef`, **`abp` CLI 10.7.0** + **yarn** (chỉ cần cho UI ở bước 6).

```powershell
cd D:\Projects\Tools\eCommerce

# 1) Bật PostgreSQL + Redis
docker compose up -d

# 2) Sinh migration (đã sinh sẵn bản Initial; chỉ chạy lại nếu đổi entity)
dotnet ef migrations add <TenName> --project src/eCommerce.EntityFrameworkCore --output-dir Migrations

# 3) Tạo bảng + seed kênh bán + user/role/permission
dotnet run --project src/eCommerce.DbMigrator

# 4) Chạy API host (cổng 5000) — ĐÂY LÀ AUTH SERVER DUY NHẤT (OpenIddict phát token)
dotnet run --project src/eCommerce.HttpApi.Host
#    → Swagger:        http://localhost:5000/swagger
#    → Health check:   http://localhost:5000/health-status   (mong đợi 200 Healthy)

# 5) Lấy token để gọi API (dev: grant `password`)
$tok = Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/connect/token' `
  -ContentType 'application/x-www-form-urlencoded' -Body @{
    grant_type='client_credentials'; client_id='eCommerce_App'; client_secret='1q2w3e*'; scope='eCommerce' }
Invoke-RestMethod 'http://localhost:5000/api/app/channel/shops' -Headers @{ Authorization = "Bearer $($tok.access_token)" }

# 6) Chạy UI QUẢN TRỊ (cổng 5100) — cần bước 6a ĐÚNG MỘT LẦN trên máy mới
# 6a) Cài thư viện client của theme (thư mục wwwroot/libs KHÔNG được commit)
abp install-libs -wd src/eCommerce.Web
# 6b) Chạy
dotnet run --project src/eCommerce.Web
#    → Đăng nhập: http://localhost:5100   (dev: admin@abp.io / 1q2w3E*)
```

> 🚨 **Quên bước 6a** ⇒ trang UI vẫn trả **`200`** nhưng **thiếu toàn bộ CSS/JS** (hiển thị trơ),
> và log sẽ có dòng `Could not find the bundle file '/libs/...'`. Không chạy `abp install-libs`
> thì **không** có lỗi biên dịch nào — đúng loại *"trông như chạy được"*.

> 📌 **Hai tiến trình, hai vai trò:** `HttpApi.Host` (5000) = API + **auth server duy nhất**;
> `eCommerce.Web` (5100) = UI MVC dùng **cookie** của ABP Identity, **không** làm auth server
> (kiểm chứng: `POST http://localhost:5100/connect/token` ⇒ **404**). UI gọi app service
> **trong tiến trình**, dùng **chung DB**.
> ⚠️ Mọi tiến trình UI **phải** tắt background worker/job (xem `src/eCommerce.Web/eCommerceWebModule.cs`) —
> nếu không, worker chạy **hai lần** (đồng bộ sàn, gửi cơ quan thuế…).

Kiểm tra ChannelHub chạy thật (không cần credential Shopee vì `EnableLiveCalls = false`):

```powershell
# Kết nối một shop Shopee giả lập
$body = @{
  channelCode = "shopee"; externalShopId = "123456"
  shopName = "Shop Test"; region = "VN"
  accessToken = "fake-access"; refreshToken = "fake-refresh"
  accessTokenExpiresAtUtc = (Get-Date).AddHours(4).ToString("o")
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "https://localhost:44300/api/app/channel" -Body $body -ContentType "application/json"
```

Sau ~60 giây, log phải hiện supervisor đăng ký worker động cho shop và syncer chạy theo nhịp.
Cách kiểm chứng nhanh nhất: thêm log hoặc xem bảng `ChnChannelShops.LastSyncedAtUtc` được cập nhật.

---

## 5. Test đang có (388 unit + 20 integration — con số chuẩn: `docs/handover-gd1.md` §1)

| Nhóm | Nội dung |
|---|---|
| **Ký Shopee** (`ShopeeSigningTests`) | Chuỗi ký **đúng thứ tự và không dấu phân cách** cho public API vs Shop API; chữ ký hex thường 64 ký tự; đổi timestamp/api_path/token/shop_id đều đổi chữ ký; URL uỷ quyền đủ tham số |
| **IPN VNPay** (`VnPayWebhookTests`) | Chữ ký hợp lệ + `ResponseCode=00` + `TransactionStatus=00` ⇒ chấp nhận; **sửa số tiền ⇒ chữ ký sai ⇒ trả RspCode 97**; chữ ký đúng nhưng giao dịch thất bại ⇒ **không** coi là thành công; thiếu chữ ký ⇒ từ chối; `vnp_Amount` = tiền × 100 |
| **Bất biến nghiệp vụ** (`OrderAndInventoryTests`) | Webhook trùng không tạo dòng đơn trùng; số lượng ≤ 0 bị chặn; tính tiền bằng decimal không sai số; bán vượt tồn kho bị chặn; `Available = OnHand − Reserved`; `MarkSucceeded` lần hai không đổi trạng thái; không ghi đè giao dịch đã thành công thành thất bại; hoàn tiền một phần/toàn bộ chuyển trạng thái đúng |

```powershell
dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj
```

> ⚠️ **Điều test KHÔNG kiểm chứng được:** chữ ký có được **server Shopee/VNPay chấp nhận** hay không —
> cần `partner_id`/`partner_key` và `vnp_TmnCode`/`vnp_HashSecret` thật. Test chỉ khoá **đặc tả**
> (thứ tự chuỗi ký, quy tắc mã thành công, quy tắc idempotency) để tránh hồi quy.

---

## 6. Đã có gì / còn thiếu gì

### ✅ Đã có (biên dịch + test được)
- Khung ABP Modular Monolith đúng layering, **19 project** (gồm `eCommerce.Web` — UI quản trị), chạy được `dotnet build`.
- Domain commerce lõi: catalog, tồn kho append-only, đơn hàng, với bất biến được test.
- **ChannelHub**: abstraction `IChannelConnector`, factory, `ShopeeConnector` (**thuật toán ký thật**), supervisor + worker động theo shop, `ChannelTokenManager` với distributed lock + double-check, ETO tích hợp.
- **PaymentHub**: `PaymentTransaction` (idempotent), `IPaymentProvider`, `VnPayPaymentProvider` (**ký + xác thực IPN thật**).
- EF Core/PostgreSQL: DbContext hợp nhất, mapping theo module, index unique chống trùng, migration đã sinh.
- Hạ tầng: Redis cache + **Redis distributed lock** (bắt buộc khi chạy nhiều instance), Swagger, seed dữ liệu.
- **UI quản trị (`eCommerce.Web`, cổng 5100)**: theme Basic (miễn phí) + **màn hình đăng nhập ABP Identity** — **đã kiểm chứng đăng nhập thật** (xem `docs/features/web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md` §16).

### ❌ Chưa có (cố ý để lại, có ghi chú trong code)
- ✅ ~~**UI đăng nhập & trang quản trị**~~ → **ĐÃ CÓ (15/10/2026)**: `src/eCommerce.Web` (cổng **5100**),
  theme Basic miễn phí + màn hình đăng nhập ABP Identity; **đăng nhập thật đã kiểm chứng**
  ([docs/features/web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md](docs/features/web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md) §16).
  ⚠️ Màn hình **nghiệp vụ** (sản phẩm/tồn kho/đơn hàng) **chưa có** — sẽ thêm cùng các module GĐ1.
- **`TenantManagement`, CMS Kit, Docs, Blogging** — chưa thêm (chưa cần ở Phase 0).
- ✅ ~~**Luồng cần database chưa được kiểm chứng**~~ → **ĐÃ kiểm chứng**: cấp token thật, ma trận
  `401/403/200` trên endpoint gắn quyền, seeder, distributed lock per-shop
  ([docs/runtime-verification-report.md](docs/runtime-verification-report.md) §9bis/§10).
- **Storefront (website bán hàng)**: chưa có (UI quản trị đã có khung) — thuộc GĐ1.
- **Gọi HTTP thật tới Shopee/Lazada**: `ShopeeConnector` mới có phần ký + khung; các method đánh dấu `TODO` và ném `NotImplementedException` khi `EnableLiveCalls = true`.
- **Webhook endpoint nhận IPN/push của sàn** và handler tạo đơn từ `ChannelOrderPulledEto`.
- **Connector Lazada/TikTok Shop**, các cổng MoMo/payOS/ZaloPay/SePay/Casso.
- **Job đối soát** (payment reconciliation) — bắt buộc vì webhook có thể mất.
- **Multi-tenant bật thật**: entity đã implement `IMultiTenant`, nhưng chưa thêm `Volo.TenantManagement`
  (UI + auto-migrate DB theo tenant là **module PRO**, cần license Team+).

### ⚠️ Vấn đề đã biết
- ✅ **Đã vá:** `System.Security.Cryptography.Xml 9.0.0` (bắc cầu qua EF Core Tools) từng gây **8 advisory
  high severity / 16 warning NU1903**. Đã ghim lên **10.0.12** bằng
  `CentralPackageTransitivePinningEnabled` trong `Directory.Packages.props` ⇒ build còn **0 warning**.
- ✅ **Đã vá:** `Volo.Abp.AutoMapper` từng kéo **AutoMapper 14.0.0** có advisory high severity
  (GHSA-rvv3-g6hj-g44x) ⇒ đã **bỏ hẳn AutoMapper**, map tay trong `ChannelAppService`.
- `ApplicationService.ObjectMapper` sẽ **lỗi lúc chạy nếu dùng** (vì đã bỏ AutoMapper). Dùng map tay,
  hoặc thêm object mapper an toàn (ví dụ Mapperly — source generator, không reflection).
- ✅ **Đã sửa ở Phase 0:** `AddAlwaysAllowAuthorization()` đã bị xoá — API đi qua OpenIddict + permission.
  ⚠️ Nhưng **mọi app service mới phải có `[Authorize]` ở cấp class**, vì ABP không tự bảo vệ application service.

---

## 7. Bước tiếp theo đề xuất

| Giai đoạn | Nội dung | Thời lượng ước tính |
|---|---|---|
| **P0** | Chạy end-to-end (Docker + migration + host), xác nhận worker động hoạt động; thêm Identity/OpenIddict | 3–5 ngày |
| **P1** | Webhook nhận đơn từ Shopee + handler tạo đơn idempotent; job đối soát; UI admin tối thiểu (danh sách shop + đơn) | 2–3 tuần |
| **P2** | Catalog/kho/giá đầy đủ + API đẩy tồn kho lên sàn; connector Lazada; cổng MoMo/payOS | 4–6 tuần |
| **P3** | Storefront (MVC/Blazor/Next.js) + checkout + vận chuyển/thuế | 4–8 tuần |

---

## 8. Ghi chú giấy phép

- **ABP Framework: LGPL-3.0-only** — dùng trong sản phẩm đóng được; cần attribution và kèm bản sao GPL/LGPL.
- Khung này **chỉ dùng module MIỄN PHÍ của ABP** ⇒ chưa cần license thương mại.
- Nếu sau này muốn dùng **SaaS Pro** (UI + auto-migrate DB theo tenant) hoặc `Volo.Payment`:
  cần **ABP Team $2.999 + VAT/năm** (3 dev seat).
  ⚠️ Trước khi mua, đọc kỹ xung đột **FAQ ("perpetual license") vs EULA §2.3/§4.3 ("disabling code… unusable")**
  — chi tiết trong `D:\Projects\Tools\WF\abp-framework-assessment.md` §5.3.
