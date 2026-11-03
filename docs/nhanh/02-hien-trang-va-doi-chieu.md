# 02 — Hiện trạng `eCommerce` và đối chiếu với Nhanh.vn

> Mục tiêu: trả lời **"giữ được bao nhiêu, phải viết lại bao nhiêu"** trước khi cam kết lộ trình.
> Số liệu lấy từ cây project thật (57 `.csproj`), migration EF, và 55 tài liệu thiết kế trong `docs/features/`.

---

## 1. Hiện trạng đo được

| Hạng mục | Giá trị | Bằng chứng |
|---|---|---|
| Project .NET | **57** `.csproj` (13 module nghiệp vụ + host + shared + test) | `Get-ChildItem -Filter *.csproj` |
| SDK | .NET **10.0.400** | `dotnet --list-sdks` |
| Framework | ABP **10.7**, modular monolith, EF Core + PostgreSQL | `Directory.Packages.props` |
| Migration | **50** migration (từ `Initial` 09/10 đến `Xc002_DlqReplay` 29/10) | `src/eCommerce.EntityFrameworkCore/Migrations/` |
| Test | **1.634 unit + 90 integration** (baseline `S19`, 29/10/2026). ⚠️ README §5 còn ghi *"388 + 20"* — số cũ | `docs/ban-giao-s19-va-con-lai-2026-10-29.md` §1 |
| Tài liệu thiết kế | **55** feature doc (16 mục/mẫu), **27** đã Approved | `docs/features/README.md` |
| UI hiện tại | **22 trang Razor MVC** + menu 2 tầng **13 mục** (8 cấp 1 + 5 mục con) | `src/eCommerce.Web/Views/`, `Navigation/MenuDieuHuong.cs` |
| Auth | OpenIddict (authorization code + client credentials), ABP Identity/Permission | `HttpApi.Host` |

### 1.1 Module **có code thật**

| Module | Thực thể chính | Năng lực thật |
|---|---|---|
| `Catalog` | `Product`, `ProductVariant`, `Category`, `Brand`, `CatalogAttribute`, `ProductMedia`, `ProductChannelVisibility` | Cây danh mục, thuộc tính sinh biến thể, slug/SEO, media, ẩn/hiện theo kênh |
| `Pricing` | `PriceList`, `PriceListItem`, `TierPriceSet`, `CostPrice`, `PriceCalculationRecord` | Bảng giá, giá bậc thang, giá vốn, ghi vết tính giá |
| `Inventory` | `Warehouse`, `StockDocument`, `StockLedger`, `StockReservation`, `StockAllotment`, `InvChannelLimit` | Sổ cái append-only, giữ chỗ TTL, phân bổ tồn theo kênh, hạn mức |
| `Sales` | `Order` (4 trục), `OrderLine`, `OrderStateMachine`, `OrderStatusHistory`, `OrderReturn`, `MarketplaceOrderIntake`, `MarketplaceEventLog` | Checkout, state machine 4 trục, tiếp nhận đơn sàn idempotent, trả hàng |
| `Customers` | `Customer`, `CustomerGroup`, `Address`, `CustomerDataExportLog` | Hồ sơ, nhóm, sổ địa chỉ, che PII, log xuất dữ liệu |
| `Address` | `AdministrativeUnit`, `CarrierAddressMapping`, `DiaChiCanonical` | Đơn vị hành chính 2 cấp, map mã địa chỉ theo hãng VC |
| `EInvoice` | `EInvoice`, bộ cấp số, retry, parser phản hồi CQT | Phát hành/điều chỉnh/thay thế, gửi CQT |
| `Signing` | `SigningCertificate`, `SigningRequest` | Ký 2 bước thủ công + chứng thư số |
| `Tax` | `TaxRateSchedule`, `TaxClassification`, `Mau01CsvExporter` | Thuế GTGT, tờ khai 01/GTGT |
| `Reconciliation` | `ReconRun`, `DoiSoatTonKho`, `PaymentReconciliationRun/Line` | Đối soát tồn & thanh toán, retention |
| `PaymentHub` | `PaymentTransaction`, `PaymentRefund`, `VnPayPaymentProvider` | VNPay ký + IPN + hoàn tiền |
| `ChannelHub` | `Channel`, `ChannelShop`, `ChannelListing`, `ChannelTokenManager`, supervisor worker | Token mã hoá, lock refresh, worker động theo shop |
| `Messaging` | `DeadLetterMessage`, `IdempotencyRecord`, outbox/inbox | DLQ + replay, idempotency |

