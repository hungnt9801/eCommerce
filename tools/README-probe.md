# 📋 DANH MỤC KIỂM TRƯỚC KHI VIẾT PROBE — và LUẬT cho form Web

> 🚨 **VÌ SAO CÓ TỆP NÀY:** trong các vòng **69 · 75 · 77 · 79 · 83**, tôi **mất trọn vòng** vì **lỗi công cụ ĐO**
> — **không** phải vì nghiệp vụ khó. Mỗi lần là một lớp lỗi **khác nhau**, và mỗi lần tôi lại **ghi một bài học**…
> rồi **lần sau vấp lớp khác**. 📌 Đúng kết luận đã rút ở `handover` **Phụ lục C**: **ghi bài học KHÔNG ĐỦ — cần CÔNG CỤ**
> và **DANH MỤC KIỂM**. Tệp này là danh mục đó.

---

## A. TRƯỚC KHI CHẠY — 9 mục bắt buộc

| # | Mục kiểm | Vì sao (bài học đã trả giá) |
|---|---|---|
| **A1** | **Kiểm CÚ PHÁP trước**: `[System.Management.Automation.Language.Parser]::ParseFile(...)` | vòng 77: **ngoặc đơn lệch** ⇒ `ParserError` ⇒ **không dòng nào chạy**, mất một vòng |
| **A2** | **Với mã sản phẩm: dùng công cụ `edit`** — **KHÔNG** `Replace` chuỗi bằng PowerShell | vòng 86: ghép **nháy lồng** ⇒ **lỗi cú pháp C#**; vòng 69: **phẫu thuật theo số dòng** ⇒ ghi vào **đầu tệp** |
| **A3** | **Mỗi việc MỘT DÒNG** · **biến trung gian CÓ TÊN** · **IN giá trị trung gian** trước khi dùng | vòng 75: `Set-Content -PassThru` hiểu sai kiểu ⇒ **đứt chuỗi**; vòng 77: `scope` bị `.Replace` thành `eCommerce_offline_access` ⇒ **token bị từ chối** |
| **A4** | **Chờ tiến trình bằng `tools/wait-host.ps1`** (chờ `/health-status` = 200; hết hạn ⇒ **in đuôi log**) | vòng 79 · 83: chờ **điểm đo không tồn tại** cho Web ⇒ **"Web không lên"** — **kết luận SAI** đã vào tài liệu |
| **A5** | **Kiểm ĐIỂM ĐO có tồn tại không** — phép chờ hết hạn ở **MỌI lần chạy** là **dấu hiệu nghi ngờ ĐIỂM ĐO** | vòng 83–84: `/health-status` là của **Host**; Web phải **được thêm** (nay đã có) |
| **A6** | **Trước khi viết lời gọi API: in route THẬT** bằng `tools/route-check.ps1 -Match '<mẫu>'` | lỗi **sai route** đã tái phạm **3 lần** (`PRC-001` §19.7 · `inv-001` §33.1 · `web-001` §53.2) |
| **A7** | **KHÔNG lọc `stderr`** | `inv-001` §39: lỗi SQL bị **che mất** ⇒ kết luận **sai về mã** |
| **A8** | **Đo ĐIỀU KIỆN TIÊN QUYẾT** trước khi kết luận (dữ liệu gieo **có** / dữ liệu rỗng **thật**) | `inv-001` §39 (gieo hỏng) · `web-001` §63.1 (ca rỗng) |
| **A9** | **KHÔNG phẫu thuật probe cũ** ⇒ **viết probe MỚI** (một mục đích) | vòng 69 · 75 · 86 — sửa probe cũ **luôn** tốn hơn viết mới |

## B. KHI ĐỌC KẾT QUẢ — 4 mục

