# Triển khai `NOT-001` — template thông báo + hàng đợi gửi

> Phiên `N87` (hàng `W22-5`, F6 đóng `WP-22`) · nhánh `feat/gd3-wp22-thong-bao` → `tich-hop/gd3` · 27/11/2026.
> Quyết định liên quan: `D-197`, `D-255`, `D-259`, `D-263`, `D-266`, `D-268`, `D-269`.
> Tài liệu này chỉ mô tả **vận hành**; thiết kế nằm ở `docs/features/notifications/NOT-001-arch.md`, đặc tả ở
> `docs/features/notifications/not-001-template-hang-doi-thong-bao.md`.

## 1. Phạm vi

- Nhánh/PR: `feat/gd3-wp22-thong-bao` → `tich-hop/gd3` (chuỗi GĐ3, `D-216` phương án B); production nhận qua PR
  tích hợp `tich-hop/gd3` → `main` — việc merge `main` là **vùng cấm** của agent, cần **biên bản người ký** (`P-21`).
- Module mới `modules/Notification/` gồm **4** project (`D-197`): `Domain` · `Application.Contracts` · `Application` ·
  `EntityFrameworkCore`. Đã nối vào đồ thị module + `eCommerceDbContext` ở
  `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs` và `eCommerceDbContext.cs` (5 `DbSet`).
- Hợp đồng API: **9** op HTTP `/api/v1/notifications/*` trên **3** controller tường minh
  (`src/eCommerce.HttpApi/Controllers/Notification{Templates,Queue,Batches}Controller.cs`), `[Authorize]` **cấp class**
  (ẩn danh ⇒ **401**). **Không** có `[AllowAnonymous]` trên miền này.
- **1** migration expand-only:
  `src/eCommerce.EntityFrameworkCore/Migrations/20261126132231_Added_Notification_Template_Queue.cs` — tạo **5** bảng
  `NotificationTemplates` · `NotificationTemplateVersions` · `NotificationBatches` · `NotificationQueueItems` ·
  `NotificationQueueEvents`; **7** unique index dùng **SQL thô** có `NULLS NOT DISTINCT`; **không** ALTER/DROP bảng cũ.
- Seed **idempotent 3 template hệ thống** (`TenantId = NULL`, locale `vi`): `ops-alert`, `order-confirmation`,
  `order-shipped` — GUID cố định `0193a000-0000-7000-8000-000000000001/2/3`
  (`modules/Notification/eCommerce.Notification.Domain/Seed/NotificationDataSeedContributor.cs`). Seeder chạy trong
  `DbMigrator` (bước `IDataSeeder.SeedAsync`), kiểm **từng mã** nên chạy lại không nhân bản.
- Đã áp DB dev ở `N84` **sau** `pg_dump`: tệp `artifacts/pg-dump/N84-W22-2.dump` (**457.042 byte**). `W22-3`/`W22-4`
  **0** migration.
- **Không** đổi `Directory.Packages.props` · `pnpm-lock.yaml`. **Có** đổi `eCommerce.slnx` (thêm 4 project
  `modules/Notification/*` — thay đổi cộng thêm, chỉ một luồng chạm; xem `K-10`/§7).
- Không có secret mới. Liên kết: review `docs/reviews/NOT-001-review.md` · testplan
  `docs/tests/NOT-001-testplan.md` · handover `docs/nhanh/handover-n86.md`.

## 2. Biến môi trường

Khoá đọc qua `IConfiguration` (`NotificationSettingKeys`,
`modules/Notification/eCommerce.Notification.Application/Rendering/NotificationSettingKeys.cs`) — **KHÔNG** phải
`SettingDefinition`.

