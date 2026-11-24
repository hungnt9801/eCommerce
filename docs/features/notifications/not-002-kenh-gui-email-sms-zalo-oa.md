# `NOT-002` — Kênh gửi: email, SMS, Zalo OA

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `NOT-002` |
| **Module** | **Notification** — *module mới, chưa tồn tại trong code* (kế hoạch GĐ3) |
| **Giai đoạn** | **GĐ3 — Điều hành** (theo `docs/implementation-plan.md` Phụ lục A.3, dòng 440) |
| **Ưu tiên** | **Must have** (là điều kiện để `XC-005` Q3 và `XC-003` Q6 có nơi gửi cảnh báo email kỹ thuật) |
| **Trạng thái** | 🟡 `Draft` — chờ review |
| **Người viết / người review** | *(điền)* / **Kiến trúc trưởng + Vận hành + Security** |
| **Phiên bản / ngày** | `v0.1 — 13/10/2026` |
| **Tài liệu liên quan** | `NOT-001` (template & hàng đợi — *chưa có tài liệu*) · `XC-002` (outbox/inbox + DLQ) · `XC-003` (khoá phân tán) · `XC-005` (log/correlationId/metric — **Q3, Q4**) · `XC-001` (tiền tệ/làm tròn) · `XC-006` + `ACC-001` (thuế & xuất liệu kế toán) · `SHP-002`/`SHP-003`/`SHP-004` (`docs/implementation-plan.md:424–427`) · `REP-001` (read-model) · `docs/design-doc-guidelines.md` |

---

## 0. Tóm tắt & quyết định thiết kế

**Tóm tắt:** `NOT-002` chốt **lớp vận chuyển (transport) cho ba kênh gửi ra ngoài** — **email**, **SMS brandname**, **Zalo OA** —
sau một **abstraction duy nhất** (`INotificationChannelSender` + provider registry), kèm **vòng đời gửi có state machine ném lỗi**,
**nhật ký gửi append-only**, **chống trùng ở mọi biên vào** (callback/webhook nhà cung cấp), **retry có giới hạn + DLQ**,
**quota/rate-limit**, **quiet hours + opt-out**, và **sổ chi phí gửi** (append-only, dùng `decimal` theo `XC-001`).

**Ranh giới sống còn với `NOT-001`:** `NOT-001` là *"Template & hàng đợi thông báo (chống gửi trùng)"*
(`docs/implementation-plan.md:439`); `NOT-002` là *"Kênh gửi"* (`docs/implementation-plan.md:440`).
⇒ **`NOT-001` quyết định gửi *GÌ*, cho *AI*, *KHI NÀO*; `NOT-002` quyết định gửi *BẰNG CÁCH NÀO* và *ĐÃ TỚI CHƯA*.**
Trộn hai việc này vào nhau là cách chắc chắn nhất để sinh ra "gửi trùng" và "không biết tin nào đã tới".

### 0.1 Hiện trạng thật (đã kiểm bằng `read`/`grep` — không viết theo trí nhớ)

| Hạng mục | Hiện trạng | Bằng chứng (đường dẫn thật) |
|---|---|---|
| Module `Notification` | ❌ **CHƯA CÓ** — `modules/` hiện có 15 module (`Address`, `Catalog`, `ChannelHub`, `Customers`, `EInvoice`, `Inventory`, `Messaging`, `PaymentHub`, `Pos`, `Pricing`, `Reconciliation`, `Reporting`, `Sales`, `Signing`, `Tax`) nhưng **không** có `Notification` | liệt kê `modules/` (24/11/2026) |
| Project Notification (Domain/EF/Application) | ❌ **CHƯA CÓ** — kế hoạch GĐ3 | `docs/implementation-plan.md:440` |
| Code gửi email / SMS / Zalo OA | ❌ **0 dòng** — `grep` cho `MailKit|SmtpClient|Zalo|Sms|SendGrid|Twilio|eSMS|Email` trong `*.cs` **không** có kết quả nào thuộc miền thông báo; các kết quả duy nhất là (a) cột `Email` của bảng ABP Identity trong `Migrations/`, (b) `AdminEmail` của seeder, (c) `"email"`/`"phone"` trong **danh sách nhãn BỊ CẤM** của metric | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:218–223` · `src/eCommerce.DbMigrator/IdentityDataSeedContributor.cs:35` |
| Gói NuGet gửi thư/SMS | ❌ **0 gói** — không có `MailKit`, `SendGrid`, `Twilio`, `Volo.Abp.Emailing` trong danh sách package tập trung | `Directory.Packages.props` (toàn bộ file, 108 dòng) |
| `eCommerce.Kernel` | ✅ **ĐÃ CÓ** (`shared/eCommerce.Kernel/`) — đã có `Money`/`RoundingPolicy`/`Currency`/`MoneyAllocator`, `LockErrorCodes`, `INotificationEvent`, `Observability/eCommerceMetrics.cs`. ❌ **Chưa có** helper minor-units `ToMinorUnits`/`ILockManager` | `shared/eCommerce.Kernel/Money/Money.cs` · `.../Money/RoundingPolicy.cs` · `.../Locking/LockErrorCodes.cs` · `.../Events/INotificationEvent.cs` |
| Bảng `AbpBackgroundJobs` (job bền vững) | ✅ **CÓ** (đã migrate) ⇒ dùng được cho retry/backoff bền vững | `src/eCommerce.EntityFrameworkCore/Migrations/20261009102627_Added_Phase0_AuthModules.cs:64,83,457` |
| `XC-002` (outbox/inbox + DLQ) | ✅ **ĐÃ TRIỂN KHAI** — bảng `AbpEventOutbox`/`AbpEventInbox`, `DeadLetterAppService` (phát lại DLQ qua outbox), `IdempotencyGuard` dùng chung | `src/eCommerce.EntityFrameworkCore/Migrations/20261024045237_AbpEventBusOutboxInbox.cs:68–72` · `src/eCommerce.Application/Messaging/DeadLetterAppService.cs:40` · `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:50` |
| Distributed lock (`IAbpDistributedLock`, Redis) | ✅ **CÓ thật**, đã dùng cho refresh token | `modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelTokenManager.cs:29,67–91` |
| `KeyPrefix` của lock | ✅ `"ecommerce:"` (đã vá lỗi dính tiền tố) | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:222,237` |
| Metric | ✅ **16 metric đã đăng ký** (`eCommerceMetricNames.All`) **và đã có điểm phát** ở luồng nghiệp vụ (ví dụ `DeadLetterAppService`, `ChannelTokenManager`, `OrderStatusMetricHandler`) | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:81–107` · `test/eCommerce.Tests/Observability/MetricsTests.cs:45–73` |
| Metric cho thông báo | ❌ **CHƯA CÓ** metric nào cho notification; registry hiện **16 tên**, test **so tập tên phát ra với registry** và **không còn** chốt cứng số lượng | `test/eCommerce.Tests/Observability/MetricsTests.cs:64–73` |
| Nhãn metric `email`/`phone` | 🚨 **BỊ CẤM** (cardinality cao) — đây là ràng buộc **trực tiếp** lên `NOT-002` | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:218–223` (`eCommerceMetricTags.Forbidden`) |
| `[Authorize]` cấp class | ✅ **ĐÃ CÓ tiền lệ đúng** (bài học `GetShopsAsync` từng là API công khai) | `src/eCommerce.Application/Channels/ChannelAppService.cs:21–28` |
| `[Authorize]` cấp class cho miền thông báo | ❌ chưa có — module chưa tồn tại | — |
| `DbSet` cho entity của module | ✅ **CÓ tiền lệ**: phải khai `DbSet` trên `eCommerceDbContext` hợp nhất, nếu không `IRepository<T>` **không** được đăng ký | `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:47–64` |
| Unit of Work | ✅ `TransactionBehavior = Auto` cho toàn hệ thống | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:91–95` |
| Setting (`ISettingProvider`, `SettingDefinition`) | ❌ **0 định nghĩa setting trong code app** — chỉ có **bảng** `AbpSettings`/`AbpSettingDefinitions` do migration của ABP tạo | `grep "SettingDefinition"` chỉ khớp trong `Migrations/` |
| Sổ cái append-only | ✅ tiền lệ rõ: `StockLedgerEntry` "APPEND-ONLY. Không bao giờ UPDATE/DELETE" | `src/eCommerce.Domain/Entities/Inventory/StockLedger.cs:80–84` |
| Mask dữ liệu nhạy cảm (kể cả `otp`) | ✅ đã có thật, có test | `modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:313–337` (`MaskSensitiveData`, `otp` ở :323) · `test/eCommerce.Tests/PaymentHub/WebhookMaskingTests.cs` |
| Health check | ✅ `/health-status` kiểm PostgreSQL thật | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:155–158,506–509` |
| Tài liệu `SHP-002/003/004`, `REP-*`, `ACC-001`, `NOT-001` | ❌ **CHƯA CÓ** — `docs/features/` chỉ có `XC-001`…`XC-006` | liệt kê `docs/features/` |

> 🚨 **Kết luận bắt buộc đọc trước khi review:** *toàn bộ `NOT-002` là thiết kế MỚI.*
> **Không có** dòng code nào của miền thông báo để đối chiếu. Mọi mục dưới đây ghi rõ **"chưa có trong code — kế hoạch GĐ3"**
> khi nói tới việc phải viết mới, và chỉ ra **tiền lệ thật** để bám theo khi viết.

**Phạm vi:**
- ✅ **Ba kênh gửi**: email · SMS brandname · Zalo OA (gồm ZNS nếu kênh hỗ trợ — *chưa kiểm chứng*, xem §15 Q3).
- ✅ **Hợp đồng adapter** dùng chung (`INotificationChannelSender`) + provider registry (thêm kênh = thêm class + 1 dòng DI).
- ✅ **Vòng đời một tin** (state machine) + **nhật ký gửi append-only** + **thử lại có giới hạn + DLQ**.
- ✅ **Biên vào**: callback/webhook trạng thái từ nhà cung cấp — **chống trùng bằng unique index** (lock **không** thay thế).
- ✅ **Quota / rate limit** theo kênh + **quiet hours** + **opt-out** (danh sách từ chối).
- ✅ **Sổ chi phí gửi** append-only, `decimal`, theo `XC-001`.
- ✅ **Credential** nhà cung cấp: mã hoá at-rest, không trả ra DTO, không log.
- ✅ **Khoá phân tán** cho tài nguyên chia sẻ (token OA, quota, phát lại DLQ) — theo `XC-003`.

**Không nằm trong phạm vi:**
- ❌ **Soạn nội dung / template / hàng đợi chống trùng** — thuộc **`NOT-001`** (xem ranh giới ở §0).
- ❌ **Quyết định nghiệp vụ "ai được nhận tin gì"** — thuộc module nghiệp vụ (`SAL`, `SHP`, `ACC`…) phát ETO.
- ❌ **Zalo OA / ZNS: thủ tục đăng ký, mẫu tin, cửa sổ gửi, giá** — *chưa kiểm chứng trong repo*, xem §15 Q3.
- ❌ **Báo cáo/tổng hợp hiệu quả gửi** — thuộc **`REP-*`** (đọc read-model, **không** query DB giao dịch — xem §8.3).
- ❌ **Xuất liệu kế toán** (file Excel/CSV cho kế toán) — thuộc **`ACC-001`** liên kết `XC-006`/`XC-001`; `NOT-002` chỉ **gửi** file/thông báo đó.
- ❌ **Push notification (FCM/APNs), chat Facebook, tổng đài** — chưa có nhu cầu trong GĐ3.
- ❌ **Hạ tầng outbox/inbox** — thuộc **`XC-002`** (**đã triển khai**, xem §0.1/F5). `NOT-002` **dùng trực tiếp** `AbpEventOutbox`/`AbpEventInbox`/DLQ; chế độ suy giảm chỉ còn là phương án dự phòng nếu có hồi quy (§15.3 giả định 1).

### 0.2 Quyết định thiết kế (ADR ngắn)

| # | Quyết định | Phương án đã cân nhắc | Lý do chọn | Hệ quả |
|---|---|---|---|---|
| **ADR-01** | **Mọi kênh gửi nằm sau MỘT abstraction** `INotificationChannelSender` + `INotificationChannelFactory` (registry) | (a) `if (kênh == "sms")` rải trong app service · (b) abstraction + registry · (c) dùng `Volo.Abp.Emailing` cho email rồi tự viết phần còn lại | Tiền lệ **đã chạy thật** trong repo: `IChannelConnectorFactory` (`modules/ChannelHub/eCommerce.ChannelHub.Application/Connectors/ChannelConnectorFactory.cs`) và `IPaymentProvider` (`modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/IPaymentProvider.cs`) — thêm nhà cung cấp = thêm class | Không được gọi SDK nhà cung cấp trực tiếp ở app service (BR-001); mọi kênh phải qua interface ⇒ test được bằng fake |
| **ADR-02** | **Tách lớp: `NOT-001` = quyết định gửi, `NOT-002` = vận chuyển & theo dõi phát** | (a) gộp chung một module làm hết · (b) tách theo trách nhiệm | `implementation-plan.md:439–440` đã tách sẵn hai tài liệu; gộp lại thì "chống gửi trùng" và "retry vận chuyển" tranh nhau một trạng thái | `NOT-002` **không** được tự sinh nội dung; mọi yêu cầu vào `NOT-002` phải có `IdempotencyKey` do tầng trên cấp (BR-002) |
| **ADR-03** | **Idempotency ở MỌI biên vào**, cài bằng **bảng + unique index** | (a) tin vào distributed lock · (b) chỉ kiểm ở code · (c) bảng idempotency + unique index | `XC-003` BR-008 + §7.3: lock **không** thay thế idempotency (lock hết TTL ⇒ mất tác dụng). Tiền lệ index thật: `modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs:44–47` (UNIQUE `(TenantId, ChannelCode, ExternalOrderId)` có filter — đơn sàn) và `PaymentTransaction.IdempotencyKey` (`PaymentTransaction.cs:76`) | Phải có bảng `NotificationInbox` + `NotificationMessage` unique index; **thừa** index còn hơn thiếu |
| **ADR-04** | **State machine NÉM LỖI** khi chuyển trạng thái sai, **cấm** no-op im lặng | (a) bỏ qua im lặng · (b) log rồi bỏ qua · (c) `BusinessException` có mã | Bài học eShop (`SetPaidStatus()` no-op) đã được ghi thành nguyên tắc; tiền lệ code thật: `TaxClassification.Approve()` ném `TaxErrorCodes.InvalidStatusTransition` (`modules/Tax/eCommerce.Tax.Domain/Classifications/TaxClassification.cs:122–134`) | Mỗi nhánh state machine cần 1 test; callback đến **sai thứ tự** phải có đường xử lý tường minh (§3 bảng guard) |
| **ADR-05** | **Nhật ký gửi là APPEND-ONLY** (bảng `NotificationDeliveryEvent`), tách khỏi aggregate có trạng thái | (a) một bảng vừa giữ trạng thái vừa làm lịch sử · (b) tách: `NotificationMessage` (trạng thái) + `NotificationDeliveryEvent` (append-only) | Tiền lệ thật: `StockLedgerEntry` "APPEND-ONLY. Không bao giờ UPDATE/DELETE" (`src/eCommerce.Domain/Entities/Inventory/StockLedger.cs:80–84`) và `XC-006` BR-005 | Truy vết "tin này đã đi qua những bước nào" luôn đọc được; **cấm** sửa/xoá event |
| **ADR-06** | **Nhãn metric chỉ lấy từ `eCommerceMetricTags.Allowed`**; **cấm** `email`, `phone`, `messageId` | (a) gắn người nhận làm nhãn để dễ lọc · (b) nhãn hữu hạn + log | `email`/`phone` **đã nằm trong danh sách BỊ CẤM** của repo (`eCommerceMetrics.cs:218–223`, `eCommerceMetricTags.Forbidden`), và `XC-005` BR-009 cấm cardinality cao. Mỗi số điện thoại là **một chuỗi thời gian riêng** ⇒ nổ hệ thống metric | Truy vết theo người nhận **chỉ** bằng **log** (đã có `CorrelationId`), không bằng metric |
| **ADR-07** | **Thêm metric mới ⇒ PHẢI sửa registry + `XC-005` §5.1 + test**, trong **cùng** PR | (a) phát metric mới ngay · (b) cập nhật registry trước | Registry hiện **16 tên** (`eCommerceMetricNames.All`); test **so tập tên phát ra với registry** và **không còn** chốt cứng số lượng (`MetricsTests.cs:45–73`, đã bỏ `Assert.Equal(12, …)`) ⇒ phát metric chưa đăng ký là **CI đỏ**, thêm metric hợp lệ thì không | `NOT-002` cần metric ⇒ đây là **việc phải làm tường minh** trong PR đầu của module, không phải việc phụ |
| **ADR-08** | **Token Zalo OA (có rotate) chỉ ghi trong phạm vi `IAbpDistributedLock` + double-check** | (a) refresh không khoá · (b) khoá nhưng không double-check | Tiền lệ **đã chạy thật**: `ChannelTokenManager` khoá `channel:{channelCode}:{shopId}:token-refresh`, rồi **đọc lại** entity trong khoá (`ChannelTokenManager.cs:67–91`). Shopee rotate refresh token ⇒ refresh song song làm **mất token** | Khoá mới `notification:zalo-oa:{oaId}:token-refresh`; timeout bắt buộc (`XC-003` BR-004); không giành được khoá ⇒ **NÉM** `eCommerce:Lock:0001`, **không** gửi tiếp |
| **ADR-09** | **`NOT-002` KHÔNG gọi app service của module khác** — chỉ **tiêu thụ ETO** | (a) gọi `IShippingAppService.GetWaybillAsync()` để lấy dữ liệu tin · (b) nhận ETO đã đủ dữ liệu | Quy ước bất biến: **module gọi app BỊ CẤM**. Tiền lệ: `ChannelOrderPulledEto` mang **đủ** dữ liệu để app tự tạo đơn, nhờ vậy ChannelHub không cần domain app (`modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:5–15`) | ETO cho `NOT-002` **phải** chứa đủ trường để dựng tin (tên, số tiền, mã đơn) — **không** để consumer phải gọi ngược lại |
| **ADR-10** | **Retry/backoff chạy trên job BỀN VỮNG** (`AbpBackgroundJobs`), **không** dùng timer trong bộ nhớ | (a) timer trong bộ nhớ như supervisor kênh · (b) hàng đợi job bền vững trong DB | Bảng `AbpBackgroundJobs` **đã có** (`20261009102627_Added_Phase0_AuthModules.cs:64`) ⇒ không thêm hạ tầng. Và **bài học F6 thật**: `ChannelSyncSupervisorWorker` từng gọi `AddAsync` mỗi nhịp ⇒ ABP **THAY THẾ** worker ⇒ timer reset ⇒ worker **không bao giờ chạy** (`modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelSyncSupervisorWorker.cs:68–85`) | Tin đang chờ gửi **sống qua restart**; **cấm** thiết kế "giữ hàng đợi trong `ConcurrentDictionary`" |
| **ADR-11** | **Chi phí gửi & mọi số tiền dùng `decimal` + quy ước `XC-001`**; **cấm** `double`/`float`; **cấm** so tiền bằng `==` | (a) `double` cho "đơn giá nhỏ" · (b) `decimal` + so theo **minor units** | `XC-001` BR-003 (cấm `==` trên `decimal`), BR-008 (không `double`/`float` trong **bất kỳ** biểu thức tiền nào kể cả DTO/adapter). Tiền lệ code: `PaymentTransaction.Amount` là `decimal` (`PaymentTransaction.cs:52–53`), `ProductVariant.Price` `HasPrecision(18,4)` (`eCommerceDbContext.cs:126`) | Sổ chi phí gửi `numeric(18,4)`; test reflection chặn `double`/`float` |
| **ADR-12** | **CẤM lưu & CẤM log nội dung nhạy cảm**; nội dung lưu vào DB phải **mask**; **OTP KHÔNG BAO GIỜ được lưu** | (a) lưu nguyên nội dung để gỡ lỗi · (b) lưu bản đã mask + không lưu OTP | `XC-005` BR-005/BR-012; tiền lệ thật: `PaymentTransaction.MaskSensitiveData` che 2 lớp và **che cả `otp`** (`PaymentTransaction.cs:313–337`, `otp` ở :323), đã có test `WebhookMaskingTests` | Tin chứa OTP (đổi COD — `SHP-003`) **không** có bản ghi nội dung; chỉ có bản ghi "đã gửi OTP tới đuôi số X" |

