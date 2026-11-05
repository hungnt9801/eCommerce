# 04 — Web Admin bằng Next.js, UI theo Nhanh.vn

> Mục tiêu: một Web Admin **dày thông tin, thao tác nhanh, giống Nhanh đến mức người dùng cũ dùng được
> ngay**, nhưng code hiện đại (App Router, type-safe, test được).
> Tham chiếu bắt buộc: [01 §2–§4](01-khao-sat-nhanh-vn.md) (tokens, layout, pattern) · [03](03-kien-truc-dich.md) (API).

---

## 1. Stack

| Lớp | Chọn | Vì sao |
|---|---|---|
| Framework | **Next.js (App Router) + React 19 + TypeScript strict** | RSC giảm JS, routing lồng nhau hợp sidebar 2 cấp |
| Style | **Tailwind CSS v4** + CSS variables | Chép token Nhanh thành biến, không hard-code |
| Component | **shadcn/ui (Radix)** + bổ sung tự viết | Có accessibility, không khoá style; tự viết bảng vì shadcn không có DataTable |
| Bảng | **TanStack Table v8** (headless) | Bảng 20 cột, ẩn/hiện cột, sticky, group, virtual |
| Data | **TanStack Query v5** + fetch của server | Cache, retry, invalidate sau mutation |
| Form | **React Hook Form + Zod** | Form sản phẩm/hóa đơn rất lớn, cần validate theo schema dùng lại ở server |
| URL state | **nuqs** | Bộ lọc/tab/phân trang nằm trên URL (chia sẻ được, back/forward đúng) |
| i18n | **next-intl** | `vi` mặc định, `en` mở đường |
| Ngày/số | **dayjs** + `Intl.NumberFormat('vi-VN')` | Nhanh hiển thị `150,000`, `18/11` |
| Chart | **Recharts** | Dashboard/báo cáo |
| Editor | **Tiptap** | Mô tả sản phẩm (Nhanh dùng HugeRTE) |
| Icon | **@phosphor-icons/react** | Cùng bộ icon với Nhanh → nhìn quen ngay |
| Type API | **openapi-typescript + openapi-fetch** | Sinh từ `/swagger/v1/swagger.json`, không chép tay |
| Auth | **Auth.js v5** (OIDC với OpenIddict) | Authorization Code + PKCE, token chỉ ở server |
| Test | **Vitest + Testing Library + Playwright** | Unit cho logic bảng/form, E2E cho 6 luồng tiền |

> 📌 Pin phiên bản tại lúc khởi tạo; **không** dùng bản beta cho ba gói: `next`, `auth.js`, `tanstack/query`.

---

## 2. Cấu trúc `apps/admin-web`

```
apps/admin-web/
├─ app/
│  ├─ (auth)/login/page.tsx
│  ├─ (admin)/[business]/
│  │  ├─ layout.tsx                ← AppShell: topbar + sidebar + breadcrumb
│  │  ├─ page.tsx                  ← Dashboard tổng quan
│  │  ├─ product/item/page.tsx     ← danh sách SP
│  │  ├─ product/item/[id]/page.tsx
│  │  ├─ product/item/new/page.tsx
│  │  ├─ product/{brand,category,batch,variant}/page.tsx
│  │  ├─ inventory/{bill,transfer,check,imex,limit,damaged}/page.tsx
│  │  ├─ order/manage/page.tsx
│  │  ├─ order/manage/[id]/page.tsx
│  │  ├─ pos/bill/page.tsx         ← POS full-screen (layout riêng)
│  │  ├─ pos/bill/[id]/page.tsx
│  │  ├─ customer/{list,card,level,group}/page.tsx
│  │  ├─ promotion/{discount,coupon,gift,point,pricelist}/page.tsx
│  │  ├─ report/[...slug]/page.tsx ← 12 báo cáo qua registry
│  │  ├─ accounting/{journal,cash,debt}/page.tsx
│  │  └─ setting/{store,sale,order,payment,print,label}/page.tsx
│  ├─ (print)/print/{bill,order,stocktake}/[id]/page.tsx  ← chỉ CSS in, không JS
│  └─ api/
│     ├─ auth/[...nextauth]/route.ts
│     └─ proxy/[...path]/route.ts   ← BFF: gắn token, forward
├─ components/
│  ├─ shell/{Sidebar,Topbar,BusinessSwitcher,CommandPalette}.tsx
│  ├─ data/{DataTable,FilterBar,PageTabs,BulkActionBar,ColumnChooser,Pagination}.tsx
│  ├─ form/{MoneyInput,QuantityInput,BarcodeInput,DateRange,SelectAsync}.tsx
│  ├─ domain/{ProductPicker,CustomerPicker,StockBadge,OrderStatusBadge,PaymentSplit}.tsx
│  └─ ui/…                          ← shadcn
├─ lib/{api,auth,permissions,format,query-keys,sse}.ts
├─ styles/tokens.css                ← design tokens Nhanh
├─ messages/{vi,en}.json
└─ e2e/
```

