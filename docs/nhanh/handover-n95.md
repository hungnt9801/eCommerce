# Handover N95 — WP-24 / hàng `W24-3` (F3 FE) XONG — **mở F4 QA + F5 REV (`W24-4`)**

> Phiên `N95` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W24-3` · WP-24 · mã `PRO-002` · module `Promotions` ·
> nhánh `feat/gd3-wp24-dieu-kien-km` (base `origin/tich-hop/gd3` @ `84157a1`; đỉnh trước phiên `6a1cbe7`) · 27/11/2026.
> Phạm vi phiên: **F0** mở phiên + probe tài liệu → **F1** `ba` (SRS v0.11 → **v0.15**, 4 lượt) → **F2** `arch` (v0.6 → **v0.8**) →
> **F1b** `docrev` 3 vòng kiểm delta (**`DOC-OK` P1=0/P2=0**) → **F3 BE** `be` mở bề mặt `/api/v1/promotion/**` **7 route** +
> `rule-catalog` + `PromotionDetailDto.Limits` → **F3 FE** `fe` sinh lại snapshot (59 → **75** path) + **3 màn** §10.1 + **15 test** →
> **G3** `kiem-tat-ca.ps1` **exit 0** → **NC-N95-1** đỏ→xanh.

## 1. Đã xong

**Cổng tài liệu + quyết định (F1 → F2 → F1b)**

- **Phát hiện gốc (đo, không đoán)**: SRS §6.3 chỉ khai `/api/app/promotion/**` (auto-controller ABP); FE chỉ gọi `/api/v1/**`
  (`packages/api-client/scripts/gen.mjs:38` lọc đúng tiền tố `/api/v1`) ⇒ `schema.d.ts` **0** path promotion, `openapi.v1.json`
  **0** path promotion ⇒ **FE không có hợp đồng để code**. `decider` chốt **`D-285`** (7 route v1 + `rule-catalog`), **`D-286`**
  (§10.2 ngoài phạm vi hàng — nợ `SAL-002`/`WEB-*`), **`D-287`** (thứ tự `ba`→`arch`→`docrev`→`be`→`fe`), **`D-288`**
  (`PromotionDetailDto.Limits` additive — giữ đúng 7 route), **`D-289`** (`rule-catalog` khi thiếu dòng seed: mặc định + `IsSeeded`
  + log, **không** ném `0010`), **`D-290`** (nhãn tiếng Việt ở `PromotionRuleCostCatalog.Meta.Label`, không đụng `D8`).
- **`ba`** ×4: SRS **v0.11 → v0.15** — §6.2 (`IPromotionRuleCatalogAppService`, `IsSeeded`, nguồn nhãn đích danh), §6.3 (bảng
  `/api/v1/promotion/**` 7 route + `Limits` + 4 op không mở), §10.1 (map UI → op; đồng hồ giới hạn dùng `detail.limits`; **cấm**
  hằng số `64/8/32/1024`; định nghĩa số đếm client; bắt buộc tải lại khi `0001`/`0002`), §10.2 (nợ có chủ), §14.1 (`TC-70` (d)
  sửa + **`TC-71`**), §14.3 (57 test case), §15.1 (`Q30` → **Closed**), §Trạng thái triển khai (N95).
- **`arch`** ×2: `PRO-002-arch.md` **v0.6 → v0.8** — §4.3 (service thứ hai, allowlist 10 method giữ nguyên; `Limits`; `IsSeeded`;
  nguồn nhãn), §4.4 (bề mặt `/api/v1` 7 route, giữ `/api/app` 10 route + bảng map mã→HTTP; 4 op không mở kèm chủ nợ), §12.1
  `docDeviation` **D11**, `ProjectReference` "**phải thêm**" (đã kiểm: trước phiên **chưa có**).
- **`docrev`** ×3 vòng trên delta: vòng 6 **DOC-CHANGES P1=0/P2=2** (ARCH dẫn `PRO-002-AQ11` không tồn tại; nguồn nhãn không đích
  danh) → vòng 7 còn **P2=1** (`TC-70` (d) "khác rỗng" luôn đúng) → vòng 8 **`DOC-OK` P1=0/P2=0** (4 P3, không chặn).

**F3 BE — bề mặt `/api/v1`**

- `src/eCommerce.HttpApi/Controllers/PromotionsController.cs` (**mới**): `[Route("api/v1/promotion")]`, `[Authorize(Promotions.Default)]`
  cấp class, **đúng 7 route** — list `ApiPage<PromotionListItemDto>` · `rule-catalog` · detail (`Nodes` + `Limits`) · create ·
  save-tree · approve · set-status (chỉ `Paused`/`Active`/`Cancelled`); chỉ inject app service, 0 repository/DbContext.
  Route literal `rule-catalog` khai trước `{id:guid}`.
- `src/eCommerce.HttpApi/Controllers/PromotionApiSupport.cs` (**mới**): query + phân trang (mặc định 50 / trần 200).
- `IPromotionRuleCatalogAppService` + `PromotionRuleCatalogItemDto` (6 trường, có `IsSeeded`) + `PromotionRuleCatalogAppService`
  (lặp `PromotionRuleRegistry.SystemNames`, thiếu dòng ⇒ `PromotionRuleCostCatalog.Cua` + `IsSeeded=false` + `LogWarning` 1 lần/lượt,
  **không** ném `0010`, 0 metric).
- `PromotionRuleCostCatalog.Meta` thêm **`Label`** (4 nhãn v1); `PromotionTreeLimitsDto` + `PromotionDetailDto.Limits` gán từ
  `DocLimitsAsync()`; `eCommerce.HttpApi.csproj` +1 `ProjectReference` (`.Contracts`).
- **Không** đổi `IPromotionAppService` (vẫn **10** method), **0** migration, **0** đụng `Sales`/`Pos`.

**F3 FE — 3 màn §10.1**

- `pnpm spec:snapshot`: **59 → 75** path (+16: **6** path promotion = 7 operation, + **10** path `/api/v1/reports/*` vận hành đã có
  trong code từ `W26-2` mà snapshot cũ chưa cập nhật — nợ `D-276`; **0** path bị xoá); `pnpm gen:api` 2 lần ⇒ hash giống hệt
  (**drift = 0**); `schema.d.ts` **additive** (9 key `eCommerce.Pricing.Dtos.*` chỉ **di chuyển vị trí**, không xoá kiểu nào;
  `admin-web` không dùng các kiểu đó).
- Màn: `/[business]/promotion` (danh sách, "Đã dùng" **cạnh** "Trần lượt", lọc + phân trang) · `/promotion/new` (ẩn `FlashSale`
  + `FreeShipping`, lỗi theo mã) · `/promotion/[id]` (dựng cây AND/OR, nhãn từ `rule-catalog`, banner `isSeeded=false`, nhãn COD,
  **đồng hồ giới hạn** lấy trần **duy nhất** từ `detail.limits`, khoá Lưu, cảnh báo 80 %/100 %, tải lại bắt buộc khi `0001`/`0002`);
  `loading.tsx` skeleton 2 màn; menu "Khuyến mãi" (`Promotions.View`); `lib/promotion.ts` + **`lib/promotion.test.ts` (15 ca)**.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** · 74,17 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` | 0 | **2419/2419** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 288 tệp · 975 link · 1734 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` (điều kiện vào) | 0 | **XANH** |
| `check-feature-doc.ps1 -Path <SRS> -WP14` (sau mỗi lượt BA) | 0 | **DAT** · 16/16 mục · 20/20 BR · 57 TC |
| `check-agent-artifact.ps1 -Kind docreview` | 0 | **DAT** · `DOC-OK` **P1=0 · P2=0** |
| `check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 117 dòng |
| `dotnet build` sau F3 BE (ORCH đo) | 0 | **0 error · 9 warning** · 53,75 s |
| `dotnet test test/eCommerce.Tests --no-build` sau F3 BE | 0 | **2419/2419** |
| `pnpm --filter @ecommerce/admin-web typecheck` | 0 | **0 lỗi** |
| `pnpm --filter @ecommerce/admin-web test` | 0 | **60/60** (+15) · 6 tệp |
| `pnpm --filter @ecommerce/admin-web build` | **1** | compile + type-check + static-gen **XANH** (4/4, `BUILD_ID u6-FQ1kYdEKNQD4aqpW_6`); **đỏ ở bước copy standalone** — `EPERM: symlink` (môi trường Windows, `output: standalone`) — **nợ K-05 cũ, không sửa `next.config.ts`** |
| `pnpm lint` (root) | — | **`no-script`**: `None of the selected packages has a 'lint' script` (nợ `N70` cũ) |
| `pnpm gen:api` ×2 | 0 | **không đổi `schema.d.ts`** (drift 0) · 75/75 path |
| **G3** `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 bước · 366,5 s** — build 83,2 s · unit **2419/2419** · integration **139/139** · mermaid **214/0** · cổng tài liệu **XANH** · FE typecheck+Vitest **60/60** 10,6 s · `gen:api` không đổi 2,2 s · DB **25/25** 15 s · test cổng **156,8 s** |
| **NC-N95-1** đột biến `dongHoGioiHan` (`nodeCount > maxNodes` → `+1000`) | 1 → 0 | **ĐỎ** 1 ca (`12 node với trần {10,5,8,64} ⇒ KHOÁ, câu chứa '10'`) → khôi phục (hash khớp) → **XANH 60/60**; `git status` sạch |
| **Đo lại lúc đóng** `dotnet build` | 0 | **0 error · 9 warning** · 63,51 s |
| **Đo lại** `dotnet test test/eCommerce.Tests --no-build` | 0 | **2419/2419** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W24-3`** — DoD F3 FE đạt (G3 exit 0). Nhưng có **một khoảng hở phải đóng ở `W24-4`**, ghi rõ để không mất dấu:

1. **Test BE của bề mặt v1 CHƯA có** — `be` **bị hook `guard-write-scope` chặn** ghi `test/**` (`.claude/scopes.json`: `be` chỉ
   `modules/**`/`src/**`/`shared/**`; `test/**` thuộc `qa`). Ba tệp phải thêm ở `W24-4`: `PromotionV1SurfaceTests` (allowlist
   **đúng 7** route + `[Authorize]` cấp class + map permission + `IPromotionAppService` **đúng 10** method + `rule-catalog` không bị
   `{id:guid}` nuốt), `PromotionRuleCatalogTests` (5 ca của `D-289` ⑥ + `D-290` ⑥), `GetAsync`-`Limits` (2 ca: mặc định
   `64/8/32/1024`; tenant `MaxNodes=10`/`MaxDepth=5`). Hợp đồng đã có sẵn ở `D-285` ⑥ / `D-288` ⑥ / `D-289` ⑥ / `D-290` ⑥ và
   `TC-70`/`TC-71` (SRS §14.1) — **chỉ thiếu người viết**.
2. **`pnpm build` đỏ vì môi trường** (EPERM symlink `output: standalone`) — tiền lệ `W22-3`; **không** sửa `next.config.ts` để lách.
3. **`pnpm lint` không có script** (nợ `N70`) — cổng `kiem-tat-ca` không chạy lint nên G3 vẫn đo được.

## 3. Migration đã áp

**Không có** — hàng này **0 migration, 0 `pg_dump`** (schema `Pro*` đã áp ở `N94/W24-2`).

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung (ngắn) |
|---|---|---|
| `D-285` | TẠM CHỐT (DEC) | Mở `/api/v1/promotion/**` **7 route** bằng controller tường minh (6 op §10.1 + `rule-catalog`), **không** mở `evaluate`/`validate-voucher`/`import-rule-costs`/`redemptions` ở v1 này. |
| `D-286` | TẠM CHỐT (DEC) | §10.2 (checkout + chi tiết đơn) **ngoài** phạm vi FE của hàng — nợ có chủ `SAL-002` (checkout + 2 op) và `WEB-*` ("Vì sao giảm" + `redemptions`); dòng "Chiết khấu" đã as-built. |
| `D-287` | TẠM CHỐT (DEC) | Thứ tự trong hàng: `ba` (SRS) → `arch` (ARCH) → `docrev` kiểm nhanh **P1=0** → `be` → `fe`, cùng nhánh/PR. |
| `D-288` | TẠM CHỐT (DEC) | `PromotionDetailDto.Limits` **additive** (4 trần) — **không** thêm route thứ 8; FE **cấm** hằng số `64/8/32/1024`, trần là nguồn **duy nhất** từ `detail.limits`; tải chi tiết lỗi ⇒ trạng thái error. |
| `D-289` | TẠM CHỐT (DEC) | `rule-catalog` lặp **registry** (không lặp bảng DB); thiếu dòng `ProPromotionRuleCosts` ⇒ `PromotionRuleCostCatalog.Cua` + `IsSeeded=false` + `LogWarning`; **không** ném `0010`, **không** bỏ dòng, **0** metric. |
| `D-290` | TẠM CHỐT (DEC) | Nhãn tiếng Việt ở `PromotionRuleCostCatalog.Meta.Label` (code, không DB), fallback `Label = SystemName`; **không** sửa `IPromotionConditionRule` (`D8` giữ), **0** cột DB/migration. |

`check-agent-artifact -Kind decision` **DAT** (117 dòng); hàng đợi xem lại có dòng cho **D-285…D-290**.

## 5. Nợ / rủi ro

- 🔴 **Test BE bề mặt v1 chưa có** (mục §2.1) — chủ nợ **`qa` / `W24-4`**; `TC-70`/`TC-71` đã có trong SRS §14.1, **không** được đánh dấu đã phủ trước khi viết.
- 🟡 **Snapshot nhảy +16 path** (59 → 75): 10 path `/api/v1/reports/*` vận hành **đã có trong code** từ `W26-2` nhưng snapshot cũ chưa cập nhật (`D-276` ghi nợ "1 hàng contract `be`") — nay **đã** vào `schema.d.ts` (additive). `fe` chưa dựng màn nào cho 10 path đó (thuộc `WEB-*` — `D-276`).
- 🟡 **`pnpm build` EPERM** + **`pnpm lint` không script** (`N70`) — môi trường/nợ cũ, không phải do hàng này.
- 🟡 **`D-286` nợ**: checkout (§10.2) cần `evaluate` + `validate-voucher` mở ở hàng `SAL-002`; "Vì sao giảm" cần `BreakdownJson` (không có trong `RedemptionDto`) + route `redemptions` mở ở hàng `WEB-*`.
- 🟡 **4 P3 của `docrev` vòng 8** (không chặn): `TC-71` (v) nên tách hai vế; ARCH §4.4 còn "31 path"; ARCH header còn trích SRS `v0.14`; ARCH §4.3 chưa ghi "`Meta` **thêm** `Label`" — chủ `arch`/`ba`, gộp vòng sau.
- 🟡 **`D-283`/`D-284`** (N94) vẫn nguyên: 4/9 metric `ChuaCoNoiPhat`, chưa nối `IIdempotencyGuard` cho `SaveTree`/`Import`.
- **Chờ người (vùng cấm)**: (a) duyệt lô `D-277…D-290`; (b) **AQ10/Q8** GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release; (d) merge `main` (biên bản người ký).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG W24-3 (WP-24 F3 FE) → mở `W24-4` (WP-24 F4 QA + F5 REV).**

- Hàng kế = **`W24-4`** (loại `code`, `buoc` = F4 QA + F5 REV): `qa` viết testplan **`PRO-002`** (tệp mới dưới `docs/tests/`) phủ **20 BR** + `TC-70`/`TC-71`
  + **3 tệp test BE còn thiếu ở §2.1** (vì `be` bị scope chặn), negative control đỏ→xanh có bằng chứng, integration `TC-33/34/42/43`
  trên DB thật; `TC-22` ghi **5/9** metric có điểm phát (**không** ghi 9/9, `D-283`); phần "guard chống trùng payload" = **chưa áp dụng v1**
  (`D-284`). Rồi `rev` review (`VERDICT: PASS`, P1 = 0).
- Đọc trước: SRS `v0.15` §14.1/§14.2, `D-285`…`D-290`, `docs/reviews/PRO-002-docreview.md` (vòng 8).
- **Nhớ**: `W24-5` chạy `-DayDu` + push `tich-hop/gd3`.

```
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`.
- Commit phiên: **`bc7670d`** (SRS v0.15 + ARCH v0.8 + DOCREV + `D-285…D-290`) → **`66deae3`** (F3 BE: 7 route + `rule-catalog` + `Limits`)
  → **`26a3a11`** (F3 FE: 3 màn + 15 test + snapshot 59→75), cả ba **đã push** `origin/<nhánh>`.
- **Không** push `tich-hop/gd3` (chỉ hàng `dong-wp` = `W24-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N95`): `ba` ×4 (v0.12 · v0.13 · v0.14 · v0.15) · `arch` ×2 (v0.7 · v0.8) · `docrev` ×3 (vòng 6/7/8) ·
`decider` ×3 (`D-285…287` · `D-288` · `D-289`/`D-290`) · `be` ×1 · `fe` ×2 (dựng 3 màn · NC-N95-1). **0 lần chặn dừng.**
Cảnh báo hook: **"thiếu khối JSON"** (decider lượt 1, `arch` lượt 1 — cả hai **dừng ở trần lượt** trước khi phát báo cáo; nội dung đã
ghi vào tệp, ORCH kiểm lại bằng `git status` + `grep`; `arch` lượt 1 **30/30 lượt**, đã kiểm §4.3/§4.4/§12.1 **có** đủ) ·
`be` **1 lần bị `guard-write-scope` chặn** khi tạo tệp `test/**` — **không lách**, đã chuyển thành nợ `qa`/`W24-4` (§2.1).
**0 tệp lọt vùng thật** (`git status` sau mỗi vai đều đúng vùng).
