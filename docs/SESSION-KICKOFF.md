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

## 3. Trạng thái ĐO ĐƯỢC — baseline chuẩn (đo 17/10/2026, `-DayDu` exit 0)

```
[B1] dọn tiến trình + cổng trống      2.0s
[B2] build + KIỂM MỐC nhị phân       55.0s   · 68 tệp DLL/EXE đổi mốc
[B3] unit test                        3.3s   · 1205/1205
[B4] integration (PostgreSQL thật)    7.2s   ·   41/41
[B5] mermaid (parser thật)           32.4s   ·  213 khối · 0 lỗi
[B6] bất biến DB                      5.4s   ·   21/21 · NND = 49 · 53 quyền · quét 83 vị trí probe
[B7] test cho chính các cổng         74.9s   ·   22 ca (A…F · R…V · L…P · G…K)
[B8] E2E có phiên đăng nhập          19.1s   ·   59/59
     ─────────────────────────────────────
     TỔNG                            198.7s
```

**Code:** 295 tệp · ~40.350 dòng (chưa tính test) · 94 tệp test · ~18.340 dòng · **37 migration** (đo lại cuối `S1`, 18/10/2026).
**Git:** nhánh `main` · **273 commit** · **cây sạch** · **`origin` = `https://github.com/hungnt9801/eCommerce`** (đã push, `refs/heads/main` = `ab9a7ff`) · ⚠️ repo đang **PUBLIC**.

🚨 **Ba con số ĐÃ TRÔI so với bàn giao — dùng số ở trên, đừng dùng số cũ:**
| Số cũ (handover) | Số đo 17/10/2026 | Vì sao |
|---|---|---|
| ~145–155s | **198.7s** | cổng ⑦ thêm 5 ca `R…V` (link) · ⑤ mermaid 213 khối · 📌 đổi theo máy/nhiệt độ, **không** phải chỉ tiêu |
| 17 ca test cổng | **22 ca** | +5 ca cho cổng tham chiếu tài liệu |
| 1181 unit / 38 integration / NND 48 | **1205 / 41 / 49** | `S1` thêm 24 unit + 3 integration + 1 unique index |
| 197 khối mermaid | **213 khối** | +16 từ 3 tài liệu mới (`ADDR-004`, `PAY-001`, `XC-007`) |

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
| `S2` | `ADDR-004` phần B — `CustomerAddressLink` + API + quyền + PII (49→51) |
| `S3` | `CUS-001` đóng — **nối `ChoPhepGiaoDichAsync`** (lỗi thật: khách `Blocked` vẫn đặt được) + `CustomerGroup` + phân trang |
| `S4` | `PAY-001` A — đường **TẠO** giao dịch + cầu nối `Order.PaymentStatus` + ETO |
| `S5` | `PAY-001` B — **IPN/HMAC** + hoàn tiền + vá **F-PAY-1** và **F-PAY-7** |
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
