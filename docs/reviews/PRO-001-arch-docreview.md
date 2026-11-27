# Doc review — `PRO-001` Kiến trúc Voucher/Coupon (`PRO-001-arch.md` v0.3 → v0.4)

**VERDICT: DOC-OK**

**P1 = 0 · P2 = 2 · P3 = 4** (toàn tệp; **vòng 4 delta N101 `D-305`/`D-306`**: P1=0 · P2=0 · P3=5 — verdict **mới nhất**, xem mục "Vòng 4" cuối tệp; vòng 3 delta N101: P1=0 · P2=2 · P3=3)

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

---

## Vòng 3 — delta N101 (27/11/2026)

> Phạm vi: **delta** của N101/W25-2 trên 2 tệp — `pro-001-voucher-coupon.md` §6.1 (`ReserveVoucherDto` +`Subtotal`/`Currency`/`TtlSeconds`, `CommitVoucherDto` +`CheckoutSessionId`; đồng bộ §1 basic flow, JSON mẫu, code mẫu §14) và `PRO-001-arch.md` **v0.4** (§4.2 đổi `Guid`→`string` cho `CheckoutSessionId` ở **cả hai** record; §1.2/§7.6 "14→17 dòng map"). Căn cứ `D-298`, `D-302`, `D-303`, `D-304`. Toàn văn đã `DOC-OK` ở vòng 2 (N100); đây là kiểm **delta**, không kiểm lại toàn văn.
>
> **VERDICT (vòng 3): DOC-OK** — **P1 = 0 · P2 = 2 · P3 = 3** (delta). Verdict của mục này là verdict của lần kiểm này.

