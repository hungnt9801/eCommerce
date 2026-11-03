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

---

### WP-08 — Reporting (S5, BE 6 + FE 3)

- Schema `report`: `FactSalesLine`, `FactInventorySnapshot`, `DimDate/Product/Store/Staff`, `AggDaily*`.
- Job tổng hợp theo watermark (chạy lại được, backfill theo khoảng).
- API: `GET /reports/revenue`, `/reports/orders/summary`, `/reports/inventory/summary`; Dashboard 4 KPI.
- FE: trang Dashboard + 2 báo cáo (bảng + chart + export CSV).

**DoD:** tổng doanh thu báo cáo **=** `SUM(RetailBills + Orders)` trong cùng kỳ (test so sánh);
job chạy lại 2 lần không nhân đôi dữ liệu.

---

### WP-09 — Test & cổng (xuyên suốt, 5 ngày)

- Playwright 8 luồng: login · tạo SP · nhập kho · bán lẻ · in · đổi trạng thái đơn · kết ca · export.
- `dotnet test` + `pnpm test` + `pnpm e2e` trong CI; job `openapi-drift`.
- Mở rộng `tools/kiem-tat-ca.ps1` thêm bước FE (không phá baseline cũ).

### WP-10 — Ops (2 ngày)

- `docker-compose.yml`: thêm service `admin-web` (build production, `depends_on` host).
- `.env.example`: `API_BASE_URL`, `AUTH_SECRET`, `AUTH_ISSUER`, `NEXT_PUBLIC_*`.
- Health: `/api/health` (BFF) → gọi `/health-status` của Host.

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

**Ô checklist §12 cập nhật:** đội 3 người ✅ · dev transport **http 54275** ✅ · plan đã duyệt ✅ · baseline đã chạy ✅

