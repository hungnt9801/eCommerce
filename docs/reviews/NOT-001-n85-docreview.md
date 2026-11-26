# Doc review — `NOT-001` Template và hàng đợi thông báo (as-built `W22-3`)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (v0.7) |
| Ngày · vòng | 26/11/2026 · **vòng 2** (kiểm nhanh 5 mục BA đã sửa theo docreview vòng 1) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 1 đọc toàn văn bản trước sửa (dòng 1–1329); vòng 2 đọc lại vùng sửa: **124–133** (§1 A1/NFR-009) · **214–221** (§3 hàng 2) · **526–529 + 617–626** (§6.1) · **1282–1288** (§15.5/N6) · **1265–1280** (§15.4) · grep toàn tệp xác nhận không còn dòng cũ (`PublishPreviewDto`, "chỉ `CreateBatchAsync` sinh", "từ UI/`REP-*`") |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi; 16/16 heading, 25/25 BR có test, đối chiếu Nhanh dòng 1291) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc lại §1 A1 (dòng 127) ⇄ §3 hàng 2 (dòng 219) ⇄ BR-025/§5.3/§9; §6.1 (dòng 528, 619–626) ⇄ §6.2; NFR-009 §1 (dòng 130) ⇄ §13 (dòng 987) ⇄ `TC-28` (dòng 1033) ⇄ §10 (dòng 899) | 0 phát hiện (4 vòng-1 P2 liên quan đã đóng) |
| D2 chéo tài liệu / code / D-xx | `codegraph_explore` interface template; grep `CreateBatchAsync` toàn tệp (11 chỗ) — tất cả thống nhất "header lô / enqueue có `BatchId`"; `NOT-001-arch.md` §4.2 ⇄ §15.5 N6 | 0 phát hiện |
| D3 cơ chế thực thi | `TC-28` mới **loại trừ** panel `GetEventsAsync` (khớp §10 + `QueueTable.tsx:53`) ⇒ không còn test "đỏ trên FE đúng thiết kế"; `TC-45` giữ "server lọc = chưa kiểm được" | 0 phát hiện |
| D4 SQL chạy được | grep ` ```sql `/` ```powershell `/` ```bash ` (vòng 1 + vòng 2) | không có khối sql (0 phát hiện) |
| D5 test/probe/tệp có thật | grep các tên mới trong vùng sửa: `INotificationTemplateAppService.ArchiveAsync` (`NotificationAppServiceInterfaces.cs:17`), `PreviewResultDto` (dòng 15), `QueueTable.tsx:53` | 0 phát hiện (mọi tên có thật) |
| D6 nhãn tin cậy | header dòng 11 nay `v0.7 — 26/11/2026` khớp §15.4 dòng 1276 + §15.5; nhãn "chưa kiểm được" của `TC-45` giữ nguyên | 0 phát hiện (vòng-1 P3 #4 đã đóng) |
| D7 14 quy tắc + đối chiếu Nhanh | mục "Đối chiếu mô hình Nhanh" dòng 1291; §15.5 có N1–N6 (N6 = nợ ARCH §4.2) | 0 phát hiện (vòng-1 P3 #5 đã đóng) |

**Truy vấn đồ thị (bắt buộc trước Grep):**
- `graph: codegraph_explore "INotificationTemplateAppService GetTemplatesAsync CreateAsync PublishVersionAsync PreviewAsync ArchiveAsync PreviewResultDto"` ⇒ interface có **đúng 5** method, `ArchiveAsync(Guid)` ở dòng 17, `PreviewAsync` trả `PreviewResultDto` (khớp §6.1 đã sửa); controller `NotificationTemplatesController` map 5 op `/api/v1/notifications/templates` (khớp §6.2).
- (Kế thừa cùng phiên, vòng 1) `graph: codegraph_explore "NotificationTemplatesController NotificationQueueController NotificationBatchesController ..."` ⇒ 9 op; `graph: codegraph_explore "... CreateBatchAsync NotificationQueueAppender TaoMoi DanhDauChoDuyet"` ⇒ `CreateBatchAsync` chỉ tạo header, appender sinh `Moi→ChoDuyet` khi enqueue có `BatchId`.

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| — | — | — | Không còn phát hiện mở ở vòng 2 (0 P1 · 0 P2 · 0 P3); 5 mục vòng 1 đã đóng — xem bảng "Vòng trước" | — |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` ⇒ không có gì để chạy.
- **`TC-45` phần "server lọc `Marketing`":** vẫn đúng là **CHƯA KIỂM ĐƯỢC** (`GetTemplatesForBatch` không tồn tại; nợ BE §15.5 N1) — không phải lỗi tài liệu.
- **Cổng `pnpm lint`/`typecheck`/`test`/`build` và `dotnet test`:** chưa chạy trong lượt kiểm tài liệu; "Trạng thái triển khai" vẫn chỉ có số đo `W22-2`.
- **Ghi nhận ngoài 5 mục (có từ v0.6, không xếp mức ở vòng 2):** §0 bảng "Hiện trạng thật" (dòng 44–45) và câu mở §5 (dòng 326) vẫn ghi module `Notification` **"chưa có trong code"**, lệch với "Trạng thái triển khai"/§15.5 (module đã build + migrate ở `W22-2`). Việc này không do 5 mục vòng 2 sinh ra; đề nghị BA sửa một dòng ở lần chạm tài liệu kế tiếp (không chặn `W22-3`).

## Vòng trước (vòng 1 → vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó | Kiểm lại ở vòng 2 |
|---|---|---|---|
| 1 | đã đóng | P2 | §1 A1 (dòng 127) nay: "tạo **header** … qua `CreateBatchAsync` (**chỉ** header, **0** mục) → enqueue **từng người nhận** kèm `BatchId` (appender sinh `Moi → ChoDuyet`)"; §3 hàng 2 (dòng 219) nay: "sinh khi **enqueue có `BatchId`** (appender); `CreateBatchAsync` **chỉ** tạo header lô" — khớp BR-025/§5.3/§9/§10 và code `NotificationBatchAppService.cs:55-93`. Grep `CreateBatchAsync` (11 chỗ) không còn câu "chỉ `CreateBatchAsync` sinh". |
| 2 | đã đóng | P2 | §6.1 heading (dòng 528) nay: "hợp đồng **đã có trong code** — as-built `W22-2`"; khối interface (dòng 619–626) có `Task<PreviewResultDto> PreviewAsync(...)` + `Task ArchiveAsync(Guid id);` — khớp `NotificationAppServiceInterfaces.cs:9-17`. |
| 3 | đã đóng | P2 | NFR-009 §1 (dòng 130) + §13 (dòng 987) nay thu hẹp về "`REP-*`/**trang báo cáo**… panel chi tiết mục đọc qua `GetEventsAsync` — **ngoài** phạm vi"; `TC-28` (dòng 1033) nay "quét lớp reporting + trang danh sách báo cáo; **loại trừ** panel chi tiết mục" — hết mâu thuẫn §10/`QueueTable.tsx:53`. |
| 4 | đã đóng | P3 | Header dòng 11 nay `v0.7 — 26/11/2026` (as-built `W22-3`), khớp §15.4 dòng 1276. |
| 5 | đã đóng | P3 | §15.5 thêm hàng **N6** (dòng 1287): "`NOT-001-arch.md` §4.2 … **lỗi thời** … as-built `/api/v1` **9 op** thắng (§6.2); **nợ ARCH 1 dòng** — không sửa ARCH theo `D-266`/`D-268`; mở trước PR `tich-hop/gd3 → main`". |