### 2.1 Vì sao có `api/proxy` (BFF)

Trình duyệt **không bao giờ** giữ access token. Luồng:

```
Browser ──cookie httpOnly (session)──► Next server (RSC / route handler)
                                          │ Bearer <access token> (server memory/cookie mã hoá)
                                          ▼
                                    eCommerce.HttpApi.Host
```

Lợi ích: không lộ token, không cần CORS, đổi API host không ảnh hưởng client, dễ thêm rate-limit/log.

---

## 3. Design system (chép từ Nhanh, không phải "lấy cảm hứng")

`styles/tokens.css`:

```css
:root{
  --primary:#0C83FF; --primary-hover:#0B76E6; --primary-active:#0A6FD9;
  --success:#059669; --success-hover:#05875F; --success-active:#048059;
  --secondary:#247297; --info:#049AAD; --warning:#F58646; --danger:#EF4444;
  --bg-app:#E2E6ED; --bg-surface:#FFFFFF; --bg-subtle:#F3F4F6;
  --text:#1F2937; --text-muted:#6B7280; --border:#DEE2E6;
  --sidebar-bg:#F3F4F6; --sidebar-text:#252B36; --topbar-bg:#252B36; --topbar-text:#FFFFFF;
  --radius:8px; --radius-sm:6px;
  --table-cell-py:12px; --table-cell-px:20px;
  --font:'Inter',system-ui,sans-serif;
}
```

### 3.1 Ba luật thị giác (nếu sai thì "không giống Nhanh")

1. **Nền xám, nội dung trắng**: app background `#E2E6ED`, card/bảng `#FFFFFF`, viền `#DEE2E6` 1px.
2. **Nút chính xanh lá `#059669`**, nút phụ viền xám, link/ID xanh dương `#0C83FF`.
3. **Mật độ cao**: hàng bảng cao **48–56px**, chữ 13–14px, padding ô 12/20px — *không* dùng layout
   "thoáng kiểu SaaS Mỹ" (row 72px).

### 3.2 Typography & số

| Thành phần | Style |
|---|---|
| Tiêu đề trang | 18–20px / 600 |
| Tab | 14px / 500, active gạch chân 2px `--primary`, badge số nền `--primary` chữ trắng |
| Nhãn cột bảng | 13px / 600, màu `--text` (không xám nhạt) |
| Ô bảng | 14px / 400; ID link xanh; tiền **căn phải**, `tabular-nums` |
| Chênh lệch âm | đỏ `--danger`; dương xanh `--success` |

### 3.3 Bản đồ component Nhanh → của ta

| Nhanh | Của ta | Ghi chú |
|---|---|---|
| Tab có số đếm | `PageTabs` (`items:{label,count,value}`) | count=0 vẫn hiện, màu xám |
| Bộ lọc pill | `FilterBar` (grid 4–7 cột) + `FilterPanel` (nâng cao) | `Lưu bộ lọc` → dropdown chọn/tạo |
| Nút `Thao tác ▾` | `BulkActionBar` (sticky đáy hoặc cạnh nút) | chỉ hiện khi `selectedRows.length > 0` |
| Ẩn/hiện cột | `ColumnChooser` + lưu `localStorage` theo user+screen | |
| Bảng nhóm đơn | `DataTable` chế độ `grouped` (header nhóm + dòng con) | dùng cho đơn hàng, không dùng cho SP |
| Chi tiết 2 cột | `DetailLayout` (main 2/3 + rail 1/3, card) | |
| POS đa tab | `PosTabs` (state giữ trong IndexedDB, không mất khi F5) | |
| Mẫu in | `PrintPreview` (iframe) + `PrintTemplate` HTML | in từ server route |

---

## 4. Khung nhìn & điều hướng

### 4.1 Sidebar 2 cấp (đúng 180 mục của Nhanh, chia 9 nhóm)

