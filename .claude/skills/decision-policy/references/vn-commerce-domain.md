# Domain — Nhanh.vn và thông lệ bán hàng Việt Nam (để quyết thay chủ dự án)

> **Định hướng sản phẩm đã chốt:** `eCommerce` là phần mềm **quản lý bán hàng đa kênh kiểu Nhanh.vn**
> (hàng hoá – kho – hoá đơn bán lẻ – đơn giao vận – kế toán), **không** phải website kiểu Shopify
> (`docs/nhanh/01-khao-sat-nhanh-vn.md` §1, D-02…D-10).
>
> **Thứ tự nguồn khi quyết một câu hỏi nghiệp vụ:**
> 1. Quyết định `ĐÃ CHỐT` + tài liệu feature đã Approved trong `docs/features/`.
> 2. **Hành vi Nhanh đo được** (DOM, route, Open API v3): `docs/nhanh/01-khao-sat-nhanh-vn.md`, `docs/nhanh/nhanh-pos-v3-api-inventory.md`, `docs/nhanh/nhanh-pos-module-map.md`, `docs/nhanh/evidence/README.md`.
> 3. Thông lệ chung của phần mềm bán hàng VN: **KiotViet, Sapo, Haravan, MISA eShop, Pancake** — chỉ dùng khi Nhanh **không** có bằng chứng.
> 4. Quy tắc của **sàn** (Shopee, Lazada, TikTok Shop) và **hãng/NCC** (GHN, GHTK, VNPay, VietQR/Napas, NCC HĐĐT) — chỉ từ tài liệu chính thức, có URL + ngày tra.
> 5. Không có gì ⇒ phương án **cấu hình được với mặc định an toàn**.
>
> Nhãn tin cậy: ✅ có trong repo (đo/tra, có nguồn) · 🔶 thông lệ phổ biến, **chưa** xác minh trong repo — được dùng cho
> `TAM_CHOT` lùi rẻ, nhưng muốn `confidence = CAO` thì phải tra lại · ⬜ chưa biết — không được dựa vào.
>
> Nhanh **khác** thông lệ ⇒ theo **Nhanh** (người dùng mục tiêu quen Nhanh), trừ khi trái pháp lý hoặc trái bất biến T1–T3.

---

## 1. Pháp lý (chỉ dùng để chốt THIẾT KẾ; kết luận tuân thủ là vùng cấm)

| Mảng | Căn cứ đã có trong repo | Nhãn | Hệ quả cho quyết định |
|---|---|---|---|
| Hoá đơn điện tử | **NĐ 254/2026/NĐ-CP** (hiệu lực 01/7/2026) + **TT 91/2026/TT-BTC**; NĐ 123/2020 và NĐ 70/2025 **hết hiệu lực** (tra 12/10/2026, `docs/business-modules-proposal.md` §6.7) | ✅ | Không viện dẫn NĐ 123 trong tài liệu mới; trường XML/endpoint CQT **không** đoán — adapter + giả lập |
| Ký số | VNPT ký **2 bước** (xin hash → gửi hash đã ký); token/USB ⇒ **luôn có bước người** | ✅ | `DigitalSigningHub` là **máy trạng thái**, không phải hàm đồng bộ |
| Thuế GTGT | Phân loại thuế suất theo `docs/features/cross-cutting/XC-006-phan-loai-thue-gtgt.md`; Nhanh có mã đặc biệt `-1` không chịu thuế · `-2` không kê khai · `-100` thuế 0% | ✅ | Thuế suất là **dữ liệu có hiệu lực theo thời gian**, không hằng số trong code; mức giảm thuế tạm thời phải tra lại hiệu lực khi quyết | 
| Địa giới hành chính | Từ 01/07/2025 bỏ cấp huyện, **63 → 34 tỉnh**; phải hỗ trợ **cả 3 cấp cũ và 2 cấp mới**; mã địa chỉ của từng hãng vận chuyển là **dữ liệu đồng bộ** | ✅ | Xem `docs/features/address/addr-003-chuan-hoa-dia-chi-3-cap-2-cap.md` |
| Lưu trữ chứng từ | Luật Kế toán 88/2015/QH13 Điều 41 — **số năm cụ thể chưa xác minh** | ⬜ | Không tự đặt thời hạn xoá; mặc định **không xoá** chứng từ |
| Dữ liệu cá nhân khách | Văn bản hiện hành về bảo vệ dữ liệu cá nhân — **chưa tra trong repo** | ⬜ | Mọi quyết định xuất/xoá/chia sẻ dữ liệu khách ⇒ `CHUYEN_NGUOI` |
| Giấy phép mã nguồn | Không copy từ nguồn copyleft; danh sách port được ở `docs/business-modules-proposal.md` §7.3 | ✅ | |
| "Giống Nhanh" | Được giống bố cục/mật độ/route/nhãn/pattern; **không** copy logo, tên, asset, HTML/CSS/JS (`docs/nhanh/04-web-admin-nextjs.md` §11) | ✅ | |

