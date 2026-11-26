# Handover N85 — WP-22 / hàng `W22-3` (F3 FE) XONG: hợp đồng `/api/v1` 9 op + 5 màn Web Admin

> Phiên `N85` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W22-3` · WP-22 · mã `NOT-001` · nhánh
> `feat/gd3-wp22-thong-bao` (base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` `bcd82e0`) · 26/11/2026.
> Phạm vi phiên: **F3 FE** — mở bề mặt `/api/v1` cho màn hình (`D-268`), rồi dựng 5 màn Web Admin contract-first.
> Không migration, không đổi `modules/**`.

## 1. Đã xong

- **F0 — điều kiện vào**: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` **exit 0** (XANH) ở mở phiên.
- **`DEC` — `D-268`** (xem §4): màn "Lô gửi" dùng `GetTemplatesAsync` + **lọc client `Purpose = Marketing`**;
  `GetTemplatesForBatch` = nợ BE, **không** chặn WP; hàng contract `/api/v1` chạy **trong** W22-3 trước hàng `fe`.
- **Hợp đồng `/api/v1`** (`be`, commit `bc8f898`) — 3 controller tường minh, **9 op**, `[Authorize]` **cấp class**,
  method ghi khai permission riêng, dùng lại DTO module + `ApiPage<T>`:
  `GET/POST /api/v1/notifications/templates` · `POST …/templates/{id}/versions` · `POST …/templates/{id}/archive`
  (`Templates.Publish`, khớp app service — **không** phải Manage) · `POST …/templates/preview` · `GET …/queue` ·
  `GET …/queue/{id}/events` · `POST …/batches` · `POST …/batches/{id}/approve`. `0` sửa `modules/**`, `0` migration.
  Bằng chứng: host chạy thật, swagger **8 path / 9 operation** `/notifications`, gọi ẩn danh **401** (không 500).
- **Hợp đồng sinh tự động**: `pnpm spec:snapshot` → `openapi.v1.json` **51 → 59 path** (8 path mới, **0** path/xoá),
  `pnpm gen:api` → `schema.d.ts` **+710 dòng thuần bổ sung** (0 dòng cũ bị xoá), chạy lại **không drift**
  (sha256 ổn định). 3 936 dòng "xoá" trong `openapi.v1.json` là **đổi thứ tự khoá** của các schema cũ — đã kiểm
  bằng so sánh ngữ nghĩa: **0** path/schema cũ đổi nội dung.
- **5 màn Web Admin** (`fe`, commit `bcd82e0`) dưới `app/[business]/notifications/` + `components/notification/`:
  - **Danh sách template** (trả cả `Archived`, lọc client theo kênh/mục đích/trạng thái, rỗng ⇒ hướng dẫn).
  - **Soạn template mới** + **editor version** (whitelist placeholder chèn bằng nút, chặn `{{…}}` ngoài whitelist —
    chống `0015`; hộp thoại publish nêu **bất biến**; editor **trống** vì API chưa có GET version — nợ §5).
  - **Xem trước** — `<pre>` + `pre-wrap`, `textContent`, **0** `dangerouslySetInnerHTML` (`D-199`), nhãn
    "Đây là bản xem trước — chưa gửi"; template chưa publish ⇒ hướng dẫn publish (`0012` = lỗi cấu hình).
  - **Hàng đợi** — lọc server-side (`Status`/`ChannelKind`/`TuNgayUtc`/`DenNgayUtc`), người nhận **chỉ hiện
    `recipientMasked`** (`D-191`); **chi tiết mục = panel mở rộng** gọi `GET /queue/{id}/events` (timeline chỉ đọc,
    hiện `ErrorCode` + `CorrelationId`, `renderedBodyMasked` trong `<pre>`).
  - **Lô gửi** — chọn template **lọc client Marketing** (`D-268`) + cảnh báo khi `TotalCount > số đã tải`; chặn khi
    template chưa `currentPublishedVersionId`; `TotalItems` 1..50 000 + `IdempotencyKey` sinh client; sau tạo hiện
    mã lô + **liên kết chia sẻ** `?batchId=`; **ẩn nút duyệt với người vừa tạo** (`duocDuyetLo`, user id từ
    `GET /api/v1/system/me`); thẻ duyệt cần `Batches.Approve`, hộp thoại **"sẽ gửi ra người thật"**, lỗi `0011`
    dịch đúng nghĩa.
  - **Menu** 3 mục nhóm "Thông báo" (`Templates.View` / `Queue.View` / `Batches.Create`), `enabled: true`.
- **BA + DOCREV (F1/F1b — đồng bộ SRS cùng PR)**: `ba` cập nhật SRS `NOT-001` **v0.6 → v0.7** (as-built: §1/§3
  `CreateBatchAsync` **chỉ tạo header**, appender sinh `Moi → ChoDuyet` khi enqueue có `BatchId`; §6.1 interface
  `ArchiveAsync` + `PreviewResultDto`; §6.2 bảng 9 op `/api/v1`; §9 bảng quyền thật; §10 5 màn as-built; NFR-009 +
  TC-28 thu hẹp; §15.5 nợ N1–N6). `docrev` **2 vòng**: vòng 1 `DOC-CHANGES` (**0 P1 · 3 P2 · 2 P3**) → BA sửa →
  vòng 2 **`DOC-OK` · 0 P1 / 0 P2 / 0 P3** (artifact `docs/reviews/NOT-001-n85-docreview.md`).
- **Negative control** **NC-N85-1**: bỏ guard `duocDuyetLo` (`createdByUserId !== currentUserId`) ⇒ Vitest **ĐỎ**
  → khôi phục **XANH**.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2251/2251** |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở phiên) | 0 | **49/49** |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` (mở phiên) | 0 | **XANH** |
| `dotnet build eCommerce.slnx --no-incremental` (đóng phiên) | 0 | **0 error / 9 warning** (không warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (đóng phiên) | 0 | **2251/2251** |
| `pnpm --dir apps/admin-web typecheck` | 0 | **0 lỗi** |
| `pnpm --dir apps/admin-web test` (Vitest) | 0 | **45/45** (25 cũ + **20** mới `lib/notification.test.ts`) |
| `pnpm --dir apps/admin-web lint` | 1 | **không có script** — nợ có sẵn (`N70` §5), không thêm ESLint |
| `pnpm --dir apps/admin-web build` | 1 | **EPERM symlink** ở bước copy `output: standalone` (môi trường Windows); compile + type-check + render **XANH** |
| `pnpm spec:snapshot` | 0 | **59/59** path `/api/v1` (trước **51**; +8 notification) |
| `pnpm gen:api` (2 lần) | 0 | `schema.d.ts` **+710** dòng thuần bổ sung, **không drift** |
| `pwsh -File tools/kiem-tat-ca.ps1` (đóng phiên) | 0 | **10/10 · 387,4 s** |
| `negative-control.ps1` NC-N85-1 | 0 | **ĐỎ → XANH** |
| `check-feature-doc.ps1 -WP14 -Path <SRS>` | 0 | **DAT** · 16/16 heading · 25/25 BR |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/NOT-001-n85-docreview.md` | 0 | **DAT** (DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md -Id D-268` | 0 | **DAT** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau handover) | 0 | **PASS** · 958 link · 1640 đường dẫn · 0 chết |
| `node --test .claude/hooks/test/hooks.test.mjs` (đóng phiên) | 0 | **49/49** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W22-3`.** Hai mục là **chặn môi trường/nợ có sẵn**, không phải code dở:

- **`pnpm build` đỏ trên máy dev này**: `EPERM: operation not permitted, symlink …` khi Next copy
  `output: "standalone"` (cần quyền tạo directory symlink trên Windows — máy không bật Developer Mode/chạy admin).
  Tái hiện cả sau `rm -rf apps/admin-web/.next`; **không** do thay đổi phiên này (compile + type-check + static
  pages đều xanh trước bước copy). CI/container Linux thoát lỗi này — cần người xác nhận cổng build trên CI.
- **`pnpm lint` không tồn tại** ở `apps/admin-web` (nợ có sẵn từ `N70` §5) — `CLAUDE.md` §3 đang ghi lệnh không chạy được.
- **Nợ BE có chủ** (SRS `v0.7` §15.5 N1–N5, không chặn WP): `GetTemplatesForBatch` (lọc server-side Marketing),
  `GET /templates/{id}` + đọc version hiện tại (prefill editor + preview bản nháp), `GET /batches/{id}` (UI kiểm
  người tạo trước khi duyệt), endpoint đọc whitelist placeholder, `IdempotencyKey` cho create/publish template.
- **Nợ ARCH** (§15.5 N6): `NOT-001-arch.md` §4.2 còn "12 op conventional" — as-built `/api/v1` 9 op thắng; mở 1 dòng
  khi `arch` chạm ARCH lần sau (không sửa theo `D-266`/`D-268`).

## 3. Migration đã áp

**Không có** — phiên này 0 migration (W22-2 đã áp `20261126132231_Added_Notification_Template_Queue`, `pg_dump` 457 042 B).

## 4. Quyết định mới

- **`D-268`** — TẠM CHỐT (DEC, uỷ quyền M2): màn lô lọc **client-side** `Purpose = Marketing` từ `GetTemplatesAsync`
  (`MaxResultCount = 200`, cảnh báo khi thiếu trang, fail-closed UI); hàng contract `/api/v1` đi trước hàng `fe`
  trong W22-3; `GetTemplatesForBatch` mở sau, không chặn. Lùi: xoá 1 biểu thức lọc + 1 ca Vitest.
- Không có câu **chờ người** mới; các câu vùng cấm vẫn ở §5.

## 5. Nợ / rủi ro

- **Build FE trên Windows (môi trường)** — chủ **ops/người**; xem §2. Chưa có bằng chứng CI xanh cho commit `bcd82e0`.
- **`pnpm lint` thiếu** — chủ **fe/ops** (nợ từ `N70` §5).
- **5 nợ BE + 1 nợ ARCH** — xem §2; chủ **BA/arch/BE**, xếp hàng khi mở WP màn hình kế hoặc `WEB-001`.
- **Phạm vi UI so với SRS**: SRS §10 có 8 vùng; báo cáo tổng hợp đã hoãn theo `D-255`/ARCH §11; **0** thay đổi.
- **PII thật / OTP / `IgnoreQueryFilters`**: **0** trong phiên (UI chỉ hiện bản đã che).
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (P-21, `D-71`/§A.5); `tich-hop/gd3` chỉ nhận ở `W22-5` (G6).
- **Hàng đợi xem lại**: **169** dòng "Đang chờ" (mở phiên 168; **+1** `D-268`).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền):** chọn NCC kênh 2 + khoá HMAC production (R3-6); merge `main`;
  duyệt `D-268` (đã có trong hàng đợi xem lại).
- **Cảnh báo hook** (`artifacts/agents/canh-bao.jsonl`): **12** dòng "thiếu khối JSON cuối" (harness không cấp
  `last_assistant_message` đúng lúc) — **tất cả** vai đã trả JSON trong báo cáo; **0** tệp ngoài vùng lọt;
  **0** lần chặn dừng.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-3` → mở `W22-4` (WP-22, F4 QA + F5 REV).**

- `W22-4`: phủ BR bằng test + **7 negative control đầy đủ** theo ARCH §13.2 (mới có NC-N84-1 và NC-N85-1), testplan
  `docs/tests/<MÃ>-testplan.md` ghi rõ **TC-45 phần "server lọc Marketing" = chưa kiểm được** (nợ BE), rồi `rev`
  trên `git diff` của cả chuỗi W22-2/W22-3.
- Đóng cổng G3 còn thiếu: xác nhận `pnpm build` trên CI/container (máy dev Windows lỗi symlink); `pnpm lint` vẫn nợ.
- Không gọi NCC, không PII thật, không `IgnoreQueryFilters`, không `migrations add` khi chưa `pg_dump`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`; base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` **`bcd82e0`**.
- Commit phiên: `bc8f898` (hợp đồng `/api/v1`) · `bcd82e0` (FE 5 màn + sinh hợp đồng).
- **Đã push** `origin/feat/gd3-wp22-thong-bao`; **chưa** đẩy `tich-hop/gd3` (chỉ ở `W22-5` khi G6 xanh).
- Không mở PR (`D-216` ③); commit đóng phiên (handover + `07 §13` + registry + SRS `v0.7` + docreview) đi kèm sau.

---

**Hàng rào agent** (phiên `N85`): **9 lượt vai** — `decider` ×1 (`D-268`) · `be` ×1 (hợp đồng `/api/v1` 9 op) ·
`fe` ×2 (lượt 1 chạm trần 60 lượt ⇒ tiếp; lượt 2 hàng đợi + lô gửi) · `ba` ×2 (SRS `v0.7`; sửa 5 phát hiện docrev) ·
`docrev` ×2 (vòng 1 `DOC-CHANGES` 3 P2/2 P3; vòng 2 `DOC-OK`). **0** lần chặn dừng; **0** tệp ngoài vùng lọt;
12 cảnh báo "thiếu JSON" của harness (dương tính giả — mọi vai đã trả JSON).
