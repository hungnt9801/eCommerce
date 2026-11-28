# UI-41 — Kiểm kê giao diện Web Admin (nền cho chuỗi `ui-1`)

- **Mã:** `UI-41` · **Trạng thái:** `Draft`
- **Ngày:** 28/11/2026 · **Người soạn:** vai `ux` (phiên N120, nhánh `feat/ui-wp41-nen`)
- **Nguồn:** `D-327` (hướng thẩm mỹ + chuỗi `ui-1`), hàng `U41-1` (`docs/agents/bang-phien-ui-1.md` dòng 78,
  `agents/state/phien-ui-1.json`), số đo Nhanh `docs/nhanh/01-khao-sat-nhanh-vn.md` §2, stack/định dạng
  `docs/nhanh/04-web-admin-nextjs.md` §1–§3 §9.
- **Phạm vi dùng:** bằng chứng nền cho `arch` viết `docs/ui/01-kien-truc-ui.md` và cho `WP-42…WP-51`.
- ⚠️ **Chưa có BR duyệt bởi người; dùng cho U1.** Tệp này **không** phải đặc tả dựng component — đặc tả dựng là
  `02-token.md` … `10-pos.md` của các WP sau.

Cách đọc: mọi `file:dòng` dưới đây đã được `Read`/`Grep` trên cây làm việc ngày 28/11/2026 (nhánh
`feat/ui-wp41-nen`, đỉnh `99e922e`). Cột ghi **"chưa kiểm"** = chưa có bằng chứng, **không** được coi là "không có".

---

## §1. Mục tiêu, phạm vi & tiêu chí chấp nhận

### 1.1 Mục tiêu

1. Đếm **thật** route, component, pattern lặp của `apps/admin-web` (không chép số từ kế hoạch).
2. Chỉ ra **lệch số đo Nhanh** đọc được trong code (px/rem/hex) kèm số đo Nhanh + nguồn.
3. Liệt kê **nợ a11y thấy được bằng đọc code** (không suy đoán cái phải chạy axe).
4. Chốt **danh sách ảnh dự kiến đổi** của WP-41 = **rỗng** (QA dùng ở `U41-3`).

### 1.2 Trong phạm vi

- `apps/admin-web/app/**` (route, layout, loading) · `apps/admin-web/components/**` · `apps/admin-web/app/globals.css`.
- 0 thay đổi giao diện, 0 thay đổi code. WP-41 chỉ **đo và ghi**.

### 1.3 Ngoài phạm vi (WP-42 trở đi)

Token/theme tối/mật độ compact (`WP-42`), i18n (`WP-43`), primitive (`WP-44`), bảng–lọc–phân trang (`WP-45`),
form–trạng thái (`WP-46`), shell + thí điểm màn kho (`WP-47`), chuyển màn đợt 1/2 (`WP-48`/`WP-49`), POS (`WP-50`),
quản trị/ESLint mức lỗi (`WP-51`). Kiến trúc 4 tầng + gói `packages/ui`: `arch`, `docs/ui/01-kien-truc-ui.md`.

### 1.4 Tiêu chí chấp nhận (BR)

- **BR-01** Mọi route người dùng thấy có một dòng ở §2 với **URL** + **`file:dòng` của `page.tsx`**; tổng số dòng
  route = 28 và khớp danh sách `app/**/page.tsx` trên đĩa. Route handler (`app/api/**`) liệt kê riêng, có `file:dòng`.
- **BR-02** Mọi component export trong `apps/admin-web/components/**` có một dòng ở §3 với `file:dòng` **và** ≥ 1
  route dùng nó; tổng = 32. Component cục bộ (khai báo trong tệp khác) liệt kê ở §3.2, mỗi cái có `file:dòng`.
- **BR-03** Mọi nhóm pattern ở §4 có **≥ 2 nơi** (`file:dòng` từng nơi), **khác biệt đo được**, **tầng đích**
  (primitive / pattern / domain) và **WP dự kiến**.
- **BR-04** Mỗi dòng §5 có đủ 5 cột: hạng mục, **số đo Nhanh + nguồn** (`01` §x hoặc tệp evidence), **số đo/giá trị
  hiện tại + `file:dòng`**, **lệch**, **WP xử lý**. Không có dòng nào chỉ ghi "chưa giống".
- **BR-05** Mỗi dòng §6 có `file:dòng`, **ảnh hưởng**, **mức P1/P2/P3**; chỉ nêu cái đọc code thấy được.
- **BR-06** §7 = **rỗng** (0 route/story đổi ảnh ở WP-41). QA: mọi khác biệt ảnh ở `U41-3` so với hiện tại = **đỏ**.
- **BR-07** §2.4 ghi rõ **số thật vs số kế hoạch** (37 route / 47 component) và **vì sao lệch**; không sửa số kế
  hoạch, chỉ nêu chênh.
- **BR-08** **Phụ lục B** liệt kê **mọi selector E2E/ảnh đang phụ thuộc** (`data-pos-*`, class `.nh-card`, `div.p-4`,
  `tbody tr`, `role` + tên) — WP sau **không** được bỏ; nếu buộc bỏ thì dừng, báo ORCH (X4, `D5`).
- **BR-09** Không tệp code/test/lockfile nào đổi trong WP-41 ngoài `docs/ui/**` (kiểm bằng `git status`), **trừ**
  `tools/probe-*.ps1` theo `D-328` ⑤ (công cụ cổng — vùng ghi `qa`, kê riêng trong handover; đã có ở `99e922e`
  của N120). `git status` chỉ dùng cho cây làm việc; DoD đo bằng diff nhánh `tich-hop/ui` với ngoại lệ trên.

---

## §2. Kiểm kê route

### 2.1 Số đếm thật

| Loại | Số | Ghi chú |
|---|---|---|
| Route **trang** (`page.tsx`) | **28** | 26 dưới `app/[business]/**` + `app/login/page.tsx` + `app/page.tsx` (redirect) |
| Route **trang có giao diện chụp được** | **27** | trừ `app/page.tsx` (chỉ `redirect`, không render) |
| Route handler API (`route.ts`) | **3** | `api/auth/[...nextauth]`, `api/health`, `api/proxy/[...path]` |
| Layout | **2** | root + `[business]` (AppShell) |
| `loading.tsx` (ranh giới tải) | **6** | notifications ×4, promotion ×2 |
| **Tổng node route của Next** | **39** | 28 + 3 + 2 + 6 |

### 2.2 Bảng route trang

