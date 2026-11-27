# Triển khai `PRO-002` — 27/11/2026

> Phiên `N98`, hàng `W24-5` (`dong-wp`, F6), chuỗi `gd3-1`, WP-24. Nhánh `feat/gd3-wp24-dieu-kien-km`
> (base `origin/tich-hop/gd3` @ `84157a1`). Quyết định nguồn: `D-151`, `D-216`, `D-283`, `D-284`, `D-285`…`D-294`
> ([decisions.md](../nhanh/decisions.md)). Thiết kế: [PRO-002-arch.md](../features/promotions/PRO-002-arch.md).
> SRS: [pro-002-cay-dieu-kien-engine-khuyen-mai.md](../features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md).
> Đây là WP **module mới**: **1 migration** · **4 project module** · **7 route `/api/v1`** · **3 màn FE**
> · **+3 dòng wiring `src/eCommerce.Web`** (`D-293`/`D-294` — storefront `/DatHang`).

## 1. Phạm vi

- **Nhánh/PR:** `feat/gd3-wp24-dieu-kien-km` → `tich-hop/gd3` theo **chuỗi `D-216` phương án B**: nhánh WP xếp chồng trên
  `tich-hop/gd3`, **không** mở PR mỗi phiên (PR xếp chồng mở ở cuối chuỗi), **không** merge `main` từ nhánh này.
  Production nhận qua PR tích hợp `tich-hop/gd3` → `main` khi người duyệt.
- **Module mới:** `Promotions` — **4 project** dưới `modules/Promotions/`:
  `eCommerce.Promotions.Domain`, `eCommerce.Promotions.Application.Contracts`, `eCommerce.Promotions.Application`,
  `eCommerce.Promotions.EntityFrameworkCore`.
- **Migration:** **1** migration EF sinh `20261126224627_Add_Promotion_Module` (+ `.Designer.cs` + `eCommerceDbContextModelSnapshot.cs`),
  **expand-only**: tạo **7 bảng** `ProPromotions`, `ProPromotionConditions`, `ProPromotionRuleCosts`, `ProVoucherCodes`,
  `ProPromotionMappings`, `ProFlashSaleSlots`, `ProPromotionRedemptions` + **20 index** (trong đó **9** unique
  `NULLS NOT DISTINCT`, **5** trong số đó là partial) + CHECK + FK `Restrict`. **Không** ALTER/DROP bảng cũ,
  **không** backfill (module mới, 0 dữ liệu cũ).
  - Đã áp ở DB dev phiên **N94** bằng `DbMigrator` sau `pg_dump`: `artifacts/pg-dump/N94-W24-2.dump` (**523.700 byte**)
    — [handover-n94.md](../nhanh/handover-n94.md) §3. Production **phải** `pg_dump` trước (§3 bước 2).
- **Hợp đồng API:** giữ **nguyên 10 route** auto `/api/app/promotion/**` (ABP auto-controller của `IPromotionAppService`)
  và mở **thêm 7 route** `/api/v1/promotion/**` cho BFF + `gen:api` (`D-285`). `IPromotionAppService` vẫn **đúng 10 method**
  (không `UpdateAsync`). 4 op **không** mở ở v1: `evaluate`, `validate-voucher`, `import-rule-costs`, `redemptions`
  (nợ có chủ `SAL-002`/`WEB-*` — [PRO-002-arch.md](../features/promotions/PRO-002-arch.md) §4.4).
- **FE:** **3 màn** `apps/admin-web/app/[business]/promotion/` — danh sách · tạo mới · dựng cây + duyệt
  (SRS §10.1; `D-285` ③). `schema.d.ts` sinh lại **additive** (59 → 75 path; 7 operation promotion + 10 path
  `/api/v1/reports/*` đã có trong code từ `W26-2` — nợ `D-276`).
- **Package:** **0** `PackageReference` mới (dùng version ABP sẵn có; `Directory.Packages.props` **0** đổi) ·
  `pnpm-lock.yaml` **0** đổi · **`eCommerce.slnx` +6 dòng**: thêm `<Folder Name="/modules/Promotions/">` với 4 project mới
  (bắt buộc để solution build module mới; additive, **không** đổi cấu hình package — chỉ nhánh tạo module chạm tệp này,
  K-10 không kích hoạt vì không có luồng song song nào khác sửa `eCommerce.slnx` trong WP-24).
- **Hạ tầng:** **0** đổi `.github/**`, `docker-compose.yml`, `Dockerfile*` (đã kiểm `git diff --stat 84157a1..HEAD`
  cho các đường dẫn này: **rỗng**).

### 1.1 File sản phẩm thay đổi (nhóm theo vùng)

