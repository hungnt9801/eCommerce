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

**Vì sao GĐ0 (pháp lý) song song GĐ1:** hoá đơn điện tử nay điều chỉnh bởi **NĐ 254/2026/NĐ-CP** (hiệu lực **01/7/2026**, thay thế NĐ 123/2020 + NĐ 70/2025) — nghĩa vụ pháp lý **hiện tại**;
việc bỏ cấp huyện (01/07/2025) **thay đổi mô hình dữ liệu địa chỉ** ⇒ làm sai từ đầu phải migrate lại toàn bộ đơn hàng.
Hai việc này **không chặn** GĐ1 về mặt kỹ thuật, nhưng **phải xong trước khi phát hành** cho khách thật.

---

> 🚦 **Cổng tài liệu (áp cho MỌI giai đoạn):** trước khi code bất kỳ feature nào, tài liệu thiết kế của feature đó
> phải ở trạng thái **Approved** (Definition of Ready). Danh sách đầy đủ tài liệu cần viết theo từng giai đoạn:
> **Phụ lục A**. Mẫu: `templates/feature-design-template.md`. Hướng dẫn: `design-doc-guidelines.md`.

| Giai đoạn | Tài liệu phải **Approved** trước khi bắt đầu code giai đoạn đó | Số lượng | Danh sách |
|---|---|:--:|---|
| **Nền (trước GĐ1)** | `XC-001`…`XC-007` | **7** | Phụ lục A.3 |
| **GĐ0** — Pháp lý | `ADDR-001`…`004` · `EINV-001`…`003` · `SIGN-001` | **8** | Phụ lục A.3 |
| **GĐ1** — Bán được | `CAT-001`…`003` · `PRC-001`…`002` · `INV-001`…`003` · `SAL-001`…`005` · `CUS-001` · `REC-001` · `PAY-001` · `WEB-001`…`003` | **19** | Phụ lục A.3 |
| **GĐ2** — Vận hành VN | `PRO-001`…`002` · `SHP-001`…`004` · `REC-002`…`003` · `SAL-006` | **9** | Phụ lục A.3 |
| **GĐ3** — Điều hành | `REP-001`…`003` · `NOT-001`…`002` · `ACC-001` | **6** | Phụ lục A.3 |
| **GĐ4** — Tăng trưởng | `CHN-001`…`004` · `LOY-001` · `REV-001` | **6** | Phụ lục A.3 |
| | | **55** | |

> **Cách áp dụng thực tế:** không cần Approved cả 18 tài liệu GĐ1 trong một ngày. Nguyên tắc là
> **mỗi feature phải Approved trước khi code feature đó** — nên có thể duyệt theo từng đợt nhỏ,
> miễn là **7 tài liệu nền (`XC-001`…`XC-007`) xong trước tất cả** (mọi module đều phụ thuộc chúng).

## 6. GIAI ĐOẠN 0 — Pháp lý (AddressHub · EInvoiceHub · DigitalSigningHub)

### 6.1 AddressHub 🔴

| Hạng mục | Nội dung |
|---|---|
| **Entity** | **`AdministrativeUnit`** (**MỘT** bảng, phân cấp bằng `Level` — thay cho `Province`/`Ward`/`District`) · **`CarrierAddressMapping`** (thay `CarrierAddressCode`) · 🆕 **`Address`** (sổ địa chỉ dùng chung — 🚨 **thuộc AddressHub, KHÔNG thuộc Customers**; [`ADDR-004`](features/address/addr-004-so-dia-chi-khach.md) 🟢 **Approved 17/10/2026**) · 📌 `AddressLevelPolicy` **không có trong code** — chính sách cấp hành chính nằm ở cấu hình (chốt tại `ADDR-004` **ADR-12**: theo hiện trạng code) |
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

> 📄 **Bắt buộc trước tiên:** 7 tài liệu nền **`XC-001`…`XC-007`** phải **Approved** (Phụ lục A.3) —
> vì **mọi module GĐ1 trở đi đều phụ thuộc chúng** (Money/làm tròn, outbox/inbox, lock, phân quyền, observability, CI).
> Sau đó mới tạo code hạ tầng dưới đây.

| # | Việc | DoD |
|---|---|---|
| 0 | ✍️ Viết & duyệt **`XC-001`…`XC-006`** (tài liệu, không phải code) | 6 tài liệu ở trạng thái **Approved** |
| 1 | Tạo `shared/eCommerce.Kernel` (Money, Currency, enum, mã lỗi, lock key — 🚨 **KHÔNG** có `Address`: `Address` là **entity** thuộc module `Address`, xem `ADDR-004` **ADR-02**) | Build sạch; mọi module tham chiếu được; có unit test cho `Money` (cộng/trừ khác tiền tệ ⇒ ném lỗi; VND dùng điểm cố định 0 chữ số thập phân) |
| 2 | Tạo `eCommerce.Web` + `Volo.Account` + theme miễn phí | Có màn hình login; vào được trang chủ admin |
| 3 | Thêm **Outbox/Inbox** của ABP + **DLQ** | Có test chứng minh event không mất khi giả lập publish lỗi |
| 4 | Tạo `test/eCommerce.IntegrationTests` + Testcontainers PostgreSQL | 1 test mẫu chạy được trong CI |
| 5 | `docs/coding-conventions.md` + `.editorconfig` | Nhóm thống nhất trước khi viết module đầu |
| 6 | 🚨 **Migration `NULLS NOT DISTINCT`** cho **mọi** unique index có `TenantId` (phát hiện F1 của `XC-004`) | Test chứng minh **chèn trùng bị chặn khi `TenantId = NULL`** — ⚠️ **cần PostgreSQL**; hiện unique index **mất tác dụng** ở chế độ 1 tenant |
| 7 | 🚨 **2 test reflection** trong CI: mọi `*AppService` có `[Authorize]` cấp class; mọi permission dùng trong `[Authorize]` đã được định nghĩa (`XC-004` TC-01/TC-02) | CI **đỏ** nếu vi phạm — chặn tái phát 2 lỗi đã xảy ra thật ở Phase 0 |
| 8 | 🚨 **Bật observability** (`XC-005`): JSON console + wire `AbpCorrelationIdMiddleware` + `/health-status` + chỉnh mức log production | `CorrelationId` **có giá trị thật** và trả về trong response; hiện **chưa** wire nên giá trị **rỗng**; **12 metric** chưa có nơi phát |

### 7.1 Các module GĐ1

