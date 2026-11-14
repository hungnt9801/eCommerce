# KẾ HOẠCH TÁCH 4 MODULE GĐ1 — `Catalog` · `Inventory` · `Sales` · `Reconciliation`
> 🗄️ **LỊCH SỬ — KHÔNG dùng làm điểm vào** (gắn nhãn 14/11/2026 theo `D-69`). Tệp ghi lại một thời kỳ/việc **đã qua**; **số đo, trạng thái và 'việc tiếp theo' trong tệp đã cũ** — kể cả câu *'đọc tệp này trước'* bên dưới. Điểm vào hiện tại: `CLAUDE.md` → `docs/nhanh/README.md` → handover mới nhất `docs/nhanh/handover-n*.md` → `docs/nhanh/07-ke-hoach-trien-khai.md`. Xếp loại tài liệu: `docs/agents/ke-hoach-he-thong-agent.md` §14.1.


> **Trạng thái:** 4 module GĐ1 **đã tách** (24/10/2026); `S17` (29/10/2026) **đóng nốt phần hợp đồng** —
> xem §3.1 để biết dịch vụ nào còn ở host và **vì sao**.
> **Ngày:** 24/10/2026 · **Cập nhật:** 29/10/2026 (`S17`) · **Căn cứ:** `docs/audit-gd1.md` §2 (đo được: 9/16 module; 4 module GĐ1 nằm trong `src/`).
> **Mục tiêu:** đưa 4 bounded context GĐ1 ra `modules/` **không đổi hành vi, không đổi lược đồ, không đổi route**.

---

## 1. Mục tiêu & không phải mục tiêu

**Mục tiêu**
- `modules/Catalog`, `modules/Inventory`, `modules/Sales`, `modules/Reconciliation` — mỗi module **Domain + Application
  + Application.Contracts + EntityFrameworkCore**.
- **0** tệp entity/app service của 4 module còn nằm trong `src/`.
- **Cổng chặn tái phát**: test kiến trúc đỏ nếu ai thêm lại entity module vào `src/`, hoặc module tham chiếu `src/eCommerce.Domain`.
- `-DayDu` xanh, **cùng số ca** (1422 unit · 71 integration · 133 E2E · 25/25 DB · NND 53 · 62 quyền · 41 migration).

**KHÔNG làm trong đợt này**
- ❌ Không đổi bảng/cột/index ⇒ **0 migration** (đây là điều kiện để rollback rẻ).
- ❌ Không đổi route API (`/api/app/...`) hay route web.
- ❌ Không đổi hành vi nghiệp vụ (kể cả các lỗi đã biết — sửa lỗi là việc khác, PR khác).
- ❌ Không gộp với việc thêm entity mới (`Brand`, `CostPrice`, `OrderStatusHistory`…).
- ❌ Không "big bang" một PR.

---

## 2. Đích đến — cấu trúc & luật phụ thuộc

```
modules/
├─ Catalog/
│  ├─ eCommerce.Catalog.Domain/                 Product · ProductVariant · Category · CatalogAttribute
│  │                                            · ProductCategory · ProductMedia · ProductChannelVisibility
│  │                                            · SinhSlug · CategoryTreeService · VariantCombinationGenerator
│  ├─ eCommerce.Catalog.Application.Contracts/  DTO + IProductAppService…
│  ├─ eCommerce.Catalog.Application/            ProductAppService · CategoryAppService · ProductMediaAppService…
│  └─ eCommerce.Catalog.EntityFrameworkCore/    CatalogDbContextModelCreatingExtensions
├─ Inventory/                                   Warehouse · InventoryItem · StockLedgerEntry · StockDocument*
│  │                                            · StockReservation · InvChannelLimit · StockPostingService…
│  ├─ …Application.Contracts/                   (MỚI — Sales cần đọc tồn/giữ chỗ qua hợp đồng)
│  └─ …
├─ Sales/                                       Order · OrderLine · MarketplaceOrderIntake · MarketplaceEventLog
│  │                                            · Checkout/ · Marketplace/ · Sales/ app services
│  └─ …
└─ Reconciliation/                              ReconRun · ReconRunMismatch · DoiSoat* · NguongLech
   └─ …                                        (đối soát THANH TOÁN vẫn ở PaymentHub đợt này — xem §7)
```

