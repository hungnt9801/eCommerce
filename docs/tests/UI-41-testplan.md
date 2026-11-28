# Testplan `UI-41` — harness so ảnh 27 route + a11y nền (bước `U41-3`)

- **SRS:** `docs/ui/00-kiem-ke.md` (§1.4 BR-01…BR-09) · **Kiến trúc/harness:** `docs/ui/01-kien-truc-ui.md`
- **Nhánh:** `feat/ui-wp41-nen` · **Phiên:** `N122` / hàng `U41-3` + cập nhật `N123` / hàng `U41-4` (`D-337` §2.2 + `D-338` §2.3/§7) (`bang-phien-ui-1.md`)
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
| Ca Playwright (lần chạy sạch, không `--update`) — **vòng 2 trên server 3100** | **54 passed / 0 failed / 0 skipped** × **2 lần liên tiếp** (mỗi lần 27 ảnh + 27 axe) |
| Ca tĩnh | `specs/01-tinh.spec.ts` — 19 ca (`02-login` … `28-notifications-batches`) |
| Ca động | `specs/02-dong.spec.ts` — 8 ca (`12-pos-bill` … `26-template-detail`) |
| Phép đo cố định (`D-331`) | viewport 1440×900 · dsf 1 · `locale vi-VN` · `timezoneId Asia/Ho_Chi_Minh` · `animations: disabled` · `maxDiffPixelRatio 0.001` |
| Cấu hình | `playwright.visual.config.ts` kế thừa root config, **không** sửa root (`D-332`) |

**Nguyên tắc ca không rỗng (Q5):** mỗi ca **khẳng định nội dung thật** (tiêu đề/nhãn/placeholder của route)
**trước** `toHaveScreenshot` — ảnh không bao giờ là ca rỗng; nếu selector/route đổi, ca đỏ trước khi so ảnh.

**Mặt nạ biến động (`support/manhinh.ts`):** `.tabular` (số liệu), ngày/giờ (`dd/MM/yyyy`, `yyyy-MM-dd`),
`main table tbody` (dòng dữ liệu), chuỗi mang `runId` (số đơn/phiếu/mã KM/tên khách) ⇒ so sánh **chỉ** bắt
bố cục/màu/nhãn, không đỏ giả theo dữ liệu lượt chạy. Tiêu đề/tab/nhãn/khung/màu token **không** mask (WP-42
đổi token ⇒ ảnh **phải** đỏ). Bổ sung `D-338` (§2.3): `chup()` chuẩn hoá **bề rộng badge đếm** tab
(`main a span.rounded-full`) bằng CSS, và `maskChung`… **không** đổi — thay vào đó **mask `main table thead`
theo từng route đo được trôi** header (`#4`/`#6`/`#13`/`#19`/`#21`/`#24`), header bảng các route còn lại vẫn được so.

**Vòng 2 — F-02 (mask quá rộng) — P2 chấp nhận:** `main table tbody` **giữ nguyên** (không thu hẹp về ô).
Lý do đo được: DB không reset, mỗi lượt `global-setup`/`taoFixture` **thêm dòng** ⇒ **số dòng** bảng danh
sách tăng giữa các lượt ⇒ chiều cao `tbody` đổi ⇒ mask từng ô vẫn lệch (và lộ giá trị `runId`). Vì vậy che cả
`tbody` là điều kiện tất định. Bù lại, các trang chi tiết dựng fixture mới mỗi lượt (`10`, `11`, `17`, `23`) che
thêm dòng tiêu đề/trạng thái + thẻ thông tin + bảng + mục tồn hiện tại. **Riêng `#16 customer/list` KHÔNG mask
hàng tab** (giữ phủ so ảnh cho nhãn + active-tab theo `D-331`): thay vào đó `chup()` chuẩn hoá **bề rộng ô đếm**
`(n)` bằng **một rule CSS** (`D-337`, `support/manhinh.ts`), còn `maskChung` vẫn mask **chữ số** `(n)`
(`getByText(/^\(\d+\)$/)`). Lý do: mask Playwright tô theo **bounding box**; số hồ sơ tích luỹ đổi **số chữ số**
⇒ ô đếm rộng ra ⇒ nhãn 5 tab dịch 3–6 px ⇒ ảnh đỏ giả nếu không cố định `min-width` (chi tiết §2.2).

