# 08 — Chia phiên làm việc (session protocol)
> Trả lời câu hỏi: **"chạy cả kế hoạch 12 tuần trong một session hay chia nhiều session?"**
> **Trả lời ngắn: chia nhiều session** — 1 session ≈ 1 work package, mở/đóng bằng số đo.
> Tài liệu này là quy ước để phiên sau **không phải đọc lại toàn bộ** lịch sử.
---
## 1. Vì sao **không** chạy một session
| Lý do | Hệ quả nếu cố chạy liền |
|---|---|
| **Context có hạn** — 12 tuần ≈ 120 dev-week, mỗi WP đã tốn 30–80k token cho đọc code + build/test log | Đến WP-04 sẽ phải "nhớ lại" bằng tóm tắt ⇒ **mất chi tiết quyết định**, dễ sửa sai chỗ đã chốt |
| **Lỗi tích luỹ** — một migration sai ở WP-03 sẽ làm mọi bước sau vô nghĩa | Càng dài, chi phí quay lui càng lớn; không có điểm dừng xanh để so sánh |
| **Cửa sổ kiểm chứng** — `dotnet test` 1.634 test + build + DB probe mỗi vòng | Chạy 6 WP trong 1 session ⇒ hàng chục vòng đo, log nuốt hết ngân sách context |
| **Bài học của chính repo** | GĐ1 đã chia **S1…S20**, có `SESSION-KICKOFF.md` + biên bản bàn giao; đó là lý do 1.634 test vẫn xanh sau 20 phiên |
**Ngược lại, cũng đừng chia quá nhỏ** (mỗi hàm một session): chi phí dựng lại ngữ cảnh (đọc 2–4 tài liệu + chạy baseline) ≈ 20–30% thời gian mỗi phiên.
---
## 2. Nguyên tắc chia
1. **1 session = 1 work package** (WP-01…WP-10) hoặc **nửa WP** nếu WP > 10 ngày công.
2. **Chỉ cắt ở điểm xanh**: `dotnet build` 0 error + `dotnet test` ≥ baseline. **Không** cắt giữa migration/backfill.
3. **Mỗi session một nhánh**: `feature/wp-03-catalog` → merge khi xanh; không làm 2 WP trên 1 nhánh.
4. **Mở session bằng baseline, đóng session bằng số đo** — nếu số lệch baseline cũ, dừng và báo, không tự diễn giải.
5. **Không chạy song song 2 session cùng module.** Song song chỉ khi chạm **file rời nhau** (ví dụ: FE dựng màn mới + BE viết endpoint khác module) và phải có người merge.
6. **Mọi quyết định mới** ghi vào [decisions.md](decisions.md) trong session đó, không để "nhớ trong đầu".
---
## 3. Bản đồ session đề xuất (GĐ0–GĐ2)
| Session | WP | Nội dung | Điều kiện xanh cuối session |
|---|---|---|---|
| **N1** ✅ đã xong | WP-01 + WP-02 | Next.js shell + login + BFF, `HttpApi` v1, 3 endpoint | login thật → trang 200; `next build` ✓; 1634 test |
| **N2** ✅ đã xong | WP-03 (nửa đầu) | `ProductType/Unit/ParentId/VAT` + entity `ProductUnit`, migration **51** additive | build + test xanh (1671); tạo SP 3 ĐVT qua API — [handover](handover-n2.md) |
| **N3** ✅ đã xong | WP-03 (nửa sau) | Backfill `ProductVariant → Product con` (dry-run → thật), luật cha–con + publish theo ĐVT gốc, `OrderLine.ProductId`, adapter đọc cũ | backfill **2 lần cùng kết quả** (2 → 2 rồi 2 → 0, DB y nguyên); `/SanPham` **200** cho cả hai mô hình; 1684 test — [handover](handover-n3.md) |
| **N4** ✅ đã xong | WP-04 (BE) | 8 chiều tồn + `Mode` phiếu + bút toán theo `(mode, chiều)` + API `/api/v1/stock/*`, migration **53**; `N4b`: `decimal(18,3)` hoá 16 cột số lượng, migration **54** | bất biến `tổng sổ cái = ảnh chụp` **10 = 10**; nhập 10 → tồn 10; số lẻ sống sót (`1,5 kg`, 1000 × 0,001 = 1,000) — probe HTTP 12 ca `exit 0`; **1773** test — [handover N4](handover-n4.md) · [N4b](handover-n4b.md) |
| **N5** ✅ đã xong | WP-04 (FE) | DS phiếu (tab + bulk + cột) · tạo phiếu (form động) · chi tiết (ghi sổ/huỷ) · tồn 9 chiều · in phiếu · **tiền (đơn giá → thành tiền → tổng)** | Post → tồn đổi **trên UI** (`Bán 10`); Post lại **403 `0010`**; bulk **2 thành công / 0 lỗi**, skip **1 bỏ qua**; tiền **75.000/6.000/81.000**; probe FE **19/19** — [handover](handover-n5.md) |
| **N6** ✅ đã xong | WP-05 | 18 trạng thái (append 9) · `OrderType/OrderSource/OrderReasonCode` · 11 cột đơn · bảng cạnh Nhanh + 15 cạnh huỷ · nhãn suy từ 4 trục · API `/api/v1/orders` (DS **14 tab** · chi tiết · `transitions` · `cancel` · `history`), migration **56** · FE 2 màn | đơn đi trọn `Mới → Thành công` (**8/8 lời gọi 200**); xác nhận ⇒ `OnHand 10→8` (`ChotGiuCho`); hoàn ⇒ `OnHand 7→8` + phiếu `HOAN-…`; **403 đúng mã** `0003`/`0001`/`0010`; probe **29 lời gọi** `exit 0`; **1855** test — [handover](handover-n6.md) |
| **N7** ✅ đã xong | WP-06 | `RetailBill` + `CashSession` + `RetailReturn` + thanh toán đa phương thức + in + **outbox offline** | bán 3 SP, chia 2 phương thức (**150.000**), in, kết ca (`expectedCash 500.000`/`variance 0`); **F5 không mất tab** + rớt mạng vẫn lưu; probe **34 lời gọi** `exit 0`; browser **10/10**; **1900** test — [handover](handover-n7.md) |
| **N8** ✅ đã xong | WP-07 | Khách/nhân viên + điểm/công nợ **vào khách** + data scope | data scope lọc đúng (**`Own` ⇒ hoá đơn người khác biến mất + đọc chi tiết 403 `Pos:0028`**); điểm/công nợ của POS ghi vào `Customer` (**12 / 100.000 / 50.000**, tự lên cấp; trả/huỷ đảo đúng); probe **28 lời gọi** `exit 0`; **1933** test — [handover](handover-n8.md) |
| **N9** ✅ đã xong | WP-08 | Reporting read-model (`Rpt*` 8 bảng) + job mốc/backfill + Dashboard 4 KPI + 2 báo cáo | báo cáo **`1.470.000` = `1.245.000` + `225.000`** (`SUM(RetailBills + Orders)` bằng SQL độc lập); chạy lại **17/2/38 dòng không đổi**; hoá đơn/đơn **đã huỷ** ⇒ `0` dòng; probe **20 lời gọi** `exit 0`; **1961** test + **96** integration — [handover](handover-n9.md) |
| **N10** ✅ đã xong | WP-09 | Playwright **8 luồng** Web Admin + `pnpm test` (Vitest) + job CI `openapi-drift` + **cổng có bước FE** | **8/8 E2E xanh** trên **DB TRONG** (job `e2e-playwright`); `pnpm gen:api` **không đổi** `schema.d.ts` (**51/51** path); **25** unit test FE; cổng `kiem-tat-ca.ps1` **thêm 3 bước FE** — [handover](handover-n10.md) |
| **N11** ✅ đã xong | WP-10 | Ops: `docker-compose.yml` thêm service `admin-web` (production build), `.env.example`, health `/api/health` (BFF → `/health-status`) | `docker compose up -d admin-web` dựng được Admin Web production trên cổng **3000** (image **354 MB**); `/api/health` trả **200** khi Host sống, **503** khi Host chết (container `unhealthy` **91 s** / `healthy` **10–15 s**) — `tools/kiem-ops-wp10.ps1` **9 lời gọi, exit 0** — [handover](handover-n11.md) |
| **N12** ✅ đã xong | WP-11 | **Làm cổng xanh** (nợ phát sinh `N5…N11`): ① **9 script cũ** hết đường dẫn Windows viết cứng (bước ⑨) ② **quyết dứt điểm 6 dòng `CusCustomers`** của cổng DB (bước ⑧) ③ *(tuỳ)* job CI `docker-build` | `pwsh -File .\tools\kiem-tat-ca.ps1` → **exit 0** (9/9 bước) · test cổng **33/33** · DB **25/25** · E2E **8/8** · drift **51/51** — [handover](handover-n12.md) |
| **N13** ✅ đã xong | WP-12 | **Làm `-DayDu` xanh** (phát sinh từ `N12`): sửa `probe-oversell` so **số** thay vì so **chuỗi** (`D-31` → `decimal(18,3)`), soát các probe cùng họ, chạy **trọn** `-DayDu` | `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` → **exit 0**, `TONG SO BUOC DAT: 19` (**sửa `N13`**: 17 là đếm sai); bản nhẹ **vẫn** `exit 0` (9/9) — [handover](handover-n13.md) |
| **N14** ✅ đã xong | WP-13 | **P1 — đóng 2 nợ kiểm chứng**: ① `probe-kho-wp04.ps1` qua cổng auth — **một chỗ** `Invoke-KhongCoDoiMatKhau` + cặp `Start-KhongCoDoiMatKhau`/`BatLai-CoDoiMatKhau` trong `nen-tang.ps1`, đóng luôn lỗ **4** probe tắt cờ mà **không** bật lại ② *(tuỳ)* job CI `docker-build` | `probe-kho-wp04.ps1` **exit 0** (**12 ca**) · `probe-pos.ps1` **exit 0** (**34** lời gọi — chứng minh cặp hàm, cờ về `ADMIN\|true`) · `kiem-db.ps1` **25/25** · bản nhẹ **9/9** · `-DayDu` **19/19** · test cổng **34/34** (full **40/40**) · **0 migration** — [handover](handover-n14.md) |
| **N15** ✅ đã xong | WP-13 ② | **Job CI `docker-build`** — workflow **riêng** `.github/workflows/docker-build.yml`: build image Web Admin trên runner **SẠCH** + 2 bất biến sau build (`server.js` standalone · `/login` **200** khi API chết) | run **`41808550753`** **xanh** trên `ubuntu-latest` (**76 s**, bước build **68 s**, **6/6** bước) · lượt đo **đầu ĐỎ** vì `Dockerfile`/`.dockerignore` **untracked** (`D-57` ③) · nhánh tạm **đã xoá**, `main` **không đổi** · bản nhẹ **9/9** · `-DayDu` **19/19** · **0 migration** — [handover](handover-n15.md) |
| **N16** ✅ đã xong | WP-13 (đóng gói) | **Commit → push → PR → required check**: `d8e22dc` (cây FE — **bắt buộc**, `D-58`) + `6c49d3a` (3 tệp `.dockerignore` · `Dockerfile` · workflow) · PR **#1** · bật **`docker-build`** required (**D-59**) | **`docker-build` xanh trên `pull_request` lần đầu** — run **`41811821775`**, **88 s**, **6/6** bước; cây sạch `6c49d3a`: build **exit 0**, `/login` **200**, `/api/health` **503**; required check **đã bật** (trước đó `main` **không** có protection — `GET` **404**); ⚠️ `ci` đỏ bước 7 (**nợ `packageManager`+`yarn` có sẵn**) · **0 migration** — [handover](handover-n16.md) |
| **N17** ✅ đã xong phần được duyệt | WP-13 (nợ CI) | Bỏ `packageManager` khỏi `package.json` gốc (**Yarn 1.22** của `abp install-libs` từ chối field) + sửa comment `Dockerfile`; **KHÔNG** merge PR #1 | bước **7 `ci` SUCCESS** trên CI thật (run **`41955331290`**), `docker-build` **6/6**; `ci` còn đỏ ở **⑦/Y1** vì 3 probe `N12`–`N14` chưa commit ⇒ **`D-61`: không merge** (merge làm `ci` `main` đỏ + nửa FE/API 404); cổng nhẹ **9/9** · `-DayDu` **19/19** · DB **25/25** · **0 migration** — [handover](handover-n17.md) |
| **N18** ✅ đã xong phạm vi | WP-13 (commit + merge) | **Commit cây làm việc** (BE + `tools/` + docs — **6** commit · **160** tệp) ⇒ `ci` xanh trên PR #1 ⇒ **MERGE**; sửa **2 lỗi chỉ lộ trên cây sạch** (mốc ảnh tồn kho ghi cho lượt chụp **0 dòng**; cổng link bỏ qua `artifacts/`) | `ci` lượt **1** (head `3f93c14`) **ĐỎ** (link bước 11 + `e2e-playwright` **7 passed/1 failed**) ⇒ sửa `39df2d4` ⇒ `ci` lượt **2** **[`41963379150`](https://github.com/hungnt9801/eCommerce/actions/runs/41963379150)** **3/3 XANH (10,2 phút)** · `docker-build` **6/6** ⇒ **merge `fab0089`** (PR #1: 46 commit · **610** tệp); cổng nhẹ **9/9** · `-DayDu` **19/19 (436,8 s)** · test cổng **36/36** · DB **25/25** · **0 migration**; 🚨 **nợ mới `D-64`**: `probe-giu-cho-2instance` **flaky** trên CI (cùng mã: **17/17** vs **14/3**) — [handover](handover-n18.md) |
| **N19** ✅ đã xong phạm vi | WP-13 (required check) | **Bật `cong-15-buoc` làm required check** — việc còn lại duy nhất của `D-63`; **không** mở WP mới (`07` chưa có `§WP-14`) | đo **TRƯỚC** `GET …/protection` ⇒ `["docker-build"]`; `ci` trên `main` xanh (run **`41969985282`**, 3/3 job, head `0e5c275`) ⇒ `PUT` **cả object** ⇒ đo **SAU** `["docker-build","cong-15-buoc"]`, 10 cờ còn lại không đổi; chứng minh hiệu lực bằng PR **#3** (`mergeable_state=blocked` trước khi 2 check xanh); cổng nhẹ **9/9** · `-DayDu` **19/19** · test cổng **36/36** · DB **25/25** · **0 migration**; 🚨 `D-64` flaky **nay chặn merge** — [handover](handover-n19.md) |
> ⏱ Mỗi session N2…N10 ước **2–4 giờ máy chạy** (kể cả build/test). Nếu một session chưa xong WP: **cắt ở điểm xanh**, ghi handover, session sau làm tiếp — **không** kéo dài quá 1,5× dự kiến.
---
## 4. Mở session (kickoff) — copy 6 dòng này
```powershell
cd D:\Projects\Tools\eCommerce
git status --short          # phải sạch hoặc chỉ có docs
git switch <nhánh-của-WP>
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```
Câu mở đầu cho agent (dán vào session mới):
> Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-0X
> và handover gần nhất trong `docs/nhanh/`. Chạy baseline 6 lệnh ở `session-protocol.md` §4, ghi số,> rồi làm **đúng WP-0X**. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
⚠️ Câu trên chỉ là **bản đầy đủ cho phiên mới tinh**. Phiên trước **phải** đưa sẵn câu để bạn dán —
có **hai biến thể** (dở dang / đã xong), xem **§5.1**. Đừng tự chế lại.
---
## 5. Đóng session (handover) — mẫuTạo tệp handover (đặt tên `handover-Nx.md`, cùng thư mục này) với **đúng 7 mục** (không viết lan):
1. **Đã xong** — việc + số đo (test/build/endpoint), kèm lệnh kiểm.
2. **Chưa xong / cắt giữa** — chính xác đang ở đâu, file nào đang sửa dở.
3. **Migration đã áp** — số hiệu + DB nào; cách lùi.
4. **Quyết định mới** — dòng `D-xx` đã thêm vào [decisions.md](decisions.md).
5. **Nợ / rủi ro** — điều gì sẽ nổ nếu phiên sau không xử lý.
6. **Việc đầu tiên của phiên sau** — **dòng trạng thái** (`XONG <WP> → mở <WP kế>` hoặc   `DỞ DANG <WP> → mở lại <WP>`), rồi 1–3 gạch đầu dòng kèm lệnh, rồi **khối copy-paste ĐÃ ĐIỀN** theo §5.1.
7. **Trạng thái nhánh** — commit SHA, đã push chưa, PR nào.
> 🔑 **Bài học từ GĐ1 (đã trả giá):** handover phải ghi **số đo**, không ghi "đã ổn".
> Một dòng *"test xanh"* mà không có **tên lệnh + số ca** thì phiên sau không phân biệt được> "xanh thật" với "không chạy".
### 5.1 Câu mở phiên kế — **BẮT BUỘC**, hai biến thể
Kết thúc **mỗi** phiên, câu trả lời cho người dùng phải có **đúng một** khối copy-paste dưới đây (trongcode block), chọn theo **trạng thái thật của phiên** — không phải theo cảm giác "chắc là xong":
| Trạng thái phiên | Gửi biến thể | Phiên kế làm gì |
|---|---|---|
| **Chưa hoàn thành** WP — **DoD chưa đạt**, tệp đang sửa dở, hoặc migration/backfill dừng giữa | **A** | mở **cùng WP**, làm **tiếp phần còn lại** đã ghi ở handover §2 |
| **Đã hoàn thành** WP — DoD đạt **và** đã đo lại; mọi mục ở handover §2 là **cắt CÓ QUYẾT ĐỊNH** (đã giao WP khác hoặc đã ghi lý do ở `decisions.md`) | **B** | mở **WP kế tiếp** theo bản đồ §3 |
> 🚨 **Phân biệt quan trọng (dễ sai nhất):** *"handover §2 còn mục"* **KHÔNG** tự động nghĩa là **A**.
> `N7` có **10 mục** ở §2 nhưng đều là **cắt có quyết định** (`D-42…D-44`, có mục giao WP-07/P1) ⇒ vẫn là **B**.
> Ngược lại, `N4` từng hoãn `decimal` hoá số lượng và **WP-04 vẫn mở** ⇒ phải tách `N4b` và dùng **A**.
> **Dấu hiệu quyết định:** mục đó có **nhận chủ** (WP sau/P1) hoặc có dòng `D-xx` không? Có ⇒ B. Không ⇒ A.
**A — phiên dở dang (mở lại đúng phiên đó):**
```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §<WP-0X>
và handover `docs/nhanh/handover-<Ny>.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên <Ny> **dừng giữa** <WP-0X>: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```
**B — phiên đã xong (mở phiên kế tiếp):**
```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §<WP-0Y>
và handover gần nhất `docs/nhanh/handover-<Ny>.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng <WP-0Y>** (phiên <Nz>). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```
Luật (mỗi luật là một lần đã trả giá vì prompt mơ hồ):
1. **Đúng MỘT khối, dán được ngay** — không kèm danh sách tệp, không kèm số đo, không kèm lời dặn ngoài lề.   Số đo và danh sách tệp thuộc **handover**, không thuộc prompt (prompt dài ⇒ phiên sau bỏ qua nửa sau).
2. **Luôn có đủ 3 thứ**: mã WP, mã phiên đích, **đường dẫn handover**. Thiếu một thứ là phiên sau phải   **đoán** — và sẽ đoán khác ý.
3. **Khi GỬI phải thay HẾT `<…>`** bằng giá trị thật (WP-07, N8, `handover-n7.md`…). Prompt gửi đi còn dấu   `<` nghĩa là **chưa xong** — người dùng phải tự sửa, đúng thứ quy ước này sinh ra để tránh. Hai khối trên   để **`<…>`** vì đây là **mẫu**; còn bản gửi cho người dùng là bản **đã điền**.
4. **Trạng thái lấy từ ĐO + từ quyết định**, không từ cảm giác: **B** chỉ khi DoD của WP đã đạt **và** đã đo lại   (build/test/probe xanh) **và** mọi mục §2 đã có nhận chủ/`D-xx`; ngược lại **A**. Nếu bản thân người viết   **không phân loại được** ⇒ chọn **A** (mở lại WP là **an toàn**: phiên sau chỉ việc xác nhận và đóng, rẻ hơn   bỏ sót một phần việc của WP).
5. **Không nhét điều mới vào prompt.** Muốn phiên sau ưu tiên gì ⇒ ghi vào handover §6 hoặc   [decisions.md](decisions.md) **trước**, rồi mới mở phiên. Prompt không phải nơi chở yêu cầu.
6. **Không chứa số liệu** (số test, số migration, SHA…) — chúng đổi mỗi lần chạy; nguồn sự thật là handover.
7. Nếu phiên dừng vì **lỗi môi trường** (cổng bận, DB chưa lên) **trước khi** chạm mã: vẫn dùng **A**, và   handover §2 phải nói rõ *"chưa chạm mã"* để phiên sau **không** đi tìm thay đổi không tồn tại.
8. **Khối gửi kèm 1 dòng trạng thái** ngay trên nó: `Trạng thái phiên: XONG <WP> → mở <WP kế>` hoặc   `Trạng thái phiên: DỞ DANG <WP> → mở lại <WP>`. 
📌 Dòng này **cũng phải** nằm ở **đầu handover §6**, để   phiên sau đọc một chỗ là biết mình đang mở lại hay mở tiếp.
---
## 6. Dấu hiệu phải dừng session (dù chưa xong WP)
| Dấu hiệu | Hành động |
|---|---|
| 3 vòng liên tiếp sửa–build–fail cùng một lỗi | Dừng, ghi handover, mô tả **giả thuyết sai** đã loại |
| Context đã dùng > ~70% | Dừng ở điểm xanh gần nhất, handover |
| Phải sửa > 5 file ngoài phạm vi WP | Dừng: phạm vi đang trôi → cập nhật plan trước |
| Test đỏ do thay đổi ngoài dự kiến | **Không** sửa test cho xanh; dừng, báo |
| Migration chạm DB có dữ liệu mà chưa backup | Dừng, snapshot trước (`pg_dump`) |
---
## 7. Khi nào **được** chạy song song
| Được | Không được |
|---|---|
| Session khảo sát/đọc tài liệu + session code (file rời nhau) | 2 session cùng sửa `Product`/`StockDocument`/`Order` |
| FE dựng màn mới **trên API đã có** + BE làm endpoint **module khác** | 2 session cùng chạy `dotnet ef migrations add` (đụng `ModelSnapshot`) |
| 2 session đọc/viết tài liệu khác thư mục | 2 session cùng sửa `eCommerce.slnx` / `Directory.Packages.props` |
---
## 8. Kết luận cho trường hợp của bạn
- **Chia 10 session** (N1 đã xong) cho GĐ0–GĐ2, đúng bản đồ §3.
- Session hiện tại nên **đóng sau khi commit** phần WP-01/02 — đã đủ lớn (khảo sát + 6 tài liệu + 2 project + build/login chạy thật).
- **N2** (WP-03 nửa đầu), **N3** (WP-03 nửa sau), **N4** + **N4b** (WP-04 BE), **N5** (WP-04 FE),  **N6** (WP-05), **N7** (WP-06), **N8** (WP-07), **N9** (WP-08), **N10** (WP-09) và **N11** (WP-10) **đã xong**  — đóng bằng số đo, xem [handover-n2](handover-n2.md), [handover-n3](handover-n3.md), [handover-n4](handover-n4.md),  [handover-n4b](handover-n4b.md), [handover-n5](handover-n5.md), [handover-n6](handover-n6.md),  [handover-n7](handover-n7.md), [handover-n8](handover-n8.md), [handover-n9](handover-n9.md),  [handover-n10](handover-n10.md), [handover-n11](handover-n11.md).  
📌 `N3` **không** làm form sản phẩm FE (việc FE 3 ngày của WP-03) — vẫn có thể mở một session **FE độc lập**  trên API đã có (điều kiện §7: chạm file rời nhau với BE). `N5` đã dựng sẵn `StockBadge`, `BalancesTable`,  `ProductPicker`, `lib/format.ts` (số 3 lẻ + `vi-VN`) ⇒ form sản phẩm dùng lại được, **đừng viết lại**.  
📌 `N6` **chưa** làm 4 bảng phụ của WP-05 (`OrderLabel`/`OrderPacking`/`OrderHandover`/`OrderComplaint`),  **chưa** có bulk đổi trạng thái và tab **Thanh toán** của đơn — xem [handover-n6](handover-n6.md) §2.  
📌 `N7` **chưa** làm: huỷ **phiếu trả**, **service worker/Background Sync** (cần HTTPS), xung đột tồn khi đồng  bộ muộn, 3 chế độ quét (**IMEI**), quà tặng — xem [handover-n7](handover-n7.md) §2/§5.  
📌 `N8` **đã đóng** nợ #6 của `N7` (**điểm/công nợ ghi vào khách**), nhưng **chưa**: màn CRUD cửa hàng/  phòng ban, gán **role ABP** cho người dùng, phạm vi **theo từng nhân viên**, nối `Level.DiscountPercent`  vào bảng giá — xem [handover-n8](handover-n8.md) §2.  
📌 `N9` (WP-08) **đã xong** — 8 bảng `Rpt*` + job mốc + 4 KPI + 2 báo cáo, **3.900 → 4.067** dòng  `schema.sql`, **1961** test — xem [handover-n9](handover-n9.md).  
📌 `N10` (WP-09) **đã xong** — **8/8** luồng Playwright + **25** unit test FE + job `openapi-drift`  (**51/51** path) + job `e2e-playwright` (DB **trống**) + cổng có **3 bước FE**; ⚠️ **`WP-10` (Ops) chưa làm**  và **cổng DB còn 1 HỒNG có sẵn** (`CusCustomers` 6 dòng probe `WP06`/`WP07`) — xem  [handover-n10](handover-n10.md) §2/§5.  
📌 `N11` (WP-10) **đã xong** — `admin-web` production trong compose (**354 MB**, cổng 3000) + `/api/health`  **200/503** theo Host + `.env.example` có `AUTH_ISSUER`/`NEXT_PUBLIC_APP_NAME`; ⚠️ **2 HỒNG có sẵn** phải xử lý  ở phiên sau: **cổng DB** (`CusCustomers`, 6 dòng probe — bước ⑧) và **`test-cong-kiem.ps1` 32/33** (ca Y1:  **9 script cũ** còn đường dẫn Windows viết cứng — bước ⑨, hiện bị **che** sau bước ⑧) ⇒ xem  [handover-n11](handover-n11.md) §5. **Cả hai đã đóng ở `N12`.**  
📌 `N12` (WP-11) **đã xong** — 9 script cổng hết khuôn chỉ-Windows + `CusCustomers` ra khỏi nhóm "phải rỗng"  (bất biến **mạnh hơn**); `kiem-tat-ca.ps1` **exit 0** (9/9), test cổng **33/33**, DB **25/25** — xem  [handover-n12](handover-n12.md).  
📌 `N13` (WP-12) **đã xong** — `-DayDu` **exit 0 (19/19, 462,5 s)** + bản nhẹ **9/9**; 2 probe so **SỐ** cho  `decimal(18,3)`; sửa **lỗi in** của cổng ở bước ⑲ (Playwright đã 8/8 nhưng cổng NÉM vì `$_.Line` = `null`);  **0 migration**, **0** test BE thêm — xem [handover-n13](handover-n13.md).  
📌 `N14` (WP-13) **đã xong** — `probe-kho-wp04` **exit 0 (12 ca)** qua cổng auth bằng **một chỗ**  (`Invoke-KhongCoDoiMatKhau` + cặp `Start-KhongCoDoiMatKhau`/`BatLai-CoDoiMatKhau` trong `nen-tang.ps1`);  đóng luôn lỗ **4** probe tắt cờ bảo mật mà **không** bật lại (`probe-pos` **34** lời gọi vẫn `exit 0`, cờ về  `ADMIN|true`); **2** ca kiểm mới `Y6`/`L2`; `kiem-db` **25/25** · bản nhẹ **9/9** · `-DayDu` **19/19** ·  **0 migration** — xem [handover-n14](handover-n14.md).  
📌 `N15` (WP-13 ②) **đã xong** — workflow **riêng** `.github/workflows/docker-build.yml`; **đo thật** trên  `ubuntu-latest` run **`41808550753`** xanh **76 s** (build **68 s**); lượt đo **đầu ĐỎ** vì  `Dockerfile`/`.dockerignore` **chưa từng commit** (`D-57` ③); nhánh tạm **đã xoá**, `main` **không đổi** —  xem [handover-n15](handover-n15.md).
- Session kế tiếp: **GĐ0–GĐ2 đã xong toàn bộ WP-01…WP-13** theo `07`, **kể cả `WP-13` ②** (`N15`: job CI  `docker-build` chạy thật xanh trên `ubuntu-latest` — xem [handover-n15](handover-n15.md)). ⇒ **Không còn mục  P1 nào đang mở.** Việc còn lại **không phải** một WP: ① **commit + push + PR** (phiên `N7…N15` **chưa  commit**; `Dockerfile`/`.dockerignore` là file **untracked** ⇒ phải vào commit đầu — `D-57` ③) và ② bật  `docker-build` làm **required check** trên GitHub (`D-57` ⑥ — cài đặt repo, không nằm trong mã).  ⛔ **Không** tự đặt WP khác; muốn mở P1 mới thì người lập kế hoạch phải viết `§WP-14` vào `07` **trước**  (`08 §5.1` luật 5).
- 🔒 **Quy ước ĐÓNG phiên (bắt buộc từ 04/11/2026):** mỗi phiên kết thúc bằng **một** khối copy-paste theo  **§5.1** — **A** nếu phiên dở dang (mở lại *cùng* WP), **B** nếu đã xong (mở WP kế). Không dán số đo, không  dán danh sách tệp vào prompt; nguồn sự thật là handover. Người dùng **chỉ cần dán** khối đó để mở phiên sau.  
📌 Phiên `N7…N15` **chưa commit** — commit theo khuôn cũ (`git add` **đường dẫn cụ thể**), **đừng**  `git add -A` (còn **hơn 100** mục rác khảo sát/probe).