# GĐ1 — CHIA PHIÊN CODING (viết 17/10/2026)

> **Câu hỏi:** *"Giai đoạn 1 phần coding có cần chia thành nhiều session không?"*
> **Trả lời: CÓ — 11 phiên** (`S0`…`S10`). Lý do ở §1, danh sách ở §4.
> Đọc kèm: [`handover/BAN-GIAO-GD1-2026-10-17.md`](handover/BAN-GIAO-GD1-2026-10-17.md) ·
> [`implementation-plan.md`](implementation-plan.md) §7 / Phụ lục A · [`features/README.md`](features/README.md).

---

## 1. Vì sao PHẢI chia — ba lý do ĐO ĐƯỢC, không phải khẩu vị

| # | Lý do | Số đo |
|---|---|---|
| 1 | **Mỗi phiên phải kết thúc bằng cổng XANH, và cổng rất đắt** | `tools\kiem-tat-ca.ps1 -DayDu` = **8 bước · ~145–155 giây** (② build + kiểm mốc · ③ **1181** unit · ④ **38** integration · ⑤ mermaid · ⑥ **21** bất biến DB · ⑦ **22** ca test cổng — 17 gốc + 5 ca `R`…`V` mới · ⑧ **59** ca E2E). 📌 Một phiên ôm 3 feature ⇒ cổng đỏ ở đâu **không biết quy cho feature nào** |
| 2 | **Mỗi tài liệu thiết kế là một "cửa vào" rất lớn** | 19 tài liệu GĐ1, **~100–240 KB/tài liệu** (lớn nhất `inv-001` 2.843 dòng, `web-001` 3.538 dòng). 📌 Một phiên đọc **1–2** tài liệu + code + tự kiểm là **vừa hết ngân sách ngữ cảnh**; đọc 4 tài liệu là **chắc chắn quên chi tiết** — và quên chi tiết ở đây là **sai bất biến** |
| 3 | **6 quyết định CHẶN — ✅ ĐÃ CHỐT TOÀN BỘ 17/10/2026** (`Q1`…`Q6`) + `Q7` (repo) ⇒ hiện **0 chặn** | 🔑 Nếu gộp vào phiên code, phiên đó **dừng giữa đường** hoặc tệ hơn: **tự chọn** một đáp án rồi code 2.000 dòng theo nó. 📌 Tách `S0` (chỉ chốt quyết định, **0 dòng code**) là cách rẻ nhất để không lặp lại lỗi "nói quá" của phiên trước |

**Quy mô hiện tại (đo 17/10/2026):** **292 tệp · ~39.800 dòng** code sản phẩm · **89 tệp · ~17.900 dòng** test ·
**36 migration**. ⇒ Đây không phải dự án nhỏ; GĐ1 còn lại **không** gói vừa một phiên.

---

## 2. Hiện trạng GĐ1 — đo lại, và **sửa con số đã trôi**

🚨 **Bảng "⏳ CÒN" trong `implementation-plan.md` §7.1 đã LỖI THỜI** ở nhiều dòng. Đo lại bằng `grep`/`Test-Path`:

| Hạng mục | Plan ghi "CÒN" | Đo được hôm nay | Kết luận |
|---|---|---|---|
| `CAT-003` media | *"0 dòng code"* | ✅ `ProductMedia` + `ProductChannelVisibilities` (**DbSet** `eCommerceDbContext.cs:43,50`) + **2 migration** (`20261016112214_Cat003_ProductMedia`, `20261016135724_Cat003_ProductChannelVisibility`) + `ProductMediaAppService` | ✅ **XONG** — không làm lại |
| `CAT-003` slug/SEO | *"chưa tự sinh khi publish"* | ✅ migration `20261016070004_Cat003_ProductSlug_Unique` | 🟡 chỉ còn **quyết định** mở storefront công khai |
| `SAL-005` watermark | *"handler CHƯA nối luật watermark"* | ✅ `ApDungWatermark` **có** trong `MarketplaceOrderIntakeHandler.cs` | ✅ **XONG** |
| `WEB-001` menu/quyền | *"CÒN menu theo permission"* | ✅ `MenuViewComponent.cs` + `MenuRenderTests` (10 ca) | ✅ **XONG** |
| `REC-001` `querydr` | *"đối soát thanh toán chưa làm"* | ✅ `PaymentReconciliationWorker.cs` **tồn tại** | ✅ **XONG** (còn API/UI lịch sử) |
| `CUS-001` xuất PII | *"chưa xuất dữ liệu cá nhân"* | ✅ `CustomerDataExportLog` + `CsvNhatKy` | ✅ **XONG** |
| `SAL-006` webhook/job | *"chưa có webhook controller, chưa có job querydr"* | ✅ worker có; còn phiếu RMA | 🟡 còn nhỏ |

### 2.1 Việc THẬT SỰ còn lại của GĐ1 (có bằng chứng)

