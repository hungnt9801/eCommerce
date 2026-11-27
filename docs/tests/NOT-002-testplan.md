# Testplan `NOT-002` — 28/11/2026

- SRS: `docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md` (§1 BR-NOT-002-001…024 · §6.1 hợp đồng adapter · §14.1 TC-01…47 · §14.3 DoD tách phạm vi `WP-23` · §16.3 test as-built)
- Kiến trúc bổ trợ: `docs/features/notifications/NOT-002-arch.md` (§6.1 7 điều kiện fail-closed · §13.1 map tầng + bảng `TC mới → TC gốc/BR` · §13.2 6 negative control · §13.3 phần BR chưa phủ)
- Nhánh: `feat/gd3-wp23-kenh-gui` · Phiên `N109` · Hàng `W23-4` · Bước **F4 QA** (F5 REV làm sau)
- Trạng thái nền: `W23-2` (BE) + `W23-3` (no-FE) XONG; SRS `as-built` cho phạm vi `WP-23`.
- Phạm vi `WP-23` (theo SRS §14.3): **hợp đồng adapter kênh + `fake` sender + fail-closed `0003` + registry/DI/kiến trúc** trên **0** bảng · **0** migration · **0** endpoint · **0** quyền · **0** metric mới · **0** gói NuGet NCC.
- Tầng test dùng: **Domain thuần** (enum/mã lỗi) · **Application** (`NotificationSender` thật + `fake` adapter/config/clock, không DB) · **Registry/DI** (container ABP thật, không Host/DB) · **Kiến trúc** (quét tệp nguồn + csproj) · **Integration PostgreSQL** (chạy lại, 0 ca mới) · **FE**: 0 tệp.

## 1. Phủ BR

