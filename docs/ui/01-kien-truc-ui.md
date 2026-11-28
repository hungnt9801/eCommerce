# UI-41 — Kiến trúc nền tảng UI Web Admin (4 tầng + gói `packages/ui`)

- **Mã:** `UI-41` · **Trạng thái:** `Draft`
- **Ngày:** 28/11/2026 · **Người soạn:** vai `arch` (phiên N120, nhánh `feat/ui-wp41-nen`, hàng `U41-1`)
- **Nguồn:** `D-327` (hướng thẩm mỹ + chuỗi `ui-1`, người chốt 28/11/2026) · `D-328` (cổng vào `probe-cong-tai-lieu.ps1`) ·
  [00-kiem-ke.md](00-kiem-ke.md) (kiểm kê 28 route / 32 component / 15 pattern) · số đo Nhanh
  [01-khao-sat-nhanh-vn.md](../nhanh/01-khao-sat-nhanh-vn.md) §2 · stack [04-web-admin-nextjs.md](../nhanh/04-web-admin-nextjs.md) §1–§3 §9–§10 ·
  luật FE [nextjs-admin-rules](../../.claude/skills/nextjs-admin-rules/SKILL.md) · dây chuyền [ui-pipeline](../../.claude/skills/ui-pipeline/SKILL.md).
- **Phạm vi dùng:** khung kiến trúc cho `WP-42…WP-51`; `ux` viết đặc tả `02-token.md`…`10-pos.md` theo khung này, `ds` dựng
  `packages/ui`, `fe` chuyển màn, `qa` dựng harness so ảnh, `decider` TẠM CHỐT bảng gói + bản ghim (§10).
- ⚠️ **Chưa duyệt bởi người; dùng cho U1.** Không phải đặc tả dựng component — số đo/trạng thái từng primitive thuộc `04-primitive.md`
  (`U44-1`). Bảng gói ở §10 là **ĐỀ XUẤT**, không phải quyết định; chỉ `D-xx` của `decider` mới ràng buộc.

Cách đọc: mọi `file:dòng` dưới đây đã được `Read`/`codegraph_explore` trên cây làm việc ngày 28/11/2026 (đỉnh `99e922e`).
Số **chưa kiểm** được ghi rõ là "chưa kiểm", không suy đoán.

---

## §1. Bối cảnh & mục tiêu

### 1.1 Vì sao cần design system

`apps/admin-web` hiện là **một app Next.js tự viết toàn bộ lớp trình bày**: 28 route trang, 32 component export, 15 nhóm
pattern lặp mà **0 component dùng chung** cho bảng/lọc/tab/phân trang ([00-kiem-ke.md](00-kiem-ke.md) §4 P-01…P-15).
Hệ quả đo được:

- Cùng một khái niệm "bảng danh sách" có **2 bộ số đo** (`.nh-table` 14px/52px so với bảng tự chế 12px) — `app/globals.css:91-115`
  so với `app/[business]/order/manage/page.tsx:298`.
- 15 pattern lặp ⇒ mỗi lần sửa hành vi (ví dụ phân trang vô hiệu đúng cách) phải sửa **8 nơi**, và đã có nơi làm sai (A-08).
- Không có token tầng ngữ nghĩa ⇒ màu hex rải trong component (≥ 20 chỗ, M-16) và **token dùng mà không khai** (`--brand`,
  `--surface`) gây sai màu thật (M-15).

Chuỗi `ui-1` (`WP-41…51`) biến lớp trình bày thành **design system có hợp đồng**, giữ nguyên hành vi nghiệp vụ và giao diện.

### 1.2 Mục tiêu của WP-41 (nền)

1. Chốt **ranh giới 4 tầng** và **gói `packages/ui`** để các WP sau không phải quyết lại chỗ đặt code.
2. Chốt **nơi chạy** Storybook + so ảnh + ngân sách bundle, để `qa`/`ops` có chỗ gắn cổng.
3. Chốt **luật chuyển dần**: giữ selector/URL/hành vi, `.nh-*` sống tới `WP-51`.
4. Đưa **danh sách gói + bản ghim đề xuất** cho `decider` TẠM CHỐT (kèm ngoại lệ `next-auth` beta và biểu đồ SVG thay Recharts).

### 1.3 Ngoài phạm vi

- Không viết code, không tạo `packages/ui`, không cài gói, không đổi `pnpm-lock.yaml` (việc của `U41-2` do `ds`).
- Không đặc tả số đo/trạng thái từng component (`02-token.md`…`10-pos.md`).
- Không đổi API, không đổi `schema.d.ts`, không migration.
- Không chốt bản ghim: §10 chỉ là đề xuất.

---

## §2. Hiện trạng & vấn đề (dẫn chứng, không chép lại bảng)

Tóm tắt từ [00-kiem-ke.md](00-kiem-ke.md); chi tiết và `file:dòng` đầy đủ ở tệp đó.

| Vấn đề | Bằng chứng | Hệ quả cho kiến trúc |
|---|---|---|
| **0 `data-testid`** | `grep -rn "data-testid" apps/admin-web --include=*.tsx --include=*.ts` = **0** (28/11/2026) | "Giữ selector" ở §9 nghĩa là giữ selector **đang có** (Phụ lục B) + **thêm** `data-testid` không phá chúng |
| **Token thiếu & hex rải rác** | thiếu `--radius-sm`, `--primary-active`, `--success-active`, `--table-cell-py/px` (`app/globals.css:8-29`); `var(--brand)`/`var(--surface)` **không khai** — M-15; ≥ 20 hex trong component — M-16 | Tầng T0 phải là **nguồn duy nhất**; component **cấm** hex (luật D2 của `ui-pipeline`) |
| **15 pattern lặp** | bảng P-01…P-15: 11 thanh lọc, 8 phân trang, 12 khối lỗi, 7 cổng quyền, 6 skeleton | T2 phải gom thành pattern có props; mỗi pattern cần **≥ 2 bên gọi thật** trước khi dựng |
| **Nợ a11y 19 mục** | §6 `00-kiem-ke.md` có **22 dòng** A-01…A-22: **19 mục nợ** (A-01…A-19, P2/P3) và 3 dòng không phải nợ (A-20 "0 ảnh", A-21/A-22 tốt); 2 hộp thoại tự viết thiếu focus trap (`components/notification/BatchApprove.tsx:123-124`, `TemplateVersionEditor.tsx:386-387`); `window.prompt` (`components/pos/VoidBillButton.tsx:38`) | Dialog/AlertDialog **phải** lên từ Radix ở T1, không "tự viết lại" |
| **Shell dính server action** | `Topbar` là server component chứa `"use server"` (`components/shell/Topbar.tsx:16-24`); `Sidebar` là client (`Sidebar.tsx:1`) | Shell không thể bê nguyên vào gói: pattern chỉ nhận **slot** (`sidebar`/`topbar`/`children`) |
| **Không có script `lint`** | `apps/admin-web/package.json:5-12` không có `lint`; nhưng gốc `package.json:7` gọi `pnpm --filter @ecommerce/admin-web lint` | Tiền đề "luật import kiểm bằng ESLint" (§5) **chưa tồn tại** — `U41-2` phải dựng, đó là lý do WP-41 là "nền" |
| **Chuỗi tiếng Việt nằm trong component** | nhãn/menu/thông điệp rải khắp `app/**` + `lib/menu.ts` | i18n (`WP-43`) phải đặt ranh giới: **gói không chứa chuỗi nghiệp vụ**, nhận `messages`/`labels` qua props |

