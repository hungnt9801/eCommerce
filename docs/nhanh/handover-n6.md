# Handover `N6` — WP-05: đơn hàng **18 trạng thái** + transition API + DS/chi tiết đơn

> Ngày 03/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`505692a`**)
> Kế hoạch: [07 §WP-05](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-38](decisions.md), [D-39](decisions.md), [D-40](decisions.md), [D-41](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-05** (BE việc 1–5 + FE việc 6), **không mở rộng phạm vi**.
> ⚠️ Phiên bị **khởi động lại máy** giữa lúc làm; mọi số đo dưới đây lấy **sau** khi khởi động lại (11:07–11:30).

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **82 mục** (`N5` chưa commit: 20 `M` + 62 `??`, phần lớn `??` là rác khảo sát Nhanh) |
| 2 | `git switch` | đã ở `feature/wp-03-catalog` · `HEAD = 505692a` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** · 11,5 s |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1785/1785 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 107 `.md`, **493** link, **347** đường dẫn backtick, **0** chết |
| — | Migration / NND | **55** / **74** |

**Điểm lùi trước khi sửa:** commit **`505692a`**. 📌 `N5` **chưa commit** khi mở phiên ⇒ phiên này commit
**một lần** cả `N5` + `N6` thành **`e6076ce`** (xem §7). Muốn dọn rác khảo sát: thêm mẫu vào `.gitignore`,
**đừng** `git add -A`.

### 1.2 Đã thi công

| # | Việc (07 §WP-05) | Nội dung | Đo được |
|---|---|---|---|
| 1 | `FulfillmentStatus` **18** + 3 danh mục | **append 9** giá trị `9…17` (giữ `0…8`): `PendingConfirm/Confirming/OutOfStock/WarehouseSwitched/Packed/CustomerCancelled/SystemCancelled/CarrierCancelled/ReturnConfirmed`; thêm `OrderType` (**10**), `OrderSource` (**14**), `OrderReasonCode` (**24**) — **số đo được** từ Open API v3 | 18/10/14/24 giá trị; test khoá **số cũ không đổi** + số mới ở cuối dải (§1.3) |
| 2 | `Order` + 11 cột | `Type`, `Source`, `CarrierCode`, `TrackingNumber`, `CodAmount`, `ShippingFeeCustomer`, `ShippingFeeActual`, `WeightGram`, `WarehouseId`, `SaleStaffId`, `PackerId`, `ReasonCode` + 3 hàm ghi có kiểm giá trị; **migration 56** (additive, `Type/Source` default **1**) | cột thật trên DB (§1.3); `0011` khi số âm/ngoài danh mục |
| 3 | `OrderStateMachine` mở rộng | **giữ 100% cạnh cũ** + cạnh đường xác nhận Nhanh + đường hoàn + `Hết hàng/Đổi kho`; **15 cạnh huỷ** sinh từ `CoTheHuyTu`; `API POST /orders/{id}/transitions`; **`POST /orders/{id}/cancel`** (2 cửa quyền, `D-40`) | `Mới → … → Thành công` **8/8 lời gọi 200**; 403 đúng mã `0003`/`0001`/`0010` (§1.4) |
| 4 | Nhãn hiển thị 4 trục | `OrderStateMachine.NhanHienThi` — **một** nhãn, ưu tiên huỷ → trả → hoàn → thất bại → thành công → chuyển → đóng gói → xác nhận → chờ xác nhận → hết hàng → mới | **19 ca** `[Theory]` + ca "mọi giá trị enum đều có nhãn" (chặn `_ => "Mới"`) |
| 5 | Tác động kho (`D-39`) | `TacDongKhoKhiVao`: `Confirmed` ⇒ **chốt giữ chỗ** (`Sale` + `ReservationRelease`); `Returned` ⇒ **nhập lại kho** (`CustomerReturn`, số `HOAN-{đơn}`, idempotent); **không** `ShippingOut` | `OnHand 10→8`, `Reserved 3→1`; hoàn: `OnHand 7→8` + phiếu `HOAN-…` (`Type=3/Status=1`) |
| 6 | FE 2 màn | `/{business}/order/manage` — **14 tab có badge**, grouped rows theo ngày, lọc server-side (số đơn/vận đơn · loại · nguồn · kho · ngày); `…/manage/[id]` — tab **động** (Thông tin · Sản phẩm · Lịch trình), nút **chỉ các bước hợp lệ** (từ `allowedTransitions`), huỷ 3 lựa chọn + 24 lý do | `typecheck` ✓ · `build` ✓ **14 route** (`order/manage` 171 B, `[id]` **3,32 kB**/109 kB) |

**Tệp thêm/sửa (BE 12 + FE 7 + docs):**
- **BE:** `src/eCommerce.Domain.Shared/Enums.cs` (18 + 3 enum) · `Orders/OrderErrorCodes.cs` (`0009/0010/0011`) ·
  `modules/Sales/…Domain/{Order,OrderStateMachine}.cs` · `…Application.Contracts/{OrderDtos,IOrderStatusAppService,OrderStatusDtos}.cs` ·
  `…Application/{OrderAppService,OrderStatusAppService}.cs` · `…EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs` ·
  `src/eCommerce.HttpApi/{Controllers/OrdersController.cs,eCommerce.HttpApi.csproj}` ·
  `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` (+1 `CHECK`) · migration **56** + snapshot + `schema.sql` ·
  `packages/api-client/{openapi.v1.json,src/schema.d.ts}`.
- **FE:** `lib/order.ts` · `components/order/{OrderStatusBadge,OrderActions}.tsx` ·
  `app/[business]/order/manage/{page.tsx,[id]/page.tsx}` · `lib/menu.ts` (bật mục **Đơn hàng**).
- **Khác:** `tools/probe-don-hang.ps1` (mới) · `test/eCommerce.Tests/Sales/Wp05DonHangNhanhTests.cs` (69 ca) ·
  `test/eCommerce.Tests/Domain/OrderStatusApiHopDongTests.cs` (+1 cặp quyền).

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · 34,5 s — 9 `CS8604` **có sẵn** (Address/Sales), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1855/1855 pass** (**+70** so với 1785) |
| `dotnet test test\eCommerce.IntegrationTests --no-build` | **90/90 pass** · 713 ms |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — **14 route**; `order/manage` 171 B (106 kB), `order/manage/[id]` **3,32 kB** (109 kB) |
| `pnpm spec:snapshot` + `pnpm gen:api` | **18/18 path** `/api/v1` (12 cũ + **6** của order); chạy **2 lần liên tiếp cùng hash** (không drift) |
| Migration | **56** — `20261103040705_Wp05_DonHang_Nhanh` (có trong `__EFMigrationsHistory`) |
| Cột thật trên DB | `OrdOrders.Type/Source` = `integer NOT NULL DEFAULT 1`; `CodAmount/ShippingFeeCustomer/ShippingFeeActual` = `numeric`; `WarehouseId` = `uuid` |
| NND (DB dev) | **74** — **không đổi** (không thêm unique index) |
| `schema.sql` | sinh lại từ chính migration: **+19 dòng, 0 xoá** (2787 → 2806) |
| `pwsh -File .\tools\probe-don-hang.ps1` | **`exit 0`** — **29 lời gọi HTTP** qua BFF + SQL (§1.4) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (xem §1.5) |
| DB | bất biến `tổng sổ cái = OnHand` = **8.000 = 8.000** |

### 1.4 Bằng chứng HTTP thật — `tools/probe-don-hang.ps1` (`exit 0`)

Đăng nhập **NextAuth** → gọi qua **BFF** `/api/proxy` (đúng đường client component dùng, `D-12`).
📌 **Setup** dùng API thật: tạo kho → phiếu nhập 10 → post → **giữ chỗ** 2 (đơn A) + 1 (đơn C).
📌 **Seed 3 đơn** bằng SQL vì đường tạo đơn công khai đòi **sổ giải trình giá** + **giữ chỗ** (`SAL-001/002`) —
hai việc đó **không** thuộc WP-05 (lý do ghi trong header của script **và** §5 nợ #1).

| # | Ca | Kết quả đo |
|---|---|---|
| ① | `GET …/order/manage` | **200** — *"Đơn hàng"*, *"Cần xử lý"*, *"Chờ/Đang xác nhận"*, *"Đã huỷ"* |
| ② | `GET /api/proxy/v1/orders/tabs` | **200** — **14 tab** |
| ③ | `GET …/orders?tab=can-xu-ly&q=<số đơn A>` | **200** — **1 dòng**, nhãn **"Mới"** |
| ④ | `GET …/order/manage/{A}` | **200** — số đơn, *"Mới"*, *"Huỷ đơn"*, *"Đã xác nhận"* |
| ⑤–⑦ | `POST …/orders/{A}/transitions` → 9 → 10 → **1** | **200/200/200**; bước **1** trả `stockEffect=ChotGiuCho`, `stockAffectedLines=1`, `warning=null` |
| ⑧ | `GET …/stock/balances?sku=` | **200** — `onHand 8.000`, `reserved 1.000`, `available 7.000` |
| ⑨ | transitions với `expectedVersion` **cũ** | **403** + `eCommerce:Order:0003` (`expectedVersion 1`/`currentVersion 3`) |
| ⑩ | `Đã xác nhận → Thành công` (nhảy cóc) | **403** + `eCommerce:Order:0001` (`from 1`/`to 5`) |
| ⑪ | huỷ qua cửa `transitions` | **403** + `eCommerce:Order:0010` (*"phải gọi POST /orders/{id}/cancel"*) |
| ⑫ | `POST …/orders/{B}/cancel` (`Khách huỷ`, lý do **4 Đơn trùng**) | **200** — `fulfillmentStatus 14`, `isCancelled true`, nhãn **"Đã huỷ"** |
| ⑬ | huỷ B **lần hai** | **200** — **idempotent**, không nhả chỗ lần 2 |
| ⑭ | huỷ A (**đã xuất kho**) | **403** + `eCommerce:Order:0001` (*"Hàng ĐÃ rời kho ⇒ phải TRẢ HÀNG"*) |
| ⑮ | A: → 2 → 13 → 3 → 4 → **5** | **5/5 = 200**; nhãn cuối **"Thành công"** |
| ⑯ | `GET …/orders/{A}/history` | **200** — `totalCount = 8` (8 bước, trục `Fulfillment`) |
| ⑰ | C: → 1 → 2 → 13 → 3 → 4 → **7 → 17 → 8** | **8/8 = 200**; bước 8 trả `stockEffect=NhapHangHoan`, nhãn **"Đã hoàn"** |
| ⑰i | `GET …/stock/balances?sku=` sau hoàn | **200** — `onHand 8.000` (7 + 1 nhập lại), `reserved 0` |
| ⑰j | SQL `InvStockDocuments` | **1** phiếu `HOAN-WP05-DH-C-…` · `Type = 3` (CustomerReturn) · `Status = 1` (Posted) |
| ⑱ | SQL bất biến | **tổng sổ cái 8.000 = OnHand 8.000** |

> ⚠️ **Hai bẫy đã dặp (đáng ghi):**
> ① **`OrdOrderLines` KHÔNG có `ExtraProperties`/`ConcurrencyStamp`** (khác `OrdOrders` — `FullAuditedEntity`
> chứ không phải `AggregateRoot`) ⇒ script seed chèn dòng đơn **thất bại im lặng** (psql không dừng), đơn C
> "Đã hoàn" ném `Order:0011` *"Đơn KHÔNG có dòng hàng"*. Nay probe **đếm lại 3 dòng sau seed** và `exit 1`
> nếu thiếu.
> ② **Tiến trình Host cũ sống sót**: `Stop-Process -Name eCommerce.HttpApi.Host` **không** khớp vì Host chạy
> bằng `dotnet eCommerce.HttpApi.Host.dll` (tên tiến trình là `dotnet`) ⇒ cổng 54275 vẫn do **Host cũ** giữ và
> `wait-host` báo "lên" ngay. Muốn chắc: kill theo **PID giữ cổng** (`Get-NetTCPConnection -LocalPort 54275`).

### 1.5 Cổng tài liệu + Mermaid

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **108** `.md`, **506** link, **347** đường dẫn
backtick, **0** chết. `npm --prefix tools run check:mermaid` → **PASS** — **213** khối, **0** lỗi.
📌 Mọi tài liệu mới (`handover-n6`, `decisions` D-38…D-41, `07 §WP-05`, `08 §3`) đã được cổng này quét —
không có link chết.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục sau **vẫn nằm trong WP-05** nhưng **chưa làm** (cắt có lý do):

1. **4 bảng phụ** `OrderLabel` · `OrderPacking` · `OrderHandover` · `OrderComplaint` (07 §WP-05 việc 2) —
   chưa có **đường ghi** nào cần tới chúng trong P0 (nhãn/biên bản bàn giao/khiếu nại là màn P1), và mỗi bảng
   là **một migration + API + màn** ⇒ làm khi có use case thật, **không** dựng bảng rỗng.
2. **Bulk đổi trạng thái** trên DS đơn — mỗi đơn có **tập bước hợp lệ khác nhau** (`allowedTransitions`) ⇒ một
   "đổi hàng loạt" phải chọn đích **chung** hoặc bỏ qua từng đơn; Nhanh giải bằng **nhập trạng thái từ Excel**.
   Cần quyết định trước khi code (ghi ở §5 nợ #3).
3. **Tab "Thanh toán" của đơn** — thuộc **WP-06** (đơn chưa có bảng thanh toán nhiều phương thức). DoD
   *"đơn tự vận chuyển ẩn 2 tab"* vì vậy **chưa nghiệm thu được** (tab *Lịch trình* đã ẩn đúng).
4. **Sửa nội dung đơn** (`order/edit` của Nhanh) — chưa có API; đơn đã tạo chỉ đổi **trạng thái**.
5. **Import trạng thái từ Nhanh/sàn** (`POST /orders/import-status` trong `03 §5.2`) — chưa làm; state machine
   đã sẵn sàng nhận 18 giá trị nên chỉ còn tầng map.
6. **Kiểm UI bằng browser thật (CDP)** — phiên này đo bằng **trang SSR thật qua NextAuth session** (§1.4 ①③④)
   + unit test, **không** mở Chrome như `N5`. Ghi rõ để phiên sau không tưởng đã có kiểm browser.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **`20261103040705_Wp05_DonHang_Nhanh`** (migration thứ **56**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` — `cd src\eCommerce.DbMigrator; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run` |
| Nội dung | `OrdOrders`: **+13 cột** (`Type`, `Source` NOT NULL DEFAULT **1**; `ReasonCode`; `CarrierCode`; `TrackingNumber`; `CodAmount`; `ShippingFeeCustomer`; `ShippingFeeActual`; `WeightGram`; `WarehouseId`; `SaleStaffId`; `PackerId`) + **2 index** (`(TenantId, WarehouseId, CreationTime)`, `(TenantId, TrackingNumber)` filter `NOT NULL`) + **1 CHECK** `CK_OrdOrders_TienVaKhoiLuong_KhongAm` |
| `Down` | Có: xoá CHECK → 2 index → 13 cột (mất dữ liệu vận chuyển/COD đã nhập — không tái tạo được) |
| Cách lùi | `dotnet ef database update 20261103033818_Wp04c_Tien_PhieuKho -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+19 dòng, 0 xoá**; `PostgreSqlSchemaTests` (74 NND) xanh |
| Ánh xạ mã lỗi | CHECK mới **đã** map trong `ViPhamRangBuocDb` (`CK_OrdOrders_TienVaKhoiLuong_KhongAm → eCommerce:Db:0001`) — thiếu là `RangBuocDbChuaAnhXaTests` **ĐỎ** |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-38** | Danh mục Nhanh dùng **số đo được** (type **10** · source **14** · reason **24**), giữ số gốc, **không** tự chế cho đủ số như kế hoạch ghi (13/12) |
| **D-39** | Tác động kho: `Confirmed` = **chốt giữ chỗ**, `Returned` = **nhập lại kho**, **không** `ShippingOut` (nếu ghi sẽ **trừ `OnHand` hai lần**); huỷ chỉ khi hàng chưa rời kho; `Xác nhận hoàn` có nhãn **riêng** |
| **D-40** | 3 trạng thái huỷ = **trục thực hiện** + đóng đơn trong **một** lời gọi (`HuyDonNhanh`); **2 cửa quyền**: `transitions` (`StatusChange`, chặn huỷ `0010`) ≠ `cancel` (`Cancel`) |
| **D-41** | **14 tab là dữ liệu** (`OrderTabs`), đếm & lọc **chung một `Expression`**; định nghĩa *"Cần xử lý"*; chống chồng tab `Thành công`/`Đang hoàn` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. ⚠️ **Seed đơn bằng SQL trong probe** — chỉ để đo transition API; **không** phải đường tạo đơn thật. Khi
   `SAL-001/002` (sổ giải trình + giữ chỗ) lên P0, probe phải đổi sang gọi API tạo đơn thật.
2. ⚠️ **Ba trạng thái huỷ KHÔNG có đường "HVC huỷ sau khi đã gửi"**: `CarrierCancelled` chỉ đạt được khi
   **hàng chưa rời kho** (`CoTheHuyTu`). Ca thật *"HVC huỷ đơn đang giao"* phải đi `Đang hoàn → Đã hoàn`
   (nhập lại kho). 📌 Đừng "nới" `CoTheHuyTu` cho có đủ 3 trạng thái — nới là **huỷ đơn đã xuất kho**.
3. ⚠️ **Bulk đổi trạng thái chưa có**: nếu làm ẩu (gọi tuần tự mọi đơn với **một** đích), các đơn không có
   cạnh sẽ trả `0001` và người dùng tưởng hỏng — phải theo khuôn `D-35` (**3 trạng thái**: thành công/lỗi/**bỏ qua**).
4. ⚠️ **`OrderTabs` là `Expression` trên `Order`** ⇒ thêm cột/thêm trạng thái **phải** sửa ở **một** chỗ đó;
   nếu ai viết thêm `if` trong page FE thì badge và nội dung **sẽ lệch**.
5. ⚠️ **`TacDongKhoKhiVao` là nguồn duy nhất** của *"bước nào đụng kho"*. Thêm side effect ở app service mà
   không thêm vào hàm thuần ⇒ test `Tac_dong_kho__CHI_Confirmed_va_Returned…` vẫn xanh nhưng hành vi **đã khác**.
6. ⚠️ **`HuyDon` cũ và `HuyDonNhanh` cùng tồn tại**: `HuyDon` (guard `Pending`, 1 dòng lịch sử) phục vụ
   `/api/app/**` + test cũ; `HuyDonNhanh` (5 trạng thái nguồn, 2 dòng lịch sử) phục vụ API v1. **Đừng** xoá
   `HuyDon` khi chưa chuyển hết caller.
7. ⚠️ **Probe để lại dữ liệu** (`WP05-…`, `WP05-DH-{A,B,C}-…`, `HOAN-WP05-…`): 6+ kho, nhiều đơn, phiếu nhập/hoàn
   — **cố ý**, để phiên sau kiểm bằng SQL.
8. 📌 **Số danh mục lệch kế hoạch** (10/14 vs 13/12): nếu ai "sửa cho khớp 07" bằng cách **thêm enum bịa**,
   báo cáo sẽ có nhóm không có thật. `D-38` là chỗ trả lời.

---

## 6. Việc đầu tiên của phiên sau

1. ~~**Commit `N5` + `N6`**~~ ✅ **ĐÃ LÀM trong phiên này** — commit **`e6076ce`** trên `feature/wp-03-catalog`
   (gộp cả `N5` chưa commit của phiên trước). Lệnh đã dùng (giữ lại để phiên sau tái sử dụng khuôn, **không**
   `git add -A` vì còn 51 mục rác khảo sát Nhanh):
   ```powershell
   cd D:\Projects\Tools\eCommerce
   git status --short        # xem danh sách; KHÔNG `git add -A`
   git add apps/admin-web tools/probe-kho-fe.ps1 tools/probe-don-hang.ps1 docs/nhanh modules/Inventory `
           modules/Sales src shared test packages/api-client
   git commit -m "feat(wp-04+wp-05): phieu kho FE + don hang 18 trang thai (N5+N6)"
   ```
2. **`N7` — WP-06 (nửa đầu)**: `RetailBill` + `CashSession` + thanh toán + in. Dùng lại `OrderStatusBadge`,
   `lib/format.ts`, pattern `OrderActions` (client gọi BFF + hiện **mã lỗi**) và **`04 §5` màn #12**.
3. Khi WP-06 có bảng thanh toán: **mở lại §2 mục 3** (tab Thanh toán của đơn) và **§2 mục 2** (bulk).
4. (Nếu cần) **commit rác khảo sát**: chốt với chủ dự án — hiện **51 mục `??`** là ảnh/JSON khảo sát Nhanh,
   **cố ý** không commit; đừng để chúng lẫn vào PR.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
dotnet test test\eCommerce.IntegrationTests --no-build
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\probe-don-hang.ps1      # 29 lời gọi HTTP (tự bật/tắt Host + Next)
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N4b` + `N5` + `N6`) |
| Commit | **`e6076ce`** — gộp **`N5` + `N6`** (cha: `505692a` = checkpoint `N1…N4b`) |
| Push / PR | Chưa |
| Tệp `N6` đã chạm | **BE:** `src/eCommerce.Domain.Shared/{Enums.cs,Orders/OrderErrorCodes.cs}` · `modules/Sales/eCommerce.Sales.Domain/{Order,OrderStateMachine}.cs` · `…Application.Contracts/{OrderDtos,OrderStatusDtos,IOrderStatusAppService}.cs` · `…Application/{OrderAppService,OrderStatusAppService}.cs` · `…EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs` · `src/eCommerce.HttpApi/{Controllers/OrdersController.cs,eCommerce.HttpApi.csproj}` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `src/eCommerce.EntityFrameworkCore/{Migrations/*Wp05*,Migrations/eCommerceDbContextModelSnapshot.cs}` · `test/eCommerce.IntegrationTests/Assets/schema.sql` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}` · `test/eCommerce.Tests/{Sales/Wp05DonHangNhanhTests.cs,Domain/OrderStatusApiHopDongTests.cs}`. **FE:** `lib/{order,menu}.ts` · `components/order/*` · `app/[business]/order/manage/{page,[id]/page}.tsx`. **Khác:** `tools/probe-don-hang.ps1` · docs (`decisions`, `07`, `08`, `handover này`) |