---

## 1. Use Case Specification

| Thuộc tính | Nội dung |
|---|---|
| **Use Case ID** | `NOT-002` |
| **Use Case Name** | Gửi thông báo ra ngoài qua email / SMS / Zalo OA và theo dõi tới đích |
| **Description** | *"Là hệ thống, tôi muốn gửi một thông báo qua email, SMS hoặc Zalo OA bằng **một cách duy nhất**, biết chắc **tin đã tới hay chưa**, **không gửi trùng** khi có retry/callback trùng, **không** để lộ OTP/dữ liệu khách, và **không** gửi vượt hạn mức hay ngoài khung giờ cho phép."* |
| **Actor(s)** | **Hệ thống** (module nghiệp vụ phát ETO) · **`NOT-001`** (hàng đợi + template) · **Nhà cung cấp** (SMTP/API SMS/Zalo OA — gọi vào & callback ra) · **Nhân viên vận hành** (xem DLQ, phát lại, bật/tắt kênh, xem chi phí) · **Khách hàng** (người nhận — chỉ tác động qua opt-out) |
| **Priority** | **Must have** |
| **Trigger** | (a) `NOT-001` chuyển một tin đã dựng nội dung sang bước gửi; (b) đến nhịp job thử lại; (c) nhà cung cấp gọi callback trạng thái; (d) vận hành phát lại DLQ; (e) cảnh báo kỹ thuật từ `XC-005` (`XC-005` §15.1 **Q3**: *"alert gửi email kỹ thuật … Thuộc `NOT-002`"*) hoặc từ `XC-003` **Q6** |
| **Pre-Condition(s)** | • **Tenant hiện tại** (`TenantId`) — mọi bản ghi và credential đều theo tenant (`IMultiTenant`).<br>• **Kênh đã được bật** cho tenant và **có credential hợp lệ** (email: SMTP/API key; SMS: brandname + API key; Zalo OA: `OA access token` còn hiệu lực **hoặc** có refresh token để làm mới).<br>• **Người nhận có định danh hợp lệ** cho kênh đó (email đã chuẩn hoá; số điện thoại **đã chuẩn hoá về E.164 hoặc đầu số VN**; Zalo `user_id`/`oa_id`).<br>• **`IdempotencyKey` ổn định** do tầng trên (`NOT-001`) cấp — **không** phải `Guid.NewGuid()` mỗi lần gọi.<br>• **`ChannelCode`** (*kênh bán*) **chỉ** đưa vào log/ngữ cảnh, **không** quyết định kênh gửi: cùng một đơn từ Shopee và từ website có thể gửi cùng một mẫu SMS.<br>• **`XC-002` (outbox/inbox) sẵn sàng** — nếu chưa, module chạy chế độ suy giảm (§15.3 giả định 1). |
| **Post-Condition(s)** | • Mỗi yêu cầu gửi có **đúng một** `NotificationMessage` (ràng buộc unique trên `IdempotencyKey`).<br>• Trạng thái cuối của tin thuộc tập **kết thúc**: `DaPhat` · `KhongPhatDuoc` · `HetHanXacNhan` · `ThatBai` · `Huy`.<br>• Mọi bước chuyển trạng thái có **một** `NotificationDeliveryEvent` **append-only** (kèm `CorrelationId`, `ProviderCode`, `Status`, `Reason`).<br>• Tin chưa gửi được **không** bị mất: nằm ở `ChoThuLai` (có `NextAttemptAtUtc`) hoặc `DLQ`.<br>• Chi phí phát sinh ghi vào `NotificationCostLedger` (append-only) với `decimal(18,4)`.<br>• **Không** tồn tại bản ghi chứa OTP; nội dung lưu đã **mask**. |
| **Basic Flow** | 1. `NOT-001` gọi `INotificationSender.SendAsync(request)` với `IdempotencyKey`, `ChannelKind` (Email·Sms·ZaloOa), `Recipient`, `TemplateCode`, `Data`, `TenantId`.<br>2. `NotificationSender` **kiểm idempotency**: tra `NotificationMessage` theo `(TenantId, IdempotencyKey)`. **Đã có** ⇒ trả kết quả cũ, **không** gửi lại (BR-002).<br>3. Kiểm **opt-out** (số/email đã từ chối) và **quiet hours**; vi phạm ⇒ `Huy` (opt-out) hoặc dời `NextAttemptAtUtc` sang khung cho phép (quiet hours) (BR-012).<br>4. Trong **một Unit of Work**: tạo `NotificationMessage` (`ChoGui`) + `NotificationDeliveryEvent(Queued)` + ghi **outbox** (theo `XC-002`) (BR-006).<br>5. Job nền lấy tin `ChoGui` có `NextAttemptAtUtc <= now` ⇒ giành **khoá phân tán** `notification:provider:{providerCode}:quota:{tenantId}` (khoá ở §8.2; không giành được ⇒ **ném** `eCommerce:Lock:0001`).<br>6. Gọi adapter của kênh: `await sender.SendAsync(message, ct)` với **timeout** và **retry có giới hạn** (BR-011).<br>7. Thành công (nhà cung cấp **nhận**, có `ProviderMessageId`) ⇒ `DaGui` + `DeliveryEvent(Sent)`. Lỗi **tạm thời** ⇒ `ChoThuLai` + `NextAttemptAtUtc = now + backoff`. Lỗi **vĩnh viễn** ⇒ `ThatBai`.<br>8. Nhà cung cấp gọi **callback** trạng thái ⇒ **chống trùng** bằng unique index, ghi `DeliveryEvent` và cập nhật `DaPhat`/`KhongPhatDuoc` (BR-003).<br>9. Ghi **chi phí** vào `NotificationCostLedger` (append-only) với `decimal(18,4)` (BR-013). |
| **Alternative Flow** | **A1: Email tới nhiều người nhận (to/cc/bcc)** — **mỗi người nhận một `NotificationMessage`** (một `Recipient`/tin; khoá `{gốc}:{channelKind}:{h}`, `h` = HMAC rút gọn người nhận theo `D-204`/`D-206`) cùng `RootMessageId`; trạng thái tổng = *kết quả xấu nhất*. **Không** có bảng con `NotificationRecipient` (cột người nhận nằm trên `NotificationMessages`, §5.1).<br>**A2: Kênh ưu tiên dự phòng** — Zalo OA thất bại **vĩnh viễn** ⇒ gửi SMS dự phòng **cùng** `IdempotencyKey` gốc với hậu tố `:fallback-sms` (**khoá khác** ⇒ không bị chặn nhầm, nhưng vẫn truy vết được về tin gốc qua `RootMessageId`).<br>**A3: Cảnh báo kỹ thuật** (`XC-005` Q3) — dùng **cùng** pipeline (`TemplateCode=ops-alert`); người nhận = **cấu hình** `NotificationChannelAccounts.Settings.opsAlertRecipients` (trần **20**/kênh; trống ⇒ **fail-closed NÉM** `0003`, **0** tin, log `Error` + metric; **cấm** hard-code/bịa địa chỉ) — **không** phải khách hàng ⇒ **cấm** opt-out áp lên nhóm này, **cấm** quiet hours chặn mức `Critical` (BR-012). `Priority=Critical` ⇒ **fan-out song song** theo `Settings.opsAlertCriticalChannels` (mặc định `["Email"]`): mỗi (người nhận × kênh) một `NotificationMessage` khoá `{gốc}:{channelKind}:{h}` (`h` = HMAC rút gọn người nhận, khuôn `D-204`); Email **không** triệt SMS; **≥1** kênh tới ⇒ coi là đã tới; **tất cả** fail ⇒ `DLQ` + `Error` + metric, chống đệ quy. Kênh 2 bật **chỉ khi** có NCC + credential + `unitCostPerMessage` (GĐ3 mặc định **Email-only**; rủi ro F6 **chặn release** tới khi bật kênh 2 hoặc chủ dự án chấp nhận **bằng văn bản**). Cảnh báo **host-level** (`TenantId = null`) **chưa có đường gửi** — nợ có chủ OPS + BA `XC-005`.<br>**A4: Vận hành phát lại DLQ** — giành khoá `notification:dlq-replay:{tenantId}` (chỉ **một** tiến trình), sau đó đưa tin về `ChoGui` với `AttemptCount = 0` và ghi `DeliveryEvent(ReplayedBy=userId)`.<br>**A5: Nhà cung cấp chậm** — quá `SentAckTtl` mà **không** có callback ⇒ `HetHanXacNhan` + metric; **không** tự gửi lại (tránh gửi trùng ra người thật) mà đưa vào báo cáo đối soát (§8.3). |
| **Exception Flow** | **E1:** trùng `IdempotencyKey` ⇒ **không** gửi lại, trả kết quả cũ + `AlreadySent=true`.<br>**E2:** người nhận **sai định dạng** (email không hợp lệ / số điện thoại sai) ⇒ `ThatBai` vĩnh viễn, `eCommerce:Notification:0002`, **không** thử lại.<br>**E3:** thiếu credential / credential hết hạn ⇒ `eCommerce:Notification:0003`, tin ở `ChoThuLai` với backoff dài; **có** metric + **cảnh báo vận hành** (nếu im lặng thì thông báo chết hàng loạt mà không ai biết).<br>**E4:** hết hạn mức nhà cung cấp (SMS/Zalo) ⇒ `ChoThuLai` + `Reason=quota`, **không** tính là lỗi vĩnh viễn.<br>**E5:** quá số lần thử ⇒ `DLQ` + `eCommerce:Notification:0005`; **cấm** thử vô hạn.<br>**E6:** callback có **chữ ký sai / không xác thực được** ⇒ **từ chối**, ghi `NotificationInbox` với `ProcessStatus=Rejected`, tăng `payment_*`-kiểu counter tương ứng (metric mới, ADR-07), **không** đổi trạng thái tin.<br>**E7:** callback đến **sai thứ tự** (đã `DaPhat` rồi mới nhận `Sent`) ⇒ **không** lùi trạng thái; ghi event với `Status=IgnoredOutOfOrder` + log `Warning` (BR-004).<br>**E8:** không giành được khoá ⇒ `eCommerce:Lock:0001` (dùng **nguyên** mã lỗi của `XC-003`, không tự đặt mã mới). |
| **Business Rules** | • **BR-NOT-002-001**: Mọi lần gửi **phải** đi qua `INotificationChannelSender`; **cấm** gọi SDK/HTTP của nhà cung cấp trực tiếp từ app service hoặc job.<br>• **BR-NOT-002-002**: Mọi yêu cầu gửi **phải** có `IdempotencyKey` **ổn định**; gửi lại cùng khoá ⇒ **không** tạo tin thứ hai (ràng buộc unique ở DB, không chỉ kiểm ở code).<br>• **BR-NOT-002-003**: Mọi **callback/webhook** nhà cung cấp **phải** chống trùng bằng unique index trên `(TenantId, ProviderCode, ProviderMessageId, EventType)`; **cấm** dựa vào distributed lock để chống trùng.<br>• **BR-NOT-002-004**: **Mọi** chuyển trạng thái sai **phải NÉM** `BusinessException` có mã; **cấm** no-op im lặng. Chuyển trạng thái **lùi** (đã phát → chưa phát) **cấm** tuyệt đối.<br>• **BR-NOT-002-005**: `NotificationDeliveryEvent` là **APPEND-ONLY** — **cấm** `UPDATE`/`DELETE`; sửa sai phải ghi event mới.<br>• **BR-NOT-002-006**: Mọi thao tác ghi đi qua **Unit of Work**; `NotificationMessage` + `NotificationDeliveryEvent` + **outbox** ghi trong **cùng** transaction.<br>• **BR-NOT-002-007**: Nhãn metric **chỉ** lấy từ `eCommerceMetricTags.Allowed`; 🚨 **cấm** `email`, `phone`, `messageId`, `recipientId` (cardinality cao).<br>• **BR-NOT-002-008**: Thêm/đổi metric hoặc tag **phải** cập nhật `eCommerceMetricNames`/`eCommerceMetricTags` **và** `XC-005` §5.1 **và** test — trong **cùng** PR.<br>• **BR-NOT-002-009**: 🚨 **CẤM log & CẤM lưu OTP**; nội dung tin lưu vào DB **phải** đi qua hàm mask (kế thừa `PaymentTransaction.MaskSensitiveData`); **cấm** log token nhà cung cấp, số điện thoại đầy đủ, email đầy đủ ở mức `Information`.<br>• **BR-NOT-002-010**: Token Zalo OA **chỉ** được ghi trong phạm vi khoá `notification:zalo-oa:{oaId}:token-refresh` **và** phải **đọc lại (double-check)** trong khoá trước khi gọi API làm mới.<br>• **BR-NOT-002-011**: Mọi lần gọi nhà cung cấp **phải** có **timeout hữu hạn** và **retry có giới hạn** + backoff; quá giới hạn ⇒ `DLQ`. **Cấm** retry vô hạn và **cấm** gọi không timeout.<br>• **BR-NOT-002-012**: **Quiet hours** + **opt-out** **phải** được tôn trọng cho tin tới **khách hàng**; **ngoại lệ có kiểm soát**: cảnh báo kỹ thuật mức `Critical` **không** bị quiet hours chặn, và opt-out **không** áp cho người nhận nội bộ.<br>• **BR-NOT-002-013**: Mọi số tiền (chi phí gửi, số tiền in trong tin) **phải** là `decimal`; 🚨 **cấm** `double`/`float`; 🚨 **cấm** so sánh tiền bằng `==` (theo `XC-001` BR-003/BR-008).<br>• **BR-NOT-002-014**: 🚨 Trạng thái vận đơn đến từ webhook **KHÔNG xác thực được** (GHTK — `SHP-002`, `docs/implementation-plan.md:425` ghi rõ *"không có HMAC ở GHTK"*) **KHÔNG** được dùng làm **nguồn duy nhất** để gửi tin có **số tiền/COD**; trước khi gửi tin chứa số tiền **phải** có nguồn thứ hai đối chiếu, hoặc gửi tin **không** chứa số tiền.<br>• **BR-NOT-002-015**: 🚨 OTP đổi COD (`SHP-003`, `docs/implementation-plan.md:426` ghi rõ *"GHN cần OTP"*) **chỉ** gửi qua kênh **đã xác thực danh tính người nhận**; OTP **không** được lưu vào bất kỳ bảng nào, **không** được log; có **TTL** và **giới hạn số lần gửi lại** trong một cửa sổ thời gian.<br>• **BR-NOT-002-016**: **Cấm** `NOT-002` gọi app service của module khác; dữ liệu để dựng tin **phải** có sẵn trong ETO (ADR-09).<br>• **BR-NOT-002-017**: Credential nhà cung cấp **phải** mã hoá at-rest và **cấm** trả ra DTO (kể cả bản đã mã hoá) — kế thừa `ChannelShop.AccessTokenEncrypted` + `ChannelAppService.ToDto` (`src/eCommerce.Application/Channels/ChannelAppService.cs:189–209`).<br>• **BR-NOT-002-018**: Mọi lỗi **đã xử lý** (bị nuốt có chủ ý) **phải** log ở mức ≥ `Warning` + **có `ErrorCode`** — **cấm** nuốt im lặng (kế thừa `XC-005` BR-013).<br>• **BR-NOT-002-019**: 🚨 **Chống trùng callback** bằng **một** cột `DedupKey` + **một** UNIQUE `UX_NotificationInbox_Dedup` `(TenantId, ProviderCode, ProviderMessageId, EventType, DedupKey)` **NULLS NOT DISTINCT** (`D-203`). Khoá sinh ở **một cửa** `INotificationCallbackParser`: có id sự kiện ⇒ `"e:" + id`; không có ⇒ `"h:" + sha256(JSON chuẩn hoá nội dung **nghiệp vụ**, bỏ trường vận chuyển/không ổn định)`; id > 128 ⇒ `"h:" + sha256(giá trị nguyên)`. **Cấm** `Guid.NewGuid()`, **cấm** cắt chuỗi, **cấm** băm bản đã mask (băm payload thô **trước** mask).<br>• **BR-NOT-002-020**: 🚨 `NotificationOptOuts.RecipientHash` = **băm một chiều** HMAC-SHA256 hex thường **64** ký tự (`^[0-9a-f]{64}$`) của `"v1|" + ChuẩnHoáNgườiNhận`, chuẩn hoá ở **một cửa** `NotificationRecipientHash.Tinh` (`D-204`). **0** cột plaintext; kiểm opt-out **trước mọi lần gửi** bằng **cùng** hàm; ghi qua **một cửa** `CreateOptOutAsync` + UNIQUE `(TenantId, ChannelKind, RecipientHash)` **NULLS NOT DISTINCT**; thiếu khoá production ⇒ **fail-closed NÉM**.<br>• **BR-NOT-002-021**: `SentAckTtl` lưu ở `NotificationChannelAccounts.Settings.sentAckTtlMinutes` (mặc định Email **1440** / SMS **720** / ZaloOa **720** phút; biên `[60, 43200]`; trống/`≤0`/sai ⇒ **mặc định** + log `Warning` + `ErrorCode`, **cấm** hết hạn tức thì). Hết TTL **chỉ** chuyển `DaGui → HetHanXacNhan` + **đúng một** event `AckExpired` + metric; **0** gọi mạng/0 gửi lại/0 xoá/0 ghi chi phí (`D-205`).<br>• **BR-NOT-002-022**: Cảnh báo kỹ thuật: người nhận = **cấu hình** `Settings.opsAlertRecipients` (trần **20**/kênh; trống ⇒ **NÉM** `0003`, 0 tin, log `Error` + metric; **cấm** hard-code). `Priority=Critical` ⇒ **fan-out song song** theo `Settings.opsAlertCriticalChannels` (mặc định `["Email"]`), mỗi (người nhận × kênh) một `NotificationMessage`; ≥1 kênh tới ⇒ đã tới, tất cả fail ⇒ `DLQ` + metric, chống đệ quy; `Critical` không bị quiet hours chặn; opt-out không áp cho người nhận nội bộ; kênh 2 chỉ bật khi có NCC + credential + `unitCostPerMessage` (`D-206`).<br>• **BR-NOT-002-023**: 🚨 `NotificationOptOuts` là **nguồn sự thật duy nhất** + **cửa kiểm duy nhất** trước mỗi lần gửi; đồng bộ với NCC **chỉ** qua **hai adapter** — KÉO (`IOptOutSourceAdapter` + job, `Settings.optOutSync.mode` mặc định **off**) và ĐẨY (adapter trả `SendOutcome.RecipientRefused` ⇒ core ghi opt-out `Source=ProviderReport` cùng UoW + **không** retry) (`D-207`). **Cấm** đẩy danh sách của ta sang NCC; `required=true` mà danh sách chưa có/cũ ⇒ `0003` fail-closed; **không** kết luận tuân thủ pháp luật tin quảng cáo (`R3-5`).<br>• **BR-NOT-002-024**: 🚨 Phần **trung lập** NCC: mọi phụ thuộc hãng nằm ở `Settings` + ABP Setting (`ProviderCode`, base URL, `SenderIdentity`, credential mã hoá, QuietHours, DailyQuota, đơn giá/segment); **0** hằng số NCC/0 NuGet NCC; mọi test dùng `fake` sender (0 kết nối mạng). Kênh tắt/thiếu credential/thiếu đơn giá/thiếu `znsTemplateCode` ⇒ **fail-closed NÉM** `0003`, **không** gọi mạng, **không** đánh dấu `DaGui`, **không** suy đoán `UnitCost` (`D-200`/`D-202`). |
| **Non-Functional Requirement** | • **NFR-NOT-002-001**: Độ trễ `SendAsync` (đã có credential, kênh rảnh) ⇒ **p95 < 2 s** cho SMS/Zalo OA, **< 5 s** cho email.<br>• **NFR-NOT-002-002**: Cùng `IdempotencyKey` gửi **1.000** lần ⇒ **đúng 1** tin gửi ra ngoài, **1** `NotificationMessage`.<br>• **NFR-NOT-002-003**: **100 %** callback có `CorrelationId` **và** được chống trùng (integration test gửi cùng callback 2 lần).<br>• **NFR-NOT-002-004**: **0** dòng log chứa OTP / token / số điện thoại đầy đủ (quét mã nguồn ở CI).<br>• **NFR-NOT-002-005**: **0** nhãn metric cardinality cao; **< 100** series mỗi metric.<br>• **NFR-NOT-002-006**: Retry tối đa **3** lần với backoff **1 s → 5 s → 25 s**; lần thứ 4 ⇒ `DLQ`. Không có tin nào thử quá **3** lần.<br>• **NFR-NOT-002-007**: Thông lượng tối thiểu **50 tin/giây/instance** với `ChannelKind=Sms` (đo bằng fake sender).<br>• **NFR-NOT-002-008**: **100 %** tin marketing có `NextAttemptAtUtc` nằm **trong** khung giờ cho phép (test với `TimeProvider` giả).<br>• **NFR-NOT-002-009**: Cách ly tenant: **0** trường hợp tenant B đọc/ghi được credential hay nội dung của tenant A.<br>• **NFR-NOT-002-010**: **0** tin gửi tới người nhận đã opt-out cho **mọi** `Source` (khách yêu cầu / thủ công / NCC báo / file NCC) (test 1.000 lần ngẫu nhiên).<br>• **NFR-NOT-002-011**: Chi phí gửi tính đúng tới **0,0001** đơn vị tiền và **khớp** file đối soát nhà cung cấp (lệch **0**); phép so dùng **minor units**.<br>• **NFR-NOT-002-012**: Log là **một dòng JSON**/sự kiện, có `CorrelationId` (kế thừa `XC-005` BR-010/BR-002).<br>• **NFR-NOT-002-013**: Không query nặng trên DB giao dịch khi mở báo cáo — **0** truy vấn không-index trên `NotificationDeliveryEvents` từ UI.<br>• **NFR-NOT-002-014**: Metric phát ra **nằm trong** bảng đăng ký — **100 %** (test `TC-06` hiện có, `MetricsTests.cs:38–47`). |

