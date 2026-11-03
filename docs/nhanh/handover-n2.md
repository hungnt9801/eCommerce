# Handover `N2` — WP-03 nửa đầu (Catalog theo mô hình Nhanh)

> Ngày 02/11/2026 · nhánh `feature/wp-03-catalog` (tách từ `feature/wp-01-02-foundation` @ `713acdd`)
> Kế hoạch: [07 §WP-03](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) · Quyết định: [decisions.md](decisions.md)
> **Phạm vi đã làm đúng N2:** `ProductType/Unit/ParentId/VAT` + entity `ProductUnit` (và 5 entity phụ của WP-03),
> migration **51** additive, **tạo SP 3 ĐVT qua API thật**. Không làm backfill, không làm FE form, không sửa Razor.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4)

| # | Lệnh | Kết quả đo | Ghi chú |
|---|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch**: 5 tệp `M` + nhiều tệp `??` | Toàn bộ là việc **N1 chưa commit** (`apps/`, `src/eCommerce.HttpApi/`, `docs/nhanh/`, `packages/`, `pnpm-*`, `tools/_*.png`). Không có thay đổi nào của WP-03 ở thời điểm này |
| 2 | `git switch <nhánh-WP>` | `feature/wp-03-catalog` tạo từ `713acdd` | Tạo bằng `git switch -c`; cây làm việc dơ của N1 đi theo (xem §7) |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Running** | Docker 29.6.2 |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** · 27,6 s | ⚠️ 0 warning là do build **tăng dần** (project không biên dịch lại). Build sạch ở §1.3 cho **9 warning có sẵn** — khớp [baseline](baseline-2026-11-02.md) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1634/1634 pass** · 6 s · 11,9 s tổng | Khớp baseline S19 |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 101 `.md`, **423** link, **344** đường dẫn backtick, **0** chết | |
| — | Migration (đếm thêm) | **50** | số tiếp theo: `51` |

### 1.2 Đã thi công

| Hạng mục | Nội dung | Đo được |
|---|---|---|
| Enum mới (append-only) | `ProductType` 1..10, `VatMode` 1..2, `ProductImeiStatus` **10 giá trị** (giữ khoảng trống số 4) | `Enums.cs`; test `ImeiStatus_KHONG_co_gia_tri_so_4…` |
| `Product` | thêm `Type`, `ParentId?`, `VatPercent?`, `VatMode`, `WeightGram?`, `WarrantyMonths?`, `StorageTimeDays?`, `IsHot`, `IsNew`, `ExternalId?` + `Units[]` + `DatCha`/`DatVatPercent`/`AddUnit` | migration 51 thêm **10 cột**, default `Type=1`, `VatMode=2` |
| Entity mới | `ProductUnit`, `ProductImei`, `ProductBatch`, `ComboComponent`, `PackageComponent`, `ProductTag` + 6 `DbSet` | `CatProductUnits/Batches/Imeis/ComboComponents/PackageComponents/Tags` có trong DB |
| Migration **51** | `20261102085805_Wp03_Catalog_NhanhModel` — **additive**, có `Down`; **8 unique index viết tay `NULLS NOT DISTINCT`** | áp vào DB dev: `__EFMigrationsHistory` có `20261102085805_…`; NND **64 → 72** |
| `schema.sql` | sinh lại từ chính migration (chỉ **thêm** 232 dòng, **0** xoá) | `test/eCommerce.IntegrationTests/Assets/schema.sql` |
| Cổng ràng buộc DB | 5 ràng buộc ánh xạ mã nghiệp vụ + 7 dòng danh sách trắng **có lý do** | `PostgreSqlSchemaTests` **F1 pass**; `RangBuocDbChuaAnhXaTests` xanh (nằm trong 1671) |
| API ghi | `POST /api/v1/products` (201) nhận `type/vat/parentId/units[]`; `GET` trả thêm `units`, `type`, `vatPercent`, `vatMode`, `unitCount` | xem §1.4 |
| Test mới | `ProductNhanhModelTests` — **37 ca** (default, cha–con, VAT, ĐVT, IMEI/lô/combo/gói/nhãn) | **1634 → 1671** |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · 52,3 s — 9 warning là `CS8604` **có sẵn** ở `Address` (6) + `Sales` (3), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1671/1671 pass** · 0 failed · 8 s |
| `dotnet test test\eCommerce.IntegrationTests --filter "FullyQualifiedName~F1_du_72_index"` | **1/1 pass** · 251 ms (DB thật trong Docker qua Testcontainers) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 0 tham chiếu chết (xem §1.5) |
| Migration | **51** |
| NND (đo trên DB dev) | **72** — `select count(*) from pg_indexes … indexdef like '%NULLS NOT DISTINCT%'` |

