# Handover `N3` — WP-03 nửa sau (backfill biến thể → sản phẩm con, luật cha–con, adapter đọc cũ)

> Ngày 02/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ `713acdd`, cây làm việc dở của `N1`+`N2`)
> Kế hoạch: [07 §WP-03](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) · Quyết định: [decisions.md](decisions.md)
> **Phạm vi đã làm đúng "việc đầu tiên của phiên sau" ở [handover-n2 §6](handover-n2.md):** (1) backfill
> `ProductVariant → Product con` có `--dry-run` + bảng đối chiếu, (2) nối `Publish()` với mô hình mới + luật
> *"cha không bán được"* + chống chu trình, (3) `OrderLine.ProductId` + `IProductLegacyReader`.
> **Không** làm FE form sản phẩm, **không** làm API ghi IMEI/lô/combo/gói/nhãn (xem §2).

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4)

| # | Lệnh | Kết quả đo | Ghi chú |
|---|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch**: 19 tệp `M` + nhiều `??` | Toàn bộ là `N1`+`N2` **chưa commit** (đúng như [handover-n2 §7](handover-n2.md)). Không có tệp nào của N3 ở thời điểm này |
| 2 | `git switch <nhánh-WP>` | Đã ở `feature/wp-03-catalog` · `HEAD = 713acdd` | Không tạo nhánh mới (nhánh WP-03 đã đúng) |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Up 2 days (healthy)** | Docker 29.6.x |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** · 17,9 s | ⚠️ 0 warning do build **tăng dần** — build sạch ở §1.3 cho **9 warning có sẵn** (khớp baseline) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1671/1671 pass** · 0 failed · 1 s | Khớp [handover-n2 §1.3](handover-n2.md) |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 103 `.md`, **434** link, **347** đường dẫn backtick, **0** chết | |
| — | Migration (đếm thêm) | **51** | số tiếp theo: `52` |
| — | Dữ liệu dev DB (`psql`) | `CatProducts=1` · `CatProductVariants=0` · `CatProductUnits=3` · `OrdOrderLines=0` | Đúng trạng thái `N2` để lại ⇒ điều kiện *"dry-run phải in `0 variant → 0 child`"* kiểm được thật |

### 1.2 Đã thi công