### Đã kiểm (delta)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đối chiếu cặp câu cùng đối tượng trong delta: SRS §1 basic flow ↔ §6.1 bảng DTO ↔ JSON mẫu ↔ code §14 (thứ tự `subtotal, currency, ttlSeconds` khớp record; `Subtotal=500000` = 2×250000 ở JSON, `100_000m` = 1×100_000 ở code); §1.2 ↔ §7.6 ↔ changelog v0.4 (đều "17") | 0 phát hiện D1 (ngoài #1 là D2) |
| D2 chéo tài liệu / `D-xx` | `CheckoutSessionId` = `string` (≤128) ở arch §4.2 **cả hai** record (300/310) — khớp `D-303`/`D-304`; SRS chỉ khai `string` cho `CommitVoucherDto.CheckoutSessionId` (480), **không** có khai tương ứng cho `ReserveVoucherDto` (#5); `Subtotal` `decimal`/`Currency` `string`/`TtlSeconds` `int` khớp §4.2 ↔ SRS ↔ `D-298`/`D-302`; **§12-D13 ↔ SRS §6.1 thực tế mâu thuẫn** (#1). `graph: codegraph_explore "CheckoutInput IdempotencyKey CheckoutDtos TaoDonTuCheckoutInput CheckoutSessionId"` ⇒ `CheckoutInput.IdempotencyKey` là `string [StringLength(128)]`. | 2 phát hiện (#1 P2, #5 P3) |
| D3 cơ chế thực thi | `TtlSeconds` `[Range(60,86400)]` **và** arch §6.1 bước 1 nêu NÉM `AbpValidationException` — cơ chế có (400); `Subtotal` là `decimal` **non-nullable** "bắt buộc" nhưng `[Required]` vô hiệu trên kiểu giá trị, không `[Range]`/guard ⇒ `= 0`/thiếu **KHÔNG** bị chặn dù `D-304` test `= 0 ⇒ 400` (#2) | 1 phát hiện (#2 P2) |
| D4 SQL chạy được | **không có** khối `sql`/DDL mới trong delta (grep `^\+.*```sql`/`ALTER TABLE`/`INSERT INTO` = 0; các cụm `UPDATE` chỉ nằm trong **văn xuôi** SRS §1 basic flow). Kế thừa kết quả vòng 1 (đã chạy `BEGIN…ROLLBACK` trên PG17). | 0 phát hiện |
| D5 test/probe/tệp có thật | `graph: codegraph_explore "CheckoutInput IdempotencyKey CheckoutDtos …"` ⇒ khai báo thật ở `CheckoutDtos.cs:67-69`; SRS:480 lại dẫn `63-69` (#4). Delta **không** nêu tên test mới; `D-304` hoãn 2 ca (`TtlSeconds` ngoài dải, `Subtotal`=0) về `qa` W25-4 — ghi rõ mốc, hợp lệ. | 1 phát hiện (#4 P3) |
| D6 nhãn tin cậy / phiên bản | arch **v0.4** khớp changelog v0.4 (áp `D-303` + sửa 14→17); SRS **vẫn `v0.10`** và dòng changelog cuối (1176) **không** có mục cho delta N101 (#3) | 1 phát hiện (#3 P3) |
| D7 14 quy tắc + Nhanh | #7 tiền `decimal` (không `==`); #11 Mermaid — delta không thêm sơ đồ, không `;`/`<...>` trong lưới; mục "Đối chiếu mô hình Nhanh" SRS còn (dòng 1178); `check-feature-doc.ps1 -WP14` trên SRS = **DAT** (16/16 heading · 36/36 BR có test · có Đối chiếu) | 0 phát hiện mới |

### Phát hiện (delta vòng 3)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | `PRO-001-arch.md` §12-**D13** (dòng 775) ↔ SRS §6.1 (461-462, 478-480) | D13 vẫn nói "SRS §6.1 ghi chữ ký **cũ**: `ReserveVoucherDto` **chưa** có `Subtotal`/`Currency`/`TtlSeconds`, `CommitVoucherDto` **chưa** có `CheckoutSessionId`" và để việc sửa cho "chủ `ba` ở `W25-2`" — nhưng **chính delta này** đã cập nhật SRS: bảng 461-462 **khớp đúng** chữ ký D13 mong đợi + bullet 478-480 đã có. Mâu thuẫn chéo tài liệu trong cùng PR. | Đánh dấu **D13 đã đóng ở N101** (SRS đã sửa), hoặc ghi "đã sửa — xem SRS §6.1 N101"; **không** để trạng thái "chờ `ba`". |
| 2 | P2 | SRS §6.1 (478) · arch §4.2 (`decimal Subtotal`, dòng 304) · §6.1 bước 1 (436) · `D-304` ①(5) | `Subtotal` khai **`decimal` (non-nullable) "bắt buộc"**, nhưng `[Required]` **không có tác dụng** trên kiểu giá trị và tài liệu **không** nêu `[Range]`/guard nào ⇒ `Subtotal = 0` (mặc định khi thiếu) **lọt qua**, trong khi `D-304` yêu cầu `Subtotal = 0 ⇒ 400`. Câu "bắt buộc có" trên `decimal` là vô nghĩa — không có gì đỏ khi `Subtotal = 0`/thiếu. | Nêu cơ chế: `[Range(0.01, …)]` trên `Subtotal` (khuôn `TraHangLineDto.Quantity [Range(1,…)]` — `DonHangDtos.cs:46`) hoặc đổi `decimal?` + `[Required]` + guard NÉM `AbpValidationException`; sửa **cả** SRS 478 và arch 304/436. |
| 3 | P3 | SRS header dòng 15 · changelog dòng 1176 | SRS **vẫn `v0.10`**; dòng lịch sử cuối (`v0.10`) chỉ nói `D-296`/docreview vòng 4 — **không** có dòng cho delta N101 (thêm `Subtotal`/`Currency`/`TtlSeconds`/`CommitVoucherDto.CheckoutSessionId`). Nội dung §6.1 đổi nhưng nhãn phiên bản/changelog không đổi (arch đã bump v0.4). | Bump SRS `v0.11` + thêm dòng changelog cho N101; cập nhật tham chiếu "SRS (v0.10)" trong arch header. |
| 4 | P3 | SRS §6.1 dòng 480 | Dẫn `CheckoutDtos.cs:63-69`; khai báo `CheckoutInput.IdempotencyKey` thực tế ở `CheckoutDtos.cs:67-69` (63-66 là XML doc comment); `D-303` cũng dẫn `67-69`. | Đổi thành `CheckoutDtos.cs:67-69`. |
| 5 | P3 | SRS §6.1 (461-462, 478-480) | Chỉ `CommitVoucherDto.CheckoutSessionId` có bullet khai `string` ≤128 (480); **không** có bullet tương ứng cho `ReserveVoucherDto.CheckoutSessionId` — trong khi arch §4.2 khai **cả hai** = `string` và `D-303` nói **cả hai** là `string`; JSON mẫu lại là chuỗi dạng GUID. | Thêm bullet khai `ReserveVoucherDto.CheckoutSessionId` = `string` ≤128 (đồng bộ arch/D-303); nêu validator định dạng cho `Currency` (3 ký tự IN HOA) vì `D-304` test "sai 3 ký tự ⇒ 400". |

### Vòng trước (khép vòng 2)

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 12 (§1.2/§7.6 "14 dòng map") | **đã đóng** — v0.4 sửa thành **17 dòng `options.Map`** (map đủ `0101…0119` trừ `0109`/`0113`), khớp bảng §7.6 (17 mã) + 14 hằng **mới** + 3 hằng sẵn (`0101`/`0104`/`0119`); nhất quán changelog v0.4 | P3 |

> Vòng 1 (mục 1–11) đã khép ở vòng 2 (xem "Vòng trước (vòng 1)" phía trên); không mở lại. Vòng 3 kiểm **delta** so với v0.4/SRS N101; các mục ngoài delta không rà lại.

---

## Vòng 4 — delta N101 `D-305`/`D-306` (27/11/2026)

> Phạm vi: **delta** mới nhất của N101/W25-2 trên 2 tệp — `PRO-001-arch.md` **v0.6** (áp `D-305`: `CommitVoucherDto` +`OrderDiscountTotal` = **trường thứ 5**, `Promotions` tự so `Σ DiscountAmount` của lượt `Applied` ⇒ `0111`, nhánh replay **vẫn so**, payload khoá **giữ 3 thành phần**; áp `D-306`: job `voucher-expiry` làm **ngay `W25-2`**, khoá `voucher-expiry`/TTL 300 s/lô 500/`requiresNew`/guard `Status=0`/`40P01` thử lại có trần/2 counter, `reservation_expired` rời `ChuaCoNoiPhat`, ETO ra hoãn có chủ, nêu nợ as-built `PromotionExpiryWorker` **chưa** đọc cờ) và `pro-001-voucher-coupon.md` **v0.12** (đồng bộ §1 BR-06/E6, §6.1/§6.2, §7.1, §7.2, §8.1/§8.2/§13, `TC-42`). Căn cứ `D-305`, `D-306` (`docs/nhanh/decisions.md:319-320`). Toàn văn đã `DOC-OK` ở N100/vòng 3; đây là kiểm **delta**, không kiểm lại toàn văn.
>
> **VERDICT: DOC-OK** (vòng 4) — **P1 = 0 · P2 = 0 · P3 = 5** (delta). Verdict của mục này là verdict của lần kiểm này.

### Đã kiểm (delta vòng 4)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đối chiếu cặp câu cùng đối tượng trong delta: arch §4.2/§6.3 bước 1/4 ↔ SRS §6.1 bullet/BR-06/E6/§6.2 `0111` — đều "`Promotions` tự so `Σ`", "replay vẫn so", "`OrderDiscountTotal` cho phép `0`", "**không** trong payload khoá `{RedemptionId, OrderId, Code}`". Job `voucher-expiry` §6.4 ↔ SRS §7.2 #5/§8.2 #4/§13 — khớp từng tham số (khoá `voucher-expiry`, TTL 300 s, lô 500, `requiresNew`, guard `Status=0`, `40P01` thử lại có trần, 2 counter). `reservation_expired` "rời `ChuaCoNoiPhat`" ở **cả hai** ghi là **kế hoạch `W25-2`**, **không** nói "đã có". | 0 phát hiện D1 thuần (xem #4 ngữ nghĩa) |
| D2 chéo tài liệu / `D-xx` / code | `graph: codegraph_explore "CommitVoucherDto VoucherRedemptionService ThucHienCommitAsync VoucherRequestValidator KiemTraCommit PromotionErrorCodes RedemptionSumMismatch"` ⇒ `CommitVoucherDto` **4** trường (`IVoucherRedemptionService.cs:46-52`); `ThucHienCommitAsync(CommitVoucherDto input,…)` `:429` (**có** `input` ⇒ khối so `Σ` chèn được); `KiemTraCommit` `:60-70` **nay chỉ** kiểm `CheckoutSessionId` (khớp câu "nay chỉ kiểm"); `RedemptionSumMismatch` = `0111` `:79-80`. `PromotionIdempotencyKeys.Commit` trả `PromotionCommit:{host}:{orderId}:{code}` (`PromotionLockKeys.cs:56-58`); payload as-built **3** thành phần `{RedemptionId, OrderId, Code}` (`VoucherRedemptionService.cs:387`). `DonHangAppService.cs:111-113` (`SetDiscount`/`SetTotals`/`Recalculate`), gọi `CommitAsync` `:118-130`, tripwire `:133` — khớp `D-305` ①(2). `PromotionOptions` có `JobPeriodMinutes`/`ExpiryJobName`/`MaxUsageSequenceAttempts`, **0** option TTL/lô (khớp arch §1.1). | 1 phát hiện (#1 P3 — trỏ SRS `v0.11` khi SRS `v0.12`) |
| D3 cơ chế thực thi | `0111` = `new BusinessException(PromotionErrorCodes.RedemptionSumMismatch)` trong `ThucHienCommitAsync` **sau** `UPDATE` lượt→`Applied` cùng transaction — có lớp + điểm gọi thật; nhưng hai vế đều `decimal` **không `Currency`** nên quy tắc "so bằng toán tử `Money`, cấm `==`" (`BR-08`) **không áp nguyên văn** (#4). Guard `OrderDiscountTotal < 0` ở `KiemTraCommit` **trước mọi `UPDATE`** — có cơ chế; "thiếu ⇒ 400" là **dead** trên `decimal` non-nullable (#5). Job `40P01` "thử lại **có trần**" nhưng **không** số/option/test (#3). | 3 phát hiện P3 (#3, #4, #5) |
| D4 SQL chạy được | Delta **có 1 khối `sql` mới** (arch §6.3 bước 4: `SELECT COALESCE(SUM("DiscountAmount"),0) … WHERE … "Status"=1`). **Chưa chạy**: máy dev **không có** binary `psql` (không ở PATH, `C:\Program Files\PostgreSQL` không tồn tại) dù `docker compose` có `postgres:17-alpine` healthy; shell vai `docrev` chỉ cho `psql … BEGIN…ROLLBACK` trực tiếp, không `docker exec`. Đã xác minh **cú pháp/tên cột/giá trị enum** bằng code: cột `DiscountAmount`/`OrderId`/`TenantId`/`Status` có thật (`PromotionRedemption.cs`); `PromotionRedemptionStatus.Applied = 1` (`PromotionEnums.cs:51-52`) ⇒ `"Status" = 1` đúng. **0** DDL/`ALTER`/`INSERT` mới trong delta. | 0 phát hiện (cú pháp đúng); **chưa chạy** |
| D5 test/probe/tệp có thật | `VoucherExpiryWorker` **chưa có** (`Workers/` chỉ `PromotionExpiryWorker.cs`) — tài liệu ghi rõ "`W25-2` **tạo**"/kế hoạch, **không** nói "đã có" (đúng). `MetricEmitterTests` có (`test/eCommerce.Tests/Observability/MetricEmitterTests.cs:30`). `PromotionVoucherReleased.Add` `:549`, `PromotionVoucherReserved.Add` `:591` (điểm phát thật). `TC-05/16/18/39/42/60/62` đều có ở SRS §14.1 (`:884/895/897/919/922/937/939`). Nợ as-built `PromotionExpiryWorker` **chưa** đọc cờ: `grep "EnabledSettingName|Promotions:Enabled"` trong worker = **0** ⇒ khai "nợ `PRO-002`" **đúng**. | 1 phát hiện P3 (#2 — `:84-122 (DoWorkAsync)` lệch mốc dòng) |
| D6 nhãn tin cậy / phiên bản | Header arch **v0.6** ↔ SRS **v0.12** khớp changelog mới; nhưng (a) arch header dòng 11 **vẫn** trỏ SRS `v0.11` (#1); (b) bảng lịch sử phiên bản arch (§14, dòng 975-979) dừng ở **v0.5**, **thiếu dòng v0.6**. `✅` "reserved/released đã có điểm phát" đúng as-built; `reservation_expired` giữ nhãn **nợ** đúng. | 2 phát hiện P3 (#1, #2) |
| D7 14 quy tắc + Nhanh | #1 BR kiểm chứng được (`TC-39`+`NC`); #2 NFR có ngưỡng (NFR-04 ≥1000 phút/0 treo >2×TTL); #5 chống trùng mọi cửa vào (job quét theo trạng thái + guard `Status`, ghi rõ); #6 tên khoá `promotion:job:voucher-expiry:{tenant}`; #7 tiền `decimal` (xem #4); #11 Mermaid — delta không thêm sơ đồ; #12/#13 không `<...>`/thẻ HTML lạ; `check-feature-doc.ps1 -WP14` trên **SRS** = **DAT** (16/16 · 36/36 BR có test · có Đối chiếu); `-arch` = 3 LOI **cấu trúc** — `-arch` đã khai ngoài mẫu 16 mục (N/A, không phải phát hiện). Mục "Đối chiếu Nhanh" SRS đã thêm 2 dòng `D-305`/`D-306`. | 0 phát hiện mới |

### Phát hiện (delta vòng 4)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | `PRO-001-arch.md` header dòng 11 ↔ SRS header dòng 15 | Arch **v0.6** khai "chỉ thiết kế quanh SRS (`v0.11 — 27/11/2026`)", nhưng SRS đã bump **v0.12** (chứa chính `D-305`/`D-306` mà arch v0.6 nói "áp"); SRS v0.12 lại khai "đồng bộ `PRO-001-arch.md` v0.6" ⇒ trỏ chéo **lệch phiên bản**. Bằng chứng: `grep "v0.11"` arch = dòng 11 (còn), SRS = `v0.12`. | Đổi dòng 11 thành `v0.12 — 27/11/2026` (hoặc "SRS `v0.12`, đồng bộ cùng delta v0.6"). |
| 2 | P3 | `PRO-001-arch.md` §14 (dòng 975-979) | Header bump **v0.6** nhưng bảng lịch sử phiên bản chỉ có `v0.1…v0.5` — **thiếu dòng v0.6** ghi `D-305`/`D-306`; nội dung delta không được ghi vào changelog như mọi vòng trước. | Thêm `v0.6 \| 27/11/2026 \| ARCH (N101/W25-2) \| Áp D-305 + D-306 …`. |
| 3 | P3 | `PRO-001-arch.md` §1.1 (dòng ~45) · §6.5 | Dẫn "`PromotionExpiryWorker.cs:84-122` (`DoWorkAsync`)" — `DoWorkAsync` thực tế bắt đầu **`:50`**, kết thúc **~`:124`**; `:84-122` chỉ là **thân vòng tenant** bên trong. SRS §13 cùng đối tượng dẫn đúng `:50-125` ⇒ hai tài liệu lệch mốc dòng cho cùng method. | Đổi arch thành `:50-124` (`DoWorkAsync`) hoặc ghi "`:84-122` (vòng tenant trong `DoWorkAsync`)". |
| 4 | P3 | `PRO-001-arch.md` §6.3 bước 4 · SRS BR-06 | Câu "So **bằng phép so `decimal`**, **cấm** `==`" mâu thuẫn nội tại: hai vế đều là `decimal` trần (`Σ DiscountAmount` và `OrderDiscountTotal` — DTO **không** có `Currency`), nên **không** dùng được toán tử `Money` như `BR-08` yêu cầu; không nêu cơ chế cụ thể (as-built dùng `decimal.Compare(…)` ở `DonHangAppService.cs:126`). | Nêu rõ: "so bằng `decimal.Compare(a,b) != 0`" (như `DonHangAppService.cs:126`) hoặc thêm `Currency` cho `OrderDiscountTotal` để so qua `Money`. |
| 5 | P3 | `PRO-001-arch.md` §6.4 · SRS §7.2 #5/§8.2 #4 | `40P01` "bắt ⇒ thử lại **có trần**" nhưng **không** nêu số lần trần, **không** map option/hằng (`PromotionOptions` không có), **không** có test/NC cho trần đó (`D-306` ⑥ liệt kê `MetricEmitterTests`/ca cờ/thứ tự/NC — **không** có ca trần `40P01`). "Có trần" không đo được ⇒ người code chọn tuỳ ý. | Nêu số cụ thể (vd ≤ 3 lần/lô) + hằng/option + ca âm (ép `40P01` tới lần trần ⇒ dừng + log + metric). |

### Vòng trước (khép vòng 3)

> Vòng 3 (`DOC-CHANGES`) đã đóng **cả 5** mục qua v0.5/SRS v0.11–v0.12; nội dung không mở lại. Chi tiết lịch sử ở mục "Vòng 3" phía trên.

| # vòng 3 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (D13 báo SRS chữ ký cũ) | **đã đóng** — D13 v0.5+ chuyển "đã đóng một phần ở N101", không còn nói SRS cũ | P2 |
| 2 (`Subtotal` không `[Range]`) | **đã đóng** — `ReserveVoucherDto.Subtotal` có `[property: Range(typeof(decimal),"0.01",…)]` (`IVoucherRedemptionService.cs:37`) | P2 |
| 3 (SRS chưa bump — v0.10) | **đã đóng** — SRS nay **v0.12** + changelog N101 | P3 |
| 4 (`CheckoutDtos.cs:63-69`) | **đã đóng** — SRS §6.1 nay dẫn `67-69` | P3 |
| 5 (thiếu bullet `ReserveVoucherDto.CheckoutSessionId`) | **đã đóng** — SRS §6.1 có bullet `string` ≤128 | P3 |
