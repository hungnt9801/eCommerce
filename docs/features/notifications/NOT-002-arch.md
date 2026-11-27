# `NOT-002` — Kiến trúc liên module: kênh gửi **trung lập nhà cung cấp** (hợp đồng adapter · registry · fail-closed `0003`)

> Vai **ARCH**, phiên **N106**, chuỗi `gd3-1`, hàng **`W23-1`** (bước **F0–F2b** của `WP-23`), nhánh `feat/gd3-wp23-kenh-gui`
> (`docs/agents/bang-phien-gd3-1.md:84`). Tài liệu này **chỉ thiết kế** phần **code mới** của `WP-23` trong
> [`not-002-kenh-gui-email-sms-zalo-oa.md`](./not-002-kenh-gui-email-sms-zalo-oa.md) (sau đây gọi **SRS**, `v0.4 — 24/11/2026`).
> **Không** code, **không** sinh migration, **không** sửa SRS.
>
> **Phạm vi hàng `W23-1` là hẹp có chủ đích** — `docs/nhanh/07-ke-hoach-trien-khai.md:753` ghi rõ `WP-23` =
> *"`NOT-002` kênh gửi — **thiết kế trung lập** … chỉ hợp đồng adapter + `fake` sender + fail-closed `0003`"*.
> Vì vậy tài liệu này **không** thiết kế đầy đủ 6 bảng / state machine / retry / callback của `NOT-002`; phần đó là
> **increment sau** (xem §1.3 và §1.4).
>
> **Nguồn quyết định:** `D-197` (một module `Notification`, một enum/error-codes/lock-keys/permission) — `docs/nhanh/decisions.md:211`;
> `D-200` (trung lập NCC, `0` NuGet NCC, fail-closed `0003`) — `:214`; `D-202` (thông số Zalo OA là vùng cấm, callback GĐ3 không mở) — `:216`;
> `D-204` (khoá HMAC production là việc chủ dự án) — `:218`; `D-264` (`INotificationSender` canonical) — `:278`;
> `D-312` (tạm duyệt thiết kế `NOT-002` có hiệu lực trên nhánh `WP-23`) — `:326`; `D-208` mục (7) — `:222`;
> hồ sơ `R3-6` — `docs/agents/uy-quyen-chu-du-an.md:83`, danh sách đỏ §2.1 — `:32-36`.
>
> **Module chủ của mọi kiểu/mã lỗi dưới đây: `Notification`** (`D-197` ①). Toàn bộ mục dưới là **thiết kế MỚI, kế hoạch GĐ3**;
> mọi `file:dòng` của code as-built đã đọc/đối chiếu trong phiên N106 (`codegraph_explore` trước, `Read`/`Grep` sau cho phần đồ thị cắt bớt).
>
> **Loại tài liệu:** `-arch` là tài liệu **bổ trợ**, **ngoài** mẫu 16 mục / BR / mục "Đối chiếu mô hình Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; đúng chủng loại khuôn `NOT-001-arch.md:16-18`
> và `docs/features/address/ADDR-002-arch.md:12-14`.
>
> **DOCREV:** vòng 1 (28/11/2026) ra `DOC-CHANGES` (**1 P1 · 8 P2 · 9 P3**, `docs/reviews/NOT-002-arch-docreview.md:3`);
> vòng 2 đã sửa **tại chỗ toàn bộ #1–#18** (xem §17.2).

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đang có trong code (đo bằng đồ thị + đọc tệp, không viết theo trí nhớ)

| Hạng mục | Hiện trạng | Bằng chứng (đường dẫn thật) |
|---|---|---|
| Module `Notification` | **CÓ** — 4 project (`Domain` · `Application.Contracts` · `Application` · `EntityFrameworkCore`); **28** tệp tracked (`git ls-files modules/Notification`), `modules/` có **17** thư mục (đo N106) | `modules/Notification/*/*.csproj:9-21` · `docs/nhanh/decisions.md:326` (`D-312` chốt **17** module / **28** tệp) |
| Hợp đồng bàn giao canonical | **CÓ** — `INotificationSender` đúng **một** method; request **8** trường; **bỏ** `TemplateCode`/`Data`/`Recipient` plaintext; **không** có `HandleCallbackAsync` | `modules/Notification/eCommerce.Notification.Application.Contracts/Sending/INotificationSender.cs:8-27` |
| Sender mặc định WP-22 | **CÓ** — `ChuaCoKenhGuiSender` **NÉM** `InvalidOperationException` (fail-closed, không "gửi ma"); có thêm `ITransientDependency` | `modules/Notification/eCommerce.Notification.Application/NotificationApplicationModule.cs:24,40-51` |
| Job bàn giao | **CÓ** — đọc `Notification:EnableDispatch` (mặc định `false` trong code), gọi sender ngoài transaction, `catch` mọi exception ⇒ *không* đánh dấu `DaBanGiao` | `modules/Notification/eCommerce.Notification.Application/Dispatch/NotificationDispatchWorker.cs:43-49,268-271,279,281-286` |
| Enum kênh | **CÓ** — **một** `ChannelKind { Email=0, Sms=1, ZaloOa=2 }`; **0** `NotificationChannelKind` | `modules/Notification/eCommerce.Notification.Domain/NotificationEnums.cs:7-12` |
| Mã lỗi | **CÓ** dải `0010–0020` (`NOT-001`); **CHƯA** khai `0001–0009` (`NOT-002`) | `modules/Notification/eCommerce.Notification.Domain/NotificationErrorCodes.cs:8-42` |
| Khoá phân tán | **CÓ** **một** lớp `NotificationLockKeys` (4 hàm, không tiền tố `ecommerce:`); **chưa** có khoá của `NOT-002` | `NotificationErrorCodes.cs:67-83` · nhãn module `"notification"` đã có ở `src/eCommerce.Application/Observability/DoThoiGianGiuKhoa.cs:36-38` |
| Hằng số độ dài | **CÓ** `NotificationConsts` (một nguồn); **chưa** có `ProviderCodeToiDa` | `NotificationErrorCodes.cs:45-61` |
| EF | **CÓ** **một** `ConfigureNotification` gọi **một lần**; **5** `DbSet` của `NOT-001`; **0** bảng `NOT-002` | `modules/Notification/eCommerce.Notification.EntityFrameworkCore/NotificationDbContextModelCreatingExtensions.cs:29` · `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:63-67,393` |
| Cấu hình Job | **CÓ** `NotificationSettingKeys` (`EnableDispatch`, `Dispatch:*`, `Batch:*`); **0** section `Notification` trong `appsettings*.json` của Host (đo N106) | `modules/Notification/eCommerce.Notification.Application/Rendering/NotificationSettingKeys.cs:4-14` |
| Quyền | **CÓ** lớp `Notifications` **9** quyền `Templates/Queue/Batches`; **chưa** có `Messages/Dlq/Channels/OptOut/Cost` | `src/eCommerce.Domain.Shared/eCommerceConsts.cs:448-464` · `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:218-231` |
| Map mã lỗi → HTTP | **CÓ** `0010–0020`; **0** map cho `0001–0009` | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:429-440` |
| Metric thông báo | **CÓ** **6 counter + 1 gauge backlog** (**7** tên `NOT-001`); nhãn cho phép **đã có** `channel`/`reason`; `ChuaCoNoiPhat` **không còn nợ `NOT-001`** (chỉ còn `promotion_applied_total` của `PRO-002`) | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:95-113,187-193,260-266,357-368,297-300,315` |
| Code gửi thật (SMTP/SMS/Zalo) | **0 dòng** — `grep` `mailkit|sendgrid|twilio|esms` trên `modules/Notification` + `Directory.Packages.props` = **0**; chỉ có chuỗi nhãn `"zalo_oa"` và enum | `NotificationQueueAppender.cs:304-310` · `NotificationEnums.cs:11` · `Directory.Packages.props` (không có gói NCC) |
| `fake` sender cho test | **CÓ** mẫu `SenderGia : INotificationSender` (đếm số lần gọi) | `test/eCommerce.Tests/Notification/NotificationQueueAppenderTests.cs:642-655` |
| Mã hoá người nhận | **CÓ** `IStringEncryptionService` dùng ở tầng Application; mask **một cửa** `NotificationMasker.CheBiMat` | `modules/Notification/eCommerce.Notification.Application/Queue/NotificationQueueAppender.cs:117-121` · `modules/Notification/eCommerce.Notification.Domain/Rendering/NotificationMasker.cs:15,70` |
| Bất biến schema | **114** index `NULLS NOT DISTINCT`; test **đích danh** tên index; `NotificationPersistenceTests` chốt **đúng 5** bảng | `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs:227` · `test/eCommerce.Tests/Notification/NotificationPersistenceTests.cs:29-36` |

**Kết luận:** `WP-23` **không** phải dựng lại module — module đã có (WP-22). Việc của `WP-23` là **mở đúng một cửa** cho lớp vận chuyển,
**chưa** gắn nhà cung cấp nào.

### 1.2 Phạm vi `WP-23` (IN)

1. **Hợp đồng adapter trung lập NCC** trong `eCommerce.Notification.Application.Contracts`:
   `INotificationChannelSender` · `INotificationChannelFactory` (registry) · record `SendCommand`/`SendOutcome` · enum `NotificationFailureKind`
   · cổng cấu hình kênh `INotificationChannelConfigProvider` (đọc-side, xem §4.3).
2. **`fake` sender cho test** — **0** kết nối mạng, đặt trong project test (không đăng ký ở Host).
3. **Fail-closed `eCommerce:Notification:0003`** (`503`): kênh chưa bật / thiếu credential / thiếu adapter / thiếu khoá cấu hình bắt buộc
   (gồm đơn giá) ⇒ **NÉM** `0003` **trước** mọi lời gọi mạng, **không** ghi `DaBanGiao`/`DaGui`.
4. **Thay sender mặc định**: `NotificationSender` (hiện thực canonical) là **hiện thực duy nhất** của `INotificationSender` ở production;
   `ChuaCoKenhGuiSender` được thay thế (xem §5.3, `AQ6`).

