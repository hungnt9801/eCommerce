# `XC-005` — Observability: log có cấu trúc, CorrelationId & metric

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `XC-005` |
| **Module** | **Nền dùng chung** — cấu hình ở `eCommerce.HttpApi.Host`; tên field/metric là **hợp đồng dùng chung** |
| **Giai đoạn** | **Nền — phải xong TRƯỚC GĐ1** |
| **Ưu tiên** | **Should have** (nhưng **phải có trước khi lên production**) |
| **Trạng thái** | 🟡 `Draft` — chờ review |
| **Người viết / người review** | *(điền)* / **Kiến trúc trưởng + Vận hành** |
| **Phiên bản / ngày** | `v0.1 — 13/10/2026` |
| **Tài liệu liên quan** | `XC-002` (yêu cầu 4 metric messaging) · `XC-003` (yêu cầu 3 metric lock) · `XC-004` (audit, không log bí mật) · `docs/phase-0-execution-report.md` |

---

## 0. Tóm tắt & quyết định thiết kế

**Tóm tắt:** `XC-005` chốt **cách hệ thống kể lại chuyện nó đã làm**: log có cấu trúc (một dòng JSON, có trường chuẩn),
**`CorrelationId` xuyên suốt** request → job → sự kiện, **bảng đăng ký metric**, và health check.

**Điểm xuất phát thật (đã kiểm trong repo — xem §15.2 F1):**

| Hạng mục | Hiện trạng |
|---|---|
| Audit log (bảng DB) | ✅ **Có** — module `Volo.Abp.AuditLogging` đã bật (`AuditLog` xuất hiện **110** chỗ) |
| Log ứng dụng | ⚠️ Chỉ **console mặc định** qua mục `Logging` trong `appsettings.json`; **không** JSON, **không** trường tuỳ chỉnh |
| `CorrelationId` | ❌ **Chỉ tồn tại như CỘT trong bảng của ABP** — **12/12** chỗ nằm trong `Migrations/`; **chưa** có middleware, **0** chỗ trong code nghiệp vụ ⇒ giá trị hiện **rỗng** |
| Metric | ❌ **Không có** — `Serilog` / `OpenTelemetry` / `HealthCheck`: **0** gói trong `Directory.Packages.props` |
| `Console.WriteLine` | ✅ **0** chỗ (tốt — không có log tuỳ tiện) |

⇒ 🔴 **Hệ quả quan trọng:** `XC-002` và `XC-003` **đã yêu cầu 7 metric** (`messaging_outbox_pending`, `messaging_dlq_size`,
`lock_timeout_total`, …) nhưng **hiện không có nơi nào phát** chúng. Tài liệu này định nghĩa **tên + loại + nhãn**,
đồng thời ghi rõ **việc triển khai còn nợ**.

**Phạm vi:**
- ✅ Hợp đồng **tên trường log** + quy tắc mức log + quy tắc **không log dữ liệu nhạy cảm**.
- ✅ **`CorrelationId`**: nguồn, cách truyền, cách trả về cho client.
- ✅ **Bảng đăng ký metric** (tên · loại · nhãn · nguồn yêu cầu) + **quy tắc cardinality**.
- ✅ **Health check** + quy tắc không lộ chi tiết.
- ✅ Phân biệt rõ **audit log (dữ liệu nghiệp vụ)** vs **application log (vận hành)**.

**Không nằm trong phạm vi:**
- ❌ **Dashboard/alerting** (Grafana, Prometheus, Sentry) — chọn công cụ ở §15 (Q1). Tài liệu chỉ chốt **hợp đồng dữ liệu**.
- ❌ **Distributed tracing đầy đủ** (OpenTelemetry spans) — ghi nhận là bước sau (§15 Q2).
- ❌ **Chính sách lưu trữ/pháp lý của audit log** — thuộc `ACC-001`/kế toán.

### 0.1 Quyết định thiết kế (ADR ngắn)

| # | Quyết định | Phương án đã cân nhắc | Lý do chọn | Hệ quả |
|---|---|---|---|---|
| **D1** | **Log JSON một dòng** bằng formatter **có sẵn của .NET** (`AddJsonConsole`) | (a) console text mặc định · (b) **JSON console built-in** · (c) Serilog + sink | Chọn (b) trước: **không thêm gói NuGet nào**, đã đủ cho thu thập log tập trung; Serilog chỉ cần khi có yêu cầu sink cụ thể | Cấu hình ở `appsettings.json`; **cấm** log nhiều dòng (BR-010) |
| **D2** | **`CorrelationId` bắt buộc** với mọi request/job/sự kiện | (a) không có · (b) chỉ trong audit log · (c) **middleware + log scope xuyên suốt** | Nếu chỉ có trong `AbpAuditLogs` thì **không** nối được log ứng dụng với nhau; khi có sự cố cần **một mã** tra ra cả chuỗi | Wire `AbpCorrelationIdMiddleware` (**có sẵn** trong `Volo.Abp.AspNetCore`, không cần gói mới) + đưa vào log scope |
| **D3** | **Tên trường log là hợp đồng** (§5.2), không đặt tuỳ hứng | (a) mỗi nơi một tên · (b) registry | Tên khác nhau ⇒ truy vấn log không chạy được; đây là loại "nợ" rất khó sửa về sau vì log cũ không đổi được | Mọi log nghiệp vụ dùng `ILogger` với **structured logging** (tham số, không nối chuỗi) |
| **D4** | **Tên metric**: `<miền>_<đối tượng>_<đơn vị>` snake_case | (a) camelCase · (b) snake_case + hậu tố đơn vị | Hậu tố đơn vị (`_total`, `_seconds`, `_size`) là quy ước phổ biến, giúp người đọc biết **cách dùng** metric | Bảng đăng ký §5.1 |
| **D5** | 🚨 **Cấm nhãn có cardinality cao** (`orderId`, `sku`, `userId`, `correlationId`) | (a) gắn mọi thứ làm nhãn · (b) **nhãn hữu hạn** + log để truy vết | Mỗi giá trị nhãn tạo **một chuỗi thời gian riêng** ⇒ nổ bộ nhớ hệ thống metric. Đây là **bẫy kinh điển** | Nhãn chỉ từ tập **hữu hạn** (`channel`, `provider`, `eventName`, `status`, `tenantId`); chi tiết ⇒ **log** |
| **D6** | **Cấm log dữ liệu nhạy cảm**: token, mật khẩu, số thẻ, PII | (a) log thoải mái để dễ gỡ lỗi · (b) allowlist trường được log | Log thường được thu thập, gửi ra ngoài và giữ lâu ⇒ rò bí mật ở đó là **sự cố bảo mật**, không phải lỗi tiện lợi | Có test quét mã nguồn `TC-05`; §15.2 **F2** cảnh báo payload webhook |
| **D7** | **Audit log (DB) ≠ application log** | (a) dùng chung · (b) tách rõ | Audit là **dữ liệu nghiệp vụ** (phải giữ, có thể bị cơ quan thuế/kiểm toán hỏi); application log là **vận hành** (xoá được) | Audit **không** thay thế log khi gỡ lỗi; log **không** thay thế audit khi giải trình |
| **D8** | **Health check** dùng `AddHealthChecks()` có sẵn của ASP.NET Core | (a) tự viết endpoint · (b) **HealthChecks built-in** | Không thêm gói; đủ để load balancer/giám sát biết sống/chết **kèm** phụ thuộc (DB/Redis) | Endpoint **không** lộ chi tiết ra ngoài (BR-009) |
| **D9** | 🚨 **Cấm log ở mức `Information` trong vòng lặp nóng** | (a) log mỗi vòng · (b) log khi **có việc thật** | Relay outbox chạy nhịp **2 giây** và job quét DLQ nhịp **1 phút** ⇒ log mỗi vòng làm **ngập log**, che mất lỗi thật, và tốn chi phí lưu trữ | BR-004 + test `TC-09` (đo số dòng log khi hệ thống rỗi) |
| **D10** | **Log là best-effort** — không được làm hỏng nghiệp vụ | (a) để lỗi log nổi lên · (b) bọc và bỏ qua | Logger hỏng/đĩa đầy **không** được làm request thất bại | §12: nuốt lỗi ghi log (có đếm), **không** nuốt lỗi nghiệp vụ |

---

## 1. Use Case Specification

