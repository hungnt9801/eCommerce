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

---

## G. 🐛 HAI LỖI PROBE **MỚI** PHÁT HIỆN Ở VÒNG 94 (đọc trước khi viết hàm trợ giúp)

| # | Lỗi | Triệu chứng | Cách tránh |
|---|---|---|---|
| **G1** | Đặt tên tham số/biến là **biến tự động chỉ-đọc** của PowerShell | ``WriteError: Cannot overwrite variable pid because it is read-only or constant`` ⇒ **hàm ném lỗi ngay dòng gọi đầu tiên** ⇒ **không có gì chạy** | **KHÔNG** dùng: ``$pid`` · ``$home`` · ``$host`` · ``$input`` · ``$args`` · ``$error`` · ``$matches`` · ``$profile`` … Đặt tên rõ nghĩa: ``$idTienTrinh`` · ``$duongDanNha`` … |
| **G2** | Hàm trích dòng gọi ``.Trim()`` trên **``$null``** | ``Where-Object: You cannot call a method on a null-valued expression`` ⇒ **probe đứt giữa đường** | Viết hàm chịu được rỗng: ``$dong = $ket \| Where-Object { $_ -and $_.Trim() } \| Select-Object -First 1; if ($null -eq $dong) { return '' }`` — **kiểm ``$_ -and`` trước khi gọi phương thức** |

