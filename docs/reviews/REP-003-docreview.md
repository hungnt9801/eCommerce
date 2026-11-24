# Doc review — «REP-003» «Báo cáo tồn kho luân chuyển & hiệu suất kênh»

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/reporting/rep-003-bao-cao-ton-kho-hieu-suat-kenh.md` |
| Ngày · vòng | 24/11/2026 · **vòng 3 — kiểm nhanh §8.2** (`changedSections = ["§8.2"]`; không mở lại mục khác) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | §8.2 dòng 566–607 (toàn bộ mục, nguyên văn) + `XC-003` §5/§5.4 dòng 199–262; nền: vòng 2 đã đọc toàn văn dòng 1–995 |
| Máy | `check-feature-doc.ps1 -Path <tệp> -WP14` → `KET QUA: DAT` (16/16 heading; 14/14 BR có test; mục «Đối chiếu mô hình Nhanh» dòng 974; §15 còn 0 câu `Open`) · không chạy lại `check:mermaid` (§8.2 không có sơ đồ Mermaid, phiên này không sửa Mermaid) |

## Vòng kiểm nhanh §8.2 — kết quả

**P2#1 vòng 2 (tiền tố `ecommerce:` trong bảng khoá §8.2): ĐÃ ĐÓNG — sửa đúng.** Kiểm bằng ba cách độc lập:

1. **Đọc lại nguyên văn §8.2** (dòng 583–606): bốn dòng bảng dòng 599–602 nay là `report:tong-hop`, `report:turnover:{tenantId}:{periodStart}`, `report:channel:{tenantId}:{channelCode}:{channelShopId}:{businessDate}`, `report:export:{tenantId}:{reportKind}:{from}:{to}` — **không** còn tiền tố. Dòng 590 (§8.2 #4) và dòng 587 (#1) dùng đúng cùng chuỗi `report:tong-hop` / `report:channel:…` — khớp từng ký tự với bảng.
2. **Grep toàn tài liệu** `` `report: `` → 10 chỗ (dòng 86, 159, 188, 587, 590, 599–602), **tất cả** cùng dạng không tiền tố; mọi chỗ còn chữ `ecommerce:` (dòng 599, 606, 901, 948) chỉ là **ghi chú giải thích** "ABP thêm tiền tố", không phải chuỗi khoá. Không còn `report:aggregation` / `report:export:…:{runId}` cũ ⇒ D1 nội bộ §8.2 sạch.
3. **Đối chiếu code thật** (G10): ba nơi được dẫn trong ghi chú dòng 606 đều trả tên **không** tiền tố — `eCommerceConsts.cs:816–817` (`inventory:{warehouseId}:{sku}`), `InventoryLockKeys.cs:16–17` (`inventory:{tenantId}:{warehouseId}:{sku}`), `ChannelHubConsts.cs:22–31` (`channel:…`); có test chặn hồi quy `test/eCommerce.Tests/DistributedLocking/LockKeyPrefixTests.cs` (`AppLockKeys_khong_chua_tien_to:58`, `ChannelLockKeys_khong_chua_tien_to:42`). Dẫn dòng as-built `TongHopBaoCaoService.TenKhoaPhanTan:44` (G9) **đúng dòng** — xem `modules/Reporting/eCommerce.Reporting.Application/TongHopBaoCaoService.cs:44` `public const string TenKhoaPhanTan = "report:tong-hop";`.

