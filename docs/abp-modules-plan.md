# Kế hoạch đưa module ABP vào source code

> Trả lời câu hỏi: *"Tôi chưa thấy các module Framework trong source code"*.
> **Kết luận ngắn:** các **framework module ĐÃ có trong source** — nhưng ở dạng **NuGet package**,
> không phải file `.cs`, nên không "nhìn thấy" khi mở cây thư mục. Cái **chưa có** là các
> **application module** (Identity, Tenant Management, OpenIddict…). Tài liệu này là kế hoạch đưa chúng vào.

---

## 1. Ba loại "module" — phân biệt trước khi lập kế hoạch

ABP tự định nghĩa **hai loại**, và với dự án của chúng ta cần thêm loại thứ ba (module của chính dự án):

| Loại | Định nghĩa (theo tài liệu ABP) | Trong repo hiện tại | Ví dụ |
|---|---|---|---|
| **1. Framework module** | *"core modules of the framework… do not have application/business functionalities"* — hạ tầng: caching, emailing, security, EF Core integration… | ✅ **ĐÃ CÓ — dạng NuGet** (24 `PackageReference`, 14 package) | `Volo.Abp.Ddd.Domain`, `Volo.Abp.EntityFrameworkCore.PostgreSql`, `Volo.Abp.DistributedLocking`, `Volo.Abp.BackgroundWorkers`, `Volo.Abp.EventBus` |
| **2. Application module** | *"implement specific application/business functionalities"* — có entity, service, API, UI riêng | ❌ **CHƯA CÓ** (cố ý, xem §3) | `Volo.Abp.Identity`, `Volo.Abp.TenantManagement`, `Volo.Abp.OpenIddict`, `Volo.Saas` (Pro), `Volo.Payment` (Pro) |
| **3. Module của dự án** | Module do chúng ta viết, cũng có entity/service/API | ✅ **ĐÃ CÓ — dạng source code** | `ChannelHub` (4 project), `PaymentHub` (3 project) |

### Vì sao framework module "không thấy" trong source code

Vì chúng được tiêu thụ dưới dạng **thư viện biên dịch (DLL)**, y hệt cách bạn dùng `Microsoft.EntityFrameworkCore`.
Cái nhìn thấy được trong repo là **hai chỗ khai báo**:

**(a) Khai báo version — `Directory.Packages.props` (central package management, 1 file duy nhất):**
```xml
<PackageVersion Include="Volo.Abp.Ddd.Domain" Version="10.7.0" />
<PackageVersion Include="Volo.Abp.DistributedLocking" Version="10.7.0" />
<PackageVersion Include="Volo.Abp.EventBus" Version="10.7.0" />
... (14 package framework)
```

**(b) Khai báo sử dụng + thứ tự khởi động — trong module class của từng project:**
```csharp
[DependsOn(
    typeof(AbpAutofacModule),                 // ← framework module
    typeof(AbpAspNetCoreMvcModule),           // ← framework module
    typeof(AbpCachingStackExchangeRedisModule), // ← framework module
    typeof(AbpDistributedLockingModule),      // ← framework module
    typeof(eCommerceEntityFrameworkCoreModule) // ← module của dự án
)]
public class eCommerceHttpApiHostModule : AbpModule { ... }
```

**Bằng chứng cụ thể — 14 framework module đang dùng:**