> Mỗi `BR-NOT-002-0xx` của SRS §1 phải có **ít nhất một** test thật cho **phần đã hiện thực**. BR ngoài phạm vi `WP-23` (ARCH §13.3) ghi ⚠️ **"chưa kiểm được"** kèm lý do — **không** bịa test giả, **không** đánh dấu đã phủ.

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-NOT-002-001** (mọi lần gửi qua `INotificationChannelSender`; cấm gọi SDK/HTTP trực tiếp từ app service/job) | `test/eCommerce.Tests/Notification/NotificationChannelArchTests.cs` · `TC_ARCH_01_khong_co_goi_NuGet_nha_cung_cap_trong_solution_va_module` · `TC_ARCH_02_NotificationSender_va_job_khong_dung_HttpClient_SmtpClient_System_Net_Mail` (quét **cả** `Channels/` **và** `Dispatch/`); `…/NotificationChannelSenderTests.cs` · `TC_MAP_01`/`TC_MAP_02`/`TC_MAP_03`; `…/NotificationChannelRegistryDiTests.cs` · `TC_REG_01`/`TC_REG_02` · `TC_DI_01` | Kiến trúc + Application | ✅ (tầng kênh + job; `TC_DI_01` chứng minh hiện thực production **duy nhất**) |
| **BR-NOT-002-002** (`IdempotencyKey` ổn định; gửi lại cùng khoá ⇒ không tạo tin thứ hai bằng **unique DB**) | `…/NotificationChannelSenderTests.cs` · `TC_KEY_01_khoa_va_noi_dung_di_nguyen_van_sang_SendCommand` | Application | ⚠️ **một phần**: khoá đi **nguyên văn** qua hợp đồng; unique index chưa kiểm được vì `WP-23` **0 bảng** |
| **BR-NOT-002-003** (callback/webhook chống trùng bằng unique index) | — | — | ⚠️ **chưa kiểm được**: callback **chưa mở** (`D-202`), 0 bảng/0 endpoint |
| **BR-NOT-002-004** (chuyển trạng thái sai **NÉM**; cấm lùi) | `…/NotificationChannelSenderTests.cs` · `TC_MAP_03_vi_pham_hop_dong_thi_NEM_InvalidOperationException` (chỉ chặn outcome `SendOutcome` mâu thuẫn — **không** phải máy trạng thái `NOT-002`) | Application | ⚠️ **một phần**: state machine `NOT-002` **chưa hiện thực** ở `WP-23` |
| **BR-NOT-002-005** (`NotificationDeliveryEvents` APPEND-ONLY) | — | — | ⚠️ **chưa kiểm được**: bảng/entity `NotificationDeliveryEvents` **chưa có** (0 bảng) |
| **BR-NOT-002-006** (mọi ghi qua UoW; message + event + outbox cùng transaction) | — | — | ⚠️ **chưa kiểm được**: `NotificationSender` **ghi 0** (đúng fail-closed) nên chưa có transaction để chứng minh |
| **BR-NOT-002-007** (nhãn metric chỉ từ `Allowed`; cấm `email`/`phone`/`messageId`/`recipientId`) | `…/NotificationChannelSenderTests.cs` · `TC_MET_01_fail_closed_tang_metric_queue_failed_reason_0003` (mọi khoá nhãn ∈ `Allowed`, **0** recipient/credential); `…/NotificationChannelArchTests.cs` · `TC_ARCH_04` phần (b)(c) | Application + Kiến trúc | ✅ |
| **BR-NOT-002-008** (thêm/đổi metric phải cập nhật registry + `XC-005` + test cùng PR) | `…/NotificationChannelArchTests.cs` · `TC_ARCH_04_khong_endpoint_AllowAnonymous_va_metric_dung_ngoai_registry` (metric dùng ⊆ registry, 0 `new Meter(`); `…/NotificationChannelSenderTests.cs` · `TC_MET_01` | Kiến trúc + Application | ✅ (trong phạm vi: **0** metric mới — dùng lại `notification_queue_failed_total`) |
| **BR-NOT-002-009** (cấm log/lưu OTP; mask nội dung; cấm log token/SĐT/email mức `Information`) | `…/NotificationChannelSenderTests.cs` · `TC_LOG_01_fail_closed_phat_Warning_co_ErrorCode_va_0_PII` (khẳng định credential/recipient/subject/body **không** xuất hiện ở log lẫn `Exception.Data`) | Application | ⚠️ **một phần**: phần **log** đã phủ; phần **lưu/mask** không áp được vì `WP-23` **0 bảng** (mask thuộc `NOT-001`, `NotificationMaskerTests`) |
| **BR-NOT-002-010** (token Zalo OA chỉ trong khoá `notification:zalo-oa:{oaId}:token-refresh`, double-check) | — | — | ⚠️ **chưa kiểm được**: Zalo OA **chưa bật** (`D-200`/`D-202`), chưa có luồng refresh token |
| **BR-NOT-002-011** (timeout hữu hạn + retry giới hạn + backoff; quá giới hạn ⇒ DLQ) | `…/NotificationChannelArchTests.cs` · `TC_ARCH_05_hop_dong_adapter_bat_buoc_CancellationToken_cho_timeout` (mọi `SendAsync` có tham số cuối `CancellationToken` mặc định) | Kiến trúc (reflection hợp đồng) | ⚠️ **một phần**: hợp đồng **huỷ được** đã phủ; `timeout`/`retry`/`backoff`/`DLQ` chưa chứng minh được khi chưa có adapter thật (ARCH §13.3) |
| **BR-NOT-002-012** (quiet hours + opt-out cho khách; ngoại lệ `Critical`; opt-out không áp nội bộ) | — | — | ⚠️ **chưa kiểm được**: quiet hours/opt-out **chưa hiện thực** ở `WP-23` |
| **BR-NOT-002-013** (mọi số tiền `decimal`; cấm `double`/`float`; cấm `==`) | — | — | ⚠️ **chưa kiểm được**: `WP-23` **không** xử lý tiền; `unitCostPerMessage` chỉ là **khoá Settings bắt buộc** (`TC_FC_06`) |
| **BR-NOT-002-014** (webhook không HMAC GHTK không là nguồn duy nhất cho tin có tiền) | — | — | ⚠️ **chưa kiểm được**: `SHP-002`/GHTK **ngoài** `WP-23` |
| **BR-NOT-002-015** (OTP đổi COD qua kênh đã xác thực; không lưu/log; TTL + giới hạn gửi lại) | — | — | ⚠️ **chưa kiểm được**: `SHP-003`/OTP **ngoài** `WP-23` |
| **BR-NOT-002-016** (cấm `NOT-002` gọi app service module khác; dữ liệu có sẵn trong ETO) | `…/NotificationChannelArchTests.cs` · `TC_ARCH_03_Channels_khong_tham_chieu_IAppService_module_khac` (quét `using` ngoài module + mẫu `I*AppService`) | Kiến trúc | ✅ (tầng `Channels/`; job `Dispatch/` không đổi ở `WP-23`, không tham chiếu app service) |
| **BR-NOT-002-017** (credential mã hoá at-rest; cấm trả ra DTO kể cả bản mã hoá) | `…/NotificationChannelSenderTests.cs` · `TC_KEY_01_khoa_va_noi_dung_di_nguyen_van_sang_SendCommand` (sender nhận **đúng ciphertext**, **không** giải mã) | Application | ⚠️ **một phần**: phần "sender chỉ dùng ciphertext" đã phủ; "mã hoá at-rest"/"không trả DTO" chưa có bảng/DTO ở `WP-23` |
| **BR-NOT-002-018** (lỗi đã xử lý phải log ≥ `Warning` + `ErrorCode`; cấm nuốt im lặng) | `…/NotificationChannelSenderTests.cs` · `TC_LOG_01_fail_closed_phat_Warning_co_ErrorCode_va_0_PII` (`Warning` + `ErrorCode=0003`, một dòng JSON) | Application | ✅ |
| **BR-NOT-002-019** (`DedupKey` callback + UNIQUE `NULLS NOT DISTINCT`; một cửa parser) | — | — | ⚠️ **chưa kiểm được**: `INotificationCallbackParser`/bảng inbox **chưa hiện thực** |
| **BR-NOT-002-020** (`RecipientHash` HMAC-SHA256; 0 plaintext; kiểm opt-out; fail-closed) | — | — | ⚠️ **chưa kiểm được**: opt-out/`RecipientHash` **chưa hiện thực** |
| **BR-NOT-002-021** (`SentAckTtl` mặc định/biên; hết TTL ⇒ đúng một `AckExpired`) | — | — | ⚠️ **chưa kiểm được**: TTL đối soát **chưa hiện thực** |
| **BR-NOT-002-022** (cảnh báo kỹ thuật: `opsAlertRecipients` trần 20; `Critical` fan-out; 0 hard-code) | — | — | ⚠️ **chưa kiểm được**: cảnh báo kỹ thuật **chưa hiện thực** |
| **BR-NOT-002-023** (opt-out là nguồn sự thật duy nhất + hai adapter đồng bộ) | — | — | ⚠️ **chưa kiểm được**: opt-out sync **chưa hiện thực** |
| **BR-NOT-002-024** (trung lập NCC: 0 hằng số/0 NuGet NCC; fake sender; kênh tắt/thiếu credential/thiếu đơn giá ⇒ fail-closed `0003` trước mạng) | `…/NotificationChannelArchTests.cs` · `TC_ARCH_01` · `TC_ARCH_02`; `…/NotificationChannelSenderTests.cs` · `TC_FC_01`…`TC_FC_07` (7 điều kiện §6.1, **0** lần gọi adapter khi fail-closed) · `TC_KEY_01` (test dùng `fake` sender, 0 mạng) | Kiến trúc + Application | ✅ |

