# `NOT-001` — Kiến trúc liên module: template, hàng đợi và chống gửi trùng

> Vai **ARCH**, phiên **N83**, chuỗi `gd3-1`, hàng `W22-1` (bước **F0–F2b** của `WP-22`), nhánh `feat/gd3-wp22-thong-bao`.
> Tài liệu này **chỉ thiết kế** phần **code mới** của
> [`not-001-template-hang-doi-thong-bao.md`](./not-001-template-hang-doi-thong-bao.md) (sau đây gọi **SRS**, `v0.5 — 26/11/2026`).
> **Không** code, **không** sinh migration, **không** sửa SRS.
>
> **Module chủ của mọi bảng/DTO/endpoint/ETO/mã lỗi dưới đây: `Notification`** — module **chưa có một dòng code nào**
> (xác nhận 4 vòng bằng đồ thị mã: `docs/reviews/NOT-001-docreview.md:49`). Toàn bộ mục dưới là **thiết kế MỚI, kế hoạch GĐ3**.
> Mọi `file:dòng` nêu trong tệp đã đọc/đối chiếu trong phiên N83 (`codegraph_explore` trước, `Read`/`Grep` sau cho phần đồ thị cắt bớt).
>
> **Nguồn quyết định:** `D-197` (một module `Notification`), `D-198` (lô + duyệt), `D-199` (GĐ3 chỉ `Text`), `D-191` (PII tắt),
> `D-193` (chi phí kỳ thuộc `NOT-002`), `D-208` mục (6) (tạm duyệt có điều kiện), `D-253` (tạm duyệt có hiệu lực cho code WP-22 **trên nhánh**)
> — `docs/nhanh/decisions.md:205,207,211,212,213,222,267`.
>
> 📄 **Loại tài liệu:** `-arch` là tài liệu **bổ trợ**, **ngoài** mẫu 16 mục / BR / mục "Đối chiếu mô hình Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó.
> Vì vậy tệp này không có `## 0.`…`## 15.` là **đúng chủng loại** (khuôn `docs/features/address/ADDR-002-arch.md:12-14`).

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đang có trong code (đọc từ code, không viết theo trí nhớ)

| Thành phần | Hiện trạng | Bằng chứng (đường dẫn thật) |
|---|---|---|
| Module `Notification` | ❌ **CHƯA CÓ** — `modules/` có 15 thư mục, **không** có `Notification` | liệt kê `modules/` phiên N83 |
| Code template/hàng đợi/render | ❌ **0 dòng** — graph không có `NotificationTemplate`/`NotificationQueueItems`/`NotificationErrorCodes` | `docs/reviews/NOT-001-docreview.md:49` (4 vòng) |
| `DbSet` + `OnModelCreating` hợp nhất | ✅ `eCommerceDbContext.OnModelCreating` gọi **10** hàm `ConfigureXxx` của module (+ `ConfigureCommerce` gọi thêm 6; ABP tự cấu hình 9), **không** có `ConfigureNotification`; outbox/inbox đã bật | `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:47,50,353-391` (đặc biệt `:389-390`) |
| Khuôn cấu hình EF của module | ✅ `ConfigureChannelHub` gọi từ DbContext hợp nhất | `modules/ChannelHub/eCommerce.ChannelHub.EntityFrameworkCore/ChannelHubDbContextModelCreatingExtensions.cs:14` |
| `AbpBackgroundJobs` (job bền vững) | ✅ **CÓ** — module EF đã đăng ký `AbpBackgroundJobsEntityFrameworkCoreModule` | `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:47` |
| Worker định kỳ chạy thật + supervisor đã sửa starvation | ✅ `ChannelSyncSupervisorWorker` — `RegisteredWorkers` + **chỉ** `AddAsync` khi chưa có/nhịp đổi | `modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelSyncSupervisorWorker.cs:30,47-48,51,58,97-101,103,121,132,139` |
| Khoá phân tán + `KeyPrefix` | ✅ `KeyPrefix = "ecommerce:"` (ghép **không** dấu phân cách); Redis `AbortOnConnectFail = false` | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:231,246,260` |
| Khuôn khoá của module (không tiền tố) | ✅ `ChannelHubConsts.LockKeys` trả `channel:…`; `CustomersConsts.LockKeys` trả `customers:…` | `modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:19-32` · `modules/Customers/eCommerce.Customers.Domain/CustomersConsts.cs:13-35` |
| Mã hoá at-rest | ✅ `IStringEncryptionService` inject trong tầng Application | `modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelTokenManager.cs:28,160-165` |
| `Scalar` khoá lock cho metric nhãn `module` | ⚠️ `TienToModule` **chưa** có `"notification"` ⇒ khoá mới sẽ rơi nhãn `other` | `src/eCommerce.Application/Observability/DoThoiGianGiuKhoa.cs:35-39,66-78` |
| Registry metric | ⚠️ **19** tên (không còn 16); `ChuaCoNoiPhat` **rỗng**; test kiểm **hai chiều** | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:13,94-124,152-174,189-200` · `test/eCommerce.Tests/Observability/MetricsTests.cs:44-45,55,72-73,100-103` |
| Nhãn metric | ✅ `Allowed` 15 nhãn; `Forbidden` có `email`,`phone`,`correlationId`… | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:238-248` |
| Bất biến unique index `NULLS NOT DISTINCT` | ✅ test chốt **94** index + danh sách **đích danh** | `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs:208,215-216,421-423` · `src/eCommerce.EntityFrameworkCore/Migrations/20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:8-25,39-86` |
| `[Authorize]` cấp class + provider permission | ✅ mẫu đúng; `GroupName = "eCommerce"`, các nhóm là lớp con | `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:21-25` · `src/eCommerce.Domain.Shared/eCommerceConsts.cs:15-393` |
| Sổ cái append-only | ✅ `StockLedgerEntry` *"APPEND-ONLY. Không bao giờ UPDATE/DELETE"* | `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:240,243` |
| Đánh dấu sự kiện **thông báo** | ✅ `INotificationEvent` ở Kernel — bộ dò độ phủ sự kiện phân loại theo interface | `shared/eCommerce.Kernel/Events/INotificationEvent.cs:31` · `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:586-602` |
| Outbox publish | ✅ `PublishAsync(..., useOutbox: true)` | `modules/Address/eCommerce.Address.Application/Sync/AdministrativeUnitSyncWorker.cs:210` · `src/eCommerce.Application/Messaging/DeadLetterAppService.cs:300-305` |
| `Messaging` có project gì | ⚠️ **chỉ** `Domain` + `EntityFrameworkCore` — **không có** `Application.Contracts` | liệt kê `modules/Messaging/` phiên N83 |

> 🚨 **Kết luận:** *toàn bộ `NOT-001` là thiết kế mới.* Thiết kế dưới đây chỉ dựa trên **tiền lệ thật** ở bảng trên.

### 1.2 Phạm vi `WP-22` (IN)

1. Tạo **module mới** `modules/Notification/` — **4 project** đúng `D-197` ①(1): `eCommerce.Notification.Domain` · `.Application.Contracts` · `.Application` · `.EntityFrameworkCore`, namespace `eCommerce.Notification`.
2. Registry template có **phiên bản bất biến** + engine render **hàm thuần** (`INotificationRenderer`).
3. Hàng đợi ý định bền vững trên PostgreSQL (`NotificationQueueItems`) + lô (`NotificationBatches`) + sổ sự kiện append-only (`NotificationQueueEvents`).
4. **Chống gửi trùng 5 lớp** — hàng rào thật ở DB (unique index + `UPDATE … WHERE Status = ChoGui`).
5. State machine **15 hàng** (§3 SRS) — **mỗi cạnh 1 hàm + NÉM lỗi + 1 event**.
6. Job bàn giao định kỳ + lease khôi phục; bàn giao qua `INotificationSender` (**hợp đồng nội module**, xem §4.4).
7. App service + REST + **10 permission** + map HTTP mã `0010–0020`.
8. Metric mới (7) + cập nhật `eCommerceMetricNames.All` + `KyHieuMetric` + `XC-005` §5.1 + test **trong cùng PR**.
9. Migration **expand-only** tạo 5 bảng + 7 unique index `NULLS NOT DISTINCT` + seed idempotent 3 template hệ thống.

### 1.3 Ngoài phạm vi (OUT)

- ❌ **Kênh gửi / vận chuyển / retry / DLQ / callback / quota / quiet hours / opt-out / sổ chi phí** — **`NOT-002`** (WP-23), `D-193`.
- ❌ Hạ tầng outbox/inbox của ABP — `XC-002` (đã có) — `NOT-001` **chỉ dùng**.
- ❌ Báo cáo/tổng hợp — `REP-001`/`REP-002` (read-model, chưa xây ở WP-22 ⇒ **hoãn endpoint tổng hợp**, xem §11/§15).
- ❌ Xuất liệu kế toán — `ACC-001`.
- ❌ Push/FCM/APNs; HTML/WYSIWYG (`D-199`).
- ❌ Định nghĩa ETO **đầu vào** từ module nghiệp vụ (biên #1) — xem §4.5 + câu hỏi `AQ1`.

### 1.4 Chừa chỗ cho `NOT-002` (WP-23) — **bắt buộc**

| Hạng mục | `NOT-001` (WP-22) | `NOT-002` (WP-23) | Cách chừa chỗ |
|---|---|---|---|
| Thư mục module | tạo `modules/Notification/` (4 project) | **thêm file** vào **cùng** 4 project | **không** tách project; `D-197` ①(7) |
| Cấu hình EF | `ConfigureNotification(this ModelBuilder)` trong `Notification.EntityFrameworkCore` | thêm `builder.Entity<…>(…)` vào **cùng** hàm | **một** hàm, gọi **một lần** — `eCommerceDbContext.cs:353-391` |
| `DbSet` | 5 `DbSet` của `NOT-001` | thêm `DbSet` của `NOT-002` | cùng `eCommerceDbContext` |
| Mã lỗi | `NotificationErrorCodes` dải `0010–0020` | **cùng file**, dải `0001–0009` (đã chiếm) | **một** tệp `NotificationErrorCodes.cs` (§3.7) |
| Enum kênh | `ChannelKind { Email=0, Sms=1, ZaloOa=2 }` | dùng lại; **đổi tên** `NotificationChannelKind` → `ChannelKind` cùng PR WP-23 | **một** enum (§3.1) — nợ `D-197` ⑥, `TC-44` |
| Khoá lock | `NotificationLockKeys` (file riêng) | thêm hàm vào **cùng** lớp | **một** lớp; cấm trùng tên khoá (§3.6) |
| Permission | lớp `Notifications` trong `eCommerceConsts.Permissions` — nhóm con `.Templates/.Queue/.Batches/.Reports` | thêm `.Messages/.Dlq/.Channels/.OptOut/.Cost` **vào cùng lớp** | **một** chỗ khai `eCommerceConsts.cs:394` (§8) |
| ETO | `NotificationEnqueuedEto`/`HandedOff`/`QueueFailed`/`BatchApproved` | `NotificationQueuedEto` **khác tên** | **cấm** trùng tên (§4.5) |
| Hợp đồng bàn giao | **định nghĩa** `INotificationSender` + DTO + sender **fail-closed** mặc định | **hiện thực** sender thật theo **đúng kiểu §4.4** | §4.4; `NOT-002` §6.1 **phải** đổi theo (`docDeviation #11`) |

---

## 2. Ranh giới module

### 2.1 Chiều phụ thuộc (một chiều, đúng `abp-module-rules` M1/M2/M5)