| Package | Project dùng |
|---|---|
| `Volo.Abp.Ddd.Domain.Shared` | eCommerce.Domain.Shared |
| `Volo.Abp.Ddd.Domain` | eCommerce.Domain, ChannelHub.Domain, PaymentHub.Domain |
| `Volo.Abp.Ddd.Application.Contracts` | eCommerce.Application.Contracts, ChannelHub.Application.Contracts |
| `Volo.Abp.Ddd.Application` | eCommerce.Application, ChannelHub.Application, PaymentHub.Application |
| `Volo.Abp.EntityFrameworkCore` | ChannelHub.EntityFrameworkCore, PaymentHub.EntityFrameworkCore |
| `Volo.Abp.EntityFrameworkCore.PostgreSql` | eCommerce.EntityFrameworkCore |
| `Volo.Abp.Autofac` | eCommerce.HttpApi.Host, eCommerce.DbMigrator |
| `Volo.Abp.AspNetCore.Mvc` | eCommerce.HttpApi.Host |
| `Volo.Abp.Swashbuckle` | eCommerce.HttpApi.Host |
| `Volo.Abp.Caching.StackExchangeRedis` | eCommerce.HttpApi.Host |
| `Volo.Abp.DistributedLocking` | eCommerce.HttpApi.Host, eCommerce.Application, ChannelHub.Application |
| `Volo.Abp.BackgroundWorkers` | eCommerce.HttpApi.Host, ChannelHub.Application |
| `Volo.Abp.EventBus` | ChannelHub.Application, PaymentHub.Application |
| `Volo.Abp.Caching` | (khai báo version; dùng qua `.StackExchangeRedis`) |

⇒ **Không cần "đưa" các module này vào source.** Nếu bạn copy source của `Volo.Abp.DistributedLocking` vào repo,
bạn tự nhận việc merge mỗi lần ABP ra bản mới mà **không được lợi gì** (trừ khi bạn cần vá chính nó).

---

## 2. Danh sách application module — bản chuẩn (đã lấy từ tài liệu ABP)

### Miễn phí (LGPL-3.0, có source công khai trên GitHub)

| Module | Dùng cho việc gì trong dự án này |
|---|---|
| **Identity** | User, role, claim, organization unit, permission (nền của toàn bộ phân quyền) |
| **Permission Management** | Lưu permission (Identity phụ thuộc nó) |
| **Setting Management** | Lưu setting theo tenant/user |
| **Feature Management** | Bật/tắt tính năng theo tenant/edition (nền của SaaS) |
| **Audit Logging** | Ghi log thao tác — cần cho đối soát |
| **Background Jobs** | Job bền vững (khác `BackgroundWorkers` in-process hiện tại) |
| **Account** | UI đăng nhập/đăng ký/quên mật khẩu |
| **OpenIddict** | Auth server chuẩn của ABP 10 (đã thay IdentityServer) |
| **Tenant Management** | Tạo/quản tenant (multi-tenant thật) |
| **CMS Kit**, **Blogging**, **Docs**, **Virtual File Explorer** | Nội dung/tài liệu (chỉ thêm nếu cần) |
| **Operation Rate Limiting** | Rate limit tầng nghiệp vụ — ⚠️ xem cảnh báo bên dưới |

> ⚠️ **Hai điểm cần xác minh lại trước khi dựa vào:**
> 1. **`Operation Rate Limiting`**: trang danh sách module **không** đánh dấu "(Pro)", nhưng trang chi tiết
>    của chính nó ghi *"You must have an **ABP Team or a higher license** to use this module"*.
>    Hai tài liệu **mâu thuẫn** ⇒ phải hỏi Volosoft bằng văn bản hoặc thử cài để biết chắc.
> 2. **IdentityServer** (bản cũ, tích hợp IdentityServer4) có trong danh sách miễn phí nhưng **đã lỗi thời** —
>    ABP 10 dùng **OpenIddict**. Không dùng IdentityServer cho dự án mới.

### Pro (cần license Team trở lên; **source code chỉ có từ gói Business**)

| Module Pro | Ghi chú cho dự án này |
|---|---|
| **SaaS** | UI quản tenant + **auto-migrate DB theo tenant**. Đây là lý do chính phải cân nhắc mua license nếu làm SaaS nhiều tenant nghiêm túc. ⚠️ Module **không mã hoá** connection string khi lưu. |
| **Payment** | Chỉ có Stripe / PayPal / 2Checkout / PayU / Iyzico / Alipay — **KHÔNG có cổng Việt Nam** ⇒ vẫn phải tự viết provider. Xem §5 P3 để dùng lai với `PaymentHub`. |
| **File Management** | Quản lý file dạng cây thư mục (thay `BLOB Storing` thô) |
| **Language Management** | Thêm/sửa ngôn ngữ lúc chạy |
| **Identity Pro / Account Pro / OpenIddict Pro** | Bản nâng cao + UI quản trị của các module miễn phí tương ứng |
| **CMS Kit Pro / Forms / Chat / GDPR / Twilio SMS / Text Template** | Chỉ thêm khi có nhu cầu cụ thể |

