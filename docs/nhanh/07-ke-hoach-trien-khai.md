# 07 — Kế hoạch triển khai (GĐ0 → GĐ2, 12 tuần)

> **Trạng thái: CHỜ DUYỆT.** Chưa có dòng code nào được sửa cho phương án này.
> Đầu vào: [03 — Kiến trúc](03-kien-truc-dich.md) · [04 — Web Admin](04-web-admin-nextjs.md) ·
> [05 — Lộ trình](05-lo-trinh-rui-ro.md) · [decisions.md](decisions.md) (**D-01…D-18 đã chốt**).
> Phạm vi tài liệu này: **GĐ0–GĐ2** (nền + hàng hóa/kho/đơn + POS). GĐ3–GĐ5 giữ nguyên ở [05](05-lo-trinh-rui-ro.md).

---

## 0. "Duyệt plan" nghĩa là gì

Bạn duyệt → tôi bắt đầu **WP-01 + WP-02** (mục 5). Trong 2 tuần đầu bạn sẽ thấy:
repo có `apps/admin-web` chạy được, đăng nhập thật qua OpenIddict, và **màn danh sách sản phẩm đọc API thật**
(không mock). Nếu không duyệt, không có gì thay đổi ngoài thư mục `docs/nhanh/`.

---

## 1. Tám nguyên tắc thi công (áp dụng cho mọi WP)

| # | Nguyên tắc | Hệ quả cụ thể |
|---|---|---|
| 1 | **Additive trước, phá sau** | Mỗi migration chỉ **thêm** bảng/cột/enum ở cuối; cột cũ giữ tới khi có WP dọn |
| 2 | **Không big-bang** | Razor cũ vẫn chạy trong suốt GĐ0–GĐ2; Next.js phủ dần từng màn |
| 3 | **Không phá test** | Trước mỗi WP chạy baseline; số test chỉ được **tăng**, giảm phải ghi lý do trong PR |
| 4 | **API không breaking** | `/api/app/**` giữ nguyên; API mới ở `/api/v1/**` (D-13) |
| 5 | **PR nhỏ, một WP một nhánh** | `feature/wp-04-inventory-buckets`; mỗi PR ≤ ~600 dòng diff trừ migration |
| 6 | **Enum append-only** | `FulfillmentStatus`, `StockMovementType`, `StockDocumentType`… chỉ thêm cuối (D-17) |
| 7 | **Mọi con số phải đo** | DoD của WP ghi **lệnh đo** + **kết quả**, không ghi "đã xong" |
| 8 | **Rollback có đường** | Mỗi WP nêu cách lùi (feature flag / tắt đọc bảng mới / revert migration) |

---

## 2. Baseline & lệnh kiểm chứng (đo trước khi sửa)

