# Inventory kỹ thuật — Nhanh.vn POS Open API v3

> Khảo sát trực tiếp từ tài liệu chính thức: **https://developers.nhanh.group/pos/** (bản API v3.0, trang Change log ghi mốc mới nhất **2026-11-02**).
> Phương pháp: đọc trang giới thiệu + Change log + Model Constant, rồi bóc toàn bộ 73 trang con của mục POS từ `sitemap.xml` (`https://developers.nhanh.group/sitemap.xml`), cộng 6 trang hướng dẫn dùng chung (`/app/api-request/`, `/app/access-token/`, `/app/api-rate-limit/`, `/app/webhooks/`, `/app/webhooks/events/`, `/app/changelog/`).
> Nguyên tắc: chỉ ghi những gì quan sát được. Chỗ tài liệu không nói rõ → ghi rõ **"không thấy trong tài liệu"**.
> Không có file `openapi.json` / `swagger.json`: đây là site Docusaurus render HTML tĩnh (đã thử `pos/openapi.json` → lỗi). Toàn bộ nội dung lấy từ HTML.

---

## 0. Khung giao thức dùng chung (áp dụng cho cả 68 endpoint)

**Base URL:** `https://pos.open.nhanh.vn` — version path `/v3.0/...`
Nguồn: [Gọi API request](https://developers.nhanh.group/app/api-request/)

| Hạng mục | Nội dung quan sát được |
|---|---|
| Method | **POST toàn bộ** ("Version 3.0 sẽ dùng POST method"), body raw JSON, `Content-Type: application/json` |
| Query params bắt buộc | `appId` (int), `businessId` (int) — truyền trên URL |
| Header bắt buộc | `Authorization: <accessToken>` |
| Envelope thành công | `{"code":1, "data": ..., "paginator": {...}}`, có thể kèm `warning` (string/object/array) |
| Envelope lỗi | `{"code":0, "errorCode":"...", "messages": {...hoặc [...]}, "data": {...}}` |
| Phân trang | `paginator: {size, sort, next}`. `size` mặc định ≤ 100 (một số API ghi rõ giới hạn riêng: product/combo 50, invoice/list 50, inventory/archive 50, imeisold 50). `next` là **object/array có cấu trúc riêng từng API** (`{"id":100}`, `{"totalRevenue":..,"productId":..}`, hoặc string) và **phải truyền lại đúng hình dạng**; `next` null/không có = hết dữ liệu. Trang có thể trả ít hơn `size` mà vẫn còn `next`. |
| Rate limit | 150 requests / 30 giây, khoá theo **appId + businessId + API URL**. Lỗi `ERR_429` / `ERR_EXCEEDED_RATE_LIMIT` trả `{"data":{"lockedSeconds":10,"unlockedAt":<ts>}}`. Không hỗ trợ mở khoá. |
| accessToken | OAuth2 authorization-code: `https://nhanh.vn/oauth?appId=&returnLink=` → `accessCode` (sống 10 phút) → `POST /v3.0/app/getaccesstoken?appId=` với body `{accessCode, secretKey}`. Token sống **1 năm**, **không có refresh token**. v3.0 chỉ giữ **1 token / (appId+businessId)**: tạo token mới thì token cũ chỉ còn hiệu lực 15 phút. Response trả `depotIds`, `pageIds`, `permissions`, `expiredAt`. Có `POST /v3.0/app/checkaccesstoken` để kiểm tra quyền/kho. |
| Mã lỗi chung | `ERR_INVALID_APP_ID`, `ERR_INVALID_ACCESS_TOKEN`, `ERR_INVALID_VERSION`, `ERR_INVALID_BUSINESS_ID`, `ERR_INVALID_DEPOT_ID`, `ERR_EXCEEDED_RATE_LIMIT`/`ERR_429`, `ERR_BUSINESS_NOT_ENABLE_API`, `ERR_INVALID_FORM_FIELDS` (kèm map field→message), `ERR_INVALID_DATA` (JSON hỏng), `ERR_403` (token thiếu quyền). |
| Mã lỗi riêng của POS | Trang `/app/api-request/` có **heading "POS common error codes" nhưng bảng rỗng** — tức là **không thấy trong tài liệu**. Mã lỗi riêng chỉ xuất hiện rải rác ở từng API (xem cột "mã lỗi riêng" bên dưới). |
| Phân quyền | Quyền gắn cứng vào token tại thời điểm cấp quyền (không tự cập nhật khi phân quyền nhân viên thay đổi). `depotIds` rỗng/`All` = toàn bộ kho. Đây là nguồn gốc lỗi "tồn kho trả về ít hơn giao diện". |

---

## 1. Bảng tổng hợp nhóm API

| # | Nhóm | Số endpoint | Entity chính | Trang gốc |
|---|---|---|---|---|
| 1 | Business | 5 | Depot (kho), Supplier (NCC), Department (phòng ban), User (nhân viên) | https://developers.nhanh.group/pos/business/depot/ |
| 2 | Product | 19 | Product, ProductUnit, Batch (lô), IMEI, Combo, Category, Brand, Tag, Gift, SupplierProduct | https://developers.nhanh.group/pos/product/list/ |
| 3 | Order | 10 | Order (info/channel/shippingAddress/products/carrier/payment/gifts), OrderLog, OrderSource, OrderTag, Complaint | https://developers.nhanh.group/pos/order/list/ |
| 4 | Shipping | 3 | Location (địa chỉ 3 cấp & 2 cấp), Carrier + Service, ShippingFee | https://developers.nhanh.group/pos/shipping/location/ |
| 5 | Bill | 7 | Bill (phiếu XNK), BillImex (dòng sản phẩm XNK), DraftBill | https://developers.nhanh.group/pos/bill/list/ |
| 6 | Inventory | 5 | Phiếu nhập/xuất NCC & khác, InventoryLimit (hạn mức tồn) | https://developers.nhanh.group/pos/inventory/importsupplier/ |
| 7 | Invoice | 1 | EInvoice (hoá đơn điện tử) | https://developers.nhanh.group/pos/invoice/list/ |
| 8 | Customer | 3 | Customer, CustomerTag | https://developers.nhanh.group/pos/customer/list/ |
| 9 | Promotion | 8 | CouponBatch, Coupon, PriceList, PriceListProduct | https://developers.nhanh.group/pos/promotion/batch/ |
| 10 | Accounting | 3 | Transaction (bút toán), Account (tài khoản kế toán), Debt (công nợ) | https://developers.nhanh.group/pos/accounting/transaction/ |
| 11 | Zalo | 2 | ZBS/ZNS template message | https://developers.nhanh.group/pos/zalo/zbs/ |
| 12 | Ecom | 2 | EcomShop, EcomReturn | https://developers.nhanh.group/pos/ecom/shop/ |
| 13 | Webhooks | 8 event (POS) + 2 event (App) | — (không phải endpoint) | https://developers.nhanh.group/pos/webhooks/order/ |
| | **Tổng** | **68 endpoint + 10 webhook event** | | |

**Đặc điểm bao trùm:** chỉ có **1 API ghi (write) cho Order** (`add`, `edit`), **1 cho Bill** (`addretail`), **1 cho Product** (`add`, `edit`, `externalimage`), **1 cho Customer** (`add`). **Không có DELETE chuẩn** — chỉ có endpoint "danh sách đã xoá" (`order/deleted`, `bill/deleted`) để đồng bộ trạng thái xoá. Không có API xoá/sửa phiếu kho, không có API sửa đơn vị tính / lô / IMEI.

---

## 2. Chi tiết từng nhóm

### 2.1 Business — 5 endpoint
Nguồn: https://developers.nhanh.group/pos/business/depot/ · `/supplier/` · `/addsupplier/` · `/department/` · `/user/`

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/business/depot` | Danh sách kho hàng |
| POST | `/v3.0/business/supplier` | Danh sách nhà cung cấp |
| POST | `/v3.0/business/addsupplier` | Thêm mới nhà cung cấp |
| POST | `/v3.0/business/department` | Danh sách phòng ban |
| POST | `/v3.0/business/user` | Danh sách nhân viên |

**Depot** — filter: `ids[]`. Trả: `id, code, name, mobile, location{cityId,cityName,districtId,districtName,wardId,wardName,address}`, **`locationV2`** `{cityId,cityName,wardId,wardName,address}` (địa chỉ 2 cấp — thêm 2026-05-30). Khuyến cáo cache 24h.

**Supplier** — filter: `id`. Trả: `id, name, mobile`. (API list rất mỏng.)

**AddSupplier** — body: `name*`, `code, mobile, email, address, taxCode, typeId, personalId, note, status`, `bank{name,branch,accountNumber,accountHolder}`.
Enum `typeId`: **1 = Cá nhân, 2 = Doanh nghiệp**. `status`: **1 = Đang giao dịch, 2 = Ngừng giao dịch**.

**Department** — filter: `name, code, status` (1 Active / 2 Inactive). Trả: `id, parentId, code, name, status, createdAt` → **cây phòng ban**.

**User** — filter: `ids[]` (≤100). Trả: `id, username, fullName, email, mobile, roleName, depots[{id,name}]`.
Quy tắc quan trọng: `depots` rỗng ⇒ user thao tác **toàn bộ kho**; có giá trị ⇒ **chỉ các kho đó**.

---

### 2.2 Product — 19 endpoint
Nguồn: https://developers.nhanh.group/pos/product/{list,detail,add,edit,batch,brand,category,internalcategory,combo,expire,externalimage,gift,imei,imeihistory,imeisold,inventory,supplierproduct,tags,unit}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/product/list` | Danh sách sản phẩm (kèm tồn kho tổng hợp) |
| POST | `/v3.0/product/detail` | Chi tiết 1 sản phẩm |
| POST | `/v3.0/product/add` | Thêm sản phẩm (**mảng, ≤100/request**) |
| POST | `/v3.0/product/edit` | Sửa sản phẩm (**1 object, không phải mảng**) |
| POST | `/v3.0/product/batch` | Danh sách lô (sản phẩm bán theo lô) |
| POST | `/v3.0/product/brand` | Danh sách thương hiệu |
| POST | `/v3.0/product/category` | Danh mục sản phẩm |
| POST | `/v3.0/product/internalcategory` | Danh mục nội bộ |
| POST | `/v3.0/product/combo` | Danh sách sản phẩm combo + thành phần |
| POST | `/v3.0/product/expire` | Hạn sử dụng tồn kho theo lô/kho |
| POST | `/v3.0/product/externalimage` | Gắn URL ảnh ngoài (CDN riêng) |
| POST | `/v3.0/product/gift` | Quà tặng theo sản phẩm (từ chương trình KM) |
| POST | `/v3.0/product/imei` | Danh sách IMEI |
| POST | `/v3.0/product/imeihistory` | Lịch sử luân chuyển 1 IMEI |
| POST | `/v3.0/product/imeisold` | IMEI đã bán ra theo ngày |
| POST | `/v3.0/product/inventory` | **Danh sách tồn kho (nhẹ, lọc theo thời gian đổi tồn)** |
| POST | `/v3.0/product/supplierproduct` | Map sản phẩm ↔ nhà cung cấp |
| POST | `/v3.0/product/tags` | Nhãn sản phẩm (màu nền/chữ) |
| POST | `/v3.0/product/unit` | Đơn vị tính của sản phẩm nhiều ĐVT |

**Field chính của Product (theo `detail`, đầy đủ nhất):**
`id, parentId, code, barcode, name, otherName, status, category{id,code,name}, internalCategory{...}, prices{retail, import, old, wholesale, avgCost}, vat, images{avatar, others[]}, warranty{month, phone, address}, brand{id,name}, type{id,name}, shipping{length,width,height,weight}, countryName, units{name, list[{id,name,quantity,price{retail,import,wholesale}}]}, combos[{id,code,name,quantity}], inventory{...}, attributes[{id,name,value,order,orderValue}], description, content, suppliers{id,name,mobile}, videos[{title,src}], branchPrices[{branch{id,name}, price, wholesalePrice}], childs[], tagIds[], updatedAt, createdAt`.
`list` trả: `id, parentId, code, barcode, name, otherName, status, priceVatMode, importPriceVatMode, categoryId, internalCategoryId, prices{retail, retailVat, import, importVat, old, wholesale, avgCost}, images, warranty, brandId, type, shipping, countryName, units, combos, inventory, attributes, updatedAt, createdAt`.
**Khác `detail`:** `list` **không** trả `vat`, `description`, `content`, `childs`; `category`/`internalCategory`/`brand`/`type` ở `list` là **ID phẳng** (`categoryId`, `internalCategoryId`, `brandId`, `type`) còn `detail` là **object `{id, name, code}`**; `attributes` ở `list` có thêm `nameOther`, `code`, `codeOther` nhưng **không có** `order`/`orderValue` như `detail`.

**`inventory` object — mô hình tồn kho nhiều "ngăn" (rất quan trọng):**
`remain` (tồn), `shipping` (đang giao), `damaged` (lỗi), `holding` (tạm giữ), `transfering` (đang chuyển kho), `warranty` (bảo hành), `warrantyHolding` (tạm giữ bảo hành), `holdingNew` (tạm giữ loại xuất S), `shippingExport` (đang chuyển loại xuất S), `available` (có thể bán), `depots[]` (cùng bộ field theo từng kho).
`product/inventory` trả `warranty` dạng object `{remain, holding}` thay vì 2 field phẳng, và **không** có `holdingNew`/`shippingExport` → **shape tồn kho không đồng nhất giữa 2 API**: `product/detail` + webhook `product*` dùng **field phẳng** (`warranty`, `warrantyHolding`, `holdingNew`, `shippingExport`) × 10 khóa; `product/list` + `product/inventory` + webhook `inventoryChange` dùng **`warranty{remain,holding}`** × 7 khóa.

**Enum Product:**
- `type`: **1** Loại sản phẩm · **2** Voucher · **3** Cân đo · **4** Theo IMEI · **5** Gói sản phẩm · **6** Dịch vụ · **7** Dụng cụ · **8** Bán theo lô · **9** Combo · **10** Nhiều đơn vị tính.
- `status`: **1** Mới (chỉ bán offline) · **2** Đang bán (online + offline) · **3** Ngừng bán (ẩn cả 2, không hiện gợi ý) · **4** Hết hàng (vẫn hiện web, chặn đặt, giữ link tránh 404).
- `priceVatMode` / `importPriceVatMode`: **1** giá đã gồm VAT · **2** giá chưa gồm VAT (mặc định). Công thức tài liệu: chưa gồm ⇒ giá bán = giá + VAT; đã gồm ⇒ giữ nguyên.
- `vat`: số % thường (5, 8, 10) hoặc **-1** không chịu thuế · **-2** không kê khai nộp thuế · **-100** thuế bằng 0.
- `parentId`: **-1** sản phẩm độc lập · **-2** sản phẩm cha. Filter `gpid`: **-1** độc lập · **-2** cha · **1** con · **2** cha + độc lập · **3** con + độc lập.
- `batch.status`: 1 Active / 2 Inactive. `brand/category/tags status`: 1 Active / 2 Inactive.
- `imei.status`: **1** Mới · **2** Đã bán · **3** Đang vận chuyển · **5** Lỗi · **6** Đã trả NCC · **7** Tồn trong cửa hàng · **8** Đang chuyển kho · **9** Đang bảo hành · **10** Đã trả bảo hành · **11** Hàng Demo (không có giá trị 4).

**Quy tắc nghiệp vụ đáng chú ý:**
- `list`/`inventory`: **`updatedAt` KHÔNG bao gồm thay đổi tồn kho** — muốn theo dõi tồn phải dùng `inventoryChange` webhook hoặc `/product/inventory` với `updatedAtFrom/To`.
- `add`: ≤100 sản phẩm/request, `appProductId*` là khoá idempotency do phía bạn sinh.
- `edit`: `brandId` không truyền / 0 / null ⇒ **giữ nguyên** thương hiệu (khác `add` ⇒ không gán).
- `tagIds`: tối đa **30 nhãn** (cả request với `add`, mỗi sản phẩm với `edit`).
- `externalimage`: ≤30 sản phẩm/request, ≤20 ảnh/sản phẩm; `mode: update|deleteall`; Nhanh **không copy ảnh** mà dùng trực tiếp URL (dùng cho Facebook Shop, cần ≥500×500px).
- `imeihistory.itemType`: **1** Hoá đơn · **2** Hoá đơn khách hàng · **3** Đơn hàng · **4** Bảo hành · **5** Chuyển kho.
- `expire`: lọc theo `billId, depotId, categoryId, internalCategoryId, expiredDateFrom/To, daysWarningFrom/To` → trả `depot, product, billId, quantity, expiredDate, priorWarningDays`.
- `unit.quantity` = hệ số quy đổi; `product/combo` phân trang ≤50.
- Tồn kho trả về phụ thuộc `depotIds` trong accessToken (lỗi "thiếu tồn" được tài liệu giải thích rõ).

---

### 2.3 Order — 10 endpoint
Nguồn: https://developers.nhanh.group/pos/order/{list,add,edit,history,deleted,source,tags,sendcarrier,cancelCarrier,addcomplain}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/order/list` | Danh sách đơn hàng (đầy đủ cấu trúc lồng) |
| POST | `/v3.0/order/add` | Tạo đơn hàng |
| POST | `/v3.0/order/edit` | Cập nhật đơn (info / địa chỉ / carrier / payment) |
| POST | `/v3.0/order/history` | Lịch sử thao tác đơn |
| POST | `/v3.0/order/deleted` | Danh sách đơn **đã xoá** |
| POST | `/v3.0/order/source` | Danh sách nguồn đơn hàng |
| POST | `/v3.0/order/tags` | Danh sách nhãn đơn hàng |
| POST | `/v3.0/order/sendcarrier` | Gửi đơn sang hãng vận chuyển (mới 2026-11-02) |
| POST | `/v3.0/order/cancelCarrier` | Huỷ vận chuyển (curl ghi `cancelcarrier` — path viết thường) |
| POST | `/v3.0/order/addcomplain` | Tạo khiếu nại vận chuyển |

*(Không có `order/detail` trong tài liệu — chi tiết đơn phải lấy qua `list` với filter `ids`.)*

**`order/list` — cấu trúc response 7 khối:**
`data[] = { info, channel, shippingAddress, products[], carrier, payment, gifts[] }`

- **info**: `id, depotId, pickupDepotId, type, mode (loại giá bán), exportType (đã xuất kho thủ công?), originalOrderId, returnFromId, idReturn, createdById/Name, saleId/Name, packedById/Name, customerCareId/Name, createdAt, updatedAt, confirmedAt, packedAt, description, privateDescription, orderIndex, status, reason, handoverIds[], trackingUrl, tags[{id,name,textColor,bgColor,status}]`.
- **channel**: `appOrderId, saleChannel, trafficSource, pageId, conversationId, fbAdsId, fbPsid, domain, shopId, shopName, affiliate{code,value,bonus,bonusRate}, marketting{utmSource,utmMedium,utmCampaign}` (lưu ý typo `marketting` trong doc).
- **shippingAddress**: `id, code, name, mobile, email, address, birthday, cityId, districtId, wardId, companyName, companyAddress, taxCode` — đây thực chất là **snapshot + tham chiếu Customer**.
- **products[]**: `productId, orderProductId, typeId, code, name, appProductId, weight, quantity, price, originalPrice, priceVatMode, priceAfterVAT, discount, discountAmount, avgCost, vatPercent, description, imeiId, imei, imeiIds[], imeiCodes[], priceListName, transactionFee, paymentStatus, partialReturnStatus, usedPoints, usedPointAmount, batch{id,name,manufactureDate,expiredDate}, unit{id,name,quantity,price,priceAfterVAT}, partialDelivery{deliveredQuantity, deliveredUnitQuantity, returnQuantity, returnUnitQuantity, lostQuantity, lostUnitQuantity}, combos[], gifts[], totalAmount`.
- **carrier**: `id, name, service{id,code,name,type,typeName}, carrierCode, fullCarrierCode, carrierSortCode, packageCode, checkGoods, sendCarrierType, sendCarrierAt, deliveryAt, isPartDelivery, isDeclaredFee, declaredValue, declaredFee, carrierPromotionCode, weight, shipFee, codFee, overWeightShipFee, returnFee, customerShipFee, ecomFee, carrierPaymentStatus`.
- **payment**: `usedPoints{points,amount}, vat{percent,amount,taxCode,taxDate}, discount{percent,amount}, couponCode, debtDueDate, deposit{amount,accountId,name}, transfer{amount,accountId,name}, credit{amount,accountId,code,name}, codAmount, businessPayment, businessPaymentStatus`.
- **gifts[]**: `productId, orderProductId` (**>0 = quà theo sản phẩm; =0 = quà theo đơn**), `name, quantity, price, batch, unit, imeiCodes[]`.

**Công thức `codAmount` (doc ghi nguyên văn):** `Tổng thu khách = Tổng tiền hàng + Phí ship báo khách − Chiết khấu − Chuyển khoản − Đặt cọc − Tiêu điểm`. Đơn trạng thái Đang hoàn / Xác nhận hoàn / Đã hoàn ⇒ **codAmount = 0**. Đơn loại "Khách trả lại hàng" ⇒ codAmount **âm** = Tổng tiền hàng − (customerShipFee + Chiết khấu).

**`order/add` — body 6 khối:** `info, channel, shippingAddress, carrier, products[], payment, invoice`
- `info`: `depotId, type (default 1), saleId, createdById, description, privateDescription, status` (**chỉ cho phép Mới / Chờ xác nhận / Đang xác nhận / Đã xác nhận**), `tagIds[]`.
- `channel`: `appOrderId*` (**chống trùng theo appId+appOrderId**), `sourceName, affiliate{code,value,bonus}, marketing{utmCampaign,utmSource,utmMedium}`.
- `shippingAddress`: `name*, mobile*, email, address, cityId, districtId` (bắt buộc nếu 3 cấp), `wardId`, `locationVersion` (**v1** 3 cấp / **v2** 2 cấp).
- `carrier`: xem enum + quy tắc 2 hình thức kết nối bên dưới; thêm `allowTest, deliveryDate, deliveryTime, customerShipFee, isPartDelivery, isDeclaredFee, declaredValue, autoSend, extraServices, orderPackage{weight,length,width,height,merge}, feeCustomerReject, isBrokenGoods`.
- `products[]` (≤**500** dòng): `id*, price*, quantity*, imeis[], vat, priceVatMode, discount, description, gifts[{id,quantity,price}]`.
- `payment`: `depositAmount, depositAccountId, transferAmount, transferAccountId, couponCode, usedPoints, discountAmount, discountType (cash|percent)`.
- `invoice`: `tax{type, code, name, address, citizenIdNumber, passportNumber}` + `contact{name, mobile, email, email2}`.

**Quy tắc nghiệp vụ đặc thù (rất đáng chú ý khi redesign):**
1. **Hai hình thức kết nối vận chuyển** — `sendCarrierType`: **1** = dùng bảng giá sẵn của Nhanh (bắt buộc `id, serviceId`, và phải truyền `info.depotId`); **2** = tài khoản riêng của shop (`serviceCode, accountId, shopId?`). Đây là khái niệm **không tồn tại** trong Shopify-style.
2. **Chiết khấu là "ngoại vi"**: `couponCode` **chỉ ghi nhận**, Nhanh **không tự tính** tiền giảm từ coupon; app phải tự tính và nhồi vào `payment.discountAmount`.
3. `discount` theo đơn khi lưu sẽ **được phân bổ tỉ lệ ngược về từng dòng sản phẩm**.
4. Gắn `depotId` ⇒ hệ thống **tự tính lại `holding` (tạm giữ)** như đơn tạo từ admin (ví dụ tồn 10, tạm giữ 1, thêm đơn 1 ⇒ tạm giữ 2).
5. `autoSend=1` ⇒ gửi luôn sang HVC, trả `carrierCode`, **response chậm** tuỳ hãng.
6. `order/edit`: đơn **đã gửi HVC** hoặc **Thành công / Đã hoàn** ⇒ **không được sửa**; **chưa hỗ trợ sửa sản phẩm** trong đơn; đơn sàn (Shopee/Tiktok/Lazada) **không sửa được carrier & payment**. Truyền field rỗng = **xoá dữ liệu** (phải truyền delta).
7. `order/list` chỉ lấy **trong 31 ngày**; bắt buộc `createdAtFrom/To` trừ khi lọc theo `ids`, `customerId/mobile`; `deliveryAt*` và `updatedAt*` cũng giới hạn 31 ngày.
8. `order/list` trả **toàn bộ đơn** (kể cả đơn sàn) — giao diện Nhanh tách trang riêng theo sàn, nên hay gây hiểu nhầm "thiếu/thừa đơn".
9. `sendcarrier`: `ERR_DATA_404` (đơn không tồn tại). `cancelCarrier`: `ERR_ORDER_CARRIER_NOT_YET_SENT`, `ERR_ORDER_CARRIER_NO_API_CANCEL`, `ERR_ORDER_CARRIER_REJECT`.
10. `addcomplain.reason`: 6 Lý do khác · 7 Yêu cầu thanh toán COD · 8 Báo hoàn hàng · 9 Báo phát lại · 10 Yêu cầu miễn giảm cước/bồi thường · 33 Báo lưu kho bưu cục · 34 Yêu cầu cập nhật lại trạng thái · 35 Báo huỷ đơn · 36 Thay đổi thông tin đơn/đổi COD · 37 Báo thu gom · 38 Giao hàng một phần.
11. `history`: filter `type` = `loguser` (thao tác trên Nhanh) | `logcarrier` (từ HVC), `steps[]`, `createdAtFrom/To`; trả `status{old,new}`.

**Enum Order (đầy đủ — nguồn [Model Constant](https://developers.nhanh.group/pos/model-constant/)):**

- **saleChannel**: 1 Admin · 2 Website · 10 API · 20 Facebook · 21 Instagram · 41 Lazada · 42 Shopee · 43 Sendo · 45 Tiki · 48 Tiktok Shop · 49 Zalo OA · 50 Shopee chat · 51 Lazada chat · 52 Zalo cá nhân.
- **carrier.id**: 2 Viettel · 5 Giao hàng nhanh (GHN) · 8 Giao hàng tiết kiệm (GHTK) · 12 Tự vận chuyển · 18 Ahamove · 22 Việt Nam Post · 24 J&T Express · 25 EMS · 26 Best Express · 27 NinjaVan · 28 SuperShip · 29 SPX · 30 LEX · 31 Grab.
- **type**: 1 Giao hàng tận nhà · 2 Mua tại quầy · 3 Đặt trước · 5 Đổi quà · 10 Xin báo giá · 12 Đổi sản phẩm · 14 Khách trả lại hàng · 15 Hàng chuyển kho · 16 Đơn hoàn một phần · 17 Đền bù mất hàng.
- **status**: 40 Đã đóng gói · 42 Đang đóng gói · 43 Chờ thu gom · 54 Đơn mới · 55 Đang xác nhận · 56 Đã xác nhận · 57 Chờ khách xác nhận · 58 HVC huỷ đơn · 59 Đang chuyển · 60 Thành công · 61 Thất bại · 63 Khách huỷ · 64 Hệ thống huỷ · 68 Hết hàng · 71 Đang chuyển hoàn · 72 Đã chuyển hoàn · 73 Đổi kho xuất hàng · 74 Xác nhận hoàn. *(dải số không liên tục — không có 41, 44–53, 62, 65–67, 69–70)*
- **reason**: 1 Đặt nhầm · 2 Phí ship cao · 3 Không muốn chuyển khoản · 4 Đơn trùng · 5 Không gọi được khách · 6 Hết hàng · 8 Chờ chuyển khoản · 9 Khách không thích · 10 Không hài lòng NV vận chuyển · 11 Giao chậm · 12 Đã mua tại cửa hàng · 14 Sai địa chỉ · 16 Không muốn mua nữa · 18 Lý do khác · 19 Không liên hệ được người gửi · 20 Người gửi không bán online/ngoại tỉnh · 22 Không bàn giao cho HVC · 23 HVC lấy hàng muộn · 24 Sai địa chỉ kho lấy · 25 HVC làm mất hàng · 26 Người gửi tự vận chuyển · 29 Người gửi không xử lý đơn · 30 Sai giá · 35 Khách đi vắng.
- **step** (hành động, 58 giá trị): 1 Tạo đơn · 2 Xác nhận · 3 In · 4 Vận chuyển · 6 Sửa · 7 Đổi trạng thái · 8 Gửi HVC · 9 Cập nhật tiền chuyển khoản · 10 Đổi kho · 11/12 Thêm/xoá khỏi biên bản · 13 HVC cập nhật trạng thái · 14 Đổi HVC · 15 Báo HVC huỷ · 16 HVC trả mã · 17 Xoá đơn · 18 Trả trạng thái & khối lượng · 19 Thêm nội dung · 20 API cập nhật · 21 HVC đã nhận hàng · 22/23 Gộp đơn (lỗi) · 25 Gửi HVC lỗi · 26 Đổi mã vận đơn · 27 Lấy trạng thái HVC · 28 Đổi trạng thái từ biên bản · 29 Tour → biên bản · 30 Lỗi huỷ đơn HVC · 31 Lỗi lấy lịch trình · 32 Vượt cân · 33 Đối soát lỗi · 34 Import đơn · 35 Import trạng thái · 36 Đang đối soát · 37 Đã đối soát · 38 Tạo hoá đơn bán lẻ · 39 Chuyển sang bán sỉ · 40 Chuyển kho · 41 Copy đơn · 42 Đóng gói · 43 Cập nhật NV đóng gói · 44 Tạo đơn giao 1 phần · 45 Xoá khỏi tour · 46 Tạo link thanh toán · 47/48 Tạo phiếu xuất/nhập kho · 49/50 Xoá phiếu xuất/nhập · 51 Gửi sang Vpage · 52 Huỷ ghép giao dịch CK · 53 Nhập hoàn kho · 54 Tách đơn · 55 Gửi hàng đa điểm · 56 Sửa COD · 57 Cập nhật NV bán hàng · 58 Tạo hoá đơn điện tử.
- **allowTest** (carrier): 1 Cho xem hàng, không cho thử · 2 Cho phép thử · 3 Không cho xem · 4 Cho xem, không thu ship.
- **extraServices** (chỉ `sendCarrierType=2`): BEST `isReturnGoods=1`; Viettel `isDocument=1` (loại thư từ) / `handDelivery=1` (phát tận tay).

---

### 2.4 Shipping — 3 endpoint
Nguồn: https://developers.nhanh.group/pos/shipping/{location,carrier,fee}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/shipping/location` | Danh sách tỉnh/huyện/xã (3 cấp & 2 cấp) |
| POST | `/v3.0/shipping/carrier` | Danh sách hãng vận chuyển + dịch vụ |
| POST | `/v3.0/shipping/fee` | Tính phí vận chuyển |

**location** — filter `locationVersion*` (**v1** 3 cấp / **v2** 2 cấp), `type*` (**CITY | DISTRICT | WARD**, mặc định CITY), `parentId` (DISTRICT ⇒ id city; WARD ⇒ id district nếu v1, id city nếu v2). Trả `id, name, otherName`.
Ghi chú nghiệp vụ VN rất đáng chú ý: **toàn bộ HVC vẫn dùng địa chỉ 3 cấp**, chỉ một số ít hỗ trợ 2 cấp, **chưa hãng nào bắt buộc 2 cấp**; khuyến cáo tiếp tục dùng 3 cấp.

**carrier** — trả `id, name, logo, status (1 hoạt động / 2 không), shortName, services[{id,name,description}]`.

**fee** — filter: `type*` (1 = bảng giá Nhanh / 2 = tài khoản shop), `depotId, shippingWeight* (gram), price, totalCod`, `carrier{id*, service, followTotalFee, isDeclaredValue, declaredValue, promotionCode, ids[], extraServices}`, `shippingFrom*`, `shippingTo*` (`cityId*, districtId*, wardId*, address, locationVersion*`).
Trả về mảng theo từng hãng/dịch vụ: `logo, carrier{id, accountId, accountName, shopId}, service{id,name,code,description}, shipFee, codFee, declaredFee, customerShipFee`.
→ Đây là API "rate" theo mô hình VN: **shipFee + codFee + declaredFee** tách bạch, cộng thêm `customerShipFee` (phí báo khách, có thể = 0 nếu freeship).

---

### 2.5 Bill (phiếu xuất nhập kho) — 7 endpoint
Nguồn: https://developers.nhanh.group/pos/bill/{list,retail,addretail,deleted,draftlist,draftproducts,imexs}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/bill/list` | Danh sách phiếu XNK **mọi loại** (NCC, bán lẻ, bán sỉ, chuyển kho, kiểm kho) |
| POST | `/v3.0/bill/retail` | Danh sách **chỉ hoá đơn bán lẻ** (quyền hẹp hơn) |
| POST | `/v3.0/bill/addretail` | Tạo hoá đơn bán lẻ (≤100 sản phẩm, có thể tạo HĐĐT) |
| POST | `/v3.0/bill/deleted` | Phiếu đã xoá |
| POST | `/v3.0/bill/draftlist` | Danh sách **phiếu nháp** |
| POST | `/v3.0/bill/draftproducts` | Dòng sản phẩm của phiếu nháp |
| POST | `/v3.0/bill/imexs` | **Dòng sản phẩm xuất nhập kho** (flat, để đối soát/biên bản) |

**`bill/list` filters:** `ids[] (≤100), type (1 nhập / 2 xuất), modes[], depotIds[], orderIds[] (≤100), customerId, customerMobile, fromDate, toDate (yyyy-mm-dd)`. `dataOptions`: `["giftProducts","tags"]`. Giới hạn **31 ngày**, sort mặc định `date DESC, id DESC`.

**Bill object (giàu nhất trong toàn bộ API):**
`id, orderId, requirementBillId (phiếu nháp), inventoryCheckId (kiểm kho), warrantyBillId, depotId, date, type, mode,`
`related{type, parentId, billId, businessId, depotId, requirementBillId, userId, userName},`
`customer{id,name,mobile,address}, created{id,name,createdAt}, sale{id,name,saleBonus}, technicalStaff{id,name}, supplier{id,name},`
`products[{imexId, id, code, barcode, name, imeiId, imei[], quantity, price, discount, extendedWarrantyAmount, vat{percent,amount}, amount, giftProducts[]}],`
`payment{amount, customerAmount, discount, points, usedPoints{points,amount}, transfer{accountId,name,amount}, cash{...}, installment{accountId,name,amount}, credit{accountId,name,code,amount}, coupon{code,value,amount}, returnFee, returnAmount},`
`description, customerSource{id,name}, tags[]`
(`retail` trả thêm `createdById, createdAt`, `priceVatMode` theo dòng, `supplierId` phẳng.)

**`addretail` body:** `depotId*`, `customer{id,name,mobile,email,address,facebookLink,cityLocationId,districtLocationId,wardLocationId,birthday,gender,groupId,businessName,businessAddress,taxCode,description,tagIds}`, `saleId, technicalStaffId, description, tagIds[]`, `products[{id*, quantity*, priceVatMode, price, discount{type:percent|cash, value}, vat, imeis[], unitId, batchId, extendedWarrantyId, extendedWarrantyMonths, extendedWarrantyMoney, description}]`, `payment{usedPoints, customerAmount, cash{accountId,value}, transfer{...}, credit{accountId,value,code}, installment{id,value,code}, debtDueDate}`, `invoice{publishType, tax{...}, contact{...}}`.
**Giới hạn của addretail (doc nói rõ):** chỉ chiết khấu **theo sản phẩm**; **không** chiết khấu tổng đơn, **không** tự tính chiết khấu; **không** nhận `couponCode`; **không** hỗ trợ dòng quà tặng (phải tạo sản phẩm giá 0); **không** kiểm tra cài đặt bắt buộc nhập khách hàng/nhân viên.
Mã lỗi riêng: `ERR_INVOICE_NO_SETTING`, `ERR_INVOICE_PRODUCT_NO_DECLARED_VAT`, `ERR_INVOICE_PRODUCT_NO_MATCH`, `ERR_INVOICE_BLOCK_TAG`, `ERR_INVOICE_BLOCK_PRODUCT`.

**`imexs`** — filter `id, fromDate, toDate, billId, type, mode, depotId, relatedDepotId, warrantyBillId, orderId/orderIds, supplierId, product{id, relatedId, categoryId, internalCategoryId, brandId, typeId, imei, imeiId}`. Trả `billId, type, mode, date, reference{orderId, requirementBillId, inventoryCheckId, warrantyBillId}, depot{id,name}, relatedDepot{id,name}, supplier{id,name,mobile}, product{id,code,barcode,name,prices{retail,import,avgCost}}, imei{id,code}, batch{id,name,manufactureDate,expiredDate}, avgCost, productPrice, quantity, vat, vatPercent, discount, description, points, usedPoints, usedPointAmount, createdAt, saleBonus, extendedWarranty{id,amount,month}, warrantyReasonId`.

**`draftlist`/`draftproducts`** — phiếu nháp có vòng đời duyệt 3 bước: `created`, `approved`, `confirmed` (mỗi bước có userId + timestamp). `draftproducts` trả song song 3 bộ số lượng: `required{quantity,price,description,userId,userName,datetime}`, `approved{...}`, `confirmed{quantity,confirmedAt}` + `damagedQuantity`. → mô hình **workflow phê duyệt nhập/xuất** không có trong eCommerce thường.

**Enum Inventory (Model Constant):**
- `type`: **1** nhập kho · **2** xuất kho.
- `mode`: 1 Giao hàng · 2 Bán lẻ · 3 Chuyển kho · 4 Quà tặng ở HĐ bán lẻ · 5 Nhà cung cấp · 6 Bán sỉ · 8 Kiểm kho · 10 Khác · 13 Phiếu bảo hành · 14 Trung tâm bảo hành · 15 Phiếu sửa chữa · 16 Linh kiện bảo hành · 17 Quà tặng ở HĐ bán sỉ · 18 Quà tặng ở đơn hàng · 19 Combo. *(không có 7, 9, 11, 12)*
- `related.type`: 1 Xuất bán giữa 2 doanh nghiệp · 2 Nhập giữa 2 doanh nghiệp · 3 Xuất quà tặng HĐ bán lẻ · 4 Xuất quà tặng đơn hàng · 5 Xuất quà tặng bán sỉ · 6 Nhập trả lại bán lẻ/bán sỉ · 7 Nhập xuất chuyển kho · 8 Nhập xuất combo.
- **Vênh tài liệu:** filter `bill/list` ghi `8 = Bù trừ kiểm kho`, `6 = Bán buôn`; Model Constant ghi `8 = Kiểm kho`, `6 = Bán sỉ`. Dùng theo Model Constant, coi giá trị 8 là kiểm kho.

---

### 2.6 Inventory (tạo phiếu) — 5 endpoint
Nguồn: https://developers.nhanh.group/pos/inventory/{importsupplier,exportsupplier,importother,exportother,archive}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/inventory/importsupplier` | Tạo phiếu **nhập** từ NCC (mode 5, type 1) |
| POST | `/v3.0/inventory/exportsupplier` | Tạo phiếu **xuất** trả NCC (mode 5, type 2) |
| POST | `/v3.0/inventory/importother` | Tạo phiếu **nhập khác** (mode 10, type 1) |
| POST | `/v3.0/inventory/exportother` | Tạo phiếu **xuất khác** (mode 10, type 2) |
| POST | `/v3.0/inventory/archive` | **Hạn mức tồn kho** (min/max/waiting) |

Body chung: `depotId*, products[{id*, quantity*, price, discount{type,value}, vat{type,value}, imeis[] | imei, unitId, batchId, weight, expiredDate, priorWarningDays, description}], description, tagIds[], payment{manualDiscountType, manualDiscount, cash{accountId,value}, transfer{accountId,value}}, debtDueDate`
Khác nhau: `importsupplier`/`importother` có `supplierId*` hoặc `customer{}` + `updateImportPrice` (1: cập nhật giá nhập theo lần nhập này); `exportsupplier`/`exportother` dùng `imei` (số ít).
Quy tắc: nếu truyền **cả chiết khấu tổng đơn và chiết khấu sản phẩm ⇒ ưu tiên chiết khấu tổng đơn, bỏ chiết khấu sản phẩm**.
Response: `{id, totalAmount}`.

**`archive`** (hạn mức tồn — mới 2025-12-03): filter `productIds[] (≤100), depotIds[] (≤500), categoryId, brandId, createdAt/updatedAt`. Trả `depot{id,name}, product{id,barcode,code,name,remain}, minRemain, maxRemain, waiting (chờ nhập hàng), available, createdBy, createdAt, updatedAt`.
→ Có khái niệm **tồn tối thiểu / tối đa / đang chờ nhập** — thuộc replenishment, không có trong Shopify.

---

### 2.7 Invoice (hoá đơn điện tử) — 1 endpoint
Nguồn: https://developers.nhanh.group/pos/invoice/list/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/invoice/list` | Danh sách hoá đơn điện tử (≤50/trang) |

Filters: `depotId, referenceItemId, taxType, taxInvoiceNumber, updatedAtFrom/To, createdAtFrom/To, issueFromDate, issueToDate`.
**taxType: 1 Hoá đơn nháp · 2 Đã phát hành · 3 Điều chỉnh · 4 Thay thế** (đúng nghiệp vụ VN: hoá đơn điều chỉnh/thay thế).
Trả: `id, createdById, createdAt, description, taxId, gatewayId, taxInvoiceBillId, taxTemplateId, taxTemplateSeriesNumber (ký hiệu mẫu), taxStatus, taxStatusName, taxType, taxTypeName, errorCode, errorMessage, depot{...}, reference{ecomOrderId, itemId, itemIdText, itemType (1 đơn hàng / 2 hoá đơn), itemDate}, saleChannel{id,name,shopName,shopId}, buyer{name,mobile,address,taxCode,email,companyName}, products[{id,price,vatPercent,vatPercentSub (% VAT giảm),vatAmount,quantity,unitName,priceVatMode,product{...}}], totalAmount, invoice{number, govNumber (số HĐ cơ quan thuế), date, type}`.
Điều kiện dùng: doanh nghiệp phải mua hoá đơn của Nhanh + cấu hình ở Kênh bán > Hoá đơn điện tử; user cấp quyền phải có quyền tạo nháp/phát hành.
`publishType` (khi tạo từ `bill/addretail`): **1** tạo nháp · **2** phát hành. `tax.type`: **1** Hộ kinh doanh · **2** Công ty · **3** Cá nhân.

---

### 2.8 Customer — 3 endpoint
Nguồn: https://developers.nhanh.group/pos/customer/{list,add,tags}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/customer/list` | Danh sách khách hàng |
| POST | `/v3.0/customer/add` | Thêm khách hàng (mảng, ≤100) |
| POST | `/v3.0/customer/tags` | Nhãn khách hàng |

`list` filters: `ids[] (≤100), mobile, lastBoughtDateFrom/To, updatedAtFrom/To, type`.
Trả: `id, type, name, mobile, code, gender, address, email, birthday, points (điểm tích luỹ), saleName, startedDate, taxCode, description, locations{cityId,districtId,wardId}, location{cityId,wardId} (2 cấp), business{name,address}, level{id,name}, group{id,name}, totalAmount, updatedAt`.
**type: 1 Khách lẻ · 2 Khách buôn/sỉ · 3 Đại lý** (list) — nhưng `add` chỉ nhận **1 = Khách lẻ (default) / 2 = Khách buôn** (không có 3). `gender`: 1 Nam · 2 Nữ (addretail thêm 3 = Không xác định).
`add` body: `name*, mobile*, type, address, gender, birthday, locations{cityId,districtId,wardId}, email, pid (CCCD), groupId, tagIds[], points, taxCode, businessName, businessAddress, description, facebookLink, fromCustomer (SĐT người giới thiệu), saleName (username/email NV phụ trách)`.
→ Có **level + group + points + người giới thiệu + NV phụ trách**: mô hình CRM nhẹ, không phải chỉ "customer account" kiểu Shopify.

---

### 2.9 Promotion — 8 endpoint
Nguồn: https://developers.nhanh.group/pos/promotion/{batch,batchadd,batchincreasecode,couponcode,couponproduct,couponupdate,pricelist,pricelistproduct}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/promotion/batch` | Danh sách **chương trình** coupon |
| POST | `/v3.0/promotion/batchadd` | Tạo chương trình coupon (sinh hàng loạt mã) |
| POST | `/v3.0/promotion/batchincreasecode` | Phát hành thêm mã vào chương trình (≤1000) |
| POST | `/v3.0/promotion/couponcode` | Danh sách **mã** coupon |
| POST | `/v3.0/promotion/couponproduct` | Sản phẩm được áp dụng coupon |
| POST | `/v3.0/promotion/couponupdate` | Báo **đã tặng** / **đã dùng** mã |
| POST | `/v3.0/promotion/pricelist` | Danh sách bảng giá |
| POST | `/v3.0/promotion/pricelistproduct` | Sản phẩm trong bảng giá |

**Batch fields:** `id, name, description, startDate, endDate, status, numberOfCodes, codeLength, prefix, suffix, valueType (1 tiền mặt / 2 %), value, valueMax, fromValue, toValue, depotIds[] ([] = tất cả), notApplyWithDiscountPromotion, isCategoryApplyOnce, createdBy{id,name}, totalUsedTime, totalAssign`.
**batchadd body:** `name*, status*, startDate*, endDate*, codeLength*, numberOfCodes* (≤20.000), canUsedTimes*, valueType*, value*, depotIds, categoryIds, fromValue, toValue, prefix (≤5), suffix (≤5), valueMax, description, notApplyWithDiscountPromotion`.
**couponcode:** `depotId, code, value, valueMax, valueType, status, usedAt, totalCanUsed, totalAssigned, totalUsed, createdAt`.
**couponupdate:** `act*` = `giveaway` (báo đã tặng) | `used` (báo đã dùng), `code*`. → Cơ chế **đếm lượt** để tránh tặng/dùng quá số lần.
**couponproduct:** trả `batchId, product{id,name}`; **`productIds` null/rỗng = áp dụng cho tất cả sản phẩm**.
**pricelist:** `id, businessId, name, status, startDate, endDate, createdBy, createdDateTime`; filter `ids, name, status, depotIds`.
**pricelistproduct:** filter `productIds, priceListIds*`; trả `product{id,name,code,barcode,price,remain,importPrice,category{id,name}}, priceList[{id,businessId,name,value}]`.
→ Mô hình khuyến mại **2 tầng: batch (chương trình) → coupon (mã)**, cộng **price list đa giá theo cửa hàng**. Không thấy API tạo/sửa **khuyến mại tự động** (mua X tặng Y, giảm giá theo danh mục) — chỉ có `product/gift` để **đọc** quà tặng; không có endpoint ghi.

---

### 2.10 Accounting — 3 endpoint
Nguồn: https://developers.nhanh.group/pos/accounting/{transaction,account,debts}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/accounting/transaction` | Danh sách bút toán (≤100/trang) |
| POST | `/v3.0/accounting/account` | Danh sách tài khoản kế toán |
| POST | `/v3.0/accounting/debts` | Công nợ khách hàng (đầu kỳ/trong kỳ/cuối kỳ) |

**transaction** filters: `id, fromDate, toDate, typeDate (1 ngày giao dịch (default) / 2 ngày tạo), type, mode, filterMode (1 tự động / 2 không tự động), contactType (1 KH · 2 NCC · 3 Dịch vụ trả góp · 4 Nhân viên · 20 Khác · 21 Sàn TMĐT)`.
Trả: `id, date, type, mode, amount, itemType, itemId, documentCode, description, items[{amount, itemType, itemId, description, credit{id,name,code}, debit{id,name,code}}], depot{...}, contact{id,name,code}, created{id,name,createdAt}`.
→ **Bút toán 2 vế Nợ/Có (debit/credit) theo từng dòng** — mô hình kế toán kép thật.
**account** filters: `id, types[], status[]`. Trả `id, parentId, type (1 Tiền trong ngân hàng · 3 Tiền mặt cửa hàng · 7 Tiền quẹt thẻ), code, name, status, label, level, depotId, createdById, createdAt` → **tài khoản gắn với kho**.
**debts** filters: `id, fromDate, toDate, code, name, address, mobile, itemSubType (1/2/3), debtType (1 Phải thu · 2 Phải trả · 3 Cả hai), lastRemain {"from","to"}`.
Trả: `id, code, name, mobile, address, firstDebit, firstCredit, debit, credit, receive (phải thu cuối kỳ), pay (phải trả cuối kỳ), debtLimit (giới hạn công nợ)`.
**Enum Accounting** (Model Constant):
- `type`: 1 Báo nợ (rút tiền) · 2 Báo có (nộp tiền) · 3 Phiếu thu · 4 Phiếu chi · 5 Phiếu trả hàng · 6 Phiếu bán hàng · 7 Khác · 8 Phiếu nhập · 9 Phiếu xuất · 12 Kết chuyển.
- `mode`: 1 Nhập NCC · 2 Xuất trả NCC · 3 Bán hàng · 4 Hàng trả lại · 5 Bán sỉ · 6 Trả lại bán sỉ · 7 Nhập máy cũ · 8 Bảo hành · 9 Xuất linh kiện · 15 Chuyển quỹ · 20 Hạch toán trả góp · 21 Công nợ đầu kì · 22 Đơn hàng · 23 Đơn hàng trả lại · 24 Nhập NCC VAT · 25 Xuất NCC VAT · 26 XNK khác · 27 Thu hộ trả góp · 28 Nhập VAT · 29 Xuất VAT · 30 Xác nhận nhận tiền thanh toán · 31 Xác nhận chi tiền thanh toán vận chuyển · 32 Phiếu nhập quà tặng · 33 Phiếu xuất quà tặng.

---

### 2.11 Zalo — 2 endpoint
Nguồn: https://developers.nhanh.group/pos/zalo/{zbs,zns}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/zalo/sendzbs` | Gửi tin Zalo ZBS (tên mới của ZNS) |
| POST | `/v3.0/zalo/sendzns` | Gửi tin Zalo ZNS — **deprecated, ngừng hỗ trợ sau 2026-08-31** |

Body: `zaloOaId*, zbsTemplateId* (hoặc znsTemplateId), mobile*, keywords{KEY:VALUE}`.
Quy tắc: keyword khi gửi phải **bỏ dấu `< >`** (`<_TEN_KHACH_HANG_>` → `_TEN_KHACH_HANG_`); **chỉ gửi được 06:00–22:00 (UTC+7)**, trừ template loại OTP.
Response: `{oaBalance, quota{remainingQuota, dailyQuota}}`.
Mã lỗi riêng (tiền tố `ERR_ZBS_` / `ERR_ZNS_`): `OA_404`, `INVALID_OA`, `EXCEEDED_LIMIT_IN_DAY`, `OA_NOT_ENOUGH_BALANCE`, `INVALID_PHONE_NUMBER`, `ZALO_ACCOUNT_NOT_EXISTED`, `INVALID_TEMPLATE`, `PROHIBITED_TIMEFRAME`, `REQUIRED_KEYWORDS`, `KEYWORDS_NOT_MATCH`, `INVALID_KEYWORD_VALUES`, `CODE_1121`, `CODE_1123`, `CODE_1124`.

---

### 2.12 Ecom (sàn TMĐT) — 2 endpoint
Nguồn: https://developers.nhanh.group/pos/ecom/{shop,return}/

| Method | Path | Mục đích |
|---|---|---|
| POST | `/v3.0/ecom/shop` | Danh sách gian hàng đã kết nối sàn |
| POST | `/v3.0/ecom/return` | Yêu cầu trả hàng/hoàn tiền trên sàn (Shopee/TikTok/Lazada) |

**shop** — filter `appIds[]`; trả `appId, shop{id,name}, expriredAt (typo trong doc), createdAt`.
**appId:** **8142** Lazada · **8195** Shopee · **8237** Sendo · **8238** Tiki · **8855** Tiktok.
**return** — filter `appId*, shopIds[], returnIdOrReturnTrackingNumber, originalEcomOrderId, createdFromAt/To, updatedFromAt/To, nhanhStatuses[]`.
Trả: `shopId, returnId, businessId, returnType (1 Hoàn tiền ngay · 2 Trả hàng và hoàn tiền · 3 Đổi mới), originalEcomOrderId, reasonInfor{reasonId,reasonName}, productsInfor[{image,name,quantity,price,sku}], amount, ecomStatus{id,name}, nhanhStatus{id,name}, createdAt, updateAt`.
**nhanhStatuses:** 1 Đã hoàn tiền · 2 Đang xử lý · 3 Đã hủy yêu cầu · 4 Đang khiếu nại · 5 Đang hoàn tiền.
→ Mô hình **2 trạng thái song song**: trạng thái của sàn (`ecomStatus`) và trạng thái chuẩn hoá của Nhanh (`nhanhStatus`).

---

## 3. Webhooks

Nguồn cài đặt: https://developers.nhanh.group/app/webhooks/ · danh sách sự kiện: https://developers.nhanh.group/app/webhooks/events/
Nguồn payload: https://developers.nhanh.group/pos/webhooks/{order,inventory,product}/

**Cơ chế:** POST JSON tới `Webhooks callback URL` (bắt buộc **https**, phải trả **HTTP 200**), header `Content-Type: application/json` + `Authorization: <Webhooks verify token>` (token do bạn tự đặt). Envelope: `{event, businessId, data}`. Retry **tối đa 3 lần**; xử lý lỗi/chậm ⇒ bị giảm ưu tiên, có thể **tự tắt webhooks** hoặc **khoá app**. Sự kiện `webhooksEnabled` dùng để test URL. Quyền cần: `orderAdd/Update/Delete/PartialReturn` cần quyền **danh sách đơn hàng**; `product*`/`inventoryChange` cần quyền **danh sách sản phẩm**. **Chỉ bắn dữ liệu mới phát sinh**, không backfill quá khứ.

| Event | Nhóm | Ý nghĩa | `data` |
|---|---|---|---|
| `webhooksEnabled` | App | Test URL khi bật webhooks | `{event}` |
| `appUninstalled` | App | Doanh nghiệp gỡ app | `{event, businessId}` |
| `orderAdd` | POS | Đơn mới | object đơn (cấu trúc riêng, xem dưới) |
| `orderUpdate` | POS | Đơn đổi trạng thái/cập nhật | object đơn |
| `orderDelete` | POS | Xoá đơn | **mảng** `[orderId, ...]` |
| `orderPartialReturn` | POS | Trả hàng 1 phần / nhập hoàn kho đơn sàn (thêm 2026-01-09) | object đơn + `originalOrder` |
| `productAdd` | POS | Thêm sản phẩm | object sản phẩm |
| `productUpdate` | POS | Sửa sản phẩm | object sản phẩm |
| `productDelete` | POS | Xoá sản phẩm | **mảng** `[productId, ...]` |
| `inventoryChange` | POS | Tồn kho thay đổi | **mảng** productId + tồn |

**Payload `order*` (khác cấu trúc `order/list` — điểm rất dễ sai):**
`info{id, depotId, pickupDepotId, type, status, createdBy{id,name}, sale{id,name}, technicalStaff{id,name}, customerCare{id,name}, createdAt, updatedAt, confirmedAt, packedAt, description, privateDescription, trackingUrl, tagIds[]}`,
`channel{appOrderId, appShopId, appShopName, saleChannel, trafficSource{id,name}, facebook{pageId,conversationId,adId,postId}, utmCampaign, domain}`,
`shippingAddress{id, name, mobile, email, address, cityId, districtId, wardId, locationVersion}`,
`products[{id, typeId, code, name, avatar, weight, quantity, price, originalPrice, discount, available, avgCost, vat{value,amount}, description, imei{id,code}, batch{...}, combos[], unit{id,name,quantity}, gifts[]}]`,
`carrier{id, name, orderPackageSize{height,merge,length,weight,width}, service{id,name,code}, carrierCode, isPartDelivery, sendCarrier{type,date}, deliveryDate, weight, shipFee, codFee, declaredFee, returnFee, customerShipFee}`,
`payment{couponCode, credit{amount,accountId,code}, deposit{amount,accountId}, discount{type,amount}, transfer{amount,accountId}, usedPoints{points,amount}, vat{type,value,amount,taxCode,taxDate}}`,
`originalOrder{id, channel{appOrderId, appShopId, saleChannel}}` (chỉ có ở `orderPartialReturn`).
→ Khác biệt: webhook dùng **object lồng `{id,name}`** cho nhân viên, `products[].vat` là **object**, có `available`/`avatar`; còn `order/list` dùng **field phẳng `createdById/createdByName`**, `vatPercent` là số. **Không thể dùng chung 1 DTO.**

**Payload `inventoryChange`:** `[{id, typeId, code, remain, shipping, damaged, holding, transfering, available, warranty{remain, holding}, depots[{id, remain, shipping, damaged, holding, transfering, available, warranty{remain,holding}}]}]`.
Quy tắc: có thể **gộp nhiều sản phẩm vào 1 request**; với **sản phẩm combo trả về cả combo và các sản phẩm con**.

**Payload `product*`:** giống `product/list` + `inventory` đầy đủ (gồm `holdingNew`, `shippingExport`, `warrantyHolding`), `attributes[]`, `videos[]`, `branchPrices[]`, `childs[]`, `tagIds[]`, `suppliers` (số ít, không phải mảng như `detail`).

---

## 4. Nhận xét về mô hình dữ liệu

### 4.1 Quan hệ thực tế giữa các entity

```
Business (doanh nghiệp = tenant)
 ├─ Depot (1..n)  ── địa chỉ 3 cấp + địa chỉ 2 cấp song song
 ├─ User ──(n-n, qua depotIds trong token/`depots`)── Depot
 ├─ Department (cây, parentId)
 ├─ Supplier (cá nhân/doanh nghiệp, có bank)
 ├─ Customer (level, group, points, sale phụ trách, người giới thiệu)
 │    └─ CustomerTag (n-n)
 ├─ Product ──┬─ ProductUnit (1..n, quantity = hệ số quy đổi, giá riêng từng ĐVT)
 │            ├─ Batch (1..n, lô: manufactureDate/expiredDate)
 │            ├─ IMEI (1..n, serial-level, trạng thái 11 giá trị)
 │            ├─ Combo ──(n-n Product qua comboItems/qty)── Product
 │            ├─ Category (cây) / InternalCategory (cây) / Brand (cây) / Tag
 │            ├─ SupplierProduct (n-n + supplierProductCode)
 │            └─ PriceListProduct (n-n PriceList, value riêng)
 ├─ Order ──── 7 khối: info · channel · shippingAddress · products[] · carrier · payment · gifts[]
 │    ├─ OrderProduct (orderProductId) ── imeiIds[] · batch · unit · combos[] · gifts[]
 │    ├─ OrderLog (step 1..58, status{old,new}) · OrderSource · OrderTag
 │    ├─ Complaint (reason 6..38)
 │    └─ Bill (orderId) ── sinh phiếu XNK
 ├─ Bill (phiếu XNK) ── type(1/2) × mode(19 giá trị) × related{type,billId,parentId}
 │    ├─ BillImex (dòng: imexId, product, imei, batch, qty, price, vat, discount, points, saleBonus)
 │    └─ DraftBill (created → approved → confirmed)
 ├─ Inventory (không phải bảng riêng: là **view tổng hợp** từ Bill, theo product × depot)
 │    └─ InventoryLimit (min/max/waiting theo product × depot)
 ├─ EInvoice (taxType 1..4, tham chiếu reference{itemType,itemId})
 ├─ CouponBatch ── Coupon (code unique toàn hệ, canUsedTimes/totalAssign/totalUsed)
 ├─ PriceList ── PriceListProduct
 ├─ AccountingAccount (type 1/3/7, parentId, gắn depotId)
 ├─ Transaction (bút toán kép: items[].debit/credit + contact{1 KH,2 NCC,3 trả góp,4 NV,20 khác,21 sàn})
 ├─ Debt (số dư đầu kỳ/trong kỳ/cuối kỳ theo customer + debtLimit)
 └─ EcomShop (appId 8142/8195/8237/8238/8855) ── EcomReturn (ecomStatus ↔ nhanhStatus)
```

**Điểm mấu chốt:** **không có bảng "inventory" độc lập với sự kiện** — tồn kho là **hệ quả của các phiếu XNK (`Bill`)**. Mỗi `Bill` có `related.type` nói rõ nguồn (chuyển kho, quà tặng, combo, trả lại, bán buôn…). Vì vậy muốn "chỉnh tồn" bạn **không set số** mà **tạo phiếu** (`importother`/`exportother` = mode 10 "Khác") — khác hoàn toàn mô hình "inventory_levels" của Shopify nơi ta set `available` trực tiếp.

### 4.2 Tồn kho là vector 7–9 chiều, không phải 1 số

`remain · shipping · damaged · holding · transfering · warranty · warrantyHolding · holdingNew · shippingExport · available`, mỗi chiều **lặp lại theo từng depot**. Trong đó:
- `holding` (tạm giữ) tự tăng khi tạo đơn có `depotId` — tức **tồn khả dụng bị giữ chỗ ở tầng đơn hàng**, không phải tầng payment.
- `holdingNew` / `shippingExport` là "loại xuất S" — một biến thể xuất kho riêng của Nhanh, không có tương đương Shopify.
- `available` là field **tính sẵn** để chặn bán (doc nói rõ "dùng để chặn việc đặt các sản phẩm hết hàng").
- `warranty` + `warrantyHolding`: hàng đang bảo hành vẫn nằm trong hệ tồn.

### 4.3 Những điểm KHÁC biệt so với eCommerce Shopify-style

| # | Shopify-style | Nhanh POS v3 | Hệ quả thiết kế |
|---|---|---|---|
| 1 | Product → Variant (size/màu) → SKU | **Không có Variant.** "Biến thể" được mô hình hoá bằng 3 cơ chế khác nhau: `parentId` (cha/con, gpid), `units[]` (nhiều ĐVT + hệ số quy đổi + giá riêng), `type` 10 loại sản phẩm (IMEI, lô, combo, cân đo, voucher, dịch vụ, dụng cụ…) | Không thể map `variant_id`; phải map theo `product.id` + `unit.id` + `imei` |
| 2 | InventoryItem + inventory_levels, set số lượng trực tiếp | Tồn = **tổng hợp từ phiếu XNK**, vector nhiều ngăn, có `holding`/`warranty`/`transfering` | Cần event-sourcing từ `Bill`, không dùng "set available" |
| 3 | Serial/IMEI là optional app | **IMEI là first-class entity** với 11 trạng thái, lịch sử luân chuyển, tra cứu IMEI bán theo ngày | Cần bảng `imei` + `imei_history` |
| 4 | 1 order = 1 shipment, fulfillment tách rời | `carrier` nằm **trong** order; `sendCarrierType` 1/2 phân biệt bảng giá Nhanh vs tài khoản shop; `autoSend` gửi ngay khi tạo | Carrier là **thuộc tính của đơn**, không phải entity fulfillment riêng |
| 5 | Discount/coupon do platform tính | Nhanh **không tính** chiết khấu từ `couponCode`; app tự tính rồi nhồi `payment.discountAmount`; `addretail` còn **không nhận coupon** | Logic giá phải nằm ở hệ bạn; Nhanh chỉ ghi nhận |
| 6 | Order status tuyến tính (pending→paid→fulfilled) | **3 trục trạng thái độc lập**: `status` (18 giá trị, dải số không liên tục) × `reason` (24 lý do) × `carrierPaymentStatus`/`businessPaymentStatus` (đối soát) + `partialReturnStatus`/`paymentStatus` theo từng dòng | Không thể dùng 1 enum; cần state machine nhiều trục |
| 7 | Refund là entity riêng (Refund/Return) | Trả hàng = **Order type 14/16** + `orderPartialReturn` webhook + `ecom/return` cho đơn sàn; hoàn kho = phiếu XNK `related.type` 6 | "Return" vừa là order type, vừa là phiếu kho, vừa là bản ghi sàn |
| 8 | Nhiều kho là multi-location đơn giản | **`depotIds` gắn vào accessToken** — quyền dữ liệu theo kho ở tầng auth, không phải tầng query | Phân quyền theo kho là **hard constraint** |
| 9 | Tax = tax rate theo region, tự tính | `vat` là **thuộc tính sản phẩm** + `priceVatMode` (giá đã/chưa gồm VAT) ở cả product, order line và bill line; có giá trị đặc biệt **-1 không chịu thuế / -2 không kê khai / -100 thuế 0** | Cần lưu **2 con số** (giá trước VAT, % VAT) + mode, không chỉ "price" |
| 10 | Hoá đơn = PDF do platform xuất | **EInvoice là domain riêng**: nháp/phát hành/điều chỉnh/thay thế, ký hiệu mẫu, số HĐ cơ quan thuế, gateway, `errorCode/errorMessage` phát hành | Cần bảng e_invoice + quan hệ tới bill/order |
| 11 | Payout/balance | **Accounting kép thật**: `Transaction.items[].debit/credit` + `Account` (ngân hàng/tiền mặt/quẹt thẻ, gắn depot) + `Debt` (đầu kỳ/trong kỳ/cuối kỳ, `debtLimit`) | Cần ledger, không phải "payout" |
| 12 | Webhook payload = resource JSON | **Payload webhook KHÁC shape với REST list** (object lồng vs field phẳng, `vat` object vs `vatPercent` số) | Phải viết 2 mapper riêng |
| 13 | Pagination cursor opaque | `next` là **object có schema riêng từng API**, phải echo lại đúng cấu trúc | Không viết được 1 paginator generic |
| 14 | REST có GET/POST/PUT/DELETE | **100% POST**; không có DELETE; xoá chỉ phát hiện qua `*/deleted` + webhook `*Delete` | API client chỉ cần 1 verb |
| 15 | 1 tenant / 1 token | `businessId` là tenant, `accessToken` gắn cứng `(appId, businessId, depotIds, permissions)` | Multi-tenant phải quản lý token theo businessId |

### 4.4 Những "mùi" mô hình đáng lưu ý khi thiết kế lại

1. **`order/list` gộp đơn ngoại sàn và đơn sàn** nhưng `order/edit` lại **cấm sửa carrier/payment cho đơn sàn** ⇒ cùng 1 resource, 2 luật khác nhau theo `saleChannel`.
2. **`shippingAddress` trong order vừa là snapshot vừa là FK**: có `id` (= customer id) + `code` + toàn bộ field địa chỉ copy. Không rõ khi customer đổi địa chỉ thì order có đổi theo — **không thấy trong tài liệu**.
3. **`supplier` trong `product/detail` là object số ít**, nhưng về logic là n-n (`product/supplierproduct` tồn tại) ⇒ denormalize lẫn chuẩn hoá.
4. **`warranty` xuất hiện ở 4 nơi**: `product.warranty{month,phone,address}`, `imei.warranty{month,expiredDate}`, `imei.extendedWarranty{id,name,month}`, `bill.extendedWarranty{id,amount,month}` — 4 mô hình bảo hành chồng nhau, chưa có entity `WarrantyTicket` công khai (chỉ có `warrantyBillId`, `warrantyReasonId`).
5. **`mode` (kiểu giá bán) trong `order/list` là field không có enum công bố** — không thấy trong tài liệu.
6. **`exportType`** ("đánh dấu đơn đã xuất kho thủ công hay chưa") không có enum — không thấy trong tài liệu.
7. **`paymentStatus`, `partialReturnStatus`, `businessPaymentStatus`, `carrierPaymentStatus`** không có bảng enum — chỉ có tên. Đây là các trục trạng thái **không thể dựng state machine chính xác** từ tài liệu.
8. **Giới hạn 31 ngày** áp cho `order/list`, `bill/list`, `bill/retail`, `bill/draftlist` ⇒ hệ thống được thiết kế cho **đồng bộ tăng dần (incremental)**, không phải full-scan.
9. **Không có API đọc "1 order theo id"** riêng, **không có API sửa dòng sản phẩm trong đơn**, **không có API xoá/sửa phiếu kho** ⇒ hệ quả: muốn sửa sai phiếu kho phải tạo phiếu đảo.

---

## 5. Câu hỏi còn mở (tài liệu không trả lời)

1. **Enum của `info.mode` (loại giá bán) trong order** — giá trị nào là gì? Không thấy bảng.
2. **Enum của `exportType`, `paymentStatus`, `partialReturnStatus`, `businessPaymentStatus`, `carrierPaymentStatus`** — không thấy bảng giá trị.
3. **Bảng mã lỗi chung của POS** — heading có nhưng bảng rỗng; chỉ có mã lỗi chung của App + Vpage. Mã lỗi POS thực tế rải ở từng endpoint.
4. **`gpid` vs `parentId`**: `parentId` = -1/-2, nhưng filter `gpid` nhận -1/-2/1/2/3 với nghĩa khác. Quan hệ cha–con sâu tối đa bao nhiêu tầng? Không thấy.
5. **Đồng bộ tồn kho giữa `remain` và `holding`**: công thức chính xác của `available` không được viết ra (chỉ mô tả bằng lời). `available = remain − holding − shipping ± …`? Không thấy.
6. **"Loại xuất S"** (`holdingNew`, `shippingExport`) là nghiệp vụ gì? Không có trang nào giải thích, chỉ có trong field description.
7. **Đơn vị của `weight`/`shippingWeight`**: có chỗ ghi gram, có chỗ không ghi; `orderPackage` ghi gram & cm; `product.shipping` không ghi đơn vị. Cần xác nhận.
8. **`order/add` có hỗ trợ `gifts` ở cấp đơn (không thuộc sản phẩm nào) không?** Doc chỉ mô tả `products[].gifts`.
9. **`bill/list` mode 8 = "Bù trừ kiểm kho" vs Model Constant = "Kiểm kho"** — chênh lệch tên gọi; giá trị nào đúng cho filter?
10. **`bill/addretail` không nhận coupon nhưng `order/add` có nhận `couponCode`** — vậy với bán lẻ tại quầy, coupon được xử lý ở đâu? Không thấy.
11. **Không có endpoint tạo/sửa chương trình khuyến mại tự động** (mua X tặng Y, giảm theo danh mục) — chỉ có `product/gift` để đọc. Có phải tính năng này chỉ cấu hình trên UI? Không thấy.
12. **Không có endpoint quản lý bảo hành** (tạo phiếu bảo hành, tra cứu) dù có `mode` 13/14/15/16 và `warrantyBillId`. Chỉ đọc được qua `bill/list`.
13. **`accounting/*` chỉ có đọc** — không có API tạo phiếu thu/chi/bút toán. Xác nhận?
14. **`invoice/list` chỉ có list**, không có API phát hành/huỷ/điều chỉnh hoá đơn điện tử (chỉ tạo kèm khi `bill/addretail` hoặc `order/add`).
15. **`accessToken` hết hạn 1 năm, không refresh** — không có cơ chế thu hồi token chủ động. Khi app bị `appUninstalled` thì token chết ngay hay hết hạn tự nhiên? Không thấy.
16. **`VAT` trong `order/products[].vat`**: nhận cả `-1/-2/-100` (const) — nhưng kiểu field là `double`. Cách hệ thống lưu giá trị đặc biệt này trong tính toán dòng tiền không được mô tả.
17. **Địa chỉ 2 cấp (`locationVersion=v2`)**: khi tạo đơn v2 nhưng hãng vận chuyển chỉ hỗ trợ 3 cấp thì Nhanh xử lý thế nào? Doc khuyến cáo dùng v1 nhưng không nói cơ chế fallback.
18. **`order/history` `step` 58 giá trị** nhưng không nói step nào ứng với status nào — cần map thủ công.
19. **`ecom/return`**: `nhanhStatus` 1..5 và `returnType` 1..3 độc lập hay phụ thuộc nhau? Không thấy.
20. **Không có API cho `Promotion` dạng "flash sale"/"giá theo thời gian"** — chỉ có `pricelist` có startDate/endDate. Đây có phải cơ chế giá theo thời gian duy nhất?

---

## 6. Phụ lục — Danh sách URL nguồn đã đọc

**Tổng quan & hằng số**
- https://developers.nhanh.group/pos/
- https://developers.nhanh.group/pos/changelog/
- https://developers.nhanh.group/pos/model-constant/

**Giao thức dùng chung**
- https://developers.nhanh.group/app/api-request/
- https://developers.nhanh.group/app/access-token/
- https://developers.nhanh.group/app/api-rate-limit/
- https://developers.nhanh.group/app/webhooks/
- https://developers.nhanh.group/app/webhooks/events/
- https://developers.nhanh.group/app/changelog/

**Business** — `/pos/business/{depot,supplier,addsupplier,department,user}/`
**Product** — `/pos/product/{list,detail,add,edit,batch,brand,category,internalcategory,combo,expire,externalimage,gift,imei,imeihistory,imeisold,inventory,supplierproduct,tags,unit}/`
**Order** — `/pos/order/{list,add,edit,history,deleted,source,tags,sendcarrier,cancelCarrier,addcomplain}/`
**Shipping** — `/pos/shipping/{location,carrier,fee}/`
**Bill** — `/pos/bill/{list,retail,addretail,deleted,draftlist,draftproducts,imexs}/`
**Inventory** — `/pos/inventory/{importsupplier,exportsupplier,importother,exportother,archive}/`
**Invoice** — `/pos/invoice/list/`
**Customer** — `/pos/customer/{list,add,tags}/`
**Promotion** — `/pos/promotion/{batch,batchadd,batchincreasecode,couponcode,couponproduct,couponupdate,pricelist,pricelistproduct}/`
**Accounting** — `/pos/accounting/{transaction,account,debts}/`
**Zalo** — `/pos/zalo/{zbs,zns}/`
**Ecom** — `/pos/ecom/{shop,return}/`
**Webhooks** — `/pos/webhooks/{order,inventory,product}/`

*(Tất cả dưới domain `https://developers.nhanh.group`. Bản raw text đã bóc về máy tại `D:\Projects\Tools\eCommerce\_nhanh_docs\` — 79 file `.txt`.)*