```
Host (eCommerce.HttpApi.Host)
  └─ [DependsOn] Notification.Application ──▶ Notification.Application.Contracts
                                             ├─▶ Notification.Domain
                                             └─▶ eCommerce.Kernel
     Notification.Application ──▶ Domain · Contracts · Kernel
                              ──▶ (ABP) Ddd.Application · BackgroundWorkers · DistributedLocking · EventBus · Security.Encryption
     Notification.Domain      ──▶ (ABP) Ddd.Domain · eCommerce.Kernel (Money)
     Notification.EntityFrameworkCore ──▶ Domain · (ABP) EntityFrameworkCore
eCommerce.EntityFrameworkCore ──▶ Notification.EntityFrameworkCore   (để gọi ConfigureNotification)
```

**So với tiền lệ:** `eCommerce.ChannelHub.Application.Contracts.csproj` ref **Domain + Kernel**; `eCommerce.ChannelHub.Application.csproj` ref **Domain + Kernel + Contracts** (+ `Messaging.Domain` cho probe) — xem 3 tệp csproj `modules/ChannelHub/*/`. `Notification` **bỏ** tham chiếu `Messaging` (lý do §2.3).

### 2.2 Luật áp cho WP-22

| # | Luật | Cách giữ |
|---|---|---|
| B1 | Module **sở hữu** 5 bảng + mọi entity/mã `00xx` | tất cả trong `modules/Notification/eCommerce.Notification.Domain`; **không** khai mã `eCommerce:Notification:*` ở `eCommerceConsts.ErrorCodes` |
| B2 | Gọi chéo module **chỉ** qua `*.Application.Contracts` hoặc ETO | `NOT-001` **không** tham chiếu module nào (không có `using eCommerce.Sales…`); `INotificationSender` **nội module** → **không** vi phạm `ADR-09` |
| B3 | Module **không** phụ thuộc ngược app/host | **0** `using eCommerce.Web/HttpApi/Application` trong `modules/Notification/**` |
| B4 | Không FK/navigation xuyên module | 5 bảng **chỉ** FK nội bộ (`Versions→Templates`, `Batches→Templates/Versions`, `QueueItems→Batches/Templates/Versions`, `Events→QueueItems`); **không** FK tới `OrdOrders`… |
| B5 | Mọi entity dùng `IRepository<T>` có `DbSet` hợp nhất | thêm **5** `DbSet` ở `eCommerceDbContext` (§5.6); thiếu ⇒ repo **không** đăng ký, lỗi im lặng |
| B6 | Mỗi project `.Application` có `AbpModule` **và** nằm trong `[DependsOn]` Host | thêm `typeof(Notification.NotificationApplicationModule)` vào `eCommerceHttpApiHostModule.cs:40-...` (khuôn `:59`) |
| B7 | `ConventionalControllers` đăng ký động cho assembly `eCommerce*.Application` đã nạp | **không** sửa chỗ đăng ký controller; chỉ cần module nạp |
| B8 | Không đặt bảng nghiệp vụ vào `Messaging` | 5 bảng ở `Notification`; `Messaging` **không** đổi |

### 2.3 Vì sao `Notification` **không** `[DependsOn] Messaging`

`D-197` ①(2) nói *"module `Notification` chỉ `[DependsOn]` **hợp đồng** của `Messaging` khi cần outbox"*. Đo code N83: `modules/Messaging/` **chỉ có** `eCommerce.Messaging.Domain` + `.EntityFrameworkCore`, **không có** `Application.Contracts`. Mà outbox/inbox của ABP **không** cần tham chiếu `Messaging`: bảng `AbpEventOutbox`/`AbpEventInbox` đã map trên **DbContext hợp nhất** (`eCommerceDbContext.cs:47,50,389-390`), publish bằng `IDistributedEventBus.PublishAsync(..., useOutbox: true)` (gói `Volo.Abp.EventBus`). ⇒ **Thiết kế: 0 tham chiếu `Messaging`.** Đúng **ý** `D-197` ①(2) (không gộp bảng; chỉ dùng hạ tầng outbox), không cần sửa quyết định.

### 2.4 Vi phạm ranh giới hiện có

**0 vi phạm ranh giới** trong code hiện tại liên quan `Notification` (module chưa tồn tại). Ghi nhận **nợ đã biết, không sửa** (ngoài WP): `ChannelShopTokenRefreshedEto` **thiếu** `CorrelationId` — `modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:116-125` (`NOT-001` **không** lặp lại — BR-018/`TC-17`).

---

## 3. Thiết kế tầng Domain

### 3.1 Enum (một nguồn, `D-197`)

Tệp: `modules/Notification/eCommerce.Notification.Domain/NotificationEnums.cs`

| Enum | Giá trị | Ghi chú |
|---|---|---|
| `ChannelKind` | `Email=0` · `Sms=1` · `ZaloOa=2` | **enum dùng chung** với `NOT-002`; **cấm** enum thứ hai; WP-23 đổi tên `NotificationChannelKind` → `ChannelKind` |
| `NotificationPurpose` | `Transactional=0` · `Marketing=1` · `OpsAlert=2` | quyết định khử trùng + (NOT-002) quiet hours |
| `NotificationTemplateStatus` | `Draft=0` · `Published=1` · `Archived=2` | **KHÔNG** sửa trực tiếp — chỉ qua hàm entity; nguồn SRS §5.1 (cột `Status`); chỉ thêm cuối (`D-17`) |
| `NotificationBodyFormat` | `Text=0` · `Html=1` | GĐ3 **chỉ** `Text`; `Html` **cuối** enum (`D-17`/`D-199`) |
| `NotificationQueueStatus` | `Moi=0` · `ChoDuyet=1` · `ChoGui=2` · `DangBanGiao=3` · `DaBanGiao=4` · `DaHuy=5` · `HetHan=6` · `Loi=7` | **chỉ thêm ở cuối** |
| `NotificationPlaceholderKind` | `Text=0` · `Money=1` · `DateTime=2` · `Number=3` · `Url=4` | `Money` nhận **chuỗi đã định dạng** (`ADR-07`) |
| `NotificationBatchStatus` | `ChoDuyet=0` · `DaDuyet=1` · `DaHuy=2` · `DaBanGiao=3` | hấp thụ `DaBanGiao`/`DaHuy` |
| `NotificationQueueEventType` | `Enqueued=0` · `ReadyToDispatch=1` · `Dispatching=2` · `HandedOff=3` · `LeaseExpired=4` · `Failed=5` · `Expired=6` · `Cancelled=7` · `Suppressed=8` · `Replayed=9` · `BatchApproved=10` · `QueuedForApproval=11` · `RetryScheduled=12` | **13** giá trị, chỉ thêm cuối |

> Tên trường/khoá theo `D-17` (enum lưu DB là `int`; CHECK ở §5).

### 3.2 Aggregate `NotificationTemplate` + `NotificationTemplateVersion`

- `NotificationTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant` — cột theo SRS §5.1. Hàm: `TaoMoi(...)`, `PublishVersion(versionId)`, `Archive()` (guard: đã `Archived` ⇒ NÉM `0011`). **Không** setter công khai cho `Status`.
- `NotificationTemplateVersion : CreationAuditedEntity<Guid>, IMultiTenant` (`modules/.../Templates/NotificationTemplateVersion.cs`). Sau `PublishedAtUtc`:
  - **không** setter công khai cho `Subject`/`BodyText`/`Placeholders`/`ContentHash`;
  - `SuaNoiDung(...)` **NÉM** `NotificationErrorCodes.PhienBanBatBien` (`0016`).
  - `ContentHash` = SHA-256 **canonical** của `Subject` + `BodyText` + `Placeholders` (sắp `Ordinal`), ghi **một lần** lúc publish.
- **Ghim version:** `NotificationQueueItem.TemplateVersionId` **bắt buộc**; version `Archived` **không** làm hỏng render (đọc bản ghi theo Id).

### 3.3 Aggregate `NotificationBatch`

`NotificationBatch : FullAuditedAggregateRoot<Guid>, IMultiTenant`. Hàm: `TaoMoi`, `Duyet(userId, now)` (guard: `Status != ChoDuyet` ⇒ NÉM `0011`; `CreatedByUserId == userId` ⇒ NÉM `0011` — tách nhiệm vụ, `TC-25`), `Huy(lyDo)`.

### 3.4 Aggregate `NotificationQueueItem` — trái tim chống gửi trùng

`NotificationQueueItem : FullAuditedAggregateRoot<Guid>, IMultiTenant`. Cột theo SRS §5.4. **State machine 15 hàng — mỗi hàng 1 hàm**:

| Hàng §3 | Hàm Domain | Guard (NÉM `0011` nếu sai) | Event phát |
|---|---|---|---|
| 1 | `TaoMoi(...)` (factory) | khoá chưa tồn tại — **không** ném; appender xử lý trùng | `Enqueued` |
| 2 | `DanhDauChoDuyet(batchId)` | đang `Moi` & có lô | `QueuedForApproval` |
| 3 | `DanhDauSanSang(now)` | đang `Moi` & `ScheduledAtUtc <= now` | `ReadyToDispatch` — 📌 **đường nóng** dùng `UPDATE … WHERE Status=Moi` rồi ghi event trong **cùng** UoW, không gọi hàm entity (§7.2 (C1)); hàm entity dùng cho đường không đua |
| 4 | `DuyetLo(userId)` | đang `ChoDuyet` | `BatchApproved` |
| 5 | `GianhBanGiao(lease, now)` | **không** gọi hàm Entity ở đường nóng — dùng `UPDATE … WHERE Status = ChoGui` (trả `bool`); hàm Entity chỉ dùng khi đã thắng | `Dispatching` |
| 6 | `DanhDauDaBanGiao(now)` | đang `DangBanGiao` | `HandedOff` |
| 7 | `KhoiPhucLease(now)` | đang `DangBanGiao` & `now > DispatchLeaseExpiresAtUtc`; **không** tăng `AttemptCount` | `LeaseExpired` |
| 8 | `DanhDauLoi(code, reasonMasked)` | đang `DangBanGiao` | `Failed` |
| 9 | `HetHanTuChoGui(now)` | đang `ChoGui` & `ExpiresAtUtc != null` & `now > ExpiresAtUtc` | `Expired` |
| 10 | `Huy(lyDo, userId)` | đang `Moi`/`ChoGui` (chưa hấp thụ) | `Cancelled` |
| 11 | **phát lại** = `TaoMoi` mục **mới** (`RootItemId`, khoá `:replay-{n}`) | khoá mới chưa tồn tại; mục gốc **ở lại** `Loi` | `Enqueued` (mới) + `Replayed` (gốc) |
| 12 | `ChuyenSang(status)` (private, mọi hàm gọi) | `DaBanGiao`/`DaHuy`/`HetHan`/`Loi` là **hấp thụ** | NÉM `0011` |
| 13 | `GhiNhanBiKhudup(lyDo)` | sinh trong appender (pre-check) | `Suppressed` |
| 14 | `HoanLaiChoGui(attempt, code)` | đang `DangBanGiao` & (`ExpiresAtUtc IS NULL` **hoặc** `now <= ExpiresAtUtc`); `AttemptCount++` | `RetryScheduled` |
| 15 | `HetHanTuDangBanGiao(now)` | đang `DangBanGiao` & `ExpiresAtUtc != null` & `now > ExpiresAtUtc`; **không** tăng `AttemptCount` | `Expired` |

