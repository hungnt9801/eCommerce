# Báo cáo thực thi Phase 0 — đưa module ABP vào source code

**Ngày:** 09/10/2026 · **Trạng thái:** ✅ hoàn thành phần code · ⏳ chưa kiểm chứng được luồng cần database
**Commit:** `fc84e80` (checkpoint) → `d59128c` (thêm module + auth) → `adb8990` (sửa 3 lỗi thực chiến)

---

## 1. Đã làm gì

```powershell
# 8 module ABP, cài bằng ABP CLI 10.7.0 (đúng tên trong danh mục `abp list-modules`)
abp add-module Volo.Identity             # user/role/permission/OU/security log
abp add-module Volo.PermissionManagement
abp add-module Volo.SettingManagement
abp add-module Volo.FeatureManagement
abp add-module Volo.AuditLogging
abp add-module Volo.BackgroundJobs
abp add-module Volo.OpenIddict           # auth server (thay IdentityServer)
abp add-module Volo.Account              # chỉ được phần Application (xem §3.3)
abp add-package Volo.Abp.OpenIddict.AspNetCore -p src/eCommerce.HttpApi.Host/eCommerce.HttpApi.Host.csproj
```

Kết quả kiểm chứng:

| Hạng mục | Kết quả |
|---|---|
| Biên dịch | ✅ **0 lỗi / 0 warning** |
| Test nghiệp vụ | ✅ **20/20 pass** (không hồi quy) |
| Migration | ✅ **3 migration**, tổng **46 bảng** (10 commerce/channel/payment + 17 Identity + 19 auth modules) |
| Host khởi động | ✅ `Initialized all ABP modules` + `Application started` |
| Swagger | ✅ HTTP 200 (87 KB, 8 endpoint) |
| `/connect/token` | ✅ trả lỗi OAuth chuẩn (ID2029 thiếu `grant_type`, ID2059 thiếu `username/password`) |
| API được bảo vệ | ⏳ chưa xác nhận được 401/403 — cần PostgreSQL (xem §5) |

**Ghi chú quan trọng:** `abp add-module` **tự nhận biết central package management** và ghi version vào
`Directory.Packages.props` (không gây lỗi NU1008 như tôi lo ban đầu). Đây là điểm cộng lớn của CLI 10.7.0.

---

## 2. 🚨 6 phát hiện CHỈ LỘ RA KHI CHẠY THẬT

Đây là phần giá trị nhất của Phase 0. Tất cả đều **build sạch** nhưng hỏng khi chạy:

| # | Vấn đề | Triệu chứng | Đã sửa |
|---|---|---|---|
| 1 | **Thiếu `Configure<AbpDbContextOptions>(o => o.UseNpgsql())`** | `AbpException: No configuration found for Microsoft.EntityFrameworkCore.DbContext!` → **mọi API chạm DB trả 500**. Mỗi module ABP tự đăng ký DbContext riêng (`OpenIddictDbContext`, `AbpAuditLoggingDbContext`…) nên chỉ cấu hình cho `eCommerceDbContext` là không đủ | ✅ `eCommerceEntityFrameworkCoreModule` |
| 2 | **Thiếu `DbSet` cho entity của module** | `The requested service 'IRepository<ChannelShop, Guid>' has not been registered` → supervisor worker của ChannelHub chết ngay nhịp đầu. Vì `AddDefaultRepositories(includeAllEntities: true)` chỉ tạo repository cho entity **có DbSet** | ✅ thêm `DbSet<Channel/ChannelShop/ChannelListing/PaymentTransaction>` vào `eCommerceDbContext` |
| 3 | **Thiếu `[Authorize]` ở cấp class** | ABP **không** tự bảo vệ application service. `GetShopsAsync`/`GetShopAsync` không có attribute nào ⇒ **API đọc dữ liệu là công khai**. Đã quan sát đúng hành vi này trong log (action thực thi khi chưa xác thực) | ✅ `[Authorize]` ở cấp class `ChannelAppService` |
| 4 | **`AbortOnConnectFail` mặc định = true** | Redis chưa lên ⇒ `ConnectionMultiplexer.Connect()` **ném exception lúc khởi động** (worker dọn token của OpenIddict resolve `IDistributedLockProvider` ngay trong `OnApplicationInitialization`) ⇒ **cả ứng dụng không start**. Rủi ro thật cả khi Redis restart lúc đang chạy | ✅ đặt `AbortOnConnectFail = false` |
| 5 | **ABP CLI 10.7.0 ghi sai tên type** | `<c>BackgroundJobsDomainSharedModule</c>` / `BackgroundJobsDomainModule` / `BackgroundJobsEntityFrameworkCoreModule` — **thiếu tiền tố `Abp`** ⇒ CS0246 ở 3 file | ✅ sửa thành `AbpBackgroundJobs*` |
| 6 | **CLI không thêm `using` cho sub-namespace** | `AbpPermissionManagementDomainIdentityModule` (namespace `.Identity`) và `AbpPermissionManagementDomainOpenIddictModule` (namespace `.OpenIddict`) không resolve | ✅ thêm `using` thủ công |

