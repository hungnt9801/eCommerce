# Doc review — REP-001 Read-model và job tổng hợp

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/reporting/rep-001-read-model-job-tong-hop.md` |
| Ngày · vòng | 24/11/2026 · **vòng 3 — kiểm nhanh** (sau khi BA sửa 8 P2 của vòng 2) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1032 (toàn văn); trọng tâm 9 mục đã đổi: §0, §1, §3.1, §5.2.1, §6.2, §8.1, §14.1, §14.2, §15.2 + đối chiếu chéo §3.2/§5.1/§8.2/§10/§11/§12/§13/§15.1 |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (DAT; 16/16 heading, 17/17 BR có test, mục đối chiếu Nhanh dòng 1007); `npm --prefix tools run check:mermaid` → 211 khối, **0 lỗi** |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep "sự kiện tăng dần / hai đường / ON CONFLICT / upsert / report:job / 30 s / 2 s / 23 test / NhipMacDinhPhut / numeric(18,4) / TenantId NOT NULL / reports/danh-thu" toàn tài liệu; đối chiếu §0 ↔ §1 ↔ §3.1 ↔ §6.2 ↔ §8.1 ↔ §8.2 ↔ §14.1 ↔ §14.2 ↔ §15.2 | 2 phát hiện mới (#8, #9 P3) + 7 P3 còn từ vòng 2 |
| D2 chéo tài liệu / code / D-xx | `codegraph_explore` + `read` + `grep` (xem "Truy vấn đồ thị"); đối chiếu route, DTO, kiểu tiền, TenantId, khoá phân tán, `D-47/D-48/D-17/D-27/D-31/D-170…D-174` | 0 P1/P2; các P2 vòng 2 đã đóng |
| D3 cơ chế thực thi | soi TC-02 (allow-list 1 method), TC-16, TC-20, TC-05b; đối chiếu BR-002/BR-005/BR-009 | 1 P3 (#7 TC-13 chưa đổi — còn từ vòng 2) |
| D4 SQL chạy được | Grep mở đầu khối ` ```sql/powershell/pwsh/bash/sh/psql/shell/console ` → **không có** khối lệnh nào (chỉ mermaid/csharp/json) ⇒ không có gì để chạy `BEGIN…ROLLBACK` | 0 phát hiện |
| D5 test/probe/tệp có thật | `grep`/`read` `test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj` (`Testcontainers.PostgreSql 4.16.0` dòng 19), `eCommerce.slnx:101`, `PostgreSqlWp08BaoCaoTests.cs` (chèn `TenantId = NULL` dòng 35-47), migration `20261104033300_Wp08_BaoCao_Reporting.cs` | 0 phát hiện (P2 #4 đã đóng) |
| D6 nhãn tin cậy | kiểm các nhãn "as-built" ở 9 mục đã đổi có `file:dòng` và khớp code: route (`ReportsController.cs:35,62`), `DinhNghiaTien` (`ReportingDtos.cs:67`), `HasPrecision(18,2)` (`ReportingDbContextModelCreatingExtensions.cs:103-107`), `report:tong-hop` (`TongHopBaoCaoService.cs:44`) | 0 phát hiện (các nhãn ✅ đều có bằng chứng) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14; mục "Đối chiếu mô hình Nhanh" dòng 1007 có thật, khớp D-07/D-17/D-18/D-27/D-31/D-47/D-48/D-170…174; `check:mermaid` 211/0 | 2 phát hiện P3 còn (#2, #4) |

**Truy vấn đồ thị (bắt buộc khi kết luận về code — docrev §1/§9):**

- `graph: codegraph_explore "IReportAppService ReportAppService GetDoanhThuAsync GetTongQuanDonHangAsync ChayTongHopAsync ReportsController revenue aggregate"` → `ReportsController` có `[HttpGet("revenue")]` (`ReportsController.cs:35`) và `[HttpPost("aggregate")]` (`:62`); `GetTongQuanDonHangAsync` đọc thẳng `_donHang.GetQueryableAsync()` + `_dongDonHang` (`ReportAppService.cs:125,134`) ⇒ **xác nhận** route `revenue` (P2 #6) và ngoại lệ BR-002 (P2 #5) đúng as-built.
- `graph: codegraph_explore "ReportingDbContextModelCreatingExtensions FactSalesLine HasPrecision ... TongHopBaoCaoService TenKhoaPhanTan ChayAsync"` → `TenKhoaPhanTan = "report:tong-hop"` (`TongHopBaoCaoService.cs:44`); `HasPrecision(18, 2)` cho `UnitPrice/LineDiscount/LineAmount/VatPercent/VatAmount` (`ReportingDbContextModelCreatingExtensions.cs:103-107`), `Quantity` = `18,3` (`:102`); unique `(TenantId,Nguon,SourceLineId)` (`:110-112`); entity `public Guid? TenantId` (`FactSalesLine.cs:23`) ⇒ **xác nhận** P2 #2 (18,2), P2 #3 (nullable), P2 #7 (`report:tong-hop`).
- `graph: codegraph_explore "… ReportingDtos BaoCaoDoanhThuDto DinhNghiaTien"` → `BaoCaoDoanhThuDto.DinhNghiaTien` (`ReportingDtos.cs:67`) ⇒ **xác nhận** P2 #8 (`dinhNghiaTien`, không phải `dinhNghia`).

Phần còn lại (migration NND, Testcontainers, slnx) xác nhận bằng `grep`/`read` sau truy vấn đồ thị: migration có 8 `CREATE UNIQUE INDEX … NULLS NOT DISTINCT` (`20261104033300_Wp08_BaoCao_Reporting.cs:218-248`), `TenantId` nullable (`:146`), `VatPercent numeric(18,2)` (`:161`); `eCommerce.IntegrationTests.csproj:19` = `Testcontainers.PostgreSql 4.16.0`; `eCommerce.slnx:101` có project ⇒ **xác nhận** P2 #3 (NND) và P2 #4 (hạ tầng Integration đã có).

## Phát hiện

> Vòng nhanh: **0 P1, 0 P2**. Toàn bộ 8 P2 của vòng 2 đã đóng (xem "Vòng trước"). Còn lại là P3 (không đổi verdict).

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §14.1 dòng 881; §14.3 dòng 929 | Ghi "**23** test case" nhưng bảng §14.1 có **25** ID (thêm `TC-03b`, `TC-05b`). | Sửa 23 → 25 (cả hai chỗ). |
| 2 | P3 | §13 dòng 822–823 | Metric đặt tên `reporting_rows_upserted_total` / `reporting_upsert_conflict_total` trong khi mô hình là **thay thế** (`DELETE` + `INSERT`, không upsert — D-47). | Đổi sang `reporting_rows_written_total` / `reporting_replace_conflict_total` (hoặc tương đương). |
| 3 | P3 | §12 dòng 784 | "Đơn bị xoá mềm ở module nguồn ⇒ read-model **vẫn còn** số liệu" chỉ đúng giữa hai lượt; lượt tổng hợp kế tiếp của ngày đó sẽ bỏ dòng. | Ghi rõ "còn tới lượt tổng hợp kế tiếp của ngày đó". |
| 4 | P3 | §5.1 ERD dòng 316–393; chú thích dòng 397 | ERD vẽ **6** bảng nhưng mô hình có **8** (`RptDimStores`, `RptDimStaffs` thiếu), trong khi chú thích dòng 397 nói "**4** bảng `RptDim*`". | Thêm 2 bảng chiều vào ERD (hoặc ghi chú lược bớt cho khớp "4 bảng RptDim*"). |
| 5 | P3 | §14.4 dòng 953; §15.1 Q4 dòng 967 | Ghi "Code hiện `NhipMacDinhPhut = 15` (`ReportingOptions.cs:17-18`)" — tại `ReportingOptions.cs:18` property là `NhipJobPhut`; `NhipMacDinhPhut` là const ở `ReportingAggregationWorker.cs:31`. | Ghi đúng: `ReportingOptions.NhipJobPhut` (mặc định 15) và `ReportingAggregationWorker.NhipMacDinhPhut = 15`. |
| 6 | P3 | §10 dòng 735 | Trạng thái rỗng hướng dẫn "chạy `Reconciliation` hoặc `Rebuild`" — cả hai là ⬜ kế hoạch; as-built chỉ có `POST /api/v1/reports/aggregate`. | Đổi thành "chạy tổng hợp (`POST /api/v1/reports/aggregate`)". |
| 7 | P3 | §14.1 TC-13 dòng 869 | Test "quét route của `ReportAppService`" là **no-op**: app service không mang `[Route]`, route nằm ở `ReportsController` ⇒ không thể đỏ khi có `POST/PUT/DELETE` trên fact. | Đổi input sang quét `ReportsController` (khuôn `Wp08BaoCaoTests.Controller_bao_cao_PHAI_co_du_5_route…`). |
| 8 | P3 | §14.1 TC-18 dòng 874 vs §14.2 dòng 924 | TC-18 đánh dấu loại **Unit** ở §14.1, nhưng §14.2 liệt kê TC-18 trong nhóm "test đánh dấu **Integration**". Cùng một ca, hai loại. | Chọn một: nếu kiểm unique index ở DB ⇒ Integration (bỏ khỏi danh sách Unit ở §14.1); nếu kiểm guard ở app ⇒ Unit (bỏ TC-18 khỏi câu Integration ở §14.2). |
| 9 | P3 | §3.1 dòng 191 vs §8.2 dòng 674 / §4.1 dòng 254 | Transaction/lock: §3.1 (`RepRunLog`, kế hoạch) ghi timeout khoá `report:tong-hop` **30 s**, còn §4.1/§8.2 (as-built) ghi `TryAcquire` **2 s** trên **cùng** khoá. Chưa ghi rõ đây là hai mức chờ khác nhau của hai loại job. | Thêm chú thích: RepRunLog dự kiến chờ 30 s (khác 2 s của worker as-built) — hoặc đồng bộ về 2 s cho một nguồn sự thật. |
| 10 | P3 | §6.1 dòng 513; §9 dòng 698–700 | Permission **kế hoạch** dùng tiền tố `eCommerce.Reporting.Rebuild/PeriodClose/PeriodReopen`, lệch tiền tố as-built `eCommerce.Reports` (+ đã có `eCommerce.Reports.Aggregate`). Bảng đối chiếu (dòng 1024) lại nói §6/§9 đã dùng `eCommerce.Reports`. | Đặt permission kế hoạch dưới nhóm `eCommerce.Reports.*` (vd `eCommerce.Reports.Rebuild`) cho nhất quán, hoặc ghi rõ đây là tên dự kiến khác nhóm. |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` nào (chỉ mermaid + csharp + json) ⇒ không có lệnh để chạy trong `BEGIN…ROLLBACK`.
- **Test Integration/NFR:** **không** chạy `dotnet test test/eCommerce.IntegrationTests` trong vòng nhanh này (cần Docker/Testcontainers) ⇒ các con số NFR-001/002/006/009 và "`PostgreSqlSchemaTests` 96/96" **chưa được tôi đo lại**. Sự **tồn tại + cấu hình** hạ tầng đã xác nhận bằng `.csproj` + `.slnx` + file test (xem D5).
- **Mermaid:** đã chạy `npm --prefix tools run check:mermaid` → 211 khối, 0 lỗi.
- **`check-feature-doc.ps1 -WP14`:** exit 0 (DAT).

## Vòng trước (vòng 2 — 15 phát hiện)

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — §0 dòng 20 nay "batch thay thế theo `(ngày, nguồn)` — không tiêu thụ ETO; D-47" | P2 |
| 2 | đã đóng — §5.2.1 + BR-004 nay `numeric(18,2)`; khớp `HasPrecision(18,2)` (`ReportingDbContextModelCreatingExtensions.cs:103-107`) + migration `:161` | P2 |
| 3 | đã đóng — TC-20 dòng 876 nay "`TenantId` **nullable** (`Guid?`) + unique index `NULLS NOT DISTINCT`"; khớp entity `FactSalesLine.cs:23` + migration `:146,:218-219` | P2 |
| 4 | đã đóng — §14.2 dòng 922-925 + §15.2 F6 ghi hạ tầng Integration **đã có** (`Testcontainers.PostgreSql 4.16.0`, `eCommerce.slnx:101`) | P2 |
| 5 | đã đóng — BR-002 dòng 642 nêu **ngoại lệ có kiểm** `GetTongQuanDonHangAsync` + TC-02 dòng 856 allow-list đúng 1 method; khớp `ReportAppService.cs:125,134` | P2 |
| 6 | đã đóng — §1 bước 7 + §6.2 nay `/api/v1/reports/revenue`; khớp `ReportsController.cs:35` | P2 |
| 7 | đã đóng — §3.1 dòng 191 nay khoá `report:tong-hop`; khớp `TongHopBaoCaoService.cs:44` | P2 |
| 8 | đã đóng — §6.2 dòng 550 nay khoá JSON `dinhNghiaTien`; khớp `ReportingDtos.cs:67` | P2 |
| 9 | còn (đã chép #3) | P3 |
| 10 | còn (đã chép #4) | P3 |
| 11 | còn (đã chép #1) | P3 |
| 12 | còn (đã chép #2) | P3 |
| 13 | còn (đã chép #5) | P3 |
| 14 | còn (đã chép #6) | P3 |
| 15 | còn (đã chép #7) | P3 |
