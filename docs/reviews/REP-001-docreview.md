# Doc review — REP-001 Read-model và job tổng hợp

**VERDICT: DOC-CHANGES**

| | |
|---|---|
| Tài liệu | `docs/features/reporting/rep-001-read-model-job-tong-hop.md` |
| Ngày · vòng | 24/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1032 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (DAT; 16/16 heading, 17/17 BR có test, mục đối chiếu Nhanh dòng 1008); `npm --prefix tools run check:mermaid` → 211 khối, **0 lỗi** |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep "cấm/không được/chỉ/thay thế/append/sự kiện" toàn tài liệu, đối chiếu §0 ↔ §0.1 ↔ §1 ↔ §6.1 ↔ §8.1 ↔ §12 ↔ §14.1 | 4 phát hiện (#1, #3, #5, #7) |
| D2 chéo tài liệu / code / D-xx | `codegraph_explore` + `Read`: `IReportAppService`, `ReportAppService`, `TongHopBaoCaoService`, `TongHopDoanhThu`, `ReportingErrorCodes`, `ReportingDtos`, entity `Rpt*`, migration `Wp08_BaoCao_Reporting`, `eCommerceDbContext`, `eCommerceMetrics`, `ReportingOptions`; đối chiếu `D-07/D-17/D-27/D-31/D-47/D-48/D-170…D-174` | 4 phát hiện (#2, #3, #4, #8) |
| D3 cơ chế thực thi | 15 BR "phải/không được" → cái gì đỏ; soi BR-002/TC-02, BR-004/TC-14, TC-20, TC-13 | 2 phát hiện (#5, #15) |
| D4 SQL chạy được | Grep mở đầu khối `sql` / `bash` / `powershell` / `psql` / `shell` → **không có** khối sql/lệnh; chỉ mermaid + csharp + json ⇒ không có gì để chạy | 0 phát hiện |
| D5 test/probe/tệp/endpoint có thật | `grep` + `Read` `test/eCommerce.Tests/Reporting/Wp08BaoCaoTests.cs`, `test/eCommerce.IntegrationTests/*`, `ReportsController.cs`, `ReportingDtos.cs`, `ReportingConsts.cs`; kiểm route và tên khoá JSON | 4 phát hiện (#4, #6, #8, #15) |
| D6 nhãn tin cậy | Kiểm ✅ có số/đường dẫn: 8 `DbSet` (`eCommerceDbContext.cs:260-274`), 16 metric (`eCommerceMetrics.cs:81-107`), `MetricsTests.cs:39-73`; kiểm nhãn "as-built"/"chưa chạy được" | 2 phát hiện (#2, #4) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14; mục "Đối chiếu mô hình Nhanh" dòng 1008 có thật; `check:mermaid` 211/0 | 2 phát hiện (#1, #7) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §0 Tóm tắt, dòng 20 | Tóm tắt vẫn "chốt" cơ chế là **sự kiện tăng dần + job đối chiếu định kỳ — hai đường độc lập**, trái `D-47` và trái §0.1 dòng 71, §1 Trigger dòng 105, §7.1 dòng 587 ("Không có bảng ETO tiêu thụ"). Bằng chứng: dòng 20 `cơ chế cập nhật (**sự kiện tăng dần** + **job đối chiếu định kỳ** …)`. | Sửa Tóm tắt theo as-built: "job tổng hợp theo mốc, thay thế theo `(ngày, nguồn)`"; nếu muốn nhắc lịch sử thì ghi rõ v0.1 **đã bị** `D-47` thay. |
| 2 | P2 | §5.2.1 dòng 417–421; BR-004 dòng 644 | Data dictionary ghi 5 cột tiền `numeric(18,4)` và BR-004 khẳng định "Tiền `numeric(18,4)`", nhưng schema as-built là `numeric(18,2)`. Người làm `REP-002`/`ACC-001` hoặc migration mới theo tài liệu sẽ lệch kiểu cột. Bằng chứng: `ReportingDbContextModelCreatingExtensions.cs:103-107` = `HasPrecision(18, 2)`; migration `20261104033300_Wp08_BaoCao_Reporting.cs:158-162` = `numeric(18,2)`. | Sửa §5.2.1 + BR-004 thành `numeric(18,2)` (khớp code) **hoặc**, nếu `XC-001` buộc 18,4, ghi rõ đây là **nợ code** + migration sửa — không để nhãn "as-built" sai. |
| 3 | P2 | §14.1 TC-20, dòng 876 (đối chiếu §5.2.1 dòng 407) | TC-20 đòi `TenantId` **không nullable** + CHECK `TenantId NOT NULL`, trong khi chính §5.2.1 ghi `✔ (DB)`, entity là `Guid?` và cột migration nullable. Làm theo TC-20 (thêm `NOT NULL`) sẽ hỏng dòng tenant host — đúng loại dòng mà integration test hiện chèn. Bằng chứng: `FactSalesLine.cs:23` `Guid? TenantId`; migration `:146` `nullable: true`; `PostgreSqlWp08BaoCaoTests.cs:48` chèn `DBNull`. | Bỏ vế `TenantId NOT NULL`; sửa TC-20 thành "8/8 `IMultiTenant`, `TenantId` nullable, unique index `NULLS NOT DISTINCT`". |
| 4 | P2 | §14.2 dòng 922–926; §15.2 F6 dòng 983; §14.3 dòng 931 | Tài liệu nói chưa có `test/eCommerce.IntegrationTests`/Testcontainers và 10 test Integration "chưa chạy được", nhưng project **đã tồn tại**, đã vào solution, có `Testcontainers.PostgreSql` 4.16.0 và `PostgreSqlWp08BaoCaoTests` cho chính read-model. Chính §15.1 Q2 (dòng 966) lại dẫn `PostgreSqlSchemaTests 96/96` (test nằm trong project đó) ⇒ tự mâu thuẫn; §14.3 dòng 931 lại tick ✅ migration "chạy được từ DB trống và DB đã có dữ liệu" trong khi §14.2 nói không chạy được. Bằng chứng: `eCommerce.slnx:101`; `test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj:19`; `PostgreSqlWp08BaoCaoTests.cs:92-157`. | Cập nhật §14.2/F6: hạ tầng Integration **đã có**; ghi rõ test nào đã chạy/đỏ; bỏ câu "việc đó là GĐ1". |
| 5 | P2 | §8.1 BR-002 dòng 642, đối chiếu §6.1 dòng 509 và TC-02 dòng 856 | BR-002 **cấm** báo cáo/API đọc trực tiếp bảng giao dịch, nhưng `GetTongQuanDonHangAsync` (as-built, đúng như §6.1 mô tả) đọc `OrdOrders`/`OrdOrderLines`; TC-02 chỉ quét `IRepository<…>` "ngoài `Reporting`" nên không bắt được chỗ này ⇒ bất biến không có cơ chế đỏ. Bằng chứng: `ReportAppService.cs:32,125-135` (`IRepository<Order>`, `_donHang.GetQueryableAsync()`). | Hoặc ghi rõ `GetTongQuanDonHangAsync` là **ngoại lệ/nợ GĐ3** (sửa BR-002 + TC-02 theo), hoặc đổi API sang đọc read-model; không để BR "cấm" trong khi thân tài liệu mô tả đúng việc bị cấm. |
| 6 | P2 | §1 Basic Flow bước 7, dòng 108 | Endpoint `GET /api/v1/reports/danh-thu` **không tồn tại**; route thật là `GET /api/v1/reports/revenue`. Bằng chứng: `ReportsController.cs:35` `[HttpGet("revenue")]`; `Wp08BaoCaoTests.cs:450` chốt danh sách route có `GET revenue`. | Đổi thành `/api/v1/reports/revenue`. |
| 7 | P2 | §3.1 dòng 191, đối chiếu §8.2 dòng 674–676 | Khoá ở state machine là `report:job:{jobName}:{tenantId}` — không có trong bảng khoá §8.2 và khác as-built `report:tong-hop`; người làm `RepRunLog` sẽ tạo khoá song song, không loại trừ với lượt job hiện tại. Bằng chứng: `TongHopBaoCaoService.cs:44` `TenKhoaPhanTan = "report:tong-hop"`; §8.2 chỉ liệt kê `report:tong-hop`, `report:rebuild:…`, `report:period-close:…`. | Dùng đúng tên trong bảng §8.2, hoặc thêm `report:job:{jobName}:{tenantId}` vào bảng và ghi rõ quan hệ với `report:tong-hop`. |
| 8 | P2 | §6.2 response mẫu, dòng 550 | Khoá JSON `dinhNghia` không có trong DTO; tên thật là `DinhNghiaTien` (JSON `dinhNghiaTien`). Client làm theo mẫu sẽ đọc `undefined` thay vì câu định nghĩa tiền. Bằng chứng: `ReportingDtos.cs:67` `public string DinhNghiaTien`. | Sửa khoá thành `dinhNghiaTien`; nếu muốn mẫu đầy đủ thì bổ sung `tongDoanhThu`/`tongTienVat`/`tongSoChungTu`. |
| 9 | P3 | §12 dòng 784 | "Đơn bị xoá mềm ở module nguồn ⇒ read-model vẫn còn số liệu" chỉ đúng giữa hai lượt: lượt kế tiếp `DELETE` + `INSERT` từ nguồn (đã lọc) sẽ bỏ dòng đó. | Ghi rõ "còn tới lượt tổng hợp kế tiếp của ngày đó". |
| 10 | P3 | §5.1 ERD, dòng 316–393 | ERD vẽ 6 bảng nhưng tài liệu/migration có 8 (`RptDimStores`, `RptDimStaffs` thiếu trong sơ đồ). | Thêm 2 bảng chiều vào ERD (hoặc ghi chú lược bớt). |
| 11 | P3 | §14.1 dòng 881; §14.3 dòng 930 | Ghi "Tổng: **23** test case" nhưng bảng có **25** ID (thêm `TC-03b`, `TC-05b`). | Sửa 23 → 25 (cả hai chỗ). |
| 12 | P3 | §13 bảng metric, dòng 822–823 | Đặt tên `reporting_rows_upserted_total` / `reporting_upsert_conflict_total` trong khi mô hình **không upsert** (`D-47` dùng DELETE + INSERT). | Đổi sang `reporting_rows_written_total` / `reporting_replace_conflict_total` (hoặc tương đương). |
| 13 | P3 | §14.4 dòng 954; §15.1 Q4 dòng 968 | Ghi "Code hiện `NhipMacDinhPhut = 15` (`ReportingOptions.cs:17-18`, `ReportingAggregationWorker.cs:31`)" — tại `ReportingOptions.cs:18` tên là `NhipJobPhut`; `NhipMacDinhPhut` là const ở `ReportingAggregationWorker.cs:31`. | Ghi đúng: `ReportingOptions.NhipJobPhut` (mặc định 15) và `ReportingAggregationWorker.NhipMacDinhPhut = 15`. |
| 14 | P3 | §10 dòng 735 | Trạng thái rỗng hướng dẫn người dùng "chạy `Reconciliation` hoặc `Rebuild`" — cả hai là ⬜ kế hoạch, as-built chỉ có `POST /api/v1/reports/aggregate`; UI hiện dùng nút chạy gọi aggregate (`apps/admin-web/app/[business]/report/revenue/page.tsx:27`). | Đổi hướng dẫn thành "chạy tổng hợp (`POST /api/v1/reports/aggregate`)". |
| 15 | P3 | §14.1 TC-13 dòng 869 | Test "quét route của `ReportAppService`" là **no-op**: app service không mang `[Route]`, route nằm ở `ReportsController` ⇒ không thể đỏ khi có `POST/PUT/DELETE` trên fact. Bằng chứng: route ở `ReportsController.cs:20,35,40,48,53,62`. | Đổi input sang quét `ReportsController` (khuôn `Wp08BaoCaoTests.Controller_bao_cao_PHAI_co_du_5_route…`). |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` nào (chỉ mermaid + csharp + json) ⇒ không có lệnh để chạy trong `BEGIN…ROLLBACK`.
- **Test Integration/NFR:** không chạy `dotnet test test/eCommerce.IntegrationTests` trong phiên này (cần Docker/Testcontainers) ⇒ các con số NFR-001/002/006/009 và "`PostgreSqlSchemaTests` 96/96" **chưa được tôi đo lại**. Sự **tồn tại** của hạ tầng thì đã xác nhận bằng tệp + `.csproj` + `eCommerce.slnx` (xem #4).
- **Mermaid:** đã chạy `npm --prefix tools run check:mermaid` → 211 khối, 0 lỗi (đạt D7 #14).
- **`check-feature-doc.ps1 -WP14`:** exit 0.

## Vòng trước (chỉ vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — đã xác nhận `IReportAppService.cs:16-28` và `ReportAppService.cs:254-260` đúng `ChayTongHopAsync(ChayTongHopInput)`; khối code §6.1 khớp | P1 |
| 2 | đã đóng — §2 chỉ còn batch (`UC2` "chạy job tổng hợp theo cửa sổ ngày"), không còn UC "nhận sự kiện" | P2 |
| 3 | đã đóng — §1 E1 dùng `eCommerce:Lock:0001` (`eCommerceConsts.cs:545`, `TongHopBaoCaoService.cs:116`) | P2 |
| 4 | đã đóng — TC-03/NFR-005 đã đổi sang "batch/lượt tổng hợp" | P2 |
| 5 | đã đóng — TC-16 đã đổi sang "chỉ xoá trong lượt thay thế theo `(ngày, nguồn)`" | P2 |
| 6 | đã đóng phần đếm (8/8); vế `TenantId NOT NULL` vẫn sai — đã chép thành phát hiện mới #3 | P2 |
| 7 | đã đóng — mẫu quét TC-02 đã thêm `RetailBill/InvItems/InvStockLedger` + `IRepository<…>` | P2 |
| 8 | đã đóng — §9 và TC-13 đã dùng `ReportAppService` | P2 |
| 9 | còn một phần — đã bỏ tiền tố `ecommerce:` nhưng tên `report:job:{jobName}:{tenantId}` vẫn không khớp bảng §8.2 (đã chép lên #7) | P2 |
| 10 | còn (đã chép lên #9) | P3 |
| 11 | còn (đã chép lên #10) | P3 |
| 12 | còn (đã chép lên #11) | P3 |
| 13 | còn (đã chép lên #12) | P3 |