- Tổng BR: **24** · có test thật cho phần đã hiện thực: **10** (6 đủ ✅ · 4 một phần ⚠️) · chưa kiểm được: **14** (`003`/`004`/`005`/`006`/`010`/`012`/`013`/`014`/`015`/`019`/`020`/`021`/`022`/`023`).
- Test bổ sung ở `W23-4` (trong vùng QA, tệp đã có từ `W23-2`):
  - `NotificationChannelArchTests.cs` · **`TC_ARCH_05`** (mới) — **BR-011** phần hợp đồng (`CancellationToken` bắt buộc trên `INotificationChannelSender.SendAsync` và `INotificationSender.SendAsync`).
  - `NotificationChannelArchTests.cs` · **mở rộng `TC_ARCH_02`** — quét **cả** `Dispatch/` (job) không chỉ `NotificationSender.cs`, để **BR-001** phủ đúng câu "cấm gọi từ app service **hoặc job**".

### Map `TC mới` → `TC gốc`/BR (lấy từ `NOT-002-arch.md` §13.1)

| Mã mới | TC gốc / BR | Ghi chú |
|---|---|---|
| `TC-FC-01…07` | `TC-45` (SRS), **BR-024** | tách 7 điều kiện fail-closed §6.1 |
| `TC-EN-01…03` | `TC-44` của **`NOT-001`**, `D-17`/`D-197` | enum/mã lỗi một nguồn |
| `TC-MAP-01…04` | `TC-01/02/03` (happy path), **BR-001** | ánh xạ `SendOutcome` → `NotificationSendResult` |
| `TC-KEY-01` | `TC-04` (phần truyền khoá), **BR-002** | chưa kiểm unique index (0 bảng) |
| `TC-LOG-01` | `TC-13`, `TC-47`, **BR-009/BR-018** | log không PII, có `ErrorCode` |
| `TC-MET-01` | `TC-12`, NFR-014, **BR-007** | dùng lại metric đã đăng ký |
| `TC-REG-01/02` | `TC-45`, **BR-001** | registry một cửa, không fallback |
| `TC-DI-01/02` | (không có TC gốc), B6/§5.3 | DI một hiện thực + resolve qua container ABP |
| `TC-ARCH-01…04` | `TC-26`, `TC-27`, **BR-016/BR-017/BR-024** | 0 SDK/0 gói/0 app service khác/0 endpoint |
| `TC-ARCH-05` (**mới `W23-4`**) | `TC-18`, **BR-011** | hợp đồng bắt buộc `CancellationToken` (điều kiện tiên quyết của timeout hữu hạn) |

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (đầu `N109`) | Sau (đo thật `N109`) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **2649** | **2650** | **+1** (`TC_ARCH_05`); mở rộng `TC_ARCH_02` **không** thêm ca; 0 fail |
| Domain thuần + Application + Registry/DI + Kiến trúc (namespace `eCommerce.Tests.Notification`) | **143** | **144** | **+1**; `WP-23` đóng góp **31** ca (Sender/fail-closed **21** · Registry/DI **5** · Kiến trúc **5**) |
| Application — `NotificationSender` + fail-closed `0003` | (trong 143) | (trong 144) | `NotificationChannelSenderTests`: `TC_EN_01…03` · `TC_FC_01…07` · `TC_MAP_01…04` · `TC_KEY_01` · `TC_LOG_01` · `TC_MET_01` |
| Registry / DI | — | **5** | `TC_REG_01` ×2 · `TC_REG_02` · `TC_DI_01` · `TC_DI_02` (container ABP thật) |
| Kiến trúc (quét tệp/csproj/reflection) | — | **5** | `TC_ARCH_01…05` |
| API `/api/v1` (200 + 401/403) | — | **N/A** | `WP-23` **0** endpoint mới (ARCH §9) ⇒ không đổi ma trận `401/403/200` |
| Integration PostgreSQL | **162** | **162** | **0** ca mới (0 schema); `dotnet test test/eCommerce.IntegrationTests` xanh — `PostgreSqlSchemaTests` (114 index `NULLS NOT DISTINCT`) không đổi |
| UI Vitest / Playwright | — | **0** | **0** tệp FE thay đổi (`D-316`, đo ở §3) |
| OpenAPI drift | — | **0** | `packages/api-client/src/schema.d.ts` **82** path · `git diff` vs `origin/tich-hop/gd3` **rỗng** |