| Hạng mục | Giá trị hiện tại | Lệnh |
|---|---|---|
| Build | 0 error / 0 warning | `dotnet build eCommerce.slnx` |
| Unit + integration | **1.634 + 90** (S19, 29/10) | `dotnet test test\eCommerce.Tests` · `test\eCommerce.IntegrationTests` |
| Migration | **50** | `src/eCommerce.EntityFrameworkCore/Migrations/` |
| Cổng tài liệu | PASS | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` · `npm --prefix tools run check:mermaid` |
| Cổng đầy đủ (cần Docker) | `-DayDu` exit 0 | `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` |
| Host dev | https `54274` · http `54275` | `src/eCommerce.HttpApi.Host/Properties/launchSettings.json` |
| Auth dev | `AuthServer:DisableTransportSecurityRequirement = true` | BFF gọi **http** `54275` (D-16) |
| Swagger | `/swagger/v1/swagger.json` | `AddAbpSwaggerGen` + `ConventionalControllers` |
| CORS | **chưa có policy** (chỉ `app.UseCors()`) | BFF gọi server-to-server ⇒ **không cần CORS**; chỉ thêm khi gọi trực tiếp từ browser |

📌 **Bắt buộc**: chạy baseline và **ghi số** vào PR đầu tiên. Nếu số lệch với bảng trên ⇒ dừng, báo, không tự diễn giải.

---

## 3. Cấu trúc repo sau thay đổi

```
eCommerce/
├─ pnpm-workspace.yaml                  ← MỚI (D-15)
├─ package.json                         ← MỚI (script gốc: dev, build, gen:api)
├─ apps/
│  └─ admin-web/                        ← MỚI (Next.js, 04 §2)
├─ packages/
│  └─ api-client/                       ← MỚI: schema.d.ts sinh từ swagger + fetch wrapper
├─ src/
│  ├─ eCommerce.HttpApi/                ← MỚI (D-14): controller /api/v1/**
│  ├─ eCommerce.HttpApi.Host/           ← giữ; đăng ký module HttpApi mới
│  ├─ eCommerce.Web/                    ← ĐÓNG BĂNG (chỉ sửa lỗi P0)
│  └─ … (không đổi)
├─ modules/
│  ├─ Catalog/                          ← mở rộng (WP-03)
│  ├─ Inventory/                        ← mở rộng (WP-04)
│  ├─ Sales/                            ← mở rộng (WP-05)
│  ├─ Pos/                              ← MỚI (WP-06)
│  ├─ Customers/                        ← mở rộng (WP-07)
│  ├─ Reporting/                        ← MỚI (WP-08)
│  └─ … (giữ nguyên)
└─ tools/
   └─ data-migration/                    ← MỚI: script backfill có dry-run
```

**Không làm ở GĐ0–GĐ2:** tách microservice, đổi tên project, bỏ Razor, bỏ `ProductVariant` khỏi DB.

---

## 4. Tổng hợp work package

| WP | Nội dung | Phụ thuộc | Ngày công (BE/FE) | Sprint |
|---|---|---|---:|---|
| WP-01 | Nền Frontend: pnpm workspace, Next.js, tokens, AppShell, login BFF | — | 0 / 6 | S0 |
| WP-02 | Hợp đồng API: project `HttpApi`, `/api/v1` mẫu, OpenAPI → TS client | — | 4 / 0 | S0 |
| WP-03 | Catalog: `Type`/`ProductUnit`/`Barcode`/`IMEI`/`Batch` + backfill variant | WP-02 | 10 / 3 | S1 |
| WP-04 | Inventory: 9 cột tồn + `Mode` phiếu + posting + backfill | WP-03 | 12 / 4 | S2 |
| WP-05 | Order: 18 trạng thái + loại đơn + lý do + transition API | WP-04 | 7 / 5 | S3 |
| WP-06 | POS: `RetailBill` + `CashSession` + thanh toán + in + offline | WP-04 | 8 / 8 | S4 |
| WP-07 | Customers + Settings/Staff + data scope | WP-05 | 5 / 3 | S5 |
| WP-08 | Reporting read-model + Dashboard + 2 báo cáo | WP-05 | 6 / 3 | S5 |
| WP-09 | E2E Playwright + cổng CI + OpenAPI drift | WP-01…06 | 2 / 3 | S0→S5 |
| WP-10 | Ops: Docker, env, health, log | WP-01 | 1 / 1 | S0, S5 |
| | **Tổng** | | **55 / 36** | **~91 ngày công ≈ 18 dev-week** |

Với đội 3 người (2,1 dev-week hữu ích/tuần) ⇒ **12 tuần** là khả thi, có buffer cho review/QA.
Nếu bạn làm **1 người**: 30–34 tuần cho cùng phạm vi.

---

## 5. Chi tiết work package

### WP-01 — Nền Frontend (S0, 6 ngày FE)

**Mục tiêu:** có app Next.js chạy, đăng nhập thật, shell đúng Nhanh, gọi được API thật.

Việc cụ thể:
1. `pnpm-workspace.yaml` + `package.json` gốc (`dev`, `build`, `lint`, `test`, `gen:api`).
2. `apps/admin-web`: Next.js App Router + TypeScript strict + Tailwind v4 + shadcn init.
3. `styles/tokens.css` chép **đúng** token ở [01 §2.1](01-khao-sat-nhanh-vn.md) (primary `#0C83FF`,
   success `#059669`, topbar `#252B36`, nền `#E2E6ED`, Inter, radius 8px).
4. `components/shell`: `Sidebar` (2 cấp, thu gọn 64px, nhớ trạng thái), `Topbar` (52px, dark),
   `BusinessSwitcher`, `CommandPalette` (Cmd+K).
5. `lib/auth`: Auth.js v5 + OpenIddict OIDC (Authorization Code + PKCE) → cookie httpOnly;
   `app/api/auth/[...nextauth]/route.ts`.
6. `app/api/proxy/[...path]/route.ts`: gắn Bearer, forward `http://localhost:54275`, map lỗi.
7. `lib/permissions` + `Can` component; menu lọc theo claim `permissions`.
8. `middleware.ts`: `?businessId=` → `/{business}/…` (308, D-11).

**DoD:** `pnpm dev` → `/login` → đăng nhập `admin@abp.io` → thấy shell + menu; mở `/vi/products`
(có thể rỗng) gọi `/api/v1/products` trả 200; refresh token tự động sau khi token hết hạn.

**Rollback:** xoá `apps/` + `packages/`, không ảnh hưởng backend.

**Cạm bẫy đã lường trước:** cert self-signed ở `https://localhost:54274` làm Node fetch lỗi
⇒ dùng **http `54275`** (D-16); Auth.js cần `AUTH_SECRET`; cookie `Secure` phải tắt ở dev.

---

### WP-02 — Hợp đồng API (S0, 4 ngày BE)

**Mục tiêu:** có bề mặt `/api/v1` ổn định + type TS sinh tự động, không chép tay.

Việc cụ thể:
1. Tạo project `src/eCommerce.HttpApi` (`eCommerce.HttpApi.csproj`), module `eCommerceHttpApiModule`
   `[DependsOn(Application, AspNetCore.Mvc)]`; thêm vào slnx + Host.
2. Controller mẫu `ProductsController` với **đúng 3 endpoint**: `GET /api/v1/products`,
   `GET /api/v1/products/{id}`, `GET /api/v1/products/lookup?barcode=`.
3. Chuẩn chung: `[ApiController]`, `[Route("api/v1/[controller]")]`, `[Authorize(Permission)]`,
   `ProducesResponseType` đủ, `CancellationToken`, phân trang `{items,total,page,pageSize}`.
4. Middleware lỗi: map `BusinessException` → RFC 7807 `{code,message,details,traceId}`.
5. Bật `openapi.json` ổn định: `CustomSchemaIds`, `OperationId` theo `Controller_Action`,
   `IncludeXmlComments` nếu có.
6. `packages/api-client`: `openapi-typescript` + `openapi-fetch`; script `pnpm gen:api`
   tải `http://localhost:54275/swagger/v1/swagger.json`.
7. CI: job `openapi-drift` — sinh lại schema, `git diff --exit-code`.

**DoD:** `pnpm gen:api` ra `schema.d.ts`; gọi 3 endpoint qua BFF trả dữ liệu thật; đổi 1 DTO
⇒ CI báo drift.

---

### WP-03 — Catalog (S1, BE 10 + FE 3)

**Mục tiêu:** sản phẩm theo mô hình Nhanh, **không phá** dữ liệu/test cũ.

Việc cụ thể:
1. Enum mới (append): `ProductType` 1..10, `VatMode` 1..2; mở rộng `ProductStatus` lên 4 giá trị
   (`Mới/Đang bán/Ngừng bán/Hết hàng`) — **không đổi số cũ**.
2. `Product`: thêm `Type`, `ParentId?`, `VatPercent`, `VatMode`, `WeightGram`, `WarrantyMonths`,
   `StorageTimeDays`, `IsHot`, `IsNew`, `ExternalId?`.
3. Entity mới: `ProductUnit` (Name, ConversionFactor, Barcode, IsBase, giá), `ProductImei`
   (Serial, Status 11, WarehouseId?, BatchId?..), `ProductBatch`, `ComboComponent`,
   `PackageComponent`, `ProductTag`.
4. Migration `51_Catalog_NhanhModel` (**additive**, có `Down`).
5. **Backfill** (`tools/data-migration/backfill-catalog.ps1` + service trong DbMigrator, có `--dry-run`):
   mỗi `CatProductVariants` → 1 `Product` con (`ParentId` = cha) + 1 `ProductUnit` gốc mang SKU/barcode/giá;
   ghi bảng đối chiếu `VariantId → ProductId` để lần chạy lại không tạo trùng.
6. `OrderLine`: thêm `ProductId?`, backfill theo `Sku`; **giữ** `Sku` + `ProductVariantId` (legacy).
7. Adapter đọc cũ: `IProductLegacyReader` cho Razor `/SanPham` (map ngược) — tránh sửa 22 view.

**DoD:**
- Tạo SP có 3 ĐVT + 2 IMEI + 1 lô qua API; `GET /products` trả đủ trường cho bảng 20 cột.
- Backfill chạy 2 lần cho **cùng kết quả** (idempotent), có log `n variant → n child`.
- Razor `/SanPham`, `/Brands` vẫn 200 và hiển thị đúng.
- Test: barcode trùng → lỗi; `ConversionFactor ≤ 0` → lỗi; SP cha không bán được.

**Rollback:** tắt đọc bảng mới bằng `Catalog:UseNhanhModel=false`; migration `Down` xoá bảng mới
(không xoá `ProductVariant`).

---

### WP-04 — Inventory (S2, BE 12 + FE 4)

**Mục tiêu:** tồn kho 9 chiều + phiếu kho theo mode Nhanh, sổ cái vẫn append-only.

> 🔧 **Điều chỉnh so với [03 §3.2](03-kien-truc-dich.md)**: hiện thực hoá `StockBill` bằng cách
> **mở rộng `StockDocument` + `StockDocumentLine`** (giữ bảng `InvStockDocuments`) thay vì bảng song song —
> giảm rủi ro migration, giữ nguyên 100% test sổ cái hiện có. Tên lớp giữ `StockDocument`, thêm `Mode`.

Việc cụ thể:
1. `InventoryItem` (bảng `InvItems`): thêm cột `Transferring`, `Shipping`, `Holding`, `Damaged`,
   `Warranty`, `WarrantyHolding`, `PreOrder`, `WaitingInbound`; đổi `OnHand/Reserved` **int → decimal(18,3)**
   (nới kiểu, an toàn); thêm `RowVersion`. Computed: `Available = OnHand − Transferring − Holding − Damaged`.
2. `StockDocument`: thêm `Direction` (In/Out), `Mode` (19 giá trị), `CounterpartyType/Id`, `RefType/RefId`,
   `DiscountAmount`, `PaidAmount`; `StockDocumentLine`: thêm `UnitId`, `VatPercent`, `BatchId`, `ImeiSerials`.
3. `StockMovementType`: append `ShippingOut=11`, `ShippingReturn=12`, `DamagedIn=13`, `DamagedOut=14`,
   `WarrantyHold=15`, `WarrantyRelease=16`, `Hold=17`, `HoldRelease=18`, `RetailSaleOut=19`.
4. `StockPostingService`: một cửa duy nhất, idempotent theo `(RefType, RefId, MovementType)`,
   khoá `InventorySku`; cấm Post 2 lần; cấm `Available < 0` khi bán.
5. Migration `52_Inventory_Buckets_Mode` + backfill `InvItems` từ `InvStockLedger`
   (script SQL có kiểm `SUM(delta) = OnHand` trước/sau).
6. API: `GET /stock/balances`, `GET/POST /stock/bills`, `POST /stock/bills/{id}/post|cancel`.
7. FE: màn DS phiếu + tạo phiếu (chọn mode → form động), cột tồn 3 số. ✅ **XONG ở `N5`** — 5 route
   `/{business}/inventory/bill{,/new,/[id],/print}` + `/{business}/product/inventory`; **tiền** (đơn giá →
   thành tiền → tổng) **đã làm** ở `N5` lượt 3 (`D-37`, migration **55**).

**DoD:** nhập 10 → Post → `OnHand=10`; bán 3 → `Available` giảm đúng; Post lại → 409/không đổi;
huỷ phiếu đã Post → bị chặn; test bất biến `tổng sổ cái = ảnh chụp` xanh.

**Trạng thái DoD (đo lại ở `N5`, 03/11/2026):** nhập 10 → Post → **`OnHand=10` trên UI** (`Bán 10`) ✅ ·
Post lại → **403 `0010`**, tồn **không đổi** ✅ · huỷ phiếu đã Post → **403 `0013`** ✅ · bất biến
**10.000 = 10.000** ✅ · "bán 3 → `Available` giảm" **chưa** có màn bán (WP-06) nhưng probe BE đã đo bằng
phiếu `GoodsIssue/Retail` (**⑩** `0001` khi bán 15 > tồn 10).
➕ **AC của màn #8 giờ đo được:** *"tổng tiền = Σ(dòng) đúng VAT"* — form 3 × 25.000 + VAT 8% hiện
**75.000 / 6.000 / 81.000**; lưu xong chi tiết phiếu vẫn **75.000 / 6.000 / 81.000** (server tính lại — `D-37`).

**Rollback:** `Inventory:UseBuckets=false` → API đọc `OnHand/Reserved` cũ; migration `Down` xoá cột mới.

---

### WP-05 — Order (S3, BE 7 + FE 5)

Việc cụ thể:
1. `FulfillmentStatus`: append 18 giá trị Nhanh (giữ `Pending/Confirmed/Processing/Shipped/Completed`
   ánh xạ sang nhóm mới), thêm `ReasonCode` (24 lý do), `OrderType` (13), `OrderSource` (12).
2. `Order`: thêm `Type`, `Source`, `CarrierCode`, `TrackingNumber`, `CodAmount`,
   `ShippingFeeCustomer`, `ShippingFeeActual`, `WeightGram`, `WarehouseId`, `SaleStaffId`, `PackerId`;
   `OrderLabel`, `OrderPacking`, `OrderHandover`, `OrderComplaint` (bảng mới).
3. `OrderStateMachine`: mở rộng transition theo bảng "thao tác × trạng thái" của Nhanh;
   **giữ** 4 trục + `RowVersion`; API `POST /orders/{id}/transitions`.
4. Nhãn hiển thị suy ra từ 4 trục (hàm thuần + test bảng ở [03 §3.5](03-kien-truc-dich.md)).
5. Migration `53_Order_NhanhStatus` (append enum số, thêm cột).
6. FE: DS đơn (14 tab đếm, grouped rows, bulk), chi tiết (tab động), đổi trạng thái.

**DoD:** đơn đi trọn `Mới → … → Thành công`; mỗi bước sinh đúng bút toán kho; tab "Cần xử lý"
đúng định nghĩa; đơn tự vận chuyển ẩn 2 tab.

**Trạng thái DoD (đo lại ở `N6`, 03/11/2026 — WP-05 ĐÓNG phần BE + FE chính):**
- ✅ **18 trạng thái**: `FulfillmentStatus` **append 9** giá trị `9…17` (giữ nguyên `0…8`), tổng đúng **18**.
- ✅ **Đơn đi trọn `Mới → … → Thành công`** trên **API thật** qua BFF: `Mới → Chờ xác nhận → Đang xác nhận →
  Đã xác nhận → Đang đóng gói → Đã đóng gói → Chờ thu gom → Đang chuyển → Thành công` — **8 lời gọi 200**,
  nhãn cuối **"Thành công"**, `FulfillmentVersion = 8`, **8 dòng** `OrdStatusHistory` (probe ⑤–⑯).
- ✅ **Mỗi bước sinh đúng bút toán kho**: bước `Đã xác nhận` ⇒ **chốt giữ chỗ** (`Sale` + `ReservationRelease`):
  `OnHand 10 → 8`, `Reserved 3 → 1`; bước `Đã hoàn` ⇒ **nhập lại kho** (`CustomerReturn`): `OnHand 7 → 8`
  + phiếu `HOAN-…` (`Type=3`, `Status=1`). **Không** ghi `ShippingOut` (lý do: `D-39`).
- ✅ **Tab "Cần xử lý" đúng định nghĩa**: **14 tab** có badge số, **đếm và lọc dùng CHUNG một `Expression`**
  (`D-41`); đơn `Đang chuyển` **không** thuộc tab, đơn `Thất bại` **thuộc** tab (có người phải xử lý).
- ⚠️ **"Đơn tự vận chuyển ẩn 2 tab"**: tab *Thanh toán* của đơn thuộc **WP-06** (chưa có) ⇒ DoD này
  **chưa nghiệm thu được**; tab *Lịch trình* đã ẩn với đơn chưa gửi HVC. Ghi ở `handover-n6` §2.
- 📌 **Số danh mục thực tế khác kế hoạch**: `OrderType` **10** (kế hoạch ghi 13), `OrderSource` **14**
  (kế hoạch ghi 12), `OrderReasonCode` **24** (đúng) — dùng **số đo được** từ Open API v3 (`D-38`).

---

### WP-06 — POS (S4, BE 8 + FE 8)

Việc cụ thể:
1. Module `Pos`: `RetailBill`, `RetailBillLine`, `RetailBillPayment`, `CashSession`, `RetailReturn`.
2. Luật: chiết khấu **dòng HOẶC bill** (chặn cả 2 tầng), `Payments[]` đa phương thức,
   thiếu tiền → công nợ + hạn thanh toán, điểm dùng/điểm tích.
3. Posting: `RetailSaleOut`; cập nhật tồn + điểm + công nợ **trong 1 transaction**.
4. API: `POST /pos/bills` (idempotent `Idempotency-Key = ClientBillId`), `POST /pos/bills/{id}/void`,
   `GET/POST /pos/sessions`, `POST /pos/sessions/{id}/close`.
5. FE: POS full-screen, tab đa hoá đơn lưu **IndexedDB**, phím F1/F3/F4/F6/F8/F9/F10,
   quét mã (3 chế độ), in (route `/print/bill/[id]`).
6. Offline: hàng đợi IndexedDB + service worker; trạng thái "chưa đồng bộ"; xung đột ghi `StockVariance`.

**DoD:** bán 3 SP, chia tiền mặt + chuyển khoản, in hoá đơn, F5 không mất tab, rớt mạng vẫn lưu,
kết ca ra `ExpectedCash`/`Variance`; tồn giảm đúng; doanh thu lên Dashboard.

---

### WP-07 — Customers + Settings (S5, BE 5 + FE 3)

- `Customer`: `Type` (lẻ/buôn/đại lý), `LevelId`, `Points`, `AccumulatedAmount`, `DebtLimit`,
  `DebtBalance`, `SaleStaffId`, `ReferrerId`, `TaxCode`, `Birthday`; `CustomerLevel` (ngưỡng),
  `CustomerCare` (log 5 loại).
- `Store`, `Department`, `Staff` (gắn `IdentityUser`), `RoleDataScope` (own/store/all) + seed quyền.
- Áp `data_scope` vào `Orders`/`RetailBills` (lọc server-side, không tin client).
- FE: DS khách 5 tab phân khúc, chi tiết 5 tab; nhân viên & ma trận quyền.

**DoD:** nhân viên bật "chỉ xem dữ liệu của mình" chỉ thấy đơn mình tạo; tiền tích luỹ đúng công thức.

**Trạng thái DoD (đo lại ở `N8`, 04/11/2026 — WP-07 ĐÓNG):**
- ✅ **11 cột mới trên `CusCustomers`** + **6 bảng** (`CusLevels` · `CusCares` · `SetStores` · `SetDepartments` ·
  `SetStaffs` · `SetRoleDataScopes`) — **migration 58**, `has-pending-model-changes` = *"No changes"*.
- ✅ **Sổ điểm/công nợ của POS ghi vào KHÁCH** (`D-45`, mở lại nợ #6 của `handover-n7`): bán 100.000
  (dùng 3 điểm, tích 15, trả 47.000) ⇒ khách **điểm 12 · tích luỹ 100.000 · nợ 50.000**; **tự lên cấp**
  theo ngưỡng 100.000; trả hàng ⇒ về **0/0/0**; huỷ ⇒ **đảo**, không âm.
- ✅ **`data_scope` lọc SERVER-SIDE** (`D-46`): `Own` ⇒ hoá đơn của người khác **không có trong DS** và
  đọc chi tiết ⇒ **403 `Pos:0028`**; trả `All` ⇒ thấy lại. Cùng hợp đồng áp cho `Orders` (mã `Order:0012`).
- ✅ **"Tiền tích luỹ đúng công thức"** (DoD): `AccumulatedAmount = Σ(Subtotal − DiscountTotal) − Σ(GoodsAmount)`
  — `DoiChieuSoDu` tự đối chiếu và trả `tienTichLuyKhop`/`congNoKhop`; 📌 probe lượt đầu **bắt được** chỗ
  dùng sai `TotalPayment` ⇒ đã sửa về **một** công thức.
- ✅ **Nhân viên & ma trận quyền**: `Settings.Default`/`Settings.Manage` khai **cả hai nơi**; ma trận đọc
  **sống** từ `PermissionDefinitionProvider` (**4 nhóm · 80 quyền**); lưu một ô ⇒ đọc lại từ store.
- ⚠️ **Cắt có quyết định:** chưa có màn CRUD cửa hàng/phòng ban (API có, FE chưa); chưa gán **role ABP** cho
  người dùng; phạm vi theo **vai trò** chứ chưa theo từng nhân viên; `Level.DiscountPercent` **chưa** nối
  bảng giá — xem `handover-n8` §2.

---

### WP-08 — Reporting (S5, BE 6 + FE 3)

- Schema `report`: `FactSalesLine`, `FactInventorySnapshot`, `DimDate/Product/Store/Staff`, `AggDaily*`.
- Job tổng hợp theo watermark (chạy lại được, backfill theo khoảng).
- API: `GET /reports/revenue`, `/reports/orders/summary`, `/reports/inventory/summary`; Dashboard 4 KPI.
- FE: trang Dashboard + 2 báo cáo (bảng + chart + export CSV).

**DoD:** tổng doanh thu báo cáo **=** `SUM(RetailBills + Orders)` trong cùng kỳ (test so sánh);
job chạy lại 2 lần không nhân đôi dữ liệu.

**Trạng thái DoD (đo lại ở `N9`, 04/11/2026 — WP-08 ĐÓNG):**
- ✅ **8 bảng read-model** (`RptFactSalesLines` · `RptFactInventorySnapshots` · `RptAggDailySales` ·
  `RptWatermarks` · 4 chiều `RptDimDates/Products/Stores/Staffs`) — **migration 59**,
  `has-pending-model-changes` = *"No changes"*; **8 index `NULLS NOT DISTINCT`** viết tay (NND **85 → 93**).
- ✅ **DoD #1 — doanh thu báo cáo = `SUM(RetailBills + Orders)`:** probe đo trên **dữ liệu thật**:
  API `1.470.000` **=** SQL độc lập `1.245.000` (bán lẻ) **+** `225.000` (đơn hàng); hội đủ
  `tongSoChungTu 13`, hoá đơn **đã huỷ** và đơn **đã huỷ** đều **không** vào read-model (`0` dòng).
- ✅ **DoD #2 — chạy lại 2 lần không nhân đôi:** chạy cùng khoảng 2 lần ⇒ **17** dòng sự kiện ·
  **2** dòng tổng hợp · **38** ảnh tồn kho **giữ nguyên**; DB có unique `(TenantId, Nguon, SourceLineId)` làm
  hàng rào cuối (test PostgreSQL thật: trùng ⇒ `23505`, khác nguồn/khác tenant ⇒ lọt).
- ✅ **Job theo mốc + backfill theo khoảng:** `KhoangTongHop` (mốc `null` ⇒ lùi 30 ngày; backfill thắng mốc;
  mốc **chỉ tiến**; trần **366 ngày** ⇒ ném `Reports:0003` chứ **không** tự cắt); worker đăng ký tường minh
  (`ReportingAggregationWorker`, `RunOnStart`, log vô điều kiện) + khoá phân tán `report:tong-hop`.
- ✅ **API:** `GET /reports/revenue` · `/reports/orders/summary` · `/reports/inventory/summary` ·
  `/reports/dashboard` (4 KPI) · `POST /reports/aggregate` (**quyền riêng** `eCommerce.Reports.Aggregate`).
- ✅ **FE:** Dashboard 4 KPI + biểu đồ 7 ngày, trang **Doanh thu** (bảng + biểu đồ SVG + **Xuất CSV**),
  trang **Tồn kho** (theo kho + sắp hết + CSV) — **23 route** (`pnpm build`).
- ⚠️ **Cắt có quyết định** (`D-48`): báo cáo **chưa** trừ **hàng trả lại**, **chưa** áp **phạm vi dữ liệu**
  (`D-46`), và ảnh tồn kho là **ảnh mới nhất** (không phải lịch sử) — xem `handover-n9` §2.

---

### WP-09 — Test & cổng (xuyên suốt, 5 ngày)

- Playwright 8 luồng: login · tạo SP · nhập kho · bán lẻ · in · đổi trạng thái đơn · kết ca · export.
- `dotnet test` + `pnpm test` + `pnpm e2e` trong CI; job `openapi-drift`.
- Mở rộng `tools/kiem-tat-ca.ps1` thêm bước FE (không phá baseline cũ).

**Đã thi công (`N10`, 04/11/2026) — [handover-n10](handover-n10.md), quyết định [D-49](decisions.md)/[D-50](decisions.md):**

- ✅ **8 luồng Playwright** trong `apps/admin-web/e2e/tests/` (`01-dang-nhap` … `08-xuat-csv`), chạy
  **tuần tự** (`workers: 1`) như **một ngày bán hàng liên tục**; chứng từ của luồng trước được luồng sau dùng
  (`e2e/.run/state.json`) ⇒ in đúng hoá đơn vừa bán, kết ca vừa mở.
- ✅ **`pnpm e2e` tự bật cả hai server** (`webServer`: Host 54275 + Next 3000) từ **build hiện tại** —
  `tools/e2e.ps1` **giết** tiến trình cũ trước khi chạy, nên không có cửa cho *"xanh trên build hôm qua"*.
- ✅ **`pnpm test` = Vitest (node), 25 ca** hàm thuần: định dạng `vi-VN`, BOM + `;` của CSV, công thức tiền POS,
  `toPage`/nhãn/`sortTransitions` của đơn. 🔑 Chọn **node** (không jsdom/RTL): UI thật do Playwright đo; test
  đơn vị chỉ giữ lớp **hàm thuần** — nơi lỗi im lặng sống.
- ✅ **CI:** job `openapi-drift` (bật Host → `spec:snapshot` + `gen:api` → so **trước/sau**) và job
  `e2e-playwright` (chạy trên **DB TRONG**, chỉ migrate + seed) — cộng vào `cong-15-buoc` (giữ nguyên làm
  required check, thêm 3 bước FE + build FE + chromium).
- ✅ **`tools/kiem-tat-ca.ps1` thêm 3 bước FE** (không phá baseline cũ): typecheck + Vitest; `gen:api` không
  làm đổi `schema.d.ts`; *(chỉ `-DayDu`)* Playwright 8 luồng qua `tools/e2e.ps1`.
- ✅ **Đo được:** `8 passed` E2E · **25/25** unit test FE · `openapi-drift` **PASS** (**51/51** path,
  `schema.d.ts` không đổi) · cổng `kiem-tat-ca.ps1` xanh với **3 bước FE** mới.
- ⚠️ **Hai điểm cắt có nhận chủ** (`D-49` ④): luồng "tạo SP" đi qua **API thật của BFF** (chưa có form SP —
  `WP-03` FE), và E2E tạo thêm **biến thể cũ** để có SKU (mô hình Nhanh giữ giá ở ĐVT) — nợ ở
  `handover-n10` §2/§5. **`WP-10` (Ops) CHƯA làm** — phiên `N11`.

### WP-10 — Ops (2 ngày)

- `docker-compose.yml`: thêm service `admin-web` (build production, `depends_on` host).
- `.env.example`: `API_BASE_URL`, `AUTH_SECRET`, `AUTH_ISSUER`, `NEXT_PUBLIC_*`.
- Health: `/api/health` (BFF) → gọi `/health-status` của Host.

**Đã thi công (`N11`, 04/11/2026) — [handover-n11](handover-n11.md), quyết định [D-51](decisions.md)/[D-52](decisions.md):**

- ✅ **`docker-compose.yml` thêm service `admin-web`** (build production, cổng **3000** mặc định): `docker compose
  up -d admin-web` dựng được Web Admin từ image `ecommerce-admin-web:local` (**354 MB**). Build context = **gốc
  repo** (workspace pnpm cần lockfile + `packages/api-client`) + `.dockerignore` ở gốc
  (không có nó: context ≈ **1 GB** và `node_modules` Windows làm `pnpm install` **bỏ qua** ⇒ image thiếu binary Linux).
- ✅ **`apps/admin-web/Dockerfile`** 2 tầng (`node:22-alpine`, `pnpm install --frozen-lockfile`, `next build` với
  `output: "standalone"`) — `next start` **vẫn** chạy ⇒ không phá 8 luồng E2E của `WP-09`.
- ⚠️ **`depends_on` host → thay bằng `healthcheck` gọi chính `/api/health`** (`D-51` ①): Host **không** là service
  của compose (D-16), và `depends_on` sẽ **chặn** admin-web khởi động khi Host chết ⇒ **che mất** đúng hành vi
  cần chứng minh (`503`). Container nói chuyện với Host qua `host.docker.internal` + `extra_hosts`.
- ✅ **`.env.example`**: `API_BASE_URL`, `AUTH_SECRET`, **`AUTH_ISSUER`** (mới — trống ⇒ fallback `API_BASE_URL`),
  `NEXT_PUBLIC_APP_NAME` (biến công khai **duy nhất**), `OAUTH_*`, `DEFAULT_BUSINESS`, `HEALTH_TIMEOUT_MS`.
  🚨 `NEXT_PUBLIC_*` nhúng **lúc build** ⇒ compose truyền bằng `build.args` (`D-52` ⑤).
- ✅ **`GET /api/health`** (`apps/admin-web/app/api/health/route.ts`): **200** Host sống / **503** Host chết hoặc
  DB hỏng, body chỉ `{status, checks.host}` (**không** lộ chi tiết — BR-012), `no-store`, trần 3 s.
- ✅ **Đo được:** `tools/kiem-ops-wp10.ps1` → **exit 0**, **9 lời gọi HTTP**, **0 lỗi**: Host chết ⇒ `503` +
  `Unreachable` (container `unhealthy` sau **91 s**), Host sống ⇒ `200` + `Healthy` (container `healthy` sau
  **10–15 s**); bản `next start` (không Docker) cũng **200/503** đúng; **`/login` vẫn 200** khi API chết.
  📌 `pnpm build` = **24 route** (+`/api/health`) · typecheck ✓ · **25/25** unit test FE · **8/8** E2E · drift
  **51/51** · **1961** unit + **96** integration · **0 migration mới** (59 vẫn là mới nhất).
- ⚠️ **Cắt có nhận chủ:** **chưa** thêm job `docker-build` vào `ci.yml` (`D-51` ⑦) — chưa kiểm được trên
  `ubuntu-latest`; **chưa** đưa Host .NET vào compose (image SDK + migration).

### WP-11 — Làm CỔNG XANH (0,5–1 ngày, phát sinh từ `N11`)

> 📌 **Vì sao có WP này:** `WP-10` đóng thì **hết WP-01…WP-10** của GĐ0–GĐ2, nhưng `N11` **đo được 2 HỒNG CÓ SẴN**
> ở cổng — nghĩa là **CI sẽ đỏ ngay lần push đầu** (job `cong-15-buoc` là required check). 🔑 Đây là nợ **cơ chế
> kiểm chứng**, không phải tính năng: sửa trước mọi việc khác, nếu không thì các phiên sau **mất lưới an toàn**.

- **① Bước ⑨ của cổng sẽ đỏ khi bước ⑧ xanh:** `tools/test-cong-kiem.ps1` (chạy trực tiếp) → **32/33**
  (ca Y1 "không còn API chi-Windows") vì **9 script cũ** còn **đường dẫn Windows viết cứng** thay vì
  `Lay-DuongDanBin` (`e2e.ps1`, `kiem-openapi-drift.ps1`, `kiem-pos-browser.ps1`, `probe-bao-cao-wp08.ps1`,
  `probe-don-hang.ps1`, `probe-khach-hang-wp07.ps1`, `probe-kho-fe.ps1`, `probe-kho-wp04.ps1`, `probe-pos.ps1`).
  📌 **Hiện nó bị CHE sau bước ⑧** (cổng dừng ở DB nên bước ⑨ không chạy) — sửa ② xong thì ① nổ ngay. Đúng lớp
  lỗi mà `XC-007` sinh ra để chống: trên `ubuntu-latest` các script đó **không tìm thấy DLL** ⇒ đỏ **vì môi
  trường**, và người ta học cách bỏ qua.
- **② Bước ⑧ (DB) đỏ:** **6 dòng** `CusCustomers` của probe `WP06`/`WP07` — phải **quyết dứt điểm**
  (xoá kèm dữ liệu phụ thuộc **hoặc** thêm ngoại lệ **có ghi lý do** trong `kiem-db.ps1`); ⛔ **KHÔNG** tắt cổng.
- *(tuỳ, nếu còn thời gian)* **③** job CI `docker-build` (`D-51` ⑦) — build `apps/admin-web/Dockerfile` trên
  `ubuntu-latest`.

**DoD:** `pwsh -File .\tools\kiem-tat-ca.ps1` → **exit 0** (test cổng **33/33** · DB **25/25**), và một dòng
`docker build` xanh nếu làm ③. 📌 Nếu ③ làm CI chậm quá ngưỡng chấp nhận ⇒ ghi lý do, **đừng** để job ở trạng
thái "đỏ mà không ai đọc".

**Đã thi công (`N12`, 04/11/2026) — [handover-n12](handover-n12.md), quyết định [D-53](decisions.md)/[D-54](decisions.md):**

- ✅ **① 9 script cũ hết khuôn chỉ-Windows** — `e2e.ps1` · `kiem-openapi-drift.ps1` · `kiem-pos-browser.ps1` ·
  `probe-bao-cao-wp08.ps1` · `probe-don-hang.ps1` · `probe-khach-hang-wp07.ps1` · `probe-kho-fe.ps1` ·
  `probe-kho-wp04.ps1` · `probe-pos.ps1`: đường dẫn `bin` → `Lay-DuongDanBin` (**đúng thứ `07` chỉ định**),
  `Get-NetTCPConnection` → `Lay-PidDangNghe`, `curl.exe` → `Lay-Curl`, `-WindowStyle` → `Start-TienTrinhAn`,
  `cmd.exe` → **`Start-NextAdmin`** (mới), `"$env:TEMP\…"` → **`Lay-DuongDanTam`** (mới), `-o NUL` → tệp tạm.
  📌 Hai hàm mới nằm ở `tools/nen-tang.ps1` ("một chỗ"), và `Lay-MauHamNenTang` của `test-cong-kiem.ps1`
  **đã biết** hai tên đó. Đo: `test-cong-kiem.ps1 -ChiKiemMoc` **33/33** (ca **Y1** từ đỏ → **xanh**).
- ✅ **② 6 dòng `CusCustomers` — quyết dứt điểm bằng bất biến MẠNH HƠN** (`D-53`): xoá **1** dòng vô chủ
  (**0** chứng từ), 5 dòng còn lại **giữ** (chúng có **11 hoá đơn + 3 phiếu trả + 8 dòng fact** trỏ vào —
  xoá là **xoá bằng chứng của chính chứng từ**). Nhóm **①c** mới: *mọi khách còn sống phải có ≥ 1 chứng từ*
  + *0 bản ghi xoá mềm*. Đo: DB **25/25**; **chiều ngược** `INSERT`+`ROLLBACK` ⇒ `moCoi 0 → 1 → 0`.
- ⏭ **③ job CI `docker-build`: KHÔNG làm** — lý do ghi ở `handover-n12` §2 (chưa kiểm được trên
  `ubuntu-latest`; thêm một job **chưa đo** vào required check = đúng thứ `WP-11` cảnh báo). Nhận chủ: **`WP-12` ③**.
- 🚨 **Đo thêm `-DayDu` (lệnh CI thật) ⇒ phát hiện bước ⑭ `probe-oversell` ĐỎ** (`T01`/`T09` so **chuỗi** với
  số lượng `decimal(18,3)` — nợ có sẵn từ `D-31`/`N4b`, **không** do `N12`) ⇒ đã **mở `WP-12`** ngay dưới.
- ✅ **DoD đạt:** `pwsh -File .\tools\kiem-tat-ca.ps1` → **exit 0** · **9/9** bước (**192,8 s** lượt cuối) ·
  test cổng **33/33** · DB **25/25** · **0 migration mới** · **0** test BE thêm (vẫn **1961** unit + **96**
  integration) · `e2e.ps1` **8/8** · drift **51/51** · tài liệu **114** tệp / **0** link chết.


### WP-12 — Làm **`-DayDu` XANH** (0,5 ngày, phát sinh từ `N12`)

> 📌 **Vì sao có WP này:** `N12` đóng **DoD của `WP-11`** (`kiem-tat-ca.ps1` **exit 0**, 9/9 bước) — nhưng
> **lệnh CI thật** là `-DayDu` (`ci.yml` bước `cong-15-buoc`), và cổng **chưa từng đi qua bước ⑧** kể từ `N7`
> nên **chưa ai thấy** các bước ⑩…⑰. 🔑 `N12` đo lượt đầu: **bước ⑭ `probe-oversell` ĐỎ** (`T01`/`T09`) vì
> `D-31` (`N4b`) đổi số lượng sang `decimal(18,3)` mà probe **so sánh CHUỖI** (`'1/0'` vs `1.000/0.000`).
> ⇒ 🔒 Đây vẫn là **nợ cơ chế kiểm chứng**, không phải tính năng: `cong-15-buoc` là required check, **đỏ** ⇒
> người ta học cách bỏ qua (đúng lý do `WP-11` sinh ra).

- **① Sửa `tools/probe-oversell.ps1` cho `decimal`:** so **SỐ** (`[decimal]`), **không** so chuỗi —
  `T01` hai vế, `T09` `[decimal]$phan[2] -eq 0`. ⛔ **Không** nới điều kiện, **không** bỏ ca.
  📌 Soát luôn 3 probe cùng họ dùng `Psql` + `-eq '…'` cho cột số lượng (`probe-outbox*`,
  `probe-giu-cho-2instance`, `probe-don-san`, `probe-thanh-toan`, `probe-khach-hang`, `probe-so-dia-chi`).
- **② Chạy TRỌN `-DayDu`** (`pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu`) và sửa **hết** bước đỏ —
  📌 mỗi bước đỏ phải có **lý do gốc** ghi ở handover, không "làm cho xanh".
- **③ *(tuỳ)* job CI `docker-build`** (`D-51` ⑦, `WP-11` ③) — cùng chủ đề "cổng/CI"; chỉ thêm sau khi
  đo được trên `ubuntu-latest` **một lần thật**.

**DoD:** `pwsh -File .\tools\kiem-tat-ca.ps1 -DayDu` → **exit 0** · `TONG SO BUOC DAT` = **19**
*(sửa ở `N13`: con số **17** ghi lúc mở WP là **đếm sai** — 9 bước nhẹ + **10** bước nặng; xem `D-55` ②;
⛔ **không** xoá bước nào để khớp con số)*, và `pwsh -File .\tools\kiem-tat-ca.ps1` (không `-DayDu`) **vẫn**
`exit 0` (không được làm đỏ bản nhẹ).

**Đã thi công (`N13`, 04/11/2026) — [handover-n13](handover-n13.md), quyết định [D-55](decisions.md):**

- ✅ **① `probe-oversell` so SỐ:** thêm `So()` = `[decimal]::Parse(…, InvariantCulture)`; `T01` tách
  `OnHand/Reserved` rồi so **số**, `T08` `So` thay `[int]`, `T09` `So` thay so chuỗi. 📌 Parse **invariant**
  vì trên máy `vi-VN` dấu `.` là phân cách nghìn. ⛔ Không nới điều kiện, không bỏ ca.
  Đo: `T01 … DAT | ton = 1.000/0.000` · `T09 … DAT | Available = 0.000`.
- ✅ **①b soát probe cùng họ:** `probe-giu-cho-2instance` **cùng lớp lỗi** (bị bỏ sót ở `N12`) — `P01`/`P11`
  seed + `P09` (`[int]`) + `P10` (chuỗi) → `So`. Các probe còn lại **không** cùng lớp: `probe-outbox*` so
  `FulfillmentVersion` (**int**)/`count(*)`; `probe-don-san` so watermark qua **JSON**; `probe-thanh-toan` so
  `lineTotal` từ JSON; `probe-khach-hang`/`probe-so-dia-chi` không so cột số lượng bằng `Psql` + `-eq '…'`.
  Đo: `P01 ton = 1.000/0.000` · `P10 Available = 0.000` · `P11 ton = 2.000/0.000` · probe **17/0**.
- ✅ **② Chạy TRỌN `-DayDu`:** lượt 1 xanh ①…⑱, **⑲ ĐỎ vì lỗi in của chính cổng** — `Get-Content` trả **CHUỖI**
  nên `$_.Line` = `$null` ⇒ `$_.Line.Trim()` **NÉM** dù Playwright đã `8 passed`. ✅ Sửa **một chỗ**
  (`$_.Line.Trim()` → `$_.Trim()`; 7 chỗ còn lại giữ `.Line` vì đi sau `Select-String` — `D-55` ③).
  Đo lượt 2: **exit 0 · 19/19 bước · 462,5 s**; ⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 instance
  **9/0** · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8** — ⇒ **nợ #3 của `handover-n12` §2 ĐÓNG**.
- ⏭ **③ job CI `docker-build`: KHÔNG làm** — chưa có `ubuntu-latest` để thấy nó xanh **một lần thật**; nhận chủ
  **`WP-13` ②**. 📌 Không phải bỏ sót.
- ✅ **DoD đạt:** `-DayDu` **exit 0 (19/19, 462,5 s)** · bản nhẹ **exit 0 (9/9, 193,5 s)** · test cổng **33/33**
  (full **38/38**) · DB **25/25** · E2E **8/8** · drift **51/51** · **0 migration** · **0** test BE thêm (1961 unit
  + 96 integration).

### WP-13 — P1: đóng 2 nợ **kiểm chứng** còn lại (0,5 ngày, phát sinh từ `N13`)

> 📌 **Vì sao có WP này:** `N13` đóng `WP-12` — mọi cổng **bắt buộc** đã xanh (`-DayDu` **19/19**), nhưng còn
> **2 nợ không chặn CI** mà `N12`/`N13` **đã đo được là không chạy được / chưa từng đo**. Cùng chủ đề
> "cổng/kiểm chứng", **không** phải tính năng. ⛔ **Không** tắt cổng nào để "cho xanh".

- **① `probe-kho-wp04.ps1` qua được cổng auth** (nợ `handover-n12` §5 #1): probe xin token bằng **password grant**
  trong khi cổng DB (nhóm ② của `kiem-db.ps1`) **bắt buộc** `admin.ShouldChangePasswordOnNextLogin = true`.
  → **một chỗ**: helper dùng chung trong `tools/nen-tang.ps1` (*mở cờ tạm → `finally` **bật lại***) cho cả 3 probe
  đang tự `UPDATE` cờ, **hoặc** cho probe đi qua **BFF/NextAuth** như `probe-kho-fe`. 📌 Sau lượt chạy, cờ **phải**
  = `true`, nếu không cổng DB đỏ và người ta sẽ **vô hiệu hoá cổng an ninh**.
- **② *(tuỳ)* job CI `docker-build`** (`D-51` ⑦, `WP-12` ③) — **chỉ** thêm sau khi thấy nó xanh **một lần** trên
  `ubuntu-latest`; chưa có runner thì ghi lý do, giữ P1.

**DoD:** `pwsh -File .\tools\probe-kho-wp04.ps1` → **exit 0** (**12 ca**) · `pwsh -File .\tools\kiem-db.ps1` →
**25/25** · `pwsh -File .\tools\kiem-tat-ca.ps1` (nhẹ) → **exit 0** · `-DayDu` vẫn **19/19** · **0 migration**.

**Đã thi công (`N14`, 04/11/2026) — [handover-n14](handover-n14.md), quyết định [D-56](decisions.md):**

- ✅ **① `probe-kho-wp04.ps1` qua cổng auth — MỘT CHỖ:** thêm **4 hàm** vào `tools/nen-tang.ps1`
  (`Dat-CoBuocDoiMatKhau` = hàm gốc, `Invoke-KhongCoDoiMatKhau` = mở → khối → `finally` bật lại,
  `Start-KhongCoDoiMatKhau`/`BatLai-CoDoiMatKhau` = cặp cho probe tắt suốt lượt chạy). Probe xin token
  **trong** `Invoke-KhongCoDoiMatKhau` ⇒ không tự `UPDATE`, cờ về `true` **ngay** sau khi lấy token.
  📌 Đo **trước** khi sửa: `exit 1` tại dòng token, `{"error":"invalid_grant","error_description":
  "ShouldChangePasswordOnNextLogin","error_uri":"…/ID2024"}` — ⚠️ **không** phải sai mật khẩu.
  Đo **sau**: **exit 0**, **12 ca** (①…⑫), ⑫ `tổng sổ cái 10.000 = OnHand 10.000`.
- ✅ **①b Đóng luôn lỗ "quên bật lại" (đo được **4** tệp, không phải 3):** `probe-pos` · `probe-don-hang` ·
  `probe-bao-cao-wp08` · `probe-khach-hang-wp07` **tắt** cờ mà **không** bao giờ bật lại ⇒ lượt chạy để lại
  một admin **không bị buộc đổi mật khẩu** + cổng DB nhóm ② đỏ ở lượt sau. Nay cả 4 dùng
  `Start-KhongCoDoiMatKhau` + `BatLai-CoDoiMatKhau` ở **dòng đầu `finally`**.
  🚨 **Vì sao 4 tệp này cần cặp hàm (không phải wrapper):** chúng đi **cả** token Host **và** đăng nhập
  BFF/NextAuth — BFF cũng xin token bằng **password grant** ⇒ mở cờ quanh một khối ngắn là **không đủ**.
  Đo: `probe-pos.ps1` **exit 0**, **34** lời gọi HTTP, `expectedCash 500.000`/`variance 0`, bất biến
  **9/7/9 = 9/7/9** ⇒ cờ về `ADMIN|true`, `kiem-db.ps1` **25/25** ngay sau đó.
- ✅ **①c Ca kiểm mới cho chính nợ này** (`test-cong-kiem.ps1`, **2 ca**): **Y6** — quét *script nào mở cờ
  phải có dòng bật lại* (**hai chiều**: phải thấy ≥ 4 tệp, nếu không thì phép quét vô nghĩa); **L2** — DB:
  cờ `false` **bên trong** khối, `true` sau, **và** ở **tiến trình con** khối `exit 7` ⇒ exit code **7**
  giữ nguyên + cờ `true` (🔑 `finally` của PowerShell **chạy cả khi** khối lệnh `exit`).
- ⏭ **② job CI `docker-build`: KHÔNG làm** — chưa có lần chạy nào trên `ubuntu-latest` để thấy nó **xanh một
  lần thật** (máy dev chỉ có `origin` + **không** có `gh`; `git log` toàn commit local, nhánh **chưa push**).
  Thêm job **chưa đo** vào required check = đúng thứ `WP-11`/`WP-12` cảnh báo ⇒ giữ **P1**, nhận chủ
  **phiên P1 kế tiếp** (phải đo **trước**, giống luật của `WP-12` ③).
- ✅ **DoD đạt:** `probe-kho-wp04` **exit 0 (12 ca)** · `kiem-db.ps1` **25/25** (cờ **bật lại**: `ADMIN|true`) ·
  bản nhẹ **exit 0 (9/9)** · `-DayDu` **exit 0 (19/19)** · **0 migration** (59 vẫn mới nhất) · **0** test BE thêm
  (**Y6** + **L2** là 2 ca của **cổng**, không phải test .NET).

**Đã thi công (`N15`, 04/11/2026) — [handover-n15](handover-n15.md), quyết định [D-57](decisions.md):**

- ✅ **② job CI `docker-build` — ĐÃ LÀM, và đo được **một lần thật** trên `ubuntu-latest`:** workflow **riêng**
  `.github/workflows/docker-build.yml` (`name: docker-build`, job `docker-build`, `timeout 20 phút`) —
  `docker build -f apps/admin-web/Dockerfile` với **context = gốc repo** (`D-51` ④) + `NEXT_PUBLIC_APP_NAME`
  là **build arg** (`D-52` ⑤), rồi **2 bất biến sau build**: image có `apps/admin-web/server.js` +
  `.next/static` (`output: "standalone"`), và container **bật được** với `/login` = **200** khi API chết
  (`D-52` ⑦). 📌 Workflow **riêng** vì job không cần .NET/DB: có status check riêng, không làm chậm
  `cong-15-buoc`, log không trộn (cùng lý do `D-50` ③).
- ✅ **Đo:** run [**`41808550753`**](https://github.com/hungnt9801/eCommerce/actions/runs/41808550753) —
  `docker-build` **success**, **76 s** (bước build **68 s**), **6/6** bước xanh, trên nhánh tạm
  `ci/n15-docker-build` (đã **xoá**; `main` **không đổi** = `531bd9d`). Trước đó, cùng ngày, **cùng máy dev**:
  `docker build` **exit 0** (image `354 MB` theo `docker images` / **83 MB** theo `inspect .Size`), `/login`
  **200** ở lần thử **2**, `/api/health` **503** khi API chết.
- 🚨 **Lượt đo ĐẦU ĐỎ — lý do gốc, không phải "làm cho xanh":** `apps/admin-web/Dockerfile` và `.dockerignore`
  **chưa từng được commit** (untracked từ `N11`) ⇒ commit tạm đầu **không có** chúng ⇒ `docker build` chết ở
  bước build. 🔑 Đây là **nợ thật**: *clone repo công khai hôm nay **không** build được image Web Admin*.
  ⇒ Ghi ở `handover-n15` §5 #1: `Dockerfile` + `.dockerignore` **phải** vào commit đầu tiên.
- ⚠️ **Required check chưa bật** (`D-57` ⑥): bật `docker-build` trong branch protection là thao tác trên
  **settings của repo**, không nằm trong mã — và chỉ nên bật sau khi thấy nó xanh trên **một PR thật**.

**Đã đóng gói (`N16`, 04/11/2026) — [handover-n16](handover-n16.md), quyết định [D-58](decisions.md)/[D-59](decisions.md):**

- ✅ **Commit → push → PR #1:** `d8e22dc` (**cây FE**, **60** tệp — 🚨 **bắt buộc**, xem gạch đầu dòng sau) +
  `6c49d3a` (**đúng 3 tệp** đóng gói: `.dockerignore` · `apps/admin-web/Dockerfile` ·
  `.github/workflows/docker-build.yml`) ⇒ `origin/feature/wp-03-catalog` = `6c49d3a` ⇒ **PR [#1](https://github.com/hungnt9801/eCommerce/pull/1)**
  (`open`, 39 commit, 488 tệp). 📌 Máy dev **không có `gh`** ⇒ PR tạo bằng **REST API** + credential sẵn có của git.
- 🚨 **Đo TRƯỚC khi push — lượt A ĐỎ:** `docker build` trên cây `HEAD` + **đúng 3 tệp** ⇒
  `"/repo/apps/admin-web/.next/standalone": not found` (exit 1) vì `output: "standalone"` (`N11`) nằm ở
  `next.config.ts` **chưa commit** ⇒ **D-58**: commit đóng gói **phải** kèm cây FE (tách 2 commit theo chủ đề).
  ✅ Cây **sạch** của `6c49d3a`: build **exit 0** · `server.js` **6380 B** · `/login` **200** (lần thử 2) khi API
  chết · `/api/health` **503** · **83 MB** (`inspect`) / **354 MB** (`docker images`).
- ✅ **`docker-build` XANH trên event `pull_request` (lần đầu):** run
  [**`41811821775`**](https://github.com/hungnt9801/eCommerce/actions/runs/41811821775) — **success**, **88 s**,
  **6/6** bước ⇒ **rồi mới** bật `docker-build` làm **required check** trên `main` (**D-59**).
- 🚨 **`main` trước phiên này KHÔNG có bảo vệ nhánh** (`GET …/protection` ⇒ **404**) ⇒ *"giữ nguyên
  `cong-15-buoc` làm required check"* (`D-50` ④) **chưa từng đúng**. ⛔ Chỉ bật `docker-build`
  (`app_id` 15368, `strict=false`); **không** bật `cong-15-buoc` vì nó **đỏ** do nợ **có sẵn**:
  `packageManager: pnpm@10.22.0` ở `package.json` gốc + `abp install-libs` ⇒ `yarn` 1.22.22 từ chối
  (`"yarn@pnpm@10.22.0"`). Nợ này **chưa có WP** — 3 hướng sửa **chưa chọn** (`handover-n16` §5 #1).
- ✅ **DoD đạt:** `docker-build` **xanh trên PR** + **required check đã bật** · `-DayDu` **exit 0 (19/19)** ·
  **0 migration** (59 vẫn mới nhất) · **0** test BE thêm.

**Đã xử lý nợ CI (`N17`, 05/11/2026) — [handover-n17](handover-n17.md), quyết định [D-60](decisions.md)/[D-61](decisions.md):**

- ✅ **Bỏ `packageManager` khỏi `package.json` gốc** — commit **`18405ca`** (**2** tệp: `package.json` +
  comment `Dockerfile`): Yarn **1.22** của `abp install-libs` đọc field từ `package.json` **gần nhất đi ngược
  lên** (`src/eCommerce.Web/package.json` **không** có field — đo được) và **từ chối** giá trị `pnpm@…`.
  Đo máy dev: `yarn --version` **exit 1 → exit 0** · `abp install-libs -wd src/eCommerce.Web` **exit 0** (10,2 s).
  🔒 pnpm **vẫn ghim**: `ci.yml` bước 6b + `Dockerfile` dòng 21 (`corepack prepare`). ⛔ **Không** bỏ bước 7
  khỏi `ci.yml` (mất phép kiểm thư viện client của `eCommerce.Web`).
- ✅ **Đo trên CI thật:** run [**`41955331290`**](https://github.com/hungnt9801/eCommerce/actions/runs/41955331290) —
  bước **7 SUCCESS** (trước: đỏ sau **38 s**, `yarn@pnpm@10.22.0`); `docker-build` run
  [**`41955331294`**](https://github.com/hungnt9801/eCommerce/actions/runs/41955331294) **xanh 6/6** (**66 s**).
- 🚨 **`ci` VẪN đỏ ở ⑦/Y1** (`test-cong.log`: **37/38**): **3 tệp probe** `probe-don-hang` · `probe-kho-fe` ·
  `probe-kho-wp04` là **mới so với `main`** và vẫn dùng API chỉ-Windows; bản sửa (`D-54`) nằm trong
  **cây làm việc chưa commit** ⇒ **merge PR #1 sẽ làm `ci` của `main` đỏ ở Y1** (main đang xanh — run
  `41929871748`).
- ⏭ **③ merge PR #1: KHÔNG làm** — **`D-61`** (thêm lý do `handover-n16` §5 #3: PR mang **nửa FE**, BE chưa
  commit ⇒ FE gọi API **404**). **Nhận chủ:** phiên **commit cây làm việc** (BE + `tools/` `N12`–`N14` + docs),
  rồi merge khi **đo** thấy `ci` xanh. 📌 Bật `cong-15-buoc` required **vẫn chưa** (bản `ci.yml` có bước FE
  đang chưa commit).
- ✅ **DoD phần được duyệt ĐẠT:** bản nhẹ **exit 0 (9/9, 228,5 s)** · `-DayDu` **exit 0 (19/19, 455,9 s)** ·
  test cổng **34/34** (full **40/40**) · DB **25/25** · **0 migration** (59 vẫn mới nhất) · **0** test BE thêm.

**Đã hợp nhất (`N18`, 05/11/2026) — [handover-n18](handover-n18.md), quyết định [D-62](decisions.md)/[D-63](decisions.md):**

- ✅ **③ merge PR #1: ĐÃ LÀM — sau khi commit cây và **đo** thấy `ci` xanh:** **6 commit · 160 tệp**
  (`6ab9114` BE `WP-06…WP-08` + **migration 57…59** · `4248d80` `ci.yml` + 2 job · `b94b13e` compose `admin-web`
  · `0bbc140` `tools/` `N12`–`N14` · `3f93c14` docs) ⇒ `ci` lượt **1** (head `3f93c14`)
  [**`41961196209`**](https://github.com/hungnt9801/eCommerce/actions/runs/41961196209) **ĐỎ** ⇒ sửa `39df2d4`
  ⇒ `ci` lượt **2** [**`41963379150`**](https://github.com/hungnt9801/eCommerce/actions/runs/41963379150)
  **3/3 job XANH (10,2 phút)** · `docker-build` [**`41963379197`**](https://github.com/hungnt9801/eCommerce/actions/runs/41963379197)
  **6/6** ⇒ **merge PR #1 = `fab0089`** (46 commit · **610** tệp).
- 🚨 **Lượt CI ĐẦU ĐỎ — 2 lỗi CHỈ lộ trên cây sạch** (cổng nghiệp vụ vẫn **19/19** ngay lượt đó, đo trong log:
  `TAT CA PHEP KIEM DAT (290,5 s)`): ① bước **11** `CONG 13` — **3** link `../../artifacts/*.png` (thư mục
  **gitignore** theo `README` §6) ⇒ clone sạch **không có** ⇒ `FAIL — 3 tham chieu chet`; ② job
  `e2e-playwright` — **7 passed / 1 failed** (`waitForEvent('download')` timeout; ảnh chụp trang:
  `Xuất CSV [disabled]` + *"Chưa chụp ảnh tồn kho lần nào"*) vì **DB TRỐNG**: job `ReportingAggregationWorker`
  chụp ảnh tồn kho **0 dòng** lúc Host khởi động **nhưng vẫn ghi mốc = hôm nay** ⇒ mọi lượt sau **bỏ qua**
  bước chụp ⇒ báo cáo tồn kho **rỗng cả ngày**.
- ✅ **Sửa MỘT chỗ mỗi lỗi:** `TongHopBaoCaoService` chỉ `GhiNhan` mốc tồn kho khi **`soAnhTonKho > 0`**;
  `kiem-link-tai-lieu.ps1` coi link vào **`artifacts/`** (bằng chứng ngoài VCS **đã khai báo**) là hợp lệ
  (đối xứng với `$boQua`) — ⛔ **không** nới cho link **trong** repo (ca **V5** canh đúng điều đó) · **2** ca
  mới `V4`/`V5` ⇒ test cổng **36/36** · `e2e/08` khẳng định khớp **đúng** câu chữ *"Chưa chụp ảnh tồn kho lần nào"*.
- ⏭ **`cong-15-buoc` required: KHÔNG bật** (**D-63**) — điều kiện `D-59` ③ đã hết hiệu lực (`ci.yml` có bước FE
  **đã** ở `main`, `ci` **đã** xanh) **nhưng** bật required check là **settings của repo**, phải là phiên riêng
  có đo **TRƯỚC/SAU**.
- ✅ **DoD đạt:** bản nhẹ **exit 0 (9/9)** · `-DayDu` **exit 0 (19/19, 436,8 s)** · test cổng **36/36** ·
  DB **25/25** · link **PASS (657 link)** · **0 migration** (59 vẫn mới nhất) · **0** test BE thêm.
- 🚨 **Nợ MỚI có số đo (`D-64`) — `probe-giu-cho-2instance` FLAKY trên CI:** cùng một cây mã (khác **chỉ** `.md`),
  run `41963379150` **17/17 DAT** vs run `41967295632` **14 DAT / 3 HONG** (`P12` `200/409`, `P13` **1** dòng,
  `P14` **1** exception) — hai request **chồng nhau** trên cùng `InventoryItem` ⇒ `DbConcurrencyException` ⇒
  **`409`** (đúng *"conflict, thử lại"*). ⛔ **Không** nới probe, **không** sửa trong `N18`; hai hướng phải **chọn**
  ở phiên `WP-05/06` kế (retry có giới hạn **hoặc** chấp nhận `409` + sửa kỳ vọng probe).

**Đã bật required check (`N19`, 05/11/2026) — [handover-n19](handover-n19.md), quyết định [D-65](decisions.md):**

- ✅ **Bật `cong-15-buoc` làm required check trên `main` — việc còn lại duy nhất của `D-63`** (phiên **không** mở
  WP mới vì `07` chưa có `§WP-14`). 📌 `N19` **chỉ** đổi **settings của repo** + docs: **0** tệp `.cs`/`.ps1`/`.ts`,
  **0** migration (59 vẫn mới nhất), **0** test BE thêm.
- ✅ **Đo TRƯỚC:** `GET …/branches/main/protection` ⇒ **200**, `contexts = ["docker-build"]` (`app_id` 15368),
  `strict=false`, `enforce_admins=false` — khớp `handover-n18` §7 (khác `N16`: lúc đó **404**).
- ✅ **Điều kiện "`ci` xanh trên `main`" ĐÃ THOẢ (đo trên đúng head `0e5c275`):** run
  [**`41969985282`**](https://github.com/hungnt9801/eCommerce/actions/runs/41969985282) **SUCCESS 3/3 job**
  (`cong-15-buoc` 04:47:39→04:56:50 · `openapi-drift` · `e2e-playwright`), check-run của head **4/4 success** — đây là
  lượt xanh **thứ hai** trên `main` (trước: run `41964732639` @ `fab0089`).
- ✅ **Bật + đo SAU:** `PUT …/protection` **cả object** (11 cờ + 1 context) ⇒ **200**; `contexts =
  ["docker-build","cong-15-buoc"]`, 10 cờ còn lại **không đổi** ⇒ không nuốt cấu hình. 🔒 Chỉ bật **1** job
  (⛔ không `openapi-drift`/`e2e-playwright` — ngoài phạm vi).
- ✅ **Chứng minh cổng có hiệu lực:** PR **#3** (docs-only) ⇒ `mergeable_state = blocked` khi 2 check còn pending
  — và 🚨 **lượt `ci` đầu của PR #3 ĐỎ thật** đúng `D-64` (`probe-giu-cho-2instance` **14/3**: `P12` `200/409`,
  `P13` **1** dòng, `P14` **1** exception) trong khi **cùng lúc** máy dev đo **17/0** ⇒ **merge bị chặn**. ⚠️
  `rerun-failed-jobs` ⇒ attempt 2 **bị HUỶ** khi push head mới (`concurrency.cancel-in-progress` của PR) ⇒ head
  cuối **`99addf3`** (**`41983348862`** + `41983348917`) **xanh** mới merge **`2d5ba1b`**. ⇒ **`D-63` ĐÓNG.**
  📌 **Đo SAU merge:** `ci` trên `main` (`2d5ba1b`) **xanh 3/3** (`41984368687`) + `docker-build` **success**
  (`41984368722`).
- 🚨 **Hệ quả đổi mức nghiêm trọng:** `D-64` (`probe-giu-cho-2instance` **flaky**: `17/17` vs `14/3` trên cùng cây
  mã) **từ nay chặn merge PR thật** — đo được ngay trong phiên: PR **chỉ đổi `.md`** tốn **1** lượt ĐỎ + **2** lượt
  huỷ + **1** lượt xanh. Phải chốt hướng xử lý trước khi tin cổng tuyệt đối; ⛔ không tắt required check để
  "merge cho xong". 📌 Bài học: **`rerun` ≠ `push`** — push huỷ lượt đang chạy.
- 📌 **Baseline build ghi lệch `N18`:** build **full** của `main` = **0 error / 9 warning** `CS8604` có sẵn
  (`Address` 6 · `Sales` 3); build **incremental** = **0/0**. Con số *"0/0"* ở các handover trước là **đầu ra của
  build không compile lại** — từ nay ghi rõ *"full"*/*"incremental"*.

