# Doc review — «LOY-001» «Điểm thưởng»

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/loyalty/loy-001-diem-thuong.md` (v0.8 — 24/11/2026) |
| Ngày · vòng | 24/11/2026 · vòng 4 — **kiểm nhanh** (chỉ mục BA đã đổi theo `changedSections` v0.8 + xác nhận 3 phát hiện vòng 3; mục không đổi giữ kết luận vòng 3) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–943 (toàn văn) + `docs/nhanh/decisions.md` `D-167`/`D-168` (đọc nguyên văn dòng 181–182) |
| Máy | `check-feature-doc.ps1 -Path docs/features/loyalty/loy-001-diem-thuong.md -WP14` → exit 0 (0 lỗi; 16/16 heading, 24/24 BR có test, mục đối chiếu Nhanh ở dòng 925); `npm --prefix tools run check:mermaid` → 211 khối, **0** lỗi (5 khối của `loy-001`: dòng 144/193/248/280/309 PASS) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep toàn văn các ràng buộc `CẤM`/`KHÔNG`/`phải`; đối chiếu chéo §0 ⇔ §1 ⇔ §5.1/§5.2 ⇔ §6.1/§6.2 ⇔ §7.0/§7.1 ⇔ §8.1 ⇔ §9 ⇔ §10 ⇔ §11 ⇔ §12 ⇔ §13 ⇔ §14 ⇔ §15 + bảng "Đối chiếu" | 1 phát hiện (#2 — §7.1 "6 biên vào" vs 7 hàng) |
| D2 chéo tài liệu / code / D-xx | đọc `D-167`/`D-168` (`decisions.md`:181–182) nguyên văn; mở đúng dòng code: `IDataScopeResolver.cs:70/77/86` (6/6/5 tham số), `CustomerLedger.cs:37–110`, `RetailBillAppService.cs:262–268/323–329`, `RetailReturnAppService.cs:146–151/183–188`, `PosDtos.cs:33/35/119/122/285/312`, `RetailBill.cs:233`, `StockLedger.cs:260–262`, `eCommerceHttpApiHostModule.cs:222/237`, `eCommerceMetrics.cs:219–222`, permission provider (**19** nhóm) | 2 phát hiện (#1 §11 thiếu unique `RetailBillId`; #3 trích `PosDtos` chưa phủ `PointsUsed`) |
| D3 cơ chế thực thi | rà cái gì **đỏ** khi vi phạm cho thay đổi `D-168`: unique `(TenantId, RetailBillId)`, `TC-52` (kiến trúc)/`TC-53` (Integration), `TC-49` mở rộng, trigger append-only | xuyên #1 (unique thiếu trong danh sách §11) |
| D4 SQL chạy được | liệt kê code fence: **5** `mermaid` + **1** `csharp` (skeleton placeholder); **không** có `sql`/`powershell`/`bash` | 0 phát hiện (không có câu để chạy — xem "Không kiểm được") |
| D5 test/probe/tệp có thật | mở đúng dòng mọi `file:dòng` ở mục đã đổi; module `Loyalty` **chưa tồn tại** ⇒ `TC-49`/`TC-52`/`TC-53` + trigger/index là **kế hoạch** | 0 phát hiện |
| D6 nhãn tin cậy | các ✅ đổi/mới (§0.2, §15.2 `G9`) đều kèm dòng code đã mở; không có ✅ chưa đo | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | NFR có ngưỡng số (§13); guard-sai có hành vi (§3); tên khoá phân tán cụ thể (§5.3); mermaid không `;`/`<...>`; mục "Đối chiếu mô hình Nhanh" (dòng 925) có hàng `D-168` + ghi `D-167` đã cập nhật 24/11/2026 | 0 phát hiện mới |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §11 dòng 634 (hàng "Unique index + `NULLS NOT DISTINCT`") ↔ §5.2 dòng 336 (hàng 4) ↔ `D-168` (4) | §11 ghi "**Bắt buộc** cho **mọi** unique index có `TenantId`" rồi liệt kê **3** (`LoyAccounts(TenantId, CustomerId)`, `LoyLedgerEntries(TenantId, ReferenceType, ReferenceId, EntryType)`, `LoyRedemptions(TenantId, IdempotencyKey)`) — **thiếu** `LoyRedemptions(TenantId, RetailBillId) WHERE RetailBillId IS NOT NULL` (và `(TenantId, OrderId) WHERE OrderId IS NOT NULL` từ trước) mà `D-168` (4) gắn đích danh "(NULLS NOT DISTINCT, **§11**)" và §5.2 hàng 4 định nghĩa. Ai dựng migration `Added_Loyalty_Points` theo §11 sẽ **không** tạo unique chống trùng lô tiêu POS theo hoá đơn ⇒ mất lưới chống trùng DB của biên vào POS. | Thêm `LoyRedemptions(TenantId, RetailBillId) WHERE RetailBillId IS NOT NULL` + `LoyRedemptions(TenantId, OrderId) WHERE OrderId IS NOT NULL` (cùng `NULLS NOT DISTINCT`) vào danh sách §11 dòng 634. |
| 2 | P3 | §7.1 dòng 476 | Heading "Sự kiện NHẬN VÀO (**6** biên vào — …)" nhưng bảng ngay dưới có **7** hàng (thêm hàng `Pos` bán/huỷ/trả — `D-168`, dòng 482). | Đổi "6" → "7". |
| 3 | P3 | §1 3b (dòng 133) · §6.1 (dòng 414) · §7.1 hàng `Pos` (dòng 482) | Trích `PosDtos.cs:35,122` cho "bỏ tin `PointsEarned`/`PointsUsed` của client": `:35`/`:122` đều là `PointsEarned`, còn `PointsUsed` ở `:33`/`:119` ⇒ trích **chưa phủ** `PointsUsed`. | Trích `PosDtos.cs:33,35,119,122` (hoặc `:119,122` cho DTO đầu vào `CreateRetailBillDto`). |

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` (chỉ 5 `mermaid` + 1 `csharp` skeleton placeholder) ⇒ không có câu SQL nào để chạy `psql BEGIN…ROLLBACK`; DDL trigger `LoyLedgerEntries_AppendOnly`/`CHECK`/`EXCLUDE` **chưa chạy** (module `Loyalty` chưa có trong code).
- **D5 (một phần):** module `Loyalty` **chưa tồn tại** trong repo ⇒ `TC-49`/`TC-52`/`TC-53`, trigger `LoyLedgerEntries_AppendOnly`, các unique index là **kế hoạch**; chỉ kiểm được các tệp thật ở `Pos`/`Customers`/`Sales`/`Kernel` (đã mở đúng dòng).
- `csharp` skeleton §14.2 dùng `Shouldly`/`TaoTaiKhoanVoiSoDu` (placeholder) — minh hoạ, không biên dịch; không phải cam kết chạy.

