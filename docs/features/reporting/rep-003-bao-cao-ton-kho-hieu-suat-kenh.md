# `REP-003` — Báo cáo tồn kho luân chuyển & hiệu suất kênh

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `REP-003` |
| **Module** | **Reporting** (`modules/Reporting/`) — 🔶 **có một phần trong code** (`WP-08`): `RptFactSalesLines`/`RptFactInventorySnapshot`/`RptWatermark` + `TongHopBaoCaoService` + `ReportingAggregationWorker`; **chưa có** `RptInventoryTurnover`/`RptChannelDailyPerformance`/`RptAggregationRuns`/`RptReconciliations` (D-176, D-183; xem §15.2 F1) |
| **Giai đoạn** | **GĐ3 — Điều hành** (`docs/implementation-plan.md:114`, `:436–438`) |
| **Ưu tiên** | **Should have** (nhưng là **điều kiện để vận hành có số** — không có nó thì mọi quyết định nhập hàng/đấu sàn là phỏng đoán) |
| **Trạng thái** | 🟡 `Draft` — chờ review |
| **Người viết / người review** | *(điền)* / **Kiến trúc trưởng + Kế toán trưởng + Vận hành** |
| **Phiên bản / ngày** | `v0.1 — 14/10/2026` |
| **Tài liệu liên quan** | `REP-001` (read-model & job tổng hợp — **tài liệu nền của nhóm này**) · `REP-002` (doanh thu & lãi gộp) · `ACC-001` (xuất liệu kế toán) · `XC-001` (Money/làm tròn) · `XC-003` (khoá phân tán) · `XC-005` (log/metric) · `XC-006` (thuế GTGT) · `INV-001`/`INV-003` · `SAL-003`/`SAL-005` · `REC-002`/`REC-003` · `SHP-002`/`SHP-003`/`SHP-004` · `CHN-004` |

---

## 0. Tóm tắt & quyết định thiết kế

**Câu hỏi nghiệp vụ mà feature này trả lời:**

1. **Tồn kho luân chuyển:** một SKU/kho **quay vòng bao nhiêu lần** trong kỳ, **bao nhiêu ngày tồn kho**, hàng nào **chậm/đã chết**, vốn đang **nằm chết** ở đâu?
2. **Hiệu suất kênh:** mỗi kênh (`web`, `shopee`, `lazada`…) đóng góp **bao nhiêu doanh thu ròng**, **bao nhiêu đơn**, **AOV**, **tỉ lệ huỷ/hoàn**, **phí sàn**, và **lãi gộp** sau khi trừ các loại phí?

**Tóm tắt thiết kế:** `REP-003` **không** tính số liệu lúc người dùng mở báo cáo. Nó dựng **read-model riêng** (bảng tiền tố `Rpt`) do **job tổng hợp** ghi, và **API báo cáo chỉ đọc read-model**. Đây chính là yêu cầu bắt buộc của GĐ3:

> `docs/implementation-plan.md:213` — *"**Không** báo cáo nào query trực tiếp trên bảng giao dịch; có test số liệu khớp với dữ liệu gốc"*

**Vì sao phải có tài liệu này — ba rủi ro thật đã nhìn thấy trong code hiện tại:**

| # | Rủi ro | Bằng chứng (đọc code) |
|---|---|---|
| 1 | **Read-model cho 2 báo cáo này chưa có** ⇒ nếu viết báo cáo theo cách thông thường, mọi truy vấn sẽ đổ lên `InvStockLedger` / `OrdOrders` | `modules/Reporting/**` chỉ có fact doanh thu/tồn (`RptFactSalesLine`/`RptFactInventorySnapshot`) + `TongHopBaoCaoService`; **0** bảng vòng quay/kênh (D-176, D-183) |
| 2 | **Không thể tách doanh thu theo thuế suất** vì `OrderLine` **chưa có** `TaxRate`/`TaxClassificationId` (dù `XC-006` D3 yêu cầu snapshot) | `modules/Sales/eCommerce.Sales.Domain/Order.cs:808–840` — `OrderLine` chỉ có `TaxAmount` (`:838`), không có cột thuế suất/phân loại |
| 3 | **Không tính được lãi gộp** vì **chưa có đường ghi/động cơ giá vốn** (bảng `PrcCostPrices` **đã có**, chỉ **đọc**) | `modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:36,57–68` (`EffectiveFrom` `DateOnly`, `DaDuyet`, `CoHieuLucTai`); `modules/Pricing/eCommerce.Pricing.Application/CostPriceAppService.cs:24–27` **chỉ đọc** (D-177) |

**Phạm vi:**

- ✅ **Read-model** cho 2 nhóm báo cáo: luân chuyển tồn kho (snapshot ngày + vòng quay kỳ) và hiệu suất kênh (ngày × kênh × shop).
- ✅ **Job tổng hợp** (batch theo cửa sổ ngày) + **job đối soát** (read-model ↔ nguồn) + **sổ đăng ký lần chạy** (append-only, có state machine).
- ✅ **Hợp đồng port/adapter đảo chiều** để module Reporting **không** tham chiếu app (§0.1 D3).
- ✅ **Hai làn cập nhật**: batch hằng đêm (tồn kho) + làn **trong ngày** (hiệu suất kênh) bằng **đúng một job** `D-172` (5 phút) + **đối soát hằng đêm**; consumer ETO chỉ là tối ưu về sau (D-178).
- ✅ **Định nghĩa chỉ tiêu** (vòng quay, ngày tồn kho, doanh thu ròng, lãi gộp) — có công thức tường minh.
- ✅ **Metric/log/CorrelationId** theo `XC-005`; **khoá phân tán** theo `XC-003`; **tiền** theo `XC-001`.
- ✅ **Đường cấp SỐ cho `ACC-001`** qua hợp đồng **chỉ đọc** `IReportFactReader` khai trong `modules/Reporting/eCommerce.Reporting.Application.Contracts` (D-183). `ACC-001` tự sinh file CSV/XLSX + `SHA-256` + `AccExportRuns`/`AccExportArtifacts` append-only; `REP-003` **không** xuất artifact kế toán, **không** giữ hash.

**Không nằm trong phạm vi:**

- ❌ **Định giá tồn kho** — phương pháp đã chốt **bình quân gia quyền di động** (D-177), không FIFO. **Chủ = Pricing (`PRC-001`)** (**D-186**): sở hữu bảng `PrcCostPrices` + **đường ghi** + **động cơ bình quân gia quyền di động** + luồng duyệt (`CostPrice.Duyet`), **chỉ Pricing ghi** bảng này; động cơ đọc số lượng/đơn giá qua hợp đồng **chỉ đọc** ở `Inventory.Application.Contracts` (không tham chiếu `Inventory.Domain`/`InvStockLedger`, không ghi sổ cái). `INV-001` **chỉ cấp** đơn giá nhập để tra cứu (as-built `StockDocumentLine.UnitPrice`; ánh xạ *"`UnitCost` (tài liệu) ↔ `UnitPrice` (as-built)"*, **không** đổi tên — D-09). `ACC-001` chỉ **tiêu thụ** qua `IReportFactReader` (D-183) + **một** method hợp đồng mở rộng additive của `ICostPriceAppService` trả `(Amount, CurrencyCode | null)` theo `(Sku, ngày hiệu lực)`, **chỉ** dòng `DaDuyet = true`; không sở hữu bảng/động cơ, không ghi ngược. **Đường ghi + động cơ không thuộc `REP-003`**; thiếu giá vốn ⇒ `CogsAmount`/`TurnoverRatio`/`DaysOfInventory`/cả hai cột lãi gộp = **NULL** + cờ (BR-009).
- ❌ **Bảng doanh thu & lãi gộp chi tiết theo sản phẩm/khách** — thuộc `REP-002`.
- ❌ **Settlement sàn thật** (phí sàn thực tế, tiền sàn phải trả) — thuộc `REC-002`.
- ❌ **Đối soát COD** (import file Excel theo "phiên") — thuộc `REC-003`.
- ❌ **Hạ tầng read-model dùng chung** (khung job, bảng `Rpt*` nền, registry) — thuộc **`REP-001`**; tài liệu này **giả định `REP-001` đã Approved và đã có khung**.
- ❌ **Dashboard/BI bên ngoài** (Metabase/Grafana) — chỉ nêu yêu cầu tối thiểu ở §15.
- ❌ **Dashboard thời gian thực** (sub-second) — `REP-003` là báo cáo **T+1 / gần thời gian thực 15 phút**, không phải monitoring.

### 0.1 Quyết định thiết kế (ADR ngắn)

| # | Quyết định | Phương án đã cân nhắc | Lý do chọn | Hệ quả |
|---|---|---|---|---|
| **D1** | **Reporting là module riêng** `modules/Reporting/` (Domain + EntityFrameworkCore + Application + Contracts) | (a) nhét vào `src/eCommerce.Application`; (b) module riêng | Yêu cầu GĐ3 là **read-model riêng**; nhét vào app ⇒ báo cáo và nghiệp vụ chung một tầng, không cưỡng chế được ranh giới "không query bảng giao dịch". Khuôn module đã có sẵn: `modules/Tax/` + `modules/ChannelHub/` (`docs/structure-and-techstack.md:64–99`) | Thêm 3–4 project; chi phí này **cố ý** để ranh giới kiểm được bằng test |
| **D2** | **API báo cáo CHỈ đọc bảng `Rpt*`** | (a) đọc trực tiếp bảng giao dịch; (b) read-model | Truy vấn nặng trên DB giao dịch làm chậm bán hàng; yêu cầu GĐ3 nói rõ (`docs/implementation-plan.md:213`) | Có **độ trễ dữ liệu** (T+1 cho tồn kho, < 15 phút cho kênh) — phải nói rõ trên UI (§10) |
| **D3** | **Port/adapter ĐẢO CHIỀU**: module Reporting **định nghĩa** `IInventoryFactReader`/`IOrderFactReader`; **app (hoặc module sở hữu) cài đặt** | (a) module tham chiếu entity của app; (b) app cài đặt interface do module định nghĩa | 🔒 **Module gọi app BỊ CẤM** (bất biến dự án). Đây đúng khuôn đã dùng: `ChannelHub` **phát ETO**, app subscribe và tự tạo `Order` — module không tham chiếu app (`modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelShopSyncer.cs:13–19`) | Cần thêm 2 interface + 1 adapter ở tầng app; khi Inventory/Sales tách thành module (GĐ1) thì adapter **chuyển về module đó**, không đổi interface |
| **D4** | **Hai làn cập nhật**: (1) batch hằng đêm cho tồn kho; (2) làn **trong ngày** cho hiệu suất kênh bằng **ĐÚNG MỘT job** định kỳ (`D-172`: 5 phút, lookback 3 ngày, ABP Setting); (3) **đối soát hằng đêm** | (a) chỉ batch; (b) consumer ETO bắt buộc; (c) job định kỳ | Tồn kho cần **số dư cuối ngày** ⇒ batch là tự nhiên. Hiệu suất kênh cần thấy **trong ngày**; consumer ETO (`StockMovementRecordedEto`/`OrderCreatedEto`) **chưa có** ở GĐ3 ⇒ **không** là điều kiện chặn (D-178) | L2 dùng lại cửa đọc/khoá của job `D-172`; số trong ngày là **tạm tính**, L3 đêm mới chốt; cờ L2 TẮT ⇒ báo cáo ghi T+1 trung thực |
| **D5** | **Upsert theo KHOÁ NGHIỆP VỤ** thay vì "xoá rồi ghi" | (a) DELETE + INSERT theo kỳ; (b) upsert theo khoá duy nhất | `DELETE` rộng có thể xoá dữ liệu của lần chạy khác; upsert làm **chạy lại là idempotent** và **lỗi giữa chừng không mất dữ liệu cũ** | Cần unique index đúng khoá; `ChannelShopId` là **`uuid` NULLABLE** + unique index **`NULLS NOT DISTINCT`** — **bỏ** sentinel `Guid.Empty` (D-180) |
| **D6** | **Sổ đăng ký lần chạy `RptAggregationRuns` là APPEND-ONLY** + state machine **ném lỗi** | (a) sửa/xoá run row; (b) append-only + bản ghi thay thế | Cần trả lời *"con số này do lần chạy nào sinh ra, lúc nào, có qua đối soát không"* — đúng bài học `XC-006` D6 | Không xoá run; muốn chạy lại ⇒ tạo run mới + `Superseded` run cũ |
| **D7** | **Hàng rào cuối là DB**, không phải lock: **unique index có filter `WHERE Status = Running`** | (a) tin vào lock; (b) lock + partial unique index | Kế thừa `XC-003` D2: Redis chết ⇒ lock vô hiệu nhưng app vẫn chạy | Cần index filter (EF Core hỗ trợ `HasFilter`, đã dùng ở `eCommerceDbContext.cs:434`) |
| **D8** | **Tiền = `decimal` + `Money`**; so sánh bằng **minor units**, **cấm `==`** | (a) `double`; (b) so `decimal` bằng `==` | Kế thừa `XC-001` BR-003/BR-008 (bài học EShop vỡ với VND/payOS) | ⚠️ **`Money` CHƯA có trong code** (`grep` `Money|ToMinorUnits` = **0**) ⇒ **REP-003 phụ thuộc `XC-001` phải xong trước GĐ3** |
| **D9** | **Không được ghi `0` thay cho "không biết"** | (a) thiếu giá vốn ⇒ lãi gộp = 0; (b) `NULL` + cờ lý do | Lãi gộp = 0 nghĩa là "hoà vốn" — **sai nghiêm trọng** và không ai phát hiện | Read-model có cột cờ `CostMissing` / `SettlementPending`; UI **phải** hiện nhãn thay vì số 0 |
| **D10** | **Metric mới phải đăng ký ở HAI nơi** | (a) phát metric tự do; (b) registry `eCommerceMetricNames` + `XC-005` §5.1 | Đã có test chốt: `MetricsTests.TC06_khong_co_metric_phat_ra_ngoai_bang_dang_ky` (`test/eCommerce.Tests/Observability/MetricsTests.cs:49–59`) và test hậu tố tên (`:111–127`) ⇒ phát metric lạ là **CI đỏ** | 3 metric mới của `REP-003` **bắt buộc** sửa `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs` **và** `XC-005` §5.1 trong cùng PR |
| **D11** | **Retention phân tầng**: chi tiết ngày **mặc định KHÔNG xoá** (`DetailRetentionMonths = 0` = TẮT, giữ vô hạn tới khi kế toán chốt hạn lưu trữ), bảng tổng hợp tháng **giữ vô hạn** | (a) giữ tất cả chi tiết; (b) tổng hợp + xoá chi tiết ở 24 tháng | Chi tiết ngày tăng tuyến tính theo SKU × kho × ngày; nhưng **đối soát kế toán cần lịch sử dài** ⇒ giữ bản tổng hợp. `24` tháng chỉ là **đề xuất**, **không** phải kết luận pháp lý (D-182) | Job dọn fail-closed (chỉ xoá khi `> 0`, âm ⇒ NÉM) + **dry-run mặc định**; **cấm** chạm bảng tháng và tài liệu nguồn kế toán |
| **D12** | **Xuất file tách khỏi số liệu**: tính bằng **hàm thuần**, ghi định dạng bằng lớp riêng | (a) vừa tính vừa ghi; (b) tách | Khuôn đã có và đã chứng minh: `Mau01Part2Calculator` là **hàm thuần** *"dùng chung cho mọi định dạng xuất (bảng số liệu, HTKK, eTax)"* (`modules/Tax/eCommerce.Tax.Domain/Mau01/Mau01Part2Calculator.cs:50–57`) | Xuất Excel/CSV của `ACC-001` dùng lại đúng số liệu đã kiểm thử |

---

## 1. Use Case Specification

