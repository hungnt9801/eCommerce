# Doc review — ADDR-003 Chuẩn hoá địa chỉ khách hàng: convert 3 cấp ↔ 2 cấp, xử lý ca sáp nhập tỉnh

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-003-chuan-hoa-dia-chi-3-cap-2-cap.md` |
| Ngày · vòng | 19/11/2026 · vòng kiểm nhanh sau vòng 3 (sau 2 lượt BA áp `D-105`…`D-107` rồi `D-111`; cùng tệp) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1100 (toàn văn; đọc theo đoạn 1-150, 150-260, 260-430, 429-628, 628-827, 827-966, 966-1100) + `decisions.md` dòng 111-125 (`D-97`…`D-111`, nguyên văn) + `Address.cs:248-289` |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (KET QUA: DAT; 16/16 heading, 17/17 BR có test, mục đối chiếu ở dòng 1074, 12 dòng `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 212 khối, 0 lỗi |

Kết quả lượt này: **0 P1, 2 P2, 37 P3**. Verdict `DOC-OK` (0 P1, 2 P2 < 3).

- **#1 (P2, đã biết)** là P2 của vòng 3. `D-107` chốt sửa trước WP-15 nên lượt này **không** tính là lỗi mới. `D-111` đã làm rõ được một phần: ở vế (2), `From*` lấy từ `To*` gốc, `InputSnapshot` chép từ dòng gốc. Phần còn lại vẫn mở, và vì vế (2) mới mà rộng thêm (xem #1).
- **#2 (P2, mới):** `TC-36` là cổng chặn release của `D-105`, nhưng cách viết hiện nay khiến nó không bao giờ đỏ được.
- **Đã đóng 1 P3 vòng 3:** #12 (review `NoMapping` gặp nhóm mới), nhờ vế (3) + `TC-44`.
- **Còn một phần:** #7 vòng 3 (`DaXacMinh`), nay có Q23 nhưng mới phủ một phần.

Các mâu thuẫn ORCH nhờ soát, kết quả từng mục:

| Mục ORCH nhờ soát | Kết quả |
|---|---|
| Bảng `InputTexts` xoá được vs BR-08 | **không** mâu thuẫn. BR-08 chỉ áp cho ledger (363, 366); grep `freeText` chỉ còn ở dòng lịch sử (trừ #13 mới) |
| `Withdrawn` vs CHECK `MappingKind` | **không** mâu thuẫn. `Withdrawn = 3` chỉ có trong file, CHECK `in (0,1,2)` (329, 404), có `TC-42` |
| `D-111` vs `D-100` (6) / `D-97` (e) | thân giữ vế (1) đúng chữ `D-100` (6). Vế (2) thực hiện `D-97` (e). Có 3 chỗ hở mới: #5, #6, #7 |
| BR có TC | 17/17 BR có TC (máy) |
| Số đếm | khớp: 47 TC = TC-01…TC-47, 17 BR, 4 `DbSet` (971, 974, 775) |
| SQL / index | không có khối `sql`; mô tả index/CHECK hợp lệ cú pháp PG 15+, **chưa chạy**. Có 2 chỗ hở: #8, #9 |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | **Chỉ kiểm các mục đã đổi** và mâu thuẫn chéo do chúng sinh ra. Grep `-o` cả tệp: `freeText\|FreeText\|InputTexts\|InputHash` (42 chỗ) — `freeText` chỉ còn ở §11 782 (hướng đã chọn), Q15 1014 (lịch sử) và mục đối chiếu 1089/1092. Grep `Withdrawn\|MappingKind in` (13 chỗ) — 280, 329, 404, 515, 599, 774, 805, 888 khớp nhau. Grep `chỉ quét\|Cũng quét\|chỉ địa chỉ có ledger` — chỉ còn trong câu hỏi Q22 (1022) và cột "cũ nói" (1095). Đã so: BR-17 (94) ↔ §5.2 345-377 ↔ §4.2 bước 4 (240) ↔ §10.1 (742) ↔ §12 (807) ↔ TC-36 (882). Hàng 5 guard `D-106` Q19 (178) ↔ §6.2 `ConfirmAsync` (512) ↔ §6.3 `03xx` (603) ↔ TC-43. Hàng 7 vế (1)–(3) (180) ↔ §7.2 (632) ↔ §11 (780) ↔ index (395-396) ↔ TC-41, TC-44…TC-47 ↔ Q23 | 5 phát hiện mới (#3, #4, #5, #6, #12) |
| D2 chéo tài liệu / code / D-xx | Đọc nguyên văn `D-105`, `D-106`, `D-107`, `D-111`, rồi đi theo danh sách "BA sửa cùng PR" của từng dòng. `D-111` đủ cả: hàng 7, §11, §5.2 index, §7.2, TC-41 bỏ 🔶, TC-44…TC-47 khớp ⑥(2)…(5), Q22 đóng, Q23 = nợ N1, mục đối chiếu 1095. `D-106`: Q12/Q17/Q18/Q19 đóng (1016-1019), `MappingKind.Withdrawn` cuối dải, `03xx` của Q19. `D-105`: bảng riêng, `InputHash`, Q21. Code: `Address.cs:27` = 256 ✅, `:30` = 512 ✅, `:255-260` là guard `SoNhaDuong` (256), guard 512 ở `:282-286`. `ChannelAppService.cs:28` = `[Authorize]` (grep) | 2 phát hiện mới (#10, #13) |
| D3 cơ chế thực thi | BR-17 "mất bản ghi không làm hỏng" ⇒ `TC-36`. Chỗ này **không đỏ được** (#2). `Withdrawn` không lưu ⇒ CHECK + `TC-42` ✅. "1 dòng mỗi (lô, địa chỉ)" ⇒ khoá §7.2 + `TC-46` ✅. Ưu tiên (1) > (2) ⇒ `TC-46` "đổi thứ tự quét" ✅. Unique `(TenantId, LedgerId)` có `NULLS NOT DISTINCT` nhưng `TC-17` không phủ (#8) | 2 phát hiện mới (#2, #8) |
| D4 SQL chạy được | Rào code: `mermaid` (101, 158, 198), `csharp` (424, 897), `json` (531, 545, 570). **Không có khối `sql`/`powershell`/`bash`** (grep `` ^``` ``). Index partial `WHERE "Outcome" = 0/1`, unique `NULLS NOT DISTINCT (TenantId, LedgerId)`, CHECK `MappingKind in (0,1,2)`, CHECK `RawText/StreetLine` mới chỉ là mô tả, **chưa chạy**: máy không có `psql`, `docker`, `postgres` | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | `Address.cs:27,30` khớp. `Address.cs:256-260` dẫn sai cho `RawText` (#10). `ChannelAppService.cs:28`: §1 (89) đúng, §6.2 (505) vẫn ghi `:27` (#26, còn). TC-39…TC-47 có trong §14.1; DoD "47 test case" khớp | 1 phát hiện mới (#10) |
| D6 nhãn tin cậy | Q22 "✅ Closed — Đã chốt `D-111` (TẠM CHỐT)", khớp dòng 125. DoR (986) ghi trạng thái chốt đúng từng `D-xx`. Q21 và Q23 `⬜ Open`. Không có ✅ mới nào thiếu số đo | 0 phát hiện mới |
| D7 14 quy tắc + đối chiếu Nhanh | #3 "guard sai thì làm gì": hàng 5 có `03xx` + `TC-43`. #5 index idempotency: `InputTexts` có unique, nhưng `TC-17` không đếm (#8). #9 đếm ≠ dòng hiển thị: review cũ bị thay vẫn tính là `Pending` (#4). Mermaid: 0 lỗi. Mục đối chiếu có hàng `D-105`, `D-106`, `D-107`, `D-111`; hàng `D-101` lỗi thời (#13) | 2 phát hiện mới (#4, #13) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §3.1 hàng 7 (180), §11 (780) ↔ ledger `InputSnapshot` NOT NULL (345), A3 / §4.2 bước 5 (92, 241), TC-38 (884) | *(vòng 3 #1, còn. `D-107`: sửa trước WP-15, **không** tính là lỗi mới.)* `D-111` mới chốt đầu vào cho vế (2): `From*` = `To*` gốc, `InputSnapshot` chép từ gốc. Vế (1)/(3) vẫn chưa nói `InputSnapshot`/`From*` của dòng mới lấy từ đâu. Vế (2) lại sinh hai vấn đề mới. **Thứ nhất**, cùng một dòng mà `From*` là mã V2 (B) còn `InputSnapshot` là `Legacy*` V1 gốc, trong khi A3/§4.2 bước 5 cho `ToThreeLevelAsync` đọc "`From*Code` + `InputSnapshot`" mà không nói cột nào thắng. **Thứ hai**, `TaoHoacLayAsync` có thể gom nhiều nguồn v1 vào cùng một `Address` v2, nên "dòng gốc" của vế (2) có thể có nhiều dòng với `InputSnapshot` khác nhau. TC-38 vẫn chưa có vế kiểm nội dung | Ghi ở hàng 7, cho cả ba vế: dòng mới chép `InputSnapshot` (+ `InputHash`) từ dòng `Normalized` đã sinh kết quả hiện tại. Nếu có nhiều dòng như vậy, ghi luật chọn (vd dòng mới nhất). Ghi ở A3: `ToThreeLevelAsync` **chỉ** đọc `InputSnapshot`, **không** đọc `From*` của dòng soát lại. Thêm vế vào TC-38 |
| 2 | P2 | §14.1 TC-36 (882) ↔ BR-17 (94), §5.2 (377), §11 (782) "chặn release tới khi code + test (`TC-36`)" | `TC-36` xoá bản ghi `AddrNormalizationInputTexts` "SQL trong `BEGIN…ROLLBACK`" rồi mới gọi `ConfirmAsync`/`ToThreeLevelAsync`/`GetReviewQueueAsync`. App service chạy trên kết nối/UoW riêng. Với READ COMMITTED + MVCC, kết nối đó **vẫn đọc thấy** bản ghi vì lệnh xoá chưa commit. Đường tới lỗi: BE lỡ để `ConfirmAsync` hoặc §10.1 phụ thuộc bản ghi chuỗi, QA làm đúng như TC-36, test vẫn xanh, cổng release của `D-105` qua. Đến khi Q21 có đường xoá thật thì các luồng này NÉM/500. Khác với TC-38/TC-42: ở đó `INSERT` vi phạm CHECK và lỗi được đọc trong **cùng** phiên nên vẫn đúng | Đổi thành: xoá **có commit** trên DB test riêng của test (hoặc fixture không bao giờ ghi bản ghi chuỗi), rồi mới gọi 3 luồng. Thêm đối chứng: trước khi xoá, chuỗi đánh dấu có hiện ở §10.1 |
| 3 | P3 | §10.1 cột trái (742) ↔ §3.1 hàng 7 (180), §5.2 `LedgerId` (372) | Chuỗi phố được tra "theo `LedgerId`" của dòng `NeedsReview`. Dòng soát lại do hàng 7 ghi có `LedgerId` mới và **không bao giờ** có bản ghi chuỗi (job không ghi). Vì vậy mọi review `MappingSuperseded` sẽ hiện *"chuỗi tự do đã bị xoá"*, mà thực ra chưa ai xoá | Tra chuỗi theo dòng ledger gốc (cùng nguồn với `InputSnapshot` ở #1). Tách hai nhãn "không có chuỗi" / "đã bị xoá" |
| 4 | P3 | §3.1 hàng 7 vế (3) (180) ↔ hàng 6 (179), `D-96` (5), §6.2 `GetReviewQueueAsync` (510) | Vế (3) ghi dòng `NeedsReview` mới cho review đang `Pending`, nhưng dòng cũ không có dòng kết thúc. Theo `D-96` (5), dòng cũ **vẫn `Pending`**. Hệ quả: một địa chỉ có hai review `Pending`. `RejectAsync` trên `ReviewLedgerId` cũ qua guard hàng 6. "`TotalCount` = số review `Pending`" bị tính đôi nếu đếm theo dòng (§6.2 chỉ nói lấy "dòng mới nhất" khi liệt kê) | Định nghĩa: review `Pending` = dòng `NeedsReview` **mới nhất** của địa chỉ, chưa có dòng kết thúc. Gọi hàng 5/6 trên dòng không còn là mới nhất ⇒ NÉM (`03xx`/`0103`). Thêm vế này vào TC-43/TC-44 |
| 5 | P3 | §3.1 hàng 7 vế (1) (180) ↔ vế (2) "`ResultAddressId ?? SourceEntityId`", khoá `Address:ReNormalize:{L}:{addressId}` (632), TC-46 (892) | Vế (2) ghi rõ địa chỉ là `ResultAddressId ?? SourceEntityId`, còn vế (1) chỉ nói "địa chỉ `DaChuanHoa` có ledger `MappingImportId`…". Với nguồn v1 X tự động ra `Address` v2 Y, nếu đọc vế (1) theo `SourceEntityId` thì X (v1) bị đánh dấu, còn Y (đang dùng) giữ `DaChuanHoa` với mã cũ. Hai vế cũng ra hai `addressId` khác nhau, nên luật "ưu tiên (1) > (2)" và kỳ vọng "1 dòng" của TC-46 không áp được | Ghi ở vế (1): địa chỉ = `ResultAddressId ?? SourceEntityId` của dòng ledger đó, giống vế (2) |
| 6 | P3 | TC-47 (893); §3.1 hàng 7 vế (2) cấp tỉnh (180) ↔ `AddressCandidateDto` (471-474: `WardCode`/`WardName` không nullable), `ConfirmAsync(ChosenWardCode)` (512) | Nhóm cấp tỉnh B→C ra `Candidates = [C]`, tức chỉ có mã tỉnh. Ứng viên bắt buộc có xã, và `ConfirmAsync` nhận mã xã, nên CSKH chỉ còn **Từ chối** được. Tài liệu không nói ứng viên của nhóm cấp tỉnh lấy xã từ đâu (giữ `XaCode` hiện tại nếu xã thuộc C trong danh mục?) | Ghi luật ghép xã cho ứng viên cấp tỉnh. Sửa Expected của TC-47 thành ứng viên `(C, xã)` |
| 7 | P3 | §5.2 index vế (3) (396) ↔ hàng 7 vế (3) "mã `From*` ở cấp `G.FromLevel`" (180), `FromLevel` `3→2` (327) | Index chỉ có `FromProvinceCode`/`FromWardCode`. Nhóm V1→V2 cấp huyện (`FromLevel = 3`) khớp theo `FromDistrictCode` thì không có index ⇒ job quét toàn bảng (chỉ chậm, không sai) | Thêm `FromDistrictCode` vào danh sách, hoặc ghi rõ cấp 3 không cần index |
| 8 | P3 | §14.1 TC-17 (863), fixture (958-964) ↔ §5.2 unique `AddrNormalizationInputTexts` (402), §11 (774, 783) | Unique `NULLS NOT DISTINCT (TenantId, LedgerId)` mới không có trong danh sách của TC-17, nên migration EF tự sinh làm mất thuộc tính (rủi ro ở 783) mà không gì đỏ. TC-36 kiểm `23505` nhưng không nói chạy với `TenantId = NULL` | Thêm index này vào TC-17 (đếm theo tên + `NULLS NOT DISTINCT`), hoặc thêm ca `TenantId = NULL` vào TC-36 |
| 9 | P3 | §5.2 CHECK `"RawText" IS NOT NULL OR "StreetLine" IS NOT NULL` (375, 403) ↔ `AddressInput.StreetLine` là `string` không nullable (457), `Address.cs:255` | CHECK cho qua chuỗi rỗng. Vì `StreetLine` luôn khác `NULL` nên CHECK luôn đúng, trái mục đích "không ghi dòng rỗng". `Address` thì chuẩn hoá chuỗi trắng thành `null` (`:255`) | Ghi rõ: chuỗi trắng được chuẩn hoá thành `NULL` trước khi ghi; cả hai `NULL` ⇒ không ghi bản ghi. CHECK dùng `btrim(…) <> ''` |
| 10 | P3 | §5.2 `RawText` (373) | "vượt ⇒ NÉM như `Address` (`Address.cs:256-260`)": `:256-260` là guard `SoNhaDuong` (256). Guard độ dài 512 (`DiaChiDayDu`) ở `:282-286` (đã đọc) | Đổi thành `Address.cs:282-286` (dòng `StreetLine` dùng `:256-260`) |
| 11 | P3 | §5.2 `InputHash` (346) ↔ §8.2 `inputHash` (697), Q21 (1021) | `InputHash` là SHA-256 **không muối** của đầu vào, có cả chuỗi tự do, và được giữ vĩnh viễn ở sổ cái. Sau khi xoá bảng chuỗi, ai có sẵn một chuỗi nghi ngờ vẫn kiểm được là khớp hay không. Không kết luận tuân thủ | Ghi điểm này vào Q21 (`nguoi`) để người quyết cùng cơ chế xoá |
| 12 | P3 | §3.1 hàng 7 (180) ↔ Q23 (1023) | Q23 ghi "Ảnh hưởng: §3.1 hàng 7 (giới hạn của vế (2))", nhưng hàng 7 không trỏ Q23. Người đọc hàng 7 không biết vế (2) bỏ sót địa chỉ không có ledger và địa chỉ `DaXacMinh` | Thêm "giới hạn: Q23" vào hàng 7 |
| 13 | P3 | Mục đối chiếu hàng `D-101` (1089) | Cột "Đã sửa thành" vẫn ghi "`freeText` duy nhất chứa PII", trái với thân hiện tại (`AddressInputSnapshot` không còn `freeText` — 466-469, `D-105`) | Thêm "(thay bởi `D-105`, hàng 1092)" |
| 14 | P3 | §6.3 (588, 603) ↔ §5.2 `IsActive` (332) | §6.3 có hàng `03xx` cho Q19, nhưng mã NÉM của `NgungHieuLuc` (`D-100` (1), cũng "`03xx` cuối dải") không có hàng. Hai mã cùng "số kế tiếp khi code" | Thêm hàng cho mã `NgungHieuLuc`, hoặc ấn định số cho cả hai |
| 15 | P3 | §3.1 hàng 7 cột Guard (180) ↔ cột Hành động (180) | *(vòng 3 #2, còn)* "resolver thấy **1** dòng ⇒ **tự động** tới C" trái với "kể cả khi tra mới ra đúng 1 đích: vẫn `NeedsReview`" | "lượt chuẩn hoá **mới** của mã A ⇒ tự động; địa chỉ đã `DaChuanHoa` ⇒ review (`D-103`)" |
| 16 | P3 | §8 bảng đăng ký idempotency key (693-701) | *(vòng 3 #3, còn)* Thiếu khoá `…:{inputHash}:Invalid` (177) | Thêm hàng |
| 17 | P3 | §6.2 `GetReviewQueueAsync` (510) ↔ §10.1 bộ lọc (741) | *(vòng 3 #4, còn)* Tập gốc `Address.TrangThai ∈ {ChuaChuanHoa, NghiNgo}` làm lọc `Confirmed` thiếu dòng | Tập gốc = dòng ledger `NeedsReview` mới nhất |
| 18 | P3 | §10.2 nhãn trạng thái (755) | *(vòng 3 #5, còn)* "*đang chờ xác nhận*" vẫn hiện sau khi review đã kết thúc | Nhãn theo review `Pending` |
| 19 | P3 | §3.1 hàng 4 (177) | *(vòng 3 #6, còn)* "suy lại từ input bằng kiểm định dạng thuần" không suy ra được `0001` | "NÉM lại mã của lượt kiểm hiện tại"; thêm vế `0001` vào TC-37 |
| 20 | P3 | §3.1 hàng 7 vế (1) (180) ↔ Q23 (1023) | *(vòng 3 #7, còn một phần)* Q23 đã nói `DaXacMinh` ở vế (2). Vế (1) (địa chỉ `DaXacMinh` có ledger trỏ dòng bị thay) vẫn chưa nói có soát lại hay không | Mở rộng Q23 sang vế (1), hoặc ghi lý do bỏ |
| 21 | P3 | §0 Phạm vi (40) | *(vòng 3 #8, còn)* "✅ Value object `Address` … snapshot 3 cấp cũ" | "entity `Address` (`ADDR-004`) + ledger giữ đầu vào 3 cấp" |
| 22 | P3 | §15.2 F5 (1034); §15.3 giả định 2 (1044) | *(vòng 3 #9, còn)* `DatasetVersionId` **NOT NULL** | Đổi sang `SchemaVersion` |
| 23 | P3 | §13 NFR-08 (835); §11 (775) | *(vòng 3 #10, còn)* "assembly `AddressHub`", `IRepository<AddrWard, Guid>` | `eCommerce.Address.*`; `IRepository<AddrNormalizationLedger, Guid>` |
| 24 | P3 | §14.2 fixture TC-03 (940) | *(vòng 3 #11, còn)* `Assert.NotEqual("79", …)` ngược với chú thích | `Assert.Equal` |
| 25 | P3 | §14.2 fixture TC-09 (923-926) | *(vòng 3 #13, còn)* `.Value with { LegacyDistrictCode… }` không có trên `DiaChiSnapshotDto` | Viết lại theo `D-96` (6) |
| 26 | P3 | §6.2 `[Authorize]` (505) | *(vòng 3 #21, còn)* `ChannelAppService.cs:27`; thật ở `:28` (grep lượt này) | Sửa số dòng |
| 27 | P3 | §15.2 F4 (1033) | *(vòng 3 #14, còn)* `src/eCommerce.Domain/Entities/Orders/Order.cs:10-52` không tồn tại | Đổi sang `modules/Sales/eCommerce.Sales.Domain/Order.cs:11` |
| 28 | P3 | §14.3 DoD (971) | *(vòng 3 #15, còn)* "phủ hết … **10 NFR**" nhưng NFR-01/06/09 không có TC | Thêm TC hoặc ghi 🔶 benchmark chưa đo |
| 29 | P3 | §5.2 CHECK (398); §8.1 #4 (660) | *(vòng 3 #16, còn)* `LegalBasis <> ''` cho qua chuỗi chỉ có khoảng trắng | `btrim("LegalBasis") <> ''` |
| 30 | P3 | §8.2 #5 (676) | *(vòng 3 #17, còn)* "ghim id lô ánh xạ một lần ở đầu UoW" — sau `D-100` không còn "một lô hiện hành" | Đọc nhóm `IsActive` trong một truy vấn |
| 31 | P3 | Mục đối chiếu hàng `D-96` (1084) | *(vòng 3 #18, còn)* "TC-09 sửa theo" nhưng fixture chưa sửa (#25) | Sửa fixture hoặc ghi "chỉ §14.1" |
| 32 | P3 | §15.2 F7 (1036) | *(vòng 3 #19, còn)* bằng chứng còn `eCommerceHttpApiHostModule.cs:127`, `eCommerceConsts.cs:104` | Cập nhật `:222,237`; đo lại `LockKeys.InventorySku` |
| 33 | P3 | §14.2 fixture TC-17 (958-964) | *(vòng 3 #20, còn)* "`AddrUnitMappingImports` đếm riêng" nhưng không có câu đếm; không kiểm `WHERE`; tên test `ledger_va_review` | Thêm câu đếm; đổi tên test (xem thêm #8) |
| 34 | P3 | §13 NFR-06 (833); §1 NFR-06 (95), Post-Condition (90) | *(vòng 3 #22, còn)* "100 % / đúng một bản ghi" trái với `Strict` (0 dòng) và hàng 3 (nguồn `NghiNgo` không ghi) | Nêu hai ngoại lệ |
| 35 | P3 | §3.1 sơ đồ (158-166) + bảng (174-182) | *(vòng 3 #23, còn)* Thiếu hàng cho nguồn đã có mã v1 có ánh xạ duy nhất ⇒ `TaoHoacLayAsync` v2; nhãn cạnh `DaChuanHoa → NghiNgo` chỉ nói ca hàng 7 | Thêm hàng + nhãn |
| 36 | P3 | §6.2 `GetReviewQueueAsync` (510) | *(vòng 3 #24, còn)* Input `PagedAndSortedResultRequestDto` mà lọc `ReasonCode`/ngày/`Status` | DTO riêng |
| 37 | P3 | §3.1 hàng 2/5 (175, 178); §12 (808) ↔ §7.1 (617) | *(vòng 3 #25, còn)* Hai ETO (`AddressChangedEto` + `AddressNormalizedEto`) cho một thay đổi | Ghi quan hệ hai ETO |
| 38 | P3 | §6.3 thông điệp `0001` (593) | *(vòng 3 #26, còn một phần)* thông điệp vẫn dùng `{dataset}` | `{dataset}` ⇒ `{schemaVersion}` |
| 39 | P3 | §15.4 (1054-1056); header (11) | *(vòng 3 #27, còn một phần)* Lịch sử nay có 2 dòng 19/11 nhưng thiếu dòng cho các sửa 18/11 (`D-92`…`D-103`); header vẫn ghi `v0.1` | Thêm dòng 18/11 |

## Không kiểm được

- D4: máy không có `psql`/`docker`/`postgres` (`which` → không có, không có `C:\Program Files\PostgreSQL`, `C:\Program Files\Docker`). Vì vậy **chưa chạy**:
  - unique `NULLS NOT DISTINCT (TenantId, LedgerId)`;
  - 2 index partial `WHERE "Outcome" = 0 / = 1`;
  - CHECK `"MappingKind" in (0,1,2)`;
  - CHECK `RawText/StreetLine`;
  - CHECK `CK_AddrNormalizationLedger_Outcome`;
  - câu SQL nhúng trong fixture TC-17.

  Tài liệu không có khối `sql`/`powershell`/`bash`.
- Kết luận MVCC ở #2 (phiên khác không thấy `DELETE` chưa commit) là hành vi chuẩn của PostgreSQL ở READ COMMITTED. Lượt này **chưa đo** vì không có DB.
- Phạm vi lượt này: chỉ kiểm các mục ORCH báo đã đổi và mâu thuẫn chéo do chúng sinh ra. Các mục đó là:
  - §1 BR-17; §3.1 hàng 5/7; §4.2; §5.1; §5.2; §6.1; §6.2; §6.3;
  - §7.2; §10.1; §11; §12; §14.1 (TC-36, TC-39…TC-47); §14.3; §14.4;
  - §15.1 (Q12/Q15/Q17/Q18/Q19/Q21-Q23); §15.4; mục đối chiếu.

  Các P3 còn mở từ vòng 3 được chép lại, không kiểm lại từ đầu.
- Ngoài phạm vi: ghi chú `D-99`/`D-107` ① ở `addr-004` §6.1 (làm ngoài vùng ghi vai `ba`). Nguồn pháp lý §15.5 và Q21: không tra web.

## Vòng trước (chỉ vòng 3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 3 có 0 P1, 1 P2, 26 P3. Vòng 1 và vòng 2: mọi P1/P2 đã đóng ở vòng 2–3; các P3 còn mở đã chép qua vòng 3 và nay chép lên bảng trên.

| # vòng 3 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | còn — `D-107`, sửa trước WP-15; `D-111` đóng được một phần (chép lên #1) | P2 |
| 2 | còn (chép lên #15) | P3 |
| 3 | còn (chép lên #16) | P3 |
| 4 | còn (chép lên #17) | P3 |
| 5 | còn (chép lên #18) | P3 |
| 6 | còn (chép lên #19) | P3 |
| 7 | còn một phần — Q23 phủ vế (2) (chép lên #20) | P3 |
| 8 | còn (chép lên #21) | P3 |
| 9 | còn (chép lên #22) | P3 |
| 10 | còn (chép lên #23) | P3 |
| 11 | còn (chép lên #24) | P3 |
| 12 | **đã đóng** — vế (3) hàng 7 (`D-111`) + TC-44: review `NoMapping` gặp nhóm mới được mở lại | P3 |
| 13 | còn (chép lên #25) | P3 |
| 14 | còn (chép lên #27) | P3 |
| 15 | còn (chép lên #28) | P3 |
| 16 | còn (chép lên #29) | P3 |
| 17 | còn (chép lên #30) | P3 |
| 18 | còn (chép lên #31) | P3 |
| 19 | còn (chép lên #32) | P3 |
| 20 | còn (chép lên #33; thêm #8) | P3 |
| 21 | còn (chép lên #26) | P3 |
| 22 | còn (chép lên #34) | P3 |
| 23 | còn (chép lên #35) | P3 |
| 24 | còn (chép lên #36) | P3 |
| 25 | còn (chép lên #37) | P3 |
| 26 | còn một phần (chép lên #38) | P3 |
| 27 | còn một phần — có dòng 19/11, thiếu dòng 18/11 (chép lên #39) | P3 |
