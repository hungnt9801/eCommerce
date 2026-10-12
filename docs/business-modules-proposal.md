# Đề xuất xây dựng module nghiệp vụ — bản khung

> **Trạng thái:** khung đề xuất (do tôi thiết kế dựa trên hiện trạng solution + yêu cầu đa kênh VN).
> Phần **§6 "Nguồn tham chiếu & bài học từ các repo đã khảo sát"** đang chờ 6 nhánh khảo sát song song
> hoàn tất. Các kết luận về giấy phép ở §2 sẽ được đối chiếu lại với **file LICENSE thật** của từng repo.

---

## 1. Hiện trạng domain của solution (đã xác minh bằng code)

| Module | Entity | Trạng thái |
|---|---|---|
| **App.Domain** | `Product`, `ProductVariant` (decimal money), `InventoryItem` + `StockLedgerEntry` (append-only ledger), `Order` + `OrderLine` | ✅ có, còn **rất mỏng** |
| **ChannelHub** | `Channel`, `ChannelShop` (token mã hoá), `ChannelListing` (map SKU↔item sàn, `ChannelPrice`, `AllottedQuantity`) | ✅ có, đã chạy |
| **PaymentHub** | `PaymentTransaction` (idempotent, `IdempotencyKey`) | ✅ có, mới có VNPay |

**Khoảng trống nghiệp vụ lớn nhất:** không có Category/Brand/Attribute, không có Promotion/Voucher,
không có Cart/Checkout, không có Warehouse/Receipt/Transfer, không có Shipping/Carrier, không có
Tax/Invoice, không có Customer/Address, không có Return/RMA, không có đối soát.

---

## 2. Trục quyết định #1: GIẤY PHÉP quyết định nguồn nào được lấy code

Đây là ràng buộc cứng, phải chốt trước khi nói tới "xây module từ repo nào":

| Nguồn | Giấy phép (đã đối chiếu file LICENSE / metadata thật) | Được **copy code** vào sản phẩm đóng? |
|---|---|---|
| nopCommerce | NPL v4.0 = **AGPL-3.0 + "powered by nopCommerce" trên mọi màn hình** (có bán commercial license để thoát) | ❌ **Không** (viral + attribution) |
| Smartstore | **AGPL-3.0** (nguyên văn) | ❌ **Không** (kể cả chạy SaaS — AGPL §13) |
| GrandNode **2.x** (`grandnode/grandnode2`) | **GPL-3.0** | ❌ **Không**, và **không mua được để thoát** |
| GrandNode **1.x** (`grandnode/grandnode`, đã archive 2022) | nopCommerce Public License v3.0 | ❌ Không |
| Orchard Core / OrchardCore.Commerce | **BSD-3** | ✅ Được (kèm attribution) |
| ABP framework (mọi package `Volo.Abp.*`) | **LGPL-3.0-only** | ⚠️ Dùng dạng **thư viện/NuGet = an toàn** cho sản phẩm đóng |
| **ABP application modules** (Identity, Account, OpenIddict, Blogging, AuditLogging, CMS Kit…) | 🚨 **LGPL-3.0-only — KHÔNG phải MIT** (xem bằng chứng bên dưới) | ⚠️ Dùng **package** = an toàn · **COPY SOURCE vào sản phẩm đóng = rủi ro copyleft** |
| EasyAbp/EShop | **MIT** | ✅ **Được** — nguồn gần nhất với ta |
| SimplCommerce | Apache-2.0 | ✅ Được |
| dotnet/eShop | MIT | ✅ Được |

### 🚨 Điểm mâu thuẫn quan trọng: tài liệu ABP nói MIT, metadata nói LGPL-3.0

Tài liệu ABP (ví dụ `docs/en/modules/identity.md`, `openiddict.md`) ghi: *"The source code is licensed with
**MIT**, so you can freely use and customize it."* — **nhưng đó không phải giấy phép thực tế của gói**:

| Bằng chứng (tôi đọc trực tiếp) | Nội dung |
|---|---|
| `.../volo.abp.identity.domain/10.7.0/volo.abp.identity.domain.nuspec` | `<license type="expression">LGPL-3.0-only</license>` |
| `abpframework/abp/dev/common.props` | `<PackageLicenseExpression>LGPL-3.0-only</PackageLicenseExpression>` — **mọi module import file này** |
| `modules/<tên>/LICENSE` cho các module ứng dụng | **404** — không có LICENSE riêng ⇒ theo giấy phép repo (LGPL-3.0) |

**Hệ quả thực dụng:**
- ✅ **Tiếp tục dùng NuGet package** của module ABP (như Phase 0 đang làm) → an toàn cho sản phẩm đóng.
- ❌ **Không copy source module ABP** vào sản phẩm đóng mà không hỏi luật sư — kể cả lệnh
  `abp add-module <X> --with-source-code` hay `abp get-source` mà kế hoạch trước đây của tôi khuyến nghị.
  Đây là **quyết định pháp lý, không phải kỹ thuật** — cần sửa lại khuyến nghị ở `abp-modules-plan.md` §4.

**Hệ quả trực tiếp:** 4 nền tảng lớn nhất mà chúng ta đã khảo sát (nopCommerce, Smartstore, GrandNode 1 & 2)
**chỉ được dùng để HỌC THIẾT KẾ**, tuyệt đối không copy code. Ngược lại, **EasyAbp/EShop (MIT, ABP-native)**,
**Orchard Core Commerce (BSD)** và **dotnet/eShop (MIT)** là ba nguồn có thể **port code hợp pháp**.

> ⚖️ Nguyên tắc: bản quyền bảo vệ **biểu hiện** (code cụ thể), không bảo vệ **ý tưởng/phương pháp**.
> Học mô hình module, luồng nghiệp vụ, cách đặt trạng thái = hợp pháp; **dịch/sao chép code** = không.
> ⚠️ Ranh giới này cần luật sư xác nhận trước khi bàn giao sản phẩm cho khách.

---

## 3. Bản đồ module nghiệp vụ đề xuất

Thiết kế theo **bounded context**, mỗi module sở hữu entity của mình và giao tiếp qua event
(giữ được đường tách microservice, giống cách `ChannelHub` đang làm).

### Nhóm A — Nền tảng bán hàng (cần cho mọi kênh)

| # | Module | Nội dung chính | Ưu tiên |
|---|---|---|---|
| **A1** | **Catalog** | `Category` (cây, có path/level để query nhanh), `Brand`, `ProductCategory`, `ProductAttribute` + `AttributeOption` + `ProductAttributeMapping` (biến thể sinh từ tổ hợp thuộc tính), `ProductMedia`, `ProductTag`, SEO slug, trạng thái xuất bản, `ProductChannelVisibility` (sản phẩm hiển thị ở kênh nào) | 🔴 P1 |
| **A2** | **Pricing** | `PriceList` (theo kênh + khoảng thời gian), giá niêm yết trên variant, `ChannelPrice` (đã có trên `ChannelListing`), `TierPrice` (giá sỉ theo số lượng), `CostPrice` (để tính lãi gộp), `Currency` + tỷ giá | 🔴 P1 |
| **A3** | **Promotions** | `Voucher`/`Coupon` (mã, điều kiện áp dụng, giới hạn số lần, thời gian), **discount rule engine** (%, số tiền, mua X tặng Y, theo nhóm khách), `FlashSale` (theo khung giờ), `Campaign`. **Bắt buộc tách engine khỏi Order** để tính lại được | 🟠 P2 |
| **A4** | **Inventory** | `Warehouse`, `StockItem`, `StockLedgerEntry` (đã có), `StockReservation` (**giữ chỗ có TTL** — chống oversell), `StockAllotment` theo kênh (đã có `ChannelListing.AllottedQuantity`), `GoodsReceipt`/`GoodsTransfer` (phiếu nhập/chuyển), `InventoryCount` (kiểm kê + điều chỉnh) | 🔴 P1 |
| **A5** | **Cart & Checkout** | `Cart` + `CartItem` (theo customer/tenant/phiên, hỗ trợ guest), **tính lại giá tại checkout** (không bao giờ dùng giá đã lưu trong cart), `CheckoutSession`, chọn địa chỉ giao, phương thức giao/thanh toán, `OrderPlacementService` (điểm duy nhất tạo đơn + giữ kho) | 🔴 P1 |
| **A6** | **Orders** | `Order` + `OrderLine` (đã có), **state machine** trạng thái đơn (ai được chuyển trạng thái nào), `OrderHistory`, `OrderReturn`/RMA, `Refund`, ghi chú nội bộ, idempotency theo `ExternalOrderId` (đã có unique index) | 🔴 P1 (luồng cơ bản) / 🟠 P2 (RMA) |
| **A7** | **Customers** | `Customer` (gắn `IdentityUser` của ABP), `Address` (sổ địa chỉ), `CustomerGroup` (bán lẻ/bán sỉ ảnh hưởng giá), lịch sử mua, điểm thưởng (sau) | 🔴 P1 |

### Nhóm B — Hoàn tất đơn hàng & tuân thủ Việt Nam

