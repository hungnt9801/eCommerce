# Handover N89 — WP-26 / hàng `W26-2` (F3 BE: vận hành báo cáo) XONG

> Phiên `N89` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W26-2` · WP-26 · mã `REP-001` · nhánh
> `feat/gd3-wp26-read-model` (base `origin/tich-hop/gd3` @ `763d376`; `HEAD` code+docs `7b82b53`; handover commit theo sau) · 27/11/2026.
> Phạm vi phiên: **F3 BE** (domain + app service + endpoint + migration expand-only + test đơn vị) · **D-275** (`be` làm tiếp) ·
> **BA đồng bộ SRS v0.4** cùng PR · 1 migration đã áp sau `pg_dump`. `main` không đụng; chưa vào `tich-hop/gd3`.

## 1. Đã xong

- **F3 BE — 3 bảng vận hành** (module `Reporting`): `RepRunLogs` (nhật ký + checkpoint rebuild, `D-271`/`D-275`),
  `RepConsistencyChecks` (lộ lệch BR-012), `RepPeriodCloses` (chốt kỳ, `D-171` + `EXCLUDE USING gist`).
- **Domain**: 4 enum `smallint` + CHECK (`RepProjection`/`RepRunStatus`/`RepPeriodStatus`/`RepMismatchKind`); 3 aggregate
  (máy trạng thái NÉM `0015`; `RepRunLog.GhiCuaSo` checkpoint; `RepPeriodClose` đóng/mở lại tái dùng dòng);
  hàm thuần `SoDoiChieu`/`BamKyChot`/`KhoangChot`/`NhatKyAggregate`; `RepIdempotency`; `ReportingLockKeys`; mã lỗi **`0010–0017`**.
- **Application**: `IReportAdminAppService` + DTO (map tay, 0 PII); `ReportAdminAppService` (rebuild/resume/get/list ·
  chốt/mở/get/list · đối chiếu/lệch); `ReportRebuildWorker` (`ICurrentTenant.Change` **trước** UoW, mỗi ngày **một** UoW
  + `GhiCuaSo` cùng commit, terminal UoW riêng, resume từ `CheckpointDate+1`); tách `TongHopBaoCaoService.ChayKhoangAsync`
  (không giành khoá toàn cục); **`D-275`**: `ReportingAggregationWorker` ghi `RepRunLog` cho lượt `Aggregate` (có việc ⇒
  dòng BanHang/TonKho cùng UoW; lỗi ⇒ đúng 1 dòng `Failed` UoW riêng; rỗng/bỏ nhịp khoá ⇒ 0 dòng).
- **EF**: 3 entity config + 3 `DbSet`; migration **expand-only** `20261126181842_Wp26_VanHanh_BaoCao` (EF sinh, sửa tay
  4 unique `NULLS NOT DISTINCT` + partial + `btree_gist` + `EXCLUDE`).
- **Permission** `eCommerce.Reports.{Rebuild,PeriodClose,PeriodReopen,Reconcile}` (hằng + `AddChild`, `D-270`);
  4 metric `All` **26→30** + nhãn `projection`; 10 endpoint trên `ReportsController` + map `0010–0017` ở Host.
- **BA**: SRS `REP-001` **v0.3 → v0.4** — 10 `docDeviation` + 10 P3, metric 16/20 → 26/30, NFR-007 theo `D-271`,
  `D-275` as-built; `XC-005` §5.1 26→30.
- **DB**: `pg_dump` **497 829 B** (`artifacts/pg-dump/N89-W26-2.dump`) → `DbMigrator` áp; kiểm `__EFMigrationsHistory`
  top = migration này, 3 bảng, `EX_RepPeriodCloses_KyDaChot_KhongChong` (`contype='x'`), 4 index NND.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** (CS8604 có sẵn) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2283/2283** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | **PASS** · 962 link · 1684 đường dẫn |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-26` | 0 | **XANH** (mọi mã WP-26 `Tạm duyệt`) |
