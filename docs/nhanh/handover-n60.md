# Handover N60 — WP-14 · hàng P-16 (doc-gate đợt C): `NOT-001` `DOC-OK`, P-16 XONG

> Phiên `N60` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N59`.
> Hàng `P-16` (`doc-gate`) **XONG**: `NOT-001` đạt `DOC-OK` (0 P1) sau 3 vòng.
> **3 quyết định mới** `D-197`…`D-199` (toàn bộ `TẠM CHỐT (DEC)`, uỷ quyền `D-114`).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (**198** tệp · **887** link · **1279** đường dẫn backtick) — *baseline N60: 197/886/1261* |
| `npm --prefix tools run check:mermaid` | 0 | **212** khối · 0 lỗi (*baseline: 212/0*) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **48/48** pass |
| `check-feature-doc -WP14` `not-001` | 0 | **DAT** — 16/16 heading · **25/25** BR có test · mục đối chiếu Nhanh dòng **1233** · 12 câu `Open` |
| `check-agent-artifact -Kind docreview` `NOT-001` | 0 | DAT — **`DOC-OK`** vòng 3 kiểm nhanh (P1=0 · **P2=2** · P3=3) |
| `check-agent-artifact -Kind decision` | 0 | DAT — **84** dòng kiểm (tới `D-199`) |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK: bảng khớp registry (đầu phiên; cuối phiên đo lại sau `done`) |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N60):** link PASS 197 tệp/886 link/1261 backtick · mermaid 212/0 · hooks 48/48 · `PH check` OK. Build/test **bỏ qua có lý do** "WP thuần tài liệu" (`D-71`).
- **`NOT-001`** (136,7 KB → 161,6 KB): BA đối chiếu as-built (module `Notification` **chưa có** trong code — `graph` 0 ký hiệu; `ls modules/` = 15) + thêm mục **"Đối chiếu mô hình Nhanh (24/11/2026)"** + **7 test case** phủ 6 BR thiếu (22 → 25 BR có test, §14.3 41 TC); đóng nợ chéo `D-193` (chi phí gửi = chi phí kỳ; sổ `NotificationCostLedger` đúng 8 cột, cấm `OrderId`/`OrderLineId`) và đối chiếu `D-191` (PII mặc định tắt; người nhận chỉ `RecipientEncrypted`+`RecipientHash`); sửa hiện trạng lệch (metric **16** không đếm cứng, **93** index, `ChannelOrderPulledEto` đã có `CorrelationId`, outbox/inbox ABP đã có, `eCommerce.Kernel` đã có `Money`).
- **3 câu §15 chặn → decider:** `D-197` Q10 — **một** module `modules/Notification/` (4 project như `ChannelHub`), một `ConfigureNotification`/`NotificationErrorCodes` (NOT-002 `0001–0009` + NOT-001 `0010–0019`)/enum `ChannelKind`/`NotificationLockKeys`, giữ tên bảng `Notification*`, không gộp `Messaging`; `D-198` Q1 — **giữ** `NotificationBatches` + `ChoDuyet`, duyệt **chỉ** mục thuộc lô marketing, `CreateBatchAsync` là nguồn duy nhất của `Moi→ChoDuyet`, ETO nghiệp vụ **không** mang `BatchId`, fail-closed, giữ `93 → 100` index; `D-199` Q2 — GĐ3 **chỉ** `BodyFormat = Text = 0`, mã mới **`0020`** (`BodyFormatNotSupported`, dải `0010–0020`), giữ cột + CHECK `(0,1)` + `Html=1` ở cuối enum (mở sau 0 migration), Subject loại CR/LF/NUL, **0 sanitizer/0 NuGet**, preview `textContent`.
- **D2 — DOCREV 3 vòng:** vòng 1 `DOC-CHANGES` (**0 P1 · 6 P2** · 6 P3 — `CorrelationId` sai code, phát lại vừa hấp thụ vừa tạo mục mới, BR-005 "mọi chuyển sai phải NÉM" mâu thuẫn no-op, F5 `TaxLockKeys` thực tế đã sửa, F11 sai số tệp test, Q10 chưa chốt) → BA sửa hết → **vòng 2 `DOC-CHANGES`** (0 P1 · **5 P2 mới** do chính bản sửa: `0018` vs `0017`, thiếu event `Moi→ChoDuyet`, cơ chế khử trùng cửa sổ vs partial UNIQUE, tên enum `ChannelKind` vs `NotificationChannelKind`, thiếu cạnh `DangBanGiao→ChoGui`) → BA sửa hết → **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · **2 P2** · 3 P3; 2 P2 mới là #24 cạnh `DangBanGiao→HetHan` và #26 rollback/UoW mới khi bắt `23505` — để người duyệt theo luật "< 3 P2 sau `DOC-OK`").
- **D3:** cập nhật [`docs/agents/nhap-bien-ban-gd3-c.md`](../agents/nhap-bien-ban-gd3-c.md) — dòng `NOT-001` = **đề xuất trình duyệt** (Kiến trúc trưởng + BA vì có state machine, `design-doc-guidelines.md` §6); mục §2 thêm gạch đầu dòng N60; §3 cập nhật việc còn lại.
- **Điểm xanh:** commit `71ce88f`, đã push.

## 2. Chưa xong / cắt giữa

Không. Hàng `P-16` đạt DoD (`check-feature-doc` exit 0 · DOCREV `DOC-OK` 0 P1 · D3 có dòng · D4 xanh).

- **`NOT-001` 2 P2 (#24, #26) + 15 P3 + 10 câu §15 `Open`** để người duyệt — không sửa sau `DOC-OK`.
- **Nợ chéo tài liệu** (chủ khác, thuộc `P-17`/`NOT-002`): đổi `NotificationChannelKind` → **`ChannelKind`** (`D-197`); dải mã lỗi NOT-001 thành `0010–0020` (`D-199`); ghi chú F8/F9 (tên ETO + dải mã) và **đóng `not-002…:954` Q9 theo `D-193`** khi chạy `P-17`.
- Không sửa `inv-001`/`prc-001` (`D-186`) trong phiên này — vẫn thuộc chủ nợ như handover N59.

## 3. Migration đã áp

Không có (WP thuần tài liệu; 0 tệp `.cs`, 0 migration).

## 4. Quyết định mới

**3 dòng `TẠM CHỐT (DEC)`** (uỷ quyền `D-114`/`D-115`, hồ sơ HIỆU LỰC), mọi dòng có ④ Lùi + ⑤ Xem lại trước = release:

| Mã | Chủ đề |
|---|---|
| `D-197` | `NOT-001` Q10 — **một** module `Notification` dùng chung NOT-001+NOT-002; một `ConfigureNotification`/`NotificationErrorCodes`/`ChannelKind`/`NotificationLockKeys`/permission; không gộp `Messaging`; tách sau = dời project, 0 migration schema |
| `D-198` | `NOT-001` Q1 — **giữ** duyệt lô (`NotificationBatches` + `ChoDuyet`), chỉ mục thuộc lô marketing phải duyệt; tin giao dịch/`OpsAlert` đi thẳng `Moi → ChoGui`; ETO không `BatchId`; fail-closed; giữ 5 bảng/7 index/`93 → 100` |
| `D-199` | `NOT-001` Q2 — GĐ3 **chỉ Text** (`BodyFormat = 0`), `Html` NÉM mã **`0020`**; dải NOT-001 `0010–0020`; 0 sanitizer/0 NuGet; Subject loại CR/LF/NUL; mở HTML sau phải có `D-xx` + Security |

## 5. Nợ / rủi ro

- **`NOT-001` không thuộc nhóm đỏ cứng kế toán** (`D-114` §2.1 xếp nhóm tạm duyệt được), nhưng `DOC-OK` chỉ là điều kiện **trình duyệt**: Kiến trúc trưởng + BA phải ký ở biên bản người (`P-19`) trước khi dùng làm căn cứ code.
- **Chờ người:** `P-19` duyệt đợt C · `P-21` merge PR (vùng cấm) · 2 P2 (#24/#26) + 15 P3 + 10 câu `Open` của `NOT-001` (Q3/Q5/Q6/Q8 cần Vận hành, Q9/Q11 cần Security xác nhận; Q11 còn treo cơ chế vượt query-filter `IMultiTenant` cho template `TenantId = NULL`).
- **Nợ chéo bắt buộc** (chủ `P-17`/`NOT-002`): đổi tên enum `ChannelKind`, dải mã `0010–0020`, ghi chú F8/F9, đóng `not-002` Q9 theo `D-193`.
- **Blemish commit:** `71ce88f` có tiền tố `@` trong subject (lỗi here-string của Git Bash). Không `amend` vì `/chay-phien` cấm sửa lịch sử; người dùng có thể `git commit --amend` trước khi mở PR nếu muốn.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-16` → mở `P-17`** (`doc-gate`: `NOT-002` kênh email/SMS/Zalo OA — kiểm kích thước, nếu > 120 KB thì một mã/phiên).