| # | Route (URL) | Tệp `page` | Mục đích | Component chính | `data-testid` phải giữ | Ghi chú |
|---|---|---|---|---|---|---|
| 1 | `/` | `app/page.tsx:4` | Chuyển hướng về `/{business}` mặc định | — | — | Không render UI; E2E không chụp |
| 2 | `/login` | `app/login/page.tsx:36` | Đăng nhập (credentials → OpenIddict) | form thuần + `nh-card`/`nh-input`/`nh-btn` | không có; E2E dùng `input[name=username/password]`, `role=button "Đăng nhập"` (`e2e/support/ui.ts:16-18`) | `?error=missing\|invalid`, `?next=` |
| 3 | `/{business}` | `app/[business]/page.tsx:17` | Dashboard 4 KPI + biểu đồ 7 ngày | `StackedBarChart` | không có; E2E `heading "Tổng quan"` (`01-dang-nhap.spec.ts:28`) | Ghi "chưa tổng hợp" thay `0` |
| 4 | `/{business}/product/item` | `app/[business]/product/item/page.tsx:38` | DS sản phẩm + tab trạng thái + lọc `q` | bảng thô trong trang | không có; E2E `tbody tr` lọc theo text (`02-tao-san-pham.spec.ts:50`) | 8 cột (Nhanh 20), `D-…` mở rộng ở WP sau |
| 5 | `/{business}/product/inventory` | `app/[business]/product/inventory/page.tsx:23` | Tồn kho 9 chiều theo cặp kho–SKU | `BalancesTable` | không có | Lọc `sku`, `warehouseId`, `page` |
| 6 | `/{business}/order/manage` | `app/[business]/order/manage/page.tsx:49` | DS đơn, 14 tab badge, nhóm theo ngày | `OrderStatusBadge` | không có | Lọc `tab,q,type,source,warehouseId,from,to,page` |
| 7 | `/{business}/order/manage/[id]` | `app/[business]/order/manage/[id]/page.tsx:27` | Chi tiết đơn, tab động `info\|lines\|history` | `OrderActions`, `OrderStatusBadge` | không có | `?tab=`; 2 cột 1fr/320px |
| 8 | `/{business}/inventory/bill` | `app/[business]/inventory/bill/page.tsx:46` | DS phiếu kho, tab trạng thái, xem cột | `BillTable`, `SavedFilters` | không có | Lọc `direction,mode,status,warehouseId,from,to,page` |
| 9 | `/{business}/inventory/bill/new` | `app/[business]/inventory/bill/new/page.tsx:17` | Tạo phiếu kho | `BillForm` (→`ProductPicker`) | không có; E2E `heading "Tạo phiếu kho"`, `button "Lưu phiếu (Nháp)"` (`03-nhap-kho.spec.ts:32,48`) | `?kind=` chọn kiểu phiếu |
| 10 | `/{business}/inventory/bill/[id]` | `app/[business]/inventory/bill/[id]/page.tsx:27` | Chi tiết phiếu + tồn hiện tại | `Badges`, `BillActions`, `BalancesTable` | không có; E2E `button "Ghi sổ phiếu"` (`03-nhap-kho.spec.ts:52`) | 20 SKU trần truy vấn tồn |
| 11 | `/{business}/inventory/bill/print` | `app/[business]/inventory/bill/print/page.tsx:33` | In 1…20 phiếu kho (`?ids=a,b`) | `PrintButton` | không có | Route **in**, `print:hidden` cho nút |
| 12 | `/{business}/pos/bill` | `app/[business]/pos/bill/page.tsx:18` | POS bán lẻ (màn doanh thu) | `PosTerminal` (1134 dòng) | `data-pos-dong`, `data-pos-tab`, `data-pos-tab-active`, `data-pos-dong-bo`, `data-pos-tab-moi` | `?warehouseId=`; F1–F10 |
| 13 | `/{business}/pos/bill/list` | `app/[business]/pos/bill/list/page.tsx:27` | DS hoá đơn bán lẻ + huỷ + in | `VoidBillButton` | không có | Lọc `q,warehouseId,status,page` |
| 14 | `/{business}/pos/return` | `app/[business]/pos/return/page.tsx:8` | Trả hàng từ hoá đơn | `ReturnForm` | không có | P0 chỉ trả từ HĐ (`D-42`) |
| 15 | `/{business}/print/bill/[id]` | `app/[business]/print/bill/[id]/page.tsx:15` | In hoá đơn bán lẻ khổ hẹp (380px) | `PrintButton` | E2E `div.p-4` + text `"HOÁ ĐƠN BÁN LẺ"` (`05-in.spec.ts:32`) | Mở tab mới, `print:hidden` |
| 16 | `/{business}/customer/list` | `app/[business]/customer/list/page.tsx:32` | DS khách, 5 tab phân khúc | bảng thô + nút tab | không có | Lọc `q,tab,type,levelId,conNo,page` |
| 17 | `/{business}/customer/[id]` | `app/[business]/customer/[id]/page.tsx:33` | Chi tiết khách 5 tab | `SegmentForm`, `CareForm` | không có | `?tab=tong-quan\|don-hang\|hoa-don\|cong-no\|cham-soc` |
| 18 | `/{business}/settings/staff` | `app/[business]/settings/staff/page.tsx:27` | Nhân viên + phạm vi dữ liệu + ma trận quyền | `StaffForm`, `ScopeSelect`, `PermissionMatrix` | không có | 3 khối dọc |
| 19 | `/{business}/report/revenue` | `app/[business]/report/revenue/page.tsx:30` | Báo cáo doanh thu + CSV + chart | `ExportCsvButton`, `StackedBarChart` | không có | Đọc read-model; `daTongHop=false` ⇒ "chưa tổng hợp" |
| 20 | `/{business}/report/inventory` | `app/[business]/report/inventory/page.tsx:19` | Báo cáo tồn (ảnh chụp mới nhất) + CSV | `ExportCsvButton` | không có | `?ngay=` |
| 21 | `/{business}/promotion` | `app/[business]/promotion/page.tsx:27` | DS khuyến mãi (PRO-002) | bảng thô + badge trạng thái | không có | Lọc `status,kind,page`; ẩn nút tạo khi thiếu quyền |
| 22 | `/{business}/promotion/new` | `app/[business]/promotion/new/page.tsx:10` | Tạo khuyến mãi | `PromotionForm` | không có | Ẩn form khi thiếu `Promotions.Manage` |
| 23 | `/{business}/promotion/[id]` | `app/[business]/promotion/[id]/page.tsx:22` | Chi tiết + dựng cây điều kiện | `TreeBuilder` | không có | `D-288`: thiếu `Limits` ⇒ chặn sửa |
| 24 | `/{business}/notifications/templates` | `app/[business]/notifications/templates/page.tsx:22` | DS mẫu thông báo (lọc client) | `TemplateTable` | không có | `loading.tsx` bên cạnh |
| 25 | `/{business}/notifications/templates/new` | `app/[business]/notifications/templates/new/page.tsx:10` | Tạo mẫu | `TemplateForm` | không có | Chỉ `Text` (`D-199`) |
| 26 | `/{business}/notifications/templates/[id]` | `app/[business]/notifications/templates/[id]/page.tsx:20` | Soạn/publish version | `TemplateVersionEditor` | không có | Chưa có `GET /templates/{id}` ⇒ tải list rồi tìm |
| 27 | `/{business}/notifications/queue` | `app/[business]/notifications/queue/page.tsx:25` | Hàng đợi + timeline | `QueueTable` | không có | `D-191` che PII |
| 28 | `/{business}/notifications/batches` | `app/[business]/notifications/batches/page.tsx:23` | Tạo lô + duyệt lô | `BatchForm`, `BatchApprove` | không có | `?batchId=` |