### 1.3 Ngoài phạm vi (OUT) — **hoãn có chủ** (`R3-6`, `D-200`/`D-202`/`D-204`, `D-193`)

- 6 bảng của `NOT-002`: `NotificationMessages` · `NotificationDeliveryEvents` · `NotificationInbox` · `NotificationCostLedger` ·
  `NotificationChannelAccounts` · `NotificationOptOuts` (SRS §5); **kể cả** bảng cấu hình kênh (`AQ1`).
- State machine đầy đủ 13 cặp chuyển trạng thái (SRS §3), `DaGui`/`DaPhat`/`DLQ`/`HetHanXacNhan`.
- Retry/backoff/DLQ (SRS §1 BR-011), quota/rate-limit, quiet hours, opt-out (BR-012/BR-020/BR-023), sổ chi phí (BR-013, `D-193`).
- Callback/webhook nhận trạng thái (SRS §7.2): **GĐ3 KHÔNG mở endpoint** (`D-202`) — chỉ thiết kế **tên** hợp đồng callback (`AQ5`).
- Adapter thật SMTP/SMS/Zalo OA; chọn NCC `Q1`/`Q3`; khoá HMAC production (SRS §15.1, `R3-6`).
- 8 endpoint `NOT-002` (SRS §6.2) và **FE** (SRS §10) — không thuộc `WP-23`.

### 1.4 Chừa chỗ cho increment sau (đối xứng `NOT-001-arch.md:70-83`)

| Hạng mục | `WP-23` (hàng này) | Increment sau | Cách chừa chỗ |
|---|---|---|---|
| Thư mục module | **thêm tệp** vào 4 project hiện có | **không** tách project | `D-197` ①(1) |
| Adapter thật | **0** adapter (registry rỗng ở production) | mỗi NCC = **một class** trong `Application/Channels/` | `D-200`; thêm class = có kênh |
| Cấu hình kênh | cổng `INotificationChannelConfigProvider` + stub trả `null` | hiện thực đọc `NotificationChannelAccounts` | thay hiện thực DI; **phải mở rộng** `FindAsync` (thêm `SenderIdentity`/`ChannelAccountId`) khi có nhiều tài khoản/kênh — **nợ N1**, không chỉ thay DI (§4.3) |
| Mã lỗi | thêm `0002`, `0003`, `0006` vào **cùng** `NotificationErrorCodes.cs` | thêm `0001/0004/0005/0007/0008/0009` | dải `0001–0009` là hợp đồng; `NOT-001` giữ `0010–0020` |
| Enum | thêm `NotificationFailureKind` vào **cùng** `NotificationEnums.cs` | `NotificationStatus` của `NOT-002` | **một** nơi khai enum (bài học `D-197` ⑥, `TC-44` của **`NOT-001` SRS**) |
| Bảng dữ liệu | **0 bảng, 0 migration** | 6 bảng `NOT-002` sau `R3-6` | một chủ migration (`D-216`), expand-only |
| Callback | chỉ **tên** hợp đồng (`AQ5`), **0** endpoint | parser + receiver sau (`D-202`) | endpoint ẩn danh phải **người duyệt từng endpoint** (P10) |
| Metric | **0 metric mới**, dùng lại `notification_queue_failed_total` | metric kênh/quota/chi phí | ADR-07: thêm metric phải cùng PR registry + `XC-005` §5.1 + test |
| Quyền | **0** quyền mới, **0** endpoint | `Channels.*`, `Messages.*`, `Dlq.*`, `OptOut.*`, `Cost.*` | thêm vào **cùng** lớp `eCommerceConsts.Permissions.Notifications` |

---

## 2. Ranh giới module

### 2.1 Chiều phụ thuộc (một chiều, `abp-module-rules` M1/M2/M5)

```
Host (eCommerce.HttpApi.Host)
  └─ [DependsOn] Notification.Application ──▶ Notification.Application.Contracts
                                             ├─▶ Notification.Domain
                                             └─▶ eCommerce.Kernel
     Notification.Application ──▶ Domain · Contracts · Kernel · eCommerce.Domain.Shared
                              ──▶ (ABP) Ddd.Application · BackgroundWorkers · DistributedLocking · EventBus · EntityFrameworkCore
     Notification.Application.Contracts ──▶ Domain · eCommerce.Kernel
     Notification.Domain      ──▶ (ABP) Ddd.Domain · eCommerce.Kernel (Money)
     Notification.EntityFrameworkCore ──▶ Domain · (ABP) EntityFrameworkCore
eCommerce.EntityFrameworkCore ──▶ Notification.EntityFrameworkCore (chỉ để gọi ConfigureNotification)
```

- Đo as-built: `Application.Contracts.csproj:9-10` ref **Domain + Kernel**; `Application.csproj:9-12` ref **Domain + Contracts + Kernel + Domain.Shared**
  (`modules/Notification/*/*.csproj`). `Application` **không** tham chiếu project `src/eCommerce.EntityFrameworkCore` — chỉ dùng **gói**
  `Volo.Abp.EntityFrameworkCore` (`:21`) cho `ExecuteUpdateAsync` của job. **0** `using eCommerce.Web/HttpApi/Application` trong module.
- `Notification` **không** `[DependsOn] Messaging`: lý do và bằng chứng đã chốt ở `NOT-001-arch.md:117-119` (outbox/inbox là hạ tầng ABP
  đã map trên DbContext hợp nhất `eCommerceDbContext.cs:417-418`) — không lặp lại, **không** đổi.

### 2.2 Luật áp cho `WP-23`

| # | Luật | Cách giữ |
|---|---|---|
| B1 | Module **sở hữu** mọi kiểu/mã của mình | `NotificationFailureKind`, `SendCommand`, `SendOutcome`, `0002/0003/0006` **chỉ** trong `modules/Notification/**` |
| B2 | Gọi chéo module **chỉ** qua `*.Application.Contracts` hoặc ETO | `WP-23` **không** tham chiếu module nghiệp vụ nào; adapter **cấm** gọi `I*AppService` module khác (`ADR-09`, BR-016) |
| B3 | Module **không** phụ thuộc ngược app/host | **0** `using` Host/Web/HttpApi trong `modules/Notification/**` |
| B4 | Không FK/navigation xuyên module | `WP-23` **0** entity ⇒ không phát sinh FK |
| B5 | `IRepository<T>` phải có `DbSet` hợp nhất | `WP-23` **0** entity/`DbSet` mới |
| B6 | Project `.Application` có `AbpModule` và nằm trong `[DependsOn]` Host | đã có `NotificationApplicationModule` (`NotificationApplicationModule.cs:13-19`); **không** đổi `[DependsOn]` |
| B7 | Trung lập NCC (`D-200`) | **0** gói NuGet NCC; **0** endpoint/brandname hard-code; mọi phụ thuộc hãng vào `Settings`/Setting; test dùng `fake` |
| B8 | Sự kiện mang dữ liệu fail-closed | `WP-23` **0** ETO mới; `NotificationSender` **0** publish ETO |

### 2.3 Vi phạm ranh giới hiện có

**0 vi phạm** liên quan `Notification` (đo N106). Nợ đã biết, **không sửa** (ngoài `WP`): `ChannelShopTokenRefreshedEto` thiếu `CorrelationId`
(`modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Events/ChannelEvents.cs:117-125`; đã ghi `NOT-001-arch.md:123`) —
`WP-23` **không** lặp lại (xem `AQ4`).

---

## 3. Thiết kế tầng Domain

### 3.1 Enum `NotificationFailureKind` (một nơi, `D-197`)

Tệp: `modules/Notification/eCommerce.Notification.Domain/NotificationEnums.cs` (nối **sau** `NotificationPriorities` — hiện ở `:91-97`).
Lưu DB: **không** — `WP-23` không có bảng; enum là kiểu trong bộ nhớ giữa adapter và sender. Vẫn khai **một nơi** và **chỉ thêm ở cuối** (`D-17`)
để increment sau có thể dùng lại.

| Giá trị | Số | Nghĩa | Hệ quả ở `NotificationSender` |
|---|---|---|---|
| `None` | `0` | thành công (adapter tự khai) | chỉ hợp lệ khi `Accepted = true`; `Accepted=false` + `None` ⇒ vi phạm hợp đồng ⇒ NÉM |
| `Transient` | `1` | timeout/5xx/rate-limit | trả `IsTransient = true` ⇒ job `HoanLaiChoGui` |
| `Permanent` | `2` | định danh sai / mẫu bị từ chối | trả `IsTransient = false, ErrorCode = 0002` ⇒ job `DanhDauLoi` |
| `QuotaExceeded` | `3` | hết hạn mức NCC | trả `IsTransient = true, ErrorCode = "quota"` (thử lại cửa sổ sau) |
| `Unauthenticated` | `4` | credential sai/hết hạn ở phía NCC | **NÉM** `0003` như mọi nhánh fail-closed (không trả `IsTransient`) — xem §6.2 / `docDeviation #8` |

### 3.2 `NotificationErrorCodes` — dải `0001–0009` (`NOT-002`)

Thêm **cùng tệp** `NotificationErrorCodes.cs` (`D-197` ⑥, `D-199`), **chỉ** hằng `WP-23` dùng thật (append-only; `NOT-001` giữ `0010–0020`):

| Hằng | Mã | Dùng ở `WP-23` |
|---|---|---|
| `NguoiNhanKhongHopLe` | `eCommerce:Notification:0002` | `SendOutcome` `Permanent` → kết quả canonical |
| `KenhGuiChuaSanSang` | `eCommerce:Notification:0003` | **NÉM** khi fail-closed (§6); map **HTTP 503** |
| `KhongCoProviderMessageId` | `eCommerce:Notification:0006` | adapter `Accepted=true` mà **không** có `ProviderMessageId` |

`0001`/`0004`/`0005`/`0007`/`0008`/`0009` **chưa** khai ở `WP-23` (chưa có bảng/DLQ/callback/opt-out). Đây là **khoảng trống có chủ**, không phải
mã bị chiếm: dải là hợp đồng, khai lúc dùng.

