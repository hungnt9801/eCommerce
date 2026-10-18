# Danh mục tài liệu thiết kế feature

> 🚨 **MỖI MODULE MỘT THƯ MỤC RIÊNG** — quy ước từ **17/10/2026**: `docs/features/<module>/<mã>-<slug>.md`.
> Bảng ánh xạ tiền tố mã ↔ thư mục: [`design-doc-guidelines.md`](../design-doc-guidelines.md) **§3.1**.
> Mẫu để viết tài liệu mới: [`feature-design-template.md`](../templates/feature-design-template.md).

| Hạng mục | Giá trị đo được (17/10/2026) |
|---|---|
| Thư mục module | **20** |
| Tài liệu thiết kế | **55** (khớp danh sách `implementation-plan.md` Phụ lục A.3) |
| Tệp khác trong cây | **1** — [`XC-006-phieu-hoi-ke-toan.md`](cross-cutting/XC-006-phieu-hoi-ke-toan.md) là **phiếu hỏi kế toán**, không phải tài liệu thiết kế |
| Đủ **16/16 mục** theo mẫu | ✅ **55/55** |
| 🚨 **Đã được con người `Approved`** | 🟡 **1/55** (`ADDR-004`, 17/10/2026) — xem §"Trạng thái thật" bên dưới |

---

## 1. Hai cổng kiểm cho tài liệu — chạy TRƯỚC khi commit

```powershell
# ① Sơ đồ Mermaid phải parse được (parser thật, không phải regex)
npm --prefix tools run check:mermaid

# ② Mọi tham chiếu đường dẫn phải trỏ tới tệp CÓ THẬT (thoát 1 nếu có tham chiếu chết)
pwsh -File tools\kiem-link-tai-lieu.ps1
```

📌 Đo ngay 17/10/2026 (sau khi thêm cổng): Mermaid **213 khối · 0 lỗi** · tham chiếu **0 chết** (249 link markdown + 298 đường dẫn backtick).

🔑 **Vì sao phải là cổng:** một tham chiếu chết **không làm đỏ `dotnet build`** — đúng cùng loại lỗi
hỏng-âm-thầm như Mermaid. Khi tài liệu được dồn vào thư mục module, **mọi** đường dẫn tương đối **sâu thêm một
cấp**; lần tổ chức lại đầu tiên đã đo được **175 tham chiếu chết**, trong đó có **một sai lệch CÓ TỪ TRƯỚC**
(~100 chỗ trỏ tới `eCommerceMetrics.cs` đã bị di chuyển sang `shared/eCommerce.Kernel/`).

📌 Cổng ② có test cho chính nó: `tools\test-cong-kiem.ps1` **nhóm 1b**, 5 ca `R`…`V` (cả chiều đỏ, cả chiều
xanh, và ca chống dương tính giả).

---

## 2. Trạng thái thật — **đọc kỹ, đừng suy ra từ số lượng**

⚠️ **"Đã có tệp" KHÁC "đã được duyệt".**

| Sự thật | Bằng chứng |
|---|---|
| Cả **55** tài liệu đã **có tệp**, đủ 16/16 mục | đo bằng `grep '^## N\.'` cho N=0..15 |
| 🟡 **1/55** tài liệu đã được **con người duyệt** — `ADDR-004` (17/10/2026); **54** tệp vẫn `Draft` | đọc dòng `**Trạng thái**` của từng tệp |
| Vì vậy **cổng Definition of Ready** của `implementation-plan.md` §A.5 **CHƯA đạt** | §A.5: *"toàn bộ tài liệu của giai đoạn đó đã Approved"* |

🔑 **Hệ quả cần nói thẳng:** về **số lượng tệp** thì giai đoạn tài liệu đã xong, nhưng về **cổng chất lượng**
thì chưa có tài liệu nào được duyệt. Việc còn lại **không phải viết thêm**, mà là **review + Approved**.

---

## 3. Phân bố theo giai đoạn

| Giai đoạn | SL | Mã tài liệu |
|---|:--:|---|
| **Nền** (cross-cutting) | 7 | `XC-001` … `XC-007` |
| **GĐ0** Pháp lý | 8 | `ADDR-001` … `ADDR-004` · `EINV-001` … `EINV-003` · `SIGN-001` |
| **GĐ1** Bán trên website | 19 | `CAT-001` … `CAT-003` · `PRC-001` … `PRC-002` · `INV-001` … `INV-003` · `SAL-001` … `SAL-005` · `CUS-001` · `REC-001` · `PAY-001` · `WEB-001` … `WEB-003` |
| **GĐ2** Vận hành & tuân thủ VN | 9 | `PRO-001` … `PRO-002` · `SHP-001` … `SHP-004` · `REC-002` … `REC-003` · `SAL-006` |
| **GĐ3** Điều hành | 6 | `REP-001` … `REP-003` · `NOT-001` … `NOT-002` · `ACC-001` |
| **GĐ4** Tăng trưởng | 6 | `CHN-001` … `CHN-004` · `LOY-001` · `REV-001` |
| | **55** | |

