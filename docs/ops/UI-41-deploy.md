# Triển khai `UI-41` — nền tảng UI Web Admin, bước F6 OPS (29/11/2026)

> Phiên `N123`, nhánh `feat/ui-wp41-nen`, chuỗi `ui-1`, WP-41 hàng `U41-4`. Quyết định liên quan: `D-327` (chuỗi nền tảng UI),
> `D-330` (`U41-2` ESLint + Vitest + axe), `D-331` (27 ảnh chuẩn), `D-332` (harness so ảnh tự chứa), `D-335` (`dotnet` env-blocked).
> Đây là WP **THUẦN FE** — **0 migration, 0 biến môi trường mới, 0 thay đổi .NET**.

## 1. Phạm vi

- Nhánh/PR: `feat/ui-wp41-nen` → `tich-hop/ui` (chuỗi `ui-1`, `D-327` ④; `tich-hop/ui` xếp trên `tich-hop/gd3`).
- **FE (`packages/ui` + `apps/admin-web`):**
  - Khung `packages/ui` (`@ecommerce/ui`: build/typecheck/test) — `U41-2`.
  - ESLint flat config (`next`, `react-hooks`, `jsx-a11y`) cho `apps/admin-web` + `packages/ui`; `pnpm lint` ở gốc =
    `pnpm -r --if-present lint` (phủ cả hai gói) — `U41-2`, `D-330`.
  - Vitest + Testing Library + axe — `U41-2`.
  - Harness so ảnh Playwright `apps/admin-web/e2e/visual/playwright.visual.config.ts` (kế thừa `playwright.config.ts` gốc) +
    **27 ảnh chuẩn** (commit) + **27 ca axe** — `U41-3`, `D-331`, `D-332`.
  - **Ổn định harness so ảnh (test-only, `D-337`/`D-338`):** chuẩn hoá bề rộng ô đếm/badge tab bằng **2 rule CSS** trong `chup()`
    (`e2e/visual/support/manhinh.ts`) — chỉ test, **không** sửa sản phẩm/root config; mask `thead` **theo route** cho **6 route**
    đo được trôi (`#4/#6/#13/#19/#21/#24`); sinh lại **8 ảnh chuẩn** (danh sách ở §5).
- **Cổng máy (OPS, `U41-4`):**
  - `tools/kiem-tat-ca.ps1`: thêm **2 bước** —
    - `FE: lint (pnpm lint)` (BUOC 4b-2, NGAY SAU `FE: typecheck + unit test (Vitest)`): chạy `pnpm run lint` tại gốc repo,
      log `fe-lint.log`, `throw` khi exit != 0 (`D-327`, `U41-2`).
    - `Web Admin: so anh 27 route (UI-41 visual harness)` (BUOC 18, CUỐI khối `-DayDu`, sau E2E Playwright):
      `pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts` tại gốc repo,
      log `visual.log`, `throw` khi exit != 0 (`D-331`, `D-332`).
  - `tools/test-cong-kiem.ps1` NHÓM 1c: thêm 2 khoá `fe-lint.log` + `e2e/visual/playwright.visual.config.ts` vào `$buocBatBuoc`
    để "cổng của cổng" bắt buộc 2 bước mới (chống "bước bị xoá mà cổng vẫn xanh").
