# `PRO-001` — Kiến trúc Voucher/Coupon: giữ · chốt · nhả lượt trên mô hình `Pro*` của `PRO-002`

> **v0.7 — 28/11/2026** (N103/W25-4 — **doc-only giữ code**, áp **`D-310`** + **`D-309`**: (1) §6.4 sửa **câu chữ** cho khớp as-built — counter `reservation_expired`/`released{reason=TTL_EXPIRED}` **chỉ** phát khi `daNha > 0`, nhánh **0 dòng** = `LogDebug` + bỏ qua, **KHÔNG** metric; (2) §1.1 + §8 sửa as-built cũ — **cả 3** metric voucher đã có điểm phát, cả 3 đã rời `ChuaCoNoiPhat`, nợ chỉ còn `promotion_applied_total`; (3) thêm **§6.1b** đặc tả thứ tự guard `GenerateAsync` + **pre-check dung lượng mã** (`36^(Length−len(Prefix)) < Quantity` ⇒ `0107`); giữ nguyên `VoucherExpiryWorker.cs:180-186` + test `D306_…`; §13 trỏ thêm §6.1b/§1.1/§8) ·
> **v0.6 — 27/11/2026** (N101/W25-2 — áp **`D-305`** + **`D-306`**: `CommitVoucherDto` **5** trường + `Promotions` tự so `Σ DiscountAmount` vs `OrderDiscountTotal` ⇒ `0111`; job `voucher-expiry` nhả TTL **ngay trong `W25-2`** + 2 counter; ETO ra **hoãn có chủ**; nêu nợ as-built `PromotionExpiryWorker` **chưa** đọc cờ) ·
> **v0.5 — 27/11/2026** (N101/W25-2 — sửa `docrev` vòng 3 **delta** N101: **2 P2 + 1 P3** vùng arch — nêu **validator** `Subtotal`/`Currency`/`CheckoutSessionId` ở §4.2/§6.1; **D13** chuyển "đã đóng một phần ở N101") ·
> **v0.4 — 27/11/2026** (N101/W25-2: áp `D-303` — `CheckoutSessionId` = **`string`** (≤ 128) ở `ReserveVoucherDto`/`CommitVoucherDto`; sửa số dòng map `01xx` **14 → 17**) ·
> **v0.3 — 27/11/2026** (sửa `docrev` vòng 1: `DOC-CHANGES` P1=0/P2=5/P3=6 → sửa 5 P2 + 6 P3) ·
> **v0.2** cùng ngày (áp `D-297`…`D-302`, đóng toàn bộ §11) · bản đầu v0.1.
>
> Vai **ARCH**, phiên **N100** (v0.1–v0.3), **N101** (v0.4–v0.6) và **N103** (v0.7), hàng `W25-1`/`W25-2`/`W25-4`, chuỗi `gd3-1` (`WP-25`), nhánh `feat/gd3-wp25-voucher`.
> Tài liệu này **chỉ thiết kế** quanh SRS
> [`pro-001-voucher-coupon.md`](./pro-001-voucher-coupon.md) (`v0.11 — 27/11/2026`, sau đây gọi **SRS**);
> **không** code, **không** sinh migration, **không** sửa SRS.
>
> **Nguồn schema duy nhất của bảng `Pro*` là `PRO-002`** (`D-151`, `pro-002` §5.2/§5.3/§6.4). Tài liệu này
> **không** định nghĩa lại bảng; nó chỉ ra **cái đã có as-built** (W24-2) và **delta còn thiếu** cho `W25-2`.
> Khuôn 15 mục kế thừa [`PRO-002-arch.md`](./PRO-002-arch.md) `v0.9`.
>
> **Kết luận lớn nhất của phiên này:** phần **schema** mà `PRO-001` cần (CR-1…CR-7 + CR-11) **đã nằm trong
> migration `Add_Promotion_Module`** từ W24-2; `W25-2` **KHÔNG** cần migration tạo bảng/cột. Phần **còn thiếu là
> code**: 2 service voucher, 2 máy trạng thái (mã/lượt), 14 hằng mã lỗi `01xx` còn lại + map HTTP, 3 quyền
> `.Voucher.*`, 3 metric (đã đăng ký nhưng chưa có điểm phát), 2 job nền và **điểm cắm `Sales`** (thay `EvaluateAsync`
> bằng `ReserveAsync`/`CommitAsync`).
>
> **Nguồn quyết định:** `D-143` (guard `OrderDiscount ≠ 0`), `D-146` (vãng lai ⇒ `0118`), `D-147` (bộ đếm trần
> promotion ⇒ `0119`), `D-148` (huỷ đơn ⇒ `Applied→Released`, `ReasonCode=ORDER_CANCELLED`), `D-149` (chỉ giảm cấp
> đơn, registry **đúng 4** rule), `D-150` (không đường sửa), `D-151` (schema source), `D-158` (`FreeShipping` chặn),
> `D-159` (khoá `Checkout:` chỉ của `ReserveAsync`), `D-169`, `D-216`, `D-144` (dải `01xx`), **`D-295`** (trần **5 lần
> THỬ** `UsageSequence`, `0` metric mới), **`D-296`** (as-built `0109` = `AbpValidationException` 400, không `Code`),
> **`D-297`** (tạm duyệt v0.1 để code F3, hiệu lực **có điều kiện** `docrev` = `DOC-OK` 0 P1 trên bản chốt),
> **`D-298`** (`AQ1`+`AQ5`: DTO additive `Sales`, server kiểm tenant+phiên; 2 lá khách **luôn `false`** ở v1),
> **`D-299`** (`AQ2`+`AQ3`: `VoucherController` tường minh `/api/v1/voucher/**` **7 route** + audit bằng ABP audit log,
> **0** cột/migration), **`D-300`** (`AQ4`: **một** migration `Add_Voucher_Status_Check` do **`PRO-002` sở hữu**),
> **`D-301`** (`AQ6`: trả **cả hai** `ExpiresAtUtc`, FE hiện **min**), **`D-302`** (`Q2/Q6/Q7/Q9/Q11`: TTL **900 s**
> không gia hạn · `MaxBatchSize = 10000` không rate limit · liệt kê lượt giữ qua `GetRedemptionsAsync(status=Reserved)`
> · không dry-run · test chuỗi khoá 0 tiền tố `ecommerce:`), **`D-303`** (`CheckoutSessionId` của `ReserveVoucherDto`/`CommitVoucherDto` là **`string`** ≤ 128, **không** `Guid`; SRS giữ `string`), **`D-304`** (2 ca `TtlSeconds` ngoài dải + `Subtotal`=0/`Currency` sai hoãn **`qa`** ở `W25-4`; **cơ chế validator** phải có sẵn ở `W25-2` — §4.2/§6.1),
> **`D-305`** (`BR-06`/`0111`: `CommitVoucherDto` thêm **trường thứ 5** `decimal OrderDiscountTotal` — additive, cho phép `0`;
> `Promotions` **tự** so `Σ DiscountAmount` của lượt `Applied` cùng `OrderId` rồi NÉM `eCommerce:Promotion:0111` (500) —
> **không** đẩy việc kiểm sang `Sales`, nhánh replay **vẫn so**; `0` migration), **`D-306`** (job `voucher-expiry` nhả TTL
> **làm ngay trong `W25-2`** — worker riêng `Workers/`, DI ở module Application, đọc cờ `Promotions:Enabled`, khoá
> `Job("voucher-expiry", tenantId)` TTL 300 s, lô 500/UoW `requiresNew`, guard `Status=0`, phát **2** counter
> `reservation_expired{kind}` + `released{reason=TTL_EXPIRED}`, gỡ `reservation_expired` khỏi `ChuaCoNoiPhat`, **giữ**
> `promotion_applied_total`; ETO ra **hoãn có chủ** `REP-001`/`SAL-004`).
>
> 📄 `-arch` là tài liệu **bổ trợ**, ngoài mẫu 16 mục áp cho SRS; tệp giữ khuôn 15 mục của `PRO-002-arch`.

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đang có trong code (đọc từ code, không viết theo trí nhớ)

| Hạng mục | Hiện trạng | Bằng chứng (đã đọc trong phiên N100) |
|---|---|---|
| Module `Promotions` | ✅ **đã có** 4 project (W24-2), khác SRS §14.5 (viết 23/11) | `modules/Promotions/{Domain,Application.Contracts,Application,EntityFrameworkCore}` |
| 7 bảng `Pro*` + migration | ✅ **đã áp** `Add_Promotion_Module` (26/11/2026) | `src/eCommerce.EntityFrameworkCore/Migrations/20261126224627_Add_Promotion_Module.cs` |
| CR-1 (5 cột giá trị giảm) | ✅ có trên `Promotion` | `modules/Promotions/eCommerce.Promotions.Domain/Promotion.cs:44-48` |
| CR-11 (bộ đếm promotion) | ✅ có + `CHECK` trần | `Promotion.cs:39-42`; `PromotionsDbContextModelCreatingExtensions.cs:70-71` |
| CR-2/CR-3 (`ReservedCount`, `Status`, `CHECK` gộp) | ✅ có | `modules/Promotions/eCommerce.Promotions.Domain/VoucherCode.cs:12-18`; EF `:139-140` |
| CR-4/CR-5 (lượt `Reserved`, 8 cột, `OrderId` nullable) | ✅ có đủ 8 cột | `modules/Promotions/eCommerce.Promotions.Domain/PromotionRedemption.cs:13-30` |
| CR-6 (3 index mới + sửa index `pro-002:431`) | ✅ có, filter đã thêm `AND "OrderId" IS NOT NULL` | EF `PromotionsDbContextModelCreatingExtensions.cs:193-211` |
| Unique `NULLS NOT DISTINCT` | ✅ 9 index của 7 bảng; test chốt cứng **114** | migration `:353-410`; `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs:222-228` |
| Engine `IPromotionEngine` + đường voucher | ✅ đọc + **ném** `0007`/`0104`/`0119` khi hết lượt | `modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionEngine.cs:372`, `:383`, `:411` |
| Registry **đúng 4** rule (`D-149`) | ✅ `MinOrderAmount`/`ChannelScope`/`CustomerGroup`/`FirstOrderOnly` | `modules/Promotions/eCommerce.Promotions.Domain/Rules/PromotionRules.cs:14-66` |
| **Service voucher** (`IVoucherAdminAppService`, `IVoucherRedemptionService`) | ❌ **chưa có** (0 file) | `find modules/Promotions -name "*.cs"` — không có `Voucher*Service` |
| **Máy trạng thái** mã/lượt | ❌ mới có factory `TaoMoi`, **chưa có** method chuyển trạng thái | `VoucherCode.cs:25-36`; `PromotionRedemption.cs:36-70` |
| Mã lỗi `01xx` | 🔶 **chỉ có** `0101`, `0104`, `0119` | `modules/Promotions/eCommerce.Promotions.Domain/PromotionErrorCodes.cs:40-53` |
| Map mã lỗi → HTTP | ❌ chỉ map `0001–0010` | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:449-458` |
| Permission voucher | ❌ **5/8** (`View/Manage/Approve/Import` + gốc); thiếu `.Voucher.Generate/.Publish/.OverrideLimit`, `.Export` | `src/eCommerce.Domain.Shared/eCommerceConsts.cs:371-387`; `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:210` |
| 3 metric voucher | ✅ **cả 3 đã có điểm phát** (N101/`W25-2`): `reserved` (`ReserveAsync`), `released` (`ReleaseAsync` + job `voucher-expiry` `reason=TTL_EXPIRED`), `reservation_expired` (job `voucher-expiry`, **chỉ khi** có lượt nhả thật — `D-310`); cả 3 tên **đã rời** `ChuaCoNoiPhat`, danh sách nợ **chỉ còn** `promotion_applied_total` (`SAL-001`) | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:147-154,297-316` (`:315` là dòng nợ **duy nhất**); `VoucherExpiryWorker.cs:180-186`; `VoucherRedemptionService.cs:575-577` (`ReleaseAsync`) |
| Job hết hạn promotion | ✅ có (`promotion-expiry`); **as-built KHÔNG đọc cờ `Promotions:Enabled`** (nợ `PRO-002`, `D-306` ⑥) | `modules/Promotions/eCommerce.Promotions.Application/Workers/PromotionExpiryWorker.cs:35-50` (class + ctor), `:84-122` (`DoWorkAsync`) |
| Job nhả TTL lượt + job đối soát | ❌ **chưa có** — `W25-2` **tạo** `VoucherExpiryWorker` (job `voucher-expiry`) theo `D-306`; job đối soát vẫn **hoãn** | cùng thư mục `Workers/` chỉ có 1 file (`PromotionExpiryWorker.cs`) |
| Tuỳ chọn & setting | 🔶 `PromotionOptions` có `JobPeriodMinutes`/`ExpiryJobName`/`MaxUsageSequenceAttempts`; **0** option/Setting TTL; lô job `voucher-expiry` = **hằng 500**, cờ dùng `PromotionContractConsts.EnabledSettingName` (`D-306`) | `PromotionExpiryWorker.cs:16-25`; `PromotionContractConsts.cs:10` |
| Guard `CheckoutInput.OrderDiscount ≠ 0 ⇒ NÉM` | ✅ **đã có** | `modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:74-83` |
| `Sales` gọi engine | ✅ N101/F3: có mã ⇒ `ReserveAsync` (**thay** `EvaluateAsync`); lúc tạo đơn ⇒ `CommitAsync` | `CheckoutAppService.cs:175-189` (`:143` đọc cờ); `DonHangAppService.cs:118-130` |
| `CheckoutInput` / `KetQuaCheckoutDto` mang mã + redemption | ✅ N101/F3: `CheckoutInput.VoucherCode`; `KetQuaCheckoutDto.VoucherRedemptionId`/`VoucherDiscountTotal`/`VoucherExpiresAtUtc` | `modules/Sales/eCommerce.Sales.Application.Contracts/CheckoutDtos.cs:63,99-105` |
| UoW tạo đơn + `SetDiscount` | ✅ cùng UoW (điểm cắm `CommitAsync`) | `modules/Sales/eCommerce.Sales.Application/DonHangAppService.cs:103-133` |
| `IIdempotencyGuard` | ✅ có | `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:27-34` |

### 1.2 Phạm vi `WP-25` / `W25-2` (IN) — **code + 1 migration `CHECK` (do `PRO-002` sở hữu, `D-300`)**

1. **Domain**: 2 máy trạng thái — `PromotionRedemption` (`Reserved→Applied→Released/Reversed`, sai ⇒ `0105`) và
   `VoucherCode` (`Generated→Issued→Revoked`, sai ⇒ `0006`); **cấm** `Expired`/`Exhausted` trong `Status` (`ADR-09`);
   14 hằng mã lỗi `01xx` còn thiếu trong `PromotionErrorCodes` (`0102/0103/0105/0106/0107/0108/0110/0111/0112/0114/0115/0116/0117/0118`;
   `0109`/`0113` **giữ số, không hằng** — `D-296`/`D-158`); hàm thuần đọc `MaxBatchSize` (kẹp `1…100000`, `D-302`).
2. **Contracts**: `IVoucherAdminAppService` (**7** method + `RedemptionFilterDto` **tạo mới tại đây** — **không** thêm
   method vào `IPromotionAppService`, `TC-31` giữ 10) + `IVoucherRedemptionService` (**4** method, **không**
   `IApplicationService` ⇒ **không** sinh controller) + DTO tiền kèm `Currency`; `CommitVoucherDto` thêm
   `OrderDiscountTotal` (**trường thứ 5**, `D-305`); `RedemptionDto.ExpiresAtUtc` additive.
3. **Application**: 2 service; **2 job** — `voucher-expiry` nhả TTL **làm ngay `W25-2`** (`D-306`: worker riêng
   `VoucherExpiryWorker`, DI ở `PromotionsApplicationModule`, đọc cờ, lô 500, guard `Status=0`, phát **2** counter) và
   `voucher-counter-audit` đối soát **chỉ đọc** (hoãn); cấp `UsageSequence` đúng `D-295`; `CommitAsync` **tự so**
   `Σ DiscountAmount` vs `OrderDiscountTotal` ⇒ `0111` (`D-305`); **3** tên metric đều **có** điểm phát sau `W25-2`
   (`reserved`/`released`/`reservation_expired`) ⇒ **gỡ `reservation_expired`** khỏi `ChuaCoNoiPhat`, **giữ**
   `promotion_applied_total` (nợ `SAL-001`, `D-283`); TTL **bắt buộc** từ `ReserveVoucherDto.TtlSeconds` (60…86400, `D-302`).
4. **Tích hợp `Sales`** (`D-298`, `D-305`): `CheckoutAppService` gọi `ReserveAsync` **thay** `EvaluateAsync` khi có
   `CheckoutInput.VoucherCode`; `DonHangAppService` gọi `CommitAsync` **sau `SetDiscount`+`Recalculate()` (`:111-113`),
   trước tripwire `:133`**, cùng UoW (`ADR-13`), truyền `don.DiscountTotal`; 7 trường DTO additive phía `Sales` (§4.5),
   `ba` ghi CR `SAL-002`/`SAL-001` **cùng nhánh cùng PR**.
5. **Bề mặt HTTP + map lỗi** (`D-299`): controller tường minh `VoucherController` `/api/v1/voucher/**` **7 route** (khuôn
   `PromotionsController`); **giữ** auto `/api/app/voucher-admin/**`; **cấm** `[AllowAnonymous]`/`RemoteService(Name=…)`;
   thêm **17** dòng map `01xx → HTTP` vào `eCommerceHttpApiHostModule` (§7.6; map đủ `0101…0119` trừ `0109`/`0113`) — thiếu thì `0104`/`0119` trả **403** che lỗi.
