# Doc review — «NOT-002-arch» Kiến trúc liên module: kênh gửi trung lập NCC (hợp đồng adapter · registry · fail-closed `0003`)

**VERDICT: DOC-OK** (vòng 2 — **0 P1 · 0 P2 · 0 P3**; vòng 1 là `DOC-CHANGES` **1 P1 · 8 P2 · 9 P3**, đã đóng đủ 18/18)

| | |
|---|---|
| Tài liệu | `docs/features/notifications/NOT-002-arch.md` — 703 dòng sau sửa tại chỗ, thiết kế MỚI (0 code / 0 migration), hàng `W23-1`, nhánh `feat/gd3-wp23-kenh-gui` |
| Ngày · vòng | 28/11/2026 · **vòng 2 — kiểm nhanh** (đọc lại độc lập toàn văn, không tin lời `arch`) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N106) |
| Đã đọc | vòng 2: **1–703 toàn văn** + đối chiếu lại SRS `not-002-…md` §0/§1/§3/§5.5/§6.1/§6.2/§12/§14.1, `NOT-001-arch` §1.4/§4.4/§15.2 #11, `decisions.md` `D-197`/`D-199`/`D-200`/`D-202`/`D-204`/`D-207`/`D-208`/`D-263`/`D-264`/`D-312`, `uy-quyen-chu-du-an` §2.1/§5, `coding-conventions.md:107`, code as-built |
| Máy | `kiem-link-tai-lieu.ps1` → **PASS** (309 tệp · 1016 link · 1884 đường dẫn backtick · 0 chết); `check:mermaid` → **PASS** (214 khối · 0 lỗi); `check-feature-doc.ps1 -WP14` **không áp dụng** (`-arch` ngoài 16 mục — header dòng 22-24 nêu đúng chủng loại); `check-agent-artifact.ps1 -Kind docreview` → exit **0**. |
| Phạm vi | Kiểm **18 mục vòng 1** + **tương tác trực tiếp** của bản sửa (SendCommand mới, `AQ8`, `docDeviation #8/#9`, §8.2 phụ lục, §13.1 map TC); **không** mở lại toàn bộ ngoài vùng đổi. Code as-built **không đổi** (`git status` chỉ có tài liệu). |

> **Lịch sử vòng 2 (28/11/2026):** đọc lại độc lập bản 703 dòng; đối chiếu từng mục #1–#18 của vòng 1 trên bản sửa; **18/18 đã đóng đúng bản chất**, **0 phát hiện mới** ⇒ `DOC-OK`.

## Vòng 2 — kiểm nhanh

### Đã kiểm (vòng 2)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc lại §3.1 ↔ §4.1 ↔ §4.3 ↔ §4.5 ↔ §4.6 ↔ §6.1 ↔ §6.2 ↔ §10 ↔ §12; `Grep` các câu cấm cũ ("cấm … `SendCommand`", "0 dòng ghi", "`IsTransient = true, ErrorCode = 0003`") — **0** còn | **0 phát hiện mới** (3 mục D1 vòng 1 đã đóng) |
| D2 chéo tài liệu / code / `D-xx` | `SendCommand` mới ↔ SRS §6.1 + `docDeviation #2`; `Unauthenticated` ↔ SRS §1 E3/§12 + `docDeviation #8`; `0006` ↔ SRS §3 hàng 3/§12 + `docDeviation #9`; `D-199`/`D-207` truy vết | **0 phát hiện mới** (5 mục D2 vòng 1 đã đóng) |
| D3 cơ chế thực thi | Đường credential có thật: config → `SendCommand.CredentialEncrypted` → adapter giải mã (không đọc lại provider); 7 điều kiện `0003` + mọi lỗi credential đều **NÉM** | **0 phát hiện mới** (P1 vòng 1 đã đóng) |
| D4 SQL / lệnh | Quét toàn văn: **0** khối `sql`/`powershell`/`bash`; §8.2 là phụ lục mô tả bảng | **chưa chạy: không có khối lệnh** · 0 phát hiện mới (NND bắt buộc + `114 → 115`) |
| D5 tên test / tệp / dòng | `Glob **/NotificationLockKeys.cs`; kiểm lại 20+ dẫn và 3 dẫn đã sửa; đối chiếu `TC-*` mới với SRS §14.1 + bảng map | **0 phát hiện mới** (3 mục D5 vòng 1 đã đóng) |
| D6 nhãn tin cậy | Đo lại `git ls-files modules/Notification` = **28**, `modules/` = **17**, `PostgreSqlSchemaTests` = **114**, metric `NOT-001` = **6 counter + 1 gauge**, `ChuaCoNoiPhat` còn `promotion_applied_total` | **0 phát hiện mới** (2 mục D6 vòng 1 đã đóng) |
| D7 14 quy tắc + `docDeviation` | Đi #1–#14; kiểm `docDeviation #8/#9`, phụ lục `AQ1`, đường lùi, module chủ, không endpoint hứa trước, Mermaid (không vẽ) | **0 phát hiện mới** (5 mục D7 vòng 1 đã đóng) |