| Thuộc tính | Nội dung |
|---|---|
| **Use Case ID** | `XC-005` |
| **Use Case Name** | Ghi lại và truy vết hoạt động hệ thống |
| **Description** | *"Là người vận hành/lập trình viên, tôi muốn từ **một mã** (`CorrelationId`) tra ra toàn bộ chuỗi xử lý, và từ **metric** biết hệ thống đang khoẻ hay có tồn đọng — để phát hiện sự cố trước khi khách báo."* |
| **Actor(s)** | **Vận hành** · **Lập trình viên** (gỡ lỗi) · **Hệ thống** (ghi log/metric tự động) · **Giám sát ngoài** (gọi health check) |
| **Priority** | **Should have** — nhưng là **điều kiện để lên production** |
| **Trigger** | Mọi request HTTP, mọi job nền, mọi sự kiện được phát/tiêu thụ, mọi lần health check |
| **Pre-Condition(s)** | • Tenant hiện tại (`TenantId`) — đưa vào log scope khi có.<br>• `ChannelCode` — đưa vào log **khi** sự kiện có nguồn gốc kênh (không bắt buộc cho log hạ tầng).<br>• Đã bật cấu hình JSON console (§11). |
| **Post-Condition(s)** | • Mọi dòng log nghiệp vụ **có** `CorrelationId`.<br>• Mọi response **trả lại** `X-Correlation-Id`.<br>• Metric trong bảng §5.1 **được phát** với nhãn **hữu hạn**.<br>• **Không** dòng log nào chứa token/mật khẩu/số thẻ/PII. |
| **Basic Flow** | 1. Request vào ⇒ middleware **đọc `X-Correlation-Id`** (nếu client gửi) hoặc **sinh mới**.<br>2. Mở **log scope** với `CorrelationId`, `TenantId`, `UserId`.<br>3. Mọi log trong phạm vi **tự động** có các trường đó.<br>4. App service phát sự kiện ⇒ `CorrelationId` **truyền sang** sự kiện (trường `CorrelationId` của ETO — `XC-002` D11).<br>5. Consumer/job mở scope với cùng `CorrelationId` ⇒ **nối được chuỗi** xuyên tiến trình.<br>6. Response trả `X-Correlation-Id`; metric được phát. |
| **Alternative Flow** | **A1: Job định kỳ** — không có request ⇒ **sinh** `CorrelationId` mới cho mỗi vòng, kèm `JobName`.<br>**A2: Webhook từ NCC** — dùng `X-Correlation-Id` nếu NCC gửi, ngược lại sinh mới **và** ghi lại mã giao dịch của NCC để tra chéo. |
| **Exception Flow** | **E1:** ghi log lỗi (đĩa đầy, sink chết) ⇒ **bỏ qua**, tăng bộ đếm `logging_failures_total`, **không** làm hỏng request (D10).<br>**E2:** `X-Correlation-Id` do client gửi **không hợp lệ** (quá dài, ký tự lạ) ⇒ **sinh mới** + log `Warning` (không tin dữ liệu client — BR-014 của `XC-004`).<br>**E3:** health check **thất bại** ⇒ trả `503` + **chi tiết chỉ ở log**, không ở response. |
| **Business Rules** | • **BR-XC-005-001**: Mọi request HTTP **phải** có `CorrelationId` (nhận từ `X-Correlation-Id` hoặc sinh mới) và **phải** trả lại trong response header.<br>• **BR-XC-005-002**: Mọi log nghiệp vụ **phải** nằm trong scope có `CorrelationId`.<br>• **BR-XC-005-003**: `CorrelationId` **phải** được truyền sang ETO và **phải** được khôi phục ở consumer/job (nối chuỗi xuyên tiến trình).<br>• **BR-XC-005-004**: 🚨 **Cấm log ở mức `Information` trong vòng lặp nóng** (relay outbox, xử lý inbox, job quét DLQ). Chỉ log khi **có việc thật** hoặc khi lỗi.<br>• **BR-XC-005-005**: **Cấm** log token, mật khẩu, số thẻ, khoá bí mật, PII; payload thô phải **che** trước khi log.<br>• **BR-XC-005-006**: **Cấm** nối chuỗi khi log — **bắt buộc** structured logging (tham số hoá) để trường tách rời, truy vấn được.<br>• **BR-XC-005-007**: **Cấm** `Console.Write*` — chỉ dùng `ILogger<T>`.<br>• **BR-XC-005-008**: Mọi metric **phải** có tên theo quy ước **D4** và **phải** nằm trong bảng đăng ký (§5.1).<br>• **BR-XC-005-009**: 🚨 **Cấm** dùng nhãn metric có **cardinality cao** (`orderId`, `sku`, `userId`, `correlationId`, `email`).<br>• **BR-XC-005-010**: Log **phải là một dòng** cho mỗi sự kiện (không log nhiều dòng/không xuống dòng trong message).<br>• **BR-XC-005-011**: Mọi job/worker **phải** log **khởi động**, **dừng**, và **lỗi** (không log mỗi vòng — BR-004).<br>• **BR-XC-005-012**: Health check **không** được lộ chi tiết nội bộ (chuỗi kết nối, phiên bản, stack) ra ngoài.<br>• **BR-XC-005-013**: Mọi lỗi **đã xử lý** (bị nuốt, bị bỏ qua có chủ ý) **phải** log ở mức ≥ `Warning` + **có mã lỗi nghiệp vụ** — cấm nuốt im lặng (kế thừa `XC-006`/`XC-002`). |
| **Non-Functional Requirement** | • **NFR-XC-005-001**: Chi phí log p95 **< 1 ms**/dòng (không chặn luồng nghiệp vụ).<br>• **NFR-XC-005-002**: **100 %** dòng log trong phạm vi request/job có `CorrelationId` (kiểm bằng integration test).<br>• **NFR-XC-005-003**: Số **chuỗi** (series) mỗi metric **< 100** (nhãn hữu hạn) — đo bằng test cấu hình.<br>• **NFR-XC-005-004**: Khi hệ thống **rỗi**, số dòng log mỗi phút **< 10** (chống ngập do vòng lặp nóng).<br>• **NFR-XC-005-005**: Health check trả trong **< 100 ms** (không gọi phụ thuộc nặng mỗi lần).<br>• **NFR-XC-005-006**: **0** dòng log chứa mẫu dữ liệu nhạy cảm (quét tự động).<br>• **NFR-XC-005-007**: Từ `CorrelationId` tra ra **toàn bộ** log liên quan của một đơn (mọi module, mọi tiến trình).<br>• **NFR-XC-005-008**: Log ở dạng **JSON hợp lệ** từng dòng (parse được bằng công cụ chuẩn). |

---

## 2. Sơ đồ Use Case

```mermaid
flowchart LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef usecase fill:#e2f4f6,stroke:#17a2b8,stroke-width:2px;

    OPS(("👤<br/>Vận hành"))
    DEV(("👤<br/>Lập trình viên"))
    MON(("🌐<br/>Giám sát ngoài"))

    subgraph OBS["📦 Observability"]
        direction TB
        UC1(["«log có cấu trúc»"])
        UC2(["«gắn & truyền CorrelationId»"])
        UC3(["«phát metric»"])
        UC4(["«tra chuỗi theo CorrelationId»"])
        UC5(["«health check»"])
        class UC1,UC2,UC3,UC4,UC5 usecase;
    end

    DEV --- UC1
    DEV --- UC4
    OPS --- UC3
    MON --- UC5
    UC1 -. "«include»" .-> UC2
    UC4 -. "«include»" .-> UC2
```

---

## 3. Sơ đồ trạng thái (state machine)

**Không áp dụng — lý do:** observability không có vòng đời hay chuyển trạng thái; đây là **hợp đồng dữ liệu + cấu hình**.
Các bất biến thay thế được liệt kê ở §8.1.

---

## 4. Sequence Diagram & luồng dữ liệu

### 4.1 `CorrelationId` xuyên request → sự kiện → consumer

```mermaid
sequenceDiagram
    participant C as Client
    participant MW as AbpCorrelationIdMiddleware
    participant APP as App Service
    participant OUT as Outbox
    participant CON as Consumer / Job
    participant LOG as Log (JSON 1 dong)

    C->>MW: «POST /api/app/orders + X-Correlation-Id (tuy chon)»
    alt «client co gui ma hop le»
        MW->>MW: «dung lai ma do»
    else «khong co / khong hop le»
        MW->>MW: «sinh ma moi»
    end
    MW->>APP: «mo log scope: CorrelationId + TenantId + UserId»
    APP->>LOG: «Info: OrderPlaced {OrderNumber} {Amount}»
    APP->>OUT: «publish OrderPlacedEto (kem CorrelationId)»
    OUT->>CON: «relay phat su kien»
    CON->>CON: «khoi phuc CorrelationId tu ETO -> mo scope»
    CON->>LOG: «Info: StockReserved {Sku} (CUNG CorrelationId)»
    MW-->>C: «response + X-Correlation-Id»
    Note over LOG: «Mot ma tra ra toan bo chuoi»
```

### 4.2 Luồng log — và vì sao **cấm** log trong vòng lặp nóng

```mermaid
graph LR
    classDef ok fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef bad fill:#f8d7da,stroke:#721c24,stroke-width:2px;
    classDef proc fill:#cce5ff,stroke:#004085,stroke-width:2px;

    A["«Request HTTP»"] --> L1["«Info: nghiep vu»"]
    B["«Job dinh ky (1 phut)»"] --> L2["«Info: chi khi CO VIEC»"]
    C["«Relay outbox (2 giay)»"] --> L3["«Debug: moi vong»"]
    D["«Loi bat ky»"] --> L4["«Error + ma loi nghiep vu»"]

    L3 --> X["«Neu log Info moi vong => 43.200 dong/ngay»"]
    L1 --> OK["«Thu thap tap trung»"]
    L2 --> OK
    L4 --> OK

    class L1,L2,L4,OK ok;
    class L3,X bad;
    class A,B,C,D proc;
```

---

## 5. Hợp đồng dữ liệu

**Không có bảng DB mới** (log không lưu vào DB nghiệp vụ; audit log dùng bảng có sẵn của ABP).
"Mô hình dữ liệu" của feature này là **hai bảng đăng ký**: **metric** và **trường log**.

### 5.1 Bảng đăng ký metric

> 🚨 **ĐỌC KÈM §19 — bảng này ĐÃ TỪNG LỆCH 12 ↔ 15 TÊN so với mã.**
> Từ 21/10/2026 (`S10`), bảng dưới đây **được máy đối chiếu**: `test/eCommerce.Tests/Observability/MetricEmitterTests.cs`
> khoá **ba chiều** — (①) `eCommerceMetricNames.All` ⇒ **mọi tên phải có điểm phát thật** trong `src/`/`modules`/`shared`,
> trừ những tên **khai báo tường minh** là *chưa có* (`eCommerceMetricNames.ChuaCoNoiPhat`); (②) tên **khai chưa có**
> thì **không được** có lời gọi nào (khai báo cũ ⇒ ĐỎ); (③) bảng ký hiệu phủ **đúng** `All`.
> ⇒ 📌 Cột **"Nơi phát"** không còn là lời khai: **sai ⇒ CI đỏ**.

| Tên metric | Loại | Nhãn (hữu hạn) | Nguồn yêu cầu | Nơi phát (`S10` đo) |
|---|---|---|---|---|
| `messaging_outbox_pending` | Gauge | — | **`XC-002` NFR-003** | ✅ **24/10/2026** — `OutboxPendingWorker` (project EF; đếm `AbpEventOutbox` mỗi 60 s) |
| `messaging_inbox_failed_total` | Counter | `eventName` | **`XC-002` NFR-009** | ✅ `IdempotencyGuard` |
| `messaging_dlq_size` | Gauge | — | **`XC-002` NFR-009** | ✅ `MarketplaceReconciliationWorker` |
| `messaging_dlq_replayed_total` | Counter | `result` (`ok`), `eventName` | **`XC-002` NFR-009** | ✅ **29/10/2026 (`S16b`)** — `DeadLetterAppService.PhatLaiAsync`: **publish lại event gốc** lên `IDistributedEventBus` (`useOutbox: true`, cùng UoW) rồi **mới** tăng counter (xem §21) |
| `lock_timeout_total` | Counter | `module` | **`XC-003` NFR-003** | ✅ `ChannelTokenManager` |
| `lock_skipped_total` | Counter | `module` | **`XC-003` A1** | ✅ `TaxUnclassifiedSkusWorker` |
| `lock_ttl_exceeded_total` | Counter | `module` | **`XC-003` NFR-007** | ✅ **29/10/2026** — `DoThoiGianGiuKhoa` (decorator `IAbpDistributedLock`, nghe `HandleLostToken`) |
| `http_request_duration_seconds` | Histogram | `method`, `route`, `status` | `XC-005` | ✅ `RequestDurationRecorder` (`shared/`) |
| `payment_webhook_rejected_total` | Counter | `provider`, `reason` | `XC-005` (`PaymentHub`) | ✅ `VnPayPaymentProvider` |
| `channel_sync_failed_total` | Counter | `channel`, `kind` | `XC-005` (`ChannelHub`) | ✅ `ChannelShopSyncer` |
| `inventory_recon_mismatch_skus` | Gauge | — | `INV-001` ADR-10 | ✅ `InventoryReconciliationService` |
| `tax_unclassified_skus` | Gauge | — | `XC-006` §10 | ✅ `TaxUnclassifiedSkusWorker` |
| `payment_recon_unresolved_total` | Gauge | — | `REC-001` §7 | ✅ `PaymentReconciliationWorker` + `PaymentAppService` |
| `payment_recon_changed_total` | Counter | `result` | `REC-001` §7 | ✅ `MetricDoiSoatThanhToan` |
| `sales_order_transition_total` | Counter | `axis`, `from`, `to` | **`SAL-003` NFR-012** (24/10/2026) | ✅ `OrderStatusMetricHandler` (nghe `OrderStatusChangedEto`) |
| `logging_failures_total` | Counter | — | `XC-005` D10 | ✅ **29/10/2026** — `DemLoiGhiLogProvider` (bọc mọi `ILoggerProvider`; lỗi ⇒ stderr + counter) |