### 2.3 Route handler, layout, ranh giới tải

| Loại | Đường dẫn | Tệp | Ghi chú |
|---|---|---|---|
| Handler | `/api/auth/[...nextauth]` | `app/api/auth/[...nextauth]/route.ts` | Auth.js; token server-side (`A1`) |
| Handler | `/api/health` | `app/api/health/route.ts` | Health check |
| Handler | `/api/proxy/[...path]` | `app/api/proxy/[...path]/route.ts` | BFF gắn Bearer; **không** phải route UI |
| Layout | toàn app | `app/layout.tsx:15` | `<html lang="vi">`, metadata từ `NEXT_PUBLIC_APP_NAME` |
| Layout | `/{business}` | `app/[business]/layout.tsx:11` | AppShell: `Sidebar` + `Topbar` + `<main class="p-5">`; guard session ở server |
| Loading | notifications (4) + promotion (2) | `app/[business]/notifications/*/loading.tsx`, `app/[business]/promotion/**/loading.tsx` | 6 skeleton gần giống nhau |

### 2.4 Chênh với kế hoạch (BR-07)

| Hạng mục | Kế hoạch ghi (`bang-phien-ui-1.md:60,78`) | Đếm thật | Vì sao lệch |
|---|---|---|---|
| Route | 37 | **28** trang (+3 API +2 layout +6 loading = 39 node) | Kế hoạch đếm theo "route/màn của Nhanh cần chuyển" (gồm route chưa dựng), không theo cây `app/`. Thực tế chưa có `product/brand`, `inventory/transfer\|check`, `accounting/*`, `report/[...slug]` 12 báo cáo, `setting/*`… như `04 §2` mô tả. **Không** sửa số kế hoạch; QA chụp ảnh chuẩn theo số thật. |
| Component | 47 | **32** export (+7 component cục bộ) | Kế hoạch có thể tính cả component dự kiến dựng ở `packages/ui`. Trong `components/**` hiện chỉ 32 export; không có `BusinessSwitcher`, `CommandPalette`, `PageTabs`, `FilterBar`, `Pagination`, `ColumnChooser`, `DetailLayout`, `MoneyInput`, `QuantityInput`… như `04 §2` (bản thật chỉ có những gì E2E/GĐ3 đã dùng). |

---

## §3. Kiểm kê component

### 3.1 Component export (32)

Cột **Tầng hiện tại**: `domain` = nghiệp vụ · `pattern` = khối lặp được · `primitive` = hạt cơ bản · `1 lần` = chỉ 1 màn dùng.
Cột **Tầng đích**: `P` = primitive (WP-44) · `Pa` = pattern (WP-45/46) · `D` = domain/giữ ở `apps` · `–` = không chuyển.

