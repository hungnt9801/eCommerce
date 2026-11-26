# Handover N88 — WP-26 / hàng `W26-1` (F0–F2b: ARCH + DOCREV + G1/G2) XONG

> Phiên `N88` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W26-1` · WP-26 · mã `REP-001` · nhánh
> `feat/gd3-wp26-read-model` (base `origin/tich-hop/gd3` @ `763d376`; `HEAD` `b1fa9ce`) · 27/11/2026.
> Phạm vi phiên: **F0** đọc SRS theo mục · **F2 ARCH** (`arch`) · **F2b DOCREV** (`docrev` ×2 vòng) ·
> **G1/G2 theo M2** (`decider` `D-270…D-274`). **0 migration, 0 `.cs`**, `main` không đụng.

## 1. Đã xong

- **F0 — đọc SRS `REP-001` theo mục** (`docs/features/reporting/rep-001-read-model-job-tong-hop.md`, 1031 dòng):
  lõi read-model **đã có** (WP-08, 8 bảng `Rpt*`); WP-26 làm phần **vận hành GĐ3 còn lại** — rebuild/backfill có
  checkpoint + audit, chốt kỳ, đối chiếu lộ lệch, 4 metric, permission. Xác định **người review** theo loại feature:
  **Kiến trúc trưởng · BA · kế toán** (biên bản tạm duyệt `gd3-c` dòng `REP-001`).
- **F2 — `arch` viết `docs/features/reporting/REP-001-arch.md`** (759 dòng, mới): 3 bảng vận hành
  **`RepRunLogs`** (checkpoint rebuild + audit), **`RepConsistencyChecks`** (lộ lệch, `Delta` không `==`),
  **`RepPeriodCloses`** (`EXCLUDE USING gist` + `btree_gist`, **chỉ** chặn 2 kỳ `DaChot` chồng nhau — 5 ca chạy
  THẬT trên `postgres:17-alpine`); `IReportAdminAppService` (StartRebuild/ResumeRebuild/ClosePeriod/ReopenPeriod/
  RunReconciliation) + `ReportRebuildWorker` (quét DB, `ICurrentTenant.Change` trước UoW); 4 metric `All` 26→30 +
  nhãn `projection`; mã lỗi `0010–0017`; permission `eCommerce.Reports.{Rebuild,PeriodClose,PeriodReopen,Reconcile}`;
  migration expand-only `Wp26_VanHanh_BaoCao` + đường lùi; kế hoạch **4 ca EXCLUDE** trước `migrations add` (`D-171`).
- **F2b — `docrev` kiểm 2 vòng (ngữ cảnh sạch):** vòng 1 **`DOC-CHANGES` 1 P1 · 3 P2 · 5 P3**
  (`RepRunLog.Reason` NOT NULL `>=10` mâu thuẫn `D-271`; checkpoint "commit rồi mới ghi" tự mâu thuẫn; "3 unique NND"
  trong khi thiết kế 4; metric SRS 16/20 vs code 26/30) → `arch` sửa → vòng 2 **`DOC-OK` 0 P1 · 0 P2 · 1 P3**
  (`IdempotencyKey` dòng hệ thống) → `arch` sửa → **`DOC-OK`**. DDL `EXCLUDE`/`NND+partial`/`CHECK` đã chạy thật PG17.
- **G1/G2 theo M2 — `decider` `D-270…D-274`** (5 TẠM CHỐT, **0 CHUYEN_NGUOI**): nhóm permission `eCommerce.Reports.*`
  + `Reconcile` (`D-270`); phạm vi `RepRunLog` (`D-271`); rebuild `TonKho` quá khứ từ chối `0013` (`D-272`);
  `D-172` ngoài WP-26 (`D-273`); ngưỡng lý do đóng/mở/rebuild 10/20/10 + `0016` (`D-274`, kế toán ký trước release).

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** (CS8604 có sẵn) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2283/2283** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | **PASS** · 960 link · 1675 đường dẫn |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-26` | 0 | **XANH** — mọi mã WP-26 `Tạm duyệt` (biên bản `gd3-c`) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/REP-001-arch-docreview.md` | 0 | **DAT** · `DOC-OK` · **P1=0 · P2=0** |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** · 116 dòng |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | **PASS** · 961 link · 1678 đường dẫn · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **214** khối · **0** lỗi |
| `pwsh -File tools/kiem-tat-ca.ps1` (cổng nhẹ) | 0 | **10/10 bước ĐẠT · 326,7 s** · 0 bước HỎNG |
| ├ build (bước 2) | 0 | 0 error · 9 warning · 83,8 s |
| ├ unit (bước 3) | 0 | **2283/2283** · 22,5 s |
| ├ integration (bước 4) | 0 | **110/110** · 27,3 s |
| └ 5–10 (mermaid · cổng tài liệu · FE typecheck/Vitest · `gen:api` · DB 25/25 · test cổng 60/60) | 0 | **ĐẠT** |
| `git push -u origin feat/gd3-wp26-read-model` | 0 | nhánh mới `b1fa9ce` |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W26-1`** — F0–F2b của hàng này đã xong, DoD đạt. Phần còn lại của WP-26 là **hàng kế**
`W26-2` (F3 BE), không phải cắt giữa.