> 📌 **ĐẾM ĐƯỢC (`S16`, 29/10/2026): 15/16 có nơi phát · 1/16 chưa.** 🔒 **KHÔNG** ghi *"16/16"* khi chưa có phép đo —
> con số này khớp với `MetricEmitterTests` (quét mã **hai chiều**).
> ✅ **`S16b` (29/10/2026): 16/16.** Nợ cuối cùng (`messaging_dlq_replayed_total`) **đã đóng bằng mã** —
> `DeadLetterAppService.PhatLaiAsync` **publish lại event thật** rồi mới đếm (xem §21) ⇒
> `eCommerceMetricNames.ChuaCoNoiPhat` nay **rỗng**, và `MetricEmitterTests` vẫn xanh **cả hai chiều**.

> 🚨 **Quy tắc nhãn (BR-009):** nhãn **chỉ** lấy từ tập hữu hạn. **Cấm** `orderId`, `sku`, `userId`, `correlationId`,
> `email`, `phone`. Cần truy vết theo các giá trị đó ⇒ **dùng log** (đã có `CorrelationId`), **không** dùng metric.

### 5.2 Bảng đăng ký trường log (log fields)

| Trường | Kiểu | Bắt buộc ở | Ý nghĩa |
|---|---|---|---|
| `CorrelationId` | string | **Mọi** log trong phạm vi request/job (**BR-002**) | Mã nối chuỗi |
| `TenantId` | string | Khi có ngữ cảnh tenant | Cách ly & truy vết |
| `UserId` | string | Khi có người dùng | Ai gây ra |
| `ChannelCode` | string | Log miền kênh | `shopee`/`lazada`/`web`… |
| `EventId` · `EventName` | string | Log phát/tiêu thụ sự kiện | Khớp `XC-002` D11 |
| `AggregateId` · `AggregateVersion` | string · int | Log miền có xếp thứ tự | Khớp `XC-002` D6 |
| `IdempotencyKey` | string | Log biên vào | Khớp `XC-002` |
| `LockKey` | string | Log liên quan khoá | Khớp `XC-003` |
| `OrderNumber` · `Sku` | string | Log nghiệp vụ bán/kho | **Chỉ ở log**, **cấm** làm nhãn metric |
| `Provider` | string | Log tích hợp NCC | `vnpay`/`ghn`/… |
| `DurationMs` | number | Log đo thời gian | Không log riêng `Start/End` |
| `ErrorCode` | string | Log lỗi đã xử lý (**BR-013**) | Mã lỗi nghiệp vụ (`eCommerce:…`) |

---

## 6. Hợp đồng API & cấu hình

### 6.1 Health check

| Endpoint | Trả về | Quy tắc |
|---|---|---|
| `/health-status` | `200` (khoẻ) / `503` (có phụ thuộc hỏng) | **Không** lộ chi tiết; chi tiết chỉ ở **log** (BR-012). Kiểm: PostgreSQL, Redis |
| `/.well-known/*`, `/connect/*` | OpenIddict | **Allowlist** `[AllowAnonymous]` — khớp `XC-004` Q1 |

### 6.2 Cấu hình log (đề xuất)

```jsonc
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning", // tranh log moi cau SQL
      "eCommerce": "Information"
    },
    "Console": { "FormatterName": "json" }   // D1: JSON 1 dong, khong can goi NuGet
  }
}
```

> ⚠️ Ghi rõ: mức `Debug` **chỉ** bật ở môi trường dev hoặc khi đang gỡ lỗi có thời hạn.

---

## 7. Sự kiện, tích hợp ngoài & idempotency

### 7.1 Sự kiện phát ra

| ETO | Khi nào | Ghi chú |
|---|---|---|
| `CorrelationId` **trong mọi ETO** | — | Không phải ETO riêng, mà là **trường bắt buộc** theo `XC-002` D11 (BR-003) |

### 7.2–7.3 Nhận từ ngoài / gọi ra ngoài

**Không áp dụng** — trừ việc **nhận** `X-Correlation-Id` từ NCC (A2). **Không** gửi dữ liệu log/metric ra dịch vụ ngoài
trong phạm vi tài liệu này (chọn công cụ ở Q1).

**Idempotency key:** **Không áp dụng** — log/metric **không** phải nguồn sự thật nghiệp vụ. 🔒 **Cấm** dùng metric
để quyết định nghiệp vụ (ví dụ "DLQ có bản ghi thì chặn bán") — metric có thể mất/trễ.

---

## 8. Bất biến & xử lý đồng thời

### 8.1 Bất biến

| # | Bất biến | Cơ chế bảo vệ |
|---|---|---|
| 1 | Mọi log trong phạm vi request/job có `CorrelationId` | Middleware + scope; test `TC-04` |
| 2 | Response luôn có `X-Correlation-Id` | Middleware; test `TC-03` |
| 3 | **0** dòng log chứa dữ liệu nhạy cảm | Quét `TC-05` + allowlist trường |
| 4 | **0** `Console.Write*` trong mã nguồn | Quét `TC-02` |
| 5 | Mọi metric phát ra **nằm trong** bảng đăng ký | Quét tên metric literal vs registry `TC-06` |
| 6 | **0** nhãn metric cardinality cao | Review + test cấu hình `TC-07` |
| 7 | Log/metric **không** ảnh hưởng nghiệp vụ (best-effort) | Bọc lỗi ghi log (D10); test `TC-10` |

### 8.2 Xử lý đồng thời

| # | Tình huống | Cơ chế |
|---|---|---|
| 1 | Nhiều instance cùng ghi log/metric | Ghi **cục bộ** (stdout) → hệ thống thu thập gom; **không** tranh chấp |
| 2 | Nhiều instance cùng phát cùng metric | Metric **đếm theo instance** rồi cộng; không cần khoá |
| 3 | Log scope trong luồng bất đồng bộ | Dùng `AsyncLocal` của `ILogger.BeginScope` — **tự** theo luồng; lưu ý khi dùng `Task.Run` phải **truyền** scope/CorrelationId |

**Khoá phân tán:** **Không dùng.** **Idempotency key:** **Không áp dụng**.

---

## 9. Phân quyền

**Không áp dụng — lý do:** không có endpoint nghiệp vụ. Riêng **health check** là `[AllowAnonymous]` **có chủ ý**
(nằm trong allowlist của `XC-004` BR-004) — nhưng **không** lộ chi tiết (BR-012).
Màn hình xem DLQ/tồn đọng **có** phân quyền — thuộc `XC-002` §6.2 (không phải tài liệu này).

---

## 10. UI Mockup & mapping dữ liệu

**Không có UI riêng.** Dashboard là công cụ ngoài (Q1). Quy tắc cho UI khác:

| Vùng UI | Dữ liệu | Nguồn | Quy tắc |
|---|---|---|---|
| Mọi màn hình lỗi | `CorrelationId` | response header | **Hiển thị mã này** cho người dùng khi có lỗi (kèm "gửi mã này cho hỗ trợ") — giúp tra log **tức thì** |
| Màn hình quản trị tồn đọng/DLQ | metric + danh sách | `XC-002` §6.2 | chỉ đọc; có phân quyền |

---

## 11. Migration & seed

| Mục | Nội dung |
|---|---|
| Migration | **Không có** — log/metric không lưu vào DB nghiệp vụ |
| Cấu hình bắt buộc | (1) thêm `Console.FormatterName = json`; (2) wire `AbpCorrelationIdMiddleware` + `UseAbpRequestLocalization` trong pipeline; (3) `AddHealthChecks()` + map `/health-status`; (4) điều chỉnh mức log (tắt EF command log ở production) |
| Seed | **Không có** |
| ⚠️ **Còn nợ** | **Phát 12 metric** trong §5.1 — hiện **chưa có** nơi phát. Cần khi có gói metric (Q1) |
| Bảo mật | **Không** log `ConnectionStrings`, secret, `appsettings.Production.json` (đã `.gitignore`) |

---

## 12. Xử lý lỗi & trường hợp biên

| Tình huống | Nguyên nhân | Hành vi | Ghi chú |
|---|---|---|---|
| Ghi log thất bại | đĩa đầy / sink chết | **Bỏ qua**, tăng `logging_failures_total` | 🚨 **Không** làm request thất bại (D10) |
| `X-Correlation-Id` không hợp lệ | client gửi bậy | **Sinh mới** + log `Warning` | Không tin dữ liệu client (BR-014 `XC-004`) |
| `CorrelationId` mất khi qua job | không truyền vào ETO | **Vi phạm BR-003** — phát hiện bằng test `TC-04` | Đây là lỗi hay gặp nhất khi thêm sự kiện mới |
| Vòng lặp nóng log `Information` | lập trình sai | **Ngập log** ⇒ che lỗi thật + tốn chi phí | BR-004 + `TC-09` |
| Nhãn metric có `orderId` | lập trình sai | **Nổ số chuỗi** hệ thống metric | BR-009 + `TC-07` |
| Health check gọi DB/Redis | thiết kế sai | Chậm + tạo tải giả | Ngưỡng < 100 ms; cache kết quả ngắn nếu cần |
| Log chứa payload webhook | in cả payload để gỡ lỗi | 🚨 rò PII ra hệ thống log | §15.2 **F2**; phải **che** trước khi log (BR-005) |
| Log đa dòng (stack trace nối chuỗi) | nối `ex.ToString()` vào message | Bản ghi log hỏng khi thu thập | BR-010: truyền `Exception` **như tham số**, không nối chuỗi |