| Tên | Kiểu | Mặc định dev | Khuyến nghị production | Nơi đọc | Ghi chú |
|---|---|---|---|---|---|
| `Notification:EnableDispatch` | bool (`GetValue(defaultValue: false)`) | **`false`** | **`false`** | `NotificationDispatchWorker.DoWorkAsync` | **Công tắc bàn giao.** `false` ⇒ worker chỉ log `Warning` `[NOT-DISPATCH] … =false — KHÔNG bàn giao` rồi thoát. **Giữ `false` tới khi `NOT-002`/WP-23 có sender thật.** |
| `Notification:Dispatch:LeaseSeconds` | int (`GetValue(defaultValue: 120)`) | `120` | `120` | `NotificationDispatchWorker` | Thời hạn lease `DangBanGiao`; lease hết ⇒ pha (A) đưa mục về `ChoGui`. Đọc mỗi nhịp. |
| `Notification:Batch:MaxRecipients` | int (`GetValue(defaultValue: 50_000)`) | `50000` | `50000` | `NotificationBatchAppService.CreateBatchAsync` | Vượt ngưỡng ⇒ **400** (`AbpValidationException`, `D-259`); `<= 0`/không đọc được ⇒ rơi về `50000`. Đọc lúc tạo lô. |
| `Notification:Dispatch:PeriodSeconds` | int (khai báo, mặc định `10`) | `10` | `10` | ⚠️ **hiện CHƯA được đọc** | Hằng số có khai báo, nhưng `NotificationDispatchWorker` **hard-code** `Timer.Period = 10_000` ms. Đổi khoá này **không** đổi nhịp hôm nay — nợ code (xem §7). |

- **Không** có secret mới. Cả 4 khoá là cấu hình thường; đặt qua `.env`/secret store hoặc `appsettings` của Host,
  **không** commit giá trị production vào repo (O1).
- ⚠️ **Sender hiện tại là fail-closed**: `NotificationApplicationModule` đăng ký `INotificationSender` →
  `ChuaCoKenhGuiSender`, mà `SendAsync` **NÉM** `InvalidOperationException` (không trả "thành công ma", `D-263`).
  Nếu ai đó bật `EnableDispatch=true` khi **chưa** có sender thật: **0** tin được gửi (sender luôn ném), nhưng worker
  log `Warning` mỗi mục + mục quay vòng `DangBanGiao → ChoGui` theo lease ⇒ nhiễu log/lock vô ích. Vì vậy **production
  giữ `false`** cho tới `NOT-002`.

## 3. Thứ tự triển khai

1. **Backup DB production trước khi chạm schema** (K-01/M9): `pg_dump -Fc` ra tệp **ngoài repo** (ví dụ
   `artifacts/pg-dump/<PROD>-not001.dump`), ghi đường dẫn + kích thước tệp vào biên bản triển khai.
2. **Merge code BE trước** (`feat/gd3-wp22-thong-bao` → `tich-hop/gd3` → `main`). `eCommerce.slnx` đổi cộng thêm 4
   project — đảm bảo **không** luồng nào khác đang sửa cùng tệp (`K-10`).
3. **Build binary mới**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng **0 error / 9 warning**, 0 warning mới).
4. **Áp migration bằng binary MỚI qua `DbMigrator`** (M5) — **KHÔNG** `dotnet ef database update` (M6, `D-75`):
   `dotnet run --project src/eCommerce.DbMigrator`. Bước này cũng chạy seed 3 template hệ thống.
5. **Kiểm DB THẬT** (không tin `exit=0`) — chạy các truy vấn ở §5.3.
6. **Deploy BE (API)** sau khi DB xanh.
7. **Deploy FE** (`apps/admin-web`) **sau khi** API đã xanh (merge BE trước FE).
8. **Giữ `Notification:EnableDispatch = false`** — xác nhận log có dòng `[NOT-DISPATCH] … =false` và **0** dòng mới
   trong `NotificationQueueItems`/`NotificationQueueEvents` do worker sinh ra.
9. **Kiểm sau triển khai** (§5.2) rồi mới coi là xong.

## 4. Đường lùi (bắt buộc)