```
TỔNG QUAN        Dashboard
HÀNG HÓA         Sản phẩm · Thương hiệu · Danh mục · Thuộc tính · Lô · Tồn kho
KHO              Xuất nhập · Chuyển kho · Kiểm kho · Phiếu nháp · Hạn mức · Hàng lỗi · Vị trí
BÁN HÀNG (POS)   Bán lẻ · Bán sỉ · Trả hàng · Hóa đơn nháp · Kết ca
ĐƠN HÀNG         Danh sách · Đóng gói · Chờ gửi VC · Khiếu nại · Đối soát · Đơn trùng
KHÁCH HÀNG       Danh sách · Thẻ · Cấp độ · Nhóm · Chăm sóc
KHUYẾN MẠI       Chiết khấu · Mã giảm giá · Quà tặng · Tích điểm · Bảng giá · Hoa hồng
KẾ TOÁN          Bút toán · Thu chi · Công nợ · Kỳ kế toán
BÁO CÁO          Doanh thu · Đơn hàng · Bán lẻ · Tồn kho · Sản phẩm · Khách hàng
KÊNH SÀN         Shopee · TikTok · Lazada · Tiki · Đối soát COD
CÀI ĐẶT          Cửa hàng · Nhân viên · Mẫu in · Thanh toán · Vận chuyển · HĐĐT
```

Hành vi: thu gọn còn **64px** (chỉ icon, tooltip); nhóm nhớ trạng thái mở; mục đang chọn có nền
`#E8F1FF` + vạch trái 3px `--primary`; **menu lọc theo quyền** (không hiện mục không có quyền,
không hiện nhóm rỗng).

### 4.2 Route: giữ quen thuộc, thêm tiền tố doanh nghiệp

| Cách | URL | Ghi chú |
|---|---|---|
| Nhanh | `/product/item/index?businessId=230222` | query param |
| **Ta** | `/{business}/product/item` | path param, cache tốt hơn, SSR sạch |
| Tương thích | `middleware.ts` đọc `?businessId=` → **redirect 308** sang path | link cũ của người dùng vẫn chạy |

Segment giữ **đúng tên Nhanh** (`product/item`, `inventory/bill`, `order/manage`, `pos/bill`) để
tài liệu, ảnh chụp, video hướng dẫn của Nhanh **dùng lại được** khi đào tạo nhân viên.

### 4.3 Topbar

Trái: logo + **BusinessSwitcher** (tên doanh nghiệp, danh sách nếu user thuộc nhiều).
Giữa: ô tìm kiếm toàn cục (`Cmd/Ctrl+K` → `CommandPalette`: SP, đơn, KH, chức năng).
Phải: thông báo (badge), trợ giúp, avatar + menu (hồ sơ, đổi mật khẩu, đăng xuất).

---

## 5. Danh mục màn hình P0 (18) — hợp đồng thi công