📌 **Điểm chung của G1 và G2 (bài học #11 nhắc lại lần thứ ba):** *hàm trợ giúp là nơi lỗi ẩn náu.* Một hàm trợ giúp viết ẩu
làm **cả probe** đứt ở chỗ **không liên quan** tới điều đang đo — và người đọc dễ **kết luận sai về sản phẩm**.
⇒ **Quy tắc:** hàm trợ giúp phải **chịu được đầu vào rỗng**, và **tên biến không được trùng biến hệ thống**.

📌 **Và một ghi nhận tích cực:** ở **cả hai** lần hỏng của vòng 94, quy tắc mới
(*``git commit`` phải nằm **TRONG** ``if (đạt)``*) **đã chặn được hồ sơ sai** — **không** tài liệu, **không** commit nào được ghi.
Đây là **lần đầu** một quy tắc viết ra **chứng minh giá trị ngay trong vòng kế tiếp**.

---

## H. 🚨 HAI CÁI BẪY **NGHIÊM TRỌNG** PHÁT HIỆN Ở VÒNG 97 (đọc trước khi chèn mã / kiểm build)

### H1. 🚨 **`dotnet build` … `Select-String "0 Error"` LÀ PHÉP KIỂM KHÔNG ĐÁNG TIN**

``dotnet build`` in **tổng kết theo TỪNG DỰ ÁN**. Một dự án **thành công** vẫn in ``0 Error(s)`` ⇒ khớp mẫu ``0 Error``
**dù một dự án KHÁC đang lỗi**. Vòng 97: tôi đọc ``build OK: True`` từ phép kiểm này, **trong khi** lời gọi thật có
**80 dòng lỗi** ở ``DatHangController.cs`` ⇒ tôi **tưởng build xanh** và **tiêu mất hai vòng** (chạy probe rồi mới phát hiện
``eCommerce.Web.dll`` **không tồn tại**).

**⇒ Phép kiểm ĐÚNG (dùng từ nay):**
| Cách | Ghi chú |
|---|---|
| 🔥 ``$LASTEXITCODE -eq 0`` **sau** ``dotnet build`` | **nguồn sự thật** — dùng cái này **trước tiên** |
| 🔥 Đếm ``': error '`` (có tiền tố **đường dẫn:dòng**) | ``($b \| Select-String -Pattern ': error ' \| Measure-Object).Count`` ⇒ mong đợi **``0``** |
| 🔥 Kiểm **tệp đầu ra TỒN TẠI** (``eCommerce.Web.dll`` / ``eCommerce.HttpApi.Host.dll``) | lỗi biên dịch ⇒ **không** sinh DLL ⇒ probe **chết ngay** với thông báo *"command or file was not found"* — **dễ đọc sai thành "app không lên"** |

📌 **Và đừng quên in lại ĐẦU RA của công cụ chờ:** vòng 97 tôi viết ``ChoLen`` **chỉ in ``exit code``** ⇒ **nuốt mất** thông báo
``Could not execute because the specified command or file was not found``. **Đúng lỗi §68.3 mà ``wait-host.ps1`` sinh ra để chống** —
bị **vô hiệu ngay tại chỗ dùng**. ⇒ **Luôn in lại đầu ra của công cụ chờ.**

### H2. 🚨 **KHÔNG chèn CÂU LỆNH vào giữa OBJECT INITIALIZER**

Vòng 97 tôi chèn khối ``StringBuilder`` **ngay trước** dòng
``Items = GioHelper.PhanTichGio(form.Sku, form.SoLuong, thue)`` — nhưng dòng đó nằm **TRONG** ``new CheckoutInput { … }``
⇒ chèn **statement** vào giữa **danh sách khởi tạo** ⇒ **lỗi cú pháp C#** (``Syntax error, ',' expected`` · ``} expected`` · 80 dòng lỗi).
📌 **Quy tắc:** trước khi chèn mã, **xác định NGỮ CẢNH** của dòng neo — nó là **câu lệnh**, **khởi tạo đối tượng**, hay **biểu thức**?
Nếu là **initializer** ⇒ đặt mã **TRƯỚC** ``new …`` (hoặc **trước** câu lệnh chứa nó), **không** chen vào giữa.
📌 **Cách kiểm ngữ cảnh rẻ nhất:** xem **thụt lề** và **các dấu ``{`` ``}`` ``(`` ``)`` chưa đóng** trong 5–10 dòng quanh dòng neo.

### H3. 🔁 **Giá trị của việc HOÀN NGUYÊN**

Vòng 97 **không** commit gì cho tính năng (cây làm việc bị **hoàn nguyên** » **``git status`` sạch** » **build lại ``0`` lỗi, ``exit 0``**,
``eCommerce.Web.dll`` **có**). ⇒ Repo về **đúng trạng thái đã kiểm chứng gần nhất (183 commit)** thay vì mang theo mã **không biên dịch được**.
📌 **Bài học:** ``git checkout -- <tệp>`` là cách **rẻ nhất** để thoát khỏi một bản sửa hỏng **chưa commit** — **nhanh hơn** sửa tiếp.

---

## I. 🚨 **PROBE + SETTING CỦA ABP + LOG TIẾNG VIỆT** (vòng GĐ1 `INV-003`/`REC-001`, 16/10/2026)

> **Vì sao có mục này:** phép kiểm hạn mức theo kênh cho **4 ca SAI** (F2 · G2 · G3 · H2a) và **không có lỗi nào**
> chỉ ra — nguyên nhân nằm **hoàn toàn ở PROBE**, không ở sản phẩm. 📌 Đúng kết luận Phụ lục C: *ghi bài học
> **không đủ** — cần **danh mục** để lần sau không vấp lại.*

| # | Bẫy | Triệu chứng **đo được** | Cách tránh |
|---|---|---|---|
| **I1** | 🔴 **Mẫu key cache setting SAI** | Xoá cache bằng `--pattern '*TenSetting*'` ⇒ **`(không có)`** (0 key) và **giá trị CŨ vẫn có hiệu lực** ⇒ 4 ca sai | Key THẬT: `c:Volo.Abp.SettingManagement.Setting,*` + `*AbpInMemorySettingCacheStamp*`. 🔒 **IN RA SỐ KEY đã xoá** — `0 key` là **dấu hiệu MẪU SAI**, không phải *"không có gì để xoá"* |
| **I2** | 🔴 **Cache tầng 2 TRONG BỘ NHỚ tiến trình** | Xoá Redis **không đủ**: setting vẫn là giá trị cũ cho tới khi Host khởi động lại | Đổi setting ⇒ **xoá cache + KHỞI ĐỘNG LẠI Host**; hoặc tách **mỗi nhóm chính sách = một phiên Host** (probe `INV-003` làm đúng vậy: 5 phiên) |
| **I3** | 🔴 **`UPDATE 0` là BẰNG CHỨNG, không phải lỗi vặt** | Đổi `Recon.Mismatch.Action` ⇒ **`UPDATE 0`** (setting **không có dòng nào** trong `AbpSettings`; giá trị đến từ `SettingDefinitionProvider`) ⇒ lượt quét ghi `KetLuan = 1` **dù đã "đổi"** | **ĐỌC SỐ DÒNG bị ảnh hưởng** sau mỗi `UPDATE`/`DELETE`. Giá trị **mặc định** của setting **KHÔNG nằm trong DB** ⇒ `SELECT` rỗng nghĩa *"đang dùng mặc định trong mã"*, **không** nghĩa *"chưa đặt"*. Muốn đổi ⇒ **`INSERT`** |
| **I4** | 🔴 **Log có tiếng Việt ⇒ KHÔNG tìm được bằng chuỗi có dấu** | `Select-String 'HẠN MỨC KÊNH'` ⇒ **0 dòng**, nhưng dòng đó **CÓ** trong log (`H?N M?C` — console logger làm hỏng dấu) | **Đặt NEO ASCII trong chính câu log** (`[INV003-HANMUC-AP-DUNG]` · `[REC001-CO-LECH]`) rồi tìm bằng neo. 📌 Neo là **một phần của mã sản phẩm** ⇒ phải thêm vào `Logger.*` **ngay khi viết log**, không phải sửa probe |
| **I5** | 🔴 **Điều kiện tiên quyết của DỮ LIỆU GIEO bị bỏ qua** | Dòng sổ cái gieo **thất bại** (sai tên cột) mà probe vẫn chạy ⇒ ca "đối soát đúng" và ca "dữ liệu gieo hỏng" cho **cùng kết quả** | **ĐỌC LẠI** điều kiện tiên quyết **sau khi gieo** và **in ra** (`A5 SOCAI_THUONG=1000`). ⚠️ Đã dính **hai lần trong cùng một vòng**: sai `ExtraProperties` **rồi** sai `ConcurrencyStamp` — vì **đoán** tên cột ⇒ **luôn `information_schema.columns`** trước khi viết `INSERT` |
| **I6** | 🔴 **`$ketQua.Add($f ...)` + `$f \| Out-Null`** | Hàm trả về object ⇒ **in cả bảng thuộc tính** ra output; dùng `\| Out-Null` để chặn thì **chặn luôn** các `Write-Output` **bên trong** ⇒ **không một dòng kết quả nào** để đọc | Hàm **ghi vào một `List` tham chiếu** (tham số `-Mang`) và **KHÔNG `return`** object; điều khiển việc in bằng **`-Im`** |
| **I7** | 🔴 **Hết hàng giữa chừng làm hỏng phép đo** | Các ca `Warn`/`PhamVi` trả `0001` (*không đủ hàng*) ⇒ phép đo **không trả lời được** câu hỏi đang hỏi | **Cô lập biến số:** phép đo *"hạn mức nào đang áp"* chỉ có nghĩa khi **tồn khả dụng DƯ** (gieo `1000`, không phải `40`) |

📌 **Ba quy tắc gọn để dán vào đầu mọi probe có setting/log:**

1. Đổi setting ⇒ **xoá ĐÚNG key + khởi động lại Host** + **in số key đã xoá** + **in số dòng UPDATE**.
2. Log nghiệp vụ ⇒ **neo ASCII trong chính câu log**, tìm bằng neo.
3. Gieo dữ liệu ⇒ **đọc lại bằng `SELECT`** và **in điều kiện tiên quyết**; **tra `information_schema.columns`** trước khi `INSERT`.


---

## 🚨 `kiem-moc-build.ps1` — CHỐNG BẪY *"build `exit 0` mà binary KHÔNG ĐỔI"*

### Vì sao có script này (nó **KHÔNG** phải tiện nghi — nó là **kết quả của BỐN LẦN TRẢ GIÁ**)

`dotnet build` có thể trả **`exit 0`** + in **`Build succeeded.`** ⚠️ nhưng **KHÔNG ghi** nhi phân, vì một
tiến trình (Host/Web) **đang chạy và đang giữ tệp**. 📌 Triệu chứng **duy nhất** là:
**mã nguồn ĐÚNG, DLL CŨ** ⇒ test / phép kiểm **ĐỎ vì LÝ DO SAI** ⇒ 🔒 người sửa **kết luận sai về SẢN PHẨM**.

| Lần | Ở đâu | Chuyện gì xảy ra |
|---|---|---|
**1** | `web-001` §97.6 | Build `exit 0`, **mốc DLL không đổi** |
**2** | `web-001` §98.6 | Chạy test trên **binary cũ** ⇒ **7 ca đỏ** dù mã đã đúng |
**3** | `sal-006` §21.6 | Job **không có neo nào**; tôi tưởng **đăng ký hỏng**, thật ra **Host giữ DLL cũ** |
**4** | `sal-006` §22.7 | Khôi phục mã xong test **vẫn đỏ** ⇒ suýt kết luận *"khôi phục không sạch"* |

➡️ 🔒 **Bốn lần.** Một **quy tắc trong đầu** không đủ ⇒ phải là **script**.

### 🚨 Bài học quan trọng nhất — kiểm **BẢN COPY TRONG `bin` CỦA TIẾN TRÌNH SẼ CHẠY**

```
src\eCommerce.Application\bin\...\eCommerce.Application.dll          ← bản trong THƯ MỤC DỰ ÁN
src\eCommerce.HttpApi.Host\bin\...\eCommerce.Application.dll         ← 🚨 BẢN ĐƯỢC NẠP (thường MỚI HƠN)
src\eCommerce.Web\bin\...\eCommerce.Application.dll
```
📌 Kiểm mốc của bản **dự án** mà kết luận về bản **Host** là **sai công cụ** — đúng lỗi đã xảy ra ở `sal-006` §21.6.

### Cách dùng — **ba bước**, và bước ① **phải trước** khi build

```powershell
# ① Ghi nhớ mốc TRƯỚC khi build
pwsh -File tools\kiem-moc-build.ps1 -GhiNho

# ② Build (nên có --no-incremental khi nghi ngờ)
dotnet build eCommerce.slnx -v m --no-incremental /nodeReuse:false

# ③ Kiểm mốc SAU khi build — THOÁT KHÁC 0 nếu binary KHÔNG đổi
pwsh -File tools\kiem-moc-build.ps1 -Kiem
```

**Lọc theo tệp cụ thể** (nhanh hơn, ít nhiễu):
```powershell
pwsh -File tools\kiem-moc-build.ps1 -GhiNho -Ten 'eCommerce.Application.dll'
# ... build ...
pwsh -File tools\kiem-moc-build.ps1 -Kiem -Ten 'eCommerce.Application.dll'
```

### Mã thoát — 🔑 **HAI nguyên nhân KHÁC NHAU phải có HAI mã KHÁC NHAU**

| Mã | Nghĩa | Phải làm gì |
|---|---|---|
**0** | ✅ **Mọi tệp đều ĐỔI mốc** ⇒ bản build **ĐÃ VÀO** | Tin test/kiểm chứng được |
**1** | 🚨 **Có tệp KHÔNG ĐỔI** ⇒ bản build **KHÔNG VÀO** | **Dừng** Host/Web, chờ cổng `5000`/`5100` **TRỐNG**, build lại với `--no-incremental`, **kiểm mốc lại**. 🔒 **Đừng** kết luận gì về sản phẩm khi mốc chưa đổi |
**2** | ⚠️ **Phép kiểm VÔ NGHĨA** (không tìm thấy tệp nào, hoặc chưa có mốc) | Sửa `-ThuMuc`/`-Ten`, hoặc chạy `-GhiNho` trước |

🔒 **VÌ SAO TÁCH MÃ 1 VÀ 2:** gộp lại thì người đọc log **không phân biệt** *"bản build không vào"* với
*"phép kiểm sai"* — ⇒ 📌 và sẽ đi **sửa SAI CHỖ**. (📌 Đây cũng là bài học của chính lượt viết script:
`Write-Error` của PowerShell ép **exit 1** ⇒ tôi phải dùng `Write-Output` + `exit 2`.)

➕ **Tệp mốc `.moc-build.json`** nằm ở **gốc repo** và **đã** vào `.gitignore` (nó là **công cụ kiểm chứng**,
không phải mã nguồn).

---

## 🚨 `kiem-tat-ca.ps1` — **MỘT LỆNH, NĂM BƯỚC**, đúng exit code

### Vì sao có script này

Trước nó, **mỗi vòng làm việc** phải chạy **năm bước bằng tay**: build · kiem moc · unit test · integration
test · mermaid. ⚠️ Và mỗi bước đều có **bẫy riêng đã cắn người**:

| Bẫy | Hậu quả |
|---|---|
🚨 **`dotnet build … \| Select-String`** | 🔒 **CHE MẤT exit code** (`$LASTEXITCODE` khi đó là của `Select-String`, **luôn 0**) ⇒ *"build 0 lỗi"* có thể là **SAI** |
🚨 **`build exit 0` mà binary KHÔNG ĐỔI** | Test **ĐỎ vì LÝ DO SAI** ⇒ kết luận sai về **SẢN PHẨM** (**BỐN LẦN**: `web-001` §97.6 · §98.6 · `sal-006` §21.6 · §22.7) |
⚠️ **Quên một bước** | 📌 *"tất cả xanh"* nhưng thực ra **chưa chạy** bước đó — và **không ai** biết |

✅ Script gom lại: **một lệnh, năm bước, exit code thật, DỪNG NGAY khi hỏng.**

### Cách dùng

```powershell
# Vòng lặp nhanh trong lúc sửa
pwsh -File tools\kiem-tat-ca.ps1 -BoQuaIntegration

# Trước khi commit (mặc định)
pwsh -File tools\kiem-tat-ca.ps1

# Đầy đủ (thêm E2E có phiên — ⚠️ hiện BÁO LỖI vì probe E2E vẫn là script tay)
pwsh -File tools\kiem-tat-ca.ps1 -DayDu
```

**Năm bước:** ① dọn tiến trình + chờ cổng `5000`/`5100` **TRỐNG** → ② build **+ kiểm mốc nhi phân** →
③ unit test → ④ integration test → ⑤ mermaid.

### 🔒 Hai quy tắc mà **chính script** thi hành (không phải lời khuyên)

**① Không bao giờ ống dẫn output của lệnh mà ta cần exit code.** Mỗi lệnh ghi ra **tệp tạm**, rồi đọc tệp
đó ⇒ ✅ `$LASTEXITCODE` luôn là của **chính lệnh**. (Hàm `ChayLenh` dùng `*>` chứ **không** dùng `|`.)

**② Build xong **phải** kiểm mốc nhi phân.** 🚨 `exit 1` ⇒ **bản build KHÔNG VÀO** ⇒ 🔒 **DỪNG** ngay,
**không** chạy test (test trên binary cũ sẽ đỏ vì **LÝ DO SAI** và làm người sửa **kết luận sai về sản phẩm**).

➕ **Bước ① cũng là một cổng:** nó **dừng** tiến trình eCommerce cũ và **chờ cổng trống** — 📌 vì một tiến
trình đang chạy **giữ tệp** là **nguyên nhân gốc** của cả **bốn lần** trả giá ở trên.

➕ **Bước E2E `-DayDu` BÁO LỖI thay vì im lặng bỏ qua** — 🔑 một bước *"im lặng bỏ qua"* nguy hiểm hơn một
bước **báo lỗi**: 📌 nó khiến người ta tin rằng **đã** kiểm. 🚨 Đây chính là **nợ đã ghi**
(`web-001` §98.7 #1: probe E2E vẫn là **script tay**).
