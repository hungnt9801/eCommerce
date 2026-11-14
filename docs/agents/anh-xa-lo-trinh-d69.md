# Ánh xạ việc còn lại của `implementation-plan.md` vào lộ trình chính (`D-69`)

> **Ngày:** 14/11/2026 · **Căn cứ:** `D-69` trong `docs/nhanh/decisions.md` (lộ trình chính = `docs/nhanh/05-lo-trinh-rui-ro.md`
> + `docs/nhanh/07-ke-hoach-trien-khai.md`) · bước **1b** của `docs/agents/ke-hoach-he-thong-agent.md`.
> **Mục đích:** để không việc nào của lộ trình cũ (GĐ0–GĐ4 website-first, phiên `S0…S19`) bị **rơi mất** khi chuyển sang lộ trình
> Nhanh-hoá, và không việc nào bị **thêm vào** mà không có người quyết.
> **Không sửa `07`.** Đây là đầu vào để lập `§WP-14+`; lập WP là bước riêng.
> ✅ **14/11/2026 — đã lập `§WP-14`** (cổng tài liệu GĐ3, 18 tài liệu, 3 đợt) trong `docs/nhanh/07-ke-hoach-trien-khai.md` theo `D-71`.

## 0. Cách đo trạng thái code (nói thẳng giới hạn)

Cột "Code hiện có" đo bằng **đếm tệp `.cs` chứa từ khoá** trong `modules/`, `src/`, `shared/` (14/11/2026) — **chưa đọc code**,
chỉ đủ phân biệt "chưa có gì" với "đã có dấu vết". Cột "Duyệt" tra theo thứ tự biên bản duyệt → `docs/features/README.md` §2:
`26` tài liệu (`XC-*` + GĐ1 cũ) duyệt theo `docs/bien-ban-duyet-gd1-2026-10-29.md` + `ADDR-004` (17/10/2026) ⇒ **27/55**.

## 1. Bảng ánh xạ

Nhãn ánh xạ: **R** = rõ ràng (có giai đoạn tương ứng trong `05` hoặc có `D-xx` chỉ thẳng) · **H** = **không** ánh xạ được ⇒ hỏi chủ
dự án (theo `D-69` ②).

| Mã (lộ trình cũ) | Nội dung | Code hiện có | Duyệt | → Lộ trình chính | Nhãn |
|---|---|---|---|---|---|
| `ADDR-001…003` | Đơn vị hành chính 2 cấp, mã địa chỉ theo hãng, chuẩn hoá 3↔2 cấp | **Có** (76 tệp `AdministrativeUnit`; đồng bộ mã GHN/GHTK trong `modules/Address`) | ❌ | Nền cho **GĐ3 vận chuyển** của `05` — duyệt tài liệu trước khi `SHP-*` dùng | **R** |
| `ADDR-004` | Sổ địa chỉ khách | Có | ✅ | Đã dùng ở `CUS-001` / `N8` | **R** |
| `EINV-001…003`, `SIGN-001` | Phát hành / gửi CQT / điều chỉnh HĐĐT, ký 2 bước | **Có** (`modules/EInvoice`, `modules/Signing`) | ❌ | **GĐ4** của `05` ("UI HĐĐT 4 tab + lỗi"); căn cứ pháp lý đã đổi sang NĐ 254/2026 — tài liệu phải rà lại | **R** |
| `PRO-001` | Voucher / coupon | Chưa (1 tệp nhắc tới) | ❌ | **GĐ3** của `05` (khuyến mại: chiết khấu + coupon + tích điểm + bảng giá) · mô hình Nhanh 2 tầng đợt → mã | **R** |
| `PRO-002` | Cây điều kiện AND/OR, engine khuyến mại | Chưa | ❌ | **GĐ3** của `05` (engine là của ta — Nhanh không tự tính) | **R** |
| `SHP-001`, `SHP-004` | Adapter GHN, GHTK | Chưa (GHN chỉ có ở đồng bộ mã địa chỉ) | ❌ | **GĐ3** của `05` (vận chuyển GHN/GHTK adapter, biên bản bàn giao) | **R** |
| `SHP-002` | Webhook trạng thái vận đơn | Chưa | ❌ | **GĐ3** của `05` | **R** |
| `SHP-003`, `REC-003` | COD thu hộ, đổi COD · đối soát COD import Excel | Chưa | ❌ | **GĐ3** của `05` ("đối soát COD") | **R** |
| `REC-002` | Settlement sàn | Chưa | ❌ | **GĐ5** của `05` (sàn TMĐT, đối soát COD sàn) | **R** |
| `SAL-006` | Trả hàng / RMA & hoàn tiền cho **đơn** | **Có một phần** (24 tệp `OrderReturn`/`Rma`, phiên `S14`) | ❌ | Trả hàng **tại quầy** đã làm ở GĐ2 của `05` (`N7`). Trả hàng **đơn giao vận** không có dòng riêng trong `05` — gần nhất là loại đơn "khách trả lại / hoàn một phần" của Nhanh (`docs/nhanh/01-khao-sat-nhanh-vn.md` §5.3) | **H** |
| `REP-001`, `REP-002` | Read-model, báo cáo doanh thu & lãi gộp | **Có** (`WP-08` / `N9`: 8 bảng `Rpt*`, 2 báo cáo) | ❌ | **GĐ3** của `05` (6 báo cáo) — phần còn lại | **R** |
| `REP-003` | Báo cáo tồn luân chuyển & **hiệu suất kênh** | Chưa | ❌ | Tồn kho ⇒ **GĐ3**; hiệu suất **kênh sàn** ⇒ **GĐ5** | **R** |
| `NOT-001`, `NOT-002` | Template + hàng đợi thông báo; email / SMS / Zalo OA | **Có** (`modules/Messaging`) | ❌ | `05` chỉ có "Zalo/SMS **marketing**" ở **Sau GĐ5**; thông báo **giao dịch** (đơn, vận đơn) không có dòng | **H** |
| `ACC-001` | Xuất liệu kế toán Excel/CSV | Chưa | ❌ | `D-03`: GĐ0–GĐ3 làm **công nợ + xuất Excel/MISA** ⇒ **GĐ3** (trước kế toán kép ở **GĐ4**) | **R** |
| `CHN-001…004` | OAuth shop, connector Shopee/Lazada, allotment đa kênh | **Có một phần** (ký Shopee, token manager, factory connector) | ❌ | **GĐ5** của `05` (pull đơn, ghép SP sàn, push tồn) | **R** |
| `LOY-001` | Điểm thưởng | **Có một phần** (điểm/hạng khách — `D-45`, `N8`) | ❌ | **GĐ3** của `05` ("tích điểm" trong khuyến mại) | **R** |
| `REV-001` | Đánh giá sản phẩm (storefront) | Chưa | ❌ | `05` không có; `D-06`: **không** làm lại storefront | **H** |
| `CAT-003` (phần còn lại), `PRC-002` (phần còn lại), `WEB-002` (phần còn lại) | Media/SEO/hiển thị theo kênh, giá bậc thang, storefront | Có một phần | ✅ | Phần **storefront** ⇒ ngoài phạm vi theo `D-06`; phần **giá bậc thang** ⇒ "bảng giá" của **GĐ3** | **H** cho phần storefront · **R** cho giá |