**Chiến lược transaction:** **Không áp dụng** — log/metric nằm **ngoài** transaction nghiệp vụ (cố ý: không được
làm transaction dài hơn hay hỏng theo nghiệp vụ).

---

## 13. Phi chức năng

| Mã | Yêu cầu | Ngưỡng | Cách kiểm chứng |
|---|---|---|---|
| NFR-XC-005-001 | Chi phí log | p95 **< 1 ms**/dòng | benchmark |
| NFR-XC-005-002 | `CorrelationId` hiện diện | **100 %** log trong scope | integration `TC-04` |
| NFR-XC-005-003 | Chuỗi metric | **< 100** series/metric | test cấu hình `TC-07` |
| NFR-XC-005-004 | Log khi hệ thống rỗi | **< 10** dòng/phút | đo `TC-09` |
| NFR-XC-005-005 | Health check | **< 100 ms** | test |
| NFR-XC-005-006 | Dữ liệu nhạy cảm trong log | **0** mẫu khớp | quét `TC-05` |
| NFR-XC-005-007 | Truy vết theo `CorrelationId` | ra **toàn bộ** log liên quan | test tích hợp |
| NFR-XC-005-008 | JSON hợp lệ | **100 %** dòng parse được | test formatter |

---

## 14. Test cases & Acceptance Criteria

### 14.1 Danh sách test case

| ID | Mục tiêu | Loại | Input | Expected | Phủ |
|---|---|---|---|---|---|
| **TC-01** | Mọi app service/job dùng `ILogger<T>` | Unit (reflection) | quét type | **0** chỗ dùng logger tĩnh kiểu cũ | BR-007 |
| **TC-02** | **Không** `Console.Write*` | Unit (quét) | grep mã nguồn | **0** match | **BR-007** |
| **TC-03** | Response trả `X-Correlation-Id` | **Integration** | request có/không có header | luôn có header trả về | **BR-001** |
| **TC-04** | Log có `CorrelationId` **xuyên sự kiện** | **Integration** | đặt đơn → consumer xử lý | log của consumer có **cùng** `CorrelationId` | **BR-002/003** |
| **TC-05** | Không log dữ liệu nhạy cảm | Unit (quét) | tìm `password`/`token`/`card` trong lời gọi log | **0** match | **BR-005**, NFR-006 |
| **TC-06** | Metric phát ra **nằm trong** registry | Unit (quét) | so tên metric literal vs §5.1 | **0** metric ngoài registry | **BR-008** |
| **TC-07** | Không nhãn cardinality cao | Unit (cấu hình) | danh sách nhãn | **0** nhãn cấm; **< 100** series | **BR-009**, NFR-003 |
| TC-08 | Health check | **Integration** | DB/Redis OK; dừng Redis | `200`; sau đó `503` — **không** lộ chi tiết | **BR-012** |
| **TC-09** | Vòng lặp nóng **không** log `Information` | **Integration** | để hệ thống rỗi 60 s | **< 10** dòng log | **BR-004**, NFR-004 |
| TC-10 | Ghi log lỗi **không** làm hỏng nghiệp vụ | Unit | logger ném exception | request vẫn thành công + `logging_failures_total` tăng | **D10** |
| TC-11 | Log là JSON hợp lệ từng dòng | Unit | đọc output | **100 %** parse được | BR-010, NFR-008 |
| TC-12 | Lỗi đã xử lý có log + mã lỗi | Unit (quét) | tìm `catch` rỗng | **0** catch rỗng | **BR-013** |

### 14.2 Fixture skeleton (xUnit)

```csharp
public class ObservabilityTests
{
    /// TC-02 — khong cho phep log tuy tien.
    [Fact]
    public void TC02_khong_co_Console_Write_trong_ma_nguon()
    {
        var viPham = QuetMaNguon(@"Console\.Write")
            .Where(f => !f.Contains("Program.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0, "Dung Console.Write*:\n" + string.Join("\n", viPham));
    }

    /// TC-06 — metric phai nam trong bang dang ky (giong cach XC-003 kiem khoa lock).
    [Fact]
    public void TC06_moi_metric_phat_ra_phai_nam_trong_bang_dang_ky()
    {
        var daDangKy = MetricRegistry.TatCa;          // §5.1
        var dungTrongCode = QuetTenMetricLiteral();   // ví dụ: "messaging_dlq_size"

        var ngoaiRegistry = dungTrongCode.Except(daDangKy).ToList();
        Assert.True(ngoaiRegistry.Count == 0,
            "Metric chua dang ky:\n" + string.Join("\n", ngoaiRegistry));
    }

    /// TC-07 — cam nhan co cardinality cao (bay kinh dien lam no he thong metric).
    [Fact]
    public void TC07_cam_nhan_metric_cardinality_cao()
    {
        string[] cam = ["orderId", "sku", "userId", "correlationId", "email", "phone"];

        var viPham = MetricRegistry.TatCaNhan()
            .Where(n => cam.Contains(n, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0,
            "Nhan metric bi cam (cardinality cao):\n" + string.Join("\n", viPham));
    }
}
```

### 14.3 Acceptance Criteria (Definition of Done)

- [ ] 12 test case pass, phủ **hết 13 BR** và **8 NFR**
- [ ] **JSON console** đã bật; log parse được bằng công cụ chuẩn (NFR-008)
- [ ] **`AbpCorrelationIdMiddleware` đã wire**; `TC-03`/`TC-04` pass (hiện **chưa** wire — §15.2 F1)
- [ ] `/health-status` hoạt động; **không** lộ chi tiết
- [ ] **Bảng đăng ký metric (§5.1) khớp 100 % với code** — `TC-06` xanh
- [ ] Mức log production đã chỉnh (tắt `Microsoft.EntityFrameworkCore.Database.Command`)
- [ ] ⚠️ **Việc còn nợ được ghi rõ**: **12 metric chưa có nơi phát** — cần khi chọn công cụ metric (Q1)
- [ ] Tài liệu này cập nhật khớp code

### 14.4 Definition of Ready

- [x] ✅ Đã kiểm hiện trạng: **0** gói Serilog/OpenTelemetry/HealthCheck; **0** `Console.Write*`; audit log **đã có**
- [x] ✅ Đã xác định **7 metric** đang bị `XC-002`/`XC-003` yêu cầu mà chưa có nơi phát
- [ ] Chốt **công cụ** thu thập log + metric (Q1)
- [ ] Chốt **thời gian lưu** log (Q4)
- [ ] Chốt có làm **distributed tracing** (OpenTelemetry) ở giai đoạn này không (Q2)

---

## 15. Câu hỏi mở, giả định & lịch sử thay đổi

### 15.1 Câu hỏi mở

| # | Câu hỏi | Cần ai | Trạng thái | Ảnh hưởng |
|---|---|---|---|---|
| **Q1** | **Công cụ** thu thập log & metric: chỉ stdout + công cụ ngoài, hay thêm Serilog/OpenTelemetry + Prometheus? Và khi nào? | Kiến trúc trưởng + Vận hành | ✅ `Closed` — **PHÁT NGAY bằng `System.Diagnostics.Metrics` (BCL, KHÔNG thêm gói NuGet nào)** + **log JSON ra stdout**. Công cụ thu thập (OTel Collector → Prometheus/Grafana, hay chỉ stdout + công cụ ngoài) **chọn SAU**: vì đổi tầng *xuất* không phải sửa nơi *phát* | Quyết định **khi nào** 12 metric ở §5.1 được phát thật |
| **Q2** | Có làm **distributed tracing** (OpenTelemetry) ở GĐ1 không, hay để GĐ3? | Kiến trúc trưởng | ✅ `Closed` — **KHÔNG** làm distributed tracing ở GĐ1. `X-Correlation-Id` đã **nối log giữa các tầng** (đủ để điều tra). Bật OTel tracing khi tách service (GĐ4+) | Tracing giúp gỡ lỗi chuỗi dài nhưng thêm hạ tầng |
| **Q3** | **Alert** gửi đi đâu (email/Zalo OA/Slack)? Ngưỡng nào? | Vận hành | ✅ `Closed` — alert gửi **email kỹ thuật** *(chốt 13/10/2026)*; ngưỡng theo §5.2 | Thuộc `NOT-002`; cần trước production |
| **Q4** | **Thời gian lưu** log (30/90 ngày?) và audit log (theo luật kế toán)? | Vận hành + Kế toán | ✅ `Closed` — **log lưu 90 ngày**; **audit log theo quy định luật kế toán** *(chốt 13/10/2026)* | Ảnh hưởng chi phí lưu trữ + nghĩa vụ lưu trữ |
| **Q5** | `X-Correlation-Id` có **bắt buộc** trả lại trong **mọi** response kể cả `401`/`403` không? | Kiến trúc trưởng | ✅ `Closed` — **CÓ** *(đã triển khai + đo được 13/10/2026)*: `app.UseCorrelationId()` ⇒ **mọi** response có header `X-Correlation-Id` (đo thật: `aa003348cac54ee99f7d7c7114dca531`) | Ảnh hưởng middleware order; đề xuất **có** |
| **Q6** | Có cần **che PII** trong `PaymentTransaction.LastWebhookPayload` (đang lưu tới 8.000 ký tự)? | Kiến trúc trưởng + Security | ✅ `Closed` — **CÓ** *(đã triển khai 13/10/2026)*: `PaymentTransaction.MaskSensitiveData` che **2 lớp** (tên trường nhạy cảm + chuỗi 12–19 chữ số giữ 4 số cuối) + **5 test**; test đã bắt được lỗi trong chính regex đầu tiên | Xem **F2** — dữ liệu thật trong DB, không chỉ log |

### 15.2 🚨 Phát hiện

