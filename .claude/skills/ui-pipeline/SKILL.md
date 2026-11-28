---
name: ui-pipeline
description: Dây chuyền cho hàng loai=ui của chuỗi ui-1 (nền tảng UI Web Admin, D-327) — ba bước U1 thiết kế (ux · arch · docrev · decider), U2 dựng (ds hoặc fe), U3 kiểm (qa · a11y · rev); cổng máy cho token, Storybook, theme sáng/tối, i18n, axe, so ảnh, ngân sách bundle. ORCH nạp khi /chay-phien-code gặp hàng loai=ui; không gọi tay.
user-invocable: false
---

# ui-pipeline — hàng `loai = ui` của chuỗi `ui-1`

Nguồn: `docs/agents/bang-phien-ui-1.md` (kế hoạch + bảng) · docs/ui/01-kien-truc-ui.md (sau `U41-1`) · `D-327`.
Skill **dẫn**; mâu thuẫn ⇒ tệp nguồn thắng. Luật git, nhánh, đóng phiên, commit: giữ nguyên `.claude/skills/chay-phien-code/SKILL.md`.

Mã của WP trong chuỗi này là `UI-<số>` (vd `UI-44` cho `WP-44`). Tệp sinh ra theo mã:
`docs/ui/<số>-<tên>.md` (đặc tả) · `docs/reviews/UI-<số>-docreview.md` · `docs/reviews/UI-<số>-review.md` ·
`docs/reviews/UI-<số>-a11y.md` · `docs/tests/UI-<số>-testplan.md` · `docs/ops/UI-<số>-deploy.md`.

## 0. Điều kiện vào (mọi bước)

- `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-<số>` exit 0 (`07` ghi "Không có tài liệu feature" cho WP-41…51).
- Đọc `hang.noiDung` — đó là phạm vi **duy nhất** của hàng. Việc ngoài phạm vi ⇒ ghi `docs/inbox/UI-<số>-ngoai-pham-vi.md`, không làm.
- **Hướng thẩm mỹ cố định** (`D-327`): bố cục, mật độ, màu, nhãn **giống Nhanh.vn** theo số đo `docs/nhanh/01-khao-sat-nhanh-vn.md` §2;
  nút hành động chính **xanh lá `#059669`**, link/tab active xanh dương `#0C83FF`, topbar tối `#252B36`, font Inter 14px, icon Phosphor.
  **Không** chép logo, ảnh, HTML/CSS/JS của Nhanh (`nextjs-admin-rules` U4) — chỉ tái tạo bằng số đo.

## 1. Bước `U1` — thiết kế (ux · arch khi cần · docrev · decider)

1. `ux` viết/cập nhật đặc tả `docs/ui/<số>-<tên>.md` theo khung §4. Đặc tả có **danh sách ảnh dự kiến đổi** (route/story nào sẽ khác ảnh
   chuẩn và vì sao) — đây là căn cứ duy nhất để QA chấp nhận khác biệt ảnh ở `U3`.
2. `arch` chỉ khi hàng ghi `arch` trong cột Vai **hoặc** đặc tả chạm ranh giới gói (`packages/ui` ↔ `apps/admin-web`), thêm phụ thuộc,
   đổi luồng dữ liệu (nuqs, server action), hay đổi cấu trúc route — cập nhật docs/ui/01-kien-truc-ui.md.
3. `docrev` kiểm đặc tả ⇒ `docs/reviews/UI-<số>-docreview.md`. Tối đa 2 vòng; vòng 2 còn P1 ⇒ `PH dodang`.
4. Câu mở (gói mới, phiên bản, đánh đổi) ⇒ `decider`. UI **không** thuộc danh sách đỏ ⇒ M2 cho decider `TẠM CHỐT` khi DOCREV `DOC-OK`.
   Thêm thư viện **luôn** cần một `D-xx` (tên gói, bản **ghim chính xác**, lý do, kích thước gzip ước lượng, đường lùi).

**DoD U1:** `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/UI-<số>-docreview.md` exit 0 · `-Kind decision` exit 0 nếu có `D-xx` mới ·
`kiem-link-tai-lieu.ps1` PASS · 0 tệp code đổi.

## 2. Bước `U2` — dựng (`ds` cho `packages/ui`, `fe` cho màn trong `apps/admin-web`)

Luật dựng (vai tự kiểm, ORCH đo lại):

