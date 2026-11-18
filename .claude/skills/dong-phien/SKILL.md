---
name: dong-phien
description: Đóng phiên eCommerce đúng docs/nhanh/08-session-protocol.md §5 — đo lại, viết handover-n<x>.md đúng 7 mục kèm số đo, liệt kê D-xx decider đã ghi trong phiên, gom MỘT lần mọi câu chờ người, nhật ký hàng rào hook, và đúng một khối câu mở phiên A hoặc B đã điền. Gọi tay — /dong-phien.
disable-model-invocation: true
---

# /dong-phien — đóng phiên

## 1. Đo lại (cùng bộ lệnh của `/mo-phien` §3) — ghi exit + số

Chỉ cắt phiên ở **điểm xanh** (CLAUDE.md §4). Đang dở giữa migration/backfill ⇒ không đóng, làm tới điểm xanh trước.

## 2. Thu thập

- `D-xx` mới trong phiên: grep `docs/nhanh/decisions.md` theo mã phiên (`phiên <Nx>`) — tách `TẠM CHỐT (DEC)` / `ĐỀ XUẤT (DEC)` / người chốt.
  Chạy `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` → exit 0.
- Câu **chờ người** (CHUYEN_NGUOI + `needs = NCC | ke-toan | nguoi` từ `/doc-gate`): gom thành **một** bảng, mỗi câu có khuyến nghị + việc đã làm tiếp.
- Nhật ký hàng rào: `artifacts/agents/<ngày>.jsonl` (số lượt mỗi vai, số lần chặn dừng) và `artifacts/agents/canh-bao.jsonl` (ghi ngoài vùng — phải bằng 0 lần lọt).
- Hạn mức: hỏi người dùng % hạn mức 5 giờ / tuần trong `/usage` **chỉ nếu** họ đang ở đó; không có ⇒ ghi "chưa đo".

## 3. Viết `docs/nhanh/handover-n<x>.md` — đúng 7 mục (`08` §5)

1. **Đã xong** — việc + số đo + tên lệnh (bảng `/mo-phien` §4 + lần đo lại).
2. **Chưa xong / cắt giữa** — mã nào ở bước nào (`/doc-gate` D1…D4), tệp đang sửa dở.
3. **Migration đã áp** — WP thuần tài liệu: "Không có".
4. **Quyết định mới** — các `D-xx` của §2 (tách DEC tạm chốt / chờ người).
5. **Nợ / rủi ro** — kèm chủ nợ; thêm dòng "Chờ người" (bảng §2) nếu có.
6. **Việc đầu tiên của phiên sau** — dòng trạng thái `XONG <WP> → mở <WP kế>` hoặc `DỞ DANG <WP> → mở lại <WP>`, 1–3 gạch đầu dòng kèm lệnh,
   rồi **đúng một** khối A hoặc B **đã điền hết** (không còn `<…>`), theo `08` §5.1. Phiên kế dùng `/mo-phien <WP> <Nx+1>`.
7. **Trạng thái nhánh** — nhánh, SHA ngắn (nguồn sự thật `git log -1`), đã push chưa, PR nào.

Thêm sau mục 7 (không đánh số, ngắn): **"Hàng rào agent"** — bảng lượt gọi theo vai, lần chặn, cảnh báo ngoài vùng.

## 4. Kiểm handover

`pwsh -File tools/kiem-link-tai-lieu.ps1` → PASS. Cập nhật `docs/nhanh/07-ke-hoach-trien-khai.md` §13 (tiến độ) một dòng có số đo.

## 5. Câu trả lời cuối cho người dùng

1. Bảng **chờ người** (nếu có) — hỏi **một lần**, mỗi câu có khuyến nghị đứng đầu.
2. Dòng trạng thái + **đúng một** khối A/B đã điền.
3. Lệnh commit **đề xuất** (`git add` theo đường dẫn cụ thể, message ASCII `docs(nhanh): <Nx> - …`) — chỉ chạy khi người dùng yêu cầu (CLAUDE.md §8).