---

## 4. Ba tài liệu **đã THIẾU** và đã viết trong phiên 17/10/2026

Trước phiên này danh sách là **52**; đối chiếu **code thật** thì thiếu **3** — mỗi tài liệu ứng với một lỗ hổng
**có bằng chứng**, không phải suy đoán:

| Mã | Vì sao THIẾU (bằng chứng) | Đã viết |
|---|---|---|
| 🚨 **`ADDR-004`** | Nợ **CHẶN #1** (`handover` §3.1): entity `Address` của sổ địa chỉ **chưa từng tồn tại** (`git log --all` = 0), trong khi `implementation-plan` §6.1 **không** liệt kê `Address` ở AddressHub còn §7.1 lại xếp nó vào **Customers**, ngược với `CUS-001` **ADR-01** (Address thuộc AddressHub). ⇒ **hai nguồn sự thật** + **một nợ chặn** | [`ADDR-004`](address/addr-004-so-dia-chi-khach.md) — chốt chủ sở hữu, state machine, index, migration |
| 🚨 **`PAY-001`** | Module `PaymentHub` **đã có code thật** (`PayTransactions`, `VnPayPaymentProvider`, `PaymentAppService`) nhưng **không tài liệu nào sở hữu**: `SAL-002` ghi rõ *"Thanh toán / IPN — thuộc `PaymentHub` + `REC-001`"*, còn `REC-001` chỉ làm đối soát | [`PAY-001`](payment/pay-001-cong-thanh-toan-vnpay.md) — mô tả đúng code đang có + ghi rõ phần còn thiếu |
| 🚨 **`XC-007`** | Nợ *"chưa có CI"*: `handover` §7 việc #2, `web-001` §99.5. 🔑 *"Cổng không chạy tự động thì không phải cổng"* | [`XC-007`](cross-cutting/XC-007-ci-cd-pipeline.md) — pipeline, cổng, phát hành, rollback |

⚠️ **Nợ CHẶN #2 KHÔNG phải nợ tài liệu:** hoàn tiền tiền thật không kiểm chứng được vì
`"TmnCode": ""` và `"HashSecret": ""` **rỗng** (`handover` §3.1). 📌 Tài liệu `PAY-001` chỉ có thể **ghi rõ**
điều đó, **không** giải quyết được — cần **credential sandbox VNPay**.

---

## 5. Cách thêm một tài liệu mới

1. Chọn **mã** theo module (bảng §3.1 của `design-doc-guidelines.md`) — ví dụ `SHP-005`.
2. Copy [`feature-design-template.md`](../templates/feature-design-template.md) vào
   `docs/features/<module>/<mã>-<slug>.md`.
3. Viết **đủ 16 mục** — mục không áp dụng ghi **"Không áp dụng + lý do"**, **không xoá mục**.
4. Chạy **cả hai** cổng ở §1. 🚨 Nếu bạn **thêm** tài liệu thì cập nhật thêm: danh sách Phụ lục A
   (`implementation-plan.md`) và bảng trong tệp này.
5. 🔒 Mermaid: **không** dùng `;` trong text sơ đồ, **không** dùng `<...>` làm placeholder (dùng `«...»`).

---

## 6. Danh mục đầy đủ theo thư mục module

> 📌 Bảng dưới **sinh từ cây thư mục thật** (số dòng đọc từ tệp), không viết tay — để con số không bị trôi.
> "Trạng thái" ở đây là **trạng thái tài liệu** (`Draft`), **không** phải trạng thái code.

### `accounting/` — AccountingHub — xuất liệu kế toán

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `ACC-001` | [Xuất liệu kế toán Excel/CSV](accounting/acc-001-xuat-lieu-ke-toan-excel-csv.md) | 1004 | 🟡 Draft · 16/16 mục |