| Hạng mục | Nội dung | Đo được |
|---|---|---|
| **Bảng đối chiếu backfill** | Entity `ProductVariantMap` (bảng `CatVariantMaps`: `VariantId → ProductId`, `UnitId`, `Sku`) + `DbSet` + mapping; unique `(TenantId, VariantId)` **`NULLS NOT DISTINCT`** viết tay | có trong DB dev: `\d "CatVariantMaps"`; NND **72 → 73** |
| **`BackfillCatalogService`** (trong DbMigrator) | `--backfill-catalog [--dry-run]`; biến thể → **1 sản phẩm con** (`Code` = SKU, `ParentId` = cha, VAT/brand/mô tả chép từ cha, `WeightGram` từ biến thể) + **1 ĐVT gốc** (`Cái`, hệ số 1, barcode + giá + `OldPrice` = `CompareAtPrice`); ghi bản đồ; gán `OrdOrderLines.ProductId` theo `Sku` | `src/eCommerce.DbMigrator/BackfillCatalogService.cs`; số đo §1.4 |
| **Script chạy** | `tools/data-migration/backfill-catalog.ps1` — tự đọc lại số đo (**không tin mắt**), thoát `0` khớp / `1` xung đột / `2` khung chạy hỏng | `-DryRun`, `-ConnectionString` (qua biến môi trường `ConnectionStrings__Default`) |
| **`OrdOrderLines.ProductId`** | Cột nullable + index `IX_OrdOrderLines_TenantId_ProductId`; **giữ** `Sku` + `ProductVariantId` | migration 52; DB copy: 2/2 dòng đơn được gán |
| **`Publish()` nới đúng mô hình mới** | "Có gì để bán" = **ĐVT gốc** *hoặc* **biến thể cũ đang hoạt động** (giữ mã `0003`); thêm **`AutoInclude` cho `Units`** — thiếu nó thì guard đọc `Units` rỗng và sản phẩm 3 ĐVT **vẫn không publish được** (xem §5.1) | `ProductNhanhModelNuaCuoiTests` (4 ca publish) |
| **Luật cha–con** | `CaySanPham.KiemTraKhongChuTrinh` (mã **`0048`**, giữ **`0043`** cho ca một mức, chặn cả dữ liệu cũ có vòng + giới hạn độ sâu 64); `PublishAsync` chặn **sản phẩm cha** (mã **`0047`**); `UpdateProductDto.ParentId` + `UpdateAsync` kiểm chu trình | 8 ca test lõi; 2 mã mới trong sổ đăng ký (pass `CatalogErrorCodeRegistryTests`) |
| **Adapter đọc cũ** | `ProductLegacyAdapter` (hàm thuần) + `IProductLegacyReader` / `ProductLegacyReader` (không `[Authorize]`); `DocSanPhamCongKhai` **không** đọc `ProductVariant` trực tiếp nữa | `/SanPham` **200** cho cả hai mô hình — §1.5 |
| **Migration 52** | `20261102093528_Wp03_Catalog_Backfill_OrderLineProductId`: +1 bảng, +1 cột, +5 index (1 unique NND), +1 FK; `Down` xoá bảng + cột | migration **51 → 52** |
| **`schema.sql`** | sinh lại **từ chính migration** (chỉ **thêm** 272 dòng so với `HEAD`) | `test/eCommerce.IntegrationTests/Assets/schema.sql` |
| **Cổng ràng buộc DB** | thêm 1 dòng danh sách trắng **có lý do** cho index mới | `RangBuocDbChuaAnhXaTests` pass (nằm trong 1684) |
| **OpenAPI** | `UpdateProductDto` thêm `parentId` ⇒ **sinh lại** `spec:snapshot` + `gen:api` (đường `/api/v1` **không đổi**) | `packages/api-client/openapi.v1.json`, `.../src/schema.d.ts` |
| **Test mới** | `ProductNhanhModelNuaCuoiTests` — **13 ca** (publish theo ĐVT gốc, vòng nhiều mức, dữ liệu cũ có vòng, độ sâu, adapter 4 ca) | **1671 → 1684** |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · 43,1 s — 9 warning `CS8604` **có sẵn** ở `Address` (6) + `Sales` (3), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1684/1684 pass** · 0 failed · 2 s |
| `dotnet test test\eCommerce.IntegrationTests --filter "FullyQualifiedName~F1_du_73_index"` | **1/1 pass** · 284 ms (PostgreSQL thật qua Testcontainers) |
| `dotnet test test\eCommerce.IntegrationTests --no-build` | **90/90 pass** · 0 failed · 848 ms (cả bộ, gồm `PostgreSqlSchemaTests`) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (§1.6) |
| Migration | **52** (`__EFMigrationsHistory`) |
| NND (DB dev) | **73** — `select count(*) from pg_indexes where indexdef like '%NULLS NOT DISTINCT%'` |

### 1.4 `backfill` — bằng chứng đo được (điều kiện xanh của `N3`)

**① DB dev (1 sản phẩm mới + 0 biến thể) — dry-run:**

```
pwsh -File .\tools\data-migration\backfill-catalog.ps1 -DryRun
[BACKFILL-CATALOG] variants=0 alreadyMapped=0 toCreate=0 conflicts=0
[BACKFILL-CATALOG] RESULT: 0 variant -> 0 child (DRY-RUN: KHONG ghi gi)
KET QUA: OK — doi soat khop (0 variant → 0 child).
```

**② Bản sao DB có biến thể thật** (`ecommerce_n3_copy`, dựng bằng chính migrator rồi chèn dữ liệu cũ:
cha `SP-CHA` **Published** + 2 biến thể `AT-DO-M`/`AT-XANH-L` có barcode + 1 đơn 2 dòng theo SKU):

