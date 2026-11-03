# 01 — Khảo sát Nhanh.vn (bằng chứng thực đo)

> **Phương pháp:** đăng nhập sẵn trong Chrome profile `%LOCALAPPDATA%\DSH\nhanh-chrome-profile`
> (businessId = **230222**), attach CDP cổng **9222** bằng cầu có sẵn trong repo
> [`tools/nhanh-cdp.mjs`](../../tools/nhanh-cdp.mjs), rồi đọc **DOM thật**, **route thật**, **CSS thật**.
> Bổ sung: 73 trang tài liệu Open API v3 (`docs/nhanh/nhanh-pos-v3-api-inventory.md`) và 45 trang
> hướng dẫn sử dụng GitBook (`docs/nhanh/nhanh-pos-module-map.md`).
>
> **Không phải suy đoán từ ảnh chụp.** Mọi con số dưới đây đo được từ phiên đăng nhập thật, trừ chỗ ghi rõ "theo tài liệu".

---

## 1. Nhanh.vn là gì (định vị — quyết định phạm vi thiết kế)

Nhanh.vn **không phải** một eCommerce website-first như Shopify. Nó là **phần mềm quản lý bán hàng
(QLBH) đa kênh** cho doanh nghiệp Việt Nam: trung tâm là **hàng hóa – kho – đơn hàng – kế toán**,
với website/sàn TMĐT chỉ là **một trong các kênh bán**.

| Đặc trưng | Shopify-style | Nhanh.vn |
|---|---|---|
| Đơn vị nghiệp vụ trung tâm | Product + Order (online) | **Hóa đơn bán lẻ tại quầy** + **Đơn giao vận** + **Phiếu xuất nhập kho** |
| Biến thể | Variant + option | **Không có Variant**; thay bằng `parentId` (cha–con) + `units[]` (nhiều ĐVT) + `type` (10 loại SP) |
| Tồn kho | `inventory_levels.available` (1 số) | **Vector 7–10 chiều theo từng kho** (tồn, tạm giữ, đang chuyển, lỗi, bảo hành, đặt trước…) |
| Tiền | Price + discount | **7 loại giá** + VAT 2 chế độ + giá vốn bình quân/đích danh + công nợ + kế toán kép |
| Trạng thái đơn | 5–8 | **18 trạng thái × 13 loại đơn × 5 trạng thái bàn giao** |
| Báo cáo | Vài dashboard | **~90 route báo cáo**, gồm sổ kế toán hộ kinh doanh (S1a/S2a–S2e) |
| Đa doanh nghiệp | 1 store / tenant | `businessId` + `depotIds` **gắn cứng vào access token** |

📌 **Kết luận phạm vi:** nếu chỉ "làm lại UI cho đẹp" thì sẽ hỏng, vì **mô hình dữ liệu kho/đơn/kế toán
khác về bản chất**. Phải thiết kế lại domain (mục 03), UI chỉ là lớp cuối.

---

## 2. Stack frontend của Nhanh (đo từ HTML/CSS thật)

| Hạng mục | Giá trị đo được | Nguồn |
|---|---|---|
| Framework | **Angular** (SPA, `<app-root>`, module preload, chunk hash) | `https://nhanh.vn/login` |
| UI kit | **Angular Material (MDC)** + CSS tiện ích kiểu Bootstrap tự viết (`--btn-*`, `--table-*`) | `--mdc-theme-primary` trong `styles-*.css` |
| Font | **Inter** 300–700 | `<link>` Google Fonts |
| Icon | **Phosphor Icons** (thin/light/regular/bold/fill/duotone) | 6 `<link>` jsDelivr |
| Editor mô tả SP | **HugeRTE** (fork TinyMCE) | `tools/README-nhanh.md` §"Bốn cái bẫy" |
| Lưu nháp POS | **IndexedDB** (`POS_BUS_<id>` / `posBills`) | tài liệu Bán lẻ, FAQ #8 |
| CSS size | `all.min.css` 721 KB + `styles-*.css` 38 KB + `override.css` 5 KB | tải trực tiếp |
| Build | Vite-style asset naming (`styles-PHIIJ3YG.css`) | HTML login |

