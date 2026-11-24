# Handover N49 — WP-14 · hàng P-11 (`PRO-002` đang dở, `LOY-001` chưa bắt đầu)

> Phiên `N49` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Tiếp nối `N47` và phiên `N48` bị cắt ngang.

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `check-feature-doc.ps1 -Path docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md -WP14` | 0 | `KET QUA: DAT` — 16/16 mục · 20/20 BR có test · 11 câu `Open` |
| `check-feature-doc.ps1 -Path docs/features/promotions/pro-001-voucher-coupon.md -WP14` | 0 | DAT (delta v0.8) |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRO-002-docreview.md` | 0 | `DOC-CHANGES` vòng 3, P1=1, P2=3 |
| `check-agent-artifact.ps1 -Kind decision` (`D-161`, `D-162`) | 0 | DAT |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi (đầu phiên) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | pass (đầu phiên) |

- BA: `PRO-002` v0.5 → v0.8 (áp `D-160`, `D-161`, `D-162`; đóng 6 P2 + 6 P3 của vòng 2); delta `pro-001` v0.8 (§5.2, §6.1).
- Decider: `D-161` (Q24, vãng lai + trần theo khách ⇒ bỏ qua, `CustomerRequired = 11`), `D-162` (Q25, lá khách + `CustomerId` NULL ⇒ `false`).
- DOCREV vòng 3: vòng 2 đóng hết; **1 P1 + 3 P2 + 5 P3 mới** (xem `docs/agents/doc-gate-2026-11-23.md`).

## 2. Chưa xong / cắt giữa

- `PRO-002`: BA sửa 1 P1 + 3 P2 của vòng 3 rồi DOCREV vòng kiểm nhanh:
  1. P1 §4.2 bước 2 / A2 (1) / §6.1 quy tắc 8 — candidate tự động chỉ gồm `Kind = 'Discount'` + TC.
  2. P2 §7.2 #5 / `TC-50` — khoá job theo kỳ giờ làm kích hoạt trễ ~1 giờ.
  3. P2 lỗi cấu hình `0001/0002/0004/0010` với candidate tự động (đảo một phần `D-154` (5)? ⇒ gửi decider).
  4. P2 §6.1 — định nghĩa `PromotionRuleContext`, `CustomerGroupId`, "đơn đầu".
- `LOY-001`: chưa bắt đầu (một tài liệu/phiên theo `/doc-gate` §0).
- Q26 (lỗi nạp context khách, BA khuyến nghị (b)) — để `Open`, chỉ chặn nhánh code.
- D3 (`nhap-bien-ban-gd3-b.md` dòng `PRO-002`) chưa làm, chờ `DOC-OK`.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- `D-160` (N47), `D-161`, `D-162` (N49) — **TẠM CHỐT (DEC)**, uỷ quyền `D-114`; xem `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

- Vòng DOCREV mỗi lượt lại lộ P1/P2 mới ở luồng ghép mã + tự động; BA đã 3 lần sửa. Nên giữ phạm vi sửa hẹp, không thêm tính năng.
- SQL §5.3 chưa chạy thật (không `psql`/docker).
- **Chờ người:** không có câu mới (mọi câu đã vào decider).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ `P-11` — làm tiếp `PRO-002` (BA sửa 4 mục ở §2 → DOCREV kiểm nhanh), rồi `LOY-001` ở phiên kế.**

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về push. Chưa PR.
