# KẾ HOẠCH THỰC HIỆN — Hệ thống eCommerce đa kênh

> **Trạng thái:** bản kế hoạch để review — **chưa code**.
> **Tài liệu liên quan:** `structure-and-techstack.md` (cấu trúc & công nghệ) ·
> `business-modules-proposal.md` (đề xuất module + bằng chứng khảo sát 9 nền tảng) ·
> `phase-0-execution-report.md` (xác thực đã xong) · `THIRD-PARTY-NOTICES.md` (tuân thủ giấy phép).

---

## 1. Mục tiêu & phạm vi

**Mục tiêu:** hệ thống bán hàng **đa kênh** (website + sàn TMĐT) có quản trị tập trung **tồn kho**, **giá theo kênh**,
**thanh toán Việt Nam**, **hoá đơn điện tử** và **đối soát**; chạy trên .NET 10 + ABP Modular Monolith + PostgreSQL.

**Trong phạm vi:** 16 module nghiệp vụ · UI quản trị · storefront website · tích hợp thanh toán VN · hoá đơn điện tử ·
đối tác vận chuyển VN · đối soát (thanh toán/sàn/COD).

**Ngoài phạm vi (giai đoạn này):** đa tiền tệ hoạt động thật · HSM/ký số tự động 100% · mobile app ·
marketplace nhiều nhà bán (multi-vendor) · POS · đấu sàn ngay ở GĐ1.

---

## 2. Tám quyết định đã chốt (ràng buộc, không thay đổi giữa chừng)