| # | Vấn đề | Bằng chứng | Mức độ | Xử lý |
|---|---|---|---|---|
| **F1** | **Hạ tầng quan sát gần như trống**: **0** gói Serilog/OpenTelemetry/HealthCheck; `CorrelationId` **chỉ** là **cột trong bảng của ABP** (**12/12** chỗ nằm trong `Migrations/`), **chưa** có middleware ⇒ giá trị hiện **rỗng**; **7 metric** mà `XC-002`/`XC-003` yêu cầu **chưa có nơi phát** | quét `Directory.Packages.props` + repo | 🟠 **Trung bình–Cao** (không sai dữ liệu, nhưng **mù** khi có sự cố) | ⬜ **Chưa sửa** — `XC-005` định nghĩa hợp đồng; việc wire + phát metric là **mục bắt buộc trước production** (§11) |
| **F1** | **Hạ tầng quan sát gần như trống**: **0** gói Serilog/OpenTelemetry/HealthCheck; `CorrelationId` **chỉ** là **cột trong bảng của ABP** (**12/12** chỗ nằm trong `Migrations/`), **chưa** có middleware ⇒ giá trị hiện **rỗng**; **7 metric** mà `XC-002`/`XC-003` yêu cầu **chưa có nơi phát** | quét `Directory.Packages.props` + repo | 🟠 **Trung bình–Cao** (không sai dữ liệu, nhưng **mù** khi có sự cố) | ✅ **PHẦN LÕI ĐÃ VÁ + KIỂM CHỨNG (13/10/2026)**: bật `UseCorrelationId()` (header `X-Correlation-Id` trả về **thật**), thêm `/health-status` (**200 `Healthy`** khi PostgreSQL sống · **503** khi **DỪNG** PostgreSQL ⇒ check **thật sự** kiểm tra phụ thuộc), bật **log JSON 1 dòng**. ✅ **12 metric ĐÃ CÓ NƠI PHÁT** (13/10/2026) bằng `System.Diagnostics.Metrics` **có sẵn trong .NET** — không thêm gói NuGet nào; có **6 test `MeterListener`** chốt đúng 12 tên (`TC-06`) và chặn nhãn cardinality cao (`TC-07`) |
| **F3** 🆕 | Khi vá health check gặp 2 lỗi **chỉ lộ lúc chạy**: (1) `MapHealthChecks` là extension trên **`IEndpointRouteBuilder`** chứ không phải `IApplicationBuilder`; (2) inject thẳng **`AbpDbConnectionOptions` sẽ ném lúc chạy** — ABP đăng ký qua `Configure<T>` nên DI chỉ có `IOptions<T>`, mà health check dùng `ActivatorUtilities` (resolve theo **đúng kiểu tham số**) | Lỗi biên dịch (1) + `"Unable to resolve service for type 'AbpDbConnectionOptions'"` (2) ⇒ `/health-status` trả **500** | 🟠 Trung bình | ✅ **Đã sửa**: dùng `app.UseEndpoints(...)` và `IOptions<AbpDbConnectionOptions>` |
| **F2** | `PaymentTransaction.LastWebhookPayload` lưu **payload thô tới 8.000 ký tự** | `PaymentHub.Domain/Entities/PaymentTransaction.cs` | 🟠 **Trung bình** | ⬜ Q6: cần **che** dữ liệu nhạy cảm (số thẻ, PII) khi lưu **và** khi log |
| **F3** | Relay outbox nhịp **2 giây** (`XC-002`) + job quét DLQ nhịp **1 phút** ⇒ nếu log mỗi vòng là **43.200 dòng/ngày** cho một worker | `XC-002` §0.1 D1 + BR-004 ở đây | 🟡 Thấp nếu tuân BR-004 | ✅ Đã thành **BR-004** + test `TC-09` |
| **F4** | Tài khoản seed có mật khẩu trong tài liệu (kế thừa `XC-004` **F4**) — **cấm** để lộ vào log/cấu hình | `XC-004` §15.2 F4 | 🟠 Trung bình | ⬜ Vẫn chưa sửa — theo `XC-004` Q2 |

### 15.3 Giả định

| # | Giả định | Nếu SAI |
|---|---|---|
| 1 | Log ghi ra **stdout**, hệ thống ngoài thu thập | Nếu ghi file trực tiếp ⇒ cần xoay vòng file + quyền ghi; đổi ở §11 |
| 2 | Metric được phát **từ tiến trình** (pull/`/metrics` hoặc push) | Nếu dùng dịch vụ metric riêng ⇒ chỉ đổi cách xuất, **không** đổi tên/nhãn |
| 3 | `tenantId` là nhãn **hữu hạn** (số tenant nhỏ) | Nếu GĐ4 có **hàng nghìn** tenant ⇒ phải **bỏ** `tenantId` khỏi nhãn |
| 4 | Health check **không** cần xác thực | Nếu cần ⇒ đưa vào allowlist kèm token tĩnh (§15 Q5) |

### 15.4 Lịch sử thay đổi

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.1 | 13/10/2026 | *(điền)* | Bản nháp đầu — hiện trạng lấy **từ code thật** (0 gói observability; `CorrelationId` chỉ là cột DB, 12/12 trong migration; audit log đã có; 0 `Console.Write*`); lập **bảng đăng ký metric** gom 7 metric đang bị `XC-002`/`XC-003` yêu cầu mà chưa có nơi phát; phát hiện **F2** (payload webhook thô 8.000 ký tự) |


---

## 16. ⚠️ PHÁT HIỆN KIẾN TRÚC (14/10/2026) — **vì sao 7 metric vẫn CHƯA có nơi phát**

Trong lúc làm mục (2) của GĐ0 (wire metric), tôi phát hiện **nguyên nhân gốc không phải "chưa làm" mà là
một lỗi TẦNG**:

`eCommerceMetrics` (bảng đăng ký + 12 instrument) đang nằm ở
**`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs`** — tức **tầng APP**.

Nhưng nơi cần phát metric lại là **bên trong MODULE**:
| Metric | Nơi cần phát | Project |
|---|---|---|
| `messaging_inbox_failed_total` | `IdempotencyGuard` | `modules/Messaging/…Domain` |
| `lock_timeout_total` · `lock_skipped_total` | `ChannelTokenManager` | `modules/ChannelHub/…Application` |
| `channel_sync_failed_total` | `ChannelShopSyncer` | `modules/ChannelHub/…Application` |

🚨 **Quy ước #3 của dự án CẤM module tham chiếu app** (`module→app forbidden`) ⇒ các module **KHÔNG THỂ**
dùng `eCommerceMetrics` ở vị trí hiện tại. Đó là lý do **cấu trúc**: 12 instrument đã đăng ký + có 6 test
`MeterListener`, nhưng **không nơi nào phát được** mà không vi phạm quy ước.

### ✅ Cách sửa ĐÚNG (chưa thực hiện — cần một lượt riêng để làm và KIỂM CHỨNG)

**Chuyển `eCommerceMetrics` + `eCommerceMetricNames` + `eCommerceMetricTags` sang
`shared/eCommerce.Kernel/Observability/`** (namespace `eCommerce.Kernel.Observability`).

Vì sao Kernel là đúng chỗ: nó **đã** được mọi module tham chiếu (`Messaging.Domain` → Kernel;
`Address`/`EInvoice`/`Signing` → …), và hợp đồng quan sát **dùng chung** đúng bản chất "shared kernel" —
giống `Money`/`Currency`. Sau khi chuyển thì **mọi module phát metric được mà KHÔNG vi phạm quy ước nào**.

**Việc kèm theo khi chuyển:** cập nhật `MetricsTests` (đổi `using`), thêm `ProjectReference` Kernel cho các
module cần phát, rồi mới wire được 3 điểm phát thật ở bảng trên. **Ước lượng: 1 lượt làm + kiểm chứng.**

> 📌 Đây là ví dụ thứ hai trong dự án về **lỗi tầng** (lỗi đầu là `ConventionalControllers` chỉ thấy
> assembly **đã nạp** — C16). Cả hai đều **không lộ ra khi build**, chỉ lộ khi hỏi *"vậy ai gọi cái này?"*.

---

## 17. ✅ ĐÃ SỬA LỖI TẦNG + ĐIỂM PHÁT METRIC ĐẦU TIÊN (14/10/2026)

**Đã chuyển** `eCommerceMetrics` + `eCommerceMetricNames` + `eCommerceMetricTags` sang
**`shared/eCommerce.Kernel/Observability/`** (namespace `eCommerce.Kernel.Observability`) và **xoá bản cũ**
ở `src/eCommerce.Domain.Shared/Observability/` — **một nguồn sự thật duy nhất**.

**Đã wire điểm phát THẬT đầu tiên:** `IdempotencyGuard` (module `Messaging`) phát
`messaging_inbox_failed_total` kèm nhãn `module` + `reason` khi xử lý thất bại.

**ĐÃ KIỂM CHỨNG BẰNG TEST** (`MeterListener`, 2 test mới):
| Test | Khẳng định |
|---|---|
| `Xu_ly_loi_thi_PHAT_metric_messaging_inbox_failed_total` | Metric **được phát thật** với giá trị 1 và nhãn `module = messaging` |
| `Xu_ly_THANH_CONG_thi_KHONG_phat_metric_loi` | Đường **thành công KHÔNG** phát metric lỗi (nếu phát thì cảnh báo sẽ nhiễu vô ích) |

⇒ **12 metric nay đã có đường phát hợp lệ về mặt kiến trúc.** Đây là lần đầu tiên hệ thống có metric
**thực sự được phát ra** (trước đó: đăng ký đủ + test đủ nhưng **không nơi nào phát được** vì registry
nằm sai tầng).

### ⚠️ CÒN LẠI — các điểm phát khác (cùng một mẫu, đã hết chướng ngại kiến trúc)

| Metric | Nơi cần wire | Ghi chú |
|---|---|---|
| `lock_timeout_total` · `lock_skipped_total` | `ChannelTokenManager` (ChannelHub.Application) | Cần thêm `ProjectReference` Kernel cho `ChannelHub.Application` |
| `channel_sync_failed_total` | `ChannelShopSyncer` | Cùng project trên |
| `messaging_dlq_size` · `messaging_dlq_replayed_total` | Job DLQ | 🚧 **chưa có DLQ** (outbox/relay chưa làm) |
| `messaging_outbox_pending` | Relay outbox | 🚧 **chưa có relay** |
| `http_request_duration_seconds` | Middleware đo độ trễ | Cần một middleware mới trong Host |
| `tax_unclassified_skus` · `payment_webhook_rejected_total` · `logging_failures_total` | Nơi phát tương ứng | Mẫu đã chứng minh |

---

## 18. ĐIỂM PHÁT METRIC: 5/12 → **7/12** (14/10/2026)

Thêm 2 điểm phát trong cùng một tính năng — **job đếm SKU chưa phân loại thuế**:

| Metric | Nơi phát | Ghi chú |
|---|---|---|
| `tax_unclassified_skus` (gauge) | `TaxUnclassifiedSkusWorker` (tầng app) | Lõi đếm thuần: `TaxUnclassifiedSkuCounter` |
| `lock_skipped_total` | Cùng worker, khi **không giành được khoá** | Khác `lock_timeout_total` (đã có ở `ChannelTokenManager`) |

### 🎯 Vì sao đếm SKU chưa phân loại lại quan trọng về NGHIỆP VỤ

