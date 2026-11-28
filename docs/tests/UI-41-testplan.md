# Testplan `UI-41` — harness so ảnh 27 route + a11y nền (bước `U41-3`)

- **SRS:** `docs/ui/00-kiem-ke.md` (§1.4 BR-01…BR-09) · **Kiến trúc/harness:** `docs/ui/01-kien-truc-ui.md`
- **Nhánh:** `feat/ui-wp41-nen` · **Phiên:** `N122` / hàng `U41-3` (`bang-phien-ui-1.md`)
- **Bằng chứng thực thi:** `apps/admin-web/e2e/visual/` — ảnh chuẩn `__screenshots__/*.png` (commit), kết quả chạy `.run/` (gitignore)

> Đây là **testplan của harness UI** (so ảnh + axe), không phải testplan nghiệp vụ. `U41-3` **không** thêm
> mã sản phẩm/endpoint/domain ⇒ các tầng Domain/Application/API của K-06 **không áp dụng** (không có luật
> tiền/tồn/trạng thái/quyền mới để kiểm). Tầng được đo là **E2E Playwright (ảnh)** + **a11y (axe)** + cổng
> **Vitest** hiện hữu (không đổi).

## 1. Phủ BR

| BR | Test / bằng chứng (tệp · ca) | Tầng | Trạng thái |
|---|---|---|---|
| BR-01 | `specs/01-tinh.spec.ts` (19 ca) + `specs/02-dong.spec.ts` (8 ca); `__screenshots__/` có **27** ảnh khớp 27 route chụp được (bỏ #1 `/` chỉ redirect). 28 route trang − 1 = 27. | E2E ảnh | ✅ đạt |
| BR-02 | 27 route render đủ component thật (axe quét DOM từng route); mọi ca có **khẳng định nội dung** trước khi chụp ⇒ component phải render đúng mới xanh. | E2E ảnh | 🟡 nền (đo gián tiếp qua render route; component không có ca riêng) |
| BR-03 | Tiêu chí **cấu trúc tài liệu** (§4 pattern ≥ 2 nơi) — do `docrev`/`check-feature-doc` đảm bảo, không phải phạm vi Playwright. | Tài liệu | 🟡 nền |
| BR-04 | Tiêu chí **cấu trúc tài liệu** (§5 đủ 5 cột) — `docrev`. | Tài liệu | 🟡 nền |
| BR-05 | Tiêu chí **cấu trúc tài liệu** (§6 có mức P1/P2/P3) — `docrev`; kết quả axe bên dưới là **đầu vào** cho báo cáo a11y. | Tài liệu + a11y | 🟡 nền |
| BR-06 | §7 rỗng ở WP-41 ⇒ **mọi khác biệt ảnh = ĐỎ**. `playwright.visual.config.ts` `expect.toHaveScreenshot.maxDiffPixelRatio = 0.001`; lần chạy sạch **54/54 xanh** (27 ảnh khớp). Negative control NC-1 chứng minh ảnh **đỏ được**. | E2E ảnh | ✅ đạt |
| BR-07 | Tiêu chí **cấu trúc tài liệu** (§2.4 số thật vs kế hoạch) — `docrev`; testplan này chụp đúng **số thật 27**, không sửa số kế hoạch 37. | Tài liệu | 🟡 nền |
| BR-08 | Phụ lục B (selector E2E/ảnh) phải còn. Harness dùng `role`/`placeholder`/`heading` + `?warehouseId`; 54 ca xanh chứng minh selector còn sống. Không ca nào phụ thuộc `data-pos-*` bị mất. | E2E ảnh + a11y | ✅ đạt |
| BR-09 | `git status` vùng QA chỉ chạm `apps/admin-web/e2e/visual/**` + `docs/tests/**` (harness); **không** sửa `docs/ui/**`, mã sản phẩm, `pnpm-lock.yaml`. | Quy trình | ✅ đạt |

- **Tổng BR: 9** · có dòng/bằng chứng: **9** · đo trực tiếp bằng harness: **4** (BR-01, BR-06, BR-08, BR-09) · là tiêu chí cấu trúc tài liệu ("nền", do `docrev`): **5** (BR-02…BR-05, BR-07).

## 2. Harness 27 route (so ảnh) — số đo

| Hạng mục | Số đo |
|---|---|
| Route trang có giao diện | **27** (19 tĩnh + 8 động; bỏ #1 `/` redirect) |
| Ảnh chuẩn đã sinh | **27** tệp `apps/admin-web/e2e/visual/__screenshots__/*.png` |
| Ca Playwright (lần chạy sạch, không `--update`) | **54 passed / 0 failed / 0 skipped** (27 ảnh + 27 axe) |
| Ca tĩnh | `specs/01-tinh.spec.ts` — 19 ca (`02-login` … `28-notifications-batches`) |
| Ca động | `specs/02-dong.spec.ts` — 8 ca (`12-pos-bill` … `26-template-detail`) |
| Phép đo cố định (`D-331`) | viewport 1440×900 · dsf 1 · `locale vi-VN` · `timezoneId Asia/Ho_Chi_Minh` · `animations: disabled` · `maxDiffPixelRatio 0.001` |
| Cấu hình | `playwright.visual.config.ts` kế thừa root config, **không** sửa root (`D-332`) |

**Nguyên tắc ca không rỗng (Q5):** mỗi ca **khẳng định nội dung thật** (tiêu đề/nhãn/placeholder của route)
**trước** `toHaveScreenshot` — ảnh không bao giờ là ca rỗng; nếu selector/route đổi, ca đỏ trước khi so ảnh.

**Mặt nạ biến động (`support/manhinh.ts`):** `.tabular` (số liệu), ngày/giờ (`dd/MM/yyyy`, `yyyy-MM-dd`),
`main table tbody` (dòng dữ liệu), chuỗi mang `runId` (số đơn/phiếu/mã KM/tên khách) ⇒ so sánh **chỉ** bắt
bố cục/màu/nhãn, không đỏ giả theo dữ liệu lượt chạy. Tiêu đề/tab/nhãn/khung/màu token **không** mask (WP-42
đổi token ⇒ ảnh **phải** đỏ).

⚠️ **Rủi ro font (đã ghi `D-331`, `playwright.visual.config.ts`):** ảnh sinh trên **Windows**. Khi cổng
visual vào CI Linux (`U42-4`/`U51-4`) phải **tái sinh** ảnh chuẩn trên Linux — font Segoe UI/Inter fallback
khác ⇒ lần đầu trên Linux dự kiến đỏ đều; đó là chuyển nền tảng, không phải hồi quy giao diện.

## 3. Chiến lược fixture (`D-333`) + route chưa chụp

- **Dữ liệu nền bằng API thật** (`support/fixtures.ts`, `support/api.ts`): kho, phiếu kho (tạo + ghi sổ),
  ca POS, hoá đơn bán lẻ, khách, khuyến mãi, mẫu thông báo — **không** mock/stub.
- **Đơn hàng:** dùng **ngoại lệ SQL duy nhất đã duyệt `D-49`** (`e2e/support/don-hang.ts`) — **không** thêm
  câu SQL nào khác. Route `07-order-detail` là ca duy nhất chạm seed SQL.
- **Mã duy nhất mỗi lời gọi:** `taoFixture()` thêm hậu tố 8 ký tự ngẫu nhiên vào mã kho/SKU/số phiếu/đơn/KM/mẫu
  và số điện thoại — để `02-dong` (ảnh) và `03-axe` (a11y) cùng chạy trong **một** lượt Playwright mà không
  đụng unique index (`(TenantId, Code)` của kho; số phiếu; số đơn). *(Sửa trong `U41-3`: số ĐT cố định
  `0900000000` và mã theo `runId` gây đỏ khi gọi lần hai / lần chạy sau.)*
- **Routes chưa chụp được:** **0** — cả 27 route đều dựng được dữ liệu thật sau bản sửa fixture trên.
  Nếu một route không dựng được dữ liệu ⇒ `test.skip(true, lýDo)` + ghi vào `axe-summary.json`.`boQua`,
  **tuyệt đối không** chụp ảnh giả (route không có dữ liệu thật).

## 4. Báo cáo a11y nền (axe-core 27 route)

- Spec: `specs/03-axe.spec.ts` — inject `axe-core@4.14.1` (có sẵn ở `packages/ui`, **không** cài gói mới) qua
  `page.addScriptTag({ path: duongAxe() })` rồi `page.evaluate` gọi `axe.run` (tag `wcag2a/aa/21/22 + best-practice`).
- Kết quả ghi JSON: `apps/admin-web/e2e/visual/.run/axe-summary.json` (schema `{routes[], boQua[], tong{}}`).
- **Số đo lần chạy sạch (28/11/2026):** quét **27/27** route · **40** vi phạm —
  `critical 1 · serious 26 · moderate 8 · minor 5`.
  - `critical`: `label` (107 node) @ `18-settings-staff` (ma trận quyền thiếu nhãn cho ô/nút).
  - `serious`: chủ yếu `target-size` (vùng chạm < 24px) rải khắp 27 route (nợ chung của shell/bảng hiện tại).
  - Đây là **bản đồ nợ nền**, KHÔNG phải hợp đồng "0 vi phạm"; `U41-3` chỉ chặn **P1 do WP-41 gây ra**
  (WP-41 không đổi giao diện ⇒ không thêm vi phạm).

## Negative control

> **Trạng thái:** ⏳ **chờ ORCH chạy** `tools/agents/negative-control.ps1` **sau khi commit** (script từ chối
> tệp chưa commit). Dưới đây là **recipe** đã chốt; ORCH chạy sẽ **tự điền** cột đỏ/xanh (kèm dòng `exit`).
> QA **không** tự ghi "đỏ/xanh" trước khi chạy (K-03).

| # | Tệp (đã commit) | Find → Replace (đột biến **thật**) | Ca chạy (`--grep`) | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| NC-1 | `apps/admin-web/e2e/visual/specs/01-tinh.spec.ts` | `page.locator("main .grid.grid-cols-4"),` → `page.locator("main .grid.grid-cols-4"), page.locator("main"),` | `#3 dashboard` | chờ ORCH | chờ ORCH |
| 2026-11-29 | NC-1 | apps/admin-web/e2e/visual/specs/01-tinh.spec.ts:44 | page.locator("main .grid.grid-cols-4"), -> page.locator("main .grid.grid-cols-4"), page.locator("main"), | pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts --project=visual --grep "#3 dashboard" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

- **Mô tả NC-1:** thêm `main` vào mặt nạ của ca dashboard ⇒ toàn bộ vùng nội dung bị tô khối ⇒ ảnh chụp
  lệch so với `03-dashboard.png` quá `maxDiffPixelRatio 0.001` ⇒ ca **PHẢI ĐỎ**; khôi phục ⇒ **PHẢI XANH**.
  Đây chứng minh phép so ảnh (BR-06) **thực sự so pixel**, không phải ca rỗng.
- **TestCommand:**
  `pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts --project=visual --grep "#3 dashboard"`
- **Lệnh đầy đủ cho ORCH:**
  ```
  pwsh -File tools/agents/negative-control.ps1 `
    -File apps/admin-web/e2e/visual/specs/01-tinh.spec.ts `
    -Find 'page.locator("main .grid.grid-cols-4"),' `
    -Replace 'page.locator("main .grid.grid-cols-4"), page.locator("main"),' `
    -TestCommand 'pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts --project=visual --grep "#3 dashboard"' `
    -Testplan docs/tests/UI-41-testplan.md -Id NC-1
  ```

## 5. Cổng (số đo thực thi 28/11/2026)

| Cổng | Lệnh | Kết quả |
|---|---|---|
| Typecheck | `pnpm --filter @ecommerce/admin-web typecheck` | **exit 0** (0 lỗi) |
| Lint | `pnpm --filter @ecommerce/admin-web lint` | **0 error** · 12 warning (bằng nền `U41-2`, không tăng) |
| Vitest | `pnpm --filter @ecommerce/admin-web test` | **60/60 passed** (6 tệp) — không đổi so `U41-2` |
| Visual (sạch) | `playwright test -c e2e/visual/playwright.visual.config.ts` | **54/54 passed** (27 ảnh + 27 axe) |
| Ảnh chuẩn | `__screenshots__/` | **27** tệp PNG |

## 6. Chưa phủ / nợ

- Tầng Domain/Application/API (K-06): **không áp dụng** — `U41-3` không thêm luật nghiệp vụ.
- Ảnh chuẩn sinh trên **Windows** ⇒ phải tái sinh trên Linux khi dựng cổng visual CI (`U42-4`/`U51-4`).
- Báo cáo a11y chi tiết (theo mức + `file:dòng`) là việc của vai `a11y` (`docs/…UI-41-a11y.md`); testplan này
  chỉ cung cấp `axe-summary.json` làm nguyên liệu.
