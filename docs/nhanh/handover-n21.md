# Handover `N21` — `D-67`: **`C10` đo QUÁ SỚM**, chứng minh bằng **THỨ TỰ COMMIT**; chẩn đoán tự in bằng chứng

> Ngày 06/11/2026 · nhánh `ops/n21-d67-bang-chung` (từ `main` = **`f858b10`**, merge PR #6)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-67](decisions.md) (nợ) · **D-68** (phiên này) · Trước: [handover-n20](handover-n20.md)
> **Mục tiêu phiên:** `07` **chưa có `§WP-14`** ⇒ **không** mở WP mới. Việc còn lại đúng `handover-n20` §2 #6 +
> §6 #3: **thu bằng chứng cho `D-67`** (`probe-don-san` `C10` flaky) và **giữ 2 required check đúng cấu hình đã đo**.
> **Không mở rộng** phạm vi.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline 6 lệnh §4 của [08](08-session-protocol.md) — đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **39** mục — **tất cả** `?? tools/_*` (rác cổng). **0** tệp mã/docs đang sửa dở ⇒ khớp `handover-n20` §1.1 #1 |
| 2 | `git log --oneline -1` · `git branch --show-current` | `main` = **`f858b10`** (*Merge pull request #6* — docs `D-67`) · HEAD đầy đủ **`f858b104c168218208f96051068fd6e96ea2226e`** = `origin/main` · nhánh làm việc mới **`ops/n21-d67-bang-chung`** (tạo từ `main`). 📌 `handover-n20` §7 ghi `main` = `b1955989`; giữa hai phiên đã merge **PR #6** (`0f8b925` — **chỉ docs**, ghi nợ `D-67`) ⇒ **không** phải nợ mới |
| 3 | `docker compose up -d` · `docker compose ps` | daemon **đã chạy sẵn** (`up -d` chỉ in *Running*) ⇒ `ecommerce-postgres` **Up 27 hours (healthy)** · `ecommerce-redis` **Up 27 hours (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **full**: **0 error / 9 warning** `CS8604` có sẵn (`Address` 6 · `Sales` 3), **58,9 s** — khớp `handover-n20` §1.1 #4 ⇒ **không** hồi quy |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (6 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **122** tệp `.md` · **690** link markdown · **456** đường dẫn backtick · **0** chết |

📌 Ghi rõ **full**/*incremental* như luật `handover-n19` §5 #6 (build full = **9** `CS8604`, incremental = 0/0).

### 1.2 Đo TRƯỚC — required check + `ci` trên `main` (KHÔNG sửa gì)

| Mục | Giá trị đo được |
|---|---|
| `GET /repos/hungnt9801/eCommerce/branches/main/protection` | **200** · `contexts = ["docker-build","cong-15-buoc"]` · `checks` = 2 mục, `app_id` **15368** (GitHub Actions) |
| `strict` · `enforce_admins` | **false** · **false** |
| `allow_force_pushes` · `allow_deletions` · `required_linear_history` · `required_conversation_resolution` · `lock_branch` · `allow_fork_syncing` | **false** × 6 |
| `required_pull_request_reviews` · `restrictions` | **null** (không bắt review, không giới hạn người push) |
| `ci` trên `main` (head `f858b10`, event `push`) | run **`42013066249`** **SUCCESS** · attempt **1** · `docker-build` **`42013066257`** **success** |
| `ci` trên `main` (head `f858b10`, event **`schedule`**) | run **`42126116769`** **SUCCESS** · attempt **1** · job `cong-15-buoc` **665 s** · `openapi-drift` **117 s** · `e2e-playwright` **230 s** |
| check-run của head `f858b10` | **7/7 success** (2 lượt × 3 check + `docker-build`), `app_id` 15368 |

⇒ Khớp **từng trường** với `D-65` ④ / `handover-n20` §1.2 ⇒ **cấu hình 2 required check vẫn đúng như đã đo**.
⛔ Phiên này **không** `PUT` gì (không có lý do đổi), **không** bật thêm job nào.

### 1.3 Bằng chứng `D-67` đọc **trực tiếp** từ artifact của lượt ĐỎ (không suy đoán)

`D-67` ③ ghi *"cần log Host của lượt ĐỎ — mà artifact không chứa log Host"*. Hoá ra **một phần** sai:

| Mục | Đo được |
|---|---|
| Run có lượt ĐỎ | **`42005307510`** (head `b1955989`, **cùng cây mã** với PR xanh: `git rev-parse …^{tree}` = **`a64d69a3`**) — attempt **1 ĐỎ** · **2 ĐỎ** · **3 XANH** |
| Artifact của 2 lượt ĐỎ **vẫn còn** | **`13404468797`** (attempt 1) · **`13405821894`** (attempt 2) — **31,5 KB** (không có `Evidence.json`/`link.log` ⇒ cổng chết **giữa** đường); lượt xanh attempt 3 = **`13406741779`** (**48,5 KB**) |
| Tệp **có** trong artifact ĐỎ | **`tools/_tam-kiem/donsan.log`** (4.661 B / 4.663 B) — 🔑 **có** nguyên output probe, kể cả dòng `Log Host: …/tools/_tam-donsan/host.log`. **Thiếu** đúng **`host.log`** (nằm ở `tools/_tam-donsan/`, ngoài glob artifact) |
| Nội dung lượt ĐỎ | **`PROBE DON SAN: 48/49 DAT`** · **chỉ** `C10` sai: `tong = 0 · Late = 0`. **`C11`** (`watermark` **không** đổi) · **`C12`** (0 đơn nội bộ) · **`C13`** (**DLQ `LyDo = HaWatermark` = 1**) · **`C14`** (payload > 100 ký tự) · `C20…C49` **đều DAT** |
| Nội dung lượt XANH (cùng cây, 3 artifact) | `C10` = **`1/1`** · bước ⑭ **23,8 s** / **28,3 s** / **28,6 s** · `PROBE DON SAN: 49/49` |
| Hệ quả | Sự kiện **không** mất và **không** bị nuốt: **đã vào DLQ** ⇒ chỉ **thời điểm đo** khác nhau giữa 2 lượt |

⚠️ **Bài học đo được (đính chính `D-67` ③ một phần):** artifact **vẫn có** output probe (`donsan.log`) nhưng **KHÔNG có**
log Host; và dòng `C10` cũ **chỉ in 2 con số** (`tong`, `Late`) nên **không phân biệt** được *"handler chưa chạy"* với
*"chạy mà dòng không vào DB"* — đó là lý do lượt ĐỎ **vô dụng** dù log có được giữ.

### 1.4 🔑 Chứng minh **THỨ TỰ COMMIT** ⇒ `C10` đo **SỚM**, không phải "ghi hỏng"

Đọc mã (không suy đoán) — 3 mắt xích, mỗi mắt xích có dòng cụ thể:

| # | Mắt xích | Dòng |
|---|---|---|
| ① | Nhánh `DenMuon` gọi **DLQ TRƯỚC**, rồi **mới** ghi nhật ký sự kiện `Late` | `MarketplaceOrderIntakeHandler` (`DayVaoDlqAsync(… HaWatermark …)` → `_eventLog.GhiAsync(… Late …)`) |
| ② | `DeadLetterWriter.DayVaoDlqAsync` ghi **trong UoW của NƠI GỌI** (không mở UoW riêng — chỉ `DayVaoDlqUoWRiengAsync` mới mở) | `DeadLetterWriter` dòng **62–73** |
| ③ | `MarketplaceEventLogWriter` mở **UoW RIÊNG `requiresNew`** và **commit NGAY** (`uow.CompleteAsync` trong `ChenTrongUoWRiengAsync`) | `MarketplaceEventLogWriter` dòng **172–185** |

⇒ Dòng DLQ **chỉ** commit ở `uow.CompleteAsync()` **của handler** — tức **SAU** khi dòng nhật ký `Late` đã commit xong
(transaction riêng, không thể rollback theo). **Hệ quả logic:** `DLQ = 1` ⇒ dòng `Late` **ĐÃ** có trong DB.
Vì vậy `C13 = 1` mà `C10 = 0` **không thể** là "ghi hỏng" — nó nghĩa là **dòng đến sau hạn 30 s** của `C10`.

📌 Đây là **kết luận có chứng minh**, không phải giả thuyết: `D-67` ③ (giả thuyết *"writer ghi trong UoW riêng + dedupe
⇒ có thể chưa kịp"*) đúng phần **UoW riêng** nhưng **sai** phần kết luận — chính vì UoW riêng **commit trước** DLQ.

### 1.5 Đã thêm: **chẩn đoán tự in bằng chứng** (⛔ KHÔNG đổi điều kiện ĐẠT/ĐỎ của ca nào)

| Tệp | Thay đổi | Vì sao **không** phải "nới cổng" |
|---|---|---|
| `tools/probe-don-san.ps1` | hàm **thuần** `Dem-NeoLogHost` (đếm dòng log Host **theo 1 đơn**) · `C10` in thêm **`choDu`** + **3** số neo (`[MP-EVENTLOG-DA-GHI]` / `-GHI-HONG` / `-TRUNG`) · **1** dòng `[CD-D67]` **đo lại `Late` SAU khi DLQ đã có** + 4 số neo · khi `C10` đỏ ⇒ in **thẳng** ≤ 20 dòng log Host của đơn đó vào output probe | Khẳng định **giữ nguyên** `$tongA -eq 1 -and $lateA -eq 1`; hạn chờ **giữ nguyên 30 s**; **không** thêm/bớt ca (vẫn **49** ca) — chỉ **in thêm số** |
| `tools/kiem-tat-ca.ps1` (bước ⑭) | lọc hiển thị thêm `CD-D67`, `-First 44` → **46** | Chỉ đổi **dòng in ra `gate.log`**, không đổi điều kiện |
| `tools/test-cong-kiem.ps1` | ca mới **`Y8`** + hàm thuần `Tim-ViPhamChanDoanC10` — **4 chiều xuôi** + **4 chiều ngược** | Chống *"sửa cho xanh"*: nới 30 → 99 · xoá khẳng định · bỏ điều kiện `Outcome = Late` · xoá neo chẩn đoán ⇒ phép quét **PHẢI** đỏ |

| Số đo | Giá trị |
|---|---|
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **38/38 DAT · 0 HONG** (trước phiên: **37/37**) |
| `pwsh -File .\tools\probe-don-san.ps1` (sau khi sửa) | **exit 0 — 49/49 DAT** · `C10 … choDu = True · GHI = 1 · GHI-HONG = 0 · TRUNG = 0` · `[CD-D67] … Late = 1` |
| Migration · tệp `.cs` | **0** (59 vẫn mới nhất) · **0** |

### 1.6 Kiểm chẩn đoán chạy **THẬT** trên ca ĐỎ (bản giả lập, **không** commit)

| Mục | Số đo |
|---|---|
| Cách làm | bản sao `tools/_n21-probe-c10-do.ps1` (gitignore `tools/_*`) — ép `C10` đo **sớm**: đổi hạn chờ xuống **2 s** + ghim `$lateA = 0` để mô phỏng đúng ảnh chụp trên CI. 📌 **Tệp giả lập đã xoá** sau lượt đo |
| Dòng `C10` in ra | `KHONG DAT … tong = 0 · Late = 0 · **choDu = False** · log Host: GHI = 0 · GHI-HONG = 0 · TRUNG = 0` ⇒ **đúng** dòng của lượt ĐỎ trên CI, **cộng** phần chẩn đoán |
| Dòng `[CD-D67]` in ra | `don POC-K9S6-A-…: sau khi DLQ da co ⇒ **tong = 1 · Late = 1** · log Host: GHI = 1 · GHI-HONG = 0 · TRUNG = 0 · THATBAI-HONG = 0` ⇒ 🔑 **cùng một lượt chạy**: `C10` đỏ ở mốc 2 s nhưng dòng `Late` **có thật** ngay sau đó ⇒ chứng minh **trực tiếp** kết luận §1.4 |
| Khối chi tiết khi đỏ | `[CD-D67 chi tiet] log Host cua don …` in được (rỗng ở lượt này vì lúc đo writer **chưa** chạy ⇒ đúng nghĩa *"handler chưa tới"*) |

### 1.7 Đo **tỉ lệ** (máy dev Windows, cùng cây mã)

| Lượt | Kết quả |
|---|---|
| **10 lượt `probe-don-san`** (bản **đã** sửa; log `tools/_tam-kiem/n21/don-san-lan-1…10.log`) | **49/49 DAT × 10** · **10 XANH / 0 ĐỎ** · `choDu = True` **10/10** · `[CD-D67] Late = 1` **10/10** · thời gian **29,9 – 34,4 s** (trung bình ≈ **32,6 s**) ⇒ ⚠️ **máy dev KHÔNG tái hiện được flake** (khớp `handover-n20` §1.7) |
| **Tỉ lệ trên CI (đếm từ artifact + run thật)** | **2 ĐỎ / 7 lượt** trên cùng cây mã (`C10`): `42005307510` attempt 1 **ĐỎ** · attempt 2 **ĐỎ** · attempt 3 **XANH** · `42013066249` **XANH** · `42126116769` **XANH** · PR #7 `42168084408` **XANH attempt 1** · `main` sau merge `42169269729` **XANH attempt 1** |
| 🎯 **Xác nhận chẩn đoán chạy trên CI** (run `42168084408`, artifact `13480536321`) | `donsan.log` + **`gate.log`** đều có dòng mới: `C10 … DAT — tong = 1 · Late = 1 · **choDu = True** · log Host: GHI = 1 · GHI-HONG = 0 · TRUNG = 0` và `[CD-D67] … sau khi DLQ da co ⇒ **tong = 1 · Late = 1**` ⇒ 🔑 lượt ĐỎ **tương lai** sẽ in **đúng cặp số** phân biệt *thời điểm* ↔ *lỗi ghi*; cổng **19/19**, bước ⑭ **28,9 s** |
| Vì sao máy dev không tái hiện | ⑭ trên máy dev **28–34 s** cho **cả** probe (gồm bật Host); trên CI bước ⑭ **23,8–28,9 s** ⇒ **không** phải "CI chậm toàn cục"; khe *dispatch sự kiện → commit* phải **> 30 s** mới làm `C10` đỏ ⇒ cần log Host **có mốc thời gian** của lượt ĐỎ ⇒ nay **in được** (§1.5) |

### 1.8 Đo lại cuối phiên (sau khi sửa; máy dev Windows)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ) | **exit 0 — 9/9 bước, 193,2 s** (bước ⑨ *test cổng* **101,6 s** · ⑧ DB **7,1 s** · ⑤ mermaid **4,6 s**) |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0 — 19/19 bước, 458,3 s** (⑭ đơn sàn **28,0 s** · ⑮ oversell **11/0**, 15,6 s · ⑯ outbox **9/0**, 22,7 s · ⑰ outbox 2 instance **9/0**, 22,9 s · ⑱ giữ chỗ 2 instance **17/0**, 19,5 s · ⑲ Playwright **8 passed**, 40,4 s) · dòng chẩn đoán mới **có** trong `gate.log`: `[CD-D67] … Late = 1` |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **38/38 DAT · 0 HONG** |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HONG** (exit 0) — `CusCustomers` 6 dòng, bất biến **①c** (mọi khách có chứng từ) vẫn đúng |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **123** tệp `.md` · **700** link · **466** backtick · **0** chết (đo **sau** khi thêm `handover-n21.md` + sửa 3 tài liệu) |
| `npm --prefix tools run check:mermaid` | **PASS** — **213** khối · **0** lỗi |
| Migration | **0** migration mới (**59** vẫn mới nhất) · **0** test BE thêm (1961 unit + 96 integration, đo ở bước ③/④ của cổng) |

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc còn lại đều **cắt có quyết định**:

1. 🚨 **`D-67` VẪN MỞ** — phiên này **chẩn đoán**, **không sửa `C10`**. ⛔ Không nới `Cho-DieuKien` 30 s, không bỏ ca
   (đúng `D-67` ④). **Nhận chủ:** phiên có `§WP-14`. 📌 Hai hướng, **phải CHỌN** ở phiên đó:
   (a) đổi thứ tự **ĐO** (chờ **DLQ** — mốc commit **cuối** của nhánh `Late` — rồi mới đo `Late`; dựa trên chứng minh
   §1.4, **không** phải kéo dài thời gian chờ); (b) giữ nguyên `C10` và tìm **vì sao** dispatch in-process trên CI
   có thể trễ > 30 s (cần log Host của lượt ĐỎ — nay **thu được**).
   ⚠️ Hướng (a) chỉ đúng khi thứ tự *DLQ trước, nhật ký sau* **không** đổi ⇒ muốn chọn (a) phải kèm **neo** ở domain
   + ca `Y8` (đã có) để đổi thứ tự là **cổng đỏ**.
2. ⏭ **`409` vẫn `code = null`** — nguyên trạng `D-66` ⑦; **nhận chủ:** phiên có `§WP-14`.
3. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).
4. ⏭ **`openapi-drift` + `e2e-playwright` vẫn KHÔNG required** — `D-65` ⑤ giữ nguyên (ngoài phạm vi phiên).
5. 📌 **39 tệp `tools/_*` vẫn untracked** + `tools/_n21-do-ti-le.ps1` (script đo tỉ lệ của phiên này) + `tools/_tam-kiem/n21/` (10 log) **đúng ý đồ** (gitignore).
6. 📌 **Máy dev không tái hiện flake** (§1.7) ⇒ đừng kết luận *"hết flaky"*; chỉ số **2 ĐỎ / 5** trên CI mới là tỉ lệ thật.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration mới | **KHÔNG có** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB đã áp | **Không** đổi schema. 📌 Có **chạm dữ liệu dev** qua **12** lượt probe (seed + dọn `POC-K9S6-*`/shops `K9S6-*` của chính probe; `C48/C49` xác nhận **0** dòng còn lại + cờ admin **bật lại `true`**) |
| Vì sao không có | Chỉ đổi **probe + cổng + test cổng + docs**: **0** tệp `.cs`, **0** DTO/API |
| Cách lùi | `git revert <merge commit của PR phiên này>` ⇒ probe về bản `N20` (chỉ in `tong/Late`, **không** có `CD-D67`) + xoá ca `Y8`; **không** cần lùi DB |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-68** | **`D-67` chẩn đoán: `C10` đo QUÁ SỚM, không phải mất dữ liệu — chứng minh bằng THỨ TỰ COMMIT** · artifact lượt ĐỎ **có** `donsan.log` (48/49 DAT: `C11…C14` DAT, `C13` DLQ = 1) nhưng **không** có log Host · 🔑 DLQ ghi **UoW của handler** (commit **cuối**), nhật ký `Late` ghi **UoW riêng `requiresNew`** (commit **trước**) ⇒ `DLQ = 1` ⇒ dòng `Late` **đã** có ⇒ `C10 = 0` là **30 s hết hạn** · thêm chẩn đoán **chỉ in** (`choDu` + 3 số neo · dòng `[CD-D67]` đo lại `Late` sau DLQ · in ≤ 20 dòng log Host khi đỏ) · ca **`Y8`** chống nới (**4 xuôi + 4 ngược**; test cổng **37/37 → 38/38**) · ⛔ **không** nới hạn, **không** bỏ ca; sửa `C10` thuộc phiên có `§WP-14` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`D-67` vẫn chặn merge**: `cong-15-buoc` **là required check** ⇒ `C10` flaky (**2/5** lượt đo được) làm **PR thật
   bị `blocked`**. 📌 Đừng "sửa" bằng nới hạn: ca `Y8` sẽ **ĐỎ** (cố ý).
2. 🚨 **Đọc đúng cặp số khi gặp lại `C10` đỏ**: `[CD-D67] … Late = 1` ⇒ **chỉ** là thời điểm (rerun/không nợ mới);
   `Late = 0` **mà** `DLQ = 1` ⇒ **mâu thuẫn thứ tự commit** = lỗi **GHI thật** ⇒ mở nợ mới **ngay**, kèm artifact.
3. ⚠️ **`PUT …/protection` ghi đè TOÀN BỘ object** (`D-65` ③) — phiên này **không** `PUT`; lần sau đổi bất cứ cờ nào
   phải `GET` → sửa **đúng 1** trường → `PUT` **cả object** → `GET` lại so **từng** trường.
4. 📌 **Máy dev không có `gh`/`act`**: token qua `git credential fill` (`hungnt9801`), 🔒 **không in token**.
   📌 `head_sha` của API runs cần **SHA ĐẦY ĐỦ**.
5. 📌 **Commit message phải thuần ASCII** (hook `commit-msg` chặn cả `§`).
6. 📌 **Baseline build**: ghi rõ **full** (0 error / **9** `CS8604` có sẵn) hay **incremental** (0/0).
7. 🚨 **Cổng nhẹ bước ⑦ có thể báo drift GIẢ trên checkout Windows MỚI (`CRLF`)** — nợ **có sẵn** từ `N19` §1.7 #1.
   ✅ Phiên này **không** gặp (nhánh **không** checkout lại cây FE). Nếu gặp: chạy `pnpm gen:api` một lần rồi chạy lại;
   ⛔ **không** nới phép kiểm.
8. 📌 **Đừng `rerun` rồi `push`** — `concurrency.cancel-in-progress` của `pull_request` **huỷ** lượt đang chạy (`N19` §1.6).
9. 📌 **Artifact ĐỎ vẫn có `tools/_tam-kiem/donsan.log`** — `D-67` ③ nói *"artifact không chứa log Host"* là **đúng**,
   nhưng **sai** nếu suy ra *"không có bằng chứng gì"*. 📌 Đừng đóng `D-67` chỉ vì *"rereun đã xanh"*.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG phạm vi `N21`** — `D-67` **đã có chẩn đoán** (`C10` đo **quá sớm**, chứng minh bằng thứ tự
commit; chẩn đoán tự in bằng chứng + ca `Y8` chống nới), **`D-67` vẫn MỞ** (chưa sửa `C10` — thuộc phiên có `§WP-14`);
2 required check **giữ nguyên** cấu hình đã đo (`docker-build`, `cong-15-buoc`; `strict=false`, `enforce_admins=false`).
`07` **chưa** có `§WP-14` ⇒ phiên `N22` **không** mở WP mới.

1. 🔑 Đọc **`D-68`** + **`D-67`** + **§2** **trước** khi làm gì: `D-67` **mở**; muốn đóng phải **chọn** hướng (a)/(b) ở §2 #1.
2. Kiểm **cấu hình còn đúng** (không `PUT` nếu không có quyết định mới):
   `GET /repos/hungnt9801/eCommerce/branches/main/protection` ⇒ **2** context (`docker-build`, `cong-15-buoc`),
   `strict=false`, `enforce_admins=false`; và `ci` trên `main` **xanh** ở head mới nhất.
3. Nếu có `§WP-14` ⇒ làm đúng WP đó (nhánh mới từ `main`). Nếu **chưa** có: **không** tự đặt WP; chỉ tiếp tục
   **thu bằng chứng** `D-67` (đọc cặp số `Late` sau DLQ trong artifact) và **không** nới hạn 30 s.
4. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) · trước khi push `-DayDu`
   (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**38/38**) · `kiem-db.ps1` (**25/25**) · `kiem-link-tai-lieu.ps1` (**PASS**).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n21.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N22` **không** mở WP mới: việc còn lại đúng phạm vi ghi ở
handover §2 — tiếp tục thu bằng chứng cho `D-67` (đọc cặp số `Late` sau DLQ trong artifact của lượt ĐỎ)
và giữ 2 required check đúng cấu hình đã đo. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): đủ **3 thứ** là *mã phiên đích*
> (`N22`) · *đường dẫn handover* (`handover-n21.md`) · *việc phải làm* (`D-67` + giữ cổng), **không** mang số đo.

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh làm việc | **`ops/n21-d67-bang-chung`** (tạo từ `main` = `f858b10`) |
| `main` trước phiên | **`f858b10`** (*Merge pull request #6* — docs `D-67`); `git log -1 --format=%H origin/main` là **nguồn sự thật** |
| Commit của phiên | **`928557b`** (tools: **3** tệp · +147/−4 — chẩn đoán, **0** đổi điều kiện) + **`3ac6e2e`** (docs: **5** tệp · +228/−1) — 📌 chỉ ghi SHA **ngắn**; nguồn sự thật là `git log -1 --format=%H` |
| PR | [**#7**](https://github.com/hungnt9801/eCommerce/pull/7) — `ops/n21-d67-bang-chung` → `main` (probe + cổng + test cổng + docs; **0** migration); `mergeable_state = blocked` khi 2 required check còn **pending** → **`clean`** sau khi cả 2 **success** (đúng `D-65` ⑥) |
| Run Actions | `ci` [**`42168084408`**](https://github.com/hungnt9801/eCommerce/actions/runs/42168084408) **SUCCESS attempt 1** (head `3ac6e2e`; cổng **19/19**, ⑭ **28,9 s**; artifact `13480536321` có `[CD-D67]`) · `docker-build` [**`42168084400`**](https://github.com/hungnt9801/eCommerce/actions/runs/42168084400) **success** |
| Branch protection `main` | required: **`docker-build`** · **`cong-15-buoc`** (`strict=false`, `enforce_admins=false`) — **không đổi** trong phiên này (`D-65`); đo **SAU merge**: y hệt (2 context, `app_id` 15368, 6 cờ `false`, `reviews`/`restrictions` **null**) |
| **`main` sau phiên** | **`8aee153`** (merge PR #7) — `ci` trên `main` (run **`42169269729`**, head `8aee153`) *xem §7.1*; `docker-build` **`42169269770`** |
| Tệp `N21` đã tạo | `docs/nhanh/handover-n21.md` |
| Tệp `N21` đã sửa | `tools/probe-don-san.ps1` (chẩn đoán, **0** đổi điều kiện) · `tools/kiem-tat-ca.ps1` (bước ⑭: lọc thêm `CD-D67`) · `tools/test-cong-kiem.ps1` (ca **`Y8`** + hàm thuần `Tim-ViPhamChanDoanC10`) · `docs/nhanh/decisions.md` · `docs/nhanh/07-ke-hoach-trien-khai.md` · `docs/nhanh/08-session-protocol.md` · `docs/nhanh/README.md` |
| Dữ liệu `N21` đã xoá | **Không** chạm schema; dữ liệu probe `POC-K9S6-*`/`K9S6-*` đã dọn (xác nhận `C48/C49`), cờ admin **bật lại** `true`; log đo tỉ lệ ở `tools/_tam-kiem/n21/` (gitignore) + `%TEMP%\n21\art\` (ngoài repo) |

### 7.1 `ci` trên `main` **sau merge** — `main` xanh (đo, không suy đoán)

| Mục | Giá trị |
|---|---|
| `ci` trên `main` (head `8aee153`, event `push`) | run **`42169269729`** **SUCCESS · attempt 1** — 📌 **không** phải `rerun`: `C10` **không** flaky ở lượt này |
| Job | `cong-15-buoc` **687 s** · `openapi-drift` **149 s** · `e2e-playwright` **171 s** |
| `docker-build` trên `main` | run **`42169269770`** **success** |
| check-run của head `8aee153` | **4/4 success** (`cong-15-buoc` · `openapi-drift` · `e2e-playwright` · `docker-build`), `app_id` 15368 |
| Tổng kết tỉ lệ `C10` trên CI (toàn phiên) | **2 ĐỎ / 7 lượt** — 2 lượt ĐỎ **đều** ở attempt 1–2 của run `42005307510` (trước phiên), 5 lượt sau đó **XANH** (gồm `42168084408` @ PR #7 và `42169269729` @ `main`) |
