# Handover `N19` — bật `cong-15-buoc` làm **required check** (đo TRƯỚC/SAU, không mở WP mới)

> Ngày 05/11/2026 · nhánh `ops/n19-required-check` (từ `main` = **`0e5c275`**, merge PR #2)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-63](decisions.md) (điều kiện) · **D-65** (phiên này)
> **Mục tiêu phiên:** `07` **chưa có `§WP-14`** ⇒ **không** mở WP mới. Việc còn lại đúng `handover-n18` §2 #1 +
> `D-63`: bật **`cong-15-buoc`** làm required check — **đo TRƯỚC** (`GET …/branches/main/protection`), **chỉ bật sau
> khi thấy `ci` trên `main` xanh**. **Không mở rộng** phạm vi (không bật `openapi-drift`/`e2e-playwright`).

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline 6 lệnh §4 của [08](08-session-protocol.md) — đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **39** mục — **tất cả** `?? tools/_*` (rác cổng: `_*.png`, `_anh-tam/`, `_bang-nhap-nhanh.csv`…). Đúng `handover-n18` §2 #3 · **0** tệp mã/docs đang sửa dở |
| 2 | `git log --oneline -1` · `git branch --show-current` | `main` = **`0e5c275`** (merge PR #2, docs `N18`) · nhánh làm việc mới **`ops/n19-required-check`** (tạo từ `origin/main` = `0e5c2759f487913bf119a9f2c08936e0526ba9be`) |
| 3 | `docker compose up -d` · `docker compose ps` | daemon **đã chạy sẵn** (không phải bật) ⇒ `ecommerce-postgres` + `ecommerce-redis` **Up 5 hours (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 9 warning** (biên dịch **đầy đủ**, 60,3 s) — ⚠️ xem ghi chú dưới |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (6 s) — khớp `handover-n18` §1.1 |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **120** tệp `.md`, **669** link markdown, **436** đường dẫn backtick, **0** chết |

📌 **Ghi chú #4 — số 9 warning KHÔNG phải hồi quy:** `main` (không chạm mã trong phiên này) biên dịch **đầy đủ**
sinh **9** cảnh báo `CS8604` ở **2** module: `Address` (`AddressAppService.cs` 100/120/175 · `AddressDirectory.cs`
74/75/76) và `Sales` (`MarketplaceIntakeAppService.cs` 124/125 · `OrderStatusAppService.cs` 184). Ngay sau đó
`dotnet build` **lần 2** trả **0 warning / 0 error** (14,97 s) vì **không biên dịch lại** (incremental) — tức con số
*"0/0"* ở các handover trước là **đầu ra của build không compile lại**, không phải *"mã sạch tuyệt đối"*.
⛔ Phiên này **không** sửa mã nên **không** đụng 9 cảnh báo đó. 🔒 Từ nay khi ghi baseline build phải ghi rõ
*"full"* hay *"incremental"*.

### 1.2 Đo **TRƯỚC** khi bật — `GET /repos/hungnt9801/eCommerce/branches/main/protection`

| Trường | Giá trị đo được |
|---|---|
| HTTP | **200** (khác `N16`: lúc đó **404**, `main` chưa có protection) |
| `required_status_checks.contexts` | **`["docker-build"]`** — đúng `handover-n18` §7 |
| `required_status_checks.checks` | `[{ context: docker-build, app_id: 15368 }]` (app = GitHub Actions) |
| `strict` | **false** (không buộc PR rebase `main`) |
| `enforce_admins` | **false** |
| `allow_force_pushes` · `allow_deletions` · `required_linear_history` · `required_conversation_resolution` | **false** · **false** · **false** · **false** |
| `required_pull_request_reviews` · `restrictions` | **không có** (không bắt review, không giới hạn người push) |
| `GET /branches/main` | `protected = True` |

### 1.3 Điều kiện *"`ci` trên `main` xanh"* — đo trên **đúng head** `0e5c275`

| Mục | Số đo |
|---|---|
| Workflow `ci` trên `main` | run [**`41969985282`**](https://github.com/hungnt9801/eCommerce/actions/runs/41969985282) — **SUCCESS**, head `0e5c275`, event `push` (merge PR #2) |
| 3 job | `cong-15-buoc` **success** (04:47:39 → 04:56:50) · `openapi-drift` **success** · `e2e-playwright` **success** |
| Check-run của head `0e5c275` (4 cái) | `e2e-playwright` · `openapi-drift` · **`cong-15-buoc`** · `docker-build` — **tất cả success**, `app_id` **15368** |
| Workflow `docker-build` cùng head | run [`41969985267`](https://github.com/hungnt9801/eCommerce/actions/runs/41969985267) — **SUCCESS** |

🔑 Đây là điều kiện của `handover-n18` §6 #2: **`ci` đã xanh trên `main`** (không phải chỉ trên PR) ⇒ đủ điều kiện
bật. 📌 `cong-15-buoc` đã có **lần chạy xanh thật** trên `main` ở **cả** `fab0089` (run `41964732639`) **và**
`0e5c275` — tức **2** lượt, không phải suy đoán.

### 1.4 Bật — `PUT /branches/main/protection`

| Mục | Giá trị |
|---|---|
| Cách làm | `PUT` **cả object** (API này **ghi đè toàn bộ**, không phải PATCH) — body mang **đủ 11 cờ** đang có, chỉ thêm **1** context |
| `required_status_checks` | `strict=false`, `checks` = `[{docker-build, 15368}, {cong-15-buoc, 15368}]` |
| Các cờ còn lại | giữ **nguyên** `enforce_admins=false` · `required_pull_request_reviews=null` · `restrictions=null` · `allow_force_pushes=false` · `allow_deletions=false` · `required_linear_history=false` · `required_conversation_resolution=false` · `lock_branch=false` · `allow_fork_syncing=false` |
| HTTP | **200** |

### 1.5 Đo **SAU** — `GET …/protection` (cùng phiên)

| Trường | Giá trị |
|---|---|
| `contexts` | **`["docker-build", "cong-15-buoc"]`** |
| `checks` | `[{docker-build, 15368}, {cong-15-buoc, 15368}]` — cùng `app_id` (nếu `app_id` **null** thì check **không** gắn với Actions) |
| `strict` · `enforce_admins` | **false** · **false** (không đổi) |
| cờ còn lại | **false**/**null** y như §1.2 ⇒ **không nuốt** cấu hình nào |
| `GET /branches/main` | `protected = True` |

📌 Bằng chứng JSON đầy đủ của lượt SAU: `%TEMP%\n19-protection-after.json` (*ngoài repo* — không commit).

### 1.6 Bằng chứng **cổng có hiệu lực** — PR [#3](https://github.com/hungnt9801/eCommerce/pull/3)

PR này **docs-only** (5 tệp `.md`: handover + `decisions`/`07`/`08`/`README`), mở từ chính nhánh của phiên.
Đo **TRƯỚC khi merge**: `GET /pulls/3` ⇒ `mergeable_state = **blocked**`, danh sách check gồm **cả**
`cong-15-buoc` **và** `docker-build` (2 required) — 🔑 đây là phép đo *"cổng **thật sự** chặn merge"*, thứ mà
`D-63` muốn thấy trước khi tin. Sau khi **cả 2** check xanh mới merge (**đúng luật**: ⛔ không merge khi check
còn `pending`/`failure`).

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc còn lại đều **cắt có quyết định**:

1. ⏭ **`D-64` — `probe-giu-cho-2instance` FLAKY: chưa sửa** (ngoài phạm vi phiên; cần **chọn** giữa *retry có
   giới hạn* và *chấp nhận `409`*). 🚨 **Hệ quả đổi mức nghiêm trọng từ phiên này:** trước `N19` job này đỏ chỉ
   *"không ai đọc"*; **nay nó là required check** ⇒ flaky **chặn merge PR thật**. Nhận chủ: phiên `WP-05/06`
   hoặc phiên có `§WP-14`.
2. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).
3. ⏭ **`openapi-drift` + `e2e-playwright` vẫn KHÔNG required** — `N19` **không** bật (ngoài phạm vi: chỉ làm đúng
   1 việc `D-63`). Hệ quả: 2 job đó đỏ **vẫn merge được** — muốn đổi phải là phiên riêng có `§WP-14`/
   quyết định mới đo cả 2 trên `main`.
4. 📌 **39 tệp `tools/_*` vẫn untracked** — **đúng** ý đồ (rác cổng), không phải bỏ sót.
5. 📌 **`strict=false` giữ nguyên** ⇒ PR cũ **không** phải rebase `main` trước khi merge; check phải xanh trên
   **head của PR**, không phải trên `main` mới nhất. Muốn `strict` thì phải **quyết định riêng** (mọi PR sẽ
   phải cập nhật nhánh) — ⛔ không tự đổi.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration mới | **KHÔNG có** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB đã áp | **Không** chạm DB (phiên chỉ đổi settings GitHub + docs) |
| Vì sao không có | Không sửa mã, không đổi schema — **0** tệp `.cs`/`.ps1`/`.ts` bị sửa |
| Cách lùi | Cấu hình: `PUT …/protection` với `checks = [docker-build]` (giữ đủ 11 cờ như §1.2) ⇒ cổng về như trước · Docs: `git revert <merge commit của PR #3>` |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-65** | **Bật `cong-15-buoc` required (`N19`) — chỉ sau khi ĐO thấy `ci` xanh trên `main`; `PUT` phải mang ĐỦ object; giữ `strict=false` + `enforce_admins=false`; chỉ bật đúng 1 job** · TRƯỚC: `contexts=["docker-build"]` (`GET` 200, `app_id` 15368) · điều kiện: `ci` run **`41969985282`** SUCCESS trên head `0e5c275` (**3/3** job, có `cong-15-buoc`) · SAU: `contexts=["docker-build","cong-15-buoc"]`, 10 cờ còn lại **không đổi** · chứng minh hiệu lực: PR #3 `mergeable_state=blocked` trước khi 2 check xanh · ⚠️ **hệ quả:** `D-64` flaky **giờ chặn merge** ⇒ phải xử lý trước khi tin cổng tuyệt đối · 📌 API này **ghi đè toàn bộ** ⇒ body thiếu cờ = **mất** cờ (bài học giữ nguyên từ `D-59` ②) |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`D-64` flaky + required check = đỏ ngẫu nhiên chặn merge.** Đo được: cùng cây mã, `17/17` vs `14/3`
   (`P12` `200/409` · `P13` **1** dòng · `P14` **1** exception). Khi thấy đúng 3 ca đó: **kiểm tra mã có đổi
   không** rồi mới kết luận; ⛔ **không** nới probe, ⛔ **không** tắt required check để "merge cho xong"
   (tắt = quay lại đúng `D-63`). Cách xử lý đúng: chọn hướng **retry có giới hạn** hoặc **chấp nhận `409`** +
   sửa kỳ vọng probe, rồi đo lại **tỉ lệ** trên nhiều lượt.
2. ⚠️ **`PUT …/protection` là API ghi đè** — lần sau đổi bất cứ cờ nào: `GET` → sửa **đúng 1** trường → `PUT`
   **cả object** → `GET` lại so **từng** trường. Body **thiếu** `required_pull_request_reviews`/`restrictions`
   ⇒ chúng thành `null` **im lặng**.
3. ⚠️ **`cong-15-buoc` chạy ~10 phút/PR** — từ nay PR phải chờ nó; đừng "merge sớm rồi sửa" (cổng sẽ chặn, và
   bypass bằng admin là **phá** chính thứ phiên này dựng).
4. 📌 **Máy dev không có `gh`/`act`**: dùng `git credential fill` (`hungnt9801`) + `curl`/`Invoke-RestMethod`.
   🔒 **Không in token**. 📌 `head_sha` của API runs cần **SHA ĐẦY ĐỦ** (SHA ngắn ⇒ **0** run).
5. 📌 **Commit message phải thuần ASCII** (hook `commit-msg` chặn cả `§`).
6. 📌 **Baseline build**: ghi rõ **full** (0 error / **9** CS8604 có sẵn) hay **incremental** (0/0). Đừng ghi
   *"0/0"* mà không nói loại — đó là chỗ `N18` ghi lệch.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG phạm vi `N19` (bật `cong-15-buoc` required + đo TRƯỚC/SAU + merge docs); `07` **chưa** có `§WP-14` ⇒ phiên `N20` **không** mở WP mới — việc còn lại là **chốt hướng xử lý `D-64`** (flaky) trước khi coi cổng là đáng tin.**

1. 🔑 Đọc **`D-64`** + **`D-65`** + §2 **trước** khi làm gì: `D-64` giờ **chặn merge**, không còn là "đỏ không ai đọc".
2. Kiểm **cấu hình còn đúng**: `GET /repos/hungnt9801/eCommerce/branches/main/protection` ⇒ **2** context
   (`docker-build`, `cong-15-buoc`), `strict=false`, `enforce_admins=false`; và `ci` trên `main` **xanh** ở head mới nhất.
3. Nếu có `§WP-14` ⇒ làm đúng WP đó (nhánh mới từ `main`, **không** dùng lại `ops/n19-required-check`).
   Nếu **chưa** có: **không** tự đặt WP — chỉ đóng nợ `D-64` theo hướng đã chốt.
4. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) · trước khi push `-DayDu`
   (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**36/36**) · `kiem-db.ps1` (**25/25**) · `kiem-link-tai-lieu.ps1` (**PASS**).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n19.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N20` **không** mở WP mới: việc còn lại đúng phạm vi ghi ở
handover §2 — chốt hướng xử lý `D-64` (flaky `probe-giu-cho-2instance`) và giữ 2 required check đúng
cấu hình đã đo. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): đủ **3 thứ** là *mã phiên đích*
> (`N20`) · *đường dẫn handover* (`handover-n19.md`) · *việc phải làm* (`D-64` + giữ cổng), và **không** mang số đo.

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh làm việc | **`ops/n19-required-check`** (tạo từ `main` = `0e5c275`); `feature/wp-03-catalog` **là lịch sử**, không dùng lại |
| `HEAD` local | commit docs của phiên (`git log -1 --format=%h`) — 📌 không chép SHA vào tài liệu, mỗi lần thêm docs lại đổi |
| **`main` trước phiên** | **`0e5c275`** (merge PR #2 — docs `N18`) · `git log -1 --format=%H origin/main` là **nguồn sự thật** |
| **`main` sau phiên** | merge commit của **PR #3** (docs-only) — xem `git log --oneline -3 origin/main` |
| PR | [**#3**](https://github.com/hungnt9801/eCommerce/pull/3) — docs-only, **`ci` + `docker-build` xanh trước khi merge** (đo `mergeable_state=blocked` khi còn pending) |
| Branch protection `main` | required: **`docker-build`** · **`cong-15-buoc`** (`strict=false`, `enforce_admins=false`) — `D-65` |
| Tệp `N19` đã tạo | `docs/nhanh/handover-n19.md` |
| Tệp `N19` đã sửa | `docs/nhanh/decisions.md` · `docs/nhanh/07-ke-hoach-trien-khai.md` · `docs/nhanh/08-session-protocol.md` · `docs/nhanh/README.md` |
| Dữ liệu `N19` đã xoá | **Không** chạm DB (0 migration) · bằng chứng protection ở `%TEMP%` (ngoài repo) · **0** tệp `tools/_*` mới |
