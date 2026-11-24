# Handover phiên N61 — WP-14, `P-17` (`NOT-002`) XONG

> Phiên 24/11/2026 · hàng `P-17` (`doc-gate`, đợt C) · nhánh `docs/n22-wp14-dot-a` · WP-14 (D-71).

## 1. Đã xong — việc + số đo

**Baseline (WP thuần tài liệu — `D-71`: 0 `.cs`; build/test bỏ qua có lý do "WP thuần tài liệu", build đang đỏ `MSB3491` trên máy dev, chủ `WP-15`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 199 tệp `.md`, 889 link, 1281 đường dẫn backtick, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 48/48 pass |

**Dây chuyền `NOT-002` (8 bước, tuần tự):**

| Bước | Việc | Kết quả / số đo |
|---|---|---|
| D1 | BA đối chiếu as-built + nợ chéo | `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục · **18/18** BR có test) |
| D1q | 8 câu §15 có `blocks` → decider (tuần tự) | `D-200`…`D-207` (2 `ĐỀ XUẤT` chờ người, 6 `TẠM CHỐT`) |
| D1 | BA áp 8 quyết định vào tài liệu | `check-feature-doc.ps1 -WP14` exit 0 (16/16 · **24/24** BR · 14 NFR) |
| D2 v1 | DOCREV vòng 1 | `DOC-CHANGES` — 0 P1 · **3 P2** (F2 `TaxLockKeys` lỗi thời, F4 `CorrelationId` lỗi thời, `ChoTai` không tồn tại) |
| D1 | BA sửa 3 P2 + 6 P3 | `check-feature-doc.ps1 -WP14` exit 0 |
| D2 v2 | DOCREV vòng 2 (toàn tài liệu) | `DOC-CHANGES` — 0 P1 · **5 P2** (lỗi có sẵn: `NULLS NOT DISTINCT` §5.1, "5 bảng" §4.2, NFR-007/012 thiếu TC, toạ độ as-built) |
| D1 | BA sửa 5 P2 + 3 P3 | `check-feature-doc.ps1 -WP14` exit 0 |
| D2 v3 | DOCREV vòng 3 kiểm nhanh (mục đã đổi) | **`DOC-OK`** — 0 P1 · **1 P2** · 1 P3 |
| D1s | `check-agent-artifact.ps1 -Kind docreview` | exit 0 (`DOC-OK`, P1=0/P2=1) |
| D3 | Dòng `NOT-002` trong `docs/agents/nhap-bien-ban-gd3-c.md` | **6/6** tài liệu đợt C đề xuất trình duyệt |
| D4 | Điểm xanh | `kiem-link` PASS (890 link · 1312 backtick) · `check:mermaid` 212/0 · `git status` chỉ `docs/**` + `agents/state/**` |

- `check-agent-artifact.ps1 -Kind decision` → exit 0 (**92** dòng kiểm, tới `D-207`).
- Cập nhật `docs/nhanh/07-ke-hoach-trien-khai.md` §13 (một dòng N61 có số đo).
- Commit `d779cf0` (7 tệp, +297/−122): tài liệu feature, docreview, `decisions.md`, `hang-doi-xem-lai.md`, nháp biên bản, bảng phiên, state. **Chưa push** — xem §5.

## 2. Chưa xong / cắt giữa

Không. Hàng `P-17` đạt DoD của `doc-gate`:

- `check-feature-doc.ps1 -WP14` exit 0 · DOCREV `DOC-OK` 0 P1 · D3 có dòng · D4 xanh.
- **Còn mở có chủ đích:** `Q1` (chọn NCC email/SMS — `D-200` `CHUYEN_NGUOI`) và `Q3` (5 thông số Zalo OA — `D-202` `CHUYEN_NGUOI`) giữ `Open`; thiết kế trung lập đã áp (adapter + Settings + fake sender + fail-closed `0003`).
- **Để người duyệt:** 1 P2 (ADR-03 còn dẫn `PaymentTransaction.cs:76` thay vì `:84`) + 1 P3 (§1 Pre-Condition còn nhắc "chế độ suy giảm" dù §15.3 giả định 1 đã ghi XC-002 triển khai). Không sửa sau `DOC-OK` (luật `/doc-gate` §1).

## 3. Migration đã áp

Không có (WP thuần tài liệu; 0 tệp `.cs`, 0 migration).

## 4. Quyết định mới

**8 dòng** (`docs/nhanh/decisions.md`), mọi dòng có ④ Lùi + ⑤ Xem lại:

| Mã | Trạng thái | Chủ đề |
|---|---|---|
| `D-200` | `ĐỀ XUẤT (DEC)` — `CHUYEN_NGUOI` | `NOT-002` Q1 — chưa chọn NCC email/SMS (R3-6, vùng cấm); chốt phần trung lập: hợp đồng adapter + registry, cấu hình, fake sender, fail-closed `0003`, 0 NuGet NCC |
| `D-201` | `TẠM CHỐT (DEC)` | Q2 — lưu `BodyMasked` (mask trong module, OTP ⇒ `NULL`, 0 `BodyHash`, cấm `using eCommerce.PaymentHub`) |
| `D-202` | `ĐỀ XUẤT (DEC)` — `CHUYEN_NGUOI` | Q3 — 5 thông số Zalo OA chưa xác minh (R3-6); Settings + `WebhookSecretEncrypted`, không mở endpoint callback GĐ3 |
| `D-203` | `TẠM CHỐT (DEC)` | Q4 — một `DedupKey varchar(160)` + `DedupKeyVersion`, UNIQUE `NULLS NOT DISTINCT`, dùng `CanonicalJsonHasher` |
| `D-204` | `TẠM CHỐT (DEC)` | Q5 — opt-out băm một chiều HMAC-SHA256 hex64 + `HashVersion`, một cửa, API check theo người nhận |
| `D-205` | `TẠM CHỐT (DEC)` | Q6 — `SentAckTtl` 1440/720/720 phút, cấu hình theo dòng kênh, biên `[60, 43200]` |
| `D-206` | `TẠM CHỐT (DEC)` | Q7 — cảnh báo kỹ thuật: người nhận cấu hình, fan-out `Critical`, fail-closed; **vá lỗ hổng §5 thiếu cột người nhận** |
| `D-207` | `TẠM CHỐT (DEC)` | Q8 — opt-out sync: mặc định off + adapter kéo; đường đẩy `SendOutcome.RecipientRefused`; `Source` append |

## 5. Nợ / rủi ro

- **Chờ người (R3-6):** chọn NCC email (SMTP riêng vs dịch vụ API) + NCC SMS brandname + giá/segment + thủ tục đăng ký brandname + hợp đồng + **khoá HMAC production**; 5 thông số Zalo OA. Đây là điều kiện để viết adapter thật; GĐ3 chỉ chạy được adapter email tổng quát + fake sender.
- **`NOT-002` vẫn cần người duyệt ở `P-19`** (BA + Kiến trúc trưởng + người); `DOC-OK` chỉ là điều kiện trình duyệt.
- **1 P2 + 1 P3** để người duyệt (§2). P2 là toạ độ as-built, P3 là câu chữ.
- **Tệp ngoài phạm vi chưa xử lý:** `docs/agents/huong-dan-chay-tu-dong.md`, `tools/agents/chay-tu-dong.ps1`, `tools/agents/test-chay-tu-dong.ps1` bị sửa trong lúc phiên chạy (hook `bao-orch.mjs` gắn cảnh báo "GHI NGOAI VUNG" cho lượt `docrev` vòng 3, mtime 18:23:51–52). Nội dung là tính năng theo dõi tiến trình của bộ chạy tự động, **không** do dây chuyền tài liệu sinh ra. **Không** `git restore` (không rõ chủ, sợ mất việc người khác) và **không** commit. Người cần quyết: giữ, commit riêng, hay bỏ.
- **Push bị chặn:** `git push origin docs/n22-wp14-dot-a` bị auto-mode classifier từ chối ("out-of-place publication"). Commit `d779cf0` **chỉ ở máy**. Người push tay khi duyệt.
- **Blemish cũ:** commit `71ce88f` (N60) còn tiền tố `@` trong subject; không `amend`.
- Hàng đợi xem lại: thêm 8 dòng `D-200`…`D-207` vào `docs/agents/hang-doi-xem-lai.md`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-17` → mở `P-18`** (`tong-hop`: hoàn thiện `docs/agents/nhap-bien-ban-gd3-c.md` — đủ 6 dòng, người review theo loại, bảng câu chờ người của đợt C).

- `/chay-phien` (sẽ chọn `P-18` qua `PH next`).
- `P-18` xong thì `P-19` (**người**: duyệt đợt C, bắt buộc kế toán cho `ACC-001`/`REP-002`), `P-20` (`dong-wp`), `P-21` (**người**: merge PR).
- Khi người chốt NCC (R3-6) ⇒ ghi `D-xx` mới thay `D-200`/`D-202`, rồi mới mở WP code `Notification`.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; `HEAD` = `d779cf0`, `main` = `be301c5`. Commit phiên `N61`: `d779cf0` (**7 tệp**, +297/−122) — **chưa push** (classifier từ chối; xem §5). Chưa PR.

---

**Hàng rào agent** (phiên N61): **12** lượt spawn vai — `ba` ×4 (đối chiếu, áp quyết định, sửa vòng 1, sửa vòng 2) + `decider` ×8 (`D-200`…`D-207`, tuần tự từng câu) + `docrev` ×3 (vòng 1, vòng 2, vòng 3 kiểm nhanh). **0** lần ghi ngoài vùng do vai tài liệu; **1** cảnh báo "GHI NGOAI VUNG" ở lượt `docrev` vòng 3 cho 3 tệp `tools/` + `docs/agents/huong-dan-chay-tu-dong.md` (không rõ chủ, xem §5). **5** cảnh báo `SubagentStop` "thiếu khối JSON cuối" — **dương tính giả** đã biết (`D-104`; báo cáo vẫn có khối JSON, hook chỉ ghi nhật ký). Đồ thị: BA + DOCREV dùng `codegraph_explore` trước `Grep`; mọi phát hiện P1/P2 về code đều có dòng `graph:`.