| Thay đổi / sự cố | Đường lùi | Ghi chú |
|---|---|---|
| **Gửi thật bị sai** (sau này, khi đã bật dispatch) | Đặt `Notification:EnableDispatch=false` rồi khởi động lại Host ⇒ worker chỉ log và thoát; **0** thay đổi dữ liệu nghiệp vụ, **0** lần gọi sender. | Hôm nay sender còn fail-closed (ném) ⇒ kể cả bật nhầm cũng **không** gửi ra người thật (`D-263`). |
| **Cấu hình lease/size sai** | Sửa `Notification:Dispatch:LeaseSeconds` / `Notification:Batch:MaxRecipients` trong `.env`/`appsettings` rồi restart. **0** code, **0** migration, **0** dữ liệu. | `MaxRecipients <= 0` tự rơi về `50000`. **`PeriodSeconds` KHÔNG có tác dụng** (worker hard-code 10 s) — đổi nhịp phải sửa code (§7). |
| **Cần bỏ hẳn mã `NOT-001`** | `git revert` **đúng** commit WP-22: `7f23726` (BE + migration) · `bc8f898` (v1 HTTP) · `bcd82e0` (FE) · `6a940d5` (SRS v0.7) · `93ea079` (QA + mask `D-269`). | Revert từng commit (commit `24fd918` chỉ là handover, bỏ qua được). Revert `7f23726` gỡ luôn 4 project khỏi `eCommerce.slnx`. **KHÔNG** revert migration đã áp (dòng dưới). |
| **Schema 5 bảng `Notification*`** | **KHÔNG** chạy `Down`/`DROP`/xoá bảng ở production. Để 5 bảng lại. | Migration **expand-only**; 5 bảng mới chỉ chứa template/lô/hàng đợi/event của module — **không** phải dữ liệu nghiệp vụ đang phục vụ ⇒ **0** dòng nghiệp vụ bị mất. |
| **Revert file migration khỏi assembly** trong khi DB **đã** áp | Dòng `__EFMigrationsHistory` (`20261126132231_Added_Notification_Template_Queue`) thừa **vô hại** — EF bỏ qua migration không còn trong assembly. **Không** tự xoá dòng lịch sử. | Chạy lại `dotnet run --project src/eCommerce.DbMigrator` để xác nhận **không** áp/drop gì và `exit=0`. |
| **Seed 3 template hệ thống** | Idempotent theo `(Code, ChannelKind, Locale)`; chạy lại không nhân bản. Không cần lùi riêng. | Nếu template hệ thống **bị sửa tay** sai: **không** sửa tay DB; xoá/khôi phục qua UI có quyền `Templates.Publish` hoặc khôi phục từ `pg_dump`. |

- Khôi phục DB từ `pg_dump`: `pg_restore -d <DB> artifacts/pg-dump/<PROD>-not001.dump` **chỉ** khi cần lùi toàn bộ
  (mất dữ liệu phát sinh sau dump) — ưu tiên các đường lùi cấp trên trước.
- Sau revert, kiểm Host khởi động được và `GET /health-status` trả **200** trước khi coi là hoàn tất.

## 5. Cổng (số đo thật)

### 5.1. Cổng đã đo trong WP-22

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (`N84` + `N86` đóng phiên) | `exit 0` · **0 error / 9 warning** (0 warning mới) — `docs/nhanh/handover-n86.md` §4. |
| `dotnet test test/eCommerce.Tests` (`N86`) | `exit 0` · **2283/2283** (+32; không xoá/skip ca nào). |
| `dotnet test test/eCommerce.IntegrationTests` (`N86`, Docker) | `exit 0` · **110/110** (+4 `PostgreSqlNotificationTests`). |
| `tools/agents/check-feature-doc.ps1 -Path <SRS> -WP14` | `exit 0` · **DAT** · 16/16 heading · **25/25 BR**. |
| `tools/agents/check-agent-artifact.ps1 -Kind review` | `exit 0` · **DAT** (`VERDICT: PASS`, **P1 = 0**; P2=2, P3=3). |
| `tools/agents/check-agent-artifact.ps1 -Kind docreview` | `exit 0` · **DAT** (`DOC-OK` 2 vòng, **P1 = P2 = P3 = 0**). |
| `tools/agents/check-agent-artifact.ps1 -Kind testplan` | `exit 0` · **DAT** · BR 25/25. |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | `exit 0` · **PASS** · 958 link · 1648 đường dẫn · 0 chết. |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **ORCH đo ở cuối phiên `N87`; số đo chốt ghi ở `docs/nhanh/handover-n87.md`.** Agent `ops` **không** tự chạy (tránh hết bộ nhớ, bài học `N69`). |

### 5.2. Kiểm sau triển khai (thủ công, trên production)

| Kiểm | Kỳ vọng |
|---|---|
| `GET /health-status` (Host) · `GET /api/health` (BFF `apps/admin-web`) | **`200`** |
| **9** route `/api/v1/notifications/*` **không** Bearer token | **`401`** cho cả 9 (xem bảng route bên dưới) |
| Gọi có token **đủ** quyền | `200`/`201` theo op |
| Gọi có token **thiếu** quyền | **`403`** (nếu môi trường đã expose `/api/identity/**`; nếu chưa ⇒ đo ở staging, xem `D-252`) |
| Cờ bàn giao | Log Host có `[NOT-DISPATCH] … \`Notification:EnableDispatch\`=false — KHÔNG bàn giao`; **0** dòng mới `NotificationQueueItems`/`NotificationQueueEvents` |
| Seed template | `SELECT count(*) FROM "NotificationTemplates" WHERE "TenantId" IS NULL;` → **3** |
| `NOT-002` chưa có | `Notification:EnableDispatch` **vẫn `false`** |

