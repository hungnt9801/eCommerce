# Handover `N5` — WP-04 (FE): DS phiếu + tạo phiếu + bulk + tiền + cột tồn 9 chiều

> Ngày 03/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`505692a`**)
> Kế hoạch: [07 §WP-04](07-ke-hoach-trien-khai.md) việc **7** · Chia phiên: [08 §3](08-session-protocol.md) · Quyết định: [D-33](decisions.md), [D-35](decisions.md), [D-36](decisions.md), [D-37](decisions.md) (**thay thế D-34**)
> **Mục tiêu phiên:** làm việc **7** của WP-04 (FE) ⇒ **WP-04 (BE + FE) ĐÓNG**.
> **Ba lượt trong cùng ngày:** ① 4 màn kho · ② khảo sát Nhanh + bulk/cột/bộ lọc/in phiếu · ③ **tiền của phiếu**
> (BE: `UnitPrice` + migration **55**; FE: Đơn giá · Thành tiền · Tổng tiền). **Lượt ③ có sửa BE.**

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.0 Khảo sát Nhanh.vn (cầu CDP, tài khoản `businessId=230222`) — 03/11/2026

Chi tiết + bảng đo đầy đủ đã ghi vào **[01 §4.2b](01-khao-sat-nhanh-vn.md)**. Tóm tắt cái **đổi được thi công**:

| Đo được trên Nhanh | Ảnh hưởng tới ta |
|---|---|
| Bộ lọc **một hàng**: `Kho hàng · ID · Loại · Kiểu · Ngày · Lọc` | Đổi nhãn DS phiếu: `Kho hàng` / `Loại` (nhập-xuất) / `Kiểu` (mode) / `Ngày` |
| DS **7 cột**: `ID \| Ngày · Kho hàng · SP · SL · Tổng tiền thanh toán · Người tạo · Khách hàng` | Thêm cột **Tổng tiền** (từ `totalAmount`); vẫn giữ 8 cột chứng từ/trạng thái của ta |
| `Thao tác ▾` bulk: `Xuất dữ liệu · In các phiếu XNK đã chọn · Gắn/Gỡ nhãn · Đổi ngày · Xoá` | Làm **4 việc tương ứng API hiện có**: ghi sổ · huỷ · **xuất CSV** · **in phiếu** (D-35) |
| **Không có tab trạng thái** cho phiếu kho (phiếu Nhanh xác nhận ngay) | Ta **giữ** tab `Nháp/Đã ghi sổ/Đã huỷ` (ADR-02) — khác biệt **có chủ ý** |
| Form nhập/xuất: dòng `Sản phẩm · Tồn · $/% · Thành tiền`; cuối phiếu `Tổng tiền hàng · Chiết khấu · VAT · Số HĐ VAT · Thanh toán` | Làm **Giá/Thành tiền/Tổng tiền hàng/VAT/Chiết khấu/Đã trả** (D-37); **Số HĐ VAT + tách thanh toán** là việc POS (WP-06) |
| Bảng dòng có **checkbox ẩn/hiện cột** | `⚙ Cột` cho bảng DS (D-36) |
| Mỗi loại phiếu một **route riêng** | `?kind=` cho form + menu **Chuyển kho** mở đúng kiểu |

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — 105 mục: `N1…N4b` chưa commit (49 `M` + 56 `??`) |
| 2 | `git switch` | đã ở `feature/wp-03-catalog` · `HEAD = 713acdd` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** · 26,75 s |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1773/1773 pass** · 6 s |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 106 `.md`, **472** link, **347** đường dẫn backtick, **0** chết |
| — | Migration / NND | **54** / **74** |

**Điểm lùi trước khi sửa:** commit **`505692a`** (gộp `N1…N4b`). 📌 **Còn 51 mục `??`** là **rác khảo sát Nhanh** —
**cố ý không commit**; muốn dọn thì thêm mẫu vào `.gitignore`, **đừng** `git add -A`.

### 1.2 Đã thi công (việc 7 của WP-04 + tiền)

