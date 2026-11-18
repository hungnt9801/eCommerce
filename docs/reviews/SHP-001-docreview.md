# Doc review — SHP-001 Adapter GHN: tính phí và tạo vận đơn

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/shipping/shp-001-adapter-ghn-tinh-phi-van-don.md` (v0.5.1 theo changelog dòng 1248; header dòng 27 vẫn ghi `v0.5`) |
| Ngày · vòng | 19/11/2026 · vòng kiểm nhanh sau vòng 2 (chỉ kiểm phần đổi: #28, #29, #34) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, không đọc giải thích của BA) |
| Đã đọc | Phần đổi và phần phải khớp: 101-120 (§1, NFR dòng 117), 262-279 (§4.2), 555-607 (§6.2), 934-987 (§13), 1161-1266 (§14.4, §15.1–§15.4, mục đối chiếu). Grep cả tệp theo `D-89`, `D-110`, `NFR-004`, `TC-50`, `v0.5`, `12 NFR`, `52 test`. Đếm TC: `grep -c "^| \*\*TC-"` → 52 |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (16/16 heading, 22/22 BR có test, mục đối chiếu ở dòng 1252, 18 câu `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → exit 0 (3 khối của tệp PASS ở 124, 178, 224) |

Kết quả: **0 P1, 0 P2, 17 P3**. Cả hai P2 của vòng 2 (#28, #29) đã đóng. P3 #34 cũng đã đóng.
Bản sửa không sinh P1/P2 mới. Có 2 P3 mới:
- #35: grep theo đúng ràng buộc của #34 ("đường theo đơn dùng cửa mã `D-110`") còn thấy 2 chỗ ngoài vùng sửa vẫn nói theo `D-89`.
- #36: header chưa nâng lên v0.5.1.

Tệp lớn thêm 7 dòng (1259 → 1266): 6 dòng DoR (1173-1178) và 1 dòng changelog (1248). Vị trí từ dòng 1161 trở về trước không đổi.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | §13 NFR-004 (941) so với §1 NFR-004 (117), §4.2 hàng "ngoài transaction" (269), BR-002 (116), TC-08 (1001), TC-09 (1002). Cả năm chỗ cùng nói một điều: "**0** transaction DB mở trong lúc HTTP; UoW ngầm chỉ ở dạng `isTransactional: false`", kiểm bằng TC-08 (quét attribute) + TC-09 (HTTP thật). Không còn câu "0 UoW mở". DoR Q15–Q20 (1173-1178) so với §15.1 Q15–Q20 (1202-1207): cột "Chặn"/"Ảnh hưởng" khớp từng câu. Grep `D-89` (14 chỗ) theo ràng buộc của #34 | 1 phát hiện mới (#35, P3) |
| D2 chéo tài liệu / code / D-xx | §6.2 (601) nay trỏ cửa mã `D-110`, khớp BR-020 (116), E2 (115), §6.1 (468, 534), §6.3 `0014` (621). DoR Q16 dẫn `D-17` đúng nghĩa dòng gốc (đã lưu DB thì không đổi số). Không mở lại code: các dòng đổi không thêm `file:dòng` nào mới | 0 mới |
| D3 cơ chế thực thi | NFR-004 nay có hai test đỏ khi vi phạm: TC-08 (thiếu attribute / thiếu `requiresNew`) và TC-09 (`pg_stat_activity`). DoR: Q16 và Q20 ghi "CHẶN migration đầu", đúng hai câu mà sai thì không sửa lại được sau migration (`D-17`; cột NOT NULL). Q15, Q17, Q18, Q19 là ô `[ ]` chưa tích, nên DoR vẫn chưa đạt cho tới khi chốt | 0 mới |
| D4 SQL chạy được | Các mục đã đổi không có khối `sql`/`powershell`/`bash`. Cả tệp chỉ có rào `mermaid`, `csharp`, `json`, `jsonc` (đã liệt kê ở vòng 2) | không có khối sql; DDL mô tả ở §5.2 vẫn **chưa chạy** (vòng 2: `psql: command not found`) |
| D5 test/probe/tệp có thật | TC-08, TC-09 có ở 1001-1002. TC-32, TC-49, TC-50 mà DoR Q20 nhắc có ở §14.1. TC-17 mà DoR Q17 nhắc có. Nhưng TC-50 (1043) **không** ghi `TenantId = NULL`, trong khi DoR (1174) và Q20 (1207) khẳng định TC-50 chạy với giá trị đó. #33 vẫn còn, nay lặp thêm ở DoR | 0 mới; #33 còn |
| D6 nhãn tin cậy | Phần đổi không thêm ✅ nào. Changelog ghi "Không đổi số BR/test case": khớp, máy đếm 22 BR và grep đếm 52 TC (§14.3 dòng 1137 ghi 52/22/12) | 0 mới |
| D7 14 quy tắc + đối chiếu Nhanh | #2 NFR có ngưỡng: NFR-004 có ngưỡng số "0" và hai cách đo. #10: không có mục nào bị bỏ. #11–#14: Mermaid exit 0. Changelog v0.5.1 nằm ở **§15.4** (1248), không phải §15.3 (§15.3 là "Giả định"); nội dung ghi đúng ba mục đã sửa. Header (27) vẫn `v0.5` | 1 phát hiện mới (#36, P3) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 35 | P3 | F5 (1217), mục đối chiếu hàng `D-89` (1261) | Grep theo ràng buộc của #34 ("đường theo đơn tra mã hãng bằng cửa mã `D-110`") còn 2 chỗ nói theo `D-89`. F5: "Phụ thuộc **còn thiếu** duy nhất: `ResolveCarrierAddressCodeAsync` (`D-89`)". Câu này bỏ sót cửa mã `D-110` và hợp đồng Sales `IOrderShippingDirectory` (F7: chặn code); §6.5 dòng 680 lại ghi đủ cả hai cửa. Hàng `D-89` ghi "mã hãng chỉ qua `ResolveCarrierAddressCodeAsync`", ngược với BR-020 (116). Hàng `D-110` ngay dưới đã thay câu đó, nên không gây lỗi hành vi | F5: thêm "cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`) + `IOrderShippingDirectory` (F7)". Hàng `D-89`: ghi "(đường theo đơn: xem hàng `D-110`)" |
| 36 | P3 | Header (27) ↔ §15.4 (1248) | Header ghi `v0.5 — 19/11/2026`, còn changelog đã có v0.5.1 | Nâng header lên `v0.5.1` |
| 16 | P3 | §12 (917), §9 (836) | Còn 2 trích dẫn trôi. `PaymentTransaction.cs:147`: `MaskSensitiveData` khai báo ở `:313`. `ChannelAppService.cs:21–27`: §6.1 dòng 440 dùng `:28` | Đổi thành `:313` và `:21-28` |
| 17 | P3 | §5.1 (287-288), F1 (1213) | Vẫn ghi "grep `ShippingHub\|Shipment\|Carrier\|CodRecord` ⇒ 0", nhưng mẫu `Carrier` khớp `modules/Address/**` (chính dòng 299 trích `CarrierAddressMappingAppService`) | Bỏ `Carrier` khỏi mẫu grep |
| 18 | P3 | 5, 11, 13, 46, 75, 92, 116, 348, 552, 790, 862, 866, 912, 1113, 1145, 1157, 1159, 1229 | Trích dẫn dòng sang tài liệu khác bị trôi. Ví dụ `implementation-plan.md:199` → `:202`, `:290` → `:293`, `:338` → `:341`; `design-doc-guidelines.md:102`/`:116`/`:119` | Cập nhật số dòng |
| 19 | P3 | §3.2 #6 (208) ↔ #8 (210) | `Created → Cancelled` khai hai lần | Xoá hàng #8 |
| 20 | P3 | §6.3 `0015` (622), E3 (115) ↔ §12 (903) | Cùng mã `0015` nhưng hai HTTP khác nhau: 400 ở §6.3, 500 ở §12 | Chọn một HTTP, hoặc tách mã riêng |
| 21 | P3 | TC-38 (1031) | TC-38 khẳng định "0 lời gọi `ResolveCarrierAddressCodeAsync`" (cửa Guid). Đường theo đơn vốn dùng cửa mã, nên khẳng định này luôn đúng và không bắt được lỗi nào | Đổi sang cửa mã (hoặc kiểm cả hai cửa) |
| 22 | P3 | §1 NFR (117) ↔ §13 (948-949), §14.3 (1137) | §1 chỉ có NFR-001…010. §13 có thêm 011, 012, và §14.3 ghi "12 NFR" | Thêm NFR-011/012 vào §1 |
| 23 | P3 | §14.1 cột "Phủ": TC-04 (997), TC-27 (1020), TC-28 (1021) | Cột "Phủ" gắn sai BR | Sửa cho đúng BR (vd TC-04 → BR-020/ADR-06, TC-27 → NFR-007, TC-28 → ADR-08) |
| 25 | P3 | §13 ghi chú (962-963), TC-29 (1022) | Tài liệu nói `Forbidden` chặn `shipmentId`/`trackingCode`/`clientOrderCode`, nhưng `eCommerceMetrics.cs:219-221` không có ba nhãn này | Cho TC-29 kiểm theo whitelist `Allowed` (`:212`), hoặc ghi việc thêm 3 nhãn vào `Forbidden` |
| 26 | P3 | §11 bước 7 (880) | Chỉ ghi "thêm vào provider", thiếu hằng số ở module | Ghi rõ `ShippingHubPermissions` ở module + dòng ở provider |
| 27 | P3 | §3.2 #5 (207) | Guard của job thử lại không loại `FailureKind = Business` | Thêm điều kiện "attempt cuối là `TransientFailure`" |
| 30 | P3 | §5.2 `TotalWeightGrams` (372) ↔ Q5 (1192) | Cột ghi công thức `Σ Product…` như đã chốt, trong khi Q5 còn `Open` | Ghi "nguồn theo Q5" |
| 31 | P3 | §6.4 `ShipmentCreateLockTtlSeconds` (643), §8.2 cột TTL (794-797) | TTL theo từng khoá không có chỗ áp: repo chỉ gọi `TryAcquireAsync(name, timeout)`, còn provider dựng không kèm options (host `:254`) | Ghi TTL là tham số toàn cục của provider, hoặc bỏ cột TTL |
| 32 | P3 | A5 (114) ↔ §4.2 P1 (266), Q15 (1202) | A5 ghi "bản `Draft`/`Submitting` … chưa có cơ chế đi tiếp — Q15", trong khi Q15 và DoR (1175) chỉ nói về `Draft` | Sửa A5 thành "bản `Draft` …" |
| 33 | P3 | TC-50 (1043) ↔ Q20 (1207), DoR (1174) | DoR và Q20 nói TC-50 chạy với `TenantId = NULL`, nhưng Input của TC-50 không ghi điều đó | Ghi "`TenantId = NULL`" vào Input của TC-50 |

## Không kiểm được

- D4: không chạy được mảnh DDL nào ở §5.2 (vòng 2: `psql: command not found`). Phần đổi của vòng này không có khối SQL.
- Không mở lại code. Các dòng sửa (#28, #29, #34) không thêm trích dẫn `file:dòng` nào mới.
- Không đọc lại toàn văn: vòng kiểm nhanh chỉ đọc các đoạn ghi ở "Đã đọc". Các P3 còn mở được chép lại theo vị trí của vòng 2; với vị trí sau dòng 1161 thì cộng thêm 6 dòng (đã kiểm Q5, Q15, Q20, F1).

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 1 (5 P1, 11 P2): đã đóng hết ở vòng 2, chỉ còn #16 (hạ P3). Bảng dưới là trạng thái các phát hiện **vòng 2** sau lần sửa v0.5.1.

| # vòng 2 | Trạng thái | Mức lúc đó | Cách xác nhận (v0.5.1) |
|---|---|---|---|
| 28 | đã đóng | P2 | §13 (941): ngưỡng "0 transaction DB mở trong lúc HTTP; UoW ngầm chỉ ở dạng `isTransactional: false`", cách kiểm `TC-08` + `TC-09` (`pg_stat_activity`). Khớp §1 (117), §4.2 (269), BR-002 (116) |
| 29 | đã đóng | P2 | §14.4 thêm Q16, Q20 (🔴 CHẶN migration đầu, 1173-1174), Q15, Q17, Q18, Q19 (1175-1178). Phần ảnh hưởng ghi ở từng dòng khớp §15.1 (1202-1207) |
| 34 | đã đóng | P3 | §6.2 (601): "mã hãng tra qua cửa mã `D-110` từ mã trong snapshot" |
| 16 | còn (đã chép lên bảng "Phát hiện") | P3 | 917, 836 không đổi |
| 17 | còn (đã chép lên bảng "Phát hiện") | P3 | 287-288; F1 chuyển sang 1213 |
| 18 | còn (đã chép lên bảng "Phát hiện") | P3 | không đổi; 1223 chuyển sang 1229 |
| 19 | còn (đã chép lên bảng "Phát hiện") | P3 | 208 + 210 |
| 20 | còn (đã chép lên bảng "Phát hiện") | P3 | 622 ↔ 903 |
| 21 | còn (đã chép lên bảng "Phát hiện") | P3 | 1031 |
| 22 | còn (đã chép lên bảng "Phát hiện") | P3 | 117 |
| 23 | còn (đã chép lên bảng "Phát hiện") | P3 | 997, 1020, 1021 |
| 25 | còn (đã chép lên bảng "Phát hiện") | P3 | 962-963, 1022 |
| 26 | còn (đã chép lên bảng "Phát hiện") | P3 | 880 |
| 27 | còn (đã chép lên bảng "Phát hiện") | P3 | 207 |
| 30 | còn (đã chép lên bảng "Phát hiện") | P3 | 372; Q5 chuyển sang 1192 |
| 31 | còn (đã chép lên bảng "Phát hiện") | P3 | 643, 794-797 |
| 32 | còn (đã chép lên bảng "Phát hiện") | P3 | 114 |
| 33 | còn (đã chép lên bảng "Phát hiện") | P3 | 1043; nay DoR (1174) cũng nhắc lại |
