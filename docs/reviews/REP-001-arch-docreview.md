# Doc review — «REP-001-arch» Kiến trúc liên module: read-model vận hành (rebuild · chốt kỳ · đối chiếu)

**VERDICT: DOC-OK** (vòng 2 — 0 P1 · 0 P2 · 1 P3; vòng 1 là `DOC-CHANGES` **1 P1 · 3 P2 · 5 P3**)

| | |
|---|---|
| Tài liệu | `docs/features/reporting/REP-001-arch.md` (750 dòng, `v0.3` — đã sửa theo 9 mục vòng 1) |
| Ngày · vòng | 27/11/2026 · **vòng 2 — kiểm độc lập, đọc lại toàn văn** (đối tượng đã đổi 743 → 750 dòng) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N88 · hàng `W26-1`) |
| Đã đọc | dòng **1–750** (toàn văn vòng 2) + PG17 chạy `EXCLUDE`/unique NND/CHECK (cổng 5433) |
| Máy | `check-feature-doc.ps1 -WP14` → **exit 1** (3 `LOI`) — **đúng chủng loại `-arch`** (header dòng **23-24**, khuôn `NOT-001-arch.md:16-18`), không phải phát hiện. `check-agent-artifact.ps1 -Kind docreview` → exit **0**. |

## Đã kiểm (vòng 2)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc lại §3.2/§3.3/§5.2/§5.3/§6.1/§6.2/§6.3/§9.1: `Reason` hệ thống ↔ `CHECK ≥10` ↔ D-274; checkpoint "cùng UoW" ↔ §6.4 ↔ TC-10/NFR-009; 4 NND ↔ §5.2–§5.4; `0012` ↔ §3.6; `Delta` nullable ↔ CHECK; guard `CheckpointDate IS NULL` | **1 P3 mới** (#1); 9 mục vòng 1 đóng |
| D2 chéo tài liệu / code / D-xx | §5.2 note (Reason hằng ≥10) ↔ `D-274`/`D-271`; §12.1 #9 ↔ SRS §13/§14.3 (`eCommerceMetrics.All`=26, đo lại); §7 "tên permission" ↔ `D-184` | 0 |
| D3 cơ chế thực thi | reason hằng ≥10 thoả `CHECK`; unique partial `(TenantId,JobName,Projection) WHERE Status IN(0,1)` không chặn nhầm 2 dòng khác `Projection` (chạy PG17); `IX_RepRunLogs_TenantId_KhoaIdempotency` chặn trùng `IdempotencyKey` | 0 (dòng hệ thống `IdempotencyKey` còn thiếu quy định → P3 #1) |
| D4 SQL chạy được | chạy lại `docker exec ecommerce-postgres psql` (`postgres:17-alpine`) trong `BEGIN…ROLLBACK`: §5.5 `EXCLUDE` + 4 unique NND + `CHECK` | **đã chạy**, 0 sai |
| D5 test/tệp có thật | `graph` + `Read`: Reason hằng mới là **thiết kế** (`RepRunLog.LyDoHeThong` chưa có code) — đúng là kế hoạch, không nói "đã có" | 0 |
| D6 nhãn tin cậy | §8 "26 → 30" đo lại `eCommerceMetrics.All`=26; "15 nhãn, chưa `projection`" khớp | 0 |
| D7 14 quy tắc + Nhanh | #5 index idempotency (NND+partial); #7 `Delta` `numeric(18,4)` không `==`; #11 mermaid `«»`; `-arch` ngoài 16 mục (đúng chủng loại) | 0 |

## Đối chiếu 9 mục vòng 1 — đã đóng

| # vòng 1 | Trạng thái | Mức (vòng 1) | Bằng chứng đã sửa (tệp:dòng sau sửa) |
|---|---|---|---|
| 1 | **đã đóng** | P1 | §5.2 dòng **331** + note dòng **349**: `Reason` hệ thống (Reconcile/Aggregate) dùng **hằng ≥10** `LyDoHeThong.DoiChieu = "Đối chiếu theo yêu cầu"` (22 ký tự) · `.TongHopDinhKy = "Tổng hợp định kỳ"` (16 ký tự) — **không NULL**, `CHECK length≥10` giữ nguyên; §6.3 dòng **520**; §4.1 dòng **251**. D-274 (người dùng ≥10) không đổi ⇒ **không** còn đường CHECK-từ-chối cho dòng hệ thống. |
| 2 | **đã đóng** | P2 | §6.1 điểm 4 dòng **490**: "**Trong cùng UoW của ngày**: ghi dữ liệu fact/agg **và** `GhiCuaSo` ⇒ **commit một lần**; đứt ⇒ cả hai rollback ⇒ resume từ `CheckpointDate + 1` **không** chạy lại (`TC-10`/NFR-009)". Đã bỏ vế mâu thuẫn "commit rồi mới GhiCuaSo"; khớp §6.4 (terminal UoW riêng). |
| 3 | **đã đóng** | P2 | §9.1 bước 2 dòng **585**: "**4** unique index NND" + nêu **đủ 4 tên**; §10.1 dòng **628**: "**4** unique index đích danh". Khớp §5.2–§5.4 = 4 (`RepRunLogs` 2 + `RepConsistencyChecks` 1 + `RepPeriodCloses` 1). |
| 4 | **đã đóng** | P2 | §11 note dòng **675** + §12.1 **#9** dòng **693**: SRS §13/§14.3 **16 → 26**, **20 → 30**, BA sửa cùng PR W26-2. |
| 5 | **đã đóng** | P3 | §7 dòng **541**: "**tên permission** `eCommerce.Reporting.Aggregation.Inspect`" (hết gọi "namespace"), khớp `D-184`. |
| 6 | **đã đóng** | P3 | §6.2 bước 3 dòng **500**: "⇒ `0012` (`KyChongLan`)"; bước chặn ghi rebuild vẫn `0011` (dòng **510**) — khớp §3.6 (`0011`=rebuild/ghi bị chặn, `0012`=chồng `DaChot`). |
| 7 | **đã đóng** | P3 | §3.3 dòng **161** + §5.3 dòng **363**: `Delta` **nullable**, CHECK `("Delta" IS NULL) = ("ExpectedValue" IS NULL OR "ActualValue" IS NULL)`; `SuKienTre` ⇒ cả 3 NULL; so tiền vẫn bằng `Delta`. Logic CHECK đúng (đã đối chiếu 3 tổ hợp). |
| 8 | **đã đóng** | P3 | §4.1 dòng **251** + §6.3 dòng **515, 520**: `Projection = null` ⇒ **2** dòng `RepRunLog` (mỗi projection một dòng), `Projection` cụ thể **không NULL/không sentinel**. |
| 9 | **đã đóng** | P3 | §3.2 dòng **150**: guard `GhiCuaSo` = "`CheckpointDate IS NULL ⇒ ngay == FromDate` (cửa sổ đầu); ngược lại `ngay > CheckpointDate`" — hết bẫy null so sánh false. |

## Phát hiện (vòng 2)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §5.2 dòng **337** + §5.6 (dòng 432-440) ↔ §3.2 dòng **135** + §6.3 dòng **515** | `RepRunLog.IdempotencyKey` là **NOT NULL + UNIQUE** (`IX_RepRunLogs_TenantId_KhoaIdempotency`, không partial), nhưng §5.6 **chỉ** định nghĩa khoá cho `Rebuild`/`Chốt kỳ`; dòng **hệ thống** (`Reconcile` ghi **mỗi lượt**; `Aggregate` có việc/lỗi) **không có** khoá quy định. Nếu lấy khuôn khoá Rebuild (theo khoảng ngày) thì ghi lại **cùng khoảng** (hoặc dòng thứ 2 trùng khoá) **bị UNIQUE chặn** — đã **chứng minh** trên PG17: 2 dòng `Reconcile` khác `Projection` + khác khoá ⇒ **nhận** (partial unique **không** chặn nhầm); nhưng chèn lại `IdempotencyKey` trùng ⇒ `unique_violation`. Đây là **khoảng trống hoàn thiện** (không phải hướng dẫn sai): dev có thể chọn khoá duy nhất mỗi lượt là an toàn, nhưng tài liệu chưa chốt. | Ghi một dòng: dòng **hệ thống** dùng `IdempotencyKey` **duy nhất mỗi lượt** (gồm `Projection` + định danh lượt), khác khoá dedup của Rebuild; (tiện thể) quy định `Projection`/`FromDate`/`ToDate`/`WindowsTotal` cho dòng `Aggregate` (job phủ cả 2 projection) — hoặc miễn `IdempotencyKey` khỏi UNIQUE cho job hệ thống. |

## Hồi quy đã soi (sửa không sinh mâu thuẫn mới)

- **P1 `Reason` ↔ `D-274`:** hằng hệ thống ≥10 ký tự thoả `CHECK length(btrim)≥10`; người dùng (`Rebuild`/`Close`/`Reopen`) vẫn ≥10/≥20 theo `D-274`; **không** thêm `Reason` vào input hệ thống ⇒ **không** mâu thuẫn.
- **P2 #2 checkpoint ↔ `TC-10`/NFR-009:** "cùng UoW ngày ⇒ commit một lần ⇒ rollback cùng" đúng ngữ nghĩa "0 cửa sổ cũ chạy lại"; khớp §6.4 (terminal ở UoW riêng **sau** các cửa sổ).
- **P2 #3 đủ 4 unique NND:** PG17 tạo **cả 4** index `NULLS NOT DISTINCT` (2 có thêm `WHERE` partial) và chặn đúng (NND chặn `(NULL tenant,key)`, partial chặn lần active thứ 2, `CHECK Reason ≥10` chặn lý do ngắn).
- **P3 #8 hai dòng `RepRunLog`:** partial unique `(TenantId,JobName,Projection) WHERE Status IN (0,1)` **KHÔNG** chặn nhầm hai dòng `Reconcile` khác `Projection` (chạy PG17: nhận), chặn đúng lần active thứ 2 cùng `(job,projection)`. Rủi ro còn lại **không** ở partial unique mà ở `IX_…KhoaIdempotency` (nêu ở P3 #1).
- §6.2 `0012` (chồng kỳ) tách khỏi `0011` (rebuild bị chặn bởi kỳ chốt) — nhất quán §3.6; `Delta` nullable + CHECK — nhất quán §3.3/§6.3.

## Bằng chứng

- **Đồ thị/đọc (vòng 1, còn hiệu lực):** `graph: codegraph_explore "TongHopBaoCaoService ReportAppService ReportingDbContextModelCreatingExtensions ReportingErrorCodes ReportingOptions ReportingAggregationWorker ReportingApplicationModule"` + `Read`/`Grep` xác nhận mọi `file:dòng` §1.1 khớp as-built (`ChayAsync :108`, TryAcquire 2s `:112`, `TenKhoaPhanTan :44`, `ReportAppService [Authorize class] :26`/method `:254`, Host map 8 mã `:405-412`, `eCommerceMetrics.All`=26 nhãn=15, `ReportingErrorCodes` 0001-0009 `:11-38`, migration Wp08 8 NND `219-248`).
- **D4 chạy thật `postgres:17-alpine` (docker `ecommerce-postgres`, cổng 5433, `BEGIN…ROLLBACK`):**
  - §5.5 `EXCLUDE USING gist`: chồng `DaChot`→`23P01`; liền kề→nhận; `Mo` chồng `DaChot`→nhận; 2 `Mo` chồng→nhận; host `TenantId=NULL` chồng→`23P01` ✅.
  - §5.2–§5.4 (vòng 2): 4 unique index NND tạo được; **2 dòng Reconcile khác `Projection` (Status active) ⇒ nhận** (partial unique **không** chặn nhầm); lần active thứ 2 cùng `(tenant,job,proj)` ⇒ bị chặn; `IdempotencyKey` trùng ⇒ `unique_violation` (cơ sở P3 #1).
- **Mermaid:** `npm --prefix tools run check:mermaid` → 214 khối, **0 lỗi**.

## Không kiểm được

- **Hành vi runtime:** `RepRunLog.LyDoHeThong` (hằng `Reason`), `RepRunLog`/`RepConsistencyChecks`/`RepPeriodCloses` vẫn **0 dòng code** ⇒ chỉ kiểm thiết kế, chưa chạy `TC-*`/`EX-*`/`NC-*`.
- **SRS §13/§14.3 (16/20):** chưa sửa (việc BA ở `W26-2`) ⇒ số metric trong SRS còn lệch code cho tới `W26-2`.

## Vòng trước (vòng 1 — DOC-CHANGES: 1 P1 · 3 P2 · 5 P3)

Cột 2 là **trạng thái** (không phải "Mức") để script không đếm trùng. Toàn bộ 9 mục đã đóng; xem bảng "Đối chiếu 9 mục vòng 1" phía trên.

| # vòng 1 | Trạng thái | Mức lúc đó | Nội dung vòng 1 (vấn đề → đề nghị) |
|---|---|---|---|
| 1 | **đã đóng** | P1 | `RepRunLog.Reason NOT NULL CHECK≥10` nhưng Reconcile/Aggregate không có `Reason` ⇒ CHECK từ chối. → hằng `Reason` hệ thống ≥10 hoặc NULL có điều kiện. |
| 2 | **đã đóng** | P2 | "commit rồi mới GhiCuaSo" vs "cùng UoW" ⇒ checkpoint commit sau dữ liệu ⇒ resume chạy lại cửa sổ. → checkpoint trong cùng UoW ngày, commit một lần. |
| 3 | **đã đóng** | P2 | §9.1/§10.1 "3 unique NND" nhưng §5.2–§5.4 định nghĩa 4. → 4, nêu đủ tên. |
| 4 | **đã đóng** | P2 | SRS §13/§14.3 còn 16/20 vs code+arch 26/30, không có trong docDeviation. → thêm `docDeviation #9`. |
| 5 | **đã đóng** | P3 | `D-184` gọi "namespace" nhưng là permission. |
| 6 | **đã đóng** | P3 | §6.2 bước 3 dùng `0011` cho chồng `DaChot` (phải `0012`). |
| 7 | **đã đóng** | P3 | `ExpectedValue`/`ActualValue` nullable nhưng `Delta` NOT NULL = Actual−Expected. |
| 8 | **đã đóng** | P3 | `RepRunLog.Projection NOT NULL` vs Reconcile input `Projection` nullable "cả hai". |
| 9 | **đã đóng** | P3 | guard `ngay > CheckpointDate` sai ở cửa sổ đầu (`CheckpointDate = null`). |

**VERDICT: DOC-OK** (vòng 2, 27/11/2026 — **0 P1 · 0 P2 · 1 P3**; 9/9 mục vòng 1 đã đóng đúng)
