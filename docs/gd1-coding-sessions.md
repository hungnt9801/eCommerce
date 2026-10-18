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
| 2 | **`CustomerAddressLink` + API + quyền** | chỉ là **comment** trong `Customer.cs`, chưa có entity/bảng | LỚN — **việc của `S2`** (⇒ NND 49 → 51) |
| 3 | 🚨 **Cổng khách bị chặn KHÔNG được gọi** | `ChoPhepGiaoDichAsync` **định nghĩa 1 nơi, gọi 0 nơi** (chỉ 3 tệp: khai báo + DTO + comment) ⇒ **khách `Blocked` VẪN đặt được hàng** | NHỎ nhưng **là lỗi thật** |
| 4 | **Đường TẠO giao dịch thanh toán** | `new PaymentTransaction` chỉ có trong **test** (3 tệp, 0 tệp sản phẩm) | LỚN |
| 5 | **Endpoint IPN/webhook** | `PaymentHub` **0 controller** (`[Route(` = 0) ⇒ VNPay gọi vào **không có cửa** | LỚN |
| 6 | **Cầu nối `Order.PaymentStatus`** | `Order.cs` có trường; **không nơi nào** cập nhật từ thanh toán ⇒ *"đơn đã trả vẫn `Unpaid`"* | VỪA |
| 7 | **ETO/outbox cho thanh toán** | `IHasEventOutbox` không có trong `PaymentHub` | VỪA |
| 8 | **Sổ hoàn tiền + `CHECK`** | `RegisterRefund` có ở entity; **không** có bảng dòng hoàn tiền, **không** có `CHECK RefundedAmount <= Amount` | VỪA |
| 9 | **DLQ + `SalMarketplaceEventLogs` (D7) + job `D12`** | `grep SalMarketplaceEventLogs` = **0** | VỪA |
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
| **S2** | **`ADDR-004` phần B — sổ địa chỉ của khách** | 🆕 **B1. Tạo project `modules/Address/eCommerce.Address.Application.Contracts`** (+ thêm vào `eCommerce.slnx` + cho `eCommerce.Address.Application` tham chiếu) — theo **ADR-08**, vì `Customers` **cấm** tham chiếu `Address.Application` · **B2.** `CustomerAddressLink` (`CusAddressLinks`) + **2 unique index** (⇒ **49 → 51**) · **B3.** **partial unique** "một mặc định / `(CustomerId, AddressType)`" + đổi mặc định **trong MỘT transaction** (bỏ-trước-đặt-sau) · **B4.** `IAddressDirectory` + **DTO snapshot** ở Contracts · **B5.** API (`IAddressAppService` + `ICustomerAddressAppService`) · **B6.** permission `eCommerce.Addresses.*` khai **hai nơi** · **B7.** `AddressChangedEto` + outbox · **B8.** `Addresses:MaxPerCustomer` = **cấu hình** (mặc định 20 — `Q1`) · **B9.** bản xuất PII thêm *"giao tới đâu"* | E2E có ca *"hai luồng cùng đặt mặc định ⇒ chỉ 1 thắng"* · kiểm HTTP thật · `BatBienPhanQuyenTests` xanh · cập nhật `ADDR-004` §16 | S1 |
| **S3** | **`CUS-001` đóng lại** | 🚨 **Nối `ChoPhepGiaoDichAsync` vào đường bán hàng** (lỗi thật: khách `Blocked` vẫn đặt được) · `CustomerGroup` API + seed · phân trang `/KhachHang` · kiểm HTTP (mới đo route) | E2E **1 ca mới**: *"khách `Blocked` ⇒ KHÔNG tạo được đơn"* · gate xanh | S2 |
| **S4** | **`PAY-001` phần A — đường TẠO + cầu nối** | Tạo `PaymentTransaction` từ checkout/xác nhận đơn · **`MerchantReference` ỔN ĐỊNH** (cấm `Guid.NewGuid()` mỗi lần) · cập nhật **`Order.PaymentStatus`** khi `Succeeded/Failed` · **ETO + outbox** | Integration: *"IPN gửi trùng ⇒ 1 giao dịch"* · đơn chuyển `Paid` · gate xanh | S0 |
| **S5** | **`PAY-001` phần B — IPN/webhook + hoàn tiền** | Endpoint **IPN `[AllowAnonymous]`** + `HMAC-SHA512` · idempotency · 🚨 **vá F-PAY-1** (`VerifyWebhookAsync` không bỏ tham số rỗng ⇒ có thể **từ chối IPN THẬT**) · bảng dòng hoàn tiền + **`CHECK RefundedAmount <= Amount`** · 🚨 **vá F-PAY-7** (UoW cả lô ⇒ rollback mất bằng chứng) | E2E: IPN trùng · **sai chữ ký ⇒ 400** · hoàn vượt ⇒ **ném lỗi** · gate xanh | S4 |
| **S6** | **`SAL-005` — DLQ + D7 + D12** | Bảng **DLQ** (`XC-002`) · `SalMarketplaceEventLogs` (**D7**) · worker **`D12`** đối soát để cờ `GapSuspected` **không tồn đọng** · connector trả **`update_time`** ⇒ `AggregateVersion` ≠ `null` | Integration: *"event lỗi ⇒ vào DLQ, không mất"* · job D12 chạy · gate xanh | S0 |
| **S7** | **UI/API còn thiếu của Inventory + Reconciliation** | Màn hình **hạn mức kênh** (API đã có) · lọc theo kho · phân trang · **API/UI lịch sử lượt quét** đối soát · **retention** dòng lệch | E2E trên HTML thật · gate xanh | ✅ `Q4`/`Q5` đã chốt — chỉ còn việc của phiên |
| **S8** | **Thanh toán trên UI + đóng `WEB-002`** | `/DatHang` → tạo giao dịch → chuyển cổng → trang kết quả · chi tiết sản phẩm + SEO còn lại | E2E end-to-end tới trang kết quả | `S5` · ✅ `Q6` đã chốt (mở công khai) |
| **S9** | **`XC-007` — CI thật** | `.github/workflows` gọi `-DayDu` · **vá khả chuyển Linux P-01…P-06** (6 điểm Windows-only) · **build làm nóng** để vá **F2** (`-GhiNho` trả `exit 2` trên runner sạch ⇒ **CI đỏ ngày đầu**) · thêm cổng ⑨ `kiem-link-tai-lieu.ps1` · `commit package-lock.json` (**F4**) | Pipeline xanh **trên Linux** · `test-cong-kiem.ps1` xanh trên Linux | ✅ repo + remote **đã sẵn** (17/10/2026) — 📌 chỉ còn thiếu workflow |
| **S10** | **Đóng GĐ1** | Nợ nhỏ: CSV `;` · **5/12 metric** chưa có nơi phát · `CHN-002` `Timer.RunOnStart` · xác nhận outbox/relay · **chạy `-DayDu` cuối** + cập nhật bàn giao (⚠️ ghi đúng "còn gì", **không nói quá**) | `-DayDu` xanh với **số đo mới** · tệp bàn giao GĐ1→GĐ2 | S1–S9 |

### 4.1 Thứ tự & phụ thuộc

```
S0 (quyết định) ──┬─► S1 ► S2 ► S3            ← nợ CHẶN #1, mở khoá PII
                  ├─► S4 ► S5 ─────► S8        ← dòng tiền
                  ├─► S6                       ← không mất event
                  ├─► S7                       ← UI/API còn thiếu
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