| # | Màn (04 §5) | Route | Nội dung | Đo được |
|---|---|---|---|---|
| 7a | **#7 DS phiếu kho** | `/{business}/inventory/bill` | tab **có badge số**, lọc **server-side** (Kho hàng · Loại · Kiểu · Ngày), phân trang, empty state, hiện **mã lỗi** | trang **200** (§1.5 ①⑩) |
| 7b | **#8 Tạo phiếu kho** | `…/bill/new` (+ `?kind=`) | **chọn kiểu → form động** (9 tổ hợp hợp lệ), **ProductPicker**, dòng hàng động (SL 3 số lẻ · **Giá** · VAT% · serial), `IdempotencyKey` ở client | **201** (§1.5 ③); `?kind=transfer` **200** (§1.5 ⑮) |
| 7c | **Chi tiết phiếu** | `…/bill/[id]` | thông tin + **Ghi sổ/Huỷ**, **tồn hiện tại từng SKU**, **Đơn giá/Thành tiền/Tổng tiền** | trước post *"chưa có dòng tồn"*; sau post *"Đã ghi sổ" + `Bán 10`* (§1.5 ④⑦) |
| 7d | **Cột tồn 3 số** | `/{business}/product/inventory` | bảng **9 chiều**, `StockBadge` **Tồn/Giữ/Bán**, âm đỏ | **200** + `Bán 10` (§1.5 ⑧) |
| 7e | **Bulk `Thao tác`** | DS phiếu | chọn dòng + chọn tất cả; **Ghi sổ/Huỷ nhiều phiếu · Xuất CSV · In phiếu**; kết quả **3 trạng thái** (`thành công`/`lỗi`/`bỏ qua`) — D-35 | browser: *"2 thành công / 0 lỗi"*, Nháp **6 → 4**; *"0 thành công / 1 bỏ qua"* (§1.4) |
| 7f | **Cột + bộ lọc** | DS phiếu | `⚙ Cột`, `+ Lưu bộ lọc` (localStorage, P0) — D-36 | ẩn *"Chứng từ"* ⇒ header mất + **7 khoá** (§1.4) |
| 7g | **In phiếu** | `…/bill/print?ids=…` | mẫu cố định + **tổng SL + Đơn giá/Thành tiền + VAT + Thanh toán**, auto `window.print()`, trần **20** phiếu | **200** có *"PHIẾU"*, *"Tổng"* (§1.5 ⑭) |
| 7h | **Nhãn + `?kind=`** | DS + menu | nhãn `Kho hàng/Loại/Kiểu/Ngày`; menu **Chuyển kho** mở đúng kiểu | `?kind=transfer` có *"Kho đích"* (§1.5 ⑮) |
| **7i** | **TIỀN (BE + FE)** | 4 màn trên | `StockDocumentLine.UnitPrice numeric(18,2)` + hàm thuần **`TinhTienChungTu`** + DTO `Amount/VatAmount/TotalAmount/TotalVat/TotalPayment`; FE: ô **Giá** mỗi dòng, cột **Thành tiền**, **Tổng tiền hàng/VAT/Chiết khấu/Đã trả/Thanh toán**, cột **Tổng tiền** ở DS, tiền trên bản in — **D-37** | **browser**: 3 × 25.000 + VAT 8% ⇒ **75.000 / 6.000 / 81.000**, lưu xong chi tiết **y hệt** (§1.4) |

**Tệp thêm/sửa (17 tệp FE + 7 tệp BE):**

- **BE:** `modules/Inventory/…/TinhTienChungTu.cs` (mới) · `StockDocument.cs` (`UnitPrice` + `DatChiTiet`) ·
  `StockDocumentDtos.cs` · `StockDocumentAppService.cs` (map + tổng) · `InventoryDbContextModelCreatingExtensions.cs`
  (precision + CHECK) · `src/eCommerce.Domain.Shared/InventoryErrorCodes.cs` (`0050`) ·
  `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` (map CHECK → `0050`) · migration **55** + snapshot +
  `test/eCommerce.IntegrationTests/Assets/schema.sql` · `test/eCommerce.Tests/Inventory/Wp04cTienPhieuKhoTests.cs` (12 ca) ·
  `packages/api-client/{openapi.v1.json,src/schema.d.ts}`.
