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

---

## 🚨 `kiem-db.ps1` — **CỔNG BẤT BIẾN DB** (bước ⑥ của `kiem-tat-ca.ps1`)

### Vì sao có script này

Mỗi vòng làm việc, tôi vẫn kiểm DB **bằng tay** (5–6 câu `psql`) trước khi commit. ⚠️ Và đó là **bước dễ
quên nhất** — 📌 quên nó thì *"tất cả xanh"* nhưng **dữ liệu thử nghiệm còn nằm trong DB**, và lần sau một
phép đo sẽ **đọc phải chúng** rồi **kết luận sai**.

➕ **Và đây là bước DUY NHẤT chạm vào TRẠNG THÁI THẬT** (không phải mã nguồn): mermaid kiểm **tài liệu**,
test kiểm **mã**, còn bước này kiểm **cái đang nằm trong database**.

### 🚨🚨 RANH GIỚI BẤT BIẾN — bài học quan trọng nhất

| Nhóm | Bảng | Kỳ vọng |
|---|---|---|
**① PHẢI RỖNG** (dấu vết thử nghiệm) | `AbpSettings` · `CusCustomers` · `CusGroups` · `CatProductMedia` · `CatProductChannelVisibilities` · `MsgDeadLetters` · `SalMarketplaceOrderIntakes` · `InvChannelLimits` · `PayTransactions` · `ReconRunMismatches` | **0 dòng** |
**② GIỮ LỊCH SỬ** (🚨 **KHÔNG** được coi là rác) | **`ReconRuns`** | 🔑 **con số TĂNG DẦN là ĐÚNG** |

🔒 **Vì sao nhóm ② quan trọng:** `ReconRuns` là **nhật ký lượt quét đối soát** — job
`InventoryReconciliationWorker` ghi **một dòng mỗi nhịp**. 📌 Đặt một phép kiểm *"phải rỗng"* lên nó sẽ
**ĐỎ** sau vài giờ chạy job, rồi người ta sẽ **vô hiệu hoá phép kiểm** — hoặc tệ hơn: **xoá dữ liệu thật**
để cho nó xanh. 🔒 Xoá `ReconRuns` là **xoá bằng chứng kiểm toán**.

🔑 **Bài học chung:** một phép kiểm *"bảng phải rỗng"* chỉ đúng với bảng **không có ý nghĩa nghiệp vụ**.
⚠️ Áp nó lên bảng **có lịch sử** là **tự tạo ra một cổng luôn đỏ** — 📌 và **cổng luôn đỏ thì người ta học
cách bỏ qua nó** (cùng bài học `sal-006` §23.4).

### Năm nhóm bất biến

| Nhóm | Kiểm gì |
|---|---|
**①** | Bảng phải rỗng — ⚠️ đếm **CẢ** bản ghi **XOÁ MỀM** (`IsDeleted`). 📌 Một dòng xoá mềm **không** hiện trong truy vấn thường nhưng **vẫn chiếm** unique index `NULLS NOT DISTINCT` ⇒ lần gieo sau **thất bại vì lý do khó hiểu** |
**②** | `AbpUsers.NormalizedUserName = 'ADMIN'` phải có `ShouldChangePasswordOnNextLogin = true`. 🚨 Probe **tắt** cờ này để đăng nhập ⇒ 🔒 quên bật lại = commit một **admin không bị buộc đổi mật khẩu** |
**③** | Số unique index `NULLS NOT DISTINCT` — 🔑 đọc **từ chính test** (`F1_du_48…`), **không** viết cứng. ⚠️ Một con số cứng trong script sẽ **lệch** với con số trong test ⇒ **hai nguồn sự thật** |
**④** | Quyền bắt buộc phải còn (9 quyền, gồm `eCommerce.Pricing`, `eCommerce.Operations`, `eCommerce.ChannelLimits`…) — 🔑 **cơ sở** cho `WEB-001` (menu theo quyền) |
**⑤** | Dấu vết thử nghiệm: **mọi** tiền tố probe (`K9`, `k9`, `K97`) — 🔑 phép kiểm **mạnh nhất** vì bắt rác ở **mọi** bảng có cột mã, không chỉ 10 bảng ở nhóm ① |

### Mã thoát

| Mã | Nghĩa |
|---|---|
**0** | ✅ Mọi bất biến ĐẠT |
**1** | 🚨 Có bất biến **HỎNG** (kèm danh sách) |
**2** | ⚠️ **Phép kiểm VÔ NGHĨA** — không kết nối được PostgreSQL, hoặc đọc được **0 quyền**. 🔒 **KHÔNG** được coi là *"DB sạch"* |

➕ `-ChoPhepRong` (⚠️ **KHÔNG** dùng trước khi commit): cho phép bảng nhóm ① có dữ liệu, **cảnh báo** thay vì
thất bại. 📌 Dùng khi chạy trên DB **đang phát triển**.

### ✅ Đã tiêm lỗi để chắc cổng BẮT (3 ca, cả 3 đều ĐỎ)

| Tiêm | Kết quả đo |
|---|---|
Dòng **XOÁ MỀM** trong `InvChannelLimits` | ✅ ĐỎ — `TONG=1 · conSong=0 · xoaMem=1` ⇒ 🔑 **đếm thường sẽ bỏ sót ca này** |
`ShouldChangePasswordOnNextLogin = false` | ✅ ĐỎ + nhắc rõ *"Probe TẮT cờ này để đăng nhập"* |
Dòng `K97TIEM1` trong `PayTransactions` | ✅ **2** bất biến đỏ (nhóm ① **và** nhóm ⑤) |

---

## 🚨 NHÓM ⑥ CỦA `kiem-db.ps1` — **TỰ DÒ** dấu vết probe (thay danh sách viết tay)

### Vấn đề nó sửa

Nhóm ⑤ quét một **DANH SÁCH VIẾT TAY** (7 cặp bảng/cột). ⚠️ Danh sách đó **lệch** khi schema đổi: 🔑 bảng
**mới** thêm sẽ **không** được quét, và 📌 **không ai biết** — phép kiểm vẫn **XANH**.

📌 **Cùng một họ nợ** như con số `48` viết cứng (đã sửa ở nhóm ③) và danh sách *"phải rỗng"* (nhóm ①):
🔒 **danh sách thủ công sẽ lệch, và lệch thì IM LẶNG.**

### ✅ Cách sửa — **tự dò** thay vì liệt kê

🔑 **Dấu hiệu chung:** mọi dòng probe đều được gieo bằng một **GUID trong khối dành riêng**
`e0000000-0000-0000-0000-0000000000XX`. 📌 Đó là dấu hiệu **không phụ thuộc tên bảng/cột**:

```sql
-- Lấy MỌI cột khoá chính kiểu uuid
select c.table_name || '|' || c.column_name
from information_schema.columns c
join information_schema.table_constraints tc
  on tc.table_schema = c.table_schema and tc.table_name = c.table_name and tc.constraint_type = 'PRIMARY KEY'
join information_schema.key_column_usage kcu
  on kcu.constraint_schema = tc.constraint_schema
 and kcu.constraint_name = tc.constraint_name and kcu.column_name = c.column_name
where c.table_schema = 'public' and c.data_type = 'uuid';
```

Rồi ghép **MỘT** câu `UNION ALL` (`select '<bảng>.<cột>' as cho, count(*) … where "<cột>"::text like 'e0000000-%'`)
⇒ 📌 **một** lần đi-vòng qua DB thay vì 80 lần.
➕ **Và in ra số chỗ đã quét** (`daQuet=81 cho`) — 🔑 một phép kiểm mà **không nói nó quét bao nhiêu** thì
không ai biết nó có thật sự chạy hay không.

### 🔒 Vì sao quét **CỘT KHOÁ CHÍNH** (không phải mọi cột `uuid`)

| Lý do | Giải thích |
|---|---|
**Rẻ** | Mọi bảng có PK `uuid` ⇒ một lần quét **index** |
**Đúng** | Dòng probe **luôn** được gieo bằng `Id` **tường minh** (xem mọi probe PowerShell) ⇒ PK là dấu hiệu **đúng** |
**Không nhiễu** | Quét **mọi** cột `uuid` (FK, `CreatorId`, `TenantId`…) sẽ bắt cả dòng **thật** trỏ tới dòng probe ⇒ ⚠️ nhiễu và chậm |

