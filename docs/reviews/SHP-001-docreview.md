# Doc review — SHP-001 Adapter GHN: tính phí và tạo vận đơn

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/shipping/shp-001-adapter-ghn-tinh-phi-van-don.md` (v0.5.4, header dòng 27 khớp changelog dòng 1300; 1321 dòng) |
| Ngày · vòng | 20/11/2026 · vòng kiểm nhanh sau v0.5.4 (áp `D-118`, sửa #37/#39/#40/#41/#43) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, không đọc giải thích của BA) |
| Đã đọc | Phạm vi ORCH giao (chỉ các mục đổi v0.5.3 → v0.5.4): 101-120 (§1, BR-024), 199-212 (§3.2, để xác nhận #19), 431-710 (§6.1–§6.5), 811 (§8.2 #9), 1014-1321 (§14, §15, mục đối chiếu). Đối chiếu: `decisions.md` dòng 130 (`D-116`), 132 (`D-118`); `shp-003` 414-425, 495-514 (§6.1); `shp-004` 398-479 (§6.1); `shp-002` 452-455, 531-539 (§6.1). Grep: `I\w*AppService` + 4 tên method neo trên `shp-002/003/004`; các dấu hiệu của P3 còn mở trên `shp-001` |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (16/16 heading, 24/24 BR có test, mục đối chiếu ở dòng 1304, 4 dòng chứa chữ `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → exit 0 (3 khối của tệp PASS ở 124, 178, 224) · grep `^\| \*\*TC-` → 57 |

Kết quả: **0 P1, 1 P2, 20 P3**. Bản sửa đóng #37 (P2) và 4 P3: #39, #40, #41, #43. Phát sinh 1 P2 mới (#45) và 3 P3 mới (#46–#48).

Các điểm orchestrator yêu cầu kiểm:
- **#37 đóng.**
  - TC-53 (1072) nay dùng danh sách đóng {`IShipmentAppService`, `IShipmentCodAppService`} theo kiểu "⊆ + chứa", khớp nguyên văn câu chữ của `D-118` ①.
  - `shp-003` §6.1 (419, 504-514) khai `IShipmentCodAppService` với 7 method: `GetAsync`, `GetAdjustmentsAsync`, `RequestAdjustmentAsync`, `VerifyAdjustmentAsync`, `ApproveAdjustmentAsync`, `TriggerReconcileAsync`, `GetDiscrepanciesAsync`. Không method nào trùng 4 tên neo (b) ⇒ TC-53 (a)(b) không đỏ chéo với `SHP-003`.
  - `shp-002` §6.1 (531-539) không khai tên neo nào.
- **TC-19 không nới.** Bản v0.5.3 quét `*AppService`, nên sẽ luôn đỏ khi có `IShipmentCodAppService` (cửa đổi COD hợp lệ của `SHP-003`). Bản mới chỉ loại **đúng** cửa đó, theo tên. Mẫu code (1153-1170) mở rộng từ `typeof(IShipmentAppService)` sang mọi interface `IApplicationService` của assembly. Có một khe hở nhỏ giữa bảng và mẫu, ghi ở #46 (P3).
- **#39 đóng:** tên hàm là `TC10_chuyen_trang_thai_sai_thi_nem_0105` (1116).
- **#40 đóng:**
  - Q11 (1246) ghi "Đã chốt — `D-108`"; Q13 (1248) ghi `Closed (D-110)`; Q14 (1249) ghi `Closed (D-109)`. Không còn "TẠM CHỐT" ở ba câu này.
  - "TẠM CHỐT" chỉ còn ở Q5/Q7/Q9 (1240, 1242, 1244), dẫn `D-116`. Nhãn này đúng: `decisions.md:130` ghi `TẠM CHỐT (DEC)`.
  - Q14 nay ghi "Q3: phần xác minh sandbox … còn R3-2".