- **FE:** `lib/{stock,format,api-error,api,menu}.ts` · `components/inventory/{Badges,BalancesTable,BillActions,BillForm,ProductPicker,BillTable,SavedFilters,PrintButton}.tsx` ·
  `app/[business]/inventory/bill/{page,new/page,[id]/page,print/page}.tsx` · `app/[business]/product/inventory/page.tsx` ·
  `app/[business]/page.tsx` · `app/globals.css` · `tools/probe-kho-fe.ps1`.

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · 47,6 s — 9 `CS8604` **có sẵn** (Address/Sales), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1785/1785 pass** (**+12** ca tiền so với 1773) |
| `dotnet test test\eCommerce.IntegrationTests --no-build` | **90/90 pass** · 719 ms |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — **12 route**: `bill` **4,09 kB** (112 kB), `bill/[id]` **1,68 kB** (107 kB), `bill/new` **4,94 kB** (113 kB), `bill/print` **366 B** (106 kB) |
| `pnpm spec:snapshot` + `pnpm gen:api` | **12/12 path** — `schema.d.ts` **có** `unitPrice`/`amount`/`vatAmount`/`totalAmount`/`totalVat`/`totalPayment` |
| Migration | **55** — `20261103033818_Wp04c_Tien_PhieuKho` (có trong `__EFMigrationsHistory`) |
| Cột thật trên DB | `InvStockDocumentLines.UnitPrice` = **`numeric / 18 / 2`**; `CHECK CK_InvStockDocumentLines_UnitPrice_KhongAm` **có** |
| NND (DB dev) | **74** — **không đổi** (không thêm unique index) |
| `schema.sql` | sinh lại từ chính migration: **+10 dòng, 0 xoá** |
| `pwsh -File .\tools\probe-kho-wp04.ps1 -BoQuaHost` | **12/12 ca** — BE **không hồi quy** |
| `pwsh -File .\tools\probe-kho-fe.ps1` | **19/19 ca** (`exit 0`) — §1.5 |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 107 `.md`, **493** link, **347** đường dẫn backtick, **0** chết |
| `npm --prefix tools run check:mermaid` | **PASS** — **213** khối, **0** lỗi |
| DB | bất biến `tổng sổ cái = OnHand` = **10.000 = 10.000** |

### 1.4 Kiểm UI bằng **browser thật** (Chrome + CDP, không mock)

Mở **tab riêng** (không đụng tab Nhanh của bạn), đăng nhập như người dùng rồi thao tác thật:

| # | Thao tác | Kết quả đo |
|---|---|---|
| ① | Đăng nhập form `/login` → `/230222` | **200**, vào Dashboard |
| ② | `…/inventory/bill?status=Draft` | **6** checkbox dòng · có `Ghi sổ phiếu đã chọn` · `+ Lưu bộ lọc` |
| ③ | Chọn **2 phiếu** `PN-BULK-…` → **Ghi sổ phiếu đã chọn** | *"2 thành công / 0 lỗi"* · mỗi phiếu *"đã ghi sổ (1 bút toán)"* · tab Nháp **6 → 4** |
| ④ | Chọn **2 phiếu đã ghi sổ** → **Huỷ** | *"0 thành công / 1 bỏ qua"* — **không gọi API** (D-35) |
| ⑤ | **Xuất CSV** (bắt blob để đọc) | **5 dòng**, `;`, có nháy bao, BOM UTF-8 ở byte đầu |
| ⑥ | **⚙ Cột** → bỏ *"Chứng từ"* | header mất đúng cột · `localStorage nh:bill-cols` = **7 khoá** |
| ⑦ | **TIỀN:** form mới, dòng `MONEY-SKU-001` SL **3** × Giá **25.000**, VAT **8%** | cột Thành tiền **75.000** · Tổng tiền hàng **75.000** · VAT **6.000** · Thanh toán **81.000** |
| ⑧ | Bấm **Lưu phiếu (Nháp)** → chi tiết phiếu | **200**: dòng `3 · 25.000 · 8 · 75.000`; tổng **75.000 / 6.000 / 81.000** — **server tính lại**, khớp dự phóng |