### 1.2 **Chỉ có tài liệu, KHÔNG có code** (dễ tưởng nhầm là đã có)

`Promotions` (`PRO-001/002`), `Shipping` (`SHP-001…004`), `Reporting` (`REP-001…003`),
`Loyalty` (`LOY-001`), `Reviews` (`REV-001`), `Notifications` (`NOT-001/002`),
`Accounting export` (`ACC-001`) — **7 nhóm, 0 dòng code**.

⚠️ Đây là điểm phải nói rõ với người lập kế hoạch: **"đã có tài liệu" không phải "đã có code"**.

### 1.3 UI hiện tại (Razor MVC) — 13 mục menu

`Đơn hàng · Phân bổ tồn theo kênh · Sản phẩm · Thương hiệu · Đối soát tồn kho · Kho · Bảng giá ·
Vận hành (Hạn mức kênh · Ngân sách tồn · Đối soát thanh toán · Giá vốn)`

So với Nhanh **180 mục menu / 204 route**: UI hiện tại phủ **~7%** bề mặt vận hành.

---

## 2. Ma trận đối chiếu (module Nhanh ↔ hiện trạng ↔ quyết định)

Ký hiệu: 🟢 giữ và mở rộng · 🟡 cần tái cấu trúc · 🔴 viết mới · ⚫ không làm (P0–P2)

| # | Module Nhanh | Hiện trạng | Quyết định | Ghi chú thi công |
|---|---|---|---|---|
| 1 | **Hàng hóa** (`/product/*`) | `Catalog` với Product+Variant | 🟡 | Bỏ mô hình "variant sinh từ thuộc tính" làm **trung tâm**; thêm `Type` (10), `Units[]`, `Barcodes[]`, `ParentId`, `Imei[]`, `BatchId`, `VatMode`; giữ `Category`/`Brand`/`Media`/`Slug` |
| 2 | **Kho** (`/inventory/*`) | `Inventory` sổ cái + phiếu | 🟡 | `StockDocument` → `StockBill` với **19 mode**; thêm `StockBalance` vector 9 chiều; thêm kiểm kho 3 loại, chuyển kho có duyệt, phiếu nháp, hàng lỗi, hạn mức min/max, lô/hạn dùng, vị trí kho |
| 3 | **Bán lẻ / POS** (`/pos/*`) | ❌ không có | 🔴 | **Aggregate mới** `RetailBill` + phiên quầy (`CashSession`), đa tab, F1–F10, thanh toán đa phương thức, trả góp |
| 4 | **Đơn hàng** (`/order/*`) | `Order` 4 trục | 🟡 | Mở rộng trục thực hiện thành **18 trạng thái Nhanh**, thêm `Type` 13, `Carrier`, `CodAmount`, `Labels`, `Packing`, `Handover`, `Complaint`, `PartialReturn` |
| 5 | **Khách hàng** (`/customer/*`) | `Customer` + group | 🟡 | Thêm `Type` (lẻ/buôn/đại lý), `Level` (cấp độ tự động), `Points`, `AccumulatedAmount`, `DebtLimit`, `SaleId`, `ReferrerId` |
| 6 | **Khuyến mại** (`/promotion/*`) | ❌ (chỉ tài liệu) | 🔴 | Batch→Coupon, chiết khấu 6 loại, quà tặng, tích điểm, bảng giá, hoa hồng; **engine tự viết** |
| 7 | **Kế toán** (menu ẩn với business 230222) | ❌ | 🔴 | `Account`, `JournalEntry`, `CashTransaction`, `Receivable/Payable`, kỳ kế toán, bảng định khoản 13 nghiệp vụ |
| 8 | **Báo cáo** (`/report/*`) | ❌ | 🔴 | Read-model + 12 báo cáo P0/P1; không làm 90 báo cáo |
| 9 | **Cài đặt / cửa hàng** (`/setting`, `/store`) | ⚠️ ABP Identity + setting rời rạc | 🟡 | `Store/Branch`, `Department`, `Staff` + **phân quyền xem dữ liệu** (own-only), `PrintTemplate`, `Label`, tài khoản thanh toán, cấu hình VC |
| 10 | **Kênh sàn** (`/ecommerce/*`) | `ChannelHub` + Shopee signing | 🟢 | Đã có khung tốt (token lock, worker động); cần pull order/listing thật + mapping SP sàn |
| 11 | **Hóa đơn điện tử** (`/invoice/*`) | `EInvoice` + `Signing` + `Tax` | 🟢 | Đã có; cần UI 4 tab + danh mục 11 lỗi |
| 12 | **Vận chuyển** (`/shipping/*`) | ❌ (chỉ tài liệu) | 🔴 | Adapter GHN/GHTK/VTP/J&T…, biên bản bàn giao, đối soát COD |
| 13 | **Bảo hành** (`/warranty/*` — có trong Nhanh) | ❌ | 🔴 (P2) | Tiếp nhận, trạng thái, trung tâm BH, BH mở rộng |
| 14 | **Nhà cung cấp** (`/supplier/*`) | ❌ | 🔴 (P1) | NCC + công nợ 331 + phiếu nhập NCC |
| 15 | **Website** (`/website/*`) | Storefront tối giản | ⚫ (P2) | Không làm CMS/builder ở giai đoạn này |
| 16 | **Affiliate / Zalo / SMS** | ❌ | ⚫ (P2) | Tích hợp sau |

