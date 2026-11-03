# Handover `N4` — WP-04 (BE): tồn kho 9 chiều + phiếu kho kiểu Nhanh + ghi sổ

> Ngày 02/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ `713acdd`; cây làm việc vẫn gồm `N1`+`N2`+`N3` **chưa commit**)
> Kế hoạch: [07 §WP-04](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) · Quyết định: [decisions.md](decisions.md)
> **Phạm vi:** đúng **WP-04 phần BE** (việc 1–6). **Không** làm FE màn DS/tạo phiếu (thuộc `N5`).
>
> 📌 **CẬP NHẬT (cùng ngày, phiên `N4b`):** hai mục **hoãn** ghi ở §2 bên dưới đã được xử lý —
> **D-25** (decimal hoá số lượng) **đã thực hiện** (D-31) và **D-26** (không thêm `RowVersion`) **giữ nguyên có lý do**
> (D-32). ⇒ **WP-04 (BE) đã đóng.** Đọc [handover-n4b.md](handover-n4b.md) cho phần số lẻ + số đo cuối.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch**: `N1`+`N2`+`N3` chưa commit (19 tệp `M` + `??`) — không có tệp nào của `N4` |
| 2 | `git switch` | đã ở `feature/wp-03-catalog` · `HEAD = 713acdd` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (build tăng dần) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1684/1684 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (đo lại ở §1.5) |
| — | Migration | **52** · NND **73** |

### 1.2 Đã thi công (đúng 6 việc của WP-04)

| Việc (07 §WP-04) | Nội dung | Đo được |
|---|---|---|
| **1. `InventoryItem` 9 chiều** | +8 cột: `Transferring`, `Shipping`, `Holding`, `Damaged`, `Warranty`, `WarrantyHolding`, `PreOrder`, `WaitingInbound`; computed `InWarehouse = OnHand − Transferring`, `Available = OnHand − Reserved − Transferring − Holding − Damaged`; `ApplyMovement` phủ **11 loại mới** và kiểm **mọi chiều ≥ 0** + `Available ≥ 0` | unit `Wp04TonKhoChieuTests` (14 ca) |
| **2. `StockDocument`/`Line`** | `Direction` (nullable cho kiểm kê), `Mode`, `CounterpartyType/Id`, `RefType/RefId`, `DiscountAmount`, `PaidAmount`; dòng: `UnitId`, `VatPercent`, `BatchId`, `ImeiSerials` + luật (VAT 0..100, **số serial = số lượng**, serial trùng, **lô + serial cùng lúc** ⇒ NÉM) | unit `Wp04PhieuKhoNhanhTests` (26 ca) |
| **3. `StockMovementType` append** | `ShippingOut=11` … `OtherOut=21` (**11 giá trị**, gồm 2 giá trị ngoài danh mục 07 — D-28); `DoiSoatTonKho.Dau` **đã phủ đủ 11 nhánh** (thiếu ⇒ **job đối soát ném/chết**) | `StockMovementTypeTests` (22 InlineData) + theory bảng dấu |
| **4. `StockPostingService`** | Bút toán theo **`(mode, chiều)`** qua hàm thuần `TinhButToanChungTu.ChonBienDongTheoMode`; bút toán mang `RefType/RefId` của chứng từ nguồn; chống Post 2 lần (mã `0010`) + khoá phân tán theo SKU (đã có từ `INV-001`) | §1.4 ca ⑥⑦ |
| **5. Migration 53 + backfill** | `20261102095854_Wp04_Inventory_Buckets_Mode` (additive, có `Down`); unique `(TenantId, IdempotencyKey)` + **SQL thô `NULLS NOT DISTINCT`**; 5 `CHECK`; `tools/data-migration/backfill-inventory.ps1` (dry-run mặc định, `-Apply`, **từ chối dựng dòng không hợp lệ**, **đo lại** sau khi ghi) | §1.3, §1.4 ca ⑫ |
| **6. API `/api/v1/stock`** | `GET /balances` (9 chiều + 2 số suy ra, phân trang), `GET /bills` (lọc chiều/trạng thái/mode/kho/ngày), `GET /bills/{id}`, `GET /bills/{id}/lines`, `POST /bills`, `POST /bills/{id}/post`, `POST /bills/{id}/cancel` — controller tường minh ở `eCommerce.HttpApi` (D-14) | probe 12 ca, §1.4 |

