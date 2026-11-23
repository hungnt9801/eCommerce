# Doc review — «LOY-001» «Điểm thưởng»

**VERDICT: DOC-CHANGES**

| | |
|---|---|
| Tài liệu | `docs/features/loyalty/loy-001-diem-thuong.md` (v0.7 — 24/11/2026) |
| Ngày · vòng | 24/11/2026 · vòng 3 — **kiểm nhanh** (chỉ mục BA đã đổi theo `changedSections` v0.7 + xác nhận P1/P2 vòng 2) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–926 (toàn văn) |
| Máy | `check-feature-doc.ps1 -Path docs/features/loyalty/loy-001-diem-thuong.md -WP14` → exit 0 (0 lỗi; 16/16 heading, 24/24 BR có test, mục đối chiếu Nhanh ở dòng 909) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep 18 câu cấm/`CẤM`/`KHÔNG được` trên 926 dòng; đối chiếu chéo §5.2 (dòng 340) ⇔ §3 guard #2/#3 (206–207) ⇔ §6.1 (397–399) ⇔ §6.2 `0001` (418) ⇔ §8.1 #2/#6 (534/538) ⇔ §10 (594/603) ⇔ §12 (643) ⇔ §13 NFR-003 (681) ⇔ TC-05/TC-49 (720/764) | 1 phát hiện (#3) |
| D2 chéo tài liệu / code / D-xx | mở đúng dòng: `IDataScopeResolver.cs:60–88/70–71/77–78/86` (chữ ký `GhiNhanTraHangAsync` = **5** tham số có `noGiam`, trả `Task<bool>`), `CustomerLedger.cs:86–110`, `Customer.cs:454–513/473/492–513/526`, `CustomerDtos.cs:43/109`, `RetailReturnAppService.cs:183–188`, **`RetailBillAppService.cs:242/262–268`**, `decisions.md` dòng `D-167` (181), `SAL-006` §7.1 :318 + `BR-023` :117 | 2 phát hiện (#1, #2) |
| D3 cơ chế thực thi | rà 24 BR + 12 bất biến §8.1 (v0.7): cái gì **đỏ** khi vi phạm (guard/unique/CHECK/trigger/test `TC-*`), tập trung bất biến mới "`pointsFromLots == Customer.Points` mọi thời điểm" | 1 phát hiện (#1) |
| D4 SQL chạy được | liệt kê code fence: 5 `mermaid` (140/189/244/276/305) + 1 `csharp` (770); **không** có `sql`/`powershell`/`bash` ⇒ không có câu nào để chạy trong `psql BEGIN…ROLLBACK` | 0 phát hiện |
| D5 test/probe/tệp có thật | mở đúng dòng mọi `file:dòng` trong mục đã đổi; grep `modules/Sales/**` cho `GhiNhanBanHangAsync\|DaoBanHangAsync\|GhiNhanTraHangAsync` → **0** hit (nên `TC-51` khả thi, đường Sales hiện chưa gọi thẳng); `module Loyalty` vẫn **chưa tồn tại** ⇒ test/cổng/trigger của Loyalty là kế hoạch | 0 phát hiện |
| D6 nhãn tin cậy | §0.2/§7.0: các ✅ đã đổi (`Money` ✅, `IIdempotencyGuard` ✅, outbox ✅ dòng 35/377–379) đều có dòng code kèm; không có ✅ mới chưa đo trong mục v0.7 | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | `npm --prefix tools run check:mermaid` → 211 khối, **0 lỗi**; NFR có ngưỡng số; guard-sai có hành vi (§3 #2/#3); mục "Đối chiếu mô hình Nhanh" dòng 909 + hàng mới `D-167`/`D-45` (926); tên khoá phân tán ghi cụ thể (§5.3/§7.1/§7.3) | 0 phát hiện mới (xuyên #1, #2) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P1 | §8.1 #2 (dòng 534) · §5.2 chú thích (dòng 340) · §13 NFR-003 (dòng 681) · §6.1 hàng `GET /balance` (dòng 397) · TC-49 (dòng 764) · §11 "Không backfill: chưa có điểm nào" (dòng 632) | **Bất biến "`pointsFromLots` == `Customer.Points` **mọi thời điểm**" (bất biến v0.7 mở rộng) không thể đúng vì POS ghi điểm **không tạo lô**.** Đường POS đã có thật trong repo: `RetailBillAppService.cs:262–268` gọi thẳng `ICustomerLedger.GhiNhanBanHangAsync(diemTich: hoaDon.PointsEarned, diemSuDung: hoaDon.PointsUsed)` (giá trị do client gửi — `RetailBill.HoanTat(..., diemTich: input.PointsEarned)` :242), và `RetailReturnAppService.cs:183–188` gọi `GhiNhanTraHangAsync(...)` trực tiếp — **không** chỗ nào đi qua `LoyLedgerEntries`/`LoyCreditLots`. Hệ quả khi làm theo tài liệu: (a) khách có hoá đơn POS tích/tiêu điểm ⇒ `GET /api/app/loyalty/admin/reconciliation` `difference ≠ 0` **vĩnh viễn** (ô đỏ + ticket, §10 dòng 606) trong khi `TC-49` dựng 10.000 bút toán **bằng Loyalty** nên **xanh**; (b) `preview.maxRedeemable` = Σ lô (chỉ phần Sales) ⇒ khách **không tiêu được** điểm đã thấy ở `balance` (§6.1 trả `Customer.Points`) — mất quyền lợi đã hiển thị; (c) §11 "chưa có điểm nào" sai nếu bất kỳ hoá đơn POS nào đã ghi `PointsEarned > 0` ⇒ không có kế hoạch backfill lô. | Chốt **một** hướng và ghi vào tài liệu: (a) **ưu tiên** — POS cũng đi cổng Loyalty (`GhiNhanDiemHoaDonAsync` cho `RetailBill`, POS **cấm** truyền `diemTich`/`diemSuDung` thẳng xuống `ICustomerLedger`; sửa §1/§7.1 thêm biên vào POS, thêm TC kiến trúc cho `modules/Pos/**` + TC đối soát có hoá đơn POS); hoặc (b) nếu POS ngoài phạm vi ⇒ **backfill lô** cho tồn điểm POS (hoặc tạo lô gốc "không hết hạn"), định nghĩa lại `maxRedeemable`/guard cho phần điểm không lô, và sửa NFR-003/§8.1 #2 thành "chỉ phần điểm Loyalty sở hữu". Ghi ca POS vào `TC-49`. |
| 2 | P2 | §1 A2 (dòng 131) · §7.0 (dòng 453) · §7.1 (dòng 476/483) · §12 (dòng 651) · TC-12 (dòng 727) · "Đối chiếu" (dòng 926) ↔ `D-167` trong `docs/nhanh/decisions.md` (dòng 181) | **Chữ ký cổng v0.7 (8 tham số, có `noGiam`) lệch dòng quyết định `D-167` (7 tham số, không `noGiam`).** `D-167` ① vẫn ghi `GhiNhanDaoDiemTraHangAsync(customerId, orderId, returnId, T, V, V_trước, nguon)`; tài liệu tự nhận "đối chiếu `D-167`" (dòng 926, 917) nhưng thêm `noGiam` mà **không** ghi delta vào quyết định. Ai đọc `decisions.md` — điểm vào #3 theo `CLAUDE.md` §1, và là nguồn cho hợp đồng chéo module — sẽ dựng cổng thiếu `noGiam` ⇒ lặp lại đúng lỗi P1 vòng 2 (mất giảm nợ khi `noGiam ≠ 0`). | ORCH/decider cập nhật dòng `D-167` (**TẠM CHỐT** — được sửa theo `CLAUDE.md` §5) thêm `noGiam` + câu "Loyalty **chuyển nguyên** xuống `ICustomerLedger.GhiNhanTraHangAsync` (5 tham số, `IDataScopeResolver.cs:86`)", hoặc thêm `D-168` ghi delta chữ ký; bảng "Đối chiếu" ghi rõ "D-167 đã cập nhật `<ngày>`". |
| 3 | P2 | §10 bề mặt 1 (dòng 594) · bề mặt 2 (dòng 603) ↔ §5.2 (dòng 340) · §6.1 (dòng 397) · `CustomerDtos.cs:43` | **§10 gọi thứ đọc từ `ICustomerDirectory` là "số dư khả dụng" — trái định nghĩa vừa chốt.** `ICustomerDirectory` trả `CustomerDto.Points` (= `Customer.Points`), còn "số dư khả dụng" (v0.7) = Σ `RemainingAmount` lô **chưa hết hạn**; hai số khác nhau trong cửa sổ hết hạn **và** với điểm không lô (#1). FE làm theo §10 sẽ dán nhãn "dùng được" lên số khách không tiêu được ⇒ lỗi hiển thị tài sản điểm, đúng loại lỗi mà `NFR-010`/BR-006 muốn chặn. | Sửa §10: thẻ "Điểm của tôi" hiện `balance` (`LoyaltyBalanceDto` = `Customer.Points`, gọi đúng tên "điểm tích luỹ"), **số dùng được** lấy `preview.maxRedeemable` (số dư khả dụng — §5.2); bỏ chữ "(từ `ICustomerDirectory`)" ở hai ô đang gọi là "số dư khả dụng". |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` (chỉ 5 `mermaid` + 1 `csharp`) ⇒ không có câu SQL nào để chạy trong `psql BEGIN…ROLLBACK`.
- **D5 (một phần):** module `Loyalty` **chưa tồn tại** trong repo ⇒ entity, cổng `ILoyaltyEarnGateway`, `LoyaltyPointsCalculator`, trigger `LoyLedgerEntries_AppendOnly`, `CHECK`/`EXCLUDE`, `TC-01…TC-51` là **kế hoạch**, không kiểm được sự tồn tại. Chỉ kiểm được các tệp thật được trích ở `Customers`/`Sales`/`Pos`/`Inventory`/`Kernel` (đã mở đúng dòng).
- **D4/D7 — PostgreSQL:** chưa chạy; các cam kết trigger/`CHECK`/`EXCLUDE`/`NULLS NOT DISTINCT` ghi "chưa chạy" (tài liệu tự ghi ⚠️ *chưa có tiền lệ*). Không có migration nào tồn tại để chạy.
- `csharp` skeleton §14.2 dùng `ShouldBe`/`TaoTaiKhoanVoiSoDu` (placeholder) — minh hoạ, không biên dịch; không phải cam kết chạy.
- Phát hiện **#1** dựa trên code thật đã đọc (không phải suy đoán cơ chế); phần "điểm POS đã phát sinh trong dữ liệu production" **chưa đo được** (repo không có client POS; `PointsEarned` do client gửi) — nhưng đường ghi là **hợp đồng API đang mở**.

## Vòng trước (vòng 2 — 24/11/2026, `DOC-CHANGES`)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | **đã đóng (trong tài liệu)** — cổng nay 8 tham số có `noGiam` (A2/§7.0/§7.1/§12/TC-12/TC-51/TĐC đều khớp); Loyalty **chuyển nguyên** xuống `ICustomerLedger.GhiNhanTraHangAsync` (5 tham số có `noGiam` — `IDataScopeResolver.cs:86` xác nhận); khẳng định "đã viết trong SAL-006 §7.1" đã đổi thành **CR chéo bắt buộc** và `SAL-006` :318/`BR-023` :117 **đúng là vẫn** ghi Sales gọi thẳng. Vết còn: dòng quyết định `D-167` chưa cập nhật ⇒ phát hiện #2 mới | P1 |
| 2 | **đóng một phần** — phần "cửa sổ hết hạn" đã đúng và nhất quán (§5.2/§3/§6.2 `0001`/§8.1 #2-#6/§13/§12/TC-05/TC-49 đều dùng "số dư khả dụng", yêu cầu vượt ⇒ NÉM `0001`, 0 `Consumption`); phần "`pointsFromLots == Customer.Points` **mọi thời điểm**" **còn sai** với điểm POS ⇒ phát hiện #1 mới + #3 (§10) | P2 |
| 3 | còn — để nguyên theo yêu cầu (#3 vòng 2: Basic Flow bước 2 thiếu `noTang` trong `GhiNhanBanHangAsync`) | P3 |
| 4 | còn — để nguyên (`StockLedger.cs` dòng 100–103 ⇒ đúng là 260–262) | P3 |
| 5 | còn — để nguyên (§6.2 `0015` chỉ ghi `V > T`, chưa đủ 3 điều kiện; no-op xét trước) | P3 |
| 6 | còn — để nguyên (§5.3 trích `eCommerceHttpApiHostModule.cs` 115–128 ⇒ đúng 222/237) | P3 |
| 7 | còn — để nguyên (§9 "provider 6 nhóm" ⇒ thực tế nhiều nhóm) | P3 |
| 8 | còn — để nguyên (§13 dòng 702–704 trích `Forbidden` 81–85 ⇒ thực tế 219–222, không có `customerId`) | P3 |

> **Vòng 1:** xem lịch sử ở bản vòng 2 — 9 mục; #1–#6 đã đóng, #7–#9 chuyển thành #6–#8 vòng 2 (vẫn còn, P3).