> 📌 **Tab Nhanh của bạn đã được đóng tab test và đưa về `/product/item/index`**.

### 1.5 Bằng chứng HTTP thật — `tools/probe-kho-fe.ps1` (19 ca, `exit 0`)

Đăng nhập **NextAuth** → gọi qua **BFF** `/api/proxy` (đúng đường client component dùng, D-12):

| # | Ca | Kết quả đo |
|---|---|---|
| ① | `GET …/inventory/bill` | **200** — *"Phiếu kho"* + *"Tạo phiếu"* |
| ② | `GET …/bill/new` | **200** — *"Kiểu giao dịch"* + kho vừa tạo trong dropdown |
| ③ | `POST /api/proxy/v1/stock/bills` (10 × 25.000, VAT 8) | **201** — `Draft`, `In`, `Supplier` |
| ④ | `GET …/bill/{id}` **trước** ghi sổ | **200** — *"Nháp"* + *"chưa có dòng tồn"* |
| ⑤ | `POST …/{id}/post` | **200** — `postedEntries: 1` |
| ⑥ | `POST …/{id}/post` **lần hai** | **403** + `eCommerce:Inventory:0010` |
| ⑦ | `GET …/bill/{id}` **sau** ghi sổ | **200** — *"Đã ghi sổ"* + `Bán 10` |
| ⑧ | `GET …/product/inventory?sku=` | **200** — SKU + `Bán 10` |
| ⑨ | `GET …/stock/balances?sku=` | **200** — `onHand 10.000`, `available 10.000` |
| ⑩ | `GET …/bill?status=Posted` | **200** — có số phiếu |
| ⑬ | `GET …/inventory/bill` (bulk) | **200** — 4 nút bulk + `type="checkbox"` |
| ⑭ | `GET …/bill/print?ids={id}` | **200** — *"PHIẾU"* + số phiếu + *"Tổng"* |
| ⑮ | `GET …/bill/new?kind=transfer` | **200** — *"Chuyển kho"* + *"Kho đích"* |
| **⑯** | `GET …/bill/{id}` — **tiền** | **200** — `totalAmount 250.000`, `totalVat 20.000`, `totalPayment 270.000` |
| **⑰** | `GET …/bill/{id}` — **UI tiền** | **200** — có `Đơn giá`, `Thành tiền`, `Tổng tiền hàng`, **`250.000`**, **`25.000`** |
| **⑱** | phiếu **không khai giá** | **201**, `totalAmount = null` ⇒ UI hiện `—` (D-37) |
| **⑲** | **đơn giá âm** | **400** — lỗi tại `Lines[0].UnitPrice` ("Đơn giá phải từ 0 đến …") |
| ⑪ | Bất biến `Σ sổ cái = OnHand` (SQL) | **10.000 = 10.000** |

> ⚠️ **Hai bẫy đã dặp (đáng ghi):** ① **`next start` của build CŨ** giữ cổng 3000 ⇒ probe báo **404 toàn bộ
> trang** trong khi API xanh — nay probe **cảnh báo** khi thấy Next đang chạy sẵn. ② **`DbMigrator` bật lại
> `ShouldChangePasswordOnNextLogin`** ⇒ token trả `invalid_grant`/`ID2024`; cách xử: chạy
> `update "AbpUsers" set "ShouldChangePasswordOnNextLogin"=false where "UserName"='admin';` trước khi probe.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục sau **không** thuộc việc 7 của WP-04:

1. **Số hoá đơn VAT + ngày xuất hoá đơn + tách thanh toán (tiền mặt/chuyển khoản)** — Nhanh có, ta **chưa**: cần bảng thanh toán của POS (**WP-06**). Hiện chỉ có `PaidAmount` (một số) và `DiscountAmount`.
2. **`VatMode` (giá đã gồm VAT hay chưa)** — `D-37` tính `Thanh toán = tiền hàng + VAT − chiết khấu` theo giả định **giá chưa gồm VAT**; xử lý `Included/Excluded` là việc WP-06.
3. **Mẫu in cấu hình được** (P0 #18) — hiện là **mẫu cố định**, trần **20** phiếu/lần.
4. **Màn kiểm kho riêng** (#9) — tạo được phiếu `Stocktake` qua `?kind=stocktake`, nhưng màn đếm/lệch riêng **chưa** làm; menu **Kiểm kho** vẫn **mờ**.
5. **Sửa phiếu `Draft`** — **không có** endpoint (ghi ở [handover-n4b](handover-n4b.md) §2.5); màn #8 chỉ **tạo**.
6. **Chọn ĐVT/lô trên dòng phiếu** — chưa có API liệt kê `ProductUnit`/`ProductBatch`.
7. **Bulk gắn nhãn/đổi ngày/xoá** — không có API tương ứng (chưa có bảng nhãn cho phiếu kho).
8. **Test UI tự động** — chưa có (Playwright = **WP-09**); lượt này kiểm bằng **browser thật qua CDP** (§1.4).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **`20261103033818_Wp04c_Tien_PhieuKho`** (migration thứ **55**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` — `cd src\eCommerce.DbMigrator; dotnet run` (⚠️ chạy **Production** thì migrator in cảnh báo và **exit 1**, không áp; đặt `ASPNETCORE_ENVIRONMENT=Development`) |
| Nội dung | **+1 cột** `InvStockDocumentLines.UnitPrice numeric(18,2) NULL`; **+1 CHECK** `CK_InvStockDocumentLines_UnitPrice_KhongAm` (`NULL OR >= 0`) |
| `Down` | Có: xoá CHECK rồi xoá cột. ⚠️ Lùi **mất đơn giá** đã nhập (dữ liệu không tái tạo được) |
| Cách lùi | `dotnet ef database update 20261102102215_Wp04b_SoLuong_Decimal -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+10 dòng, 0 xoá**; `PostgreSqlSchemaTests` (74 NND) xanh |
| Ánh xạ mã lỗi | CHECK mới **phải** map trong `ViPhamRangBuocDb` (`CK_InvStockDocumentLines_UnitPrice_KhongAm → 0050`) — thiếu là test `RangBuocDbChuaAnhXaTests` **ĐỎ** (đã dặp và sửa ngay) |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-33** | Hợp đồng dữ liệu FE kho: type từ `schema.d.ts`; enum gửi **số**; 9 tổ hợp phiếu hợp lệ; `IdempotencyKey` sinh ở client |
| **D-35** | Bulk = gọi API **tuần tự**; kết quả **3 trạng thái**, `bỏ qua` ≠ `lỗi` |
| **D-36** | In phiếu = route cố định trần 20 · cột + bộ lọc = **localStorage** · CSV ở client có BOM |
| **D-37** | **TIỀN (thay thế D-34)**: `UnitPrice numeric(18,2)` + `TinhTienChungTu` + migration **55**; **làm tròn 2 số lẻ TỪNG DÒNG** rồi cộng; **thiếu giá ⇒ tổng `null`** (≠ 0); `UnitPrice` là **ảnh chụp giá**, không phải nguồn sự thật; **VAT chỉ là thông tin** + giả định **giá chưa gồm VAT** |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. ⚠️ **Cây làm việc vẫn dơ**: checkpoint `505692a` đã có nhưng **toàn bộ `N5` chưa commit** (BE + FE + docs). `git checkout` mạnh tay sẽ mất cả.
2. 🚨 **51 mục `??` là rác khảo sát Nhanh** — **đừng** `git add -A`.
3. ⚠️ **Tiền có hai bản tính**: server (`TinhTienChungTu`) là **chính thức**; FE (`tongTienPhieu`) chỉ **dự phóng**. Sửa công thức phải sửa **cả hai** + test — nếu không, form hiện một số còn chi tiết hiện số khác.
4. ⚠️ **Giả định "giá chưa gồm VAT"** đang nằm trong `TotalPayment`. Khi WP-06 đưa `VatMode` vào **phải** sửa `TinhTienChungTu.TongThanhToan` + ghi lại quyết định — đừng sửa lẻ ở UI.
5. ⚠️ **`UnitPrice` là ảnh chụp**, không phải giá hiện hành: báo cáo doanh thu/lợi nhuận phải dùng `UnitPrice` của phiếu, **không** join `ProductUnit.SalePrice`.
6. ⚠️ **`CHECK` mới phải giữ ánh xạ `0050`**; ai xoá dòng trong `ViPhamRangBuocDb` ⇒ test ràng buộc đỏ (tốt); nếu ai thêm vào danh sách trắng thay vì map thì client **mất mã lỗi**.
7. ⚠️ **In phiếu trần 20** — vượt thì **im lặng cắt** (`slice(0,20)`).
8. ⚠️ **Bulk tuần tự**: mỗi trang tối đa 20 phiếu ⇒ ≤ 20 lời gọi/lần; mở `pageSize` lớn hơn phải thêm chặn.
9. ⚠️ **`stocktake` trong form ≠ màn kiểm kho #9** — đừng để ai tưởng đã có "kiểm kho đầy đủ".
10. 📌 **Probe để lại dữ liệu** (`WPF5-…`, `WP04-…`, `PN-BULK-…`, `MONEY-SKU-001`): 3+ kho, nhiều phiếu — **cố ý**, để phiên sau kiểm bằng SQL.

---

## 6. Việc đầu tiên của phiên sau

1. **Commit toàn bộ `N5`** (BE + FE + docs) — cây đang có checkpoint `505692a`:
   ```powershell
   cd D:\Projects\Tools\eCommerce
   git add apps/admin-web tools/probe-kho-fe.ps1 docs/nhanh modules/Inventory src shared test packages/api-client
   git commit -m "feat(wp-04): phieu kho FE day du + tien dong phieu (N5)

   Refs: D-33, D-35, D-36, D-37"
   ```
2. **`N6` — WP-05** (đơn hàng 18 trạng thái): dùng lại `StockBadge`/`BalancesTable`/`lib/format.ts`/pattern `BillTable`.
3. (Khi WP-06) thêm **thanh toán nhiều phương thức + `VatMode`** ⇒ mở lại §2.1/§2.2; **đừng** sửa `TinhTienChungTu` lẻ tẻ mà không đổi `D-37`.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
dotnet test test\eCommerce.IntegrationTests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\probe-kho-fe.ps1        # 19 ca UI + BFF (tự bật/tắt Host + Next)
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N4b` + `N5`) |
| Commit | **`505692a`** (checkpoint `N1…N4b`) · **`N5` CHƯA commit** |
| Push / PR | Chưa |
| Tệp `N5` đã chạm | **BE:** `modules/Inventory/eCommerce.Inventory.Domain/{TinhTienChungTu,StockDocument}.cs` · `…Application.Contracts/StockDocumentDtos.cs` · `…Application/StockDocumentAppService.cs` · `…EntityFrameworkCore/InventoryDbContextModelCreatingExtensions.cs` · `src/eCommerce.Domain.Shared/InventoryErrorCodes.cs` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `src/eCommerce.EntityFrameworkCore/{Migrations/*Wp04c*,Migrations/eCommerceDbContextModelSnapshot.cs}` · `test/eCommerce.IntegrationTests/Assets/schema.sql` · `test/eCommerce.Tests/Inventory/Wp04cTienPhieuKhoTests.cs` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}`. **FE:** `apps/admin-web/lib/{stock,format,api-error,api,menu}.ts` · `components/inventory/{Badges,BalancesTable,BillActions,BillForm,ProductPicker,BillTable,SavedFilters,PrintButton}.tsx` · `app/globals.css` · `app/[business]/inventory/bill/{page,new/page,[id]/page,print/page}.tsx` · `app/[business]/product/inventory/page.tsx` · `app/[business]/page.tsx` · `tools/probe-kho-fe.ps1` · docs (`decisions`, `01`, `07`, `08`, `README`, handover này) |
