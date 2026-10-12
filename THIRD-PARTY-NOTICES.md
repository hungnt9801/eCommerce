# THIRD-PARTY NOTICES

Tài liệu này ghi lại **mọi thành phần bên thứ ba** mà dự án dùng, **giấy phép của chúng**, và
**quy tắc bắt buộc cho nhóm phát triển**: được lấy code từ đâu, không được lấy từ đâu.

> ⚠️ **Tài liệu này KHÔNG phải ý kiến pháp lý.** Các kết luận dưới đây dựa trên việc **đọc trực tiếp
> file LICENSE / metadata gói**, nhưng **phải được luật sư xác nhận** trước khi bàn giao sản phẩm cho khách.
> Phân tích đầy đủ (39 URL bằng chứng + 10 điểm cần luật sư xác nhận):
> `D:\Projects\Tools\WF\license-matrix-abp-ecommerce-2026-10.md`.

---

## 1. Thư viện đang dùng (dạng gói NuGet — KHÔNG copy source)

Danh sách version chính xác: xem `Directory.Packages.props`. Tất cả đều dùng dạng **binary/NuGet**,
không có mã nguồn bên thứ ba nào được copy vào repo này.

| Thành phần | Giấy phép | Bằng chứng đã đọc |
|---|---|---|
| **ABP Framework** (`Volo.Abp.*` — Ddd, EntityFrameworkCore, Autofac, AspNetCore.Mvc, Swashbuckle, EventBus, BackgroundWorkers, DistributedLocking, Caching, MultiTenancy…) | **LGPL-3.0-only** | `abpframework/abp/dev/common.props` → `<PackageLicenseExpression>LGPL-3.0-only</PackageLicenseExpression>` |
| **ABP Application Modules** (`Volo.Abp.Identity.*`, `PermissionManagement.*`, `SettingManagement.*`, `FeatureManagement.*`, `AuditLogging.*`, `BackgroundJobs.*`, `OpenIddict.*`, `Account.*`) | **LGPL-3.0-only** 🚨 *(KHÔNG phải MIT — xem §3)* | nuspec `volo.abp.identity.domain/10.7.0` → `<license type="expression">LGPL-3.0-only</license>`; `modules/<tên>/LICENSE` → **404** (không có license riêng) |
| Microsoft.EntityFrameworkCore / .NET runtime packages | MIT | metadata NuGet |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL License | metadata NuGet |
| StackExchange.Redis | MIT | metadata NuGet |
| DistributedLock.Redis (Medallion) | MIT | metadata NuGet |
| Swashbuckle.AspNetCore | MIT | metadata NuGet |
| xunit / Microsoft.NET.Test.Sdk (chỉ dùng khi test) | Apache-2.0 / MIT | metadata NuGet |

### Nghĩa vụ của chúng ta với LGPL-3.0 (đang dùng dạng thư viện)

| # | Việc phải làm | Trạng thái |
|---|---|---|
| 1 | Kèm **bản sao giấy phép** LGPL-3.0 (+ GPL-3.0 mà LGPL tham chiếu) khi phát hành sản phẩm | ⬜ **cần làm** trước khi phát hành |
| 2 | Ghi **notice** về việc dùng thư viện ABP | ✅ tài liệu này |
| 3 | **Không chặn** việc thay thế/bổ sung (re-link) thư viện — không obfuscate theo cách cản trở | ✅ .NET nạp assembly động, không obfuscate thư viện ABP |
| 4 | Nếu **sửa** thư viện ABP ⇒ phải chia sẻ phần sửa đó | ✅ hiện **không sửa** thư viện ABP (chỉ cấu hình + dùng API công khai) |

⚠️ **Việc #4 là lý do then chốt để KHÔNG copy source module ABP** — xem §3.

---

## 2. 🚫 TUYỆT ĐỐI KHÔNG copy code từ các nguồn sau

Đây là các nền tảng chúng ta **đã khảo sát để học thiết kế**. Copy code từ chúng sẽ làm **toàn bộ
sản phẩm của chúng ta** phải chịu giấy phép viral (GPL/AGPL), phá vỡ mô hình sản phẩm đóng.