⚠️ **Đánh đổi đã biết:** một dòng probe gieo **không** có `Id` tường minh (để DB sinh) sẽ **không** bị bắt bởi
nhóm này — 📌 nhưng nhóm ⑤ (tiền tố mã) vẫn bắt nó **nếu** bảng đó có cột mã.

### ✅ Đã kiểm chứng **giá trị thật** của nó

| Ca | Kết quả đo |
|---|---|
🚨 Tiêm dòng probe GUID vào **`AbpAuditLogs`** — bảng **KHÔNG có** trong danh sách viết tay | ✅ **BẮT** — `🚨 AbpAuditLogs.Id = 1 dong ⇒ RAC TU PROBE (phat hien TU DONG)` ⇒ 🔑 **đúng thứ mà danh sách viết tay SẼ BỎ SÓT** |
🚨 Tiêm *"metadata trả về 0 dòng"* (sửa câu metadata thành `and 1 = 0`) | ✅ **BẮT** — `🚨 Phep do metadata tra ve 0 cot ⇒ PHEP KIEM VO NGHIA` ⇒ 🔒 *"0 tìm thấy"* **KHÔNG** bị coi là *"sạch"* |
➕ DB sạch | ✅ `✅ Khong bang nao con dong mang GUID probe (da quet 81 cho)` · **21/21 bất biến ĐẠT** · `exit 0` |

---

## 🚨 QUY TẮC: DÙNG **HERE-STRING** CHO MỌI CÂU SQL — KHÔNG THOÁT NHÁY

### Vì sao (đo được 17/10/2026, `sal-006` §25.5)

Khi tiêm một dòng probe để kiểm cổng, tôi viết:

```powershell
# ❌ SAI — thoát nháy ('') BÊN TRONG một chuỗi có nháy
Psql "insert into ""AbpAuditLogs"" (""Id"",""ApplicationName"") values (''e0000000-...'',''eCommerce'');"
```

⚠️ Câu SQL bị **sai cú pháp**, `psql` báo lỗi — 📌 **và tôi đã bỏ qua output của `INSERT`**.
🔑 Kết quả: cổng kiểm tra báo **`exit 0`** (không bắt), và tôi **suýt kết luận** *"cơ chế tự dò không hoạt động"*.

🚨 **Sự thật:** **dòng tiêm KHÔNG HỀ TỒN TẠI** (`so dong = 0`).
📌 Tức *"cổng không bắt"* thực ra là ***"tôi không hề tiêm"***.

### ✅ Cách đúng

```powershell
# ✅ ĐÚNG — here-string (nháy đơn rồi a-còng), KHÔNG thoát nháy nào
Psql @'
insert into "AbpAuditLogs" ("Id","ApplicationName","ExecutionTime","ExecutionDuration","ExtraProperties","ConcurrencyStamp")
values ('e0000000-0000-0000-0000-0000000000aa','eCommerce',now(),1,'{}','k97x1');
'@
```

🔒 **Vì sao here-string giải quyết triệt để:** nó là **chuỗi nguyên văn** — 📌 không có phép thay thế biến,
không có xử lý thoát ⇒ câu SQL đi tới `psql` **đúng như ta viết**.

🚨 **CẢNH BÁO KÈM THEO (đã vấp ngay khi viết chính mục này):** **KHÔNG** đặt here-string **lồng trong**
here-string khác — 📌 dấu kết thúc của khối trong sẽ **kết thúc khối ngoài** ⇒ `ParserError`. 🔑 Muốn ghi một
tệp có chứa here-string thì dùng **công cụ sửa tệp** (`edit`), **không** dùng here-string của PowerShell.

### 🚨 HAI QUY TẮC KÈM THEO — quan trọng không kém

1. **Sau khi tiêm, PHẢI KHUẲNG ĐỊNH dòng đã vào** bằng một `SELECT` **độc lập**.
   📌 Nếu không thì *"cổng không bắt"* và *"tiêm không thành"* **trông GIỐNG HỆT NHAU** —
   🔒 và bạn sẽ đi **sửa cổng** trong khi lỗi ở **phép tiêm**.
2. **Phải KIỂM output/exit code của chính lệnh tiêm.** ⚠️ `psql` in `INSERT 0 1` khi thành công và
   **lỗi rõ ràng** khi hỏng — 📌 nhưng chỉ khi ta **đọc** nó.

📌 **Đây là mặt NGƯỢC LẠI của bài học `web-001` §97** (*"một ca ĐỎ có thể đỏ vì **TẦNG KHÁC** từ chối trước"*):
🔑 ở đây là *"một ca **XANH** có thể xanh vì **PHÉP TIÊM không xảy ra**"*.

---

## 🚨 `test-cong-kiem.ps1` — **TEST CHO CHÍNH CÁC CỔNG KIỂM CHỨNG**

### Vì sao có file này — **HAI LỖI THẬT, CẢ HAI TÌM RA NHỜ MAY MẮN**

| Lỗi | Chuyện gì xảy ra | Phát hiện nhờ |
|---|---|---|
🚨 `sal-006` **§23.4** | `kiem-moc-build.ps1` bị **VIẾT NGƯỢC ĐIỀU KIỆN** ⇒ 🔒 **cổng LUÔN ĐỎ** (báo *"BAN BUILD KHONG VAO"* **ngay cả khi mọi tệp ĐỀU ĐỔI**) | ⚠️ Tôi **tình cờ** chạy nó trên một bản build **vừa xong** |
🚨 `sal-006` **§25.5** | Phép **tiêm lỗi KHÔNG hề xảy ra** (sai cú pháp SQL do thoát nháy) ⇒ cổng báo **XANH** | ⚠️ Tôi **tình cờ** đo lại trạng thái thật |

🔒 **Và một cổng LUÔN ĐỎ thì người ta học cách BỎ QUA nó** ⇒ hỏng nó nghĩa là **quay lại** đúng cái bẫy mà nó
sinh ra để chống. ⇒ **Phải có test TỰ ĐỘNG, không phụ thuộc may mắn.**

### Hai quy tắc thiết kế