### 2.1 Design tokens (chép nguyên từ `styles-PHIIJ3YG.css`)

```css
--primary:#0C83FF; --primary-hover:#0B76E6; --primary-active:#0A6FD9;
--success:#059669;  /* ← MÀU NÚT CHÍNH "Thêm mới", "Lọc" */
--secondary:#247297; --info:#049AAD; --warning:#F58646; --danger:#EF4444;
--light:#F3F4F6; --dark:#252B36; --body-bg:#E2E6ED; --text-color:#1F2937;
--border-color:#dee2e6; --dialog-inner-border-radius:.5rem;
--table-cell-padding-y:.75rem; --table-cell-padding-x:1.25rem;
--sidebar-custom-bg:#F3F4F6; --sidebar-custom-color:#252B36;
--theme-navbar-bg:#252B36;   /* topbar TỐI */
font-family: Inter; font-size: 14px (đo trực tiếp trên body; fs-sm .875rem, fs-7 .75rem)
```

> 🔑 **Nghịch lý cần hiểu đúng:** `--primary` là **xanh dương** (dùng cho link, tab active, số ID),
> nhưng **nút hành động chính lại là xanh lá `#059669`** (`btn-success`). Nếu làm UI "theo Nhanh"
> mà dùng xanh dương cho nút chính thì **sai bản sắc** — đây là chi tiết dễ mất nhất khi clone UI.

### 2.2 Cấu trúc khung nhìn (đo từ DOM)

```
┌─────────────────────────────────────────────────────────────────────┐
│ TOPBAR  #252B36  cao 52px (logo · business switcher · tìm kiếm · user)│
├──────────┬──────────────────────────────────────────────────────────┤
│ SIDEBAR  │ PAGE HEADER: tabs có BADGE SỐ  (Tất cả | Xác nhận 87 | …) │
│ #F3F4F6  │ ───────────────────────────────────────────────────────── │
│ 2 cấp    │ FILTER BAR (input pill + select + date-range + Lọc/Lưu)   │
│ ~256px   │ ───────────────────────────────────────────────────────── │
│ thu gọn  │ ACTION BAR (Thêm mới ▾ · Thao tác ▾ · In đơn ▾ · … phải:  │
│ còn icon │             1–30/158 · phân trang · ẩn/hiện cột)          │
│          │ ───────────────────────────────────────────────────────── │
│          │ TABLE / GROUPED LIST (nền #E2E6ED, card trắng)            │
└──────────┴──────────────────────────────────────────────────────────┘
```

📏 Đo trực tiếp: **topbar 52px**, **header bảng 36px**, `body { font-size:14px; font-family:Inter }`,
viewport 1920×945. Bề rộng sidebar hiện **0** ở trang này (người dùng đã thu gọn) ⇒ số ~256px là
**theo ảnh chụp**, không phải số đo — Web Admin chọn 256/64px.

Đo thực tế: viewport 1920×945, sidebar **197 link**, menu **180 mục** (xem
[`evidence/menu-sidebar.json`](evidence/menu-sidebar.json)).

---

## 3. Bản đồ route thật (204 route nội bộ — trích)

Toàn bộ danh sách: [`evidence/routes-raw.json`](evidence/routes-raw.json).
Quy ước: `/{module}/{entity}/{action}` — **giữ nguyên quy ước này** cho Web Admin mới để người dùng
quen Nhanh không phải học lại.