| Vùng | Thay đổi chính |
|---|---|
| `modules/Promotions/eCommerce.Promotions.Domain/**` (mới) | Aggregate `Promotion` (máy trạng thái **NÉM `0006`**), `PromotionCondition`, `PromotionRuleCost`, `VoucherCode`, `PromotionMapping`, `FlashSaleSlot`, `PromotionRedemption`; `PromotionErrorCodes` (`0001–0010` + khai `0101/0104/0119` của `PRO-001`); `PromotionLockKeys`/`PromotionIdempotencyKeys`; `PromotionSettingDefinitionProvider` + `PromotionSettingNames` + `PromotionSettings`; validator/evaluator; `PromotionRuleRegistry` + 4 rule thuần; seeder `ProPromotionRuleCosts` (upsert, 0 DELETE) |
| `modules/Promotions/eCommerce.Promotions.Application.Contracts/**` (mới) | `IPromotionEngine` + request/result/breakdown; `PromotionRejectionReason` (12 giá trị, ghim ordinal); `IPromotionConditionRule`; `IPromotionAppService` (**10 method**); `IPromotionRuleCatalogAppService`; `PromotionContractConsts.EnabledSettingName` |
| `modules/Promotions/eCommerce.Promotions.Application/**` (mới) | `PromotionEngine` (**chỉ đọc**, 0 idempotency guard), `PromotionDiscountCalculator` (default **NÉM**), `EfPromotionDataSource`, `PromotionAppService` (10 method), `PromotionRuleCatalogAppService`, `PromotionExpiryWorker` (job `promotion-expiry`, nhịp mặc định 5 phút, `RunOnStart = true`) |
| `modules/Promotions/eCommerce.Promotions.EntityFrameworkCore/**` (mới) | Mapping 7 bảng `Pro*` + CHECK; gọi từ `eCommerceDbContext.OnModelCreating` qua `ConfigurePromotions(builder)` |
| `src/eCommerce.EntityFrameworkCore/**` | **7 `DbSet`** mới; `ConfigurePromotions`; migration `20261126224627_Add_Promotion_Module` (+ Designer + snapshot); `eCommerceEntityFrameworkCoreModule` thêm dependency `PromotionsEntityFrameworkCoreModule` |
| `src/eCommerce.HttpApi/**` | `Controllers/PromotionsController.cs` (**7 route `/api/v1/promotion`**), `Controllers/PromotionApiSupport.cs`; `eCommerce.HttpApi.csproj` +1 `ProjectReference` tới `Promotions.Application.Contracts` |
| `src/eCommerce.HttpApi.Host/**` | `eCommerceHttpApiHostModule.cs`: `DependsOn(PromotionsApplicationModule)` + **13 dòng `options.Map`** mã `Promotion*` (dòng `449–458` + `Sales 0004`/Money/Messaging); `eCommerce.HttpApi.Host.csproj` +1 ref |
| `src/eCommerce.Web/**` (`D-293`/`D-294`) | **+3 dòng wiring, BẮT BUỘC deploy lại Web**: `eCommerce.Web.csproj` +1 `ProjectReference` tới `modules/Promotions/eCommerce.Promotions.Application`; `eCommerceWebModule.cs` +`typeof(Promotions.PromotionsApplicationModule)` trong `[DependsOn]` + `context.Services.Replace(ServiceDescriptor.Singleton<IAbpDistributedLock, LocalAbpDistributedLock>())`. **Chỉ deploy Host là KHÔNG đủ**: `/DatHang` gọi `ICheckoutAppService` (ctor tiêm `IPromotionEngine`) ⇒ Web thiếu module = **HTTP 500** ở storefront (đo N98, G6); thiếu dòng `Replace` = **Web không boot** (`AbpInitializationException … Cannot resolve IDistributedLockProvider` vì `PromotionsApplicationModule` kéo `AbpDistributedLockingModule`/Medallion) |
| `modules/Sales/**` | `CheckoutAppService`: **NÉM `eCommerce:Sales:0004`** khi client gửi `OrderDiscount ≠ 0` (trước mọi tác dụng phụ); đọc cờ `Promotions:Enabled`; gọi engine lấy `DiscountTotal`; lượt dò giá dùng `KhongGhiSoGiaiTrinh = true`. `SalesErrorCodes` +`0004`; csproj +1 ref |
| `modules/Pricing/**` | `PriceCalculationDtos.KhongGhiSoGiaiTrinh` (**additive, mặc định `false`**); `PriceCalculationAppService` bỏ ghi sổ giải trình khi cờ `true` (`D-291` ②) |
| `shared/eCommerce.Kernel/**` | **9 metric** `promotion_*` (5 có điểm phát, 4 ở `ChuaCoNoiPhat` — `D-283`) |
| `src/eCommerce.Domain.Shared/**` | **5 quyền** `Promotions.{Default,View,Manage,Approve,Import}` (`eCommerceConsts.cs`) |
| `apps/admin-web/**` | 3 màn `promotion` + `components/promotion/{PromotionForm,TreeBuilder}.tsx` + `lib/promotion.ts` + `lib/promotion.test.ts` (15 ca) + menu |
| `packages/api-client/**` | `openapi.v1.json` + `src/schema.d.ts` sinh lại (**additive**; `gen:api` 2 lần cho cùng hash) |
| `test/**` | `test/eCommerce.Tests/Promotions/**` (14 tệp) + `test/eCommerce.IntegrationTests/PostgreSqlPromotionSchemaTests.cs`; `RepVanHanhTests.All.Count` 30 → 39 (registry tăng thật); `eCommerce.Tests.csproj` +4 `ProjectReference` |