---

## 2. Sơ đồ Use Case

```mermaid
flowchart LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef usecase fill:#e2f4f6,stroke:#17a2b8,stroke-width:2px;
    classDef note fill:#fffbe6,stroke:#ffc107,stroke-width:1px,color:#5c4a00;

    KH(("👤<br/>Khách hàng<br/>(người nhận)"))
    NV(("👤<br/>Nhân viên<br/>vận hành"))
    HT(("⚙️<br/>Module khác<br/>(SAL · SHP · ACC · TAX)"))

    subgraph NOT["📦 Module Notification — lớp kênh gửi NOT-002"]
        direction TB
        UC1(["«gửi email»"])
        UC2(["«gửi SMS brandname»"])
        UC3(["«gửi Zalo OA»"])
        UC4(["«nhận callback trạng thái»"])
        UC5(["«thử lại có giới hạn và DLQ»"])
        UC6(["«quản lý khoá và credential»"])
        UC7(["«opt-out và quiet hours»"])
        UC8(["«sổ chi phí gửi»"])
        class UC1,UC2,UC3,UC4,UC5,UC6,UC7,UC8 usecase;
    end

    N1["<b>Ranh giới:</b><br/>«NOT-001 quyết định gửi GÌ và KHI NÀO»<br/>«NOT-002 quyết định gửi BẰNG CÁCH NÀO và ĐÃ TỚI CHƯA»"]
    class N1 note;

    HT --- UC1
    HT --- UC2
    HT --- UC3
    NV --- UC5
    NV --- UC6
    NV --- UC8
    KH --- UC7
    UC1 -. "«include»" .-> UC4
    UC2 -. "«include»" .-> UC4
    UC3 -. "«include»" .-> UC4
    UC1 -. "«include»" .-> UC5
    UC2 -. "«include»" .-> UC5
    UC3 -. "«include»" .-> UC5
    UC3 -. "«include»" .-> UC6
    UC5 -.- N1
```

### Chú giải

| Ký hiệu | Ý nghĩa |
|---|---|
| ⭕ Xanh lá | Actor (kể cả hệ thống khác và nhà cung cấp) |
| 🟦 Bầu dục xanh dương | Use Case |
| 🟨 Khung vàng | Note |
| `───` | Liên kết Actor ↔ Use Case |
| `-.«include».->` | Quan hệ bao hàm |

---

## 3. Vòng đời một tin gửi (state machine)

> 📌 **Giới hạn sơ đồ:** tài liệu này dùng **đúng 3** sơ đồ Mermaid (đây là sơ đồ thứ 2). ERD ở §5 vì vậy được **mô tả bằng bảng** thay vì sơ đồ.

```mermaid
stateDiagram-v2
    [*] --> ChoGui : «NOT-001 enqueue»
    ChoGui --> DangGui : «job giành được khoá»
    ChoGui --> Huy : «opt-out hoặc huỷ có lý do»
    DangGui --> DaGui : «nhà cung cấp nhận, có ProviderMessageId»
    DangGui --> ChoThuLai : «lỗi TẠM THỜI: timeout, 5xx, quota»
    DangGui --> ThatBai : «lỗi VĨNH VIỄN: định danh sai, template sai»
    ChoThuLai --> DangGui : «backoff xong và còn lượt»
    ChoThuLai --> DLQ : «quá 3 lần thử»
    DaGui --> DaPhat : «callback delivered»
    DaGui --> KhongPhatDuoc : «callback undeliverable hoặc bounced»
    DaGui --> HetHanXacNhan : «quá TTL không có callback»
    DLQ --> DangGui : «vận hành phát lại có duyệt»
    Huy --> [*]
    DaPhat --> [*]
    KhongPhatDuoc --> [*]
    HetHanXacNhan --> [*]
    ThatBai --> [*]
```

**Bảng chuyển trạng thái & guard (mọi chuyển sai **phải ném lỗi**, không im lặng):**

| # | Từ → Sang | Guard (điều kiện cho phép) | Ai được chuyển | Side effect | Nếu guard sai |
|---|---|---|---|---|---|
| 1 | `[*]` → `ChoGui` | `IdempotencyKey` **chưa** tồn tại cho `(TenantId, IdempotencyKey)` | `NotificationSender` | ghi `NotificationMessage` + `DeliveryEvent(Queued)` + outbox (cùng UoW) | **NÉM** `eCommerce:Notification:0001` (trùng khoá) — hoặc trả kết quả cũ nếu ở chế độ "đã gửi" (BR-002) |
| 2 | `ChoGui` → `DangGui` | Giành được khoá `notification:provider:{providerCode}:quota:{tenantId}` **và** `NextAttemptAtUtc <= now` | Job nền | ghi `DeliveryEvent(Sending)` | **NÉM** `eCommerce:Lock:0001` (**mã của `XC-003`**, không tự đặt mã mới) |
| 3 | `DangGui` → `DaGui` | Adapter trả `Accepted=true` **và** có `ProviderMessageId` | Adapter kênh | `DeliveryEvent(Sent)` + ghi `ProviderMessageId` | **NÉM** `eCommerce:Notification:0006` (nhà cung cấp nhận nhưng thiếu mã ⇒ không theo dõi được) |
| 4 | `DangGui` → `ChoThuLai` | `AttemptCount < 3` **và** lỗi phân loại là **tạm thời** | Adapter kênh | `AttemptCount++`, `NextAttemptAtUtc = now + backoff`, `DeliveryEvent(RetryScheduled)` | Nếu `AttemptCount >= 3` ⇒ sang `DLQ` + `eCommerce:Notification:0005` |
| 5 | `ChoThuLai` → `DangGui` | `NextAttemptAtUtc <= now` **và** còn lượt | Job nền | `DeliveryEvent(Retrying)` | Ở lại `ChoThuLai` (không lỗi — đúng thiết kế) |
| 6 | `ChoThuLai` → `DLQ` | `AttemptCount >= 3` **hoặc** lỗi vĩnh viễn sau khi đã thử | Job nền | `DeliveryEvent(MovedToDlq)` + **metric** | **NÉM** `eCommerce:Notification:0005` nếu bị gọi sai trạng thái |
| 7 | `DangGui` → `ThatBai` | Lỗi phân loại **vĩnh viễn** (`InvalidRecipient`, `TemplateRejected`) | Adapter kênh | `DeliveryEvent(FailedPermanent)` | **NÉM** `eCommerce:Notification:0002` kèm `Reason` |
| 8 | `DaGui` → `DaPhat` | Callback **hợp lệ** `EventType=Delivered` **và** chưa từng nhận event này | Webhook nhận callback | `DeliveryEvent(Delivered)` + `DeliveredAtUtc` | Trùng ⇒ **không** ghi thêm, trả `200` (idempotent, BR-003) |
| 9 | `DaGui` → `KhongPhatDuoc` | Callback **hợp lệ** `EventType=Bounced/Undeliverable` | Webhook nhận callback | `DeliveryEvent(Undeliverable)` | như trên |
| 10 | `DaGui` → `HetHanXacNhan` | `now > SentAtUtc + SentAckTtl` (`Settings.sentAckTtlMinutes`, mặc định Email **1440** / SMS **720** / ZaloOa **720** phút, biên `[60, 43200]`) **và** không có callback | Job đối soát | **đúng một** `DeliveryEvent(AckExpired)` + **metric**; **0** gọi mạng/0 gửi lại/0 xoá/0 ghi chi phí | Ở lại `DaGui` (không lỗi) |
| 11 | `DaPhat` → `*` (lùi) | 🚨 **KHÔNG BAO GIỜ** | — | — | **NÉM** `eCommerce:Notification:0007` (`InvalidStatusTransition`) — trạng thái **kết thúc là hấp thụ** |
| 12 | `DLQ` → `DangGui` | Người phát lại có quyền `eCommerce.Notification.Dlq.Replay` **và** giành được khoá `notification:dlq-replay:{tenantId}` | Vận hành | `AttemptCount = 0`, `DeliveryEvent(Replayed, by=userId)` | **NÉM** `403` nếu thiếu quyền; **NÉM** `eCommerce:Lock:0001` nếu đang có người phát lại |
| 13 | `ChoGui` → `Huy` | Có `opt-out` **hoặc** người dùng huỷ yêu cầu | `NotificationSender` | `DeliveryEvent(Cancelled, reason)` | **NÉM** `eCommerce:Notification:0007` nếu cố huỷ tin **đã phát** (BR-004) |

> 📌 **`SentAckTtl` (`D-205`)**: mặc định `Email=1440` · `Sms=720` · `ZaloOa=720` **phút**, lưu theo **dòng kênh** ở `NotificationChannelAccounts.Settings.sentAckTtlMinutes` (jsonb, **không** secret); thiếu khoá ⇒ mặc định theo `ChannelKind` (một bảng hằng trong module). Biên `[60, 43200]`; trống/`≤0`/sai ⇒ **mặc định** + log `Warning` + `ErrorCode`, **không bao giờ** hết hạn tức thì. Hết TTL **chỉ** `DaGui → HetHanXacNhan` + **đúng một** `AckExpired`; siết theo SLA NCC = **đổi cấu hình sau pilot** (0 code, 0 migration).

> ⚠️ **Trạng thái nguy hiểm nhất là `HetHanXacNhan`.** Nhà cung cấp **có thể đã gửi** nhưng callback bị mất
> (đúng bài học `XC-006` §7.1 về eShop: *"consumer luôn ACK kể cả khi handler lỗi ⇒ mất sự kiện vĩnh viễn"*).
> Vì vậy khi rơi vào trạng thái này **tuyệt đối KHÔNG tự gửi lại** — gửi lại có thể làm khách nhận **2** tin
> (và với Zalo OA/SMS, gửi trùng còn **tốn tiền thật**). Hành vi đúng: đưa vào **báo cáo đối soát** (§8.3) và
> để người quyết định. Đây là lý do `HetHanXacNhan` **không** có cạnh quay lại `ChoThuLai`.

---

## 4. Sequence Diagram & luồng dữ liệu

### 4.1 Luồng gửi + callback trạng thái

```mermaid
sequenceDiagram
    participant MOD as Module khác (SAL · SHP · TAX · ACC)
    participant N1 as NOT-001 (template + hàng đợi)
    participant SVC as NotificationSender (NOT-002)
    participant LOCK as IAbpDistributedLock
    participant CH as Adapter kênh
    participant P as Nhà cung cấp (SMTP · SMS · Zalo OA)
    participant DB as PostgreSQL

    MOD->>N1: «phát ETO kèm CorrelationId»
    N1->>SVC: «SendAsync(IdempotencyKey, ChannelKind, Recipient, TemplateCode, Data)»
    SVC->>DB: «tra (TenantId, IdempotencyKey)»
    alt «đã tồn tại»
        DB-->>SVC: «bản ghi cũ»
        SVC-->>N1: «trả kết quả cũ, AlreadySent=true — KHÔNG gửi lại»
    else «chưa tồn tại»
        SVC->>DB: «ghi Message + DeliveryEvent(Queued) + outbox — CÙNG Unit of Work»
        SVC->>LOCK: «khoá notification:provider:quota:{tenantId}»
        alt «không giành được khoá»
            LOCK-->>SVC: «null»
            SVC-->>N1: «NÉM eCommerce:Lock:0001»
        else «giành được»
            SVC->>CH: «SendAsync kèm timeout»
            CH->>P: «HTTP hoặc SMTP»
            alt «nhà cung cấp nhận»
                P-->>CH: «accepted + ProviderMessageId»
                CH-->>SVC: «Accepted=true»
                SVC->>DB: «DaGui + DeliveryEvent(Sent)»
            else «lỗi tạm thời»
                P-->>CH: «timeout hoặc 5xx»
                CH-->>SVC: «Transient»
                SVC->>DB: «ChoThuLai + NextAttemptAtUtc = now + backoff»
            else «lỗi vĩnh viễn»
                CH-->>SVC: «Permanent + Reason»
                SVC->>DB: «ThatBai + DeliveryEvent(FailedPermanent)»
            end
        end
        SVC-->>N1: «kết quả»
    end
    P->>SVC: «callback trạng thái (webhook)»
    SVC->>DB: «chống trùng bằng unique index → append DeliveryEvent»
    Note over SVC,DB: «callback trùng thì KHÔNG ghi thêm và vẫn trả 200»
```

### 4.2 DFD — từ nghiệp vụ tới người nhận (mô tả, không vẽ sơ đồ)

| Bước | Nguồn → Đích | Dữ liệu | Ghi chú |
|---|---|---|---|
| 1 | Module nghiệp vụ → `NOT-001` | ETO (đủ trường để dựng tin — ADR-09) | `NOT-002` **không** gọi ngược lại module nào |
| 2 | `NOT-001` → `NOT-002` | `SendRequest` (khoá idempotency + template + data) | Đây là **biên vào thứ nhất** ⇒ kiểm idempotency |
| 3 | `NOT-002` ↔ PostgreSQL | `NotificationMessages`, `NotificationDeliveryEvents`, `NotificationInbox`, `NotificationCostLedger`, `NotificationChannelAccounts`, `NotificationOptOuts` | 6 bảng, xem §5 (opt-out là biên đọc **trước mỗi lần gửi** — BR-020/BR-023) |
| 4 | `NOT-002` → Nhà cung cấp | HTTP/SMTP (timeout + retry) | Khoá quota bao ngoài |
| 5 | Nhà cung cấp → `NOT-002` | Callback trạng thái | **Biên vào thứ hai** ⇒ chống trùng bằng unique index |
| 6 | `NOT-002` → `REP-*` / `ACC-001` | `DeliveryEvent` + `NotificationCostLedger` | Qua **read-model**, **không** cho `REP-*` query trực tiếp DB giao dịch (§8.3) |

---

## 5. Mô hình dữ liệu

> 📌 **Không vẽ ERD** để giữ đúng giới hạn **3 sơ đồ Mermaid** của tài liệu này (2 sơ đồ đã dùng ở §2 và §3).
> Quan hệ giữa các bảng được ghi rõ bằng cột **FK** trong Data Dictionary dưới đây.
>
> 🚨 **Trạng thái code:** module `Notification` **chưa có trong code** ⇒ toàn bộ §5 là **thiết kế mới, kế hoạch GĐ3**.
> Khi triển khai **bắt buộc**: (a) mọi entity **phải** có `DbSet` trên `eCommerceDbContext` hợp nhất, nếu không
> `IRepository<T, Guid>` **không** được đăng ký (bài học `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:47–52`);
> (b) **một** hàm `ConfigureNotification(...)` dùng chung cho **cả** `NOT-001`+`NOT-002` (một module `Notification` — `D-197`), gọi **một lần** trong `OnModelCreating` (mẫu `ConfigureChannelHub()` —
> `eCommerceDbContext.cs:347`). **Mỗi bảng MỘT nguồn:** `NOT-002` sở hữu 6 bảng dưới đây; `NOT-001` sở hữu template/version/batch/queue — **không** bảng nào mô tả ở `§5` của cả hai tài liệu (`D-197`).

### 5.1 Bảng `NotificationMessages` — aggregate có trạng thái

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✓ | tenant | `IMultiTenant`; `null` = tin hạ tầng (**không** chứa dữ liệu khách) |
| `IdempotencyKey` | `varchar(256)` | ✗ | khoá chống trùng từ tầng trên | **UNIQUE** `(TenantId, IdempotencyKey)` — hàng rào cuối (ADR-03) |
| `RootMessageId` | `uuid` | ✓ | tin gốc khi đây là kênh dự phòng (A2) | tự tham chiếu |
| `ChannelKind` | `int` | ✗ | `Email=0`, `Sms=1`, `ZaloOa=2` | |
| `ProviderCode` | `varchar(32)` | ✗ | mã nhà cung cấp, ví dụ `smtp`, `esms`, `zalo-oa` | đối chiếu `INotificationChannelSender.ProviderCode` |
| `TemplateCode` | `varchar(64)` | ✗ | mã mẫu tin (**do `NOT-001` sở hữu**) | `NOT-002` **không** quản nội dung mẫu |
| `RecipientEncrypted` | `text` | ✓ | người nhận **đã mã hoá** at-rest (để job gửi/retry) | 🚨 **KHÔNG** plaintext; dùng `IStringEncryptionService` (khuôn `CredentialEncrypted`) |
| `RecipientMasked` | `varchar(320)` | ✓ | người nhận **đã che** để hiển thị/log | luật mask của module (email `k****@gmail.com`, SĐT `09xx***456`) |
| `RecipientHash` | `varchar(128)` | ✗ | HMAC-SHA256 hex **64** của người nhận (khuôn `D-204`) | tra opt-out **O(1)**, **0** plaintext; `CHECK ~ '^[0-9a-f]{64}$'` |
| `BodyMasked` | `text` | ✓ | nội dung **đã mask** để đối soát | 🚨 **CẤM** lưu OTP (BR-009); mask theo **luật của module** (tái dùng **luật** `PaymentTransaction.MaskSensitiveData`, **cấm** `using eCommerce.PaymentHub`) |
| `Status` | `int` | ✗ | theo §3 | **KHÔNG** sửa trực tiếp — chỉ qua hàm của entity |
| `AttemptCount` | `int` | ✗ | số lần đã thử | trần **3** (NFR-006) |
| `NextAttemptAtUtc` | `timestamptz` | ✓ | thời điểm được thử lại / khung giờ cho phép | quiet hours ghi vào đây (BR-012) |
| `ProviderMessageId` | `varchar(128)` | ✓ | mã tin do nhà cung cấp cấp | dùng cho callback; **UNIQUE** `(TenantId, ProviderCode, ProviderMessageId)` khi khác `null` |
| `SentAtUtc` / `DeliveredAtUtc` | `timestamptz` | ✓ | mốc thời gian | mốc của nhà cung cấp, không phải mốc ta nhận callback |
| `LastReason` | `varchar(512)` | ✓ | lý do lỗi/tạm hoãn gần nhất | **đã mask** |
| `Priority` | `int` | ✗ | `Low=0`, `Normal=1`, `High=2`, `Critical=3` | `Critical` **không** bị quiet hours chặn (BR-012) |

**Index & ràng buộc**