### 1.4 `tạo SP 3 ĐVT qua API` — bằng chứng HTTP + DB (điều kiện xanh của N2)

```
POST http://localhost:54275/api/v1/products   (Bearer, Products.Default + Products.Create)
body: type=MultiUnit(10), vatPercent=8, vatMode=Excluded(2),
      units=[lon 1 (base, barcode …101), thùng 24 (…102), kiện 144 (…103)]
→ 201, JSON: "type":"MultiUnit", "vatPercent":8, "vatMode":"Excluded",
             "units":[3 dòng], "barcode":"8935000000101", "price":10000, "unitCount":3
GET  /api/v1/products/{id}      → 200, units = 3
GET  /api/v1/products?q=WP03…   → total=1, items[0].type=MultiUnit, price=10000, unitCount=3
```

DB dev sau khi tạo:

```
 Name  | ConversionFactor | IsBase |  SalePrice
-------+------------------+--------+-------------
 lon   |           1.0000 | t      |   10000.0000
 thùng |          24.0000 | f      |  228000.0000
 kiện  |         144.0000 | f      | 1350000.0000
```

**Ba ca lỗi qua API (mã đúng, không phải 500):**

| Ca | HTTP | `error.code` |
|---|---|---|
| Barcode trùng ĐVT khác | 403 | `eCommerce:Catalog:0041` |
| `ConversionFactor = 0` | 403 | `eCommerce:Catalog:0040` |
| `VatPercent = 101` | 403 | `eCommerce:Catalog:0045` |

**Hàng rào DB thật (chèn SQL thô, `TenantId = NULL` — đúng tình trạng 1 tenant):**

```
ERROR: duplicate key value violates unique constraint "IX_CatProdUnits_TenantId_Barcode"
ERROR: duplicate key value violates unique constraint "IX_CatProdUnits_MotDonViGoc"
```

⇒ `NULLS NOT DISTINCT` **có tác dụng thật** khi `TenantId = NULL` (đây là lỗi C4/F1 mà `PostgreSqlSchemaTests` sinh ra để chặn).

### 1.5 Cổng tài liệu cuối phiên

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — 103 `.md`, **434** link markdown, **347** đường dẫn backtick, **0** tham chiếu chết (chạy sau khi thêm handover này + sửa `decisions.md`/`07`).
`npm --prefix tools run check:mermaid` **chưa chạy lại** ở phiên này (không thêm khối Mermaid mới; nếu CI đòi thì chạy).

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

Đúng **nửa đầu** WP-03; các việc sau **cố ý chưa làm** (thuộc N3 — nửa sau):

1. **Backfill `CatProductVariants → Product con + ProductUnit`** (WP-03 mục 5) + bảng đối chiếu + `--dry-run`: **chưa có**.
   File dự kiến `tools/data-migration/backfill-catalog.ps1` + service trong `DbMigrator` — **chưa tồn tại**.