6. **Phân quyền**: 3 quyền hẹp `.Voucher.*` khai **2 nơi**; `[Authorize]` cấp class.
7. **Migration** (`D-300`): **đúng một** `Add_Voucher_Status_Check` (2 `CHECK` miền `Status`, **0** cột/index/dữ liệu) —
   **`PRO-002` sở hữu mapping + migration**; `PRO-001` chỉ gửi CR; nếu **không** phải chủ migration duy nhất ⇒ hoãn
   sang hàng `PRO-002`, **không** chặn code.

### 1.3 Ngoài phạm vi (OUT) — **không đụng**

- ❌ `FreeShipping`, voucher theo SKU/danh mục, cộng dồn nhiều mã, quầy `Pos`/`RetailBill` — `D-149`/`D-158`/`D-142`.
- ❌ Voucher của sàn (`ChannelDiscount`) — `ADR-08`; `Pos` **0** tham chiếu `Promotions`.
- ❌ Sửa `Order`/`OrdOrders`, `TinhTienDon`, SRS `pro-001`/`pro-002`, `decisions.md`, biên bản.
- ❌ Thêm bảng/cột `Pro*` (schema source là `PRO-002`) — trừ 2 `CHECK` ở §5.5 (**`D-300` đã chốt**, `PRO-002` sở hữu).

---

## 2. Ranh giới module ABP

### 2.1 Chiều phụ thuộc (giữ nguyên `PRO-002-arch` §2.1)

```
Host / Web ──[DependsOn]──▶ Promotions.Application ──▶ Contracts ──▶ Domain
                                         └─▶ Kernel · Messaging.Domain (allowlist 2 namespace — §2.2)
Sales.Application ──▶ Promotions.Application.Contracts      (in-process, cùng UoW — ADR-13)
Promotions.* ✗──▶ Sales/Pos/src/**                          (CẤM chiều ngược)
```

| Project | Tham chiếu **được phép** | **Cấm** |
|---|---|---|
| `eCommerce.Promotions.Domain` | Kernel, Domain.Shared | mọi `*.Application*`, Host, module nghiệp vụ khác |
| `eCommerce.Promotions.Application.Contracts` | Domain, Kernel, `Abp.Ddd.Application.Contracts` | Application, EF, module khác |
| `eCommerce.Promotions.Application` | Domain, Contracts, Kernel, Abp Ddd/BackgroundWorkers/DistributedLocking/EventBus/EF, **`eCommerce.Messaging.Domain`** (allowlist §2.2) | Host, module nghiệp vụ khác, `Messaging.Application`/`.EntityFrameworkCore` |
| `eCommerce.Promotions.EntityFrameworkCore` | Domain, Abp.EntityFrameworkCore | Application, Host |
| `eCommerce.Sales.Application` | **đã** tham chiếu `Promotions.Application.Contracts` | `Promotions.Domain`, `Promotions.Application` |
| `eCommerce.Pos.*` | — | **mọi** `eCommerce.Promotions.*` (`TC-47`) |

### 2.2 Ngoại lệ B2 **mở rộng có chủ** — `Promotions.Application → Messaging.Domain`

`PRO-002-arch` §2.1 hiện allowlist **đúng một** namespace `eCommerce.Messaging.EventCoverage`. `PRO-001` **bắt buộc**
thêm **đúng một** namespace nữa: `eCommerce.Messaging.Idempotency` — vì `BR-02` yêu cầu `IIdempotencyGuard` ở mọi
biên vào **có ghi**; `Messaging` **không có** project `.Application.Contracts`, và `Sales.Application` đã đi cùng đường
(`modules/Sales/eCommerce.Sales.Application/eCommerce.Sales.Application.csproj`).

- Allowlist sau `W25-2`: **đúng 2** namespace, **không** wildcard:
  `eCommerce.Messaging.EventCoverage` · `eCommerce.Messaging.Idempotency`.
- **Cấm** `eCommerce.Messaging.Application`/`.EntityFrameworkCore`/`eCommerce.Messaging.Outbox*`; **cấm** dùng
  `IIdempotencyGuard` cho việc khác ngoài biên vào ghi (không dùng cho job quét theo trạng thái).
- Test ranh giới **hiện có** — method `RanhGioi__Promotions_Application_chi_dung_Messaging_EventCoverage()`
  (`test/eCommerce.Tests/Promotions/PromotionSurfaceTests.cs:97-108`) assert **mọi** dòng `using eCommerce.Messaging*`
  bằng đúng `using eCommerce.Messaging.EventCoverage;`. Hôm nay test **vacuous** (chưa có `using` nào); khi thêm
  `using eCommerce.Messaging.Idempotency`, **test này ĐỎ** ⇒ **phải** sửa allowlist thành **đúng 2** chuỗi trong
  **cùng PR**. `eCommerce.Promotions.Application.csproj:16` **đã** có `ProjectReference` `Messaging.Domain` ⇒ **không**
  có ca "build đỏ vì thiếu ref"; rủi ro duy nhất là test ranh giới đỏ nếu quên sửa.
- 📌 Đây là **thay đổi chủ đích**, **không** phải vi phạm: ghi vào `boundaryViolations` là **sai**.

### 2.3 Luật áp cho `W25-2`

| # | Luật | Cách giữ |
|---|---|---|
| B1 | Module sở hữu entity/bảng/mã lỗi | `PromotionErrorCodes` (một class, dải `0001–0010` + `0101–0199`); **cấm** thêm vào `eCommerceConsts.ErrorCodes` |
| B2 | Gọi chéo module **chỉ** qua Contracts hoặc ETO | `Sales → Promotions.Application.Contracts`; `Promotions` **không** gọi module nghiệp vụ nào |
| B3 | Không phụ thuộc ngược app/host | 0 `using eCommerce.Web/HttpApi/Application` trong `modules/Promotions` |
| B4 | Không FK xuyên module | `ProPromotionRedemptions.OrderId` là **tham chiếu mềm** (`PromotionRedemption.cs:9,16`) |
| B5 | `IRepository<T>` ⇒ phải có `DbSet` | **đã đủ 7** `DbSet` (`eCommerceDbContext.cs`) — `W25-2` **không** thêm |
| B6 | Module `.Application` nằm trong `[DependsOn]` **Host và Web** | **đã có** (`eCommerceHttpApiHostModule.cs:85`) — `W25-2` **không** đổi |
| B7 | Controller tự sinh cho assembly `eCommerce*.Application` **đã nạp** | `IVoucherAdminAppService` sinh auto route; **`IVoucherRedemptionService` không được** sinh (§4.3) |
| B8 | Module nguồn cấm tham chiếu ngược | `Sales`/`Pos` không bị gọi ngược |
| B9 | Tenant filter | 2 bảng voucher/redemption **đã** `IMultiTenant`; 0 bảng mới |

> 📌 **Vi phạm ranh giới MỚI do `W25-2`: 0** — với điều kiện allowlist §2.2 được cập nhật **cùng PR**.

---

## 3. Thiết kế tầng Domain

### 3.1 Enum & hằng số (đã có — ghim ordinal, append-only `D-17`)

| Enum | Giá trị | Lưu DB as-built | Ghi chú |
|---|---|---|---|
| `PromotionRedemptionStatus` | `Reserved=0`, `Applied=1`, `Released=2`, `Reversed=3` | **`int`** (`IsRequired()`, không conversion) | `PromotionEnums.cs:49-55`; filter index dùng `"Status" <> 2` ⇒ **cấm đổi số** |
| `VoucherCodeStatus` | `Generated=0`, `Issued=1`, `Revoked=2` | **chuỗi** `varchar(16)` | `PromotionEnums.cs:58-63`; EF `HasConversion<string>()` `:132` |
| `PromotionDiscountKind` | `Percentage`, `FixedAmount`, `FreeShipping` | **chuỗi** `varchar(16)` | `FreeShipping` bị `CHECK` chặn (`:66`) |

🚨 **`Expired`/`Exhausted` KHÔNG BAO GIỜ vào `VoucherCode.Status`** (`ADR-09`, `BR-09`); cách biểu diễn là **suy ra**
từ `EndsAtUtc` và `UsedCount + ReservedCount >= MaxUsage`. Test `TC-43` đỏ nếu ai thêm giá trị vào enum hoặc ghi được
giá trị lạ.

### 3.2 `VoucherCode` — máy trạng thái **NÉM** (`Generated → Issued → Revoked`)

Thêm 3 method vào entity (hiện chỉ có factory `TaoMoi`, `VoucherCode.cs:25-36`):

| Method | Từ → Sang | Guard | Vi phạm ⇒ |
|---|---|---|---|
| `Issue(DateTime now)` | `Generated → Issued` | `Status == Generated` | `BusinessException(PromotionErrorCodes.InvalidStatusTransition)` = `0006` |
| `Revoke(DateTime now)` | `Issued → Revoked` | `Status == Issued` | `0006` |
| *(không có)* | `Revoked → *` | hấp thụ | `0006` |
| *(không có)* | * → `Expired`/`Exhausted` | — | không tồn tại trong enum (compile-time) |

- Bộ đếm `UsedCount`/`ReservedCount` **KHÔNG** có setter công khai và **KHÔNG** đổi qua entity — mọi thay đổi đi bằng
  `ExecuteUpdateAsync` có điều kiện (§6), quyết định **có chủ ý**: nếu entity bị track rồi `SaveChanges` ghi đè
  `ReservedCount`, hai luồng song song sẽ mất một lượt mà **không** có lỗi.
- `RowVersion` (`xmin`) là concurrency token của cột khác; **không** dựa vào nó để chống trùng lượt (`ADR-03`: điều
  kiện nằm trong chính câu `UPDATE`).

### 3.3 `PromotionRedemption` — sổ cái **append-only**, máy trạng thái **NÉM**

Thêm 3 method chuyển trạng thái (hiện chỉ có `TaoMoi`, `PromotionRedemption.cs:36-70`):

| Method | Từ → Sang | Guard | Kèm theo | Vi phạm ⇒ |
|---|---|---|---|---|
| `ChotApplied(Guid orderId, DateTime now)` | `Reserved → Applied` | `Status == Reserved` **và** (server đã kiểm `ExpiresAtUtc > now` **trong** `UPDATE`) | `OrderId`, `AppliedAtUtc`, `Status` | `0105` |
| `NhaReleased(DateTime now, string reasonCode)` | `Reserved → Released` (TTL) **hoặc** `Applied → Released` (huỷ đơn) | `Status ∈ {Reserved, Applied}`; nhánh huỷ đơn **chỉ** consumer `OrderCancelledEto` được gọi | `ReleasedAtUtc`, `ReasonCode`, `Status` | `0105` |
| `DaoReversed(DateTime now, string reasonCode)` | `Applied → Reversed` | `Status == Applied` | `ReversedAtUtc`, `ReasonCode`, `Status` | `0105` |

- **Cột bất biến sau khi ghi** (`BR-11`): `DiscountAmount`, `IdempotencyKey`, `VoucherCodeId`, `PromotionId`,
  `CustomerId`, `UsageSequence`, `TreeVersion`, `EvaluatedAtUtc` — **cấm** method nào đổi. `OrderId` chỉ được đặt
  **đúng một lần** (`NULL → giá trị`) tại `ChotApplied`.
- Hàm chuyển trạng thái **không** tự cộng/trừ bộ đếm; service chịu trách nhiệm cặp `UPDATE` bộ đếm (mã **trước**,
  promotion **sau**, `D-147` (2)) trong **cùng** UoW.
- Tiền: `DiscountAmount` là `decimal` + `Currency` (đã có); **cấm** `double`/`float`; so tiền qua `Money`
  (`shared/eCommerce.Kernel/Money/Money.cs`), **cấm** `==` trên `decimal` tiền.

### 3.4 Giá trị giảm — **một** nguồn tính

- `ReserveAsync` **tái dùng** `PromotionDiscountCalculator` (`modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionDiscountCalculator.cs`)
  và `PromotionEvaluationContext` của engine — **cấm** viết bộ tính giảm thứ hai (`BR-07`, `ADR-10`).
- Kết quả giảm là **một** `DiscountTotal` cấp đơn; phân bổ xuống dòng + thuế sau giảm do `TinhTienDon` của `Pricing`
  (SRS §0 `ADR-05`, `D-149`) — `Promotions` **không** trả giảm theo dòng.
- Trần `≤ Subtotal`: vượt ⇒ **NÉM** `0009`, **cấm** kẹp (`BR-07`).

### 3.5 Khoá phân tán & khoá idempotency (đã có — **không** khai hàm mới)

| Hàm as-built | Giá trị | Dùng cho |
|---|---|---|
| `PromotionLockKeys.Job("voucher-expiry", tenantId)` | `promotion:job:voucher-expiry:{tenant}` | job nhả TTL, `SkipIfBusy` |
| `PromotionLockKeys.Job("voucher-counter-audit", tenantId)` | `promotion:job:voucher-counter-audit:{tenant}` | job đối soát, `SkipIfBusy` |
| `PromotionLockKeys.VoucherUsage(tenantId, code)` | `promotion:voucher:{tenant}:{code}` | **tối ưu** tranh chấp, **không phải** hàng rào |

`PromotionLockKeys.cs:9-52`. **Cấm** thêm tiền tố `ecommerce:` (`ADR-11`); **cấm** dùng distributed lock làm hàng rào
giữ/chốt/nhả lượt (`ADR-03`).

**Khoá idempotency** (sinh **trong** module, không nhận từ client):

| Biên | Khoá | Ghi chú |
|---|---|---|
| `ReserveAsync` | `Checkout:{tenantId}:{checkoutSessionId}:{code}` | `checkoutSessionId` = `CheckoutInput.IdempotencyKey` của `Sales`; chỉ `ReserveAsync` dùng (`D-159`) |
| `CommitAsync` | `PromotionCommit:{tenantId}:{orderId}:{code}` | `orderId` sinh **một lần**, lưu DB |
| Huỷ đơn | `PromotionRefund:{tenantId}:{orderId}:{promotionId}` | **tái dùng** `PromotionIdempotencyKeys.Refund` (`PromotionLockKeys.cs:48-50`) |
| Trả hàng | `PromotionReverse:{tenantId}:{orderId}:{promotionId}` | **mới** — thêm hàm vào `PromotionIdempotencyKeys`, dùng cùng khuôn |
| Job TTL / đối soát | **không** khoá idempotency | job quét theo trạng thái + guard `UPDATE … WHERE Status`, chạy lại vô hại |

### 3.6 Tuỳ chọn & setting của `W25-2` (`D-302`) — **một nguồn**, 0 migration

| Hạng mục | Chốt | Ghi chú |
|---|---|---|
| **TTL giữ lượt** | `ReserveVoucherDto.TtlSeconds` **bắt buộc**, `int`, chỉ nhận `60…86400` (ngoài ⇒ 400 `AbpValidationException`, **0** mã mới) | `Sales` truyền `(CheckoutInput.TtlPhut ?? 15) * 60` ⇒ mặc định **900 s**, **trùng mốc** giữ chỗ kho — **một** nguồn. **Không** Setting TTL ở v1 (sẽ là dead-code vì `Sales` luôn gửi); **không** gia hạn trượt (`ExpiresAtUtc` tính **một lần**) |
| **`Promotions:MaxBatchSize`** | ABP Setting, **mặc định 10000**, phạm vi Application/Configuration/Global/Tenant; đọc bằng hàm thuần kẹp `1…100000`, giá trị sai ⇒ mặc định + `hopLe = false`, **không** ném (khuôn `PromotionSettings.Doc`) | Khai trong `PromotionSettingDefinitionProvider` (đã có provider) + `PromotionSettingNames`; `Quantity > hiệu lực` ⇒ **`0108` trước khi ghi dòng nào**. **Không** rate limit v1 (chỉ admin tenant có `.Voucher.Generate`) |
| **Liệt kê lượt đang giữ** | `GetRedemptionsAsync(RedemptionFilterDto{ Status = Reserved })` — **dùng cửa có sẵn**, **không route mới** | `RedemptionDto` thêm **additive** `ExpiresAtUtc` (CSHK cần hạn); **không** trả `IdempotencyKey`. **Gỡ tay không có ở v1** (nợ `P11`/`P6`) |
| **Dry-run** | **Không** ở v1 | Trước phát hành dùng `IPromotionAppService.ValidateVoucherAsync` (**chỉ đọc**, 0 side effect); kiểm lô nằm trong `GenerateAsync` **trước khi ghi**. Dry-run lô = nợ `P11` |
| **Chuỗi khoá** | Cổng trong-repo: test khẳng định 3 chuỗi `PromotionLockKeys` (`promotion:job:voucher-expiry:{tenant}`, `promotion:job:voucher-counter-audit:{tenant}`, `promotion:voucher:{tenant}:{code}`) **không** có tiền tố `ecommerce:` (`ADR-11`) | `ops` chạy `redis-cli MONITOR` khi có Redis (**chưa** có: Web dùng `LocalAbpDistributedLock`, `D-294`); **không** chặn `W25-2` |

---

## 4. Hợp đồng `Application.Contracts`

### 4.1 `IVoucherAdminAppService` — 7 method (SRS §6.1)

```csharp
namespace eCommerce.Promotions.Contracts;

public interface IVoucherAdminAppService : IApplicationService
{
    Task<PagedResultDto<VoucherDto>> GetListAsync(VoucherFilterDto input);
    Task<VoucherDetailDto> GetAsync(Guid id);
    Task<GeneratedVouchersDto> GenerateAsync(GenerateVouchersDto input);
    Task<VoucherDto> IssueAsync(Guid id);
    Task<VoucherDto> RevokeAsync(Guid id, string reason);
    Task<VoucherDto> OverrideLimitAsync(Guid id, int newMaxUsage, string reason);
    Task<PagedResultDto<RedemptionDto>> GetRedemptionsAsync(RedemptionFilterDto input);
}
```

