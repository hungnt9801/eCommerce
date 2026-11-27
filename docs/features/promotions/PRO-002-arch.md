# `PRO-002` — Kiến trúc module `Promotions`: cây điều kiện AND/OR & engine khuyến mãi

> Vai **ARCH**, phiên **N93** (bản đầu) — cập nhật N94/N95/N98, chuỗi `gd3-1`, hàng `W24-1`/`W24-2`/`W24-3`/`W24-5` (`WP-24`), nhánh `feat/gd3-wp24-dieu-kien-km`.
> Tài liệu này **chỉ thiết kế** quanh SRS
> [`pro-002-cay-dieu-kien-engine-khuyen-mai.md`](./pro-002-cay-dieu-kien-engine-khuyen-mai.md) (`v0.14 — 27/11/2026`, sau đây gọi **SRS**);
> **không** code, **không** sinh migration, **không** sửa SRS.
>
> **Module chủ của mọi bảng/DTO/endpoint/mã lỗi/khoá dưới đây: `Promotions`** — module **mới**, hiện **0 dòng code**
> (`ls modules` ngày 27/11/2026: `Address, Catalog, ChannelHub, Customers, EInvoice, Inventory, Messaging, Notification, PaymentHub, Pos, Pricing, Reconciliation, Reporting, Sales, Signing, Tax`; không có `Promotions`).
> Vì vậy mọi mục §3–§9 là **thiết kế mới**; mọi khẳng định “as-built” về module khác đều kèm `file:dòng` đã đối chiếu trong phiên N93.
>
> **Nguồn quyết định:** `D-151`…`D-163` (PRO-002 đã áp trong SRS v0.3–v0.10), `D-144` (`docs/nhanh/decisions.md:158`),
> `D-163` (`decisions.md:177`), `D-209` (`decisions.md:223` — WP-24 sau WP-16), `D-169` (tạm duyệt có điều kiện + 2 P3 — **đã đóng**),
> `D-270` (mẫu 4 quyền hẹp `Reports.*`, dùng lại cho `Promotions.*`), `D-17` (enum chỉ thêm ở cuối), `D-31`
> (số lượng `decimal(18,3)`), `D-155` (`Money`/`MoneyAllocator.PhanBo`), `D-151` (**`Add_Promotion_Module` là nguồn schema `Pro*` duy nhất**).
> Nhóm hàng `W24-3`: `D-285` (bề mặt `/api/v1/promotion/**` **7** route + service đọc `IPromotionRuleCatalogAppService`), `D-286` (§10.2 ngoài hàng — nợ có chủ),
> `D-287` (thứ tự `ba` → `arch` → `docrev` → `be` → `fe`), `D-288` (`PromotionDetailDto.Limits` **additive** — **không** thêm route thứ 8),
> `D-289` (`rule-catalog` vẫn trả dòng khi registry ∈ mà chưa seed — mặc định `PromotionRuleCostCatalog.Cua` + `IsSeeded = false` + `LogWarning`, **không** ném `0010`),
> `D-290` (nhãn tiếng Việt ở `PromotionRuleCostCatalog.Meta.Label` trong **code**, fallback `SystemName`; **không** cột DB/`IPromotionConditionRule`).
> Lộ trình: `docs/nhanh/07-ke-hoach-trien-khai.md:754` (WP-24) · song song/migration: `docs/nhanh/09-parallel-sessions.md:114-122`.
>
> **DOCREV:** [`docs/reviews/PRO-002-docreview.md`](../../reviews/PRO-002-docreview.md) — `VERDICT: DOC-OK`, **0 P1 / 0 P2**;
> 2 P3 của SRS (số test case 54→55 ở §15.4; trích số dòng cũ trong delta `pro-001` §6.1 đổi sang tên mục) **đã đóng ở N94/F1**;
> P3 vòng 3 của ARCH (thu hẹp allowlist `eCommerce.Messaging.EventCoverage` + tên test `RanhGioiPromotionsTests`) **đã đóng ở vòng 4**
> ([`PRO-002-arch-docreview.md`](../../reviews/PRO-002-arch-docreview.md)) — nay còn **0 P3**.
> Bản `v0.7`/`v0.8` (N95/`W24-3`) sửa **§4.3/§4.4/§12.1** theo `D-285`/`D-287`/`D-288` (và `D-289`/`D-290`) ⇒ chờ `docrev` kiểm **nhanh phần sửa**
> (**P1 = 0**, `D-287` ①(3)) — **không** phải DOC-OK toàn tài liệu.
>
> 📄 `-arch` là tài liệu **bổ trợ**, ngoài mẫu 16 mục áp cho SRS; tệp này giữ khuôn 15 mục của
> [`REP-001-arch.md`](../reporting/REP-001-arch.md).

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đang có trong code (đọc từ code, không viết theo trí nhớ)

| Hạng mục | Hiện trạng | Bằng chứng |
|---|---|---|
| Module `Promotions` | ❌ **chưa có** (0 project, 0 entity, 0 migration) | `ls modules` (27/11/2026); SRS §15.2 **F5** |
| Hạ tầng `Money` / phân bổ | ✅ có | `shared/eCommerce.Kernel/Money/Money.cs:36-44`, `shared/eCommerce.Kernel/Money/MoneyAllocator.cs:31-108`, `shared/eCommerce.Kernel/Money/RoundingPolicy.cs:25-37` |
| `IIdempotencyGuard` | ✅ có | `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:27-34` |
| Khoá phân tán (`IAbpDistributedLock`) | ✅ dùng thật ở module mới | `modules/Reporting/eCommerce.Reporting.Domain/ReportingLockKeys.cs:9-27`; `modules/Notification/eCommerce.Notification.Application/Dispatch/NotificationDispatchWorker.cs:79` (`TryAcquireAsync`) |
| Mẫu module 4 project (mới nhất) | ✅ `Notification`, `Reporting` | `modules/Notification/eCommerce.Notification.Domain`, `.Application.Contracts`, `.Application`, `.EntityFrameworkCore`; `modules/Reporting/...`; `eCommerce.slnx:27-31,82-86` |
| DbContext hợp nhất (không DbContext riêng) | ✅ module mới cung cấp hàm mapping, gọi **một lần** | `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:367-409` (`ConfigureNotification`), `:476` (`ConfigureReporting`) |
| `DbSet` là điều kiện để `IRepository<T>` resolve | ✅ ghi trong code | `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:63-67`, `:267-284`, `:102` |
| Unique `NULLS NOT DISTINCT` bằng SQL thô | ✅ tiền lệ migration | `src/eCommerce.EntityFrameworkCore/Migrations/20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:1-60` |
| `btree_gist` | ✅ **chỉ** dùng ở Tax + Reporting | `src/eCommerce.EntityFrameworkCore/Migrations/20261013012648_Add_Tax_Module.cs:118`; `.../20261126181842_Wp26_VanHanh_BaoCao.cs:168` |
| Permission khai **2 nơi** + `[Authorize]` cấp class | ✅ | `src/eCommerce.Domain.Shared/eCommerceConsts.cs:340-361`; `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:196-204`; `modules/Reporting/eCommerce.Reporting.Application/ReportAppService.cs:26-27` |
| Metric registry 3 tầng + test hai chiều | ✅ | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:13`, `:240-300` |
| Map mã lỗi → HTTP ở Host | ✅ | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:311-443` |
| `IPromotionEngine` / entity `Pro*` | ❌ **chưa có** (toàn bộ là thiết kế) | SRS §15.2 **F5/F7** |
| `OrderCancelledEto` | ❌ **chưa có** (`SAL-004`) | graph `codegraph_explore` không có ký hiệu này; SRS §7.2 #4 |
| Base class `IntegrationEventBase` | ❌ **không có trong code**; ETO as-built tự khai trường | `shared/eCommerce.Kernel/Events/INotificationEvent.cs`; `modules/Address/eCommerce.Address.Application.Contracts/Events/AddressEvents.cs:13-35` |

### 1.2 Phạm vi `WP-24` (IN)

1. **4 project mới** của module `Promotions` (Domain · Application.Contracts · Application · EntityFrameworkCore) + đăng ký `eCommerce.slnx`, DbContext hợp nhất, `[DependsOn]` Host.
2. **7 bảng** `Pro*` (gộp **CR-1…CR-7 + CR-11** của `PRO-001` theo `D-151`; `CR-11` đã áp riêng ở `D-147`), **một** migration `Add_Promotion_Module` do EF sinh.
3. **Domain**: aggregate `Promotion` + máy trạng thái §3.2 (sai ⇒ ném `0006`), cây `PromotionCondition`, `PromotionRuleRegistry` + `IPromotionConditionRule` thuần, guard 3 tầng, ngân sách chạy, `Money`.
4. **Contracts**: `IPromotionEngine` + request/result/breakdown + enum `PromotionRejectionReason` **12 giá trị `0…11`** (ghim ordinal).
5. **Application**: app service quản trị cây (10 method §6.2 SRS), engine in-process, job `promotion-expiry`, consumer hoàn lượt (hoãn tới khi có `OrderCancelledEto`), seeder `ProPromotionRuleCosts`, outbox/ETO.
6. **Tích hợp `Sales`**: chỉ thêm tham chiếu `Promotions.Application.Contracts` ở `Sales.Application` + gọi engine trong `CheckoutAppService` — **không** đụng `Pos`, **không** đụng `TinhTienDon`.
7. **Phân quyền** `eCommerce.Promotions.*` (9 dòng SRS §9: 8 quyền cấp được + `.Export` để GĐ3), **9 metric** (6 của PRO-002 + 3 của PRO-001), map **13 mã lỗi** → HTTP (`0001–0010` + `Money:0001` + `Messaging:0001/0002`), test/QA.

### 1.3 Ngoài phạm vi (OUT) — **không đụng**

- ❌ **Giảm theo SKU/danh mục/thương hiệu / `PerLine`** — nợ **`CR-P1`**, `D-152`. Engine v1 chỉ ra **một** `DiscountTotal` cấp đơn.
- ❌ **`FlashSale`** (`Kind = FlashSale`, `ProFlashSaleSlots`) — nợ **`CR-P2`**, `D-156`; v1 chặn bằng `CHECK` + validation.
- ❌ **`FreeShipping`** — nợ **`CR-S2`**, `D-158`; v1 chặn bằng `CHECK` + validation.
- ❌ **Khuyến mãi tại quầy `RetailBill`/`Pos`** — `D-142`/`D-153`; `Pos` **không** tham chiếu `Promotions`.
- ❌ **Điều kiện theo địa chỉ/vùng giao** — **chặn bởi Q8/R3-1** (GHTK chưa xác minh địa chỉ 2 cấp); **0** rule, **0** cột.
- ❌ **Khuyến mãi của sàn** trong cùng breakdown — **Q9**; v1 chỉ `Source = Internal`.
- ❌ **Sửa SRS**, sửa `Pos`, sửa `Pricing`, sửa `TinhTienDon`, sửa `Order`.
- ❌ **Tách DB/microservice** — monolith, đọc/ghi cùng DB.

---

## 2. Ranh giới module ABP

### 2.1 Chiều phụ thuộc (một chiều — `abp-module-rules` M1/M2/M5)

```
Host (eCommerce.HttpApi.Host) + Web (eCommerce.Web)   ← cả hai chạy in-process, cùng `[DependsOn]` (§B6)
  └─ [DependsOn] Promotions.Application ──▶ Promotions.Application.Contracts ──▶ Promotions.Domain
                                          └─▶ eCommerce.Kernel (Money, Locking, Metrics)
     Promotions.Application ──▶ Domain · Contracts · Kernel
                              · Abp.Ddd.Application · Abp.BackgroundWorkers
                              · Abp.DistributedLocking · Abp.EventBus · Abp.EntityFrameworkCore
     Promotions.Application ──▶ eCommerce.Messaging.Domain  (CHỈ `IEventCoverageProbe` — ngoại lệ B2)
     Promotions.Domain      ──▶ eCommerce.Kernel · eCommerce.Domain.Shared (chỉ hằng số dùng chung)
     Promotions.EntityFrameworkCore ──▶ Domain · Abp.EntityFrameworkCore
eCommerce.EntityFrameworkCore ──▶ Promotions.EntityFrameworkCore   (gọi ConfigurePromotions một lần)
Sales.Application ──▶ Promotions.Application.Contracts              (in-process IPromotionEngine)
```

**Project tham chiếu project — chốt cho BE:**

| Project | Tham chiếu **được phép** | Tham chiếu **cấm** |
|---|---|---|
| `eCommerce.Promotions.Domain` | `shared/eCommerce.Kernel`, `src/eCommerce.Domain.Shared` | mọi `*.Application*`, Host, module khác |
| `eCommerce.Promotions.Application.Contracts` | Domain, Kernel, `Volo.Abp.Ddd.Application.Contracts` | Application, EF, module khác |
| `eCommerce.Promotions.Application` | Domain, Contracts, Kernel, Abp Ddd/BackgroundWorkers/DistributedLocking/EventBus/EF; **`eCommerce.Messaging.Domain`** — **chỉ** để inject `IEventCoverageProbe` (ngoại lệ B2) | Host; **mọi** module nghiệp vụ khác và `Messaging.Application` |
| `eCommerce.Promotions.EntityFrameworkCore` | Domain, Abp.EntityFrameworkCore | Application, Host |
| `eCommerce.Sales.Application` | **thêm** `Promotions.Application.Contracts` | `Promotions.Domain`, `Promotions.Application` |
| `eCommerce.Pos.*` | — | **mọi** `eCommerce.Promotions.*` (`TC-38`) |