2. **`OrderLine.ProductId` + backfill theo `Sku`** (mục 6): **chưa làm**; `OrderLine` giữ nguyên `Sku`/`ProductVariantId`.
3. **`IProductLegacyReader`** cho Razor `/SanPham` (mục 7): **chưa làm**. Razor hiện vẫn đọc `ProductVariant` trực tiếp ⇒ **chưa có gì đọc `ProductUnit`** ngoài API v1 (chưa sửa view nào).
4. **FE form sản phẩm** (màn #5 của 04): **chưa làm** — nửa sau.
5. **API ghi cho IMEI/lô/combo/gói/nhãn**: **chưa có** (bảng đã dựng, chưa có đường ghi).
6. **Luật "SP cha không bán được"**, chống **chu trình cha–con nhiều mức**, bắt buộc serial cho `Type=Imei`: **chưa làm**
   (hiện chỉ chặn `ParentId == Id`). Đây là các ca trong DoD **đầy đủ** của WP-03, không phải N2.
7. **`UpdateProductDto` chưa mang trường mới** (`Type/VatPercent/…`): sửa sản phẩm hiện chỉ đổi `Name/Description/BrandId/Slug`.
8. **`Publish()` vẫn đòi `ProductVariant` đang hoạt động** — sản phẩm tạo theo mô hình mới (chỉ có ĐVT, 0 variant) **chưa publish được**.
   ⚠️ Đây là điểm nối bắt buộc ở N3 (đổi guard sang "có ĐVT gốc" hoặc "có variant cũ" trong giai đoạn chuyển tiếp).

**Không có tệp nào đang sửa dở** — mọi thay đổi đã build/test xanh.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | `20261102085805_Wp03_Catalog_NhanhModel` (migration thứ **51**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (container `ecommerce-postgres`), chạy bằng `cd src\eCommerce.DbMigrator; dotnet run` |
| Nội dung | +10 cột `CatProducts`; +6 bảng (`CatProductUnits`, `CatProductImeis`, `CatProdBatches`, `CatComboComponents`, `CatPackageComponents`, `CatProductTags`); +6 `CHECK`; +8 unique index **`NULLS NOT DISTINCT` viết tay**; +3 index thường; +1 self-FK `FK_CatProducts_CatProducts_ParentId` |
| `Down` | Có: xoá 6 bảng + 10 cột + FK + index + 2 `CHECK` (chỉ `CatProducts`); **không** xoá `CatProductVariants` |
| Cách lùi | `dotnet ef database update 20261029033402_Xc002_DlqReplay -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` (hoặc `migrations remove` nếu chưa chia sẻ) |
| `schema.sql` | đã sinh lại **từ chính migration**; diff **chỉ thêm** 232 dòng |

⚠️ **Hai điều phải biết khi chạy lại migrator:**
1. Phải `cd src\eCommerce.DbMigrator` rồi `dotnet run` — chạy `dotnet run --project …` từ gốc repo làm **content root sai**
   ⇒ `The ConnectionString property has not been initialized` (đã gặp và xác định nguyên nhân trong phiên này).
2. Chạy ở môi trường **Production** (mặc định) ⇒ seeder **bật cờ `ShouldChangePasswordOnNextLogin`** cho `admin@abp.io`
   ⇒ grant `password` trả `invalid_grant: ShouldChangePasswordOnNextLogin`. Phiên này đã **xoá cờ trong DB dev** để lấy token test:
   `update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "UserName" = 'admin';`
   📌 Đây là **dữ liệu dev**, không phải thay đổi mã; lần chạy migrator Production tiếp theo sẽ bật lại.

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Quyết định |
|---|---|
| **D-19** | `ProductStatus` **giữ nguyên** 4 giá trị hiện có; **"Hết hàng"** của Nhanh là trạng thái **suy ra** từ tồn kho ⇒ **không** thêm giá trị enum (Nhanh `4` sẽ va `Archived=3` nếu nán theo; D-17 cấm đổi số cũ) |
| **D-20** | `ProductImeiStatus` dùng **10 giá trị đo được** từ Open API v3, **giữ khoảng trống số 4** (Nhanh không có); không tự chế giá trị để "đủ 11" |
| **D-21** | `ProductUnit` là **entity riêng**; **đúng một** ĐVT gốc do domain + unique index có filter bảo đảm; nửa đầu WP-03 **chỉ** tạo ĐVT **kèm** sản phẩm qua `POST /api/v1/products` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`Publish()` vẫn đòi variant** (mục 2.8). Sản phẩm mới tạo bằng API **không publish được**; nếu FE nửa sau bấm publish sẽ nhận `eCommerce:Catalog:0003`.
2. 🚨 **Chưa có `ProductStatus` ↔ số của Nhanh.** API trả **chuỗi** (`Draft/Published/…`); Nhanh dùng `1..4` với nghĩa *Mới/Đang bán/Ngừng bán/Hết hàng*.
   Khi nhập/xuất dữ liệu Nhanh **phải** map tường minh (bảng map thuộc N3), nếu không *"Hết hàng"* của Nhanh sẽ bị đọc thành `Archived`.
3. ⚠️ **Business exception trả HTTP 403** (không phải 400/409) kèm `message` chung *"An internal error occurred…"* — hành vi **có sẵn của Host**, không do WP-03.
   Client **phải** đọc `error.code`; middleware RFC 7807 của WP-02 §4.4 **chưa được hiện thực**. Ghi lại để không ai "sửa" bằng cách đổi status ngẫu nhiên.
4. ⚠️ **`IX_CatProducts_ParentId`** (1 cột) do EF tự sinh cho FK, **trùng vai** với `IX_CatProducts_TenantId_ParentId`.
   Không sai, chỉ thừa; dọn ở migration sau (không đáng một migration riêng).
5. ⚠️ **`dotnet ef` để lại tiến trình Host sống** (design-time host) ⇒ lần build sau **khoá DLL** (`MSB3027/MSB3021`).
   Đã gặp 2 lần trong phiên này. Cách xử: `Get-Process eCommerce.HttpApi.Host | Stop-Process` **trước** khi build/rebuild.
6. ⚠️ **Cây làm việc vẫn dơ** (xem §7): toàn bộ N1 **chưa commit**. Nếu phiên sau `git checkout`/`reset` mạnh tay sẽ **mất N1** (apps/, HttpApi, tài liệu).
7. 📌 **Danh sách trắng ràng buộc** (`RangBuocChuaAnhXa.txt`) có 7 dòng WP-03 "chưa có đường ghi". Khi N3 thêm API IMEI/lô/combo/gói/nhãn,
   **phải** thay từng dòng bằng ánh xạ ở `ViPhamRangBuocDb` — nếu không, lỗi trùng sẽ rơi về mã chung `eCommerce:Db:0001`.

---

## 6. Việc đầu tiên của phiên sau (`N3` — WP-03 nửa sau)

1. **Backfill dry-run** (mục 5 của WP-03): viết `tools/data-migration/backfill-catalog.ps1` + service trong `DbMigrator`,
   chạy `--dry-run` trên DB dev **đang có 1 sản phẩm mới + 0 variant** ⇒ phải in `0 variant → 0 child`;
   sau đó chạy trên bản copy DB có variant thật. Điều kiện dừng: `n variant ≠ n child` ⇒ **dừng**, không ghi.
2. **Nối `Publish()`** với mô hình mới (ĐVT gốc) + test cho sản phẩm chỉ có ĐVT; thêm luật "SP cha không bán được" + chống chu trình.
3. **`OrderLine.ProductId`** + backfill theo `Sku`; **`IProductLegacyReader`** để Razor `/SanPham` vẫn 200.

Lệnh mở phiên (đúng §4):
```powershell
cd D:\Projects\Tools\eCommerce
git status --short
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh hiện tại | `feature/wp-03-catalog` (tạo từ `713acdd`, tức `feature/wp-01-02-foundation`) |
| Commit | **CHƯA commit** gì cho WP-03; `HEAD` vẫn là `713acdd` |
| Push / PR | **Chưa** push, **chưa** có PR |
| Cây làm việc | **Dơ có chủ ý**: (a) toàn bộ việc **N1 chưa commit** (apps/, `src/eCommerce.HttpApi/`, docs/nhanh/, packages/, pnpm-*, tools ảnh); (b) thay đổi WP-03 của phiên này (xem `git status`) |
| Tệp WP-03 đã chạm | `src/eCommerce.Domain.Shared/{Enums.cs,eCommerceConsts.cs}`, `modules/Catalog/**` (6 entity mới + mapping + DbSet + DTO/app service), `src/eCommerce.HttpApi/**`, migration 51 + snapshot/Designer, `test/…/Assets/schema.sql`, `PostgreSqlSchemaTests`, `RangBuocChuaAnhXa.txt`, `ViPhamRangBuocDb.cs`, `ProductNhanhModelTests.cs`, docs (decisions/07/handover) |

📌 **Đề nghị trước khi mở N3:** commit riêng N1 (hoặc ít nhất `git stash create`) để có điểm lùi, rồi commit WP-03 thành **một** commit
`feat(catalog): WP-03 first half — Nhanh model + migration 51` — phiên này **không tự commit** vì cây đang trộn 2 phiên và
chưa có yêu cầu từ bạn.

### 7.1 ⚠️ Phát hiện khi đóng phiên: có **phiên KHÁC đang chạy song song** trong CÙNG cây làm việc

Khi kiểm `git status` + mtime lúc đóng phiên, có các tệp **không do phiên này tạo** nhưng mới hơn lúc mở phiên:

| Tệp / nhóm | mtime | Suy ra (không khẳng định) |
|---|---|---|
| `docs/nhanh/09-parallel-sessions.md` (**mới**) | 15:57 | Một phiên khác đang viết tài liệu "chạy song song" |
| `docs/nhanh/README.md` | 15:53 | Phiên khác đã sửa bảng tài liệu (thêm dòng `09-…`); phiên này chỉ **thêm 1 dòng** `handover-n2` vào cùng bảng |
| `packages/api-client/{openapi.v1.json,scripts/*,package.json,src/schema.d.ts}` | 15:52 | Phiên khác chạy `gen:api` / snapshot OpenAPI |
| `src/eCommerce.Domain.Shared/Enums.Inventory.cs` | 15:54 | Phiên khác đang chạm **Inventory** (có thể là chuẩn bị WP-04) |
| `artifacts/logs/api-54275.{log,err.log}` | 15:57–15:58 | Có ai đó đã khởi động Host ở cổng 54275 (phiên này cũng dùng cổng đó, tuần tự) |

🔒 **Ảnh hưởng tới số đo của phiên này:** build sạch + 1671 test + F1 integration được chạy **sau** các mốc trên (16:06)
⇒ số đo **bao gồm** trạng thái hiện tại của các tệp đó, không phải chỉ thay đổi WP-03. Không có `migration 52` nào được sinh
⇒ **chưa** đụng `ModelSnapshot`/`schema.sql` của nhau.

🚨 **Rủi ro nếu phiên sau chạy tiếp mà không kiểm:** hai phiên cùng `dotnet ef migrations add` sẽ **ghi đè `ModelSnapshot`**
và làm `schema.sql` lệch — đúng điều `08 §7` cấm. Trước khi làm N3: `git status` xem `Enums.Inventory.cs`/migration mới,
và **chỉ một phiên** được chạy `dotnet ef` tại một thời điểm. Nếu phiên Inventory đã sinh migration 52 thì migration của
WP-03 phải được **rebase lại** (xoá + sinh lại) để thứ tự 51/52 không đảo.

