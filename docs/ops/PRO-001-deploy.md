# Triển khai `PRO-001` — 28/11/2026

> Phiên `N104`, hàng `W25-5` (`dong-wp`, F6), chuỗi `gd3-1`, WP-25. Nhánh `feat/gd3-wp25-voucher`
> (base `origin/tich-hop/gd3` @ `e7aa32c`). Quyết định nguồn: `D-216`, `D-295`…`D-311`
> ([decisions.md](../nhanh/decisions.md)); trạng thái cuối `W25-4` ở [handover-n103.md](../nhanh/handover-n103.md).
> Thiết kế as-built: [PRO-001-arch.md](../features/promotions/PRO-001-arch.md) **v0.7**.
> SRS: [pro-001-voucher-coupon.md](../features/promotions/pro-001-voucher-coupon.md) **v0.13**.
> Kiểm thử: [PRO-001-testplan.md](../tests/PRO-001-testplan.md) · Review: [PRO-001-review.md](../reviews/PRO-001-review.md)
> (vòng 2 `PASS · P1=0`). Khuôn tài liệu: [PRO-002-deploy.md](./PRO-002-deploy.md).
>
> Đây là WP **bổ sung voucher/coupon lên module `Promotions` đã có** (không tạo project mới):
> **1 migration** (2 `CHECK`, expand-only) · **7 route `/api/v1/voucher/**`** · **3 permission mới** ·
> **0 dòng FE** (`D-307`) · **0 đổi hạ tầng**.

## 1. Phạm vi

- **Nhánh/PR:** `feat/gd3-wp25-voucher` → `tich-hop/gd3` theo **chuỗi `D-216` phương án B**: nhánh WP xếp chồng trên
  `tich-hop/gd3`, **không** mở PR mỗi phiên (PR xếp chồng mở ở cuối chuỗi), **không** merge `main` từ nhánh này.
  Production nhận qua PR tích hợp `tich-hop/gd3` → `main` khi người duyệt. Phạm vi nhánh: **64 tệp, +20.575/−150**
  so base `e7aa32c` (gồm `W25-0` doc-gate → `W25-1` F0–F2b → `W25-2` F3 BE → `W25-3` F3 FE = 0 dòng → `W25-4` QA/REV).
- **Module:** `Promotions` (quản trị + giữ/chốt/nhả lượt) và `Sales` (checkout + tạo đơn mang mã). **0** project mới;
  `eCommerce.slnx` **0** đổi.
- **Migration:** đúng **1** migration EF sinh `20261127153549_Add_Voucher_Status_Check`
  (+ `.Designer.cs` + `eCommerceDbContextModelSnapshot.cs`), **expand-only**, nội dung **chỉ 2 `AddCheckConstraint`**:
  - `CK_ProVoucherCodes_Status` CHECK `("Status" IN ('Generated','Issued','Revoked'))` trên bảng `ProVoucherCodes`;
  - `CK_ProPromotionRedemptions_Status` CHECK `("Status" BETWEEN 0 AND 3)` trên bảng `ProPromotionRedemptions`.
  - **0** cột/0 bảng/0 index/0 dữ liệu; `Down()` = 2 `DropCheckConstraint`. **CHƯA ÁP** DB dev (`__EFMigrationsHistory`
    chưa có dòng nào của migration này) — auto mode **chặn**; **người** chạy `DbMigrator` ngoài auto mode (§3 bước 4).
    `pg_dump` trước đó: `artifacts/pg-dump/N101-W25-2.dump` (**576.910 byte**).
- **Hợp đồng API:** controller tường minh `src/eCommerce.HttpApi/Controllers/VoucherController.cs`,
  `[Route("api/v1/voucher")]`, `[Authorize]` cấp **class** = `Promotions.Default`, **đúng 7 route** với permission hẹp
  từng method (`GET /` và `GET /redemptions` ⇒ `Promotions.View`; `GET /{id}` ⇒ `View`; `POST /generate` ⇒
  `VoucherGenerate`; `POST /{id}/issue` + `POST /{id}/revoke` ⇒ `VoucherPublish`; `POST /{id}/override-limit` ⇒
  `VoucherOverrideLimit`). **0** `[AllowAnonymous]`; **0** route `reserve`/`commit` (hai method đó là hợp đồng nội bộ
  `IVoucherRedemptionService`, **không** mở HTTP). Giữ nguyên bề mặt auto `/api/app/voucher-admin/**` (`D-299`, hai
  bề mặt như `D-285`). Hợp đồng OpenAPI tái sinh (`D-308`): **75 → 82 path** (+7 `/api/v1/voucher/**`).
- **Mã lỗi:** Host map dải `01xx` tại `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:459-478` —
  **17 mã có map** (`0101`…`0108`, `0110`…`0112`, `0114`…`0119`; `0109`/`0113` **giữ số, v1 không có đường ném** —
  `D-296`/`D-158`). Thêm **1 mã `Sales`** `eCommerce:Sales:0005` (`VoucherKhongCoGiam`, HTTP **400**, map ở `:484`).
  Không map ⇒ `BusinessException` rơi về **403** và che lỗi thật.