**Ngoài dự kiến nhưng BẮT BUỘC (đều do đo, không do suy đoán):**

| # | Phát hiện | Vì sao phải sửa trong WP-04 |
|---|---|---|
| F1 | `StockDocumentType` **không có loại phiếu XUẤT** (chỉ Nhập/Chuyển kho/Kiểm kê/Trả hàng) | Không có nó thì phiếu bán lẻ/giao hàng/trả NCC **không tồn tại** ⇒ phải mượn `StockTransfer` (báo cáo chuyển kho đếm cả hàng bán). Thêm `GoodsIssue = 4` (D-30) |
| F2 | `ApplyMovement` **thiếu nhánh** cho `SupplierReturnOut`/`OtherOut` (do chính test của tôi bắt) | Ghi sổ phiếu xuất NCC **ném** ⇒ không dùng được |
| F3 | `ThenBy(x => x.Sku, StringComparer.Ordinal)` trên `IQueryable` ⇒ EF **không dịch được** ⇒ **HTTP 500** ở **cả hai** endpoint danh sách (probe bắt) | Sửa: bỏ comparer ở tầng SQL (giữ sắp xếp tất định) |
| F4 | `[ExposeServices(typeof(IWarehouseAppService))]` chỉ đăng ký **interface** ⇒ controller quy ước resolve **lớp** ⇒ **POST `/api/app/warehouse` = 500** (lỗi **có sẵn** từ `S17`, probe bắt) | Không tạo được kho ⇒ **toàn bộ** luồng phiếu kho/POS bị chặn từ bước đầu. Sửa: khai **cả hai** service type (giữ test `HopDongHostTests`) |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** — 9 `CS8604` **có sẵn** (Address 6 + Sales 3), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1768/1768 pass** (+**84** so với baseline) |
| `dotnet test test\eCommerce.IntegrationTests --no-build` | **90/90 pass** · 818 ms (PostgreSQL thật qua Testcontainers, gồm `F1_du_74_index…`) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (§1.6) |
| Migration | **53** (`__EFMigrationsHistory`) |
| NND (DB dev) | **74** — `select count(*) from pg_indexes where indexdef like '%NULLS NOT DISTINCT%'` |
| `schema.sql` | sinh lại từ chính migration: **+336 dòng, 0 xoá** |
| OpenAPI | `pnpm spec:snapshot` + `pnpm gen:api`: **12/12 path `/api/v1`** (trước: 6) — 6 endpoint `stock` mới |
| FE không hồi quy | `pnpm --filter @ecommerce/admin-web build` **✓** (First Load JS **102–106 kB**, middleware 34,1 kB) — schema TS mới **không** phá màn đang có |

### 1.4 Bằng chứng HTTP thật (`tools/probe-kho-wp04.ps1`, chạy **2 lần**, `exit 0`)

