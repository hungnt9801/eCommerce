# Doc review — NOT-002 Kênh gửi: email, SMS, Zalo OA

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md` (1109 dòng, v0.6) |
| Ngày · vòng | 28/11/2026 · **vòng 2** (kiểm bản v0.6 — BA đóng 5 P2 + 3 P3 của vòng 5) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1109 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (`DAT`; 16/16 heading · 24/24 BR có test · mục đối chiếu Nhanh :1088) |
| Đối chiếu | `NOT-002-arch.md` §3.1–§3.2, §4.1–§4.5, §6, §16 · `D-264`/`D-312`/`D-313`/`D-314`/`D-315` · as-built thật |

## Đối chiếu as-built (graph — bằng chứng lệnh)

- `graph: codegraph_explore "INotificationSender NotificationSendRequest NotificationSendResult"` → `INotificationSender.cs:8-11` (interface **1** method); `NotificationSendRequest:16-24` = **8** trường (**không** `CorrelationId`); `NotificationSendResult:26-30` = 4 trường; tệp hết ở **:30**.
- `graph: codegraph_explore "NotificationErrorCodes"` → `modules/Notification/eCommerce.Notification.Domain/NotificationErrorCodes.cs:8-42`, dải **`0010–0020`** (`0020 = BodyFormatKhongHoTro`).
- `graph: codegraph_explore "eCommerceMetrics … All"` → `eCommerceMetricNames.All` (`:157-209`) đếm **39** tên; **7** metric thông báo ở `:187-193` (6 counter + 1 gauge); `eCommerceMetricTags.Forbidden` ở **`:364–368`** (chứa `email`, `phone`).
- `graph: codegraph_explore "NotificationQueueStatus … DanhDauLoi"` → `NotificationQueueStatus.Loi = 7`; `DanhDauLoi` (`NotificationQueueEntities.cs:297`) chuyển sang `Loi`; `NotificationDispatchWorker.cs:339` gọi `DanhDauLoi`.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc toàn văn + `grep` các ràng buộc cấm; so cặp dễ mâu thuẫn: §5 trạng thái code (:300) ↔ header(:6)/§0.1(:32); "39 metric" (:41/:92/:1024/:1056) ↔ mục "Đối chiếu Nhanh" (:1097 "16"); `INotificationSender.cs:8-27` (:33/:1106) ↔ `:8-30` (:470/:533) | 2 phát hiện mới (#2, #3) |
| D2 chéo tài liệu / code / `D-xx` | `graph` xác nhận `INotificationSender`/`NotificationSendRequest`/`NotificationSendResult`, `NotificationErrorCodes`, `eCommerceMetricNames.All` = 39, `Forbidden` :364-368, `NotificationQueueStatus.Loi`. Đối chiếu `NOT-002-arch.md` §4.4 (:16-24, 8 trường) · §4.5 (#2 → `0006` → `DanhDauLoi`, không NÉM) · §16 #6 "11 endpoint" · `D-264`/`D-313`/`D-314`/`D-315` — khớp | 1 phát hiện (#1, con trỏ dòng) |
| D3 cơ chế thực thi | BR-002/BR-019/BR-020 vẫn có UNIQUE index §5.1 :333 / §5.3 :381 / §5.6 :457; fail-closed `0003` có cơ chế **NÉM** (arch §6). Không BR mới nào thiếu cơ chế | 0 |
| D4 SQL chạy được | `grep '^```(sql\|powershell\|bash\|ps1)'` = **0**; chỉ **3** `mermaid` + **2** `csharp` minh hoạ kế hoạch ⇒ không có gì phải chạy trên PostgreSQL | 0 |
| D5 test/probe/tệp có thật | Tệp `INotificationSender.cs`, `NotificationErrorCodes.cs` **thật** (graph). `MetricsTests.cs` có `TC06_phat_ra_DUNG…` (:39), `TC06_khong_co_metric…` (:50), `Bang_dang_ky_KHONG_trung…` (:70) — thật; các dẫn dòng `:38–47`/`:45–73`/`:64–73` đều trỏ tới test thật cùng tệp (khác khoảng nhưng cùng đích — ghi nhận, không thành phát hiện). Không tên test/probe bịa | 1 phát hiện (#3, khoảng dòng) |
| D6 nhãn tin cậy | Rà ✅/🔶 ở §0.1: 17 module · `Notification` 28 tệp · **39** metric · **7** metric thông báo `NOT-001` · `NOT-002` 0 metric mới (`D-315` AQ7) — **đúng**. Sót nhãn số ở mục "Đối chiếu Nhanh" (:1097 "16") | 1 phát hiện (#2) |
| D7 14 quy tắc + đối chiếu Nhanh | BR kiểm chứng được · NFR có ngưỡng số · guard sai có hành vi · chống trùng mọi cửa + index · tên khoá phân tán chính xác §8.2 · `decimal`/cấm `==` · không hard-code endpoint NCC · đếm≠dòng hiển thị §10 · 3 Mermaid (`«…»`, không `<…>`). Mục "Đối chiếu mô hình Nhanh (24/11/2026)" :1088 có thật, đã có hàng `D-264`/`D-312`/`D-313-314`/`D-315` | 0 (số liệu cũ ở :1097 đã tính ở #2) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §0.1 :34, :43 · ADR-06 :91 · §8.1 :688 · §15.3 :1072 (5 chỗ) | Dẫn `eCommerceMetrics.cs:218–223` cho `eCommerceMetricTags.Forbidden`, nhưng `Forbidden` thật ở **`:364–368`**; dòng 218–223 chỉ là comment đo lường phát metric (S10/S16/S16b), không liên quan. Ràng buộc `email`/`phone` **vẫn đúng** và có test `TC-07`; chỉ con trỏ dòng đã trôi khi tệp lớn lên (39 metric). Không phải do v0.6. | Sửa 5 chỗ thành `eCommerceMetrics.cs:364–368`. |
| 2 | P3 | Mục "Đối chiếu mô hình Nhanh" dòng 1097 | Hàng `as-built` ghi "**16** metric có điểm phát" + `eCommerceMetrics.cs:81–107`, **mâu thuẫn** §0.1 :41 / ADR-07 :92 / §14.4 :1024 / F3 :1056 ("**39**"). Đây là mâu thuẫn **do v0.6 để lại** — 4 nơi chính đã sửa thành 39 nhưng quên hàng bảng lịch sử này. | Cập nhật :1097 thành **39** metric (dẫn `:157–209`). |
| 3 | P3 | §0.1 :33 và hàng `D-264` :1106 | Dẫn `INotificationSender.cs:8-27`, trong khi §6.1 :470/:533 dẫn `:8-30`. `:8-27` cắt mất 3 trường cuối `NotificationSendResult` (tệp hết ở :30). Cùng một tệp, hai khoảng dòng khác nhau trong cùng tài liệu (vòng-5 #5 mới sửa §6.1, chưa sửa 2 chỗ còn lại). | Đồng bộ tất cả về `:8-30` (interface `:8-11`). |

## Không kiểm được

- **D4/`check:mermaid`**: tài liệu **0** khối `sql`/`powershell`/`bash` để chạy trên PostgreSQL; **chưa chạy** `check:mermaid` (hook vai DOCREV chỉ cho lệnh đọc + `psql` `BEGIN…ROLLBACK`). Mermaid của v0.6 **không đổi cấu trúc** (chỉ sửa text) ⇒ rủi ro thấp.
- **Không chạy `dotnet test`/`probe-cong-tai-lieu.ps1`**: 47 TC §14 là **kế hoạch**; WP-23 **0** code ứng dụng mới (fail-closed `0003` là hợp đồng, `D-313`/`D-314`).
- Không đọc lời giải thích của BA; chỉ đối chiếu tài liệu, ARCH, `D-xx`, as-built.

## Vòng trước — vòng 5 (v0.5) → vòng 2 (v0.6)

| # vòng 5 | Trạng thái | Mức lúc đó | Xác minh vòng 2 (v0.6) |
|---|---|---|---|
| 1 | **đã đóng** | P2 | §5 :300 nay: "**6 bảng dưới đây** chưa có trong code (module `Notification` **đã có** ở WP-22 — `D-312`)" — khớp header :6 / §0.1 :32; bỏ câu "module chưa có" |
| 2 | **đã đóng** | P2 | §6.2 :592: "**tệp đã có** (`NotificationErrorCodes.cs:8-42`, dải `0010–0020`); `NOT-002` thêm `0001–0009` cùng tệp" — graph xác nhận tệp :8-42, dải 0010–0020 |
| 3 | **đã đóng** | P2 | §0.1 :42 "**7** metric `NOT-001` (6 counter + 1 gauge)"; :41/:92/:1024/:1056 "**39** metric"; graph: `All` = 39, 7 metric thông báo :187-193. (Sót hàng :1097 → phát hiện mới #2) |
| 4 | **đã đóng** | P2 | §3 hàng 3 :209 + note :223 + §12 :813: `Loi`/`DanhDauLoi` = trạng thái hàng đợi as-built `NOT-001` (`NotificationQueueStatus`), **không** thuộc tập §3; graph: `Loi = 7`, `DanhDauLoi` :297, worker :339 |
| 5 | **đã đóng** | P2 | §6.1 :470/:533 "as-built `:8-30` (interface 1 method; request :16-24 **8** trường; result :26-30)"; `W23-2` **thêm** `CorrelationId?` additive (`D-315` AQ4), interface không đổi — graph khớp. (Sót `:8-27` ở :33/:1106 → phát hiện mới #3) |
| 6 | **đã đóng** | P3 | §14.2 :968 dùng `LayNotificationMessage(yeuCau.QueueItemId)` + comment "`NotificationSendResult` KHÔNG có `MessageId`"; bỏ `.MessageId` |
| 7 | **đã đóng** | P3 | §1 A3 :114: "mẫu `ops-alert` do `NOT-001` chọn ở bước enqueue (`NotificationSendRequest` **không** mang `TemplateCode`)" — hết mâu thuẫn với Basic Flow :113 |
| 8 | **đã đóng** | P3 | §6.2 :569 nêu rõ "**11** endpoint (**8** lõi + **3** `opt-outs`); ARCH §1.3 '8' chưa tính 3 opt-out, §16 #6 cũng ghi 11". Đếm bảng :573–583 = **11** dòng ⇒ SRS **tự nhất quán** (lệch còn lại nằm trong ARCH — ngoài vùng ghi) |

```json
{
  "agent": "DOCREV",
  "doc": "docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md",
  "artifact": "docs/reviews/NOT-002-docreview.md",
  "round": 2,
  "verdict": "DOC-OK",
  "p1": 0,
  "p2": 0,
  "p3": 3,
  "sqlRun": "không có khối sql",
  "readRanges": ["1-1109"],
  "mustFix": []
}
```