> 🚨 **NÉM, không no-op im lặng** (`BR-005`, `D-201` tinh thần): mọi hàm chuyển trạng thái **gọi `ChuyenSang`**; guard sai ⇒ `BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)` = `0011`. Job định kỳ **không gọi hàm** khi guard chưa đủ ⇒ no-op **có log + metric** (hàng 3/5/9).
> 🚨 **`ExpiresAtUtc` NULL** (tin không hạn): hàng 14 **vẫn thử lại** (vế `IS NULL`), hàng 15 **không** áp. Đóng P3 #29.

### 3.5 `NotificationQueueEvent` — sổ append-only

`NotificationQueueEvent : CreationAuditedEntity<Guid>, IMultiTenant` — **không** có setter đổi, **không** method; cột theo SRS §5.5. `FromStatus`/`ToStatus` **(xem §15.2 ngoại lệ thiết kế #4: `int?`)**; `EventType` 13 giá trị. **Cấm** `UPDATE`/`DELETE` (kế thừa `StockLedger.cs:240`).

### 3.6 Value object + hàm thuần

| Kiểu | Vị trí | Bất biến |
|---|---|---|
| `ContentFingerprint` | `Domain/Templates/ContentFingerprint.cs` | `record` bọc `string Sha256`; kiểm `^[0-9a-f]{64}$` |
| `IdempotencyKey` | `Domain/Queue/IdempotencyKey.cs` | `record`; **cấm** rỗng; độ dài ≤ 256 |
| `NotificationRecipient` | `Domain/Queue/NotificationRecipient.cs` | `record(string Encrypted, string Hash)`; **không** lưu plaintext; `Hash` = SHA-256 của `"v1|" + ChuẩnHoá(recipient)` (chuẩn hoá **một cửa**, khuôn tinh thần `D-204`) |
| `INotificationRenderer` | `Domain/Rendering/INotificationRenderer.cs` | **hàm thuần** — **0** DB, **0** I/O, **0** `async`, **0** `DateTime.Now`; chỉ `whitelist` placeholder; `Subject`/giá trị thay vào **loại** `\r`/`\n`/NUL ở **một** hàm thuần (BR-024, `TC-43`); tiền nhận **chuỗi** qua `INotificationMoneyFormatter` (ADR-07) |
| `INotificationMoneyFormatter` | `Domain/Rendering/INotificationMoneyFormatter.cs` | **một** hàm định dạng; VND **0** chữ số, `vi-VN` (ADR-07/`XC-001`) |
| `NotificationLockKeys` | `Domain/NotificationLockKeys.cs` | trả `notification:…`; **0** tiền tố `ecommerce:` (bẫy tiền tố đôi) |
| `NotificationErrorCodes` | `Domain/NotificationErrorCodes.cs` | dải `0010–0020`, **một** tệp (khuôn `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs`) |

`NotificationLockKeys` — **4** hàm (SRS §8.2): `QueueDispatch(tenantId)` → `notification:queue-dispatch:{tenantId}` · `Template(templateId)` · `Batch(batchId)` · `QueueReplay(tenantId)`. **Cấm** trùng `NOT-002` (`notification:dlq-replay:*`, `…:zalo-oa:…`) và trùng `AbpOutbox_*`/`AbpInbox_*` (`XC-003` BR-006).

> ⚠️ **Việc kèm theo (file app, không phải module):** thêm `"notification"` vào `TienToModule` — `src/eCommerce.Application/Observability/DoThoiGianGiuKhoa.cs:35-39`; nếu thiếu, mọi khoá Notification bị gắn nhãn metric `module="other"` (vẫn đúng nghiệp vụ, sai nhãn quan sát).

### 3.7 `NotificationErrorCodes` (một tệp, dải `0010–0020`)

| Hằng | Mã |
|---|---|
| `KhoaXungDot` | `eCommerce:Notification:0010` |
| `ChuyenTrangThaiKhongHopLe` | `…:0011` |
| `TemplateChuaSanSang` | `…:0012` |
| `ThieuTruongBatBuoc` | `…:0013` |
| `DaBanGiaoRoi` | `…:0014` |
| `PlaceholderNgoaiWhitelist` | `…:0015` |
| `PhienBanBatBien` | `…:0016` |
| `KhongTimThay` | `…:0017` |
| `LoChuaDuyet` | `…:0018` |
| `BiKhudupCuaSo` | `…:0019` |
| `BodyFormatKhongHoTro` | `…:0020` |

---

## 4. Hợp đồng `Application.Contracts`

Tệp gợi ý: `modules/Notification/eCommerce.Notification.Application.Contracts/`

### 4.1 DTO & interface app service (đúng SRS §6.1)

| Kiểu | Chữ ký | Bên gọi | Breaking |
|---|---|---|---|
| `INotificationTemplateAppService` | `GetTemplatesAsync(PagedAndSortedResultRequestDto)` · `CreateAsync(CreateTemplateDto)` · `PublishVersionAsync(PublishTemplateVersionDto)` · `PreviewAsync(PreviewTemplateDto)` · `ArchiveAsync(Guid)` | FE admin + REST | không (mới) |
| `INotificationQueueAppService` | `EnqueueAsync(SendManualRequest)` · `GetQueueAsync(GetQueueRequest)` · `GetEventsAsync(Guid)` · `RetryAsync(Guid)` · `CancelAsync(CancelQueueItemRequest)` | FE + REST | không |
| `INotificationBatchAppService` | `CreateBatchAsync(CreateBatchDto)` · `ApproveBatchAsync(Guid)` | FE + REST | không |
| `INotificationQueueAppender` | `Task<EnqueueResult> EnqueueAsync(EnqueueRequest, CancellationToken)` | **biên #1/#2/#4** (nội module) | không |
| `CreateTemplateDto` | `(Code, Name, ChannelKind, Locale, NotificationPurpose, NotificationBodyFormat = Text)` | app service | không |
| `PublishTemplateVersionDto` | `(Guid TemplateId, string Subject, string BodyText, IReadOnlyList<NotificationPlaceholderDto>, NotificationBodyFormat = Text)` | app service | không |
| `EnqueueRequest` | `(Guid? TenantId, string IdempotencyKey, string TemplateCode, ChannelKind, string Locale, string Recipient, IReadOnlyDictionary<string,string> Data, int Priority, DateTime ScheduledAtUtc, DateTime? ExpiresAtUtc, Guid? BatchId, string? SourceEventType, string? SourceAggregateId, string CorrelationId)` | appender | không |
| `EnqueueResult` | `(Guid QueueItemId, bool AlreadyQueued, string Status, string? ErrorCode)` | appender | không |
| `RenderResult` | `(string Subject, string Body, string **BodyFingerprint**, IReadOnlyList<string> MissingRequiredPlaceholders)` | renderer | không — **đổi tên** khỏi SRS (`#21`, §15.2 #3) |

- `ChannelKind` là enum **Domain**; `Contracts` ref `Domain` (tiền lệ `ChannelHub.Application.Contracts.csproj`).
- `SendManualRequest` **bắt buộc** `IdempotencyKey`; thiếu ⇒ NÉM `0010` **trước** mọi ghi (biên #2). `Recipient` **chỉ tồn tại trong bộ nhớ** — **cấm** ghi log/ETO.

### 4.2 Endpoint REST (SRS §6.2) — **route cụ thể chưa xác minh**

| Method (ABP conventional) | Ý định route (SRS) | Quyền (`[Authorize]` cấp class + policy) |
|---|---|---|
| `GET` `GetTemplatesAsync` | `/api/notification/templates` | `…Templates.View` |
| `POST` `CreateAsync` | `/api/notification/templates` | `…Templates.Manage` |
| `POST` `PublishVersionAsync` | `/api/notification/templates/{id}/versions` | `…Templates.Publish` |
| `POST` `PreviewAsync` | `/api/notification/templates/{id}/preview` | `…Templates.View` |
| `POST` `ArchiveAsync` | `/api/notification/templates/{id}/archive` | `…Templates.Publish` |
| `POST` `EnqueueAsync` | `/api/notification/queue` | `…Queue.Create` |
| `GET` `GetQueueAsync` | `/api/notification/queue` | `…Queue.View` |
| `GET` `GetEventsAsync` | `/api/notification/queue/{id}/events` | `…Queue.View` |
| `POST` `RetryAsync` | `/api/notification/queue/{id}/retry` | `…Queue.Retry` |
| `POST` `CancelAsync` | `/api/notification/queue/{id}/cancel` | `…Queue.Cancel` |
| `POST` `CreateBatchAsync` | `/api/notification/batches` | `…Batches.Create` |
| `POST` `ApproveBatchAsync` | `/api/notification/batches/{id}/approve` | `…Batches.Approve` |
| `GET` `GetSummaryAsync` (**hoãn** — §11/§15.2 #6) | `/api/notification/reports/summary` | `…Reports.View` |

- **Không** đặt `[Route]` tay; ABP sinh route từ tên lớp/method (`eCommerceHttpApiHostModule.cs:40-...` nạp module, conventional controller tự đăng ký). **Đoạn đường dẫn cụ thể chưa xác minh** — BE xác nhận bằng `pnpm gen:api` + ghi handover (khuôn `ADDR-002-arch.md:168-170`).
- **`[Authorize]` ở CẤP CLASS** trên **3** app service (bài học `GetShopsAsync`); `[Authorize]` cấp method chỉ để **siết** thêm.
- **0** endpoint `[AllowAnonymous]` (`NOT-001` không nhận webhook NCC).

### 4.3 Mã lỗi ↔ HTTP (map ở Host)

Thêm khối `options.Map` cho `NotificationErrorCodes` vào `Configure<AbpExceptionHttpStatusCodeOptions>` (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:308` và khối tiếp theo):

| Mã | HTTP | Mã | HTTP |
|---|---|---|---|
| `0010` | `409` | `0016` | `409` |
| `0011` | `409` | `0017` | `404` |
| `0012` | `400` | `0018` | `409` |
| `0013` | `400` | `0019` | `409` |
| `0014` | `409` | `0020` | `400` |
| `0015` | `400` | `eCommerce:Lock:0001` | dùng **nguyên** (`XC-003:314`) |

`0001–0009` (NOT-002) **không** map ở WP-22 (chưa có sender thật).

### 4.4 `INotificationSender` — hợp đồng bàn giao **nội module** (bắt buộc có ở WP-22)

Tệp: `modules/Notification/eCommerce.Notification.Application.Contracts/Sending/INotificationSender.cs`

```csharp
public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(NotificationSendRequest request, CancellationToken ct = default);
}

public sealed record NotificationSendRequest(
    Guid? TenantId, Guid QueueItemId, string IdempotencyKey, ChannelKind ChannelKind,
    string RecipientEncrypted, string Subject, string Body, int Priority);
    // Subject/Body = nội dung ĐÃ RENDER (đã qua masker một cửa) — KHÔNG phải plaintext gốc,
    // KHÔNG có TemplateCode/Data (NOT-002 KHÔNG tự render — ADR-06/ADR-09).

public sealed record NotificationSendResult(
    bool Accepted, bool AlreadySent, bool IsTransient, string? ErrorCode);
```

- **Vì sao có ở WP-22 khi `NOT-002` là WP-23:** job bàn giao **có bên gọi thật** (chính nó) ⇒ hợp đồng hợp lệ (bài học 3). `NOT-002` **hiện thực** ở WP-23 — **cùng module** ⇒ **không** phải "gọi app service module khác".
- **Hợp đồng này là CANONICAL cho WP-22** (phát hành trước). `NOT-002` §6.1 hiện khai `SendAsync(SendRequest)` nhận `Recipient` plaintext + `TemplateCode` + `Data` (để nó tự render) và gộp `HandleCallbackAsync` — **trái** ADR-06 (NOT-001 render) và ADR-01 (NOT-002 không dựng nội dung). ⇒ `NOT-002` **phải** đổi theo kiểu §4.4: nhận `RecipientEncrypted` + `Subject` + `Body` **đã render**, **bỏ** `TemplateCode`/`Data`; tách `HandleCallbackAsync` sang hợp đồng callback **riêng của NOT-002**. Ghi ở `docDeviation #11`; BA sửa `NOT-002` cùng PR WP-23 (hoặc cùng PR W22-2 nếu muốn đồng bộ sớm). **Không** sửa SRS ở hàng này.
- **Che hay không che ở payload gửi:** `Subject`/`Body` trong DTO gửi là **đúng bản đã lưu** trên mục (`RenderedSubject` + `RenderedBodyMasked`), tức **đã qua masker một cửa**; với nội dung hợp lệ masker là **no-op** (BR-010 cấm OTP/token trong template), còn nếu có secret lọt vào template thì **cắt** chính là **fail-closed** — **không** gửi secret ra người thật. `RenderedBodyMasked`/`LastReasonMasked` **chỉ** là **tên cột** lưu để hiển thị/audit; **cấm** thêm trường `…Masked` vào DTO gửi (đã đổi tên thành `Body`). **Không** render lại lúc bàn giao (không lưu `Data` ⇒ không thể và không cần).
- **Mặc định WP-22:** đăng ký `ChuaCoKenhGuiSender` (fail-closed) — khi `Notification:EnableDispatch = true`, trả `IsTransient = false, ErrorCode = null`? **Không**: để tránh "gửi ma", `ChuaCoKenhGuiSender` **NÉM** `InvalidOperationException` ⇒ mục ở lại `DangBanGiao` rồi lease đưa về `ChoGui`, **0** dấu `DaBanGiao` giả. Cờ `Notification:EnableDispatch` mặc định **`false`** ở dev (SRS §15.3 giả định 2), khi `false` job **không** bàn giao (chỉ log `Warning` 1 lần/nhịp) — **không** đánh dấu `DaBanGiao`.

### 4.5 ETO phát ra (đúng SRS §7.1) — **không** PII, **có** `CorrelationId`

| ETO | Trường (ngoài `CorrelationId`) | Ai tiêu thụ | Loại |
|---|---|---|---|
| `NotificationEnqueuedEto` | `TenantId`, `QueueItemId`, `ChannelKind`, `TemplateCode`, `IdempotencyKey`, `ScheduledAtUtc` | `REP-*` | **thông báo** |
| `NotificationHandedOffEto` | + `DispatchedAtUtc`, `Priority` | `REP-*` | **thông báo** |
| `NotificationQueueFailedEto` | + `ErrorCode`, `LastReasonMasked` | OPS alert, `REP-*` | **thông báo** |
| `NotificationBatchApprovedEto` | `BatchId`, `TotalItems`, `ApprovedByUserId` | `REP-*` | **thông báo** |

- **Tất cả** implement `INotificationEvent` (`shared/eCommerce.Kernel/Events/INotificationEvent.cs:31`): dữ liệu **đã nằm trong DB** ⇒ **không** phải sự kiện mang dữ liệu ⇒ bộ dò độ phủ báo `Warning` chứ không `Error` khi chưa có người nhận (`eCommerceHttpApiHostModule.cs:586-602`). ⇒ **không** cần `IEventCoverageProbe` fail-closed ở nơi phát (luật E3 chỉ áp cho sự kiện **mang dữ liệu**).
- **0 PII**: chỉ `QueueItemId`/`IdempotencyKey`; `NOT-002` tra người nhận (đã mã hoá) từ `NotificationQueueItems`.
- **Không** dùng tên `NotificationQueuedEto` (của `NOT-002`).
- Publish `useOutbox: true`, **cùng UoW** với `QueueItem`/`QueueEvent`/`QueueBatch` (BR-007).

**ETO đầu vào (biên #1) — chưa định nghĩa ở WP-22** (không có module phát nào trong GĐ3 hiện tại) ⇒ **AQ1**. Khi có module phát đầu tiên, thêm `NotificationRequestedEto` + handler `IDistributedEventHandler<>` **mở UoW riêng** `requiresNew: true, isTransactional: true` (luật E2), gọi `INotificationQueueAppender` — **0** migration (chống trùng đã nằm ở unique index).

---

## 5. EF Core + DbContext hợp nhất

### 5.1 Bảng `NotificationTemplates` (SRS §5.1)

`Id uuid PK` · `TenantId uuid NULL` · `Code varchar(64) NOT NULL` · `Name varchar(256) NOT NULL` · `ChannelKind int` · `Locale varchar(8)` · `Purpose int` · `CoalesceWindowSeconds int NOT NULL` (0 = TẮT) · `DefaultExpiresAfterSeconds int NULL` · `Status int NOT NULL` · `CurrentPublishedVersionId uuid NULL` · `IsSystem bool NOT NULL`.

- UNIQUE `(TenantId, Code, ChannelKind, Locale)` `NULLS NOT DISTINCT`.
- INDEX `(TenantId, Status, ChannelKind)`.
- CHECK `ChannelKind IN (0,1,2)` · `Purpose IN (0,1,2)` · `CoalesceWindowSeconds >= 0` · `Purpose = 1 OR CoalesceWindowSeconds = 0` (ADR-12 ở tầng DB).

### 5.2 `NotificationTemplateVersions` (SRS §5.2 — **bất biến**)

`Id uuid PK` (= `TemplateVersionId` được ghim) · `TenantId uuid NULL` · `TemplateId uuid NOT NULL` (**FK** cùng module) · `Version int` · `Subject varchar(512) NULL` · `BodyText text NOT NULL` · `BodyFormat int NOT NULL` (CHECK `(0,1)`, GĐ3 chỉ `0`) · `Placeholders jsonb NOT NULL` (`[{name,kind,required}]`) · `ContentHash char(64) NOT NULL` · `PublishedAtUtc timestamptz NOT NULL` · `PublishedByUserId uuid NULL` · `RowVersion xmin` (Npgsql `IsRowVersion()`, `uint`).

- UNIQUE `(TenantId, TemplateId, Version)` `NULLS NOT DISTINCT`.
- UNIQUE `(TenantId, TemplateId, ContentHash)` `NULLS NOT DISTINCT`.
- INDEX `(TemplateId, Version DESC)`; CHECK `Version >= 1` · `BodyFormat IN (0,1)`.
- **Không** đường `UPDATE` nội dung sau `PublishedAtUtc` (test `TC-07`).

### 5.3 `NotificationBatches` (SRS §5.3)

`Id uuid PK` · `TenantId uuid NOT NULL` · `Code varchar(64)` · `TemplateId uuid` · `TemplateVersionId uuid` · `IdempotencyKey varchar(256)` · `Status int` · `TotalItems int` · `CreatedByUserId uuid NULL` · `ApprovedByUserId uuid NULL` · `ApprovedAtUtc timestamptz NULL` · `CorrelationId varchar(64)`.

- UNIQUE `(TenantId, IdempotencyKey)` + UNIQUE `(TenantId, Code)` — **cả hai** `NULLS NOT DISTINCT`.
- INDEX `(TenantId, Status, CreationTime)`.
- CHECK `TotalItems >= 0` · `Status IN (0,1,2,3)` · `ApprovedAtUtc IS NULL OR ApprovedByUserId IS NOT NULL`.

### 5.4 `NotificationQueueItems` (SRS §5.4)

`Id uuid PK` · `TenantId uuid NULL` (null = tin hạ tầng) · `IdempotencyKey varchar(256) NOT NULL` · `RootItemId uuid NULL` · `BatchId uuid NULL` · `SourceEventType varchar(256) NULL` · `SourceAggregateId varchar(128) NULL` · `ChannelKind int` · `TemplateId uuid` · `TemplateVersionId uuid` · `RecipientEncrypted text` · `RecipientHash char(64)` · `RenderedSubject varchar(512) NULL` · `RenderedBodyMasked text NULL` · `ContentFingerprint char(64)` · `CoalesceBucketStartUtc timestamptz NULL` (NULL = không khử trùng) · `Priority int` · `Status int` · `AttemptCount int` · `ScheduledAtUtc timestamptz` · `ExpiresAtUtc timestamptz NULL` · `DispatchLeaseExpiresAtUtc timestamptz NULL` · `DispatchedAtUtc timestamptz NULL` · `LastErrorCode varchar(64) NULL` · `LastReasonMasked varchar(512) NULL` · `CorrelationId varchar(64)` · `RowVersion xmin`.

**Index & ràng buộc** (7 unique `NULLS NOT DISTINCT` — xem §5.6):

| Loại | Cột / điều kiện | Mục đích |
|---|---|---|
| **UNIQUE** | `(TenantId, IdempotencyKey)` | 🚨 idempotency **hàng rào cuối** |
| **UNIQUE partial** | `(TenantId, RecipientHash, ChannelKind, ContentFingerprint, CoalesceBucketStartUtc)` `WHERE CoalesceBucketStartUtc IS NOT NULL` | 🚨 khử trùng cửa sổ (ADR-12) |
| INDEX | `(TenantId, Status, ScheduledAtUtc)` — partial `WHERE Status IN (0,1,2)` | job lấy việc (NFR-008) |
| INDEX | `(TenantId, BatchId)` | theo dõi lô |
| INDEX | `(TenantId, SourceEventType, SourceAggregateId)` | "đã gửi gì cho đơn này" |
| INDEX | `(TenantId, Status, DispatchLeaseExpiresAtUtc)` | khôi phục lease (hàng 7) |
| CHECK | `ChannelKind IN (0,1,2)` · `Priority IN (0,1,2,3)` · `AttemptCount >= 0` · `Status IN (0..7)` · `ExpiresAtUtc IS NULL OR ExpiresAtUtc > ScheduledAtUtc` | |

> **Khuyến nghị additive (BA xác nhận, không bắt buộc):** CHECK hex `~ '^[0-9a-f]{64}$'` cho `RecipientHash`/`ContentFingerprint` (khuôn `AddressDbContextModelCreatingExtensions.cs:201-204`). Không có trong SRS ⇒ **không** đưa vào phạm vi bắt buộc.

### 5.5 `NotificationQueueEvents` (SRS §5.5 — append-only)

`Id uuid PK` · `TenantId uuid NULL` · `QueueItemId uuid NOT NULL` (**FK** cùng module) · `EventType int` · `FromStatus int?` · `ToStatus int?` · `ReasonMasked varchar(512) NULL` · `ErrorCode varchar(64) NULL` · `CorrelationId varchar(64)` · `ActorUserId uuid NULL` · `OccurredAtUtc timestamptz`.

- INDEX `(TenantId, QueueItemId, OccurredAtUtc)` · INDEX `(TenantId, EventType, OccurredAtUtc)`.
- **0** unique index (nhiều event cùng loại hợp lệ).
- CHECK `EventType IN (0..12)` · `(EventType = 0 AND FromStatus IS NULL) OR (EventType <> 0 AND FromStatus IS NOT NULL)` (đóng `#22`, §15.2 #4).

### 5.6 `DbSet` + `ConfigureNotification` (**một lần**) + tên index ≤ 63 byte

- Thêm **5** `DbSet<…>` vào `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs` (khuôn `:47-55`; thiếu ⇒ `IRepository<T>` **không** đăng ký).
- Thêm **một** dòng `Notification.EntityFrameworkCore.NotificationDbContextModelCreatingExtensions.ConfigureNotification(builder);` trong `OnModelCreating` (`eCommerceDbContext.cs:353-391`) — **đúng một lần** (`D-197` ①(3), `TC-39`).
- `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:30-48`: thêm `typeof(Notification.NotificationEntityFrameworkCoreModule)` (kéo `Notification.Domain` vào đồ thị ⇒ **seeder + DI** đăng ký).
- Host `eCommerceHttpApiHostModule.cs:40-...`: thêm `typeof(Notification.NotificationApplicationModule)` (khuôn `:59`).
- `eCommerce.slnx` + `src/eCommerce.EntityFrameworkCore.csproj`: thêm 4 project/tham chiếu (một luồng, `D-216`).

**7 unique index `NULLS NOT DISTINCT` — tên thật (≤ 63 byte):**

| # | Tên index | Byte |
|---|---|---|
| 1 | `IX_NotificationTemplates_Tenant_Code_Channel_Locale` | 51 |
| 2 | `IX_NotificationTemplateVersions_Tenant_Template_Version` | 55 |
| 3 | `IX_NotificationTemplateVersions_Tenant_Template_Hash` | 52 |
| 4 | `IX_NotificationBatches_Tenant_IdempotencyKey` | 44 |
| 5 | `IX_NotificationBatches_Tenant_Code` | 34 |
| 6 | `IX_NotificationQueueItems_Tenant_IdempotencyKey` | 47 |
| 7 | `IX_NotificationQueueItems_Tenant_Recipient_Fingerprint_Bucket` | 61 |

- EF Core **không** mô hình hoá `NULLS NOT DISTINCT` (`Migrations/20261013024226_…cs:19-25`) ⇒ migration **thay** `CreateIndex(unique: true)` bằng `migrationBuilder.Sql("CREATE UNIQUE INDEX … NULLS NOT DISTINCT …")` — **không** để cả hai (bài học as-built `ADDR-002-arch.md:259`).
- **Cập nhật `PostgreSqlSchemaTests`**: `Assert.Equal(94L, …)` → **`101L`** (`test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs:215-216`) + **7 tên đích danh** vào danh sách (`:421-423`). ⚠️ Số **94** là **số thật hôm nay**, **không phải 93** như SRS §11 ⇒ §15.2 #1.

---

## 6. Chống gửi trùng — 5 lớp, lớp nào là hàng rào thật (SRS §7.4)

| Lớp | Cơ chế | Vị trí | Bỏ lớp này thì |
|---|---|---|---|
| **L1** | `IdempotencyKey` **ổn định** do tầng trên / client cấp | ngoài module | mọi lớp dưới vô nghĩa |
| **L2** | **UNIQUE** `(TenantId, IdempotencyKey)` `NULLS NOT DISTINCT` | `NotificationQueueItems` (DB) | 2 mục ⇒ **2 tin ra người thật** |
| **L3** | `UPDATE … WHERE Status = ChoGui` + khoá `notification:queue-dispatch:{tenantId}` | job (DB + Redis) | 2 job cùng bàn giao 1 mục ⇒ gửi trùng (Redis chết ⇒ **chỉ** DB còn chặn) |
| **L4** | `IdempotencyKey` **nguyên vẹn** sang `NOT-002` (unique **riêng** của nó) | `NOT-002` (DB) | mất tầng khử trùng **độc lập** |
| **L5** | partial UNIQUE theo cửa sổ nội dung | `NotificationQueueItems` (DB) | spam cùng nội dung (không gửi trùng kỹ thuật) |

**Cơ chế L5 (khử trùng cửa sổ) — đúng P2 #26 `D-208`:**
1. **pre-check** theo partial index (`SELECT … FOR` không cần) **trước** khi ghi;
2. có trùng ⇒ ghi mục `DaHuy` với `CoalesceBucketStartUtc = NULL` (**cố ý** ra ngoài index) + **đúng 1** `QueueEvent(Suppressed)` + trả `0019`;
3. **đua** hai luồng: bên thua bắt `23505` ⇒ **rollback UoW đã lỗi** rồi ghi **lần hai trong UoW MỚI `Begin(requiresNew: true, isTransactional: true)`** ⇒ mục `DaHuy` bucket `NULL` + **đúng 1** `Suppressed` ⇒ `0019`. 🚨 **Cấm** dùng lại transaction đã abort (PostgreSQL 17 trả `25P02`).
4. Partial index là **backstop**, không phải đường chính.

**IdempotencyKey theo biên:**

| Biên | Cách sinh | Cấm |
|---|---|---|
| #1 ETO | `{EventType}:{AggregateId}:{Milestone}:{ChannelKind}[:{RecipientHash[..12]}]` | `Guid.NewGuid()` |
| #2 API | **client truyền**; thiếu ⇒ `0010` **trước** mọi ghi | server tự sinh |
| #3 job | **nguyên** khoá **đã lưu** của mục | sinh khoá mới |
| #4 phát lại | `{gốc}:replay-{n}` | dùng lại khoá gốc |
| lô | `batch:{tenantId}:{code}` | — |

> 🎯 Trả lời `XC-003` BR-007 (*"bỏ lock, DB có chặn?"*) ⇒ **CÓ** (L2 + L3 + L5 đều ở PostgreSQL). Redis chết (`AbortOnConnectFail = false` — `eCommerceHttpApiHostModule.cs:260`) ⇒ mất tuần tự, **không** mất tính đúng.

---

## 7. Job bền vững

### 7.1 Một worker định kỳ, **không** dynamic worker

`NotificationDispatchWorker : AsyncPeriodicBackgroundWorkerBase` (khuôn `ChannelSyncSupervisorWorker.cs:27-51`), đăng ký `context.AddBackgroundWorkerAsync<NotificationDispatchWorker>()` (`ChannelHubApplicationModule.cs:50`). Nhịp cấu hình `Notification:Dispatch:PeriodSeconds` (mặc định **10**), `Timer.RunOnStart = true` (`ChannelSyncSupervisorWorker.cs:47-48`).

- 🚨 **KHÔNG** dùng dynamic worker `AddAsync` mỗi nhịp — bài học C10: ABP **THAY THẾ** worker ⇒ timer reset ⇒ worker **không bao giờ chạy** (`ChannelSyncSupervisorWorker.cs:84-101`). Thiết kế **0** dynamic worker; tính bền vững đến từ **DB**, không từ worker trong RAM.
- Dòng **log vô điều kiện** đầu mỗi nhịp (`ChannelSyncSupervisorWorker.cs:53-58`; `BackgroundWorkerCoCheTests` đòi).
- `AbpBackgroundJobs` (`eCommerceEntityFrameworkCoreModule.cs:47`) **có sẵn** nhưng **không** dùng cho nhịp quét; nếu sau này cần job **một-lần-có-hẹn** (vd gửi lại đúng `ScheduledAtUtc`), dùng `IBackgroundJobManager` — **additive**, không đổi schema.

### 7.2 Ba pha trong một nhịp (một khoá/tenant)

```
DoWorkAsync
 1. log bắt đầu  ───────────────────────────────────────────────  (vô điều kiện)
 2. EnsureDispatchEnabled?  false ⇒ log Warning 1 lần, return
 3. danh sách tenant = DISTINCT TenantId của mục chưa kết thúc  (+ null)
    foreach tenant:
      handle = TryAcquire("notification:queue-dispatch:{tenant}", timeout 5s)
      null ⇒ log Debug + metric lock_skipped_total  → next tenant   (SkipIfBusy, XC-003 D10)
      (A) khôi phục lease: mục DangBanGiao có DispatchLeaseExpiresAtUtc < now
            → KhoiPhucLease (hàng 7) + event LeaseExpired + metric  (KHÔNG tăng AttemptCount)
      (B) hết hạn: mục ChoGui có ExpiresAtUtc < now → HetHanTuChoGui (hàng 9) + metric
      (C) bàn giao — HAI bước tường minh, mỗi bước một UoW:
          (C1) sẵn sàng: mục Moi tới hạn (ScheduledAtUtc <= now) và (không thuộc lô HOẶC lô DaDuyet)
                 UPDATE … SET Status=ChoGui, ScheduledAtUtc giữ nguyên WHERE Id=@id AND Status=Moi
                   → 1 dòng ⇒ insert QueueEvent(ReadyToDispatch) TRONG CÙNG UoW (hàng 3)
                   → 0 dòng ⇒ mục đã bị luồng khác đổi (hoặc bị hàng 13 khử trùng → DaHuy) ⇒ bỏ qua,
                                KHÔNG gọi DanhDauSanSang, KHÔNG ném  ← đường nóng không ném lỗi
          (C2) giành bàn giao: mục ChoGui có ScheduledAtUtc <= now
                 UPDATE … SET Status=DangBanGiao, DispatchLeaseExpiresAtUtc=now+lease
                   WHERE Id=@id AND Status=ChoGui   → 0 dòng ⇒ bỏ qua (KHÔNG lỗi)
                   → 1 dòng  ⇒ gọi INotificationSender.SendAsync NGOÀI transaction (D12)
                          Accepted/AlreadySent → DaBanGiao + HandedOff + ETO
                          IsTransient          → HoanLaiChoGui (hàng 14) / HetHanTuDangBanGiao (hàng 15)
                          vĩnh viễn            → DanhDauLoi (hàng 8) + metric
      dispose handle (await using)  ← khoá NGOÀI UoW, nhả SAU commit
```

- **UoW:** mỗi mục một UoW `requiresNew: true, isTransactional: true` (chuyển trạng thái + event + outbox **cùng** transaction — BR-007); **lời gọi sender nằm ngoài** transaction (§12 SRS).
- **Vì sao (C1) dùng `UPDATE … WHERE Status=Moi` thay vì gọi `DanhDauSanSang` trước:** hai luồng cùng chạy `Moi→ChoGui` trên **một** mục ⇒ nếu gọi hàm entity ở đường nóng, luồng thua sẽ **NÉM** `0011` (nhiễu log/metric cho một việc bình thường). Cập nhật **có điều kiện** giữ đúng bất biến (`Moi` chỉ sang `ChoGui` một lần, event `ReadyToDispatch` ghi **cùng** UoW với dòng vừa đổi) và biến ca đua thành **0 dòng ⇒ bỏ qua**. Hàng 13 (khử trùng cửa sổ) đổi `Moi→DaHuy` **trong UoW lúc xếp hàng**, nên sau khi `23505` rollback + ghi lần hai, mục là `DaHuy` ⇒ (C1) `WHERE Status=Moi` trả **0** (không "hồi sinh"). Nếu app bị kill **giữa** (C1) và (C2): mục ở `ChoGui` ⇒ nhịp sau (C2) bàn giao — **không mất việc**, **không** gửi trùng (L3). Đối chiếu `TC-01` (3 event: `Enqueued`/`ReadyToDispatch`/`HandedOff`) và `TC-05` hàng 3/5.
- **Lease:** `Notification:Dispatch:LeaseSeconds` mặc định **120 s** > timeout gọi sender (10 s — `Q13` của SRS §15.1 còn `Open`, đề xuất **10 s**) × (1 + retry) + dư; **cấm** lease ≤ timeout sender (khôi phục sớm ⇒ tải giả, vẫn an toàn nhờ L4).
- **Index:** chỉ dùng `(TenantId, Status, ScheduledAtUtc)` (NFR-008; `EXPLAIN` không full-scan).
- **Metric + log:** mỗi mục có `CorrelationId`; backlog gauge sau mỗi nhịp; lease-expired/counter.

---

## 8. Phân quyền

- **Một chỗ khai:** lớp `Notifications` trong `eCommerceConsts.Permissions`, chèn **sau** lớp `Addresses` (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:363-394`; đóng `Permissions` ở `:395`) — `GroupName + ".Notification"`:
  - `.Templates.View` · `.Templates.Manage` · `.Templates.Publish`
  - `.Queue.Create` · `.Queue.View` · `.Queue.Cancel` · `.Queue.Retry`
  - `.Batches.Create` · `.Batches.Approve` · `.Reports.View`
- **Định nghĩa trong provider:** `eCommercePermissionDefinitionProvider.Define` (`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:21-25`): `var notif = group.AddPermission(eCommerceConsts.Permissions.Notifications.Templates...)` + `AddChild(...)` cho **cả 10** — khuôn `:47-53`. Thiếu một nơi ⇒ `403` cho **mọi** người kể cả `admin` (`XC-004` BR-002, `BatBienPhanQuyenTests.TC02b` khoá **hai chiều**).
- **ABP group** vẫn là `"eCommerce"` (`GroupName`); `eCommerce.Notification` là **tên permission**, không phải group ABP mới — đúng `D-197` ⑥ ("nhóm permission `eCommerce.Notification`" = tiền tố tên).
- `Batches.Approve` + `Queue.Retry` mặc định **admin**; `CreatedByUserId != ApprovedByUserId` (domain + `TC-25`).
- **Không** dùng lại `.Messages/.Dlq/.Channels/.OptOut/.Cost` (của `NOT-002`).
- Chạy `DbMigrator` (binary mới) sau khi thêm để role `admin` được cấp (bài học `S6`); provider nằm trong `eCommerce.Application.Contracts` mà DbMigrator đã `[DependsOn]` (`src/eCommerce.DbMigrator/Program.cs:14-23`).
- Test reflection: mọi `[Authorize]` dùng hằng ⇒ hằng **phải** có định nghĩa (2 chiều).

---

## 9. PII — mặc định TẮT (`D-191`)

| # | Luật | Cơ chế |
|---|---|---|
| 1 | Template/version **cấm** PII thật | `Subject`/`BodyText`/`Placeholders`/seed **chỉ** placeholder + dữ liệu mẫu tổng hợp; `PreviewAsync` mặc định **chỉ** dữ liệu mẫu (`AQ7`) |
| 2 | Người nhận **chỉ** `RecipientEncrypted` + `RecipientHash` | mã hoá `IStringEncryptionService` ở tầng Application (`ChannelTokenManager.cs:28,160-165`); hash 1 chiều để tra/khử trùng; **0** cột plaintext |
| 3 | `Rendered*`/`LastReasonMasked` **đã mask** | mask ở một hàm (khuôn `PaymentTransaction.MaskSensitiveData`, che cả `otp`) **trước** khi ghi |
| 4 | ETO/log/metric **0 PII** | ETO chỉ `QueueItemId`/`IdempotencyKey`; **cấm** nhãn `email`/`phone` (`eCommerceMetrics.cs:245-248`) |
| 5 | **OTP KHÔNG BAO GIỜ** vào template/hàng đợi | không có cột/nhánh ghi OTP; test quét `TC-13` |

- `Recipient` trong `EnqueueRequest` **chỉ ở bộ nhớ**; **cấm** ghi log/ETO; chuẩn hoá + băm ở **một cửa** (`NotificationRecipient`).
- `TC-41` chứng minh PII tắt; `TC-13` chứng minh 0 OTP.

---

## 10. Metric

Thêm **7** metric (`SRS §11`) — nhãn **chỉ** từ `eCommerceMetricTags.Allowed` (`eCommerceMetrics.cs:238-242`), **tất cả** nhãn đề xuất **đã có** (`channel`,`result`,`reason`) ⇒ **không** phải sửa `Allowed`:

| Tên | Kiểu | Nhãn | Cập nhật `KyHieuMetric` |
|---|---|---|---|
| `notification_queued_total` | Counter | `channel` | `NotificationQueued` |
| `notification_dispatched_total` | Counter | `channel`, `result` | `NotificationDispatched` |
| `notification_queue_failed_total` | Counter | `channel`, `reason` | `NotificationQueueFailed` |
| `notification_render_failed_total` | Counter | `reason` | `NotificationRenderFailed` |
| `notification_queue_backlog` | Gauge | — | `SetNotificationQueueBacklog` |
| `notification_dispatch_lease_expired_total` | Counter | — | `NotificationDispatchLeaseExpired` |
| `notification_window_suppressed_total` | Counter | `channel` | `NotificationWindowSuppressed` |

- **Bắt buộc cùng PR** (ADR-08): (a) hằng trong `eCommerceMetricNames` (`eCommerceMetrics.cs:13-107`); (b) tên vào `All` (`:94-124`) — **19 → 26**; (c) **7** phương thức phát trên `eCommerceMetrics` (`:263+`); (d) **7** dòng `KyHieuMetric` (`:152-174`); (e) **không** thêm vào `ChuaCoNoiPhat` (rỗng — vì sẽ có điểm phát thật); (f) cập nhật `XC-005` §5.1 (`docs/features/cross-cutting/XC-005-observability-log-metric.md:181`); (g) test `MetricsTests` xanh — so **đúng tập** + **chiều ngược** (`test/eCommerce.Tests/Observability/MetricsTests.cs:44-45,55,72-73,100-103`) + `MetricEmitterTests`.
- **Cấm** nhãn `email`/`phone`/`messageId`/`queueItemId`/`correlationId` (`XC-005` BR-009). Nhãn `channel` = **ChannelKind** (hữu hạn 3) — **không** phải `ChannelCode` kênh bán.

---

## 11. Hợp đồng FE tối thiểu

SRS §10 **có màn admin** ⇒ W22-3 **có FE** (khác `ADDR-002` không có FE).

| Vùng UI | Nguồn | Trạng thái bắt buộc |
|---|---|---|
| Danh sách template | `GetTemplatesAsync` | `Archived` **vẫn** hiện; rỗng ⇒ "hướng dẫn tạo"; **không** ẩn bản ghi `Archived` |
| Soạn template | `CreateAsync`/`PublishVersionAsync` | placeholder chèn từ whitelist (**không** gõ `{{…}}` tự do — chống `0015`); hộp thoại nêu **bất biến** |
| Xem trước | `PreviewAsync` | render `textContent` (`<pre>` + `pre-wrap`); **CẤM** `dangerouslySetInnerHTML` (`D-199`) |
| Hàng đợi | `GetQueueAsync` | **che** email/SĐT; `0012` = lỗi cấu hình (hướng dẫn publish), **không** "chưa có dữ liệu" |
| Chi tiết mục | `GetEventsAsync` | chỉ đọc; hiện `ErrorCode` + `CorrelationId` |
| Lô gửi | `CreateBatchAsync`/`ApproveBatchAsync` | template **chỉ** `Marketing`; **cấm** nút duyệt cho người tạo; nêu "sẽ gửi ra người thật" |
| Báo cáo tổng hợp | **hoãn** | ô số liệu **ẩn** ở WP-22 (read-model `REP-*` chưa có — §15.2 #6) |

- **Contract-first:** hợp đồng là `schema.d.ts` sinh bằng `pnpm gen:api`; **không** đổi `schema.d.ts` ngoài diff mong đợi. Đường dẫn thật (`/api/app/...` của ABP vs `/api/v1` của BFF) **chưa xác minh** ⇒ FE dùng route sinh từ OpenAPI, **không** hard-code (`ADDR-002-arch.md:168-173`).
- Enum trả **số** (0/1/2…) như DTO hiện có; FE map nhãn. Phân biệt **"rỗng" ≠ "lỗi thật"**.

---

## 12. Migration expand-only + đường lùi

### 12.1 Kế hoạch (mô tả — **BE sinh** ở W22-2, hàng này **0** migration)

| Bước | Việc | Ai | Ghi chú |
|---|---|---|---|
| 0 | `pg_dump` DB dev → `artifacts/pg-dump/N83-W22-2.dump` **trước** khi áp | OPS/BE | `ef-migration-postgres` M9 + `D-253` ③ |
| 1 Expand | Sửa model + `ConfigureNotification` + **5 `DbSet`**; `dotnet ef migrations add Added_Notification_Template_Queue` | **một** chủ migration (BE) | sinh **5** `CREATE TABLE` + index + CHECK; **thay** 7 `CreateIndex(unique:true)` bằng `Sql("CREATE UNIQUE INDEX … NULLS NOT DISTINCT")` |
| 2 Kiểm diff | Chỉ `CREATE TABLE/INDEX` + CHECK; **0** `DROP`/`ALTER` bảng cũ | BE + review | 5 bảng **mới** ⇒ không đụng bảng nào |
| 3 Áp | `dotnet build` rồi `dotnet run --project src/eCommerce.DbMigrator` (binary **mới**); kiểm `__EFMigrationsHistory` + `\d` 5 bảng | BE/OPS | **cấm** `dotnet ef database update` (M6) |
| 4 Seed | Idempotent (12.3) | DbMigrator | kiểm chạy **2 lần** không đổi |
| 5 Contract | **Không có** ở WP-22 (không xoá/đổi kiểu) | — | |

### 12.2 Thứ tự deploy

Migration **trước**, bật job **sau**. Code chạy khi bảng chưa có ⇒ job lỗi `PostgresException` mỗi nhịp ⇒ phải `DbMigrator` **trước** khi deploy code; `Notification:EnableDispatch` mặc định `false` cho tới khi có sender thật (`NOT-002`).

### 12.3 Seed (idempotent, **0** PII)

`NotificationDataSeedContributor : IDataSeedContributor` trong `modules/Notification/eCommerce.Notification.Domain/Seed/` — chạy qua `IDataSeeder.SeedAsync()` (`src/eCommerce.DbMigrator/Program.cs:74-76`); đồ thị module qua `Notification.EntityFrameworkCore` (đã thêm ở §5.6). Kiểm tồn tại `(TenantId IS NULL, Code, ChannelKind, Locale)` ⇒ bỏ qua. Tạo **3** template hệ thống `ops-alert` (Email/`OpsAlert`), `order-confirmation`, `order-shipped` + **version 1 `Published`** với `GUID` **hằng** + `ContentHash` **tính sẵn** + dữ liệu mẫu tổng hợp. **Cấm** seed người nhận/lô/mục; **cấm** credential/token/email/SĐT thật.

> Dự phòng (nếu không muốn đổi `[DependsOn]` của DbMigrator): dùng `HasData` trong migration — **nhưng** `jsonb`/`xmin` khi seed dễ vỡ và seed thành một phần snapshot; ưu tiên **contributor** (đã có `Program.cs:14-23` nạp `Application.Contracts` + EF module).

### 12.4 Đường lùi

- **(a) Tắt tính năng:** `Notification:EnableDispatch=false` ⇒ job **không** bàn giao; xếp hàng vẫn chạy; hệ thống như trước (0 tin ra ngoài).
- **(b) Trước merge:** `git revert` + `dotnet ef migrations remove` (chưa có dữ liệu).
- **(c) Sau merge:** migration **mới** `Drop` theo thứ tự ngược FK (`QueueEvents` → `QueueItems` → `Batches` → `TemplateVersions` → `Templates`); **0** module khác có FK tới 5 bảng ⇒ an toàn về schema. ⚠️ **Mất hàng đợi chưa bàn giao** ⇒ **dừng job + bàn giao hết** trước khi rollback.
- **(d) Rollback code:** tắt `AddBackgroundWorkerAsync` (1 dòng) ⇒ job ngừng; API vẫn 404 (module gỡ khỏi Host `[DependsOn]`).
- **Không** backfill (module mới, 0 dữ liệu lịch sử). **Không** sửa `Directory.Packages.props` (0 gói mới; `D-199`).

---

## 13. Test / QA plan (map BR → tầng) + negative control

### 13.1 Theo tầng (neo vào `TC-01…TC-45` của SRS §14.1)

| Tầng | Ca bắt buộc (SRS) | Phủ |
|---|---|---|
| Domain thuần (**không** DB) | `TC-04` render tái lập 100 lần; `TC-35` thiếu trường ⇒ `0013` / placeholder lạ ⇒ `0015`; `TC-43` loại CR/LF/NUL; `TC-05` **15 hàng** (gồm hàng 14 vế `NULL`, hàng 15, hàng 7 không tăng `AttemptCount`); `TC-06` lùi `DaBanGiao` ⇒ `0011`; `TC-07` sửa version `Published` ⇒ `0016`; `TC-08` ghim version; `TC-31` không khử trùng tin giao dịch | BR-001/003/004/005/023/024, ADR-02/06/12 |
| Domain (reflection/quét) | `TC-12` 0 `Update/Delete` event; `TC-20` `NotificationLockKeys` không `ecommerce:`; `TC-21` 0 trùng khoá ABP; `TC-22` 0 `double/float`; `TC-17` ETO 100 % `CorrelationId`; `TC-44` một module một nguồn (1 `NotificationErrorCodes` khớp `^eCommerce:Notification:\d{4}$`, `0001–0009 ∩ 0010–0020 = ∅`, 1 `ChannelKind`, 0 `NotificationChannelKind`); `TC-40` 0 `DeliveredAt`/`Cost`/`OptOut`/`QuietHours`/`OrderId`; `TC-41` PII tắt | BR-006/010/011/012/015/018/022, NFR-006/014/018 |
| Application (fake sender) | `TC-01` happy path 3 event + **1** lần sender; `TC-03` trùng khoá payload khác ⇒ `0010`; `TC-10` khoá nguyên vẹn; `TC-11` bận khoá ⇒ log+metric; `TC-25` tự duyệt ⇒ NÉM; `TC-26` lô `ChoDuyet` ⇒ **0** sender; `TC-32` lease hết hạn ⇒ về `ChoGui`; `TC-33` 0 tham chiếu `I*AppService` module khác; `TC-39` 5 `DbSet` + `ConfigureNotification`; `TC-42` `Html` ⇒ `0020` **trước** ghi; `TC-45` `GetTemplatesForBatch` lọc `Marketing` | BR-002/008/009/014/017/019/023/025, ADR-11 |
| Endpoint (Host thật/`WebApplicationFactory`) | `TC-38` 3 app service: `401` / `403` / `200`; **0** `[AllowAnonymous]`; **100 %** permission có định nghĩa | BR-016, `XC-004` |
| Integration (PostgreSQL) | `TC-02` 1.000 lần (nửa song song) ⇒ **1** mục / 999 `AlreadyQueued`; `TC-09` 2 job song song 10.000 vòng ⇒ 0 bàn giao 2 lần; `TC-19` `TenantId = NULL` vẫn chặn `23505`; `TC-24` sống qua restart; `TC-27` cách ly 2 tenant; `TC-29` 7 index `NULLS NOT DISTINCT` (danh sách đích danh); `TC-30` khử trùng cửa sổ **tuần tự + song song** ⇒ `0019` + **đúng 1** `Suppressed`; `TC-36` `QueueItem`+`QueueEvent`+outbox **cùng** UoW; `TC-18` `CorrelationId` xuyên ETO | BR-002/007/008/013/021, NFR-002/003/010/011 |
| Đo hiệu năng | NFR-001 p95 < 5 ms (render); NFR-008 `EXPLAIN` dùng index; NFR-012 p95 < 30 s; NFR-016 ≥ 200 mục/s | — |
| UI | W22-3/W22-4: preview **không** `dangerouslySetInnerHTML` (`TC-42` vi); hiển thị che người nhận | `D-199` |

### 13.2 Negative control **bắt buộc** (đột biến **đỏ** → khôi phục **xanh**; ORCH chạy `tools/agents/negative-control.ps1`)

| # | Đột biến | Ca phải đỏ |
|---|---|---|
| NC-1 | Bỏ `NULLS NOT DISTINCT` ở index `(TenantId, IdempotencyKey)` | `TC-19` (`TenantId = NULL`, chèn 2 cùng khoá) |
| NC-2 | Bàn giao bằng `if (status == ChoGui) { … }` **không** có `UPDATE … WHERE` | `TC-09` (2 job song song ⇒ 2 lần gọi sender) |
| NC-3 | Nhánh `23505` khử trùng dùng lại UoW đã abort (bỏ `requiresNew`) | `TC-30` (mất mục/event hoặc `25P02`/`500` thay vì `0019`) |
| NC-4 | Guard trạng thái `return;` im lặng thay vì NÉM `0011` | `TC-05`/`TC-06` |
| NC-5 | Render đọc `DateTime.Now`/DB (bỏ thuần) | `TC-04` (100 lần khác nhau / có truy vấn DB) |
| NC-6 (khuyến nghị) | Gắn nhãn `phone` vào `notification_queued_total` | test nhãn (`TC-16`/`MetricEmitterTests`) |
| NC-7 (khuyến nghị) | Thêm metric phát ra **không** khai trong `All` | `MetricsTests` (chiều "mọi tên phát ra ⊆ All") |

---

## 14. Xử lý **15 P3** đang mở (`docs/reviews/NOT-001-docreview.md`)

| # | Vấn đề (docreview) | Thiết kế này xử lý |
|---|---|---|
| **#7** | SRS dẫn `eCommerceMetrics.cs:75–78` cho `Allowed` (sai dòng) | **Đóng bằng thiết kế**: §10 dẫn **đúng** `Allowed` `eCommerceMetrics.cs:238-242`; **0** nhãn mới. **BA** sửa dòng trong SRS §11 cùng PR W22-2 |
| **#8** | SRS dẫn số dòng `eCommerceHttpApiHostModule.cs` sai (49/56/99/735/781/912/922) | **Đóng bằng thiết kế**: §1.1/§2/§4.3/§6 dẫn **đúng** `:40,59,94,231,246,260,308`; **BA** sửa SRS §0/§12 |
| **#9** | SRS dẫn `StockLedger.cs` đường dẫn cũ | **Đóng**: đúng là `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs:240,243`; **BA** sửa SRS §5.5 |
| **#10** | Q11 (`TenantId = NULL` cho template hệ thống) còn `Open`; Basic Flow không nêu cách vượt query-filter | **Thiết kế**: **KHÔNG** `IgnoreQueryFilters`; chế độ 1 tenant đọc được template `TenantId = NULL` qua filter mặc định; đa tenant là **nợ GĐ4** (khuôn `D-92` ⑤) ⇒ giữ **`AQ3`** cho Security/ARCH |
| **#11** | `0010` gộp hai nghĩa (trùng khoá payload khác + thiếu khoá API) | **Đóng bằng thiết kế**: giữ **một** mã `0010` (dải là hợp đồng, `ADR-14`) nhưng phân biệt bằng `BusinessException.WithData("lyDo", …)`; API kiểm **thiếu khoá trước mọi ghi**; **BA** làm rõ `Thông điệp` ở SRS §6.2 |
| **#12** | Hàng 8 guard sai lại NÉM mã `NOT-002` | **Đóng bằng thiết kế**: **mọi** chuyển sai ⇒ `0011`; mã `NOT-002` (`0002`/`0007`) **chỉ** lưu vào cột `LastErrorCode` (dữ liệu), **không** làm `Code` của exception. **BA** sửa §3 hàng 8 |
| **#18** | Cùng test dẫn hai khoảng dòng (`69–105` vs `86–105`) | **Đóng**: §10 dẫn **một** bộ dòng thật `MetricsTests.cs:44-45,55,72-73,100-103`; **BA** thống nhất SRS §0/§11/§14.4 |
| **#19** | F9 dẫn "§6.3" (không tồn tại) | **Đóng**: đúng là **§6.2** (bảng mã lỗi); **BA** sửa F9 |
| **#20** | Mục "Đối chiếu Nhanh" ghi `NOT-001` có `DOC-OK` | **Đóng bằng thực tế**: vòng 4 `VERDICT: DOC-OK` (`docs/reviews/NOT-001-docreview.md:3`) — nay **đúng** |
| **#21** | `ContentFingerprint` tính ở renderer thuần nhưng công thức cần `RecipientHash`/`TemplateVersionId` | **Đóng bằng thiết kế (§15.2 #3)**: renderer trả `BodyFingerprint` (chỉ Subject+Body); `ContentFingerprint` mục = SHA-256(`TemplateVersionId` + `RecipientHash` + `RenderedSubject` + `RenderedBodyMasked`) do **appender** tính. **BA** sửa tên `RenderResult`/§6.1 |
| **#22** | `FromStatus` NOT NULL nhưng event `Enqueued` không có trạng thái nguồn | **Đóng bằng thiết kế (§15.2 #4)**: `FromStatus`/`ToStatus` **`int?`**; CHECK `Enqueued ⇒ FromStatus IS NULL`, event khác ⇒ NOT NULL. **BA** sửa §5.5 |
| **#23** | `NOT-002` ghi `NOT-001` tiêu thụ `NotificationSentEto`; §7.2 không có | **Đóng bằng thiết kế**: `NOT-001` **KHÔNG** tiêu thụ `NotificationSentEto` (không sở hữu "đã tới" — ADR-01); kết quả nhận **đồng bộ** qua `INotificationSender.SendAsync`. **BA** sửa `NOT-002` cùng PR WP-23 |
| **#27** | Hàng 14 giữ `ScheduledAtUtc` ⇒ thử lại mỗi nhịp, không backoff/trần | **Đóng bằng thiết kế**: **thử lại mỗi nhịp là chủ ý** (trần do `ExpiresAtUtc`; việc thử lại **vận chuyển** do `NOT-002` sở hữu — **ADR-01 tách lớp · SRS §0**, `D-193` chỉ nói *chi phí kỳ*, **không** nói retry); `AttemptCount` hiển thị + metric. **QA phủ** `TC-05` hàng 14/15 |
| **#28** | §4.1 sequence thiếu nhánh "lỗi tạm thời" | **Đóng bằng thiết kế**: §7.2 có nhánh `IsTransient → HoanLaiChoGui/HetHanTuDangBanGiao`; **BA** thêm nhánh `else «lỗi tạm thời»` vào sơ đồ SRS §4.1 |
| **#29** | Guard hàng 14 thiếu vế `ExpiresAtUtc IS NULL` | **Đóng bằng thiết kế (§15.2 #7)**: hàng 14 guard = `ExpiresAtUtc IS NULL OR now <= ExpiresAtUtc`; **BA** sửa SRS §3 hàng 14 |

> **0 P3 bị bỏ rơi.** 8 mục đóng **bằng thiết kế** (#7,#8,#9,#11,#12,#18,#20,#22), 7 mục đóng **thiết kế + BA sửa SRS cùng PR W22-2** (#10,#19,#21,#23,#27,#28,#29). Không mục nào cần `qa` phủ thêm ngoài `TC-05/TC-30` đã ghi ở §13.

---

## 15. `docDeviation` (SRS ↔ ARCH) & câu hỏi mở

### 15.1 Câu hỏi mở (`openQuestions` — **không tự quyết**)

| ID | Câu hỏi | Cần | Đề xuất |
|---|---|---|---|
| `NOT-001-AQ1` | Định nghĩa **ETO đầu vào** (biên #1) + handler ngay trong WP-22 (khi **chưa có** module phát) hay để tới khi `SAL`/`SHP` phát lần đầu? | DEC | **Để sau** (bài học 3: hợp đồng phải có bên gọi thật; chống trùng đã ở unique index ⇒ thêm handler **0** migration) |
| `NOT-001-AQ2` | Endpoint `GET /reports/summary` **hoãn** tới khi `REP-001`/`REP-002` có read-model? | DEC | **Hoãn** (BR-020 cấm query DB giao dịch; chưa có read-model) |
| `NOT-001-AQ3` | Q11 — template hệ thống `TenantId = NULL` cho mọi tenant đọc? Bật multi-tenancy thật (GĐ4) xử lý ra sao? | Security/ARCH | Chỉ đọc, 0 PII; nay 1 tenant; ghi nợ GĐ4 |
| `NOT-001-AQ4` | Q5/Q6 — cửa sổ khử trùng mặc định (đề xuất 300 s) + hạn sống theo loại tin (OTP 5 m / giao dịch 24 h / marketing 72 h)? | Vận hành | Theo đề xuất SRS |
| `NOT-001-AQ5` | Q7 — engine template **tự viết** (thay `{{…}}`) — không gói NuGet? | DEC | **Tự viết** (`D-188`/`D-199`) |
| `NOT-001-AQ6` | Q8 — số người nhận tối đa một lô (đề xuất 50.000)? | Vận hành + ARCH | 50.000; vượt ⇒ chia lô |
| `NOT-001-AQ7` | Q9 — `PreviewAsync` có cho dùng **dữ liệu thật**? | Security | **Chỉ** dữ liệu mẫu |

### 15.2 `docDeviation` — chỗ thiết kế **buộc** khác SRS / `NOT-002` (ORCH gửi `ba` sửa SRS cùng PR W22-2; riêng `#11` sửa `NOT-002` cùng PR WP-23)

| # | SRS nói | Thiết kế chốt | Lý do |
|---|---|---|---|
| 1 | §11: test index `93 → 100` | **`94 → 101`** | `PostgreSqlSchemaTests.cs:215-216` đã là **94** sau WP-18 (`:208`); SRS v0.5 viết trước khi WP-18 merge |
| 2 | §0/§11: "16 metric" | **19** hiện có → **26** sau khi thêm 7 | `eCommerceMetrics.cs:94-124` (WP-18 thêm 3) |
| 3 | §6.1 `RenderResult.ContentFingerprint` | **`BodyFingerprint`** (renderer) + `ContentFingerprint` do appender | `#21`: renderer thuần **không** biết `RecipientHash`/`TemplateVersionId` |
| 4 | §5.5 `FromStatus`/`ToStatus NOT NULL` | **`int?`** + CHECK theo `EventType` | `#22`: `Enqueued` không có trạng thái nguồn |
| 5 | §6.2 route `/api/notification/...` | **Không** đặt `[Route]` tay; route do ABP/BFF sinh; xác minh bằng `pnpm gen:api` | `ADDR-002-arch.md:168-173`: conventional controller; BFF chỉ `/api/v1` (W18-3) ⇒ **chưa xác minh** |
| 6 | §6.2 có `GET /reports/summary` | **Hoãn** ở WP-22 | `REP-001/002` chưa có; BR-020 cấm query DB giao dịch (`AQ2`) |
| 7 | §3 hàng 14 guard `now <= ExpiresAtUtc` | thêm vế **`ExpiresAtUtc IS NULL`** | `#29`: tin không hạn vẫn phải thử lại |
| 8 | §7.1 cột "Ai tiêu thụ" của `NotificationEnqueuedEto` = "job bàn giao" | job **quét DB**; ETO chỉ cho `REP-*` | ADR-05: job không phụ thuộc sự kiện; tránh 2 đường cùng bàn giao |
| 9 | §15.3 giả định "`NOT-002` sẵn sàng" | WP-22 phát hành `INotificationSender` + `ChuaCoKenhGuiSender` fail-closed + `EnableDispatch=false` | `NOT-002` là WP-23; cần chống "gửi ma" |
| 11 | `NOT-002` §6.1: `INotificationSender.SendAsync(SendRequest)` nhận `Recipient` **plaintext** + `TemplateCode` + `Data` (để `NOT-002` render) + `HandleCallbackAsync` | **§4.4 là canonical** (WP-22 phát hành trước): `SendAsync(NotificationSendRequest)` nhận `RecipientEncrypted` + `Subject` + `Body` **đã render**, **bỏ** `TemplateCode`/`Data`; `HandleCallbackAsync` tách sang hợp đồng callback **riêng của `NOT-002`** | Một interface **không** thể có hai định nghĩa (lỗi F1 `XC-003`); ADR-06 (NOT-001 render) + ADR-01 (NOT-002 không dựng nội dung). BA sửa `NOT-002` §6.1 **cùng PR WP-23** (hoặc cùng PR W22-2 để đồng bộ sớm); **không** sửa SRS ở hàng này |

> **Không lệch — tuân thủ (nguyên `#10` của bảng này, nay đưa ra ngoài):** `D-197` ①(2) nói `Notification` *"chỉ `[DependsOn]` **hợp đồng** của `Messaging` **khi cần** outbox"*. Điều kiện *"khi cần"* **không kích hoạt** vì outbox/inbox là hạ tầng ABP đã map trên **DbContext hợp nhất** (`eCommerceDbContext.cs:47,50,389-390`) và publish qua `IDistributedEventBus` (`Volo.Abp.EventBus`); `modules/Messaging/` **không** có `Application.Contracts`. ⇒ **0** tham chiếu `Messaging` là **đúng ý** quyết định, **không** cần BA sửa quyết định/SRS (§2.3).

---

## 16. Truy vết quyết định (`D-xx` ↔ mục)

| `D-xx` | Nội dung | Mục trong tài liệu |
|---|---|---|
| `D-197` | Một module `modules/Notification/` (4 project), một `ConfigureNotification`, một `NotificationErrorCodes.cs`, một enum `ChannelKind`, một `NotificationLockKeys`, permission một chỗ; giữ tên bảng `Notification*`; không gộp `Messaging` | §1.2, §1.4, §2.1–2.3, §3.1, §3.7, §5.6, §8, §15.2 (ghi chú **tuân thủ** `①(2)`), `TC-44` (§13) |
| `D-198` | Giữ `NotificationBatches` + `ChoDuyet`, duyệt **chỉ** mục thuộc lô marketing, tách nhiệm vụ tạo/duyệt | §3.3, §5.3, §8, §11, §13 (`TC-25/TC-26/TC-45`) |
| `D-199` | GĐ3 **chỉ** `Text`; `Html` ⇒ `0020` **trước** khi ghi; giữ cột + CHECK `(0,1)`; 0 sanitizer/NuGet; `Subject` loại CR/LF/NUL | §3.1, §3.6, §4.3, §5.2, §9, §11, §13 (`TC-42/TC-43`) |
| `D-191` | PII mặc định TẮT: template 0 PII; người nhận mã hoá + hash; ETO/log/metric 0 PII; OTP không lưu | §5.4 (cột), §9, §13 (`TC-13/TC-41`) |
| `D-193` | Chi phí gửi là **chi phí kỳ**, thuộc `NOT-002`; **0** `OrderId`/cột chi phí ở `NOT-001` | §1.3, §13 (`TC-40`), §14 (#27) |
| `D-208` (6) | Tạm duyệt `NOT-001` có điều kiện: đã đóng #24 (hàng 15) + #26 (UoW `requiresNew` + 1 `Suppressed`) — **điều kiện**: 15 P3 làm cùng WP | §3.4 (hàng 15 + hàng 13), §6 (L5), §14 (toàn bộ) |
| `D-253` | Tạm duyệt có hiệu lực cho code WP-22 **trên nhánh**; merge `main` vẫn cần biên bản người ký; **`pg_dump` trước migration DB có dữ liệu** | header, §12.1 |
| `D-17` | Enum lưu DB chỉ thêm ở **cuối** | §3.1, §5 (CHECK `IN (…)`) |
| `D-216` | Chuỗi WP code, một luồng, một chủ migration, nhánh xếp chồng | header, §12.1, §5.6 (`eCommerce.slnx`) |

---

## 17. Tự kiểm

- **Bằng chứng đồ thị (N83):** `codegraph_explore "eCommerceDbContext OnModelCreating DbSet ConfigureChannelHub OutgoingEvents IncomingEvents"` → `OnModelCreating` `:353-391`, outbox/inbox `:47,50`, `ConfigureChannelHub` `:358`. `codegraph_explore "ChannelSyncSupervisorWorker …"` → starvation `:84-101`, `Timer.RunOnStart` `:47-48`, log `:58`, `BuildWorkerName` `:139`. `codegraph_explore "eCommerceMetrics …"` → `All` `:94-124` (**19** tên), `KyHieuMetric` `:152-174`, `ChuaCoNoiPhat` `:189-200` (rỗng), `Allowed` `:238-242`, `Forbidden` `:245-248`. `codegraph_explore "ChannelHubConsts … IStringEncryptionService IAbpDistributedLock"` → khoá `channel:…` `:19-32`, giải mã `:160-165`. `codegraph_explore "eCommercePermissionDefinitionProvider …"` → `Define` `:23-25`, `AddChild` `:33-50`. `codegraph_explore "eCommerceHttpApiHostModule DependsOn KeyPrefix …"` + `Grep`: `DependsOn` `:40`, `TransactionBehavior` `:94`, `KeyPrefix` `:231,246`, `AbortOnConnectFail` `:260`, `AbpExceptionHttpStatusCodeOptions` `:308`.
- **Kiểm bằng đọc tệp:** `PostgreSqlSchemaTests.cs:208,215-216,421-423` (**94**); `eCommerceDbContext.cs:353-391`; `eCommerceConsts.cs:363-395`; `StockLedger.cs:240,243`; `INotificationEvent.cs:31`; `DoThoiGianGiuKhoa.cs:35-39,66-78`; `eCommerceEntityFrameworkCoreModule.cs:30-48` (đặc biệt `:47` `AbpBackgroundJobs`); `eCommerceDbMigrator Program.cs:14-23,74-76`; csproj `modules/ChannelHub/*/` (Contracts→Domain+Kernel; Application→+Messaging.Domain — lý do §2.3).
- **0** việc bỏ ngỏ ngoài 7 `AQ`; **0** cam kết endpoint NCC (không gọi ngoài); **0** sửa code/`modules/**`/`src/**`/`test/**`/SRS/`decisions.md`.
- Mọi bảng ghi **module chủ** = `Notification`; **0** tên bảng trùng `NOT-002` (`D-197` ⑤).
- **DOCREV vòng 1 (26/11/2026) — đã sửa 2 P2 + 6 P3:** `#1` §7.2 tách pha (C1) `Moi→ChoGui` (`UPDATE … WHERE Status=Moi` + `ReadyToDispatch` cùng UoW) trước (C2) giành bàn giao + nêu tính nguyên tử/đua với hàng 13 (`TC-01`/`TC-05`) · `#2` chốt §4.4 **canonical** + `docDeviation #11` buộc `NOT-002` §6.1 đổi · `#3` thêm enum `NotificationTemplateStatus` §3.1 · `#4` sửa "12" → **10** `ConfigureXxx` module (§1.1) · `#5` `#27` dẫn đúng ADR-01/SRS §0 (không `D-193`) · `#6` đổi `BodyMasked` → `Body` + nêu rõ payload gửi = bản đã lưu đã masker, cột `…Masked` chỉ audit (§4.4) · `#7` chuyển "0 tham chiếu `Messaging`" ra khỏi bảng `docDeviation` (là **tuân thủ** `D-197` ①(2), §2.3/§15.2).
