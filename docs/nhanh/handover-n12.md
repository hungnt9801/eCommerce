# Handover `N12` — WP-11: **Làm cổng xanh** (9 script hết khuôn chỉ-Windows + bất biến mới cho `CusCustomers`)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-11](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-53](decisions.md), [D-54](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-11** — ① 9 script cũ hết **đường dẫn Windows viết cứng** ·
> ② **quyết dứt điểm** 6 dòng `CusCustomers` của cổng DB · ③ *(tuỳ)* job CI `docker-build`.
> **Không mở rộng phạm vi.**
> 📌 Đây là **nợ cơ chế kiểm chứng**, không phải tính năng: `WP-10` đóng xong GĐ0–GĐ2 nhưng cổng
> **chưa exit 0** ⇒ `cong-15-buoc` (required check) sẽ **đỏ ngay lần push đầu**.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **171 mục** (**50 ` M`** của `N7…N11` chưa commit + **121 `??`**) |
| 2 | `git log --oneline -1` · `git branch --show-current` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (18 phút) |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (15,9 s, incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (2 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **113** tệp `.md`, **581** link markdown, **371** đường dẫn backtick, **0 chết** |

📌 Baseline **khớp** `handover-n11` §1.3/§7 (1961 test · migration 59 · 50/121 mục git). Không có sai lệch phải dừng.

### 1.2 Đo lại HIỆN TRẠNG 2 HỒNG của cổng (**trước** khi sửa — đúng như `N11` mô tả)

| Lệnh | Kết quả đo |
|---|---|
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **32/33** — **1 HỒNG đúng ca `Y1`**: 9 script còn *"dung API chi-Windows"* (`e2e.ps1` · `kiem-openapi-drift.ps1` · `kiem-pos-browser.ps1` · `probe-bao-cao-wp08.ps1` · `probe-don-hang.ps1` · `probe-khach-hang-wp07.ps1` · `probe-kho-fe.ps1` · `probe-kho-wp04.ps1` · `probe-pos.ps1`) |
| `pwsh -File .\tools\kiem-db.ps1` | **24/25** — **1 HỒNG**: `CusCustomers TONG=6` (5 dòng `Khách/Khach WP06·WP07` **có** chứng từ + **1** dòng **0** chứng từ) |

### 1.3 Đã thi công (đúng 3 việc của `07 §WP-11`)

| # | Việc (`07 §WP-11`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **① 9 script cũ hết đường dẫn Windows viết cứng** | Mỗi khuôn chỉ-Windows thay bằng **hàm của `tools/nen-tang.ps1`**: `bin\Debug` → **`Lay-DuongDanBin`**; `Get-NetTCPConnection` → **`Lay-PidDangNghe`**; `curl.exe` → **`Lay-Curl`**; `-WindowStyle Hidden` → **`Start-TienTrinhAn`**; `cmd.exe /c pnpm…` → **`Start-NextAdmin`** *(mới)*; `"$env:TEMP\x"` → **`Lay-DuongDanTam`** *(mới)*; `-o NUL` → `-o (Lay-DuongDanTam 'dang-nhap.out')`. Cộng thêm đường dẫn nhiều đoạn ở `e2e.ps1`/`kiem-openapi-drift.ps1` (`apps\admin-web`, `.next\BUILD_ID`, `packages\api-client\…`) → `[IO.Path]::Combine` | `test-cong-kiem.ps1 -ChiKiemMoc` **33/33** (ca `Y1` **đỏ → xanh**); `e2e.ps1` **8/8**; `kiem-openapi-drift.ps1` **PASS 51/51** (§1.4) |
| 2 | **② quyết dứt điểm 6 dòng `CusCustomers`** | **Bất biến MẠNH HƠN** thay cho *"phải rỗng"* (`D-53`): nhóm **①c** = *(a)* mọi khách **còn sống** phải có **≥ 1 chứng từ** (`PosRetailBills`·`PosRetailReturns`·`OrdOrders`·`CusCares`) + *(b)* **0** bản ghi **xoá mềm**. Đã **xoá đúng 1 dòng vô chủ** (0 chứng từ, 0 tham chiếu); **giữ** 5 dòng có chứng từ | `kiem-db.ps1` **25/25**; `conSong=5 · moCoi=0 · xoaMem=0`; **chiều ngược đã đo**: `INSERT` 1 khách + `ROLLBACK` ⇒ `moCoi 0 → 1 → 0` |
| 3 | **③ job CI `docker-build`** | ⏭ **KHÔNG làm (cắt CÓ QUYẾT ĐỊNH)** — xem §2 #1 | — |

📌 **Ba thứ đã đo để KHÔNG phải đoán** (đều ghi ở `D-54`):
`Start-Process pnpm` **NÉM** *"%1 is not a valid Win32 application"* (pnpm là shim `.cmd`) còn
`cmd.exe /c "pnpm --version"` cho **`exit 0` + `10.22.0`**; `(& (Lay-Curl) -s -o <tệp> -w '%{http_code}')` cho
**`200`** + body ghi đúng tệp; `Start-NextAdmin` bật được Next (`/login` **200**, PID thật).

> 🚨 **PHÁT HIỆN THÊM (đo `-DayDu` — lệnh CI thật):** `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` chạy
> **qua bước ⑨ (33/33)** rồi **ĐỎ ở bước ⑭ `probe-oversell`**: **9 DAT / 2 HỎNG** —
> `T01` (`'1/0'` so với `1.000/0.000`) và `T09` (`'0'` so với `0.000`). 🔑 **Nguyên nhân: `D-31`** (N4b,
> 02/11) đổi 16 cột số lượng sang `decimal(18,3)`, còn `tools/probe-oversell.ps1` **so sánh CHUỖI** ⇒ sai
> **định dạng**, không sai nghiệp vụ (`T04…T08`, `T10` vẫn DAT). 📌 Tệp này **không** bị sửa trong phiên
> (`git status` sạch cho nó; commit cuối `e2c2785` — GĐ1) ⇒ **nợ có sẵn từ `N4b`**, chỉ lộ ra bây giờ vì cổng
> **lần đầu** đi qua bước ⑧. ⛔ **Không sửa** trong `N12` (`08 §6`: *"Test đỏ do thay đổi ngoài dự kiến ⇒
> **không** sửa test cho xanh; dừng, báo"* + ngoài phạm vi `WP-11`) — xem §5 #2.


### 1.4 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1` (**DoD**) | **exit 0** — **9/9** bước · **192,8 s** (chạy **2 lượt**, cả hai `exit 0`: 195,2 s → 192,8 s) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **33/33 DAT · 0 HỎNG** (trước: 32/33) |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HỎNG** (trước: 24/25) |
| `dotnet build eCommerce.slnx` | **0 error / 0 warning** |
| `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (không đổi — WP-11 **không** thêm test BE) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` · `pnpm test` | **✓ 0 lỗi** · **25/25 pass** (bước [6] của cổng) |
| `pwsh -File .\tools\e2e.ps1` | **`8 passed`** (38,6 s) · **exit 0** |
| `pwsh -File .\tools\kiem-openapi-drift.ps1` | **PASS** — **51/51** path, `schema.d.ts` **KHÔNG ĐỔI** |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **114** tệp `.md`, **595** link markdown, **377** đường dẫn backtick, **0** chết (baseline: 113/581/371) |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` *(lệnh CI)* | **ĐỎ ở bước ⑭** — `probe-oversell` **9 DAT / 2 HỎNG** (so sánh **chuỗi** với `decimal(18,3)`) ⇒ **nợ có sẵn từ `N4b`**, **ngoài** phạm vi `WP-11` — xem khối 🚨 ở §1.3 và §5 #2 |
| Mermaid (bước [5] của cổng) | **213** khối · **0** lỗi |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| NND (DB dev) | **93** (không đổi — WP-11 không đổi schema) |

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **114** tệp `.md`, **595** link markdown,
**377** đường dẫn backtick, **0** chết (baseline: 113/581/371).
Mọi tài liệu mới/sửa (`handover-n12`, `decisions` D-53/D-54 + dòng nhật ký, `07 §WP-11`/`§13`, `08 §3`, `README`)
đã được cổng này quét.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **cố ý cắt**, mỗi mục có **nhận chủ**:

1. **③ job CI `docker-build` — KHÔNG làm** (`D-51` ⑦). 📌 **Lý do:** thêm một job **chưa từng đo** vào
   required check là **đúng thứ `WP-11` cảnh báo** (*"đừng để job ở trạng thái đỏ mà không ai đọc"*) — trên
   máy này **không** kiểm được `ubuntu-latest` (cần `corepack` + `pnpm --frozen-lockfile` + BuildKit), mà
   `docker build` cục bộ **đã** đo được ở `N11` (image **354 MB**). Nhận chủ: **P1** (làm cùng lần push đầu
   **nếu** muốn thấy log trước, hoặc khi có runner Linux thật).
2. **Host .NET vẫn KHÔNG nằm trong compose** (`D-51` ①) — một WP riêng, không phải thiếu sót của `WP-11`.
3. **Chưa đo luồng đăng nhập thật trong container `admin-web`** (chỉ `/login` 200 + `/api/health`) — nhận chủ
   `WP-11` trong `handover-n11` §2 #3, **vẫn mở**.
4. **`public/` không tồn tại** ⇒ `Dockerfile` không copy (thêm thư mục đó phải thêm một dòng `COPY`) — như `N11`.
5. 🚨 **`probe-oversell.ps1` còn ĐỎ khi chạy `-DayDu`** (bước ⑭) — **KHÔNG sửa trong phiên này**, xem §5 #2:
   đó là **nợ có sẵn từ `N4b`** (`D-31`) và `08 §6` chốt *"test đỏ do thay đổi ngoài dự kiến ⇒ không sửa
   test cho xanh"*. 📌 **Hệ quả cần biết trước khi push:** `cong-15-buoc` (chạy `-DayDu`) **vẫn đỏ** cho tới khi
   có WP sửa probe này. **Nhận chủ: `WP-12`/P1** (phải viết vào `07` trước khi mở phiên — `08 §5.1` luật 5).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-11` là **cổng + script + docs**; bất biến mới của `CusCustomers` là **truy vấn đọc** trong `kiem-db.ps1`, **không** bảng/cột/enum. ✅ Bằng chứng: integration **96/96** (gồm `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT`), `schema.sql` **không đổi** |
| **Dữ liệu đã xoá** | **1 dòng** `CusCustomers` (**`3a24b677-4009-ce02-f1a9-a3baac8c9426`** · `Khách WP06 220419` · tạo 02:01:42) — **0** hoá đơn, **0** phiếu trả, **0** đơn, **0** chăm sóc, **0** dòng fact. 📌 Đây là dòng **vô chủ** duy nhất; **5** dòng còn lại **giữ nguyên** (có **11** hoá đơn + **3** phiếu trả + **8** dòng fact trỏ vào) |
| Cách lùi | Không cần (không có thay đổi schema). Dòng đã xoá là rác probe, **không** tái tạo bằng script |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-53** | **`CusCustomers` RA khỏi nhóm "phải rỗng"** — thay bằng bất biến **MẠNH HƠN** (nhóm ①c): *mọi khách còn sống phải có ≥ 1 chứng từ* + *0 bản ghi xoá mềm*. 🔑 **Lý do đo được:** `D-42` ④ buộc POS có khách + API **không có** `DELETE` khách ⇒ probe **không thể** dọn ⇒ cổng "phải rỗng" là cổng **luôn đỏ**; và xoá cả 6 dòng là **xoá bằng chứng của chính chứng từ**. ✅ Bất biến mới **bắt được 1 dòng rác thật** ngay lượt đầu ⇒ **mạnh hơn**, không phải "nới cổng". ⛔ Không tắt cổng, không dùng `-ChoPhepRong` |
| **D-54** | **"Một chỗ" cho Windows/Linux:** +`Lay-DuongDanTam` (`[IO.Path]::GetTempPath()` — `$env:TEMP` **không có** trên Linux) · +`Start-NextAdmin` (`Start-Process pnpm` **NÉM** trên Windows, `cmd.exe` **không có** trên Linux — **đo được cả hai**) · `Start-TienTrinhAn` thêm `-DanhSachThamSo [string[]]` (đường dẫn có dấu cách) + 3 tham số log thành **tuỳ chọn** (15 chỗ gọi cũ **không đổi hành vi**) · `Lay-MauHamNenTang` của `test-cong-kiem.ps1` **biết 2 hàm mới** |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`probe-kho-wp04.ps1` KHÔNG chạy được ở trạng thái DB hiện tại** — **KHÔNG do `N12`**: probe xin token
   bằng **password grant** (`/connect/token`) trong khi cổng DB (nhóm ②) **bắt buộc**
   `admin.ShouldChangePasswordOnNextLogin = true` ⇒ OpenIddict trả **`invalid_grant` /
   `ShouldChangePasswordOnNextLogin`**. 📌 Các probe `WP06`/`WP07` **không** dính vì chúng `UPDATE` cờ đó bằng
   SQL trước khi gọi (và `e2e.ps1` tắt/mở lại trong setup/teardown — `D-49` ③). **Nhận chủ:** `WP-11` P1 —
   hoặc cho probe đi qua **BFF/NextAuth** như `probe-pos`, hoặc dùng chung một helper "mở cờ tạm rồi bật lại".
   ⛔ **Đừng** tắt nhóm ② của `kiem-db.ps1` để cho probe chạy.
2. 🚨 **`-DayDu` (lệnh CI `cong-15-buoc`) ĐỎ ở bước ⑭ `probe-oversell` — ĐO ĐƯỢC, KHÔNG do `N12`:** chạy
   `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` ⇒ bước ①…⑬ **xanh** (kể cả ⑧ DB **25/25** và ⑨ **33/33**),
   bước ⑭ **`9 DAT / 2 HONG`**:
   `T01` so chuỗi `'1/0'` trong khi DB trả `1.000/0.000`; `T09` so `'0'` trong khi DB trả `0.000`.
   🔑 **Nguyên nhân: `D-31`** (`N4b`, 02/11 — `decimal(18,3)` cho 16 cột số lượng) ⇒ probe **so sánh CHUỖI**
   nên sai **định dạng**, **không** sai nghiệp vụ (`T04…T08`, `T10` vẫn **DAT**; `git status` **sạch** cho tệp
   này, commit cuối `e2c2785` = GĐ1). 📌 Nó **chỉ lộ ra bây giờ** vì cổng **lần đầu** đi qua bước ⑧ (`N7…N11`
   luôn dừng ở ⑧). ⛔ **Không sửa** trong `N12`: `08 §6` chốt *"test đỏ do thay đổi ngoài dự kiến ⇒ **không**
   sửa test cho xanh; dừng, báo"*, và nó **ngoài** 3 gạch đầu dòng của `WP-11`.
   **Cách sửa (đúng, nhỏ):** so **số** (`[decimal]$phan[2] -eq 0`, và `T01` so `[decimal]` hai vế) — **không**
   nới điều kiện. **Nhận chủ: `WP-12`/P1** → ⚠️ **viết `§WP-12` vào `07` trước** rồi mới mở phiên.
   ⛔ **Đừng** bỏ `-DayDu` khỏi CI để "cho xanh" — đó là **tắt cổng**.
3. 📌 **Các bước ⑮–⑰ của `-DayDu` CHƯA đo** (`outbox` · `outbox 2 instance` · `giu-cho 2 instance` ·
   Playwright) vì lượt chạy **dừng ở ⑭**. 📌 Chúng có thể có cùng lớp lỗi (so sánh chuỗi sau `decimal`).
   ⇒ Khi sửa ⑭: chạy **lại `-DayDu` trọn lượt** rồi mới kết luận CI xanh.
4. ⚠️ **`0` migration mới nhưng DB dev đã bị xoá 1 dòng** — nếu ai dựng lại DB từ backup cũ, `CusCustomers`
   sẽ có **6** dòng; cổng ①c **vẫn xanh** cho 5 dòng có chứng từ và **đỏ** đúng ở dòng vô chủ.
   ⇒ 🔑 Đó **chính là** điều phép kiểm mới sinh ra để bắt: xoá dòng vô chủ rồi đo lại.
5. ⚠️ **Image `ecommerce-admin-web:local` + container đã `stop`** vẫn còn (`docker ps -a`) — như `N11` §5 #8.
6. ⚠️ **`.next/standalone` cảnh báo `next start` không dùng với `output: standalone`** (thấy khi đo
   `Start-NextAdmin`): chỉ là **cảnh báo**, `/login` vẫn **200** và 8/8 E2E vẫn xanh — 📌 nhưng nếu sau này
   chuyển hẳn sang `node .next/standalone/server.js` thì `tools/e2e.ps1` + `playwright.config.ts` phải đổi
   **cùng lượt** (nếu không, `next start` bật một bản không có `server.js`).
7. ⚠️ **Docker Desktop trên máy này đã tự tắt một lần** (`N11` §5 #7) ⇒ nếu `docker compose ps` thiếu
   postgres/redis: bật Docker Desktop rồi `docker compose up -d postgres redis`.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-11 → mở `WP-12` (Làm `-DayDu` xanh — phát sinh từ `N12`) — phiên `N13`.**

> 📌 `WP-11` **đã đạt DoD** (`kiem-tat-ca.ps1` **exit 0**, 9/9 bước). 🔑 Nhưng phiên này **đo thêm** lệnh CI
> thật (`-DayDu`) và thấy **bước ⑭ đỏ** vì nợ có sẵn từ `N4b` ⇒ `07` đã thêm **`§WP-12`** đúng khuôn mà
> **`WP-11` từng được sinh ra** (một WP "làm cổng xanh" phát sinh từ số đo của phiên trước). ⛔ **Không** tự
> đặt WP khác; muốn mở P1 thì người lập kế hoạch phải viết `§WP-13` vào `07` **trước** (`08 §5.1` luật 5).

1. Đọc `07 §WP-12` + §5 #2/#3 của tệp này: sửa `probe-oversell` so **số** thay vì so **chuỗi** (`D-31`), rồi
   chạy **trọn** `-DayDu` và sửa **hết** bước đỏ (⛔ **không** bỏ `-DayDu` khỏi CI, **không** nới điều kiện).
   📌 DoD của `WP-12` = `-DayDu` **exit 0** với **17** bước, **và** bản nhẹ vẫn `exit 0`.
2. *(chỉ nếu ① xong)* `WP-11` ③ — job CI `docker-build`; chỉ thêm sau khi thấy nó xanh **một lần** trên
   `ubuntu-latest`.
3. *(nợ §5 #1)* probe `probe-kho-wp04.ps1` không qua được `ShouldChangePasswordOnNextLogin`: cho nó đi qua
   **BFF/NextAuth** hoặc dùng chung helper "mở cờ tạm rồi **bật lại**" (`D-49` ③).
   ⛔ **Đừng** tắt nhóm ② của `kiem-db.ps1`.
4. ⚠️ Sửa bất kỳ script nào trong `tools/`: `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` (**33/33**).
5. ⚠️ Sau khi sửa đường đăng nhập/BFF/POS/kho/đơn: `pwsh -File .\tools\e2e.ps1`
   (và **`docker compose stop admin-web`** nếu container đang chạy — `handover-n11` §5 #2).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
pwsh -File .\tools\kiem-tat-ca.ps1               # DoD WP-11: exit 0, 9/9 buoc
pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc   # 33/33
pwsh -File .\tools\kiem-db.ps1                   # 25/25
pwsh -File .\tools\e2e.ps1                       # 8 luong
pwsh -File .\tools\kiem-openapi-drift.ps1        # 51/51, 0 drift
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-12
và handover gần nhất `docs/nhanh/handover-n12.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-12** (phiên N13). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N12`) |
| Commit | **chưa commit** phiên này — working tree **56 ` M`** + **122 `??`** (baseline: **50 ` M`** + **121 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A`. 📌 **6 tệp ` M` thêm** = `tools/nen-tang.ps1`, `tools/kiem-db.ps1`, `tools/test-cong-kiem.ps1`, `tools/probe-don-hang.ps1`, `tools/probe-kho-fe.ps1`, `tools/probe-kho-wp04.ps1` + docs |
| Push / PR | Chưa |
| Tệp `N12` đã tạo | `docs/nhanh/handover-n12.md` |
| Tệp `N12` đã sửa | `tools/nen-tang.ps1` (+`Lay-DuongDanTam`, +`Start-NextAdmin`, `Start-TienTrinhAn` nhận mảng đối số) · `tools/test-cong-kiem.ps1` (`Lay-MauHamNenTang` +2 tên) · `tools/kiem-db.ps1` (nhóm ①c `CusCustomers`, `$bangPhaiRong`/`$bangGiuLichSu`, `.DESCRIPTION`) · **9 script cổng**: `e2e.ps1` · `kiem-openapi-drift.ps1` · `kiem-pos-browser.ps1` · `probe-bao-cao-wp08.ps1` · `probe-don-hang.ps1` · `probe-khach-hang-wp07.ps1` · `probe-kho-fe.ps1` · `probe-kho-wp04.ps1` · `probe-pos.ps1` · docs (`decisions` D-53/D-54 + 2 dòng nhật ký, `07 §WP-11` + **`§WP-12` mới** + `§13`, `08 §3` (N12 ✅ + N13), `README`) |
| Dữ liệu `N12` đã xoá | `CusCustomers` **1** dòng (`Khách WP06 220419`) — xem §3 |
