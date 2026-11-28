# Bảng phiên chạy — chuỗi nền tảng UI số 1 (`ui-1`)

> Xây nền tảng UI chuyên nghiệp, lâu dài cho Web Admin: design system `packages/ui` (token → primitive → pattern → domain) + Storybook,
> theme sáng/tối, mật độ compact, i18n `vi`/`en`, rồi chuyển dần **37 route** sang. WP-41 → WP-51 ([07 §WP-15+](../nhanh/07-ke-hoach-trien-khai.md)).
> Quyết định: **`D-327`** (người, 28/11/2026) — giữ giao diện **giống Nhanh.vn** · catalog **Storybook** · **UX do agent** ·
> kế hoạch riêng bắt đầu ngay · theme tối + i18n **làm ngay**.
> Dây chuyền: [`.claude/skills/ui-pipeline/SKILL.md`](../../.claude/skills/ui-pipeline/SKILL.md) (hàng `loai = ui`) trong `/chay-phien-code`.
> Nguồn sự thật: `agents/state/phien-ui-1.json`; bảng §4 **sinh bằng script** (`phien.ps1 -Chuoi ui-1 sync`), không sửa tay.

## 0. Chạy tự động

```powershell
pwsh -File tools/agents/chay-tu-dong.ps1 -Chuoi ui-1 -ChiTiet
```

Giống `gd3-1`: mỗi vòng `phien.ps1 -Chuoi ui-1 next` → `claude -p "/chay-phien-code chuoi=ui-1"` → phiên tự chuyển/cắt nhánh WP
(`feat/ui-wpNN-<tên>` từ đỉnh `tich-hop/ui`), làm đúng bước của hàng, kiểm cổng máy, commit + push; hàng `dong-wp` đẩy fast-forward vào
`tich-hop/ui`. Dừng khi: hết việc · tới hàng 👤 (`U44-n`, `U47-n`, `U51-n`) · CI `tich-hop/ui` đỏ · 2 phiên liền không tiến triển · tệp phanh
`artifacts/agents/tu-dong/DUNG`. Chạy tay một phiên: dán `/chay-phien-code chuoi=ui-1`.

## 1. Việc của chủ dự án — một lần trước khi chạy

1. Commit bộ khởi tạo chuỗi (các tệp ở §5) lên một nhánh cắt từ `tich-hop/gd3`, rồi tạo nhánh tích hợp từ commit đó (M3 — người tạo nhánh):
   ```powershell
   git switch -c chore/ui-1-khoi-tao origin/tich-hop/gd3
   git add -- docs/agents/bang-phien-ui-1.md agents/state/phien-ui-1.json .claude/skills/ui-pipeline/SKILL.md `
     .claude/agents/ux.md .claude/agents/ds.md .claude/agents/a11y.md .claude/agents/fe.md .claude/scopes.json `
     .claude/skills/chay-phien-code/SKILL.md .claude/hooks/test/hooks.test.mjs docs/nhanh/07-ke-hoach-trien-khai.md docs/nhanh/decisions.md
   git commit -m "chore(agents): khoi tao chuoi ui-1 nen tang UI (D-327)"
   git push -u origin chore/ui-1-khoi-tao
   git push origin chore/ui-1-khoi-tao:refs/heads/tich-hop/ui
   ```
2. Kiểm: `node --test .claude/hooks/test/hooks.test.mjs` xanh · `pwsh -File tools/agents/phien.ps1 -Chuoi ui-1 check` exit 0 ·
   `pwsh -File tools/agents/phien.ps1 -Chuoi ui-1 next` in `U41-1`.
3. Mở **PR nháp** `tich-hop/ui` → `tich-hop/gd3` để CI chạy (cầu dao đọc lần chạy `ci` của `tich-hop/ui`) — sau hàng `dong-wp` đầu tiên (`U41-4`)
   vì lúc đầu hai nhánh chỉ khác commit khởi tạo.
4. **Không** chạy chuỗi khác chạm `apps/admin-web` (vd `gd3-2` có hàng `F3 FE`) song song với `ui-1` — xem §2.