⚠️ **Rủi ro font (đã ghi `D-331`, `playwright.visual.config.ts`):** ảnh sinh trên **Windows**. Khi cổng
visual vào CI Linux (`U42-4`/`U51-4`) phải **tái sinh** ảnh chuẩn trên Linux — font Segoe UI/Inter fallback
khác ⇒ lần đầu trên Linux dự kiến đỏ đều; đó là chuyển nền tảng, không phải hồi quy giao diện.

## 2.1. Vòng 2 — vá P1 "ảnh trang KHÔNG có CSS" (rev bắt) + F-03

**Nguyên nhân P1 (đã xác minh bằng PNG + HTTP):** 27 ảnh vòng 1 chụp từ server cũ (PID 18936, build cũ) phục vụ
HTML trỏ `/_next/static/css/d659…css`, nhưng `.next` đã thay bằng `486d…css` ⇒ CSS **404** ⇒ ảnh là **form trần /
sidebar `<ul>`**. Cổng so ảnh vẫn "xanh" vì ảnh chuẩn cũng trần ⇒ **BR-06/D-331 "đổi token ⇒ ảnh đỏ" bất khả
thi** (cổng xanh giả). PID 18936 không kill được (access denied); ORCH dựng server **MỚI** `next start -p 3100`
(PID 26472): `http://localhost:3100/login` HTTP 200, CSS `486d…css` HTTP 200 **29002 bytes**.

**Cách vá trong harness (phạm vi QA):**
- **GUARD CSS** (`support/manhinh.ts` → `kiemCss`, gọi trong `chup()` **mọi ca**): chèn `<div class="flex">` dò
  vào `body`, đọc `getComputedStyle(display)`. Tailwind áp ⇒ `.flex{display:flex}` ⇒ **`display === "flex"`**;
  đồng thời `document.styleSheets.length > 0`. Không áp ⇒ **ca ĐỎ ngay** với thông điệp "trang chưa có CSS:
  build/server sai". (Dùng `flex` **cấu trúc** thay `h-4`/`w-4` vì Tailwind v4 không sinh `h-4` trong CSS đã
  build ⇒ guard `h-4` sẽ đỏ giả.)
- Chạy Playwright với `E2E_WEB=http://localhost:3100` (`reuseExistingServer:true` tái dùng server mới).

**F-03 (chụp toàn trang) — P2 chấp nhận, `fullPage:false`:** đã thử `fullPage:true`; ghi nhận **8/27 rồi 7/27
ca đỏ** vì trang danh sách có `tbody` dài biến đổi dưới màn hình (cùng lý do F-02) ⇒ **không tất định**. Giữ
viewport 1440×900 tất định; hồi quy nội dung cuộn xử lý ở cổng visual CI (`U42-4`/`U51-4`) khi có DB tất định.

**Số đo vòng 2 (server 3100):** sinh lại **27/27** ảnh (`--update-snapshots` → 54 passed) · chạy sạch **54/54**
× **2 lần** liên tiếp (không `--update`) ⇒ tất định. Ảnh `02-login.png`/`03-dashboard.png` đã mở kiểm: có nền,
card, nút xanh `--brand`, font, sidebar màu — **KHÔNG** phải form trần.

## 2.2. `D-337` (phiên N123 / `U41-4`) — `#16` bề rộng ô đếm badge

