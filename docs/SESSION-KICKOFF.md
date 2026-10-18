# 🚀 SESSION KICKOFF — dán tệp này khi bắt đầu mỗi phiên

> **Cách dùng:** mỗi lần mở phiên mới, gửi **toàn bộ tệp này** (hoặc đường dẫn + câu *"đọc SESSION-KICKOFF rồi làm phiên S…"*).
> Cuối tệp có ô **「PHIÊN NÀY」** — điền số phiên (`S1`, `S2`, …) trước khi gửi.
> 📌 Tệp này **không** thay bàn giao; nó là **cửa vào**: nó nói *đọc gì*, *luật gì*, *số đo nào là chuẩn*, *phiên này làm gì*.

---

## 1. Bối cảnh trong 5 dòng

Dự án **eCommerce đa tenant / đa kênh** (.NET 10 · ABP 10.7 · PostgreSQL 17 · Redis 7 · MVC/Razor), tại
`D:\Projects\Tools\eCommerce`. Giai đoạn 1 = *"bán được trên website"*. **Tài liệu thiết kế (55 tệp) đã đủ**,
**`ADDR-004` đã duyệt (1/55)**. Việc còn lại là **code** — đã chia **11 phiên** (`S0`…`S10`).
🔑 **Bài học lớn nhất của dự án:** *"một phép đo bất ngờ thường là phép đo SAI, không phải sản phẩm sai"* —
và phiên trước từng **nói quá "GĐ1 hoàn thành"** vì **không đo lại**.

---

## 2. Đọc gì trước — ĐÚNG THỨ TỰ NÀY, đừng đọc hết

| # | Tệp | Đọc để biết |
|---|---|---|
| 1 | `docs/gd1-coding-sessions.md` | **Phiên này làm gì** + cổng ra + phụ thuộc (§4) · các quyết định chặn (§3) |
| 2 | `docs/handover/BAN-GIAO-GD1-2026-10-17.md` | Ràng buộc schema/ABP (§4) · công cụ kiểm (§5) · **bảy cái bẫy** PowerShell/C# (§5.1) · kỷ luật ĐO (§5.2) |
| 3 | `docs/features/README.md` | Danh mục 55 tài liệu theo module + trạng thái thật |
| 4 | **Tài liệu feature của phiên** (1–2 tệp, `docs/features/<module>/…`) | Thiết kế + BR/NFR + test case. 📌 **Chỉ đọc tài liệu của phiên mình** |
| 5 | `docs/design-doc-guidelines.md` §4–§5 | Quy tắc vàng *"code lệch tài liệu ⇒ sửa tài liệu TRƯỚC, cùng PR"* |
| 6 | `tools/README-probe.md` | **Trước khi viết probe mới** — danh mục cổng đã có, dùng lại |

⚠️ **Đừng** đọc cả 55 tài liệu (≈4,8 MB). Ngân sách ngữ cảnh của một phiên = **1–2 tài liệu feature**.

---

## 3. Trạng thái ĐO ĐƯỢC — baseline chuẩn (đo lại **18/10/2026** cuối `S5`, `-DayDu` exit 0)

```
[B1] dọn tiến trình + cổng trống      1.1s
[B2] build + KIỂM MỐC nhị phân       44.0s   · 71 tệp DLL/EXE đổi mốc
[B3] unit test                        3.7s   · 1347/1347
[B4] integration (PostgreSQL thật)    8.7s   ·   57/57
[B5] mermaid (parser thật)            5.3s   ·  213 khối · 0 lỗi
[B6] bất biến DB                      6.5s   ·   23/23 · NND = 52 · 58 quyền · quét 85 vị trí probe
[B7] test cho chính các cổng         81.3s   ·   22 ca (A…I hiển thị 20 dòng)
[B8] E2E có phiên đăng nhập          22.6s   ·   67/67
[B9] sổ địa chỉ HTTP thật            13.2s   ·   17/17
[B10] khách hàng HTTP thật           15.6s   ·   26/26
[B11] thanh toán HTTP thật           12.7s   ·   26/26   ← 🆕 `S5`
      ─────────────────────────────────────
      TỔNG                           214.8s
```

