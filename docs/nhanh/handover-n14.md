# Handover `N14` — WP-13: **đóng 2 nợ kiểm chứng** (cờ `ShouldChangePasswordOnNextLogin` = **một chỗ**)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-56](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-13** — ① cho `probe-kho-wp04.ps1` qua được cổng auth bằng **một chỗ**
> (helper trong `tools/nen-tang.ps1`: mở cờ tạm → `finally` **bật lại**) hoặc qua BFF · ② *(tuỳ)* job CI
> `docker-build`. **Không mở rộng phạm vi.** 📌 Đây là **nợ kiểm chứng**, không phải tính năng.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **181 mục** (**58 ` M`** của `N7…N13` chưa commit + **123 `??`**) |
| 2 | `git log --oneline -1` · `git branch --show-current` | `39b8766` · nhánh `feature/wp-03-catalog` (không có `git switch` — đúng nhánh của WP) |
| 3 | `docker compose up -d` · `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (2 giờ) — không phải dựng lại |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (25,51 s, incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (2 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **115** tệp `.md`, **610** link markdown, **383** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n13` §1.4/§7 (1961 test · 115 tệp / 610 link · 58 ` M` + 123 `??`) —
không có sai lệch phải dừng.

### 1.2 Hiện trạng HỎNG — **đo được**, không suy đoán