## 2. Ba phát hiện phải biết trước khi lập `§WP-14`

1. **Toàn bộ việc của GĐ3 trong `05` đều dựa trên tài liệu CHƯA duyệt** (`PRO-*`, `SHP-*`, `REC-003`, `ACC-001`, `REP-*`, `ADDR-001…003`).
   Theo `docs/implementation-plan.md` §A.5 (vẫn hiệu lực theo `D-69` ③) ⇒ **không được code** trước khi duyệt. Vì vậy `WP-14` hợp lý
   là **"duyệt tài liệu GĐ3"** (DOCREV rà theo D1–D7 + người duyệt ghi biên bản) — không phải code.
2. Tài liệu GĐ2/GĐ3 cũ được viết cho mô hình **website-first** (trước `D-07`, `D-08`, `D-18`): nhiều chỗ còn dùng `ProductVariant`,
   7 trạng thái đơn, không có POS. Duyệt nguyên văn sẽ **mâu thuẫn** với code hiện tại ⇒ cần **rà lại theo mô hình Nhanh** trước khi duyệt.
3. `EINV-*` viện dẫn căn cứ pháp lý cũ (NĐ 123/2020, NĐ 70/2025 — đã hết hiệu lực từ 01/7/2026 theo `docs/business-modules-proposal.md` §6.7)
   ⇒ phải cập nhật trước khi đưa vào GĐ4.

## 3. Bốn câu cần chủ dự án quyết (nhãn **H**) — ✅ **ĐÃ CHỐT `D-70` (14/11/2026), đúng khuyến nghị**

> Kết quả: `SAL-006` (RMA đơn) và `NOT-*` (bản tối thiểu) **vào GĐ3**; `REV-001` **bỏ**; storefront còn lại của `CAT-003`, `WEB-002`
> **đóng băng**. ⇒ Phạm vi GĐ3 để lập `§WP-14+`: `PRO-001/002` · `SHP-001…004` · `REC-003` · `SAL-006` · `NOT-001/002` (tối thiểu) ·
> `ACC-001` · `REP-*` (phần còn lại) · `LOY-001` (phần còn lại) · giá bậc thang `PRC-002` · `ADDR-001…003` (duyệt tài liệu).

| # | Câu hỏi | Khuyến nghị |
|---|---|---|
| H1 | `SAL-006` — RMA cho **đơn giao vận** đặt ở giai đoạn nào? | **GĐ3**, cùng vận chuyển (hàng hoàn qua hãng là nguồn trả hàng chính của đơn giao vận); làm theo loại đơn "khách trả lại / hoàn một phần" của Nhanh |
| H2 | `NOT-*` — thông báo **giao dịch** (xác nhận đơn, trạng thái vận đơn) có thuộc phạm vi trước GĐ5 không? | **GĐ3 bản tối thiểu** (email + một kênh), Zalo/SMS marketing giữ ở "Sau GĐ5" |
| H3 | `REV-001` — đánh giá sản phẩm | **Bỏ khỏi lộ trình** (theo `D-06`), giữ tài liệu ở trạng thái lưu trữ |
| H4 | Phần storefront còn lại của `CAT-003`, `WEB-002` | **Đóng băng** theo `D-06`/`D-10`, không lập WP |

Bốn câu này nằm trong vùng cấm #5 của agent `decider` (thêm/bỏ phạm vi) ⇒ **không** tự quyết; đã ghi khuyến nghị để duyệt một lần.
