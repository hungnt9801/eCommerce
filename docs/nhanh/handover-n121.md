# Handover N121 — chuỗi `ui-1` · `U41-2` (WP-41 bước U2 dựng nền UI)

- **Ngày:** 28/11/2026 · **WP:** WP-41 · **Hàng:** `U41-2` (`loai = ui`, bước `U2 dựng`, vai `ds`) · **Phiên:** N121
- **Nhánh:** `feat/ui-wp41-nen` · **Base:** `origin/tich-hop/ui` @ `80ab7a5` · **Trạng thái hàng:** ✅ XONG
- **Vai đã chạy:** `decider` (D-330) · `ds` (dựng + cổng).

## 1. Đã xong

### 1.1 Baseline đo lại (mở phiên)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 2064 đường dẫn backtick, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 49/49 pass |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` | 0 | XANH (điều kiện vào) |
| `pnpm -C apps/admin-web typecheck` / `test` | 0 / 0 | 0 lỗi / **60/60** ca |
| `docker compose ps` | 0 | `ecommerce-postgres` (healthy 5433) + `ecommerce-redis` (healthy 6380) |

### 1.2 Sản phẩm U2 (nền tảng UI) — `ds`

| Nhóm | Tệp | Nội dung |
|---|---|---|
| Khung gói | `packages/ui/{package.json,tsconfig.json,vitest.config.ts,eslint.config.mjs}` + `src/{index.ts,lib/cn.ts,tokens/*.css,test/*}` | `@ecommerce/ui` xuất source TS, `exports` `.`/`tokens.css`/`theme.css`, `sideEffects:["**/*.css"]`, peer react/react-dom + next optional; `cn()` + `cva`; Vitest `jsdom` + Testing Library + `jest-axe`. |
| Tailwind | `apps/admin-web/app/globals.css` | thêm `@import "@ecommerce/ui/tokens.css"` + `@source "../../../packages/ui/src"`. |
| Next | `apps/admin-web/next.config.ts` | `transpilePackages: ["@ecommerce/ui"]` (giữ `output:"standalone"`). |
| Docker | `apps/admin-web/Dockerfile` | **đúng 2 dòng** `COPY packages/ui/package.json` + `COPY packages/ui`. |
| ESLint | `apps/admin-web/eslint.config.mjs` (mới) + script `lint`; `packages/ui/eslint.config.mjs` | flat config `@next/eslint-plugin-next` · `typescript-eslint` · `jsx-a11y` · `react-hooks` · `@eslint/js`; app **warn** (exit 0 trên mã sẵn có), gói `eslint src --max-warnings=0`. |
| Font | `apps/admin-web/app/layout.tsx` | `next/font/google` Inter `subsets:["latin","vietnamese"]`, weight 300–700, `swap`, `--font-inter`; bỏ `@import` Google Fonts. |
| Ghim | `apps/admin-web/package.json` | `next-auth` `"beta"` ⇒ **`5.0.0-beta.32`**; root `package.json` `lint` ⇒ `pnpm -r --if-present lint`. |

**0 thay đổi giao diện:** không sửa `apps/admin-web/components/**`, route, markup; `tokens.css`/`theme.css` rỗng (điền ở WP-42). `pnpm-lock.yaml` do **một luồng này** sinh lại.