> 🔓 **Ngoại lệ có chủ — `Promotions.Application → eCommerce.Messaging.Domain`:** `Messaging` là module **hạ tầng** (outbox/inbox/idempotency/coverage), không phải module nghiệp vụ; `IEventCoverageProbe` nằm ở `modules/Messaging/eCommerce.Messaging.Domain/EventCoverage/IEventCoverageProbe.cs:17` và **Messaging không có** project `.Application.Contracts`. Tiền lệ as-built: `Promotions.Application` đi cùng đường với `Sales.Application` — cả hai đều tham chiếu `Messaging.Domain` (`modules/Sales/eCommerce.Sales.Application/eCommerce.Sales.Application.csproj`). **Hệ quả cho test ranh giới:** test quét `using` (tên: `RanhGioiPromotionsTests`) phải **allowlist đúng một** namespace `eCommerce.Messaging.EventCoverage` (**không** wildcard) trong `Promotions.Application`; **cấm** mở rộng sang `eCommerce.Messaging.Application`/`eCommerce.Messaging.EntityFrameworkCore` hoặc dùng probe cho việc khác ngoài phát ETO. Đây là phương án **(a)**; phương án (b) tách probe sang một project Contracts dùng chung là việc cross-cutting **ngoài WP-24** (nếu chọn (b): v1 **không phát** `PromotionAppliedEto` + ghi nợ, không để BE tự đoán).

Bằng chứng as-built cho mẫu “module mới = 4 project, chỉ tham chiếu Contracts của module khác”:
`modules/Reporting/eCommerce.Reporting.Application/eCommerce.Reporting.Application.csproj` (chỉ Contracts + Domain module nguồn);
`modules/Sales/eCommerce.Sales.Application/eCommerce.Sales.Application.csproj` (Contracts của Inventory/Pricing/Customers/ChannelHub/PaymentHub);
`modules/Pos/eCommerce.Pos.Application/eCommerce.Pos.Application.csproj` (**không** có `Promotions` — giữ nguyên).

### 2.2 Luật áp cho `WP-24`

| # | Luật | Cách giữ |
|---|---|---|
| B1 | Module **sở hữu** entity/bảng/mã lỗi | `PromotionErrorCodes` trong Domain (mẫu `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:10-35`); **cấm** thêm `eCommerce:Promotion:*` vào `eCommerceConsts.ErrorCodes` |
| B2 | Gọi chéo module **chỉ** qua `*.Application.Contracts` hoặc ETO | `Sales` → `Promotions.Application.Contracts`; `Promotions` **không** gọi module nghiệp vụ nào. **Ngoại lệ duy nhất:** `Promotions.Application` → `eCommerce.Messaging.Domain` **chỉ** cho `IEventCoverageProbe` (module hạ tầng; Messaging không có Contracts) — allowlist đúng `eCommerce.Messaging.EventCoverage` (không wildcard) trong test ranh giới (§2.1) |
| B3 | Module **không** phụ thuộc ngược app/host | 0 `using eCommerce.Web/HttpApi/Application` trong module; `Promotions.Domain` không biết EF/HTTP |
| B4 | **Không FK xuyên module** | `ProPromotionRedemptions.OrderId` là **tham chiếu mềm**, không FK sang `Sales` |
| B5 | `IRepository<T>` ⇒ **phải** có `DbSet` hợp nhất | 7 `DbSet` (§5.3); lưới `test/eCommerce.Tests/EfCoreRegistration/DbSetRegistrationTests.cs` |
| B6 | Project `.Application` có `AbpModule` + nằm trong `[DependsOn]` **Host và Web** | `PromotionsApplicationModule` thêm vào `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:40-92` **và** `src/eCommerce.Web/eCommerceWebModule.cs:47-86`; Web chạy **in-process** ⇒ dùng app service của module nào **phải** nạp module đó ở **cả** Host **và** Web — chỉ `ProjectReference` là **không đủ**; thiếu ở Host ⇒ API 404 body rỗng, thiếu ở Web ⇒ `CheckoutAppService` không resolve `IPromotionEngine` ⇒ `GET /DatHang` = **500** (`D-293`) |
| B7 | `ConventionalControllers` quét assembly `eCommerce*.Application` **đã nạp** | Host đã quét động (`eCommerceHttpApiHostModule.cs:445-478`) ⇒ **không** sửa chỗ đăng ký |
| B8 | Module nguồn **cấm** tham chiếu `Promotions.*` | `Sales`/`Pos` không bị `Promotions` gọi ngược; test `TC-38` |
| B9 | Mọi entity **có** `TenantId` + tenant filter | 6/7 bảng `IMultiTenant`; **ngoại lệ có chủ**: `ProPromotionRuleCosts` là bảng tra **toàn cục**, không tenant (SRS §5.2) — ghi rõ để test phân quyền/tenant không đỏ nhầm |

### 2.3 Ngoại lệ / nợ ranh giới đã biết (không sửa ở WP-24)

- `OrderCancelledEto` (SAL-004) **chưa có**; khi thêm **phải** đặt ở `Sales.Application.Contracts` để `Promotions` chỉ tham chiếu Contracts (M2). Nếu ai đặt ở `Sales.Domain` thì consumer phải tham chiếu Domain ⇒ **vi phạm** — xem `AQ9`.
- ETO as-built tự khai trường, **không** có `IntegrationEventBase` (`AddressEvents.cs:13-35`); SRS §7.1 gọi tên base class không tồn tại — xem §12 `docDeviation` D3.

> 📌 **Vi phạm ranh giới MỚI do `WP-24`: 0** (`boundaryViolations: []`).

---

## 3. Thiết kế tầng Domain

### 3.1 Enum & hằng số (một nguồn, đóng, append-only — `D-17`)

| Enum | Giá trị v1 | Lưu DB | Ghi chú |
|---|---|---|---|
| `PromotionKind` | `Voucher`, `Discount`, `FlashSale` | **chuỗi** `varchar(32)` | `FlashSale` **tồn tại trong enum** nhưng bị `CHECK` chặn tạo (`D-156`); candidate tự động **chỉ** `Discount` |
| `PromotionStatus` | `Draft`, `Approved`, `Active`, `Paused`, `Expired`, `Cancelled` | **chuỗi** `varchar(32)` | máy trạng thái §3.2 |
| `PromotionDiscountKind` | `Percentage`, `FixedAmount`, `FreeShipping` | **chuỗi** `varchar(16)` | `FreeShipping` bị `CHECK` chặn (`D-158`) |
| `PromotionConditionGroupType` | `And`, `Or` | **chuỗi** `varchar(8)` | chỉ node nhóm |
| `PromotionRejectionReason` (Contracts) | 12 giá trị `0…11` | không lưu DB (chỉ breakdown/log) | ghim ordinal + test `TC-46` (iv) |

> ⚠️ SRS chốt **lưu chuỗi** cho `Kind`/`Status`/`DiscountKind` để `CHECK` đọc được; module mới nhất as-built dùng `HasConversion<int>()`
> (`modules/Notification/eCommerce.Notification.EntityFrameworkCore/NotificationDbContextModelCreatingExtensions.cs:41-43`). Xem §12 D1 — **giữ SRS**.

### 3.2 Aggregate `Promotion` (bảng `ProPromotions`) — máy trạng thái **ném lỗi**

- Kế thừa `FullAuditedAggregateRoot<Guid>, IMultiTenant`; **chỉ** constructor/工厂 và method chuyển trạng thái; **cấm** `SetStatus(...)` công khai.
- Bảng chuyển trạng thái **đúng** SRS §3.2; mọi chuyển không có trong bảng ⇒ `BusinessException(PromotionErrorCodes.InvalidStatusTransition)` = `eCommerce:Promotion:0006`.
- `Approve()` chỉ từ `Draft`; `ActivateByJob(now)` chỉ từ `Approved` và `now >= StartsAtUtc`; `ExpireByJob(now)` chỉ từ `Active`/`Paused` và `now > EndsAtUtc`; `Pause()`, `Resume()`, `Cancel()` đúng bảng.
- **Cây bất biến từ `Approved`**: `SaveTree` chỉ chấp nhận ở `Draft`; trạng thái khác ⇒ `0006` (`D-159`). Không có “version cây thứ hai”, không có method nhân bản.
- **Không có đường sửa** trường giá trị/giới hạn (`D-150`): chỉ đặt trong `Create(...)`; `IPromotionAppService` **không** có `UpdateAsync` (allowlist 10 method — `TC-31`).
- Tiền: mọi phép tính qua `Money` + `IRoundingPolicy`; **cấm** `double`/`float`; **cấm** so `decimal` trần bằng `==`; phân bổ (nếu có) **chỉ** `MoneyAllocator.PhanBo` (`shared/eCommerce.Kernel/Money/MoneyAllocator.cs:31`).
- Mã lỗi: `PromotionErrorCodes` một class, tiền tố `eCommerce:Promotion:`, dải `0001–0099` (`D-144`); **không** dùng `0119`/`0104`/`0105`/`0115` (thuộc PRO-001).

### 3.3 Cây `PromotionCondition` (bảng `ProPromotionConditions`)

- Entity `Entity<Guid>, IMultiTenant` — **không** soft-delete, **không** audit (SRS §5.2; cần cho partial unique một gốc).
- `IsGroup` quyết định node nhóm/lá; node lai bị **cả** validator **và** `CHECK` chặn (`0004`).
- `Depth` **lưu sẵn** (1 = gốc) để `CHECK ("Depth" BETWEEN 1 AND 8)`; `MaxDepth` setting chỉ nhận `1…8`.
- Duyệt **tất định**: `ORDER BY SortOrder, Id`; `SortOrder` do app gán khi lưu cây.
- `SaveTreeAsync`: **xoá cũ trước, thêm mới sau** trong **cùng** transaction; lỗi `23505` của index một gốc ⇒ ánh xạ `0004` **theo tên constraint**.

### 3.4 `PromotionRuleRegistry` + `IPromotionConditionRule` (thuần, không I/O)

- `IPromotionConditionRule` **chỉ** có `string SystemName { get; }` và `bool IsSatisfied(PromotionRuleContext ctx)` — **không** `async`, **không** inject repository/HttpClient (SRS §6.1 quy tắc 5; `TC-29`).
- `PromotionRuleRegistry`: tập rule **do DI cung cấp**, khoá theo `SystemName` **ordinal**; khởi tạo một lần (singleton/`ISingletonDependency`) để kết quả tất định.
- v1 seed **4 rule**: `MinOrderAmount`, `ChannelScope`, `CustomerGroup`, `FirstOrderOnly`; **không** seed `SkuInclude`/`SkuExclude`/`CategoryInclude` (`D-149`/`D-152`).
- Lá phụ thuộc khách: kiểm `CustomerId` **trước**; `NULL` ⇒ `false`, không đọc `CustomerGroupId`/lịch sử đơn, không ném (`D-162`, `BR-08`).
- **Định nghĩa context** (chốt theo SRS §6.1): `PromotionEvaluationContext` = context engine dựng **một lần** (dữ liệu giỏ + `IClock.Now` **một lần** + đồng hồ ngân sách); `PromotionRuleContext` = **hình chiếu chỉ-đọc** truyền cho rule (không có bộ đếm ngân sách). Giữ **hai** tên; gộp là `Q29`.

### 3.5 Guard 3 tầng, ngân sách & phân loại rule

| Tầng | Thành phần | Đầu ra |
|---|---|---|
| **GHI** | `PromotionTreeValidator` (hàm thuần) + app service `SaveTreeAsync`/`ApproveAsync` | `0001` trần node/độ sâu/lá → `0002` tổ hợp → `0004` node lai/gốc ≠ 1/rule chưa từng đăng ký → `0010` rule đã gỡ |
| **CHẠY** | engine: guard tĩnh lặp lại **theo thứ tự cố định** (node → depth → leaf → declared cost) rồi duyệt short-circuit, đếm `LeafVisits`/`Comparisons` | vượt trần tĩnh: **ném** `0001`/`0002` ở **mọi nguồn**; vượt ngân sách chạy: **nguồn mã** ⇒ `0005`; **nguồn tự động** ⇒ bỏ candidate + log `Warning` + metric (`BR-07`, `D-158`/ADR-12) |
| **DB** | `CHECK` node lai, `Depth`, `Kind`, `DiscountKind`, trần `UsedCount + ReservedCount`, `TargetType`; unique một gốc; unique partial | `23514`/`23505` — hàng rào cuối, đúng cả khi Redis chết |

Phân loại `0004`/`0010` **chỉ** dựa vào **sự có mặt của dòng** trong `ProPromotionRuleCosts` (bất kể `RetiredAtUtc`), theo `D-154`; không dựa vào giá trị `RetiredAtUtc`.

`ProPromotionRuleCosts` là **nguồn khai báo chi phí tĩnh** cho `BR-06`; engine đọc **một lần mỗi candidate** (ngoài vòng lá), không đọc trong vòng lá (`NFR-04`).

### 3.6 Ngân sách chạy & tính tất định

