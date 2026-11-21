# Doc review — SAL-006 Trả hàng / RMA và hoàn tiền (một phần / toàn bộ)

**VERDICT: DOC-CHANGES**

| | |
|---|---|
| Tài liệu | `docs/features/sales/sal-006-tra-hang-rma-hoan-tien.md` (2725 dòng, v1.6) |
| Ngày · vòng | 21/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–2725 (toàn văn, kể cả §16–§27 vòng 1 bỏ qua) + `decisions.md:146-152` (`D-132`…`D-138`) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi; 16/16 heading, 23/23 BR có test, mục đối chiếu ở dòng 2698; INFO 14 câu `Open`, 0 TODO) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep `phiếu mới`/`phiếu khác`/`nhiều phiếu`/`qua nhiều phiếu`/`hai phiếu` (31 chỗ), `RefundedAmount`, `0004`, `TH-{ReturnNumber}`, `NhapHangKhachTraAsync` trên toàn tệp; so ADR-11/BR-005/BR-012 với §3.3, §5.1, §5.2, §6.2, §8.3, §11, TC-40/41/43 | 4 phát hiện (#3, #4, #5, #10) |
| D2 chéo tài liệu / code / D-xx | đọc `CustomerReturnGateway.cs` 1–83; `OrderReturnService.cs` 60–209; `OrderStateMachine.cs` 175–247 (7 cạnh `Closure`); `Order.cs` 236–257, 600–644, 825–849; `OrderReturn.cs` 9–36, 173–300, 382, 420; `OrderReturnAppService.cs` (grep `Authorize`/`TH-`); `MoneyAllocator.cs` 15–94; `InventoryDbContextModelCreatingExtensions.cs` 94/110/160–168/240; `eCommerceConsts.cs` (grep :99/:100/:108/:802/:827); `eCommerceHttpApiHostModule.cs` (grep `KeyPrefix`); `DonHangAppService.cs`/`MarketplaceOrderFactory.cs` (grep); so với `D-132` (3)(5), `D-135` (3), `D-136` (1), `D-137` (4), `D-138` | 5 phát hiện (#1, #2, #8, #9, #11) |
| D3 cơ chế thực thi | BR-004/005/009/011/012/016/023 → cái gì đỏ; riêng BR-012 + `D-137` (4) dò đường gọi cổng hai lần cùng số chứng từ qua nhánh idempotent `CustomerReturnGateway.cs:45-54` | 1 phát hiện (#1) |
| D4 SQL chạy được | không có khối `sql`/`powershell`/`bash` (grep fence: 3 `mermaid`, 6 `csharp`). CHECK nội tuyến §5.1 (`"Method" <> 1 OR "EvidenceReference" IS NOT NULL`) **chưa chạy**: `which psql` → không có; `docker` → command not found (exit 127) | 0 phát hiện |
| D5 test/probe/tệp có thật | grep `test/`: `OrderStateMachineTests`, `Wp04TonKhoChieuTests`, `OrderReturnTests` có; `DbSet` RMA `eCommerceDbContext.cs:220-225` có; provider `:89-90`, `:96` có; registry 16 tên (`eCommerceMetrics.cs:16-78`), tag `:192-210`, `Allowed :212`, `Forbidden :219` khớp; `ReturnRefundMethod.Manual = 1` (`OrderReturn.cs:34`) khớp CHECK; `KeyPrefix` dẫn sai dòng | 1 phát hiện (#11) |
| D6 nhãn tin cậy | ✅ ở §0 (dòng 31–54) và §14.4 so code đã đọc ở D2/D5 | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | #1–#14; `npm --prefix tools run check:mermaid` → 211 khối, 0 lỗi (exit 0); `pwsh -File tools/kiem-link-tai-lieu.ps1` → 770 link + 969 backtick, 0 chết (exit 0); thẻ HTML; mục đối chiếu (dòng 2698–2721) so thân + `D-132`…`D-138` | 3 phát hiện (#6, #12, #13) + #1 (hàng `D-137` của mục đối chiếu) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P1 | §1 Basic Flow bước 9 (dòng 114); BR-012 (dòng 117); §7.4 hàng `Sales → Inventory` (dòng 644); F6 (dòng 1044); TC-34 (vi) (dòng 921); TC-41 (v) (dòng 928); mục đối chiếu hàng `D-137` (dòng 2717) | **Lỗi mới do chỗ sửa.** Một đợt nhận có cả dòng `Restock` lẫn dòng hàng lỗi bị tài liệu bắt đi **hai lời gọi cổng cùng số chứng từ** `TH-{ReturnNumber}-{rev}`: dòng `Restock` qua `NhapHangKhachTraAsync` "không đổi hợp đồng", dòng hàng lỗi qua method additive "nhận cùng số chứng từ, nằm trong cùng chứng từ, một lần `GhiSoAsync`". Điều này không làm được. `NhapHangKhachTraAsync` tự tạo chứng từ và tự ghi sổ (`CustomerReturnGateway.cs:62-75`), nên lời gọi thứ hai cùng số rơi vào nhánh idempotent "chứng từ đã có ⇒ trả id cũ, không nhập kho" (`:45-54`), đúng cái BR-012 đòi ("idempotent theo số chứng từ ở cổng"). Đường hỏng: BE làm đúng chữ §7.4, gọi `Restock` trước rồi hàng lỗi sau. Lời gọi hàng lỗi bị nuốt mà không báo gì, nên `OnHand`/`Damaged` thiếu đúng phần hàng lỗi. Gọi ngược thứ tự thì phần `Restock` mất. Trong cả hai trường hợp phiếu vẫn `Received`, đơn vẫn `Returned`. Chính `D-132` (3) đã cấm cách này: "không dùng chung số … trùng số ⇒ lời gọi sau bị nuốt im lặng", nên `D-137` (4) mâu thuẫn với `D-132` (3). Thêm một chỗ nữa: unique `(TenantId, StockDocumentId, Sku)` (`InventoryDbContextModelCreatingExtensions.cs:166-168`) không cho hai dòng cùng SKU trong một chứng từ, nên đợt trộn có hai dòng đơn cùng SKU khác cách xử lý sẽ đỏ ở DB | ORCH gửi **DEC** chốt một trong hai cách (vì hai `D-xx` đang mâu thuẫn, BA không tự chọn được). (a) Đợt có dòng hàng lỗi ⇒ **một** lời gọi method additive nhận **mọi** dòng của đợt (`Restock` + hàng lỗi), chạy một `GhiSoAsync`; `NhapHangKhachTraAsync` chỉ dùng cho đợt toàn `Restock`. (b) Hai chứng từ mỗi đợt, có hậu tố riêng cho nhánh hàng lỗi theo `D-132` (3), và bỏ câu "một đợt = một chứng từ". Sau đó BA sửa Basic Flow 9, BR-012, §7.4, F6, TC-34 (vi), TC-41 (v), hàng `D-137` của mục đối chiếu; sửa cả `D-132` (5) ("hàm guard chỉ nhận chứng từ của nhánh hàng lỗi" không còn phân biệt được nếu chọn (a)); thêm ca TC "đợt trộn `Restock` + `Damaged`, hai dòng đơn cùng SKU" |
| 2 | P2 | §3 câu mở đầu (dòng 186); §1 E1, E6 (dòng 116); §6.2 hàng `0001`, `0006`, `0011` (dòng 553, 558, 563); TC-06/TC-21 (dòng 893, 908) | **Vòng 1 #4 mới đóng một phần.** §6.2 chỉ ghi mã thật cho `0003`/`0004`/`0014`, rồi viết "không ghi = chưa có code (`S14b` khai mới)". Nhưng chuyển trạng thái phiếu sai, phiếu đã hấp thụ, và đóng phiếu khi hàng chưa về **đã** ném `eCommerce:Sales:0010` trong code (`OrderReturn.cs:173`, `:178-181`, `:208-213`, `:286-293`), và E2E đang khoá đúng mã này (§32.2 `S14-E8`, §34.2 `S14-E19`). Ai làm `S14b` theo §6.2 sẽ khai `Return:0001`/`0006`/`0011` cho những khái niệm đã có mã. Như vậy hoặc trái chính ADR-10/NFR-012 ("đã có mã `Sales:*` thì dùng lại"), hoặc làm đỏ `S14-E8`/`S14-E19` | Ghi mã thật hiện nay (`Sales:0010`) vào ô của `0001`/`0006`/`0011`. Chốt rõ có tách mã hay không. Nếu tách thì ghi lộ trình và nói rõ sẽ đổi kỳ vọng `S14-E8`/`S14-E19`, TC-06/TC-21 |
| 3 | P2 | §3.3 hàng 8 (dòng 246); §5.2 (dòng 470); §6.2 thông điệp `0006` (dòng 558); §8.3 #10 (dòng 701) | Vẫn còn di chứng của vòng 1 #1: bốn chỗ hướng dẫn "**tạo phiếu trả mới**" sau khi phiếu đã kết thúc. Cụ thể: "muốn hoàn thêm ⇒ tạo phiếu trả mới"; "trả một phần nhiều lần — ADR-11"; "Hãy tạo phiếu mới."; "kế toán mở phiếu mới nếu cần". Nhưng theo ADR-11/`D-135`, phiếu `Rejected`/`Closed` làm đơn hấp thụ, nên tạo phiếu mới sẽ NÉM `Order:0006` (`Order.cs:612-622`). Phiếu đã có lần hoàn `Succeeded` thì không huỷ được (§3.1 #5), nên cũng không về `Open` được. UI hoặc vận hành làm theo các câu này sẽ đưa ra nút hay quy trình luôn lỗi | Sửa cả 4 chỗ thành "không có phiếu thứ hai sau phiếu đã kết thúc (nợ Q17)". Bỏ "Hãy tạo phiếu mới." khỏi thông điệp `0006`. §5.2 ghi lý do quan hệ 1–n là "có các phiếu `Cancelled`", không phải "trả nhiều lần" |
| 4 | P2 | §5.1 `OrdReturnLines.RefundAmount` cột ghi chú (dòng 380); cùng câu ở `D-136` (1) | Ghi bất biến `Σ RefundAmount == OrdReturns.RefundedAmount`. Nhưng `RefundedAmount` là "Σ tiền **đã hoàn**" (dòng 348, cộng dồn ở `GhiNhanHoanTien`, `OrderReturn.cs:303-318`), còn `Σ RefundAmount` là số **đề nghị**. Hoàn một phần là luồng hợp lệ (TC-16 60 % rồi 40 %, `PartiallyRefunded`), nên đẳng thức sai ngay giữa chừng. TC-43 lại dùng `Σ RefundAmount` làm **trần** (`≤`). Ai làm theo "==" (assert, CHECK, hoặc gán `RefundedAmount := Σ RefundAmount`) sẽ chặn hoàn một phần, hoặc ghi "đã hoàn" một khoản chưa chi | Viết lại thành `RefundedAmount ≤ Σ RefundAmount` (trần cấp phiếu, TC-43), bằng nhau khi đã hoàn đủ. BR-006 giữ nghĩa "phân bổ tổng xuống dòng". Báo ORCH để `D-136` (1) có dòng đính chính |
| 5 | P3 | BR-005 ví dụ "trả 1/1/1", "trả 1 rồi 2", "trả hết qua nhiều phiếu" + "hàng rào luỹ kế `r0 > 0`" (dòng 117); §5.1 dòng 378–379; TC-40 (iv)(v)(viii)(xi) (dòng 927); §15.3 #10 (dòng 1073) | Theo `D-135`, mỗi lần tạo phiếu thành công thì luôn có `r0 = 0`: huỷ hay từ chối đều nhả `ReturnedQuantity` (`OrderReturnService.cs:176`, `:195`), còn phiếu thứ hai khi phiếu trước chưa huỷ thì NÉM. Vì vậy công thức luỹ kế và hàng rào `r0 > 0` chỉ có nghĩa khi Q17 cho phép phiếu thứ hai. Các ví dụ "qua nhiều phiếu" trái câu "không có lần trả thứ hai" ở ADR-11 | Ghi rõ "`r0 > 0` chỉ xuất hiện khi Q17 mở; dưới `D-135` thì `r0 = 0`". TC-40 (viii) đổi thành "trả hết trong một phiếu". Ghi (iv)/(xi) là test hàm thuần |
| 6 | P3 | §11 hàng "Bảng có thêm cột (còn lại)" (dòng 813) so với Migration 1 (dòng 806); thứ tự các hàng Migration | Hàng tóm tắt ghi `OrdReturnLines (+2 snapshot)`, nhưng Migration 1 thêm **4** cột (`OrderLineQuantitySnapshot`, `ReturnedBeforeQuantity`, `OrderDiscountAllocatedSnapshot`, `OrderTaxAllocatedSnapshot`). Đánh số Migration là 1, 3, 4, 5, 7, 6: thiếu số 2, số 7 đứng trước số 6 | Ghi "+4 cột (2 snapshot số lượng F18 + 2 snapshot phân bổ `D-138`)" và đánh số lại |
| 7 | P3 | §5.1 tiêu đề `OrdOrderLines` (dòng 420); ghi chú SQL thô (dòng 459) | Vẫn ghi "bổ sung 1 cột" và "phải thêm cột `ReturnedQuantity` + constraint bằng SQL thô, **và** backfill `0`", trong khi cột và CHECK đã có từ `Gd1S14_OrderReturn` (chính §11 dòng 805 ghi vậy) | Ghi là "đã có", bỏ việc thêm cột ra khỏi danh sách cần SQL thô |
| 8 | P3 | ADR-11 cột "Lý do chọn" (dòng 91); §34.5 #3 (dòng 2522) | ADR-11 dẫn "`HuyAsync` cho phép tạo phiếu khác — `OrderReturnService.cs:198-201`" làm bằng chứng. Đó là **comment sai** trong code: chính F16 và §3.1 #4 nói huỷ xong thì đơn kẹt `ReturnRequested` và phiếu 2 NÉM `Order:0001`. §34.5 #3 cũng nói "đơn vẫn tạo được phiếu trả khác" mà không có ghi chú lỗi thời như dòng 4 của cùng bảng | ADR-11 bỏ câu dẫn này, hoặc ghi rõ "comment hiện sai (F16)". Thêm ghi chú ở §34.5 rằng #3 đã lỗi thời theo `D-135` |
| 9 | P3 | A3 (dòng 115); §3.1 #1 (dòng 207); §12 (dòng 835) | Ghi đơn `Cancelled` ⇒ `Order:0006`, nhưng code chặn từ trước bằng `Sales:0010` "Đơn đã HUỶ" (`OrderReturnService.cs:95-98`), chưa đi tới `ChuyenKetThuc` | Ghi `Sales:0010` cho trường hợp đơn `Cancelled`, hoặc ghi rõ là cố ý đổi mã (kèm TC) |
| 10 | P3 | BR-012, Basic Flow 9 (dòng 114, 117); §3.2 ghi chú (dòng 231); §12 (dòng 837) | Ghi "`ReceivedQuantity` giảm ⇒ NÉM `0004`", trong khi §6.2 định nghĩa `0004` là "thiếu `ReasonCode`/actor/`Disposition`" | Dùng nhãn khác (vd `0001`, mã thật `Sales:0010`), hoặc mở rộng định nghĩa của `0004` ở §6.2 |
| 11 | P3 | §0 hàng "Khoá phân tán" (dòng 48) | Dẫn `KeyPrefix` ở `eCommerceHttpApiHostModule.cs:115-128`, nhưng thực tế `options.KeyPrefix = "ecommerce:"` nằm ở `:222` và `:237` (grep) | Sửa dòng dẫn |
| 12 | P3 | §23.2 (dòng 1651–1656); §25.5 (dòng 1865–1869); §26.2 (dòng 1908–1916) | Dùng thẻ `<list type="number">`/`<item>`, không phải thẻ HTML hợp lệ (D7 #13), nên renderer Markdown không đánh số và có thể hiện sai | Đổi sang danh sách Markdown `1.` / `2.` |
| 13 | P3 | NFR-SAL-006-005 (§1 dòng 118; §13 dòng 871) | Ghi "gửi lại sự kiện 100 lần ⇒ 1 bút toán **và 1 lần hoàn**", kiểm bằng TC-20. Nhưng TC-20 nay gọi `NhanHangAsync` (API) 100 lần và không phủ phần hoàn tiền | Đổi "cách kiểm" thành TC-20 (kho) + TC-17/NFR-007 (hoàn tiền), hoặc thêm ca cho sự kiện |

## Không kiểm được

- D4: không có khối `sql`/shell. CHECK nội tuyến ở §5.1 (`OrdRefunds`: `"Method" <> 1 OR "EvidenceReference" IS NOT NULL`) **chưa chạy** vì máy không có `psql` (`which psql` không thấy) và không có `docker` (exit 127). Tài liệu đã tự ghi "chưa chạy, chạy khi viết migration `S14b`".
- P1 #1 kết luận bằng **đọc mã** (`CustomerReturnGateway.cs:45-75`, `InventoryDbContextModelCreatingExtensions.cs:166-168`), chưa có test tái hiện vì method hàng lỗi additive chưa tồn tại. QA nên viết TC-34 (vi) đỏ trước khi BE dựng method đó.
- Không chạy `dotnet test`. Số ca ghi trong §28–§35 là số đo lịch sử, chưa đo lại.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — ADR-11/§3.1 #4/#5/§8.3/§12/TC-42 theo `D-135` (2 cạnh `→ Open`, ánh xạ chặt; mã máy trạng thái khớp `OrderStateMachine.cs:184-203`, `Order.cs:612-638`); di chứng ở 4 chỗ khác → chép lên #3, #5, #8 | P1 |
| 2 | đã đóng — BR-005 có công thức `P(x,k)` + `D-136`/`D-138`, cột snapshot ở §5.1, TC-40 (i)–(xii); số ví dụ đã kiểm tay (98.000 · 294.000 · 6.667/3.333 · 19.333/9.667 · 319.000); di chứng → #4, #5 | P1 |
| 3 | đã đóng phần chính — `D-137` (revision, delta, `LastReceiptRevision`, TC-41); **lỗi mới** ở đợt trộn `Restock` + hàng lỗi → #1 | P1 |
| 4 | còn (đóng một phần: `0003`/`0004`/`0014` đã có mã thật) → chép lên #2 | P2 |
| 5 | đã đóng — §6.1/§9/§11/§14.3 khớp `OrderReturnAppService.cs:15/58/73/77/81/85/124/128`, provider `:89-90`, `:96`; 3 permission mới | P2 |
| 6 | đã đóng — ADR-06/§0/§4.1/§4.2/§7.1/TC-20 dùng cổng đồng bộ | P2 |
| 7 | đã đóng — §8.1 #8, §8.2 theo `(đợt, dòng, MovementType)` | P2 |
| 8 | đã đóng — `OrdRefunds.Method` + CHECK cùng bảng; `Manual = 1` khớp `OrderReturn.cs:34` | P2 |
| 9 | đã đóng — F20, A5, BR-009, TC-43 | P2 |
| 10 | đã đóng — ADR-12/BR-016/§8.3 dùng tên khoá thật `sales:return:{tenant}:{orderId}` (`OrderReturnService.cs:75`) | P2 |
| 11 | đã đóng — §11 chỉ còn phần chưa có; hàng tóm tắt lệch số cột → #6 | P2 |
| 12 | đã đóng — §0/§1/F7/TC-10/TC-44 ghi `PayloadHash` chưa so (khớp `OrderReturnService.cs:82-91`) | P2 |
| 13 | đã đóng — §0 F19, Pre-Condition, TC-44 | P2 |
| 14 | đã đóng | P3 |
| 15 | đã đóng | P3 |
| 16 | đã đóng | P3 |
| 17 | đã đóng — chỉ còn `IPaymentRefundPort` (grep `IPaymentHubRefundPort` = 0) | P3 |
| 18 | đã đóng — 16 tên, dòng dẫn `:16-78`/`:190-212`/`:219` khớp | P3 |
| 19 | đã đóng — CHECK `"Disposition" = 0 OR "ReceivedQuantity" > 0` | P3 |
| 20 | đã đóng | P3 |
| 21 | đã đóng | P3 |
| 22 | đã đóng — BR-002 dùng `Order.ChuyenKetThuc` (grep `ExecuteTransition` chỉ còn ở trích `SAL-003` dòng 24 và F1 lịch sử) | P3 |
| 23 | đã đóng | P3 |
| 24 | đã đóng — header `v1.6`; TC-23 không còn `TaxRateSnapshot` | P3 |