---

## 3. Vì sao hiện tại CHƯA có application module nào

Đây là **quyết định có chủ ý** khi dựng khung, không phải thiếu sót:

1. **Khung cần chứng minh phần khó trước** — `ChannelHub` (worker động, distributed lock, connector, idempotency)
   và `PaymentHub` (ký/xác thực cổng VN). Đó là phần **không mua được ở bất kỳ đâu**.
   Identity/Tenant Management là phần **cài là có** ⇒ thêm sau không rủi ro.
2. **Tránh phụ thuộc license khi chưa cần**: các module miễn phí thì không sao, nhưng nếu thêm
   `Volo.Payment`/`Volo.Saas` ngay thì khung sẽ **không build được** với người không có license Team.
3. **Tránh `abp add-module` ghi đè cấu trúc viết tay** trước khi có bản build/test ổn định (xem §6).

⚠️ **Hệ quả hiện tại phải nhớ:** Host đang dùng `AddAlwaysAllowAuthorization()` ⇒ **mọi API đang mở**.
Đây là **lý do số 1 phải làm Phase 0 ngay** trước khi có dữ liệu thật.

---

## 4. Ba cách đưa module vào source code (chọn theo mục đích)

> 🚨 **CẢNH BÁO GIẤY PHÉP — đọc trước khi chọn cách B/C/D**
> **Module ứng dụng của ABP là `LGPL-3.0-only`, KHÔNG phải MIT** (dù tài liệu ABP ghi "licensed with MIT").
> Bằng chứng tôi đọc trực tiếp:
> - nuspec `volo.abp.identity.domain 10.7.0` → `<license type="expression">LGPL-3.0-only</license>`
> - `abpframework/abp/dev/common.props` → `<PackageLicenseExpression>LGPL-3.0-only</PackageLicenseExpression>`
>   (mọi module đều import file này; `modules/<tên>/LICENSE` không tồn tại → 404)
>
> **Hệ quả:** dùng **NuGet package** thì an toàn cho sản phẩm đóng. Nhưng **copy source module vào sản phẩm
> đóng** (tức `--with-source-code` / `abp get-source`) là **quyết định pháp lý, không phải kỹ thuật** —
> phải hỏi luật sư trước. Xem `business-modules-proposal.md` §2.

| Cách | Lệnh | Khi nào dùng | Hệ quả |
|---|---|---|---|
| **A. PackageReference** *(mặc định)* | `abp add-module Volo.Abp.Identity` | 99% trường hợp: chỉ cần dùng module | Module vẫn là DLL. Nâng cấp = `abp update`. **Không** phải merge gì. |
| **B. Kèm source code** | `abp add-module Volo.Abp.Identity --with-source-code` | Khi **buộc phải sửa** module (patch bug, thêm hành vi) | ✅ Đây là cơ chế chính thức: *"Downloads the source code of the module to your solution folder."* Source được thêm thành project trong solution (`--add-to-solution-file`) ⇒ **bạn tự merge mỗi lần nâng cấp major**. ⚠️ Nếu bạn phát hành bản đã sửa của thư viện **LGPL** thì phải chia sẻ phần sửa đó. |
| **C. Tạo module mới từ template** | `abp add-module Volo.MyModule --new` | Khi tự viết một module mới theo chuẩn ABP | `--new` **luôn** kèm source code (mặc định) và **không** tạo migration (`--skip-db-migrations` luôn true). |
| **D. Source module PRO** | `abp add-module Volo.Payment --with-source-code` | Chỉ khi cần sửa module Pro | 🔴 **Chỉ gói Business $5.999 trở lên mới có source module Pro.** Gói **Team $2.999 KHÔNG có**. Và EULA §12.2 **cấm open-source hoá** sản phẩm chứa module PRO. |