> 📌 Đo thật (phiên `N109`): `dotnet test test/eCommerce.Tests` → **2650/2650 pass**; `dotnet test test/eCommerce.IntegrationTests --no-build` → **162/162 pass**; số ca **chỉ tăng** (2649 → 2650). Đo namespace: `--filter "FullyQualifiedName~eCommerce.Tests.Notification"` → **144/144**.

## 3. Đo lại phạm vi FE (`D-316`)

- `git diff --name-only origin/tich-hop/gd3...HEAD -- apps/admin-web packages/api-client` → **rỗng** ⇒ **0** tệp FE.
- `packages/api-client/src/schema.d.ts`: **82** path; `git diff --stat origin/tich-hop/gd3...HEAD -- packages/api-client/src/schema.d.ts` → **rỗng** ⇒ schema **không đổi** (không cần chạy `pnpm gen:api` vì không có DTO/route thay đổi; số đo W23-2 `kiem-tat-ca` 10/10 ĐẠT vẫn giữ nguyên giá trị).
- Kết luận: `WP-23` là **BE-only**; **0** test FE cần thêm.

## Negative control (bắt buộc — đỏ → xanh)

> Bảng **bằng chứng chạy thật** dưới đây do ORCH chèn bằng `tools/agents/negative-control.ps1` (áp đột biến → test phải **ĐỎ** → khôi phục byte gốc → test phải **XANH**). `TestCommand` **build lại** (không `--no-build`) vì script sửa mã nguồn. Ở `W23-4`, QA **chỉ ghi kế hoạch**; bằng chứng do **ORCH chèn sau**.