Các việc **cắt CÓ QUYẾT ĐỊNH** (giao W26-2 / BA, nên trạng thái vẫn là **B**):
- `ba` sửa SRS `REP-001` **cùng PR W26-2**: `NFR-007` + §5.2.4/§5.2.5/§5.2.6 + §7.3 + §9 + §3.1; metric §13/§14.3
  **16→26 → 20→30**; **10 P3** vòng 3 (`docs/reviews/REP-001-docreview.md` dòng 39–48).
- 10 `docDeviation` của ARCH (cuối `REP-001-arch.md` §11–§12) chốt khi code ở W26-2.
- `D-184` (`eCommerce.Reporting.Aggregation.Inspect`) đồng nhất namespace khi làm `REP-003` (`D-270` ghi nợ).

## 3. Migration đã áp

**Không có migration nào trong phiên này** (0 `.cs`, 0 bảng mới). Thiết kế 3 bảng + migration expand-only
`Wp26_VanHang_BaoCao` chỉ là **kế hoạch** trong `REP-001-arch.md`; `W26-2` sẽ sinh migration và **phải** `pg_dump`
trước khi áp (K-01/M9) + đạt **4 ca EXCLUDE** (`D-171`) trước `migrations add`.

## 4. Quyết định mới

**5 `D-xx` TẠM CHỐT (DEC, uỷ quyền `D-114`/M2)** — không có câu `CHUYEN_NGUOI`, không có dòng người chốt:

| Mã | Nội dung 1 dòng |
|---|---|
| `D-270` | Permission `eCommerce.Reports.{Rebuild,PeriodClose,PeriodReopen,Reconcile}` (con của `Reports.Default`, chỉ cấp admin tenant) + **thêm** `Reports.Reconcile`; không đảo `D-184`. |
| `D-271` | `RepRunLog` ghi **luôn** cho rebuild + reconcile; lượt `Aggregate` chỉ ghi khi có việc/lỗi (lượt rỗng vẫn log + metric). |
| `D-272` | Rebuild `TonKho` cho ngày quá khứ **từ chối** (`eCommerce:Reports:0013`, map 400); chỉ `FromDate==ToDate==hôm nay` (UTC+7). |
| `D-273` | `D-172` (nhịp 5 phút/lookback 3 ngày/`IsStale` 900 s) **ngoài** WP-26; giữ 15 phút as-built; nợ có chủ WP `D-172`. |
| `D-274` | Lý do đóng kỳ ≥ **10**, mở lại ≥ **20**, rebuild ≥ **10** ký tự; thiếu ⇒ `0016` (map 400); kế toán R3-3 duyệt lô trước release. |

## 5. Nợ / rủi ro

- 🟠 **SRS `REP-001` lệch code/arch** (chủ **`ba`**, sửa cùng PR W26-2): metric §13/§14.3 còn 16/20 trong khi code
  26 và arch thiết kế 30; `NFR-007` "100 % lượt chạy có `RepRunLog`" trái `D-271`; §5.2.4/§5.2.5/§5.2.6/§7.3/§9/§3.1
  cần bổ sung cột/khoá/`0013`/`0016`. 10 P3 vòng 3 (docreview SRS) vẫn mở.
