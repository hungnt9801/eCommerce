# Nháp biên bản duyệt GĐ3 — đợt B (WP-14)

> ⚠️ **NHÁP — chưa có hiệu lực duyệt.** Tệp này do orchestrator ghi trong `/doc-gate` để chủ dự án tham khảo.
> Biên bản thật (`docs/bien-ban-duyet-gd3-b-<ngày>.md`) do **chủ dự án** lập khi duyệt (`07` §WP-14 ④).

| MÃ | Tệp | DOCREV | §15 còn Open | Chờ người | Người review bắt buộc (`design-doc-guidelines.md` §6) | Đề xuất |
|---|---|---|---|---|---|---|
| PRO-001 | [pro-001-voucher-coupon.md](../features/promotions/pro-001-voucher-coupon.md) | `DOC-OK` vòng 3 kiểm nhanh v0.6 (N45) · 0 P1 · **1 P2 (#20 `MaxUsagePerCustomer = NULL` chưa định nghĩa ở bước 7/BR-04/§5.2 dòng 385 — `TC-08`/`TC-59` dựa vào giá trị này; nên sửa trước khi code)** · 6 P3 (#12, #21–#25) ([docreview](../reviews/PRO-001-docreview.md)) | 8 dòng `Open` (script; Q2, Q4, Q6, Q7, Q9… — không chặn DoD) | 0 — mọi câu đã vào `D-146…D-150` (TẠM CHỐT, uỷ quyền `D-114`); CR chờ chủ: CR-11 → `PRO-002`, CR-12 → `SAL-004`, CR-P1 → `PRC-002` (chưa gửi) | Kiến trúc trưởng (máy trạng thái lượt voucher, bộ đếm, khoá) + BA (+ kế toán cho Q4 `FreeShipping` hạch toán) | **Tạm duyệt được** theo hồ sơ uỷ quyền (M2) sau khi BA sửa P2 #20; trình người sau khi xem `D-146…D-150`. SQL (`UPDATE` bước 5, `CHECK` CR-11, index) chưa đo — không có `psql`/docker; `TC-53`/`TC-59` sẽ đo |

## Tóm tắt đợt B (N45, 23/11/2026)

- 1/3 tài liệu (`PRO-001`) có `DOC-OK`; `PRO-002`, `LOY-001` chưa làm (hàng P-11).
- `PRO-001` v0.6: 64 test case · 36 BR có test · `check-feature-doc.ps1 -WP14` exit 0.
- Nợ chéo: CR-11 → `PRO-002` §5.2/§5.3 (+ `pro-002` Q6/Q7 `Closed` theo `D-146`, đối chiếu `ProPromotionMappings` theo `D-149`); CR-12 → `SAL-004`; CR-P1 → `PRC-002`.
