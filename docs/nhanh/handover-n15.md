# Handover `N15` — WP-13 ②: **job CI `docker-build`** (đo **thật** trên `ubuntu-latest` rồi mới thêm)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-57](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-13 ②** — thêm job CI `docker-build` **chỉ sau khi** thấy nó xanh **một lần
> thật** trên `ubuntu-latest`; chưa đo được thì **ghi lý do, giữ P1**. **Không mở rộng phạm vi.**
> 📌 Đây là **nợ kiểm chứng** (mục P1 cuối cùng của `WP-13`), không phải tính năng.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **183 mục** (**59 ` M`** của `N7…N14` chưa commit + **124 `??`**) |
| 2 | `git log --oneline -1` · `git branch --show-current` | `39b8766` · nhánh `feature/wp-03-catalog` (đúng nhánh của WP, không `git switch`) |
| 3 | `docker compose up -d` · `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** (3 giờ) — không phải dựng lại |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (24,70 s, incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (3 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **116** tệp `.md`, **622** link markdown, **393** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n14` §7 (1961 test · 116/622/393 · 59 ` M` + 124 `??`) — không có sai lệch phải dừng.

### 1.2 Vì sao 3 phiên trước (N11→N14) **giữ P1**: *tưởng thiếu runner, thực ra thiếu **đường trigger***

Đo lại **trước khi sửa** trên máy này:

| Kiểm | Kết quả |
|---|---|
| `gh --version` · `act --version` | **NOT FOUND** cả hai (đúng như `handover-n14` §2 #1) |
| `git remote -v` | `origin https://github.com/hungnt9801/eCommerce.git` |
| `GET api.github.com/repos/hungnt9801/eCommerce` | **HTTP 200** · `private=**false**` · `default_branch=main` |
| `GET …/actions/runs?per_page=5` | **5 run** `main` **success**, mới nhất **03/11/2026** ⇒ **Actions đang bật** |
| `git push --dry-run origin HEAD:refs/heads/ci-probe/…` | **exit 0** (`* [new branch]`) ⇒ **push được** (credential helper = `manager`) |
| `git fetch origin main` · `rev-list --left-right --count HEAD...origin/main` | **37 / 0** — `origin/main` = `531bd9d` (là tổ tiên của `HEAD`) |

🔑 **Kết luận đo được:** runner **có** (GitHub-hosted `ubuntu-latest`), tài khoản **push được**; thứ duy nhất
chặn phép đo là **đường trigger** — `ci.yml` chỉ có `on.push.branches: [main]` và `pull_request` **chỉ chạy khi
có PR**, mà máy dev **không có `gh`** để tạo PR. ⇒ Cách đo **không cần `gh`**: đẩy một **nhánh tạm** chứa
workflow với `on: push` (mọi nhánh) ⇒ **có run thật**, đo xong **xoá nhánh**.

### 1.3 Đã thi công: **`.github/workflows/docker-build.yml`** (mới, 120 dòng, 6 bước)

| Bước | Nội dung | Vì sao (không phải trang trí) |
|---|---|---|
| 0 | `actions/checkout@v4` | — |
| 1 | **Tiền đề:** `docker info --format '{{.OSType}}'` = `linux` | Dockerfile dùng `node:22-alpine`; engine Windows ⇒ đỏ **vì môi trường** (cùng lý do `ci.yml` bước 4) |
| 2 | **`docker build -f apps/admin-web/Dockerfile`** + `--build-arg NEXT_PUBLIC_APP_NAME=eCommerce` + `--label …revision=$GITHUB_SHA`, context = **gốc repo**, in **hai** cách đo kích thước | `D-51` ④ (context gốc: workspace pnpm) · `D-52` ⑤ (`NEXT_PUBLIC_*` là **build arg**) |
| 3 | **Image phải có** `apps/admin-web/server.js` + `.next/static` (chạy `sh -c ls` **trong** image) | `output: "standalone"`; sai đường `COPY --from=builder` thì tầng 2 vẫn build **XANH** nhưng container chết ở `CMD` |
| 4 | **Image phải BẬT ĐƯỢC:** `docker run -d` với `API_BASE_URL=http://127.0.0.1:9` (Host **chết**), poll `/login` ≤ **60 s** ⇒ phải **200**, in `docker logs` rồi `rm -f` | Build xanh **không** chứng minh image chạy được; `/login` **200** khi API chết là bất biến `D-52` ⑦ |
| 5 | `$GITHUB_STEP_SUMMARY`: commit · size · môi trường (`if: always()`) | bằng chứng bám theo lượt chạy, không "nhớ" |

📌 `on:` **bản giao** = `pull_request` + `push: branches: [main]` + `workflow_dispatch`; `permissions: contents: read`;
`concurrency: docker-build-${{ github.ref }}` (PR huỷ lượt cũ, `main` xếp hàng); `timeout-minutes: 20`.
📌 **Workflow RIÊNG** (không nhét job vào `ci.yml`): job **không cần** .NET/PostgreSQL/Redis ⇒ status check riêng,
**không** làm chậm `cong-15-buoc` (~30 phút), log **không** trộn (cùng lý do `D-50` ③).

### 1.4 Đo — bốn lượt, **trước/sau** đều có số

| # | Ở đâu | Lệnh/run | Kết quả |
|---|---|---|---|
| 0 | **máy dev** (trước push) | `docker build -f apps/admin-web/Dockerfile -t ecommerce-admin-web:ci-n15 --build-arg NEXT_PUBLIC_APP_NAME=eCommerce .` | **exit 0** · `pnpm build` trong container **45,8 s** · `next build` in **ƒ /api/health**, **ƒ /login** … |
| 0b | máy dev | `docker image inspect .Size` **vs** `docker images` | **87.168.294 B = 83 MB** **vs** cột `SIZE` = **354 MB** — **cùng một image** (📌 `N11` ghi 354 MB = cách đo của `docker images`) |
| 0c | máy dev | `docker run --rm --entrypoint sh … ls -l apps/admin-web/server.js` | `-rw-r--r-- node node 6380 … server.js` + `apps/admin-web/.next/static` ⇒ **exit 0** |
| 0d | máy dev | `docker run -d … -e API_BASE_URL=http://127.0.0.1:9` rồi poll `/login` | **200 ở lần thử 2** · `/api/health` = **503** · log `✓ Ready in 79ms` |
| 1 | **`ubuntu-latest`** | run [`41808380570`](https://github.com/hungnt9801/eCommerce/actions/runs/41808380570) (sha `08b048a`) | 🚨 **FAILURE** ở bước **2 (build)**, bước 3/4 `skipped` — **lý do gốc ở §1.5** |
| 2 | **`ubuntu-latest`** | run [**`41808550753`**](https://github.com/hungnt9801/eCommerce/actions/runs/41808550753) (sha `c754720`) | ✅ **SUCCESS** — **6/6** bước xanh · job **76,0 s** (bước build **68 s**, boot-test **1 s**, checkout **2 s**) · runner `GitHub Actions 1000000025` |

📌 **Run 2 vẫn đọc được sau khi xoá nhánh** (đo lại: `GET …/actions/runs/41808550753` ⇒ `completed/success`,
`head_branch=ci/n15-docker-build`) ⇒ **bằng chứng không mất** khi dọn nhánh.

### 1.5 🚨 Lượt đo **ĐẦU** đỏ — lý do gốc (**nợ thật**, không phải "làm cho xanh")

**Dấu hiệu:** run 1 `failure` ở **đúng bước 2**, các bước sau **skipped**; log **không đọc được** bằng API
ẩn danh (`/logs` ⇒ **403** — log của repo công khai vẫn cần token) nên phải chứng minh **nguyên nhân** bằng
dữ liệu của chính commit đã đẩy:

```text
git ls-tree -r --name-only 08b048a -- apps/admin-web/Dockerfile   →  RỖNG
git ls-tree -r --name-only 08b048a -- .dockerignore               →  RỖNG
```

🔑 **Nguyên nhân gốc:** `apps/admin-web/Dockerfile` và `.dockerignore` **CHƯA TỪNG được commit** (vẫn `??` từ
`N11`) ⇒ commit tạm đầu tiên **không có** chúng ⇒ `docker build -f apps/admin-web/Dockerfile` **không có gì để
đọc**. ⚠️ Đây **không** phải lỗi của job: nó là **phát hiện đúng** — *một người clone repo công khai hôm nay
**không** build được image Web Admin*. Đo lại sau khi đưa **đúng cây làm việc của phần FE** (`.dockerignore`,
`apps/admin-web/**`, `packages/api-client/**`, `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`) vào
commit tạm ⇒ run 2 **xanh**.

### 1.6 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0** — **19/19** bước · **474,9 s** (⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 instance **9/0** · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8**) |
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **193,5 s** |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HONG** (`CusCustomers` **6** dòng — **không** thêm khách nào; NND **93**) · cờ bảo mật **không** bị chạm (job CI không gọi `psql`) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **34/34 DAT · 0 HONG** (không đổi — phiên này **không** sửa script cổng/probe) |
| `dotnet build` / `dotnet test` (trong cổng) | **0 error / 0 warning** (70,7 s, `--no-incremental`) · **1961/1961 pass** |
| `dotnet test test\eCommerce.IntegrationTests` (trong cổng) | **96/96 pass** |
| `pnpm typecheck` + `pnpm test` (bước [6] của cổng) | ✓ 0 lỗi · **25/25 pass** |
| Mermaid (bước [5] của cổng; chạy lại sau khi viết xong tài liệu) | **PASS** — **213** khối · **0** lỗi (5,1 s trong cổng) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **117** tệp `.md`, **635** link, **404** backtick, **0** chết |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB dev (NND) | **93** (không đổi — phiên này **không** chạm schema) |
| Rác máy dev | ảnh `ecommerce-admin-web:ci-n15` đã **`docker rmi`**; container `admin-web-ci` đã `docker rm -f` |

📌 Lượt `-DayDu` chạy **sau** khi sửa docs (07/08/README/decisions) ⇒ bước [5] mermaid đo **đúng** cây sẽ giao.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** `WP-13` **đóng cả ① và ②**. Các mục **cố ý cắt**, mỗi mục có **nhận chủ**:

1. ⏭ **Required check `docker-build` CHƯA bật** (`D-57` ⑥). 🔑 Đó là **cài đặt của repo** (Settings → Branches
   → required status checks), **không** sửa được bằng mã; và chỉ nên bật **sau** khi thấy nó xanh trên **một PR
   thật**. **Nhận chủ:** người có quyền admin trên repo, ở phiên **commit + push + PR**.
2. ⏭ **Job chưa chạy trên event `pull_request`** — đã đo trên event `push` (nhánh tạm). 📌 Thân job
   **byte-identical**; chỉ `on:` của **bản ghi đo** khác bản giao (xem §5 #3). **Nhận chủ:** PR đầu tiên.
3. ⏭ **Chưa đưa `apps/admin-web/Dockerfile` + `.dockerignore` vào commit** — phiên này **không** commit gì
   (đúng quy ước `N7…N14` chưa commit). 🚨 Nhưng đây là **nợ chặn**: xem §5 #1.
4. ⏭ **Không** làm image .NET/Host trong CI (`D-51` ⑦ — việc khác, cần quyết định ai sở hữu migration).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-13` ② chỉ thêm **1 workflow CI** + docs; **không** bảng/cột/enum/index. ✅ Bằng chứng: integration **96/96** · `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT` · `schema.sql` **không đổi** |
| Cách lùi | Không cần. Lùi = **xoá** `.github/workflows/docker-build.yml` (job chưa bật required ⇒ biến mất không ảnh hưởng ai) + `git checkout` 4 tệp docs nếu muốn bỏ `D-57` |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-57** | **JOB CI `docker-build` (`WP-13` ②) — WORKFLOW RIÊNG + đo **THẬT** trên `ubuntu-latest` trước khi thêm; và *"cây chưa commit thì không build được"*** — ① run `41808550753` **success** 76 s (build 68 s) ② workflow **riêng** (không cần .NET/DB; status check riêng; không làm chậm cổng 30 phút; cùng lý do `D-50` ③) ③ 🚨 lượt đo **đầu ĐỎ** vì `Dockerfile`/`.dockerignore` **untracked** ⇒ *clone repo công khai **không** build được image* — job này là **lưới** cho đúng lớp lỗi đó ④ job **không chỉ build**: `server.js` standalone + `/login` **200** khi API chết (`D-52` ⑦) ⑤ **hai** cách đo size trên **cùng** image (83 MB `inspect` vs 354 MB `docker images`) ⑥ required check **chưa bật** (cài đặt repo, không phải mã) ⑦ ⛔ không mở rộng sang image .NET |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`apps/admin-web/Dockerfile` + `.dockerignore` là file `??` (untracked) tới hôm nay.** Hệ quả **đo được**:
   run 1 đỏ vì chúng **không có** trong commit. ⇒ Nếu commit/push mà **quên** 2 tệp này thì **người clone repo
   không build được image Web Admin**, và job `docker-build` sẽ đỏ trên PR đầu tiên — **vì thiếu tệp**, không
   vì Dockerfile sai. 🔒 Chúng **phải** vào commit đầu tiên (`git add .dockerignore apps/admin-web/Dockerfile`).
2. ⚠️ **Required check chưa bật** ⇒ job là **advisory**: PR vẫn merge được khi nó đỏ. Muốn nó có răng thì bật
   trong branch protection — nhưng **chỉ sau** khi thấy xanh trên PR thật (`WP-11`/`WP-12` đã trả giá cho việc
   bật check chưa đo).
3. ⚠️ **`on:` của bản ghi đo ≠ `on:` của bản giao.** Bản đo (nhánh tạm) dùng `push` **mọi nhánh** để có run mà
   không cần `gh`/PR; bản giao dùng `pull_request` + `push: branches:[main]` + `workflow_dispatch`. 📌 Chỉ khối
   `on:` khác — **thân job y nguyên** (6 bước + mọi tham số). ⛔ Đừng "sửa" bản giao thành `push: ['**']` để
   "cho giống lần đo": làm vậy job chạy **2 lần** cho mỗi PR-và-main và tốn phút Actions.
4. ⚠️ **`docker images` SIZE (354 MB) ≠ `inspect .Size` (83 MB)** trên **cùng** image. `N11`/`README`/`07` ghi
   **354 MB** — **đừng** "sửa" số cũ thành 83 MB: cả hai đúng, khác **cách đo**. Job in **cả hai** để lần sau
   không tưởng image nhỏ đi 4 lần.
5. ⚠️ **Xoá nhánh có thể báo lỗi phía GitHub:** lần 1 trả `remote: fatal error in commit_refs` +
   `! [remote rejected] (failure)`; **thử lại lần 2 xoá được**. 📌 Đừng kết luận "không có quyền" rồi bỏ mặc
   nhánh tạm trên repo công khai.
6. ⚠️ **Boot-test 1 s là bình thường** (`Next ready in 79ms` trên image standalone) — nhưng nếu Next khởi động
   chậm hơn trên runner, vòng poll **60 s** lo; đừng hạ trần xuống vài giây.
7. 📌 **Bước build chiếm 68/76 s** ⇒ job rẻ; giữ nó **tách** khỏi `cong-15-buoc` (~30 phút). Nếu sau này thêm
   `--cache-from`/registry thì phải **đo lại**, đừng giả định.
8. 🔒 **Quy ước cờ bảo mật** (`D-56`) **không** bị chạm trong phiên này: job CI **không** gọi `psql`, **không**
   `UPDATE` `AbpUsers`; sau `-DayDu` cổng DB vẫn **25/25**.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-13 (cả ① và ②) → `07` đã hết WP; phiên `N16` làm việc **đóng gói**, không mở WP mới.**

> 📌 `WP-13` ② **đã đạt** đúng luật của nó: thấy job xanh **một lần thật** trên `ubuntu-latest`
> (run `41808550753`, 76 s) **rồi mới** thêm vào CI. `07` **chưa có `§WP-14`** ⇒ phiên sau **không** được tự
> đặt WP khác (`08 §5.1` luật 5). Việc còn lại là **commit + push + PR** (đóng nợ §5 #1) và bật required check
> (§5 #2).

1. 🚨 **Commit theo khuôn cũ** (`git add` **đường dẫn cụ thể**, **không** `git add -A`) và **bắt buộc** có:
   `.dockerignore` · `apps/admin-web/Dockerfile` · `.github/workflows/docker-build.yml` — thiếu 2 tệp đầu thì
   **job đỏ trên PR đầu** (§5 #1). Lệnh kiểm: `git ls-files --error-unmatch .dockerignore apps/admin-web/Dockerfile`.
2. Mở PR ⇒ xem job **`docker-build`** chạy trên event **`pull_request`** (lần đầu). Nếu xanh ⇒ bật nó làm
   **required check**; nếu đỏ ⇒ đọc **annotations của job** (log API ẩn danh **403**, cần `gh`/token).
3. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) và trước khi push
   `-DayDu` (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**34/34**) · `kiem-db.ps1` (**25/25**).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
git ls-files --error-unmatch .dockerignore apps/admin-web/Dockerfile .github/workflows/docker-build.yml
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
và handover gần nhất `docs/nhanh/handover-n15.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `WP-13` đã xong **cả ②** và `07` **chưa có `§WP-14`** ⇒ phiên `N16` **không** mở WP mới: làm
**đúng** việc đóng gói ghi ở handover §6 — commit (`.dockerignore` + `apps/admin-web/Dockerfile` +
`.github/workflows/docker-build.yml`) → push → PR, rồi bật `docker-build` làm required check **sau khi** thấy
nó xanh trên PR. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N15`) |
| Commit | **không** commit gì trong phiên: commit tạm phục vụ phép đo đã bị `git reset --mixed HEAD~1` bỏ ⇒ `HEAD` vẫn **`39b8766`**. Working tree **185 mục** (**59 ` M`** + **126 `??`**); so baseline **+2 `??`** = `.github/workflows/docker-build.yml` + `docs/nhanh/handover-n15.md` (các ` M` **không đổi** = **59**). Commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A` |
| Push | **Có** — 1 nhánh **tạm** `ci/n15-docker-build` (2 lần: `08b048a` đỏ, `c754720` xanh) rồi **đã xoá** (`git ls-remote --heads origin` còn **đúng `main`**). `main` **không đổi** = **`531bd9d`**. **PR: không tạo** |
| Run Actions (bằng chứng) | [**`41808550753`**](https://github.com/hungnt9801/eCommerce/actions/runs/41808550753) **success** (76 s) · `41808380570` **failure** (lượt đầu, §1.5) — cả hai **vẫn đọc được** qua API sau khi xoá nhánh |
| Tệp `N15` đã tạo | `.github/workflows/docker-build.yml` · `docs/nhanh/handover-n15.md` |
| Tệp `N15` đã sửa | `docs/nhanh/decisions.md` (**D-57** + nhật ký) · `docs/nhanh/07-ke-hoach-trien-khai.md` (`§WP-13` ② + `§13` nhật ký) · `docs/nhanh/08-session-protocol.md` (`§3` hàng **N15** + `§8`) · `docs/nhanh/README.md` (bảng tài liệu) |
| Dữ liệu `N15` đã xoá | **Không** chạm DB (0 migration). Máy dev: ảnh `ecommerce-admin-web:ci-n15` đã `docker rmi`, container `admin-web-ci` đã `docker rm -f` |
| Cổng tài liệu (`kiem-link-tai-lieu.ps1`) | **PASS** — **117** tệp `.md`, **635** link markdown, **404** đường dẫn backtick, **0** chết (baseline: 116/622/393) |