### 1.2 Hành vi mới cần nói với bên gọi

1. **Client KHÔNG được gửi `OrderDiscount ≠ 0`** ở checkout — `Sales` **NÉM `eCommerce:Sales:0004` (HTTP 400)** trước mọi
   tác dụng phụ. Giảm giá cấp đơn **chỉ** đến từ engine khuyến mãi (`D-143`, `D-151`).
2. **Khi `Promotions:Enabled = true` (mặc định)**, checkout gọi thêm **một lượt dò giá** (`KhongGhiSoGiaiTrinh = true`,
   **không** ghi sổ giải trình) rồi mới tính tiền chính thức. Lượt dò lấy `Subtotal`/`CurrencyCode` **từ pipeline giá**
   (không `Sum(ListPrice)`, không hard-code `"VND"` — `D-291` ②). **0** ghi sổ hai lần.
3. **Cờ tắt** (`Promotions:Enabled = false`) ⇒ **không** gọi engine, `DiscountTotal = 0`, hành vi tính tiền như trước WP-24
   (vẫn còn guard `Sales:0004` — muốn bỏ guard phải revert code).
4. **Route v1 mới** cho Web Admin; lỗi cây/trạng thái **NÉM** mã `0001…0010` (HTTP map ở Host), **không** trả danh sách
   `ViPham`. `rule-catalog` trả **4 rule v1** kèm `IsSeeded` (`D-289`) và nhãn tiếng Việt từ code (`D-290`).
5. **Job `promotion-expiry` tự chạy** khi module được nạp (mỗi 5 phút, `RunOnStart = true`): `Approved → Active` khi tới
   `StartsAtUtc`, `Active/Paused → Expired` khi quá `EndsAtUtc`. Job **không** đọc cờ `Promotions:Enabled` (vẫn chạy).

## 2. Biến môi trường / Setting

| Tên | Mặc định | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

**Không thêm biến môi trường.** 8 khoá là **ABP Setting** (`PromotionSettingDefinitionProvider`), phạm vi
`Default | Configuration | Global | Tenant` (**loại trừ user**) — **không** phải secret/`.env`. Giá trị mặc định lấy từ
`SettingDefinition.DefaultValue`; đổi sau triển khai qua `setting-management/*` (Host đã nạp), **không cần deploy lại**.

### 2.1 Tám Setting (nguồn: `PromotionSettingDefinitionProvider.cs` / `PromotionSettingNames.cs`)

| Khoá | Mặc định | Giá trị hợp lệ | Ai đọc |
|---|---|---|---|
| `Promotions:Enabled` | `true` | chỉ chuỗi `"false"` (không phân biệt hoa/thường) mới **tắt**; còn lại bật | `Sales/CheckoutAppService` |
| `Promotions:MaxNodes` | `64` | số nguyên `≥ 1`; sai/âm/không phải số ⇒ **mặc định** + `LogWarning` | validator, `PromotionAppService` |
| `Promotions:MaxDepth` | `8` | **bị kẹp `1…8`** (trần cứng khớp `CHECK (Depth BETWEEN 1 AND 8)`) | validator, `PromotionAppService` |
| `Promotions:MaxLeaves` | `32` | số nguyên `≥ 1` | validator, `PromotionAppService` |
| `Promotions:MaxDeclaredCombinations` | `1024` | số nguyên `≥ 1` | validator, `PromotionAppService` |
| `Promotions:MaxCandidatesPerCart` | `50` | số nguyên `≥ 1` | `PromotionEngine` (ngân sách chạy) |
| `Promotions:LeafVisitBudget` | `512` | số nguyên `≥ 1` | `PromotionEngine` |
| `Promotions:ComparisonBudget` | `5000` | số nguyên `≥ 1` | `PromotionEngine` |

- Mặc định production = mặc định trong code ⇒ **không cần thao tác khởi tạo**. Giá trị lạ **không ném** (một setting gõ
  sai không được làm chết checkout): dùng mặc định an toàn + `LogWarning`
  *"PROMOTION: có setting cây/ngân sách không hợp lệ — dùng mặc định an toàn."*