### 2.1 Tổng hợp mức độ phủ

| Mức | Số module | Ghi chú |
|---|---:|---|
| 🟢 Giữ/mở rộng | 3 | ChannelHub, EInvoice+Signing+Tax, PaymentHub/Reconciliation |
| 🟡 Tái cấu trúc | 5 | Catalog, Inventory, Sales, Customers, Settings |
| 🔴 Viết mới | 6 | POS, Promotion, Accounting, Reporting, Shipping, Supplier (+Warranty P2) |
| ⚫ Không làm giai đoạn này | 2 | Website builder, Affiliate/Zalo |

**Ước lượng:** tái dùng được **~40% domain hiện có** (catalog lõi, sổ cái kho, state machine, đối soát,
hạ tầng outbox/idempotency, ABP auth/permission). **60% còn lại là xây mới hoặc đổi mô hình.**

---

## 3. Bốn xung đột mô hình phải quyết trước khi viết dòng code nào

### 3.1 "Variant" vs "cha–con + ĐVT"

Hiện tại: `Product` → `ProductVariant` sinh từ `CatalogAttribute` (màu/size), mỗi variant có SKU/giá/tồn.
Nhanh: **không có variant**; có SP cha (không tồn) + SP con (có tồn), và **ĐVT** là chiều độc lập
(1 thùng = 24 lon, giá riêng).

✅ **Quyết định đề xuất:** giữ `Product` làm gốc, **bỏ `ProductVariant` như khái niệm bắt buộc**;
thay bằng:
- `Product.ParentId` (cha–con, tự tham chiếu) — dùng cho "sản phẩm chia thuộc tính".
- `ProductUnit[]` (ĐVT + hệ số + giá + mã vạch) — dùng cho "nhiều ĐVT".
- `Product.AttributeValues` (chỉ để hiển thị/lọc, **không sinh SKU**).

Lý do: nếu giữ variant, mọi màn hình kho/IMEI/lô sẽ phải join 2 tầng và **không map được** sang Nhanh
(hoặc ngược lại, không import được dữ liệu Nhanh). Chi phí migration: đổi `OrderLine.VariantId` →
`ProductId + UnitId`; test hiện có phải sửa theo.

### 3.2 "Tồn = 1 số" vs "tồn = vector"

Hiện tại `StockLedger` (append-only) + `InventoryItem` (ảnh chụp OnHand/Reserved) — **đúng hướng**.
Nhanh cần thêm **đang chuyển, đang giao, lỗi, bảo hành, đặt trước, chờ nhập**.

✅ **Quyết định:** giữ sổ cái, **mở rộng `StockBalance`** thành 9 cột (mục 03 §3.2), mọi cột đều
**suy ra từ sổ cái** (không UPDATE tay). Thêm bút toán `ShippingOut`, `ShippingReturn`, `DamagedIn/Out`,
`WarrantyHold/Release`, `PreOrderHold`.

### 3.3 "OrderStatus 7 giá trị" vs "18 trạng thái"

Hiện tại đã có **4 trục** (Fulfillment/Payment/Reconciliation/Return) — **tốt hơn** Nhanh về mặt mô hình.
Vấn đề: `FulfillmentStatus` chỉ có 6 giá trị, thiếu giai đoạn đóng gói/thu gom/hoàn.

