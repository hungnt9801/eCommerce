# Handover `N39` — WP-14: `P-07` (`SAL-006`) `DOC-OK` vòng 3 (v1.7)

> Phiên `N39` (lệnh `/chay-phien`, 21/11/2026). Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/sales/sal-006-tra-hang-rma-hoan-tien.md -WP14` | 0 | 16/16 mục, 23/23 BR có test, mục đối chiếu Nhanh có (dòng 2694), 14 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/SAL-006-docreview.md` | 0 | `DOC-OK`, P1=0, P2=2, P3=5 (vòng 3) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | 26 dòng đạt (gồm `D-139`, `D-140`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 36/36 pass |

Việc làm: decider chốt `D-139` (P1 #1: chứng từ kho riêng `TH-{ReturnNumber}-{rev}-L` cho nhánh hàng lỗi, thay phần (4) của `D-137`) và `D-140` (dùng lại `Sales:0010`). BA đưa `SAL-006` lên v1.7 (P1 #1, P2 #2–#4, 9 P3, thêm TC-45; 45 test case). DOCREV vòng 3 kiểm nhanh: `DOC-OK`. D3: dòng nháp trong `docs/agents/nhap-bien-ban-gd3-a.md` đã cập nhật (đề xuất "trình duyệt có điều kiện").

## 2. Chưa xong / cắt giữa

- **P2 #1 (vòng 3):** đơn vị đếm bút toán — BR-012 câu đầu (:117), Post-Condition (:113), §8.1 #8 (:663), TC-20 (:907) còn "mỗi dòng phiếu trả"; sau gộp SKU (`D-139` (3)) phải là "mỗi dòng chứng từ".
- **P2 #2:** `D-140` (1) áp một nửa: §6.2 :555/:556/:566 và §12 :834/:835/:837/:840/:848 còn ghi `400`/`409` cho `Sales:0010/0011`; repo chưa map mã Sales (chưa đo bằng API thật).
- 5 P3 (#3 phạm vi câu "Σ bút toán một SKU"; #4 truy vết `TH-{ReturnNumber}` cũ; #5 gạch nhãn `0004`/`0014`; #6 hàng `D-134` dẫn "Migration 2" cũ; #7 dòng `D-136` (1) trong `decisions.md` còn `Σ RefundAmount == RefundedAmount` — không sửa dòng `D-xx` cũ, cần dòng `D-xx` đính chính hoặc ghi chú).
- Không chặn duyệt; BA sửa cùng PR trước WP code. Hàng `P-07` đạt DoD (DOC-OK).

## 3. Migration đã áp

Không có (WP thuần tài liệu).

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung |
|---|---|---|
| `D-139` | TẠM CHỐT (DEC) | Q: P1 #1 — đợt nhận tối đa 2 chứng từ (`TH-…-{rev}` Restock, `TH-…-{rev}-L` hàng lỗi), gộp delta theo SKU, cùng UoW; thay `D-137` (4) |
| `D-140` | TẠM CHỐT (DEC) | P2 #2 — dùng lại `eCommerce:Sales:0010` + `lyDo` theo code, không tách `Return:*` |

Người chốt: không có (hồ sơ uỷ quyền `D-114` HIỆU LỰC). Hai dòng đã có trong `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | 2 P2 + 5 P3 của `SAL-006` (§2) | BA `SAL-006` |
| 2 | Arch Inventory: cách nhận biết chứng từ `-L` để sinh `DamagedIn` (0 enum, 0 migration; `TinhButToanChungTu.cs:33-36`) — buộc thêm enum/cột ⇒ hỏi lại DEC | arch Inventory |
| 3 | `SAL-003` (đã duyệt) cần sửa additive theo `D-135` (hai cạnh huỷ về `Open`) | BA/arch `SAL-003` |
| 4 | Q17 hấp thụ `SAL-003`; công thức hoàn thuế/giảm cấp đơn (`D-136`/`D-138`) cần kế toán | chủ dự án / kế toán |
| 5 | `GrandTotal` cộng thuế hai lần (`Order.cs:243`); POS hoàn `qty × unitPrice` | BA `SAL-002`, BA POS |
| 6 | Hook báo sai "thiếu khối JSON cuối" cho `ba`/`docrev`/`decider` dù có (qua kênh hand-back) | chủ dự án (`.claude/hooks`) |

**Chờ người:** Q17 và công thức kế toán (nợ #4).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: HOÀN THÀNH `P-07` → làm `P-08`** (chuẩn bị hồ sơ duyệt đợt A).

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
| `ba` | 1 (v1.6 → v1.7) | — |
| `decider` | 1 | `D-139`, `D-140` |
| `docrev` | 1 (vòng 3 kiểm nhanh) | `DOC-OK` |
