---
name: doc-review
description: Bảy nhóm kiểm tài liệu D1–D7 cho vai docrev của eCommerce — tự mâu thuẫn, mâu thuẫn chéo tài liệu/code/D-xx, yêu cầu không có cơ chế thực thi, SQL phải chạy được, tên test/probe được nhắc phải có thật, nhãn tin cậy, 14 quy tắc chất lượng của design-doc-guidelines — kèm mức P1/P2/P3, luật verdict DOC-OK/DOC-CHANGES/DOC-BLOCKED và mẫu báo cáo docreview. Nạp sẵn cho docrev.
user-invocable: false
---

# Kiểm định tài liệu D1–D7

Mục tiêu: tìm chỗ **làm theo tài liệu sẽ sinh lỗi**, trước khi người duyệt. Không chấm văn phong.
Nguồn gốc: vai `65-doc-reviewer` của dự án SelfLearningAI (D1–D6) + `docs/design-doc-guidelines.md` §5 (D7).

## 1. Bảy nhóm — theo thứ tự ưu tiên

### D1 — Tự mâu thuẫn trong cùng tài liệu 🔴
Với mỗi ràng buộc **cấm** (cấm X, không được X, chỉ Y), `Grep` cả tài liệu tìm chỗ **yêu cầu** X.
Ca thật (nguồn): một mục cấm mọi trường `phone`, cùng mục 6 dòng sau yêu cầu `phoneLast4`.
Hay gặp ở eCommerce: §3 state machine nói một tập trạng thái, §6 API/§14 test dùng tập khác; §5 kiểu cột khác §6 DTO.

### D2 — Mâu thuẫn giữa tài liệu ↔ tài liệu / code / `D-xx` 🔴
Lập bảng đối chiếu cho từng thực thể chính: cột §5 có trong code (`modules/<Module>/**`) không; trường API §6 có trong DTO không; mã lỗi
dùng có trong `*ErrorCodes` không; module chủ của bảng có đúng ranh giới module không; quyết định `D-xx` (skill `feature-doc-rules` §2) có bị trái không.

### D3 — Yêu cầu không có cơ chế thực thi 🟠
Với mỗi BR "phải / không được / không bao giờ / đúng một lần": **cái gì sẽ đỏ** khi vi phạm (unique index, guard ném lỗi, test)?
"Không có gì" ⇒ phát hiện. Ca thật (nguồn): `UNIQUE (ordering_key, seq)` với `seq` là IDENTITY ⇒ không bao giờ báo vi phạm.

### D4 — SQL / lệnh trong tài liệu phải chạy được 🔴
Mọi khối `sql`, `powershell`, `bash` trong tài liệu là **cam kết chạy được**. Có PostgreSQL của `docker compose` ⇒ chạy trong
`psql ... -c "BEGIN; …; ROLLBACK;"` (hook chỉ cho đúng dạng này). Không có DB / không kết nối được ⇒ ghi **"chưa chạy: <lý do>"**.
Ca thật (nguồn): câu phát lại DLQ vi phạm CHECK + dùng CTE ở câu thứ hai ⇒ chỉ lộ khi chạy.

### D5 — Tên test / probe / tệp / endpoint được nhắc phải có thật 🟠
`Grep` trong `test/**`, `tools/probe-*.ps1`, `modules/**`. Tài liệu nói "đã có test X" mà X không có ⇒ phát hiện.
Tài liệu nói code đã có ở `file:dòng` ⇒ mở đúng dòng xác nhận.

### D6 — Nhãn tin cậy 🟡
✅ mà chưa đo ⇒ đề nghị hạ 🔶 "chưa đo". Số liệu (số ca, số dòng, ngưỡng) không có nguồn ⇒ P3/P2 tuỳ ảnh hưởng.

### D7 — 14 quy tắc chất lượng (`docs/design-doc-guidelines.md` §5) 🟠
Đi lần lượt #1–#14: BR kiểm chứng được · NFR có ngưỡng số · guard sai thì làm gì · chống trùng mọi cửa vào ngoài · index idempotency ·
tên khoá phân tán · tiền `decimal`, không `==` · không hard-code endpoint NCC · đếm ≠ dòng hiển thị · mục không áp dụng có lý do ·
Mermaid không `;` · Mermaid không `<...>` · thẻ HTML hợp lệ · đã chạy `check:mermaid`.
Thêm cho WP-14: mục **"Đối chiếu mô hình Nhanh"** có thật, khớp các `D-xx` của skill `feature-doc-rules` §2, và **thân** tài liệu đã sửa theo
(bảng ghi "đã sửa §3" mà §3 vẫn nói 7 trạng thái ⇒ D1 + D7, P1).

## 2. Mức và verdict

| Mức | Nghĩa | Ví dụ |
|---|---|---|
| **P1** | Làm theo sẽ sinh lỗi production / không triển khai được / sai tiền, tồn, trạng thái đơn im lặng | BR chống trùng không có index; SQL chạy đỏ; mâu thuẫn trạng thái đơn với `D-08` |
| **P2** | Gây hiểu nhầm, sẽ thành lỗi nếu có người sửa theo | NFR không ngưỡng; BR thiếu test §14; nhãn ✅ chưa đo ở chỗ quan trọng |
| **P3** | Hành văn, ví dụ, số liệu không ảnh hưởng hành vi | lỗi chính tả, link thừa |

`DOC-OK` = 0 P1 **và** < 3 P2 · `DOC-CHANGES` = ≥ 1 P1 hoặc ≥ 3 P2 · `DOC-BLOCKED` = thiếu mục căn bản, không kiểm được.
`tools/agents/check-agent-artifact.ps1 -Kind docreview` kiểm lại luật này bằng cách đếm cột "Mức" của bảng phát hiện — **giữ đúng mẫu bảng**.

## 3. Viết phát hiện

Mỗi phát hiện: **vị trí** (`§mục`, dòng) · **vấn đề** (một câu) · **bằng chứng** (trích ≤ 1 dòng, hoặc lệnh + kết quả) · **đề nghị** (BA sửa gì).
Không viết "nên cân nhắc". Không gộp hai lỗi vào một dòng. Mỗi P1 phải có đường dẫn tới lỗi (ai làm gì thì hỏng cái gì).

## 4. Mẫu báo cáo

Dùng đúng `templates/docreview.md` cạnh tệp này. Vòng 2: giữ bảng vòng 1 ở mục "Vòng trước", đánh dấu `đã đóng` / `còn`;
phát hiện **còn** phải chép lên bảng "Phát hiện" (script chỉ đếm mức ở cột 2 của bảng — đừng đặt mức ở cột 2 của bảng khác).