1. 🔒 **KHÔNG THÊM PHỤ THUỘC.** Máy này chỉ có **Pester 3.4.0** (API khác Pester 5), và cả toolchain trong
   `tools\` là **script thuần + exit code** ⇒ file này cũng thế: `Describe`/`It` **tự viết**.
   🔑 Thêm một framework ở đây là đổi **công cụ**, không phải thêm **giá trị**.
2. 🔒 **MỌI CA PHẢI TỰ DỌN DẸP.** Các ca "tiêm" tạo **dữ liệu thật**; ⚠️ nếu một ca **ném giữa đường** mà
   không dọn thì lần chạy sau **hỏng vì dữ liệu ca trước còn lại** ⇒ 🔑 một test **tự làm hỏng chính nó**.
   ✅ `try/finally` cho **mọi** ca có side effect.

### 22 ca — chia bốn nhóm

| Nhóm | Số ca | Chạm DB? |
|---|:--:|---|
| `kiem-moc-build.ps1` | 7 (`A`, `A2`, `B`…`F`) | ✖ |
| `kiem-link-tai-lieu.ps1` | 5 (`R`…`V`) — thêm 17/10/2026 | ✖ |
| `ham-tiem.ps1` | 5 (`L`…`P`) | ✖ |
| `kiem-db.ps1` | 5 (`G`…`K`) | ✅ cần Docker + PostgreSQL |

**Nhóm `kiem-moc-build.ps1` (7 ca, 📌 không chạm DB):**

| Ca | Bất biến |
|---|---|
🚨 **A** | `-GhiNho` → **BUILD** → `-Kiem` ⇒ mốc **ĐỔI** ⇒ **`exit 0`** ⇒ 🛡️ chống **cổng luôn đỏ** (§23.4). ⚠️ **Phải dùng `--no-incremental`**: build **tăng dần** không ghi lại binary ⇒ mốc **không đổi** ⇒ ca kiểm **không tất định** (🔑 và một ca **không tất định** thì **không phải lưới an toàn**) |
🚨 **A2** | `-GhiNho` → **KHÔNG** build → `-Kiem` ⇒ mốc **không đổi** ⇒ **`exit 1`** ⇒ 🛡️ chống **cổng luôn xanh** (chiều đối xứng) |
**B** | Mốc **cũ** (lùi 2020) ⇒ tệp đổi ⇒ `exit 0`, **không** báo động |
**C** | Mốc **rỗng** ⇒ `exit 2` — ⚠️ **KHÔNG** được coi là *"ĐÃ VÀO"* |
**D** | Mốc **sai định dạng** (object đơn) ⇒ `exit 2` — 🚨 khóa đúng bẫy *"`foreach` duyệt thuộc tính ⇒ bảng tra cứu rỗng ⇒ mọi tệp thành MỚI ⇒ cổng XANH dù binary không đổi"* |
**E** | **Chưa có** mốc ⇒ `exit 2` |
**F** | **Lọc rỗng** (không tệp nào khớp) ⇒ `exit 2` — 🛡️ *"không tìm thấy tệp nào"* **KHÔNG** được coi là *"mọi tệp đều đổi"* |

**Nhóm `kiem-link-tai-lieu.ps1` (5 ca, 📌 không chạm DB — thêm 17/10/2026):**

| Ca | Bất biến |
|---|---|
**R** | Tài liệu **sạch** (link trỏ tới tệp **có thật**) ⇒ `exit 0` ⇒ 🛡️ chống **cổng luôn đỏ** |
🚨 **S** | Một link markdown **chết** ⇒ `exit 1` ⇒ 🛡️ chống **cổng luôn xanh**. 🔑 Ca này **còn khẳng định log NHẮC TỚI đúng tên tệp chết** — vì *"cổng bắt được"* và *"cổng đỏ vì lý do khác"* **trông giống hệt nhau** (bài học §25.5) |
🚨 **T** | Một đường dẫn trong **backtick** chết (`` `docs/….md` ``) ⇒ `exit 1` — 🔒 **luật (B) KHÁC luật (A)**: trong repo này backtick là kiểu **phổ biến nhất** ⇒ chỉ kiểm link markdown là **lọt lưới** |
**U** | Chuỗi trông giống đường dẫn nhưng **không phải** (ví dụ minh hoạ `src/…/x.cs`, URL `https://…/x.md`) ⇒ **KHÔNG** báo sai — 🔒 **ca chống DƯƠNG TÍNH GIẢ**: một cổng báo sai sẽ bị người ta **bỏ qua** |
🚨 **V** | **Link nằm TRONG code span** (dạng `` `` `[chữ](…)` `` ``) ⇒ **KHÔNG** phải liên kết ⇒ `exit 0` — 📌 **ca này sinh ra từ MỘT LỖI THẬT** ngay khi viết chính tệp `README` này: tài liệu mô tả *"định dạng link"* bằng cách **viết link trong backtick**, và bản sửa đầu (regex một-backtick) **ghép SAI** khi delimiter là **hai** backtick ⇒ nội dung bên trong **lọt lưới** |

**Nhóm `kiem-db.ps1` (5 ca, ⚠️ cần Docker + PostgreSQL):**

| Ca | Bất biến |
|---|---|
**G** | DB sạch ⇒ **0** bất biến hỏng ⇒ `exit 0` |
🚨 **H** | Dòng **XOÁ MỀM** trong bảng *"phải rỗng"* ⇒ `exit 1` — 🔑 **ca đáng giá nhất**: một phép **đếm thường sẽ BỎ SÓT** ca này |
**I** | Cờ bảo mật admin `= false` ⇒ `exit 1` |
🚨 **J** | Dòng probe GUID trong **`AbpAuditLogs`** (bảng **KHÔNG** có trong danh sách viết tay) ⇒ **nhóm ⑥ TỰ DÒ bắt** |
**K** | Sau **mọi** ca tiêm ⇒ DB **sạch lại** ⇒ `exit 0` — 🔒 ca này **bảo vệ chính bộ test** |

➕ 🔒 **Mọi câu SQL trong file này là HERE-STRING** ⇒ **không có phép thoát nháy nào** (bài học §25.5).
➕ 🚨 **Mỗi ca tiêm phải KHUẲNG ĐỊNH dòng đã vào** bằng một `SELECT` độc lập — 📌 nếu không thì
*"cổng không bắt"* và *"tiêm không thành"* **trông GIỐNG HỆT NHAU**.

### ✅ Đã kiểm giá trị thật

🚨 Tiêm lại **đúng lỗi §23.4** (đảo điều kiện) ⇒ ✅ **3 ca ĐỎ** (A, A2, B) ⇒ 🔑 **bộ test THẬT SỰ BẮT**.
📌 Và **lần tiêm đầu KHÔNG xảy ra** (phép thay chuỗi không khớp) ⇒ ⚠️ tôi **suýt** kết luận *"test không bắt
được gì"* — 🔒 **lần thứ HAI** của bài học §25.5, **cách nhau đúng một vòng**.

---

## 🚨 `ham-tiem.ps1` — **THƯ VIỆN TIÊM: TỰ KHẲNG ĐỊNH sau mỗi phép tiêm**

### Vì sao — **CÙNG MỘT LỖI, HAI VÒNG LIÊN TIẾP**

| Vòng | Chuyện gì xảy ra | Hậu quả |
|---|---|---|
`sal-006` **§25.5** | Tiêm dòng probe bằng `INSERT` có **thoát nháy** ⇒ **sai cú pháp SQL** | ⚠️ Dòng **KHÔNG hề tồn tại** ⇒ suýt **kết luận sai về sản phẩm** |
`sal-006` **§26.5 #1** | Tiêm lại lỗi bằng `String.Replace` và **phép thay KHÔNG khớp** | ⚠️ Bộ test báo `exit 0` ⇒ suýt kết luận *"bộ test không bắt được gì"* |

🔒 **ĐIỂM CHUNG:** phép tiêm **KHÔNG XẢY RA**, nhưng **không ai kiểm** ⇒ và *"cổng không bắt"* **trông GIỐNG
HỆT NHAU** với *"tiêm không thành"*. 📌 Vì vậy một ca **XANH** có thể xanh **vì lý do sai**.

✅ **CÁCH CHẶN:** mọi hàm trong thư viện này **TỰ KHẲNG ĐỊNH** rằng phép tiêm **ĐÃ XẢY RA** — 🔑 và **NÉM**
nếu không. 📌 Từ đây, *"tiêm không thành"* trở thành một **lỗi ồn ào**, không còn là một **giả định ngầm**.

### Cách dùng — **dot-source**, không chạy trực tiếp

```powershell
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')     # nạp các hàm

$t = TiemVaoTep -DuongDan $f -KhoiCu $cu -KhoiMoi $moi -NeoXacNhan 'K97TIEM'
try { ... chạy phép kiểm ... } finally { KhoiPhucTep -TayTiem $t -NeoDaTiem 'K97TIEM' }
```

⚠️ **KHÔNG** đặt `param` ở cấp tệp: 🔑 một tệp có `[CmdletBinding()]` ở cấp cao nhất sẽ **đòi tham số** khi bị
dot-source ⇒ 📌 nó **treo** hoặc **lỗi** tuỳ ngữ cảnh gọi. ✅ Tệp này **chỉ có hàm**.

### Năm hàm — và mỗi hàm khẳng định điều gì

| Hàm | Tự khẳng định |
|---|---|
**`TiemVaoTep`** | 🔑 Khối cũ **phải khớp ĐÚNG MỘT LẦN** (⚠️ khớp **0** lần = phép tiêm không xảy ra; khớp **>1** lần = `Replace` thay **hết** ⇒ vượt ý định). ➕ Nội dung **phải đổi**. ➕ Nếu có `-NeoXacNhan` thì neo **phải có** trong nội dung mới. ➕ **Đọc lại từ đĩa** — ⚠️ tệp bị **khoá** có thể **ghi hụt im lặng** (📌 cùng họ với bẫy *"build `exit 0` mà binary không đổi"*) |
**`KhoiPhucTep`** | Nội dung **phải trở về đúng bản gốc**, và `-NeoDaTiem` **phải MẤT** — 🔒 nếu không thì một phép khôi phục hỏng sẽ làm **các ca sau** đỏ vì **lý do vô hình** |
**`TiemVaoDb`** | 🚨 Câu SQL **phải có ít nhất một nháy đơn** (mọi giá trị chuỗi đều cần nó) ⇒ ⚠️ không có nháy nào ⇒ gần như chắc chắn chuỗi đã bị **lớp gọi ăn mất** ⇒ **NÉM trước khi chạm DB**. ➕ Sau khi ghi, **đọc lại** (`-KiemSql` + `-SoDongMongDoi`) và **NÉM** nếu lệch |
**`Invoke-Psql`** | 🔒 Ghi ra **tệp tạm rồi đọc**, **KHÔNG** ống dẫn — ⚠️ ống dẫn **che mất exit code** ⇒ 📌 `docker` thất bại bị hiểu nhầm là *"0 dòng"*, và *"0 dòng"* lại bị hiểu nhầm là *"sạch"* |
**`TrongKhiDonDep`** | Dọn dẹp **luôn** chạy, kể cả khi ca kiểm **NÉM** |

