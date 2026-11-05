# Handover `N20` — chốt hướng xử lý `D-64`: **`409` là hợp đồng "chồng request, thử lại"** (probe thử lại có giới hạn)

> Ngày 05/11/2026 · nhánh `ops/n20-d64-reservation` (từ `main` = **`25d2bd5`**, merge PR #4)
> Kế hoạch: [07 §WP-13](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §4](08-session-protocol.md) ·
> Quyết định: [D-64](decisions.md) (nợ) · **D-66** (phiên này) · Trước: [handover-n19](handover-n19.md)
> **Mục tiêu phiên:** `07` **chưa có `§WP-14`** ⇒ **không** mở WP mới. Việc còn lại đúng `handover-n19` §2 #1 +
> `D-65` ⑦: **chốt hướng xử lý `D-64`** (`probe-giu-cho-2instance` flaky) và **giữ 2 required check đúng cấu
> hình đã đo**. **Không mở rộng** phạm vi.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline 6 lệnh §4 của [08](08-session-protocol.md) — đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **39** mục — **tất cả** `?? tools/_*` (rác cổng). **0** tệp mã/docs đang sửa dở ⇒ khớp `handover-n19` §1.1 #1 |
| 2 | `git log --oneline -1` · `git branch --show-current` | `main` = **`25d2bd5`** (*Merge pull request #4* — docs `N19`) · nhánh làm việc mới **`ops/n20-d64-reservation`** (tạo từ `main`) |
| 3 | `docker compose up -d` · `docker compose ps` | daemon **đã chạy sẵn** (lệnh `up -d` chỉ in *Running*) ⇒ `ecommerce-postgres` + `ecommerce-redis` **Up 8 hours (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **full**: **0 error / 9 warning** `CS8604` có sẵn (`Address` 6 · `Sales` 3), **58,8 s** — khớp `handover-n19` §1.1 #4 ⇒ **không** hồi quy |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** (6,7 s) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **121** tệp `.md` · **679** link markdown · **446** đường dẫn backtick · **0** chết |

📌 Ghi rõ **full**/*incremental* như luật `handover-n19` §5 #6 (build full = **9** `CS8604`, incremental = 0/0).

### 1.2 Đo TRƯỚC — required check + `ci` trên `main` (KHÔNG sửa gì)

| Mục | Giá trị đo được |
|---|---|
| `GET /repos/hungnt9801/eCommerce/branches/main/protection` | **200** · `contexts = ["docker-build","cong-15-buoc"]` · `checks` = 2 mục, `app_id` **15368** (GitHub Actions) |
| `strict` · `enforce_admins` | **false** · **false** |
| `allow_force_pushes` · `allow_deletions` · `required_linear_history` · `required_conversation_resolution` · `lock_branch` · `allow_fork_syncing` | **false** × 6 |
| `required_pull_request_reviews` · `restrictions` | **null** (không bắt review, không giới hạn người push) |
| `ci` trên `main` (head `25d2bd5`) | run **`41987011787`** **SUCCESS** · `docker-build` **`41987011784`** **success** |
| check-run của head `25d2bd5` | **4/4 success** (`cong-15-buoc` · `openapi-drift` · `e2e-playwright` · `docker-build`), `app_id` 15368 |

⇒ Khớp **từng trường** với `D-65` ④ / `handover-n19` §1.5 ⇒ **cấu hình 2 required check vẫn đúng như đã đo**.
⛔ Phiên này **không** `PUT` gì (không có lý do đổi), **không** bật thêm job nào.

### 1.3 Repro **TẤT ĐỊNH** ca `D-64` — lần đầu thấy tận mắt (máy dev, 05/11/2026)

| Mục | Đo được |
|---|---|
| Cách làm | Seed `K9HC-*` (như probe) + `OnHand = 2` ⇒ một **transaction `psql` khác** `begin; update "InvItems" set "Reserved"=0, "ConcurrencyStamp"='zzz-gia-lap' …; select pg_sleep(12); commit;` giữ dòng đúng lúc `POST /api/app/checkout/tinh-va-giu-cho` chạy ⇒ request đọc **ảnh cũ**, UPDATE **0 dòng** |
| Lượt 1 | **HTTP 409** · `{"error":{"code":null,"message":"The data you have submitted has already been changed by another user. Discard your changes and try again.","details":null,"data":null,"validationErrors":null}}` |
| Log Host | **1** dòng `AbpDbConcurrencyException` (**0** dòng `Unhandled exception`) · `warn … AbpDbContext` *"There is an entry which is not saved due to concurrency exception: InventoryItem {Id: …d7} Modified"* · `fail … AbpExceptionFilter` · 📌 stack: **`EfCoreRepository`2.InsertAsync(TEntity entity, Boolean autoSave)`** ← `StockReservationService.GiuChoAsync` ⇒ 🚨 **đúng chỗ flush `_soCaiRepository.InsertAsync(but, autoSave: true)` — NGOÀI khối `try/catch`** (khối đó chỉ bọc 2 lệnh `autoSave: false`) |
| **Lượt 2 — gửi lại CÙNG `idempotencyKey`** | **HTTP 200** + **1** dòng giữ chỗ mới ⇒ `OnHand/Reserved = 2.000/1.000` (**chỉ 1** lượt giữ) ⇒ **rollback trọn vẹn**, bất biến `Reserved ≤ OnHand` **không** đổi |
| Kết luận | **`409` = chồng request trên cùng `InventoryItem`, THỬ LẠI được** — không phải 5xx, không phải im lặng, **không** phải bug khoá phân tán |

📌 **Nguyên nhân gốc (code-read + stack ở trên):** khoá phân tán `InventoryLockKeys.TheoSku` được **nhả ở cuối
`GiuChoAsync`**, còn transaction của checkout **commit ở cuối app service** (`CheckoutAppService.TinhVaGiuChoAsync`)
⇒ khe giữa hai mốc ⇒ request thua đọc **ảnh cũ** ⇒ `ConcurrencyStamp` cũ ⇒ 0 dòng.
📌 Script repro ở `%TEMP%\n20-xung-dot.ps1` (*ngoài repo*, không commit). 🚨 **Bẫy đo được:** phải chạy Host ở
**cổng 5000** — `AuthServer:Authority` của `appsettings.Development.json` **ghim** `http://localhost:5000`; chạy ở
5002 thì token **không** validate ⇒ **401** với `IsAuthenticated=false` (mất 3 lượt thử mới phát hiện).

### 1.4 Chốt hướng + sửa **MỘT chỗ** (probe, **không** sửa mã nghiệp vụ)

- ✅ **Chốt hướng (b) của `D-64`** (`D-66`): `409` là **hợp đồng**; client **thử lại**. ⛔ **Không** chọn (a) *retry
  trong server*: `GiuChoAsync` chạy **trong** transaction checkout ⇒ PostgreSQL **đã abort** ⇒ retry tại chỗ bất
  khả; muốn retry thật phải mở **UoW mới ở trên** app service (đổi mô hình *một UoW* của `D-42`/`D-45`) — **không**
  tương xứng với mức nợ và **không cần** để giữ bất biến (đo được: thử lại là đủ).
- ✅ `tools/probe-giu-cho-2instance.ps1`: thêm **`$SoLanThuLai = 3`** + `$ChoGiuaLanThuMs = 300` · `TaoBody()` (một
  chỗ sinh body ⇒ request gốc và request thử lại **giống hệt**) · `GuiLaiMotRequest()` · **`ThuLaiNeu409()`** (gửi
  lại **đúng** request: **cùng Host + cùng `idempotencyKey`**, tối đa 3 lần, **đếm** số lần) · `BanHaiRequest()` trả
  thêm `Url`/`Khoa` để biết **thử lại cái nào** · `P12` đổi tên thành *"…CA HAI request deu 200 **sau khi thu lai 409**"* ·
  **`P14` phân loại lại log** · 1 dòng `ℹ️` in số lần đã thử lại.
- ✅ **`P14` mới (đo được, không suy đoán):** tách **`AbpDbConcurrencyException`** (được phép — nhưng **mỗi** dòng
  phải ứng với **≥ 1** `409` probe đã thấy ⇒ không có xung đột bị **nuốt im lặng**) khỏi
  **`Unhandled exception`/`OutOfMemoryException`** (vẫn **phải = 0**). 📌 Ca 409 in đúng **1** dòng
  `AbpDbConcurrencyException` · **0** dòng `Unhandled exception` (§1.3) ⇒ phép đếm **không** phụ thuộc câu chữ của ABP.
- 📌 `modules/Inventory/eCommerce.Inventory.Domain/StockReservationService.cs`: **chỉ thêm comment** (12 dòng) ghi
  hợp đồng `409` ngay tại chỗ flush + cấm "sửa" bằng cách mở rộng `try/catch` ⇒ **0** thay đổi hành vi.
- **0** migration (59 vẫn mới nhất) · **0** test BE thêm · **0** thay đổi DTO/API.

### 1.5 Chống "nới cổng để cho xanh" — ca **`Y7`** (`test-cong-kiem.ps1`)

| Mục | Số đo |
|---|---|
| Ca mới | **`Y7`** — quét **chính** `probe-giu-cho-2instance.ps1`, hàm **thuần** `Tim-ViPhamThuLai409` (nhận **nội dung** tệp, không đọc đĩa) |
| Điều kiện (a) — tệp thật | `$SoLanThuLai` **khai báo** và **≤ 3** · vòng thử lại **chỉ vì `409`** (`-eq 409`) · còn đủ **3** khẳng định bất biến (`Reserved <= OnHand` · `Available = 0` · `CA HAI request deu 200`) |
| Điều kiện (b) — **chiều ngược 1** | nới giới hạn lên **99** ⇒ phép quét **PHẢI ĐỎ** (đã khẳng định trong ca) |
| Điều kiện (c) — **chiều ngược 2** | xoá `Reserved <= OnHand` ⇒ phép quét **PHẢI ĐỎ** (đã khẳng định trong ca) |
| Test cổng | `-ChiKiemMoc`: **37/37** (trước: **36/36**) · **full: 43/43** (chạy trong bước ⑨ của cổng) |

### 1.6 Kiểm đường **thử lại** chạy THẬT (không chỉ đọc mã)

| Mục | Số đo |
|---|---|
| Bản giả lập | `tools/_tam-kiem/n20/probe-thu-lai-409.ps1` (thư mục **gitignore**, **không** commit): sau khi bắn 2 request, **ép** phần tử thứ 2 = `409` rồi để `ThuLaiNeu409` gửi lại **thật** lên API |
| Kết quả | **17 DAT / 0 HONG** · `ℹ️ THU LAI 409 (D-66): **1** lan · gioi han 3 lan/request · HTTP cuoi: chieu duong 200/403 · chieu nguoc **200/200**` · `P13` = **2** dòng giữ chỗ |

### 1.7 Đo lại **tỉ lệ** (máy dev, cùng cây mã)

| Lượt | Kết quả |
|---|---|
| **5 lượt TRƯỚC khi sửa** | **17 DAT / 0 HONG × 5** (`tools/_tam-kiem/n20/lan-1…5.log`) · `P12 = 200/200` · `P14 exception A = 0 · B = 0` ⇒ ⚠️ **máy dev KHÔNG tái hiện được flake** (khe *nhả khoá → commit* rất hẹp; CI mới trúng — `D-64` ①: `17/17` vs `14/3`) |
| **5 lượt SAU khi sửa** | **17 DAT / 0 HONG × 5** (`tools/_tam-kiem/n20/sau-lan-1…5.log`) · `ℹ️ THU LAI 409: 0 lan` (5/5) · `P12 = 200/200` · `P14 loi that A = 0 · B = 0` |
| 🎯 **CI — lượt `ci` của PR #5** (run **`42002078789`**, head `6c1fb5c`) | **ĐÚNG CA `D-64` ĐÃ XẢY RA VÀ ĐÃ ĐƯỢC THỬ LẠI CỨU:** artifact `giu-cho-2inst.log` ⇒ **17 DAT / 0 HONG** · `P12 = 200/200` · `P13` = **2** dòng · `ℹ️ THU LAI 409 (D-66): **1** lan` · `P14: loi that A = 0 · B = 0 · xung dot trong log A = 0 · **B = 1** · 409 da thay = **1**` ⇒ 📌 Host B **thật sự** vấp `AbpDbConcurrencyException` đúng như `D-64` ① và probe **vẫn xanh** vì đã thử lại **1** lần. Cổng trên CI: **19/19 (293,9 s)** · ⑱ **17,4 s** · Playwright **8/8 (24,4 s)** · link **PASS** (456 backtick) · `docker-build` **success** (`42002078816`) |

📌 Vì máy dev **không** tái hiện, bằng chứng "thử lại cứu được ca 409" là **hai** phép đo khác: repro tất định (§1.3:
`409 → 200` với cùng khoá) và bản giả lập (§1.6: đường `ThuLaiNeu409` chạy thật). **Tỉ lệ thật trên CI** phải đọc ở
lượt `ci` của PR phiên này (§7).

### 1.8 Đo lại cuối phiên (sau khi sửa; máy dev Windows)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-tat-ca.ps1` (bản nhẹ) | **exit 0 — 9/9 bước, 211,7 s** (bước ⑨ *test cổng* **113,3 s** · ⑧ DB **25/25**) |
| `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` | **exit 0 — 19/19 bước, 452,5 s** (⑱ giữ chỗ 2 instance **17/0** · ⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 inst **9/0** · ⑲ Playwright **8/8, 41,0 s**) |
| `pwsh -File .\tools\test-cong-kiem.ps1 -ChiKiemMoc` | **37/37 DAT · 0 HONG** |
| `pwsh -File .\tools\kiem-db.ps1` | **25/25 DAT · 0 HONG** (exit 0) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **122** tệp `.md` · **690** link · **456** backtick · **0** chết |
| `npm --prefix tools run check:mermaid` | **PASS** — **213** khối · **0** lỗi |
| Migration | **0** migration mới (**59** vẫn mới nhất) · **0** test BE thêm (1961 unit + 96 integration, đo ở bước ③/④ của cổng) |

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Việc còn lại đều **cắt có quyết định**:

1. ⏭ **`409` vẫn `code = null`** — map về mã nghiệp vụ (`Inventory:0001`) chỉ đổi **nhãn**, **không** sửa được khe;
   và muốn sửa khe thì phải đổi mô hình *một UoW* (`D-66` ④). **Nhận chủ:** phiên có `§WP-14`.
   📌 Hệ quả cho client: **phải đọc `status = 409`**, đừng chỉ đọc `code`.
2. ⏭ **`07` chưa có `§WP-14`** ⇒ phiên sau **không** được tự đặt WP mới (`08 §5.1` luật 5).
3. ⏭ **`openapi-drift` + `e2e-playwright` vẫn KHÔNG required** — `D-65` ⑤ giữ nguyên (ngoài phạm vi phiên).
4. 📌 **39 tệp `tools/_*` vẫn untracked** + `tools/_tam-kiem/n20/` (log + bản giả lập) **đúng ý đồ** (rác cổng, gitignore).
5. 📌 **Máy dev không tái hiện được flake** (§1.7) ⇒ đừng kết luận *"hết flaky"* chỉ từ 5 lượt local; đọc `ci` của PR.
6. ⏭ **`D-67` (mới, đo được sau merge) — `probe-don-san` `C10` flaky `2/3` lượt trên `main`** (§5 #9): **chưa sửa**, và
   **chưa** đủ bằng chứng để chốt nguyên nhân (cần log Host `[MP-EVENTLOG-*]`). **Nhận chủ:** phiên có `§WP-14`.
   ⛔ **Không** nới `Cho-DieuKien` / bỏ ca `C10`; 📌 khi gặp lại: **so cây mã với PR** + `rerun` (đã đo: attempt 3 xanh).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration mới | **KHÔNG có** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn mới nhất |
| DB đã áp | **Không** đổi schema. 📌 Có **chạm dữ liệu dev** qua probe/repro: seed + xoá `K9HC-*` (bộ dọn của probe + `%TEMP%\n20-xung-dot.ps1`), và cờ `admin.ShouldChangePasswordOnNextLogin` **bật lại = true** (kiểm: `kiem-db.ps1` **25/25**, bước ⑧ của cổng) |
| Vì sao không có | Không đổi schema/DTO: **1** tệp `.cs` **chỉ thêm comment** · 1 probe · 1 cổng · 1 test cổng · docs |
| Cách lùi | `git revert <merge commit của PR phiên này>` ⇒ probe về bản `D-64` cũ (không thử lại, `P14` gộp exception) + xoá ca `Y7`; **không** cần lùi DB |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-66** | **`D-64` chốt hướng: `409` là HỢP ĐỒNG ⇒ probe thử lại có giới hạn (3 lần, cùng `idempotencyKey`); KHÔNG nới bất biến, KHÔNG retry trong server** · repro tất định: `409` `code=null` + **1** dòng `AbpDbConcurrencyException` (**0** `Unhandled exception`), stack tại `EfCoreRepository.InsertAsync(autoSave)` ← `GiuChoAsync`; **thử lại cùng khoá ⇒ 200**, `2.000/1.000` · `P14` tách *xung đột* ↔ *lỗi thật* · ca `Y7` **hai chiều** chống nới probe (test cổng **36/36 → 37/37**) · ⛔ không chọn (a) vì `GiuChoAsync` chạy trong transaction checkout — PostgreSQL đã abort · ⚠️ nợ còn lại: `409` `code = null` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Nếu `P12` còn ĐỎ** dù kho **đủ** hàng ⇒ đó là **tín hiệu THẬT** (khe *nhả khoá → commit* rộng ra, hoặc `409`
   lặp > 3 lần) ⇒ **đừng** tăng `$SoLanThuLai` (ca `Y7` sẽ ĐỎ — cố ý) mà mở `D-67` với số đo từ log Host.
2. ⚠️ **`409` có `code = null`** — client nào chỉ đọc `code` sẽ tưởng lỗi hạ tầng. Hợp đồng là đọc **`status`**;
   muốn có mã đẹp phải là phiên có `§WP-14` (`D-66` ⑦).
3. ⚠️ **`PUT …/protection` ghi đè TOÀN BỘ object** (`D-65` ③) — phiên này **không** `PUT`; lần sau đổi bất cứ cờ nào
   phải `GET` → sửa **đúng 1** trường → `PUT` **cả object** → `GET` lại so **từng** trường.
4. 📌 **Máy dev không có `gh`/`act`**: token qua `git credential fill` (`hungnt9801`), 🔒 **không in token**.
   📌 `head_sha` của API runs cần **SHA ĐẦY ĐỦ**.
5. 📌 **Commit message phải thuần ASCII** (hook `commit-msg` chặn cả `§`).
6. 📌 **Baseline build**: ghi rõ **full** (0 error / **9** `CS8604` có sẵn) hay **incremental** (0/0).
7. 🚨 **Cổng nhẹ bước ⑦ có thể báo drift GIẢ trên checkout Windows MỚI (`CRLF`)** — nợ **có sẵn** từ `N19` §1.7 #1
   (`schema.d.ts` bị `git checkout` ghi CRLF, `pnpm gen:api` ghi LF). ✅ Phiên này **không** gặp (9/9 ngay lượt 1 —
   nhánh **không** checkout lại cây FE). Nếu gặp: chạy `pnpm gen:api` một lần rồi chạy lại cổng; ⛔ **không** nới phép kiểm.
8. 📌 **Đừng `rerun` rồi `push`** — `concurrency.cancel-in-progress` của `pull_request` **huỷ** lượt đang chạy (`N19` §1.6).
9. 🚨 **`D-67` — `probe-don-san` `C10` FLAKY trên `main` (2/3 lượt ĐỎ) dù CÙNG CÂY MÃ với PR xanh 2/2.** Đo được:
   run `42005307510` (head `b1955989`, tree **`a64d69a3`** = tree của `e9f5942`) — attempt **1 ĐỎ** · **2 ĐỎ** (sau `rerun`) ·
   **3 XANH**; chỗ đỏ: **⑭** `C10` `tong = 0 · Late = 0` trong khi `C11…C14` + `C20…C35` DAT (sự kiện **đã** vào DLQ).
   📌 **Không** phải hồi quy `N20` (`N20` không chạm `Sales`/`Marketplace*`; PR xanh 2/2 + máy dev `-DayDu` xanh 2/2).
   ⛔ **Không** nới `Cho-DieuKien` **30 s** hay bỏ ca; muốn chốt nguyên nhân phải **in thêm log Host**
   (`[MP-EVENTLOG-GHI-HONG]`/`[MP-EVENTLOG-TRUNG]`) vì artifact hiện **không** chứa log Host. **Nhận chủ:** phiên có `§WP-14`.
   📌 Đây là **cùng lớp** `D-64` (*probe đo thời điểm*): khi thấy đỏ, **so cây mã với PR** + `rerun` trước khi kết luận.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG phạm vi `N20` — `D-64` đã chốt hướng (`409` là hợp đồng, probe thử lại có giới hạn + ca `Y7`); **CI xác nhận đúng ca đó** (`PR #5`, run `42002078789`: `B = 1` xung đột · `THU LAI 1 lan` · `17/0`); merge **`b1955989`**; 2 required check **giữ nguyên** cấu hình đã đo. ⚠️ Sau merge, `main` lộ **nợ mới `D-67`** (`probe-don-san` `C10` flaky `2/3`, cùng cây mã với PR xanh) — **chưa** sửa. `07` **chưa** có `§WP-14` ⇒ phiên `N21` **không** mở WP mới.**

1. 🔑 Đọc **`D-66`** + **`D-67`** + **`D-64`** + §2 **trước** khi làm gì: `D-66` **đã đóng** (`409` = hợp đồng, probe thử lại,
   CI xác nhận); nợ **mở** là **`D-67`** (`probe-don-san` `C10`).
2. Kiểm **cấu hình còn đúng** (không `PUT` nếu không có quyết định mới):
   `GET /repos/hungnt9801/eCommerce/branches/main/protection` ⇒ **2** context (`docker-build`, `cong-15-buoc`),
   `strict=false`, `enforce_admins=false`; và `ci` trên `main` **xanh** ở head mới nhất (`b1955989` — attempt 3).
3. **`D-67` — thu bằng chứng, ⛔ KHÔNG sửa vội:** chạy `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` **vài lượt** trên máy
   dev (ghi tỉ lệ), và nếu đỏ thì **in thêm log Host** (`[MP-EVENTLOG-GHI-HONG]`/`[MP-EVENTLOG-TRUNG]`) — hiện artifact
   **không** chứa log Host nên lượt ĐỎ trên CI **không** đọc được nguyên nhân. ⛔ Không nới `Cho-DieuKien` 30 s, không bỏ ca.
4. Nếu có `§WP-14` ⇒ làm đúng WP đó (nhánh mới từ `main`, **không** dùng lại `ops/n20-d64-reservation`). Nếu **chưa** có:
   **không** tự đặt WP.
5. Sau khi sửa bất cứ thứ gì: `pwsh -File .\tools\kiem-tat-ca.ps1` (**9/9**) · trước khi push `-DayDu`
   (**19/19**) · `test-cong-kiem.ps1 -ChiKiemMoc` (**37/37**) · `kiem-db.ps1` (**25/25**) · `kiem-link-tai-lieu.ps1` (**PASS**).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-13
và handover gần nhất `docs/nhanh/handover-n20.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số. `07` chưa có `§WP-14` ⇒ phiên `N21` **không** mở WP mới: việc còn lại đúng phạm vi ghi ở
handover §2 — thu bằng chứng cho `D-67` (flaky `probe-don-san` `C10`) và giữ 2 required check đúng cấu
hình đã đo. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

> 📌 Khối trên là biến thể **"đóng nợ"** của `08 §5.1` (không có `§WP-14` để mở): đủ **3 thứ** là *mã phiên đích*
> (`N21`) · *đường dẫn handover* (`handover-n20.md`) · *việc phải làm* (`D-67` + giữ cổng), **không** mang số đo.

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh làm việc | **`ops/n20-d64-reservation`** (tạo từ `main` = `25d2bd5`) |
| `main` trước phiên | **`25d2bd5`** (*Merge pull request #4* — docs `N19`); `git log -1 --format=%H origin/main` là **nguồn sự thật** |
| Commit của phiên | **`6c1fb5c`** (9 tệp · **+431 / −41**) — 📌 chỉ ghi SHA **ngắn**; nguồn sự thật là `git log -1 --format=%H` |
| PR | [**#5**](https://github.com/hungnt9801/eCommerce/pull/5) — `ops/n20-d64-reservation` → `main` (docs + probe + cổng; **0** migration); `mergeable_state = blocked` khi 2 required check còn **pending** (đúng `D-65` ⑥) |
| Run Actions | `ci` [**`42002078789`**](https://github.com/hungnt9801/eCommerce/actions/runs/42002078789) **SUCCESS** (head `6c1fb5c`; cổng **19/19, 293,9 s**; ⑱ **17/0** + `THU LAI 409: 1 lan`) · `docker-build` [**`42002078816`**](https://github.com/hungnt9801/eCommerce/actions/runs/42002078816) **success** · `mergeable_state = blocked` khi 2 check còn **pending** |
| Branch protection `main` | required: **`docker-build`** · **`cong-15-buoc`** (`strict=false`, `enforce_admins=false`) — **không đổi** trong phiên này (`D-65`); đo **SAU merge**: y hệt (2 context, `app_id` 15368, 8 cờ `false`) |
| **`main` sau phiên** | **`b1955989`** (merge PR #5) — ⚠️ `ci` lượt **đầu** trên `main` **ĐỎ** ở ⑭ (`probe-don-san` `C10`), **attempt 2 ĐỎ**, **attempt 3 XANH** ⇒ `main` **xanh** (check-run **4/4 success**), nhưng **nợ mới `D-67`** (§5 #9); `docker-build` `42005307468` **success** |
| Tệp `N20` đã tạo | `docs/nhanh/handover-n20.md` |
| Tệp `N20` đã sửa | `docs/nhanh/decisions.md` · `docs/nhanh/07-ke-hoach-trien-khai.md` · `docs/nhanh/08-session-protocol.md` · `docs/nhanh/README.md` · `tools/probe-giu-cho-2instance.ps1` · `tools/test-cong-kiem.ps1` · `tools/kiem-tat-ca.ps1` (comment) · `modules/Inventory/eCommerce.Inventory.Domain/StockReservationService.cs` (**chỉ comment**) |
| Dữ liệu `N20` đã xoá | **Không** chạm schema; dữ liệu probe `K9HC-*` đã dọn (probe + repro), cờ admin **bật lại** `true`; log/bản giả lập ở `tools/_tam-kiem/n20/` (gitignore) + `%TEMP%` (ngoài repo) |