| Nguồn | Giấy phép (đọc từ file LICENSE) | Vì sao không được copy |
|---|---|---|
| `nopSolutions/nopCommerce` | **NPL v4.0** = AGPL-3.0 + buộc hiển thị "powered by nopCommerce" trên **mọi** màn hình | Viral + attribution bắt buộc. Có bán commercial license nhưng **giá chưa xác minh được** |
| `smartstore/Smartstore` | **AGPL-3.0** | Viral, **kể cả khi chạy SaaS** (AGPL §13). Mua edition **không** thoát: base code của mọi edition giống bản open-source |
| `grandnode/grandnode2` | **GPL-3.0** | Viral. **Không bán** license để thoát GPL (chỉ bán LTS/support) |
| `grandnode/grandnode` (legacy 1.x, đã archive) | **nopCommerce Public License v3.0** | Viral + "powered by" |
| `simplcommerce/SimplCommerce` | 📛 **CHƯA XÁC MINH** — GitHub API & README ghi Apache-2.0 nhưng **không tìm thấy file LICENSE** (đã thử 5 đường dẫn → 404) | ❌ **Không dùng cho tới khi clone repo và xác minh được file LICENSE** |

### 🚨 Cảnh báo chiến lược: bàn giao source = "conveying"

Nếu codebase có GPL/AGPL, **bàn giao source cho khách chính là hành vi `conveying`** (GPLv3 §0) và kích hoạt
§5c (cấp phép **toàn bộ** tác phẩm theo GPL) + §6 (cung cấp Corresponding Source).
Khách nhận GPL/AGPL **tự động có quyền phân phối lại** (§10) — không thể ràng buộc bằng NDA.

⇒ Chiến lược *"chạy SaaS để lách GPL, chỉ bàn giao source cho khách trả nhiều tiền"* là **tự mâu thuẫn**:
nhánh bàn giao source chính là nhánh phá vỡ mô hình đóng. **Với dự án này: không dùng GPL/AGPL ở bất kỳ đâu.**

---

## 3. ⚠️ Ngoại lệ nguy hiểm: ABP application modules là LGPL, KHÔNG phải MIT

**Tài liệu ABP nói sai.** `docs/en/modules/identity.md` và `openiddict.md` ghi:
*"The source code is licensed with **MIT**, so you can freely use and customize it."*

Nhưng bằng chứng thực tế (tôi đọc trực tiếp):

| Bằng chứng | Nội dung |
|---|---|
| nuspec `volo.abp.identity.domain 10.7.0` | `<license type="expression">LGPL-3.0-only</license>` |
| `abpframework/abp/dev/common.props` | `<PackageLicenseExpression>LGPL-3.0-only</PackageLicenseExpression>` — **mọi module import file này** |
| `modules/<tên>/LICENSE` cho module ứng dụng | **404** — không có giấy phép riêng ⇒ theo giấy phép repo (LGPL-3.0) |

**Quy tắc cho nhóm:**

| Hành động | Cho phép? |
|---|---|
| Tham chiếu `Volo.Abp.*` qua **NuGet package** | ✅ **Được** (đang làm — xem §1) |
| Đọc source module ABP để **học cách dùng API** | ✅ Được |
| `abp add-module <X> --with-source-code` / `abp get-source` để **copy source vào repo** | ⚠️ **KHÔNG làm khi chưa có luật sư xác nhận** — đây là quyết định **pháp lý**, không phải kỹ thuật |
| **Sửa** source module ABP rồi dùng | ❌ **Không** — kích hoạt nghĩa vụ chia sẻ phần sửa (LGPL §2/§3) |

---

## 4. ✅ Nguồn ĐƯỢC PHÉP copy code (kèm attribution)

Bốn nguồn này **đã đọc file LICENSE thật** và cho phép copy vào sản phẩm đóng, với điều kiện
**giữ nguyên thông báo bản quyền + giấy phép**:

| Nguồn | Giấy phép | Ghi chú khi copy |
|---|---|---|
| `EasyAbp/EShop` (+ `EasyAbp/PaymentService`, `EasyAbp/Abp.WeChat`) | **MIT** | 🥇 **Nguồn tốt nhất** — viết trên ABP, cùng mô hình module; có `PackageLicenseExpression=MIT` trong `common.props` |
| `OrchardCMS/OrchardCore.Commerce` | **MIT** (đọc `License.md`: "MIT License, Copyright (c) 2018 OrchardCMS") | Module commerce tách theo Catalog/Price/Inventory/Tax/Shipping/Payment/Order — tham chiếu tốt cho ranh giới module |
| `dotnet/eShop` | **MIT** | Tham chiếu DDD + outbox/idempotency của Microsoft |
| `OrchardCMS/OrchardCore` (lõi) | **BSD-3-Clause** | ⚠️ Điều khoản 3 **cấm dùng tên "Orchard"** để quảng bá/endorse sản phẩm của ta |

**Khi copy, bắt buộc:**
1. Thêm mục vào **§5** bên dưới (nguồn, file đã lấy, ngày lấy).
2. Giữ **nguyên** header copyright trong file được copy.
3. Nếu sửa file đó, ghi rõ **"đã sửa bởi eCommerce team, ngày…"**.

---

## 5. Nhật ký code bên thứ ba đã copy

*(hiện **trống** — chưa copy dòng code bên thứ ba nào. Mọi thứ đang dùng đều là NuGet package.)*

| Ngày | Nguồn | File trong repo ta | File gốc | Giấy phép | Đã sửa? |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

### Quy ước header bắt buộc khi port code

Mọi file có code port từ nguồn MIT/BSD **phải** bắt đầu bằng header:

```csharp
// Ported from EasyAbp/EShop (MIT, Copyright (c) 2020 EasyAbp Team)
//   src/.../OrderManager.cs @ 48c75e8e
// Modified by eCommerce team on 2026-10-12: thêm chiều ChannelId, đổi sang StockLedgerEntry.
```

Ba dòng: **(1)** nguồn + giấy phép + copyright, **(2)** đường dẫn file gốc + commit, **(3)** ghi rõ nếu đã sửa.

**Nguồn đã xác minh dùng được** (xem §4): `EasyAbp/EShop` + `PaymentService` (MIT, `Copyright (c) 2020 EasyAbp Team`) ·
`OrchardCore.Commerce` (MIT, `Copyright (c) 2018 OrchardCMS`, file tên `License.md`) · `dotnet/eShop` (MIT, .NET Foundation).

### ✅ Đã xác minh: Lombiq Helpful Libraries là **BSD-3-Clause** — **được copy code**

Trước đây tài liệu này chặn việc copy file OCC có `using Lombiq.*` vì chưa đọc giấy phép Lombiq. **Đã xác minh xong:**

| Bằng chứng | Nội dung |
|---|---|
| `Lombiq/Helpful-Libraries` file **`License.md`** (⚠️ một số repo Lombiq dùng `Licence.md` — kiểm tra tự động phải thử **cả hai**) | `BSD-3-Clause`, `Copyright © 2011, Lombiq Technologies Ltd.` |
| nuspec `Lombiq.HelpfulLibraries.OrchardCore` v13.0.0 | `<license type="expression">BSD-3-Clause</license>` |
| **Không** có dual-license / không bán license phần mềm | Lombiq chỉ bán **hỗ trợ thương mại** (service) — khác biệt pháp lý quan trọng |

**Điều khoản 3 (nguyên văn) — cấm dùng TÊN, KHÔNG cấm copy code:**
> *"Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission."*