- **Permission:** **3 quyền mới** `Promotions.Voucher.Generate` / `Promotions.Voucher.Publish` /
  `Promotions.Voucher.OverrideLimit` (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:391/394/397`; đăng ký
  `eCommercePermissionDefinitionProvider.cs:214-216`). Đây là quyền ABP cấp qua **role** ⇒ **phải gán cho role quản trị
  sau deploy** (§5.2 #3), nếu không admin cũng nhận **403**.
- **FE:** **0 dòng** `apps/admin-web/**` (`D-307` = `KHONG_CO_FE_DONE_LY_DO`); màn voucher thuộc WP `WEB-001`
  (`D-247`), **chưa có lịch**. `packages/api-client/**` tái sinh **additive**: `openapi.v1.json` **+731/−4** (75 → 82
  path; 4 dòng "xoá" là khối `expiresAtUtc`/`laKetQuaDaCo` được chèn lại — **không** mất trường/path),
  `schema.d.ts` **+417/−0**.
- **Hạ tầng:** **0** đổi `.github/**`, `docker-compose.yml`, `Dockerfile*`, `eCommerce.slnx`,
  `Directory.Packages.props`, `pnpm-lock.yaml` (đã kiểm `git diff --name-only e7aa32c..HEAD` cho các đường dẫn này:
  rỗng, trừ 2 tệp `packages/api-client/**`).

### 1.1 Vùng → thay đổi

| Vùng | Thay đổi chính |
|---|---|
| `modules/Promotions/eCommerce.Promotions.Domain/**` | `PromotionErrorCodes`: 19 số dải `0101`–`0119` (17 có đường ném ở v1, `0109`/`0113` giữ số); `PromotionSettingNames.MaxBatchSize` + `PromotionSettings.DocMaxBatchSize`/`KepMaxBatchSize` (`D-302`); `VoucherCode.Issue/Revoke/DoiGioiHan` (máy trạng thái **NÉM `0006`**, `0116`); `PromotionRedemption`; `PromotionLockKeys.Job("voucher-expiry", …)`; 2 `HasCheckConstraint` trong `PromotionsDbContextModelCreatingExtensions` |
| `modules/Promotions/eCommerce.Promotions.Application.Contracts/**` | `IVoucherAdminAppService` (**7 method**, không `UpdateAsync`), `IVoucherRedemptionService` (**4 method nội bộ**, **không** kế thừa `IApplicationService`), DTO voucher/redemption + `ReserveVoucherDto`/`CommitVoucherDto` (`D-303`/`D-305`) |
| `modules/Promotions/eCommerce.Promotions.Application/**` | `VoucherAdminAppService` (7 method; pre-check dung lượng mã `D-309` ⇒ `0107`); `VoucherRedemptionService` (reserve/commit/release/reverse, SAVEPOINT `sp_seq`/`sp_commit`, so `Σ` ⇒ `0111`); `UsageSequenceAllocator` (`D-295`: trần 5 lần thử, cấm gán `UsageSequence=1`); `VoucherRequestValidator`; `Workers/VoucherExpiryWorker` (job `voucher-expiry`); `PromotionsApplicationModule` thêm `AddBackgroundWorkerAsync<VoucherExpiryWorker>()`; `PromotionExpiryWorker` sửa nhẹ |
| `src/eCommerce.EntityFrameworkCore/**` | **1 migration** `20261127153549_Add_Voucher_Status_Check` (+ Designer + ModelSnapshot) |
| `src/eCommerce.HttpApi/**` | `Controllers/VoucherController.cs` (**7 route `/api/v1/voucher`**) |
| `src/eCommerce.HttpApi.Host/**` | `eCommerceHttpApiHostModule.cs`: **17 dòng `options.Map`** mã `01xx` + **1 dòng `Sales:0005`** |
| `src/eCommerce.Domain.Shared/**` + `src/eCommerce.Application.Contracts/**` | **3 quyền** `Promotions.Voucher.{Generate,Publish,OverrideLimit}` (hằng số + provider) |
| `modules/Sales/**` | `CheckoutInput.VoucherCode`/`TtlPhut`; `KetQuaCheckoutDto` +4 trường additive (`VoucherRedemptionId`, `VoucherDiscountTotal`, `VoucherExpiresAtUtc`, `VoucherReasonCode`); `TaoDonTuCheckoutInput.VoucherRedemptionId/VoucherCode`; `CheckoutAppService` gọi `ReserveAsync` **thay** `EvaluateAsync`; `DonHangAppService` gọi `CommitAsync` cùng UoW + guard `Sales:0005`; `SalesErrorCodes.VoucherKhongCoGiam` |
| `shared/eCommerce.Kernel/**` | 3 metric voucher (`reserved`/`released`/`reservation_expired`) **có điểm phát**; 2 counter của job **chỉ cộng khi `daNha > 0`** (`D-310`); `ChuaCoNoiPhat` chỉ còn `promotion_applied_total` (`SAL-001`) |
| `packages/api-client/**` | `openapi.v1.json` + `schema.d.ts` sinh lại (additive; `gen:api` không drift) |
| `test/**` | `test/eCommerce.Tests/Promotions/**` (9 tệp voucher + `PromotionSurfaceTests`) + `test/eCommerce.IntegrationTests/PostgreSqlVoucherWp25Tests.cs`; `tools/probe-voucher-wp25.ps1` |

### 1.2 Hành vi mới cần nói với bên gọi

1. **Checkout có `VoucherCode` ⇒ `ReserveAsync`, KHÔNG `EvaluateAsync`.** `CheckoutAppService` gọi
   `IVoucherRedemptionService.ReserveAsync` **thay** engine tự động (`D-298` ①(1)); **không** gọi cả hai. `Subtotal`/
   `Currency` lấy từ pipeline giá; TTL = `(TtlPhut ?? 15) × 60` (mặc định **900 s**). Kết quả trả thêm
   `VoucherRedemptionId`, `VoucherDiscountTotal`, `VoucherExpiresAtUtc`, `VoucherReasonCode` — **additive**;
   `ExpiresAtUtc` cũ vẫn là hạn giữ chỗ kho (`D-301`). Không có mã ⇒ đường tự động như trước.
2. **Tạo đơn có `VoucherRedemptionId` ⇒ `CommitAsync` cùng UoW**, sau `SetDiscount`+`Recalculate()` và trước tripwire
   `GrandTotal != tien.Total` (`DonHangAppService.cs:115-132`). Lỗi ⇒ rollback cả đơn lẫn lượt (không đơn, không chốt).
   Có `VoucherRedemptionId` mà sổ giải trình **không** có giảm từ mã ⇒ **400 `eCommerce:Sales:0005`**; `Σ` lệch ⇒
   **500 `0111`**.
3. ⚠️ **Nợ mở `D-311` P2 #3 (chưa đóng, trước `WEB-001`):** guard `BR-06` hiện **một chiều** — đơn **có giảm giá**
   nhưng **thiếu** `VoucherRedemptionId` **không** bị chặn ⇒ lượt vẫn `Reserved`, mã dùng lại được. Chưa lộ vì FE
   voucher (`WEB-001`) chưa có. Đây là **cổng cứng** trước khi mở màn voucher, không phải "đã ổn".
4. **Cờ tắt `Promotions:Enabled = false`:** `Sales` không gọi engine/reserve, `DiscountTotal = 0`; job `voucher-expiry`
   **đọc cờ và không làm gì**. Hệ quả vận hành: lượt đã `Reserved` **không** được job nhả trong thời gian cờ tắt
   (khác kỳ vọng "job vẫn chạy") ⇒ phải bật lại cờ để job nhả, hoặc xử lý theo §4. Route quản trị voucher **vẫn mở**.
5. **7 route v1** cho Web Admin (BFF), **giữ** auto `/api/app/voucher-admin/**`. Lỗi trạng thái/mã **NÉM** mã `0006`/
   `0107`/`0108`/`0114`/`0116`… theo bảng Host map; **không** trả danh sách vi phạm. `GET /{id}` không thấy ⇒ **404**
   `EntityNotFoundException` (**không** `Code`).
6. **Job `voucher-expiry` tự chạy** khi module nạp (nhịp 5 phút, `RunOnStart = true`, khoá
   `voucher-expiry`/tenant TTL 300 s, lô 500): nhả `Reserved` hết TTL. 2 counter `promotion_voucher_released_total
   {reason=TTL_EXPIRED}` và `promotion_voucher_reservation_expired_total{kind=voucher}` **chỉ cộng khi `daNha > 0`**
   (`D-310`) — nhánh 0 dòng chỉ `LogDebug`, **không** có sample.
7. **Quyền:** 3 quyền mới **chưa** gán cho role nào ở DB đích ⇒ admin cũng **403** cho `generate`/`issue`/`revoke`/
   `override-limit` cho tới khi gán (§5.2 #3). `GET` danh sách/chi tiết/sổ lượt chỉ cần `Promotions.View`.

## 2. Biến môi trường / Setting

| Tên | Mặc định | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

**Không thêm biến môi trường, không thêm secret.** 9 khoá là **ABP Setting**
(`PromotionSettingDefinitionProvider`), phạm vi `Default | Configuration | Global | Tenant` (**loại trừ user**) —
**không** phải `.env`. Đổi sau triển khai qua `setting-management/*` (Host đã nạp), **không cần deploy lại**.

### 2.1 Chín Setting (nguồn: `PromotionSettingNames.cs` / `PromotionSettingDefinitionProvider.cs` / `PromotionSettings.cs`)

| Khoá | Mặc định | Giá trị hợp lệ | Ai đọc |
|---|---|---|---|
| `Promotions:Enabled` | `true` | chỉ chuỗi `"false"` (không phân biệt hoa/thường) mới **tắt** | `Sales/CheckoutAppService`, `VoucherExpiryWorker` |
| `Promotions:MaxBatchSize` 🆕 | `10000` | số nguyên, **kẹp `1…100000`**; vượt ⇒ **`0108`** trước khi ghi dòng nào | `VoucherAdminAppService.GenerateAsync` |
| `Promotions:MaxNodes` | `64` | số nguyên `≥ 1`; sai ⇒ mặc định + `LogWarning` | validator, `PromotionAppService` |
| `Promotions:MaxDepth` | `8` | **kẹp `1…8`** (khớp `CHECK (Depth BETWEEN 1 AND 8)`) | validator, `PromotionAppService` |
| `Promotions:MaxLeaves` | `32` | số nguyên `≥ 1` | validator, `PromotionAppService` |
| `Promotions:MaxDeclaredCombinations` | `1024` | số nguyên `≥ 1` | `PromotionAppService` |
| `Promotions:MaxCandidatesPerCart` | `50` | số nguyên `≥ 1` | `PromotionEngine` |
| `Promotions:LeafVisitBudget` | `512` | số nguyên `≥ 1` | `PromotionEngine` |
| `Promotions:ComparisonBudget` | `5000` | số nguyên `≥ 1` | `PromotionEngine` |

- Mặc định production = mặc định trong code ⇒ **không cần thao tác khởi tạo**. Giá trị lạ **không ném** (một setting gõ
  sai không được làm chết sinh mã/checkout): dùng mặc định an toàn + `LogWarning`.
- Cấu hình **tuỳ chọn** của job (không bắt buộc, không có trong `appsettings.json`): `Promotions:JobPeriodMinutes`
  (mặc định **5**) và `Promotions:ExpiryJobName` (mặc định `expiry`) đọc qua `IConfiguration` section `Promotions`
  (`PromotionOptions`). Không đặt ⇒ mặc định; **không** phải env bắt buộc.
- ⚠️ **Chưa đo trên Host thật** rằng provider được ABP quét (cùng loại nợ `R8` của `PRC-002`). Nếu **không** được
  quét: `Promotions:Enabled` **vẫn bật** (chỉ `"false"` mới tắt), trần rơi về mặc định ⇒ **an toàn**, nhưng đổi setting
  sẽ **không có tác dụng**. Đây là **bước kiểm sau triển khai bắt buộc** (§5.2 #4).

## 3. Thứ tự triển khai

1. **Merge code BE trước** (nhánh → `tich-hop/gd3`). Biến thể `D-216` ③: push **fast-forward**
   `feat/gd3-wp25-voucher:tich-hop/gd3`; bị từ chối ⇒ **dừng**, **không** force/merge. `D-307`: **không** có bước deploy
   FE màn voucher (0 dòng; thuộc `WEB-001`).
2. **`pg_dump` trước khi áp migration trên DB có dữ liệu** (M9 / `ef-migration-postgres`):
   ```bash
   docker exec ecommerce-postgres pg_dump -U ecommerce -Fc ecommerce > artifacts/pg-dump/N104-W25-5.dump
   ```
   Ghi **đường dẫn + kích thước tệp** vào biên bản triển khai/handover; tệp dump để **ngoài repo** (O1). DB dev đã có
   dump `N101-W25-2.dump` (**576.910 byte**); **production chưa có** ⇒ bắt buộc dump trước.
3. **Build binary MỚI**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng **0 error, 0 warning mới**).
4. **Áp schema bằng binary MỚI qua `DbMigrator`** (M5):
   ```bash
   dotnet run --project src/eCommerce.DbMigrator
   ```
   **CẤM** `dotnet ef database update` (M6 / `D-75`, deny trong `.claude/settings.json`). Migration này **không** seed gì.
5. **Kiểm DB THẬT** (không tin `exit=0`):
   - `SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Add_Voucher_Status_Check%';`
     → **đúng 1 dòng**.
   - Hai `CHECK` **đích danh** (không đếm tổng):
     ```sql
     SELECT conname, pg_get_constraintdef(oid)
     FROM pg_constraint
     WHERE conname IN ('CK_ProVoucherCodes_Status','CK_ProPromotionRedemptions_Status')
     ORDER BY conname;
     ```
     → **đúng 2 dòng**: `CHECK (("Status")::text = ANY ((ARRAY['Generated','Issued','Revoked'])::text[]))` và
     `CHECK (("Status" >= 0) AND ("Status" <= 3))` (định dạng `pg_get_constraintdef` có thể khác chữ, nhưng phải đúng
     biểu thức). Thiếu dòng nào ⇒ **dừng**, không chạy tiếp.
   - `SELECT count(*) FROM "ProVoucherCodes";` / `"ProPromotionRedemptions"` → **không** được giảm so trước migration
     (migration không `DELETE`/`UPDATE`).
6. **Khởi động Host** (API + job) bằng binary MỚI. Nếu cùng máy còn chạy **Web (`src/eCommerce.Web`, storefront
   `/DatHang`)** thì **deploy lại Web bằng binary MỚI cùng lượt**: Web nạp `Sales`/`Promotions` **in-process**; WP này
   **0 dòng sửa** `src/eCommerce.Web/**` (không có wiring mới như `D-293`/`D-294` của WP-24), nhưng binary cũ **không** có
   `ReserveAsync`/`CommitAsync`/guard `Sales:0005` ⇒ storefront sẽ chạy hành vi **trước** WP-25. Không bắt buộc để boot.
7. **Gán 3 permission** `Promotions.Voucher.Generate`/`Publish`/`OverrideLimit` cho role quản trị (admin tenant)
   qua UI role/permission hoặc `PUT /api/v1/settings/permissions` — **trước** khi kiểm §5.2 #5. `Publish`/`OverrideLimit`
   là hành động rủi ro tiền: chỉ gán vai admin tenant.
8. **Job tự chạy** — không bật tay. Theo dõi log `JOB voucher-expiry: bắt đầu một lượt.` mỗi ~5 phút; cờ tắt ⇒ log
   `Promotions:Enabled=false ⇒ không làm gì.`
9. **Kiểm sau triển khai** theo §5.2.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| **Voucher ở checkout + job nhả TTL** (`Promotions:Enabled`) | Đặt `Promotions:Enabled = false` (global host hoặc tenant) qua `setting-management` ⇒ `Sales` **không** gọi engine/`ReserveAsync`, `DiscountTotal = 0`; job `voucher-expiry` **đọc cờ và không quét**; **0 deploy**, **0 mất dữ liệu** | **Ưu tiên số 1** — rẻ nhất. ⚠️ **Lượt đã `Reserved` sẽ KHÔNG được nhả** trong thời gian cờ tắt (job dừng theo cờ; consumer `OrderCancelledEto`/`OrderReturnedEto` **chưa tồn tại** — `SAL-004`/`SAL-006`) ⇒ `ReservedCount` đọng lại và mã bị giữ chỗ tới khi **bật lại cờ** (job nhả theo TTL) hoặc người xử lý. Trước khi tắt lâu, ghi nhận số lượt `Reserved` và hẹn bật lại. Route quản trị voucher **vẫn mở**. Guard `Sales:0004`/`Sales:0005` **vẫn còn** (thuộc code; muốn bỏ phải revert). |
| **Mã nguồn WP** | `git revert` theo **thứ tự ngược**: `6f618f5`/`696ce46` (bằng chứng test `D-309`) · `b3cb947` (vá `D-309`) · `27538f8` (F4 QA) · `11613ad` (api-client) · `8dd40e1` (`D-305`+`D-306` worker) · `d55cf56` (F3 BE + Sales wiring + migration) — commit `docs/**` không cần revert | Một lần revert code **không** chạm dữ liệu. ⚠️ Nếu DB **đã áp** migration: **giữ** `20261127153549_Add_Voucher_Status_Check` + `.Designer.cs` + `eCommerceDbContextModelSnapshot.cs` — xoá khỏi lịch sử sẽ khiến migration sau sinh lại `ADD CONSTRAINT` cho constraint đã tồn tại ⇒ lỗi. Cách an toàn: revert phần **dùng** module (controller, Sales wiring, worker, permission provider) và **giữ** 3 tệp migration/snapshot. |
| **Bề mặt `/api/v1/voucher/**`** | Deploy lại bản `admin-web` trước WP-25 (không có màn voucher) hoặc gỡ `VoucherController.cs` (7 method vẫn dùng được qua auto `/api/app/voucher-admin/**` — `PRO-001-arch.md` §7.4) | Nếu revert trong git: **không** `git checkout` thô `packages/api-client/**` về base — bản base **thiếu 10 path `/api/v1/reports/*`** (nợ `D-276`); chạy `pnpm spec:snapshot` + `pnpm gen:api` sau khi gỡ code để sinh lại đúng. |
| **Guard `Sales:0005` + trường voucher trong DTO `Sales`** | Revert `d55cf56` (hoặc `git revert` riêng phần `modules/Sales/**`) | Trường `VoucherCode`/`VoucherRedemptionId`/`VoucherDiscountTotal`/`VoucherExpiresAtUtc`/`VoucherReasonCode` là **additive** (`?`/mặc định) ⇒ client cũ **không** đổi hành vi khi còn binary mới; revert = về đúng hợp đồng trước WP-25. **0** dữ liệu mất (đơn/lượt cũ nằm nguyên trong `Pro*`). |
| **Schema — 2 `CHECK`** | **Migration expand-only: KHÔNG drop 2 `CHECK` khi lùi production.** Giữ constraint + dữ liệu, chỉ ngừng dùng code | `Down()` (2 `DropCheckConstraint`) **chỉ** dùng ở **dev/test** (bảng rỗng/không có `Status` sai). Production cần **người duyệt** + **BE sinh migration MỚI** drop constraint (một chủ migration `M2`/`M4`; mapping/migration do `PRO-002` sở hữu — `D-300`/`D-151`) — **không** viết tay, **không** sửa migration cũ. Chưa có mốc ⇒ mặc định **giữ vĩnh viễn**. |
| **Dữ liệu** | **0 mất dữ liệu** ở mọi phương án trên | Migration chỉ `ADD CONSTRAINT`; không `UPDATE`/`DELETE`; job chỉ đổi `Reserved → Released` (**hành vi đúng**, không phải mất dữ liệu). Lượt/đơn/mã do người dùng tạo vẫn nguyên trong `Pro*`. |

- **Không** revert migration đã áp; sửa sai ⇒ migration **mới** (M4). **Không** tắt bước CI nào để xanh.
- Thứ tự ưu tiên khi sự cố: ① tắt cờ (phút, không deploy — nhớ lượt `Reserved` đọng) → ② gỡ bề mặt `/api/v1/voucher/**`
  (deploy lại FE cũ / gỡ controller) → ③ revert code BE (giữ schema) → ④ chỉ drop `CHECK` khi có quyết định người duyệt.

## 5. Cổng (số đo thật)

| Lệnh | Kết quả | Nguồn |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | **exit 0** · **0 error · 9 warning** (0 warning mới; 9 warning nền ở `Address`/`Sales`) | N103 (ORCH, giữa phiên) |
| `dotnet test test/eCommerce.Tests --no-build` | **exit 0** · **2619/2619** (baseline 2593 ⇒ **+26**: 8 `VoucherV1SurfaceTests`, 4 `VoucherOpenApiContractTests`, 8 `VoucherDomainRulesTests`, 4 `VoucherMaxBatchSizeTests.D309_*`, 2 `VoucherRequestValidatorTests`…) | N103 — [PRO-001-testplan.md](../tests/PRO-001-testplan.md) §2/§4 |
| `dotnet test test/eCommerce.IntegrationTests` | **exit 0** · **162/162** (baseline 156 ⇒ **+6** `PostgreSqlVoucherWp25Tests`) | N103 |
| `pwsh -File tools/probe-voucher-wp25.ps1` | **exit 0** · **6/6 PASS** — ẩn danh **401×3**; `View` `GET /` **200**; `View` `POST /generate` **403**; admin `GET /` **200** | N103 — [PRO-001-testplan.md](../tests/PRO-001-testplan.md) §3.1 |
| `pnpm typecheck` + Vitest (`apps/admin-web`) | **0 lỗi** · **60/60** (không đổi — WP 0 dòng FE) | N103 |
| `pnpm gen:api` drift | **0** (82/82 path; `schema.d.ts` không đổi thêm) | N103 (`kiem-tat-ca` bước `gen:api`) |
| `pwsh -File tools/kiem-tat-ca.ps1` (nhẹ) | **exit 0** · **10/10 bước · 297,1 s** — build 0 err/9 warn · unit 2619/2619 · integration 162/162 · mermaid 214/0 · FE typecheck+Vitest 0 lỗi/60/60 · `gen:api` drift 0 · DB 25/25 · test cổng 60/60 | N103 (ORCH, đóng phiên) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | **exit 0** · PASS · **998 link · 1841 backtick** | N103 (đóng) |
| `npm --prefix tools run check:mermaid` | **exit 0** · **214 khối · 0 lỗi** | N103 (mở + đóng) |
| `node --test .claude/hooks/test/hooks.test.mjs` | **exit 0** · **49/49** | N103 (mở) |
| `check-agent-artifact -Kind testplan/review/decision` | **DAT** — BR chưa phủ = 0 · verdict `PASS` · P1=0 · decisions 117 dòng | N103 |
| REV vòng 2 | **`VERDICT: PASS` · P1=0 · P2=3 · P3=4** (P2 #1 đã vá `D-309`; P2 #2/#3 nợ `D-311`; P2 #4 pre-existing) | [PRO-001-review.md](../reviews/PRO-001-review.md) |

- **`-DayDu` — việc của ORCH, sau khi tài liệu này xong:** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu`
  (**19 bước** theo [handover-n103.md](../nhanh/handover-n103.md) §6); số bước + thời gian do **ORCH** ghi vào
  handover `N104` và `docs/nhanh/07-ke-hoach-trien-khai.md` §13. OPS **KHÔNG** chạy `-DayDu`/`dotnet build`/
  `dotnet test` trong phiên này (N69: không chạy build/test song song). Số trước G6: build **0 error · 9 warning**,
  unit **2619/2619**, integration **162/162** (N103).
- **Số đo riêng của OPS phiên `N104`** (chỉ cổng tài liệu; không build/test): xem §5.3.

### 5.1 PR / CI

- PR hiện đúng **2 required check** (`ci`, `cong-tai-lieu`); **không** bước nào bị `continue-on-error` để che lỗi.
- WP **không** chạm `.github/**`, `docker-compose.yml`, `Dockerfile*` ⇒ **không** phát sinh bước CI mới. `eCommerce.slnx`
  **0** đổi (không có project mới) ⇒ CI build phủ tự động theo solution sẵn có.
- Nhánh WP **không** mở PR riêng (`D-216` ③); push fast-forward `tich-hop/gd3` chỉ khi G6 xanh. Nếu bị từ chối ⇒
  **không** force, ghi `blocked`.

### 5.2 Kiểm sau triển khai

1. `GET /api/health` (Host) → **200**; BFF `apps/admin-web` (nếu deploy) → **200**.
2. **Migration + 2 `CHECK`:** chạy lại đúng 2 truy vấn §3 bước 5 → `__EFMigrationsHistory` **1 dòng**, `pg_constraint`
   **2 dòng**. Thiếu ⇒ dừng, xem §6.
3. **Gán 3 permission cho role quản trị** (§3 bước 7) rồi kiểm: user admin **có** `Promotions.Voucher.Generate/Publish/
   OverrideLimit`; user chỉ `Promotions.View` **không** có. Thiếu ở provider (403 cho cả admin) ⇒ ARCH/BE, không tự sửa
   DB quyền ngoài role ABP.
4. **Setting bắt buộc:** đọc `setting-management` (hoặc 1 lượt `GET /api/v1/voucher`) xác nhận 9 khoá §2.1 **tồn tại và
   đọc được** ở host (global) và một tenant. Không thấy ⇒ dừng, xem §6 (nợ `R8`).
5. **7 route v1:** ẩn danh `GET /api/v1/voucher` · `POST /api/v1/voucher/generate` · `GET /api/v1/voucher/redemptions`
   → **401**; token `View` `GET /api/v1/voucher` → **200** `ApiPage` (`Items`/`Total`/`Page`/`PageSize`);
   `GET /api/v1/voucher/{id-lạ}` → **404** (không `Code`); cùng user `View` gọi `POST /generate` → **403**; admin
   (đã gán quyền) gọi `GET /` → **200**.
6. **Sinh lô mã + pre-check `D-309`:** admin `POST /generate` với promotion `Approved`, `{Length:3, Prefix:"AB",
   Quantity:100}` (dung lượng `36^1 = 36 < 100`) → **400 + `0107`**, **0** dòng `ProVoucherCodes` mới. Lô hợp lệ
   (`Quantity ≤ 10000`) → **200**, đếm số dòng đúng `Quantity`; `Quantity = 10001` (mặc định `MaxBatchSize`) →
   **400 + `0108`** (trước `0107`). `codespace == Quantity` (ví dụ `{Length:4, Prefix:"AB", Quantity:1296}`) **không**
   ném.
7. **`issue`/`revoke`/`override-limit`:** `issue` mã `Generated` → **200** `Issued`; `issue` lần hai → **409 `0006`**;
   `revoke` thiếu `reason` (< 10 ký tự) → **400** validation; `override-limit` `newMaxUsage < Used+Reserved` → **409
   `0116`**.
8. **Checkout có mã:** `POST` checkout (`Sales`) với `VoucherCode` hợp lệ → kết quả có `VoucherRedemptionId` khác null,
   `VoucherDiscountTotal > 0`, `VoucherExpiresAtUtc` ≈ now + `(TtlPhut ?? 15)` phút, `VoucherReasonCode` null khi áp
   thành công; giỏ **không** mã ⇒ 4 trường null và hành vi như trước. Mã sai/ngoài cửa sổ → **400 `0101`/`0102`** hoặc
   kết quả không áp **không** ném (theo luồng engine) — ghi nhận đúng mã thực nhận.
9. **Chốt lượt khi tạo đơn:** `POST` tạo đơn từ checkout có `VoucherRedemptionId` → **200**; `ProPromotionRedemptions`
   chuyển `Reserved → Applied`, `UsedCount` +1/`ReservedCount` −1, **không** đơn mồ côi. Gửi `VoucherRedemptionId` sai/
   không thuộc phiên → **409 `0105`** và **0** đơn. **`Sales:0005`:** tạo đơn có `VoucherRedemptionId` mà sổ giải trình
   không có giảm từ mã (dựng ở dev: sổ không mang giảm voucher) → **400 `eCommerce:Sales:0005`**, **0** đơn, lượt vẫn
   `Reserved`.
10. **Job:** log `JOB voucher-expiry` xuất hiện; dựng 1 lượt `Reserved` có `ExpiresAtUtc` đã qua → sau 1 nhịp chuyển
    `Released`, 2 counter tăng **đúng 1 lần**; nhịp **0 dòng** ⇒ chỉ `LogDebug`, **không** có sample mới (`D-310`).
11. **Đường lùi (kiểm trước go-live):** đặt `Promotions:Enabled=false`, cùng giỏ có mã ⇒ `DiscountTotal = 0`, **0** lượt
    `Reserved` mới, job log "không làm gì"; **ghi nhận** số lượt `Reserved` đang đọng rồi **bật lại cờ**. Bật lại ⇒ job
    nhả tiếp.
12. **Biết trước, không phải sự cố:** `0109`/`0113` **không** có đường ném ở v1 (giữ số); `GET /{id}` không thấy trả
    **404** không `Code`; `POST /api/app/voucher-admin/generate` (auto surface) vẫn tồn tại song song.

### 5.3 Cổng OPS tự chạy phiên `N104`

| Lệnh | Kết quả |
|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | **exit 0** — PASS · **305 tệp · 1012 link · 1852 backtick · 0 tham chiếu chết** |
| `npm --prefix tools run check:mermaid` | **exit 0** — **214 khối mermaid · 0 lỗi** |

> Cả hai chạy lại **sau** khi tạo tệp này. OPS **không** chạy `dotnet build`/`dotnet test`/`-DayDu` (ORCH chạy).

## 6. Khi hỏng — ai gọi

- **Checkout trả giảm giá sai / thiếu `VoucherRedemptionId`/`VoucherDiscountTotal` / `Σ` lệch `0111`** ⇒ **BE** (`CheckoutAppService`,
  `VoucherRedemptionService`, `DonHangAppService`); đối chiếu [PRO-001-review.md](../reviews/PRO-001-review.md) và
  [PRO-001-arch.md](../features/promotions/PRO-001-arch.md) §4.2/§6.3. Không nới guard để xanh.
- **Tạo đơn có `VoucherRedemptionId` mà nhận **400 `eCommerce:Sales:0005`** hoặc đơn không chốt lượt** ⇒ **BE (Sales +
  Promotions)**; kiểm `CommitAsync` có chạy **cùng UoW** sau `Recalculate()` không (`DonHangAppService.cs:115-132`).
- **Đơn có giảm giá mà thiếu `VoucherRedemptionId` vẫn qua** ⇒ **nợ `D-311` P2 #3**, chủ **BE (Sales)**, mốc **trước
  `WEB-001`**; không phải sự cố mới của bản deploy này nhưng là **cổng cứng** trước khi mở màn voucher.
- **`generate` treo/không trả `0107` khi thiếu dung lượng mã, `0108` sai thứ tự** ⇒ **BE** (`VoucherAdminAppService`
  `D-309`); test khoá `VoucherMaxBatchSizeTests.D309_*`.
- **403 cho cả admin trên 4 method ghi** ⇒ **ARCH/BE** (provider/hằng số quyền) trước, rồi **OPS** gán role (§5.2 #3).
- **Mã lỗi trả 403 thay vì 400/404/409** ⇒ **BE/arch** — Host map `eCommerceHttpApiHostModule.cs:459-478`; map thiếu
  một mã là lỗi cấu hình, không phải lỗi quyền.
- **Setting không đọc được ở host/tenant** (đổi mà hệ thống vẫn mặc định) ⇒ **ARCH/QA** — cần Host thật kiểm ABP quét
  `SettingDefinitionProvider`; đường lùi kỹ thuật: chuyển provider sang `src/eCommerce.Domain/Settings/`.
- **Migration/2 `CHECK` sai, `DbMigrator` hỏng, `__EFMigrationsHistory` không có dòng** ⇒ **OPS** (chạy binary **mới**;
  **không** `dotnet ef database update`). Production: khôi phục từ `pg_dump`, **không** sửa tay DB (`M10`); drop
  constraint chỉ khi người duyệt + migration mới (§4).
- **Job `voucher-expiry` không chạy / nhả sai / chạy chồng** ⇒ **OPS** kiểm trước: dòng
  `AddBackgroundWorkerAsync<VoucherExpiryWorker>()` trong `PromotionsApplicationModule`, log nhịp, khoá
  `voucher-expiry`/tenant. Rồi **BE** (`VoucherExpiryWorker`) — `D-306`/`D-310`.
- **Lượt `Reserved` đọng sau khi tắt cờ** ⇒ **OPS** bật lại `Promotions:Enabled=true` để job nhả; theo dõi
  `promotion_voucher_reservation_expired_total`. Consumer huỷ/trả đơn **chưa có** (`SAL-004`/`SAL-006`) nên không có
  đường nhả khác ngoài TTL.
- **`packages/api-client` lệch / `gen:api` đổi `schema.d.ts`** ⇒ **FE**; chạy `pnpm spec:snapshot` + `pnpm gen:api`
  trên binary mới, **không** `checkout` thô về base (thiếu 10 path reports).
- **Sự cố tiền/đơn production không rõ nguyên nhân** ⇒ tắt cờ `Promotions:Enabled=false` trước (§4), ghi số lượt
  `Reserved` đang đọng, rồi gọi **ORCH** để mở `blocked`; không tự xử lý secret/NCC/production.

## 7. Nợ / rủi ro khi vận hành (không chặn triển khai)

- 🔴 **Migration `Add_Voucher_Status_Check` CHƯA ÁP** ở DB dev/production — chủ **người**; chạy `DbMigrator` ngoài auto
  mode rồi kiểm `__EFMigrationsHistory` + `pg_constraint` (§3 bước 4–5). Trước đó `Assets/schema.sql` của integration
  test **thiếu** 2 `CHECK` ⇒ `BR-09` mức DB (`23514`) **chưa** phủ; áp migration rồi tái sinh `schema.sql`.
- 🟠 **`D-311` P2 #2** — nhánh chốt **đầu tiên** của `VoucherRedemptionService.ThucHienCommitAsync` **chưa có test nào
  thực thi**; chủ **`qa`** (+`be` nếu cần seam), mốc **trước `WEB-001`**. Đây là chỗ chạm tiền/lượt.
- 🟠 **`D-311` P2 #3** — guard `BR-06` **một chiều** (đơn có giảm giá mà thiếu `VoucherRedemptionId` không bị chặn); chủ
  **`be` (Sales)**, mốc **trước `WEB-001`**. Cổng cứng, không phải "đã ổn".
- 🟡 **`P3 #9`** — ca chống treo của `D-309` chỉ chặn ở **mức test** (`Task.WaitAsync`); gỡ hẳn pre-check vẫn treo tiến
  trình (đã quan sát > 600 s khi NC chạy sai đột biến). Chủ `qa`/`be`, trước `WEB-001`.
- 🟡 **`P2 #4` (pre-existing `PRO-002`)** — `PromotionAppService` 7 chỗ + `PromotionRuleCostSeedContributor` 3 chỗ
  `autoSave: true`, trái `BR-13`/`NFR-09`; QA khoá bằng allowlist (lỗi **mới** vẫn đỏ). Ngoài phạm vi `PRO-001`.
- 🟡 **`P3 #6/#7/#8`** — comment `csproj` lỗi thời (Messaging/Idempotency); `VoucherExpiryWorker` nạp toàn bộ `Reserved`
  mọi tenant vào RAM để `Distinct`/`Take(500)` (tối ưu khi có tải thật); migration chưa áp ⇒ `schema.sql` thiếu 2 CHECK.
- 🟡 **Test service còn thiếu** — `BR-12`/`BR-17` (`0110`) và `BR-24` (`0118`) tại `ReserveAsync` chưa có test service;
  `BR-05` (`0115`) chưa có ca ép `23505` thấy mã. Nợ chủ **`qa`**, đã ghi [PRO-001-testplan.md](../tests/PRO-001-testplan.md) §5 #3/#4.
- 🟡 **Consumer `OrderCancelledEto`/`OrderReturnedEto` chưa tồn tại** (`SAL-004`/`SAL-006`) ⇒ nhả lượt `Applied` khi huỷ
  đơn và `Applied → Reversed` khi trả hàng **chưa có đường chạy**; hiện chỉ có TTL cho `Reserved`.
- 🟡 **Màn voucher** — chủ WP `WEB-001` (`D-247`/`D-307`), **chưa có lịch**; hợp đồng API đã sẵn (`D-308`).
- 🟡 **`promotion_applied_total`** còn trong `ChuaCoNoiPhat` (chủ `SAL-001`, `D-283`); 3 metric voucher **đã** có điểm phát.
- 🟡 **`D-297`** — tạm duyệt (M2) `PRO-001-arch.md`, **không** phải biên bản người; lô `D-277…D-311` **chờ người duyệt**;
  không tự đổi khi chưa duyệt. **`D-309`/`D-310`/`D-311`** cũng trong lô chờ duyệt / hàng đợi xem lại.
- 🟡 **Probe `403` phụ thuộc `PUT /api/v1/settings/permissions`** — nếu admin thiếu `Settings.Manage`, probe ghi
  `CHUA_DO`; **không** hạ kỳ vọng `403`.
- 🟡 **FE (nợ môi trường cũ)** — `pnpm build` **EPERM symlink** standalone (`K-05`/`N95`) và `pnpm lint` **no-script**
  (`N70`); **không** sửa `next.config.ts`; `kiem-tat-ca` vẫn 0.
- **Chờ người:** (a) áp migration bằng `DbMigrator` ngoài auto mode + `pg_dump` production trước; (b) duyệt lô
  `D-277…D-311`; (c) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) trước release; (d) xếp lịch
  `WEB-001` (màn voucher); (e) merge `main` + PR `tich-hop/gd3 → main` khi chuỗi xong.