| # | Component | `file:dòng` | Tầng hiện tại | Route dùng | Trùng lặp với | Tầng đích (WP) | Ghi chú |
|---|---|---|---|---|---|---|---|
| 1 | `Sidebar` | `components/shell/Sidebar.tsx:10` | pattern (shell) | mọi route `/{business}` | — | Pa (WP-47) | Thu gọn dùng 2 ký tự + `title`, không icon; state không nhớ |
| 2 | `Topbar` | `components/shell/Topbar.tsx:3` | pattern (shell) | mọi route `/{business}` | — | Pa (WP-47) | Thiếu BusinessSwitcher/tìm kiếm/CommandPalette (`04 §4.3`) |
| 3 | `StockBadge` | `components/inventory/Badges.tsx:9` | domain | product/inventory, inventory/bill/[id] | badge thô ở product/item:160 | D (WP-49) | 3 số Tồn/Giữ/Bán |
| 4 | `StatusBadge` | `components/inventory/Badges.tsx:35` | domain | inventory/bill, inventory/bill/[id] | promotion:160, TemplateTable:176 | P (WP-44) Badge | Màu theo `status` phiếu kho |
| 5 | `DirectionBadge` | `components/inventory/Badges.tsx:51` | domain | inventory/bill, inventory/bill/[id] | — | D | Nhập/Xuất/Không chiều |
| 6 | `BalancesTable` | `components/inventory/BalancesTable.tsx:14` | pattern (bảng) | product/inventory, inventory/bill/[id] | bảng thô mọi DS | Pa (WP-45) DataTable | 13 cột; `title=` làm tooltip cột |
| 7 | `BillActions` | `components/inventory/BillActions.tsx:18` | domain | inventory/bill/[id] | `BillTable.chayBulk:109` | D | Ghi sổ/huỷ + mã lỗi |
| 8 | `BillForm` | `components/inventory/BillForm.tsx:69` | domain (form lớn) | inventory/bill/new | `PromotionForm`, `TreeBuilder` (khung form) | D (`WP-46` cung cấp FormField) | 582 dòng; dòng hàng động |
| 9 | `BillTable` | `components/inventory/BillTable.tsx:57` | pattern (bảng + bulk + chọn cột + CSV + in) | inventory/bill | `ExportCsvButton`, `SavedFilters` | Pa (WP-45) | Tự viết `xuatCsv:173`, `inPhieu:217` |
| 10 | `PrintButton` | `components/inventory/PrintButton.tsx:10` | pattern (hành vi) | inventory/bill/print, print/bill/[id] | — | Pa (WP-49) | `window.print()` sau 400 ms |
| 11 | `ProductPicker` | `components/inventory/ProductPicker.tsx:24` | pattern (combobox tự viết) | inventory/bill/new (qua `BillForm`) | `PosTerminal` ô tìm SP | P (WP-44) Combobox | `mousedown` ngoài để đóng; không role combobox/listbox |
| 12 | `SavedFilters` | `components/inventory/SavedFilters.tsx:20` | pattern (lưu bộ lọc) | inventory/bill | — | Pa (WP-45) | `localStorage` `nh:bill-filters:v1` |
| 13 | `OrderActions` | `components/order/OrderActions.tsx:32` | domain | order/manage/[id] | `BillActions`, `VoidBillButton` | D | Nút theo `allowedTransitions`; khối huỷ nội tuyến |
| 14 | `OrderStatusBadge` | `components/order/OrderStatusBadge.tsx:16` | domain | order/manage, order/manage/[id] | #4, promotion:160 | P (WP-44) Badge + D | 5 tông màu, hex nội tuyến |
| 15 | `PosTerminal` | `components/pos/PosTerminal.tsx:49` | domain + nhiều pattern dính | pos/bill | — | D (tách theo `WP-50`) | **1134 dòng**; tab/giỏ/khách/thanh toán/phím tắt |
| 16 | `ReturnForm` | `components/pos/ReturnForm.tsx:27` | domain | pos/return | — | D | 356 dòng; `Row:336`, `O:347` cục bộ |
| 17 | `VoidBillButton` | `components/pos/VoidBillButton.tsx:14` | domain | pos/bill/list | `OrderActions.huy` | D | Dùng `window.prompt` |
| 18 | `ExportCsvButton` | `components/report/ExportCsvButton.tsx:13` | pattern (xuất) | report/revenue, report/inventory | `BillTable.xuatCsv:173` | P (WP-44) Button + Pa | Client-side từ dữ liệu đang hiện |
| 19 | `StackedBarChart` | `components/report/StackedBarChart.tsx:26` | pattern (chart) | dashboard, report/revenue | — | P (WP-49) Chart | SVG thuần 720×220; `role=img` |
| 20 | `PermissionMatrix` | `components/settings/PermissionMatrix.tsx:18` | domain | settings/staff | — | D | Bảng checkbox; lazy PUT từng ô |
| 21 | `ScopeSelect` | `components/settings/ScopeSelect.tsx:15` | domain | settings/staff | — | D | 2 select + nút lưu |
| 22 | `StaffForm` | `components/settings/StaffForm.tsx:16` | domain | settings/staff | `CareForm`, `SegmentForm` (khung form) | D | 5 ô 1 hàng |
| 23 | `TemplateTable` | `components/notification/TemplateTable.tsx:24` | pattern (bảng + lọc client) | notifications/templates | bảng thô | Pa (WP-45/48) | 3 select lọc tại chỗ |
| 24 | `TemplateForm` | `components/notification/TemplateForm.tsx:22` | domain | notifications/templates/new | #28 | D | |
| 25 | `TemplateVersionEditor` | `components/notification/TemplateVersionEditor.tsx:39` | domain + dialog tự viết | notifications/templates/[id] | `BatchApprove` | D + P Dialog (WP-44) | 462 dòng; `role=dialog` tại `:386` |
| 26 | `QueueTable` | `components/notification/QueueTable.tsx:28` | pattern (bảng mở rộng dòng) | notifications/queue | — | Pa (WP-45/48) | `<tr onClick>` `:106` |
| 27 | `BatchForm` | `components/notification/BatchForm.tsx:32` | domain | notifications/batches | — | D | IdempotencyKey client |
| 28 | `BatchApprove` | `components/notification/BatchApprove.tsx:19` | domain + dialog tự viết | notifications/batches | #25 | D + P Dialog (WP-44) | `role=dialog` `:121` |
| 29 | `PromotionForm` | `components/promotion/PromotionForm.tsx:19` | domain | promotion/new | #8 | D | 12 ô lọc/thời gian |
| 30 | `TreeBuilder` | `components/promotion/TreeBuilder.tsx:41` | domain | promotion/[id] | — | D | 655 dòng; cây điều kiện |
| 31 | `CareForm` | `components/customer/CareForm.tsx:16` | domain | customer/[id] tab chăm sóc | #22 | D | |
| 32 | `SegmentForm` | `components/customer/SegmentForm.tsx:16` | domain | customer/[id] tab tổng quan | #22 | D | Export thêm `customerTypeNumber:146` (không phải component) |

### 3.2 Component cục bộ (khai báo trong trang/khác tệp)

| # | Component | `file:dòng` | Dùng ở | Ghi chú |
|---|---|---|---|---|
| L1 | `O` (nhãn–giá trị) | `app/[business]/customer/[id]/page.tsx:150` | customer/[id] | Trùng `L2`, `L4` |
| L2 | `O` | `app/[business]/order/manage/[id]/page.tsx:294` | order/manage/[id] | `children` thay chuỗi |
| L3 | `Dong` (dt/dd) | `app/[business]/inventory/bill/[id]/page.tsx:258` | inventory/bill/[id] | `mono` cho idempotency key |
| L4 | `Dong` | `app/[business]/print/bill/[id]/page.tsx:131` | print/bill/[id] | Bản in khổ hẹp |
| L5 | `Row` | `components/pos/PosTerminal.tsx:1125` | pos/bill | |
| L6 | `Row` | `components/pos/ReturnForm.tsx:336` | pos/return | Khác `L5` (tham số `gt`) |
| L7 | `O` | `components/pos/ReturnForm.tsx:347` | pos/return | |
| L8 | `TabDonHang`/`TabHoaDon`/`TabCongNo`/`TabChamSoc` | `app/[business]/customer/[id]/page.tsx:161,214,273,338` | customer/[id] | 4 server component con trong 1 tệp |

---

## §4. Pattern lặp

