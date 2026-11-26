# Triển khai `REP-001` — read-model vận hành báo cáo (rebuild · chốt kỳ · đối chiếu)

> Phiên `N92` (hàng `W26-5`, bước **F6** đóng `WP-26`) · nhánh `feat/gd3-wp26-read-model` → `tich-hop/gd3` · 27/11/2026.
> Quyết định liên quan: `D-47`, `D-48`, `D-114`, `D-171`, `D-172`, `D-173`, `D-174`, `D-208`, `D-216`, `D-270`…`D-276`.
> Tài liệu này chỉ mô tả **vận hành**; thiết kế ở `docs/features/reporting/REP-001-arch.md`, đặc tả ở
> `docs/features/reporting/rep-001-read-model-job-tong-hop.md` (SRS `v0.4`), review `docs/reviews/REP-001-review.md`,
> testplan `docs/tests/REP-001-testplan.md`.

## 1. Phạm vi

- **Nhánh/PR:** `feat/gd3-wp26-read-model` → `tich-hop/gd3` (chuỗi GĐ3, `D-216` phương án B). `tich-hop/gd3` **chỉ nhận**
  khi G6 (`-DayDu`) xanh. Production nhận qua PR tích hợp `tich-hop/gd3` → `main` — việc merge `main` là **vùng cấm** của
  agent, cần **biên bản người ký**. Agent **không** mở PR cho nhánh WP (`D-216` ③).
- **Module:** `Reporting` (`modules/Reporting/**`, **4** project `Domain` · `Application.Contracts` · `Application` ·
  `EntityFrameworkCore`) — **đã có từ `WP-08`**. `WP-26` **không** thêm project, **không** thêm tham chiếu module.
- **3 bảng mới** (module `Reporting`): `RepRunLogs` (nhật ký + checkpoint rebuild) · `RepConsistencyChecks` (lộ lệch
  BR-012) · `RepPeriodCloses` (chốt kỳ, `D-171`).
- **1 migration expand-only:** `src/eCommerce.EntityFrameworkCore/Migrations/20261126181842_Wp26_VanHanh_BaoCao.cs`
  - Tạo **3 bảng** trên; `Down()` **chỉ** `DropTable` 3 bảng mới. **Không** `ALTER`/`DROP` bảng cũ.
  - **4 index thường:** `IX_RepConsistencyChecks_TenantId_ChuaXuLy` · `IX_RepConsistencyChecks_TenantId_Ngay` ·
    `IX_RepPeriodCloses_TenantId_TrangThai` · `IX_RepRunLogs_TenantId_ThoiDiem`.
  - **4 unique index viết tay có `NULLS NOT DISTINCT`** (EF không sinh được NND + partial):
    `IX_RepRunLogs_TenantId_KhoaIdempotency` (partial `WHERE "RequestedBy" IS NOT NULL`) ·
    `UX_RepRunLogs_TenantId_Job_Projection_DangChay` (partial `WHERE "Status" IN (0,1)`) ·
    `UX_RepConsistencyChecks_DangMo` (partial `WHERE "ResolvedAtUtc" IS NULL`) ·
    `IX_RepPeriodCloses_TenantId_KhoaIdempotency` (đầy đủ).
  - `CREATE EXTENSION IF NOT EXISTS btree_gist` + constraint `EX_RepPeriodCloses_KyDaChot_KhongChong`
    `EXCLUDE USING gist` — **chỉ** chặn hai kỳ `DaChot` chồng nhau (`D-171`).
- **Hợp đồng API:** **10** op mới thêm vào `ReportsController` hiện có (`src/eCommerce.HttpApi/Controllers/ReportsController.cs`),
  route gốc `/api/v1/reports`, `[Authorize(eCommerceConsts.Permissions.Reports.Default)]` **cấp class** (ẩn danh ⇒ **401**);
  method **ghi** thêm quyền hẹp (**AND-combination**). Bảng op → quyền ở §5.2.
- **Không** đổi `eCommerce.slnx` · `Directory.Packages.props` · `pnpm-lock.yaml`. **Không** có secret mới.
- **FE = N/A** (`D-276`: 0 dòng FE). Không có deploy `apps/admin-web` trong WP này.