📌 **Đo HAI lần ở cuối `S5`** (cùng 11 bước, cùng số ca): **214.8s** và **211.1s** — chênh **1,7 %**, đúng
mức máy/nhiệt độ. 🔑 Ghi **cả hai** để phiên sau không đọc một con số là **chỉ tiêu**.
📌 **Đo ĐẦU phiên `S5` (trước khi sửa) cũng `exit 0` · 204.4s** với unit **1294** / integration **51** /
NND **51** / **84** chỗ quét — tức **mọi số khớp `S4`** (thời gian chênh ~5 % là **máy/nhiệt độ**, không phải
sản phẩm). 🔑 **Đây là bằng chứng baseline `S4` vẫn đúng**, không phải suy đoán từ tài liệu.
**Code:** **341 tệp · 41.414 dòng** — 📌 **phương pháp (ghi rõ để không trôi):**
`Get-ChildItem src, modules, shared -Recurse -Filter *.cs`, **loại** `\bin\`, `\obj\`, `*.Designer.cs`,
`*Snapshot.cs`, rồi đếm bằng **`| Measure-Object -Line`** (tức **bỏ dòng RỖNG**) · đo 18/10/2026 cuối `S5` ·
**107 tệp test · 17.255 dòng** (cùng phương pháp, trên `test/`) · **39 migration** (78 tệp = 39 `X.cs` +
39 `X.Designer.cs`).
🔑 **Vì sao phải ghi thêm `Measure-Object -Line`:** ở `S4` tôi đếm bằng `.Count` (mọi dòng) ⇒ ra **47.191**,
lệch **7.967** ⇒ suýt kết luận *"code phình 20 %"*. ✅ Dùng `Measure-Object -Line`: `S4` = **40.291**,
`S5` = **41.414** ⇒ **+1.123 dòng cho 4 tệp sản phẩm mới** (≈ 280 dòng/tệp — hợp lý vì chú thích chiếm phần lớn,
đúng quy ước dự án). 📌 Đây lại đúng bài học §1: *"một phép đo bất ngờ thường là phép đo SAI"*.
⚠️ **Số cũ (`324/38.460` · `95/15.115` · `47.191`) KHÔNG đối chiếu lại được** với phương pháp này — 🔑 đừng cộng trừ
với chúng; chênh lệch **không** phải "code phình" mà là **hai phép đo khác nhau**.
**Git:** nhánh `main` · **282 commit** · ✅ **cây SẠCH** (đã commit — xem `git rev-parse --short HEAD`) · **`origin` =
`https://github.com/hungnt9801/eCommerce`** · ⚠️ repo đang **PUBLIC**.
📌 **`refs/heads/main` trên remote = `551c7b0`** — 🚨 **ĐO LẠI 18/10/2026 (cuối `S5`) và con số CŨ ĐÃ TRÔI:**
bản trước ghi `ab9a7ff` + *"`S1`/`S2`/`S3`/`S4` chưa push"*. Sự thật đo được: `origin/main` **đã chứa `S1`**
(`ab3cfc7`) và **2 commit tài liệu** ⇒ **chỉ `S2`…`S5` chưa push**, cụ thể **5 commit**:
`4a1095d` (tài liệu `S1`) · `e43bf76` (`S2`) · `6495835` (`S3`) · `d6d7eca` (`S4`) · `82bfb4d` (`S5`).
⇒ **cần push trước khi mở `S9`** (CI). 🔑 Đây đúng bài học §1: *con số ghi trong tài liệu cũng TRÔI*, nên phép
`git rev-list --count origin/main..HEAD` là **nguồn sự thật**, không phải dòng chữ này.
📌 Con số commit **không tự khớp** với chính commit ghi nó (commit đó làm số tăng 1) — 🔑 nên mốc đúng là
*"sau `S5`, 18/10/2026"*.

