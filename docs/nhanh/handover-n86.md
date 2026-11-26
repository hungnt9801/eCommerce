# Handover N86 — WP-22 / hàng `W22-4` (F4 QA + F5 REV) XONG

> Phiên `N86` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W22-4` · WP-22 · mã `NOT-001` · nhánh
> `feat/gd3-wp22-thong-bao` (base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` `93ea079`) · 26–27/11/2026.
> Phạm vi phiên: phủ BR bằng test + negative control · phát hiện & sửa lệch PII (`D-269`) · `rev` toàn chuỗi W22-2/W22-3.
> 0 migration, `main` không đụng.

## 1. Đã xong

- **F4 — `docs/tests/NOT-001-testplan.md`**: phủ **25/25 BR**, ghi trung thực **13 mục "chưa kiểm được"** kèm lý do
  (đáng chú ý `TC-45` phần "server lọc `Marketing`" — endpoint `GetTemplatesForBatch` **không tồn tại**, nợ BE N1).
  12 ca mới: `NotificationMaskerTests` (10) · `NotificationQueueAppenderTests` (+1 mask/fingerprint) · `NotificationQueueAppServiceTests` (+1 `CancelAsync`).
  Test mới của W22-4 (vòng 1): `NotificationBoundaryTests` (TC-33) · `NotificationPersistenceTests` (TC-39) ·
  `NotificationReportingIsolationTests` (TC-28) · `NotificationSecurityTests` (TC-38 reflection) ·
  `NotificationQueueAppServiceTests` (TC-37) · `PostgreSqlNotificationTests` (TC-19 + TC-30 ×3, PostgreSQL thật qua Testcontainers).
- **Phát hiện lệch PII → `D-269` → sửa**: `NotificationMasker.CheBiMat` là **no-op**, appender lưu `RenderedSubject`/`RenderedBodyMasked`/`LastReasonMasked`/`ReasonMasked` **nguyên văn**.
  Đã cài mask thật **ở một cửa ghi** (`modules/Notification/.../Domain/Rendering/NotificationMasker.cs`, chuyển từ Application sang Domain;
  `NotificationSettingKeys` tách sang tệp riêng): email · SĐT di động VN (`+84`/dấu phân cách) · PAN 13–19 số · query `token|sig|signature|code|key|password|otp`;
  **giữ nguyên** tiền/ngày/`MaDon`/`1900…`/`024…`; idempotent; `null`/rỗng ⇒ `string.Empty`. `ContentFingerprint` tính trên giá trị **đã lưu (đã che)**.
- **BA + DOCREV**: SRS `NOT-001` **v0.7 → v0.8** (ghi chú "Luật che (`D-269`)" §5.2/§5.4/§5.5; as-built §0/header/§5/§14.4/§15.2 F1;
  `TC-14` khớp luật (ii)). DOCREV **2 vòng**: vòng 1 `DOC-OK` **P1=0 · P2=1 · P3=1** → BA sửa → vòng 2 **`DOC-OK` · P1=P2=P3=0**
  (`docs/reviews/NOT-001-n86-docreview.md`).
- **F5 — `docs/reviews/NOT-001-review.md`**: **`VERDICT: PASS` · P1 = 0** (P2 = 2, P3 = 3 — xem §5).
- **Negative control** (ORCH tự chạy `tools/agents/negative-control.ps1`, bằng chứng trong testplan):
  **NC-1** (bỏ `NULLS NOT DISTINCT` ⇒ `TC-19` đỏ) · **NC-4** (bỏ `Nem` guard hàng 5 ⇒ `TC-05` đỏ) ·
  **NC-5** (render đọc `DateTime.Now` ⇒ `TC-04` đỏ) · **NC-8** (bỏ mask email ⇒ `NotificationMaskerTests` đỏ). Cả 4: **exit 1 → khôi phục → exit 0**.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2251/2251** |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở phiên) | 0 | **49/49** |
