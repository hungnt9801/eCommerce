# 05 — Lộ trình, khối lượng, rủi ro và quyết định cần chốt

> Cách đọc: §1 là **giả định** (nếu sai thì mọi con số dưới đổi theo). §2 là lộ trình 6 giai đoạn.
> §3 là khối lượng theo dev-week. §4 rủi ro. §5 là **những câu bạn phải trả lời** trước khi tôi viết code.

---

## 1. Giả định lập kế hoạch

| Giả định | Giá trị | Nếu khác |
|---|---|---|
| Đội | **3 người**: 1 backend (ABP), 1 frontend (Next.js), 1 full-stack/QA | 1 người → ×2.5 thời gian |
| Sprint | 2 tuần | |
| Năng suất | ~0.7 dev-week hữu ích / người / tuần (đã trừ họp, sửa lỗi) | |
| Ưu tiên | P0 trước, P1 sau; **không** làm song song 2 module lớn | |
| Môi trường | Dev có Docker + PostgreSQL + Redis; có tài khoản Nhanh để đối chiếu UI | |
| Phạm vi | Web Admin (desktop-first) + API; **không** app mobile, **không** website builder | |
| Chất lượng | Mỗi màn P0 có test + E2E; không nợ test | |

---

## 2. Lộ trình 6 giai đoạn

### GĐ0 — Chuẩn bị & chốt hợp đồng (2 tuần)

| Việc | Đầu ra |
|---|---|
| Chốt 6 quyết định ở §5 | Biên bản quyết định (`docs/nhanh/decisions.md`) |
| Nâng Node ≥ 24.22.1, cài REA, đăng ký MCP | `rea doctor` xanh |
| Bật NSwag → `openapi.json` trong CI | `packages/api-client` sinh type tự động |
| Dựng `apps/admin-web` skeleton + tokens + login + AppShell | Deploy staging, đăng nhập được |
| Spike: multi-tenant (TenantManagement) 3 ngày | Kết luận có/không + ước lượng |
| Chốt P0 list (18 màn) + wireframe từng màn | Ảnh/Figma 18 màn (không cần tệp trong repo ở bước này) |

**Demo:** đăng nhập → thấy sidebar 180 mục (lọc theo quyền) → mở 1 trang danh sách rỗng.

### GĐ1 — Lõi hàng hóa + kho + đơn (6 tuần, sprint 1–3)

| Tuần | Backend | Frontend |
|---|---|---|
| 1–2 | Refactor `Product` (Type/Units/ParentId/VAT), migration + sửa test | Màn 4 (DS SP), màn 5 (form type Thường) |
| 3–4 | `StockBalance` 9 cột + `StockBill` mode + Post + sổ cái | Màn 6 (chi tiết SP), màn 7–8 (phiếu kho) |
| 5–6 | `Order` 18 trạng thái + transition API + reason | Màn 10–11 (DS + chi tiết đơn) |

**DoD:** tạo SP → nhập kho → Post → tồn đổi → tạo đơn → xác nhận → xuất kho. Có test tích hợp
(1 test/flow), E2E Playwright cho 2 flow.

### GĐ2 — POS & trả hàng (4 tuần, sprint 4–5)

| Việc | Đầu ra |
|---|---|
| `RetailBill` + `CashSession` + thanh toán đa phương thức | API `/pos/*` |
| POS UI đa tab, F1–F10, quét mã, chiết khấu, in | Màn 12–13 |
| Trả hàng tại quầy + hoàn điểm/công nợ | Màn 14 |
| IndexedDB offline + `ClientBillId` idempotent | Bán được khi rớt mạng |
| Mẫu in + route in | Màn 18 (bản P0) |

**Demo:** bán 3 SP, chia 2 phương thức thanh toán, in hoá đơn, kết ca — số khớp báo cáo.

### GĐ3 — Đa kênh, khách hàng, khuyến mại, báo cáo (6 tuần, sprint 6–8)

Khách hàng (5 tab, cấp độ/điểm/công nợ) · khuyến mại (chiết khấu + coupon + tích điểm + bảng giá) ·
vận chuyển (GHN/GHTK adapter, biên bản bàn giao, đối soát COD) · 6 báo cáo (doanh thu, đơn, bán lẻ, tồn,
sản phẩm, khách) · dashboard.

### GĐ4 — Kế toán & HĐĐT (6 tuần, sprint 9–11)

Chart of accounts + journal + auto posting 13 nghiệp vụ + thu chi + công nợ + kỳ kế toán ·
UI HĐĐT 4 tab + lỗi · nhà cung cấp + công nợ 331 · 3 báo cáo kế toán (KQHĐKD, BCĐKT, sổ).

**Chặn:** cần **kế toán thật review** bảng định khoản trước khi code (1 tuần của họ).