- `PromotionEvaluationContext` giữ `Lines`, `ChannelCode`, `Currency`, `Subtotal`, `CustomerId?`, `CustomerGroupId?`, `IsFirstOrder?`, `NowUtc` (**một lần** từ `IClock`).
- Bộ đếm `LeafVisits`/`Comparisons` **thuộc engine** (biến cục bộ/struct), **không** đưa vào `PromotionRuleContext`.
- Trần mặc định **cấu hình được qua ABP Setting theo tenant**: `MaxNodes=64`, `MaxDepth=8` (trần cứng `1…8`), `MaxLeaves=32`, `MaxDeclaredCombinations=1024`, `MaxCandidatesPerCart=50`, `LeafVisitBudget=512`, `ComparisonBudget=5000` — mẫu khai báo `SettingDefinitionProvider`: `modules/Pricing/eCommerce.Pricing.Domain/PricingSettingDefinitionProvider.cs:10-50`.
- Cắt candidate theo `Priority DESC, Id ASC` khi vượt `MaxCandidatesPerCart` + log `Warning` + metric `promotion_candidates_truncated_total` (ADR-15).
- **Cấm** `DateTime.Now`, `Guid.NewGuid()` trong đường quyết định; **cấm** song song hoá đánh giá candidate.

### 3.7 Khoá phân tán & idempotency key (module sở hữu, **không** tiền tố)

`PromotionLockKeys` (một lớp, mẫu `ReportingLockKeys`/`TaxLockKeys` — `modules/Reporting/eCommerce.Reporting.Domain/ReportingLockKeys.cs:9-27`, `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:45-50`):

| Hàm | Giá trị | TTL |
|---|---|---|
| `Tree(tenantId, promotionId)` | `promotion:tree:{tenantId}:{promotionId}` | 30 s |
| `VoucherUsage(tenantId, code)` | `promotion:voucher:{tenantId}:{code}` | 10 s |
| `Job("expiry", tenantId)` | `promotion:job:expiry:{tenantId}` | 300 s |
| `Import(tenantId)` | `promotion:import:{tenantId}` | 120 s |

Idempotency key **chỉ** ở biên **có ghi**: `PromotionTree:…`, `Import:…`, `PromotionAuto:{tenantId}:{orderId}:{promotionId}`, `PromotionRefund:…`; `evaluate` **không** khoá (`D-159`). Khoá voucher (`Checkout:{tenantId}:{checkoutSessionId}:{code}`) thuộc `ReserveAsync`/`CommitAsync` của `PRO-001`.

> 📌 `PromotionTree:…` và `Import:…` khai trong `PromotionIdempotencyKeys` nhưng **chưa dùng** ở v1 (`D-284`): `SaveTreeAsync`/`ImportRuleCostsAsync` **không** bọc `IIdempotencyGuard` (allowlist ranh giới giữ đúng `eCommerce.Messaging.EventCoverage`, không nới); chống trùng theo **nghiệp vụ** = khoá phân tán `Tree`/`Import` + delete-then-insert cùng transaction + import chỉ cập nhật dòng có. Xem §12.1-D9.

---

## 4. Hợp đồng `Application.Contracts`

### 4.1 Chữ ký (chép **nguyên** SRS §6.1 — không tự đổi)

```csharp
namespace eCommerce.Promotions.Contracts;

public interface IPromotionEngine
{
    Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PromotionEvaluationRequest(
    Guid CheckoutSessionId,
    IReadOnlyList<PromotionCartLine> Lines,
    string ChannelCode,
    string Currency,
    decimal Subtotal,
    Guid? CustomerId,
    Guid? CustomerGroupId,
    bool? IsFirstOrder,
    string? VoucherCode,
    PromotionApplyMode Mode);

public enum PromotionApplyMode { CustomerRequested = 0, AutoApply = 1 }

public sealed record PromotionBreakdownItem(
    Guid PromotionId,
    Guid? NodeId,
    bool Satisfied,
    PromotionRejectionReason? RejectionReason,
    string Source);                      // v1 chỉ "Internal"

public sealed record PromotionEvaluationResult(
    bool Applied,
    decimal DiscountTotal,
    string Currency,
    IReadOnlyList<PromotionBreakdownItem> Breakdown,
    PromotionRejectionReason? RejectionReason,
    int LeafVisits,
    int Comparisons);

public enum PromotionRejectionReason
{
    NoCandidate = 0, ConditionFalse = 1, OutOfWindow = 2, ChannelMismatch = 3,
    UsageLimitHit = 4, BudgetExceeded = 5, CandidatesTruncated = 6, UnverifiedCod = 7,
    SupersededByVoucher = 8, DiscountCapExceeded = 9,
    ExcludedByNonCumulative = 10, CustomerRequired = 11
}

public interface IPromotionConditionRule
{
    string SystemName { get; }
    bool IsSatisfied(PromotionRuleContext context);
}
```

`PromotionCartLine` = `{ Sku, Quantity (decimal, `D-31`), UnitPrice, ProductGroup? }`; mọi giá trị tiền là `decimal` **cùng `Currency`** của request, **không** `double`.

> 📌 **Vị trí as-built của `IPromotionConditionRule`/`PromotionRuleContext`/`PromotionCartLine`:** khai trong **assembly Domain** (`modules/Promotions/eCommerce.Promotions.Domain/Contracts/PromotionRuleContracts.cs:1`) nhưng giữ **namespace `eCommerce.Promotions.Contracts`** — hợp lệ theo §2.1 vì chiều phụ thuộc `Contracts → Domain` cho phép (rule lá cần đọc dòng giỏ, mà Domain **không** được tham chiếu `Application.Contracts`). **Không** đổi chữ ký công khai (D8).

### 4.2 Ai gọi, phiên bản, tương thích

| Hợp đồng | Bên gọi | Kiểu tham chiếu | Ghi chú tương thích |
|---|---|---|---|
| `IPromotionEngine` | `Sales.Application` (`CheckoutAppService.TinhVaGiuChoAsync`) | project reference `Promotions.Application.Contracts` | additive; **không** expose qua HTTP cho `Sales` |
| `PromotionEvaluationRequest/Result` | `Sales` + app service `EvaluateAsync` (UI/CSKH) | record immutable | thêm trường ⇒ **phải** có default/`?`; **cấm** đổi thứ tự tham số record đã phát |
| `IPromotionConditionRule` | chỉ trong `Promotions` (registry) | in-module | **cấm** async/I/O |
| `PromotionRejectionReason` | `Sales` (breakdown hiển thị), metric `reason` | enum | **ghim ordinal 0…11**, chỉ thêm ở cuối (`D-17`); metric `reason` = tên enum hữu hạn |

### 4.3 App service quản trị (10 method — allowlist `TC-31`) + **service đọc rule catalog**

`GetAsync`, `GetListAsync`, `CreateAsync`, `EvaluateAsync`, `SaveTreeAsync`, `ApproveAsync`, `SetStatusAsync`, `ValidateVoucherAsync`, `GetRedemptionsAsync`, `ImportRuleCostsAsync` — đúng SRS §6.2. `SetStatusAsync` **không** nhận đích `Approved`/`Expired` (`0006`). **Không** có `UpdateAsync` (`D-150`).

As-built: `IPromotionAppService` khai **đúng 10** method (`modules/Promotions/eCommerce.Promotions.Application.Contracts/IPromotionAppService.cs:10-31`), `PromotionAppService` gắn `[Authorize(Promotions.Default)]` ở **cấp class** (`modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:25-26`).

**Service đọc riêng — `IPromotionRuleCatalogAppService` (`D-285` ①(ii))**

| Hạng mục | Nội dung |
|---|---|
| Interface | `IPromotionRuleCatalogAppService : IApplicationService` — **một** method `Task<IReadOnlyList<PromotionRuleCatalogItemDto>> GetRulesAsync()` |
| DTO | `PromotionRuleCatalogItemDto`: `SystemName` (string) · `Label` (tiếng Việt, **từ code** — xem hàng "Nguồn nhãn") · `RequiresVerifiedCod` (bool) · `DeclaredCostFactor` (int) · `RetiredAtUtc` (DateTime?) · **`IsSeeded`** (bool — `true` ⇔ có dòng `ProPromotionRuleCosts`, `D-289`) |
| Vị trí | `Promotions.Application.Contracts` — **service thứ hai** của **cùng** module `Promotions`; impl ở `Promotions.Application` |
| Permission | `Promotions.View` — `[Authorize]` **cấp class** trên impl (luật A1); **không** `[AllowAnonymous]` |
| Dữ liệu | Lặp **`PromotionRuleRegistry.SystemNames`** (`modules/Promotions/eCommerce.Promotions.Domain/Rules/PromotionRuleRegistry.cs:38-40`) — **không** lặp bảng DB ⇒ mỗi rule **đúng 1** dòng; ⨝ `ProPromotionRuleCosts` theo PK `Id` = tên rule (`modules/Promotions/eCommerce.Promotions.Domain/PromotionRuleCost.cs:10-15`) |
| Nguồn nhãn | `PromotionRuleCostCatalog.Meta.Label` — bảng metadata rule **trong code** ở `modules/Promotions/eCommerce.Promotions.Domain/Seed/PromotionRuleCostSeedContributor.cs:13-27` (nguồn sự thật `D-154`), **không** lấy từ DB; 4 nhãn v1: `MinOrderAmount` = "Giá trị đơn tối thiểu" · `ChannelScope` = "Kênh bán" · `CustomerGroup` = "Nhóm khách hàng" · `FirstOrderOnly` = "Khách mua lần đầu"; tên **ngoài** bảng ⇒ `Label = SystemName` (hiển thị thô, **không** tính là "có nhãn") — `D-290` |
| Ghi | **0** — chỉ đọc registry + repository, không ghi, không khoá, không idempotency |

- 🚨 **Không** thêm method thứ 11 vào `IPromotionAppService`: allowlist **10** method của `TC-31` **giữ nguyên**; test phản chiếu đang assert đúng 10 (`test/eCommerce.Tests/Promotions/PromotionSurfaceTests.cs:36-49`) vẫn xanh. Bề mặt mới kiểm bằng `PromotionV1SurfaceTests` (**7** route `/api/v1`), không đếm tổng số interface (§4.4).
- **Vì sao cần**: §10.1 buộc lá hiển thị **`Label` tiếng Việt** và nhãn COD theo `RequiresVerifiedCod`; hôm nay đường duy nhất chạm `ProPromotionRuleCosts` là `ImportRuleCostsAsync` (đường **ghi**, quyền `.Import`) — không có đường **đọc** nào (`D-285` ①(ii)). Bảng chi phí **không có cột nhãn** (`PromotionRuleCost.cs:10-15`), nên nhãn thuộc **code** (`PromotionRuleCostCatalog`) — bất biến "mọi `SystemName` trong registry **phải** có nhãn khác `SystemName`" kiểm bằng `TC-70` (d).
- **An toàn tenant**: `ProPromotionRuleCosts` là bảng **toàn cục** (không `TenantId` — §5.4) ⇒ service đọc không lộ dữ liệu chéo tenant; không cần `ICurrentTenant`.
- **Đã chốt — `D-289`** (khe deploy code-trước-seeder: `SystemName` ∈ registry nhưng **chưa có dòng** `ProPromotionRuleCosts`): vẫn **trả dòng** với `DeclaredCostFactor = 1` / `RequiresVerifiedCod = false` lấy từ **`PromotionRuleCostCatalog.Cua(name)`** (cùng bộ `O1`/`1`/`false` mà seeder và `PromotionTreeValidator.TinhDeclaredCost` đang dùng — **một** nguồn, **cấm** khai lại `1`/`false` trong Application), `IsSeeded = false`, `RetiredAtUtc = null`, `LogWarning` **một lần/lượt gọi** (liệt kê tên thiếu, trần ~10 + "…"). **Không** ném `0010` (chiều `0010` là **∉ registry và có dòng** — ngược lại), **không** bỏ khỏi danh sách (FE dựng lá từ catalog), **0** metric mới, **0** route, **0** migration. Có dòng ⇒ dùng **3 giá trị của dòng** + `IsSeeded = true` (bất kể `RetiredAtUtc`). Test `PromotionRuleCatalogTests`: bảng rỗng ⇒ **đủ** `SystemNames`, mỗi dòng `1`/`false`/`IsSeeded = false`; đổi vòng lặp sang duyệt DB hoặc `continue` khi thiếu dòng ⇒ **ĐỎ**.
- **Nhãn không đụng hợp đồng engine**: giữ nguyên `IPromotionConditionRule` (`SystemName` + `IsSatisfied`, `modules/Promotions/eCommerce.Promotions.Domain/Contracts/PromotionRuleContracts.cs:40-45`) ⇒ **`D8` giữ**; `PromotionRuleCostCatalog` là lớp `static` **nội bộ** của Domain, **không** phải kiểu công khai của `D8`; **không** cột `Label` ở DB ⇒ **0** migration (hàng `W24-3` cấm migration mới); **không** từ điển nhãn thứ hai ở Application (`D-290`).

**`PromotionDetailDto.Limits` — trường additive (`D-288` ①)**

| Hạng mục | Nội dung |
|---|---|
| DTO | `PromotionTreeLimitsDto`: `MaxNodes` / `MaxDepth` / `MaxLeaves` / `MaxDeclaredCombinations` (int) — **4** trần, **không** gồm ngân sách chạy |
| Gắn vào | `PromotionDetailDto : PromotionDto` — thêm **một** trường `Limits` (`modules/Promotions/eCommerce.Promotions.Application.Contracts/IPromotionAppService.cs:59-62` là chỗ khai) |
| Gán ở | `PromotionAppService.GetAsync` (`modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:60-82`) từ **`DocLimitsAsync()`** (`:507-520`) — **một cửa** đọc setting đã có, hiện dùng chung với `SaveTreeAsync` (`:177`) và `ApproveAsync` (`:221`); giá trị do `PromotionSettings.Doc` kẹp `MaxDepth` về **1…8** (`modules/Promotions/eCommerce.Promotions.Domain/PromotionSettings.cs:33-63`) |
| Phạm vi | **Additive** trên `/api/app` (**giữ đúng 10** method) và trên `/api/v1` (**giữ đúng 7** route — chỉ thêm trường, **không** route thứ 8); rollback = xoá 1 trường + 2 dòng map + test, 0 migration, 0 dữ liệu |