| Thuộc tính | Nội dung |
|---|---|
| **Use Case ID** | `REP-003` |
| **Use Case Name** | Xem tồn kho luân chuyển và hiệu suất kênh **trên read-model đã đối soát** |
| **Description** | *"Là quản lý vận hành, tôi muốn biết SKU nào quay vòng chậm và kênh nào thực sự mang lại tiền — với con số khớp dữ liệu gốc và nói rõ chỗ nào chưa biết (giá vốn, phí sàn thực tế) — mà không làm chậm hệ thống bán hàng."* |
| **Actor(s)** | **Quản lý vận hành/kho** (xem luân chuyển) · **Quản lý kênh/kinh doanh** (xem hiệu suất) · **Kế toán trưởng** (xuất liệu cho `ACC-001`) · **Job tổng hợp** (hệ thống) · **Vận hành kỹ thuật** (chạy lại kỳ, xem đối soát) |
| **Priority** | **Should have** — nhưng là **điều kiện để vận hành có số**; nằm trong GĐ3 |
| **Trigger** | (a) người dùng mở màn hình báo cáo; (b) đến nhịp job tổng hợp (đêm); (c) có sự kiện đơn hàng mới/đổi trạng thái (làn incremental); (d) vận hành bấm **chạy lại** một kỳ vì nghi ngờ số liệu |
| **Pre-Condition(s)** | • **`TenantId` BẮT BUỘC** (từ `ICurrentTenant`); `TenantId` rỗng ⇒ **ném lỗi tham số** (host không xem dữ liệu nghiệp vụ của tenant — §12).<br>• **`ChannelCode`** chỉ tham gia với **báo cáo hiệu suất kênh**; báo cáo luân chuyển tồn kho **không** phân theo kênh (tồn kho là tài nguyên dùng chung — xem `Channel.HoldsInventory` ở `modules/ChannelHub/eCommerce.ChannelHub.Domain/Entities/ChannelEntities.cs:21–22`).<br>• Khoá nghiệp vụ của kỳ **đã có ít nhất 1 lần chạy `Published`**; nếu chưa ⇒ **hiện trạng thái "chưa có dữ liệu kỳ này"** (không phải lỗi — BR-013).<br>• Read-model có dữ liệu nguồn tối thiểu: `InvStockLedger` / `OrdOrders` / `OrdOrderLines` **đã được ánh xạ** trên DbContext hợp nhất ⇒ `IRepository<T>` được đăng ký (khối `DbSet` trong `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs`).<br>• `XC-001` (`Money`) đã xong (D8). |
| **Post-Condition(s)** | • Mọi số liệu trả ra **đến từ bảng `Rpt*`** đã `Published`; **0** truy vấn bảng giao dịch trong đường đọc.<br>• Mỗi bộ số liệu **truy được** về một `RptAggregationRuns` (`RunId`) kèm thời điểm chạy + kết quả đối soát.<br>• Chạy lại cùng kỳ ⇒ read-model **giống hệt** (idempotent) và **không nhân bản** dòng.<br>• Chỗ chưa biết (giá vốn, phí sàn thực tế) hiển thị **`NULL` + nhãn lý do**, **không** hiển thị `0`. |
| **Basic Flow** | 1. Job (hoặc API) giành khoá `TongHopBaoCaoService.TenKhoaPhanTan` (`report:tong-hop`; ABP thêm tiền tố) và tạo bản ghi `RptAggregationRuns` với `Status = Pending`.<br>2. Chuyển `Running`; gọi `IOrderFactReader` / `IInventoryFactReader` để lấy **fact rows** theo **cửa sổ ngày bị chặn** + **batch theo con trỏ**.<br>3. Trong **Unit of Work**, upsert vào `RptInventoryDailySnapshots` / `RptChannelDailyPerformance` theo **khoá nghiệp vụ duy nhất** (BR-003).<br>4. Job **đối soát**: cộng lại read-model và so với tổng tính trực tiếp từ nguồn (tiền: **0 đồng**; số lượng: **0 đơn vị**).<br>5. Job **đối soát**: lệch tiền = **0 đồng** và lệch số lượng = **0 đơn vị** ⇒ `Computed → Validated → Published`; lệch khác `0` ⇒ `Failed` + `DriftAmount` (**không publish**). **Không có ngưỡng dung sai**: hai bên cộng cùng các dòng **đã làm tròn** (BR-007) nên đẳng thức kiểm được tuyệt đối.<br>6. Người dùng mở báo cáo ⇒ app service **chỉ đọc `Rpt*`**, lọc theo kỳ/kho/kênh, trả DTO; UI hiển thị **nhãn độ trễ** và **nhãn chỗ chưa biết**.<br>7. Kế toán xuất liệu ⇒ `ACC-001` đọc read-model qua hợp đồng `IReportFactReader` rồi tự sinh file + `SHA-256` (D-183); xuất CSV màn hình là **client-side** (D-36), `REP-003` **không** sinh artifact kế toán. |
| **Alternative Flow** | **A1: Job định kỳ gặp khoá đang bận** — chế độ `SkipIfBusy`: **bỏ qua nhịp này**, ghi log `Information` + metric `lock_skipped_total{module=report}`; **không** ném lỗi (khớp `XC-003` A1 và cách `ChannelTokenManager` đang làm: `modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelTokenManager.cs:74–83`).<br>**A2: Chạy lại kỳ đã `Published`** — **không** sửa run cũ; tạo **run mới** (`Version + 1`), ghi read-model đè theo khoá nghiệp vụ, run cũ chuyển `Superseded` sau khi run mới `Published`.<br>**A3: Thiếu giá vốn** — vẫn chạy và publish; `CogsAmount`/`TurnoverRatio`/`DaysOfInventory` + **cả hai** cột lãi gộp = `NULL`, cờ `CostMissing = true`; UI hiện *"chưa có giá vốn — xem PRC-001"* (BR-009, D-177, D-181).<br>**A4: Kỳ rỗng (không có đơn/biến động)** — ghi đúng **0 dòng** và trả **danh sách rỗng của kỳ**, kèm thông điệp hướng dẫn ở UI; **không** trả lỗi (BR-013).<br>**A5: Dữ liệu COD chưa đối soát** — báo cáo kênh hiện cột COD là **tạm tính** + cờ `CodUnreconciled = true` (⚠️ xem cảnh báo bảo mật/nguồn ở §7.2). |
| **Exception Flow** | **E1:** đã có run `Running` cho cùng `(tenant, scope, kỳ)` ⇒ **`eCommerce:Reporting:0001`** (`409`).<br>**E2:** lỗi ghi read-model giữa chừng ⇒ UoW **rollback**, run `Failed`, **`0002`** (`500`); dữ liệu cũ **còn nguyên** (nhờ upsert theo khoá).<br>**E3:** đối soát lệch khác `0` ⇒ **`0003`** (`422`), run `Failed` + `DriftAmount`, **không** publish.<br>**E4:** publish khi run chưa `Validated` ⇒ **`0004`** (`400`).<br>**E5:** run mới thay thế khi run cũ **chưa** `Published` ⇒ **`0005`** (`409`).<br>**E6:** huỷ run ở trạng thái không cho phép (đã `Published`) ⇒ **`0006`** (`400`).<br>**E7:** sửa/xoá run đã `Published` ⇒ **`0007`** (`409`) — append-only (D6).<br>**E8:** kỳ báo cáo **ngoài cửa sổ** cho phép hoặc **ngày tương lai** ⇒ **`0008`** (`400`).<br>**E9:** yêu cầu lãi gộp khi `CostMissing` **và** tham số `BatBuocCoGiaVon = true` ⇒ **`0009`** (`409`) — "từ chối rõ ràng" tốt hơn trả số 0 sai (kế thừa tinh thần `XC-001` Q6). |
| **Business Rules** | • **BR-REP-003-001**: Báo cáo **chỉ** đọc read-model `Rpt*`; **cấm** app service của Reporting truy vấn bảng giao dịch (`OrdOrders`, `OrdOrderLines`, `InvStockLedger`, `InvItems`, `Chn*`, `Pay*`, `Tax*`).<br>• **BR-REP-003-002**: Read-model **chỉ** được ghi bởi job tổng hợp (qua port); **cấm** đường ghi từ API người dùng (trừ `RptAggregationRuns` do API tạo run).<br>• **BR-REP-003-003**: Mỗi dòng read-model có **khoá nghiệp vụ duy nhất**; mọi ghi là **upsert xác định** ⇒ chạy lại **không nhân bản** (NFR-011).<br>• **BR-REP-003-004**: `RptAggregationRuns` là **append-only**; nội dung run `Published` **bất biến** (không sửa/xoá); chỉ cho phép **đúng một** chuyển trạng thái `Published → Superseded` (ghi `SupersededByRunId`, §3).<br>• **BR-REP-003-005**: Mọi chuyển trạng thái run **sai** phải **NÉM lỗi có mã** — **cấm** no-op im lặng.<br>• **BR-REP-003-006**: Tiền trong read-model là `decimal` + `Money`; **cấm** `double`/`float`; **cấm** so sánh tiền bằng `==` (so bằng `ToMinorUnits()`).<br>• **BR-REP-003-007**: Báo cáo **không làm tròn lại**; dùng số đã chốt trên dòng đơn; **tổng = tổng các dòng đã làm tròn** (kế thừa `XC-001` BR-005).<br>• **BR-REP-003-008**: Run **chỉ được publish** khi đối soát đạt: **lệch tiền = 0 đồng**, **lệch số lượng = 0 đơn vị** (NFR-004).<br>• **BR-REP-003-009**: Thiếu giá vốn ⇒ `CogsAmount`/`TurnoverRatio`/`DaysOfInventory`/`GrossMargin1Amount`/`GrossMargin2Amount` = **`NULL`** + cờ `CostMissing`; **cấm** ghi `0`; giá vốn `0` là **hợp lệ** (D-177, D-181).<br>• **BR-REP-003-010**: Phí sàn **dự kiến** (`Order.ChannelFeeAmount`) và **thực tế** (settlement) là **hai cột riêng**; chưa có settlement ⇒ cột thực tế `NULL` + cờ `SettlementPending` — cờ **chỉ** bật cho **kênh sàn** (`Channel.Type = 1`); kênh web/offline hai cột `NULL` + `SettlementPending = false`; **cấm** ghi `0` thay "chưa biết" và **cấm** lấy phí dự kiến làm số thực tế (D-179).<br>• **BR-REP-003-011**: Đơn `Cancelled`/`Returned` **bị loại khỏi doanh thu ròng** (và hoàn lại số lượng đã bán) — nhưng **vẫn hiển thị** số đơn gộp để đối chiếu.<br>• **BR-REP-003-012**: Cách ly tenant: báo cáo của tenant A **không** chứa dữ liệu tenant B, kể cả qua bộ lọc/sắp xếp/xuất file.<br>• **BR-REP-003-013**: UI **phải** ghi rõ: (a) **độ trễ dữ liệu** của kỳ; (b) **chênh lệch giữa số đếm và số dòng hiển thị** khi có lọc/phân trang; (c) kết quả **rỗng không phải lỗi**.<br>• **BR-REP-003-014**: Job **phải** log khởi động/dừng/lỗi + `CorrelationId`; **cấm** log `Information` mỗi vòng; metric mới **phải** nằm trong `eCommerceMetricNames` **và** `XC-005` §5.1; nhãn metric **chỉ** lấy từ `eCommerceMetricTags.Allowed`. |
| **Non-Functional Requirement** | • **NFR-REP-003-001**: Truy vấn báo cáo **p95 < 1 s** cho kỳ 30 ngày × 50.000 SKU (chỉ đọc read-model).<br>• **NFR-REP-003-002**: Job tổng hợp **1 ngày × 100.000 dòng ledger < 5 phút**.<br>• **NFR-REP-003-003**: Độ trễ read-model: tồn kho **T+1** (xong trước **05:00** giờ VN); hiệu suất kênh **< 15 phút** — L2 thực hiện bằng **một job** `D-172` (mặc định 5 phút, trần < 15 phút), **không** hạ về T+1 (D-178).<br>• **NFR-REP-003-004**: Đối soát: lệch tiền **0 đồng**, lệch số lượng **0 đơn vị**.<br>• **NFR-REP-003-005**: **0** truy vấn trên DB giao dịch kéo dài **> 30 s**; job **nặng** (L1/L3 batch) **chỉ** chạy trong cửa sổ **01:00–05:00** giờ VN — làn L2 (job 5 phút/lookback 3 ngày) **miễn trừ** cửa sổ này (D-178).<br>• **NFR-REP-003-006**: Xuất Excel **12 tháng × 20 kênh < 60 s**.<br>• **NFR-REP-003-007**: Retention chi tiết ngày **mặc định KHÔNG xoá** (Setting `0`; `24` tháng là **đề xuất** chờ kế toán — D-182); bảng tổng hợp tháng **giữ vô hạn**; read-model **≤ 20 %** dung lượng DB giao dịch.<br>• **NFR-REP-003-008**: Cách ly tenant **100 %** (test 2 tenant).<br>• **NFR-REP-003-009**: **0** `double`/`float` trong assembly read-model (test reflection).<br>• **NFR-REP-003-010**: Metric mới **nằm trong** bảng đăng ký ⇒ `TC-06`/`TC-07` hiện có (`test/eCommerce.Tests/Observability/MetricsTests.cs`) **vẫn xanh**.<br>• **NFR-REP-003-011**: Chạy lại 1 kỳ **10 lần** ⇒ hash read-model **không đổi**.<br>• **NFR-REP-003-012**: Khi hệ thống rỗi, log **< 10 dòng/phút** (kế thừa `XC-005` NFR-004). |

---

## 2. Sơ đồ Use Case

```mermaid
flowchart LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef usecase fill:#e2f4f6,stroke:#17a2b8,stroke-width:2px;
    classDef note fill:#fffbe6,stroke:#ffc107,stroke-width:1px,color:#5c4a00;

    QL(("👤<br/>Quản lý<br/>vận hành kho"))
    KD(("👤<br/>Quản lý<br/>kênh"))
    KT(("👤<br/>Kế toán<br/>trưởng"))
    JOB(("⚙️<br/>Job tổng hợp"))

    subgraph REP["📦 Module Reporting — read-model Rpt"]
        direction TB
        UC1(["«xem vòng quay và ngày tồn kho»"])
        UC2(["«xem tồn kho theo kho và SKU»"])
        UC3(["«xem hiệu suất theo kênh»"])
        UC4(["«so sánh kênh và phí sàn»"])
        UC5(["«chạy lại kỳ tổng hợp»"])
        UC6(["«đối soát read-model với nguồn»"])
        UC7(["«xuất Excel cho kế toán»"])
        class UC1,UC2,UC3,UC4,UC5,UC6,UC7 usecase;
    end

    N1["<b>Nguyên tắc:</b><br/>«báo cáo CHỈ đọc read-model»<br/>«chỉ job tổng hợp mới đọc DB giao dịch»"]
    class N1 note;

    QL --- UC1
    QL --- UC2
    KD --- UC3
    KD --- UC4
    KT --- UC7
    JOB --- UC5
    JOB --- UC6
    UC1 -. "«include»" .-> UC6
    UC3 -. "«include»" .-> UC6
    UC5 -.- N1
```

---

## 3. Sơ đồ trạng thái (state machine) — vòng đời một lần chạy tổng hợp

```mermaid
stateDiagram-v2
    [*] --> Pending : «API yêu cầu hoặc job định kỳ (5 phút)»
    Pending --> Running : «giành được khoá và ghi được run row»
    Pending --> Cancelled : «admin huỷ trước khi chạy»
    Running --> Computed : «ghi xong read-model trong Unit of Work»
    Running --> Failed : «lỗi ghi hoặc lỗi đọc nguồn»
    Computed --> Validated : «đối soát lệch = 0 (tiền, số lượng)»
    Computed --> Failed : «đối soát lệch khác 0»
    Validated --> Published : «đánh dấu công bố cho đường đọc»
    Published --> Superseded : «run mới hơn đã Published»
    Failed --> [*]
    Cancelled --> [*]
    Superseded --> [*]
```

**Bảng chuyển trạng thái & guard (mọi chuyển sai **phải ném lỗi**, không im lặng):**

| Từ | Sang | Guard | Nếu vi phạm |
|---|---|---|---|
| `[*]` | `Pending` | `TenantId` có giá trị **và** kỳ nằm trong cửa sổ cho phép **và** ngày không ở tương lai | **`eCommerce:Reporting:0008`** (`400`) |
| `Pending` | `Running` | Giành được khoá `report:tong-hop` (`TongHopBaoCaoService.TenKhoaPhanTan`; D-178) **và** không có run `Running` cùng `(tenant, scope, kỳ)` **và** ghi được run row | **`0001`** (`409`); DB chặn bằng partial unique index (D7) |
| `Pending` | `Cancelled` | Có quyền `eCommerce.Reporting.Aggregation.Manage` | **`0006`** (`400`) |
| `Running` | `Computed` | **Mọi** batch ghi xong, `SourceRowCount`/`WrittenRowCount` khớp số dự kiến | Giữ `Running` nếu còn batch; lỗi ⇒ `Failed` + **`0002`** |
| `Running` | `Failed` | Đã bắt được exception **và** ghi `ErrorCode` + `CorrelationId` | **`0002`** (`500`) — **cấm** nuốt lỗi im lặng |
| `Computed` | `Validated` | Đối soát: **lệch tiền = 0 đồng** và **lệch số lượng = 0 đơn vị** | **`0003`** (`422`) |
| `Computed` | `Failed` | Đối soát lệch khác `0` | Ghi `DriftAmount`; **`0003`**; **cấm** publish |
| `Validated` | `Published` | Run có bản ghi đối soát `Passed = true` **và** chưa từng `Published` | **`0004`** (`400`) |
| `Published` | `Superseded` | Tồn tại run mới hơn **đã** `Published` cho cùng `(tenant, scope, kỳ)` | **`0005`** (`409`) — bản thay thế phải publish trước |
| `Published` | *(bất kỳ)* | **CẤM** sửa **nội dung**/xoá/huỷ; ngoại lệ **duy nhất** là `Published → Superseded` (dòng trên) | **`0007`** (`409`) — append-only (D6) |
| `Failed`/`Cancelled`/`Superseded` | *(bất kỳ)* | Trạng thái kết thúc — chỉ tạo **run mới** | **`0005`** |

> ⚠️ **`Computed → Failed` là nhánh quan trọng nhất:** đây là chỗ hệ thống **từ chối công bố** một con số đã tính ra nhưng **không khớp nguồn**. Nếu cho publish, kế toán và vận hành sẽ ra quyết định trên số sai — và **không có dấu vết nào**. Kế thừa nguyên tắc `XC-003` D2: *"nếu bỏ lock đi, DB có chặn không?"* — ở đây là *"nếu bỏ đối soát đi, ai biết số sai?"*.

---

## 4. Sequence Diagram & luồng dữ liệu

### 4.1 Job tổng hợp một ngày — từ nguồn tới read-model đã công bố

```mermaid
sequenceDiagram
    participant W as ReportingAggregationWorker
    participant L as Redis lock
    participant P as Port doc du lieu nguon
    participant SDB as PostgreSQL giao dich
    participant RM as Bang Rpt read-model
    participant RC as Buoc doi soat
    participant API as Report App Service

    W->>L: «TryAcquire report:tong-hop»
    alt «khong gianh duoc khoa»
        W->>W: «SkipIfBusy — log Information + metric lock_skipped_total»
    else «gianh duoc khoa»
        W->>RM: «tao RptAggregationRuns Status = Pending roi Running»
        W->>P: «GetOrderFactsAsync va GetInventoryFactsAsync theo cua so ngay»
        P->>SDB: «truy van co chi muc, batch theo con tro»
        SDB-->>P: «fact rows»
        P-->>W: «fact rows»
        W->>RM: «upsert theo khoa nghiep vu trong Unit of Work»
        W->>RC: «cong lai read-model va so voi tong nguon»
        alt «lech trong nguong 0 dong»
            RC-->>W: «Passed»
            W->>RM: «Computed roi Validated roi Published»
        else «lech vuot nguong»
            RC-->>W: «Failed kem DriftAmount»
            W->>RM: «Status = Failed — KHONG publish»
        end
    end
    API->>RM: «chi doc bang Rpt — khong cham DB giao dich»
    RM-->>API: «so lieu da Published kem RunId»
```

### 4.2 Luồng dữ liệu & hai làn cập nhật

| Làn | Nguồn | Nhịp | Bảng đích | Độ trễ | Ghi chú |
|---|---|---|---|---|---|
| **L1 — Batch tồn kho** | `InvStockLedger` (append-only), `InvItems` (snapshot) | 1 lần/ngày, 01:00–05:00 giờ VN | `RptInventoryDailySnapshots`, `RptInventoryTurnover` | **T+1** | Cần **số dư cuối ngày** ⇒ không thể event-driven thuần |
| **L2 — Làn trong ngày (kênh)** | `Rpt*` + nguồn đọc lại qua **đúng một job** `D-172` (lookback 3 ngày) | **5 phút** (ABP Setting, trần < 15 phút) | `RptChannelDailyPerformance` (dòng ngày hiện tại) | **< 15 phút** | Số trong ngày là **tạm tính** (`provisional` + `RunId` + thời điểm); consumer ETO chỉ là **tối ưu về sau** (D-178) |
| **L3 — Đối soát & chốt** | Nguồn (đọc lại theo cửa sổ) | 1 lần/ngày (đêm) sau L1/L2 | `RptReconciliations` + **chốt** dòng `Rpt*` | T+1 | Bắt buộc — L2 luôn có thể trôi; L3 đêm mới `Published` (BR-008) |