| `dotnet build eCommerce.slnx --no-incremental` (G3) | 0 | **0 error · 9 warning** (0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (G3) | 0 | **2342/2342** (+59 so baseline) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **122/122** (+12) · 4 ca `EXCLUDE` + 4 NND |
| `pwsh -File tools/kiem-tat-ca.ps1` (sau D-275) | 0 | **10/10 bước ĐẠT · 342,5 s** |
| ├ build / unit / integration | 0 | 0 error · 2342 · 122 · 77,9 s / 28,3 s / 27,4 s |
| └ mermaid · cổng tài liệu · FE typecheck/Vitest · `gen:api` · DB · test cổng | 0 | **ĐẠT** (214/0 · 60/60) |
| `pwsh -File tools/agents/check-feature-doc.ps1 -WP14 -Path …rep-001…` | 0 | **DAT** · 16/16 heading · 17/17 BR |
| `npm --prefix tools run check:mermaid` | 0 | **214** khối · **0** lỗi |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | **PASS** · 962 link · 1687 đường dẫn |
| `negative-control.ps1` NC-N89-1 (Rebuild key `host`→`tenant`) | 0 | **DAT** · đỏ (exit 1) → khôi phục xanh (exit 0) |
| `negative-control.ps1` NC-N89-2 (bỏ guard `SoAnhTonKho > 0`) | 0 | **DAT** · đỏ (exit 1) → khôi phục xanh (exit 0) |
| `docker exec … pg_dump -Fc ecommerce > artifacts/pg-dump/N89-W26-2.dump` | 0 | **497 829 B** |
| `dotnet run --project src/eCommerce.DbMigrator` + `psql` kiểm | 0 | migration top · 3 bảng · `x` + 4 NND |

## 2. Chưa xong / cắt giữa

**Không có việc dở của W26-2** — F3 BE + D-275 + BA SRS v0.4 xong, DoD G3 đạt. Phần còn lại là **hàng kế** `W26-3`
(F3 FE) của WP-26, không phải cắt giữa.

Các việc **cắt CÓ QUYẾT ĐỊNH** (giao hàng sau, trạng thái vẫn **B**):
- **Harness `DoWorkAsync` cho module Reporting chưa có** ⇒ 3 ca worker (có việc/lỗi/rỗng) mới kiểm bằng **hàm thuần**
  `NhatKyAggregate`; gọi worker thật để **QA** dựng khuôn `D-248` ở G4 (testplan ghi "chưa kiểm được", **không** đánh dấu phủ).
- `EXPLAIN` index mới (NFR-003/007), test 3 instance (NFR-006), TC-06 cách ly 2 tenant, TC-10 resume đầy đủ ⇒ **QA/G4**.
- `D-172` (nhịp 5 phút/lookback/`IsStale`) **ngoài** WP-26 (`D-273`), giữ 15 phút as-built.
- Đối chiếu `TonKho` chỉ so được **hôm nay** (ảnh là trạng thái hiện tại — `D-47` ⑦).

## 3. Migration đã áp

- **`20261126181842_Wp26_VanHanh_BaoCao`** (expand-only): `CREATE TABLE` ×3 (chỉ bảng mới) + 4 unique index
  `NULLS NOT DISTINCT` (partial đúng chỗ) + `CREATE EXTENSION btree_gist` + `EXCLUDE USING gist`; `Down()` chỉ `DropTable` 3 bảng mới.
- **`pg_dump` trước khi áp**: `artifacts/pg-dump/N89-W26-2.dump` = **497 829 byte** (K-01/M9).
- Áp bằng `dotnet run --project src/eCommerce.DbMigrator` (**không** `dotnet ef database update`).
- Kiểm DB thật: migration top trong `__EFMigrationsHistory`; `EX_RepPeriodCloses_KyDaChot_KhongChong` (`contype='x'`);
  `IX_RepRunLogs_TenantId_KhoaIdempotency` · `UX_RepRunLogs_TenantId_Job_Projection_DangChay` ·
  `IX_RepPeriodCloses_TenantId_KhoaIdempotency` · `UX_RepConsistencyChecks_DangMo`.

## 4. Quyết định mới

**1 `D-xx` TẠM CHỐT (DEC, uỷ quyền `D-114`/M2)** — không có câu `CHUYEN_NGUOI` không giải được:

| Mã | Nội dung 1 dòng |
|---|---|
| `D-275` | Giữ `D-271`: `ReportingAggregationWorker` ghi `RepRunLog` cho lượt `Aggregate` — **có việc** (BanHang/TonKho) ghi trong **cùng UoW** trước `CompleteAsync`; **lỗi** ⇒ đúng **1 dòng `Failed`** UoW riêng (`LastErrorCode` = mã nghiệp vụ hoặc tên kiểu); **rỗng/bỏ nhịp khoá** ⇒ **0 dòng**. Hàm thuần `NhatKyAggregate` + 6 test; QA dựng harness `DoWorkAsync` (`D-248`) ở G4. |

## 5. Nợ / rủi ro

- 🟠 **Harness `DoWorkAsync` module `Reporting` chưa có** (chủ **`qa`**, W26-4): 3 ca worker mới phủ ở tầng hàm thuần;
  phải dựng khuôn `D-248` + chạy worker thật cho NFR-007, nếu không testplan ghi "chưa kiểm được".
- 🟡 **`D-272`** rebuild `TonKho` chỉ hôm nay; `D-275` dòng lỗi **đại diện BanHang** (không biết projection nào hỏng) — đúng scope tối thiểu.
- 🟡 **NFR-003 bị code vi phạm** (`ReportingOptions.NhipJobPhut=15` vs `D-172` 5 phút) — nợ có chủ **WP `D-172`** (`D-273`).
- 🟡 **`D-184`** permission `eCommerce.Reporting.Aggregation.Inspect` lệch quy ước — nợ **REP-003**.
- 🟡 **Hàng rào hook**: `lanChan=0`; cảnh báo `ngoaiVung` là **XC-005** + append "Trạng thái triển khai" (dòng heuristic)
  + **2 tệp test** (DoD yêu cầu) — không phải rò rỉ thật. `be`/`decider` bị cảnh báo "thiếu khối JSON" (harness
  `last_assistant_message` rỗng ở lượt resume; báo cáo thực **có** JSON). Bao gồm 4 cảnh báo N88 cũ.
- **Hàng đợi xem lại**: `decider` thêm **1** dòng (`D-275`), xem `docs/agents/hang-doi-xem-lai.md`.
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (`P-21`, `D-208`); `tich-hop/gd3` chưa nhận nhánh này.
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền)**: (a) duyệt lô `D-270…D-275`; (b) **kế toán R3-3** ký ngưỡng lý do
  `D-274` (10/20/10) trước release; (c) merge `main`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W26-2` → mở `W26-3` (WP-26, F3 FE).**

- Hàng kế của chuỗi `gd3-1` = **`W26-3`** (WP-26, bước **F3 FE**: màn Web Admin theo SRS, contract-first
  `schema.d.ts`; **WP không có FE ⇒ done kèm lý do**).
- Đọc `REP-001-arch.md` §4.2 (10 endpoint) + SRS v0.4 §6/§7 + `D-270…D-275`; kiểm `ReportsController` route đã có;
  nếu có màn ⇒ `pnpm gen:api` + dựng UI; nếu không ⇒ ghi lý do từ SRS.
- Chạy `/chay-phien-code chuoi=gd3-1` (không cần mã hàng — `PH next` trả `W26-3`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp26-read-model`; base `origin/tich-hop/gd3` @ `763d376`; `HEAD` **`7b82b53`**
  (`docs(reporting): N89 - W26-2 SRS v0.4 ...`) — trước đó `681c289` (F3 BE) + `0718f13` (D-275 code).
- **Đã push** `origin/feat/gd3-wp26-read-model` (`7b82b53`). **Chưa** đưa vào `tich-hop/gd3` — thuộc hàng `dong-wp`
  (`W26-5`) khi G6 xanh (fast-forward).
- **Không** mở PR (`D-216` ③); PR xếp chồng mở một lần cuối chuỗi.

---

**Hàng rào agent** (phiên `N89`): `be` ×2 lượt (lần 1 chạm trần 80 lượt giữa chừng — đã resume hoàn tất; lần 2 làm `D-275`) ·
`ba` ×2 (SRS v0.4 + D-275) · `decider` ×1 (`D-275`). **0** lần chặn dừng (`lanChan=0`); cảnh báo `ngoaiVung` đã ghi ở §5
(không có rò rỉ thật). 2 lần NC đỏ→xanh (NC-N89-1/2).