### 3.3 `NotificationConsts`

Thêm **một** hằng vào tệp hiện có (`NotificationErrorCodes.cs:45-61`): `ProviderCodeToiDa = 32` (khớp SRS §5.1/§5.5 `varchar(32)`).
Không thêm hằng nào khác: `WP-23` không có cột/bảng.

### 3.4 Không thêm entity/aggregate

`WP-23` **0** entity, **0** aggregate, **0** `IRepository`, **0** `DbSet`. Mọi trạng thái vận chuyển đầy đủ (message/delivery/cost) thuộc increment sau (§1.3).

---

## 4. Hợp đồng `Application.Contracts`

Tệp gợi ý: `modules/Notification/eCommerce.Notification.Application.Contracts/Sending/Channels.cs` (một tệp cho adapter + registry + config;
`INotificationSender.cs` **giữ nguyên** — đã phát hành ở WP-22, `D-264`).

### 4.1 `INotificationChannelSender` + `SendCommand` + `SendOutcome`

```csharp
namespace eCommerce.Notification;

/// <summary>Hợp đồng MỘT kênh gửi (ADR-01). CẤM app service/job gọi SDK/NCC trực tiếp (BR-001).</summary>
public interface INotificationChannelSender
{
    string ProviderCode { get; }
    ChannelKind ChannelKind { get; }

    /// <summary>Khoá Settings BẮT BUỘC của adapter (vd apiBaseUrl; SMS/Zalo: unitCostPerMessage + currency).
    /// Thiếu bất kỳ khoá nào ⇒ 0003 TRƯỚC khi gọi mạng (BR-024).</summary>
    IReadOnlyCollection<string> RequiredSettingsKeys { get; }

    Task<SendOutcome> SendAsync(SendCommand command, CancellationToken ct = default);
}

/// <summary>Lệnh gửi canonical của WP-23. KHÔNG có TemplateCode/Data (NOT-002 không render — ADR-06/ADR-09).
/// Recipient và credential ở dạng ĐÃ MÃ HOÁ; adapter giải mã ở biên cuối, KHÔNG log/DTO/ETO.
/// Adapter KHÔNG đọc lại INotificationChannelConfigProvider — mọi thứ cần dùng nằm trong lệnh này.</summary>
public sealed record SendCommand(
    Guid? TenantId,
    Guid QueueItemId,
    string IdempotencyKey,
    ChannelKind ChannelKind,
    string ProviderCode,
    Guid? ChannelAccountId,
    string? CredentialEncrypted,        // CHỈ ciphertext — xem AQ8; adapter giải mã ở biên cuối
    string RecipientEncrypted,
    string Subject,
    string Body,
    int Priority,                       // NotificationPriorities (0..3)
    string? CorrelationId,              // xem AQ4
    IReadOnlyDictionary<string, string> Settings);

/// <summary>Kết quả adapter. `Reason` là token NGẮN do adapter tự đặt (≤ 64, KHÔNG chứa payload/PII của NCC).
/// `RecipientRefused` GIỮ CHỖ cho D-207 — WP-23 KHÔNG xử lý (opt-out ngoài phạm vi).</summary>
public sealed record SendOutcome(
    bool Accepted,
    string? ProviderMessageId,
    NotificationFailureKind FailureKind,
    string? Reason,
    bool RecipientRefused = false);
```

- **Đường credential DUY NHẤT (đóng P1 vòng 1):** `NotificationChannelConfig.CredentialEncrypted` → `SendCommand.CredentialEncrypted`
  (nguyên ciphertext) → adapter giải mã **một lần** bằng `IStringEncryptionService`. Adapter **không** inject
  `INotificationChannelConfigProvider` (tránh đọc cấu hình hai lần/lệch `IsEnabled`); plaintext **cấm** log/serialize/DTO/ETO.
  Lựa chọn (a) này chờ DEC xác nhận ở `AQ8`.
- **Khác SRS §6.1** (đã ghi `docDeviation #2`): bỏ `TemplateCode`/`Data` (không render lại), bỏ `Priority` kiểu `string`,
  thay `MessageId` = id bảng chưa có bằng `QueueItemId` + `IdempotencyKey` (khoá đã phát hành), thêm `ProviderCode`/`Settings`/
  `RequiredSettingsKeys`/`RecipientEncrypted`/`CredentialEncrypted`.
- **Bỏ `RefreshCredentialAsync` ở `WP-23`**: SRS §6.1 có method này cho Zalo OA rotate token; `WP-23` **chưa** có adapter Zalo và
  **chưa** có kho credential ⇒ khai method không có hiện thực là hợp đồng chết (`docDeviation #3`, `AQ3`). Khi Zalo vào, thêm method là
  **additive**, không phá adapter đã có.
- **Bất biến của `SendOutcome`** (sender NÉM `InvalidOperationException` nếu vi phạm — fail-closed, không đoán):
  `Accepted = true ⇒ FailureKind = None`; `Accepted = false ⇒ FailureKind ≠ None`; `Accepted = true` mà `ProviderMessageId` rỗng ⇒ **coi là lỗi**
  theo SRS §3 hàng 3 (mã `0006`), **không** đánh dấu thành công.
- `Subject`/`Body` là **bản đã render + đã mask** đã lưu trên mục (canonical `INotificationSender.cs:14`); adapter **cấm** ghi log.

### 4.2 Registry `INotificationChannelFactory`

```csharp
public interface INotificationChannelFactory
{
    IReadOnlyCollection<INotificationChannelSender> All { get; }
    INotificationChannelSender? Find(ChannelKind channel, string providerCode);
}
```

- Hiện thực `NotificationChannelFactory` (tầng Application) nhận `IEnumerable<INotificationChannelSender>` của DI, dựng từ điển theo
  `(ChannelKind, ProviderCode)` **một lần**; **trùng khoá ⇒ NÉM khi khởi tạo** (không "last wins" im lặng).
- `Find` trả `null` khi không có adapter ⇒ `NotificationSender` NÉM `0003` (§6). `Find` **không** fallback sang kênh khác (cấm hạ cấp ZNS → CSKH, SRS §7.3).
- **Bên gọi thật:** chỉ `NotificationSender`. `fake` sender trong test cũng đi qua registry (test đường thật, không patch sender).

### 4.3 Cổng cấu hình kênh `INotificationChannelConfigProvider`

Đây là **seam** để kiểm fail-closed `0003` khi `WP-23` **chưa có** bảng `NotificationChannelAccounts`:

```csharp
public sealed record NotificationChannelConfig(
    Guid? ChannelAccountId,
    ChannelKind ChannelKind,
    string ProviderCode,
    bool IsEnabled,
    string? CredentialEncrypted,
    DateTime? CredentialExpiresAtUtc,
    IReadOnlyDictionary<string, string> Settings);   // jsonb đã parse; KHÔNG chứa secret (D-202)

public interface INotificationChannelConfigProvider
{
    /// <summary>Tenant lấy từ DI scope hiện tại (ABP `ICurrentTenant`); KHÔNG nhận TenantId từ caller.</summary>
    Task<NotificationChannelConfig?> FindAsync(ChannelKind channel, CancellationToken ct = default);
}
```

- `WP-23` hiện thực `ChuaCauHinhKenhProvider` trả **`null`** (không có kênh nào được cấu hình) ⇒ mọi lần gửi fail-closed `0003`.
  Đây là **hành vi đúng** ở `WP-23` (chưa chọn NCC, `D-200`), không phải việc bỏ ngỏ.
- Increment sau thay hiện thực DI bằng bản đọc bảng `NotificationChannelAccounts`. **Nợ N1:** SRS §5.5 cho phép **nhiều dòng**
  cùng `(TenantId, ChannelKind, ProviderCode)` khác `SenderIdentity` ⇒ `FindAsync` **phải mở rộng** (thêm `SenderIdentity`/
  `ChannelAccountId`) — không phải "chỉ thay DI, 0 đổi sender" như bản vòng 1.
- Credential trả về là **bản mã hoá**; sender chép **nguyên ciphertext** vào `SendCommand.CredentialEncrypted`; giải mã xảy ra
  **trong adapter** (biên cuối), **không** ở sender, **không** ở DTO. Adapter **không** đọc lại provider này.
- `FindAsync` **không** nhận `TenantId` từ tham số: tenant lấy từ `ICurrentTenant` để không thể đọc chéo tenant (`NFR-NOT-002-009`).

### 4.4 Ánh xạ canonical `NotificationSendRequest` → `SendCommand`

| Trường canonical (`INotificationSender.cs:16-24`) | `SendCommand` | Ghi chú |
|---|---|---|
| `TenantId` | `TenantId` | nguyên |
| `QueueItemId` | `QueueItemId` | nguyên |
| `IdempotencyKey` | `IdempotencyKey` | **nguyên văn**, adapter **cấm** sinh khoá mới (BR-002) |
| `ChannelKind` | `ChannelKind` | nguyên |
| `RecipientEncrypted` | `RecipientEncrypted` | **không** giải mã ở sender |
| `Subject` / `Body` | `Subject` / `Body` | bản đã render + mask; **không** render lại |
| `Priority` | `Priority` | `int` (0..3), khớp `NotificationPriorities` (`NotificationEnums.cs:91-97`) |
| *(không có)* | `ProviderCode` / `ChannelAccountId` / `CredentialEncrypted` / `Settings` | lấy từ `NotificationChannelConfig`; **credential chỉ ciphertext** |
| *(không có)* | `CorrelationId` | **phụ thuộc `AQ4`** — nếu không được chấp thuận thì `null` |

### 4.5 Ánh xạ `SendOutcome` → canonical `NotificationSendResult`