### Phát hiện (vòng 2)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| — | — | — | **0 phát hiện mới**: không còn P1/P2/P3 sau 18/18 mục vòng 1 | Không cần sửa thêm |

## Vòng trước (vòng 1 — 1 P1 · 8 P2 · 9 P3) — 18/18 đã đóng

Cột 2 là **trạng thái** (không phải "Mức") để `check-agent-artifact.ps1` không đếm nhầm. Mỗi dòng đã đối chiếu lại trên bản sửa.

| # vòng 1 | Trạng thái | Mức lúc đó | Vị trí / vấn đề vòng 1 | Bằng chứng đã sửa (dòng trên bản vòng 2) |
|---|---|---|---|---|
| 1 | **đã đóng** | P1 | §4.3/§4.6/§10/§4.1 — adapter là nơi duy nhất giải mã credential nhưng `SendCommand` không mang credential và §10 cấm | §4.1 dòng **212** `string? CredentialEncrypted` (chỉ ciphertext); dòng **230-233** "Đường credential DUY NHẤT"; §4.3 dòng **286-287** sender chép ciphertext, adapter giải mã; §4.4 dòng **301**; §4.6 dòng **341**; §10 dòng **510** "cấm plaintext/log/serialize, ciphertext chỉ đi trong `SendCommand.CredentialEncrypted`"; `AQ8` dòng **632**; `docDeviation #2` dòng **641** |
| 2 | **đã đóng** | P2 | `Unauthenticated` trả `IsTransient+0003` trái §6.2; xử lý credential không nhất quán; thiếu `docDeviation` so với SRS §1 E3/§12 | §3.1 dòng **153** NÉM `0003`; §4.5 hàng 5 dòng **312** NÉM + dòng **320-321** "cùng ngữ nghĩa"; §6.2 dòng **433-435**; `docDeviation #8` dòng **647** |
| 3 | **đã đóng** | P2 | §6.1 "0 dòng ghi DB … hiển nhiên" sai — job ghi `Dispatching`/`LeaseExpired` | §6.1 dòng **414-416** "**`NotificationSender` ghi 0** … job vẫn ghi `Dispatching`/`LeaseExpired` (`…Worker.cs:122-124`) … **không phải** 0 event"; §13.1 dòng **561** "0 `DaBanGiao`/`DaGui`" |
| 4 | **đã đóng** | P2 | §7 dẫn `NotificationLockKeys.cs:69-70` — tệp không tồn tại | §7 dòng **444** `NotificationErrorCodes.cs:69-70` (khớp §1.1 dòng 43 `:67-83`) |
| 5 | **đã đóng** | P2 | `NotificationMasker.cs:15` cho `CheBiMat` (phương thức ở :70), lặp 4 chỗ | §1.1 dòng **52** `:15,70`; §4.5 dòng **319** `:70`; §10 dòng **513** `:70` |
| 6 | **đã đóng** | P2 | §8.2 thiếu `WebhookSecretEncrypted` (D-202) + `OptOutLastSyncedAtUtc` (D-207) + cột SRS §5.5 | §8.2 thành **Phụ lục (ngoài phạm vi)** dòng **464-466** có ghi "Không đầy đủ — nguồn duy nhất SRS §5.5 + D-202 + D-207"; dòng **468-477** đủ `RefreshCredentialEncrypted`/`WebhookSecretEncrypted`/`DailyQuota`/`UsedToday`/`QuietHoursFrom/To`/`OptOutLastSyncedAtUtc` |
| 7 | **đã đóng** | P2 | §8.2 bỏ NND với lý do "A4" — trái `coding-conventions.md:107` | §8.2 dòng **478-479** "**PHẢI `NULLS NOT DISTINCT`** … (`docs/coding-conventions.md:107`, CLAUDE.md §7), **kể cả** cột khoá `NOT NULL`"; dòng **482** "**114 → 115**" |
| 8 | **đã đóng** | P2 | `TC-44` không định danh — trong NOT-002 SRS là test opt-out | §1.4 dòng **87** `TC-44` của **`NOT-001` SRS**; §13.1 dòng **573** cùng |
| 9 | **đã đóng** | P2 | Mã test mới §13.1 không nhãn "tạo ở W23-4" và không map TC gốc | §13.1 dòng **555-556** quy ước "TC mới — tạo ở `W23-4`"; mọi hàng gắn "(mới)"; bảng map dòng **568-580** |
| 10 | **đã đóng** | P3 | "7 metric + gauge backlog" — thực là 7 tên **gồm** gauge | §1.1 dòng **49** "**6 counter + 1 gauge backlog** (**7** tên)" |
| 11 | **đã đóng** | P3 | "`ChuaCoNoiPhat` rỗng" — còn `promotion_applied_total` | §1.1 dòng **49** "không còn nợ `NOT-001` (chỉ còn `promotion_applied_total` của `PRO-002`)"; dẫn `:315` |
| 12 | **đã đóng** | P3 | §4.5 hàng 2 dẫn SRS §3 hàng 3 (NÉM `0006`) nhưng trả kết quả, thiếu `docDeviation` | §4.5 dòng **322-323** "Hàng 2 khác SRS §3 hàng 3 … `docDeviation #9`, BA sửa §3/§12 cùng PR"; `docDeviation #9` dòng **648** |
| 13 | **đã đóng** | P3 | §5.3 "Thiết kế chốt" xoá `ChuaCoKenhGuiSender` trong khi `AQ6` mở | §5.3 dòng **376** "**Đề xuất** cho `W23-2` (**chờ `AQ6`**)"; dòng **385-386** nhánh giữ lớp |
| 14 | **đã đóng** | P3 | §8.2 dựng bảng thuộc OUT (§1.3) dễ đọc như IN | §8.2 dòng **464-466** "**Phụ lục (ngoài phạm vi `WP-23`)** … đối chiếu §1.3 … chỉ để DEC cân nhắc `AQ1`" |
| 15 | **đã đóng** | P3 | §17.1 thiếu `D-199`/`D-207` | §17.1 dòng **671** `D-199` → §3.2/§3.3; dòng **672** `D-207` → §4.1/§4.5/§16 #4 |
| 16 | **đã đóng** | P3 | Dẫn `NotificationPersistenceTests.cs:22-35`; danh sách 5 bảng ở :29-36 | §1.1 dòng **53**, §8.1 dòng **459**, §8.2 dòng **482** đều `:29-36` |
| 17 | **đã đóng** | P3 | `FindAsync(ChannelKind)` không chọn được tài khoản ⇒ "thay một dòng DI" chưa đủ | §1.4 dòng **85** "**phải mở rộng** `FindAsync` … **nợ N1**"; §4.3 dòng **283-285**; §14 R5 dòng **611** |
| 18 | **đã đóng** | P3 | §4.5 không nêu `RecipientRefused=true` rơi vào nhánh nào | §4.5 dòng **324** "`RecipientRefused = true` **bị bỏ qua** ở `W23-2` (rơi vào hàng 6 ⇒ `0002`); increment sau mới ghi opt-out `Source=ProviderReport`" |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` nào ⇒ không có lệnh để chạy trong PostgreSQL (`BEGIN … ROLLBACK`). §8.2 là phụ lục mô tả bảng/index, đã kiểm bằng đối chiếu cột/quy ước, không bằng chạy DDL.
- **`check-feature-doc.ps1 -WP14`:** không áp dụng cho `-arch` (ngoài 16 mục / BR / "Đối chiếu Nhanh") — đúng chủng loại theo header dòng 22-24.
- **As-built `NotificationSender`/adapter:** chưa tồn tại (0 code) ⇒ chỉ đối chiếu được với hợp đồng trong tài liệu.

## Bằng chứng đồ thị (xác nhận lại — code as-built không đổi trong vòng 2)

- `graph: codegraph_explore "INotificationSender NotificationSendRequest NotificationSendResult ChuaCoKenhGuiSender NotificationApplicationModule"` → `INotificationSender.cs:8-30` (interface 1 method; request **8** trường :16-24; `RecipientEncrypted` :21); `NotificationApplicationModule.cs:13-19` `[DependsOn]`, `:24` explicit `AddTransient`, `:40-51` lớp `ChuaCoKenhGuiSender : INotificationSender, ITransientDependency` ném `InvalidOperationException` :47-49 ⇒ **hai** đường đăng ký như §5.3.
- `graph: codegraph_explore "NotificationEnums NotificationErrorCodes NotificationSettingKeys NotificationLockKeys NotificationPriorities ChannelKind"` → `NotificationEnums.cs:7-12` / `:91-97`; `NotificationErrorCodes.cs:8-42` (`0010–0020`), `:45-61` `NotificationConsts`, `:67-83` `NotificationLockKeys`, `QueueDispatch` **:69-70** (khớp §7 dòng 444); `NotificationSettingKeys.cs:4-14`, `DispatchLeaseSeconds` :8 / 120 :12.
- `graph: codegraph_explore "NotificationDispatchWorker BanGiaoAsync DoWorkAsync KhoiPhucLeaseAsync"` → `DoWorkAsync` :35-98 (`EnableDispatch` false :42-48); `KhoiPhucLeaseAsync` :100 ghi `LeaseExpired` **:122-124** (khớp §6.1 dòng 415); `BanGiaoAsync` :210-290 (request :268-271, sender :279, `catch` :281-286); `GhiKetQuaBanGiaoAsync` :292 (accepted :308-320, transient :321-335, `DanhDauLoi` :339).
- `graph: codegraph_explore "eCommerceMetrics NotificationQueueFailed NotificationDispatched ChuaCoNoiPhat eCommerceMetricTags Forbidden Allowed"` → `eCommerceMetrics.cs:95-113` **7** tên (gồm gauge backlog :107); `:189` trong `All`; `:262`; `:297-316` `ChuaCoNoiPhat` còn `[PromotionAppliedTotal]` **:315** (khớp §1.1 dòng 49); `:357-369` `Allowed`/`Forbidden`; `:428` thuộc tính `NotificationQueueFailed`.
- `graph: codegraph_explore "NotificationQueueAppender KenhNhan NotificationMasker CheBiMat IStringEncryptionService"` → `NotificationQueueAppender.cs:117-121` (encrypt :118, `CheBiMat` :119-120); `KenhNhan` :304-310; `NotificationMasker.CheBiMat` **:70** (lớp ở :15 — khớp dẫn `:15,70`).

Đo bằng đọc/Grep (đồ thị không phủ): `git ls-files modules/Notification` = **28**; `modules/` = **17**; `PostgreSqlSchemaTests.cs:227` = **114**; `NotificationPersistenceTests.cs:29-36` = **5** bảng; `NotificationQueueAppenderTests.cs:642-655` `SenderGia`; 4 `*.csproj`; `eCommerceDbContext.cs:63-67,393,417-418`; `eCommerceConsts.cs:453-464`; permission provider `:223-231`; `appsettings*.json` 0 section `Notification`; `Directory.Packages.props` 0 gói NCC; `Glob **/NotificationLockKeys.cs` = 0 tệp.

## Đã đọc

`readRanges`: **1-703** (toàn văn bản sửa, đọc lại độc lập). Nguồn đối chiếu đã đọc: SRS dòng 16-115, 173-232, 406-465, 458-592, 629-698, 714-823, 835-894, 1004-1063; `NOT-001-arch` 14-21, 70-119, 120-123, 280-303, 434-439, 631-660; `decisions.md` 211, 213, 214, 216, 218, 221, 222, 277, 278, 326; `uy-quyen-chu-du-an` 28-37, 78-85; `07-ke-hoach-trien-khai` 748-757; `bang-phien-gd3-1` 80-89; `coding-conventions.md:107`; code as-built liệt kê ở mục đồ thị.