- **#41 đóng:** grep `đề nghị DEC` chỉ còn khớp ở changelog (1300). Q8 (1243) và F3 (1263) đã bỏ câu này.
- **#43 đóng:** đã khai record `CarrierFeeRequest` (484-502) và quy tắc chuẩn hoá. BR-024 (116) và §6.4 (653) trỏ về §6.1. Quy tắc ghép khoá có lỗi mới (#45).
- **Hàng `D-118` ở mục đối chiếu (1319):** thân đã sửa đúng các mục hàng này liệt kê: §6.1 437, TC-19 1038, TC-53 1072, hằng `DanhSachDong` 1144-1151, §14.3 1182/1190, Q7b 1242.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep cả tệp theo các ràng buộc mới.<br>• "4 method vận đơn chỉ ở `IShipmentAppService`": 437, 1072 (b), 1182. Bảng "phần thuộc `SHP-001`" (558-569) khai cả 4 trên `IShipmentAppService`. Khớp.<br>• "`[UnitOfWork(isTransactional: false)]` theo method, không ở class": 440, TC-08 1027, TC-53 (c). Khớp.<br>• "Khoá cache trừ `CorrelationId`": BR-024 116, §6.1 487, §6.4 653. §8.2 #9 (811) không nhắc "trừ `CorrelationId`", nhưng trỏ "đã chuẩn hoá", tức quy tắc ở §6.1 ⇒ không mâu thuẫn.<br>• "TẠM CHỐT", "đề nghị DEC", "Q3 vẫn Open": hết ở thân (#40, #41 đóng) | 0 mới |
| D2 chéo tài liệu / code / D-xx | **`D-118` ↔ TC-53:** đủ (a), (b), (c), câu "Đỏ khi", câu giải thích "⊆ + chứa", ngoại lệ `GetAsync`/`GetListAsync`/`GetDiscrepanciesAsync`, và hai ca QA.<br>**`D-118` ↔ TC-19:** đúng câu chữ, có "dùng chung hằng danh sách".<br>**`D-116` ① ↔ `CarrierFeeRequest`:** record có mã tuyến đi/đến, `ProviderServiceId`, `TotalWeightGrams`, `CodAmountMinorUnits`, `CurrencyCode`. Riêng việc loại `CorrelationId` khỏi khoá là làm rõ cần thiết (để nguyên thì cache không bao giờ trúng), không trái `D-116`.<br>**Chéo `shp-003`:** không đỏ chéo (xem trên). Hàng `D-118` (1319) ghi "một câu trỏ `D-118` ở đó do ORCH ghi", nhưng grep `D-118` trên `shp-003` = 0 (#48).<br>**Chéo `shp-004`:** grep 4 tên neo ⇒ `shp-004` §6.1 (469-476) khai lại `CreateShipmentAsync(CreateShipmentInput)` và `CancelShipmentAsync(CancelShipmentInput)` trên **cùng** `IShipmentAppService`, với DTO khác `SHP-001`. TC-53 (b) sẽ đỏ ở WP `SHP-004`, nhưng §15.2 không có F-dòng nào nêu việc này (#47) | 2 mới (#47, #48, P3) |
| D3 cơ chế thực thi | **BR-024:** quy tắc "ghép theo thứ tự khai báo, null ghi là rỗng" không nêu dấu phân cách, nên hai request khác nhau có thể cho cùng khoá. Không TC nào đỏ khi khoá va nhau (#45).<br>**TC-53:** "thêm interface thứ ba ⇒ đỏ" có ca QA; "method vận đơn ở interface khác ⇒ đỏ" có ca QA.<br>**TC-19:** bảng nói quét cả "class cài đặt" và tìm `Cod`/`Otp`/`ChangeCod`. Mẫu code chỉ quét interface, và chỉ bắt `Cod` + `Change`/`Update`/`Set` (#46).<br>#42 (TC-56 không có đầu vào lỗi, không có ca `ProviderCode`) vẫn còn | 2 mới (#45 P2, #46 P3) |
| D4 SQL chạy được | Grep rào code → `mermaid`×3, `csharp`×2, `json`×2, `jsonc`×1; 0 rào `sql`/`bash`/`powershell` | không có khối sql. Vòng này không đổi DDL §5.2 (vòng 2: `psql: command not found`) |
| D5 test/probe/tệp có thật | TC-53, TC-19 có ở 1072, 1038. Mẫu code TC-19 dùng hằng `DanhSachDong`/`CuaDoiCod` (1146-1151). `IShipmentCodAppService` có thật ở `shp-003:419`. Câu "một câu trỏ `D-118` ở đó" chưa có thật (#48). Grep `TC-` → 57 hàng, khớp §14.3 (1181) và changelog v0.5.4 | 1 mới (#48, đã tính ở D2) |
| D6 nhãn tin cậy | §6.1 437 ghi "✅ **Một** interface …" rồi "⬜ kế hoạch". ✅ ở đây là trạng thái quyết định, không phải tuyên bố đã đo ⇒ không hạ. TTL `300` vẫn ghi "chưa đo" ở §6.4, §8.2 #9, Q9, DoR 1220 | 0 mới |
| D7 14 quy tắc + đối chiếu Nhanh | #1: TC-53/TC-19 kiểm chứng được bằng reflection. #7: `CarrierFeeRequest` dùng `long?` minor units, không `decimal`/`double` ở biên. #8: không thêm endpoint. #10: không xoá mục nào. #11–#14: Mermaid exit 0. Có hàng `D-118` ở mục đối chiếu, thân khớp. Header v0.5.4 khớp changelog | 0 mới |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 45 | P2 | §6.1 chú thích `CarrierFeeRequest` (488-489), BR-024 (116) | **Vấn đề:** quy tắc lập khoá cache là "ghép theo THỨ TỰ KHAI BÁO …, null ghi là ký tự rỗng". Quy tắc không nêu dấu phân cách, nên hai request khác nhau cho ra **cùng** khoá.<br>**Bằng chứng (cùng tenant, hãng, tuyến, dịch vụ):** (`TotalWeightGrams = 1250`, `CodAmountMinorUnits = 250000`) và (`12502`, `50000`) đều cho chuỗi `…1250250000VND`. Ca thứ hai: (`1250`, COD `null`) và (`125`, COD `0`) đều cho `…1250VND`.<br>**Đường tới lỗi:** BE viết hàm thuần lập khoá đúng như chữ (`string.Concat` theo thứ tự trường). Khách đặt đơn 12,5 kg ở checkout trong vòng 300 s sau một đơn 1,25 kg cùng tuyến sẽ nhận phí ước tính của đơn 1,25 kg từ cache. Khách bị báo/thu sai phí. Lệch chỉ được ghi nhận khi tạo vận đơn (E8), và TC-55…57 không có ca nào đỏ | Đổi quy tắc thành một dạng không va: dấu phân cách không xuất hiện trong giá trị kèm thoát ký tự, hoặc tiền tố độ dài từng trường, hoặc tuần tự hoá chuẩn (JSON theo thứ tự trường) rồi băm. Ghi rõ null ≠ rỗng ≠ `0`. Thêm vào TC-56 (hoặc TC mới) hai ca va nêu trên ⇒ provider giả bị gọi **2** lần |
| 46 | P3 | TC-19 bảng (1038) ↔ mẫu code (1153-1170) | Bảng nói quét interface **và class cài đặt**, và tìm `Cod`/`Otp`/`ChangeCod`. Mẫu chỉ quét interface của assembly Contracts, và chỉ bắt tên chứa `Cod` + (`Change`/`Update`/`Set`). Một class `…AppService : ApplicationService` không có interface ở Contracts, có `AdjustCodAsync`/`…Otp…`, vẫn lọt dù ABP vẫn phát endpoint cho nó. Ngược lại, `Cod` khớp cả `Code` (vd `UpdateTrackingCodeAsync`) ⇒ đỏ oan | Mẫu: thêm quét class `IApplicationService` ở `eCommerce.ShippingHub.Application` (trừ `ShipmentCodAppService`), thêm `Otp`, và so `Cod` theo ranh giới từ (không khớp `Code`) |
| 47 | P3 | §15.2 (thiếu F-dòng), TC-53 (b) (1072) ↔ `shp-004` §6.1 (473, 476) | `shp-004` khai lại `CreateShipmentAsync(CreateShipmentInput)` và `CancelShipmentAsync(CancelShipmentInput)` trên `IShipmentAppService` (DTO khác `SHP-001`; `CreateShipmentRequest` của `shp-004` còn nhận `CodAmount`). Làm theo ở WP `SHP-004` ⇒ TC-53 (b) "đúng 1 lần" đỏ. Đỏ là đúng ý, nhưng `SHP-001` không có F-dòng giao việc sửa (F9 chỉ nói `ClientOrderCode`) | Thêm vào F9 (hoặc F-dòng mới): "`SHP-004` §6.1 bỏ hai hàng `CreateShipmentAsync`/`CancelShipmentAsync` (thuộc `SHP-001`, `D-116`/`D-118`); chọn hãng bằng `ProviderCode`/cấu hình, không bằng overload" |
| 48 | P3 | Hàng `D-118` mục đối chiếu (1319) | Ghi "`shp-003` §6.1 khai `IShipmentCodAppService` (một câu trỏ `D-118` ở đó do ORCH ghi)". Grep `D-118` trên `shp-003-cod-thu-ho-doi-cod.md` = 0 (bản làm việc lúc kiểm) | Ghi "⬜ chưa ghi" cho tới khi câu đó có, hoặc ORCH ghi câu ở `shp-003:419` trước khi gộp PR |
| 38 | P3 | §6.3 ghi chú (632), §7.2 (743-744), F12(d) (1272) | Vẫn nói `SHP-002` ở dải cũ (`0001`–`0011`, "`SHP-002` chỉ có thể trả `eCommerce:Shipping:0004`"), trong khi `shp-002` đã dùng `0201`–`0210` | Sửa 632 và F12(d) theo hiện trạng; sửa 744 thành `0204` |
| 42 | P3 | TC-56 (1075), BR-024 (116) | Expected nói "kết quả lỗi **không** được cache", nhưng Input không có lời gọi lỗi. Cũng không có ca nào đỏ khi khoá thiếu `ProviderCode` | Thêm ca provider lỗi lần 1, thành công lần 2 ⇒ 2 lời gọi; thêm ca hai `ProviderCode` cùng request ⇒ 2 lời gọi |
| 44 | P3 | §6.4 ghi chú (653) ↔ jsonc (655-684) | Ghi chú nói timeout/retry/backoff/trần thử/chu kỳ job là **ABP Setting**, nhưng jsonc đặt các giá trị đó trong `ShippingHubOptions`. Không rõ nguồn nào thắng | Ghi rõ: Setting (jsonc chỉ còn secret/endpoint), hoặc options + Setting đè (nêu thứ tự ưu tiên) |
| 16 | P3 | §12 (943), §9 (859), §0 (97) | Trích dẫn trôi: `PaymentTransaction.cs:147` (`MaskSensitiveData` ở `:313`, như BR-012 đã ghi) và `ChannelAppService.cs:21–27` (§6.1 dùng `:28`) | Đổi thành `:313` và `:21-28` |
| 17 | P3 | §5.1 (288), F1 (1261) | Mẫu grep `ShippingHub\|Shipment\|Carrier\|CodRecord` ⇒ 0, nhưng `Carrier` khớp `modules/Address/**` | Bỏ `Carrier` khỏi mẫu grep |
| 18 | P3 | nhiều dòng (danh sách vòng trước, số dòng dời theo tệp) | Trích dẫn dòng sang tài liệu khác bị trôi (vd `implementation-plan.md:199` → `:202`, `:290` → `:293`; `design-doc-guidelines.md:102`) | Cập nhật số dòng |
| 19 | P3 | §3.2 #6 (208) ↔ #8 (210) | `Created → Cancelled` khai hai lần | Xoá hàng #8 |
| 20 | P3 | §6.3 `0103` (639) ↔ §12 (928-929) | Cùng mã `0103` cho ca "thành công mà không có mã vận đơn" nhưng HTTP khác nhau (400 ở §6.3, 500 ở §12) | Chọn một HTTP hoặc tách mã |
| 21 | P3 | TC-38 (1057) | Khẳng định "0 lời gọi `ResolveCarrierAddressCodeAsync`" (cửa Guid). Đường theo đơn không dùng cửa này, nên khẳng định luôn đúng | Đổi sang cửa mã `ResolveCarrierAddressCodeByCodeAsync` |
| 22 | P3 | §1 NFR (117) ↔ §13 (974-975), §14.3 (1181) | §1 chỉ có NFR-001…010, trong khi §13 và §14.3 có 12 | Thêm NFR-011/012 vào §1 |
| 23 | P3 | §14.1 cột "Phủ": TC-04 (1023), TC-27 (1046), TC-28 (1047) | Gắn sai BR (TC-04/TC-27 → BR-011, TC-28 → BR-017) | TC-04 → BR-020/ADR-06; TC-27 → NFR-007; TC-28 → ADR-08 |
| 25 | P3 | §13 ghi chú (989), TC-29 (1048) | Nói `Forbidden` chặn `shipmentId`/`trackingCode`/`clientOrderCode`, nhưng `eCommerceMetrics.cs:219-221` không có ba nhãn này | Kiểm theo whitelist `Allowed` (`:212`), hoặc ghi việc thêm 3 nhãn vào `Forbidden` |
| 26 | P3 | §11 bước 7 | Chỉ ghi "thêm vào provider", thiếu hằng số ở module | Ghi rõ `ShippingHubPermissions` ở module + dòng ở provider |
| 27 | P3 | §3.2 #5 (207) | Guard của job thử lại không loại `FailureKind = Business` | Thêm điều kiện "attempt cuối là `TransientFailure`" |
| 31 | P3 | §6.4 `ShipmentCreateLockTtlSeconds` (666), §8.2 cột TTL | Không có chỗ áp TTL theo từng khoá: repo chỉ gọi `TryAcquireAsync(name, timeout)` | Ghi TTL là tham số toàn cục của provider, hoặc bỏ cột TTL |
| 33 | P3 | TC-50 (1069) ↔ Q20 (1255) | Q20 nói TC-50 chạy với `TenantId = NULL`, nhưng Input của TC-50 không ghi | Thêm "`TenantId = NULL`" vào Input của TC-50 |
| 35 | P3 | F5 (1265), hàng `D-89` mục đối chiếu (1313) | F5 vẫn ghi "Phụ thuộc **còn thiếu** duy nhất: `ResolveCarrierAddressCodeAsync`", bỏ sót cửa mã `D-110` và `IOrderShippingDirectory` (F7). Hàng `D-89` ghi "mã hãng chỉ qua `ResolveCarrierAddressCodeAsync`", trái BR-020 | F5: thêm cửa mã `D-110` + `IOrderShippingDirectory`. Hàng `D-89`: thêm "(đường theo đơn: xem hàng `D-110`)" |

## Không kiểm được

- **Chỉ đọc theo mục ORCH giao.** Không đọc lại toàn văn: các đoạn ngoài `readRanges` không được kiểm lại ở vòng này. Bản v0.5.3 chưa commit (HEAD = v0.5.1), nên không tách được diff riêng v0.5.3 → v0.5.4 bằng `git diff`. Việc giới hạn phạm vi dựa vào changelog v0.5.4 (1300) cùng danh sách mục của ORCH.
- **Các P3 cũ ngoài phạm vi** (#16–#35, #38, #42, #44) chỉ được xác nhận "còn" bằng grep dấu hiệu, chưa đọc lại toàn đoạn. #18 và #26 không đối chiếu từng dòng.
- **D4:** không có khối SQL. DDL §5.2 chưa từng chạy (`psql: command not found` ở vòng 2).
- **Không mở code.** Vòng này không có trích dẫn `file:dòng` mới tới code.
- **Không kiểm `shp-002`/`shp-003`/`shp-004` ngoài §6.1.** Bản làm việc của chúng chưa commit, nên #47 và #48 dựa trên `git status` lúc kiểm.

## Vòng trước (vòng kiểm nhanh 20/11/2026, v0.5.3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 1 (5 P1, 11 P2), vòng 2 (2 P2) và các P3 #30, #32, #36 đã đóng ở các vòng trước. Bảng dưới là trạng thái các phát hiện của vòng v0.5.3, sau bản v0.5.4.

| # trước | Trạng thái | Mức lúc đó | Cách xác nhận (v0.5.4) |
|---|---|---|---|
| 37 | đã đóng | P2 | TC-53 (1072) theo nguyên văn `D-118`. `shp-003` §6.1 (504-514) không khai tên neo nào ⇒ không đỏ chéo. TC-19 (1038) chỉ loại đúng `IShipmentCodAppService` |
| 39 | đã đóng | P3 | 1116 `TC10_chuyen_trang_thai_sai_thi_nem_0105` |
| 40 | đã đóng | P3 | Q11/Q13/Q14 (1246, 1248, 1249) không còn "TẠM CHỐT"; Q14 nay ghi "Q3: phần xác minh sandbox … còn R3-2" |
| 41 | đã đóng | P3 | grep `đề nghị DEC` → chỉ còn ở changelog (1300) |
| 43 | đã đóng | P3 | record ở 484-502 + quy tắc chuẩn hoá; quy tắc ghép có lỗi mới (#45) |
| 38 | còn (đã chép lên bảng "Phát hiện") | P3 | 632, 743-744, 1272 |
| 42 | còn (đã chép lên bảng "Phát hiện") | P3 | TC-56 (1075) không đổi |
| 44 | còn (đã chép lên bảng "Phát hiện") | P3 | 653 ↔ 655-684 không đổi |
| 16 | còn (đã chép lên bảng "Phát hiện") | P3 | grep → 943, 859, 97 |
| 17 | còn (đã chép lên bảng "Phát hiện") | P3 | grep → 288, 1261 |
| 18 | còn (đã chép lên bảng "Phát hiện") | P3 | không đối chiếu lại từng dòng |
| 19 | còn (đã chép lên bảng "Phát hiện") | P3 | 208 + 210 (đã đọc) |
| 20 | còn (đã chép lên bảng "Phát hiện") | P3 | grep → 639 ↔ 928-929 |
| 21 | còn (đã chép lên bảng "Phát hiện") | P3 | 1057 (đã đọc) |
| 22 | còn (đã chép lên bảng "Phát hiện") | P3 | 117 (đã đọc); 974-975 |
| 23 | còn (đã chép lên bảng "Phát hiện") | P3 | 1023, 1046, 1047 (đã đọc) |
| 25 | còn (đã chép lên bảng "Phát hiện") | P3 | grep → 989, 1048 |
| 26 | còn (đã chép lên bảng "Phát hiện") | P3 | không đọc lại §11 |
| 27 | còn (đã chép lên bảng "Phát hiện") | P3 | 207 (đã đọc) |
| 31 | còn (đã chép lên bảng "Phát hiện") | P3 | 666 (đã đọc) |
| 33 | còn (đã chép lên bảng "Phát hiện") | P3 | 1069 (đã đọc) |
| 35 | còn (đã chép lên bảng "Phát hiện") | P3 | 1265, 1313 (đã đọc) |