| # | `SendOutcome` | Điều kiện | `NotificationSendResult` | Hệ quả ở job (as-built) |
|---|---|---|---|---|
| 1 | `Accepted=true, FailureKind=None, ProviderMessageId ≠ rỗng` | hợp lệ | `(Accepted=true, AlreadySent=false, IsTransient=false, ErrorCode=null)` | `DanhDauDaBanGiao` + `HandedOff` + `NotificationHandedOffEto` (`NotificationDispatchWorker.cs:308-320`) |
| 2 | `Accepted=true` mà `ProviderMessageId` rỗng | SRS §3 hàng 3 | `(false, false, false, 0006)` | `DanhDauLoi` (`:337-349`) — **không** "thành công không theo dõi được" |
| 3 | `Accepted=false, Transient` | | `(false, false, true, "loi-tam-thoi")` | `HoanLaiChoGui` / `HetHanTuDangBanGiao` (`:321-335`) |
| 4 | `Accepted=false, QuotaExceeded` | | `(false, false, true, "quota")` | như #3 |
| 5 | `Accepted=false, Unauthenticated` | credential bị NCC từ chối | **NÉM** `BusinessException(0003)` (không trả kết quả) + metric | `catch` của worker ⇒ lease về `ChoGui`; **0** `DaBanGiao`; log `Warning` + `ErrorCode` |
| 6 | `Accepted=false, Permanent` | | `(false, false, false, 0002)` | `DanhDauLoi` + `NotificationQueueFailedEto` |
| 7 | `Accepted=false, None` **hoặc** (Accepted=true, FailureKind≠None) | vi phạm hợp đồng | **NÉM** `InvalidOperationException` (lỗi lập trình) | `catch` của worker ⇒ lease đưa về `ChoGui`; **0** đánh dấu |

- `AlreadySent` **luôn `false`** ở `WP-23`: chưa có bảng chống trùng của `NOT-002` (that là increment sau). Giữ trường để **không** đổi
  `NotificationSendResult` đã phát hành.
- `SendOutcome.Reason` **không** được truyền thẳng vào `LastReasonMasked`: sender chỉ map theo `FailureKind`; nếu ghi log thì **qua**
  `NotificationMasker.CheBiMat` (`NotificationMasker.cs:70`) và giới hạn token ngắn (adapter **cấm** trả nguyên payload NCC).
- `Unauthenticated` xử lý **giống hệt** nhánh thiếu credential (§6.1): **NÉM** `0003`, **không** trả `IsTransient` — một ngữ nghĩa
  cho mọi lỗi credential (`docDeviation #8`).
- Hàng 2 khác SRS §3 hàng 3: SRS nói guard sai ⇒ **NÉM** `0006`; ARCH **trả** `0006` để job chuyển `Loi` (không ném) —
  `docDeviation #9`, BA sửa §3/§12 cùng PR `W23-2`.
- `RecipientRefused = true` **bị bỏ qua** ở `W23-2` (rơi vào hàng 6 ⇒ `0002`); increment sau mới ghi opt-out `Source=ProviderReport`.

### 4.6 Luồng gọi một tin (mức khối)

```
NotificationDispatchWorker.BanGiaoAsync (as-built: :210-289)
  │  (C2a) UPDATE … WHERE Status=ChoGui + event Dispatching, cùng UoW
  │  (C2b) dựng NotificationSendRequest (TenantId, QueueItemId, IdempotencyKey, ChannelKind,
  │         RecipientEncrypted, Subject, Body, Priority) — :268-271
  ▼
INotificationSender.SendAsync(NotificationSendRequest)            [canonical, D-264]
  ▼
NotificationSender  (Application/Channels/NotificationSender.cs)
  1. config = configProvider.FindAsync(ChannelKind)   null ⇒ NÉM 0003   ── 0 mạng
  2. adapter = factory.Find(config.ChannelKind, config.ProviderCode)  null ⇒ NÉM 0003
  3. kiểm: IsEnabled · CredentialEncrypted ≠ rỗng · CredentialExpiresAtUtc > now
           · Settings ⊇ adapter.RequiredSettingsKeys                    vi phạm ⇒ NÉM 0003
  4. dựng SendCommand (RecipientEncrypted + CredentialEncrypted giữ nguyên ciphertext; Subject/Body đã mask)
  5. outcome = adapter.SendAsync(command, ct)        ← ĐIỂM DUY NHẤT có thể gọi mạng
  6. map outcome → NotificationSendResult (§4.5)
  ▼
NotificationDispatchWorker.GhiKetQuaBanGiaoAsync  (as-built: :292-…)
  Accepted/AlreadySent ⇒ DaBanGiao + HandedOff ; IsTransient ⇒ HoanLai/HetHan ; còn lại ⇒ Loi
```

- **Không** vẽ Mermaid để tránh rủi ro cú pháp; sơ đồ khối trên là hợp đồng luồng.
- Lời gọi adapter nằm **ngoài** transaction (job đã làm đúng: `:256-280`); `NotificationSender` **không** mở UoW, **không** ghi DB.
- `NotificationSender` **không** publish ETO (`WP-23` 0 ETO) — kết quả đi **đồng bộ** về job qua `NotificationSendResult`.

---

## 5. Registry + DI

### 5.1 Thêm một kênh = thêm một class

- Adapter mới: `modules/Notification/eCommerce.Notification.Application/Channels/<Provider>NotificationChannelSender.cs`,
  hiện thực `INotificationChannelSender` + `ITransientDependency`; **0** sửa `NotificationSender`, **0** sửa factory, **0** migration.
- Nếu cần đăng ký tường minh (ví dụ adapter có cấu hình riêng), thêm **một** dòng trong `NotificationApplicationModule.ConfigureServices` —
  vẫn là "thêm kênh = thêm class (+ 1 dòng DI)".
- **Cấm** đăng ký adapter trong Host/`eCommerce.EntityFrameworkCore`; **cấm** adapter ở project test lọt vào production.

### 5.2 Thứ tự kênh

- `D-200` ①: **email (SMTP tổng quát) trước**; SMS/Zalo OA sau khi `R3-6` đóng. `WP-23` **chưa** viết adapter nào (xem `AQ2`).
- Registry **không** có khái niệm "kênh ưu tiên/fallback" ở `WP-23`; fallback kênh (SRS A2) là increment sau.

### 5.3 Thay sender mặc định — **một** hiện thực `INotificationSender`

As-built có **hai** đường đăng ký cùng trỏ `ChuaCoKenhGuiSender`: dòng tường minh ở `NotificationApplicationModule.cs:24` **và**
`ITransientDependency` trên lớp (`:40`). Khi thêm `NotificationSender`, **không** được để hai hiện thực cùng tồn tại — kết quả resolve phụ thuộc
thứ tự đăng ký (rủi ro "last wins" im lặng, xem §14).

**Đề xuất** cho `W23-2` (chờ `AQ6`):

1. **Xoá** dòng `context.Services.AddTransient<INotificationSender, ChuaCoKenhGuiSender>();` (`:24`).
2. `NotificationSender` đăng ký **một** cách (conventional `ITransientDependency` **hoặc** tường minh — không cả hai).
3. **Xoá** `ChuaCoKenhGuiSender` (hoặc bỏ `INotificationSender`/`ITransientDependency` khỏi lớp). Bài test đỏ của `D-263`
   ("NÉM + 0 `DaBanGiao`") **được chuyển** sang `NotificationSender` với cùng kỳ vọng, không xoá kỳ vọng (`AQ6`).
4. Thêm test DI: `services.GetRequiredService<INotificationSender>()` trả **đúng** `NotificationSender`; đếm hiện thực trong assembly
   production = **1** (reflection). Thay đổi DI ⇒ **khởi động Host thật** (skill `abp-module-rules` §6).

Nếu DEC chọn **giữ** `ChuaCoKenhGuiSender`: bỏ `ITransientDependency` khỏi lớp (để nó **không** còn là ứng viên DI production),
giữ lớp làm fixture test; **chỉ** `NotificationSender` đăng ký `INotificationSender`.

### 5.4 Đăng ký `NotificationChannelFactory` / `ChuaCauHinhKenhProvider`

- `NotificationChannelFactory` : `ISingletonDependency` (chỉ đọc từ điển bất biến) hoặc `ITransientDependency` — chọn **một**, ghi trong PR.
- `ChuaCauHinhKenhProvider` : `ITransientDependency`, trả `null`; **không** đọc `appsettings`/DB.
- **0** sửa `[DependsOn]` module, **0** sửa `eCommerceHttpApiHostModule`/`eCommerceDbContext`/`eCommerce.slnx` (không có project mới).

---

## 6. Fail-closed `0003`

### 6.1 Điều kiện → hành vi → ca test

`NotificationSender` kiểm **tuần tự**; mọi điều kiện **trước** bước 5 (§4.6). Vi phạm ⇒
`throw new BusinessException(NotificationErrorCodes.KenhGuiChuaSanSang).WithData("channel", …).WithData("provider", …)`.
`WithData` **cấm** chứa credential/recipient/PII (BR-009).

| # | Điều kiện | Nguồn kiểm | Hành vi | Ca test (W23-4) |
|---|---|---|---|---|
| 1 | Không có cấu hình kênh (chưa tạo dòng / `FindAsync` trả `null`) | `INotificationChannelConfigProvider` | NÉM `0003` · **0** mạng · **0** state | `TC-FC-01` |
| 2 | `IsEnabled = false` | `NotificationChannelConfig` | NÉM `0003` | `TC-FC-02` |
| 3 | `CredentialEncrypted` rỗng/`null` | `NotificationChannelConfig` | NÉM `0003` | `TC-FC-03` |
| 4 | `CredentialExpiresAtUtc <= now` | `NotificationChannelConfig` + `IClock` | NÉM `0003` | `TC-FC-04` |
| 5 | Không có adapter khớp `(ChannelKind, ProviderCode)` | registry | NÉM `0003` | `TC-FC-05` |
| 6 | `Settings` thiếu khoá trong `RequiredSettingsKeys` (SMS/Zalo gồm `unitCostPerMessage` + `currency`) | adapter metadata | NÉM `0003` | `TC-FC-06` |
| 7 | Adapter tự ném `BusinessException(0003)` (vd Zalo thiếu `znsTemplateCode`) | adapter | nguyên `0003`, **0** mạng nếu adapter kiểm trước | `TC-FC-07` |