| # | Ca | Kết quả đo |
|---|---|---|
| ① | Tạo kho `WP04-…` | **200** |
| ② | Tạo phiếu `Supplier`/`GoodsReceipt` 10 đơn vị (`idempotencyKey`) | **201** — `direction:"In"`, `mode:"Supplier"`, `lineCount:1` |
| ③ | Đọc lại phiếu | **200** — cùng `direction/mode` |
| ④ | `POST …/post` | **200** — `postedEntries:1`, `status:"Posted"` |
| ⑤ | `GET /balances?sku=` | **200** — `onHand:10, available:10, inWarehouse:10`, 8 chiều còn lại `0` |
| ⑥ | `POST …/post` **lần hai** | **403** `eCommerce:Inventory:0010`; `OnHand` **vẫn 10** (không cộng hai lần) |
| ⑦ | `POST …/cancel` phiếu đã ghi sổ | **403** `eCommerce:Inventory:0013` |
| ⑧ | Tạo lại với **cùng `idempotencyKey`** | **403** `eCommerce:Inventory:0035` + `data.constraint = IX_InvStockDocuments_TenantId_IdempotencyKey` |
| ⑨ | Tạo phiếu `Retail`/`GoodsIssue` 15 | **201** — `direction:"Out"` |
| ⑩ | Ghi sổ phiếu bán 15 khi tồn 10 | **403** `eCommerce:Inventory:0001` (`movementType: RetailSaleOut`, `onHand:-5`) |
| ⑪ | `GET /bills?direction=In&mode=Supplier` | **200** — `total:1` |
| ⑫ | Bất biến `tổng sổ cái = ảnh chụp` (SQL) | **10 = 10** |

> 📌 Probe để lại 1 kho + 1 phiếu + 1 dòng tồn + 2 bút toán trong DB dev (mã `WP04-<random>`), cố ý **không dọn** để phiên sau kiểm lại được bằng SQL.

### 1.5 Backfill — **mutation test** (chứng minh script thật sự bắt lệch)

| Bước | Kết quả đo |
|---|---|
| 1. Dry-run trên DB dev | `2 cặp` · LỆCH **0** · THIẾU-DÒNG **0** · KHÔNG-HỢP-LỆ **1** (nợ dữ liệu E2E — §5.1) ⇒ `exit 1` |
| 2. **Sửa tay** `OnHand += 5` cho SKU probe | `UPDATE 1` |
| 3. Dry-run lại | **LỆCH 1** — in đúng `WP04-SKU-… | anh_chup OnHand=15 | so_cai OnHand=10 | LECH` ⇒ `exit 1` ✅ |
| 4. `-Apply` | `da sua: 1 dong` · đo lại **0 lệch** ⇒ `exit 0` ✅ |
| 5. Dry-run lần 3 | LỆCH **0** ⇒ `exit 0` (idempotent) ✅ |

### 1.6 Cổng tài liệu

| Lệnh | Kết quả |
|---|---|
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 105 `.md`, **463** link markdown, **347** đường dẫn backtick, **0** chết |
| `npm --prefix tools run check:mermaid` | **PASS** — **213** khối Mermaid, **0** lỗi |

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

