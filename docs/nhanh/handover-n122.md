# Handover N122 — chuỗi `ui-1` · `U41-3` (WP-41 bước U3 kiểm: harness so ảnh + a11y nền + review)

- **Ngày:** 29/11/2026 · **WP:** WP-41 · **Hàng:** `U41-3` (`loai = ui`, bước `U3 kiểm`, vai `qa · a11y · rev`) · **Phiên:** N122
- **Nhánh:** `feat/ui-wp41-nen` · **Base:** `origin/tich-hop/ui` @ `80ab7a5` · **Trạng thái hàng:** ✅ XONG (đạt DoD qua bằng chứng thay thế `D-334` cho cổng testplan)
- **Vai đã chạy:** `decider` (D-331…D-336) · `qa` (harness + 27 ảnh chuẩn + testplan + NC; vòng 2 vá P1) · `a11y` (báo cáo nền, vòng 2 refresh) · `rev` (vòng 1 bắt P1; vòng 2 PASS)

## 1. Đã xong

### 1.1 Baseline đo lại (mở phiên)

| Lệnh | Exit | Số đo |
|---|---|---|
| `docker compose up -d` / `ps` | 0 | `ecommerce-postgres` (healthy 5433) + `ecommerce-redis` (healthy 6380) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 2073 đường dẫn backtick, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pnpm --filter @ecommerce/ui typecheck` / `test` / `lint` | 0 / 0 / 0 | 0 lỗi / **4/4** ca / 0 warning |
| `pnpm --filter @ecommerce/admin-web typecheck` / `test` / `lint` | 0 / 0 / 0 | 0 lỗi / **60/60** ca / **0 error · 12 warning** |
| `dotnet build` / `dotnet test` | — | **env-blocked** (host PID 22404 giữ DLL — nợ N121; `D-335`). U41-3 **0 đổi C#** |

### 1.2 Sản phẩm U41-3

| Vai | Tệp | Nội dung + số đo |
|---|---|---|
| `qa` | `apps/admin-web/e2e/visual/**` | Harness so ảnh **tự chứa** (`D-332`): `playwright.visual.config.ts` kế thừa root config (không sửa root), `specs/01-tinh` (19 route) + `02-dong` (8 route động) + `03-axe`, `support/{manhinh,fixtures,axe}.ts`. Phép đo cố định (`D-331`): viewport 1440×900 · dsf 1 · `vi-VN` · `Asia/Ho_Chi_Minh` · `maxDiffPixelRatio 0.001`. |
| `qa` | `apps/admin-web/e2e/visual/__screenshots__/*.png` | **27 ảnh chuẩn** (= 28 `page.tsx` − 1 redirect `/`; `D-329` AQ1). **Sinh lại vòng 2 trên build CÓ CSS** (server 3100). |
| `qa` | `docs/tests/UI-41-testplan.md` | SRS `00-kiem-ke.md`; **BR-01…BR-09 = 9/9** có dòng; mục "Negative control" + bằng chứng thật. |
| `qa` | NC | **NC-1** (vòng 1) + **NC-1v2** (vòng 2)`: đột biến mask dashboard ⇒ **exit 1 (đỏ) → khôi phục → exit 0 (xanh)** trên build CÓ CSS; đã ghi bằng chứng vào testplan. |
| `a11y` | `docs/reviews/UI-41-a11y.md` | Axe-core 4.14.1 **27/27 route** trên build CÓ CSS: **43 vi phạm** = `critical 2` (label @settings/staff 107 node + @pos/bill 3 node) · `serious 27` (`color-contrast` 25 route/571 node + `scrollable-region-focusable` 2) · `moderate 8` · `minor 6`. Tất cả là **nợ nền `P1 (nền)`**; **P1 do WP-41 gây ra = 0** (WP-41 đổi 0 giao diện). `VERDICT: PASS`; `check-agent-artifact -Kind review` = **DAT**. |
| `rev` | `docs/reviews/UI-41-review.md` | **Vòng 1: `CHANGES` P1=1** — 27 ảnh chụp giao diện **KHÔNG CSS** (cổng xanh giả). **Vòng 2: `PASS` P1=0** (P2=3, P3=3); `p1Fixed=true`, `imagesVerified=true`, scope sạch; `check-agent-artifact -Kind review` = **DAT**. |

### 1.3 Cổng DoD U3 (ORCH tự chạy lại)

| Lệnh | Exit | Số đo |
|---|---|---|
| `playwright test -c e2e/visual/playwright.visual.config.ts` (E2E_WEB=3100) | 0 | **54/54 passed** (27 ảnh + 27 axe), **2.2 phút** — ORCH chạy độc lập; QA chạy sạch ×2 trước đó đều 54/54 |
| CSS guard chống "không CSS" (`manhinh.ts` `kiemCss`) | — | Chạy trên server cũ 3000 (không CSS) ⇒ ca **ĐỎ** "Trang KHÔNG nạp stylesheet nào"; server 3100 (CSS `486d…css` 200, 29 002 byte) ⇒ **XANH** |
| `check-agent-artifact -Kind review` `UI-41-review.md` | 0 | **DAT** — verdict PASS · P1=0 |
| `check-agent-artifact -Kind review` `UI-41-a11y.md` | 0 | **DAT** — verdict PASS · P1=0 |
| `check-agent-artifact -Kind testplan -Srs docs/ui/00-kiem-ke.md` | 1 | **KHONG DAT** *chỉ* vì `SRS khong co BR o §1` (heading `## §1.` vs regex `## 1.`). Phần NC **đã đạt** (≥2 `exit`). Bằng chứng thay thế **`D-334`**: testplan 9/9 BR + NC đỏ→xanh (ORCH kiểm tay). |
| `check-agent-artifact -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** — 120 dòng |
| `pnpm --filter @ecommerce/admin-web typecheck` / `test` / `lint` | 0 / 0 / 0 | 0 lỗi / **60/60** / 0 error · 12 warning (không tăng) |
| `pnpm --filter @ecommerce/ui test` | 0 | **4/4** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối, 0 lỗi |

### 1.4 Sự cố P1 đã vá trong phiên (ghi để phiên sau nhớ)

- Server Next cũ **PID 18936** (start 18:08) phục vụ build cũ: HTML trỏ `/_next/static/css/d659…css` nhưng tệp đã bị thay bằng `486d…css` (build 23:38) ⇒ CSS **404/400** ⇒ **mọi ảnh chuẩn vòng 1 không CSS** (form trần, sidebar `<ul>`). rev bắt **P1**; ORCH xác minh bằng ảnh + HTTP.
- **Không kill được PID 18936** (Access denied). ORCH dựng server MỚI đúng build trên **cổng 3100** (`next start -p 3100`, PID 26472) + chạy harness với `E2E_WEB=http://localhost:3100`. `qa` thêm **CSS guard** (đỏ ngay khi trang thiếu stylesheet) và **sinh lại 27 ảnh** trên build có CSS. rev vòng 2 xác nhận ảnh có CSS ⇒ **PASS**.

## 2. Chưa xong / cắt giữa

**Không có việc dở của `U41-3`.** Nợ môi trường / có nhận chủ:

- 🔴 **Server Next cũ cổng 3000 (PID 18936)** không dừng được (Access denied). Cổng visual chạy trên **3100**. **Chủ:** người (restart/kill server 3000 khi tiện).
- 🔴 **`dotnet build`/`dotnet test` bị host PID 22404 giữ DLL** (nợ N121; `D-335`). U41-3 0 đổi C#. **Chủ:** người (dừng host ngoài phiên đo) / CI.
- 🟠 **Cổng `check-agent-artifact -Kind testplan` hỏng** vì SRS `docs/ui/00-kiem-ke.md` dùng `## §1.` còn checker tìm `## 1.`; tệp checker ngoài mọi vùng ghi. **Chủ:** người (`D-334`).
- 🟡 **F-02/F-03 (P2 chấp nhận):** `maskChung` che cả `main table tbody` (DB không reset + fixture thêm dòng ⇒ chiều cao đổi) và `fullPage:false` (thử `true` ⇒ 7–8/27 đỏ). Đường lùi: bật lại khi DB test tất định (`U42-4`/`U51-4`).
- 🟡 **`@vitejs/plugin-react 6.3.0`** peer `vite ^8` vs Vite 7 — giữ + esbuild fallback (`D-336`, P2/nit).
- 🟡 **Ảnh chuẩn sinh trên Windows** — font khác Linux; tái sinh trên CI Linux khi dựng cổng visual (`U42-4`/`U51-4`, `D-331`).

## 3. Migration đã áp

**Không có migration, 0 thay đổi schema, 0 `pg_dump`.**

## 4. Quyết định mới

| `D-xx` | Trạng thái | Tóm tắt |
|---|---|---|
| **`D-331`** | TẠM CHỐT (DEC, M2) | Sinh 27 ảnh chuẩn + theme sáng ngay phiên này trên Windows; tái sinh CI Linux ở `U42-4`/`U51-4`. |
| **`D-332`** | TẠM CHỐT (DEC, M2) | Harness tự chứa `apps/admin-web/e2e/visual/**` kế thừa root config; **không** sửa root config / mở scope. |
| **`D-333`** | TẠM CHỐT (DEC, M2) | Fixture 7 route động qua API thật + **chỉ** ngoại lệ SQL `D-49`; route thiếu dữ liệu ghi "chưa chụp được", không ảnh giả. |
| **`D-334`** | TẠM CHỐT (DEC, M2) | Testplan phủ BR-01…BR-09 + NC ≥2 `exit`, ORCH kiểm tay; nợ **người** sửa 1 regex checker + 1 ca. |
| **`D-335`** | TẠM CHỐT (DEC, M2) | Không kill host PID 22404; `dotnet` ghi env-blocked (chủ người/CI). |
| **`D-336`** | TẠM CHỐT (DEC, M2) | Giữ `@vitejs/plugin-react 6.3.0` + esbuild fallback tới WP-42; rev ghi P2/nit; không đổi lockfile. |

`D-331…D-336` đã có dòng trong `docs/agents/hang-doi-xem-lai.md`. `check-agent-artifact -Kind decision` = **DAT**.

## 5. Nợ / rủi ro

| # | Nợ | Mức | Chủ |
|---|---|---|---|
| 1 | Server Next cũ cổng 3000 (PID 18936) không kill được — visual chạy 3100 | 🔴 | người |
| 2 | `dotnet build/test` bị host PID 22404 giữ DLL (`D-335`) | 🔴 | người/CI |
| 3 | Cổng testplan hỏng do heading `## §1.` (`D-334`) | 🟠 | người |
| 4 | F-02 mask tbody · F-03 fullPage:false (P2 chấp nhận) | 🟡 | `U42-4`/`U51-4` |
| 5 | Ảnh sinh trên Windows (font) | 🟡 | `U42-4`/`U51-4` |
| 6 | `@vitejs/plugin-react 6.3.0` peer Vite 8 | 🟡 | `WP-42`/`WP-51` |

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp |
|---|---|---|---|
| 1 | Dừng/khởi động lại server Next cổng 3000 (PID 18936) để dev dùng build mới? | **(khuyến nghị)** người restart; cổng visual đã dùng 3100 nên không chặn | Đã dựng server 3100 đúng build; harness chạy `E2E_WEB` |
| 2 | Dừng dev host `dotnet.exe` PID 22404 để `dotnet build`/`kiem-tat-ca.ps1 -DayDu` xanh (cần cho `U41-4` G6)? | **(khuyến nghị)** dừng rồi chạy G6; hoặc để CI | Ghi `MSB3061` + `D-335`; không tự kill |
| 3 | Sửa regex checker `## §1.` (D-334) hoặc đổi heading `docs/ui/**` sang `## 1.`? | **(khuyến nghị)** sửa 1 regex + 1 ca `tools/test-cong-kiem.ps1` (chủ người) | Testplan 9/9 BR + NC thật; ORCH kiểm tay |
| 4 | Duyệt `D-331…D-336` + 2 tài liệu U1 (`D-329`) trước merge `tich-hop/ui → main` | **(khuyến nghị)** duyệt ở biên bản người cuối chuỗi | Đã có dòng `hang-doi-xem-lai.md` |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `U41-3` (WP-41 bước U3) → mở `U41-4`.**

- Hàng kế: `U41-4` (`loai = dong-wp`, vai `ops · ORCH`): F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` (ops thêm `pnpm lint` + so ảnh vào cổng), tài liệu deploy UI-41 (tạo ở U41-4) có đường lùi, `07` §13 một dòng ⇒ **đẩy fast-forward `feat/ui-wp41-nen` vào `tich-hop/ui`**.
- **Cảnh báo:** `U41-4` cần `kiem-tat-ca.ps1 -DayDu` xanh, mà `dotnet build` vẫn bị host PID 22404 khoá (`D-335`) ⇒ có thể `PH blocked`/`dodang`; xử nợ #2 (dừng host) hoặc chuyển CI trước.
- Cổng vào `U41-4`: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` = 0 (đã xanh).

```text
/chay-phien-code chuoi=ui-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/ui-wp41-nen`; base `origin/tich-hop/ui` @ `80ab7a5`.
- Commit phiên này: `67c26d6` (harness + 27 ảnh vòng 1 + testplan + `D-331..336`) · `8c7ec22` (NC-1 vòng 1) · `6fd7aad` (**vòng 2** CSS guard + 27 ảnh sinh lại + testplan) · commit cuối (a11y + review + NC-1v2 + handover) — **SHA cuối ghi ở câu trả lời**. **Push** `origin/feat/ui-wp41-nen`; **chưa** đẩy `tich-hop/ui` (chỉ ở `U41-4`). **Không** force/merge/PR/`main`.

---

**Hàng rào agent** (phiên `N122`): `decider` ×1 · `qa` ×3 lượt trả báo cáo (+1 lượt bị cắt ở giới hạn 60 lượt, tiếp ở lượt sau) · `a11y` ×2 · `rev` ×2. Cảnh báo hook "thiếu khối JSON" cho các vai = **dương tính giả đã biết** (`chanDungKhiThieuJson=false`; khối JSON vẫn có trong báo cáo). `git status` cuối: chỉ `apps/admin-web/e2e/visual/**`, `docs/tests/UI-41-testplan.md`, `docs/reviews/UI-41-a11y.md` + `docs/reviews/UI-41-review.md`, `docs/nhanh/decisions.md` + `docs/nhanh/handover-n122.md`, `docs/agents/**`, `agents/state/**` — đúng vùng.