---

## §3. Kiến trúc 4 tầng

### 3.1 Luật một chiều

```
domain → pattern → primitive → token
```

Được phép phụ thuộc **xuống**; **cấm** phụ thuộc **lên**. Một `import` ngược chiều biến gói thành app thu nhỏ và khoá
đường chuyển dần (bài học K-10 của `abp-module-rules` áp cho FE: module không phụ thuộc ngược app).

### 3.2 Bảng tầng

| Tầng | Trách nhiệm | Được phụ thuộc | **KHÔNG** được | Ví dụ hiện tại | Gói / vị trí |
|---|---|---|---|---|---|
| **T0 Token** | biến màu ngữ nghĩa, thang chữ, khoảng cách 4px, bo góc, bóng, z-index, chuyển động; `[data-theme=dark]`, `[data-density=compact]` | — (CSS thuần) | import React; chứa từ nghiệp vụ; hex **ngoài** tệp token | `app/globals.css:8-29` (`:root`); `:40` `font-size:14px` | `packages/ui/src/tokens/*.css`, xuất qua `@ecommerce/ui/tokens.css` |
| **T1 Primitive** | hạt cơ bản có a11y: Button, Input, Select, Combobox, Checkbox, Switch, Badge, Tabs, Tooltip, Popover, DropdownMenu, Dialog, AlertDialog, Drawer, Toast, Skeleton, Spinner, DatePicker | T0 + `react` + Radix + `cmdk` (Combobox) | biết nghiệp vụ ("Đơn hàng", "Tồn"), gọi API, import `@/lib/*` hay `schema.d.ts`, tự đặt màu hex | `.nh-btn` (`app/globals.css:45-62`), `.nh-input` (`:74-88`), hình dạng badge (`components/inventory/Badges.tsx:35-66`) | `packages/ui/src/primitives/` (WP-44) |
| **T2 Pattern** | cấu trúc ghép được: DataTable, FilterBar, PageTabs, Pagination, BulkActionBar, ColumnChooser, FormField/MoneyInput/QuantityInput, EmptyState/ErrorState, AppShell khung, Chart | T0 + T1 + lib headless (TanStack…) | gọi API; đọc cookie/token; nhãn nghiệp vụ **cứng** (nhận `labels`/`messages` qua props); tự định nghĩa màu | `BillTable` (`components/inventory/BillTable.tsx:57`), `BalancesTable` (`BalancesTable.tsx:14`), `TemplateTable` (`TemplateTable.tsx:24`), `StackedBarChart` (`StackedBarChart.tsx:26`) | `packages/ui/src/patterns/` (WP-45/46/47/49) |
| **T3 Domain** | ghép theo nghiệp vụ: `OrderStatusBadge` (5 tông màu), `ProductPicker` (gọi API tìm SP), `PosTerminal`, `BillForm`, `PermissionMatrix`, các form khách/khuyến mại | tất cả tầng trên + `@/lib/*`, `schema.d.ts`, server action | được `packages/ui` import ngược; đặt hex; tự viết lại primitive/pattern đã có | `OrderStatusBadge.tsx:16`, `PosTerminal.tsx:49`, `BillForm.tsx:69` | **ở lại** `apps/admin-web/components/**` (xem AQ4) |

### 3.3 Ở lại `apps/admin-web` vs lên `packages/ui`

