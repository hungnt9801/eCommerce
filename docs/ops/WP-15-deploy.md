# Triển khai `WP-15` — 25/11/2026

> Phiên `N69`, nhánh `feat/n69-wp15-nen-gd3`. Quyết định liên quan: `D-209`, `D-210`, `D-211`, `D-212`,
> `D-213`, `D-214`, `D-215`. Đây là WP **hạ tầng/cổng** — không có migration, không có biến môi trường mới.

## 1. Phạm vi

- Nhánh/PR: `feat/n69-wp15-nen-gd3` → `main`.
- Migration: **0** (không `migrations add`, không chạm schema). `DbMigrator` chạy bình thường nhưng **không áp
  migration mới**; không cần `pg_dump` vì không có thay đổi dữ liệu.
- Hợp đồng API (thay đổi hành vi có chủ đích, `D-210`): xung đột `ConcurrencyStamp` (`AbpDbConcurrencyException`)
  trả **HTTP `409`** với `error.code = eCommerce:Db:0002` (trước đây `code = null`). Ràng buộc DB
  (`23505`/`23514`/`23503`) **giữ nguyên** `403` + `eCommerce:Db:0001`.
- Cổng máy mới (`D-211`, `D-212`): `tools/probe-cong-tai-lieu.ps1` (QA sở hữu logic), đấu dây trong
  `tools/kiem-tat-ca.ps1` + `tools/test-cong-kiem.ps1` (OPS) và workflow `.github/workflows/cong-tai-lieu.yml`.

## 2. Biến môi trường

| Tên | Mặc định dev | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

- `GITHUB_BASE_SHA` trong workflow `cong-tai-lieu.yml` là biến **của GitHub Actions** (lấy từ
  `github.event.pull_request.base.sha`), không phải biến môi trường của Host; không cần khai ở `.env`/secret store.

## 3. Thứ tự triển khai

1. **Merge code BE trước** (PR `feat/n69-wp15-nen-gd3` → `main`): binary mới chứa filter `409` + hằng mã
   `eCommerce:Db:0002` (`D-210`). Không có migration ⇒ `DbMigrator` không có gì để áp; vẫn chạy bằng binary mới
   theo `ef-migration-postgres` M5 để xác nhận `__EFMigrationsHistory` không đổi.
2. **FE**: không có thay đổi trong WP này.
3. **Workflow `cong-tai-lieu.yml`** tự có hiệu lực sau merge (chạy trên `pull_request` vào `main` của các PR kế
   tiếp). Job chạy chế độ `MergeMain` — hôm nay mọi WP có mã đều ĐỎ cho tới khi có biên bản **người ký**
   (`D-115` ⑤); `WP-15` XANH vì không có mã tài liệu (`D-212` (iii)).
4. **Bật job `cong-tai-lieu` thành required check = VIỆC NGƯỜI** (branch protection là vùng cấm của agent —
   `decision-policy` §3.1). Trước khi bật, đo trạng thái hiện tại và ghi vào hàng đợi xem lại như khuôn `D-65`.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| Filter `409` + mã `eCommerce:Db:0002` (`D-210`) | `git revert <sha của commit D-210>` (1 commit, 2 tệp: `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs`, `src/eCommerce.HttpApi.Host/Filters/BoLocViPhamRangBuocDb.cs`) | Hành vi trở về `409` + `code = null` (`D-66` ①). Không chạm dữ liệu. |
| `P15` phân biệt 2 nguyên nhân `409` (`D-215` ①(3), thay ① `D-214`) | Hoàn nguyên 1 commit, 2 tệp: `tools/probe-giu-cho-2instance.ps1` (4 biến + 3 nhánh (ii)/(iii)/(iv) + câu tổng kết) và `tools/test-cong-kiem.ps1` (3 ràng buộc mới + chiều ngược 5) | Về đúng hành vi `D-214` ①: mọi `409` phải `eCommerce:Db:0002`, `eCommerce:Lock:0001` sẽ ĐỎ. 0 migration, 0 `.cs`, 0 dữ liệu. |
| Cổng tài liệu (`D-211` ④) | Xoá `.github/workflows/cong-tai-lieu.yml` + bỏ `Buoc 'Cong tai lieu: WP dang code co duyet khong'` trong `tools/kiem-tat-ca.ps1` + bỏ ca `Y9` trong `tools/test-cong-kiem.ps1` (**OPS**); QA xoá `tools/probe-cong-tai-lieu.ps1` + testplan `WP-15` (**QA**) | Tổng 1 commit phía OPS (3 tệp) + 1 commit phía QA (2 tệp); cổng về "chưa có" như `D-209` ⑥. |
| Dữ liệu | Không cần | 0 migration, 0 `UPDATE`/`DELETE` mới; không cần `pg_dump`. |

- Không revert migration (không có migration). Không tắt bước CI nào để xanh.

## 5. Cổng (số đo thật, phiên `N69`)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File tools/test-cong-kiem.ps1` | `exit 0` · **60/60 DAT** (`Y7` DAT · `Y8` DAT · `Y9` DAT) |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | `exit 0` · **54/54 DAT** (trước phiên: 52/53, `Y8` HỎNG đúng dự kiến) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | `exit 0` · **8/8 ca DAT** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code` | `exit 0` · `WP-15 khong co ma tai lieu ⇒ XANH` |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain` | `exit 0` · `WP-15 khong co ma tai lieu ⇒ XANH` |
| `pwsh -File tools/probe-giu-cho-2instance.ps1` (**QA đo**, testplan `WP-15` §4/§5) | `exit 0` · **18 DAT / 0 HONG / 0 CHUA CHAY** · `P15 = DAT` (`so lan 409 = 1 · ma = eCommerce:Db:0002`) — cơ chế `D-215` vẫn tách `Lock:0001`/0 lần 409 ⇒ `CHUA CHAY` |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **exit 0** · **20/20 bước · 836,4 s** (lượt **ngoài phiên** 25/11/2026 trên cây cuối, log `artifacts/dayDu-wp15.txt`; nợ "hết bộ nhớ" của `N69` **đã đóng**) |