**Đã chốt hướng xử lý `D-64` (`N20`, 05/11/2026) — [handover-n20](handover-n20.md), quyết định [D-66](decisions.md):**

- ✅ **Chốt hướng (b) của `D-64`:** `409` = **chồng request trên cùng `InventoryItem`, THỬ LẠI** — không phải 5xx,
  không phải im lặng, và DB **vẫn** giữ `Reserved ≤ OnHand`. ⛔ Không nới bất biến, không bật/tắt required check.
- ✅ **Repro TẤT ĐỊNH (máy dev) — lần đầu thấy tận mắt ca `D-64`:** một transaction `psql` khác giữ `InvItems` + đổi
  `ConcurrencyStamp` đúng lúc request chạy ⇒ **HTTP 409** `code = null` (*"…changed by another user…"*); log Host
  **1** dòng `AbpDbConcurrencyException` (**0** dòng `Unhandled exception`), stack tại
  `EfCoreRepository.InsertAsync(autoSave)` ← `GiuChoAsync` ⇒ 🚨 **đúng chỗ flush `_soCaiRepository.InsertAsync(but, autoSave: true)`,
  nằm NGOÀI `try/catch`**; **gửi lại cùng `idempotencyKey` ⇒ 200** + 1 dòng giữ chỗ, `OnHand/Reserved = 2.000/1.000`.
  📌 **Nguyên nhân gốc:** khoá phân tán nhả ở **cuối `GiuChoAsync`**, transaction checkout commit ở **cuối app service**.
- ✅ **Sửa MỘT tệp (probe, không phải mã nghiệp vụ):** `$SoLanThuLai = 3` + `ThuLaiNeu409` (gửi lại **đúng** request:
  cùng Host, cùng `idempotencyKey`) + `P14` tách *xung đột đã khai báo* ↔ *lỗi thật* + 1 dòng `ℹ️` báo số lần thử lại.
  **0** tệp `.cs` · **0** migration (59 vẫn mới nhất).
- ✅ **Chống "nới cổng để cho xanh":** ca **`Y7`** (`test-cong-kiem.ps1`) quét **chính** tệp probe, **hai chiều**
  (nới giới hạn lên **99** / xoá `Reserved <= OnHand` ⇒ phép quét **PHẢI ĐỎ**) ⇒ test cổng **36/36 → 37/37**.
- ✅ **Đo lại tỉ lệ:** probe **5 lượt trước khi sửa `17/0 ×5`** (máy dev **không** tái hiện flake — khe rất hẹp) ·
  **5 lượt sau khi sửa `17/0 ×5`** · bản **giả lập 409** (`tools/_tam-kiem/n20/`, **không commit**) ⇒ **17/0** +
  `ℹ️ THU LAI 409: 1 lần` ⇒ đường thử lại **chạy thật** trên API (không chỉ đọc mã).
- 🎯 **CI xác nhận ĐÚNG ca `D-64` — và đã được cứu:** lượt `ci` của PR **#5** (run **`42002078789`**, head `6c1fb5c`)
  ⇒ artifact `giu-cho-2inst.log`: **`xung dot trong log A = 0 · B = 1`** · **`409 da thay = 1`** · `ℹ️ THU LAI 409:
  **1** lan` · `P12 = 200/200` · `P13` **2** dòng · **17/0** ⇒ Host B **thật sự** vấp `AbpDbConcurrencyException`
  đúng như `D-64` ①, probe vẫn **xanh** nhờ **1** lần thử lại. Cổng trên CI **19/19 (293,9 s)** · `docker-build` success.
- ✅ **Giữ 2 required check đúng cấu hình đã đo:** `GET …/branches/main/protection` ⇒ `contexts =
  ["docker-build","cong-15-buoc"]`, `app_id` **15368**, `strict=false`, `enforce_admins=false`, 8 cờ còn lại
  `false`/`null`; `ci` trên `main` (head `25d2bd5`) **4/4 check success**. ⛔ **Không** `PUT`, không đổi cờ nào.
- ⚠️ **Sau merge, `ci` trên `main` ĐỎ (KHÔNG do `N20`) — nợ mới `D-67`:** run `42005307510` (head `b1955989`, tree
  **`a64d69a3`** = tree của PR head `e9f5942`) đỏ ở bước **⑭** `probe-don-san` `C10` (`tong = 0 · Late = 0`, trong khi
  `C11…C14` + `C20…C35` DAT — sự kiện **đã** vào DLQ): attempt **1 ĐỎ** · **2 ĐỎ** · **3 XANH** ⇒ `main` **xanh**, nhưng
  `C10` **flaky `2/3`**. ⛔ **Không** nới `Cho-DieuKien` (30 s) / bỏ ca; nhận chủ: phiên có `§WP-14`.
