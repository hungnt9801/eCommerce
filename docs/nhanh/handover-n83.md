# Handover N83 — WP-22 / hàng `W22-1` (F0–F2b) XONG: ARCH `NOT-001` + DOCREV `DOC-OK` + G2 tạm duyệt

> Phiên `N83` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W22-1` · WP-22 · mã `NOT-001` · nhánh
> `feat/gd3-wp22-thong-bao` (base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` mở phiên `aa5c069`) · 26/11/2026.
> Phạm vi phiên: **F0–F2b** — đọc SRS theo mục, thiết kế kiến trúc `NOT-001`, DOCREV 2 vòng, G1/G2 theo M2.
> **W22-1: 0 `.cs` sản phẩm, 0 migration, 0 package, 0 đổi FE/CI/compose.**

## 1. Đã xong

- **F0 — chuẩn bị**: cổng tài liệu `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` **exit 0** (XANH, đo cả lúc mở và lúc đóng);
  đọc SRS `NOT-001` v0.5 theo mục (§0/§0.1, §1, §3, §4.1–4.2, §5, §6, §7, §8, §9, §10, §11, §12, §13, §14, §15 + mục Nhanh)
  và `NOT-002` phần ranh giới; quyết định đối chiếu `D-197/198/199/191/193/208/253`.
  Danh sách người cần duyệt theo **K-08**: `NOT-001` có **state machine** ⇒ **Kiến trúc trưởng**; module `Notification` mới
  (`D-197`) ⇒ kiến trúc; **PII** (`D-191`) ⇒ người phụ trách bảo mật; `XC-002/003/004/005` ⇒ nhóm cross-cutting; **Vận hành**
  (job/hàng đợi). Không có tích hợp NCC, không có tiền/kế toán trong `NOT-001`.
- **F2 — ARCH `NOT-001`**: `docs/features/notifications/NOT-001-arch.md` (**686 dòng**) — module mới `modules/Notification/`
  (4 project như `ChannelHub`), ranh giới một chiều + ETO (không gọi AppService chéo), tầng Domain (template/version bất biến,
  batch, queue item 15 hàng state machine ném lỗi, queue event append-only, renderer thuần, `NotificationLockKeys` `notification:…`,
  dải mã `0010–0020` một tệp), `Application.Contracts` (DTO/interface/ETO có `CorrelationId`, 0 PII), EF Core hợp nhất
  (`DbSet` + `ConfigureNotification` một lần, unique index `NULLS NOT DISTINCT`, partial index soft-dedupe, CHECK theo `EventType`),
  chống gửi trùng 5 lớp (hàng rào thật = `UPDATE … WHERE Status = ChoGui` + unique + UoW mới `requiresNew` sau `23505`),
  job bền vững trên `AbpBackgroundJobs` (tránh bài học starvation `AddAsync` mỗi nhịp), phân quyền, PII, metric (cập nhật
  `eCommerceMetricNames.All` + `XC-005` §5.1 + test cùng PR), FE contract tối thiểu, migration expand-only + đường lùi,
  test/QA plan + **7 negative control**, xử lý **15 P3**, `docDeviation` 11 mục, 7 câu hỏi mở.
- **F2b — DOCREV 2 vòng (ngữ cảnh sạch)**:
  - Vòng 1 **DOC-CHANGES**: 0 P1 · **2 P2** · 6 P3 — P2 #1: pha (C) §7.2 chỉ `UPDATE … WHERE Status=ChoGui` còn mục `Moi`
    không được gọi `DanhDauSanSang` ⇒ kẹt vĩnh viễn; P2 #2: `INotificationSender` định nghĩa **hai nơi khác nhau** (ARCH §4.4
    vs `NOT-002` §6.1 — trái ADR-06, thiếu `docDeviation`).
  - ARCH sửa (tách pha **C1** `Moi→ChoGui` cùng UoW trước **C2** giành bàn giao; chốt §4.4 **canonical** + `docDeviation #11`
    buộc `NOT-002` §6.1 đổi; thêm enum `NotificationTemplateStatus`; sửa số hàm `ConfigureXxx`; sửa trích dẫn `#27`; `Body`
    thay `BodyMasked`; chuyển "0 tham chiếu `Messaging`" ra khỏi bảng lệch — **tuân thủ** `D-197` ①(2)).
  - Vòng 2: **`DOC-OK` · 0 P1 · 0 P2 · 1 P3** (`docs/reviews/NOT-001-arch-docreview.md`, 75 dòng). P3 còn lại = **TC-01 đếm 3
    event** vs `Dispatching` ở hàng 5 (kế thừa SRS) — **đã chốt bằng `D-265`** (happy path **4 event**).
