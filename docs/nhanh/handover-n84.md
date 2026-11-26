# Handover N84 — WP-22 / hàng `W22-2` (F3 BE) XONG: module `Notification` + migration + 81 test

> Phiên `N84` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W22-2` · WP-22 · mã `NOT-001` · nhánh
> `feat/gd3-wp22-thong-bao` (base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` commit điểm xanh `7f23726`) · 26/11/2026.
> Phạm vi phiên: **F3 BE** — domain + app service + endpoint + migration (`expand-only`, `pg_dump` trước khi áp) + test đơn vị.

## 1. Đã xong

- **F0 — điều kiện vào**: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` **exit 0** (XANH) ở mở phiên.
- **F3 BE — module mới `modules/Notification/`** (4 project, 27 tệp nguồn):
  - Domain: 8 enum (chỉ thêm cuối), `NotificationTemplate` + `NotificationTemplateVersion` (bất biến sau publish ⇒ `0016`, `ContentHash` SHA-256), `NotificationBatch`, `NotificationQueueItem` (**15 hàng state machine**, guard sai **NÉM** `0011`, trạng thái kết thúc **hấp thụ**, hàng 14 vế `ExpiresAtUtc IS NULL`, hàng 7 không tăng `AttemptCount`), `NotificationQueueEvent` append-only, `NotificationRecipient` (mã hoá + hash một cửa), renderer **hàm thuần** (loại CR/LF/NUL), money formatter VND, `NotificationLockKeys` (`notification:…`, 0 tiền tố `ecommerce:`), `NotificationErrorCodes` dải `0010–0020`, seed contributor 3 template hệ thống (`TenantId = NULL`, idempotent, 0 PII).
  - Application.Contracts: DTO, 3 interface app service, `INotificationQueueAppender`, `INotificationSender` **canonical** (§4.4), 4 ETO có `CorrelationId` + 0 PII (implement `INotificationEvent`).
  - Application: 3 app service `[Authorize]` **cấp class**, appender (pre-check + UNIQUE + `23505` ⇒ UoW mới `requiresNew`, cửa sổ khử trùng Marketing), renderer/masker, `ChuaCoKenhGuiSender` fail-closed, `NotificationDispatchWorker` 3 pha (A lease, B hết hạn, C1 sẵn sàng / C2 giành bàn giao bằng `UPDATE … WHERE Status`), `EnableDispatch` mặc định `false`.
  - EntityFrameworkCore: `ConfigureNotification` **một lần**, 5 bảng + CHECK, **7** unique index `NULLS NOT DISTINCT` (tên ≤ 63 byte).
  - Nối hệ thống: 5 `DbSet`, `ConfigureNotification` trong `eCommerceDbContext`, 2 `[DependsOn]` (EF + Host), **9** permission (`eCommerce.Notification.*`, **không** có `Reports.View` theo `D-255`), map HTTP `0010–0020`, 7 metric (`All` **19 → 26**) + `KyHieuMetric` + emitter + `DoThoiGianGiuKhoa` nhãn `notification`.
  - **Sửa 2 lỗi chức năng** sau lượt đầu: (a) appender đưa mục có `BatchId` sang `ChoDuyet` + event `QueuedForApproval` (hàng 2) ⇒ `ApproveBatchAsync` thấy mục của lô; (b) thêm `SendManualRequest.BatchId` (additive) nên API nạp được lô.
- **Migration** `20261126132231_Added_Notification_Template_Queue`: 5 `CREATE TABLE` + index + CHECK, **expand-only** (0 `DROP`/`ALTER` bảng cũ); 7 `CreateIndex(unique:true)` thay bằng `Sql("CREATE UNIQUE INDEX … NULLS NOT DISTINCT …")`. Áp bằng `dotnet run --project src/eCommerce.DbMigrator` (KHÔNG `dotnet ef database update`); seed chạy 2 lần vẫn **3** template.
- **BA** (3 lượt): SRS `NOT-001` **v0.5 → v0.6** (9 `docDeviation` #1–#9 + P3 #1-v2 **4 event** + 15 P3 SRS-side); đóng **2 P2** docrev (§7.1 consumer `REP-* (chỉ)`, §9 bỏ permission `Reports.View`); `XC-005` §5.1 +7 metric; SRS §6.1 `SendManualRequest.BatchId`.
- **DOCREV** (2 lượt): kiểm nhanh v0.6 `DOC-OK` · P1=0 · P2=2 → BA đóng → **P1=0/P2=0**, artifact `docs/reviews/NOT-001-n84-docreview.md` (không đè bản vòng 4).
- **DEC**: **`D-267`** (giữ gauge `notification_queue_backlog`; `qa` mở whitelist hậu tố `_backlog`; cấm đổi tên metric trong WP-22).
- **QA** (2 lượt): **81 ca** đơn vị mới (`test/eCommerce.Tests/Notification/**`: 15 hàng state machine, renderer 100 lần, `0013/0015/0016`, appender 4 event + `0010`, hàng 2/4, quét `TC-12/17/20/21/22/40/41/44`); `MetricsTests` theo `D-267`; `PostgreSqlSchemaTests` **94 → 101** + 7 tên NND; `schema.sql` regen; `RangBuocChuaAnhXa.txt` +23.
- **Negative control**: **NC-N84-1** (khởi tạo `Moi → ChoGui`) ⇒ test `NotificationStateMachineTests` **ĐỎ** → khôi phục **XANH**.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2170/2170** |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở phiên) | 0 | **49/49** |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` (mở phiên) | 0 | **XANH** |
| `dotnet build eCommerce.slnx --no-incremental` (đóng phiên) | 0 | **0 error / 9 warning** (không warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (đóng phiên) | 0 | **2251/2251** (+81) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` (đóng phiên) | 0 | **106/106** |
| `pwsh -File tools/kiem-tat-ca.ps1` (đóng phiên) | 0 | **10/10 · 383,6 s** |
| `negative-control.ps1` NC-N84-1 | 0 | **ĐỎ → XANH** |
| `check-feature-doc.ps1 -WP14 -Path <SRS>` | 0 | **DAT** · 16/16 heading · 25/25 BR |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/NOT-001-n84-docreview.md` | 0 | **DAT** (DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md -Id D-267` | 0 | **DAT** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau handover) | 0 | **PASS** · 266 tệp · 957 link · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |
| `node --test .claude/hooks/test/hooks.test.mjs` (đóng phiên) | 0 | **49/49** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W22-2`** (G3 xanh, migration áp xong, test + NC đạt). Các mục còn lại là **cắt có quyết định** sang hàng sau của cùng WP:

- **`GetTemplatesForBatch` (BR-025 / TC-45)** — SRS §6.1/BR-025/TC-45 đòi danh sách template cho lô **lọc `Purpose = Marketing`**, nhưng **ARCH §4.1 không liệt kê** hàm này nên BE chưa có; `qa` phát hiện và **không** vá sản phẩm. Chuyển **W22-3** (FE cần cho màn lô) hoặc **W22-4** (QA phủ BR-025); **BA** cần đồng bộ câu chữ BR-025 ("`Moi → ChoDuyet` **chỉ** sinh trong `CreateBatchAsync`" vs as-built: appender sinh khi enqueue có `BatchId`) trong cùng PR xếp chồng.
- **`CreateBatchDto` không mang danh sách người nhận** — luồng đầy đủ = tạo header lô → enqueue từng người nhận kèm `BatchId` → duyệt lô (đã ghi SRS §6.1). Nếu W22-3 muốn 1 API tạo lô + sinh mục trong 1 bước ⇒ bổ sung DTO ở hàng BE sau.
- **8/25 BR chưa phủ unit** (BR-016/017/019/020, một phần BR-007/008/014/025) + **TC-30** (cửa sổ khử trùng tuần tự/song song, đua `23505`) → **W22-4** (F4 QA + negative control đầy đủ theo ARCH §13.2).
- **ARCH §13.1 còn "TC-01 3 event"** — ARCH **không sửa** sau `DOC-OK`; `D-265` đã chốt **4 event**, SRS đã đúng.

## 3. Migration đã áp

- `20261126132231_Added_Notification_Template_Queue` — **đã áp** dev bằng `dotnet run --project src/eCommerce.DbMigrator`, có dòng `__EFMigrationsHistory`.
- `pg_dump` **trước** khi áp: `artifacts/pg-dump/N84-W22-2.dump` — **457.042 B** (K-01, `D-253` ③).
- 5 bảng: `NotificationTemplates` · `NotificationTemplateVersions` · `NotificationBatches` · `NotificationQueueItems` · `NotificationQueueEvents`; **7** unique index `NULLS NOT DISTINCT`; **3** template seed (`TenantId = NULL`), chạy seed lần 2 không đổi.
- Đường lùi: `Notification:EnableDispatch=false` (0 tin ra ngoài) · `git revert` + `migrations remove` (trước merge) · migration `Drop` theo thứ tự ngược FK (sau merge).

## 4. Quyết định mới

- **`D-267`** — TẠM CHỐT (DEC, uỷ quyền): **giữ tên gauge `notification_queue_backlog`** (đã duyệt ở ARCH §10/SRS §11/`XC-005` §5.1); `qa` thêm `_backlog` vào whitelist hậu tố `MetricsTests` (đúng 1 dòng); **cấm** đổi tên metric trong WP-22. Lùi: xoá 1 mệnh đề test.
- Không có câu **chờ người** mới (decider trả `TAM_CHOT`); các câu vùng cấm vẫn ở §5.

## 5. Nợ / rủi ro

- **BR-025 / `GetTemplatesForBatch`** — lệch SRS↔ARCH, chủ **BA/W22-3**; xem §2.
- **`CreateBatchDto` thiếu danh sách người nhận** — luồng lô 2 bước; nợ thiết kế, ghi ở SRS §6.1.
- **`A` điều kiện `D-266`**: 15 P3 + P3 #1-v2 đã đóng ở SRS v0.6; 7 negative control đầy đủ **chưa** chạy (mới NC-N84-1) — W22-4.
- **PII thật / OTP / `IgnoreQueryFilters`**: 0 (test `TC-41/TC-13`); template seed 0 PII.
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (P-21, `D-71`/§A.5); `tich-hop/gd3` chỉ nhận ở `W22-5` (G6).
- **Hàng đợi xem lại**: **168** dòng "Đang chờ" (mở phiên 167; **+1** `D-267`).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền):** chọn NCC kênh 2 + khoá HMAC production (R3-6); `pg_dump` trước `migrations add` (đã làm lần này); merge `main`; duyệt lô `D-267`.
- **Cảnh báo hook** (`artifacts/agents/canh-bao.jsonl`, 2 dòng mới gắn N84): `be` bị báo **ghi ngoài vùng** khi thêm 7 dòng metric vào `XC-005` (ORCH đã `git restore`; BA thêm lại đúng vùng) + báo sửa dòng 11 SRS (thực chất là header **của BA**, cùng tệp — dương tính giả của hook). Các vai `ba`/`docrev`/`be`/`decider` bị báo "thiếu khối JSON cuối" ở lượt hand-back đầu (harness không cấp `last_assistant_message` đúng lúc) — **tất cả đã trả JSON**. **0** tệp ngoài vùng lọt sau khi revert; **0** lần chặn dừng.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-2` → mở `W22-3` (WP-22, F3 FE).**

- `W22-3`: màn Web Admin theo SRS §10 / ARCH §11 (contract-first `schema.d.ts`; preview `textContent`, cấm `dangerouslySetInnerHTML`; che người nhận; nút duyệt lô không cho người tạo). Chạy `pnpm gen:api` — **không** được đổi `schema.d.ts` ngoài diff mong đợi.
- **Trước khi code FE**: chốt `GetTemplatesForBatch` (BE bổ sung hay BA hạ khỏi phạm vi) — FE màn lô cần danh sách template `Marketing`.
- Không gọi NCC, không PII thật, không `IgnoreQueryFilters`, không `migrations add` khi chưa `pg_dump`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`; base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` **`7f23726`**.
- **Đã push** `origin/feat/gd3-wp22-thong-bao` (`dd8ee17..7f23726`); **chưa** đẩy `tich-hop/gd3` (chỉ ở `W22-5` khi G6 xanh).
- Không mở PR (`D-216` ③); commit đóng phiên (handover + `07 §13` + registry) đi kèm sau.

---

**Hàng rào agent** (phiên `N84`): **11 lượt vai** — `ba` ×3 (SRS v0.6; đóng 2 P2; `XC-005` + SRS §6.1) · `docrev` ×2 (kiểm nhanh `DOC-OK`; sửa artifact) · `decider` ×1 (`D-267`) · `be` ×3 (F3 BE; chạm trần 80 lượt rồi tiếp; sửa 2 lỗi batch) · `qa` ×2 (81 test; chạm trần 60 lượt rồi sửa `BusGia` + báo cáo). **0 lần chặn dừng**; **0** tệp ngoài vùng lọt (1 lần `XC-005` đã revert); cảnh báo "thiếu JSON" của harness đã xử lý.