- **Migration:** **0** (không `migrations add`, không chạm schema). `DbMigrator` không áp gì mới; **không** cần `pg_dump`.
- **Biến môi trường:** **0** mới (§2). **.NET:** **0** thay đổi (không đổi C#, không đổi `eCommerce.slnx`/`Directory.Packages.props`).
- **KHÔNG sửa** `.github/workflows/ci.yml` (bản vá CI cho chuỗi là việc của `U51-4`/người).

## 2. Biến môi trường

| Tên | Mặc định dev | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

- `E2E_WEB=http://localhost:3100` là **cách đo trong phiên `N123`**: PID 18936 giữ cổng 3000 (không kill, `D-335`) ⇒ harness so ảnh
  chạy trên server mới ở cổng 3100. `E2E_WEB` là tuỳ chọn **sẵn có** của harness/`tools/e2e.ps1`, **không** phải biến của Host;
  không cần khai ở `.env`/secret store.
- `.next/BUILD_ID` + Host DLL là **điều kiện chạy** của harness so ảnh (đã bật ở `tools/e2e.ps1`), không phải biến môi trường.

## 3. Thứ tự triển khai

1. **Merge FE** (PR `feat/ui-wp41-nen` → `tich-hop/ui`): `packages/ui` khung + ESLint/vitest + `apps/admin-web/e2e/visual/**`
   + 2 bước cổng. Không có migration ⇒ `DbMigrator` không có gì để áp; **không** cần `pg_dump`.
2. **Build FE** ở gốc repo: `pnpm build` (tạo `.next/BUILD_ID` cho `next start` của harness) — phải xanh.
3. **Ảnh chuẩn là artefact commit:** 27 PNG trong `apps/admin-web/e2e/visual/__screenshots__/` được **git commit** (không gitignore);
   chỉ thư mục kết quả `e2e/visual/.run/` là gitignore. Cập nhật ảnh chuẩn chỉ cho ảnh nằm trong "danh sách ảnh dự kiến đổi"
   của đặc tả (`ui-pipeline` §3, `D-331`).
4. **Sinh lại ảnh chuẩn đúng danh sách đo được (`D-337`/`D-338`):** với `E2E_WEB=http://localhost:3100`, chạy `--update` sinh lại
   **đúng** ảnh trong danh sách đo được (6 route mask `thead` `#4/#6/#13/#19/#21/#24` + 2 route chỉ trôi badge `#8/#16` = **8 PNG**,
   liệt kê ở §5); ảnh **ngoài** danh sách ⇒ **phục hồi** (`git checkout` bản `U41-3`/`N122`), **không** nhận. Đạt khi **2 lượt sạch
   liên tiếp, giữa 2 lượt chạy 1 lượt E2E đổi dữ liệu** ⇒ cả hai **54/54** (không `--update`).
5. **Cổng đầy đủ:** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` — phải xanh, gồm cả bước `FE: lint` + `Web Admin: so anh 27 route`
   (cần Host DLL + cổng 3000 trống; xem §5 — phiên `N123` **env-blocked** bởi PID 22404/PID 18936, `D-335`).
6. **Không** chạy `dotnet ef database update` (cấm); **không** tắt bước CI nào để xanh.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| 2 bước cổng mới (`FE: lint`, `Web Admin: so anh 27 route`) | `git revert <sha commit cổng>` — 2 tệp: `tools/kiem-tat-ca.ps1`, `tools/test-cong-kiem.ps1` | Cổng trở về như trước `U41-4`. Không chạm dữ liệu/không chạm `.NET`. |
| Harness so ảnh + 27 ảnh chuẩn | `git revert <sha commit U41-3/U41-4>` (hoặc checkout bản trước của `apps/admin-web/e2e/visual/**`) | Ảnh chuẩn là artefact commit ⇒ khôi phục = lấy lại đúng bộ PNG/bản config trước. Xoá cả `e2e/visual/**` cũng an toàn (không có mã nghiệp vụ phụ thuộc). |
| Ổn định harness so ảnh (`D-337`/`D-338`, test-only) | Xoá **2 rule CSS** trong `chup()` (`e2e/visual/support/manhinh.ts`) + gỡ mask `thead` ở **6 ca** trong `specs/01-tinh.spec.ts` + `git checkout 6fd7aad -- apps/admin-web/e2e/visual/__screenshots__/` (8 PNG: `04/06/08/13/16/19/21/24`) | Test-only, **0 dữ liệu/0 sản phẩm**; về đúng bản `U41-3`/`N122`. Trên DB dev trôi, ảnh sinh lại có thể đỏ lại — nợ **DB tất định** `U42-4`/`U51-4`. |
| Khung `packages/ui` + ESLint/vitest | `git revert` các commit `U41-2` (`D-330`) | `apps/admin-web` chạy lại như trước khi có `@ecommerce/ui`; `pnpm lint` gốc quay về trước khi thêm `packages/ui`. |
| Dữ liệu | **Không cần** | 0 migration, 0 `UPDATE`/`DELETE`; **không** cần `pg_dump`. |

- Không revert migration (không có migration). Không tắt bước CI nào để xanh (K-03).

## 5. Cổng (số đo thật cuối phiên `N123`)

> Số **thật cuối phiên** trên `feat/ui-wp41-nen`. `dotnet build`/`-DayDu` **env-blocked** — ghi rõ, **không bịa số** (`D-335`).

| Lệnh | Kết quả |
|---|---|
| `pnpm run lint` (gốc) | `exit 0` · `apps/admin-web: 12 problems (0 errors, 12 warnings)` · `packages/ui`: `eslint src --max-warnings=0` xanh (0) · `D-327`/`D-330` |
| `pnpm --filter @ecommerce/admin-web typecheck` | `exit 0` · `tsc --noEmit` 0 lỗi |
| `pnpm --filter @ecommerce/admin-web test` | `exit 0` · **60/60 tests passed** |
| `pnpm --filter @ecommerce/ui test` | `exit 0` · **4/4 tests passed** |
| `E2E_WEB=http://localhost:3100 pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts` | **54/54** sau `--update` (sinh 8 ảnh) → **54/54** lượt sạch 1 → E2E **8/8** (`E2E_WEB=3100`) → **54/54** lượt sạch 2 → ORCH chạy lại **54/54**. Tổng **4 lượt xanh** trên **2 trạng thái dữ liệu** (trước/sau E2E); 3 lượt sạch **không** `--update` |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | `exit 1` · **52/54 DAT · 2 HONG** — NHÓM 1c **W1 ✅** (0 bước thiếu, gồm 2 khoá mới) · **W2 ✅** (xoá `probe-don-san.ps1` ⇒ đúng 1 thiếu). 2 HỎNG **môi trường, không do thay đổi này**: ① `A)` `dotnet build --no-incremental` trả `1` = **build lock** (host PID 22404 giữ DLL); ② `Y1)` hai tệp **gitignored** `tools/ecommerce-run-chung.ps1`, `tools/ecommerce-start.ps1` dùng API chỉ-Windows (rác môi trường, không thuộc diff) |
| `dotnet build eCommerce.slnx --no-incremental` | **KHÔNG chạy** — **env-blocked**: PID 22404 giữ DLL (`D-335`, chủ người/CI) |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **KHÔNG chạy** — **env-blocked**: PID 22404 giữ DLL + PID 18936 giữ cổng 3000 (`D-335`, chủ người/CI) — **không bịa số** |

**8 ảnh chuẩn sinh lại** (`apps/admin-web/e2e/visual/__screenshots__/`, từ `D-337`/`D-338`):
`04-product-item.png` · `06-order-manage.png` · `08-inventory-bill.png` · `13-pos-bill-list.png` · `16-customer-list.png` ·
`19-report-revenue.png` · `21-promotion.png` · `24-templates.png`.

- Ghi chú môi trường: `pnpm lint` gốc chạy `eslint .` (admin-web) — 12 cảnh báo hiện có **không** làm đỏ cổng (admin-web không
  đặt `--max-warnings=0`); `packages/ui` **có** `--max-warnings=0` và đang sạch.
- Yêu cầu DoD: trước khi push/merge phải chạy được `-DayDu` xanh khi host/CI nhả DLL và cổng 3000 trống.

## 6. Khi hỏng — ai gọi

- **`FE: lint` đỏ** (rule mới / import / unused): **fe**/**ds** sửa mã trong `apps/admin-web`/`packages/ui`; nếu do **cấu hình**
  ESLint ⇒ **ds** (chủ `D-330`); nếu do **đấu dây bước cổng** ⇒ **OPS**.
- **`Web Admin: so anh 27 route` đỏ** (ảnh khác ảnh chuẩn): **qa** đối chiếu "danh sách ảnh dự kiến đổi" của đặc tả —
  ngoài danh sách ⇒ **fe**/**ds** sửa mã (KHÔNG cập nhật ảnh chuẩn); trong danh sách ⇒ **qa** cập nhật ảnh chuẩn.
- **Ảnh đỏ do TRÔI DỮ LIỆU ngoài tab/`thead`** (DB dev không tất định): **KHÔNG** thêm mask/cấu hình để xanh — nâng nợ **DB tất định**
  lên `U42-4`/`U51-4` (`D-338`). **Ảnh đỏ do MÃ**: `qa`/`fe` xử theo "danh sách ảnh dự kiến đổi" của đặc tả (`ui-pipeline` §3).
- **Harness lỗi môi trường** (cổng 3000 bị chiếm, thiếu `.next/BUILD_ID`/Host DLL): **người/ORCH** dừng server cũ / build FE;
  **OPS không kill** PID 22404/18936 (`D-335`).
- **`dotnet build`/`-DayDu` đỏ do host giữ DLL**: **chủ người/CI** (`D-335`); OPS **không** kill host, **không** tắt bước để xanh.
- **`test-cong-kiem` đỏ vì 2 khoá mới** (`fe-lint.log`, `e2e/visual/playwright.visual.config.ts`): ai xoá/đổi tên bước trong
  `tools/kiem-tat-ca.ps1` ⇒ **OPS** khôi phục đúng khối `Buoc '...'`; thiếu dòng trong NHÓM 1c ⇒ **OPS** thêm lại.