🚨 **Các con số ĐÃ TRÔI — dùng số ở trên, đừng dùng số cũ:**
| Số cũ (handover / `S2` / `S3` / `S4`) | Số đo 18/10/2026 (cuối `S5`) | Vì sao |
|---|---|---|
| ~145–155s / 175.9s / 203.2s / **196.0s** | **214.8s** | **11** bước (thêm ⑪); 📌 đổi theo máy/nhiệt độ, **không** phải chỉ tiêu (`S5` đo đầu phiên = **204.4s**, cũng `exit 0`) |
| 17 ca / 22 ca test cổng | **22 ca** (hiển thị 20 dòng ✅ A…I) | không đổi (`S5` không thêm ca cho chính các cổng) |
| 1181 / 1219 / 1228 / **1294** unit · 38 / 46 / **51** integration | **1347 / 57** | `S5` **+53 unit** (chữ ký IPN cả hai chiều · luật ghi nhận hoàn tiền · entity dòng hoàn · ETO/handler · reflection DI) và **+6 integration** (dòng hoàn + 3 `CHECK` TIỀN trên PostgreSQL thật) |
| 48 / 51 index `NND` · 53 / 57 / **58** quyền | **52 · 58 quyền** | `S5` thêm **1** unique index (`IX_PayRefunds_…`) ⇒ **1 migration**; **không** thêm permission |
| 197 / **213** khối mermaid | **213 khối** | không đổi ở `S5` (sửa **nội dung** một sơ đồ, **không** thêm khối) |
| 59 / **67** ca E2E | **67 ca** | `S5` không thêm ca E2E (thanh toán **chưa** lên UI — `S8`); thay vào đó là **bước ⑪** với **26 ca HTTP thật** |
| 84 vị trí quét probe | **85** | `S5` thêm bảng `PayRefunds` (PK `uuid`) ⇒ phép **tự dò** quét thêm **1** chỗ — 📌 con số **tăng là ĐÚNG**, không phải lỗi |
| — (chưa từng có) | **26 ca** probe thanh toán HTTP thật | 🆕 bước ⑪ của `S5` |

🔑 **Vì vậy bước ĐẦU TIÊN của mọi phiên là ĐO LẠI** (mục 6) — nếu số khác bảng trên thì **dừng, tìm hiểu trước khi sửa**.

---

## 4. LUẬT KHÔNG THƯƠNG LƯỢNG — vi phạm là hỏng NGẦM

**Schema/DB**
1. 🚨 **MỌI unique index trên entity `IMultiTenant` PHẢI có `NULLS NOT DISTINCT`** — viết **SQL thô** trong migration (kèm `DROP INDEX IF EXISTS`).
2. 🚨 **Thêm/bớt unique index ⇒ sửa CÙNG PR**: con số **48** trong `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` **và** danh sách tên index. Không sửa ⇒ cổng đỏ **giả** (một index hỏng bị một index mới **bù**).
3. Tiền là **`decimal`**, **cấm `double`**, **cấm so tiền bằng `==`**.

**ABP — bốn cái bẫy đã trả giá**
4. 🚨 **`DbSet<>` phải có trên `eCommerceDbContext` cho MỌI entity** — ABP đăng ký `IRepository<T>` bằng cách **quét `DbSet`**, không quét model. Thiếu ⇒ **DI nổ lúc chạy ⇒ HTTP 500**, triệu chứng **không trỏ tới chỗ sai**.
5. 🔐 `[Authorize]` ở **cấp class** + **permission PHẢI được định nghĩa** (permission chưa định nghĩa ⇒ `403` cho **mọi** người, kể cả admin).
6. 🔒 **Background worker KHÔNG được gọi app service có `[Authorize]`** (interceptor từ chối principal ẩn danh) — đọc repository trực tiếp, chia sẻ **luật thuần** ở Domain.
7. `AbpExceptionFilter` biến `BusinessException` thành **HTTP 500** trên MVC ⇒ controller **chỉ** `catch BusinessException`, **không** nuốt guard.

