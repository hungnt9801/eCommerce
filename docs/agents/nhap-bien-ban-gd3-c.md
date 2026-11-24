# Nháp biên bản duyệt GĐ3 — đợt C (WP-14)

> ⚠️ **NHÁP — chưa có hiệu lực duyệt.** Tệp này do orchestrator ghi trong `/doc-gate` để chủ dự án tham khảo.
> Biên bản thật (`docs/bien-ban-duyet-gd3-c-<ngày>.md`) do **chủ dự án** lập khi duyệt (`07` §WP-14 ④) — `ACC-001` và `REP-002` **bắt buộc kế toán** (`D-114` M4).
>
> Cập nhật **N56 (24/11/2026)** — hàng `P-14` (`doc-gate`, đợt C): mới chạy `REP-001`; `REP-002` chưa chạy (luật "≥ 5 câu gửi decider ⇒ một tài liệu/phiên" — `REP-001` đã dùng 5 câu). Hàng `P-18` (`tong-hop`) điền đủ 6 dòng của đợt.
> Cập nhật **N57 (24/11/2026)** — `REP-001` sửa xong 8 P2 vòng 2; DOCREV **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 10 P3) ⇒ đủ điều kiện **trình duyệt**. `REP-002` chạy đủ dây chuyền: 7 câu §15 chốt (6 `TIEN_LE` + `D-175`); DOCREV **vòng 2 `DOC-CHANGES`** (0 P1 · **5 P2** · 3 P3) — hết trần 2 vòng, còn 5 P2 (đỏ cứng, cần kế toán).

## 1. Bảng tài liệu đợt C

| MÃ | Tệp | DOCREV | §15 còn Open | Chờ người | Người review bắt buộc (`design-doc-guidelines.md` §6) | Đề xuất |
|---|---|---|---|---|---|---|
| REP-001 | [rep-001-read-model-job-tong-hop.md](../features/reporting/rep-001-read-model-job-tong-hop.md) | **`DOC-OK`** vòng 3 kiểm nhanh (24/11/2026) · 0 P1 · **0 P2** · 10 P3 ([docreview](../reviews/REP-001-docreview.md)); 8 P2 vòng 2 đã đóng, xác nhận bằng đồ thị | 0 câu (Q1/Q2/Q4/Q6/Q8 `Closed` bởi `D-170`…`D-174`; 3 dòng `Open` còn lại là mô tả lịch sử trong bảng đối chiếu) | 0 câu chờ người; 10 P3 để người duyệt; kế toán duyệt lô M4 (`D-172` ngưỡng vận hành; `D-173` COD tách khỏi `REC-003`) trước release | **Kiến trúc trưởng** (read-model theo lô, kế hoạch `RepPeriodClose`) + **kế toán** (giá vốn/ghi nhận: `D-114` P2/P4) | **Trình duyệt** — DOC-OK, 0 P1/0 P2; 10 P3 để người duyệt |
| REP-002 | [rep-002-bao-cao-doanh-thu-lai-gop.md](../features/reporting/rep-002-bao-cao-doanh-thu-lai-gop.md) | `DOC-CHANGES` **vòng 2** (24/11/2026) · 0 P1 · **5 P2** · 3 P3 ([docreview](../reviews/REP-002-docreview.md)); hết trần 2 vòng ⇒ ghi [`doc-gate-2026-11-24.md`](doc-gate-2026-11-24.md) | 0 câu (7 câu §15 chốt: 6 `TIEN_LE` `D-114`/`D-48`/`D-172`/`D-03`/`D-36` + `D-175`) | 0 câu chờ người; 5 P2 + 3 P3 cần sửa/duyệt trước khi trình | Kiến trúc trưởng + **kế toán** (đỏ cứng `D-114` §2.1) | **Chưa** — còn 5 P2 (node Mermaid PRC, allow-list TC-02, chỗ chụp giá vốn, SAL-002, BR-018 huỷ muộn); sửa rồi DOCREV vòng kiểm nhanh |
| REP-003 | [rep-003-bao-cao-ton-kho-hieu-suat-kenh.md](../features/reporting/rep-003-bao-cao-ton-kho-hieu-suat-kenh.md) | chưa chạy | — | — | Kiến trúc trưởng + **kế toán** (đỏ cứng) | Chưa bắt đầu — hàng `P-15` |
| ACC-001 | [acc-001-xuat-lieu-ke-toan-excel-csv.md](../features/accounting/acc-001-xuat-lieu-ke-toan-excel-csv.md) | chưa chạy | — | — | Kiến trúc trưởng + **kế toán** (đỏ cứng, R3-3/R3-8) | Chưa bắt đầu — hàng `P-15`, bắt buộc kế toán (`P-19`) |
| NOT-001 | [not-001-template-hang-doi-thong-bao.md](../features/notifications/not-001-template-hang-doi-thong-bao.md) | chưa chạy | — | — | BA + Kiến trúc trưởng | Chưa bắt đầu — hàng `P-16` |
| NOT-002 | [not-002-kenh-gui-email-sms-zalo-oa.md](../features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md) | chưa chạy | — | — | BA + Kiến trúc trưởng + **người** (khoá HMAC/chọn kênh, R3-6) | Chưa bắt đầu — hàng `P-17` |

## 2. Tóm tắt phần đã chạy (N56–N57)

- **`REP-001`**: BA đối chiếu lại toàn thân theo as-built `WP-08` (module `Reporting` + 8 bảng `Rpt*` + `ReportAppService` đã có) và `D-47`/`D-48`; 5 câu §15 chốt bằng `D-170`…`D-174`; `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục · 17/17 BR có test). **N57:** BA sửa 8 P2 vòng 2 (sai as-built: kiểu tiền, route, DTO, `TenantId`, Testcontainers, cơ chế BR-002, khoá cache/JSON) → DOCREV **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 10 P3, mỗi phát hiện code xác nhận bằng dòng `graph:`).
- **`REP-002`** (N57): BA đối chiếu + 7 câu §15 → decider (6 `TIEN_LE` theo `D-114` P2/P3/P4 + `D-48` + `D-172` + `D-03`/`D-36`; 1 `TẠM CHỐT` `D-175`); BA áp 7 quyết định. DOCREV vòng 1 `DOC-CHANGES` (1 P1 · 6 P2) → sửa → **vòng 2 `DOC-CHANGES` (0 P1 · 5 P2 · 3 P3)** — hết trần 2 vòng. `REP-002` là **đỏ cứng** (`D-114` §2.1): cần người/kế toán ký biên bản trước khi code.

## 3. Việc cần trước khi trình đợt C

1. ✅ **N57:** BA sửa 8 P2 `REP-001` → `check-feature-doc` DAT → `DOCREV` vòng 3 kiểm nhanh `DOC-OK` (0 P1 · 0 P2). 10 P3 để người duyệt.
2. **N57:** `REP-002` đã chạy đủ dây chuyền, DOCREV vòng 2 `DOC-CHANGES` (0 P1 · 5 P2). **Phiên kế:** BA sửa 5 P2 + DOCREV vòng kiểm nhanh → `DOC-OK`.
3. Kế toán duyệt lô M4 cho `REP-001`/`REP-002` trước release; `REP-002` + `ACC-001` **bắt buộc** ký ở biên bản người (`P-19`).
