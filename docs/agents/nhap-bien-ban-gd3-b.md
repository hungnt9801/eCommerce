# Nháp biên bản duyệt GĐ3 — đợt B (WP-14)

> ⚠️ **NHÁP — chưa có hiệu lực duyệt.** Tệp này do orchestrator ghi trong `/doc-gate` để chủ dự án tham khảo.
> Biên bản thật (`docs/bien-ban-duyet-gd3-b-<ngày>.md`) do **chủ dự án** lập khi duyệt (`07` §WP-14 ④).

| MÃ | Tệp | DOCREV | §15 còn Open | Chờ người | Người review bắt buộc (`design-doc-guidelines.md` §6) | Đề xuất |
|---|---|---|---|---|---|---|
| PRO-001 | [pro-001-voucher-coupon.md](../features/promotions/pro-001-voucher-coupon.md) | `DOC-OK` vòng 3 kiểm nhanh v0.6 (N45) · 0 P1 · **1 P2 (#20 `MaxUsagePerCustomer = NULL` chưa định nghĩa ở bước 7/BR-04/§5.2 dòng 385 — `TC-08`/`TC-59` dựa vào giá trị này; nên sửa trước khi code)** · 6 P3 (#12, #21–#25) ([docreview](../reviews/PRO-001-docreview.md)) | 8 dòng `Open` (script; Q2, Q4, Q6, Q7, Q9… — không chặn DoD) | 0 — mọi câu đã vào `D-146…D-150` (TẠM CHỐT, uỷ quyền `D-114`); CR chờ chủ: CR-11 → `PRO-002`, CR-12 → `SAL-004`, CR-P1 → `PRC-002` (chưa gửi) | Kiến trúc trưởng (máy trạng thái lượt voucher, bộ đếm, khoá) + BA (+ kế toán cho Q4 `FreeShipping` hạch toán) | **Tạm duyệt được** theo hồ sơ uỷ quyền (M2) sau khi BA sửa P2 #20; trình người sau khi xem `D-146…D-150`. SQL (`UPDATE` bước 5, `CHECK` CR-11, index) chưa đo — không có `psql`/docker; `TC-53`/`TC-59` sẽ đo |
| PRO-002 | [pro-002-cay-dieu-kien-engine-khuyen-mai.md](../features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md) | `DOC-OK` vòng 4 kiểm nhanh v0.9 (N51) · 0 P1 · 0 P2 · 2 P3 (#1 §15.4 :1213 ghi "DoD 54 test case" trong khi §14.1/§14.3 = 55; #2 delta `pro-001` §6.1 :467/:469 còn trích số dòng cũ của `pro-002` — P3 vòng 3 #9 chưa sửa, ngoài vế đã soát) ([docreview](../reviews/PRO-002-docreview.md)) | 14 dòng `Open` (script; Q5, Q8, Q9, Q10, Q26, Q28, Q29… — không chặn DoD; Q26/Q28 chỉ chặn nhánh code) | 0 — câu thiết kế đã vào `D-160…D-163` (TẠM CHỐT, uỷ quyền `D-114`); Q27 `Closed — D-163`; Q26/Q28/Q29 còn `Open` (DEC được uỷ quyền quyết, không chặn) | Kiến trúc trưởng (engine cây điều kiện, ghép, idempotency, budget) + **kế toán** (tiền giảm cấp đơn, `D-160`/`D-163` trước release — M4) | **Tạm duyệt được** theo hồ sơ uỷ quyền (M2) — `DOC-OK` 0 P1/0 P2; kế toán duyệt "đơn đầu"/tiền giảm trước release (M4, `D-163`). 2 P3 sửa cùng WP code. SQL §5.3 chưa đo (không `psql`/docker) |

## Tóm tắt đợt B (N51, 23/11/2026)

- **2/3 tài liệu** có `DOC-OK`: `PRO-001` (v0.6) + `PRO-002` (v0.9, vòng 4 kiểm nhanh). `LOY-001` chưa làm — hàng `P-11` còn mở.
- `PRO-001` v0.6: 64 test case · 36 BR có test · `check-feature-doc.ps1 -WP14` exit 0.
- `PRO-002` v0.9: 55 test case · 20 BR có test · `check-feature-doc.ps1 -WP14` exit 0; vòng 3 → vòng 4 đóng 1 P1 + 3 P2; `D-163` (định nghĩa "đơn đầu", khác khuyến nghị BA vì vòng lách huỷ-đơn-hoàn-lượt `D-148`).
- Nợ chéo: CR-11 → `PRO-002` (đã nhận, `D-151`); CR-12 → `SAL-004`; CR-P1 → `PRC-002`.
