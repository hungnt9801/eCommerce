# Handover N69 — WP-15 (Nền GĐ3 + 2 nợ `D-71`④)

> Phiên `N69` · 25/11/2026 · nhánh `feat/n69-wp15-nen-gd3` · **B6/Pilot-C** (`docs/agents/ke-hoach-v2-claude-code-pro.md` §11) · mức **L1**.
> Dây chuyền `/feature WP-15`: **F0 → F3 BE → F4 QA → F5 REV → F6 OPS → G3/G5**; **G6 chưa trọn** (xem §2).
> Không có SRS: hàng `WP-15` ở `docs/nhanh/07-ke-hoach-trien-khai.md:745` ghi *"Không có tài liệu feature"* ⇒ **V1 hợp lệ theo ngoại lệ WP hạ tầng**.

## 1. Đã xong

- **`D-210` — mã nghiệp vụ cho HTTP `409`**: `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` thêm `MaXungDot = "eCommerce:Db:0002"`, `ThongDiepXungDot`, `LaXungDotDuLieu(Exception?)` (đi `InnerException` ≤ 10, chỉ nhận `AbpDbConcurrencyException`); `BoLocViPhamRangBuocDb` kiểm nhánh này **trước** Postgres ⇒ `409` + body khuôn ABP + `ExceptionHandled = true` + `LogWarning`. **Không đổi** bất kỳ hành vi `403` nào (`23505/23514/23503` + `MaChung = 0001` giữ nguyên). Thêm `PackageReference Volo.Abp.Data` (Kernel) + `PackageVersion` 10.7.0 — **không kéo EF**.
- **`D-213` — `C10`**: `tools/probe-don-san.ps1` chờ **`DLQ LyDo = HaWatermark ≥ 1`** (một lần chờ, `$GiayToiDa = 30` không nới), bỏ lần chờ trùng ở `C13`; giữ `($tongA -eq 1 -and $lateA -eq 1)`, `[CD-D67]`, 4 neo log.
- **`D-214` + `D-215` — ca `409` runtime**: `tools/probe-giu-cho-2instance.ps1` lưu **mọi** body `409`; `P15` (theo `D-215`, thay ① của `D-214`): mọi `409` phải có `error.code` khác rỗng (`null` ⇒ **ĐỎ**), `Db:0002` + message ⇒ **DAT**, `Lock:0001`/mã khác ⇒ **CHUA CHAY** (không đỏ oan), 0 lần `409` ⇒ **CHUA CHAY**. Bằng chứng runtime **thật**: 2 lượt thấy `{"error":{"code":"eCommerce:Db:0002","message":"Dữ liệu vừa bị người khác thay đổi…"}}` ⇒ **P15 DAT**.
- **`D-211` + `D-212` — 2 cổng máy tài liệu**: QA viết `tools/probe-cong-tai-lieu.ps1` (`-CheDo Code|MergeMain`, `-WP`, `-Goc`, `-TuKiem`; exit `0/1/2`, fail-closed; 8/8 fixture DAT); OPS đấu dây: 1 `Buoc 'Cong tai lieu…'` trong `tools/kiem-tat-ca.ps1` (coi `exit 2` là ĐỎ), ca `Y9` trong `tools/test-cong-kiem.ps1`, workflow mới `.github/workflows/cong-tai-lieu.yml` (`pull_request` base `main`, `GITHUB_BASE_SHA`, `fetch-depth: 0`, không `continue-on-error`). **WP-15 XANH cả hai chế độ** (không có mã tài liệu).
- **Chống nới**: `Y7` + `Y8` cập nhật theo `D-213`/`D-214`/`D-215` (5 chiều ngược `Y8`, 5 chiều ngược `Y7`; mỗi phép thay có khẳng định "đã xảy ra"); `Y9` đòi `-TuKiem` + repo thật xanh.
- **Tài liệu**: `docs/tests/WP-15-testplan.md` (R-01…R-10, negative control, số đo), `docs/ops/WP-15-deploy.md` (0 migration, đường lùi), `docs/reviews/WP-15-review.md` (**VERDICT: PASS · P1 = 0**; P2 #2 đã đóng bằng `D-215`, P2 #3 có bằng chứng runtime; P3 #9 đã đồng bộ), `D-210`…`D-215` trong `docs/nhanh/decisions.md` + `docs/agents/hang-doi-xem-lai.md`.

| Lệnh | Exit | Số đo (baseline → cuối phiên) |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | 0 error · **9 warning** (cả hai lần) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **1961 → 1966** (+5 `Fact` `D-210`), 0 fail |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **96/96** |
| `tools/probe-don-san.ps1` | 0 | **49/49 DAT** · `C10 DAT` (`tong = 1 · Late = 1 · choDu = True`) |
| `tools/probe-giu-cho-2instance.ps1` | 0 | **18 DAT / 0 HONG / 0 CHUA CHAY** · **`P15 DAT`** (1 lần 409 `Db:0002`) — vòng 2 `D-215` |
| `tools/test-cong-kiem.ps1` | 0 | **53 → 60/60 DAT** (`Y7`/`Y8`/`Y9` DAT) |
| `… -ChiKiemMoc` | 0 | **54/54 DAT** |
| `tools/probe-cong-tai-lieu.ps1 -TuKiem` | 0 | **8/8 ca DAT** · `-CheDo Code` / `MergeMain` trên nhánh này: **0** |
| `pwsh -File tools/kiem-tat-ca.ps1` (bản nhẹ) | 0 | **10/10, 296,8 s** (đo trước vòng `D-215`; cây cuối đã được cổng đầy đủ `-DayDu` phủ **20/20** — dòng dưới) |
| `tools/kiem-link-tai-lieu.ps1` | 0 | **PASS 0 chết** · **921** link · **1.417** backtick (đo sau khi có handover) |
| `npm --prefix tools run check:mermaid` | 0 | **212** khối · **0** lỗi |
| `check-agent-artifact.ps1 -Kind review` | 0 | **PASS · P1 = 0 · DAT** |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT (**100** dòng) |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **0** | **20/20 bước · 836,4 s** — người dùng chạy **ngoài phiên** 25/11/2026 trên cây cuối, log `artifacts/dayDu-wp15.txt`: build **0 error** · unit **1966/1966** · integration **96/96** · mermaid **212 khối/0 lỗi** · cổng tài liệu XANH (`WP-15` không mã) · FE typecheck + Vitest · `gen:api` không đổi `schema.d.ts` · DB **25/25** · test cổng **60/60** · **E2E Playwright 8/8** · **`P15 DAT`** (`Db:0002`, 2 lần 409) · `C10 DAT` |

- `pnpm lint`: **không có script `lint`** ở `apps/admin-web` (root `pnpm lint` trỏ tới script không tồn tại) — nợ **có sẵn**, không do WP-15; các cổng FE thật đã chạy: typecheck 0 lỗi · Vitest **25/25** · `gen:api` **không đổi** `schema.d.ts` (blob `be3cebf…` = HEAD).
- `next build` trên máy Windows lỗi **EPERM symlink** (quyền tạo symlink của Windows) — **môi trường**; CI `docker-build` là nơi build thật.

## 2. Chưa xong / cắt giữa

1. **[ĐÃ ĐÓNG 25/11/2026 — môi trường]** `-DayDu` đã chạy **trọn 20/20 (836,4 s)** trên cây cuối bằng lượt **ngoài phiên** (`artifacts/dayDu-wp15.txt`, exit 0) ⇒ nợ "hết bộ nhớ" của `N69` **đóng**; Playwright **8/8**; `P15 DAT`. Môi trường: đã cài **pnpm@10.22.0 toàn cục cho user** (npm) và **bỏ shim corepack** ở `%TEMP%` ⇒ bước FE chạy không cần shim, **không** đổi cấu hình corepack.
2. **Chưa commit / chưa có PR** — người dùng chưa yêu cầu (CLAUDE.md §8). Nhánh `feat/n69-wp15-nen-gd3` đang giữ **11 tệp tracked + 5 tệp mới** (xem §7).
3. **Bật `cong-tai-lieu` thành required check** — **việc NGƯỜI** (branch protection = vùng cấm).
4. **`negative-control.ps1` NC-1** — chỉ chạy được sau khi commit (tệp đích phải đã commit, không dirty); bằng chứng trước commit: `-TuKiem` CA1–CA6 + đột biến tay NC-1 (`exit 1` → khôi phục `exit 0`, sha256 `66eab23…`).
5. **P3 còn lại của REV** (không chặn merge): #4 chú thích `BUOC 4a` lệch số bước; #5 `MergeMain` chưa in cảnh báo README; #6 chú thích Kernel "closure bắc cầu"; #7 sàn số bước `ci-khang-dinh` giữ 15; #8 thứ tự luật "rỗng mã" trước luật ĐỎ (b); #10 lượt `-DayDu` chưa xanh (nay là nợ môi trường đã ghi). **P2 #1** (diff ~1.475 dòng > ~600) — chấp nhận, không tách (631 dòng là probe cổng do `D-211`① buộc).

## 3. Migration đã áp

- **Không có** (0 migration, 0 `.cs` thay schema). Không cần `pg_dump`.
- Có seed **dữ liệu tham chiếu dev**: `tools/gieo-danh-muc-dvhc.ps1` (2 dòng ĐVHC `01`/`00004`, idempotent) — cần cho probe ⑨/`-DayDu`; **không** chạy cho production.

## 4. Quyết định mới

- **`D-210`** — mã chung cho `409`: `eCommerce:Db:0002` khai ở Kernel + nhánh `AbpDbConcurrencyException` trong filter Host; **không** dùng lại `0001`.
- **`D-211`** — vùng ghi cổng tài liệu: QA sở hữu logic (`tools/probe-cong-tai-lieu.ps1` + `-TuKiem`), OPS sở hữu đấu dây (`kiem-tat-ca`, `test-cong-kiem`, workflow).
- **`D-212`** — ngữ nghĩa 2 cổng: `Code` trong `kiem-tat-ca.ps1`; `MergeMain` ở workflow `pull_request` base `main`; WP-15 xanh vì không mã; nguồn trạng thái = 3 biên bản tạm duyệt + biên bản người ký; README chỉ cảnh báo.
- **`D-213`** — `C10` chờ `DLQ (LyDo = HaWatermark) ≥ 1`, trần ≤ 30 s, giữ khẳng định `tong = 1 VA Late = 1`.
- **`D-214`** — ca "409 có `error.code` khác null" = khẳng định **runtime** trong `probe-giu-cho-2instance` + mở rộng `Y7`.
- **`D-215`** — **thay ① của `D-214`**: mọi `409` phải có mã khác rỗng; `Db:0002` ⇒ DAT; `Lock:0001`/mã khác ⇒ CHUA CHAY; `code` null ⇒ ĐỎ.
- Tất cả **TẠM CHỐT (DEC)**, đã có dòng trong `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

- **`P15` nhánh `code = null ⇒ ĐỎ` chưa có ca tất định** (`D-215`①(4)/`D-214`②): nếu **3 lượt CI liền** có `0` lần `409` ⇒ QA thêm đường tạo xung đột **tất định** bằng `psql` (giữ `InvItems` + bump `ConcurrencyStamp`, khuôn `D-66`①), **không** nới `$SoLanThuLai`. Chưa có ca tự động cho 2 body `Lock:0001`/`code=null`.
- **Merge `main` của code GĐ3** vẫn cần **biên bản người ký** (`0/18` hiện có, `D-115`⑤); **WP-15 tự nó XANH** ở `MergeMain` (không mã). Job `cong-tai-lieu` **chưa** là required check — việc người.
- **Môi trường (đã xử lý 25/11/2026):** `-DayDu` **đã chạy trọn 20/20 · 836,4 s** (`artifacts/dayDu-wp15.txt`); **pnpm@10.22.0** đã cài **toàn cục cho user** bằng npm, **đã bỏ shim corepack** ở `%TEMP%` ⇒ không cần shim tạm, **không** đổi cấu hình corepack. Chromium Playwright ở `%LOCALAPPDATA%\ms-playwright` (`chromium_headless_shell-1248`). Vẫn khuyến nghị **không** chạy `-DayDu` song song với build/test khác.
- **OPS có ghi tạm** `packages/api-client/src/schema.d.ts` (sửa CRLF rồi `git checkout` khôi phục) — cây cuối **không còn diff** ở `packages/`; hook đã ghi cảnh báo "ghi ngoài vùng". Không còn tệp ngoài vùng.
- **Cảnh báo hook**: `decider` (lượt 1) và `ops` có lượt **thiếu khối JSON cuối đúng khuôn** (báo cáo hand-back vẫn có JSON); đã ghi trong `artifacts/agents/canh-bao.jsonl`. Không phải lỗi sản phẩm.
- **`pnpm lint` không tồn tại** (CLAUDE.md §3 ghi lệnh này nhưng `apps/admin-web/package.json` không có script `lint`) — nợ quy trình **có sẵn**, cần chủ (FE/OPS) quyết: thêm ESLint hoặc sửa CLAUDE.md.
- **Diff ~1.475 dòng** > ngưỡng ~600 (P2 #1 của REV; 631 dòng probe cổng do `D-211` buộc). Nếu người muốn PR nhỏ: tách được 2 PR (BE+QA probe/tests; OPS cổng+workflow+docs), nhưng phải giữ thứ tự và cùng nhánh.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-15 → mở lại WP-15** *(đã thực hiện ở phiên đóng `N70` — xem [handover-n70](handover-n70.md))*.

- Chạy `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` trên máy **đủ bộ nhớ** (không chạy song song build/test); kỳ vọng **20/20**, gồm 8 luồng Playwright (chromium đã cài) và `P15` (DAT hoặc CHUA CHAY — cả hai đều đúng luật `D-215`).
- Nếu `-DayDu` đỏ: **không** nới test/cổng; xác định là sản phẩm hay môi trường theo bảng phân loại của cổng.
- Khi `-DayDu` xanh: chạy `negative-control.ps1` NC-1 sau khi **người** commit nhánh (hoặc ghi lý do nếu chưa commit); đóng WP-15 (handover N70 dùng biến thể **B** → mở **WP-16** theo `D-209`).
- Đọc lại `docs/reviews/WP-15-review.md` §điều kiện trước merge và `docs/ops/WP-15-deploy.md` §5 (số đo đã đồng bộ tới lượt cuối).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-15
và handover `docs/nhanh/handover-n69.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N69 **dừng giữa** WP-15: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/n69-wp15-nen-gd3`, base `main` head `7267060`; **chưa commit** (người dùng chưa yêu cầu), **chưa push**, **chưa PR**.
- Tracked **11 tệp, +468 / −28**: `Directory.Packages.props` · `docs/agents/hang-doi-xem-lai.md` · `docs/nhanh/decisions.md` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `shared/eCommerce.Kernel/eCommerce.Kernel.csproj` · `src/eCommerce.HttpApi.Host/Filters/BoLocViPhamRangBuocDb.cs` · `test/eCommerce.Tests/Inventory/ViPhamRangBuocDbTests.cs` · `tools/kiem-tat-ca.ps1` · `tools/probe-don-san.ps1` · `tools/probe-giu-cho-2instance.ps1` · `tools/test-cong-kiem.ps1`.
- Tệp mới (untracked, 979 dòng): `.github/workflows/cong-tai-lieu.yml` (41) · `docs/ops/WP-15-deploy.md` (99) · `docs/tests/WP-15-testplan.md` (119) · `docs/reviews/WP-15-review.md` (89) · `tools/probe-cong-tai-lieu.ps1` (631) · handover này.
- Tổng diff ước **~1.475 dòng** (chưa kể handover) — xem P2 #1 ở §2/§5.

---

**Hàng rào agent** (phiên `N69`): **5 vai / 7 lượt** (`decider` ×2, `be`, `qa` ×2, `rev` ×2, `ops` ×2) · **0** lần chặn dừng · **1** cảnh báo ghi ngoài vùng (`ops` → `packages/api-client/src/schema.d.ts`, đã khôi phục, cây cuối sạch) · **4** cảnh báo thiếu khối JSON đúng khuôn (`decider` 1, `ops` 3) · **0** tệp ngoài phạm vi còn lại. `/usage`: **chưa đo**.