| # | Module | Nội dung chính | Ưu tiên |
|---|---|---|---|
| **B0** | **AddressHub** | Mô hình địa chỉ **2 cấp mới** (tỉnh + xã, **bỏ huyện** từ 01/07/2025) **và** 3 cấp cũ; **bảng mã địa chỉ theo từng hãng vận chuyển** (là *dữ liệu đồng bộ*, không phải master data). 🔴 **P0 vì breaking change đang diễn ra** | 🔴 **P0** |
| **B1** | **Payments** (đã có `PaymentHub`) | Thêm provider MoMo/payOS/ZaloPay/SePay; **module ĐỐI SOÁT thanh toán** (job `querydr` định kỳ) — **bắt buộc** vì webhook có thể mất | 🔴 P1 |
| **B2** | **ShippingHub** | `Carrier` adapter (**GHN tốt nhất** — REST + sandbox + publish `llms.txt`; GHTK OK; 7 hãng còn lại phần lớn **không có tài liệu API**), `ShippingMethod`/`Service`, tính phí theo cân nặng/kích thước/địa chỉ, `Shipment` + mã vận đơn + webhook, **COD amount**, giao thất bại/giao lại | 🟠 P2 |
| **B3** | **EInvoiceHub + DigitalSigningHub + Tax** | Thuế GTGT; **hoá đơn điện tử** (NĐ 123/2020 **từ 01/07/2022** + **NĐ 70/2025 từ 01/06/2025**); ⚠️ **`DigitalSigningHub` là STATE MACHINE 2 bước, không phải hàm sync** (VNPT: xin Hash → gửi hash đã ký) ⇒ với token/USB **không tự động hoá 100% được** | 🔴 **P0 (phần pháp lý)** |
| **B4** | **Channel Settlement** (đối soát sàn) | Phí sàn theo đơn, tiền sàn phải trả, kỳ đối soát, chênh lệch, hoàn tiền/trả hàng của sàn | 🟠 P2 |
| **B5** | **CodReconciliation** | ⚠️ **KHÔNG có API đối soát** — GHN chỉ có API **đổi** COD; bảng kê là **file Excel tải THỦ CÔNG**, kỳ theo **"phiên"** (`mã phiên`). Phải thiết kế luồng **import file thủ công + đối soát** | 🟠 P2 |
| **B6** | **AccountingHub / InboundInvoiceHub** | MISA/Bravo/Fast **không công bố spec .NET**; Bravo chính thức hỗ trợ **Excel/CSV** | 🟡 P3 |

### Nhóm C — Điều hành & tăng trưởng (làm sau)

