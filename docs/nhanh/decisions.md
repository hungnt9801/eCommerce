# Biên bản quyết định (decision log) — dự án Nhanh-hoá

> Mỗi dòng là **một quyết định có hệ quả kỹ thuật**. Trạng thái: `ĐỀ XUẤT` → `ĐÃ CHỐT` / `TỪ CHỐI`.
> Không sửa quyết định đã chốt — thay bằng dòng mới ghi rõ "thay thế D-x".

**Ngày chốt:** 02/11/2026 · **Người chốt:** chủ dự án, theo đề xuất của phiên khảo sát
⇒ **D-01 … D-21 đều ĐÃ CHỐT** với đúng giá trị đề xuất.

| ID | Quyết định | Giá trị đã chốt | Trạng thái |
|---|---|---|---|
| D-01 | Mô hình đa doanh nghiệp | Thiết kế `TenantId` **từ đầu** trên mọi bảng mới; **chưa bật** UI/tenant management ở GĐ0–GĐ2; resolve `businessId` sau | ĐÃ CHỐT |
| D-02 | Backend | **Giữ ABP monolith + refactor tại chỗ**, không viết lại NestJS | ĐÃ CHỐT |
| D-03 | Kế toán | GĐ0–GĐ3: **công nợ + xuất Excel/MISA**; bút toán kép đầy đủ để **GĐ4** | ĐÃ CHỐT |
| D-04 | POS offline | **Có** — IndexedDB + `ClientBillId` idempotent (+2 tuần) | ĐÃ CHỐT |
| D-05 | Phạm vi báo cáo | **12 báo cáo** P0/P1 (không làm 90) | ĐÃ CHỐT |
| D-06 | Website bán hàng | **Không** làm lại; giữ `eCommerce.Web` storefront hiện tại | ĐÃ CHỐT |
| D-07 | Mô hình sản phẩm | Bỏ `ProductVariant` là nơi giữ tồn/giá → `ParentId` + `ProductUnit` + `Type` (10 loại) | ĐÃ CHỐT |
| D-08 | Trạng thái đơn | Giữ **4 trục**, mở rộng `FulfillmentStatus` lên **18 giá trị** (append); UI hiển thị 1 nhãn suy ra | ĐÃ CHỐT |
| D-09 | Quy ước đặt tên | API/DTO **tiếng Anh**; domain method tiếng Việt được phép; **không rename hàng loạt** | ĐÃ CHỐT |
| D-10 | UI | **Next.js App Router** thay dần Razor; Razor **đóng băng** sau khi P0 xong | ĐÃ CHỐT |
| D-11 | Route Web Admin | `/{business}/product/item` (path); middleware **redirect 308** từ `?businessId=` | ĐÃ CHỐT |
| D-12 | Token | Chỉ giữ **server-side** (BFF + cookie httpOnly), **không** localStorage | ĐÃ CHỐT |
| D-13 | Bề mặt API | API mới `/api/v1/**` bằng **controller tường minh**; `/api/app/**` (ABP conventional) **giữ nguyên** cho Razor/tích hợp | ĐÃ CHỐT |
| D-14 | Project API | Thêm **`src/eCommerce.HttpApi`** chứa controller v1 (không nhét vào Host) | ĐÃ CHỐT |
| D-15 | Monorepo FE | **pnpm workspace**: `apps/admin-web` + `packages/api-client`; không trộn vào slnx | ĐÃ CHỐT |
| D-16 | Môi trường dev | BFF gọi **`http://localhost:54275`** (OpenIddict dev đã cho phép HTTP); production HTTPS | ĐÃ CHỐT |
| D-17 | Enum lưu DB | Chỉ **thêm giá trị ở CUỐI** dải, không đổi số cũ (quy ước đã có trong repo) | ĐÃ CHỐT |
| D-18 | POS | **Aggregate riêng** `RetailBill` + `CashSession`; **không** tái dùng `Order` | ĐÃ CHỐT |
| D-19 | `ProductStatus` vs 4 giá trị Nhanh | **Giữ nguyên** 4 giá trị hiện có (`Draft/Published/Unpublished/Archived`); **"Hết hàng"** của Nhanh là trạng thái **SUY RA** từ tồn kho, **không** thêm giá trị enum (D-17: không đổi số cũ; Nhanh `4` sẽ va `Archived=3` nếu nán theo) | ĐÃ CHỐT |
| D-20 | `ProductImeiStatus` | Dùng **10 giá trị đo được** từ Open API v3 (`nhanh-pos-v3-api-inventory.md` §2.2), **giữ khoảng trống số `4`** (Nhanh không có); **không** tự chế giá trị để "đủ 11" | ĐÃ CHỐT |
| D-21 | ĐVT & đường tạo sản phẩm | `ProductUnit` là **entity riêng** trong `Catalog` (`CatProductUnits`); **đúng một** ĐVT gốc do `Product.AddUnit` bảo đảm (+ unique index có filter ở DB); nửa đầu WP-03 **chỉ** tạo ĐVT **kèm** sản phẩm qua `POST /api/v1/products` — **chưa** có endpoint tạo ĐVT rời | ĐÃ CHỐT |
| D-22 | Backfill catalog (WP-03 mục 5, phiên `N3`) | Có **bảng đối chiếu** `CatVariantMaps (TenantId, VariantId)` unique **`NULLS NOT DISTINCT`** làm nguồn sự thật *"biến thể nào đã chuyển"* — **không** suy ra từ `Product.Code`. Mọi xung đột (mã sản phẩm trùng, barcode trùng toàn tenant, cha không tồn tại) ⇒ **ABORT, không ghi gì**, exit `1`; mọi insert/update trong **một** transaction. Chạy qua `eCommerce.DbMigrator --backfill-catalog [--dry-run]` + `tools/data-migration/backfill-catalog.ps1` | ĐÃ CHỐT |
| D-23 | "Có gì để bán" + luật cha–con (WP-03 nửa sau) | `Product.Publish()` chấp nhận **ĐVT gốc** *hoặc* **biến thể cũ đang hoạt động** (giữ mã `0003`); **sản phẩm cha không publish được** (`0047`, chỉ áp cho **lần chuyển trạng thái mới** — không áp cho dữ liệu đã `Published`); **chu trình nhiều mức** chặn ở app service bằng `CaySanPham` (`0048`, giữ `0043` cho ca một mức) ⇒ `UpdateProductDto` **phải** mang `ParentId` | ĐÃ CHỐT |
| D-24 | Adapter đọc cũ (WP-03 mục 7) | `IProductLegacyReader` + `ProductLegacyAdapter` (không `[Authorize]`): **biến thể trước, ĐVT gốc sau**; chỉ **ĐVT gốc** vào đường đọc cũ (ĐVT khác không có khoá tra giá); `UpdateProductDto.ParentId = null` nghĩa là **bỏ cha** | ĐÃ CHỐT |
| D-25 | `int → decimal(18,3)` cho số lượng tồn (**hoãn**, phiên `N4`) | **GIỮ `int`** cho `OnHand/Reserved/8 chiều` + `StockLedger.QuantityDelta/BalanceAfter` + `StockDocumentLine.Quantity` trong WP-04. **Lý do đo được:** đổi kiểu chạm **16 tệp**, trong đó `Reconciliation` (`DoiSoatTonKho`, `ReconRunMismatch`, `StockBalanceReader` — **bảng lịch sử đối soát**) và **4 tệp test**; vượt ngưỡng "≤ 5 tệp ngoài phạm vi" của [08 §6](08-session-protocol.md). Bảng đối soát cần migration riêng + cập nhật `schema.sql`/NND. ⇒ Tách **WP-04b**, làm khi có ca thật cần số lẻ (cân đo/kệ hàng); cột mới đã là `int` nên **không** có di trú dở dang | ĐÃ CHỐT |
| D-26 | `RowVersion` cho `InventoryItem` (WP-04 việc 1) | **KHÔNG** thêm cột `RowVersion`: `InventoryItem` đã có `ConcurrencyStamp` của ABP và `StockReservationService` **đã** bắt `AbpDbConcurrencyException` ⇒ thêm cột thứ hai là **hai cơ chế cho một việc** (trái bất biến "một nghĩa cho một cột"). Ghi rõ để phiên sau không tưởng thiếu | ĐÃ CHỐT |
| D-27 | Nghĩa của 9 chiều + công thức `Available` | `OnHand` = **TỔNG** hàng thuộc kho. Chiều **đã rời kho** thì `OnHand` **giảm** (`Shipping`, `Warranty`); chiều **còn nằm trong kho nhưng không bán được** thì `OnHand` **giữ nguyên**, chỉ bị trừ khỏi `Available` (`Holding`, `Damaged`, `Transferring`). `InWarehouse = OnHand − Transferring`; `Available = OnHand − Reserved − Transferring − Holding − Damaged` (CÓ `Reserved` — nếu bỏ ra thì giữ chỗ làm `Available` **tăng vọt** ⇒ oversell). `Shipping`/`Warranty` **không** có trong công thức vì đã trừ ở `OnHand`. Mọi biến động phải giữ **mọi chiều ≥ 0**, `OnHand ≥ 0`, `Reserved ≤ OnHand`, `Available ≥ 0` | ĐÃ CHỐT |
| D-28 | Số của `StockDocumentMode` + 2 loại biến động thêm | `StockDocumentMode` dùng **đúng số Nhanh** (`1,2,3,4,5,6,8,10,13…19` — **15 giá trị**; tài liệu 07 ghi *"19 giá trị"* là **giá trị lớn nhất**, không phải số phần tử). Một phiếu **có `type` và `mode` cùng lúc** (`mode` chỉ để phân loại/báo cáo). Vì Nhanh có phiếu **xuất** cho NCC/Khác mà enum cũ không diễn đạt được, **append thêm 2 giá trị** `SupplierReturnOut=20`, `OtherOut=21` (D-17) — **không** mượn `TransferOut`/`Sale` (báo cáo sẽ sai) và **không** mượn `Adjustment` (nó **tăng** `OnHand` ⇒ đảo chiều nghiệp vụ) | ĐÃ CHỐT |
| D-29 | Chống tạo phiếu kho trùng khi retry | Khoá `IdempotencyKey` do client sinh (`ClientBillId` POS — D-04) + **unique index** `(TenantId, IdempotencyKey)` có bộ lọc `IS NOT NULL` và **`NULLS NOT DISTINCT`** (SQL tay). Trùng khoá ⇒ mã **`eCommerce:Inventory:0035`** (không phải `500`), **không** tạo phiếu thứ hai. 📌 Idempotency theo `(RefType, RefId, MovementType)` **không** đặt unique index: một đơn có thể có **nhiều chuyến giao** cùng `ShippingOut` ⇒ index đó sẽ **chặn oan**; hàng rào ghi trùng sổ cái vẫn là `(TenantId, ReferenceLineId, MovementType)` | ĐÃ CHỐT |
| D-30 | Loại chứng từ **phiếu XUẤT** (`WP-04`) | Thêm `StockDocumentType.GoodsIssue = 4` (**append**, D-17). 📌 **Lý do đo được:** enum cũ **không có** loại nào diễn đạt *"xuất vì bán lẻ/giao hàng/trả NCC"* ⇒ trước `WP-04` chỉ có thể **mượn** `StockTransfer` (báo cáo chuyển kho đếm cả hàng bán) hoặc `CustomerReturn` (sai chiều). Phiếu `GoodsIssue` **bắt buộc có `Mode`** (mã `eCommerce:Inventory:0036`) vì `mode` là thứ duy nhất quyết định bút toán | ĐÃ CHỐT |
| D-31 | **`int → decimal(18,3)`** cho số lượng (**THAY THẾ D-25**) | Đã **thực hiện** trong cùng WP-04 (phiên `N4b`), không tách sang WP khác: 16 cột đổi kiểu — `InvItems` (10), `InvStockLedger.QuantityDelta/BalanceAfter`, `InvStockDocumentLines.Quantity`, `ReconRunMismatches.OnHand/TongSoCai/ChenhLeh` — cùng entity, DTO, hàm thuần (`ApplyMovement`, `QuyetDinhGiuCho`, `HanMucKenh`, `TinhButToanChungTu`, `DoiSoatTonKho`, `NguongLech`). **Độ chính xác 3 số lẻ**: đủ cho gram/ml/cm và khớp Nhanh; `ConversionFactor` của `ProductUnit` vẫn 4 số (hệ số ≠ số lượng). 📌 **D-25 bị thay thế** vì lý do hoãn (bán kính 16 tệp) **đã được xử lý** trong một phiên có đo lại đầy đủ: 1773 test · 90 integration · probe 12 ca · backfill 0 lệch. ⚠️ **`StockReservation.Quantity` + hạn mức kênh vẫn `int`** (đơn hàng hiện chỉ có số nguyên) — đổi khi WP-05/POS cho phép đơn lẻ | ĐÃ CHỐT |
| D-32 | `RowVersion` cho `InventoryItem` (WP-04 việc 1) — **vẫn KHÔNG thêm** (thay thế D-26, giữ nguyên kết luận) | `InventoryItem` đã có `ConcurrencyStamp` (ABP) và `StockReservationService` bắt `AbpDbConcurrencyException`; thêm token thứ hai là **hai nguồn cho một bất biến**. 📌 Ghi ở đây để người đọc [07 §WP-04](07-ke-hoach-trien-khai.md) thấy rõ mục `RowVersion` **đã được xem xét và loại có lý do**, không phải bị bỏ sót | ĐÃ CHỐT |