---

## 2. Theo mảng nghiệp vụ

### 2.1 Hàng hoá (Catalog)

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| Mô hình **cha–con** + `ProductUnit`, **không** Variant giữ tồn/giá; 10 loại sản phẩm (thường, voucher, cân đo, IMEI, gói, dịch vụ, dụng cụ, theo lô, combo, nhiều ĐVT) | D-07, `01` §5.1 | ✅ |
| Đúng **một** ĐVT gốc; sản phẩm cha **không** bán được | D-21, D-23 | ✅ |
| "Hết hàng" là trạng thái **suy ra** từ tồn | D-19 | ✅ |
| 7 loại giá (nhập, vốn, bán, cũ, buôn, chi nhánh, kèm VAT); bảng giá theo cửa hàng/thời gian (`pricelist` có `startDate/endDate`) | `01` §5.1, §5.5 | ✅ |
| Combo: tồn = min(tồn thành phần) | `01` §5.1 | ✅ |
| Mã SKU duy nhất trong doanh nghiệp; mã vạch có thể trùng SKU hoặc để trống | thông lệ KiotViet/Sapo | 🔶 |
| Giá bán lẻ niêm yết **đã gồm VAT** khi bán cho người tiêu dùng | thông lệ bán lẻ VN | 🔶 |

### 2.2 Kho (Inventory)

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| Tồn là **vector** (tồn, đang chuyển, đang giao, tạm giữ, lỗi, bảo hành, đặt trước, chờ nhập…); `Có thể bán = Tồn trong kho − Tạm giữ − Lỗi` | `01` §5.2, D-27 | ✅ |
| **Không có "set tồn"**: mọi thay đổi đi qua **phiếu** (XNK, chuyển, kiểm) ghi sổ cái | `01` §5.2, D-43 | ✅ |
| Loại phiếu dùng **đúng số `mode` của Nhanh** | D-28 | ✅ |
| Chống oversell 3 lớp: unique index · khoá theo SKU · allotment theo kênh | README #5, `docs/features/inventory/inv-003-phan-bo-ton-theo-kenh.md` | ✅ |
| Giữ chỗ có TTL; huỷ đơn ⇒ nhả giữ chỗ | `docs/features/inventory/inv-002-giu-cho-reservation-ttl.md` | ✅ |
| **Bán âm kho**: mặc định **không cho**; nếu cần ⇒ cấu hình theo cửa hàng (KiotViet/Sapo đều có tuỳ chọn kiểu này) | thông lệ | 🔶 |
| **Giá vốn**: chưa có tiền lệ — mặc định đề xuất **bình quân gia quyền**, cấu hình hoá; ảnh hưởng kế toán ⇒ `reviewBefore = release` | thông lệ KiotViet/MISA | 🔶 |

### 2.3 Đơn hàng (Sales) — đơn giao vận

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| **4 trục** trạng thái, `FulfillmentStatus` 18 giá trị (append), UI hiển thị **một nhãn suy ra** | D-08, `docs/features/sales/sal-003-state-machine-4-truc.md` | ✅ |
| Danh mục loại đơn/nguồn đơn/lý do: dùng **số đo được** từ Open API v3, không theo con số trong kế hoạch | D-38 | ✅ |
| 3 trạng thái huỷ (khách, hệ thống, hãng vận chuyển) thuộc **trục thực hiện**; huỷ + đóng đơn trong một lời gọi | D-40 | ✅ |
| Tác động kho theo vòng đời đơn: bảng **đã hiện thực** ở D-39 thắng bảng đề xuất ở `03` §4 | D-39 | ✅ |
| DS đơn 14 tab là dữ liệu | D-41 | ✅ |
| Nút thao tác hiện theo trạng thái (bảng "thao tác × trạng thái"); trang chi tiết có tab động | `01` §4.3 | ✅ |
| Đơn sàn: **sàn là nguồn sự thật** cho trạng thái sàn; ta phản chiếu, không tự chuyển trạng thái mà sàn không cho | thông lệ tích hợp sàn | 🔶 |