| # | Mục kiểm | Vì sao |
|---|---|---|
| **B1** | **In `HTTP STATUS` TRƯỚC**, rồi mới tới body | `404`/`405` từng bị đọc thành *"rỗng"* / *"khác kiểu JSON"* (§52.3) |
| **B2** | `Invoke-WebRequest` **TỰ ĐI THEO REDIRECT** ⇒ đọc **`StatusCode` trần** là **sai**; dùng `-MaximumRedirection 0` hoặc **URL cuối** | §47: suýt báo **hồi quy giả** cho cổng đăng nhập |
| **B3** | 🚨 **Phép kiểm VĂN BẢN THÔ trên HTML** (`substring`, regex `[^<]*`) là **nguồn kết luận SAI** — đã **3 lần** (§48 · §52.3 · §57.3). Dùng **khớp tới thẻ đóng** rồi **bỏ thẻ**, hoặc kiểm **sự hiện diện** của chuỗi nghiệp vụ | token **ngẫu nhiên** và `<strong>` làm phép trích cho **rỗng** |
| **B4** | **Sau khi "khôi phục", LUÔN `git diff`** | `Copy-Item`/script **báo thành công** mà tệp **không** về đúng trạng thái (§46.3) |

## C. KHI PHÉP ĐO PHỨC TẠP HỎNG ≥ 2 LẦN

> **Bài học #13 (`web-001` §69.4):** *đừng sửa nó — **tách thành phép đo nhỏ nhất còn trả lời được câu hỏi**.*

Ví dụ thật: 4 vòng cố chạy lại **toàn bộ đường UI → API** đều hỏng; vòng 81 **thắng ngay lần đầu** khi **bỏ Web** và
chỉ giữ **biến số cần kiểm** (định dạng số đơn `WEB-…`).
📌 **Không** làm bằng chứng yếu đi — nó **mạnh hơn**, vì **cô lập được nguyên nhân**.

## D. DỌN DẸP — 3 mục

| # | Mục | Vì sao |
|---|---|---|
| **D1** | Ghi SQL vào **tệp `.sql`** rồi chạy (`docker exec -i … psql`) — **KHÔNG** nhồi SQL có **nháy lồng** vào chuỗi PowerShell | `web-001` §52.3: `syntax error` làm **dữ liệu thử ở lại DB** |
| **D2** | **`SELECT` xác nhận sau khi dọn** (đếm bảng liên quan + **cờ bảo mật** `ShouldChangePasswordOnNextLogin = true`) | để không làm nhiễu phép đo sau |
| **D3** | **Kiểm tiến trình `dotnet` treo** (lọc `CommandLine` chứa `eCommerce`) | tránh cổng bị chiếm ở vòng sau |

---

## E. 🔐 LUẬT CHO **FORM WEB** (bài học vòng 65 · 86)

🚨 **Mọi `POST` từ trang Web PHẢI mang token chống giả mạo** — thiếu ⇒ **ABP trả `400`** và **nút bấm không hoạt động**.

| Trường hợp | Cách làm ĐÚNG |
|---|---|
| Trang viết bằng **`.cshtml`** | dùng **`@Html.AntiForgeryToken()`** trong **mỗi** `<form method="post">` |
| Trang render **trong C#** | inject **`IAntiforgery`** ⇒ `GetAndStoreTokens(HttpContext).RequestToken` ⇒ chèn `<input name="__RequestVerificationToken" type="hidden" value="…" />` vào **mỗi** form |
| **Probe gửi POST** | **trích** `__RequestVerificationToken` từ trang GET (cùng session) rồi **gửi kèm** trong body |

📌 **Triệu chứng nhận biết:** `POST` ⇒ **`400`** (không phải `403`/`302`) và **trạng thái trong DB KHÔNG đổi**.
📌 **Đừng** tự sinh GUID ngẫu nhiên làm token — vòng 65 tôi đã làm vậy và mất một vòng.

---

## F. HAI CÔNG CỤ ĐÃ CÓ (dùng, đừng viết lại)

| Công cụ | Việc |
|---|---|
| **`tools/wait-host.ps1`** | chờ `/health-status` = 200 · hết hạn ⇒ **in 15 dòng cuối log** rồi mới ném lỗi · `-ProcessId` phát hiện tiến trình đã thoát |
| **`tools/route-check.ps1`** | in **route THẬT** từ `swagger.json` + gọi thử và **in HTTP STATUS trước body** + cảnh báo khi `404`/`405` |

📌 **Điểm đo nay đã THỐNG NHẤT:** **cả Host (:5000) lẫn Web (:5100) đều có `/health-status`** (`web-001` §71)
⇒ dùng **cùng một lệnh** cho mọi tiến trình.