Trong lúc chạy: hàng 👤 xong thì dán `/chay-phien-code chuoi=ui-1 U44-n đã duyệt, biên bản ở <tệp>` (tương tự `U47-n`, `U51-n`).

## 2. Luật của chuỗi

- **Tuần tự tuyệt đối**, một phiên một lúc. `tich-hop/ui` xếp **trên** `tich-hop/gd3` (`15c46a3`): khi GĐ3 đi tiếp, người rebase/merge
  `tich-hop/gd3` vào `tich-hop/ui` **giữa hai WP** (agent không bao giờ rebase/force). Chuỗi GĐ3 sau cần FE ⇒ chờ `ui-1` xong WP đang chạy,
  và từ `WP-47` trở đi nên viết màn mới bằng `@ecommerce/ui`.
- **Hướng thẩm mỹ cố định:** giống Nhanh bằng **số đo** (`01` §2), không chép asset/mã của Nhanh. Đổi hướng = `D-xx` người chốt.
- **Thư viện:** chỉ gói có `D-xx` ghim bản chính xác; không beta/rc trừ ngoại lệ ghi rõ.
- **Chuyển màn = chỉ đổi lớp trình bày:** giữ `data-testid`, URL, tham số, lời gọi API; E2E không sửa. Màn chưa tới lượt vẫn chạy bằng lớp
  `.nh-*` cũ (ánh xạ sang token từ `WP-42`) cho tới `WP-51`.
- **So ảnh là hợp đồng:** khác biệt ảnh chỉ hợp lệ khi nằm trong "danh sách ảnh dự kiến đổi" của đặc tả `ux`.
- **Đỏ ⇒ không commit phần đỏ** (`stash` có tên, handover), hàng `dodang`; không `reset`/`--force`. Không migration, không đổi `schema.d.ts`.

## 3. Vai và kiến trúc đích

| Tầng | Nội dung | WP |
|---|---|---|
| T0 Token | màu ngữ nghĩa, chữ, khoảng cách, bo góc, bóng · theme sáng/tối · mật độ | 42 |
| T1 Primitive | ~24 primitive trên Radix (shadcn sinh mã vào dự án) | 44 |
| T2 Pattern | DataTable, FilterBar, PageTabs, Pagination · form kit, MoneyInput… · Empty/Error/Loading | 45–46 |
| T3 Domain + màn | shell, picker, badge nghiệp vụ, chart · 37 route | 47–50 |
| Nền + quản trị | ESLint, test + axe, i18n, Storybook, so ảnh, ngân sách bundle, luật mức lỗi | 41, 43, 51 |

| Vai | Việc trong chuỗi | Mới? |
|---|---|---|
| `ux` | kiểm kê, đặc tả `docs/ui/**` (BR, số đo Nhanh, ảnh dự kiến đổi), thuật ngữ vi/en, đánh giá thí điểm | mới |
| `ds` | dựng `packages/ui`, Storybook, nền (ESLint, i18n, theme) | mới |
| `a11y` | axe + bàn phím + tương phản, `UI-<số>-a11y.md` | mới |
| `arch` · `decider` · `docrev` | kiến trúc UI, ghim thư viện (`D-xx`), kiểm đặc tả | có sẵn |
| `fe` | chuyển màn nghiệp vụ sang `@ecommerce/ui` | có sẵn |
| `qa` · `rev` · `ops` | testplan + so ảnh + E2E · review · cổng `kiem-tat-ca` + deploy doc | có sẵn |
| người | `U44-n` xem trước primitive trong Storybook (~10 phút) · `U47-n` xem thẩm mỹ sau thí điểm (~20 phút) · `U51-n` áp bản vá CI + ký đóng chuỗi | — |

## 4. Bảng

