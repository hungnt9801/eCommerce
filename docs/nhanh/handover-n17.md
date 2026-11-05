# Handover `N17` — đóng nợ CI `packageManager`; **KHÔNG merge** PR #1 (đo được: merge làm `ci` của `main` đỏ ở Y1)

> Ngày 05/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`6c49d3a`**)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-60](decisions.md) · [D-61](decisions.md)
> **Mục tiêu phiên:** `07` **chưa có `§WP-14`** ⇒ phiên này **không** mở WP mới. Việc còn lại: **merge PR #1**
> (đọc `handover-n16` §5 #3 trước) + **nợ `cong-15-buoc`** (`handover-n16` §5 #1) **nếu được duyệt**. Chủ dự án
> đã duyệt hướng **(a)** (`D-60`) và chọn *"sửa trên PR #1, merge khi `ci` xanh"* — **nhưng đo xong vẫn KHÔNG
> merge** (lý do đo được ở §1.4; chủ dự án chốt lại sau khi xem số, `D-61`). **Không mở rộng phạm vi.**

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline 6 lệnh §4 của [08](08-session-protocol.md) — đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **151** mục (**45 ` M`** + **106 `??`**) — khớp `handover-n16` §7 |
| 2 | `git log --oneline -1` · `git branch --show-current` | `6c49d3a` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` · `docker compose ps` | 🔴 **lần đầu ĐỎ**: daemon Docker **không chạy** — `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified`. Bật `%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe` ⇒ `ServerVersion` **29.6.2** ⇒ `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (24 s) |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (26,83 s) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (8 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **118** tệp `.md`, **647** link markdown, **414** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n16` §7 (1961 test · 118/647/414 · 45 ` M` + 106 `??`) — chỉ khác **#3**: Docker
daemon **tắt sẵn** (N16 để 2 container chạy 3 giờ). Không phải lỗi compose — xem §5 #3.

### 1.2 Nợ `cong-15-buoc` — đo **TRƯỚC** khi sửa

| Nơi đo | Kết quả |
|---|---|
| CI thật (run `41811821821` của `N16`) | job `cong-15-buoc` **đỏ sau 38 s**; log: `error This project's package.json defines "packageManager": "yarn@pnpm@10.22.0". However the current global version of Yarn is 1.22.22.` ⇒ **bước 8…13 chưa từng chạy** |
| Máy dev: `yarn --version` trong `src/eCommerce.Web` | **exit 1** — *"This project is configured to use pnpm because `D:\Projects\Tools\eCommerce\package.json` has a `packageManager` field"* (Yarn **1.22.22**). 📌 `src/eCommerce.Web/package.json` **không** có field ⇒ Yarn đọc **ngược lên** `package.json` gốc |

### 1.3 Đã sửa — commit **`18405ca`** (đúng **2** tệp)

| Tệp | Thay đổi | Vì sao |
|---|---|---|
| `package.json` (gốc repo) | bỏ **1 dòng** `"packageManager": "pnpm@10.22.0"` | Yarn 1.22 của `abp install-libs` từ chối field **không** phải `yarn@…` |
| `apps/admin-web/Dockerfile` | sửa **comment** dòng 19–21: pnpm ghim bằng `corepack prepare pnpm@10.22.0`, **không** dựa vào field đã bỏ | comment cũ nói *"corepack đọc packageManager"* — nay **sai sự thật** |

**Đo lại trên máy dev:**

| Lệnh | Kết quả |
|---|---|
| `yarn --version` (trong `src/eCommerce.Web`) | **1.22.22** · **exit 0** |
| `abp install-libs -wd src/eCommerce.Web` | **exit 0** · **10,2 s** — `Running Yarn…` → `success Already up-to-date` |
| `pnpm install --frozen-lockfile` | **exit 0** — `Done in 588ms using pnpm v10.22.0` |

📌 Push lần **1** là được (N16 phải thử **4** lần vì GitHub 5xx).

### 1.4 Đo trên **CI thật** — head `18405ca` (đây là chỗ quyết định `D-61`)

| Workflow | Run | Kết quả |
|---|---|---|
| **`docker-build`** (required check) | [`41955331294`](https://github.com/hungnt9801/eCommerce/actions/runs/41955331294) | ✅ **SUCCESS** — **6/6** bước, **66 s** |
| `ci` | [`41955331290`](https://github.com/hungnt9801/eCommerce/actions/runs/41955331290) | 🔴 **FAILURE** — **294 s**: bước **7 SUCCESS** (nợ `packageManager` **ĐÓNG**) · bước **⑩ ĐỎ ở ⑦/Y1** |

Bằng chứng của chỗ đỏ (artifact `bang-chung-…`, `tools/_tam-kiem/test-cong.log`: **37/38 DAT · 1 HONG**):

```text
🚨 Script cua cong con phu thuoc Windows (XC-007 P-01…P-06):
  probe-don-hang.ps1: dung API chi-Windows
  probe-kho-fe.ps1: dung API chi-Windows
  probe-kho-wp04.ps1: dung API chi-Windows
```

🔑 **Ba tệp này là MỚI so với `main`** (`merge-base` = `531bd9d` = `main`): bản sửa của chúng nằm trong **cây
làm việc chưa commit** (`N12`–`N14`, `D-54`), nên **cây của PR** vẫn là bản cũ. ⇒ **Merge PR #1 bây giờ = đưa 3
tệp cũ đó vào `main` ⇒ `ci` đêm của `main` (đang **xanh**, run `41929871748`) sẽ **đỏ ở Y1**.** Cộng với lý do
**thứ hai độc lập** (`handover-n16` §5 #3: nửa FE, BE chưa commit) ⇒ **hai lý do để KHÔNG merge**.

⇒ **`D-61`: KHÔNG merge PR #1.** PR vẫn `open` · `mergeable=true` · **40** commit · **488** tệp · head `18405ca`;
`mergeable_state=unstable` (vì `ci` đỏ, mà `ci` **không** nằm trong required checks — `D-59`).

### 1.5 Đo LẠI cuối phiên (sau khi sửa)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **228,5 s** |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0** — **19/19** bước · **455,9 s** (⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 instance **9/0** · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8**) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **34/34 DAT · 0 HONG** (61,6 s) · full trong cổng: **40/40** |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HONG** (6,7 s) · `ADMIN\|true` |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **119** tệp `.md`, **660** link markdown, **425** đường dẫn backtick, **0** chết |
| Mermaid (`npm run check:mermaid` trong `tools/`) | **PASS** — **213** khối · **0** lỗi (chạy **sau** khi sửa docs) |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất · **0** test BE thêm |
| Rác máy dev | **0** worktree/ảnh Docker tạm. Log cổng ở **gốc repo** (`gate-n17-nhe.log` · `gate-n17-daydu.log` · `testcong-n17.log` · `kiemdb-n17.log`) — `*.log` **đã gitignore**, không vào commit |

📌 Các số `-DayDu` **khớp** `N16` (19/19; 455,9 s vs 485 s — chênh do máy, **không** do mã: phiên này không
chạm `.ps1`/mã BE).

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc còn lại là **cắt có quyết định**, mỗi mục có **nhận chủ**:

1. ⏭ **PR #1 CHƯA merge — quyết định CÓ ĐO (`D-61`):** merge sẽ (a) làm `ci` của `main` **đỏ ở Y1** (3 probe
   cũ vào `main`), và (b) đưa **nửa FE** vào `main` (BE chưa commit ⇒ FE gọi `/api/v1/pos|reports|customers`
   **404**). **Nhận chủ:** phiên **commit cây làm việc**.
2. ⏭ **`ci` trên PR #1 vẫn đỏ — nhưng KHÔNG còn vì `packageManager`:** bước **7 đã xanh** (đo trên CI thật);
   nay đỏ ở **⑦/Y1** vì **cây `tools/` `N12`–`N14` chưa commit** (`nen-tang.ps1` + `test-cong-kiem.ps1` + 3
   probe, và rất có thể cả `probe-oversell`/`probe-giu-cho-2instance` của `N13`). Muốn `ci` xanh phải commit
   **bộ `tools/` đó** — **ngoài phạm vi** "nợ `packageManager`" đã được duyệt. **Nhận chủ:** phiên commit cây.
3. ⏭ **Cây làm việc vẫn chưa commit** — **45 ` M`** + **107 `??`** (baseline **106**; **+1** là `docs/nhanh/handover-n17.md`). **Nhận chủ:** phiên commit BE.
4. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration mới | **KHÔNG có** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đang ở migration 59 từ `N9`) |
| Vì sao không có | Phiên này **chỉ** sửa `package.json` + comment `Dockerfile` + docs. **Không** bảng/cột/enum/index nào đổi |
| Cách lùi | `git revert 18405ca` (lùi cả 2 tệp). Nếu cần "như trước phiên": `git push --force-with-lease` chỉ khi chưa merge — **không** khuyến nghị |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-60** | **BỎ `packageManager` khỏi `package.json` GỐC** để `abp install-libs` (Yarn 1.22) chạy được; Yarn đọc field từ `package.json` **gần nhất đi ngược lên** và **từ chối** giá trị `pnpm@…`. pnpm vẫn ghim **2 chỗ** (`ci.yml` bước 6b + `Dockerfile` dòng 21) ⇒ image **không** phụ thuộc field. ⛔ Không bỏ bước 7 khỏi `ci.yml`, không ghim `yarn@` |
| **D-61** | **KHÔNG merge PR #1** — đo được merge làm `ci` của `main` **đỏ ở Y1** (3 probe `N12`–`N14` chưa commit là **mới so với `main`**) **và** đưa **nửa FE** vào `main` (API 404). Chờ **phiên commit cây làm việc** (BE + `tools/`) rồi merge khi **đo** thấy `ci` xanh. ⛔ Không "merge rồi sửa sau": required check **không** bắt hai lỗi đó |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Merge PR #1 lúc này = 2 hậu quả đo được:** (a) `ci` của `main` **đỏ ở Y1** (3 probe cũ vào `main` —
   `main` đang xanh, run `41929871748`); (b) FE gọi `/api/v1/pos|reports|customers` **404** (BE chưa commit).
   `docker-build` **xanh** không cứu được hai lỗi này — required check **không** phủ chúng.
2. ⚠️ **`main` chỉ required `docker-build`** (`D-59`): `ci` đỏ **không** chặn merge ⇒ *"merge được"* **không**
   đồng nghĩa *"nên merge"*. Muốn bảo vệ thật thì phải bật thêm `cong-15-buoc` — **nhưng** chỉ sau khi `ci.yml`
   bản **có bước FE** (đang chưa commit) được đo xanh.
3. ⚠️ **Docker daemon không tự chạy** (§1.1 #3): `docker compose up -d` báo *"failed to connect to the docker API
   … pipe … not found"* **không** phải compose sai. Bật Docker Desktop rồi chờ daemon (đo: lên sau **5 s**).
4. ⚠️ **Yarn 1.22 đọc `packageManager` từ thư mục CHA:** ai thêm lại field này vào `package.json` gốc là **tái
   phát** đúng nợ này (kể cả trong `apps/*` hay `packages/*`). 🔒 pnpm ghim ở CI + `Dockerfile`, **không** ở field.
5. ⚠️ **`git add -A` vẫn là bẫy:** cây có **107 `??`**, phần lớn là **rác cổng** (`tools/_*.png`, `tools/_mau/`,
   `apps/admin-web/e2e/.run`, `test-results`). Commit theo **đường dẫn cụ thể** như các phiên trước.
6. ⚠️ **Máy dev không có `gh`/`act`**; đọc PR/run/artifact bằng **REST API** + credential sẵn có của git
   (`git credential fill` ⇒ user `hungnt9801`). 🔒 **Không** in token ra log.
7. 📌 **Log CI đọc được bằng token** (`GET /actions/jobs/{id}/logs` ⇒ **89.992 B**), **artifact tải được**
   (`bang-chung-…` ⇒ **11.268 B**, chứa `test-cong.log` — thứ chỉ ra đúng 3 tệp probe). Lần sau **không** đoán.
8. 📌 **READ-ONLY không áp dụng**: phiên này chỉ chạm `package.json` + comment `Dockerfile` ⇒ mọi số của cổng
   **không đổi** so với `N16` (19/19 · 455,9 s vs 485 s — chênh do máy).

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG nợ `packageManager` (đo trên CI) → **KHÔNG** merge PR #1 (`D-61`); phiên `N18` phải
commit **cây làm việc** rồi mới merge.**

> 📌 Việc của `N17` **đã đạt** đúng phạm vi được duyệt: nợ `abp install-libs` + `packageManager` **đóng, có số
> đo trên CI thật** (bước 7 xanh). Việc **merge** bị **hoãn có đo** — không phải bỏ sót (`D-61`).

1. 🔑 **Đọc `D-61` + §5 #1 TRƯỚC khi nghĩ tới merge.**
2. Nếu chủ dự án duyệt: **commit cây làm việc** theo **đường dẫn cụ thể** (BE `Pos`/`Reporting`/`Customers` +
   controllers + migrations `57…59`; `tools/` `N12`–`N14`; docs) — message **thuần ASCII** (hook `commit-msg`
   chặn ký tự ngoài ASCII).
3. Push → chờ **`ci` + `docker-build`** trên PR #1 → **chỉ merge khi cả hai xanh**; sau đó mới **cân nhắc** bật
   `cong-15-buoc` required (việc **mới**: `ci.yml` bản có bước FE chưa từng chạy xanh — phải đo trước).
4. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) và trước khi push `-DayDu`
   (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**34/34**) · `kiem-db.ps1` (**25/25**).

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
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n17.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N18` **không** mở WP mới: việc còn lại là **commit cây làm việc**
(BE + `tools/` + docs còn chưa commit) để `ci` trên **PR #1** xanh, **rồi** merge PR #1 — đúng phạm vi ghi ở
handover §2, không mở rộng. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): vẫn đủ **3 thứ** là *mã phiên
> đích* (`N18`) · *đường dẫn handover* (`handover-n17.md`) · *việc phải làm* (`commit cây làm việc` → merge PR #1),
> và **không** mang số đo (luật 6).

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N17`) |
| Commit | **1 commit mới**: `18405ca` (**2** tệp) ⇒ `HEAD` = **`18405ca`**. Working tree **152** mục (**45 ` M`** + **107 `??`** — baseline **151**; **+1** = `handover-n17.md`; log cổng mới sinh đều bị `*.log` gitignore) |
| Push | **Có** — `origin/feature/wp-03-catalog` = **`18405ca`** (**1** lần thử). `origin/main` **không đổi** = **`531bd9d`** |
| PR | [**#1**](https://github.com/hungnt9801/eCommerce/pull/1) — `open` · base `main` · **40** commit · **488** tệp · `mergeable=true` · `mergeable_state=unstable` · **chưa merge** |
| Run Actions (head `18405ca`) | [`41955331290`](https://github.com/hungnt9801/eCommerce/actions/runs/41955331290) `ci` **failure** (294 s — bước 7 xanh, ⑦/Y1 đỏ) · [`41955331294`](https://github.com/hungnt9801/eCommerce/actions/runs/41955331294) `docker-build` **success** (66 s, 6/6) |
| Branch protection `main` | **Không đổi**: required check **`docker-build`** (`strict=false`, `enforce_admins=false`) |
| Tệp `N17` đã tạo | `docs/nhanh/handover-n17.md` |
| Tệp `N17` đã sửa | `package.json` (gốc) · `apps/admin-web/Dockerfile` (**đã commit**) · `docs/nhanh/decisions.md` · `docs/nhanh/07-ke-hoach-trien-khai.md` · `docs/nhanh/08-session-protocol.md` · `docs/nhanh/README.md` |
| Dữ liệu `N17` đã xoá | **Không** chạm DB (0 migration). Máy dev: `package.json.bak-n17` (tệp tạm khi đo) **đã xoá** |