- ⚠️ **Nợ còn lại (có nhận chủ):** `409` vẫn `code = null` (map mã nghiệp vụ = phiên có `§WP-14`);
  `openapi-drift`/`e2e-playwright` vẫn **không** required (`D-65` ⑤ giữ nguyên).

### WP-14 — GĐ3: cổng tài liệu — rà theo mô hình Nhanh + duyệt (3 phiên, mở 14/11/2026 theo `D-71`)

> 📌 **Vì sao có WP này (không phải code):** `D-69` chốt lộ trình chính là `05` + `07`; `D-70` chốt phạm vi GĐ3.
> Bảng ánh xạ [`anh-xa-lo-trinh-d69.md`](../agents/anh-xa-lo-trinh-d69.md) §2 đo được: **mọi** việc GĐ3 dựa trên tài liệu
> **chưa duyệt** (27/55 duyệt — không tài liệu GĐ3 nào trong đó), mà `implementation-plan.md` §A.5 (còn hiệu lực theo `D-69` ③)
> **cấm code** khi tài liệu chưa duyệt. Thêm nữa, các tài liệu này viết **trước** `D-07`/`D-08`/`D-18`/`D-27`/`D-31`/`D-45`
> (mô hình website: `ProductVariant`, 7 trạng thái đơn, không POS, tồn một số) ⇒ duyệt nguyên văn sẽ **mâu thuẫn code hiện có**.
> ⇒ WP-14 làm cho GĐ3 **được phép bắt đầu code**. ⛔ **0 tệp `.cs`, 0 migration** trong WP này.

**Phạm vi — 18 tài liệu, chia 3 đợt (mỗi đợt một phiên, duyệt được theo đợt — §A.5 cho duyệt theo từng feature):**

| Đợt | Phiên dự kiến | Tài liệu | Người review thêm (luật `design-doc-guidelines.md` §6) |
|---|---|---|---|
| **A** — Vận chuyển, COD, địa chỉ, trả hàng đơn | `N22` | `ADDR-001…003` · `SHP-001…004` · `REC-003` · `SAL-006` (9) | người **đã liên hệ GHN/GHTK** (tích hợp NCC) · kiến trúc (`SAL-006`, `SHP-002` có state machine) |
| **B** — Khuyến mại, điểm | `N23` | `PRO-001` · `PRO-002` · `LOY-001` (3) | kiến trúc (engine điều kiện) |
| **C** — Báo cáo, kế toán, thông báo | `N24` | `REP-001…003` · `ACC-001` · `NOT-001` · `NOT-002` (6) | **kế toán** (`ACC-001`, `REP-002` lãi gộp) |

📌 `PRC-002` (giá bậc thang) **đã duyệt** ở `Q5` ⇒ **không** nằm trong WP này. `REC-002`, `CHN-*` thuộc **GĐ5**. `REV-001` **bỏ**,
storefront còn lại **đóng băng** (`D-70`).

- **① Đối chiếu mô hình Nhanh** — mỗi tài liệu thêm mục **"Đối chiếu mô hình Nhanh (14/11/2026)"** ở cuối: bảng *chỗ lệch* với
  `D-07` (cha–con + ĐVT) · `D-08` (4 trục, 18 trạng thái) · `D-18`/`D-42` (POS riêng) · `D-27` (tồn vector) · `D-31` (`decimal(18,3)`) ·
  `D-45` (một cửa ghi điểm/công nợ) · `D-47`/`D-48` (read-model báo cáo) · căn cứ pháp lý mới (`business-modules-proposal.md` §6.7),
  rồi **sửa thân tài liệu** cho hết lệch. Không lệch ⇒ ghi "đã đối chiếu, không lệch" (không để trống).
  📌 Code đã có một phần (`SAL-006` 24 tệp, `ADDR` 76 tệp, `REP` từ `WP-08`, `LOY` từ `WP-07`) ⇒ tài liệu phải mô tả **code thật**
  kèm `file:dòng`, phần chưa có ghi rõ là kế hoạch (`coding-conventions.md` §12).
- **② Kiểm tài liệu độc lập** — người/agent **khác** người sửa (vai `DOCREV` khi đã dựng): 6 nhóm D1–D6 + **D7** = 14 quy tắc của
  `design-doc-guidelines.md` §5; ghi `docs/reviews/<MÃ>-docreview.md` có `VERDICT: DOC-OK | DOC-CHANGES`. SQL trong tài liệu chạy thử
  trong `BEGIN … ROLLBACK` nếu có PostgreSQL; không có ⇒ ghi "chưa chạy" (không bỏ qua im lặng).
- **③ Câu hỏi mở** của từng tài liệu: qua agent `decider` trước (`CLAUDE.md` §5); câu vùng cấm (NCC, pháp lý, chính sách đổi trả với
  khách) ⇒ gom vào `docs/agents/hang-doi-xem-lai.md`, **không** chặn các tài liệu khác của đợt.
- **④ Duyệt** — chủ dự án (+ người review thêm ở bảng trên) duyệt theo đợt; ghi **biên bản duyệt mới** cùng khuôn
  `docs/bien-ban-duyet-gd1-2026-10-29.md` (đặt tên `docs/bien-ban-duyet-gd3-<đợt>-<ngày>.md`), **không** sửa header từng tệp (cùng
  quy ước `Q5`); cập nhật `docs/features/README.md` §2 (số tài liệu đã duyệt).
- **⑤ Sau đợt cuối:** đề xuất thứ tự WP **code** của GĐ3 (`§WP-15+`) — thứ tự trong phạm vi `D-70` là việc `decider` được chốt tạm;
  thêm việc ngoài phạm vi ⇒ hỏi người.