| Nhóm | Route tiêu biểu (đo thật) |
|---|---|
| **Sản phẩm** | `/product/item/index` · `/add` · `/add2` · `/detail` · `/edit` · `/inventory` · `/storagetime`; `/product/brand/index`; `/product/batch/index`; `/product/category/index`; `/product/variant/index`; `/product/barcode/export` |
| **Kho** | `/inventory/bill/index` · `/import` · `/import2` · `/export` · `/export2`; `/inventory/transfer/index` · `/add` · `/add2`; `/inventory/check/index`; `/inventory/imex/index`; `/inventory/requirement/index` · `/requirement/bill`; `/inventory/archive/index` (hạn mức); `/inventory/position/index` · `/category` · `/bill` · `/imex`; `/inventory/product/damaged`; `/inventory/product/companyavailable`; `/inventory/forecasting/movingaverage`; `/inventory/log/imexbill` |
| **Đơn hàng** | `/order/manage/index` · `/add` · `/add2` · `/packing` · `/sendingcarrier` · `/complain` · `/payment` · `/checkduplicate` · `/deleted`; `/order/payment/index` |
| **POS / Kênh bán** | `/pos/bill/index` · `/add` · `/add2` · `/addpackage` · `/extractpackage` · `/addwholesale` · `/lookup` · `/wholesale`; `/pos/return/index`; `/pos/sale/index` (hóa đơn nháp); `/pos/invoice/index`; `/pos/debt/bill` |
| **Khách hàng** | `/customer/code/customerlist` · `/customer/code/index` (thẻ) · `/customer/care/index` · `/customer/store/level` · `/customer/group/index` · `/customer/type/index` · `/customer/reason/index` |
| **Khuyến mại** | `/promotion/setting/discount` · `/pricelist` · `/point` · `/coupon` · `/gift` · `/commission` |
| **Báo cáo** | `/report/revenue/*` (10) · `/report/order/*` (11) · `/report/retail/*` (8) · `/report/wholesale/*` (2) · `/report/inventory/*` (11) · `/report/product/*` (8) · `/report/customer/*` (8) · `/report/promotion/*` (5) · `/report/household/s1a` · `s2a–s2e` · `/report/zalo/*` · `/report/sms/date` |
| **Cài đặt / cửa hàng** | `/setting/store/index` · `/sale` · `/labels`; `/setting/order/index`; `/setting/payment/index`; `/setting/zalo` · `/email` · `/sms`; `/setting/expire/depot`; `/store/user/index` (nhân viên); `/store/branch/index`; `/store/agency/index`; `/store/template/design` (mẫu in); `/store/traffic/source` (nguồn đơn) |
| **Sàn TMĐT** | `/ecommerce/shopee/product` · `/order` · `/productreview`; `/tiktok/…`; `/lazada/…`; `/tiki/…`; `/ecommerce/meta/product`; `/ecommerce/manage/index` (đối soát COD); `/manage/return`; `/manage/reportads`; `/finance/dashboard`; `/manage/setting` |
| **Hóa đơn điện tử** | `/invoice/bill/index` · `/invoice/product/index` · `/invoice/setting/index` |
| **Website** | `/website/content/index` · `/website/menu/index` · `/website/content/tags` |
| **Vận chuyển** | `/shipping/handover/index` (biên bản bàn giao) |
| **Hệ thống** | `/system/announcement/index` · `/profile/index` · `/notification/index/setting` · `/business/tool/index` (3 nút tính lại) |

⚠️ **204 route ≠ 204 màn hình cần làm.** Nhiều route là biến thể `add`/`add2`, `import`/`import2`
(bản cũ/mới). Bản rút gọn để thi công: **50 màn hình** theo bảng P0/P1/P2 trong
[`nhanh-pos-module-map.md`](nhanh-pos-module-map.md) §14.

---

## 4. Mẫu UI đặc trưng (đo từ DOM + ảnh chụp thật)

Ảnh: [`artifacts/nhanh-survey/product-list.png`](../../artifacts/nhanh-survey/product-list.png),
[`order-list.png`](../../artifacts/nhanh-survey/order-list.png),
[`bill-detail.png`](../../artifacts/nhanh-survey/bill-detail.png).

### 4.1 Danh sách sản phẩm — cột đo được (20 cột)

