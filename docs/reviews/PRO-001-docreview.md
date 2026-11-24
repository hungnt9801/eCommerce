# Doc review — PRO-001 Voucher / Coupon: phát hành, giới hạn lượt & điều kiện áp dụng

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-001-voucher-coupon.md` (v0.6, 23/11/2026; 1175 dòng) |
| Ngày · vòng | 23/11/2026 · vòng 3 (kiểm nhanh: chỉ các mục đổi + đóng 9 P2 vòng 2) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, không đọc giải thích của BA) |
| Đã đọc | `git diff -U0 HEAD` của tệp (41 hunk) để khoanh mục đổi; rồi đọc nguyên văn dòng 1–20, 100–145, 199–681, 724–1175. Đối chiếu: `decisions.md` dòng 164 (`D-150`, toàn văn); `pro-002` dòng 160–189 (§3.2, §3.3), 351–370 (§5.2 `ProPromotions`), 440–554 (§6.1–§6.4), grep `0010`/`0109`/`CreatePromotionDto`/`IPromotionAppService` (`:511`, `:550`, `:761`, `:839`); code: `MetricsTests.cs` (grep tên test → `:70`, `:87`), `IdempotencyGuard.cs:27` |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (KET QUA: DAT; 16/16 heading, 36/36 BR có test, mục đối chiếu ở dòng 1155, 8 dòng `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 3 khối của tệp PASS (148, 208, 262), 0 FAIL · `kiem-link-tai-lieu.ps1` → PASS, 0 tham chiếu chết (176 tệp) |