## 2. Biến môi trường / cấu hình

Cấu hình đọc qua section **`Reporting`** (`ReportingApplicationModule.cs:27`
`Configure<ReportingOptions>(… GetSection("Reporting"))`; lớp
`modules/Reporting/eCommerce.Reporting.Application/ReportingOptions.cs`) — **KHÔNG** phải `SettingDefinition`.
Mọi khoá có **mặc định đúng** trong code ⇒ chạy được khi không cấu hình.

| Tên | Kiểu | Mặc định | Nơi đọc | Ghi chú |
|---|---|---|---|---|
| `Reporting:GioLechUtc` | int | `7` (`NgayKinhDoanh.GioLechMacDinh`) | `ReportAppService` · `ReportAdminAppService` · `TongHopBaoCaoService` · `ReportingAggregationWorker` | Giờ lệch **ngày kinh doanh** UTC+7. 🚨 Đổi **sau khi có dữ liệu** ⇒ ngày đã tổng hợp và ngày kinh doanh lệch nhau, phải backfill khoảng bị ảnh hưởng. |
| `Reporting:NhipJobPhut` | int | `15` | `ReportingAggregationWorker.DoWorkAsync` | Nhịp **job tổng hợp định kỳ** (mặc định `NhipMacDinhPhut = 15` nếu `<= 0`). ⚠️ **Chỉ** áp cho job Aggregate — **KHÔNG** đổi nhịp `ReportRebuildWorker` (xem dòng dưới). Nợ `D-172`/`D-273`. |
| `Reporting:NguongSapHet` | decimal | `5` | `ReportAppService` (`:182,235`) | Ngưỡng "sắp hết" theo khả dụng cho Dashboard + báo cáo tồn kho. |
| `Reporting:SoMatHangSapHetToiDa` | int | `20` | `ReportAppService` (`:201`) | Trần số mặt hàng "sắp hết" trả về. |
| — (không có khoá) | — | `60` giây, **hard-code** | `ReportRebuildWorker.NhipMacDinhGiay = 60` (`ReportRebuildWorker.cs:29,34`) | Nhịp worker xử lý hàng đợi rebuild **không** cấu hình được — đổi phải sửa code. |
| `Reporting:EnableRebuildWorker` / `Reporting:EnablePeriodClose` | — | **KHÔNG TỒN TẠI trong code** | — | ⚠️ `REP-001-arch.md` §9.3(b) **đề xuất** 2 cờ này, nhưng `ReportingOptions`/`appsettings`/worker **không** đọc cờ nào. **Không có công tắc bật/tắt bằng config** cho rebuild/chốt kỳ — đường lùi phải **revert code** (§4). Ghi nợ §7. |

- **Không** có secret mới. Cả 4 khoá là cấu hình thường; đặt qua `.env`/`appsettings` của Host, **không** commit giá trị
  production vào repo (O1).
- File mẫu hiện tại `src/eCommerce.HttpApi.Host/appsettings.json:32-37` có đúng 4 khoá trên.

## 3. Thứ tự triển khai

1. **Backup DB production trước khi chạm schema** (K-01/M9): `pg_dump -Fc` ra tệp **ngoài repo**
   (ví dụ `artifacts/pg-dump/<PROD>-rep001.dump`), ghi đường dẫn + kích thước tệp vào biên bản triển khai.
   (DB dev đã dump ở `N89`: `artifacts/pg-dump/N89-W26-2.dump` = **497.829 byte**.)
2. **Merge code BE trước** (`feat/gd3-wp26-read-model` → `tich-hop/gd3` → `main`). WP này **không** đổi `eCommerce.slnx` /
   `Directory.Packages.props` / `pnpm-lock.yaml` ⇒ không vướng `K-10`.