- **Vì sao trường chứ không phải route**: trần là **ABP Setting theo tenant** (`Promotions:MaxNodes`/`MaxDepth`/`MaxLeaves`/`MaxDeclaredCombinations`, `PromotionSettingDefinitionProvider.cs:26-34`), nên FE **không** được chép hằng số `64/8/32/1024` sang app (`D-33`; `Q1` còn `Open` ⇒ mặc định chỉ là **đề xuất**). Repo **không** có route đọc setting của module khác: `/api/v1/settings/**` (`src/eCommerce.HttpApi/Controllers/SettingsController.cs:23-101`) = **14** route cấu hình tổ chức/ma trận quyền, **0** route đọc ABP Setting (`grep -rn "application-configuration" apps/admin-web/app src` = **0**, đo 27/11/2026 — `D-288` ③). Thêm route thứ 8 chỉ để trả 4 số là mở bề mặt không cần thiết.
- Số **đếm** (node/độ sâu/lá/tổ hợp) **không** do server trả trong `GetAsync` — FE tự đếm từ `Nodes` + `rule-catalog` (`D-288` ①(1)); số **chính thức** chỉ có **sau** lần lưu thành công (`PromotionTreeValidationResultDto`).


### 4.4 REST

Route theo quy ước auto-controller ABP, **giữ bảng SRS §6.3** (10 route `/api/app/promotion/**`). `Sales` **không** gọi các route này; nó gọi engine in-process.

**Map mã lỗi → HTTP** (đăng ký trong `AbpExceptionHttpStatusCodeOptions` ở Host — mẫu `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:400-422`; **không** map ⇒ ABP trả `403` cho `BusinessException`):

| Mã | HTTP | Nguồn |
|---|---|---|
| `eCommerce:Promotion:0001` `TreeTooLarge` | 400 | Host |
| `eCommerce:Promotion:0002` `CombinationExplosion` | 400 | Host |
| `eCommerce:Promotion:0003` `ConditionCycleDetected` | 400 | Host |
| `eCommerce:Promotion:0004` `InvalidConditionNode` | 400 | Host |
| `eCommerce:Promotion:0005` `EvaluationBudgetExceeded` | **422** | Host |
| `eCommerce:Promotion:0006` `InvalidStatusTransition` | 409 | Host |
| `eCommerce:Promotion:0007` `UsageLimitExceeded` (mức **mã**) | 409 | Host |
| `eCommerce:Promotion:0008` `UnverifiedCodCondition` | **422** | Host |
| `eCommerce:Promotion:0009` `DiscountExceedsOrder` | 400 | Host |
| `eCommerce:Promotion:0010` `RuleNotRegistered` | 400 | Host |
| `eCommerce:Money:0001` | 400 | `XC-001` (đã map ở Host nếu có; nếu chưa ⇒ thêm cùng PR) |
| `eCommerce:Messaging:0001` `PayloadConflict` | 409 | `XC-002` (`IdempotencyGuard`) |
| `eCommerce:Messaging:0002` `DangXuLy` | 409 | `XC-002` |

`0119`/`0104`/`0105`/`0115` thuộc **`PRO-001`** — map ở WP của `PRO-001`, **không** nhân đôi tại đây.

#### Bề mặt `/api/v1/promotion/**` — **7 route** dựng bằng controller tường minh (`D-285`, hàng `W24-3`)

**Giữ nguyên** 10 route `/api/app/promotion/**` ở trên (ABP auto-controller của `IPromotionAppService`, `D-13`) — dùng cho Razor/tích hợp nội bộ; bảng SRS §6.3 vẫn là as-built của bề mặt đó. Màn Web Admin §10.1 đi **đường thứ hai**, tường minh, vì BFF + `packages/api-client/scripts/gen.mjs:35-45` **chỉ** nhận `/api/v1` (`openapi.v1.json` 31 path, **0** path promotion trước hàng này — `D-285` ②).

