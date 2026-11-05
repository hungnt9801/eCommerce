# Handover `N18` — commit **cây làm việc** → `ci` xanh trên PR #1 → **MERGE** (2 lỗi chỉ lộ trên CI)

> Ngày 05/11/2026 · nhánh `feature/wp-03-catalog` (checkpoint **`18405ca`**) ⇒ **đã merge vào `main`** = **`fab0089`**
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-62](decisions.md) · [D-63](decisions.md)
> **Mục tiêu phiên:** `07` **chưa có `§WP-14`** ⇒ phiên này **không** mở WP mới. Việc còn lại đúng phạm vi
> `handover-n17` §2: **commit cây làm việc** (BE + `tools/` + docs) để `ci` trên **PR #1** xanh, **rồi** merge.
> **Không mở rộng** — nhưng `ci` lượt đầu **đỏ** vì 2 lỗi **chỉ lộ trên cây sạch** ⇒ phải sửa mới merge được (§1.4).

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline 6 lệnh §4 của [08](08-session-protocol.md) — đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **152** mục (**45 ` M`** + **107 `??`**) — khớp `handover-n17` §7 |
| 2 | `git log --oneline -1` · `git branch --show-current` | `18405ca` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` · `docker compose ps` | daemon **29.6.2** đã chạy sẵn (không phải bật) ⇒ `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** **2 giờ** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (24,2 s) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (5,1 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **119** tệp `.md`, **660** link markdown, **425** đường dẫn backtick, **0** chết |

📌 Baseline **khớp** `handover-n17` §1.1 (152 mục · 1961 test · 119/660/425) — khác **#3**: Docker **đang chạy**
(từ phiên trước để lại), nên **không** có pha "bật Docker Desktop".

### 1.2 Commit cây làm việc — **6 commit · 160 tệp** (không `git add -A`)

| Commit | Tệp | Nội dung |
|---|---|---|
| **`6ab9114`** | **107** | BE `WP-06…WP-08`: `modules/Pos` + `modules/Reporting` + `Customers` (WP-07) + `Settings` + 4 controller `/api/v1` + **migration 57/58/59** + `schema.sql` + 4 tệp test mới |
| **`4248d80`** | **1** | `ci.yml`: bước **6b/6c** (pnpm + build + Chromium) + **2 job** `openapi-drift` · `e2e-playwright` |
| **`b94b13e`** | **1** | `docker-compose.yml`: service **`admin-web`** (profile `ops`) |
| **`0bbc140`** | **32** | `tools/` `N12`–`N14`: **4 hàm** một chỗ cho cờ đổi mật khẩu, **9** script hết khuôn chỉ-Windows, probe mới (`probe-pos` · `probe-khach-hang-wp07` · `probe-bao-cao-wp08` · `kiem-ops-wp10` · `kiem-openapi-drift` · `e2e.ps1`), **2** ca `Y6`/`L2`, `.gitignore` cho `e2e/.run` |
| **`3f93c14`** | **15** | docs: **11** handover `N7…N17` + `07`/`08`/`README`/`decisions` (D-19…D-61) |
| **`39df2d4`** | **4** | **2 fix đo được trên CI** (§1.4) |

📌 **Rác cổng vẫn ngoài VCS:** **39** tệp `tools/_*` (`_*.png`, `_*.json`, `_bang-nhap-nhanh.csv`, `_anh-tam/`,
`_mau/`) — **cố ý** không commit (`git status` cuối phiên chỉ còn đúng nhóm này). Commit theo **đường dẫn cụ
thể** như các phiên trước.

### 1.3 `ci` lượt **1** (head `3f93c14`) — cổng **19/19 XANH**, nhưng **2 chỗ ĐỎ**

| Workflow | Run | Kết quả |
|---|---|---|
| **`docker-build`** | [`41961196202`](https://github.com/hungnt9801/eCommerce/actions/runs/41961196202) | ✅ **SUCCESS** (6/6) |
| `ci` | [`41961196209`](https://github.com/hungnt9801/eCommerce/actions/runs/41961196209) | 🔴 **FAILURE** — **3** job: `openapi-drift` ✅ · `cong-15-buoc` 🔴 · `e2e-playwright` 🔴 |

🔑 **Điểm quan trọng nhất của lượt này:** bước **10** (`kiem-tat-ca.ps1 -DayDu`) **XANH** —
`TAT CA PHEP KIEM DAT (290,5 s)` · `TONG SO BUOC DAT: 19` · Playwright **8 passed (21,9 s)`. Tức là
**cổng nghiệp vụ đã xanh trên CI**; hai chỗ đỏ là hai bước/job **đo thứ khác**:

| Chỗ đỏ | Bằng chứng đo được | Nguyên nhân gốc |
|---|---|---|
| Bước **11** (`CONG 13` — link tài liệu) + bước **12** (`ci-khang-dinh` A7) | `FAIL — 3 tham chieu chet:` → `docs/nhanh/01-khao-sat-nhanh-vn.md` · `../../artifacts/nhanh-survey/{bill-detail,order-list,product-list}.png` | 3 ảnh đó **cố ý không commit** (`README` §6: *"`artifacts/` gitignore"*) ⇒ **máy dev có thư mục** ⇒ xanh; **bản clone sạch** ⇒ đỏ |
| Job `e2e-playwright` — bước **9** (8 luồng) | `1 failed · 7 passed (2,6 m)` · `✘ 8 08-xuat-csv.spec.ts` · `Error: page.waitForEvent: Test timeout of 60000ms exceeded` · ảnh chụp trang: **`Xuất CSV [disabled]`** + *"Chưa chụp ảnh tồn kho lần nào"* | **DB TRỐNG**: job `ReportingAggregationWorker` chạy lúc Host khởi động (chưa có hàng) ⇒ chụp ảnh tồn kho **0 dòng** **nhưng vẫn ghi mốc = hôm nay** ⇒ mọi lượt sau **bỏ qua** bước chụp ⇒ báo cáo tồn kho **rỗng cả ngày** |

### 1.4 Sửa **2 lỗi chỉ lộ trên cây sạch** — commit **`39df2d4`** (**4** tệp)

| Tệp | Sửa | Vì sao **một chỗ** |
|---|---|---|
| `modules/Reporting/eCommerce.Reporting.Application/TongHopBaoCaoService.cs` | chỉ `GhiNhan` mốc tồn kho khi **`soAnhTonKho > 0`** | mốc = *"ngày đã **CÓ** ảnh"*, không phải *"ngày đã **THỬ** chụp"*; lượt chụp rỗng để nguyên mốc ⇒ lượt 15 phút sau chụp lại. 📌 Không nới điều kiện nào khác |
| `tools/kiem-link-tai-lieu.ps1` | link trỏ vào **`artifacts/`** (bằng chứng **ngoài VCS**) **không** tính "chết"; đường dẫn **trong repo** vẫn phải có thật | cùng quy ước với `$boQua` (đã loại `artifacts` khỏi danh sách tệp `.md` được quét) — đây là vế **đối xứng** còn thiếu |
| `tools/test-cong-kiem.ps1` | **2** ca mới: **V4** (`artifacts/` ⇒ exit 0) + **V5** (cùng tệp có thêm link chết **trong repo** ⇒ **vẫn** exit 1) | 🔒 chống "sửa cổng cho xanh": V5 chứng minh phép bỏ qua **không** nuốt lỗi thật |
| `apps/admin-web/e2e/tests/08-xuat-csv.spec.ts` | khẳng định khớp **đúng** câu chữ *"Chưa chụp ảnh tồn kho lần nào"* | câu cũ tìm *"…nào"* ⇒ **luôn đúng** ⇒ khi trang thật sự rỗng, lỗi hiện ra là `waitForEvent timeout` (**khó đọc**) thay vì *"chưa chụp ảnh tồn kho"* (**đúng nguyên nhân**) |

**Đo lại trên máy dev sau khi sửa:** build **0/0** · **1961/1961** unit · **96/96** integration ·
`kiem-link-tai-lieu.ps1` **PASS** (**119** tệp · **657** link — giảm đúng **3** link `artifacts/` · **425** backtick) ·
`test-cong-kiem.ps1 -ChiKiemMoc` **36/36** (trước **34/34**; **+V4 +V5**) ·
`kiem-tat-ca.ps1 -DayDu` **exit 0 — 19/19, 436,8 s** (⑲ Playwright **8/8**, 37,9 s).

### 1.5 `ci` lượt **2** (head `39df2d4`) — **3/3 job XANH**

| Workflow | Run | Kết quả |
|---|---|---|
| **`docker-build`** | [`41963379197`](https://github.com/hungnt9801/eCommerce/actions/runs/41963379197) | ✅ **SUCCESS** (6/6) |
| **`ci`** | [**`41963379150`**](https://github.com/hungnt9801/eCommerce/actions/runs/41963379150) | ✅ **SUCCESS** — **10,2 phút**, **3/3** job: `openapi-drift` **2m14s** · `cong-15-buoc` **10m13s** · `e2e-playwright` **3m58s** |

Số đo **trong log CI** (không phải máy dev): `TAT CA PHEP KIEM DAT (352,8 s)` · `TONG SO BUOC DAT: 19` ·
Playwright **8 passed (27,1 s)** · link: **119** tệp · **657** link · `PASS — 0 tham chieu chet` ·
`CONG CUA CONG: DU BANG CHUNG (A1…A6)` · **`⑨ tham chieu tai lieu PASS`**.

### 1.6 **MERGE PR #1** (điều kiện: **`docker-build` xanh** *và* **`ci` xanh** — đã đo)

| Mục | Giá trị |
|---|---|
| Cách merge | **REST API** `PUT /pulls/1/merge` (`merge_method=merge`, không squash — giữ **46** commit của `N1…N18`) |
| Kết quả | `merged=true` · **`fab0089`** · lúc `03:56:58Z` · PR **closed** · **46** commit · **610** tệp (**+258.467 / −4.073**) |
| `origin/main` | **`fab0089`** (trước phiên: `531bd9d`) |
| **Đo SAU merge trên `main`** (điều `D-61` lo nhất) | ✅ `ci` **[`41964732639`](https://github.com/hungnt9801/eCommerce/actions/runs/41964732639)** **SUCCESS** — **10,4 phút, 3/3 job** · ✅ `docker-build` **[`41964732648`](https://github.com/hungnt9801/eCommerce/actions/runs/41964732648)** **SUCCESS** (76 s) ⇒ **`ci` của `main` KHÔNG đỏ** — đúng điều `D-61` đòi hỏi trước khi merge |

### 1.7 Đo lại cuối phiên (sau merge + docs)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ — **không được đỏ**) | **exit 0** — **9/9** bước · **235,2 s** |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0** — **19/19** bước · **436,8 s** (⑲ Playwright **8/8**, 37,9 s) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **36/36 DAT · 0 HONG** (trước phiên **34/34**; **+V4 +V5**) |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HONG** (6,9 s — nằm trong bước ⑧ của cổng nhẹ) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **120** tệp `.md` · **669** link markdown · **436** đường dẫn backtick · **0** chết |
| Mermaid (`npm run check:mermaid` trong `tools/`) | **PASS** — **213** khối · **0** lỗi |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất · **0** test BE thêm |

📌 Bảng trên: **cổng nhẹ** + **link** + **mermaid** chạy **SAU** khi viết xong docs; `-DayDu` (**436,8 s**) và
`test-cong-kiem` (**36/36**) là lượt chạy **ngay sau** `39df2d4` — sau đó phiên này **chỉ** sửa `.md` (không chạm
mã, không chạm `.ps1`) nên **không** chạy lại `-DayDu`: thứ duy nhất docs ảnh hưởng là **link** + **mermaid**, và
cả hai đã đo lại. 🔒 Nói rõ để phiên sau **không** đọc *"19/19"* thành *"đã chạy lại sau mọi thay đổi"*.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc còn lại đều **cắt có quyết định**:

1. ⏭ **`cong-15-buoc` CHƯA bật làm required check** — **điều kiện đã đổi** so với `D-59` ③ (`N16`):
   `ci.yml` bản **có bước FE** nay **đã** nằm trên `main` và **đã đo xanh** (PR #1 + lượt `push` trên `main`).
   ⇒ Việc bật là **một dòng settings của repo**, **không** nằm trong mã ⇒ cần phiên/quyết định riêng
   (**D-63**), ⛔ **không** tự bật trong phiên "commit cây".
2. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).
3. 📌 **39 tệp `tools/_*` vẫn untracked** (rác cổng) — **đúng** ý đồ, không phải bỏ sót.
4. 📌 **Ý nghĩa "ảnh tồn kho" sau fix:** ảnh được chụp **một lần/ngày** ở lượt tổng hợp **đầu tiên có hàng**.
   Nếu **cả ngày** kho thật sự trống thì báo cáo tồn kho hiện *"chưa chụp ảnh"* (**đúng**: *chưa có gì để chụp*),
   không phải `0`. Muốn "ảnh luôn mới nhất trong ngày" thì phải đổi sang **chụp lại mỗi lượt** — việc **chưa làm**
   (xem §5 #2).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration mới | **KHÔNG có** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (ở migration 59 từ `N9`); DB của CI do `DbMigrator` áp từ đầu mỗi run |
| Vì sao không có | Phiên này **chỉ** commit cây có sẵn + sửa **1 hàm** trong `Reporting` (không đổi schema) + 2 tệp `tools/` + 1 tệp `e2e` |
| Cách lùi | `git revert -m 1 fab0089` (lùi **cả** PR #1) · hoặc `git revert 39df2d4` (lùi riêng 2 fix — **không khuyến nghị**: `ci` sẽ đỏ lại ở `e2e-playwright`) |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-62** | **`N18` MERGE PR #1** sau khi **commit cây làm việc** (**160** tệp / 6 commit) — điều kiện merge là **ĐO**: `docker-build` **và** `ci` cùng xanh trên **cùng** head `39df2d4`. 🚨 **2 lỗi chỉ lộ trên cây sạch** được sửa **trước** khi merge: ① mốc ảnh tồn kho **chỉ ghi khi đã có ảnh** (nếu không: DB trống ⇒ báo cáo tồn kho rỗng cả ngày **mà không lỗi nào**); ② **cổng link** bỏ qua `artifacts/` (**bằng chứng ngoài VCS**, đã khai báo) — ⛔ **không** bỏ qua link **trong** repo (ca **V5** canh đúng điều đó). 📌 Nguyên tắc: *"xanh trên máy dev"* **không** phải *"xanh"* khi phép đo phụ thuộc **tệp không commit** |
| **D-63** | **`cong-15-buoc` required: KHÔNG bật trong `N18`** — dù điều kiện `D-59` ③ đã **hết hiệu lực** (`ci.yml` có bước FE **đã** ở `main`, `ci` **đã** xanh trên `main`). Lý do: bật required check là **thay đổi settings của repo** (không nằm trong mã, không review được bằng diff) ⇒ phải là **một phiên riêng có đo TRƯỚC/SAU** (`GET`/`PUT .../protection`), đúng cách `D-59` đã làm. 📌 Hệ quả đang có: `main` chỉ chặn **1** job ⇒ `ci` đỏ **không** chặn merge — đừng đọc *"merge được"* thành *"nên merge"* |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Cổng link chỉ đúng khi chạy trên cây sạch — bài học lặp lại LẦN THỨ HAI** (`D-57` ③ là lần đầu):
   mọi phép đo *"xanh"* trên máy dev đều phải hỏi *"tệp này có trong repo không?"*. `N18` mất **1 lượt CI
   (~10 phút)** để phát hiện 2 lỗi thuộc đúng lớp này.
2. ⚠️ **Ảnh tồn kho = một ảnh/ngày** (sau `D-62` ①). Hệ quả: báo cáo tồn kho **không** phản ánh tồn **lúc mở
   màn hình**, mà tồn ở **lượt tổng hợp đầu tiên trong ngày**. Muốn "mỗi 15 phút một ảnh" phải đổi luật
   (chụp lại mỗi lượt, `DELETE` + `INSERT` theo ngày) — **cần đo chi phí** với 20.000 SKU trước khi đổi.
3. ⚠️ **`ci` trên `main` giờ chạy 3 job (~10–25 phút)**; required check vẫn **chỉ** `docker-build` (**D-63**).
4. ⚠️ **PR #1 đã merge ⇒ nhánh `feature/wp-03-catalog` là lịch sử**: đừng push thêm commit nghiệp vụ vào đó;
   phiên sau tạo nhánh mới từ `main` (`fab0089`).
5. 📌 **`main` mang 610 tệp mới** — người clone lần đầu giờ build được **cả BE + FE + image** (trước phiên này
   thì **không**: `D-57` ③ · `D-58` · `D-61` ②).
6. ⚠️ **`git add -A` vẫn là bẫy:** còn **39** tệp `tools/_*`. Commit theo **đường dẫn cụ thể**.
7. 📌 **Máy dev không có `gh`/`act`**: PR/run/log/artifact đọc bằng **REST API** + credential của git
   (`git credential fill`, user `hungnt9801`). 🔒 **Không** in token ra log. 📌 **`head_sha` của API runs cần
   SHA ĐẦY ĐỦ** — truyền SHA ngắn trả **0 run** (đã mất một lượt chờ vì lỗi này).
8. 📌 **Commit message PHẢI thuần ASCII** — hook `commit-msg` **chặn** (`§` trong message cũng bị chặn; đã
   dính **1** lần, phải viết lại).

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG phạm vi `N18` (commit cây → `ci` xanh trên PR #1 → **merge `fab0089`**); `07` **chưa**
có `§WP-14` ⇒ phiên `N19` **không** mở WP mới — việc còn lại là **bật `cong-15-buoc` required** (cần duyệt,
`D-63`) rồi mới tới WP kế.**

1. 🔑 **Đọc `D-62` + `D-63` + §2 TRƯỚC** khi nghĩ tới việc bật required check.
2. Đo **TRƯỚC** khi bật: `GET /repos/hungnt9801/eCommerce/branches/main/protection` (kỳ vọng: chỉ
   `docker-build`) · run `ci` **trên `main`** của head `fab0089` (**đã xanh** — §7) — *không* bật required cho
   một job **chưa** từng xanh trên `main`.
3. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) · trước khi push `-DayDu`
   (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**36/36**) · `kiem-db.ps1` (**25/25**) ·
   `kiem-link-tai-lieu.ps1` (**PASS**).
4. 🔒 **Nhánh mới từ `main` = `fab0089`** (không dùng lại `feature/wp-03-catalog`).

```powershell
cd D:\Projects\Tools\eCommerce
git fetch origin --prune
git switch main
git pull --ff-only
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
pwsh -File .\tools\kiem-tat-ca.ps1               # 9/9, exit 0
pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu        # 19/19, exit 0
pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc   # 36/36
pwsh -File .\tools\kiem-db.ps1                   # 25/25
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n18.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N19` **không** mở WP mới: việc còn lại là **bật `cong-15-buoc`
làm required check** (đo TRƯỚC bằng `GET .../branches/main/protection`, chỉ bật sau khi thấy `ci` trên
`main` xanh) — đúng phạm vi ghi ở handover §2, không mở rộng. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): vẫn đủ **3 thứ** là *mã phiên
> đích* (`N19`) · *đường dẫn handover* (`handover-n18.md`) · *việc phải làm* (`bật cong-15-buoc required`),
> và **không** mang số đo (luật 6).

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh làm việc | `feature/wp-03-catalog` — **đã merge**, giữ làm lịch sử (tên **không** còn khớp phạm vi: chứa `N1…N18`) |
| `HEAD` local | **`de05492`** (commit **docs** của phiên này; `origin/feature/wp-03-catalog` = `de05492`) |
| **`main`** | **`fab0089`** (merge commit của PR #1) · **rồi** merge commit của **PR #2** (docs) — 📌 `git log -1 --format=%H origin/main` là **nguồn sự thật**, không chép SHA vào tài liệu (mỗi lần merge lại đổi) |
| PR | [**#1**](https://github.com/hungnt9801/eCommerce/pull/1) — **MERGED** · 46 commit · **610** tệp · `+258.467 / −4.073` · [**#2**](https://github.com/hungnt9801/eCommerce/pull/2) — **docs-only** (5 tệp `.md`: handover này + `decisions`/`07`/`08`/`README`), `ci` + `docker-build` xanh trước khi merge |
| Run Actions **trên `main`** (head `fab0089`) | ✅ `ci` **[`41964732639`](https://github.com/hungnt9801/eCommerce/actions/runs/41964732639)** — **SUCCESS, 10,4 phút, 3/3 job** (`openapi-drift` 2m33s · `e2e-playwright` 3m57s · `cong-15-buoc` 10m21s) · ✅ `docker-build` **[`41964732648`](https://github.com/hungnt9801/eCommerce/actions/runs/41964732648)** — **SUCCESS, 76 s** |
| Branch protection `main` | **Không đổi**: required check **`docker-build`** (`strict=false`, `enforce_admins=false`) — **`cong-15-buoc` chưa bật** (`D-63`) |
| Tệp `N18` đã tạo | `docs/nhanh/handover-n18.md` |
| Tệp `N18` đã sửa | `docs/nhanh/README.md` · `docs/nhanh/decisions.md` · `docs/nhanh/07-ke-hoach-trien-khai.md` · `docs/nhanh/08-session-protocol.md` |
| Dữ liệu `N18` đã xoá | **Không** chạm DB (0 migration). Máy dev: log cổng ở **gốc repo** (`gate-n18-daydu.log` · `gate-n18-daydu-2.log`) — `*.log` **đã gitignore**; tệp tạm trong `%TEMP%` (không vào repo) |
