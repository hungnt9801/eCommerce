# Triển khai `NOT-002` — kênh gửi trung lập nhà cung cấp (hợp đồng adapter + fail-closed `0003`)

> Phiên `N110` (hàng `W23-5`, F6 đóng `WP-23`) · nhánh `feat/gd3-wp23-kenh-gui` → `tich-hop/gd3` · 28/11/2026.
> Quyết định liên quan: `D-263` (cờ bàn giao mặc định **`false`** + sender fail-closed), `D-313`/`D-314` (**0** bảng · **0** migration ·
> **0** `DbSet` · **0** endpoint), `D-315` (`CorrelationId` additive + dùng lại metric), `D-316` (WP-23 **không** có phần FE),
> `D-317` (hoãn ca "Host boot" bằng `WebApplicationFactory`).
> Tài liệu này chỉ mô tả **vận hành**; thiết kế ở [NOT-002-arch.md](../features/notifications/NOT-002-arch.md), đặc tả ở
> [SRS `NOT-002`](../features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md) (v0.7). Khuôn cùng module:
> [NOT-001-deploy.md](./NOT-001-deploy.md).

## 1. Phạm vi

- Nhánh/PR: `feat/gd3-wp23-kenh-gui` → `tich-hop/gd3` (chuỗi GĐ3, `D-216` phương án B); production nhận qua PR tích hợp
  `tich-hop/gd3` → `main` — việc merge `main` là **vùng cấm** của agent, cần **biên bản người ký** (`P-21`, `D-71`).