**Sự cố F-02 (đo trên server 3100, `U41-3`/ORCH):** lượt chạy **53 passed / 1 failed**; ca `#16 customer/list`
lệch **2266 px** (ratio **0.01** > ngưỡng `0.001`). Nguyên nhân: `maskChung` che **chữ số** `(n)` của 5 tab bằng
`getByText(/^\(\d+\)$/)`, nhưng Playwright tô mask theo **bounding box** của locator; số hồ sơ tích luỹ đổi **số
chữ số** (`9` → `10` …) ⇒ ô đếm `span.opacity-80` rộng ra ⇒ nhãn 5 tab dịch 3–6 px ⇒ lệch ảnh.

**Cách vá (`D-337`, test-only 1 tệp `support/manhinh.ts`):** thêm vào lời gọi `page.addStyleTag` **sẵn có** (cùng
chỗ ẩn scrollbar) rule
`main a.nh-btn span.opacity-80{display:inline-block;min-width:3.25rem;text-align:right;overflow:hidden}` — chỉ khớp
ô đếm 5 tab của `#16` (`opacity-80` **chỉ** xuất hiện ở `app/[business]/customer/list/page.tsx:130`), cố định bề
rộng ⇒ bounding box tất định. **KHÔNG** mask hàng tab (giữ phủ nhãn/active-tab theo `D-331`), **không** sửa
`page.tsx` / root `playwright.config.ts` / fixture. Sinh lại **duy nhất** `__screenshots__/16-customer-list.png`.

**Kết quả N123 (số đo thật):** `--update-snapshots` **54 passed** (2.2 phút) — `git status` vùng ảnh chỉ còn
`16-customer-list.png` đổi (ảnh `#6` bị `--update` ghi lại do **trôi dữ liệu latent**, đã khôi phục về HEAD ⇒
`git diff --quiet` exit 0). Lượt **sạch** (không `--update`, `E2E_WEB=http://localhost:3100`): **53 passed / 1
failed** (2.3 phút) — `#16` **PASS**; ca đỏ còn lại là `#6 order/manage` (**6597 px**, ratio 0.01), **trôi dữ
liệu độc lập với rule** (rule `span.opacity-80` **không** khớp `#6` — `#6` dùng badge `.tabular` `rounded-full`).
→ `#6` cùng các route trôi header sau đó được xử lý ở **§2.3 (`D-338`)**: hiện **54/54**.

**Nợ latent (xử lý tiếp ở `D-338` §2.3):** cùng cơ chế bề rộng badge theo số chữ số — `#4`/`#6`/`#8` dùng badge
`rounded-full`; `D-338` cố định bề rộng badge + mask `thead` theo route đo được trôi. Gốc (DB dev/CI tất định) vẫn
là `U42-4`/`U51-4`. Gỡ rule chuẩn hoá `#16` khi có `PageTabs` badge **bề rộng cố định** (`U45-2` + chuyển `#16` ở
`U48-3`). Nợ phụ **P3**: `#4` có 2 locator mask NO-OP (chọn nhầm class DOM) do `qa` sửa khi sinh lại ảnh ở `U49-4`.

## 2.3. `D-338` (phiên N123 / `U41-4`) — bước so ảnh `-DayDu` không tất định theo dữ liệu

**Bối cảnh đo được (sau `D-337`):** `#16` đã xanh, nhưng lượt **sạch** còn đỏ `#6 order/manage` **6597 px**
(ratio 0.01). Nguyên nhân: (①) **badge đếm của 14 tab** dùng `span.rounded-full.tabular`, bề rộng co theo **số
chữ số** của đếm (DB dev trôi: E2E trong `-DayDu` chạy **trước** bước ⑱ và tạo thêm dòng mỗi lượt) ⇒ nhãn tab
dịch ngang; (②) bảng danh sách **auto-layout** (`table.w-full` / `nh-table`, `thead th{white-space:nowrap}`
`globals.css:98-107`) ⇒ bề rộng cột co theo **nội dung `tbody`** (mask **không** gỡ nội dung khỏi layout) ⇒
**nhãn header dịch**. `maskChung` chỉ tô `tbody`/`.tabular`, **không** chặn dịch layout.