`ID · Ảnh · Mã vạch · Mã · Tên · ĐVT · Giá nhập · Giá nhập + VAT · Giá vốn · Giá bán · Giá bán + VAT ·
Giá sỉ · Giá sỉ + VAT · Tổng tồn · Tồn · Bảo hành · Tạm giữ linh kiện · Địa chỉ bảo hành · Đặt trước · Bán`

Nút/hành vi đo được: `Lưu bộ lọc hiện tại · Lọc · Nhập từ file · Xuất dữ liệu · In mã vạch ·
Đổi trạng thái sản phẩm · Set lại SP hot/new/cha-con · Gắn nhãn · Xoá dòng đã chọn · Cập nhật danh mục ·
Xem trên website`; mỗi dòng có **ngôi sao** (yêu thích) và menu `⋮`.

### 4.2 Danh sách đơn hàng — tab + cột + bulk action (đo được)

**Tab có badge số:** `Đơn ngoại sàn · Shopee · Tiktok · Lazada · Tiki · Tất cả · Xác nhận thanh toán ·
Xác nhận · In & đóng gói · Đang chuyển · Cần xử lý · Thanh toán · Khiếu nại · GH1P`

**Cột:** `ID · Khách hàng · Sản phẩm · Đơn vị tính · Giá · SL · Khối lượng · VAT sản phẩm ·
Giá trị đơn hàng · Phí ship báo khách · Phí vận chuyển · Biên bản bàn giao · Mã affiliate ·
Hóa đơn điện tử · Người tạo đơn · Nhãn đơn hàng · Link website · Lợi nhuận`

**Hành động hàng loạt:** `Nhập trạng thái từ file · Cập nhật hãng vận chuyển · Gắn/Gỡ nhãn ·
Cập nhật NV đóng gói · Cập nhật NV bán hàng · Cập nhật trạng thái từ HVC · Đổi trạng thái · Đổi kho ·
Gộp các đơn đã chọn · Tách đơn Mega Live · Xuất dữ liệu · Gửi đơn sang HVC · Xoá ·
Tạo HĐĐT nháp · Phát hành HĐĐT`

### 4.2b Phiếu xuất nhập kho — đo 03/11/2026 (bằng cầu CDP, tài khoản `businessId=230222`)

Phục vụ `WP-04` (FE). Đo trên **bản Nhanh đang chạy của bạn**, không suy từ ảnh.

**Route thật:** `/inventory/bill/index` (DS phiếu) · `/inventory/bill/import` + `/export` (nhập/xuất kho,
bản v2 là `import2`/`export2`) · `/inventory/transfer/add2` (chuyển kho) · `/inventory/check/index` (kiểm kho).

**Bộ lọc (một hàng, server-side):** `Kho hàng · ID · Loại · Kiểu · Ngày · Lọc`
— **`Loại`** = nhập/xuất, **`Kiểu`** = mode (bán lẻ/giao hàng/NCC…), **`Ngày`** = khoảng ngày, **`ID`** = số phiếu.

**Cột DS phiếu (7):** `ID | Ngày · Kho hàng · SP · SL · Tổng tiền thanh toán · Người tạo · Khách hàng`
📌 Nhanh **gộp ID + Ngày vào một ô** (`72 31/10`) và **không có tab trạng thái** cho phiếu kho — phiếu kho
của Nhanh **xác nhận ngay khi lưu**, khác đơn hàng (14 tab). Ta giữ `Draft/Posted` (ADR-02 của repo) nên
**có thêm** tab trạng thái; đó là **khác biệt có chủ ý**, không phải thiếu sót.

**`Thêm mới ▾`:** `Nhập kho · Xuất kho · Nhập từ excel`.
**`Thao tác ▾` (bulk):** `Xuất dữ liệu · In các phiếu XNK đã chọn · Gắn nhãn, Gỡ nhãn ·
Đổi ngày cho phiếu XNK · Xoá các dòng đã chọn` — có **checkbox từng dòng + chọn tất cả**.

