# Triển khai `PRC-001` — động cơ giá vốn bình quân gia quyền di động (WP-17)

> Phiên `N119`, hàng `W17-5`, chuỗi `gd3-1` (`D-216` phương án B), WP-17 "giá vốn", mã `PRC-001`,
> nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`), ngày 28/11/2026.
> Quyết định nguồn: `D-318` (uỷ quyền WP-17, C4/C6/C9), `D-320` (tạm duyệt G2 ARCH), `D-321` (ánh xạ loại
> chuyển động + VND), `D-322` (thiếu giá ⇒ `null`), `D-323` (job quét + command), `D-324` (mã lỗi giữ chỗ,
> `using eCommerce.Inventory`, sổ run 3 pha), **`D-325`** (replay toàn lịch sử từ `DauLichSu = 2000-01-01`),
> **`D-326`** (kỳ một ngày hợp lệ) — [decisions.md](../nhanh/decisions.md).
> Đây là WP **BE thuần** — **1 migration, 0 FE, 0 package, 0 biến môi trường mới, 0 đổi `.github`/compose/Dockerfile**.

## 1. Phạm vi

- **Nhánh/PR:** `feat/gd3-wp17-gia-von` → `tich-hop/gd3` theo **chuỗi `D-216` phương án B**: nhánh WP xếp chồng trên
  `tich-hop/gd3`, **không** mở PR mỗi phiên (PR xếp chồng mở ở cuối chuỗi). **Không** merge `main`.
- **Diff:** **69 tệp** so với `origin/tich-hop/gd3` (`5c902ed`). Trong đó sản phẩm BE: **17 tệp** `modules/Pricing/**` +
  **2 tệp** `modules/Inventory/**` + **5 tệp** `src/**` (permissions/EF/migration) + **2 tệp** OpenAPI/`schema.d.ts`; còn lại là
  test (**11 tệp**) + tài liệu + state chuỗi.
- **Hợp đồng API:** `/api/v1` **không đổi** (`snapshot.mjs` chỉ giữ `/api/v1` ⇒ **82 path trước = sau**). Hai app
  service mới **tự lộ** qua ConventionalControllers của ABP ở `/api/app/...` (không thuộc `/api/v1`):
  `/api/app/cost-price-admin` (ghi/duyệt giá vốn) và `/api/app/cost-engine/chay` (command chạy động cơ).
  `openapi.v1.json` **+106 dòng**, `schema.d.ts` **+35 dòng** (thuần additive; `pnpm gen:api` không đổi) — [handover-n117.md](../nhanh/handover-n117.md).
- **Migration:** **1** — `20261128052137_Added_Pricing_CostEngine` (**đã áp ở `N116`**, sau `pg_dump` 634674 bytes).
  Nội dung **expand-only**:
  - `CREATE TABLE "PrcCostEngineRuns"` (sổ run: `Id` PK · `TenantId` · `RunKey varchar(128)` · `FromDate`/`ToDate date` ·
    `Trigger varchar(64)` · `TrangThai int` · `Attempt int` · `StartedAtUtc`/`FinishedAtUtc` `timestamptz` ·
    `RowsUpserted`/`RowsSkippedApproved`/`RowsFlaggedMissing int` · `ErrorMessage varchar(2048)` + audit ABP).
  - `ALTER TABLE "PrcCostPrices"`: **4 `ADD COLUMN` nullable** — `NguonTinh integer`, `RunId uuid`,
    `ComputedAtUtc timestamptz`, `IdempotencyKey varchar(128)`.
  - `CREATE INDEX` `IX_PrcCostEngineRuns_TenantId_StartedAtUtc`; **2 unique index sửa tay `NULLS NOT DISTINCT`**:
    `IX_PrcCostEngineRuns_TenantId_RunKey` và `IX_PrcCostPrices_TenantId_IdempotencyKey` (filtered `WHERE IdempotencyKey IS NOT NULL`).
  - **0 `DROP`/`ALTER TYPE`/`SET NOT NULL` trên bảng cột có dữ liệu**; `Down()` chỉ drop phần mới thêm.
- **FE:** **0** màn hình mới (UI Pricing/giá vốn thuộc `WEB-001`; ARCH §1.1).
- **Package:** **0** (không sửa `Directory.Packages.props` / `eCommerce.slnx` / `pnpm-lock.yaml`).

### 1.1 Tệp sản phẩm thay đổi

| Nhóm | Tệp | Thay đổi (1 dòng) |
|---|---|---|
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/CostEngineRun.cs` | **Mới** — entity sổ run + máy trạng thái `BatDauLai`/`HoanTat`/`ThatBai` (NÉM khi sai nhánh) |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs` | Thêm 4 thuộc tính nullable + `GanKhoaIdempotency`/`DanhDauDongCo`/`CapNhatGiaVonChuaDuyet`; guard kỳ `<=`→`<` (`D-326`) |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/CostPriceSource.cs` | **Mới** — enum `ThuCong=0`/`DongCo=1` (thêm cuối dải) |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/TinhBinhQuanGiaQuyenDiDong.cs` | **Mới** — hàm **thuần** tính bình quân di động (không I/O, tiền qua `Money`) |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/PricingChinhSach.cs` | Thêm `PRC001SettingTenKhoa` + 4 hàm đọc thuần (`DocPhuongPhapGiaVon`, `DocBool`, …) |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/PricingErrorCodes.cs` | Thêm `0014`/`0015`; khai giữ chỗ `0012`/`0013`; thêm `PricingLockKeys.GiaVonTheoSku` |
| Domain | `modules/Pricing/eCommerce.Pricing.Domain/PricingSettingDefinitionProvider.cs` | Khai **4 Setting** động cơ + default (engine **TẮT**) |
| Application | `modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs` | **Mới** — orchestration 3 pha UoW, fail-closed `0014`, replay `[DauLichSu, to]`, đối soát |
| Application | `modules/Pricing/eCommerce.Pricing.Application/CostEngineWorker.cs` | **Mới** — `AsyncPeriodicBackgroundWorkerBase`, nhịp **30 phút**, `RunOnStart = true` |
| Application | `modules/Pricing/eCommerce.Pricing.Application/CostEngineAppService.cs` | **Mới** — endpoint command `POST /api/app/cost-engine/chay` (`[Authorize]` cấp class) |
| Application | `modules/Pricing/eCommerce.Pricing.Application/CostPriceAdminAppService.cs` | **Mới** — tạo (chưa duyệt) + duyệt 2 bước (`.Manage`/`.Approve`) |
| Application | `modules/Pricing/eCommerce.Pricing.Application/CostPriceLookupAdapter.cs` | **Mới** — hiện thực port đọc `ICostPriceLookup` (chỉ dòng `DaDuyet`, không endpoint) |
| Application | `modules/Pricing/eCommerce.Pricing.Application/PricingApplicationModule.cs` | Đăng ký worker + `DependsOn(AbpBackgroundWorkersModule)` |
| Application | `modules/Pricing/eCommerce.Pricing.Application/eCommerce.Pricing.Application.csproj` | Thêm `ProjectReference` → `Inventory.Application.Contracts` |
| Contracts | `modules/Pricing/eCommerce.Pricing.Application.Contracts/CostPriceContracts.cs` | **Mới** — DTO + `ICostPriceLookup` + `ICostPriceAdminAppService` + `ICostEngineAppService` |
| Contracts | `modules/Pricing/eCommerce.Pricing.Application.Contracts/PricingPermissions.cs` | Thêm `.CostPrice.Manage` / `.CostPrice.Approve` |
| Inventory | `modules/Inventory/eCommerce.Inventory.Application.Contracts/ICostFeedLookup.cs` | **Mới** — port đọc sổ cái + `CostMovementDto` (không `IApplicationService`) |
| Inventory | `modules/Inventory/eCommerce.Inventory.Application/CostFeedLookupAdapter.cs` | **Mới** — adapter đọc `StockLedger`/`StockDocumentLine.UnitPrice`, ánh xạ `D-321`, fail-closed `0014` |
| Persistence | `modules/Pricing/eCommerce.Pricing.EntityFrameworkCore/PricingDbContextModelCreatingExtensions.cs` | Mapping `PrcCostEngineRuns` + 4 cột/index |
| Persistence | `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs` | Thêm `DbSet<CostEngineRun>` |
| Persistence | `src/eCommerce.EntityFrameworkCore/Migrations/20261128052137_Added_Pricing_CostEngine.cs` (+`.Designer.cs`, `eCommerceDbContextModelSnapshot.cs`) | Migration + snapshot |
| Permissions | `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs` | Khai 2 permission con của `Pricing` |
| OpenAPI | `packages/api-client/openapi.v1.json` (+106) · `packages/api-client/src/schema.d.ts` (+35) | 2 app service mới, thuần additive |

**Test (11 tệp, không phải sản phẩm):** `test/eCommerce.Tests/Pricing/{CostEngineK06Tests, CostEngineKienTrucTests,
CostEngineServiceK06Tests, CostPriceAdminAppServiceTests, CostPriceLookupK06Tests, CostPriceTests,
LoaiDongGiaVonKhopInventoryTests}.cs` · `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt` ·
`test/eCommerce.IntegrationTests/{PostgreSqlPricingCostEngineTests, PostgreSqlSchemaTests}.cs` ·
`test/eCommerce.IntegrationTests/Assets/schema.sql`. Chi tiết phủ BR + 16 ca K-06: [PRC-001-testplan.md](../tests/PRC-001-testplan.md).

### 1.2 Hành vi mới cần báo bên gọi

1. **Động cơ TẮT mặc định** — `Pricing:GiaVonDongCo = false`. Worker vẫn được đăng ký và chạy mỗi 30 phút nhưng
   `CostEngineService.ChayAsync` **trả `null`** ngay (log `[PRC001-COST-JOB-TAT]`); **0 dòng** `PrcCostPrices` mới.
2. **Endpoint admin giá vốn** — `/api/app/cost-price-admin` (tạo/duyệt) và endpoint đọc `/api/app/cost-price`
   (`ICostPriceAppService`, `.CostPrice.View`) đã có từ `S12`; cả hai đều `[Authorize]` (401 chưa đăng nhập,
   403 thiếu quyền). `/api/app/cost-engine/chay` cần `.CostPrice.Manage`.
3. **Mã lỗi mới** — `eCommerce:Pricing:0014` (`CauHinhGiaVonKhongHopLe`) và `0015` (`GiaVonKhongTheTinhLai`).
   `0012`/`0013` **vẫn giữ chỗ cho `PRC-002`** (`D-324` ①), chưa tiêu thụ. Đường admin tái dùng `0006`
   (thiếu `IdempotencyKey`) và `0009` (dữ liệu giá vốn sai/duyệt lặp).
4. **Fail-closed `0014`** — động cơ **NÉM** (không rơi về mặc định) khi: `Pricing:PhuongPhapGiaVon` ≠
   `BinhQuanGiaQuyenDiDong`; `Pricing:GiaVonTheoKho = true` (v1 chỉ SKU toàn hệ thống); giá trị bool không hợp lệ;
   loại chuyển động kho lạ/lệch dấu ở adapter; `TienTeNhap` ≠ VND. **`0015`** khi `ChoNhapLuiNgay = true` mà tính
   lại **đụng dòng đã duyệt** và giá trị sẽ đổi (không ghi đè).
5. **Thiếu giá vốn ⇒ `null` + `CostMissing`, CẤM `0`** — `ICostPriceLookup` chỉ trả dòng `DaDuyet`; không có dòng
   hiệu lực ⇒ `AmountValue = null`. Giá vốn `0` **hợp lệ** (`CoGiaVon = true`).
6. **Kỳ một ngày hợp lệ** (`D-326`) — `EffectiveTo == EffectiveFrom` được ghi; kỳ **ngược** (`EffectiveTo < EffectiveFrom`)
   vẫn NÉM `0009`.

## 2. Biến môi trường / Setting

| Tên | Mặc định | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

**Không thêm biến môi trường.** Bốn chính sách là **ABP Setting** lưu trong bảng `AbpSettings` (đã tồn tại), **định nghĩa
trong code** (`PricingSettingDefinitionProvider`) — **không** phải env/`.env`/secret store. Đổi giá trị sau triển khai
bằng endpoint `setting-management/*` của ABP, **không cần deploy lại**.

### 2.1 Bốn Setting động cơ (nguồn: `PricingSettingDefinitionProvider.cs:41-47`, `PricingChinhSach.cs`)

| Khoá | Mặc định | Giá trị hợp lệ | Phạm vi | Quyết định |
|---|---|---|---|---|
| `Pricing:PhuongPhapGiaVon` | `BinhQuanGiaQuyenDiDong` | **chỉ** `BinhQuanGiaQuyenDiDong` (khác ⇒ `0014`) | `Application \| Tenant` | `D-318` C9 / `D-177` ② |
| `Pricing:GiaVonTheoKho` | `false` | `false` (v1; `true` ⇒ `0014`) | `Application \| Tenant` | `D-318` C9 |
| `Pricing:ChoNhapLuiNgay` | `false` | `true` · `false` | `Application \| Tenant` | `D-325` / ARCH §5.4 |
| `Pricing:GiaVonDongCo` | **`false` (TẮT)** | `true` · `false` | `Application \| Tenant` | `D-318` C6 / `D-323` / `D-324` |

- **Mặc định production = mặc định trong code** nên **không cần thao tác khởi tạo**.
- 🚨 **Thứ tự kiểm tra:** `GiaVonTheoKho = true` **luôn NÉM `0014`** kể cả khi `GiaVonDongCo = false`
  (`CostEngineService.cs:132-135` đứng **trước** nhánh tắt động cơ `:144`). Đây là điểm kiểm sau triển khai §5.2.
- ⚠️ **Bật động cơ là việc NGƯỜI:** đặt `Pricing:GiaVonDongCo = true` **sau** khi backfill/duyệt lô M4 và phê duyệt
  của kế toán (`D-318` C6). OPS **không** tự bật trong triển khai này.
- ⚠️ **R6 chưa chứng minh trên Host thật:** provider Setting nằm trong assembly **module** (`Pricing.Domain`) — build/test
  **không** chứng minh ABP quét được lúc chạy. Nếu không quét: 4 khoá **không tồn tại** ⇒ cấu hình bị bỏ qua im lặng
  (mặc định vẫn an toàn: engine tắt). Đây là **bước kiểm sau triển khai bắt buộc** (§5.2). Đường lùi kỹ thuật: chuyển
  provider sang `src/eCommerce.Domain/Settings/` (chủ: QA/ARCH, không phải OPS).

## 3. Thứ tự triển khai

1. **Merge code BE trước** (nhánh → `tich-hop/gd3`): binary mới chứa `CostEngineWorker`, `CostEngineService`, migration,
   4 Setting, 2 permission, adapter Inventory. Commit F3 BE: **`54ab64b`** (`feat(pricing): N116 - W17-2 F3 BE …`);
   vá P1/kỳ-một-ngày sau đó: **`ac4af17`** (`fix(pricing): N118 - W17-4 fix P1 full-history replay and one-day period`).
2. **Không có FE** — không deploy `apps/admin-web`.
3. **`DbMigrator` bằng binary MỚI build** (M5): chạy `dotnet run --project src/eCommerce.DbMigrator` rồi **xác nhận
   `__EFMigrationsHistory` có `20261128052137_Added_Pricing_CostEngine`** (kiểm `\d "PrcCostPrices"` có 4 cột nullable;
   `pg_indexes` có 2 index `NULLS NOT DISTINCT`). **Cấm** `dotnet ef database update` (M6 / `D-75`).
   Trên DB dev hiện tại migration **đã áp ở `N116`**; bước này để bảo đảm mọi môi trường mới có đúng schema.
4. **Khởi động lại Host** để đăng ký `CostEngineWorker` (`PricingApplicationModule.OnPostApplicationInitializationAsync`
   → `AddBackgroundWorkerAsync<CostEngineWorker>()`; Host `DependsOn` module tại `eCommerceHttpApiHostModule.cs:76`).
   Engine **TẮT mặc định** ⇒ lượt quét no-op, **0 dòng mới**.
5. **Backfill lịch sử giá vốn:** **không có script backfill riêng.** Khi người vận hành bật `Pricing:GiaVonDongCo = true`,
   worker (nhịp 30 phút, `RunOnStart = true`) hoặc command `POST /api/app/cost-engine/chay` sẽ **replay toàn lịch sử**
   `[DauLichSu = 2000-01-01, to]` (`D-325`), ghi/cập nhật các kỳ **chưa duyệt** (`NguonTinh = DongCo`, `DaDuyet = false`),
   chờ kế toán duyệt. Kỳ đã duyệt **không** bị ghi đè. **OPS không tự bật/bổ sung dữ liệu thật** (`D-318` C6).
6. **Không bật job cấu hình ngoài** — không đổi `appsettings`, không thêm secret.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| **Toàn bộ hành vi động cơ** (0 dòng mới, dữ liệu cũ nguyên vẹn) | Đặt/giữ `Pricing:GiaVonDongCo = false` (mặc định) ⇒ worker no-op, endpoint trả `null` | **Rẻ nhất, không deploy.** Chỉ là cấu hình ABP Setting (bảng `AbpSettings`). |
| **Chỉ job nền chạy sai** (worker treo/lỗi nhịp) | Gỡ dòng `AddBackgroundWorkerAsync<CostEngineWorker>()` (`PricingApplicationModule.cs:29`) rồi deploy lại | Endpoint command vẫn còn (chạy tay). Ưu tiên dùng (a) nếu chỉ do engine. |
| **Revert code** | `git revert ac4af17` (P1/kỳ-một-ngày) rồi `git revert 54ab64b` (F3 BE), theo thứ tự **ngược thời gian** | Commit F3 BE **`54ab64b`**; commit vá **`ac4af17`**. ⚠️ `54ab64b` **có chứa tệp migration** — DB đã áp ở `N116` ⇒ **KHÔNG** chạy `dotnet ef migrations remove` trên DB thật; để schema expand-only nằm nguyên (bảng/cột không dùng). Snapshot quay về trước WP ⇒ phối hợp **BE** trước lần `migrations add` kế tiếp. |
| **Migration (schema)** | **Không revert migration** trên DB có dữ liệu. Giữ bảng `PrcCostEngineRuns` (rỗng) + 4 cột nullable (NULL) vô hại | `Down()` chỉ dùng khi **chưa** áp, hoặc sau `pg_dump` + người duyệt (xoá bảng/cột = phá huỷ). |
| **Dữ liệu** | `pg_dump` **đã có** ở `N116`: `artifacts/pg-dump/N116-W17-2.dump` (**634 674 bytes**, chưa commit, `artifacts/` gitignored) | Engine TẮT ⇒ **0 `INSERT`/`UPDATE`/`DELETE`** trên `PrcCostPrices`; không cần `pg_restore`. Nếu **bật** engine rồi muốn lùi dữ liệu ⇒ **`pg_dump` MỚI trước khi bật** (M9) rồi `pg_restore`. |

- **Không tắt bước CI nào để xanh.** Ưu tiên đường lùi **cấu hình** (dòng 1) trước `git revert`.
- **Không sửa migration đã merge** (M4); thay đổi tiếp theo = migration mới.

## 5. Cổng (số đo tham chiếu)

- **G6 — việc của ORCH, sau khi tài liệu này xong:** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` ⇒ **số bước + thời gian
  do ORCH ghi vào handover `N119` và `docs/nhanh/07-ke-hoach-trien-khai.md` §13**. OPS **KHÔNG** chạy `-DayDu` trong
  phiên này (máy dev từng **hết bộ nhớ** — `N69`; không chạy song song build/test nặng).
- **Số đo tham chiếu (phiên `N118`, không phải phiên này):** `pwsh -File tools/kiem-tat-ca.ps1` (nhẹ) → **exit 0 ·
  10/10 · 340,3 s**; `dotnet test test/eCommerce.Tests --no-build` → **2691/2691**; `dotnet test
  test/eCommerce.IntegrationTests --no-build` → **171/171**; `-DayDu` **chưa chạy** ở `N118` — [handover-n118.md](../nhanh/handover-n118.md) §1.

### 5.1 PR / CI

- PR hiện đúng **2 required check**; không bước nào bị `continue-on-error` để che lỗi.
- WP **không** chạm `.github/**`, `docker-compose.yml`, `Dockerfile` ⇒ **không** phát sinh bước CI mới.

### 5.2 Kiểm sau triển khai

1. `GET /health-status` → **200 Healthy**. (Đo từ mã: Host map `/health-status` tại
   `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:619` — **không** phải `/api/health`; `/api/health` chỉ là
   BFF của `apps/admin-web`, ngoài phạm vi WP này.)
2. **R6 bắt buộc:** xác nhận 4 khoá §2.1 **tồn tại và đọc được** ở **cả host (global) và một tenant** (qua
   `setting-management` hoặc log động cơ khi chạy command). Nếu không đọc được ⇒ dừng, xem §6 (R6). Với engine **TẮT**,
   việc này là no-op an toàn — không ghi dữ liệu.
3. **Đường đọc giá vốn:** `GET /api/app/cost-price` với quyền `.CostPrice.View` → **200** (danh sách rỗng/không đổi).
   Gọi không token → **401**; thiếu quyền → **403**.
4. **Đường ghi/duyệt (không seed dữ liệu thật):** xác nhận `/api/app/cost-price-admin` và
   `/api/app/cost-price-admin/{id}/approve` trả **401/403** đúng khi thiếu quyền; **không** tạo dòng thật trên production.
5. **Động cơ TẮT:** `POST /api/app/cost-engine/chay` (quyền `.CostPrice.Manage`, môi trường **non-prod**) → trả **`null`**
   (không có run); log worker có `[PRC001-COST-JOB-TAT]`.
6. **Fail-closed `0014` (non-prod):** đặt `Pricing:GiaVonTheoKho = true` rồi gọi `/api/app/cost-engine/chay` ⇒
   `BusinessException` **`eCommerce:Pricing:0014`** (kể cả khi engine tắt). Đặt lại `false` ngay sau kiểm.
7. **Không seed dữ liệu thật**; `PrcCostPrices` **0 dòng mới** khi engine tắt.

## 6. Khi hỏng — ai gọi

| Hiện tượng | Gọi ai | Ghi chú |
|---|---|---|
| Lỗi động cơ / mã `0014`/`0015` sai nghĩa, fail-closed chặn oan | **BE / arch** | Đối chiếu [PRC-001-arch.md](../features/pricing/PRC-001-arch.md) §5.2/§5.4 và [PRC-001-review.md](../reviews/PRC-001-review.md). Không phải OPS. |
| Setting không đọc được ở host/tenant (đổi giá trị mà hệ thống chạy mặc định) | **QA / ARCH** (R6) | Cần khởi động Host thật; đường lùi kỹ thuật = chuyển provider sang `src/eCommerce.Domain/Settings/`. |
| Migration lỗi / `__EFMigrationsHistory` thiếu / index sai | **OPS + `pg_dump`** | Không `database update` tay; dùng `DbMigrator` binary mới; `pg_dump` trước khi chạm dữ liệu. |
| Worker chạy sai nhịp / treo / run kẹt `DangChay` | **OPS** | Nhịp **30 phút hardcode** (`CostEngineWorker.NhipMacDinhPhut`), đổi cần deploy code. Process chết giữa 3 pha UoW ⇒ run kẹt `DangChay` ⇒ `409` tới khi reset; **chưa có lệnh reset** (nợ `W17-4/W17-5`) — gọi **BE**. |
| Worker chỉ xử lý một ngữ cảnh tenant | **BE** | Worker **không** lặp tenant (không có vòng `Using(tenant)`); chạy ở scope host (`ICurrentTenant` hiện tại). Đúng cho mô hình **1 tenant** hiện nay; multi-tenant thật ⇒ cần command theo tenant hoặc bổ sung vòng lặp (**D-xx** mới). |
| Muốn **bật engine / backfill giá vốn thật** | **Người** (`M4`/kế toán) | Ngoài phạm vi OPS — `D-318` C6; backfill giá vốn thật cho SKU có tồn > 0 luôn chuyển người. |

> Ngoài phạm vi WP-17: màn vận hành động cơ là `WEB-001`; nếu cần 2 endpoint giá vốn qua `/api/v1` thì **BE** phải thêm
> controller v1 viết tay (tách WP). P2/P3 còn lại của review (K06-08b mô phỏng SQL, `IClock`, câu chữ SRS Transfer) là nợ
> **BE/BA**, không chặn triển khai.
