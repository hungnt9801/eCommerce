# 03 — Kiến trúc đích (backend) theo mô hình Nhanh.vn

> Đầu vào: [01 — khảo sát](01-khao-sat-nhanh-vn.md) · [02 — đối chiếu](02-hien-trang-va-doi-chieu.md).
> Đầu ra: cây solution, bounded context, mô hình domain,state machine, hợp đồng API, đa doanh nghiệp,
> hiệu năng. **Không có UI ở đây** — UI ở [04](04-web-admin-nextjs.md).

---

## 1. Ba phương án kiến trúc và lựa chọn

| # | Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|---|
| A | **Giữ ABP monolith + refactor domain + Next.js admin** | Tái dùng 40% domain, 388 test, auth/outbox/sổ cái đã kiểm chứng; 1 ngôn ngữ backend | Phải sửa mô hình Product/Stock/Order (đau nhưng 1 lần) | ✅ **CHỌN** |
| B | Viết lại backend bằng NestJS + TypeScript dùng chung type với Next.js | Đồng nhất ngôn ngữ FE/BE, type safety end-to-end | Bỏ 57 project + 388 test; 9–12 tháng mới bằng hiện tại; mất ABP permission/multi-tenant có sẵn | ❌ |
| C | Giữ Razor MVC, chỉ thêm Next.js cho POS | Ít việc nhất | Hai UI song song, không đạt "UI giống Nhanh", Razor không đáp được bảng 20 cột/tab động | ❌ |

**Nguyên tắc A:** *strangler* — dựng màn hình mới trên API mới, module cũ đóng băng dần;
không big-bang rewrite, không "đập đi xây lại".

---

## 2. Cây solution đích

```
eCommerce.slnx
├─ apps/
│  └─ admin-web/                    ← Next.js (xem 04). KHÔNG nằm trong slnx
├─ packages/
│  ├─ api-client/                   ← type sinh từ OpenAPI + fetch wrapper (npm workspace)
│  ├─ ui/                           ← design system dùng chung (nếu tách)
│  └─ config/                       ← eslint/tsconfig/prettier dùng chung
├─ src/
│  ├─ eCommerce.HttpApi.Host        ← API duy nhất + OpenIddict (auth server)
│  ├─ eCommerce.DbMigrator          ← migration + seed
│  ├─ eCommerce.Web                 ← storefront Razor (đóng băng, giữ chạy)
│  └─ eCommerce.Kernel              ← shared kernel: Money, Quantity, Result, Clock, Ids
├─ modules/
│  ├─ Catalog/          Hàng hóa        🟡 refactor
│  ├─ Inventory/        Kho             🟡 refactor
│  ├─ Sales/            Đơn hàng        🟡 mở rộng
│  ├─ Pos/              Bán lẻ tại quầy 🔴 mới
│  ├─ Customers/        Khách hàng      🟡 mở rộng
│  ├─ Promotions/       Khuyến mại      🔴 mới
│  ├─ Accounting/       Kế toán         🔴 mới
│  ├─ Reporting/        Báo cáo         🔴 mới
│  ├─ Shipping/         Vận chuyển      🔴 mới
│  ├─ Suppliers/        Nhà cung cấp    🔴 mới (P1)
│  ├─ Warranty/         Bảo hành        🔴 mới (P2)
│  ├─ ChannelHub/       Kênh sàn        🟢 giữ
│  ├─ PaymentHub/       Thanh toán      🟢 giữ
│  ├─ EInvoice/         HĐĐT            🟢 giữ
│  ├─ Signing/          Chữ ký số       🟢 giữ
│  ├─ Tax/              Thuế            🟢 giữ
│  ├─ Address/          Địa chỉ         🟢 giữ
│  ├─ Reconciliation/   Đối soát        🟢 giữ
│  ├─ Messaging/        Outbox/DLQ      🟢 giữ
│  └─ Settings/         Cấu hình/cửa hàng 🔴 mới (P0, nhỏ)
└─ test/
   ├─ eCommerce.Tests
   └─ eCommerce.IntegrationTests
```

**Quy ước mỗi module** (giữ nguyên như hiện tại, đã chạy tốt):
`<Module>.Domain` · `<Module>.Application.Contracts` · `<Module>.Application` · `<Module>.EntityFrameworkCore`.

