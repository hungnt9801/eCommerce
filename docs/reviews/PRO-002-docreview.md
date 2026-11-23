# Doc review — PRO-002 Cây điều kiện AND/OR & engine khuyến mãi

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (v0.9, 1245 dòng) |
| Ngày · vòng | 23/11/2026 · vòng 4 (kiểm nhanh — `docs/nhanh/handover-n49.md` §2) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | đoạn 1–135, 133–257, 460–495, 496–655, 657–775, 852–1245; `decisions.md:177` (D-163); `pro-001-voucher-coupon.md:461-470` (delta §6.1); code `Enums.cs:121-130`, `CheckoutAppService.cs:32`, `CustomerDtos.cs:27,109` |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (16/16 heading, 20/20 BR có test, mục Đối chiếu Nhanh dòng 1217, 14 câu `Open`) · `kiem-link-tai-lieu.ps1` → exit 0 (180 tệp `.md`, 801 link, 1075 đường dẫn backtick, 0 chết) · `check:mermaid` → exit 0 (211 khối, 0 lỗi) |

Tóm tắt vòng 4 (kiểm nhanh, **không** đọc lại toàn văn): cả **4 phát hiện vòng 3** — #1 (P1) và #2/#3/#4 (P2) — **đã đóng**. Không có P1/P2 **mới** ở các mục BA vừa đổi. Còn **2 P3**: một số liệu trong nhật ký thay đổi (§15.4 `v0.9` ghi "54" test case, thực tế **55**) và trích dẫn số dòng **cũ** còn sót trong delta `pro-001` (dòng 467/469 — ngoài vế vòng 4 yêu cầu soát, dòng 468 thì đã khớp `D-163`).

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep `candidate tự động`/`Kind = 'Voucher'`/`Kind = 'Discount'` toàn tài liệu: §4.2 bước 2 (:244), A2 (1) (:103), §5.2 `Kind` (:382), §6.1 quy tắc 8 (:580) đều nói **cùng một** luật (Voucher chỉ vào qua mã). Grep `kỳ giờ`/`theo kỳ`: chỉ còn các câu "**bỏ** khoá theo kỳ" (:15,:682,:743,:771,:993,:1213) — **không** chỗ nào còn yêu cầu khoá theo kỳ giờ. Grep `0001`/`0002`/`0004`/`0010` + "nguồn": §3.3 (:191), §6.1 (:580), §12 (:862) cùng nói "NÉM ở **mọi** nguồn" | 0 phát hiện |
| D2 chéo tài liệu / code / D-xx | Đọc `decisions.md:177` (D-163), đối chiếu §6.1 quy tắc 11 (:583) — khớp từng vế (đếm cả đơn huỷ/trả, đơn đang xét không tính, không "đơn nháp", `CustomerId NULL ⇒ null`, dùng chung `PRO-001`/`PRO-002`). Delta `pro-001:468` khớp `D-163`. §14.4 DoR tick `Q27 — D-163` (:1137). Code: `Enums.cs:121-130` đúng **7** `OrderStatus` (`Pending…Returned`, không `Draft`); `CheckoutAppService.cs:32` "**Chưa tạo đơn hàng**"; `CustomerDtos.cs:27` = `CustomerGroupId`, `:109` = `ICustomerDirectory` (nguồn `Sales` nạp) | 0 phát hiện (1 P3 delta — xem #2) |
| D3 cơ chế thực thi | Bốn đường vừa sửa, tìm "cái gì đỏ khi sai": (a) candidate tự động `Kind='Discount'` ⇒ `TC-67` (i) request không mã ⇒ `DiscountTotal=0` + 0 redemption, (ii) có mã ⇒ xuất hiện **đúng một** lần; (b) job không khoá-theo-kỳ ⇒ `TC-50` (viii) `StartsAtUtc` giữa hai lần chạy ⇒ lần sau kích hoạt; (c) lỗi cấu hình NÉM mọi nguồn ⇒ `TC-68` seeder/test **ĐỎ**; (d) "đơn đầu" ⇒ `TC-69` (i)–(vii) + `TC-66` (ii) | 0 phát hiện |
| D4 SQL chạy được | Grep ```` ```sql ````/```` ```powershell ````/```` ```bash ````: **không có** khối rào nào. Biểu thức `CHECK`/unique partial §5.3 (:464-485) và `UPDATE … WHERE còn lượt` (Basic Flow 7, §8.2 #1) **chưa chạy**: `which psql docker` ⇒ **không có** trên máy kiểm | 0 phát hiện (chưa chạy) |
| D5 test/probe/tệp có thật | Mọi `TC-xx` được trích ở vòng 4 (`TC-50`, `TC-66`…`TC-69`) đều có hàng ở §14.1; đếm `grep -c '^| \*\*TC-'` = **55** hàng (§14.1). Code refs của vòng 4 mở đúng dòng: `Enums.cs:121-130`, `CheckoutAppService.cs:32`, `CustomerDtos.cs:27,109` | 0 phát hiện |
| D6 nhãn tin cậy | `Q27` `Closed — D-163` (:1174) có `D-xx`; §6.3 route gắn 🔶 chưa đo (:623); DoD đếm **55** khớp §14.1 (:1100). Nhật ký `v0.9` ghi "**54**" test case (:1213) ⇒ lệch số | 1 phát hiện (#1, P3) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14: Mermaid 211 khối 0 lỗi (3 sơ đồ `:138,:203,:263`), không `;`/`<...>`. Mục "Đối chiếu mô hình Nhanh" (:1217) đủ heading, có **23** hàng `D-xx` + câu "không lệch" cho `D-07/D-08/D-17/D-27/D-45/D-47/D-48/D-03/D-06/D-70`, có hàng `D-163` (:1239) khớp thân §6.1 quy tắc 11 | 0 phát hiện |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §15.4 nhật ký `v0.9` (dòng 1213) | Ghi "DoD **54** test case", nhưng §14.3 (:1100) và §14.1 (§14.1 có **55** hàng: `TC-01…TC-51` + `TC-66…TC-69`) là **55**. Số liệu nhật ký lệch 1, không ảnh hưởng hành vi. | Sửa `54` → `55`. |
| 2 | P3 | delta `pro-001-voucher-coupon.md` §6.1 dòng 467, 469 (ngoài vế vòng 4 yêu cầu soát — dòng 468 đã khớp `D-163`) | Dòng 467/469 còn trích số dòng **cũ** của `pro-002`: `pro-002:164`, `:513`, `:186`, `:550` (:467) và `pro-002:101` (:469) — nay ở `:169`, `:588`, `:191`, `:633`, `:105`. Trích theo số dòng sẽ vỡ ở lần sửa sau. | Đổi sang **tên mục** (`pro-002` §3.2, §6.2, §6.4, BR-08) thay cho số dòng. |

## Không kiểm được

- D4: không có khối ```` ```sql ````. Các biểu thức `CHECK`, unique partial `NULLS NOT DISTINCT … WHERE …` ở §5.3 (:464-485) và `UPDATE … WHERE` ở Basic Flow 7 / §8.2 **chưa chạy** — máy kiểm không có `psql` lẫn `docker` (`which psql docker` ⇒ không tìm thấy).
- Route §6.3 theo quy ước ABP: tài liệu đã tự gắn 🔶 chưa đo (chưa có code `Promotions` để chạy swagger).
- Vòng 4 là **kiểm nhanh**: KHÔNG đọc lại toàn văn. Các mục **ngoài phạm vi** vòng 4 (§5.1/§5.2, §9–§11, §10) không rà lại; kết luận vòng 3 về các mục đó vẫn giữ (đã đóng ở vòng 3).
- Delta `pro-001` dòng 467/469 (trích số dòng cũ) là **P3 ngoài phạm vi** vòng kiểm nhanh (vòng 4 chỉ yêu cầu soát dòng 468) — ghi ở bảng Phát hiện #2 để không mất dấu.

## Vòng trước (vòng 3 — 4 phát hiện bắt buộc + 5 P3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Mức lúc đó để ở cột 3. -->

| # (v3) | Trạng thái | Mức lúc đó | Bằng chứng đóng (vòng 4) |
|---|---|---|---|
| 1 | đã đóng | P1 | §4.2 bước 2 (:244) — tự động = `Kind = 'Discount'`, `Voucher` chỉ qua `VoucherCode`, khử trùng theo `PromotionId`; A2 (1) (:103); §6.1 quy tắc 8 (:580); §5.2 `Kind` (:382); `TC-67` (:996) |
| 2 | đã đóng | P2 | §7.2 #5 (:682), §8.2 #5 (:743), bảng khoá (:771), `TC-50` (viii) (:993) — bỏ khoá idempotency **theo kỳ giờ**, job vẫn kích hoạt `StartsAtUtc` giữa hai lần chạy |
| 3 | đã đóng | P2 | §6.1 quy tắc 8 (:580) + §3.3 `LoiCauHinh` (:191) — `0001/0002/0004/0010` **NÉM ở mọi nguồn** (`D-154` (5)); §12 (:862) hàng rào trước deploy; `TC-68` (:997); `Q28` (:1175) mở, không chặn |
| 4 | đã đóng | P2 | `PromotionEvaluationRequest.CustomerGroupId`/`IsFirstOrder` có nguồn (:519-520); `PromotionRuleContext` định nghĩa (:558-559, "Định nghĩa context" :567); quy tắc 10/11 (:582-583); `Q27` `Closed — D-163` (:1174); `TC-69` (:998) |
| 5 | đã đóng | P3 | §14.3 nay ghi **55** test case (:1100) + giải thích dải `TC-52`…`TC-65` thuộc `PRO-001` |
| 6 | đã đóng | P3 | Basic Flow 1 (:102) + §4.2 bước 1 (:243) không còn "`Now` từ `Sales`"; engine lấy `IClock` một lần (§6.1 quy tắc 2 :574) |
| 7 | đã đóng | P3 | §0 :61 viết lại: phạm vi quầy `RetailBill` rõ ràng; khuyến mãi tự động cho `web`/`Sales` **trong** phạm vi |
| 8 | đã đóng | P3 | §6.1 quy tắc 4 (:576) kiểm `Mode` **hai chiều** (`VoucherCode ≠ null ⇔ Mode = CustomerRequested`) |
| 9 | còn (ngoài phạm vi) | P3 | delta `pro-001` :467,:469 vẫn trích số dòng cũ của `pro-002` — đã chép lên bảng "Phát hiện" **#2** |

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — `D-160` đã áp: A2 (2a)/(5), BR-11, §4.2 bước 7, enum `= 10`, `Subtotal` ở request (:517) + quy tắc 9, §12 :865, §13 "12 giá trị `0…11`", Q22/Q23 `Closed`, DoR :1123, `TC-48`/`TC-49`, hàng đối chiếu :1220 | P2 |
| 2 | đã đóng — candidate tự động hết lượt ⇒ bỏ qua + `UsageLimitHit`, không ném: Basic Flow 6, §3.3 :188, §4.1 :229, §4.2 :248, §12 :868, `TC-51` | P2 |
| 3 | đã đóng — quy tắc 8 (:575) chế độ theo nguồn candidate. BR-07, §7.4 #4, §12 :857/:872 viết theo đó. Có `TC-07` (c) | P2 |
| 4 | đã đóng — `UsageSequence` của dòng tự động ở Basic Flow 7, `TC-43` (iv)–(vi); vãng lai theo `D-161` | P2 |
| 5 | đã đóng — `promotion-expiry` kiêm kích hoạt: §7.2 #5, §8.2 #5, UC-06, §3.2 :171, `TC-50` (khoá theo kỳ giờ sinh vấn đề mới ⇒ #2 vòng này) | P2 |
| 6 | đã đóng — khoá lưu cây có `TreeVersion` (:673, :762), `TC-28` (f) | P2 |
| 7 | đã đóng — route `POST /api/app/promotion` (:608) | P3 |
| 8 | đã đóng — route theo quy ước ABP + 🔶 chưa đo (:609-617) | P3 |
| 9 | đã đóng — `TC-13` (a)–(d) là mã khách nhập, (a) đổi `Percentage` 100 % | P3 |
| 10 | đã đóng — §10.2 :813 lấy từ `EvaluateAsync` | P3 |
| 11 | đã đóng — dòng trống trước `**Idempotency key**` (:754) | P3 |
| 12 | đã đóng — record `PromotionBreakdownItem` (:525-530) | P3 |