Các option khác của `abp add-module` (đã xác minh bằng `abp add-module --help`):
- `-s|--solution <file>` — chỉ định solution (giải pháp ta có `eCommerce.slnx`).
- `-sp|--startup-project <path>` — project khởi động để sinh migration (của ta: `src/eCommerce.DbMigrator`).
- `--skip-db-migrations <true|false>` — có sinh migration mới hay không.
- `-v|--version` — mặc định lấy theo version ABP của solution (10.7.0).
- Công cụ yêu cầu **thư mục có file `.sln` hoặc `.slnx`** ⇒ solution của ta **hợp lệ**.

---

## 5. Kế hoạch theo giai đoạn

### Phase 0 — Xác thực & phân quyền (BẮT BUỘC, làm trước mọi thứ)

```powershell
cd D:\Projects\Tools\eCommerce
$env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"

dotnet tool install --global Volo.Abp.Cli --version 10.7.0   # ✅ đã xác minh chạy được

# ✅ TÊN ĐÚNG — lấy từ `abp list-modules` (kế hoạch ban đầu ghi Volo.Abp.* là SAI)
abp add-module Volo.Identity
abp add-module Volo.PermissionManagement
abp add-module Volo.SettingManagement
abp add-module Volo.FeatureManagement
abp add-module Volo.AuditLogging
abp add-module Volo.BackgroundJobs
abp add-module Volo.OpenIddict
abp add-module Volo.Account
abp add-module Volo.BasicTheme                 # hoặc Volo.Abp.LeptonXTheme.Lite

# Package bổ sung bắt buộc cho Host (không nằm trong nhóm module nào)
abp add-package Volo.Abp.OpenIddict.AspNetCore -p src/eCommerce.HttpApi.Host/eCommerce.HttpApi.Host.csproj
```

> ⚠️ **ĐÃ THỰC THI XONG (09/10/2026).** Kết quả thật — bao gồm **6 lỗi chỉ lộ khi chạy**,
> cách `abp add-module` sửa file, và những gì còn phải kiểm chứng —
> xem **[phase-0-execution-report.md](phase-0-execution-report.md)**.
> Đọc báo cáo đó trước khi làm Phase 1: nó ghi lại các bẫy mà tài liệu này dự đoán sai.

**Sau khi thêm, phải sửa Host (bắt buộc):**
- ❌ **Xoá** `context.Services.AddAlwaysAllowAuthorization();` trong `eCommerceHttpApiHostModule`.
- ✅ Thêm `app.UseAuthentication();` **trước** `app.UseAuthorization();`.
- ✅ Thêm `app.UseAbpRequestLocalization();`, `app.UseStaticFiles();` (cho UI theme).
- ✅ Thêm `app.UseConfiguredEndpoints()` sau `app.UseAbpOpenIddictValidation()`.
- ✅ Sinh migration: `dotnet ef migrations add Added_Identity_And_OpenIddict --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.DbMigrator`
- ✅ Chạy `dotnet run --project src/eCommerce.DbMigrator` (tạo bảng + seed admin user/role mặc định).

### Phase 1 — Vận hành & multi-tenant

```powershell
abp add-module Volo.Abp.TenantManagement      # multi-tenant thật (entity đã IMultiTenant sẵn)
abp add-module Volo.Blogging                  # chỉ nếu cần nội dung
```
- ⚠️ **Ranh giới license:** `TenantManagement` **miễn phí** cho entity + UI quản tenant,
  nhưng **UI + auto-migrate DB riêng cho từng tenant là `Volo.Saas` (Pro)**.
  Nếu chưa mua license, ta vẫn chạy multi-tenant được nhưng phải tự viết phần tạo/migrate DB per tenant
  (`IConnectionStringResolver` là miễn phí — xem `D:\Projects\Tools\WF\abp-framework-assessment.md` §5.2).
- Cân nhắc **Hangfire/Quartz** cho `BackgroundJobs`: `ChannelHub` hiện dùng `BackgroundWorkers` **in-process**
  (mất khi restart, mặc dù supervisor tự đăng ký lại). Job dài (đồng bộ lịch sử đơn) nên chuyển sang job bền vững.