| `dotnet build eCommerce.slnx --no-incremental` (đóng phiên) | 0 | **0 error / 9 warning** (0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (đóng phiên) | 0 | **2283/2283** (+32; không xoá/skip ca nào) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` (đóng phiên) | 0 | **110/110** (+4 `PostgreSqlNotificationTests`) |
| `node --test .claude/hooks/test/hooks.test.mjs` (đóng phiên) | 0 | **49/49** |
| `pwsh -File tools/kiem-tat-ca.ps1` (đóng phiên) | 0 | **10/10 · 345,2 s** |
| `negative-control.ps1` NC-1 / NC-4 / NC-5 / NC-8 | 0 | **ĐỎ (exit 1) → XANH (exit 0)** |
| `check-agent-artifact.ps1 -Kind testplan` | 0 | **DAT** · BR 25/25 · 0 thiếu |
| `check-agent-artifact.ps1 -Kind docreview` | 0 | **DAT** (DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind review` | 0 | **DAT** (PASS · P1=0) |
| `check-feature-doc.ps1 -Path <SRS> -WP14` | 0 | **DAT** · 16/16 heading · 25/25 BR |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 958 link · 1648 đường dẫn · 0 chết |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W22-4`.** Các mục dưới là **cắt CÓ QUYẾT ĐỊNH** (đã ghi lý do trong testplan §4 và handover §5):

- **NC-2 / NC-3 chưa chạy** (đột biến cần chạy thật): `NC-2` (bàn giao bằng `if (status == ChoGui)` không `UPDATE … WHERE`) và
  `NC-3` (nhánh `23505` dùng lại UoW đã abort) đòi **host + PostgreSQL + `ExecuteUpdateAsync` của worker** — hạ tầng test hiện có
  (`RepoGia` in-memory) **không** mô phỏng được đua. `NC-6`/`NC-7` (nhãn/metric, khuyến nghị) cũng chưa chạy.
- **13 mục "chưa kiểm được"** trong testplan §4 (`TC-02`, `TC-09`/`TC-11`, `TC-13`, `TC-18`, `TC-24`, `TC-26`, `TC-27`,
  `TC-30` nhánh đua, `TC-36` rollback DB, `TC-38` HTTP thật, `TC-45` server lọc `Marketing`, NFR p95, Redis chết) — cùng gốc:
  **chưa có harness host ABP + `WebApplicationFactory`**.
- **P2 của REV** (không chặn merge, phải chốt trước khi **bật gửi thật**): masker chạy trên **chính body gửi đi** (`DispatchWorker` đọc
  `RenderedBodyMasked`) ⇒ false-positive có thể méo tin thật (PAN che mã đơn/số TK, `code=` che voucher, hotline/email của shop).
  Hiện **chưa lộ production** vì `EnableDispatch = false` mặc định — chỉ bật ở `NOT-002`/WP-23.
- Nợ **môi trường** kế thừa N85: `pnpm build` `EPERM symlink` trên Windows (cần xác nhận trên CI); `pnpm lint` chưa có script.

## 3. Migration đã áp

**Không có** — phiên này 0 migration (`D-269` cố ý **0** cột/migration). Migration của WP-22 vẫn là
`20261126132231_Added_Notification_Template_Queue` (W22-2, `pg_dump` 457 042 B).

## 4. Quyết định mới

- **`D-269`** — TẠM CHỐT (DEC, uỷ quyền M2): cài **mask thật** ở **một cửa ghi** thay vì nới BR-010/`TC-14`; luật mask hẹp
  (không mask dãy 6–8 số để không phá `MaDon`/tiền/ngày); **0** migration/DTO/route; lùi = revert 1 commit.
  Ghi ở `docs/nhanh/decisions.md` + hàng đợi xem lại (tin cậy CAO, chi phí lùi THẤP).
- **Không có câu chờ người mới.** Việc `be` mà `D-269` đề nghị tách thành hàng kế đã làm **ngay trong `W22-4`** (điều kiện
  "không đóng W22-4 khi `TC-14` còn đỏ" đã thoả: `TC-14` xanh bằng 12 ca thật).

## 5. Nợ / rủi ro

- 🟠 **Mask trên body gửi đi** (P2 REV) — chủ **`ba`/`be`, WP-23 (`NOT-002`)**: trước khi bật `EnableDispatch`, phải chốt
  "gửi bản đã che" hay "gửi bản gốc + chỉ che khi hiển thị"; `D-269` ⑤ đã ghi mốc xem lại này.
- 🟠 **Negative control NC-2/NC-3 + 13 TC integration** — chủ **`qa`, khi có harness host**: mở một WP hạ tầng test
  (host ABP + `WebApplicationFactory` + PostgreSQL) rồi chạy lại; **không** đánh dấu đã phủ trước đó.
- 🟡 **Server lọc `Marketing`** (`GetTemplatesForBatch`) — nợ **BE** N1 (SRS §15.5); FE đang lọc client (`D-268`), `TC-45` phần server = chưa kiểm được.
- 🟡 **Nợ môi trường FE**: `pnpm build` EPERM (Windows), `pnpm lint` thiếu — chủ **ops/người**; cần xác nhận trên CI.
- 🟡 **Nợ ARCH** §15.5 N6 (`NOT-001-arch.md` §4.2 còn "12 op") — mở 1 dòng khi `arch` chạm ARCH lần sau.
- **Merge `main` vẫn ĐỎ** tới khi có biên bản người ký (P-21, `D-71`/§A.5). `tich-hop/gd3` chỉ nhận ở `W22-5` (G6).
- **Hàng đợi xem lại**: **169** dòng "Đang chờ" (gồm `D-269`).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền):** chọn NCC kênh 2 + khoá HMAC production (R3-6); merge `main`; duyệt `D-268`/`D-269`.
- **Hàng rào agent**: **10** cảnh báo "thiếu khối JSON cuối" trong phiên (harness không cấp `last_assistant_message` — mọi vai
  vẫn trả JSON trong báo cáo); **0** lần chặn dừng; **0** tệp ngoài vùng lọt.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-4` → mở `W22-5` (WP-22, F6 OPS + G6 → đẩy fast-forward vào `tich-hop/gd3`).**

- `W22-5`: `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (không chạy song song build/test khác) · tài liệu triển khai của WP-22
  (tạo mới ở `docs/ops/`, **phải có đường lùi**) · `07` §13 một dòng · push `feat/gd3-wp22-thong-bao` rồi
  `git push origin <nhanh>:tich-hop/gd3` (chỉ fast-forward).
- Không gọi NCC, không PII thật, không `IgnoreQueryFilters`, không `migrations add` khi chưa `pg_dump`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`; base `origin/tich-hop/gd3` @ `58326ba`; `HEAD` **`93ea079`** (+ commit đóng phiên).
- Commit phiên: `93ea079` (F4 QA + mask `D-269` + SRS v0.8 + docreview + review).
- **Đã push** `origin/feat/gd3-wp22-thong-bao`; **chưa** đẩy `tich-hop/gd3` (chỉ ở `W22-5` khi G6 xanh).
- Không mở PR (`D-216` ③).

---

**Hàng rào agent** (phiên `N86`): **11 lượt vai** — `qa` ×2 (F4 vòng 1 + test mask; lượt 1 chạm trần 60 lượt ⇒ tiếp) ·
`decider` ×1 (`D-269`) · `be` ×1 (mask thật) · `ba` ×2 (ghi chú mask; sửa P2/P3 docrev) · `docrev` ×2 (`DOC-OK` 2 vòng) ·
`rev` ×1 (`PASS`, chạm trần 30 lượt ⇒ viết artifact ở lượt tiếp). **0** lần chặn dừng; **0** tệp ngoài vùng.