3. **Build binary mới:** `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng **0 error / 9 warning**, 0 warning mới).
4. **Áp migration bằng binary MỚI qua `DbMigrator`** (M5) — **KHÔNG** `dotnet ef database update` (M6, `D-75`):
   `dotnet run --project src/eCommerce.DbMigrator`. Bước này cũng chạy provider permission (cấp `eCommerce.Reports.*` cho role `admin`).
5. **Kiểm DB THẬT** (không tin `exit=0`) — chạy SQL ở §5.3.
6. **Deploy BE (Host/API)** sau khi DB xanh. Worker `ReportRebuildWorker` tự đăng ký khi app khởi động
   (`ReportingApplicationModule.cs:39`); không cần bật tay.
7. **FE:** N/A (`D-276`).
8. **Kiểm sau triển khai** (§5.2) rồi mới coi là xong.
9. **G6** (`pwsh -File tools/kiem-tat-ca.ps1 -DayDu`) do **ORCH** đo ở phiên này; số đo chốt ghi ở handover N92 (docs/nhanh/handover-n92.md, chưa tồn tại khi soạn tài liệu này).
   `ops` **không** tự chạy `-DayDu` (nợ bộ nhớ `N69`).

## 4. Đường lùi (bắt buộc)

| Thay đổi / sự cố | Đường lùi | Ghi chú |
|---|---|---|
| **Rebuild ghi số liệu sai / worker lỗi liên tục** | ⚠️ **Không có cờ tắt** (§2). Bỏ dòng đăng ký worker `await context.AddBackgroundWorkerAsync<ReportRebuildWorker>();` (`ReportingApplicationModule.cs:39`) rồi deploy lại, **hoặc** `git revert` commit WP-26 (dòng dưới). Worker không chạy ⇒ hàng đợi `Queued` đứng yên ⇒ **0** ghi read-model. | Sửa số đúng bằng `POST /api/v1/reports/rebuild` (thay thế theo `(ngày, nguồn)`, idempotent — không cộng dồn). |
| **Ghi vào kỳ ĐÃ CHỐT (`0011`)** | Cổng **fail-closed** `TongHopBaoCaoService.ChanNeuGiaoKyDaChotAsync(ngay)` kiểm `Status == DaChot && FromDate <= ngay && ToDate >= ngay` **TRƯỚC `DELETE`** trên **3** đường ghi (`ChayAsync`, `ChayKhoangAsync`, `ChupAnhTonKhoAsync`) ⇒ UoW ngày **rollback**, **0 dòng thay đổi**, run `PartiallySucceeded`/`Failed` có `LastErrorCode = 0011`. | Muốn ghi hợp lệ: **mở lại kỳ** (`POST /period-close/{id}/reopen`, `Reason` ≥ 20) → rebuild → chốt lại. **Không** gỡ cổng. |
| **Cấu hình sai (`GioLechUtc`/`NhipJobPhut`/ngưỡng)** | Sửa `Reporting:*` trong `.env`/`appsettings` rồi restart Host. **0** code, **0** migration, **0** dữ liệu. | `NhipJobPhut <= 0` tự rơi về `15`. Nhịp **rebuild** hard-code 60 s — đổi phải sửa code. Đổi `GioLechUtc` sau khi có dữ liệu ⇒ phải backfill. |
| **Cần bỏ hẳn mã `REP-001`** | `git revert` **đúng** các commit WP-26, **thứ tự mới → cũ**: `57cf660` (review vòng 2) · `4a964e6` (vá P1 cổng `0011`) · `608915d` (test F4) · `7b82b53` (SRS v0.4 + `D-275`) · `0718f13` (`D-275` Aggregate ghi RepRunLog) · `681c289` (BE + 3 bảng + service + worker + migration). Các commit bằng chứng `7f6c8f4`/`a0ade44` chỉ là test negative control, bỏ qua được. | `4a964e6` là bản vá P1 nằm **trên** code WP-26 ⇒ phải revert **trước** `681c289`. **KHÔNG** revert riêng migration đã áp (dòng dưới). |
| **Schema 3 bảng `Rep*`** | **KHÔNG** chạy `Down`/`DROP`/xoá bảng ở production. Để 3 bảng lại. | Migration **expand-only**; 3 bảng chỉ chứa nhật ký/kỳ chốt/độ lệch của module — **không** phải nguồn sự thật (nguồn ở bảng giao dịch), read-model rebuild lại được ⇒ **0** dữ liệu nghiệp vụ bị mất. |
| **Revert tệp migration khỏi assembly** trong khi DB **đã** áp | Dòng `__EFMigrationsHistory` (`20261126181842_Wp26_VanHanh_BaoCao`) thừa **vô hại** — EF bỏ qua migration không còn trong assembly. **Không** tự xoá dòng lịch sử. | Chạy lại `dotnet run --project src/eCommerce.DbMigrator` để xác nhận **không** áp/drop gì và `exit=0`. |
| **Lùi toàn bộ** (mất dữ liệu phát sinh sau dump) | `pg_restore -d <DB> artifacts/pg-dump/<PROD>-rep001.dump` — **chỉ** khi các đường lùi cấp trên không đủ. | Ưu tiên (a) revert code → (b) để bảng lại → (c) restore dump. |

- 🚨 **Mất `RepRunLog`/`RepPeriodClose` = mất audit chốt kỳ** (không phải dữ liệu báo cáo). Ghi rõ trong biên bản rollback.
- **`btree_gist` KHÔNG được drop** khi lùi — module `Tax` đã dùng extension này (`20261013012648_Add_Tax_Module.cs`).
- Sau revert, kiểm Host khởi động được và `GET /health-status` trả **200** trước khi coi là hoàn tất.

## 5. Cổng (số đo thật)

### 5.1. Cổng đã đo trong WP-26

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (`N91`, sau vá P1) | `exit 0` · **0 error / 9 warning** (0 warning mới; CS8604 có sẵn) — `docs/nhanh/handover-n91.md` §1. |
| `dotnet test test/eCommerce.Tests` (`N91`) | `exit 0` · **2376/2376** (+34 so baseline `N88` 2342; không xoá/skip ca nào). |
| `dotnet test test/eCommerce.IntegrationTests` (`N91`, Docker) | `exit 0` · **139/139** (+17 `PostgreSqlWp26ReadModelNndTests` và ca `EXCLUDE`). |
| `tools/agents/check-feature-doc.ps1` (SRS) | **DAT** · 16/16 heading. |
| `tools/agents/check-agent-artifact.ps1 -Kind testplan` · `docs/tests/REP-001-testplan.md` | `exit 0` · **DAT** · BR trong SRS 17 · thiếu 0 · phủ **14/15** (`BR-008` chưa kiểm được, §7). |
| `tools/agents/check-agent-artifact.ps1 -Kind review` · `docs/reviews/REP-001-review.md` | `exit 0` · **DAT** · **`VERDICT: PASS`** · **P1 = 0** (P2 = 0, P3 = 4). |
| Negative control `NC-1`…`NC-4` (`tools/agents/negative-control.ps1`, ORCH) | **4 đỏ → xanh**; `NC-4` khoá **cổng `0011` ở đường ghi** (`TongHopBaoCaoService.cs:467`, `Status ==` → `!=`) đỏ (exit 1) → khôi phục xanh (exit 0). Bằng chứng `20261127-034726-*`. |
| `pwsh -File tools/kiem-tat-ca.ps1` (G3) | `exit 0` · **10/10 bước ĐẠT · 564,6 s**. |
| ├ build / unit / integration | `0 error · 9 warning` · **2376** · **139** · 118,9 s / 43,2 s / 47,7 s. |
| ├ mermaid / cổng tài liệu | 74,9 s · 3,6 s (ĐẠT). |
| ├ FE typecheck / Vitest / `gen:api` | 0 lỗi · **45/45** · `schema.d.ts` **KHÔNG đổi** (59/59 path). |
| └ DB / test cổng | **25/25** · **60/60** · 11,5 s / 210,9 s. |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (`N91`) | `exit 0` · `PASS` · 964 link · 1692 backtick · 0 chết. |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (G6) | **ORCH đo ở cuối phiên `N92`; số đo chốt ghi ở handover N92 (docs/nhanh/handover-n92.md).** Agent `ops` **không** tự chạy (nợ bộ nhớ `N69`). |
| `dotnet run --project src/eCommerce.DbMigrator` + `psql` kiểm (DB dev `N89`) | `exit 0` · migration top = `20261126181842_Wp26_VanHanh_BaoCao` · 3 bảng · `EX_…` (`contype='x'`) · 4 index NND. |
| `pg_dump -Fc` trước khi áp (DB dev `N89`) | `artifacts/pg-dump/N89-W26-2.dump` = **497.829 byte**. |

### 5.2. Kiểm sau triển khai (thủ công, trên production)

| Kiểm | Kỳ vọng |
|---|---|
| `GET /health-status` (Host) · `GET /api/health` (BFF `apps/admin-web`) | **`200`** |
| **10** route mới `/api/v1/reports/**` **không** Bearer token | **`401`** cho cả 10 |
| Gọi có token **đủ** quyền | `200` (mọi op mới; `POST /rebuild` trả `200` — đã sửa từ `202` theo review P3 #5) |
| Gọi có token **thiếu** quyền hẹp trên op ghi | **`403`** (AND-combination: cần **cả** `eCommerce.Reports` **và** quyền hẹp) |
| Worker rảnh (không có hàng đợi) | `SELECT count(*) FROM "RepRunLogs" WHERE "Status" IN (0,1);` → **0**; log không lỗi mỗi nhịp |
| Rebuild thử 1 khoảng nhỏ (admin) | `POST /rebuild` → `Status = Queued` → (≤ ~60 s) `Running` → `Succeeded`; `RowsWritten > 0`; `CheckpointDate = ToDate` |
| Chốt kỳ khi còn dòng lệch mở | `POST /period-close` → **`409`** `eCommerce:Reports:0014` |
| Chốt kỳ hợp lệ rồi rebuild chồng | `POST /rebuild` phủ ngày đã chốt → **`409`** `eCommerce:Reports:0011` |
| Lỡ chốt 2 kỳ chồng nhau | **`409`** `eCommerce:Reports:0012` (`23P01`) |

Bảng 10 op mới → quyền (nguồn: `ReportsController.cs:74-132`; class = `eCommerce.Reports`):

| # | Method · route | Quyền (method thêm trên class) |
|---|---|---|
| 1 | `POST /api/v1/reports/rebuild` | `eCommerce.Reports.Rebuild` |
| 2 | `POST /api/v1/reports/rebuild/{runId}/resume` | `eCommerce.Reports.Rebuild` |
| 3 | `GET /api/v1/reports/rebuild/{runId}` | (class) `eCommerce.Reports` |
| 4 | `GET /api/v1/reports/rebuilds` | (class) `eCommerce.Reports` |
| 5 | `POST /api/v1/reports/period-close` | `eCommerce.Reports.PeriodClose` |
| 6 | `POST /api/v1/reports/period-close/{id}/reopen` | `eCommerce.Reports.PeriodReopen` |
| 7 | `GET /api/v1/reports/period-close/{id}` | (class) `eCommerce.Reports` |
| 8 | `GET /api/v1/reports/period-closes` | (class) `eCommerce.Reports` |
| 9 | `POST /api/v1/reports/reconciliation` | `eCommerce.Reports.Reconcile` |
| 10 | `GET /api/v1/reports/consistency-checks` | (class) `eCommerce.Reports` |

- **4 permission hẹp** khai **2 nơi** (`eCommerceConsts.Permissions.Reports` + `eCommercePermissionDefinitionProvider`,
  `D-270`) và đều là **con** của `Reports.Default`. Thiếu một nơi ⇒ **403 cho mọi người kể cả admin**. Chạy `DbMigrator`
  (binary mới) để role `admin` được cấp.
- **Map mã lỗi → HTTP** đã thêm `0010–0017` ở `eCommerceHttpApiHostModule.cs:415-422` (thiếu map ⇒ ABP trả `403`).

### 5.3. Kiểm DB thật (chạy tay, không tin `exit=0`)

```sql
-- (1) Migration history: đúng 1 dòng
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%Wp26_VanHanh_BaoCao%';
-- kỳ vọng: 20261126181842_Wp26_VanHanh_BaoCao

-- (2) Đủ 3 bảng
SELECT tablename FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('RepConsistencyChecks','RepPeriodCloses','RepRunLogs');
-- kỳ vọng: 3 dòng

-- (3) Đủ 4 unique index có "NULLS NOT DISTINCT" (M7 — thiếu cụm này = ràng buộc mất tác dụng im lặng)
SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'IX_RepRunLogs_TenantId_KhoaIdempotency',
    'UX_RepRunLogs_TenantId_Job_Projection_DangChay',
    'UX_RepConsistencyChecks_DangMo',
    'IX_RepPeriodCloses_TenantId_KhoaIdempotency')
  AND indexdef LIKE '%NULLS NOT DISTINCT%';
-- kỳ vọng: 4 dòng

-- (4) btree_gist + constraint EXCLUDE tồn tại
SELECT extname FROM pg_extension WHERE extname = 'btree_gist';                 -- 1 dòng
SELECT conname, contype FROM pg_constraint
WHERE conrelid = '"RepPeriodCloses"'::regclass AND contype = 'x';
-- kỳ vọng: EX_RepPeriodCloses_KyDaChot_KhongChong | x
```

**Kiểm nhanh 1 ca `EXCLUDE` chặn 2 kỳ `DaChot` chồng nhau** — chạy trong **transaction + `ROLLBACK`**, dùng tenant giả,
**không** để lại dữ liệu:

```sql
BEGIN;
INSERT INTO "RepPeriodCloses"
  ("Id","TenantId","FromDate","ToDate","Status","SnapshotHash","CloseReason",
   "ClosedBy","ClosedAtUtc","IdempotencyKey","CreationTime")
VALUES
  (gen_random_uuid(), '00000000-0000-0000-0000-0000000000aa',
   '2026-09-01','2026-09-30', 2, repeat('a',64), 'Kiem thu EXCLUDE ky thu nhat',
   '00000000-0000-0000-0000-0000000000bb', now(), 'probe-rep001-1', now());

-- Kỳ thứ hai CHỒNG [09-15..10-15], cũng DaChot ⇒ phải vi phạm EXCLUDE
INSERT INTO "RepPeriodCloses"
  ("Id","TenantId","FromDate","ToDate","Status","SnapshotHash","CloseReason",
   "ClosedBy","ClosedAtUtc","IdempotencyKey","CreationTime")
VALUES
  (gen_random_uuid(), '00000000-0000-0000-0000-0000000000aa',
   '2026-09-15','2026-10-15', 2, repeat('b',64), 'Kiem thu EXCLUDE ky thu hai',
   '00000000-0000-0000-0000-0000000000bb', now(), 'probe-rep001-2', now());
-- kỳ vọng: ERROR ... exclusion constraint "EX_RepPeriodCloses_KyDaChot_KhongChong" (SQLSTATE 23P01)
ROLLBACK;
```

> Ca kiểm **phải** dùng `BEGIN … ROLLBACK` (hoặc chạy trên staging): nếu chạy `COMMIT`, dòng `probe-*` sẽ nằm lại và chặn
> rebuild các ngày đó. Nếu DB production cấm ghi tay ⇒ **chỉ** kiểm `contype='x'` ở (4) và ghi "ca EXCLUDE kiểm ở staging".

## 6. Khi hỏng — ai gọi

- **Migration / 3 bảng / 4 index `NULLS NOT DISTINCT` / `EXCLUDE` sai, `DbMigrator` hỏng:** gọi **`ops`** (chủ vận hành DB,
  chạy `DbMigrator` bằng binary mới). **Không** dùng `dotnet ef database update`.
- **Rebuild kẹt `Queued`/`Running`, worker lỗi lặp, nhịp 60 s không đủ:** gọi **`ops`** (kiểm Host/worker, khoá `report:rebuild:*`)
  → **`be`** (`ReportRebuildWorker`).
- **`0011` (ghi kỳ đã chốt) / `0014` (còn lệch mở) / `0012` (chồng kỳ):** `0011`/`0014`/`0012` là **đúng thiết kế**
  (fail-closed). Nếu nghi cổng bỏ sót ⇒ gọi **`be`** (`TongHopBaoCaoService`, `ReportAdminAppService`). **Không** gỡ cổng.
- **Chốt kỳ ra `SnapshotHash` sai / mở lại–chốt lại sinh dòng thứ hai:** gọi **`be`** (`RepPeriodClose`, `BamKyChot`).
- **Đối chiếu báo lệch sai dấu / thiếu `MismatchKind`:** gọi **`be`** (`SoDoiChieu`, `DoiChieuMotNgayAsync`).
- **`401`/`403`:** `401` khi thiếu token là **đúng thiết kế**. `403` khi thiếu quyền ⇒ kiểm 4 quyền hẹp khai **2 nơi** và
  role `admin` đã được cấp; nếu vẫn sai gọi **`be`**.
- **Hợp đồng `/api/v1/reports/*` đổi hình dạng / FE cần màn hình:** gọi **`arch`** + **`ba`** (contract, `D-276`); WP này **0** dòng FE.
- **Không chắc về secret/NCC/production, hoặc cần merge `main`:** báo **ORCH** để mở `blocked` / xin **biên bản người ký**;
  `decider` cho câu hỏi đảo ngược được. **Không** tự merge `main`, **không** đổi branch protection.

## 7. Nợ & rủi ro đã biết (theo dõi trước go-live)

| # | Mức | Nợ / rủi ro | Chặn gì | Chủ |
|---|---|---|---|---|
| 1 | 🟡 P3 R1 | **TOCTOU hẹp**: cổng đọc và `DELETE` cùng UoW nhưng khoá khác nhau (rebuild vs chốt kỳ). Cần chốt kỳ commit đúng giữa cổng-đọc và `DELETE` để lọt. | Cửa sổ rất hẹp, chấp nhận; ghi nợ. | `be` |
| 2 | 🟡 P3 R2 | **Double-submit** `StartRebuild` **đồng thời** vẫn có thể `23505` → **500** (chỉ lỗi HTTP, không hỏng dữ liệu). | Không hỏng dữ liệu; khó chịu cho FE. | `be` |
| 3 | 🟡 P3 R3 | `AlreadyRequested` gộp nghĩa "cùng yêu cầu đã xếp hàng" với "một rebuild **khác** đang chạy" ⇒ admin tưởng khoảng của mình đã xếp hàng. | Ghi rõ trong tài liệu API/màn vận hành. | `be` + `ba` |
| 4 | 🟡 P3 | **BR-002 allow-list** "đối chiếu được đọc nguồn" chưa nêu ngoại lệ trong SRS; `TC02` chỉ quét `ReportAppService.cs`. | Nợ tài liệu, không chặn code. | `ba` |
| 5 | 🟡 P3 | **BR-008 / UoW chưa có test thật** — cần harness `DoWorkAsync` + `PeriodicBackgroundWorkerContext` + UoW/tenant (khuôn `D-248`). Testplan giữ **"chưa kiểm được"**. | Bất biến "mỗi ngày một UoW, commit rồi sang ngày sau" chưa có ca runtime. | phiên dựng harness |
| 6 | 🟡 | **NFR-001/002/006/009 chưa đo định lượng** (NFR-004/007/008 mới một phần; worker thật + metric chưa kiểm). | Chưa chặn go-live read-model; cần đo trước khi tin tưởng tải lớn/3 instance. | `qa` |
| 7 | 🟡 | **Không có cờ tắt** rebuild/chốt kỳ bằng config (`REP-001-arch.md` §9.3(b) đề xuất `EnableRebuildWorker`/`EnablePeriodClose` nhưng code **không** đọc). | Đường lùi phải revert code (§4), không phải 1 dòng `.env`. | `be`/`ops` |
| 8 | 🟡 | **`Reporting:NhipJobPhut = 15` as-built** lệch `D-172` (5 phút / lookback 3 ngày / `IsStale` 900 s). **Không** đổi ở `WP-26`. | Nợ có chủ ở WP `D-172` (`D-273`). | WP `D-172` |
| 9 | 🟡 | **`D-184`** permission `eCommerce.Reporting.Aggregation.Inspect` lệch quy ước đặt tên. | Nợ **`REP-003`**, không đổi ở `WP-26`. | `REP-003` |
| 10 | 🟡 | **FE = N/A** (`D-276`: 0 dòng FE); contract + màn hình `WEB-*` nằm ngoài WP-26. | Chưa có UI vận hành rebuild/chốt kỳ; hiện dùng HTTP trực tiếp. | `ba`/`fe` (hàng contract) |
| 11 | 🔴 | Merge `main` cần **biên bản người ký**; `tich-hop/gd3` chỉ nhận khi **G6 xanh**. | Agent **không** merge `main`, **không** đổi branch protection. | chủ dự án |