| Thành phần | Quyết định | Vì sao (có bên gọi thật?) |
|---|---|---|
| `Sidebar`, `Topbar`, khung AppShell | **Tách**: khung trình bày (`AppShell` nhận `sidebar`/`topbar`/`children`) lên T2; phần nối dữ liệu (auth, `MENU`, `getGrantedPermissions`) **ở lại** `app/[business]/layout.tsx:18-41`; server action `signOut` **ở lại** `components/shell/Topbar.tsx:16-25` | `Topbar.tsx:16-25` chứa server action — không chuyển được vào gói không có Next runtime |
| `StockBadge`, `OrderStatusBadge`, `DirectionBadge` | **Ở lại** (T3), dựng trên primitive `Badge` + token | Chỉ `apps/admin-web` dùng; chưa có bên gọi thứ hai. Chuyển lên = thêm bề mặt không có người dùng (bài học #3) |
| `ProductPicker` | **Ở lại** (gọi API + `schema.d.ts`); **phần hộp thoại/kết quả** dùng `Combobox` T1 (`cmdk`) | Tách được: shell combobox dùng chung, nạp dữ liệu ở app |
| `StackedBarChart` | **Lên** T2 (Chart), thuần SVG, nhận `points`/màu token | 2 bên gọi thật: `app/[business]/page.tsx` + `report/revenue/page.tsx`; SVG thuần không phụ thuộc lib |
| `BillTable`, `BalancesTable`, `TemplateTable`, `QueueTable` | **Lên** T2 dưới dạng `DataTable` + cấu hình cột; logic riêng (bulk, CSV, in) tách ra khỏi bảng | ≥ 2 bên gọi mỗi pattern (P-01) |
| `lib/format.ts` | **Lên** `packages/ui/src/lib/format.ts` (hàm thuần, nhận `locale`); app **giữ tệp re-export** `lib/format.ts` | Tránh sửa ~20 điểm gọi ở `fe`; test hiện có (`apps/admin-web/test/format.test.ts`) vẫn xanh |
| `lib/api.ts`, `lib/auth.ts`, `lib/menu.ts` | **Ở lại** tuyệt đối | BFF/token/quyền là ranh giới app (`nextjs-admin-rules` A1–A2) |

---

## §4. Gói `packages/ui`

### 4.1 Tên và cấu trúc

Tên: **`@ecommerce/ui`** (khớp tiền tố `@ecommerce/*` đang dùng: `@ecommerce/admin-web`, `@ecommerce/api-client`).

```
packages/ui/
├─ package.json          # name @ecommerce/ui, private, sideEffects: ["**/*.css"]
├─ tsconfig.json         # strict, noEmit, jsx preserve (đồng bộ apps/admin-web/tsconfig.json)
├─ vitest.config.ts      # environment "jsdom" (khác app: app dùng "node" cho hàm thuần)
├─ src/
│  ├─ tokens/            # T0 — tokens.css, theme.css (dark), density.css
│  ├─ primitives/        # T1 — mỗi primitive 1 tệp + story + test cạnh nhau
│  ├─ patterns/          # T2
│  ├─ lib/               # cn(), format (theo locale), hook thuần không phụ thuộc Next
│  └─ index.ts           # điểm xuất DUY NHẤT
├─ stories/              # truyện tầng (Tokens/*, Patterns/*) nếu không colocate
├─ test/                 # vùng ghi của qa (theo .claude/scopes.json) — ca tích hợp + axe
└─ .storybook/           # main.ts, preview.tsx, decorator theme/density/locale
```

### 4.2 Cách build: xuất source TS + `transpilePackages` (khuyến nghị) hay build bằng `tsup`

| | **A. Xuất source TS** (khuyến nghị) | **B. Build bằng `tsup`/`tsc` ra `dist`** |
|---|---|---|
| Cách làm | `exports["."] = "./src/index.ts"`; `apps/admin-web/next.config.ts` thêm `transpilePackages: ["@ecommerce/ui"]` | `pnpm --filter @ecommerce/ui build` sinh `dist/`; consumer đọc `dist/index.js` + `.d.ts` |
| Tiền lệ trong repo | `packages/api-client/package.json:6-9` đã xuất source TS (`"." : "./src/index.ts"`) — nhưng chỉ **type-only**, nên chưa từng cần `transpilePackages` | — |
| Ưu | 0 bước build, HMR tức thì, Storybook/Vitest đọc cùng nguồn, `tsc --noEmit` phủ luôn gói | Ranh giới rõ, đóng gói được, không phụ thuộc transpile của Next |
| Nhược | Next **phải** transpile gói; quên `transpilePackages` ⇒ lỗi khó hiểu (`JSX not enabled`); không phát hành ngoài được | Thêm bước build vào mọi cổng + `.d.ts` lệch; sửa component phải build lại mới thấy trong app; chậm vòng lặp |
| Khuyến nghị | **A** — đúng quy mô một app, một gói, một người dùng (monorepo nội bộ) | Dùng khi cần phát hành ngoài (không có nhu cầu) |
| Đường lùi | Chuyển sang B: thêm `build` + `exports` trỏ `dist`, giữ nguyên `src` — **không đổi API công khai**, chỉ đổi `package.json` | — |

**Bắt buộc kèm A:** `apps/admin-web/next.config.ts:3-20` thêm `transpilePackages: ["@ecommerce/ui"]`, và `apps/admin-web/Dockerfile:27-36`
thêm `COPY packages/ui/package.json packages/ui/package.json` (tầng cache) + `COPY packages/ui packages/ui` (mã nguồn) — thiếu bước này
job `docker-build` đỏ vì `pnpm install --frozen-lockfile` không thấy gói. `.dockerignore:12` đã loại `**/node_modules` nên không cần sửa.

### 4.3 Exports map và peer deps

```json
{
  "name": "@ecommerce/ui",
  "exports": {
    ".": "./src/index.ts",
    "./tokens.css": "./src/tokens/tokens.css",
    "./theme.css": "./src/tokens/theme.css"
  },
  "sideEffects": ["**/*.css"]
}
```

- **Cấm deep import** (`@ecommerce/ui/src/patterns/DataTable`): mọi thứ đi qua `src/index.ts`.
- Named export từ index để bundler **tree-shake**; `"sideEffects"` chỉ đánh dấu CSS ⇒ component không dùng bị loại.
- `peerDependencies`: `react`, `react-dom` (đúng bản app dùng); `next` là **optional peer** (chỉ vài chỗ dùng `next/link`); `tailwindcss` **không** là peer (chỉ là build-time của app).
- `dependencies` (không phải peer): Radix, `cmdk`, `sonner`, `react-day-picker`, `class-variance-authority`, `clsx`, `tailwind-merge`, TanStack — **đúng phiên bản `decider` ghim ở §10**; gói không tự nâng cấp.
- `@ecommerce/api-client` **không** là dependency của `@ecommerce/ui` (xem §5 hàng 4).

### 4.4 Tailwind v4 `@source`

`apps/admin-web/app/globals.css:2` đang `@import "tailwindcss";`. Tailwind v4 **chỉ quét tệp trong dự án** ⇒ phải khai báo nguồn ngoài:

```css
@import "tailwindcss";
@import "@ecommerce/ui/tokens.css";
@source "../../../packages/ui/src";
```

Đường dẫn tính từ `apps/admin-web/app/globals.css` → gốc repo → `packages/ui/src`. Thiếu `@source` ⇒ class trong gói **không sinh CSS**
(hiện tượng "mất style chỉ ở Storybook/app", khó đoán). Cách chặn: story bắt buộc hiển thị đúng + so ảnh (§7).

### 4.5 Test và Storybook đặt ở đâu

| Loại | Vị trí | Ai sở hữu (`.claude/scopes.json`) |
|---|---|---|
| Unit + axe của từng component | `packages/ui/src/**/*.test.tsx` (colocate) | `ds` (vùng ghi `packages/ui/**`) |
| Ca tích hợp / trang thử của QA | `packages/ui/test/**/*.test.tsx` | `qa` (vùng ghi riêng) |
| Story | `packages/ui/src/**/*.stories.tsx` | `ds` |
| Storybook config | `packages/ui/.storybook/` | `ds` |

`vitest` của gói chạy `environment: "jsdom"` (khác `apps/admin-web/vitest.config.ts:22` đang `node` cho hàm thuần) — hai phạm vi
khác nhau **có chủ đích**: app test logic thuần, gói test tương tác DOM.

---

## §5. Quy tắc import

### 5.1 Bảng luật

| # | Nguồn → Đích | Cho phép? | Cách làm |
|---|---|---|---|
| 1 | `apps/admin-web` → `@ecommerce/ui` (entry) | ✅ | `import { Button, DataTable } from "@ecommerce/ui"` |
| 2 | `apps/admin-web` → `@ecommerce/ui/src/...` (deep) | ❌ | Chỉ entry; deep import làm exports map vô nghĩa và chặn refactor |
| 3 | `packages/ui` → `apps/admin-web` (kể cả alias `@/`) | ❌ | Gói không biết app tồn tại; cần dữ liệu ⇒ nhận qua props/`children` |
| 4 | `packages/ui` → `@ecommerce/api-client/schema` | ❌ | Giữ gói không phụ thuộc DTO; component cần kiểu nghiệp vụ ⇒ ở lại T3 |
| 5 | `packages/ui` T1 → T2/T3 | ❌ | Một chiều `T3 → T2 → T1 → T0` (§3.1) |
| 6 | hai primitive/pattern tự import nhau cùng tầng | ⚠️ chỉ qua `src/index.ts` hoặc tệp `lib/` dùng chung | Tránh vòng import |
| 7 | `apps/admin-web` định nghĩa lại primitive/pattern đã có trong gói | ❌ | `.nh-btn`/`.nh-input` là **lớp tương thích**, không phải nơi thêm biến thể mới |
| 8 | `schema.d.ts` | **chỉ đọc** | Sinh bằng `pnpm gen:api`; PR của FE **không** được đổi (`nextjs-admin-rules` F2) |
| 9 | BFF / token | ❌ trong gói | Chỉ `apps/admin-web/lib/api.ts:32-49` gọi `auth()` + Bearer; gói không đọc cookie/`localStorage` token |

### 5.2 `use client` và server component

- Tệp trong `packages/ui` **dùng hook/Radix state** phải bắt đầu bằng `"use client"`. Hiện `Sidebar.tsx:1` là client, `Topbar.tsx:3` là
  server — gói phải giữ được **cả hai** kiểu người dùng.
- Component **thuần trình bày** (nhận props, không hook) **không** gắn `"use client"` ⇒ dùng được từ RSC và giảm JS client.
- **Không** đặt server action/server-only code trong gói; `Topbar` giữ form `"use server"` ở app (`Topbar.tsx:16-24`) và truyền slot.
- App layout hiện `force-dynamic` (`app/[business]/layout.tsx:9`) ⇒ slot shell render server-safe, không thêm JS client ngoài `Sidebar`.

### 5.3 Luật ESLint (dự kiến WP-51, dựng nền ở `U41-2`)

| Luật | Kiểm cái gì | Giai đoạn |
|---|---|---|
| `no-restricted-imports` | cấm `@ecommerce/ui/src/*`, cấm `@/` từ `packages/ui`, cấm `@ecommerce/api-client` trong gói | `U41-2` (mức cảnh báo) → `WP-51` (mức lỗi) |
| `jsx-a11y` | nhãn form, `role`, `alt` | `U41-2` |
| `react-hooks` | dependency array | `U41-2` |
| cấm hex/rgb trong component | chỉ cho trong `src/tokens/*.css` | `WP-51` |
| `next/no-html-link-for-pages`… | theo preset Next | `U41-2` |

⚠️ **Hiện trạng:** `apps/admin-web/package.json:5-12` **không có** script `lint`, nên `pnpm --filter @ecommerce/admin-web lint`
(gốc `package.json:7`) đỏ ngay hôm nay. `U41-2` phải thêm `lint` + ESLint flat config cho **cả** `apps/admin-web` và `packages/ui`.
**Trước khi có ESLint** (`WP-41…50`), kiểm tạm bằng: `pnpm typecheck` + `grep -R "@ecommerce/ui/src/" apps packages` (0 kết quả) +
`grep -REn "#[0-9a-fA-F]{3,8}" packages/ui/src --include=*.tsx --include=*.css` rồi **loại trừ** `src/tokens/` (kỳ vọng 0 dòng). Ba lệnh này ghi vào testplan của từng WP.
**Hiệu chuẩn lệnh hex (đã đo 28/11/2026):** trên `apps/admin-web/components` mẫu **không** dấu cách sau hex (`#[0-9a-fA-F]{3,8}`) ra **26 dòng** hex thật;
mẫu có dấu cách (`…#[0-9a-fA-F]{3,8} …`) ra **0 dòng** ⇒ lệnh có dấu cách là **no-op**, không dùng. Cách kiểm: chạy đúng hai lệnh trên cùng thư mục và so số dòng.

---

## §6. Theme / mật độ / i18n

### 6.1 Thuộc tính trên `<html>`

`app/layout.tsx:17` hiện là `<html lang="vi">`. Đích:

```tsx
<html lang={locale} data-theme={theme} data-density={density}>
```

- `ThemeProvider` là **server component** đọc `cookies()` (`nh-theme`, `nh-density`, `NEXT_LOCALE`) trong root layout ⇒ thuộc tính có
  ngay trong HTML đầu tiên, **không nháy** (không cần inline script, không cần `next-themes`, không thêm gói).
- Đánh đổi: đọc cookie ở root layout làm **toàn app dynamic**. Hiện **cả** `/login` đã `force-dynamic` (`app/login/page.tsx:13`) ⇒ **không mất trang
  tĩnh nào**; chấp nhận (đăng nhập không cần static). Đường lùi: chỉ đọc cookie trong `app/[business]/layout.tsx:8-9` (khai báo `dynamic` của layout) ⇒
  `/login` về tĩnh — đổi 1 dòng.
- `middleware.ts:8-29` **không** được dùng cho theme/locale: nó đang lo link kiểu Nhanh (`businessId`); trộn thêm việc sẽ khó đo và
  dễ đổi 308 thành 307.

### 6.2 Theme tối

- `[data-theme="dark"]` **chỉ** ghi đè token **ngữ nghĩa**; hex gốc (`#059669`, `#0c83ff`…) khai một lần ở tầng gốc `tokens.css`.
- `.nh-*` cũ phải **không đổi một pixel** ở theme sáng (điều kiện của `U42-3`: ảnh chuẩn 0 khác), nên `.nh-*` được viết lại trên
  token chứ không đổi giá trị.
- Tương phản AA kiểm ở `WP-42` (`U42-3`) bằng script + axe; ngưỡng: chữ ≥ 4.5:1, chữ lớn/UI ≥ 3:1.

### 6.3 Mật độ

- `data-density="comfortable"` (mặc định) | `"compact"`.
- Token điều khiển: `--row-h` 52 → 40px; `--control-h` 36 → 32px; `--cell-py`, `--cell-px` giữ nguyên trừ khi `02-token.md` chốt khác.
- ⚠️ Lệch đã đo: Nhanh `--table-cell-padding-y: .75rem` = **12px** (`01` §2.1) nhưng hiện `padding: 8px 20px` (`app/globals.css:109`).
  Nếu `WP-42` đổi sang 12px thì **ảnh đổi** ⇒ phải nằm trong "danh sách ảnh dự kiến đổi" của `UI-42`, không được lén sửa (M-09).

### 6.4 i18n (`vi` gốc, `en`)

- **Không tiền tố URL** (đúng hàng `U43-1`): locale lấy từ cookie `NEXT_LOCALE`, mặc định `vi`; route giữ `/{business}/…` và `/login`
  ⇒ E2E **không sửa** (điều kiện D5 của `ui-pipeline`). `next-intl` ở chế độ không routing, `NextIntlClientProvider` đặt trong layout.
- `<html lang>` theo locale (A-21 hiện đúng `vi`, giữ hành vi).
- Gói `packages/ui` **không** chứa chuỗi nghiệp vụ: nhận `labels`/`messages` qua props (`DataTable` nhận `labels.empty`…); chuỗi tĩnh
  trong gói (nếu buộc có) là **không** được — đưa về app.
- Định dạng: `packages/ui/src/lib/format.ts` nhận `locale`; `apps/admin-web/lib/format.ts:13` đang khoá `LOCALE = "vi-VN"` ⇒ `WP-43`
  biến nó thành re-export mỏng (giữ chữ ký `qty/amount/int/money/dateTime/dateOnly`) để `apps/admin-web/test/format.test.ts` và ~20
  điểm gọi không phải sửa. **Tiền luôn VND** dù locale `en` (`nextjs-admin-rules` U2 + `D-37`), chỉ đổi cách phân cách.
- `dayjs` chỉ dùng cho tính toán/khoảng ngày phía client; định dạng hiển thị vẫn qua `Intl` để khớp số đo hiện tại.

---

## §7. Storybook + so ảnh

### 7.1 Storybook chạy ở đâu

| Việc | Lệnh | Ở đâu |
|---|---|---|
| Local (người xem `U44-n`, `U47-n`) | `pnpm --filter @ecommerce/ui storybook` → cổng **6006** | `packages/ui/.storybook` |
| CI (cổng) | `pnpm --filter @ecommerce/ui build-storybook` → `storybook-static/` | job `storybook` (thêm ở `U42-4` — `ops`, F6 OPS WP-42) |
| Deploy xem trước | tĩnh, upload artifact — **không** deploy production | `ops` |

- Framework: `@storybook/nextjs-vite` **nếu** gói cần `next/link`/`next/image` (khuyến nghị, đỡ phải cấm import); phương án nhẹ hơn là
  `@storybook/react-vite` nếu §5 chốt "gói không import `next/*`". Bản ghim do `decider` chốt (§10).
- Addon tối thiểu: **a11y** (axe trong story), **themes/globals** (công tắc theme + density + locale), `essentials` (docs/controls).
- Tổ chức story **theo tầng**: `Tokens/*` · `Primitives/*` · `Patterns/*`. **Không** story cho T3 domain (domain ở app, E2E phủ).

### 7.2 Harness so ảnh

- **Dùng lại phần khởi động của `apps/admin-web/e2e`, không dùng lại bộ test**: `playwright.config.ts:62-89` đã có `webServer`
  (Host + `next start`) và `globalSetup` đăng nhập; route `force-dynamic` (`app/[business]/layout.tsx:9`) **bắt buộc** Host + DB sống ⇒
  harness riêng không có server sẽ không chụp được.
- Đề xuất: thêm **project** `visual` trong `apps/admin-web/playwright.config.ts` với `testDir: "./e2e-visual"`, `testMatch` riêng, bật
  bằng env `NH_VISUAL=1` ⇒ 8 luồng `e2e/tests/**` **không đổi**; `apps/admin-web/e2e/support/**` dùng chung (`dangNhap`, `duong`).
- Ảnh chuẩn commit tại `apps/admin-web/e2e-visual/__screenshots__/`; **sinh ảnh chỉ trên CI Linux** (cùng image Playwright) — font
  render khác giữa Windows/Linux là nguồn "flaky ảnh" kinh điển; chạy local chỉ để xem, không cập nhật ảnh.

### 7.3 Ảnh chuẩn: 27 hay 37

- Bảng phiên ghi **37 route** (`docs/agents/bang-phien-ui-1.md` §4); đếm thật là **28 `page.tsx`**, trong đó `app/page.tsx:4` chỉ
  `redirect` ⇒ **27 route chụp được** ([00-kiem-ke.md](00-kiem-ke.md) §2.1, §2.4).
- **Đề xuất:** ảnh chuẩn = **27** (chụp được), ghi chú chênh 37 là số **kế hoạch** (gồm route chưa dựng). Đây chính là `UI-41-Q1`
  của `ux`; `decider` xác nhận ở AQ1 §12. Không sửa số trong bảng phiên.
- **Ai được cập nhật ảnh:** chỉ ảnh nằm trong mục "Danh sách ảnh dự kiến đổi" của đặc tả WP đang chạy (`ui-pipeline` §3.1). Ngoài danh
  sách ⇒ **đỏ**, dừng, báo ORCH. `ds`/`fe` **không** bao giờ commit ảnh chuẩn.
- WP-41: `expectedSnapshotChanges` = **[]** (`00-kiem-ke.md` §7) — `U41-3` chỉ **tạo** bộ ảnh nền từ giao diện hiện tại.

---

## §8. Ngân sách bundle

| Hạng mục | Ngưỡng | Nguồn |
|---|---|---|
| JS trang danh sách (gzip) | **< 200 KB** | [04-web-admin-nextjs.md](../nhanh/04-web-admin-nextjs.md) §10 · `nextjs-admin-rules` §5 |
| T0 token | **0 KB JS** (CSS) | thiết kế tầng |
| Tên gói dùng ở màn danh sách | chỉ T1/T2 đã cần; **không** kéo T3 | §3 |

**Cách đo (2 mức, đều phải ghi rõ nguồn số):**

1. **Mức cổng (rẻ):** `pnpm --filter @ecommerce/admin-web build` (CI đã chạy ở `.github/workflows/ci.yml:126-135`) in bảng route
   kèm `Size`/`First Load JS`. Script `tools/kiem-ngan-sach-bundle.ps1` **do `ops` tạo ở `U45-4`** (vùng ghi `ops`; hiện **chưa có**) đọc output đã lưu,
   đối chiếu danh sách route trang danh sách (product/item, inventory/bill, order/manage, customer/list, pos/bill/list, promotion, notifications/*) với ngưỡng.
   ⚠️ Cổng link `tools/kiem-link-tai-lieu.ps1:142` **chỉ đối chiếu đường dẫn `.md`** ⇒ tệp `.ps1` **không** được cổng tài liệu bảo vệ; tên tệp phải khớp hàng `U45-4`.
   ⚠️ Phải xác nhận **một lần** đơn vị Next in ra là KB đã gzip và ghi vào testplan — nếu không, ngưỡng 200 KB vô nghĩa.
2. **Mức điều tra (khi vượt):** `@next/bundle-analyzer` (**devDependency**, trong `next.config.ts` có điều kiện env) để tìm gói nào phình;
   **không** đưa analyzer vào cổng CI (chậm, sinh HTML lớn).

**Cách chặn:** `U45-4` (`ops`) thêm bước vào `tools/kiem-tat-ca.ps1` + CI; trước đó `U41-2` chỉ cần cổng build xanh. Vượt ngưỡng mà không
có `D-xx` cho phép ⇒ **đỏ** (`ui-pipeline` D8).

**Rủi ro cụ thể:** barrel `@ecommerce/ui` kéo cả Radix nếu bundler không tree-shake ⇒ bắt buộc `"sideEffects": ["**/*.css"]` (§4.3) và
kiểm `First Load JS` sau mỗi WP chuyển màn (`WP-47` trở đi) — đây là lúc ngân sách thật sự bị thử.

---

## §9. Chuyển dần, giữ selector / URL / tham số

### 9.1 Hiện trạng ràng buộc

- **0 `data-testid`** trong `apps/admin-web` (đo 28/11/2026). Vì vậy "giữ `data-testid`" = **giữ các selector đang có**
  ([00-kiem-ke.md](00-kiem-ke.md) Phụ lục B) **và** thêm `data-testid` mới **không phá** chúng.
- Selector không được bỏ: `input[name="username"|"password"]`, `role=button "Đăng nhập"`, `heading "Tổng quan"|"Tạo phiếu kho"|"Bán lẻ tại quầy"`,
  `tr[data-pos-dong]`, `[data-pos-tab]`, `[data-pos-tab-active]`, `[data-pos-tab-moi]`, `[data-pos-dong-bo]`, `div.nh-card`, `div.p-4`,
  `tbody tr` lọc theo text, nhãn nút `Lưu phiếu (Nháp)`/`Ghi sổ phiếu`/`Lưu (F9)`/`In` (`e2e/support/ui.ts:16-18`, `e2e/tests/*.spec.ts`).
- **Buộc phải đổi** một trong các selector trên ⇒ **dừng, báo ORCH** (D5, QA quyết). Không tự sửa `e2e/**` (vùng ghi của `qa`).

### 9.2 Ánh xạ lớp `.nh-*` → token

| Lớp cũ | Hiện tại | Token đích | Thay bằng | WP xoá lớp cũ |
|---|---|---|---|---|
| `.nh-btn` | `height:36px`, `padding:0 14px`, `radius 8px`, viền `--border` (`app/globals.css:45-61`) | `--control-h`, `--radius`, `--border`, `--bg-surface`, `--text` | `Button` (variant `outline`/`ghost`, 3 cỡ 36/32/30 theo số đo đang có) | WP-51 |
| `.nh-btn-primary` | nền `--success` `#059669` (`:63-67`) | `--action-primary`/`--action-primary-hover` | `Button variant="primary"` — **vẫn xanh lá** (nghịch lý `01` §2.1) | WP-51 |
| `.nh-input` | `height:36px`, focus ring `0 0 0 3px rgb(12 131 255/.12)` (`:73-88`) | `--control-h`, `--focus-ring`, `--border` | `Input` (+ trạng thái `disabled`/lỗi — hiện **thiếu**, M-21) | WP-51 |
| `.nh-table` | header 36px/600, ô 14px, `padding:8px 20px`, `height:52px` (`:91-115`) | `--table-header-h`, `--cell-py`, `--cell-px`, `--row-h` | `Table` (T1) + `DataTable` (T2) | WP-51 |
| `.nh-card` | nền trắng, viền 1px, radius 8 (`:118-122`) | `--bg-surface`, `--border`, `--radius` | `Card` | WP-51 |
| `.tabular` | `font-variant-numeric: tabular-nums` (`:124-126`) | utility Tailwind `tabular-nums` | giữ nguyên (không phải design token) | — |

### 9.3 Bảng giai đoạn

| WP | Cái gì chuyển | Không đổi |
|---|---|---|
| **41** | Nền: gói + ESLint + Vitest/axe + `next/font` + ảnh chuẩn | **0 pixel** giao diện |
| **42** | Token v2 + theme tối + density + Storybook (catalog); `.nh-*` viết lại **trên token**, sáng không đổi | Số đo sáng giữ nguyên (ảnh 0 khác); vá `--brand`/`--surface` (AQ3) |
| **43** | Chuỗi → `next-intl`; `format` theo locale; `lang` theo locale | Route/URL; tiền vẫn VND |
| **44** | ~24 primitive; badge/`.nh-btn`/`.nh-input` thay bằng primitive | Kích thước/cỡ chữ hiện có |
| **45** | DataTable/FilterBar/PageTabs/Pagination/BulkActionBar/ColumnChooser; thay 15 pattern | URL state, tham số lọc, `tbody tr` |
| **46** | Form kit (RHF+Zod), MoneyInput/QuantityInput, Empty/Error/Loading, ConfirmDialog | Hành vi submit, mã lỗi hiển thị |
| **47** | AppShell (slot) + CommandPalette; **thí điểm màn kho** (`inventory/bill*`, `product/inventory`, `report/inventory`) | Luồng nghiệp vụ; selector kho |
| **48** | Thông báo (5 màn) + Khuyến mại (3) + Khách hàng (2) + Nhân viên | API, URL `?tab=` |
| **49** | Đơn hàng, Sản phẩm, Báo cáo, In, Đăng nhập, Trang chủ; Chart primitive | CSS in không phụ thuộc JS |
| **50** | POS: tách `PosTerminal` (1134 dòng) thành giỏ/tìm SP/khách/thanh toán/phím tắt | `data-pos-*`, F1–F10, IndexedDB |
| **51** | ESLint mức **lỗi** (cấm deep import, cấm hex), xoá `.nh-*`, đóng chuỗi | — |

Nguyên tắc: mỗi WP chỉ đổi **lớp trình bày**; màn chưa tới lượt vẫn chạy `.nh-*` tới `WP-51`.

---

## §10. Danh sách gói + bản ghim — **ĐỀ XUẤT cho `decider` TẠM CHỐT**

> ⚠️ **Không phải quyết định.** `decider` chốt thành `D-xx` (≥ 1 dòng) theo `ui-pipeline` §1.4: mỗi gói cần **tên + bản ghim chính xác +
> lý do + gzip ước lượng + đường lùi**. Bản ghi "**có (cài đặt)**" là số **đo được** trên cây làm việc 28/11/2026; bản ghi
> "**cần xác minh (npm view)**" là **chưa** xác minh được trong phiên này (shell của vai `arch` chỉ đọc, không chạy `npm view`) —
> **không** được coi là bản tồn tại.

### 10.1 Gói đang cài (đo được) — phải chuyển sang ghim chính xác ở `U41-2`

| Gói | Bản ghim đề xuất | Đang khai trong `package.json` | Dùng cho | Tầng | Đã có? | Gzip ước tính | Đường lùi |
|---|---|---|---|---|---|---|---|
| `next` | `15.5.28` | `^15.5.0` (`apps/admin-web/package.json:16`) | framework, App Router, RSC | nền | có | không tính (server) | revert `next.config.ts` + lockfile |
| `react` | `19.3.1` | `^19.0.0` (`:18`) | runtime UI | nền | có | ~2–3 KB | cùng lockfile |
| `react-dom` | `19.3.1` | `^19.0.0` (`:19`) | render client | nền | có | ~45 KB (client, ước lượng thô) | cùng lockfile |
| `next-auth` | **`5.0.0-beta.32`** | **`beta`** (`:17`) — **trôi**, vi phạm "ghim chính xác" | OIDC/OpenIddict, BFF token | nền | có | 0 (chỉ server) | hạ về bản cũ hơn 1 minor; **ngoại lệ beta có `D-xx`** |
| `@phosphor-icons/react` | `2.1.10` | `^2.1.7` (`:15`) | icon (giống Nhanh) | T1 | có | ~1–3 KB mỗi icon dùng (tree-shake) | bỏ icon ⇒ chữ; không thêm `lucide` |
| `tailwindcss` / `@tailwindcss/postcss` | `4.3.3` | `^4.1.0` | utility + `@source` | T0/build | có | 0 JS; CSS đầu ra ước lượng 20–35 KB | giữ v4; không về v3 (v4 đã dùng) |
| `typescript` | `5.9.3` | `^5.7.3` | type | dev | có | 0 | — |
| `vitest` | `3.2.7` | `^3.2.7` | test đơn vị | dev | có | 0 | — |
| `@playwright/test` | `1.64.1` | `^1.64.1` | E2E + so ảnh | dev | có | 0 | — |

### 10.2 Gói mới đề xuất (chưa cài) — `decider` chốt

| Gói | Bản ghim đề xuất | Dùng cho | Tầng | Đã có? | Gzip ước tính | Đường lùi |
|---|---|---|---|---|---|---|
| `radix-ui` (gói hợp nhất) **hoặc** từng `@radix-ui/react-{dialog,dropdown-menu,popover,tabs,tooltip,checkbox,switch,select,radio-group,separator,avatar}` | cần xác minh (npm view) — `decider` chốt | nền a11y cho T1 (focus trap, Esc, trả focus) | T1 | chưa | ~3–10 KB mỗi primitive | gói hợp nhất ⇄ tách lẻ không đổi API dùng |
| `shadcn` (CLI `shadcn`) | cần xác minh (npm view) — `decider` chốt | **sinh mã** primitive vào repo | T1 (dev) | chưa | **0 runtime** (devDependency) | bỏ CLI, giữ mã đã sinh |
| `class-variance-authority` | cần xác minh (npm view) | biến thể primitive (variant/size) | T1 | chưa | ~2 KB | thay bằng hàm `cn` + nhánh thường |
| `clsx` | cần xác minh (npm view) | ghép class có điều kiện | lib | chưa | < 1 KB | tự viết 5 dòng |
| `tailwind-merge` | cần xác minh (npm view) | gộp class xung đột (`p-2` vs `p-4`) | lib | chưa | ~7 KB (ước lượng) | bỏ ⇒ phải cẩn thận thứ tự class |
| `@tanstack/react-table` | cần xác minh (npm view) | DataTable headless (sort, chọn dòng, cột) | T2 | chưa | ~12 KB (ước lượng) | quay lại bảng tự viết như `BillTable` |
| `@tanstack/react-virtual` | cần xác minh (npm view) | ảo hoá ≥ 200 dòng (60 fps) | T2 | chưa | ~4 KB (ước lượng) | ngưỡng trang 50 dòng như hiện tại |
| `@tanstack/react-query` | **đề xuất HOÃN** (xem AQ6) | cache phía client, invalidate | T2/domain | chưa | ~13 KB (ước lượng) | không thêm = 0 KB |
| `nuqs` | cần xác minh (npm view) — **chờ `U45-1`** | state lọc/tab/page trên URL | T2 | chưa | ~6 KB (ước lượng) | `searchParams` + `<form method=get>` như hiện tại |
| `next-intl` | cần xác minh (npm view) | i18n `vi`/`en`, không tiền tố URL | nền | chưa | ~12 KB (ước lượng, chủ yếu server) | quay lại chuỗi hằng (như hiện tại) |
| `dayjs` | cần xác minh (npm view) | tính khoảng ngày/giờ phía client | lib | chưa | ~3 KB + locale ~1 KB | `Intl`/`Date` như hiện tại |
| `react-hook-form` | cần xác minh (npm view) | form lớn | T2 | chưa | ~9 KB (ước lượng) | `useState` + `zod` thuần (như hiện tại) |
| `zod` | cần xác minh (npm view) | validate dùng chung client/server action | lib | chưa | ~13 KB (ước lượng) | validate tay từng form |
| `@hookform/resolvers` | cần xác minh (npm view) | cầu nối Zod ↔ RHF | T2 | chưa | ~1 KB | gọi `schema.safeParse` trong `onSubmit` |
| `cmdk` | cần xác minh (npm view) | Combobox/CommandPalette Ctrl+K | T1/T2 | chưa | ~7 KB (ước lượng) | `ProductPicker` hiện tại (thiếu role listbox — A-12) |
| `sonner` | cần xác minh (npm view) | Toast | T1 | chưa | ~5 KB (ước lượng) | khối `role=alert` tự viết (như hiện tại) |
| `react-day-picker` | cần xác minh (npm view) | DatePicker/khoảng ngày | T1 | chưa | ~10 KB (ước lượng) | `<input type="date">` |
| `storybook` + `@storybook/nextjs-vite` (hoặc `@storybook/react-vite`) + `@storybook/addon-a11y` + `addon-essentials`/`addon-themes` | cần xác minh (npm view) — **catalog đã chốt ở `D-327` ②**, bản cụ thể do `decider` | catalog component | dev | chưa | **0 runtime** | bỏ Storybook ⇒ mất xem trước `U44-n`/`U47-n` (không có đường lùi thay thế) |
| `jsdom` | cần xác minh (npm view) — **bắt buộc cho `environment: "jsdom"`** | DOM ảo cho Vitest test component | dev (devDependency) | chưa | 0 runtime | đổi `environment` sang `node` + chỉ test hàm thuần |
| `@vitejs/plugin-react` | cần xác minh (npm view) | biên dịch JSX + Fast Refresh cho Vitest | dev (devDependency) | chưa | 0 runtime | `esbuild` sẵn có của Vitest (không Fast Refresh) |
| `@testing-library/react` | cần xác minh (npm view) | test component | dev | chưa | 0 | — |
| `@testing-library/user-event` | cần xác minh (npm view) | mô phỏng bàn phím/chuột | dev | chưa | 0 | — |
| `vitest-axe` **hoặc** `jest-axe` (+ `axe-core`) | cần xác minh (npm view) | axe trong test | dev | chưa | 0 runtime | a11y chỉ kiểm ở `U*‑3` bằng Playwright |
| `@next/bundle-analyzer` | cần xác minh (npm view) | điều tra bundle khi vượt ngưỡng | dev | chưa | 0 runtime | đọc thẳng output `next build` |

### 10.3 Biểu đồ: SVG thuần, **không** Recharts

| | Quyết định |
|---|---|
| Hiện trạng | `StackedBarChart.tsx:26-101` — SVG thuần 720×220, `role="img"` (`:45`), `<title>` từng cột (`:73-76`), cờ `aMau`/`bMau` truyền vào |
| Đề xuất | **Giữ SVG thuần**, chuyển thành pattern `Chart` ở `WP-49` (`U49-1`/`U49-3`), nhận màu **token** thay chuỗi hex |
| Lý do bundle | Recharts kéo `d3-*` + `react-smooth` — bậc **100–300 KB** (ước lượng, chưa đo) cho **một** loại biểu đồ trong 2 màn ⇒ đụng ngưỡng 200 KB |
| Lý do a11y | SVG hiện đã có `role="img"` + `aria-label` + `<title>` từng cột; vẫn nợ A-17 (thiếu bảng/mô tả tổng) — sửa rẻ hơn là cấu hình a11y của lib |
| Lý do in | Trang in (`print/bill/[id]`) không tải JS; SVG server-render in được, lib chart cần hydrate |
| Hệ quả | Chỉ có **một** kiểu biểu đồ. Cần loại thứ hai (đường/tròn) ⇒ mở `D-xx` riêng, **không** mặc định thêm Recharts |
| Đường lùi | Thêm Recharts sau vẫn được (pattern `Chart` giữ props `points`); khi đó phải đo lại ngân sách ở màn đó |

---

## §11. Rủi ro & đường lùi

| # | Rủi ro | Dấu hiệu | Giảm thiểu | Đường lùi |
|---|---|---|---|---|
| R1 | Ranh giới tầng rò rỉ (domain lọt vào gói) | `packages/ui` import `@/`, `schema.d.ts`, hoặc chuỗi nghiệp vụ | ESLint `no-restricted-imports` (`U41-2` cảnh báo → `WP-51` lỗi) | gỡ import, chuyển component về app — 1 tệp |
| R2 | Hex/màu cứng quay lại | `#` trong component | luật D2 + grep tạm (§5.3) | sửa về token; ảnh chuẩn bắt được |
| R3 | Bundle vượt 200 KB | `First Load JS` màn danh sách | tree-shake, `sideEffects`, hoãn `react-query` | bỏ gói gây phình; xem analyzer |
| R4 | Ảnh chuẩn "flaky" do font/OS | ảnh lệch vài pixel chỉ trên máy dev | chỉ sinh/cập nhật ảnh trên CI Linux | bỏ so ảnh ở local, chỉ cổng CI |
| R5 | `next-auth` beta vỡ khi nâng `next` | lỗi type/runtime auth | ghim `5.0.0-beta.32`, nâng chỉ khi có `D-xx` | hạ `next` về bản đã kiểm; hoặc hạ `next-auth` |
| R6 | Tailwind không quét gói | class mất CSS | `@source` + story + so ảnh | thêm `@source` (1 dòng) |
| R7 | Quên đưa `packages/ui` vào Docker | job `docker-build` đỏ | §4.2 nêu đúng 2 dòng `COPY` | thêm `COPY`, rebuild |
| R8 | E2E đỏ vì selector đổi | luồng 01–08 đỏ | §9.1: chỉ thêm `data-testid`, không bỏ selector | revert commit WP; dừng, báo ORCH |
| R9 | i18n thiếu khoá | chuỗi hiện key thô | script kiểm thiếu khoá (`WP-43`) | mặc định `vi` bằng chuỗi gốc |
| R10 | Chuyển màn đổi hành vi nghiệp vụ | test/ hành vi khác | D6: chỉ đổi lớp trình bày; không đổi API | revert theo WP |

**Đường lùi tổng:** toàn chuỗi nằm trên `tich-hop/ui` **chưa vào `main`** ⇒ dừng bất kỳ lúc nào, không ai bị ảnh hưởng. Màn chưa chuyển
vẫn chạy `.nh-*`. Mỗi WP là một nhánh xếp chồng ⇒ revert theo WP. **Không migration, không đổi schema, không đổi API.**
Rủi ro duy nhất khó lùi: Storybook trở thành nơi người duyệt (`U44-n`, `U47-n`) — huỷ nó là huỷ một điểm nghiệm thu, cần `D-xx`.

---

## §12. Câu hỏi mở

Mỗi câu: lựa chọn · khuyến nghị · tin cậy · cái cần (`D-xx`). UI **không** thuộc danh sách đỏ ⇒ `decider` được TẠM CHỐT khi
DOCREV `DOC-OK` (`ui-pipeline` §1.4), trừ câu đụng cam kết ảnh/biên bản.

| # | Câu hỏi | Lựa chọn | Khuyến nghị | Tin cậy | Cần |
|---|---|---|---|---|---|
| **UI-41-AQ1** | Ảnh chuẩn chụp **bao nhiêu** route: 37 (kế hoạch) hay 27 (chụp được)? (tiếp `UI-41-Q1` của `ux`) | (a) 37; (b) **27** (28 trang − 1 redirect) | **(b)** — không chụp được route không tồn tại; ghi chú chênh ở `00-kiem-ke.md` §2.4 | Cao | DEC |
| **UI-41-AQ2** | Baseline "component" cho các WP sau: 32 export, 39 (+7 cục bộ), hay 47 (kế hoạch)? (tiếp `UI-41-Q2`) | (a) **32 export**; (b) 39; (c) 47 | **(a)** làm "component dùng chung"; nhóm cục bộ là nợ gộp ở `WP-44/45` | Cao | DEC |
| **UI-41-AQ3** | Token `--brand`/`--surface`/`--surface-alt` **thiếu** (M-15, sai màu thật ở 4 tệp): vá ở `WP-41` hay `WP-42`? (tiếp `UI-41-Q3`) | (a) vá `WP-41` ⇒ **phá** cam kết "0 đổi giao diện" của `U41-3`; (b) **`WP-42`** định nghĩa `--brand = --primary` / `--surface = --bg-surface` rồi vá, ảnh đổi nằm trong danh sách `UI-42` | **(b)** — giữ `WP-41` ảnh-0-khác; nhưng phải ghi rõ trong đặc tả `02-token.md` | Trung bình | **DEC** (đụng cam kết ảnh) |
| **UI-41-AQ4** | T3 domain đặt ở đâu: `packages/ui/src/domain` hay `apps/admin-web/components/**`? | (a) **ở app**; (b) ở gói | **(a)** — chưa có bên gọi thứ hai; badge/picker dựng trên primitive, chuyển lên sau nếu có app thứ hai | Trung bình | DEC |
| **UI-41-AQ5** | Cách build gói: xuất source TS + `transpilePackages` hay `tsup` ra `dist`? | (a) **source TS**; (b) `tsup` | **(a)** — 0 bước build, vòng lặp nhanh; đổi sang (b) không phá API | Trung bình | DEC |
| **UI-41-AQ6** | Có thêm `@tanstack/react-query` ngay (`D-327` ⑥ có nêu) hay hoãn? | (a) thêm ngay; (b) **hoãn tới khi có màn cần cache client thật** | **(b)** — hiện `lib/api.ts:32-49` gọi server-side (`cache:"no-store"`); thêm bây giờ là 0 bên gọi | Cao | DEC |
| **UI-41-AQ7** | Primitive lấy từ đâu: gói hợp nhất `radix-ui` + `shadcn` CLI, hay từng `@radix-ui/react-*` + tự viết? | (a) **`radix-ui` + shadcn sinh mã**; (b) từng gói + tự viết | **(a)** — ít bề mặt tự viết a11y hơn, đúng `D-327` ⑥; mã sinh commit vào repo | Trung bình | DEC |
| **UI-41-AQ8** | Storybook: framework `@storybook/nextjs-vite` hay `@storybook/react-vite`; đặt ở `packages/ui/.storybook`? | (a) **nextjs-vite** + `packages/ui`; (b) react-vite + cấm gói import `next/*` | **(a)** — giữ được `next/link` trong gói (`Sidebar` hiện dùng); (b) chỉ khi chốt cấm `next/*` | Trung bình | DEC |
| **UI-41-AQ9** | Mật độ compact có **đổi** padding ô 12px theo Nhanh (M-09) ở `WP-42` không? | (a) đổi 12px ⇒ ảnh đổi (phải vào danh sách `UI-42`); (b) **giữ 8px** tới khi có `D-xx` | **(b)** — `WP-42` cam kết "sáng 0 khác"; đổi kèm ảnh là quyết định thị giác của người (biên bản `U44-n`) | Trung bình | DEC / người |

Ghi chú: Q4 (đếm `lib/menu.ts`) và Q5 (cao tab/nút chưa đo) của `00-kiem-ke.md` §8 **không** chạm ranh giới — đã có khuyến nghị
trong tệp đó, không lặp ở đây.
