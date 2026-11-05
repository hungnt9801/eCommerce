# Handover `N16` — **đóng gói** image Web Admin: commit → push → **PR #1** → bật required check `docker-build`

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-57](decisions.md) (của `N15`) + **D-58**/**D-59** (phiên này)
> **Mục tiêu phiên:** `WP-13` **đã xong cả ① và ②** và `07` **chưa có `§WP-14`** ⇒ phiên này **không** mở WP
> mới: làm **đúng** việc đóng gói ở `handover-n15` §6 — commit (`.dockerignore` + `apps/admin-web/Dockerfile` +
> `.github/workflows/docker-build.yml`) → push → PR → bật `docker-build` làm **required check** **sau khi** thấy
> nó xanh trên PR. **Không mở rộng phạm vi.**

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **185** mục (**59 ` M`** + **126 `??`**) |
| 2 | `git log --oneline -1` · `git branch --show-current` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` · `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (3 giờ) — không phải dựng lại |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (26,04 s) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (2 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **117** tệp `.md`, **635** link markdown, **404** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n15` §7 (1961 test · 117/635/404 · 59 ` M` + 126 `??`) — không có sai lệch phải dừng.

### 1.2 Đã thi công: **2 commit** (khuôn cũ: `git add` **đường dẫn cụ thể**, không `git add -A`)

| # | Commit | Tệp | Vì sao có mặt |
|---|---|---|---|
| 1 | `d8e22dc` `feat(web-admin): FE WP-06..WP-11 …` | **60** | 🚨 **bắt buộc** để `docker build` chạy được trên cây sạch — §1.3 đo được: **thiếu nó thì job ĐỎ** |
| 2 | `6c49d3a` `chore(ops): dong goi image Web Admin …` | **3** (` .dockerignore` · `apps/admin-web/Dockerfile` · `.github/workflows/docker-build.yml`) | Đúng 3 gạch đầu dòng của `handover-n15` §6 (nợ §5 #1) |

📌 Cả 2 message **thuần ASCII** — hook `commit-msg` của repo chặn ký tự ngoài ASCII (đo được: lần đầu **bị
chặn** vì 3 byte ngoài ASCII trong message; sửa xong mới commit được). 📌 `tools/commit-msg-kiem.ps1` mà hook
gợi ý **không tồn tại** trong repo (chỉ là lời khuyên cũ) ⇒ kiểm bằng đếm byte `> 127`.

### 1.3 🚨 Đo **TRƯỚC khi push**: 3 lượt `docker build`, và **lượt A là ĐỎ** (quyết định của `D-58`)

| # | Cây build | Lệnh / kết quả |
|---|---|---|
| **A** | `HEAD` **+ đúng 3 tệp** đóng gói (cây `git worktree add … HEAD`) | 🔴 **exit 1** — `ERROR: failed to compute cache key: "/repo/apps/admin-web/.next/standalone": not found` (Dockerfile dòng 61) |
| **B** | `HEAD` + 3 tệp + **cây FE đang có** (`apps/admin-web/**`, `packages/api-client/**`, `package.json`, `pnpm-lock.yaml`) | ✅ **exit 0** |
| **C** | **cây SẠCH của commit** `6c49d3a` (đúng thứ CI clone) | ✅ **exit 0** · `apps/admin-web/server.js` **6380 B** · `.next/static` có · `/login` = **200** ở **lần thử 2** (API chết) · `/api/health` = **503** · size `inspect .Size` **87.168.454 B (83 MB)** vs `docker images` **354 MB** |

🔑 **Nguyên nhân ĐỎ của lượt A:** `output: "standalone"` (`N11`) nằm trong `next.config.ts` **chưa commit** ⇒
`.next/standalone` không được sinh ⇒ tầng `runner` không có gì để `COPY`. Nghĩa là: **commit đúng 3 tệp sẽ
làm job ĐỎ trên PR** — không phải vì Dockerfile sai, mà vì cây của nhánh **chưa từng chứa** phần FE mà
Dockerfile cần. ⇒ **D-58**.

### 1.4 Đo trên CI thật — **PR #1**, event `pull_request` (**lần đầu** job chạy trên event này)

| Workflow | Run | Kết quả |
|---|---|---|
| **`docker-build`** | [**`41811821775`**](https://github.com/hungnt9801/eCommerce/actions/runs/41811821775) | ✅ **SUCCESS** — **88 s** (10:05:18 → 10:06:46) · **6/6** bước xanh (checkout · tiền đề Docker linux · **build** · có `server.js` standalone · **`/login` 200 khi API chết** · bằng chứng) |
| `ci` | [`41811821821`](https://github.com/hungnt9801/eCommerce/actions/runs/41811821821) | 🔴 **FAILURE** ở **bước 7** (`abp install-libs`) — **nợ có sẵn**, xem §5 #1 (**không** do phiên này) |

📌 Log của job **đọc được bằng token** (`GET /actions/jobs/{id}/logs` ⇒ **44.149 B** văn bản) — khác `N15` §1.5
(đọc ẩn danh ⇒ **403**). Nghĩa là lần sau **không** cần `gh` để đọc log.

### 1.5 Required check `docker-build` — bật **SAU** khi thấy xanh (đúng luật `D-57` ⑥)

| Bước | Lệnh | Kết quả |
|---|---|---|
| Trước | `GET /repos/…/branches/main/protection` | **404** — 🔑 `main` **KHÔNG có** bảo vệ nhánh nào (kể cả `cong-15-buoc`) ⇒ câu *"giữ nguyên `cong-15-buoc` làm required check"* (`D-50` ④) **chưa từng đúng** trên repo thật |
| Bật | `PUT …/branches/main/protection` (body: chỉ `required_status_checks`) | **200** — `required_status_checks.checks` = **`docker-build`** (`app_id` **15368** = GitHub Actions) · `strict=false` · `enforce_admins=false` · **không** đặt review/restriction |
| Sau | `GET …/protection/required_status_checks` | **`docker-build`** · `strict=false` |
| PR #1 | `GET /pulls/1` | `open` · **39** commit · **488** tệp · `mergeable=true` · `mergeable_state=unstable` (vì `ci` đỏ) |

⛔ **Không** bật `cong-15-buoc` làm required check: nó đang **đỏ** trên chính PR này vì lý do **có sẵn** (§5 #1);
bật nó = khoá `main` bằng một job chưa xanh ⇒ đúng thứ `WP-11`/`WP-12` đã trả giá. Ghi thành **D-59**.

### 1.6 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0** — **19/19** bước · **485 s** (⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 instance **9/0** · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8**) |
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **190,8 s** |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **34/34 DAT · 0 HONG** |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 bất biến DAT · 0 HONG** (`CusCustomers` **6** dòng — phiên này **không** thêm khách; cờ bảo mật **không** bị chạm) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **118** tệp `.md`, **647** link markdown, **414** đường dẫn backtick, **0** chết |
| Mermaid (`npm run check:mermaid` trong `tools/`) | **PASS** — **213** khối, **0** lỗi (chạy **sau** khi sửa docs) |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất · NND **93** (không đổi — `PostgreSqlSchemaTests` xanh trong `-DayDu`) |
| Rác máy dev | **3** worktree tạm (`n16-head3` · `n16-full` · `n16-clean`) đã `git worktree remove` · **2** ảnh thử (`n16-full`, `n16-clean`) đã `docker rmi` · container boot-test đã `rm -f` · còn đúng `ecommerce-admin-web:local` (của `N11`) |
| 🚨 Sự cố **trong** phiên (đã khắc phục) | Khi chèn dòng `N16` vào **chính tệp này** (`08-session-protocol.md`), lệnh `Set-Content -NoNewline` **nối mất toàn bộ newline** (232 dòng → **1 dòng**). ⇒ Khôi phục bằng cách **chỉ chèn lại newline** (không đổi ký tự) + kiểm bằng phép *bỏ newline ⇒ phải bằng **đúng** chuỗi gốc*: **20.356 ký tự · `EQUAL=True`**. ⚠️ Hệ quả còn lại: các **đoạn văn** trong tệp nay là **một dòng dài** thay vì ngắt tay như bản cũ (nội dung & cách hiển thị **không đổi**); `kiem-link-tai-lieu` **PASS** (118/647/414) + mermaid **PASS** (213 khối) chạy lại **sau** khi khôi phục |

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc đóng gói **đã xong**; các mục dưới là **cắt có quyết định**, mỗi mục có
**nhận chủ**:

1. ⏭ **PR #1 chưa merge** — merge là **quyết định của chủ dự án** (không phải việc của agent). **Nhận chủ:** bạn.
   🔑 Nhưng **đọc §5 #3 trước khi merge**: PR này mang **nửa FE**, chưa có **BE** (`Pos`/`Reporting`/`Customers`
   + 3 migration `57…59`).
2. ⏭ **`cong-15-buoc` ĐỎ trên PR #1** — **nợ có sẵn**, không sửa trong phiên này (ngoài phạm vi). Nguyên nhân
   **đo được** ở §5 #1. **Nhận chủ:** phiên xử lý nợ CI (chưa có, phải chốt `§WP-14`).
3. ⏭ **Phần lớn cây làm việc `N7…N15` vẫn chưa commit** — **45 ` M`** + **106 `??`** (BE `modules/Pos`,
   `modules/Reporting`, `Customers`, `src/**` controllers + 3 migration, `tools/*` probe, `docs/nhanh/*`).
   Phiên này **chỉ** commit phần **cần cho image** (D-58). **Nhận chủ:** phiên commit BE.
4. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | Phiên này **chỉ** commit tệp có sẵn + bật 1 required check. **Không** bảng/cột/enum/index nào đổi |
| Cách lùi | **Mã:** `git revert 6c49d3a d8e22dc` (thứ tự ngược). **Settings:** `DELETE /repos/…/branches/main/protection` (xoá **toàn bộ** protection — trước phiên này `main` không có gì) hoặc `DELETE …/protection/required_status_checks` nếu chỉ muốn bỏ required check |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-58** | **Commit "đóng gói" phải kèm cây FE** — đo được: **3 tệp trên cây `HEAD` ⇒ `docker build` ĐỎ** (`standalone not found`) vì `output: "standalone"` nằm ở `next.config.ts` **chưa commit**. Tách **2 commit** (FE trước, đóng gói sau) để mỗi commit một chủ đề; commit đóng gói vẫn **đúng 3 tệp** như `handover-n15` §6 |
| **D-59** | **Required check: `main` trước phiên này KHÔNG có bảo vệ nhánh** (`GET` ⇒ **404**) ⇒ *"giữ nguyên `cong-15-buoc`"* (`D-50` ④) là **chưa từng đúng**. Bật **đúng một** check `docker-build` (`app_id` 15368, `strict=false`, `enforce_admins=false`); ⛔ **không** bật `cong-15-buoc` khi nó đang đỏ vì nợ riêng (`packageManager` + `yarn`, §5 #1) |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`cong-15-buoc` ĐỎ vì `packageManager` + `yarn` — nợ CÓ SẴN, đo được, KHÔNG do phiên này.**
   Bước 7 của `ci.yml` chạy `abp install-libs -wd src/eCommerce.Web` ⇒ ABP gọi `yarn` (1.22.22) ⇒ Yarn 1 đọc
   `packageManager` và **từ chối** giá trị không phải `yarn@…`:
   ```text
   error This project's package.json defines "packageManager": "yarn@pnpm@10.22.0". However the current global version of Yarn is 1.22.22.
   ##[error]Process completed with exit code 1.
   ```
   🔑 Nguồn: `package.json` **gốc repo** có `"packageManager": "pnpm@10.22.0"` (thêm ở `N1`-era, có trong
   `39b8766`, **không có** trên `main`). PR #1 là **lần đầu** `ci.yml` chạy trên một cây **có** tệp đó (mọi lần
   trước là `push` lên `main`, mà `main` không có `package.json` gốc) ⇒ **nợ cũ, mới lộ**.
   ⚠️ **Chưa chọn cách sửa** (mỗi cách phải đo lại): (a) bỏ `packageManager` khỏi `package.json` gốc — image
   **không** phụ thuộc nó (`Dockerfile` đã `corepack prepare pnpm@10.22.0 --activate`); (b) giữ và cho
   `abp install-libs` đi đường khác (`npm`); (c) ghim `yarn@` — **không** được, vì repo dùng pnpm.
   ⛔ **Không** "sửa" bằng cách bỏ bước 7 khỏi `ci.yml` (mất phép kiểm thư viện client của `eCommerce.Web`).
2. ⚠️ **Required check chỉ có `docker-build`** ⇒ `cong-15-buoc` đỏ **không** chặn merge. Đừng tưởng `main` đã
   được bảo vệ toàn diện: hiện chỉ chặn đúng **một** job.
3. 🚨 **Merge PR #1 = đưa NỬA FE vào `main`.** PR mang `apps/admin-web/**` (FE `N7…N11` gọi `/api/v1/pos`,
   `/reports`, `/customers`, `/settings`) nhưng **BE** của các module đó (`modules/Pos`, `modules/Reporting`,
   `CustomersController`…) + **3 migration `57/58/59`** vẫn **chưa commit** ⇒ sau khi merge, `main` **build được
   image** nhưng **không chạy đúng nghiệp vụ** (route FE gọi API 404). ⇒ Nếu muốn `main` luôn "chạy được", phải
   commit **BE** trước hoặc **cùng** PR. Ghi ở đây để không ai đọc *"PR xanh"* thành *"sản phẩm đủ"*.
4. ⚠️ **GitHub trả lỗi 5xx liên tục khi push/PR (đo được 04/11/2026):** `git push` fail **3 lần** —
   `remote: Internal Server Error` (500), rồi `remote: fatal error in commit_refs` + `! [remote rejected] (failure)`
   **2 lần** — lần **4** mới `* [new branch]`. `POST /pulls` fail **1 lần** (`502 Bad Gateway`) rồi được. 📌 **Đừng**
   kết luận "không có quyền"/"repo hỏng": **thử lại** (cùng lớp với `N15` §5 #5).
5. ⚠️ **Máy dev KHÔNG có `gh`, `act`** (đo lại: `gh NOT FOUND`, `act NOT FOUND`). PR + required check làm bằng
   **REST API** với credential **sẵn có của git** (`git credential fill`, user `hungnt9801`, token `gho_…`) —
   **đủ quyền admin** trên repo (`permissions.admin=true`) để `PUT` branch protection. 🔒 Không in token ra log.
6. ⚠️ **`git add -A` vẫn là bẫy:** cây làm việc có **106 `??`**, phần lớn là **rác gate** (`tools/_*.png`,
   `tools/_tam-*`, `apps/admin-web/e2e/.run`, `test-results`) ⇒ commit theo **đường dẫn cụ thể** như phiên này.
7. 📌 **`READ-ONLY` không áp dụng:** phiên này **không** chạm `.ps1`/mã BE ⇒ các con số `-DayDu` **không** đổi
   so với `N15` (19/19 · 485 s vs 474,9 s — chênh do máy, không do mã).
8. 🔒 **Cờ bảo mật** (`D-56`) **không** bị chạm: `kiem-db.ps1` xanh **25/25** sau phiên (xem §1.6).
9. ⚠️ **Required check chỉ có răng khi nhánh PR CHỨA workflow** — `.github/workflows/docker-build.yml` hiện **chỉ** có trên
   nhánh `feature/wp-03-catalog`; đo `git ls-tree origin/main -- .github/workflows/docker-build.yml` ⇒ **rỗng**. 📌 Suy ra từ cơ
   chế GitHub (**chưa đo** — chưa có PR thứ hai): PR từ nhánh **không** chứa workflow sẽ đứng ở *"Expected — waiting for status"*
   và **không merge được**. ✅ Đóng bằng cách **merge PR #1** (đưa workflow vào `main`); trước đó, nhánh mới phải có tệp này.
10. ⚠️ **`mergeable_state = unstable` KHÔNG phải "bị chặn"**: PR #1 có `mergeable=true` (required check duy nhất đã xanh) —
    `unstable` chỉ vì `ci` đỏ, mà `ci` **không** nằm trong required checks.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG việc đóng gói** (`WP-13` đã đóng cả ① và ②; nợ §5 #1 của `handover-n15` **đã đóng**)
→ `07` **CHƯA có `§WP-14`** ⇒ phiên `N17` **không** mở WP mới; việc còn lại là **merge PR #1** (quyết định của
bạn) và **nợ `cong-15-buoc`** ở §5 #1 (cần được duyệt thành một WP riêng).

> 📌 Việc của `N16` **đã đạt** đúng luật `D-57` ⑥: **thấy `docker-build` xanh trên PR thật rồi mới** bật required
> check (run `41811821775`, 88 s). Không có việc nào của `WP-13` còn mở.

1. 🔑 **Đọc §5 #3 TRƯỚC khi bấm merge PR #1** — PR mang nửa **FE**, BE chưa commit.
2. Nếu chủ dự án duyệt xử lý nợ CI: **`abp install-libs` + `packageManager`** (§5 #1) — đo **trước** khi sửa
   (`ci.yml` bước 7), sửa **một chỗ**, rồi chạy lại `-DayDu`; ⛔ **không** bỏ bước khỏi `ci.yml`.
3. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) và trước khi push `-DayDu` (**19/19**)
   · `test-cong-kiem.ps1 -ChiKiemMoc` (**34/34**) · `kiem-db.ps1` (**25/25**).

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
và handover gần nhất `docs/nhanh/handover-n16.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N17` **không** mở WP mới: việc còn lại là **merge PR #1** (đọc
handover §5 #3 trước) và xử lý **nợ `cong-15-buoc`** ghi ở handover §5 #1 nếu được duyệt — đúng phạm vi đã
ghi, không mở rộng. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): vẫn đủ **3 thứ** là *mã phiên
> đích* (`N17`) · *đường dẫn handover* (`handover-n16.md`) · *việc phải làm* (`merge PR #1` + nợ `D-59`), và
> **không** mang số đo (luật 6).

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N16`) |
| Commit | **2 commit mới**: `d8e22dc` (FE, **60** tệp) · `6c49d3a` (đóng gói, **3** tệp) ⇒ `HEAD` = **`6c49d3a`**. Working tree còn **151** mục (**45 ` M`** + **106 `??`** — so baseline **185** giảm **63** tệp đã commit, tăng **29** mục rác do cổng/probe sinh ra) |
| Push | **Có** — `origin/feature/wp-03-catalog` = **`6c49d3a`** (4 lần thử, 3 lần GitHub 5xx). `origin/main` **không đổi** = **`531bd9d`** |
| PR | [**#1**](https://github.com/hungnt9801/eCommerce/pull/1) — `open` · base `main` · **39** commit · **488** tệp · `mergeable=true` · **chưa merge** |
| Run Actions | [`41811821775`](https://github.com/hungnt9801/eCommerce/actions/runs/41811821775) `docker-build` **success** (88 s) · [`41811821821`](https://github.com/hungnt9801/eCommerce/actions/runs/41811821821) `ci` **failure** (bước 7 — nợ §5 #1) |
| Branch protection `main` | **MỚI**: required check **`docker-build`** (app_id 15368, `strict=false`, `enforce_admins=false`). Trước phiên: **không có** protection (404) |
| Tệp `N16` đã tạo | `docs/nhanh/handover-n16.md` |
| Tệp `N16` đã sửa | `docs/nhanh/decisions.md` (**D-58**/**D-59** + nhật ký) · `docs/nhanh/07-ke-hoach-trien-khai.md` (`§WP-13` + `§13`) · `docs/nhanh/08-session-protocol.md` (`§3` hàng `N16`) · `docs/nhanh/README.md` (bảng tài liệu) |
| Dữ liệu `N16` đã xoá | **Không** chạm DB (0 migration). Máy dev: 3 worktree tạm + 2 ảnh thử đã xoá (§1.6) |