| # | Nhóm | Nơi đang lặp (`file:dòng`) | Khác biệt giữa các nơi | Tầng đích | WP |
|---|---|---|---|---|---|
| P-01 | **Bảng danh sách** | `nh-table`: `BillTable:316`, `BalancesTable:27`, `product/item:129`, `inventory/bill/[id]:194`, `promotion:128`, `TemplateTable:132`, `QueueTable:73`, `report/revenue:230,284`, `report/inventory:156,195`; bảng **tự chế**: `order/manage:298`, `pos/bill/list:144`, `customer/list:186`, `settings/staff:61,110`, `PermissionMatrix:91`, `customer/[id]:172,225,298` | 2 bộ số đo: `.nh-table` (ô 14px, 52px) vs tự chế (`text-[12px]`/`[13px]`, header xám `font-medium`). Header `.nh-table` 36px/600/`--text` | Pattern `DataTable` (WP-45), primitive `Table` (WP-44) | 45 |
| P-02 | **Tab + badge số** | `product/item:70-98` (badge chỉ tab "Tất cả"), `order/manage:168-196` (14 tab server, tab active đổi màu badge), `inventory/bill:177-205`, `order/manage/[id]:134-155` (không badge), `customer/list:113-134` (nút pill, không gạch chân), `customer/[id]:100-115` (pill) | 3 biến thể hình thức (gạch chân / pill / pill active `--brand`) + cao 38/36/0px; không `aria-current`; badge có/không | Pattern `PageTabs` (WP-45) | 45 |
| P-03 | **Thanh lọc** | `product/item:101`, `product/inventory:90`, `order/manage:199`, `inventory/bill:208`, `pos/bill/list:105`, `customer/list:136`, `promotion:80`, `notifications/queue:96`, `report/revenue:122`, `report/inventory:89`, `TemplateTable:56` | 11 nơi, 0 component dùng chung; khác ở: nút submit (`nh-btn-primary` vs `nh-btn`), có/không "Xoá lọc", hiển thị tổng ở phải, nhãn trên (`<span>`) vs trong (`<div>`), client vs GET form | Pattern `FilterBar` (WP-45) | 45 |
| P-04 | **Phân trang Trước/Sau + `n / m`** | `product/item:180-200`, `product/inventory:146-158`, `order/manage:378-390`, `inventory/bill:300-312`, `promotion:188-200`, `notifications/queue:154-166`, `pos/bill/list:235-258`, `customer/list:249-259` | 2 cách vô hiệu: `aria-disabled` trên `<Link>` (vẫn bấm/focus được) vs `pointerEvents:none`+`opacity` (chỉ pos/bill/list:238-253); customer/list **không** vô hiệu; nhãn `Trước`/`← Trước`, `Sau`/`Sau →` | Pattern `Pagination` (WP-45) | 45 |
| P-05 | **Trạng thái rỗng** | Trong ô bảng: `product/item:143-149`, `inventory/bill/[id]:207-213`, `BalancesTable:66-72`, `pos/bill/list:162-168`, `customer/list:202-208`, `TemplateTable:146-160`, `QueueTable:87-93`; khối card riêng: `order/manage:286-294`, `inventory/bill:286-297`; câu văn: `dashboard:118-128` (chart), `report/revenue:239-245` | 2 hình thức (dòng bảng vs card), 2 vị trí; có nơi gộp "rỗng" + "khớp lọc" thành 2 dòng (TemplateTable) | Pattern `EmptyState` = **T2** (WP-46) | 46 |
| P-06 | **Trạng thái lỗi** | `product/item:121-125`, `product/inventory:130-135`, `order/manage:279-284`, `inventory/bill:279-284`, `order/manage/[id]:63-71`, `inventory/bill/[id]:59-67`, `report/revenue:165-170`, `report/inventory:109-114`, `promotion:116-125`, `notifications/queue:140-149`, `promotion/[id]:51-60`, `notifications/templates:52-61` | 12 nơi; cấu trúc/khả năng "Thử lại" khác nhau; mọi nơi đều in **mã lỗi** (tốt) nhưng markup khác (`div` màu vs card + nút) | Pattern `ErrorState` = **T2** (WP-46) | 46 |
| P-07 | **Hộp thoại** | `BatchApprove:121-156`, `TemplateVersionEditor:386-420`, `window.prompt` `VoidBillButton:38` | 2 hộp tự viết + 1 prompt trình duyệt; không focus trap, không Esc, không trả focus | Primitive `Dialog`/`AlertDialog` (WP-44) | 44 |
| P-08 | **Xuất CSV** | `BillTable:173-215` (tự dựng Blob + `;` + BOM), `components/report/ExportCsvButton.tsx:24-36` + `lib/report.buildCsv` | 2 mã; cùng quy ước BOM/`;` nhưng 1 nơi nằm trong component bảng | Pattern `ExportButton` (WP-45) | 45 |
| P-09 | **Form + nút gửi chống lặp** | `BillForm:…` (`:69`), `PromotionForm`, `TemplateForm:149`, `BatchForm:225-232`, `TemplateVersionEditor:361`, `ScopeSelect:97`, `PermissionMatrix:136`, `CareForm:98`, `SegmentForm:120`, `StaffForm:120`, `BillActions:86-101`, `OrderActions:135-159`, `VoidBillButton:73`, `BillTable:240-261` | Cùng cơ chế `disabled={dangGui}` + đổi nhãn "Đang …", nhưng 0 primitive `Button`; mỗi nơi tự khai `useState` + `fetch` + `docLoiApi` | Primitive `Button` (WP-44) + `FormField` (WP-46) | 44/46 |
| P-10 | **Nhãn trạng thái (badge/pill)** | `Badges.tsx:35,51`; `OrderStatusBadge`; `promotion:160-166` + `mauTrangThai:216`; `product/item:160-170`; `TemplateTable:176-194`; `QueueTable:120-124` | 5+ bộ hex nền/chữ; cùng hình dạng `text-[11px] px-2 py-0.5 rounded-full` | Primitive `Badge` (WP-44) | 44 |
| P-11 | **Thẻ KPI** | `dashboard:104-116` (link, 26px số), `report/revenue:183-212`, `report/inventory:126-153` (24px số) | 3 nơi, 2 cỡ số (26/24px), chỉ dashboard bấm được | Pattern `StatCard` (WP-45) | 45 |
| P-12 | **Khung chi tiết 2 cột** | `order/manage/[id]:157` (`1fr 320px`), `inventory/bill/[id]:124` (`minmax(0,1fr) 320px`), `BillForm:262,491` (form + aside sticky) | Rail 320px cả 3; khác `sticky top-0` (chỉ `inventory/bill/[id]:183`, `BillForm:491`) | Pattern `DetailLayout` (WP-45) | 45 |
| P-13 | **Skeleton tải** | 6 tệp `loading.tsx` (xem §2.3) | Cấu trúc gần như bản sao; đều `aria-busy` + `aria-label` | Primitive `Skeleton` = **T1** (WP-44) | 44 |
| P-14 | **Cổng quyền phía UI** | `promotion:43-52`, `promotion/new:12-24`, `promotion/[id]:29-36`, `notifications/templates:29-35`, `notifications/templates/new:16-28`, `notifications/templates/[id]:27-32`, `notifications/batches:34-41` | 7 nơi tự `getGrantedPermissions` + `? form : câu "Cần quyền …"`; chưa có `<Can>` như `F9` | Pattern `PermissionGate` (WP-47) | 47 |
| P-15 | **Định dạng tiền/số** | `lib/format.ts` (`money/int/qty/amount/dateTime/dateOnly`); ngoại lệ cục bộ: `product/item:33` `money`, `BillForm:47` `soTuChuoi`, `promotion:206` `moTaGiam`, `promotion/[id]:142`, `Split` trong `PosTerminal` | 1 nguồn chính (tốt) + 4 bản cục bộ; `BillTable:195` ghi rỗng khi `null` (đúng `D-37`) | Giữ `lib/format`, thêm theo locale ở `WP-43` | 43 |

---

## §5. Lệch số đo Nhanh