Bảng 9 op → quyền (nguồn: 3 controller + `eCommerceConsts.Permissions.Notifications`):

| # | Method · route | Quyền |
|---|---|---|
| 1 | `GET /api/v1/notifications/templates` | `eCommerce.Notification.Templates.View` (class) |
| 2 | `POST /api/v1/notifications/templates` | `eCommerce.Notification.Templates.Manage` |
| 3 | `POST /api/v1/notifications/templates/{id}/versions` | `eCommerce.Notification.Templates.Publish` |
| 4 | `POST /api/v1/notifications/templates/{id}/archive` | `eCommerce.Notification.Templates.Publish` |
| 5 | `POST /api/v1/notifications/templates/preview` | `eCommerce.Notification.Templates.View` |
| 6 | `GET /api/v1/notifications/queue` | `eCommerce.Notification.Queue.View` (class) |
| 7 | `GET /api/v1/notifications/queue/{id}/events` | `eCommerce.Notification.Queue.View` (class) |
| 8 | `POST /api/v1/notifications/batches` | `eCommerce.Notification.Batches.Create` (class) |
| 9 | `POST /api/v1/notifications/batches/{id}/approve` | `eCommerce.Notification.Batches.Create` (class) + `eCommerce.Notification.Batches.Approve` |

- **9 permission** khai **2 nơi** (`eCommerceConsts.Permissions.Notifications` +
  `eCommercePermissionDefinitionProvider`). Thiếu một nơi ⇒ **403 cho mọi người kể cả admin** — đã có test
  `BatBienPhanQuyenTests.TC02b`. Kiểm sau deploy: admin gọi được, user thường thiếu quyền nhận `403`.

### 5.3. Kiểm DB thật (chạy tay, không tin `exit=0`)

```sql
-- (1) Migration history: đúng 1 dòng
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%Added_Notification_Template_Queue%';
-- kỳ vọng: 20261126132231_Added_Notification_Template_Queue

-- (2) Đủ 5 bảng
SELECT tablename FROM pg_tables
WHERE tablename IN ('NotificationTemplates','NotificationTemplateVersions',
                    'NotificationBatches','NotificationQueueItems','NotificationQueueEvents');
-- kỳ vọng: 5 dòng

-- (3) Đủ 7 unique index có "NULLS NOT DISTINCT"
SELECT indexname, indexdef FROM pg_indexes
WHERE indexname IN (
  'IX_NotificationTemplates_Tenant_Code_Channel_Locale',
  'IX_NotificationTemplateVersions_Tenant_Template_Version',
  'IX_NotificationTemplateVersions_Tenant_Template_Hash',
  'IX_NotificationBatches_Tenant_IdempotencyKey',
  'IX_NotificationBatches_Tenant_Code',
  'IX_NotificationQueueItems_Tenant_IdempotencyKey',
  'IX_NotificationQueueItems_Tenant_Recipient_Fingerprint_Bucket')
  AND indexdef LIKE '%NULLS NOT DISTINCT%';
-- kỳ vọng: 7 dòng. Thiếu cụm "NULLS NOT DISTINCT" = ràng buộc mất tác dụng im lặng (M7).

-- (4) Seed 3 template hệ thống
SELECT "Code","TenantId","Locale","IsSystem" FROM "NotificationTemplates"
WHERE "TenantId" IS NULL;
-- kỳ vọng: 3 dòng (ops-alert, order-confirmation, order-shipped), Locale='vi', IsSystem=true
```

> Không có `ALTER`/`DROP` bảng cũ trong `Up()` — đã đọc tệp migration. `Down()` chỉ drop 5 bảng mới; **không** chạy
> `Down` ở production (§4).

### 5.4. Mã lỗi miền `Notification` (`NotificationErrorCodes`)

`eCommerce:Notification:0010` khoá xung đột · `0011` chuyển trạng thái không hợp lệ · `0012` template chưa sẵn sàng ·
`0013` thiếu trường bắt buộc · `0014` đã bàn giao rồi · `0015` placeholder ngoài whitelist · `0016` phiên bản bất biến ·
`0017` không tìm thấy · `0018` lô chưa duyệt · `0019` bị khuất cửa sổ · `0020` body format không hỗ trợ.
(Nhánh `40x`/`409` theo framework: `AbpValidationException` cho `MaxRecipients` vượt ⇒ **400**; `BusinessException`
⇒ `403`/`409` theo mã.)

