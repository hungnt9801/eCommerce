# Doc review — SHP-002 Webhook trạng thái vận đơn & đối soát

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md` (v0.3, 20/11/2026; 1188 dòng) |
| Ngày · vòng | 20/11/2026 · vòng 2 (vòng 1 cùng ngày, trên v0.2: `DOC-CHANGES`, 1 P1 · 11 P2 · 17 P3) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, không đọc giải thích của BA) |
| Đã đọc | dòng 1–1188 (toàn văn v0.3). Đối chiếu: `decisions.md` dòng 132–133 (`D-118`, `D-119`); `IdempotencyGuard.cs:104-168` (nhánh `ThatBai` ⇒ cho thử lại, dòng 124–126); `design-doc-guidelines.md` dòng 152, 154; `implementation-plan.md` (grep `ShippingHub\|GĐ2`) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi: 16/16 heading, 14/14 BR có test, mục đối chiếu ở dòng 1173, 6 dòng `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 4 khối của tệp PASS (104, 158, 246, 289) · `kiem-link-tai-lieu.ps1` → PASS, 0 tham chiếu chết |

Kết quả vòng 2: **0 P1, 2 P2, 6 P3**. Vòng 1 có 29 phát hiện: **29/29 đã đóng**. 8 phát hiện mới ở bảng dưới đều nằm trong phần mới thêm (#16, trigger, CHECK đủ hai vế, bắt `23505`, Redis).

Các điểm ORCH yêu cầu kiểm:
- **`D-119`:** tôi dựng lại tập đến-được từ 13 cạnh ở §3.1 (dòng 159–172) cho từng `S`. Kết quả **khớp đúng 7 hàng** của bảng ở dòng 206–214.
  - `Cancelled` không phải đích nhảy được (#16 có `T ≠ Cancelled`; TC-36).
  - `Manual` bị loại khỏi #16.
  - Thứ tự kiểm (dòng 216) đúng như `D-119` (4).
  - `D-119` (5) đều có: 1 ETO với `FromStatus` thật, log `Warning`, metric `shipping_status_skipped_total`, 0 cột mới.
  - Nợ `SAL-003` (dòng 698) đã có chủ.
  - Các TC `D-119` ⑥ yêu cầu (TC-30, TC-33…TC-36, ca bảng mọi cặp `(S,T)`) đều có.
- **`Duplicate = 2` giữ chỗ:** đúng `D-17` vì không được rút giá trị khỏi enum. Câu chữ nhất quán ở A1, §3.3 dòng 237, §5.2 dòng 393, §10 dòng 854.
- **`MaxEventProcessAttempts = 5`:** có nhãn 🔶, thuộc khối Setting chưa đo (dòng 669, `D-114` P8). Dùng nhất quán ở E7, §3.3, §6.3, TC-12. Ở các nhánh `DangXuLy`/Redis chết, `AttemptCount` không tăng nên không bị `Rejected` oan.
- **Trigger append-only:** có ở §5.2 dòng 406 + bảng index dòng 454, §11 dòng 874/881, §8.1 #2, TC-06. Còn lệch danh sách cột (#7).
- **`LastSeenAtUtc`:** cột `NOT NULL`, bằng `DetectedAtUtc` khi tạo (dòng 435). Đi cùng partial unique `WHERE "ResolvedAtUtc" IS NULL` (dòng 461), BR-013, §7.1 dòng 694 (không phát lại ETO) và TC-38. Cách bắt `23505` còn vấn đề (#2).
- Các lựa chọn trên do BA tự chọn và không có dòng `D-xx`. Tất cả đều là lựa chọn kỹ thuật đảo ngược được, nằm trong hướng `D-119`/`D-114` P8, nên tôi không ghi thành phát hiện. ORCH quyết có cần DEC ghi lại hay không.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep toàn tệp theo từng ràng buộc mới: `23505` (A1, E8, §3.2 #15, §4.1, §8.2 #1, §12 B1, bảng index), `StatusSource = 1` (CHECK dòng 452) so với §11 backfill `Pull`, `Duplicate` (6 chỗ), `Received` + `AttemptCount` (E7/§3.3/§8.2/§12/TC-12), "đến được" so với D10 "ngược chiều NÉM" trên đồ thị có chu trình `OutForDelivery → DeliveryFailed → Redelivery → OutForDelivery` | 2 phát hiện (#1, #4) |
| D2 chéo tài liệu / code / D-xx | `D-119` (1)–(6), ⑥ so với §0.1 D10, §3.1–§3.3, §7.1, §12, §14 · `IdempotencyGuard.cs:104-168` (E7 thử lại được vì `ThatBai` ⇒ `ThuLai`) · `eCommerceMetrics.cs:207,210` (nhãn `from`/`to` có thật) · `IdempotencyModels.cs:17-18` (scope ≤ 64: `shipping:reconcile:` + 32 = 51) · `docs/implementation-plan.md` dòng 115 so với mốc `:113` ở header | 2 phát hiện (#5, #8) |
| D3 cơ chế thực thi | BR-005 so với danh sách cột của trigger · BR-013 so với partial unique + cách xử lý `23505` · TC-40 so với khoá Redis của chính job nhặt lại (§8.2 #4) | 3 phát hiện (#3, #6, #7) |
| D4 SQL chạy được | Không có khối ```` ```sql ````. Trigger, CHECK mới `("Status" NOT IN (7, 8)) OR ("ConfirmedAtUtc" IS NOT NULL AND "StatusSource" = 1)`, partial unique `ShpDiscrepancies` và hành vi `23505` trong giao dịch **chưa chạy**: máy không có `docker`/`psql` (đã kiểm ở vòng 1) | 0 phát hiện; #2/#3 dựa trên hành vi PostgreSQL đã tài liệu hoá, **chưa đo** |
| D5 test/probe/tệp có thật | Mốc dòng mới: `business-modules-proposal.md:549` ✓, `implementation-plan.md:202,203,216,295,324` ✓, `design-doc-guidelines.md:138,152,154` ✓ (quy tắc 6 ở 152, quy tắc 8 ở 154), `feature-design-template.md:216,289,364` ✓, `IdempotencyModels.cs:32` ✓, `IdempotencyGuard.cs:94-100` ✓. Còn `implementation-plan.md:113` ở header (thực tế 115) | 1 phát hiện (#8) |
| D6 nhãn tin cậy | ✅ mới: Q9 "đã chốt" (khớp `shp-001:1188`); "✅ Nhãn dùng lại đúng tập cho phép" có `from`/`to` (đúng code); 🔶 `MaxEventProcessAttempts` | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | #3 guard sai làm gì: #16 có cột "Nếu vi phạm" · #5 index idempotency: có (B1, partial unique) · #6 tên khoá: không đổi · #7 tiền: `ToMinorUnits` chỉ còn ở dòng 1076 (câu phủ định) và 1188 (lịch sử) · #10: §5.1 có lý do đúng (4 sơ đồ) · #11–#14: máy PASS. Mục đối chiếu: thêm `D-116`, `D-119`, dòng "docreview vòng 1". Thân đã sửa theo từng dòng (đối chiếu ở cột D2) | 0 phát hiện |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §11 dòng 879 so với CHECK §5.2 dòng 452, BR-004 dòng 96 | §11 bảo: nếu `SHP-001` chạy trước thì *"backfill `Status` từ trạng thái đơn tương ứng… `StatusSource = Pull`"*. Nhưng CHECK mới bắt `Status ∈ {7, 8}` phải có `ConfirmedAtUtc` **và** `StatusSource = 1`. Đường tới lỗi: `SHP-001` lên trước (§11 dòng 874: *"`SHP-001` đi trước"*), có đơn đã `Delivered`/`Returned` ⇒ backfill ghi `Status = 7`, `StatusSource = 3` ⇒ CHECK từ chối ⇒ migration/backfill dừng. Ngay cả khi không có CHECK, backfill như vậy cũng trái BR-004: `Delivered` mà không qua đối soát | Chọn một: (a) backfill **không** đặt `Delivered`/`Returned`; giữ trạng thái chưa hấp thụ + `PendingConfirmStatus`, để job đối soát xác nhận; hoặc (b) bỏ hẳn backfill (ghi lý do: `SHP-001` không tạo được trạng thái giao). Sửa dòng 879 và thêm một TC |
| 2 | P2 | §1 bước 4 dòng 93, A1/E8 dòng 94–95, §8.2 #1 dòng 782, §12 B1 dòng 917 | B1 viết *"`INSERT` gặp `23505`: đọc `PayloadHash` của dòng sẵn có"* nhưng không nói đọc ở giao dịch nào. Trong PostgreSQL, sau `23505` giao dịch hiện tại bị huỷ: mọi lệnh tiếp theo lỗi `25P02` tới khi rollback. ABP mặc định mở UoW **có transaction** cho request `POST`. Đường tới lỗi: mọi webhook trùng (at-least-once, nên rất thường gặp) ⇒ bước đọc hash lỗi ⇒ `500` ⇒ hãng retry, hoặc tắt webhook — đúng hậu quả D4 muốn tránh. TC-02 trên Testcontainers sẽ bắt được, nhưng máy dev chưa chạy được integration (§14.2). **Chưa đo** (không có PostgreSQL) | Ghi rõ cơ chế ở §12 B1: `INSERT … ON CONFLICT ("TenantId","ProviderCode","ProviderEventId") DO NOTHING RETURNING "Id"` (0 dòng ⇒ đọc hash bằng câu riêng), **hoặc** INSERT trong UoW `requiresNew` rồi đọc hash ở UoW khác. Áp cùng câu cho §8.2 #1 |
| 3 | P3 | §5.2 bảng index dòng 461; BR-013 dòng 96 | *"`23505` ⇒ coi là 'đã có' và chỉ cập nhật `LastSeenAtUtc`"* gặp cùng vấn đề giao dịch huỷ như #2. Ở đây nó nằm trong UoW B3 cùng với `LastReconciledAtUtc`/`PendingConfirmStatus` ⇒ cả UoW rollback, vận đơn bị chọn lại mỗi kỳ. TC-38 (integration) sẽ đỏ nên không im lặng. **Chưa đo** | Ghi `INSERT … ON CONFLICT … WHERE "ResolvedAtUtc" IS NULL DO UPDATE SET "LastSeenAtUtc" = …` (hoặc đọc trước, chèn sau, và chỉ dùng `23505` cho ca đua) |
| 4 | P3 | D10 dòng 77; §3.1 ghi chú dòng 181; §3.2 #16 | "Tiến = đến được" không phân biệt được tiến/lùi bên trong chu trình `OutForDelivery → DeliveryFailed → Redelivery → OutForDelivery`. Ba trạng thái này đến được lẫn nhau, nên một sự kiện cũ đến muộn (vd `OutForDelivery` sau `DeliveryFailed`) được coi là "tiến" và được áp. Chỉ #14 (thời gian) chặn được, mà Q3 nói hãng có thể không gửi thời gian. D10 vẫn khẳng định *"ngược chiều phải NÉM"* | Ghi một câu ở D10/§3.2: trong chu trình này, thứ tự chỉ dựa vào #14. Thiếu `ProviderOccurredAtUtc` thì chấp nhận lật trạng thái, có metric (đã có `shipping_status_skipped_total`), và đối soát là chốt cuối |
| 5 | P3 | §7.1 dòng 698 so với dòng 692–696 và `D-119` (6) | Nợ `SAL-003` ghi *"Consumer phải đi qua các cạnh Sales có sẵn… (vd `AwaitingPickup → OutForDelivery → Delivered`)"* mà không nói **consumer nào**. Theo luật "một event duy nhất" ngay phía trên, `Delivered`/`ReturnConfirmed` chỉ đi qua `ShipmentDeliveredEto`/`ShipmentReturnedEto`, nên nghĩa vụ đi chuỗi cạnh thuộc **cả** consumer của hai ETO này. `D-119` (6) chỉ nêu consumer `ShipmentStatusChangedEto` | Ghi rõ: consumer `ShipmentStatusChangedEto` (cho `Returning`/`DeliveryFailed`…) **và** consumer `ShipmentDeliveredEto`/`ShipmentReturnedEto` đều đi chuỗi cạnh có sẵn trong một UoW |
| 6 | P3 | §8.2 #7 dòng 788; TC-40 dòng 1019 so với §8.2 #4 dòng 785 | Khi Redis chết, TC-40 mong *"job nhặt lại log `Warning` số dòng tồn"*. Nhưng chính job đó phải giành khoá `shipping:event-retry:{tenantId}` qua `IAbpDistributedLock` (Redis) + `SkipIfBusy`. Redis chết ⇒ không giành được khoá ⇒ bỏ nhịp (hoặc ném lỗi) trước khi đếm ⇒ cảnh báo thiết kế cho đúng ca này không phát. **Chưa đo** `TryAcquireAsync` khi Redis mất kết nối | Ghi: đếm + log `Warning` chạy **trước** bước giành khoá (đọc DB, không cần Redis), hoặc khi giành khoá lỗi kết nối thì log `Warning` kèm số dòng `Received` tồn. Sửa TC-40 cho khớp |
| 7 | P3 | BR-005 dòng 96; §5.2 dòng 403–406 | Có ba danh sách cột bất biến khác nhau: BR-005 = *"mọi cột trừ 5 cột xử lý"*; dòng 403 = 4 cột; trigger dòng 406 = 7 cột. Các cột bằng chứng `ProviderTrackingCode`, `ProviderStatusCode`, `ProviderOccurredAtUtc`, `SourceIp`, `CorrelationId` **không** được trigger bảo vệ, dù BR-005 cấm đổi chúng | Trigger kiểm theo **danh sách cho phép** (chỉ 5 cột xử lý + cột kỹ thuật như `xmin`/`ConcurrencyStamp` nếu có), không theo danh sách cấm. Sửa dòng 403 cho cùng một danh sách |
| 8 | P3 | Header dòng 13 | `docs/implementation-plan.md:113` (GĐ2 của SHP) lệch: bảng GĐ2 có `SHP-001…004` ở dòng 115 | Sửa mốc dòng |

## Không kiểm được

- D4: không chạy được trigger `BEFORE UPDATE`/`BEFORE DELETE`, CHECK BR-004 mới, partial unique `ShpDiscrepancies`, và hành vi `23505` → `25P02` trong giao dịch (#2, #3). Máy không có `docker`/`psql`.
- #6: chưa đo `RedisDistributedSynchronizationProvider.TryAcquireAsync` khi Redis mất kết nối (trả `null` hay ném lỗi). Cả hai khả năng đều dẫn tới không đếm.
- Như vòng 1: chưa mở `XC-002`/`XC-005` để xác nhận số hiệu BR/TC được dẫn; sự thật NCC (Q1/Q2) chưa xác minh.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 1 (v0.2, cùng ngày): `DOC-CHANGES`, 1 P1 · 11 P2 · 17 P3. Kiểm từng mục trên v0.3:

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đóng (v0.3) |
|---|---|---|---|
| 1 | đã đóng | P1 | §3.2 #16 + bảng đến-được (dòng 202–214, đã dựng lại từ §3.1: khớp) · §12 dòng 905 · TC-30 (`PickedUp`), TC-33…TC-36 · `D-119` |
| 2 | đã đóng | P2 | Hậu điều kiện dòng 92 ("đã xác thực **và không trùng**"), A1 dòng 94, §3.3 bỏ cạnh `Received → Duplicate`, `Duplicate = 2` giữ chỗ |
| 3 | đã đóng | P2 | B1 so `PayloadHash` khi `23505` (dòng 93, E8 dòng 95, §8.2 #1, TC-03). Cách đọc trong giao dịch: xem #2 mới |
| 4 | đã đóng | P2 | E7 giữ `Received` + `AttemptCount++` (dòng 95), §3.3 dòng 236, §8.2 #2/#5, §12 dòng 908, TC-12. `IdempotencyGuard.cs:124-126` cho thử lại sau `ThatBai` |
| 5 | đã đóng | P2 | §7.1 dòng 691–696, 706, 708: một event duy nhất · TC-37 |
| 6 | đã đóng | P2 | Dòng 432, 903: `Money.Equals`. Grep `ToMinorUnits`: chỉ còn 96 (BR-007 "chưa có"), 1076 (chú thích phủ định), 1188 (lịch sử) |
| 7 | đã đóng | P2 | `ShipmentDiscrepancyFoundEto` ở bảng ETO dòng 694 |
| 8 | đã đóng | P2 | Partial unique dòng 461 + `LastSeenAtUtc` dòng 435 + BR-013 + TC-38. Cách bắt `23505`: xem #3 mới |
| 9 | đã đóng | P2 | Dòng 358, 639, 741, 906 + TC-39: "thất bại liên tiếp", reset khi thành công |
| 10 | đã đóng | P2 | Trigger dòng 406, 454, §11 dòng 874/881, TC-06. Danh sách cột: xem #7 mới |
| 11 | đã đóng | P2 | Dòng 370, Q9 dòng 1137, DoR dòng 1117: `Đã chốt — D-114`, khớp `shp-001:1188` |
| 12 | đã đóng | P2 | §8.2 #7 dòng 788, §12 dòng 910: "nhận và ghi vết, B2 dừng" + TC-40. Cảnh báo: xem #6 mới |
| 13 | đã đóng | P3 | §6.2 dòng 629, §12 dòng 909: "nội bộ, đã ACK `200`" |
| 14 | đã đóng | P3 | Mốc dòng đã sửa (cột D5). Còn `:113` ở header (#8 mới, phát hiện ở vòng này) |
| 15 | đã đóng | P3 | Dòng 5, 735, 1145: `:295` 🟠 vừa |
| 16 | đã đóng | P3 | §5.1 dòng 326: 4 sơ đồ |
| 17 | đã đóng | P3 | Dòng 415 khớp dòng 459 |
| 18 | đã đóng | P3 | NFR-009 dòng 97 = §13 dòng 935 (< 10 dòng/nhịp) |
| 19 | đã đóng | P3 | NFR-011/012 ở §1 dòng 97 |
| 20 | đã đóng | P3 | `TC09_…_nem_0205`, `TC08_so_tien_cod_qua_money_equals` |
| 21 | đã đóng | P3 | §6.2 dòng 625: `403`/`404` |
| 22 | đã đóng | P3 | TC-04 dòng 983, TC-09 dòng 988 |
| 23 | đã đóng | P3 | BR-002 dòng 96: lý do bỏ guard cho sửa tay |
| 24 | đã đóng | P3 | CHECK dòng 452 có `StatusSource = 1`; TC-29 hai ca. Hệ quả với backfill: xem #1 mới |
| 25 | đã đóng | P3 | D2 dòng 69 |
| 26 | đã đóng | P3 | Mục đối chiếu dòng 1185 (`D-116`) |
| 27 | đã đóng | P3 | §6.2 dòng 626: `0208` chỉ cho `OverrideStatusAsync` |
| 28 | đã đóng | P3 | §6.1 dòng 515: 6 nhãn |
| 29 | đã đóng | P3 | Dòng 70, 93, 723, 753–754: `IdempotencyKey.Cua(scope, value)` |