Không sinh lỗi mới ở mức P1/P2 trong §8.2. Còn **2 P3** (bên dưới): một về cách ghi khi bổ sung bảng đăng ký `XC-003` §5, một về nhãn "kế hoạch" của 3 tên lớp chưa tồn tại.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc nguyên văn §8.2 dòng 566–607; grep toàn tài liệu `` `report: `` (10 chỗ) và `ecommerce:report` (0 chỗ) rồi so từng chuỗi giữa bảng khoá 599–602 ↔ dòng 587/590 ↔ §3 dòng 159 ↔ §1 dòng 86 ↔ sơ đồ dòng 188 | 0 phát hiện (P2#1 cũ đã đóng) |
| D2 chéo tài liệu / code / D-xx | §8.2 ↔ `XC-003` §5 (dòng 204–213) + §5.4 (230–261) + `D-178`/`D-184`; đối chiếu 3 nguồn tên khoá với code (G9, G10) | 1 phát hiện (P3 #1 — quy ước cột khi trộn sang `XC-003` §5) |
| D3 cơ chế thực thi | §8.2 #1/#6: khoá **+** partial unique index `WHERE Status = Running` (§5.2 dòng 353) vẫn là cơ chế đỏ khi vi phạm; phiên này không đổi phần đó | 0 phát hiện |
| D4 SQL chạy được | grep ```` ```sql ````/```` ```powershell ````/```` ```bash ```` trong §8.2 và toàn tệp | **không có khối sql/lệnh** ⇒ không có gì để chạy (không phải "chưa chạy vì thiếu DB") |
| D5 test/probe/tệp có thật | grep `ReportingLockKeys` trong `**/*.cs` = **0**; `codegraph_explore` không có ký hiệu (G9). Dẫn dòng as-built `TongHopBaoCaoService.cs:44` + `eCommerceConsts.cs:816–817` + `InventoryLockKeys.cs:16–17` + `ChannelHubConsts.cs:22–31` đều **đúng** (G9, G10) | 1 phát hiện (P3 #2 — 3 tên lớp chưa tồn tại, không có nhãn "kế hoạch") |
| D6 nhãn tin cậy | rà nhãn trong §8.2: chỉ dòng 599 mang `(as-built …)`; dòng 600–602 không có nhãn ⬜/🔶/"kế hoạch" cho tên lớp chưa có | 1 phát hiện (gộp vào P3 #2) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5: #6 (tên khoá phân tán chính xác — đạt, L6), #11 (Mermaid — §8.2 không có), #12–#14 không áp dụng cho §8.2; NFR-002 "< 5 phút" so với TTL 1800 s vẫn nhất quán; mục «Đối chiếu mô hình Nhanh» (dòng 974) không đổi trong phiên | 0 phát hiện |

## Nguồn đồ thị đã dùng

| # | Truy vấn đồ thị | Kết quả (một dòng) |
|---|---|---|
| G1 | `graph: mcp__codegraph__codegraph_explore "StockLedger ApplyMovement StockPostingService ApDungButToan StockLedgerEntry QuantityDelta TinhButToanChungTu"` | `StockLedger.cs:103–181` `ApplyMovement`: dấu do `MovementType`, `QuantityDelta` là độ lớn dương — khớp §5.2 dòng 257/266. |
| G2 | `graph: mcp__codegraph__codegraph_explore "Order LineTotal TaxAmount DiscountTotal ShippingTotal ChannelFeeAmount ShippingFeeActual ChannelShopId ChannelCode OrderLine Quantity"` | `Order.cs:832` `Quantity int`; `:838` `TaxAmount`; `:840` `LineTotal` cộng thuế; `ChannelCode :19`. |
| G3 | `graph: mcp__codegraph__codegraph_explore "AnhXaNguonBanHang ThanhTienDongDonHang ReportingDbContextModelCreatingExtensions ConfigureReporting RptFactSalesLine HasPrecision"` | `ThanhTienDongDonHang` **không** gồm thuế; `RptFactSalesLine.Quantity.HasPrecision(18,3)` — khớp §5.2. |
| G4 | `graph: mcp__codegraph__codegraph_explore "eCommerceConsts LockKeys InventorySku InventoryLockKeys TheoSku ChannelHubConsts"` | Cả ba nơi trả tên khoá **không** tiền tố — khớp ghi chú dòng 606. |
| G5 | `graph: mcp__codegraph__codegraph_explore "eCommerceDbContext OnModelCreating ConfigureReporting HasFilter IX_MsgDeadLetters"` | `eCommerceDbContext.cs:448` gọi `ConfigureReporting`; `HasFilter :434` — khớp §11. |
| G6 | `graph: mcp__codegraph__codegraph_explore "Order DiscountTotal ChannelFeeAmount ShippingTotal ShippingFeeActual ChannelCode OrderStatusHistory OccurredAtUtc Chuyen SetStatus"` | `OrderStatusHistory.cs:55` `OccurredAtUtc`; `Order.Chuyen` :579–693; `SetStatus` đã xoá — khớp F6. |
| G7 | `graph: mcp__codegraph__codegraph_explore "Order ShippingFeeActual StockMovementType ... OrderStatus ... Enums"` | `Order.cs:127` `ShippingFeeActual?`; `StockMovementType Sale=3 / Transfer=6 / ShippingOut=11 / RetailSaleOut=19` — khớp §5.2/TC-37. |
| G8 | `graph: mcp__codegraph__codegraph_explore "ReportingOptions NhipJobPhut ReportingAggregationWorker NhipMacDinhPhut TongHopBaoCaoService TenKhoaPhanTan"` | `ReportingOptions.cs:18` `NhipJobPhut = 15`; `ReportingAggregationWorker.cs:31` `NhipMacDinhPhut = 15` — khớp §9. |
| G9 | `graph: mcp__codegraph__codegraph_explore "ReportingLockKeys TurnoverRollup ChannelDay Export TongHopBaoCaoService TenKhoaPhanTan"` (vòng 3) | `TenKhoaPhanTan` tại `TongHopBaoCaoService.cs:44` (đúng dẫn dòng 599); **`ReportingLockKeys`, `TurnoverRollup`, `ChannelDay` không có trong chỉ mục** ("Not in the index") — khớp `grep -rn "ReportingLockKeys" --include=*.cs` = 0. |
| G10 | `graph: mcp__codegraph__codegraph_explore "InventoryLockKeys TheoSku ChannelHubConsts LockKeys KeyPrefix AbpDistributedLockOptions"` (vòng 3) | `InventoryLockKeys.TheoSku` :16–17 → `inventory:{tenantId}:{warehouseId}:{sku}`; `ChannelHubConsts.LockKeys.*` :22–31 → `channel:…`; `eCommerceConsts.LockKeys.InventorySku` :816–817 → `inventory:{warehouseId}:{sku}` — **không** tiền tố; test `LockKeyPrefixTests.cs` tồn tại. |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §8.2 dòng 604 (lệnh "bổ sung 4 dòng vào `XC-003` §5") vs `XC-003` §5 dòng 204–213 | **Hai quy ước cho cùng một cột "Khoá (mẫu)".** `XC-003` §5 ghi **khoá Redis thật** (có tiền tố: `ecommerce:inventory:…` dòng 206, `ecommerce:channel:…` dòng 207–209 — đối chiếu G10: hàm sinh khoá trả `inventory:…`/`channel:…`), còn bảng §8.2 vừa sửa ghi **tên do hàm sinh** (không tiền tố). Ai gộp 4 dòng sang `XC-003` §5 theo đúng kiểu các dòng đang có sẽ ghi lại `ecommerce:report:…` vào bảng đăng ký ⇒ người code theo bảng đăng ký có thể đặt tiền tố vào hàm sinh khoá ⇒ **tiền tố đôi** — đúng lỗi P2#1 vừa đóng, nhưng ở tài liệu nguồn. | Ghi rõ ngay tại dòng 599/604: cột `Khoá (mẫu)` của `XC-003` §5 đang trình bày khoá thật (tiền tố do `KeyPrefix` cấp) ⇒ 4 dòng `report:*` phải ghi **tên không tiền tố**; hoặc tách 2 cột "tên hàm sinh khoá" / "khoá thật" như `ADDR-003` §8.2 (dòng 683–686). Ghi chú dòng 606 hiện đã có nội dung này nhưng **nằm ngoài bảng** nên không đi theo khi trích 4 dòng. |
| 2 | P3 | §8.2 dòng 600–602 (cột "Hàm sinh khoá") | **Ba tên lớp không tồn tại mà không có nhãn "kế hoạch".** `ReportingLockKeys.TurnoverRollup/ChannelDay/Export` — `grep -rn "ReportingLockKeys" --include=*.cs` = **0**, `codegraph_explore` không có ký hiệu (G9); chỉ dòng 599 mang nhãn `(as-built …)`, ba dòng còn lại trình bày như đã có. `XC-003` §5 đánh dấu tương tự bằng `*(đề xuất)*` (dòng 212–213) — L10/D6 yêu cầu phần chưa có phải ghi "kế hoạch". | Thêm "(⬜ kế hoạch — chưa có trong code)" vào 3 ô cột "Hàm sinh khoá" (hoặc một ghi chú ngay dưới bảng), không đổi tên lớp. |

> 0 P1 · 0 P2 · **2 P3 mới** (§8.2) ⇒ `DOC-OK`. P3 #2/#3 của vòng 2 (§13, §0.1 D10, §5.2 dòng 293 — ngoài `changedSections`) **giữ nguyên** cho người duyệt, không mở lại.

## Không kiểm được

- **D4:** §8.2 và toàn tài liệu **không có** khối `sql`/`powershell`/`bash` ⇒ không có lệnh để chạy; phiên này không chạy `psql` (không cần) và không chạy lại `npm --prefix tools run check:mermaid` (không sửa Mermaid).
- Không chạy `dotnet test`: dẫn chứng test `LockKeyPrefixTests.cs` là **đọc mã nguồn**, không phải kết quả chạy.
- Kết luận "hàm sinh khoá không tiền tố" là kết luận **tĩnh** (đọc mã + đồ thị), không đo runtime bằng `redis-cli MONITOR`; `KeyPrefix = "ecommerce:"` của Host lấy theo `XC-003` §5.4 (đã đo 13/10/2026), không đo lại.

## Vòng trước

### Vòng 2 (24/11/2026)

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | **đã đóng** — bảng khoá §8.2 dòng 599–602 nay viết **không** tiền tố, khớp ghi chú dòng 606 và code (G10) | P2 |
| 2 | **còn** — số dòng metric/test đã trôi ở §0.1 D10 / §13 (ngoài `changedSections`, giữ cho người duyệt; **không** chép lại lên bảng Phát hiện) | P3 |
| 3 | **còn** — §5.2 dòng 293 dẫn `Order.ChannelCode (Order.cs:18)` trượt 1 dòng, thật ở `:19` (G2) (ngoài `changedSections`) | P3 |

### Vòng 1 (24/11/2026)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — §5.2 dòng 257/266 dùng `Σ (dấu theo MovementType) × QuantityDelta`, khớp `ApplyMovement` (G1) | P1 |
| 2 | đã đóng — §5.2 dòng 298/304 dùng `Σ (LineTotal − TaxAmount)` = `ThanhTienDongDonHang`, cấm `OrderLine.LineTotal` (G2/G3) | P1 |
| 3 | đã đóng ở vòng 3 — bảng khoá §8.2 hết tiền tố (vòng 2 mới đóng một phần) | P2 |
| 4 | đã đóng — F6 nay đúng: `OrderStatusHistory` đã có, `SetStatus` đã xoá (G6) | P2 |
| 5 | đã đóng — F3/§0 nay dẫn `modules/Sales/.../Order.cs:808–840`, `TaxAmount :838` (G2) | P2 |
| 6 | đã đóng — §11 nay ghi `ConfigureReporting` đã có và đã gọi tại `eCommerceDbContext.cs:448`, `HasFilter :434` (G5) | P2 |
| 7 | đã đóng — `UnitsSold` nay `numeric(18,3)`/`decimal` ở §5.2, §6.1, Đối chiếu (G3) | P2 |
| 8 | đã đóng — §1/§9/CHECK nay thống nhất "lệch = 0, không có ngưỡng dung sai" | P2 |
| 9 | đã đóng — BR-004/§8.1#5/§3 nay nói rõ "nội dung bất biến; một chuyển `Published → Superseded`" | P2 |
| 10 | còn (vòng 2 #2) | P3 |
| 11 | đã đóng — changelog v0.2 nay "TC-27…TC-37 (11 TC)" → 37 | P3 |
| 12 | đã đóng — §5.2 nêu nguồn cột; `RptReconciliations` tách kiểu số lượng `numeric(18,3)` | P3 |
| 13 | đã đóng — §6.1 ghi rõ namespace `eCommerce.Reporting.Sources` (project `eCommerce.Reporting.Domain`) | P3 |