### Hành vi khác của CLI cần biết

- Nó thêm **một attribute `[DependsOn]` riêng cho mỗi module** và làm hỏng thụt lề class
  (C# vẫn hợp lệ nhưng rất khó đọc) ⇒ nên **gộp lại** sau mỗi lần chạy.
- Nó **không** thêm `[ReplaceDbContext(...)]` và **không** bắt DbContext implement `IIdentityDbContext`.
  ✅ **Điều này đúng với ABP 10.7**: migration chứng minh chỉ cần
  `builder.ConfigureIdentity()` là đủ để tạo **17 bảng Identity**.
  ⇒ **Checklist §7 mục 3 và 5 trong `abp-modules-plan.md` là theo pattern ABP CŨ — cần bỏ.**

---

## 3. Sửa lại kế hoạch: đính chính quan trọng

### 3.1 Tên module trong kế hoạch ban đầu SAI

Danh mục thật lấy từ `abp list-modules`:

| ❌ Tôi đã ghi trong kế hoạch | ✅ Tên đúng | Ghi chú |
|---|---|---|
| `Volo.Abp.Identity` | **`Volo.Identity`** | |
| `Volo.Abp.PermissionManagement` | **`Volo.PermissionManagement`** | |
| `Volo.Abp.SettingManagement` | **`Volo.SettingManagement`** | |
| `Volo.Abp.FeatureManagement` | **`Volo.FeatureManagement`** | |
| `Volo.Abp.AuditLogging` | **`Volo.AuditLogging`** | |
| `Volo.Abp.BackgroundJobs` | **`Volo.BackgroundJobs`** | |
| `Volo.Abp.OpenIddict` | **`Volo.OpenIddict`** | |
| `Volo.Abp.Account` | **`Volo.Account`** | |
| `Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic` | **`Volo.BasicTheme`** | hoặc `Volo.Abp.LeptonXTheme.Lite` |
| (không có trong danh mục) | **`Volo.TenantManagement`**, `Volo.CmsKit`, `Volo.Docs`, `Volo.VirtualFileExplorer`, `Volo.Blogging`, `Volo.Users`, `Volo.IdentityServer` (lỗi thời) | |

**Cách tra luôn đúng:** `abp list-modules` (20 module mã nguồn mở).

### 3.2 Hai lệnh CLI thực sự cần dùng

| Lệnh | Dùng khi |
|---|---|
| `abp add-module <TênTrongDanhMục>` | Thêm cả nhóm package của một module vào đúng tầng project. **Tự xử lý CPM.** |
| `abp add-package <PackageName> -p <csproj>` | Thêm **một** package vào **một** project (ví dụ `Volo.Abp.OpenIddict.AspNetCore` vào Host) |
| `abp get-source <module>` | Kéo **mã nguồn** module vào solution (module miễn phí: MIT/LGPL; module Pro: chỉ từ gói Business) |

### 3.3 Hệ quả của việc KHÔNG có project Web

`abp add-module Volo.Account` chỉ thêm được `Volo.Abp.Account.Application` +
`.Application.Contracts`; **phần UI (`*.Web`) bị bỏ qua** vì solution không có project Web.

⇒ Kết quả: **API có auth server nhưng KHÔNG có màn hình đăng nhập.**
Lấy token để test bằng cách gọi trực tiếp `/connect/token` (grant `password` hoặc `client_credentials`).
Muốn có UI login/admin ⇒ phải **tạo project Web** trước (việc của P1).

---

## 4. Cấu hình đã thêm

| File | Nội dung |
|---|---|
| `eCommercePermissionDefinitionProvider.cs` | Định nghĩa 12 permission (`eCommerce.Products/Inventory/Channels/Orders/Payments.*`). **Thiếu file này thì `[Authorize]` luôn từ chối** dù user là admin |
| `OpenIddictDataSeedContributor.cs` | Tạo scope `eCommerce` + client `eCommerce_App` (secret `1q2w3e*`, `ConsentType = Implicit` để không cần UI consent). Không có nó ⇒ `/connect/token` luôn `invalid_client` |
| `IdentityDataSeedContributor.cs` | Seed admin `admin@abp.io` / `1q2w3E*` **và cấp toàn bộ permission cho role `admin`** |
| `appsettings.Development.json` | `AuthServer:DisableTransportSecurityRequirement = true` — **chỉ để test HTTP ở localhost**; production phải để false |
| `eCommerceHttpApiHostModule` | Bỏ `AddAlwaysAllowAuthorization()`; thêm `app.UseAuthentication()`; Redis `AbortOnConnectFail=false` |

---

## 5. ⏳ Phần CHƯA kiểm chứng được (và cách kiểm chứng)

Môi trường dựng khung **không có PostgreSQL, không có Redis, Docker daemon tắt** ⇒ không thể xác nhận:

- Cấp token thật từ `/connect/token` (cần DB cho OpenIddict đọc client/scope).
- Token có gọi được API **200** và API có trả **401** khi không có token (cần DB cho permission check).
- Seeder chạy đúng (admin user + grant permission + OpenIddict client).
- Luồng **distributed lock per-shop** khi refresh token Shopee (cần Redis).

**Chạy 4 lệnh này trên máy có Docker Desktop đang bật để hoàn tất kiểm chứng:**

```powershell
cd D:\Projects\Tools\eCommerce
docker compose up -d                                    # PostgreSQL 17 + Redis 7
dotnet run --project src/eCommerce.DbMigrator           # tạo 46 bảng + seed admin/scope/client
dotnet run --project src/eCommerce.HttpApi.Host         # chạy API

# (1) API phải trả 401 khi không có token
curl.exe -i http://localhost:5000/api/app/channel/shops

# (2) Lấy token bằng grant password
curl.exe -X POST http://localhost:5000/connect/token -d "grant_type=password&client_id=eCommerce_App&client_secret=1q2w3e*&username=admin@abp.io&password=1q2w3E*&scope=eCommerce offline_access"

# (3) Gọi API bằng token → kỳ vọng 200
curl.exe -H "Authorization: Bearer <access_token>" http://localhost:5000/api/app/channel/shops
```

**Kỳ vọng đọc log:** sau ~60 giây phải thấy supervisor của ChannelHub đăng ký worker động cho shop
và `ChnChannelShops.LastSyncedAtUtc` được cập nhật.

---

## 6. Việc tiếp theo

| # | Việc | Ghi chú |
|---|---|---|
| 1 | **Chạy 4 lệnh ở §5** để đóng Phase 0 | Cần Docker Desktop |
| 2 | Tạo project **`eCommerce.Web`** (MVC) rồi thêm `Volo.Account` + `Volo.BasicTheme` trỏ vào đó | Mở khoá UI login + admin |
| 3 | Thêm `Volo.TenantManagement` khi bắt đầu làm multi-tenant thật | Miễn phí; nhưng auto-migrate DB per tenant là `Volo.Saas` (Pro) |
| 4 | Chuyển OpenIddict client/secret sang cấu hình + secret store | Đang hardcode trong seeder, **chỉ dùng cho dev** |
| 5 | Production: cấu hình chứng chỉ ký/mã hoá thật, tắt `AddDevelopmentEncryptionAndSigningCertificate` | |
