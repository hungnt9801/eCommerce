# Handover N45 — WP-14 · hàng P-10 (`PRO-001`, XONG)

> Phiên `N45` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Tiếp nối `N43` và phiên `N44` bị cắt ngang (decider `D-150` + BA sửa dở nằm trong cây làm việc, chưa commit; không có handover N44).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `check-feature-doc.ps1 -Path docs/features/promotions/pro-001-voucher-coupon.md -WP14` | 0 | `KET QUA: DAT` — 16/16 mục · 36/36 BR có test · mục đối chiếu Nhanh (dòng 1155) · 8 dòng `Open` · 0 TODO |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRO-001-docreview.md` | 0 | `DOC-OK` vòng 3 (kiểm nhanh v0.6), P1=0, P2=1 |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT, 35 dòng |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |

- BA (`PRO-001` v0.5 → v0.6): đóng 9 P2 vòng 2 (#1–#6, #8–#10), áp `D-150`; thêm `TC-66` (override), `TC-67` (trả hàng); 64 test case.
- DOCREV vòng 3: 9 P2 cũ đóng; **1 P2 mới #20** + 6 P3 — để nguyên cho người duyệt (luật `/doc-gate`: không gọi BA sửa sau `DOC-OK`).
- D3: tạo `docs/agents/nhap-bien-ban-gd3-b.md` (dòng `PRO-001`). D4 xanh; cây làm việc chỉ có `docs/**` + `agents/state/**`.

## 2. Chưa xong / cắt giữa

Hàng `P-10` đạt DoD. Việc còn của đợt B: `P-11` (`PRO-002` + `LOY-001`), `P-12`, `P-13` (người duyệt). Nợ tài liệu nhỏ: BA sửa P2 #20 (`MaxUsagePerCustomer = NULL`) trước khi code.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- `D-150` (phiên `N44`) — **TẠM CHỐT (DEC)**, uỷ quyền `D-114`: đính chính `D-147` ⑥ (`TC-59` còn lại `0007` hoặc `0119`), đính chính `D-149` (1) (lá rule không đăng ký ⇒ `0004`), thay `D-147` (4) phần `0106` (v1 không có đường sửa). Hàng đợi xem lại ở `hang-doi-xem-lai.md`.
- Phiên này không tạo `D-xx` mới.

## 5. Nợ / rủi ro

- CR chờ chủ: CR-11 → `PRO-002`; CR-12 → `SAL-004`; CR-P1 → `PRC-002` (chưa gửi). `PRO-002` Q6/Q7 phải `Closed` theo `D-146`.
- P3 `PRO-001`: #12 skeleton `TC35` thiếu `CustomerGroup`; #21 `0109` ở `CreateAsync` (method `PRO-002`); #22 tên `IPromotionAppService`; #23 dòng 1171 ghi "3 method" (thực tế 8); #24 gọi nhầm cột CR-1; #25 nhánh `TC-67` (c) không đạt được.
- SQL chưa chạy thật (không `psql`/docker) — `TC-53`/`TC-59` sẽ đo.
- Hook báo "thiếu khối JSON cuối" cho `ba` — báo cáo thật có khối JSON; ghi nhận.
- **Chờ người:** không có câu mới.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-10` → mở `P-11` (`PRO-002` + `LOY-001`).**

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1` (nền `c5c83c8`); xem câu trả lời cuối về push. Chưa PR.

---

**Hàng rào agent**

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ba | 1 | cảnh báo "thiếu JSON" 1 lần (báo cáo có JSON) |
| docrev | 1 (vòng 3, kiểm nhanh) | không ghi ngoài vùng |
| decider | 0 | |