**Cách vá (test-only, `qa`):**
- `support/manhinh.ts` → `chup()`: mở rộng lời gọi `page.addStyleTag` **sẵn có** thêm
  `main a span.rounded-full{display:inline-block;min-width:2.5rem;text-align:center}` (giữ rule `opacity-80` 3.25rem
  của `D-337`) ⇒ cố định bề rộng badge đếm; **vẫn** so nhãn/active/màu tab (không mask hàng tab).
- `specs/01-tinh.spec.ts`: thêm `page.locator("main table thead")` vào **mask riêng** của **route đo được trôi**;
  **không** đưa vào `maskChung` toàn cục. `maskChung` **không** đổi. Ngưỡng `maxDiffPixelRatio 0.001` **giữ nguyên**.

**Phép đo 2 lượt (xác định route trôi header — bằng diff PNG, không suy đoán):** `--update` → clean (`#6` `#13`
`#19` lộ trôi thead) → **E2E đổi dữ liệu** → clean lần 2 (lộ thêm `#4` `#21` `#24`). Diff PNG xác nhận vùng đỏ **chỉ
ở hàng `thead`** (`#13`: 2267 px; `#19`: 1710 px; `#4`: 1680 px; `#21`, `#24` cùng dạng). Các route có bảng nhưng
**đo không trôi** (`#8`, `#16`) ⇒ **giữ** so header, **không** mask.

| Route được mask `thead` (`D-338`) | Lý do đo được |
|---|---|
| `#4 product/item` (`nh-table`) | diff 1680 px ở header (ID/Mã/Tên/SKU…) |
| `#6 order/manage` (`table.w-full`) | 6597 px (trước vá) — header 8 cột auto |
| `#13 pos/bill/list` | 2267 px ở header (Số HĐ/Thời gian/Kênh…) |
| `#19 report/revenue` | 1710 px ở header bảng tổng hợp |
| `#21 promotion` (`nh-table`) | header Mã/Tên/Loại/Giá giảm/Trạng thái… |
| `#24 notifications/templates` | header Mã/Tên/Kênh/Mục đích… |

**Điều kiện đạt (`D-338` ①, đo thật):** 2 lượt **sạch** liên tiếp, **giữa 2 lượt chạy 1 lượt E2E** ⇒ cả hai **54/54**
(không `--update`) — xem §7. Bước ⑱ trong `-DayDu` **vẫn bắt buộc**; **không** nới ngưỡng, **không** bỏ ca.

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
| 2026-11-29 | NC-1v2 | apps/admin-web/e2e/visual/specs/01-tinh.spec.ts:44 | page.locator("main .grid.grid-cols-4"), -> page.locator("main .grid.grid-cols-4"), page.locator("main"), | $env:E2E_WEB='http://localhost:3100'; pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts --project=visual --grep "#3 dashboard" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

> **Vòng 2 (29/11/2026):** dòng đột biến NC-1 **vẫn còn nguyên** `page.locator("main .grid.grid-cols-4"),`
> tại `01-tinh.spec.ts:44` ⇒ **giữ nguyên recipe**. Lưu ý khi ORCH chạy lại NC: **phải** đặt
> `E2E_WEB=http://localhost:3100` trong `-TestCommand` để trỏ server có CSS (nếu trỏ server cũ 3000 ⇒ guard
> CSS ném lỗi trước cả phép so ảnh, không chứng minh được pixel-diff). Guard CSS vòng 2 là lớp bảo vệ **bổ
> sung**, không thay NC-1.

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

## 5. Cổng (số đo thực thi — vòng 2 `N122` + `D-337`/`D-338` `N123`, 29/11/2026, server 3100)