### `address/` — AddressHub — đơn vị hành chính, mã hãng vận chuyển, sổ địa chỉ

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `ADDR-001` | [Tra cứu đơn vị hành chính 2 cấp (tỉnh + xã) và 3 cấp cũ](address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md) | 833 | 🟡 Draft · 16/16 mục |
| `ADDR-002` | [Đồng bộ mã địa chỉ theo từng hãng vận chuyển](address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md) | 928 | 🟡 Draft · 16/16 mục |
| `ADDR-003` | [Chuẩn hoá địa chỉ khách hàng: convert 3 cấp ↔ 2 cấp, xử lý ca sáp nhập tỉnh](address/addr-003-chuan-hoa-dia-chi-3-cap-2-cap.md) | 1013 | 🟡 Draft · 16/16 mục |
| `ADDR-004` | [Sổ địa chỉ khách: entity `Address` (AddressHub) + `CustomerAddressLink`](address/addr-004-so-dia-chi-khach.md) | 1088 | 🟢 Approved · 🔨 **đang triển khai** (phần A xong ở `S1`) |

### `catalog/` — Catalog — sản phẩm, biến thể, danh mục, media/SEO

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `CAT-001` | [Sản phẩm và biến thể (SKU)](catalog/cat-001-san-pham-va-bien-the-sku.md) | 1076 | 🟡 Draft · 16/16 mục |
| `CAT-002` | [Cây danh mục & thuộc tính/tổ hợp biến thể](catalog/cat-002-cay-danh-muc-thuoc-tinh-bien-the.md) | 1333 | 🟡 Draft · 16/16 mục |
| `CAT-003` | [Media, SEO slug và hiển thị theo kênh](catalog/cat-003-media-seo-slug-hien-thi-theo-kenh.md) | 2101 | 🟡 Draft · 16/16 mục |

### `channel/` — ChannelHub — OAuth shop, connector Shopee/Lazada, allotment

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `CHN-001` | [Kết nối shop qua OAuth (đã có phần nền)](channel/chn-001-ket-noi-shop-qua-oauth.md) | 828 | 🟡 Draft · 16/16 mục |
| `CHN-002` | [Connector Shopee: pull đơn, push tồn/giá](channel/chn-002-connector-shopee.md) | 907 | 🟡 Draft · 16/16 mục |
| `CHN-003` | [Connector Lazada (chứng minh abstraction đúng)](channel/chn-003-connector-lazada.md) | 938 | 🟡 Draft · 16/16 mục |
| `CHN-004` | [Allotment & đồng bộ tồn đa kênh](channel/chn-004-allotment-dong-bo-ton-da-kenh.md) | 926 | 🟡 Draft · 16/16 mục |

### `cross-cutting/` — Nền tảng (dùng chung mọi module)

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `XC-001` | [Money, Currency & chính sách làm tròn](cross-cutting/XC-001-money-currency-lam-tron.md) | 631 | 🟡 Draft · 16/16 mục |
| `XC-002` | [Outbox / Inbox & chống trùng sự kiện](cross-cutting/XC-002-outbox-inbox-chong-trung-su-kien.md) | 1339 | 🟡 Draft · 16/16 mục |
| `XC-003` | [Khoá phân tán: quy ước & phạm vi](cross-cutting/XC-003-khoa-phan-tan-quy-uoc.md) | 556 | 🟡 Draft · 16/16 mục |
| `XC-004` | [Xác thực, phân quyền & chiều tenant](cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md) | 676 | 🟡 Draft · 16/16 mục |
| `XC-005` | [Observability: log có cấu trúc, CorrelationId & metric](cross-cutting/XC-005-observability-log-metric.md) | 603 | 🟡 Draft · 16/16 mục |
| `XC-006` | [Phân loại thuế GTGT theo SKU & mức thuế suất theo thời gian](cross-cutting/XC-006-phan-loai-thue-gtgt.md) | 799 | 🟡 Draft · 16/16 mục |
| `—` | [PHIẾU HỎI KẾ TOÁN — Mẫu 01 giảm thuế GTGT 8%](cross-cutting/XC-006-phieu-hoi-ke-toan.md) | 109 | 📄 không phải tài liệu thiết kế |
| `XC-007` | [CI/CD: cổng kiểm tự động và quy trình phát hành](cross-cutting/XC-007-ci-cd-pipeline.md) | 1117 | 🟡 Draft · 16/16 mục |

### `customers/` — Customers — hồ sơ khách

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `CUS-001` | [Hồ sơ khách hàng và sổ địa chỉ](customers/cus-001-ho-so-khach-hang-so-dia-chi.md) | 2007 | 🟡 Draft · 16/16 mục |

> 🚨 Sổ địa chỉ **không** thuộc module này: `Address` do **AddressHub** sở hữu, `Customers` giữ bảng liên kết
> `CustomerAddressLink` — chốt tại [`ADDR-004`](address/addr-004-so-dia-chi-khach.md).