**Form Nhập kho:** đầu phiếu `Kho hàng* · Loại nhập hàng* · Nhà cung cấp · Nhãn`; bảng dòng 4 cột
`Sản phẩm · Tồn · $/% · Thành tiền`, mỗi dòng có `SL · Giá · ĐVT · Chiết khấu · VAT · Lô hàng · Khối lượng`;
cuối phiếu `Tổng tiền hàng · Chiết khấu ($ hoặc %) · VAT ($ hoặc %) · Số hoá đơn VAT · Ngày xuất hoá đơn ·
Thanh toán (Tiền mặt / Chuyển khoản) · Ghi chú`; nút `Lưu (F9) · Lưu và in (F10) · Xem chi tiết phiếu ·
Tiếp tục thêm`; checkbox `Cập nhật giá nhập sản phẩm theo giá nhập lần này`.
**Form Xuất kho:** giống hệt nhưng nhãn là **`Loại trả hàng*`** (không phải "Loại nhập hàng").
📌 Bảng dòng có **checkbox ẩn/hiện cột** (`isHideProductStt`, `isHideProductBatch`) — pattern ẩn/hiện cột
áp **cả bảng dòng**, không chỉ bảng danh sách.

> 💡 **Hệ quả cho ta:** mọi trường **tiền** của phiếu Nhanh (`Giá`, `Thành tiền`, `Tổng tiền hàng`,
> `Chiết khấu`, `VAT`, `Thanh toán`) **trước đây không có** trong `StockDocumentLine` của repo (lý do `D-34`
> hoãn). **Đã xử lý 03/11/2026:** thêm `UnitPrice` + `TinhTienChungTu` (migration **55**, `D-37`) ⇒ ta có
> `Giá`/`Thành tiền`/`Tổng tiền hàng`/`VAT`; **chưa** có `Số hoá đơn VAT`, `Ngày xuất hoá đơn` và tách
> `Thanh toán` theo phương thức (tiền mặt/chuyển khoản — thuộc POS/WP-06).

### 4.3 Bảy pattern phải clone (vì người dùng đã quen)

1. **Tab có đếm số** — biến danh sách thành *hàng đợi công việc*, không phải bảng dữ liệu.
2. **Bộ lọc 2 tầng**: vài ô nhanh + nút `▾` mở panel nâng cao; **`Lưu bộ lọc`** thành tab riêng.
3. **`Thao tác ▾`** gom 10–20 hành động bulk; chỉ hiện khi có dòng được chọn.
4. **Ẩn/hiện cột** + lưu theo người dùng (bảng 20 cột không thể vừa màn hình).
5. **Trạng thái điều khiển UI**: nút nào hiện phụ thuộc trạng thái đơn (bảng "thao tác × trạng thái").
6. **Tab set động ở trang chi tiết** (đơn chưa gửi HVC: 4 tab; đã gửi: 5 tab; tự vận chuyển: mất 2 tab).
7. **POS đa tab + phím tắt** F1 thêm HĐ, F3 tìm SP, F4 khách, F6 chiết khấu, F8 tiền mặt, F9 lưu, F10 lưu+in.

### 4.4 Ngôn ngữ hiển thị

Toàn bộ nhãn **tiếng Việt không dấu phân cách**, số tiền `150,000`, ngày `18/11`, giờ `09:56 18/11`.
→ Web Admin phải dùng `Intl.NumberFormat('vi-VN')` + `dd/MM/YYYY`, **không** để `en-US` lọt vào UI.

---

## 5. Mô hình dữ liệu — những chỗ khác Shopify và quyết định thiết kế

### 5.1 Sản phẩm: 10 loại, không có Variant

