# Handover `N38` — WP-14: `P-07` (`SAL-006`) `DOC-CHANGES` vòng 2, còn 1 P1 + 3 P2

> Phiên `N38` (lệnh `/chay-phien`, 21/11/2026). Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/sales/sal-006-tra-hang-rma-hoan-tien.md -WP14` | 0 | 16/16 mục, 23/23 BR có test, mục đối chiếu Nhanh có (dòng 2698), 14 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/SAL-006-docreview.md` | 0 | `DOC-CHANGES`, P1=1, P2=3, P3=9 (vòng 2; vòng 1 = `DOC-CHANGES` 3 P1/10 P2/11 P3) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | 23 dòng đạt (gồm `D-132…D-138`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 36/36 pass (baseline đầu phiên) |

`SAL-006` v1.6 (≈2725 dòng): 3 P1 của vòng 1 đã đóng thật (một phiếu đang mở/đơn — `D-135`; công thức `RefundAmount` — `D-136`/`D-138`; nhận nhiều đợt — `D-137`). Vòng 2 sinh **P1 mới** do chính chỗ sửa nhận nhiều đợt. D3: dòng nháp (đề xuất "chưa trình duyệt") trong `docs/agents/nhap-bien-ban-gd3-a.md`. Chạm trần 2 vòng DOCREV ⇒ hàng `P-07` giữ 🔁.

## 2. Chưa xong / cắt giữa

- **P1 #1 (DOCREV vòng 2):** đợt nhận trộn hàng tốt (`Restock`) + hàng lỗi bắt gọi cổng kho hai lần cùng số `TH-{ReturnNumber}-{rev}`; `CustomerReturnGateway.cs:45-54` nuốt lời gọi thứ hai (idempotent theo số chứng từ) ⇒ tồn sai im lặng; unique `(TenantId, StockDocumentId, Sku)` còn chặn hai dòng cùng SKU trong một chứng từ. `D-137` (4) chọi `D-132` (3). **Cần decider chốt một trong: (a) một lời gọi additive nhận mọi dòng của đợt · (b) chứng từ riêng cho nhánh hàng lỗi** (thay thế phần (4) của `D-137`), rồi BA sửa Basic Flow 9, BR-012, §7.4, F6, TC-34 (vi), TC-41 (v).
- **P2 #2:** §6.2/E1/E6 khai `Return:0001/0006/0011` trong khi code đã ném `Sales:0010` (E2E `S14-E8`/`S14-E19` khoá mã này) — chốt tách mã hay dùng lại.
- **P2 #3:** bỏ hướng dẫn "tạo phiếu trả mới" sau phiếu kết thúc ở §3.3 hàng 8, §5.2, thông điệp `0006`, §8.3 #10 (`D-135` ⇒ luôn NÉM `Order:0006`).
- **P2 #4:** §5.1:380 và `D-136` (1) ghi `Σ RefundAmount == RefundedAmount`; đổi thành `RefundedAmount ≤ Σ RefundAmount`, bằng khi hoàn đủ (khớp TC-16/TC-43).
- 9 P3 (số cột §11 "+2 snapshot" vs Migration 1 thêm 4 cột; số thứ tự Migration nhảy; ví dụ "trả nhiều phiếu" chỉ có nghĩa khi Q17 mở…).
- Sau khi sửa: DOCREV **vòng kiểm nhanh** (chỉ các mục BA đổi), cùng tệp docreview.

## 3. Migration đã áp

Không có (WP thuần tài liệu).

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung |
|---|---|---|
| `D-132` | TẠM CHỐT (DEC) | Q11: hàng hỏng/trả NCC về kho = `CustomerReturn` + `DamagedIn` cùng số lượng, nguyên tử qua cổng `Inventory` |
| `D-133` | TẠM CHỐT (DEC) | Q12: ghi sổ khách đúng một lần ở chuyển `→ Delivered`, cột `CustomerLedgerPostedAtUtc` |
| `D-134` | TẠM CHỐT (DEC) | Q13: đổi `int → decimal(18,3)` một lần cùng WP đơn số lẻ |
| `D-135` | TẠM CHỐT (DEC) | Q14: một phiếu đang mở/đơn; 2 cạnh huỷ `ReturnRequested/ReturnApproved → Open` (SAL-003 additive) |
| `D-136` | TẠM CHỐT (DEC) | Q15: `RefundAmount = TienHang + Thue`; `tienHangTra` không gồm thuế |
| `D-137` | TẠM CHỐT (DEC) | Q16: nhận nhiều đợt, `TH-{ReturnNumber}-{rev}`, delta, `LastReceiptRevision` — **phần (4) cần thay thế (P1 #1)** |
| `D-138` | TẠM CHỐT (DEC) | Q18: phân bổ giảm/thuế cấp đơn xuống dòng bằng `MoneyAllocator`, 2 cột snapshot nullable |

Người chốt: không có (hồ sơ uỷ quyền `D-114` HIỆU LỰC). Bảy dòng đã có trong `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | P1 #1 + 3 P2 + 9 P3 của `SAL-006` (§2) | decider → BA `SAL-006` |
| 2 | `SAL-003` (đã duyệt) cần sửa additive: thêm `E_ReturnRequested --> E_Open`, `E_ReturnApproved --> E_Open` vào sơ đồ trục 4, hàng 33/34 vào bảng trục 4, hàng 30 giữ nguyên (`D-135`) | BA/arch `SAL-003`, cùng nhánh, trước WP code |
| 3 | Q17: ngữ nghĩa hấp thụ của `SAL-003` (phiếu Rejected/nhận đủ ⇒ không trả lần hai; `CodLine*Eto` của `REC-003` NÉM `0006` → DLQ; nhãn "Đang hoàn" vĩnh viễn) | chủ dự án / arch `SAL-003` |
| 4 | Q19: hàng rào luỹ kế `D-138` (4) khi snapshot trước là `NULL` — BA hiểu là NÉM, chưa qua decider (không chặn tài liệu) | BE/QA khi viết `S14b` |
| 5 | `GrandTotal` cộng thuế hai lần (`Order.cs:243`) lệch `D-133`; POS hoàn `qty × unitPrice` (`RetailReturn.cs:258`) | BA `SAL-002`, BA POS |
| 6 | Công thức hoàn thuế/giảm cấp đơn (`D-136`/`D-138`) cần kế toán duyệt trước release | chủ dự án / kế toán |
| 7 | CHECK nội tuyến `OrdRefunds` chưa chạy trên PostgreSQL (máy không có `psql`/`docker`) | BE khi viết migration |
| 8 | Hook cảnh báo "thiếu khối JSON cuối" cho `decider`/`ba` dù báo cáo có khối JSON (qua kênh hand-back) — có thể là báo sai của hook | chủ dự án (`.claude/hooks`) |

**Chờ người:** Q17 (nợ #3) và nợ #6.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`P-07` `SAL-006` còn P1 #1: gọi decider chốt `D-137` (4) vs `D-132` (3), BA sửa, DOCREV kiểm nhanh; rồi `P-08`).

**Khối B — mở phiên mới:**

1. Mở cửa sổ mới, chạy một phiên một lúc.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. Chưa PR.

---

**Hàng rào agent**

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `ba` | 3 (sửa theo docreview vòng 1; áp `D-135…D-137`; áp `D-138`) | — |
| `decider` | 4 (Q14, Q15, Q16, Q18; Q15 hết 40 lượt nhưng đã ghi `D-136`) | `D-135…D-138` |
| `docrev` | 1 (vòng 2; vòng 1 từ phiên bị cắt) | `DOC-CHANGES` |
