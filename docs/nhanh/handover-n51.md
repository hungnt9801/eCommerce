# Handover N51 — WP-14 · hàng P-11 (`PRO-002` DOC-OK, `LOY-001` còn lại)

> Phiên `N51` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N49`; phiên `N50` bị cắt ngang (đã để lại bản nháp `PRO-002` v0.9 trong cây làm việc, chưa commit).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `check-feature-doc.ps1 -Path …/pro-002-cay-dieu-kien-engine-khuyen-mai.md -WP14` | 0 | `KET QUA: DAT` — 16/16 mục · 20/20 BR có test · 14 câu `Open` |
| `check-feature-doc.ps1 -Path …/pro-001-voucher-coupon.md -WP14` | 0 | DAT (vẫn xanh sau 1 vế delta `D-163`) |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRO-002-docreview.md` | 0 | `verdict=DOC-OK · P1=0 · P2=0` |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT (`D-163`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 180 tệp · 801 link · 1075 đường dẫn backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **PRO-002**: BA (2 lượt) đóng **1 P1 + 3 P2** của DOCREV vòng 3 (`#1` candidate tự động chỉ `Kind='Discount'` + `TC-67`; `#2` bỏ khoá job theo kỳ giờ + `TC-50` (viii); `#3` lỗi cấu hình NÉM mọi nguồn + hàng rào + `TC-68`; `#4` định nghĩa context + "đơn đầu") → **DOCREV vòng 4 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 2 P3). Tài liệu `v0.9 — 23/11/2026`, 55 TC, 20 BR.
- **Decider `D-163`** (Q27, định nghĩa "đơn đầu" cho `IsFirstOrder`): chọn **(b)** — đếm **cả** đơn đã huỷ/đã trả — **khác khuyến nghị BA (a)** vì (a) mở vòng lách: áp "đơn đầu" → huỷ đơn trước giao → `OrderCancelledEto` hoàn lượt (`D-148`) → khách lại là "đơn đầu" → giảm lại vô hạn. Đã áp vào `pro-002` §6.1 quy tắc 11 + `TC-69`, và **một vế** delta `pro-001` §6.1.
- **D3**: thêm hàng `PRO-002` vào `docs/agents/nhap-bien-ban-gd3-b.md`; cập nhật tóm tắt đợt B (2/3 tài liệu DOC-OK). Cập nhật `docs/agents/doc-gate-2026-11-23.md` (PRO-002 đã DOC-OK).

## 2. Chưa xong / cắt giữa

- **`LOY-001`** (điểm thưởng) — **chưa bắt đầu** (một tài liệu/phiên theo `/doc-gate` §0; `PRO-002` là tài liệu lớn, chiếm trọn phiên). Đây là phần còn lại của hàng `P-11`.
- **2 P3 của PRO-002** (DOC-OK cho phép < 3 P2 — để người duyệt, **không** gọi BA sửa sau DOC-OK, N22):
  1. §15.4 :1213 nhật ký `v0.9` ghi "DoD 54 test case", thực tế §14.1/§14.3 = **55**.
  2. Delta `pro-001` §6.1 :467/:469 còn trích số dòng cũ của `pro-002` (`:164/:513/:186/:550/:101` → nay `:169/:588/:191/:633/:105`) — P3 vòng 3 #9, ngoài vế đã soát.
- **Câu `Open` không chặn DoD**: `Q26` (lỗi nạp context khách — khuyến nghị (b), chỉ chặn nhánh code), `Q28` (tách lỗi cấu hình theo nguồn — câu tương lai, v1 giữ NÉM `D-154` (5)), `Q29` (gộp/giữ hai tên context — giữ hai). `Q8` chờ GHTK. Tất cả decider được uỷ quyền quyết khi cần.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- **`D-163`** (TẠM CHỐT DEC, uỷ quyền `D-114`, phiên N51) — định nghĩa "đơn đầu" (`IsFirstOrder`): đếm cả đơn đã huỷ/trả; đơn đang xét không tính; `CustomerId = NULL` ⇒ `null`; dùng chung cho `PRO-001`/`PRO-002`. Cách lùi + hạn xem lại (release, M4) ở `decisions.md` và `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

- **2 P3** PRO-002 ở §2 — để người duyệt / sửa cùng WP code.
- **SQL §5.3 chưa chạy** (không `psql`/docker) — như các tài liệu đợt B khác.
- **Chờ người:** không có câu mới. `Q8` (GHTK chưa xác minh địa chỉ 2 cấp — nguồn NCC) vẫn `Open`, không chặn v1 (điều kiện vùng giao ngoài phạm vi `PRO-002`).
- `pro-002` chưa có biên bản duyệt ⇒ tin cậy `D-163` ở mức VỪA (đã ghi trong `D-163`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ `P-11` — `PRO-002` đã `DOC-OK`; phiên sau làm `LOY-001` (một tài liệu/phiên: BA đối chiếu → decider các câu chặn → DOCREV → D3).**

- Đọc `docs/reviews/PRO-002-docreview.md` **không** cần nữa (đã DOC-OK). Tập trung `LOY-001`.
- Lệnh: `/doc-gate LOY-001` (sau khi mở phiên N52).

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; điểm xanh PRO-002 = `4252c5d` (đã push); commit đóng phiên (handover + registry) kế tiếp. Chưa PR.

---

**Hàng rào agent** (phiên N51): `ba` ×2 (`a88fe5a0`, `adb2c7bc`) · `decider` ×1 (`ac169495e7`) · `docrev` ×1 (`a2f3bfe7`). Cảnh báo `canh-bao.jsonl`: 2 lượt thiếu khối JSON cuối (decider, docrev) — **hạn chế đã biết** của Claude Code (không gửi `last_assistant_message`; `scopes.json` `chanDungKhiThieuJson: false`); công thật `check-agent-artifact` đều **exit 0**. **0** lần ghi ngoài vùng.