| Module | Entity chính | API chính | Sự kiện phát | Phụ thuộc | DoD |
|---|---|---|---|---|---|
| **Catalog** | `Product`, `ProductVariant`(SKU), `Category`(cây), `Brand`, `ProductAttribute`/`Option`/`Mapping`, `ProductMedia`, `ProductChannelVisibility` | CRUD sản phẩm/biến thể/danh mục; tra cứu theo slug; publish/unpublish | `ProductPublishedEto`, `VariantPriceChangedEto` | Kernel | ✅ **`CAT-001` XONG** · ✅ **`CAT-002` lát cắt 1–4 XONG** (cây danh mục + API + liên kết sản phẩm↔danh mục + **thuộc tính & sinh tổ hợp biến thể**, đều kiểm chứng HTTP thật); **còn**: `CatCategoryAttributes`, `ProductVariantCreatedEto`, UI; sau đó **`CAT-003`**, người nhận cho 2 ETO (`cat-001-…md` §16–§19 · `cat-002-…md` §16–§19) |
| **Pricing** | `PriceList`, `PriceListItem`, `ChannelPrice`, `TierPrice`, `CostPrice` | tính giá theo (SKU, kênh, số lượng, thời điểm); CRUD bảng giá | `PriceCalculatedEto` | Catalog, Kernel | 🟡 **lát cắt 1 XONG (15/10/2026)** — module `Pricing` + `PriceList`/`PriceListItem`/`ChannelPrice` + **`GiaCoSoResolver` hàm thuần 4 cấp** (thứ tự cố định, NÉM khi cùng cấp+cùng priority) + bậc số lượng + **3 unique index `NULLS NOT DISTINCT`**; **còn**: app service/API + `IPriceResolver` (nạp DB), import idempotent, `CostPrice`, job theo ngày, `RowVersion`, audit (`prc-001-…md` §16.4) |
| **Inventory** | `Warehouse`, `StockItem`, `StockLedgerEntry`(đã có), **`StockReservation`**, `StockAllotment` | nhập/chuyển/kiểm kê; tra tồn khả dụng **theo kênh**; giữ chỗ/nhả chỗ | `StockChangedEto`, `StockReservedEto`, `StockReservationExpiredEto` | Catalog, Kernel | **Test chống oversell 2 luồng đồng thời** (integration, PostgreSQL thật + lock); ledger append-only không có UPDATE; reservation có TTL + job nhả |
| **Sales** | `Cart`, `CartItem`, `CheckoutSession`, `Order`, `OrderLine`, `OrderStatusHistory`, `OrderReturn` | giỏ hàng; checkout; đặt hàng; huỷ; trả hàng; lịch sử trạng thái | `OrderPlacedEto`, `OrderPaidEto`, `OrderCancelledEto`, `OrderReturnedEto` | Catalog, Pricing, Inventory, Customers, PaymentHub | **State machine 4 trục** (thực hiện/tài chính/đối soát/phụ) + **ném lỗi** khi chuyển sai; tính lại giá tại checkout; idempotent theo `ExternalOrderId`; test: webhook gửi trùng không tạo 2 đơn |
| **Customers** | `Customer`, **`CustomerAddressLink`** (bảng LIÊN KẾT của sổ địa chỉ — 🚨 **`Address` do `AddressHub` sở hữu**, không phải Customers: xem [`ADDR-004`](features/address/addr-004-so-dia-chi-khach.md) · `CUS-001` **ADR-01**), `CustomerGroup` | hồ sơ; sổ địa chỉ (dùng AddressHub); nhóm khách | `CustomerRegisteredEto` | Kernel, AddressHub, Identity | Không có mô hình địa chỉ hai nguồn sự thật; customer gắn `IdentityUser` |
| **đối soát thanh toán** (trong `Reconciliation`) | `PaymentReconciliationRun`, `PaymentReconciliationLine`, `Discrepancy` | job định kỳ `querydr` với cổng; báo cáo lệch | `PaymentDiscrepancyFoundEto` | PaymentHub | Có job chạy định kỳ; phát hiện được ca "đã trừ tiền mà đơn chưa `Paid`" |

**Cổng kiểm soát GĐ1 (Definition of Done cấp giai đoạn):**
0. 📄 **Tài liệu GĐ1 đã Approved** (`CAT-*`, `PRC-*`, `INV-*`, `SAL-001…005`, `CUS-001`, `REC-001`, `PAY-001`, `WEB-*`) và 7 tài liệu nền `XC-001…007`.
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
| **Tài liệu thiết kế** | 55 tài liệu feature (Phụ lục A), ~0.5–2 pd/tài liệu | **+28 – 65 person-day** (~15% tổng công) |
| **TỔNG CỘNG** | code + tài liệu | **199 – 337 person-day** (≈ **10–17 tháng** cho 1 dev) |

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
| Vị trí | 🚨 **MỖI MODULE MỘT THƯ MỤC**: `docs/features/<module>/<mã>-<slug>.md` (bảng thư mục: `design-doc-guidelines.md` §3.1) |
| Trạng thái | `Draft → In review → Approved → Implementing → Implemented → Changed` |

## A.3 Danh sách tài liệu cần viết (**55** tài liệu — cập nhật 17/10/2026)