Nguồn số đo Nhanh: `01` §2.1 (token), §2.2 (khung nhìn), `04` §3 (design system). "Chưa đo" = không có bằng chứng, đề xuất đo ở WP tương ứng.

| # | Hạng mục | Số đo Nhanh (nguồn) | Hiện tại (`file:dòng`) | Lệch | WP |
|---|---|---|---|---|---|
| M-01 | Nền app | `#E2E6ED` (`01` §2.1 `--body-bg`) | `--bg-app:#e2e6ed` (`globals.css:17`, dùng `:37`) | 0 | — |
| M-02 | Topbar | cao **52px**, `#252B36`, chữ trắng (`01` §2.2) | `h-[52px]`, `var(--topbar-bg)=#252b36` (`Topbar.tsx:6-7`, `globals.css:25`) | 0 | — |
| M-03 | Sidebar | rộng **~256px**, thu gọn **64px** (`01` §2.2; ghi rõ ~256 là theo ảnh, không đo) | `width: collapsed ? 64 : 256` (`Sidebar.tsx:18`) | 0 (khớp quyết định 04 §4.1) | — |
| M-04 | Sidebar thu gọn | chỉ icon + tooltip (`04 §4.1`) | 2 ký tự đầu của nhãn + `title` (`Sidebar.tsx:60,80,82`); không icon mục | **Lệch hành vi** (nhãn cắt chữ, không nhận ra mục) | 47 |
| M-05 | Sidebar nhớ trạng thái | "thu gọn nhớ trạng thái" (`04 §4.1`) | `useState(false)` cục bộ (`Sidebar.tsx:11`) ⇒ F5 mất | Lệch | 47 |
| M-06 | Font | Inter, body **14px** (`01` §2.1) | `font-size:14px`, `--font: Inter…` (`globals.css:28,40`) | 0 | — |
| M-07 | Nạp font | (không áp dụng — Nhanh tự host/GFonts) | `@import url(…fonts.googleapis.com…)` (`globals.css:1`) | Rủi ro CLS/privacy; `U41-2` đổi sang `next/font` | 41 |
| M-08 | Đầu bảng | cao **36px** (`01` §2.2) | `height:36px` (`globals.css:98`) | 0 | — |
| M-09 | Ô bảng | `--table-cell-padding-y:.75rem (12px)`, `-x:1.25rem (20px)` (`01` §2.1); hàng 48–56px (`04 §3.1`) | `padding:8px 20px; height:52px` (`globals.css:108-109`) | **padding dọc 8 vs 12px (−4px)**; cao dòng 52 nằm trong 48–56 | 42 |
| M-10 | Cỡ chữ trong bảng | ô 14px/400, nhãn cột **13px/600 màu `--text`** (`04 §3.2`) | `.nh-table` 14px (`globals.css:95`); nhưng bảng tự chế ép **12px** và header `--text-muted` + `font-medium`: `order/manage:298,300`, `pos/bill/list:144,146`, `customer/list:186,188`, `settings/staff:61,110,112`, `PermissionMatrix:91,93`, `customer/[id]:172,225,298` | **12 vs 14px, 500 vs 600, màu xám vs `--text`** | 45 |
| M-11 | Nút chính | xanh lá `#059669` (`01` §2.1, nghịch lý §2.1) | `.nh-btn-primary{background:var(--success)}` = `#059669` (`globals.css:63-67`) | 0 | — |
| M-12 | Link/tab active, ID | `#0C83FF` (`01` §2.1 `--primary`) | `--primary:#0c83ff` (`globals.css:9`), tab active dùng `--primary` | 0 | — |
| M-13 | Bo góc | `--dialog-inner-border-radius:.5rem` = 8px; 04 §3 có thêm `--radius-sm:6px` | `--radius:8px`; **thiếu `--radius-sm`** (`globals.css:8-29`) | Thiếu token (0 nơi dùng 6px) | 42 |
| M-14 | Token đủ bộ của `04 §3` | có `--primary-active`, `--success-active`, `--radius-sm`, `--table-cell-py/px` | thiếu cả 4 (`globals.css:8-29`); `primary-active`/`success-active` chưa nơi nào dùng | Thiếu token | 42 |
| M-15 | Token dùng nhưng **không khai** | — | `var(--brand)`: `customer/list:126`, `customer/[id]:108,289,356`, `CareForm:109`, `SegmentForm:132`, `StaffForm:131`; `var(--surface)`: `PermissionMatrix:94`; `var(--surface-alt)`: `PermissionMatrix:111` | **Sai màu thật**: chữ "Đã lưu/Đã thêm" và pill tab active không có màu, ô "Quyền" không nền | 42 |
| M-16 | Hex trong component (D2) | chỉ cho phép trong tệp token | ≥ 20 chỗ: `#ecfdf5` (`Badges:38`, `OrderStatusBadge:4`, `promotion:219`, `TemplateTable:181`, `BillActions:123`, `OrderActions:268`), `#fef2f2` (`Badges:40`, `OrderStatusBadge:5`, `login:72`, `BillActions:124`, `OrderActions:269`, `PosTerminal:646`), `#fff7ed`/`#fefce8` (`Badges:66`, `promotion:222,225`, `TemplateTable:183`), `#fffbeb`/`#b45309` (`OrderStatusBadge:6`, `OrderActions:274`), `#f8fafc` (`BillTable:347`, `ProductPicker:141`, `PosTerminal:798`), `#94a3b8` (`dashboard:127`, `report/revenue:222`), `#eff6ff`/`#1e40af` (`PosTerminal:640`), `#e8f1ff` (`Sidebar:75`) | Nhiều; `WP-42` ánh xạ, `WP-51` cấm | 42/51 |
| M-17 | Mật độ compact | hàng bảng 52/40px, input 36/32px (kế hoạch `U42-1`) | chỉ có 52px / 36px; không có biến thể compact (`globals.css`) | Thiếu cả chế độ (đúng lịch) | 42 |
| M-18 | Theme tối | cần (`D-327` ⑤) | 0 token `[data-theme]`, không `prefers-color-scheme` (`globals.css`) | Thiếu (đúng lịch) | 42 |
| M-19 | Cao tab | **chưa đo** (không có trong `01`/`evidence`) | 3 giá trị: 38px (`product/item:81`, `order/manage:175`, `inventory/bill:184`), 36px (`order/manage/[id]:143`), 34px (`PosTerminal:722`) | Chưa đo — đề xuất chốt 1 giá trị ở WP-45 | 45 |
| M-20 | Cao nút | **chưa đo** | 36px (`.nh-btn` `globals.css:49`), 32px (`BillTable:242,250,256,259,269`), 30px (`SavedFilters:83,96`) | Chưa đo — đề xuất 3 cỡ primitive (WP-44) | 44 |
| M-21 | Ô nhập | **chưa đo**; 04 §3 `--radius:8px` | 36px, focus ring `0 0 0 3px rgb(12 131 255/.12)` (`globals.css:74-88`); **không có** style `:disabled`/lỗi | Chưa đo; thiếu trạng thái | 42/46 |
| M-22 | Menu 2 cấp | **180 mục**, 9 nhóm (`01` §2.2, `04 §4.1`) | `lib/menu.ts` (số mục **chưa kiểm** — không đếm trong lượt này) | Chưa kiểm | 47 |
| M-23 | Màu header bảng | `04 §3.1` card `#FFFFFF`, viền `#DEE2E6` 1px | `thead th{background:#fff; border-bottom:1px solid var(--border)}` (`globals.css:97-106`) | 0 | — |