### GĐ5 — Sàn TMĐT & hoàn thiện (6 tuần, sprint 12–14)

Pull đơn Shopee/TikTok/Lazada/Tiki · ghép SP sàn · push tồn · đối soát COD sàn · đơn trùng ·
bảo hành (P2) · tối ưu hiệu năng + hardening.

**Tổng: 30 tuần (~7 tháng) cho P0+P1 với đội 3 người.** Thêm 25–30% buffer → **9 tháng**.

### Sau GĐ5 (không nằm trong cam kết)

Website builder · Affiliate · Zalo/SMS marketing · báo cáo còn lại · mobile app · đa tiền tệ.

---

## 3. Khối lượng chi tiết (dev-week)

| Hạng mục | Week | Ghi chú |
|---|---:|---|
| GĐ0 setup + contract + skeleton | 6 | 3 người × 2 tuần |
| Refactor Catalog (Type/Unit/Parent/IMEI/Batch) | 5 | có migration + sửa test |
| Inventory: vector + bill + stocktake + transfer | 9 | nặng nhất |
| Sales: 18 trạng thái + type + carrier + handover | 6 | |
| POS: bill + session + offline + print | 8 | |
| Customers: level/point/debt/timeline | 3 | |
| Promotions: engine + coupon + gift + point + pricelist | 7 | |
| Shipping: adapter + handover + COD recon | 5 | 2 hãng đầu |
| Accounting: chart + journal + auto posting + debt | 9 | cần review kế toán |
| Reporting: read-model + 12 báo cáo | 8 | |
| Settings: store/staff/print/label/sequence | 4 | |
| API layer + OpenAPI + contract test | 6 | |
| Frontend P0 18 màn | 18 | |
| Frontend P1 14 màn | 16 | |
| E2E + CI + hardening | 6 | |
| Import/migrate dữ liệu (nếu cần) | 4 | dùng API v3 |
| **Tổng** | **~120 dev-week** | |

Với 3 người (2.1 dev-week hữu ích/tuần) → **~57 tuần** thuần thi công; thực tế có song song hoá
FE/BE → **30–34 tuần** là khả thi; cộng buffer → **9 tháng**.

⚠️ Nếu làm **1 người**: 14–16 tháng. Nếu **chỉ cần P0** (6 màn lõi + POS + đơn): **~10–12 tuần** với 2 người.

---

## 4. Rủi ro

| # | Rủi ro | Xác suất | Tác động | Giảm thiểu |
|---|---|---|---|---|
| 1 | **Scope creep** — Nhanh có 204 route, 90 báo cáo | Cao | Cao | Chốt cứng P0 18 màn; mọi yêu cầu mới vào backlog, không chen sprint |
| 2 | Refactor `Product`/`Stock` làm đỏ ~1.700 test | Cao | Trung bình | Làm theo nhánh + compatibility (`VariantId` → `ProductId`), sửa test theo từng bước, không gộp PR lớn |
| 3 | Multi-tenant làm muộn → phải sửa mọi query | Trung bình | Cao | **Spike 3 ngày ở GĐ0**, quyết trước GĐ1 |
| 4 | Báo cáo chậm khi có 1 triệu dòng | Cao | Trung bình | Read-model từ GĐ1 (chỉ ghi, chưa đọc), báo cáo đọc sau |
| 5 | Kế toán sai định khoản | Trung bình | **Rất cao** (sai thuế) | Kế toán review bảng định khoản; test số cụ thể; đối chiếu sổ |
| 6 | POS offline xung đột tồn/giá | Trung bình | Cao | `ClientBillId` idempotent; chấp nhận lệch + báo cáo chênh (như Nhanh) |
| 7 | Thiếu credential HĐĐT/VNPay/hãng VC | Cao | Trung bình | Làm adapter + fake provider; sandbox trước, thật sau |
| 8 | In hoá đơn/máy in tem | Trung bình | Trung bình | Template HTML + print CSS; test trên Chrome; driver máy in để P2 |
| 9 | Đội chưa quen RSC/App Router | Trung bình | Trung bình | Skeleton + pair 1 tuần; quy ước rõ server/client component |
| 10 | Clone UI vi phạm quyền | Thấp | Cao | Chỉ clone bố cục/màu/pattern; **không** logo/asset/code; review pháp lý trước release |
| 11 | Phụ thuộc tài khoản Nhanh để đối chiếu | Trung bình | Thấp | Chụp ảnh + lưu DOM/route (đã có trong `artifacts/nhanh-survey/`) |
| 12 | Đổi yêu cầu nghiệp vụ giữa đường | Cao | Cao | Mỗi module có "definition of done" viết trước; thay đổi → đổi phạm vi, không đổi deadline |

---

## 5. Sáu quyết định **phải chốt** trước khi tôi code

