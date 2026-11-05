# Handover `N11` — WP-10: **Ops** (Admin Web production trong compose + `.env.example` + `/api/health`)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-10](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-51](decisions.md), [D-52](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-10** (`docker-compose.yml` thêm service `admin-web` production ·
> `.env.example` có `API_BASE_URL`/`AUTH_SECRET`/`AUTH_ISSUER`/`NEXT_PUBLIC_*` · health `/api/health` ở BFF →
> `/health-status` của Host), **không mở rộng phạm vi**.
> 📌 `08 §3` gộp `WP-09 + WP-10` vào `N10`; `N10` chỉ làm `WP-09` ⇒ phiên này làm phần **còn thiếu**, đúng
> *"việc đầu tiên của phiên sau"* mà `handover-n10` §6 đã ghi.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **157 mục** (**42 ` M`** của `N7`/`N8`/`N9`/`N10` chưa commit + **115 `??`** rác khảo sát/probe) |
| 2 | `git log --oneline -1` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (26 giờ) |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (incremental, 28 s) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (7 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **112** tệp `.md`, **561** link markdown, **365** đường dẫn backtick, **0 chết** |

📌 Baseline **khớp** `handover-n10` (1961 test · migration 59 · 23 route · 8/8 E2E). Không có sai lệch phải dừng.

### 1.2 Đã thi công (đúng 3 việc của `07 §WP-10`)

| # | Việc (`07 §WP-10`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **compose thêm service `admin-web`** (production build) | `apps/admin-web/Dockerfile` (2 tầng `node:22-alpine`: `corepack prepare pnpm@10.22.0` → `pnpm install --frozen-lockfile` → `next build`; runner chỉ copy `.next/standalone` + `.next/static`, chạy **user `node`**, `HEALTHCHECK` gọi `/api/health`) · **`.dockerignore` ở GỐC** (context = gốc repo vì workspace pnpm) · service `admin-web` trong `docker-compose.yml` (cổng **3000**, `extra_hosts: host.docker.internal:host-gateway`, **profile `ops`**, `build.args` cho `NEXT_PUBLIC_APP_NAME`) · `next.config.ts`: **`output: "standalone"`** | `docker compose up -d admin-web` ⇒ container **running** trên **cổng mặc định 3000**; image **354 MB**; `/login` **200** (chuỗi `eCommerce Admin` có trong HTML); build **~2 phút** (nguội, có cache tầng `pnpm install`) |
| 2 | **`.env.example`** (`API_BASE_URL`, `AUTH_SECRET`, `AUTH_ISSUER`, `NEXT_PUBLIC_*`) | `apps/admin-web/.env.example` viết lại thành **danh sách đầy đủ** 7 nhóm: `API_BASE_URL` · `OAUTH_*` · **`AUTH_ISSUER`** (mới) · `AUTH_SECRET`/`AUTH_TRUST_HOST` · **`NEXT_PUBLIC_APP_NAME`** (mới, **biến công khai duy nhất**) · `DEFAULT_BUSINESS` · **`HEALTH_TIMEOUT_MS`** (mới) — kèm ghi chú **hai chế độ local/container** của `API_BASE_URL`. Code: `lib/config.ts` thêm `AUTH_ISSUER` (trống ⇒ fallback `API_BASE_URL`), `TOKEN_ENDPOINT`, `HEALTH_TIMEOUT_MS`; `lib/public-config.ts` (`APP_NAME` dùng ở **sidebar**, **tiêu đề trang**, **màn đăng nhập** — giá trị mặc định ⇒ **giao diện không đổi một ký tự**) | `tsc --noEmit` **✓**; `pnpm build` **✓ 24 route**; `AUTH_ISSUER` có tác dụng thật (token endpoint lấy từ nó — `lib/auth.ts` gọi `TOKEN_ENDPOINT`) |
| 3 | **Health `/api/health`** (BFF → `/health-status`) | `apps/admin-web/app/api/health/route.ts`: `200 {"status":"Healthy","checks":{"host":"Healthy"}}` · `503 {"status":"Unhealthy","checks":{"host":"Unhealthy"\|"Unreachable"}}` · `cache: no-store` + **trần `HEALTH_TIMEOUT_MS`** · **không** lộ tên bảng/chuỗi kết nối/stack (`XC-005` BR-012) · compose `healthcheck` gọi **chính** endpoint này | `tools/kiem-ops-wp10.ps1` → **exit 0**, **9 lời gọi HTTP**, **0 lỗi** (chi tiết §1.4) |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-ops-wp10.ps1` | **exit 0** — **9 lời gọi HTTP**, **0 lỗi**; 200/503 đúng cả trong Docker **và** bản `next start` (chạy **4 lượt**: có/không `-Build`, sau khi thêm profile `ops`, và sau khi bỏ `restart`) |
| `dotnet build eCommerce.slnx` | **0 error / 0 warning** (25,8 s) |
| `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (không đổi — WP-10 **không** thêm test BE) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** (0 lỗi) |
| `pnpm test` | **25/25 pass** (4 tệp) |
| `pnpm build` | **✓ — 24 route** (`23` của `N10` **+ `/api/health`**) |
| `pwsh -File .\tools\e2e.ps1` | **`8 passed`** (40,4 s) · **`exit 0`** — `output: standalone` + `APP_NAME` + đổi `lib/config.ts` **không** phá luồng nào |
| `pwsh -File .\tools\kiem-openapi-drift.ps1` | **PASS** — **51/51** path, `schema.d.ts` **không đổi** (`/api/health` **không** thuộc `/api/v1`) |
| `pwsh -File .\tools\kiem-tat-ca.ps1` | **DỪNG ở bước [8] (DB)** — **24/25**, **1 HỒNG có sẵn** `CusCustomers` **6 dòng** probe `WP06`/`WP07`; bước [1]…[7] **xanh** (đúng như `N10`). ⚠️ Bước **[9] "Test cho chính các cổng" KHÔNG tới được** ⇒ chạy trực tiếp: `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` = **32/33**, **1 HỒNG có sẵn** (xem §5 #1) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **113** tệp `.md`, **581** link markdown, **371** đường dẫn backtick, **0 chết** |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| NND (DB dev) | **93** (không đổi — WP-10 không đổi schema) |

### 1.4 Bằng chứng probe — `tools/kiem-ops-wp10.ps1` (`exit 0`)

| # | Phép đo | Kết quả (lượt chạy cuối) |
|---|---|---|
| ① | `docker compose config --quiet` | **OK** (tệp compose hợp lệ) |
| ② | `docker compose up -d admin-web` (**cổng MẶC ĐỊNH 3000**) | container **running**; `/api/health` **trả lời** (503 — Host đang tắt) |
| ③ | Host **CHẾT** ⇒ `GET /api/health` | **`503`** · `{"status":"Unhealthy","checks":{"host":"Unreachable"}}` |
| ④ | `docker inspect .State.Health.Status` khi Host chết | **`unhealthy`** sau **91 s** (interval 15 s × retries 3 + `start-period`) |
| ⑤ | Host **SỐNG** ⇒ `GET /api/health` | **`200`** · `{"status":"Healthy","checks":{"host":"Healthy"}}` |
| ⑥ | `docker inspect .State.Health.Status` khi Host sống | **`healthy`** sau **10 s** |
| ⑦ | `docker compose stop admin-web` | cổng 3000 **nhả** (cần cho `tools/e2e.ps1`) |
| ⑧ | `next start` (**KHÔNG** Docker), Host sống / chết | **`200`** / **`503`** — hành vi health **không** phụ thuộc Docker |
| ⑨ | `GET /login` khi Host **chết** | **`200`** — BFF sống độc lập với API (người dùng **không** thấy trang lỗi) |

> 📌 **Đo thêm (ngoài probe, có ảnh hưởng thiết kế):** khi container chạy, cổng **3000** do **`com.docker.backend`**
> giữ (PID 48380 — proxy publish port của Docker Desktop), **không** phải Next ⇒ `docker compose up -d` (lệnh
> baseline của `08 §4`) **không** được tự bật service này (xem `D-51` ⑧).
> 🔁 **Sự cố môi trường giữa phiên:** Docker Desktop **tắt** một lần (postgres/redis `Exited (255)`) ⇒ probe
> lượt đầu **đỏ ở `docker compose up`** với thông điệp *"cannot find the file specified … dockerDesktopLinuxEngine"*.
> Đã bật lại Docker Desktop + `docker compose up -d postgres redis` rồi chạy lại ⇒ xanh. **Không phải lỗi mã.**

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **113** tệp `.md`, **581** link markdown,
**371** đường dẫn backtick, **0 chết** (baseline: 112/561/365).
Mọi tài liệu mới/sửa (`handover-n11`, `decisions` D-51/D-52, `07 §WP-10` + `§WP-11` + `§13`, `08 §3`/§8,
`README`) đã được cổng này quét.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **thuộc phạm vi ops** nhưng **cố ý cắt** (mỗi mục có **nhận chủ**):

1. **Chưa thêm job CI `docker-build`** (`D-51` ⑦): chưa kiểm được trên `ubuntu-latest` (build image cần
   `corepack` + `pnpm --frozen-lockfile` + BuildKit). Nhận chủ: **`WP-11` ③** — làm cùng lượt sửa cổng, hoặc
   lần push đầu nếu muốn thấy log trước.
2. **Host .NET KHÔNG nằm trong compose** (`D-51` ①): cần image `dotnet/sdk:10.0` + publish **toàn** solution
   (261 MB nguồn + 55 MB modules) + ai sở hữu migration. 📌 Đó là **một WP riêng**, không phải "thiếu sót" —
   và chính nó làm phép đo `503` trở nên **không quan sát được** nếu làm ẩu.
3. **Chưa đo luồng ĐĂNG NHẬP THẬT trong container** (chỉ đo `/login` 200 + `/api/health` 200): health chứng minh
   `host.docker.internal` **thông** tới Host (cùng đường `fetch` mà `lib/auth.ts` dùng cho `/connect/token`),
   nhưng **chưa** bấm đăng nhập trong trình duyệt trỏ vào cổng 3000 của container. Nhận chủ: **`WP-11`** (thêm
   1 ca E2E "chạy trên container" là **rẻ**, vì E2E đã có `baseURL` từ `E2E_WEB`).
4. **`public/` không tồn tại** ⇒ `Dockerfile` **không** copy nó. Thêm `apps/admin-web/public/` sau này **phải**
   thêm một dòng `COPY` (nếu không: ảnh/font 404 **chỉ trong container**, dev vẫn thấy). Đã ghi ở `Dockerfile`.
5. **Chưa có unit test cho `/api/health`**: cố ý — `D-50` chốt `pnpm test` = **hàm thuần** (node, không
   jsdom/RTL); route này chỉ có một nhánh `res.ok`, còn hành vi thật do **probe HTTP** đo (và đã đo **hai chiều**).
6. **`.env` cấp gốc cho compose chưa tạo**: compose dùng `${VAR:-default}` ⇒ override bằng biến môi trường hoặc
   `.env` cạnh `docker-compose.yml` (Docker tự đọc). 📌 Cố ý **không** tạo `.env.example` thứ hai: hai tệp mẫu
   là hai nguồn sự thật, sẽ lệch. Mẫu duy nhất: `apps/admin-web/.env.example`.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-10` là **ops**: Dockerfile, compose, route BFF, biến môi trường — **không** bảng/cột/enum. ✅ Bằng chứng: integration **96/96** (gồm `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT`), `schema.sql` **không đổi**, NND **93** |
| Cách lùi | Không cần (không có thay đổi schema) |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-51** | **OPS**: compose chỉ thêm `admin-web`, **không** thêm service `host` — `depends_on` sẽ **che mất** phép đo `503` (Host chết thì container không được phép khởi động) ⇒ thay bằng `healthcheck` gọi `/api/health`; `host.docker.internal` + `extra_hosts` + Host bind `0.0.0.0`; **hai** giá trị mặc định cho `API_BASE_URL` (local vs container); build context = **gốc repo** + `.dockerignore` ở gốc (không có: context ≈ **1 GB** và `node_modules` Windows làm `pnpm install` bỏ qua ⇒ thiếu binary Linux); `output: "standalone"` (image **354 MB**, `next start` vẫn chạy); **profile `ops`** (cổng 3000 do `com.docker.backend` giữ ⇒ `docker compose up -d` baseline **không** được bật nó); probe phải `stop` container trước khi dọn cổng |
| **D-52** | **`/api/health`**: sức khoẻ của **HOST** (không phải của Next) — `200`/`503` + **3 giá trị** `checks.host` (`Healthy`/`Unhealthy`/`Unreachable`); `no-store` + trần **3 s** (*treo* ≠ *chết*); **không lộ chi tiết** (`XC-005` BR-012, endpoint `AllowAnonymous`); `NEXT_PUBLIC_*` nhúng **lúc build** ⇒ compose truyền `build.args`; chỉ **một** biến công khai (`NEXT_PUBLIC_APP_NAME`) — `D-12` nguyên vẹn; `AUTH_ISSUER` tách khỏi `API_BASE_URL` với fallback; `TOKEN_ENDPOINT` một chỗ; BFF phải sống độc lập với API (`/login` **200** khi Host chết) |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **CỔNG CÒN 2 HỒNG CÓ SẴN — cả hai đều KHÔNG do `N11`** (đã đo, có danh sách):
   **(a) bước [8] DB:** `CusCustomers` **6 dòng** probe `WP06-KHACH-*`/`WP07-KHACH-*` do `N7`/`N8` để lại
   ⇒ cổng **dừng** ở đây (**24/25**), nên **5 bước HTTP sau không chạy**;
   **(b) bước [9] `test-cong-kiem.ps1`:** **32/33** — ca **Y1** ("không còn API chi-Windows") báo **9 script cũ**
   còn đường dẫn Windows viết cứng thay vì `Lay-DuongDanBin`: `e2e.ps1` · `kiem-openapi-drift.ps1` ·
   `kiem-pos-browser.ps1` · `probe-bao-cao-wp08.ps1` · `probe-don-hang.ps1` · `probe-khach-hang-wp07.ps1` ·
   `probe-kho-fe.ps1` · `probe-kho-wp04.ps1` · `probe-pos.ps1`. 📌 `tools/kiem-ops-wp10.ps1` (mới) **KHÔNG**
   nằm trong danh sách ⇒ phiên này **không** làm xấu thêm; nhưng trên `ubuntu-latest`, 9 script đó sẽ đỏ **vì
   môi trường** ⇒ **`cong-15-buoc` (required check) sẽ đỏ** ⇒ người ta sẽ học cách bỏ qua. Nhận chủ: **`WP-11`**
   (xem `07 §WP-11`). ⛔ **Đừng tắt cổng.**
2. 🚨 **`docker compose up -d admin-web` đang chạy ⇒ `tools/e2e.ps1` sẽ GIẾT DOCKER DESKTOP** (đo được: cổng
   3000 do `com.docker.backend` giữ). `D-51` ⑧ xử lý bằng **profile `ops`** (baseline không bật service), và
   `tools/kiem-ops-wp10.ps1` **dừng container trước** khi dọn cổng. ⚠️ **`tools/e2e.ps1` thủ công vẫn chưa có**
   guard đó: nếu bạn **cố ý** bật container rồi chạy `e2e.ps1`, hãy `docker compose stop admin-web` trước.
3. ⚠️ **Image chỉ build lại khi gọi `--build`**: `docker compose up -d admin-web` **không** tự build lại khi mã
   đổi (image `ecommerce-admin-web:local` đã tồn tại) ⇒ *"sửa mã mà container vẫn bản cũ"* — đúng lớp lỗi
   `N5`/`WP-09` đã trả giá với `next start` cũ. Luôn `docker compose up -d --build admin-web` sau khi sửa FE.
4. ⚠️ **Thêm dependency vào `apps/admin-web/package.json` ⇒ phải commit `pnpm-lock.yaml`**: image dùng
   `--frozen-lockfile` (cố ý) ⇒ lockfile lệch là **build đỏ**, và thông điệp nằm giữa log Docker.
5. ⚠️ **`NEXT_PUBLIC_APP_NAME` là biến BUILD-TIME**: đổi tên hiển thị = **build lại image**; đặt ở
   `environment:` của compose là **no-op im lặng** (`D-52` ⑤).
6. ⚠️ **Cấu hình `API_BASE_URL` trong container sai ⇒ `/api/health` trả `503` vĩnh viễn** và trông y hệt
   *"Host chết"*. 📌 `checks.host = Unreachable` **không** phân biệt *"Host chết"* với *"phân giải
   `host.docker.internal` hỏng"* — muốn phân biệt: `docker exec ecommerce-admin-web node -e "fetch(...)"`.
7. 📌 **Docker Desktop trên máy này đã tự tắt một lần giữa phiên** ⇒ nếu `docker compose ps` báo thiếu
   postgres/redis: bật Docker Desktop rồi `docker compose up -d postgres redis` (baseline `08 §4` vẫn đúng).
8. 📌 **Dữ liệu probe để lại**: image `ecommerce-admin-web:local`, container `ecommerce-admin-web` (**đã `stop`**,
   còn trong `docker ps -a`). Không tạo dòng DB nào ở phiên này.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-10 → mở WP-11 (`07 §WP-11`, "Làm cổng xanh").**

> 📌 `WP-10` là **WP cuối** của GĐ0–GĐ2 trong `07 §4`. Cái chặn *"coi như xong"* **không phải** tính năng mà là
> **cơ chế kiểm chứng**: `kiem-tat-ca.ps1` **chưa exit 0** (2 HỒNG có sẵn ở §5 #1) ⇒ `cong-15-buoc` — required
> check của CI — sẽ **đỏ ngay lần push đầu**. Vì thế `07` đã thêm **WP-11 (0,5–1 ngày)** với DoD *"cổng exit 0"*.

1. Đọc `07 §WP-11`: ① **9 script cũ** hết đường dẫn Windows viết cứng (`Lay-DuongDanBin`) ② **quyết dứt điểm**
   6 dòng `CusCustomers` của cổng DB (xoá kèm dữ liệu phụ thuộc **hoặc** ngoại lệ **có ghi lý do** — ⛔ **không**
   tắt cổng) ③ *(tuỳ)* job CI `docker-build`.
2. ⚠️ Sửa ① thì **phải** chạy `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` (**33/33**) — đó là lưới an
   toàn của chính các script cổng; và giữ `tools/kiem-ops-wp10.ps1` **exit 0**.
3. ⚠️ Sau khi sửa **bất kỳ** đường nào của đăng nhập/BFF/POS/kho/đơn: `pwsh -File .\tools\e2e.ps1`
   (và nhớ **`docker compose stop admin-web`** nếu container đang chạy — §5 #2).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
pnpm --filter @ecommerce/admin-web typecheck
pnpm test
pnpm build
pwsh -File .\tools\e2e.ps1                       # 8 luồng, tự bật Host + Next từ build hiện tại
pwsh -File .\tools\kiem-ops-wp10.ps1             # ops: compose + /api/health 200/503
pwsh -File .\tools\kiem-openapi-drift.ps1        # 51/51 path, 0 drift
pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc   # 33/33 sau khi sửa WP-11 ①
pwsh -File .\tools\kiem-tat-ca.ps1               # DoD WP-11: phải exit 0
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-11
và handover gần nhất `docs/nhanh/handover-n11.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-11** (phiên N12). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N11`) |
| Commit | **chưa commit** phiên này — mã `N7`+`N8`+`N9`+`N10`+`N11` nằm trong working tree (**50 ` M`** + **121 `??`**, đo cuối phiên; baseline là **42 ` M`** + **115 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A` |
| Push / PR | Chưa |
| Tệp `N11` đã tạo | `.dockerignore` (gốc) · `apps/admin-web/Dockerfile` · `apps/admin-web/app/api/health/route.ts` · `apps/admin-web/lib/public-config.ts` · `tools/kiem-ops-wp10.ps1` · `docs/nhanh/handover-n11.md` |
| Tệp `N11` đã sửa | `docker-compose.yml` (service `admin-web`, profile `ops`) · `apps/admin-web/.env.example` (viết lại: + `AUTH_ISSUER`, `NEXT_PUBLIC_APP_NAME`, `HEALTH_TIMEOUT_MS`) · `apps/admin-web/next.config.ts` (`output: "standalone"`) · `apps/admin-web/lib/config.ts` (`AUTH_ISSUER`/`TOKEN_ENDPOINT`/`HEALTH_TIMEOUT_MS`) · `apps/admin-web/lib/auth.ts` (dùng `TOKEN_ENDPOINT`) · `apps/admin-web/app/layout.tsx` + `app/login/page.tsx` + `components/shell/Sidebar.tsx` (`APP_NAME`) · docs (`decisions` D-51/D-52, `07 §WP-10`/`§WP-11`/`§13`, `08 §3`/§8, `README`) |