---

## §6. Nợ a11y thấy được

Chỉ nêu cái **đọc code thấy được**. Mức: **P1** = critical/serious (chặn dùng/tiền), **P2** = moderate, **P3** = minor
(theo `ui-pipeline` §3.2). Không suy đoán cái phải chạy axe.

| # | Vấn đề | `file:dòng` | Ảnh hưởng | Mức sơ bộ | WP dự kiến |
|---|---|---|---|---|---|
| A-01 | Dòng bảng bấm được bằng **chuột**, không bàn phím | `QueueTable.tsx:106-130` (`<tr onClick>`, không `tabIndex`/`role`/`onKeyDown`/`aria-expanded`) | Người dùng bàn phím/screen reader không mở được timeline hàng đợi | P2 | 48 |
| A-02 | Tab hoá đơn POS là `div` bấm được | `PosTerminal.tsx:718-729` (`onClick` trên `div`, không `role=tab`/`tabIndex`) | Không chuyển tab bằng bàn phím; screen reader không biết là tab | P2 | 50 |
| A-03 | Ô nhập thanh toán không có nhãn chương trình | `PosTerminal.tsx:1002-1014` (label là `span` anh em), `:1040-1062` (select + số tiền), `:1084-1097` (chỉ `placeholder`) | Screen reader đọc "edit text/number" không ngữ cảnh — sai tiền | P2 | 50 |
| A-04 | Ô nhập trong bảng dòng hàng không nhãn | `BillForm.tsx:407-413,421-427,430-436,439-445,457-463` (chỉ `placeholder`/tên cột ở `thead`) | Cùng lý do; nhiều ô giống nhau, không phân biệt được | P2 | 46 |
| A-05 | Ô SL/Đơn giá/CK trong giỏ POS không nhãn | `PosTerminal.tsx:854-884` | Như A-04 | P2 | 50 |
| A-06 | Hộp thoại tự viết thiếu vòng focus/Esc/trả focus/`aria-labelledby` | `BatchApprove.tsx:121-156`; `TemplateVersionEditor.tsx:386-420` (`role=dialog`, `aria-modal=true`, còn lại thiếu) | Bàn phím đi ra sau hộp thoại; Esc không đóng; focus không trả | P2 | 44 |
| A-07 | `window.prompt` cho lý do huỷ hoá đơn | `VoidBillButton.tsx:38` | Không kiểm soát, không nhãn, chặn luồng; mobile kém | P2 | 50 |
| A-08 | Phân trang `aria-disabled` trên `<Link>` vẫn bấm + vẫn focus | `product/item:184,194`; `product/inventory:148,154`; `order/manage:380,386`; `inventory/bill:302,308`; `promotion:190,196`; `notifications/queue:156,162` | Người dùng tưởng nút tắt nhưng bấm vẫn đi; trạng thái không nhất quán với `pos/bill/list:238-253` | P3 | 45 |
| A-09 | Tab hiện tại thiếu `aria-current`/`aria-selected` | `product/item:78-96`, `order/manage:172-193`, `inventory/bill:181-202`, `customer/list:120-132`, `customer/[id]:102-114`, `order/manage/[id]:140-152` | Screen reader không biết tab đang chọn | P3 | 45 |
| A-10 | Thông báo lỗi/thành công không `role="alert"`/`aria-live` | `login:71-75`; `BillActions:118-133`; `OrderActions:263-283`; `ScopeSelect:101-105`; `CareForm:102-111`; `SegmentForm:125-135`; `StaffForm:125-133`; `BatchForm:217-221`; `QueueTable:164-166` | Lỗi không được đọc lên (đặc biệt khi thao tác tiền) | P2 cho lỗi chặn tiền, P3 còn lại | 46 |
| A-11 | Sidebar thu gọn: nhãn cắt còn 2 ký tự, chỉ `title` | `Sidebar.tsx:55-66,71-84` (`title` ở `:58,80`) | Điều hướng không đọc được tên mục khi thu gọn | P2 | 47 |
| A-12 | Ô "Sản phẩm"/tìm SP: kết quả là nút, không `role=listbox/option`, không hỗ trợ mũi tên | `ProductPicker.tsx:112-156`; `PosTerminal.tsx:776-814,926-940` | Chỉ Enter chọn mục đầu; bàn phím không duyệt danh sách | P2 | 44 |
| A-13 | Checkbox ma trận quyền không tên trực tiếp | `PermissionMatrix.tsx:136-141` (ngữ cảnh chỉ ở `<th>` hàng/cột) | Screen reader phải suy từ ô bảng; dễ cấp sai quyền | P3 | 48 |
| A-14 | `.nh-btn` không có `:focus-visible` tuỳ biến; input đã tắt outline mặc định và thay bằng ring | `globals.css:45-71` (nút), `:85-88` (input) | Focus trên nút dựa outline UA (không đồng nhất); input có ring riêng — chấp nhận nhưng nên thống nhất | P3 | 42 |
| A-15 | Phím tắt toàn cục không giới hạn ngữ cảnh | `PosTerminal.tsx:547-585` (F1/F3/F4/F6/F8/F9/F10 gắn `window`) | F1 tạo tab mới khi đang gõ; F9/F10 lưu từ bất kỳ đâu (chủ ý nhưng cần tài liệu + cách thoát) | P3 | 50 |
| A-16 | Bảng nhóm theo ngày: dòng tiêu đề nhóm nằm trong `<tbody>` riêng, không quan hệ với dòng con | `order/manage:312-373` | Screen reader không đọc "nhóm ngày" khi duyệt từng dòng | P3 | 45 |
| A-17 | Biểu đồ `role="img"` chỉ có `aria-label` chung | `StackedBarChart.tsx:45` (dữ liệu ở `<title>` từng cột `:73-76`) | Không có mô tả tổng/ bảng thay thế; `<title>` chỉ hiện khi hover | P3 | 49 |
| A-18 | Không có skip-link tới `<main>` | `app/[business]/layout.tsx:34-41` (`<main>` có, không skip link) | Người dùng bàn phím Tab qua toàn bộ sidebar mỗi trang | P3 | 47 |
| A-19 | `aria-busy` đặt trên `div` không live region; skeleton không có `role=status` | 6 tệp `loading.tsx` (vd `notifications/templates/loading.tsx:7`) | Trạng thái tải không được thông báo | P3 | 44 |
| A-20 | Ảnh: **0** thẻ `<img>`/`next/image` trong `app/**` + `components/**` | (grep `--include=*.tsx` không có kết quả) | Không có `alt` sai vì không có ảnh | — | — |
| A-21 | `<html lang="vi">` đúng | `app/layout.tsx:17` | — (tốt) | — | 43 (dynamic theo locale) |
| A-22 | Landmark `<aside>/<header>/<main>` có | `app/[business]/layout.tsx:36-39` | — (tốt) | — | — |