| Loại | Cột | Mục đích |
|---|---|---|
| **UNIQUE** | `(TenantId, IdempotencyKey)` **NULLS NOT DISTINCT** | 🚨 **idempotency biên vào #1** (BR-002); `TenantId` nullable ⇒ **bắt buộc** NND, nếu không tin hạ tầng (`TenantId=null`) lọt trùng (`D-203`) |
| **UNIQUE có filter** | `(TenantId, ProviderCode, ProviderMessageId)` **NULLS NOT DISTINCT** `WHERE ProviderMessageId IS NOT NULL` | chống 2 tin cùng trỏ về một mã nhà cung cấp; `TenantId` nullable ⇒ NND |
| INDEX | `(TenantId, Status, NextAttemptAtUtc)` | job lấy việc (**partial** `WHERE Status IN (ChoGui, ChoThuLai)` — dùng **hằng enum**, **không** chép số `0,3`; giảm kích thước index) |
| INDEX | `(TenantId, ChannelKind, CreationTime)` | tra cứu vận hành |
| INDEX | `(TenantId, ChannelKind, RecipientHash)` | tra cứu theo người nhận (đối soát, opt-out) |
| CHECK | `AttemptCount >= 0 AND AttemptCount <= 3` | ngăn retry vô hạn ở **tầng DB**, không chỉ ở code (NFR-006) |
| CHECK | `ChannelKind IN (0,1,2)` | |
| CHECK | `RecipientHash ~ '^[0-9a-f]{64}$'` | cùng chuẩn `D-204`, chặn plaintext lọt vào |

> 🚨 **Mask nội dung là hàm THUẦN trong module (`D-201`), MỘT cửa ghi:** bộ luật tối thiểu — khoá nhạy cảm (`otp`/`pin`/`token`/`secret`/`password`/`authorization`/`signature`/`card_number`/`cvv`/`cvc`) ⇒ `***`; PAN 12–19 số ⇒ giữ **4 số cuối**; email ⇒ `k****@gmail.com`; SĐT ⇒ giữ đầu + 4 cuối (`09xx***456`); link chứa `token`/`sig`/`code`/`key` ⇒ che **giá trị query**; **không chắc** ⇒ **fail-closed** `NULL` + ghi chú. Tin chứa OTP ⇒ `BodyMasked = NULL` + ghi chú *"nội dung không lưu (chứa OTP)"*. Cắt `Truncate(..., 8000)` (khuôn `PaymentTransaction.cs:339`). **Tái dùng LUẬT** của `PaymentTransaction.MaskSensitiveData` (`modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:313-337`), **cấm** `using eCommerce.PaymentHub` (muốn dùng chung ⇒ tách `eCommerce.Kernel` bằng dòng `D-xx` mới). `RecipientMasked`/`BodyMasked` **chỉ** đọc ở màn "Chi tiết tin" + permission; **cấm** nội dung trong DTO danh sách/ETO/log/metric (`XC-005` BR-005/D6).

### 5.2 Bảng `NotificationDeliveryEvents` — **sổ append-only**

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính |
| `TenantId` | `uuid` | ✓ | tenant |
| `MessageId` | `uuid` | ✗ | **FK** → `NotificationMessages.Id` |
| `EventType` | `int` | ✗ | `Queued · Sending · Sent · RetryScheduled · Retrying · Delivered · Undeliverable · AckExpired · FailedPermanent · MovedToDlq · Replayed · Cancelled · IgnoredOutOfOrder` |
| `FromStatus` / `ToStatus` | `int` | ✗ | mốc chuyển trạng thái (**bắt buộc** — để chứng minh guard đã chạy) |
| `Reason` | `varchar(512)` | ✓ | đã mask |
| `CorrelationId` | `varchar(64)` | ✗ | theo `XC-005` BR-002 |
| `ActorUserId` | `uuid` | ✓ | có khi do người thao tác (phát lại DLQ) |
| `OccurredAtUtc` | `timestamptz` | ✗ | |

> 🚨 **`NotificationDeliveryEvents` là APPEND-ONLY** (BR-005) — kế thừa nguyên tắc `StockLedgerEntry`
> (*"APPEND-ONLY. Không bao giờ UPDATE/DELETE"* — `src/eCommerce.Domain/Entities/Inventory/StockLedger.cs:80–84`).
> **Cấm** `UPDATE`/`DELETE`; sửa sai phải ghi event mới.

### 5.3 Bảng `NotificationInbox` — chống trùng callback (biên vào #2)

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính |
| `TenantId` | `uuid` | ✓ | tenant |
| `ProviderCode` | `varchar(32)` | ✗ | nhà cung cấp gọi vào |
| `ProviderMessageId` | `varchar(128)` | ✗ | mã tin phía nhà cung cấp |
| `EventType` | `int` | ✗ | loại callback |
| `ProviderEventId` | `varchar(128)` | ✓ | id sự kiện do nhà cung cấp cấp (nếu có) — **chỉ để điều tra** |
| `DedupKey` | `varchar(160)` | ✗ | khoá chống trùng sinh ở **một cửa** `INotificationCallbackParser` (`D-203`) |
| `DedupKeyVersion` | `int` | ✗ | phiên bản thuật toán khoá, `=1` (khuôn `CanonicalJsonHasher.PhienBanHienHanh`) |
| `SignatureValid` | `bool` | ✗ | kết quả xác thực — **lưu lại để điều tra** |
| `ProcessStatus` | `int` | ✗ | `Processed · IgnoredDuplicate · Rejected · IgnoredOutOfOrder` |
| `RawPayloadMasked` | `text` | ✓ | payload **đã mask**, cắt độ dài |
| `ReceivedAtUtc` | `timestamptz` | ✗ | |

| Loại ràng buộc | Cột | Mục đích |
|---|---|---|
| **UNIQUE** `UX_NotificationInbox_Dedup` | `(TenantId, ProviderCode, ProviderMessageId, EventType, DedupKey)` **NULLS NOT DISTINCT** | 🚨 **hàng rào chống trùng DUY NHẤT** (BR-019, `D-203`). `TenantId` nullable ⇒ **bắt buộc** `NULLS NOT DISTINCT`, nếu không hàng rào **mất tác dụng im lặng** |
| INDEX | `(TenantId, ReceivedAtUtc)` | điều tra & đối soát |

> 🔑 **Sinh `DedupKey` (một cửa, `D-203`):** parser trả id sự kiện ≤ 128 ⇒ `"e:" + id`; không có id ⇒ `"h:" + CanonicalJsonHasher.Bam(JSON chuẩn hoá nội dung **nghiệp vụ**, **bỏ** trường vận chuyển/không ổn định — chữ ký, timestamp/nonce, header retry, correlation)`; id > 128 ⇒ `"h:" + sha256(giá trị nguyên)` — **cấm cắt chuỗi** (sinh khoá trùng giả). Băm **payload thô TRƯỚC khi mask** (`RawPayloadMasked` chỉ để điều tra; đổi luật mask **không** được làm hỏng khoá). Tái dùng `CanonicalJsonHasher` (`modules/Messaging/eCommerce.Messaging.Domain/Idempotency/CanonicalJsonHasher.cs:21-100`). Tuỳ chọn ABP Setting `Notification.Inbox.DedupFields:<providerCode>` (trống = toàn nội dung nghiệp vụ; trường khai báo mà **thiếu** ⇒ **NÉM**, không lặng lẽ bỏ qua). **Fail-closed:** thiếu `ProviderMessageId`/payload không parse ⇒ `400` + **0** dòng inbox; **cấm** rơi về `Guid.NewGuid()`.

### 5.4 Bảng `NotificationCostLedger` — **sổ chi phí append-only**

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✗ | tenant | |
| `MessageId` | `uuid` | ✗ | **FK** → `NotificationMessages.Id` | |
| `ChannelKind` / `ProviderCode` | `int` / `varchar(32)` | ✗ | kênh & nhà cung cấp | |
| `UnitCost` | `numeric(18,4)` | ✗ | đơn giá **một** tin | 🚨 `decimal` — **cấm** `double`/`float` (`XC-001` BR-008) |
| `Quantity` | `int` | ✗ | số segment tin (đếm nguyên) — **không** phải số lượng hàng hoá (`D-31`) | |
| `TotalCost` | `numeric(18,4)` | ✗ | `UnitCost × Quantity` | 🚨 so khớp **theo minor units**, **cấm** `==` trên `decimal` (`XC-001` BR-003) |
| `Currency` | `varchar(8)` | ✗ | `VND` | `XC-001` BR-009 |
| `OccurredAtUtc` | `timestamptz` | ✗ | kỳ ghi nhận = đổi sang **ngày kinh doanh UTC+7** (`D-193`/`D-47`) | |

**Index:** `INDEX (TenantId, OccurredAtUtc)`; **append-only** (không có đường `UPDATE`/`DELETE`).

> 💰 **Đơn giá/segment chờ NCC (`D-200`):** `UnitCost` lấy từ `NotificationChannelAccounts.Settings` của dòng kênh (đơn giá/segment là **dữ liệu hợp đồng**). NCC **chưa chọn** ⇒ **KHÔNG suy đoán `UnitCost`** (`NOT NULL` ⇒ không ghi sổ khi thiếu giá); đơn giá/segment là việc **chủ dự án/NCC** (`R3-6`).

> 🔗 **Liên kết `ACC-001` + `XC-001` + `XC-006`:** sổ này là **nguồn dữ liệu** cho `ACC-001` xuất liệu kế toán
> (`docs/implementation-plan.md:441`). Mọi số tiền trong đó **phải** theo `XC-001` (decimal, làm tròn,
> so theo minor units). Việc **tổng hợp** cho kế toán **không** được query trực tiếp bảng này mỗi lần
> mở báo cáo — phải qua **read-model của `REP-001`** (§8.3).

> 📌 **Ngữ nghĩa (`D-193`, Q9 đã chốt):** dòng xuất là **chi phí bán hàng phát sinh trong kỳ** (chi phí kỳ),
> **KHÔNG** vào giá vốn/`CogsAmount`/`GrossMargin*`; sổ giữ **đúng 8 cột nghiệp vụ** ở trên, **KHÔNG** thêm
> `OrderId`/`OrderLineId`; **cấm** join sổ chi phí với bảng đơn (§8.3).

### 5.5 Bảng `NotificationChannelAccounts` — credential & cấu hình kênh

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính | |
| `TenantId` | `uuid` | ✗ | tenant sở hữu credential | **cách ly tenant** (BR-017) |
| `ChannelKind` | `int` | ✗ | kênh | |
| `ProviderCode` | `varchar(32)` | ✗ | nhà cung cấp | |
| `SenderIdentity` | `varchar(256)` | ✗ | email gửi / brandname SMS / `oa_id` Zalo | **hiển thị được** |
| `CredentialEncrypted` | `text` | ✓ | API key / mật khẩu SMTP / OA token — **đã mã hoá** | 🚨 dùng `IStringEncryptionService` (tiền lệ `ChannelShop.AccessTokenEncrypted`, `modules/ChannelHub/eCommerce.ChannelHub.Domain/Entities/ChannelEntities.cs:52–59`) |
| `RefreshCredentialEncrypted` | `text` | ✓ | refresh token (Zalo OA có rotate) | **chỉ** ghi trong khoá (BR-010) |
| `WebhookSecretEncrypted` | `text` | ✓ | secret xác thực callback (HMAC key / shared secret) | 🚨 **KHÔNG** để trong `Settings`; mã hoá bằng `IStringEncryptionService` (`D-202`) |
| `CredentialExpiresAtUtc` | `timestamptz` | ✓ | hạn của credential | |
| `DailyQuota` / `UsedToday` | `int` | ✗ | hạn mức & đã dùng trong ngày | đặt lại theo ngày; `UsedToday` cập nhật trong khoá quota |
| `QuietHoursFrom` / `To` | `time` | ✓ | khung giờ **được** gửi (giờ VN) | |
| `IsEnabled` | `bool` | ✗ | bật/tắt kênh | `ZaloOa` chỉ `true` khi có `unitCostPerMessage` (fail-closed **lúc bật**, `D-202`) |
| `OptOutLastSyncedAtUtc` | `timestamptz` | ✓ | lần đồng bộ danh sách từ chối NCC gần nhất | `D-207`; dùng phát hiện danh sách **cũ** (`staleAfterDays`) |
| `Settings` | `jsonb` | ✓ | tham số riêng nhà cung cấp (endpoint, template id ZNS…) | **không** chứa secret |

> 🔧 **Khoá `Settings` (jsonb, DỮ LIỆU — không secret; `D-200`/`D-202`/`D-205`/`D-206`/`D-207`):** `providerCode` · `apiBaseUrl` · `senderIdentity` · `dailyQuota` · `quietHoursFrom/To` · `unitCostPerMessage` + `currency` · **Zalo** `webhookAuthMode ∈ {none, hmac_sha256, ip_allowlist, shared_secret_url}` (mặc định `none`) · `ipAllowlist` · `znsTemplateCode` · `sentAckTtlMinutes` · **cảnh báo** `opsAlertRecipients` (trần 20) · `opsAlertCriticalChannels` (mặc định `["Email"]`) · **opt-out** `optOutSync = {mode: off|file|api (mặc định off), intervalHours (24), staleAfterDays (7), required (false)}`. Mọi phụ thuộc hãng vào **cấu hình**, **0** hằng số NCC trong mã, **0** NuGet NCC (`D-200`). Secret (HMAC key/shared secret) **không** vào `Settings` ⇒ cột `WebhookSecretEncrypted`.

**Index & ràng buộc**

| Loại | Cột | Mục đích |
|---|---|---|
| **UNIQUE** | `(TenantId, ChannelKind, ProviderCode, SenderIdentity)` | một tenant không khai trùng cùng một người gửi |
| CHECK | `DailyQuota >= 0 AND UsedToday >= 0 AND UsedToday <= DailyQuota` | ngăn vượt hạn mức ở tầng DB |

### 5.6 Bảng `NotificationOptOuts` — danh sách từ chối

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✗ | khoá chính |
| `TenantId` | `uuid` | ✗ | tenant |
| `ChannelKind` | `int` | ✗ | opt-out theo **từng kênh** (khách có thể chặn SMS nhưng vẫn nhận email) |
| `RecipientHash` | `varchar(128)` | ✗ | **băm một chiều** HMAC-SHA256 hex thường **64** ký tự (khoá `Notification:OptOut:HashKey`, `"v1|" + ChuẩnHoáNgườiNhận`) | `D-204`; `CHECK ~ '^[0-9a-f]{64}$'` |
| `HashVersion` | `int` | ✗ | phiên bản thuật toán/khoá băm, `=1` | `D-204`; đổi khoá ⇒ **nạp lại** + tăng version |
| `Source` | `int` | ✗ | `CustomerRequest=0` · `Manual=1` · `ProviderReport=2` · `ProviderSuppressionImport=3` | `D-207`; enum **append** (`D-17`) |
| `SourceRef` | `varchar(128)` | ✓ | mã lô/file/`ProviderMessageId` | `D-207`; **không** PII |
| `Reason` | `varchar(256)` | ✓ | lý do (khách yêu cầu / nhà cung cấp báo) | |
| `OccurredAtUtc` | `timestamptz` | ✗ | | |

| Loại | Cột | Mục đích |
|---|---|---|
| **UNIQUE** | `(TenantId, ChannelKind, RecipientHash)` **NULLS NOT DISTINCT** | tra cứu **O(1)** trước mỗi lần gửi (NFR-010); một cửa `CreateOptOutAsync`, trùng ⇒ trả dòng cũ |
| CHECK | `Source IN (0,1,2,3)` | chặn `Source` ngoài dải (`D-207`) |
| INDEX | `(TenantId, ChannelKind)` | danh sách cho vận hành |

> 🚨 **Băm một chiều (`D-204`) — KHÔNG mã hoá, 0 plaintext:** `RecipientHash` = hex thường **64** ký tự `HMAC-SHA256(khoá, "v1|" + ChuẩnHoáNgườiNhận(recipient))`; chuẩn hoá ở **một cửa** `NotificationRecipientHash.Tinh` (hàm thuần): email = `Normalize(FormC).Trim().ToLowerInvariant()`; SĐT = bỏ khoảng trắng/`-`/`.`/ngoặc, `0…`→`+84…`; Zalo `user_id` = `Trim()` nguyên văn. **Cấm** chuẩn hoá ở chỗ khác (lệch ⇒ opt-out **trượt im lặng** ⇒ gửi cho người đã từ chối). `GET /api/notification/opt-outs` **chỉ** trả `RecipientHash` (kèm **8 ký tự đầu** làm dấu nhận dạng) + `ChannelKind`/`Reason`/`OccurredAtUtc` + `Source`; thêm `POST /api/notification/opt-outs/check` (băm server-side, **không** query/không log). Khoá HMAC đọc từ secret ngoài repo; **thiếu khoá production ⇒ fail-closed NÉM** (0 tin gửi, 0 dòng opt-out) — tạo khoá là việc `R3-6`.

---

## 6. Hợp đồng API

### 6.1 Application Service (thiết kế mới — chưa có trong code, kế hoạch GĐ3)

```csharp
namespace eCommerce.Notification;

/// <summary>Loại kênh gửi — **enum dùng chung** `NOT-001` + `NOT-002` (`D-197`, **không** tạo enum thứ hai). Giá trị lưu dạng int trong DB.</summary>
public enum ChannelKind { Email = 0, Sms = 1, ZaloOa = 2 }

/// <summary>Phân loại lỗi — quyết định thử lại hay không. Đây là điểm dễ sai nhất khi tích hợp.</summary>
public enum NotificationFailureKind
{
    None = 0,             // thành công
    Transient = 1,        // timeout, 5xx, rate limit ⇒ THỬ LẠI
    Permanent = 2,        // định danh sai, template bị từ chối ⇒ KHÔNG thử lại
    QuotaExceeded = 3,    // hạn mức ⇒ thử lại ở cửa sổ sau
    Unauthenticated = 4   // credential sai/hết hạn ⇒ thử lại + CẢNH BÁO VẬN HÀNH
}

/// <summary>
/// HỢP ĐỒNG DUY NHẤT của lớp kênh gửi (ADR-01).
/// Mọi nhà cung cấp phải hiện thực interface này; app service/job KHÔNG được gọi SDK trực tiếp (BR-001).
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>Mã nhà cung cấp — khớp <c>ProviderCode</c> trong DB. Ví dụ: smtp · esms · zalo-oa.</summary>
    string ProviderCode { get; }

    ChannelKind ChannelKind { get; }

    Task<SendOutcome> SendAsync(SendCommand command, CancellationToken cancellationToken = default);

    /// <summary>Làm mới credential (Zalo OA có rotate). BẮT BUỘC gọi trong phạm vi distributed lock (BR-010).</summary>
    Task<RefreshOutcome> RefreshCredentialAsync(Guid channelAccountId, CancellationToken cancellationToken = default);
}

public sealed record SendCommand(
    Guid MessageId,
    Guid? TenantId,
    string Recipient,                 // email hoặc số điện thoại đã chuẩn hoá hoặc Zalo user_id
    string TemplateCode,
    IReadOnlyDictionary<string, string> Data,
    string Priority,                  // Low · Normal · High · Critical
    string CorrelationId);

public sealed record SendOutcome(
    bool Accepted,
    string? ProviderMessageId,
    NotificationFailureKind FailureKind,
    string? Reason,
    bool RecipientRefused = false);   // D-207: NCC báo "người nhận từ chối/bị chặn" ⇒ core ghi opt-out Source=ProviderReport cùng UoW, KHÔNG retry

public interface INotificationSender
{
    /// <summary>
    /// Gửi (hoặc trả kết quả cũ nếu đã gửi cùng <paramref name="request"/>).
    /// 🚨 IdempotencyKey PHẢI ổn định — CẤM sinh Guid mới mỗi lần gọi (BR-002).
    /// </summary>
    Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xử lý callback/webhook của nhà cung cấp — chống trùng bằng unique index (BR-003).</summary>
    Task<CallbackResult> HandleCallbackAsync(
        string providerCode,
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

public sealed record SendRequest(
    Guid? TenantId,
    string IdempotencyKey,
    ChannelKind ChannelKind,
    string Recipient,
    string TemplateCode,
    IReadOnlyDictionary<string, string> Data,
    string Priority,
    Guid? RootMessageId = null);

public sealed record SendResult(
    Guid MessageId,
    bool AlreadySent,
    string Status,
    string? ProviderMessageId);
```