- **Cấu hình tuỳ chọn của job** (không bắt buộc, không có trong `appsettings.json` hiện tại): `Promotions:JobPeriodMinutes`
  (mặc định **5**) và `Promotions:ExpiryJobName` (mặc định `expiry`) — đọc qua `IConfiguration` section `Promotions`
  (`PromotionOptions`). Không đặt ⇒ dùng mặc định; **không** phải env bắt buộc.
- ⚠️ **Chưa đo trên Host thật** rằng 8 khoá hiện diện trong `setting-management` (cùng loại nợ `R8` của `PRC-002`).
  Nếu provider **không** được ABP quét: `GetOrNullAsync` trả `null` ⇒ `Promotions:Enabled` **vẫn bật** (chỉ `"false"` mới tắt)
  và các trần rơi về mặc định ⇒ **an toàn**, nhưng đổi setting sẽ **không có tác dụng**. Đây là **bước kiểm sau triển khai**
  bắt buộc (§5.2 #3).

## 3. Thứ tự triển khai

1. **Merge code BE trước** (nhánh → `tich-hop/gd3`): 4 project module + migration + controller + Sales/Pricing/Host/Web
   wiring (`src/eCommerce.Web` — `D-293`/`D-294`).
   Biến thể `D-216` ③: push **fast-forward** `feat/gd3-wp24-dieu-kien-km:tich-hop/gd3`; bị từ chối ⇒ dừng, **không** force/merge.
2. **`pg_dump` trước khi áp migration trên DB có dữ liệu** (M9 / `ef-migration-postgres`):
   ```bash
   docker exec ecommerce-postgres pg_dump -U ecommerce -Fc ecommerce > artifacts/pg-dump/N98-W24-5.dump
   ```
   Ghi **đường dẫn + kích thước tệp** vào biên bản triển khai/handover. DB dev đã có dump `N94-W24-2.dump`; **production
   chưa có** ⇒ bắt buộc dump trước, tệp để **ngoài repo** (O1).
3. **Build binary MỚI**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng **0 error, 0 warning mới**).
4. **Áp schema bằng binary MỚI qua `DbMigrator`** (M5):
   ```bash
   dotnet run --project src/eCommerce.DbMigrator
   ```
   **CẤM** `dotnet ef database update` (M6 / `D-75`, deny trong `.claude/settings.json`). `DbMigrator` cũng chạy seeder:
   upsert **4 dòng** `ProPromotionRuleCosts` (0 DELETE, idempotent).
5. **Kiểm DB THẬT** (không tin `exit=0`):
   - `SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Add_Promotion_Module%';` → **đúng 1 dòng**.
   - `\dt "Pro*"` → **đúng 7 bảng**.
   - **9 index `NULLS NOT DISTINCT`** — kiểm `pg_indexes`/`pg_indexes.indexdef` (đích danh):
     `IX_ProPromotions_TenantId_Code` · `IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort` ·
     `IX_ProPromotionConditions_MotGoc` · `IX_ProVoucherCodes_TenantId_Code` ·
     `IX_ProPromotionRedemptions_TenantId_IdempotencyKey` · `IX_ProPromRedemptions_Tenant_Prom_Order_Voucher` ·
     `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq` · `IX_ProPromotionRedemptions_TenantId_OrderId_Voucher` ·
     `IX_ProPromotionRedemptions_TenantId_PromotionId_OrderId_TuDong`. Mỗi `indexdef` phải có `UNIQUE` + `NULLS NOT DISTINCT`
     (+ `WHERE …` với 5 index partial).
   - `SELECT "SystemName" FROM "ProPromotionRuleCosts" ORDER BY 1;` → **4 dòng** (`MinOrderAmount`, `ChannelScope`,
     `CustomerGroup`, `FirstOrderOnly`).
6. **Khởi động Host VÀ Web** (`D-293`/`D-294` — thiếu một cái là storefront hỏng) → **kiểm Web** → **deploy FE sau**:
   - **Host** (API + job) chạy binary MỚI như bước 3–5.
   - **Web (`src/eCommerce.Web`) cũng phải deploy lại bằng binary MỚI**, không dùng lại bản trước WP-24: Web chạy
     in-process nên **phải** nạp `PromotionsApplicationModule` (cho `ICheckoutAppService` → `IPromotionEngine`) và
     `Replace` khoá phân tán về `LocalAbpDistributedLock`. **Chỉ deploy Host ⇒ `GET /DatHang` = 500** (đo N98).
   - **Kiểm Web ngay sau khi khởi động** (chưa cần đăng nhập): `GET /Account/Login` = **200**; log **không** có
     `AbpInitializationException` / `Cannot resolve … IDistributedLockProvider` (có ⇒ thiếu dòng `Replace`, `D-294`).
   - **Deploy FE sau** (BE phải có 7 route trước; FE gọi `/api/v1/promotion/**`).