---

## §7. Danh sách ảnh dự kiến đổi

**RỖNG** — WP-41 **không** đổi giao diện (0 dòng code UI). Vì vậy:

- `U41-3` chụp **ảnh chuẩn 27 route có giao diện** (trừ `app/page.tsx` chỉ redirect) + theme sáng hiện tại.
- Mọi khác biệt ảnh ở `U41-3`, `U42-3`, `U43-3`… so với bộ ảnh này **ngoài** danh sách của đặc tả WP tương ứng ⇒ **đỏ**.
- `expectedSnapshotChanges` của `UI-41` = `[]`.

---

## §8. Câu hỏi mở

| # | Câu hỏi | Lựa chọn | Khuyến nghị | Tin cậy |
|---|---|---|---|---|
| UI-41-Q1 | Bộ ảnh chuẩn chụp **bao nhiêu route**: theo kế hoạch (37) hay theo số thật? | (a) 37 theo kế hoạch — không chụp được; (b) **27 route có giao diện** (28 trang − 1 redirect) | **(b)** — `qa` không chụp được route không tồn tại; ghi chú chênh ở §2.4. Không cần `D-xx` (đảo ngược được). | Cao |
| UI-41-Q2 | "Component" baseline cho các WP sau: 32 export hay 32 + 7 cục bộ + 4 tab con? | (a) 32 export trong `components/**`; (b) 32 + 7 cục bộ + 4 tab con = **43**; (c) 47 là **số kế hoạch**, không suy từ cây | **(a)** làm baseline "component dùng chung"; nhóm cục bộ (§3.2) chỉ là **nợ gộp component** cho `WP-44/45`. | Cao |
| UI-41-Q3 | Token `--brand`/`--surface`/`--surface-alt` **đang thiếu** (M-15) gây sai màu thật ở 4 tệp: vá ngay trong WP-41 (sẽ **đổi ảnh**), hay để WP-42 định nghĩa token rồi vá? | (a) vá ở WP-41 → phá BR-06 (ảnh đổi); (b) **để WP-42** định nghĩa `--brand = --primary`/`--surface = --bg-surface` rồi vá, ảnh đổi nằm trong danh sách của `UI-42` | **(b)** — giữ WP-41 "0 đổi giao diện"; cần `decider` xác nhận vì đụng cam kết ảnh của `U41-3`. | Trung bình |
| UI-41-Q4 | Menu `lib/menu.ts` có bao nhiêu mục so với 180 của Nhanh — chưa đếm trong lượt này | (a) đếm ở `U41-3`/`WP-47`; (b) đếm ngay | **(a)** — không chặn U1; ghi "chưa kiểm" ở M-22. | Cao |
| UI-41-Q5 | Cao tab / cao nút chưa có số đo Nhanh (M-19/M-20) — chốt khi nào? | (a) đo Nhanh thêm bằng REA/CDP; (b) chốt theo primitive ở WP-44/45 và ghi "đề xuất" | **(b)** — tránh mở khảo sát mới giữa chuỗi; nếu người muốn giống tuyệt đối thì (a) trước `U44-1`. | Trung bình |

---

## Phụ lục A — Lệnh đồ thị/khảo sát đã dùng (bằng chứng)

- `Glob apps/admin-web/app/**/*.tsx` · `Glob apps/admin-web/components/**/*` · `Glob apps/admin-web/lib/**/*` (danh sách tệp).
- `Grep` `export default function|export function|^function|^const [A-Z]` (định nghĩa component + dòng).
- `Grep` `data-testid` → **0 kết quả** (chưa có test id; xem Phụ lục B/BR-08).
- `Grep` `getByRole|getByLabel|locator` trong `apps/admin-web/e2e` (selector E2E đang phụ thuộc).
- `Grep` `role="dialog"|window.confirm|window.prompt|aria-modal` · `Grep` `aria-|role=|tabIndex|autoFocus|<label`.
- `Read` toàn bộ 28 `page.tsx`, 2 layout, 6 `loading.tsx`, `globals.css`, và 32 component (hoặc phần liên quan của
  `PosTerminal`/`TreeBuilder`/`BillForm`/`TemplateVersionEditor`).
- `gitnexus route_map` **không** dùng để đếm route trang (công cụ chỉ trả route API).

## Phụ lục B — Selector đang ràng buộc (BR-08, không được bỏ)

| Selector | Nơi dùng | Nguồn |
|---|---|---|
| `input[name="username"]`, `input[name="password"]` | trang đăng nhập | `e2e/support/ui.ts:16-17`, `01-dang-nhap.spec.ts:16-24` |
| `role=button` tên `Đăng nhập`, `heading "Tổng quan"`, `heading "Tạo phiếu kho"`, `heading "Bán lẻ tại quầy"` | nhiều luồng | `e2e/**` |
| `tr[data-pos-dong="<sku>"]` | giỏ POS | `04-ban-le.spec.ts:58`, `PosTerminal.tsx:843` |
| `[data-pos-tab]`, `[data-pos-tab-active]`, `[data-pos-tab-moi]`, `[data-pos-dong-bo]` | POS | `PosTerminal.tsx:720-721,750,621` |
| `div.nh-card` + `button "Mở ca"` | POS | `04-ban-le.spec.ts:41-43` |
| `div.p-4` + text `"HOÁ ĐƠN BÁN LẺ"` | trang in bill | `05-in.spec.ts:32`, `print/bill/[id]/page.tsx:44,50` |
| `tbody tr` lọc theo text (mã SP, SKU) | DS sản phẩm, tồn | `02-tao-san-pham.spec.ts:50`, `03-nhap-kho.spec.ts:41,70` |
| Nhãn nút: `Lưu phiếu (Nháp)`, `Ghi sổ phiếu`, `Lưu (F9)`, `In` | luồng kho/POS/in | `e2e/**` |