- **Hệ quả bắt buộc:** **0** lời gọi HTTP/SMTP; **0** `DaBanGiao` / **0** `DaGui`. **`NotificationSender` ghi 0** (không DB, không UoW),
  nhưng job as-built **vẫn ghi** `Dispatching` (C2a) và `LeaseExpired` (`NotificationDispatchWorker.cs:122-124`) mỗi lease.
  Ca test khẳng định **0 `DaBanGiao`/0 `DaGui`**, **không phải** "0 event/0 dòng ghi".
- **Map HTTP:** thêm **một** dòng trong khối `AbpExceptionHttpStatusCodeOptions` của Host
  (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:429-440`):
  `options.Map(NotificationErrorCodes.KenhGuiChuaSanSang, System.Net.HttpStatusCode.ServiceUnavailable); // 0003`.
  Không map ⇒ ABP trả `403` cho `BusinessException` ⇒ client hiểu sai thành "không có quyền" (bài học đã ghi ở `:426-428`).
- **Metric:** trước khi ném, tăng `notification_queue_failed_total` với `channel` + `reason = "eCommerce:Notification:0003"`
  (dùng lại metric đã đăng ký — xem §11). Không thêm metric ⇒ không kích hoạt ADR-07.
- **Log:** `Warning` **một** dòng JSON, có `ErrorCode`, `QueueItemId`, `ChannelKind`, `ProviderCode`; **0** recipient/credential/body
  (BR-018; `XC-005` BR-002).

### 6.2 Vì sao **NÉM** mà không trả `IsTransient`

- Job as-built `catch` mọi exception (`NotificationDispatchWorker.cs:281-286`), **không** gọi `GhiKetQuaBanGiaoAsync` ⇒ mục ở lại
  `DangBanGiao` ⇒ `KhoiPhucLeaseAsync` (`:100`) đưa về `ChoGui` **sau lease** (`Notification:Dispatch:LeaseSeconds` mặc định **120 s**,
  `NotificationSettingKeys.cs:8,12`). Nhờ vậy: **không** đánh dấu `DaBanGiao` giả (`D-263`), và cấu hình sai **không** âm thầm "thành công".
- Nếu trả `IsTransient=true` thay vì ném, mục cũng về `ChoGui` nhưng `LastErrorCode`/metric mang nghĩa "lỗi vận chuyển NCC", trong khi lỗi thật là
  **lỗi cấu hình** — dễ chẩn đoán sai. Ném `BusinessException(0003)` giữ đúng ngữ nghĩa "kênh chưa sẵn sàng" như SRS §6.2.
- **Mọi lỗi credential dùng chung một cách xử lý:** thiếu credential (kiểm **trước** khi gọi) **và** `Unauthenticated` do NCC trả
  (**sau** khi gọi) đều **NÉM** `0003`; không nhánh nào trả `IsTransient`. SRS §1 E3/§12 yêu cầu `ChoThuLai` + backoff dài cho
  credential — ARCH chọn **NÉM/lease** (`docDeviation #8`); BA sửa SRS cùng PR `W23-2`.
- Hệ quả cần biết: khi `EnableDispatch=true` mà kênh chưa cấu hình, **mỗi lease** sẽ thử lại và ném lại (retry storm ở mức log/metric).
  Đây là lý do `EnableDispatch` vẫn **mặc định `false`** (`:43-49`) cho tới khi có kênh thật; xem §14 rủi ro R1.

---

## 7. Job bàn giao — **không đổi**

- `WP-23` **không** sửa `NotificationDispatchWorker`: vẫn một worker định kỳ, `Timer.RunOnStart = true`, nhịp 10 s, khoá
  `notification:queue-dispatch:{tenant}` (`NotificationErrorCodes.cs:69-70`), `UPDATE … WHERE Status=ChoGui` (C2a), gọi sender ngoài transaction (C2b).
- Chỉ **một** thứ thay đổi ở biên DI: `INotificationSender` resolve ra `NotificationSender` (§5.3) thay vì `ChuaCoKenhGuiSender`.
- **Cấm** thêm dynamic worker / timer trong bộ nhớ (bài học C10, `NOT-001-arch.md:437`); **cấm** cho job biết adapter/NCC (trung lập qua interface).
- Retry/backoff/DLQ của `NOT-002` **không** thuộc job này ở `WP-23`; job hiện tại chỉ có retry "mỗi nhịp + lease" của `NOT-001`.

---

## 8. EF Core + migration

### 8.1 Quyết định: **0 bảng, 0 migration, 0 `DbSet` ở `WP-23`**

Lý do:

1. Phạm vi hàng này **chỉ** hợp đồng adapter + `fake` + `0003` (`07-ke-hoach-trien-khai.md:753`); bảng cấu hình kênh là **dữ liệu hợp đồng**
   phụ thuộc NCC/Zalo **chưa xác minh** (`D-200`, `D-202`, `R3-6`).
2. Thêm bảng sẽ buộc sửa **hai** bất biến test đang chốt: `NotificationPersistenceTests.cs:29-36` (đúng **5** bảng) và
   `PostgreSqlSchemaTests.cs:227` (**114** index `NULLS NOT DISTINCT`) — thay đổi schema không có bên dùng thật ở `WP-23`.
3. `ConfigureNotification` đã gọi **một lần** (`eCommerceDbContext.cs:393`); không có lý do mở cửa cấu hình khi chưa có entity.
4. Fail-closed `0003` **không cần** bảng: cổng `INotificationChannelConfigProvider` trả `null` là trạng thái "chưa cấu hình" trung thực (§4.3).

### 8.2 Phụ lục (ngoài phạm vi `WP-23`) — phương án `AQ1`: **nếu** DEC chọn có bảng

> Mục này **ngoài phạm vi** (đối chiếu §1.3) — chỉ để DEC cân nhắc `AQ1`. **Không đầy đủ:** nguồn duy nhất là SRS §5.5 + `D-202` + `D-207`.

Bảng `NotificationChannelAccounts` (module chủ `Notification`): `Id uuid PK` · `TenantId uuid NOT NULL` · `ChannelKind int` ·
`ProviderCode varchar(32)` · `SenderIdentity varchar(256)` · `CredentialEncrypted text NULL` · `RefreshCredentialEncrypted text NULL` ·
`WebhookSecretEncrypted text NULL` · `CredentialExpiresAtUtc timestamptz NULL` · `DailyQuota int` · `UsedToday int` ·
`QuietHoursFrom time NULL` · `QuietHoursTo time NULL` · `IsEnabled bool` · `OptOutLastSyncedAtUtc timestamptz NULL` ·
`Settings jsonb NULL` · `RowVersion xmin`.

- Cột **chưa dùng ở GĐ3** (có cột nhưng không có luồng đọc/ghi): `WebhookSecretEncrypted` (callback không mở, `D-202`),
  `OptOutLastSyncedAtUtc` (opt-out ngoài phạm vi, `D-207`), `RefreshCredentialEncrypted` (Zalo chưa bật),
  `DailyQuota`/`UsedToday` (quota ngoài), `QuietHoursFrom/To` (quiet hours ngoài). `CredentialEncrypted`/`Settings`/`IsEnabled`
  là các cột `WP-23` cần **nếu** chọn có bảng.
- UNIQUE `(TenantId, ChannelKind, ProviderCode, SenderIdentity)` **PHẢI `NULLS NOT DISTINCT`** — mọi unique index trên bảng
  `IMultiTenant` đều bắt buộc (`docs/coding-conventions.md:107`, CLAUDE.md §7), **kể cả** cột khoá `NOT NULL`.
- `Settings` **không** chứa secret (`D-202`); secret chỉ ở cột `*Encrypted`.
- Migration **do EF sinh**, expand-only; `pg_dump` **trước** khi áp lên DB có dữ liệu (`ef-migration-postgres` M9 / K-01); một chủ migration (`D-216`).
- Sau khi áp: cập nhật `NotificationPersistenceTests` (**5 → 6** bảng, `:29-36`) + `PostgreSqlSchemaTests` (**114 → 115**, `:227`)
  + thêm tên index đích danh.
- Đường lùi: trước merge `git revert` + `dotnet ef migrations remove`; sau merge migration **mới** `DROP TABLE` (không module nào có FK tới bảng này).

### 8.3 Đường lùi của phương án mặc định (0 migration)

- Code: `git revert` commit `W23-2`; khôi phục **một** dòng DI `ChuaCoKenhGuiSender` nếu cần (hoặc revert cả cụm).
- Vận hành: `Notification:EnableDispatch=false` (mặc định trong code `:43-49`) ⇒ job **không** bàn giao; hệ thống như trước `WP-23`.
- **0** dữ liệu, **0** schema ⇒ không cần `pg_dump`, không cần backfill.

---

## 9. Phân quyền

- `WP-23` **0** endpoint, **0** permission mới. Không thêm `Messages.*`/`Channels.*`/`Dlq.*`/`OptOut.*`/`Cost.*` (SRS §9) — các quyền này chỉ có
  nghĩa khi endpoint tồn tại; khai trước là "hợp đồng chết" (đúng ghi chú as-built ở `eCommerceConsts.cs:448-451`).
- Lớp `Notifications` hiện có **9** quyền (`eCommerceConsts.cs:453-464`) đã định nghĩa **đủ hai nơi**
  (`eCommercePermissionDefinitionProvider.cs:223-231`); `WP-23` **không** chạm.
- **0** `[AllowAnonymous]` mới; callback **không mở** (`D-202`).
- Nếu increment sau thêm `Channels.Manage`, **bắt buộc** thêm cùng lúc vào `eCommerceConsts.Permissions.Notifications` **và** provider
  (thiếu một nơi ⇒ `403` cho mọi người, kể cả `admin`).

---

## 10. PII & bảo mật

| # | Luật | Cơ chế ở `WP-23` |
|---|---|---|
| 1 | Credential **cấm plaintext/log/serialize**; ciphertext **chỉ** đi trong `SendCommand.CredentialEncrypted` tới adapter | `NotificationChannelConfig.CredentialEncrypted` (ciphertext) → `SendCommand.CredentialEncrypted` → adapter giải mã **một lần** qua `IStringEncryptionService` (khuôn `NotificationQueueAppender.cs:118`); **cấm** plaintext/DTO/ETO/log |
| 2 | Người nhận **không** plaintext ở biên module | canonical request chỉ có `RecipientEncrypted` (`INotificationSender.cs:21`); `SendCommand` giữ nguyên ciphertext; adapter giải mã **cuối**, **cấm** log/DTO/ETO |
| 3 | Nội dung gửi **đã mask** | `Subject`/`Body` lấy bản đã mask (`NotificationQueueAppender.cs:119-121`, `NotificationMasker.CheBiMat`); adapter **cấm** log |
| 4 | `SendOutcome.Reason` không chứa payload NCC | adapter **chỉ** trả token ngắn ≤ 64; sender mask trước khi log (`NotificationMasker.cs:70`) |
| 5 | **0** PII trong metric | nhãn chỉ `channel`/`reason`; `email`/`phone` nằm trong `Forbidden` (`eCommerceMetrics.cs:364-368`) |
| 6 | Tenant cách ly | `FindAsync` lấy tenant từ `ICurrentTenant`, **không** nhận `TenantId` từ caller; hiện thực stub trả `null` nên chưa có dữ liệu chéo |
| 7 | **0** kết nối mạng ở `WP-23` | **0** adapter production; test chỉ dùng `fake`; `TC-FC-*` khẳng định adapter **không** được gọi khi fail-closed |

- **OTP:** `WP-23` không có bảng/cột/nhánh ghi OTP; không log nội dung (BR-009/BR-015 vẫn ngoài phạm vi nhưng **không** bị vi phạm).
- **Khoá HMAC production** (`D-204`): **không** dùng ở `WP-23` (opt-out ngoài phạm vi) ⇒ **không** đọc/không tạo secret.

---

## 11. Metric & log

### 11.1 Metric: **0 metric mới**

- Dùng lại **`notification_queue_failed_total`** (`eCommerceMetrics.cs:101,189,428`) với nhãn `channel` (qua `NotificationQueueAppender.KenhNhan`,
  `:304-310`) và `reason = "eCommerce:Notification:0003"` khi fail-closed.
- **Không** sửa `eCommerceMetricNames`/`All`/`KyHieuMetric`/`ChuaCoNoiPhat`/`XC-005` §5.1 ⇒ **không** kích hoạt ADR-07; `MetricsTests` phải **xanh nguyên trạng**.
- Nếu decider muốn metric riêng (`notification_channel_unavailable_total`), đó là **thay đổi registry**: phải làm **cùng PR** registry + `XC-005` §5.1 +
  test hai chiều (`AQ7`). Mặc định: **không**.

### 11.2 Log

- Mỗi lần fail-closed: **một** dòng `Warning` JSON, có `ErrorCode`=0003, `QueueItemId`, `ChannelKind`, `ProviderCode`;
  **0** recipient/credential/subject/body (`XC-005` BR-005/BR-010).
- `CorrelationId`: chỉ có nếu `AQ4` được chấp thuận (canonical request hiện **không** mang trường này — xem §14 R3).
- Lỗi handled **không** bị nuốt: BR-018 thoả (log ≥ `Warning` + `ErrorCode`).

---

## 12. Hợp đồng FE

- **Không có** màn hình / endpoint / DTO mới ở `WP-23` ⇒ **0** thay đổi `apps/admin-web`, **0** diff `schema.d.ts` (kỳ vọng `pnpm gen:api` **không đổi**).
- FE của `NOT-002` (danh sách tin, DLQ, kênh gửi, opt-out, sổ chi phí — SRS §10) thuộc increment sau khi có endpoint; `W23-3` ghi "done kèm lý do"
  (`bang-phien-gd3-1.md:86`).
- Không có gì để FE hiển thị trạng thái "kênh chưa sẵn sàng" ở `WP-23`: lỗi chỉ xuất hiện trong job/log/metric.

---

## 13. Test / QA plan (map BR → tầng) + negative control

### 13.1 Theo tầng

> **Quy ước mã test:** toàn bộ `TC-FC-*`, `TC-EN-*`, `TC-MAP-*`, `TC-KEY-*`, `TC-LOG-*`, `TC-MET-*`, `TC-REG-*`, `TC-DI-*`,
> `TC-ARCH-*` là **TC mới — tạo ở `W23-4`**, **không** có trong SRS §14.1 (`TC-01…TC-47`). Bảng map ở cuối mục nối về TC gốc/BR.

| Tầng | Ca bắt buộc | Phủ |
|---|---|---|
| Domain thuần (không DB) | `TC-EN-01` (mới) `NotificationFailureKind` đúng 5 giá trị `0..4`, **append-only**; `TC-EN-02` (mới) `0002/0003/0006` khớp `^eCommerce:Notification:\d{4}$`, giao với `0010–0020` **rỗng**; `TC-EN-03` (mới) `ProviderCodeToiDa = 32` | `D-17`, `D-197` ⑥, `D-199` |
| Application (unit, `fake` adapter + `fake` config) | `TC-FC-01…07` (**mới**, bảng §6.1): **NÉM** `0003`, adapter **0** lần gọi, **0 `DaBanGiao`/`DaGui`**; `TC-MAP-01` (mới) bảng ánh xạ §4.5 (7 hàng); `TC-MAP-02` (mới) `Accepted=true` + `ProviderMessageId` rỗng ⇒ `0006`; `TC-MAP-03` (mới) `Accepted=false` + `None` ⇒ NÉM; `TC-MAP-04` (mới) `Unauthenticated` ⇒ NÉM `0003`; `TC-KEY-01` (mới) `IdempotencyKey`/`RecipientEncrypted`/`CredentialEncrypted`/`Subject`/`Body` **nguyên văn** sang `SendCommand`; `TC-LOG-01` (mới) log `Warning` + `ErrorCode`, **0** PII; `TC-MET-01` (mới) metric `reason=0003` | BR-001, BR-002 (phần truyền khoá), BR-018, BR-024, NFR-014 |
| Registry | `TC-REG-01` (mới) trùng `(ChannelKind, ProviderCode)` ⇒ NÉM khi khởi tạo; `TC-REG-02` (mới) `Find` trả `null` khi rỗng (không fallback) | BR-001, `D-200` |
| DI / Host | `TC-DI-01` (mới) **đúng một** `INotificationSender` = `NotificationSender`; `TC-DI-02` (mới) Host khởi động resolve được (skill §6 "thay DI ⇒ khởi động Host thật") | B6, §5.3 |
| Kiến trúc (quét) | `TC-ARCH-01` (mới) **0** gói NuGet NCC (`Directory.Packages.props` + csproj module); `TC-ARCH-02` (mới) `NotificationSender` **0** `HttpClient`/`SmtpClient`/`System.Net.Mail`; `TC-ARCH-03` (mới) **0** `I*AppService` của module khác; `TC-ARCH-04` (mới) **0** endpoint/`[AllowAnonymous]` mới; **0** metric mới ngoài registry | BR-001, BR-016, BR-024, NFR-005/014 |
| Endpoint | không có endpoint mới ⇒ kiểm **không đổi** ma trận `401/403/200` hiện có của module | `XC-004` |
| Integration (PostgreSQL) | **không có** ca mới (0 schema); chạy lại `PostgreSqlSchemaTests` phải **114** không đổi | §8 |

**Map TC mới → TC gốc/BR (cho QA/REV):**

| Mã mới (tạo ở `W23-4`) | TC gốc / BR | Ghi chú |
|---|---|---|
| `TC-FC-01…07` | `TC-45` (SRS `NOT-002`), BR-024 | tách 7 điều kiện fail-closed |
| `TC-EN-01…03` | `TC-44` của **`NOT-001` SRS**, `D-17`/`D-197` | enum/mã lỗi một nguồn |
| `TC-MAP-01…04` | `TC-01/02/03` (happy path), BR-001 | ánh xạ outcome → kết quả canonical |
| `TC-KEY-01` | `TC-04` (phần truyền khoá), BR-002 | chưa kiểm unique index (0 bảng) |
| `TC-LOG-01` | `TC-13`, `TC-47`, BR-009/BR-018 | log không PII, có `ErrorCode` |
| `TC-MET-01` | `TC-12`, NFR-014 | dùng lại metric đã đăng ký |
| `TC-REG-01/02` | `TC-45`, BR-001 | registry một cửa |
| `TC-DI-01/02` | (không có TC gốc), B6/§5.3 | DI một hiện thực + Host boot |
| `TC-ARCH-01…04` | `TC-26`, `TC-27`, BR-016/BR-017/BR-024 | 0 SDK/0 gói/0 app service khác |

### 13.2 Negative control **bắt buộc** (đột biến **đỏ** → khôi phục **xanh**; ORCH chạy `tools/agents/negative-control.ps1`)

| # | Đột biến | Ca phải đỏ |
|---|---|---|
| NC-1 | Bỏ kiểm "không có adapter" (`factory.Find(...)!`) | `TC-FC-05` (kỳ vọng `0003`, thực tế `NullReferenceException`/gọi tiếp) |
| NC-2 | `NotificationSender` trả `Accepted=true` khi chưa có cấu hình | `TC-FC-01` + ca "0 `DaBanGiao`" |
| NC-3 | Bỏ kiểm `IsEnabled`/credential, đăng ký một adapter `fake` **ném nếu bị gọi** | `TC-FC-02/03` (adapter bị gọi ⇒ đỏ) |
| NC-4 | Bỏ phát metric khi `0003` | `TC-MET-01` |
| NC-5 | Thêm `using System.Net.Mail`/`HttpClient` vào `NotificationSender` | `TC-ARCH-02` (quét kiến trúc) |
| NC-6 (khuyến nghị) | Đăng ký **hai** `INotificationSender` (giữ `ChuaCoKenhGuiSender` + thêm `NotificationSender`) | `TC-DI-01` |

### 13.3 Phần BR **chưa phủ được** ở `WP-23` (ghi trung thực, không "xanh giả")

`BR-003` (callback), `BR-004` (state machine `NOT-002`), `BR-005` (delivery event append-only), `BR-006` (UoW + outbox `NOT-002`),
`BR-010` (Zalo token), `BR-012` (quiet hours/opt-out), `BR-013` (chi phí), `BR-014` (GHTK), `BR-015` (OTP), `BR-019/020/021/022/023`
(callback dedupe/opt-out/TTL/cảnh báo/opt-out sync) — **chưa có bảng/adapter/endpoint** ⇒ thuộc `W23-4` chỉ ở phần đã hiện thực, phần còn lại
ghi "chưa kiểm được" (khuôn `docs/tests/NOT-001-testplan.md`). `BR-011` chỉ phủ **một phần**: interface có `CancellationToken`, **0** lời gọi mạng
⇒ "timeout hữu hạn" chưa chứng minh được cho tới khi có adapter thật.

---

## 14. Rủi ro & đường lùi

| # | Rủi ro | Mức | Giảm thiểu / đường lùi |
|---|---|---|---|
| R1 | `EnableDispatch=true` mà kênh chưa cấu hình ⇒ `0003` lặp **mỗi lease** (120 s), log/metric tăng | Trung bình | `EnableDispatch` mặc định `false` (`:43-49`); metric `reason=0003` làm cảnh báo vận hành; sửa cấu hình là hết; `ExpiresAtUtc` chặn vô hạn nếu tin có hạn |
| R2 | Hai hiện thực `INotificationSender` (explicit + `ITransientDependency`) ⇒ resolve phụ thuộc thứ tự | **Cao** nếu bỏ qua | §5.3: **một** hiện thực; `TC-DI-01`; khởi động Host thật |
| R3 | Canonical `NotificationSendRequest` **không** có `CorrelationId` ⇒ log của sender không nối được chuỗi (`XC-005` BR-002) | Trung bình | `AQ4`: thêm `string? CorrelationId = null` **ở cuối** record (additive, không phá caller); nếu từ chối ⇒ ghi nợ NFR-NOT-002-012 |
| R4 | `Accepted=true` mà NCC không trả `ProviderMessageId` ⇒ đánh `Loi`; người vận hành gửi lại tay có thể **gửi trùng thật** | Trung bình | Theo SRS §3 hàng 3/§12 (không đánh dấu thành công); ghi rõ trong log/metric; cảnh báo trong tài liệu vận hành increment sau |
| R5 | Stub config luôn `null` ⇒ không kênh nào gửi được ở `WP-23` | Thấp (đúng thiết kế) | Đây là fail-closed có chủ; có kênh thật khi increment sau thay hiện thực DI (**kèm** mở rộng `FindAsync` — nợ N1) |
| R6 | `SendOutcome.Reason` tự do ⇒ lộ payload NCC/PII vào log | Trung bình | Hợp đồng: token ≤ 64 + sender mask một cửa; test `TC-LOG-01` |
| R7 | Adapter lỡ đăng ký ở Host (không qua module) | Thấp | `TC-ARCH-03`/review; adapter **chỉ** trong `modules/Notification/eCommerce.Notification.Application/Channels/` |
| R8 | Không có bảng ⇒ tenant isolation của cấu hình chưa chứng minh | Thấp | Stub 0 dữ liệu; khi có bảng, integration 2 tenant (increment sau) |

**Đường lùi tổng thể:** (a) `git revert` `W23-2` (0 migration, 0 dữ liệu) + khôi phục **một** dòng DI; (b) `Notification:EnableDispatch=false`;
(c) xoá adapter = xoá **một** class; (d) **không** migration nào đã áp ⇒ không cần `pg_dump`/revert schema.

---

## 15. Câu hỏi mở (`openQuestions` — **không tự quyết**)

| ID | Câu hỏi | Cần | Đề xuất | Vì sao |
|---|---|---|---|---|
| `NOT-002-AQ1` | `WP-23` có tạo bảng `NotificationChannelAccounts` (+ endpoint cấu hình kênh) để kiểm `0003`, hay **0 migration** + cổng config stub? | DEC | **0 migration** — cổng `INotificationChannelConfigProvider` + stub `null`; bảng khi increment đầy đủ sau `R3-6` | Cột/Settings phụ thuộc NCC/Zalo **chưa xác minh** (`D-200`/`D-202`); thêm bảng buộc sửa 2 bất biến test (5 bảng, 114 index) mà chưa có bên dùng thật; lùi 0 dữ liệu |
| `NOT-002-AQ2` | Adapter **email SMTP tổng quát** có thuộc `WP-23` hay hoãn? | DEC | **Hoãn** sang increment sau (email đi trước, `D-200` cho phép không cần `R3-6`) | Hàng `W23-1` chỉ "thiết kế trung lập"; viết adapter thật mở đường gọi mạng, phá bảo đảm "0 kết nối" của `WP-23` và cần thêm test/cấu hình ngoài phạm vi |
| `NOT-002-AQ3` | Shape `SendOutcome`/`NotificationFailureKind`: giữ `ProviderMessageId`/`FailureKind`/`Reason`/`RecipientRefused`, đổi `Recipient`→`RecipientEncrypted`, bỏ `TemplateCode`/`Data`, bỏ `RefreshCredentialAsync`; đúng chưa? | DEC | **Đúng như §4.1**; `RecipientRefused` giữ chỗ `D-207` (0 hành vi); `RefreshCredentialAsync` thêm khi có Zalo (additive) | D-264 bắt canonical không render/không plaintext; method refresh chưa có hiện thực là hợp đồng chết; `RecipientRefused` đã có trong SRS nên giữ để không phải đổi chữ ký lần nữa |
| `NOT-002-AQ4` | Có thêm `CorrelationId` (tuỳ chọn, cuối record) vào canonical `NotificationSendRequest` ở `W23-2` không? | DEC | **Có** — additive, default `null`; job truyền `muc.CorrelationId` | Thiếu nó ⇒ log của sender không có `CorrelationId` (`XC-005` BR-002 / NFR-NOT-002-012); thêm ở cuối không phá caller hiện có |
| `NOT-002-AQ5` | Tên/kiểu hợp đồng **callback** (chỉ thiết kế, **không** mở endpoint): `INotificationCallbackParser.Parse(providerCode, rawBody, headers)` + `INotificationCallbackReceiver`? | DEC | `INotificationCallbackParser` (đúng tên SRS §6.1) cho **parse + DedupKey**; receiver/app service để sau `D-202` | `D-202` cấm endpoint ẩn danh ở GĐ3; chốt **tên** bây giờ để increment sau không phải đổi; chỉ parse là hàm, test được bằng `fake` payload |
| `NOT-002-AQ6` | Xoá `ChuaCoKenhGuiSender` và chuyển test đỏ `D-263` sang `NotificationSender`, hay giữ làm fallback? | DEC | **Xoá/thay**; giữ **kỳ vọng** test ("NÉM + 0 `DaBanGiao`") trên `NotificationSender` | Hai hiện thực `INotificationSender` là rủi ro resolve (R2); hành vi fail-closed vẫn đúng vì `NotificationSender` ném `0003` khi chưa cấu hình |
| `NOT-002-AQ7` | Metric "kênh chưa sẵn sàng": dùng lại `notification_queue_failed_total{reason=0003}` hay thêm counter mới? | DEC | **Dùng lại** (0 registry/`XC-005`/test thay đổi) | ADR-07 buộc thêm metric phải cùng PR registry + `XC-005` §5.1 + test; `WP-23` hẹp, metric mới không cần thiết để vận hành |
| `NOT-002-AQ8` | Đường credential tới adapter: (a) thêm `CredentialEncrypted` (ciphertext) vào `SendCommand`; (b) `SendAsync(SendCommand, NotificationChannelConfig)`; (c) adapter inject `INotificationChannelConfigProvider`? | DEC | **(a)** — sender đọc config **một lần**, chép ciphertext vào lệnh; adapter giải mã **một lần** ở biên cuối | Đóng P1 vòng 1; (b) đổi chữ ký rộng hơn; (c) đọc cấu hình hai lần, có thể lệch `IsEnabled`. `docDeviation #2` đã cập nhật theo (a) |

---

## 16. `docDeviation` (SRS ↔ ARCH)

| # | SRS nói | Thiết kế chốt | Lý do / ai sửa |
|---|---|---|---|
| 1 | §6.1 `INotificationSender.SendAsync(SendRequest)` nhận `Recipient` plaintext + `TemplateCode` + `Data`; `HandleCallbackAsync` **trong cùng** interface | **Canonical WP-22** (`INotificationSender.cs:8-27`): `NotificationSendRequest(TenantId, QueueItemId, IdempotencyKey, ChannelKind, RecipientEncrypted, Subject, Body, Priority)`; **bỏ** `TemplateCode`/`Data`; callback **tách** hợp đồng riêng (`AQ5`) | `D-264`; ADR-06/ADR-09; một interface **không** thể hai định nghĩa. **BA sửa §6.1 cùng PR `WP-23`** (hàng `W23-2`) — không sửa SRS ở hàng này |
| 2 | §6.1 `SendCommand(MessageId, TenantId, Recipient, TemplateCode, Data, Priority string, CorrelationId)`; `INotificationChannelSender` có `ProviderCode`/`ChannelKind`/`SendAsync`/`RefreshCredentialAsync` | §4.1: `SendCommand(TenantId, QueueItemId, IdempotencyKey, ChannelKind, ProviderCode, ChannelAccountId, CredentialEncrypted, RecipientEncrypted, Subject, Body, Priority int, CorrelationId?, Settings)`; adapter khai `RequiredSettingsKeys`; **bỏ** `MessageId` (bảng chưa có) và `TemplateCode`/`Data` | Cùng lý do #1 + cần khoá idempotency đã phát hành + cần `ProviderCode`/`Settings` cho trung lập NCC + cần **một** đường credential ciphertext tới adapter (`AQ8`) |
| 3 | §6.1 `RefreshCredentialAsync(Guid channelAccountId)` | **Bỏ khỏi** `WP-23`; thêm khi adapter Zalo OA vào (additive) | Chưa có kho credential/tài khoản; khai method không hiện thực là hợp đồng chết |
| 4 | §6.1 `SendOutcome.RecipientRefused` (D-207) | **Giữ trường**, `WP-23` **0** hành vi (opt-out ngoài phạm vi) | Không đổi chữ ký lần nữa; increment sau mới xử lý |
| 5 | §11 "Migration mới **CÓ** — `Added_Notification_Module` tạo **6** bảng"; §14.3 DoD 47 TC / 6 bảng | `WP-23` **0 migration**, **0 bảng**; 6 bảng thuộc increment đầy đủ | `WP-23` là phần trung lập (`07-ke-hoach-trien-khai.md:753`); tách increment tránh schema dựa trên thông số NCC chưa xác minh |
| 6 | §6.2 danh sách 11 endpoint `NOT-002` | `WP-23` **0 endpoint**, **0 permission mới** | `D-202` (callback không mở), `D-200` (chưa chọn NCC); endpoint cần bảng/DTO chưa có |
| 7 | §0.1/§14.4/§15.2 F1 ghi `modules/` có **15** module, **0** dòng code miền thông báo | Thực tế N106: **17** module, `Notification` **28** tệp | `D-312` ③ đã giao **`ba`** sửa as-built **trong PR code `WP-23` đầu tiên**; ARCH **không** sửa SRS |
| 8 | §1 E3 + §12: thiếu/hết hạn credential ⇒ `ChoThuLai` + backoff dài | **Mọi** lỗi credential (thiếu trước khi gọi **và** `Unauthenticated` sau khi gọi) **NÉM** `0003`; job catch/lease như fail-closed; **0** `IsTransient` | Một ngữ nghĩa cho mọi lỗi credential; tránh cùng mã `0003` mang hai nghĩa. BA sửa §1 E3/§12 cùng PR `W23-2` |
| 9 | §3 hàng 3: `Accepted=true` thiếu `ProviderMessageId` ⇒ guard sai **NÉM** `0006`; §12 nói `ThatBai` | ARCH **trả** `NotificationSendResult(..., 0006)` để job `DanhDauLoi` (không ném) | Không thể ném sau khi NCC đã nhận (mất dấu tin đã gửi); `Loi` cho người xử lý. BA sửa §3/§12 cùng PR `W23-2` |

**Không lệch — tuân thủ:** `D-200` (0 NuGet NCC, `Settings`/Setting, `fake`, fail-closed `0003`), `D-202` (callback không mở; 5 thông số Zalo
không suy đoán), `D-204` (không dùng/không tạo khoá HMAC production), `D-197` (một module/một enum/một tệp mã lỗi/một lớp khoá/một nhóm quyền),
`D-193` (chi phí kỳ ngoài `WP-23`).

---

## 17. Truy vết quyết định (`D-xx` ↔ mục) & tự kiểm

### 17.1 Truy vết

| `D-xx` | Nội dung | Mục trong tài liệu |
|---|---|---|
| `D-197` | Một module `Notification` (4 project), một `ConfigureNotification`, một `NotificationErrorCodes.cs`, một enum `ChannelKind`, một `NotificationLockKeys`, một nhóm permission | §1.1, §1.4, §2.1, §3.1-3.3, §5.4, §9, §16 |
| `D-200` | Trung lập NCC: `0` NuGet NCC, `0` endpoint/brandname hard-code, `Settings`+Setting, `fake` sender, fail-closed `0003` | Header, §1.2, §2.2 B7, §4.2-4.3, §6, §13, §16 |
| `D-202` | Zalo OA 5 thông số là vùng cấm; callback GĐ3 **không mở**; `Settings` không chứa secret | §1.3, §4.3, §6 (Zalo `znsTemplateCode`), §10, §15 `AQ5`, §16 |
| `D-204` | Khoá HMAC production là việc chủ dự án | §1.3, §10 (không dùng), §16 |
| `D-264` | `INotificationSender` §4.4 ARCH là canonical; BA sửa `NOT-002` §6.1 cùng PR `WP-23` | §1.1, §4.1, §4.4, §15 `AQ4`, §16 #1 |
| `D-312` | Tạm duyệt thiết kế `NOT-002` có hiệu lực trên nhánh `WP-23`; P2 as-built hoãn sang PR code | Header, §1.1, §16 #7 |
| `D-208` (7) | Điều kiện tạm duyệt `NOT-002` đã đạt (sau N105) | Header, §16 #7 |
| `D-263` | Fail-closed mặc định, `EnableDispatch=false`, 0 `DaBanGiao` giả | §1.1, §5.3, §6.2, §15 `AQ6` |
| `D-201` | Nội dung gửi lưu dạng đã mask; một cửa mask | §10 (Subject/Body đã mask), §4.5 (`Reason` mask) |
| `D-199` | GĐ3 chỉ `Text`; dải `0010–0020` thuộc `NOT-001`, `NOT-002` không chiếm `0010+` | §3.2, §3.3 |
| `D-207` | Opt-out là nguồn sự thật duy nhất; `SendOutcome.RecipientRefused` | §4.1 (giữ trường), §4.5 (bỏ qua ở `W23-2`), §16 #4 |
| `D-191` | PII tắt: người nhận mã hoá/băm, 0 plaintext ở biên | §4.4, §10 |
| `D-193` | Chi phí gửi là chi phí kỳ, thuộc `NOT-002` đầy đủ | §1.3, §16 (không lệch) |
| `D-17` | Enum lưu DB chỉ thêm ở cuối | §3.1 (append-only) |
| `D-216` | Một chủ migration; nhánh WP xếp chồng | Header, §8.2 |

### 17.2 Tự kiểm

- **Vòng 2 (28/11/2026) — đã đóng toàn bộ `docreview` vòng 1 (#1–#18):** P1 #1 chốt **một** đường credential ciphertext
  (`NotificationChannelConfig` → `SendCommand.CredentialEncrypted` → adapter; `AQ8`); P2 #2 `Unauthenticated` **NÉM** `0003`
  thống nhất (`docDeviation #8`); P2 #3 tách "**sender** ghi 0" khỏi event `Dispatching`/`LeaseExpired` của job (`:122-124`);
  P2 #4 `NotificationErrorCodes.cs:69-70`; P2 #5 `NotificationMasker.cs:70`; P2 #6 §8.2 thành **phụ lục** + đủ cột
  (`WebhookSecretEncrypted`, `OptOutLastSyncedAtUtc`, `RefreshCredentialEncrypted`, `DailyQuota`/`UsedToday`, `QuietHours*`);
  P2 #7 NND **bắt buộc** (`coding-conventions.md:107`), kỳ vọng **114 → 115**; P2 #8 `TC-44 (NOT-001 SRS)`; P2 #9 mọi mã test
  mới gắn "**TC mới — tạo ở `W23-4`**" + bảng map TC gốc. P3 #10–#18: **6 counter + 1 gauge**; `ChuaCoNoiPhat` chỉ còn promotion;
  hàng 2 `0006` → `Loi` (`docDeviation #9`); §5.3 "Đề xuất (chờ `AQ6`)" + nhánh giữ lớp; §8.2 phụ lục; thêm `D-199`/`D-207`;
  `NotificationPersistenceTests.cs:29-36`; **nợ N1** `FindAsync` mở rộng; `RecipientRefused` bị bỏ qua (§4.5).
- **Đồ thị trước, grep sau:** `codegraph_explore "INotificationSender NotificationSendRequest ChuaCoKenhGuiSender NotificationEnums NotificationErrorCodes"`
  → `INotificationSender.cs:8-27`, `NotificationApplicationModule.cs:21-51`, `NotificationErrorCodes.cs:8-83`, `NotificationEnums.cs:7-97`;
  `codegraph_explore "NotificationEnums ChannelKind NotificationDbContextModelCreatingExtensions ConfigureNotification eCommerceDbContext ConfigureNotification"`
  → `ConfigureNotification.cs:29-…`, `eCommerceDbContext.cs:393`, `DbSets :63-67`; `codegraph_explore "NotificationDispatchWorker BanGiaoAsync NotificationQueueAppender KenhNhan NotificationEvents"`
  → worker `:210-289` (gọi sender `:279`, catch `:281-286`), `KenhNhan :304-310`. Vòng 2 xác nhận thêm:
  `NotificationMasker.CheBiMat → NotificationMasker.cs:70`; `KhoiPhucLeaseAsync` ghi `LeaseExpired` `:122-124`.
- **Grep chỉ dùng cho:** tài liệu (`SRS`/`decisions`/`uy-quyen`/`07`/`bang-phien`), csproj/`Directory.Packages.props` (gói NuGet), `appsettings*.json`
  (không có section `Notification`), và xác nhận **0** `mailkit|sendgrid|twilio|esms` trong module — những chỗ đồ thị **không** phủ (tệp cấu hình/tài liệu).
- **Mọi `file:dòng` có thật** (đã đọc/đo trong N106). Mọi bảng ghi **module chủ = `Notification`**; `WP-23` **0** bảng mới.
- **0** mâu thuẫn nội tại: mọi chỗ nói `WP-23` đều "0 migration/0 endpoint/0 metric mới"; mọi chỗ nói `0003` đều "NÉM, không `DaBanGiao`"
  (kể cả `Unauthenticated`); credential chỉ tồn tại dưới dạng **ciphertext** trong `SendCommand`.
- **0** việc bỏ ngỏ ngoài `AQ1…AQ8` + **nợ N1**; **0** cam kết endpoint/trường NCC; **0** sửa code/SRS/`decisions.md`/`.claude/**`.
- **Rủi ro cao nhất đã nêu:** R2 (hai `INotificationSender`), R3 (`CorrelationId` thiếu) — cả hai có câu hỏi/đề xuất trong §14/§15.
- **Máy (vòng 2):** `kiem-link-tai-lieu.ps1` **PASS** · `check:mermaid` **214 khối / 0 lỗi** · `git status` chỉ tệp `-arch`.