**Hợp đồng phụ trợ (kế hoạch GĐ3):** `INotificationCallbackParser` (Application.Contracts, đăng ký theo registry như ADR-01) trả `(ProviderMessageId, EventType, ProviderEventId?, DedupKey)` — **một cửa** sinh `DedupKey` (`D-203`). `IOptOutSourceAdapter` (Application.Contracts, `ProviderCode` + `FetchAsync`) cho đường KÉO danh sách từ chối (`D-207`; mặc định **0** adapter ⇒ tính năng **tắt**). Thứ tự viết adapter theo `D-200`: **email tổng quát (SMTP) trước** (giao thức, không phải hãng); adapter **HTTP API/SMS/Zalo OA** chỉ viết **sau khi `R3-6` đóng**.

### 6.2 REST endpoint

| Method | Route | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/notification/messages` | `eCommerce.Notification.Messages.View` | tra cứu tin (lọc theo kênh/trạng thái/thời gian) |
| `GET` | `/api/notification/messages/{id}/events` | `eCommerce.Notification.Messages.View` | **nhật ký append-only** của một tin |
| `POST` | `/api/notification/messages/{id}/resend` | `eCommerce.Notification.Messages.Resend` | gửi lại **thủ công** (sinh khoá mới có hậu tố `:resend-{n}` — **cấm** dùng lại khoá cũ) |
| `POST` | `/api/notification/dlq/{id}/replay` | `eCommerce.Notification.Dlq.Replay` | phát lại từ DLQ (khoá `notification:dlq-replay:{tenantId}`) |
| `GET` | `/api/notification/channels` | `eCommerce.Notification.Channels.View` | danh sách kênh + hạn mức (⚠️ **không** trả credential) |
| `PUT` | `/api/notification/channels/{id}` | `eCommerce.Notification.Channels.Manage` | bật/tắt, hạn mức, quiet hours, credential (ghi vào trường **mã hoá**) |
| `POST` | `/api/notification/channels/{id}/test-send` | `eCommerce.Notification.Channels.Manage` | gửi tin thử để kiểm tra credential |
| `GET` | `/api/notification/opt-outs` | `eCommerce.Notification.OptOut.View` | danh sách từ chối — **chỉ** trả `RecipientHash` (kèm 8 ký tự đầu) + `ChannelKind`/`Reason`/`OccurredAtUtc`/`Source`, **0** plaintext |
| `POST` | `/api/notification/opt-outs` | `eCommerce.Notification.OptOut.Manage` | ghi nhận từ chối (một cửa `CreateOptOutAsync`) |
| `POST` | `/api/notification/opt-outs/check` | `eCommerce.Notification.OptOut.View` | tra "người nhận này có từ chối không" — người nhận trong **body**, băm server-side, **không** query/không log plaintext |
| `POST` | `/api/notification/callbacks/{providerCode}` | **chờ người duyệt + rate limit** (`D-202`) | **webhook nhận callback** — GĐ3 **KHÔNG mở**; test bằng `fake` payload gọi thẳng app service (§7.2) |

> 🔐 **QUAN TRỌNG — bài học đã trả giá ở Phase 0:** ABP **KHÔNG** tự bảo vệ application service. Đã từng có
> `GetShopsAsync` trở thành API **công khai** vì chỉ đặt `[Authorize]` ở vài method
> (`src/eCommerce.Application/Channels/ChannelAppService.cs:21–28`). ⇒ `NotificationAppService` **BẮT BUỘC**
> `[Authorize]` **ở CẤP CLASS**; permission nào chưa định nghĩa thì **luôn** bị từ chối
> (`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:8–11`).
> Ngoại lệ **duy nhất** là endpoint callback, và nó **phải** tự xác thực chữ ký (§7.2).

**Mã lỗi nghiệp vụ** — **một** `NotificationErrorCodes.cs` dùng chung `NOT-001`+`NOT-002` (`modules/Notification/.../NotificationErrorCodes.cs`
— **chưa có**, kế hoạch GĐ3; theo đúng khuôn `ChannelHubErrorCodes` `modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:36–42`
và `TaxErrorCodes` `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:10–35`). **Dải mã (F9, `D-197` + `D-199`):** `NOT-002` dùng
**`0001–0009`**; `NOT-001` dùng **`0010–0020`** (`0020` = `BodyFormatNotSupported`); **append-only**, `NOT-002` **không** chiếm `0010+`.

| Mã | HTTP | Khi nào | Thông điệp |
|---|---|---|---|
| `eCommerce:Notification:0001` | `409` | Trùng `IdempotencyKey` khi đang ở chế độ **cấm** trùng | `Yêu cầu gửi này đã tồn tại.` |
| `eCommerce:Notification:0002` | `400` | Người nhận sai định dạng / lỗi vĩnh viễn | `Người nhận không hợp lệ: {lý do}.` |
| `eCommerce:Notification:0003` | `503` | Thiếu credential hoặc credential hết hạn | `Kênh gửi {kênh} chưa sẵn sàng (credential).` |
| `eCommerce:Notification:0004` | `429` | Vượt hạn mức gửi của kênh | `Đã vượt hạn mức gửi của kênh {kênh}.` |
| `eCommerce:Notification:0005` | `409` | Tin đã vào DLQ / cố phát lại sai trạng thái | `Tin đang ở DLQ; cần phát lại có kiểm soát.` |
| `eCommerce:Notification:0006` | `502` | Nhà cung cấp nhận nhưng **không** trả `ProviderMessageId` | `Nhà cung cấp không trả mã tin; không theo dõi được.` |
| `eCommerce:Notification:0007` | `409` | Chuyển trạng thái không hợp lệ (kể cả lùi trạng thái) | `Không thể chuyển tin từ {từ} sang {sang}.` |
| `eCommerce:Notification:0008` | `403` | Callback không xác thực được chữ ký | `Callback không hợp lệ.` |
| `eCommerce:Notification:0009` | `409` | Người nhận đã opt-out | `Người nhận đã từ chối nhận tin qua kênh {kênh}.` |

> 📌 **Không** tự đặt mã cho "không giành được khoá" — dùng **nguyên** `eCommerce:Lock:0001` của `XC-003`
> (hằng chuẩn `shared/eCommerce.Kernel/Locking/LockErrorCodes.cs:20`; `docs/features/cross-cutting/XC-003-khoa-phan-tan-quy-uoc.md` §6.2).
>
> 📌 **`BodyFormat`/`Subject` là của `NOT-001`, không phải `NOT-002`:** GĐ3 chỉ `BodyFormat = Text (=0)`, `Html` **NÉM** `0020`,
> Subject loại CR/LF/NUL, **0 sanitizer/0 NuGet** (`D-199`). `NOT-002` chỉ vận chuyển, **không** định nghĩa lại định dạng/escape mẫu.

---

## 7. Sự kiện, tích hợp ngoài & idempotency

### 7.1 Sự kiện phát ra (ETO)

| ETO | Phát khi | Trường dữ liệu | Ai tiêu thụ | Idempotent? |
|---|---|---|---|---|
| `NotificationQueuedEto` | tạo tin `ChoGui` | `TenantId`, `MessageId`, `ChannelKind`, `IdempotencyKey`, `CorrelationId` | Job gửi | ✅ theo `MessageId` |
| `NotificationSentEto` | `DaGui` | + `ProviderCode`, `ProviderMessageId`, `SentAtUtc` | `NOT-001` (ghi vết), `REP-*` | ✅ |
| `NotificationDeliveredEto` | `DaPhat` | + `DeliveredAtUtc` | `NOT-001`, `REP-*` | ✅ theo `(MessageId, EventType)` |
| `NotificationFailedPermanentEto` | `ThatBai` / `KhongPhatDuoc` | + `Reason`, `ErrorCode` | Vận hành, `REP-*` | ✅ |
| `NotificationMovedToDlqEto` | vào `DLQ` | + `AttemptCount`, `LastReason` | Vận hành (**cảnh báo**) | ✅ |

> 📌 **Cơ chế:** outbox của ABP + relay, theo `XC-002` — **đã triển khai** (`AbpEventOutbox`/`AbpEventInbox`, `DeadLetterAppService`, `IdempotencyGuard`; xem §0.1).
> 📌 **F8 (`D-197`):** ETO hàng đợi của `NOT-002` là `NotificationQueuedEto`, **khác tên** `NotificationEnqueuedEto` của `NOT-001` — **không** tồn tại hai kiểu `*Eto` cùng tên giữa hai tài liệu (test kiến trúc quét `*Eto`).
> 📌 Mọi ETO **phải** mang `CorrelationId` (`XC-005` BR-003). `ChannelOrderPulledEto` **đã có** trường này
> (`modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:72`, thêm ở `e7c56f6`);
> chỉ `ChannelShopTokenRefreshedEto` còn thiếu (`:117–125`) — `NOT-002` **không** lặp lại (xem §15.2 F4).

### 7.2 Sự kiện / webhook nhận vào

| Nguồn | Loại | Xác thực | Chống trùng bằng | Nếu xử lý lỗi? | Thứ tự đến |
|---|---|---|---|---|---|
| **Nhà cung cấp email** (bounce/complaint webhook) | webhook | chữ ký **nếu có**; nếu không ⇒ **IP allowlist** + secret trong URL | `UX_NotificationInbox_Dedup` UNIQUE `(TenantId, ProviderCode, ProviderMessageId, EventType, DedupKey)` **NULLS NOT DISTINCT** | trả `5xx` để nhà cung cấp thử lại; `4xx` nếu payload sai | ❌ có thể sai thứ tự ⇒ xử lý bằng `IgnoredOutOfOrder` (E7) |
| **Nhà cung cấp SMS** (delivery report) | webhook / callback | ⚠️ **chưa kiểm chứng** — mỗi nhà cung cấp một kiểu (một số chỉ dùng IP allowlist) | như trên | như trên | ❌ có thể sai thứ tự |
| **Zalo OA** (sự kiện tin nhắn/trạng thái) | webhook | ⚠️ **chưa kiểm chứng** 5 thông số (`D-202`) — cấu hình `webhookAuthMode ∈ {none, hmac_sha256, ip_allowlist, shared_secret_url}` (mặc định `none`); secret ở `WebhookSecretEncrypted` | như trên (`DedupKey`, `D-203`) | như trên; **`webhookAuthMode = none`** ⇒ `SignatureValid=false` + `ProcessStatus=Rejected` + metric BR-018, **không** đổi trạng thái tin | ❌ |

> 🚪 **GĐ3 KHÔNG mở `POST /api/notification/callbacks/{providerCode}`** (`D-202`): endpoint `[AllowAnonymous]` phải **người duyệt từng endpoint + rate limit** (`P10`) ⇒ GĐ3 chỉ test bằng `fake` payload gọi **thẳng app service** (kiểm parser sinh `DedupKey`, chống trùng) — không lộ endpoint ẩn danh.

> 🚨 **Nguyên tắc bắt buộc khi nhà cung cấp KHÔNG có chữ ký HMAC** (kế thừa cảnh báo của `SHP-002` —
> `docs/implementation-plan.md:425` ghi rõ *"không có HMAC ở GHTK"*):
> 1. **Không** tin payload. Chỉ **đọc** `ProviderMessageId` + `EventType`, **không** nhận nội dung nghiệp vụ từ callback.
> 2. Bù bằng **IP allowlist** + **secret trong URL** + **đối soát chủ động** (§8.3).
> 3. Nếu callback chứa **số tiền/trạng thái đơn** ⇒ **cấm** dùng trực tiếp để gửi tin cho khách (BR-014).
> 4. Mọi callback **không** xác thực được ⇒ `ProcessStatus=Rejected` + metric + log `Warning` (BR-018) — **không** nuốt im lặng.

### 7.3 Gọi API ra ngoài

| Nhà cung cấp | Endpoint (base URL) | Auth | Timeout | Retry | Nếu thất bại |
|---|---|---|---|---|---|
| SMTP/API email (**chưa chọn NCC** — §15 Q1) | cấu hình trong `NotificationChannelAccounts.Settings` | mật khẩu SMTP / API key (mã hoá) | **10 s** | 3 lần, backoff 1/5/25 s | `ChoThuLai` → `DLQ` |
| SMS brandname (**chưa chọn NCC** — §15 Q1) | cấu hình | API key + brandname | **10 s** | 3 lần, backoff 1/5/25 s | `ChoThuLai` → `DLQ`; `QuotaExceeded` ⇒ thử lại cửa sổ sau |
| Zalo OA (**chưa kiểm chứng 5 thông số** — §15 Q3, `D-202`) | `Settings.apiBaseUrl` | OA access token (+ refresh token) | **10 s** | 3 lần, backoff 1/5/25 s | như trên; thiếu `znsTemplateCode` ⇒ **NÉM** `0003` **trước** gọi mạng, **không** hạ cấp sang CSKH; `Unauthenticated` ⇒ refresh **trong khoá** rồi thử lại **một** lần |

> ⚠️ **Cấm hard-code endpoint** vào code — endpoint đặt trong `NotificationChannelAccounts.Settings`
> (quy tắc 8 của `docs/design-doc-guidelines.md` §5; tiền lệ `ShopeeOptions.BaseUrl`
> `modules/ChannelHub/eCommerce.ChannelHub.Application/ChannelHubOptions.cs:39–44`).

> 🧩 **Trung lập NCC (`D-200`) — làm được ngay, không cần chọn hãng:** (i) hợp đồng `INotificationChannelSender` + `INotificationChannelFactory`/registry (khuôn ADR-01); mọi thứ phụ thuộc hãng vào `NotificationChannelAccounts.Settings` + ABP Setting (`ProviderCode`, base URL, `SenderIdentity`, credential mã hoá, QuietHours, DailyQuota, đơn giá/segment) — **0** hằng số NCC trong mã, **0** NuGet NCC; (ii) `fake` sender cho **mọi** test, **0** kết nối mạng; (iii) **fail-closed**: kênh tắt/thiếu credential ⇒ `eCommerce:Notification:0003` (`503`), **không** gọi mạng, **không** đánh dấu `DaGui`; thiếu đơn giá ⇒ **không** suy đoán `UnitCost`; (iv) adapter email **tổng quát (SMTP)** viết **trước**, adapter HTTP API/SMS/Zalo OA **sau** khi `R3-6` đóng.

**Idempotency key** (⚠️ **phải ổn định qua retry**):

| Đối tượng | Cách sinh | ⚠️ Lưu ý |
|---|---|---|
| Tin từ `NOT-001` | do `NOT-001` cấp, ví dụ `order:{orderId}:shipped:sms` | 🚨 **CẤM** `Guid.NewGuid()` mỗi lần gọi — sẽ **gửi trùng** mà không ai phát hiện |
| Kênh dự phòng (A2) | `{khoá gốc}:fallback-sms` | cố ý **khác** khoá để không bị chặn nhầm; liên kết qua `RootMessageId` |
| Gửi lại **thủ công** | `{khoá gốc}:resend-{n}` | **cấm** dùng lại khoá gốc (sẽ bị chặn) |
| Callback nhận vào | `DedupKey` sinh ở **một cửa** `INotificationCallbackParser` (có id ⇒ `"e:"+id`; không ⇒ `"h:"+sha256(JSON nghiệp vụ)`) + UNIQUE `UX_NotificationInbox_Dedup` (`D-203`, §5.3) | **cấm** `Guid.NewGuid()`, **cấm** cắt chuỗi, băm payload thô **trước** mask |

---

## 8. Bất biến & xử lý đồng thời

### 8.1 Bất biến

| # | Bất biến | Cơ chế bảo vệ |
|---|---|---|
| 1 | Cùng `(TenantId, IdempotencyKey)` chỉ có **tối đa một** `NotificationMessage` | **UNIQUE index** `(TenantId, IdempotencyKey)` **NULLS NOT DISTINCT** ở DB (không chỉ kiểm ở code; `TenantId` nullable) |
| 2 | Cùng một callback **không** làm chuyển trạng thái **hai** lần | **UNIQUE** `UX_NotificationInbox_Dedup` (`DedupKey`, `NULLS NOT DISTINCT`) + xử lý trong 1 transaction (`D-203`) |
| 3 | **Không** có chuyển trạng thái **lùi**; trạng thái kết thúc là **hấp thụ** | Guard **NÉM** `eCommerce:Notification:0007`; test từng nhánh §3 |
| 4 | `NotificationDeliveryEvents` **append-only** | Không có API sửa/xoá + test chặn `UPDATE`/`DELETE` |
| 5 | **Không** có bản ghi nào chứa OTP | Không có cột/nhánh ghi OTP + test quét |
| 6 | `AttemptCount` **không bao giờ** > 3 | CHECK ở DB + guard ở entity |
| 7 | Nhãn metric **không** chứa `email`/`phone` | `eCommerceMetricTags.Forbidden` (`eCommerceMetrics.cs:218–223`) + test `TC-07` (`MetricsTests.cs:107+`) |
| 8 | **Không** tồn tại `double`/`float` trong biểu thức tiền của module | Test reflection (theo `XC-001` NFR-004) |
| 9 | Tenant B **không** đọc/ghi được credential/nội dung của tenant A | `IMultiTenant` + filter của ABP + integration test 2 tenant |
| 10 | Credential **không** xuất hiện trong bất kỳ DTO/response nào | Không map vào DTO (tiền lệ `ChannelAppService.ToDto` bỏ token — `ChannelAppService.cs:189–209`) + test |

### 8.2 Khoá phân tán dùng trong feature

| Tên khoá (**chuỗi do module sinh**) | Khoá thật trong Redis | Phạm vi | Vì sao cần |
|---|---|---|---|
| `notification:zalo-oa:{oaId}:token-refresh` | `ecommerce:notification:zalo-oa:{oaId}:token-refresh` | theo OA | refresh token **có rotate** ⇒ refresh song song làm **mất token** (tiền lệ `ChannelTokenManager.cs:67–91`) |
| `notification:provider:{providerCode}:quota:{tenantId}` | `ecommerce:notification:provider:{providerCode}:quota:{tenantId}` | theo tenant + NCC | cập nhật `UsedToday` và giữ hạn mức nhà cung cấp — tránh vượt quota |
| `notification:channel-account:{accountId}` | `ecommerce:notification:channel-account:{accountId}` | theo tài khoản kênh | 2 vận hành viên cùng sửa credential cùng lúc |
| `notification:dlq-replay:{tenantId}` | `ecommerce:notification:dlq-replay:{tenantId}` | theo tenant | chỉ **một** tiến trình phát lại DLQ (A4) |
| `notification:opt-out-sync:{tenantId}:{channelKind}` | `ecommerce:notification:opt-out-sync:{tenantId}:{channelKind}` | theo tenant + kênh | job KÉO danh sách từ chối NCC (`D-207`; **không** tiền tố `ecommerce:` — F2, `XC-003`) |

> 📌 **Một nguồn khoá:** 5 tên khoá trên khai trong **một** `NotificationLockKeys` (dùng chung `NOT-001`+`NOT-002`, `D-197`) — **không** rải chuỗi trong code gọi.

> 🚨 **BẪY TIỀN TỐ ĐÔI — đã có lỗi thật trong repo, `NOT-002` KHÔNG được lặp lại:**
> ABP ghép khoá thật bằng `KeyPrefix + tên khoá` **KHÔNG có dấu phân cách**
> (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:222,237`), và `KeyPrefix` đã là `"ecommerce:"`.
> ⇒ **Tên khoá do module sinh TUYỆT ĐỐI KHÔNG chứa `ecommerce:`** (đúng như `ChannelHubConsts.LockKeys` trả
> `channel:…` — `modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:19–32`).
> ✅ **Tiền lệ lỗi đã đóng (`0f2f286`, 13/10/2026):** `TaxLockKeys` từng trả `ecommerce:tax:…` ⇒ khoá thật thành
> **`ecommerce:ecommerce:tax:…`**; nay đã bỏ tiền tố — `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:45–50` trả
> `tax:classification:{tenantId}:{targetId}` / `tax:rate-schedule:{tenantId}`. `NOT-002` giữ đúng quy ước:
> hàm sinh khoá **không** chứa `ecommerce:` (§15.2 **F2** đã đóng).

### 8.3 ⚠️ `REP-*` **KHÔNG** được query DB giao dịch (ràng buộc bắt buộc)