Cột "API" là endpoint ở [03 §5.2](03-kien-truc-dich.md#52-bề-mặt-endpoint-p0-rút-gọn). "AC" = tiêu chí nghiệm thu.

| # | Màn hình | Route | Thành phần chính | API | AC (đo được) |
|---|---|---|---|---|---|
| 1 | Đăng nhập | `/login` | form + SSO-ready | OpenIddict | sai mật khẩu → lỗi rõ; đăng nhập xong về Dashboard |
| 2 | App shell | `/{business}/layout` | Sidebar, Topbar, CommandPalette | `/me` | 180 mục lọc theo quyền; thu gọn nhớ trạng thái |
| 3 | Dashboard | `/{business}` | 4 KPI + chart 7/30 ngày + top 5 SP + cảnh báo tồn | `/reports/*` | số khớp báo cáo doanh thu cùng kỳ |
| 4 | DS sản phẩm | `product/item` | PageTabs, FilterBar, DataTable 20 cột, ColumnChooser | `/products` | 20.000 SP: tải trang < 1s; lọc 5 điều kiện giữ trên URL |
| 5 | Form sản phẩm | `product/item/new` · `product/item/[id]/edit` | 9 section theo `Type`, unit repeater, IMEI list, ảnh | `POST/PATCH /products` | đổi Type → section đổi đúng; lưu thiếu field → lỗi tại field |
| 6 | Chi tiết sản phẩm | `product/item/[id]` | 5 tab: Thông tin · Tồn theo kho · Lịch sử giá · IMEI/Lô · Lịch sử sửa | `/products/{id}`, `/stock/balances` | tồn 9 cột khớp sổ cái |
| 7 | DS phiếu kho | `inventory/bill` | tab Nhập/Xuất, DataTable, bulk | `/stock/bills` | lọc theo mode/ngày/kho |
| 8 | Tạo phiếu kho | `inventory/bill/new` | chọn mode → form động, ProductPicker, tiền VAT | `POST /stock/bills` | tổng tiền = Σ(dòng) đúng VAT; Post mới đổi tồn |
| 9 | Kiểm kho | `inventory/check` | bảng đếm, cột lệch, gộp phiếu | `/stocktakes` | lệch = thực tế − sổ; Post sinh bút toán gain/loss |
| 10 | DS đơn hàng | `order/manage` | 14 tab có badge, grouped rows, bulk 16 hành động | `/orders` | tab "Cần xử lý" khớp định nghĩa; đổi trạng thái hàng loạt |
| 11 | Chi tiết đơn | `order/manage/[id]` | tab **động theo trạng thái** (4/5 tab) | `/orders/{id}` | đơn tự vận chuyển ẩn tab Lịch trình/Thanh toán |
| 12 | POS bán lẻ | `pos/bill` | full-screen, đa tab, F1–F10, quét mã, thanh toán đa phương thức | `/pos/bills` | quét 2 lần → SL=2; F9 lưu; in F10; offline vẫn lưu |
| 13 | DS hóa đơn bán lẻ | `pos/bill` (list) | DataTable + cột icon thanh toán + 2 loại chiết khấu | `/pos/bills` | hiện đúng icon tiền mặt/chuyển khoản/quẹt thẻ/điểm |
| 14 | Trả hàng | `pos/return` | chọn HĐ gốc, dòng trả, phí, IMEI | `/pos/returns` | trả xong tồn tăng, công nợ/điểm giảm |
| 15 | DS khách hàng | `customer/list` | 5 tab phân khúc tự động | `/customers` | tab "Lâu chưa mua" đúng công thức 60 ngày |
| 16 | Chi tiết khách | `customer/[id]` | 5 tab: Thông tin · Mua hàng · Chăm sóc · Bảo hành · Sửa | `/customers/{id}/*` | tiền tích luỹ đúng công thức |
| 17 | Nhân viên & quyền | `setting/staff` | bảng + ma trận quyền + phạm vi dữ liệu | `/staff`, `/permissions` | bật "chỉ xem dữ liệu của mình" → list lọc đúng |
| 18 | Mẫu in | `setting/print` | danh sách mẫu + editor + preview + version | `/print-templates` | sửa mẫu → in ra đúng; khôi phục bản cũ |

**P1 (14):** kênh sàn (ghép SP, tải đơn), đối soát COD, biên bản bàn giao, chuyển kho có duyệt,
hàng lỗi, lô/hạn dùng, chiết khấu, coupon, tích điểm/cấp độ, bảng giá, 4 báo cáo, công nợ, thu chi, HĐĐT.
**P2:** vị trí kho, hạn mức, phiếu nháp, bút toán tay, affiliate, website, bảo hành, Zalo/SMS.

---

## 6. Đặc tả 4 màn khó nhất

### 6.1 Danh sách sản phẩm (bảng 20 cột)

```
┌ PageTabs: Sản phẩm | Lịch sử sửa xoá ──────────────────────────────┐
├ FilterBar: [Cửa hàng▾][ID][Tên/mã/mã vạch][Danh mục▾][Tồn▾][Lọc▾] │
├ Toolbar: [+ Thêm mới ▾][Thao tác ▾]           1–30/158  [◀◀◀▶▶] [⚙]│
├ DataTable (sticky header, sticky cột đầu, row hover #F8FAFC)        │
│  ☐ ID↕ Ảnh Mã vạch↕ Mã↕ Tên↕ ĐVT Giá nhập Giá vốn Giá bán Tồn…     │
│  ☐ 37496290 ★ [img] …                                              │
└────────────────────────────────────────────────────────────────────┘
```

Quy tắc:
- **Cột "Tồn"** hiển thị 3 số nhỏ (Tồn / Tạm giữ / Có thể bán) bằng `StockBadge`, âm → đỏ.
- **Ô tiền** căn phải, `150,000`; hover hiện `+VAT`.
- **Sắp xếp** server-side (`sort=-updatedAt`), không sort client.
- **Chọn dòng** → `BulkActionBar` hiện; `Xoá` phải gõ tên để xác nhận.
- **`Lưu bộ lọc`** → `POST /saved-filters` (P1) hoặc localStorage (P0).

### 6.2 Form sản phẩm (9 loại)

Bố cục 2 cột: trái = thông tin chung + giá/ĐVT; phải = trạng thái, danh mục, ảnh, SEO, tồn ban đầu.
Section **bật/tắt theo `Type`**:

| Type | Section bắt buộc |
|---|---|
| 4 IMEI | danh sách serial (nhập tay / import), trạng thái từng serial |
| 8 Theo lô | bảng lô (mã, NSX, HSD, SL) |
| 3 Cân đo | ĐVT gốc là kg/g, cho phép SL thập phân (3 chữ số) |
| 5 Gói / 9 Combo | bảng thành phần (SP + SL), hiển thị tồn khả dụng = min |
| 10 Nhiều ĐVT | repeater ĐVT: tên, hệ số, mã vạch, giá riêng |
| 6 Dịch vụ | ẩn tồn/kho, không cho nhập kho |

Validate dùng **cùng Zod schema** ở client và server (chia sẻ qua `packages/contracts` nếu cần).

### 6.3 POS bán lẻ (màn quan trọng nhất về doanh thu)

```
┌ Tabs hóa đơn: [HĐ 1 ×][HĐ 2 ×][+] ────────────────  Thu ngân: Ngọc ─┐
│ ┌ Tìm SP / quét mã vạch (F3) ─────────────────────┐ ┌ Khách hàng ──┐│
│ │ [ ................................ ]  SL [ ]    │ │ [SĐT/tên F4] ││
│ ├────────────────────────────────────────────────┤ │ Cấp độ/điểm  ││
│ │ SP            ĐVT   SL   Giá    CK    Tổng      │ ├──────────────┤│
│ │ Bánh Cosy      1     1  50.000      50.000      │ │ Tổng tiền    ││
│ │  + Quà tặng …                                  │ │ Chiết khấu F6││
│ ├────────────────────────────────────────────────┤ │ VAT          ││
│ │ [Thêm quà][BHMR][Ghi chú]                       │ │ ─────────────││
│ └────────────────────────────────────────────────┘ │ Tiền mặt  F8 ││
│                                                     │ Chuyển khoản ││
│                                                     │ Quẹt thẻ     ││
│                                                     │ Điểm         ││
│                                                     │ [F9 Lưu][F10]││
└─────────────────────────────────────────────────────┴──────────────┘
```

Bắt buộc:
- **Đa tab** (F1 mở tab mới), state lưu **IndexedDB** → F5 không mất.
- **Quét mã**: gõ/scan → tìm theo `barcode` (ưu tiên) → thêm dòng/SL+1; 3 chế độ như Nhanh.
- **Phím tắt** đúng Nhanh: F1/F3/F4/F6/F8/F9/F10.
- **Luật chiết khấu**: chỉ 1 trong 2 (dòng **hoặc** bill) — chặn ở UI, backend cũng chặn.
- **Thanh toán chia nhiều phương thức**; thiếu tiền → ghi công nợ (nếu được phép) + hẹn ngày.
- **In** từ mẫu local, kèm mã QR chuyển khoản.

### 6.4 Chi tiết đơn hàng (tab động)

```
Header: #457828063  [Đang chuyển]  Kho Tổng · 09:56 18/11 · NV: Đặng Sơn
Tabs:   Thông tin | Lịch trình | In & đóng gói | Thanh toán | Khiếu nại
Layout: main (KH, SP, tiền, ghi chú) + rail (trạng thái, vận đơn, lịch sử)
```

Tab hiển thị theo nhóm trạng thái (bảng [01 §4.3](01-khao-sat-nhanh-vn.md)); nút hành động ẩn/hiện
theo bảng "thao tác × trạng thái" của Nhanh — **không** hiện nút rồi báo lỗi.

---

## 7. Tầng dữ liệu

```ts
// lib/api/client.ts
import createClient from 'openapi-fetch';
import type { paths } from './schema';           // sinh từ swagger
export const api = createClient<paths>({ baseUrl: '/api/proxy' });
```

| Việc | Cách làm |
|---|---|
| Đọc danh sách | **Server Component** gọi `api.GET` với cookie → render HTML; client nhận `initialData` |
| Tương tác (lọc/sort/page) | `nuqs` đổi URL → RSC re-render (không cần TanStack Query cho list) |
| Mutation | **Server Action** → gọi API → `revalidatePath`/`router.refresh()` |
| Việc cần optimistic | POS (thêm dòng), gắn nhãn, đổi trạng thái đơn |
| Cache | `next: { revalidate: 30 }` cho reference data; **không cache** tồn kho |
| Lỗi | map `problem+json` → `toast` + lỗi field nếu `details[].field` |
| Quyền | server: `requirePermission('products.create')`; client: `<Can permission="...">` |

---

## 8. Xác thực & phân quyền

```
Browser → /login → Auth.js (OIDC, PKCE) → OpenIddict /connect/authorize
        → callback: đổi code → token (access+refresh) → **lưu server-side**
        → session cookie httpOnly, SameSite=Lax, Secure
Mỗi request RSC → đọc session → Bearer access token → API
Access token hết hạn → refresh trong route handler (single-flight lock) → retry 1 lần
```

- **Không** lưu token trong `localStorage`/`sessionStorage` (XSS là đủ mất).
- Claim mang: `sub`, `tenant_id`/`businessId`, `permissions[]`, `stores[]`, `data_scope`.
- Middleware chặn `/{business}` nếu user không thuộc business → 404 (không lộ tồn tại).
- UI ẩn nút theo quyền, **nhưng server vẫn kiểm** — UI chỉ là trải nghiệm.

---

## 9. Định dạng Việt Nam (dễ sai nhất)

| Loại | Quy tắc | Ví dụ |
|---|---|---|
| Tiền | `Intl.NumberFormat('vi-VN')`, không ký hiệu trong bảng, có `₫` ở tổng | `150.000` (Nhanh dùng dấu phẩy `150,000` — **chọn dấu phẩy để giống**) |
| Nhập tiền | `MoneyInput` hiển thị `66.000`, giá trị thô `66000` | không cho nhập chữ |
| Số lượng cân đo | thập phân 3 chữ số, dấu `,` | `1,250 kg` |
| Ngày | `dd/MM/YYYY`, giờ `HH:mm` | `18/11/2026 09:56` |
| Điện thoại | chuẩn hoá 10–11 số, bắt đầu `0` | `0943396829` |
| Địa chỉ | 3 cấp (Tỉnh/Huyện/Xã) + địa chỉ mới 2 cấp | dùng module `Address` sẵn có |

---

## 10. Hiệu năng & chất lượng

| Chỉ tiêu | Ngưỡng | Cách đạt |
|---|---|---|
| LCP trang danh sách | < 1.5s (20k SP) | RSC + phân trang server + skeleton |
| Tương tác lọc | < 300ms | URL state + prefetch, không fetch lại toàn bộ |
| Bundle trang danh sách | < 200KB gzip | bảng headless, không import UI kit nặng |
| Bảng 1000 dòng | 60fps | virtualization (`@tanstack/react-virtual`) |
| POS mở tab mới | < 100ms | state local, không gọi API |
| In hoá đơn | < 800ms | route in riêng, không JS |

Chất lượng: ESLint + `tsc --noEmit` + Vitest (logic bảng/form) + Playwright 6 luồng:
đăng nhập · tạo SP · nhập kho · bán lẻ · đổi trạng thái đơn · xuất báo cáo.

---

## 11. "Giống Nhanh" đến đâu là đủ (và giới hạn pháp lý)

| Nên giống | Không nên/không được |
|---|---|
| Bố cục, mật độ, màu, font, icon set | Copy logo, tên "Nhanh", asset hình ảnh của họ |
| Route `/module/entity/action` | Copy HTML/CSS/JS của họ |
| Nhãn tiếng Việt, cách đặt tên nghiệp vụ | Dịch ngược mã nguồn của họ |
| Pattern tab/bulk/print | Bê nguyên ảnh chụp vào sản phẩm |

Cách kiểm "giống": dùng **REA `compare_web_screenshots`** so ảnh chụp màn hình của ta với ảnh Nhanh
cùng màn hình, đo % lệch bố cục — mục tiêu **cấu trúc khớp, chi tiết thương hiệu khác**.

---

## 12. Thứ tự thi công Frontend (walking skeleton trước)

1. **Skeleton**: Next.js + tokens + AppShell + login + 1 trang danh sách giả → chạy được, deploy được.
2. **Sản phẩm**: list → detail → form (chỉ type Thường) → sau đó mở 9 type.
3. **Kho**: DS phiếu → tạo phiếu nhập → Post → tồn đổi.
4. **POS**: bán 1 SP → thanh toán tiền mặt → in → kết ca.
5. **Đơn hàng**: list + chi tiết + đổi trạng thái.
6. **Dashboard** + 2 báo cáo.
7. Các P1 còn lại.

Mỗi bước phải **chạy end-to-end với API thật**, không mock quá 1 ngày.