- 🟡 **10 `docDeviation` ARCH ↔ SRS** (chủ **`be`/`ba`**, W26-2): `CASE WHEN Status=2` thay `Status WITH =`; `Projection`
  int + `RowsWritten`; `report:rebuild` 30 s tách khỏi `report:tong-hop` 2 s; `DaMoLai` giữ dòng; `ResolvedAtUtc/By`
  + unique partial đang mở; thêm khoá dòng hệ thống `Report:Reconcile/AggregateRun:{runId}` + unique partial
  `WHERE RequestedBy IS NOT NULL`.
- 🟡 **`D-184`** dùng permission `eCommerce.Reporting.Aggregation.Inspect` (không phải `eCommerce.Reports.*`) — chủ
  **`be`/`ba`** khi làm `REP-003`.
- 🟡 **NFR-003 bị code vi phạm** (`ReportingOptions.NhipJobPhut=15` vs D-172 5 phút) — nợ có chủ **WP `D-172`**
  (`D-273` tách khỏi WP-26).
- 🟡 **4 ca EXCLUDE bắt buộc + `pg_dump`** trước `migrations add` (`D-171`, K-01/M9) — chủ **`be`/`ops`**, W26-2.
- 🟡 **Hàng rào hook**: **4** cảnh báo trong `artifacts/agents/canh-bao.jsonl` — 2 lượt `docrev` "thiếu khối JSON cuối"
  và 2 lượt `arch` "thiếu khoá `boundaryViolations`" (harness **không** cấp `last_assistant_message` ở lượt resume;
  báo cáo thực tế **có** khối JSON). **0** lần chặn dừng; **`ngoaiVung=[]`** mọi lượt.
- 🟡 **`check-feature-doc -WP14` exit 1 (3 LOI)** là **đúng chủng loại** cho tệp `-arch` (header mẫu), không phải phát hiện.
- **Hàng đợi xem lại**: `decider` thêm **5** dòng (`D-270…D-274`), xem `docs/agents/hang-doi-xem-lai.md`.
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (`P-21`, `D-71`/§A.5); `tich-hop/gd3` chưa nhận nhánh này.
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền)**: (a) duyệt lô `D-270…D-274`; (b) **kế toán R3-3** ký ngưỡng lý do
  `D-274` trước release; (c) merge `main`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W26-1` → mở `W26-2` (WP-26, F3 BE).**

- Hàng kế của chuỗi `gd3-1` = **`W26-2`** (WP-26, bước **F3 BE**: domain + app service + endpoint + migration
  expand-only + test đơn vị; **`pg_dump` trước khi áp**; **4 ca EXCLUDE đỏ→xanh trước `migrations add`**).
- Đọc `REP-001-arch.md` (759 dòng) + SRS + `D-270…D-274`; `ba` sửa SRS **cùng PR**; **không** đụng cột giá vốn /
  COD / bảng fact giờ / `REC-003`; **không** sửa `eCommerce.slnx`/`Directory.Packages.props`/`pnpm-lock.yaml`.
- Chạy `/chay-phien-code chuoi=gd3-1` (không cần mã hàng — `PH next` trả `W26-2`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp26-read-model`; base `origin/tich-hop/gd3` @ `763d376`; `HEAD` **`b1fa9ce`**
  (`docs(reporting): N88 - W26-1 F0-F2b arch REP-001 + D-270..274`, 6 tệp, +859/−5).
- **Đã push** `origin/feat/gd3-wp26-read-model` (`b1fa9ce`, nhánh mới, đặt upstream). **Chưa** đưa vào
  `tich-hop/gd3` — việc đó thuộc hàng `dong-wp` (`W26-5`) khi G6 xanh (fast-forward).
- **Không** mở PR (`D-216` ③); PR xếp chồng mở một lần cuối chuỗi.

---

**Hàng rào agent** (phiên `N88`): `arch` ×1 (bị trần 30 lượt giữa chừng, resume 4 lượt cùng agent: viết → áp `D-270…274`
→ sửa vòng 1 → đóng P3) · `docrev` ×1 agent (kiểm vòng 1 `DOC-CHANGES`, resume kiểm vòng 2 `DOC-OK`) · `decider` ×1.
**0** lần chặn dừng; **0** tệp ngoài vùng (`ngoaiVung=[]`); **4** cảnh báo định dạng JSON (harness, xem §5).