Quyết định **D5**: SKU chưa phân loại **vẫn cho bán** nhưng **CHẶN phát hành hoá đơn**
(`eCommerce:Tax:0003`). Nghĩa là SKU chưa phân loại là **việc đang tồn của kế toán**, và nó **chặn
doanh thu hợp pháp**. Không đo được con số này thì nó **chỉ lộ ra khi có khách xin hoá đơn** — lúc đó đã muộn.

### 🚨 SENTINEL `-1` = "CHƯA XÁC ĐỊNH" (KHÔNG để 0 khi đếm lỗi)

Gauge mặc định là `0`, mà `0` mang nghĩa **"kế toán không còn việc gì"**. Nếu việc đếm **thất bại**
(ví dụ bảng thuế suất trống) mà gauge vẫn là `0` thì hệ thống **nói dối một cách im lặng**.
Vì vậy: đếm lỗi ⇒ đặt gauge = **`-1`** (hằng số `TaxUnclassifiedSkuCounter.ChuaXacDinh`) và ghi log Error.
Dashboard **phải** phân biệt `-1` (chưa xác định) với `0` (đã xong).

### 🚨 HAI CHI TIẾT DỄ SAI ĐÃ XỬ LÝ

1. **`Timer.RunOnStart = true`**: gauge mặc định là 0 ⇒ nếu chờ hết nhịp 10 phút mới đếm lần đầu thì
   **10 phút đầu sau mỗi lần deploy, dashboard hiển thị 0 một cách SAI**. Đếm ngay khi khởi động.
2. **Đăng ký worker tường minh** trong `eCommerceApplicationModule.OnPostApplicationInitializationAsync`.
   Nếu chỉ có class mà không đăng ký thì **KHÔNG có lỗi build**, app vẫn khởi động, nhưng job
   **im lặng không bao giờ chạy** — và metric mãi mãi là 0. (Đây là dạng lỗi đã gặp 2 lần trong dự án.)

### 🚨 GIỚI HẠN ĐÃ BIẾT của con số này (ghi rõ để không hiểu nhầm)

Danh mục sản phẩm hiện **CHƯA có khái niệm `Category`** (`Product` chỉ có `BrandId`; `ProductVariant`
không có `CategoryId`) ⇒ chuỗi danh mục truyền vào resolver là **rỗng**, phân loại chỉ có thể ở **cấp SKU**.
Khi danh mục được thêm vào, **con số này sẽ GIẢM** vì nhiều SKU sẽ được phân loại theo danh mục — đó là
điều đúng, không phải lỗi. `TaxRateResolver` đã sẵn sàng cho cấp danh mục.

### ✅ ĐÃ KIỂM CHỨNG END-TO-END TRÊN POSTGRESQL THẬT (không chỉ unit test)

| Bước | Bằng chứng |
|---|---|
| Worker khởi động | `Started background worker: eCommerce.Tax.TaxUnclassifiedSkusWorker` |
| Chèn **1** SKU đang bán + **0** phân loại thuế vào DB thật | `CatProductVariants` = 1 dòng |
| Worker **THỰC SỰ CHẠY** và đếm đúng | log: `Đếm SKU chưa phân loại thuế: 1/1 SKU đang bán chưa phân loại.` |
| Cảnh báo hành động được | log Warning kèm `eCommerce:Tax:0003` |
| Dọn dẹp | `CatProductVariants` = 0, `CatProducts` = 0 |

### CÒN LẠI 5/12

> 🚨 **ĐÍNH CHÍNH 21/10/2026 (`S10`):** bảng dưới đây **đã lỗi thời** — `messaging_dlq_size` **đã** có nơi phát
> (`S6`: `MarketplaceReconciliationWorker`), và con số đúng là **11/15**. 📌 Đọc **§19** (đo lại + cơ chế).
> 🔒 Giữ nguyên bảng cũ ở đây **có chủ ý**: xoá nó đi thì mất dấu vết rằng tài liệu này **đã từng tuyên bố một
> con số không ai kiểm** trong **8 ngày**.

| Metric | Cần gì |
|---|---|
| `messaging_outbox_pending` · `messaging_dlq_size` · `messaging_dlq_replayed_total` | 🏗️ Outbox + DLQ (**chưa có**) |
| `logging_failures_total` | 🏗️ Logger provider bắt lỗi sink |
| `lock_ttl_exceeded_total` | ⬜ Chưa có chỗ phát (cần nơi phát hiện vượt TTL khoá) |

---

## 19. 🚨 ĐÍNH CHÍNH + CƠ CHẾ: **11/15** metric có nơi phát (21/10/2026, `S10`)

### 19.1 Con số cũ SAI ở đâu — và vì sao nó sống được 8 ngày

| | Tài liệu nói (13/10/2026 → 21/10/2026) | Đo được (21/10/2026) |
|---|---|---|
| Metric **đăng ký** | 12 | **15** (`inventory_recon_mismatch_skus`, `payment_recon_unresolved_total`, `payment_recon_changed_total` được thêm sau mà §5.1 **không** cập nhật) |
| Metric **có nơi phát** | "12/12 — ĐÃ CÓ NƠI PHÁT" | **11/15** |
| Phép kiểm đối chiếu *"đăng ký ↔ có chỗ gọi"* | ❌ **KHÔNG CÓ** | ✅ `MetricEmitterTests` (3 ca) |

🔑 **Vì sao nó sống được:** `MetricsTests.TC06` chỉ chứng minh **instrument được TẠO** (constructor của
`eCommerceMetrics`) — 📌 nó **không** và **không thể** chứng minh *"có luồng nghiệp vụ nào gọi"*. 🔒 Và vì
**một phép kiểm dễ** đã xanh, người ta **tưởng** câu hỏi khó đã được trả lời.
⚠️ Đây đúng là điều `NOT-002` **F3** đã cảnh báo: *"đã đăng ký & đã có nơi phát (constructor) nhưng CHƯA được
gọi ở luồng nghiệp vụ nào"* — 📌 cảnh báo đó **đúng**, và nó **không được đo** cho tới `S10`.

### 19.2 ✅ Cách đóng — bằng **mã**, không bằng chữ