7. **Job tự chạy** — không bật tay. Theo dõi log `JOB promotion-expiry: bắt đầu một lượt.` mỗi ~5 phút.
8. **Kiểm sau triển khai** theo §5.2.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| **Bật/tắt khuyến mãi ở checkout** (`Promotions:Enabled`) | Đặt `Promotions:Enabled = false` (global host hoặc tenant) qua `setting-management` ⇒ `Sales` **không** gọi engine, `DiscountTotal = 0`; **0 deploy**, **0 mất dữ liệu** | **Ưu tiên số 1** — rẻ nhất. ⚠️ Job `promotion-expiry` **vẫn chạy** (vẫn đổi trạng thái `ProPromotions`) và route quản trị **vẫn mở** — chỉ đường tính tiền ngừng gọi engine. Guard `Sales:0004` (`OrderDiscount ≠ 0`) **vẫn còn** (thuộc D6; muốn bỏ phải revert code). Theo khai báo provider, `Configuration` nằm trong phạm vi nên `Promotions__Enabled=false` cũng đọc được — **chưa đo** trên Host thật, nên ưu tiên `setting-management` (lưu bền, có audit). |
| **Mã nguồn module/engine** | `git revert` các commit sản phẩm của WP trên nhánh/PR: `0ebc110` (F3 BE module) · `66deae3` (bề mặt v1) · `26a3a11` (F3 FE) · `48d9732`/`3d52d51`/`e602e92` (vá REV + test/probe) — theo **thứ tự ngược**; commit docs không cần revert | Một lần revert code **không** chạm dữ liệu. ⚠️ Khi revert **không** xoá tệp migration/`Designer`/`ModelSnapshot` khỏi lịch sử nếu DB đã áp — nếu xoá, migration sau sẽ sinh lại `CREATE TABLE` cho bảng đã tồn tại ⇒ lỗi. Cách an toàn: giữ migration + snapshot, chỉ revert phần code dùng module (bỏ `DependsOn`, `ConfigurePromotions`, 7 `DbSet`, controller, Sales/Pricing wiring) — đúng §9.3 của [PRO-002-arch.md](../features/promotions/PRO-002-arch.md). |
| **Wiring `src/eCommerce.Web/**` (`D-293`/`D-294`)** | `git revert` **cả cụm** commit vá Web (1 `ProjectReference` + dòng `DependsOn` `PromotionsApplicationModule` + dòng `Replace(... LocalAbpDistributedLock)`), rồi deploy lại **Web cùng Host** | ⚠️ **Không revert lẻ**: (i) bỏ `DependsOn` mà giữ `Replace` ⇒ Web boot nhưng `/DatHang` **500** (không resolve `IPromotionEngine`, khi Host vẫn nạp Promotions); (ii) bỏ `Replace` mà giữ `DependsOn` ⇒ **Web không boot** (`AbpInitializationException … Cannot resolve IDistributedLockProvider`). Revert **cả cụm** ⇒ Web về đúng hành vi trước WP-24; **0** migration, **0** dữ liệu, **0** package, **0** đổi route/`schema.d.ts`. |
| **Bề mặt `/api/v1/promotion/**`** | Deploy lại bản `admin-web` trước WP-24 (khuyến nghị) hoặc revert `26a3a11` + `66deae3` | Nếu revert trong git: **không** `git checkout` thô `packages/api-client/src/schema.d.ts` về base — bản base **thiếu 10 path `/api/v1/reports/*`** đã có trong code từ `W26-2` (nợ `D-276`); hãy chạy `pnpm spec:snapshot` + `pnpm gen:api` sau khi gỡ code promotion để sinh lại đúng. |
| **Schema — 7 bảng `Pro*`** | **Migration expand-only: KHÔNG drop 7 bảng khi lùi.** Giữ bảng + dữ liệu, chỉ ngừng dùng (cờ tắt/revert code) | `Down()` của migration **chỉ** dùng ở **dev/test** (`NFR-11`, `TC-27`); **cấm** chạy `Down()` trên production. **Ai quyết drop:** người duyệt (vùng cấm — phá huỷ dữ liệu) sau khi (i) **0** phiên bản nào còn tham chiếu `Promotions`, (ii) có `pg_dump` mới, (iii) **BE** sinh **migration mới** `Drop_Promotion_Module` bằng EF (một chủ migration, `M2`/`M4`) — **không** xoá tay, **không** sửa migration cũ. Chưa có mốc thời gian ⇒ mặc định **giữ vĩnh viễn** cho tới khi có quyết định. |
| **Ảnh hưởng `Sales`/`CheckoutAppService`** | Cờ tắt ⇒ về hành vi trước WP-24 (một lượt tính giá, `DiscountTotal = 0`); revert code ⇒ bỏ cả guard `Sales:0004` và lượt dò giá | `Subtotal`/`CurrencyCode` của lượt dò lấy từ pipeline giá; cờ `KhongGhiSoGiaiTrinh` là trường **additive, mặc định `false`** trên `PriceCalculationDtos` ⇒ caller cũ **không đổi hành vi**; revert = trả về đúng hợp đồng cũ. **0** dữ liệu mất. |
| **Dữ liệu** | **0 mất dữ liệu** ở mọi phương án trên | Không có `UPDATE`/`DELETE` mới trên bảng cũ; seeder chỉ upsert `ProPromotionRuleCosts`; `Redemption`/promotion do người dùng tạo vẫn nằm nguyên trong 7 bảng nếu chỉ tắt cờ. |