Kết quả: **0 P1 · 1 P2 · 6 P3**. Cả 9 P2 vòng 2 đã đóng, `D-150` đã áp đúng vào thân tài liệu. 9/10 P3 vòng 2 cũng đã đóng, chỉ còn #12 (skeleton `TC35`). Lượt sửa này sinh ra một P2 mới: `TC-08` và `TC-59` dựa vào `MaxUsagePerCustomer = NULL`, nhưng bước 7 / `BR-04` chưa định nghĩa cách cấp `UsageSequence` khi giá trị này rỗng.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Với từng ràng buộc mới của v0.6, grep cả tệp tìm chỗ còn yêu cầu điều bị cấm. "Không có đường sửa" (`D-150`): grep `UpdateAsync\|UpdateVoucherDto\|Form sửa\|tạo/sửa` → chỉ còn `ExecuteUpdateAsync` (106) và hai câu phủ định (454, 901). "`0004` cho lá không đăng ký": grep `0010` → 462, 1171 nói `0010` giữ nghĩa `PRO-002`, 16/35/1085/1148 là dải hoặc lịch sử. "Không giảm theo dòng": grep `PerLineDiscount\|SKU bị loại` → chỉ còn ở ngữ cảnh "bỏ / nợ `CR-P1`". `BR-11` (140) so với §3.1 #3 (225, `OrderId`). Nhánh 0 dòng trả hàng: §3.1 #5 (227), §7.2 #4 (589), §12 (798), `TC-67` (925). `MaxUsagePerCustomer = NULL` ở `TC-08`/`TC-59` (869, 917) so với bước 7 (137), `BR-04` (140), §5.2 (385) | 2 phát hiện (#20, #25) |
| D2 chéo tài liệu / code / D-xx | `D-150` (1)–(3) đối chiếu từng chỗ BA phải sửa: `TC-59` (917), BR-26/§6.1/§12/`TC-64`/Q15 (140, 462, 780, 922, 1099), BR-18/E7/§3.2 #5/§10/§12/§6.2/`TC-40`/`TC-61` (139, 140, 248, 733, 792, 535, 901, 919). Mã `0004`: `pro-002:164` (guard duyệt), `:186` (`LoiCauHinh`), `:544`, `:839` khớp. `pro-002:510-517` có 8 method. `pro-002:367` cho `MaxUsagePerCustomer` nullable. Ai ném `0109` theo `pro-002:511` (`CreateAsync`) | 3 phát hiện (#21, #22, #23) |
| D3 cơ chế thực thi | `BR-18` mới: cái gì sẽ đỏ? `TC-40` phản chiếu allowlist method/DTO/setter, `TC-66` cho ngoại lệ. Danh sách thuộc tính bị khoá so với 5 cột CR-1 (§5.2, 357–361) | 1 phát hiện (#24) |
| D4 SQL chạy được | Không có khối ```` ```sql ````. Các câu `UPDATE` ở bước 5/10 và các `CHECK` CR-11 không đổi so với vòng 2: **chưa chạy**, vì máy không có `psql` lẫn `docker` (`which psql docker` → exit 2) | 0 phát hiện do chạy |
| D5 test/probe/tệp có thật | `Bang_dang_ky_KHONG_trung_ten_va_KHONG_rong` có ở `MetricsTests.cs:70` ✓, `MOI_hang_so_metric_PHAI_co_trong_All__chieu_NGUOC` có ở `:87` ✓, `IdempotencyGuard.cs:27` = `interface IIdempotencyGuard` ✓. Grep `IPromotionAppService` trong `docs/features/promotions` → chỉ thấy ở `pro-001:901` (#22). `CreatePromotionDto` có ở `pro-002:511` ✓ | 1 phát hiện (#22, đã tính ở D2) |
| D6 nhãn tin cậy | §8.1 #2 (638) nay ghi "🔶 chưa đo — `TC-59` là kế hoạch" ✓. §8.2 #9 (662) ghi "⚠️ Chưa đo" ✓. Không có ✅ mới nào trong các hunk đổi | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | #1: 36/36 BR có TC (máy); `TC-66`/`TC-67` mới có cột "Phủ". #3: §3.1 #5 đã có cột "Nếu guard sai" phân loại 0 dòng. #10: §7.3 vẫn có lý do. #11–#14: máy PASS. §14.3: đếm ID ở §14.1 được 55 (`TC-01`…`TC-55`) + 9 (`TC-59`…`TC-67`) = 64, khớp dòng 1029 và lịch sử 1153. Mục đối chiếu: có dòng `D-150` (1171) và đã sửa thân theo đó; `D-146`…`D-149`, `D-69` giữ nguyên | 1 phát hiện (#23, đã tính ở D2) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 20 | P2 | Bước 7 dòng 137; BR-04 dòng 140; §5.2 dòng 385; `TC-08` dòng 869; `TC-59` dòng 917; skeleton `TC08` dòng 988 | `TC-08`/`TC-59` (sửa ở v0.6) đặt `MaxUsagePerCustomer = NULL` "để trần theo khách không quyết kết quả". Nhưng bước 7 chỉ định nghĩa "số nhỏ nhất trong `[1, MaxUsagePerCustomer]` …; hết số ⇒ `0104`", và không câu nào nói gì khi giá trị này `NULL` (`pro-002:367` cho phép null). Có hai đường dẫn tới lỗi. (a) BE làm đúng chữ: `[1, NULL]` là khoảng rỗng ⇒ mọi request nhận `0104` ⇒ `TC-59` (chặn release) đỏ, và mọi promotion không có trần theo khách thành không dùng được. (b) BE theo tiền lệ dòng 385 ("dòng vãng lai = `1`"), gán `UsageSequence = 1` khi `NULL` ⇒ unique §5.2 biến promotion "không giới hạn theo khách" thành **1 lượt/khách** im lặng (lần hai ⇒ `23505` ⇒ `0104`) | Ghi rõ ở bước 7, `BR-04` và §5.2 dòng 385: "`MaxUsagePerCustomer = NULL` ⇒ không có trần theo khách". Chọn một: (i) vẫn cấp số nhỏ nhất chưa bị chiếm trong `[1, ∞)`, hoặc (ii) không cấp, đặt `UsageSequence` theo một quy ước không đụng unique. Thêm một TC: "`MaxUsagePerCustomer = NULL`, cùng khách giữ 3 lượt ⇒ cả 3 thành công, 0 `0104`" |
| 21 | P3 | `TC-20` dòng 881; §12 dòng 790; §6.2 dòng 478; CR-1 dòng 404 | v0.6 chuyển chỗ ném `0109` sang "ngay khi tạo (`CreateAsync`)". `CreateAsync` là method của `PRO-002` (`pro-002:511`), nhưng CR-1 chỉ thêm cột + `CHECK` và không yêu cầu `PRO-002` kiểm `BR-19` rồi ném `0109` (mã của `PRO-001`). §6.2 vẫn gắn `0109` cho route `voucher/generate`. Hệ quả: `CHECK` CR-1 vẫn chặn dữ liệu sai, nhưng client nhận `23514`/`500` thay vì `400` + `0109`, và `TC-20` không có chủ để viết | Thêm vào CR-1 một câu: "`CreateAsync` của `PRO-002` kiểm `BR-19` ⇒ NÉM `eCommerce:Promotion:0109` trước khi ghi". Đổi §6.2 thành "`0109` khi sinh mã cho promotion cấu hình sai" (nếu `GenerateAsync` cũng kiểm), hoặc bỏ `0109` khỏi route `generate` |
| 22 | P3 | `TC-40` dòng 901 | Test phản chiếu gọi `IPromotionAppService` (`PRO-002`), nhưng `pro-002` §6.2 (`:506-517`) không đặt tên interface. Grep `IPromotionAppService` trong `docs/features/promotions` chỉ thấy ở `pro-001:901` | Viết "app service quản trị cây của `PRO-002` §6.2 (tên do `PRO-002` đặt)", hoặc gửi kèm CR để `PRO-002` đặt đúng tên này |
| 23 | P3 | Mục đối chiếu dòng 1171 (cột "Code thật") | Dòng này ghi "`pro-002:510-517` chỉ có `CreateAsync`/`SaveTreeAsync`/`SetStatusAsync`". Thực tế khối đó có 8 method, trong đó còn `ApproveAsync` (ghi trạng thái) và `ImportRuleCostsAsync` (ghi). Allowlist của `TC-40` lấy "theo `pro-002:510-517`" nên không sai hành vi, nhưng người duyệt đọc dòng này sẽ hiểu sai bề mặt ghi | Sửa thành "`pro-002:510-517`: không có method nào sửa cột CR-1/`MaxUsageTotal` sau `CreateAsync`" |
| 24 | P3 | BR-18 dòng 140; `TC-40` dòng 901; §3.2 #5 dòng 248 | `BR-18` gọi `MaxUsagePerCustomer` là "cột CR-1". Thực tế đây là cột có sẵn ở `pro-002:367`. Ngược lại, `Currency` mới đúng là cột CR-1 (§5.2 dòng 361), nhưng không nằm trong danh sách bị khoá của `BR-18`/`TC-40`/§3.2 #5. Đổi `Currency` của promotion đã có lượt sẽ làm `DiscountValue`/`MinOrderAmount` đổi nghĩa mà `TC-40` không đỏ | Viết lại: "5 cột CR-1 (`DiscountKind`, `DiscountValue`, `MaxDiscountAmount`, `MinOrderAmount`, `Currency`) + `MaxUsagePerCustomer`, `MaxUsageTotal` (có sẵn ở `PRO-002`)". Thêm `Currency` vào danh sách setter của `TC-40` |
| 25 | P3 | `TC-67` (c) dòng 925; §3.1 #5 dòng 227; §7.2 #4 dòng 589 | Nhánh "lượt còn `Reserved` ⇒ `0105`" không đạt được: consumer đọc lượt theo `OrderId`, mà lượt `Reserved` luôn có `OrderId = NULL` (`BR-11`: `OrderId` chỉ gán lúc chốt). Lượt `Released` do TTL cũng vậy. QA chỉ dựng được input (c) bằng cách chèn dòng trái bất biến | Thu hẹp (c) thành "lượt `Released` với `ORDER_CANCELLED` (đơn đã huỷ mà nhận trả hàng) ⇒ `0105`". Ghi rõ ở §3.1 #5 rằng `Reserved` không có `OrderId` nên rơi vào nhánh "không có lượt" |
| 12 | P3 | §14.2 skeleton `TC35` dòng 1004–1006 | (còn từ vòng 2) Skeleton vẫn chỉ ghép 3 rule `MinOrderAmount`/`ChannelScope`/`FirstOrderOnly`, thiếu `CustomerGroup` (`BR-26`: đúng 4) ⇒ test xanh dù `CustomerGroup` chưa khai `DeclaredCostFactor` | Thêm `.Concat(PromotionRuleRegistry.TenCua<CustomerGroupRule>())`, hoặc duyệt toàn bộ rule mà `PRO-001` đăng ký |

## Không kiểm được

- D4: chưa chạy câu `UPDATE` ở bước 5/10, hai `CHECK` của CR-11, index sửa `pro-002:431` (`AND "OrderId" IS NOT NULL`) và index trần theo khách `NULLS NOT DISTINCT`. Lý do: máy không có `psql` lẫn `docker` (`which psql docker` → exit 2). Kết luận "`TC-59` không có `40P01`" (không có chu trình chờ khi mọi đường khoá mã trước, promotion sau) và §8.2 #9 (thứ tự khoá theo lô) dựa trên ngữ nghĩa khoá dòng, **chưa đo**.
- Vòng kiểm nhanh: không đọc lại dòng 21–99 (§0.0), 146–198 (§2), 682–723 (§9). Các hunk của `git diff` không chạm những đoạn này.

## Vòng trước (vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 1 có 26 phát hiện, cả 26 đã đóng ở vòng 2.

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — skeleton `TC07` có `CustomerId = Guid.NewGuid()` (957); `TC-07`/`TC-08` nêu `CustomerId` (868–869) | P2 |
| 2 | đã đóng — `TC-59` (917) khớp nguyên văn `D-150` (1): "còn lại `0007` hoặc `0119`", mỗi request một `CustomerId`, cấm đảo khoá. Sinh ra #20 | P2 |
| 3 | đã đóng — §8.1 #2 (638): "🔶 chưa đo — `TC-59` là kế hoạch" | P2 |
| 4 | đã đóng — sơ đồ §4.1 dòng 276, 281, 282: `DiscountTotal` cấp đơn, `giamGiaCapDon`; mermaid PASS | P2 |
| 5 | đã đóng — BR-18 (140) có ngoại lệ `OverrideLimitAsync` + điều kiện; §6.1 (454); `TC-66` (924) | P2 |
| 6 | đã đóng — chọn (b) theo `D-150` (3): BR-18, E7 (139), §3.2 #5 (248), §10 (733), §12 (792), §6.2 `0106` (535), `TC-40` phản chiếu (901), `TC-61` (919). Sinh ra #22, #23, #24 (P3) | P2 |
| 7 | đã đóng — §8.2 #9 (662), §12 chiến lược transaction (811): mã trước theo `Id`, promotion sau; `40P01` ⇒ thử lại | P3 |
| 8 | đã đóng — §3.1 #5 (227), §7.2 #4 (589), §12 (798), `TC-67` (925). Sinh ra #25 (P3) | P2 |
| 9 | đã đóng — BR-11 (140) có `OrderId` (một lần, `NULL` → giá trị); `TC-52` so cột | P2 |
| 10 | đã đóng — `0004` ở BR-26, §6.1 (462), §6.2 (525), §12 (780), `TC-64` (922), Q15 (1099); khớp `pro-002:164,186,544,839` | P2 |
| 11 | đã đóng — §12 (797) thu hẹp "Event đến sai thứ tự" | P3 |
| 12 | còn — chép lên bảng "Phát hiện" | P3 |
| 13 | đã đóng — grep `SKU bị loại` → 0; §5.1 (335) ghi "không dùng ở v1" | P3 |
| 14 | đã đóng — dòng 135 bỏ cụm "điều kiện DoR chặn"; F9 (1113) ghi 11 CR | P3 |
| 15 | đã đóng — §7.4 tầng 2 (617), §8.1 #5 (641): chốt/nhả/đảo qua `IIdempotencyGuard` | P3 |
| 16 | đã đóng — CR-11 (414) sửa nghĩa `0007` ở `pro-002` §6.4 | P3 |
| 17 | đã đóng — bước 1 (137), A3 (138), BR-24: `0118` kiểm trước engine; `TC-55` (b) | P3 |
| 18 | đã đóng — bước 1 có `customerId` | P3 |
| 19 | đã đóng — trích theo tên test (67, 118, 140, 1107); tên có thật ở `MetricsTests.cs:70,87` | P3 |