| Thành phần | Ở đâu | Khoá điều gì |
|---|---|---|
| `eCommerceMetricNames.KyHieuMetric` | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs` | **Ký hiệu** phải tìm ở nơi phát — dùng `nameof(...)` ⇒ 🔒 đổi tên thành viên là **lỗi build** |
| `eCommerceMetricNames.ChuaCoNoiPhat` | cùng tệp | **Danh sách nợ TƯỜNG MINH** + **lý do** + **hạ tầng còn thiếu** |
| `MetricEmitterTests` | `test/eCommerce.Tests/Observability/` | ① metric **không khai nợ** mà **không** có lời gọi ⇒ ĐỎ · ② khai nợ mà **có** lời gọi ⇒ ĐỎ (khai cũ) · ③ bảng ký hiệu phủ **đúng** `All` · ④ phép quét phải thấy **≥ 11** điểm phát (chống "phạm vi quét bị thu hẹp") |

🚨 **HAI CHIỀU đã được TIÊM để chứng minh (không tin "test xanh"):**
① bỏ `metric.SetMessagingDlqSize(...)` ⇒ **2 ca ĐỎ**, thông điệp nêu đúng `messaging_dlq_size`;
② khai `lock_timeout_total` vào bảng nợ ⇒ **1 ca ĐỎ**, thông điệp *"Khai 'CHUA CO NOI PHAT' nhung DA CO loi goi"*;
③ hoàn nguyên **cả hai** ⇒ xanh. 📌 Đây là lần thứ **N** của bài học: *"luôn đúng" và "không hoạt động" trông
giống hệt nhau*.

### 19.3 🔒 3 nợ còn lại — nêu rõ **cần gì**, không giả vờ

| Metric | Hạ tầng còn thiếu | Thuộc về |
|---|---|---|
| `messaging_outbox_pending` | **Outbox + relay** (`XC-002`): hiện sự kiện đi **in-process**, **không có bảng outbox** ⇒ 📌 mọi con số sẽ là **BỊA** | `XC-002` |
| `messaging_dlq_replayed_total` | **Luồng phát lại thủ công** có thật. `DeadLetterAppService` **cố ý** chỉ đọc + đánh dấu ⇒ 🔒 đếm *"đánh dấu"* thành *"phát lại"* là **nói dối** | `XC-002` |
| `lock_ttl_exceeded_total` | **Wrapper khoá** đọc được **TTL**; `IAbpDistributedLock` không lộ TTL ra ngoài | `XC-003` |
| `logging_failures_total` | **Logger provider** bọc sink để bắt lỗi ghi log | `XC-005` §6.2 |

📌 **Vì sao `S10` không "làm cho xong" 4 mục này:** cả bốn đòi **hạ tầng mới** (outbox/relay · luồng phát lại ·
wrapper khoá · logging provider) ⇒ 🔒 làm gấp trong một phiên đóng nợ là cách tạo **hai nguồn sự thật**.
✅ Thay vào đó: **đo được · khai báo được · khoá được** ⇒ 📌 con số *"11/15"* **không thể trôi** nữa.

> ⚠️ **CẬP NHẬT 29/10/2026:** bảng trên là **ảnh chụp `S10`**. `S16` (xem **§20**) đã đóng
> `lock_ttl_exceeded_total` + `logging_failures_total`; `messaging_outbox_pending` đã đóng từ 24/10 (`XC-002` §17).
> ✅ **`S16b` (xem §21) đã đóng nốt `messaging_dlq_replayed_total`** ⇒ **không còn nợ metric nào**.

---

## 20. `S16` — 2/3 metric còn nợ đã có ĐIỂM PHÁT THẬT (29/10/2026)

> **Mục tiêu:** đóng các metric còn nợ ở §19.3 **bằng hạ tầng thật**, không bằng cách thêm tên vào bảng.
> 🔒 Ràng buộc: `MetricEmitterTests` kiểm **hai chiều** — metric khai nợ mà có lời gọi ⇒ ĐỎ; metric không khai nợ
> mà không có lời gọi ⇒ ĐỎ. Vì vậy mỗi mục dưới đây phải có **đường phát thật + test chứng minh cả hai chiều**.

### 20.1 Kết quả đo được

| Metric | Trạng thái `S16` | Điểm phát thật | Hạ tầng mới |
|---|---|---|---|
| `lock_ttl_exceeded_total` | ✅ **XONG** | `DoThoiGianGiuKhoa` — decorator `IAbpDistributedLock` | bọc descriptor + nghe `IDistributedSynchronizationHandle.HandleLostToken` |
| `logging_failures_total` | ✅ **XONG** | `DemLoiGhiLogProvider` — bọc **mọi** `ILoggerProvider` ở **cả Host và Web** | provider/logger decorator + dòng dự phòng `Console.Error` |
| `messaging_dlq_replayed_total` | ❌ **CÒN NỢ** | — | **chưa có luồng phát lại DLQ** (xem §20.3) |

📌 **Đếm được sau `S16`: 15/16 metric có điểm phát · 1/16 còn nợ.** Con số này là kết quả của
`MetricEmitterTests` (quét `src/` + `modules/` + `shared/`), không phải lời khai trong bảng.

### 20.2 Hai metric đã đóng — và một ĐÍNH CHÍNH quan trọng

#### (a) `lock_ttl_exceeded_total`

🚨 **ĐÍNH CHÍNH KỸ THUẬT (đo từ mã ABP 10.7, không suy đoán):** tham số thứ hai của
`IAbpDistributedLock.TryAcquireAsync(name, timeout, ct)` là **thời gian CHỜ giành khoá** —
`MedallionAbpDistributedLock` chuyển thẳng nó xuống `IDistributedLockProvider.TryAcquireLockAsync(key, timeout, ct)`.
**Nó KHÔNG phải TTL của lease.** TTL thật do Medallion giữ (mặc định 30 s) và **tự động gia hạn** khi handle còn sống.
⇒ Nếu lấy `timeout` làm TTL và so "giữ lâu hơn" thì một job quét hợp pháp giữ khoá 60 s sẽ **báo động giả** —
đúng loại metric nói dối mà tài liệu này cấm.

✅ **Cách đo thật đã dùng:** Medallion phơi `IDistributedSynchronizationHandle.HandleLostToken`; token **chỉ bị huỷ
khi lease đã mất** (hết TTL mà không gia hạn được: tiến trình treo, mất Redis, tranh chấp). ABP có hàm mở handle
`AbpDistributedLockHandleExtensions.ToDistributedSynchronizationHandle()`. Decorator:

1. chuyển tiếp **nguyên vẹn** `TryAcquireAsync` (kể cả `null` khi không giành được khoá);
2. khi có handle, đăng ký `HandleLostToken` ⇒ tăng counter **đúng một lần** cho mỗi handle bị mất;
3. khi handle được trả, **ngắt đăng ký trước** rồi mới dispose handle ⇒ mất khoá *sau khi đã trả* không bị đếm.

📌 Nhãn `module` lấy từ **tiền tố hữu hạn** của tên khoá (`inventory`, `catalog`, `sales`, `payment`…);
không gắn cả tên khoá vì nó chứa tenant/SKU/shopId (cardinality cao — BR-009).

⚠️ **Giới hạn đã biết:** nếu provider không phải Medallion thì không có `HandleLostToken`; decorator vẫn chuyển
tiếp handle như cũ nhưng **không quan sát được** metric. Đây là lý do nó được đăng ký ở host — nơi provider
Medallion/Redis được chỉ định tường minh — chứ không bọc mù mọi môi trường.

#### (b) `logging_failures_total`

`DemLoiGhiLogProvider` bọc **từng** `ILoggerProvider` (console, debug, event source…) và trả một `ILogger` bọc.
Nó được đăng ký ở **cả `eCommerceHttpApiHostModule` và `eCommerceWebModule`**: hai tiến trình có bộ provider
riêng, nên chỉ bọc Host sẽ để lỗi ghi log của UI **vô hình** (counter = 0 vì không ai đo, không phải vì không lỗi).
Mọi `IsEnabled` / `BeginScope` / `Log` được chuyển tiếp. Khi provider bên trong ném:

1. **ghi ngay** một dòng dự phòng ra `Console.Error` (hoặc sink tiêm vào khi test) — **không nuốt log**;
2. tăng `logging_failures_total` **một lần**.

🔒 **Không đệ quy:** dòng dự phòng dùng `Console.Error` trực tiếp, **không** đi qua `ILogger`; nếu chính việc tăng
metric ném thì exception bị chặn **sau khi** dòng dự phòng đã được ghi. Scope được chuyển tiếp qua
`ISupportExternalScope` để không đổi hành vi log hiện có.

### 20.3 Vì sao `messaging_dlq_replayed_total` VẪN còn nợ (ở `S16`) — ✅ **ĐÃ ĐÓNG Ở `S16b`, XEM §21**

`DeadLetterAppService` hiện **chỉ đọc + đánh dấu đã xử lý**; **không có** đường phát lại nào:
không endpoint replay, không deserialize payload thành event, không publish qua `IDistributedEventBus`, không có
cờ/idempotency cho lần phát lại. 📌 Đếm "đã đánh dấu" thành "đã phát lại" là **nói dối** — đúng điều §19.3 đã chặn.
⇒ `S16` **giữ nguyên** tên này trong `ChuaCoNoiPhat` kèm lý do; muốn đóng phải làm **một luồng replay thật**
(đọc `MsgDeadLetters` → phát lại event qua outbox → đánh dấu đã replay → idempotent) và chỉ khi đó mới đặt lời gọi
`MessagingDlqReplayed.Add(...)` tại **chính nơi phát lại**.

> ✅ **`S16b` (29/10/2026) làm đúng 4 bước trên** — xem **§21**. Mục này giữ lại **nguyên văn** vì nó là
> **lý do** (không phải mô tả trạng thái hiện tại): 🔒 đó cũng là điều kiện để lần sau không ai "đóng nợ"
> bằng cách đếm lần **đánh dấu**.

### 20.4 Bằng chứng HAI CHIỀU (test mới)

`test/eCommerce.Tests/Observability/S16MetricTests.cs` — **7 ca**:

| Ca | Chiều | Khẳng định |
|---|---|---|
| `Lock_ttl_exceeded_PHAT_khi_lease_MAT_THAT` | **CÓ** sự kiện | `HandleLostToken` huỷ ⇒ counter = 1 · nhãn `module=inventory` · huỷ lặp lại vẫn chỉ 1 |
| `Lock_ttl_exceeded_KHONG_PHAT_khi_tra_khoa_binh_thuong` | **KHÔNG** có sự kiện | trả handle bình thường ⇒ counter = 0 |
| `Lock_ttl_exceeded_KHONG_PHAT_neu_lease_mat_SAU_khi_da_tra` | **KHÔNG** có sự kiện | mất token sau khi dispose ⇒ counter = 0 (đã ngắt đăng ký) |
| `Wrapper_lock_KHONG_doi_hanh_vi_khi_khong_gianh_duoc_khoa` | tương thích | `null` vẫn là `null`, tên khoá vẫn được chuyển tiếp |
| `Logging_failures_PHAT_khi_sink_NEM__va_KHONG_mat_thong_diep` | **CÓ** sự kiện | sink ném ⇒ counter = 1 · dòng dự phòng chứa **nguyên** message + exception |
| `Logging_failures_KHONG_PHAT_khi_sink_binh_thuong` | **KHÔNG** có sự kiện | sink bình thường ⇒ counter = 0 · message vẫn đến sink |
| `Dang_ky_logger_boc_DUNG_provider_trong_DI__va_van_phat_metric` | hạ tầng DI | `LoggerFactory` thật + provider thật bị bọc ⇒ sink ném vẫn phát metric |

📌 **Không** có ca nào "chỉ kiểm constructor": metric chỉ xanh khi **sự kiện chạy qua đường phát thật**, và
**đỏ** (hoặc không đếm) khi sự kiện không xảy ra.

### 20.5 Ảnh hưởng vận hành

- **`lock_ttl_exceeded_total > 0`** = một tiến trình **đã thực sự mất lease** trước khi trả khoá. Đây là bất
  thường thật (treo/GC dài/mất Redis), không phải "job chạy lâu". Khi thấy: kiểm tra Redis, thời gian GC/thread
  pool của instance, và mức `ExtensionCadence` của Medallion.
- **`logging_failures_total > 0`** = ít nhất một sink không ghi được. Dòng `[LOGGING-FAILURE]` ở `stderr` vẫn giữ
  category/level/eventId/message gốc để không mất dấu; điều tra sink (đĩa đầy, collector chết, quyền ghi).
- **Không đổi schema/migration/quyền/route/DTO.** Chỉ thêm hai decorator ở host + hai ca metric.

### 20.6 Đo cổng cuối (cây đã sửa)

- `pwsh -File tools\kiem-tat-ca.ps1 -DayDu`: **exit 0 · 370.7s · 15/15 bước**.
- Unit **1612/1612** (tăng **7** từ 1605) · integration **87/87** · E2E **145/145** · DB **25/25** · NND **64** ·
  **71** quyền · migration **49** · quét **99** chỗ.
- `npm --prefix tools run check:mermaid`: **213 khối · 0 lỗi** · `pwsh -File tools\kiem-link-tai-lieu.ps1`:
  **296 link + 325 đường dẫn backtick · 0 chết**.

---

## 21. `S16b` — REPLAY DLQ THẬT: metric **16/16** (29/10/2026)

> **Mục tiêu:** đóng nợ **cuối cùng** của §19.3 — `messaging_dlq_replayed_total` — **bằng hạ tầng thật**.
> 🔒 **Định nghĩa do chủ dự án chốt: "replay" = PHÁT LẠI EVENT LÊN BUS**, **KHÔNG** phải *"đánh dấu đã xử lý"*.
> 📌 Vì vậy mọi thứ dưới đây xoay quanh **một** câu hỏi: *"event có thật sự đi không?"* — không phải *"cờ có bật không?"*.

### 21.1 Vì sao "đánh dấu" không phải "phát lại" — và cái bẫy của nó

`PostDanhDauDaXuLyAsync` đã tồn tại từ `XC-002` §16 và nó **ghi `DaXuLy = true`**. Nếu đặt lời gọi
`MessagingDlqReplayed.Add(1)` **ngay tại đó** thì:

- metric **tăng** — trông như *"đã phát lại"*;
- nhưng **không có event nào đi** ⇒ consumer **không bao giờ** nhận;
- 📌 và **không có lỗi nào** để lần ra: dòng DLQ *"đã xử lý"*, biểu đồ *"có phát lại"*, còn sự kiện **đã mất**.

🔑 Đây đúng là kiểu **nói dối bằng observability** mà §19.3 đã chặn trước khi có luồng thật. `S16b` **không**
đổi cách đánh dấu; nó thêm một **đường khác** — đường **publish**.

### 21.2 Hợp đồng đã hiện thực

| Hạng mục | Giá trị | Vì sao chốt như vậy |
|---|---|---|
| Entity | `DeadLetterMessage.ReplayedAtUtc` · `ReplayCount` · `ReplayedBy` (+ `DaPhatLai`) | 🔒 **cột RIÊNG**, không suy từ `DaXuLy`: *"đã bỏ qua"* ≠ *"đã phát lại"* |
| Migration | `20261029033402_Xc002_DlqReplay` (**50**) — 3 `AddColumn`, **không** index/`CHECK` mới | NND **giữ 64**; bất biến idempotency nằm ở entity + app service |
| `schema.sql` | **sinh lại**; diff **chỉ thêm** 3 cột | nếu quên ⇒ integration test đỏ vì cột **không tồn tại** |
| API | `IDeadLetterAppService.PhatLaiAsync(Guid id)` → `PhatLaiDlqKetQuaDto` · route mới `POST /api/app/dead-letter/{id}/phat-lai` | **không** đổi route/DTO cũ; DTO trả **cả** *"đã phát"* và *"bỏ qua + lý do"* |
| Quyền | **`eCommerce.Operations.DeadLetters.Replay`** (khai **cả hai nơi** + `quyenPhaiCon` + `DbMigrator`) | phát lại = **gửi sự kiện vào hệ thống** ⇒ tách khỏi quyền **đọc** (đặc quyền tối thiểu) |
| Metric | `messaging_dlq_replayed_total` +1 **sau commit**, nhãn `result=ok` · `eventName` | tăng **trước** commit là đếm một lần có thể **rollback** |
| UoW | `Begin(requiresNew: true, isTransactional: true)`; publish `useOutbox: true`, `onUnitOfWorkComplete: false` | dấu phát lại + dòng **outbox** cùng **một** transaction ⇒ không mất, không phát hai lần |
| HTTP status | `0008`/`0009`/`0010` ⇒ **409**; `0007` ⇒ **404** | ABP mặc định trả **403** ⇒ client đọc thành *"thiếu quyền"* và **đi xin quyền** thay vì sửa dữ liệu |

### 21.3 Idempotency: **một dòng DLQ — một lần phát lại**

Thứ tự trong `PhatLaiAsync` (mỗi bước là một quyết định, không phải thứ tự tuỳ ý):

1. **chặn trùng TRƯỚC** (`ban.DaPhatLai`) ⇒ trả DTO `DaPhatLai = false` + lý do. 📌 **Không ném**: bấm hai lần là
   chuyện thường; ném sẽ khiến người dùng tưởng **hệ thống hỏng**;
2. **ánh xạ `TenSuKien` → `Type`** (fail-closed `0009`);
3. **deserialize payload** (fail-closed `0010`);
4. **ghi dấu** (`DanhDauDaPhatLai` + `DanhDauDaXuLy`) — 🔒 *sau* khi biết chắc payload hợp lệ ⇒ không bao giờ có
   dấu *"đã phát lại"* cho một event chưa đi;
5. **publish** trong UoW (outbox);
6. **commit**, rồi **mới** tăng metric.

🔒 **Vì sao `DaXuLy` KHÔNG làm khoá idempotency:** một dòng có thể bị đánh dấu **bỏ qua có lý do** (không phát lại).
Nếu dùng `DaXuLy` làm khoá thì lần phát lại **đầu tiên** của dòng đó **bị chặn** — hoặc tệ hơn: một dòng **đã**
phát lại bị coi như chưa ⇒ phát **hai lần** ⇒ consumer nhận trùng.

➕ **Hai lớp chặn:** app service kiểm trước (trả lý do) **và** entity `DanhDauDaPhatLai` **NÉM** `0008` nếu bị gọi
lần hai. 🚨 Nếu hai request **đồng thời** cùng qua cửa trước, `ConcurrencyStamp` của aggregate làm **một** UoW
rollback ⇒ dòng **outbox của nó cũng rollback** ⇒ chỉ **một** event đi. 📌 Đây là lý do phải publish **trong**
UoW chứ không phải sau.

### 21.4 Fail-closed: **hai cửa**, và cả hai đều **không** publish

| Cửa | Ca | Kết quả |
|---|---|---|
| `IDeadLetterEventTypeResolver` | `TenSuKien` không khớp loại nào **hoặc** khớp **≥ 2** loại cùng tên ngắn | **NÉM** `eCommerce:Messaging:0009` (kèm danh sách ứng viên) |
| Deserialize | payload không phải JSON object của loại đã ánh xạ (mảng, thiếu trường, sai kiểu) | **NÉM** `eCommerce:Messaging:0010` |

🚨 **Vì sao không có "phương án dự phòng":** publish một `object` *"đoán"* nghĩa là consumer **không có handler**
⇒ sự kiện **rơi vào khoảng không**, trong khi dấu `ReplayedAtUtc` đã ghi ⇒ **mất bằng chứng + mất sự kiện**.
📌 Bộ ánh xạ chấp nhận **cả** tên ngắn (`nameof(...)` — dạng `DeadLetterWriter` đang ghi) **và** tên đầy đủ
(dạng ABP dùng làm tên event), nhưng **chỉ khi khớp duy nhất**.

### 21.5 Bằng chứng **HAI CHIỀU** (không chỉ "test xanh")

**(a) Unit — `test/eCommerce.Tests/Messaging/DeadLetterPhatLaiTests.cs` · 11 ca.** Bus/UoW là **test double**,
metric là `MeterListener` **thật**, entity là **thật**:

| Ca | Chiều | Khẳng định |
|---|---|---|
| `Phat_lai_MOT_lan__…` | **CÓ** sự kiện | publish **đúng 1** lần, đúng `Type` + đúng **dữ liệu gốc**; `useOutbox = true`; `onUnitOfWorkComplete = false`; publish **TRƯỚC** `Complete`; `ReplayedAtUtc`/`ReplayCount = 1`/`ReplayedBy` được ghi; metric **+1** với `result=ok` |
| `Goi_LAI_cung_dong__…` | **KHÔNG** có sự kiện | không publish lần hai; metric **không** tăng; dấu **không** bị ghi đè; DTO trả **lý do** *"BỎ QUA … MỘT lần"* |
| `TenSuKien_khong_anh_xa_duoc__…` | **FAIL-CLOSED** | **NÉM** `0009`; **không** publish; metric **0**; **không** ghi dấu; UoW **không** complete |
| `Payload_khong_khop_loai__…` | **FAIL-CLOSED** | **NÉM** `0010`; **không** publish; metric **0** |
| `Dong_khong_ton_tai__…` | biên | **NÉM** `0007`; không publish |
| `Bo_anh_xa_…` (5 dòng `Theory`) | hợp đồng tên | tên ngắn · tên đầy đủ · `"FullName, Assembly"` ⇒ tìm thấy; tên lạ/rỗng ⇒ **NÉM** |
| `Ten_NGAN_nhap_nhang__…` | fail-closed | 2 loại **cùng tên ngắn** ⇒ **NÉM**; tên **đầy đủ** vẫn phân biệt được |

**(b) Integration — `test/eCommerce.IntegrationTests/PostgreSqlS16bDlqReplayTests.cs` · 3 ca** (PostgreSQL 17 thật,
schema sinh từ migration): dòng **chưa** phát lại ⇒ `ReplayCount = 0` + `NULL`; dòng **đã** phát lại ⇒ đọc lại
**đúng** mốc (micro-giây, `Kind = Utc`)/người/bộ đếm; `ReplayedBy` **256 ký tự lọt**, **257 bị chặn**
(`22001 StringDataRightTruncation`).

**(c) Probe HTTP thật — `tools/probe-dlq-replay.ps1` · 21/21** (chạy tay; **không** thêm bước cổng mới):

| Ca | Đo được |
|---|---|
| `D05` | route **thật** từ swagger: `POST /api/app/dead-letter/{id}/phat-lai` |
| `D06`–`D08` | `200` · `daPhatLai = true` · DB `1|true|true|admin` |
| 🚨 `D09` | **event ĐÃ ĐI THẬT**: `AbpEventInbox` có dòng **MỚI** cho event (outbox → relay 2 s → consumer) |
| `D10` | outbox **về 0** (relay đã phát, không treo) |
| `D11`–`D15` | lần hai: `200` + `daPhatLai = false` + lý do *"BỎ QUA"*; **không** dòng inbox thứ hai; `ReplayCount` **vẫn 1** |
| 🚨 `D17`–`D20` | `TenSuKien` không ánh xạ được ⇒ **409** (không phải `403`), **không** ghi dấu, outbox **0**, inbox mới **0** |
| `D16` | dọn sạch: DLQ probe **0** · outbox **0** · cờ bảo mật admin **true** |

🔑 **`D09` là ca quan trọng nhất:** nó là **thứ duy nhất** phân biệt *"đã gọi publish"* với *"event đã đi"*.
📌 Một unit test với bus giả **không thể** chứng minh điều đó — đúng bài học §19.1.

### 21.6 Đo cổng cuối (cây đã sửa)

- `pwsh -File tools\kiem-tat-ca.ps1 -DayDu`: **exit 0 · 377.4s · 15/15 bước**.
- Unit **1627/1627** (tăng **11**) · integration **90/90** (tăng **3**) · E2E **159/159** · DB **25/25** ·
  NND **64** (không đổi — không thêm unique index) · **72** quyền (tăng **1** — `…DeadLetters.Replay`) ·
  migration **50** (tăng **1**) · quét **99** chỗ (không đổi — không bảng mới).
- `check:mermaid` **213 khối · 0 lỗi** · `kiem-link-tai-lieu` **0 chết**.

### 21.7 Giới hạn & phần còn nợ (nói rõ, không giấu)

1. **KHÔNG có phát lại tự động** (job/retry) — cố ý: phát lại có thể **hỏng lại**. Đường gọi là **có người** +
   quyền riêng + audit.
2. **Phát lại không sửa dữ liệu gốc** ⇒ nếu nguyên nhân là dữ liệu sai thì nó **hỏng lại** và sinh **một dòng DLQ
   MỚI** (dòng cũ **vẫn** giữ làm bằng chứng). 📌 Đây là hành vi đúng, nhưng người trực phải biết: *"phát lại"*
   không phải *"sửa"*.
3. **Bộ ánh xạ chỉ thấy assembly ĐÃ NẠP** (`eCommerce*`). Loại sự kiện thuộc assembly chưa nạp ⇒ **NÉM**
   (hướng an toàn) — không publish sai.
4. **Chưa có UI** cho nút phát lại (API + quyền đã có; `/VanHanh/Dlq` vẫn là nợ của `XC-002` §16.5 #4).
5. **Chưa có phát lại theo LÔ** (spec `XC-002` §6.1 có `ReplayBatchAsync` — **chưa** hiện thực; làm lô cần
   **giới hạn số dòng + báo cáo từng dòng**, không được "thử hết rồi kệ").