| # | Loại (`type`) | Hệ quả dữ liệu |
|---|---|---|
| 1 | Thường | 1 SKU, 1 tồn |
| 2 | Voucher | bán như hàng, không tồn thực |
| 3 | Cân đo | bán theo kg → **số lượng thập phân** |
| 4 | IMEI | **1 dòng = 1 serial**, 11 trạng thái IMEI |
| 5 | Gói | cha gồm nhiều SP con |
| 6 | Dịch vụ | không tồn |
| 7 | Dụng cụ | tồn nhưng không bán |
| 8 | Theo lô | tồn gắn `batchId` + hạn dùng |
| 9 | Combo | tồn = min(tồn thành phần) |
| 10 | Nhiều ĐVT | `units[]` hệ số quy đổi + giá riêng |

**Trạng thái SP (4):** `Mới` (offline) · `Đang bán` · `Ngừng bán` · `Hết hàng` (giữ link, chặn bán).
**VAT đặc biệt:** `-1` không chịu thuế · `-2` không kê khai · `-100` thuế 0%.
**7 loại giá:** nhập · vốn · bán · cũ · buôn · chi nhánh · (kèm `+ VAT`).

Ghi chú thiết kế: Nhanh dùng **cha–con** (`parentId: -1` độc lập, `-2` cha) chứ không dùng
"product + variant". Đây là **quyết định phải theo**, vì tồn kho/IMEI/lô gắn ở **SP con**.

### 5.2 Tồn kho: vector, không phải một số

Theo tài liệu (module map §4 + API inventory §4.2), các thành phần tồn:

```
Tồn (remain) ─ Đang chuyển kho (transfering) ─ Đang giao (shipping) ─ Tạm giữ (holding)
   ─ Lỗi (damaged) ─ Bảo hành (warranty) ─ Tạm giữ linh kiện (warrantyHolding)
   ─ Đặt trước (preOrder) ─ Chờ nhập (waitingInbound)
Tồn trong kho   = Tồn − Đang chuyển
Có thể bán      = Tồn trong kho − Tạm giữ − Lỗi
Tổng tồn        = Σ tồn các cửa hàng + đang chuyển kho
```

🔑 **Không có API "set tồn".** Muốn đổi tồn phải tạo **phiếu XNK** (`bill`) hoặc **phiếu kho**
(`inventory/importsupplier|exportsupplier|importother|exportother`). Đây là điều buộc phải bỏ tư duy
"update inventory level" kiểu Shopify.

### 5.3 Đơn hàng: 18 trạng thái, 13 loại, 5 bàn giao

Bảng đầy đủ + map "trạng thái → tồn kho" ở [`nhanh-pos-module-map.md`](nhanh-pos-module-map.md) §6.2.
Tóm tắt trục:

```
Mới → Chờ xác nhận → Đang xác nhận → Đã xác nhận → Đang đóng gói → Đã đóng gói
   → Chờ thu gom → Đang chuyển → {Thành công | Thất bại → Đang hoàn → Xác nhận hoàn → Đã hoàn}
Nhánh phụ: Hết hàng · Đổi kho hàng · Khách hủy · Hệ thống hủy · HVC hủy
```

Loại đơn: giao tận nhà · tại quầy · đặt trước · đổi quà · xin báo giá · đổi SP · khách trả lại ·
chuyển kho · hoàn một phần · đền bù mất hàng.
Bàn giao: đã/chưa bàn giao · đã/chưa chuyển hoàn · đã cho vào tour.

### 5.4 Kế toán: bút toán kép thật (không phải "payout")

- `JournalEntry` có `items[]` **Nợ/Có**, `contactType` (KH/NCC/trả góp/NV/khác/sàn), 12 loại chứng từ, 33 mode.
- Công nợ: `firstDebit/firstCredit/debit/credit/receive/pay/debtLimit` (hạn mức nợ **riêng từng KH**).
- Tài khoản tiền gắn **kho** (`depotId`): tiền mặt / ngân hàng / quẹt thẻ.
- Bảng định khoản tự động 13 nghiệp vụ (module map §8.2) — dùng làm spec, **không tự nghĩ lại**.

