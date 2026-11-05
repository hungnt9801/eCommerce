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
| 05/11/2026 | **Đóng nợ CI `packageManager` (`N17`) — và KHÔNG merge PR #1**: bỏ **1 dòng** `packageManager` ở `package.json` gốc + sửa comment `Dockerfile` ⇒ `yarn`/`abp install-libs` chạy lại được; ⛔ **không** bỏ bước 7 khỏi `ci.yml` | **Bước 7 `ci` XANH trên CI thật** (run `41955331290`, job **294 s**; trước: đỏ sau **38 s**) · `docker-build` **xanh 6/6** (run `41955331294`, 66 s) · `yarn --version` **exit 1 → exit 0** · `abp install-libs` **exit 0** (10,2 s) · `pnpm install --frozen-lockfile` **exit 0** · cổng nhẹ **9/9 (228,5 s)** · `-DayDu` **19/19 (455,9 s)** · test cổng **34/34** (full **40/40**) · DB **25/25** · **0 migration** | `D-60` (bỏ field; pnpm ghim **2 chỗ**), `D-61` (**KHÔNG merge**: `ci` đỏ ở **⑦/Y1** vì 3 probe `N12`–`N14` chưa commit là **mới so với `main`** ⇒ merge làm `main` đỏ; + nửa FE/API 404). Chi tiết: [handover-n17.md](handover-n17.md) |


**Ô checklist §12 cập nhật:** đội 3 người ✅ · dev transport **http 54275** ✅ · plan đã duyệt ✅ · baseline đã chạy ✅

