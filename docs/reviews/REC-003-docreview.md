# Doc review — REC-003 Đối soát COD: import file Excel theo "phiên"

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/reconciliation/rec-003-doi-soat-cod-import-excel.md` (v0.5, 1152 dòng) |
| Ngày · vòng | 20/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1152 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi: 16/16 heading, 16/16 BR có test, mục đối chiếu Nhanh ở dòng 1133; INFO 12 dòng câu hỏi `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 5/5 khối của tệp `PASS` (dòng 101, 156, 208, 253, 306) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc toàn văn; với các ràng buộc đã sửa ở vòng 1 ("cấm ghi 0", `NOT NULL`/CHECK #18, "không sửa dòng cũ", "chỉ đối chiếu sổ đã xác nhận" (đã gỡ), dấu số theo profile, nguồn `→ Superseded`, "tối đa một lệch mở mỗi `ReasonCode`", "không `ISoftDelete`", "cấm `DELETE`") grep lại toàn tài liệu: `sổ đã xác nhận`, `Failed`, `DuplicateInFile`, `DuplicateTracking`, `webhook vận đơn`, `FullAudited`, `ReasonCode` của dòng; dò TC-02 với regex ADR-08 cho từng ô ở cả hai profile (khớp); dò quy đổi kỳ TC-20 (c)/TC-44 (e) với `NgayKinhDoanh.cs:46-61` (khớp) | 6 phát hiện: #2, #4, #5, #6, #8, #9 |
| D2 chéo tài liệu / code / D-xx | Đọc `D-125`, `D-126`, `D-127` (`decisions.md:139-141`) và đối chiếu từng "việc kèm" với thân; code: `OrderStateMachine.cs:178-181,221,233-242`, `Order.cs:390-410,668-692` (`ChuyenDoiSoat` nhận `string reasonCode` không null; ETO trong cùng transaction), `OrderStatusMetricHandler.cs:20`, `IOrderStatusAppService.cs:16`, `OrderStatusAppService.cs:129`, `NgayKinhDoanh.cs:46-61`, `eCommerceMetrics.cs:212,219`, `MetricsTests.cs:108-116`, `eCommerceDbContext.cs:57-59,281,400`, `ReconErrorCodes.cs:13,22,78`, `eCommerceConsts.cs:98,193,196-199`, `eCommerceHttpApiHostModule.cs:382-414`, `DoiSoatRetention.cs:163`; `sal-003-state-machine-4-truc.md:212,266-270`; `shp-003-cod-thu-ho-doi-cod.md:211,698` (`Pending → Collected` chỉ khi dòng `REC-003` khớp) | 1 phát hiện: #1 |
| D3 cơ chế thực thi | 16 BR + bất biến mới (CHECK #18, #19; UNIQUE một phần #15; vị từ `PhatMismatchedEto`; hai bước `D-127` một UoW): tìm index/guard/test đỏ — đều có TC (TC-35, TC-39, TC-42, TC-43, TC-44, TC-41). Riêng phạm vi snapshot `MissingInFile` (Basic Flow 8 (ii)) đối chiếu với thứ tự "Superseded" ở bước 5 và A4 | 1 phát hiện: #3 |
| D4 SQL chạy được | Tài liệu **không có khối `sql`** (khối mã: không nhãn dòng 26, `mermaid` ×5, `json` ×3, `csharp` dòng 936). DDL dạng bảng §5.3 (UNIQUE một phần #15 `NULLS NOT DISTINCT … WHERE`, CHECK #18, #19) **chưa chạy**: máy không có `psql` lẫn `docker` (`which psql`/`which docker` → không có) | 0 phát hiện — không có khối sql; DDL §5.3 chưa chạy |
| D5 test/probe/tệp có thật | Grep/Read các tệp mới được nhắc ở v0.4–v0.5: `NgayKinhDoanh.cs`, `OrderStatusMetricHandler.cs`, `sal-003` dòng 212/269, `Order.cs:670-690`, `OrderStateMachine.cs:221` (`CoCanh`), `DoiSoatRetention.cs:163`, `eCommerceDbContext.cs:281` (câu trích `IRepository<ChannelShop, Guid>` — đúng), `eCommerceHttpApiHostModule.cs:394-414` — đều có thật, nội dung khớp | 0 phát hiện |
| D6 nhãn tin cậy | Dò ✅/🔶/⬜: §5.4 đã đổi 🔶 kế hoạch; ADR-02 🔶 chưa đo; §5.3 #15/#19 🔶 chưa chạy PostgreSQL; §6.2 route 🔶 tham số `id` chưa đo — đúng mức | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14: NFR 13/13 có ngưỡng + TC (NFR-001 → TC-38); guard sai §3.1 (9 cạnh) / §3.2 (6 cạnh) đủ; chống trùng 3 cửa vào; khoá 2 tên đủ; tiền minor units; §7.3 "Không áp dụng + lý do"; Mermaid PASS. Mục "Đối chiếu mô hình Nhanh" có 14 hàng + câu không lệch; đã thêm `D-125`/`D-126`/`D-127`; Q15–Q17 `Closed`, Q18 `Open` có khuyến nghị; F14/F15 có chủ. Định dạng bảng §15.1 | 3 phát hiện: #7, #10, #11 |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §1 A3 (dòng 91), §5.2 `MatchStatus` (410), BR-014 vị từ (93), §7.1 `CodLineMatchedEto`/`CodLineResolvedEto` (612, 614), §12 "vận đơn xác nhận trễ" (823) ↔ `shp-003:211` | Dòng `OutOfPeriod` **khớp đúng tiền** không bao giờ đưa `ShpCodRecords` sang `Collected`. Đường: batch có kỳ; vận đơn X giao sát cuối kỳ, job `SHP-002` đặt `ConfirmedAtUtc` sau `PeriodEndUtc` (nguyên nhân chính §12 tự nêu) ⇒ `MatchStatus = OutOfPeriod` ⇒ vị từ `D-126` phát `CodLineMismatchedEto`, **không** phát `CodLineMatchedEto` ⇒ ShippingHub chỉ nghe `CodLineMatchedEto` (§7.1, `shp-003:211`) ⇒ X giữ `Pending`; kế toán đóng lệch ⇒ `CodLineResolvedEto` chỉ tới Sales ⇒ sổ COD của X kẹt `Pending` mãi (phiên sau không có X; A4 chép nguyên kỳ nên vẫn `OutOfPeriod`) — sổ COD báo "chưa thu" một khoản đã thu, không ai thấy. Cùng gốc: dòng `AmountMismatch` đã đóng lệch cũng không tới ShippingHub | Đưa vào §15.1 thành câu hỏi mới (`needs: DEC`), chọn-một: (a) dòng `OutOfPeriod` có tiền khớp phát `CodLineMatchedEto` (lệch `OutOfPeriod` chỉ là cờ kỳ) và trục 3 đi `Matched`; (b) ShippingHub nghe thêm `CodLineResolvedEto` (cần `SHP-003` đổi guard §3.3); (c) giữ, ghi rõ đường tay để đưa `ShpCodRecords` ra khỏi `Pending`. Thêm ca vào TC-20 (d) nêu ETO nào phát cho dòng `OutOfPeriod` đúng tiền |
| 2 | P2 | §1 Basic Flow 8 (ii) (dòng 90), §1 A4 (91), §12 "Sổ COD có vận đơn mà file không có" (821), TC-20 (e) (908) | Điều kiện loại trừ "không có dòng file ở batch **khác** còn hiệu lực cùng tenant + hãng" không loại batch **cùng phiên** đang bị thay thế. Đường: A2 — hãng gửi file điều chỉnh v2 bỏ vận đơn X (v1 có X là dòng file, đã `Matched`); khi tính snapshot của v2, nếu v1 chưa được ghi `Superseded` xuống DB (A4 liệt kê "batch cũ `Superseded`" **sau** bước đối chiếu; EF không flush thay đổi trạng thái trước truy vấn) thì v1 còn "hiệu lực" ⇒ X bị loại ⇒ 0 `MissingInFile` cho X ⇒ v1 bị `Superseded` ⇒ khoản hãng rút lại không có lệch nào, trục 3 của X vẫn `Matched`. Ý định `D-125` (2) là loại trùng giữa **hai phiên** kỳ chồng nhau | Viết lại điều kiện: "batch còn hiệu lực của **phiên khác** (`SessionCode ≠` phiên đang nạp)" hoặc nêu tường minh batch cũ cùng phiên đã bị loại trước bước 8; thống nhất thứ tự "`Superseded` trước đối chiếu" ở bước 5, A2, A4. Thêm ca TC-20 (e): A2 cùng phiên bỏ X ⇒ có `MissingInFile` cho X |
| 3 | P3 | §12 "Chiến lược transaction" (dòng 840), BR-011 (93), §4.1 sequence (237-240) | Phạm vi UoW liệt kê file + batch + dòng + lệch + outbox nhưng **không** gồm việc chuyển batch cũ sang `Superseded` (bước 5 đứng trước parse ở bước 6); sequence §4.1 không có bước này. Theo thứ tự chữ, batch cũ có thể bị `Superseded` dù file mới lỗi dòng (`0303`) nếu ai tách UoW | Ghi "chuyển batch cũ `Superseded`" vào danh sách UoW của §12/BR-011 và vào §4.1 |
| 4 | P3 | §5.3 #6 (dòng 443) | Mục đích index còn ghi "+ webhook vận đơn" — v0.5 đã bỏ việc tiêu thụ ETO vận đơn (§7.2 dòng 631); người dùng thật của index là handler `CodAdjustmentConfirmedEto` (§7.2 dòng 632) | Sửa thành "+ handler `CodAdjustmentConfirmedEto`" |
| 5 | P3 | §15.2 F1 (dòng 1093) ↔ BR-010 (93), TC-24 (912) | F1 bảo "theo khuôn `PaymentReconciliation*` (entity `FullAuditedAggregateRoot` + `IMultiTenant`)", trái BR-010 (batch/dòng/file **không** `FullAudited*`/`ISoftDelete`). TC-24 sẽ đỏ nên không lọt im lặng | Thêm vào F1: "trừ `CodImportFile`/`CodReconciliationBatch`/`CodReconciliationLine` — xem BR-010" |
| 6 | P3 | §7.1 `CodLineMismatchedEto` (dòng 613) ↔ §5.2 `RecCodLines` (397-412), A3 (91) | "`reasonCode` = `ReasonCode` của dòng" nhưng `RecCodLines` không có cột `ReasonCode` và một dòng có thể có 2 lệch lúc đối chiếu (`OutOfPeriod` + `AmountMismatch`) — trường `ReasonCode` của ETO không xác định | Nêu quy tắc chọn (vd ưu tiên `AmountMismatch`, hoặc suy từ `MatchStatus`) |
| 7 | P3 | §7.1 ghi chú handler Sales (dòng 620) ↔ `D-127` ① (1) | Lời gọi bước 1 vẫn để `«reasonCode»`; `D-127` giao BA đặt giá trị (vd `CodReconRerun`), và `Order.ChuyenDoiSoat` nhận `string reasonCode` không null (`Order.cs:397`) | Chốt hằng `reasonCode` cho bước trung gian |
| 8 | P3 | §6.1 `ImportCodFileDto` (dòng 504), §6.2 (519), request mẫu (539-548) | Ba chỗ nói ba dạng nội dung file: `Content (base64/stream)`, `multipart/form-data`, JSON `contentBase64` | Chọn một (vd `IRemoteStreamContent` + `multipart/form-data`) và sửa request mẫu |
| 9 | P3 | §11 Retention (dòng 798) ↔ BR-010 (93) | "Bảng dòng/`CodDiscrepancy` giữ **tối thiểu** 24 tháng" ngụ ý được xoá sau 24 tháng, trong khi BR-010 cấm `DELETE` dòng (repository ném, FK `Restrict`) và `CodDiscrepancy` không có đường xoá | Ghi rõ "không xoá" (như file gốc) hoặc nêu ngoại lệ retention trong BR-010 |
| 10 | P3 | §15.1 dòng 1085 | Hàng Q15 và Q16 dính vào **một** dòng (`… mặc định không \|\| **Q16** \| …`) ⇒ khi hiển thị, GFM bỏ các ô thừa ⇒ Q16 (`Closed D-126`) biến mất khỏi bảng người duyệt đọc | Tách Q16 thành dòng riêng |
| 11 | P3 | TC-40 (c) (dòng 928) ↔ §7.1 (620), `D-127` ① (4) | Ca thay thế batch `Resolved` chỉ kiểm phía `REC-003`; không nêu kết quả phía Sales: mọi dòng đã `Resolved` ở trục 3 nhận `CodLine*Eto` của v2 ⇒ NÉM → DLQ (đã chấp nhận ở `D-127` (4), nợ F12 (b)) | Thêm Expected phía Sales (NÉM → DLQ, không im lặng) để QA đếm được |

## Không kiểm được

- D4: không có khối `sql` trong tài liệu; DDL §5.3 (UNIQUE `NULLS NOT DISTINCT` #1/#2/#3/#5, UNIQUE một phần #15 `… NULLS NOT DISTINCT WHERE Status IN (0, 1)`, CHECK #11–#13, #18, #19, FK #16) **chưa chạy** — máy không có `psql` và không có `docker` (`which psql`, `which docker` → không có).
- Hợp đồng đọc sổ COD và trường của `CodAdjustmentConfirmedEto` ở `SHP-003` — nợ F13 (b), ngoài tài liệu này; chỉ đọc `shp-003` dòng 211, 698 để đánh giá #1.
- Căn cứ pháp lý §11 (Luật Kế toán 88/2015/QH13 Điều 41, "10 năm") — không tự kết luận tuân thủ; cần kế toán xác nhận.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đóng (v0.5) |
|---|---|---|---|
| 1 | đã đóng | P1 | `CodDeclaredAmount`/`SourceRowNumber` nullable + CHECK #18 (dòng 404, 408, 455); §8.1 #6 (678); `TotalLineCount` không tính `MissingInFile` + `MissingInFileCount` (387, 390); TC-42 |
| 2 | đã đóng | P1 | A4 = batch `Version + 1` từ cùng `CodImportFile`, `IdempotencyKey` `:r{Version}` (91, 378, 707); `ReconcileAgainAsync` (513); TC-13 Expected cụ thể (901) |
| 3 | đã đóng | P2 | Bộ đếm là ảnh chụp lúc đối chiếu + `OpenDiscrepancyCount` + nhãn (184, 506, 782); index #17; TC-39 |
| 4 | đã đóng | P2 | Ánh xạ §3.1 ↔ enum (184); `Resolved = 4`, bỏ `Failed` (484); Trigger (d) đổi (87) |
| 5 | đã đóng | P2 | Nguồn `→ Superseded` = `Matched`/`DiscrepancyFound`/`Resolved` thống nhất ở bước 5, A2, §3.1 #8, BR-009, §12 (90, 91, 181, 93, 819); TC-40 |
| 6 | đã đóng | P2 | Cột `ThousandSeparator`/`DecimalSeparator` (352); thuật toán regex ADR-08 (69); TC-02 + mẫu test khớp regex ở cả hai profile (890, 945-968) |
| 7 | đã đóng | P2 | E3 tách hai ca (92); §12 hai hàng (815-816); TC-18 (a)/(b) (906) |
| 8 | đã đóng | P2 | Route theo quy ước ABP + 🔶 tham số `id` (519-530); thêm `reconcile-again` |
| 9 | đã đóng | P2 | `AssignDiscrepancyAsync`/`RejectDiscrepancyAsync` (508, 510), §3.2 #2/#5 (191, 194), §9 (737), TC-12 (900) |
| 10 | đã đóng | P2 | ADR-12 (73), §5.2 (380), §13 việc (4) (878-879) trỏ đúng `eCommerceMetrics.cs:219-221`; còn là nợ PR code F13 (a) |
| 11 | đã đóng | P2 | BR-010 nêu base class không `ISoftDelete` + repository ném (93); TC-23 (b)/TC-24 kiểm `IsDeleted` (911-912) — còn câu lệch ở F1 ⇒ P3 #5 vòng 2 |
| 12 | đã đóng | P2 | Đã gỡ "chỉ đối chiếu sổ đã xác nhận"; khớp cả bản ghi chưa xác nhận (90, 631, 723) |
| 13 | đã đóng | P2 | §7.2 hàng ETO vận đơn ghi "KHÔNG tiêu thụ" (631) — còn sót chữ ở §5.3 #6 ⇒ P3 #4 vòng 2 |
| 14 | đã đóng | P2 | UNIQUE một phần #15 (452); handler no-op khi còn mở (632); TC-35 (c), TC-39 |
| 15 | đã đóng | P2 | `D-127`: hai cạnh có sẵn `:181` rồi `:178` một UoW (620); TC-41 (929) |
| 16 | đã đóng | P2 | `D-125`: kỳ nullable nửa khoảng (383), CHECK #19 (456), `0308` (594), snapshot (90), TC-20/TC-44 — phát sinh #2 vòng 2 về phạm vi loại trừ |
| 17 | đã đóng | P3 | NFR-010 thống nhất §1 (94) ↔ §13 (859) |
| 18 | đã đóng | P3 | Tên test `_nem_0304`/`_nem_0305`, chữ ký 3 tham số (982, 992, 1003; 90, 238) |
| 19 | đã đóng | P3 | Nhãn ERD (323) |
| 20 | đã đóng | P3 | `eCommerceDbContext.cs:278-281` (72, 792, 1038) |
| 21 | đã đóng | P3 | Có heading `### 6.3 Mã lỗi nghiệp vụ` (583) |
| 22 | đã đóng | P3 | Post-Condition "ít nhất một, tối đa một đang mở mỗi `ReasonCode`" (89) |
| 23 | đã đóng | P3 | Bỏ `DuplicateInFile`/`DuplicateTracking` (485-486, 817) |
| 24 | đã đóng | P3 | §5.4 đổi 🔶 kế hoạch (470) |
| 25 | đã đóng | P3 | #15 trỏ `TC-35` (b), `TC-39` (452) |
| 26 | đã đóng | P3 | ADR-02 🔶 chưa đo (63) |
| 27 | đã đóng | P3 | DoR checkbox sửa (1050-1061); NFR-001 → TC-38 (850, 926) |