`REP-001` là *"Read-model & job tổng hợp (**không query nặng trên DB giao dịch**)"* (`docs/implementation-plan.md:436`).
`NotificationDeliveryEvents` **tăng trưởng nhanh nhất** trong module (mỗi tin có 3–6 event) ⇒ **tuyệt đối không**
cho báo cáo quét bảng này mỗi lần mở màn hình. Bắt buộc:

| Việc | Đúng | Sai (cấm) |
|---|---|---|
| Báo cáo tỉ lệ phát thành công theo kênh | job tổng hợp của `REP-001` ghi vào bảng read-model riêng | `SELECT COUNT(*) … GROUP BY ChannelKind` trên `NotificationDeliveryEvents` khi người dùng mở trang |
| Đối chiếu chi phí với nhà cung cấp | import file đối soát + so **theo minor units**, lưu kết quả vào read-model | join trực tiếp `NotificationCostLedger` với bảng đơn hàng |
| Cảnh báo vận hành (DLQ, tỉ lệ lỗi) | **metric** (ADR-07) + job định kỳ quét **có index** `(TenantId, Status, NextAttemptAtUtc)` | quét toàn bảng mỗi nhịp |

> 🔗 **`ACC-001`:** số liệu chi phí gửi đưa vào xuất liệu kế toán **phải** lấy từ **read-model** đã tổng hợp,
> và **phải** theo `XC-001` (làm tròn, minor units) + liên kết chứng từ thuế theo `XC-006`
> (ví dụ: chi phí gửi là **chi phí mua vào** ⇒ **ngoài** phạm vi Mẫu 01 Phần II mà hệ thống xuất —
> xem `docs/features/cross-cutting/XC-006-phan-loai-thue-gtgt.md` §6.3 điểm 3).

**Idempotency key:** xem §7.3.

---

## 9. Phân quyền

| Permission | Nhóm | Method được gán | Vai trò mặc định |
|---|---|---|---|
| `eCommerce.Notification.Messages.View` | `eCommerce.Notification` | `GetMessagesAsync`, `GetEventsAsync` | nhân viên CSKH |
| `eCommerce.Notification.Messages.Resend` | `eCommerce.Notification` | `ResendAsync` | CSKH trưởng |
| `eCommerce.Notification.Dlq.Replay` | `eCommerce.Notification` | `ReplayDlqAsync` | **admin** (thao tác rủi ro: gửi lại ra người thật) |
| `eCommerce.Notification.Channels.View` | `eCommerce.Notification` | `GetChannelsAsync` | nhân viên vận hành |
| `eCommerce.Notification.Channels.Manage` | `eCommerce.Notification` | `UpdateChannelAsync`, `TestSendAsync` | **admin** (đổi credential = đổi danh tính người gửi) |
| `eCommerce.Notification.OptOut.View` | `eCommerce.Notification` | `GetOptOutsAsync` | CSKH |
| `eCommerce.Notification.OptOut.Manage` | `eCommerce.Notification` | `CreateOptOutAsync` | CSKH trưởng |
| `eCommerce.Notification.Cost.View` | `eCommerce.Notification` | xem sổ chi phí gửi | **kế toán trưởng** (liên kết `ACC-001`) |

> 📌 **Định nghĩa permission là ĐIỀU KIỆN BẮT BUỘC, không phải việc phụ:** nếu permission **không** được khai
> trong `PermissionDefinitionProvider` thì ABP **luôn** coi là bị từ chối ⇒ API trả `403` dù user là admin
> (`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:8–11`).
> Danh sách permission phải được thêm vào `eCommerceConsts.Permissions` theo đúng khuôn hiện có
> (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:54–73`) — **một nhóm `eCommerce.Notification`** dùng chung `NOT-001`+`NOT-002` (`D-197`).

> 🚪 **Endpoint callback** (`POST /api/notification/callbacks/{providerCode}`) **KHÔNG mở ở GĐ3** (`D-202`): endpoint `[AllowAnonymous]` phải **người duyệt từng endpoint + rate limit** (`P10`). Nếu mở sau này, nó nằm ở class/controller **riêng** (không thuộc `NotificationAppService`) và **bắt buộc** tự xác thực chữ ký/IP allowlist rồi **mới** ghi dữ liệu. Tiền lệ allowlist có kiểm soát: `/health-status` (`eCommerceHttpApiHostModule.cs:506–509`) và các endpoint OpenIddict (`docs/features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md`).

> 📌 **Người nhận cảnh báo** (`Settings.opsAlertRecipients`/`opsAlertCriticalChannels`, `D-206`) sửa qua `PUT /api/notification/channels/{id}` với **`eCommerce.Notification.Channels.Manage`** (admin tenant, `P12`) — **không** có permission riêng; danh sách **thật** do Vận hành điền (DEC **không** bịa địa chỉ).

---

## 10. UI Mockup & mapping dữ liệu

| Vùng UI | Thành phần | Dữ liệu | Nguồn (DTO/trường) | Quy tắc |
|---|---|---|---|---|
| **Danh sách tin** | bảng + lọc theo kênh/trạng thái/khoảng thời gian | mã tin, kênh, trạng thái, người nhận (**đã che một phần**), mốc gửi | `NotificationMessages` | 🚨 **che** email/số điện thoại (`k****@gmail.com`, `09xx***456`); **cấm** hiển thị đầy đủ |
| **Chi tiết tin** | timeline | các `DeliveryEvent` theo thứ tự thời gian | `NotificationDeliveryEvents` | chỉ đọc (append-only) |
| **Nội dung đã gửi** | khối văn bản | `BodyMasked` | `NotificationMessages` | hiển thị **bản đã mask**; nếu là tin OTP ⇒ hiện *"nội dung không lưu (chứa OTP)"* |
| **DLQ** | bảng + nút phát lại | tin `DLQ`, `AttemptCount`, `LastReason` | `NotificationMessages` | nút phát lại yêu cầu permission `Dlq.Replay` + hộp thoại xác nhận nêu rõ **sẽ gửi ra người thật** |
| **Kênh gửi** | form | kênh, người gửi, hạn mức, quiet hours, trạng thái credential, **người nhận cảnh báo** (`opsAlertRecipients`, trần 20) | `NotificationChannelAccounts` | credential **chỉ ghi**, **không bao giờ** hiển thị lại; hiện `HasCredential = true/false` (khuôn `ChannelShopDto.HasRefreshToken` — `src/eCommerce.Application.Contracts/Channels/ChannelDtos.cs`); sửa người nhận cảnh báo dưới `Channels.Manage` (`D-206`) |
| **Danh sách từ chối** | bảng + lọc theo kênh/nguồn | `RecipientHash` (8 ký tự đầu), kênh, **nguồn** (`Source`), lý do, thời điểm, **đồng bộ lần cuối** | `NotificationOptOuts` + `NotificationChannelAccounts.OptOutLastSyncedAtUtc` | **0** plaintext; hiện banner *"danh sách NCC cũ từ ngày X"* khi quá `staleAfterDays`; tra-theo-người-nhận dùng `POST /opt-outs/check` (`D-204`/`D-207`) |
| **Sổ chi phí gửi** | bảng + tổng theo kỳ | đơn giá, số lượng, thành tiền | `NotificationCostLedger` (qua **read-model**) | hiển thị **tổng = tổng các dòng** (theo `XC-001` BR-005: **không** làm tròn tổng); ⚠️ nếu có bộ lọc ⇒ **phải ghi rõ** tổng đang là *tổng sau lọc* |
| **Cảnh báo vận hành** | banner | trạng thái kênh, tỉ lệ lỗi, kích thước DLQ | metric + read-model | không đọc DB giao dịch khi tải trang (§8.3) |

> 📌 **Phân biệt "không có dữ liệu" vs "lỗi thật"** (quy tắc của `docs/design-doc-guidelines.md` §2.1):
> danh sách tin **rỗng** ⇒ hiện hướng dẫn, **không** hiện lỗi và **không** trả `500`.
> Ngược lại, **kênh chưa cấu hình credential** ⇒ hiện **hướng dẫn thiết lập** + liên kết tới màn hình Kênh gửi,
> vì đây là **lỗi cấu hình** chứ không phải "chưa có dữ liệu".

**Ghi rõ chênh lệch số liệu:** "tổng chi phí trong kỳ" (theo `OccurredAtUtc`) **khác** "tổng chi phí của các tin
hiển thị" khi có bộ lọc theo kênh — UI **phải** ghi rõ đang hiển thị tổng nào. Đây là loại sai sót rất khó phát hiện
khi test (đã được ghi thành thói quen tốt trong `docs/design-doc-guidelines.md` §2.1).

---

## 11. Migration & seed

| Mục | Nội dung |
|---|---|
| Migration mới | **CÓ** — `Added_Notification_Module`: tạo **6** bảng `NotificationMessages`, `NotificationDeliveryEvents`, `NotificationInbox`, `NotificationCostLedger`, `NotificationChannelAccounts`, `NotificationOptOuts` + index + CHECK. Gồm: `UX_NotificationInbox_Dedup` **NULLS NOT DISTINCT** + `DedupKey`/`DedupKeyVersion` (`D-203`); `WebhookSecretEncrypted` (`D-202`); `OptOutLastSyncedAtUtc` (`D-207`); `NotificationMessages.RecipientEncrypted/Masked/Hash` (`D-206`); UNIQUE `(TenantId, IdempotencyKey)` + `(TenantId, ProviderCode, ProviderMessageId)` trên `NotificationMessages` **NULLS NOT DISTINCT** (`D-203`); `NotificationOptOuts.RecipientHash` HMAC hex64 + `HashVersion` + `Source`/`SourceRef` + UNIQUE **NULLS NOT DISTINCT** + `CHECK Source IN (0,1,2,3)` (`D-204`/`D-207`) |
| `DbSet` | 🚨 **BẮT BUỘC** khai báo **cả 6** `DbSet` trên `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs`, nếu không `IRepository<T, Guid>` **không** được đăng ký (bài học đã trả giá — `eCommerceDbContext.cs:47–52`) |
| Module mapping | thêm **một lần** `builder.ConfigureNotification();` vào `OnModelCreating` — **một cửa cấu hình** dùng chung `NOT-001`+`NOT-002` (`D-197`; `eCommerceDbContext.cs:347`) |
| Seed | **CÓ, idempotent**: KHÔNG seed credential thật. Chỉ seed (a) `NotificationChannelAccounts` ở trạng thái `IsEnabled=false` cho 3 kênh với `SenderIdentity` rỗng — để UI có chỗ điền; (b) **không** seed opt-out; (c) **không** seed mẫu tin (`NOT-001` sở hữu) |
| 🚨 **CẤM seed** | **CẤM** seed API key / mật khẩu SMTP / OA token vào migration hoặc `appsettings.json` (kế thừa cảnh báo `XC-004` F4 về mật khẩu seed trong tài liệu — `docs/features/cross-cutting/XC-005-observability-log-metric.md:463`) |
| Ảnh hưởng dữ liệu cũ | **Không backfill** — module mới, không có dữ liệu lịch sử. Tin cũ (nếu có) nằm ngoài hệ thống |
| Rollback | `DROP` 6 bảng; an toàn vì không có module nào tham chiếu **khoá ngoại** tới chúng (module khác chỉ phát ETO) |
| Việc phải làm **trước** khi thêm metric | cập nhật `eCommerceMetricNames` + `eCommerceMetricTags` (`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs`) **và** `XC-005` §5.1 **và** test `MetricsTests` (`MetricsTests.cs:45–73` — so tập tên phát ra với registry, **không** chốt số lượng) — theo ADR-07/BR-008 |
| ⚠️ **Còn nợ phải xử lý trong PR đầu** | (1) Đăng ký `NotificationAppService` + permission vào `eCommercePermissionDefinitionProvider`; (2) thêm `typeof(NotificationApplicationModule)` vào `[DependsOn]` của `eCommerceHttpApiHostModule`; (3) thêm section `Notification` vào `appsettings.json` |

---

## 12. Xử lý lỗi & trường hợp biên

| Tình huống | Nguyên nhân | Hành vi hệ thống | Phản hồi |
|---|---|---|---|
| **Không có tin nào cần gửi** | hàng đợi rỗng | job **không** log `Information` (tránh ngập log — `XC-005` BR-004/D9) | **không phải lỗi** |
| Trùng `IdempotencyKey` | retry của tầng trên | **Không** gửi lại; trả kết quả cũ + `AlreadySent=true` | `200` (hoặc `409` `0001` ở chế độ cấm trùng) |
| Người nhận sai định dạng | dữ liệu khách sai | `ThatBai` vĩnh viễn, **không** thử lại, `DeliveryEvent(FailedPermanent)` | `400` `0002` |
| Thiếu / hết hạn credential | chưa cấu hình, token hết hạn | `ChoThuLai` + backoff dài + **metric + cảnh báo vận hành** | `503` `0003` |
| Vượt hạn mức nhà cung cấp | gửi nhiều / quota ngày | `ChoThuLai` với `Reason=quota`; **không** tính là lỗi vĩnh viễn | `429` `0004` |
| Nhà cung cấp timeout | sự cố NCC | `ChoThuLai`, thử lại theo backoff; quá 3 lần ⇒ `DLQ` | log `Error` + metric |
| Nhà cung cấp nhận nhưng **không** trả `ProviderMessageId` | NCC lỗi/hợp đồng khác | `ThatBai` (**không** đánh dấu thành công — nếu không sẽ không bao giờ theo dõi được phát) | `502` `0006` |
| **Callback trùng** | NCC gửi lại (at-least-once) | `IgnoredDuplicate`; **không** ghi event thứ hai; vẫn trả `200` (để NCC ngừng retry) | `200` |
| **Callback sai thứ tự** | mạng/NCC | `IgnoredOutOfOrder` + log `Warning`; **không** lùi trạng thái (E7) | `200` |
| **Callback không xác thực được** | giả mạo / cấu hình sai | `Rejected`; **không** đổi trạng thái tin; metric + log `Warning` | `403` `0008` |
| NCC **không** hỗ trợ callback | hợp đồng NCC | Tin **ở lại `DaGui`** ⇒ sau TTL thành `HetHanXacNhan`; đối soát §8.3 | log `Information` **một lần**/tin |
| Người nhận đã **opt-out** | khách yêu cầu | `Huy` + `DeliveryEvent(Cancelled)`; **không** gửi | `409` `0009` |
| Gửi ngoài **quiet hours** | chính sách | Dời `NextAttemptAtUtc` sang khung cho phép (**không** mất tin) | bình thường |
| **Không giành được khoá** | tiến trình khác đang gửi | **NÉM** `eCommerce:Lock:0001` (mã của `XC-003`) | `409` |
| **Redis chết** | sự cố hạ tầng | Lock **vô hiệu** nhưng app **vẫn chạy** (`AbortOnConnectFail=false` — `eCommerceHttpApiHostModule.cs:251`). ⇒ Bất biến **phải** được DB bảo vệ: unique index idempotency + CHECK `AttemptCount` (§8.1 #1/#6) | bình thường |
| Tin chứa **OTP** | đổi COD (`SHP-003`) | **Không** lưu nội dung; chỉ ghi "đã gửi OTP tới đuôi số X"; TTL + giới hạn gửi lại (BR-015) | bình thường |
| Trạng thái vận đơn đến từ webhook **không HMAC** (GHTK — `SHP-002`) | NCC không ký | **Không** dùng làm nguồn duy nhất cho tin có **số tiền** (BR-014) | log `Warning` |
| Callback thiếu `ProviderMessageId` / payload không parse | NCC lỗi/định dạng lạ | **Fail-closed:** `400` + **0** dòng inbox; **cấm** `Guid.NewGuid()` (BR-019, `D-203`) | `400` |
| `webhookAuthMode = none` mà nhận callback | kênh chưa cấu hình xác thực Zalo | `SignatureValid=false` + `ProcessStatus=Rejected` + metric BR-018; **không** đổi trạng thái tin (`D-202`) | `403` `0008` |
| `sentAckTtlMinutes` trống/`≤0`/sai/ngoài biên | cấu hình lỗi | **Dùng mặc định** kênh + log `Warning` + `ErrorCode`; **không** hết hạn tức thì (BR-021, `D-205`) | bình thường |
| Danh sách từ chối NCC **cũ**/chưa đồng bộ | job KÉO lỗi/không adapter | `required=false` ⇒ giữ danh sách cũ + `Error` + metric, **không** chặn gửi giao dịch; `required=true` ⇒ `0003` fail-closed (BR-023, `D-207`) | `503` `0003` (khi `required=true`) |
| Cảnh báo kỹ thuật **thiếu người nhận** / host-level | chưa cấu hình `opsAlertRecipients`; `TenantId=null` | Trống ⇒ **NÉM** `0003`, 0 tin, log `Error` + metric; **host-level chưa có đường gửi** — nợ có chủ OPS + BA `XC-005` (BR-022, `D-206`) | `503` `0003` |
| Lỗi đã xử lý bị nuốt | lập trình sai | **Bắt buộc** log ≥ `Warning` + `ErrorCode` (BR-018; kế thừa `XC-005` BR-013) | — |

**Chiến lược transaction:** mọi thao tác ghi đi qua **Unit of Work** của ABP
(`TransactionBehavior.Auto` — `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:91–95`).
`NotificationMessage` + `NotificationDeliveryEvent` + **outbox** ghi **cùng một transaction** (BR-006).
Phạm vi **khoá phân tán bao NGOÀI** Unit of Work (theo `XC-003` §12) ⇒ khoá được nhả **sau** khi transaction kết thúc.

---

## 13. Phi chức năng

| Mã | Yêu cầu | Ngưỡng đo được | Cách kiểm chứng |
|---|---|---|---|
| NFR-NOT-002-001 | Độ trễ `SendAsync` | p95 **< 2 s** (SMS/Zalo) · **< 5 s** (email) | benchmark với fake sender + đo thật 1 NCC |
| NFR-NOT-002-002 | Chống gửi trùng | **1.000** lần cùng `IdempotencyKey` ⇒ **1** tin ra ngoài | integration test (đếm số lần fake sender được gọi) |
| NFR-NOT-002-003 | Callback có `CorrelationId` + chống trùng | **100 %** | integration test gửi cùng callback 2 lần |
| NFR-NOT-002-004 | Không lộ OTP/token/số điện thoại trong log | **0** mẫu khớp | quét mã nguồn ở CI (kế thừa `XC-005` TC-05) |
| NFR-NOT-002-005 | Nhãn metric | **0** nhãn cardinality cao; **< 100** series/metric | test cấu hình (kế thừa `XC-005` TC-07) |
| NFR-NOT-002-006 | Retry có giới hạn | tối đa **3** lần, backoff **1/5/25 s**; **0** tin thử quá 3 lần | unit + CHECK ở DB |
| NFR-NOT-002-007 | Thông lượng SMS | **≥ 50** tin/giây/instance | benchmark với fake sender |
| NFR-NOT-002-008 | Quiet hours | **100 %** tin marketing có `NextAttemptAtUtc` trong khung cho phép | unit với `TimeProvider` giả (khuôn `XC-006` NFR-004) |
| NFR-NOT-002-009 | Cách ly tenant | **0** rò rỉ credential/nội dung | integration test 2 tenant |
| NFR-NOT-002-010 | Opt-out (**mọi** `Source`) | **0** tin gửi tới người nhận đã từ chối (khách / thủ công / NCC báo / file NCC) | integration test (`TC-37`) |
| NFR-NOT-002-011 | Chi phí gửi | đúng tới **0,0001**; lệch **0** với file đối soát NCC | đối chiếu theo **minor units** |
| NFR-NOT-002-012 | Log một dòng JSON + `CorrelationId` | **100 %** | kế thừa `XC-005` NFR-002/008 |
| NFR-NOT-002-013 | Không query nặng trên DB giao dịch khi mở báo cáo | **0** truy vấn không-index trên `NotificationDeliveryEvents` từ UI | review + `EXPLAIN` trên read-model |
| NFR-NOT-002-014 | Metric phát ra **nằm trong** bảng đăng ký | **100 %** | test `TC-06` hiện có (`MetricsTests.cs:38–47`) |

---

## 14. Test cases & Acceptance Criteria

### 14.1 Danh sách test case

| ID | Mục tiêu | Loại | Input | Expected | Phủ |
|---|---|---|---|---|---|
| TC-01 | Gửi email happy path | Unit (fake sender) | yêu cầu hợp lệ | `DaGui` + `ProviderMessageId` + 2 event `Queued`/`Sent` | **BR-001**, NFR-001 |
| TC-02 | Gửi SMS happy path | Unit | như trên, `ChannelKind=Sms` | `DaGui` | **BR-001** |
| TC-03 | Gửi Zalo OA happy path | Unit | như trên, `ChannelKind=ZaloOa` | `DaGui` | **BR-001** |
| TC-04 | **Cùng `IdempotencyKey` 1.000 lần** | **Integration** | vòng lặp 1.000, có cả chạy song song | **1** tin gửi ra, **1** `NotificationMessage`; 999 lần `AlreadySent` | **BR-002**, NFR-002 |
| TC-05 | **Callback gửi trùng** | **Integration** | cùng callback 2 lần | lần 2 `IgnoredDuplicate`, **không** thêm event, trả `200` | **BR-003**, NFR-003 |
| TC-06 | Callback **sai chữ ký** | Unit | chữ ký hỏng | `Rejected`, **không** đổi trạng thái, `403` `0008` | **BR-003**, BR-018 |
| TC-07 | **Mọi nhánh state machine §3** | Unit (lý thuyết hoá) | 13 cặp chuyển trạng thái | hợp lệ ⇒ đổi trạng thái + event; **sai ⇒ NÉM** `eCommerce:Notification:0007` | **BR-004** |
| TC-08 | **Lùi trạng thái** `DaPhat` → `ChoGui` | Unit | gọi thẳng hàm | **NÉM** `0007` | **BR-004** |
| TC-09 | Không sửa/xoá `DeliveryEvent` | Unit (reflection/quét) | quét API của repository | **0** đường `Update`/`Delete` cho event | **BR-005** |
| TC-10 | Ghi message + event + outbox **cùng transaction** | **Integration** | cho outbox ném lỗi | **không** có `NotificationMessage` nào được ghi | **BR-006** |
| TC-11 | **Cấm nhãn cardinality cao** | Unit (cấu hình) | danh sách nhãn dùng trong module | **0** nhãn nằm trong `eCommerceMetricTags.Forbidden` | **BR-007**, NFR-005 |
| TC-12 | Metric phát ra nằm trong registry | Unit (quét + `MeterListener`) | tên metric literal | **0** metric ngoài `eCommerceMetricNames.All` | **BR-008**, NFR-014 |
| TC-13 | 🚨 **Không log/lưu OTP** | Unit (quét mã nguồn) | tìm `otp` trong lời gọi log và trong cột ghi | **0** match | **BR-009**, NFR-004 |
| TC-14 | Nội dung lưu đã **mask** | Unit | nội dung chứa `otp`/số thẻ | `BodyMasked` **không** chứa giá trị gốc | **BR-009** |
| TC-15 | **Token Zalo OA: double-check trong khoá** | **Integration** | 2 luồng cùng refresh | chỉ **1** lần gọi API làm mới; luồng 2 dùng token **mới** | **BR-010** |
| TC-16 | Không giành được khoá ⇒ **NÉM** `eCommerce:Lock:0001` | **Integration** | giữ khoá trước | `BusinessException` code `eCommerce:Lock:0001` | **BR-010**, ADR-08 |
| TC-17 | **Retry có giới hạn** | Unit | sender luôn lỗi tạm thời | thử **3** lần rồi vào `DLQ`; `AttemptCount` không vượt 3 | **BR-011**, NFR-006 |
| TC-18 | Không có lần gọi NCC nào **thiếu timeout** | Unit (reflection) | quét API adapter | **0** overload thiếu `CancellationToken`/timeout | **BR-011** |
| TC-19 | **Quiet hours** (đồng hồ giả) | Unit | 22:00 gửi tin marketing | `NextAttemptAtUtc` = đầu khung cho phép | **BR-012**, NFR-008 |
| TC-20 | Quiet hours **KHÔNG** chặn `Priority=Critical` | Unit | 02:00, cảnh báo kỹ thuật | gửi **ngay** | **BR-012** (ngoại lệ A3) |
| TC-21 | **Opt-out** | **Integration** | người nhận đã từ chối | `Huy`, **0** lần gọi sender; `409` `0009` | **BR-012**, NFR-010 |
| TC-22 | 🚨 **Không `double`/`float`** trong biểu thức tiền | Unit (reflection) | quét type của module | **0** xuất hiện | **BR-013**, NFR-011 |
| TC-23 | So tiền **không** dùng `==`; dùng minor units | Unit | đối chiếu chi phí | khớp | **BR-013**, NFR-011 |
| TC-24 | 🚨 Webhook **không HMAC** (GHTK — `SHP-002`) | Unit | callback trạng thái vận đơn có `COD` | **không** gửi tin chứa số tiền; sinh cảnh báo cần đối chiếu | **BR-014** |
| TC-25 | 🚨 **OTP đổi COD** (GHN — `SHP-003`) | **Integration** | gửi OTP | gửi qua kênh đã xác thực; **0** bản ghi chứa OTP; giới hạn gửi lại theo cửa sổ | **BR-015** |
| TC-26 | **Không** gọi app service module khác | Unit (quét tham chiếu) | quét `using`/DI của module | **0** tham chiếu tới `I*AppService` của module khác | **BR-016**, ADR-09 |
| TC-27 | Credential **không** xuất hiện trong DTO | Unit (reflection) | quét DTO | **0** trường chứa credential | **BR-017** |
| TC-28 | Lỗi đã xử lý có log + `ErrorCode` | Unit (quét) | tìm `catch` rỗng | **0** catch rỗng | **BR-018** |
| TC-29 | **Cách ly tenant** | **Integration** | tenant B đọc/ghi dữ liệu tenant A | không thấy / không ghi được | NFR-009 |
| TC-30 | `/health-status` **không** gọi NCC (không làm chậm/chết health check) | **Integration** | NCC "chết" (fake) | health vẫn `200`; trạng thái NCC chỉ ở log/metric | NFR-001 |
| TC-31 | Báo cáo **không** query trực tiếp `NotificationDeliveryEvents` | Unit (quét) | quét lớp reporting | **0** truy vấn tới bảng event | **NFR-013**, §8.3 |
| TC-32 | Phát lại DLQ chỉ **một** tiến trình | **Integration** | 2 luồng cùng phát lại | chỉ 1 thành công; luồng 2 nhận `eCommerce:Lock:0001` | §3 hàng 12 |
| TC-33 | Chuẩn hoá người nhận ⇒ **cùng hash** | Unit | `09xx…`, `+84 9xx…`, `(+84) 9xx…`; email hoa/thường + khoảng trắng | cùng `RecipientHash`; khác người ⇒ khác hash; khớp `^[0-9a-f]{64}$` | **BR-020** |
| TC-34 | Opt-out **0 plaintext** + ghi trùng ⇒ **1** dòng | Unit + schema | quét cột `NotificationOptOuts`; ghi trùng `(TenantId, ChannelKind, RecipientHash)` | **0** cột plaintext; trùng qua `CreateOptOutAsync` ⇒ **1** dòng | **BR-020** |
| TC-35 | API opt-out **không lộ plaintext** | Unit | `GET opt-outs`; `POST opt-outs/check` | `GET` **chỉ** hash + 8 ký tự đầu; `check` **0** query/0 log plaintext | **BR-020** |
| TC-36 | Thiếu khoá HMAC production ⇒ **fail-closed** | Unit | cấu hình thiếu `Notification:OptOut:HashKey` | **NÉM** lỗi cấu hình, **0** tin gửi, **0** dòng opt-out | **BR-020** |
| TC-37 | Opt-out **mọi `Source`** đều bị chặn | **Integration** | `Source ∈ {ProviderReport, ProviderSuppressionImport}` | `Huy` + **0** lần gọi adapter + `409` `0009` | **BR-023**, NFR-010 |
| TC-38 | NCC báo "từ chối" lúc gửi ⇒ **tự ghi opt-out** | Unit | adapter trả `SendOutcome.RecipientRefused` | **đúng 1** dòng opt-out `Source=ProviderReport` (cùng UoW) + tin `ThatBai` + **0** gửi lại | **BR-023** |
| TC-39 | `DedupKey` ổn định | **Integration** | cùng payload 2 lần; đổi thứ tự khoá/khoảng trắng JSON | **1** dòng inbox, lần 2 `200` `IgnoredDuplicate`; **cùng** `DedupKey` | **BR-019**, NFR-003 |
| TC-40 | `DedupKey` **fail-closed** | Unit | thiếu `ProviderMessageId`/payload hỏng; id > 128 | `400` + **0** dòng inbox; id > 128 ⇒ `"h:"+sha256(nguyên)` **không** cắt chuỗi | **BR-019** |
| TC-41 | `SentAckTtl` mặc định + biên | Unit (`TimeProvider` giả) | Email 1439/1441; SMS/Zalo 720/721; khoá trống/`0`/`-1`/`"abc"`/`59`/`43201` | mặc định đúng; hết hạn ⇒ **đúng 1** `AckExpired` + **0** gọi mạng/gửi lại/ghi chi phí | **BR-021** |
| TC-42 | Cảnh báo kỹ thuật **thiếu người nhận** | Unit (quét) | `opsAlertRecipients` trống; quét literal email/SĐT | **NÉM** `0003`, **0** `NotificationMessage`, **0** lời gọi mạng; **0** literal ngoài test | **BR-022** |
| TC-43 | `Critical` **fan-out song song** | **Integration** | 2 kênh; 1 kênh lỗi; tất cả lỗi; quiet hours | **2** `NotificationMessage` khác `ChannelKind`/khoá; 1 lỗi **không** chặn kênh kia; tất cả lỗi ⇒ `DLQ`; quiet hours **không** chặn; **0** đệ quy | **BR-022** |
| TC-44 | Đồng bộ opt-out **idempotent + fail-closed** | **Integration** | cùng file 2 lần; job lỗi + `required=false`; `required=true` + cũ; mặc định `off` | **0** dòng mới lần 2; `required=false` ⇒ **0** tin chặn; `required=true` + cũ ⇒ `0003` + **0** adapter; `off`/0 adapter ⇒ **0** mạng | **BR-023** |
| TC-45 | Phần **trung lập NCC** fail-closed | Unit (quét) | kênh tắt/thiếu credential/thiếu `znsTemplateCode`/thiếu đơn giá | **NÉM** `0003` **trước** gọi mạng, **không** `DaGui`; thiếu đơn giá ⇒ **không** ghi `NotificationCostLedger`; **0** NuGet NCC/0 endpoint hard-code; test dùng `fake` sender | **BR-024** |
| TC-46 | **Thông lượng SMS** ≥ **50** tin/giây/instance | Benchmark (fake sender) | 1 instance, `ChannelKind=Sms`, hàng đợi đủ lớn, fake sender trả ngay | đo được **≥ 50** tin/giây/instance; **0** lời gọi mạng | **NFR-007** |
| TC-47 | Log **một dòng JSON** + `CorrelationId` trên mọi sự kiện gửi | Unit (bắt log) | gửi 1 tin + 1 callback | **100 %** dòng log của module là **một** JSON/dòng và có `CorrelationId` khớp ETO; **0** log đa dòng | **NFR-012** |

### 14.2 Fixture skeleton (xUnit)

```csharp
public class NotificationChannelTests
{
    /// <summary>TC-04 — chống gửi trùng là bất biến quan trọng nhất (tốn tiền thật nếu sai).</summary>
    [Fact]
    public async Task TC04_cung_idempotency_key_chi_gui_mot_lan()
    {
        var dem = new DemSoLanGuiSender();          // fake INotificationChannelSender
        var sut = TaoSender(dem);
        var yeuCau = TaoYeuCau(idempotencyKey: "order:O-1:shipped:sms");

        for (var i = 0; i < 1_000; i++)
        {
            await sut.SendAsync(yeuCau);
        }

        Assert.Equal(1, dem.SoLanGui);
    }

    /// <summary>TC-07/TC-08 — guard trạng thái PHẢI ném lỗi, không im lặng.</summary>
    [Fact]
    public async Task TC08_khong_duoc_lui_trang_thai_tu_DaPhat()
    {
        var tin = TaoTinDaPhat();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Task.FromResult(tin.ChuyenSang(NotificationStatus.ChoGui)));

        Assert.Equal("eCommerce:Notification:0007", ex.Code);
    }

    /// <summary>TC-19 — dùng đồng hồ giả để kiểm quiet hours (khuôn XC-006 NFR-004).</summary>
    [Theory]
    [InlineData("2026-10-13T22:30:00", false)]  // ngoài khung ⇒ phải hoãn
    [InlineData("2026-10-13T09:00:00", true)]   // trong khung ⇒ gửi ngay
    public async Task TC19_quiet_hours(string thoiDiem, bool guiNgay)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(thoiDiem));
        var (sut, sender) = TaoSenderVoiDongHo(clock);

        await sut.SendAsync(TaoYeuCauUuTien("Normal"));

        Assert.Equal(guiNgay, sender.CoGuiNgay);
    }

    /// <summary>TC-25 — OTP TUYỆT ĐỐI không được lưu (BR-015). `BodyMasked`/`GhiChuKhongLuuNoiDung`
    /// là mã minh hoạ — KHÔNG thuộc `SendResult` (§6.1) mà đọc từ `NotificationMessage` đã lưu.</summary>
    [Fact]
    public async Task TC25_tin_otp_khong_luu_noi_dung()
    {
        var (sut, _) = TaoSenderVoiKhoLuu();
        var tin = await sut.SendAsync(TaoYeuCauOtpDoiCod());
        var daLuu = LayNotificationMessage(tin.MessageId);   // SendResult → entity (§6.1)

        Assert.Null(daLuu.BodyMasked);
        Assert.Contains("OTP", daLuu.GhiChuKhongLuuNoiDung);
    }
}