## Cách dùng

1. Mọi PR mở đầu bằng dòng `Refs: D-xx` nếu có liên quan.
2. Quyết định bị đảo: thêm dòng `D-xx (thay thế D-yy)` + lý do, **không xoá** dòng cũ.
3. Quyết định mới phát sinh trong thi công: thêm dòng, chốt trong sprint review.

## Nhật ký

| Ngày | Việc |
|---|---|
| 02/11/2026 | Tạo biên bản từ [05 — Lộ trình](05-lo-trinh-rui-ro.md) §5; D-01…D-12 ở trạng thái ĐỀ XUẤT |
| 02/11/2026 | **Chốt toàn bộ D-01…D-12 theo đề xuất**; bổ sung D-13…D-18 rút ra khi lập kế hoạch triển khai ([07](07-ke-hoach-trien-khai.md)) |
| 02/11/2026 | **WP-03 nửa đầu (`N2`)**: D-19…D-21 — chốt 3 chỗ tài liệu 07 ghi chưa đủ rõ khi thi công thật: `ProductStatus` (không thêm giá trị), `ProductImeiStatus` (giữ khoảng trống số 4), và ĐVT là entity riêng + chỉ tạo kèm sản phẩm. Chi tiết: [handover-n2.md](handover-n2.md) |
| 02/11/2026 | **WP-03 nửa sau (`N3`)**: D-22…D-24 — backfill có bảng đối chiếu + dừng-không-ghi (D-22), "có gì để bán" theo ĐVT gốc + cha không bán được + chống chu trình (D-23), adapter đọc cũ biến thể-trước-ĐVT-sau (D-24). Chi tiết + số đo: [handover-n3.md](handover-n3.md) |
| 02/11/2026 | **WP-04 (BE) (`N4`)**: D-25…D-30 — hoãn `decimal` hoá số lượng (D-25), không thêm `RowVersion` (D-26), nghĩa 9 chiều + công thức `Available` (D-27), số `Mode` đúng Nhanh + 2 loại biến động thêm (D-28), chống tạo phiếu trùng bằng `IdempotencyKey` (D-29), thêm `GoodsIssue` cho phiếu xuất (D-30). Chi tiết + số đo: [handover-n4.md](handover-n4.md) |
| 02/11/2026 | **WP-04b (`N4b`) — HOÀN TẤT WP-04 (BE)**: **D-31 thay thế D-25** (đã `decimal(18,3)` hoá 16 cột số lượng + toàn bộ tầng đọc/ghi) và **D-32 thay thế D-26** (giữ kết luận: không thêm `RowVersion`). Migration **54**, 1773 test, probe 12 ca. Chi tiết: [handover-n4b.md](handover-n4b.md) |