## Vòng trước

> Cột 2 là "Trạng thái" (không phải "Mức") để `check-agent-artifact.ps1` không đếm trùng P1/P2/P3.

### Vòng 3 (24/11/2026, `DOC-CHANGES`) — đã đóng cả 3

| # vòng 3 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | **đã đóng (trong tài liệu v0.8)** — POS bán/huỷ/trả nay đi **cổng** `ILoyaltyEarnGateway` cùng UoW, `modules/Pos/**` **0** lời gọi `ICustomerLedger`; §8.1 #2/#12 ghi POS là một nguồn; §14 thêm `TC-52`/`TC-53` + mở rộng `TC-49`; Q12 ⇒ `Closed (D-168)`; §15.2 `G9`. Đã kiểm code: `RetailBillAppService.cs:262–268/323–329`, `RetailReturnAppService.cs:183–188` xác nhận POS **hiện** gọi thẳng (việc phải CR) — tài liệu phản ánh đúng hiện trạng + kế hoạch `D-168`. | P1 |
| 2 | **đã đóng** — `D-167` (`decisions.md`:181) đã **cập nhật 24/11/2026**: thêm `noGiam` (**8** tham số `GhiNhanDaoDiemTraHangAsync`), trạng thái giữ `TẠM CHỐT`; A2/§7.0/§7.1/§12/`TC-12`/bảng đối chiếu (dòng 942) đều khớp "8 tham số" + "Loyalty **chuyển nguyên** `noGiam` xuống `ICustomerLedger.GhiNhanTraHangAsync` (5 tham số — `IDataScopeResolver.cs:86`, đã mở: trả `Task<bool>`)". | P2 |
| 3 | **đã đóng** — §10 (dòng 600/609/617) nay tách rõ: `balance` = `Customer.Points` gọi "**điểm tích luỹ**"; "**số dư khả dụng**" = `preview.maxRedeemable`; ghi đích danh "**không** gọi `balance` là 'số dư khả dụng'". | P2 |

### Vòng 2 (24/11/2026) — lịch sử

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

### Vòng 1 — lịch sử

> **Vòng 1:** 9 mục; #1–#6 đã đóng, #7–#9 chuyển thành #6–#8 vòng 2 (P3). Các P3 vòng 2 (#3–#8) đã được v0.8 sửa trong tài liệu (Basic Flow **6** tham số `GhiNhanBanHangAsync`; `StockLedger` 260–262; `0015` đủ **3** điều kiện + no-op xét trước; `KeyPrefix` 222/237; §9 **19** nhóm; `Forbidden` 219–222) — đã kiểm lại ở vòng 4, khớp code.