### `einvoice/` — EInvoiceHub — phát hành / gửi CQT / điều chỉnh hoá đơn

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `EINV-001` | [Phát hành hoá đơn điện tử (NĐ 254/2026/NĐ-CP + TT 91/2026/TT-BTC)](einvoice/einv-001-phat-hanh-hoa-don-dien-tu.md) | 1176 | 🟡 Draft · 16/16 mục |
| `EINV-002` | [Gửi cơ quan thuế và nhận kết quả (SOAP/XML, parse `OK:` và `ERR:`)](einvoice/einv-002-gui-co-quan-thue-nhan-ket-qua.md) | 867 | 🟡 Draft · 16/16 mục |
| `EINV-003` | [Điều chỉnh, thay thế, huỷ hoá đơn](einvoice/einv-003-dieu-chinh-thay-the-huy-hoa-don.md) | 1051 | 🟡 Draft · 16/16 mục |

### `inventory/` — Inventory — kho, sổ cái, giữ chỗ, phân bổ theo kênh

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `INV-001` | [Nhập, chuyển, kiểm kê và sổ cái kho (append-only)](inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md) | 2843 | 🟡 Draft · 16/16 mục |
| `INV-002` | [Giữ chỗ (reservation) + TTL và job nhả kho](inventory/inv-002-giu-cho-reservation-ttl.md) | 908 | 🟡 Draft · 16/16 mục |
| `INV-003` | [Phân bổ tồn theo kênh và tra tồn khả dụng](inventory/inv-003-phan-bo-ton-theo-kenh.md) | 1451 | 🟡 Draft · 16/16 mục |

### `loyalty/` — Loyalty — điểm thưởng

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `LOY-001` | [Điểm thưởng](loyalty/loy-001-diem-thuong.md) | 785 | 🟡 Draft · 16/16 mục |

### `notifications/` — Notifications — template, hàng đợi, kênh gửi

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `NOT-001` | [Template và hàng đợi thông báo (chống gửi trùng)](notifications/not-001-template-hang-doi-thong-bao.md) | 1156 | 🟡 Draft · 16/16 mục |
| `NOT-002` | [Kênh gửi: email, SMS, Zalo OA](notifications/not-002-kenh-gui-email-sms-zalo-oa.md) | 987 | 🟡 Draft · 16/16 mục |

### `payment/` — PaymentHub — cổng thanh toán VNPay, IPN, hoàn tiền

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `PAY-001` | [Cổng thanh toán VNPay: tạo giao dịch, IPN/webhook, hoàn tiền & đối soát](payment/pay-001-cong-thanh-toan-vnpay.md) | 1229 | 🟡 Draft · 16/16 mục |

### `pricing/` — Pricing — bảng giá, giá theo kênh, giá bậc thang

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `PRC-001` | [Bảng giá và giá theo kênh](pricing/prc-001-bang-gia-va-gia-theo-kenh.md) | 1167 | 🟡 Draft · 16/16 mục |
| `PRC-002` | [Giá bậc thang (tier) và pipeline tính giá + làm tròn](pricing/prc-002-gia-bac-thang-pipeline-tinh-gia.md) | 1404 | 🟡 Draft · 16/16 mục |

### `promotions/` — Promotions — voucher, engine điều kiện AND/OR

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `PRO-001` | [Voucher / Coupon: phát hành, giới hạn lượt & điều kiện áp dụng](promotions/pro-001-voucher-coupon.md) | 1063 | 🟡 Draft · 16/16 mục |
| `PRO-002` | [Cây điều kiện AND/OR & engine khuyến mãi (guard bùng nổ tổ hợp)](promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md) | 1026 | 🟡 Draft · 16/16 mục |

### `reconciliation/` — Reconciliation — đối soát thanh toán, settlement, COD

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `REC-001` | [Đối soát thanh toán (querydr định kỳ)](reconciliation/rec-001-doi-soat-thanh-toan-querydr.md) | 1462 | 🟡 Draft · 16/16 mục |
| `REC-002` | [Settlement sàn (phí sàn, tiền phải trả, chênh lệch)](reconciliation/rec-002-settlement-san.md) | 1014 | 🟡 Draft · 16/16 mục |
| `REC-003` | [Đối soát COD: import file Excel theo "phiên"](reconciliation/rec-003-doi-soat-cod-import-excel.md) | 1040 | 🟡 Draft · 16/16 mục |