| Lần chạy | `variants` / `alreadyMapped` / `toCreate` / `conflicts` | `RESULT` | Dòng đơn | DB sau đó (products/units/maps/linked) |
|---|---|---|---|---|
| dry-run | 2 / 0 / 2 / 0 | **2 variant → 2 child** (không ghi) | 2 → 2 | **1 / 0 / 0 / 0** ⇒ dry-run **không** ghi gì |
| thật (lần 1) | 2 / 0 / 2 / 0 | **2 variant → 2 child** | 2 → 2 | **3 / 2 / 2 / 2** |
| thật (lần 2) | 2 / 2 / 0 / 0 | **2 variant → 0 child** (idempotent) | 0 → 0 | **3 / 2 / 2 / 2** ⇒ **không tạo trùng** |
| xung đột (thêm biến thể `SP-CHA` trùng mã sản phẩm) | 3 / 2 / 0 / **1** | `ABORT — KHONG ghi gi` · **exit 1** | — | **3 / 2 / 2** ⇒ dừng **trước** khi ghi |

Dữ liệu sau lần chạy thật (SQL trên bản sao):

```
    con    |  cha   | st | vat      sp     | dvt | heso | goc |    Barcode    |  SalePrice
-----------+--------+----+-----   ----------+-----+------+-----+---------------+-------------
 AT-DO-M   | SP-CHA |  0 | 8.00     AT-DO-M   | Cái | 1.00 | t   | 8935000000201 | 199000.0000
 AT-XANH-L | SP-CHA |  0 | 8.00     AT-XANH-L | Cái | 1.00 | t   | 8935000000202 | 219000.0000
 SP-CHA    |        |  1 | 8.00
```

`OrdOrderLines`: 2/2 dòng có `ProductId` trỏ đúng sản phẩm con; `ProductVariantId` **giữ nguyên** (NULL trong ca này).

⇒ **Điều kiện xanh của `N3`:** chạy **2 lần cho cùng kết quả** ✅ (lần 2: `toCreate=0`, DB y nguyên).

### 1.5 `Razor /SanPham` vẫn 200 — và đường ĐỌC đi qua adapter

Web chạy với `ConnectionStrings__Default` trỏ **bản sao DB** (`http://127.0.0.1:5199`):

| Ca | Dữ liệu | Kết quả đo |
|---|---|---|
| A | Cha `SP-CHA` **Published**, có 2 biến thể + bảng giá `BG-N3` (`AT-DO-M`/`AT-XANH-L`) | `GET /SanPham` → **200**, HTML có `Ao thun nam` (đường **biến thể**) |
| B | Cha → `Unpublished`; **sản phẩm con** `AT-DO-M` → `Published` | `GET /SanPham` → **200**, HTML có `Ao thun nam — AT-DO-M` ⇒ sản phẩm **0 biến thể, 1 ĐVT gốc** hiện được nhờ **adapter** (đường **ĐVT**) |
| C | Con có `Slug` đặt tay | `GET /SanPham/ChiTiet/ao-thun-nam-at-do-m` → **200**, `Giá từ 199,000 VND`, bảng giá `BG-N3` ⇒ giá ghép theo `Code` (= SKU cũ) |
| D | Sản phẩm **`Slug = NULL`** (cha, Published) | `GET /SanPham/ChiTiet/ao-thun-nam` → **404** — ⚠️ **hành vi CÓ SẴN**, không phải hồi quy của N3 (xem §5.4) |

### 1.6 Cổng tài liệu cuối phiên

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **104** `.md`, **453** link markdown, **347** đường dẫn backtick, **0** tham chiếu chết (chạy **sau** khi thêm handover này + sửa `07`/`08`/`decisions`/`README`).
`npm --prefix tools run check:mermaid` **chưa chạy** (không thêm khối Mermaid mới).

---

## 2. Chưa xong / cắt giữa

Đúng **3 việc đầu tiên** của [handover-n2 §6](handover-n2.md) đã xong; các việc sau **cố ý chưa làm**:

1. **FE form sản phẩm** (WP-03 phần **FE 3 ngày**, màn #5 của [04](04-web-admin-nextjs.md)) — **chưa làm**. Không nằm trong 3 việc của `N3` (§6 của handover trước) và cần phiên FE riêng; API đọc/ghi đã đủ cho form (§1.2).
2. **API ghi IMEI/lô/combo/gói/nhãn** (WP-03 mục 3 phần "đường ghi") — **chưa có**; bảng đã dựng từ `N2`, tức **6 dòng danh sách trắng** trong `RangBuocChuaAnhXa.txt` vẫn còn.
3. **`UpdateProductDto` chỉ mang thêm `ParentId`** — `Type/VatPercent/WeightGram/…` vẫn **chưa** sửa được qua đường cập nhật (nợ cũ [handover-n2 §2.7](handover-n2.md)); `UpdateAsync` cũng chưa cho sửa ĐVT.
4. **Backfill chưa có "hoàn tác dữ liệu" tự động**: `Down` của migration 52 xoá **bảng đối chiếu** + cột, **không** xoá sản phẩm con đã tạo. Lùi dữ liệu phải đọc `CatVariantMaps` **trước** khi `Down` (xem §3).
5. **Dòng đơn không khớp SKU** (SKU không có sản phẩm/biến thể tương ứng) giữ `ProductId = NULL` — đúng thiết kế, nhưng **chưa** có báo cáo "còn bao nhiêu dòng chưa gán" ngoài số `ORDERLINE … linked` của một lần chạy.
6. **Sản phẩm cha legacy đang `Published` KHÔNG bị tự unpublish** khi backfill (cố ý: không đổi trạng thái bán của dữ liệu thật). Hệ quả: trong giai đoạn chuyển tiếp, **cha và con có thể cùng hiện** trên storefront nếu ai đó publish con mà không unpublish cha.

**Không có tệp nào đang sửa dở** — mọi thay đổi đã build/test xanh.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | `20261102093528_Wp03_Catalog_Backfill_OrderLineProductId` (migration thứ **52**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (container `ecommerce-postgres`) + **bản sao** `ecommerce_n3_copy` (dựng bằng chính migrator) |
| Nội dung | +1 bảng `CatVariantMaps` (FK `Restrict` → `CatProducts`); +1 cột `OrdOrderLines.ProductId` + index `(TenantId, ProductId)`; unique `IX_CatVariantMaps_TenantId_VariantId` **`NULLS NOT DISTINCT`** (SQL thô, cùng cách migration 51) |
| `Down` | Có — **đã kiểm chứng trên DB nháp** `ecommerce_n3_down`: `dotnet ef database update 20261102085805_Wp03_Catalog_NhanhModel` ⇒ `CatVariantMaps` **không còn** (`to_regclass` = NULL), `OrdOrderLines.ProductId` **không còn** (0 cột), lịch sử về migration **51**; chạy `update` lại ⇒ bảng + cột + index **trở lại**, lịch sử **52**. **Không** xoá sản phẩm con/ĐVT đã sinh |
| Cách lùi (schema) | `dotnet ef database update 20261102085805_Wp03_Catalog_NhanhModel -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| Cách lùi (dữ liệu) | **Trước** khi `Down`: `select "ProductId" from "CatVariantMaps"` → đó là danh sách sản phẩm con do backfill sinh; xoá mềm chúng (`CatProducts.IsDeleted = true`) là lùi được về trạng thái cũ |
| `schema.sql` | sinh lại **từ chính migration**; diff **chỉ thêm** 272 dòng |
| Bản sao DB | `ecommerce_n3_copy` **giữ lại** làm bằng chứng; xoá bằng `docker exec ecommerce-postgres psql -U ecommerce -d postgres -c 'DROP DATABASE ecommerce_n3_copy;'` |

⚠️ **Ba điều phải biết khi chạy lại migrator** (giữ nguyên từ [handover-n2 §3](handover-n2.md) + thêm 1):
1. Phải `cd src\eCommerce.DbMigrator` rồi `dotnet run` — `dotnet run --project …` từ gốc repo làm **content root sai** ⇒ `The ConnectionString property has not been initialized`.
2. Môi trường mặc định **Production** ⇒ seeder bật lại `ShouldChangePasswordOnNextLogin` cho `admin@abp.io`.
3. 🆕 **Chỉ MỘT tiến trình** được chạy `--backfill-catalog` tại một thời điểm. Bảng đối chiếu có index unique để **DB chặn** ca hai tiến trình, nhưng triệu chứng khi đó là `23505` khó hiểu — script không tự khoá phân tán.

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Quyết định |
|---|---|
| **D-22** | **Backfill có bảng đối chiếu + dừng-không-ghi khi xung đột.** `CatVariantMaps (TenantId, VariantId)` unique **NND** là nguồn sự thật của *"biến thể nào đã chuyển"* (không suy ra từ `Product.Code`); mọi xung đột (mã trùng, barcode trùng, cha mất) ⇒ **ABORT, không ghi gì**, exit `1`; mọi insert/update trong **một** transaction |
| **D-23** | **"Có gì để bán" = ĐVT gốc HOẶC biến thể cũ đang hoạt động** (giữ mã `0003`); **sản phẩm cha không publish được** (mã `0047`, chỉ áp cho **lần chuyển trạng thái mới**, không áp cho dữ liệu đã Published); **chu trình nhiều mức** chặn ở app service bằng `CaySanPham` (mã `0048`, giữ `0043` cho ca một mức) ⇒ `UpdateProductDto` **phải** mang `ParentId` |
| **D-24** | **Adapter đọc cũ** `IProductLegacyReader` + `ProductLegacyAdapter`: **biến thể trước, ĐVT gốc sau**; chỉ **ĐVT gốc** được đưa ra đường đọc cũ (ĐVT khác không có khoá tra giá); `UpdateProductDto.ParentId = null` nghĩa là **bỏ cha** |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`AutoInclude` cho `Units` là điều kiện sống còn của luật publish mới** (`CatalogDbContextModelCreatingExtensions`). Nếu ai bỏ nó: `Product.Units` rỗng khi đọc từ DB ⇒ **mọi** sản phẩm mô hình Nhanh lại **không publish được** (403 lúc bấm, build vẫn xanh). Đây đúng loại lỗi "sửa nửa đường" đã xảy ra ở `N2` (guard đúng, dữ liệu nạp sai).
2. 🚨 **Backfill chưa chạy trên dữ liệu thật** — mới chạy trên DB dev (0 biến thể) và **bản sao dựng tay** (2 biến thể, 1 đơn). Số liệu thật (108 sản phẩm, số biến thể/đơn chưa biết) **chưa** được đo ⇒ lần chạy đầu trên dữ liệu thật **phải** là `-DryRun` và đọc dòng `CONFLICT` trước khi chạy thật.
3. ⚠️ **Trùng vai trò index** (giống nợ `N2`): EF tự sinh `IX_CatVariantMaps_ProductId` (1 cột) song song `IX_CatVariantMaps_TenantId_ProductId`, và `IX_CatProducts_ParentId` song song `IX_CatProducts_TenantId_ParentId`. Không sai, chỉ thừa — dọn ở migration sau.
4. ⚠️ **`/SanPham/ChiTiet/{slug}` trả 404 khi `Product.Slug` là NULL** (đo được, ca D §1.5). Hành vi **có sẵn** từ `CAT-003`, nhưng **backfill không set slug** ⇒ sản phẩm con chỉ có URL chi tiết **sau khi** ai đó đặt slug. Nếu phiên FE form sản phẩm không nhớ điều này thì mọi link từ danh sách sẽ 404.
5. ⚠️ **Storefront chỉ thấy ĐVT GỐC** của sản phẩm mô hình mới (giới hạn của `ProductLegacyAdapter`, D-24). Bán theo thùng/kiện phải đọc thẳng `Product.Units` (POS, WP-06) — đừng "sửa" adapter bằng cách tự chế khoá `Code + …` (sẽ tạo mã giá không tồn tại trong bảng giá).
6. ⚠️ **`PublishAsync` kiểm "có con" bằng `AnyAsync` trên toàn bảng** mỗi lần publish — chưa có cache/CTE. Quy mô hiện tại ổn; khi danh mục lớn phải tính (index `IX_CatProducts_TenantId_ParentId` đã có).
7. 📌 **Bản sao DB `ecommerce_n3_copy` vẫn tồn tại** trên máy dev và **chứa dữ liệu thử** (3 sản phẩm, 1 đơn, 1 bảng giá). Không ảnh hưởng DB dev, nhưng đừng trỏ nhầm Web/Host vào nó.
8. 📌 **Cây làm việc vẫn dơ** (toàn bộ `N1`+`N2`+`N3` chưa commit). Nếu phiên sau `git checkout`/`reset` mạnh tay sẽ **mất cả ba phiên**.
9. 📌 **`openapi.v1.json` đã sinh lại** ⇒ nội dung `UpdateProductDto` **có** `parentId`. Đường `/api/v1` **không đổi**; khi CI có job `openapi-drift` thì đây là trạng thái chuẩn.

---

## 6. Việc đầu tiên của phiên sau (`N4` — WP-04 BE, tồn kho 9 cột)

1. **Đọc [handover-n2 §7.1](handover-n2.md) trước khi chạy `dotnet ef`**: phiên `Inventory` từng chạm cùng cây làm việc. Kiểm `git status` xem có migration lạ; **chỉ một** phiên được `dotnet ef migrations add` tại một thời điểm.
2. **WP-04 theo [07 §WP-04](07-ke-hoach-trien-khai.md)**: `InventoryItem` +8 cột (int → `decimal(18,3)`), `StockDocument.Mode`, `StockMovementType` append 9 giá trị, `StockPostingService` idempotent — **migration 53**.
3. **Trên dữ liệu thật**: chạy `backfill-catalog.ps1 -DryRun` **trước**, đọc `CONFLICT`, rồi mới chạy thật; sau đó kiểm `select count(*) from "CatVariantMaps"` = số biến thể.

Lệnh mở phiên (đúng §4):
```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh hiện tại | `feature/wp-03-catalog` |
| Commit | **CHƯA commit** gì cho `N2`/`N3`; `HEAD` vẫn là `713acdd` |
| Push / PR | **Chưa** push, **chưa** có PR |
| Cây làm việc | **Dơ có chủ ý**: (a) `N1` chưa commit (apps/, `src/eCommerce.HttpApi/`, packages/, pnpm-*); (b) `N2` (migration 51 + 6 entity); (c) `N3` (migration 52, backfill, luật cha–con, adapter) |
| Tệp `N3` đã chạm | `modules/Catalog/eCommerce.Catalog.Domain/{Product.cs,CaySanPham.cs,ProductLegacyAdapter.cs,ProductVariantMap.cs}`, `modules/Catalog/eCommerce.Catalog.Application/{ProductAppService.cs,IProductLegacyReader.cs,ProductLegacyReader.cs}`, `modules/Catalog/eCommerce.Catalog.Application.Contracts/ProductDtos.cs`, `modules/Catalog/eCommerce.Catalog.EntityFrameworkCore/CatalogDbContextModelCreatingExtensions.cs`, `modules/Sales/eCommerce.Sales.Domain/Order.cs`, `modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs`, `src/eCommerce.Domain.Shared/eCommerceConsts.cs`, `src/eCommerce.EntityFrameworkCore/{eCommerceDbContext.cs,Migrations/20261102093528_*,.…ModelSnapshot.cs}`, `src/eCommerce.DbMigrator/{Program.cs,BackfillCatalogService.cs}`, `src/eCommerce.Web/Storefront/DocSanPhamCongKhai.cs`, `tools/data-migration/backfill-catalog.ps1`, `packages/api-client/{openapi.v1.json,src/schema.d.ts}`, `test/eCommerce.Tests/Catalog/ProductNhanhModelNuaCuoiTests.cs`, `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`, `test/eCommerce.IntegrationTests/{Assets/schema.sql,PostgreSqlSchemaTests.cs}`, docs |

📌 **Đề nghị trước khi mở `N4`:** commit thành 3 commit tách bạch — `N1` (nền FE + HttpApi), `N2` (migration 51), `N3`
(`feat(catalog): WP-03 second half — backfill + publish rules + legacy reader`) — để lần lùi đầu tiên không phải
`git reset` cả cây.