| Cổng | Lệnh | Kết quả |
|---|---|---|
| Typecheck | `pnpm --filter @ecommerce/admin-web typecheck` | **exit 0** (0 lỗi) |
| Lint | `pnpm --filter @ecommerce/admin-web lint` | **0 error** · 12 warning (bằng nền `U41-2`, không tăng) |
| Vitest | `pnpm --filter @ecommerce/admin-web test` | **60/60 passed** (6 tệp) — không đổi so `U41-2` |
| Visual (vòng 2, sạch ×2 — N122) | `E2E_WEB=http://localhost:3100 playwright test -c e2e/visual/playwright.visual.config.ts` | **54/54 passed** × **2 lần liên tiếp** (27 ảnh + 27 axe) |
| Visual (sau `D-337` — N123) | `E2E_WEB=http://localhost:3100 playwright test -c e2e/visual/playwright.visual.config.ts` | **53/54** (2.3 phút): `#16` **PASS** sau vá; `#6` **FAIL** 6597 px (ratio 0.01) do trôi dữ liệu latent `U49-4`, **không** liên quan rule `opacity-80`. Sinh lại ảnh: chỉ `16-customer-list.png` đổi. |
| Visual (sau `D-338` — N123, a) | `E2E_WEB=http://localhost:3100 playwright test -c e2e/visual/playwright.visual.config.ts` (lượt **sạch** 1) | **54/54 passed** (2.3 phút) |
| E2E đổi dữ liệu (b) | `E2E_WEB=http://localhost:3100 pnpm --filter @ecommerce/admin-web e2e` | **8/8 passed** (38 s) — Host 54275 tự bật, reuse server web 3100 |
| Visual (sau `D-338` — N123, c) | cùng lệnh visual (lượt **sạch** 2, sau E2E) | **54/54 passed** (2.4 phút) ⇒ tất định qua thay đổi dữ liệu |
| Ảnh chuẩn | `__screenshots__/` | **27** tệp PNG; `D-338` sinh lại **8** tệp (§7) |

## 6. Chưa phủ / nợ

- Tầng Domain/Application/API (K-06): **không áp dụng** — `U41-3` không thêm luật nghiệp vụ.
- **F-02 (mask rộng) — P2 chấp nhận:** `main table tbody` giữ nguyên do số dòng bảng tăng giữa các lượt (DB không
  reset). Các trang chi tiết dựng fixture mới mỗi lượt che thêm header/thẻ/bảng. Chi tiết + lý do ở §2.
- **F-03 (fullPage) — P2 chấp nhận:** `fullPage:true` gây 8/7 ca đỏ (chiều cao `tbody` biến đổi) ⇒ giữ viewport
  1440×900. Chi tiết ở §2.1.
- **`D-337` + `D-338` (N123) — `#16`/`#6` đã vá:** `D-337` cố định ô đếm `#16` (`opacity-80`, chỉ ở
  `customer/list/page.tsx:130`); `D-338` cố định badge đếm `.rounded-full` (`#4`/`#6`/`#8`) + mask `thead` theo
  route đo được trôi. Lượt sạch **54/54** (×2, qua E2E) — §2.2/§2.3/§5/§7.
- **`D-338` — HỒI QUY BỊ MẤT (ghi rõ):** với **6 route** `#4`/`#6`/`#13`/`#19`/`#21`/`#24`, hàng **header bảng**
  (`thead`) **không còn được so ảnh** (đã mask) — nhãn cột tĩnh, rủi ro thấp; các route còn lại **vẫn so** header.
  Khôi phục khi có DB tất định (`U42-4`/`U51-4`): **gỡ mask `thead`** + re-baseline ảnh. Nợ latent còn lại: badge
  vẫn rộng theo số chữ số khi vượt `min-width` (đếm ≥ 3 chữ số) ⇒ nợ DB tất định `U42-4`/`U51-4`; gỡ rule badge khi
  có `PageTabs` bề rộng cố định (`U45-2` + chuyển `#16` ở `U48-3`). `#4` còn 2 locator mask **NO-OP** (`D-337` §2.2)
  — `qa` sửa khi chuyển màn `U49-4`.