**Phân tầng**
8. `Domain`/`Domain.Shared` **KHÔNG** tham chiếu `Application.Contracts`; `Application.Contracts` **KHÔNG** tham chiếu `Domain`. Hằng số độ dài để ở `eCommerceConsts` (`Domain.Shared`).

**Quy trình**
9. 🔒 **Code lệch tài liệu ⇒ SỬA TÀI LIỆU TRƯỚC, cùng PR.** Cập nhật §16 "đã xong / còn lại" của tài liệu feature.
10. 🔒 **Commit message PHẢI thuần ASCII** — hook `commit-msg` **chặn** (đã cắn 2 lần). Không dùng `§`, `Đ`, `→`.
11. 🔑 **Đo TRƯỚC khi kết luận** — in **giá trị thật**; tách đúng **vùng**; khẳng định **phép tiêm đã xảy ra**; đo **cả hai chiều**; **không tin `exit 0`** (kiểm mốc/cỡ DLL).
12. 🚫 **KHÔNG bypass kiểm soát bảo mật.** Cụ thể: **cấm** bỏ/vô hiệu `KiemChuKy` hay `VerifyWebhookAsync` của VNPay — ai gửi `vnp_ResponseCode=00` cũng thành **đơn đã trả tiền** ⇒ giao hàng miễn phí (`PAY-001` §7.7.4).
➕ **Hai bẫy MỚI, cả hai đều hỏng ÂM THẦM** (phát hiện ở `S1`, chi tiết: `tools/README-probe.md` **§B1/B2**):
13. 🚨 `dotnet ef migrations add … --no-build` dùng bản build của **STARTUP project** ⇒ nếu `src/eCommerce.DbMigrator` chưa build lại thì sinh **MIGRATION RỖNG** mà vẫn in `Done.` ⇒ **luôn mở tệp ra đọc `Up()`**.
14. 🚨 Tệp trong `Data/`, `Assets/`, `appsettings*.json` được **copy vào output** ⇒ sửa nguồn rồi chạy `dotnet test --no-build` là đọc **bản CŨ** (test đỏ/xanh vì lý do không phải điều mình tưởng) ⇒ **`dotnet build` lại** project test.

---

## 5. HAI CỔNG TÀI LIỆU — chạy trước mỗi commit

```powershell
npm --prefix tools run check:mermaid          # 213 khối · 0 lỗi
pwsh -File tools\kiem-link-tai-lieu.ps1       # 0 tham chiếu chết (thoát 1 nếu chết)
```

📌 Mermaid: **cấm `;`** trong text sơ đồ · **cấm `<...>`** làm placeholder (dùng `«...»`).

---

## 6. ĐỊNH NGHĨA "XONG" CỦA MỘT PHIÊN

- [ ] ① **Đo baseline TRƯỚC khi sửa** (`pwsh -File tools\kiem-tat-ca.ps1 -DayDu`) — ghi số, so bảng §3. Chênh lệch lớn ⇒ **báo, không tự diễn giải**.
- [ ] ② Làm **đúng phạm vi** của phiên (§4 của `gd1-coding-sessions.md`). Không ôm việc phiên khác.
- [ ] ③ **Migration + con số 48 + `schema.sql` + test** trong **cùng** thay đổi.
- [ ] ④ **Cập nhật tài liệu feature** (§16) **trong cùng phiên**.
- [ ] ⑤ **`-DayDu` XANH ở cuối phiên** — không mang cổng đỏ sang phiên sau.
- [ ] ⑥ **Báo cáo trung thực**: làm được gì / **chưa** làm được gì / **chưa kiểm chứng** gì. 📌 Câu *"xong"* chỉ được dùng khi ①–⑤ đã đo.
- [ ] ⑦ Nếu bị chặn bởi một quyết định: ghi vào **§15.1 câu hỏi mở** của tài liệu, **đóng phiên với cổng xanh**, **không** tự chọn đáp án rồi code tiếp.

---

## 7. QUYẾT ĐỊNH ĐÃ CHỐT — 🔒 KHÔNG MỞ LẠI