- **Controller**: `src/eCommerce.HttpApi/Controllers/PromotionsController.cs` — `[ApiController]`, `[Route("api/v1/promotion")]`, `[Authorize(eCommerceConsts.Permissions.Promotions.Default)]` ở **cấp class**, permission hẹp (`View`/`Manage`/`Approve`) khai `[Authorize]` ở **cấp method**; khuôn `NotificationTemplatesController.cs:28-38`.
- **Chỉ gọi app service**: inject `IPromotionAppService` (+ `IPromotionRuleCatalogAppService` cho `rule-catalog`); **không** repository, **không** `DbContext`, **không** lọc/truy vấn trong controller — mọi lọc/phân trang do app service (khuôn `NotificationTemplatesController.cs:12-38` "mọi đường đọc đi qua app service"). Controller nằm **ngoài** module `Promotions` (project `eCommerce.HttpApi` thuộc app) nên **không** đụng allowlist ranh giới `eCommerce.Messaging.EventCoverage` (`D-284` §2.1); chiều phụ thuộc `HttpApi → Promotions.Application.Contracts` là hợp lệ M1/M2.
- **Tham chiếu project — phải THÊM ở hàng `W24-3`**: `src/eCommerce.HttpApi/eCommerce.HttpApi.csproj` hiện **chưa** có `ProjectReference` tới `Promotions.Application.Contracts` (đọc 27/11/2026: 8 dòng reference — `Application.Contracts`, `Catalog`, `Inventory`, `Sales`, `Pos`, `Customers`, `Reporting`, `Notification`). Thêm **một** dòng `<ProjectReference Include="..\..\modules\Promotions\eCommerce.Promotions.Application.Contracts\eCommerce.Promotions.Application.Contracts.csproj" />` (additive, khuôn dòng `Notification`/`Reporting` sẵn có) — **chỉ** `.Application.Contracts`, **không** kéo `.Domain`/`.Application` vào project HTTP; quên ⇒ controller không biên dịch.
- **Danh sách trả `ApiPage<T>`** (`src/eCommerce.HttpApi/Dtos/ProductApiDtos.cs:9` — `Items`/`Total`/`Page`/`PageSize`), **không** trả `PagedResultDto` của ABP: controller map `PagedResultDto → ApiPage` sau khi app service trả (khuôn `NotificationTemplatesController.cs:44-58`), để client Next.js có hợp đồng `/api/v1` ổn định.
- **Route literal `rule-catalog` không đụng `GET /{id:guid}`** nhờ ràng buộc `guid` — khai **đúng** constraint, **không** đổi thành `{id}` (nếu đổi, `rule-catalog` rơi vào `404`/parse lỗi).
- **Hệ quả của service thứ hai**: `IPromotionRuleCatalogAppService` cũng là `IApplicationService` trong assembly `eCommerce*.Application` mà `ConventionalControllers` quét (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:466-469`) ⇒ ABP **tự sinh** thêm một route auto dạng `/api/app/promotion-rule-catalog/...` (tên thực xác nhận bằng swagger sau khi có code). Đó là **additive**; bảng 10 route `/api/app` ở trên vẫn đúng cho `IPromotionAppService`, `TC-31` đếm **method của interface** (không đếm route), và FE **không** dùng route auto này — FE đi `/api/v1/promotion/rule-catalog`.

| Method | Route | Query/Body | Trả về | Permission | Mã lỗi có thể |
|---|---|---|---|---|---|
| `GET` | `/api/v1/promotion` | `status?`, `kind?`, `page`, `pageSize`, `sort` | `ApiPage<PromotionListItemDto>` | `Promotions.View` | — |
| `GET` | `/api/v1/promotion/{id:guid}` | — | `PromotionDetailDto` (cây `Nodes` + **`Limits`** — trường additive `D-288`, §4.3) | `Promotions.View` | `404` (không có) |
| `POST` | `/api/v1/promotion` | `CreatePromotionDto` | `PromotionDto` | `Promotions.Manage` | validation `400` (`FlashSale`/`FreeShipping`/`Currency` — 0 mã mới) |
| `POST` | `/api/v1/promotion/{id:guid}/save-tree` | cây điều kiện | `PromotionTreeValidationResultDto` — **200 chỉ khi** cây hợp lệ; validator **NÉM** `0001`/`0002`/`0003`/`0004`/`0010` (và `0006` khi `Status ≠ Draft`), **không** trả `ViPham` | `Promotions.Manage` | `0001` `0002` `0003` `0004` `0006` `0010` |
| `POST` | `/api/v1/promotion/{id:guid}/approve` | — | `PromotionDto` | `Promotions.Approve` | `0001` `0002` `0004` `0006` `0010` |
| `POST` | `/api/v1/promotion/{id:guid}/set-status` | `{ status }` — **chỉ** `Paused`/`Active`/`Cancelled` | `PromotionDto` | `Promotions.Manage` | `0006` |
| `GET` | `/api/v1/promotion/rule-catalog` | — | `IReadOnlyList<PromotionRuleCatalogItemDto>` | `Promotions.View` | — |

Map mã → HTTP **dùng chung** bảng ở trên (Host `eCommerceHttpApiHostModule.cs:449-458`); hàng này **0** mã lỗi mới.

**4 op còn lại không mở ở v1** (`D-285` ④, `D-286`) — ghi nợ **có chủ**; thêm route nào trong 4 op này vào `/api/v1` mà **không** có dòng `D-xx` ⇒ `PromotionV1SurfaceTests` **ĐỎ**:

| Op | Vì sao chưa mở | Chủ nợ | Mốc mở |
|---|---|---|---|
| `POST …/evaluate` | cần **màn checkout** (ô mã giảm giá + câu theo `PromotionRejectionReason`); repo **0** màn checkout | **`SAL-002`** | hàng màn checkout |
| `POST …/validate-voucher` | cùng màn checkout; `ValidateVoucherAsync` **không** trả `SupersededByVoucher`/`ExcludedByNonCumulative` — hai lý do đó **chỉ** có ở `evaluate` (`D-286` ①(i)) | **`SAL-002`** | hàng màn checkout |
| `POST …/import-rule-costs` | không có màn trong §10; cần upload tệp + `Promotions.Import` | **`ops`/`WEB-*`** | khi có nhu cầu thật |
| `GET …/redemptions?orderId=` | thuộc khối "Vì sao giảm" ở **chi tiết đơn** (`BreakdownJson` chưa có trong `RedemptionDto`) | **`WEB-*`** | hàng màn chi tiết đơn |

**Kiểm bằng máy (`D-285` ⑥, `D-288` ⑥(ii)):** `PromotionV1SurfaceTests` — allowlist **đúng 7** route (`[Route]` + `[Http*]`), `[Authorize]` cấp class `Promotions.Default` + map method → permission, `IPromotionAppService` vẫn **đúng 10** method (`PromotionSurfaceTests` giữ xanh); probe swagger: `0006` ⇒ **409**, `0001` ⇒ **400**, thiếu token ⇒ **401**; thêm trường `Limits` **không** được làm số route khác 7.

**Đường lùi:** revert **1** commit (`PromotionsController.cs`, và `IPromotionRuleCatalogAppService`/DTO/impl + test nếu lùi cả bề mặt) + `git checkout packages/api-client/openapi.v1.json packages/api-client/src/schema.d.ts`; **0** migration, **0** dữ liệu, `/api/app/promotion/**` không đụng.


---

## 5. EF Core + DbContext hợp nhất

### 5.1 Quyết định: **dùng `eCommerceDbContext` hợp nhất** (theo as-built)

Hai module mới nhất **không** có DbContext riêng: `Notification` và `Reporting` chỉ có project `.EntityFrameworkCore` cung cấp hàm mở rộng mapping, được gọi **một lần** từ `eCommerceDbContext.OnModelCreating`
(`src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:367-409` gọi `ConfigureNotification`; `:476` gọi `ConfigureReporting`; comment “Gọi **MỘT LẦN**” tại `modules/Notification/eCommerce.Notification.EntityFrameworkCore/NotificationDbContextModelCreatingExtensions.cs:9`).
⇒ `Promotions` **theo đúng khuôn**: project `.EntityFrameworkCore` chỉ chứa `PromotionsDbContextModelCreatingExtensions.ConfigurePromotions(this ModelBuilder)` + `PromotionsEntityFrameworkCoreModule`; **không** tạo DbContext riêng.

### 5.2 Đăng ký DI / đồ thị module

1. `eCommerce.EntityFrameworkCore.csproj` thêm project reference tới `Promotions.EntityFrameworkCore`; `eCommerceEntityFrameworkCoreModule` thêm `[DependsOn(typeof(Promotions.EntityFrameworkCore.PromotionsEntityFrameworkCoreModule))]` (mẫu as-built `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs` với Reporting/Notification).
2. `eCommerceDbContext.OnModelCreating` thêm **một** dòng `global::eCommerce.Promotions.EntityFrameworkCore.PromotionsDbContextModelCreatingExtensions.ConfigurePromotions(builder);`.
3. **Host và Web** đều thêm `typeof(Promotions.PromotionsApplicationModule)` vào `[DependsOn]` (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:40-92` · `src/eCommerce.Web/eCommerceWebModule.cs:47-86`) — thiếu ⇒ API 404 body rỗng (Host) / `GET /DatHang` **500** do `CheckoutAppService` không resolve `IPromotionEngine` (Web, hồi quy `D-293`). Web chạy **in-process** ⇒ **chỉ** `ProjectReference` là **không đủ**, phải có **cả** `DependsOn`.
4. `eCommerce.slnx` thêm 4 project (mẫu `eCommerce.slnx:27-31,82-86`).

> 🚨 **Web `Replace` khoá phân tán (`D-294`):** nạp `PromotionsApplicationModule` kéo `AbpDistributedLockingModule` ⇒ ABP thay `IAbpDistributedLock` bằng `MedallionAbpDistributedLock`, loại này **cần** `IDistributedLockProvider` (Redis) ⇒ Web **không boot**. `eCommerceWebModule.ConfigureServices` gọi `context.Services.Replace(ServiceDescriptor.Singleton<IAbpDistributedLock, LocalAbpDistributedLock>());` (`src/eCommerce.Web/eCommerceWebModule.cs:117-123`, `using Volo.Abp.DistributedLocking`). **Phạm vi:** chỉ tiến trình UI — worker/job đã tắt tường minh (`:131-140`) nên không cần khoá liên tiến trình; **hệ quả:** 0 Redis, 0 package mới, hành vi Web y như trước `W24-5`. Host **không** đổi (vẫn `AbpDistributedLockingModule` + provider Redis riêng — `eCommerceHttpApiHostModule.cs:44-45,254-271`).

### 5.3 `DbSet` bắt buộc (`BR-18`) — **7 dòng mới**

`ProPromotions`, `ProPromotionConditions`, `ProPromotionRuleCosts`, `ProVoucherCodes`, `ProPromotionMappings`, `ProFlashSaleSlots`, `ProPromotionRedemptions`.

Thiếu một `DbSet` ⇒ `IRepository<T>` **không** resolve (“The requested service … has not been registered”) — comment as-built `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:102`; lưới `test/eCommerce.Tests/EfCoreRegistration/DbSetRegistrationTests.cs` phải quét **đích danh** 7 entity.

### 5.4 Mapping từng bảng (mô tả — BE sinh migration)

| Bảng | Mapping chính |
|---|---|
| `ProPromotions` | `ToTable("ProPromotions", t => …)` + `ConfigureByConvention()`; `Code` 64 · `Name` 256 · `Kind` 32 · `Status` 32 · `DiscountKind` 16 · **`ChannelScope` 64 nullable** (`null` = mọi kênh của `Order`); `Priority` int; `StartsAtUtc/EndsAtUtc` (`timestamptz`, `CHECK EndsAtUtc IS NULL OR EndsAtUtc > StartsAtUtc`); `DiscountValue/MaxDiscountAmount/MinOrderAmount` `numeric(18,4)`; `Currency` `char(3)`; **`MaxUsageTotal?`/`MaxUsagePerCustomer?`/`UsedCount`/`ReservedCount`** int; `TreeVersion` int; `RowVersion` optimistic; `HasCheckConstraint` cho `Kind <> 'FlashSale'`, `DiscountKind <> 'FreeShipping'`, `DiscountValue >= 0`, `Percentage ⇒ <=100 AND MaxDiscountAmount NOT NULL`, `UsedCount/ReservedCount >= 0` và trần `MaxUsageTotal`; index `(TenantId, Status, StartsAtUtc, EndsAtUtc)` |
| `ProPromotionConditions` | `ToTable` + `HasCheckConstraint` node lai, `InteractionType IN ('And','Or')`, `ParentId <> Id`, `Depth BETWEEN 1 AND 8`, `SortOrder >= 0`; FK `PromotionId` → `ProPromotions` **CASCADE**; FK tự thân `ParentId` → `Id` **Restrict**; unique `(TenantId, PromotionId, ParentId, SortOrder)` + **partial unique** `(TenantId, PromotionId) WHERE "ParentId" IS NULL` |
| `ProPromotionRuleCosts` | `ToTable`; **PK** `RuleSystemName` 128; `CostKind` 16; `DeclaredCostFactor` int `>=1`; `RequiresVerifiedCod` bool; `RetiredAtUtc` nullable — **không** `IMultiTenant` |
| `ProVoucherCodes` | `Code` 64; counters int; `Status` 16; unique `(TenantId, Code)`; `CHECK` **duy nhất** `UsedCount >= 0 AND ReservedCount >= 0 AND UsedCount + ReservedCount <= MaxUsage` (**thay** bản cũ, không song song) |
| `ProPromotionMappings` | `TargetType` 16; `Mode` 16; `CHECK ("TargetType" NOT IN ('Sku','Category','Brand'))` |
| `ProFlashSaleSlots` | `SalePrice numeric(18,4)`, `Currency char(3)`, `QuantityLimit/SoldCount numeric(18,3)`; `CHECK` slot — bảng **0 dòng** trong v1 vì `Kind <> 'FlashSale'` |
| `ProPromotionRedemptions` | FK `PromotionId` (Restrict), FK `VoucherCodeId?` → `ProVoucherCodes`; `OrderId?` **không FK**; **`CustomerId?`**; `Status` int (0–3); `UsageSequence` int `>= 1`; **`ReasonCode` 32 nullable** (`TTL_EXPIRED`/`ORDER_CANCELLED`/`ORDER_RETURNED`/`MANUAL`, bắt buộc khi `Released`/`Reversed`); `DiscountAmount numeric(18,4)`; `Currency char(3)`; `BreakdownJson` jsonb; `IdempotencyKey` 256; **`TreeVersion` int**; **`EvaluatedAtUtc`**; **`ExpiresAtUtc`/`AppliedAtUtc`/`ReleasedAtUtc`/`ReversedAtUtc` timestamptz nullable**; `CHECK` theo `Status` cho 4 cột thời điểm đó; `CHECK DiscountAmount >= 0` |

`CHECK` khai được bằng API EF thì dùng `ToTable(t => t.HasCheckConstraint(...))` (tiền lệ `modules/Pricing/eCommerce.Pricing.EntityFrameworkCore/PricingDbContextModelCreatingExtensions.cs:102-114`, `modules/Notification/eCommerce.Notification.EntityFrameworkCore/NotificationDbContextModelCreatingExtensions.cs:52-54`); dạng EF **không** mô hình hoá (`NULLS NOT DISTINCT`, partial unique, CHECK phức tạp) ⇒ SQL thô trong `Up()` (§9).

### 5.5 `NULLS NOT DISTINCT` + partial unique (SQL thô, **đích danh**)

| Index | Dạng |
|---|---|
| `IX_ProPromotions_TenantId_Code` | `UNIQUE … NULLS NOT DISTINCT` |
| `IX_ProPromotionConditions_TenantId_PromotionId_ParentId_SortOrder` | `UNIQUE … NULLS NOT DISTINCT` |
| `IX_ProPromotionConditions_MotGoc` | `UNIQUE … (TenantId, PromotionId) WHERE "ParentId" IS NULL` (`NULLS NOT DISTINCT` vô hại, giữ đồng dạng) |
| `IX_ProVoucherCodes_TenantId_Code` | `UNIQUE … NULLS NOT DISTINCT` |
| `IX_ProPromotionRedemptions_TenantId_IdempotencyKey` | `UNIQUE … NULLS NOT DISTINCT` |
| `IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_VoucherCodeId` | **`UNIQUE` … `NULLS NOT DISTINCT`** + partial `WHERE "VoucherCodeId" IS NOT NULL AND "OrderId" IS NOT NULL` |
| `IX_ProPromotionRedemptions_TenantId_PromotionId_CustomerId_UsageSequence` | **`UNIQUE` … `NULLS NOT DISTINCT`** + partial `WHERE "CustomerId" IS NOT NULL AND "Status" <> 2` |
| `IX_ProPromotionRedemptions_TenantId_OrderId_Voucher` | **`UNIQUE` … `NULLS NOT DISTINCT`** + `(TenantId, OrderId) WHERE "OrderId" IS NOT NULL AND "VoucherCodeId" IS NOT NULL` |
| `IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong` | **`UNIQUE` … `NULLS NOT DISTINCT`** + `(TenantId, PromotionId, OrderId) WHERE "OrderId" IS NOT NULL AND "VoucherCodeId" IS NULL` |
| `IX_ProPromotionRedemptions_TenantId_Status_ExpiresAtUtc` | index thường (job nhả lượt) |

> 🚨 **CẢ 4 index chống trùng trên `ProPromotionRedemptions` PHẢI là `UNIQUE` + `NULLS NOT DISTINCT`** (cả 4 đều có `WHERE` — filter **không** thay thế được `UNIQUE`/NND). Nếu BE chép thiếu `UNIQUE` (phát `CREATE INDEX` thường) thì hai dòng `(PromotionId, OrderId)` trùng được ghi ⇒ `UsedCount` +2, `DiscountTotal` bị giảm hai lần mà **không có lỗi DB nào** — đúng loại lỗi im lặng mà `D-157` sinh ra để chặn.
> **Cái gì ĐỎ khi sai (`TC-33`):** đọc `pg_indexes`/`pg_constraint` và khẳng định **đích danh** từng tên index có `UNIQUE` + `NULLS NOT DISTINCT`; ghi thẳng 2 dòng cùng `(TenantId, PromotionId, OrderId)` (cùng `VoucherCodeId` NULL) ⇒ **phải** `23505`; hai lượt `Reserved` (`OrderId` NULL) cùng mã ⇒ **không** `23505`.

Mẫu SQL thô + cảnh báo “EF sinh lại index sẽ **mất** thuộc tính NND”: `src/eCommerce.EntityFrameworkCore/Migrations/20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:1-60`. Test `TC-33` phải đọc `pg_indexes`/`pg_constraint` và kiểm **đích danh từng tên index** (không đếm tổng).

> 📌 PostgreSQL cắt định danh > 63 byte **âm thầm**, nên 3 tên dài được **rút gọn ≤ 63 byte** khi tạo: `IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort`, `IX_ProPromRedemptions_Tenant_Prom_Order_Voucher`, `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq`; đã đọc `pg_indexes` xác nhận đủ `UNIQUE` + `NULLS NOT DISTINCT` + `WHERE` đúng (D7).

---

## 6. Luồng nghiệp vụ

### 6.1 `EvaluateAsync` — **chỉ đọc** (CR-7, `D-151`)

1. `Sales` dựng **một lần** `PromotionEvaluationRequest` (dòng giỏ, kênh, `Subtotal`, khách: `CustomerId`/`CustomerGroupId`/`IsFirstOrder`).
   🚨 **Trước đó `Sales` PHẢI chặn** `CheckoutInput.OrderDiscount ≠ 0` ⇒ **NÉM** (SRS §6.1 quy tắc 6, `D-143` (2)): `DiscountTotal` của đơn **chỉ** đến từ engine. As-built hiện **truyền thẳng** `OrderDiscount = input.OrderDiscount` trong `modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:109` ⇒ **W24-2 phải thêm guard + test** (xem §12.1-D6); đây là điều kiện để không có “nguồn giảm giá thứ hai”.
2. Engine dựng `PromotionEvaluationContext` **một lần** (thêm `IClock.Now` **một lần**).
3. Nạp candidate: (a) tự động = `Status = Active` ∧ `Kind = 'Discount'` ∧ trong khung giờ ∧ mapping `Channel`/`Shipping`; (b) mã khách nhập = tra `ProVoucherCodes` ⇒ `PromotionId`; khử trùng theo `PromotionId`; cắt theo `MaxCandidatesPerCart`.
4. Nạp **cây** cho mỗi candidate (ngoài vòng lá), guard tĩnh, duyệt short-circuit, đếm ngân sách.
5. Kiểm “còn lượt” **chỉ đọc** (mức mã + mức promotion + trần theo khách); kiểm `CustomerRequired` cho candidate tự động + khách vãng lai.
6. Ghép theo **một luật** (cây → khách → ghép → tiền → trần), tính `Money` trên **cùng một** `Subtotal`, áp trần `≤ Subtotal` **không kẹp**.
7. Trả `PromotionEvaluationResult` (một `DiscountTotal` + breakdown). **0** ghi DB; **0** `IIdempotencyGuard`; **0** truy vấn trong vòng lá.

### 6.2 Đường **ghi** — do `Sales`/`PRO-001`, không do engine

| Nguồn | Đường ghi | Ghi chú |
|---|---|---|
| Voucher | `ReserveAsync`/`CommitAsync` của `PRO-001` | cùng UoW với đơn + outbox; `UPDATE … WHERE UsedCount + ReservedCount < MaxUsage`; 0 dòng ⇒ `0007` |
| Tự động có `MaxUsageTotal` | `ProPromotionRedemptions` **Applied** thẳng + `UPDATE ProPromotions SET UsedCount = UsedCount + 1 WHERE Id = @id AND UsedCount + ReservedCount < MaxUsageTotal` | 0 dòng ⇒ `0119` + rollback **cả đơn**; `UsageSequence` nhỏ nhất chưa bị chiếm; unique `(TenantId, PromotionId, OrderId) WHERE VoucherCodeId IS NULL` là hàng rào cuối (`D-157`) |

`PromotionAppliedEto`: phát **sau commit**, qua outbox (`useOutbox: true`), **không** phát trong vòng lặp candidate bị từ chối. As-built outbox bật tường minh trong `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs` (Outboxes/Inboxes dùng `eCommerceDbContext`).
**Phân loại sự kiện (E1):**
- `PromotionAppliedEto` **mang dữ liệu** (SRS §7.1 giao `Reporting`/`AccountingHub`; mất sự kiện ⇒ read-model/bút toán **thiếu** giảm giá) ⇒ **KHÔNG** gắn `INotificationEvent`; nơi phát **fail-closed** qua `IEventCoverageProbe` (`modules/Messaging/eCommerce.Messaging.Domain/EventCoverage/IEventCoverageProbe.cs:17`; là **ngoại lệ B2** — `Promotions.Application` tham chiếu `Messaging.Domain`, xem §2.1): chưa có người nhận ⇒ **NÉM**, không nuốt (E3). Vì vậy trước khi bật phát, `Reporting` (hoặc `AccountingHub`) **phải** có handler; nếu chưa, WP-24 **không** bật phát (ghi nợ, không phát “cho có”).
- `PromotionUsageLimitReachedEto`, `PromotionExpiredEto` = **thông báo** (`INotificationEvent`, mẫu `shared/eCommerce.Kernel/Events/INotificationEvent.cs`): dữ liệu đã nằm trong `ProPromotions`/`ProPromotionRedemptions`; thiếu handler ⇒ log `Warning`, không chặn.
Cả ba phát qua outbox và **không** phát khi rollback.

Handler hoàn lượt: chỉ `OrderCancelledEto` ⇒ `Applied → Released` (`D-148`); **chưa implement ở WP-24** vì ETO chưa tồn tại (§2.3, `AQ9`). Khi có: handler `IDistributedEventHandler<OrderCancelledEto>` + UoW `requiresNew: true, isTransactional: true` (luật E2), idempotency `PromotionRefund:…`.

### 6.3 Job `promotion-expiry` (kích hoạt + hết hạn, theo **giờ**)

- `AsyncPeriodicBackgroundWorkerBase`, đăng ký `OnApplicationInitializationAsync` (mẫu `modules/Notification/eCommerce.Notification.Application/NotificationApplicationModule.cs:24-31`).
- Mỗi lượt: giành `promotion:job:expiry:{tenantId}` (`SkipIfBusy`, log + metric khi bỏ lượt); quét `ProPromotions` có `Status ∈ {Approved, Active, Paused}`; gọi `ActivateByJob(now)`/`ExpireByJob(now)`; `RowVersion` chống chạy chồng; **không** dùng khoá idempotency theo kỳ.
- **Vấn đề tenant (mới, chưa có tiền lệ as-built):** worker chạy ở host (`CurrentTenant = null`); module **chưa** có `TenantManagement`. Thiết kế đề xuất: lấy `DISTINCT TenantId` từ `ProPromotions` (tắt tenant filter **có ghi lý do**: job hệ thống phục vụ mọi tenant, không trả dữ liệu cho client), rồi `using (ICurrentTenant.Change(tenantId))` cho từng tenant — xem `AQ8` (cần `DEC` vì chạm mẫu cách ly tenant).
- Mọi chuyển trạng thái sai/sớm/không có trong bảng ⇒ **không** phải lỗi job (giữ nguyên); chuyển tay sai ⇒ `0006`.

### 6.4 Seeder & import

- `PromotionRuleCostSeedContributor : IDataSeedContributor` (mẫu `modules/Notification/eCommerce.Notification.Domain/Seed/NotificationDataSeedContributor.cs:13,37-50`): **upsert** theo registry; dòng không còn trong registry ⇒ đặt `RetiredAtUtc = now()` nếu đang `NULL`; **0 `DELETE`/`ExecuteDelete`**; idempotent (chạy 2 lần = 0 thay đổi) — `D-154`.
- `ImportRuleCostsAsync`: idempotent `Import:{tenantId}:{sha256(fileContent)}`; **chỉ** cập nhật dòng đã có; tên ∉ registry ⇒ `0004`.

---

## 7. Phân quyền

### 7.1 Nhóm `Promotions.*` — khai **hai nơi** (bắt buộc)

| Permission | Method | Ghi chú |
|---|---|---|
| `eCommerce.Promotions` (gốc) | — | nhóm |
| `.View` | `GetAsync`, `GetListAsync`, `EvaluateAsync`, `ValidateVoucherAsync`, `GetRedemptionsAsync` | **không** `[AllowAnonymous]` |
| `.Manage` | `CreateAsync`, `SaveTreeAsync`, `SetStatusAsync` | **không** được tự duyệt (`SetStatusAsync → Approved` ⇒ `0006`) |
| `.Approve` | `ApproveAsync` | tách người dựng ≠ người duyệt = `Q5` |
| `.Import` | `ImportRuleCostsAsync` | |
| `.Voucher.Generate/.Publish/.OverrideLimit` | thuộc `PRO-001` (§9 SRS) | `.Publish`/`.OverrideLimit` **chỉ** admin tenant |

Khai tại **cả hai** nơi: `src/eCommerce.Domain.Shared/eCommerceConsts.cs:15` (hằng số) + `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:196-204` (định nghĩa) — thiếu một nơi ⇒ `403` cho **mọi người kể cả admin**; lưới `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs`.

### 7.2 `[Authorize]` cấp **class** + method hẹp

- `PromotionAppService` (app service công khai) **bắt buộc** `[Authorize(eCommerceConsts.Permissions.Promotions.Default)]` ở **class** (mẫu `modules/Reporting/eCommerce.Reporting.Application/ReportAppService.cs:26-27`); method cần quyền hẹp thêm `[Authorize(Permissions.Promotions.Manage)]` / `.Approve` / `.Import`.
- `PromotionEngine` **không** phải app service (không kế thừa `ApplicationService`, không `IApplicationService`) ⇒ **không** có controller/route; đăng ký DI (`ITransientDependency`) và **không** gắn `[Authorize]` (gọi in-process trong UoW đã xác thực của `Sales`).
- `ValidateVoucherAsync` **không** `[AllowAnonymous]`; `AllowlistCongKhaiTests` phải giữ đỏ nếu ai thêm (`test/eCommerce.Tests/Authorization/AllowlistCongKhaiTests.cs`).

---

## 8. Metric (9 mới = 6 + 3, cùng PR — `ADR-14`/`BR-20`)

| Tên | Loại | Nhãn (đã có trong `eCommerceMetricTags`) | Nguồn |
|---|---|---|---|
| `promotion_eval_duration_seconds` | Histogram | `kind` | engine |
| `promotion_eval_budget_exceeded_total` | Counter | `kind` | engine |
| `promotion_condition_rejected_total` | Counter | `kind`, `reason` (12 giá trị enum) | engine |
| `promotion_applied_total` | Counter | `kind`, `channel` | `Sales` sau commit |
| `promotion_usage_limit_hit_total` | Counter | `kind` | engine/`Sales` |
| `promotion_candidates_truncated_total` | Counter | `kind` | engine |
| `promotion_voucher_reserved_total` | Counter | `kind`, `result` | `PRO-001` |
| `promotion_voucher_released_total` | Counter | `reason` | `PRO-001` |
| `promotion_voucher_reservation_expired_total` | Counter | `kind` | `PRO-001` |

Cập nhật **cùng PR**: `eCommerceMetricNames` (hằng số + `All` + `KyHieuMetric`), thêm hàm phát, `XC-005` §5.1, test `TC-22` — registry as-built ở `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:13`, `:240-300`.
**9** metric `promotion_*` đã đăng ký; **5** có điểm phát thật (`PromotionEngine`: `promotion_eval_duration_seconds`, `promotion_eval_budget_exceeded_total`, `promotion_condition_rejected_total`, `promotion_usage_limit_hit_total`, `promotion_candidates_truncated_total`); **4** (`promotion_voucher_reserved_total`/`_released_total`/`_reservation_expired_total` = `PRO-001`; `promotion_applied_total` = `SAL-001`) nằm trong `ChuaCoNoiPhat` là **nợ có chủ**. Chỉ được thêm tên vào `ChuaCoNoiPhat` khi có đủ: (a) hạ tầng còn thiếu nêu rõ, (b) chủ nợ, (c) mốc đóng; xoá ngay khi có lời gọi phát thật — `MetricEmitterTests` đỏ cả hai chiều. `D-283`.
Thêm **cấm** `promotionId`, `voucherCode`, `customerId` vào `eCommerceMetricTags.Forbidden` (SRS §13); nhãn chỉ từ `Allowed`.

---

## 9. Migration expand-only + đường lùi

### 9.1 Kế hoạch (mô tả — **BE sinh** ở W24-2; hàng này **0** migration)

- **Đúng một** migration `Add_Promotion_Module` (`dotnet ef migrations add` — **cấm** viết tay; thiếu `.Designer.cs` ⇒ bị bỏ qua âm thầm), gộp **CR-1…CR-7 + CR-11** của `PRO-001` theo `D-151` (`CR-11` đã áp riêng ở `D-147`; bảng trống ⇒ **không** backfill `Currency`).
- **Thứ tự tạo bảng**: `ProPromotions` → `ProPromotionConditions` (FK promotion + tự thân) → `ProPromotionRuleCosts` → `ProVoucherCodes` → `ProPromotionMappings` → `ProFlashSaleSlots` → `ProPromotionRedemptions` (FK promotion + voucher). **`Down()` xoá theo thứ tự ngược**: `ProPromotionRedemptions` → `ProFlashSaleSlots` → `ProPromotionMappings` → `ProVoucherCodes` → `ProPromotionRuleCosts` → `ProPromotionConditions` → `ProPromotions`.
- **Xoá cây khi lưu (`SaveTreeAsync`)**: FK tự thân là `Restrict` (kiểm **ngay** từng dòng) ⇒ **không** xoá một câu theo `PromotionId` được nếu cha còn con. Thứ tự an toàn: xoá lá sâu nhất trước (`ORDER BY Depth DESC`, hoặc xoá lặp theo `Depth`), **rồi** mới chèn cây mới, **tất cả trong cùng transaction**; cách khác là đổi FK tự thân sang `NO ACTION` (kiểm cuối câu) — nếu BE chọn cách này phải ghi lý do trong migration.
- **Không** cần `btree_gist`: `EXCLUDE USING gist` chỉ có ở Tax + Reporting (`20261013012648_Add_Tax_Module.cs:118`, `20261126181842_Wp26_VanHanh_BaoCao.cs:168`); PRO-002 **không** có ràng buộc loại trừ miền.
- **SQL thô trong `Up()`** (sau khi EF sinh bảng/CHECK): tạo unique **NND** + partial index (§5.5) theo khuôn `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs`; `CHECK` dạng EF không mô hình hoá được nếu có ⇒ SQL thô kèm tên constraint **cố định**.
- **Seed**: `ProPromotionRuleCosts` qua `IDataSeedContributor` (upsert, 0 DELETE) — **không** seed bằng `INSERT` tay trong migration; **không** seed promotion nghiệp vụ.
- **Trần node/lá** **không** biểu diễn được bằng `CHECK` (điều kiện nhiều dòng) ⇒ validator + guard chạy; **không** trigger ở v1 (Q2).

### 9.2 Trước khi áp

1. `git status --short` sạch; **một chủ migration** (`docs/nhanh/09-parallel-sessions.md:114-122`).
2. `pg_dump` DB đang có dữ liệu + ghi đường dẫn tệp dump vào handover (M9).
3. Build binary **mới** rồi áp bằng `dotnet run --project src/eCommerce.DbMigrator` — **cấm** `dotnet ef database update` (M6); `DbMigrator` exit 0 **không** đủ, phải kiểm `__EFMigrationsHistory` + `\d` bảng thật.

### 9.3 Đường lùi (viết trước giải pháp — K-05/A8)

- **Cờ tắt**: `eCommerce.Promotions.Enabled = false` (ABP Setting) ⇒ `Sales` **không** gọi engine, `DiscountTotal = 0`; job/route vẫn tồn tại nhưng không tác động.
- **Revert code**: gỡ `PromotionsApplicationModule` khỏi `[DependsOn]` + gỡ dòng `ConfigurePromotions` + gỡ 7 `DbSet` (migration **không** revert ngay).
- **Giữ bảng**: 7 bảng `Pro*` ở lại (expand-only); `Down()` của migration xoá đủ 7 bảng theo thứ tự FK ngược **chỉ** dùng ở môi trường dev/test (`NFR-11`, `TC-27`).
- **Không** revert migration đã merge; sửa sai ⇒ migration mới.

---

## 10. Test / QA plan

### 10.1 Theo tầng (neo dải `TC-01…TC-51` + `TC-66…TC-69` của SRS §14.1 — SRS **không** có `TC-52…TC-65`, dải đó thuộc `PRO-001`)

| Tầng | Test | BR/NFR |
|---|---|---|
| Unit thuần (không DB) | `TC-01/02/03/04/06/07/08/10/11/13/19/23/24/25/28(a)(b)(f)/29/31(a)/32(a)(b)(c)/35/42(ii)/44(ii)/45/46/48/49/50(i)(iii)(vi)(vii)/51(i)(ii)(iii)(iv)/66/67(i)(ii)(iii)/69(vi)` | BR-01…12, 15, ADR-04/05/06/12/13/15 |
| Unit quét mã/phản chiếu | `TC-15/17/22/26/35/37/38` | BR-12/13/16/20, `D-152`/`D-153` |
| Integration PostgreSQL 17 thật (Testcontainers) | `TC-05/09/12/16/18/20/21/27/28(c)(d)(e)/30/32(d)/33/34/36/39/40/41/42(i)/43/44(i)/47/50(ii)(iv)(v)(viii)/51/68/69(i)–(v)(vii)` | BR-03/05/10/14/17/18, NFR-01/04/07/11/13/14 |
| Benchmark | `TC-41` p95 ≤ 20 ms cho cây sát trần, 50 dòng giỏ | NFR-01 |

### 10.2 Harness cần

- Fixture integration tạo tenant + promotion + cây + voucher; **đếm command EF** (`TC-21`); `IClock` giả (`TC-11`, `TC-50`); DB thật để kiểm `23505`/`23514`/NND.
- Lưới có sẵn phải mở rộng: `test/eCommerce.Tests/EfCoreRegistration/DbSetRegistrationTests.cs` (7 `DbSet`), `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs` (hằng số ↔ provider), `test/eCommerce.Tests/Authorization/AllowlistCongKhaiTests.cs`.
- Test bất biến **đích danh** tên index (`TC-33`) — không đếm tổng.
- **Cơ chế ĐỎ cho 2 bất biến vận hành:** (a) quét assembly `Promotions` tìm `Task.WhenAll`/`Parallel.` trong đường đánh giá ⇒ đỏ (§3.6 **cấm** song song hoá để giữ tất định); (b) đặt `eCommerce.Promotions.Enabled = false` ⇒ test `Sales` khẳng định engine **không** được gọi (`0` command, `DiscountTotal = 0`) — cờ tắt phải thật sự tắt, không chỉ là chữ trong tài liệu.
- Test ranh giới `using` **`RanhGioiPromotionsTests`** (ở `test/eCommerce.Tests/...`): khẳng định `eCommerce.Promotions.Application` chỉ `using` **đúng** namespace `eCommerce.Messaging.EventCoverage` (không `eCommerce.Messaging.*` wildcard, không `Messaging.Application`/`Messaging.EntityFrameworkCore`); đỏ nếu ai mở rộng.

### 10.3 `TC-xx` **chưa kiểm được** ở W24-1 (design-only)

- `TC-33/34/42(i)/43/44(i)` — cần migration + DB thật của W24-2.
- `TC-38` (Pos không tham chiếu) — đỏ được ngay khi assembly `Promotions` tồn tại; trước đó là “chưa có đối tượng”.
- `TC-27` rollback — cần migration đã áp.
- Route §6.3 — ABP tự sinh; phải so swagger sau khi có code (SRS §6.3 gắn 🔶 “chưa đo”).

---

## 11. Xử lý câu hỏi mở còn `Open` (không tự chốt nghiệp vụ)

| Câu | Mốc | Đề xuất của ARCH | Cần |
|---|---|---|---|
| **Q1** trần cụ thể | trước `migrations add` | Giữ **đề xuất** của SRS làm **giá trị mặc định** (ABP Setting): `64/8/32/1024/50/512/5000`; `MaxDepth` cứng `1…8` khớp `CHECK`. Đổi sau = setting, không migration | Kiến trúc trưởng + BA (`DEC`) |
| **Q2** trigger DB trần node/lá | trước release (không chặn W24) | **Không** trigger v1: validator + guard chạy + test; dữ liệu SQL tay vẫn bị engine ném `0001`. Trigger = logic ẩn trong DB, khó test | Kiến trúc trưởng + DBA (`DEC`) |
| **Q5** tách nhiệm vụ 4 mắt | trước release | v1 **có** permission `.Approve` nhưng **chưa** cưỡng chế “người duyệt ≠ người dựng” (không có cột người duyệt trong SRS); ghi nợ, không tự thêm cột | BA + Kiểm soát nội bộ (`DEC`) |
| **Q9** khuyến mãi sàn vs của ta | trước khi mở `External` | v1 `Source = Internal`; giữ trường `Source` trong breakdown để additive sau; **cấm** cộng khuyến mãi sàn vào `DiscountTotal` | BA + Kế toán (`DEC`) |
| **Q26** lỗi nạp context của khách có `CustomerId` | trước khi nối `Sales` | Chọn **(b)** fail-closed: `CustomerGroupId = null` / `IsFirstOrder = null` ⇒ lá `false`, đơn vẫn tạo, không ném; **cần** `DEC` vì chưa thành BR | BA + Kiến trúc `Sales` (`DEC`) |
| **Q28** lỗi cấu hình khi candidate tự động | trước release | v1 giữ **(a)** NÉM ở **mọi** nguồn (`D-154` (5)) + hàng rào `TC-68`; tách theo nguồn là thay đổi chính sách, cần `DEC` | BA + Kiến trúc (`DEC`) |
| **Q29** gộp hai context | sau W24 (refactor) | Giữ **hai** tên với quan hệ “hình chiếu chỉ-đọc”; gộp không đem lại giá trị, làm mờ ranh giới ngân sách | Kiến trúc trưởng (`DEC`) |
| **Q8** điều kiện địa chỉ/vùng giao | **chặn** | **Không** thiết kế; chờ GHTK xác minh 2 cấp (R3-1). 0 rule/cột/DTO | `NCC` (GHTK/BA) |
| **AQ8** job liệt kê tenant | trước W24-2 | Đề xuất `DISTINCT TenantId` từ `ProPromotions` + tắt tenant filter có ghi lý do + `ICurrentTenant.Change`; thay thế nếu có `TenantManagement` | `DEC` |
| **AQ9** nơi đặt `OrderCancelledEto` | trước khi implement consumer | ETO **phải** ở `Sales.Application.Contracts`; WP-24 **hoãn** handler tới SAL-004. Nếu SAL-004 đặt ở `Sales.Domain` ⇒ phải `DEC` cách khác | `DEC` |

---

## 12. `docDeviation` (SRS ↔ ARCH/as-built) & câu hỏi mở

### 12.1 `docDeviation` — thiết kế **buộc** khác as-built (ORCH gửi `ba` sửa SRS cùng PR W24-2)

| # | SRS nói | As-built | Đề xuất ARCH |
|---|---|---|---|
| **D1** | `Kind`/`Status`/`DiscountKind` lưu **chuỗi** (SRS §5.2) | Module mới nhất lưu enum **int** (`NotificationDbContextModelCreatingExtensions.cs:41-43`; `PricingDbContextModelCreatingExtensions.cs:33-34`) | **Giữ SRS** (chuỗi + `CHECK` đọc được); đây là khác biệt **kiểu cột**, không đổi hợp đồng. BA ghi rõ trong SRS để người đọc không tưởng lệch |
| **D2** | `Currency char(3)` | `Sales`/`Pricing` dùng `varchar(8)` (`modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs:21`) | **Giữ SRS** `char(3)` + `CHECK ~ '^[A-Z]{3}$'` (mẫu `PricingDbContextModelCreatingExtensions.cs:113`); không FK chéo module nên không xung đột |
| **D3** | §7.1: payload **phải kế thừa** `IntegrationEventBase` | **Không có** base class; ETO as-built tự khai `EventId/OccurredOn/TenantId/AggregateId/AggregateVersion` (`modules/Address/eCommerce.Address.Application.Contracts/Events/AddressEvents.cs:13-35`); Kernel chỉ có `INotificationEvent` | **Theo as-built**: ETO của `Promotions` tự khai đủ trường `XC-002` BR-020; **chỉ** sự kiện thông báo gắn `INotificationEvent`; `PromotionAppliedEto` **mang dữ liệu** ⇒ **không** gắn marker + phát **fail-closed** (`IEventCoverageProbe`). **Không** tạo base class trong WP-24 (thay đổi cross-cutting). BA sửa câu “kế thừa” thành “có đủ trường” |
| **D4** | §6.1 `namespace eCommerce.Promotions.Contracts` | Module mới dùng namespace gốc `eCommerce.Notification` (`modules/Notification/eCommerce.Notification.Application.Contracts/NotificationDtos.cs:4`) | **Giữ SRS** (namespace con `.Contracts` không xung đột project); ghi nhận để không ai “sửa cho giống” rồi phá chữ ký đã duyệt |
| **D5** | SRS §7.2 #4 coi `OrderCancelledEto` là đầu vào đã có | ETO **chưa tồn tại** (SAL-004) | Ghi rõ “chờ SAL-004”; consumer hoãn, không giả lập ETO trong `Promotions` (`AQ9`) |
| **D6** | SRS §6.1 quy tắc 6: `CheckoutInput.OrderDiscount` do client gửi **≠ 0 ⇒ server NÉM** (`D-143` (2)) | As-built **chưa** có guard: `modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:109` truyền thẳng `OrderDiscount = input.OrderDiscount` | **W24-2** thêm guard ở `CheckoutAppService` + test (client gửi ≠ 0 ⇒ `400`, **0** đơn/giữ chỗ); đây là điều kiện để engine là **nguồn giảm giá duy nhất**. Không sửa `Order` (thuộc `SAL-002`/`SAL-003`) |
| **D7** | SRS §5.2 đặt tên index đầy đủ (`IX_ProPromotionConditions_TenantId_PromotionId_ParentId_SortOrder`, `IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_VoucherCodeId`, `IX_ProPromotionRedemptions_TenantId_PromotionId_CustomerId_UsageSequence`) | PostgreSQL cắt định danh > 63 byte **âm thầm**; migration `Add_Promotion_Module` rút gọn ≤ 63 byte: `IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort`, `IX_ProPromRedemptions_Tenant_Prom_Order_Voucher`, `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq`; đọc `pg_indexes` xác nhận đủ `UNIQUE` + `NULLS NOT DISTINCT` + `WHERE` đúng | **Theo as-built** (tên rút gọn — §5.5); test `TC-33` kiểm **đích danh** tên đã rút gọn. BA ghi tên rút gọn vào SRS để không ai tưởng index còn nguyên chữ dài |
| **D8** | SRS §6.1 đặt `IPromotionConditionRule`/`PromotionRuleContext`/`PromotionCartLine` dưới namespace `eCommerce.Promotions.Contracts` | Khai trong **assembly Domain** (`modules/Promotions/eCommerce.Promotions.Domain/Contracts/PromotionRuleContracts.cs:1`) nhưng **giữ** namespace `eCommerce.Promotions.Contracts` | **Theo as-built** (Contracts→Domain hợp lệ §2.1); **không** đổi chữ ký công khai, **không** di chuyển kiểu. BA ghi rõ vị trí để không ai “sửa cho khớp assembly” rồi phá tham chiếu |
| **D9** | SRS §7.2 #2/#3 cột “Idempotency key (ổn định)” (`PromotionTree:…`/`Import:…`) | `SaveTreeAsync`/`ImportRuleCostsAsync` **không** nối `IIdempotencyGuard` ở v1 (as-built `modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:154-203,345-408`); allowlist ranh giới giữ đúng `eCommerce.Messaging.EventCoverage` | **Theo as-built** (`D-284`): chống trùng theo nghiệp vụ = khoá `Tree`/`Import` + delete-then-insert cùng transaction + import chỉ cập nhật dòng có; 2 khoá `PromotionTree:…`/`Import:…` khai nhưng **chưa dùng** (§3.7); retry `SaveTree` tăng `TreeVersion` +1 ở `Draft` — **chấp nhận** |
| **D10** | SRS §13: “Metric mới (9) — ⚠️ CHƯA có trong code” | 9 đã **đăng ký**; **5** có điểm phát thật (`PromotionEngine`); **4** (`promotion_voucher_reserved_total`/`_released_total`/`_reservation_expired_total` = `PRO-001`; `promotion_applied_total` = `SAL-001`) ở `ChuaCoNoiPhat` là **nợ có chủ** | **Theo as-built** (`D-283` — §8); `MetricEmitterTests` đỏ cả hai chiều; BA sửa SRS §13 thành “9 đăng ký; 5 có điểm phát; 4 nợ có chủ” |
| **D11** | Allowlist `TC-31` chỉ biết **một** app service quản trị — **10** method của `IPromotionAppService` (SRS §6.2 cũ) | SRS §6.2/§6.3 (v0.12–v0.14, `D-285` ①(ii) + `D-289`/`D-290`) thêm **interface thứ hai** `IPromotionRuleCatalogAppService.GetRulesAsync()` (chỉ đọc, `Promotions.View`) **cùng** `Promotions.Application.Contracts`; DTO có trường additive **`IsSeeded`** (`D-289`) và **`Label` lấy từ `PromotionRuleCostCatalog.Meta` trong code, fallback `SystemName`** (`D-290`; **không** cột DB, **không** sửa `IPromotionConditionRule` — `D8` giữ); test phản chiếu vẫn đếm **10** method của `IPromotionAppService` (`PromotionSurfaceTests.cs:36-49`) | **Thiết kế bổ sung, không đảo `TC-31`**: allowlist **10** method **giữ nguyên** (không thêm method thứ 11); `/api/v1` vẫn **7** route; `/api/app` có thêm auto route của service mới (additive, §4.4). QA/`docrev` **không** viết test "tổng 11 method" — test đúng 10 trên `IPromotionAppService` + đúng 7 route trên `/api/v1` (`PromotionV1SurfaceTests`); `PromotionRuleCatalogTests` phủ `IsSeeded` + nguồn nhãn (`D-289`/`D-290` ⑥) |

### 12.2 Câu hỏi mở **mới** (đưa vào khối JSON)

`AQ8` (tenant iteration của job) và `AQ9` (nơi đặt `OrderCancelledEto`) — xem §11; `D6` là việc bắt buộc của W24-2 (không phải câu hỏi mở, đã có `D-143` (2)).

### 12.3 Ghi nhận P3 của DOCREV (đã đóng)

P3 #1 (54→55 test case ở SRS §15.4) và P3 #2 (trích số dòng cũ trong delta `pro-001` §6.1) — **BA** đã đóng ở N94/F1 (SRS `v0.10`); ARCH không sửa SRS.

---

## 13. Truy vết quyết định (`D-xx` ↔ mục)

| Quyết định | Nội dung áp | Mục |
|---|---|---|
| `D-151` | PRO-002 là nguồn schema `Pro*` duy nhất; `EvaluateAsync` **chỉ đọc**; gộp **CR-1…CR-7 + CR-11** vào `Add_Promotion_Module`; 0 mã lỗi mới | §1.2, §4.1, §5.3, §6.1–6.2, §9.1 |
| `D-152` | v1 **một** `DiscountTotal` cấp đơn; bỏ `PerLine`; `CHECK TargetType` | §1.3, §3.5, §5.4, §10.1 |
| `D-153` | Pos/RetailBill ngoài phạm vi | §1.3, §2.1, §10.1 (`TC-38`) |
| `D-154` | `RetiredAtUtc`; phân loại `0004`/`0010` theo **sự có mặt của dòng**; seeder upsert 0 DELETE | §3.5, §6.4, §9.1 |
| `D-155` | `Money`/`MoneyAllocator.PhanBo`; cấm bộ phân bổ thứ hai | §3.2, §3.6 |
| `D-156` | `FlashSale` chặn bằng `CHECK` + validation | §1.3, §3.1, §5.4 |
| `D-157` | **4** index `ProPromotionRedemptions` phải `UNIQUE` + NND (một voucher/đơn + một lượt tự động/đơn) | §5.5, §6.2 |
| `D-158` | trần `≤ Subtotal` không kẹp theo nguồn; `FreeShipping` chặn | §3.5, §5.4, §6.1 |
| `D-159` | cây bất biến từ `Approved`; unique một gốc; `evaluate` không guard | §3.2, §3.3, §5.5, §6.1 |
| `D-160` | `Percentage` trên `Subtotal` gốc; `ExcludedByNonCumulative` | §4.1, §6.1 |
| `D-161` | `CustomerRequired` cho tự động + vãng lai | §3.4, §4.1, §6.1 |
| `D-162` | lá khách khi `CustomerId = NULL` ⇒ `false` | §3.4 |
| `D-163` | định nghĩa “đơn đầu” do `Sales` nạp | §4.1, §6.1 |
| `D-144` (`decisions.md:158`) | dải `0001–0099` + tiền tố `eCommerce:Promotion:`; map mã → HTTP | §3.2, §4.4 |
| `D-143` (2) | client `CheckoutInput.OrderDiscount ≠ 0` ⇒ NÉM | §6.1, §12.1-D6 |
| `D-147` | `0119` trần mức promotion (thuộc PRO-001) | §6.2 |
| `D-148` | chỉ `OrderCancelledEto` hoàn lượt | §6.2, §12.1-D5 |
| `XC-002` E3 (fail-closed) | ETO mang dữ liệu kiểm người nhận qua `IEventCoverageProbe`; ngoại lệ có chủ `Promotions.Application → Messaging.Domain` | §2.1, §2.2-B2, §6.2 |
| `D-150` | không đường sửa v1 | §3.2, §4.3 |
| `D-17` | enum chỉ thêm ở cuối | §3.1, §4.2 |
| `D-31` | số lượng `decimal(18,3)` | §4.1, §5.4 |
| `D-209` (`decisions.md:223`) | WP-24 sau WP-16 | §1.2 (thứ tự thi công) |
| `D-270` | mẫu quyền hẹp khai 2 nơi | §7.1 |

---

## 14. Tự kiểm

| Mục | Đã kiểm bằng gì |
|---|---|
| Module `Promotions` chưa có | `ls modules` (27/11/2026); graph không có `IPromotionEngine`/entity `Pro*` |
| Mẫu 4 project + DbContext hợp nhất | `modules/Notification/**`, `modules/Reporting/**`; `eCommerce.slnx:27-31,82-86`; `eCommerceDbContext.cs:367-409,476` |
| `[Authorize]` cấp class + permission 2 nơi | `ReportAppService.cs:26-27`; `eCommerceConsts.cs:340-361`; `eCommercePermissionDefinitionProvider.cs:196-204` |
| Map mã lỗi → HTTP | `eCommerceHttpApiHostModule.cs:400-422` (mẫu Reporting) |
| `NULLS NOT DISTINCT` + SQL thô | migration `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:1-60` |
| Không dùng `btree_gist` | chỉ có ở `20261013012648_Add_Tax_Module.cs:118` và `20261126181842_Wp26_VanHanh_BaoCao.cs:168` |
| `CHECK` bằng `HasCheckConstraint` | `PricingDbContextModelCreatingExtensions.cs:102-114`; `NotificationDbContextModelCreatingExtensions.cs:52-54` |
| `Money`/`MoneyAllocator`/`RoundingPolicy` | `Money.cs:36-44,132-140`; `MoneyAllocator.cs:31-108`; `RoundingPolicy.cs:25-37` |
| `IIdempotencyGuard` | `IdempotencyGuard.cs:27-34,50-56` |
| Seeder idempotent | `NotificationDataSeedContributor.cs:13,37-50` |
| Setting định nghĩa theo tenant | `PricingSettingDefinitionProvider.cs:10-50` |
| Worker đăng ký thật | `NotificationApplicationModule.cs:24-31`; `ReportingAggregationWorker.cs:37-52` |
| ETO as-built không có base class | `AddressEvents.cs:13-35`; `shared/eCommerce.Kernel/Events/INotificationEvent.cs` |
| Cổng tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` PASS · `npm --prefix tools run check:mermaid` 0 lỗi. ⚠️ `tools/agents/check-feature-doc.ps1` (16 mục) **KHÔNG** áp cho tệp `-arch` (chỉ áp cho SRS `<mã>-<slug>.md`) — chạy sẽ báo thiếu `## 0.`…`## 15.`; **không** tuyên bố “16 mục xanh” |
| DOCREV vòng 1 (N93) | P1=1 + P2=6 đã đóng tại §4.4, §5.4, §5.5, §6.1–6.2, §9.1, §12.1; P3 #8–#13 đã áp (§6.3, §9.1, §10.1–10.2, §14) |
| Không còn đánh dấu việc bỏ ngỏ | quét từ khoá bỏ-ngỏ trên tệp = **0** (N93) |

---

## 15. Lịch sử tài liệu

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.1 | 27/11/2026 | ARCH (N93/W24-1) | Bản đầu: 15 mục; chốt ranh giới module `Promotions` mới; hợp đồng `IPromotionEngine` chép nguyên SRS §6.1; 7 bảng + một migration `Add_Promotion_Module` (không `btree_gist`); 9 metric; 9 dòng permission; 5 `docDeviation`; 10 câu mở (`Q1`, `Q2`, `Q5`, `Q9`, `Q26`, `Q28`, `Q29`, `Q8` chặn, `AQ8`, `AQ9`) |
| v0.2 | 27/11/2026 | ARCH (N93 — sửa theo DOCREV vòng 1: P1=1, P2=6, P3=6) | **P1:** §5.5 ghi rõ **UNIQUE + `NULLS NOT DISTINCT`** cho **cả 4** index chống trùng `ProPromotionRedemptions` + cơ chế đỏ `TC-33`. **P2:** bổ sung cột thiếu §5.4 (`ChannelScope`; `CustomerId`/`ReasonCode`/`TreeVersion`/`EvaluatedAtUtc`); thống nhất **CR-1…CR-7 + CR-11**; thêm bảng map mã lỗi → HTTP (§4.4, sửa tham chiếu §7.4); sửa citation `:52-54` → `:41-43`; `PromotionAppliedEto` **mang dữ liệu = fail-closed**, chỉ 2 ETO thông báo gắn `INotificationEvent`; thêm guard `CheckoutInput.OrderDiscount ≠ 0 ⇒ NÉM` (§6.1, `D6`). **P3:** citation worker `:24-31`; cơ chế đỏ “cấm song song hoá” + cờ `Promotions.Enabled=false`; ghi trung thực phạm vi cổng 16 mục; dải `TC-01…TC-51` + `TC-66…TC-69`; đếm lại 13 mã lỗi / 9 dòng permission; thứ tự xoá cây an toàn với FK tự thân `Restrict` |
| v0.3 | 27/11/2026 | ARCH (N93 — sửa theo DOCREV vòng 2: P1=0, P2=1, P3=4) | **P2:** chốt **phương án (a)** cho `IEventCoverageProbe` — `Promotions.Application` tham chiếu **`eCommerce.Messaging.Domain`** (module hạ tầng, không có Contracts), khai nhánh phụ thuộc §2.1 + ngoại lệ B2 + hệ quả allowlist test ranh giới; §6.2 nêu đường dẫn probe + phương án (b) cross-cutting ngoài WP-24 (nếu chọn: v1 không phát `PromotionAppliedEto`). **P3:** sửa bằng chứng khoá phân tán sang `NotificationDispatchWorker.cs:79`; §5.5 “cả 4 index đều có `WHERE`”; §5.4 khai 4 cột `ExpiresAtUtc`/`AppliedAtUtc`/`ReleasedAtUtc`/`ReversedAtUtc`; §14 ghi cụ thể phép quét từ khoá bỏ-ngỏ = 0 |
| v0.4 | 27/11/2026 | ARCH (N94/W24-2) | P3 DOCREV vòng 3: thu hẹp allowlist test ranh giới còn đúng namespace `eCommerce.Messaging.EventCoverage` (không wildcard) + nêu tên test `RanhGioiPromotionsTests` ở §10.2. |
| v0.5 | 27/11/2026 | ARCH (N94/W24-2) | Cập nhật trạng thái: SRS `v0.10`, DOCREV vòng 4/5 `DOC-OK`, 0 P3 còn mở; không đổi thiết kế. |
| v0.6 | 27/11/2026 | ARCH (N94/W24-2) | áp `D-283` (metric 5/9 + 4 nợ) & `D-284` (không nối guard); thêm docDeviation D7–D10; ghi rõ tên index rút gọn; không đổi thiết kế khác. |
| v0.7 | 27/11/2026 | ARCH (N95/W24-3) | Áp `D-285`/`D-287`/`D-288`: §4.3 thêm **service đọc riêng** `IPromotionRuleCatalogAppService.GetRulesAsync()` (+ `PromotionRuleCatalogItemDto`) — allowlist **10** method giữ nguyên; §4.3 thêm `PromotionDetailDto.Limits` (`PromotionTreeLimitsDto` gán từ `DocLimitsAsync()`, additive); §4.4 thêm tiểu mục bề mặt `/api/v1/promotion/**` **7** route (controller tường minh, `ApiPage<T>`, 4 op không mở + chủ nợ `SAL-002`/`ops`/`WEB-*`), giữ nguyên `/api/app` 10 route + bảng map mã → HTTP; §12.1 thêm `docDeviation` **D11**; header lên SRS `v0.13`. **0** migration, **0** đổi schema, **0** mã lỗi mới. |
| v0.8 | 27/11/2026 | ARCH (N95/W24-3) | Áp `D-289`/`D-290` + dọn P3 `docrev`: §4.3 DTO `PromotionRuleCatalogItemDto` thêm **`IsSeeded`** (true ⇔ có dòng `ProPromotionRuleCosts`); `GetRulesAsync` lặp `PromotionRuleRegistry.SystemNames` (không lặp bảng), thiếu dòng ⇒ `PromotionRuleCostCatalog.Cua` (`O1`/`1`/`false`) + `IsSeeded = false` + `LogWarning` một lần/lượt gọi, **không** ném `0010`/không bỏ khỏi danh sách/0 metric; **gỡ** gạch "Biên chưa chốt" + viện dẫn `AQ11` (đã chốt bằng `D-289`); thêm hàng **"Nguồn nhãn"** = `PromotionRuleCostCatalog.Meta.Label` trong code (`PromotionRuleCostSeedContributor.cs:13-27`) + fallback `Label = SystemName`, **không** sửa `IPromotionConditionRule`/không cột DB/không từ điển thứ hai ở Application; §4.4 nói rõ `eCommerce.HttpApi.csproj` **phải thêm** `ProjectReference` tới `Promotions.Application.Contracts` (hiện **chưa** có); §12.1 D11 bổ sung `IsSeeded` + nguồn `Label`; header lên SRS `v0.14`. **0** migration, **0** route/method mới. |
| v0.9 | 27/11/2026 | ARCH (N98/W24-5) | Áp `D-293`/`D-294` (hồi quy `GET /DatHang` = **500** do Web thiếu `PromotionsApplicationModule`): §2.1 sơ đồ + §B6 + §5.2 mục 3 đổi "Host" → "**Host và Web**" (Web chạy in-process ⇒ chỉ `ProjectReference` là **không đủ**); thêm đoạn `Replace(... IAbpDistributedLock, LocalAbpDistributedLock ...)` ở Web (nạp module kéo `AbpDistributedLockingModule` ⇒ Medallion cần `IDistributedLockProvider`; chỉ tiến trình UI, worker/job tắt; **0** Redis, **0** package). **0** migration, **0** đổi schema/hợp đồng. |