- **Không** revert migration đã áp; sửa sai ⇒ migration **mới** (M4). **Không** tắt bước CI nào để xanh.
- Thứ tự ưu tiên khi sự cố: ① tắt cờ (phút, không deploy) → ② revert bề mặt FE (deploy lại bản trước) → ③ revert code BE
  (giữ schema) → ④ chỉ drop bảng khi có quyết định của người duyệt.

## 5. Cổng (số đo thật)

| Lệnh | Kết quả | Nguồn |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | **exit 0** · **0 error · 9 warning** (0 warning mới sau vá `D-293`/`D-294`; N97: 55,1 s) | N97 + N98 (ORCH) |
| `dotnet test test/eCommerce.Tests` (`--no-build`) | **exit 0** · **2477/2477** — N98 = 2461 + **16** ca `PromotionsModuleKienTrucTests` (12 theory + 4 fact); 0 ca cũ đổi kỳ vọng | N97 baseline 2461 → N98 (qa/ORCH) — [PRO-002-testplan.md](../tests/PRO-002-testplan.md) §4.1 |
| `…PromotionsModuleKienTrucTests` + NC-N98-1/2 | **16/16 xanh**; NC: gỡ `Replace` ⇒ **exit 1** (1 failed), gỡ `DependsOn` `PromotionsApplicationModule` ⇒ **exit 1** (2 failed) → khôi phục ⇒ **exit 0** (16/16) | N98 (qa) — testplan mục Negative control |
| `dotnet test test/eCommerce.IntegrationTests` | **exit 0** · **156/156** | N97 |
| `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0** · **10/10 bước · 346,0 s** — unit 2461 · integration 156 · mermaid 214/0 · FE typecheck+Vitest 60/60 · `gen:api` không đổi · DB 25/25 · test cổng 60/60 | N97 |
| `pwsh -File tools/probe-promotion-wp24.ps1` | **401 ×3** · **200** (`rule-catalog` **4 tên**, `POST /`, `save-tree`, `approve`) · **400 `0001`** · **409 `0006`**; **403 CHƯA ĐO** | N97 — [PRO-002-testplan.md](../tests/PRO-002-testplan.md) §3.1 |
| `check-agent-artifact -Kind testplan/review/docreview/decision` | **DAT** — `BR trong SRS=20 · chua co trong testplan=0`; REV **PASS · P1=0**; DOC-OK **P1=0/P2=0** | N97 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | **exit 0** · PASS — 292 tệp · 977 link · 1757 backtick | N97 (sau) |
| `node --test .claude/hooks/test/hooks.test.mjs` | **exit 0** · **49/49** | N97 |

- **`-DayDu` — việc của ORCH, sau khi tài liệu này xong:** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu`; số bước + thời gian
  do **ORCH** ghi vào handover `N98` và `docs/nhanh/07-ke-hoach-trien-khai.md` §13. OPS **KHÔNG** chạy `-DayDu`/
  `dotnet build`/`dotnet test` trong phiên này (N69: không chạy build/test song song). Số trước G6: build **0 error ·
  9 warning**, unit **2477/2477** (N98); G6 lần 1 đã **ĐỎ** ở `/DatHang` 500 ⇒ sau vá `D-293`/`D-294` phải chạy lại
  **toàn bộ** `-DayDu`, bước [11] E2E **150/150**, `/DatHang` **200**.
- **Số đo riêng của OPS phiên `N98`** (chỉ cổng tài liệu, không build/test): xem §5.3.

### 5.1 PR / CI

- PR hiện đúng **2 required check** (`ci`, `cong-tai-lieu`); **không** bước nào bị `continue-on-error` để che lỗi.
- WP **không** chạm `.github/**`, `docker-compose.yml`, `Dockerfile*` ⇒ **không** phát sinh bước CI mới. `eCommerce.slnx`
  thêm 4 project ⇒ CI build tự động bao gồm module mới (không cần sửa workflow). `src/eCommerce.Web` sửa trong
  solution ⇒ CI build phủ tự động; lỗi DI lúc chạy chỉ bắt được bằng bước E2E ([11] `/DatHang`) của `-DayDu`.
- Nhánh WP **không** mở PR riêng (`D-216` ③); push fast-forward `tich-hop/gd3` chỉ khi G6 xanh.

