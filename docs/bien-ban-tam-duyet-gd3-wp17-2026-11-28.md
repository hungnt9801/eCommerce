# TẠM DUYỆT (DEC, uỷ quyền D-318) — chưa phải biên bản người; chặn release tới khi kế toán duyệt lô M4; không merge `main`

> **WP-17 (GĐ3) · hàng `W17-0` (cổng tài liệu `D-186` ⑥)** · ngày 28/11/2026 · phiên `N113` · chuỗi `gd3-1` · nhánh `feat/gd3-wp17-gia-von`.
> Quyết định: [D-319](nhanh/decisions.md) (`TẠM CHỐT (DEC)`). Căn cứ: [hồ sơ uỷ quyền](agents/uy-quyen-chu-du-an.md) §2 M2 + §2.1 (ngoại lệ WP-17), `D-318` C1/C2/C5, `D-186` ⑥.
> Đầu vào: [DOCREV PRC-001](reviews/PRC-001-docreview.md) · [DOCREV INV-001](reviews/INV-001-docreview.md) · [dòng xem lại D-319](agents/hang-doi-xem-lai.md).
>
> - "Tạm duyệt" chỉ cho phép **công việc tài liệu trên nhánh WP** đúng phạm vi dưới; **không** mở cổng code: `D-319` ghi rõ C5(vi) (ca domain invariant K-06 + `check-agent-artifact -Kind testplan` exit 0) **chưa đạt** ở G1 và là điều kiện cứng của `W17-1`/`W17-4`.
> - Tạm duyệt áp cho bản đã qua DOCREV `DOC-OK` **0 P1/0 P2**. Tài liệu đổi mà không qua DOCREV ⇒ tạm duyệt của dòng đó **tự mất hiệu lực**.
> - Merge `main` vẫn cần biên bản **người ký** `docs/bien-ban-duyet-gd3-wp17-<ngày>.md` (`implementation-plan.md` §A.5); `W17-0n` ký phần `REP-003`.
> - **`REP-003` KHÔNG nằm trong biên bản này** (đỏ cứng §2.1) — xem mục cuối.

| # | MÃ | Tài liệu | DOCREV | Kết luận DEC | Lý do | Điều kiện (trước WP code / trước release) | Người review bắt buộc còn thiếu |
|---|---|---|---|---|---|---|---|
| 1 | PRC-001 | [prc-001](features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md) | [DOC-OK vòng 1](reviews/PRC-001-docreview.md) · P1=0 · P2=0 · P3=3 | **Tạm duyệt có điều kiện** (G1, `D-318` C2) | Ngoài danh sách đỏ §2.1; `D-186` ⑥ đồng bộ chủ động cơ giá vốn = Pricing (Q8 `Closed (D-186)`, §20.4 mục 1, §15.4 v0.7) | Trước code: `W17-1` ARCH phải có ca domain invariant K-06 (giá vốn 0 hợp lệ; thiếu ⇒ NULL + `CostMissing`) + `W17-4` `check-agent-artifact -Kind testplan` exit 0 — **C5(vi) chưa đạt ở G1**. **Q4** (luồng duyệt 2 bước `CostPrice`) **vẫn Open** — câu kế toán, decider không đóng. Trước release: kế toán duyệt lô M4/R3-3 | Kiến trúc trưởng · **kế toán trưởng** (Q4) |
| 2 | INV-001 | [inv-001](features/inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md) | [DOC-OK vòng 2](reviews/INV-001-docreview.md) · P1=0 · P2=0 · P3=2 | **Tạm duyệt có điều kiện** (G1, `D-318` C2) | Ngoài danh sách đỏ §2.1; `D-186` ⑥: §0 tách rõ giá vốn/định giá tồn kho = `PRC-001`, `ACC-001` chỉ tiêu thụ, `INV-001` chỉ cấp đơn giá nhập (ánh xạ `UnitCost`↔`UnitPrice`, `D-09`); §5.2.2 + Q8 `Closed (D-186)`; §15.4 v0.2 | Trước code: cùng C5(vi) của `W17-1`/`W17-4` (chưa đạt ở G1). Trước release: kế toán duyệt lô M4/R3-3 | Kiến trúc trưởng · kế toán |

## Phạm vi KHÔNG gồm — `REP-003` (đỏ cứng, chữ ký người ở `W17-0n`)

`D-318` C3/C8: chủ dự án **ký `REP-003` §0/Q11 kiểu (c)** tại phiên U-17; ORCH **đính 2 vị trí** ở hàng `W17-0` để `W17-0n` đối chiếu. `REP-003` **không đổi** trong phiên N113 (commit tài liệu cuối: `17e6b38`, N59). Hai vị trí hiện tại:

- **§0, dòng 47** (gạch đầu dòng "Định giá tồn kho"): đã ghi chủ = **Pricing (`PRC-001`)** (`D-186`), `INV-001` chỉ cấp đơn giá, `ACC-001` chỉ tiêu thụ, động cơ đọc Inventory qua hợp đồng chỉ đọc — **không còn nhãn Q11 mở**.
- **§15.1, dòng 938 — Q11**: trạng thái **`✅ Closed (D-186)`**.

Nếu `W17-0n` đối chiếu thấy hai vị trí trên lệch so với `D-318` C3 ⇒ **chữ ký vô hiệu**, quay lại người. DOCREV `REP-003` hiện có = `DOC-OK` (P1=0/P2=0) — **không** phải nguồn tạm duyệt của DEC.

## Tóm tắt

- Tạm duyệt có điều kiện: **2/2** tài liệu trong phạm vi (`PRC-001`, `INV-001`). `REP-003` **chưa** (đỏ cứng — chữ ký người ở `W17-0n`).
- **0/2** có chữ ký người. Lùi: xoá tệp này + dòng `D-319` ở `docs/nhanh/decisions.md` và `docs/agents/hang-doi-xem-lai.md` (`D-320 (thay thế D-319)` nếu chỉ rút tạm duyệt) ⇒ `probe-cong-tai-lieu -CheDo Code -WP WP-17` ĐỎ lại.