- `WP-23` là **hợp đồng adapter + `fake` sender + fail-closed**, **KHÔNG** có kênh gửi thật. Phạm vi đã chốt (`D-314`):
  **0 migration · 0 bảng · 0 `DbSet` · 0 endpoint · 0 DTO · 0 màn hình · 0 permission mới · 0 gói NuGet NCC**.
  Đây là hàng `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-23 ("thiết kế trung lập").
- Cái **thực sự** vào production (thay đổi cộng thêm trong `modules/Notification/**` + **1** dòng map ở Host):
  - Hợp đồng kênh trung lập NCC: `INotificationChannelSender` · `SendCommand` (**13** trường, `CredentialEncrypted` **chỉ ciphertext**) ·
    `SendOutcome` (**5** trường) · `INotificationChannelFactory` · `NotificationChannelConfig` · `INotificationChannelConfigProvider` —
    `modules/Notification/eCommerce.Notification.Application.Contracts/Sending/Channels.cs`.
  - `NotificationFailureKind` (5 giá trị, append-only) và mã lỗi dải `0001–0009` dùng ở WP-23: `0002` · `0003` · `0006` +
    `NotificationConsts.ProviderCodeToiDa = 32` — `modules/Notification/eCommerce.Notification.Domain/NotificationErrorCodes.cs`.
  - `NotificationChannelFactory` (trùng khoá ⇒ NÉM; `Find` **không** fallback) · `ChuaCauHinhKenhProvider` (**luôn** trả `null`) ·
    `NotificationSender` — hiện thực **DUY NHẤT** của `INotificationSender`
    (`modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs`).
  - Xoá `ChuaCoKenhGuiSender` cùng **2** đường đăng ký cũ; `NotificationApplicationModule` bỏ `ConfigureServices`.
  - Worker truyền **1** dòng `muc.CorrelationId` vào `NotificationSendRequest`
    (`modules/Notification/eCommerce.Notification.Application/Dispatch/NotificationDispatchWorker.cs:268-271`).
  - Host map `0003` → HTTP **503** (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:443-444`) — không map thì ABP trả `403`
    và client hiểu sai thành "không có quyền".
- **Không** đổi `eCommerce.slnx` · `Directory.Packages.props` · `pnpm-lock.yaml`; **0** tệp `apps/admin-web/**`,
  **0** diff `schema.d.ts` (`D-316`); **0** secret, **0** kết nối mạng ra NCC.
- Bằng chứng: review [NOT-002-review.md](../reviews/NOT-002-review.md) `VERDICT: PASS` · **P1 = 0** (1 P2 retry storm, 8 P3) ·
  testplan [NOT-002-testplan.md](../tests/NOT-002-testplan.md) phủ **24/24 BR** · handover [handover-n109.md](../nhanh/handover-n109.md).

## 2. Biến môi trường / cờ

Khoá đọc qua `IConfiguration` (`NotificationSettingKeys`,
`modules/Notification/eCommerce.Notification.Application/Rendering/NotificationSettingKeys.cs`) — **KHÔNG** phải `SettingDefinition`.
`WP-23` **không thêm khoá mới**; bảng dưới là các khoá module đang đọc (một số thuộc `NOT-001`, giữ nguyên).

| Tên | Kiểu | Mặc định dev | Khuyến nghị production | Nơi đọc | Ghi chú |
|---|---|---|---|---|---|
| `Notification:EnableDispatch` | bool (`GetValue(defaultValue: false)`) | **`false`** | **`false`** — giữ tới **increment đầy đủ `NOT-002` sau `R3-6`** | `NotificationDispatchWorker.DoWorkAsync` (`:42-48`) | **Công tắc bàn giao.** `false` ⇒ worker log `Warning` `[NOT-DISPATCH] … =false` rồi **return TRƯỚC** `GetRequiredService<INotificationSender>()` ⇒ **0** lần gọi sender, **0** ghi DB. |
| `Notification:Dispatch:LeaseSeconds` | int (`GetValue(defaultValue: 120)`) | `120` | `120` | `NotificationDispatchWorker` | Thời hạn lease `DangBanGiao`; lease hết ⇒ đưa mục về `ChoGui`. **Khi cờ bật mà chưa có kênh**, đây chính là chu kỳ lặp `0003` (xem cảnh báo dưới). |
| `Notification:Dispatch:PeriodSeconds` | int (khai báo, mặc định `10`) | `10` | `10` | ⚠️ **hiện CHƯA được đọc** | `NotificationDispatchWorker` **hard-code** `Timer.Period = 10_000` ms (`:32`). Đổi khoá này **không** đổi nhịp hôm nay — nợ code (xem §7). |
| `Notification:Batch:MaxRecipients` | int (mặc định `50_000`) | `50000` | `50000` | `NotificationBatchAppService.CreateBatchAsync` | Thuộc `NOT-001` (`D-259`), **WP-23 không đổi**; liệt kê cho đủ bộ khoá module. |

- `appsettings*.json` của Host **chưa có** section `Notification` (đo N106) ⇒ `GetValue(..., false)` trả **`false`** từ mặc định code.
  Nếu vận hành thêm section, **tuyệt đối không** đặt `EnableDispatch = true` trước increment đầy đủ.
- **Không có secret mới.** `WP-23` **không** đọc credential kênh, **không** đọc/tạo khoá HMAC production (`D-204`), **không** gọi
  API NCC. Các khoá trên là cấu hình thường: đặt qua `.env`/secret store hoặc `appsettings` của Host, **không** commit giá trị
  production vào repo (O1).
- ⚠️ **Sender thật là fail-closed `0003`**: `ChuaCauHinhKenhProvider.FindAsync` **luôn** trả `null` ⇒ `NotificationSender` **NÉM**
  `BusinessException("eCommerce:Notification:0003")` **TRƯỚC mọi lời gọi mạng**, **không** trả "thành công ma" (`D-263`), **không**
  đánh dấu `DaBanGiao`. Đây là hành vi **đúng** ở WP-23 (chưa chọn NCC), không phải lỗi.
- 🚨 **Cảnh báo retry storm (P2 #1 của REV):** nếu ai đó bật `EnableDispatch=true` khi **chưa** cấu hình kênh, worker **catch**
  exception ⇒ mục ở lại `DangBanGiao` ⇒ lease (`120 s`) đưa về `ChoGui` ⇒ mỗi vòng **NÉM** `0003` + **1** dòng `Warning`
  `[NOT-CHANNEL] … ErrorCode=eCommerce:Notification:0003` + **1** lần tăng `notification_queue_failed_total{channel, reason=…0003}`
  cho **mỗi** mục. Hàng đợi **không bao giờ cạn**, log/metric tăng vô hạn, **không** có trần thử/DLQ bảo vệ. Vì vậy production
  **giữ `false`** cho tới khi increment đầy đủ `NOT-002` bổ sung trần thử + DLQ + cảnh báo khởi động.

## 3. Thứ tự triển khai

1. **Merge code BE trước** (`feat/gd3-wp23-kenh-gui` → `tich-hop/gd3`, chỉ fast-forward ở `W23-5` sau khi G6 xanh; PR
   `tich-hop/gd3` → `main` do **người** ký). `WP-23` **không** đổi `eCommerce.slnx`/lock file nên không vướng `K-10`; vẫn phải
   rebase nếu luồng khác vừa chạm cùng tệp.
2. **Build binary mới**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng **0 error / 9 warning**, 0 warning mới).
3. **0 bước migration** (`D-314`): **không** có schema mới ⇒ **không** cần `pg_dump`, **không** cần backfill, **không** chạy
   `Down`/`DROP`. Chạy `dotnet run --project src/eCommerce.DbMigrator` là **no-op** (0 dòng mới trong `__EFMigrationsHistory`);
   không bắt buộc, và **cấm** `dotnet ef database update` (`M6`, `D-75`).
4. **Deploy Host (API)** bằng binary mới. `WP-23` đổi **biên DI** (xoá `ChuaCoKenhGuiSender`, còn **đúng một**
   `INotificationSender` = `NotificationSender`) ⇒ phải **khởi động Host thật** và xác nhận `/health-status` = `200`. Cổng
   `-DayDu` đã chứng minh **Host boot** ở `N109`; `TC-DI-02` chứng minh **resolve** (`D-317`) — hai phép đo bù nhau.
5. **Deploy FE**: **không có bước này** — `WP-23` **0** màn hình/endpoint/DTO (`D-316`); kỳ vọng `pnpm gen:api` **không** đổi
   `schema.d.ts` (**82/82** path).
6. **Giữ `Notification:EnableDispatch = false`**, xác nhận log có `[NOT-DISPATCH] … =false` và **0** dòng mới trong
   `NotificationQueueEvents` do worker sinh ra.
7. **Kiểm sau triển khai** (§5.2) rồi mới coi là xong. Cổng `-DayDu` chính thức (G6) do **ORCH** chạy cuối phiên `N110`,
   **không** chạy song song build/test khác (bài học `N69`).

## 4. Đường lùi (bắt buộc)

| Thay đổi / sự cố | Đường lùi | Ghi chú |
|---|---|---|
| **Gửi thật bị sai** (sau này, khi đã có kênh) | Đặt `Notification:EnableDispatch=false` rồi khởi động lại Host ⇒ worker chỉ log và thoát; **0** lần gọi sender, **0** thay đổi dữ liệu nghiệp vụ. | Hôm nay sender còn fail-closed (NÉM `0003`) ⇒ kể cả bật nhầm cờ cũng **không** gửi ra người thật (`D-263`). |
| **Bật cờ nhầm gây retry storm** (`0003` lặp mỗi lease) | Đặt `EnableDispatch=false` + restart; mục `DangBanGiao` được lease đưa về `ChoGui`, **0** `DaBanGiao` giả. | Việc vận hành, **`ops`** làm ngay; nếu lease không khôi phục ⇒ gọi **`be`**. |
| **Cần bỏ hẳn mã `WP-23`** | `git revert` **đúng** cụm commit WP-23. Tại thời điểm viết: `git revert --no-commit 88d5eb2^..a940be4` (**8** commit: `88d5eb2` doc-gate · `df6f33e` arch · `99434d7`/`6cd8128` BE (`W23-2`) · `bc9f508` handover · `4df0191` no-FE (`W23-3`) · `199a59b` testplan · `a940be4` handover). Nếu `N110` thêm commit đóng phiên, dùng `88d5eb2^..<HEAD mới>`. | Revert `6cd8128` gỡ hợp đồng/sender và **khôi phục** `ChuaCoKenhGuiSender` + 2 dòng DI cũ. **Sau revert phải kiểm đúng MỘT** hiện thực `INotificationSender` — revert từng phần có thể để lại **hai** hiện thực (rủi ro "last wins" im lặng, arch §5.3). |
| **Schema** | **Không có gì để lùi** — `WP-23` **0** migration/0 bảng/0 `DbSet` (`D-314`), `git diff … -- '**/migrations/**'` rỗng. **Không** chạy `Down`/`DROP`. | 5 bảng `Notification*` của `NOT-001` **không bị chạm**. |
| **Khôi phục DB** | **Không cần** — không có migration nào được áp. `pg_restore` chỉ khi lùi toàn bộ vì lý do khác, và sẽ **mất** dữ liệu phát sinh sau dump. | Không có `pg_dump` riêng cho `WP-23` (đúng: 0 schema). |
| **Seed** | Không đổi: 3 template hệ thống của `NOT-001` (idempotent). Không cần lùi riêng. | Nếu template bị sửa tay sai: sửa qua UI có quyền, **không** sửa tay DB. |
| **Map `0003` → 503** | Revert 1 dòng ở `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:443-444`; lỗi quay về `403`. | Chỉ ảnh hưởng cách client đọc lỗi; `WP-23` **0** endpoint nên hôm nay không có client nào gọi. |
| **Hợp đồng adapter đổi hình dạng** | Revert `6cd8128`; FE **không** phụ thuộc (0 diff `schema.d.ts`). | Nếu increment sau đã dùng `SendCommand`/`SendOutcome`, revert phải đi cùng revert increment đó. |

- Sau revert, kiểm Host khởi động được và `GET /health-status` trả **200** trước khi coi là hoàn tất.
- **Không** cần `pg_dump` trước/sau bất kỳ bước lùi nào ở `WP-23` (0 schema, 0 dữ liệu do mã này sinh).

## 5. Cổng (số đo thật)

### 5.1. Cổng đã đo trong WP-23

> Vai `ops` **không** tự chạy lại build/test/`-DayDu` trong hàng `W23-5` (tránh chạy song song — bài học `N69`); số dưới đây lấy từ
> handover/testplan của phiên đo, **ORCH** chạy lại cổng G6 chính thức cuối `N110` và ghi số chốt ở handover `N110`.

| Lệnh | Kết quả | Nguồn |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | `exit 0` · **0 error / 9 warning** (0 warning mới) | handover N109 §1 |
| `dotnet test test/eCommerce.Tests` | `exit 0` · **2650/2650** pass (**+1** so với 2649; không xoá/skip ca nào) | handover N109 §1 |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | `exit 0` · **162/162** pass (**0** ca mới — 0 schema) | testplan §5 |
| `dotnet test … --filter "FullyQualifiedName~eCommerce.Tests.Notification"` | **144/144** pass | testplan §5 |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | `exit 0` · **20/20 bước ĐẠT** · **888,2 s** — log `artifacts/DayDu-N109.log`; có bước Host lên (`/health-status = 200`) | handover N109 §1.1 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | `exit 0` · **PASS**, **0** link chết (baseline mở phiên `N110`; bản N109: 312 tệp · 1019 link) | handover N109 §1 |
| `tools/agents/check-agent-artifact.ps1 -Kind review` | `exit 0` · **DAT** (`VERDICT: PASS`, **P1 = 0**) | review §Kết luận |
| `tools/agents/check-agent-artifact.ps1 -Kind testplan -Srs …` | `exit 0` · **DAT** · **24/24 BR** | handover N109 §1 |
| Negative control (`tools/agents/negative-control.ps1`) | **6/6** đỏ → xanh, log trong `artifacts/negative-control/` | testplan §Negative control |
| `git diff --name-only origin/tich-hop/gd3...HEAD -- apps/admin-web packages/api-client` | **rỗng** (0 tệp FE) | testplan §5 |

### 5.2. Kiểm sau triển khai (thủ công, trên production)

| Kiểm | Kỳ vọng |
|---|---|
| `GET /health-status` (Host) · `GET /api/health` (BFF `apps/admin-web`) | **`200`** — chứng minh DI mới (một `INotificationSender`) khởi động được |
| Cờ bàn giao | Log Host mỗi nhịp 10 s có dòng `[NOT-DISPATCH] … =false — KHÔNG bàn giao` (khoá `Notification:EnableDispatch`) |
| **0** lần fail-closed | Log **không** có dòng `[NOT-CHANNEL] … ErrorCode=eCommerce:Notification:0003`; metric `notification_queue_failed_total{reason="eCommerce:Notification:0003"}` **không** tăng |
| **0** dòng mới do worker | `SELECT count(*) FROM "NotificationQueueEvents" WHERE "CreationTime" > timestamptz '<thời điểm deploy>';` → **0** nếu môi trường không có luồng nghiệp vụ khác tạo thông báo trong cửa sổ kiểm. Nếu `> 0`, đối chiếu `CorrelationId`/`ActorUserId`: worker khi cờ `false` **không** tạo event. |
| Không có tin bị bàn giao giả | `SELECT count(*) FROM "NotificationQueueEvents" WHERE "CreationTime" > timestamptz '<deploy>' AND "ErrorCode" = 'eCommerce:Notification:0003';` → **0** |
| Ma trận endpoint **không đổi** | **9** route `/api/v1/notifications/*` của `NOT-001` vẫn `401` khi không Bearer token; `WP-23` **0** route mới |
| Seed template | `SELECT count(*) FROM "NotificationTemplates" WHERE "TenantId" IS NULL;` → **3** (không đổi) |
| `schema.d.ts` | `pnpm gen:api` **không** đổi — **82/82** path (kỳ vọng đã đo ở `N109`) |

> Không có bước kiểm DB schema cho `WP-23` vì **0** migration. Không chạy `dotnet ef database update` ở bất kỳ bước nào.

### 5.3. Mã lỗi `NOT-002` và cách đọc

- `eCommerce:Notification:0002` — người nhận không hợp lệ theo NCC.
- `eCommerce:Notification:0003` — **kênh chưa sẵn sàng**, fail-closed **NÉM** trước mọi lời gọi mạng; map **HTTP 503**
  (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:443-444`). Gồm 7 điều kiện: không cấu hình kênh · `IsEnabled=false` ·
  credential rỗng · credential hết hạn · không có adapter khớp `(ChannelKind, ProviderCode)` · thiếu khoá `Settings` bắt buộc ·
  adapter tự ném `0003`.
- `eCommerce:Notification:0006` — adapter báo `Accepted=true` nhưng thiếu `ProviderMessageId` (không đánh dấu thành công).
- Hôm nay `0003` **không** xảy ra khi cờ `false` (worker return trước khi gọi sender); nếu thấy `0003` lặp ⇒ cờ đang `true` **hoặc**
  ai đó bật nhầm — dừng bằng §4.
- Dải `0010–0020` vẫn thuộc `NOT-001` (xem [NOT-001-deploy.md](./NOT-001-deploy.md) §5.4); `WP-23` **không** đụng.

## 6. Khi hỏng — ai gọi

- **Host không khởi động sau deploy / DI resolve sai / hai `INotificationSender`**: gọi **`be`** (module `Notification`, biên DI
  `NotificationApplicationModule`) và **`arch`** nếu hợp đồng phải đổi. Không tự sửa DI ở Host.
- **Bật nhầm `EnableDispatch` / hàng đợi kẹt `DangBanGiao` / log `0003` lặp**: đặt cờ `false` + restart (việc **`ops`**); nếu lease
  không khôi phục ⇒ **`be`** (worker).
- **`0003` xuất hiện dù cờ `false`, hoặc metric/log sai**: **`ops`** kiểm giá trị cấu hình thực tế của Host; sau đó **`be`**.
- **Map `0003`/`0002`/`0006` → HTTP sai**: **`be`** (`eCommerceHttpApiHostModule.cs`); **`arch`** nếu cần đổi ngữ nghĩa API.
- **Chọn NCC email/SMS (`Q1`, `D-200`), 5 thông số Zalo OA (`Q3`, `D-202`), khoá HMAC production (`D-204`)**: **việc của người**
  (ngoài tạm duyệt `R3-6`) — báo **ORCH** mở `blocked`, **không** tự chọn NCC/ký hợp đồng/tạo secret.
- **Không chắc về secret/production**: **ORCH** mở `blocked`, **không** tự xử (O1).
- **Merge `main` / branch protection**: **chủ dự án** (biên bản người ký); agent **không** merge `main`, **không** đổi protection.
- **Review phát hiện P1 mới ở increment sau**: **`rev`** + **`decider`** trước khi bật cờ.

## 7. Nợ & rủi ro đã biết (theo dõi trước khi bật kênh thật)

| # | Mức | Nợ / rủi ro | Chặn gì | Chủ |
|---|---|---|---|---|
| 1 | 🟠 **P2** (`REV #1`) | **Retry storm**: bật `EnableDispatch=true` khi chưa cấu hình kênh ⇒ mỗi lease **NÉM** `0003` + `Warning` + metric tăng vô hạn; **không** có trần thử/DLQ/cảnh báo khởi động. | **Phải đóng TRƯỚC khi** bật cờ/kênh thật: thêm trần thử + DLQ cho `0003` lặp, hoặc log `Error` một lần/tenant/nhịp, và cảnh báo khởi động khi `EnableDispatch=true` mà chưa có adapter/config. Cờ **mặc định `false`** nên không chặn merge. | increment đầy đủ `NOT-002` / `ops` |
| 2 | 🟡 **`D-317`** | Ca "Host boot" bằng `WebApplicationFactory` **hoãn**; hiện "boot" đo bằng `-DayDu`, "resolve" bằng `TC-DI-02`. | Không chặn merge; là điều kiện tin cậy trước increment đầy đủ. Hạn = increment đầy đủ `NOT-002` sau `R3-6`. | `qa` |
| 3 | 🟠 **Ngoài tạm duyệt (`R3-6`)** | Chọn NCC email/SMS (`Q1`, `D-200`) · 5 thông số Zalo OA (`Q3`, `D-202`) · khoá HMAC production (`D-204`). | Tới khi chốt: **chỉ** hợp đồng trung lập + `fake` sender + fail-closed `0003`; **0** gói NuGet NCC, **0** kết nối mạng. Không suy đoán thông số NCC. | **người** |
| 4 | 🟠 **P2 kế thừa (`NOT-001`)** | Masker chạy trên chính body gửi đi (`D-269`) — có thể méo tin thật (mã đơn 13–19 số, `code=`, hotline). | `WP-23` chưa gửi được nên chưa ảnh hưởng, nhưng **phải chốt** "gửi bản đã che" vs "gửi bản gốc + che khi hiển thị" **trước khi** cấu hình kênh đầu tiên. | `decider` + `be` |
| 5 | 🟡 **P3 (`REV`)** | 5 nợ code/test còn lại: log `NotificationDispatchWorker.cs:44-46` còn chữ "chưa có kênh gửi thật" (sai thời sự) · `ProviderCodeToiDa` chưa có điểm dùng production (0 bảng) · `_clock.Now` không `.ToUniversalTime()` (`NotificationSender.cs:69`) · `record.ToString()` của `SendCommand`/`NotificationChannelConfig` in ciphertext nếu bị log · `TC-DI-01` chỉ đếm assembly `Application`. | Không chặn merge; xử lý trong increment đầy đủ. | `be`/`qa` |
| 6 | 🟡 | `Notification:Dispatch:PeriodSeconds` **khai báo nhưng chưa được đọc** — worker hard-code `Timer.Period = 10_000`. | Đổi khoá không đổi nhịp; cần sửa code nếu muốn cấu hình nhịp. | `be` |
| 7 | 🟡 | Host **chưa có** section `Notification` trong `appsettings*.json` ⇒ cờ lấy mặc định code `false`. | Nếu thêm section, **không** đặt `true` trước increment đầy đủ; ghi giá trị vào `.env`/secret store, không commit production (O1). | `ops` |
| 8 | 🔴 | Merge `main` cần **biên bản người ký** (`P-21`, `D-71`); PR `tich-hop/gd3` → `main` mở **cuối chuỗi** (`D-216`). | Agent **không** merge `main`, **không** đổi branch protection. | chủ dự án |
| 9 | 🟡 | Lệch câu chữ P3: ARCH §1.3 ghi "8 endpoint" vs SRS §6.2 **11**. | Không ảnh hưởng vận hành hôm nay (0 endpoint); sửa ở increment (`arch`). | `arch` |