### Số đo `kiem-tat-ca.ps1 -DayDu` (lượt cuối — XANH 20/20)

Lượt `-DayDu` chạy **trọn 20/20 bước** trên cây cuối (người dùng chạy ngoài phiên, log
`artifacts/dayDu-wp15.txt`, tổng **836,4 s**).

| Bước | Kết quả |
|---|---|
| ① Dọn tiến trình + cổng trống | 1,3 s · 0 tiến trình, cổng 5000/5100 trống |
| ② Build + kiểm mốc | 82,7 s · `dotnet build exit=0` · **0 error · 9 warning** · mọi mốc đổi |
| ③ Unit test | 26,2 s · `Failed: 0, Passed: 1966` |
| ④ Integration test | 32,1 s · `Failed: 0, Passed: 96` |
| ⑤ Mermaid | 10,6 s · **212 khối, 0 lỗi** |
| ⑥ **Cổng tài liệu (bước mới)** | 0,9 s · `WP-15 ... ⇒ XANH`, **exit 0** |
| ⑦ FE typecheck + Vitest | 13 s · typecheck 0 lỗi · **`Tests 25 passed`** |
| ⑧ `gen:api` không đổi contract | 4 s · **không đổi** `schema.d.ts` (sau khi chuẩn hoá checkout về LF — xem ghi chú môi trường) |
| ⑨ Bất biến DB | 21,9 s · **25/25** |
| ⑩ Test cho chính các cổng kiểm | 176,7 s · **60/60 DAT** |
| ⑪ E2E có phiên đăng nhập (`web-001`) | 120,8 s · **DAT** |
| ⑫ Sổ địa chỉ HTTP thật (`ADDR-004`) | 24,6 s · **DAT** |
| ⑬ Khách hàng HTTP thật (`CUS-001`) | 28,4 s · **DAT** |
| ⑭ Thanh toán HTTP thật (`PAY-001`) | 25,2 s · **DAT** |
| ⑮ Đơn sàn đường chạy thật (`SAL-005`, DLQ/watermark) | 47,5 s · **DAT** |
| ⑯ Oversell hai request đồng thời | 26,5 s · **DAT** |
| ⑰ Outbox (process chết sau commit) | 36,1 s · **DAT** |
| ⑱ Outbox 2 instance | 47,1 s · **DAT** |
| ⑲ Giữ chỗ 2 instance | 40 s · **18 DAT / 0 HONG / 0 CHUA CHAY** · **`P15 DAT`** (`Db:0002`, 2 lần 409) |
| ⑳ Web Admin E2E 8 luồng Playwright | 71 s · **8/8 passed** |

Số đo bổ sung: `test-cong-kiem.ps1` **60/60 DAT** · `-ChiKiemMoc` **54/54 DAT** · `probe-giu-cho-2instance.ps1`
**18 DAT / 0 HONG / 0 CHUA CHAY** (`P15 = DAT`, **1** lần `409` mã `eCommerce:Db:0002`) · `probe-don-san.ps1`
**`C10` DAT**.

- Lưu ý môi trường: **đã cài `pnpm@10.22.0` toàn cục cho user** (npm) và **bỏ shim corepack** ở `%TEMP%`;
  bước ⑦/⑧ chạy bằng `pnpm` trên PATH, **không** đổi cấu hình corepack. Không còn shim tạm.
- Bước ⑧ (`gen:api`) trên Windows: checkout dùng `core.autocrlf=true` (CRLF) còn generator ghi LF ⇒ `schema.d.ts`
  đổi **byte** dù **nội dung y hệt** (blob hash = `be3cebf…` = HEAD). Đã chuẩn hoá working file về LF bằng
  `git -c core.autocrlf=false checkout --`; chạy `pnpm run gen:api` **hash không đổi** (`da162436…`).
- PR phải hiện đúng 2 required check hiện có; workflow `cong-tai-lieu` **chưa** là required check (việc NGƯỜI).
- Kiểm sau deploy: `GET /api/health` 200; 1 luồng nghiệp vụ chính (giữ chỗ kho) trả `200`, và khi có xung đột
  đồng thời trả `409` + `error.code = eCommerce:Db:0002` (đo bằng `tools/probe-giu-cho-2instance.ps1`, ca `P15`).

## 6. Khi hỏng — ai gọi

- Cổng tài liệu chặn oan PR tài liệu (`exit 2`/`exit 1` khó hiểu): **ORCH** đọc log
  `tools/_tam-kiem/cong-tai-lieu.log`, đối chiếu `D-212` (i)/(iv); sửa **logic** ⇒ báo **QA** (chủ
  `tools/probe-*.ps1`), sửa **đấu dây** ⇒ **OPS**.
- Workflow `cong-tai-lieu` đỏ vì thiếu biên bản người ký: **đúng thiết kế** (`D-115` ⑤) — cần người ký biên bản
  `docs/bien-ban-duyet-gd3-*.md`, không sửa workflow để xanh.
- Host trả `409` sai mã: **BE/arch** (`D-210`), không phải OPS.