## 6. Khi hỏng — ai gọi

- **Migration / 5 bảng / 7 index `NULLS NOT DISTINCT` sai, `DbMigrator` hỏng**: gọi **`ops`** (chủ vận hành DB, chạy
  `DbMigrator` bằng binary mới). **Không** dùng `dotnet ef database update`.
- **Bật `EnableDispatch` mà hàng đợi bị kẹt `DangBanGiao` / log `Sender ném` mỗi nhịp**: đặt lại `EnableDispatch=false`
  (việc vận hành, **`ops`**); nếu lease không khôi phục ⇒ gọi **`be`** (worker).
- **Template/publish/placeholder (`0012`/`0015`/`0016`) sai**: gọi **`be`** (domain `NotificationTemplate`/renderer).
- **Lô gửi: người tạo tự duyệt, `0011`/`0018` sai nhánh, `MaxRecipients` không chặn**: gọi **`be`**
  (`NotificationBatchAppService`, `D-259`).
- **`401`/`403`**: `401` khi thiếu token là **đúng thiết kế**. `403` khi thiếu permission ⇒ kiểm 9 quyền đã khai **2
  nơi** chưa; nếu vẫn sai gọi **`be`**.
- **Hợp đồng `/api/v1/notifications/*` đổi hình dạng**: gọi **`arch`** + **`fe`** (FE/BFF phụ thuộc `schema.d.ts`).
- **Sai lệch mask đang gửi đi (`D-269` — P2)**: gọi **`decider`** + **`be`**; **không** tự nới mask hay đổi BR-010.
- **Không chắc về secret/NCC/production**: báo **ORCH** để mở `blocked`, không tự xử.

## 7. Nợ & rủi ro đã biết (theo dõi trước go-live)

| # | Mức | Nợ / rủi ro | Chặn gì | Chủ |
|---|---|---|---|---|
| 1 | 🟠 **P2** (`REV`, `D-269`) | Masker chạy **trên chính body gửi đi**: `DispatchWorker` đọc `RenderedBodyMasked` để bàn giao ⇒ false-positive có thể **méo tin thật** (PAN che mã đơn/số TK, `code=` che voucher, hotline/email shop). | **Phải chốt** "gửi bản đã che" vs "gửi bản gốc + chỉ che khi hiển thị" **TRƯỚC khi bật `EnableDispatch`** ở WP-23/`NOT-002` (`D-269` ⑤). | `decider` + `be` |
| 2 | 🟡 | **15** mục "chưa kiểm được" ở `docs/tests/NOT-001-testplan.md` §4 (thiếu harness host ABP + `WebApplicationFactory`); **`NC-2`** (2 job song song) và **`NC-3`** (đua `23505` ⇒ `0019`) **chưa chạy**. | Không chặn `-DayDu` (ghi nhận trung thực), nhưng là điều kiện tin cậy **trước khi bật gửi thật**. | `qa` |
| 3 | 🟡 | Server **chưa lọc** `Purpose = Marketing` cho lô (`GetTemplatesForBatch` **chưa có**); FE đang lọc client (`D-268`). | Lô gửi hàng loạt dựa vào FE để loại template Marketing. Bật gửi thật khi filter server chưa có ⇒ có thể gửi marketing nhầm. | `be` |
| 4 | 🟡 | `Notification:Dispatch:PeriodSeconds` **khai báo nhưng chưa được đọc** — worker hard-code `Timer.Period = 10_000`. | Đổi khoá không đổi nhịp; cần sửa code nếu muốn cấu hình nhịp. | `be` |
| 5 | 🟡 | Nợ **môi trường FE**: `pnpm build` lỗi `EPERM symlink` trên Windows; `pnpm lint` **không có script**. | Cần xác nhận trên **CI** (Linux) trước go-live FE. | `ops`/người |
| 6 | 🔴 | Merge `main` cần **biên bản người ký** (`P-21`); `tich-hop/gd3` chỉ nhận ở `W22-5` (G6). | Agent **không** merge `main`, **không** đổi branch protection. | chủ dự án |
| 7 | 🟡 | `eCommerce.slnx` bị đổi trong WP-22 (thêm 4 project Notification). | Chỉ một luồng được sửa `slnx`/`Directory.Packages.props`/`pnpm-lock.yaml` (`K-10`); merge xếp chồng phải rebase nếu luồng khác cũng chạm. | `ops` |