| # | Quyết định | Ở đâu |
|---|---|---|
| D1 | **`Address` (sổ địa chỉ) thuộc `modules/Address`**; `Customers` chỉ giữ bảng liên kết `CustomerAddressLink` | `ADDR-004` **ADR-01** + **10/10 câu hỏi đã chốt** (🟢 Approved 17/10/2026) |
| D2 | **KHÔNG** có value object `Address` ở `eCommerce.Kernel` | `ADDR-004` **ADR-02** |
| D3 | Hàng đợi chuẩn hoá địa chỉ ở **`Address.TrangThai`** (một nguồn), không có bảng `AddrNormalizationReviews` | `ADDR-004` **Q9** |
| D4 | Trần địa chỉ/khách = **cấu hình** (mặc định 20) · `CountryCode` + mã nullable **ngay từ migration đầu** | `ADDR-004` **Q1/Q7** |
| D5 | **VNPay: bypass được lớp NCC/mạng, TUYỆT ĐỐI KHÔNG bypass HMAC.** Merchant test **tự đăng ký được**; `FakePaymentProvider` (chỉ Development, fail-fast) + trang giả lập **ký HMAC thật** | `PAY-001` **§7.7** |
| D6 | Mã lỗi: **code là nguồn sự thật**; dải `eCommerce:Customers:0010…0016` = sổ địa chỉ | `cus-001` §6.2 · `ADDR-004` §6 |
| D7 | 🔒 **Giỏ hàng GĐ1 = GIỎ-COOKIE** (`gio-web`, 2 giờ). Bảng `OrdCarts` **bị loại** khỏi GĐ1 · `MaxLines` = **5** | `sal-001` (khối quyết định đầu tài liệu) |
| D8 | 🔒 **`Percent` hạn mức kênh tính trên tồn TỪNG `(kho, SKU)`** — là *trần an toàn theo kho*, không phải ngân sách kênh | `inv-003` · ⚠️ `S7` phải **đổi nhãn UI** |
| D9 | 🔒 **Xác nhận lệch đối soát = ĐÓNG CẢNH BÁO + audit**, **KHÔNG** tự sinh bút toán điều chỉnh tồn | `rec-001` · `S7` thêm permission `eCommerce.Reconciliation.Confirm` |
| D10 | 🔒 **Storefront MỞ CÔNG KHAI** (`/SanPham*` vào allowlist). Cấm lộ `CostPrice` + tồn chi tiết; allowlist phải có test | `web-002` · `cat-003` · `S8` |
| D11 | 🔒 **CI = GitHub Actions · `ubuntu-latest`** + vá `P-01…P-06`. ✅ Repo **đã có** + đã push: `https://github.com/hungnt9801/eCommerce` (17/10/2026) — ⚠️ **PUBLIC** ⇒ bí mật thật phải vào **GitHub Secrets** | `XC-007` · `S9` |

✅ **KHÔNG còn quyết định nào chờ** (17/10/2026: `Q1`…`Q7` đã chốt — xem D1…D11 ở trên). 🔒 **Không mở lại** các quyết định này; nếu buộc phải lệch, sửa tài liệu tương ứng **trước**.

---

## 8. 「PHIÊN NÀY」

> ✏️ **Điền trước khi gửi** (hoặc chỉ cần gửi: *"làm phiên S…"*).

| Mục | Giá trị |
|---|---|
| **Phiên** | `S…` |
| **Tên phiên** | *(lấy từ `gd1-coding-sessions.md` §4)* |
| **Tài liệu feature phải đọc** | *(1–2 tệp, lấy từ cùng bảng)* |

**Nhắc lại phạm vi các phiên** (chi tiết + cổng ra: `gd1-coding-sessions.md` §4):