public class NotificationConventionTests
{
    /// <summary>TC-11/TC-12 — nhãn & metric phải nằm trong bảng đăng ký (khuôn XC-005 TC-06/TC-07).</summary>
    [Fact]
    public void TC11_TC12_metric_va_nhan_hop_le()
    {
        var nhanDung = QuetNhanMetricTrongModule();
        var viPham = nhanDung.Intersect(eCommerceMetricTags.Forbidden, StringComparer.OrdinalIgnoreCase);

        Assert.True(!viPham.Any(),
            "Nhãn BỊ CẤM trong module Notification:\n" + string.Join("\n", viPham));
    }

    /// <summary>TC-22 — cấm double/float trong biểu thức tiền (XC-001 BR-008).</summary>
    [Fact]
    public void TC22_khong_dung_double_float_cho_tien()
    {
        Assert.Empty(QuetKieuSoThucTrongModuleNotification());
    }
}
```

### 14.3 Acceptance Criteria (Definition of Done)

- [ ] **47 test case** pass, phủ **hết 24 BR** và **14 NFR** (`NFR-007` = `TC-46`, `NFR-012` = `TC-47`)
- [ ] **Idempotency ở CẢ HAI biên vào** chứng minh bằng test: `TC-04` (khoá gửi, có chạy **song song**) và `TC-05` (callback trùng) — **unique index** là hàng rào, **không** chỉ code
- [ ] **Mọi nhánh state machine (§3) có test** — **kể cả nhánh guard sai** (`TC-07`, `TC-08`)
- [ ] **Redis chết ⇒ bất biến vẫn đúng** (nhờ unique index + CHECK) — test tích hợp dừng Redis (kế thừa `XC-003` TC-11)
- [ ] 🚨 **`TC-13` chứng minh 0 OTP trong log và trong DB** — bắt buộc, vì đây là rủi ro bảo mật thật (`SHP-003`)
- [ ] 🚨 **`TC-24` chứng minh không dùng webhook không-HMAC (GHTK) làm nguồn duy nhất cho tin có số tiền** (`SHP-002`)
- [ ] **Bảng đăng ký metric (`XC-005` §5.1) đã cập nhật** và `TC-06`/`TC-12` xanh (ADR-07/BR-008)
- [ ] **6 bảng có `DbSet`** trên `eCommerceDbContext` + `ConfigureNotification()` trong `OnModelCreating`
- [ ] `[Authorize]` **cấp CLASS** trên `NotificationAppService` + **8 permission** đã định nghĩa trong `PermissionDefinitionProvider`; test `401`/`403`
- [ ] **Cách ly tenant** đúng: `TC-29` xanh
- [ ] **`REP-*` không query DB giao dịch**: `TC-31` xanh; báo cáo đọc từ read-model
- [ ] **Migration chạy được từ DB trống**, rollback được; **seed idempotent** và **không** chứa credential
- [ ] **0 gói ABP trùng phiên bản**: mọi gói `Volo.Abp.*` mới (nếu có) **phải** dùng `10.7.0` trong `Directory.Packages.props`
- [ ] **Tài liệu này cập nhật khớp code** (quy tắc vàng `docs/design-doc-guidelines.md` §4)

### 14.4 Definition of Ready

- [ ] Tài liệu đã **review và Approved** (Kiến trúc trưởng + Vận hành + **Security** — vì có OTP/credential/PII)
- [ ] **Q1 đã chốt**: chọn nhà cung cấp email + SMS (§15.1)
- [x] ✅ **Ranh giới `NOT-001` / `NOT-002` đã rõ** (theo `implementation-plan.md:439–440`)
- [x] ✅ **Đã xác nhận hiện trạng**: 0 dòng code miền thông báo, 0 gói NuGet, module chưa tồn tại
- [x] ✅ **Đã xác nhận ràng buộc cứng**: `email`/`phone` **bị cấm** làm nhãn metric; registry hiện **16** tên
- [x] ✅ **Q2 đã chốt — D-201**: lưu `BodyMasked` **đã mask** (không lưu hash, không lưu thô); tin OTP ⇒ `BodyMasked = NULL` + ghi chú
- [ ] **Q3 đã chốt**: cơ chế xác thực callback của từng nhà cung cấp (**chưa kiểm chứng**)
- [ ] **Đơn giá/segment NCC đã có** để ghi `NotificationCostLedger` — **thiếu ⇒ KHÔNG suy đoán `UnitCost`** (việc `R3-6`, `D-200`; xem §5.4)
- [ ] **`XC-002` sẵn sàng** hoặc có thoả thuận chạy chế độ suy giảm (§15.3 giả định 1)
- [ ] **`eCommerce.Kernel`** đã tồn tại **hoặc** xác nhận `NOT-002` tự đặt helper trong module (§15.3 giả định 4)

---

## 15. Câu hỏi mở, giả định & lịch sử thay đổi

### 15.1 Câu hỏi mở

| # | Câu hỏi | Cần ai | Trạng thái | Ảnh hưởng nếu chưa trả lời |
|---|---|---|---|---|
| **Q1** | Chọn **nhà cung cấp** email (SMTP riêng / dịch vụ API) và SMS brandname (eSMS / VietGuys / Twilio / …)? | Vận hành + Kế toán | `Open` | Không viết được adapter thật; chỉ chạy được fake sender |
| **Q2** | Có **lưu nội dung** tin đã gửi (bản mask) hay chỉ lưu **hash** nội dung? | Security + Kiến trúc trưởng | `Đã chốt — D-201` | Lưu `BodyMasked` **đã mask** (một cửa, hàm thuần trong module, tái dùng **luật** `PaymentTransaction.MaskSensitiveData`, **cấm** `using eCommerce.PaymentHub`); **không** thêm `BodyHash`; tin OTP ⇒ `BodyMasked = NULL` + ghi chú; **cấm** nội dung trong DTO danh sách/ETO/log/metric (§5.1) |
| **Q3** | **Zalo OA**: cơ chế xác thực webhook, loại tin (CSKH / ZNS), **cửa sổ gửi**, mẫu tin, giá — hiện **chưa kiểm chứng** trong repo | Vận hành + đối tác Zalo | `Open` | Không thể chốt `NotificationChannelAccounts.Settings`; rủi ro gửi sai loại tin |
| **Q4** | Nhà cung cấp có cấp **`ProviderEventId`** cho callback không? Nếu **không** ⇒ khoá chống trùng phải là **hash payload** | Dev (đọc tài liệu NCC) | `Đã chốt — D-203` | **Một** cột `DedupKey` (`"e:"+id` hoặc `"h:"+sha256(JSON nghiệp vụ)`) + UNIQUE `UX_NotificationInbox_Dedup` **NULLS NOT DISTINCT**; `ProviderEventId` chỉ để điều tra; băm payload thô **trước** mask; fail-closed 400 + 0 inbox (§5.3) |
| **Q5** | `NotificationOptOuts.RecipientHash`: **băm** hay **mã hoá**? | Security | `Đã chốt — D-204` | **Băm một chiều** HMAC-SHA256 hex 64 + `HashVersion`; **0** cột plaintext; một cửa `NotificationRecipientHash.Tinh`; `GET opt-outs` chỉ trả hash; thêm `POST opt-outs/check`; UNIQUE `(TenantId, ChannelKind, RecipientHash)` **NND**; thiếu khoá production ⇒ fail-closed (§5.6/§6.2) |
| **Q6** | **TTL xác nhận** (`SentAckTtl`) là bao lâu cho từng kênh? Đề xuất **24 h** (email) · **12 h** (SMS) · **12 h** (Zalo OA) | Vận hành + NCC | `Đã chốt — D-205` | Mặc định **1440/720/720 phút** ở `Settings.sentAckTtlMinutes`; biên `[60,43200]`, sai ⇒ mặc định + `Warning`; hết TTL chỉ `HetHanXacNhan` + 1 `AckExpired` + metric, **0** gửi lại/mạng/chi phí; siết theo SLA = đổi cấu hình (§3) |
| **Q7** | Cảnh báo kỹ thuật (`XC-005` Q3) gửi tới **danh sách email nào**, và có cần kênh **thứ hai** (SMS/Zalo) cho mức `Critical` không? | Vận hành | `Đã chốt — D-206` (phần thiết kế) | Người nhận = `Settings.opsAlertRecipients` (trần 20; trống ⇒ `0003`); `Critical` fan-out theo `opsAlertCriticalChannels` (mặc định `["Email"]`); GĐ3 **Email-only**; rủi ro **F6 chặn release** tới khi bật kênh 2 hoặc chủ dự án chấp nhận **bằng văn bản**; danh sách **thật** Vận hành điền lúc triển khai |
| **Q8** | Có cần **hợp nhất** opt-out với nhà cung cấp (SMS: danh sách từ chối của NCC) không? | Vận hành + NCC | `Đã chốt — D-207` | `NotificationOptOuts` là nguồn sự thật + cửa kiểm **duy nhất**; đồng bộ chỉ qua **2 adapter** (KÉO `IOptOutSourceAdapter` mặc định **off** + ĐẨY `SendOutcome.RecipientRefused`); `required=true` mà danh sách cũ ⇒ `0003`; **không** đẩy danh sách ta sang NCC; dịch vụ/kết luận pháp lý tin quảng cáo vẫn ở `R3-5` (§5.6/§6.1/§10) |
| **Q9** | Chi phí gửi có được hạch toán vào **giá vốn đơn hàng** không, hay chỉ là chi phí kỳ? | Kế toán trưởng | `Đã chốt — D-193` | **Chi phí kỳ** (bán hàng phát sinh), **KHÔNG** vào giá vốn; kỳ = `OccurredAtUtc` đổi ngày UTC+7; `NotificationCostLedger` giữ đúng **8 cột**, **không** thêm `OrderId`; **cấm** join sổ chi phí với bảng đơn (§8.3) |
| **Q10** | `NOT-002` nằm ở **module mới `modules/Notification/`** hay gộp vào module `Messaging` của `XC-002`? | Kiến trúc trưởng | `Đã chốt — D-197` | **Một** module `modules/Notification/` (4 project như `ChannelHub`) dùng chung `NOT-001`+`NOT-002`; **KHÔNG** gộp `Messaging` (giữ outbox/inbox `XC-002`); một `ChannelKind`/`NotificationErrorCodes`/`NotificationLockKeys`/nhóm permission `eCommerce.Notification` |

### 15.2 🚨 Phát hiện & sai lệch đang tồn tại (liên quan trực tiếp tới `NOT-002`)

| # | Vấn đề | Bằng chứng (đường dẫn thật) | Mức độ | Xử lý |
|---|---|---|---|---|
| **F1** | **Miền thông báo hoàn toàn CHƯA CÓ trong code**: không có project, không có entity, **0 gói NuGet** gửi thư/SMS/Zalo | `modules/` có 15 module nhưng **không** có `Notification` (24/11/2026); `Directory.Packages.props` (0 gói email/SMS) | 🟠 **Trung bình** (*không* phải lỗi — đúng kế hoạch GĐ3) | ⬜ **Chưa có** — tài liệu này là bước chuẩn bị; code là **kế hoạch GĐ3** |
| **F2** | ✅ **ĐÃ SỬA trong code (`0f2f286`, 13/10/2026)** — `TaxLockKeys` từng trả `ecommerce:tax:…` trong khi `KeyPrefix` **đã là** `"ecommerce:"` ⇒ khoá thật thành **`ecommerce:ecommerce:tax:…`**; nay trả **không tiền tố**, đúng quy ước `XC-003` §5.4 điểm 2 và `ChannelHubConsts` | `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:45–50` (`tax:classification:…`/`tax:rate-schedule:…`) **vs** `modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:23,27,31` + `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:237` | 🟢 **Đã đóng** | `NOT-002` **giữ** quy ước: hàm sinh khoá **không** chứa `ecommerce:` (§8.2) |
| **F3** | ✅ **ĐÃ SỬA trong code** — **16** metric có **điểm phát thật** ở luồng nghiệp vụ (vd `DeadLetterAppService`, `ChannelTokenManager`, `OrderStatusMetricHandler`); registry lên **16 tên**, test không còn chốt cứng số lượng | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:81–107` · `test/eCommerce.Tests/Observability/MetricsTests.cs:45–73` | 🟢 **Đã đóng** | `NOT-002` **vẫn phải** gọi metric **thật** ở luồng gửi và cập nhật registry theo ADR-07/BR-008 |
| **F4** | **`ChannelOrderPulledEto` ĐÃ mang `CorrelationId`** (thêm ở `e7c56f6`, 16/10/2026); chỉ `ChannelShopTokenRefreshedEto` còn thiếu (`XC-005` BR-003) | `modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:72` (`ChannelOrderPulledEto`) · `:117–125` (`ChannelShopTokenRefreshedEto`); BR-003 ở `docs/features/cross-cutting/XC-005-observability-log-metric.md:79` | 🟠 **Trung bình** (một ETO còn thiếu ⇒ không nối được chuỗi log xuyên tiến trình) | 🔶 **Một phần** — `NOT-002` **phải** có `CorrelationId` trong ETO của mình (§7.1) |
| **F5** | ✅ **`XC-002` ĐÃ triển khai**: có bảng `AbpEventOutbox`/`AbpEventInbox`, `DeadLetterAppService` (phát lại DLQ qua outbox), `IdempotencyGuard` dùng chung | `Migrations/20261024045237_AbpEventBusOutboxInbox.cs:68–72` · `src/eCommerce.Application/Messaging/DeadLetterAppService.cs:40` · `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:50` | 🟢 **Đã đóng** | `NOT-002` **dùng trực tiếp** outbox/inbox/DLQ của `XC-002` (không cần chế độ suy giảm) |
| **F6** | **`XC-005` Q3 yêu cầu "alert gửi email kỹ thuật" nhưng chưa có nơi gửi** — tài liệu ghi rõ *"Thuộc `NOT-002`"* | `docs/features/cross-cutting/XC-005-observability-log-metric.md:449` · `docs/features/cross-cutting/XC-003-khoa-phan-tan-quy-uoc.md:528` | 🟡 **Thấp** ở GĐ3, **Cao** trước production | ✅ **Đã bao phủ** trong tài liệu này (A3 của §1, BR-012 ngoại lệ `Critical`, Q7) |
| **F7** | **`eCommerce.Kernel` ĐÃ CÓ** (`shared/eCommerce.Kernel/`: `Money`, `RoundingPolicy`, `LockErrorCodes`, `INotificationEvent`, `Observability/eCommerceMetrics.cs`) nhưng **thiếu** helper minor-units/`ILockManager` | `shared/eCommerce.Kernel/Money/Money.cs` · `.../Money/RoundingPolicy.cs` · `.../Locking/LockErrorCodes.cs` · `.../Events/INotificationEvent.cs` | 🟡 **Thấp** | 🔶 `NOT-002` dùng `Money`/`LockErrorCodes` có sẵn; helper còn thiếu thì tự đặt trong module (§15.3 giả định 4) |
| **F8** | **0 định nghĩa `SettingDefinition` trong code app** — chỉ có **bảng** `AbpSettings`/`AbpSettingDefinitions` do migration ABP tạo. Cấu hình kênh gửi hiện **không có chỗ** | `grep "SettingDefinition"` chỉ khớp trong `src/eCommerce.EntityFrameworkCore/Migrations/` | 🟡 **Thấp** | ✅ **Đã xử lý trong thiết kế**: cấu hình kênh nằm ở bảng riêng `NotificationChannelAccounts` (§5.5), **không** phụ thuộc SettingManagement |

