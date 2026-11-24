# Doc review — «REP-002» «Báo cáo doanh thu và lãi gộp»

**VERDICT: DOC-CHANGES**

| | |
|---|---|
| Tài liệu | `docs/features/reporting/rep-002-bao-cao-doanh-thu-lai-gop.md` |
| Ngày · vòng | 24/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–995 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → `KET QUA: DAT` (16/16 heading, 18/18 BR có test, mục đối chiếu Nhanh dòng 952) · `kiem-link-tai-lieu.ps1` → PASS (0 tham chiếu chết) · `check:mermaid` → 212 khối · 0 lỗi |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc toàn văn + grep `cấm`/`không được`/`chỉ`/`chưa có` + đối chiếu từng mục BA đã sửa với phần **không** sửa (§4.3, §5.1, §5.2.2, §8.2) | 2 phát hiện |
| D2 chéo tài liệu / code / D-xx | đối chiếu 20 mốc `file:dòng` của các mục đã sửa bằng codegraph + `Read`; đối chiếu chéo header `SAL-002` (✅ đã có code), `REC-002`, `D-114`/`D-171`/`D-172`/`D-175` | 2 phát hiện |
| D3 cơ chế thực thi | 18 BR "phải/không được/không bao giờ" → cái gì đỏ khi vi phạm; soi riêng BR-018 mới thêm | 1 phát hiện |
| D4 SQL chạy được | grep ```` ```sql ```` / ```` ```powershell ```` / ```` ```bash ```` trên toàn tài liệu | không có khối sql (0 phát hiện) |
| D5 test/probe/tệp có thật | grep `Wp08BaoCaoTests` (5/5 tên test §14.1 có thật; dòng 430 có test 5 route) · `PostgreSqlSchemaFixture`/`PostgreSqlSchemaTests` · `MetricsTests.cs:55-80` · `apps/admin-web/lib/report.ts:81,93` | 0 phát hiện |
| D6 nhãn tin cậy | ✅/🔶/❌ có số đo đi kèm? (`Money` nay đã dẫn `Money.cs:23`; "test xanh" chưa đo lại — xem *Không kiểm được*) | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14; mục "Đối chiếu mô hình Nhanh" dòng 952; `check:mermaid`; đối chiếu bảng §5.2 với mapping thật | 3 phát hiện |

## Nguồn đồ thị đã dùng

- `graph: mcp__codegraph__codegraph_explore "CostPrice CostPriceAppService PrcCostPrices PricingDbContextModelCreatingExtensions PricingApplicationModule CostPriceTests"` → `CostPrice` entity (`CostPrice.cs:36-141`), `CostPriceAppService.cs:25-83` **chỉ có `GetListAsync`/`GetAsync`** (không method ghi), `ICostPriceAppService.cs:42-52`, test `test/eCommerce.Tests/Pricing/CostPriceTests.cs`; 4 project Pricing ở `eCommerce.slnx:71-75`.
- `graph: mcp__codegraph__codegraph_explore "TongHopBaoCaoService TinhNgayAsync TongHopDoanhThu ChungTuKhongCoDong TinhDoanhThuChungTu KhoangTongHop"` → lọc nguồn `TongHopBaoCaoService.cs:255-259`; bỏ qua chứng từ 0 dòng + `Warning` ở `:271-287` và `:303-317`; `Reports:0009` chỉ ném trong `TongHopDoanhThu.TinhNgay:119-125` (hàm thuần).
- `graph: mcp__codegraph__codegraph_explore "ReportingDbContextModelCreatingExtensions ConfigureReporting RptAggDailySales RptFactSalesLine FactSalesLine"` → `RptAggDailySales` **chỉ** có UNIQUE `(TenantId, BusinessDate, Nguon)` (`ReportingDbContextModelCreatingExtensions.cs:189-192`); `RptFactSalesLine` (`FactSalesLine.cs:17-154`) **không có cột giá vốn**; các cột/index còn lại khớp §5.2.2.
- `graph: mcp__codegraph__codegraph_explore "modules/Reporting/eCommerce.Reporting.EntityFrameworkCore/ReportingDbContextModelCreatingExtensions.cs:159-215"` → xác nhận toàn bộ nhánh `RptAggDailySales` + `RptWatermarks` (không có INDEX `(TenantId, BusinessDate)`).
- `graph: mcp__codegraph__codegraph_explore "Order ShippingFeeActual ShippingFeeCustomer ChannelFeeAmount LineTotal TaxAmount OrderLine ShippingCostAmount"` → `ShippingCostAmount` **không có trong chỉ mục**; `ShippingFeeCustomer`/`ShippingFeeActual`/`ChannelFeeAmount`/`LineTotal` có thật; `Read Order.cs:112-173` xác nhận `:124`, `:127`, `:167`, `:840`.
- `graph: mcp__codegraph__codegraph_explore "eCommerceHttpApiHostModule AbpDistributedLockOptions KeyPrefix"` → `KeyPrefix` ở `eCommerceHttpApiHostModule.cs:222` và `:237` (khối lock `:225-238`); `Read :210-245` xác nhận.
- `graph: mcp__codegraph__codegraph_explore "KhoangTongHop XacDinh SoNgayLanDau SoNgayToiDa CacNgay"` → job chạy từ **mốc + 1 ngày** tới hôm nay (`KhoangTongHop.cs:62-72`), chỉ lùi **30 ngày** ở lần đầu chưa có mốc (`:28,58-61`); **không** có cửa sổ làm lại N ngày.
- `graph: mcp__codegraph__codegraph_explore "CheckoutAppService TinhVaGiuCho Checkout Application SAL-002"` → `SAL-002` **đã có code**: `modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:39` (`TinhVaGiuChoAsync`), `CheckoutDtos.cs:8-23` (`TaxRate` đầu vào).
- `graph: mcp__codegraph__codegraph_explore "Shipment DeliveredAtUtc ReconciliationSource ConfirmedAtUtc Settlement MarketplaceSettlement"` → `DeliveredAtUtc`/`ReconciliationSource`/`MarketplaceSettlement` **không có trong chỉ mục**; `grep Shipping eCommerce.slnx` 0 kết quả ⇒ khẳng định "kế hoạch GĐ3" của Q10 là đúng.

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §4.3 dòng 303 | Sơ đồ luồng dữ liệu vẫn ghi `EXT[("<b>PRC-001 PRC-002</b><br/>gia von - CHUA CO trong code")]`, trái với thân đã sửa §0 dòng 40/50, §5.2.4 dòng 466, §11 dòng 757, §14.4 dòng 904 ("PRC đã có bảng đọc `PrcCostPrices`"). Người đọc sơ đồ sẽ dựng nguồn giá vốn thứ hai — đúng cơ chế lỗi của P1 vòng 1. | Sửa nhãn node: "PRC-001/PRC-002 — bảng đọc `PrcCostPrices` (chưa có cửa ghi)"; rà lại mũi tên `EXT -. "gia von theo ky - khi co" .-> P2`. |
| 2 | P2 | §8.1 dòng 651, 670-672; §14.1 TC-02 dòng 833 | Allow-list "**đúng một** method `GetTongQuanDonHangAsync`" khi quét `modules/Reporting/**`, nhưng `TongHopBaoCaoService` (job) cũng inject `IRepository<Order>`/`IRepository<OrderLine>` (`TongHopBaoCaoService.cs:70-71`) và đọc ở `TinhNgayAsync:258-259`. Test đặc tả như vậy **đỏ trên code hợp lệ** (job không phải "đường đọc"), nên TC-02 không thể xanh như DoD §14.3 yêu cầu. | Giới hạn phạm vi quét ở đường đọc (`ReportAppService.GetDoanhThuAsync`) hoặc allow-list **hai** mục: `TongHopBaoCaoService` (job) + `GetTongQuanDonHangAsync`; ghi rõ BR-001 chỉ áp cho **đường đọc**, không áp cho job. |
| 3 | P2 | §5.2.4 dòng 466-467; §5.1 ERD dòng 350-394; §5.2.2 dòng 427-436; §6.3 dòng 573 | Câu "Giá vốn đơn vị được **chụp vào fact lúc bán**" không có chỗ lưu: `RptFactSalesLine` (`FactSalesLine.cs:17-61`) **không có cột giá vốn**, §5.2.2 không liệt kê cột đó, còn ERD đặt `CostUnitAmount` ở `RptGrossMarginSnapshot` (bảng **theo kỳ** `PeriodFrom/PeriodTo`). Không xác định được nơi chụp ⇒ người code dễ tra lại bảng kỳ lúc đọc, trái TC-25/TC-36 ("sửa bảng giá vốn sau ngày bán **không** đổi lãi gộp kỳ cũ") — sai lãi gộp im lặng. | Chốt một chỗ lưu snapshot (ví dụ thêm `CostUnitAmount numeric(18,2)?` + `CostMissing bool` vào `RptFactSalesLines`) rồi cập nhật §5.1/§5.2.2/§6.3; hoặc bỏ câu "chụp vào fact" và mô tả rõ bảng kỳ + ngữ nghĩa bất biến. |
| 4 | P2 | §11 dòng 757; §0 dòng 50 | Ghi "`SAL-002` (snapshot thuế) … ⚠️ **chưa có code**", nhưng `SAL-002` **đã có code** (`CheckoutAppService.cs:39`, `TinhVaGiuChoAsync`; `CheckoutDtos.cs:8-23` có `TaxRate`), và header `sal-002-checkout-va-dat-hang.md` ghi "✅ ĐÃ CÓ CODE". Cái thiếu chỉ là **lưu** `TaxRate`/`TaxClassificationId` trên `OrderLine` (`Order.cs:808-840` chỉ có `TaxAmount`). | Sửa thành "SAL-002 đã có code; phần **snapshot thuế suất trên `OrderLine`** chưa có"; đồng bộ §0 dòng 50 và §14.4 dòng 905. |
| 5 | P2 | §8.1 BR-018 dòng 668; §1 Post-Condition dòng 106 | "Chứng từ huỷ **không bao giờ** xuất hiện trong `Rpt*`" chỉ được bảo vệ **lúc tổng hợp** (`TongHopBaoCaoService.cs:255-259`); job chạy mốc+1 → hôm nay (`KhoangTongHop.cs:62-72`, lần đầu lùi 30 ngày `:28,60`) nên đơn/hoá đơn huỷ **sau** khi ngày đó đã tổng hợp vẫn nằm trong doanh thu ngày đó cho tới khi backfill tay. Không có test/cơ chế cho ca này (TC-30 chỉ phủ bộ lọc nguồn). | Thu hẹp phát biểu thành "tại thời điểm tổng hợp" và ghi rõ ca huỷ muộn phải backfill ngày đó (`POST /aggregate`) hoặc bổ sung cửa sổ làm lại N ngày / bút toán điều chỉnh kỳ sau; thêm TC cho ca "huỷ sau khi ngày đã tổng hợp". |
| 6 | P3 | §5.2.1 dòng 420 | Khai **INDEX `(TenantId, BusinessDate)`** trên `RptAggDailySales` — không tồn tại; code chỉ có UNIQUE `(TenantId, BusinessDate, Nguon)` (`ReportingDbContextModelCreatingExtensions.cs:189-192`). | Xoá dòng INDEX, hoặc ghi "tiền tố `(TenantId, BusinessDate)` của UNIQUE phục vụ truy vấn khoảng". |
| 7 | P3 | §8.2 dòng 693-694 | Dẫn `eCommerceHttpApiHostModule.cs:115-128` cho `AbpDistributedLockOptions.KeyPrefix`, nhưng `KeyPrefix` nằm ở `:222` (cache) và `:237` (lock, khối `:225-238`). Mở đúng dòng được dẫn thấy code khác. | Sửa số dòng thành `:222,225-238`. |
| 8 | P3 | §14.1 TC-35 dòng 866 | TC-35 (nhịp job đọc từ Setting, `D-172`) ghi **Phủ `NFR-007`** — NFR-007 là "Quan sát được", không phải nhịp job. | Trỏ về `NFR-003` (hiệu năng job) + `D-172`; giữ NFR-007 cho log/metric. |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` nào → không có lệnh để chạy. Không có PostgreSQL trong phạm vi phiên review.
- Không chạy lại `dotnet test`: nhãn "test `Wp08BaoCaoTests` xanh" (§14.3 dòng 887) là **số đo của repo**, không đo lại trong phiên này; 5 tên test được grep thấy thật nhưng trạng thái xanh chưa xác nhận ở đây.

## Vòng trước (chỉ vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | **còn một phần** — thân (header/§0/§5.2.4/§11/§14.4/Đối chiếu) đã sửa đúng; **§4.3 dòng 303 chưa sửa** ⇒ chép lên bảng Phát hiện vòng 2 #1 | P1 |
| 2 | đã đóng — §3.1 dòng 192-193 và §12 dòng 772 khớp code (`:271-287`, `:303-317`; `0009` chỉ ở hàm thuần `:119-125`) | P2 |
| 3 | đã đóng — §0.1 D8 dòng 77 dẫn `shared/eCommerce.Kernel/Money/Money.cs:23` (đồ thị xác nhận) | P2 |
| 4 | đã đóng — §6.3 dòng 577 dùng `Order.ShippingFeeActual`; `Order.cs:127` xác nhận ( `ShippingCostAmount` không tồn tại) | P2 |
| 5 | đã đóng — BR-REP-002-018 thêm ở dòng 668; TC-30 dòng 861 phủ `BR-018` | P2 |
| 6 | đã đóng — TC-01 dòng 832 phủ `BR-009`, `BR-003` (không còn `BR-005`) | P2 |
| 7 | đã đóng — BR-005 dòng 655 ghi rõ vế `==` chỉ là **quy ước review**, chưa có test tự động | P2 |
| 8 | **còn** — §5.2.1 dòng 420 vẫn khai INDEX không tồn tại ⇒ chép thành Phát hiện vòng 2 #6 | P3 |
| 9 | **còn** — §8.2 dòng 693 vẫn dẫn `:115-128` ⇒ chép thành Phát hiện vòng 2 #7 | P3 |
| 10 | **còn** — TC-35 dòng 866 vẫn phủ `NFR-007` ⇒ chép thành Phát hiện vòng 2 #8 | P3 |