### 2.4 Bán lẻ tại quầy (POS)

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| Aggregate `RetailBill` + `CashSession` riêng; **một quầy một ca**; kết ca có tiền mặt kỳ vọng/chênh lệch | D-18, D-42 | ✅ |
| Offline: IndexedDB + `ClientBillId` idempotent | D-04 | ✅ |
| Chiết khấu **một tầng**; nợ phải có khách + hạn | D-42 | ✅ |
| Đa tab + phím tắt F1/F3/F4/F6/F8/F9/F10 | `01` §4.3 | ✅ |
| Trả hàng là **chứng từ riêng**, đảo điểm/công nợ qua một cửa | D-45 | ✅ |
| Tra khách tại quầy bằng **số điện thoại** | thông lệ bán lẻ VN | 🔶 |

### 2.5 Khách hàng

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| Điểm / tiền tích luỹ / công nợ ghi qua **một cửa**; công thức `Subtotal − DiscountTotal`; vượt hạn mức ⇒ NÉM | D-45 | ✅ |
| Hạng khách theo ngưỡng tích luỹ, tự lên hạng | D-45, `N8` | ✅ |
| Phạm vi dữ liệu theo vai trò | D-46 | ✅ |
| SĐT chuẩn hoá (bắt đầu `0`), là khoá tra cứu chính; trùng SĐT ⇒ cảnh báo, không tự gộp | `04` §9 + thông lệ | 🔶 |

### 2.6 Khuyến mại

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| 2 tầng: đợt (batch) → mã (coupon); mã **duy nhất toàn hệ**; giá trị tiền hoặc % | `01` §5.5 | ✅ |
| **Nhanh không tự tính tiền giảm** ⇒ engine khuyến mại là **của ta** | `01` §5.5 | ✅ |
| Cây điều kiện AND/OR | `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` | ✅ |
| **Cộng dồn khuyến mại**: mặc định **không** cộng dồn, cho phép cấu hình theo chương trình | thông lệ | 🔶 |
| Phân bổ giảm giá xuống dòng **bảo toàn tổng** theo thuật toán của XC-001 | `docs/features/cross-cutting/XC-001-money-currency-lam-tron.md` | ✅ |

### 2.7 Thanh toán & đối soát

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| ReturnUrl **không** là bằng chứng; chỉ IPN/webhook **đúng chữ ký + khớp số tiền** mới `Succeeded` | README #12, `docs/features/payment/pay-001-cong-thanh-toan-vnpay.md` | ✅ |
| Chuyển khoản/VietQR: luồng **webhook-first** (SePay/Casso), đối soát theo nội dung chuyển khoản | README #11 | ✅ |
| Hoàn tiền không vượt số đã thu (mã `0002`) | `N`-probe thanh toán | ✅ |
| Đối soát chủ động (QueryDR) thay vì chỉ tin IPN | `docs/features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md` | ✅ |
| COD là phương thức phổ biến nhất ở VN ⇒ mọi luồng đơn phải chạy được **không** có thanh toán online | thông lệ | 🔶 |
| Gửi tiền tới NCC theo **minor units**; VND 0 chữ số thập phân | XC-001 | ✅ |

### 2.8 Vận chuyển

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| GHN là hãng đầu tiên (REST, sandbox, có `llms.txt`); GHTK sau | `docs/business-modules-proposal.md` §6.7 | ✅ |
| GHTK còn payload 3 cấp, webhook không có HMAC ⇒ IP allowlist + **đối soát chủ động** | §6.7 | ✅ |
| Đối soát COD: **không có API**, bảng kê Excel theo **phiên** ⇒ import Excel | `docs/features/reconciliation/rec-003-doi-soat-cod-import-excel.md` | ✅ |
| Đổi COD ở GHN cần OTP ⇒ **luôn có bước người** | §6.7 | ✅ |
| Khối lượng gửi hãng tính bằng **gram** | Nhanh `orderPackage` ghi gram + thông lệ API hãng | 🔶 |