### 5.2 Kiểm sau triển khai

1. `GET /api/health` (Host) → **200**; BFF `apps/admin-web` → **200**.
2. **Web (storefront) — `D-293`/`D-294`:** `GET /Account/Login` → **200** (Web boot được, log **không** có
   `AbpInitializationException`/`IDistributedLockProvider`). Đăng nhập rồi `GET /DatHang` → **200**, HTML có
   `name="KhachId"` — đo N98: **200** · **4815 B** (thí nghiệm boot của `D-294` đo **4914 B**: khác kích thước trang
   giữa 2 lượt, cùng 200 + `name="KhachId"`). **500** ở `/DatHang` = Web thiếu `PromotionsApplicationModule`;
   **Web không boot** = thiếu `Replace(... LocalAbpDistributedLock)`. Chỉ deploy Host mà quên Web ⇒ dừng, deploy Web.
3. **Setting bắt buộc:** đọc `setting-management` (hoặc 1 lượt `GET /api/v1/promotion/{id}`) xác nhận 8 khoá §2.1 **tồn tại
   và đọc được** ở host (global) và một tenant. Không thấy ⇒ dừng, xem §6 (cùng loại nợ `R8`).
4. **route v1:** `GET /api/v1/promotion/rule-catalog` **có** token → **200** + **4 tên** (`IsSeeded = true` sau seeder);
   **không** token → **401**. `GET /api/v1/promotion` → **200** `ApiPage` (`Items`/`Total`/`Page`/`PageSize`).
5. **Mã lỗi:** `save-tree` cây sai → **400 + `0001`**; `approve` lần hai → **409 + `0006`**; `POST` `Kind=FlashSale`/
   `DiscountKind=FreeShipping` → **400** validation.
6. **Checkout:** tạo + duyệt + `set-status Active` một promotion `MinOrderAmount`; checkout có `Subtotal` vượt ngưỡng ⇒
   `DiscountTotal > 0`, `Subtotal`/`CurrencyCode` khớp pipeline giá; sau đó `Cancelled`. Với `Promotions:Enabled=false`
   cùng giỏ ⇒ `DiscountTotal = 0` (kiểm đường lùi §4 trước go-live).
7. **Guard `Sales:0004`:** checkout gửi `OrderDiscount ≠ 0` → **400 + `eCommerce:Sales:0004`**, **0** đơn/0 giữ chỗ.
8. **Job:** log `JOB promotion-expiry` xuất hiện; promotion `Approved` có `StartsAtUtc` đã qua chuyển `Active`.
9. **403 (thiếu quyền):** **CHƯA ĐO ĐƯỢC** trên Host dev (`/api/identity/**` trả 404) — nợ `qa`/`W24-5`, **không** hạ kỳ vọng.
   Đo ở staging có Identity HTTP API (user thiếu `Promotions.Manage`/`Approve` ⇒ **403**).
10. **Biết trước, không phải sự cố:** gọi `/api/app/promotion/evaluate|validate-voucher` với `voucherCode` sai/hết lượt
   ⇒ **403** thay vì 404/409 vì `0101/0104/0119` **chưa map Host** (nợ `PRO-001`, §7). v1 **không** mở 2 route này.

### 5.3 Cổng OPS tự chạy phiên `N98`

| Lệnh | Kết quả |
|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | **exit 0** — PASS · **294 tệp · 988 link · 1774 backtick · 0 tham chiếu chết** |
| `npm --prefix tools run check:mermaid` | **exit 0** — **214 khối mermaid · 0 lỗi** |

> Cả hai chạy lại **sau** khi sửa tệp này (N98, số đo ở trên). OPS **không** chạy `dotnet build`/`dotnet test`/
> `-DayDu` (ORCH chạy).

## 6. Khi hỏng — ai gọi

- **Checkout trả giảm giá sai / `DiscountTotal` lệch / dòng đơn lệch** ⇒ **BE** (`PromotionEngine`, `PromotionDiscountCalculator`,
  `CheckoutAppService`); đối chiếu [PRO-002-review.md](../reviews/PRO-002-review.md). Không nới guard để xanh.
- **Cây điều kiện / mã lỗi `0001…0010` / HTTP map sai** ⇒ **BE/arch**; map ở `eCommerceHttpApiHostModule.cs:449-458`,
  bảng as-built ở [PRO-002-arch.md](../features/promotions/PRO-002-arch.md) §4.4.
- **`0101`/`0104`/`0119` trả 403 thay vì 404/409** ⇒ nợ **`PRO-001`** (chủ dải `01xx`); không sửa ở WP-24. Guard vẫn fail-closed
  (0 tiền giảm), chỉ sai mã HTTP.
- **`rule-catalog` rỗng / thiếu nhãn** ⇒ **BE** (`[ExposeServices]` DI + `PromotionRuleCostCatalog.Meta.Label`); đã khoá bằng
  `PromotionRuleDiTests`.
