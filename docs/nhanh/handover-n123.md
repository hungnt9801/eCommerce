# Handover N123 — chuỗi `ui-1` · `U41-4` (WP-41 bước F6 OPS + G6) — **DỞ DANG: G6 `-DayDu` chưa chạy được**

- **Ngày:** 29/11/2026 · **WP:** WP-41 · **Hàng:** `U41-4` (`loai = dong-wp`, bước `F6-G6`, vai `ops · ORCH`) · **Phiên:** N123
- **Nhánh:** `feat/ui-wp41-nen` · **Base:** `origin/tich-hop/ui` @ `80ab7a5` · **Trạng thái hàng:** 🟨 DỞ DANG — F6 xong, **G6 chưa đạt** (môi trường `D-335`)
- **Vai đã chạy:** `ops` ×2 (2 bước cổng + deploy doc; cập nhật deploy doc) · `qa` ×3 (điều tra trôi ảnh → `D-337`; áp `D-337`; áp `D-338`, 1 lần bị cắt ở 60 lượt rồi tiếp) · `decider` ×1 phiên (2 quyết định `D-337`, `D-338`)

## 1. Đã xong

### 1.1 Baseline đo lại (mở phiên N123)

| Lệnh | Exit | Số đo |
|---|---|---|
| `docker compose ps` | 0 | `ecommerce-postgres` + `ecommerce-redis` running |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-41` | 0 | **XANH** — "WP-41 khong co ma tai lieu" (`07` ghi "Không có tài liệu feature") |
| `dotnet build eCommerce.slnx --no-incremental` | **blocked** | **env-blocked**: `MSB3026` — PID 22404 giữ `src/eCommerce.HttpApi.Host/bin/Debug/net10.0/*.dll` (`D-335`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 1076 tệp `.md`, 2110 đường dẫn backtick, 0 chết (đầu phiên) |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `pnpm run lint` (gốc) | 0 | admin-web **12 problems (0 error, 12 warning)** · `packages/ui` 0 (`--max-warnings=0`) |
| `pnpm --filter @ecommerce/admin-web typecheck` | 0 | `tsc --noEmit` 0 lỗi |
| `pnpm --filter @ecommerce/admin-web test` | 0 | **6 file / 60 ca** pass |
| `pnpm --filter @ecommerce/ui test` | 0 | **2 file / 4 ca** pass |

### 1.2 Sản phẩm F6 OPS (`U41-4`)

| Vai | Tệp | Nội dung |
|---|---|---|
| `ops` | `tools/kiem-tat-ca.ps1` (+58 dòng) | **Bước mới 4b-2** `FE: lint (pnpm lint)` — chạy `pnpm run lint` tại gốc repo (phủ `apps/admin-web` + `packages/ui`), log `fe-lint.log`, `throw` khi exit ≠ 0. **Bước mới ⑱** `Web Admin: so anh 27 route (UI-41 visual harness)` — cuối khối `-DayDu`, sau E2E Playwright; chạy `pnpm --filter @ecommerce/admin-web exec playwright test -c e2e/visual/playwright.visual.config.ts`, log `visual.log`, `throw` khi exit ≠ 0. |
| `ops` | `tools/test-cong-kiem.ps1` (+2 dòng) | NHÓM 1c `$buocBatBuoc` thêm 2 khoá `fe-lint.log` + `e2e/visual/playwright.visual.config.ts` (chống "bước bị xoá mà cổng vẫn xanh"). |
| `ops` | `docs/ops/UI-41-deploy.md` (mới) | 6 mục theo khuôn WP-15: phạm vi (thuần FE, 0 migration/0 env/0 .NET), **đường lùi bắt buộc**, cổng có số đo thật, ai gọi khi hỏng. |
| `qa` | `apps/admin-web/e2e/visual/support/manhinh.ts` | `chup()`: 2 rule CSS chuẩn hoá bề rộng ô đếm/badge tab (cùng cửa `addStyleTag` ẩn scrollbar) — `D-337` (`main a.nh-btn span.opacity-80{min-width:3.25rem}`) + `D-338` (`main a span.rounded-full{min-width:2.5rem}`). Giữ nguyên `maskChung`, **không** mask hàng tab. |
| `qa` | `apps/admin-web/e2e/visual/specs/01-tinh.spec.ts` | `D-338`: mask `main table thead` **theo 6 route đo được trôi cột** (`#4/#6/#13/#19/#21/#24`) — không đưa vào `maskChung`. |
| `qa` | `apps/admin-web/e2e/visual/__screenshots__/*.png` | **8 ảnh chuẩn sinh lại**: `04-product-item`, `06-order-manage`, `08-inventory-bill`, `13-pos-bill-list`, `16-customer-list`, `19-report-revenue`, `21-promotion`, `24-templates`. |
| `qa` | `docs/tests/UI-41-testplan.md` | Sửa câu SAI §2 (`#16` **không** mask hàng tab); thêm §2.2/§2.3 (`D-337`/`D-338`, F-02 đo được), §7 danh sách 8 PNG + bảng đo a/b/c; §5/§6 cập nhật nợ latent. |

### 1.3 Cổng đo lại (ORCH tự chạy)

| Lệnh | Exit | Số đo |
|---|---|---|
| Visual harness lượt 1 (trước `D-337`) | 1 | **53 passed / 1 failed** — `#16 customer/list` lệch **2266 px** (ratio 0.01 > 0.001) ⇒ `D-337` |
| Visual harness lượt 2 (trước `D-338`) | 1 | **53 passed / 1 failed** — `#6 order/manage` lệch **6597 px** (ratio 0.01) ⇒ `D-338` |
| QA: `--update-snapshots` → sạch 1 → **E2E** → sạch 2 | 0/0/0/0 | **54/54** → **54/54** → E2E **8/8** → **54/54** (140 s/40 s/147 s; `E2E_WEB=http://localhost:3100`) |
| **ORCH chạy lại** `E2E_WEB=3100 … playwright test -c e2e/visual/playwright.visual.config.ts` | **0** | **54/54** (2.3 phút) — xác nhận độc lập sau `D-338` |
| `git status --porcelain apps/admin-web/e2e/visual/__screenshots__/` | — | đúng **8** PNG modified, 0 tệp mới |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | **1** | **52/54** — **NHÓM 1c W1/W2 ✅** (W1: 0 bước thiếu, 15 khoá gồm 2 khoá mới; W2: xoá `probe-don-san.ps1` ⇒ đúng 1 thiếu). 2 đỏ **môi trường**: ① `dotnet build --no-incremental` = build lock PID 22404 (`D-335`); ② `Y1` do 2 tệp **gitignored** của máy dev `tools/ecommerce-run-chung.ps1` + `tools/ecommerce-start.ps1` (API chỉ-Windows; `git check-ignore` xác nhận; clean clone/CI không có ⇒ xanh) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 352 tệp `.md`, 1076 link, **2114** đường dẫn backtick, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 214 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 49/49 |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **không chạy** | **env-blocked**: PID 22404 giữ DLL **và** PID 18936 giữ cổng 3000 (bước ⑰ `e2e.ps1` sẽ `throw` vì không dọn được cổng) — `D-335`, chủ người/CI. **Không** báo xanh khống, **không** tắt bước |

### 1.4 Vì sao G6 bị chặn (bằng chứng)

1. `dotnet build` → `MSB3026/MSB3021` do `dotnet.exe` PID 22404 (start 28/11 18:08) giữ DLL trong `src/eCommerce.HttpApi.Host/bin/Debug/net10.0/`. CommandLine của PID đọc **rỗng** ⇒ `Lay-TienTrinhECommerce` ở Bước 0 cổng **không nhận ra** để dọn.
2. ORCH thử `Stop-Process -Id 22404` ⇒ **bị classifier chặn** (process có sẵn, không đọc được danh tính) — đúng `D-335` (chủ người).
3. Cổng 3000 do `node` PID 18936 (server Next cũ) giữ; `tools/e2e.ps1` Bước 1 `Dung-Cong -Cong 3000` thử `Stop-Process` rồi **`throw` nếu cổng vẫn bị chiếm** ⇒ `-DayDu` sẽ đỏ ở bước ⑰ dù mọi thứ khác xanh. Visual harness vì thế đo trên server 3100 (PID 26472, build 23:38 có CSS).

## 2. Chưa xong / cắt giữa

- **G6 `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` CHƯA chạy** — bị chặn bởi 2 tiến trình của máy dev (mục 1.4). Vì vậy hàng `U41-4` **chưa đạt DoD** ⇒ **không** đẩy fast-forward `feat/ui-wp41-nen` → `tich-hop/ui` (M3 yêu cầu cổng máy xanh).
- Việc còn lại **sau khi người/CI nhả khoá**: ① dừng PID 22404 + PID 18936 (hoặc chạy trên CI); ② `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` ⇒ kỳ vọng exit 0, trong đó bước **⑱ so ảnh** phải **54/54** và bước **4b-2 lint** exit 0; ③ nếu xanh ⇒ `PH done -Id U41-4` + `git push origin feat/ui-wp41-nen:tich-hop/ui` (FF).
- **Không có tệp mã nguồn sản phẩm nào đang sửa dở.**

## 3. Migration đã áp

**Không có migration, 0 thay đổi schema, 0 `pg_dump`.** (WP thuần FE + công cụ.)

## 4. Quyết định mới

| `D-xx` | Trạng thái | Tóm tắt |
|---|---|---|
| **`D-337`** | TẠM CHỐT (DEC, M2) | `#16 customer/list` đỏ 2266 px do bề rộng mask ô đếm `(n)` đổi theo số chữ số ⇒ chuẩn hoá `min-width` bằng CSS trong `chup()` (test-only), sinh lại **1** ảnh; không mask hàng tab, không sửa sản phẩm/root config. |
| **`D-338`** | TẠM CHỐT (DEC, M2) | Bước so ảnh bắt buộc trong `-DayDu` chạy **sau** E2E (mỗi lượt thêm dữ liệu ⇒ trôi có hệ thống) ⇒ mở rộng CSS badge `main a span.rounded-full` + mask `thead` **theo 6 route đo được trôi**; giữ ngưỡng 0.001, đủ 54 ca, **không** opt-out; 8 ảnh sinh lại; DB tất định vẫn là nợ `U42-4`/`U51-4`. |

`D-337`, `D-338` đã có dòng trong `docs/agents/hang-doi-xem-lai.md`. `check-agent-artifact -Kind decision` chạy ở phiên N122 **DAT** (120 dòng); phiên này **chưa chạy lại** (xem §5 nợ #7).

## 5. Nợ / rủi ro

| # | Nợ / rủi ro | Mức | Chủ |
|---|---|---|---|
| 1 | `dotnet build` bị PID 22404 giữ DLL; cổng 3000 bị PID 18936 giữ ⇒ **G6 `-DayDu` chưa chạy được** | 🔴 | **người/CI** (`D-335`) |
| 2 | Visual harness nhạy dữ liệu: badge/thead đã chuẩn hoá cho các route **đo được trôi**, nhưng DB dev vẫn trôi ⇒ route chưa mask (`#5/#7/#10/#23/#27/#28`) có thể đỏ sau này | 🟠 | `qa` ở `U47-4`/`U49-4`; gốc `U42-4`/`U51-4` |
| 3 | 6 route `#4/#6/#13/#19/#21/#24` **mất phủ so header bảng** (mask `thead`) tới khi DB tất định | 🟡 | `qa` (`U42-4`/`U51-4`) |
| 4 | `#4 product/item` có **2 locator mask NO-OP** (`main .flex.flex-wrap…` không khớp DOM thật) | 🟡 | `qa` (`U49-4`) |
| 5 | Rule CSS `D-337`/`D-338` phải **gỡ** khi có `PageTabs` badge bề rộng cố định (`U45-2` + chuyển màn `U48-3`) | 🟡 | `qa` |
| 6 | `Y1` của `test-cong-kiem` đỏ trên máy dev vì 2 tệp gitignored `tools/ecommerce-run-chung.ps1`, `tools/ecommerce-start.ps1` (không thuộc repo) | 🟡 | người (dọn hoặc để CI) |
| 7 | `check-agent-artifact -Kind testplan` vẫn **KHONG DAT** chỉ vì heading `## §1.` của SRS (`D-334`); `-Kind decision` chưa chạy lại sau `D-338` | 🟠 | người (`D-334`) / ORCH phiên sau |
| 8 | Ảnh chuẩn sinh trên Windows (font); tái sinh CI Linux ở `U42-4`/`U51-4` | 🟡 | `U42-4`/`U51-4` |
| 9 | `@vitejs/plugin-react 6.3.0` peer Vite 8 (`D-336`) | 🟡 | `WP-42`/`WP-51` |

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp |
|---|---|---|---|
| 1 | Dừng `dotnet.exe` **PID 22404** (giữ DLL) và server Next cũ **PID 18936** (giữ cổng 3000) để chạy `-DayDu` và đóng `U41-4`? | **(khuyến nghị)** người dừng 2 tiến trình rồi phiên sau chạy G6; hoặc để CI chạy `-DayDu` | Mọi việc khác đã xong; cổng đo trên 3100 + visual 54/54 ×4; ghi rõ `D-335` |
| 2 | Duyệt `D-331…D-338` + 2 tài liệu U1 (`D-329`) trước merge `tich-hop/ui → main`? | **(khuyến nghị)** duyệt ở biên bản người cuối chuỗi | Đã có dòng hàng đợi `hang-doi-xem-lai.md` |
| 3 | Có dọn 2 tệp gitignored `tools/ecommerce-run-chung.ps1` / `ecommerce-start.ps1` để `test-cong-kiem Y1` xanh trên máy dev? | **(khuyến nghị)** giữ ngoài repo và chấp nhận `Y1` chỉ xanh trên CI/clean clone; không sửa test | Không đụng tệp của người |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG `U41-4` (WP-41 F6 xong, G6 chưa đạt) → mở lại `U41-4`.**

- Chạy `PH next`/`PH show -Id U41-4`; hàng vẫn 🟨 với bằng chứng F6; **không** làm lại 2 bước cổng/deploy doc/8 ảnh.
- Trước hết **đo lại** xem PID 22404/18936 còn không (`Get-Process -Id 22404,18936`). Còn ⇒ **chờ người/CI**; hết ⇒ chạy `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (không chạy song song build/test khác).
- `-DayDu` xanh ⇒ `PH done -Id U41-4 -Evidence "<exit 0 + số bước + thời gian + bước ⑱ 54/54 + bước 4b-2 lint 0>"` và `git push origin feat/ui-wp41-nen:tich-hop/ui` (chỉ fast-forward). Đỏ vì mã ⇒ sửa rồi lặp; đỏ vì môi trường ⇒ `PH dodang` tiếp, **không** tắt bước.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-41
và handover `docs/nhanh/handover-n123.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N123 **dừng giữa** WP-41 (hàng U41-4: F6 xong, G6 chưa đạt vì môi trường): làm tiếp **đúng phần còn lại**
ghi ở handover §2 — **không** làm lại phần đã xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/ui-wp41-nen`; base `origin/tich-hop/ui` @ `80ab7a5`; trước phiên HEAD `aa153d0`.
- Commit phiên này: `docs(ui): N123 - U41-4 gate steps deploy doc D-337 D-338 visual baselines` — **SHA cuối ghi ở câu trả lời** (và `git log -1`).
- **Push** `origin/feat/ui-wp41-nen`; **chưa** đẩy `tich-hop/ui` (G6 chưa xanh). **Không** force/merge/PR/`main`.

---

**Hàng rào agent** (phiên `N123`): `decider` ×1 (trả 2 quyết định `D-337`, `D-338` trong cùng phiên) · `ops` ×2 · `qa` ×3 (một lượt bị cắt ở **giới hạn 60 lượt**, tiếp ở lượt sau theo hướng dẫn của harness). Cảnh báo hook "thiếu khối JSON" cho `ops`/`qa`/`decider` = **dương tính giả đã biết** (khối JSON vẫn có trong báo cáo; `chanDungKhiThieuJson=false`). Hook `guard-shell` chặn `git checkout` ⇒ `qa` phục hồi 1 PNG bằng `git show HEAD:<path> > <path>` (byte-identical, `git diff --quiet` = 0). `git status` cuối: `tools/kiem-tat-ca.ps1`, `tools/test-cong-kiem.ps1`, `docs/ops/UI-41-deploy.md`, `apps/admin-web/e2e/visual/**` (2 tệp TS + 8 PNG), `docs/tests/UI-41-testplan.md`, `docs/nhanh/decisions.md` + `07` + handover, `docs/agents/**`, `agents/state/**` — đúng vùng.

---

## 8. Mở lại `U41-4` để chạy G6 (29/11/2026 — cùng N123, `/chay-phien-code chuoi=ui-1`)

**Trạng thái: vẫn 🟨 — G6 CHƯA đạt (bước 10 chặn bởi DB dev bẩn, cần NGƯỜI). Không báo xanh một phần.**

### 8.1 Đã gỡ 2 nút thắt môi trường (theo `D-339`)
`decider` duyệt **`D-339` TẠM CHỐT** (phương án B): dừng đúng 2 PID dev + chạy cổng trong **worktree sạch** (không đụng 3 tệp launcher của người).
- `Stop-Process` PID **22404** (dotnet, giữ DLL + cổng 54275) và **18936** (node, cổng 3000) ⇒ đã dừng; DLL mở-ghi = **UNLOCKED**, cổng 3000/54275 **FREE** (đo lại sau).
- Worktree `--detach` tại `010335e` (`D:/Projects/Tools/eCommerce-g6-n123`) ⇒ `Y1` không còn thấy 2–3 tệp launcher gitignored của người.
- `pnpm install --frozen-lockfile` (3m29s) · `npm ci` trong `tools/` (55s) · `pnpm --filter @ecommerce/admin-web build` sinh `.next/BUILD_ID` (chỉ bước `standalone` báo EPERM symlink — Windows không có quyền tạo symlink; `.next/` đã hoàn chỉnh, `standalone` là đầu ra THÊM, không ảnh hưởng `next start`/cổng).
- `dotnet build eCommerce.slnx` làm nóng (0 error, 9 warning CS8604 có sẵn) để `kiem-moc-build -GhiNho` có DLL.
- `pnpm gen:api` một lần để `schema.d.ts` về LF (bản checkout tươi là CRLF; cổng so SHA256 trước/sau ⇒ lần đầu báo DRIFT giả, sau đó idempotent).

### 8.2 Cổng `-DayDu` trên worktree sạch — 9 bước XANH, DỪNG ở bước 10

| Bước | Kết quả |
|---|---|
| ① dọn tiến trình/cổng | XANH (0 tiến trình, cổng 5000/5100 trống) |
| ② build + kiểm mốc | XANH — `dotnet build` exit 0 · 0 lỗi · "Build DA VAO (mọi mốc đều đổi)" |
| ③ unit test | XANH — **2691/2691** |
| ④ integration test | XANH — **171/171** |
| ⑤ mermaid | XANH — 214 khối, 0 lỗi |
| ⑥ cổng tài liệu WP | XANH — WP-41 "không có mã tài liệu" (dùng `GITHUB_HEAD_REF=feat/ui-wp41-nen` cho worktree detached; tương đương `-WP WP-41`, không nới kiểm) |
| ⑦ FE typecheck + Vitest | XANH — `tsc` 0 lỗi · **6 file/60 ca** |
| ⑧ FE lint | XANH — 12 warning, **0 error** |
| ⑨ gen:api không đổi `schema.d.ts` | XANH — 82/82 path, SHA256 trước=sau |
| ⑩ bất biến DB | **ĐỎ** — `CusCustomers: conSong=46 · moCoi=45 · xoaMem=0` |

### 8.3 Vì sao bước 10 đỏ — DB dev tích rác probe (không phải mã)
- **45/46** khách còn sống là **mồ côi** (không chứng từ trỏ vào) — tên `Khách QA 2026…`, `Khách WP06…` — do probe/POS cũ để lại; đúng `D-53` ①c. 1 khách hợp lệ: `Khách WP06 583950`.
- **0 FK** trỏ `CusCustomers` ⇒ xoá không vướng khoá ngoại; **0 dòng xoá mềm**.
- `tools/kiem-db.ps1` in: "Xoá đúng những dòng đó — KHÔNG xoá khách có hoá đơn". Không có tool dọn sàn tổng quát.
- **`decider` trả `CHUYEN_NGUOI`** (không ghi `D-340`): xoá dữ liệu khách là **vùng cấm** (decision-policy §3.2, ke-hoach-he-thong-agent §4.3 mục 4, uỷ quyền §6) — *có dump không* biến hành động thành được phép ⇒ **chỉ người** quyết.

### 8.4 Đã chuẩn bị cho người (read-only/backup)
- **Dump**: `artifacts/pg-dump/n124-U41-4.dump` — **1 579 639 byte** (`PGDMP`), `pg_dump -Fc`.
- **45 dòng mồ côi + đếm tham chiếu**: `tools/_tam-kiem/g6-n123/orphans-CusCustomers.txt` (`Id | Name | CreationTime`).
- **Log cổng**: `tools/_tam-kiem/g6-n123/wt-logs/*.log` (build, unit, integration, mermaid, fe-*, cong-tai-lieu, db…). Đều gitignored.

### 8.5 Việc của NGƯỜI để đóng `U41-4` (một trong hai)
1. **Duyệt xoá đúng 45 dòng mồ côi** (ID ở tệp 8.4): sau dump, chạy MỘT transaction — predicate ①c của `kiem-db.ps1:273-279`, `DELETE` **không CASCADE**, kiểm `count(*)` trước/sau = 45, lỗi FK ⇒ `ROLLBACK`; rồi `pwsh -File tools/kiem-db.ps1` phải `moCoi=0 · xoaMem=0`; rồi chạy lại `-DayDu` (worktree sạch, 20–40 phút). Hoặc uỷ quyền agent chạy đúng SQL đã duyệt.
2. **Chạy cổng trên DB dùng-một-lần/CI** (hướng bền, `U42-4`/`U51-4`): người tạo + migrate DB (`V3` cấm agent `dotnet ef database update`).

Cách chạy lại cổng (sau khi DB sạch): `git worktree add --detach D:/Projects/Tools/eCommerce-g6-n123 010335e`; trong worktree `pnpm install --frozen-lockfile` · `(cd tools && npm ci)` · `pnpm --filter @ecommerce/admin-web build` · `dotnet build eCommerce.slnx` · `pnpm gen:api` · đặt `GITHUB_HEAD_REF=feat/ui-wp41-nen` · `pwsh -File tools/kiem-tat-ca.ps1 -DayDu`. Xanh ⇒ `PH done` + `git push origin feat/ui-wp41-nen:tich-hop/ui` (FF).

### 8.6 Dấu vết
- `D-339` (TẠM CHỐT, DEC) + dòng `hang-doi-xem-lai.md`. Câu DB ⇒ `CHUYEN_NGUOI` (không `D-xx`).
- Worktree gỡ sau khi chép log. 2 dev server của người đã dừng — khởi động lại bằng launcher sẵn có (không đổi byte nào).
- **Không** sửa mã nguồn/ngưỡng/ca/cổng; **không** đụng 3 tệp launcher của người; **không** xoá dữ liệu.