| # | Việc | Bằng chứng "chưa có" | Cỡ |
|---|---|---|---|
| 1 | **`Address` (sổ địa chỉ)** | ✅ **XONG `S1` (18/10/2026)** — entity + migration + index `NULLS NOT DISTINCT` + 27 test. 📄 [`ADDR-004`](features/address/addr-004-so-dia-chi-khach.md) §16 | ✅ XONG |
| 2 | **`CustomerAddressLink` + API + quyền** | ✅ **XONG `S2` (18/10/2026)** — entity + bảng `CusAddressLinks` + **2 index `NULLS NOT DISTINCT`** (⇒ NND **49 → 51**) + 2 app service + 4 permission (khai **hai nơi**) + `IAddressDirectory` + ETO + xuất PII + probe HTTP **17/17**. 📄 [`ADDR-004`](features/address/addr-004-so-dia-chi-khach.md) §16.5 | ✅ XONG |
| 3 | **`Address` (sổ địa chỉ)** | ✅ **XONG `S1` (18/10/2026)** — entity + migration + index `NULLS NOT DISTINCT` + 27 test. 📄 [`ADDR-004`](features/address/addr-004-so-dia-chi-khach.md) §16 | ✅ XONG |
| 3b | 🚨 **Cổng khách bị chặn** | ✅ **XONG `S3` (18/10/2026)** — trước đó `ChoPhepGiaoDichAsync` **định nghĩa 1 nơi, gọi 0 nơi** ⇒ **khách `Blocked` VẪN đặt được hàng** (`gd1-coding-sessions.md` §2.1 #3 — lỗi thật). Nay: cổng `CuaKiemKhachGiaoDich` gọi ở **2 đường bán hàng**, **TRƯỚC** mọi tác dụng phụ · chứng minh bằng **HTTP thật** (`403` + `0005`, **0 đơn / 0 giữ chỗ / 0 sổ giá**) + **2 chiều ngược**. 📄 [`CUS-001`](features/customers/cus-001-ho-so-khach-hang-so-dia-chi.md) §28 | ✅ XONG |
| 4 | **Đường TẠO giao dịch thanh toán** | `new PaymentTransaction` chỉ có trong **test** (3 tệp, 0 tệp sản phẩm) | LỚN |
| 5 | **Endpoint IPN/webhook** | `PaymentHub` **0 controller** (`[Route(` = 0) ⇒ VNPay gọi vào **không có cửa** | LỚN |
| 6 | **Cầu nối `Order.PaymentStatus`** | `Order.cs` có trường; **không nơi nào** cập nhật từ thanh toán ⇒ *"đơn đã trả vẫn `Unpaid`"* | VỪA |
| 7 | **ETO/outbox cho thanh toán** | `IHasEventOutbox` không có trong `PaymentHub` | VỪA |
| 8 | **Sổ hoàn tiền + `CHECK`** | `RegisterRefund` có ở entity; **không** có bảng dòng hoàn tiền, **không** có `CHECK RefundedAmount <= Amount` | VỪA |
| 9 | **DLQ + `SalMarketplaceEventLogs` (D7) + job `D12`** | ✅ **XONG `S6` (18/10/2026)** — bảng + entity + writer + ghi **mọi nhánh** + **bước ⑫ 39/39**. 📄 [`SAL-005`](features/sales/sal-005-webhook-don-tu-san.md) **§20** | ✅ XONG |
| 10 | **UI/API còn thiếu**: hạn mức kênh (Web), `/KhachHang` phân trang, lịch sử lượt quét đối soát | handover §7 việc #6 | NHỎ |
| 11 | **Thanh toán trên UI** (checkout → cổng → kết quả) | `PaymentHub` không có controller; `/DatHang` chưa có bước thanh toán | VỪA |
| 12 | **CI (`.github`)** | ✅ repo từ xa **đã có** (`https://github.com/hungnt9801/eCommerce`, đã push) nhưng `Test-Path .github` = **False** | VỪA |
| 13 | **Nợ nhỏ**: CSV `;` · 5/12 metric chưa có nơi phát · `CHN-002` supervisor `Timer.RunOnStart` · xác nhận outbox/relay | handover §7 việc #5,#7 · §8 | NHỎ |

📌 **Ước lượng:** ~**8–12 phiên-la-động** (person-session) cho §2.1 — chưa tính GĐ2.

### 2.2 KHÔNG thuộc GĐ1 (đừng gộp vào)

`SHP-001…004` (vận chuyển) và `PRO-001…002` (khuyến mãi) là **GĐ2**, và **0 dòng code**
(`grep ShippingHub` = 0 · `grep eCommerce.Promotions` = 0). 🔑 Đây là **phần lớn nhất còn lại của dự án**
(`handover` §3.2) ⇒ **một chuỗi phiên riêng**, không nhồi vào GĐ1.

---

## 3. Quyết định — ✅ **TẤT CẢ ĐÃ CHỐT** (`Q1`…`Q6` + `Q7`), ngày 17/10/2026

| # | Câu hỏi | Ai trả lời | Chặn việc gì | Nếu không trả lời thì sao |
|---|---|---|---|---|
| Q1 | **Credential sandbox VNPay** | chủ dự án | ✅ **ĐÃ CHỐT 17/10/2026** | ✅ **KHÔNG còn chặn `S4`/`S5`**: VNPay cho **tự đăng ký merchant test** ([devreg](https://sandbox.vnpayment.vn/devreg/), **5–10 phút**, không cần hợp đồng) — và bypass hợp lệ bằng **`FakePaymentProvider`** (chữ ký HMAC **vẫn thật**). 📄 Thiết kế: [`PAY-001` §7.7](features/payment/pay-001-cong-thanh-toan-vnpay.md). ⚠️ Chỉ `DoD-tiền-thật` cần credential + tunnel |
| Q2 | **Sổ địa chỉ: duyệt `ADDR-004`** | chủ dự án | ✅ **ĐÃ DUYỆT 17/10/2026** — [`ADDR-004` v1.0](features/address/addr-004-so-dia-chi-khach.md), **10/10 câu hỏi đã chốt** | ✅ **`S1`/`S2` MỞ KHOÁ**. 📌 6 dòng đồng bộ tài liệu còn lại (§15.3 #9/#11/#12) + code/test (#15–#18) **là điều kiện của `S1`** |
| Q3 | **Giỏ hàng: entity `OrdCarts` hay giỏ-cookie?** | chủ dự án | ✅ **`A` — GIỎ-COOKIE** (17/10/2026) | 🔒 **Chốt:** giỏ GĐ1 = cookie `gio-web` (HttpOnly, 2 giờ) trong `DatHangController.cs:44-48`; **bảng `OrdCarts` BỊ LOẠI** khỏi GĐ1 (giữ làm dự phòng GĐ2). 📄 Ghi tại [`sal-001`](features/sales/sal-001-gio-hang.md) đầu tài liệu + ngay tại §5.2. 📌 `MaxLines` = **5** theo UI thật (thiết kế ghi 100). ❌ Mất: giỏ đa thiết bị · khôi phục giỏ · tính giá/khuyến mãi ở bước giỏ |
| Q4 | 🚨 **`Percent` hạn mức kênh tính trên CÁI GÌ?** (~~`DonVi` theo dòng~~ — đã xong) | chủ dự án | ✅ **`A` — GIỮ per `(kho, SKU)`** (17/10/2026) | 🔒 **Chốt:** `floor(tồnKhảDụng × giaTri/100)` trên tồn **từng `(kho, SKU)`** (`HanMucKenh.cs:13,45`) — nó là **trần an toàn theo kho**, **không** phải ngân sách kênh. ❌ Bị loại: tính trên tổng kênh (cần `StockAllotment` — code chưa có). ⚠️ **`S7` phải đổi nhãn UI** thành *"trần an toàn của kênh theo từng kho"*. 📄 [`inv-003`](features/inventory/inv-003-phan-bo-ton-theo-kenh.md) |
| Q5 | **Ai xác nhận lệch đối soát, và xác nhận thì LÀM GÌ?** | chủ dự án | ✅ **`A` — ĐÓNG CẢNH BÁO + audit, KHÔNG tự sửa tồn** (17/10/2026) | 🔒 **Chốt:** xác nhận ⇒ ghi `ConfirmedByUserId/ConfirmedAtUtc/ConfirmNote` rồi **đóng cảnh báo**; ❌ **bị loại** phương án tự sinh bút toán điều chỉnh tồn (job không được sửa sổ cái). 📌 `S7` phải thêm: 3 cột + permission **`eCommerce.Reconciliation.Confirm`** (chưa tồn tại) + endpoint + audit. 📄 [`rec-001`](features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md) |
| Q6 | **Mở storefront công khai?** | chủ dự án | ✅ **`A` — MỞ CÔNG KHAI** (17/10/2026) | 🔒 **Chốt:** `/SanPham` + `/SanPham/ChiTiet/{slugOrSku}` vào **allowlist** `[AllowAnonymous]`. ⚠️ Ràng buộc: chỉ hàng **đang bán** · **cấm** lộ `CostPrice` + tồn chi tiết · allowlist **phải có test**. 📌 `S8` làm + 2 ca E2E. 📄 [`web-002`](features/web/web-002-storefront-duyet-chi-tiet-san-pham.md) · [`cat-003`](features/catalog/cat-003-media-seo-slug-hien-thi-theo-kenh.md) (sitemap dùng **slug**) |
| Q7 | **Nơi đặt repo từ xa** | chủ dự án | ✅ **`GitHub`** (17/10/2026) | 🔒 **Chốt:** **GitHub Actions** · runner **`ubuntu-latest`** (vì integration test ghim `postgres:17-alpine` = Linux) + vá **P-01…P-06**. ✅ **ĐÃ LÀM 17/10/2026:** `origin` = `https://github.com/hungnt9801/eCommerce`, đã push **273 commit** (`refs/heads/main` = `ab9a7ff`). ⚠️ Repo đang **PUBLIC**. 📄 [`XC-007`](features/cross-cutting/XC-007-ci-cd-pipeline.md) |
---

## 4. CHIA PHIÊN — 11 phiên

**Luật của cách chia này** (mỗi luật ứng với một lần đã trả giá):

1. 🔒 **Mỗi phiên kết thúc bằng `kiem-tat-ca.ps1 -DayDu` XANH.** Không mang cổng đỏ sang phiên sau.
2. 🔒 **Migration + cập nhật con số unique index (48 → 49 → 51) trong CÙNG phiên** — `PostgreSqlSchemaTests` khoá con số này; quên là phiên sau mở màn bằng đỏ.
3. 🔒 **Tài liệu trước, code sau** (quy tắc vàng): lệch ⇒ sửa tài liệu **trong cùng PR**.
4. 🔒 **Mỗi phiên đo lại baseline trước khi sửa** — số trong tài liệu **trôi** (xem §2).

| Phiên | Tên | Phạm vi (làm gì) | Cổng ra (Definition of Done) | Chặn bởi |
|---|---|---|---|---|
| **S0** | **Baseline + chốt quyết định** ✅ **XONG 17/10/2026** | ⛔ **0 dòng code.** ✅ Đã chạy `-DayDu` **exit 0** và ghi số thật vào [`SESSION-KICKOFF.md`](SESSION-KICKOFF.md) §3: **228.9s** · build 73.8s · **1181** unit · **38** integration · **213** mermaid/0 · **21/21** DB (`NND=48`, 53 quyền) · **22** ca cổng · **59/59** E2E. ✅ Đã chốt **Q1** (VNPay) và **Q2** (`ADDR-004`) | ✅ Có baseline trong `SESSION-KICKOFF.md` §3 | — |
| **S1** | ✅ **XONG 18/10/2026** — **`ADDR-004` phần A: entity `Address`** | `Address` + `DiaChiCanonical` + `AddressModels` · 7 CHECK + 3 index · **`DbSet`** · migration `20261018023611_Addr004_SoDiaChi_Address` + **SQL thô `NULLS NOT DISTINCT`** · **48 → 49** · `schema.sql` regenerate · 8 ràng buộc khai báo ở `RangBuocChuaAnhXa.txt` | ✅ **24 unit + 3 integration mới** · `-DayDu` **exit 0 · 198.7s** · NND **49 = 49** · 📌 phát hiện **F11** (tên `Address` va chạm khi namespace đuôi `.Address`) | ✅ `Q2` đã chốt |
| **S2** | ✅ **XONG 18/10/2026** — **`ADDR-004` phần B: sổ địa chỉ của khách** | ✅ Đã làm đủ **B1…B10** (B11 hoãn đúng hạn — GĐ2): project `Address.Application.Contracts` (**ADR-08**) · `CustomerAddressLink` (`CusAddressLinks`) · **2 index `NULLS NOT DISTINCT` SQL thô** (**49 → 51**) · partial unique *"một mặc định / (khách, loại)"* + đổi mặc định **bỏ-trước-đặt-sau trong MỘT transaction** dưới khoá `CustomersConsts.LockKeys.TheoKhach` · `IAddressDirectory` + `DiaChiSnapshotDto` (`ON CONFLICT DO NOTHING`) · 2 app service `[Authorize]` cấp class · 4 permission khai **hai nơi** · 2 ETO (không PII) · trần **20** = **cấu hình** · xuất PII gồm sổ (cả mục đã gỡ) | ✅ **unit 1219** (+14) · **integration 46** (+5) · **probe HTTP 17/17** (2 luồng cùng đặt mặc định ⇒ **cuối cùng đúng MỘT**) · `-DayDu` **exit 0** · NND **51 = 51** · `BatBienPhanQuyenTests` xanh · 📌 **4 phát hiện mới** ghi ở `ADDR-004` §16.5 (route tài liệu ≠ thật · cache quyền âm · ABP `403` cho mọi `BusinessException` · bug thứ tự UPDATE của `go-dia-chi`) | S1 |
| **S3** | ✅ **XONG 18/10/2026** — **`CUS-001` ĐÓNG LẠI** | ✅ Đã làm: 🚨 **nối `ChoPhepGiaoDichAsync` vào đường bán hàng** (**LỖI THẬT**: khách `Blocked` vẫn đặt được) — cổng gom về `CuaKiemKhachGiaoDich` (**một** định nghĩa, **hai** chỗ gọi: checkout + tạo đơn), **NÉM** `0005` **TRƯỚC** mọi tác dụng phụ · `CustomerGroup` API (**3 method**, `SoKhach`, `Code` bất biến) + **seed** 3 nhóm (*idempotent theo mã*) + quyền `Customers.ManageGroups` (**hai nơi**) · **phân trang** `PagedResultDto` (đếm **trước** khi cắt trang) + `/KhachHang?trang=N` · **đường web mang khách vào đơn** (`KhachId` — trước đó đơn web **không thuộc về ai**) | ✅ **unit 1228** (+9) · **integration 46** · E2E **67/67** (+8) · **probe HTTP mới 26/26** (bước ⑩) · DB **23/23** (bất biến `CusGroups` **mạnh hơn**: ① → ①b) · `-DayDu` **exit 0** · 📌 **5 phát hiện mới** ở `CUS-001` §28.5 (DLL cũ do **phép tiêm** để lại · kiểm DLL bằng **chuỗi** không phân biệt được hai bản · `IndexOf` khớp **chuỗi con** · `DungDuoc` **không phải cột** · bất biến *"`CusGroups` phải rỗng"* **sai**) | S2 |
| **S4** | ✅ **XONG 18/10/2026** — **`PAY-001` phần A: đường TẠO + cầu nối** | ✅ Đã làm: **`TaoGiaoDichAsync`** (đường vào **duy nhất** của `PayTransactions`; khoá `payment:create:{…}`; **dùng lại** giao dịch đang mở của đơn ⇒ **cùng** `MerchantReference`) · **mã ỔN ĐỊNH** `SinhMaThamChieu` (`PAY{yyyyMMdd GMT+7}-{sốĐơn}-{lầnThử}`, `Goc()` để đếm lần thử, bỏ dấu tiếng Việt, chặn `> 64`) · **cầu nối `Order.PaymentStatus`** qua **ETO** (`PaymentSucceededEto`/`PaymentFailedEto` → `OrderPaymentStatusHandler` → `Order.DatTrangThaiThanhToan` **hấp thụ**) · **ETO in-process** (⚠️ **outbox bền vững = NỢ**, chủ dự án chốt hoãn) · 💯 **đóng 3 phát hiện**: `F-PAY-2` (đối chiếu số tiền), `F-PAY-3` (mã rỗng), `F-PAY-9` (`MarkFailed` → `bool`) + guard constructor | Gate: **`-DayDu` exit 0** · unit **1228 → 1294** (+66) · integration **46 → 51** (+5) · **`NND` 51 không đổi** (không migration) · 📌 **4 phát hiện mới** ghi ở `PAY-001` **§16.5** (xoá ký tự ≠ bỏ dấu ⇒ `ĐƠN-001`→`N-001` · `MarkFailed` trả `true` khi *"ghi lại cùng giá trị"* · `IRepository<T,TKey>` **không** có `GetCountAsync(predicate)` ⇒ `CS1660` · mã `eCommerce:Payment:0001` **đã khai mà chưa từng dùng**) | S0 |
| **S5** | ✅ **XONG 18/10/2026** — **`PAY-001` phần B: IPN/webhook + hoàn tiền** | ✅ Đã làm: **endpoint `GET /api/webhooks/vnpay`** (`[AllowAnonymous]` + allowlist, ở `HttpApi.Host` — trả lời `Q2`) · 🚨 **vá `F-PAY-1`** và phát hiện **lớp thứ hai** của nó (**byte chuỗi ký**: decode rồi encode lại ⇒ `+` thành `%20`) ⇒ **một** luật duy nhất `KiemChuKyTuQueryTho` · 🚨 **fail-closed khi `HashSecret` rỗng** (lỗ hổng chỉ thành hiện thực khi có endpoint) · **bảng `PayRefunds`** (dòng hoàn tiền) + **3 `CHECK` bất biến TIỀN** + unique `NULLS NOT DISTINCT` (**NND 51 → 52**) · **`PaymentRefundedEto`** + handler (đơn ⇒ `PartiallyRefunded`/`Refunded`, luật thuần `PhanLoaiHoan`) · 🆕 **vá `F-PAY-7`** (**mỗi giao dịch một UoW** + đếm `SoLoi`; `Q10` đã chốt — ⚠️ **code đã sửa nhưng CHƯA có phép đo**: không test nào chạy lô với giao dịch cố ý hỏng, ghi ở `PAY-001` §16.9 #10) · **đóng nốt `F-PAY-9`** (job đọc giá trị `MarkFailed`) · **job đối soát phát sự kiện** (hợp đồng `IPaymentEventPublisher` ở `Contracts` — luật phân tầng #8 cấm dùng lớp ở `PaymentHub.Application`) · **`F-PAY-4` một phần** (ánh xạ 6 ràng buộc + xoá 2 dòng danh sách trắng đã sai lý do) · ➕ **cổng ⑪ mới**: `tools/probe-thanh-toan.ps1` (**26 ca HTTP thật**, tự ký **HMAC-SHA512** bằng biến môi trường — **không** commit bí mật) | Gate: **`-DayDu` exit 0** · unit **1294 → 1347** (+53) · integration **51 → 57** (+6) · **NND 52 = 52** · migration **39** · `schema.sql` regenerate ỔN ĐỊNH (diff **chỉ** thêm) · **IPN trùng ⇒ 1 lần ghi + bộ đếm 3** · **sai chữ ký ⇒ 400 + DB không đổi** · **hoàn vượt ⇒ `0002`** · 📌 **5 phát hiện mới** ở `PAY-001` **§16.6** — nặng nhất: **`IXuLyKetQuaCong` KHÔNG resolve được từ DI** ⇒ IPN trả **`500`** (ABP đăng ký theo **tên lớp**, không theo hợp đồng) | S4 |
| **S6** | ✅ **XONG 18/10/2026** — **`SAL-005` phần C: nhật ký sự kiện `D7` + connector trả `update_time` + ĐO RUNTIME** | ✅ Đã làm: **`SalMarketplaceEventLogs`** (append-only, **7 kết cục**) + **`MarketplaceEventLogWriter`** (**UoW riêng** ⇒ dấu vết không bị rollback theo; nhận biết **trùng** bằng **đọc lại DB**, không đoán theo chuỗi lỗi) + handler ghi **ĐÚNG MỘT dòng ở MỌI nhánh** + 🚨 **ghi watermark ngay lần nhận đầu** (lỗ hổng: trước đó `null` ⇒ bản **cũ hơn** đến sau vẫn **ghi đè**) + migration `20261018082801` (**NND 52→53**, 3 index, 2 `CHECK`) + **connector PoC trả `UpdatedAtUtc` tất định** ⇒ `AggregateVersion ≠ null` + **`MarketplacePayloadHash`** (hash **nội dung nghiệp vụ**, không có trường vận chuyển) + ➕ **bước ⑫ mới**: `tools\probe-don-san.ps1` (**49 ca**, **đường chạy thật + DB thật**) · ➕ **cổng vận hành hạ cờ** (📄 `SAL-005` **§21**): `MarketplaceIntakeAppService` + **2 permission** (`View`/`Reprocess`) + mã `0005` + **+4 unit test reflection** + **2 ca `W`** cho `test-cong-kiem.ps1` | Gate: **`-DayDu` exit 0 · 290.8s** · unit **1347 → 1367** (+20) · integration **57 → 67** (+10) · **NND 53 = 53** · bất biến DB **24 → 25** · quét GUID **85 → 86** · migration **40** · **quyền 58 → 61** · **ca test cổng 22 → 24** · **⑫ 49/49** (event lỗi ⇒ DLQ + payload nguyên vẹn · watermark ghi từ `update_time` · **gửi lại KHÔNG DLQ giả** · job `D12` có neo **mở đầu + kết thúc**) · 📌 **2 lỗi thật** ở `sal-005` **§20.5** — nặng nhất: **hash chống trùng băm cả `CorrelationId`** ⇒ **DLQ giả ở MỌI lần kéo lại** (1358 test cũ **không** thấy) + **4 bẫy của phép đo** ở **§20.6/§21.3** (`-SimpleMatch` coi `\` là ký tự thật ⇒ **6 ca ĐỎ GIẢ** · route thật `/channel/{id}/sync-now` · dọn probe phải xoá `MsgIdempotencyRecords` · **thêm permission vào provider KHÔNG tự cấp cho admin** ⇒ `403` dù code đúng, phải chạy `DbMigrator`) | S0 |
| **S7** | ✅ **XONG 18/10/2026** — **UI/API còn thiếu của Inventory + Reconciliation** | ✅ Đã làm: `/HanMucKenh` **lọc theo KHO** (kèm lựa chọn *"(không gắn kho)"* = `Guid.Empty`) + **phân trang** (đếm **trước** khi cắt trang) + **ô chọn kho** khi đặt hạn mức + **đổi nhãn theo `Q4`** (*"trần an toàn của kênh theo từng kho"*) · 🚨 **xác nhận lệch = ĐÓNG CẢNH BÁO + audit** (`Q5` = A): **3 cột** + `ReconRun.XacNhan` + **`CHECK`** *"không xác nhận một nửa"* + permission **`eCommerce.Reconciliation.Confirm`** + endpoint + **23 ca E2E** · **RETENTION**: setting `Recon.Retention.Days` (180, `0` = TẮT) + luật **THUẦN** + service **dùng chung** cho job `ReconRetentionWorker` và nút bấm (có **XEM TRƯỚC**, **xoá CỨNG**, không bao giờ xoá `ReconRuns`/dòng chưa xử lý/dòng đang chờ xác nhận) · **phân trang** trang chi tiết (nợ §18.7 #2) | Gate: **`-DayDu` exit 0** · unit **1367 → 1395** (+28) · integration **67 → 71** (+4) · **NND 53 = 53** · migration **41** · **quyền 61 → 62** · **E2E 67 → 95 ca** · 📌 **3 lỗi thật** chỉ lộ khi đo HTML/DB: **xoá MỀM** thay vì cứng (retention **không** làm bảng nhỏ đi) · **`[Required]` trên DTO** chặn trước domain ⇒ người dùng thấy **tiếng Anh** thay vì `0005` · **`[Range]` trên `SoNgay`** làm `0` **không bao giờ** trả `0006` — 📌 cả ba đều **không** làm đỏ cổng nào trước đó | ✅ `Q4`/`Q5` đã chốt — chỉ còn việc của phiên |
| **S8** | **Thanh toán trên UI + đóng `WEB-002`** | `/DatHang` → tạo giao dịch → chuyển cổng → trang kết quả · chi tiết sản phẩm + SEO còn lại | E2E end-to-end tới trang kết quả | `S5` · ✅ `Q6` đã chốt (mở công khai) |
| **S9** | **`XC-007` — CI thật** | `.github/workflows` gọi `-DayDu` · **vá khả chuyển Linux P-01…P-06** (6 điểm Windows-only) · **build làm nóng** để vá **F2** (`-GhiNho` trả `exit 2` trên runner sạch ⇒ **CI đỏ ngày đầu**) · thêm cổng ⑨ `kiem-link-tai-lieu.ps1` · `commit package-lock.json` (**F4**) | Pipeline xanh **trên Linux** · `test-cong-kiem.ps1` xanh trên Linux | ✅ repo + remote **đã sẵn** (17/10/2026) — 📌 chỉ còn thiếu workflow |
| **S10** | **Đóng GĐ1** | Nợ nhỏ: CSV `;` · **5/12 metric** chưa có nơi phát · `CHN-002` `Timer.RunOnStart` · xác nhận outbox/relay · **chạy `-DayDu` cuối** + cập nhật bàn giao (⚠️ ghi đúng "còn gì", **không nói quá**) | `-DayDu` xanh với **số đo mới** · tệp bàn giao GĐ1→GĐ2 | S1–S9 |

### 4.1 Thứ tự & phụ thuộc

```
S0 (quyết định) ──┬─► S1 ► S2 ► S3            ← nợ CHẶN #1, mở khoá PII
                  ├─► S4 ► S5 ─────► S8        ← dòng tiền
                  ├─► S6                       ← không mất event
                  ├─► S7 ✅ (18/10/2026)        ← UI/API còn thiếu
                  └─► S9 (cần Q7)
                                     S1..S9 ──► S10
```

📌 **Có thể chạy song song** `S6` ‖ `S1` ‖ `S4` (khác tệp, khác bảng) — nhưng **KHÔNG** song song `S1`‖`S2`
(cùng bảng `Addr*`) và **KHÔNG** song song hai phiên nào **cùng sửa một migration** (migration chồng nhau ⇒
`PostgreSqlSchemaTests` + `schema.sql` đỏ, và **rất khó** biết phiên nào gây ra).

---

## 5. Hai việc PHẢI làm trong MỌI phiên (đừng để phiên sau gánh)

| # | Việc | Vì sao |
|---|---|---|
| 1 | **Đo lại baseline trước khi sửa** (`-DayDu`, ghi số) | 🔑 Phiên trước **nói quá "GĐ1 hoàn thành"** đúng vì đã **không** đo lại; và §2 ở trên chứng minh **6 dòng "CÒN" đã lỗi thời** |
| 2 | **Cập nhật tài liệu feature TRONG CÙNG phiên** (§16 "đã xong / còn lại") | Quy tắc vàng `design-doc-guidelines.md` §4: lệch ⇒ sửa tài liệu **trước**, cùng PR |

➕ **Khi một phiên bị chặn bởi Q1–Q7:** ghi vào **§15.1 câu hỏi mở** của tài liệu feature và **đóng phiên**
với gate xanh. 🚨 **Không** code tiếp bằng một giả định chưa chốt — đó chính là cách sinh ra "hai nguồn sự thật".

---

## 6. GĐ2 — chuỗi phiên RIÊNG (chưa lên lịch ở đây)

| Nhóm | Tài liệu | Code hiện tại | Gợi ý chia |
|---|---|---|---|
| **Vận chuyển** | `SHP-001`…`SHP-004` | **0** | `SHP-001` (adapter GHN) → `SHP-002` (webhook) → `SHP-003` (COD) → `SHP-004` (GHTK) — **4 phiên**, mỗi phiên 1 adapter |
| **Khuyến mãi** | `PRO-001`, `PRO-002` | **0** | `PRO-001` (voucher) → `PRO-002` (engine cây AND/OR + **guard bùng nổ tổ hợp**) — **2–3 phiên**; 📌 `PRO-002` **phức tạp nhất**, làm **sau** `PRO-001` |
| **Điều kiện vào GĐ2** | — | — | 🔒 Tài liệu `SHP-*`/`PRO-*` phải **Approved** (`implementation-plan` §A.5); ⚠️ hiện **0/55 đã duyệt** |

📌 **Trước khi mở GĐ2**, hai câu hỏi NCC vẫn treo và **chặn thiết kế**: GHTK **xác minh địa chỉ 2 cấp**
(`SHP-004`, fail-closed) và GHN **đổi COD cần OTP** (`SHP-003`, phải có bước thủ công).

---

## 7. Lịch sử

| Ngày | Thay đổi |
|---|---|
| 17/10/2026 | Bản đầu. Đo lại toàn bộ GĐ1 bằng `grep`/`Test-Path`: **6 dòng "CÒN" trong `implementation-plan.md` đã lỗi thời** (CAT-003 media, SAL-005 watermark, WEB-001 menu, REC-001 querydr, CUS-001 PII, SAL-006 webhook/job đã xong). Chia **11 phiên**; 6 quyết định chặn ở `S0` |
| 17/10/2026 (sau) | ✅ **`S0` XONG**: `-DayDu` **exit 0 · 228.9s** — baseline ghi ở [`SESSION-KICKOFF.md`](SESSION-KICKOFF.md) §3. ✅ Chốt **`Q1`** (VNPay: tự đăng ký merchant test + `FakePaymentProvider`) và **`Q2`** (`ADDR-004` 🟢 Approved, 10/10 câu hỏi). ✅ Dọn nốt 3 nợ tài liệu của `ADDR-004` §15.3: **#9** (schema `addr-001`), **#11** (bảng mã lỗi) và **#12** (permission) của `cus-001`. ⏳ Còn **4 quyết định** (`Q3`…`Q6`) + **`Q7`** (repo từ xa) |
| 18/10/2026 | ✅ **`S1` + `S2` + `S3` XONG** (xem cột *Cổng ra* của từng phiên). 🔑 **`S3` đóng `CUS-001`** và sửa một **LỖI THẬT**: `ChoPhepGiaoDichAsync` **định nghĩa 1 nơi, gọi 0 nơi** ⇒ **khách `Blocked` vẫn đặt được hàng** — nay **chứng minh bằng HTTP thật** (2 chiều + **0 tác dụng phụ**). ➕ Cổng **⑩** mới (`probe-khach-hang.ps1`, **26 ca**) · E2E **59 → 67** · unit **1219 → 1228** · DB **23/23** với bất biến `CusGroups` **mạnh hơn** (① → ①b: *đúng 3 nhóm seed + 0 rác probe*) · quyền **57 → 58**. 📄 Chi tiết: `cus-001` **§28** |
| 18/10/2026 (sau) | ✅ **`S4` XONG** — **`PAY-001` phần A** (xem cột *Cổng ra*). 🔑 Đóng **nợ #1** của `PAY-001`: *"đơn đã trả vẫn `Unpaid`"* — nay có **cầu nối ETO** ⇒ `Order.DatTrangThaiThanhToan` (luật **hấp thụ**), setter `Order.PaymentStatus` **đã đóng**. Đóng **3 phát hiện** (`F-PAY-2/3/9`) và **2 nợ** (§15.3 #1 đường tạo giao dịch · #3 cầu nối). 🚨 **KHÔNG** đánh dấu `Implemented`: chưa có endpoint IPN (`S5`), **chưa kiểm chứng tiền thật**, **outbox chưa có**. 📌 **4 phát hiện mới** ghi ở `PAY-001` **§16.5** — cả bốn đều *"không làm đỏ cổng nào"*, chỉ **test viết cùng lúc với code** bắt được. ⇒ **`S5` là phiên kế tiếp** (IPN/HMAC + hoàn tiền + vá `F-PAY-1`, `F-PAY-7`) |
| 18/10/2026 (cuối) | ✅ **`S5` XONG** — **`PAY-001` phần B** (xem cột *Cổng ra*). 🔑 Đóng **nợ #2** (endpoint IPN), **#4** (bảng dòng hoàn tiền) và **#11** (`PaymentRefundedEto`) của `PAY-001` §15.3; đóng **`F-PAY-1`**, **`F-PAY-7`**, **`F-PAY-9`** (phần còn lại) và **`F-PAY-4`** (một phần); trả lời **`Q2`** + **`Q10`**. ➕ **Cổng ⑪ mới** (`tools\probe-thanh-toan.ps1`, **26 ca HTTP thật**: IPN trùng ⇒ 1 lần ghi · **sai chữ ký ⇒ 400** và DB không đổi · hoàn vượt ⇒ **`0002`**). 📌 **5 phát hiện mới** ở `PAY-001` **§16.6** — cả năm *"không làm đỏ cổng nào"*; nặng nhất **`S5-2`**: `IXuLyKetQuaCong` **không resolve được từ DI** (ABP đăng ký theo **tên lớp**, không theo hợp đồng) ⇒ request IPN đầu tiên trả **`500`** — 🔑 và nó **chỉ** lộ khi **gọi HTTP thật**, đúng lý do bước ⑪ phải là bước của **cổng**. ⚠️ **Vẫn KHÔNG `Implemented`**: chưa kiểm chứng **tiền thật**, outbox chưa có, **`Q6`** (ai xác nhận dòng `ChoXacNhan`) còn mở ⇒ **`S6`/`S8` là các phiên kế tiếp** |
| 18/10/2026 (sau nữa) | ✅ **`S6` XONG** — **`SAL-005` phần C** (xem cột *Cổng ra*). 🔑 Đóng **nợ §17.6 #3** (`SalMarketplaceEventLogs`), **§17.6 #7** (connector trả `update_time`) và **§18.7 #1** (*"chưa đo được runtime"*) — 📌 theo đúng cách nợ đó đã chỉ: **làm ĐƯỜNG GỌI** (bước ⑫) chứ không tìm cách đo khác. 🚨 **HAI LỖI THẬT chỉ phát hiện được vì lần đầu đo runtime:** (1) **hash chống trùng băm cả `CorrelationId`** (đổi mỗi lần kéo) ⇒ **DLQ giả ở MỌI lần kéo lại** + cảnh báo mất tác dụng — **1358 test cũ KHÔNG thấy** vì cần **hai** lần gửi với `CorrelationId` khác nhau; 📌 **đo hai chiều**: tiêm lại mã cũ ⇒ `C31`/`C32` **ĐỎ** (**DLQ 2 → 3**), mã đã sửa ⇒ **39/39**; (2) **dòng intake mới không ghi watermark** ⇒ bản **cũ hơn** đến sau vẫn **ghi đè trạng thái mới**. 📊 **Số đo:** unit **1347 → 1363** · integration **57 → 67** · NND **52 → 53** · migration **39 → 40** · bất biến DB **24 → 25** · quét GUID **85 → 86** · cổng **11 → 12 bước** · `-DayDu` **exit 0 · 273.2s**. 📌 **3 bẫy của phép đo** (ghi ở `SAL-005` §20.6): `-SimpleMatch` coi `\` là ký tự thật ⇒ **6 ca ĐỎ GIẢ** (suýt kết luận *"job `D12` không chạy"*) · route thật `/api/app/channel/{id}/sync-now` ⇒ `404` ngay lần đầu · dọn probe **phải** xoá `MsgIdempotencyRecords`. ⚠️ **`SAL-005` vẫn KHÔNG `Implemented`**: **`Order` nội bộ chưa cập nhật khi áp watermark** (§20.7 #1) |
| 18/10/2026 (sau `S6`) | ✅ **`S7` XONG** — **UI/API còn thiếu của Inventory + Reconciliation** (xem cột *Cổng ra* ở §4). 🔑 Đóng **hai quyết định đã chốt** nhưng chưa có code: **`Q4`** (đổi **nhãn UI** thành *"trần an toàn của kênh theo từng kho"*) và **`Q5`** (**xác nhận lệch = đóng cảnh báo + audit**, `rec-001` §19). ➕ Đóng **3 nợ**: bàn giao §7 việc 6 (*"`/HanMucKenh` chọn kho"*) · `rec-001` §18.7 #1 (**retention** — bảng **chỉ tăng**) · §18.7 #2 (**phân trang** trang chi tiết). 📊 **Số đo:** unit **1367 → 1395** · integration **67 → 71** · **NND 53 = 53** · migration **40 → 41** · **quyền 61 → 62** · **E2E 67 → 95 ca** · `-DayDu` **exit 0 · 241.8s**. 🚨 **3 lỗi thật**, cả ba **không làm đỏ cổng nào** trước đó và **chỉ lộ khi đo HTML/DB thật**: (①) **xoá MỀM** thay vì **xoá CỨNG** (`ReconRunMismatch` là `FullAuditedEntity`) ⇒ retention *"trông chạy"* nhưng **bảng vẫn phình**; (②) **`[Required]` trên DTO** chặn **TRƯỚC** guard của domain ⇒ người dùng thấy *"The GhiChu field is required."* thay vì `0005` — 📌 đúng lỗi `rec-001` §18.4 **tái xuất ở dạng khác**; (③) **`[Range]` trên `SoNgay`** ⇒ `0` **không bao giờ** trả `0006` ⇒ **tài liệu và sản phẩm lệch nhau mà cổng vẫn xanh**. 📌 **2 lỗi của chính BỘ ĐO** (`inv-003` §20.4): neo HTML giả định `7` trong khi cột `numeric` in **`7.0000`** ⇒ **3 ca ĐỎ GIẢ** ⚠️ và các ca **chiều ngược XANH vì lý do sai** (cùng một lỗi đo làm *"chặn đúng"* và *"chặn bừa"* **trông giống hệt**) · **gộp nhóm** *"mới hơn mốc"* vào *"chờ xác nhận"* ⇒ báo cáo **nói sai lý do giữ lại**. ⚠️ **Còn nợ**: metric retention · GUID người xác nhận trên UI · nút *"Sửa"* điền sẵn · test tích hợp `GiuChoAsync` |