| # | Module | Nội dung | Ưu tiên |
|---|---|---|---|
| **C1** | **Reporting / BI** | Doanh thu, lãi gộp, tồn kho luân chuyển, hiệu suất theo kênh. **Dùng SQL read-model**, không chạy báo cáo nặng trên DB giao dịch | 🟡 P3 |
| **C2** | **Notifications** | Email/SMS/**Zalo OA** cho xác nhận đơn, giao hàng, hoàn tiền. Template + hàng đợi (dùng `BackgroundJobs` của ABP) | 🟡 P3 |
| **C3** | **Content/SEO** | CMS/Blog/landing page. **Cân nhắc dùng `Volo.CmsKit`/`Volo.Blogging` (MIT) thay vì tự viết** | 🟡 P3 |
| **C4** | **Loyalty / Reviews / Wishlist** | Điểm thưởng, đánh giá sản phẩm (có kiểm duyệt), danh sách yêu thích | 🟢 P4 |
| **C5** | **SaaS hoá** | `Volo.TenantManagement` (miễn phí) + tự viết phần cấp phát/migrate DB theo tenant, hoặc mua `Volo.Saas` (Pro) | 🟢 P4 |

---

## 4. Thứ tự xây đề xuất và lý do

```
P0 (PHÁP LÝ — phải làm cùng lúc với checkout, không phải "sau này")
   B0 AddressHub (địa chỉ 2 cấp, bỏ huyện từ 01/07/2025)
   B3 EInvoiceHub + DigitalSigningHub (NĐ 70/2025 đã hiệu lực 01/06/2025)

P1 (bán được trên website)
   A1 Catalog → A2 Pricing → A4 Inventory → A5 Cart/Checkout → A6 Orders → A7 Customers → B1 đối soát thanh toán

P2 (vận hành & tuân thủ VN)
   A3 Promotions → B2 ShippingHub(+COD) → B5 CodReconciliation → B4 Settlement → A6 RMA → A3/Voucher

P3 (điều hành)
   C1 Reporting → C2 Notifications → C3 CMS/SEO → B6 AccountingHub

P4 (tăng trưởng)
   C4 Loyalty/Reviews → C5 SaaS hoá
```

**Nguyên tắc thứ tự:**
1. **Địa chỉ + hoá đơn điện tử là P0 vì PHÁP LÝ, không vì kỹ thuật** — NĐ 70/2025 hiệu lực từ 01/06/2025,
   và việc bỏ cấp huyện (01/07/2025) làm **thay đổi mô hình dữ liệu địa chỉ**; làm sai từ đầu là phải migrate lại toàn bộ đơn hàng.
2. **Inventory + Checkout + Orders phải làm cùng nhau** — oversell và sai tiền là hai lỗi không thể sửa về sau.
3. **Pricing tách khỏi Catalog** ngay từ đầu, vì giá theo kênh/thời gian/giá sỉ sẽ phình nếu nhét vào Product.
4. **Promotions để sau Cart/Orders nhưng engine phải tách ngay khi làm.**
5. **Shipping/Tax/Invoice/COD là đặc thù VN** ⇒ **không có SDK .NET chính thức nào của bất kỳ nhà cung cấp VN nào**
   (đã kiểm chứng: chỉ 5 gói NuGet tồn tại cho cả 5 nhóm) ⇒ viết abstraction kiểu `IPaymentProvider` cho **từng** nhóm
   là **bắt buộc, không phải lựa chọn**.
6. Reporting tách hẳn sang read-model.

---

## 5. Nguyên tắc thiết kế áp dụng cho MỌI module nghiệp vụ

| # | Nguyên tắc | Vì sao |
|---|---|---|
| 1 | **Tiền luôn là `decimal`** | GrandNode 2 dùng `double` cho tiền — lỗi chúng ta không lặp lại |
| 2 | **Mọi module tự sở hữu entity của mình**, giao tiếp qua event | Module → app là phụ thuộc ngược; đã áp dụng đúng ở `ChannelHub` |
| 3 | **Idempotency ở mọi cửa vào** (webhook sàn, IPN cổng, job retry) | Webhook là at-least-once |
| 4 | **Không bao giờ tin `ReturnUrl`/callback** — chỉ IPN đã xác thực chữ ký | Đã áp dụng ở `PaymentHub` |
| 5 | **Tồn kho append-only ledger**, không UPDATE số lượng | Truy vết + đối soát |
| 6 | **Mọi app service phải có `[Authorize]` ở cấp class** | Bài học Phase 0: ABP không tự bảo vệ app service |
| 7 | **Mọi thao tác ghi đi qua Unit of Work** (transaction tự động) | Nhất quán dữ liệu |
| 8 | **Distributed lock cho tài nguyên dùng chung** (kho theo SKU, token sàn theo shop) | Chống race condition |
| 9 | **Entity có `DbSet` trên DbContext** nếu muốn `IRepository<T>` tự sinh | Bài học Phase 0 |
| 10 | **Module có UI thì cần project Web** | `abp add-module` bỏ qua phần UI khi không có project Web |

---

## 6. Nguồn tham chiếu & bài học từ các repo đã khảo sát

*(2/6 nhánh khảo sát đã xong: **ma trận giấy phép** + **GrandNode2 vs Smartstore**. Còn chờ:
nopCommerce · Orchard Core Commerce + dotnet/eShop · EasyAbp/EShop · nghiệp vụ VN.)*

### 6.1 🎯 Phát hiện quyết định: nguồn học thiết kế cho module **A4 Inventory**

| Nền tảng | Cơ chế tồn kho | Kết luận cho ta |
|---|---|---|
| **Smartstore 6.4** | ❌ **Không có `Warehouse`, không reservation, không ledger** — chỉ `int StockQuantity` trên `Product`; trừ kho ngay lúc đặt hàng (`OrderProcessingService.PlaceOrder.cs:917`); validate ở giỏ hàng **không lock** (`ShoppingCartValidator.cs:438,460`) | 🚫 **Không chống được oversell** ⇒ **không dùng làm tham chiếu cho module kho của ta** |
| **GrandNode 2.4** | ✅ **Đủ cả ba**: `InventoryJournal` (**ledger** In/Out + `PositionId` chống ghi trùng = idempotency), `ReservedQuantity` 3 cấp, `Available = Stock − Reserved` (`StockQuantityService.cs:22-38`), reserve lúc tạo đơn (`PlaceOrderCommandHandler.cs:967`), book lúc xuất kho, **reverse được** | ✅ **Học thiết kế này** (GPL ⇒ chỉ học, tự viết lại) |
| **Cả hai** | ⚠️ **Đều THIẾU chiều "kênh" trong reservation** | 🎯 **Đây là chỗ ta phải tự thiết kế — không copy được từ đâu.** Ta đã có `ChannelListing.AllottedQuantity`; cần `StockReservation` **theo (Warehouse, SKU, Channel)** |

⇒ **Chốt**: module A4 của ta giữ **ledger append-only** (đã có) + thêm **`StockReservation` có TTL và có chiều kênh**.
Đây là điểm khác biệt cạnh tranh thật: không nền tảng nào giải được oversell đa kênh.

#### ✅ Tôi đã kiểm chứng độc lập phát hiện trên (grep trực tiếp mã nguồn đã clone, không tin báo cáo)

| Kiểm tra | Smartstore 6.4 | GrandNode 2.4 |
|---|---|---|
| `class Warehouse` | **0** | — |
| `ReservedQuantity` | **0** | **24 file** |
| `InventoryJournal` / ledger | **0** | **1 class** |
| `PositionId` (chống ghi trùng ledger) | — | **2 file** |
| `StockQuantity` (chỉ số lượng đơn giản) | **36 file** | — |
| Trường `public double` | 0 (dùng `decimal` + `Money`) | **96 field** ⚠️ (khảo sát xác định ~30 là **trường tiền tệ**; phần còn lại là khối lượng/kích thước/toạ độ) |

Nguồn: repo clone sparse tại `D:\Projects\Tools\WF\_research\repos\{smartstore,grandnode2}`
(có thể dùng lại cho khảo sát sau — grep/read trực tiếp, không cần mạng).

### 6.2 Bài học khác đã có bằng chứng

| Module của ta | Bài học cụ thể (kèm nguồn) |
|---|---|
| **A2 Pricing** | Smartstore dùng **pricing pipeline có ordering số** (`CalculatorOrdering.cs`): chạy **hết** pipeline (kể cả discount) **rồi mới so sánh** giá tier tối thiểu với giá cuối (`TierPriceCalculator.cs:41-71`) — **không áp mù**. GrandNode dùng `TierPrice` 4 chiều lọc (store/currency/customer group/date) + giá riêng từng khách (`PricingService.cs:150-156`). ⇒ Ta nên làm **pipeline tính giá có thứ tự tường minh** cho `PriceList`/`ChannelPrice`/`TierPrice`. |
| **A6 Orders** | GrandNode: **32 CQRS handler, mỗi transition một handler** + `OrderStatus` là **entity cấu hình được** (không phải enum cứng) + `OrderItemStatus` + 8 entity trả hàng với 3 data scope (Admin/Store/Vendor). Smartstore: enum cố định + **guard `Can*` tách riêng** (`OrderProcessingService.Payment.cs:96,175,44,257`). ⇒ Ta dùng **state machine có guard tường minh**; cân nhắc `OrderStatus` cấu hình được vì **đơn sàn có trạng thái khác đơn web**. |
| **Ranh giới module** | Smartstore rõ hơn GrandNode (mỗi bounded context tự đầy đủ `Domain/Services/Hooks/Events` + 23 module nạp động qua `module.json`/`Module.cs`). ⚠️ **Nhưng cả hai dùng CHUNG một DbContext** ⇒ ABP của ta **có thể và nên làm chặt hơn** (đúng như ta đang làm: module sở hữu entity + giao tiếp qua event). |
| **Tiền tệ** | ⚠️ **GrandNode 2 lưu tiền bằng `double` ở ~30 field** — đã xác nhận với số dòng cụ thể: `Order.cs:140-214` (10 field), `OrderItem.cs:68-175` (11 field), `TierPrice.cs:31`, `ProductPrice.cs:7`, `Discount.cs:30,35,55`, `GiftVoucher.cs:20`. **Smartstore dùng `decimal` + type `Money`** ⇒ ta giữ `decimal` (nguyên tắc #1). |

### 6.3 Đính chính về repo (quan trọng nếu đọc lại tài liệu cũ)

| Tôi/đề bài từng ghi | Thực tế đã xác minh |
|---|---|
| `grandnode/grandnode` = GrandNode 2 | `grandnode/grandnode` = **legacy 1.x, đã archive** (push cuối 25/08/2021, license `NOASSERTION`) |
| — | **GrandNode 2 = `grandnode/grandnode2`**, branch `develop`, v2.4.0, **GPL-3.0** |
| `src/Core/Grand.Services` | **404** — business layer ở `src/Business/Grand.Business.*` (10 project) |
| `src/Web/Grand.Web/Areas/Admin/Controllers` | Không tồn tại — admin ở `src/Web/Grand.Web.AdminShared/Controllers` (39 file) |
| `Smartstore.Core` có `Customers/Directory/Media/Messages/Orders/Security` | Nay chỉ còn **8 thư mục cấp 1**: `Catalog, Checkout, Common, Content, Data, Migrations, Platform, Properties` |
| `ReturnRequest` (Smartstore) | Đã đổi tên thành **`ReturnCase`** |

### 6.4 Bài học từ **nopCommerce** (khảo sát sâu, có số dòng cụ thể)

> Bối cảnh: branch `develop` của nopCommerce nay đã lên **.NET 10** (`Directory.Build.props` → `net10.0`,
> `global.json` SDK 10.0.401, version tự khai 5.00 — tức **code chưa phát hành**; bản release vẫn là 4.90.x).
> Cùng nền tảng .NET nên đọc code tham chiếu rất thuận.

**🎯 Bài học quan trọng nhất — trạng thái đơn hàng là 3 TRỤC TRỰC GIAO, không phải 1 state machine:**

| `OrderStatus` | | `PaymentStatus` | | `ShippingStatus` | |
|---|---|---|---|---|---|
| Pending | 10 | Pending | 10 | ShippingNotRequired | 10 |
| Processing | 20 | Authorized | 20 | NotYetShipped | 20 |
| Complete | 30 | Paid | 30 | PartiallyShipped | 25 |
| Cancelled | 40 | PartiallyRefunded | 35 | Shipped | 30 |
| | | Refunded | 40 | Delivered | 40 |
| | | Voided | 50 | | |

Nhờ 3 trục trực giao, đơn **"đã giao nhưng chưa thu tiền" (COD — cực phổ biến ở VN) biểu diễn tự nhiên**
(`Shipping=Delivered` + `Payment=Pending`). `OrderStatus` được **suy ra** từ 2 trục kia
(`OrderSettings.CompleteOrderWhenDelivered`: chỉ Complete khi shipping = Delivered).

⇒ **Áp dụng cho ta:** giữ 3 trục + **thêm trục thứ 4 `ReconciliationStatus`** (chờ đối soát/khớp/lệch/đã xử lý)
mà nopCommerce **hoàn toàn không có**. Và thay enum cứng bằng **state machine có transition table** —
`OrderStatus` 4 giá trị là **quá thô** cho vận hành VN (cần: chờ lấy hàng, đang giao, giao thất bại, hoàn về).

**Tồn kho — nopCommerce xác nhận mô hình ta đã chọn:**

| nopCommerce | Ta |
|---|---|
| `ProductWarehouseInventory { StockQuantity, ReservedQuantity }` — tách **tồn thực** vs **đã giữ chỗ** | ✅ ta có `InventoryItem.OnHand` + `Reserved` |
| `StockQuantityHistory { QuantityAdjustment, **StockQuantity (số dư sau)**, Message, ... }` — ledger có số dư sau | ✅ ta có `StockLedgerEntry.BalanceAfter` |
| `BookReservedInventoryAsync` (qty phải âm) / `ReverseBookedInventoryAsync` | ✅ ta có `StockMovementType.Reservation` / `ReservationRelease` |
| ⚠️ `IProductService` **KHÔNG có tham số channel**; `ProductWarehouseInventory` **không có concurrency token** (rowversion) | 🎯 **Đây là khoảng trống ta phải tự lấp: `StockReservation` theo `(Warehouse, SKU, Channel)` + lock theo SKU** |

**Pricing — hai thứ đáng học:**
1. **`TierPrice` tách thành entity riêng** (`ProductId`, `StoreId` (0=all), `CustomerRoleId`, `Quantity`, `Price`, `StartDateUtc`, `EndDateUtc`) — không nhét vào Product.
2. **`PriceList`** (tính năng mới 06/2026): `PriceCalculationType` (PercentageDecrease/Increase · AmountDecrease/Increase · **FixedPrice**), `PriceListStrategy` (MinimalPrice · UseByPriority), `Priority`, và `PriceListItem.ManualPrice` (**ghi đè** giá đã tính).
   ⚠️ **Đối tượng áp dụng của họ là customer/role — KHÔNG phải kênh** ⇒ ta học **mô hình** (bảng giá = quy tắc + priority + ghi đè + chiến lược chọn) nhưng **phải thêm trục `ChannelId`**.

**Promotions — thiết kế đáng bắt chước nhất:**
`DiscountRequirement` = **cây điều kiện AND/OR** (`{ DiscountId, RuleSystemName, ParentId, InteractionTypeId, IsGroup }`, `RequirementGroupInteractionType { And=0, Or=2 }`) + rule cắm qua plugin (`IDiscountRequirementRule`), mapping đa hình (`DiscountProductMapping`/`DiscountCategoryMapping`/`DiscountManufacturerMapping` đều kế thừa `DiscountMapping`), `DiscountType` (OrderTotal/OrderSubTotal/SKUs/Categories/Manufacturers/Shipping), `IsCumulative`, `DiscountLimitation`.
⇒ Ta dùng **cây điều kiện** cho Voucher/Promotion, **cộng thêm điều kiện theo kênh**.

**🚫 Anti-pattern phải tránh (có số đo thật):**

| Vấn đề của nopCommerce | Số đo | Ta tránh bằng |
|---|---|---|
| `Product.cs` gánh **6 nghiệp vụ** (vật lý, download số, thuê, định kỳ, thẻ quà, PAngV Đức) | **606 dòng** | tách `ProductShippingProfile`, `DigitalAsset`, `SubscriptionPlan`… |
| `Order.cs` ~12 cột tiền denormalized ×incl/excl tax + **10 property `[Obsolete]` vẫn còn** | **388 dòng** | value object `Money { Amount, Currency, TaxInclusive }` |
| Business logic trong service khổng lồ | `OrderProcessingService` **3.564 dòng / ~45 method**; `ProductService` 2.650; `ShoppingCartService` 2.097 | tách theo use case (Placement / Payment / Fulfillment / Cancellation / Totals) |
| Lưu nghiệp vụ dạng **chuỗi XML** (`Order.TaxRates`, `OrderItem.AttributesXml`, `Address.CustomAttributes`…) | 10+ trường | bảng con thật hoặc `jsonb` |
| **Hai mô hình địa chỉ song song** (`Customer.StreetAddress…` + entity `Address` + `CustomerAddressMapping`) | — | một nguồn sự thật: `Address` + sổ địa chỉ |
| Quy ước **"0 = tất cả"** thay vì null, trộn với `null` trong cùng signature | nhiều chỗ | dùng `null`/`*` rõ nghĩa, không dùng 0 |
| **9 cặp `XxxId` + `Xxx`** không map; enum **có lỗ** (DiscountType 1,2,5,6,10,20) | — | enum chuẩn, lưu bằng string hoặc int liên tục |
| `Setting` = bảng **key-value string**, ~38 class settings serialize vào đó | — | `SettingManagement` của ABP, có kiểu |
| `OrderSettings` toàn cục điều khiển invariant giao dịch (`PlaceOrderWithLock`, `AutoCancelEnabled`, `MinimumOrderPlacementInterval`…) | — | cấu hình theo tenant/kênh qua Feature/Setting của ABP, có audit |

**⚡ Cảnh báo chiến lược quan trọng nhất — "multi-store" của nopCommerce KHÔNG phải "đa kênh":**

Cơ chế của họ = **1 cột cờ `LimitedToStores` + bảng mapping đa hình `StoreMapping { EntityId, EntityName, StoreId }`**,
resolve store theo **HTTP_HOST** (`Store.Hosts`), **cùng DB, cùng schema, không có `TenantId`** — chỉ là mệnh đề WHERE.
`SearchProductsAsync(..., int storeId = 0, ...)` với doc *"0 to load all records"*.

⇒ **Kênh Shopee/Lazada không phải website có `HTTP_HOST`.** Vì vậy:
> **Giữ `Store` cho mặt tiền website, nhưng TUYỆT ĐỐI không dùng `StoreId` làm trục cho `ChannelHub`.
> Giá / tồn kho / đơn hàng phải khoá theo `ChannelId`. Để ABP lo cách ly tenant; ta lo cách ly kênh.**

**Đối soát/settlement: nopCommerce KHÔNG CÓ GÌ để học** (chỉ có `AuthorizationTransactionId`,
`CaptureTransactionId`, `CaptureTransactionResult` trên `Order`) ⇒ đây là **module hoàn toàn mới** ta phải tự thiết kế:
`SettlementBatch`, `SettlementLine`, `ReconciliationStatus`, `Discrepancy`, đối soát **3 chiều**
(sàn ↔ `PaymentHub` ↔ sao kê ngân hàng).

### 6.5 Bài học từ **Orchard Core Commerce (OCC) + dotnet/eShop** — hai nguồn **MIT, copy được**

**⚠️ Đảo ngược giả định: hạ tầng kho của TA TỐT HƠN OCC.** Grep toàn repo OCC (536 file `.cs`):
`ledger` = **0**, `reservation|Reserve(` = **0**, `outbox|IntegrationEvent` = **0**.
Tồn kho của OCC = **một số `int`** trong `Dictionary<string,int>` nhúng trong content item, khoá bằng
**`static SemaphoreSlim`** (`LocalInventoryProvider.cs`) — **chỉ khoá trong 1 process ⇒ SAI khi chạy nhiều instance**;
trừ kho **SAU khi thanh toán** ⇒ **có cửa sổ oversell**; không có nhiều kho.
⇒ **Giữ nguyên `InventoryItem` + `StockLedgerEntry` append-only của ta. Không thay bằng OCC.**

**Modularity của OCC cũng không phải hình mẫu:** chỉ có **8 module** (core, ContentFields, Inventory,
Payment, Payment.Exactly, Payment.Stripe, Promotion, Tax) + 3 library; Cart/Order/Session/Attributes là
**feature nằm trong module core**. **Shipping hoàn toàn KHÔNG TỒN TẠI** (không project, không `ShippingPart`;
`OrderAdditionalCost` được khai báo nhưng **grep cho thấy chưa từng được ghi ở đâu**).

**Trưởng thành của OCC (số đo được):** stable mới nhất **4.0.0 ngày 30/08/2026** (.NET 10, 352 tải),
~**1 stable/năm**, 46 phiên bản / 16.2K lượt tải; 536 file `.cs` / 21.557 dòng; tự nhận
*"This work is in its initial design phases. There's a lot of work to do"*; chỉ tuyên bố production-ready
ở mức **payment provider**; thuế tự nhận là *"ongoing process"*, phù hợp *"stores that only ship locally"*.
⇒ **Không đủ chín để làm hạt nhân commerce**, nhưng có **một library đáng copy**.

#### 🥇 Pattern đáng copy nhất từ eShop: **chống trùng bằng guard trạng thái trong aggregate**

Phát hiện quan trọng: cơ chế chống trùng thật của eShop **KHÔNG phải** `IdentifiedCommandHandler`
(các integration event handler gửi command **trần**). Chống trùng thật nằm ở **guard trong aggregate `Order`**:

```csharp
// eShop Order.cs — idempotent THEO TRẠNG THÁI, không cần bảng idempotency
public void SetPaidStatus()
{
    if (OrderStatus == OrderStatus.StockConfirmed)   // chỉ chạy khi đúng trạng thái
    { OrderStatus = OrderStatus.Paid; ... }
    // ngược lại: IM LẶNG bỏ qua ⇒ webhook gửi lại không gây hại
}
```
⚠️ eShop **không nhất quán**: `SetShippedStatus`/`SetCancelledStatus` **ném** `OrderingDomainException`.
⇒ Ta phải **tự quyết định chính sách** (im lặng vs ném) và áp nhất quán.

🎯 **Kết hợp với thiết kế của ta:** ta đã có **unique index `(TenantId, ChannelCode, ExternalOrderId)`**
ở tầng DB. **Giữ unique index + thêm guard trạng thái trong aggregate** — hai lớp bổ trợ.

#### 🚨 NHƯNG guard trạng thái là **CHƯA ĐỦ** — 4 đính chính quan trọng (đã kiểm chứng trên mã nguồn)

| # | Sự thật | Bằng chứng | Hệ quả cho ta |
|---|---|---|---|
| 1 | **Guard im lặng** — `SetAwaitingValidationStatus`/`SetStockConfirmedStatus`/`SetPaidStatus` **không có `else`, không throw**; handler vẫn `SaveEntitiesAsync` + trả `true` | `Order.cs:101,110,121` vs `:132,144` (2 hàm này **throw**) | **Event mất/rơi KHÔNG để lại dấu vết nào.** Đây là *idempotency vô tình*, không phải thiết kế ⇒ **ta phải dùng state machine tường minh + `BusinessException`/log có cấu trúc** |
| 2 | **`IdentifiedCommand` KHÔNG được dùng trong saga** — chỉ dùng ở endpoint HTTP (`Apis/OrdersApi.cs:32,61,146`); **mọi handler của saga gửi command TRẦN** | 5 handler: `GracePeriodConfirmed…:19`, `OrderStockConfirmed…:12`, `OrderStockRejected…:15`, `OrderPaymentSucceeded…:12`, `OrderPaymentFailed…:12` | Guard bảo vệ **chuyển trạng thái**, nhưng **KHÔNG bảo vệ side-effect**: redeliver `OrderStatusChangedToPaid` ⇒ **trừ kho 2 LẦN** (`RemoveStock` không idempotent) ⇒ **bắt buộc có bảng inbox** |
| 3 | **eShop KHÔNG reserve tồn kho** ⇒ **không có "nhả kho"** | comment nguyên văn `OrderStatusChangedToPaidIntegrationEventHandler.cs:12` — `//we're not blocking stock/inventory`; grep `RestoreStock\|ReturnStock\|ReleaseStock\|Reserve` toàn repo = **0**; Catalog.API **không có handler** cho event huỷ | Khẳng định ở §6.5 cũ của tôi ("nhả tồn kho") là **SAI** — đã sửa. Ta **phải tự làm reservation** |
| 4 | **Trừ kho ở `Paid` có thể trừ THIẾU mà không báo lỗi** | `RemoveStock` (`CatalogItem.cs:62-76`): `int removed = Math.Min(quantityDesired, AvailableStock)` (`:74`) → **trừ một phần**; caller **bỏ qua giá trị trả về** | Đơn vẫn sang `Paid` dù kho trừ thiếu ⇒ **bắt buộc kiểm tra kết quả trừ kho và rollback nếu thiếu** |
| 5 | **Oversell là CÓ THỂ**: kiểm tra ở bước 2 và trừ kho ở bước 5 cách nhau ≥10s, không lock, không reservation | `SetStockConfirmedOrderStatusCommandHandler.cs:22` (`Task.Delay(10000)` — **demo, không port**) | ⇒ đúng khoảng trống ta đang lấp bằng reservation + lock |

#### 💣 Phát hiện quan trọng nhất về hạ tầng: **outbox có bảng nhưng THIẾU RELAY**

eShop **có** bảng `ordering.IntegrationEventLog`, ghi **cùng transaction**, publish sau commit. **Nhưng:**

- `RetrieveEventLogsPendingToPublishAsync` lọc theo **`TransactionId` của CHÍNH transaction vừa commit**
  (`IntegrationEventLogService.cs:19-23`) ⇒ **không bao giờ quét event mồ côi của transaction cũ**.
- Publish lỗi → `MarkEventAsFailedAsync` rồi **throw, không ai đọc lại `PublishedFailed`**.
- **Không có `BackgroundService`/`IHostedService` nào quét outbox.**
⇒ Nếu process chết giữa `CommitAsync()` và `PublishAsync()` (`TransactionBehavior.cs:47→52`),
event nằm `NotPublished` **mãi mãi** ⇒ **dual-write KHÔNG được loại bỏ, chỉ được thu hẹp cửa sổ**.

**Và consume thì tệ hơn:** `RabbitMQEventBus.cs:170-180` bắt exception → **vẫn `BasicAckAsync`** ⇒
**handler lỗi = event mất vĩnh viễn** (comment trong chính mã nguồn thừa nhận thiếu DLX). Không dedup
(`ProcessEvent` không kiểm tra `event.Id`), không retry nghiệp vụ (chỉ Polly retry transport 10 lần khi publish).

⇒ 🎯 **Đây là lý do mạnh nhất để dùng Outbox/Inbox CÓ SẴN của ABP** (có relay worker phát lại thật):
**giá trị lớn nhất ta lấy được từ ABP, không phải từ eShop.**

#### 🗺️ Trạng thái đơn cho **sàn TMĐT** — đề xuất tinh chỉnh

Từ 3 trục của nopCommerce (§6.4), với đơn sàn cần tách thành **2 trục độc lập + thông tin người huỷ**:

| Trục | Trạng thái |
|---|---|
| **Thực hiện (fulfillment)** | `PendingConfirm` → `Confirmed` → `Packing` → `ReadyToShip` → `HandedToCarrier` → `InTransit` → `Delivered` / `DeliveryFailed` → `Returning` / `Returned` |
| **Tài chính** | `Unpaid` → `Authorized` → `Paid` → `EscrowHeld` → `Settled` / `RefundPending` / `Refunded` |
| **Đối soát** | `NotReconciled` → `Matched` / `Discrepancy` → `Resolved` |
| **Phụ** | `AwaitingShopConfirmation` (**có SLA tự huỷ**), `AwaitingPickup`, và **phân biệt người huỷ**: `CancelledByBuyer` / `CancelledBySeller` / `CancelledByPlatform` / `CancelledBySystem` |

⚠️ **eShop KHÔNG THỂ huỷ đơn đã thanh toán** — `Order.cs:144-148` **ném lỗi** nếu `Paid || Shipped`
⇒ **không chấp nhận được với TMĐT**. Ta **phải** có compensation hoàn tiền + huỷ/trả một phần.

#### 🛠️ Bổ sung vào danh sách "phải tự xây" (từ báo cáo này)

| Thứ | Thiết kế đề xuất |
|---|---|
| **`StockReservation`** | `(OrderId, SkuId, Quantity, ExpiresAt, Status)` + **TTL + job nhả kho** (chính là compensation eShop thiếu) + **idempotency key `(OrderId, SkuId, ReservationType)`** + khoá lạc quan (`xmin`) hoặc `SELECT … FOR UPDATE` |
| **`OrderSagaState`** | `(OrderId, CurrentStep, Status, AttemptCount, NextRetryAt, FailureReason)` + job quét `NextRetryAt` để retry + job phát hiện **đơn treo** |
| **Webhook/IPN ingress** | verify chữ ký → lưu **raw payload + `EventId` của đối tác** với **unique index** → trả 200 nhanh → xử lý async. **Phải chịu được webhook đến SAI THỨ TỰ** (state machine + buffer event) — eShop hoàn toàn không có |
| **`OrderStatusHistory`** | audit mọi lần chuyển trạng thái (eShop chỉ ghi đè 1 chuỗi `Description` — `Order.cs:115,126,138,151,166`) |
| **COD** | `CodPending` → `CodCollected` → `CodRemitted` / `CodFailed`. **COD đảo dòng tiền** (giao trước thu sau) ⇒ trừ kho **lúc giao**, cần reservation thật; COD là nguồn chính của `DeliveryFailed` |
| **DLQ** | RabbitMQ DLX (bắt buộc) |

#### ⚠️ Điểm cần xác minh trước khi code (chưa kiểm chứng)

**API `UseOutbox`/`UseInbox` chính xác của ABP 10.7** (tên option/namespace) — kiến thức chung về ABP,
**chưa tra tài liệu 10.7**. Phải kiểm tra trước khi viết code outbox/inbox.

#### Port được (xếp hạng, tất cả MIT)

| # | Port gì | Nguồn | Vì sao đáng port |
|---|---|---|---|
| **1** | **`Amount` + `Currency` + `IMoneyService`** | OCC `src/Libraries/OrchardCore.Commerce.MoneyDataType/` (796 dòng) | 🥇 **Không phụ thuộc OrchardCore ⇒ copy sạch.** Có `GetFixedPointAmount` cho **số nguyên VND**, chặn cộng/trừ khác loại tiền. Hoặc dùng thẳng NuGet `OrchardCore.Commerce.MoneyDataType` |
| **2** | **Guard trạng thái trong aggregate** | eShop `Order.cs` | Chống trùng đơn **không cần bảng idempotency** |
| **3** | **Bảng inbox** (`ClientRequest` + `IRequestManager`) | eShop `Ordering.Infrastructure/Idempotency/` (3 file nhỏ) | Nền tảng chống trùng webhook sàn — **phải thêm** `TenantId`, `Channel`, `ExternalOrderId`, `EventType`, `PayloadHash` + unique index |
| **4** | **Pipeline provider + priority** | OCC `IPriceProvider` / `PrioritizedPrice` / `SimplePriceStrategy` | Mở rộng giá lúc chạy, không sửa core |
| **5** | `Payment` record nhiều charge/đơn | OCC `Abstractions/Models/Payment.cs` | Khớp `PaymentHub` (đặt cọc + phần còn lại) |

**Phải sửa khi port sang ABP:** KHÔNG copy entity nào của eShop mà không thêm `TenantId` (eShop single-tenant);
dùng `IRepository<T>` / `IUnitOfWorkManager` / **outbox của ABP** thay cho `IOrderRepository` /
`TransactionBehavior` / `IntegrationEventLogEF` thủ công; thay `SemaphoreSlim` bằng **distributed lock** ta đã có.

#### 🚫 Anti-pattern idempotency của OCC (phải tránh)

| Vấn đề | File | Hệ quả |
|---|---|---|
| `IdempotencyKey = Guid.NewGuid()` **mỗi lần gọi** | `Payment.Stripe/Services/RequestOptionsService.cs` | Retry tạo key khác ⇒ **mất hoàn toàn tác dụng idempotency** |
| **Không** lưu/kiểm tra `stripeEvent.Id` | `DefaultStripeWebhookEventHandler.cs` | Webhook gửi lại được xử lý lại |
| Guard status **check-then-act không lock** | `PaymentService.CallBackAsync` | Race condition khi 2 webhook đồng thời |

⇒ **Bài học cho `PaymentHub`:** idempotency key phải **ổn định** (sinh từ mã giao dịch nội bộ, không phải Guid mới),
và phải **lưu + kiểm tra** id sự kiện của cổng.

#### 🛠️ Nhóm PHẢI tự xây (không nguồn nào cho sẵn)

`StockReservation` có TTL + chiều kênh · dedup ở consumer · **job replay outbox** (eShop at-least-once nhưng
**không có job retry/replay**) · idempotency key **ổn định** cho cổng thanh toán ·
bảo vệ state transition ở **tầng DB** (`RowVersion` / `UPDATE … WHERE status = …`) · **price list đa kênh**.

#### 📜 Giấy phép hai nguồn này

| Repo | Giấy phép | Ghi chú |
|---|---|---|
| `OrchardCMS/OrchardCore.Commerce` | **MIT** (file tên `License.md`, có **13 bản**, mỗi project 1 file) | ✅ copy được, giữ copyright notice |
| `dotnet/eShop` | **MIT** (`Copyright (c) .NET Foundation and Contributors`) | ✅ copy được |
| ⚠️ `Lombiq.HelpfulLibraries.*` (OCC dùng `using Lombiq.*`) | **BSD-3-Clause** | ❌ **Chưa đọc nguyên văn** — phải đọc trước khi copy file có `using Lombiq.*` (BSD-3 có điều khoản endorsement mà MIT không có) |

### 6.6 Bài học từ **EasyAbp/EShop** (MIT, ABP-native — nguồn gần ta nhất, khảo sát tới mức aggregate)

**Cấu trúc thật (đã xác minh qua EF config từng module):**
`modules/` = **Orders, Payments, Products, Stores, Plugins** · `plugins/` = **Baskets, Booking, Coupons, FlashSales, Inventories, Promotions**.
Entity chính: Products → `Product`, `ProductDetail`, `ProductAttribute`/`Option`, `ProductSku`, `Category`, `ProductCategory`, **`ProductInventory`**, `ProductView` · Orders → `Order`, `OrderLine`, `OrderDiscount`, `OrderExtraFee` · Payments → `Payment`, `PaymentItem`, `Refund*` · Stores → `Store`, `StoreOwner`, **`Transaction`**.

#### 🎯 Xác nhận điều quan trọng nhất: **KHÔNG AI có tồn kho theo kênh**

| Phát hiện | Bằng chứng |
|---|---|
| Abstraction tồn kho nằm trong **module Products**, KHÔNG phải plugin `Inventories` | `DefaultProductInventoryProvider.cs`, `IProductInventoryProvider`, `ProductInventoryProviderResolver.cs` (chọn theo `Product.InventoryProviderName`); plugin `Inventories` chỉ có 2 provider **tuỳ chọn** (Dapr, Orleans) |
| `ProductInventory` **chỉ có 2 số**: `Inventory` (int) + `Sold` (long) | EF config Products |
| **KHÔNG có ledger append-only** | chỉ counter in-place — **ta có `StockLedgerEntry`, họ không có** ⇒ ta hơn |
| **KHÔNG có reservation/hold** | `ReduceAfterPlacing` **chính là** cơ chế giữ chỗ; huỷ đơn → `InventoryRollbackOrderCanceledEventHandler` hoàn kho |
| **Tồn kho dùng CHUNG cho mọi store trong tenant** | `ProductInventory` **không có `StoreId`**; `StoreId` trong `InventoryQueryModel` chỉ để phát event/khoá lock |
| Chống oversell thật = `TryReduceInventory` trong `IAbpDistributedLock` key `eshop-product-inventory-{tenant}-{product}-{sku}` (30s) + UoW | ⚠️ phụ thuộc host có cấu hình **lock provider thật** (chưa xác minh) |
| Không unique index `(ProductId, ProductSkuId)`, không `CHECK Inventory >= 0` | EF config |

⇒ **Chốt danh sách "chiều kênh" là thứ duy nhất không nền tảng nào có.** Ta giữ `StockLedgerEntry` + thêm
`StockReservation` theo `(Warehouse, SKU, Channel)`, khoá lock theo SKU (học cách đặt tên key của EShop).

#### ⚖️ Quyết định thiết kế: **KHÔNG dùng `OrderStatus` của EShop**

EShop dùng `[Flags] OrderStatus` **chỉ 4 giá trị**: `Pending=1, Processing=2, Completed=4, Canceled=8` —
**không có Shipped/Delivered/Returned/Refunding**; trạng thái thật phải **suy từ timestamp**
(`IsPaid()`, `IsCanceled()`, `IsInPayment()`, `IsInInventoryDeductionStage()`).

> ⇒ **Ta dùng 3 trục trực giao của nopCommerce + trục thứ 4 `ReconciliationStatus`** (xem §6.4),
> **KHÔNG** dùng flags của EShop. Nhưng **copy các extension point** của EShop vì chúng rất sạch.

#### 💰 Anti-pattern tiền tệ của EShop — phải tránh

| Vấn đề | Bằng chứng | Hệ quả với VN |
|---|---|---|
| So khớp tiền bằng **`==` tuyệt đối** | `OrderPaymentChecker`: `order.ActualTotalPrice == item.OriginalPaymentAmount` | ❌ Vỡ ngay với VND/payOS/VNPay nếu lệch làm tròn |
| `decimal(20,8)` cho mọi cột tiền | EF config Products | Thừa 8 số thập phân cho VND (đơn vị nhỏ nhất là 1 đồng) |
| Round qua `new Money(price, currency).Amount` (NodaMoney, **2 chữ số**) ngay khi tạo/update SKU | `ProductSku` | ❌ Mất chính xác với VND |
| **Không đa tiền tệ** — 1 Setting toàn cục `OrdersSettings.CurrencyCode` (default `USD`), `productSku.Currency` phải khớp nếu không ném `UnexpectedCurrencyException` | `NewOrderGenerator` | Không bán đa kênh đa tiền tệ được |

⇒ **Ta nên dùng `MoneyDataType` của OCC (MIT, có `GetFixedPointAmount` cho số nguyên VND)** — xem §6.5 —
hoặc `decimal` với **chính sách làm tròn tường minh**; và **không bao giờ so tiền bằng `==`**
(dùng so sánh có tolerance hoặc so theo đơn vị nhỏ nhất dạng `long`).

#### ⚠️ Hợp đồng ngầm nguy hiểm — không copy

`PaymentItem.StoreId` được đọc từ **`ExtraProperties["StoreId"]`**, thiếu thì ném `StoreIdNotFoundException`
(`PaymentSynchronizer.FillPaymentItemStoreId`). **Xoá hẳn kiểu hợp đồng ngầm này.**

#### 📋 Phân loại PORT (quyết định cụ thể)

| Nhóm | Nội dung |
|---|---|
| ✅ **COPY** | (a) mô hình tiền + bất biến `Order`/`OrderLine`/`OrderDiscount`/`OrderExtraFee` + `NewOrderGenerator`/`OrderManager`; (b) **extension point**: `IOrderLinePriceOverrider`, `IOrderExtraFeeProvider`, `IOrderDiscountProvider` + `OrderDiscountResolver` (BFS chọn tổ hợp tốt nhất), `IOrderCompletableCheckProvider`, `IOrderPaymentChecker`; (c) abstraction tồn kho (`IProductInventoryProvider` + resolver + per-SKU lock key + `ProductInventoryChangedEto`); (d) invariant catalog trong `ProductManager` (SKU↔attribute option, unique name); (e) `Stores` (Store/StoreOwner/`Transaction` append-only/`IMultiStore` — rất nhỏ) |
| 🔧 **ADAPT, không copy nguyên** | `Payments` — nó chỉ là **bản chiếu của `EasyAbp.PaymentService`** qua `PaymentSynchronizer`. Ta **đã có `PaymentHub`** ⇒ giữ *hình dạng* `Payment`/`PaymentItem`/`Refund*` làm projection nội bộ và tự phát event tương đương |
| 🔨 **PHẢI VIẾT LẠI/THÊM** | tồn kho **theo kênh** (+ ghi `StockLedgerEntry` **trong cùng UoW**) · lock provider thật · unique index + `CHECK` · chuẩn hoá tiền VND (bỏ `==`, policy làm tròn) · quyết định đa tiền tệ · **outbox/inbox + idempotency** (họ không có) · thuế/hoá đơn/**địa chỉ giao hàng**/RMA qua extension point · `ICurrentStore` + data filter nếu muốn cách ly store · map `ChannelShop ≡ Store` + thêm `ChannelId` |
| ⏭️ **SKIP** | provider Dapr/Orleans, `Booking` (kéo `EasyAbp.BookingService` + 3 module), `FlashSales` (trừ khi cần thật) |