**Luật phụ thuộc (cưỡng chế bằng test, không bằng lời):**

| Luật | Cho phép | Cấm |
|---|---|---|
| L1 | `Module.Domain` → `shared/eCommerce.Kernel`, `src/eCommerce.Domain.Shared` | → `src/eCommerce.Domain` |
| L2 | `Module.Application` → module của mình + `*.Application.Contracts` của module khác | → `src/eCommerce.Domain` / `src/eCommerce.Application` |
| L3 | `src/*` (host) → mọi module | module → host |
| L4 | `Module.*.Contracts` **không** tham chiếu `Domain` của module khác | vòng lặp project |
| L5 | Mỗi entity module **có `DbSet`** trên `eCommerceDbContext` (quy ước #5) | repository "ngầm" |

> ⚠️ **Giữ nguyên namespace** (`eCommerce.Catalog`, `eCommerce.Inventory`, `eCommerce.Orders`) để **không đổi tên bảng**
> và **không phải sửa** `using` trong 200+ tệp. Chỉ **đổi project/assembly** chứa class.
> 📌 Đây là quyết định quan trọng nhất của kế hoạch: nó biến việc tách module thành **di chuyển tệp + csproj**,
> không phải viết lại code.

---

## 3. Thứ tự tách (từ dễ → khó, mỗi bước một PR xanh)

| Bước | Module | Vì sao thứ tự này | Ước lượng |
|---|---|---|---|
| **M1** | `Reconciliation` | Ít phụ thuộc nhất (chỉ đọc Inventory + PaymentHub qua hợp đồng); làm trước để **học quy trình** | **3–4 pd** — ✅ **XONG 24/10/2026** (4 project + mapping EF; ✅ đã gỡ nợ đọc `Inventory.Domain` — dùng `IStockBalanceReader`/`IWarehouseDirectory`) |
| **M2** | `Inventory` | Nền của Sales; có nhiều invariant + service thuần; chưa có `Contracts` ⇒ tạo mới | **5–7 pd** — ✅ **XONG 24/10/2026**: 4 project; Domain/Contracts/Application **và mapping EF** đã chuyển; recon còn host (sang Reconciliation); 2 app service host có lý do |
| **M3** | `Catalog` | Nhiều entity nhưng **ít** phụ thuộc ra ngoài (Kernel + Domain.Shared) | **4–6 pd** — ✅ **XONG 24/10/2026**: 4 project; Domain/Contracts/Application **và mapping EF** đã chuyển (`ConfigureCatalog`, migration **RỖNG**); 2 tệp host (`ChannelDirectory`, `HienThiKenhAppService`) **cố ý** ở lại vì đọc ChannelHub |
| **M4** | `Sales` | Nặng nhất: phụ thuộc **3** module (Catalog, Inventory, Pricing, Customers) + Marketplace + Checkout | **8–12 pd** — ✅ **XONG 24/10/2026**: 4 project; Domain/Contracts/Application **và mapping EF** đã chuyển; 1 worker host có lý do |
| | | **Tổng** | **20–29 pd** (+ buffer 25% ⇒ **25–36 pd**) |

> 🔑 **Vì sao Reconciliation trước:** nếu tách Sales trước, `Sales.Application` sẽ tham chiếu ngược vào `src` cho
> Inventory/Catalog ⇒ tạo **nợ mới** đúng lúc đang trả nợ cũ. Làm theo thứ tự phụ thuộc: **lá → gốc**.

### 3.1 Trạng thái L2 sau `S17` — hợp đồng cho dịch vụ còn ở host (29/10/2026)

> **L2 của tài liệu này** = `Module.Application` → module của mình + `*.Application.Contracts` của module khác;
> **cấm** → `src/eCommerce.Domain` / `src/eCommerce.Application`. `S17` kiểm đúng dòng L2 ở **chiều ngược**:
> *dịch vụ chưa rời host được thì hợp đồng của nó **phải** nằm ở module sở hữu, và host phải ghim DI tường minh.*

**Đo được trước `S17`** (không phải lời khai): 4/5 hợp đồng **đã** nằm trong `Application.Contracts` của module
từ đợt tách module (commit `e2c2785`, 24/10/2026) — `IChannelDirectory`, `IHienThiKenhAppService` trong
`eCommerce.Catalog.Application.Contracts`; `IChinhSachHanMucAppService`, `IWarehouseAppService` trong
`eCommerce.Inventory.Application.Contracts`. ⇒ Việc còn lại của `S17` là **hiện thực + đăng ký DI + cổng chặn**,
không phải "chuyển hợp đồng lần nữa".

| Dịch vụ | Hợp đồng (assembly) | Hiện thực sau `S17` | Vì sao còn/đi |
|---|---|---|---|
| `WarehouseAppService` | `IWarehouseAppService` — Inventory Contracts | ✅ **module** `eCommerce.Inventory.Application` | Chỉ cần `IRepository<Warehouse>` + DTO + `eCommerceConsts` ⇒ **không** có lý do ở host. Đã tách khỏi tệp cũ; `InventoryReconciliationAppService` ở lại host |
| `ChannelDirectory` | `IChannelDirectory` — Catalog Contracts | **host** (có `[ExposeServices]`) | Đọc entity `Channel` của `ChannelHub.Domain`; module **không được** tham chiếu Domain module khác |
| `HienThiKenhAppService` | `IHienThiKenhAppService` — Catalog Contracts | **host** (có `[ExposeServices]`) | Đọc `ChannelListing` của `ChannelHub.Domain` (guard `0209`) |
| `ChinhSachHanMucAppService` | `IChinhSachHanMucAppService` — Inventory Contracts | **host** (có `[ExposeServices]`) | Đọc `Channel` (ChannelHub) **và** `ReconRun` (Reconciliation) — là **điểm nối** giữa 3 module |
| `MarketplaceReconciliationWorker` | *(không có hợp đồng — xem §3.1.2)* | **host** | Không module nào tham chiếu trực tiếp; nó đọc `DeadLetterMessage` (Messaging.Domain) + `MarketplaceOrderIntake` (Sales.Domain) |

#### 3.1.1 Cổng chặn tái phát — `HopDongHostTests`

`test/eCommerce.Tests/CrossCutting/HopDongHostTests.cs` (**18 ca**, thêm ở `S17`) khoá **bốn** bất biến đo được:

| # | Bất biến | Cách kiểm |
|---|---|---|
| H1 | Hợp đồng nằm trong `Application.Contracts` của module sở hữu | `typeof(I*).Assembly.GetName().Name` |
| H2 | Hiện thực ở đúng assembly đã chốt (`Warehouse` → module; 3 lớp + worker → host) | `typeof(lớp).Assembly` |
| H3 | Không tệp `.cs` nào dưới `modules/` tham chiếu **lớp hiện thực** ở host (chỉ được dùng `I*`) | quét văn bản nguồn **sau khi bỏ chú thích** |
| H4 | Không `.csproj` nào dưới `modules/` tham chiếu `src/eCommerce.Application` | quét csproj |
| H5 | Cặp `I* → lớp hiện thực` có `[ExposeServices]` (DI **tường minh**) | reflection trên `ExposeServicesAttribute` |

📌 **Vì sao H5 dùng `[ExposeServices]` chứ không `services.AddTransient`:** đăng ký thủ công tạo **descriptor thứ hai**
và có nguy cơ bỏ qua interceptor của ABP; `[ExposeServices]` đi **qua đúng** đường đăng ký của ABP — cùng khuôn
`XuLyKetQuaCongService` (`S5`, lỗi thật `HTTP 500` vì tên interface không theo quy ước). Đo được: route
`/api/app/warehouse*` **không đổi** (4 đường dẫn); `-DayDu` xanh.

#### 3.1.2 Vì sao `MarketplaceReconciliationWorker` **không** có hợp đồng

Nó là `AsyncPeriodicBackgroundWorkerBase` — **không** phải dịch vụ được ai **gọi**; chỉ host đăng ký nó trong
`eCommerceApplicationModule.OnPostApplicationInitializationAsync`. Thêm một interface rỗng chỉ để "có hợp đồng"
sẽ là **hợp đồng giả** (không ai resolve ⇒ không kiểm được gì). 🔒 Điều `S17` cần — *"không module nào tham chiếu
trực tiếp"* — được khoá bằng H3. Muốn chuyển nó vào `Sales.Application` thì phải mở hợp đồng đọc cho
`Messaging`/`Sales` trước; **không** làm trong `S17` vì sẽ đổi phạm vi và không đổi hành vi nào.

#### 3.1.3 Phần **còn lại** của L2 (không thuộc `S17`)

1. **Hợp đồng đọc của `ChannelHub`** (`IChannelListingReader`…) để chuyển `ChannelDirectory`/`HienThiKenhAppService`
   vào `Catalog.Application`; tương tự cần hợp đồng đọc `ReconRun` cho `ChinhSachHanMucAppService`.
2. **`Sales`/`Reconciliation` vẫn đọc Domain module khác** (nợ có ý thức — `audit-gd1.md` §0.1 **(b)**); `S17`
   **không** sửa nợ này (nó là hợp đồng **giữa các module**, không phải "dịch vụ còn ở host").
3. **Namespace legacy** `eCommerce.Application.Contracts.Inventory` của `IWarehouseAppService`/
   `IChinhSachHanMucAppService` **chưa đổi** — biên đúng là **assembly**, đổi namespace là refactor lan sang
   Web/DTO mà không thêm giá trị; ghi nợ để PR riêng.

---

## 4. Quy trình 8 bước cho MỖI module (lặp lại y hệt)

### B1 — Đo baseline trước khi sửa
```powershell
pwsh -File tools\kiem-tat-ca.ps1 -DayDu        # ghi số: 1422 · 71 · 133 · 25 · 53 · 62 · 41 · 86
```
Không khớp bảng `SESSION-KICKOFF.md` §3 ⇒ **dừng**, tìm hiểu.

### B2 — Tạo 4 project module (rỗng)
```powershell
dotnet new classlib -n eCommerce.<X>.Domain              -o modules/<X>/eCommerce.<X>.Domain
dotnet new classlib -n eCommerce.<X>.Application.Contracts -o modules/<X>/eCommerce.<X>.Application.Contracts
dotnet new classlib -n eCommerce.<X>.Application         -o modules/<X>/eCommerce.<X>.Application
dotnet new classlib -n eCommerce.<X>.EntityFrameworkCore -o modules/<X>/eCommerce.<X>.EntityFrameworkCore
dotnet sln eCommerce.slnx add modules/<X>/**/*.csproj    # kiểm tra lại bằng mắt
```
Khuôn tham chiếu: `modules/Address/` (đã đúng chuẩn, gồm cả `Contracts`).

### B3 — Di chuyển **project** chứ không đổi code
- `git mv` tệp entity → `modules/<X>/eCommerce.<X>.Domain/...` (**giữ nguyên thư mục con + namespace**).
- `git mv` tệp app service + DTO → `modules/<X>/eCommerce.<X>.Application/...` / `.Contracts/...`.
- Tạo `<X>DomainModule`, `<X>ApplicationModule`, `<X>EntityFrameworkCoreModule` **theo khuôn `AddressModule`**.
- Chuyển `DbContextModelCreatingExtensions` của module sang project EFCore của module.

### B4 — Nối vào host (3 chỗ, không hơn)
| Chỗ | Việc |
|---|---|
| `src/eCommerce.Domain/eCommerceDomainModule.cs` | thêm `[DependsOn(typeof(<X>DomainModule), typeof(<X>EntityFrameworkCoreModule))]` |
| `src/eCommerce.Application/eCommerceApplicationModule.cs` | thêm `[DependsOn(typeof(<X>ApplicationModule))]` |
| `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs` | **giữ** `DbSet<>` + `Configure<X>()` (quy ước #5) |

### B5 — Mã lỗi & quyền: **một nguồn**
- Chuyển `*ErrorCodes` của module về `modules/<X>/.../Domain` (nếu đang ở `eCommerceConsts`).
- Trong `eCommerceConsts` để **bí danh** (`public const string X = eCommerce.<X>.ErrorCodes.Y;`) **một đợt**,
  rồi xoá bí danh ở PR sau — tránh sửa 200 chỗ cùng lúc.
- `PermissionDefinitionProvider` của module: chuyển khai báo quyền từ host provider sang module (khuôn `PricingPermissions`).
  **Không đổi tên quyền** ⇒ `62 quyền` giữ nguyên.

### B6 — Test: di chuyển theo vùng, không viết lại
- `test/eCommerce.Tests/Domain/<X>*.cs` → giữ tên tệp, đổi `using` nếu cần.
- Thêm **test kiến trúc** `KienTrucModuleTests` (xem §6) — đỏ nếu vi phạm L1/L5.

### B7 — Chứng minh "không đổi hành vi"
```powershell
dotnet build eCommerce.slnx -warnaserror   # 0 warning mới
pwsh -File tools\route-check.ps1            # route KHÔNG đổi
pwsh -File tools\kiem-tat-ca.ps1 -DayDu     # 12 bước · exit 0 · CÙNG số ca
dotnet ef migrations add KiemTraRong -p src/eCommerce.EntityFrameworkCore -s src/eCommerce.DbMigrator
git diff --stat -- '**/Migrations/**'       # phải RỖNG; nếu có tệp ⇒ MÔ HÌNH ĐÃ ĐỔI ⇒ dừng
```
> 🚨 Bẫy #13 của dự án: `migrations add --no-build` dùng build cũ ⇒ sinh migration **rỗng** mà vẫn in `Done.`.
> **Luôn mở `Up()` đọc**, và ở đây kỳ vọng là **rỗng thật**.

### B8 — Cập nhật tài liệu (cùng PR)
- `docs/audit-gd1.md` §2: đổi trạng thái module tương ứng.
- Tài liệu feature của module: ghi `§16` "đã tách module ngày…".
- `SESSION-KICKOFF.md` §7: thêm dòng nếu có quyết định mới.

---

## 5. Chi tiết từng module

### M1 — `Reconciliation`
**Di chuyển:** `ReconRun`, `ReconRunMismatch`, `DoiSoatRetention`, `DoiSoatTonKho`, `NguongLech`,
`InventoryReconciliationService`, `DoiSoatAppService`, `DoiSoatRetentionService`, `ReconRetentionWorker`,
`InventoryReconciliationWorker`, `DoiSoatDtos`.
**Hợp đồng mới:** `IReconciliationReader` (đọc tồn kho) để **không** tham chiếu entity Inventory.
**Rủi ro:** job đối soát ghi `ReconRuns` — giữ nguyên bảng; worker chuyển sang module nhưng vẫn đăng ký ở host.
**Đo:** `ReconRuns` vẫn tăng 1 dòng/lượt; `-DayDu` bước ⑥ 25/25.

### M2 — `Inventory`
**Di chuyển:** toàn bộ `src/eCommerce.Domain/Entities/Inventory/*` trừ phần đã sang Reconciliation;
`StockDocumentAppService`, `WarehouseAppService`, `StockReservationAppService`, `ChinhSachHanMucAppService`,
`StockReservationExpiryWorker`, DTO/Contracts.
**Hợp đồng mới (bắt buộc cho M4):** `IStockAvailability` (đọc `Available`), `IStockReservation` (giữ/nhả/chốt),
`IStockPosting` (ghi sổ) — **DTO thuần**, không lộ entity.
**Rủi ro:** `StockLedger.cs` đang dùng `eCommerceConsts.ErrorCodes.InsufficientStock` ⇒ chuyển sang
`InventoryErrorCodes` + bí danh (B5). `InventoryLockKeysTests` giữ nguyên.
**Đo:** oversell test (khi có) + `InvStockLedger` append-only không đổi.

### M3 — `Catalog`
**Di chuyển:** 15 tệp `Catalog` (Domain) + `CatalogAttributeAppService`, `CategoryAppService`, `ProductAppService`,
`ProductMediaAppService`, `HienThiKenhAppService`, `CayDanhMucBuilder`, `ChannelVisibilityResolver`,
`ChannelDirectory`, DTO (`Catalog*Dtos`, `ProductMediaDtos`, …).
**Lưu ý:** `ChannelDirectory` đọc bảng `ChnChannels` (ChannelHub) ⇒ chuyển thành **hợp đồng** `IChannelDirectory`
(định nghĩa ở `Contracts`, hiện thực ở host/ChannelHub) để Catalog **không** tham chiếu ChannelHub.
**Đo:** `IX_CatProducts_TenantId_Slug` vẫn NND; slug/sitemap 133 E2E không đổi.

### M4 — `Sales`
**Di chuyển:** `Order`, `OrderLine`, `MarketplaceOrderIntake`, `MarketplaceEventLog`; toàn bộ
`Application/Sales`, `Application/Checkout`, `Application/Marketplace`; DTO tương ứng.
**Phụ thuộc (chỉ qua Contracts):** `IStockReservation` (Inventory) · `IPriceResolver`/`TinhTienDon` (Pricing) ·
`ICustomerDirectory` (Customers) · `IPaymentAppService` (PaymentHub — đã có Contracts) · `IChannelDirectory`.
**Rủi ro cao nhất:** `CheckoutAppService` đang gọi **trực tiếp** `GiaCoSoResolver`, `IGiuChoService`,
`IRepository<InventoryItem>`… ⇒ phải chuyển sang hợp đồng **trước** khi di chuyển (PR con M4a: "dựng hợp đồng,
host implement"; M4b: "di chuyển tệp").
**Đo:** E2E `DatHang` 133/133; probe thanh toán 26/26; oversell test.

> 📌 **Reconciliation đối soát THANH TOÁN** (`PaymentReconciliationRun/Line/Discrepancy`) **chưa có** —
> tạo ở module `Reconciliation` là việc **P0 #5** (kèm bảng mới + migration). Không gộp vào PR tách module
> (để giữ "0 migration").

---

## 6. Cổng chặn tái phát — test kiến trúc (làm ở PR đầu tiên)

`test/eCommerce.Tests/Architecture/KienTrucModuleTests.cs` — **đỏ** nếu:

| # | Bất biến | Cách kiểm |
|---|---|---|
| A1 | `src/eCommerce.Domain/Entities/{Catalog,Inventory,Orders}` **không tồn tại** | `Directory.Exists` == false |
| A2 | Không project nào dưới `modules/` tham chiếu `src/eCommerce.Domain` (trừ `Domain.Shared`) | đọc `*.csproj` **bằng văn bản** |
| A3 | Mỗi module có **đủ** `Domain` + `Application` | quét thư mục |
| A4 | Mọi entity `IMultiTenant` của 4 module có `DbSet` trên `eCommerceDbContext` | reflection trên DbContext |
| A5 | Không app service nào dùng `using eCommerce.Inventory` khi nằm ngoài module Inventory | quét `*.cs` theo đường dẫn |

> 🔑 Vì sao cần A2: đây đúng là lỗi đã xảy ra (`Customers.Application` → `src/eCommerce.Domain`).
> Không có test thì **lần sau ai đó lại làm** — đúng bài học `XC-004` TC-01/TC-02.

---

## 7. Acceptance criteria (Definition of Done)

- [ ] `modules/{Catalog,Inventory,Sales,Reconciliation}` tồn tại, mỗi module ≥ 3 project; `src/eCommerce.Domain/Entities`
      chỉ còn `Marketplace*`… **không** còn 4 thư mục trên.
- [ ] `KienTrucModuleTests` **có** và **đỏ khi tiêm** (thử thêm 1 `using` vi phạm ⇒ đỏ; gỡ ⇒ xanh).
- [ ] `dotnet build` 0 error / 0 warning mới.
- [ ] `-DayDu` `exit 0`: **1422 · 71 · 133 · 25 · 53 · 62 · 41 · 86** — **không đổi một số nào**.
- [ ] `dotnet ef migrations add` cho ra migration **rỗng** (đọc `Up()`).
- [ ] `route-check.ps1`: danh sách route **không đổi**.
- [ ] `docs/audit-gd1.md` §2 + `SESSION-KICKOFF.md` được cập nhật.

---

## 8. Rủi ro & cách giảm

| # | Rủi ro | Mức | Giảm |
|---|---|---|---|
| 1 | Big-bang làm đỏ 1422 test | 🔴 | Mỗi module **một PR**, `-DayDu` xanh mới sang module kế |
| 2 | Tách module làm **đổi lược đồ** (EF model) | 🔴 | Giữ namespace + tên bảng; bước B7 kiểm bằng migration rỗng |
| 3 | Vòng phụ thuộc (Sales ↔ Inventory) | 🔴 | `Application.Contracts` + interface, **không** tham chiếu entity |
| 4 | Route API biến mất (conventional controller theo assembly) | 🟠 | `route-check.ps1` + E2E 133; `ConventionalControllers` đăng ký **động** (C16) nên chỉ cần `DependsOn` |
| 5 | Quyền bị nhân đôi / mất | 🟠 | Chuyển provider, **không đổi tên**; đếm `AbpPermissionGrants = 62` |
| 6 | Mã lỗi đổi ⇒ test cũ đỏ | 🟠 | Bí danh ở `eCommerceConsts` trong 1 đợt |
| 7 | Thời gian dài, mất ngữ cảnh | 🟠 | Mỗi bước có DoD riêng, ghi `§16` tài liệu |

---

## 9. Rollback

Mỗi PR độc lập ⇒ `git revert <commit>` là đủ. **Không có migration** ⇒ **không có** dữ liệu phải khôi phục.
Điều kiện: **không** gộp việc thêm bảng/cột vào PR tách module.

---

## 10. Việc chưa làm trong kế hoạch này (ghi rõ để không hiểu nhầm "đã xong")

- ❌ Chưa tách `Promotions`, `ShippingHub`, `Reporting`, `Notifications`, `AccountingHub` (GĐ2–GĐ3).
- ❌ Chưa thêm entity thiếu (`Brand`, `CostPrice`, `StockAllotment`, `OrderReturn`, `PaymentReconciliation*`).
- ❌ Chưa sửa state machine đơn hàng (việc **P0 #2**, PR riêng).
- ❌ Chưa có outbox/relay (**P0 #3**) và test oversell 2 instance (**P0 #4**).
- ❌ Chưa có UI admin Catalog/Pricing/Kho.

---

## 11. Lịch sử tệp này

| Ngày | Thay đổi |
|---|---|
| 24/10/2026 | Bản đầu — kế hoạch tách 4 module GĐ1 theo thứ tự lá→gốc, giữ namespace/tên bảng, 0 migration, có test kiến trúc chặn tái phát. |
| 29/10/2026 | **`S17`** — đóng phần **hợp đồng** của L2: xác nhận 4 hợp đồng đã ở module Contracts từ `e2c2785`; chuyển `WarehouseAppService` vào `Inventory.Application`; 3 hiện thực còn host + 1 worker được ghim bằng `[ExposeServices]`/lý do và khoá bằng `HopDongHostTests` (**18 ca**). Không migration, không đổi route/quyền. |