⛔ **Không thuộc WP-14** (giữ nhận chủ, chuyển sang **WP code đầu tiên của GĐ3 — `WP-15`**): sửa `C10` của `D-67` · map mã nghiệp vụ cho
`409` (`code = null`). Hai việc này cần build/test chạy được — ⚠️ đo 14/11/2026: `dotnet build` trên máy dev **đỏ `MSB3491`** (không ghi
được `obj\`, thư mục thuộc `BUILTIN/Administrators`) ⇒ phải sửa quyền trước `WP-15`. **WP-14 không cần build** nên không bị chặn.

**DoD (mỗi đợt, đo được):**
- Mỗi tài liệu của đợt: đủ **16** heading `## 0.` … `## 15.` (`grep`) · có mục **"Đối chiếu mô hình Nhanh"** · có
  `docs/reviews/<MÃ>-docreview.md` với `VERDICT: DOC-OK` và **0 P1**.
- `pwsh -File tools\kiem-link-tai-lieu.ps1` → **PASS** · `npm --prefix tools run check:mermaid` → **0 lỗi**.
- Biên bản duyệt của đợt có **một dòng cho mỗi tài liệu** (duyệt / chưa duyệt + lý do).
- `git diff --stat` **chỉ** chạm `docs/**` · **0 tệp `.cs`** · **0 migration**.
- **Toàn WP:** 18/18 tài liệu có trạng thái cuối trong biên bản; `§WP-15+` đã có đề xuất thứ tự.

### WP-15+ — đề xuất thứ tự WP code GĐ3 (bước ⑤ của WP-14)

> **`D-209`** (DEC, uỷ quyền `D-114`/`D-70`, phiên `N64`): thứ tự dưới là **tạm chốt** trong phạm vi `D-70` theo `07` :721.
> **Quy tắc chạy:** WP có cổng **"chưa"** ⇒ **chỉ xếp lịch, không code**; dây chuyền chuyển sang WP kế tiếp có cổng đạt.
> WP khác module được song song theo [`09-parallel-sessions.md`](09-parallel-sessions.md) (DB + cổng riêng, **một chủ migration**).
> Merge `main` vẫn cần **biên bản người ký** + cổng OPS (`D-115` ⑤). Đổi thứ tự = dòng `D-xx` mới thay `D-209` + sửa mục này.

| WP | Phạm vi | Cổng tài liệu (điều kiện vào) | Phụ thuộc |
|---|---|---|---|
| **WP-15** | **Nền GĐ3 + 2 nợ `D-71` ④**: sửa `C10` của `D-67` (đo `Late` **sau** khi DLQ có — khuôn `D-68`; ⛔ không nới 30 s, không bỏ ca) + map mã nghiệp vụ cho `409` (`code = null` ⇒ **một** mã chung qua **một** exception filter ở Host, khuôn `BoLocViPhamRangBuocDb`) | Không có tài liệu feature. Vào được khi build/test chạy (gỡ `MSB3491` = việc người/máy dev) | — |
| **WP-16** | Bảng giá GĐ3 + giá bậc thang (`PRC-002` phần còn thiếu) | `PRC-001`/`PRC-002` **đã duyệt `Q5`**; câu mới ⇒ decider | WP-15 |
| **WP-17** | Giá vốn: **đường ghi + duyệt + động cơ bình quân gia quyền di động** (`PRC-001` §20; `D-177` ④–⑦, `D-186`) — **bắt buộc trước `WP-32`** | `PRC-001` duyệt `Q5`; BA sửa `prc-001` §15.1/§20.4 + `inv-001` §0 + `rep-003` §0/Q11 **cùng PR** (`D-186` ⑥); **M4** trước release | WP-15 |
| **WP-18** | `ADDR-001` + `ADDR-002` | `D-141` từng dòng: DOCREV kiểm nhanh v0.5 đóng 2 P2 **mỗi** tài liệu; mã GHTK không code bằng suy đoán (R3-1); `ADDR-003` + PII **ngoài** WP | WP-15 |
| **WP-19** | `ADDR-003` phần thiết kế (**trừ** PII `freeText`) | **"chưa"** — BA sửa P2 #1 (`D-107`) + #2 (TC-36) → DOCREV `DOC-OK` → **dòng DEC sau**; PII (R3-5) **chặn release** | WP-18 |
| **WP-20** | `SHP-001` adapter GHN (module `ShippingHub` mới) | `D-141` dòng `SHP-001`: BA sửa P2 #45/#47 + áp `D-129`/`D-130`/`D-131` cùng PR `SHP-004` P2 #1 → DOCREV kiểm nhanh; sandbox GHN/OTP = người (R3-2), API production **cấm** (M5) | WP-18 |
| **WP-21** | `SHP-002` webhook trạng thái vận đơn | `D-141` dòng `SHP-002`: sửa #1 (không dùng lại UoW đã abort `25P02`) + #2 backfill `StatusSource` → DOCREV; **P10**: controller `[AllowAnonymous]` **chờ người**; P4 chặn release | WP-20 |
| **WP-22** | `NOT-001` template + hàng đợi (module `Notification` mới, `D-197`) | `D-208` dòng `NOT-001`: sửa **2 P2** #24/#26 → DOCREV kiểm nhanh **trước** `migrations add`/state machine; 15 P3 cùng WP | WP-15 |
| **WP-23** | `NOT-002` kênh gửi — **thiết kế trung lập** | `D-208` dòng `NOT-002`: sửa P2 #1 + P3 → DOCREV; `Q1`/`Q3` + HMAC production **ngoài** tạm duyệt (R3-6) ⇒ chỉ hợp đồng adapter + `fake` sender + fail-closed `0003` | WP-22 |
| **WP-24** | `PRO-002` engine điều kiện (module `Promotions` mới; `Add_Promotion_Module` là **nguồn schema duy nhất**, `D-151`) | `D-169` dòng `PRO-002`: điều kiện "(sau P3)" **đã đạt**; 2 P3 cùng WP; Q8 chỉ chặn nhánh điều kiện địa chỉ (R3-1) | WP-16 |
| **WP-25** | `PRO-001` voucher/coupon (trên engine `WP-24`) | `D-169` dòng `PRO-001`: sửa **P2 #20** đúng hướng `D-169` (**cấm** gán `UsageSequence = 1`) → DOCREV kiểm nhanh; P3 cùng WP; **M4** trước release | WP-24 |
| **WP-26** | `REP-001` read-model + job + khoá kỳ (`RepPeriodClose`) | `D-208` dòng `REP-001`: tạm duyệt có điều kiện, điều kiện đỏ **đã đạt**; 10 P3 cùng WP; **không** đụng cột giá vốn | WP-15 |
| **WP-27** | `SHP-004` adapter GHTK | **"chưa"** — R3-1 (trả lời GHTK Q1) + 2 P2 (cùng PR `WP-20`) + DOCREV `DOC-OK` → **dòng DEC sau**; không bảng Submission riêng (`D-128`) | WP-20 |
| **WP-28** | `SHP-003` COD thu hộ / đổi COD | **"chưa" — đỏ cứng** §2.1 (người + kế toán ký); sửa 2 P2 trước khi trình; kế toán điền ngưỡng/trần + "người duyệt ≠ người yêu cầu" | WP-20 |
| **WP-29** | `REC-003` đối soát COD | **"chưa" — đỏ cứng**; P2 #1 cần **một câu DEC chọn-một**; file bảng kê thật (R3-4) trước WP code; `D-124`/`D-125` M4 | WP-28 |
| **WP-30** | `SAL-006` RMA đơn giao vận | **"chưa" — đỏ cứng**; 2 P2 + 5 P3; Q17 (đổi nghĩa `SAL-003`) người quyết trước WP code; `D-136`/`D-138` M4 | WP-20, WP-21 |
| **WP-31** | `LOY-001` điểm thưởng (phần còn lại) | **"chưa"** — người ghi "P13 đã duyệt" → BA sửa **P2 §11** (unique `(TenantId, RetailBillId)`/`(TenantId, OrderId)`, `NULLS NOT DISTINCT`) → DOCREV → **dòng DEC sau**; P2 §11 **trước** `migrations add` bảng `Loy*` | WP-15 |
| **WP-32** | `REP-002` doanh thu + lãi gộp | **"chưa" — đỏ cứng**; 5 P3; **bắt buộc** sau `WP-17` (`D-177` ⑦/`D-186` ⑦) + `WP-26` | WP-17, WP-26 |
| **WP-33** | `REP-003` tồn kho + hiệu suất kênh | **"chưa" — đỏ cứng**; 4 P3; thiếu giá vốn ⇒ `NULL` + `CostMissing`, **cấm `0`** | WP-17, WP-32 |
| **WP-34** | `ACC-001` xuất liệu kế toán | **"chưa" — đỏ cứng** (R3-3/R3-8); 5 P3; `Q12` (kế toán trưởng) không chặn xuất file; chỉ **tiêu thụ** `IReportFactReader` (`D-183`) | WP-32, WP-33 |

**Phủ `D-70`:** `PRO-001/002` = 24/25 · `SHP-001…004` = 20/21/27/28 · `REC-003` = 29 · `SAL-006` = 30 · `NOT-001/002` = 22/23 · `ACC-001` = 34 · `REP-*` = 26/32/33 · `LOY-001` = 31 · `PRC-002` = 16 · `ADDR-001…003` = 18/19 — **không** thêm/bỏ mục nào.
**Cổng máy còn thiếu** (`D-209` ⑥): "cổng tài liệu chưa đạt ⇒ cấm code WP" + "merge `main` khi biên bản chưa ký" (`D-115` ⑤) — **OPS** dựng trước `WP-15`; **QA** thêm ca quét mục này với 3 biên bản (mã nào "chưa" mà có WP đang code ⇒ ĐỎ).

---

## 6. Lịch sprint (2 tuần/sprint)

| Sprint | Tuần | Nội dung | Demo cuối sprint |
|---|---|---|---|
| **S0** | 1–2 | WP-01, WP-02, WP-10 (phần env), spike multi-tenant 3 ngày, cài REA (nếu nâng Node) | Đăng nhập → shell 180 mục (lọc quyền) → DS sản phẩm đọc API thật |
| **S1** | 3–4 | WP-03 (catalog + backfill) + FE list/form sản phẩm | Tạo SP 3 ĐVT + IMEI + lô; danh sách 20 cột chạy |
| **S2** | 5–6 | WP-04 (tồn 9 cột + phiếu + posting) + FE kho | Nhập 10 → bán 3 → tồn/khả dụng đúng; Post lại bị chặn |
| **S3** | 7–8 | WP-05 (18 trạng thái + transition) + FE đơn | Đơn đi trọn vòng đời; tab "Cần xử lý" đúng |
| **S4** | 9–10 | WP-06 (POS + in + offline) | Bán 3 SP, chia 2 phương thức, in, kết ca |
| **S5** | 11–12 | WP-07, WP-08, WP-09, hardening | Khách 5 tab; Dashboard khớp doanh thu; E2E 8 luồng xanh |

**Nhịp mỗi sprint:** thứ 2 chốt phạm vi → thứ 4 demo giữa sprint → thứ 6 (tuần 2) demo + retrospective
+ cập nhật `decisions.md`.

---

## 7. Mười ngày đầu (khi bạn duyệt)

| Ngày | Việc | Đầu ra kiểm được |
|---|---|---|
| 1 | `git switch -c feature/wp-01-02-foundation`; chạy baseline (build + test + 2 cổng tài liệu), ghi số | File baseline tạo trong thư mục này (số đo + lệnh + thời điểm) |
| 2 | `pnpm-workspace.yaml`, `package.json` gốc, scaffold `apps/admin-web`, Tokens + Tailwind | `pnpm dev` mở được trang trắng có token đúng |
| 3 | `Sidebar` + `Topbar` + `BusinessSwitcher` + `middleware` redirect | Shell hiển thị, route `/{business}` chạy |
| 4 | Auth.js + OpenIddict (dev http 54275) + cookie + `lib/api` BFF | Đăng nhập thật, gọi `/api/v1/ping` 200 |
| 5 | Project `eCommerce.HttpApi` + `ProductsController` 3 endpoint + phân trang | Swagger có `/api/v1/products` |
| 6 | `packages/api-client` + `pnpm gen:api` + CI drift | `schema.d.ts` sinh ra, sửa DTO ⇒ CI đỏ |
| 7 | FE: `DataTable` + `FilterBar` + `PageTabs` dùng chung | Storybook/màn demo 1 bảng 20 cột giả |
| 8 | Nối DS sản phẩm với API thật + phân trang + sắp xếp + lọc | Danh sách 108 SP thật của bạn hiển thị |
| 9 | WP-03 bắt đầu: enum + `ProductUnit` + migration 51 (additive) | `dotnet ef migrations add` + build xanh |
| 10 | Backfill dry-run + log đối chiếu; demo nội bộ | `n variant → n child`, chưa ghi DB thật |

---

## 8. Chiến lược migration DB (quan trọng nhất để không mất dữ liệu)

```
Bước 1  ADDITIVE      thêm bảng/cột/enum — không đổi cột cũ        (migration 51–56)
Bước 2  BACKFILL      script idempotent, có --dry-run + log đối chiếu
Bước 3  DUAL READ    API mới đọc model mới; Razor đọc qua adapter cũ
Bước 4  SWITCH       tắt adapter khi màn Razor tương ứng đã bị thay
Bước 5  CLEANUP      (GĐ3) xoá cột/bảng legacy — chỉ sau khi có bằng chứng không còn đọc
```

| Migration | Nội dung | Bảng chạm | Backfill |
|---|---|---|---|
| 51 | Catalog Nhanh-model | `CatProducts`, `CatProductUnits`… | variants → child products |
| 52 | Inventory buckets + mode | `InvItems`, `InvStockDocuments` | `InvItems` từ `InvStockLedger` |
| 53 | Order 18 trạng thái | `OrdOrders`, `OrdOrderLines` | `ProductId` theo `Sku` |
| 54 | POS | `PosRetailBills`… | không |
| 55 | Customer level/point/debt | `CusCustomers` | điểm/công nợ = 0 |
| 56 | Reporting read-model | `report.*` | job backfill 90 ngày |

**Luật:** mỗi migration phải chạy được trên **DB có dữ liệu thật** (108 SP + đơn hiện có), không chỉ DB rỗng.
Trước khi merge: `dotnet run --project src/eCommerce.DbMigrator` trên bản copy DB.

---

## 9. Chiến lược test & cổng

| Lớp | Công cụ | Bắt buộc cho |
|---|---|---|
| Domain invariant | xUnit (đã có) | mọi luật tồn/tiền/trạng thái mới |
| Application | xUnit + fixture | mỗi app service mới ≥ 3 ca (happy, invalid, permission) |
| API contract | integration + `WebApplicationFactory` | mỗi endpoint `/api/v1` ≥ 1 ca 200 + 1 ca 401/403 |
| UI unit | Vitest + RTL | `MoneyInput`, `DataTable` (sort/select), `OrderStatusBadge` |
| E2E | Playwright | 8 luồng ở WP-09 |
| Tài liệu | `kiem-link-tai-lieu.ps1` + `check:mermaid` | mỗi PR có sửa `.md` |
| OpenAPI drift | CI | mỗi PR sửa DTO/controller |

**Định nghĩa "xanh"**: `dotnet build` 0 warning · `dotnet test` ≥ baseline · `pnpm lint && pnpm test` ·
2 cổng tài liệu · `pnpm gen:api` không diff.

---

## 10. Rủi ro thi công & điểm dừng (go/no-go)

| # | Rủi ro | Dấu hiệu sớm | Xử lý |
|---|---|---|---|
| 1 | Backfill variant sai | log `n variant ≠ n child`, SP mất tồn | **Dừng**, chạy `--dry-run`, đối chiếu 20 SP mẫu bằng mắt |
| 2 | Đổi enum làm lệch nghĩa dữ liệu cũ | test cũ đỏ hàng loạt | Chỉ **append**; nếu buộc đổi ⇒ bảng map + migration dữ liệu riêng |
| 3 | EF migration khoá bảng lâu | `ALTER TABLE` > 5s trên DB thật | Thêm cột nullable/default; backfill theo lô 5.000 dòng |
| 4 | Next.js auth lệch cookie/HTTPS | đăng nhập xong quay lại login | Dùng http dev (D-16), kiểm `AUTH_URL`, `trustHost` |
| 5 | POS offline tạo trùng hoá đơn | 2 bill cùng `ClientBillId` | Unique index + test gửi 2 lần |
| 6 | Scope trôi (thêm màn ngoài P0) | backlog tăng giữa sprint | Không nhận; ghi vào GĐ3 |
| 7 | Test giảm | số test < baseline | PR bị chặn; giảm phải có lý do + người duyệt |

**Checkpoint (dừng nếu trượt > 1 tuần):**

| Cuối | Phải có | Nếu trượt |
|---|---|---|
| S0 | Login + 1 list thật + OpenAPI | Cắt WP-10, dồn FE |
| S2 | Posting kho đúng + test bất biến | **Cắt WP-08 (báo cáo)** và WP-06 offline, giữ P0 lõi |
| S3 | Vòng đời đơn chạy | Cắt WP-07 (khách) sang GĐ3 |
| S4 | POS bán + in được | Không cắt — đây là doanh thu |

---

## 11. Ngoài phạm vi GĐ0–GĐ2 (không nhận trong 12 tuần)

Kế toán bút toán kép · HĐĐT UI · khuyến mại engine · vận chuyển/đối soát COD · sàn TMĐT ·
bảo hành · website builder · affiliate · báo cáo ngoài 3 cái · app mobile · đa tiền tệ.

*(Đã có trong GĐ3–GĐ5 của [05](05-lo-trinh-rui-ro.md); không đưa vào sprint dù "chỉ 1 ngày".)*

---

## 12. Checklist "được phép bắt đầu"

- [x] D-01…D-18 đã chốt ([decisions.md](decisions.md)); D-19…D-41 phát sinh trong thi công `N2…N6`
- [x] Kiến trúc đích + thiết kế Web Admin đã viết ([03](03-kien-truc-dich.md), [04](04-web-admin-nextjs.md))
- [x] Cổng tài liệu xanh (link + Mermaid)
- [ ] **Bạn duyệt plan này** (mục 4–7)
- [ ] Docker Desktop chạy (PostgreSQL + Redis) để chạy baseline `-DayDu`
- [ ] Bạn xác nhận: đội **3 người** hay **1 người** (để tôi chỉnh lịch)
- [ ] Bạn xác nhận môi trường dev: dùng **http 54275** cho BFF (D-16) — hay cấp cert tin cậy

> Khi 4 ô cuối được tick, tôi bắt đầu **ngày 1** ở mục 7 và báo lại bằng số đo, không bằng lời.

---

## 13. Tiến độ thực tế

| Ngày | Việc | Kết quả đo | Ghi chú |
|---|---|---|---|
| 02/11/2026 | Baseline + WP-02 (API v1) + WP-01 (nền FE, login, DS sản phẩm) | build 0 error/9 warning · **1634/1634** test · `next build` ✓ · login thật → trang 200 · 3 endpoint `/api/v1` chạy | Chi tiết + 3 điểm lệch: [dev-log-wp01-02.md](dev-log-wp01-02.md) |
| 02/11/2026 | **WP-03 nửa đầu (`N2`)** — enum `ProductType`/`VatMode`/`ProductImeiStatus`, 10 cột `Product`, 6 entity (ĐVT/IMEI/lô/combo/gói/nhãn), **migration 51** additive, `POST /api/v1/products` | **1671/1671** test (+37) · build **0 error/9 warning có sẵn** · NND **64 → 72** · **201** tạo SP **3 ĐVT** + `GET` 200/3 units · barcode trùng & ĐVT gốc thứ hai bị **DB chặn** (`23505`) · `schema.sql` +232 dòng | Chi tiết + nợ/rủi ro: [handover-n2.md](handover-n2.md). **Chưa** backfill, chưa FE form, chưa `OrderLine.ProductId` |
| 02/11/2026 | **WP-03 nửa sau (`N3`)** — backfill biến thể → sản phẩm con (**dry-run → thật**, bảng đối chiếu `CatVariantMaps`), `OrderLine.ProductId`, `Publish()` theo ĐVT gốc, luật *"cha không bán được"* + chống chu trình, adapter đọc cũ, **migration 52** | **1684/1684** test (+13) · build **0 error/9 warning có sẵn** · NND **73** · backfill: dev DB `0 variant → 0 child`; bản sao có 2 biến thể: dry-run **không ghi**, chạy thật `2 → 2`, **chạy lần 2 `2 → 0`** (idempotent), xung đột ⇒ **ABORT + exit 1** · `/SanPham` **200** cho cả mô hình cũ và mới · `schema.sql` +272 dòng (so `HEAD`) | Chi tiết + 9 nợ/rủi ro: [handover-n3.md](handover-n3.md). **Chưa** FE form, chưa API ghi IMEI/lô/combo/gói/nhãn |
| 02/11/2026 | **WP-04 phần BE (`N4`)** — `InvItems` **+8 chiều tồn** + `Available`/`InWarehouse`; `StockDocument` `Direction`/`Mode`/đối tác/nguồn/tiền; dòng phiếu `UnitId`/`VatPercent`/`BatchId`/`ImeiSerials`; **11 loại biến động** mới (11…21); bút toán theo **`(mode, chiều)`**; unique **`IdempotencyKey` NND**; **migration 53**; API **`/api/v1/stock/{balances,bills}`** (6 đường); backfill `InvItems` từ sổ cái + probe 12 ca | **1768/1768** test (+84) · integration **90/90** · build **0 error/9 warning có sẵn** · NND **73 → 74** · probe HTTP: tạo phiếu 10 → post → `OnHand=10/Available=10`; post lại **403 `0010`** (tồn **không** đổi); huỷ phiếu đã ghi sổ **403 `0013`**; trùng `idempotencyKey` **403 `0035`**; bán 15 > tồn 10 **403 `0001`**; bất biến sổ cái **10 = 10** · backfill: mutation `OnHand += 5` ⇒ phát hiện **1 lệch**, `-Apply` ⇒ **0 lệch**, chạy lại **0** · `schema.sql` +336 dòng (0 xoá) · OpenAPI **12/12 path** | Chi tiết + 7 nợ/rủi ro: [handover-n4.md](handover-n4.md). **Chưa**: FE màn phiếu (`N5`), `decimal` hoá số lượng (D-25 → WP-04b), 4 chiều chưa có đường ghi (WP-05/POS) |
| 02/11/2026 | **WP-04b (`N4b`) — HOÀN TẤT WP-04 (BE)**: `int → decimal(18,3)` cho **16 cột số lượng** (`InvItems` ×10, `InvStockLedger` ×2, `InvStockDocumentLines.Quantity`, `ReconRunMismatches` ×3) + toàn bộ entity/DTO/hàm thuần; **migration 54** | **1773/1773** test (+5 ca số lẻ) · integration **90/90** · build **0 error/9 warning có sẵn** · NND **74 (không đổi)** · `OnHand` thật trên DB = `numeric/18/3` · probe **12/12** (`onHand: 10.000`, bất biến **10.000 = 10.000**) · `schema.sql` **+374 dòng, 0 xoá** · `quantity?: number` trong `schema.d.ts` · backfill 0 lệch | **WP-04 (BE) ĐÓNG** (07 việc 1–6). `RowVersion` **loại có lý do** (D-32). Chi tiết: [handover-n4b.md](handover-n4b.md) |
| 03/11/2026 | **WP-04 phần FE (`N5`) — HOÀN TẤT WP-04 (việc 7)**: 4 màn Next.js — `inventory/bill` (tab badge + lọc server-side), `inventory/bill/new` (chọn kiểu → form động, ProductPicker, `IdempotencyKey` client), `inventory/bill/[id]` (ghi sổ/huỷ + tồn hiện tại), `product/inventory` (tồn 9 chiều, cột tồn 3 số); type lấy từ `schema.d.ts` | **probe FE 11/11** (`tools/probe-kho-fe.ps1`, `exit 0`): đăng nhập NextAuth → trang **200** → `POST /api/proxy/v1/stock/bills` **201** → post **200** (`postedEntries=1`) → post lại **403 `0010`** → UI hiện *"Đã ghi sổ"* + **`Bán 10`** sau khi ghi (trước: *"chưa có dòng tồn"*) · `pnpm build` ✓ (4 route mới, First Load **106–112 kB**) · `tsc --noEmit` ✓ · unit **1773/1773** · integration **90/90** · build **0 error/9 warning có sẵn** · OpenAPI **12/12 path**, `schema.d.ts` **không đổi** (0 dòng diff) · probe BE **12/12** | **WP-04 ĐÓNG (BE + FE)**. Không migration mới. `D-33` (hợp đồng FE) + `D-34` (hoãn "thành tiền" — dòng phiếu không có đơn giá). Chi tiết: [handover-n5.md](handover-n5.md) |
| 03/11/2026 | **WP-04 (FE) — TIỀN (`D-37`, thay thế `D-34`)**: `StockDocumentLine.UnitPrice numeric(18,2)` + hàm thuần `TinhTienChungTu` + DTO `Amount/VatAmount/TotalAmount/TotalVat/TotalPayment` + UI 4 màn (form · chi tiết · DS · in), **migration 55** | **1785/1785** test (+12) · integration **90/90** · build **0 error/9 warning có sẵn** · `schema.sql` **+10 dòng** · cột thật `numeric/18/2` + `CHECK` có trên DB (migration **55**) · NND **74 (không đổi)** · probe FE **19/19** (`exit 0`): `totalAmount 250.000`, `totalVat 20.000`, `totalPayment 270.000`; dòng **không giá ⇒ `null`**; đơn giá âm ⇒ **400** (lỗi tại `Lines[0].UnitPrice`) · **browser thật**: form 3 × 25.000 + 8% ⇒ **75.000 / 6.000 / 81.000**, lưu xong chi tiết y hệt | Luật 4 điểm ở `D-37`. VAT theo giả định **giá chưa gồm VAT** (`VatMode` là việc WP-06). Chi tiết: [handover-n5.md](handover-n5.md) |
| 03/11/2026 | **WP-04 (FE) — phần còn lại**: **khảo sát Nhanh** (cầu CDP, `businessId=230222`) rồi bổ sung **(a) bulk** `Thao tác`: ghi sổ/huỷ nhiều phiếu · xuất CSV · in phiếu, **(b) ẩn/hiện cột** + **lưu bộ lọc** (localStorage), **(c) route in phiếu**, **(d) `?kind=`** cho form (chuyển kho/kiểm kho mở đúng kiểu), nhãn lọc theo Nhanh (`Kho hàng · Loại · Kiểu · Ngày · Lọc`) | Khảo sát đo được: **6 route**, DS **7 cột** (`ID|Ngày · Kho hàng · SP · SL · Tổng tiền thanh toán · Người tạo · Khách hàng`), `Thao tác ▾` **5 hành động**, form nhập/xuất **4 cột dòng + 7 trường tiền** ([01 §4.2b](01-khao-sat-nhanh-vn.md)) · **Browser thật (CDP)**: chọn 2 phiếu → **"2 thành công / 0 lỗi"** (mỗi phiếu *1 bút toán*), tab Nháp **6 → 4**; chọn phiếu đã ghi sổ → **"0 thành công / 1 bỏ qua"** (không gọi API) · CSV **5 dòng** (header + 4), `;`, có BOM · ẩn cột *"Chứng từ"* ⇒ header mất + `nh:bill-cols` ghi **7 khoá** · probe FE **15/15** (`exit 0`) · build ✓ **12 route** · unit **1773** · integration **90** | `D-35` (bulk gọi API tuần tự; `bỏ qua` ≠ `lỗi`) + `D-36` (in/cột/bộ lọc ở FE). **Chưa**: "thành tiền" (D-34 — cần cột giá ở BE), mẫu in cấu hình (#18), màn kiểm kho riêng (#9) |
| 03/11/2026 | **WP-05 (`N6`) — Order 18 trạng thái**: append **9** `FulfillmentStatus` (→ **18**), enum `OrderType` **10** / `OrderSource` **14** / `OrderReasonCode` **24** (số đo được), 11 cột `Order` (loại/nguồn/vận chuyển/COD/phí ship/cân nặng/kho/NV), bảng cạnh **mở rộng theo Nhanh** + **15 cạnh huỷ**, nhãn hiển thị suy từ 4 trục, **`HuyDonNhanh`** (2 trục trong 1 lời gọi), API **`/api/v1/orders`** (DS + **14 tab đếm** + chi tiết + `transitions` + `cancel` + `history`), **migration 56**; FE 2 màn (DS 14 tab + grouped rows, chi tiết tab động + đổi trạng thái/huỷ) | **probe `tools/probe-don-hang.ps1` 29 lời gọi HTTP, `exit 0`**: `Mới → … → Thành công` **8/8 bước 200**; xác nhận ⇒ `stockEffect=ChotGiuCho`, `OnHand 10→8`, `Reserved 3→1`; hoàn ⇒ `NhapHangHoan`, `OnHand 7→8` + phiếu `HOAN-…` (`Type=3/Status=1`); 403 đúng mã: `0003` (phiên bản cũ), `0001` (nhảy cóc + huỷ sau xuất kho), `0010` (huỷ qua cửa transitions); huỷ đơn B **200** + **idempotent** lần 2; 14 tab; bất biến sổ cái **8 = 8** · unit **1855/1855** (+70) · integration **90/90** · build **0 error** / 9 warning có sẵn · `schema.sql` **+19 dòng** · NND **74 (không đổi)** · OpenAPI **18/18 path** (2 lần sinh cùng hash) · FE `typecheck` ✓ + `build` ✓ (14 route) | `D-38` (số đo được), `D-39` (tác động kho + lệch `03 §4`), `D-40` (huỷ 2 cửa quyền), `D-41` (14 tab = dữ liệu). **Chưa**: 4 bảng phụ (`OrderLabel/Packing/Handover/Complaint`), bulk đổi trạng thái, tab "Thanh toán". Chi tiết: [handover-n6.md](handover-n6.md) |

| 04/11/2026 | **WP-06 (`N7`) — POS: hoá đơn bán lẻ + ca thu ngân + thanh toán đa phương thức + in + offline**: module `Pos` **4 project** + **6 bảng** (`RetailBill`/`Line`/`Payment` · `CashSession` · `RetailReturn`/`Line`); hàm thuần **`TinhTienHoaDon`** (thành tiền dòng → VAT → chiết khấu → điểm), **chặn chiết khấu 2 tầng** (`0004`), nợ phải có khách + hạn (`0006`); cổng **`IPosStockGateway`** (`GoodsIssue/Mode=Retail` ⇒ `RetailSaleOut`; trả/huỷ ⇒ `CustomerReturn`, **idempotent theo số chứng từ**); API **`/api/v1/pos`** (10 path: bills · sessions · returns); **migration 57**; FE 4 route (POS **đa tab IndexedDB**, F1–F10, quét mã; DS hoá đơn; trả hàng; in) + **outbox offline** | **probe `tools/probe-pos.ps1` 34 lời gọi HTTP, `exit 0`**: bán 3 SP chia **tiền mặt + CK** ⇒ `totalPayment 150.000`; tồn **8/9/9**; gửi lại cùng `ClientBillId` ⇒ `alreadyExisted`, tồn **không** đổi; chiết khấu 2 tầng ⇒ **403 `0004`**; nợ thiếu khách ⇒ **403 `0006`**; trả 1 A ⇒ hoàn 50.000 + kho **9**; trả vượt ⇒ **403 `0019`**; huỷ C ⇒ nhập lại kho, huỷ lần 2 `voided=false`; kết ca ⇒ `expectedCash 500.000`/`variance 0`, kết ca lần 2 + huỷ sau kết ca ⇒ **403 `0010`** · **browser thật** (`kiem-pos-browser.ps1`) **10/10**: F1 ⇒ 2 tab, **F5 không mất tab**, rớt mạng + F9 ⇒ outbox, mạng lại + Đồng bộ ⇒ **1 thành công**, server **đúng 1** hoá đơn · unit **1900/1900** (+45) · integration **90/90** · build **0 error** / 9 warning có sẵn · `schema.sql` **+919 dòng** · NND **74 → 79** · OpenAPI **28/28 path** · FE `typecheck` ✓ + `build` ✓ (18 route) | `D-42` (aggregate + tiền hàm thuần + idempotent + một quầy một ca + huỷ = đảo), `D-43` (cổng kho + `Ignore` navigation + nạp dòng trước khi so idempotency), `D-44` (số chứng từ theo ngày + 4 mã trùng số). **Chưa**: huỷ phiếu trả, service worker/Background Sync, xung đột tồn khi đồng bộ muộn, 3 chế độ quét (IMEI), quà tặng, điểm/công nợ **vào khách** (WP-07). Chi tiết: [handover-n7.md](handover-n7.md) |
| 04/11/2026 | **WP-07 (`N8`) — Khách hàng (điểm · công nợ · cấp) + Nhân viên & phạm vi dữ liệu**: **11 cột** `CusCustomers` (`Type/LevelId/Points/AccumulatedAmount/DebtLimit/DebtBalance/SaleStaffId/ReferrerId/TaxCode/Birthday`) + **6 bảng** (`CusLevels` ngưỡng · `CusCares` 5 loại append-only · `SetStores` · `SetDepartments` · `SetStaffs` · `SetRoleDataScopes`); **sổ khách một cửa ghi** (`GhiNhanMuaHang`/`DaoGhiNhanMuaHang` + `NghiepVuKhach` + cổng `ICustomerLedger`) nối **POS/trả/huỷ cùng UoW**; cổng **`IDataScopeResolver`** lọc **server-side** ở DS **và** đọc chi tiết của `Orders`/`RetailBills`; API **`/api/v1`** +**18** (7 khách · 3 cấp · 8 cấu hình); **migration 58**; FE 3 route (DS khách **5 tab** · chi tiết **5 tab** · nhân viên & **ma trận quyền** đọc sống từ định nghĩa) | **probe `tools/probe-khach-hang-wp07.ps1` 28 lời gọi HTTP, `exit 0`**: bán 100.000 dùng **3 điểm**/tích **15**/trả 47.000 ⇒ khách **điểm 12 · tích luỹ 100.000 · nợ 50.000 · tự lên cấp** (ngưỡng 100.000); bán tiếp **vượt hạn mức** ⇒ **409 `Customers:0019`** + **số hoá đơn không đổi** (rollback cả transaction POS); trả 1 món ⇒ **0/0/0**; huỷ ⇒ **đảo**, điểm không âm; `DoiChieuSoDu` **`tienTichLuyKhop`/`congNoKhop` = true**; phạm vi `Own` ⇒ hoá đơn người khác **biến mất** khỏi DS + đọc chi tiết **403 `Pos:0028`**, trả `All` ⇒ thấy lại; ma trận **4 nhóm · 80 quyền · 1 vai trò**; permission lạ ⇒ **400 `Settings:0001`** · unit **1933/1933** (+33) · integration **90/90** · build **0 error / 9 warning có sẵn** · `schema.sql` **+175 dòng** · NND **79 → 85** · OpenAPI **46/46 path** · FE `typecheck` ✓ + `build` ✓ (**21 route**) | `D-45` (sổ điểm/tiền tích luỹ/công nợ: một cửa ghi + công thức `Subtotal − DiscountTotal`; điểm âm/nợ vượt hạn mức ⇒ ném; **đảo kẹp 0**; cấp = hàm thuần; **không** ghi sổ ở FE), `D-46` (phạm vi theo **vai trò**, mặc định `All`, gộp **rộng nhất**, `Own`=CreatorId, `Store`=kho cửa hàng, **lọc server-side cả DS + chi tiết**, `Pos:0028`/`Order:0012`). **Chưa**: màn CRUD cửa hàng/phòng ban, gán **role ABP** cho người dùng, phạm vi **theo từng nhân viên**, nối `Level.DiscountPercent` vào bảng giá. Chi tiết: [handover-n8.md](handover-n8.md) |
| 04/11/2026 | **WP-08 (`N9`) — Báo cáo (read-model + job mốc + Dashboard 4 KPI + 2 báo cáo)**: module `Reporting` **4 project** + **8 bảng `Rpt*`** (`FactSalesLines` · `FactInventorySnapshots` · `AggDailySales` · `Watermarks` · 4 chiều `Dim*`); lõi **hàm thuần** `NgayKinhDoanh` (UTC+7, **nửa khoảng**) · `TongHopDoanhThu` (công thức **một chỗ**) · `KhoangTongHop` (mốc · backfill · trần 366 ngày · **mốc chỉ tiến**); job **thay thế theo `(ngày, nguồn)`** trong CÙNG transaction + khoá phân tán `report:tong-hop`; API **`/api/v1/reports`** **5 path** (revenue · orders/summary · inventory/summary · dashboard · aggregate — quyền **riêng** `.Aggregate`); **migration 59**; FE **23 route** (Dashboard 4 KPI + biểu đồ 7 ngày · báo cáo doanh thu · báo cáo tồn kho — biểu đồ **SVG** + **Xuất CSV**) | **probe `tools/probe-bao-cao-wp08.ps1` 20 lời gọi HTTP, `exit 0`**: báo cáo `tongDoanhThu` **1.470.000** **=** SQL độc lập **1.245.000** (bán lẻ) **+ 225.000** (đơn) — DoD #1; **chạy lại** cùng khoảng ⇒ **17 / 2 / 38** dòng **không đổi** — DoD #2; hoá đơn **đã huỷ** + đơn **đã huỷ** ⇒ **0** dòng trong read-model; Dashboard khớp báo cáo (`1.470.000`); kỳ **chưa** tổng hợp ⇒ `daTongHop false` (**không** phải "0 đồng"); worker tự chạy trong Host (log *bắt đầu* + *xong*) · unit **1961/1961** (+28) · integration **96/96** (+6) · build **0 error / 9 warning có sẵn** · `schema.sql` **+167 dòng** · NND **85 → 93** · OpenAPI **51/51 path** · FE `typecheck` ✓ + `build` ✓ (**23 route**) | `D-47` (read-model: grain dòng chứng từ · thay thế theo ngày · 8 index NND · ngày kinh doanh **UTC+7** · mốc chỉ tiến · một lượt một khoá · ảnh tồn kho **không phải lịch sử**), `D-48` (định nghĩa tiền `Σ thành tiền dòng − chiết khấu chứng từ`; **loại** chứng từ đã huỷ; VAT tách riêng; **chưa** trừ trả hàng; **chưa** áp phạm vi dữ liệu; `daTongHop=false ≠ 0`). **Chưa**: trừ **hàng trả lại**, phạm vi dữ liệu cho báo cáo, tên nhân viên ở chiều, báo cáo theo nhóm hàng/kênh, nút chạy lại trên FE. Chi tiết: [handover-n9.md](handover-n9.md) |
| 04/11/2026 | **WP-09 (`N10`) — Test & cổng**: bộ **E2E Playwright 8 luồng** Web Admin (`apps/admin-web/e2e`: 01 đăng nhập · 02 tạo SP · 03 nhập kho · 04 bán lẻ · 05 in · 06 đổi trạng thái đơn · 07 kết ca · 08 xuất CSV) chạy **tuần tự** như **một ngày bán hàng liên tục** (`state.json`), `tools/e2e.ps1` (giết server cũ + bật cả Host & Next từ **build hiện tại**); **`pnpm test` = Vitest 25 ca**; **`tools/kiem-openapi-drift.ps1`** + job CI `openapi-drift`; job CI `e2e-playwright` (DB **trống**); cổng `kiem-tat-ca.ps1` thêm **3 bước FE** | **8/8 E2E `passed`** (chạy **2 lần**: nóng 33s · nguội 46s Playwright tự bật 2 server): đăng nhập sai ⇒ `error=invalid`, đúng ⇒ `Tổng quan` 4 KPI; SP `E2E-…-SP` hiện trên DS (kèm SKU); phiếu `PN-20261104-2149` **ghi sổ** ⇒ `OnHand 10`; hoá đơn **`POS-20261104-0027` · 30.000** (thu tiền mặt); trang in đúng số/tổng + `window.print` **2 lần**; đơn `E2E-DH-…` **Mới → Chờ xác nhận** (`FulfillmentStatus 9`, 1 dòng lịch sử); ca `POS-CA-20261104-026` kết ⇒ **`ExpectedCash 530.000`, `Variance 0`** (SQL độc lập khớp); tồn còn **8**; CSV doanh thu **BOM + `Kỳ;Bán lẻ;…`** + CSV tồn kho **BOM + 5 cột** · **25/25** unit test FE · `typecheck` ✓ · drift **PASS — 51/51 path, `schema.d.ts`/`openapi.v1.json` không đổi** · cổng: bước **⑥ typecheck+Vitest** và **⑦ gen:api** xanh · unit **1961/1961** · integration **96/96** · build **0 error / 9 warning có sẵn** · `next build` ✓ **23 route** · **không migration mới** (59 vẫn là mới nhất) | `D-49` (E2E tuần tự + state file · webServer từ build hiện tại · **đúng 2 ngoại lệ SQL**: cờ đổi mật khẩu *tắt ở setup / **bật lại** ở teardown* + seed 1 đơn), `D-50` (`pnpm test` = Vitest **node** · cổng +3 bước FE · drift đo **trước/sau** · CI tách `openapi-drift` + `e2e-playwright`). ⚠️ **`WP-10` (Ops) CHƯA làm** (phiên `N11`); cổng DB còn **1 HỒNG có sẵn** (`CusCustomers` 6 dòng probe WP06/WP07 từ `N7`/`N8` — phiên này **không** thêm dòng nào, và đã **bật lại** cờ bảo mật của admin nên nhóm ② xanh: **23/25 → 24/25**). Chi tiết: [handover-n10.md](handover-n10.md) |
| 04/11/2026 | **WP-10 (`N11`) — Ops: Docker service `admin-web` + `.env.example` + `/api/health`**: `apps/admin-web/Dockerfile` (2 tầng `node:22-alpine`, `pnpm install --frozen-lockfile`, `next build` với **`output: "standalone"`**, `HEALTHCHECK` gọi chính `/api/health`, chạy user `node`); **`.dockerignore` ở GỐC** (context build = gốc repo vì workspace pnpm); compose thêm service **`admin-web`** (cổng **3000**, `extra_hosts: host.docker.internal`, `build.args` cho `NEXT_PUBLIC_APP_NAME`, **không** `depends_on` — xem `D-51` ①); `.env.example` bổ sung **`AUTH_ISSUER`** + `NEXT_PUBLIC_APP_NAME` + `HEALTH_TIMEOUT_MS` + ghi chú **hai chế độ local/container**; route **`GET /api/health`** (200/503 + `checks.host`, `no-store`, trần 3 s, **không** lộ chi tiết); `lib/config.ts` thêm `AUTH_ISSUER`/`TOKEN_ENDPOINT`/`HEALTH_TIMEOUT_MS`, `lib/public-config.ts` (`APP_NAME` dùng ở sidebar/tiêu đề/đăng nhập — giá trị mặc định ⇒ **giao diện không đổi**) | **`tools/kiem-ops-wp10.ps1` → `exit 0`, 9 lời gọi HTTP, 0 lỗi**: `docker compose config` OK; `docker compose up -d admin-web` trên **cổng mặc định 3000** ⇒ container **running**; Host **chết** ⇒ `/api/health` **`503`** + `host=Unreachable` + container **`unhealthy`** sau **91 s**; Host **sống** ⇒ **`200`** + `host=Healthy` + container **`healthy`** sau **10–15 s**; `docker compose stop` **nhả cổng 3000**; bản **`next start` không Docker** cũng **200/503**; **`/login` vẫn `200` khi API chết** · image **354 MB** · `pnpm build` **24 route** (+`/api/health`) · typecheck ✓ · **25/25** unit test FE · **8/8 E2E** · drift **PASS 51/51** · unit **1961/1961** · integration **96/96** · **0 migration mới** (59) | `D-51` (compose: không thêm service `host` — `depends_on` **che mất** phép đo 503; `host.docker.internal` + `extra_hosts`; **hai** giá trị mặc định cho `API_BASE_URL`; `.dockerignore` ở gốc; `standalone`; probe phải `stop` container), `D-52` (`/api/health` = sức khoẻ **Host**; `Unreachable ≠ Unhealthy`; `no-store` + trần; **không** lộ chi tiết; `NEXT_PUBLIC_*` = **build arg**; `AUTH_ISSUER` có fallback; BFF sống độc lập với API). **Chưa**: job CI `docker-build`, đưa Host .NET vào compose. Chi tiết: [handover-n11.md](handover-n11.md) |
| 04/11/2026 | **WP-11 (`N12`) — Làm cổng xanh**: **9 script cổng** hết khuôn chỉ-Windows (`Lay-DuongDanBin` · `Lay-PidDangNghe` · `Lay-Curl` · `Start-TienTrinhAn` · **`Start-NextAdmin`** *(mới)* · **`Lay-DuongDanTam`** *(mới)* · `-o NUL` → tệp tạm) + đường dẫn nhiều đoạn → `[IO.Path]::Combine`; `tools/nen-tang.ps1` thêm **2 hàm** + `Start-TienTrinhAn` nhận **mảng** đối số (3 tham số log thành tuỳ chọn); `test-cong-kiem.ps1` `Lay-MauHamNenTang` **biết 2 tên mới**; **`CusCustomers`** ra khỏi nhóm "phải rỗng" → **bất biến MẠNH HƠN** nhóm **①c** (*mọi khách còn sống phải có chứng từ* + *0 bản ghi xoá mềm*) + xoá **1** dòng vô chủ | **`kiem-tat-ca.ps1` → `exit 0` (9/9 bước, 195,2 s)** · `test-cong-kiem.ps1` **32/33 → 33/33** · `kiem-db.ps1` **24/25 → 25/25** (`conSong=5 · moCoi=0 · xoaMem=0`; **chiều ngược**: `INSERT` + `ROLLBACK` ⇒ `moCoi 0 → 1 → 0`) · `e2e.ps1` **8/8** · drift **PASS 51/51** · unit **1961/1961** · integration **96/96** · mermaid **213 khối · 0 lỗi** · **0 migration mới** (59) | `D-53` (*"phải rỗng"* **mô tả sai sự thật**: `D-42` ④ buộc POS có khách + API **không có** `DELETE` khách ⇒ cổng **luôn đỏ**; xoá 5 dòng có chứng từ = **xoá bằng chứng** của **11** hoá đơn + **3** phiếu trả + **8** dòng fact) và `D-54` ("một chỗ": **đo được** `Start-Process pnpm` **NÉM** trên Windows, `cmd.exe` **không có** trên Linux). ⏭ **③ job CI `docker-build` cắt có quyết định** (chưa đo được trên `ubuntu-latest`). Chi tiết: [handover-n12.md](handover-n12.md) |
| 04/11/2026 | **WP-12 (`N13`) — Làm `-DayDu` xanh**: `probe-oversell` + `probe-giu-cho-2instance` so **SỐ** (`[decimal]::Parse` **invariant**) thay so **chuỗi** cho `decimal(18,3)` — 7 ca (`T01`/`T08`/`T09` · `P01`/`P09`/`P10`/`P11`); sửa **một dòng** in kết quả Playwright của cổng (`Get-Content` trả chuỗi ⇒ `$_.Line` null ⇒ NÉM dù E2E 8/8); soát 3 probe cùng họ (không cùng lớp); **`§WP-13`** thêm cho 2 nợ kiểm chứng còn lại | **`-DayDu` exit 0 — 19/19 bước, 462,5 s** (⑮ oversell **11/0** · ⑯ outbox **9/0** · ⑰ outbox 2 instance **9/0** · ⑱ giữ chỗ 2 instance **17/0** · ⑲ Playwright **8/8**); bản nhẹ **exit 0 — 9/9, 193,5 s**; test cổng **33/33** (full **38/38**) · DB **25/25** · unit **1961/1961** · integration **96/96** · drift **51/51** · mermaid **213 khối/0 lỗi** · **0 migration** (59) | `D-55` (so SỐ + parse invariant; DoD thật **19** bước không phải 17; `Get-Content` ≠ `Select-String` — `$_.Line` chỉ có trên `MatchInfo`). ⏭ **③ `docker-build` cắt có quyết định**. Chi tiết: [handover-n13.md](handover-n13.md) |
| 04/11/2026 | **WP-13 (`N14`) — Đóng 2 nợ kiểm chứng**: **một chỗ** mở/bật cờ `ShouldChangePasswordOnNextLogin` — `Dat-CoBuocDoiMatKhau` (UPDATE + **xác nhận** `count=1`) + `Invoke-KhongCoDoiMatKhau` (mở → khối → `finally` bật lại) + cặp `Start-KhongCoDoiMatKhau`/`BatLai-CoDoiMatKhau` trong `tools/nen-tang.ps1`; **5** probe dùng lại (`probe-kho-wp04` + 4 probe tắt cờ mà **không** bật lại: `probe-pos`/`probe-don-hang`/`probe-bao-cao-wp08`/`probe-khach-hang-wp07`); `probe-kho-wp04` ⑫ so **SỐ**; **2** ca kiểm mới `Y6` (quét *mở cờ ⇒ phải có dòng bật lại*, ≥ 4 tệp) + `L2` (DB: `false` trong khối, `true` sau, khối `exit 7` ở tiến trình con giữ **exit 7**) | `probe-kho-wp04.ps1` **exit 0 — 12 ca** (trước khi sửa: `exit 1`, `invalid_grant`/`ID2024`) · `probe-pos.ps1` **exit 0 — 34 lời gọi** (cặp hàm chạy qua **cả** BFF) · cờ `ADMIN\|true` sau mỗi lượt · `kiem-db.ps1` **25/25** · `test-cong-kiem` **34/34** (full **40/40**) · bản nhẹ **exit 0 — 9/9, 196,3 s** · `-DayDu` **exit 0 — 19/19** · unit **1961/1961** · integration **96/96** · drift **51/51** · **0 migration** (59) | `D-56` (một chỗ + **4** chứ không phải 3 tệp "quên bật lại"; cặp hàm vì 4 probe đi **cả** BFF; `finally` chạy cả khi `exit`; 2 ca kiểm mới). ⏭ **② `docker-build` giữ P1** (chưa đo được trên `ubuntu-latest`). Chi tiết: [handover-n14.md](handover-n14.md) |
| 04/11/2026 | **Đóng gói image Web Admin (`N16`) — commit → push → PR #1 → bật required check**: `d8e22dc` (**60** tệp **cây FE**; 🚨 **bắt buộc** — đo được: `HEAD` + **đúng 3 tệp** ⇒ `docker build` **ĐỎ** `standalone not found`, **D-58**) + `6c49d3a` (**3** tệp: `.dockerignore` · `apps/admin-web/Dockerfile` · `.github/workflows/docker-build.yml`) ⇒ `origin/feature/wp-03-catalog` ⇒ **PR [#1](https://github.com/hungnt9801/eCommerce/pull/1)** (39 commit · 488 tệp) | **`docker-build` XANH trên `pull_request` (lần đầu)** — run [**`41811821775`**](https://github.com/hungnt9801/eCommerce/actions/runs/41811821775) **88 s**, **6/6** bước; máy dev trên **cây sạch** `6c49d3a`: build **exit 0** · `server.js` **6380 B** · `/login` **200** (lần thử 2) · `/api/health` **503** · **83 MB**/**354 MB** · `-DayDu` **19/19** (485 s) · **0 migration** | `D-58` (commit đóng gói **phải** kèm cây FE — 3 tệp trên `HEAD` ⇒ **ĐỎ**; tách 2 commit), `D-59` (**`main` không có** protection trước phiên — `GET` **404**; bật **đúng một** `docker-build`, **không** bật `cong-15-buoc` vì nó đỏ do nợ `packageManager`+`yarn` **có sẵn**). ⚠️ PR #1 mang **nửa FE**, BE chưa commit (`§5 #3`). Chi tiết: [handover-n16.md](handover-n16.md) |
| 04/11/2026 | **WP-13 ② (`N15`) — JOB CI `docker-build`**: workflow **riêng** `.github/workflows/docker-build.yml` — `docker build -f apps/admin-web/Dockerfile` với context **gốc repo** + `NEXT_PUBLIC_APP_NAME` build arg, rồi **2 bất biến**: image có `server.js`+`.next/static` (`standalone`) và container **bật được** với `/login` **200** khi API chết (`D-52` ⑦) | run [**`41808550753`**](https://github.com/hungnt9801/eCommerce/actions/runs/41808550753) **success trên `ubuntu-latest`** — **76 s** (build **68 s**), **6/6** bước · trước đó cùng ngày, máy dev: `docker build` **exit 0** · `/login` **200** (lần thử 2) · `/api/health` **503** · size **354 MB** (`docker images`) = **83 MB** (`inspect .Size`) · **0 migration** · **0** test BE thêm | `D-57` (workflow riêng + **nhánh tạm** để có run thật rồi xoá; lượt đo **đầu ĐỎ** vì `Dockerfile`/`.dockerignore` **untracked** — nợ thật; 2 bất biến sau build; **hai** cách đo size; required check **chưa bật**). Chi tiết: [handover-n15.md](handover-n15.md) |
| 05/11/2026 | **`N18` — Commit cây làm việc → `ci` xanh trên PR #1 → MERGE**: **6** commit · **160** tệp (`6ab9114` BE `WP-06…WP-08` + migration **57…59** · `4248d80` `ci.yml` bước FE + 2 job · `b94b13e` compose `admin-web` · `0bbc140` `tools/` `N12`–`N14` · `3f93c14` docs · `39df2d4` 2 fix); **39** tệp `tools/_*` **vẫn** ngoài VCS (rác cổng) | **`ci` lượt 1** (head `3f93c14`) **ĐỎ 2 chỗ** — bước 11 (`FAIL — 3 tham chieu chet`: `artifacts/`) + `e2e-playwright` (**1 failed · 7 passed**) — **nhưng cổng nghiệp vụ 19/19 XANH** (`290,5 s`); **`ci` lượt 2** (head `39df2d4`) **3/3 XANH, 10,2 phút** (run `41963379150`; log: **19/19 (352,8 s)** · Playwright **8 passed** · link **657**/PASS) · `docker-build` **6/6** (`41963379197`) ⇒ **merge PR #1 = `fab0089`** (46 commit · **610** tệp) · bản nhẹ **9/9** · `-DayDu` **19/19 (436,8 s)** · test cổng **36/36** · DB **25/25** · **0 migration** (59) | `D-62` (merge khi **đo** thấy xanh; **2 lỗi chỉ lộ trên cây sạch**: mốc ảnh tồn kho ghi cho lượt chụp **0 dòng** ⇒ báo cáo tồn kho rỗng cả ngày; cổng link bỏ qua `artifacts/` — **đối xứng** `$boQua`, ⛔ không nới cho link trong repo, ca `V4`/`V5`), `D-63` (`cong-15-buoc` required **chưa** bật — settings của repo, cần phiên riêng đo TRƯỚC/SAU). Chi tiết: [handover-n18.md](handover-n18.md) |
| 05/11/2026 | **Đóng nợ CI `packageManager` (`N17`) — và KHÔNG merge PR #1**: bỏ **1 dòng** `packageManager` ở `package.json` gốc + sửa comment `Dockerfile` ⇒ `yarn`/`abp install-libs` chạy lại được; ⛔ **không** bỏ bước 7 khỏi `ci.yml` | **Bước 7 `ci` XANH trên CI thật** (run `41955331290`, job **294 s**; trước: đỏ sau **38 s**) · `docker-build` **xanh 6/6** (run `41955331294`, 66 s) · `yarn --version` **exit 1 → exit 0** · `abp install-libs` **exit 0** (10,2 s) · `pnpm install --frozen-lockfile` **exit 0** · cổng nhẹ **9/9 (228,5 s)** · `-DayDu` **19/19 (455,9 s)** · test cổng **34/34** (full **40/40**) · DB **25/25** · **0 migration** | `D-60` (bỏ field; pnpm ghim **2 chỗ**), `D-61` (**KHÔNG merge**: `ci` đỏ ở **⑦/Y1** vì 3 probe `N12`–`N14` chưa commit là **mới so với `main`** ⇒ merge làm `main` đỏ; + nửa FE/API 404). Chi tiết: [handover-n17.md](handover-n17.md) |
| 05/11/2026 | **CHỐT HƯỚNG `D-64` (`N20`) — `409` là HỢP ĐỒNG, probe thử lại có GIỚI HẠN** (không mở WP mới: `07` chưa có `§WP-14`): `probe-giu-cho-2instance.ps1` thêm `$SoLanThuLai = 3` + `ThuLaiNeu409` (gửi lại **đúng** request: cùng Host + cùng `idempotencyKey`), `P12` đo **sau** thử lại, `P14` tách *xung đột đã khai báo* ↔ *lỗi thật*, 1 dòng `ℹ️` báo số lần thử lại; `test-cong-kiem.ps1` thêm ca **`Y7`** (quét chính tệp probe, **hai chiều**) | **Repro TẤT ĐỊNH**: transaction `psql` khác đổi `ConcurrencyStamp` ⇒ **409** `code=null` (*"…changed by another user…"*) + **1** dòng `AbpDbConcurrencyException` (**0** `Unhandled exception`), stack tại `EfCoreRepository.InsertAsync(autoSave)` ← `GiuChoAsync`; **thử lại cùng khoá ⇒ 200**, `OnHand/Reserved = 2.000/1.000` · bản **giả lập 409** (`tools/_tam-kiem`, không commit) ⇒ probe **17/0** + `ℹ️ THU LAI 409: 1 lần` · probe **5 lượt trước sửa `17/0 ×5`** / **5 lượt sau sửa `17/0 ×5`** (máy dev không tái hiện flake) · test cổng **36/36 → 37/37** · required check **không đổi** (`["docker-build","cong-15-buoc"]`, `strict=false`, `enforce_admins=false`) · **0 migration** (59), **0** tệp `.cs` | `D-66` (chốt hướng (b): `409` = chồng request, **thử lại**; ⛔ không retry trong server vì `GiuChoAsync` chạy trong transaction checkout — PostgreSQL đã abort; ⛔ không nới bất biến; `P14` **chỉ** phân loại lại, vẫn cấm `Unhandled exception`/`OutOfMemory`). Chi tiết: [handover-n20.md](handover-n20.md) |
| 06/11/2026 | **CHẨN ĐOÁN `D-67` (`N21`) — `C10` đo QUÁ SỚM, chứng minh bằng THỨ TỰ COMMIT** (không mở WP mới: `07` chưa có `§WP-14`): `probe-don-san.ps1` thêm hàm thuần `Dem-NeoLogHost` + `C10` in `choDu` & **3** số neo log Host + dòng `[CD-D67]` **đo lại `Late` SAU khi DLQ đã có** + in ≤ 20 dòng log Host của đơn khi `C10` đỏ; `kiem-tat-ca.ps1` bước ⑭ lọc thêm `CD-D67`; `test-cong-kiem.ps1` thêm ca **`Y8`** (**4** chiều xuôi + **4** chiều ngược) | **Bằng chứng đọc từ artifact lượt ĐỎ** `42005307510` (head `b195598`, tree **`a64d69a3`** = tree của PR xanh): `tools/_tam-kiem/donsan.log` **48/49 DAT** — chỉ `C10` sai (`tong = 0 · Late = 0`) trong khi `C11` (watermark không đổi) · `C12` (0 đơn) · `C13` (**DLQ `LyDo=HaWatermark` = 1**) · `C14` · `C20…C49` **DAT** ⇒ artifact **có** output probe, **thiếu** đúng `host.log` · 🔑 **thứ tự commit**: DLQ ghi **UoW của handler** (commit **cuối**), nhật ký `Late` ghi **`requiresNew`** (commit **trước**) ⇒ `DLQ = 1` ⇒ dòng `Late` **đã** có ⇒ `C10 = 0` là **hết hạn 30 s** · bản **giả lập** (không commit): `C10` in `choDu = False` + `[CD-D67] … Late = 1` ⇒ **cùng lượt chạy** vừa đỏ vừa có dòng · **10 lượt local**: `49/49 × 10`, **10 XANH / 0 ĐỎ**, 29,9–34,4 s ⇒ **không** tái hiện; tỉ lệ CI **2 ĐỎ / 5 lượt** · test cổng **37/37 → 38/38** · `-DayDu` **19/19 (458,3 s)** · bản nhẹ **9/9 (193,2 s)** · DB **25/25** · mermaid **213/0** | `D-68` (`C10` đỏ = **THỜI ĐIỂM ĐO**, không mất dữ liệu; ⛔ **không** nới 30 s / không bỏ ca; chẩn đoán **chỉ in**; `D-67` **vẫn MỞ** — sửa `C10` thuộc phiên có `§WP-14`). Chi tiết: [handover-n21.md](handover-n21.md) |
| 17–18/11/2026 | **WP-14 đợt A — phần 1 (`N22`)**: `/doc-gate ADDR-001 ADDR-002` (dây chuyền agent `ba` → `decider` → `docrev`, lần chạy thật đầu tiên); hệ agent B1–B2 tách commit riêng nhánh `chore/agents-b1-b2` | `ADDR-001` **DOC-OK** vòng 2 (V1 1 P1/12 P2 → V2 0 P1/2 P2) · `ADDR-002` **DOC-OK** vòng 2 (V1 4 P1/9 P2 → V2 0 P1/2 P2) · `check-feature-doc.ps1 -WP14` exit 0 ×2 (16/16 mục; BR có test 14/14, 16/16) · `check-agent-artifact.ps1` decision **16/16** dòng DEC · link PASS (144 tệp, 0 chết) · mermaid **212/0** · hook **26/26** · **0** `.cs`, **0** migration | `D-77…D-92` (16 TẠM CHỐT DEC) + `D-93`, `D-94` (chủ dự án chốt). Còn 7 tài liệu đợt A. Chi tiết: [handover-n22.md](handover-n22.md) |
| 18/11/2026 | **WP-14 đợt A — phần 2 (`N23`) DỞ**: `/doc-gate ADDR-003` (BA → decider ×7 → DOCREV vòng 1); phát hiện + khôi phục `D-77…D-94` bị commit `5aa3dce` xoá; dừng do hạn mức 5 giờ | `ADDR-003`: `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục, 16/16 BR có test, 12 câu `Open`) · DOCREV V1 **`DOC-CHANGES` 1 P1/17 P2/18 P3** (chưa V2) · decision **23/23** dòng DEC · link PASS (733 link, 0 chết) · mermaid **212/0** · hook **30/30** · **0** `.cs`, **0** migration | `D-95…D-101` (7 TẠM CHỐT DEC). Còn: áp `D-98…D-101` + DOCREV V2 cho `ADDR-003`, 6 tài liệu (`SHP-001…004`, `REC-003`, `SAL-006`). Chi tiết: [handover-n23.md](handover-n23.md) |
| 18/11/2026 | **WP-14 đợt A (`N24`)** — `ADDR-003` áp `D-98…D-101`, sửa 5 P2 + `D-103`, DOCREV vòng 3 **`DOC-OK`**; người chốt `D-105…D-107`; còn 6 tài liệu đợt A | `kiem-link` PASS 739 link/0 chết · `check:mermaid` 212 khối/0 lỗi · `hooks.test.mjs` 33/33 · `check-feature-doc` DAT · docreview `DOC-OK` 0 P1/1 P2/26 P3 · **0** `.cs` · **0** migration | Chi tiết: [handover-n24.md](handover-n24.md) |
| 18–19/11/2026 | **WP-14 đợt A (`N25`)** — `/doc-gate SHP-001` (BA ×5 → decider ×3 → DOCREV ×3): `DOC-CHANGES` 5 P1/11 P2 → **`DOC-OK`** 0 P1/0 P2; `ADDR-003` còn áp `D-105…D-107`; còn 5 tài liệu đợt A | `kiem-link` PASS 753 link/0 chết · `check:mermaid` 212 khối/0 lỗi · `hooks.test.mjs` 34/34 · decision 27/27 DEC · `check-feature-doc` DAT (22 BR, 52 TC) · docreview `DOC-OK` 0 P1/0 P2/17 P3 · **0** `.cs` · **0** migration | `D-108…D-110` (3 TẠM CHỐT DEC). Chi tiết: [handover-n25.md](handover-n25.md) |
| 19/11/2026 | **WP-14 đợt A (`N26`)** — `/doc-gate ADDR-003` (BA ×2 → decider ×1 → DOCREV kiểm nhanh): áp `D-105…D-107` + `D-111`, **`DOC-OK`** vòng 4 (0 P1/2 P2/37 P3); còn 5 tài liệu đợt A | `kiem-link` PASS 755 link/0 chết · `check:mermaid` 212 khối/0 lỗi · `hooks.test.mjs` 34/34 · decision 28/28 DEC · `check-feature-doc` DAT (17 BR, 47 TC) · docreview `DOC-OK` 0 P1/2 P2/37 P3 · **0** `.cs` · **0** migration | `D-111` (1 TẠM CHỐT DEC). Chi tiết: [handover-n26.md](handover-n26.md) |
| 19/11/2026 | **WP-14 — phiên uỷ quyền `U-02` (`N28`)**: chủ dự án chốt M1–M6, V1–V3, 16 chính sách R1, R2 (30 câu + 36 dòng `D-xx`), R3 (tự lo 8 việc) | **`D-114`** + **`D-115`** (bổ sung `D-71`: tạm duyệt ngoài danh sách đỏ, không merge `main`) · hồ sơ `uy-quyen-chu-du-an.md` **HIỆU LỰC** · 36 dòng `D-xx` ⇒ `ĐÃ CHỐT` · kiem-link PASS · mermaid 212 khối 0 lỗi · hooks 34/34 · 0 `.cs` | [handover-n28.md](handover-n28.md) |
| 20/11/2026 | **WP-14 đợt A (`N30`, handover `N32`) — `P-05` `SHP-003`**: `/doc-gate` BA ×4 → decider ×4 → DOCREV ×2: `DOC-CHANGES` 3 P1/12 P2 → **`DOC-OK`** vòng 2 (0 P1/2 P2/10 P3); `D-117`, `D-120` (TẠM CHỐT) + áp `D-114`/`D-82` | `check-feature-doc -WP14` exit 0 (16/16, 22/22 BR có test, 60 TC) · `check-agent-artifact -Kind docreview` exit 0 · `kiem-link` PASS · `check:mermaid` 212 khối/0 lỗi · SQL chưa đo (không `psql`) |
| 20/11/2026 | **WP-14 đợt A (`N33`, handover `N33`) — `P-06` `REC-003`**: `/doc-gate` BA ×4 → decider ×7 (`D-121…D-127`, TẠM CHỐT) → DOCREV ×2: `DOC-CHANGES` 2 P1/14 P2 → **`DOC-OK`** vòng 2 (0 P1/2 P2/9 P3); v0.5, 44 TC, 16 BR; `check-feature-doc -WP14` exit 0; link PASS, mermaid 0 lỗi. Còn: 2 P2, Q18; nợ `SHP-003`/`SAL-003`/`REC-001-002`. |
| 21/11/2026 | **WP-14 đợt A (`N37`, handover `N37`) — `P-04` `SHP-002` + `SHP-004`**: `SHP-004` BA + decider (`D-128…D-131`, TẠM CHỐT) → DOCREV ×2: `DOC-CHANGES` 1 P1/8 P2 → **`DOC-OK`** vòng 2 (0 P1/2 P2/15 P3); v0.5, 18 BR; `check-feature-doc -WP14` exit 0; link PASS, mermaid 0 lỗi. Còn: 2 P2; nợ `SHP-001`/`SHP-002`; Q1 chờ GHTK. |
| 23/11/2026 | **WP-14 đợt B (`N43`, handover `N43`) — `P-10` `PRO-001` DỞ**: BA ×2 → decider ×4 (`D-146…D-149`, TẠM CHỐT) → DOCREV vòng 2 `DOC-CHANGES` 0 P1/9 P2 (quá trần 2 vòng ⇒ `doc-gate-2026-11-23.md`); `check-feature-doc` exit 0 (36/36 BR có test) · link PASS · mermaid 0 lỗi · 0 `.cs`, 0 migration |
| 23/11/2026 | **WP-14 đợt B (`N51`, handover `N51`) — `P-11` `PRO-002` DỞ→`DOC-OK`**: BA ×2 đóng 1 P1 + 3 P2 của vòng 3 + áp `D-163` (Q27, "đơn đầu"); DOCREV **vòng 4 kiểm nhanh `DOC-OK`** (0 P1 / 0 P2 / 2 P3); v0.9, 55 TC, 20 BR; còn `LOY-001` (hàng P-11 chưa xong) | `check-feature-doc.ps1 -WP14` exit 0 (16/16, 20/20 BR) · `check-agent-artifact -Kind docreview` exit 0 · `kiem-link` PASS 801 link/0 chết · `check:mermaid` 211/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-163` (TẠM CHỐT DEC). Chi tiết: [handover-n51.md](handover-n51.md) |
| 24/11/2026 | **WP-14 đợt B (`N52`, handover `N52`) — `P-11` `LOY-001` DỞ (chưa `DOC-OK`)**: BA đối chiếu v0.2 → 7 câu D1q + `Q10`/`Q11` (5 câu `TIEN_LE`) → `D-164…D-167` → BA áp v0.7; DOCREV **vòng 1 → 2 → 3 kiểm nhanh** đều `DOC-CHANGES` (vòng 3: 1 P1 chéo POS / 2 P2); dừng `/doc-gate` §3, ghi `doc-gate-2026-11-24.md`; 51 TC, 24 BR, 0 câu `Open` | `check-feature-doc.ps1 -WP14` exit 0 (16/16, 24/24 BR) · `check-agent-artifact -Kind decision` DAT (52) · `kiem-link` PASS 0 chết (1092 backtick) · `check:mermaid` 211/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-164` `D-165` `D-166` `D-167` (TẠM CHỐT DEC) + `TIEN_LE` `D-45`①/`D-133`/`D-114` P13/P1. Chi tiết: [handover-n52.md](handover-n52.md) |
| 24/11/2026 | **WP-14 đợt B (`N53`, handover `N53`) — `P-11` `LOY-001` DỞ→`DOC-OK`, hàng `P-11` XONG**: decider chốt **`D-168`** (cả 3 đường POS đi cổng Loyalty, không nới `NFR-003`/`TC-49`; CR chéo Pos) + cập nhật `D-167` (8 tham số có `noGiam`); BA v0.8 áp `D-168` + đóng P2 #3 (§10 nhãn) + 6 P3; DOCREV **vòng 4 kiểm nhanh `DOC-OK`** (0 P1 / 1 P2 / 2 P3); 53 TC, 24 BR, 0 câu `Open` | `check-feature-doc.ps1 -WP14` exit 0 (16/16, 24/24 BR) · `check-agent-artifact -Kind docreview` exit 0 (`DOC-OK` P1=0/P2=1) · `-Kind decision` DAT (53) · `kiem-link` PASS 0 chết (845 link/1101 backtick) · `check:mermaid` 211/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-168` (TẠM CHỐT DEC) + cập nhật `D-167`. P2 §11 (danh sách unique index thiếu `RetailBillId`/`OrderId`) + 2 P3 để người duyệt. Chi tiết: [handover-n53.md](handover-n53.md) |
| 24/11/2026 | **WP-14 đợt B (`N54`, handover `N54`) — `P-12` XONG**: hoàn thiện `docs/agents/nhap-bien-ban-gd3-b.md` — gộp bảng về một bảng liền mạch (**sửa lỗi dòng trống làm hàng `LOY-001` rơi khỏi bảng**), 3/3 dòng `PRO-001`/`PRO-002`/`LOY-001`; cột người review theo `design-doc-guidelines.md` §6 (state machine ⇒ +BA, tiền/VAT ⇒ +kế toán); thêm **§3 bảng câu chờ người**: 13 câu §15 còn mở/`Deferred` + **26** dòng `TẠM CHỐT` đợt B + 6 việc cùng WP code. **Không** lập biên bản thật (hàng `P-13`) | `kiem-link` PASS 0 chết (185 tệp · 846 link · 1108 backtick) · `check:mermaid` 211/0 · hooks 40/40 · `check-agent-artifact -Kind decision` DAT (53) · `PH check` OK · **0** `.cs`, **0** migration | Không `D-xx` mới. P2 mở: `PRO-001` #20, `LOY-001` §11. Chi tiết: [handover-n54.md](handover-n54.md) |
| 24/11/2026 | **WP-14 đợt B (`N55`, handover `N55`) — `P-13` XONG**: lập `docs/bien-ban-tam-duyet-gd3-b-2026-11-24.md` từ `D-169` (decider, uỷ quyền `D-115`/M2) — **tạm duyệt có điều kiện `PRO-001` + `PRO-002`**, **`LOY-001` "chưa"** (đỏ có điều kiện §2.1, chờ người duyệt P13; đường mở khoá ghi rõ) + **xác nhận kiến trúc engine điều kiện ở mức thiết kế** (7 vế, không dựng bước review riêng); 3 dòng hàng đợi | `check-feature-doc.ps1 -WP14` exit 0 **3/3** (PRO-001 16/16·36/36 BR · PRO-002 16/16·20/20 · LOY-001 16/16·24/24) · `check-agent-artifact -Kind decision` DAT (54) · `kiem-link` PASS 0 chết (188 tệp · 859 link · 1136 backtick) · `check:mermaid` 211/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-169` (TẠM CHỐT DEC, 3 dòng: PRO-001/PRO-002/LOY-001). **Chờ người:** P13 (LOY-001). Chi tiết: [handover-n55.md](handover-n55.md) |
| 24/11/2026 | **WP-14 đợt C (`N56`, handover `N56`) — `P-14` DỞ**: `/doc-gate REP-001` — BA đối chiếu v0.1→v0.2 theo as-built `WP-08`/`D-47` (471 thêm/613 xoá) · 5 câu D1q → `D-170…D-174` → BA áp (`D1s` exit 0) · DOCREV vòng 1 `DOC-CHANGES` (1 P1/8 P2) → BA sửa → **vòng 2 `DOC-CHANGES` (0 P1/8 P2)** ⇒ hết trần ⇒ ghi `doc-gate-2026-11-24.md`; tạo `docs/agents/nhap-bien-ban-gd3-c.md` (dòng `REP-001` **"chưa"**); `REP-002` chưa chạy (luật ≥ 5 câu decider ⇒ một tài liệu/phiên) | `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục · 17/17 BR có test) · `check-agent-artifact -Kind docreview` DAT (`DOC-CHANGES` P1=0/P2=8) · `-Kind decision` DAT (**59** dòng) · `kiem-link` PASS 0 chết (**190** tệp · **869** link · **1159** backtick) · `check:mermaid` 211/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-170`…`D-174` (5 TẠM CHỐT DEC). **Còn:** 8 P2 `REP-001` (sửa → DOCREV kiểm nhanh) + `REP-002`. Chi tiết: [handover-n56.md](handover-n56.md) |
| 24/11/2026 | **WP-14 đợt C (`N57`, handover `N57`) — `P-14` DỞ**: `REP-001` sửa 8 P2 → **DOCREV vòng 3 kiểm nhanh `DOC-OK`** (0 P1/0 P2/10 P3) ⇒ đủ trình duyệt; `REP-002` chạy đủ dây chuyền: 7 câu §15 → 6 `TIEN_LE` + `D-175` → BA áp (D1s exit 0) → DOCREV vòng 1 `DOC-CHANGES` (1 P1/6 P2) → sửa → **vòng 2 `DOC-CHANGES` (0 P1/5 P2)** ⇒ hết trần, ghi `doc-gate-2026-11-24.md` | `check-feature-doc.ps1 -WP14` DAT (REP-001 16/16·17/17 BR; REP-002 16/16·18/18 BR) · `check-agent-artifact -Kind docreview` DAT (REP-001 `DOC-OK` P1=0/P2=0; REP-002 `DOC-CHANGES` P1=0/P2=5) · `-Kind decision` DAT (**60** dòng) · `kiem-link` PASS 0 chết (**192** tệp · **875** link · **1170** backtick) · `check:mermaid` 212/0 · hooks 40/40 · **0** `.cs`, **0** migration | `D-175` (TẠM CHỐT DEC) + 6 `TIEN_LE` (`D-114` P2/P3/P4 · `D-48` · `D-172` · `D-03`/`D-36`). **Còn:** 5 P2 `REP-002` (sửa → DOCREV kiểm nhanh). Chi tiết: [handover-n57.md](handover-n57.md) |
| 24/11/2026 | **WP-14 đợt C (`N58`, handover `N58`) — `P-14` XONG**: `REP-002` BA sửa 5 P2 vòng 2 (node Mermaid PRC, allow-list hai mục, chốt snapshot giá vốn `RptFactSalesLines.CostUnitAmount`/`CostMissing`, SAL-002 đã có code, BR-018 huỷ muộn + TC-44) → DOCREV **vòng 3 kiểm nhanh `DOC-CHANGES`** (0 P1/3 P2 do bản sửa) → BA sửa 3 P2 → DOCREV **vòng 4 kiểm nhanh `DOC-OK`** (0 P1/0 P2/5 P3) ⇒ đủ trình duyệt; cập nhật `nhap-bien-ban-gd3-c.md` (cả `REP-001` + `REP-002` đề xuất trình duyệt) + `doc-gate-2026-11-24.md` | `check-feature-doc.ps1 -WP14` DAT (REP-002 16/16·18/18 BR·0 câu Open) · `check-agent-artifact -Kind docreview` DAT (`DOC-OK` P1=0/P2=0) · `-Kind decision` DAT (**60** dòng) · `kiem-link` PASS 0 chết (**193** tệp · **880** link · **1171** backtick) · `check:mermaid` 212/0 · hooks 48/48 · **0** `.cs`, **0** migration | **Không** `D-xx` mới (5 P3 để người duyệt; `D-175` giữ trong hàng đợi). **Còn:** `P-15` (`REP-003` + `ACC-001`), `P-16`, `P-17`, `P-18`, `P-19` (người), `P-20`, `P-21` (người). Chi tiết: [handover-n58.md](handover-n58.md) |
| 24/11/2026 | **WP-14 đợt C (`N59`, handover `N59`) — `P-15` XONG**: `REP-003` BA áp `D-170` + 11 câu → `D-176`…`D-186` → DOCREV vòng 1 `DOC-CHANGES` (2 P1/7 P2: `QuantityDelta` lưu độ lớn dương, `LineTotal` gồm thuế) → vòng 2 `DOC-OK` (1 P2) → vòng 3 kiểm nhanh `DOC-OK` (0 P1/0 P2/4 P3); `ACC-001` BA đối chiếu + 10 câu → `D-187`…`D-196` → DOCREV vòng 1 `DOC-CHANGES` (0 P1/6 P2) → vòng 2 `DOC-OK` (1 P2) → vòng 3 kiểm nhanh `DOC-OK` (0 P1/0 P2/5 P3); cập nhật `nhap-bien-ban-gd3-c.md` (4/6 tài liệu đợt C đề xuất trình duyệt) | `check-feature-doc -WP14` DAT (REP-003 14/14 BR·0 Open; ACC-001 18/18 BR·1 Open = `Q12`) · `check-agent-artifact -Kind docreview` DAT ×2 (`DOC-OK` P1=0/P2=0) · `-Kind decision` DAT (**81** dòng, tới `D-196`) · `kiem-link` PASS 0 chết (**196** tệp · **884** link · **1259** backtick) · `check:mermaid` 212/0 · hooks 48/48 · **0** `.cs`, **0** migration | **21** `D-xx` mới (`D-176`…`D-196`, `TẠM CHỐT` DEC). **Còn:** `P-16` (`NOT-001`), `P-17` (`NOT-002`), `P-18`, `P-19` (người, bắt buộc kế toán), `P-20`, `P-21` (người). Chi tiết: [handover-n59.md](handover-n59.md) |
| 24/11/2026 | **WP-14 đợt C (`N60`, handover `N60`) — `P-16` XONG**: `NOT-001` BA đối chiếu as-built + thêm mục "Đối chiếu mô hình Nhanh" + 7 TC (22→25 BR); `D-193`/`D-191` áp; 3 câu chặn → `D-197` (một module `Notification` chung) · `D-198` (giữ duyệt lô marketing) · `D-199` (chỉ Text + mã `0020`); DOCREV vòng 1 `DOC-CHANGES` (0 P1/6 P2) → vòng 2 `DOC-CHANGES` (0 P1/5 P2 mới) → **vòng 3 kiểm nhanh `DOC-OK`** (0 P1/2 P2/3 P3) ⇒ đủ trình duyệt; cập nhật `nhap-bien-ban-gd3-c.md` (5/6 tài liệu đợt C đề xuất trình duyệt) | `check-feature-doc -WP14` DAT (16/16 · 25/25 BR · 12 câu Open) · `check-agent-artifact -Kind docreview` DAT (`DOC-OK` P1=0/P2=2) · `-Kind decision` DAT (**84** dòng, tới `D-199`) · `kiem-link` PASS 0 chết (**198** tệp · **887** link · **1279** backtick) · `check:mermaid` 212/0 · hooks 48/48 · **0** `.cs`, **0** migration | **3** `D-xx` mới (`D-197`…`D-199`, `TẠM CHỐT` DEC). **Còn:** `P-17` (`NOT-002`), `P-18`, `P-19` (người, bắt buộc kế toán cho `ACC-001`/`REP-002`), `P-20`, `P-21` (người). Chi tiết: [handover-n60.md](handover-n60.md) |

| 24/11/2026 | **WP-14 đợt C (`N61`, handover `N61`) — `P-17` XONG**: `NOT-002` BA đối chiếu as-built + đồng bộ nợ `D-197`/`D-199`/`D-193` + thêm mục "Đối chiếu mô hình Nhanh" (18→24 BR, 14 NFR, TC-33…TC-47); 8 câu §15 có `blocks` → decider (`D-200`/`D-202` `CHUYEN_NGUOI` vì R3-6; `D-201`/`D-203`…`D-207` `TẠM CHỐT`); DOCREV vòng 1 `DOC-CHANGES` (0 P1/3 P2) → vòng 2 `DOC-CHANGES` (0 P1/5 P2 lỗi có sẵn) → **vòng 3 kiểm nhanh `DOC-OK`** (0 P1/1 P2/1 P3) ⇒ đủ trình duyệt; cập nhật `nhap-bien-ban-gd3-c.md` (**6/6** tài liệu đợt C đề xuất trình duyệt) | `check-feature-doc -WP14` DAT (16/16 · 24/24 BR · 2 câu Open = `Q1`/`Q3`) · `check-agent-artifact -Kind docreview` DAT (`DOC-OK` P1=0/P2=1) · `-Kind decision` DAT (**92** dòng, tới `D-207`) · `kiem-link` PASS 0 chết (**890** link · **1312** backtick) · `check:mermaid` 212/0 · hooks 48/48 · **0** `.cs`, **0** migration | **8** `D-xx` mới (`D-200`…`D-207`: 2 `ĐỀ XUẤT` chờ người, 6 `TẠM CHỐT`). **Còn:** `P-18`, `P-19` (người, bắt buộc kế toán cho `ACC-001`/`REP-002`), `P-20`, `P-21` (người). Chi tiết: [handover-n61.md](handover-n61.md) |

| 24/11/2026 | **WP-14 đợt C (`N62`, handover `N62`) — `P-18` XONG**: hoàn thiện `docs/agents/nhap-bien-ban-gd3-c.md` — 6/6 dòng đợt C; cột người review theo `design-doc-guidelines.md` §6 (state machine ⇒ +BA; PII ⇒ +Security; tiền/thuế ⇒ +kế toán); thêm **§3 bảng câu còn chờ người**: **13** câu §15 `Open` + **36** dòng `TẠM CHỐT` + **2** `ĐỀ XUẤT` + bảng P2/P3 sửa cùng WP code. **Không** lập biên bản thật (hàng `P-19`) | `kiem-link` PASS 0 chết (201 tệp · 894 link · 1320 backtick) · `check:mermaid` 212/0 · hooks 48/48 · `check-agent-artifact -Kind decision` DAT (**92** dòng) · `PH check` OK · **0** `.cs`, **0** migration | Không `D-xx` mới. **Còn:** `P-19` (người, duyệt đợt C, bắt buộc kế toán), `P-20`, `P-21` (người). Chi tiết: [handover-n62.md](handover-n62.md) |
| 24/11/2026 | **WP-14 đợt C (`N63`, handover `N63`) — `P-19` XONG**: mở lại `P-19` (trước đó bị đánh `xong` sớm, evidence template, **không** có biên bản) → `duyet-dec` uỷ quyền `D-115`; decider **`D-208`** — **tạm duyệt có điều kiện 3/6**: `REP-001` (đỏ có điều kiện, điều kiện ĐÃ ĐẠT qua `D-114` P2/P4), `NOT-001` (2 P2 #24/#26 sửa trước WP code), `NOT-002` (**thiết kế**, trừ `Q1`/`Q3` + HMAC production — `R3-6`); **"chưa" 3/6**: `REP-002`, `REP-003`, `ACC-001` (đỏ cứng §2.1, người + kế toán ký); lập `docs/bien-ban-tam-duyet-gd3-c-2026-11-24.md` 6 dòng + 6 dòng hàng đợi | `check-agent-artifact -Kind decision` DAT (**93** dòng) · `kiem-link` PASS 0 chết (203 tệp · 913 link · 1335 backtick) · `check:mermaid` 212/0 · hooks 48/48 · `PH check` OK · **0** `.cs`, **0** migration | `D-208` (TẠM CHỐT DEC, 6 dòng: REP-001/NOT-001/NOT-002 tạm duyệt; REP-002/REP-003/ACC-001 chưa). **Chờ người:** biên bản duyệt đợt C (bắt buộc kế toán). **Còn:** `P-20`, `P-21` (người). Chi tiết: [handover-n63.md](handover-n63.md) |
| 24/11/2026 | **WP-14 (`N64`, handover `N64`) — `P-20` XONG (ĐÓNG WP-14)**: cập nhật `docs/features/README.md` §2 theo 3 biên bản **tạm duyệt** — 27/55 người duyệt **giữ nguyên**, **9/18** tạm duyệt có điều kiện, **9/18 "chưa"**, DoR §A.5 GĐ3 **chưa đạt**; decider **`D-209`** — thứ tự **20 WP code GĐ3** `WP-15`…`WP-34` **phủ đủ `D-70`**, `WP-15` nhận 2 nợ `D-71` ④, **giá vốn `WP-17` đứng trước lãi gộp `WP-32`**; thêm mục `§WP-15+` | `check-agent-artifact -Kind decision` DAT (**94** dòng) · `kiem-link` PASS 0 chết (**205** tệp · **920** link · **1344** backtick) · `check:mermaid` 212/0 · hooks 48/48 · `PH check` OK · **0** `.cs`, **0** migration | `D-209` (TẠM CHỐT DEC). **Còn:** `P-21` (người: mở + merge PR, vùng cấm). Chi tiết: [handover-n64.md](handover-n64.md) |
| 24–25/11/2026 | **B5b (`N67`) — 6 vai code + `negative-control.ps1`**: `.claude/agents/{arch,be,fe,qa,rev,ops}.md` (Opus cho `arch`/`rev`, Sonnet cho `be`/`fe`/`qa`/`ops`; 9 mục + mục "Dò code bằng đồ thị") · `tools/agents/negative-control.ps1` (từ chối tệp chưa commit → mutation **đỏ** → khôi phục → **xanh** → ghi testplan; exit 0/1/2/3) · ca hook `qa/arch/ops` **không** ghi `modules/**` · 3 ca cổng `C1–C3` · cập nhật `CLAUDE.md` §9 + plan §14 | build **0 error / 9 warning có sẵn** · unit **1961/1961** · hooks **49/49** · `test-cong-kiem.ps1` **53/53 DAT** · `kiem-link` PASS 0 chết (**223** tệp · **920** link · **1374** backtick) · `check:mermaid` 212/0 · **0** `.cs`, **0** migration | **B5 ĐÓNG** (B5a ở `N66`). `C:` đã dọn còn **26 GB**; build + hooks xanh với `TEMP` mặc định ⇒ **gốc `MSB3491` ở `N64` = ổ `C:` đầy**. PR **#16 MERGED** (`6119c86`), **#17** base `main`. Chi tiết: [handover-n67.md](handover-n67.md) |

| 25/11/2026 | **WP-15 (`N69`→`N70`, handover `N70`) — ĐÓNG**: `D-210` mã `409` `eCommerce:Db:0002` + `D-211`…`D-215`; `probe-don-san` `C10` chờ DLQ `HaWatermark ≥ 1`; `probe-giu-cho-2instance` `P15` phân biệt `Db:0002`/`Lock:0001`/`code=null`; cổng tài liệu `probe-cong-tai-lieu.ps1` + bước trong `kiem-tat-ca` + workflow; **NC-1 DAT** sau commit `dab2567` | **`-DayDu` 20/20 · 836,4 s** (`artifacts/dayDu-wp15.txt`): build 0 error · unit **1966/1966** · integration **96/96** · mermaid 212/0 · DB **25/25** · test cổng **60/60** · **E2E 8/8** · **`P15 DAT`** (`Db:0002`) · `C10 DAT`; NC-1 đột biến **exit 1** → khôi phục **exit 0**; `kiem-link` PASS · **0** migration | `D-210`…`D-215` (TẠM CHỐT DEC). Đóng nợ môi trường "hết bộ nhớ" (`pnpm@10.22.0` toàn cục, bỏ shim corepack). **P2 #1**: diff ~1551 dòng, **không tách PR** (đã chấp nhận). Chi tiết: [handover-n70.md](handover-n70.md) |

| 25/11/2026 | **WP-16/W16-1 (`N71`, chuỗi `gd3-1`) — F0–F2b XONG**: `PRC-002-arch.md` (466 dòng) + DOCREV vòng 2 **`DOC-OK`** 0 P1/0 P2; decider **`D-217`…`D-224`** (cổng tài liệu Q5 bằng tệp gương — `probe-cong-tai-lieu -CheDo Code -WP WP-16` **exit 0**; phạm vi 4 hạng mục vào WP-16; `D-219` guard tổng âm `Reject` mặc định; `D-220` G2 tạm duyệt thiết kế; AQ1–AQ4) | build **0 error/9 warning** · unit **1966/1966** · hooks **49/49** · `kiem-link` **PASS** (235 tệp · 930 link) · **0 `.cs`, 0 migration** | [handover-n71](handover-n71.md) |
| 25/11/2026 | **WP-16/W16-2 (`N72`, chuỗi `gd3-1`) — F3 BE XONG**: 4 hạng mục `D-218` (Setting + hàm đọc thuần; guard tổng âm `Reject`/`Clamp` mã `0010`; ngưỡng số bậc mã `0011`; 4 tên index NND vào `phaiCo`); BA sửa SRS `prc-002` theo `D-225`/`D-226`/`D-228`, DOCREV vòng 3+4 **`DOC-OK`** P1=0; test **+33** (thay tripwire bằng 3a/3b/3c) | build **0 error/9 warning** · unit **1999/1999** · `kiem-tat-ca` **0 (10/10, 331,8 s)** · `kiem-link` **PASS** (237 tệp · 931 link) · **0 migration** | [handover-n72](handover-n72.md) |
| 25/11/2026 | **WP-16/W16-3 (`N73`, chuỗi `gd3-1`) — F3 FE XONG (WP không có FE)**: SRS §10 xác định UI Pricing thuộc `WEB-001`; `apps/admin-web` **23 route** không có Pricing/tier-price, grep mã FE **0** hit; W16-2 không đổi DTO/route ⇒ `schema.d.ts` không đổi | `kiem-tat-ca` **0 (10/10 · 290,5 s)**: build **0 error/9 warning** · unit **1999/1999** · integration **96/96** · FE typecheck **0 lỗi** · Vitest **25/25** · `gen:api` **không đổi schema.d.ts** · DB **25/25** · test cổng **60/60** · `kiem-link` **PASS** (239 tệp · 933 link) · hooks **49/49** · **0 `.cs`, 0 migration** | Không `D-xx` mới. [handover-n73](handover-n73.md) |
| 26/11/2026 | **WP-16/W16-4 (`N74`, chuỗi `gd3-1`) — F4–F5 XONG**: testplan **21/21 BR**; 3 ca app-service `1c/2a/2b` qua `D-229`; **NC 4/4 đỏ→xanh** (NC-1…NC-4); REV **`PASS` P1=0** (1 P2, 5 P3); `D-230` nợ doc BA/ARCH | `kiem-tat-ca` **0 (10/10 · 287,4 s)**: build **0 error/9 warning** · unit **2021/2021** (+22) · integration **96/96** · mermaid **213/0** · FE typecheck **0 lỗi** · Vitest **25/25** · `gen:api` không đổi · DB **25/25** · test cổng **60/60** · `kiem-link` **PASS** (241 tệp · 933 link) · hooks **49/49** · **0 `.cs` sản phẩm, 0 migration** | `D-229`, `D-230` (TẠM CHỐT DEC). [handover-n74](handover-n74.md) |
| 26/11/2026 | **WP-16/W16-5 (`N75`, chuỗi `gd3-1`) — F6–G6 XONG (ĐÓNG WP-16)**: `ops` viết `docs/ops/PRC-002-deploy.md` (6 mục · đường lùi **cấu hình** 3 khoá `Pricing:*` + **revert `100db0a`** · 0 migration nên **không** revert migration/**không** `pg_dump` · cảnh báo `MaxTiersPerSet > 2.147.483.647` ⇒ `OverflowException`); pricing test **144/144** · doc-gate probe WP-16 **XANH**; **push fast-forward** `feat/gd3-wp16-bang-gia` → `tich-hop/gd3` | `kiem-tat-ca -DayDu` **0 (20/20 · 744,3 s)**: build **0 error/9 warning** · unit **2021/2021** · integration **96/96** · mermaid **213/0** · FE typecheck **0 lỗi** · Vitest **25/25** · `gen:api` không đổi · DB **25/25** · test cổng **60/60** · ADDR **17/17** · CUS **26/26** · PAY **30/30** · Playwright **8/8** · `kiem-link` **PASS** · hooks **49/49** · G4 **DAT** (BR 21/21) · G5 **DAT** (PASS, P1=0) · W16-5 **0 `.cs`, 0 migration** | Không `D-xx` mới. [handover-n75](handover-n75.md) |
| 26/11/2026 | **WP-18/W18-0 (`N76`, chuỗi `gd3-1`) — CỔNG TÀI LIỆU XONG (0 P2 cả hai tài liệu)**: `docrev` kiểm nhanh v0.5 — `ADDR-001` **DOC-OK 0 P1/0 P2/19 P3** (2 P2 vòng 2 đóng); `ADDR-002` **DOC-OK 0 P1/0 P2/2 P3** sau khi BA sửa P2 `K1` (§1 Post-condition + §3.2 R5 thôi gán `Tra` xuôi cho vận đơn) + DOCREV kiểm nhanh bổ sung; `D-131`… → **`D-231`** (TẠM CHỐT: `qa` sửa `tools/probe-cong-tai-lieu.ps1` nhận mã 4 chữ cái `ADDR-*` + 2 ca `-TuKiem`; 1 tệp, lùi = revert 1 commit) | `check-feature-doc -WP14` **DAT** (16/16 heading · 16/16 BR · đối chiếu Nhanh) · `check-agent-artifact -Kind docreview` **DAT** · `probe -CheDo Code -WP WP-18/20/21` **exit 0** · `probe -TuKiem` **10/10** · `test-cong-kiem -ChiKiemMoc` **54/54** · NC-N76-1 **exit 1 (đỏ) → exit 0 (xanh)** · `kiem-link` **PASS** (244 tệp · 940 link · 0 chết) · mermaid **213/0** · hooks **49/49** · W18-0 **0 `.cs`, 0 migration** | `D-231` (TẠM CHỐT). [handover-n76](handover-n76.md) |
| 26/11/2026 | **WP-18/W18-2 (`N78`, chuỗi `gd3-1`) — F3 BE XONG**: `be` ×2 thi công `ADDR-001` (guard BR-010, hàng rào 5, mã `0009`, `AddrCatalogSyncRuns` + `SourceHash`, 3 endpoint danh sách, `ImportDatasetAsync`/`CatalogSyncRunDto`, cache TTL 86400 + ETO, permission `Catalog.Import`, metric, map 8 mã) và `ADDR-002` (`ICarrierAddressResolver` + DTO hẹp, `TraNguoc` fail-closed, `GetUnmappedCodesAsync`, hàng rào 3, mã `0509`/`0510`, 2 bảng lô + bảng con `text[]` unique `NULLS NOT DISTINCT`, map 6 mã, permission `CarrierMapping.Verify`); `qa` +**68** test đơn vị; `ba` đồng bộ 2 SRS theo as-built; 2 migration EF **expand-only**; `pg_dump` **197.121 B** trước khi áp | `kiem-tat-ca` **0 (10/10 · 304,3 s)** · build **0 error/9 warning** · unit **2089/2089** (+68) · integration **96/96** · FE typecheck/Vitest/`gen:api` **không đổi** · NC-N78-1/2 **đỏ→xanh** · `check-feature-doc` **DAT ×2** (14/14, 16/16 BR) · `kiem-link` **PASS** (250 tệp · 947 link · 1505 backtick) · mermaid **213/0** · hooks **49/49** · DB thật: 3 bảng + unique `NULLS NOT DISTINCT`, migration history `Add_Addr001`/`Add_Addr002` | Không `D-xx` mới. [handover-n78](handover-n78.md) |
| 26/11/2026 | **WP-18/W18-1 (`N77`, chuỗi `gd3-1`) — F0–F2b XONG (ARCH + DOCREV + G2)**: `arch` viết `ADDR-001-arch.md` (475 dòng: bảng lô `AddrCatalogSyncRuns` + `SourceHash` + guard BR-010 + hàng rào `ParentCode ⇒ 0008` + cache `D-242` + endpoint danh sách + `ImportDatasetAsync` + permission + map 8 mã) và `ADDR-002-arch.md` (482 dòng: `AddrCarrierSyncRuns` + bảng con chưa ánh xạ + `D-84/86/87/90/91` + 2 mã `0509/0510` + tra ngược `ResolveCarrierAddressCodeAsync` + permission + map 6 mã); DOCREV vòng 1 **DOC-CHANGES** (1 P1 + 8 P2) → ARCH sửa + áp `D-232…D-242` → vòng 2 **DOC-OK** (`ADDR-002` 0 P1 / 1 P2 / 16 P3; `ADDR-001` 0 P1 / 0 P2 / 6 P3); `D-243` **G2 tạm duyệt** (M2, khuôn `D-220`) | `probe -CheDo Code -WP WP-18` **exit 0** · `check-agent-artifact -Kind docreview` **DAT ×2** (P1=0; P2=0/1) · `-Kind decision` **DAT** (115 dòng) · `kiem-link` **PASS** (249 tệp · 945 link · 1501 backtick · 0 chết) · mermaid **213/0** · hooks **49/49** · build **0 error/9 warning** + unit **2021/2021** (baseline không đổi — **0 `.cs`**, 0 migration) | `D-232`…`D-243` (TẠM CHỐT DEC). [handover-n77](handover-n77.md) |
| 26/11/2026 | **WP-18/W18-3 (`N79`, chuỗi `gd3-1`) — F3 FE XONG (WP không có FE contract-first)**: `fe` + ORCH đo: `pnpm gen:api` **51/51 path `/api/v1`**, **0** path address, `schema.d.ts` **không đổi**; `src/eCommerce.HttpApi/Controllers` **8** controller, **0** Address; `apps/admin-web` **0** route address; BFF chỉ `/api/v1`; W18-2 **0** tệp `Controllers/**`/`api-client/**`/`admin-web/**`; SRS §10 gán màn cho `WEB-001`, §6.2 route ABP `/api/app`, `ADDR-002-arch` §7.1 "không có ca UI" ⇒ **`done kèm lý do`**, **0 dòng code FE**; `decider` ghi `D-244` (done-kèm-lý-do) · `D-245` (giữ `/api/app`, v1 = hàng BE/contract WP màn hình) · `D-246` (nợ hardening BFF) · `D-247` (đề xuất WP `WEB-001`, **chuyển người**) | `probe -CheDo Code -WP WP-18` **exit 0** · `pnpm gen:api` **51/51 · schema không đổi** · `kiem-tat-ca` **0 (10/10 · 279,6 s)**: build **0 error/9 warning** · unit **2089/2089** · integration **96/96** · FE typecheck/Vitest OK · DB **25/25** · test cổng **60/60** · `check-agent-artifact -Kind decision` **DAT** (116 dòng) · `kiem-link` **PASS** (948 link · 1516 backtick) · mermaid **213/0** · hooks **49/49** · **0 `.cs`, 0 test mới, 0 migration** | `D-244`…`D-247` (3 TẠM CHỐT + 1 ĐỀ XUẤT DEC). [handover-n79](handover-n79.md) |
| 26/11/2026 | **WP-18/W18-4 (`N80`, chuỗi `gd3-1`) — F4 QA + F5 REV XONG**: 2 testplan mới phủ **14/14** + **16/16** BR; `schema.sql` regenerate từ migration (+3 bảng, 0 dòng xoá) + `PostgreSqlSchemaTests` **93 → 94**; harness `CarrierAddressSyncWorker` (7 ca, `PeriodicBackgroundWorkerContext` thật) + app service `GetUnmappedCodesAsync`/`ImportDatasetAsync` + cache `D-242` (20 ca) + integration PG; **11 negative control đỏ→xanh** (ADDR-001 8, ADDR-002 3); probe `tools/probe-dia-chi.ps1` **10/10** gồm P2 `D-233` (mã hãng lạ ⇒ **404** + `eCommerce:Address:0501`); NFR-001 p95 nguội **1,353 ms** / nóng **< 0,01 ms**; đóng **28 P3** `-arch`; vá `ADDR-001-AQ6` (`D-251`: `0605`/`BiChan` thay `0002`/`Loi`); REV **`PASS` P1=0** (P2=3, P3=9) | `kiem-tat-ca` **0 (10/10 · 305,2 s)**: build **0 error/9 warning** · unit **2166/2166** (+77) · integration **106/106** (+10) · mermaid **213/0** · FE typecheck/Vitest OK · `gen:api` không đổi · DB **25/25** · test cổng **60/60** · `check-agent-artifact -Kind testplan` **DAT ×2** · `-Kind review` **DAT ×2** (PASS, P1=0) · `-Kind decision` **DAT** (116 dòng) · NC **11/11** · probe **10/10** · **0 migration, 0 `pg_dump`** | `D-248`…`D-251` (TẠM CHỐT DEC). [handover-n80](handover-n80.md) |
| 26/11/2026 | **WP-18/W18-5 (`N81`, chuỗi `gd3-1`) — F6 OPS + G6 XONG (ĐÓNG WP-18)**: `ops` viết `docs/ops/ADDR-001-deploy.md` + `ADDR-002-deploy.md` (6 mục: `Address:Cache:*`/`Address:Sync:*`/`Address:CarrierSync:*`, 2 migration expand-only đã áp dev N78, **đường lùi** = tắt cờ + `git revert` commit WP-18, mục "Kiểm trước go-live (403)" theo `D-252`); cửa sổ `be` đóng **2 P2** REV `ADDR-001` (`D-250` xoá overload `ApDung` 1 tham số; P2 #2 giới hạn `SourceName ≤ 512` ở `CatalogSyncRun.Ghi` + validate trước khoá ⇒ 400); `qa` **+4** ca + ca `D-252` reflection `[Authorize]` `XacMinhAsync` + 4 NC; `ba`/`arch` đồng bộ SRS/arch as-built; `decider` `D-252`; **push fast-forward** `feat/gd3-wp18-dia-chi` → `tich-hop/gd3` | `kiem-tat-ca -DayDu` **0 (20/20 · 708,8 s)**: build **0 error/9 warning** · unit **2170/2170** (+4) · integration **106/106** · mermaid **213/0** · FE typecheck/Vitest · `gen:api` không đổi · DB **25/25** · test cổng **60/60** · ADDR/CUS/PAY/SAL · oversell · outbox ×2 · Playwright **8/8**; NC-9..11/NC-6 **đỏ→xanh**; `check-agent-artifact -Kind testplan` **DAT ×2**; `check-feature-doc` **DAT**; `kiem-link` PASS; **0 migration, 0 `pg_dump`** | `D-252` (TẠM CHỐT DEC). [handover-n81](handover-n81.md) |
| 26/11/2026 | **WP-22/W22-0 (`N82`, chuỗi `gd3-1`) — CỔNG TÀI LIỆU XONG (đóng 2 P2 #24/#26, `D-208` (6))**: `ba` sửa `NOT-001` lên `v0.5` (hàng 15 `DangBanGiao → HetHan` guard `now > ExpiresAtUtc` event `Expired` + sơ đồ §3/§12/TC-05 nhất quán; nhánh đua `23505` ghi lần hai trong UoW MỚI `requiresNew` sau rollback + TC-30 assert đúng 1 `QueueEvent(Suppressed)`); `docrev` kiểm nhanh **vòng 4** `DOC-OK` **0 P1/0 P2** (#25 P3 đóng kèm, 1 P3 mới #29); `decider` `D-253` xác nhận điều kiện đạt, tạm duyệt có hiệu lực trên nhánh WP; ORCH cập nhật biên bản tạm duyệt đợt C | `check-feature-doc -WP14` **0** (16/16 heading · 25/25 BR · 12 câu Open) · `check-agent-artifact -Kind docreview` **0** (DOC-OK · P1=0 · P2=0) · `check-agent-artifact -Kind decision` **0** (116 dòng; `D-253` 0) · `probe-cong-tai-lieu -CheDo Code -WP WP-22` **0** · `kiem-link` PASS (951 link · 1602 đường dẫn) · mermaid **213/0** · hooks **49/49**; build/test bỏ qua có lý do "WP thuần tài liệu"; **0 migration, 0 `.cs`** | `D-253` (TẠM CHỐT DEC). [handover-n82](handover-n82.md) |
| 26/11/2026 | **WP-22/W22-1 (`N83`, chuỗi `gd3-1`) — F0–F2b XONG (ARCH + DOCREV + G2)**: `arch` viết `NOT-001-arch.md` (**686** dòng: module mới `modules/Notification/` 4 project, ranh giới ETO một chiều, template/version bất biến + ghim version, queue item 15 hàng state machine ném lỗi, queue event append-only, renderer thuần, `NotificationLockKeys`, dải `0010–0020`, `DbSet` + `ConfigureNotification` một lần, unique `NULLS NOT DISTINCT` + partial index soft-dedupe, chống trùng 5 lớp (hàng rào thật `UPDATE … WHERE Status = ChoGui` + UoW mới `requiresNew` sau `23505`), job `AbpBackgroundJobs` tránh starvation, permission, PII `D-191`, metric `All` + `XC-005`, FE contract, migration expand-only + đường lùi, 7 negative control, xử lý **15 P3**, 11 `docDeviation`, 7 câu mở); DOCREV vòng 1 **DOC-CHANGES** (**2 P2**: pha (C) bỏ sót `Moi→ChoGui`; `INotificationSender` hai định nghĩa ARCH vs `NOT-002`) → ARCH sửa → vòng 2 **`DOC-OK` 0 P1/0 P2/1 P3** (P3 = TC-01 3 hay 4 event, chốt bằng `D-265`); `decider` **`D-254`…`D-266`** (7 `AQ` + ratify `docDeviation` + P3 + **G2 tạm duyệt** `D-266`) | `probe-cong-tai-lieu -CheDo Code -WP WP-22` **0** · `check-agent-artifact -Kind docreview` **DAT** (DOC-OK · P1=0 · P2=0) · `-Kind decision` **DAT** (116 dòng; `D-266` 0) · `kiem-link` **PASS** (955 link · 1619 backtick · 0 chết) · mermaid **213/0** · hooks **49/49**; baseline build **0 error/9 warning** + unit **2170/2170**; đóng phiên build/test bỏ qua có lý do "hàng F0–F2b, 0 `.cs`"; **0 migration, 0 `.cs`** | `D-254`…`D-266` (TẠM CHỐT DEC). [handover-n83](handover-n83.md) |
| 26/11/2026 | **WP-22/W22-3 (`N85`, chuỗi `gd3-1`) — F3 FE XONG (hợp đồng `/api/v1` 9 op + 5 màn Web Admin)**: `decider` **`D-268`** (lọc template Marketing **client-side**; `GetTemplatesForBatch` = nợ BE, không chặn; hàng contract v1 đi trước `fe`); `be` mở **3 controller tường minh** (`NotificationTemplates/Queue/Batches`) **9 op** `/api/v1/notifications/*`, `[Authorize]` cấp class + permission method riêng, swagger live **8 path/9 operation**, ẩn danh **401**; `fe` ×2 dựng **5 màn** (`templates` list/new/[id], `queue` + panel timeline, `batches` + thẻ duyệt) — preview `<pre>` `textContent` (`D-199`), người nhận `recipientMasked` (`D-191`), lọc Marketing client + cảnh báo thiếu trang, ẩn nút duyệt người tạo, menu 3 mục; `pnpm spec:snapshot` **51 → 59 path**, `schema.d.ts` **+710** thuần bổ sung, **không drift**; `ba` SRS **v0.6 → v0.7** as-built (header vs appender, §6.1/§6.2/§9/§10, NFR-009/TC-28, §15.5 nợ N1–N6) | build **0 error/9 warning** · unit **2251/2251** · `typecheck` **0** · Vitest **45/45** (+20) · `pnpm build` **đỏ do môi trường** (EPERM symlink `output: standalone` trên Windows; compile/type-check/render xanh) · `pnpm lint` **không có script** (nợ `N70`) · `kiem-tat-ca` **0 (10/10 · 387,4 s)** · NC-N85-1 **đỏ→xanh** · `check-feature-doc` **DAT** (16/16 · 25/25 BR) · `check-agent-artifact -Kind docreview` **DAT** (DOC-OK · P1=0/P2=0) · `-Kind decision` **DAT** (`D-268`) · `kiem-link` **PASS** · hooks **49/49**; **0 migration, 0 sửa `modules/**`** | `D-268` (TẠM CHỐT DEC). [handover-n85](handover-n85.md) |
| 26/11/2026 | **WP-22/W22-4 (`N86`, chuỗi `gd3-1`) — F4 QA + F5 REV XONG**: `qa` ×2 viết `docs/tests/NOT-001-testplan.md` phủ **25/25 BR** + **32 ca mới** (`NotificationMaskerTests`, `NotificationBoundary/Security/Persistence/ReportingIsolation`, `PostgreSqlNotificationTests` TC-19/TC-30, TC-37/TC-38/TC-39; ghi trung thực **13 mục "chưa kiểm được"**); phát hiện lệch PII ⇒ `decider` **`D-269`** (mask thật, một cửa ghi, luật hẹp) ⇒ `be` cài `NotificationMasker` (Domain) + fingerprint trên giá trị đã che; `ba` SRS **v0.7 → v0.8** (luật che + as-built) → `docrev` ×2 **`DOC-OK` 0/0/0**; `rev` **`VERDICT: PASS`, P1=0** (2 P2 giao WP-23/env). ORCH tự chạy **NC-1/NC-4/NC-5/NC-8** đỏ→xanh | build **0 error/9 warning** · unit **2283/2283** (+32) · integration **110/110** (+4) · `kiem-tat-ca` **0 (10/10 · 345,2 s)** · `check-agent-artifact` testplan/docreview/review **DAT** · `check-feature-doc` **DAT** · `kiem-link` **PASS** · hooks **49/49**; **0 migration** | `D-269` (TẠM CHỐT DEC). [handover-n86](handover-n86.md) |
| 26/11/2026 | **WP-22/W22-2 (`N84`, chuỗi `gd3-1`) — F3 BE XONG (module `Notification` thật)**: `be` ×3 thi công **4 project** `modules/Notification/` (domain 15 hàng state machine ném `0011`, template/version bất biến, appender 5 lớp chống trùng + `23505` UoW `requiresNew`, renderer thuần, `INotificationSender` canonical + `ChuaCoKenhGuiSender` fail-closed, worker 3 pha, seed 3 template, 9 permission, 7 metric, map mã `0010–0020`); **sửa 2 lỗi**: appender đưa mục có `BatchId` sang `ChoDuyet` (hàng 2) + thêm `SendManualRequest.BatchId`; `qa` +**81** test đơn vị (`MetricsTests` thêm `_backlog` theo `D-267`; `PostgreSqlSchemaTests` **94 → 101** + 7 tên NND; `schema.sql` regen; `RangBuocChuaAnhXa` +23); `ba` ×3 đồng bộ SRS `v0.6` (9 `docDeviation` + P3 #1-v2 + 15 P3; đóng 2 P2 docrev; `XC-005` §5.1 + SRS §6.1); `docrev` ×2 **`DOC-OK` 0 P1/0 P2**; `decider` **`D-267`**; migration EF **expand-only** `20261126132231_Added_Notification_Template_Queue` (**5 bảng**, **7** unique `NULLS NOT DISTINCT`, seed idempotent 3 template), `pg_dump` **457.042 B** trước khi áp | build **0 error/9 warning** · unit **2251/2251** (+81) · integration **106/106** · `kiem-tat-ca` **0 (10/10 · 383,6 s)** · NC-N84-1 **đỏ→xanh** · `check-feature-doc` **DAT** (16/16 · 25/25 BR) · `check-agent-artifact -Kind docreview` **DAT** (P1=0/P2=0) · `-Kind decision` **DAT** (`D-267`) · `kiem-link` **PASS** · mermaid **213/0** · hooks **49/49** · DB thật 5 bảng + 3 template | `D-267` (TẠM CHỐT DEC). [handover-n84](handover-n84.md) |

| 27/11/2026 | **WP-22/W22-5 (`N87`, chuỗi `gd3-1`) — F6 OPS + G6 XONG (ĐÓNG WP-22)**: `ops` viết `docs/ops/NOT-001-deploy.md` (7 mục: 4 project + 1 migration 5 bảng/7 NND/seed 3 template · `Notification:EnableDispatch`=false + lease/size · thứ tự pg_dump→DbMigrator→BE→FE · **đường lùi** tắt cờ + `git revert` 5 commit, KHÔNG Down/DROP · 9 route→quyền · mã lỗi 0010–0020 · nợ P2 mask `D-269`/`PeriodSeconds` chưa đọc/thiếu harness/EPERM); **push fast-forward** `feat/gd3-wp22-thong-bao` → `tich-hop/gd3` | `kiem-tat-ca -DayDu` **0 (20/20 · 745,7 s)**: build **0 error** · unit **2283/2283** · integration **110/110** · mermaid 213/0 · FE typecheck/Vitest OK · `gen:api` không đổi · DB 25/25 · test cổng 60/60 · ADDR/CUS/PAY/SAL · oversell · outbox ×2 · giữ chỗ 2 instance · Playwright 8/8; `kiem-link` PASS; hooks 49/49; **0 migration** | Không `D-xx` mới. [handover-n87](handover-n87.md) |

**Ô checklist §12 cập nhật:** đội 3 người ✅ · dev transport **http 54275** ✅ · plan đã duyệt ✅ · baseline đã chạy ✅