### 🚨 Một lỗi của tôi ngay khi viết thư viện này

Bản đầu tôi chặn câu SQL có **`''`** — ⚠️ **SAI**: `''` là **cú pháp HỢP LỆ** (một nháy đơn **bị thoát** trong
literal), nên phép chặn đó **cấm luôn** những câu **đúng** như `where "Id" = ''x''`.
🔑 **Bài học:** phép kiểm *"trông có vẻ chặt"* mà **sai ngữ nghĩa** thì sẽ **chặn việc đúng** — 📌 và người ta
sẽ **gỡ nó ra** thay vì **sửa nó**. ✅ Chữa: kiểm **sự VẮNG MẶT của nháy đơn** (dấu hiệu bị ăn mất), thay vì
kiểm sự **có mặt của `''`**.

### ✅ Đã kiểm giá trị thật (5 ca mới: L, M, N, O, P)

| Ca | Bất biến |
|---|---|
🚨 **L** | Khối cũ **không khớp** ⇒ **NÉM** — 🔑 **chính là §26.5 #1** |
**M** | Khối cũ khớp **>1 lần** ⇒ **NÉM** |
**N** | Neo không có trong khối mới ⇒ **NÉM** |
**O** | Tiêm **được** ⇒ neo **có** ⇒ khôi phục ⇒ neo **mất** (🔒 vòng tròn **khép kín**, cả hai chiều) |
🚨 **P** | SQL **không có nháy đơn** ⇒ **NÉM** kèm thông điệp trỏ tới **§25.5** — 🔑 **chính là lỗi §25.5** |

➕ **So sánh trực tiếp (đo được):** cách cũ `String.Replace` với khối không khớp ⇒ nội dung **không đổi** và
**KHÔNG ném** (📌 **im lặng**); cách mới `TiemVaoTep` ⇒ **NÉM** kèm *"PHEP TIEM KHONG XAY RA"*.

---

## 🚨 `kiem-e2e.ps1` — **KIỂM E2E CÓ PHIÊN ĐĂNG NHẬP (HTML THẬT)** — bước ⑧

### Vì sao — **NỢ LỚN NHẤT VÀ LÂU NHẤT, VÀ NÓ ĐÃ ĐÓNG**

📌 `web-001` **§97**: khi tôi làm **đường gọi CÓ PHIÊN**, nó **ngay lập tức** phát hiện **HAI lỗi sản phẩm**
mà **1101 unit test + 11 ca probe ĐỀU KHÔNG THẤY**:

| # | Lỗi | Vì sao lọt qua |
|---|---|---|
**①** | `MenuViewComponent` **CHƯA BAO GIỜ render MỤC CON** ⇒ nhóm *"Vận hành"* thành `href="#"` — **liên kết CHẾT** ⇒ 🔒 người dùng **không có lối vào** `/HanMucKenh` + `/DoiSoatThanhToan` | Unit test chỉ soi **DỮ LIỆU** (menu là gì), ⚠️ **không** soi **HTML sinh ra** |
**②** | **Căn cứ pháp lý `91/2025/QH15`** chỉ nằm trong **chú thích Razor** ⇒ **KHÔNG render** | Probe cũ không đọc HTML thật của trang đó |

🔒 **Và đó là lý do bước này phải là THƯỜNG TRÚ:** 📌 mỗi vòng tôi **phải tự viết lại** probe (hoặc **quên**)
⇒ 🔑 một cổng **không chạy tự động** thì **không phải** cổng.

### 🔒 NĂM QUY TẮC ĐƯỢC THI HÀNH TRONG SCRIPT (mỗi cái là một bài học đã trả giá)

1. **PHẢI GIẢI MÃ ENTITY SỐ TRƯỚC KHI SO KHỚP.** 📌 Razor escape tiếng Việt thành `&#x110;` (`Đ`) ⇒
   `-match 'Điều hướng'` trả **FALSE DÙ CHUỖI CÓ**. 🔒 Bài học này đã vấp **BA LẦN** ⇒ ✅ `GiaiMaHtml`.
2. **NEO PHẢI LÀ ASCII THUẦN** ⇒ `[KHACHHANG-MAN-HINH]`, `[HANMUCKENH-MAN-HINH]`, `nav-pills`.
3. **TIỀN ĐỀ PHẢI ĐƯỢC KHẲNG ĐỊNH TRƯỚC KHI ĐO.** ⚠️ Các form hành động (`Chan`/`MoChan`) **chỉ render khi có
   dữ liệu thật** ⇒ 🔑 phải **gieo dữ liệu TRƯỚC** (và script **kiểm DB sạch trước khi chạy** — nếu không thì
   ca *"có form hành động"* sẽ **XANH VÌ DỮ LIỆU CŨ**).
4. **KHÔNG CÓ LIÊN KẾT CHẾT — NHƯNG PHẢI KIỂM TRONG KHOI MENU.** 📌 `href="#"` **duy nhất** trên trang thường là
   **dropdown của THEME** (`navbar-nav`), không phải menu ta ⇒ ✅ **tách khối menu theo ranh giới `</nav>`**
   rồi mới kiểm.
5. **MỌI THỨ PHẢI ĐƯỢC DỌN DẸP — KỂ CẢ KHI ĐỎ.** 🔒 `try/finally` + **khẳng định đã dọn sạch**
   (`khach probe con=0` · `co bao mat=True`) — 📌 nếu không thì lần sau `kiem-db.ps1` sẽ **ĐỎ vì lý do vô hình**.

### 11 ca

| Nhóm | Ca |
|---|---|
**Phiên** | **B1** `GET /KhachHang` = `200` (🔑 bằng chứng **duy nhất** rằng cookie phiên có tác dụng) |
**Nội dung** | **D1** neo `[KHACHHANG-MAN-HINH]` · **D2** neo `[HANMUCKENH-MAN-HINH]` · 🚨 **D6** căn cứ pháp lý `91/2025/QH15` **hiển thị** · **D3** ô chọn `donVi` (`INV-003` mục 5 việc 2) · **D4** nói rõ *"0 = TẮT"* · **D5** nút hành động **với dữ liệu thật** |
🚨 **Menu** | **E1** menu render · **E2** nhóm *"Vận hành"* · 🚨 **E3** **mục con `/HanMucKenh`** · 🚨 **E5** trong khối menu **không** có `href="#"` |

### 🚨 HAI LỖI CỦA TÔI NGAY KHI VIẾT SCRIPT NÀY — VÀ MỘT TRONG ĐÓ RẤT ĐÁNG GIÁ