### 1.3 Cổng DoD U2 (ORCH tự chạy lại, không tin số vai)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pnpm --filter @ecommerce/ui typecheck` | 0 | `tsc --noEmit` sạch |
| `pnpm --filter @ecommerce/ui test` | 0 | **4/4** ca (2 tệp: `cn` + smoke Testing Library/axe) |
| `pnpm --filter @ecommerce/ui lint` | 0 | **0 warning** (`--max-warnings=0`) |
| `pnpm --filter @ecommerce/admin-web typecheck` | 0 | sạch |
| `pnpm --filter @ecommerce/admin-web lint` | 0 | **0 error / 12 warning** (mã sẵn có, luật `warn`) |
| `pnpm --filter @ecommerce/admin-web test` | 0 | **60/60** (không giảm) |
| `pnpm lint` (gốc) | 0 | chạy cả 2 gói |
| `pnpm gen:api` + `git diff --exit-code packages/api-client/src/schema.d.ts` | 0 | schema **0 diff** (82/82 path) |
| `grep -R "@ecommerce/ui/src/" apps packages` (bỏ node_modules/.next) | — | **0** dòng |
| `grep -REn "#[0-9a-fA-F]{3,8}" packages/ui/src` trừ `src/tokens/` | — | **0** dòng |
| `npm --prefix tools run check:mermaid` | 0 | 214/0 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 chết |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md -Id D-330` | 0 | **DAT** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `U41-2`.** Các mục dưới là **nợ môi trường / có nhận chủ**:

- 🔴 **`pwsh -File tools/kiem-tat-ca.ps1` chưa xanh trên máy này — bị chặn bởi host PID 22404 (`dotnet.exe`) đang giữ khoá DLL** của `src/eCommerce.HttpApi.Host/bin/Debug/net10.0/*.dll`. `dotnet build eCommerce.slnx --no-incremental` chạy **>9,8 phút** rồi bị cắt, log đầy `MSB3061: The file is locked by "dotnet.exe (22404)"` (không có lỗi biên dịch mã nguồn). Đây là **cùng nguyên nhân nợ `N120`** (handover N120 §2). ORCH **không được phép** `Stop-Process` 22404 (classifier chặn: không xác minh được danh tính), và `Lay-TienTrinhECommerce` **không** nhận ra nó vì `CommandLine` của 22404 đọc ra rỗng (filter `-like '*eCommerce*'` = false) ⇒ bước 0 của `kiem-tat-ca.ps1` không tự dọn được. **Chủ:** người dừng host / CI.
- 🟠 **`pnpm build` literal trên Windows host dừng ở bước copy `output:"standalone"` với `EPERM: symlink`** (máy thiếu quyền tạo symlink — không phải lỗi mã). Bằng chứng **compile đã xong**: `✓ Compiled successfully in 40s` + `✓ Generating static pages (4/4)`, chỉ copy trace dùng `fs.symlink` lỗi. CI/Linux không bị. `ds` đã dựng được exit 0 bằng shim tạm (đã xoá, không vào repo). **Chủ:** người/CI.
- 🟡 **`@vitejs/plugin-react 6.3.0`** (D-330) cần peer `vite ^8` nhưng `vitest 3.2.7` đóng gói `vite 7.3.7` ⇒ plugin không khởi động. `ds` dùng **đúng đường lùi D-330** ("esbuild của Vitest": `esbuild:{jsx:"automatic"}`); gói vẫn khai plugin ở devDeps (cho WP-42 khi Vite khớp). `pnpm install` có **1 cảnh báo peer** (không phải lỗi). Quyết ở `U41-3`/review: giữ hay ghim 5.x.
- 🟡 **Storybook hoãn sang `WP-42`** theo `D-330`: không cài 4 gói, không `.storybook/`, `build-storybook` vẫn từ WP-42 (`ui-pipeline` §2 DoD).
- 🟡 **Hai tài liệu U1 (`docs/ui/00`, `01`) vẫn `Draft`/`D-329` TẠM CHỐT** — người duyệt trước merge `tich-hop/ui` → `main` (không đổi trong phiên này).

## 3. Migration đã áp

**Không có migration, 0 thay đổi schema, 0 `pg_dump`.**

## 4. Quyết định mới

| `D-xx` | Trạng thái | Tóm tắt |
|---|---|---|
| **`D-330`** | TẠM CHỐT (DEC, M2) | Bộ gói + bản ghim của `packages/ui` + ESLint flat config 2 gói + `next/font`; **hoãn Storybook sang WP-42**; `eslint-plugin-react-hooks 5.2.0` (khớp peer `eslint-config-next`); **bỏ** `eslint-config-next`/`@eslint/eslintrc` (dựng thẳng từ `@next/eslint-plugin-next 15.5.28`); luật `warn`→`error` ở WP-51; `next-auth 5.0.0-beta.32`. |

`D-330` đã có dòng trong `docs/agents/hang-doi-xem-lai.md`. `check-agent-artifact -Kind decision -Id D-330` = **DAT**.
Sửa định dạng nhỏ trong `D-330` (ORCH): bỏ backtick quanh docs/ops/UI-41-deploy.md (tệp tạo ở `U41-4`) để `kiem-link` PASS — không đổi nội dung quyết định.

## 5. Nợ / rủi ro

| # | Nợ | Mức | Chủ |
|---|---|---|---|
| 1 | `.NET build/kiem-tat-ca` bị host PID 22404 khoá DLL (máy này) | 🔴 chặn gate máy | người dừng host / CI |
| 2 | `pnpm build` standalone EPERM symlink (Windows) — compile ✓ | 🟠 môi trường | người (Developer Mode) / CI |
| 3 | `@vitejs/plugin-react 6.3.0` không dùng được với Vite 7 (esbuild fallback) | 🟡 | `U41-3` review |
| 4 | `pnpm-lock.yaml` +2654 dòng (thêm ESLint/test stack) | 🟡 | review `U41-3` |

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp |
|---|---|---|---|
| 1 | Dừng dev host `dotnet.exe` PID 22404 để `dotnet build`/`kiem-tat-ca.ps1` xanh trên máy này? | **(khuyến nghị)** dừng rồi chạy `pwsh -File tools/kiem-tat-ca.ps1`; hoặc để CI/Linux chạy | Đã ghi bằng chứng `MSB3061` + lệnh; không tự kill (classifier) |
| 2 | `pnpm build` trên Windows cần quyền symlink (standalone)? | **(khuyến nghị)** để CI/Linux; máy dev bật Developer Mode | Đã chứng minh compile ✓ 40s + 4/4 trang |
| 3 | Giữ `@vitejs/plugin-react 6.3.0` (chờ Vite 8) hay ghim 5.x/ bỏ? | **(khuyến nghị)** giữ + dùng esbuild tới WP-42, quyết ở review `U41-3` | Đang chạy bằng esbuild, 4/4 test xanh |
| 4 | Duyệt `D-330` + 2 tài liệu U1 (`D-329`) trước merge `tich-hop/ui` → `main` | **(khuyến nghị)** duyệt ở biên bản người ký cuối chuỗi | Đã có dòng `hang-doi-xem-lai.md` |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `U41-2` (WP-41 bước U2) → mở `U41-3`.**

- Hàng kế: `U41-3` (`loai = ui`, vai `qa · a11y · rev`, depends `U41-2`): harness so ảnh Playwright + **ảnh chuẩn 27 route** (AQ1), báo cáo a11y nền `UI-41-a11y.md` (axe 27 route), `rev` PASS P1=0.
- Cổng vào: `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` = 0 (đã xanh).
- **Trước khi chạy cổng máy của `U41-3`:** xử 2 nợ môi trường §2 (dừng host 22404 + quyền symlink) hoặc chuyển sang CI.

```text
/chay-phien-code chuoi=ui-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/ui-wp41-nen`; base `origin/tich-hop/ui` @ `80ab7a5`.
- Commit phiên này: `feat(ui): N121 - U41-2 WP-41 U2 khung packages/ui ESLint vitest next-font D-330` (SHA ghi ở câu trả lời cuối). **Push** `origin/feat/ui-wp41-nen`; **chưa** đẩy `tich-hop/ui` (chỉ ở `U41-4`). **Không** force/merge/PR/`main`.

---

**Hàng rào agent** (phiên `N121`): `decider` ×1 · `ds` ×1. Cảnh báo hook: 1 cảnh báo "thiếu khối JSON" cho `ds` — **dương tính giả đã biết** (`chanDungKhiThieuJson=false`; khối JSON vẫn có trong báo cáo). `git status` cuối chỉ có `packages/ui/**`, 6 tệp cấu hình app, `package.json`, `pnpm-lock.yaml`, `docs/nhanh/decisions.md`, `docs/nhanh/handover-n121.md`, `docs/agents/bang-phien-ui-1.md`, `docs/agents/hang-doi-xem-lai.md`, `agents/state/**` — đúng vùng.