| Ngày | Mã | Tệp:dòng | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-28 | NC-1 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:53 | NC-1 bo kiem khong co adapter (adapter null van di tiep) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_FC_05" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-2 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:48 | NC-2 tra Accepted=true khi chua co cau hinh kenh | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_FC_01" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-3 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:59 | NC-3 bo kiem IsEnabled (kenh tat van gui tiep) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_FC_02" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-4 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:174 | NC-4 metric 0003 mat nhan reason dung | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_MET_01" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-5 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:5 | NC-5 them using System.Net.Mail vao tang kenh gui | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_ARCH_02" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-6 | modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs:17 | NC-6 them hien thuc INotificationSender thu hai | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC_DI_01" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

### Kế hoạch 6 negative control theo `NOT-002-arch.md` §13.2

| # | Đột biến (thật, trong mã) | Ca phải đỏ | Tệp đích dự kiến | Kỳ vọng (ORCH chạy) |
|---|---|---|---|---|
| NC-1 | Bỏ kiểm "không có adapter" — dùng `factory.Find(...)!` (null-forgiving) thay vì trả-null-rồi-NÉM | `TC-FC-05` (kỳ vọng `0003`, thực tế `NullReferenceException`/gọi tiếp) | `modules/Notification/eCommerce.Notification.Application/Channels/NotificationSender.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-2 | `NotificationSender` trả `Accepted=true` khi chưa có cấu hình (bỏ guard `config is null`) | `TC-FC-01` + ca "0 lần gọi adapter" | `…/Channels/NotificationSender.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-3 | Bỏ kiểm `IsEnabled`/credential; đăng ký adapter `fake` **ném nếu bị gọi** để phát hiện gọi nhầm | `TC-FC-02`/`TC-FC-03` | `…/Channels/NotificationSender.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-4 | Bỏ phát metric `notification_queue_failed_total{reason=0003}` | `TC-MET-01` | `…/Channels/NotificationSender.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-5 | Thêm `using System.Net.Http;` (hoặc `System.Net.Mail`) vào `NotificationSender` | `TC-ARCH-02` (quét kiến trúc) | `…/Channels/NotificationSender.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-6 (khuyến nghị) | Đăng ký **hai** `INotificationSender` (giữ `ChuaCoKenhGuiSender` + thêm `NotificationSender`) | `TC-DI-01` | `modules/Notification/eCommerce.Notification.Application/NotificationApplicationModule.cs` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |

- Xác nhận sau mỗi ca (ORCH): `git status --porcelain -- modules/ src/` rỗng ⇒ đã khôi phục byte gốc.
- Nhóm luật quan trọng được phủ negative control: **fail-closed trạng thái/cấu hình** (NC-1/02/03) · **metric** (NC-4) · **trung lập NCC/cấm gọi mạng** (NC-5) · **DI một hiện thực** (NC-6). **Idempotency DB** (`BR-002`) **chưa có** negative control vì `WP-23` **0 bảng** — ghi ở §4.

## 4. Chưa kiểm được (lý do cụ thể — KHÔNG đánh dấu đã phủ)

> Theo `NOT-002-arch.md` §13.3. Các BR dưới đây **chưa có** bảng/adapter/endpoint nên **không thể** kiểm ở `WP-23`; thuộc increment đầy đủ sau `R3-6`.

1. **BR-003** (callback dedupe bằng unique index): callback **chưa mở** (`D-202`), **0** bảng `NotificationInbox`, **0** endpoint. Không có gì để test.
2. **BR-004** (state machine `NOT-002` §3 · NÉM `0007`, cấm lùi): máy trạng thái `NotificationMessage` **chưa hiện thực**; `TC_MAP_03` chỉ chặn `SendOutcome` mâu thuẫn, **không** thay thế.
3. **BR-005** (`NotificationDeliveryEvents` append-only): bảng/entity **chưa có** (0 bảng).
4. **BR-006** (message + event + outbox cùng transaction): `NotificationSender` **ghi 0** (đúng fail-closed) ⇒ không có transaction; job as-built thuộc `NOT-001`/increment sau.
5. **BR-010** (Zalo OA token refresh trong khoá + double-check): Zalo OA **chưa bật** (`D-200`/`D-202`), chưa có luồng refresh.
6. **BR-012** (quiet hours + opt-out; ngoại lệ `Critical`): **chưa hiện thực** ở `WP-23`.
7. **BR-013** (tiền `decimal`, cấm `double`/`float`/`==`): `WP-23` **không** xử lý số tiền; chưa có biểu thức tiền để quét.
8. **BR-014** (GHTK webhook không HMAC): `SHP-002` **ngoài** `WP-23`.
9. **BR-015** (OTP đổi COD): `SHP-003` **ngoài** `WP-23`.
10. **BR-019** (`DedupKey` callback): `INotificationCallbackParser` **chưa hiện thực**.
11. **BR-020** (`RecipientHash` HMAC + opt-out): **chưa hiện thực**.
12. **BR-021** (`SentAckTtl` + `AckExpired`): **chưa hiện thực**.
13. **BR-022** (cảnh báo kỹ thuật / `Critical` fan-out): **chưa hiện thực**.
14. **BR-023** (opt-out nguồn sự thật + hai adapter đồng bộ): **chưa hiện thực**.
15. **BR-002** phần **unique index DB** và **BR-011** phần **timeout/retry/backoff/DLQ**: chỉ phủ phần **hợp đồng** (`TC_KEY_01`, `TC_ARCH_05`); hàng rào DB/backoff cần adapter + bảng thật.
16. **`TC-DI-02` phần "Host thật boot" — đã chốt `D-317` (TẠM CHỐT):** **KHÔNG** dựng `WebApplicationFactory`/Host thật trong `W23-4`; giữ `TC-DI-02` = **container ABP thật** (`AbpApplicationFactory` + `CreateServiceProvider`, resolve `INotificationSender` + `INotificationChannelConfigProvider`, fail-closed `0003`) vì đây là tầng bắt đúng **lỗi quy ước DI**. Nửa "Host khởi động" đo bằng cổng có sẵn `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (Host thật + PostgreSQL/Redis/OpenIddict). **Giới hạn phải ghi đúng:** `EnableDispatch=false` ⇒ `NotificationDispatchWorker` **return trước** `GetRequiredService<INotificationSender>()` ⇒ `-DayDu` chứng minh **boot**, `TC-DI-02` chứng minh **resolve**; hai phép đo **bù nhau**; **cấm** ghi "Host boot ✅". Nợ có chủ: `qa`, hạn = increment đầy đủ `NOT-002` sau `R3-6` (khi đó dựng `WebApplicationFactory`).
17. **NFR** (`NFR-001` p95 · `NFR-007` ≥ 50 tin/giây · `NFR-012` log JSON 1 dòng đầy đủ): **chưa đo** (không thuộc phạm vi test chức năng; cần harness tải).
18. **Negative control `NC-8`-kiểu cho BR-002** (unique index): **chưa có** vì 0 bảng; ORCH chỉ chạy 6 NC của §13.2.

