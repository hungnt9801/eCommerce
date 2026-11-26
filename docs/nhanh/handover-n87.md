# Handover N87 — WP-22 / hàng `W22-5` (F6 OPS + G6) XONG — ĐÓNG WP-22

> Phiên `N87` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W22-5` · WP-22 · mã `NOT-001` · nhánh
> `feat/gd3-wp22-thong-bao` (base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` code/ops `24fd918` + commit đóng phiên) · 27/11/2026.
> Phạm vi phiên: **F6 OPS** (tài liệu triển khai + đường lùi) · **G6** `kiem-tat-ca.ps1 -DayDu` · **fast-forward**
> nhánh WP vào `tich-hop/gd3`. **0 migration**, `main` không đụng.

## 1. Đã xong

- **F6 — `docs/ops/NOT-001-deploy.md`** (`ops`, tệp mới): 7 mục — phạm vi (4 project module, **1** migration, **9** op HTTP,
  **9** permission khai 2 nơi) · biến môi trường (`Notification:EnableDispatch` mặc định **`false`**, lease/size, cảnh báo
  `PeriodSeconds` **chưa được đọc**) · thứ tự pg_dump → merge BE → build → `DbMigrator` → kiểm DB thật → BE → FE → giữ cờ
  `false` · **đường lùi** (tắt cờ 0 dữ liệu · sửa cấu hình 0 code/migration · `git revert` 5 commit, **KHÔNG** `Down`/`DROP`
  5 bảng · dòng lịch sử `__EFMigrationsHistory` thừa vô hại) · cổng + SQL kiểm DB thật + bảng 9 route→quyền + mã lỗi
  `0010–0020` · "khi hỏng — ai gọi" · nợ/rủi ro.
- **G6** — `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` **exit 0 · 20/20 bước · 745,7 s**; không bước HỎNG. Log
  `artifacts/dayDu-wp22.out` (24 345 B).
- **Đóng WP-22**: push nhánh WP rồi **fast-forward** vào `tich-hop/gd3` (chi tiết §7). **Không** mở PR (`D-216` ③).
- **Không có `D-xx` mới** trong phiên.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | **PASS** · 959 link · 1654 đường dẫn · 0 chết |
| Baseline `npm --prefix tools run check:mermaid` (mở phiên) | 0 | **213** khối · **0** lỗi |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở phiên) | 0 | **49/49** pass |
| **G6** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **0** | **20/20 bước ĐẠT · 745,7 s** · 0 bước HỎNG |
| ├ `dotnet build eCommerce.slnx --no-incremental` (bước 2) | 0 | **0 error** · 0 dòng lỗi · 80,6 s |
| ├ `dotnet test test/eCommerce.Tests` (bước 3) | 0 | **2283/2283** · 0 skipped · 23,7 s |
| ├ `dotnet test test/eCommerce.IntegrationTests` (bước 4) | 0 | **110/110** · 0 skipped |
| └ các bước 5–20 (mermaid · FE typecheck/Vitest · `gen:api` không đổi · DB 25/25 · test cổng 60/60 · ADDR/CUS/PAY/SAL · oversell · outbox ×2 · giữ chỗ 2 instance · Playwright 8/8) | 0 | **ĐẠT** (xem `artifacts/dayDu-wp22.out`) |
| `git push origin feat/gd3-wp22-thong-bao` | 0 | xem §7 |
| `git push origin feat/gd3-wp22-thong-bao:tich-hop/gd3` (fast-forward) | 0 | xem §7 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | **PASS** (số đo ở câu trả lời cuối) |

