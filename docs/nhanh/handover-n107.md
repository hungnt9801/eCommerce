# Handover N107 — WP-23 / hàng `W23-2` (F3 BE `NOT-002`) XONG — hợp đồng kênh trung lập NCC + fail-closed `0003` + 30 test

> Phiên `N107` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W23-2` · WP-23 · mã `NOT-002` · module `Notification` ·
> nhánh `feat/gd3-wp23-kenh-gui` (base `origin/tich-hop/gd3` @ `c29940f`; commit phiên `99434d7`, `6cd8128`) · 28/11/2026.
> Phạm vi: **F1–F1b** (đồng bộ SRS theo `docDeviation` #1–#9) + **F3 BE** (hợp đồng adapter trung lập NCC, registry, sender,
> fail-closed `0003`) + **test đơn vị** (QA viết — hook chặn `be` ghi `test/**`, tiền lệ `N101`). **0 migration · 0 bảng · 0 `DbSet` · 0 endpoint · 0 quyền · 0 gói NuGet NCC.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Baseline mở phiên | ORCH | build **0 error · 9 warning** (có sẵn) · unit **2619/2619** · `kiem-link` **PASS** (1017 link · 1890 backtick) · mermaid **214/0** · `hooks.test.mjs` **49/49** · Docker `ecommerce-postgres` + `ecommerce-redis` **up** |
| Cổng vào `Code` WP-23 | ORCH | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-23` **XANH (exit 0)** |
| F1 — BA đồng bộ SRS | `ba` ×2 | `not-002-kenh-gui-email-sms-zalo-oa.md` **v0.4 → v0.6**: áp **9 `docDeviation`** (§6.1 canonical `INotificationSender`/`SendCommand` §4.1, bỏ `TemplateCode`/`Data`/`HandleCallbackAsync`/`RefreshCredentialAsync`; `SendOutcome.RecipientRefused` giữ chỗ; `CorrelationId?` additive; `0003` NÉM cho **mọi** lỗi credential; `0006` **trả** không ném; WP-23 0 bảng/0 endpoint tách khỏi increment đầy đủ; as-built **17 module / 28 tệp**; callback `INotificationCallbackParser`) |
| F1b — DOCREV | `docrev` ×2 | vòng 1 **`DOC-CHANGES`** 0 P1/5 P2/3 P3 → BA vòng 2 → vòng 2 **`DOC-OK` 0 P1/0 P2/3 P3**; BA **v0.7** đóng **3 P3** (metric `:364–368`, 39 metric, `INotificationSender.cs:8-31`) |
| F3 — BE | `be` ×2 | `Channels.cs` (`INotificationChannelSender`, `SendCommand` 13 trường credential **chỉ ciphertext**, `SendOutcome` 5 trường, `INotificationChannelFactory`, `NotificationChannelConfig`, `INotificationChannelConfigProvider`); `CorrelationId?` cuối canonical; `NotificationFailureKind` 5 giá trị; mã `0002/0003/0006` + `ProviderCodeToiDa=32`; `NotificationChannelFactory` (trùng khoá NÉM, `Find` null không fallback) · `ChuaCauHinhKenhProvider` (null ⇒ fail-closed) · `NotificationSender` (7 hàng §4.5, metric + log 1 dòng Warning **0 PII**); xoá `ChuaCoKenhGuiSender` + **2** đường DI; worker truyền `CorrelationId`; Host map `0003` → **503**. Vòng 2: `[ExposeServices(typeof(INotificationChannelConfigProvider))]` — QA bắt **lỗi DI thật** (ABP không tự phơi interface do tên lớp) |
| Test đơn vị | `qa` ×1 (2 lượt) | 3 tệp mới `NotificationChannelSenderTests` (521 dòng) · `NotificationChannelRegistryDiTests` (143) · `NotificationChannelArchTests` (187); phủ `TC-EN-01…03`, `TC-FC-01…07`, `TC-MAP-01…04`, `TC-KEY-01`, `TC-LOG-01`, `TC-MET-01`, `TC-REG-01/02`, `TC-DI-01/02`, `TC-ARCH-01…04`; `TC44` **11 → 14** mã (dải `0010–0020` vẫn đủ 11) |
| SRS as-built | `ba` | thêm **§16 Trạng thái triển khai (WP-23)** + §16.3 cập nhật số đo cuối phiên; header **v0.7 — 28/11/2026** |

**DoD của hàng — ORCH tự chạy (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| G3 build | `dotnet build eCommerce.slnx --no-incremental` | **0 error · 9 warning** (không warning mới; cả 9 CS8604 có sẵn ở Address/Sales) |
| G3 test | `dotnet test test/eCommerce.Tests --no-build` | **2649/2649** (baseline 2619, **+30**) |
| G3 toàn bộ | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 — 10/10 bước · 346,9 s** (gồm integration **139/139**, FE typecheck + Vitest, `gen:api` **không đổi** `schema.d.ts`, DB 25/25, test cổng) |
| Negative control | `tools/agents/negative-control.ps1` NC-1 (bỏ guard `config is null`) | **exit 0 — ĐỎ (exit 1) → khôi phục → XANH (exit 0)**; log `artifacts/negative-control/20261128-041249-{do,xanh}.log`. NC đầy đủ (6 ca) thuộc `W23-4` |
| Tài liệu | `check-feature-doc.ps1 -WP14` · `check-agent-artifact -Kind docreview` · `kiem-link-tai-lieu.ps1` · `check:mermaid` | **DAT** (16/16 · 24/24 BR) · **DAT** (`DOC-OK` P1=0/P2=0) · **PASS** (1017 link · 1889 backtick · 0 chết) · **214/0** |
| Hàng rào | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49** |
| Kích thước diff | `git diff --stat` + tệp mới | **+113/−48** (8 tệp) + **1194** dòng mới (sản phẩm **343** · test **851** · ghi chú: vượt mốc mềm ~600 do 30 ca test — không có migration để trừ) |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W23-2`.** Các mục dưới là **cắt có quyết định**, đúng lộ trình:

1. **F4 QA đầy đủ + testplan** (tệp `NOT-002-testplan.md` trong `docs/tests/`, tạo ở `W23-4`) và **6 negative control** đỏ→xanh — hàng `W23-4` (QA đã có 3 tệp test + `NC-1` chạy trước; `W23-4` mở rộng + testplan + NC còn lại).
2. **F3 FE** — hàng `W23-3`: SRS không có màn hình cho `WP-23` (`NOT-002-arch §12`), dự kiến `done` kèm lý do; ORCH phải kiểm lại SRS trước khi đánh dấu.
3. **F5 REV** + **F6 OPS/G6 + push FF `tich-hop/gd3`** — hàng `W23-4`/`W23-5`.
4. **ARCH §1.3 ghi "8 endpoint"** vs SRS §6.2 **11** (8 lõi + 3 opt-outs) — DOCREV coi là ngoài vùng SRS; chủ: increment/`arch` khi chạm lại.

## 3. Migration đã áp

**Không có.** `D-314` chốt WP-23 **0 migration/0 bảng/0 `DbSet`** ⇒ **0 `pg_dump`**, không chạm schema.

## 4. Quyết định mới

**Không có `D-xx` mới trong phiên N107** (không gọi `decider`; mọi câu hỏi của vai đều thuộc hợp đồng đã chốt `D-313/314/315`). `check-agent-artifact -Kind decision` vẫn **DAT**.

## 5. Nợ / rủi ro

- 🟠 **Ngoài tạm duyệt (`R3-6`)** — chọn NCC email/SMS (`Q1`, `D-200`), 5 thông số Zalo OA (`Q3`, `D-202`), khoá HMAC production (`D-204`) — **người**. Tới khi chốt: chỉ hợp đồng trung lập + `fake` sender + fail-closed `0003`; **0** gói NuGet NCC.
- 🟡 **N1** — `INotificationChannelConfigProvider.FindAsync` phải mở rộng (`SenderIdentity`/`ChannelAccountId`) khi có bảng tài khoản kênh (increment đầy đủ sau `R3-6`).
- 🟡 **Mock config luôn `null`** ⇒ production chưa gửi được kênh nào (đúng thiết kế `D-314`/R5); bật `Notification:EnableDispatch=true` sẽ lặp `0003` mỗi lease (R1).
- 🟡 **ARCH §1.3 "8 endpoint"** vs SRS §6.2 "11" — lệch câu chữ P3, chủ increment/`arch`.
- 🟡 **Diff > ~600 dòng** (do 851 dòng test) — ghi nhận, không phải migration; `W23-4` sẽ thêm test tiếp.
- 🔴 **Merge `main`** vẫn cần **biên bản người ký** (`D-71`/§A.5; cổng `MergeMain` ĐỎ). PR `tich-hop/gd3 → main` mở cuối chuỗi.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG W23-2 → mở W23-3 (WP-23 F3 FE, dự kiến done kèm lý do).**

- Hàng kế = **`W23-3`** (loại `code`, WP-23, bước **F3 FE**, vai `fe`); cùng nhánh `feat/gd3-wp23-kenh-gui`.
- Kiểm lại SRS `NOT-002` §10 và `NOT-002-arch §12`: nếu đúng "không có màn hình", đánh dấu `W23-3 done` với bằng chứng "WP không có phần FE: <lý do từ SRS>"; nếu SRS có màn hình thì giao `fe`.
- Sau đó `W23-4` (F4 QA testplan + 6 NC, F5 REV) → `W23-5` (F6 OPS + G6 `-DayDu` + push FF `tich-hop/gd3`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp23-kenh-gui`; base `origin/tich-hop/gd3` @ `c29940f`.
- Commit phiên: `99434d7` (docs SRS + DOC-OK) · `6cd8128` (F3 BE + 30 test + SRS v0.7) · commit đóng phiên (handover + `07` §13 + SRS §16.3 + registry) — **SHA = `git log -1`**.
- **Đã push nhánh WP** (lần cuối `6cd8128`; commit đóng phiên push kèm §5 `chay-phien-code`). **KHÔNG** đẩy `tich-hop/gd3` (chỉ FF ở `W23-5` khi G6 xanh). **Không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

**Hàng rào agent** (phiên `N107`): `ba` ×2 (4 lượt: vòng 1, vòng 2, §16, §16.3) · `docrev` ×2 · `be` ×1 (2 lượt) · `qa` ×1 (2 lượt). `canh-bao.jsonl` ghi **9** entry: **8** "thiếu khối JSON cuối" (lỗi đọc transcript đã biết — cả 8 vai đều gửi khối JSON trong hand-back, ORCH đã đọc) + **1** `ngoaiVung` (BE tự thêm §16 vào SRS — ORCH **`git restore`** rồi giao lại `ba`; **0 lần lọt vùng cuối cùng**). **0 lần chặn dừng**.