### 5.5 Khuyến mại: 2 tầng batch → coupon

`PromotionBatch` (đợt) → `Coupon` (mã) → `CouponProduct` (ràng buộc SP). Mã **unique toàn hệ**.
`valueType` tiền mặt / %. Có `canUsedTimes`, `totalAssign/totalUsed`, `act=giveaway|used`.
**PriceList** đa giá theo cửa hàng.

⚠️ **Nhanh KHÔNG tự tính tiền giảm**: API chỉ *ghi nhận* `couponCode`, app phải tự tính rồi nhồi
`payment.discountAmount`. → Engine khuyến mại **là của chúng ta**, không thể trông vào Nhanh.

### 5.6 Webhook: payload khác REST — phải 2 mapper

10 event: `orderAdd/Update/Delete`, `orderPartialReturn`, `productAdd/Update/Delete`,
`inventoryChange`, `appUninstalled`. Payload webhook dùng `createdBy{id,name}`, `products[].vat` là object…
trong khi REST list dùng field phẳng `createdById/createdByName`, `vatPercent` số.
→ Nếu viết lại tích hợp: **2 DTO riêng**, đừng hợp nhất.

---

## 6. Số liệu tổng hợp để lập kế hoạch

| Chỉ số | Giá trị | Ý nghĩa cho kế hoạch |
|---|---|---|
| Route nội bộ | **204** | ~50 màn hình thực chất (sau khi gộp biến thể) |
| Mục menu | **180** | Sidebar 2 cấp, cần cấu hình theo quyền + gói dịch vụ |
| API v3 | **68 endpoint / 12 nhóm** | Đủ để **nhập dữ liệu từ Nhanh** nếu cần chuyển đổi |
| Webhook | **10 event** | Đủ để đồng bộ 2 chiều giai đoạn đầu |
| Báo cáo | **~90 route** | Không làm hết; chọn 12 báo cáo P0/P1 |
| Bảng DB ước tính | **~120–150** | Gấp ~3 lần hiện tại (xem 03) |
| Ngôn ngữ/tiền tệ | vi-VN, VND, 3 cấp hành chính | Không cần đa tiền tệ ở P0 |

---

## 7. Điều **chưa** khảo sát được (nói thẳng)

| Hạng mục | Vì sao | Cách lấp |
|---|---|---|
| Hành vi realtime (WebSocket/SSE) | REA passive không giữ body; chưa bật network capture | Dùng `capture_browser_scenario` của REA sau khi nâng Node |
| API nội bộ của Nhanh (`/api/...` của SPA) | Không cần: đã có Open API v3 công bố | — |
| Màn hình kế toán trên UI business 230222 | Tài khoản này **chưa bật module kế toán** (menu không có) | Đã lấy đủ qua tài liệu + API |
| Bảo hành, Website builder, Affiliate | Chưa fetch hết 25 trang `.md` còn lại | Bổ sung khi làm GĐ3 |
| Giá gói dịch vụ | Không ảnh hưởng thiết kế | — |

---

## 8. Kết luận của phần khảo sát

1. **Sản phẩm đích là QLBH đa kênh**, không phải website bán hàng → phải bổ sung **POS, kho nhiều
   loại tồn, kế toán, báo cáo, khuyến mại** (hiện repo mới có ~40%).
2. **Mô hình dữ liệu phải đổi ở 4 chỗ cốt lõi**: sản phẩm (loại/ĐVT/IMEI/lô), tồn kho (vector + phiếu),
   đơn hàng (18 trạng thái), kế toán (bút toán kép).
3. **UI không phải "bootstrap đẹp"**: bản sắc nằm ở *tab có badge + bộ lọc 2 tầng + bulk action +
   bảng dày 20 cột + nút xanh lá*, và **route `/module/entity/action`**.
4. Mọi thứ trên **đo được**, có file bằng chứng — dùng làm tiêu chí nghiệm thu UI sau này
   (so sánh từng màn hình với Nhanh).