- **`check-agent-artifact.ps1 -Kind testplan`:** script tách mục `## 1.` của SRS bằng regex `^##\s+1\.`, nhưng
  tài liệu `docs/ui/00-kiem-ke.md` đánh số mục kiểu `## §1.` (có ký tự `§`) ⇒ script đọc §1 rỗng, báo
  "SRS không có BR ở §1". Đây là **lệch định dạng công cụ ↔ tài liệu** (không phải testplan thiếu BR: §1 ở
  trên phủ đủ BR-01…BR-09). Vá thuộc `tools/`/`docs/ui/**` — **ngoài vùng ghi QA**, ghi lại để ORCH xử lý.
- Ảnh chuẩn sinh trên **Windows** ⇒ phải tái sinh trên Linux khi dựng cổng visual CI (`U42-4`/`U51-4`).
- Báo cáo a11y chi tiết (theo mức + `file:dòng`) là việc của vai `a11y` (`docs/…UI-41-a11y.md`); testplan này
  chỉ cung cấp `axe-summary.json` làm nguyên liệu.

## 7. `D-338` — danh sách PNG sinh lại (chính xác) + số đo điều kiện đạt

**Lệnh sinh lại:** `E2E_WEB=http://localhost:3100 pnpm --filter @ecommerce/admin-web exec playwright test -c
e2e/visual/playwright.visual.config.ts --update-snapshots` ⇒ **54 passed** (2.5 phút).
**Soát** `git status --porcelain apps/admin-web/e2e/visual/__screenshots__/` ⇒ **đúng 8** tệp, khớp **toàn bộ** danh
sách route đã đổi rule/mask; **0** tệp ngoài danh sách giải thích được (không phải phục hồi tệp nào về HEAD).

| # | PNG đổi | Nguyên nhân (rule/mask áp vào route) |
|---|---|---|
| 1 | `04-product-item.png` | badge tab `.rounded-full` (rule `D-338`) + mask `thead` (đo trôi 1680 px) |
| 2 | `06-order-manage.png` | badge tab `.rounded-full` + mask `thead` (đo trôi 6597 px) |
| 3 | `08-inventory-bill.png` | badge tab `.rounded-full` (đo **không** trôi `thead` ⇒ giữ so header) |
| 4 | `13-pos-bill-list.png` | mask `thead` (đo trôi 2267 px) |
| 5 | `16-customer-list.png` | rule ô đếm `opacity-80` (`D-337`, giữ nguyên) |
| 6 | `19-report-revenue.png` | mask `thead` (đo trôi 1710 px) |
| 7 | `21-promotion.png` | mask `thead` (đo trôi) |
| 8 | `24-templates.png` | mask `thead` (đo trôi) |

**Đo điều kiện đạt `D-338` (thật, server 3100):**

| Bước | Lệnh | Kết quả | Thời gian |
|---|---|---|---|
| a. Lượt sạch 1 | visual (không `--update`) | **exit 0 — 54/54 passed** | 140 s |
| b. Đổi dữ liệu | `E2E_WEB=http://localhost:3100 pnpm --filter @ecommerce/admin-web e2e` | **exit 0 — 8/8 passed** | 40 s |
| c. Lượt sạch 2 (sau E2E) | visual (không `--update`) | **exit 0 — 54/54 passed** | 147 s |

⇒ `D-338` ④(d) **không** kích hoạt: sau (c) **không** còn route đỏ do trôi ngoài tab/thead ⇒ không cần nâng thêm nợ
ngoài mục §6. **Điều kiện gỡ mask `thead`** (khôi phục so header): có DB tất định (`U42-4`/`U51-4`) ⇒ gỡ 6 mask
`thead` + `git checkout` 8 ảnh trên rồi re-baseline.