| # | Câu hỏi | Lựa chọn | Đề xuất của tôi |
|---|---|---|---|
| 1 | **Multi-tenant** (nhiều doanh nghiệp/1 DB) hay **single-tenant** (1 doanh nghiệp/1 deployment)? | a) multi-tenant · b) single · c) single trước, multi sau | **c)** — thiết kế `TenantId` từ đầu nhưng chưa bật UI; tránh phải sửa schema sau |
| 2 | **Giữ ABP** hay viết lại backend? | a) giữ + refactor · b) NestJS · c) khác | **a)** — xem [03 §1](03-kien-truc-dich.md#1-ba-phương-án-kiến-trúc-và-lựa-chọn) |
| 3 | **Kế toán**: tự làm bút toán kép hay **xuất dữ liệu sang MISA/Excel**? | a) full · b) export + công nợ đơn giản | **b) trước, a) sau** — tiết kiệm 8–9 dev-week và tránh rủi ro thuế |
| 4 | **POS**: chỉ online (cần mạng) hay **offline**? | a) online · b) offline | **b)** — cửa hàng thật hay rớt mạng; chi phí +2 tuần |
| 5 | **Báo cáo**: 12 báo cáo P0/P1 hay làm đủ 90? | a) 12 · b) 90 | **a)**; bổ sung theo yêu cầu |
| 6 | **Website bán hàng (storefront)** có nằm trong giai đoạn này? | a) có · b) không | **b)** — dùng lại `eCommerce.Web` hiện tại, không làm CMS |

Ngoài ra cần bạn cung cấp (không chặn code, nhưng chặn go-live):

- Tài khoản **sandbox** VNPay / Momo / payOS (nếu làm thanh toán online).
- Hợp đồng + credential **hãng vận chuyển** (GHN/GHTK/Viettel).
- Đơn vị cung cấp **HĐĐT** (Nhanh Invoice/VNPT/MISA) + chữ ký số.
- Mẫu hoá đơn, mẫu phiếu giao hàng, logo — để làm mẫu in.
- Danh mục kế toán đang dùng (hệ thống TK, kỳ kế toán).

---

## 6. Việc làm ngay trong 10 ngày (không cần chờ quyết định)

| Ngày | Việc | Ai |
|---|---|---|
| 1 | Nâng Node ≥ 24.22.1, `npm i -g rea-agents`, `rea setup --client dsh`, `rea doctor` | BE |
| 1 | Chụp lại 18 màn P0 của Nhanh (ảnh + DOM) làm baseline đối chiếu | FE |
| 2 | Bật NSwag, xuất `openapi.json`, tạo `packages/api-client` | BE |
| 2–3 | Dựng `apps/admin-web`: tokens, AppShell, login, 1 list rỗng | FE |
| 3 | Viết `docs/nhanh/decisions.md` với 6 câu ở §5 (chờ bạn duyệt) | PM |
| 4 | Migration refactor `Product`: thêm `Type/Units/ParentId/VatMode` **chưa xoá** `ProductVariant` | BE |
| 5 | API `/products` + `/products/lookup` + test hợp đồng | BE |
| 6–7 | Màn DS sản phẩm + form type Thường (nối API thật) | FE |
| 8–9 | `StockBalance` 9 cột + job suy ra từ sổ cái + test bất biến | BE |
| 10 | Demo nội bộ: SP → tồn → (giả lập POS) | Cả đội |

Kết thúc 10 ngày phải có: **một đường dọc chạy được từ DB → API → UI**, không mock.

---

## 7. Cách đo tiến độ (không đo bằng "màn hình đã xong")

| Chỉ số | Mục tiêu mỗi sprint |
|---|---|
| Số **luồng end-to-end** chạy được với API thật | +1 |
| Số test bất biến nghiệp vụ mới (tồn, tiền, trạng thái) | ≥ 5 |
| Số màn P0 đạt AC (có E2E) | +2 |
| Số trang chậm > 1.5s (đo staging với 20k SP) | 0 |
| Số lỗi P1/P2 mở | không tăng |

---

## 8. Kết luận thẳng

- **Khả thi** để có bản dùng được (P0: hàng hóa, kho, POS, đơn, khách, 2 báo cáo) trong **10–12 tuần**
  với 2 người.
- **P0 + P1 đầy đủ** (đa kênh, khuyến mại, kế toán, 12 báo cáo) trong **~9 tháng** với 3 người.
- **Ngang Nhanh** (204 route, 90 báo cáo, website builder, affiliate, bảo hành…) là **14–18 tháng**
  — và **không nên** làm, vì phần lớn không tạo doanh thu cho bạn.
- Rủi ro lớn nhất **không phải kỹ thuật** mà là **phạm vi** và **kế toán**. Hai thứ đó phải chốt bằng
  văn bản trước sprint 1.

