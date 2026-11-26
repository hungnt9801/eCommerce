# Testplan `NOT-001` — 26/11/2026

- SRS: `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (§1 BR · §3 máy trạng thái · §6 hợp đồng · §9 quyền · §13 NFR · §14.1 TC · §14.3 DoD · §15.5 nợ)
- Kiến trúc bổ trợ: `docs/features/notifications/NOT-001-arch.md` (§13.1 map tầng · §13.2 7 negative control)
- Nhánh: `feat/gd3-wp22-thong-bao` · Phiên `N86` · Hàng `W22-4` · Bước **F4 QA** (F5 REV làm sau)
- Trạng thái nền: `W22-2` (BE) + `W22-3` (FE) XONG; SRS `v0.7 as-built`.
- Tầng test dùng: **Domain thuần** (không DB) · **Application** (`RepoGia`/fake, không host ABP) · **Integration** (Testcontainers PostgreSQL 17) · **FE Vitest**.

## 1. Phủ BR

> Mỗi `BR-NOT-001-xxx` của SRS §1 phải có **ít nhất một** test thật (mã BR ghi trong tên/comment test).

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-NOT-001-001** (render từ version đã `Published`; cấm nối chuỗi trong code) | `test/eCommerce.Tests/Notification/NotificationQueueAppenderTests.cs` · `TC01_enqueue_happy_path_tao_muc_Moi_va_event_Enqueued` · `Template_chua_san_sang_thi_NEM_0012_truoc_moi_ghi` | Application | ✅ |
| **BR-NOT-001-002** (`IdempotencyKey` ổn định; cùng `(TenantId, Key)` ⇒ tối đa một mục) | `…/NotificationQueueAppenderTests.cs` · `TC03_trung_khoa_CUNG_payload_tra_AlreadyQueued_khong_ghi_lan_hai` · `test/eCommerce.IntegrationTests/PostgreSqlNotificationTests.cs` · `TC19_chan_hai_muc_cung_IdempotencyKey_khi_TenantId_NULL` | App + Integration | ✅ (hàng rào DB: `TC-19`) |
| **BR-NOT-001-003** (ghim `TemplateVersionId`; render lại cùng nội dung) | `…/NotificationTemplateAndBatchTests.cs` · `TC08_publish_version_moi_khong_doi_noi_dung_muc_ghim_v1` · `TC08_version_archived_van_doc_duoc_theo_Id` | Domain | ✅ |
| **BR-NOT-001-004** (placeholder trong whitelist + đủ giá trị bắt buộc; cấm render rỗng im lặng) | `…/NotificationRendererTests.cs` · `TC35_thieu_truong_bat_buoc_thi_NEM_0013` · `TC35_placeholder_ngoai_whitelist_thi_NEM_0015` · `TC35_placeholder_tuy_chon_thieu_thi_thay_chuoi_rong_khong_nem` | Domain | ✅ |
| **BR-NOT-001-005** (chuyển trạng thái sai ⇒ NÉM `0011`; cấm lùi; trạng thái kết thúc hấp thụ) | `…/NotificationStateMachineTests.cs` · `TC05_hang1…hang15` · `TC06_khong_lui_duoc_tu_DaBanGiao` · `TC05_hang12_moi_trang_thai_hap_thu_khong_co_canh_ra` | Domain | ✅ |
| **BR-NOT-001-006** (`NotificationQueueEvents` APPEND-ONLY) | `…/NotificationConventionsTests.cs` · `TC12_NotificationQueueEvent_khong_co_setter_cong_khai_va_khong_co_Update_Delete` | Domain (reflection) | ✅ |
| **BR-NOT-001-007** (mọi ghi qua UoW; `QueueItem` + `QueueEvent` + outbox cùng transaction) | `…/NotificationQueueAppenderTests.cs` · `BR007_ghi_muc_va_event_va_outbox_trong_MOT_uow` · `BR007_render_loi_thi_KHONG_mo_uow_va_khong_ghi_gi` (+ `UowManagerGia` khẳng định `requiresNew:true`, `isTransactional:true`) | Application | ✅ (atomic rollback DB: xem §5 `TC-36`) |
| **BR-NOT-001-008** (bàn giao chỉ qua `INotificationSender` + `UPDATE … WHERE Status=ChoGui`; cấm gọi sender khi chưa giành khoá) | `…/NotificationStateMachineTests.cs` · `TC05_hang5_GianhBanGiao_ChoGuiSangDangBanGiao` · `TC05_hang5_guard_sai_trang_thai_thi_NEM_0011` | Domain | ⚠️ phần guard; concurrency 2-job: xem §5 `TC-09`/`TC-11` |
| **BR-NOT-001-009** (khoá sang `NOT-002` là khoá **đã lưu**; cấm sinh `Guid` mới) | `…/NotificationQueueAppenderTests.cs` · `TC10_khoa_idempotency_nguyen_ven_sang_muc_va_sender` | Application | ✅ |
| **BR-NOT-001-010** (cấm OTP/PII; người nhận mã hoá + hash; `Rendered*`/reason đã che) | `…/NotificationMaskerTests.cs` · `TC14_email_giu_ky_tu_dau_va_domain` · `TC14_sdt_di_dong_bi_che_ke_ca_co_dau_phan_cach` · `TC14_sdt_di_dong_dau_84_bi_che` · `TC14_PAN_chi_giu_4_cuoi` · `TC14_tham_so_query_token_sig_code_bi_che` · `TC14_cap_otp_token_bi_che` · `TC14_tien_ngay_madon_hotline_so_ban_giu_nguyen` · `TC14_chay_hai_lan_cho_cung_ket_qua` · `TC14_null_hoac_rong_tra_chuoi_rong` · `TC14_bon_cot_da_che_chi_duoc_gan_tu_CheBiMat_trong_Domain` (+ `…/NotificationQueueAppenderTests.cs` · `TC14_appender_mask_than_va_fingerprint_tren_gia_tri_da_luu`; `…/NotificationQueueAppServiceTests.cs` · `TC14_CancelAsync_ly_do_chua_SDT_bi_mask_o_muc_va_event`; `…/NotificationConventionsTests.cs` · `TC41_PII_tat_chi_luu_ma_hoa_va_bam_khong_luu_plaintext` · `TC17_moi_ETO_deu_co_CorrelationId_va_khong_mang_PII`) | Domain (hàm thuần) + App + Reflection | ✅ (`D-269`: mask thật — `TC-14` đã phủ) |
| **BR-NOT-001-011** (tiền từ `decimal` qua một hàm định dạng; cấm `double`/`float`; cấm `==`) | `…/NotificationRendererTests.cs` · `TC23_dinh_dang_tien_VND_khong_thap_phan_vi_VN` · `…/NotificationConventionsTests.cs` · `TC22_khong_co_double_float_trong_chu_ky_thanh_vien_module` | Domain | ✅ |
| **BR-NOT-001-012** (khoá phân tán từ `NotificationLockKeys`; không tiền tố `ecommerce:`; không trùng ABP) | `…/NotificationConventionsTests.cs` · `TC20_TC21_khoa_phan_tan_khong_tien_to_ecommerce_va_khong_dung_khoa_ABP` | Domain (reflection) | ✅ |
| **BR-NOT-001-013** (mọi biên vào chống trùng: ETO · API · job · phát lại) | `…/NotificationQueueAppenderTests.cs` · `TC03_trung_khoa_CUNG_payload_tra_AlreadyQueued…` · `test/eCommerce.IntegrationTests/PostgreSqlNotificationTests.cs` · `TC19_chan_hai_muc_cung_IdempotencyKey_khi_TenantId_NULL` · `…/NotificationQueueAppServiceTests.cs` · `TC37_phat_lai_lan_hai_la_replay_2` | App + Integration | ✅ (biên job bàn giao: xem §5 `TC-09`) |
| **BR-NOT-001-014** (phát lại dùng khoá MỚI `{gốc}:replay-{n}`; mục gốc ở lại `Loi`) | `…/NotificationQueueAppServiceTests.cs` · `TC37_phat_lai_tao_muc_MOI_khoa_replay_1_goc_o_lai_Loi` · `TC37_phat_lai_lan_hai_la_replay_2` · `…/NotificationStateMachineTests.cs` · `TC05_hang11_DanhDauPhatLai_Replayed_FromToLoi` | Application + Domain | ✅ |
| **BR-NOT-001-015** (metric trong `eCommerceMetricNames` + `XC-005` §5.1 + test; nhãn chỉ từ `Allowed`) | `test/eCommerce.Tests/Observability/MetricsTests.cs` (registry so đúng tập + chiều ngược + `Allowed ∩ Forbidden = ∅`) · `…/NotificationQueueAppenderTests.cs` · `TC01_enqueue_happy_path…` (đếm `notification_queued_total`), `Render_thieu_truong…` (đếm `notification_render_failed_total`), `TC30_…` (đếm `notification_window_suppressed_total`) | Domain (reflection) + App | ✅ |
| **BR-NOT-001-016** (`[Authorize]` cấp CLASS + permission định nghĩa; cấm magic string) | `test/eCommerce.Tests/Notification/NotificationSecurityTests.cs` · `TC38_ba_app_service_Notification_deu_co_Authorize_o_cap_CLASS` · `TC38_chin_permission_Notification_deu_duoc_DINH_NGHIA` · `TC38_ba_controller_v1…` · `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs` | Domain (reflection) | ✅ (HTTP 401/403: xem §5 `TC-38`) |
| **BR-NOT-001-017** (5 `DbSet` + `ConfigureNotification(...)` trong `OnModelCreating`) | `test/eCommerce.Tests/Notification/NotificationPersistenceTests.cs` · `TC39_nam_entity_Notification_deu_co_DbSet_rieng` · `TC39_ConfigureNotification_duoc_goi_nam_bang_duoc_map` · `TC39_script_tao_bang_sinh_ra_du_nam_bang_Notification` · `test/eCommerce.Tests/EfCoreRegistration/DbSetRegistrationTests.cs` | Domain (model) | ✅ |
| **BR-NOT-001-018** (ETO mang `CorrelationId`) | `…/NotificationConventionsTests.cs` · `TC17_moi_ETO_deu_co_CorrelationId_va_khong_mang_PII` | Domain (reflection) | ✅ |
| **BR-NOT-001-019** (cấm gọi app service module khác; chỉ tiêu thụ ETO) | `test/eCommerce.Tests/Notification/NotificationBoundaryTests.cs` · `TC33_Notification_Application_KHONG_tham_chieu_module_khac` · `TC33_khong_constructor_hay_field_nao_goi_AppService_module_khac` | Domain (reflection) | ✅ |
| **BR-NOT-001-020** (`REP-*` cấm query bảng giao dịch; đọc read-model) | `test/eCommerce.Tests/Notification/NotificationReportingIsolationTests.cs` · `TC28_khong_assembly_Reporting_nao_tham_chieu_eCommerce_Notification` · `TC28_khong_kieu_Reporting_nao_dung_bang_giao_dich_NOT001` | Domain (reflection) | ✅ |
| **BR-NOT-001-021** (`TenantId` mọi mục; unique index có `NULLS NOT DISTINCT`) | `test/eCommerce.IntegrationTests/PostgreSqlNotificationTests.cs` · `TC19_chan_hai_muc_cung_IdempotencyKey_khi_TenantId_NULL` · `TC30_partial_index_chan_hai_muc_cung_noi_dung_trong_CUNG_cua_so` · `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` · `F1_du_73_index_deu_dung_NULLS_NOT_DISTINCT` (danh sách 7 index `NOT-001`) | Integration | ✅ |
| **BR-NOT-001-022** (không sở hữu "đã tới"/chi phí/opt-out/quiet-hours/`OrderId`) | `…/NotificationConventionsTests.cs` · `TC40_khong_co_cot_da_toi_chi_phi_opt_out_quiet_hours_OrderId` | Domain (reflection) | ✅ |
| **BR-NOT-001-023** (GĐ3 chỉ `Text`; `Html` ⇒ `0020` trước mọi ghi; cấm hạ cấp im lặng) | `…/NotificationTemplateAndBatchTests.cs` · `TC42_version_Html_thi_NEM_0020` · `TC42_version_hop_le_co_BodyFormat_Text` | Domain | ✅ |
| **BR-NOT-001-024** (`Subject` + giá trị thay vào loại `\r`/`\n`/NUL ở một hàm thuần) | `…/NotificationRendererTests.cs` · `TC43_Subject_va_gia_tri_thay_vao_loai_CR_LF_NUL` · `TC43_ham_lam_sach_loai_dung_ba_ky_tu_dieu_khien` | Domain | ✅ |
| **BR-NOT-001-025** (lô chỉ marketing; `Moi → ChoDuyet` khi có `BatchId`; ETO không `BatchId`; fail-closed) | `…/NotificationQueueAppenderTests.cs` · `Hang2_muc_co_BatchId_vao_ChoDuyet_va_phat_QueuedForApproval` · `BR025_lo_bang_template_Transactional_van_vao_ChoDuyet_KHONG_gui_thang` · `BR025_ETO_nghiep_vu_KHONG_mang_BatchId` · `apps/admin-web/lib/notification.test.ts` (`D-268: màn lô gửi lọc Purpose = Marketing phía client`, `người tạo lô KHÔNG được tự duyệt`) | App + FE | ✅ (server lọc `Marketing`: xem §5 `TC-45`) |

- Tổng BR: **25** · đã phủ: **25** (mỗi BR có ≥ 1 test thật) · chưa phủ: **0**.
- Test bổ sung ở `W22-4` (mới, trong vùng QA):
  - `test/eCommerce.Tests/Notification/NotificationPersistenceTests.cs` — `TC-39` (BR-017).
  - `test/eCommerce.Tests/Notification/NotificationSecurityTests.cs` — `TC-38` reflection (BR-016).
  - `test/eCommerce.Tests/Notification/NotificationBoundaryTests.cs` — `TC-33` (BR-019).
  - `test/eCommerce.Tests/Notification/NotificationReportingIsolationTests.cs` — `TC-28` (BR-020).
  - `test/eCommerce.Tests/Notification/NotificationQueueAppServiceTests.cs` — `TC-37` phát lại (BR-014/BR-008 replay-lock).
  - `test/eCommerce.Tests/Notification/NotificationQueueAppenderTests.cs` — thêm `TC-30` (khử trùng cửa sổ tuần tự), `BR-007` (một UoW), `BR-025` (fail-closed + ETO không `BatchId`).
  - `test/eCommerce.IntegrationTests/PostgreSqlNotificationTests.cs` — `TC-19` + `TC-30` trên PostgreSQL thật (BR-002/021, ADR-12).
- Test bổ sung ở `W22-4` **vòng 2** (`D-269` — mask thật, trong vùng QA):
  - `test/eCommerce.Tests/Notification/NotificationMaskerTests.cs` — **10 ca** `TC-14` (hàm thuần `CheBiMat`: email · SĐT di động + `+84` · PAN · query `token/sig/signature/code/key/password/otp` · giữ tiền/ngày/`MaDon`/`1900…`/`024…` · idempotent · `null`/rỗng · **quét mã nguồn** bốn cột đã-che ở tầng Domain).
  - `…/NotificationQueueAppenderTests.cs` — `TC14_appender_mask_than_va_fingerprint_tren_gia_tri_da_luu` (mask **một lần** sau render, fingerprint trên giá trị đã lưu).
  - `…/NotificationQueueAppServiceTests.cs` — `TC14_CancelAsync_ly_do_chua_SDT_bi_mask_o_muc_va_event` (lý do huỷ chứa SĐT bị mask ở cả mục và event).

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (đầu `W22-4`) | Sau (đo thật, `N86` vòng 2) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **2251** | **2283** | **+32** ca (lần 1 +20 · vòng 2 `D-269` +12); không xoá/skip/nới; 0 fail |
| Domain invariant / Unit (`Notification`) | **81** | **113** | +32; vòng 2 +12: 10 ca `NotificationMaskerTests` (hàm thuần Domain) + 2 ca Application (`TC14_appender…`, `TC14_CancelAsync…`) nằm chung namespace `Notification` (đo `--filter FullyQualifiedName~eCommerce.Tests.Notification` → 113) |
| Application (`Notification`) | (trong 81) | (trong 113) | `NotificationQueueAppenderTests` (16 ca, appender thật + `RepoGia`), `NotificationQueueAppServiceTests` (4 ca, `RetryAsync`/`CancelAsync` thật + `RepoGia`), ≥ 3 ca/app-service-equivalent: happy · sai dữ liệu · sai quyền/khoá |
| API `/api/v1` (200 + 401/403) | — | **một phần** | `BR-016` phủ **nguyên nhân gốc** bằng reflection (class `[Authorize]` + permission định nghĩa + controller không `AllowAnonymous`). **HTTP 401/403 thật cần host** ⇒ xem §5 |
| Integration PostgreSQL | **106** | **110** | **+4** `PostgreSqlNotificationTests` (`TC-19`, `TC-30` ×3); Testcontainers `postgres:17-alpine`, ~0.1 s sau khi container lên |
| UI Vitest / Playwright | **45** | **45** | `apps/admin-web/lib/notification.test.ts` = 20 ca (gồm phủ FE của `TC-45`/`D-268`); **không** đụng FE ở `W22-4` ⇒ không thêm. E2E Playwright **không** thuộc WP (chưa có luồng lô gửi end-to-end) |
| OpenAPI drift | — | **N/A** | `W22-4` **không** đổi DTO/route ⇒ `pnpm gen:api` không đổi `schema.d.ts` (không chạy lại) |

> 📌 Đo thật (phiên `N86`, sau vòng 2): `dotnet test test/eCommerce.Tests` → **2283/2283 pass**; `dotnet test test/eCommerce.IntegrationTests` → **110/110 pass**. Số ca **chỉ tăng**.

## Negative control (bắt buộc — đỏ → xanh)

> Bảng **bằng chứng chạy thật** dưới đây do `tools/agents/negative-control.ps1` chèn (áp đột biến → test phải **ĐỎ** → khôi phục byte gốc → test phải **XANH**). `TestCommand` **build lại** (không `--no-build`) vì script sửa mã nguồn.

| Ngày | Mã | Tệp:dòng | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-26 | NC-1 | test/eCommerce.IntegrationTests/Assets/schema.sql:4353 | ON "NotificationQueueItems" ("TenantId", "IdempotencyKey") NULLS NOT DISTINCT; -> ON "NotificationQueueItems" ("TenantId", "IdempotencyKey"); | dotnet test test/eCommerce.IntegrationTests --filter "FullyQualifiedName~PostgreSqlNotificationTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-4 | modules/Notification/eCommerce.Notification.Domain/Queue/NotificationQueueEntities.cs:256 | Nem(Status, NotificationQueueStatus.DangBanGiao); -> _ = NotificationQueueStatus.DangBanGiao; | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC05_hang5_guard_sai_trang_thai_thi_NEM_0011" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-5 | modules/Notification/eCommerce.Notification.Domain/Rendering/NotificationRendering.cs:134 | var subject = ThayThe(template.Subject ?? string.Empty, whitelist, data, moneyFormatter); -> var subject = ThayThe(template.Subject ?? string.Empty, whitelist, data, moneyFormatter) + DateTime.Now.Ticks; | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~TC04_render_tai_lap_100_lan_cung_ket_qua" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-8 | modules/Notification/eCommerce.Notification.Domain/Rendering/NotificationMasker.cs:77 | NC-8 bo mask email (masker thanh no-op mot phan) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~NotificationMaskerTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

### Kế hoạch 7 negative control theo `ARCH §13.2`

| # | Đột biến | Ca phải đỏ | Khả thi phiên này |
|---|---|---|---|
| NC-1 | Bỏ `NULLS NOT DISTINCT` ở index `(TenantId, IdempotencyKey)` | `TC-19` (chèn 2 cùng khoá, `TenantId = NULL`) | ✅ đã chạy (bảng trên) |
| NC-2 | Bàn giao bằng `if (status == ChoGui) {…}` **không** `UPDATE … WHERE` | `TC-09` (2 job song song ⇒ 2 lần gọi sender) | ❌ **chưa chạy** — cần host + DB thật với `ExecuteUpdateAsync` của worker; xem §5 |
| NC-3 | Nhánh `23505` khử trùng dùng lại UoW đã abort (bỏ `requiresNew`) | `TC-30` nhánh **đua** (mất mục/event hoặc `25P02`/`500` thay vì `0019`) | ❌ **chưa chạy** — nhánh đua cần appender trên PostgreSQL thật; phần tuần tự + partial index đã phủ |
| NC-4 | Guard trạng thái **không ném** `0011` (bỏ `Nem(...)` ở `GianhBanGiao`) | `TC-05` hàng 5 guard sai | ✅ đã chạy (bảng trên) |
| NC-5 | Render đọc `DateTime.Now` (bỏ thuần) | `TC-04` (100 lần khác nhau) | ✅ đã chạy (bảng trên) |
| NC-6 (khuyến nghị) | Gắn nhãn `phone` vào `notification_queued_total` | test nhãn (`TC-16`) | ❌ **chưa chạy** — chưa có test đọc *tag* của instrument; xem §5 |
| NC-7 (khuyến nghị) | Thêm metric phát ra **không** khai trong `All` | `MetricsTests` (chiều "mọi tên phát ra ⊆ All") | ❌ **chưa chạy** — `MetricsTests` phủ registry nhưng chưa mutation trong phiên này |
| NC-8 (`D-269`) | `NotificationMasker.CheBiMat` trả `noiDung` (bỏ mask — đúng lỗi cũ) | `NotificationMaskerTests` (`TC14_email…`, `TC14_PAN…`, `TC14_cap_otp…`… phải đỏ) | ❌ **chưa chạy được ở vai QA** — `tools/agents/negative-control.ps1` từ chối tệp đích chưa commit (`NotificationMasker.cs` đang `??`, `D-269` chưa commit) + hook vùng ghi `qa` chặn `modules/**`; **ORCH chạy sau khi `be` commit** `D-269` |

> ⚠️ `NC-8` là nhóm luật **PII/bảo mật** quan trọng, nhưng phiên này **không có** bằng chứng đỏ→xanh qua script (lý do trên). Các ca `TC-14` đã chạy **xanh** và được viết để **đỏ được** (mỗi khẳng định bám đúng một luật (i)–(vi)); ORCH cần chạy `NC-8` trước khi đóng `W22-4`. Ba nhóm còn lại (idempotency/DB `NC-1`, trạng thái `NC-4`, render thuần `NC-5`) đã có bằng chứng.

- Xác nhận sau mỗi ca: `git status --porcelain -- modules/ src/ test/eCommerce.IntegrationTests/Assets/` rỗng (script tự khôi phục byte gốc + tự kiểm lại).

## 3. Cổng (đo trong phiên N86)

```powershell
dotnet build eCommerce.slnx --no-incremental      # 0 error / 9 warning (bằng baseline phiên)
dotnet test test/eCommerce.Tests --no-build        # 2283 pass · 0 fail · trước 2271 (+12 vòng 2; +32 so đầu W22-4)
dotnet test test/eCommerce.IntegrationTests --no-build   # 110 pass · 0 fail · trước 106 (+4)
pnpm --prefix apps/admin-web test                  # 45 pass (không đổi)
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/NOT-001-testplan.md -Srs docs/features/notifications/not-001-template-hang-doi-thong-bao.md
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error**, **9 warning** (không phát sinh warning mới).
- `dotnet test test/eCommerce.Tests` → **2283/2283 pass**, exit **0** (vòng 2 `D-269`: +12 ca `TC-14`).
- `dotnet test test/eCommerce.IntegrationTests` → **110/110 pass**, exit **0** (Docker chạy).
- `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan …` → **`KET QUA: DAT`** (exit 0).

## 4. Chưa kiểm được (lý do cụ thể — KHÔNG đánh dấu đã phủ)

> ✅ `TC-14` (nội dung lưu đã mask) **đã ra khỏi danh sách này ở vòng 2 `D-269`** — mask thật + 10 ca `NotificationMaskerTests` + 1 ca appender + 1 ca `CancelAsync`; `BR-NOT-001-010` chuyển ⚠️ → ✅.

1. **`TC-02` (1.000 lần có song song) — CHƯA KIỂM ĐƯỢC ở dạng integration.** Appender chạy với `RepoGia` in-memory không mô phỏng được đua `23505`. Đã phủ: (a) ngữ nghĩa idempotent tuần tự (`TC03_…AlreadyQueued…`), (b) **hàng rào DB thật** `TC-19` (unique `(TenantId, IdempotencyKey)` chặn trùng khi `TenantId = NULL`). Cần host ABP + PostgreSQL để chạy 1.000 lần song song.
2. **`TC-09` (2 job bàn giao song song) / `TC-11` (bận khoá ⇒ skip + metric) — CHƯA KIỂM ĐƯỢC.** Worker `NotificationDispatchWorker` dùng `ExecuteUpdateAsync` (EF) + `PeriodicBackgroundWorkerContext` ⇒ cần host + PostgreSQL thật. Đã phủ phần **guard trạng thái** (`TC05_hang5_…NEM_0011`).
3. **`TC-13` (0 OTP trong log/DB) — CHƯA KIỂM ĐƯỢC** ở dạng quét nội dung log thật. Đã phủ bất biến PII tắt bằng reflection (`TC-41`) + mask nội dung/người nhận (`TC-14`).
4. **`TC-18` (correlation xuyên ETO + log) — CHƯA KIỂM ĐƯỢC** đầy đủ. Đã phủ: mọi ETO có `CorrelationId` (`TC-17`); event phát lại mang `CorrelationId` gốc (`TC-37`).
5. **`TC-24` (bền vững qua restart) — CHƯA KIỂM ĐƯỢC** (cần dựng/tắt host thật).
6. **`TC-26` (lô `ChoDuyet` ⇒ 0 sender; NÉM `0018` nếu gọi thẳng) — CHƯA KIỂM ĐƯỢC** ở đường worker. Đã phủ: `ChoDuyet → ChoGui` chỉ qua `DuyetLo` (`TC05_hang4`); appender đặt `ChoDuyet` khi có `BatchId` (`Hang2`, `BR025`).
7. **`TC-27` (cách ly tenant) — CHƯA KIỂM ĐƯỢC** (cần host 2 tenant thật; `RepoGia` không có query filter tenant).
8. **`TC-30` nhánh **đua** (`23505` ⇒ UoW mới `requiresNew` ⇒ `0019` + đúng 1 `Suppressed`) — CHƯA KIỂM ĐƯỢC.** Đã phủ: nhánh **tuần tự** (`TC30_khu_trung_cua_so_marketing…`) + **partial index trên PostgreSQL thật** (`TC30_partial_index…`, `TC30_ban_ghi_bi_khu_trung_bucket_NULL…`, `TC30_hai_muc_cung_noi_dung_KHAC_cua_so…`).
9. **`TC-36` (rollback DB khi ghi event/outbox lỗi) — CHƯA KIỂM ĐƯỢC.** Đã phủ: **một** lần mở UoW cho cả 3 lần ghi (`BR007_ghi_muc_va_event_va_outbox_trong_MOT_uow`, `BR007_render_loi_thi_KHONG_mo_uow…`). Cần fault injection trên DB thật để chứng minh rollback.
10. **`TC-38` phần HTTP `200`/`401`/`403` — CHƯA KIỂM ĐƯỢC** (cần `WebApplicationFactory`). Đã phủ **nguyên nhân gốc**: 3 app service `[Authorize]` cấp class, 9 permission định nghĩa, 3 controller `[Authorize]` cấp class + 0 `[AllowAnonymous]` (`NotificationSecurityTests`), và `BatBienPhanQuyenTests` toàn hệ thống.
11. **`TC-45` phần "server lọc `Marketing`" (`GetTemplatesForBatch`) — CHƯA KIỂM ĐƯỢC**: endpoint **không tồn tại** (nợ BE `N1`, SRS §15.5). FE lọc client (`D-268`) **đã phủ** bằng 20 ca Vitest.
12. **`TC-16` (tag cardinality) — phủ gián tiếp**: `MetricsTests` chứng minh `Allowed ∩ Forbidden = ∅` (registry), nhưng **chưa** có test đọc *tag thực tế* phát ra từ module ⇒ NC-6 chưa chạy.
13. **NFR đo hiệu năng** (`NFR-001` p95 < 5 ms · `NFR-008` `EXPLAIN` · `NFR-012` p95 < 30 s · `NFR-016` ≥ 200 mục/s) — **CHƯA ĐO** (không thuộc phạm vi test chức năng của `W22-4`; cần harness tải).
14. **Redis chết ⇒ bất biến vẫn đúng** (DoD §14.3) — **CHƯA KIỂM**: cần dựng host + dừng Redis thật; `TC-19`/`TC-30` chứng minh hàng rào **DB** độc lập với lock.
15. **`NC-8` (mask `D-269`) — CHƯA CHẠY qua script** (chi tiết ở bảng NC): tệp đích chưa commit + hook vùng ghi `qa` chặn `modules/**` ⇒ ORCH chạy sau khi `be` commit.