| Phiên | Việc |
|---|---|
| `S1` | ✅ **XONG 18/10/2026** — `ADDR-004` phần A: entity `Address` + migration (**48→49**) |
| `S2` | ✅ **XONG 18/10/2026** — `ADDR-004` phần B: `CustomerAddressLink` + API + 4 quyền + PII (**49→51**) · ➕ **bước ⑨ mới**: `probe-so-dia-chi.ps1` (17/17 ca HTTP thật) |
| `S3` | ✅ **XONG 18/10/2026** — `CUS-001` **ĐÓNG**: nối `ChoPhepGiaoDichAsync` (🚨 **LỖI THẬT**: khách `Blocked` vẫn đặt được) + `CustomerGroup` API/seed + phân trang · ➕ **bước ⑩ mới**: `probe-khach-hang.ps1` (26/26 ca HTTP thật) |
| `S4` | ✅ **XONG 18/10/2026** — **`PAY-001` phần A: đường TẠO + cầu nối** (📄 `PAY-001` **§16**): tạo `PaymentTransaction` từ đơn/checkout (`TaoGiaoDichAsync`) · **`MerchantReference` ỔN ĐỊNH** (`SinhMaThamChieu`, cấm `Guid.NewGuid()` mỗi lần) · cập nhật **`Order.PaymentStatus`** qua **ETO** + luật hấp thụ · ⚠️ **outbox bền vững = NỢ** (chủ dự án chốt hoãn) · ➕ đóng `F-PAY-2/3/9`. Cổng ra: cöng "IPN gửi trùng ⇒ 1 giao dịch" ✅ **2 tầng** (domain + unique index **PostgreSQL thật**) · đơn chuyển `Paid` ✅ · unit **+66** · integration **+5** · `-DayDu` **exit 0** |
| `S5` | ✅ **XONG 18/10/2026** — **`PAY-001` phần B: IPN/webhook + hoàn tiền** (📄 `PAY-001` **§16.7**): endpoint **`GET /api/webhooks/vnpay`** `[AllowAnonymous]` + allowlist (ở `HttpApi.Host` — trả lời `Q2`) · **HMAC-SHA512** kiểm trên **query THÔ** ⇒ 🚨 **vá `F-PAY-1`** + phát hiện **lớp thứ hai** (byte chuỗi ký: `+` ↔ `%20`) + **fail-closed khi `HashSecret` rỗng** · **idempotency** (bảng `PayRefunds` + `CHECK` TIỀN: `RefundedAmount ≤ Amount`, `≥ 0`, `Amount > 0`) ⇒ 🚨 **vá `F-PAY-7`** (**mỗi giao dịch một UoW**) · **`PaymentRefundedEto`** ⇒ đơn sang `PartiallyRefunded`/`Refunded` · **job đọc** giá trị `MarkFailed` trả về (đóng nốt `F-PAY-9`) · **job đối soát phát sự kiện** · ➕ **bước ⑪ mới**: `probe-thanh-toan.ps1` (**26 ca HTTP thật**). Cổng ra: **IPN trùng ⇒ 1 lần ghi + bộ đếm 3** ✅ · **sai chữ ký ⇒ `400` + DB không đổi** ✅ · **hoàn vượt ⇒ `0002`** ✅ · unit **+53** · integration **+6** · `-DayDu` **exit 0 · 214.8s**. ⚠️ **Vẫn KHÔNG `Implemented`**: chưa kiểm chứng **tiền thật**, outbox chưa có, `Q6` còn mở |
| `S6` | `SAL-005` — DLQ + `SalMarketplaceEventLogs` + worker `D12` + `update_time` |
| `S7` | UI/API còn thiếu — hạn mức kênh · lịch sử đối soát · retention |
| `S8` | Thanh toán trên UI + đóng `WEB-002` |
| `S9` | `XC-007` — CI thật (`.github` + khả chuyển Linux + vá F2) |
| `S10` | Đóng GĐ1 — nợ nhỏ + `-DayDu` cuối + bàn giao |

✅ **Không phiên nào còn bị chặn**: `Q7` xong (repo + push, 17/10/2026) ⇒ `S9` chạy được ngay; `Q3`…`Q6` đã chốt ⇒ `S7`/`S8` chỉ còn việc của phiên.
📌 **GĐ2** (`SHP-001…004`, `PRO-001…002`) **không** thuộc GĐ1 — cả 6 **0 dòng code**, là chuỗi phiên riêng.