- **Không** có `UpdateAsync` (`D-150`, `BR-18`); `OverrideLimitAsync` là **ngoại lệ duy nhất**, `newMaxUsage < UsedCount +
  ReservedCount` ⇒ `0116`.
- `RevokeAsync.reason` **bắt buộc ≥ 10 ký tự** (validate ở app service, `400` — **không** mã mới); `OverrideLimitAsync`
  `reason` bắt buộc.
- DTO tiền (`VoucherDto`/`VoucherDetailDto`) **bắt buộc** kèm `Currency`; số lượt còn lại là **trường suy ra**
  `RemainingUses = MaxUsage − UsedCount − ReservedCount` (**không** lưu cột).
- **`RedemptionFilterDto` chưa tồn tại trong code** — `W25-2` **tạo mới** trong `eCommerce.Promotions.Application.Contracts`
  (cùng file/namespace với `IVoucherAdminAppService`): `OrderId?`, `Status?` (`PromotionRedemptionStatus?`), `CustomerId?`,
  `Paging` (`SkipCount`/`MaxResultCount`/`Sorting`). **Không** thêm method nào vào `IPromotionAppService` (`TC-31` giữ **10**).
- **Bề mặt HTTP (`D-299`)**: `be` thêm `src/eCommerce.HttpApi/Controllers/VoucherController.cs` — `[ApiController]`,
  `[Route("api/v1/voucher")]`, `[Authorize(eCommerceConsts.Permissions.Promotions.Default)]` **cấp class**, `[Authorize]`
  cấp method, **chỉ** gọi app service (khuôn `PromotionsController.cs:27-31`), **7 route**:
  `GET /api/v1/voucher` (`View`, `ApiPage<VoucherDto>`) · `GET /{id:guid}` (`View`) · `POST /generate` (`.Voucher.Generate`)
  · `POST /{id:guid}/issue` + `POST /{id:guid}/revoke` (`.Voucher.Publish`) · `POST /{id:guid}/override-limit`
  (`.Voucher.OverrideLimit`) · `GET /redemptions` (`View`). **Giữ nguyên** auto `/api/app/voucher-admin/**` (hai bề mặt,
  khuôn `D-285`); **cấm** controller tường minh chạy `/api/app/voucher` (`D-13` — `/api/app/**` = conventional);
  **cấm** `[RemoteService(Name = "voucher")]` (**chưa xác minh** ở ABP 10.7).

### 4.2 `IVoucherRedemptionService` — hợp đồng **nội bộ**, 4 method (SRS §6.1)

```csharp
/// Hợp đồng nội bộ cho Sales — KHÔNG kế thừa IApplicationService ⇒ KHÔNG sinh controller/route.
public interface IVoucherRedemptionService
{
    Task<VoucherQuoteResultDto> ReserveAsync(ReserveVoucherDto input, CancellationToken ct = default);
    Task<VoucherAppliedDto> CommitAsync(CommitVoucherDto input, CancellationToken ct = default);
    Task ReleaseAsync(ReleaseVoucherDto input, CancellationToken ct = default);
    Task ReverseAsync(ReverseVoucherDto input, CancellationToken ct = default);
}

public sealed record ReserveVoucherDto(
    string VoucherCode,
    string CheckoutSessionId,    // D-303: = CheckoutInput.IdempotencyKey của Sales; [property: Required][StringLength(128, MinimumLength=1)], KHÔNG Guid; rỗng/>128 ⇒ 400
    Guid? CustomerId,            // NULL ⇒ NÉM 0118 (D-146, TRƯỚC engine/TRƯỚC ghi)
    string ChannelCode,
    IReadOnlyList<PromotionCartLine> Lines,
    decimal Subtotal,            // D-298 ①(4)+D-304: lấy từ GioTinhTienDto của Sales; [property: Range(typeof(decimal), "0.01", "99999999999999.9999")] (= numeric(18,4)); 0/thiếu ⇒ 400; CẤM Σ ListPrice×Quantity
    string Currency,             // [property: Required][RegularExpression("^[A-Z]{3}$")] (khớp CHECK Pro*); cùng Currency của Subtotal
    int TtlSeconds);             // D-302: [property: Range(60, 86400)]; ngoài dải ⇒ 400; Sales = (TtlPhut ?? 15) * 60

public sealed record CommitVoucherDto(
    Guid RedemptionId, Guid OrderId, string VoucherCode,
    string CheckoutSessionId,    // D-298/D-303: [property: Required][StringLength(128, MinimumLength=1)], KHÔNG Guid; so khớp string IdempotencyKey == Checkout:{tenant}:{session}:{code}
    decimal OrderDiscountTotal); // 🆕 D-305 (trường thứ 5, additive, CUỐI danh sách): = Order.DiscountTotal của đơn Sales SAU SetDiscount+Recalculate; [property: Range(typeof(decimal), "0.00", "99999999999999.9999")] — CHO PHÉP 0; -1/thiếu ⇒ 400 (guard tường minh)
public sealed record ReleaseVoucherDto(Guid RedemptionId, string ReasonCode);
public sealed record ReverseVoucherDto(Guid OrderId, string ReasonCode);
```

- **`Subtotal`/`Currency` bắt buộc trong `ReserveVoucherDto`** (`D-298` ①(4)): `PromotionEvaluationRequest` **cần**
  `Subtotal`; `Sales` lấy từ `GioTinhTienDto` (`CheckoutAppService.cs:151-189`). **CẤM** tự tính
  `Σ ListPrice × Quantity` (`D-291` ②, `pro-002` §6.1 quy tắc 9).
- 🆕 **Cơ chế từ chối `Subtotal = 0` / `Currency` sai / `CheckoutSessionId` rỗng** (`D-304` ⑥, `D-303` ⑥) — **cơ chế
  thật**, không chỉ "bắt buộc" trên kiểu giá trị (**`decimal`/`string` non-nullable thì `[Required]`/không mô tả không
  chặn được `0`**):
  - **Lớp ném**: `AbpValidationException` ⇒ **400** (as-built `Promotion.cs:130` = ca `0109`, `§12-D7`/`D-296`; **0** mã
    lỗi mới). **Không** dùng `ArgumentException` (ABP **không** map 400 ⇒ rơi 500).
  - **`Subtotal`**: `[property: Range(typeof(decimal), "0.01", "99999999999999.9999")]` — cận dưới **0.01** ⇒ `0`/thiếu
    **400** (đúng ca `D-304` ⑥); cận trên = **trần `numeric(18,4)`** as-built của cột tiền `Pro*`
    (`PromotionsDbContextModelCreatingExtensions.cs:52-54,186`). Khuôn overload `Range(typeof(decimal), …)`:
    `StockDocumentDtos.cs:185`; khuôn `Range` cận dưới: `TraHangLineDto.Quantity` (`DonHangDtos.cs:46`). **Không** dùng
    `[Range(0.01, double.MaxValue)]` (khuôn `OrderReturnDtos.cs:99`) cho tiền — biên kiểu `double` trái `T1`.
  - **`Currency`**: `[Required]` + `[RegularExpression("^[A-Z]{3}$")]` — **cùng biểu thức** với `CHECK
    CK_ProPromotions_Currency`/`CK_ProPromotionRedemptions_Currency` (`:73`, `:217`); rỗng/sai 3 ký tự IN HOA ⇒ **400**
    (fail-closed `T3`, **không** tự `.ToUpper()`).
  - **`CheckoutSessionId`** (cả **hai** record): `[Required]` + `[StringLength(128, MinimumLength = 1)]`; rỗng/> 128 ⇒
    **400** (`D-303` ⑥).
  - 📌 **Vì sao phải guard tường minh**: `VoucherRedemptionService` **không** kế thừa `IApplicationService` (§4.3) ⇒
    interceptor DataAnnotations của ABP **không tự chạy** trên `ReserveVoucherDto`; `ReserveAsync` **bước 1** (§6.1) tự
    kiểm và ném `AbpValidationException` **trước** mọi `UPDATE`/`INSERT` ⇒ **0** dòng `ProPromotionRedemptions`.
    Thuộc tính đặt dạng `[property: …]` trên tham số record để rơi vào **property** (DataAnnotations đọc property).
- **`ReserveVoucherDto.CheckoutSessionId` = `string` (không rỗng, ≤ 128)** (`D-303`; cùng nguồn
  `CheckoutInput.IdempotencyKey`, `CheckoutDtos.cs:67-69`) — **không** chỉ `CommitVoucherDto`; đây là nguồn dựng khoá
  `Checkout:{tenant}:{session}:{code}` (§3.5), **không** nhận khoá thô từ client (**không** `Guid`, **không** parse lại).
- **`CustomerId` mang trong `ReserveVoucherDto`**; **`CommitVoucherDto.CheckoutSessionId` bắt buộc**: server kiểm
  `(TenantId, RedemptionId)` **và** `IdempotencyKey == Checkout:{tenant}:{session}:{code}`; **lệch ⇒ `0105`** (mở rộng
  nghĩa "lượt không khớp phiên/khách", **0** cột/khoá mới).
- `VoucherQuoteResultDto`: `RedemptionId`, `DiscountTotal`, `Currency`, `ReasonCode?`, `IdempotencyKey`,
  **`ExpiresAtUtc`** (hạn **lượt voucher**) — **không** có `ShippingDiscountTotal` ở v1 (`D-158`). `ExpiresAtUtc`
  **không** đổi nghĩa trường cũ của `KetQuaCheckoutDto` (`D-301`).
- **`CommitAsync` chọn lượt theo `RedemptionId`**, **không** theo mã (2 khách giữ cùng mã sẽ lẫn lượt).
- 🆕 **`CommitVoucherDto.OrderDiscountTotal` — trường thứ 5, additive, CHO PHÉP `0`** (`D-305` `BR-06`/`0111`):
  - **Nguồn**: `Sales.DonHangAppService.TaoTuCheckoutAsync` truyền **`don.DiscountTotal`** tại **đúng** điểm gọi đã chốt
    (`D-298` ①(3)): **sau** `don.SetDiscount(tien.TotalDiscount)` + `don.Recalculate()` (`DonHangAppService.cs:111-113`),
    **trước** tripwire `GrandTotal != tien.Total` (`:133`), **cùng** UoW (`ADR-13`). Không truyền `tien.TotalDiscount`
    thay thế — `Order.DiscountTotal` chỉ đặt trong `SetDiscount`/`Recalculate` (`Order.cs:240-250`).
  - **Kiểu/validate**: `decimal` (không `double`/`float`, `T1`), `[property: Range(typeof(decimal), "0.00",
    "99999999999999.9999")]` — **cận dưới `0.00`** vì đơn hợp lệ có thể **không** giảm; cận trên = trần `numeric(18,4)`
    as-built. Vì `VoucherRedemptionService` **không** phải `IApplicationService`, **phải** guard **tường minh** trong
    `VoucherRequestValidator.KiemTraCommit` (`VoucherRequestValidator.cs:60-70`, hiện chỉ kiểm `CheckoutSessionId`) —
    thêm nhánh `OrderDiscountTotal < 0` ⇒ `AbpValidationException` (**400**, **0** mã mới), **trước** mọi `UPDATE`.
    Hằng cận trên dùng lại `VoucherRequestValidator.SubtotalToiDa` (`:19`) hoặc hằng mới cùng giá trị.
  - **Vị trí tham số**: **cuối** danh sách record; **không** đổi thứ tự 4 trường `D-298`/`D-303`; **không** đổi
    `VoucherAppliedDto`; **0** cột/bảng/migration. Bên gọi **duy nhất** là `Sales` ⇒ cập nhật **cùng PR** (`D-305` ④).
  - **Không** đưa `OrderDiscountTotal` vào **payload khoá idempotency** (§6.3 bước 1) — giữ nguyên
    `{RedemptionId, OrderId, Code}` để lỗi lệch tiền vẫn là `0111`, **không** bị đổi thành `Messaging:0001`
    (`PayloadConflict`).
- **Điểm gọi `CommitAsync` (chốt, `D-298` ①(3))**: trong `DonHangAppService.TaoTuCheckoutAsync`, **sau**
  `don.Recalculate()` (`DonHangAppService.cs:113`) và **trước** tripwire `GrandTotal != tien.Total` (`:133`), **cùng**
  UoW ABP (`ADR-13`). Có `VoucherRedemptionId` mà sổ giải trình **không** có giảm từ mã ⇒ **400** (`BusinessException`
  cuối dải `eCommerce:Sales:*`) — **cấm** tạo đơn mất giảm im lặng.
- `ReleaseAsync` **chỉ** `Reserved → Released`; `Applied → Released` (huỷ đơn) **chỉ** consumer `OrderCancelledEto`
  (§6.3) — **cấm** đi qua method này (`BR-25`).
- `ReverseAsync` **không** hoàn `UsedCount`.

### 4.3 Ai gọi, phiên bản, tương thích

| Hợp đồng | Bên gọi | Kiểu | Tương thích |
|---|---|---|---|
| `IVoucherAdminAppService` | `VoucherController` tường minh `/api/v1/voucher/**` (**7 route**, `D-299`) **và** auto `/api/app/voucher-admin/**` (`D-285` hai bề mặt) | app service | thêm method ⇒ **phải** cập nhật `TC-40` (allowlist 7 method) + `VoucherSurfaceTests` (7 route) |
| `IVoucherRedemptionService` | `Sales.Application` — `CheckoutAppService` (Reserve), `DonHangAppService` (Commit) | project reference `Promotions.Application.Contracts` | thêm trường DTO ⇒ **phải** có default/`?`; **cấm** đổi thứ tự tham số record. `D-305` thêm `OrderDiscountTotal` **ở cuối** `CommitVoucherDto` (hợp đồng **nội bộ**, bên gọi duy nhất `Sales` cập nhật **cùng PR**; **0** đổi `schema.d.ts`/route) |
| `PromotionCartLine` | `Sales` dựng | record (Domain assembly, namespace `.Contracts`) | **không** đổi chữ ký |

🚨 **`IVoucherRedemptionService` KHÔNG được kế thừa `IApplicationService`.** Nếu kế thừa, `ConventionalControllers`
quét assembly (Host `eCommerceHttpApiHostModule.cs:466-475`) sẽ **sinh route HTTP** cho `reserve`/`commit` ⇒
**phá** `ADR-13` (một request HTTP riêng **không** chung UoW với `Order`) và mở đường tạo lượt không qua checkout.
Thêm `[RemoteService(IsEnabled = false)]` làm lưới thứ hai; test `TC-34` quét controller/route.

### 4.4 DTO — ràng buộc cứng

- **Cấm** `double`/`float`; **cấm** DTO đầu vào nhận "tổng đã giảm"/"số phải trả" (server tính).
- `ReserveVoucherDto`/`CommitVoucherDto` **cấm** nhận `TenantId` — lấy `ICurrentTenant`; `Guid.Empty` ⇒ `0112`.
- `GenerateVouchersDto`: `PromotionId`, `Prefix`, `Length`, `Quantity`, `MaxUsage`; `Quantity > MaxBatchSize` ⇒ `0108`
  **trước khi ghi dòng nào**.
- `RedemptionFilterDto` (**tạo mới** ở `IVoucherAdminAppService`): `OrderId?`, `Status?`, `CustomerId?`, phân trang —
  **không** trả `IdempotencyKey` cho người không có quyền.

### 4.5 DTO phía `Sales` — **additive**, server kiểm tenant + phiên (`D-298`, `D-301`)

| DTO | Trường thêm | Ghi chú |
|---|---|---|
| `CheckoutInput` | `VoucherCode` (`string?`, `[StringLength(64)]`) | Có mã ⇒ `CheckoutAppService` gọi `ReserveAsync` **thay** `EvaluateAsync` (không gọi cả hai); `Promotions:Enabled = false` ⇒ **0** lời gọi, `DiscountTotal = 0` |
| `KetQuaCheckoutDto` | `VoucherRedemptionId` (`Guid?`), `VoucherDiscountTotal` (`decimal?`), `VoucherExpiresAtUtc` (`DateTime?`), `VoucherReasonCode` (`string?`) | **Không** đổi nghĩa `ExpiresAtUtc` cũ (= min hạn giữ chỗ kho) |
| `TaoDonTuCheckoutInput` | `VoucherRedemptionId` (`Guid?`), `VoucherCode` (`string?`) | `DonHangAppService` gọi `CommitAsync` **sau** `Recalculate() :107`, **trước** tripwire `:110`, cùng UoW |
| FE (§10 SRS) | — | Đếm ngược theo **`min(ExpiresAtUtc, VoucherExpiresAtUtc ?? +∞)`**; mã hết trước ⇒ "báo giá lại" (**không** tự gia hạn) |

> 📌 `Sales` **ghép**, `Promotions` **không** biết hạn giữ chỗ kho và **không** sửa `ProPromotionRedemptions` (`D-301`).
> `ba` ghi **CR `SAL-002`** (2 DTO + 1 trường) + **`SAL-001`** (`TaoDonTuCheckoutInput`) **cùng nhánh cùng PR**.
> `git revert` 1 commit là đường lùi: 0 migration, 0 dữ liệu; `schema.d.ts` chỉ **thêm** trường.

---

## 5. EF Core + DbContext hợp nhất

### 5.1 DbSet & mapping — **đã đủ**, `W25-2` không sửa `OnModelCreating`

7 `DbSet` + `ConfigurePromotions` đã có (`src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs`; gọi một lần).
Mapping 3 bảng liên quan voucher:

| Bảng | Mapping as-built (đích danh) |
|---|---|
| `ProVoucherCodes` | `Code` 64 · counters `int` · `Status` `varchar(16)` · `RowVersion` (`xmin`); FK `PromotionId` **Restrict**; unique `(TenantId, Code)`; `CHECK CK_ProVoucherCodes_Usage = UsedCount >= 0 AND ReservedCount >= 0 AND UsedCount + ReservedCount <= MaxUsage` (`PromotionsDbContextModelCreatingExtensions.cs:126-141`) |
| `ProPromotionRedemptions` | `Status` int · `ReasonCode` 32 · `IdempotencyKey` 256 · `DiscountAmount numeric(18,4)` · `Currency char(3)` · `BreakdownJson jsonb`; FK promotion/voucher **Restrict**; 5 index unique có filter + 1 index thường; `CHECK` `UsageSequence>=1` · `DiscountAmount>=0` · `Currency ~ '^[A-Z]{3}$'` · 4 `CHECK` mốc thời gian theo `Status` · `ReasonCode` bắt buộc khi `Status ∈ {2,3}` (`:178-225`) |
| `ProPromotions` | 5 cột giá trị giảm + 2 bộ đếm + `CHECK CK_ProPromotions_Usage` / `Percentage` / `Window` / `Currency` (`:40-75`) |

### 5.2 Delta schema **thật** cho `W25-2`

| Cột/index/CHECK | Trạng thái | Quyết định |
|---|---|---|
| `DiscountKind`, `DiscountValue`, `MaxDiscountAmount`, `MinOrderAmount`, `Currency` (`ProPromotions`) | ✅ đã có (W24-2) | **0** thay đổi |
| `UsedCount`, `ReservedCount`, 2 `CHECK` (`ProPromotions`) | ✅ đã có | **0** thay đổi |
| `ReservedCount`, `Status` + `CHECK` gộp (`ProVoucherCodes`) | ✅ đã có | **0** thay đổi |
| `OrderId` nullable + 8 cột + 3 index + 1 filter sửa (`ProPromotionRedemptions`) | ✅ đã có | **0** thay đổi |
| `IX_ProPromotionRedemptions_TenantId_OrderId_Voucher` | ✅ `UNIQUE … NULLS NOT DISTINCT` + `WHERE OrderId IS NOT NULL AND VoucherCodeId IS NOT NULL` | **0** thay đổi (xem `D5` §12) |
| `IX_ProPromotionRedemptions_TenantId_Status_ExpiresAtUtc` | ✅ có (job nhả TTL) | **0** thay đổi |
| `CHECK` miền `ProVoucherCodes.Status IN ('Generated','Issued','Revoked')` | ❌ **chưa có** | **`D-300`: thêm** trong migration `Add_Voucher_Status_Check` (**`PRO-002` sở hữu**, §5.5) |
| `CHECK` miền `ProPromotionRedemptions.Status BETWEEN 0 AND 3` | ❌ **chưa có** | **`D-300`: thêm** cùng migration (§5.5) |

> 🔑 **Kết luận delta:** nghiệp vụ chạy được **không** cần cột/index mới; **`D-300` chốt thêm 2 `CHECK` miền `Status`**
> (gia cố `BR-09`/`ADR-09` ở DB) bằng **đúng một** migration do **`PRO-002` sở hữu** — `PRO-001` **chỉ** gửi CR.

### 5.3 `NULLS NOT DISTINCT` — **đã đúng**, test chốt cứng 114

9 index NND của 7 bảng `Pro*` do SQL thô trong `Add_Promotion_Module` (`:353-410`); `PostgreSqlSchemaTests.cs:222-228`
(Assert đếm ở `:227-228`)
chốt `Assert.Equal(114L, …)`. `W25-2` **cấm** đổi số này (0 index mới); nếu §5.5 được duyệt thì `CHECK` **không**
ảnh hưởng phép đếm index.

### 5.4 Không FK xuyên module

`ProPromotionRedemptions.OrderId` là `uuid` **không** FK sang `Sales` (`PromotionRedemption.cs:9,16`) — giữ nguyên.

### 5.5 Migration `Add_Voucher_Status_Check` — **chốt `D-300`**, do **`PRO-002` sở hữu**

- **Đúng một** migration `Add_Voucher_Status_Check` (EF sinh, `M1`): **2** `ALTER TABLE … ADD CONSTRAINT` —
  `CK_ProVoucherCodes_Status CHECK ("Status" IN ('Generated','Issued','Revoked'))` và
  `CK_ProPromotionRedemptions_Status CHECK ("Status" BETWEEN 0 AND 3)`; **0** cột/index/dữ liệu.
- Thêm 2 `HasCheckConstraint` vào `PromotionsDbContextModelCreatingExtensions.cs` để snapshot đúng — **`PRO-002` sở
  hữu mapping + migration** (`D-151`); `PRO-001` **chỉ gửi CR** (`ba` ghi `pro-002` §5.2/§6.4 **cùng nhánh cùng PR**);
  **một chủ migration** (`09-parallel-sessions` §6).
- `Down()` = 2 `DROP CONSTRAINT`; bảng `Pro*` mới (26/11/2026) **trống** ⇒ không khoá bảng; vẫn ghi đường dump vào
  handover nếu áp trên DB có dữ liệu.
- **Nếu `W25-2` không phải chủ migration duy nhất** (phiên song song) ⇒ **hoãn migration** sang hàng `PRO-002` kế tiếp;
  **không** chặn code: `BR-09` vẫn cưỡng chế bằng enum (compile-time) + máy trạng thái NÉM + `TC-43`.
- `D-17`: thêm giá trị enum ở cuối **sau này** ⇒ phải nới `CHECK` bằng migration mới — chấp nhận (fail-closed).
- **Cấm**: sửa `Add_Promotion_Module` đã merge (`M4`), `dotnet ef database update` (`M6`), migration rỗng.

---

## 6. Luồng nghiệp vụ

### 6.1 `ReserveAsync` — giữ lượt khi báo giá (SRS §4.1 bước 1–8)

Thứ tự **bắt buộc** (mọi kiểm "trước khi ghi" ở đây là **trước** `UPDATE` đầu tiên):

1. **Tenant + tham số** (guard **tường minh** vì `VoucherRedemptionService` **không** phải `IApplicationService` —
   §4.2): `ICurrentTenant.Id == Guid.Empty` (`00000000-0000-0000-0000-000000000000`) ⇒ **NÉM** `0112`;
   `ICurrentTenant.Id == null` là **ca 1-tenant hợp lệ** (**không** ném); `TtlSeconds ∉ [60, 86400]` · `Subtotal ∉
   [0.01, 99999999999999.9999]` (kể cả `= 0`/thiếu) · `Currency !~ ^[A-Z]{3}$` (rỗng/sai) · `CheckoutSessionId` rỗng/> 128
   ⇒ **NÉM** `AbpValidationException` (**400**, **0** mã mới, **0** dòng ghi; `D-302`/`D-298`/`D-303`/`D-304`).
   📌 `Sales` dựng `TtlSeconds` = `(CheckoutInput.TtlPhut ?? 15) * 60` ⇒ mặc định **900 s**, **trùng** mốc giữ chỗ kho.
2. **Chuẩn hoá + tìm mã** theo tenant: không có ⇒ `0101`; `Status = Revoked` ⇒ `0103`; `Generated` ⇒ `0101`;
   ngoài `[StartsAtUtc, EndsAtUtc]` ⇒ `0102` (đọc `ProPromotions` qua repository).
3. **Khách vãng lai**: `CustomerId == null` ⇒ **NÉM** `0118` — **trước** engine, **trước** mọi ghi (`D-146`, `BR-24`).
4. **Idempotency**: `IIdempotencyGuard.ThucThiAsync` với khoá `Checkout:…` + `PayloadHash` của request.
   - cùng khoá + cùng payload ⇒ trả **kết quả cũ**, metric `result=duplicate`, **không** giữ lượt lần 2;
   - cùng khoá + khác payload ⇒ `eCommerce:Messaging:0001` (`409`);
   - khoá đang xử lý ⇒ `eCommerce:Messaging:0002` (`409`);
   - lượt cũ của khoá đã `Released` ⇒ **NÉM** `0117` (**cấm** trả kết quả cũ ⇒ `Sales` mở phiên mới).
5. **Đánh giá cây + điều kiện** bằng `IPromotionEngine.EvaluateAsync` (**chỉ đọc**), `Subtotal`/`Currency` lấy từ
   `TinhGioInput` của `Sales`. Cây `false` ⇒ trả báo giá **không giảm** + `PromotionRejectionReason` — **không** ném.
6. **Giành lượt ở DB** — **hàng rào duy nhất** (`ADR-03`), đúng thứ tự **mã trước, promotion sau**:
   ```sql
   UPDATE "ProVoucherCodes"
      SET "ReservedCount" = "ReservedCount" + 1
    WHERE "TenantId" IS NOT DISTINCT FROM @t AND "Id" = @voucherId
      AND "Status" = 'Issued'
      AND "UsedCount" + "ReservedCount" < "MaxUsage";
   -- 0 dòng ⇒ NÉM eCommerce:Promotion:0007
   ```
   và **chỉ khi** `MaxUsageTotal IS NOT NULL`:
   ```sql
   UPDATE "ProPromotions"
      SET "ReservedCount" = "ReservedCount" + 1
    WHERE "TenantId" IS NOT DISTINCT FROM @t AND "Id" = @promotionId
      AND "UsedCount" + "ReservedCount" < "MaxUsageTotal";
   -- 0 dòng ⇒ NÉM eCommerce:Promotion:0119  (cả UoW rollback, kể cả lượt mã)
   ```
   Dùng `ExecuteUpdateAsync` trên `DbSet` của `eCommerceDbContext` (không đi qua entity tracked ⇒ không ghi đè bộ đếm).
7. **Cấp `UsageSequence` + `INSERT` lượt** — đúng `D-295` (§6.2).
8. Trả `VoucherQuoteResultDto` (`RedemptionId`, `DiscountTotal`, `ExpiresAtUtc` = hạn **lượt voucher**); `Sales` đưa
   `DiscountTotal` vào `TinhGioInput.OrderDiscount` của `Pricing` (bước ② tính tiền — `CheckoutAppService.cs:180-189`)
   rồi **lưu `RedemptionId`** trong `KetQuaCheckoutDto.VoucherRedemptionId` cùng phiên (`D-298`); `Sales` **ghép**
   `VoucherExpiresAtUtc` **additive** — **không** đổi nghĩa `ExpiresAtUtc` cũ (`D-301`).

### 6.1b `GenerateAsync` — sinh lô mã: thứ tự guard **trước khi ghi** (`D-302` + `D-309`)

`VoucherAdminAppService.GenerateAsync` (`VoucherAdminAppService.cs:92-174`) kiểm **theo đúng thứ tự** dưới đây; mọi
nhánh ném đều **trước** `InsertAsync` đầu tiên (`:165`, `autoSave: false`) ⇒ **0** dòng `ProVoucherCodes`:

1. **Quyền + trạng thái**: `[Authorize(Promotions.VoucherGenerate)]` ở method (`:92`); promotion phải
   `Approved`/`Active` — sai ⇒ **NÉM** `PromotionNotApproved` (`:98-103`).
2. **Số lượng tối thiểu**: `Quantity < 1` hoặc `MaxUsage < 1` ⇒ `AbpValidationException` (**400**, `:108-114`).
3. **Trần lô (`0108`)**: `Quantity > MaxBatchSize` (Setting `Promotions:MaxBatchSize`, `D-302`) ⇒ **NÉM `0108`**
   `BatchSizeExceeded` (`:116-122`) — kiểm **trước** mọi guard hình dạng mã.
4. **Hình dạng mã (`0107`)**: `Length ∉ [3, 32]` hoặc `len(Prefix) ≥ Length` ⇒ **NÉM `0107`** `VoucherCodeInvalid`
   (`:124-131`); `Prefix` chuẩn hoá ≤ 8 ký tự `A-Z`/`0-9`/`-` (`ChuanHoaPrefix`, `:284-305`).
5. 🆕 **Dung lượng không gian mã (`D-309`, `0107`)** — chống **treo vô hạn** cho input **hợp lệ** ở bước 4:
   `codespace = 36^(Length − len(Prefix))` (`Alphabet.Length = 36`, `:30`), tính bằng **vòng nhân có trần** dừng khi
   `codespace >= Quantity` (`:138-143`) để **không tràn** `long` khi `Length` tới 32; `codespace < Quantity` ⇒
   **NÉM `0107`** `VoucherCodeInvalid` + `WithData("length"/"prefix"/"codespace"/"quantity", …)` (`:145-152`).
   Ví dụ đo được: `{Length: 3, Prefix: "AB", Quantity: 100}` ⇒ `36 < 100` ⇒ **`0107`** ngay, **không** vào `while`.
6. **Sinh + ghi**: `while (HashSet.Count < Quantity)` sinh qua `TaoMa` (`:154-158`), rồi `InsertAsync(..., autoSave:
   false)` từng mã (`:160-166`) — cùng UoW của app service.

> 📌 Quan hệ `0108` ↔ `0107` (`D-309` ①(2)): `0108` nghĩa là *"lô vượt `MaxBatchSize`"* và **luôn kiểm trước**; khi
> `Quantity ≤ MaxBatchSize` mà **không gian mã không đủ** thì nguyên nhân là **tham số `Length`/`Prefix`** ⇒ dùng
> `0107`, **không** dùng `0108`, **không** thêm/đổi số mã lỗi. Cả hai là **400** (client sửa được tham số), **không** 500.

### 6.2 Cấp `UsageSequence` — đúng `D-295`, **cấm** tiếp tục trong transaction đã abort (`25P02`)

