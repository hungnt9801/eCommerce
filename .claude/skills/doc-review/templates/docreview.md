# Doc review — «MÃ» «tên tài liệu»

**VERDICT: «DOC-OK | DOC-CHANGES | DOC-BLOCKED»**

| | |
|---|---|
| Tài liệu | `docs/features/<module>/<mã>-<slug>.md` |
| Ngày · vòng | «dd/mm/yyyy» · vòng «1 \| 2» |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng «1–N» (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit «0 \| 1» («số» lỗi) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep «các ràng buộc cấm» trên toàn tài liệu | «n» phát hiện |
| D2 chéo tài liệu / code / D-xx | đối chiếu §5↔code, §6↔DTO, D-07/D-08/… | «n» phát hiện |
| D3 cơ chế thực thi | «n» BR "phải/không được" → cái gì đỏ | «n» phát hiện |
| D4 SQL chạy được | «đã chạy n khối trong BEGIN…ROLLBACK \| chưa chạy: lý do \| không có khối sql» | «n» phát hiện |
| D5 test/probe/tệp có thật | grep test/**, tools/probe-*.ps1, modules/** | «n» phát hiện |
| D6 nhãn tin cậy | ✅ có số đo đi kèm? | «n» phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14 + mục đối chiếu | «n» phát hiện |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P1 | §«x», dòng «y» | «một câu + bằng chứng» | «BA sửa gì» |

## Không kiểm được

- «mục / lý do — vd D4: không kết nối được PostgreSQL»

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng / còn (đã chép lên bảng Phát hiện) | P1 |
