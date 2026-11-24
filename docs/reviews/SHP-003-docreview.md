# Doc review — SHP-003 COD: thu hộ, đổi COD (GHN cần OTP)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/shipping/shp-003-cod-thu-ho-doi-cod.md` (v0.5 theo §15.4, 1257 dòng) |
| Ngày · vòng | 20/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1257 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi: 16/16 heading, 22/22 BR có test, mục đối chiếu ở dòng 1236; 8 dòng §15 `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 212 khối, 0 lỗi (3 khối SHP-003 PASS: :101, :163, :237) · `kiem-link-tai-lieu.ps1` → PASS, 0 tham chiếu chết (161 tệp, 759 link, 893 đường dẫn backtick) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Vòng 2: kiểm lại 26 mục vòng 1 trên thân mới; grep `Shipping:00\d\d`, `` `00(0[4-9]\|1\d\|2[0-5])` ``, `AcquireAsync`, `AlreadyProcessed`, `cod-discrepancies`, `6 permission`, `47 test` — chỉ còn ở dòng lịch sử (:614, :1026, :1205, :1230, :1249, :1256). Với mỗi ràng buộc mới (`RequestKey` bắt buộc, partial UNIQUE BR-022, so-và-đặt #15, `OtpSent → OtpExpired`, `ApprovedBy IS NULL`, Reject), đối chiếu §1 A/E ↔ §3.1 sơ đồ ↔ §3.2 #1–#17 ↔ §6.1/§6.2 ↔ §12 ↔ §14 | 1 P2 (#1) + 5 P3 (#4, #5, #6, #7, #12) |
| D2 chéo tài liệu / code / D-xx | Đọc dòng gốc `D-120` (`decisions.md:134`); `shp-002` §5.2 :429–430, :460–461 (`RunId varchar(40)`, `Kind` 0–5), §6.2 :619–628 (`0201`–`0210`, `0207` = `403`/`404`), §7.1 :694 (`ShipmentDiscrepancyFoundEto`); `shp-001` :645, :647 (`0109`, `0111`), `ShipmentCreatedEto` (:57, :250), `IShipmentCodAppService` (11 chỗ); code: `IdempotencyGuard.cs:27-167` (nhánh `ThatBai` :124–126 cho chạy lại **không** so payload), `IdempotencyModels.cs:88-118`, `IdempotencyRecord.cs:95`, `PosController.cs:28-30,75-86` (so header ↔ body ở **controller**), `CheckoutDtos.cs:62-69`, `Order.cs:292-305`, `OrderStatusHistory.cs:28,31`, `eCommerceHttpApiHostModule.cs:289-309` | 4 P3 (#3, #8, #10, #11) |
| D3 cơ chế thực thi | 22 BR + 13 NFR + §8.1 15 bất biến: BR-012 → UNIQUE `(TenantId, IdempotencyKey)` + `0315` + TC-04/TC-57; BR-022 → partial UNIQUE `WHERE Status IN (0,1,2,3,6)` + TC-49; §8.1 #15 → `UPDATE … WHERE CodAmount = @old` + TC-50; NFR-001…013 → TC-24/30/31/32/51–56 (đếm lại cột "Phủ" §14.1); lối ra của mọi trạng thái chưa hấp thụ (`Requested`, `PendingApproval`, `OtpSent`, `OtpFailed`, `Unknown`) | 1 P2 (#2) |
| D4 SQL chạy được | `Grep` `^```(sql\|powershell\|bash\|pwsh)` → 0 khối. Biểu thức CHECK/UNIQUE inline §5.2 :400–413 **chưa chạy**: `psql --version` → `command not found` (exit 127), không có PostgreSQL. Kiểm bằng đọc: mọi cột trong index/CHECK có trong bảng cột :338–363, :373–386 (lỗi `ProviderCode` vòng 1 đã hết) | 0 khối chạy; 0 phát hiện |
| D5 test/probe/tệp có thật | Mở đúng dòng: `PosController.cs:28-30,75-86` ✔, `CheckoutDtos.cs:62-69` ✔, `Order.cs:300-305` ✔ (NÉM COD âm), `OrderStatusHistory.cs:28,31` ✔ (`int FromStatus/ToStatus`), `IdempotencyGuard.cs:115-116` ✔, `IdempotencyModels.cs:85-98` ✔, `eCommerceHttpApiHostModule.cs:297-304` ✔ (`AbpExceptionHttpStatusCodeOptions.Map`); `shp-002` `RunId` đủ chỗ cho `"adj:" + Guid` (4 + 36 = 40 = `varchar(40)`) ✔; TC-01…TC-60 liên tục, không trùng ID | 0 phát hiện |
| D6 nhãn tin cậy | Đi mọi ✅/🔶: §7.3 kết luận 1/3 đã hạ 🔶 (:744, :746), §14.4 :1163 🔶; §6.4 ✅ khớp code; §6.2 :613/:615 ✅ "đã chốt" có `D-114`/`D-120` làm nguồn; header :21 còn ghi `v0.4` | 1 P3 (#9) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14: BR kiểm chứng được ✔ (22/22 có TC); NFR có ngưỡng + TC ✔ (13/13); guard sai làm gì ✔ trừ #1/#2; chống trùng mọi cửa vào ✔ (§7.2 thêm `ShipmentCreatedEto`); index idempotency ✔; tên khoá ✔ (§8.2); tiền `decimal`, không `==` ✔; endpoint NCC trong cấu hình, `null` không mặc định ✔; đếm ≠ dòng ✔ (§10); mục không áp dụng có lý do ✔ (§4.2, §5.1); Mermaid ✔ (cổng 0 lỗi). Mục "Đối chiếu mô hình Nhanh" :1236–1257 có 18 hàng, đủ `D-114` R0 `SHP-001`-Q7, `D-116`/`D-118`, `D-119`, `D-120`; thân đã sửa theo từng hàng | 0 phát hiện mới (#1, #2 đếm ở D1/D3) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §3.2 #8 :196, #9 :197, #15 :204; §1 E7 :91; §12 :915; TC-11 :1004 | Cùng một tình huống (CSKH nhập OTP sau khi đã sai đủ `OtpMaxAttempts` lần) có **ba** mã: #9 chuyển `OtpFailed → OtpExpired` ngay khi `OtpAttemptCount ≥ OtpMaxAttempts` (lần sai thứ 5), nên lần nhập thứ 6 gặp bản ghi `OtpExpired` ⇒ theo #15 (hấp thụ) NÉM `0302`, theo #9 ("`VerifyAsync` trên bản ghi đã hết hạn") NÉM `0303`; nhưng TC-11 đòi "lần 6 ⇒ `0306`" và §12 :915 gắn `0306` (`429`). BE làm đúng §3.2 thì TC-11 đỏ, làm theo TC-11 thì trái #15; UI không biết hiện "OTP sai" (`400`) hay "vượt số lần thử" (`429`) | Chốt một: lần sai làm `OtpAttemptCount` chạm trần NÉM `0306` và chuyển `OtpExpired`; mọi `VerifyAsync` sau đó trên bản ghi `OtpExpired` (do số lần **hoặc** TTL) NÉM một mã duy nhất, ghi rõ ở #9 và #15 (ngoại lệ của "hấp thụ ⇒ `0302`"); sửa E7 ("vượt" ⇒ "chạm"), §12, TC-11 (ghi lần 5 và lần 6 ra mã gì) |
| 2 | P2 | §3.2 #12 :200, #17 :202; §1 E17 :91; TC-59 :1052; §6.1 :534; `D-120` Q13 (`decisions.md:134`) | Yêu cầu `Unknown` khi COD hãng **khác cả cũ lẫn mới** (vd CSKH sửa COD thẳng trên cổng GHN lúc yêu cầu đang `Unknown`), hoặc khi so-và-đặt lệch ở #12, không có lối ra trong hệ thống: #17 bảo kế toán `ResolveDiscrepancyAsync` "rồi mới đi #12/#13", nhưng guard #12/#13 là "COD **hãng** = số mới / số cũ" — đóng `Discrepancy` không đổi số của hãng; §6.1 không có method nào cho kế toán đưa yêu cầu về `Failed`/`Confirmed`. Đường hỏng: yêu cầu `Unknown` (`250k → 300k`), hãng thực có `270k` ⇒ job mỗi nhịp giữ `Unknown` ⇒ partial UNIQUE (BR-022) giữ chỗ vĩnh viễn ⇒ mọi lần đổi COD sau đó của vận đơn NÉM `0313`, trong khi `ShpShipments.CodAmount = 250k` ≠ số shipper thu. `D-120` Q13 coi `ResolveDiscrepancyAsync` là lối ra của `Unknown`, thân tài liệu chưa nói nó làm gì với yêu cầu | Đặc tả lối ra cho #17: vd thao tác kế toán (quyền `.Resolve` hoặc `.Approve`) đóng yêu cầu `Unknown` sang `Failed` kèm `Reason`, **không** sửa `CodAmount`, `Discrepancy.AmountMismatch` giữ mở cho đối soát; hoặc ghi rõ `ResolveDiscrepancyAsync` của `SHP-002` chuyển yêu cầu liên quan. Thêm cạnh vào §3.1/§3.2, method + permission vào §6.1/§9, ca vào TC-59 |
| 3 | P3 | D6 :68; §6.1 :444, :529; TC-57 (b) :1050 | Đòi so header `Idempotency-Key` ↔ `RequestKey` "ở **app service**", nhưng chữ ký `RequestAsync(RequestCodAdjustmentDto, ct)` / `RequestAdjustmentAsync(RequestCodAdjustmentDto)` không nhận header; khuôn được dẫn (`PosController.cs:75-86`) so ở **controller**. TC-57 (b) loại "Unit" không có chỗ đưa header vào. Ảnh hưởng thấp vì body là nguồn sự thật | Ghi cách header tới app service (tham số `string? idempotencyKeyHeader` do controller truyền, hoặc so ở controller như `PosController`); đổi TC-57 (b) sang integration HTTP nếu so ở controller |
| 4 | P3 | §3.1 :171 | Nhãn cạnh `OtpSent --> OtpFailed : «OTP sai hoặc hết hạn»` trái #7 (chỉ khi hãng trả `OTP_NOT_VALID`) và #9 (hết hạn ⇒ `OtpSent → OtpExpired`, không qua `OtpFailed`; TC-10 cũng khẳng định "không đi qua `OtpFailed`") | Đổi nhãn thành «hãng trả OTP_NOT_VALID» |
| 5 | P3 | §6.1 :453, :456 vs :531–532; §1 A3/A6 :90; §12 :907; TC-14 :1007 | Hai bộ tên cho cùng thao tác: port `ApproveAsync`/`RejectAsync`, app service `ApproveAdjustmentAsync`/`RejectAdjustmentAsync`; §12 :907 trộn "`ApproveAsync`/`RejectAdjustmentAsync`" | Giữ cả hai tầng nhưng ghi rõ tầng ở mỗi chỗ nhắc, hoặc thống nhất một tên |
| 6 | P3 | §6.2 `0305` :597; §9 :832 | Thông điệp `0305` ("vượt ngưỡng cho phép, cần người có quyền phê duyệt") không khớp nghĩa đã đổi (gọi duyệt/từ chối khi thiếu quyền hoặc sai trạng thái). Thêm: nếu `.Approve` gắn bằng `[Authorize]` trên method thì ABP ném lỗi phân quyền của nó trước, nhánh "thiếu quyền ⇒ `0305`" không bao giờ chạy | Đổi thông điệp; ghi `0305` chỉ cho "yêu cầu không ở `PendingApproval`", thiếu quyền = lỗi phân quyền của ABP (`403`) |
| 7 | P3 | §1 Pre-Condition :87; §6.2 `0207` :592; §6.3 :668–669; §12 :897 | `0207` (chủ `SHP-002`: "hãng chưa được cấu hình") được dùng lại cho Setting **tenant** `Cod.OtpTtlSeconds`/`Cod.OtpMaxAttempts` trống — nghĩa khác; `D-120` ① Q14 đặt luật "chỉ dùng lại mã dải khác khi **cùng nghĩa + cùng HTTP**". Thông điệp ở :592 cũng khác thông điệp `SHP-002` :625 cho cùng mã (localization theo mã chỉ giữ được một) | Khai mã `03xx` riêng cho "Setting COD của tenant chưa điền", hoặc ghi lý do coi là cùng nghĩa; dùng đúng thông điệp của `SHP-002` |
| 8 | P3 | §12 :907 | Hứa `202` cho yêu cầu vào `PendingApproval`, nhưng §6.1/§6.2 trả `CodAdjustmentDto` qua app service (ABP trả `200`), không nói cơ chế trả `202`; không TC nào kiểm | Đổi thành `200` + `status = PendingApproval`, hoặc ghi cách trả `202` |
| 9 | P3 | Header :21 | Header còn `v0.4` và không liệt kê `D-120`, trong khi §15.4 :1232 có v0.5 áp `D-120` | Sửa header thành v0.5, thêm `D-120` |
| 10 | P3 | §7.1 :699; `shp-002` :694 | Hai ETO cho cùng một dòng `ShpDiscrepancies`: SHP-003 phát `CodDiscrepancyFoundEto`, SHP-002 phát `ShipmentDiscrepancyFoundEto` "khi ghi `Discrepancy` mới"; hai `Kind` COD đi qua đúng luật ghi của SHP-002 (:414) ⇒ REC-003/Notifications có thể nhận hai sự kiện khác kiểu cho một lệch (inbox không gộp được) | Ghi rõ: dùng `ShipmentDiscrepancyFoundEto` của SHP-002 (lọc `Kind`) hoặc chỉ một trong hai được phát cho `Kind` 3/6/7 |
| 11 | P3 | §5.2 :401–402; §6.2 `0313` :606 | Chỉ nói `23505` của partial UNIQUE ⇒ `0313`; `23505` của UNIQUE `(TenantId, IdempotencyKey)` chưa có mã. Đường tới: lần gọi đầu NÉM sau khi ghi dòng (vd `0311` quota) ⇒ bản ghi guard `ThatBai`; `IdempotencyGuard.cs:124-126` cho chạy lại **không** so payload; nếu dòng đã hấp thụ (`Failed`) thì pre-check bước 3 (chỉ tìm dòng chưa hấp thụ) không thấy ⇒ INSERT cùng khoá ⇒ `23505` — BE dễ ánh luôn về `0313` ("đang có yêu cầu chưa hoàn tất"), sai nghĩa | Ghi: `23505` trên `(TenantId, IdempotencyKey)` ⇒ trả lại bản ghi cũ (cùng payload) hoặc `Messaging:0001` (khác payload); pre-check bước 3 tìm theo `RequestKey` trên **mọi** trạng thái |
| 12 | P3 | §1 E7 :91; §7.2 :719; TC-15 :1008 | Ba chỗ nhỏ: E7 thiếu dấu backtick (`` `OtpAttemptCount++; vượt `OtpMaxAttempts` `` hiển thị lệch); §7.2 "`AttemptCount` tăng" — bảng không có cột này; TC-15 "lần thứ 4 trong ngày" không ghi `Cod.MaxAdjustmentsPerShipmentPerDay = 3` (mặc định trống ⇒ vào `PendingApproval`, không ra `0304`) | Sửa backtick; bỏ hoặc đổi tên cột ở §7.2; thêm giá trị Setting vào input TC-15 |

## Không kiểm được

- D4: không có PostgreSQL (`psql --version` → `command not found`, exit 127) ⇒ biểu thức CHECK/UNIQUE/partial UNIQUE ở §5.2 :400–413 **chưa chạy**. Tài liệu không có khối ` ```sql `.
- §7.3: nội dung trang GHN/GHTK bên ngoài — không tra web; chỉ kiểm nhất quán nội bộ (nhãn đã hạ 🔶).
- #6 (ABP ném lỗi phân quyền trước `0305`): dựa trên hành vi `[Authorize(permission)]` của ABP, chưa chạy thử trong repo.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — §6.2 :590–615 dải `0301`–`0315`, dùng lại `0207`/`0111`; §1, §12, §14, F5, mục đối chiếu :1249 đã theo | P1 |
| 2 | đã đóng — D6 :68, bước 2–3 :89, §6.2 :558, E15, TC-04/TC-57: `RequestKey` trong body (`D-120`) | P1 |
| 3 | đã đóng — BR-022, §8.1 #14/#15, partial UNIQUE :402, so-và-đặt bước 10, E16/E17, #17, TC-49/TC-50 (lỗ lối ra của #17 ghi thành phát hiện mới #2) | P1 |
| 4 | đã đóng — E12 :91, §6.2 :609–610, §12 :922–923, TC-58 | P2 |
| 5 | đã đóng — §5.2 :414 trỏ luật `RunId`/`LastSeenAtUtc` của SHP-002; TC-23 :1016 | P2 |
| 6 | đã đóng — §6.1 :534, §6.2 :548, §9 :834, §11 mục 5 "4 permission" | P2 |
| 7 | đã đóng — `RejectAdjustmentAsync` :532, route :545, A6, #3, TC-48 | P2 |
| 8 | đã đóng — #9 `OtpSent → OtpExpired` 0 lời gọi hãng; #10 chỉ `Requested` treo; NFR-011 ≤ 2 nhịp; TC-10/TC-32 | P2 |
| 9 | đã đóng — #1 `ApprovedBy IS NULL`; `ApproveAdjustmentAsync` chạy tiếp bước 5–7 (:451–452, :531); TC-12 | P2 |
| 10 | đã đóng — `TenantId` ✔ ở :341, :376 | P2 |
| 11 | đã đóng — index :400 bỏ `ProviderCode` | P2 |
| 12 | đã đóng — §7.2 :724–726, §8.1 #7 :781: guard ứng dụng, DB chỉ chặn thiếu `CollectedAtUtc` | P2 |
| 13 | đã đóng — `bool? RequiresOtpToChangeCod` :473, TC-46 (b) | P2 |
| 14 | đã đóng — §3.3 :217–220 tạo từ `ShipmentCreatedEto`, cập nhật `Amount` cùng UoW; §7.2 :720; TC-60 | P2 |
| 15 | đã đóng — §14.3 :1141 liệt kê TC cho 13/13 NFR; khớp cột "Phủ" §14.1 | P2 |
| 16 | đã đóng — `Reason` ⇒ `0312` riêng (E5, :605) | P3 |
| 17 | đã đóng — `0207` = `403` ở §6.2 :592 và §12 :926, có lý do (tenant từ token) | P3 |
| 18 | đã đóng — F3 :1203 ghi Q5 đã chốt `D-82` | P3 |
| 19 | đã đóng — F5 :1205 trỏ `eCommerceConsts.cs:496-498` | P3 |
| 20 | đã đóng — NFR-006 → TC-31 (:942), §8.1 #9 → TC-30 (:783) | P3 |
| 21 | đã đóng — §14.2: chú thích TC-18 đúng test, TC08 `async`/`await`, TC06 có ghi chú đổi sang `QuyDong()` | P3 |
| 22 | đã đóng — §4.1 dùng `ThucThiAsync` + `TraVeKetQuaCu`/`DangXuLy`/`XungDotPayload`/`DaThucThi` | P3 |
| 23 | đã đóng — bước 8 "giành lại khoá" :89 | P3 |
| 24 | đã đóng — mục đối chiếu có `D-116`·`D-118` (:1250), `D-119` (:1251); §6.1 :431 có câu danh sách đóng | P3 |
| 25 | đã đóng — §7.3 :744, :746 và §14.4 :1163 hạ 🔶 | P3 |
| 26 | đã đóng — `Cod.OtpTtlSeconds`/`Cod.OtpMaxAttempts` là Setting tenant (§6.3 :634, :668–669) | P3 |