| # | Luật |
|---|---|
| D1 | Component dùng chung sống ở `packages/ui` (`@ecommerce/ui`); `apps/admin-web` **không** định nghĩa lại primitive/pattern đã có. |
| D2 | Màu, cỡ chữ, khoảng cách, bo góc **chỉ** qua token (`var(--…)` / lớp Tailwind ánh xạ `@theme`). Cấm mã hex/rgb trong component (trừ tệp token). |
| D3 | Mỗi component mới/đổi: **story** (sáng + tối + compact) · **test** Vitest + Testing Library · **axe** trong test (0 vi phạm). |
| D4 | Mọi chuỗi hiển thị mới qua `next-intl` (`messages/vi.json` + `messages/en.json`) — từ `WP-43` trở đi. `vi` là bản gốc. |
| D5 | Chuyển màn **giữ nguyên** `data-testid`, URL, tham số query, thứ tự luồng ⇒ E2E không cần sửa. E2E phải sửa ⇒ dừng, báo ORCH (QA quyết). |
| D6 | Không đổi hành vi nghiệp vụ, không đổi lời gọi API, không sửa `schema.d.ts`. Chuyển màn = **chỉ** đổi lớp trình bày. |
| D7 | Thêm phụ thuộc chỉ khi có `D-xx` (U1); ghim bản chính xác; **không** bản beta/rc (ngoại lệ ghi trong `D-xx`). |
| D8 | Ngân sách: trang danh sách < 200 KB gzip JS (`04` §10); vượt ⇒ đỏ. |

**DoD U2** (ORCH chạy lại, không tin số vai báo):
`pnpm lint` · `pnpm typecheck` · `pnpm test` · `pnpm build` (gốc repo, mọi gói) exit 0 · `pnpm --filter @ecommerce/ui build-storybook` exit 0
(từ `WP-42`) · `pnpm gen:api` rồi `git diff --exit-code packages/api-client/schema.d.ts` · kiểm thiếu khoá i18n exit 0 (từ `WP-43`) ·
kiểm tương phản token exit 0 (từ `WP-42`) · `pwsh -File tools/kiem-tat-ca.ps1` exit 0 · số ca test **không giảm**.

## 3. Bước `U3` — kiểm (qa · a11y · rev)

1. `qa`: testplan `docs/tests/UI-<số>-testplan.md` (mỗi component/màn trong phạm vi ⇒ ≥ 1 ca; mục "negative control" ≥ 2 dòng có `exit`);
   so ảnh Playwright (story + route) — mọi khác biệt ngoài **danh sách ảnh dự kiến đổi** của đặc tả ⇒ đỏ; E2E + Playwright toàn bộ xanh;
   cập nhật ảnh chuẩn **chỉ** cho ảnh nằm trong danh sách đó.
2. `a11y`: `docs/reviews/UI-<số>-a11y.md` — axe trên story + route trong phạm vi, **cả** theme sáng/tối; bàn phím (Tab/Shift+Tab/Esc/
   Enter/mũi tên), focus nhìn thấy, focus trap + trả focus ở lớp phủ, nhãn form, tương phản AA (chữ ≥ 4.5:1, chữ lớn/UI ≥ 3:1).
   Khuôn verdict giống REV: `VERDICT: PASS | CHANGES`; mức **P1 = critical/serious**, P2 = moderate, P3 = minor.
3. `rev`: `docs/reviews/UI-<số>-review.md` — đối chiếu luật D1–D8, đặc tả, rò rỉ token phía client (`nextjs-admin-rules` A1–A2).
4. P1 ở a11y/rev ⇒ vá (`ds`/`fe`) → test lại (`qa`) → vòng 2; trần 2 vòng/vai rồi `PH dodang`.

**DoD U3:** `check-agent-artifact.ps1 -Kind testplan` (với `-Srs docs/ui/<số>-<tên>.md`) exit 0 · `-Kind review` exit 0 cho **cả**
`UI-<số>-review.md` và `UI-<số>-a11y.md` · negative control đỏ→xanh (`tools/agents/negative-control.ps1`) cho test mới ·
`kiem-tat-ca.ps1` exit 0.

## 4. Khung đặc tả `docs/ui/<số>-<tên>.md` (ux viết)

1. **Mục tiêu, phạm vi & tiêu chí chấp nhận** — liệt kê đúng component/màn; ngoài phạm vi; tiêu chí viết **ngay trong mục 1**
   dạng `**BR-01**`, `**BR-02**`… (đúng khuôn `check-agent-artifact.ps1 -Kind testplan` đọc §1 — QA phủ từng BR).
2. **Đối chiếu Nhanh** — số đo (px, màu, cỡ chữ, mật độ) dẫn `01` §2 / `docs/nhanh/evidence/**`; chỗ cố ý khác và lý do.
3. **Giải phẫu** — cấu trúc, biến thể, kích thước, trạng thái (mặc định/hover/focus/active/disabled/loading/lỗi), theme tối, compact.
4. **Hành vi** — bàn phím, chuột, màn hẹp (≥ 1280px là chính; 1024px không vỡ), quyền (ẩn ≠ kiểm quyền).
5. **Nội dung** — nhãn vi/en, thông điệp rỗng/lỗi, định dạng tiền/số/ngày (`nextjs-admin-rules` U2–U3).
6. **API component** — props, ví dụ dùng, cái **không** hỗ trợ.
7. **Danh sách ảnh dự kiến đổi** — story/route + lý do.