> 📌 **17/10/2026 — thêm 3 tài liệu** so với danh sách 52 ban đầu, sau khi đối chiếu **code thật**:
> **`ADDR-004`** (nợ CHẶN: entity `Address` của sổ địa chỉ **chưa từng tồn tại**, trong khi `CUS-001` ADR-01 và `implementation-plan` §7.1 **mâu thuẫn nhau** về chủ sở hữu — ✅ **đã có tài liệu và đã DUYỆT 17/10/2026**; ⏳ còn **code**, là việc `S1`/`S2`) ·
> **`PAY-001`** (module `PaymentHub` **đã có code** — `PayTransactions`, `VnPayPaymentProvider` — nhưng **không tài liệu nào sở hữu**; `SAL-002` ghi rõ *"Thanh toán/IPN thuộc `PaymentHub`"* ) ·
> **`XC-007`** (nợ *"chưa có CI"* — `handover` §7 việc #2, `web-001` §99.5).

### Chuẩn bị (cross-cutting) — **viết TRƯỚC GĐ1**

| Mã | Tên tài liệu | Ước lượng |
|---|---|---|
| XC-001 | Money, Currency & chính sách làm tròn (nền cho mọi module) | 1 pd |
| XC-002 | Outbox / Inbox & chống trùng sự kiện (dùng của ABP, có relay) | 1.5 pd |
| XC-003 | Khoá phân tán: quy ước đặt tên & phạm vi | 0.5 pd |
| XC-004 | Xác thực, phân quyền & định nghĩa permission | 1 pd |
| XC-005 | Observability: log có cấu trúc, `CorrelationId`, metric | 1 pd |
| XC-006 | **Phân loại thuế GTGT theo SKU & mức thuế suất theo thời gian** (danh mục luật là **dữ liệu**, không hard-code) | 2 pd |
| **XC-007** | 🆕 **CI/CD: cổng kiểm tự động & quy trình phát hành** (`.github/workflows` gọi `tools\kiem-tat-ca.ps1 -DayDu`) | 1 pd |

### GĐ0 — Pháp lý (8 tài liệu)

| Mã | Tên tài liệu |
|---|---|
| ADDR-001 | Tra cứu đơn vị hành chính **2 cấp** (tỉnh + xã) và **3 cấp** cũ |
| ADDR-002 | Đồng bộ **mã địa chỉ theo từng hãng vận chuyển** (dữ liệu đồng bộ, không phải master) |
| ADDR-003 | Chuẩn hoá địa chỉ khách hàng: convert 3 cấp ↔ 2 cấp, xử lý **ca sáp nhập tỉnh** |
| **ADDR-004** | 🆕 **Sổ địa chỉ khách: entity `Address` (AddressHub) + `CustomerAddressLink`** — 🟢 **Approved 17/10/2026** · ⏳ **chờ code** (`S1`/`S2`) — 🔑 **nợ CHẶN #1 đã có quyết định** |
| EINV-001 | Phát hành hoá đơn điện tử (**NĐ 254/2026/NĐ-CP** + **TT 91/2026/TT-BTC**) |
| EINV-002 | Gửi cơ quan thuế & nhận kết quả (SOAP/XML, parse `OK:`/`ERR:`) |
| EINV-003 | Điều chỉnh / thay thế / **huỷ** hoá đơn |
| SIGN-001 | Quy trình **ký 2 bước thủ công** + quản lý chứng thư số |

## A.3.1 Danh sách theo giai đoạn

### GĐ1 — Bán được trên website (19 tài liệu)

| Mã | Tên tài liệu | Module |
|---|---|---|
| CAT-001 | Sản phẩm & biến thể (SKU) | Catalog | ✅ **XONG + kiểm chứng (15/10/2026)**: domain `Product`/`ProductVariant` + **máy trạng thái** (guard `D9`: publish cần biến thể hoạt động) · **FK `Restrict`** thay `Cascade` (giữ lịch sử bán) · xoá mềm `D4` · **11 route API chạy thật** · **2 ETO** + phân loại *ETO mang dữ liệu* vs *thông báo* (**log khởi động 0 Error**) · **sổ đăng ký mã lỗi Catalog đã chốt**. Chi tiết: `cat-001` **§16–§19** |
| CAT-002 | Cây danh mục & thuộc tính/tổ hợp biến thể | Catalog | ✅ **XONG + kiểm chứng (15/10/2026)**: **cây danh mục** (`Category` + `Path` vật chất hoá + `CategoryTreeService` **chống chu trình** + hàng rào DB `CHECK` `Path`/`Level`) · **API cây danh mục** · **liên kết sản phẩm ↔ danh mục** (chống gán trùng + **tối đa MỘT danh mục chính**) · **thuộc tính + SINH TỔ HỢP biến thể** (unique `(TenantId, ProductId, AttributeHash)` — sửa một **lỗi trong tài liệu**) · **BR-012 (biến thể mồ côi) + guard `0023`**. Chi tiết: `cat-002` **§16–§20** |
| CAT-003 | Media, SEO slug & **hiển thị theo kênh** | Catalog | 🟡 **LÁT CẮT 1 XONG — SLUG SEO + SITEMAP (16/10/2026)**: unique index `IX_CatProducts_TenantId_Slug` (**`NULLS NOT DISTINCT`** + bộ lọc, SQL thô) · hàm thuần **`SinhSlug`** (bỏ dấu tiếng Việt **kể cả `đ`/`Đ`** — cái bẫy NFD không tách được) · `Product.DatSlug` **trả slug CŨ** ⇒ `ProductSlugChangedEto` **có `OldSlug`** (trả lời **Q3 của `WEB-002`**) · `DatSlugAsync` (khoá `catalog:slug` + hậu tố số + phát sự kiện) · `GetBySlugAsync`/`GetByVariantSkuAsync` · **`/sitemap.xml` + `/robots.txt`** (công khai) · `SanPham/ChiTiet/{slugOrSku}` (`canonical` = slug, **giữ** URL SKU cũ) · **cơ chế allowlist `[AllowAnonymous]`** (`AllowlistCongKhaiTests` — trước đó `XC-004` BR-004 chỉ là quy tắc **trong tài liệu**) · mã lỗi `0026`/`0027`. **Kiểm chứng 15/15 ca** (Host + Web + DB thật). ⏳ **CÒN — HAI PHẦN LỚN CỦA `CAT-003`**: **(1) MEDIA** (`ProductMedia` + `IMediaStorage` + magic bytes — **0 dòng code**) · **(2) HIỂN THỊ THEO KÊNH** (`ProductChannelVisibility` — **0 dòng code**) · thêm: slug **chưa** tự sinh khi publish · `sitemap` còn dùng **SKU** làm URL · **storefront CHƯA công khai** (`/SanPham*` có `[Authorize]` ⇒ `302` tới `/Account/Login` ⇒ **SEO không phát huy được** cho tới khi có quyết định mở cửa). Chi tiết: `cat-003` **§16** |
| PRC-001 | Bảng giá & **giá theo kênh** | Pricing | ✅ **XONG + kiểm chứng (15/10/2026)**: module `Pricing` · **giá cơ sở 4 cấp** (hàm thuần) · **API bảng giá + bộ tra giá** · **BR-007** (*chỉ MỘT bảng giá `Active` cho mỗi phạm vi*) · **cấp 4 = giá niêm yết** + **hết N+1** (đo được: **3 lệnh EF/giỏ**). Chi tiết: `prc-001` **§16–§19** |
| PRC-002 | Giá bậc thang (tier) & **pipeline tính giá + làm tròn** | Pricing | 🟡 **lát cắt 1 XONG (15/10/2026)** — `TierPriceSet`/`TierPrice` + `ChonBacThang` (trùng `MinQuantity` ⇒ `0008`) + 2 unique index `NULLS NOT DISTINCT`; lát cắt 2: **tính tiền B6–B8 + 4 điểm làm tròn R1–R4** (hàm thuần) XONG; lát cắt 3: **LẮP PIPELINE B1→B8 + API `price-calculation/tinh-gio`** XONG — **bậc thang đã có tác dụng**; lát cắt 4: **API bậc thang + `ChannelShopId`** XONG; lát cắt 5: **sổ giải trình + idempotency** XONG; lát cắt 6: **API tra sổ (`GET`) + lịch sử** XONG; **còn**: B4 (khuyến mãi/`PRO-*`), 🚨 **guard tổng đơn ÂM (cần người quyết)**, cấu hình chính sách làm tròn, phân trang sổ |
| INV-001 | Nhập / chuyển / kiểm kê & **sổ cái kho (append-only)** | Inventory | ✅ **lát cắt 1+2 XONG (15/10/2026)** — 5 `CHECK` (ADR-03) · 4 `StockMovementType` mới (ADR-04) · unique bộ lọc chống ghi trùng sổ cái (ADR-05) · **`Warehouse` + `StockDocument`/dòng (ADR-01/02)** (chứng từ & ghi sổ là HAI bước) · 3 unique index `NULLS NOT DISTINCT` · **mã lỗi kho hợp nhất về một nguồn**; **lát cắt 3+4+5 XONG (15/10/2026)**: dịch vụ ghi sổ + sinh bút toán + **API kho/chứng từ (kiểm chứng HTTP 10 phép đo)** + **API kho & đối soát có metric** (`inventory_recon_mismatch_skus` — metric đầu tiên cho kho); ✅ **exception filter `23505`/`23514`/`23503` XONG (15/10/2026 — handover Phụ lục B)**: 3 ca từng `500` nay trả mã nghiệp vụ (`0032`/`0014`/`0015`), **không nuốt lỗi hạ tầng** (test khoá ranh giới). **CÒN**: tách quyền chi tiết + reseed, idempotency ghi sổ, 📌 chốt diễn giải `Quantity` kiểm kê với BA. ✅ **`SAL-002` checkout một bước XONG** — tính tiền + giữ chỗ trong MỘT transaction, **fail-closed** + **idempotent** (5 phép đo). ✅ **`SAL-003` chốt giữ chỗ → xuất kho XONG (15/10/2026)** — `OnHand` và `Reserved` cùng giảm ⇒ `Available` không đổi; chốt quá hạn bị chặn (`0031`). ✅ **`SAL-001` tạo đơn từ checkout XONG (15/10/2026)** — số tiền lấy **từ sổ giải trình** (không tính lại) + **tripwire** `GrandTotal == TotalTuSoGiaiTrinh`; kiểm chứng HTTP: đơn `324.745` **khớp từng đồng**. ✅ **khép vòng đơn hàng XONG (15/10/2026)**: cột `CheckoutIdempotencyKey` + unique index (chống hai đơn/một checkout) + **`XacNhanAsync`** (xác nhận ⇒ chốt giữ chỗ ⇒ xuất kho); kiểm chứng HTTP (`6/0/6`, đơn hai lần ⇒ `Orders:0002`, quá hạn ⇒ `0031`). ✅ **huỷ đơn ⇒ nhả chỗ NGAY XONG + đã kiểm chứng** (idempotent). ✅ **job nhả chỗ định kỳ HOẠT ĐỘNG (15/10/2026)** — tìm ra & sửa **2 nguyên nhân gốc**: đăng ký worker không được ghi + **thiếu `UnitOfWork`** (job im lặng ném `ObjectDisposedException`); kiểm chứng DB: `Held/Reserved=3` ⇒ **`Expired/Reserved=0`** + bút toán `ReservationRelease`, `fail: 0` (`inv-001` §27) · ✅ **trả hàng XONG + đã kiểm chứng** (`6/0/6` ⇒ `10/0/10`, đối soát `0` lệch) ⇒ **vòng đời đơn KÍN CẢ HAI CHIỀU**. **✅ **loại chứng từ `CustomerReturn` riêng XONG (15/10/2026)** — sổ cái tách bạch *bán/trả* (không cần migration). ✅ **trả hàng MỘT PHẦN + giới hạn LUỸ KẾ XONG (15/10/2026)** — trả quá số đã mua ⇒ `0040` · SKU lạ ⇒ `0041` · **phủ cả hai hình dạng `lines`** (`inv-001` §32–§36). ✅ **`INV-002` giữ chỗ XONG** (15/10/2026) — chống bán vượt tồn đã kiểm chứng HTTP. ✅ **`INV-003` bước 1 + bước 2 XONG** · ✅ **`REC-001` đối soát tồn kho XONG** (15–16/10/2026) · ✅ **`WEB-001…003` XONG phần UI admin/storefront** — xem dòng riêng của từng mã. **CÒN**: **hoàn tiền** (`SAL-006`/`PaymentHub`/kế toán) · tách quyền chi tiết + reseed · idempotency ghi sổ · 📌 chốt diễn giải `Quantity` kiểm kê với BA |
| INV-002 | **Giữ chỗ (reservation) + TTL** & job nhả kho | Inventory | ✅ **XONG + kiểm chứng**: giữ chỗ có TTL + khoá phân tán theo `(tenant, kho, SKU)` (đọc tồn **sau** khi có khoá) · chống giữ trùng nguồn (`0032`) · **job nhả chỗ quá hạn** `RunOnStart` (đã sửa **2 nguyên nhân gốc**: đăng ký worker + **thiếu `UnitOfWork`**; đo được `Held/Reserved=3` ⇒ `Expired/Reserved=0`, `fail: 0`) · **hạn mức kênh** (`INV-003`) cưỡng chế ngay trên đường giữ chỗ (`0042`) — `inv-001` §27–§29 · `inv-003` §16 |
| INV-003 | **Phân bổ tồn theo kênh** & tra tồn khả dụng | Inventory | ✅ **bước 1 XONG (15/10/2026)**: báo cáo phân bổ theo kênh (`GET /api/app/don-hang/phan-bo-theo-kenh`) — `DaBan`/`DangGiu`/`DaTra`, suy ra **từ đơn** (không cần migration); kiểm chứng HTTP khớp dữ liệu thật. ✅ **bước 2 — HẠN MỨC RIÊNG TỪNG KÊNH XONG + kiểm chứng 15/15 ca (16/10/2026)**: bảng `InvChannelLimits` (khoá `(TenantId, ChannelCode, WarehouseId)` · `WarehouseId NULL` = **cả kênh** · `NULLS NOT DISTINCT`) · `HanMucKenh.ChonHanMuc` (**3 nguồn**: dòng theo kho → dòng cả kênh → mặc định chung; nguồn ghi vào **log + dữ liệu lỗi `0042`**) · **`PhamVi` (`Channel` vs `ChannelWarehouse`) nay CÓ tác dụng** · hành động `Block`/`Warn` **riêng từng kênh** · 3 mã lỗi mới (`0043`/`0044`/`0045`) · **+47 unit test** (793 tổng) · migration `Inv003_ChannelLimits_Rec001_ReconRuns`. ✅ **bước 3 — ĐƯỜNG GHI CHUẨN CHO ADMIN XONG + kiểm chứng 11/11 ca (16/10/2026)**: `ChinhSachHanMucAppService` — 6 endpoint `/api/app/chinh-sach-han-muc/*` (đọc/đổi chính sách · danh sách hạn mức · **danh mục kênh có thật** · đặt hạn mức **UPSERT** · xoá **mềm**) · 🔒 ghi qua **`ISettingManager`** ⇒ **cache 2 tầng tự xoá** (đo được: đổi chính sách qua API ⇒ giữ chỗ **bị chặn `0042` NGAY**, **không** restart, **không** xoá cache tay) · validate **kênh có thật** (`0046`) + **giá trị setting** (`Settings:0001`) · cảnh báo **tại chỗ ghi** khi `PhamVi=Channel` mà lưu dòng theo kho · **`GhichuNo()`** trả nợ đang mở ra cho UI · permission **`eCommerce.ChannelLimits`(`.Manage`)** đã seed. Chi tiết: `inv-003` **§17**. ✅ **bước 4 — GIAO DIỆN WEB XONG (§20, `S7`, 18/10/2026)**: `/HanMucKenh` **lọc theo KHO** (kèm *"(không gắn kho)"*) + **phân trang** (`TotalCount` đếm **trước** khi cắt trang) + **ô chọn kho** khi đặt hạn mức + **nhãn theo `Q4`** (*"trần an toàn của kênh theo từng kho"*; `Percent` = **% tồn khả dụng của TỪNG `(kho, SKU)`**) · 8 ca E2E trên **HTML thật** (`inv-003` §20). ⏳ **CÒN**: nút *"Sửa"* điền sẵn (§18.7 #3) · test tích hợp `GiuChoAsync` · **mô hình tồn theo kênh** chưa có (đổi nhãn **không** đổi hành vi) |
| SAL-001 | Giỏ hàng | Sales | ✅ **XONG + kiểm chứng**: tạo đơn từ checkout — **tiền lấy NGUYÊN VĂN từ sổ giải trình** + **tripwire** chống lệch giá + **khoá checkout trên đơn** (unique index chặn **hai đơn/một checkout**) + **`XacNhanAsync`** (xác nhận ⇒ chốt giữ chỗ ⇒ **xuất kho**) (`inv-001` §24–§25, §33) |
| SAL-002 | Checkout & **đặt hàng** (tính lại giá + giữ kho) | Sales | ✅ **XONG + kiểm chứng**: `tinh-va-giu-cho` (giữ chỗ, fail-closed, idempotent) · tiền lấy từ **sổ giải trình** (`PRC-002`) |
| SAL-003 | **State machine 4 trục** + lịch sử trạng thái | Sales | ✅ **XONG + kiểm chứng**: chốt giữ chỗ ⇒ xuất kho (`OnHand` **và** `Reserved` cùng giảm, `Available` **không đổi**) · job nhả chỗ quá hạn (`inv-001` §27) |
| SAL-004 | Huỷ đơn & compensation | Sales | ✅ **XONG + kiểm chứng ĐẦY ĐỦ**: huỷ ⇒ **nhả chỗ NGAY** (idempotent) · **trả hàng** (toàn bộ · **một phần** · **luỹ kế** bị chặn `0040` · SKU lạ bị chặn `0041`) · loại chứng từ **`CustomerReturn`** riêng · đối soát `0` lệch (`inv-001` §30–§36) |
| SAL-005 | **Webhook đơn từ sàn** (idempotent, chịu được sai thứ tự) | Sales | 🟢 **ĐÃ CÓ CODE — 5 LÁT CẮT tới 18/10/2026** (📄 [`sal-005`](features/sales/sal-005-webhook-don-tu-san.md) §16…§21). 🆕 **`S6`**: **§20** = nhật ký sự kiện `SalMarketplaceEventLogs` (append-only, **7 kết cục**) + **connector trả `update_time`** + **bước ⑫** đo **đường chạy thật** (49 ca) · **§21** = **cổng vận hành** đọc watermark + **hạ cờ `GapSuspected`** (**2 permission** mới). ⚠️ **Chưa `Implemented`**: `Order` nội bộ **chưa cập nhật** khi áp watermark (thuộc `SAL-003`/`SAL-006`) · chưa có UI nhật ký/DLQ (`S7`) · chưa kiểm **tiền thật**. 📌 *Mô tả dưới đây là **lịch sử** `LÁT CẮT 1+2` (15–16/10/2026) — giữ để đối chiếu, **không** còn là trạng thái hiện tại:* **LÁT CẮT 1 + 2**: **nơi nhận đơn thật** (`MarketplaceOrderIntake` + handler) ⇒ cổng **fail-closed tự mở** · số đơn sinh **TẤT ĐỊNH** từ `(channelCode, externalOrderId)` · chống trùng ở **mức ĐƠN** (unique `NULLS NOT DISTINCT`, tên index đặt tường minh vì tên EF tự sinh **67 ký tự > 63** bị PostgreSQL **cắt**) · **watermark `D5`**: `PayloadCanonicalizer` · `LuatWatermark` (**4 nhánh**) · **8 trường ETO** + **3 trường `ChannelOrderModel`** · **5 cột watermark** | ✅ đã xử lý tiếp: **DLQ** (§18) · **job `D12`** (§19) · **nhật ký sự kiện + `update_time`** (§20) · **cổng hạ cờ** (§21) |
| CUS-001 | Hồ sơ khách hàng & sổ địa chỉ | Customers | ✅ **XONG (18/10/2026, `S1`+`S2`+`S3`)** — **module `Customers`** (`Customer` + `CustomerGroup` + `CustomerAddressLink`) · **máy trạng thái NÉM LỖI** (`ADR-04`) · **nâng cấp khách vãng lai** (`ADR-07`) · **`PiiMasker`** (`ADR-11`) · **`ICustomerDirectory`** + `CustomerDirectory` (`ADR-12`) · **6 unique index `NULLS NOT DISTINCT`** · **sổ địa chỉ** (`ADDR-004` A+B) · **xuất dữ liệu cá nhân** + **nhật ký kiểm toán** + **CSV** (Luật 91/2025/QH15) · 🚨 **`S3`: CỔNG KHÁCH BỊ CHẶN ĐÃ NỐI** (`CuaKiemKhachGiaoDich` — trước đó **khách `Blocked` vẫn đặt được hàng**: hàm định nghĩa 1 nơi, gọi 0 nơi) — cổng ở **2 đường bán hàng**, **TRƯỚC** mọi tác dụng phụ, chứng minh bằng **HTTP thật** (`403`+`0005`, **0 đơn/0 giữ chỗ/0 sổ giá**, + **2 chiều ngược**) · **`CustomerGroup` API + seed** (3 nhóm, idempotent **theo mã**) + quyền `Customers.ManageGroups` (**hai nơi**) · **phân trang** `PagedResultDto` + `/KhachHang?trang=N` · **đường web mang khách vào đơn** (`KhachId` — trước đó đơn web **không thuộc về ai**). 📊 **Đo:** unit **1228** · E2E **67/67** · probe HTTP **26/26** · DB **23/23**. 📄 Chi tiết: `cus-001` **§28** · ⏳ **CÒN**: **UI** nhóm khách · **gán** khách vào nhóm · **xoá** nhóm (cần người quyết) · **ETO `CustomerChangedEto`** |
| REC-001 | Đối soát thanh toán (`querydr` định kỳ) | Reconciliation | ✅ **ĐỐI SOÁT TỒN KHO XONG + kiểm chứng ĐẦY ĐỦ (15/10/2026)**: job định kỳ 15 phút (`RunOnStart`, UoW, khoá phân tán, log vô điều kiện); **phát hiện đúng** lệch và **đếm đúng** (1 cặp ⇒ báo `1`), **không báo động giả**; đối chiếu **độc lập** bằng SQL khớp. ✅ **LỊCH SỬ LƯỢT QUÉT + ÁP NGƯỠNG THẬT XONG + kiểm chứng (16/10/2026)**: bảng `ReconRuns` (**kèm ẢNH CHỤP chính sách** đơn vị/giá trị ngưỡng/hành động ⇒ đổi setting **không** viết lại lịch sử) + `ReconRunMismatches` (trần 10 000 dòng/lượt) · hàm thuần `NguongLech` (**cơ sở = sổ cái**, không phải `OnHand`; `TongSoCai = 0` + có lệch ⇒ **vượt**) · mã lỗi dải riêng `eCommerce:Recon:0001` · job đổi `UoW` sang **transactional** · migration `Inv003_ChannelLimits_Rec001_ReconRuns` (chung với `INV-003`). **Đo được:** ngưỡng `999` ⇒ `CoVuotNguong = false` (**lọc được báo động giả**) · **4 hàng rào DB nổ đúng mã** ở đường lỗi (`23505` ×3 · `23514` ×2 · `23503`). Chi tiết + bài học `UPDATE 0`: `rec-001` **§16**. ✅ **XÁC NHẬN LỆCH + RETENTION XONG (§19, `S7`, 18/10/2026)**: `Q5` = A — **xác nhận = ĐÓNG CẢNH BÁO + audit** (`ConfirmedByUserId`/`ConfirmedAtUtc`/`ConfirmNote` + **`CHECK`** *"không xác nhận một nửa"* + quyền **`eCommerce.Reconciliation.Confirm`** + endpoint; **KHÔNG** sửa tồn/sổ cái) · **retention** (`Recon.Retention.Days`, 180, `0` = TẮT; luật **THUẦN**; job `ReconRetentionWorker`; nút bấm có **XEM TRƯỚC**; **xoá CỨNG**) · **phân trang** trang chi tiết · 23 ca E2E trên **HTML/DB thật**. ⚠️ **CÒN**: metric retention + metric đường xử lý lệch · GUID người xác nhận trên UI · *"xử lý hàng loạt"* · đối soát **thanh toán** (`querydr`) **chưa kiểm tiền thật** (`inv-001` §37–§39) |
| WEB-001 | UI admin: khung, đăng nhập, menu, phân quyền | Web | ✅ **XONG + kiểm chứng (15/10/2026)**: khung MVC + theme · **cổng đăng nhập** (state machine 7 bước, **`BR-009` kiểm chứng**: `ReturnUrl` ngoài ⇒ `/`, nội bộ ⇒ giữ nguyên, `web-001` §49) · **harness đăng nhập** (mở khoá mọi kiểm chứng trang Web có dữ liệu) · **điểm đo thống nhất `/health-status`** cho **cả** Host (:5000) **và** Web (:5100) · **`/DonHang` đủ BA hành động** (xác nhận · huỷ · trả hàng — đo tới **tồn kho + chứng từ**, `web-001` §65/§69) · **lối vào `/DonHang`** từ trang kết quả · **chi tiết SP + SEO** (`/SanPham/ChiTiet/{sku}` — `<title>`/`meta description`/`canonical`). ⏳ **CÒN**: **menu/điều hướng theo permission** + `[AllowAnonymous]` allowlist + ma trận quyền tự động cho UI (`web-001` §16.6) |
| WEB-002 | Storefront: duyệt & chi tiết sản phẩm (SEO) | Web | ✅ **một phần**: `/SanPham` (hàng đang bán × đơn giá, **đối chứng chéo API**, `web-001` §54). ⏳ **CÒN**: **chi tiết sản phẩm** + **SEO** (slug/meta — `CAT-003`) |
| WEB-003 | Storefront: giỏ hàng & checkout | Web | ✅ **XONG phần cốt lõi + kiểm chứng (15/10/2026)**: `/DatHang` — **khoá checkout sinh ở GET** (bấm hai lần ⇒ **1 đơn**, `web-001` §56) · **giỏ nhiều dòng** (`SKU:SL,…`, **gộp SKU trùng**) · **CA BIÊN đã đo** (`SKU:0`/`SKU:abc` ⇒ mặc định · trùng ⇒ gộp · **SKU lạ ⇒ không tạo đơn** + mã `Pricing:0004` kèm dữ liệu, §72) · **thông điệp lỗi kèm mã + dữ liệu** (§59) · **đường nối UI→API đủ BA chuyển trạng thái**: xác nhận (§65) · huỷ (§69) · trả hàng (§69) · **giỏ tới 5 dòng** (chọn số dòng 1..5 · ca biên · giữ dòng **cả hai đường**) · **giỏ SỐNG QUA PHIÊN** bằng cookie · nút xoá giỏ. ⏳ **CÒN**: **`CAT-003`** (slug thay SKU + `sitemap.xml`/`robots.txt`) · thanh toán/hoàn tiền trên UI |
| **PAY-001** | 🆕 **Cổng thanh toán VNPay: tạo giao dịch, IPN/webhook, hoàn tiền & đối soát** | **PaymentHub** | 🟡 **ĐÃ CÓ CODE, MỚI CÓ TÀI LIỆU (17/10/2026)**: module `PaymentHub` có `PaymentTransaction` (bảng `PayTransactions`, 3 index), `IPaymentProvider`/`VnPayPaymentProvider`/`VnPayRequestBuilder`, `PaymentAppService`, `LuatDoiSoatThanhToan`, `MetricDoiSoatThanhToan` — nhưng **không tài liệu nào sở hữu** (Phụ lục A cũ không có mã `PAY-*`; `SAL-002` ghi rõ *"Thanh toán/IPN thuộc `PaymentHub` + `REC-001`"*). 🚨 **Nợ chặn #2 KHÔNG phải tài liệu mà là CREDENTIAL**: `"TmnCode": ""` và `"HashSecret": ""` **rỗng** ⇒ **chưa kiểm chứng được tiền thật ra** (`handover` §3.1). Chi tiết: `docs/features/payment/pay-001-cong-thanh-toan-vnpay.md` |

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
| SAL-006 | Trả hàng / **RMA** & hoàn tiền (một phần / toàn bộ) | 🟡 **ĐANG LÀM SỚM (dù thuộc GĐ2)** — vì `SAL-006` là **cổng kiểm soát "KHÔNG MẤT TIỀN"** và `GOAL` hiện tại yêu cầu nó. **Tiến độ chi tiết KHÔNG nằm ở bảng này** (bảng này chỉ liệt kê **GĐ1**) — xem `docs/tiep-tuc-phien-moi.md` dòng **7a** và `docs/features/sales/sal-006-tra-hang-rma-hoan-tien.md` **§16/§17**. 📌 **ĐÃ XONG**: bất biến hoàn tiền ở tầng domain (§16) · **`RefundAsync`/`QueryStatusAsync` VNPAY cài THẬT** + API hoàn tiền 4 endpoint (§17). ⏳ **CÒN**: 🚨 **chưa kiểm chứng được TIỀN THẬT RA** (không có credential sandbox) · **chưa có phiếu RMA** · chưa có webhook controller · chưa có job đối soát `querydr` định kỳ |

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

**Tổng: ~28–65 person-day** cho 55 tài liệu (đã cộng vào §13).

## A.5 Điều kiện để một giai đoạn được coi là "bắt đầu"

Một giai đoạn **chỉ được bắt đầu code** khi:
1. ✅ **Toàn bộ tài liệu của giai đoạn đó đã Approved**, hoặc
2. ✅ Có thoả thuận rõ ràng bằng văn bản cho phép làm song song (ví dụ: XC-001 dùng chung nên phải xong trước; các tài liệu còn lại có thể Approved theo từng đợt nhỏ), **nhưng** mỗi feature vẫn phải Approved **trước khi code feature đó**.

## A.6 Việc cần làm ngay khi bắt đầu

1. Nhóm **review mẫu** `templates/feature-design-template.md` — đặc biệt 4 mục mới so với tài liệu gốc: state machine · sự kiện/idempotency · bất biến & đồng thời · migration.
2. Viết **XC-001…XC-007** (7 tài liệu nền) — vì mọi module phụ thuộc chúng.
3. Viết **ADDR-001…004 + EINV-001** (GĐ0) — vì đây là nghĩa vụ pháp lý và chặn thiết kế địa chỉ/hoá đơn.
4. Song song: gửi email hỏi NCC (§16) để có thông số thật điền vào tài liệu.

## A.7 Trạng thái

| Hạng mục | Trạng thái |
|---|---|
| Mẫu tài liệu | ✅ đã viết — **chờ nhóm review** |
| Hướng dẫn + bảng sửa đổi so với tài liệu tham khảo | ✅ đã viết |
| Danh sách 55 tài liệu | ✅ đã liệt kê (A.3) — **52 bản gốc + 3 bổ sung 17/10/2026** (`ADDR-004`, `PAY-001`, `XC-007`) |
| Script kiểm tra Mermaid | ✅ `npm --prefix tools run check:mermaid` |
| Script kiểm tra **tham chiếu chết** | ✅ `pwsh -File tools\kiem-link-tai-lieu.ps1` (thêm 17/10/2026 — sau khi tài liệu được tổ chức lại theo module) |
| **Tài liệu thực tế** | 🟢 **55/55 đã có tệp** — trong đó 🟡 **1/55 đã `Approved`** (`ADDR-004`, 17/10/2026); **54** còn lại vẫn `Draft` |
| **Nhóm nền `XC-*`** | ✅ **ĐÃ VIẾT ĐỦ 7/7** — sẵn sàng review để chốt trước khi vào GĐ1 |

### A.8 Tài liệu đã viết

| Mã | Tài liệu | Trạng thái | Ghi chú |
|---|---|---|---|
| `XC-001` | [Money, Currency & chính sách làm tròn](features/cross-cutting/XC-001-money-currency-lam-tron.md) | 🟡 `Draft` | 630 dòng · 7 quyết định thiết kế (ADR) · 12 BR · 6 NFR · 15 test case · **6/6 câu hỏi đã đóng** |
| `XC-002` | [Outbox/Inbox & chống trùng sự kiện](features/cross-cutting/XC-002-outbox-inbox-chong-trung-su-kien.md) | 🟡 `Draft` | **1196 dòng** · 12 ADR · **21 BR** · 13 NFR · **38 test case** · 2 state machine · 4 bảng trong data dictionary · ✅ **4 câu hỏi đã chốt 13/10/2026**: transport **in-process** · module **`Messaging`** · ngưỡng thử **5** · job DLQ **1 phút** · 🔍 đã **xác minh độc lập bằng quét 424 DLL ABP 10.7** (xác nhận **không** có `IsEventOrderingEnabled`) |
| `XC-005` | [Observability: log, CorrelationId & metric](features/cross-cutting/XC-005-observability-log-metric.md) | 🟡 `Draft` | **13 BR** · 8 NFR · 12 test case · **bảng đăng ký metric** gom **7 metric** đang bị `XC-002`/`XC-003` yêu cầu mà **chưa có nơi phát** · 🚨 **phát hiện F1: hạ tầng quan sát gần như TRỐNG** — **0** gói Serilog/OpenTelemetry/HealthCheck; `CorrelationId` chỉ là **cột trong bảng ABP** (12/12 chỗ nằm trong `Migrations/`), **chưa** wire middleware ⇒ giá trị **rỗng** · F2: payload webhook lưu thô 8.000 ký tự |
| `XC-004` | [Xác thực, phân quyền & chiều tenant](features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md) | 🟡 `Draft` | **14 BR** · 8 NFR · **14 test case** · 🚨 **2 test reflection chặn tái phát lỗi Phase 0** (`TC-01` mọi app service phải có `[Authorize]`; `TC-02` mọi permission dùng phải được **định nghĩa**) · 🚨 **phát hiện F1: unique index mất tác dụng khi `TenantId` NULL** (PostgreSQL `NULLS DISTINCT`) ⇒ **chưa sửa được** (cần PostgreSQL) · F2 multi-tenancy chưa bật · F3/F4 password grant + mật khẩu seed trong tài liệu |
| `XC-003` | [Khoá phân tán: quy ước & phạm vi](features/cross-cutting/XC-003-khoa-phan-tan-quy-uoc.md) | 🟡 `Draft` | **bảng đăng ký khoá lập từ CODE THẬT** (8 khoá + 2 khoá ABP) · 10 ADR · 10 BR · 7 NFR · 14 test case · 🚨 **phát hiện F1: khoá kênh định nghĩa 2 nơi cho ra 2 CHUỖI KHÁC NHAU** (`ecommerce:channel:…` vs `channel:…`) ⇒ dùng lẫn là **khoá không bảo vệ gì** — **đã sửa**; **F3** (khoá Messaging thiếu tiền tố) **đã sửa**; **F2/F4** ghi nhận |
| `XC-006` | [Phân loại thuế GTGT theo SKU](features/cross-cutting/XC-006-phan-loai-thue-gtgt.md) | 🟡 `Draft` → 🚀 **đang triển khai** | **11 BR** · 8 NFR · **22 test case** · **7/9 câu hỏi đã đóng** · đặc tả **Mẫu 01 (Phụ lục III)** từ **file gốc** · danh mục Phụ lục I/II là **dữ liệu**, không hard-code · ✅ **code lõi xong**: module `Tax` (Domain+EFCore), migration + `EXCLUDE USING gist`, seed 4 mức thuế, **17 unit test** (⚠️ chưa chạy trên PostgreSQL thật) |

---

## A.9 Tài liệu thiết kế — ✅ **55/55 đã có tệp** (cập nhật 17/10/2026; bản gốc 46/46 ngày 13/10/2026)

Toàn bộ tài liệu thiết kế của **A.3** đã có tệp. **Kiểm chứng tập trung (chạy máy, không phải tự khai):**

| Kiểm chứng | Kết quả |
|---|---|
| Đủ **16/16 mục** theo mẫu (§0, §0.1 ADR, §1..§15) | ✅ **cả 55 tài liệu** (trừ `XC-006-phieu-hoi-ke-toan.md` — đó là **phiếu hỏi kế toán**, không phải tài liệu thiết kế) |
| Mermaid hợp lệ (parser thật) | ✅ chạy `npm --prefix tools run check:mermaid` |
| **Tham chiếu chết** trong tài liệu | ✅ chạy `pwsh -File tools\kiem-link-tai-lieu.ps1` — 0 tham chiếu chết |
| Neo vào code thật (kèm `file:dòng`) | ✅ có — mỗi tài liệu ghi rõ phần **"chưa có trong code — kế hoạch GĐx"** và **"chưa kiểm chứng"** |
| 🚨 **Trạng thái review** | 🟡 **1/55 đã được `Approved`** — **`ADDR-004`** (duyệt 17/10/2026) · 🔴 **54/55 vẫn `Draft`**. Đây là **cổng thật** của A.5, xem ghi chú dưới |

| GĐ | SL | Mã tài liệu |
|---|---|---|
| **Nền** `XC-*` | 7 | `XC-001` `XC-002` `XC-003` `XC-004` `XC-005` `XC-006` `XC-007` |
| **GĐ0** Pháp lý | 8 | `ADDR-001` `ADDR-002` `ADDR-003` `ADDR-004` `EINV-001` `EINV-002` `EINV-003` `SIGN-001` |
| **GĐ1** Bán trên website | 19 | `CAT-001` `CAT-002` `CAT-003` `PRC-001` `PRC-002` `INV-001` `INV-002` `INV-003` `SAL-001` `SAL-002` `SAL-003` `SAL-004` `SAL-005` `CUS-001` `REC-001` `PAY-001` `WEB-001` `WEB-002` `WEB-003` |
| **GĐ2** Vận hành & tuân thủ VN | 9 | `PRO-001` `PRO-002` `SHP-001` `SHP-002` `SHP-003` `SHP-004` `REC-002` `REC-003` `SAL-006` |
| **GĐ3** Điều hành | 6 | `REP-001` `REP-002` `REP-003` `NOT-001` `NOT-002` `ACC-001` |
| **GĐ4** Tăng trưởng | 6 | `CHN-001` `CHN-002` `CHN-003` `CHN-004` `LOY-001` `REV-001` |

> ⚠️ **Trạng thái thật: `Draft` — CHƯA có tài liệu nào được con người review.**
> Chúng do nhiều agent viết song song, **mỗi tài liệu neo vào code thật** (kèm `file:dòng`), và phần lớn
> **đã phát hiện sai lệch thật** trong code hoặc trong tài liệu khác (xem **§15.2** của từng tài liệu).
> Một số phát hiện đã được **sửa ngay trong code**, ví dụ:
> • **C13** — tiền tố **ĐÔI** trên 3 hàm sinh khoá lock (`ecommerce:ecommerce:…`) → đã bỏ tiền tố + **4 test bất biến**
> • **C16** — `ConventionalControllers` chỉ đăng ký 2 assembly ⇒ **mọi module mới trả `404` body rỗng** (đúng triệu chứng đã truy lỗi C8) → đã đăng ký động + **ghi log `[C16]`**
> • **C15** — allowlist ở `XC-004` Q1 **mâu thuẫn** với webhook bắt buộc `[AllowAnonymous]` → đã bổ sung 2 nhóm

---

## A.10 CHỐT GIAI ĐOẠN 0 (14/10/2026)

📄 **Báo cáo chốt GĐ0: [`docs/gd0-closure-report.md`](gd0-closure-report.md)**

**Kết luận:** GĐ0 **đóng ở mức có thể đóng bằng code**. 7/7 feature GĐ0 có Domain + DB + API; luồng
nghiệp vụ khép kín từ *xác minh địa chỉ* tới *nhận kết quả cơ quan thuế*. Phần còn lại là **5 việc phụ
thuộc input bên ngoài** (văn bản pháp luật, tài liệu đối tác, chứng thư số) — **không thể suy đoán**.

**Bổ sung trong lượt chốt — `ADDR-001` lõi đồng bộ danh mục ĐVHC:**
- `AdministrativeUnitSynchronizer` (lõi thuần, 16 test) + `AdministrativeUnit.DoiTen/MoLai`.
- **4 hàng rào chống "đồng bộ phá hoại"**: `eCommerce:Address:0601` (danh mục rỗng) · `0602` (quá ít) ·
  `0603` (thiếu cấp tỉnh) · `0604` (đóng vượt 20%).
  *Lý do:* đồng bộ **đóng** những mã không còn trong nguồn; nếu nguồn trả về **một phần** thì sẽ
  **đóng gần hết địa chỉ đang dùng** ⇒ mọi vận đơn mới fail-closed ⇒ **sập luồng bán hàng**.
- Bất biến: **không bao giờ xoá** (chỉ đóng) · **đổi tên tại chỗ** (giữ `Id`) · **chỉ xét đúng phiên bản schema**.

**Việc làm được ngay, không chờ ai (đề xuất lượt sau):**
**(A)** worker đồng bộ ĐVHC dùng 4 hàng rào đã có · **(B)** ràng buộc `EINV-003` một bản thay thế hiệu lực ·
**(C)** worker đồng bộ mã hãng với adapter cắm được.

---

## A.11 🔑 BÀN GIAO SANG GIAI ĐOẠN 1 (14/10/2026)

📄 **ĐỌC TRƯỚC KHI LÀM GÌ TIẾP: [`docs/handover-gd1.md`](handover-gd1.md)**

Chứa: trạng thái hiện tại (số liệu đã kiểm chứng) · **quy trình kiểm chứng đã trả giá** (13 quy tắc, rút ra
từ **16 lỗi im lặng** thật) · **quyết định đang mở của GĐ1** (lỗi #22 — đơn hàng mất) · 5 input bên ngoài ·
bản đồ tài liệu · quy ước bất biến · **cách mở session mới cho đúng**.

> 🚨 **Quan trọng:** session mới **không nhớ** cuộc trò chuyện trước. File bàn giao là **bộ nhớ bền vững**.
> **Đừng dán lại lịch sử trò chuyện cũ vào session mới** — nó sẽ **cạn ngữ cảnh ngay từ đầu**.

---

## A.12 LƯỢT GĐ1 ĐẦU TIÊN (14/10/2026) — **SỬA GỐC LỖI #22 (FAIL-CLOSED)**

**Quyết định của người:** **"B ngay + A sau"** — chặn mất dữ liệu **ngay**, rồi xây **Order intake** (việc A)
trong GĐ1. Và: **làm hạ tầng nền §7.0 trước khi vào các module GĐ1**.

| Hạng mục | Kết quả |
|---|---|
| **Lỗi #22 (fail-closed)** | ✅ **XONG** — `ChannelShopSyncer` **không** publish + **không** tiến con trỏ khi lô có đơn mà chưa có người nhận |
| **Biến thể thứ HAI của #22** (phát hiện mới) | ✅ **XONG** — nút *"Đồng bộ ngay"* (`ChannelAppService.SyncNowAsync`) trước đây **tự kéo đơn + tự tiến con trỏ + không phát sự kiện cho ai** ⇒ nay đi qua **cùng một cổng** `IChannelShopSyncService` (đóng luôn **F3** của `CHN-002`) |
| **Một nguồn sự thật** cho *"sự kiện có người nhận chưa?"* | ✅ `EventCoverageScan` + `IEventCoverageProbe` (Messaging) dùng **chung** cho cảnh báo lúc boot **và** quyết định lúc đồng bộ ⇒ hai nơi **không thể lệch nhau** |
| Kiểm chứng | Build **0/0** · **383 unit** + **20 integration** · Host thật `health 200` · **DB thật**: con trỏ **không tiến** + `LastError` `eCommerce:Channel:0005` · log **Error** thật · **ma trận quyền đo lại đầy đủ** (`401`/`403`/`200` trên endpoint **gắn quyền**) |
| Bằng chứng chi tiết | `docs/runtime-verification-report.md` §9bis · `docs/features/channel/chn-002-connector-shopee.md` §16 |

### 🚨 `F-403` — **ĐÃ SỬA** (cùng lượt): mọi API gắn quyền trả `403` dù đã cấp quyền

**Triệu chứng:** **mọi** endpoint có `[Authorize(permission)]` trả **`403`** kể cả với `admin`, trong khi
endpoint **chỉ cần đăng nhập** vẫn `200` ⇒ **tưởng hệ thống vẫn tốt**. Nó **không phát ra một dòng log nào ở
phía server** nên đã ẩn rất lâu.

**Nguyên nhân gốc (ĐO, không đoán):** `JwtBearer` mặc định `MapInboundClaims = true` ⇒ **đổi tên claim**
`role`/`sub` sang khuôn WS-Federation cũ, trong khi **ABP đọc tên NGẮN OIDC** (`AbpClaimTypes`) ⇒
`ICurrentUser` **RỖNG** ⇒ `RolePermissionValueProvider` không thấy vai trò nào ⇒ từ chối tất cả.
Log chẩn đoán thật: `claim vai trò: …/claims/role=admin` nhưng `ABP current user: id='' · Roles=[(RỖNG)]`.

**Đã sửa:** `MapInboundClaims = false` + `NameClaimType = "preferred_username"` + **bất biến lúc khởi động**
`KiemTraBatBienTenClaim` (ghi **Error** nếu ai bật lại) — vì đây là bất biến **cấu hình**, test thường
**không** thấy được. Đồng thời bịt **lỗ hổng quan sát** bằng `ChanDoanTuChoiQuyenHandler` (mỗi lần từ chối
ghi một dòng `Warning` kèm đủ dữ kiện chẩn đoán).

**Ma trận đã đo lại (Host thật):** không token → **401** · `client_credentials` (không quyền) → **403** ·
admin có quyền → **200**. Chi tiết: `docs/features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md` **§15.2 F6 + §15.6**.

> ⚠️ **ĐÍNH CHÍNH:** dòng *"ma trận `401`/`403`/`200` đúng"* ở `docs/handover-gd1.md` §1 dựa trên ca `200`
> đo **nhầm endpoint** (chỉ cần đăng nhập, **không** gắn quyền) ⇒ **không** chứng minh được máy phân quyền
> hoạt động. **Bài học: đo trên "đối tượng gần giống" thì không phải là kiểm chứng.**

### ⚠️ Hệ quả CẦN BIẾT của phương án B

Cho tới khi xây xong **Order intake (A)**, **đồng bộ sàn bị chặn CÓ CHỦ Ý**: đơn **KHÔNG mất** (sàn gửi lại)
nhưng cũng **chưa vào được hệ thống**, và log **Error** lặp **mỗi 5 phút/shop** (nhịp mặc định 300 s).
Đây là **đánh đổi đã được người quyết định chấp nhận** — **không phải lỗi**, và **không cần bật cờ nào**:
khi Order intake đăng ký handler cho `ChannelOrderPulledEto`, fail-closed **tự mở**.

### Tiến độ hạ tầng nền §7.0 (cập nhật 14/10/2026)

| # | Việc (§7.0) | Trạng thái |
|---|---|---|
| 0 | Viết & duyệt `XC-001`…`XC-006` | 🟡 **đã viết đủ 6/6** nhưng **chưa có tài liệu nào được con người review** (đang `Draft`) |
| 1 | `shared/eCommerce.Kernel` (Money, Currency, mã lỗi, lock key, metrics) | ✅ **có** — `Money`/`Currency`/`RoundingPolicy`/`MoneyAllocator` + `Observability` + test `MoneyTests` |
| 2 | `eCommerce.Web` + `Volo.Account` + theme + màn hình login | ✅ **XONG (15/10/2026)** — `src/eCommerce.Web` (cổng **5100**), theme **Basic** miễn phí + `Volo.Abp.Account.Web`. **Đăng nhập THẬT đã kiểm chứng** (vào được trang chủ quản trị). Chi tiết + bằng chứng: `docs/features/web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md` **§16** |
| 3 | Outbox/Inbox + DLQ | ⬜ **cố ý hoãn** — xem cảnh báo dưới |
| 4 | `test/eCommerce.IntegrationTests` + Testcontainers PostgreSQL | ✅ **có** — **20** integration test chạy trên PostgreSQL 17 thật |
| 5 | `docs/coding-conventions.md` + `.editorconfig` | ✅ **XONG (14/10/2026)** |
| 6 | Migration `NULLS NOT DISTINCT` cho mọi unique index có `TenantId` | ✅ **XONG** — **19** index `NULLS NOT DISTINCT`, có test đối chiếu **đích danh tên index** |
| 7 | 2 test reflection `XC-004` TC-01/TC-02 trong CI | ✅ **XONG (14/10/2026)** — `BatBienPhanQuyenTests` (**5** test) + **kiểm chứng ngược bằng đột biến** |
| 8 | Observability (JSON console, `CorrelationId`, `/health-status`, mức log production) | 🟡 **một phần** — đã wire `UseCorrelationId` + `/health-status` + middleware đo độ trễ. ✅ **`S10` (21/10/2026) ĐO LẠI thay vì khai báo:** **11/15** metric **có** nơi phát · **4/15** **chưa** (`messaging_outbox_pending` · `messaging_dlq_replayed_total` · `lock_ttl_exceeded_total` · `logging_failures_total`) — 📌 cả 4 cần **hạ tầng mới**. 🔒 Từ nay con số này **có phép kiểm hai chiều** (`MetricEmitterTests`) ⇒ không thể trôi. 📄 `XC-005` **§19** |

> 🚨 **Vì sao #3 (Outbox/Inbox + DLQ) vẫn HOÃN:** **không có message broker** (chỉ PostgreSQL + Redis).
> Outbox bảo đảm *giao tin cậy* — nhưng giao **cho không ai** thì dữ liệu **vẫn mất**. **Thứ tự đúng:
> có NGƯỜI NHẬN trước (Order intake `SAL-005`), rồi mới bàn tới giao tin cậy.**

### Việc tiếp theo (thứ tự đề xuất)

1. **`PRC-001` (tiếp)** — app service/API bảng giá + `IPriceResolver` nạp DB rồi giao `GiaCoSoResolver` thuần (kiểm chứng **bằng HTTP thật**). Sau đó **`PRC-002`** (bậc thang + pipeline 8 bước + 4 điểm làm tròn) rồi **Inventory**. Phần còn lại của `CAT-002` (`CatCategoryAttributes`, `ProductVariantCreatedEto`, UI) **không chặn** luồng bán ⇒ làm sau cùng với `CAT-003` (media/slug — ⚠️ **index `Slug` phải dùng dạng CÓ FILTER**, `cat-002` §18.3).
   ✅ **ETO đã có** (`CAT-001` §18) và đã **phân loại đúng**; ✅ **mã lỗi đã chốt sổ đăng ký**
   (`CAT-001` §19: `CAT-002` dùng dải `0020+`; mã khoá phân tán dùng chung `eCommerce:Lock:0001`).
   **Còn**: **người nhận** cho `ProductPublishedEto`/`VariantPriceChangedEto` (thuộc `CHN-*` và `WEB-002`).
2. 🚨 **Cần người quyết — CHÍNH SÁCH LỖI CỦA API (3 điểm, đo được ở `CAT-001` §17.3 + §19.7):**
   (a) `AbpExceptionHandlingOptions` có nên ánh xạ `BusinessException` → **`400`/`409`** thay vì **`403`**?
   Hiện lỗi nghiệp vụ **trùng mã trạng thái với lỗi thiếu quyền** ⇒ client/QA không phân biệt được.
   (b) *"Không tìm thấy"* phải **nhất quán**: hiện `GET /{id}` trả **`404` không có mã**, còn
   `GET /by-code` trả **`403` kèm `Catalog:0010`** — cùng một service, hai hành vi.
   (c) **`404` đang ghi log mức `Error`** (`EntityNotFoundException` qua `AbpExceptionFilter`) ⇒ nhiễu cảnh báo.
   ⚠️ **`Catalog:0012` còn bị `[Required]` của DTO che khuất trên đường API** ⇒ cần chốt ai chịu trách nhiệm
   báo lỗi: **validation của DTO** hay **guard của domain**. Đây là **thay đổi hợp đồng API toàn hệ thống** ⇒ **không** tự ý làm.
3. ✅ **`SAL-005` §16.4** — xử lý **cập nhật theo watermark** (D5): **ĐÃ XONG 16/10/2026** (§18 — nối luật 4 nhánh vào handler) và **`S6`** làm nốt **nguồn watermark** (connector trả `update_time` ⇒ đo được ở bước ⑫ `C21`). 📌 **Còn lại thuộc `SAL-003`**: cập nhật **`Order` nội bộ** khi `ApDung` ·
   `SalMarketplaceOrderEventLogs` · **đẩy DLQ** cho xung đột payload khi `XC-002` có DLQ.
4. **`WEB-001` §16.6** — menu/điều hướng theo permission + `[AllowAnonymous]` allowlist + ma trận quyền tự động cho UI.
5. **Test tích hợp ma trận quyền** trên endpoint **gắn quyền** (`XC-004` TC-05/TC-06) — hiện chỉ đo bằng **script thủ công**.
6. **5 metric còn lại** · phần observability còn thiếu (§7.0 #8) · supervisor thiếu `Timer.RunOnStart` (`CHN-002` §16.5 #5).