**Luật phụ thuộc (bắt buộc):**
1. Module **không** tham chiếu `.Domain` của module khác — chỉ `.Application.Contracts`.
2. Giao tiếp bất đồng bộ qua **ETO** (outbox đã có); đồng bộ qua interface `I…Gateway` khai trong Contracts.
3. App layer (`eCommerce.Application`) là nơi **điều phối** (Sales gọi Inventory/Promotion/Accounting).
4. Không module nào biết HTTP/Next.js.

---

## 3. Mô hình domain

### 3.1 Catalog — Hàng hóa

```
Product (aggregate root)
├─ Id, Code(SKU), Name, Type(1..10), Status(1..4)
├─ ParentId?            (cha–con; cha KHÔNG có tồn)
├─ CategoryIds[], BrandId?
├─ VatPercent(-100..), VatMode(1 đã gồm | 2 chưa gồm)
├─ WeightGram, WarrantyMonths, StorageTimeDays
├─ Description, Slug, Media[], Tags[], ExternalId?
├─ IsHot, IsNew
└─ Units[]  ─ ProductUnit (1..n)
             ├─ Name ("lon", "thùng"), ConversionFactor (1, 24)
             ├─ Barcode (unique/tenant), IsBase
             └─ Prices { Import, Cost, Sale, OldPrice, Wholesale, Branch[] }
```

| Enum | Giá trị |
|---|---|
| `ProductType` | 1 Thường · 2 Voucher · 3 Cân đo · 4 IMEI · 5 Gói · 6 Dịch vụ · 7 Dụng cụ · 8 Theo lô · 9 Combo · 10 Nhiều ĐVT |
| `ProductStatus` | 1 Mới · 2 Đang bán · 3 Ngừng bán · 4 Hết hàng |
| `VatPercent` đặc biệt | `-1` không chịu thuế · `-2` không kê khai · `-100` thuế 0% |

Thực thể phụ: `Imei` (Serial, Status 11 giá trị, WarehouseId, BatchId, ImportBillId, SoldBillId, WarrantyUntil),
`Batch` (mã lô, NSX/HSD), `ComboComponent`, `PackageComponent`, `ProductTag`, `ProductSupplier`.