> 📌 **Vì sao phải có L3:** `XC-002` chốt rằng sự kiện đi qua outbox/inbox và **có thể đến trùng hoặc sai thứ tự**; consumer bỏ qua bản cũ **có ghi log + metric** (`XC-002` §1 A4). ⇒ Số liệu kênh dựng thuần từ sự kiện **không thể** là nguồn sự thật. L3 là lưới an toàn, và **`REP-003` chỉ publish sau khi L3 đạt** (BR-008). ⚠️ L2 **tắt** (chưa làm kịp / đo tải vượt trần) ⇒ báo cáo ghi **T+1 trung thực**, **cấm** hiện số "trong ngày" (D-178).

> ⚠️ **`Rpt*` KHÔNG phải nguồn sự thật.** Nguồn sự thật vẫn là `InvStockLedger` (sổ cái kho append-only — `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:239–243`) và `OrdOrders`/`OrdOrderLines`. Read-model **được phép xoá và dựng lại**; nếu read-model và nguồn lệch ⇒ **nguồn đúng** (trừ khi chứng minh ngược lại được, và khi đó phải sửa nguồn, không sửa read-model).

---

## 5. Mô hình dữ liệu

### 5.1 ERD

**Không áp dụng dạng sơ đồ — lý do:** giới hạn **tối đa 3 sơ đồ Mermaid** cho tài liệu này (§2, §3, §4.1 đã dùng hết). Thay ERD bằng **quan hệ tường minh** dưới đây + Data Dictionary ở §5.2:

| Quan hệ | Kiểu | Khoá | Ghi chú |
|---|---|---|---|
| `RptInventoryDailySnapshots` **N—1** `RptAggregationRuns` | tham chiếu | `RunId` | Không FK cứng (read-model có thể dựng lại); truy vết bằng `RunId` |
| `RptInventoryTurnover` **N—1** `RptAggregationRuns` | tham chiếu | `RunId` | |
| `RptChannelDailyPerformance` **N—1** `RptAggregationRuns` | tham chiếu | `RunId` | |
| `RptReconciliations` **N—1** `RptAggregationRuns` | FK | `RunId` | **Append-only**, ghi một lần cho mỗi lần kiểm |
| `Rpt*` **N—1** `InvItems` / `OrdOrders` | **luận lý** (không FK) | `TenantId + WarehouseId + Sku` / `TenantId + ChannelCode + ChannelShopId` | Không FK vì read-model được `TRUNCATE`/dựng lại độc lập (D5) |

### 5.2 Data Dictionary

**Bảng `RptInventoryDailySnapshots`** — số dư & biến động **theo ngày** cho mỗi `(WarehouseId, Sku)`. Kiểu số lượng: `numeric(18,3)` (`D-31`; `StockLedgerEntry.QuantityDelta`/`BalanceAfter` là `decimal` — `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:254,257`). Nguồn: đọc `StockLedger` **theo từng kho** trong khoảng nửa mở `[đầu ngày, cuối ngày)`.

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✗ | tenant | **NOT NULL** (bắt buộc — §12) |
| `BusinessDate` | `date` | ✗ | ngày nghiệp vụ (giờ VN) | Lưu **ngày**, không lưu timestamp ⇒ tránh lệch múi giờ |
| `WarehouseId` | `uuid` | ✗ | kho | |
| `Sku` | `varchar(64)` | ✗ | SKU | Khớp `InvItems.Sku` (cấu hình trong `modules/Inventory/eCommerce.Inventory.EntityFrameworkCore/InventoryDbContextModelCreatingExtensions.cs`) |
| `ProductVariantId` | `uuid` | ✓ | biến thể | Có thể trống nếu SKU chưa map |
| `OpeningQty` | `numeric(18,3)` | ✗ | tồn đầu ngày | = `ClosingQty` ngày trước |
| `ReceiptQty` | `numeric(18,3)` | ✗ | nhập kho | `StockMovementType.Receipt` |
| `SaleQty` | `numeric(18,3)` | ✗ | xuất bán/giao | `Sale` (3) + `RetailSaleOut` (19, POS) + `ShippingOut` (11, phiếu `Mode=Delivery`); đọc theo `(BusinessDate, WarehouseId, Sku)` nửa khoảng ngày giờ VN, **không** `ABS`, **không** trừ trả lại (D-185) |
| `ReturnQty` | `numeric(18,3)` | ✗ | khách trả lại | `StockMovementType.CustomerReturn` |
| `AdjustmentDelta` | `numeric(18,3)` | ✗ | điều chỉnh kiểm kê (**độ lớn dương**; `Adjustment` luôn **cộng** vào `OnHand`) | `StockMovementType.Adjustment` — nhánh `+=` ở `StockLedger.cs:107–111` |
| `TransferInQty` / `TransferOutQty` | `numeric(18,3)` | ✗ | chuyển kho vào/ra (**đếm riêng**, không net) | `StockMovementType.TransferIn` / `TransferOut`; **cấm** `Transfer` (=6, legacy) (`D-170`) |
| `ClosingOnHand` | `numeric(18,3)` | ✗ | tồn cuối ngày | `OpeningQty + Σ (dấu theo MovementType) × QuantityDelta` của **chính kho** — dấu đúng như `InventoryItem.ApplyMovement` (`StockLedger.cs:103–180`); `QuantityDelta` lưu **độ lớn dương** (`StockPostingService.cs:184,187`) |
| `ClosingReserved` | `numeric(18,3)` | ✗ | đang giữ chỗ | = tồn `Reserved` cuối ngày của `(kho, SKU)` (`InventoryItem.Reserved`) |
| `ClosingAvailable` | `numeric(18,3)` | ✗ | khả dụng | `OnHand − Reserved − Transferring − Holding − Damaged` (`InventoryItem.Available`, `StockLedger.cs:72`, `D-27`) |
| `InventoryValueAmount` | `numeric(18,4)` | ✓ | giá trị tồn | ⚠️ **NULL** khi chưa có giá vốn + cờ `CostMissing` |
| `Currency` | `char(3)` | ✗ | tiền tệ của dòng | Quy ước `XC-001` §5.2; mỗi dòng **một** tiền tệ |
| `CostMissing` | `bool` | ✗ | thiếu giá vốn | BR-009 |
| `LastAggregatedAtUtc` | `timestamptz` | ✗ | thời điểm ghi | Mọi thời điểm trong DB là **UTC** (quy ước `eCommerceDomainModule.cs:47–61`) |
| `RunId` | `uuid` | ✗ | lần chạy đã sinh dòng | Truy vết |

> 🔒 **Chuyển kho là HAI bút toán (`D-170`):** mỗi dòng phiếu chuyển sinh `TransferOut` (kho nguồn) + `TransferIn` (kho đích), cùng độ lớn dương và cùng `ReferenceLineId` (unique index `(ReferenceLineId, MovementType)` — `D-132`), nên read-model đọc **theo từng kho** chỉ thấy vế của kho đó. **Không** dựng `ClosingQty` chỉ từ các cột phân rã (`Receipt + … + TransferIn − TransferOut`): chúng là **tập con** (thiếu `ShippingOut`, `SupplierReturnOut`, `StocktakeGain/Loss`, `Hold*`…). **Nhưng cũng KHÔNG cộng thô** `QuantityDelta`: phải áp **dấu theo `MovementType`** đúng bảng `ApplyMovement` — cột lưu **độ lớn dương**, chiều do loại bút toán quyết định (`StockPostingService.cs:184,187`; quy ước dấu ở `TinhButToanChungTu.cs:10–13`); cộng thô sẽ **cộng** cả phần bán/xuất vào tồn cuối.

**Bảng `RptInventoryTurnover`** — chỉ tiêu luân chuyển **theo kỳ** (tuần/tháng/quý) cho mỗi (kho, SKU).

| Cột | Kiểu | Null? | Ý nghĩa | Công thức |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✗ | tenant | |
| `PeriodStart` / `PeriodEnd` | `date` | ✗ | kỳ | |
| `WarehouseId` / `Sku` | `uuid` / `varchar(64)` | ✗ | phạm vi | |
| `OpeningQty` / `ClosingQty` | `numeric(18,3)` | ✗ | tồn đầu/cuối kỳ | từ snapshot |
| `AverageQty` | `numeric(18,3)` | ✗ | tồn bình quân **2 đầu kỳ** | `(OpeningQty + ClosingQty) / 2` — **không** dùng trung bình cộng các ngày (tránh lệch khi thiếu ngày; tái lập được) (D-176) |
| `CogsAmount` | `numeric(18,4)` | ✓ | giá vốn hàng bán | Σ `Quantity × CostUnitAmount` của `RptFactSalesLines` (snapshot theo **ngày bán** UTC+7, đọc qua **một** method hợp đồng của module sở hữu — D-177); **NULL** nếu thiếu giá vốn |
| `TurnoverRatio` | `numeric(18,6)` | ✓ | vòng quay | `CogsAmount / AverageQty` — **NULL** nếu `AverageQty = 0` hoặc thiếu giá vốn; một hàm thuần (D-176) |
| `DaysOfInventory` | `int` | ✓ | ngày tồn kho | `SốNgàyKỳ / TurnoverRatio` — **NULL** nếu `TurnoverRatio` NULL (D-176) |
| `MovementClass` | `int` | ✗ | `0=Fast · 1=Moving · 2=Slow · 3=Dead` | Ngưỡng cấu hình theo tenant (Setting) |
| `CoveredDays` | `int` | ✗ | số ngày **có** snapshot trong kỳ | 🔒 BR-013: nếu `CoveredDays < SốNgàyKỳ` ⇒ UI **phải** hiện banner "thiếu dữ liệu n ngày", **không** nội suy, `DaysOfInventory` **không** tính lại theo số ngày thực có (D-176) |
| `CostMissing` / `Currency` | `bool` / `char(3)` | ✗ | cờ + tiền tệ | |
| `RunId` | `uuid` | ✗ | truy vết | |

**Bảng `RptChannelDailyPerformance`** — hiệu suất **ngày × kênh × shop**.

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✗ | tenant | |
| `BusinessDate` | `date` | ✗ | ngày nghiệp vụ | |
| `ChannelCode` | `varchar(32)` | ✗ | mã kênh | Khớp `Order.ChannelCode` (`Order.cs:18`) và `Channel.Code` |
| `ChannelShopId` | `uuid` | ✓ | shop trên sàn | Giữ nghĩa `Order.ChannelShopId` (`Guid?`, `Order.cs:22`); unique index **`NULLS NOT DISTINCT`** — **bỏ** sentinel `Guid.Empty`; đơn không có shop ⇒ dòng `NULL` của **đúng** `ChannelCode`; **không** thêm cột `ChannelShopName` (tra `ChannelShop` lúc hiển thị) (D-180) |
| `GrossOrders` | `int` | ✗ | số đơn gộp | Mọi trạng thái |
| `CancelledOrders` / `ReturnedOrders` | `int` | ✗ | đơn huỷ / hoàn | `OrderStatus.Cancelled` / `.Returned` (`Enums.cs:50–59`) |
| `UnitsSold` | `numeric(18,3)` | ✗ | số lượng bán **ròng** | Trừ đơn huỷ/hoàn (BR-011); `D-31` — khai `decimal` dù nguồn `OrderLine.Quantity` là `int` (`Order.cs:832`), để không vỡ kiểu khi bán lẻ/trọng lượng lẻ về sau |
| `GrossRevenueAmount` | `numeric(18,4)` | ✗ | doanh thu gộp **không gồm thuế** | Σ `ThanhTienDongDonHang` = `UnitPrice×Quantity − DiscountAmount` (`modules/Reporting/eCommerce.Reporting.Application/AnhXaNguonBanHang.cs:112–120`) = Σ (`LineTotal − TaxAmount`); **cấm** dùng thẳng `OrderLine.LineTotal` vì nó **cộng** `TaxAmount` (`Order.cs:840`) |
| `DiscountAmount` | `numeric(18,4)` | ✗ | giảm giá | Σ chiết khấu **cấp chứng từ** `Order.DiscountTotal` (`Order.cs:158`) — **không** trừ lại chiết khấu dòng (đã nằm trong thành tiền dòng) |
| `TaxAmount` | `numeric(18,4)` | ✗ | thuế GTGT | Σ `OrderLine.TaxAmount` (**đã chốt** tại thời điểm bán — `XC-006` D3) |
| `ShippingCollectedAmount` | `numeric(18,4)` | ✗ | phí vận chuyển thu khách | Khớp `Order.ShippingTotal` |
| `ChannelFeeEstimatedAmount` | `numeric(18,4)` | ✓ | **phí sàn dự kiến** | Từ `Order.ChannelFeeAmount` (**nullable** — `Order.cs:167`) |
| `ChannelFeeActualAmount` | `numeric(18,4)` | ✓ | **phí sàn thực tế** | ⚠️ **Luôn NULL ở GĐ3** — `REC-002` chưa có; cờ `SettlementPending` |
| `NetRevenueAmount` | `numeric(18,4)` | ✗ | doanh thu ròng | `GrossRevenueAmount − DiscountAmount + ShippingCollectedAmount` — **không** gồm thuế (thuế ở `TaxAmount`, là khoản phải nộp, không phải doanh thu) |
| `RefundAmount` | `numeric(18,4)` | ✗ | đã hoàn tiền | Σ tiền **ghi nhận hoàn** của phiếu trả thuộc ngày nghiệp vụ (`OrderReturn.GhiNhanHoanTien`, `modules/Sales/eCommerce.Sales.Domain/OrderReturn.cs:303`); không có phiếu trả ⇒ `0` |
| `AovAmount` | `numeric(18,4)` | ✓ | giá trị đơn bình quân | `NetRevenue / SốĐơnTínhDoanhThu`; **NULL** nếu mẫu số = 0 |
| `GrossMargin1Amount` | `numeric(18,4)` | ✓ | **lãi gộp 1** (trước phí sàn/ship) | `NetRevenueAmount − CogsAmount`; **NULL** + `CostMissing` nếu thiếu giá vốn; **không** trừ VAT lần hai (D-181) |
| `GrossMargin2Amount` | `numeric(18,4)` | ✓ | **lãi gộp 2** (sau phí) | `GrossMargin1Amount − ChannelFee − Order.ShippingFeeActual`; phí/ship `NULL` ⇒ **không** cấn trừ (cấm bịa `0`); `SettlementPending` ⇒ nhãn *"tạm tính"* (D-181) |
| `CostMissing` | `bool` | ✗ | thiếu giá vốn | `true` ⇒ **cả hai** cột lãi gộp `NULL` (D-181) |
| `SettlementPending` | `bool` | ✗ | chưa có settlement | **Chỉ** `true` khi kênh sàn (`Channel.Type = 1`) chưa có settlement; web/offline **false** + hai cột phí `NULL`; DB CHECK `SettlementPending OR ChannelFeeActualAmount IS NULL` (D-179) |
| `CodUnreconciled` | `bool` | ✗ | COD chưa đối soát | ⚠️ xem §7.2 (GHTK/GHN) |
| `UnclassifiedTaxSkuCount` | `int` | ✗ | số SKU trong kỳ **chưa phân loại thuế** | Liên kết gauge `tax_unclassified_skus` (`XC-006`) — hiệu suất kênh **có thể sai thuế** nếu > 0 |
| `Currency` | `char(3)` | ✗ | tiền tệ | 1 dòng = 1 tiền tệ; GĐ3 chỉ **VND** |
| `RunId` / `LastAggregatedAtUtc` | `uuid` / `timestamptz` | ✗ | truy vết | |

**Bảng `RptAggregationRuns`** — **sổ đăng ký lần chạy, APPEND-ONLY** (D6, BR-004).

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính |
| `TenantId` | `uuid` | ✗ | tenant |
| `Scope` | `int` | ✗ | `0=Inventory · 1=Channel · 2=Both` |
| `BusinessDateFrom` / `BusinessDateTo` | `date` | ✗ | cửa sổ kỳ |
| `Version` | `int` | ✗ | tăng khi chạy lại cùng kỳ |
| `Status` | `int` | ✗ | `0=Pending · 1=Running · 2=Computed · 3=Validated · 4=Published · 5=Failed · 6=Cancelled · 7=Superseded` |
| `StartedAtUtc` / `FinishedAtUtc` | `timestamptz` | ✗ / ✓ | thời gian |
| `SourceRowCount` / `WrittenRowCount` | `bigint` | ✗ | số dòng đọc nguồn / ghi read-model |
| `DriftAmount` / `DriftQuantity` | `numeric(18,4)` / `numeric(18,3)` | ✗ | độ lệch đối soát (phải = 0 để publish — BR-008) |
| `ErrorCode` / `ErrorMessage` | `varchar(32)` / `varchar(2048)` | ✓ | mã lỗi `eCommerce:Reporting:*` |
| `CorrelationId` | `varchar(64)` | ✓ | truy vết `XC-005` |
| `LockKey` | `varchar(256)` | ✓ | khoá đã dùng — hỗ trợ điều tra khoá treo |
| `SupersededByRunId` | `uuid` | ✓ | run thay thế |

**Bảng `RptReconciliations`** — **append-only**, kết quả từng phép kiểm đối soát.

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính |
| `RunId` | `uuid` | ✗ | FK → `RptAggregationRuns` |
| `CheckKind` | `int` | ✗ | `0=RevenueTotal · 1=TaxTotal · 2=QuantityTotal · 3=OrderCount · 4=SnapshotBalance` |
| `SourceValue` / `ReadModelValue` / `DriftValue` | theo `CheckKind` | ✗ | giá trị nguồn / read-model / lệch | Tiền (`RevenueTotal`/`TaxTotal`): `numeric(18,4)`; số lượng (`QuantityTotal`): `numeric(18,3)`; số đơn: `bigint` |
| `Passed` | `bool` | ✗ | đạt |
| `CheckedAtUtc` | `timestamptz` | ✗ | thời điểm |

**Index & ràng buộc**