### `reporting/` — Reporting — read-model và báo cáo

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `REP-001` | [Read-model và job tổng hợp](reporting/rep-001-read-model-job-tong-hop.md) | 1163 | 🟡 Draft · 16/16 mục |
| `REP-002` | [Báo cáo doanh thu và lãi gộp](reporting/rep-002-bao-cao-doanh-thu-lai-gop.md) | 1145 | 🟡 Draft · 16/16 mục |
| `REP-003` | [Báo cáo tồn kho luân chuyển & hiệu suất kênh](reporting/rep-003-bao-cao-ton-kho-hieu-suat-kenh.md) | 946 | 🟡 Draft · 16/16 mục |

### `reviews/` — Reviews — đánh giá sản phẩm

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `REV-001` | [Đánh giá sản phẩm (có kiểm duyệt)](reviews/rev-001-danh-gia-san-pham.md) | 1027 | 🟡 Draft · 16/16 mục |

### `sales/` — Sales — giỏ hàng, checkout, đơn, trạng thái, webhook sàn, RMA

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `SAL-001` | [Giỏ hàng](sales/sal-001-gio-hang.md) | 918 | 🟡 Draft · 16/16 mục |
| `SAL-002` | [Checkout và đặt hàng (tính lại giá + giữ kho)](sales/sal-002-checkout-va-dat-hang.md) | 1014 | 🟡 Draft · 16/16 mục |
| `SAL-003` | [State machine 4 trục và lịch sử trạng thái](sales/sal-003-state-machine-4-truc.md) | 998 | 🟡 Draft · 16/16 mục |
| `SAL-004` | [Huỷ đơn và compensation (bù trừ)](sales/sal-004-huy-don-va-compensation.md) | 892 | 🟡 Draft · 16/16 mục |
| `SAL-005` | [Webhook đơn từ sàn (idempotent, chịu được sai thứ tự)](sales/sal-005-webhook-don-tu-san.md) | 1369 | 🟡 Draft · 16/16 mục |
| `SAL-006` | [Trả hàng / RMA và hoàn tiền (một phần / toàn bộ)](sales/sal-006-tra-hang-rma-hoan-tien.md) | 1988 | 🟡 Draft · 16/16 mục |

### `shipping/` — ShippingHub — GHN, GHTK, webhook vận đơn, COD

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `SHP-001` | [Adapter GHN: tính phí và tạo vận đơn](shipping/shp-001-adapter-ghn-tinh-phi-van-don.md) | 1151 | 🟡 Draft · 16/16 mục |
| `SHP-002` | [Webhook trạng thái vận đơn & đối soát](shipping/shp-002-webhook-trang-thai-van-don.md) | 1086 | 🟡 Draft · 16/16 mục |
| `SHP-003` | [COD: thu hộ, đổi COD (⚠️ GHN cần OTP)](shipping/shp-003-cod-thu-ho-doi-cod.md) | 1130 | 🟡 Draft · 16/16 mục |
| `SHP-004` | [Adapter GHTK (xác minh địa chỉ 2 cấp trước)](shipping/shp-004-adapter-ghtk.md) | 1008 | 🟡 Draft · 16/16 mục |

### `signing/` — DigitalSigningHub — ký 2 bước, chứng thư số

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `SIGN-001` | [Quy trình ký 2 bước thủ công & quản lý chứng thư số](signing/sign-001-ky-2-buoc-thu-cong-chung-thu-so.md) | 947 | 🟡 Draft · 16/16 mục |

### `web/` — Web — UI admin & storefront

| Mã | Tài liệu | Dòng | Trạng thái |
|---|---|---:|---|
| `WEB-001` | [UI admin: khung, đăng nhập, menu, phân quyền](web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md) | 3538 | 🟡 Draft · 16/16 mục |
| `WEB-002` | [Storefront: duyệt và chi tiết sản phẩm (SEO)](web/web-002-storefront-duyet-chi-tiet-san-pham.md) | 808 | 🟡 Draft · 16/16 mục |
| `WEB-003` | [Storefront: giỏ hàng và checkout](web/web-003-storefront-gio-hang-checkout.md) | 1017 | 🟡 Draft · 16/16 mục |

---

## 7. Lịch sử

| Ngày | Thay đổi |
|---|---|
| 17/10/2026 | Tổ chức lại **53** tệp từ `docs/features/` phẳng thành **20** thư mục module; **579 lượt** sửa tham chiếu chéo (gồm 2 sai lệch **có từ trước**: ~100 chỗ trỏ `eCommerceMetrics.cs` đã bị di chuyển, và 39 link ở `cat-001` sai độ sâu); thêm cổng `kiem-link-tai-lieu.ps1` + 5 ca test `R`…`V`; viết **3** tài liệu thiếu (`ADDR-004`, `PAY-001`, `XC-007`); sửa dòng trạng thái đã lỗi thời ở `cus-001`/`sal-001`/`sal-002`/`web-003` |