- **Setting không đọc được ở host/tenant** (đổi mà hệ thống vẫn mặc định) ⇒ **ARCH/QA** — cần Host thật kiểm ABP quét
  `SettingDefinitionProvider` trong assembly module; đường lùi kỹ thuật: chuyển provider sang `src/eCommerce.Domain/Settings/`.
- **Migration / 7 bảng / 9 index `NULLS NOT DISTINCT` sai, `DbMigrator` hỏng** ⇒ **OPS** (chạy binary mới; **không**
  `dotnet ef database update`). Dữ liệu production: khôi phục từ `pg_dump`, không sửa tay DB (`M10`).
- **Job kích hoạt/hết hạn sai giờ, chạy chồng nhiều instance** ⇒ **BE** (`PromotionExpiryWorker`; khoá `promotion:job:expiry`).
- **Web storefront `GET /DatHang` = 500 hoặc Web không boot sau deploy** ⇒ **OPS** kiểm trước (binary Web có phải bản
  MỚI + đủ 3 dòng wiring §1.1); thiếu `DependsOn`/`ProjectReference` ⇒ 500, thiếu `Replace` ⇒ không boot. Rồi **BE**
  (`src/eCommerce.Web/eCommerceWebModule.cs`) — `D-293`/`D-294`, khoá bằng `PromotionsModuleKienTrucTests`.
- **FE 3 màn lỗi / `schema.d.ts` lệch** ⇒ **FE**; `gen:api` **không** được đổi `schema.d.ts` ngoài ý muốn.
- **Sự cố tiền/đơn production không rõ nguyên nhân** ⇒ tắt cờ `Promotions:Enabled=false` trước (§4), rồi gọi **ORCH** để mở
  `blocked`; không tự xử lý secret/NCC/production.

## 7. Nợ / rủi ro khi vận hành (không chặn triển khai)

- 🟠 **`0101/0104/0119` chưa map Host** ⇒ gọi `/api/app/promotion/evaluate|validate-voucher` với mã sai/hết lượt trả **403**
  thay vì 404/409. Chủ **`PRO-001`**; v1 không mở 2 route này; guard vẫn fail-closed (0 tiền giảm). Đã ghi as-built SRS.
- 🟠 **Probe `403` chưa đo** — Host dev trả 404 cho `/api/identity/**`; nợ **`qa`** hạn `W24-5`, **không** hạ kỳ vọng `403`.
- 🟡 **`D-283`** — **4/9** metric `promotion_*` ở `ChuaCoNoiPhat` chưa có điểm phát: 3 metric voucher (`PRO-001`) +
  `promotion_applied_total` (`SAL-001`). Đóng khi đường ghi lượt voucher / tạo đơn có.
- 🟡 **`D-284`** — **chưa** nối `IIdempotencyGuard` cho `SaveTreeAsync`/`ImportRuleCostsAsync`; idempotent bằng khoá phân tán
  `Tree`/`Import` + delete-then-insert. Mở rộng allowlist ranh giới **phải** kèm `D-xx` mới.
- 🟡 **`D-291`/`D-292`** — TẠM CHỐT, nằm trong lô `D-277…D-292` **chờ người duyệt**; không tự đổi khi chưa duyệt.
- 🟡 **BR-10** (áp khuyến mãi qua một UoW `Redemption` + đơn + outbox) và **BR-19** (COD chưa đối soát ⇒ `0008`) **chưa phủ** —
  nợ `SAL-002`/`SHP-003`/`REC-003`; bổ sung khi các WP đó land.
- 🟡 **P3 còn** (REV vòng 2): `CheckoutAppService.Lines.UnitPrice` vẫn `ListPrice`; thiếu test tự động cho tenant/`Map`;
  cờ `KhongGhiSoGiaiTrinh` trên DTO công khai; `PromotionCandidateData.Status` mặc định `Active` (fail-open tiềm ẩn);
  DTO `/evaluate` chưa phơi `VoucherOutcome`.
- 🟡 **4 op v1 chưa mở** (`evaluate`/`validate-voucher`/`import-rule-costs`/`redemptions`) — nợ có chủ `SAL-002`/`WEB-*`.
- 🟡 **33 ràng buộc `Pro*`** trong `RangBuocChuaAnhXa.txt` (hàng rào cuối DB); có thể chuyển sang `ViPhamRangBuocDb` nếu cần
  mã lỗi riêng.
- 🟡 **FE:** `pnpm build` **EPERM symlink** và `pnpm lint` **no-script** là nợ môi trường cũ (`N70`/`K-05`), **không** sửa
  `next.config.ts`; `kiem-tat-ca` vẫn 0.
- **Chờ người:** duyệt lô `D-277…D-292`; merge `main` (biên bản người ký); kế toán duyệt lô M4 trước release.