---

## 9. Lịch sử tệp này

| Ngày | Thay đổi |
|---|---|
| 17/10/2026 | Bản đầu. Baseline `-DayDu` **exit 0 · 228.9s** (1181 unit · 38 integration · 213 mermaid · 21/21 DB · 22 ca cổng · 59/59 E2E). Ghi rõ **3 con số đã trôi**. 6 quyết định đã chốt + 5 chưa chốt |
| 18/10/2026 | Cập nhật sau `S1` + `S2`: baseline **175.9s · 9 bước** (1219 unit · 46 integration · 213 mermaid · **23/23** DB · **NND 51** · **57** quyền · **84** chỗ quét · 22 ca cổng · 59/59 E2E · **17/17** sổ địa chỉ). `S2` ✅ XONG ⇒ **`S3` là phiên kế tiếp**. Ghi rõ **phương pháp đo** kích thước mã (vì số cũ không ghi cách đo) |
| 18/10/2026 (sau) | Cập nhật sau `S3`: baseline **203.2s · 10 bước** (1228 unit · 46 integration · 213 mermaid · **23/23** DB · **NND 51** · **58** quyền · **84** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng). `S3` ✅ XONG ⇒ **`S4` là phiên kế tiếp** (`PAY-001` phần A). ➕ Bất biến DB mới: *`CusGroups` = đúng 3 nhóm seed* (thay *"phải rỗng"* sai) · 📌 kích thước mã **đo lại bằng phương pháp ghi rõ** và **cảnh báo không cộng trừ với số cũ** |
| 18/10/2026 (sau nữa) | Cập nhật sau `S4`: baseline **196.0s · 10 bước** (**1294** unit · **51** integration · 213 mermaid · **23/23** DB · **NND 51** · **58** quyền · **84** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng). `S4` ✅ XONG ⇒ **`S5` là phiên kế tiếp** (`PAY-001` phần B). ➕ **Phương pháp đo kích thước mã nay ghi ĐỦ** (thêm `Measure-Object -Line` = **bỏ dòng rỗng**) — nếu thiếu chi tiết đó thì lần đo `.Count` cho **47.191** dòng, lệch **7.967** ⇒ **suýt kết luận sai "code phình 20 %"**; ✅ với phương pháp đúng: **40.291** dòng, và **40.291 − dòng 7 tệp mới ≈ 39.224** = khớp số cũ. 📌 Ghi lại **3 việc còn nợ của `PAY-001`**: endpoint IPN (`S5`) · outbox bền vững (`XC-002`) · **chưa kiểm chứng tiền thật** |
| 18/10/2026 (cuối) | Cập nhật sau `S5`: baseline **214.8s · 11 bước** (**1347** unit · **57** integration · 213 mermaid · **23/23** DB · **NND 52** · **58** quyền · **85** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng · 🆕 **26/26 thanh toán**). `S5` ✅ XONG ⇒ **`S6`/`S7`/`S8` là các phiên kế tiếp** (độc lập nhau). ➕ **Bước ⑪ mới** (`probe-thanh-toan.ps1` — **HTTP thật**, tự ký HMAC bằng **biến môi trường**, **không** commit bí mật). ➕ `PayRefunds` vào nhóm ① của `kiem-db.ps1` (bảng TIỀN ⇒ rác ở đó làm **báo cáo đối soát sai**). 📌 Ghi rõ: **1 migration** (38 → 39) · `schema.sql` regenerate và **kiểm bằng `Compare-Object`** (diff **chỉ** thêm) · **85 chỗ quét là TĂNG ĐÚNG** (bảng mới có PK `uuid`). 🔑 `PAY-001` vẫn **KHÔNG** `Implemented`: **chưa kiểm chứng tiền thật** + **outbox chưa có** + **`Q6`** (ai xác nhận dòng hoàn `ChoXacNhan`) còn mở |