1. **FE (màn #7 DS phiếu, #8 tạo phiếu)** — `N5`, **cố ý chưa làm**. API đã sẵn: 6 endpoint `/api/v1/stock/*` + `schema.d.ts` đã sinh.
2. **`int → decimal(18,3)`** (WP-04 việc 1) — **HOÃN có quyết định**: D-25 (bán kính 16 tệp, gồm bảng lịch sử đối soát + 4 tệp test; tách **WP-04b**).
3. **`RowVersion`** — **không thêm**: D-26 (đã có `ConcurrencyStamp` + `AbpDbConcurrencyException`).
4. **4 chiều chưa có đường ghi**: `Transferring`, `WarrantyHolding`, `PreOrder`, `WaitingInbound` — cột + `CHECK` + API đọc **đã có**, nhưng nghiệp vụ sinh ra chúng thuộc **WP-05 (đơn)** và **WP-06 (POS)**. 📌 Ghi rõ để không ai tưởng "đã có nghiệp vụ" (báo cáo sẽ ra 0 vĩnh viễn cho tới khi nối).
5. **`StockDocumentLine.Quantity` vẫn `int`** ⇒ chưa nhập được số lẻ (cân đo/ĐVT quy đổi). Thuộc **WP-04b** (cùng D-25).
6. **IMEI/serial chỉ lưu chuỗi** ở dòng phiếu (`ImeiSerials`) — **chưa** tạo `CatProductImeis` thật (nửa sau WP-03 chưa có API ghi serial).
7. **Không có endpoint xoá/sửa phiếu `Draft`** (chỉ tạo/ghi sổ/huỷ) — màn #8 cần sửa nháp thì bổ sung ở `N5` (kèm `YeuCauChoPhepSuaNoiDung` đã có sẵn trong domain).

**Không có tệp nào đang sửa dở** — mọi thay đổi đã build/test/probe xanh.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | `20261102095854_Wp04_Inventory_Buckets_Mode` (migration thứ **53**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (container `ecommerce-postgres`) — `cd src\eCommerce.DbMigrator; dotnet run` |
| Nội dung | +8 cột `InvItems`; +8 cột `InvStockDocuments`; +4 cột `InvStockDocumentLines`; +4 index (1 unique **NND** + 1 index lọc danh sách); +5 `CHECK` (`CK_InvItems_Chieu_KhongAm`, `CK_InvStockDocuments_{Mode,Direction,Tien}_…`, `CK_InvStockDocumentLines_VatPercent_HopLe`) |
| `Down` | Có: xoá 5 `CHECK` + 4 index + 20 cột |
| Cách lùi | `dotnet ef database update 20261102093528_Wp03_Catalog_Backfill_OrderLineProductId -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` (hoặc `migrations remove` nếu chưa chia sẻ) |
| `schema.sql` | sinh lại: **+336 dòng, 0 xoá**; `PostgreSqlSchemaTests` **74 NND** xanh |
| ⚠️ Bẫy đã gặp | `dotnet ef` để lại **tiến trình Host sống** ⇒ build sau **khoá DLL** (`MSB3027`). Phải `Get-Process eCommerce.HttpApi.Host | Stop-Process` **trước** mỗi lần build/`ef`. Gặp 3 lần trong phiên này |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-25** | **Hoãn `int → decimal(18,3)`** cho số lượng — bán kính 16 tệp (gồm `ReconRunMismatch`/`DoiSoatTonKho`/`StockBalanceReader` + 4 tệp test) ⇒ tách **WP-04b** |
| **D-26** | **Không thêm `RowVersion`** cho `InventoryItem` — dùng `ConcurrencyStamp` sẵn có |
| **D-27** | Nghĩa 9 chiều + công thức `Available` (có `Reserved`); chiều **rời kho** thì `OnHand` giảm, chiều **còn trong kho nhưng không bán được** thì chỉ trừ `Available` |
| **D-28** | Số `StockDocumentMode` **đúng Nhanh** (15 giá trị dùng; "19" trong 07 là **giá trị lớn nhất**) + append `SupplierReturnOut=20`, `OtherOut=21` |
| **D-29** | Chống tạo phiếu trùng bằng `IdempotencyKey` + unique **NND** có bộ lọc; **không** đặt unique `(RefType, RefId, MovementType)` (đơn có nhiều chuyến giao ⇒ chặn oan) |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Nợ dữ liệu trong DB dev (đã đo, KHÔNG tự sửa):** cặp `K97E2E-SKU-P1` có **sổ cái `Reserved = 199` nhưng `OnHand = 0`** ⇒ vi phạm bất biến `Reserved ≤ OnHand` (rác E2E để lại). `backfill-inventory.ps1` **từ chối** dựng lại (đúng thiết kế: không ghi dòng không hợp lệ). Muốn sạch: người quyết định xoá dòng rác sổ cái của SKU đó (`InvStockLedger` + `InvStockReservations`), rồi chạy `-Apply`. 🔒 **Đừng** dùng `-ChapNhanKhongHopLe` để che lệch MỚI.
2. 🚨 **4 chiều chưa có đường ghi** (§2.4). Nếu FE hiển thị "Đang chuyển kho" mà WP-05 không nối nghiệp vụ ⇒ cột **luôn 0** và người dùng tin sai.
3. ⚠️ **`StockDocument.Type` giờ có 5 giá trị, `Mode` 15** — màn Razor cũ (`/Kho`) chỉ đọc `TonKhoAppService.GetAsync` (3 số) nên **không đổi**, nhưng **không** hiển thị chiều mới. Nếu ai sửa view cũ, đọc D-27 trước.
4. ⚠️ **Business exception trả HTTP 403** (kèm `message` chung) — hành vi có sẵn của Host, **không** phải lỗi WP-04; client phải đọc `error.code` (đã ghi ở `handover-n2` §5.3).
5. ⚠️ **`WarehouseAppService` sửa `ExposeServices`** (F4) — nếu ai đó "dọn" thuộc tính này về một tham số như cũ, **POST `/api/app/warehouse` sẽ 500 lại** (test `HopDongHostTests` vẫn xanh nếu chỉ khai interface, nên **test không bảo vệ** được ca này — chỉ probe bảo vệ).
6. 📌 **Probe để lại dữ liệu** (`WP04-<random>`) trong DB dev — vô hại, nhưng nếu ai đếm tồn kho dev thì nhớ chúng là rác kiểm chứng.
7. ⚠️ **Cây làm việc vẫn dơ** (N1+N2+N3+N4 **chưa commit**). `git checkout`/`reset` mạnh tay sẽ **mất cả 4 phiên**.

---

## 6. Việc đầu tiên của phiên sau

1. **`N5` — WP-04 (FE)**: màn DS phiếu (`/{business}/inventory/bill`) + màn tạo phiếu (chọn mode → form động) + cột tồn 3 số, dùng `schema.d.ts` đã sinh (`GET /api/v1/stock/bills`, `POST /api/v1/stock/bills`, `POST …/post`).
   Điều kiện xanh: Post → tồn đổi **trên UI**; Post lại **bị chặn** (đã có mã `0010` để hiển thị).
2. **Trước khi mở `N5`:** commit `N1…N4` thành các commit riêng (ít nhất `git stash create`) để có điểm lùi — cây đang trộn 4 phiên.
3. (Khuyến nghị) **WP-04b** — `decimal` hoá số lượng (D-25): làm cùng lúc với nhu cầu số lẻ thật của kho (cân đo).

Lệnh mở phiên (đúng §4):
```powershell
cd D:\Projects\Tools\eCommerce
git status --short
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
pwsh -File .\tools\probe-kho-wp04.ps1     # 12 ca HTTP thật của WP-04 (tự bật/tắt Host)
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh hiện tại | `feature/wp-03-catalog` (tên nhánh **không còn khớp** phạm vi: đang chứa `N1…N4`) |
| Commit | **CHƯA commit** gì cho `N4`; `HEAD` vẫn `713acdd` |
| Push / PR | Chưa |
| Tệp `N4` đã chạm | `src/eCommerce.Domain.Shared/{Enums.cs,Enums.Inventory.cs,InventoryErrorCodes.cs}` · `modules/Inventory/eCommerce.Inventory.Domain/{StockLedger,StockDocument,StockPostingService,TinhButToanChungTu}.cs` · `modules/Inventory/eCommerce.Inventory.EntityFrameworkCore/InventoryDbContextModelCreatingExtensions.cs` · `modules/Inventory/eCommerce.Inventory.Application/{StockDocumentAppService,TonKhoAppService,WarehouseAppService}.cs` · `modules/Inventory/eCommerce.Inventory.Application.Contracts/{StockDocumentDtos,TonKhoDtos}.cs` · `modules/Reconciliation/eCommerce.Reconciliation.Domain/DoiSoatTonKho.cs` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `src/eCommerce.HttpApi/{eCommerce.HttpApi.csproj,Controllers/StockController.cs}` · migration 53 + snapshot · `test/…/Assets/schema.sql` · `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` · `test/eCommerce.Tests/Inventory/{InventoryBatBienTests,Wp04TonKhoChieuTests}.cs` · `tools/data-migration/backfill-inventory.ps1` · `tools/probe-kho-wp04.ps1` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}` · docs (`decisions`, `07`, `08`, `README`, handover này) |