## 5. Cổng (đo trong phiên N109)

```powershell
dotnet build eCommerce.slnx --no-incremental      # 0 error / 9 warning (bằng baseline N109)
dotnet test test/eCommerce.Tests                   # 2650/2650 pass · 0 fail (trước 2649, +1)
dotnet test test/eCommerce.Tests --no-build --filter "FullyQualifiedName~eCommerce.Tests.Notification"   # 144/144
dotnet test test/eCommerce.IntegrationTests --no-build   # 162/162 pass · 0 fail (0 ca mới)
git diff --name-only origin/tich-hop/gd3...HEAD -- apps/admin-web packages/api-client   # rỗng (0 tệp FE)
pwsh -File tools/kiem-tat-ca.ps1 -DayDu           # exit 0 · 20/20 bước ĐẠT · 888,2 s (N109, 2026-11-28)
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/NOT-002-testplan.md -Srs docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error**, **9 warning** (không phát sinh warning mới).
- `dotnet test test/eCommerce.Tests` → **2650/2650 pass**, exit **0**.
- `dotnet test test/eCommerce.IntegrationTests --no-build` → **162/162 pass**, exit **0** (Docker chạy).
- `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (phiên `N109`, `2026-11-28`) → **exit 0 · 20/20 bước ĐẠT · 888,2 s**: E2E Playwright **8/8** luồng; probe HTTP thật **ADDR/CUS/PAY/SAL/Oversell/Outbox/Outbox-2-instance/Giữ-chỗ-2-instance**; DB **25/25**; `gen:api` không đổi `schema.d.ts` (**82/82** path); unit **2650/2650**; integration **162/162**. Log: `artifacts/DayDu-N109.log`. `-DayDu` chứng minh **Host boot**, còn `TC-DI-02` chứng minh **resolve** (`D-317`).
- `check-agent-artifact.ps1 -Kind testplan …` → **`KET QUA: DAT`** (exit **0**).