> Ghi chú kỹ thuật: `-DayDu` chạy bằng `Start-Process` nền (vượt trần 600 s/lệnh). Script kết thúc bằng
> `exit 0` **sau** khi in `TONG SO BUOC DAT: 20`; log có đủ dòng tổng kết ⇒ mã thoát 0 (bất kỳ bước hỏng nào đều
> `throw` trước đó, không tới dòng cuối).

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W22-5`.** WP-22 đã đóng: F6 + G6 xanh, đã đẩy `tich-hop/gd3`.

## 3. Migration đã áp

**Không có migration mới trong phiên này.** Migration của WP-22 vẫn là
`20261126132231_Added_Notification_Template_Queue` (W22-2, đã áp DB dev sau `pg_dump`
`artifacts/pg-dump/N84-W22-2.dump` — **457 042 byte**). G6 xác nhận `gen:api` không đổi `schema.d.ts`; **0** dòng
migration mới.

## 4. Quyết định mới

- **Không có `D-xx` mới.** Kế thừa `D-269` (mask thật ở một cửa ghi) và các quyết định WP-22 (`D-253`…`D-268`).

## 5. Nợ / rủi ro

- 🟠 **Mask trên body gửi đi** (P2 REV, `D-269`) — chủ **`ba`/`be`, WP-23 (`NOT-002`)**: trước khi bật
  `EnableDispatch`, phải chốt "gửi bản đã che" hay "gửi bản gốc + chỉ che khi hiển thị". Đã ghi ở
  `docs/ops/NOT-001-deploy.md` §7 (dòng 1).
- 🟠 **Negative control NC-2/NC-3 + các mục "chưa kiểm được"** — chủ **`qa`, khi có harness host ABP +
  `WebApplicationFactory`**. `docs/tests/NOT-001-testplan.md` §4 hiện có **15** mục đánh số (không phải 13 như
  handover `N86` ghi — đếm cũ); trong đó **NC-2/NC-3** vẫn **chưa chạy**.
- 🟡 **Lệch tài liệu testplan (P3)**: §4 mục 15 và bảng "Kế hoạch 7 NC" vẫn ghi `NC-8` **"chưa chạy"**, trong khi
  **bảng bằng chứng** (cùng tệp, dòng 78) + handover `N86` đã ghi `NC-8` **đỏ→xanh**. Chủ **`qa`** — sửa 2 dòng
  cho khớp bằng chứng; **không** đổi số ca/nội dung test.
- 🟡 **`Notification:Dispatch:PeriodSeconds` khai báo nhưng CHƯA được đọc** — `NotificationDispatchWorker`
  hard-code `Timer.Period = 10_000` ms. Đổi khoá không đổi nhịp. Chủ **`be`**; đã ghi `docs/ops/NOT-001-deploy.md` §2/§7.
- 🟡 **Server lọc `Marketing`** (`GetTemplatesForBatch`) **chưa có**; FE lọc client (`D-268`). Chủ **`be`** (nợ N1, SRS §15.5).
- 🟡 **Nợ môi trường FE**: `pnpm build` lỗi `EPERM symlink` trên Windows; `pnpm lint` **không có script**. Chủ
  **`ops`/người** — cần xác nhận trên CI (Linux).
- 🟡 **`eCommerce.slnx` bị đổi trong WP-22** (`+6` dòng: 4 project `modules/Notification/*`). Chỉ một luồng được
  sửa `slnx`/`Directory.Packages.props`/`pnpm-lock.yaml` (`K-10`); đã ghi `docs/ops/NOT-001-deploy.md` §1/§7.
- 🟡 **Nợ ARCH** §15.5 N6 (`NOT-001-arch.md` §4.2 còn "12 op") — mở 1 dòng khi `arch` chạm ARCH lần sau.
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (`P-21`, `D-71`/§A.5). `tich-hop/gd3` đã nhận WP-22 ở phiên này.
- **Hàng đợi xem lại**: **169** dòng "Đang chờ" (gồm `D-269`).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền):** chọn NCC kênh 2 + khoá HMAC production (R3-6); merge `main`; duyệt `D-268`/`D-269`.
- **Hàng rào agent**: **1** cảnh báo "thiếu khối JSON cuối" (`ops` — thực tế **có** khối JSON trong báo cáo; harness
  không cấp `last_assistant_message`); **0** lần chặn dừng; **0** tệp ngoài vùng (`ngoaiVung=[]`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-5` → mở `W26-1` (WP-26, F0–F2b).**

- Hàng kế của chuỗi `gd3-1` = **`W26-1`** (WP-26, bước F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module /
  schema / contract; G1/G2 theo M2 — `D-208` tạm duyệt có điều kiện; 10 P3 cùng WP; **KHÔNG** đụng cột giá vốn).
- Chạy `/chay-phien-code chuoi=gd3-1` (không cần mã hàng — `PH next` trả `W26-1`).
- Không gọi NCC, không PII thật, không `IgnoreQueryFilters`, không `migrations add` khi chưa `pg_dump`.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-26
và handover gần nhất `docs/nhanh/handover-n87.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-26** (phiên N88). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`; base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` code/ops **`24fd918`**
  (+ commit đóng phiên chứa tệp này).
- **Đã push** `origin/feat/gd3-wp22-thong-bao` và **fast-forward** `origin/tich-hop/gd3` = tip nhánh WP sau commit
  đóng phiên (kiểm bằng `git log --oneline -1 origin/tich-hop/gd3`).
- **Không** mở PR (`D-216` ③); PR xếp chồng mở một lần cuối chuỗi.

---

**Hàng rào agent** (phiên `N87`): **1** lượt vai — `ops` ×1 (viết `docs/ops/NOT-001-deploy.md`, **không** tự chạy
`-DayDu`). ORCH tự chạy baseline nhẹ + G6 `-DayDu` + push. **0** lần chặn dừng; **0** tệp ngoài vùng.