| Hành động | Được? |
|---|---|
| Copy code Lombiq vào sản phẩm đóng | ✅ **Được** — kèm copyright + điều kiện + disclaimer (source: giữ nguyên; **binary: tái hiện trong tài liệu/vật liệu kèm theo**) |
| Dùng `using Lombiq.*` trong file OCC mà ta copy | ✅ **Được** — `using` chỉ là chỉ thị namespace, **không phải** hành động copy code |
| Mở mã nguồn sản phẩm của ta | ❌ **Không bắt buộc** (BSD-3 không copyleft) |
| Đặt tên/logo/nhãn có **"Lombiq"**, nói "Lombiq certified", ngụ ý chứng thực | ❌ **Cần xin phép trước bằng văn bản** |

**⚠️ Hai rủi ro cần nhớ:**
1. **Dependency bắc cầu:** dùng `OrchardCore.Commerce` như NuGet package ⇒ Lombiq Helpful Libraries bị kéo theo
   ⇒ **vẫn phải ghi Lombiq vào tài liệu này** dù ta không copy code Lombiq.
2. **OCC có nhánh `ProjectReference` trỏ tới SOURCE Lombiq** (điều kiện `Exists($(LombiqHelpfulLibrariesPath))`
   trong `OrchardCore.Commerce.Abstractions.csproj`) ⇒ **nếu build OCC từ source theo nhánh đó, ta biên dịch
   mã nguồn Lombiq vào sản phẩm** ⇒ nghĩa vụ BSD-3 áp dụng **chắc chắn**. Kiểm tra khi build từ source.

**Ghi công đúng:** ghi **nguyên văn năm trong file** (`Copyright © 2011`) — Lombiq không cập nhật năm theo commit.

---

## 6. Quy tắc "học ý tưởng" an toàn (clean-room)

**Về nguyên tắc:** bản quyền bảo vệ **biểu hiện** (code cụ thể), **không** bảo vệ ý tưởng/phương pháp
(TRIPS Art. 9(2); EU 2009/24/EC Art. 1(2); Google v. Oracle 2021). Học mô hình module, luồng nghiệp vụ,
cách đặt trạng thái = **hợp pháp**.

**Nhưng ranh giới thực tế nằm ở "non-literal copying" / SSO** (structure, sequence, organization).
Các dấu hiệu **rủi ro cao**, phải tránh khi đọc nopCommerce/Smartstore/GrandNode:

| ❌ Không làm | ✅ Nên làm |
|---|---|
| Dịch 1:1 rồi đổi tên biến | Tự thiết kế lại theo yêu cầu của ta (đa kênh + VN) |
| Giữ nguyên **tên entity đặc trưng** (`ProductVariant`, `TierPrice`, `DiscountRequirement`, `ShoppingCartItem`…) | Đặt tên theo ngôn ngữ của ta và theo mô hình của ta |
| Giữ nguyên **cấu trúc thư mục** của họ | Tổ chức theo bounded context của ta (xem `business-modules-proposal.md`) |
| Sao chép **comment, resource key, thứ tự method** | Viết lại hoàn toàn |

**Khuyến nghị quy trình:** nếu một module lấy cảm hứng từ nguồn không-copy-được, dùng **clean-room**:
một người **chỉ đọc** nguồn và viết **đặc tả nghiệp vụ** (không viết code); người khác **chỉ đọc đặc tả**
và viết code. Tách thời điểm đọc/viết.

---

## 7. Việc phải làm trước khi phát hành

- [ ] Kèm bản sao **LGPL-3.0** + **GPL-3.0** trong gói phát hành (nghĩa vụ §1 việc #1).
- [ ] Xác minh **giá commercial license nopCommerce** nếu muốn dùng (hiện chưa đọc được — Cloudflare chặn).
- [ ] Xác minh **file LICENSE của SimplCommerce** trước khi dùng bất kỳ phần nào.
- [ ] **Luật sư xác nhận:** phạm vi copyleft khi copy source module LGPL của ABP vào cùng assembly với
      code nghiệp vụ độc quyền (điểm quan trọng nhất trong 10 điểm của báo cáo ma trận).
- [ ] Kiểm tra **chính sách thương hiệu** nếu định dùng tên "nopCommerce"/"Orchard"/"ABP" trong tài liệu marketing.