| # | Quyết định | Hệ quả |
|---|---|---|
| 1 | **Chỉ học thiết kế từ repo khảo sát; tự code lại** theo pattern của source mới | Không copy file `.cs` nào ⇒ `THIRD-PARTY-NOTICES.md` chỉ ghi NuGet |
| 2 | Ký số: **thủ công** | `DigitalSigningHub` có trạng thái "chờ người ký"; không mua HSM |
| 3 | Địa chỉ: **hỗ trợ cả 2 cấp mới và 3 cấp cũ** | `AddressHub` là module P0 |
| 4 | **Website trước**, sàn sau | `eCommerce.Web` lên trước; đấu sàn ở GĐ4 |
| 5 | `Money`: **port** *(xử lý: tự viết theo thiết kế OCC — xem §13 câu hỏi mở #1)* | Value object `Money` dùng chung |
| 6 | Đa tiền tệ: **không hiện tại**, có cho tương lai | `Money` có `Currency` nhưng chỉ VND hoạt động |
| 7 | **Không** port module nguyên khối EShop | Chỉ học pattern |
| 8 | UI admin: **tự viết tay trước** | Dùng module miễn phí của ABP, không mua Pro |

---

## 3. Mười lăm nguyên tắc bất di bất dịch

*(đúc kết từ lỗi thực tế đã gặp ở Phase 0 + bài học từ 9 nền tảng — chi tiết: `structure-and-techstack.md` §4)*

| # | Nguyên tắc | Nguồn |
|---|---|---|
| 1 | Tiền luôn `decimal` qua `Money`, **không `double`** | GrandNode 2 dùng `double` ở 96 trường |
| 2 | **Không so tiền bằng `==`** | EShop so `ActualTotalPrice == OriginalPaymentAmount` |
| 3 | Module sở hữu entity của mình; **module → app là cấm** | Đã gặp lỗi ở Phase 0 |
| 4 | Nền dùng chung ở **`shared/eCommerce.Kernel`** | Đã gặp lỗi `ChannelHubConsts` |
| 5 | Entity của module **phải có `DbSet`** trên DbContext hợp nhất | Lỗi Phase 0: `IRepository<T>` không đăng ký |
| 6 | **`Configure<AbpDbContextOptions>(o => o.UseNpgsql())`** | Lỗi Phase 0: mọi API chạm DB trả 500 |
| 7 | **`[Authorize]` ở cấp class** cho mọi app service | Lỗi Phase 0: API đọc dữ liệu bị công khai |
| 8 | Tồn kho = **append-only ledger**, không UPDATE số lượng | Truy vết + đối soát |
| 9 | **Idempotency ở mọi cửa vào** (bảng inbox + unique index) | Webhook là at-least-once |
| 10 | **Không tin `ReturnUrl`/callback** — chỉ IPN đã xác thực chữ ký | Đã áp dụng ở `PaymentHub` |
| 11 | **State machine phải NÉM lỗi**, không im lặng | eShop guard im lặng ⇒ event rơi không dấu vết |
| 12 | **Distributed lock** cho tài nguyên dùng chung (kho theo SKU, token sàn theo shop) | Chống race condition |
| 13 | Mọi thao tác ghi qua **Unit of Work** | Transaction tự động |
| 14 | **Gộp `[DependsOn]` thành một attribute** | ABP CLI tách nhiều attribute + thụt lề sai |
| 15 | Mọi package ABP **cùng version**, sửa **một file** `Directory.Packages.props` | Central Package Management |

---

## 4. Kiến trúc & cấu trúc (tóm tắt)

```
shared/  eCommerce.Kernel      → Money · Currency · Address · enum chung · mã lỗi · lock key
src/     8 project             → host · DbContext hợp nhất · migration · DbMigrator · eCommerce.Web (MỚI)
modules/ 16 bounded context    → mỗi module 3 project (Domain + Application + .Contracts khi cần)
test/    unit + integration
```

**Khuôn chuẩn một module:**

```
modules/<Module>/
├─ eCommerce.<Module>.Domain/                 entity · value object · domain service · mã lỗi
│                                             + <Module>DbContextModelCreatingExtensions.cs
├─ eCommerce.<Module>.Application/            DTO · app service (auto API controller)
└─ eCommerce.<Module>.Contracts/              (chỉ khi module khác cần dùng trực tiếp)
```

**Tech stack:** .NET 10.0.400 · ABP 10.7.0 (NuGet, LGPL) · PostgreSQL 17 + EF Core 10.0.11 · Redis 7 ·
ABP distributed event bus + **Outbox/Inbox có relay** · BackgroundWorkers + BackgroundJobs ·
Identity + OpenIddict · **Volo.Abp.Mapperly** (không AutoMapper) · Swashbuckle 10.2.3 ·
xUnit + Testcontainers.PostgreSql · UI MVC/Razor (SEO) + theme miễn phí.

---

## 5. Lộ trình 5 giai đoạn

```
GĐ0  PHÁP LÝ       AddressHub · EInvoiceHub · DigitalSigningHub
GĐ1  BÁN ĐƯỢC      Kernel · Web+login · Catalog · Pricing · Inventory · Sales · Customers · đối soát thanh toán
GĐ2  VẬN HÀNH VN   Promotions · ShippingHub(+COD) · Reconciliation · RMA
GĐ3  ĐIỀU HÀNH     Reporting · Notifications · AccountingHub
GĐ4  TĂNG TRƯỞNG   Loyalty/Reviews · đấu sàn (ChannelHub connectors) · SaaS hoá
```

**Vì sao GĐ0 (pháp lý) song song GĐ1:** NĐ 70/2025 hiệu lực **01/06/2025** — nghĩa vụ pháp lý **hiện tại**;
việc bỏ cấp huyện (01/07/2025) **thay đổi mô hình dữ liệu địa chỉ** ⇒ làm sai từ đầu phải migrate lại toàn bộ đơn hàng.
Hai việc này **không chặn** GĐ1 về mặt kỹ thuật, nhưng **phải xong trước khi phát hành** cho khách thật.

---

> 🚦 **Cổng tài liệu (áp cho MỌI giai đoạn):** trước khi code bất kỳ feature nào, tài liệu thiết kế của feature đó
> phải ở trạng thái **Approved** (Definition of Ready). Danh sách đầy đủ tài liệu cần viết theo từng giai đoạn:
> **Phụ lục A**. Mẫu: `templates/feature-design-template.md`. Hướng dẫn: `design-doc-guidelines.md`.

| Giai đoạn | Tài liệu phải **Approved** trước khi bắt đầu code giai đoạn đó | Số lượng | Danh sách |
|---|---|:--:|---|
| **Nền (trước GĐ1)** | `XC-001`…`XC-005` | **5** | Phụ lục A.3 |
| **GĐ0** — Pháp lý | `ADDR-001`…`003` · `EINV-001`…`003` · `SIGN-001` | **7** | Phụ lục A.3 |
| **GĐ1** — Bán được | `CAT-001`…`003` · `PRC-001`…`002` · `INV-001`…`003` · `SAL-001`…`005` · `CUS-001` · `REC-001` · `WEB-001`…`003` | **18** | Phụ lục A.3 |
| **GĐ2** — Vận hành VN | `PRO-001`…`002` · `SHP-001`…`004` · `REC-002`…`003` · `SAL-006` | **9** | Phụ lục A.3 |
| **GĐ3** — Điều hành | `REP-001`…`003` · `NOT-001`…`002` · `ACC-001` | **6** | Phụ lục A.3 |
| **GĐ4** — Tăng trưởng | `CHN-001`…`004` · `LOY-001` · `REV-001` | **6** | Phụ lục A.3 |
| | | **51** | |

> **Cách áp dụng thực tế:** không cần Approved cả 18 tài liệu GĐ1 trong một ngày. Nguyên tắc là
> **mỗi feature phải Approved trước khi code feature đó** — nên có thể duyệt theo từng đợt nhỏ,
> miễn là **5 tài liệu nền (`XC-*`) xong trước tất cả** (mọi module đều phụ thuộc chúng).

## 6. GIAI ĐOẠN 0 — Pháp lý (AddressHub · EInvoiceHub · DigitalSigningHub)

### 6.1 AddressHub 🔴

| Hạng mục | Nội dung |
|---|---|
| **Entity** | `Province` (2 cấp mới), `Ward`, `District` (chỉ để tương thích 3 cấp cũ), `AddressLevelPolicy`, `CarrierAddressCode` (mã địa chỉ theo từng hãng) |
| **API** | tra cứu tỉnh/xã (2 cấp) · tra cứu 3 cấp cũ · map địa chỉ nội bộ ↔ mã của hãng vận chuyển |
| **Quy tắc** | `Address` hỗ trợ **cả hai cấu trúc**; "địa chỉ gửi đối tác" là **dữ liệu đồng bộ**, không phải master data |
| **Nguồn dữ liệu** | `provinces.open-api.vn` (v1 = 63 tỉnh có huyện; v2 = 34 tỉnh không huyện) — ⚠️ `wards` trả rỗng khi test, **phải kiểm tra lại**; hoặc import từ file chính thức |
| **Phụ thuộc** | không |
| **DoD** | (1) import được cả 2 bộ dữ liệu; (2) API tra cứu có test; (3) convert 3 cấp ↔ 2 cấp có test với ca biên (tỉnh sáp nhập); (4) seed chạy lại được (idempotent) |

### 6.2 EInvoiceHub + DigitalSigningHub 🔴

| Hạng mục | Nội dung |
|---|---|
| **Entity** | `EInvoice` (số/ký hiệu/mẫu số, MST, trạng thái), `EInvoiceLine`, `EInvoiceProvider` (cấu hình), `SigningRequest` (**state machine**), `SigningCertificate` |
| **Trạng thái hoá đơn** | `Draft` → `PendingSign` → `Signed` → `Sent` → `Accepted` / `Rejected` → `Replaced` / `Adjusted` / `Cancelled` |
| **Trạng thái ký** | `PendingHash` → `HashIssued` → `PendingSignature` → `SignatureReceived` → `Completed` / `Failed` |
| **API** | phát hành · gửi cơ quan thuế · tra cứu · điều chỉnh/thay thế · huỷ · tải PDF/XML |
| **Nhà cung cấp** | abstraction `IEInvoiceProvider` + `IDigitalSigner`; triển khai đầu tiên: **1 NCC** (chọn theo hợp đồng — VNPT có tài liệu đầy đủ nhất: SOAP/ASMX + XML, trả về **chuỗi** `"OK:…"`/`"ERR:n"`, **không có HTTP status semantic**) |
| **Quyết định 2** | Ký **thủ công**: UI có màn hình "chờ ký" + nút nạp file hash đã ký. **Không** tự động hoá 100% |
| **Phụ thuộc** | `Sales` (nguồn dữ liệu đơn) |
| **DoD** | (1) phát hành 1 hoá đơn thành công trên môi trường sandbox của NCC; (2) state machine có test cho mọi nhánh, kể cả `Rejected`; (3) lưu raw request/response để đối soát; (4) không hard-code endpoint của NCC nào |

**Cổng kiểm soát GĐ0:** 📄 tài liệu `ADDR-001…003`, `EINV-001…003`, `SIGN-001` đã **Approved** ·
3 module trên chạy được ở môi trường sandbox với **1 NCC hoá đơn + 1 hãng vận chuyển**.

---

## 7. GIAI ĐOẠN 1 — Bán được trên website

### 7.0 Hạ tầng nền (làm trước, 1 tuần)

> 📄 **Bắt buộc trước tiên:** 5 tài liệu nền **`XC-001`…`XC-005`** phải **Approved** (Phụ lục A.3) —
> vì **mọi module GĐ1 trở đi đều phụ thuộc chúng** (Money/làm tròn, outbox/inbox, lock, phân quyền, observability).
> Sau đó mới tạo code hạ tầng dưới đây.

| # | Việc | DoD |
|---|---|---|
| 0 | ✍️ Viết & duyệt **`XC-001`…`XC-005`** (tài liệu, không phải code) | 5 tài liệu ở trạng thái **Approved** |
| 1 | Tạo `shared/eCommerce.Kernel` (Money, Currency, Address, enum, mã lỗi, lock key) | Build sạch; mọi module tham chiếu được; có unit test cho `Money` (cộng/trừ khác tiền tệ ⇒ ném lỗi; VND dùng điểm cố định 0 chữ số thập phân) |
| 2 | Tạo `eCommerce.Web` + `Volo.Account` + theme miễn phí | Có màn hình login; vào được trang chủ admin |
| 3 | Thêm **Outbox/Inbox** của ABP + **DLQ** | Có test chứng minh event không mất khi giả lập publish lỗi |
| 4 | Tạo `test/eCommerce.IntegrationTests` + Testcontainers PostgreSQL | 1 test mẫu chạy được trong CI |
| 5 | `docs/coding-conventions.md` + `.editorconfig` | Nhóm thống nhất trước khi viết module đầu |

### 7.1 Các module GĐ1

| Module | Entity chính | API chính | Sự kiện phát | Phụ thuộc | DoD |
|---|---|---|---|---|---|
| **Catalog** | `Product`, `ProductVariant`(SKU), `Category`(cây), `Brand`, `ProductAttribute`/`Option`/`Mapping`, `ProductMedia`, `ProductChannelVisibility` | CRUD sản phẩm/biến thể/danh mục; tra cứu theo slug; publish/unpublish | `ProductPublishedEto`, `VariantPriceChangedEto` | Kernel | Không có `Product` phình to (tách `ProductShippingProfile`); SKU unique theo tenant; có test bất biến |
| **Pricing** | `PriceList`, `PriceListItem`, `ChannelPrice`, `TierPrice`, `CostPrice` | tính giá theo (SKU, kênh, số lượng, thời điểm); CRUD bảng giá | `PriceCalculatedEto` | Catalog, Kernel | **Pipeline tính giá có thứ tự tường minh** (chạy hết rồi mới so sánh); có test cho thứ tự ưu tiên + ghi đè |
| **Inventory** | `Warehouse`, `StockItem`, `StockLedgerEntry`(đã có), **`StockReservation`**, `StockAllotment` | nhập/chuyển/kiểm kê; tra tồn khả dụng **theo kênh**; giữ chỗ/nhả chỗ | `StockChangedEto`, `StockReservedEto`, `StockReservationExpiredEto` | Catalog, Kernel | **Test chống oversell 2 luồng đồng thời** (integration, PostgreSQL thật + lock); ledger append-only không có UPDATE; reservation có TTL + job nhả |
| **Sales** | `Cart`, `CartItem`, `CheckoutSession`, `Order`, `OrderLine`, `OrderStatusHistory`, `OrderReturn` | giỏ hàng; checkout; đặt hàng; huỷ; trả hàng; lịch sử trạng thái | `OrderPlacedEto`, `OrderPaidEto`, `OrderCancelledEto`, `OrderReturnedEto` | Catalog, Pricing, Inventory, Customers, PaymentHub | **State machine 4 trục** (thực hiện/tài chính/đối soát/phụ) + **ném lỗi** khi chuyển sai; tính lại giá tại checkout; idempotent theo `ExternalOrderId`; test: webhook gửi trùng không tạo 2 đơn |
| **Customers** | `Customer`, `Address`(sổ địa chỉ), `CustomerGroup` | hồ sơ; sổ địa chỉ (dùng AddressHub); nhóm khách | `CustomerRegisteredEto` | Kernel, AddressHub, Identity | Không có mô hình địa chỉ hai nguồn sự thật; customer gắn `IdentityUser` |
| **đối soát thanh toán** (trong `Reconciliation`) | `PaymentReconciliationRun`, `PaymentReconciliationLine`, `Discrepancy` | job định kỳ `querydr` với cổng; báo cáo lệch | `PaymentDiscrepancyFoundEto` | PaymentHub | Có job chạy định kỳ; phát hiện được ca "đã trừ tiền mà đơn chưa `Paid`" |

**Cổng kiểm soát GĐ1 (Definition of Done cấp giai đoạn):**
0. 📄 **Tài liệu GĐ1 đã Approved** (`CAT-*`, `PRC-*`, `INV-*`, `SAL-001…005`, `CUS-001`, `REC-001`, `WEB-*`) và 5 tài liệu nền `XC-*`.
1. Một khách **đặt được đơn trên website**, thanh toán qua **1 cổng VN**, đơn chuyển đúng trạng thái.
2. Bán **vượt tồn kho là không thể** — có test integration chứng minh (2 request đồng thời, 2 instance).
3. **Không mất tiền**: có job đối soát phát hiện giao dịch lệch.
4. **Mọi API đều yêu cầu xác thực**, có màn hình login hoạt động.
5. **Không mất event**: outbox relay chạy, test giả lập publish lỗi vẫn phát lại được.

---

## 8. GIAI ĐOẠN 2 — Vận hành & tuân thủ VN

| Module | Entity chính | Nội dung | DoD |
|---|---|---|---|
| **Promotions** | `Voucher`, `Coupon`, `DiscountRequirement`(**cây AND/OR**), `DiscountMapping`, `FlashSale` | engine khuyến mãi tách khỏi `Sales`; điều kiện **AND/OR** + điều kiện theo **kênh**; giới hạn lượt dùng | Engine không nằm trong `Sales`; ⚠️ **có guard chống bùng nổ tổ hợp** khi duyệt cây điều kiện |
| **ShippingHub** | `Carrier`, `ShippingService`, `ShippingRate`, `Shipment`, `ShipmentTracking`, `CodRecord` | adapter **GHN** (ưu tiên — REST + sandbox + publish `llms.txt`), GHTK; tính phí; tạo vận đơn; webhook trạng thái; **COD** | ⚠️ **GHTK: xác minh địa chỉ 2 cấp TRƯỚC khi thiết kế**; webhook GHTK **không có HMAC** ⇒ dùng IP allowlist + đối soát chủ động; đổi COD ở GHN cần **OTP** ⇒ có bước thủ công |
| **Reconciliation** (mở rộng) | `SettlementBatch`, `SettlementLine`, `CodReconciliationBatch`, `CodReconciliationLine`, `Discrepancy` | **settlement sàn** (khi đấu sàn) + **đối soát COD** | ⚠️ **COD không có API** — GHN chỉ có API *đổi* COD; bảng kê là **file Excel tải thủ công** theo **"phiên"** ⇒ phải làm **luồng import file + đối soát** |
| **RMA** (trong `Sales`) | `OrderReturn`, `OrderReturnLine`, `Refund` | trả hàng, nhập lại kho, hoàn tiền (một phần/toàn bộ) | Có test: trả hàng ⇒ sinh ledger nhập kho + hoàn tiền đúng số |

**Cổng kiểm soát GĐ2:** 📄 tài liệu `PRO-*`, `SHP-*`, `REC-002…003`, `SAL-006` đã **Approved** ·
một đơn chạy hết vòng **đặt → giao → COD → đối soát** trên **1 hãng vận chuyển thật**;
một khuyến mãi phức tạp (nhiều điều kiện AND/OR) tính đúng tiền.

---

## 9. GIAI ĐOẠN 3 — Điều hành

| Module | Nội dung | DoD |
|---|---|---|
| **Reporting** | read-model riêng (bảng tổng hợp cập nhật bằng event/job); báo cáo doanh thu, lãi gộp, tồn kho luân chuyển, hiệu suất kênh | **Không** báo cáo nào query trực tiếp trên bảng giao dịch; có test số liệu khớp với dữ liệu gốc |
| **Notifications** | email / SMS / **Zalo OA**; template; hàng đợi (dùng BackgroundJobs); không gửi trùng | Có cơ chế chống gửi trùng theo (đơn, loại thông báo) |
| **AccountingHub** | xuất liệu kế toán **Excel/CSV trước** (Bravo/MISA công bố hỗ trợ), API sau | File xuất được kiểm tra bằng 1 kế toán thật; có test đối chiếu tổng |

---

## 10. GIAI ĐOẠN 4 — Tăng trưởng

| Việc | Nội dung | Ghi chú |
|---|---|---|
| **Đấu sàn (Shopee trước)** | hoàn thiện `ShopeeConnector` (phần ký đã có), webhook đơn, đẩy tồn kho/giá, **allotment theo kênh** | ⚠️ Nhớ các ràng buộc đã khảo sát: access token 4h, refresh token 30 ngày **và rotate** (lock bắt buộc), `partner_key` hết hạn 180 ngày |
| **Lazada** | connector thứ hai | Chứng minh abstraction `IChannelConnector` đúng (thêm sàn không sửa `Sales`) |
| **Loyalty / Reviews / Wishlist** | điểm thưởng, đánh giá có kiểm duyệt | |
| **SaaS hoá** | multi-tenant UI + cấp phát/migrate DB theo tenant | Cân nhắc mua `Volo.Saas` (Pro) hoặc tự viết (resolve connection string **miễn phí**, UI/auto-migrate là Pro) |

---

## 11. Cross-cutting — làm xuyên suốt, không để cuối

| # | Hạng mục | Yêu cầu |
|---|---|---|
| 1 | **Idempotency** | Bảng inbox `(TenantId, ChannelCode, ExternalOrderId, EventType, PayloadHash)` + unique index; áp cho webhook sàn, IPN cổng, job retry |
| 2 | **Outbox/Inbox + DLQ** | Dùng của ABP (**có relay worker**); có test chứng minh event không mất |
| 3 | **Distributed lock** | Mọi tài nguyên chia sẻ: `inventory:{warehouse}:{sku}`, `channel:{code}:{shop}:token-refresh`. **Bắt buộc có Redis thật** và test chạy **2 instance** |
| 4 | **Bảo mật** | `[Authorize]` cấp class; permission định nghĩa cho mọi nhóm chức năng; **không** `AddAlwaysAllowAuthorization`; production có chứng chỉ ký thật |
| 5 | **Audit** | `AuditLogging` của ABP + `OrderStatusHistory` cho mọi chuyển trạng thái |
| 6 | **Observability** | Serilog structured + OpenTelemetry (thêm ở GĐ1 cuối); log có `CorrelationId` nghiệp vụ |
| 7 | **Migration & seed** | Mọi thay đổi schema qua EF migration; seed **idempotent** (đã có mẫu); không sửa tay DB |
| 8 | **Sao lưu & phục hồi** | Kịch bản backup PostgreSQL + diễn tập restore trước khi lên production |

---

## 12. Chiến lược kiểm thử

| Loại | Công cụ | Bắt buộc cho |
|---|---|---|
| Unit test | xUnit (20 test hiện có) | Bất biến domain: tiền, tồn kho, state machine, thuật toán ký |
| Integration test | **Testcontainers.PostgreSql** | Chống oversell (2 luồng đồng thời), idempotency webhook, outbox relay, migration, seed |
| Test đa instance | 2 process + Redis thật | Distributed lock (token sàn, trừ kho) |
| Test hợp đồng với NCC ngoài | sandbox của NCC | Cổng thanh toán, hoá đơn điện tử, hãng vận chuyển |
| Kiểm thử thủ công có kịch bản | checklist | Luồng đặt hàng end-to-end, in hoá đơn, đối soát COD |

**Quy tắc:** mọi bug sửa xong **phải có test tái hiện** trước khi đóng.

---

## 13. Ước lượng & nguồn lực

**Giả định:** 1 dev .NET quen ABP (hoặc 2 dev đã qua 2–3 tuần làm quen) · ước lượng thô, sai số ±40% ·
**chưa** tính thời gian chờ NCC (hợp đồng, sandbox, token) — phần này **có thể dài hơn code**.

| Giai đoạn | Nội dung | Ước lượng (person-day) |
|---|---|---|
| Chuẩn bị | Hạ tầng nền §7.0, kernel, Web+login, conventions | **8 – 12** |
| GĐ0 | AddressHub + EInvoiceHub + DigitalSigningHub | **18 – 28** |
| GĐ1 | Catalog · Pricing · Inventory · Sales · Customers · đối soát thanh toán + UI admin tối thiểu | **55 – 90** |
| GĐ2 | Promotions · ShippingHub · Reconciliation(+COD) · RMA | **33 – 50** |
| GĐ3 | Reporting · Notifications · AccountingHub | **18 – 30** |
| GĐ4 | Đấu sàn Shopee/Lazada · Loyalty/Reviews · SaaS hoá | **40 – 65** |
| **Tổng (code)** | | **172 – 275 person-day** (≈ **9–14 tháng** cho 1 dev; **5–7 tháng** cho 2–3 dev) |
| **Tài liệu thiết kế** | 51 tài liệu feature (Phụ lục A), ~0.5–2 pd/tài liệu | **+25 – 60 person-day** (~15% tổng công) |
| **TỔNG CỘNG** | code + tài liệu | **197 – 335 person-day** (≈ **10–17 tháng** cho 1 dev) |

> **Vì sao vẫn nên làm tài liệu dù tốn ~15%?** Vì tài liệu thiết kế **chặn được loại lỗi đắt nhất**:
> 6 lỗi ở Phase 0 và các lỗi của eShop (outbox thiếu relay, guard im lặng, thiếu dedup) **đều là lỗi thiết kế** —
> phát hiện trên giấy tốn 30 phút, phát hiện sau khi có dữ liệu thật tốn hàng tuần.

> **Lưu ý quan trọng:** phần **kiểm thử + sửa lỗi tích hợp với NCC ngoài** thường chiếm **30–40%** thời gian
> của GĐ0/GĐ2 và rất khó đoán trước. Nên **cộng thêm buffer 25%**.

---

## 14. Rủi ro & biện pháp

| # | Rủi ro | Mức | Biện pháp |
|---|---|---|---|
| 1 | **Phụ thuộc nhà cung cấp VN** — không có SDK .NET chính thức nào; tài liệu thiếu; endpoint chỉ có từ nguồn thứ cấp | 🔴 cao | Viết abstraction cho **từng** nhóm; **không hard-code endpoint**; lấy tài liệu chính thức + sandbox **trước** khi code; gửi email hỏi NCC **ngay từ tuần 1** |
| 2 | **GHTK chưa xác nhận địa chỉ 2 cấp** | 🔴 cao | Hỏi GHTK trước khi chốt mô hình địa chỉ; thiết kế **hỗ trợ cả 2** (quyết định 3) |
| 3 | **COD không có API** — bảng kê Excel thủ công theo "phiên" | 🟠 vừa | Thiết kế luồng import file + đối soát; coi là quy trình vận hành, không phải API |
| 4 | **Webhook GHTK không có HMAC** | 🟠 vừa | IP allowlist + **đối soát chủ động** (không tin webhook) |
| 5 | **Oversell đa kênh** | 🔴 cao | Ledger + reservation TTL + allotment theo kênh + lock; **test 2 instance** là điều kiện nghiệm thu GĐ1 |
| 6 | **Mất event / mất tiền** | 🔴 cao | Outbox/Inbox **có relay** của ABP + DLQ + job đối soát + test giả lập lỗi |
| 7 | **ABP nâng version nhanh** (major mỗi năm, minor ~3 tháng) | 🟡 thấp–vừa | Ghim version trong `Directory.Packages.props`; nâng theo lịch; ABP hỗ trợ mỗi major **2 năm** |
| 8 | **Pháp lý** — hoá đơn điện tử, nghĩa vụ ký số, lưu trữ | 🔴 cao | GĐ0 làm trước; xác minh số năm lưu trữ (Luật Kế toán 88/2015 Điều 41); **hỏi luật sư/kế toán** |
| 9 | **Giấy phép** — module ABP là **LGPL-3.0-only** (không phải MIT như tài liệu ABP ghi) | 🟠 vừa | Dùng **NuGet package** (an toàn); **không copy source**; luật sư review trước khi bàn giao sản phẩm |
| 10 | **Nhân lực ABP ở VN khó tuyển** | 🟠 vừa | Tuyển dev .NET giỏi + **đào tạo ABP 2–4 tuần**; viết `coding-conventions.md` để rút ngắn |
| 11 | **Phạm vi phình** (đa tiền tệ, multi-vendor, POS, mobile) | 🟠 vừa | §1 đã ghi rõ **ngoài phạm vi**; mọi yêu cầu mới phải đổi kế hoạch có ý thức |

---

## 15. Việc không làm trong kế hoạch này

- ❌ Đa tiền tệ hoạt động thật (chỉ chuẩn bị `Money.Currency`)
- ❌ HSM / ký số tự động hoàn toàn
- ❌ Multi-vendor (nhiều nhà bán trên cùng sàn)
- ❌ POS / mobile app
- ❌ Copy mã nguồn từ nopCommerce / Smartstore / GrandNode (giấy phép viral)
- ❌ Copy source module ABP (LGPL ⇒ rủi ro copyleft)
- ❌ Tự viết SMS/email gateway, payment gateway, e-invoice provider — **luôn đi qua NCC**

---

## 16. Checklist trước khi bắt đầu GĐ1

- [ ] **Viết & duyệt tài liệu thiết kế GĐ0 + GĐ1** (Phụ lục A) — đây là điều kiện để bắt đầu code
- [ ] Tạo `docs/features/` và chốt mẫu `templates/feature-design-template.md` (đã có, cần nhóm review)
- [ ] **Chốt §17 câu hỏi mở #1** (Money: tự viết theo thiết kế OCC — mặc định)
- [ ] **Chốt §13 câu hỏi mở #2** (có chuẩn hoá tên `ChannelHub.Domain` → `eCommerce.ChannelHub.Domain` không)
- [ ] Gửi email **MISA / GHTK / FPT IS / M-Invoice / VNPost / J&T / Best / Ninja Van** hỏi API + địa chỉ 2 cấp
- [ ] Đăng ký tài khoản **sandbox**: 1 cổng thanh toán VN (payOS/VNPay) + **GHN** + **GHTK** + **1 NCC hoá đơn điện tử**
- [ ] **Luật sư**: review giấy phép (đặc biệt phạm vi copyleft của ABP LGPL) trước khi nhận khách hàng thật
- [ ] Bật **Docker Desktop** và chạy 4 lệnh trong `README.md` để đóng Phase 0 (xác thực) — hiện chưa chạy được E2E
- [ ] Xác nhận **môi trường production** (nơi deploy, chứng chỉ HTTPS, backup)
- [ ] Xác nhận **nhân sự** và lịch (1 dev hay 2–3 dev) để chốt tiến độ thực tế

---

## 17. Câu hỏi mở cần bạn chốt

| # | Câu hỏi | Đề xuất của tôi | Ảnh hưởng |
|---|---|---|---|
| 1 | `Money`: **tự viết theo thiết kế OCC** hay **copy file OCC**? *(quyết định 1 và 5 mâu thuẫn nhẹ)* | **Tự viết theo thiết kế** — đồng nhất với quyết định 1, và `THIRD-PARTY-NOTICES.md` chỉ còn ghi NuGet | Nếu copy file: phải thêm nghĩa vụ MIT + header attribution vào mọi file copy |
| 2 | **Chuẩn hoá tên** module hiện có (`ChannelHub.Domain` → `eCommerce.ChannelHub.Domain`)? | **Có, đổi một lần ngay bây giờ** (~30 phút) để không lệch khuôn về sau | Nếu không: chấp nhận 2 khuôn tên trong repo, ghi rõ ngoại lệ |
| 3 | **NCC hoá đơn điện tử nào** để làm trước? | **VNPT** — tài liệu đầy đủ nhất tìm được (313 trang, cập nhật theo NĐ 70) | Ảnh hưởng thiết kế `IEInvoiceProvider` đầu tiên |
| 4 | **Cổng thanh toán nào** làm đầu tiên ngoài VNPay? | **payOS** — có SDK chính thức (~111k lượt tải) | payOS dễ tích hợp nhất; MoMo/SePay phải tự viết |
| 5 | **Hãng vận chuyển nào** làm trước? | **GHN** — REST, có sandbox, có `llms.txt` cho coding agent | GHTK làm sau, cần xác minh địa chỉ 2 cấp trước |
| 6 | Có cần **UI storefront** ngay ở GĐ1 hay chỉ UI admin? | **Cả hai** ở mức tối thiểu (xem sản phẩm + đặt hàng), vì "bán được trên website" là mục tiêu GĐ1 | Nếu chỉ admin: GĐ1 rút ngắn ~1–2 tuần nhưng chưa bán được |

---

# PHỤ LỤC A — Quy trình tài liệu thiết kế feature

> Mẫu: `templates/feature-design-template.md` · Hướng dẫn & bảng đề xuất sửa đổi so với tài liệu tham khảo:
> `design-doc-guidelines.md`

## A.1 Quy trình 7 bước (bắt buộc)

```
1. Draft      → điền theo mẫu (kể cả §15 câu hỏi mở)
2. Self-check → đối chiếu Definition of Ready
3. Review     → ≥1 người khác; tập trung: state machine · đồng thời · idempotency · lỗi
4. Approved   → đổi trạng thái trong header  ← ĐIỀU KIỆN ĐỂ BẮT ĐẦU CODE
5. Code       → code theo tài liệu
6. Update     → nếu phải lệch: SỬA TÀI LIỆU TRƯỚC rồi mới code (cùng pull request)
7. Done       → tick hết DoD, ghi lịch sử thay đổi
```

**Nguyên tắc:** *nếu code phải lệch tài liệu, sửa tài liệu trước — trong cùng pull request.*

## A.2 Quy ước

| Mục | Quy ước |
|---|---|
| Mã | `<MODULE>-<3 chữ số>` (`SAL-003`, `INV-002`…) |
| Vị trí | `docs/features/<mã>-<slug>.md` |
| Trạng thái | `Draft → In review → Approved → Implementing → Implemented → Changed` |

## A.3 Danh sách tài liệu cần viết (51 tài liệu)

### Chuẩn bị (cross-cutting) — **viết TRƯỚC GĐ1**

| Mã | Tên tài liệu | Ước lượng |
|---|---|---|
| XC-001 | Money, Currency & chính sách làm tròn (nền cho mọi module) | 1 pd |
| XC-002 | Outbox / Inbox & chống trùng sự kiện (dùng của ABP, có relay) | 1.5 pd |
| XC-003 | Khoá phân tán: quy ước đặt tên & phạm vi | 0.5 pd |
| XC-004 | Xác thực, phân quyền & định nghĩa permission | 1 pd |
| XC-005 | Observability: log có cấu trúc, `CorrelationId`, metric | 1 pd |

### GĐ0 — Pháp lý (7 tài liệu)

| Mã | Tên tài liệu |
|---|---|
| ADDR-001 | Tra cứu đơn vị hành chính **2 cấp** (tỉnh + xã) và **3 cấp** cũ |
| ADDR-002 | Đồng bộ **mã địa chỉ theo từng hãng vận chuyển** (dữ liệu đồng bộ, không phải master) |
| ADDR-003 | Chuẩn hoá địa chỉ khách hàng: convert 3 cấp ↔ 2 cấp, xử lý **ca sáp nhập tỉnh** |
| EINV-001 | Phát hành hoá đơn điện tử (NĐ 123/2020 + **NĐ 70/2025**) |
| EINV-002 | Gửi cơ quan thuế & nhận kết quả (SOAP/XML, parse `OK:`/`ERR:`) |
| EINV-003 | Điều chỉnh / thay thế / **huỷ** hoá đơn |
| SIGN-001 | Quy trình **ký 2 bước thủ công** + quản lý chứng thư số |

### GĐ1 — Bán được trên website (18 tài liệu)

| Mã | Tên tài liệu | Module |
|---|---|---|
| CAT-001 | Sản phẩm & biến thể (SKU) | Catalog |
| CAT-002 | Cây danh mục & thuộc tính/tổ hợp biến thể | Catalog |
| CAT-003 | Media, SEO slug & **hiển thị theo kênh** | Catalog |
| PRC-001 | Bảng giá & **giá theo kênh** | Pricing |
| PRC-002 | Giá bậc thang (tier) & **pipeline tính giá + làm tròn** | Pricing |
| INV-001 | Nhập / chuyển / kiểm kê & **sổ cái kho (append-only)** | Inventory |
| INV-002 | **Giữ chỗ (reservation) + TTL** & job nhả kho | Inventory |
| INV-003 | **Phân bổ tồn theo kênh** & tra tồn khả dụng | Inventory |
| SAL-001 | Giỏ hàng | Sales |
| SAL-002 | Checkout & **đặt hàng** (tính lại giá + giữ kho) | Sales |
| SAL-003 | **State machine 4 trục** + lịch sử trạng thái | Sales |
| SAL-004 | Huỷ đơn & compensation | Sales |
| SAL-005 | **Webhook đơn từ sàn** (idempotent, chịu được sai thứ tự) | Sales |
| CUS-001 | Hồ sơ khách hàng & sổ địa chỉ | Customers |
| REC-001 | Đối soát thanh toán (`querydr` định kỳ) | Reconciliation |
| WEB-001 | UI admin: khung, đăng nhập, menu, phân quyền | Web |
| WEB-002 | Storefront: duyệt & chi tiết sản phẩm (SEO) | Web |
| WEB-003 | Storefront: giỏ hàng & checkout | Web |

### GĐ2 — Vận hành & tuân thủ VN (9 tài liệu)

| Mã | Tên tài liệu |
|---|---|
| PRO-001 | Voucher / Coupon (giới hạn lượt, điều kiện) |
| PRO-002 | **Cây điều kiện AND/OR** & engine khuyến mãi (+ guard bùng nổ tổ hợp) |
| SHP-001 | Adapter **GHN**: tính phí & tạo vận đơn |
| SHP-002 | Webhook trạng thái vận đơn & đối soát (⚠️ không có HMAC ở GHTK) |
| SHP-003 | **COD**: thu hộ, đổi COD (⚠️ GHN cần OTP) |
| SHP-004 | Adapter **GHTK** (⚠️ xác minh địa chỉ 2 cấp trước) |
| REC-002 | **Settlement sàn** (phí sàn, tiền phải trả, chênh lệch) |
| REC-003 | **Đối soát COD** — ⚠️ không có API, import file Excel theo "phiên" |
| SAL-006 | Trả hàng / **RMA** & hoàn tiền (một phần / toàn bộ) |

### GĐ3 — Điều hành (6 tài liệu)

| Mã | Tên tài liệu |
|---|---|
| REP-001 | Read-model & job tổng hợp (không query nặng trên DB giao dịch) |
| REP-002 | Báo cáo doanh thu & lãi gộp |
| REP-003 | Báo cáo tồn kho luân chuyển & hiệu suất kênh |
| NOT-001 | Template & hàng đợi thông báo (chống gửi trùng) |
| NOT-002 | Kênh gửi: email / SMS / **Zalo OA** |
| ACC-001 | Xuất liệu kế toán Excel/CSV |

### GĐ4 — Tăng trưởng (6 tài liệu)

| Mã | Tên tài liệu |
|---|---|
| CHN-001 | Kết nối shop qua OAuth (đã có phần nền) |
| CHN-002 | **Connector Shopee**: pull đơn, push tồn/giá |
| CHN-003 | **Connector Lazada** (chứng minh abstraction đúng) |
| CHN-004 | Allotment & đồng bộ tồn **đa kênh** |
| LOY-001 | Điểm thưởng |
| REV-001 | Đánh giá sản phẩm (có kiểm duyệt) |

> **Ghi chú:** feature nhỏ có thể **gộp tài liệu**, nhưng phải ghi rõ trong header (ví dụ `CAT-001+002`).
> Không được bỏ mục nào trong mẫu — mục không áp dụng ghi **"Không áp dụng + lý do"**.

## A.4 Ước lượng thời gian viết tài liệu

| Loại feature | Thời gian |
|---|---|
| Feature nhỏ, 1 luồng, không state machine | **0.5 ngày** |
| Feature trung bình (CRUD + nghiệp vụ + UI) | **1 ngày** |
| Feature có state machine **hoặc** tích hợp NCC | **1.5–2 ngày** |
| Feature có **cả** state machine **và** NCC **và** pháp lý (EINV-*, SHP-*) | **2–3 ngày** |

**Tổng: ~25–60 person-day** cho 51 tài liệu (đã cộng vào §13).

## A.5 Điều kiện để một giai đoạn được coi là "bắt đầu"

Một giai đoạn **chỉ được bắt đầu code** khi:
1. ✅ **Toàn bộ tài liệu của giai đoạn đó đã Approved**, hoặc
2. ✅ Có thoả thuận rõ ràng bằng văn bản cho phép làm song song (ví dụ: XC-001 dùng chung nên phải xong trước; các tài liệu còn lại có thể Approved theo từng đợt nhỏ), **nhưng** mỗi feature vẫn phải Approved **trước khi code feature đó**.

## A.6 Việc cần làm ngay khi bắt đầu

1. Nhóm **review mẫu** `templates/feature-design-template.md` — đặc biệt 4 mục mới so với tài liệu gốc: state machine · sự kiện/idempotency · bất biến & đồng thời · migration.
2. Viết **XC-001…XC-005** (5 tài liệu nền) — vì mọi module phụ thuộc chúng.
3. Viết **ADDR-001…003 + EINV-001** (GĐ0) — vì đây là nghĩa vụ pháp lý và chặn thiết kế địa chỉ/hoá đơn.
4. Song song: gửi email hỏi NCC (§16) để có thông số thật điền vào tài liệu.

## A.7 Trạng thái

| Hạng mục | Trạng thái |
|---|---|
| Mẫu tài liệu | ✅ đã viết — **chờ nhóm review** |
| Hướng dẫn + bảng sửa đổi so với tài liệu tham khảo | ✅ đã viết |
| Danh sách 51 tài liệu | ✅ đã liệt kê (A.3) |
| Tài liệu thực tế | ⬜ **chưa viết** — bắt đầu từ XC-001 |