**① `$home` là biến CHỈ-ĐỌC của PowerShell** ⇒ script **chết giữa đường**. 🔒 **LẦN THỨ BA** trong dự án
(`web-001` §97.6 #4 · §98.6 #3 · lượt này) ⇒ ✅ đổi thành `$trangChu`.

**② 🚨 CẮT KHỐI MENU BẰNG ĐỘ DÀI CỐ ĐỊNH ⇒ ca `E5` TỰ VÔ HIỆU.**
📌 Bản đầu tôi cắt `Substring($iMenu, 2000)`. ⚠️ Nhưng menu thật **DÀI HƠN 2200 ký tự** (đo được: nhóm
*"Vận hành"* nằm **ngoài** cửa sổ ⇒ `IndexOf('Van h')` trả **`-1`**).
🔒 **HẬU QUẢ:** `href="#"` của nhóm nằm ở **CUỐI** menu — **ngoài** cửa sổ ⇒ ca **E5** (*"không có liên kết
chết"*) **LUÔN XANH**. 📌 Nghĩa là phép kiểm **tự vô hiệu đúng ở chỗ nó quan trọng nhất** — nó sinh ra để bắt
**liên kết chết**, mà liên kết chết lại **chính là thứ bị cắt mất**. 🔑 Và nó **im lặng** (`DAT`).
➕ **Và tôi đã tiêm lỗi để kiểm — nhưng phép tiêm đầu KHÔNG TÁI HIỆN ĐÚNG LỖI LỊCH SỬ:** tôi bỏ khối
`ulCon` nhưng **giữ nhánh `span`**, nên nhóm render thành `<span>` **không có `href`** ⇒ 📌 **không có gì để
`E5` bắt**, và `E3` bắt (đúng). 🔑 **Tôi suýt kết luận *"E5 hỏng"*** — trong khi sự thật là **phép tiêm của tôi
sai** (lần thứ **BA** của họ lỗi này!).
✅ **Cách chữa:** trước §97 renderer render **MỌI** mục — kể cả nhóm — thành `<a href = muc.Url>`, và nhóm có
`Url = "#"` ⇒ đó là **nguồn `href="#"` duy nhất**. ✅ Nên phép tiêm đúng là **khôi phục renderer cũ**.
➕ Và cắt khối theo **ranh giới `</nav>`** (không theo độ dài).

### ✅ KIỂM CHỨNG — ĐÃ TIÊM ĐÚNG LỖI LỊCH SỬ

| Ca | Kết quả đo |
|---|---|
**Mã đúng + DB sạch** | ✅ **11/11** · `exit 0` · khối menu **1118** ký tự |
🚨 **Tiêm ĐÚNG lỗi lịch sử** (khôi phục renderer cũ) — `K97TIEM` **đã khẳng định vào tệp** | ✅ **9/11** · `exit 1` · 🚨 **`E3` ĐỎ** (mục con không render) **VÀ `E5` ĐỎ** (`href="#"` = liên kết chết) · khối menu **725** ký tự |
✅ **Khôi phục + build lại** | ✅ **11/11** · `exit 0` |

➕ **Tích hợp:** `tools\kiem-tat-ca.ps1` **bước ⑧** (chỉ chạy khi `-DayDu`). ➕ Khi **không** `-DayDu` thì nó
**in rõ nó kiểm GÌ** — 📌 *"im lặng bỏ qua"* nguy hiểm hơn *"báo rõ"*.
➕ **Chạy đầy đủ 8 bước:** ✅ `exit 0` · **147.4s** · **1155 unit** · **38 integration** · mermaid **197/0** ·
**DB 21/21** · **test cổng 17/17** · **E2E 11/11**.

### 🆕 `S8` (18/10/2026) — bước ⑧ **KHỞI ĐỘNG THÊM `HttpApi.Host`** (:5000)

🚨 **TIỀN ĐỀ MỚI, VÀ NÓ SẼ LÀM CỔNG ĐỎ NẾU BỊ BỎ QUA:** từ `S8`, bước ⑧ kiểm **thanh toán end-to-end**:
trang giả lập (tiến trình **Web** :5100) **ký HMAC** rồi gọi **IPN THẬT** — mà endpoint IPN nằm ở
**`eCommerce.HttpApi.Host`** (:5000). ⇒ Bước ⑧ nay **khởi động CẢ HAI tiến trình** (bước ③b) và **tắt cả hai
trong `finally`**.

🔒 **BA ĐIỀU PHẢI GIỮ (nếu không sẽ đo sai):**
1. **Cổng 5000 phải TRỐNG trước khi chạy** (bước ① đã mở rộng từ `@(5100)` thành `@(5100, 5000)`) — 🔑 một Host
   cũ còn sống ⇒ ta đo **bản build CŨ** và ca "end-to-end" thành **giả**.
2. **Khẳng định Host LÊN** (`wait-host.ps1`) trước khi đo — 📌 nếu không, `S8-P7/P8` sẽ đỏ và rất dễ bị đọc thành
   *"sản phẩm hỏng"* trong khi **phép đo thiếu một tiến trình**.
3. **Tắt Host trong `finally`** — 📌 quên ⇒ lần chạy sau `kiem-tat-ca.ps1` **bước [1]** báo *"cổng 5000 bị giữ"* và
   **dừng cả chuỗi kiểm** (đúng loại lỗi "lần sau đỏ vì lý do vô hình").

➕ **Dữ liệu probe mới:** 1 sản phẩm `Published` (+1 `Unpublished` để đo **chiều ngược**), 1 bảng giá **Active**,
1 dòng tồn kho, và **2 đơn** `WEB-K97E2E-P1/P2` + **2 giao dịch** — 🔑 **tất cả** được dọn trong `finally` và
**đếm lại** (`conGiaoDich`/`conCatalog`/`conTon`). 📌 `PayTransactions.MerchantReference` có tiền tố probe nằm
**GIỮA** chuỗi ⇒ dọn **phải** `like '%K97E2E%'` (bài học `S5-5`).

➕ **Số ca:** **102 → 123** (`S8-W1…W6` = 7 ca **đo bằng phiên ẨN DANH** + `S8-P1…P14` = 14 ca end-to-end).

🚨 **MỘT CÁI BẪY ĐÃ CẮN NGAY TRONG `S8` (ghi lại vì nó sẽ cắn lại):** khởi động `HttpApi.Host` cũng có nghĩa là
**các job nền CỦA NÓ CHẠY NGAY** — `InventoryReconciliationWorker` có `Timer.RunOnStart = true` ⇒ nó **đối soát
ngay khi Host lên** và **ghi dữ liệu THẬT** vào `ReconRunMismatches`. Trạng thái lúc đó có dòng tồn kho probe
(`OnHand = 100`, sổ cái `0`) ⇒ job ghi **một dòng lệch đúng nghiệp vụ** ⇒ 📌 **bước ⑥ của `kiem-tat-ca.ps1` ĐỎ**
(`TONG=5` sau **năm** lần chạy E2E) — và nó **không** thuộc lượt `e7` nên phép dọn cũ **không** chạm tới.
✅ Cách đúng: dọn **theo SKU probe** (`Sku like 'K97E2E%'`) + **đếm lại** (`conLechProbe`).
🔑 Câu hỏi phải hỏi mỗi khi thêm một tiến trình vào phép đo: ***"job nào chạy khi nó lên, và nó ghi cái gì?"***

---

## 🚨 `kiem-link-tai-lieu.ps1` — **CỔNG THAM CHIẾU CHẾT TRONG TÀI LIỆU** (thêm 17/10/2026)

### Vì sao có — **cùng loại lỗi hỏng-âm-thầm với Mermaid**

📌 Ngày **17/10/2026** tài liệu được tổ chức lại thành **một thư mục cho mỗi module**
(`docs/features/<module>/` — xem `docs/design-doc-guidelines.md` §3.1). Hệ quả **cơ học**: **mọi** đường dẫn
tương đối trong 53 tệp **sâu thêm một cấp**.

🚨 Và một tham chiếu chết **KHÔNG làm đỏ `dotnet build`** — đúng như lỗi cú pháp Mermaid (§7 của
`design-doc-guidelines.md`). 🔑 Phép đo ban đầu **tìm ra 175 tham chiếu chết** ngay sau khi di chuyển, trong đó
có **một sai lệch CÓ TỪ TRƯỚC** mà không ai biết: **~100 chỗ** trỏ tới
`src/eCommerce.Domain.Shared/Observability/eCommerceMetrics.cs` — tệp đã được **di chuyển** (commit `5902724`)
sang `shared/eCommerce.Kernel/Observability/` và **bản cũ đã bị xoá**.

### Nó kiểm GÌ — **hai luật, và luật (B) mới là luật phổ biến**

| Luật | Dạng | Cách giải |
|---|---|---|
**(A)** Link markdown | dạng `` `[chữ](duong/dan.md)` `` | giải theo **thư mục của tệp chứa nó** |
**(B)** Đường dẫn trong backtick | `` `docs/features/…md` ``, `` `src/…/X.cs` `` | giải theo **GỐC REPO**, rồi thử **tương đối** |

➕ **Bỏ qua có chủ ý** (để **không** báo sai — ca **P**): URL `http(s)://`, `mailto:`, neo `#…`, và mọi chuỗi
có `*`/`?`/`<`/`>`/`|` (đó là **mẫu/glob**, không phải đường dẫn cụ thể).

### Cách dùng

```powershell
pwsh -File tools\kiem-link-tai-lieu.ps1     # exit 0 = sạch, exit 1 = có tham chiếu chết
```

➕ **Đã tích hợp test:** `tools\test-cong-kiem.ps1` **nhóm 1b** — 5 ca **R/S/T/U/V** (cả chiều ĐỎ lẫn chiều XANH,
cả hai luật A và B, và ca chống dương tính giả). 📌 Một cổng **không có ca ĐỎ** thì không chứng minh được nó **bắt** gì.

### 🚨 Bài học ĐO ĐƯỢC khi viết chính script này

| # | Bẫy | Hậu quả thật |
|---|---|---|
**1** | 🔒 **Regex thay thế hàng loạt cho ra "254 tham chiếu đã sửa"** nhưng cổng vẫn **ĐỎ 175 chỗ** | 📌 Đếm số lần thay thế **KHÔNG** phải bằng chứng đã đúng — phải **chạy cổng kiểm lại** |
**2** | 🚨 **`@(@('a','b'))` bị PowerShell LÀM PHẲNG** khi chỉ có **một** phần tử | ⇒ `$cap[0]` thành **KÝ TỰ ĐẦU** của chuỗi ⇒ một script sửa link **đã thay `d` → `o`** trong bộ nhớ. ✅ Dùng `[pscustomobject]`; và **luôn `throw` khi không tìm thấy chuỗi cần thay** (bài học `ham-tiem.ps1`) |
**3** | ⚠️ **Đường dẫn tương đối phụ thuộc nơi CHẠY script**, không phải nơi đặt script | 🔑 Một lệnh `cd` trong cùng khối lệnh có thể **không** áp dụng cho `[IO.File]::ReadAllText` ⇒ dùng **đường dẫn tuyệt đối** khi xử lý hàng loạt |


---

## 🚨 HAI CÁI BẪY MỚI — **`S1` (`ADDR-004`), 18/10/2026**

> Cả hai đều **hỏng ÂM THẦM**: lệnh báo thành công, hoặc test xanh/đỏ vì lý do **không phải** điều mình tưởng.

### B1. `dotnet ef migrations add … --no-build` ⇒ **MIGRATION RỖNG** (mà vẫn báo `Done.`)

**Chuyện thật:** chạy `dotnet ef migrations add Addr004_SoDiaChi_Address --no-build` **sau khi chỉ build
`eCommerce.EntityFrameworkCore`** ⇒ EF **không thấy entity mới** ⇒ sinh migration **`Up()` rỗng**, rồi in
`Done. To undo this action, use 'ef migrations remove'`. Nếu không mở tệp ra đọc thì **không phát hiện gì**.

**Nguyên nhân:** `--no-build` dùng **bản build của STARTUP project** (`src/eCommerce.DbMigrator`), không phải
project bạn vừa build. `DbMigrator` cũ ⇒ assembly mô hình cũ ⇒ diff rỗng.

✅ **Luật:** build **startup project** trước, rồi mới `migrations add --no-build`:

```powershell
dotnet build src/eCommerce.DbMigrator            # BẮT BUỘC — không phải project EFCore
dotnet ef migrations add <Ten> --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.DbMigrator --no-build
# ✅ MỞ TỆP MIGRATION RA ĐỌC: Up() phải có CreateTable/CreateIndex. Rỗng ⇒ DỮNG, không đi tiếp.
```

➕ **Hệ quả kéo theo:** xoá tệp migration rỗng **không đủ** — assembly vẫn còn class đó ⇒ `migrations add`
báo *"The name ''<Ten>'' is used by an existing migration"*. Phải **`dotnet build` lại** rồi mới add lần nữa.
➕ Và `migrations remove` có thể **từ chối** (`… has already been applied to the database`). Khi đó:
xoá tay 2 tệp (`<Ten>.cs` + `<Ten>.Designer.cs`) + `git checkout -- …/eCommerceDbContextModelSnapshot.cs`.

### B2. Tệp trong `Data/` được **COPY VÀO OUTPUT** ⇒ sửa nguồn xong chạy `--no-build` là đọc bản CŨ

**Chuyện thật:** thêm 8 dòng vào `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt` rồi chạy
`dotnet test --no-build` ⇒ cổng `RangBuocDbChuaAnhXaTests` **vẫn ĐỎ** với đúng 8 tên vừa thêm. Không phải
cổng sai — nó đọc bản **copy cũ** trong `bin/`.

**Nguyên nhân:** test tìm tệp bằng cách đi ngược từ `AppContext.BaseDirectory`, nên **bản copy trong `bin/`
được tìm thấy TRƯỚC** bản nguồn trong repo.

✅ **Luật:** sửa **bất kỳ tệp dữ liệu nào** (`Data/*.txt`, `Assets/*.sql`, `appsettings*.json`…) mà test đọc
⇒ **PHẢI `dotnet build` lại** project test đó rồi mới chạy. `--no-build` chỉ an toàn khi bạn **chỉ** sửa `.cs`.

➕ **Điều này cũng đúng với `Assets/schema.sql`**: regenerate xong mà chạy `--no-build` thì integration test
vẫn dùng **schema cũ** ⇒ kết quả vô nghĩa (có thể xanh **giả**).

---

## 🚨 BẪY MÔI TRƯỜNG — **chặn `capture` stdio của tiến trình con ⇒ CỔNG ĐỎ GIẢ** (18/10/2026)

> 🚨 **Đây là bẫy ĐO, không phải lỗi SẢN PHẨM.** Nó làm `kiem-tat-ca.ps1` chết ở **bước ②** và trả `exit 1`
> — nhìn **giống hệt** *"build hỏng"*. Toàn bộ **mã nguồn** có thể đúng 100% trong khi cổng báo đỏ.

**Triệu chứng đo được:** chạy `pwsh -File tools\kiem-tat-ca.ps1 -DayDu` ⇒ dừng ở bước ② với
`Program 'pwsh.exe' failed to run: StandardOutputEncoding is only supported when standard output is redirected`
(cùng họ: `Access is denied` · `EPERM` khi `node` dùng `stdio: 'pipe'`). Log **không** có dòng lỗi biên dịch nào.

**Đo để tách nguyên nhân — 4 cách ĐỀU hỏng, chỉ cách KHÔNG capture là chạy:**

| Cách | Kết quả |
|---|---|
| `& dotnet --version` (**không** redirect) | ✅ chạy, `$LASTEXITCODE = 0` |
| `& dotnet --version \| …` · `> file` · `*> file` · `2>&1` | 🚨 `StandardOutputEncoding …` ⇒ **lệnh KHÔNG chạy** |

🔑 **Vì sao chí mạng:** hàm `ChayLenh` của `tools/kiem-tat-ca.ps1` (và của mọi công cụ trong `tools/`) dùng
**`*>`** để *"không ống dẫn output của lệnh mà ta cần exit code"* — đúng quy tắc ① của chính bộ công cụ.
Trong môi trường **chặn capture**, quy tắc đó **không thực hiện được** ⇒ cổng **không thể** chạy.

✅ **Luật — trước khi kết luận về SẢN PHẨM:**
1. Thấy `StandardOutputEncoding` / `Access is denied` ở bước ② ⇒ **KHÔNG** đọc là *"build hỏng"*;
   đó là **môi trường chặn capture**. Kiểm bằng **một** lệnh: `& dotnet --version > $null` (hỏng ⇒ đúng bẫy).
2. Chạy cổng ở môi trường **cho phép capture** (terminal thường / phiên có toàn quyền) rồi **mới** ghi số.
3. 📌 **Đừng** "chữa" bằng cách bỏ `*>` trong công cụ — làm thế là **gỡ đúng cái chặn** mà quy tắc ① sinh ra
   để chống (exit code bị `Select-String`/`Tee` che).

✅ **Số đo THẬT khi chạy được** (đo lại `S1`, 18/10/2026 — **3 lần**: **159.5s · 157.4s · 161.6s**, cùng số ca):
`exit 0` · build 40.2–45.7s · unit **1205** · integration **41** · mermaid **213/0** ·
DB **21/21** (`NND thucTe=49 · tuTest=49` · quét **83** chỗ) · test cổng **20/20** hiển thị · E2E **59/59**.

---

## 🚨 NĂM CÁI BẪY MỚI — **`S2` (`ADDR-004` phần B), 18/10/2026**

> 🚨 Cả năm đều **KHÔNG có lỗi biên dịch** và **KHÔNG** làm cổng nào đỏ ngay: chúng chỉ lộ ra khi **gọi HTTP
> thật**. 📌 Vì vậy `S2` thêm **bước ⑨** (`tools\probe-so-dia-chi.ps1`) vào `kiem-tat-ca.ps1 -DayDu`.

### S2-1. Route trong **tài liệu** ≠ route **thật** ⇒ probe `404` ngay lần chạy đầu

**Chuyện thật:** `ADDR-004` §6.2 v1.0 đề xuất `/api/app/customer-address/them/{customerId}`,
`/dat-mac-dinh/{customerId}/{linkId}`. Probe gọi đúng theo tài liệu ⇒ **`404`** cả 4 route.
🔑 **Hai quy luật ABP** làm route khác: ① tên route sinh từ **TÊN METHOD** (`ThemDiaChiAsync` ⇒ `them-dia-chi`);
② tham số **nguyên thuỷ** vào **QUERY STRING**, không vào path ⇒ `/dat-mac-dinh?customerId=…&linkId=…`.

✅ **Luật:** in route **THẬT** bằng `tools/route-check.ps1 -Match '…'` rồi **sửa tài liệu** — 🚨 **cấm** chép
route từ tài liệu vào probe. ➕ Và kiểm **hai chiều**: `GET` ⇒ `404` (không có dữ liệu) **và** `POST` vào cùng
route ⇒ **`405`** (chứng minh route là `GET`). 📌 `404` một mình **không** nói gì về verb.

### S2-2. 🚨 **Cache QUYỀN âm** giữ `403` sau khi cấp quyền mới (permission mới ⇒ API mới `403`)

**Chuyện thật:** thêm **4** permission `eCommerce.Addresses.*`, chạy `DbMigrator` (DB **có** `AbpPermissionGrants`:
57 grant) ⇒ API mới vẫn **`403`**. Log `ChanDoanTuChoiQuyenHandler` nói **đúng nguyên nhân**:
*"quyền `eCommerce.Addresses` · đã định nghĩa · **KHÔNG được cấp cho principal hiện tại**"*.
🔑 ABP cache **kết quả ÂM** (`KHÔNG được cấp`) theo `(provider, key, permission)`; các bản ghi âm này **không**
tự mất khi grant mới được thêm.

✅ **Luật:** sau khi cấp permission mới, **xoá cache quyền** rồi mới khởi động lại tiến trình:

```powershell
docker exec ecommerce-redis sh -c "redis-cli --scan --pattern '*PermissionGrant*' | wc -l"       # IN ra số key
docker exec ecommerce-redis sh -c 'redis-cli --scan --pattern "*PermissionGrant*" | while read k; do redis-cli DEL "$k" > /dev/null; done'
```

📌 Đo được: **174 key** ⇒ xoá xong, API hết `403` **ngay** (không cần đổi mã). ➕ `probe-so-dia-chi.ps1` **tự** làm
việc này (và **in số key**) để lần sau không phải nhớ.

### S2-3. 🚨 ABP trả **`403` cho MỌI `BusinessException`** ⇒ `404`/`409` trong tài liệu **không bao giờ** xuất hiện

**Chuyện thật:** "thêm trùng địa chỉ" (`0010`) ⇒ **`403`**; "khách không tồn tại" (`0001`) ⇒ **`403`**;
"vượt trần" (`0015`) ⇒ **`403`**. Hệ quả **kép**: ① client tưởng lỗi **phân quyền** trong khi là lỗi **dữ liệu**;
② `403` thật mất khả năng chẩn đoán (đúng thứ `ChanDoanTuChoiQuyenHandler` sinh ra để bảo vệ).

✅ **Luật:** ánh xạ **mã nghiệp vụ ⇒ HTTP status** bằng chính API của ABP (giữ nguyên payload `{error:{code,…}}`):

```csharp
Configure<Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions>(options =>
{
    options.Map("eCommerce:Customers:0010", HttpStatusCode.Conflict);   // 409
    options.Map("eCommerce:Customers:0011", HttpStatusCode.NotFound);   // 404
    // … 19 mã của ADDR-004 (§12)
});
```

📌 Ghi ở `eCommerceHttpApiHostModule.cs`; nếu quên thì **mọi mã 4xx nghiệp vụ thành 403** và **không** test nào đỏ.

### S2-4. 🚨 **Thứ tự UPDATE** với **partial unique index**: xoá mềm phải GHI XUỐNG DB trước

**Chuyện thật:** `go-dia-chi` (gỡ mục đang mặc định) ⇒ **`23505`** trên
`IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh`. Nguyên nhân: EF phát
`UPDATE … IsDefault = true` (mục **thay thế**) **trước** `UPDATE … IsDeleted = true` (mục đang gỡ) ⇒ tại thời
điểm đó **hai** dòng cùng `(khách, loại)` đều là mặc định.

✅ **Luật:** mọi thao tác đổi mặc định phải **bỏ trước — đặt sau**, và **bỏ** phải **đã xuống DB**:
`await CurrentUnitOfWork.SaveChangesAsync()` **giữa** hai bước. 📌 PostgreSQL kiểm unique index **ngay từng
câu lệnh** (không phải cuối transaction) — đúng gốc của `TC-06b`.

### S2-5. 🚨 Thiếu `DependsOn`/`ProjectReference` ở **Web** ⇒ `/KhachHang` trả **500**, build vẫn XANH

**Chuyện thật:** `CustomerAppService` được thêm phụ thuộc `IAddressDirectory`. **Host** đã có
`AddressApplicationModule`; **Web** thì **chưa** ⇒ `/KhachHang` **500**, và cổng E2E báo
*"DANG NHAP THAT BAI ⇒ PHEP KIEM VO NGHIA"* — 📌 một thông điệp **trỏ sai chỗ** (lỗi DI, không phải mật khẩu).

✅ **Luật:** thêm phụ thuộc vào một app service **đang được dùng ở cả hai tiến trình** ⇒ kiểm **cả hai** đồ thị
module (`DependsOn` **và** `ProjectReference`). 📌 Đây là bẫy #4 của dự án, lặp lại ở tầng **module**:
`DbSet` thiếu ⇒ DI nổ; `DependsOn` thiếu ⇒ **cũng** DI nổ, cùng triệu chứng `500`.

---

## 🚨 BA CÁI BẪY MỚI — **`S5` (`PAY-001` phần B), 18/10/2026**

> 📌 Cả ba đều nằm ở **công cụ ĐO**, không ở sản phẩm — đúng loại đã ngốn trọn vòng ở các lượt trước.

### S5-1. 🚨 Mã probe nằm **Ở GIỮA** chuỗi ⇒ `like 'K9S5%'` **không khớp** dòng nào

**Chuyện thật:** `probe-thanh-toan.ps1` dọn bằng `where "MerchantReference" like 'K9S5%'`. Nhưng
`MerchantReference` do `SinhMaThamChieu` sinh ra có dạng **`PAY<ngày>-<số đơn>-<lần thử>`** ⇒ mã probe nằm **ở
giữa**: `PAY20261018-K9S5-DON-01-1`. 🔑 Kết quả: phép dọn **không xoá gì**, rác ở lại, và ca **`T03`** (*"mã ổn
định"*) **ĐỎ** vì `lần thử = 3` — 📌 **trông y như sản phẩm sai**.

✅ **Luật:** khi dọn theo **mã do sản phẩm sinh**, phải biết mã đó **nằm ở đâu** trong chuỗi ⇒ dùng
**`like '%<mã>%'`**, hoặc khai **tiền tố thật** (`PAY%`). ➕ Và **in số dòng đã xoá** — *"dọn không khớp"* và
*"không có gì để dọn"* **trông giống hệt nhau** (bài học §25.5, lần thứ **N**).

### S5-2. 🚨 `-StatusCodeVariable` **chỉ** có trên `Invoke-RestMethod`, **KHÔNG** có trên `Invoke-WebRequest`

**Chuyện thật:** `Goi-Ipn` dùng `Invoke-WebRequest -StatusCodeVariable 'maHttp'` ⇒
`A parameter cannot be found that matches parameter name 'StatusCodeVariable'` ⇒ probe **chết** ở ca IPN đầu
tiên. 📌 (PowerShell **7.5.1**: `Invoke-RestMethod` **có**, `Invoke-WebRequest` **không**.)

✅ **Luật:** với `Invoke-WebRequest`, đọc **`$tra.StatusCode`** trực tiếp (nhớ `-SkipHttpErrorCheck`, nếu không
thì chính `400` — ca ta cần đo — sẽ **ném** và ta không đọc được body). ➕ Kiểm bằng **một lệnh**:
`(Get-Command Invoke-WebRequest).Parameters.Keys -contains 'StatusCodeVariable'`.

### S5-3. 🚨 `Environment` **KHÔNG** có route = `404`, nhưng DI thiếu = **`500` kèm stack trace**

**Chuyện thật:** lần đầu gọi `GET /api/webhooks/vnpay`, phản hồi là **`HTTP 500`** với
`Cannot resolve parameter 'eCommerce.PaymentHub.IXuLyKetQuaCong xuLy'`. 🔑 Nguyên nhân **không** phải thiếu đăng
ký: ABP đăng ký `ITransientDependency` theo **TÊN LỚP** (`XuLyKetQuaCongService` ⇒ `IXuLyKetQuaCongService`),
**không** theo `IXuLyKetQuaCong` ⇒ phải có `[ExposeServices(typeof(IXuLyKetQuaCong))]`.

✅ **Luật:** một interface **không** theo quy ước `I<TênLớp>` thì **phải** khai `[ExposeServices]`, và **phải** có
test reflection khoá lại (`HoanTienS5Tests.IXuLyKetQuaCong_PHAI_duoc_ExposeServices`). 📌 Và bài học đo: **`500` ở
một endpoint mới thường là DI**, không phải routing — 🔑 phân biệt bằng **`404`** (route sai) với **`500` +
"None of the constructors … can be invoked"** (DI).


---

## 🚨 `probe-don-san.ps1` — **ĐƯỜNG CHẠY THẬT** CỦA ĐƠN SÀN (`SAL-005` · `S6`) — **bước ⑫**

### Vì sao có nó — và nó khác gì `probe-thanh-toan.ps1`

`probe-thanh-toan.ps1` đo **đường HTTP** (endpoint có thật, controller trả gì). **Probe này đo một thứ khác:
một `IDistributedEventHandler` có THẬT SỰ CHẠY và ghi đúng dữ liệu hay không** — vì `sal-005` **§18.6** đã
từ chối kết luận đúng ở điểm này: *"CHƯA ĐO ĐƯỢC RUNTIME … không có đường nào phát `ChannelOrderPulledEto`"*.

✅ **Đường gọi** (đúng cách §18.7 #1 đã chỉ — *"làm đường gọi, đừng tìm cách đo khác"*):

```
gieo 4 shop (SQL)  →  Host thật (PocOrdersPerPull=1, Supervisor=3600s)
   →  POST /api/app/channel/{id}/sync-now   (đúng nút "Đồng bộ ngay")
   →  ChannelShopSyncer phát ChannelOrderPulledEto
   →  MarketplaceOrderIntakeHandler (handler THẬT)
   →  đo DB thật: OrdOrders · SalMarketplaceOrderIntakes · SalMarketplaceEventLogs · MsgDeadLetters
```

📌 **Năm câu hỏi nó trả lời** (**49 ca**): event lỗi ⇒ DLQ **và payload còn nguyên** · watermark chạy **trên dữ liệu thật** (`AppliedSourceVersion ≠ null`) · **gửi lại không DLQ giả** · job `D12` chạy **xong** · **cổng hạ cờ `GapSuspected`** bảo vệ đúng (`401`/`403`/`404`/`200` — `C38`…`C47`).
liệu thật** (`AppliedSourceVersion ≠ null`) · **gửi lại không DLQ giả** · job `D12` chạy **xong**.

### 🚨 BA CÁI BẪY MỚI — `S6`, 18/10/2026

#### S6-1. 🚨 `Select-String -SimpleMatch '\[D12-JOB-BAT-DAU\]'` ⇒ **0 khớp** dù log **CÓ** neo

| | |
|---|---|
**Triệu chứng** | **6 ca ĐỎ GIẢ** ở lần chạy đầu. Trong đó có ca nói *"job `D12` KHÔNG chạy"* — **trong khi job đã chạy đúng** và in cả **khoá nghiệp vụ** của đơn bị đánh cờ |
**Nguyên nhân** | Với `-SimpleMatch`, chuỗi mẫu được so **nguyên văn** ⇒ `\[` là **hai ký tự `\` và `[`**, không phải *"dấu `[` đã thoát"*. 🔑 Tôi viết mẫu theo thói quen **regex** nên **không khớp gì cả** |
**Cách tránh** | Dùng `-SimpleMatch` thì viết **neo trần**: `-SimpleMatch '[D12-JOB-BAT-DAU]'`. 📌 Nếu muốn regex thật thì **bỏ** `-SimpleMatch`. 🔒 Và khi một ca *"log phải có X"* ĐỎ, **mở tệp log ra grep bằng tay** trước khi kết luận về sản phẩm — `grep '[D12' host.log` (regex) sẽ thấy ngay |

📌 **Bài học:** đây **chính là** §1 của tệp này (*"một phép đo bất ngờ thường là phép đo SAI"*) ở dạng mới:
một **công cụ so khớp** sai đã tạo ra **6 phán quyết sai về sản phẩm** trong **một** lần chạy.

#### S6-2. **Route thật** là `/api/app/channel/{id}/sync-now` (lần thứ **4**)

Tôi dùng `/api/app/channel/sync-now/{id}` (thứ tự **ngược**) ⇒ **`404`** ngay lần chạy đầu. 🔒 Route thật đọc
từ `swagger.json`:
`/api/app/channel/{id}/sync-now`. 📌 Bài học **S2-1** vẫn cần nhắc lại vì **đã tái phạm**: *in `HTTP STATUS`
trước body*, và **đọc route từ swagger** thay vì viết theo trí nhớ.

#### S6-3. Dọn probe **phải** xoá `MsgIdempotencyRecords`

Dòng nhật ký nhận đơn/đơn nội bộ đều xoá được theo **tiền tố mã đơn** (`POC-K9S6-%`), nhưng **bản ghi chống
trùng** (`MsgIdempotencyRecords.KeyValue` = `ExternalOrderId`) quên xoá ⇒ lần chạy sau handler rơi vào
`TraVeKetQuaCu` ⇒ **không chạy** ⇒ mọi ca sau **XANH vì lý do SAI**. 🔑 Đúng loại lỗi **§25.5**:
*"tiêm không thành"* và *"cổng không bắt"* **trông giống hệt nhau**.

### 🔒 Vì sao `PocOrdersPerPull` và nhịp supervisor đặt bằng **biến môi trường**

`ChannelHub:Shopee:PocOrdersPerPull` = `1` ⇒ connector PoC trả **đúng 1** đơn giả **tất định** (cùng con trỏ ⇒
cùng đơn, cùng `update_time`). Nhịp supervisor = `3600` ⇒ **không** có nhịp nền nào chen vào giữa phép đo.
📌 Cả hai **chỉ** đặt trong tiến trình Host của probe ⇒ **không** commit cấu hình dev vào `appsettings.json`.

#### S6-4. 🚨 Thêm **permission** vào provider **KHÔNG** tự cấp cho `admin` ⇒ `403` dù code đúng

| | |
|---|---|
**Triệu chứng** | API mới trả **`403`** ngay cả với `admin`, trong khi hằng số **và** định nghĩa permission **đã có đủ** (`BatBienPhanQuyenTests` xanh). `select count(*) from "AbpPermissionGrants"` vẫn là con số **cũ** |
**Nguyên nhân** | Định nghĩa permission (trong `eCommercePermissionDefinitionProvider`) chỉ nói *"quyền này tồn tại"*. Việc **cấp** cho vai trò `admin` do **seeder** (`IdentityDataSeedContributor`) làm khi chạy **`eCommerce.DbMigrator`** — mà chạy Host (`dotnet run`) **không** chạy seeder |
**Cách tránh** | Sau khi thêm permission: chạy **`dotnet eCommerce.DbMigrator.dll`** ⇒ kiểm **số dòng** `AbpPermissionGrants` **tăng đúng** (58 → **61**). ➕ Xoá **cache quyền** (`*PermissionGrant*`) **trước** khi khởi động Host — ABP cache **kết quả ÂM**, nên `403` cũ vẫn nằm đó dù DB đã cấp (`README-probe` **I1**, `ADR-004`/`S2`) |
**Bài học** | 📌 *"đã định nghĩa"* và *"đã cấp"* là **hai** chuyện khác nhau. 🔑 Và triệu chứng của cả hai **giống hệt nhau**: `403`. ⇒ Luôn in **số dòng** của `AbpPermissionGrants` **trước** khi kết luận về code |

📌 **Cùng họ với `S6-1`:** cả hai đều là *"phép đo của tôi nói sai về sản phẩm"* — không phải sản phẩm sai.
