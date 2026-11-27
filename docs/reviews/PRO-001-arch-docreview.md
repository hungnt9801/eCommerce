# Doc review — `PRO-001` Kiến trúc Voucher/Coupon (`PRO-001-arch.md` v0.3)

**VERDICT: DOC-OK**

**P1 = 0 · P2 = 0 · P3 = 1**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/PRO-001-arch.md` (v0.3, 27/11/2026 — sửa docrev vòng 1) |
| Ngày · vòng | 27/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–841 (toàn văn bản v0.3; vòng 1 đọc 1–798) |
| Máy | `check-feature-doc.ps1 -WP14` → 3 LOI (`## 0.` / BR / `Đối chiếu mô hình Nhanh`) — **KHÔNG áp cho tệp `-arch`** (`D-297` ② T1, §14); không phải phát hiện |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep "rỗng / NULL / 14 / 7 trường / đúng một" toàn văn v0.3; đối chiếu cặp câu cùng đối tượng | 1 phát hiện (đếm "14 dòng map", #12); "rỗng" đã hết |
| D2 chéo tài liệu / code / D-xx | đối chiếu §6.2↔`D-295` ①(4); §7.6↔SRS §6.2 (bảng HTTP) + `PromotionEngine.cs:372/383/411` + `eCommerceHttpApiHostModule.cs:449-458`; §12-D13↔SRS §6.1 | 0 phát hiện mới |
| D3 cơ chế thực thi | §6.2 phân biệt 0 dòng vs `23505` + SAVEPOINT; §7.6 có DoD probe HTTP; unique NND; guard `UPDATE` | 0 phát hiện |
| D4 SQL chạy được | khối SQL vẫn là giả mã `@param/...`; đã chạy `BEGIN…ROLLBACK` trên PostgreSQL 17 thật (cổng 5433): conflict target v0.3 `("TenantId","PromotionId","CustomerId","UsageSequence") WHERE "CustomerId" IS NOT NULL AND "Status" <> 2` khớp index `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq` | 0 phát hiện |
| D5 test/probe/tệp có thật | đối chiếu `PromotionSurfaceTests.cs:97-108` (method `RanhGioi__…`), `PromotionEngine.cs:372/383/411`, `Promotion.cs:130`, `PostgreSqlSchemaTests.cs:227-228`, EF filter `:205` | 0 phát hiện |
| D6 nhãn tin cậy | ✅/🔶/⬜ có số đo; "TC-41 engine" đã gắn đúng `PRO-002` | 0 phát hiện |
| D7 quy tắc chất lượng | `design-doc-guidelines.md` §5 #1–#14; `-arch` ngoài mẫu 16 mục (đúng `D-297`) | 1 phát hiện (số đếm #12) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 12 | P3 | §1.2 mục 5 (dòng 86) · §7.6 (dòng 615) | Ghi "thêm **14** dòng map `01xx → HTTP`", nhưng bảng §7.6 liệt kê **19** mã và số dòng `options.Map` cần thêm là **16–17** (`0101`, `0104`, `0119` **đã có hằng nhưng chưa map** — §1.1 dòng 57 nói hiện chỉ map `0001–0010`; `0106` có hằng nhưng không ném; `0109`/`0113` không map). Con số "14" = số **hằng mới**, không phải số dòng `Map`. Không chặn: DoD §10.4 (dòng 722) probe `0104⇒409`/`0119⇒409`/`0101⇒404` sẽ đỏ nếu thiếu, buộc `be` map đủ. | Đổi "14 dòng" thành "map đủ `0101…0119` trừ `0109`/`0113` (17 dòng)" hoặc "16 dòng + 3 hằng sẵn có". |

## Vòng trước (vòng 1 — `DOC-CHANGES` P1=0/P2=5/P3=6)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (§6.2 `ON CONFLICT` không target) | đã đóng — v0.3 dòng 479–494 nêu đúng conflict target `(TenantId,PromotionId,CustomerId,UsageSequence) WHERE CustomerId IS NOT NULL AND Status <> 2`; 0 dòng = trùng số (thử lại) vs `23505` index khác = NÉM trong SAVEPOINT, không thử lại — khớp `D-295` ①(4) | P2 |
| 2 (`RanhGioiPromotionsTests` không tồn tại + "build đỏ") | đã đóng — §2.2 dòng 132–137, §10.1 dòng 693, §12-D12 trỏ đúng `PromotionSurfaceTests.RanhGioi__… (…:97-108)`, nêu vacuous + sẽ đỏ, `csproj:16` đã có ref | P2 |
| 3 (map `01xx`→HTTP thiếu) | đã đóng (mức P2) — §7.6 thêm bảng **đủ 19 mã** khớp SRS §6.2 + chỗ `eCommerceHttpApiHostModule` `:449-458` + DoD probe §10.4; còn **số đếm** "14 dòng" → tách thành P3 #12 | P2 |
| 4 ("TenantId rỗng" ≠ `NULL` hợp lệ) | đã đóng — §7.3 dòng 583–585 + §6.1 bước 1 dòng 433: `Guid.Empty ⇒ 0112`, `null` = 1-tenant hợp lệ; không còn "rỗng" | P2 |
| 5 (§12 thiếu `docDeviation` chữ ký SRS §6.1) | đã đóng — §12 **D13** (dòng 774) buộc `ba` ở `W25-2` sửa `pro-001:461-462` theo `D-298` ①(4)/`D-302` | P2 |
| 6 (engine dẫn `:292-298` không phải throw) | đã đóng — §1.1 dòng 52 + §12-D8 dòng 769 dẫn `:372,383,411` | P3 |
| 7 (`Promotion.cs:61`) | đã đóng — §12-D7 dòng 768 dẫn `Promotion.cs:130` | P3 |
| 8 (`TC-41` engine) | đã đóng — §10.1 dòng 695 nói rõ p95 engine là `TC-41` của `PRO-002` | P3 |
| 9 ("3 DTO + 5 trường") | đã đóng — §1.2 dòng 83, §13, §15 v0.2 đều "**7** trường" | P3 |
| 10 ("build đỏ thiếu ProjectReference") | đã đóng — §2.2 bỏ vế build-đỏ, nói `csproj:16` đã có ref | P3 |
| 11 (số dòng `PostgreSqlSchemaTests`/EF filter) | **rút lại** — đếm lại vòng 2: EF filter `(TenantId,OrderId)` đúng **`:205`** (`PromotionsDbContextModelCreatingExtensions.cs`), `Assert.Equal(114L…)` đúng **`:227-228`** (`PostgreSqlSchemaTests.cs`) ⇒ dẫn dòng v0.3 đúng, không phải lỗi | P3 |

## Không kiểm được

- D4: các khối SQL §6.1/§6.2 vẫn là **giả mã có tham số** (`@t`, `@voucherId`, `@seq`, `...`) nên **không chạy nguyên văn**; đã chạy **phép thử ngữ nghĩa** `ON CONFLICT` (vòng 1, áp lại cho v0.3) trong `BEGIN…ROLLBACK` trên PostgreSQL 17 thật — conflict target v0.3 trùng đúng index `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq`, và untargeted sẽ nuốt xung đột `(TenantId, IdempotencyKey)` (đã đo: 0 dòng) — nên bản sửa là đúng.
- `check-feature-doc.ps1 -WP14` KHÔNG áp cho `-arch` (3 LOI đúng như §14 khai báo) — không phải lỗi của tài liệu.