- **DEC (M2)**: **`D-254`…`D-266`** — 13 quyết định: 7 câu `AQ1…AQ7`; ratify `docDeviation #3/#21`, `#4/#22`, `#7` (`D-261`),
  `#5` route (`D-262`), `#8/#9` job quét DB + sender fail-closed (`D-263`), `#11` interface canonical (`D-264`); P3 #1-v2
  (`D-265`); **G2 tạm duyệt thiết kế** (`D-266`) cho `W22-2` code trên nhánh, kèm danh sách **KHÔNG** tạm duyệt.
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo)**:

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** (baseline cũ) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2170/2170** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS · **954** link · **1607** backtick · 0 chết |
| `node --test .claude/hooks/test/hooks.test.mjs` (mở + đóng phiên) | 0 / 0 | **49/49** |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` (mở + đóng phiên) | 0 / 0 | **XANH** |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/NOT-001-arch-docreview.md` | 0 | **DAT** (verdict=DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** (116 dòng kiểm); `-Id D-266` exit 0 |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | PASS · **955** link · **1619** backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (đóng phiên) | 0 | **213 khối · 0 lỗi** |
| `dotnet build` / `dotnet test` (đóng phiên) | — | **bỏ qua có lý do**: hàng F0–F2b, **0 `.cs`** thay đổi (`git status` chỉ `docs/**` + `agents/state/**`) |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W22-1`.** DoD đạt: `NOT-001-arch.md` + DOCREV `DOC-OK` 0 P1/0 P2 + G2 `D-266` + cổng tài liệu XANH.
Mọi mục còn lại là **cắt có quyết định**:

- **11 `docDeviation` SRS↔ARCH** — chủ **BA**, sửa SRS `NOT-001` **cùng PR `W22-2`** (`D-261`/`D-262`/`D-263`); riêng `#11`
  buộc **`NOT-002` §6.1** đổi (`D-264`) — BA sửa cùng PR WP-23 (hoặc sớm hơn), **không** sửa SRS trong phiên này.
- **15 P3** đã được ARCH xử lý (8 đóng bằng thiết kế, 7 thiết kế + BA sửa SRS cùng PR W22-2) — chi tiết §14 của ARCH.
- **P3 #1-v2** (TC-01 3 hay 4 event) — đã chốt **4 event** (`D-265`); BA sửa TC-01/§3 hàng 5/§4.1 cùng PR W22-2.
- **`NOT-002` §6.1** phải theo `INotificationSender` canonical của ARCH (`D-264`).
- **ARCH §16** truy vết chỉ có `D-197…D-253` (viết trước DEC); `D-254…D-266` nằm ở `decisions.md`. **Không sửa ARCH sau
  `DOC-OK`** (`D-208` mất hiệu lực nếu tài liệu đổi mà không qua DOCREV) — phiên sau đọc `decisions.md`.

## 3. Migration đã áp

- **Không có.** Hàng F0–F2b, 0 `.cs`, 0 migration ⇒ không chạm DB, không cần `pg_dump` (K-01 không kích hoạt).
- Migration **thiết kế** (chưa sinh, `W22-2` sẽ làm): 5 bảng `NotificationTemplates` · `NotificationTemplateVersions` ·
  `NotificationBatches` · `NotificationQueueItems` · `NotificationQueueEvents` — **expand-only**, một chủ migration;
  **bắt buộc `pg_dump`** trước khi áp nếu DB dev có dữ liệu (`D-253`/K-01).

## 4. Quyết định mới

- **`D-254`** — AQ1: WP-22 **không** định nghĩa ETO đầu vào (biên #1) + handler; mở khi có module phát thật; kèm 1 ca quét
  "module chưa có `IDistributedEventHandler`".
- **`D-255`** — AQ2 + `#6`: **hoãn** `GET …/reports/summary` + permission `Reports.View` khỏi WP-22/SRS §6.2; mở khi `REP-001/002` có read-model.
- **`D-256`** — AQ3/Q11: template hệ thống `TenantId = NULL` **chỉ đọc, 0 PII**; **cấm** `IgnoreQueryFilters`; đa tenant = nợ GĐ4.
- **`D-257`** — AQ4: cửa sổ khử trùng **300 s** chỉ cho `Marketing`; TTL **24 h** (`Transactional`/`OpsAlert`) · **72 h** (`Marketing`);
  OTP **300 s** là giá trị tương lai (0 OTP vào hàng đợi); `NULL` = không hạn.
- **`D-258`** — AQ5: engine template **tự viết**; **0 gói NuGet**, 0 đổi `Directory.Packages.props`.
- **`D-259`** — AQ6: tối đa **50.000** người nhận/lô (`Notification:Batch:MaxRecipients`); vượt ⇒ 400 + bên gọi chia lô.
- **`D-260`** — AQ7: `PreviewAsync` **chỉ dữ liệu mẫu**; cấm đọc đơn/khách thật.
- **`D-261`** — ratify `#3/#21`, `#4/#22`, `#7`: `BodyFingerprint` + `ContentFingerprint`; `FromStatus`/`ToStatus` `int?` + CHECK;
  hàng 14 thêm vế `ExpiresAtUtc IS NULL`.
- **`D-262`** — ratify `#5`: không đặt `[Route]` tay; route do ABP/BFF sinh; xác minh `pnpm gen:api` (BFF chỉ `/api/v1` ⇒ nợ FE).
- **`D-263`** — ratify `#8/#9`: job **quét DB** là đường bàn giao duy nhất; ETO chỉ cho `REP-*`; `ChuaCoKenhGuiSender` fail-closed + `EnableDispatch=false`.
- **`D-264`** — ratify `#11`: `INotificationSender` ARCH §4.4 **canonical** (`RecipientEncrypted` + `Subject` + `Body` đã render;
  bỏ `TemplateCode`/`Data`; tách `HandleCallbackAsync`).
- **`D-265`** — P3 #1-v2: `Dispatching` **có** phát ở đường nóng (C2) cùng UoW; happy path **4 event** + `DangBanGiao`.
- **`D-266`** — **G2 tạm duyệt thiết kế** `NOT-001-arch.md` (`DOC-OK` 0 P1/0 P2/1 P3) cho `W22-2…W22-4` trên nhánh; **không**
  tạm duyệt: merge `main`, migration chưa `pg_dump`, PII thật, chọn NCC/khoá HMAC, dữ liệu production, danh sách đỏ §2.1.
- **Câu chờ người:** 0 câu mới cần quyết trong phiên (`decider` trả `TAM_CHOT`); các câu **vùng cấm** đã gom ở §5.

## 5. Nợ / rủi ro

- **`docDeviation` 11 mục** — chủ **BA**, đóng cùng PR `W22-2` (trừ `#11` → WP-23). Không sửa SRS trong phiên này.
- **`D-266` điều kiện**: 15 P3 + P3 #1-v2 sửa cùng WP-22 code row; 7 negative control do `qa` chạy (bằng chứng đỏ→xanh).
- **`NOT-002` §6.1** chưa đổi theo `D-264` ⇒ nếu WP-23 code trước khi BA sửa sẽ lệch interface — theo dõi ở hàng `W23-*`.
- **`D-257`** (giá trị seed) nếu áp DB rồi mới đổi ⇒ phải `UPDATE` dữ liệu thật — chờ người duyệt lô.
- **Merge `main` ĐỎ** tới khi có biên bản người ký (P-21, `D-71`/§A.5).
- **Hàng đợi xem lại:** **167** dòng "Đang chờ" (mở phiên 154; **+13** `D-254…D-266`).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền):** chọn NCC kênh 2 + khoá HMAC production (R3-6); `pg_dump` trước `migrations add`;
  merge `main`; duyệt lô 13 dòng mới; danh sách đỏ §2.1; không mở endpoint `[AllowAnonymous]`.
- **Cảnh báo hook** (`artifacts/agents/canh-bao.jsonl`, 2 dòng): `arch` ×1 và `docrev` ×1 bị báo "thiếu khối JSON cuối" ở lượt
  deliver đầu tiên — nguyên nhân harness không cấp `last_assistant_message` đúng lúc; **cả hai đã trả lại khối JSON** trong
  hand-back (arch JSON có `artifact`/`boundaryViolations`; docrev JSON có `verdict`/`p1`/`p2`). **0** artifact bị ảnh hưởng, **0** lần chặn dừng.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-1` → mở `W22-2` (WP-22, F3 BE).**

- `W22-2`: code theo `docs/features/notifications/NOT-001-arch.md` (**đã tạm duyệt** `D-266`) — domain + app service + endpoint +
  migration expand-only (**`pg_dump` trước khi áp** nếu DB có dữ liệu, K-01) + test đơn vị.
- BA sửa SRS `NOT-001` cho **11 `docDeviation`** + P3 #1-v2 **cùng PR**; `qa` phủ `TC-05`/`TC-30` + 7 negative control ở `W22-4`.
- **Không** gọi NCC, **không** lưu PII thật, **không** `IgnoreQueryFilters`, **không** `migrations add` khi chưa `pg_dump`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`, base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` mở phiên `aa5c069`.
- Commit phiên: **commit đóng phiên** (`NOT-001-arch.md` + `NOT-001-arch-docreview.md` + `decisions.md` `D-254…D-266` +
  `hang-doi-xem-lai.md` + handover này + registry `agents/state/phien-gd3-1.json` + `docs/agents/bang-phien-gd3-1.md`).
- **Đã push** `origin/feat/gd3-wp22-thong-bao`; **chưa** đẩy `tich-hop/gd3` (chỉ ở hàng `dong-wp` `W22-5` khi G6 xanh).
- Không mở PR (`D-216` ③).

---

**Hàng rào agent** (phiên `N83`): **5 lượt vai** — `arch` ×2 (viết ARCH 686 dòng; sửa 2 P2 + 6 P3) · `docrev` ×2 (vòng 1
`DOC-CHANGES`; vòng 2 kiểm nhanh `DOC-OK`) · `decider` ×1 (`D-254…D-266`, 13 dòng). **0 lần chặn dừng**; **0** tệp ngoài vùng
(`git status` trước commit chỉ có `docs/features/notifications/**`, `docs/reviews/**`, `docs/nhanh/decisions.md`,
`docs/agents/**`, `agents/state/**`); 2 cảnh báo "thiếu JSON" của harness (đã trả lại JSON trong hand-back, không ảnh hưởng).
