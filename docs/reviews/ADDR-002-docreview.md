# Doc review — ADDR-002 Đồng bộ mã địa chỉ theo từng hãng vận chuyển

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` |
| Ngày · vòng | 18/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–966 (toàn văn; đọc theo đoạn 1-110, 111-170, 171-280, 280-379, 380-469, 470-559, 560-649, 650-708, 709-808, 809-966) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi; 16/16 heading, 16/16 BR có test, mục đối chiếu ở dòng 939, 9 câu `Open`, 0 TODO) |

0 P1, 2 P2, 14 P3. Cả 4 P1 của vòng 1 đã đóng (thân tài liệu đã sửa theo `D-87`, `D-88`, `D-89`, phạm vi `D-90`). Hai P2 còn lại là chỗ sửa sót, mỗi chỗ một dòng (#1, #2). P3 không chặn duyệt.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Với mỗi thứ bị cấm/bỏ theo `D-83`…`D-92` (phạm vi dòng lô "đã vào tới `LapKeHoach`", `TraMaHangAsync` cho vận đơn, "0 việc", `SourceVersion`/`PayloadHash`/`RowsWritten`/`DriftCount`/"1.000"/"95 %"/`AwaitingReview`/`0509`/`0510`, "giả định tạm" tenant): grep trên toàn tài liệu, đọc từng chỗ trúng (các chỗ dòng 75, 83, 99, 539-540, 635, 952-963 đều là gạch bỏ/lịch sử). So §1 NFR (dòng 132) với §13 (645-654): khớp. So Post-condition/§3.1/E9/TC-07/TC-29 với §5.2 (dòng 298). So §6.1 (379, 389) với §12 (619) và §9 (551) | 4 phát hiện (#1, #2, #6, #13) |
| D2 chéo tài liệu / code / D-xx | Đọc nguyên văn `decisions.md` dòng 101-106 (`D-87`…`D-92`), đi từng mục "BA sửa cùng PR" của mỗi dòng và tìm mục đó trong tài liệu. Đọc code `CarrierAddressMappingSynchronizer.cs:95-199` (thứ tự kiểm `:107` hãng → `:121-126` quét → `:129` hàng rào 1; `:172,:178-179` đếm lặp đúng như F8) và `CarrierAddressSyncWorker.cs:96-193` (`:124` acquire ngoài `try`, `:146-152` return sớm khi 0 việc, `:158` `autoSave: true`, `:181-191` hai `catch`). Host: grep `KeyPrefix\|AbortOnConnectFail\|ConventionalControllers` trong `eCommerceHttpApiHostModule.cs` (`:73,222,237,251,382,412`); `eCommerceMetrics.cs` (`All` `:81`, `Forbidden` `:219`) | 5 phát hiện (#7, #8, #10, #11, #12) |
| D3 cơ chế thực thi | BR mới hoặc đã sửa (BR-002/003/004/005/012/013/014) và bất biến §8.1 #4/#7/#11/#12: mỗi cái có TC có thể đỏ ở cột Phủ §14.1 (TC-03, TC-07, TC-10 4a/4b, TC-25…TC-29), unique bảng con (§5.2 dòng 356) chặn được "đúng 1 dòng con mỗi việc". "Kể cả 0 việc ⇒ 1 dòng `DaApDung`": TC-07 đỏ được, nhưng tài liệu không chỉ ra `return` sớm ở `:146-152` | 1 phát hiện (#9) |
| D4 SQL chạy được | Grep rào code: 2 khối `mermaid`, 2 `json`, 1 `csharp`, 1 khối log không ngôn ngữ, **không có khối `sql`/`powershell`/`bash`**. Khối `csharp` (702-744) không đổi so với vòng 1 | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | Grep `[Fact]` trong `CarrierAddressMappingSynchronizerTests.cs`: 14 ca, `[Fact]` ở 45…219, tên method ở 46…220 (các dẫn chiếu `:46-53`, `:58`, `:74`, `:133,141,155,163`, `:162,179`/`:163,180` đều trỏ đúng ca). `PostgreSqlSchemaTests.cs:208` (assert `93`) và `:240` (tên index) có thật. Grep `AddrCarrierMappings` trong `test/`: không có ca hành vi chèn trùng cho TC-11; thấy `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt:26,55` (đọc bởi `RangBuocDbChuaAnhXaTests.cs:29`). `design-doc-guidelines.md` "Quy tắc vàng" ở `:138`. `kiem-link-tai-lieu.ps1` → PASS, 0 tham chiếu chết (725 link, 754 đường dẫn backtick) | 4 phát hiện (#10, #12, #14, #15) |
| D6 nhãn tin cậy | Mọi ✅/🔶 ở §0.2, §8.1, §13, §14.3: nhãn §8.1 #4/NFR-002 đã hạ 🔶 (đóng #5 vòng 1); NFR-001 ghi 🔶 "chưa đo"; §8.2 #6/§12 "Redis chết" vẫn chưa gắn nhãn | 1 phát hiện (#11) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14: NFR có ngưỡng (§13); guard sai thì làm gì (§3.1 có cột, R5b có thứ tự); chống trùng biên vào (§7.2, §8.2 bảng idempotency đã viết lại); tên khoá (§8.2, D-92 không `{tenantId}`); tiền không áp dụng (BR-016); không hard-code endpoint (§7.3); đếm ≠ dòng hiển thị (§10 dòng 587, thêm lý do `TotalCount` < `SoCanNguoiAnhXa`). Mermaid: **không chạy được** `npm --prefix tools run check:mermaid` (hook vai chặn `npm`); kiểm tay 2 khối (138-180, 235-262): không `;` trong text (chỉ ở `classDef`, như vòng 1 đã PASS), không `<...>` placeholder, chỉ thẻ `<b>`/`<br/>`. Mục "Đối chiếu mô hình Nhanh" (939-966): có hàng `D-87`…`D-92`, thân đã sửa theo (trừ #1, #2) | 3 phát hiện (#3, #4, #5) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §12 hàng "Mã hãng chưa có ánh xạ khi tạo vận đơn" (dòng 619) | Hàng này vẫn ghi `TraMaHangAsync` là cửa NÉM `0501` "khi tạo vận đơn". Điều đó trái §6.1 dòng 379 ("**Không** dành cho `SHP-001`/`SHP-004` tạo vận đơn") và `D-89` (5). Hậu quả: dev `SHP-*` đọc bảng lỗi §12 sẽ gọi chiều xuôi; chiều này cần mã hãng, thứ SHP không có, nên họ dễ tự query bảng. Đó đúng là đường "chọn bừa" mà `D-89` đóng | Đổi thành `ResolveCarrierAddressCodeAsync` NÉM `0501` (0 ứng viên) / `0502` / `AnhXaNguocKhongDuyNhat` (R5b). Hoặc tách thành hai hàng: chiều xuôi (webhook/CSKH) và chiều ngược (vận đơn) |
| 2 | P2 | §5.2 tiêu đề bảng `AddrCarrierSyncRuns` (dòng 298) | Câu mô tả bảng vẫn là "một dòng cho mỗi lượt … **đã vào tới `LapKeHoach`**". Câu này trái `D-90`, §0 dòng 54, Post-condition dòng 127, §3.1 dòng 206/208, E9 và TC-29 (mọi lượt **đã giành khoá**, kể cả lỗi adapter trước `LapKeHoach`). Đây là phần sót của #9 vòng 1. Nếu BE dựng entity theo từ điển dữ liệu §5.2 thì sẽ ghi dòng sau `LapKeHoach`, và ca lỗi hay gặp nhất (hãng timeout) không có dòng | Sửa câu thành "một dòng cho mỗi lượt của mỗi hãng **đã giành được khoá** (`D-90`)" |
| 3 | P3 | §4.1 sequence (dòng 246-259) | Sau `D-90`, sơ đồ chỉ có hai nhánh `BiChan`/`DaApDung`. Không có nhánh lỗi adapter/DB, tức nhánh ghi 1 dòng `Loi` trong UoW riêng | Thêm một `else «lỗi không mong đợi»` ghi «1 dòng Loi, ErrorType, UoW riêng — kế hoạch WP-15» |
| 4 | P3 | §1 BR-015 (dòng 131) | Có dấu backtick thừa: `` `D-79`` `` (hai dấu đóng). Inline code sẽ lệch tới cuối dòng | Bỏ một dấu backtick |
| 5 | P3 | Header dòng 24 ↔ §15.4 dòng 832 | Header ghi `v0.3`, lịch sử đã có `v0.4` (chỗ còn lại của #14 vòng 1) | Đổi header thành `v0.4 — 18/11/2026` |
| 6 | P3 | §1 BR-003 (dòng 131) | "Mỗi lượt đồng bộ của mỗi hãng để lại đúng một dòng" không giới hạn "đã giành khoá", trong khi Post-condition và TC-07/TC-29 đòi 0 dòng khi bận khoá hoặc chưa có adapter | Thêm "đã giành được khoá (`D-90`); bận khoá/chưa có adapter ⇒ 0 dòng" |
| 7 | P3 | §6.1 dòng 372 · §9 bảng dòng 551 | §6.1 còn ghi "3 method còn lại giữ `[Authorize]` cấp class". Hàng §9 chỉ liệt kê `TraMaHangAsync`, `LayChuaXacMinhAsync`, `PhatHienSaiLechAsync`, thiếu 2 method đọc mới `GetUnmappedCodesAsync`/`ResolveCarrierAddressCodeAsync` (chỉ có ở ghi chú 569, kèm `Both`) | Thêm 2 method vào hàng §9 (⬜ WP-15, `MultiTenancySides.Both`); sửa "3 method" thành "các method đọc" |
| 8 | P3 | E1 dòng 130 ("bỏ `0509`"), Pre-condition dòng 126 và E2 dòng 130 ("bỏ mã `0510`") ↔ §0.2 dòng 110, §6.1 dòng 439-440 | "Bỏ `0509`/`0510`" là số kế hoạch cũ của v0.2. Nay `0509`/`0510` lại là số **dự kiến** cho hằng `D-84` và `AnhXaNguocKhongDuyNhat` (`D-89`). Người đọc dễ hiểu "0510 đã bị cấm dùng" | Ghi "số kế hoạch v0.2 `0509`/`0510` (không cấp) — không liên quan số dự kiến của `D-84`/`D-89`" |
| 9 | P3 | §1 Basic Flow bước 8, §3.1 hàng `DaApDung`, §12 "Chiến lược transaction" | Tài liệu đòi lượt 0 việc vẫn ghi 1 dòng `DaApDung`, nhưng không nêu rằng worker đang `return` sớm khi `!keHoach.CoViec` (`CarrierAddressSyncWorker.cs:146-152`, nằm **trước** `UpdateManyAsync`). Nếu BE chỉ thêm `InsertAsync` cạnh `:158` thì lượt 0 việc mất dòng (TC-07 sẽ đỏ, nên lỗi lộ ra chứ không im lặng) | Thêm vào §12/bước 8: "bỏ `return` sớm ở `:146-152`, ghi dòng lô cả khi 0 việc" |
| 10 | P3 | §11 hàng "Cập nhật test" (dòng 598) | Chỉ nêu `PostgreSqlSchemaTests.cs:208/:240`. Ràng buộc mới (unique bảng con, CHECK `Outcome`, FK `SyncRunId`) còn chạm `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`, tệp do `RangBuocDbChuaAnhXaTests.cs` đọc (đã có 2 dòng của `AddrCarrierMappings` ở `:26,55`) | Thêm dòng: cập nhật `RangBuocChuaAnhXa.txt` hoặc map mã nghiệp vụ cho ràng buộc mới |
| 11 | P3 | §8.2 #6 (dòng 520), §12 "Redis chết" (dòng 629) | (#17 + #20 vòng 1, còn) Hai chỗ này dẫn `eCommerceHttpApiHostModule.cs:137-145`, trong khi `AbortOnConnectFail` thật ở `:251`. Câu "khoá vô hiệu ⇒ hàng rào cuối là unique index" chưa đo. `TryAcquireAsync` ở `:124` nằm ngoài `try`, nên Redis lỗi sẽ ném ra khỏi `DongBoMotHangAsync` và bỏ cả các hãng sau trong vòng `foreach` (`:96-113`) | Sửa số dòng `:251`; gắn 🔶 "chưa đo"; mô tả theo code: ném ở `:124`, 0 dòng lô, lượt hỏng |
| 12 | P3 | §8.2 ghi chú (dòng 529), §15.2 F1 (dòng 803) | (#17 vòng 1, còn) `KeyPrefix` dẫn `:127`/`:115-128`; thật ở `:222`, `:237` | Cập nhật số dòng |
| 13 | P3 | §11 dòng 600-601 ↔ §14.3 dòng 753 | (#16 vòng 1, còn) §11 vẫn hướng dẫn "Thêm `builder.ConfigureAddress()`" và "Thêm assembly module Address", trong khi AC dòng 753 ghi ✅ đã có (`eCommerceDbContext.cs:352`, Host `:73`) | Đổi hai hàng thành "✅ đã có — không thêm lại" |
| 14 | P3 | §13 ghi chú metric (dòng 656-660), §15.2 F3 (dòng 805) | (#18 vòng 1, còn) Dẫn `eCommerceMetrics.cs:13-53` "12 metric" và `:81-85` `Forbidden` (thật: `All` `:81`, `Forbidden` `:219`). F3 vẫn nói `shared/eCommerce.Kernel` chưa tồn tại mà không gắn "LỖI THỜI" | Sửa số dòng, bỏ "12 metric" hoặc đo lại; gắn F3 "LỖI THỜI (18/11/2026)" |
| 15 | P3 | §7.2 dòng 476, §14.3 dòng 761, mục đối chiếu hàng `D-80` (dòng 947) | (#21 vòng 1, còn) Dẫn `design-doc-guidelines.md:102`, nhưng "Quy tắc vàng" thật ở `:138`. Hàng `D-80` dẫn `:291`/`:620`/`:819` của bản cũ | Cập nhật số dòng hoặc dẫn theo §mục |
| 16 | P3 | §0 Phạm vi dòng 51 | (#23 vòng 1, còn) "✅ Bảng đăng ký nguồn mã theo hãng (… cấp địa chỉ hãng hỗ trợ)", trong khi §0.2 dòng 109 ghi registry ⬜ và E7 đã bỏ danh mục cấp hỗ trợ | Đổi thành "cấu hình `Address:CarrierSync:Carriers` + adapter; registry ⬜" |
| 17 | P3 | §14.1 TC-11 (dòng 680) | (#24 vòng 1, còn) TC-11 (chèn trùng `(NULL,'GHN','P1')` ⇒ `UniqueViolation`) không gắn ⬜. Grep `AddrCarrierMappings` trong `test/` chỉ thấy tên index ở `PostgreSqlSchemaTests.cs:240` và `schema.sql`, không có ca hành vi. `D-92` ⑥ dựa vào TC-11 như thể "có sẵn" | Gắn ⬜ cho TC-11 (index có thật, ca chèn trùng chưa có) |

## Không kiểm được

- D4: tài liệu không có khối `sql`/`powershell`/`bash`, nên không có gì để chạy. Hook vai chặn `docker`, nên không dò được PostgreSQL.
- `npm --prefix tools run check:mermaid`: hook vai chặn `npm` (`guard-shell.mjs`: shell chỉ cho đọc + psql-rollback). Đã kiểm tay 2 khối Mermaid. **ORCH cần tự chạy** lệnh này.
- `TC-25` ca (4) (10 ánh xạ, 8 đã `NghiNgo`, 2 vắng mặt): ca chỉ ra kết quả đúng nếu danh mục còn ≥ 5 mã. Hàng rào 2 `0507` (`Synchronizer.cs:137`) chạy trước hàng rào 3. Tài liệu không nêu kích thước danh mục của ca; QA tự chọn ở WP-15, không ghi thành phát hiện.
- Mã HTTP của `05xx`: tài liệu vẫn ghi "chưa xác minh" (như vòng 1), không kết luận.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — §1 NFR (dòng 132) khớp §13; không còn lô 1.000/95 %/`RowsFetched` | P1 |
| 2 | đã đóng — `D-89`: §0 dòng 57, UC7/UC8, R5b, §6.1 `ResolveCarrierAddressCodeAsync`, §8.1 #11, TC-27; còn sót §12 dòng 619 ⇒ chép lên #1 | P1 |
| 3 | đã đóng — `D-87`: R4, NFR-005, §8.1 #7/#12, bảng mã `0508`, §18, TC-25, F8 | P1 |
| 4 | đã đóng — `D-88`: bảng con + unique, `GetUnmappedCodesAsync`, §10, §11, NFR-003, TC-26 | P1 |
| 5 | đã đóng — "0 thay đổi ánh xạ", §8.1 #4 hạ 🔶, TC-03 hai loại danh mục | P2 |
| 6 | đã đóng — §8.2 bảng idempotency viết lại (dòng 539-541) | P2 |
| 7 | đã đóng — §7.2 hàng `SHP-*` không còn `Drift` (dòng 473) | P2 |
| 8 | đã đóng — TC-01 theo `Outcome`/`So*` (dòng 670) | P2 |
| 9 | còn một phần — `D-90` áp ở §0/§1/§3.1/E9/TC-07/TC-29; §5.2 dòng 298 còn câu cũ ⇒ chép lên #2 | P2 |
| 10 | đã đóng — UoW `Begin(requiresNew: true, isTransactional: true)` ở BR-014, §3.1, §12, TC-07 | P2 |
| 11 | đã đóng — `D-92` thống nhất §5.2/§8.2/§9/Q3/§15.3 #5/NFR-006/TC-19 | P2 |
| 12 | đã đóng — §14.3 dòng 748 nêu NFR-001/005/007 → TC-28/TC-25/TC-28; cột Phủ đủ 10 NFR | P2 |
| 13 | đã đóng — §15.2 F9 ghi nợ `ShpCarrierAddressMappings`, có chủ | P2 |
| 14 | còn (header `v0.3`, lịch sử `v0.4`) ⇒ chép lên #5 | P3 |
| 15 | đã đóng — §0.1 D8 dẫn `:208` + `:240` | P3 |
| 16 | còn ⇒ chép lên #13 | P3 |
| 17 | còn ⇒ chép lên #11, #12 | P3 |
| 18 | còn ⇒ chép lên #14 | P3 |
| 19 | đã đóng — `D-91`: BR-013, E3, §4.2, bảng mã, TC-10 (4a)/(4b) | P3 |
| 20 | còn ⇒ chép lên #11 | P3 |
| 21 | còn ⇒ chép lên #15 | P3 |
| 22 | đã đóng — Q4 viết lại, Actor/Trigger (d) sửa | P3 |
| 23 | còn ⇒ chép lên #16 | P3 |
| 24 | còn ⇒ chép lên #17 | P3 |