- `/chay-phien` (sẽ chọn `P-17` qua `PH next`); chạy dây chuyền `/doc-gate NOT-002`.
- Khi chạy `P-17`, đồng bộ nợ của `NOT-001`: tên enum **`ChannelKind`** (`D-197`), dải mã **`0010–0020`** (`D-199`), F8/F9, và đóng `not-002…:954` Q9 theo `D-193`.
- `P-18`/`P-19` (tổng hợp + duyệt đợt C) sau khi `P-17` xong; `P-20`/`P-21` là việc của người.

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; `HEAD` = `71ce88f`, `main` = `be301c5`. Commit phiên `N60`: `71ce88f` (**7 tệp**, +366/−109) — **đã push** lên `origin/docs/n22-wp14-dot-a`. Chưa PR.

---

**Hàng rào agent** (phiên N60): **9** lượt spawn vai — `ba` ×3 (đối chiếu, sửa vòng 1, sửa vòng 2) + `docrev` ×3 (3 vòng) + `decider` ×3 (`D-197`/`D-198`/`D-199`). **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`). **4** cảnh báo `SubagentStop` "thiếu khối JSON cuối" — **dương tính giả** đã biết (`D-104`; báo cáo vẫn có khối JSON, hook chỉ ghi nhật ký). Đồ thị: BA + DOCREV dùng `codegraph_explore` trước `Grep`; mọi phát hiện P1/P2 về code đều có dòng `graph:`.