- Số cần cấp = **số nhỏ nhất chưa bị lượt còn hiệu lực giữ** (`Status <> Released`, cùng `TenantId, PromotionId,
  CustomerId`).
  - `MaxUsagePerCustomer` **có giá trị** ⇒ tìm trong `[1, MaxUsagePerCustomer]`; **hết số** ⇒ **NÉM ngay** `0104`
    (retry không nới trần).
  - `MaxUsagePerCustomer IS NULL` ⇒ **không trần theo khách**, tìm trong `[1, N+1]` với `N` = số lượt còn hiệu lực
    hiện có. **CẤM gán `1` cho mọi lượt** (`BR-04`, SRS v0.9 P2 #20).
- Ghi — **`ON CONFLICT` phải nêu ĐÚNG conflict target** của index chống trùng số
  `IX_ProPromotionRedemptions_Tenant_Prom_Cust_Seq` (`D-295` ①(4)):
  ```sql
  INSERT INTO "ProPromotionRedemptions" ( ..., "Status", "UsageSequence", ... )
  VALUES ( ..., 0, @seq, ... )
  ON CONFLICT ("TenantId", "PromotionId", "CustomerId", "UsageSequence")
         WHERE "CustomerId" IS NOT NULL AND "Status" <> 2
  DO NOTHING;
  ```
  - **0 dòng = trùng SỐ** của index đích ⇒ **thử lại** với số kế tiếp, **tối đa 5 lần** = **1 ghi + 4 thử lại**
    (`PromotionOptions.MaxUsageSequenceAttempts = 5`, `D-295`).
  - **`23505` từ index KHÁC** (ví dụ `IX_ProPromotionRedemptions_TenantId_IdempotencyKey` — trùng khoá phiên) ⇒ **NÉM**
    trong `SAVEPOINT`, **KHÔNG** thử lại. Nếu dùng `ON CONFLICT DO NOTHING` **không target**, xung đột khoá phiên bị
    **nuốt** ⇒ bị hiểu nhầm là "cạn `UsageSequence`" ⇒ trả `0104` sai thay vì kết quả idempotent (`D-295`; đã đo).
  - **Bắt buộc** `SAVEPOINT` mỗi lần thử: sau `23505`, PostgreSQL đánh dấu transaction `25P02` (aborted) — câu tiếp theo
    bị từ chối (chi tiết ngay dưới).
- 🚨 **Sau `23505`, PostgreSQL đánh dấu transaction `25P02` (aborted) — mọi câu tiếp theo bị từ chối cho tới
  `ROLLBACK`.** Vì vậy **mỗi lần thử** phải nằm trong **SAVEPOINT**:
  ```
  await tx.CreateSavepointAsync("sp_seq");
  try { <INSERT … ON CONFLICT DO NOTHING>; await tx.ReleaseSavepointAsync("sp_seq"); }
  catch (DbUpdateException / PostgresException 23505) { await tx.RollbackToSavepointAsync("sp_seq"); }
  ```
  (`tx = _dbContext.Database.CurrentTransaction` của UoW ABP). **Cấm** bắt `23505` rồi chạy tiếp trên transaction đã
  abort; **cấm** nuốt lỗi. Cách khác được phép: cấp số trong **UoW riêng** (`requiresNew`) — nhưng khi đó lượt `INSERT`
  **không** còn cùng transaction với `UPDATE` bộ đếm ⇒ **không** chọn (phá `ADR-13` ở `CommitAsync`).
- Hết 5 lần thử ⇒ **NÉM** `0104` (`409`) + `LogWarning` (**không** log mã voucher/`CustomerId`) + metric
  `promotion_voucher_reserved_total{result=usage_sequence_exhausted}` — **0 metric mới** (`D-295`).
- Khoá idempotency + unique `(TenantId, IdempotencyKey)` cũng là hàng rào (retry cùng phiên ⇒ 1 dòng).

### 6.3 `CommitAsync` — chốt lượt **cùng UoW** tạo đơn (`ADR-13`)

1. `IIdempotencyGuard` khoá `PromotionCommit:…` (`PromotionIdempotencyKeys.Commit(tenant, orderId, code)`); lặp cùng
   payload ⇒ trả kết quả cũ + `AlreadyApplied = true`. **Payload khoá giữ nguyên 3 thành phần
   `{RedemptionId, OrderId, Code}`** — **KHÔNG** đưa `OrderDiscountTotal` vào payload (`D-305` ①(4)): lệch tiền phải
   ném `0111`, **không** bị đổi thành `Messaging:0001` (`PayloadConflict`).
   **Trước** guard: kiểm `(TenantId, RedemptionId)` **và** `IdempotencyKey == Checkout:{tenant}:{CheckoutSessionId}:{VoucherCode}`
   (từ `CommitVoucherDto`) — lệch ⇒ **NÉM** `0105` (`D-298` ①(4): "lượt không khớp phiên/khách"; **0** cột/khoá mới).
   🆕 **Nhánh replay (`TraVeKetQuaCu`) vẫn phải SO**: so `applied.DiscountAmount` (đọc lại từ kết quả cũ) với
   `input.OrderDiscountTotal`; lệch ⇒ **NÉM `0111`**, **không** trả `AlreadyApplied = true` (`D-305` ①(4)) — chốt chặn
   **không** bị bỏ qua khi client thử lại. Chỉ khi khớp mới trả `AlreadyApplied = true`.
2. `UPDATE "ProPromotionRedemptions" SET "Status"=1, "OrderId"=@orderId, "AppliedAtUtc"=now
   WHERE "TenantId" IS NOT DISTINCT FROM @t AND "Id"=@redemptionId AND "Status"=0 AND "ExpiresAtUtc" > now`
   — **0 dòng** ⇒ **NÉM** `0105` ⇒ `Sales` **dừng**, trả báo giá mới (`SAL-002` BR-004: cấm đổi giá im lặng).
3. `ReservedCount − 1`, `UsedCount + 1` trên **dòng mã trước**, rồi **dòng promotion** (nếu `MaxUsageTotal` có giá trị)
   — cùng transaction, đúng thứ tự.
4. 🆕 **Chốt chặn `BR-06`/`0111` — cơ chế THẬT (`D-305` ①(3))**: ngay **sau** `UPDATE` lượt → `Applied` (bước 2, trong
   `ThucHienCommitAsync`, **cùng** transaction), tính
   ```sql
   SELECT COALESCE(SUM("DiscountAmount"), 0) FROM "ProPromotionRedemptions"
    WHERE "TenantId" IS NOT DISTINCT FROM @t AND "OrderId" = @orderId AND "Status" = 1;  -- 1 = Applied
   ```
   (v1: **đúng lượt vừa chốt**, vì unique `(TenantId, OrderId)` — §5.3/D5 — chặn lượt thứ hai) rồi so với
   `input.OrderDiscountTotal`; **lệch ⇒ NÉM** `new BusinessException(PromotionErrorCodes.RedemptionSumMismatch)`
   = `eCommerce:Promotion:0111` (**HTTP 500**; hằng **đã có** `PromotionErrorCodes.cs:79-80`). So **bằng phép so
   `decimal`**, **cấm** `==` (`T1`). `Sales` **cấm** tự so/tự ném `0111` (mã lỗi thuộc module `Promotions`, `B1`);
   `Promotions` **cấm** đọc `Sales` để lấy `Order` (§2.1).
5. Vi phạm unique `(TenantId, OrderId)` ở lượt voucher thứ hai của cùng đơn ⇒ `23505` ⇒ **NÉM** `0115`.
6. **ETO ra — `W25-2` KHÔNG dựng** (`D-306` ③, khuôn `D-254`: *cấm tự tạo ETO khi chưa có bên tiêu thụ*):
   `PromotionAppliedEto` (đáng lẽ phát **sau commit** qua outbox, không phát khi rollback, không phát cho lượt `Reserved`)
   **chưa có** trong code và **0** consumer ⇒ giữ **nợ có chủ `SAL-001`** (`D-283`); cùng lớp `VoucherReservedEto`/
   `VoucherReleasedEto`/`VoucherReversedEto` **hoãn có chủ `REP-001`** (nhánh huỷ đơn `SAL-004`). `qa` `W25-4` điều
   chỉnh `TC-42` thành "*`ReserveAsync` **không** phát `PromotionAppliedEto`*" — **không** đòi ETO chưa tồn tại.

> 📌 **Điểm gọi (chốt `D-298` ①(3), cập nhật `D-305` ①(2))**: `DonHangAppService.TaoTuCheckoutAsync` gọi `CommitAsync`
> **sau** `SetDiscount`+`don.Recalculate()` (`DonHangAppService.cs:111-113`) và **trước** tripwire
> `GrandTotal != tien.Total` (`:133`), **cùng** UoW ABP; truyền `don.DiscountTotal` làm `OrderDiscountTotal`
> (`:118-130`). Có `VoucherRedemptionId` mà sổ giải trình **không** có giảm từ mã ⇒ **400** (cuối dải
> `eCommerce:Sales:*`) — **cấm** tạo đơn mất giảm im lặng.

### 6.4 Nhả lượt — 3 đường, **phân biệt rõ** (SRS §3.1)

| Đường | Guard | 0 dòng | Bộ đếm |
|---|---|---|---|
| **Job `voucher-expiry`** (`Reserved`, `ExpiresAtUtc <= now`) | `Status=0` + hết TTL | **bỏ qua** + log `Debug`; **KHÔNG** phát metric — 2 counter chỉ cộng khi có lượt nhả **thật** (`daNha > 0`, `D-310`) | `ReservedCount − 1` (mã → promotion) |
| **Consumer `OrderCancelledEto`** (`Applied`, `D-148`) | `OrderId=@o AND PromotionId=@p AND Status=1` | **không có lượt** ⇒ bỏ qua + log `Debug` + metric; đã `Released/ORDER_CANCELLED` ⇒ phát lại, bỏ qua; `Reversed` ⇒ **NÉM** `0105` ⇒ DLQ | `UsedCount − 1` (mã → promotion) |
| **Consumer `OrderReturnedEto`** (`Applied → Reversed`) | `OrderId=@o AND Status=1` | không có lượt ⇒ bỏ qua; đã `Reversed` ⇒ bỏ qua; `Released/ORDER_CANCELLED` ⇒ **NÉM** `0105` ⇒ DLQ | **KHÔNG** hoàn lượt |

- Consumer `OrderCancelledEto`/`OrderReturnedEto` **chưa tồn tại** (`SAL-004`/`SAL-006`). `W25-2` **hoãn** 2 consumer;
  **cấm** tự tạo ETO trong `Promotions` (bài học `AQ9` của `PRO-002-arch`). Ghi nợ có chủ.
- Handler (khi có) **phải** mở UoW riêng `requiresNew: true, isTransactional: true` (luật E2).
- 🆕 **Job `voucher-expiry` — đặc tả THẬT (`D-306`, làm **ngay** trong `W25-2`)**:
  - **Tệp/DI**: worker riêng `modules/Promotions/eCommerce.Promotions.Application/Workers/VoucherExpiryWorker.cs`
    (khuôn `PromotionExpiryWorker`); đăng ký DI bằng `context.AddBackgroundWorkerAsync<VoucherExpiryWorker>()` trong
    `PromotionsApplicationModule.OnApplicationInitializationAsync` (`PromotionsApplicationModule.cs:35-38`) — **thiếu
    dòng này job không bao giờ chạy, không lỗi biên dịch**.
  - **Cờ tắt**: đọc `PromotionContractConsts.EnabledSettingName` (`PromotionContractConsts.cs:10`, khuôn
    `CheckoutAppService.cs:143`); tắt ⇒ **không** quét, **không** nhả.
  - **Khoá**: `PromotionLockKeys.Job("voucher-expiry", tenantId)` (`PromotionLockKeys.cs:19-21` — tên job **KHÁC**
    `"expiry"` của job cũ, tránh va khoá), TTL **300 s**; không giành được ⇒ log `Debug` + bỏ nhịp (thêm
    `LockSkipped`).
  - **Liệt kê tenant**: `DISTINCT TenantId` với `dataFilter.Disable<IMultiTenant>()` **có ghi lý do** + `ICurrentTenant.Change`
    (khuôn `PromotionExpiryWorker.cs:66-73,84-98`).
  - **Lô 500**, **mỗi lô một** UoW `requiresNew: true, isTransactional: true`. Điều kiện chọn: `Status = 0` (`Reserved`)
    **và** `ExpiresAtUtc <= now`. `UPDATE` **có guard** `Status = 0` — **0 dòng ⇒ bỏ qua** + log `Debug`
    (**không** phải lỗi, idempotent giữa 2 instance) và **KHÔNG** phát metric (`D-310` ①(a)).
  - **Bộ đếm**: `ReservedCount − 1` trên **dòng mã trước**, rồi **dòng promotion** (nếu `MaxUsageTotal` có giá trị),
    **sắp `Id`** — tránh `40P01` (SRS §8.2 #9); bắt `40P01` ⇒ thử lại **có trần** + log + metric.
  - **Hai counter** mỗi **lượt nhả thật** (`daNha > 0`): `promotion_voucher_reservation_expired_total{kind="voucher"}`
    **và** `promotion_voucher_released_total{reason="TTL_EXPIRED"}` (§8) — phát **SAU commit** (tránh đếm khi UoW
    rollback do deadlock), với `daNha` là **số dòng đã nhả** (`VoucherExpiryWorker.cs:180-186`).
  - **Metric chỉ khi có lượt nhả THẬT (`D-310`)**: nhánh **0 dòng** (lượt đã đổi trạng thái giữa 2 instance/lần quét)
    **chỉ** `LogDebug` (`VoucherExpiryWorker.cs:235`) rồi bỏ qua, **KHÔNG** gọi `Add(...)`. Lý do: counter `+= 0`
    **không tạo sample** ⇒ không phân biệt được "job chạy mà 0 việc" với "job không chạy"; tín hiệu đó đã có ở
    `LogInformation` **vô điều kiện** đầu nhịp (`:49`) + cuối nhịp (`:108`, in `tongNha`) + khoá bỏ nhịp
    (`LockSkipped`). **Cấm** thêm counter `kind="skipped"`/`Add(0)` cho nhánh này.
  - **Chỉ `Reserved`** — **cấm** nhả lượt `Applied` (đường đó thuộc consumer `OrderCancelledEto`, `SAL-004`).
- Job `voucher-counter-audit` (15 phút): **chỉ đọc**, so `UsedCount` = số lượt `Applied`+`Reversed`,
  `ReservedCount` = số lượt `Reserved`, ở **cả hai** mức; lệch ⇒ **cảnh báo + metric, cấm tự sửa** (`Q10` Closed) —
  **hoãn** khỏi `W25-2`.

### 6.5 Cờ tắt & đường lùi chạy được

`Promotions:Enabled = false` ⇒ `Sales` **không** gọi engine/reserve (`CheckoutAppService.cs:143` đọc cờ qua
`PromotionContractConsts.EnabledSettingName`) và `DiscountTotal = 0`; job `voucher-expiry` **đọc cờ** và **không** làm
gì (`D-306` ①(1)). Test khẳng định cờ tắt ⇒ **0** lệnh gọi engine / **0** lượt quét (cơ chế đỏ).

> ⚠️ **Quan sát as-built (không sửa ngoài phạm vi, `D-306` ⑥)**: `PromotionExpiryWorker` (job `promotion-expiry` cũ)
> **CHƯA** thấy đọc cờ `Promotions:Enabled` (`PromotionExpiryWorker.cs:50-125` chỉ quét `ProPromotions`) dù các bản
> trước của mục này nói "cả 2 job đọc cờ". ⇒ ghi **nợ có chủ `PRO-002`**, để `qa`/`rev` `W25-4` **xác nhận**; thiếu thật
> thì mở việc ở hàng `PRO-002` — **không** sửa trong `W25-2`. Câu `D-306` ⑥ không chặn `voucher-expiry`.

---

## 7. Phân quyền

### 7.1 8 permission — khai **hai nơi** (hằng số + provider)

| Permission | Method | Trạng thái |
|---|---|---|
| `eCommerce.Promotions` (gốc) | — | ✅ đã có |
| `.View` | `GetListAsync`, `GetAsync`, `GetRedemptionsAsync` | ✅ đã có |
| `.Manage` | *(promotion — `PRO-002`)* | ✅ đã có |
| `.Approve` | *(promotion — `PRO-002`)* | ✅ đã có |
| `.Import` | *(promotion — `PRO-002`)* | ✅ đã có |
| `.Export` | GĐ3 | ❌ **chưa có** — ngoài `W25-2` |
| 🆕 `.Voucher.Generate` | `GenerateAsync` | ❌ **W25-2 thêm** |
| 🆕 `.Voucher.Publish` | `IssueAsync`, `RevokeAsync` | ❌ **W25-2 thêm** |
| 🆕 `.Voucher.OverrideLimit` | `OverrideLimitAsync` | ❌ **W25-2 thêm** |

Khai tại `src/eCommerce.Domain.Shared/eCommerceConsts.cs:371-387` **và**
`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:210` (`AddChild`); thiếu một
nơi ⇒ `403` **cho mọi người kể cả admin** — lưới `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs`.
`.Voucher.Publish`/`.OverrideLimit` **chỉ** gán vai admin tenant (rủi ro tiền).

### 7.2 `[Authorize]` cấp **class**

- `VoucherAdminAppService` **bắt buộc** `[Authorize(eCommerceConsts.Permissions.Promotions.Default)]` ở **class**
  (khuôn `PromotionAppService.cs:25-26`); method hẹp thêm `[Authorize(Promotions.View/…)]`.
- `VoucherRedemptionService`: **không** phải app service công khai ⇒ **không** `[Authorize]`, **không** controller;
  quyền do endpoint checkout của `Sales` kiểm (đã `[Authorize]`).
- **Cấm** `[AllowAnonymous]`; `AllowlistCongKhaiTests` đỏ nếu thêm.

### 7.3 Tenant & cách ly

Mọi truy vấn lọc theo `TenantId` (ABP filter); unique NND đã có. `ICurrentTenant.Id` là `Guid?`: **`null` = ca 1-tenant
hợp lệ** (không ném); **`Guid.Empty` (`0…0`) ⇒ NÉM `0112`**. `TenantId = NULL` **được** `IS NOT DISTINCT FROM` trong câu
`UPDATE` để không bỏ sót dòng.

### 7.4 Bề mặt HTTP — **hai bề mặt** (`D-299`, khuôn `D-285`)

| Bề mặt | Route | Ai dùng | Ràng buộc |
|---|---|---|---|
| Tường minh | `/api/v1/voucher/**` — **7 route** | FE Next.js (BFF, `openapi.v1.json`) | Controller `VoucherController` trong `eCommerce.HttpApi`, `[Authorize]` class + method, chỉ gọi app service; test `VoucherSurfaceTests` đếm **đúng 7** |
| Auto (ABP) | `/api/app/voucher-admin/**` | Razor/tích hợp nội bộ | Sinh từ `IVoucherAdminAppService` (assembly đã nạp); **giữ nguyên**, **không** đổi tên |

**Cấm**: `[AllowAnonymous]`; `[RemoteService(Name = "voucher")]` (**chưa xác minh** ABP 10.7 — không suy đoán); controller
tường minh chạy `/api/app/voucher` (trái `D-13`: `/api/app/**` = conventional). **Cấm** route `reserve`/`commit`
(`TC-34`). Lùi: xoá `VoucherController.cs` (1 file) — 7 method vẫn dùng được qua route auto.

### 7.5 Audit hành động rủi ro tiền (`D-299`)

- `RevokeAsync`/`OverrideLimitAsync` giữ `reason` là **tham số method** ⇒ **ABP auditing** (mặc định bật; chỉ
  POST/PUT/DELETE) ghi vào `AbpAuditLogActions` (`Parameters varchar(2000)` **đã có** — migration
  `20261009102627_Added_Phase0_AuthModules.cs:308-317`). Thêm **một** `LogInformation` có `CorrelationId` + id mã/lượt
  (**không** log mã voucher/`CustomerId`/`reason` thô — `BR-22`/`XC-005`).
- `reason` **bắt buộc ≥ 10 ký tự** ⇒ 400 `AbpValidationException` (**0** mã mới); `OverrideLimit < Used + Reserved`
  ⇒ `0116`.
- **0 cột, 0 migration**; **không** thêm cột `Reason` (schema source `PRO-002`, `D-151`). **Nợ có chủ**: UI cần hiện lý
  do thu hồi ⇒ CR `PRO-002` thêm cột **nullable** `RevokeReason` (expand-only) — **ngoài v1**.
- ⚠️ **Thuộc tính ABP 10.7 "audit ghi tham số method" CHƯA ĐO** ⇒ `W25-4` **phải** có test integration khẳng định sau
  `RevokeAsync` có 1 dòng `AbpAuditLogActions.MethodName = 'RevokeAsync'` (thiếu ⇒ đỏ).

### 7.6 Map mã lỗi `01xx` → HTTP — **bắt buộc `W25-2`** (`D-144`, SRS §6.2)

Chỗ thêm: khối `options.Map(...)` trong `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs` — hiện **chỉ** map
`0001–0010` (`:449-458`). **Không** thêm ⇒ `BusinessException` mã `01xx` rơi về mặc định **`403`**, che lỗi thật
(`0104`/`0119` do `PromotionEngine.cs:372/383/411` ném sẽ hiện "không có quyền"). Thêm **17** dòng `options.Map` — map đủ `0101…0119` trừ `0109`/`0113` (cùng PR với 14 hằng **mới**; `0101`/`0104`/`0119` đã có hằng):

| Mã | HTTP | Mã | HTTP |
|---|---|---|---|
| `0101` `VoucherNotFound` *(đã có hằng)* | **404** | `0110` `VoucherChannelNotAllowed` | **400** |
| `0102` `VoucherOutOfWindow` | **400** | `0111` `RedemptionSumMismatch` | **500** |
| `0103` `VoucherRevoked` | **409** | `0112` `MissingTenantContext` | **400** |
| `0104` `CustomerUsageLimitExceeded` *(đã có hằng)* | **409** | `0114` `PromotionNotApproved` | **409** |
| `0105` `RedemptionNotReserved` | **409** | `0115` `SecondVoucherOnOrder` | **409** |
| `0106` `VoucherImmutable` *(giữ số, v1 không ném)* | **409** | `0116` `OverrideLimitTooLow` | **409** |
| `0107` `VoucherCodeInvalid` | **400** | `0117` `ReservationReleasedReplay` | **409** |
| `0108` `BatchSizeExceeded` | **400** | `0118` `CustomerRequiredForVoucher` | **400** |
| `0109` `PercentageInvalid` *(giữ số, không hằng — `D-296`)* | **400** | `0119` `PromotionUsageLimitExceeded` *(đã có hằng)* | **409** |
| | | `0113` `ShippingDiscountMismatch` *(giữ số, không hằng — `D-158`)* | **500** |

- Tên hằng là **đề xuất**; `be` đặt theo khuôn `PromotionErrorCodes` (một class, tiền tố `eCommerce:Promotion:`).
- **DoD/test (§10)**: probe `0104 ⇒ 409`, `0119 ⇒ 409`, `0101 ⇒ 404`, `0105 ⇒ 409`, `0102 ⇒ 400` qua HTTP thật; nếu rơi
  `403` ⇒ **đỏ**. Ca `0109`/`0113` **không** có đường ném ở v1 ⇒ không cần map/ném (giữ số).

---

## 8. Metric (`ADR-15`/`BR-21`/`CR-8`)

3 tên **đã đăng ký** (`eCommerceMetrics.cs:147-154`, factory `:576-581`). **As-built sau `W25-2`** (`D-306`, `D-310`):
**cả 3 đều có điểm phát** — `reserved` (`ReserveAsync`), `released` (`ReleaseAsync` + job `voucher-expiry` với
`reason=TTL_EXPIRED`), `reservation_expired` (**chỉ** job `voucher-expiry`, **chỉ khi** `daNha > 0` —
`VoucherExpiryWorker.cs:180-186`). Cả 3 tên **đã rời** `ChuaCoNoiPhat` (`eCommerceMetrics.cs:297-316`); danh sách nợ
**chỉ còn** `promotion_applied_total` (`:315`, chủ `SAL-001`, `D-283`). Việc `W25-2` **đã làm trong cùng PR**
(`D-306` ①(2)) — ghi lại để truy vết:

1. ✅ Thêm **điểm phát thật** cho `reservation_expired` **và** cho `released{reason=TTL_EXPIRED}` (job `voucher-expiry`);
2. ✅ **Xoá dòng `[PromotionVoucherReservationExpiredTotal]`** khỏi `ChuaCoNoiPhat` + sửa comment `:309-314`;
   **GIỮ `promotion_applied_total`** (chủ `SAL-001`, `D-283`) — **cấm** gỡ tên khi chưa có điểm phát
   (`MetricEmitterTests` kiểm **hai chiều**);
3. ✅ Cập nhật `XC-005` §5.1 + `TC-22` của `PRO-002`.

| Metric | Loại | Nhãn (`eCommerceMetricTags.Allowed`) | Điểm phát |
|---|---|---|---|
| `promotion_voucher_reserved_total` | Counter | `kind`, `result` ∈ {`ok`,`duplicate`,`rejected`,`usage_sequence_exhausted`} | `ReserveAsync` (`VoucherRedemptionService.cs:590-594`) |
| `promotion_voucher_released_total` | Counter | `reason` ∈ {`TTL_EXPIRED`,`ORDER_CANCELLED`,`ORDER_RETURNED`,`MANUAL`} | Release/job/consumer — `ReleaseAsync` + **job `voucher-expiry`** phát `reason=TTL_EXPIRED` (`D-306`) |
| `promotion_voucher_reservation_expired_total` | Counter | `kind` (=`voucher`) | **job `voucher-expiry`** (`D-306`), **chỉ** khi `daNha > 0` — phát sau commit (`VoucherExpiryWorker.cs:180-186`); nhánh 0 dòng **0** sample (`D-310`) |

- **0 metric mới** cho `usage_sequence_exhausted` — dùng **nhãn** `result` trên counter sẵn có (`D-295`).
  `D-306` **cũng 0 metric mới**: dùng **2** counter đã đăng ký, chỉ thêm **điểm phát**. `D-310`: 2 counter của job
  **không** cộng ở nhánh **0 dòng** (`counter += 0` không tạo sample) — tín hiệu "job chạy mà 0 việc" nằm ở
  `LogInformation` đầu/cuối nhịp + `LockSkipped`; **cấm** counter `kind="skipped"`.
- **Cấm** nhãn `promotionId`/`voucherCode`/`customerId` — đã nằm `eCommerceMetricTags.Forbidden`
  (`eCommerceMetrics.cs:366-371`).

---

## 9. Migration expand-only + đường lùi

### 9.1 Kế hoạch `W25-2`

- **0 migration cột/index**: mọi cột/index/`CHECK` của CR-1…CR-7 + CR-11 đã nằm trong `Add_Promotion_Module`
  (áp 26/11/2026). **Cấm** tạo migration rỗng hoặc sửa `Add_Promotion_Module` đã merge (`M4`).
- **`D-300`: đúng một** migration `Add_Voucher_Status_Check` do EF sinh (`M1`) — **2** `CHECK` miền `Status` (§5.5),
  **0** cột/index/dữ liệu; **`PRO-002` sở hữu mapping + migration**, `PRO-001` chỉ gửi CR. **Một chủ migration**
  (`09-parallel-sessions` §6); nếu `W25-2` không phải chủ duy nhất ⇒ **hoãn sang hàng `PRO-002`**, không chặn code.
- **Seed**: **0** dòng mới. `ProPromotionRuleCosts` đã seed 4 rule (`PromotionRuleCostSeedContributor`); `PRO-001`
  **không** thêm rule (`D-149`) ⇒ **không** chạm seeder. **Cấm** seed voucher nghiệp vụ thật (`BR-22`), trừ dev.

### 9.2 Trước khi áp (nếu có migration — hoặc khi môi trường chưa áp `Add_Promotion_Module`)

1. `git status --short` sạch; một chủ migration.
2. DB **có dữ liệu** ⇒ `pg_dump` + ghi đường dẫn dump vào handover (`M9`). Bảng `Pro*` mới, nhưng vẫn giữ luật.
3. Build binary **mới** rồi `dotnet run --project src/eCommerce.DbMigrator` — **cấm** `dotnet ef database update`
   (`M6`); kiểm `__EFMigrationsHistory` + `\d` bảng thật sau khi áp.

### 9.3 Đường lùi (viết trước giải pháp — K-05/A8)

- **Cờ tắt**: `Promotions:Enabled = false` ⇒ `Sales` không giữ/chốt lượt, `DiscountTotal = 0`; job tắt
  (`voucher-expiry` đọc cờ; `promotion-expiry` as-built **chưa** đọc — nợ `PRO-002`, §6.5).
- **Revert code**: gỡ 2 service + 2 job + 3 metric emitter khỏi `[DependsOn]`/DI; **giữ** bảng `Pro*`.
  Lùi `D-305` = **1 commit**: bỏ tham số `OrderDiscountTotal` khỏi `CommitVoucherDto`, bỏ đối số ở
  `DonHangAppService.cs:118-130`, bỏ khối so `Σ` trong `VoucherRedemptionService`, bỏ nhánh guard trong
  `VoucherRequestValidator`; hằng `0111` **để lại** (append-only). Lùi `D-306` = **1 commit**: xoá
  `Workers/VoucherExpiryWorker.cs` + dòng `AddBackgroundWorkerAsync<VoucherExpiryWorker>()` +
  **trả lại** dòng `[PromotionVoucherReservationExpiredTotal]` vào `ChuaCoNoiPhat`. **Cả hai: 0 migration, 0 dữ liệu**
  (lượt đã nhả do TTL là hành vi **đúng** — không phải dữ liệu mất).
- **Rollback migration `Add_Voucher_Status_Check`** (`D-300`): `DROP CONSTRAINT` 2 cái; **0** mất dữ liệu (bảng trống),
  **0** đổi API/DTO/`schema.d.ts`, **0** đổi số index NND (114).
- **Không** rollback cột/index đã merge; `Down()` của `Add_Promotion_Module` chỉ dùng dev/test.

---

## 10. Test / QA plan (khung — `be`/`qa` viết)

### 10.1 Theo tầng

| Tầng | Ca (neo `TC-xx` SRS §14.1) |
|---|---|
| Unit thuần | `TC-01/02/04/06/10/11/13/14/17/20/21/21b/22/23/24/26/27/29/30/31/35/36/38/39/40/42/43/44/46a/48a/49/52/55–68` (phần không chạm DB) |
| Unit phản chiếu / quét mã | `TC-30` (không `double`), `TC-31` (không `autoSave`), `TC-34` (0 route `reserve`/`commit`), `TC-40` (allowlist 7 method + 10 method `IPromotionAppService`), `TC-47` (`Pos` 0 tham chiếu), `TC-37` (metric), `PromotionSurfaceTests.RanhGioi__Promotions_Application_chi_dung_Messaging_EventCoverage` (`:97-108`, sửa allowlist thành **2** namespace) |
| Integration PostgreSQL 17 (Testcontainers) | `TC-03/05/07/08/09/12/15/16/18/19/32/33/41/45/53/59/62/63` — trần lượt, NND, UoW, job |
| Benchmark | `TC-59` p95 `ReserveAsync` < 150 ms (kể cả `MaxUsageTotal` có giá trị); p95 engine ≤ 20 ms là `TC-41` **của `PRO-002`** (trong `pro-001`, `TC-41` = "đổi tiền sau vận đơn") |

### 10.2 Bắt buộc có (NFR + cơ chế ĐỎ)

- **Đồng thời** `TC-08`: **200** request tranh **1** lượt (mỗi request `CustomerId` riêng, `MaxUsagePerCustomer = NULL`)
  ⇒ **đúng 1** thành công, **199** `0007`; `TC-09`: **50** request **cùng khách** (`MaxUsagePerCustomer = 2`) ⇒ **đúng 2**,
  48 `0104`.
- **Không hao lượt oan** `TC-05`: `Reserve` rồi bỏ ngang + chạy job `voucher-expiry` ⇒ `UsedCount = 0`,
  `ReservedCount = 0`; `TC-18`: chạy job **lần 2** ⇒ **0** thay đổi; `TC-60`: bộ đếm hai mức khớp sau nhả TTL **và**
  sau commit (3 ca **integration** — `qa` `W25-4`; `D-306` ①(4)). `TC-16` (huỷ đơn `Applied→Released`) **KHÔNG** phủ
  được ở `W25-2`/`W25-4` — **nợ có chủ `SAL-004`**; `qa` phủ phần làm được của `BR-25` bằng `TC-62` (unit).
- **`BR-06`/`0111` (`D-305`)** — 3 ca: (1) unit `TC-39`: sửa `DiscountAmount` của lượt trong sổ cái rồi `CommitAsync`
  ⇒ **NÉM** `0111`, **0** đơn; (2) qua `Sales`: `OrderDiscountTotal` **lệch** ⇒ `0111` + **0** `OrdOrders` + lượt **vẫn
  `Reserved`** (`ReservedCount` không đổi nhờ rollback); (3) replay cùng số ⇒ `AlreadyApplied = true` (và replay **lệch**
  số ⇒ **vẫn** `0111`, không `Messaging:0001`).
- **`UsageSequence`**: `TC-49` (giữ → hết TTL → giữ lại được số 1), `TC-68` (đua `23505` ⇒ 5 lần thử ⇒ `0104` +
  `LogWarning` + nhãn `usage_sequence_exhausted`); **`MaxUsagePerCustomer = NULL`** ⇒ cấp số tăng dần, **không** gán `1`.
- **Không tiếp tục transaction đã abort**: ca mô phỏng 2 lần `23505` liên tiếp trong **cùng** UoW — nếu thiếu
  `SAVEPOINT`, `Test` **đỏ** bằng `25P02`.
- **Cờ tắt**: `Promotions:Enabled=false` ⇒ **0** lời gọi engine/reserve, `DiscountTotal = 0`; job `voucher-expiry`
  **0** lượt quét (ca quét mã job phải thấy lời đọc `EnabledSettingName` — `D-306` ①(5)).
- **Đối soát** `NFR-05`/`NFR-14`: bộ đếm ↔ sổ cái khớp sau 1 nhịp; job **không** tự sửa.
- **Negative control** (`tools/agents/negative-control.ps1`): (a) bỏ `NULLS NOT DISTINCT` ⇒ `TC-33`/schema đỏ;
  (b) đổi filter index `OrderId` ⇒ `TC-15` đỏ; (c) bỏ guard `Status=Reserved` ở `CommitAsync` ⇒ `TC-13/14` đỏ;
  (d) thêm route `commit` ⇒ `TC-34` đỏ; 🆕 (e) **bỏ khối so `Σ`** trong `CommitAsync` ⇒ `TC-39` **đỏ** (`D-305`);
  🆕 (f) **gỡ lời gọi metric** của job ⇒ `MetricEmitterTests` **đỏ**; đặt khoá job trùng `"expiry"` ⇒ ca khoá **đỏ**
  (`D-306`).

### 10.4 Bằng chứng bắt buộc theo `D-298`…`D-306` (viết ở `W25-4`)

| `D-xx` | Ca đỏ khi vi phạm |
|---|---|
| `D-298` | (i) có mã ⇒ `ReserveAsync` gọi **đúng 1** lần, `EvaluateAsync` **0** lần; (ii) `VoucherRedemptionId`/`VoucherExpiresAtUtc` có giá trị; (iii) `redemptionId` phiên/tenant khác ⇒ `0105`, **0** đơn; (iv) `OrderDiscount ≠ 0` ⇒ 400 giữ nguyên; (v) cờ tắt ⇒ 0 lời gọi; (vi) 0 route `reserve`/`commit`; (vii) lá `CustomerGroup` + `CustomerGroupId = null` ⇒ `Applied = false` |
| `D-299` | (i) surface **đúng 7** route v1 + map permission, thiếu quyền ⇒ `403`; (ii) `[AllowAnonymous]` = 0; (iii) `reason` < 10 ⇒ 400, **0** đổi `Status`; (iv) sau `RevokeAsync` có 1 dòng `AbpAuditLogActions.MethodName = 'RevokeAsync'`; (v) log không chứa mã voucher/`CustomerId`; (vi) route auto `/api/app/voucher-admin/**` vẫn còn |
| `D-300` | (i) `UPDATE ProVoucherCodes SET Status='Expired'` ⇒ `23514`; (ii) `ProPromotionRedemptions.Status = 4` ⇒ `23514`; (iii) `\d` đủ 2 constraint; (iv) đếm index NND **vẫn 114**; (v) NC xoá 1 `HasCheckConstraint` ⇒ snapshot/migration lệch ⇒ đỏ |
| `D-301` | (i) quote có mã ⇒ `VoucherExpiresAtUtc` có giá trị, không mã ⇒ `null`; (ii) `ExpiresAtUtc` **vẫn** = min hạn giữ chỗ kho; (iii) FE chọn **min**; (iv) `gen:api` chỉ **thêm** trường |
| `D-302` | (i) `TtlSeconds` ngoài `60…86400` ⇒ 400, **0** dòng lượt; (ii) `Quantity = 10001` ⇒ `0108`, **0** dòng `ProVoucherCodes`; (iii) `Quantity ≤ 0` ⇒ 400; (iv) Setting `MaxBatchSize = "abc"/0` ⇒ mặc định 10000 + `hopLe=false` (không ném); (v) `GET …/redemptions?status=Reserved` chỉ trả `Reserved` + `.View`; (vi) chuỗi khoá **0** tiền tố `ecommerce:`; (vii) NC đổi default `MaxBatchSize` ⇒ (ii) đỏ; (viii) `validate-voucher` **0** ghi |
| `D-303`/`D-304` | (i) `Subtotal = 0`/thiếu ⇒ **400**, **0** dòng `ProPromotionRedemptions`; (ii) `Currency = ""`/`"vnd"`/`"VNDD"` ⇒ **400**; (iii) `CheckoutSessionId = ""`/> 128 ⇒ **400** ở **cả hai** record; (iv) `TtlSeconds ∈ {≤ 0, 59, 86401}` ⇒ **400**, **0** dòng; (v) reflection: `CheckoutSessionId` là `string` (**không** `Guid`) ở cả hai record; NC đổi `Subtotal` sang `decimal?`/bỏ `Range` ⇒ (i) đỏ |
| §7.6 map lỗi | Probe HTTP: `0104 ⇒ 409`, `0119 ⇒ 409`, `0101 ⇒ 404`, `0102 ⇒ 400`, `0105 ⇒ 409`; **rơi `403` ⇒ ĐỎ** (dấu hiệu thiếu dòng `options.Map` ở `eCommerceHttpApiHostModule`) |
| `D-305` | (i) `CommitVoucherDto` có **5** trường, trường 5 `Limit` cuối = `decimal` + `Range "0.00"…` (reflection); (ii) `OrderDiscountTotal < 0` ⇒ **400**, **0** dòng; (iii) `+=` sổ cái lệch ⇒ **`0111` (500)**, **0** `OrdOrders`, lượt **vẫn `Reserved`**; (iv) replay cùng số ⇒ `AlreadyApplied = true`; replay **lệch** số ⇒ **`0111`** (**không** `Messaging:0001`); (v) `Sales` **không** tự ném `0111`; **NC** bỏ khối so ⇒ `TC-39` **đỏ** |
| `D-306` | (i) `TC-05`: `Reserved` hết TTL ⇒ `ReservedCount = 0`, `UsedCount = 0`; (ii) `TC-18`: chạy lại ⇒ **0** thay đổi; (iii) `TC-60`: bộ đếm hai mức khớp; (iv) lượt `Applied` **không** bị nhả; (v) job đọc cờ `Promotions:Enabled`; (vi) khoá **`"voucher-expiry"` ≠ `"expiry"`**; (vii) `MetricEmitterTests` **hai chiều** (`reservation_expired` **có** lời gọi **và** **không** còn trong `ChuaCoNoiPhat`; `promotion_applied_total` **vẫn** trong danh sách); (viii) 0 ETO mới (`VoucherReservedEto`… vẫn **không** tồn tại) — `TC-42` điều chỉnh; **NC** gỡ metric / trùng khoá ⇒ đỏ |

### 10.3 Thêm file test mới (gợi ý, không bắt buộc tên)

`VoucherRedemptionServiceTests.cs`, `VoucherStateMachineTests.cs`, `VoucherCounterConcurrencyTests.cs` (integration),
`VoucherSurfaceTests.cs` (7 route + permission map), `VoucherMetricsTests.cs`. Đã có sẵn:
`PromotionVoucherGuardTests.cs`, `PromotionVoucherValidationTests.cs`, `PromotionSurfaceTests.cs`,
`PromotionV1SurfaceTests.cs`, `PromotionsModuleKienTrucTests.cs`.

---

## 11. Câu hỏi mở — **ĐÃ ĐÓNG toàn bộ** bởi `D-297`…`D-306`

> Không còn câu nào `Open` cho `W25-2`. Bảng dưới giữ **truy vết** câu → quyết định → hệ quả; câu mới phát sinh
> (nếu có) phải ghi `AQ7…` và **không** tự chốt.

| Id cũ | Nội dung | Chốt bởi | Hệ quả trong tài liệu |
|---|---|---|---|
| **AQ1** | `Sales` mang `VoucherCode`/`RedemptionId` qua đâu | **`D-298`** (a: DTO additive + server kiểm tenant/phiên); `D-305` bổ sung trường thứ 5 `OrderDiscountTotal` | §4.5 (3 DTO + **7** trường) · §4.2 (`CommitVoucherDto.CheckoutSessionId`, `Subtotal`, `OrderDiscountTotal`) · §6.3 (điểm gọi sau `SetDiscount`+`Recalculate` `:111-113`, trước `:133`; bước 4 cơ chế `Σ`/`0111`) |
| **AQ2** | Route `/api/app/voucher` vs auto `voucher-admin` | **`D-299`** (a: controller `/api/v1/voucher/**` **7 route**, giữ auto) | §4.1 · §4.3 · §7.4 |
| **AQ3** | Audit `RevokeAsync`/`OverrideLimitAsync` | **`D-299`** (a: ABP audit log + `LogInformation`, 0 cột) | §7.5 · §12-D3 |
| **AQ4** | 2 `CHECK` miền `Status` thêm ngay hay hoãn | **`D-300`** (thêm ngay, `PRO-002` sở hữu) | §5.2 · §5.5 · §9.1 · §9.3 |
| **AQ5** | `CustomerGroupId`/`IsFirstOrder = null` ⇒ 2 lá luôn `false` | **`D-298`** ①(5) (v1 chấp nhận + **công bố** cho Marketing; nối `CustomerGroupId` = 1 dòng khi có `D-xx`) | §3.4 · §6.1 · §12-D11 |
| **AQ6** | Đồng hồ UI theo mốc nào | **`D-301`** (trả **cả hai**, FE hiện **min**) | §4.2 · §4.5 · §6.1 bước 8 |
| **Q2** (SRS) | TTL giữ lượt + gia hạn | **`D-302`** (TTL **900 s** qua `TtlSeconds` bắt buộc, **không** gia hạn, **không** Setting TTL v1) | §3.6 · §4.2 · §6.1 bước 1 |
| **Q6** (SRS) | `MaxBatchSize` + rate limit | **`D-302`** (`10000` Setting + hàm thuần kẹp `1…100000`, **không** rate limit, vượt ⇒ `0108` trước khi ghi) | §3.6 · §4.1 |
| **Q7** (SRS) | Liệt kê lượt đang giữ | **`D-302`** (`GetRedemptionsAsync(status=Reserved)` + `RedemptionDto.ExpiresAtUtc` additive, **không** route mới, không gỡ tay v1) | §3.6 · §4.4 |
| **Q9** (SRS) | Dry-run | **`D-302`** (không v1; dùng `ValidateVoucherAsync` chỉ đọc) | §3.6 |
| **Q11** (SRS) | Kiểm chứng tiền tố khoá runtime | **`D-302`** (test chuỗi `PromotionLockKeys` 0 tiền tố + `ops` `MONITOR` khi có Redis, không chặn) | §3.5 · §3.6 |

> 📌 Nợ **có chủ** (không phải câu mở chặn `W25-2`): cột `RevokeReason` cho UI (`D-299`); nối `CustomerGroupId`/
> `IsFirstOrder` (`D-298` ①(5)); dry-run lô + gỡ lượt tay (`D-302`, `P11`); consumer `OrderCancelledEto`/`OrderReturnedEto`
> (`SAL-004`/`SAL-006`; `TC-16` chờ `SAL-004`); **ETO ra** `VoucherReservedEto`/`VoucherReleasedEto`/`VoucherReversedEto`
> (`REP-001`, nhánh huỷ `SAL-004`); `PromotionAppliedEto` (`SAL-001`/`D-283`); `PromotionExpiryWorker` **chưa** đọc cờ
> `Promotions:Enabled` (`PRO-002`, chờ `qa`/`rev` `W25-4` xác nhận) — `D-306` ③/⑥; metric `promotion_applied_total`
> (`SAL-001`) vẫn trong `ChuaCoNoiPhat`.

---

## 12. `docDeviation` (SRS ↔ as-built)

| # | SRS nói | As-built | Đề xuất ARCH |
|---|---|---|---|
| **D1** | §14.5 (23/11) "Module `Promotions` ❌ chưa có; mọi mục là thiết kế" | ✅ module **đã có** từ W24-2 (4 project, 7 bảng, migration 26/11/2026) | BA cập nhật §14.5 thành "đã có phần `PRO-002`; `PRO-001` chưa có service voucher" |
| **D2** | §11 "`PRO-001` cần **một** migration `Add_Promotion_Voucher_Limits`" | CR-1…CR-7 + CR-11 **đã gộp** vào `Add_Promotion_Module`; `W25-2` chỉ có **một** migration `Add_Voucher_Status_Check` (2 `CHECK`, `D-300`), **0** cột | BA sửa §11 theo `D-300`: "`PRO-002` sở hữu `Add_Voucher_Status_Check`; `PRO-001` chỉ gửi CR" |
| **D3** | §6.1 `RevokeAsync` "… cột `Reason` §5.2" | `ProVoucherCodes` **không có** cột `Reason`; SRS §5.2 cũng không định nghĩa | **`D-299`**: audit bằng `AbpAuditLogActions.Parameters` + `LogInformation` có `CorrelationId`, **0** cột/migration; `ba` sửa §6.1 (bỏ "cột `Reason`") |
| **D4** | §6.2 route `/api/app/voucher/**` | ABP auto từ `IVoucherAdminAppService` sẽ là `/api/app/voucher-admin/**` | **`D-299`**: bề mặt **v1** đi controller tường minh `/api/v1/voucher/**` (7 route); **giữ** auto `voucher-admin`; `ba` ghi bảng route as-built + v1 vào §6.2 |
| **D5** | §5.2 unique `(TenantId, OrderId) WHERE "OrderId" IS NOT NULL` | As-built `(TenantId, OrderId) WHERE "OrderId" IS NOT NULL **AND "VoucherCodeId" IS NOT NULL**` (`PromotionsDbContextModelCreatingExtensions.cs:203-206`, filter `:205`) | **Theo as-built**: vẫn cưỡng chế 1 voucher/đơn (`BR-05`); dòng tự động (`VoucherCodeId NULL`) do index khác quản. BA ghi rõ filter |
| **D6** | §6.2 output mẫu có `shippingDiscountTotal: 0` | §6.1 lại nói "**không** có `ShippingDiscountTotal` ở v1" (`D-158`) | **Bỏ** trường khỏi `VoucherQuoteResultDto` **và** mẫu JSON (giữ `D-158`); `ba` sửa §6.2 cùng PR |
| **D7** | §6.2 "`0109`/`0113` **giữ số**" | `PromotionErrorCodes` **không có** hằng `0109`/`0113`; `Promotion.cs:130` ném `AbpValidationException` (`D-296`) | **Theo as-built**: giữ số đã cấp, **không** thêm hằng, **không** đường ném v1. BA giữ nguyên |
| **D8** | §1/§3.2 đường giữ lượt do `Promotions` thực hiện | Engine **đã** ném `0007`/`0104`/`0119` ở **đường đọc** (`PromotionEngine.cs:372,383,411`, `D-291`/`D-292`) | **Theo as-built**: `ReserveAsync` tái dùng `EvaluateAsync` cho bước đánh giá (không ném lại mã khác); `UPDATE` có điều kiện vẫn là hàng rào cuối |
| **D9** | §13 "metric mới (3) ⚠️ chưa có trong code" | 3 tên **đã đăng ký**, **chưa** điểm phát, đang ở `ChuaCoNoiPhat` (`eCommerceMetrics.cs:312-314`) | **`D-283` + `D-297` ①(3)**: `W25-2` thêm điểm phát + **gỡ 3 tên** khỏi `ChuaCoNoiPhat` + `XC-005` §5.1 + `TC-22` **cùng PR**; `ba` sửa §13 thành "3 đăng ký; 0 nợ sau W25-2" |
| **D10** | §6.1 `IVoucherAdminAppService` (7 method) | Chưa có; `IPromotionAppService` **đã** 10 method | `W25-2` tạo **interface thứ hai cùng module** (+ `RedemptionFilterDto` mới), **không** thêm method vào `IPromotionAppService` (`TC-31` giữ 10) — `D-302` ①(3) |
| **D11** | SRS §0 `ADR-13` coi `Sales` đã "gọi contract của `Promotions`" cho voucher | As-built `Sales` **đã** tham chiếu Contracts nhưng chỉ gọi `EvaluateAsync` (auto, `VoucherCode=null`) | **`D-298` ①(1)**: `Sales` gọi `ReserveAsync` **thay** `EvaluateAsync` khi có mã; SRS ghi rõ điểm cắm **vừa** nối |
| **D12** | `PRO-002-arch` §2.1 allowlist **1** namespace `Messaging.EventCoverage` | `BR-02` buộc `IIdempotencyGuard` (`eCommerce.Messaging.Idempotency`) | **`D-298` (BR-02) + `D-297` ①(3)**: mở rộng allowlist **đúng 2 namespace** (§2.2) + sửa method `PromotionSurfaceTests.RanhGioi__…` (`:97-108`) cùng PR — thay đổi **chủ đích**, **không** phải vi phạm |
| **D13** | §6.1 (`pro-001:461-462`) **trước N101** ghi chữ ký cũ: `ReserveVoucherDto` **chưa** có `Subtotal`/`Currency`/`TtlSeconds`, `CommitVoucherDto` **chưa** có `CheckoutSessionId` | **Đã sửa ở N101 (W25-2, cùng PR)** — SRS §6.1 **bảng 461-462** nay đã có đủ 4 trường + bullet **478-480** (`Subtotal`+`Currency`, `TtlSeconds [Range(60,86400)]`, `CommitVoucherDto.CheckoutSessionId` `string` ≤128), khớp §4.2; `-arch` giữ chữ ký theo **`D-298` ①(4)** + **`D-302`** + **`D-303`** + **`D-305`** (`ReserveVoucherDto` = `VoucherCode, CheckoutSessionId, CustomerId?, ChannelCode, Lines, Subtotal, Currency, TtlSeconds`; `CommitVoucherDto` = `RedemptionId, OrderId, VoucherCode, CheckoutSessionId, OrderDiscountTotal`) | **Đã đóng một phần ở N101**; **còn lại — chủ `ba`, cùng PR**: (i) nêu **cơ chế validator** `Subtotal`/`OrderDiscountTotal` (`Range(typeof(decimal),…)`) + `Currency` (`^[A-Z]{3}$`) theo §4.2; (ii) bullet `ReserveVoucherDto.CheckoutSessionId` = `string` ≤128; (iii) `CheckoutDtos.cs:67-69`; (iv) bump SRS `v0.11` + changelog N101; 🆕 (v) `D-305` thêm **trường thứ 5** `OrderDiscountTotal` vào bảng SRS §6.1 `:461-462` + bullet `BR-06`. **v1 không** đổi thứ tự tham số nào khác |
| **D14** | SRS §7.1 (`:585-587`) trình bày `VoucherReservedEto`/`VoucherReleasedEto`/`VoucherReversedEto` như **thiết kế** sẽ phát | **0** tệp `*Eto` trong `Promotions` (`grep` = rỗng) ⇒ **0** consumer (`REP-001`/`Notifications` chưa có); `PromotionAppliedEto` **cũng chưa có** trong code | **`D-306` ③ (khuôn `D-254`)**: `W25-2` **KHÔNG** dựng ETO ra; ghi **hoãn có chủ** `REP-001` (nhánh huỷ đơn `SAL-004`), `PromotionAppliedEto` nợ `SAL-001`; `ba` ghi `docDeviation` cho SRS §7.1 **cùng nhánh**, `qa` `W25-4` điều chỉnh `TC-42` (không đòi ETO chưa tồn tại) |

---

## 13. Truy vết quyết định (`D-xx` ↔ mục)

| Quyết định | Nội dung áp | Mục |
|---|---|---|
| `D-143` | `CheckoutInput.OrderDiscount ≠ 0` ⇒ NÉM — **đã as-built** | §1.1, §6.1 |
| `D-144` | dải `eCommerce:Promotion:01xx`; `0113` giữ số | §3.1, §5.2, §12-D7 |
| `D-146` | vãng lai ⇒ `0118`, trước mọi ghi | §4.2, §6.1 |
| `D-147` | trần promotion `0119`, thứ tự mã→promotion | §5.2, §6.1–6.3 |
| `D-148` | huỷ đơn `Applied→Released`, `ORDER_CANCELLED`, hoàn `UsedCount` | §6.4 |
| `D-149` | chỉ giảm cấp đơn, **đúng 4** rule, 0 rule mới, 0 seed | §1.2, §9.1 |
| `D-150` | không đường sửa; ngoại lệ `OverrideLimitAsync` | §3.2, §4.1 |
| `D-151` | schema source `PRO-002`; `EvaluateAsync` chỉ đọc; 0 bảng trùng | §1.1, §5.2 |
| `D-158` | `FreeShipping` chặn; 0 `ShippingDiscountTotal` | §1.3, §4.2, §12-D6 |
| `D-159` | `Checkout:` chỉ của `ReserveAsync`; evaluate không khoá | §3.5, §6.1 |
| `D-216` | chuỗi WP code GĐ3 | header |
| `D-295` | `UsageSequence`: 5 lần THỬ = 1 ghi + 4 thử lại; hết ⇒ `0104` 409 + `LogWarning` + nhãn `result=usage_sequence_exhausted`; **0** metric mới; **cấm** tiếp tục transaction abort `25P02` | §3.5, §6.2, §8 |
| `D-296` | as-built `0109` = `AbpValidationException` 400, không `Code` | §5.2, §12-D7 |
| `D-297` | tạm duyệt v0.1 để code F3, **hiệu lực có điều kiện** `docrev` = `DOC-OK` 0 P1 trên bản chốt; `-arch` phải sửa theo `D-298`…`D-302` | header, §14, §15 |
| `D-298` | `AQ1`+`AQ5`: `Sales` DTO additive (3 DTO + **7** trường), server kiểm tenant+phiên; `ReserveVoucherDto` thêm `Subtotal`/`Currency`/`TtlSeconds`; `CommitVoucherDto` thêm `CheckoutSessionId`; `CommitAsync` sau `Recalculate()` trước tripwire; 2 lá khách **luôn `false`** | §1.2, §4.2, §4.5, §6.1, §6.3 |
| `D-299` | `AQ2`+`AQ3`: `VoucherController` `/api/v1/voucher/**` **7 route** + giữ auto; audit bằng ABP audit log, 0 cột/migration; `reason` ≥ 10 ⇒ 400 | §1.2, §4.1, §4.3, §7.4, §7.5 |
| `D-300` | `AQ4`: **một** migration `Add_Voucher_Status_Check` (2 `CHECK` miền `Status`), **`PRO-002` sở hữu**; lùi `DROP CONSTRAINT`; không chủ duy nhất ⇒ hoãn | §5.2, §5.5, §9.1, §9.3 |
| `D-301` | `AQ6`: trả **cả hai** `ExpiresAtUtc` additive, FE hiện **min**, Sales ghép không sửa `Promotions` | §4.2, §4.5, §6.1 bước 8 |
| `D-302` | `Q2/Q6/Q7/Q9/Q11`: TTL **900 s** (`TtlSeconds` bắt buộc 60…86400, không gia hạn) · `MaxBatchSize = 10000` (Setting + hàm thuần, không rate limit) · `GetRedemptionsAsync(status=Reserved)` + `RedemptionDto.ExpiresAtUtc` · không dry-run · test chuỗi khoá 0 tiền tố | §3.6, §4.1, §4.2, §10.4 |
| `D-303` | `Q16`: `CheckoutSessionId` (cả `ReserveVoucherDto`/`CommitVoucherDto`) là **`string`** ≤ 128 (= `CheckoutInput.IdempotencyKey` của `Sales`), **không** `Guid`; SRS giữ `string`; rỗng/>128 ⇒ **400** `AbpValidationException` | §4.2 |
| `D-304` | `Q17`: 2 ca `TtlSeconds` ngoài dải + `Subtotal`=0/`Currency` sai hoãn **`qa`** ở `W25-4`; **cơ chế** `[Range(typeof(decimal), "0.01", "99999999999999.9999")]` + `[RegularExpression("^[A-Z]{3}$")]` + `AbpValidationException` phải có ở `W25-2` | §4.2, §6.1, §10.4 |
| `D-305` | `BR-06`/`0111`: `CommitVoucherDto` thêm **trường thứ 5** `decimal OrderDiscountTotal` (additive, cho phép `0`, guard `Range "0.00"…`); `Sales` truyền `don.DiscountTotal` tại `:111-113`/`:118-130`, trước tripwire `:133`, cùng UoW; `Promotions.VoucherRedemptionService` **tự** tính `Σ DiscountAmount` của lượt `Applied` cùng `OrderId` rồi NÉM `0111` (**500**), nhánh **replay vẫn so** với payload khoá **giữ nguyên**; **0** migration/0 schema; lùi = 1 commit | header, §1.2, §4.2, §4.3, §6.3 bước 1/4/6, §10.2, §10.4, §12-D13 |
| `D-306` | job `voucher-expiry` nhả TTL **ngay `W25-2`**: `Workers/VoucherExpiryWorker.cs` + DI `PromotionsApplicationModule`; cờ `Promotions:Enabled` (`PromotionContractConsts.EnabledSettingName`); khoá `Job("voucher-expiry", tenantId)` TTL 300 s (khác `"expiry"`); lô 500/UoW `requiresNew`; `Reserved` + `ExpiresAtUtc <= now`, guard `Status=0`, 0 dòng ⇒ bỏ qua + log `Debug` (**không** metric — sửa câu chữ bởi `D-310`, xem dòng dưới); `ReservedCount − 1` mã→promotion, bắt `40P01` thử lại có trần; **2** counter (`reservation_expired{kind}`, `released{reason=TTL_EXPIRED}`); gỡ `reservation_expired` khỏi `ChuaCoNoiPhat`, **giữ** `promotion_applied_total`; ETO ra **hoãn có chủ**; nợ `PromotionExpiryWorker` chưa đọc cờ → chủ `PRO-002` | header, §1.1, §1.2, §4.2, §6.3 bước 6, §6.4, §6.5, §8, §9.3, §10.2, §10.4, §11, §12-D14 |
| `D-309` | `GenerateAsync` **pre-check dung lượng không gian mã**: `36^(Length − len(Prefix)) ≥ Quantity` (tính **sau** guard độ dài/prefix, **trước** vòng sinh); thiếu ⇒ **NÉM `0107`** `VoucherCodeInvalid` (**400**), **không** thêm mã nào; dùng **mã có sẵn**, **không** đổi số đã cấp, **0** migration | §4.1, §6.1b, §7.6 |
| `D-310` | **doc-only, giữ code**: 2 counter `reservation_expired`/`released{reason=TTL_EXPIRED}` **chỉ** phát khi `daNha > 0` (sau commit); nhánh **0 dòng** = `LogDebug` + bỏ qua, **KHÔNG** metric (`counter += 0` không tạo sample; tín hiệu "job chạy 0 việc" ở log đầu/cuối nhịp + `LockSkipped`); **cấm** counter `kind="skipped"`; giữ `VoucherExpiryWorker.cs:180-186` + test `D306_…` | §1.1, §6.4, §8 |
| `D-169` | tạm duyệt có điều kiện (nay đã đóng) — khuôn tài liệu | header |
| luật E2 (ABP module rules) | handler ghi dữ liệu mở UoW riêng | §6.4 |
| luật D3 (`NULLS NOT DISTINCT`) | giữ 9 index, test 114 | §5.3 |
| luật A1/A2 | `[Authorize]` cấp class; permission 2 nơi | §7 |
| **Nợ `PRO-002`** (`D-306` ⑥) | `PromotionExpiryWorker` as-built **chưa** đọc cờ `Promotions:Enabled` (`PromotionExpiryWorker.cs:50-125`) — `qa`/`rev` `W25-4` xác nhận; **không** sửa trong `W25-2` | §6.5 |
| **Nợ `REP-001`/`SAL-004`/`SAL-001`** (`D-306` ③) | ETO ra `VoucherReservedEto`/`VoucherReleasedEto`/`VoucherReversedEto` + `PromotionAppliedEto` **chưa dựng** (0 consumer); `TC-16` chờ `SAL-004` | §6.3 bước 6, §6.4, §11, §12-D14 |

---

## 14. Tự kiểm

| Mục | Đã kiểm bằng gì |
|---|---|
| Module + 7 bảng + migration đã có | `find modules/Promotions`; `Migrations/20261126224627_Add_Promotion_Module.cs` (đọc) |
| CR-1/2/3/4/5/6/11 **đã có** | `Promotion.cs:27-48`, `VoucherCode.cs:12-18`, `PromotionRedemption.cs:13-30`, EF `:40-75,126-225` |
| 9 index NND + test 114 | migration `:353-410`; `PostgreSqlSchemaTests.cs:222-228` |
| Không có service voucher | `find modules/Promotions -name "*.cs"` — 0 `Voucher*Service` |
| Mã lỗi chỉ 3 hằng `01xx` | `PromotionErrorCodes.cs:40-53` |
| Map HTTP chỉ `0001–0010` | `eCommerceHttpApiHostModule.cs:449-458` |
| Permission 5/8 | `eCommerceConsts.cs:371-387`; `eCommercePermissionDefinitionProvider.cs:210` |
| 3 metric đăng ký + đang nợ | `eCommerceMetrics.cs:206-208,279-281,312-314` |
| `PromotionOptions` thiếu TTL/attempts | `PromotionExpiryWorker.cs:16-20` |
| `Sales` guard + gọi Evaluate, **không** Reserve | `CheckoutAppService.cs:74-83,163-175` |
| `CheckoutInput` **không** có mã voucher | `CheckoutDtos.cs:26-88` |
| UoW tạo đơn + `SetDiscount` | `DonHangAppService.cs:86-122` |
| Rule registry đúng 4 | `PromotionRules.cs:14-66` |
| `IIdempotencyGuard` sẵn dùng | `IdempotencyGuard.cs:27-34` |
| Không `TODO`/mục bỏ ngỏ trong tệp | quét từ khoá bỏ-ngỏ = 0 |
| Đọc đúng dòng `D-297`…`D-302` trước khi sửa | `docs/nhanh/decisions.md:311-316` (đọc nguyên văn trong phiên N100) |
| Delta schema khớp `D-300` | **1** migration `Add_Voucher_Status_Check` (2 `CHECK`) do `PRO-002` sở hữu; **0** cột/index; index NND **vẫn 114** |
| DOCREV vòng 1 (v0.3) | `DOC-CHANGES` P1=0/P2=5/P3=6 → đã sửa **5 P2** (§6.2 conflict target; tên/file test ranh giới `PromotionSurfaceTests.cs:97-108`; §7.6 bảng map `01xx` + DoD; `Guid.Empty` ≠ `null`; `docDeviation` D13) + **6 P3** (`:372/383/411`, `Promotion.cs:130`, `TC-41`, 7 trường, bỏ vế build-đỏ, số dòng) |
| DOCREV vòng 3 — delta N101 (v0.5) | `DOC-OK` P1=0/P2=2/P3=3 → đã sửa **2 P2 vùng arch** (#1 `D13` → "đã đóng một phần ở N101", nêu phần SRS đã có + phần `ba` còn lại; #2 §4.2 nêu **cơ chế** `[Range(typeof(decimal),"0.01","99999999999999.9999")]`/`[RegularExpression("^[A-Z]{3}$")]` + `AbpValidationException` 400, tiền lệ `Promotion.cs:130`/`StockDocumentDtos.cs:185`, trần `numeric(18,4)` as-built) + **1 P3 arch** (#5 bullet `ReserveVoucherDto.CheckoutSessionId` = `string` ≤128) — **phần còn lại chủ `ba`/SRS: #2 (SRS), #3, #4, #5 (SRS)** ghi trong `D13`; **không** sửa SRS (ngoài vùng ghi arch) |
| Cổng tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` **PASS**; `npm --prefix tools run check:mermaid` **0 lỗi** (tệp không có Mermaid) |
| ⚠️ Giới hạn | `tools/agents/check-feature-doc.ps1` (16 mục) **KHÔNG** áp cho tệp `-arch` — **không** tuyên bố "16 mục xanh" |

---

## 15. Lịch sử tài liệu

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.1 | 27/11/2026 | ARCH (N100/W25-1) | Bản đầu: 15 mục. Chốt **schema delta = 0 cột** (CR-1…CR-7 + CR-11 đã nằm trong `Add_Promotion_Module` từ W24-2; test NND 114); đề xuất P2 `Add_Voucher_Status_Check` (`AQ4`). Thiết kế 2 máy trạng thái (mã/lượt) **NÉM**; 2 service voucher; cấp `UsageSequence` đúng **`D-295`** (SAVEPOINT, 5 lần thử, 0 metric mới); 3 metric gỡ khỏi `ChuaCoNoiPhat`; 3 quyền `.Voucher.*`; 2 job + 2 consumer hoãn (chờ `SAL-004`/`SAL-006`); **mở rộng allowlist B2 đúng 2 namespace** (`Messaging.Idempotency`); 12 `docDeviation`; 12 câu mở (`AQ1`–`AQ6` + `Q2/Q6/Q7/Q9/Q11`). |
| v0.2 | 27/11/2026 | ARCH (N100/W25-1) | Áp **`D-297`…`D-302`**, **đóng toàn bộ §11**. `D-298`: `ReserveVoucherDto` thêm `Subtotal`/`Currency`/`TtlSeconds`, `CommitVoucherDto` thêm `CheckoutSessionId`, chốt điểm gọi `CommitAsync` sau `Recalculate() :107` trước tripwire `:110`, 7 trường DTO additive phía `Sales` (§4.5); 2 lá khách **luôn `false`** + công bố Marketing. `D-299`: `VoucherController` tường minh `/api/v1/voucher/**` **7 route** + giữ auto `voucher-admin`; audit bằng ABP audit log, **0** cột/migration, `reason` ≥ 10 ⇒ 400. `D-300`: **một** migration `Add_Voucher_Status_Check` (2 `CHECK` miền `Status`) do **`PRO-002` sở hữu**. `D-301`: trả **cả hai** `ExpiresAtUtc`, FE hiện **min**. `D-302`: TTL **900 s** qua `TtlSeconds` bắt buộc (60…86400, không gia hạn, không Setting TTL v1), `MaxBatchSize = 10000` (Setting + hàm thuần, không rate limit, vượt ⇒ `0108` trước khi ghi), `GetRedemptionsAsync(status=Reserved)` + `RedemptionDto.ExpiresAtUtc` additive, không dry-run, test chuỗi khoá 0 tiền tố. Thêm §3.6, §4.5, §7.4–7.5, §10.4; §12 cập nhật D2–D4/D6/D9–D12; §13 thêm 6 dòng truy vết. |
| v0.3 | 27/11/2026 | ARCH (N100/W25-1 — sửa `docrev` vòng 1) | **P2 #1**: §6.2 `ON CONFLICT` nêu **đúng conflict target** `(TenantId,PromotionId,CustomerId,UsageSequence) WHERE "CustomerId" IS NOT NULL AND "Status" <> 2`; **0 dòng = trùng số** (thử lại), **`23505` từ index khác** (vd `IdempotencyKey`) = **NÉM trong `SAVEPOINT`, không thử lại**. **P2 #2**: test ranh giới đúng là method `PromotionSurfaceTests.RanhGioi__…` (`:97-108`, hiện vacuous; sẽ đỏ khi thêm `using …Idempotency`); bỏ vế "build đỏ vì thiếu ref" (`csproj:16` đã có). **P2 #3**: thêm §7.6 bảng **`01xx → HTTP`** + chỗ map `eCommerceHttpApiHostModule` + ca DoD (`0104⇒409`, `0119⇒409`, rơi `403` ⇒ đỏ). **P2 #4**: `ICurrentTenant.Id == null` = 1-tenant hợp lệ, **`Guid.Empty` ⇒ `0112`**. **P2 #5**: thêm `docDeviation` **D13** (`ba` sửa SRS §6.1 chữ ký mới ở `W25-2`). **P3**: `PromotionEngine.cs:372/383/411`; `Promotion.cs:130`; `TC-41` engine là của `PRO-002`; 7 trường DTO; `PostgreSqlSchemaTests` Assert `:227-228`. |
| v0.4 | 27/11/2026 | ARCH (N101/W25-2) | Áp **`D-303`**: `ReserveVoucherDto.CheckoutSessionId` và `CommitVoucherDto.CheckoutSessionId` (§4.2) **`Guid` → `string`** (không rỗng, ≤ 128 = `[StringLength(128)]` = `CheckoutInput.IdempotencyKey` của `Sales`). Sửa P3 còn lại: §1.2 + §7.6 "14 dòng map" → **17 dòng `options.Map`** (map đủ `0101…0119` trừ `0109`/`0113`). Không đổi nội dung ánh xạ hay mục nào khác. |
| v0.5 | 27/11/2026 | ARCH (N101/W25-2 — sửa `docrev` vòng 3 delta) | **P2 #1 (`D13`)**: chuyển "chờ `ba`" → **"đã đóng một phần ở N101"** — SRS §6.1 `461-462` + bullet `478-480` đã có đủ `Subtotal`/`Currency`/`TtlSeconds`/`CommitVoucherDto.CheckoutSessionId`; nêu phần còn lại chủ `ba` (#2 validator SRS, #3 bump `v0.11`, #4 `CheckoutDtos.cs:67-69`, #5 bullet `ReserveVoucherDto.CheckoutSessionId`). **P2 #2 (`D-304`)**: §4.2 nêu **cơ chế** từ chối `Subtotal = 0` — `[Range(typeof(decimal), "0.01", "99999999999999.9999")]` (trần `numeric(18,4)` as-built `:52-54,186`; tiền lệ `StockDocumentDtos.cs:185`/`DonHangDtos.cs:46`) + `Currency` `[RegularExpression("^[A-Z]{3}$")]` khớp `CHECK` `:73/:217` + `CheckoutSessionId` `[StringLength(128, MinimumLength=1)]`; lớp ném **`AbpValidationException` = 400** (as-built `Promotion.cs:130`; **không** `ArgumentException`); guard **tường minh** ở §6.1 bước 1 vì `VoucherRedemptionService` không phải `IApplicationService`. **P3 #5 arch**: thêm dòng §4.2 `ReserveVoucherDto.CheckoutSessionId` = `string` ≤128 (không chỉ `CommitVoucherDto`). Thêm §13 `D-304` + §10.4 ca đỏ; **không** đổi schema/code. |
| v0.6 | 27/11/2026 | ARCH (N101/W25-2) | Áp **`D-305`** + **`D-306`**: `CommitVoucherDto` **5** trường (`OrderDiscountTotal`) + `Promotions` tự so `Σ DiscountAmount` vs `OrderDiscountTotal` ⇒ `0111` (500), nhánh replay vẫn so; job `voucher-expiry` nhả TTL **ngay trong `W25-2`** (`Workers/VoucherExpiryWorker.cs` + DI, cờ `Promotions:Enabled`, khoá `"voucher-expiry"` TTL 300 s, lô 500/UoW `requiresNew`, guard `Status=0`) + 2 counter `reservation_expired{kind}`/`released{reason=TTL_EXPIRED}`; ETO ra **hoãn có chủ** `REP-001`/`SAL-004`/`SAL-001`; nêu **nợ as-built** `PromotionExpiryWorker` chưa đọc cờ ⇒ chủ `PRO-002`. |
| v0.7 | 28/11/2026 | ARCH (N103/W25-4 — áp `D-310` + `D-309`) | **Doc-only, giữ code.** (1) §6.4 sửa **hai chỗ** cho khớp as-built: dòng bảng job `voucher-expiry` (cột "0 dòng") và bullet đặc tả lô — **bỏ "+ metric"** ở nhánh **0 dòng**, ghi rõ 2 counter `promotion_voucher_reservation_expired_total`/`promotion_voucher_released_total{reason=TTL_EXPIRED}` **chỉ** cộng khi `daNha > 0` (**sau** commit, tránh đếm khi rollback); nhánh 0 dòng = `LogDebug` (`VoucherExpiryWorker.cs:235`) + bỏ qua, **KHÔNG** metric (lý do: `counter += 0` không tạo sample; "job chạy 0 việc" đã thấy ở `LogInformation` đầu/cuối nhịp `:49`/`:108` + `LockSkipped`); thêm bullet cấm `kind="skipped"`/`Add(0)`. (2) §1.1 (dòng "3 metric voucher") + §8 sửa **as-built cũ** "`reservation_expired` CHƯA có điểm phát / còn trong `ChuaCoNoiPhat`" → **cả 3 đã có điểm phát** (`VoucherExpiryWorker.cs:180-186`, `VoucherRedemptionService.cs:575-577`), cả 3 đã rời `ChuaCoNoiPhat` (`eCommerceMetrics.cs:297-316`), nợ **chỉ còn** `promotion_applied_total` (`:315`, `SAL-001`). (3) Thêm **§6.1b** đặc tả `GenerateAsync` **thứ tự guard trước khi ghi** + **pre-check dung lượng mã `D-309`** (`VoucherAdminAppService.cs:133-152`: `36^(Length−len(Prefix))`, vòng nhân có trần, thiếu ⇒ `0107` + `WithData`, ví dụ `{3,"AB",100}` ⇒ `36 < 100` ⇒ `0107`), nêu quan hệ với `0108` kiểm **trước**. §13 trỏ thêm §6.1b/§1.1/§8. **Giữ nguyên** code + test `VoucherExpiryWorkerTests.D306_…`; **0** thay đổi test/SRS/quyết định khác. |