✅ **Quyết định:** **giữ 4 trục** (đây là tài sản, đừng bỏ), mở rộng `FulfillmentStatus` lên **18 giá trị
Nhanh** + thêm `Reason` (24 lý do) + `CarrierStatus`. UI hiển thị **một nhãn duy nhất** suy ra từ 4 trục
(map ở mục 03 §3.5) để người dùng thấy giống Nhanh.

### 3.4 "Website-first" vs "POS-first"

Hiện tại `CheckoutAppService`, `DatHangController` phục vụ đặt hàng online; **không có POS**.

✅ **Quyết định:** thêm bounded context `Pos` (RetailBill + CashSession) **song song** `Sales` (Order).
Hai aggregate **dùng chung** `OrderLine` value object + cùng post vào `Inventory` + `Accounting`,
nhưng **không gộp** — vì vòng đời khác nhau (hóa đơn quầy là *tức thời, đã thu tiền*; đơn giao vận
là *nhiều ngày, nhiều trạng thái*).

---

## 4. Những thứ **giữ nguyên, không đụng**

| Tài sản | Vì sao giữ |
|---|---|
| ABP + OpenIddict + Permission | Đã kiểm chứng 401/403/200, có seed, có UI login |
| Sổ cái kho append-only + unique index chống trùng | Đúng bản chất, đã có test bất biến |
| Outbox/Inbox + Idempotency + DLQ | Cần cho webhook sàn & POS offline sync |
| `OrderStateMachine` 4 trục + `RowVersion` | Tốt hơn Nhanh, đã có test |
| `ChannelTokenManager` (lock + double-check) | Bài học đắt, đừng viết lại |
| `VnPayPaymentProvider` + IPN verify | Đã test chữ ký/số tiền |
| `EInvoice` + `Signing` + `Tax` + `Address` | Nghiệp vụ VN khó, đã có |
| EF migration + DbMigrator + seed | Hạ tầng ổn |
| 388 unit test | Lưới an toàn khi refactor |

---

## 5. Nợ kỹ thuật phải xử lý **trước** khi mở rộng

| # | Nợ | Ảnh hưởng | Việc cần làm |
|---|---|---|---|
| 1 | **Chưa bật multi-tenant** (entity có `IMultiTenant`, chưa có `TenantManagement`) | Không chạy được SaaS nhiều doanh nghiệp như Nhanh | Quyết định: **single-tenant per deployment** hay **multi-tenant**; nếu multi → thêm `Volo.TenantManagement` + resolve `businessId` |
| 2 | Naming trộn Việt/Anh (`ChuyenThucHien`, `DonHangAppService` cạnh `Order`) | Khó tuyển người, khó gen client | Chốt quy ước: **API/DTO tiếng Anh**, domain method tiếng Việt được phép; không rename hàng loạt |
| 3 | UI Razor 22 view sẽ **chết dần** khi có Next.js | Hai nguồn UI, dễ lệch | Đóng băng Razor sau khi Next.js phủ P0; chỉ giữ storefront |
| 4 | Không có API contract xuất bản (OpenAPI tĩnh) | Next.js phải chép tay type | Bật NSwag + `openapi.json` trong CI, sinh type tự động |
| 5 | Báo cáo chưa có read-model | Query báo cáo sẽ chậm khi dữ liệu lớn | Thêm schema `report` + job tổng hợp (đã có thiết kế `REP-001`) |
| 6 | Chưa có hàng đợi in / mẫu in | POS không in được | `PrintTemplate` + render server-side (xem 04) |

---

## 6. Kết luận

1. **Không nên viết lại từ đầu**: 40% domain + toàn bộ hạ tầng (auth, outbox, sổ cái, đối soát) đã tốt
   và có test. Viết lại = mất 6–9 tháng học lại chính mình.
2. **Nhưng cũng không thể "thêm màn hình"**: 4 xung đột mô hình ở §3 sẽ chặn mọi màn hình kho/đơn/POS
   nếu không xử lý trước.
3. **Đường đi đúng**: refactor 5 module 🟡 theo mô hình Nhanh → viết mới 6 module 🔴 theo thứ tự
   POS → Kho → Đơn → Khuyến mại → Kế toán → Báo cáo → Vận chuyển.