### Phase 2 — Tiện ích (thêm khi có nhu cầu)

`Volo.Abp.CmsKit`, `Volo.Docs` (tài liệu nội bộ), `Volo.Abp.VirtualFileExplorer` (chỉ môi trường dev),
`Volo.Abp.OperationRateLimiting` (**xác minh license trước** — xem §2).

### Phase 3 — Chỉ khi mua license Team/Business

| Module | Việc cần làm |
|---|---|
| **Volo.Saas** (Pro) | Bật UI quản tenant + auto-migrate DB per tenant. ⚠️ **Mã hoá connection string** vì module không tự làm. |
| **Volo.Payment** (Pro) | **Không thay thế `PaymentHub`** — nó không có cổng VN. Chiến lược lai: viết adapter `IPaymentGateway` của ABP gọi vào `IPaymentProvider` của `PaymentHub` ⇒ được thêm UI/plumbing của ABP mà **giữ nguyên nghiệp vụ đã viết**. |
| **Volo.FileManagement**, **Volo.LanguageManagement**, **Volo.CmsKit.Pro** | Thêm theo nhu cầu |

---

## 6. Hai phương án thi hành (rủi ro khác nhau)

### Rủi ro chính cần biết trước
`abp add-module` được thiết kế cho solution **sinh từ template chính thức ABP**. Solution của ta **do người viết tay**,
có 3 khác biệt:
1. **Không có project `eCommerce.HttpApi`** riêng (ta gộp API vào Host).
2. **Không có project UI/Web** (`.Web` hoặc `.Blazor`).
3. `eCommerceDbContext` viết tay (không sinh từ template), và `DbMigrator` không theo mẫu chuẩn.

⇒ Công cụ **có thể** sửa đúng (vì ta vẫn theo convention `*.Domain`, `*.Application`, `*.EntityFrameworkCore`,
`*.DbMigrator`, và có `.slnx`), nhưng **không đảm bảo**. Vì vậy:

### Phương án A — Scratch rồi port *(KHUYẾN NGHỊ, rủi ro thấp)*

```powershell
mkdir D:\Projects\Tools\abp-ref && cd D:\Projects\Tools\abp-ref
abp new eCommerce.Ref -t app-modular-monolith -u mvc --dbms PostgreSQL
# chạy toàn bộ lệnh abp add-module của Phase 0 trong solution tham chiếu này
# ⇒ thu được "diff chuẩn": file nào, dòng nào cần thêm
```
Sau đó **port** vào solution thật:
- `ChannelHub` và `PaymentHub` **copy nguyên** (chúng là module độc lập, không phụ thuộc gì của app).
- Entity commerce + DbContext: chuyển sang kế thừa `IIdentityDbContext`, `ITenantManagementDbContext`… theo diff.
- `eCommerceHttpApiHostModule`: thêm `[DependsOn]` + pipeline auth theo diff.

**Ưu điểm:** biết chính xác cần sửa gì, không đoán. **Nhược điểm:** tốn ~0.5–1 ngày.
**Đây cũng là cách tôi sẽ làm** nếu bạn đồng ý, vì nó biến việc "thêm module" từ phán đoán thành đối chiếu.

### Phương án B — Thêm trực tiếp vào solution hiện tại

```powershell
cd D:\Projects\Tools\eCommerce
git init; git add -A; git commit -m "chore: khung truoc khi them module ABP"   # BẮT BUỘC: điểm rollback
abp add-module Volo.Abp.Identity
dotnet build eCommerce.slnx
git diff          # kiểm tra công cụ đã sửa đúng chưa
```
**Ưu điểm:** nhanh. **Nhược điểm:** nếu công cụ sửa sai, ta phải sửa tay — nhưng có `git` để so sánh/hoàn tác.

---

## 7. Checklist kỹ thuật cho MỖI application module thêm vào

Khi thêm một module (dù bằng CLI hay tay), **7 việc** này phải xong:

