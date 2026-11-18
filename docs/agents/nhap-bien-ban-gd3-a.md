# Nháp biên bản duyệt GĐ3 — đợt A (WP-14)

> ⚠️ **NHÁP — chưa có hiệu lực duyệt.** Tệp này do orchestrator ghi trong `/doc-gate` để chủ dự án tham khảo.
> Biên bản thật (`docs/bien-ban-duyet-gd3-a-<ngày>.md`) do **chủ dự án** lập khi duyệt (`07` §WP-14 ④).

| MÃ | Tệp | DOCREV | §15 còn Open | Chờ người | Người review bắt buộc (`design-doc-guidelines.md` §6) | Đề xuất |
|---|---|---|---|---|---|---|
| ADDR-001 | [addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md](../features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md) | `DOC-OK` vòng 2 · 0 P1 · 2 P2 · 22 P3 ([docreview](../reviews/ADDR-001-docreview.md)) | 12 | 0 (Q2 đã chốt — `D-93`) | Kiến trúc trưởng + người đã liên hệ NCC (nguồn ĐVHC ngoài; Q1 hãng VC) + kế toán (địa chỉ trên hoá đơn, nợ EInvoice của `D-77`) | **Trình duyệt.** 2 P2 vòng 2 BA đã sửa (v0.5, 18/11/2026: bản ghi lô từ chối ghi `requiresNew` trước khi NÉM `0008`; khoá bận = `eCommerce:Lock:0001`) — ⚠️ sửa **sau** DOCREV vòng 2, **chưa** được DOCREV kiểm lại |
| ADDR-002 | [addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md](../features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md) | `DOC-OK` vòng 2 · 0 P1 · 2 P2 · 14 P3 ([docreview](../reviews/ADDR-002-docreview.md)) | 8 | 0 (Q12 + câu NCC của `D-92` đã trả lời "không có" — `D-94`) | Kiến trúc trưởng (máy trạng thái `CarrierMappingStatus`) + người **đã liên hệ GHN/GHTK** (tích hợp NCC) | **Trình duyệt.** 2 P2 vòng 2 BA đã sửa (v0.5, 18/11/2026: §12 tách chiều ngược `ResolveCarrierAddressCodeAsync`; §5.2 "mọi lượt đã giành được khoá") — ⚠️ sửa **sau** DOCREV vòng 2, **chưa** được DOCREV kiểm lại |
