# Doc review — SAL-006 Trả hàng / RMA và hoàn tiền (một phần / toàn bộ)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/sales/sal-006-tra-hang-rma-hoan-tien.md` (2723 dòng, v1.7) |
| Ngày · vòng | 21/11/2026 · vòng 3 (kiểm nhanh: chỉ các mục BA đổi từ v1.6 sang v1.7) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | toàn bộ `git diff --word-diff` v1.6 → v1.7 (79 dòng thêm / 81 dòng bớt); ngữ cảnh ở các dòng 38–53, 205–249, 340–384, 547–566, 628–667, 800–849, 929, 1052–1054, 2686–2720; `decisions.md:146-154` (`D-132`…`D-140`); báo cáo vòng 2 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi; 16/16 heading, 23/23 BR có test, mục đối chiếu ở dòng 2694; INFO 14 câu `Open`, 0 TODO) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep trên toàn tệp các mẫu: ``mỗi `(đợt…`/``mỗi `(dòng…`` (8 chỗ: :113, :117, :118, :663, :871, :907, :2703, :2710), `Return:0001/0006/0011`, `` `0004` `` (27 chỗ), `phiếu mới`/`tạo phiếu (trả) mới`, `Σ RefundAmount ==`, `một đợt = một chứng từ`, `cùng số chứng từ`, `Migration [0-9]`. So BR-012 với Basic Flow 9, §7.3, §7.4, §8.1 #8, TC-20, TC-34 (vi), TC-41 (v), TC-45 | 2 phát hiện (#1, #3) |
| D2 chéo tài liệu / code / D-xx | So thân tài liệu với `D-139` (1)–(6) ⑥ và `D-140` (1)–(5). Đọc code: `CustomerReturnGateway.cs:1-83` (idempotent `:45-54`; `autoSave` + `GhiSoAsync` `:62-75`); `OrderReturnAppService.cs:85-122`; `OrderReturn.cs:172-294`, `:353`, `:428-443` (`LedgerEntryId = chungTuId`); `OrderReturnService.cs:92-99`, `:320-333`; `SalesErrorCodes.cs:14`, `:20`; `eCommerceConsts.cs:522`, `:525`; `TinhButToanChungTu.cs:33-36`; `Enums.Inventory.cs:4-146`. Grep: `InventoryDbContextModelCreatingExtensions.cs` (`HasIndex` ở `:94`, `:110`, `:166-168`, `:240`); `StockPostingService.cs` (`requiresNew`/`UnitOfWork` = 0); `eCommerceHttpApiHostModule.cs` (`:94` `TransactionBehavior.Auto`, `:222`, `:237`, `:299`); `options.Map(` cho mã `Sales` trên toàn repo = 0 | 4 phát hiện (#2, #5, #6, #7) |
| D3 cơ chế thực thi | Mỗi BR mới, cái gì đỏ khi vi phạm: "không có hai lời gọi cổng cùng số" → TC-45 (vi); "lời gọi sau NÉM ⇒ cả đợt rollback" → TC-45 (iii) + UoW transactional (`TransactionBehavior.Auto`, POST); gộp theo SKU → unique `:166-168` + TC-45 (ii); `D-140` → `S14-E8`/`S14-E19` + TC-06/TC-21 | 0 phát hiện |
| D4 SQL chạy được | Grep fence `sql`/`powershell`/`bash`/`sh`/`pwsh` = 0 khối. Phần BA đổi không thêm SQL | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | `S14-E8`, `S14-E19` có ở `tools/kiem-e2e.ps1:1731`, `:1788`, `:1978`, `:2047` (khẳng định `eCommerce:Sales:0010`). TC-45 có ở §14 (dòng 932) và được dẫn đúng ở BR-012 (iii)(iv)(vi), §7.4, §8.1 #8, Q16, F17. DoD §14 ghi "45 test case" (dòng 982). `KeyPrefix` `:222`/`:237` khớp. `OrderReturn.cs:173`/`:178-181`/`:208-213`/`:286-293` và `OrderReturnService.cs:95-98` khớp nội dung được dẫn | 0 phát hiện |
| D6 nhãn tin cậy | Ghi chú §6.2 (dòng 549) "grep = 0 `Sales`": đo lại, đúng. Kết luận "HTTP theo code, chưa xác định" dừng trước con số → #2 | 1 phát hiện (#2) |
| D7 14 quy tắc + đối chiếu Nhanh | `npm --prefix tools run check:mermaid` → 211 khối, 0 lỗi (exit 0). `pwsh -File tools/kiem-link-tai-lieu.ps1` → 772 link + 977 backtick, 0 chết (exit 0). Grep `<list`/`<item>`: chỉ còn trong chữ của dòng lịch sử 2690. Mục đối chiếu (dòng 2694–2719) có hàng `D-139` và `D-140`; hàng `D-137` đã ghi rõ phần (4) bị thay | 1 phát hiện (#6) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | BR-012 câu đầu (dòng 117); Post-Condition (dòng 113); §8.1 #8 (dòng 663); TC-20 (dòng 907). So với NFR-005 (dòng 118, 871), câu gộp SKU của BR-012, TC-45 (ii) | **Đơn vị đếm bút toán chưa được sửa theo `D-139` (3).** Câu đầu BR-012 ghi "đúng một bút toán mỗi `(đợt nhận, dòng phiếu trả, MovementType)`". Nhưng câu sau của chính BR-012, cùng TC-45 (ii), yêu cầu gộp hai dòng phiếu trả cùng SKU cùng nhánh thành **một** dòng chứng từ. Unique `(TenantId, ReferenceLineId, MovementType)` (`:240`) cũng tính theo dòng **chứng từ**. Vậy hai dòng `Restock` cùng SKU chỉ sinh **1** `CustomerReturn`, không phải 2. Tài liệu đang dùng ba cách gọi cho cùng một đơn vị: "dòng phiếu trả" (:117), "dòng" (:113, :663, :907), "dòng chứng từ" (:118, :871). Cách hỏng: QA viết TC-20/TC-45 theo câu đầu BR-012 sẽ kỳ vọng 2 bút toán, mâu thuẫn với TC-45 (ii). Hoặc BE gửi mỗi dòng phiếu trả thành một dòng chứng từ thì va unique `:166-168`, cả đợt NÉM. Lỗi này làm DB báo đỏ chứ không sai im lặng, nên xếp P2 | Dùng **một** đơn vị ở cả 4 chỗ: "đúng một bút toán mỗi `(chứng từ của đợt, dòng chứng từ = SKU, MovementType)`; mỗi dòng phiếu trả nhận > 0 thuộc đúng một dòng chứng từ của nhánh mình". Sửa câu đầu BR-012, Post-Condition, §8.1 #8 và cột kỳ vọng của TC-20 |
| 2 | P2 | §6.2: ghi chú (dòng 549), hàng `0003` (`Sales:0011`, `400`, dòng 555), `0004` (`400`, :556), `0014` (`400`, :566). §12: dòng 834 (`400`), 835 (`409`, gồm cả `Sales:0010` cho đơn `Cancelled`), 837 (`409`/`400`), 840, 848 | **`D-140` (1) mới áp được một nửa.** `D-140` yêu cầu "HTTP ghi theo code đang trả … BA đo, không ghi 409/400 suy đoán". Ba hàng `0001`/`0006`/`0011` đã đổi sang "theo code (chưa có ánh xạ HTTP riêng)". Các hàng còn lại cũng là mã thật `Sales:0010`/`Sales:0011` nhưng vẫn ghi `400`/`409`. Kết quả đo: grep `options.Map(` cho mã `Sales`/`PhieuTra`/`TraVuot` trên toàn repo = 0, và bản đồ mã ở `eCommerceHttpApiHostModule.cs:299` không có mã Sales nào. Chính repo ghi rằng mã không được map thì ABP trả **`403`** (`ReportingErrorCodes.cs:8-9`, bài học `INV-003` `0042`). Tức là API thật nhiều khả năng trả 403 cho các mã này, không phải 400/409. Cách hỏng: FE `admin-web` xử lý lỗi theo §6.2/§12 (400 = sửa form, 409 = tải lại) sẽ nhận 403 và hiện "không có quyền". Chưa gọi API thật để đo (xem "Không kiểm được") | Ghi số HTTP thật, cùng một cách ghi, cho **mọi** hàng mang mã `Sales:0010`/`0011` ở §6.2 và §12. Chọn một: ghi `403 (mặc định ABP, chưa map — đo bằng …)`, hoặc đưa việc thêm `options.Map` vào `S14b` (kèm TC). Không để `400`/`409` nằm cạnh "theo code" cho cùng một mã |
| 3 | P3 | BR-012, câu "Σ bút toán `CustomerReturn` của một SKU = Σ `ReceivedQuantity` luỹ kế của các dòng phiếu trả SKU đó" (dòng 117) | Câu thiếu phạm vi. "Của một SKU" không giới hạn trong phiếu trả này (các chứng từ `TH-{ReturnNumber}-*`), nên nếu tính trên cả sổ cái (mọi phiếu, mọi kho) thì đẳng thức sai | Thêm "trong các chứng từ của phiếu này (`TH-{ReturnNumber}-*`)" |
| 4 | P3 | §5.1 `LedgerEntryId` (dòng 381), so với §11 Migration 6 (dòng 811) | "Truy vết đủ mọi đợt qua `TH-{ReturnNumber}-*`" bỏ sót chứng từ kiểu cũ `TH-{ReturnNumber}` (không có `-`). Đó là chứng từ của phiếu đã nhận một đợt trước `D-137`, chính Migration 6 có nhắc | Ghi "`TH-{ReturnNumber}` (phiếu trước `D-137`) và `TH-{ReturnNumber}-*`" |
| 5 | P3 | §6.2 hàng `0004` (dòng 556) và `0014` (dòng 566), so với ghi chú dòng 549 | Ghi chú đặt luật: "mã thật ghi trong ô Mã (gạch nhãn); nhãn không gạch = chưa có khái niệm trong code (`S14b` khai mới — `0002`, `0005`, `0007`–`0010`, `0012`, `0013`)". Hai hàng `0004`/`0014` đã có mã thật `Sales:0010` (`OrderReturn.cs:196-199` thiếu lý do; `OrderReturnAppService.cs:90-95` disposition sai) nhưng **không gạch** nhãn, và cũng **không** nằm trong danh sách khai mới. Người làm `S14b` không biết có phải khai `Return:0004`/`0014` hay không | Gạch nhãn và ghi "→ `eCommerce:Sales:0010` (không khai `Return:0004`/`0014`)", theo đúng khuôn của hàng `0001`/`0003` |
| 6 | P3 | Mục đối chiếu, hàng `D-134`, cột "Đã sửa thành" (dòng 2705) | "Migration 2 bỏ đích" đã lỗi thời. Sau khi đánh số lại (P3 #6 vòng 2), Migration 2 là SQL thô `NULLS NOT DISTINCT` (dòng 807); không còn mục migration nào về kiểu số lượng | Ghi "§11: bỏ mục đổi kiểu `numeric(18,3)` (không còn migration riêng — `D-134`)" |
| 7 | P3 | `decisions.md:150`, `D-136` (1) | Tài liệu đã đính chính (§5.1 dòng 380, mục đối chiếu dòng 2712), nhưng `D-136` (1) vẫn ghi "`Σ RefundAmount (dòng) == OrdReturns.RefundedAmount` (BR-006)", và chưa có dòng `D-xx` nào đính chính (grep `D-14[1-9]`/"đính chính" = 0). Ai đọc `D-xx` trước tài liệu sẽ làm theo đẳng thức sai | Việc của **ORCH** (không phải BA): thêm một dòng `D-xx` đính chính `D-136` (1), hoặc ghi vào handover. Không chặn tài liệu |

## Không kiểm được

- D4: tài liệu không có khối `sql`/shell, và phần BA đổi không thêm SQL. CHECK nội tuyến `OrdRefunds` từ vòng 2 vẫn **chưa chạy** vì máy không có `psql`/`docker` (như vòng 2).
- #2: chưa gọi API thật để đo mã HTTP của `Sales:0010`/`0011`. Kết luận `403` dựa trên hai điều: grep thấy 0 `options.Map` cho mã Sales, và chú thích của chính repo (`ReportingErrorCodes.cs:8-9`). BA/QA nên đo bằng probe/E2E trước khi ghi số.
- Quan sát về `D-139` (5), **không** tính là phát hiện vì tài liệu đã có lối thoát "buộc thêm enum/cột ⇒ câu mới cho DEC". `D-139` (5) giao cho arch/BE `Inventory` chọn cách nhận biết chứng từ `-L` để sinh thêm `DamagedIn`, trong giới hạn "0 enum, 0 migration". Hiện `TinhButToanChungTu.cs:33-36` cho loại `CustomerReturn` chỉ sinh `CustomerReturn`, còn `StockDocumentType` (5 giá trị) và `StockDocumentMode` không có giá trị nào cho hàng lỗi. Như vậy dấu hiệu chỉ có thể là hậu tố số chứng từ, hoặc một method tự ghi riêng. Arch nên chốt điểm này trước khi viết code.
- Không chạy `dotnet test`. TC-45 là ca **kế hoạch** (tài liệu đã ghi "chờ code"); QA cần viết (vi) đỏ trước.
- Đây là vòng kiểm nhanh: phần không đổi từ v1.6 không đọc lại, kết luận vòng 2 cho phần đó vẫn giữ.

## Vòng trước

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

### Vòng 2 (21/11/2026, v1.6 — kết luận lúc đó DOC-CHANGES, 1 P1 · 3 P2 · 9 P3)

| # vòng 2 | Trạng thái | Mức lúc đó | Ghi chú kiểm vòng 3 |
|---|---|---|---|
| 1 | đã đóng — theo `D-139`: Basic Flow 9, BR-012, §0, §3.2, §4 (sơ đồ :289, bảng :314), §5.1 :381, §7.3 :633, §7.4 :644, §8.1 #8, §11 Migration 4/6, F6/F17, Q16, TC-34 (vi), TC-41 (v) và TC-45 mới (i)–(vi) đều khớp `D-139` ⑥. `D-132` (5) giữ nguyên (`GanChungTuKho` chỉ nhận `Restock`, guard riêng cho `-L`). Hai lời gọi cổng nay khác số, nên nhánh idempotent `CustomerReturnGateway.cs:45-54` không còn nuốt lời gọi. UoW là transactional (POST, `TransactionBehavior.Auto` `:94`; gateway `autoSave` trong cùng giao dịch). Phần đơn vị đếm bút toán còn sót → #1 | P1 |
| 2 | đã đóng phần mã — theo `D-140`: §3 :186, E1/E6/E11, A3, §3.1 #1, §6.2 `0001`/`0006`/`0011`, §8.1 #1/#11, §8.3 #10, §12, TC-06/TC-21 dùng `Sales:0010` + `lyDo`; khớp `OrderReturn.cs:172-213`, `:286-293`, `OrderReturnService.cs:95-98`, `S14-E8`/`S14-E19`. Phần HTTP chưa đo → #2; hàng `0004`/`0014` → #5 | P2 |
| 3 | đã đóng — §3.3 #8, §5.2 :470, §6.2 `0006`, §8.3 #10 không còn "tạo phiếu mới". Các chỗ `phiếu mới` còn lại (grep) đều đúng ngữ cảnh: sau `Cancelled`, "0 phiếu mới", ghi chú lỗi thời §29/§34.5 | P2 |
| 4 | đã đóng ở tài liệu — §5.1 :380 ghi `RefundedAmount ≤ Σ RefundAmount`; `D-136` (1) chưa có dòng đính chính → #7 (việc của ORCH) | P2 |
| 5 | đã đóng — BR-005 ghi "`r0 > 0` chỉ khi Q17 mở"; TC-40 (iii)(iv)(xi) là hàm thuần; (viii) "trong một phiếu"; §15.3 #10 | P3 |
| 6 | đã đóng — Migration đánh số 1–6, hàng tóm tắt ghi "+4 cột"; còn một chỗ dẫn cũ ở mục đối chiếu → #6 | P3 |
| 7 | đã đóng — §5.1 :420, :456-459 | P3 |
| 8 | đã đóng — ADR-11 ghi comment `HuyAsync` là sai (F16); §34.5 #3 có ghi chú lỗi thời | P3 |
| 9 | đã đóng — A3, §3.1 #1, §12 :835 dùng `Sales:0010` cho đơn `Cancelled` | P3 |
| 10 | đã đóng — BR-012, Basic Flow 9, §3.2 :231, §12 :837, TC-41 (viii) dùng `Sales:0010` | P3 |
| 11 | đã đóng — §0 :48 dẫn `:222`, `:237` (grep khớp) | P3 |
| 12 | đã đóng — §23.2/§25.5/§26.2 dùng danh sách Markdown | P3 |
| 13 | đã đóng — NFR-005 (§1 :118, §13 :871): kho kiểm bằng TC-20, hoàn tiền bằng TC-17/NFR-007 | P3 |

### Vòng 1 (v1.3 — trạng thái chốt ở vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng ở vòng 2 — ADR-11/§3.1 #4/#5/§8.3/§12/TC-42 theo `D-135`; di chứng → vòng 2 #3, #5, #8 (đã đóng ở vòng 3) | P1 |
| 2 | đã đóng ở vòng 2 — BR-005 `P(x,k)` + `D-136`/`D-138`; di chứng → vòng 2 #4, #5 (đã đóng ở vòng 3) | P1 |
| 3 | đã đóng ở vòng 2 — `D-137`; lỗi mới đợt trộn → vòng 2 #1 (đã đóng ở vòng 3 theo `D-139`) | P1 |
| 4 | đóng ở vòng 3 phần mã (`D-140`); phần HTTP còn → #2 | P2 |
| 5 | đã đóng ở vòng 2 | P2 |
| 6 | đã đóng ở vòng 2 | P2 |
| 7 | đã đóng ở vòng 2 | P2 |
| 8 | đã đóng ở vòng 2 | P2 |
| 9 | đã đóng ở vòng 2 | P2 |
| 10 | đã đóng ở vòng 2 | P2 |
| 11 | đã đóng ở vòng 2; lệch số cột → vòng 2 #6 (đã đóng ở vòng 3) | P2 |
| 12 | đã đóng ở vòng 2 | P2 |
| 13 | đã đóng ở vòng 2 | P2 |
| 14 | đã đóng ở vòng 2 | P3 |
| 15 | đã đóng ở vòng 2 | P3 |
| 16 | đã đóng ở vòng 2 | P3 |
| 17 | đã đóng ở vòng 2 | P3 |
| 18 | đã đóng ở vòng 2 | P3 |
| 19 | đã đóng ở vòng 2 | P3 |
| 20 | đã đóng ở vòng 2 | P3 |
| 21 | đã đóng ở vòng 2 | P3 |
| 22 | đã đóng ở vòng 2 | P3 |
| 23 | đã đóng ở vòng 2 | P3 |
| 24 | đã đóng ở vòng 2 | P3 |
