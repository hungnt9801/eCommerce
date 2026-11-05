# Handover `N13` — WP-12: **Làm `-DayDu` XANH** (so **số** thay vì so **chuỗi** + sửa bước ⑲)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-12](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-55](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-12** — ① `probe-oversell` so **SỐ** (không so chuỗi) + soát probe cùng họ ·
> ② chạy **trọn** `-DayDu` và sửa **hết** bước đỏ · ③ *(tuỳ)* job CI `docker-build`. **Không mở rộng phạm vi.**
> 📌 Đây là **nợ cơ chế kiểm chứng** (required check `cong-15-buoc` chạy `-DayDu`), không phải tính năng.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **178 mục** (**56 ` M`** của `N7…N12` chưa commit + **122 `??`**) |
| 2 | `git log --oneline -1` · `git branch --show-current` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` · `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (57 phút) — không phải dựng lại |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (26,65 s, incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (3 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **114** tệp `.md`, **595** link markdown, **377** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n12` §7 (56 ` M` + 122 `??`) và §1.4 (1961 test · 114 tệp / 595 link) —
không có sai lệch phải dừng.

### 1.2 Hiện trạng HỎNG đã biết — **chứng minh lớp lỗi** trước khi sửa

`N12` đo `-DayDu`: bước cuối chạy được là `probe-oversell`, **9 DAT / 2 HỎNG** —
`T01` so chuỗi `'1/0'` trong khi DB trả `1.000/0.000`; `T09` so `'0'` trong khi DB trả `0.000`.
Không cần chạy lại bản cũ, **chứng minh trực tiếp** (PowerShell, cùng máy):

```
so chuoi:      '1.000/0.000' -eq '1/0'                                   → False   ← 2 HỎNG của N12
so SO (moi):   [decimal]::Parse('1.000', InvariantCulture) -eq 1         → True
Available cu:  '0.000' -eq '0'                                           → False
Available moi: [decimal]::Parse('0.000', InvariantCulture) -eq 0         → True
```

🔑 **Nguyên nhân gốc:** `D-31` (`N4b`, 02/11) đổi **16 cột số lượng** sang `decimal(18,3)` ⇒ `psql` in `1.000`;
probe **so chuỗi** với `1` ⇒ ĐỎ **vì định dạng**, không vì nghiệp vụ (T04…T08, T10 vẫn DAT).

**Soát cùng họ (theo `07 §WP-12` ①) — đo trên 6 tệp được nêu tên:**

| Tệp | Có cùng lớp lỗi? | Bằng chứng |
|---|---|---|
| `tools/probe-oversell.ps1` | ✅ **CÓ** | `T01` → `'1/0'`; `T08` `[int]`; `T09` → `'0'` |
| `tools/probe-giu-cho-2instance.ps1` | ✅ **CÓ** (bị bỏ sót ở `N12`) | `P01` → `'1/0'`; `P09` `[int]`; `P10` → `'0'`; `P11` → `'2/0'` |
| `tools/probe-outbox.ps1` · `probe-outbox-2instance.ps1` | ❌ không | so `FulfillmentVersion` (**int**) và `count(*)` — `bigint` in `1`, không có scale |
| `tools/probe-don-san.ps1` | ❌ không | so watermark qua **JSON** (`[long]`/số), không `Psql … -eq '…'` cho cột số lượng |
| `tools/probe-thanh-toan.ps1` | ❌ không | so `lineTotal` từ **JSON**; các ca DB dùng `count(*)` |
| `tools/probe-khach-hang.ps1` · `probe-so-dia-chi.ps1` | ❌ không | không so cột số lượng bằng `Psql` + `-eq '…'` |

📌 Ngoài 2 tệp trên, các probe còn lại **không** cần sửa — ghi lại để phiên sau không "sửa cho đủ".

### 1.3 Đã thi công (đúng 2 việc bắt buộc của `07 §WP-12`)

| # | Việc (`07 §WP-12`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **① `probe-oversell` so SỐ** | Thêm hàm `So()` = `[decimal]::Parse(…, InvariantCulture)`; `T01` tách `OnHand/Reserved` rồi so **số**; `T08` `So` thay `[int]`; `T09` `So` thay so chuỗi. 📌 Parse **invariant** vì trên máy `vi-VN` dấu `.` là **phân cách nghìn** (`[decimal]'1.000'` = **1000** nếu để culture máy quyết định) | `-DayDu`: `T01 … DAT | ton = 1.000/0.000` · `T09 … DAT | Available = 0.000` · probe **11 DAT / 0 HONG** |
| 1b | **Soát probe cùng họ** | `probe-giu-cho-2instance`: `P01`/`P11` (seed) + `P09` (`[int]`) + `P10` (chuỗi) → `So`; 3 probe còn lại **không** cùng lớp (§1.2) | probe **17 DAT / 0 HONG** — `P01 ton = 1.000/0.000` · `P10 Available = 0.000` · `P11 ton = 2.000/0.000` |
| 2 | **② Chạy TRỌN `-DayDu` + sửa hết bước đỏ** | Lượt 1: ①…⑱ **xanh** (gồm ⑮ oversell, ⑯ outbox, ⑰ outbox 2 instance, ⑱ giữ chỗ 2 instance), **⑲ ĐỎ vì lỗi in của cổng** — xem khối 🚨 dưới. Đã sửa, chạy lại **trọn**: **exit 0 · 19/19 bước · 462,5 s** | `-DayDu` **exit 0**, `TONG SO BUOC DAT: 19`; Playwright **8/8**; không còn bước HỎNG |
| 3 | **③ job CI `docker-build`** | ⏭ **KHÔNG làm (cắt CÓ QUYẾT ĐỊNH)** — chưa có `ubuntu-latest` để đo **một lần thật**; thêm job **chưa đo** vào required check là đúng thứ `WP-11` cảnh báo. Nhận chủ: **`WP-13` ②**/P1 | — |

> 🚨 **BƯỚC ⑲ — lỗi của CHÍNH CỔNG, không phải của sản phẩm (đo được):**
> Playwright đã **`8 passed (36,9 s)` + `✅ E2E: 8/8 luong XANH`**, nhưng cổng **NÉM** ngay sau đó:
> `You cannot call a method on a null-valued expression` tại `kiem-tat-ca.ps1:636`.
> 🔑 **Nguyên nhân gốc:** `Get-Content` trả **CHUỖI**, còn `Select-String` mới trả `MatchInfo` —
> `$_.Line` **chỉ có** trên `MatchInfo`. Bước ⑲ in bằng `$_.Line.Trim()` sau `Get-Content` ⇒ `$_.Line = $null`.
> 📌 Nó **chưa từng lộ** vì từ `N7` cổng luôn dừng ở bước ⑧/⑭ ⇒ bước ⑲ **chưa chạy qua đường in này** lần nào.
> ✅ Sửa **đúng một chỗ**: `$_.Line.Trim()` → `$_.Trim()`. Các chỗ còn lại **giữ** `.Line` vì đi sau `Select-String`
> (đã soát 8 chỗ — §5 #6).

### 1.4 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` (**DoD**) | **exit 0** — **19/19** bước · **462,5 s** (lượt 2; lượt 1 dừng ở ⑲ vì lỗi in, Playwright đã 8/8) |
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **193,5 s** |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **33/33 DAT · 0 HỎNG** (trong cổng, đủ nhóm DB: **38/38**) |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HỎNG** |
| `dotnet build eCommerce.slnx` | **0 error / 0 warning** (trong cổng 57,5 s, `--no-incremental`) |
| `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (không đổi — WP-12 **không** thêm test BE) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` · `pnpm test` | **✓ 0 lỗi** · **25/25 pass** (bước [6] của cổng) |
| `pwsh -File .\tools\e2e.ps1` *(trong bước ⑲)* | **`8 passed`** (35,7 s) · **exit 0** |
| `pwsh -File .\tools\kiem-openapi-drift.ps1` | **PASS** — **51/51** path, `schema.d.ts` **KHÔNG ĐỔI** |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **115** tệp `.md`, **610** link markdown, **383** đường dẫn backtick, **0** chết (baseline: 114/595/377) |
| Probe trong `-DayDu` | địa chỉ **17/17** · khách **26/26** · thanh toán **30/30** · đơn sàn **49/49** · oversell **11/0** · outbox **9/0** · outbox 2 instance **9/0** · giữ chỗ 2 instance **17/0** · Playwright **8/8** |
| Mermaid (bước [5] của cổng) | **213** khối · **0** lỗi |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| NND (DB dev) | **93** (không đổi — WP-12 không đổi schema) |

📌 **Con số `17` trong DoD của `07 §WP-12` là ĐẾM SAI** — cổng có **19** `Buoc` với `-DayDu`
(9 bước nhẹ + **10** bước nặng; bước ⑲ Playwright thêm ở `N10` bị bỏ sót khi đếm). Đã sửa `07` + ghi `D-55` ②;
⛔ **KHÔNG** xoá bước nào để khớp con số.

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **115** tệp `.md`, **610** link markdown,
**383** đường dẫn backtick, **0** chết (baseline: 114/595/377).
Mọi tài liệu mới/sửa (`handover-n13`, `decisions` D-55 + dòng nhật ký, `07 §WP-12`/`§WP-13`/`§13`,
`08 §3`/`§8`, `README`) đã được cổng này quét.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **cố ý cắt**, mỗi mục có **nhận chủ**:

1. **③ job CI `docker-build` — KHÔNG làm** (`D-51` ⑦, `WP-11` ③): chưa có runner `ubuntu-latest` để thấy
   nó **xanh một lần thật**; thêm job chưa đo vào required check là **đúng thứ `WP-12` cảnh báo**. Nhận chủ:
   **`WP-13` ②** (P1).
2. **`probe-kho-wp04.ps1` vẫn KHÔNG chạy được** (nợ §5 #1 của [handover-n12](handover-n12.md)) — probe xin token
   bằng password grant trong khi cổng DB (nhóm ②) buộc `admin.ShouldChangePasswordOnNextLogin = true`. **Ngoài**
   phạm vi WP-12 (không nằm trong `-DayDu`) ⇒ **không sửa** trong phiên này; nhận chủ **`WP-13` ①** (đã viết
   `§WP-13` vào `07` trước khi mở phiên, đúng `08 §5.1` luật 5).
3. **Các bước ⑮–⑰ của `-DayDu` mà `N12` ghi "CHƯA đo" — NAY ĐÃ ĐO HẾT:** outbox **9/0**, outbox 2 instance
   **9/0**, giữ chỗ 2 instance **17/0**, Playwright **8/8** ⇒ **nợ #3 của `handover-n12` §2 ĐÓNG**.
4. **Không sửa `probe-kho-wp04` "cho xanh"** và **không** tắt nhóm ② của `kiem-db.ps1`.
5. ⚠️ **Image `ecommerce-admin-web:local` + container đã `stop`** vẫn còn (`docker ps -a`) — như `N11` §5 #8.
6. ⚠️ **Docker Desktop trên máy này đã tự tắt một lần** (`N11` §5 #7) — nếu `docker compose ps` thiếu
   postgres/redis: bật Docker Desktop rồi `docker compose up -d postgres redis`.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-12` chỉ sửa **2 probe + 1 câu in của cổng** + docs; **không** bảng/cột/enum. ✅ Bằng chứng: integration **96/96** (gồm `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT`), `schema.sql` **không đổi** |
| Cách lùi | Không cần (không có thay đổi schema). Hai probe đã sửa là **script đo**, không ghi dữ liệu thường trực |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-55** | **`-DayDu` XANH (`WP-12`)**: ① so **SỐ** cho cột `decimal(18,3)` bằng parse **invariant** (2 probe, 7 ca) — `psql` in `1.000`, so chuỗi làm probe **ĐỎ OAN**; ⛔ không nới điều kiện/bỏ ca · ② DoD **17 bước** là **đếm sai**: gate có **19** `Buoc` với `-DayDu` ⇒ DoD đúng = **exit 0 với 19/19**, **không xoá bước** · ③ **`Get-Content` trả CHUỖI, `Select-String` trả `MatchInfo`** — `$_.Line` chỉ có trên `MatchInfo`; bước ⑲ in bằng `$_.Line.Trim()` sau `Get-Content` ⇒ **NÉM** dù Playwright 8/8 xanh ⇒ cổng đỏ vì **lỗi in**; sửa `$_.Trim()` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`probe-kho-wp04.ps1` không chạy được** (password grant vs `ShouldChangePasswordOnNextLogin`) —
   nhận chủ **`WP-13` ①**. Cách sửa **đúng, nhỏ**: helper dùng chung trong `tools/nen-tang.ps1`
   (*mở cờ tạm → `finally` bật lại*) cho cả 3 probe đang tự `UPDATE` cờ, hoặc cho probe đi qua **BFF/NextAuth**
   như `probe-kho-fe`. ⛔ **Đừng** tắt nhóm ② của `kiem-db.ps1`.
2. 🚨 **Job CI `docker-build` vẫn chưa có** — nhận chủ **`WP-13` ②**; chỉ thêm **sau** khi thấy nó xanh
   **một lần** trên `ubuntu-latest`.
3. ⚠️ **`count(*)` vẫn so chuỗi** trong 3 probe (`-eq '1'`): **ĐÚNG** vì `bigint` in `1` (không scale).
   📌 Nếu ai đổi các cột đó sang `decimal` thì phải dùng `So` — đừng "đồng bộ cho đẹp" rồi làm đỏ oan.
4. ⚠️ **`probe-kho-wp04.ps1` ⑫ so hai chuỗi decimal** (`$tongSoCai -ne $anhChup`): hiện **đúng** vì cả hai
   cùng scale 3 (cùng `numeric(18,3)`); nếu một vế đổi scale/kiểu thì **sai âm thầm** ⇒ khi sửa `WP-13` ①,
   đổi luôn sang `So`.
5. ⚠️ **`-DayDu` giờ chạy ~462,5 s** (bước ⑲ Playwright ~37 s, bước ⑨ test cổng ~109 s). 📌 Vẫn dưới ngưỡng
   chấp nhận; nếu CI chậm quá, **đừng** bỏ bước — cắt theo `-BoQuaIntegration` chỉ dùng cho vòng lặp nhanh.
6. 🔒 **Quy ước `.Line`:** chỉ dùng `$_.Line` **sau `Select-String`**; sau `Get-Content` dùng `$_` trực tiếp.
   Đã soát 8 chỗ trong `kiem-tat-ca.ps1`: 7 chỗ đi sau `Select-String` (đúng), 1 chỗ sai đã sửa.
7. ⚠️ **`.next/standalone` cảnh báo `next start`** (thấy ở `N12` §5 #6) — vẫn chỉ là cảnh báo; `/login` 200,
   8/8 E2E xanh.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-12 → mở `WP-13` (P1 — đóng 2 nợ **kiểm chứng** còn lại) — phiên `N14`.**

> 📌 `WP-12` **đã đạt DoD** (`-DayDu` **exit 0**, **19/19** bước; bản nhẹ **exit 0**, 9/9). 🔑 Phiên này **đóng**
> nợ #2/#3 của `handover-n12` và **mở `WP-13`** cho 2 nợ **không** chặn CI nhưng **đã đo được là không chạy được**:
> `probe-kho-wp04` (auth) và job `docker-build` (chưa có runner). ⛔ **Không** tự đặt WP khác ngoài `WP-13`;
> muốn mở P1 mới thì người lập kế hoạch phải viết `§WP-14` vào `07` **trước** (`08 §5.1` luật 5).

1. Đọc `07 §WP-13` + §5 #1/#2 của tệp này: cho `probe-kho-wp04.ps1` qua được cổng auth bằng helper
   **mở cờ tạm → `finally` bật lại** (đặt trong `tools/nen-tang.ps1`) hoặc qua BFF; ⛔ **không** tắt nhóm ②
   của `kiem-db.ps1`.
2. Chạy baseline 6 lệnh ở `08 §4`, rồi `pwsh -File .\tools\probe-kho-wp04.ps1` (12 ca) + `kiem-db.ps1` **25/25**.
3. *(chỉ nếu ① xong)* `WP-13` ② — job CI `docker-build`; chỉ thêm sau khi thấy nó xanh **một lần** trên
   `ubuntu-latest`.
4. ⚠️ Sửa bất kỳ script nào trong `tools/`: `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` (**33/33**).
5. ⚠️ Sau khi sửa script cổng/probe: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) — và trước khi push,
   `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` (**19/19**).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
pwsh -File .\tools\kiem-tat-ca.ps1               # 9/9, exit 0
pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu        # 19/19, exit 0
pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc   # 33/33
pwsh -File .\tools\kiem-db.ps1                   # 25/25
pwsh -File .\tools\probe-kho-wp04.ps1            # DoD WP-13: 12 ca
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n13.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-13** (phiên N14). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N13`) |
| Commit | **chưa commit** phiên này — working tree **58 ` M`** + **123 `??`** (baseline: **56 ` M`** + **122 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A`. 📌 **2 tệp ` M` thêm** = `tools/probe-oversell.ps1` + `tools/probe-giu-cho-2instance.ps1` (trước phiên **sạch**); `tools/kiem-tat-ca.ps1` vốn đã ` M` từ `N10` |
| Push / PR | Chưa |
| Tệp `N13` đã tạo | `docs/nhanh/handover-n13.md` |
| Tệp `N13` đã sửa | `tools/probe-oversell.ps1` (hàm `So` + `T01`/`T08`/`T09`) · `tools/probe-giu-cho-2instance.ps1` (hàm `So` + `P01`/`P09`/`P10`/`P11`) · `tools/kiem-tat-ca.ps1` (một dòng `$_.Line.Trim()` → `$_.Trim()` + ghi chú) · docs (`decisions` D-55 + dòng nhật ký, `07 §WP-12`/`§WP-13`/`§13`, `08 §3`/`§8`, `README`) |
| Dữ liệu `N13` đã xoá | **Không** — 2 probe tự dọn (`T10`/`P15`), `-DayDu` kết thúc với DB **25/25** |