| # | Việc | Chi tiết |
|---|---|---|
| 1 | `PackageReference` vào đúng tầng | `.Domain.Shared`, `.Domain`, `.Application.Contracts`, `.Application`, `.EntityFrameworkCore`, UI — mỗi tầng một package |
| 2 | Thêm `typeof(AbpXxxModule)` vào `[DependsOn]` | Thường ở `eCommerceEntityFrameworkCoreModule` (tích hợp EF) và `eCommerceApplicationModule` (service) |
| 3 | `eCommerceDbContext` implement `IXxxDbContext` | ❌ **KHÔNG CẦN** với ABP 10.7 — đã kiểm chứng bằng migration: chỉ `builder.ConfigureXxx()` là đủ tạo bảng. (Cách này đúng cho pattern ABP cũ hơn) |
| 4 | `builder.ConfigureXxx()` trong `OnModelCreating` | Giống cách ta đã làm với `ConfigureChannelHub()` / `ConfigurePaymentHub()` |
| 5 | `[ReplaceDbContext(typeof(IXxxDbContext))]` | ❌ **KHÔNG CẦN** với ABP 10.7 — mỗi module dùng DbContext riêng, cùng database qua connection string `Default` (đã xác nhận qua log khởi động) |
| 5b | 🔴 **`Configure<AbpDbContextOptions>(o => o.UseNpgsql())`** | **BẮT BUỘC** — thiếu dòng này mọi API chạm DB trả 500 với lỗi *"No configuration found for Microsoft.EntityFrameworkCore.DbContext"*. Chỉ lộ khi chạy, không lộ khi build |
| 5c | 🔴 **Khai báo `DbSet<T>` cho entity của module** trên app DbContext | **BẮT BUỘC** — `AddDefaultRepositories(includeAllEntities: true)` chỉ tạo `IRepository<T>` cho entity CÓ DbSet. Thiếu ⇒ *"IRepository&lt;X, Guid&gt; has not been registered"* |
| 5d | 🔴 **`[Authorize]` ở cấp class cho mọi app service** | ABP **không** tự bảo vệ application service. Chỉ đặt `[Authorize]` ở vài method ⇒ các method còn lại là API công khai |
| 6 | Migration + seed | `dotnet ef migrations add …` rồi chạy `eCommerce.DbMigrator`; Identity cần seed admin, TenantManagement cần seed tenant mặc định |
| 7 | UI theme + localization | `Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic`/`LeptonXLite` cho MVC; `.Domain.Shared` tham chiếu module tương ứng để có mã lỗi/localization |

**Ghi chú:** mọi package ABP phải **cùng version 10.7.0**. Vì ta dùng central package management nên chỉ sửa **một file** `Directory.Packages.props`.

---

## 8. Dọn dẹp kèm theo (đã làm trong lần này)

- ✅ Sửa comment **sai** trong `Directory.Packages.props`: trước đây ghi *"version CHƯA được kiểm chứng vì
  không truy cập được nuget.org"* — thực tế `dotnet restore` **đã chạy thành công** và build **0 warning**.
- ✅ Xoá entry `Volo.Abp.AutoMapper` (không dùng nữa vì đã bỏ AutoMapper để loại advisory bảo mật).
- 📌 Entry `Volo.Abp.Identity.*` (4 dòng) hiện **đã khai báo version nhưng chưa project nào reference** —
  giữ lại vì chúng dùng ngay ở Phase 0. Nếu bạn muốn tránh nhầm lẫn, có thể xoá và để `abp add-module` tự thêm.

---

## 9. Đề xuất bước tiếp theo

| Ưu tiên | Việc | Ai làm | Thời lượng |
|---|---|---|---|
| 1 | **Phase 0 bằng Phương án A** (scratch rồi port) | Tôi làm được ngay | ~0.5–1 ngày |
| 2 | Quyết định **có mua license Team/Business hay không** — ảnh hưởng Phase 1/3 | Bạn | — |
| 3 | **Xác minh `Operation Rate Limiting` free hay Pro** (tài liệu ABP mâu thuẫn) | Tôi, khi có license hoặc bằng thử cài | 10 phút |
| 4 | Gửi câu hỏi bằng văn bản cho Volosoft về xung đột FAQ-vs-EULA khi hết hạn license | Bạn | — |