| Loại | Bảng | Cột | Mục đích |
|---|---|---|---|
| **UNIQUE** | `RptInventoryDailySnapshots` | `(TenantId, BusinessDate, WarehouseId, Sku)` | ⭐ Khoá nghiệp vụ ⇒ **idempotency** (BR-003) |
| **UNIQUE** | `RptInventoryTurnover` | `(TenantId, PeriodStart, PeriodEnd, WarehouseId, Sku)` | idempotency |
| **UNIQUE** | `RptChannelDailyPerformance` | `(TenantId, BusinessDate, ChannelCode, ChannelShopId)` | idempotency; `ChannelShopId` **nullable** + **`NULLS NOT DISTINCT`** (bỏ sentinel `Guid.Empty` — D-180) |
| **UNIQUE (filter)** | `RptAggregationRuns` | `(TenantId, Scope, BusinessDateFrom, BusinessDateTo, Version)` | chống trùng run |
| **UNIQUE (filter)** | `RptAggregationRuns` | `(TenantId, Scope, BusinessDateFrom, BusinessDateTo) WHERE Status = 1` | 🚨 **Hàng rào DB chặn 2 run `Running` song song** (D7) — EF Core hỗ trợ `HasFilter` (đã dùng: `eCommerceDbContext.cs:434`) |
| **INDEX** | `RptInventoryDailySnapshots` | `(TenantId, WarehouseId, Sku, BusinessDate)` | lịch sử theo SKU |
| **INDEX** | `RptInventoryDailySnapshots` | `(TenantId, BusinessDate)` | báo cáo theo kỳ |
| **INDEX** | `RptChannelDailyPerformance` | `(TenantId, ChannelCode, BusinessDate)` | so sánh kênh |
| **INDEX** | `RptInventoryTurnover` | `(TenantId, PeriodEnd, MovementClass)` | báo cáo hàng chậm/chết |
| **CHECK** | `RptInventoryDailySnapshots` | `ClosingOnHand >= 0 AND ClosingReserved >= 0` | khớp `InventoryItem.ApplyMovement` (ném khi âm — `StockLedger.cs:185–224`) |
| **CHECK** | `RptInventoryDailySnapshots` | `ClosingAvailable >= 0 AND ClosingAvailable <= ClosingOnHand - ClosingReserved` | `D-27`: đẳng thức chỉ đúng khi `Transferring/Holding/Damaged = 0` |
| **CHECK** | `RptAggregationRuns` | `FinishedAtUtc IS NULL OR FinishedAtUtc >= StartedAtUtc` | dữ liệu hợp lệ |
| **CHECK** | `RptAggregationRuns` | `Status <> 4 OR (DriftAmount = 0 AND DriftQuantity = 0)` | 🚨 **DB chặn publish số liệu lệch** (BR-008) — không phụ thuộc code |
| **CHECK** | `RptInventoryTurnover` | `CostMissing = true OR CogsAmount IS NOT NULL` | không có "giá vốn biết mà thiếu" |
| **CHECK** | `RptChannelDailyPerformance` | `SettlementPending OR ChannelFeeActualAmount IS NULL` | 🚨 thiếu settlement ⇒ cột phí thực tế **phải** NULL (D-179); "pending chỉ cho kênh sàn" do job bảo đảm + test (cần join `Channel`) |
| Ghi chú | `RptChannelDailyPerformance` | — | **Không** đặt `CHECK (NetRevenueAmount >= 0)`: hoàn tiền/điều chỉnh có thể làm âm hợp lệ (kế thừa `XC-001` §5.2) |

**Quy ước tiền trong read-model (liên kết `XC-001` §5.2):**

| Trường hợp | Cách làm |
|---|---|
| Tiền **bắt buộc** trong entity nghiệp vụ | `ComplexProperty` ⇒ 2 cột `<Ten>Amount`, `<Ten>Currency` (`XC-001` D6) |
| Tiền trong **read-model** (`Rpt*`) | Vì mỗi dòng có **một** tiền tệ dùng chung cho mọi cột tiền, dùng **`numeric(18,4)` cho từng cột + 1 cột `Currency char(3)`** ở cấp dòng ⇒ tránh nhân đôi số cột (một dòng có tới 8 cột tiền) |
| So sánh/đối soát tiền | **Chỉ** qua `Money.ToMinorUnits()`; **cấm** `==` trên `decimal` (`XC-001` BR-003) |

---

## 6. Hợp đồng API

### 6.1 Application Service

```csharp
namespace eCommerce.Reporting.Inventory;
using eCommerce.Kernel.Monetary;   // ⚠️ XC-001 — CHƯA có trong code (xem §15.2 F5b)

public interface IInventoryTurnoverReportAppService : IApplicationService
{
    /// Vòng quay & ngày tồn kho theo kỳ. CHỈ đọc RptInventoryTurnover (BR-001).
    Task<PagedResultDto<InventoryTurnoverDto>> GetTurnoverAsync(GetInventoryTurnoverInput input);

    /// Tồn kho theo ngày (KPI + bảng chi tiết). CHỈ đọc RptInventoryDailySnapshots.
    Task<PagedResultDto<InventoryDailySnapshotDto>> GetDailySnapshotsAsync(GetInventoryDailyInput input);

    /// Hàng chậm / hàng chết (MovementClass = Slow | Dead), xếp theo giá trị tồn giảm dần.
    Task<PagedResultDto<SlowMovingStockDto>> GetSlowMovingAsync(GetSlowMovingInput input);

    /// Phân bố tuổi tồn (aging buckets: 0-30 · 31-60 · 61-90 · 91-180 · > 180 ngày).
    Task<InventoryAgingDto> GetAgingAsync(GetInventoryAgingInput input);

    Task<IRemoteStreamContent> ExportAsync(ExportInventoryTurnoverInput input); // quyền Export
}

namespace eCommerce.Reporting.Channels;

public interface IChannelPerformanceReportAppService : IApplicationService
{
    /// Hiệu suất theo ngày × kênh × shop. CHỈ đọc RptChannelDailyPerformance (BR-001).
    Task<PagedResultDto<ChannelPerformanceDto>> GetDailyAsync(GetChannelPerformanceInput input);

    /// Tổng hợp kỳ + so sánh kênh. Mặc định xếp theo **doanh thu ròng**; nếu người dùng sắp theo "lãi gộp" ⇒ sắp **LG2** (D-181).
    Task<ChannelPerformanceSummaryDto> GetSummaryAsync(GetChannelPerformanceInput input);

    /// Chuỗi thời gian để vẽ xu hướng (theo ngày/tuần/tháng).
    Task<IReadOnlyList<ChannelTrendPointDto>> GetTrendAsync(GetChannelTrendInput input);

    Task<IRemoteStreamContent> ExportAsync(ExportChannelPerformanceInput input); // quyền Export
}

namespace eCommerce.Reporting.Aggregation;

public interface IReportingAggregationAppService : IApplicationService
{
    Task<AggregationRunDto> TriggerAsync(TriggerAggregationInput input);   // tạo run Pending → chạy
    Task<AggregationRunDto> GetRunAsync(Guid id);
    Task<PagedResultDto<AggregationRunDto>> GetRunsAsync(GetAggregationRunsInput input);
    Task<AggregationRunDto> CancelAsync(Guid id, string reason);
    Task<IReadOnlyList<ReconciliationDto>> GetReconciliationsAsync(Guid runId);
}
```

**Port đọc dữ liệu nguồn (D3 — module ĐỊNH NGHĨA, app CÀI ĐẶT):**

```csharp
namespace eCommerce.Reporting.Sources;

/// Cửa sổ ngày BỊ CHẶN — bắt buộc, chống truy vấn toàn bảng (NFR-005).
public sealed record ReportingFactQuery(
    Guid TenantId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<string>? ChannelCodes = null,
    IReadOnlyList<Guid>? WarehouseIds = null,
    string? AfterCursor = null,
    int BatchSize = 5000);

/// Số lượng `decimal(18,3)` (`D-31`); `TransferInQty`/`TransferOutQty` đếm riêng theo từng kho (`D-170`).
public sealed record InventoryMovementFact(
    DateOnly BusinessDate, Guid WarehouseId, string Sku, Guid? ProductVariantId,
    decimal ReceiptQty, decimal SaleQty, decimal ReturnQty, decimal AdjustmentDelta,
    decimal TransferInQty, decimal TransferOutQty,
    decimal ClosingOnHand, decimal ClosingReserved, decimal ClosingAvailable);

/// `GrossRevenue` KHÔNG gồm thuế (dùng `ThanhTienDongDonHang`); `Tax` đi cột riêng. `UnitsSold` `decimal(18,3)` (`D-31`).
public sealed record OrderFact(
    Guid OrderId, DateOnly BusinessDate, string ChannelCode, Guid? ChannelShopId,
    int Status, string Currency, decimal GrossRevenue, decimal Discount,
    decimal Tax, decimal ShippingCollected, decimal? ChannelFee, decimal Refund,
    decimal UnitsSold, int UnclassifiedTaxSkuCount);

/// 🔒 Module Reporting KHÔNG tham chiếu entity của app (bất biến dự án).
/// App (hoặc module Inventory/Sales sau này) cài đặt 2 interface dưới đây.
public interface IInventoryFactReader
{
    Task<IReadOnlyList<InventoryMovementFact>> GetMovementsAsync(
        ReportingFactQuery query, CancellationToken ct = default);
}

public interface IOrderFactReader
{
    Task<IReadOnlyList<OrderFact>> GetOrdersAsync(
        ReportingFactQuery query, CancellationToken ct = default);
}
```

> 📌 **Đăng ký DI:** interface ở namespace `eCommerce.Reporting.Sources` (project `eCommerce.Reporting.Domain`), adapter ở tầng app (`eCommerce.Application`) — **hướng phụ thuộc app → module**, hợp lệ; hướng ngược lại là vi phạm. Khi `Inventory`/`Sales` tách thành module (GĐ1), adapter **chuyển về module đó**, interface **không đổi**.

### 6.2 REST endpoint (ABP auto API controller ⇒ `/api/app/...`)