### 2.9 Sàn TMĐT (ChannelHub)

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| ChannelHub **không tự tạo đơn**, chỉ phát `ChannelOrderPulledEto`; app tạo đơn idempotent theo `ExternalOrderId` | README #2 | ✅ |
| Mỗi shop một worker động; refresh token trong **khoá per-shop + kiểm hai lần** (Shopee xoay vòng refresh token) | README #7, #8 | ✅ |
| Token mã hoá, DTO chỉ trả cờ `HasRefreshToken` | README #9 | ✅ |
| Webhook sàn at-least-once; payload webhook khác REST ⇒ **2 mapper** | README #6, `01` §5.6 | ✅ |
| Phí sàn / settlement đối soát riêng | `docs/features/reconciliation/rec-002-settlement-san.md` | ✅ |

### 2.10 Kế toán & báo cáo

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| GĐ0–GĐ3: công nợ + **xuất Excel/MISA**; bút toán kép để GĐ4 | D-03 | ✅ |
| Bảng định khoản tự động của Nhanh là **spec**, không tự nghĩ lại | `01` §5.4 | ✅ |
| 12 báo cáo P0/P1, không làm đủ 90 | D-05 | ✅ |
| Read-model: grain = **dòng chứng từ**, ngày kinh doanh UTC+7, loại chứng từ đã huỷ, VAT tách riêng, chưa trừ trả hàng | D-47, D-48 | ✅ |

### 2.11 Giao diện Web Admin

| Mặc định | Nguồn | Nhãn |
|---|---|---|
| 7 pattern phải giống Nhanh: tab có đếm · lọc 2 tầng + lưu lọc · `Thao tác ▾` bulk · ẩn/hiện cột · nút theo trạng thái · tab động ở chi tiết · POS đa tab + phím tắt | `01` §4.3 | ✅ |
| Nhãn tiếng Việt; tiền `150,000` (giống Nhanh); số lượng 3 lẻ; `dd/MM/yyyy HH:mm` | `01` §4.4, `04` §9 | ✅ |
| Màu/font/icon theo token đo được của Nhanh | `04` §3 | ✅ |
| 18 màn P0 là hợp đồng thi công; màn ngoài danh sách ⇒ không tự thêm | `04` §5 | ✅ |

---

## 3. Câu hỏi mở của Nhanh Open API v3 — cách xử lý mặc định

`docs/nhanh/nhanh-pos-v3-api-inventory.md` §5 liệt kê 20 câu tài liệu Nhanh không trả lời. Khi một agent vướng
một câu trong đó:

| Loại câu hỏi | Quyết mặc định |
|---|---|
| Nghĩa của một giá trị enum chưa có bảng (`exportType`, `paymentStatus`…) | Lưu **nguyên số** Nhanh trả về + nhãn "chưa xác định"; **không** tự gán nghĩa; ghi nợ khảo sát |
| Đơn vị đo không ghi rõ (khối lượng) | Gram (🔶), đặt tên trường có đơn vị (`WeightGram`), kiểm bằng probe khi có tài khoản |
| Công thức Nhanh chỉ mô tả bằng lời (`available`) | Theo D-27 (đã chốt) |
| Tính năng Nhanh chỉ có trên UI, không có API | Không phụ thuộc Nhanh; làm theo mô hình của ta |
| Hành vi khi hai cấu trúc địa chỉ va nhau (v2 vs hãng chỉ 3 cấp) | Giữ cả hai mã; adapter của hãng tự chọn cấu trúc; thiếu mã ⇒ lỗi rõ ràng, không đoán |

---

## 4. Khi nào nghiệp vụ VN bắt buộc `CHUYEN_NGUOI`

- Bất kỳ con số pháp lý: thuế suất áp cho một mặt hàng cụ thể, thời hạn lưu trữ, mẫu số/ký hiệu hoá đơn.
- Chọn NCC (HĐĐT, ký số, SMS/Zalo, hãng vận chuyển, cổng thanh toán) — là việc **tiền + hợp đồng**.
- Chính sách với khách: đổi trả, hoàn tiền, bảo hành, điểm hết hạn — vì in lên hoá đơn/website và ràng buộc pháp lý với người mua.
  ✅ Trong lúc chờ: **dựng mô hình cấu hình được** (số ngày đổi trả, tỷ lệ quy đổi điểm) với giá trị mẫu, đánh dấu `reviewBefore = release`.
