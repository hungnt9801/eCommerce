# Doc review — PRO-002 Cây điều kiện AND/OR & engine khuyến mãi

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (**v0.15**, SRS — tài liệu chính) · đối chiếu `docs/features/promotions/PRO-002-arch.md` (**v0.8**) |
| Ngày · vòng | 27/11/2026 · **vòng 8** (N95 — **đóng P2 còn lại** của vòng 7 sau bản vá `v0.15`; không kiểm lại toàn tài liệu) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | SRS dòng 1–20 (header), 600–615 (§6.2), 635–655 (§6.3 v1), 840–860 (§10.1), 1040–1055 (`TC-70`/`TC-71`), 1150–1155 (DoD), 1266–1275 (§15.4); ARCH dòng 1–30 (header), 295–315 (§4.3), 345–360 (§4.4), 692–698 (§15); code `PromotionRuleCostSeedContributor.cs:1-40` (đồ thị) |
| Máy | `check-feature-doc.ps1 -Path … -WP14` → **DAT** (16/16 heading · 20/20 BR có test · đối chiếu Nhanh dòng 1276 · 15 câu `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → **214 khối, 0 lỗi** · `check-agent-artifact.ps1 -Kind docreview` → **exit 0** |

**P1 = 0 · P2 = 0 · P3 = 4** (P3 ghi nhận, không chặn)

Tóm tắt vòng 8 — xác nhận **đúng 3 điều** của bản vá `v0.15`:
1. **§14.1 `TC-70` (d) (:1049)** nay là "`Label` **khác rỗng VÀ khác `SystemName`**", cột kỳ vọng giải thích đúng vì sao (fallback làm "khác rỗng" luôn đúng, phải kiểm **cả hai vế**, `D-290` ⑥(iv)) ⇒ **đóng P2 vòng 7 #1**.
2. **§14.1 có `TC-71` (:1050)** phủ đúng `D-289` ⑥(i)–(iii) + `D-290` ⑥(iv)–(v): (i) bảng chi phí rỗng ⇒ đủ `SystemNames`, `1`/`false`/`IsSeeded = false`, không ném `0010`, 0 tên bị bỏ; (ii) có dòng ⇒ `IsSeeded = true` + giá trị theo dòng; (iii) sửa nhãn trong `PromotionRuleCostCatalog.V1` ⇒ trả theo bảng code; (iv) **đột biến** duyệt DB/`continue` ⇒ ca (i) **ĐỎ**; (v) nhãn v1 khác rỗng và khác `SystemName`, tên ngoài bảng ⇒ `Label = SystemName`. Cột **Phủ** neo đúng "**hàng `W24-4` — QA**".
3. **§14.3/DoD (:1152)** ghi **57** test case = `grep -c '^| \*\*TC-'` = **57**; **§6.3 (:639)** ghi `openapi.v1.json` **59** path = số đo lại (59 key `/api/v1`, **0** path promotion).

Còn 4 **P3** (đều ở `PRO-002-arch.md` hoặc câu chữ test), không chặn: `TC-71` (v) nên tách vế "4 tên v1" / "tên ngoài bảng"; ARCH §4.4 vẫn "31 path"; ARCH header còn trích SRS `v0.14`; ARCH §4.3 chưa ghi "thêm trường `Label`".

## Vòng 8 (N95) — đóng P2 còn lại

### Đã kiểm (vòng 8)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep `khác rỗng`/`khác \`SystemName\``/`TC-70`/`TC-71`: vế "chỉ khác rỗng" **không còn** ở chỗ nào cho nhãn — `TC-70` (d) (:1049) và `TC-71` (v) (:1050) đều nêu hai vế; `TC-70` (d) và `TC-71` (v) bổ trợ (endpoint ↔ service/bảng code), không đếm trùng route/method (vẫn 7 route, 10 method); `IsSeeded` vẫn **một** định nghĩa. | 1 phát hiện (#1 — câu chữ `TC-71` (v)) |
| D2 chéo tài liệu / code / D-xx | `TC-71` ↔ `D-289` ⑥(i)–(iii) + `D-290` ⑥(iv)–(v): phủ **đủ**, không thêm ràng buộc mới; `TC-71` neo `W24-4`. Số đo: `grep -c '^| \*\*TC-'` = **57** = DoD (:1152); `grep -c '^    "/api/v1' packages/api-client/openapi.v1.json` = **59**, promotion = **0** = §6.3 (:639). §6.2 (:609) / §10.1 (:847, :856) giữ nguồn nhãn `PromotionRuleCostCatalog.Meta.Label` + fallback `SystemName`; `PromotionRuleCostCatalog` :13-27, `Meta` :15 (3 trường, `Label` **additive** — đúng như SRS :856 ghi "trường additive, thêm ở `W24-3`"). | 0 phát hiện |
| D3 cơ chế thực thi | `TC-70` (d) **đỏ được** khi `Label = SystemName` (fallback) — đúng ca "thêm rule vào registry mà quên `V1`" (`D-290` ⑥(iv)); `TC-71` (iv) **đỏ được** khi đổi vòng lặp sang DB/`continue` (`D-289` ⑥(iii)); `TC-71` (iii) **đỏ được** khi nhãn đọc từ DB thay vì bảng code (`D-290` ⑥(v)). Mỗi bất biến của 2 quyết định có **≥ 1 ca đỏ**. | 0 phát hiện |
| D4 SQL chạy được | Bản vá `v0.15` là văn bản; `grep -c '```sql'` trên 2 tệp = **0/0**; không có khối `bash`/`powershell` mới. | **Không có khối sql** — không chạy |
| D5 test/probe/tệp có thật | `TC-71` / `PromotionV1SurfaceTests` / `PromotionRuleCatalogTests` **chưa có tệp** — là **kế hoạch** của `W24-3`/`W24-4`, tài liệu ghi đúng (TC là danh sách kế hoạch, không nói "đã có"); `PromotionRuleCostCatalog` **có thật** (:13-27). | 0 phát hiện |
| D6 nhãn tin cậy | SRS header `v0.15` (:15) + hàng `v0.15` §15.4 (:1272) khớp bản vá; "Trạng thái triển khai" :1309 "Phiên **N95** (`W24-3`)". ARCH header (:4) vẫn trích SRS `v0.14`; ARCH §4.4 (:351) vẫn "31 path" — **P3 #2/#3**. | 2 phát hiện (#2, #3) |
| D7 14 quy tắc + đối chiếu Nhanh | `check-feature-doc -WP14` **DAT** (đối chiếu Nhanh dòng 1276); mermaid 214/0; "đếm ≠ dòng hiển thị" xử lý tường minh; không thêm bảng Mermaid. ARCH §4.3 (:303) chưa ghi "thêm trường `Label`" dù SRS :856 đã ghi — **P3 #4**. | 1 phát hiện (#4) |

### Phát hiện (vòng 8)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | SRS §14.1 dòng **1050** (`TC-71` (v)) | Cột kỳ vọng (v) viết "**mọi tên** có mặt với `Label` khác rỗng **và** khác `SystemName`; tên ngoài bảng ⇒ `Label = SystemName`" — nếu tên "ngoài bảng" vẫn ∈ `SystemNames` (ca đột biến) thì hai vế **đá nhau** (vừa phải khác `SystemName`, vừa phải bằng). Ý đúng (4 nhãn v1 ⇔ ⊂ `V1`) đọc ra được nhưng dễ viết sai test. | Tách câu: "4 tên **∈ `V1`** ⇒ `Label` khác rỗng **và** khác `SystemName`; tên **∉ `V1`** (dù ∈ registry) ⇒ `Label = SystemName` **và** `TC-70` (d) **ĐỎ**". (Câu chữ; không mở vòng mới.) |
| 2 | P3 | ARCH §4.4 dòng **351** | (Còn mở từ vòng 7 #2 — chỉ còn ở ARCH) "`openapi.v1.json` **31** path" trong khi SRS §6.3 (:639) đã sửa **59** = số đo. | `arch` đổi 31 → **59** (hoặc bỏ con số) cho khớp SRS. |
| 3 | P3 | ARCH header dòng **4** | Vẫn trích SRS `v0.14 — 27/11/2026`; SRS nay `v0.15`. | `arch` cập nhật `v0.15`. |
| 4 | P3 | ARCH §4.3 dòng **303** | Hàng "Nguồn nhãn" trích `PromotionRuleCostCatalog.Meta.Label` kèm `file:dòng` nhưng **không** nói `Meta` **thêm** trường `Label` (hiện 3 trường, `:15`); SRS :856 đã ghi "trường additive, thêm ở `W24-3`". Không phải mâu thuẫn (additive, `D-290` ①(1)). | `arch` thêm "(`Meta` **thêm** trường `Label` — additive, `W24-3`)". |

### Không kiểm được (vòng 8)

- **D4:** không có khối `sql` trong 2 tệp (`grep -c '```sql'` = 0) ⇒ không có gì để chạy trong `BEGIN…ROLLBACK`.
- **Phần as-built:** `TC-70`/`TC-71`/`PromotionV1SurfaceTests`/`PromotionRuleCatalogTests`/`IPromotionRuleCatalogAppService`/`IsSeeded`/`Meta.Label` **chưa có trong code** — là kế hoạch `W24-3`/`W24-4`; tài liệu ghi đúng trạng thái kế hoạch.
- Vòng 8 chỉ **kiểm máy nhanh 3 điều** của bản vá; các mục ngoài delta giữ kết luận vòng 7/6/5.

### Bằng chứng đồ thị / đo (vòng 8)

- `graph: codegraph_explore "PromotionRuleCostCatalog PromotionRuleCostSeedContributor PromotionRuleRegistry PromotionRuleCost Cua Meta"` → `PromotionRuleCostCatalog` :13; `Meta(string CostKind, int DeclaredCostFactor, bool RequiresVerifiedCod)` :15 (**3** trường, **chưa** `Label`); `V1` :17-23 (4 rule); `Cua` :25-26; `PromotionRuleCost` :12-15 (không cột nhãn); `SystemNames` :39-40.
- `grep -c '^| \*\*TC-'` = **57** ↔ DoD :1152 "**57** test case" (khớp); `TC-71` :1050 có mặt, cột Phủ "hàng `W24-4` — QA".
- `grep -c '^    "/api/v1' packages/api-client/openapi.v1.json` = **59**, promotion = **0** ↔ SRS §6.3 :639 "**59** path"; ARCH §4.4 :351 còn "31".
- `grep -n "TC-70"` → (d) :1049 "khác rỗng **VÀ** khác `SystemName`"; không còn câu chỉ "khác rỗng" cho nhãn.

## Vòng trước (vòng 7 — kiểm lại 2 P2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 7 | Trạng thái (vòng 8) | Mức lúc đó | Ghi chú |
|---|---|---|---|
| 1 | **đã đóng** | P2 | `TC-70` (d) :1049 nay "khác rỗng **VÀ** khác `SystemName`" + `TC-71` :1050 phủ `D-289` ⑥(i)–(iii)/`D-290` ⑥(iv)–(v), neo `W24-4` |
| 2 | đóng ở SRS, **còn ở ARCH** | P3 | SRS §6.3 :639 = 59; ARCH §4.4 :351 còn 31 → chép lên bảng "Phát hiện" **#2** vòng 8 |
| 3 | **đã đóng** | P3 | DoD :1152 = **57** = đếm thật (56 + `TC-71`) |
| 4 | đóng ở SRS, **còn ở ARCH** | P3 | SRS :856 "trường additive, thêm ở `W24-3`"; ARCH §4.3 :303 chưa → **#4** vòng 8 |

### Vòng 7 — Đã kiểm (giữ nguyên, tóm tắt)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep `khác rỗng`/`IsSeeded`/`PromotionRuleCatalog`: `IsSeeded` một định nghĩa ở cả hai; fallback nhãn nói cùng một cách; còn `TC-70` (d) "khác rỗng" lệch ARCH "khác `SystemName`". | 1 phát hiện (#1) |
| D2 chéo tài liệu / code / D-xx | `PromotionRuleCostCatalog` :13-27, `Meta` :15 (3 trường), `Cua` :25-26; `PromotionRuleCost` :12-15; SRS/ARCH khớp `D-289`/`D-290`. | 1 phát hiện (#4) |
| D3 cơ chế thực thi | `D-289` ⑥ ⇒ `PromotionRuleCatalogTests` (i)-(iii); `D-290` ⑥ ⇒ (iv)-(v); SRS không có TC ⇒ `TC-70` (d) không đỏ được. | 1 phát hiện (#1) |
| D4 SQL chạy được | `grep -c '```sql'` = 0/0. | Không có khối sql |
| D5 test/probe/tệp có thật | `PromotionRuleCostCatalog` có; `PromotionRuleCatalogTests`/`IsSeeded` chưa có (kế hoạch); `AQ11` chỉ còn hàng lịch sử `v0.8`. | 0 phát hiện |
| D6 nhãn tin cậy | Header SRS v0.14 ↔ ARCH v0.8; §6.2/§10.1 ghi kế hoạch; "Trạng thái triển khai" N95; DoD 55 vs 56. | 1 phát hiện (#3) |
| D7 14 quy tắc + đối chiếu Nhanh | `check-feature-doc -WP14` DAT; mermaid 0; còn "openapi 31"+`Meta.Label`. | 2 phát hiện (#2, #4) |

### Vòng 7 — Không kiểm được / Bằng chứng (giữ nguyên)

- **D4:** không có khối `sql`; `CHECK`/index ngoài delta.
- **`IPromotionRuleCatalogAppService`/`GetRulesAsync`/`IsSeeded`/`Meta.Label`:** chưa có trong code (đồ thị) — kế hoạch `W24-3`.
- `grep -rn "AQ11" docs/features/promotions/` → **1** hit `PRO-002-arch.md:696` (hàng lịch sử `v0.8`).
- `PromotionRuleCostSeedContributor.cs:19-22` = 4 `new("O1", 1, false)`.

## Vòng trước (vòng 6 — kiểm delta W24-3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 6 | Trạng thái (vòng 7) | Mức lúc đó | Ghi chú |
|---|---|---|---|
| 1 | đã đóng (vòng 7) | P2 | `AQ11` gỡ khỏi ARCH §4.3; chỉ còn hàng lịch sử `v0.8` |
| 2 | còn một phần (vòng 7) → đã đóng (vòng 8) | P2 | Nguồn nhãn nêu đích danh; cơ chế đỏ sửa ở v0.15 (`TC-70` (d) + `TC-71`) |
| 3 | đã đóng (vòng 8) | P3 | openapi 59 ở SRS (:639) |
| 4 | đã đóng (vòng 6) | P3 | ARCH §4.4 :355 "phải THÊM `ProjectReference` … hiện chưa có" |
| 5 | đã đóng (vòng 6) | P3 | §6.2 :611 "của `IPromotionAppService` — hàng `rule-catalog` là service riêng" |
| 6 | đã đóng (vòng 6) | P3 | "Trạng thái triển khai" N95 (:1309) |
| 7 | đã đóng (vòng 6) | P3 | §10.1 dùng `{id:guid}` |

### Vòng 6 — Không kiểm được / Bằng chứng (giữ nguyên)

- **D4:** delta không có khối `sql`.
- **`PromotionV1SurfaceTests`/`PromotionTreeLimitsDto`:** chưa có trong code — kế hoạch, có nhãn 🔶.
- `graph: codegraph_explore "IPromotionAppService PromotionAppService GetAsync DocLimitsAsync PromotionDetailDto RedemptionDto"` → 10 method :10-31; `PromotionDetailDto` :59-62; `RedemptionDto` :196-210.
- `graph: codegraph_explore "PromotionTreeValidator TinhDeclaredCost …"` → `0001` :51 / `0002` :64; `TinhDeclaredCost` :94-104; `PromotionSettings.Doc` :33-63.
- Mở dòng: `PromotionRuleContracts.cs:40-45`; `PromotionSurfaceTests.cs:36-49`; `ProductApiDtos.cs:9`; `NotificationTemplatesController.cs:28-38`; Host :449-458, :486-500; `SettingsController.cs` 14 route; `openapi.v1.json` 59 path; FE `order/manage/[id]/page.tsx:177`.

## Vòng trước (vòng 5 — F1b N94, kiểm nhanh)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 5 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — header SRS `v0.15`; ARCH trích `v0.14` (P3 vòng 8 #3) | P3 |
| 2 | đã đóng — trích số dòng `pro-001` §6.1 đổi sang tên mục | P3 |
| 3 | đã đóng — cờ `Promotions:Enabled` | P3 |
| 4 | đã đóng — `REP-001 §5.2.1 LineDiscount` | P3 |

### Vòng 5 — Phát hiện (giữ nguyên)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | Header dòng 15 | Header còn `v0.9`; ARCH trích `v0.9`. | Cập nhật; đã đóng. |
| 2 | P3 | `pro-001` §6.1 :477/:492 | Còn 2 trích số dòng cũ. | Đổi sang tên mục; đã đóng. |
| 3 | P3 | §11 :865 | Cờ đặt tên kiểu permission. | Đổi `Promotions:Enabled`; đã đóng. |
| 4 | P3 | §7.1 :683 | `rep-001:442` không khớp. | Đổi `REP-001 §5.2.1 LineDiscount`; đã đóng. |

## Vòng trước (vòng 4 — kiểm nhanh, 2 P3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 4 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — DoD tăng thật lên 57 | P3 |
| 2 | đã đóng (vòng 5 #2) | P3 |

## Vòng trước (vòng 3 — 4 phát hiện bắt buộc + 5 P3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Mức lúc đó để ở cột 3. -->

| # (v3) | Trạng thái | Mức lúc đó | Bằng chứng đóng (vòng 4) |
|---|---|---|---|
| 1 | đã đóng | P1 | §4.2 bước 2 (:244); A2 (1) (:103); §6.1 quy tắc 8 (:580); §5.2 `Kind` (:382); `TC-67` (:996) |
| 2 | đã đóng | P2 | §7.2 #5 (:682), §8.2 #5 (:743), bảng khoá (:771), `TC-50` (viii) (:993) |
| 3 | đã đóng | P2 | §6.1 quy tắc 8 (:580) + §3.3 `LoiCauHinh` (:191); §12 (:862); `TC-68` (:997); `Q28` (:1175) |
| 4 | đã đóng | P2 | `CustomerGroupId`/`IsFirstOrder` có nguồn (:519-520); quy tắc 10/11 (:582-583); `Q27` `Closed` (:1174); `TC-69` (:998) |
| 5 | đã đóng | P3 | §14.3 ghi đúng số test case khi đó |
| 6 | đã đóng | P3 | Basic Flow 1 (:102) + §4.2 bước 1 (:243); `IClock` một lần |
| 7 | đã đóng | P3 | §0 :61 phạm vi quầy `RetailBill` rõ ràng |
| 8 | đã đóng | P3 | §6.1 quy tắc 4 (:576) kiểm `Mode` hai chiều |
| 9 | đã đóng (vòng 5 #2) | P3 | delta `pro-001` :467,:469 đổi sang tên mục |

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — `D-160` đã áp (A2 (2a)/(5), BR-11, enum `= 10`, `Subtotal` :517, `TC-48`/`TC-49`) | P2 |
| 2 | đã đóng — candidate tự động hết lượt ⇒ bỏ qua + `UsageLimitHit` (Basic Flow 6, `TC-51`) | P2 |
| 3 | đã đóng — quy tắc 8 (:575); BR-07, §7.4 #4; `TC-07` (c) | P2 |
| 4 | đã đóng — `UsageSequence` (Basic Flow 7, `TC-43` (iv)–(vi), `D-161`) | P2 |
| 5 | đã đóng — `promotion-expiry` kiêm kích hoạt (§7.2 #5, §8.2 #5, `TC-50`) | P2 |
| 6 | đã đóng — khoá lưu cây có `TreeVersion` (:673, :762), `TC-28` (f) | P2 |
| 7 | đã đóng — route `POST /api/app/promotion` (:608) | P3 |
| 8 | đã đóng — route theo quy ước ABP + 🔶 chưa đo (:609-617) | P3 |
| 9 | đã đóng — `TC-13` (a)–(d); (a) đổi `Percentage` 100 % | P3 |
| 10 | đã đóng — §10.2 :813 lấy từ `EvaluateAsync` | P3 |
| 11 | đã đóng — dòng trống trước `**Idempotency key**` (:754) | P3 |
| 12 | đã đóng — record `PromotionBreakdownItem` (:525-530) | P3 |
