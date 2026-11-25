# Doc review — ADDR-002 Đồng bộ mã địa chỉ theo từng hãng vận chuyển

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` |
| Ngày · vòng | 26/11/2026 · **kiểm nhanh bổ sung K1** (sau kiểm nhanh v0.5 cùng ngày; phiên N76, WP-18) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc (kiểm nhanh bổ sung — KHÔNG đọc lại toàn văn) | mục BA đã đổi: §1 Post-Condition(s) dòng 127 · §3.2 hàng R5 dòng 222 · §15.4 hàng `v0.6` dòng 835; đối chiếu §6.1 dòng 379 · §12 dòng 619–620 · R5b dòng 223 · §14.1 TC-06/TC-08/TC-16/TC-27 (dòng 676–697) · §0.2 dòng 105 · §15.2 F7/F10 (dòng 810, 813) · mục Đối chiếu dòng 962; **chưa đọc lại** §2 (138–193), §16 cuối + §17 + §18 (846–940) — chỉ rà bằng grep |
| Grep toàn tài liệu (970 dòng) | `vận đơn` (27 chỗ, đọc từng chỗ trúng) · `TraMaHangAsync`/`Tra`/`ThuTra`/`TraNguoc`/`ResolveCarrierAddressCodeAsync` |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (giữ kết quả kiểm nhanh v0.5, thân không đổi cấu trúc 16 mục/BR) · `check-agent-artifact.ps1 -Kind docreview` → exit 0 |
| Graph | `graph: codegraph_explore "CarrierAddressMappingResolver Tra TraNguoc ResolveCarrierAddressCodeAsync CarrierAddressMappingAppService TraMaHangAsync"` → `Tra` thật ở `CarrierAddressMappingResolver.cs:38`, NÉM `0502` tại `:80-87` (đúng như R5 dẫn `:80-87`); `TraMaHangAsync` (`CarrierAddressMappingAppService.cs:63`) gọi `Tra`; `TraNguoc`/`ResolveCarrierAddressCodeAsync` **không** có trong index (đúng ⬜ WP-15). Khớp `D-89` (`decisions.md:103`): chiều **xuôi** `TraMaHangAsync` giữ cho webhook/đối soát/CSKH; chiều **ngược** `ResolveCarrierAddressCodeAsync`/`TraNguoc` cho `SHP-*` tạo vận đơn |

0 P1, 0 P2 mở, 2 P3 còn (K2/K3 — BA cố ý để người duyệt). **K1 (P2 kiểm nhanh v0.5): ĐÃ ĐÓNG.** §3.2 hàng R5 (dòng 222) và §1 Post-Condition (dòng 127) đã thôi gán chiều **xuôi** `Tra` cho tạo vận đơn — tạo vận đơn chỉ đi chiều **ngược** `ResolveCarrierAddressCodeAsync`/`TraNguoc` (R5b, `D-89`). Khớp `D-89`, §6.1 (379), §12 (619–620), R5b (223); không sinh mâu thuẫn D1 mới với TC-06/TC-08/TC-16/TC-27/§14.1. Grep 27 chỗ `vận đơn`: **không** còn câu sống nào gán `Tra`/`TraMaHangAsync` cho tạo vận đơn.

## Đã kiểm

| Nhóm | Cách kiểm (kiểm nhanh v0.5 + bổ sung N76) | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Với từng ràng buộc **đã sửa** ở v0.5/N76: (a) "`Tra`/`TraMaHangAsync` **không** dùng cho tạo vận đơn" — grep `vận đơn` (27 chỗ) + `TraMaHangAsync`/`Tra`, đọc từng chỗ trúng; (b) "dòng lô cho mọi lượt **đã giành khoá**" — grep `LapKeHoach` (13 chỗ), `giành`, `đã vào tới` (3 chỗ); (c) "Q12 = không có hãng nào" — grep `khoảng trắng`, `nguyên văn`. Đọc đối chiếu §0 (29–115), §1 (116–132), §3.2 (212–228), §5.2 (279–362), §6.1 (364–450), §12 (610–640), §14.1 (665–700), §15 (776–839), mục Đối chiếu (942–971) | **K1 ĐÃ ĐÓNG** (N76: R5 + Post-Condition thôi gán `Tra` xuôi cho vận đơn, khớp `D-89`/§6.1/§12/R5b); BR-003 không giới hạn "đã giành khoá" (**K2**, P3 còn từ vòng 2 #6) |
| D2 chéo tài liệu / code / `D-xx` | Đọc `docs/nhanh/decisions.md:103` (`D-89`) + dòng 104–108 (`D-90`…`D-94`): `D-89` ①(1)(4)(5) = chiều xuôi `TraMaHangAsync` giữ webhook/CSKH, chiều ngược `ResolveCarrierAddressCodeAsync` cho `SHP-*` ⇒ khớp §3.2 R5/§1 Post-Condition/§6.1/§12. Graph xác nhận `Tra` xuôi thật + `TraNguoc` chưa có (⬜ WP-15). `D-90`/`D-94` khớp Post-condition/§5.2 | 0 phát hiện về `D-xx`; K1 (mâu thuẫn nội bộ) **ĐÃ ĐÓNG** ở N76 |
| D3 cơ chế thực thi | Không thêm BR mới (chỉ sửa câu chữ R5/Post-Condition/§15.4): §3.2 R5 dẫn đúng `Tra` thật (`CarrierAddressMappingResolver.cs:80-87`); R5b/TC-27 phủ chiều ngược; TC-16 phủ chiều xuôi ⇒ mỗi nhánh có ca có thể đỏ | 0 phát hiện |
| D4 SQL chạy được | Grep rào ngôn ngữ: 2 `mermaid`, 2 `json`, 1 `csharp`, 1 khối log; **không có** khối `sql`/`powershell`/`bash` ⇒ không có gì để chạy | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | Tên test/đường dẫn trong mục đã đổi: TC-16/TC-27 (TC-27 ghi rõ ⬜ WP-15, `test/eCommerce.Tests/AddressCarrier/`); `CarrierAddressMappingResolver.cs:38`/`:80-87` đối chiếu graph đúng `Tra`; không có tuyên bố "đã có test" mới ở mục đã đổi | 0 phát hiện |
| D6 nhãn tin cậy | Câu đã sửa không nâng nhãn tin cậy: `ResolveCarrierAddressCodeAsync`/`TraNguoc`/`AnhXaNguocKhongDuyNhat` giữ ⬜ WP-15; `Tra` giữ ✅ (code thật); §15.4 `v0.6` là biên bản sửa, không phải số đo | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | Mục "Đối chiếu mô hình Nhanh (18/11/2026)" (942–971) khớp thân; hàng `D-89` (962) phản ánh đúng hai chiều; 16/16 BR có test (máy). Đếm ≠ dòng hiển thị (§10 dòng 587) giữ lý do `TotalCount` < `SoCanNguoiAnhXa`. Mermaid không đổi ở N76; **không chạy được** `npm --prefix tools run check:mermaid` (hook vai chặn `npm`) — kiểm tay 2 khối như vòng trước, không thấy dấu hiệu mới | 1 phát hiện P3 còn (K3 — thứ tự hàng lịch sử §15.4) |

## Vòng kiểm nhanh bổ sung sau sửa K1 (N76)

ORCH chỉ đạo: kiểm nhanh **CHỈ** 3 mục BA đã đổi (`changedSections`): §1 Post-Condition(s) · §3.2 hàng R5 · §15.4 hàng `v0.6`. Không đọc lại toàn văn. Mục đích: đóng P2 **K1**.

| Mục theo lệnh ORCH | Kết quả |
|---|---|
| 1. Hai câu đã sửa hết mâu thuẫn với `D-89`, §6.1 (379), §12 (619–620), R5b (223) | **ĐẠT.** §3.2 R5 (222) = "tra **xuôi** … webhook/đối soát/CSKH; … tạo vận đơn **không** đi đường này, dùng chiều **ngược** R5b (`D-89`)". §1 Post-Condition (127) = "chiều **ngược** `ResolveCarrierAddressCodeAsync` NÉM `0502` (R5b, `D-89`); chiều **xuôi** `Tra` cũng NÉM `0502` khi tra mã nhưng chỉ phục vụ webhook/đối soát/CSKH, **không** cho tạo vận đơn". Khớp `D-89` (① (1)(4)(5)), §6.1 (379 "Không dành cho `SHP-001`/`SHP-004` tạo vận đơn"), §12 (619 chiều ngược cho tạo vận đơn; 620 chiều xuôi webhook/CSKH "Không dành cho tạo vận đơn"), R5b (223) |
| 2. Không tạo mâu thuẫn D1 mới với TC-06/TC-08/TC-16/TC-27/§14.1 | **ĐẠT.** TC-06 (676)/TC-08 (678) vẫn cho `Tra` (xuôi) NÉM `0502` khi `ChuaXacMinh`/`NghiNgo` — đúng vế "chiều xuôi `Tra` cũng NÉM `0502`" ở Post-Condition; TC-16 (686) `TraMaHangAsync` mã chưa có ⇒ `0501`, không gán vận đơn; TC-27 (697) `TraNguoc`/`ResolveCarrierAddressCodeAsync` phục vụ tạo vận đơn (chiều ngược). Không câu nào đòi `Tra` xuôi phục vụ tạo vận đơn |
| 3. Grep `vận đơn` (27 chỗ) + `cho vận đơn`: **không** còn câu sống gán chiều xuôi `Tra`/`TraMaHangAsync` cho tạo vận đơn | **ĐẠT.** Mọi chỗ trúng thuộc một trong: (a) giữ `Tra` xuôi cho webhook/đối soát/CSKH (57, 379, 620); (b) giao tạo vận đơn cho chiều ngược (106, 796, 848, 967); (c) nói về vận đơn **không** nêu method (65, 66, 210, 504, 519, 547, 626, 869, 901, 927, 932, 954); (d) lịch sử/§0.2/§15.2/Đối chiếu (74, 76, 77, 105, 810, 813, 833, 835). Riêng dòng 105 (§0.2) nêu "vận đơn phải fail-closed" làm **lý do** đổi thiết kế v0.1 — **không** gán `Tra` xuôi "cho tạo vận đơn" ⇒ không phải câu gán chiều sai |
| 4. §15.4 thêm hàng `v0.6` đúng | **ĐẠT.** Dòng 835 mô tả đúng việc sửa K1 (R5 + Post-Condition thôi gán chiều xuôi cho tạo vận đơn), "không đổi trạng thái duyệt, P3 K2/K3 để người duyệt". K3 (thứ tự `v0.5` trước `v0.4`) **còn** đúng như BA cố ý để nguyên; hàng `v0.6` nằm sau `v0.4` |
| 5. Không có P1/P2 mới; 0 P1, 0 P2 mở | **ĐẠT.** 0 P1; **0 P2 mở** (K1 đã đóng); P3 còn K2, K3 |

## Phát hiện (còn mở — sau kiểm nhanh bổ sung N76)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| K2 | P3 | §1 BR-003 (dòng 131) | BR-ADDR-002-003 vẫn là "**Mỗi lượt đồng bộ của mỗi hãng để lại đúng một dòng `CarrierAddressSyncRun`**" — không có điều kiện "đã giành được khoá", nên đọc literal thì lượt bận khoá (0 dòng, `D-90`) là vi phạm BR. Post-condition/§5.2/TC-07/TC-29 đã đúng ⇒ hiểu nhầm cục bộ, lỗi sẽ lộ nếu ai code theo BR (TC-07 đỏ). Còn từ vòng 2 #6 (BA ghi "P3 còn lại để vòng sau") | Thêm vào BR-003: "đã giành được khoá (`D-90`); bận khoá/chưa có adapter ⇒ 0 dòng" |
| K3 | P3 | §15.4 Lịch sử thay đổi (dòng 833–834) | Hàng `v0.5` nằm **trước** hàng `v0.4` (bảng đang là `v0.1` → `v0.2` → `v0.3` → **`v0.5`** → `v0.4` → `v0.6`), người đọc dễ hiểu `v0.4` mới hơn `v0.5` | Đưa hàng `v0.4` lên trước hàng `v0.5` |

## Không kiểm được

- **§2 (dòng 138–193)** và **§16 cuối + §17 + §18 (846–940)**: kiểm nhanh không đọc lại; chỉ rà bằng grep (`vận đơn`, `Tra`, `LapKeHoach`, `giành`) — các chỗ trúng là câu chung ("không dùng cho vận đơn mới", "mã biến mất ⇒ không dùng cho vận đơn mới"), **không** nêu method ⇒ không thấy mâu thuẫn, nhưng **không** khẳng định đã đọc.
- D4: tài liệu không có khối `sql`/`powershell`/`bash` ⇒ không có gì để chạy; hook vai chặn `docker`.
- `npm --prefix tools run check:mermaid`: hook vai chặn `npm` (`guard-shell.mjs`). Hai khối Mermaid không đổi ở N76. **ORCH cần tự chạy.**
- Mã HTTP của `05xx`: tài liệu vẫn ghi "chưa xác minh" — không kết luận.

## Vòng trước

### K1 (P2 kiểm nhanh v0.5) → kiểm nhanh bổ sung N76

| # | Trạng thái | Mức lúc đó |
|---|---|---|
| K1 | **ĐÃ ĐÓNG** — §3.2 R5 (222) và §1 Post-Condition (127) thôi gán `Tra` xuôi cho tạo vận đơn; tạo vận đơn chỉ đi chiều ngược `ResolveCarrierAddressCodeAsync`/`TraNguoc` (R5b, `D-89`). Khớp `D-89`/§6.1/§12; không mâu thuẫn D1 mới với TC-06/TC-08/TC-16/TC-27 | P2 |

### Vòng 2 → kiểm nhanh v0.5 (17 phát hiện của `docs/reviews/ADDR-002-docreview.md` vòng 2, 18/11/2026)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | **ĐÃ ĐÓNG** — §12 (619–620) tách hai hàng: chiều ngược `ResolveCarrierAddressCodeAsync` NÉM `0501`/`0502`/`AnhXaNguocKhongDuyNhat`; chiều xuôi `TraMaHangAsync` `0501`…`0504` (webhook/CSKH). Phần sót ở §3.2 R5 + §1 Post-condition nay **ĐÃ ĐÓNG** ở N76 (⇒ K1) | P2 |
| 2 | **ĐÃ ĐÓNG** — §5.2 (298) "mỗi lượt của mỗi hãng **đã giành được khoá**"; không còn câu sống "đã vào tới `LapKeHoach`". BR-003 không giới hạn ⇒ **K2** (P3) | P2 |
| 3 | còn — §4.1 sequence vẫn chỉ có 2 nhánh `BiChan`/`DaApDung`, chưa có nhánh `Loi` (`D-90`) | P3 |
| 4 | còn — §1 BR-015 (131) vẫn có dấu backtick thừa sau `D-79` | P3 |
| 5 | đã đóng — header dòng 24 = `v0.5 — 18/11/2026`, khớp §15.4 | P3 |
| 6 | còn ⇒ chép lên **K2** | P3 |
| 7 | còn — §6.1 (372) vẫn "3 method còn lại"; §9 (551) thiếu `GetUnmappedCodesAsync`/`ResolveCarrierAddressCodeAsync` (chỉ có ở ghi chú 569) | P3 |
| 8 | còn — E1/E2/Pre-condition vẫn viết "bỏ `0509`/`0510`", dễ lẫn với số kế hoạch của `D-84`/`D-89` | P3 |
| 9 | còn — chưa nêu `return` sớm `CarrierAddressSyncWorker.cs:146-152` (lượt 0 việc) ở §1 bước 8/§3.1/§12 | P3 |
| 10 | còn — §11 (598) chỉ nêu `PostgreSqlSchemaTests.cs:208/:240`, thiếu `RangBuocChuaAnhXa.txt` | P3 |
| 11 | còn — §8.2 #6 (520), §12 "Redis chết" (630) vẫn dẫn `:137-145` (thật `:251`); chưa gắn 🔶; `TryAcquireAsync` `:124` nằm ngoài `try` | P3 |
| 12 | còn — §8.2 ghi chú (529), §15.2 F1 (804) dẫn `KeyPrefix` `:127`/`:115-128` (thật `:222`, `:237`) | P3 |
| 13 | còn — §11 (600–601) vẫn hướng dẫn thêm `ConfigureAddress()`/assembly, trái §14.3 (754) ✅ đã có | P3 |
| 14 | còn — §13 ghi chú metric (657–661), §15.2 F3 (806) dẫn số dòng `eCommerceMetrics.cs` sai; F3 chưa gắn "LỖI THỜI" | P3 |
| 15 | còn — §7.2 (476), §14.3 (762), hàng `D-80` (949) dẫn `design-doc-guidelines.md:102` (thật `:138`) | P3 |
| 16 | còn — §0 (51) vẫn "✅ Bảng đăng ký nguồn mã …, cấp địa chỉ hãng hỗ trợ" (registry ⬜) | P3 |
| 17 | còn — TC-11 (681) chưa gắn ⬜ (index có thật, ca chèn trùng chưa có) | P3 |

### Vòng 1 → vòng 2 (giữ nguyên bảng của bản vòng 2 — trạng thái tại thời điểm 18/11/2026)

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
| 9 | còn một phần — `D-90` áp ở §0/§1/§3.1/E9/TC-07/TC-29; §5.2 dòng 298 còn câu cũ ⇒ chép lên #2 (kiểm nhanh v0.5 xác nhận **đã đóng**) | P2 |
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
