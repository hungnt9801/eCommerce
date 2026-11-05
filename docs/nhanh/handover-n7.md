# Handover `N7` — WP-06: POS (**hoá đơn bán lẻ + ca thu ngân + thanh toán đa phương thức + in + offline**)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`** / mã nguồn `036adb6`)
> Kế hoạch: [07 §WP-06](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-42](decisions.md), [D-43](decisions.md), [D-44](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-06** (BE việc 1–4 + FE việc 5 + offline việc 6), **không mở rộng phạm vi**.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **51 mục `??`** (rác khảo sát Nhanh, cố ý không commit) |
| 2 | `git log --oneline -1` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 9 warning** (9 `CS8604` có sẵn — Address/Sales) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1855/1855 pass** |
| 6 | Migration / NND | **56** / **74** |

### 1.2 Đã thi công

| # | Việc (07 §WP-06) | Nội dung | Đo được |
|---|---|---|---|
| 1 | Module `Pos` + 6 bảng | **4 project** (`Domain` · `Application.Contracts` · `Application` · `EntityFrameworkCore`) + **6 entity**: `RetailBill` · `RetailBillLine` · `RetailBillPayment` · `CashSession` · `RetailReturn` · `RetailReturnLine` (bảng `Pos*`) | `dotnet ef migrations has-pending-model-changes` = **“No changes”** sau khi áp migration; **6 `DbSet`** (lưới `DbSetRegistrationTests` xanh) |
| 2 | Luật tiền | **Hàm thuần `TinhTienHoaDon`** (thành tiền dòng → tiền hàng → VAT → chiết khấu → điểm → tổng); **chặn chiết khấu 2 tầng** (`0004`); nợ phải có **khách + hạn** (`0006`); **`Payments[]` đa phương thức**; `QuyDoiDiem` (1 điểm = 1.000 đ) | **45 unit test** mới (tổng **1900**) |
| 3 | Posting kho **trong 1 transaction** | Cổng **`IPosStockGateway`** (module `Inventory`): bán ⇒ `GoodsIssue/Mode=Retail` (**`RetailSaleOut`**), trả/huỷ ⇒ `CustomerReturn` — **cùng** `StockPostingService` (một đường ghi sổ cái); cổng **idempotent theo số chứng từ** | `OnHand` 10 → **8 / 9 / 9** sau khi bán 2/1/1; huỷ ⇒ **nhập lại** kho; bất biến `tổng sổ cái = OnHand` **9 = 9** cho cả 3 SKU |
| 4 | API `/api/v1/pos` | **9 đường**: `GET/POST bills`, `GET bills/{id}`, `POST bills/{id}/void`, `GET sessions/current`, `GET sessions`, `GET sessions/{id}`, `POST sessions/open`, `POST sessions/{id}/close`, `GET/POST returns`, `GET returns/{id}` — **10 path** trong OpenAPI | OpenAPI **28/28 path** `/api/v1` (18 cũ + **10** mới); key **idempotent** `ClientBillId`; 4 quyền mới `Pos.Sell/Void/Session.Manage/Return` |
| 5 | FE màn POS (#12) | **Full-screen** trong shell, **đa tab** lưu **IndexedDB**, phím **F1/F3/F4/F6/F8/F9/F10**, quét/gõ sản phẩm (ưu tiên **tra barcode**), thanh toán đa phương thức, tiền khách đưa/tiền thối, ghi nợ + hạn, **mở ca/kết ca** ngay trên màn | `pnpm build` ✓ — **18 route**; `pos/bill` **8,37 kB** (114 kB) |
| 6 | FE DS hoá đơn (#13) + in + trả hàng (#14) | DS: 12 cột + **icon phương thức**, lọc server-side, **Huỷ có lý do** (ẩn khi ca đã kết) · In: `/{business}/print/bill/{id}` khổ hẹp (F10 mở cửa sổ mới) · Trả hàng: chọn hoá đơn → dòng trả (giới hạn `SL − đã trả`) → phí → **trước/sau khi trừ nợ** hiển thị rõ | 3 route: `pos/bill/list` 1,3 kB · `pos/return` 4,97 kB · `print/bill/[id]` 369 B |
| 7 | Offline | **Hàng đợi IndexedDB** (`posOutbox`) + nhãn **“chưa đồng bộ”** + nút **Đồng bộ**; F9 khi mất mạng ⇒ hoá đơn **vào outbox**, mạng lại ⇒ tạo trên server, **không** tạo trùng | Browser thật: rớt mạng (`Network.emulateNetworkConditions offline`) + F9 ⇒ **“Đồng bộ 1 hoá đơn chờ”**; mạng lại + Đồng bộ ⇒ **“1 thành công · 0 còn chờ”**, server có **đúng 1** hoá đơn |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · **không** phát sinh warning mới |
| `dotnet test test\eCommerce.Tests` | **1900/1900 pass** (**+45** so với 1855) |
| `dotnet test test\eCommerce.IntegrationTests` | **90/90 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — **18 route** (`pos/bill` 8,37 kB · `pos/bill/list` 1,3 kB · `pos/return` 4,97 kB · `print/bill/[id]` 369 B) |
| `pnpm spec:snapshot` + `pnpm gen:api` | **28/28 path** `/api/v1` (12 cũ + 6 đơn + **10 POS**) |
| Migration | **57** — `20261104015230_Wp06_Pos_BanLe` (có trong `__EFMigrationsHistory`) |
| `schema.sql` | sinh lại từ chính migration: **2.806 → 3.725 dòng** (**+919**, 0 xoá) |
| NND (DB dev) | **74 → 79** (+5 index POS, **cả 5 viết tay `NULLS NOT DISTINCT`**) |
| `pwsh -File .\tools\probe-pos.ps1` | **`exit 0`** — **34 lời gọi HTTP** (9 API Host + 2 trang + 23 qua BFF) |
| `pwsh -File .\tools\kiem-pos-browser.ps1` | **`exit 0`** — **10/10 ca browser** (Chrome headless + CDP) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (xem §1.5) |
| DB | bất biến `tổng sổ cái = OnHand` = **9 = 9** cho cả 3 SKU của probe |

### 1.4 Bằng chứng HTTP thật — `tools/probe-pos.ps1` (`exit 0`)

Setup **toàn bộ bằng API thật** (không seed SQL — khác `probe-don-hang.ps1`): tạo kho → nhập 10 × 3 SKU → post
→ tạo khách → đăng nhập **NextAuth** → gọi qua **BFF** `/api/proxy` (đúng đường client component dùng, `D-12`).

| # | Ca | Kết quả đo |
|---|---|---|
| ① | `GET …/pos/bill` | **200** — *“Bán lẻ tại quầy”*, *“CHƯA MỞ CA”* |
| ② | `POST …/pos/sessions/open` (tiền đầu ca 500.000) | **200** — `status Open` |
| ②b | mở ca **lần hai** cùng quầy | **403** + `eCommerce:Pos:0012` (*một quầy một ca*) |
| ③ | `GET …/pos/sessions/current` | **200** — đúng ca vừa mở |
| ④ | `POST …/pos/bills` 3 SP + **2 phương thức** (50.000 tiền mặt + 100.000 CK) | **200** — `totalPayment 150.000`, `paidAmount 150.000`, nợ **0**, thối **0** |
| ⑤ | `GET …/pos/bills/{A}` | **200** — **2** dòng thanh toán, **2** phương thức |
| ⑥ | `GET …/stock/balances?sku=` × 3 | **200** — `OnHand` **8 / 9 / 9** (bán 2/1/1 từ 10) |
| ⑦ | gửi lại **cùng `ClientBillId`** | **200** + `alreadyExisted = true`, cùng số hoá đơn; `OnHand` **vẫn 8** (không trừ lần hai) |
| ⑧ | chiết khấu **cả dòng và tổng** | **403** + `eCommerce:Pos:0004` |
| ⑨ | ghi nợ **thiếu khách/hạn** | **403** + `eCommerce:Pos:0006` (`conNo 20.000`) |
| ⑩ | bán hoá đơn B **ghi nợ 20.000** (có khách + hạn) | **200** — `debtAmount 20.000` |
| ⑩i | `POST …/pos/returns` trả 1 × A (hoá đơn A đã trả đủ) | **201** — `goodsAmount 50.000`, `debtReduction 0`, `refund 50.000`; **cân bằng** `hàng = nợ + hoàn + phí` |
| ⑩j | `GET …/stock/balances?sku=` sau trả | `OnHand` A **9** (8 + 1 nhập lại) |
| ⑪ | trả **vượt** số đã mua (còn 1, đòi trả 5) | **403** + `eCommerce:Pos:0019` (`conLaiChuaTra 4`) |
| ⑫ | `POST …/bills/{C}/void` (lý do nêu rõ) | **200** — `status Voided`, nhập lại kho; **huỷ lần hai** ⇒ **200** + `voided = false` |
| ⑬ | `GET /{business}/print/bill/{A}` | **200** — *“HOÁ ĐƠN BÁN LẺ”*, số hoá đơn, *“PHẢI TRẢ”* |
| ⑭ | `POST …/sessions/{id}/close` (bàn giao 500.000) | **200** — `cashRevenue 50.000`, `cashRefund 50.000`, `expectedCash 500.000`, `variance 0` |
| ⑭b | kết ca **lần hai** | **403** + `eCommerce:Pos:0010` (*“đã kết ca là không quay ngược”*) |
| ⑮ | huỷ hoá đơn **của ca đã kết** | **403** + `eCommerce:Pos:0010` (*“hãy tạo PHIẾU TRẢ HÀNG”*) |
| ⑯ | SQL bất biến `tổng sổ cái = OnHand` × 3 SKU | **9 = 9** · **7 = 7** · **9 = 9** |

### 1.4b Bằng chứng **BROWSER THẬT** — `tools/kiem-pos-browser.ps1` (`exit 0`, Chrome headless + CDP)

Ba yêu cầu của `04 §6.3` **chỉ** đo được trong trình duyệt; script tự dựng **kho + sản phẩm + biến thể SKU +
phiếu nhập + ca** qua chính BFF của phiên đăng nhập:

| # | Ca | Kết quả đo |
|---|---|---|
| — | Đăng nhập NextAuth (form thật, fallback cùng endpoint credentials) | path **`/230222`** |
| ① | Trang POS render với ca đang mở | **có** *“Bán lẻ tại quầy”* + `POS-CA-…` |
| ② | Gõ tên SP + Enter | **đúng 1 dòng** (`[data-pos-dong]` = 1) |
| ②b | Thêm thanh toán **tiền mặt** đủ số còn lại | nút *“Thêm Tiền mặt…”* bấm được |
| ③ | **F1** | **2 tab** (`[data-pos-tab]` = 2) |
| ④ | **F5 (reload)** | **vẫn 2 tab** (đọc lại **IndexedDB**) |
| ⑤a | Tab 1 sau F5 | **vẫn đúng 1 dòng hàng** + **đúng thứ tự tab** |
| ⑤ | **Rớt mạng** + **F9** | toast *“MẤT MẠNG”* + **“Đồng bộ 1 hoá đơn chờ”** |
| ⑥ | Mạng lại + **Đồng bộ** | **“1 thành công · 0 còn chờ”** |
| ⑦ | Server đếm hoá đơn của quầy | **đúng 1** (không tạo trùng) |

📸 Ảnh màn hình của lượt chạy cuối: `tools/_pos-browser.png` (toast *“Đồng bộ xong: 1 thành công · 0 còn chờ”*).
📌 Ảnh **không** được commit (cùng nhóm rác `tools/_*` của khảo sát Nhanh).

> ⚠️ **Ba lỗi THẬT do probe/browser bắt (đáng ghi):**> ① **`23505 PK_PosRetailBillLines`** — để EF quản lý `HasMany(Lines)` thì `InsertAsync(bill)` **cascade insert**
>    dòng, rồi app service `InsertManyAsync` **lần nữa** ⇒ hoá đơn **không lưu được** dù 1900 test xanh.
>    Sửa: `Ignore` navigation + FK `HasOne<RetailBill>().WithMany()` (D-43).
> ② **Gửi lại cùng `ClientBillId` trả `0016`** — so `Lines.Count` trên danh sách **rỗng** (navigation bị
>    `Ignore` nên `FirstOrDefaultAsync` không kèm dòng) ⇒ **mọi** lần gửi lại bị coi là “khác nội dung”.
>    Sửa: `GanDong(...)` **trước** khi so.
> ③ **Tab đổi chỗ sau F5** — `IndexedDB.getAll()` trả theo **khoá** (`clientBillId` là UUID ngẫu nhiên) ⇒ tab
>    có dòng hàng nhảy sang vị trí 2, người dùng tưởng mất hàng. Sửa: lưu `taoLuc` + sắp lại khi tải.

### 1.5 Cổng tài liệu + Mermaid

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** (số đo ở §7). `npm --prefix tools run check:mermaid` →
**PASS**. Mọi tài liệu mới/sửa (`handover-n7`, `decisions` D-42…D-44, `07 §13`, `08 §3/§8`, `README`) đã được
cổng này quét — **0 link chết**.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **vẫn nằm trong WP-06** nhưng **chưa làm** (cắt có lý do):

1. **`RetailReturn` chưa có đường HUỶ** (`RetailReturnStatus.Voided` khai nhưng **chưa có API**) — huỷ phiếu trả
   phải **xuất lại** hàng + đảo tiền/điểm; cần use case thật (khách đổi ý sau khi trả) mới làm, tránh nửa vời.
2. **Service worker / tự đồng bộ nền** — `03 §7` ghi *"service worker"*; hiện có **outbox IndexedDB + nút Đồng bộ**
   (và **tự** đẩy vào outbox khi `fetch` ném). 📌 Thiếu: **Background Sync API** để tự gửi khi có mạng — cần PWA
   manifest + HTTPS (dev đang **http** `localhost:3000` ⇒ Background Sync **không** chạy trên http thường).
3. **Xung đột tồn khi đồng bộ muộn** (`StockVariance` trong `07`) — nếu outbox được gửi **sau khi** người khác đã
   bán hết hàng, server trả `eCommerce:Inventory:0001`; hiện outbox **giữ nguyên** hoá đơn đó + báo mã lỗi cho
   thu ngân, **chưa** có bảng ghi lệch để đối soát tự động.
4. **3 chế độ quét mã** (`nhanh-pos-module-map.md` §5.1: tìm SP / cân điện tử / **IMEI**) — mới có **2** (barcode
   + tên); **IMEI/serial** chưa nối vào POS (bảng `CatProductImeis` có, nhưng chưa có API tra/giữ serial).
5. **Quà tặng / BHMR / combo** — chưa có dòng quà tặng và **“nợ quà tặng”** (`§5.1`).
6. **Điểm tích luỹ**: **ghi nhận** trên hoá đơn (`PointsUsed/PointsEarned/PointsReversed`) nhưng **chưa** cộng/trừ
   vào khách — `Customer` **chưa có** `Points/DebtBalance` (việc của **WP-07**). 📌 Đừng tin “đã trừ điểm khách”.
7. **Trả hàng không hoá đơn** + **trả kèm mua mới** (`§5.4`) — P0 chỉ **trả từ hoá đơn** (D-42).
8. **Mẫu in cấu hình được** (P0 #18): route in **cố định**; chưa có editor/preview/version.
9. **Bulk trên DS hoá đơn** (xuất Excel, gắn nhãn, tạo HĐĐT nháp) — chưa làm (thuộc P1 #12–#14 của Nhanh).
10. **Màn “Lịch sử kết ca” riêng** — API `GET /pos/sessions` đã có, **chưa** có màn (kết ca hiện trong POS).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **`20261104015230_Wp06_Pos_BanLe`** (migration thứ **57**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` — `cd src\eCommerce.DbMigrator; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run` |
| Nội dung | **6 bảng** (`PosRetailBills` · `PosRetailBillLines` · `PosRetailBillPayments` · `PosCashSessions` · `PosRetailReturns` · `PosRetailReturnLines`) + **13 index** (5 unique **có `NULLS NOT DISTINCT`** — **viết tay**) + **9 `CHECK`** |
| `Down` | Có: xoá 6 bảng (mất hoá đơn/ca/phiếu trả đã bán — không tái tạo được) |
| Cách lùi | `dotnet ef database update 20261103040705_Wp05_DonHang_Nhanh -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+919 dòng, 0 xoá** (2.806 → 3.725); `PostgreSqlSchemaTests` **79 NND** xanh |
| Ánh xạ mã lỗi | **19** ràng buộc mới đã map trong `ViPhamRangBuocDb` (`Pos:0002/0003/0004/0006/0012/0013/0018/0019/0020/0024/0025/0026/0027` + `MaChung` cho 6 `CHECK` phòng vệ) — thiếu là `RangBuocDbChuaAnhXaTests` **ĐỎ** |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-42** | POS: aggregate riêng · **tiền do hàm thuần** · chiết khấu **một tầng** · nợ phải có khách + hạn · **idempotent theo `ClientBillId`** · **một quầy một ca** · huỷ = **đảo** và **cấm khi ca đã kết** · `TienMatThucThu` **trừ tiền thối** · **chỉ trả từ hoá đơn** (trừ nợ trước, hoàn tiền sau) · điểm **chỉ ghi nhận** |
| **D-43** | POS **không tự ghi sổ cái** — cổng `IPosStockGateway`; navigation `Lines/Payments` bị **`Ignore`** + FK `HasOne/WithMany`; **phải nạp dòng trước khi so idempotency** |
| **D-44** | Số chứng từ POS **đếm theo ngày** (nợ: không nguyên tử) + **4 mã lỗi riêng** cho trùng số/khoá (`0024` hoá đơn · `0025` phiếu trả · `0026` khoá idempotency · `0027` ca) |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. ⚠️ **Số chứng từ đếm-rồi-cộng-1 KHÔNG nguyên tử**: hai máy POS cùng lúc ⇒ DB chặn (`0024`/`0025`/`0027`) và
   client **phải gửi lại**. 📌 Cách đúng: **sequence của DB** (hoặc `nextval`) — làm ở **WP-09** cùng E2E.
2. ⚠️ **`Ignore` navigation là BẮT BUỘC, không phải tuỳ chọn**: ai “sửa cho đẹp” bằng `HasMany(...)` sẽ làm
   `InsertAsync(bill)` **cascade insert** dòng ⇒ `23505` ⇒ **không lưu được hoá đơn** (đã đo). Xem D-43.
3. ⚠️ **`TienMatThucThu` phải trừ `ChangeAmount`**: nếu ai đổi thành Σ tiền mặt, **mọi ca đều báo thiếu** đúng
   bằng tiền thối, và thu ngân bị quy trách nhiệm oan.
4. ⚠️ **Huỷ hoá đơn của ca đã kết bị CHẶN** (`0010`) — đừng “nới” để tiện: tiền mặt đã **bàn giao và đếm**;
   sửa sau đó phải là **phiếu trả hàng** (có chứng từ).
5. ⚠️ **Outbox không tự đồng bộ**: mất mạng rồi đóng trình duyệt ⇒ hoá đơn **nằm trên máy** tới khi có người bấm
   “Đồng bộ”. 📌 Màn POS **hiện số hoá đơn chờ** — nhưng nếu thu ngân bỏ qua thì **doanh thu thiếu** cho tới lần
   sau. Cần job/thông báo (WP-09) hoặc Background Sync (cần HTTPS).
6. ⚠️ **Điểm chỉ là con số trên hoá đơn**: `PointsEarned/PointsUsed` **không** làm thay đổi `Customer`.
   Đừng báo cáo “khách đã tích điểm” cho tới khi `WP-07` có sổ điểm.
7. ⚠️ **`POST /pos/bills` gửi lên `payments` rỗng ⇒ thành hoá đơn GHI NỢ**: đúng nghiệp vụ, nhưng nếu UI quên
   thêm thanh toán thì lỗi hiện ra là `Pos:0006` (thiếu khách/hạn) — **không** phải “quên thanh toán”.
8. 📌 **Probe để lại dữ liệu** (`WP06-…`, `POS-…`, `PT-…`, `PN-WP06-…`) và **browser check** để lại
   (`WP06B-…`): nhiều kho + hoá đơn + sản phẩm — **cố ý**, để phiên sau kiểm bằng SQL.

---

## 6. Việc đầu tiên của phiên sau

1. **`N8` — WP-07**: `Customer.Points/AccumulatedAmount/DebtBalance/DebtLimit` + `CustomerLevel` + áp **điểm/công
   nợ** cho POS (mở lại §5 nợ #6) + `Store/Staff/RoleDataScope` + **data scope** trên `Orders`/`RetailBills`.
2. Khi có `WP-07`: **gộp điểm/công nợ** vào `RetailBill.HoanTat` (một transaction) — đừng ghi sổ điểm ở FE.
3. (Nếu làm `WP-08`) doanh thu phải = `SUM(RetailBills.Completed) + SUM(Orders)` — hoá đơn **Voided** bị loại.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
dotnet test test\eCommerce.IntegrationTests
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\probe-pos.ps1              # 34 lời gọi HTTP (tự bật/tắt Host + Next)
pwsh -File .\tools\kiem-pos-browser.ps1       # 10 ca browser thật (Chrome headless + CDP)
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N7`) |
| Commit | **chưa commit** phiên này — mã nguồn WP-06 nằm trong working tree; commit kế tiếp theo khuôn `N5+N6` (`git add` **đường dẫn cụ thể**, **KHÔNG** `git add -A` vì còn **51 mục rác khảo sát**) |
| Push / PR | Chưa |
| Tệp `N7` đã chạm | **BE:** `src/eCommerce.Domain.Shared/{Enums.Pos.cs,Pos/PosErrorCodes.cs,eCommerceConsts.cs}` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `modules/Pos/**` (4 project: 12 tệp) · `modules/Inventory/…Application.Contracts/IPosStockGateway.cs` · `modules/Inventory/…Application/PosStockGateway.cs` · `src/eCommerce.EntityFrameworkCore/{eCommerceDbContext.cs,eCommerce.EntityFrameworkCore.csproj,Migrations/*Wp06*,Migrations/eCommerceDbContextModelSnapshot.cs}` · `src/eCommerce.Application/{eCommerceApplicationModule.cs,eCommerce.Application.csproj}` · `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs` · `src/eCommerce.HttpApi/{Controllers/PosController.cs,eCommerce.HttpApi.csproj,eCommerceHttpApiModule.cs}` · `test/eCommerce.IntegrationTests/{Assets/schema.sql,PostgreSqlSchemaTests.cs}` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}` · `test/eCommerce.Tests/Pos/{Wp06PosBanLeTests,Wp06PosApiHopDongTests}.cs` · `eCommerce.slnx`. **FE:** `lib/{pos.ts,menu.ts}` · `components/pos/{PosTerminal,ReturnForm,VoidBillButton}.tsx` · `app/[business]/pos/{bill/page.tsx,bill/list/page.tsx,return/page.tsx}` · `app/[business]/print/bill/[id]/page.tsx`. **Khác:** `tools/{probe-pos.ps1,kiem-pos-browser.ps1,kiem-pos-browser.mjs}` · docs (`decisions`, `07`, `08`, `README`, `handover này`) |