| Method | Route | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/app/inventory-turnover-report/turnover` | `eCommerce.Reporting.InventoryTurnover.View` | vòng quay + ngày tồn kho |
| `GET` | `/api/app/inventory-turnover-report/daily-snapshots` | `.View` | tồn kho theo ngày |
| `GET` | `/api/app/inventory-turnover-report/slow-moving` | `.View` | hàng chậm/chết |
| `GET` | `/api/app/inventory-turnover-report/aging` | `.View` | phân bố tuổi tồn |
| `POST` | `/api/app/inventory-turnover-report/export` | `eCommerce.Reporting.Export` | xuất Excel/CSV |
| `GET` | `/api/app/channel-performance-report/daily` | `eCommerce.Reporting.ChannelPerformance.View` | hiệu suất ngày × kênh × shop |
| `GET` | `/api/app/channel-performance-report/summary` | `.View` | tổng hợp kỳ + so sánh kênh |
| `GET` | `/api/app/channel-performance-report/trend` | `.View` | xu hướng |
| `POST` | `/api/app/channel-performance-report/export` | `eCommerce.Reporting.Export` | xuất Excel/CSV |
| `POST` | `/api/app/reporting-aggregation/trigger` | `eCommerce.Reporting.Aggregation.Manage` | chạy lại một kỳ |
| `GET` | `/api/app/reporting-aggregation/runs` | `.Manage` | danh sách lần chạy (append-only) |
| `GET` | `/api/app/reporting-aggregation/runs/{id}` | `.Manage` | chi tiết lần chạy |
| `POST` | `/api/app/reporting-aggregation/runs/{id}/cancel` | `.Manage` | huỷ (chỉ khi `Pending`/`Running`) |
| `GET` | `/api/app/reporting-aggregation/runs/{id}/reconciliations` | `.Manage` | kết quả đối soát |
| `GET` | `/api/app/reporting-aggregation/drift?scope=&tuNgay=&denNgay=` | `eCommerce.Reporting.Aggregation.Inspect` | 🔍 **công cụ so sánh read-model ↔ nguồn** (chỉ đọc, dùng chung hàm kiểm lệch với job đêm; CLI = script `tools/` mỏng — D-184) |

> 🔒 **`[Authorize]` ở CẤP CLASS** cho **mọi** app service (bất biến dự án; bài học đã ghi tại `src/eCommerce.Application/Channels/ChannelAppService.cs:21–28`: *"ABP KHÔNG tự bảo vệ application service"*).

**Mã lỗi nghiệp vụ**

| Mã | HTTP | Khi nào | Thông điệp |
|---|---|---|---|
| `eCommerce:Reporting:0001` | `409` | Đã có run `Running` cho cùng `(tenant, scope, kỳ)` **hoặc** không giành được khoá ở chế độ `ThrowOnTimeout` | `Kỳ báo cáo này đang được tổng hợp bởi tiến trình khác, vui lòng thử lại.` |
| `eCommerce:Reporting:0002` | `500` | Lỗi ghi read-model / lỗi đọc nguồn | `Tổng hợp báo cáo thất bại: {reason}. Không có số liệu nào được công bố.` |
| `eCommerce:Reporting:0003` | `422` | Đối soát lệch khác `0` | `Số liệu không khớp dữ liệu gốc (lệch {drift}). Không công bố báo cáo kỳ này.` |
| `eCommerce:Reporting:0004` | `400` | Publish khi chưa `Validated` | `Chỉ công bố được báo cáo đã qua đối soát.` |
| `eCommerce:Reporting:0005` | `409` | Run thay thế khi run cũ chưa `Published` | `Lần chạy thay thế phải được công bố trước.` |
| `eCommerce:Reporting:0006` | `400` | Huỷ ở trạng thái không cho phép | `Không thể huỷ lần chạy ở trạng thái {status}.` |
| `eCommerce:Reporting:0007` | `409` | Sửa/xoá run đã `Published` | `Lần chạy đã công bố là bất biến; hãy tạo lần chạy mới.` |
| `eCommerce:Reporting:0008` | `400` | Kỳ ngoài cửa sổ cho phép / ngày tương lai / `TenantId` rỗng | `Kỳ báo cáo {from}–{to} không hợp lệ.` |
| `eCommerce:Reporting:0009` | `409` | Yêu cầu lãi gộp khi thiếu giá vốn và `BatBuocCoGiaVon = true` | `Chưa có giá vốn nên không thể tính lãi gộp. Hãy cấu hình giá vốn (PRC-001).` |

---

## 7. Sự kiện, tích hợp ngoài & idempotency

### 7.1 Sự kiện **nhận vào** (consumer ETO — **tuỳ chọn**, tối ưu làn L2 về sau; D-178)

| ETO | Trạng thái trong code | Ai phát | Reporting dùng để | Chống trùng |
|---|---|---|---|---|
| `ChannelOrderPulledEto` | ✅ **có** — `modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:17–30` | ChannelHub | khởi tạo dòng kênh cho đơn sàn | `(TenantId, ChannelCode, ExternalOrderId)` là khoá nghiệp vụ; read-model **upsert** nên xử lý trùng là vô hại (BR-003) |
| `ChannelShopTokenRefreshedEto` | ✅ **có** — cùng file `:44–52` | ChannelHub | **không** dùng cho số liệu (chỉ vận hành) | — |
| `OrderPlacedEto` / `OrderStatusChangedEto` / `RefundIssuedEto` | ⬜ **chưa có trong code — kế hoạch GĐ1 (`SAL-002`/`SAL-003`/`SAL-006`)** | Sales | cập nhật doanh thu/đơn huỷ/hoàn | `AggregateVersion` theo `OrderId` (`XC-002` §1 A4) |
| `StockMovementRecordedEto` | ⬜ **chưa có trong code — kế hoạch GĐ1 (`INV-001`)** | Inventory | cập nhật tồn **trong ngày** | `(TenantId, WarehouseId, Sku, OccurredAtUtc)` + `IdempotencyKey` |
| `SettlementBatchImportedEto` | ⬜ **chưa có trong code — `REC-002` thuộc GĐ5** (`07` :704; `D-179`) | Reconciliation | cập nhật **phí sàn thực tế** | `(TenantId, BatchId)` |

> 🔒 **GĐ3 KHÔNG phụ thuộc ETO để có làn trong ngày (D-178):** L2 làm tươi dòng ngày hiện tại bằng **đúng một job** `D-172` (5 phút, lookback 3 ngày, ABP Setting) — không consumer ETO bắt buộc. Consumer ETO (khi GĐ1 có) là **tối ưu thay thế**, additive, 0 đổi bảng/DTO. Cờ L2 **TẮT** ⇒ báo cáo ghi **T+1**, **không** hiện "trong ngày" giả.

### 7.2 Sự kiện **phát ra**

| ETO | Phát khi | Ai tiêu thụ | Ghi chú |
|---|---|---|---|
| `ReportingRunPublishedEto` | run `Validated → Published` | Notifications (`NOT-*`), Vận hành | Payload **chỉ mang tham chiếu** (`RunId`, `Scope`, kỳ, `CorrelationId`) — **cấm** nhét số liệu vào ETO (> 100 KB ⇒ `eCommerce:Messaging:0007`, `XC-002` §1 E5). `ACC-001` **không** tiêu thụ ETO: đọc read-model qua hợp đồng (D-183) |
| `ReportingReconciliationFailedEto` | run `Computed → Failed` do lệch | Notifications, Vận hành | Kèm `DriftAmount`, `CheckKind` |

Phát qua **outbox trong cùng Unit of Work** (`XC-002` BR-001) và **có `CorrelationId`** (`XC-005` BR-003).

### 7.3 Gọi API **ra ngoài**

**Không áp dụng — lý do:** `REP-003` là báo cáo nội bộ; **không** gọi NCC, **không** gửi dữ liệu ra dịch vụ ngoài. Việc xuất file là **xuất file cục bộ** rồi kế toán tự dùng (giống cách `XC-006` §7.3 chốt cho Mẫu 01: *"xuất file Excel cho kế toán nộp, không gọi API cơ quan thuế"*).

### 7.4 ⚠️ Cảnh báo bảo mật & độ tin cậy của **nguồn dữ liệu** (kế thừa `SHP-002`/`SHP-003`/`SHP-004`)

Đây **không** phải rủi ro của riêng `REP-003`, nhưng `REP-003` **trực tiếp tiêu thụ** dữ liệu sinh ra từ các luồng đó ⇒ **phải** ghi rõ để không báo cáo số liệu "chắc chắn đúng" khi nguồn chưa chắc:

| # | Cảnh báo đã biết | Nguồn | Hệ quả cho `REP-003` |
|---|---|---|---|
| 1 | 🔴 **GHTK KHÔNG có HMAC** trên webhook — chỉ có `hash` trên URL ⇒ **có thể bị giả mạo**; phải dùng **IP allowlist + đối soát chủ động** | `docs/implementation-plan.md:199`, `:425`; `XC-002` §7.2 bảng biên vào | Cột COD/trạng thái giao hàng từ GHTK là **nguồn không xác thực mật mã** ⇒ báo cáo **phải** đánh cờ `CodUnreconciled = true` cho tới khi `REC-003` đối soát bằng file Excel theo "phiên". **Cấm** dùng số COD GHTK để kết luận doanh thu đã thu |
| 2 | 🟠 **GHN đổi COD cần OTP** ⇒ có **bước thủ công**, không tự động hoá 100 % | `docs/implementation-plan.md:199`, `:426` | Số COD sau khi đổi là **kết quả thao tác người** ⇒ cùng cờ `CodUnreconciled`; báo cáo **không** được coi là số cuối cùng cho kế toán |
| 3 | 🟠 **Đối soát COD không có API** — bảng kê là **file Excel tải thủ công** theo "phiên" | `docs/implementation-plan.md:200`, `:429` | `REP-003` **không** tự lấy được phí/COD thực tế; phải chờ `REC-003` import ⇒ BR-010 |
| 4 | 🟠 **Sàn TMĐT trả phí sàn/khuyến mãi theo settlement**, không theo đơn | `docs/implementation-plan.md:200` | `ChannelFeeEstimatedAmount` ≠ thực tế; chênh lệch là **bình thường**, không phải bug ⇒ UI phải hiện **cả hai** cột |

> 🔒 **Nguyên tắc rút ra:** *"Số liệu có nguồn chưa xác thực **phải** mang cờ tin cậy, không được trộn lẫn với số liệu đã đối soát."* Đây là lý do read-model có 3 cờ `CostMissing`, `SettlementPending`, `CodUnreconciled` — và UI **bắt buộc** hiển thị chúng (BR-013).

### 7.5 Idempotency ở **mọi biên vào**

| Biên vào | Idempotency key | Hàng rào DB | Ghi chú |
|---|---|---|---|
| Job tổng hợp (định kỳ) | `report:agg:{tenantId}:{scope}:{businessDateFrom}:{businessDateTo}:{version}` | **UNIQUE (filter)** `(TenantId, Scope, From, To) WHERE Status = Running` | `XC-002` BR-004: mọi job định kỳ **phải** idempotent |
| API chạy lại kỳ | `report:agg:{tenantId}:{scope}:{from}:{to}` + `Version` tăng | UNIQUE `(…, Version)` | Gọi 2 lần cùng tham số ⇒ lần 2 **trả run đã có** nếu đang `Running`/`Published` (không tạo trùng) |
| Consumer ETO (tối ưu L2 sau — D-178) | `{EventType}:{AggregateId}:{AggregateVersion}` (kế thừa `XC-002` BR-007) | **UNIQUE khoá nghiệp vụ** của dòng `Rpt*` + upsert | Xử lý trùng ⇒ **upsert lại cùng giá trị** ⇒ vô hại |
| Nhận sự kiện settlement (`REC-002`, chưa có) | `{BatchId}:{LineNo}` | UNIQUE khoá nghiệp vụ | ⬜ chưa có trong code |
| Webhook hãng vận chuyển / callback | **Không thuộc `REP-003`** — thuộc `SHP-002`/`PaymentHub` | — | ⚠️ `REP-003` **chỉ tiêu thụ** kết quả; nhưng **phải** giữ cờ tin cậy (§7.4) |
| Xuất cho `ACC-001` | **Không có** khoá xuất của `REP-003` | Ghi vết + `SHA-256` ở `ACC-001` (`AccExportRuns`/`AccExportArtifacts` append-only) | `ACC-001` đọc read-model qua hợp đồng **chỉ đọc** `IReportFactReader` (in-process, không endpoint/quyền mới); cùng `RunId` ⇒ file + hash giống hệt (D-183) |

---

## 8. Bất biến & xử lý đồng thời

### 8.1 Bất biến

| # | Bất biến | Cơ chế bảo vệ |
|---|---|---|
| 1 | Đường **đọc** báo cáo **không** chạm bảng giao dịch | Kiến trúc module + **test quét mã nguồn** `TC-02` — quét theo **(lớp, method)** allow-list, không theo thư mục `modules/Reporting/**` (D-184; công cụ drift ở tầng tổng hợp, **không** mở rộng allow-list `D-175`) |
| 2 | Mỗi dòng read-model có **đúng một** bản ghi cho một khoá nghiệp vụ | **UNIQUE index** (4 bảng ở §5.2) |
| 3 | Chạy lại cùng kỳ ⇒ kết quả **giống hệt** | Upsert xác định + **test hash** `TC-09` (NFR-011) |
| 4 | **Không** publish số liệu lệch nguồn | Guard `Computed → Validated` **và** `CHECK (Status <> Published OR DriftAmount = 0 AND DriftQuantity = 0)` ở DB |
| 5 | `RptAggregationRuns` là **append-only**; **nội dung** run `Published` bất biến (chỉ cho **một** chuyển `Published → Superseded`) | Không có đường UPDATE nội dung/DELETE nghiệp vụ + `TC-07` |
| 6 | **Tối đa một** run `Running` cho cùng `(tenant, scope, kỳ)` | Partial **UNIQUE index** `WHERE Status = Running` (D7) — **không** chỉ dựa vào Redis lock |
| 7 | Tiền **luôn** là `decimal`; **không** `double`/`float`; **không** so `==` | Test reflection `TC-11` (kế thừa `XC-001` TC-11) + review |
| 8 | Thiếu dữ liệu ⇒ **cờ + `NULL`**, không bao giờ `0` | `CostMissing` / `SettlementPending` / `CodUnreconciled` + `TC-13` |
| 9 | Cách ly tenant ở **mọi** đường đọc kể cả xuất file | `IMultiTenant` + filter của ABP + `TC-12` (2 tenant) |
| 10 | Metric phát ra **nằm trong** registry | `eCommerceMetricNames.All` + test `TC-06` hiện có (`test/eCommerce.Tests/Observability/MetricsTests.cs`) |

### 8.2 Xử lý đồng cấp (concurrency)

| # | Tình huống | Cơ chế |
|---|---|---|
| 1 | 2 instance cùng chạy job cho cùng kỳ | Khoá `report:tong-hop` (`TongHopBaoCaoService.TenKhoaPhanTan`, D-178) **+** partial unique index `WHERE Status = Running` ⇒ instance thứ hai **ném `0001`** (hoặc `SkipIfBusy` nếu là job định kỳ — `TC-06`) |
| 2 | Job chạy lâu hơn TTL khoá | TTL **1800 s** > thời gian tối đa ước tính (NFR-002: < 5 phút) ⇒ an toàn; **vẫn** có metric `lock_ttl_exceeded_total{module=report}` để phát hiện (`XC-003` NFR-007) |
| 3 | Vận hành bấm **chạy lại** đúng lúc job đêm đang chạy | Cùng cơ chế #1; API ở chế độ `ThrowOnTimeout` ⇒ trả **`409` (`0001`)** cho người dùng, **không** xếp hàng chờ |
| 4 | L2 (job `D-172`) chạy **đồng thời** với job batch (L1/L3) cho cùng ngày | 🔒 **Cấm** 2 đường ghi cùng lúc: L2/L3 dùng **cùng** khoá `TongHopBaoCaoService.TenKhoaPhanTan` (job) nên không chồng nhau; **khi** có consumer ETO (tối ưu sau, D-178) ⇒ khoá theo dòng `report:channel:{tenantId}:{channelCode}:{channelShopId}:{businessDate}` + **từ chối** sự kiện cũ hơn `BusinessDate` đã publish |
| 5 | Sự kiện đến **sai thứ tự** / đến muộn | So `AggregateVersion`/`OccurredAtUtc`; bản cũ hơn ⇒ **bỏ qua có log + metric** (`XC-002` §1 A4); L3 sẽ sửa trôi trong đêm |
| 6 | Redis chết | Khoá **vô hiệu** nhưng partial unique index **vẫn chặn** ⇒ không có 2 run `Running`; app vẫn chạy (fail-open theo `XC-003` D2) |
| 7 | 2 người cùng bấm **export** | Đọc-only ⇒ không tranh chấp; chỉ giới hạn tần suất ở tầng API (rate limit) nếu cần |

**Khoá phân tán dùng trong feature (bảng đăng ký bổ sung cho `XC-003` §5 — ⚠️ phải sửa `XC-003` trước khi code):**

| Khoá (mẫu) | Hàm sinh khoá | Dùng cho | TTL đề xuất | Timeout | Chế độ khi bận |
|---|---|---|---|---|---|
| `report:tong-hop` (as-built `TongHopBaoCaoService.TenKhoaPhanTan:44`; ABP thêm tiền tố `ecommerce:`) | `TongHopBaoCaoService.TenKhoaPhanTan` | job tổng hợp L1/L2/L3 **và** công cụ drift (D-184) dùng **chung** một khoá | **1800 s** | 30 s (job) / chờ ngắn (drift) | `SkipIfBusy` (job) · bận ⇒ `eCommerce:Lock:0001` (drift — D-184, 0 mã lỗi mới) |
| `report:turnover:{tenantId}:{periodStart}` | `ReportingLockKeys.TurnoverRollup` | tổng hợp vòng quay theo kỳ | **1800 s** | 30 s | `SkipIfBusy` |
| `report:channel:{tenantId}:{channelCode}:{channelShopId}:{businessDate}` | `ReportingLockKeys.ChannelDay` | **khi** có consumer ETO (tối ưu sau, D-178): áp sự kiện cho một (kênh, shop, ngày) | **300 s** | 10 s | `SkipIfBusy` |
| `report:export:{tenantId}:{reportKind}:{from}:{to}` | `ReportingLockKeys.Export` | chống xuất trùng file lớn | **600 s** | 10 s | `SkipIfBusy` + trả file đã tạo nếu còn |

> 📌 Đoạn `<module>` = **`report`** (tên miền ngắn) theo `XC-003` D5. ⚠️ **Phải bổ sung 4 dòng này vào bảng đăng ký khoá của `XC-003` §5** trước khi code (quy tắc vàng: *"sửa tài liệu trước"* — `docs/design-doc-guidelines.md:102`).
>
> 📌 **Tiền tố khoá:** đã kiểm 24/11/2026 — cả `eCommerceConsts.LockKeys.InventorySku` (`eCommerceConsts.cs:816–817`), `InventoryLockKeys.TheoSku` (`InventoryLockKeys.cs:16–17`) và `ChannelHubConsts.LockKeys.*` (`ChannelHubConsts.cs:22–31`) trả tên **KHÔNG** có tiền tố `ecommerce:` (`AbpDistributedLock` tự thêm — `XC-003` §5.4) ⇒ 4 khoá `report:*` bên trên viết **không** tiền tố.

---

## 9. Phân quyền

Áp dụng nguyên tắc đã kiểm chứng ở Phase 0: **`[Authorize]` ở cấp class** (`src/eCommerce.Application/Channels/ChannelAppService.cs:21–28`) và **mọi permission phải khai báo tập trung**, nếu không ABP **luôn từ chối** ⇒ API trả `403` dù user có role admin (`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:5–12`).

| Permission | Ai | Việc | Ghi chú |
|---|---|---|---|
| `eCommerce.Reporting.InventoryTurnover.View` | Quản lý kho, vận hành, kế toán | xem vòng quay/ngày tồn/aging/hàng chậm | đọc-only |
| `eCommerce.Reporting.ChannelPerformance.View` | Quản lý kênh, kinh doanh, kế toán trưởng | xem hiệu suất kênh | ⚠️ chứa **phí sàn & lãi gộp** ⇒ hạn chế hơn nhóm kho |
| `eCommerce.Reporting.Aggregation.Manage` | **Vận hành kỹ thuật** (không phải người dùng nghiệp vụ) | chạy lại kỳ, huỷ run, xem đối soát | 🔒 Thao tác này **ghi** read-model ⇒ cần audit (ABP audit log đã bật) |
| `eCommerce.Reporting.Aggregation.Inspect` | **Admin tenant mặc định** (không cấp cho role nào khác) | xem **drift** read-model ↔ nguồn (chỉ đọc) | 🔒 **Không** tái dùng `.Manage` (`.Manage` = ghi); audit = ABP audit log (D-184) |
| `eCommerce.Reporting.Export` | **Kế toán trưởng** + quản lý kênh | xuất Excel/CSV cho `ACC-001` | 🔒 File xuất chứa **doanh thu, phí, lãi gộp** — dữ liệu nhạy cảm về kinh doanh |

**Khai báo bổ sung (bắt buộc, cùng PR với code):**

```csharp
// src/eCommerce.Domain.Shared/eCommerceConsts.cs — thêm khối Reporting
public static class Reporting
{
    public const string Default = GroupName + ".Reporting";
    public const string InventoryTurnover  = Default + ".InventoryTurnover";   // .View
    public const string ChannelPerformance = Default + ".ChannelPerformance";  // .View
    public const string Aggregation        = Default + ".Aggregation";         // .Manage
    public const string AggregationInspect = Aggregation + ".Inspect";         // chỉ đọc drift (D-184)
    public const string Export             = Default + ".Export";
}
// + AddChild tương ứng trong eCommercePermissionDefinitionProvider.Define(...)
//   (theo đúng khuôn khối "Thuế (XC-006)" ở file đó, dòng 50–59)
```

**Setting theo tenant (có audit khi đổi — kế thừa `XC-001` BR-004):**

| Setting | Kiểu | Mặc định | Ai đổi |
|---|---|---|---|
| `eCommerce.Reporting.MovementClassThresholds` | chuỗi ngưỡng ngày tồn (`30,90,180`) | `30,90,180` | Kế toán trưởng |
| `eCommerce.Reporting.DetailRetentionMonths` | `int` | `0` = **TẮT** (giữ vô hạn; `24` là **đề xuất**, chờ kế toán — D-182) | Host admin — `<= 0` không xoá, âm/không parse ⇒ **NÉM**; job dọn hằng tháng **dry-run mặc định** |
| `eCommerce.Reporting.ChannelIntradayDelayMinutes` | `int` | `5` (trần hiệu lực **< 15**; `<= 0`/không đọc được ⇒ `5`; D-178) | Vận hành — ⚠️ chỗ lệch as-built phải sửa khi code: `ReportingOptions.NhipJobPhut = 15` (`ReportingOptions.cs:18`) và `ReportingAggregationWorker.NhipMacDinhPhut = 15` (`:31`) |
| `Reporting:PhuongPhapGiaVon` (do module **sở hữu giá vốn** đặt — Pricing/`PRC-001`) | `string` | `BinhQuanGiaQuyenDiDong` | Kế toán trưởng — v1 chỉ **một** giá trị hợp lệ; giá trị khác (`FIFO`…) ⇒ job **NÉM** khi khởi động (fail-closed, D-177) |

---

## 10. UI Mockup & mapping dữ liệu

**Menu Reporting** (thuộc `WEB-001` — ⚠️ **project `eCommerce.Web` chưa tồn tại trong `src/`**): 3 màn hình + 1 màn hình vận hành.

| Vùng UI | Thành phần | Dữ liệu | Nguồn | Quy tắc hiển thị |
|---|---|---|---|---|
| **Tồn kho luân chuyển** | bảng + bộ lọc (kỳ, kho, danh mục, phân loại vòng quay) | SKU, tồn đầu/cuối, tồn bình quân, giá vốn, **vòng quay**, **ngày tồn kho**, `MovementClass`, `CoveredDays` | `RptInventoryTurnover` | Nhãn **độ trễ T+1**; `CostMissing` ⇒ cột giá vốn/vòng quay hiện **"— chưa có giá vốn"** (không hiện `0`); `CoveredDays < SốNgàyKỳ` ⇒ banner *"thiếu dữ liệu n ngày trong kỳ"*, **không** nội suy (D-176) |
| **Tồn kho theo ngày** | biểu đồ đường + bảng | `ClosingOnHand`, `ClosingReserved`, `ClosingAvailable` | `RptInventoryDailySnapshots` | Trục ngày theo **giờ VN**; tooltip ghi `LastAggregatedAtUtc` |
| **Hàng chậm / hàng chết** | bảng xếp hạng | SKU, `DaysOfInventory`, giá trị tồn | `RptInventoryTurnover` | Sắp theo **giá trị tồn giảm dần** (không theo số lượng — 1 SKU rẻ tồn nhiều không quan trọng bằng 1 SKU đắt tồn ít) |
| **Hiệu suất kênh** | bảng + so sánh | đơn gộp/huỷ/hoàn, SL bán ròng, doanh thu gộp, giảm giá, thuế, phí ship thu, **phí sàn dự kiến**, **phí sàn thực tế**, doanh thu ròng, AOV, **lãi gộp 1**, **lãi gộp 2** | `RptChannelDailyPerformance` | 2 cột phí sàn **luôn hiện cạnh nhau**; cột thực tế `NULL` ⇒ hiện **"—"** + nhãn *"chờ settlement (REC-002)"*, **không** ẩn cột, **không** hiện `0`; hai cột lãi gộp **luôn cạnh nhau** (nhãn *"Lãi gộp 1 (trước phí)"* / *"Lãi gộp 2 (sau phí)"*), `SettlementPending` ⇒ LG2 nhãn *"tạm tính"*; `UnclassifiedTaxSkuCount > 0` ⇒ **cảnh báo vàng**; `CodUnreconciled` ⇒ nhãn *"COD chưa đối soát"* (D-179, D-181) |
| **Xu hướng kênh** | biểu đồ nhiều chuỗi | chuỗi doanh thu ròng / lãi gộp (LG2) theo ngày/tuần/tháng | `RptChannelDailyPerformance` | Ngày **trong ngày hiện tại** vẽ **nét đứt** = tạm tính (BR-013). L2 **TẮT** ⇒ ghi **T+1**, **không** vẽ nét đứt (D-178) |
| **Vận hành tổng hợp** | bảng lần chạy + nút "chạy lại", "huỷ" | `Scope`, kỳ, `Version`, `Status`, `SourceRowCount`/`WrittenRowCount`, `DriftAmount`, `ErrorCode`, `CorrelationId` | `RptAggregationRuns` + `RptReconciliations` | 🔒 chỉ hiện cho người có `Aggregation.Manage`; nút "chạy lại" cảnh báo *"sẽ tạo lần chạy mới, lần chạy cũ chuyển Superseded"* |
| Mọi màn hình lỗi | `CorrelationId` | response header | `XC-005` §10 | hiện mã để người dùng gửi hỗ trợ |

**Ba điểm bắt buộc ghi rõ trên UI (BR-013 — kế thừa thói quen tốt của tài liệu gốc):**

1. **Độ trễ dữ liệu:** *"Số liệu tồn kho tính đến hết ngày {date}; số liệu kênh cập nhật đến {hh:mm}"*.
2. **Chênh lệch đếm ↔ số dòng:** khi có bộ lọc, hiện *"Hiển thị {n}/{total} dòng (đã lọc)"* — **không** để người dùng tưởng `total` là số sau lọc.
3. **Kết quả rỗng không phải lỗi:** kỳ chưa có dữ liệu ⇒ hiện **hướng dẫn** *"kỳ này chưa được tổng hợp — xem Vận hành tổng hợp"* + trạng thái run gần nhất; **không** hiện bảng trống, **không** trả `500`.

---

## 11. Migration & seed

| Mục | Nội dung |
|---|---|
| **Migration** | ⬜ **chưa có trong code** — kế hoạch GĐ3. Tên đề xuất: `Add_Reporting_ReadModel` (theo khuôn `20261013012648_Add_Tax_Module`) |
| **Bảng tạo mới** | `RptInventoryDailySnapshots`, `RptInventoryTurnover`, `RptChannelDailyPerformance`, `RptAggregationRuns`, `RptReconciliations` + index/CHECK ở §5.2 |
| **DbSet bắt buộc** | 🔒 **Phải** khai báo `DbSet` cho **cả 5 entity** trên DbContext hợp nhất `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs`, nếu không `IRepository<T>` **không được đăng ký** và runtime báo *"The requested service 'IRepository<…, Guid>' has not been registered."* (khuôn: khối `DbSet` của chính file đó) |
| **Mapping theo khuôn module** | `ReportingDbContextModelCreatingExtensions.ConfigureReporting(this ModelBuilder)` **ĐÃ CÓ** (`modules/Reporting/eCommerce.Reporting.EntityFrameworkCore/ReportingDbContextModelCreatingExtensions.cs:22`) và **đã được gọi** tại `eCommerceDbContext.cs:448` ⇒ chỉ **bổ sung mapping** cho 5 bảng mới vào extension hiện có; **không** thêm lời gọi trùng trong `OnModelCreating` |
| **Partial unique index** | Viết bằng `HasFilter("\"Status\" = 1")` (EF Core) — **đã dùng đúng kỹ thuật này** ở `eCommerceDbContext.cs:434` (`IX_MsgDeadLetters_TenantId_ChuaXuLy`) |
| **Seed** | • **Không** seed số liệu (read-model sinh từ dữ liệu thật).<br>• Seed **Setting** mặc định (§9) — theo khuôn seed idempotent (`XC-006` §11).<br>• Seed **1 run "khởi tạo"** cho tenant demo **chỉ ở môi trường dev** (đánh dấu rõ) |
| **Backfill** | Job backfill chạy theo **cửa sổ ngày** từ ngày có dữ liệu đầu tiên, **không** chạy một lần toàn bộ lịch sử (NFR-005). ⚠️ Nếu chạy backfill, **phải** log + metric + chạy ngoài giờ cao điểm |
| **Ảnh hưởng dữ liệu cũ** | **Không** sửa bảng giao dịch (read-model chỉ đọc) ⇒ **0 rủi ro** cho dữ liệu đang chạy |
| **Rollback** | Xoá 5 bảng `Rpt*` + `DELETE` migration. **Không** mất dữ liệu nghiệp vụ (read-model dựng lại được từ nguồn). ⚠️ **Mất lịch sử lần chạy/đối soát** ⇒ nếu đã xuất file cho `ACC-001` thì **phải** giữ lại file + hash ở phía `ACC-001` |
| **Job dọn dữ liệu** | Chỉ xoá chi tiết ngày **khi** `DetailRetentionMonths > 0` (**mặc định `0` = TẮT**, giữ vô hạn; `24` chỉ là **đề xuất** chờ kế toán — D-182); job hằng tháng **dry-run mặc định** (đếm + log + metric), `<= 0` không xoá, âm/không parse ⇒ **NÉM**, so sánh `<` mốc (đúng mốc ⇒ giữ); **cấm** `DELETE` bảng tổng hợp tháng và **không** áp lên tài liệu nguồn kế toán |

---

## 12. Xử lý lỗi & trường hợp biên

| Tình huống | Nguyên nhân | Hành vi | Phản hồi |
|---|---|---|---|
| `TenantId` **rỗng** (host) | thiếu ngữ cảnh tenant | **Ném** `0008` — **cấm** báo cáo xuyên tenant | `400` |
| Kỳ báo cáo **ở tương lai** | người dùng chọn sai | **Ném** `0008` — **cấm** tổng hợp ngày chưa xảy ra | `400` |
| Kỳ **chưa có run Published** | chưa tổng hợp / kỳ quá mới | Trả **danh sách rỗng** + `latestRunStatus`; UI hiện hướng dẫn. **Không** phải lỗi | `200` |
| **Không có chuyển động kho** trong kỳ | SKU không phát sinh | Dòng có `ReceiptQty=…=0`, `ClosingQty = OpeningQty`; `MovementClass = Dead` nếu tồn > 0. **Không** phải lỗi | `200` |
| **Thiếu giá vốn** | `PrcCostPrices` không có dòng `DaDuyet = true` có hiệu lực tại **ngày bán** | `CostMissing = true`; `CogsAmount`/`TurnoverRatio`/`DaysOfInventory`/`GrossMargin1Amount`/`GrossMargin2Amount` = **NULL**; UI hiện nhãn. `BatBuocCoGiaVon = true` ⇒ **`0009`** (giá vốn `0` **hợp lệ**, không phải thiếu — D-177) | `200` / `409` |
| **Thiếu phí sàn thực tế** | `REC-002` (GĐ5) chưa có | Kênh **sàn** (`Channel.Type = 1`): `ChannelFeeActualAmount = NULL`, `SettlementPending = true`; kênh **web/offline**: cả hai cột phí `NULL` + `SettlementPending = false`; hiện **hai** cột cạnh nhau (D-179) | `200` |
| **Cấu hình phương pháp giá vốn khác** | Setting `Reporting:PhuongPhapGiaVon` ≠ `BinhQuanGiaQuyenDiDong` | Job **NÉM** khi khởi động (fail-closed), **không** rơi về mặc định (D-177) | job lỗi |
| **Làn L2 tắt** | chưa làm kịp / đo tải vượt trần < 15 phút | Báo cáo ghi **T+1 trung thực**, **0** nhãn "trong ngày" (D-178) | `200` |
| **COD chưa đối soát** | ⚠️ GHTK không có HMAC; GHN đổi COD cần OTP; `REC-003` chưa có | `CodUnreconciled = true`; báo cáo **không** dùng số COD để kết luận doanh thu | `200` + cờ |
| **SKU chưa phân loại thuế** trong kỳ | `XC-006` Q2 đã chốt: vẫn **cho bán**, **chặn hoá đơn** | `UnclassifiedTaxSkuCount > 0` ⇒ cảnh báo **vàng**; gauge `tax_unclassified_skus` đã có sẵn (`eCommerceMetrics.cs:125`) và **phải** được cập nhật | `200` + cảnh báo |
| **Đối soát lệch** | lỗi tổng hợp / sự kiện trôi / nguồn đổi | Run `Failed` + `DriftAmount`; **không publish**; cảnh báo kỹ thuật | `422` khi gọi API chạy lại |
| **Ghi read-model lỗi giữa chừng** | DB timeout / deadlock | UoW **rollback**; run `Failed`; dữ liệu của run **Published** trước đó **còn nguyên** | `500` |
| **Redis chết** | hạ tầng | Khoá vô hiệu nhưng **partial unique index** vẫn chặn 2 run `Running`; job log `Error` + tiếp tục | bình thường |
| **Khoá đang bận (job định kỳ)** | instance khác đang chạy | `SkipIfBusy`: bỏ qua nhịp, log `Information` + metric `lock_skipped_total{module=report}` | không lỗi |
| **Khoá đang bận (API chạy lại)** | người dùng bấm khi job đang chạy | **Ném** `0001` | `409` |
| **Run đã `Published` mà bị sửa** | lập trình sai | **Ném** `0007`; DB cũng chặn nếu qua đường ghi | `409` |
| **Sự kiện L2 đến muộn hơn ngày đã publish** | outbox trễ / retry | **Bỏ qua có log + metric**; L3 sửa trôi trong đêm | không lỗi |
| **Xuất file trong khi kỳ đang được tổng hợp lại** | đua giữa export và recompute | Xuất bám **`RunId`** đã chọn (snapshot đọc nhất quán 1 lần); nếu run đó bị `Superseded` ⇒ ghi chú trong file *"run {id} đã bị thay thế bởi {newId}"* | `200` |

**Chiến lược transaction:**

- Mỗi **batch ghi** nằm trong **một Unit of Work** của ABP; **outbox ghi cùng transaction** (`XC-002` BR-001).
- Phạm vi **khoá phân tán bao ngoài** UoW (`await using`), **không** nằm trong transaction ⇒ khoá được nhả **sau** khi transaction kết thúc (`XC-003` §12).
- **Không** dùng một transaction cho **toàn bộ** kỳ (quá dài ⇒ giữ lock DB lâu); thay vào đó: run row + upsert idempotent theo batch ⇒ **lỗi giữa chừng vẫn dừng được và chạy lại được** (D5).
- **Đọc** nguồn dùng **read-only** (không `SaveChanges`) và **cửa sổ ngày bắt buộc** (`ReportingFactQuery`).

---

## 13. Phi chức năng

| Mã | Yêu cầu | Ngưỡng đo được | Cách kiểm chứng |
|---|---|---|---|
| NFR-REP-003-001 | Độ trễ truy vấn báo cáo | **p95 < 1 s** (kỳ 30 ngày × 50.000 SKU) | benchmark trên dữ liệu seed lớn |
| NFR-REP-003-002 | Thời gian job tổng hợp | 1 ngày × 100.000 dòng ledger **< 5 phút** | integration test (Testcontainers.PostgreSql) |
| NFR-REP-003-003 | Độ trễ read-model | tồn kho **T+1** xong trước **05:00** giờ VN; kênh **< 15 phút** (L2 = một job `D-172`, mặc định 5 phút) | metric `reporting_readmodel_lag_seconds` + log |
| NFR-REP-003-004 | Đối soát | lệch tiền **0 đồng**; lệch số lượng **0 đơn vị** | `TC-08` + `RptReconciliations` |
| NFR-REP-003-005 | Không ảnh hưởng DB giao dịch | **0** truy vấn > 30 s; job **nặng (L1/L3)** chỉ chạy **01:00–05:00**; làn L2 (5 phút/lookback 3 ngày) **miễn trừ** cửa sổ (không nới ngưỡng đo — D-178) | log thời gian từng batch + cảnh báo |
| NFR-REP-003-006 | Xuất Excel | 12 tháng × 20 kênh **< 60 s** | benchmark |
| NFR-REP-003-007 | Retention & dung lượng | chi tiết ngày **mặc định KHÔNG xoá** (Setting `0`; `24` tháng là **đề xuất** chờ kế toán — D-182); read-model **≤ 20 %** DB giao dịch | job dọn + đo kích thước bảng |
| NFR-REP-003-008 | Cách ly tenant | **100 %** (A không thấy B, kể cả xuất file) | `TC-12` (2 tenant) |
| NFR-REP-003-009 | Không `double`/`float` | **0** trong assembly read-model | test reflection `TC-11` |
| NFR-REP-003-010 | Metric trong registry | **0** metric ngoài `eCommerceMetricNames.All`; **0** nhãn cardinality cao | test `TC-06`/`TC-07` hiện có **vẫn xanh** |
| NFR-REP-003-011 | Idempotent khi chạy lại | chạy lại 1 kỳ **10 lần** ⇒ hash read-model **không đổi** | `TC-09` |
| NFR-REP-003-012 | Log khi hệ thống rỗi | **< 10 dòng/phút** | kế thừa `XC-005` `TC-09` |

**Metric mới đề xuất (⚠️ bắt buộc đăng ký ở 2 nơi — D10):**

| Tên metric | Loại | Nhãn (từ `eCommerceMetricTags.Allowed`) | Ý nghĩa / ngưỡng |
|---|---|---|---|
| `reporting_readmodel_lag_seconds` | Gauge | — | Độ trễ của read-model so với hiện tại; **> 3600 s (tồn kho) / > 900 s (kênh) ⇒ cảnh báo** |
| `reporting_aggregation_failed_total` | Counter | `reason` | Số lần run `Failed`; **> 0 ⇒ cảnh báo ngay** (không có số liệu mới = mù) |
| `reporting_snapshot_rows_pending` | Gauge | — | Số dòng read-model **chưa** qua đối soát; kỳ vọng **0** sau 05:00 |

> 🔒 **Ba tên trên ĐÃ tuân quy ước hậu tố** đang bị test chặn: `_seconds` · `_total` · `_pending` (`test/eCommerce.Tests/Observability/MetricsTests.cs:111–127`). **Cấm** dùng nhãn `sku`/`orderId`/`lockKey` — chúng nằm trong danh sách **bị cấm** (`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:80–85`); cần truy vết theo SKU ⇒ dùng **log**.
>
> 📌 **Số liệu đã có, dùng lại (không tạo mới):** `lock_skipped_total` + `lock_timeout_total` + `lock_ttl_exceeded_total` (nhãn `module=report` — `eCommerceMetrics.cs:108–110`), `channel_sync_failed_total` (nhãn `channel`,`kind`), `tax_unclassified_skus` (gauge đã có setter `SetTaxUnclassifiedSkus` — `:125`), `http_request_duration_seconds`.

**Log bắt buộc (theo `XC-005` BR-011 — khởi động/dừng/lỗi, **không** log mỗi vòng):**

| Sự kiện | Mức | Trường |
|---|---|---|
| Job khởi động / dừng | `Information` | `RunId`, `Scope`, `TenantId`, `BusinessDateFrom/To`, `CorrelationId`, `DurationMs` |
| Bỏ qua nhịp do khoá bận | `Information` | `LockKey`, `CorrelationId`, `module=report` |
| Run `Failed` / đối soát lệch | `Error` | `RunId`, `ErrorCode`, `DriftAmount`, `CheckKind`, `CorrelationId` |
| Sự kiện L2 đến muộn bị bỏ qua | `Debug`/`Warning` | `EventName`, `AggregateId`, `AggregateVersion` |
| 🔒 **Cấm** | — | log token/bí mật; log **mỗi vòng** của L2; log payload thô |

---

## 14. Test cases & Acceptance Criteria

### 14.1 Danh sách test case

| ID | Mục tiêu | Loại | Input chính | Expected | Phủ |
|---|---|---|---|---|---|
| **TC-01** | Báo cáo **chỉ** đọc read-model | Unit (quét mã nguồn) | quét app service Reporting tìm `IRepository<Order>`/`IRepository<StockLedgerEntry>`/`OrdOrders` | **0** match | **BR-001**, NFR-005 |
| **TC-02** | Không có SQL/nguồn giao dịch trong đường đọc | Unit (quét) | tìm `FromSqlRaw`/tên bảng `OrdOrders`,`InvStockLedger` trong Reporting | **0** match | **BR-001** |
| **TC-03** | Read-model **chỉ** ghi từ job | Unit (quét) | tìm `InsertAsync`/`UpdateAsync` trên `Rpt*` trong app service báo cáo | chỉ có trong job + `RptAggregationRuns` | **BR-002** |
| **TC-04** | **Chạy lại** cùng kỳ ⇒ **không nhân bản** | **Integration** | chạy job 2 lần cho cùng ngày | số dòng **không đổi**; `WrittenRowCount` lần 2 = lần 1 | **BR-003**, D5 |
| **TC-05** | **Upsert đúng khoá** khi tồn kho đổi | **Integration** | chạy job → thêm 1 bút toán → chạy lại | dòng **cập nhật**, không thêm dòng | **BR-003** |
| **TC-06** | **Khoá đang bận** ⇒ job bỏ qua + log + metric | Unit | giả lập khoá đã bị giữ | không ném; có log **và** `lock_skipped_total` | **BR-014**, `XC-003` A1 |
| **TC-07** | Sửa/xoá run `Published` ⇒ **ném** | **Integration** | gọi update run `Published` | `BusinessException` `eCommerce:Reporting:0007` | **BR-004**, **BR-005** |
| **TC-08** | **Đối soát lệch** ⇒ **không publish** | **Integration** | nguồn lệch 1 đồng so với read-model | run `Failed`; **0** dòng `Published`; `DriftAmount = 1` | **BR-008**, NFR-004 |
| **TC-09** | Chạy lại **10 lần** ⇒ hash không đổi | **Integration** | 10 lần cho cùng kỳ | hash read-model **giống hệt** | **BR-003**, NFR-011 |
| **TC-10** | Chuyển trạng thái **sai** ⇒ ném đúng mã | Unit | `Pending → Published`, `Computed → Published`, `Published → Cancelled` | `0004` / `0004` / `0006` | **BR-005** |
| **TC-11** | **0** `double`/`float` trong read-model | Unit (reflection) | duyệt assembly Reporting | **0** xuất hiện | **BR-006**, NFR-009 |
| **TC-12** | **Cách ly tenant** | **Integration** | tenant A và B cùng có dữ liệu | A **không** thấy dữ liệu B (kể cả export) | **BR-012**, NFR-008 |
| **TC-13** | **Thiếu giá vốn** ⇒ `NULL` + cờ, **không** `0` | Unit | SKU không có dòng `CostPrice` `DaDuyet = true` | `CogsAmount`/`TurnoverRatio`/`DaysOfInventory`/`GrossMargin1Amount`/`GrossMargin2Amount` **đều `null`** **và** `CostMissing == true` | **BR-009**, D9 |
| **TC-14** | `BatBuocCoGiaVon = true` + thiếu giá vốn ⇒ **`0009`** | Unit | tham số bắt buộc | `BusinessException` `eCommerce:Reporting:0009` | **BR-009** |
| **TC-15** | **Hai cột phí sàn** | Unit | đơn **kênh sàn** (`Channel.Type = 1`) có `ChannelFeeAmount`, chưa có settlement | `ChannelFeeEstimatedAmount != null`; `ChannelFeeActualAmount == null`; `SettlementPending == true` | **BR-010** |
| **TC-16** | Đơn **huỷ/hoàn** loại khỏi doanh thu ròng | Unit | 3 đơn: 1 `Completed`, 1 `Cancelled`, 1 `Returned` | `UnitsSold` và `NetRevenue` chỉ tính đơn `Completed`; `GrossOrders = 3` | **BR-011** |
| **TC-17** | Tổng = **tổng các dòng đã chốt** (không làm tròn lại) | Unit | 3 dòng có `TaxAmount` lẻ | tổng = Σ dòng; **khác** `Round(Σ chưa làm tròn)` nếu có ca lẻ | **BR-007** |
| **TC-18** | Đối chiếu **thuế** với dữ liệu đã chốt | **Integration** | kỳ có hoá đơn đã phát hành | Σ `TaxAmount` read-model == Σ `OrderLine.TaxAmount` nguồn (**0 đồng**) | **BR-008** (`XC-006` BR-008) |
| **TC-19** | Kỳ **rỗng** ⇒ rỗng, **không** `500` | **Integration** | kỳ không có đơn/biến động | `200` + danh sách rỗng + `latestRunStatus` | **BR-013** |
| **TC-20** | Kỳ **tương lai** / `TenantId` rỗng ⇒ `0008` | Unit | `ToDate = hôm nay + 1`; `TenantId = Guid.Empty` | `BusinessException` `eCommerce:Reporting:0008` | **BR-013**, §12 |
| **TC-21** | **Hai run `Running`** cùng kỳ bị **DB chặn** | **Integration** | chèn trực tiếp 2 run `Running` cùng khoá | `DbUpdateException` do partial unique index | **D7**, bất biến 6 |
| **TC-22** | Metric mới **nằm trong** registry | Unit | duyệt `eCommerceMetricNames.All` | 3 tên mới **có** trong registry; `TC-06`/`TC-07` hiện có **vẫn xanh** | **BR-014**, NFR-010 |
| **TC-23** | Metric mới **không** dùng nhãn bị cấm | Unit | so nhãn với `eCommerceMetricTags.Forbidden` | **0** giao nhau | **BR-014**, NFR-010 |
| **TC-24** | Export bám `RunId`; run bị `Superseded` ⇒ có ghi chú | **Integration** | export → chạy lại → export lại | file 2 có ghi chú run thay thế; số liệu khác file 1 | **BR-013**, D12 |
| **TC-25** | **`CoveredDays`** thiếu ngày ⇒ hiện rõ, không nội suy | Unit | kỳ 30 ngày nhưng chỉ có 20 snapshot | `CoveredDays = 20`; `DaysOfInventory` **không** được tính lại theo 30 | **BR-013** |
| **TC-26** | Cửa sổ đọc nguồn **bị chặn** | Unit | `ReportingFactQuery` thiếu `FromDate/ToDate` | ném lỗi tham số; **không** có truy vấn toàn bảng | NFR-005, **BR-001** |
| **TC-27** | Chuyển kho: **hai bút toán**, đọc **theo từng kho** | **Integration** | một phiếu chuyển q=5 từ kho A sang B, cùng một SKU | kho A `TransferOutQty=5`, `ClosingOnHand` giảm 5; kho B `TransferInQty=5`, `ClosingOnHand` tăng 5; **không** có dòng net | `D-170`, **BR-001** |
| **TC-28** | **Vòng quay 2 đầu kỳ** + `AverageQty = 0` ⇒ NULL | Unit (hàm thuần) | `Opening=100, Closing=0`; ca `AverageQty=0` | `AverageQty = 50` (**khác** bình quân ngày); `AverageQty=0` ⇒ `TurnoverRatio`/`DaysOfInventory` **NULL**, không chia 0; chạy job 2 lần ⇒ `TurnoverRatio` giống hệt | `D-176`, **BR-009** |
| **TC-29** | Cấu hình phương pháp giá vốn fail-closed + dòng chưa duyệt + giá vốn `0` | Unit | Setting `FIFO`/rác; `CostPrice.DaDuyet=false`; `Amount=0` | Job **NÉM** lúc khởi động (không rơi về mặc định); dòng chưa duyệt ⇒ `CostMissing = true` (không dùng giá trị đó); giá vốn `0` ⇒ **hợp lệ**, không `CostMissing` | `D-177`, **BR-009** |
| **TC-30** | Làn L2 = một job; Setting nhịp; cờ TẮT ⇒ T+1 | Integration | nhịp 1 phút; Setting `0`/rác; cờ TẮT | Dòng kênh hôm nay tươi ≤ 2 nhịp; `0`/rác ⇒ mặc định **5** (không 15); cờ TẮT ⇒ báo cáo T+1, **0** nhãn "trong ngày" | `D-178`, **BR-013** |
| **TC-31** | Kênh web/offline: hai cột phí `NULL` + `pending=false`; cấm ghi `0` | Integration | đơn kênh `web`; quét đường ghi | Cả hai cột phí `NULL`, `SettlementPending = false`; **0** đường ghi `0` vào `ChannelFeeActualAmount`; CHECK `SettlementPending OR ChannelFeeActualAmount IS NULL` xanh | `D-179`, **BR-010** |
| **TC-32** | Grain **ngày × kênh × shop** + tổng theo kênh | Integration | 2 shop cùng kênh/ngày; đơn `ChannelShopId = null`; chạy lại | **2** dòng; chạy lại vẫn 2 dòng, số không đổi; đơn null ⇒ dòng `ChannelShopId IS NULL`, **0** dòng `Guid.Empty`; `SUM` theo `ChannelCode` = tổng các shop | `D-180`, **BR-003** |
| **TC-33** | Hàm thuần `TinhLaiGop` (LG1/LG2) | Unit | `Net=100, Cogs=60, phí=5, ship=10`; `VatAmount≠0`; phí `NULL`; ship `NULL` | `LG1=40`, `LG2=25`; VAT không đổi LG1/LG2 + quét hàm không chứa `Tax`/`Vat`; phí `NULL` ⇒ `LG2=LG1−ship`; ship `NULL` ⇒ `LG2=LG1−phí`; `CostMissing` ⇒ cả hai `NULL` | `D-181`, **BR-009** |
| **TC-34** | Retention fail-closed + dry-run | Unit | Setting `0`; âm/rác; `N>0`; dòng tháng cũ hơn `N` | `0` ⇒ xoá **0** dòng; âm/rác ⇒ **NÉM**, 0 dòng xoá; `N>0` ⇒ chỉ chi tiết cũ hơn mốc, **đúng mốc ⇒ giữ**; dòng tổng hợp tháng **vẫn còn**; dry-run mặc định ⇒ 0 dòng xoá | `D-182` |
| **TC-35** | Ranh giới `ACC-001` ↔ `Reporting` | Unit (quét mã nguồn) | quét `modules/Accounting/**` và `modules/Reporting/**` | `ACC-001`: **0** `IRepository<Rpt*`, **0** `Reporting.Domain`/`OrdOrders`/`InvStockLedger`; `Reporting`: **0** `Accounting*`/`AccExport*`; cùng `RunId` ⇒ file + `SHA-256` giống hệt | `D-183`, **BR-001** |
| **TC-36** | Công cụ drift chỉ đọc | Integration | nguồn lệch 1 đồng; đếm trước/sau; quyền | `DriftValue = 1`; `RptAggregationRuns`/`RptReconciliations`/`RptWatermarks`/`Rpt*` **không đổi**; thiếu `.Inspect` (chỉ có `.Manage`) ⇒ **403**; job đang chạy ⇒ `eCommerce:Lock:0001` | `D-184`, **BR-001** |
| **TC-37** | `SaleQty` = 3 loại xuất bán/giao | Unit | cùng ngày/kho/SKU: `Sale=3`, `RetailSaleOut=2`, `ShippingOut=2` + nhiều loại xuất khác | `SaleQty = 7`; loại khác (`TransferIn/Out`, `SupplierReturnOut`, `OtherOut`, `StocktakeLoss`, `DamagedOut`, `WarrantyHold`, `Hold`) **không** đổi `SaleQty`; đơn online `Confirmed` không sinh thêm `ShippingOut`; `ClosingOnHand` **không** đổi | `D-185`, **BR-001** |

### 14.2 Fixture skeleton (xUnit)

```csharp
public class ReportingAggregationTests
{
    /// TC-04/TC-09 — chay lai PHAI idempotent (khoa nghiep vu + upsert).
    [Fact]
    public async Task TC09_chay_lai_10_lan_thi_hash_readmodel_khong_doi()
    {
        var sut = TaoJobVoiNguonCoDinh();

        await sut.RunAsync(Ngay, default);
        var hash1 = await BamHashReadModelAsync(Ngay);
        var soDong1 = await DemDongReadModelAsync(Ngay);

        for (var i = 0; i < 9; i++)
        {
            await sut.RunAsync(Ngay, default);
        }

        Assert.Equal(hash1, await BamHashReadModelAsync(Ngay));
        Assert.Equal(soDong1, await DemDongReadModelAsync(Ngay));   // KHONG nhan ban
    }

    /// TC-08 — doi soat lech thi KHONG duoc publish (BR-008).
    [Fact]
    public async Task TC08_doi_soat_lech_thi_khong_publish()
    {
        var sut = TaoJobVoiNguonLech(1m);       // lech dung 1 dong

        var run = await sut.RunAsync(Ngay, default);

        Assert.Equal(ReportingRunStatus.Failed, run.Status);
        Assert.Equal(1m, run.DriftAmount);
        Assert.Equal(0, await DemRunPublishedAsync(Ngay));
    }

    /// TC-10 — chuyen trang thai sai PHAI nem loi co ma (BR-005).
    [Fact]
    public void TC10_publish_khi_chua_validated_thi_nem_0004()
    {
        var run = TaoRun(ReportingRunStatus.Computed);

        var ex = Assert.Throws<BusinessException>(() => run.Publish());

        Assert.Equal("eCommerce:Reporting:0004", ex.Code);
    }

    /// TC-13 — thieu gia von thi la NULL + co, KHONG duoc ghi 0 (BR-009).
    [Fact]
    public void TC13_thieu_gia_von_thi_lai_gop_la_NULL()
    {
        var dto = TaoDtoKhongCoGiaVon();

        Assert.Null(dto.GrossMargin1Amount);
        Assert.Null(dto.GrossMargin2Amount);
        Assert.True(dto.CostMissing);
        // 0 la gia tri SAI, khong phai "khong biet" (D-181)
    }
}

public class ReportingMetricRegistryTests
{
    /// TC-22 — metric moi PHAI nam trong registry, neu khong TC-06 hien co se do.
    [Fact]
    public void TC22_metric_moi_cua_reporting_da_dang_ky()
    {
        string[] moi = ["reporting_readmodel_lag_seconds",
                        "reporting_aggregation_failed_total",
                        "reporting_snapshot_rows_pending"];

        foreach (var ten in moi)
        {
            Assert.Contains(ten, eCommerceMetricNames.All);
        }
    }

    /// TC-23 — cam nhan cardinality cao (BR-014).
    [Fact]
    public void TC23_nhan_metric_reporting_khong_cardinality_cao()
    {
        Assert.Empty(eCommerceMetricTags.Allowed
            .Intersect(eCommerceMetricTags.Forbidden, StringComparer.OrdinalIgnoreCase));
    }
}
```

### 14.3 Acceptance Criteria (Definition of Done)

- [ ] **37 test case** ở §14.1 pass, phủ **hết 14 BR** và **12 NFR**
- [ ] **TC-01/TC-02/TC-03 xanh** — chứng minh bằng **quét mã nguồn**: đường đọc báo cáo **không** chạm bảng giao dịch (đây là DoD của GĐ3: `docs/implementation-plan.md:213`)
- [ ] **TC-08 xanh** — chứng minh **không** công bố số liệu lệch nguồn (kèm `CHECK` ở DB)
- [ ] **TC-21 xanh** — partial unique index **chặn thật** ở PostgreSQL (⚠️ cần PostgreSQL thật — máy dev hiện **chưa có** Docker/PostgreSQL, xem `XC-006` §14.5)
- [ ] **TC-22/TC-23 xanh** và `MetricsTests` **hiện có vẫn xanh** (3 metric mới đã vào `eCommerceMetricNames` + `XC-005` §5.1)
- [ ] **Bảng đăng ký khoá của `XC-003` §5 đã bổ sung 4 khoá `report:*`** (sửa `XC-003` **trước** khi code — quy tắc vàng)
- [ ] **§15.2 F3** (thiếu `TaxRate`/`TaxClassificationId` trên `OrderLine`) **đã được xử lý hoặc có quyết định bằng văn bản**; ✅ **F5** (tiền tố khoá) đã kiểm 24/11 — không còn vướng mắc
- [ ] UI: **độ trễ**, **chênh lệch đếm ↔ số dòng**, **kết quả rỗng**, **3 cờ tin cậy** đều hiển thị đúng (BR-013, §10)
- [ ] Xuất file khớp số liệu (`ACC-001` đối chiếu lệch **0 đồng**)
- [ ] Retention: job dọn chạy thật **gated bởi kế toán duyệt hạn lưu trữ** (mặc định TẮT — D-182), đo dung lượng read-model (**≤ 20 %**)
- [ ] Tài liệu này cập nhật khớp code

### 14.4 Definition of Ready

- [x] ✅ Hiện trạng **đã kiểm bằng code thật**: `modules/Reporting/**` **đã có** fact doanh thu/tồn + `TongHopBaoCaoService` (D-176, D-183); **chưa có** `RptInventoryTurnover`/`RptChannelDailyPerformance`/`RptAggregationRuns`/`RptReconciliations`
- [x] ✅ **`REP-001`** (read-model & job tổng hợp) — **phải Approved TRƯỚC**; tài liệu này **giả định khung `Rpt*` + job base đã có**
- [ ] `XC-001` (`Money`) **đã xong** — ⚠️ **hiện CHƯA có trong code** (`grep` `Money|ToMinorUnits` = **0**) ⇒ **chặn** BR-006/BR-007
- [x] ✅ **Chốt công thức** vòng quay / ngày tồn kho / lãi gộp (§15.1 Q1, Q6 — D-176, D-181)
- [ ] **Chốt ngưỡng** `MovementClassThresholds` (mặc định `30,90,180` ngày) theo ngành hàng
- [x] ✅ **Chốt** làn L2 (trong ngày) ở GĐ3 = **một job** `D-172`, không consumer ETO bắt buộc (D-178)
- [x] ✅ **Chốt** `ChannelShopId` **nullable** + unique **`NULLS NOT DISTINCT`** (bỏ sentinel `Guid.Empty` — D-180)
- [ ] **Đã bổ sung 4 khoá `report:*`** vào bảng đăng ký `XC-003` §5
- [ ] **Đã chốt** vị trí adapter `IOrderFactReader`/`IInventoryFactReader` (tầng app ở GĐ3; chuyển về module Inventory/Sales khi tách)
- [ ] Nguồn giá vốn đã có (`PrcCostPrices` **đọc** đã có; cần **đường ghi/động cơ** ở **Pricing** (`PRC-001`) — D-177/D-186) **hoặc** chấp nhận phát hành bản `REP-003` **không có lãi gộp** (cờ `CostMissing`)

---

## 15. Câu hỏi mở, giả định & lịch sử thay đổi

### 15.1 Câu hỏi mở

| # | Câu hỏi | Cần ai | Trạng thái | Ảnh hưởng |
|---|---|---|---|---|
| **Q1** | **Công thức chốt** cho "tồn kho luân chuyển": vòng quay = `COGS / tồn bình quân` (bình quân 2 đầu kỳ) hay bình quân **theo ngày**? Ngày tồn kho = `SốNgàyKỳ / VòngQuay`? | **Kế toán trưởng + BA** | ✅ `Closed (D-176)` — tồn bình quân **2 đầu kỳ** `(OpeningQty+ClosingQty)/2`; `CoveredDays < SốNgàyKỳ` ⇒ banner, **không** nội suy | Sai công thức ⇒ **mọi kết luận nhập hàng sai**; và **không có cách phát hiện** vì cả hai cách đều "ra số" |
| **Q2** | **Giá vốn lấy từ đâu** và theo phương pháp nào (bình quân gia quyền di động / FIFO)? Giá vốn lấy **tại thời điểm bán** hay **giá vốn hiện tại**? | Kế toán trưởng | ✅ `Closed (D-177)` — **bình quân gia quyền di động**; giá vốn **tại ngày bán** (UTC+7) từ `PrcCostPrices` **`DaDuyet = true`** qua **một** method hợp đồng; thiếu ⇒ NULL + cờ; chủ động cơ = **Pricing** (`PRC-001`, **D-186**) | **Chặn** lãi gộp và vòng quay (BR-009 chỉ là giải pháp tạm) |
| **Q3** | Có cần làn **trong ngày** (< 15 phút) cho hiệu suất kênh ngay ở GĐ3, hay **T+1** là đủ? | BA + Vận hành | ✅ `Closed (D-178)` — giữ **< 15 phút**; L2 = **đúng một job** `D-172` (5 phút, lookback 3 ngày); cờ TẮT ⇒ T+1 trung thực | Ảnh hưởng khối lượng công việc; **không** còn phụ thuộc ETO GĐ1 |
| **Q4** | **Phí sàn thực tế** (settlement) lấy từ `REC-002`; GĐ3 đã có `REC-002` chưa? Nếu chưa, báo cáo chấp nhận chỉ có **dự kiến**? | Kiến trúc trưởng | ✅ `Closed (D-179)` — `REC-002` thuộc **GĐ5**; GĐ3 giữ phí dự kiến + cột thực tế **luôn NULL** + cờ `SettlementPending` (chỉ kênh sàn) | Ảnh hưởng cột lãi gộp kênh: LG2 mang nhãn *"tạm tính"*, không **âm giả** do bịa `0` |
| **Q5** | Báo cáo kênh cần chi tiết tới **từng `ChannelShop`** hay chỉ tới `ChannelCode`? | Quản lý kênh | ✅ `Closed (D-180)` — grain `(TenantId, BusinessDate, ChannelCode, ChannelShopId)`; `ChannelShopId` **nullable** + unique **`NULLS NOT DISTINCT`** (bỏ sentinel `Guid.Empty`); tổng kênh = `GROUP BY ChannelCode` + `SUM` | Ảnh hưởng unique index + số dòng |
| **Q6** | **Định nghĩa lãi gộp**: đã trừ **phí sàn**, **phí vận chuyển trả hãng**, **thuế** chưa? | **Kế toán trưởng** | ✅ `Closed (D-181)` — tách **2 chỉ tiêu** `GrossMargin1Amount`/`GrossMargin2Amount` (LG2 trừ phí sàn + `Order.ShippingFeeActual`); **không** trừ VAT lần hai; phí/ship `NULL` ⇒ không cấn trừ | **Cùng một từ "lãi gộp" nhưng hai con số khác nhau** ⇒ đây là loại mâu thuẫn hay gặp giữa kế toán và kinh doanh |
| **Q7** | **Retention**: chi tiết ngày 24 tháng có đủ cho giải trình thuế/kiểm toán, hay phải giữ **lâu hơn**? | Kế toán trưởng + Vận hành | ✅ `Closed (D-182)` — Setting `DetailRetentionMonths` mặc định **`0` = TẮT** (giữ vô hạn); `24` là **đề xuất** chờ kế toán; tháng giữ vô hạn; job dọn fail-closed + dry-run | Ảnh hưởng dung lượng + nghĩa vụ lưu trữ |
| **Q8** | `ACC-001` **tự lấy** read-model hay `REP-003` **xuất file** cho `ACC-001`? | Kiến trúc trưởng + Kế toán | ✅ `Closed (D-183)` — `ACC-001` **tự đọc** qua hợp đồng chỉ đọc `IReportFactReader` (in-process); file + `SHA-256` + `AccExportRuns`/`AccExportArtifacts` append-only ở `ACC-001`; `REP-003` **không** xuất artifact kế toán | Ảnh hưởng ranh giới 2 module và trách nhiệm lưu vết |
| **Q9** | Có cần **API/CLI so sánh read-model với nguồn** để tự động phát hiện trôi (ngoài job đêm)? | Vận hành | ✅ `Closed (D-184)` — **có**: `GET /api/app/reporting-aggregation/drift` + quyền riêng `eCommerce.Reporting.Aggregation.Inspect`; dùng **chung** hàm kiểm lệch với job; **0 ghi** `Rpt*`/run/watermark | Thuộc `REP-001`/`XC-005` |
| **Q10** | `SaleQty` gồm những `StockMovementType` nào — chỉ `Sale` + `RetailSaleOut`, hay gồm cả `ShippingOut` (hàng rời kho để giao)? | BA + Vận hành | ✅ `Closed (D-185)` — `{Sale=3, RetailSaleOut=19, ShippingOut=11}`; **không** `ABS`, **không** trừ trả lại; `ClosingOnHand` vẫn là tồn thật (mọi `MovementType`, áp **dấu** theo `ApplyMovement`) | Thiếu `ShippingOut` ⇒ `SaleQty` **đếm thiếu** đơn giao vận trong khi `ClosingOnHand` vẫn đúng ⇒ lệch **im lặng** giữa cột và tồn |
| **Q11** | **Chủ động cơ giá vốn** (đường ghi + định giá): **Pricing** (`PRC-001`) hay **`ACC-001`**? | **Kiến trúc trưởng + Kế toán trưởng** | ✅ `Closed (D-186)` — chủ = **Pricing (`PRC-001`)**: sở hữu bảng `PrcCostPrices` + đường ghi + động cơ **bình quân gia quyền di động** + duyệt (`CostPrice.Duyet`), **chỉ Pricing ghi**; `INV-001` chỉ cấp đơn giá nhập (`StockDocumentLine.UnitPrice` ↔ `UnitCost`, không rename — D-09); `ACC-001` chỉ tiêu thụ qua `IReportFactReader` (D-183) + một method additive của `ICostPriceAppService` (chỉ `DaDuyet = true`), không sở hữu/ghi ngược; động cơ đọc Inventory qua hợp đồng chỉ đọc ở `Inventory.Application.Contracts` | Chốt chủ ⇒ hết mâu thuẫn `inv-001`/`prc-001`; đường ghi/động cơ = WP GĐ3 trước lãi gộp `REP-002`; `REP-003`/`ACC-001` vẫn **đỏ cứng** §2.1 tới khi có code |

### 15.2 🚨 Phát hiện khi đọc code (làm cơ sở cho tài liệu này)

| # | Phát hiện | Bằng chứng | Mức độ | Xử lý |
|---|---|---|---|---|
| **F1** | **Module Reporting có một phần** (`WP-08`): fact doanh thu/tồn + job tổng hợp; **chưa có** bảng vòng quay/kênh/run/đối soát | Liệt kê `modules/Reporting/**`; `RptFactSalesLine`/`RptFactInventorySnapshot`/`RptWatermark` + `TongHopBaoCaoService` | 🟡 Thông tin | ✅ Phần còn lại của `REP-003` là **kế hoạch GĐ3** (D-176, D-183) |
| **F2** | **Read-model 2 báo cáo này chưa có**: không có bảng turnover/kênh | `modules/Reporting/**` chỉ có `RptFactSalesLine`/`RptFactInventorySnapshot`; `grep` `ChannelCode` trong `modules/Reporting` = **0** | 🟠 Trung bình | ⬜ Chờ WP code GĐ3 (D-178) |
| **F3** | 🚨 **`OrderLine` thiếu `TaxRate` và `TaxClassificationId`** dù `XC-006` D3 yêu cầu **chốt (snapshot) thuế suất** vào dòng đơn. Hiện chỉ có `TaxAmount` | `modules/Sales/eCommerce.Sales.Domain/Order.cs:808–840` (`OrderLine`; `TaxAmount` dòng `:838`, **không** có `TaxRate`/`TaxClassificationId`) vs `XC-006` §0.1 D3 (*"Thêm cột `TaxRate`, `TaxAmount`, `TaxClassificationId` trên dòng"*) | 🔴 **Cao** | ⬜ **Chặn BR-008**: không thể hiển thị "doanh thu theo từng mức thuế suất" từ read-model; đề xuất **thêm 2 cột** vào `OrderLine` ở GĐ1 (`SAL-002`) — cần sửa tài liệu `XC-006`/`SAL-002` **trước** |
| **F4** | 🚨 **Chưa có đường ghi/động cơ giá vốn** (bảng `PrcCostPrices` **đã có**, chỉ **đọc**) ⇒ **chưa thể** tính lãi gộp/vòng quay thật | `modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:36,57–68`; `modules/Pricing/eCommerce.Pricing.Application/CostPriceAppService.cs:24–27` (chỉ đọc) | 🔴 **Cao** | ⬜ BR-009 là **giải pháp tạm** (NULL + cờ). Phương pháp = bình quân gia quyền di động; đường ghi/động cơ thuộc **Pricing** (`PRC-001`, `D-186`), xếp WP GĐ3 **trước** lãi gộp `REP-002` (D-177) |
| **F5** | ✅ **Tiền tố khoá phân tán — ĐÃ KIỂM 24/11/2026, KHÔNG còn vướng mắc**: cả 3 nơi trả tên khoá **KHÔNG** có tiền tố `ecommerce:` | `eCommerceConsts.LockKeys.InventorySku` → `inventory:{warehouseId}:{sku}` (`eCommerceConsts.cs:816–817`); `InventoryLockKeys.TheoSku` → `inventory:{tenantId}:{warehouseId}:{sku}` (`InventoryLockKeys.cs:16–17`); `ChannelHubConsts.LockKeys.*` → `channel:…` (`ChannelHubConsts.cs:22–31`); tiền tố do `AbpDistributedLockOptions.KeyPrefix` cấp (`XC-003` §5.4) | 🟡 Thông tin | ✅ Khoá `report:*` của `REP-003` viết **không** tiền tố, khớp `XC-003` §5.4 (kiểm tĩnh bằng đọc code; không cần `redis-cli`) |
| **F6** | ✅ **`OrderStatusHistory` ĐÃ CÓ** (append-only, có mốc nghiệp vụ) ⇒ báo cáo **xác định được** thời điểm đơn chuyển `Cancelled`/`Returned` | `modules/Sales/eCommerce.Sales.Domain/OrderStatusHistory.cs` (`OccurredAtUtc` `:55`, ctor `:59–86`); `Order.Chuyen` ghi lịch sử (`Order.cs:654–668`); `SetStatus` **đã xoá** (`Order.cs:360`) | 🟡 Thông tin | ✅ Báo cáo kênh **có thể** quy doanh thu huỷ/hoàn về **ngày nghiệp vụ** bằng join lịch sử; rủi ro còn lại chỉ là chi phí join + quy đổi UTC → giờ VN |
| **F7** | **Chưa có settlement/phí sàn thực tế**: không có entity `Settlement*` nào | `grep` `Settlement` = **0** | 🟠 Trung bình | BR-010 là giải pháp tạm (2 cột + cờ). `REC-002` thuộc **GĐ5** (D-179) |
| **F8** | **Chưa có `Money`**: `XC-001` (tài liệu **Nền — phải xong TRƯỚC GĐ1**) **chưa được triển khai trong code** | `grep` `Money\|eCommerce.Kernel\|ToMinorUnits\|IRoundingPolicy` trong `*.cs` = **0** | 🔴 **Cao** (phụ thuộc) | 🔒 **REP-003 phụ thuộc `XC-001`**; nếu chưa có `Money` thì BR-006/BR-007 **không kiểm chứng được** ⇒ ghi vào DoR §14.4 |
| **F9** | **Chỉ có 1 app service trong toàn bộ solution** (`ChannelAppService`) ⇒ toàn bộ tầng ứng dụng của Reporting là **mới hoàn toàn** | `grep` `class .*AppService` = 1 kết quả; `[Authorize]` cấp class chỉ ở `ChannelAppService.cs:27` | 🟡 Thông tin | Dùng `ChannelAppService` làm **khuôn tham chiếu** cho `[Authorize]` cấp class + map thủ công (không dùng AutoMapper: `docs/implementation-plan.md:83`) |

### 15.3 Giả định

| # | Giả định | Nếu SAI thì sao |
|---|---|---|
| 1 | **Inventory & Orders vẫn nằm ở `src/`** ở GĐ3 (chưa tách module) ⇒ adapter `IOrderFactReader`/`IInventoryFactReader` đặt ở tầng app | Khi đã tách `modules/Inventory` + `modules/Sales` ⇒ **chuyển adapter về module đó**; interface **không đổi** (D3 không bị ảnh hưởng) |
| 2 | **Toàn hệ thống chỉ dùng VND** ở GĐ3 ⇒ mỗi dòng read-model có **một** tiền tệ | Bật đa tiền tệ ⇒ **không** được cộng các kênh khác tiền tệ vào một dòng; phải **tách dòng theo `Currency`** ⇒ đổi unique index (thêm `Currency`) |
| 3 | `BusinessDate` theo **giờ VN**, dữ liệu nguồn lưu **UTC** | Lệch múi giờ ⇒ đơn lúc 23:30 giờ VN rơi sang ngày hôm sau. Phải chốt `TimeZoneInfo`/`IClock` **một lần** ở `REP-001` |
| 4 | Dữ liệu nguồn **đủ tin cậy sau đối soát** (L3) | Nếu nguồn sai (webhook giả mạo — ⚠️ GHTK không có HMAC, §7.4) ⇒ báo cáo sai **dù read-model khớp nguồn**. Đối soát **không** phát hiện được loại sai này ⇒ **phải** có cờ tin cậy + đối soát với hãng/sàn bên ngoài (`REC-002`/`REC-003`) |
| 5 | Job **nặng** L1/L3 (01:00–05:00) **không** ảnh hưởng người dùng; làn L2 (5 phút) **miễn trừ** cửa sổ (D-178) | Nếu có đơn hàng lớn ban đêm ⇒ **phải** dời cửa sổ hoặc giới hạn tài nguyên (statement timeout, `max_parallel_workers`); nếu L2 đo tải vượt trần < 15 phút ⇒ **tắt cờ L2** (D-178) |
| 6 | `XC-005` metric phát qua `System.Diagnostics.Metrics` (**đã có** — `eCommerceMetrics.cs`) | Nếu chọn công cụ metric khác ⇒ chỉ đổi **tầng xuất**, không đổi **tên/nhãn** (`XC-005` Q1 đã chốt) |
| 7 | Số **lần chạy lại** một kỳ là **nhỏ** (vài lần/tháng) | Nếu vận hành chạy lại liên tục (do lỗi nguồn) ⇒ read-model bị **ghi đè nhiều** ⇒ cần **lịch sử giá trị** (thêm bảng version) — chi phí lớn, chỉ làm khi có yêu cầu thật |
| 8 | 5 bảng `Rpt*` **nằm cùng DB** với nghiệp vụ (modular monolith) | Khi tách service/DB ⇒ read-model phải **đồng bộ qua sự kiện** (`REP-001`); số liệu trở thành **eventually consistent** hoàn toàn ⇒ đổi §7.1 |

### 15.4 Lịch sử thay đổi

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.2 | 24/11/2026 | BA | Đối chiếu `D-176`–`D-185` (WP-14, N59): chốt công thức vòng quay 2 đầu kỳ; giá vốn bình quân di động theo ngày bán; L2 = **một job** (giữ < 15 phút); phí sàn thực tế NULL + `SettlementPending`; grain `ngày × kênh × shop` (nullable + NND); **tách LG1/LG2**; retention **mặc định TẮT**; `ACC-001` đọc qua `IReportFactReader`; công cụ drift chỉ đọc; `SaleQty` gồm `ShippingOut`. Q1–Q10 `Closed`, thêm **Q11** (chủ động cơ giá vốn) + **TC-27…TC-37** (11 TC) |
| v0.1 | 14/10/2026 | *(điền)* | Bản nháp đầu — chốt **read-model riêng + job tổng hợp + đối soát bắt buộc trước khi publish**; **14 BR · 12 NFR · 26 TC · 12 ADR**; phát hiện **F3** (`OrderLine` thiếu snapshot thuế suất), **F4** (không có giá vốn), **F5** (tiền tố khoá phân tán không nhất quán), **F8** (`Money`/`XC-001` chưa có trong code); ghi rõ **cảnh báo nguồn không xác thực** cho COD (GHTK không có HMAC · GHN cần OTP) và **3 cờ tin cậy** trên read-model |

## Đối chiếu mô hình Nhanh (24/11/2026)

| D-xx | Tài liệu cũ nói (§mục) | Đã sửa thành (§mục) | Code thật (file:dòng) / kế hoạch |
|---|---|---|---|
| `D-170` | §5.2: `TransferInQty`/`TransferOutQty` lấy từ `StockMovementType.Transfer` | §5.2: đếm **riêng** `TransferIn`/`TransferOut` theo từng kho; `Transfer` (=6) chỉ còn là legacy cho dữ liệu cũ | `src/eCommerce.Domain.Shared/Enums.cs:47,63,66`; `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:243–254` |
| `D-170` | §5.2: `ClosingOnHand = OpeningQty + Σ QuantityDelta` (cộng thô mọi loại) | §5.2: `ClosingOnHand = OpeningQty + Σ (dấu theo MovementType) × QuantityDelta`; cột là **độ lớn dương**; cấm dựng chỉ từ cột phân rã | `StockLedger.cs:103–180,254,257`; `StockPostingService.cs:184,187`; `TinhButToanChungTu.cs:10–13` |
| `D-31` | §5.2/§6.1: số lượng `int` (kể cả `UnitsSold`/`OrderFact.UnitsSold`) | §5.2/§6.1: `numeric(18,3)` cho snapshot, turnover, drift, **`UnitsSold`**, DTO | `StockLedger.cs:254,257`; `RptFactSalesLine.Quantity` `HasPrecision(18,3)` (`ReportingDbContextModelCreatingExtensions.cs:102`) |
| `D-27` | §5.2: `ClosingAvailable = OnHand − Reserved` | §5.2: trừ thêm `Transferring/Holding/Damaged`; CHECK đổi thành bất đẳng thức | `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:72` |
| `D-42` | §5.2: `SaleQty` chỉ `Sale` | §5.2: `Sale` + `RetailSaleOut` (POS) | `src/eCommerce.Domain.Shared/Enums.cs:101` |
| `D-176` | §5.2: bình quân 2 đầu kỳ + `CoveredDays` (đã đúng) | §5.2: `TurnoverRatio numeric(18,6)`, `DaysOfInventory = SốNgàyKỳ/TurnoverRatio`, banner khi thiếu ngày, một hàm thuần | Chưa có `RptInventoryTurnover` — kế hoạch GĐ3 |
| `D-177` | §0/§5.2: "định giá thuộc `INV-001`/`PRC-001`", chưa nói phương pháp/ngày | §0/§5.2/§9: **bình quân gia quyền di động**, giá vốn tại **ngày bán** từ `PrcCostPrices` `DaDuyet=true`; Setting; chủ = **Pricing** (`D-186`) | `modules/Pricing/.../CostPrice.cs:36,57–68`; `CostPriceAppService.cs:24–27` |
| `D-178` | §4.2: L2 = ETO; NFR-005: "job chỉ 01:00–05:00" | §4.2/§7.1/§13: L2 = **đúng một job** `D-172` (5 phút); NFR-005 chỉ ràng buộc job **nặng** | `TongHopBaoCaoService.cs:44,108`; `ReportingOptions.cs:18`; `ReportingAggregationWorker.cs:31` |
| `D-179` | §5.2/§12: `SettlementPending` dùng chung; `REC-002` GĐ2 | §5.2/§1/§13: phí thực tế **luôn NULL**, pending **chỉ** kênh sàn (`Channel.Type=1`); CHECK; `REC-002` = **GĐ5** | `Order.cs:167`; `ChannelEntities.cs:16–17` |
| `D-180` | §0 D5/§5.2: `ChannelShopId` NOT NULL + sentinel `Guid.Empty` | §5.2: **nullable** + unique **`NULLS NOT DISTINCT`**; tổng kênh = `GROUP BY ChannelCode` + `SUM` | `Order.cs:22`; `ChannelEntities.cs:38–48` |
| `D-181` | §5.2: một cột `GrossMarginAmount` | §5.2/§10: **hai** cột `GrossMargin1Amount`/`GrossMargin2Amount numeric(18,4)`; không trừ VAT lần hai | Chưa có code — kế hoạch GĐ3 |
| `D-182` | §0 D11/§9/§13: retention **24 tháng** | §0/§9/§11/§13: mặc định **`0` = TẮT** (24 là đề xuất); dry-run mặc định | Chưa có code — kế hoạch GĐ3 |
| `D-183` | §0/§7.5: `REP-003` xuất file + hash cho `ACC-001` | §0/§7.2/§7.5: `ACC-001` đọc qua `IReportFactReader`; file + `SHA-256` ở `ACC-001` | `modules/Reporting/**` có thật; **không** có `IReportFactReader` |
| `D-184` | §6.2/§9: không có công cụ so sánh ngoài job | §6.2/§8/§9: thêm `GET /reporting-aggregation/drift` + quyền `.Inspect` (0 ghi) | `TongHopBaoCaoService.ChayAsync:108` (một cửa ghi) |
| `D-185` | §5.2: `SaleQty` = `Sale` + `RetailSaleOut` | §5.2: thêm `ShippingOut` (11); **không** `ABS`, **không** trừ trả lại | `src/eCommerce.Domain.Shared/Enums.cs:38,77,101` |
| `D-186` | §0/§15.1: chủ động cơ giá vốn còn mâu thuẫn (Pricing vs `ACC-001`) ⇒ Q11 chưa chốt | §0/§15.1: chủ = **Pricing** (`PrcCostPrices` + đường ghi + động cơ bình quân di động + duyệt); `INV-001` chỉ cấp đơn giá; `ACC-001` chỉ tiêu thụ; Q11 `Closed (D-186)` | `CostPrice.cs:36,121–140`; `CostPriceAppService.cs:24–27` (chỉ đọc); `StockDocumentLine.UnitPrice` (`StockDocument.cs:411,466`) |

Đã đối chiếu thêm `D-07`, `D-17`, `D-28`, `D-47`, `D-48` — không lệch trong phạm vi `REP-003`.