<!-- BANG-PHIEN:BAT-DAU -->
| # | Đợt | Nội dung | Vai | Phụ thuộc | Prompt để dán | Phiên | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|---|---|---|---|---|
| `U41-1` | WP-41 | Kiểm kê + kiến trúc: ux docs/ui/00-kiem-ke.md (37 route, 47 component, pattern lặp, lệch số đo Nhanh, nợ a11y thấy được); arch docs/ui/01-kien-truc-ui.md (4 tầng token → primitive → pattern → domain, gói `packages/ui`, quy tắc import, theme/mật độ/i18n, Storybook + so ảnh chạy ở đâu, ngân sách bundle, chuyển dần giữ `data-testid`); decider TẠM CHỐT danh sách gói + bản ghim (ngoại lệ `next-auth` beta, biểu đồ SVG thay Recharts) | ux · arch · docrev · decider | — | `/chay-phien-code chuoi=ui-1 U41-1` | N120 | ✅ Hoàn thành | N120: docs/ui/00 312d + 01 482d; DOCREV DOC-OK P1=0/P2=0 (check-agent-artifact exit 0); decision exit 0; link PASS 345 tep/1074 link; probe WP-41 exit 0 (-TuKiem 19/19); D-328 (probe gate) + D-329 (36 pins + AQ1..AQ9); handover-n120.md · `docs/nhanh/handover-n120.md` |
| `U41-2` | WP-41 | Khung `packages/ui` (`@ecommerce/ui`: build/typecheck/test) + Tailwind `@source`; ESLint flat config (next, react-hooks, jsx-a11y) cho admin-web + `packages/ui`, `pnpm lint` chạy được; Vitest + Testing Library + axe; `next/font` thay `@import` Google Fonts; ghim `next-auth` chính xác. **0 thay đổi giao diện** | ds | U41-1 | `/chay-phien-code chuoi=ui-1 U41-2` | N121 | ✅ Hoàn thành | UI tc0 test4/4 lint0; app tc0 lint0(12w) test60/60; root lint0; gen:api diff0; deep0 hex0; mermaid214/0; links PASS; pnpm build compiled OK (standalone EPERM env); dotnet blocked host PID22404 (env); D-330 · `docs/nhanh/handover-n121.md` |
| `U41-3` | WP-41 | qa: harness so ảnh Playwright + **ảnh chuẩn 37 route** (giao diện hiện tại) — mọi WP sau so với bộ này; a11y: báo cáo nền `UI-41-a11y.md` (axe 37 route, đếm theo mức — chỉ chặn P1 do WP-41 gây ra); rev PASS P1=0 | qa · a11y · rev | U41-2 | `/chay-phien-code chuoi=ui-1 U41-3` | N122 | ✅ Hoàn thành | qa: e2e/visual 27 route + 27 PNG (styled) + testplan BR9/9 + NC exit1->0; a11y UI-41-a11y PASS P1-do-WP41=0 (axe27=43 viol critical2/serious27 nen); rev UI-41-review PASS P1=0 (v1 P1 no-CSS -> v2 CSS guard fix); ORCH visual 54/54 2.2m; typecheck0 test60/60 lint0e12w; check-agent-artifact review x2 DAT decision DAT testplan KHONG-DAT chi SRS heading D-334; dotnet env-blocked D-335; D-331..D-336; SHA 67c26d6 8c7ec22 6fd7aad · `docs/nhanh/handover-n122.md` |
| `U41-4` | WP-41 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops thêm bước `pnpm lint` + so ảnh vào `tools/kiem-tat-ca.ps1`); docs/ops/UI-41-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U41-3 | `/chay-phien-code chuoi=ui-1 U41-4` | N123 | ⛔ Bị chặn | G6 CHUA DAT: -DayDu dung o buoc 10 (bat bien DB): DB dev co 45/46 CusCustomers mo coi (D-53) do probe cu — DEC CHUYEN_NGUOI (xoa du lieu khach = vung cam). Buoc 1-9 XANH tren worktree sach 010335e (build+kiem moc · unit 2691/2691 · integration 171/171 · mermaid 214/0 · cong tai lieu WP-41 XANH · FE typecheck 0 loi + vitest 60/60 · lint 0 error · gen:api khong doi schema.d.ts). Backup artifacts/pg-dump/n124-U41-4.dump (1579639B) + danh sach tools/_tam-kiem/g6-n123/orphans-CusCustomers.txt. Can NGUOI: duyet xoa 45 dong mo coi (SQL o handover-n123 §8.5) hoac chay CI/DB dung-mot-lan. · `docs/nhanh/handover-n123.md` |
| `U42-1` | WP-42 | Đặc tả token v2 docs/ui/02-token.md: tầng gốc (số đo Nhanh `01` §2.1) → tầng ngữ nghĩa (hành động, link, bề mặt, chữ, viền, trạng thái); thang chữ, khoảng cách 4px, bo góc, bóng, z-index, chuyển động; **theme tối** (bảng ánh xạ, tương phản AA); **mật độ** comfortable/compact (dòng bảng 52/40px, input 36/32px); decider ghim Storybook + addon | ux · docrev · decider | U41-4 | `/chay-phien-code chuoi=ui-1 U42-1` | — | ⬜ Chưa bắt đầu |  |
| `U42-2` | WP-42 | `packages/ui` token (`@theme` + `[data-theme=dark]` + `[data-density=compact]`), ThemeProvider (cookie, render phía server, không nháy), công tắc theme/mật độ ở Topbar; ánh xạ lớp `.nh-*` cũ sang token (theme sáng **không đổi**); Storybook (addon a11y, công tắc theme/mật độ/ngôn ngữ) + story trang token; script kiểm tương phản | ds | U42-1 | `/chay-phien-code chuoi=ui-1 U42-2` | — | ⬜ Chưa bắt đầu |  |
| `U42-3` | WP-42 | qa: so ảnh 37 route theme sáng **0 khác** so với ảnh chuẩn WP-41 + bộ ảnh tối/compact mới + ảnh story; a11y: tương phản cả 2 theme; rev | qa · a11y · rev | U42-2 | `/chay-phien-code chuoi=ui-1 U42-3` | — | ⬜ Chưa bắt đầu |  |
| `U42-4` | WP-42 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops thêm `build-storybook` + kiểm tương phản vào `kiem-tat-ca.ps1`); docs/ops/UI-42-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U42-3 | `/chay-phien-code chuoi=ui-1 U42-4` | — | ⬜ Chưa bắt đầu |  |
| `U43-1` | WP-43 | ux bảng thuật ngữ vi/en docs/ui/03-i18n.md (kho, bán, tiền, trạng thái) + quy ước định dạng theo locale (tiền luôn VND); arch: `next-intl` **không** tiền tố locale trên URL (cookie) để giữ route `/[business]/…` và E2E; decider ghim `next-intl`, `dayjs` | ux · arch · docrev · decider | U42-4 | `/chay-phien-code chuoi=ui-1 U43-1` | — | ⬜ Chưa bắt đầu |  |
| `U43-2` | WP-43 | ds: `next-intl` + `messages/{vi,en}.json` + công tắc ngôn ngữ + script kiểm thiếu khoá; `lib/format.ts` theo locale; fe: chuyển chuỗi shell, menu (`lib/menu.ts`), đăng nhập, trang chủ | ds · fe | U43-1 | `/chay-phien-code chuoi=ui-1 U43-2` | — | ⬜ Chưa bắt đầu |  |
| `U43-3` | WP-43 | `lang` đúng theo ngôn ngữ, thiếu khoá = 0, E2E (vi) xanh, so ảnh vi 0 khác ngoài danh sách; bộ ảnh en mới | qa · a11y · rev | U43-2 | `/chay-phien-code chuoi=ui-1 U43-3` | — | ⬜ Chưa bắt đầu |  |
| `U43-4` | WP-43 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops thêm kiểm thiếu khoá i18n vào `kiem-tat-ca.ps1`); docs/ops/UI-43-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U43-3 | `/chay-phien-code chuoi=ui-1 U43-4` | — | ⬜ Chưa bắt đầu |  |
| `U44-1` | WP-44 | Đặc tả docs/ui/04-primitive.md ~24 primitive: Button (chính xanh lá, phụ, viền, ghost, nguy hiểm, link; 3 cỡ; loading), IconButton, Input, Textarea, Select, Combobox, Checkbox, Radio, Switch, Label/FormField, Badge, Avatar, Separator, Skeleton, Spinner, Tabs (badge số), Tooltip, Popover, DropdownMenu, Dialog, AlertDialog, Drawer, Toast, DatePicker; decider ghim Radix/shadcn, `cmdk`, `sonner`, `react-day-picker` | ux · docrev · decider | U43-4 | `/chay-phien-code chuoi=ui-1 U44-1` | — | ⬜ Chưa bắt đầu |  |
| `U44-2` | WP-44 | Nhóm nhập liệu + hiển thị (Button … Spinner, Tabs) — mỗi cái: story sáng/tối/compact + test + axe | ds | U44-1 | `/chay-phien-code chuoi=ui-1 U44-2` | — | ⬜ Chưa bắt đầu |  |
| `U44-3` | WP-44 | Nhóm lớp phủ (Tooltip, Popover, DropdownMenu, Dialog, AlertDialog, Drawer, Toast, Combobox, DatePicker) — focus trap, Esc, trả focus; story + test + axe | ds | U44-2 | `/chay-phien-code chuoi=ui-1 U44-3` | — | ⬜ Chưa bắt đầu |  |
| `U44-4` | WP-44 | Testplan phủ mọi BR của `04-primitive`; bàn phím từng primitive; so ảnh story; rev | qa · a11y · rev | U44-3 | `/chay-phien-code chuoi=ui-1 U44-4` | — | ⬜ Chưa bắt đầu |  |
| `U44-5` | WP-44 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-44-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U44-4 | `/chay-phien-code chuoi=ui-1 U44-5` | — | ⬜ Chưa bắt đầu |  |
| `U44-n` | WP-44 | **Người xem trước primitive** (~10 phút): `git switch tich-hop/ui; git pull; pnpm install; pnpm --filter @ecommerce/ui storybook` — xem ~24 primitive ở sáng/tối/compact, đối chiếu cảm giác Nhanh; ghi `docs/bien-ban-duyet-ui-1-primitive-<ngày>.md` (`Hình thức: DUYỆT` hoặc danh sách cần sửa — có sửa ⇒ ORCH thêm hàng vá trước WP-45) | người | U44-5 | — (việc của người) | — | 👤 Chờ người |  |
| `U45-1` | WP-45 | Đặc tả docs/ui/05-bang-loc.md: DataTable (sort, chọn dòng, ẩn/hiện + nhớ cột, cột cố định, dòng tổng, mật độ, virtual ≥ 200 dòng, 3 trạng thái), Pagination kiểu Nhanh (20/50/100), FilterBar (ô pill, select, khoảng ngày, Lọc, Lưu bộ lọc), PageHeader + PageTabs có badge, BulkActionBar, ColumnChooser; arch: state trên URL (`nuqs`) + Server Component; decider ghim TanStack Table, `@tanstack/react-virtual`, `nuqs` | ux · arch · docrev · decider | U44-n | `/chay-phien-code chuoi=ui-1 U45-1` | — | ⬜ Chưa bắt đầu |  |
| `U45-2` | WP-45 | Dựng các pattern dữ liệu trong `packages/ui` + story với dữ liệu giả 20 / 1000 dòng + test + axe | ds | U45-1 | `/chay-phien-code chuoi=ui-1 U45-2` | — | ⬜ Chưa bắt đầu |  |
| `U45-3` | WP-45 | Đo 1000 dòng cuộn 60 fps + ngân sách bundle trang danh sách < 200 KB gzip; a11y vai trò bảng/sort; rev | qa · a11y · rev | U45-2 | `/chay-phien-code chuoi=ui-1 U45-3` | — | ⬜ Chưa bắt đầu |  |
| `U45-4` | WP-45 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops thêm kiểm ngân sách bundle vào `kiem-tat-ca.ps1`); docs/ops/UI-45-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U45-3 | `/chay-phien-code chuoi=ui-1 U45-4` | — | ⬜ Chưa bắt đầu |  |
| `U46-1` | WP-46 | Đặc tả docs/ui/06-form-trang-thai.md: RHF + Zod (schema dùng chung client/server action), FormField + lỗi field từ `problem+json details[].field`, MoneyInput (số nguyên, hiển thị `150,000`), QuantityInput (3 lẻ), BarcodeInput (quét + Enter), PhoneInput, DateRange, SelectAsync (gọi BFF); EmptyState, ErrorState (thử lại), Skeleton theo trang, ConfirmDialog, `ApiError` → Toast; decider ghim `react-hook-form`, `zod`, `@hookform/resolvers` | ux · docrev · decider | U45-4 | `/chay-phien-code chuoi=ui-1 U46-1` | — | ⬜ Chưa bắt đầu |  |
| `U46-2` | WP-46 | Dựng form kit + trạng thái trong `packages/ui` + story + test (MoneyInput, QuantityInput bắt buộc ca biên) + axe | ds | U46-1 | `/chay-phien-code chuoi=ui-1 U46-2` | — | ⬜ Chưa bắt đầu |  |
| `U46-3` | WP-46 | Testplan form (validate 2 lớp, chống bấm lặp, lỗi field), a11y nhãn/aria-describedby, rev | qa · a11y · rev | U46-2 | `/chay-phien-code chuoi=ui-1 U46-3` | — | ⬜ Chưa bắt đầu |  |
| `U46-4` | WP-46 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-46-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U46-3 | `/chay-phien-code chuoi=ui-1 U46-4` | — | ⬜ Chưa bắt đầu |  |
| `U47-1` | WP-47 | Đặc tả docs/ui/07-shell-thi-diem.md: shell (Topbar 52px, BusinessSwitcher, tìm kiếm + CommandPalette Ctrl+K, menu người dùng có theme/ngôn ngữ/mật độ; Sidebar 2 cấp ~256px thu gọn) + bản đồ chuyển màn kho (`inventory/bill`, `bill/new`, `bill/[id]`, `bill/print`, `product/inventory`, `report/inventory`) sang pattern | ux · docrev · decider | U46-4 | `/chay-phien-code chuoi=ui-1 U47-1` | — | ⬜ Chưa bắt đầu |  |
| `U47-2` | WP-47 | Chuyển shell (Topbar, Sidebar, CommandPalette) sang `@ecommerce/ui`; giữ `data-testid`/URL | fe | U47-1 | `/chay-phien-code chuoi=ui-1 U47-2` | — | ⬜ Chưa bắt đầu |  |
| `U47-3` | WP-47 | Chuyển màn kho: thay `BillTable`, `BillForm`, `BalancesTable`, `SavedFilters`, `ProductPicker` bằng pattern; chuỗi qua `next-intl`; 0 đổi API/hành vi | fe | U47-2 | `/chay-phien-code chuoi=ui-1 U47-3` | — | ⬜ Chưa bắt đầu |  |
| `U47-4` | WP-47 | QA/A11Y/REV như mọi WP + ux ghi **đánh giá thí điểm** (độ giống Nhanh theo số đo, API component cần sửa) vào docs/ui/07-shell-thi-diem.md §8 | qa · a11y · rev · ux | U47-3 | `/chay-phien-code chuoi=ui-1 U47-4` | — | ⬜ Chưa bắt đầu |  |
| `U47-5` | WP-47 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-47-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U47-4 | `/chay-phien-code chuoi=ui-1 U47-5` | — | ⬜ Chưa bắt đầu |  |
| `U47-n` | WP-47 | **Người xem thẩm mỹ** (~20 phút): mở Storybook + màn kho (sáng/tối/compact, vi/en), đối chiếu Nhanh; ghi `docs/bien-ban-duyet-ui-1-thi-diem-<ngày>.md` (`Hình thức: DUYỆT` hoặc danh sách cần sửa — có sửa ⇒ ORCH thêm hàng vá trước WP-48) | người | U47-5 | — (việc của người) | — | 👤 Chờ người |  |
| `U48-1` | WP-48 | Bản đồ chuyển màn đợt 1 docs/ui/08-chuyen-dot-1.md (màn → pattern, chuỗi cần dịch, ảnh dự kiến đổi): Thông báo (5 màn), Khuyến mại (3 màn), Khách hàng (2 màn), Nhân viên | ux · docrev · decider | U47-n | `/chay-phien-code chuoi=ui-1 U48-1` | — | ⬜ Chưa bắt đầu |  |
| `U48-2` | WP-48 | Chuyển Thông báo (templates, templates/new, templates/[id], queue, batches — 2 hộp thoại tự viết → Dialog) + Khuyến mại (list, new, [id], `TreeBuilder`) | fe | U48-1 | `/chay-phien-code chuoi=ui-1 U48-2` | — | ⬜ Chưa bắt đầu |  |
| `U48-3` | WP-48 | Chuyển Khách hàng (list, [id], `CareForm`, `SegmentForm`) + Nhân viên (`settings/staff`, `PermissionMatrix`, `ScopeSelect`) | fe | U48-2 | `/chay-phien-code chuoi=ui-1 U48-3` | — | ⬜ Chưa bắt đầu |  |
| `U48-4` | WP-48 | Testplan đợt 1, so ảnh, E2E đầy đủ, a11y, rev | qa · a11y · rev | U48-3 | `/chay-phien-code chuoi=ui-1 U48-4` | — | ⬜ Chưa bắt đầu |  |
| `U48-5` | WP-48 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-48-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U48-4 | `/chay-phien-code chuoi=ui-1 U48-5` | — | ⬜ Chưa bắt đầu |  |
| `U49-1` | WP-49 | Bản đồ chuyển màn đợt 2 docs/ui/09-chuyen-dot-2.md: Đơn hàng, Sản phẩm, Báo cáo, In, Đăng nhập, Trang chủ; arch: biểu đồ SVG (`StackedBarChart`) thành primitive Chart dùng token trong `packages/ui`; CSS in không phụ thuộc JS | ux · arch · docrev · decider | U48-5 | `/chay-phien-code chuoi=ui-1 U49-1` | — | ⬜ Chưa bắt đầu |  |
| `U49-2` | WP-49 | Chuyển Đơn hàng (manage, [id], `OrderActions`, `OrderStatusBadge`) + Sản phẩm (item, inventory) | fe | U49-1 | `/chay-phien-code chuoi=ui-1 U49-2` | — | ⬜ Chưa bắt đầu |  |
| `U49-3` | WP-49 | ds: Chart primitive; fe: Báo cáo (revenue, inventory, `ExportCsvButton`) + In (`print/bill/[id]`, `inventory/bill/print`) + Đăng nhập + Trang chủ `[business]` | ds · fe | U49-2 | `/chay-phien-code chuoi=ui-1 U49-3` | — | ⬜ Chưa bắt đầu |  |
| `U49-4` | WP-49 | Testplan đợt 2, so ảnh (cả bản in), E2E đầy đủ, a11y, rev | qa · a11y · rev | U49-3 | `/chay-phien-code chuoi=ui-1 U49-4` | — | ⬜ Chưa bắt đầu |  |
| `U49-5` | WP-49 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-49-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U49-4 | `/chay-phien-code chuoi=ui-1 U49-5` | — | ⬜ Chưa bắt đầu |  |
| `U50-1` | WP-50 | Đặc tả POS docs/ui/10-pos.md (phím tắt, quét mã, nhiều tab hoá đơn, mở tab < 100 ms, toàn màn hình) + arch tách `PosTerminal` (1134 dòng) thành khối giỏ / tìm SP / khách / thanh toán / phím tắt | ux · arch · docrev · decider | U49-5 | `/chay-phien-code chuoi=ui-1 U50-1` | — | ⬜ Chưa bắt đầu |  |
| `U50-2` | WP-50 | Tách `PosTerminal` theo arch **không đổi giao diện/hành vi** (0 khác ảnh, E2E bán lẻ xanh) | fe | U50-1 | `/chay-phien-code chuoi=ui-1 U50-2` | — | ⬜ Chưa bắt đầu |  |
| `U50-3` | WP-50 | Chuyển POS bán lẻ + trả hàng (`ReturnForm`) + danh sách hoá đơn (`VoidBillButton`) sang design system | fe | U50-2 | `/chay-phien-code chuoi=ui-1 U50-3` | — | ⬜ Chưa bắt đầu |  |
| `U50-4` | WP-50 | E2E bán lẻ/trả hàng, đo mở tab < 100 ms, bàn phím toàn luồng POS, so ảnh, rev | qa · a11y · rev | U50-3 | `/chay-phien-code chuoi=ui-1 U50-4` | — | ⬜ Chưa bắt đầu |  |
| `U50-5` | WP-50 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu`; docs/ops/UI-50-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U50-4 | `/chay-phien-code chuoi=ui-1 U50-5` | — | ⬜ Chưa bắt đầu |  |
| `U51-1` | WP-51 | docs/ui/README.md (cách dùng, thêm component mới, checklist review UI) + arch cập nhật `01-kien-truc-ui.md` theo thực tế; đề xuất sửa `docs/nhanh/04-web-admin-nextjs.md` §1 vào docs/inbox/UI-51-de-xuat-04.md cho người | ux · arch · docrev | U50-5 | `/chay-phien-code chuoi=ui-1 U51-1` | — | ⬜ Chưa bắt đầu |  |
| `U51-2` | WP-51 | ESLint mức lỗi: cấm `<button>/<input>/<select>` thô và mã màu hex/`style` có màu trong `apps/admin-web`; gỡ lớp `.nh-*` + CSS cũ; gỡ gói thừa; báo cáo bundle | ds | U51-1 | `/chay-phien-code chuoi=ui-1 U51-2` | — | ⬜ Chưa bắt đầu |  |
| `U51-3` | WP-51 | Audit toàn bộ: 37 route × (sáng/tối) × (vi/en) axe 0 P1; so ảnh; E2E + Playwright đầy đủ; rev | qa · a11y · rev | U51-2 | `/chay-phien-code chuoi=ui-1 U51-3` | — | ⬜ Chưa bắt đầu |  |
| `U51-4` | WP-51 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops soạn bản vá CI docs/ops/UI-51-ci-patch.md: lint, storybook, so ảnh, axe, bundle — **không** sửa `ci.yml`); docs/ops/UI-51-deploy.md có đường lùi; `07` §13 một dòng ⇒ đẩy fast-forward vào `tich-hop/ui` | ops · ORCH | U51-3 | `/chay-phien-code chuoi=ui-1 U51-4` | — | ⬜ Chưa bắt đầu |  |
| `U51-n` | WP-51 | **Người** áp bản vá CI vào `.github/workflows/ci.yml`, sửa `04` §1 theo đề xuất, ký biên bản đóng chuỗi; merge `tich-hop/ui` → `main` theo `D-115` ⑤ | người | U51-4 | — (việc của người) | — | 👤 Chờ người |  |
<!-- BANG-PHIEN:KET-THUC -->

## 5. Bộ khởi tạo chuỗi (đã thêm cùng `D-327`)

| Tệp | Thay đổi |
|---|---|
| `agents/state/phien-ui-1.json` · `docs/agents/bang-phien-ui-1.md` | registry + bảng này |
| `.claude/skills/ui-pipeline/SKILL.md` | dây chuyền U1/U2/U3, luật dựng D1–D8, khung đặc tả |
| `.claude/agents/{ux,ds,a11y}.md` · `.claude/scopes.json` | 3 vai mới + vùng ghi; `arch` thêm docs/ui/01-kien-truc-ui.md, `fe` thêm `packages/ui/**`, `qa` thêm `packages/ui/test/**`, `ds` được ghi `pnpm-lock.yaml` |
| `.claude/skills/chay-phien-code/SKILL.md` | hàng `loai = ui`; §0 nhận 3 vai mới |
| `.claude/hooks/test/hooks.test.mjs` · `.claude/agents/fe.md` | ca `F1` đòi đủ 12 vai · `fe` nạp sẵn `ui-pipeline` |
| `docs/nhanh/07-ke-hoach-trien-khai.md` | WP-41…51 trong §WP-15+ ("Không có tài liệu feature" ⇒ probe xanh) |
| `docs/nhanh/decisions.md` | `D-327` |