### 15.3 Giả định

| # | Giả định | Nếu SAI thì sao |
|---|---|---|
| 1 | **`XC-002` (outbox + relay + DLQ) ĐÃ TRIỂN KHAI** — `NOT-002` dùng trực tiếp, **không** cần chế độ suy giảm | Nếu hồi quy: `NotificationMessage` + `NotificationDeliveryEvent` trong UoW **vẫn đủ** để không mất tin (job quét DB); chỉ **mất** phát ETO cho module khác. **Cấm** dual-write không outbox |
| 2 | **Nhà cung cấp trả `ProviderMessageId`** khi nhận tin | Nếu **không**: không theo dõi được phát ⇒ `TC`/BR-003 vô nghĩa cho kênh đó; phải chuyển sang **đối soát chủ động** bằng file/DLQ và ghi rõ trong tài liệu NCC |
| 3 | **Nhà cung cấp có callback trạng thái** | Nếu **không** (một số SMS brandname chỉ báo qua file): tin ở `DaGui` → `HetHanXacNhan`; phải bổ sung **import file đối soát** (việc mới, cần tài liệu riêng) |
| 4 | `NotificationErrorCodes`/`NotificationLockKeys`/`ChannelKind` **thuộc module** `modules/Notification/` (một chỗ, `D-197`) | Kernel đã có (`shared/eCommerce.Kernel/`) nhưng là hạ tầng chung; nếu sau này Kernel có helper tiền/khoá dùng chung thì chuyển theo, **không** đổi khoá Redis |
| 5 | Gửi tin **không** cần chữ ký số hay lưu trữ pháp lý | Nếu cần (ví dụ thông báo có giá trị pháp lý): phải thêm lưu trữ bất biến + chữ ký ⇒ **thay đổi lớn**, tách tài liệu riêng |
| 6 | **Số điện thoại/email là PII** ⇒ phải mask khi hiển thị/log | Nếu pháp lý cho phép hiển thị: giảm việc mask **nhưng** `email`/`phone` **vẫn** bị cấm làm nhãn metric (ràng buộc ở `eCommerceMetrics.cs:218–223`, **không** thương lượng được ở tài liệu này) |
| 7 | `TimeProvider` khả dụng để kiểm quiet hours/TTL | Nếu không: test phụ thuộc đồng hồ hệ thống ⇒ **giòn**; xem NFR-008 (khuôn `XC-006` NFR-004) |

### 15.4 Lịch sử thay đổi

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| `v0.1` | 13/10/2026 | *(điền)* | Bản nháp đầu — **hiện trạng lấy từ code thật** (0 dòng code miền thông báo · 0 gói NuGet · `AbpBackgroundJobs` đã có · **`XC-002` chưa triển khai** · 12 metric đăng ký nhưng **chưa được gọi ở nghiệp vụ** · `email`/`phone` **bị cấm** làm nhãn metric). Chốt **12 ADR**, **18 BR**, **14 NFR**, **32 TC**. Ghi nhận **F2** (tiền tố đôi trong `TaxLockKeys`) và **F4** (ETO thiếu `CorrelationId`) là sai lệch **đang tồn tại** ngoài phạm vi tài liệu |
| `v0.2` | 24/11/2026 | BA | Đối chiếu Nhanh (WP-14 `D-71`): đồng bộ `D-197` (một module `Notification`, enum `ChannelKind`, một `ConfigureNotification`/`NotificationErrorCodes`/`NotificationLockKeys`/nhóm permission `eCommerce.Notification`) · `D-199` (dải `0010–0020`, `BodyFormat`/`Subject` thuộc `NOT-001`) · đóng **Q9** theo `D-193` (chi phí kỳ) · sửa hiện trạng as-built (`eCommerce.Kernel`/`XC-002`/16 metric đã có) |
| `v0.3` | 24/11/2026 | BA | Vòng áp quyết định N61: `D-200` (trung lập NCC + fail-closed + email SMTP trước) · `D-201` (đóng Q2 — `BodyMasked` mask một cửa) · `D-202` (trung lập Zalo, callback GĐ3 **không mở`) · `D-203` (đóng Q4 — `DedupKey` + `UX_NotificationInbox_Dedup` NND) · `D-204` (đóng Q5 — opt-out băm HMAC + `opt-outs/check`) · `D-205` (đóng Q6 — `SentAckTtl` 1440/720/720) · `D-206` (đóng Q7 — cảnh báo fan-out + cột người nhận §5.1) · `D-207` (đóng Q8 — opt-out sync 2 adapter + `Source`). Thêm **BR-019…024**, **TC-33…TC-45** (45 TC, 24 BR) |
| `v0.4` | 24/11/2026 | BA | Vòng sửa DOCREV vòng 1: đóng **F2** (`0f2f286` — `TaxLockKeys` không còn tiền tố, §8.2/§15.2) · đóng một phần **F4** (`ChannelOrderPulledEto` đã có `CorrelationId`, chỉ `ChannelShopTokenRefreshedEto` thiếu, §7.1/§15.2) · §3 bỏ trạng thái không tồn tại `ChoTai` (hàng 1 = `[*]`, hàng 13 = `ChoGui`) · sửa dẫn as-built (`eCommerceMetrics.cs:218–223`, `PaymentTransaction.cs:313–337`/`otp` :323, `MetricsTests.cs:107+`) · gộp heading trùng thành `### 0.2` · §1 đủ **14 NFR** · §14.2 đặt đúng tên `TC-25` |

---

## Đối chiếu mô hình Nhanh (24/11/2026)

Đã đối chiếu theo danh sách `D-xx` của WP-14. Các `D-07`/`D-08`/`D-18`/`D-27`/`D-31`/`D-42`/`D-45` (mô hình hàng hoá/đơn/tồn/POS/công nợ) **không áp dụng** — tài liệu này không mô hình hoá các thực thể đó; `D-06`/`D-70` (NOT-* tối thiểu ở GĐ3) và `D-47`/`D-48` (báo cáo đọc read-model) đã đúng. Các chỗ lệch đã sửa:

| D-xx | Tài liệu cũ nói (§mục) | Đã sửa thành (§mục) | Code thật (file:dòng) / kế hoạch |
|---|---|---|---|
| `D-197` | §6.1 enum `NotificationChannelKind`; §9 nhóm `eCommerce`; §15.1 Q10 mở; §5 "mỗi module" một `ConfigureNotification` | §6.1 enum **`ChannelKind`**; §9 nhóm **`eCommerce.Notification`**; Q10 **`Đã chốt — D-197`**; §5 một `ConfigureNotification`/`NotificationErrorCodes`/`NotificationLockKeys`, không gộp `Messaging` | Chưa có module `Notification` (kế hoạch GĐ3); mẫu một cửa `ConfigureChannelHub()` `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:347` |
| `D-199` | §6.2 chưa nêu biên dải mã | §6.2 **NOT-002 `0001–0009`**, **NOT-001 `0010–0020`**; append-only, `NOT-002` không chiếm `0010+`; `BodyFormat`/`Subject` thuộc `NOT-001` | Chưa có code |
| `D-193` | §15.1 Q9 mở | Q9 **`Đã chốt — D-193`**; §5.4 chi phí **kỳ** (`OccurredAtUtc` UTC+7), giữ **8 cột**, không `OrderId`, cấm join §8.3 | `NotificationCostLedger` chưa có code |
| as-built | §0.1: `eCommerce.Kernel` chưa tồn tại · `XC-002` chưa triển khai · 12 metric chưa có nơi phát | §0.1: Kernel **đã có** (`Money`/`RoundingPolicy`) · `XC-002` **đã triển khai** · **16** metric có điểm phát | `shared/eCommerce.Kernel/Money/Money.cs` · `Migrations/20261024045237_AbpEventBusOutboxInbox.cs:68–72` · `eCommerceMetrics.cs:81–107` |
| `D-200` | §7.3/§5.5: "chưa chọn NCC" nhưng chưa nêu phần trung lập | §7.3 thêm khối trung lập (hợp đồng + `Settings` + `fake` + fail-closed `0003` + email SMTP trước); §5.4 ghi chú "đơn giá/segment chờ NCC" | Chưa có module `Notification` (kế hoạch GĐ3) |
| `D-201` | §5.1 `BodyMasked` ghi "mask theo `PaymentTransaction.MaskSensitiveData`"; Q2 mở | §5.1 nêu **luật mask của module** + một cửa ghi + cấm phụ thuộc chéo; Q2 **`Đã chốt — D-201`**; **không** `BodyHash` | Tái dùng **luật** `PaymentTransaction.MaskSensitiveData` `modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:313-337`; `Truncate` `:339` (không `using eCommerce.PaymentHub`) |
| `D-202` | §6.2/§9: callback là `[AllowAnonymous]`; §5.5 chưa có secret webhook | Callback GĐ3 **KHÔNG mở**; §5.5 thêm `WebhookSecretEncrypted` + 5 khoá Zalo (`webhookAuthMode` mặc định `none`, `ipAllowlist`, `znsTemplateCode`, `unitCostPerMessage`+`currency`, `apiBaseUrl`); §7.3 ghi "chưa xác minh 5 thông số" | Chưa có code; cột nullable (`D-202`) |
| `D-203` | §5.3 UNIQUE 5 cột có `ProviderEventId` + "hoặc Hash(payload)"; Q4 mở | §5.3 **một** `UX_NotificationInbox_Dedup` trên `DedupKey` **NULLS NOT DISTINCT**; `ProviderEventId` chỉ điều tra; Q4 **`Đã chốt — D-203`** | Tái dùng `CanonicalJsonHasher` `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/CanonicalJsonHasher.cs:21-100` |
| `D-204` | §5.6 "băm hay mã hoá?" chưa chốt; Q5 mở | §5.6 `RecipientHash` HMAC hex64 + `HashVersion` + UNIQUE NND; §6.2 thêm `POST opt-outs/check`; Q5 **`Đã chốt — D-204`** | Khuôn băm `modules/Address/eCommerce.Address.Domain/DiaChiCanonical.cs:22,86-103` |
| `D-205` | §3/Q6 "TTL đề xuất 24/12/12 h" chưa chốt | §3 nêu mặc định **1440/720/720 phút** ở `Settings.sentAckTtlMinutes`, biên `[60,43200]`, hết TTL chỉ `HetHanXacNhan`; Q6 **`Đã chốt — D-205`** | Chưa có code |
| `D-206` | §1 A1 nhắc `NotificationRecipient` **không có** trong §5; A3/Q7 chưa có nguồn người nhận | §1 A1/A3 sửa (mỗi người nhận một `NotificationMessage`, người nhận từ `Settings.opsAlertRecipients`, fan-out `Critical`); §5.1 thêm `RecipientEncrypted/Masked/Hash`; Q7 **`Đã chốt — D-206`** | Chưa có code; host-level `TenantId=null` = nợ OPS + BA `XC-005` |
| `D-207` | §5.6 không có `Source`; Q8 mở (lý do "vi phạm quy định") | §5.6 thêm `Source`/`SourceRef` + CHECK; §5.5 `OptOutLastSyncedAtUtc` + `optOutSync`; §6.1 `SendOutcome.RecipientRefused`; Q8 **`Đã chốt — D-207`** (phần thiết kế) | Chưa có code; pháp lý tin quảng cáo vẫn `R3-5` |