**① `probe-kho-wp04.ps1` không qua cổng auth** (nợ `handover-n12` §5 #1). Chạy **trước** khi sửa:

```text
pwsh -File .\tools\probe-kho-wp04.ps1   →  EXIT=1
Invoke-RestMethod: …\tools\probe-kho-wp04.ps1:118 … /connect/token
  {"error":"invalid_grant","error_description":"ShouldChangePasswordOnNextLogin",
   "error_uri":"https://documentation.openiddict.com/errors/ID2024", …}
```

🔑 **Nguyên nhân gốc:** `DbMigrator` **bật** cờ `AbpUsers.ShouldChangePasswordOnNextLogin` cho `admin`
(`D-49` ③a, chủ ý an toàn) ⇒ OpenIddict từ chối `password` grant. ⚠️ Thông điệp *"invalid_grant"* rất dễ bị
đọc thành *"sai mật khẩu"* — **không phải**. Trạng thái cờ lúc đó: `ADMIN|true`.

**② Lỗ "quên bật lại" (chưa ai đo trước `N14`).** Quét `tools/*.ps1`:

| Nhóm | Số tệp | Ghi chú |
|---|---|---|
| `UPDATE … = false` **và** có `UPDATE … = true` | **10** | đúng nhưng **sao chép** SQL ở từng tệp (nguồn của lỗi nhóm dưới) |
| `UPDATE … = false` **KHÔNG** bao giờ `= true` | 🚨 **4** | `probe-pos` · `probe-don-hang` · `probe-bao-cao-wp08` · `probe-khach-hang-wp07` — 📌 **`07 §WP-13` ghi 3, đo được 4** |
| không tắt cờ (⇒ hỏng như ①) | **1** | `probe-kho-wp04` |
| hạ tầng, không phải probe | 2 | `kiem-db.ps1` (nhóm ② **kiểm** cờ) · `kiem-e2e.ps1` (tắt ở setup / **bật lại** ở teardown) |

⇒ Chạy `probe-pos`/`probe-don-hang`/`probe-bao-cao-wp08`/`probe-khach-hang-wp07` xong là **để lại một admin
không bị buộc đổi mật khẩu**, và `kiem-db.ps1` nhóm ② đỏ ở **lượt sau** — ⚠️ và cách "sửa" nhanh nhất của
người vội là **tắt phép kiểm** (đúng điều `.DESCRIPTION` của `kiem-db.ps1` cảnh báo).

### 1.3 Đã thi công (đúng 2 việc của `07 §WP-13`)

| # | Việc (`07 §WP-13`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **① qua cổng auth — MỘT CHỖ** | `tools/nen-tang.ps1` thêm **4 hàm**: `Dat-CoBuocDoiMatKhau` (gốc: `UPDATE` + **xác nhận** `count=1`; ghi SQL ra **tệp tạm** để `docker` không bị mất `exit code`), `Invoke-KhongCoDoiMatKhau` (**mở → chạy khối → `finally` bật lại**), `Start-KhongCoDoiMatKhau` + `BatLai-CoDoiMatKhau` (cặp cho probe tắt **suốt** lượt chạy). `probe-kho-wp04` xin token **trong** wrapper ⇒ không tự `UPDATE` | probe **exit 0**, **12 ca** (①…⑫); cờ `ADMIN\|true` **ngay sau** lượt chạy |
| 1b | **①b đóng lỗ "quên bật lại"** | 4 probe ở §1.2 ② chuyển sang `Start-KhongCoDoiMatKhau` + `BatLai-CoDoiMatKhau` ở **dòng đầu `finally`**. 🚨 **Vì sao cần cặp hàm (không phải wrapper):** 4 probe này đi **cả** token Host **và** đăng nhập **BFF/NextAuth** — BFF cũng xin token bằng `password` grant ⇒ mở cờ quanh **một khối ngắn là không đủ** | `probe-pos.ps1` **exit 0 — 34 lời gọi HTTP** (bán/trả/huỷ/kết ca đủ), `expectedCash 500.000`/`variance 0`, bất biến **9/7/9 = 9/7/9**; cờ về `ADMIN\|true`; `kiem-db.ps1` **25/25** ngay sau |
| 1c | **①c 2 ca kiểm mới** (chống tái phát) | `test-cong-kiem.ps1`: **Y6** — quét *script nào `Start-KhongCoDoiMatKhau` phải có `BatLai-CoDoiMatKhau`* (**hai chiều**: phải thấy **≥ 4** tệp, nếu không phép quét vô nghĩa; bỏ qua chính tệp test); **L2** (nhóm DB) — cờ `false` **bên trong** khối, `true` sau; **và** ở **tiến trình con**: khối `exit 7` ⇒ exit code **7** giữ nguyên + cờ `true` | test cổng **34/34** (bản nhẹ; trước: **33/33**) · **40/40** (full, có DB; trước: **38/38**) |
| 1d | **⑫ so SỐ** (việc `handover-n13` §5 #4 giao) | `probe-kho-wp04` ⑫ thêm `So` = `[decimal]::Parse(…, InvariantCulture)` (`D-55` ①) thay so **chuỗi** giữa `tổng sổ cái` và `OnHand`; thêm ca *"đọc không được ⇒ cảnh báo"* thay vì parse rỗng | ⑫ `tổng sổ cái = 10.000 · OnHand = 10.000` (**so số**, không so chuỗi) |
| 2 | **② job CI `docker-build`** | ⏭ **KHÔNG làm (cắt CÓ QUYẾT ĐỊNH, giữ P1)** — xem §2 #1 | — |

> ✅ **Đo TRƯỚC/SAU của đúng nợ này** (cùng máy, cùng DB):
> `probe-kho-wp04` **exit 1** (`invalid_grant`/`ID2024`) → **exit 0** (**12 ca**) · cờ `ADMIN|true` **trước và sau**.

### 1.4 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\probe-kho-wp04.ps1` (**DoD ①**) | **exit 0** — **12 ca** (① tạo kho … ⑫ bất biến **10.000 = 10.000**) |
| `pwsh -File .\tools\probe-pos.ps1` (chứng minh **cặp hàm** trên probe thật) | **exit 0** — **34** lời gọi HTTP; sau đó cờ `ADMIN\|true` |
| `pwsh -File .\tools\kiem-db.ps1` (**DoD**) | **25/25 DAT · 0 HONG** (đọc `NND thuc te = 93`) |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` (**DoD**) | **exit 0** — **19/19** bước · **452,5 s** (⑮ oversell · ⑯ outbox · ⑰ outbox 2 instance · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8**) |
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **196,3 s** |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **34/34 DAT · 0 HONG** (trong cổng, đủ nhóm DB: **40/40**) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemDb` | **6/6 DAT** (G…K + **L2**) |
| `dotnet build eCommerce.slnx` | **0 error / 0 warning** (trong cổng 52,3 s, `--no-incremental`) |
| `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (không đổi — WP-13 **không** thêm test .NET) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` · `pnpm test` | ✓ 0 lỗi · **25/25 pass** (bước [6] của cổng) |
| `pwsh -File .\tools\e2e.ps1` *(trong bước ⑲)* | **`8 passed`** (36,1 s) · **exit 0** |
| `pwsh -File .\tools\kiem-openapi-drift.ps1` | **PASS** — **51/51** path, `schema.d.ts` **KHÔNG ĐỔI** |
| Probe trong `-DayDu` | địa chỉ **17/17** · khách **26/26** · thanh toán **30/30** · đơn sàn **49/49** · oversell **11/0** · outbox **9/0** · outbox 2 instance **9/0** · giữ chỗ 2 instance **17/0** (P16: cờ **bật lại**) · Playwright **8/8** |
| Mermaid (bước [5] của cổng) | **213** khối · **0** lỗi |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| NND (DB dev) | **93** (không đổi — WP-13 không đổi schema) |

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — xem số cuối ở §7 (chạy **sau** khi viết tệp này).
Mọi tài liệu sửa trong phiên: `handover-n14` (mới) · `decisions` (**D-56** + dòng nhật ký) ·
`07 §WP-13`/`§13` · `08 §3`/`§8` · `README` · `tools/README-probe.md` (**mục F2**).

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **cố ý cắt**, mỗi mục có **nhận chủ**:

1. ⏭ **`WP-13` ② job CI `docker-build` — KHÔNG làm, giữ P1.** 🔑 **Lý do đo được:** máy dev **không có**
   `gh` (`gh --version` ⇒ *"not recognized"*), nhánh `feature/wp-03-catalog` **chưa push** (`git log` toàn
   commit local, `git remote -v` chỉ có `origin`), và **chưa từng** thấy job này xanh trên `ubuntu-latest`.
   ⇒ Thêm một job **chưa đo** vào required check là **đúng thứ** `WP-11`/`WP-12` cảnh báo.
   **Nhận chủ:** phiên P1 kế tiếp, **chỉ sau khi** đo được **một lần thật** trên `ubuntu-latest`.
2. ⚠️ **10 probe còn tự `UPDATE` cờ** (nhưng **có** bật lại): `probe-oversell` · `probe-giu-cho-2instance` ·
   `probe-outbox` · `probe-outbox-2instance` · `probe-khach-hang` · `probe-don-san` · `probe-thanh-toan` ·
   `probe-so-dia-chi` · `probe-s19-vnpay-that` · `probe-dlq-replay`. **Không** chuyển sang helper trong phiên
   này (bán kính + **5** trong số đó nằm trong `-DayDu` ⇒ đổi chúng là rủi ro cho cổng **đang xanh**).
   ✅ Chúng **bật lại** nên **không** phải lỗ; 📌 chuyển dần khi có phiên chạm đúng tệp đó (luật "một chỗ").
3. ⚠️ **Image `ecommerce-admin-web:local` + container đã `stop`** vẫn còn (`docker ps -a`) — như `N11` §5 #8.
4. ⚠️ **Docker Desktop trên máy này đã tự tắt một lần** (`N11` §5 #7) — nếu `docker compose ps` thiếu
   postgres/redis: bật Docker Desktop rồi `docker compose up -d postgres redis`.
5. 📌 **`probe-pos` để lại 1 khách** (`CusCustomers` **5 → 6** dòng) — **không phải rác**: khách này **có
   chứng từ** (hoá đơn/phiếu trả) nên bất biến **①c** (`D-53`) vẫn xanh; `kiem-db.ps1` **25/25** sau đó.
6. 📌 **`probe-kho-wp04` không dọn** kho/phiếu nó tạo (từ `N4` — ngoài phạm vi `WP-13`): dấu vết `WP04-*`
   **không** nằm trong nhóm "phải rỗng"/tiền tố `K9`/`K97` của `kiem-db.ps1` ⇒ cổng vẫn 25/25.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-13` chỉ sửa **script đo + cổng + docs**; **không** bảng/cột/enum. ✅ Bằng chứng: integration **96/96** (gồm `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT`), `schema.sql` **không đổi** |
| Cách lùi | Không cần (không có thay đổi schema). Các tệp đã sửa là **script đo/cổng**, không ghi dữ liệu thường trực — 💡 lùi = `git checkout` 5 script + 2 tệp docs (`decisions`, `07/08/README`) nếu muốn bỏ `D-56` |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-56** | **CỔNG AUTH CỦA PROBE (`WP-13` ①): MỘT CHỖ mở/bật cờ `ShouldChangePasswordOnNextLogin`** — ① bệnh đo được: `DbMigrator` bật cờ ⇒ `invalid_grant`/`ID2024` (**không** phải sai mật khẩu), `probe-kho-wp04` chết ở dòng token ② **một chỗ**: `Dat-CoBuocDoiMatKhau` (UPDATE + **xác nhận** `count=1`, ghi SQL ra **tệp tạm** để không mất `exit code`) + `Invoke-KhongCoDoiMatKhau` + cặp `Start-`/`BatLai-CoDoiMatKhau`; cả **4** tên vào `Lay-MauHamNenTang` ③ **cặp hàm là bắt buộc** vì 4 probe đi **cả** token Host **và** BFF/NextAuth (**BFF cũng password grant**) ④ 📌 số trong `07` ghi **3**, đo được **4** — ghi số đo, không bỏ sót tệp thứ 4 ⑤ `finally` của PowerShell **chạy cả khi** khối lệnh `exit` (**exit code không đổi**) ⑥ **2** ca kiểm mới **Y6**/**L2** ⑦ ⛔ không tắt nhóm ② của `kiem-db.ps1`, không cho `probe-kho-wp04` đi BFF ⑧ đổi ⑫ sang **so SỐ** (`D-55` ①) ⑨ **`WP-13` ② giữ P1** |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Job CI `docker-build` vẫn chưa có** — nhận chủ **phiên P1 kế tiếp**; chỉ thêm **sau** khi thấy nó xanh
   **một lần** trên `ubuntu-latest`. ⛔ Đừng thêm "cho đủ bộ" rồi bật required check.
2. 🚨 **Hai probe chạm cờ **song song** = đua**: `Invoke-KhongCoDoiMatKhau` tắt → probe B tắt/`bật lại` → probe A
   vẫn chạy với cờ **đã bật** ⇒ A chết ở `invalid_grant` **không rõ nguyên nhân**. 📌 Probe trong repo chạy
   **tuần tự** (cổng tuần tự + người chạy từng lệnh) nên **hôm nay không xảy ra**; muốn chạy song song thì phải
   có **khoá** (chưa làm — ghi ở đây để không ai tưởng đã an toàn).
3. ⚠️ **Cổng **Y6** chỉ bắt tệp dùng `Start-KhongCoDoiMatKhau`**; **10** probe còn `UPDATE` viết tay (§2 #2)
   **không** bị nó soi. ⇒ 🔑 Muốn luật "một chỗ" có răng thì phải chuyển nốt 10 tệp đó **hoặc** mở rộng Y6
   sang mẫu `update "AbpUsers" set "ShouldChangePasswordOnNextLogin"` — **đừng** tưởng Y6 đã phủ hết.
4. ⚠️ **`probe-kho-wp04` không dọn dữ liệu** (`WP04-*`: kho + 2 phiếu/lượt). Chạy nhiều lượt ⇒ bảng kho phình
   (`⑪` in DS có **5** dòng/lượt). 📌 Không chặn cổng, nhưng nếu ai thêm `WP04` vào nhóm "dấu vết thử nghiệm"
   của `kiem-db.ps1` thì cổng sẽ **đỏ ngay** ⇒ phải dọn **trước**.
5. ⚠️ **`-DayDu` nay ~452,5 s** (bước ⑨ test cổng ~103 s, E2E ~38 s). Vẫn dưới ngưỡng; nếu CI chậm, **đừng**
   bỏ bước — dùng `-BoQuaIntegration`/`-ChiKiemMoc` cho vòng lặp nhanh.
6. ⚠️ **`.next/standalone` cảnh báo `next start`** (từ `N12`) — vẫn chỉ là cảnh báo; 8/8 E2E xanh.
7. 🔒 **Quy ước cờ bảo mật (từ nay):** probe **KHÔNG** tự `UPDATE` cờ; dùng 2 hàm của `nen-tang.ps1`; sau
   **mọi** lượt chạy `SELECT` xác nhận `ADMIN|true` (mục **D2** của [README-probe](../../tools/README-probe.md)).

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-13 → mở `WP-13` ② (P1 còn lại: job CI `docker-build`) — phiên `N15`.**

> 📌 `WP-13` **đã đạt DoD** (`probe-kho-wp04` **exit 0 / 12 ca** · `kiem-db` **25/25**, cờ **bật lại** ·
> bản nhẹ **9/9** · `-DayDu` **19/19** · **0 migration**). 🔑 Mục **②** của `WP-13` là *"(tuỳ)"* và **vẫn mở**
> — nó **không** nằm trong DoD, nhưng **đã có nhận chủ** nên phiên này **không** phải loại **A**.
> ⚠️ `07` **chưa có `§WP-14`** ⇒ phiên sau **chỉ** làm nốt **②** (hoặc người lập kế hoạch viết `§WP-14`
> **trước** — `08 §5.1` luật 5). ⛔ **Không** tự đặt WP khác.

1. Đọc `07 §WP-13` ② + §2 #1 của tệp này: **chỉ** thêm job `docker-build` **sau khi** đo được nó **xanh một
   lần thật** trên `ubuntu-latest` (cần **push nhánh** hoặc runner `act`/VPS Linux — máy dev **không có** `gh`).
   Không đo được ⇒ **giữ P1**, ghi lý do, **không** đổi required check.
2. Trước khi sửa bất kỳ script nào trong `tools/`: `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc`
   (**34/34**) — 📌 nếu thêm/xoá ca kiểm thì cập nhật số này ở `07 §13` + handover.
3. ⚠️ Sau khi sửa script cổng/probe: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) và trước khi push:
   `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` (**19/19**).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
pwsh -File .\tools\kiem-tat-ca.ps1               # 9/9, exit 0
pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu        # 19/19, exit 0
pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc   # 34/34
pwsh -File .\tools\kiem-db.ps1                   # 25/25
pwsh -File .\tools\probe-kho-wp04.ps1            # 12 ca, exit 0
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n14.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-13 ②** (phiên N15). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N14`) |
| Commit | **chưa commit** phiên này — working tree **59 ` M`** + **124 `??`** (baseline: **58 ` M`** + **123 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A`. 📌 **` M` thêm so baseline = 1** (`tools/README-probe.md` — trước phiên **sạch**); 3 probe `probe-pos`/`probe-bao-cao-wp08`/`probe-khach-hang-wp07` vốn đã `??` (chưa từng commit) nên sửa chúng **không** đổi con số; **`??` thêm = 1** (`docs/nhanh/handover-n14.md`) |
| Push / PR | Chưa (**chưa push** kể từ `N7` — xem §2 #1) |
| Tệp `N14` đã tạo | `docs/nhanh/handover-n14.md` |
| Tệp `N14` đã sửa | `tools/nen-tang.ps1` (4 hàm cờ bảo mật) · `tools/test-cong-kiem.ps1` (dải tên hàm + **Y6** + **L2**) · `tools/probe-kho-wp04.ps1` (token qua wrapper + `So` ở ⑫) · `tools/probe-pos.ps1` · `tools/probe-don-hang.ps1` · `tools/probe-bao-cao-wp08.ps1` · `tools/probe-khach-hang-wp07.ps1` (đầu + `finally`) · `tools/README-probe.md` (**F2** — luật cờ bảo mật) · docs (`decisions` **D-56** + nhật ký, `07 §WP-13`/`§13`, `08 §3`/`§8`, `README`) |
| Dữ liệu `N14` đã xoá | **Không** — `-DayDu` kết thúc với DB **25/25**; dấu vết `WP04-*`/`WP06-*` **không** thuộc nhóm "phải rỗng" (§2 #5/#6) |
| Cổng tài liệu (`kiem-link-tai-lieu.ps1`) | **PASS** — **116** tệp `.md`, **622** link markdown, **393** đường dẫn backtick, **0** chết (baseline: 115/610/383) |