#### 🚨 Rủi ro khi port (xếp theo mức)

1. **Phụ thuộc chéo module kéo dây chuyền**: `Products.Domain` → `Orders.Domain.Shared`; `Orders.Domain` → `Payments.Domain.Shared` + `Products.Domain.Shared`; Orders còn dùng **trực tiếp ETO của `PaymentService`** ⇒ **port 1 module là kéo theo nhiều module**.
2. **Luồng kho là eventual-consistency KHÔNG có saga**: rollback viết tay **lặp 2 bản** ở 2 handler; rollback thất bại **chỉ `LogWarning`, không retry** (issue #214/#139).
3. Chống oversell **phụ thuộc cấu hình lock provider** của host.
4. **Không ledger**; **tồn kho không tách kênh**.
5. Tiền: `==` + `decimal(20,8)` + NodaMoney 2 chữ số ⇒ **dễ vỡ với VNPay/payOS/SePay**.
6. **`dev` chưa có bản NuGet tương ứng** (NuGet mới nhất 5.12.0) ⇒ dùng `dev` là phải build từ source.
7. `OrderDiscountResolver` BFS **có thể bùng nổ tổ hợp** (chưa thấy guard).

> **Kết luận về EShop:** dùng làm **nguồn pattern + vài class cụ thể (MIT)**, **KHÔNG** dùng làm hạt nhân commerce —
> vì nó thiếu đúng những thứ ta cần nhất (chiều kênh, ledger, reservation, thuế, hoá đơn, địa chỉ giao, đa tiền tệ)
> và có rủi ro kéo theo cả chuỗi module.

#### 📜 Giấy phép

`EasyAbp/EShop` = **MIT, `Copyright (c) 2020 EasyAbp Team`** (đã đọc file LICENSE); `EasyAbp/PaymentService` = MIT cùng copyright.
**Nghĩa vụ duy nhất:** kèm copyright + nguyên văn permission notice trong bản phân phối.
**Quy ước header khi port (đã ghi vào `THIRD-PARTY-NOTICES.md`):**
`// Ported from EasyAbp/EShop (MIT, Copyright (c) 2020 EasyAbp Team) — <đường dẫn gốc> @ <commit>`

### 6.7 Nghiệp vụ đặc thù Việt Nam — **P0 vì lý do PHÁP LÝ, không phải P2**

> 🚨 **ĐÍNH CHÍNH KẾ HOẠCH CŨ:** trong `ecommerce-multichannel-plan.md` (bản khảo sát trước), hoá đơn điện tử
> được xếp ở **"Phase 3, mức Trung bình"**. **Sai.** Nghị định **70/2025 hiệu lực 01/06/2025** áp dụng cho
> doanh nghiệp bán trực tiếp cho người tiêu dùng **và** hộ/cá nhân kinh doanh doanh thu **≥ 1 tỷ/năm**, qua
> HĐĐT khởi tạo từ máy tính tiền. ⇒ Đây là **nghĩa vụ pháp lý hiện tại**, phải lên **P0 cùng lúc với checkout**.
> (NĐ 123/2020 đã bắt buộc từ 01/07/2022.)

#### 🚨 Breaking change đang diễn ra: **bỏ cấp huyện + sáp nhập tỉnh**

Từ **01/07/2025**: Việt Nam **bỏ cấp huyện** và sáp nhập tỉnh (**63 → 34**). Đo trực tiếp:
`provinces.open-api.vn/api/` → **63** tỉnh **có** `districts`; `/api/v2/` → **34** tỉnh **không** huyện
(⚠️ nhưng `wards` trả **rỗng** khi test ⇒ phải kiểm tra lại).

**GHN đã thích ứng:** thêm cờ `is_new_to_address` / `is_new_from_address` / `is_new_return_address`;
`to_district_name` **required khi cờ = false**.

> ⇒ **Mô hình địa chỉ của ta phải hỗ trợ CẢ HAI cấu trúc** (3 cấp cũ và 2 cấp mới), và **"địa chỉ gửi đối tác"
> là DỮ LIỆU ĐỒNG BỘ, không phải master data** — mỗi hãng vận chuyển có bộ mã riêng.

#### 📦 6 module phải tự xây (không có SDK .NET chính thức nào)

Đã kiểm chứng qua `api.nuget.org/v3-flatcontainer`: **toàn bộ ecosystem .NET cho các API này ở VN gần như trống** —
chỉ **5 gói NuGet tồn tại** cho cả 4 nhóm, **không gói nào chính thức**.

| Module | Ưu tiên | Cơ sở / rủi ro |
|---|---|---|
| **`AddressHub`** | 🔴 **P0** | Breaking change 01/07/2025 (bỏ huyện, 63→34 tỉnh). Ghép chung với `Commerce.Core` sẽ sai mô hình ⇒ **tách riêng** |
| **`EInvoiceHub`** | 🔴 **P0** | NĐ 123/2020 (từ 01/07/2022) + **NĐ 70/2025 (từ 01/06/2025)** |
| **`DigitalSigningHub`** | 🔴 **P0** | **Là STATE MACHINE, không phải hàm sync** — xem cảnh báo bên dưới |
| **`ShippingHub`** | 🟠 P2 | **GHN tốt nhất** (REST, có sandbox, publish `llms.txt`/`llms-full.txt`); GHTK OK; **7 hãng còn lại phần lớn KHÔNG tìm thấy tài liệu API** |
| **`CodReconciliation`** | 🟠 P2 | ⚠️ **KHÔNG có API đối soát.** GHN chỉ có API **đổi** COD; bảng kê là **file Excel tải THỦ CÔNG** trên portal, kỳ theo **"phiên"** (`mã phiên`) |
| **`AccountingHub` + `InboundInvoiceHub`** | 🟡 P3 | MISA/Bravo/Fast **không công bố spec .NET**; Bravo chính thức hỗ trợ **Excel/CSV** |

#### ⚠️ `DigitalSigningHub` — điều dễ thiết kế sai nhất

VNPT dùng **quy trình ký 2 BƯỚC** (xin Hash → gửi hash đã ký) cho cả Token và SmartCA.
⇒ **Với token/USB, KHÔNG THỂ phát hành hoá đơn hoàn toàn tự động — luôn có bước con người.**
Muốn tự động 100% phải dùng **HSM hoặc remote signing** (chi phí khác hẳn).
Nguồn: tài liệu VNPT **313 trang**, bản 6.0, cập nhật theo NĐ 70 — nguồn đầy đủ nhất tìm được cho bất kỳ NCC VN nào.
⚠️ Đặc thù kỹ thuật: **SOAP/ASMX + XML**, trả về **chuỗi** `"OK:pattern;serial-…"` hoặc `ERR:n`,
**không có HTTP status semantic** ⇒ adapter phải parse chuỗi, không dựa vào status code.

#### 🚩 Rủi ro cao nhất cần hành động NGAY (không phải code)

| # | Rủi ro | Việc phải làm |
|---|---|---|
| 1 | ⚠️ **GHTK vẫn dùng payload 3 cấp** (`province`/`district`/`ward`) và **KHÔNG tìm thấy** cờ tương đương `is_new_to_address` | **Xác minh với GHTK TRƯỚC KHI thiết kế địa chỉ** |
| 2 | ⚠️ **Webhook GHTK không có HMAC được tài liệu hoá** (chỉ `hash` trên URL) — yếu hơn GHN | IP allowlist + **đối soát chủ động** (không tin webhook) |
| 3 | ⚠️ **Đổi COD ở GHN bắt buộc OTP** | Không tự động hoá được — phải có bước thủ công |
| 4 | ⚠️ Base URL MISA chỉ thấy trong **SDK cộng đồng** (`MisaConnect.EInvoice`) | Chưa xác minh với MISA — **không hard-code** |
| 5 | 5 gói NuGet tồn tại đều **không chính thức** | Chỉ dùng `Accotinator.TVanSimulator` (110 lượt tải) cho **test harness**; 4 gói còn lại chỉ **đọc source tham khảo rồi tự viết adapter** |

#### 📋 Việc cần làm ngay (song song với code)

Email **MISA** (Open API AMIS Kế toán + spec PDF) · **GHTK** (địa chỉ 2 cấp) · **FPT IS / M-Invoice /
VNPost / J&T / Best / Ninja Van** · đăng ký **GHN** (`khachhang.ghn.vn`) + **GHTK** lấy token ·
đăng nhập tenant **MISA AMIS** → Hệ thống → Kết nối → tab API · xác minh **số năm lưu trữ**
(Luật Kế toán 88/2015/QH13 Điều 41) · xác minh **ToS** của GHN/GHTK về việc **tự động tải file đối soát**.

💡 **Tận dụng:** GHN publish **`llms.txt` + `llms-full.txt`** ⇒ có thể đưa **thẳng tài liệu GHN vào context
của coding agent**, nên dùng khi viết adapter.
⚠️ **Bẫy tên:** kết quả tìm kiếm đầu cho "EasyInvoice" là `easypos.al` — **hệ thống của Albania**, không phải VN
(không có trong danh sách 39 tổ chức của Cục Thuế).

#### 📛 16 mục "KHÔNG TÌM THẤY" (ghi rõ, không suy đoán)

Danh mục T-VAN đầy đủ · API của FPT.eInvoice/M-Invoice/KiotViet/Sapo/MobiFone/TS24/SmartSign · **SDK .NET chính
thức của bất kỳ NCC HĐĐT nào** · portal API VNPost · API J&T và Best (`openapi.jtexpress.vn` **DNS không tồn tại**,
`api.best-inc.vn` **timeout**) · API merchant GrabExpress · portal Ninja Van · API Bravo và Fast ·
**chu kỳ COD "T+n" của cả 7 hãng** · NCC trung gian có **API đối soát COD** · API Tổng cục Thuế cho hoá đơn đầu vào ·
chuẩn quốc gia trao đổi dữ liệu kế toán · số năm lưu trữ · **cờ địa chỉ 2 cấp của GHTK** · base URL chính thức MISA ·
endpoint điều chỉnh/thay thế của MISA và BKAV.
⚠️ Endpoint của **Viettel SInvoice** chỉ có từ **nguồn thứ cấp** ⇒ **không hard-code**.

### 6.8 Bảng "module → nguồn" (đã đủ 6 khảo sát)

| Module của ta | Nguồn **học thiết kế** | Nguồn **port code được** | Trạng thái |
|---|---|---|---|
| A1 Catalog | ✅ nopCommerce (`SpecificationAttribute`+`Group`+`Option`, `ProductAttributeCombination` có SKU/GTIN riêng) — ⚠️ nhưng **tránh** `Product.cs` 606 dòng | OrchardCore.Commerce (MIT), EasyAbp/EShop (MIT) | 🟡 một phần |
| A2 Pricing | ✅ **Smartstore** (pipeline có ordering) + **GrandNode** (tier 4 chiều) + **nopCommerce** (`TierPrice` tách riêng, `PriceList` + priority + `ManualPrice`) + **EShop** (`IOrderLinePriceOverrider` = điểm cắm ghi đè giá theo dòng) | ✅ EShop extension point (MIT) + **OCC `MoneyDataType`** (MIT, có `GetFixedPointAmount` cho VND) | 🟢 đủ |
| A3 Promotions | ✅ **nopCommerce** — cây điều kiện **AND/OR** + rule plugin + mapping đa hình (**đáng bắt chước nhất**) + **EShop** `OrderDiscountResolver` BFS (⚠️ coi chừng bùng nổ tổ hợp) | ✅ EShop `IOrderDiscountProvider` + resolver (MIT) | 🟢 đủ |
| A4 Inventory | ✅ **GrandNode** (ledger + reserve/reverse) + **nopCommerce** (`StockQuantityHistory` có số dư sau) + **EShop** (abstraction provider + lock theo SKU); ❌ **Smartstore** (không có gì), ❌ **OCC** (1 số int + `SemaphoreSlim` sai cho multi-instance) | ✅ EShop `IProductInventoryProvider` + resolver (MIT) — nhưng **KHÔNG có ledger/reservation** ⇒ giữ thiết kế của ta | 🟢 đủ |
| A5 Cart/Checkout | ✅ nopCommerce (`GetShoppingCartItemUnitPriceEvent` ghi đè giá; tách `ShoppingCartService`) + **EShop `NewOrderGenerator`** (tính tiền + extra fee + discount) | ✅ EShop `NewOrderGenerator`/`OrderManager` (MIT) | 🟢 đủ |
| A6 Orders | ✅ **nopCommerce 3 trục trực giao** + mẫu `Can*`/`Xxx` (⚠️ **KHÔNG dùng `[Flags]` 4 giá trị của EShop**) + **GrandNode** (status là entity cấu hình được) + **Smartstore** (guard `Can*`) | ✅ EShop bất biến `Order`/`OrderLine`/`OrderDiscount`/`OrderExtraFee` + extension point `IOrderCompletableCheckProvider`/`IOrderPaymentChecker` (MIT) | 🟢 đủ |
| A7 Customers | ✅ nopCommerce (⚠️ **tránh** mô hình địa chỉ hai nguồn sự thật) — ⚠️ **EShop KHÔNG có địa chỉ giao hàng/sổ địa chỉ** ⇒ ta phải tự làm | *(chờ)* | 🟡 một phần |
| B1 đối soát thanh toán | ⚠️ nopCommerce chỉ có transaction id trên Order ⇒ tự thiết kế | — | 🟢 tự thiết kế |
| B2 Shipping | *(chờ khảo sát VN)* | *(chờ)* | ⏳ |
| B3 Tax/Invoice | *(chờ khảo sát VN)* — nopCommerce chỉ có PDF + attach email | *(chờ)* | ⏳ |
| B4 Settlement | *(không nền tảng nào có)* | — | 🟢 tự thiết kế |
| C3 CMS/SEO | nopCommerce chứng minh **không phải CMS** (`Domain/Cms` chỉ 1 file) ⇒ cân nhắc `Volo.CmsKit` (LGPL, dùng dạng package) | — | 🟢 |

---

## 7. ĐỀ XUẤT CUỐI CÙNG

### 7.1 Kết luận về nguồn (9 repo đã khảo sát)

| Nhóm | Repo | Kết luận |
|---|---|---|
| ✅ **Copy được** (MIT/BSD) | `EasyAbp/EShop` + `PaymentService` (MIT) · `OrchardCore.Commerce` (MIT) · `dotnet/eShop` (MIT) · `OrchardCore` lõi (BSD-3) | Được copy **kèm attribution**; không copyleft |
| 📛 **Chưa xác minh** | `SimplCommerce` | API/README ghi Apache-2.0 nhưng **không tìm thấy file LICENSE** ⇒ **không dùng cho tới khi xác minh** |
| 🚫 **Không copy được** | nopCommerce (**AGPL + "powered by" mọi màn hình**) · Smartstore (**AGPL-3.0**) · GrandNode 1 (**nopCommerce Public License v3.0**) · GrandNode 2 (**GPL-3.0**) | Chỉ **học ý tưởng**. AGPL kích hoạt cả khi chạy SaaS (§13) |

### 7.2 ⚠️ Không nguồn nào dùng được làm **hạt nhân commerce**

Đây là kết luận quan trọng nhất, và nó **ngược với kỳ vọng ban đầu** ("lấy module nghiệp vụ từ repo đã khảo sát"):

| Nguồn | Thiếu đúng thứ ta cần nhất |
|---|---|
| **nopCommerce** | Không có trục kênh; đối soát/settlement không có gì; `Product.cs` 606 dòng/`OrderProcessingService` 3.564 dòng ⇒ **không copy được (license)** và cũng không nên copy (thiết kế phình) |
| **Smartstore** | **Không có warehouse, không reservation, không ledger** ⇒ **không chống được oversell**; license AGPL |
| **GrandNode 2** | Có ledger + reservation (**học được**) nhưng **tiền là `double` ở ~30 trường**, license GPL ⇒ không copy |
| **Orchard Core Commerce** | **Không ledger, không reservation, không outbox**; tồn kho = 1 số `int` + `SemaphoreSlim` (sai cho multi-instance); **không có Shipping**; tự nhận *"initial design phases"* ⇒ **hạ tầng kho của ta tốt hơn nó** |
| **dotnet/eShop** | Là **reference app**, không phải nền tảng commerce: không catalog đầy đủ, không khuyến mãi, không thuế; single-tenant |
| **EasyAbp/EShop** | **Không ledger, không reservation, không tồn kho theo store/kênh, không thuế, không hoá đơn, không địa chỉ giao hàng, không RMA, không đa tiền tệ**; so tiền bằng `==`; port 1 module **kéo theo cả chuỗi module** |

⇒ **Đề xuất: TỰ XÂY kernel tinh gọn của ta** (đúng như đang làm), **học thiết kế từ tất cả**, và
**port một số ít class/pattern cụ thể từ nguồn MIT** — thay vì adopt module của bất kỳ ai.

### 7.3 Danh sách PORT cụ thể (đã kiểm tra license)

| # | Port gì | Nguồn (license) | Vì sao |
|---|---|---|---|
| 1 | **`Amount` + `Currency` + `IMoneyService`** (có `GetFixedPointAmount` cho **số nguyên VND**) | OCC `MoneyDataType` (**MIT**, không phụ thuộc OrchardCore) | Nền tảng tiền tệ đúng cho VN; tránh sai số kiểu EShop |
| 2 | **Guard trạng thái trong aggregate** (idempotent theo trạng thái, không cần bảng idempotency) | eShop `Order.cs` (**MIT**) | Chống trùng webhook sàn — kết hợp với unique index ta đã có |
| 3 | **Bảng inbox** (`ClientRequest` + `IRequestManager`) | eShop `Ordering.Infrastructure/Idempotency/` (**MIT**) | Nền chống trùng webhook — **phải thêm** `TenantId`, `ChannelCode`, `ExternalOrderId`, `EventType`, `PayloadHash` + unique index |
| 4 | **Extension point giá/phí/khuyến mãi** (`IOrderLinePriceOverrider`, `IOrderExtraFeeProvider`, `IOrderDiscountProvider` + `OrderDiscountResolver`) | EShop (**MIT**) | Điểm cắm **giá theo kênh**, phí ship, khuyến mãi — không phải sửa core |
| 5 | **Abstraction tồn kho + cách đặt tên lock key theo SKU** (`IProductInventoryProvider` + resolver) | EShop (**MIT**) | Chuẩn hoá provider kho; **giữ ledger của ta** (họ không có) |
| 6 | **Pipeline tính giá có priority** (`IPriceProvider`/`PrioritizedPrice`) | OCC (**MIT**) | Nền cho price list đa kênh |

**Quy ước bắt buộc khi port:** header `// Ported from <repo> (<license>, <copyright>) — <path> @ <commit>` +
ghi vào `THIRD-PARTY-NOTICES.md` §5. Chi tiết: xem `THIRD-PARTY-NOTICES.md`.

### 7.4 Danh sách PHẢI TỰ XÂY (không nguồn nào có)

| # | Thứ phải tự xây | Vì sao không ai có |
|---|---|---|
| 1 | **Chiều kênh (`ChannelId`) cho giá / tồn kho / đơn** | nopCommerce chỉ có `StoreId`; EShop `ProductInventory` **không có `StoreId`**; cả hai đều không có khái niệm sàn TMĐT |
| 2 | **`StockReservation` có TTL + theo kênh** | EShop không có reservation (dùng `ReduceAfterPlacing` thay thế); nopCommerce có reserved nhưng không có chiều kênh |
| 3 | **Settlement / đối soát 3 chiều** (sàn ↔ `PaymentHub` ↔ sao kê ngân hàng) | nopCommerce chỉ có transaction id trên Order; **không nền tảng nào có module này** |
| 4 | **Dedup ở consumer + job replay outbox** | eShop outbox chỉ at-least-once, **không có job retry/replay** |
| 5 | **Idempotency key ỔN ĐỊNH cho cổng thanh toán** | OCC sinh `Guid.NewGuid()` mỗi lần ⇒ **vô dụng** (anti-pattern) |
| 6 | **Bảo vệ state transition ở tầng DB** (`RowVersion` / `UPDATE … WHERE status = …`) | EShop guard chỉ ở bộ nhớ; OCC check-then-act không lock |
| 7 | **Hoá đơn điện tử VN + thuế GTGT** | Không nền tảng nước ngoài nào có |
| 8 | **Shipping VN (GHN/GHTK/ViettelPost) + COD** | OCC **không có Shipping**; nopCommerce có provider nhưng không phải đối tác VN |
| 9 | **Địa chỉ giao hàng + sổ địa chỉ khách** | **EShop không có**; nopCommerce có nhưng **hai nguồn sự thật** |

### 7.5 Quyết định tôi cần bạn chốt

| # | Câu hỏi | Đề xuất của tôi |
|---|---|---|
| 1 | **Port `MoneyDataType` của OCC hay tự viết `Money`?** | **Port OCC** (MIT, 796 dòng, có sẵn xử lý số nguyên VND) — nhanh và đã được test |
| 2 | **Làm đa tiền tệ ngay từ P1?** | **Không.** VND trước; thiết kế `Money { Amount, Currency }` để sẵn đường mở rộng, nhưng chỉ 1 tiền tệ hoạt động |
| 3 | **Port module nào của EShop nguyên khối?** | **Không port nguyên module nào** — chỉ port **pattern + vài class** (mục 7.3), vì port 1 module kéo theo chuỗi phụ thuộc chéo |
| 4 | **Thuê luật sư review trước khi port?** | **Có**, trước khi copy dòng code MIT/BSD đầu tiên vào repo — đặc biệt để chốt phạm vi copyleft của module ABP (LGPL) |
| 5 | **Ưu tiên P1: website trước hay sàn trước?** | **Website trước** (A1→A5→A6): đủ để bán và kiểm chứng oversell; `ChannelHub` đã có sẵn nên đấu sàn sau khi luồng đơn ổn định |
| 6 | **Tự viết UI admin hay mua ABP Pro?** | **Tự viết trên ABP free** trước; chỉ cân nhắc Pro (`SaaS`) khi làm multi-tenant nghiêm túc |
| 7 | 🚨 **Ký số hoá đơn: chấp nhận bước thủ công, hay đầu tư HSM/remote signing?** | **Chấp nhận bước thủ công trước** (token/USB = quy trình 2 bước của VNPT, **không tự động 100% được**); chỉ đầu tư HSM khi khối lượng hoá đơn/ngày đủ lớn để bù chi phí |
| 8 | **Địa chỉ: hỗ trợ cả 2 cấp mới và 3 cấp cũ, hay chỉ 2 cấp?** | **Hỗ trợ CẢ HAI** — vì GHTK vẫn dùng 3 cấp và **chưa xác minh được** cờ tương đương `is_new_to_address` (phải hỏi GHTK trước khi chốt) |

### 7.6 Trạng thái khảo sát

| Nhánh khảo sát | Trạng thái |
|---|---|
| Ma trận giấy phép 9 repo | ✅ xong |
| GrandNode 2 vs Smartstore (nghiệp vụ) | ✅ xong |
| nopCommerce (nghiệp vụ, 30 nhóm domain) | ✅ xong |
| Orchard Core Commerce + dotnet/eShop | ✅ xong |
| EasyAbp/EShop (tới mức aggregate) | ✅ xong |
| **Nghiệp vụ đặc thù VN** (hoá đơn điện tử, đối tác vận chuyển, đối soát COD, kế toán) | ✅ xong → §6.7 |
| Gap: eShop saga + giấy phép Lombiq | ✅ xong → §6.5 |

**Tất cả khảo sát đã hoàn tất.** Báo cáo nguồn đầy đủ nằm trong `D:\Projects\Tools\WF\`:
`license-matrix-abp-ecommerce-2026-10.md` · `_research/grandnode2-vs-smartstore-domain-survey.md` ·
`nopCommerce-domain-survey.md` · `khao-sat-orchard-commerce-va-eshop.md` ·
`easyshop-eshop-domain-survey.md` · `vn-specific-modules-survey.md` ·
`_research/eshop-orderprocessor-va-lombiq-license.md`.

⚠️ **Cần sửa `ecommerce-multichannel-plan.md`** (bản khảo sát trước): hoá đơn điện tử đang bị xếp ở
"Phase 3, mức Trung bình" — **sai**, NĐ 70/2025 đã có hiệu lực từ 01/06/2025 (xem §6.7).