⚠️ **Bỏ** `ProductVariant` như thực thể bắt buộc. `CatalogAttribute` giữ để **lọc/hiển thị**,
không sinh SKU. Xem [02 §3.1](02-hien-trang-va-doi-chieu.md#31-variant-vs-cha–con--đvt).

### 3.2 Inventory — Kho

```
StockBalance  (khoá: TenantId + WarehouseId + ProductId)   ← ẢNH CHỤP, suy ra từ sổ cái
├─ Remain            tồn thực
├─ Transferring      đang chuyển kho
├─ Shipping          đã xuất cho HVC, chưa giao xong
├─ Holding           đơn chưa xuất kho giữ chỗ
├─ Damaged           hàng lỗi
├─ Warranty          đang bảo hành
├─ WarrantyHolding   tạm giữ linh kiện
├─ PreOrder          đặt trước
├─ WaitingInbound    chờ nhập
├─ RowVersion        chống ghi đè
└─ (computed) InStock = Remain − Transferring
             Available = InStock − Holding − Damaged
```

```
StockBill (chứng từ kho — thay StockDocument)
├─ Type: 1 Nhập | 2 Xuất
├─ Mode: 19 giá trị (bán lẻ, bán sỉ, nhập NCC, xuất NCC, chuyển kho, kiểm kho, quà tặng,
│        hàng lỗi, bảo hành, combo, khác…)
├─ Status: Draft | Posted | Cancelled      (chỉ Post mới sinh sổ cái)
├─ WarehouseId, CounterpartyId?, RefType/RefId? (Order/RetailBill/Transfer…)
├─ Lines[]: ProductId, UnitId, Qty, UnitPrice, VatPercent, Discount, BatchId?, ImeiSerials[]
└─ Totals: Amount, Discount, VatAmount, PaidAmount
```
> 🔧 **Ghi chú thi công (02/11/2026):** `StockBill` được hiện thực bằng cách **mở rộng
> `StockDocument`/`StockDocumentLine` hiện có** (giữ bảng `InvStockDocuments`), thêm `Direction` + `Mode`
> — **không** tạo bảng song song. Lý do: giữ nguyên 100% test sổ cái, giảm rủi ro migration.
> Xem [07 §WP-04](07-ke-hoach-trien-khai.md).

Bảng phụ: `StockTransfer` (+ trạng thái duyệt), `Stocktake` (3 loại + dòng lệch), `StockRequirement`
(phiếu nháp 6 loại), `StockLimit` (min/max), `BinLocation`/`StockPosition`, `ExpiryAlert`.

### 3.3 Sales — Đơn hàng

Giữ **4 trục** (tài sản hiện có), mở rộng giá trị:

| Trục | Enum | Giá trị |
|---|---|---|
| Thực hiện | `FulfillmentStatus` | **18** (Mới → … → Đã hoàn) theo bảng Nhanh |
| Thanh toán | `PaymentStatus` | Unpaid · Partial · Paid · Refunded |
| Đối soát | `ReconciliationStatus` | NotRequired · Awaiting · Matched · Discrepancy |
| Kết thúc | `ReturnStatus` | Open · PartiallyReturned · Returned · Cancelled |

Trường bổ sung trên `Order`: `Type` (13), `Source` (12), `CarrierCode`, `TrackingNumber`,
`CodAmount`, `ShippingFeeCustomer`, `ShippingFeeActual`, `WeightGram`, `PackageSize`,
`WarehouseId`, `SaleStaffId`, `PackerId`, `Labels[]`, `ExternalOrderId`, `ReasonCode` (24 lý do).

Bảng phụ: `OrderHistory` (đã có) + `ReasonCode`, `OrderComplaint`, `OrderHandover` (biên bản bàn giao),
`OrderPackage`, `OrderLabel`.

### 3.4 Pos — Bán lẻ tại quầy (mới)

```
CashSession (kết ca)
├─ StoreId, ShiftCode, OpenedBy/At, ClosedBy/At
├─ OpeningCash, ExpectedCash, ClosingCash, Variance, Status

RetailBill (hóa đơn bán lẻ)   ← aggregate riêng, KHÔNG phải Order
├─ Number, StoreId, CashSessionId, CustomerId?, CashierId
├─ Lines[] (ProductId, UnitId, Qty, UnitPrice, LineDiscount, VatPercent, GiftOfLineId?, ImeiSerials[])
├─ BillDiscount (chỉ 1 trong 2: chiết khấu dòng HOẶC chiết khấu bill — luật Nhanh)
├─ Payments[] (Method: cash|transfer|card|point|installment, Amount, AccountId?, RefNo?)
├─ PointsUsed, PointsEarned, TotalAmount, PaidAmount, DebtAmount
├─ Status: Draft | Completed | Voided | Returned
├─ ClientBillId (GUID do POS sinh — chống trùng khi offline sync)
└─ PrintedCount, Note
```

### 3.5 Ánh xạ hiển thị: 4 trục → 1 nhãn Nhanh

UI chỉ hiện **một** nhãn (để giống Nhanh), nhưng DB giữ 4 trục. Quy tắc suy ra (thứ tự ưu tiên):

| Nhãn hiển thị | Điều kiện |
|---|---|
| Đã huỷ | `ReturnStatus = Cancelled` |
| Đã hoàn | `ReturnStatus` ∈ Returned **và** `FulfillmentStatus = Returned` |
| Đang hoàn | `FulfillmentStatus` ∈ {Returning, ReturnConfirmed} |
| Thất bại | `FulfillmentStatus = DeliveryFailed` |
| Thành công | `FulfillmentStatus = Delivered` **và** `ReturnStatus = Open` |
| Đang chuyển | `FulfillmentStatus` ∈ {WaitingPickup, Shipping} |
| Đã đóng gói | `FulfillmentStatus = Packed` |
| Đang đóng gói | `FulfillmentStatus = Packing` |
| Đã xác nhận | `FulfillmentStatus = Confirmed` |
| Chờ/Đang xác nhận | `FulfillmentStatus` ∈ {PendingConfirm, Confirming} |
| Hết hàng | `FulfillmentStatus = OutOfStock` |
| Mới | còn lại |

### 3.6 Customers

`Customer` thêm: `Type` (1 lẻ · 2 buôn · 3 đại lý), `GroupId`, `LevelId`, `Points`, `AccumulatedAmount`,
`DebtLimit`, `DebtBalance`, `Birthday`, `SaleStaffId`, `ReferrerId`, `TaxCode`, `CompanyName`, `CardCode`.
`CustomerLevel` (ngưỡng tích luỹ → cấp), `CustomerCare` (log chăm sóc 5 loại), `CustomerCard`.

Công thức (theo Nhanh, dùng cho báo cáo & hiển thị):
```
Tổng SP đã mua   = Σ SL hóa đơn mua − Σ SL hóa đơn trả
Tiền tích luỹ    = Σ tiền mua − Σ tiền trả − Σ tiền dùng điểm
Ngày mua cuối    chỉ tính hóa đơn mua
```

### 3.7 Promotions

```
PromotionBatch (đợt: thời gian, kênh, cửa hàng, trạng thái)
├─ DiscountProgram[]  (6 loại: theo SP / nhóm / bill / số lượng / khách / combo)
│   └─ ConditionNode (cây AND/OR: nhóm KH, cấp độ, giờ, kênh, SL, giá trị…)
├─ GiftProgram[]      (mua X tặng Y, quà theo bill/dòng)
├─ PointProgram       (tỷ lệ tích điểm, quy đổi điểm → tiền)
├─ CouponBatch → Coupon (code unique, valueType tiền/%, maxUse, totalAssign/totalUsed)
│                └─ CouponProduct (rỗng = mọi SP)
└─ CommissionRule     (hoa hồng NV/CTV)
```

**Engine** (`IPromotionEngine`) trả `Adjustment[]` có `ProgramId`, `Label`, `Amount`, `Target(line|bill)`.
Bắt buộc: **deterministic** (thứ tự ưu tiên rõ), **giải thích được** (lưu lý do vào bill — cần cho
báo cáo & khiếu nại), **không tính lại lịch sử** (bảng giá snapshot tại thời điểm bán).

### 3.8 Accounting

```
Account (TT200/TT133): Code, Name, Type(1..6), ParentId, IsCashAccount, WarehouseId?
JournalEntry: Date, Type(12), Mode(33), Description, RefType, RefId, ContactType/Id, PeriodId
JournalLine: AccountId, Debit, Credit, WarehouseId?, CostCenter?
CashTransaction: Receipt|Payment, Method(cash|bank|card), Amount, AccountId, ContactId, RefId
Receivable/Payable: ContactType/Id, Opening, Debit, Credit, Closing, DueDate, AgingBucket
AccountingPeriod: Year, Month, Status(Open|Closed)
AutoPostingRule: nghiệp vụ → bộ định khoản (13 nghiệp vụ theo bảng Nhanh)
```

Nguyên tắc: **mọi bút toán sinh từ chứng từ** (bill/order/payment/return), không nhập tay tùy tiện;
bút toán tay phải có `Reason` + quyền `Accounting.Journal.Manual`.

### 3.9 Reporting

Read-model tách schema `report`:
`FactSalesLine` (ngày, kênh, cửa hàng, SP, SL, doanh thu, giảm giá, giá vốn, lợi nhuận),
`FactInventorySnapshot` (ngày, kho, SP, 9 cột tồn), `FactCashflow`, `DimDate/DimProduct/DimStore/DimStaff/DimChannel`,
`AggDailySales`, `AggDailyInventory`.

Job tổng hợp: watermark + upsert theo ngày; backfill theo khoảng. Báo cáo **đọc read-model**, không join
bảng giao dịch.

### 3.10 Settings / Store

`Store` (chi nhánh, có `WarehouseId` mặc định), `Department`, `Staff` (gắn `IdentityUser`),
`RoleDataScope` (own | store | all), `PrintTemplate` (HTML + placeholder + version), `Label`,
`PaymentAccount`, `ShippingAccount`, `NumberSequence` (số đơn/hóa đơn theo mẫu), `BusinessSetting` (khoá–giá trị có schema).

---

## 4. Bút toán kho: luật ghi sổ (đặc tả để test)

| Sự kiện | Sổ cái | Tồn ảnh hưởng |
|---|---|---|
| Tạo đơn (chưa xuất) | `PreOrderHold`/`OrderHold` | Holding + |
| Xác nhận & xuất kho | `SaleOut` | Remain −, Holding − |
| Gửi HVC | `ShippingOut` | Shipping + |
| Giao thành công | — (chỉ chuyển trạng thái) | Shipping −, Remain đã trừ từ trước |
| Giao thất bại → hoàn | `ShippingReturn` | Shipping −, Available + |
| Khách trả hàng | `ReturnIn` | Remain + (hoặc Damaged + nếu hàng lỗi) |
| Chuyển kho | `TransferOut` → `TransferIn` | Transferring ± |
| Kiểm kho | `StocktakeGain/Loss` | Remain ± |
| Hàng lỗi | `DamagedIn/Out` | Damaged ±, Remain ∓ |
| Bảo hành | `WarrantyHold/Release` | Warranty ± |
| Bán lẻ tại quầy | `RetailSaleOut` | Remain − |

Bất biến (đã có test, giữ và mở rộng):
`Remain ≥ 0` · `Holding ≥ 0` · `Available ≥ 0` khi bán · tổng sổ cái = ảnh chụp · mỗi chứng từ Post **một lần**
(idempotent theo `RefType+RefId+MovementType`).

---

## 5. Hợp đồng API cho Web Admin

### 5.1 Quy ước

| Hạng mục | Quy ước |
|---|---|
| Base | `/api/v1` (controller tường minh cho admin) + giữ `/api/app/*` của ABP cho tích hợp cũ |
| Định dạng | JSON camelCase; tiền **number** (VND, không thập phân) hoặc **string** nếu > 2^53? → dùng `number` (VND < 10^15 an toàn) |
| Phân trang | `?page=1&pageSize=50` → `{ items, total, page, pageSize }` |
| Lọc | `?filter[status]=2,3&filter[warehouseId]=...&q=...` |
| Sắp xếp | `?sort=-createdAt,name` |
| Lỗi | RFC 7807 + `{ code, message, details[], traceId }`; mã lỗi dạng `catalog.product.duplicate_barcode` |
| Chống trùng ghi | header `Idempotency-Key` cho POST tạo chứng từ |
| Chống ghi đè | `ETag`/`If-Match` với `RowVersion`; 409 khi lệch |
| Quyền | `[Authorize(Permission)]`; claim `permissions` trả về khi login để UI ẩn/hiện |
| Phạm vi dữ liệu | claim `data_scope` + `store_ids`; server lọc, **không tin client** |

### 5.2 Bề mặt endpoint P0 (rút gọn)

```
# Hàng hóa
GET    /products                     POST /products            GET|PATCH /products/{id}
POST   /products/{id}/units          POST /products/import     GET  /products/export
GET    /products/lookup?q=&barcode=  # POS dùng, trả kèm tồn + giá theo cửa hàng

# Kho
GET    /stock/balances?warehouseId=  GET  /stock/ledger?productId=&from=&to=
GET    /stock/bills                  POST /stock/bills         POST /stock/bills/{id}/post
POST   /stock/bills/{id}/cancel      GET  /stock/bills/{id}/print
GET    /stocktakes                   POST /stocktakes          POST /stocktakes/{id}/post
GET    /transfers                    POST /transfers           POST /transfers/{id}/approve

# Đơn hàng
GET    /orders?tab=&status=&from=&to=  POST /orders             GET|PATCH /orders/{id}
POST   /orders/{id}/transitions      { to, reasonCode?, note? }
POST   /orders/{id}/handover         POST /orders/{id}/complaints
POST   /orders/merge                 POST /orders/import-status

# POS
GET    /pos/sessions/current         POST /pos/sessions/open    POST /pos/sessions/{id}/close
POST   /pos/bills                    # Idempotency-Key = ClientBillId
POST   /pos/bills/{id}/void          POST /pos/bills/{id}/print
GET    /pos/bills?cashSessionId=

# Khách hàng / khuyến mại
GET|POST /customers                  GET /customers/{id}/timeline
GET|POST /promotions/batches         POST /promotions/coupons  POST /promotions/evaluate
GET|POST /price-lists

# Kế toán / báo cáo
GET    /accounting/accounts          POST /accounting/journal-entries
GET    /accounting/debts?contactType=
GET    /reports/revenue?groupBy=day|store|product|staff&from=&to=
GET    /reports/inventory/summary    GET /reports/orders/funnel

# Cấu hình
GET|PATCH /settings/business         GET|POST /stores   GET|POST /print-templates
GET|POST  /staff                     GET /permissions
```

Mỗi endpoint P0 phải có: **permission**, **test hợp đồng**, **mô tả OpenAPI** (summary tiếng Việt).

---

## 6. Đa doanh nghiệp (quyết định lớn nhất về hạ tầng)

Nhanh là SaaS: một URL, nhiều `businessId`, token mang `depotIds`.

| Phương án | Mô tả | Chọn? |
|---|---|---|
| Single-tenant / deployment | Mỗi khách 1 DB + 1 domain | Cho bản nội bộ/khách lớn |
| **Multi-tenant single DB** (`TenantId` + data filter) | 1 DB, `TenantId` mọi bảng, resolve theo `businessId` | ✅ **mặc định** |
| Multi-tenant multi-DB | Tách DB theo tenant | Chỉ khi khách yêu cầu |

Việc cần làm: thêm `Volo.Abp.TenantManagement` (thư viện **miễn phí**; chỉ UI auto-migrate là PRO —
tự viết UI), `TenantResolver` đọc `businessId` từ route/claim, seed tenant mặc định, test cách ly dữ liệu
(bắt buộc có test "tenant A không thấy dữ liệu tenant B" — hiện chưa có).

---

## 7. POS offline (bắt buộc cho cửa hàng thật)

Nhanh lưu nháp trong **IndexedDB**. Ta cũng phải:

1. Client sinh `ClientBillId` (GUID) **trước** khi gọi API.
2. API `POST /pos/bills` idempotent theo `ClientBillId` → mất mạng gửi lại không tạo trùng.
3. Hàng đợi IndexedDB + service worker; đồng bộ khi có mạng; hiển thị trạng thái "chưa đồng bộ".
4. Xung đột tồn/giá: server **không** từ chối hoá đơn đã in; ghi `PriceVariance`/`StockVariance`
   vào báo cáo để chủ shop xử lý (đúng triết lý "tính lại" của Nhanh).
5. In hoá đơn từ mẫu local (không phụ thuộc mạng).

---

## 8. Hiệu năng & chỉ mục

| Bảng | Index bắt buộc |
|---|---|
| `Product` | `(TenantId, Code)` unique · `(TenantId, ParentId)` · GIN trên `Name` (trigram) |
| `ProductUnit` | `(TenantId, Barcode)` unique |
| `StockBalance` | `(TenantId, WarehouseId, ProductId)` unique · `(TenantId, ProductId)` |
| `StockLedger` | `(TenantId, ProductId, OccurredAt)` · `(TenantId, RefType, RefId)` |
| `Order` | `(TenantId, Number)` unique · `(TenantId, FulfillmentStatus, CreatedAt)` · `(TenantId, ExternalOrderId)` unique filter |
| `RetailBill` | `(TenantId, ClientBillId)` unique · `(TenantId, StoreId, CreatedAt)` |
| `JournalLine` | `(AccountId, EntryDate)` · `(ContactType, ContactId)` |
| `FactSalesLine` | `(DateKey, StoreId, ProductId)` |

Khác: keyset pagination cho `StockLedger`/`Order` khi > 1 triệu dòng; cache reference (product/price theo
store) bằng Redis + ETag; **không** cache tồn kho.

---

## 9. Nhập dữ liệu từ Nhanh (nếu cần chuyển đổi)

Open API v3 đủ để migrate: **68 endpoint**. Thứ tự an toàn:

1. `business/depot` + `business/user` → Store/Staff.
2. `product/category`, `brand`, `unit`, `supplier` → danh mục.
3. `product/list` (phân trang `next` object) → Product + Unit + tồn ban đầu (tạo **phiếu nhập đầu kỳ**,
   không UPDATE tồn trực tiếp).
4. `customer/list` → Customer + điểm/công nợ.
5. `bill/list` 30–90 ngày gần nhất → chứng từ kho (để có giá vốn).
6. `order/list` giới hạn **31 ngày**/lần → đơn đang mở.
7. Bật webhook 10 event để chạy song song trong giai đoạn chuyển đổi.

⚠️ Token v3 **không refresh**, sống 1 năm, mang cứng `depotIds` + `permissions` → phải tạo token
theo từng business và **ghi lại ngày hết hạn**.

---

## 10. Những thứ **không** làm (để bảo vệ tiến độ)

| Không làm | Vì sao |
|---|---|
| 90 báo cáo | Chỉ 12 báo cáo P0/P1; còn lại theo yêu cầu |
| Website builder / CMS | Ngoài lõi QLBH; dùng Next.js storefront riêng sau |
| Affiliate, Zalo OA, SMS marketing | Tích hợp sau, không chặn P0 |
| Đa tiền tệ, đa ngôn ngữ đầy đủ | Việt Nam + VND; i18n chỉ để mở đường |
| Đồng bộ 2 chiều sàn ngay | P1: pull đơn + push tồn là đủ |
| Microservice | Modular monolith đủ tới ~50k đơn/ngày; tách sau |
