# Doc review — ADDR-003 Chuẩn hoá địa chỉ khách hàng: convert 3 cấp ↔ 2 cấp, xử lý ca sáp nhập tỉnh

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-003-chuan-hoa-dia-chi-3-cap-2-cap.md` |
| Ngày · vòng | 18/11/2026 · vòng 3 (kiểm nhanh sau vòng 2; cùng tệp) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1060 (toàn văn; đọc theo đoạn 1-200, 200-429, 430-679, 680-879, 880-1060) + `decisions.md` dòng 117 (`D-103`, nguyên văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi; 16/16 heading, 16/16 BR có test, mục đối chiếu ở dòng 1038, 12 câu `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 212 khối, 0 lỗi |

Vòng 3 có **0 P1, 1 P2, 26 P3**. Cả **5 P2 của vòng 2 đã đóng**, mỗi P2 có test đi kèm:

- #1 `SourceHash`: unique đổi thành **partial** `WHERE "Outcome" = 0`. Đã kiểm khớp ở 369, 279, A5 (92), §7.2 (609), §8.2 (654, 656), §11 (755), §12 (780-782), TC-13 (837), TC-32 (856).
- #2 `InvalidInput`: dùng khoá riêng `…:Invalid`, tra khoá trước, và coi `23505` của chính khoá đó là "đã có" (177, 776). Có TC-37.
- #3 nguồn `NghiNgo`: hàng 3 đã loại nguồn `NghiNgo`, và A4 chỉ chọn `ChuaChuanHoa` chưa có review (92, 176). TC-08 đã thêm vế cho ca này.
- #4: theo `D-103`, hàng 7 ghi `NeedsReview` + `MappingSuperseded = 5`. `Outcome = 4` đã bỏ, có CHECK `in (0,1,2,3)` (180, 345, 349, 384). Có TC-38, và TC-07 loại trừ giá trị này.
- #5 số đếm: badge = `TotalCount` review `Pending` (244, 492, 746).

Có **1 P2 mới**, nằm trong hàng 7 vừa sửa: dòng `NeedsReview` của job soát lại không nói `InputSnapshot`/`From*Code` lấy từ đâu (#1). Theo bảng §6, verdict là `DOC-OK` (0 P1, 1 P2 < 3), nhưng **#1 nên sửa trước WP code**.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Chỉ kiểm các mục BA đã sửa và các mâu thuẫn chéo do chúng sinh ra. Grep `ReNormalizeRequired\|Outcome = 4\|MappingSuperseded\|:Invalid\|requiresNew\|WHERE "?Outcome\|Applied` (output `-o`): `ReNormalizeRequired` chỉ còn ở Q20 (989) và mục đối chiếu (1054-1055), cả hai là chữ lịch sử, hợp lệ. `Outcome = 4` chỉ còn ở 180/384/862 (dạng "bị CHECK chặn"). Đã so lần lượt: hàng 3 (176) với sơ đồ `NghiNgo --> NghiNgo` (164), A4 (92), Post-Condition (90) và NFR-06 (95, 811). Hàng 7 (180) với cột `InputSnapshot` NOT NULL (344), §10.1 cột trái (723), A3/§4.2 bước 5 (92, 241) và TC-31 (855). Badge (746) với `GetReviewQueueAsync` (492), bộ lọc §10.1 (722) và nhãn §10.2 (736). Hàng 4 (177) với bảng đăng ký khoá §8 (676-682) | 6 phát hiện (#1, #2, #4, #5, #6, #26) |
| D2 chéo tài liệu / code / D-xx | Đọc nguyên văn `D-103` (`decisions.md:117`) và đi theo danh sách "BA sửa cùng PR" (a)–(e): §5.2 `Outcome`/`ReasonCode`/CHECK, hàng 7, dòng 186, §11 dòng 761, TC-07, TC-38, Q20, mục đối chiếu. **Đủ cả**, kể cả `MappingImportId` = lô mới và "1 đích vẫn `NeedsReview`". Code: `Address.cs:177` (`XacMinh` nhận cả `NghiNgo`), `Address.cs:204` (`DanhDauNghiNgo` chỉ từ `DaChuanHoa`/`DaXacMinh`, khớp hàng 3); `AddressDirectory.cs:256` (`CoDungDuoc = TrangThai != ChuaChuanHoa`); `ChannelAppService.cs:28` | 1 phát hiện (#7) |
| D3 cơ chế thực thi | Hàng 4: thử lại / đua ghi ⇒ TC-37. Hàng 7: `Outcome = 4` ⇒ CHECK + TC-38. Unique partial của lô ⇒ TC-13/TC-17. Hàng 3: nguồn `NghiNgo` ⇒ TC-08. Kiểm thêm xem cái gì đỏ khi dòng soát lại mang `InputSnapshot` sai: **không có** (TC-36 chỉ kiểm tập khoá, TC-38 không kiểm nội dung) | 1 phát hiện (#1) |
| D4 SQL chạy được | Rào code: `mermaid` (101, 158, 198), `csharp` (404, 866), `json` (513, 527, 552). **Không có khối `sql`/`powershell`/`bash`** (grep "```sql" → 0). Index partial `NULLS NOT DISTINCT … WHERE "Outcome" = 0` và CHECK `Outcome in (0,1,2,3)` chỉ là mô tả, **chưa chạy**: `which psql docker` → không có cả hai | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | `ChannelAppService.cs:28` là `[Authorize]` thật, §6.2 (487) vẫn ghi `:27`. `AddressDirectory.cs:256` khớp. `Address.cs:204` khớp. TC-37/TC-38 có trong §14.1, DoD "38 test case" khớp TC-01…TC-38 | 1 phát hiện (#21, còn từ vòng 2) |
| D6 nhãn tin cậy | Chỉ kiểm các mục đã đổi. Q20 ghi "✅ Đã chốt — `D-103`" khớp dòng `TẠM CHỐT (DEC)`. Không có ✅ mới nào thiếu số đo | 0 phát hiện mới |
| D7 14 quy tắc + đối chiếu Nhanh | #3 "guard sai thì làm gì": hàng 3/4/7 đã có cột. #5 index idempotency: lô, ledger và khoá `:Invalid` đều có unique; riêng khoá `:Invalid` chưa vào bảng đăng ký (#3). **#9 đếm ≠ dòng hiển thị**: badge đã đúng; còn bộ lọc `Confirmed`/nhãn §10.2 (#4, #5). Mermaid: 0 lỗi. Mục đối chiếu (1038-1060) có hàng `D-103` và hàng "docreview vòng 2" | 3 phát hiện (#3, #4, #5) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §3.1 hàng 7 (180); §11 (761) ↔ ledger `InputSnapshot` NOT NULL (344), §10.1 cột trái (723), A3 / §4.2 bước 5 (92, 241) | Dòng `NeedsReview`/`MappingSuperseded` do job ghi không có đầu vào thật. Tài liệu không nói `InputSnapshot` (NOT NULL, allowlist) và `From*Code` của dòng này lấy từ đâu. Đường tới lỗi: nguồn thô X bị `Address.ChuanHoa` ghi đè `SoNhaDuong`/`DiaChiDayDu` (`Address.cs:267-268`), nên đầu vào gốc chỉ còn ở dòng ledger đầu tiên. BE có thể điền `InputSnapshot` từ dữ liệu hiện tại của X (mã B) hoặc `{}`. Khi đó cột "địa chỉ GỐC" ở §10.1, thứ CSKH dựa vào để quyết, hiện mã B hoặc trống. Sau `ConfirmAsync` ⇒ `Address` Y (mã C), `ToThreeLevelAsync(Y)` đi `ResultAddressId → SourceEntityId` = X (đã 2 cấp) ⇒ `0305`, dù 3 cấp gốc vẫn có trong ledger. TC-36 chỉ kiểm tập khoá, TC-38 không kiểm nội dung ⇒ không gì đỏ | Ghi ở hàng 7: dòng soát lại **chép** `InputSnapshot` + `From*Code` từ dòng ledger đã sinh kết quả hiện tại (dòng có `ResultAddressId` = địa chỉ đó và `MappingImportId` bị thay). Ghi `ToThreeLevelAsync` đi được qua bước nối này. Thêm vế vào TC-38: `InputSnapshot` của dòng soát lại = của dòng gốc; `ToThreeLevelAsync(Y)` trả 3 cấp gốc |
| 2 | P3 | §3.1 hàng 7 cột Guard (180) ↔ cột Hành động (180), TC-38 (862) | Cột Guard viết "resolver thấy **1** dòng ⇒ **tự động** tới C". Cột Hành động (`D-103`) lại viết "kể cả khi tra mới ra đúng 1 đích: vẫn `NeedsReview`, không tự `Normalized`". Câu đầu chỉ đúng cho **lượt chuẩn hoá mới** của mã A (TC-31), không đúng cho địa chỉ `DaChuanHoa` đang soát lại | Thêm chữ: "lượt chuẩn hoá **mới** của mã A ⇒ tự động tới C; địa chỉ đã `DaChuanHoa` ⇒ review (`D-103`)" |
| 3 | P3 | §8 bảng đăng ký idempotency key (676-682) ↔ §3.1 hàng 4 (177), §12 (776) | Khoá mới `Address:Normalize:{tenantId}:{sourceEntityId}:{inputHash}:Invalid` chưa có trong bảng đăng ký, mà bảng này là nơi tra khoá | Thêm một hàng "Chuẩn hoá — đầu vào sai (`InvalidInput`)" |
| 4 | P3 | §6.2 `GetReviewQueueAsync` (492) ↔ §10.1 bộ lọc `Status` (722) | Tập gốc là `Address.TrangThai ∈ {ChuaChuanHoa, NghiNgo}`. Review nguồn **thô** đã xác nhận (địa chỉ thành `DaChuanHoa`) hoặc địa chỉ đã `XacMinh` (`NghiNgo → DaXacMinh`, `Address.cs:177`) rời tập gốc ⇒ lọc `Confirmed` thiếu dòng, khác số trên ledger | Đặt tập gốc = dòng ledger `NeedsReview` (mới nhất theo `SourceEntityId`); chỉ lọc `TrangThai` khi `Status = Pending`, hoặc ghi rõ giới hạn |
| 5 | P3 | §10.2 nhãn trạng thái (736) ↔ §10.3 ghi chú (746), hàng 5/6 (178-179) | Nhãn "`ChuaChuanHoa`/`NghiNgo` ⇒ *đang chờ xác nhận*" vẫn hiện trên `Address` v1 sau khi review đã xác nhận / từ chối (v1 giữ `NghiNgo`/`ChuaChuanHoa`, không còn review `Pending`). Đây là cùng lỗi đếm ≠ hiển thị mà #5 vòng 2 đã sửa ở badge | Nhãn dựa trên review `Pending`; review đã kết thúc ⇒ "đã từ chối — chọn lại địa chỉ" / "đã thay bằng địa chỉ mới" |
| 6 | P3 | §3.1 hàng 4 (177) | "NÉM lại đúng mã (suy lại từ input bằng **kiểm định dạng thuần**)": riêng `0001` (đúng định dạng, không thuộc `SchemaVersion`) thì kiểm định dạng không suy ra được. TC-37 chỉ thử `"ABCDE"` (`0002`) | "NÉM lại mã của lượt kiểm hiện tại (định dạng ⇒ `0002`, tra danh mục ⇒ `0001`)"; thêm vế `0001` vào TC-37 |
| 7 | P3 | §3.1 hàng 7 (180); §11 (761) ↔ hàng 3 (176), `Address.cs:204` | Job soát lại chỉ quét `DaChuanHoa`. Địa chỉ `DaXacMinh` (vẫn mang mã của dòng ánh xạ bị thay) không được soát, mà không có câu nào nói đây là cố ý. Hàng 3 thì coi `DaXacMinh` là "nguồn đã có mã" | Ghi rõ: `DaXacMinh` có được soát lại không, kèm lý do |
| 8 | P3 | §0 Phạm vi (40) | *(vòng 1 #19, còn)* "✅ Value object `Address` … snapshot 3 cấp cũ" trái ADR-08 và `D-96` (6) | "entity `Address` (`ADDR-004`) + ledger giữ đầu vào 3 cấp" |
| 9 | P3 | §15.2 F5 (1000); §15.3 giả định 2 (1010) | *(vòng 1 #20, còn)* "cột `DatasetVersionId` **NOT NULL**", "BR-02 (`DatasetVersionId`)" | Đổi sang `SchemaVersion` |
| 10 | P3 | §13 NFR-08 (813); §11 (756) | *(vòng 1 #21, còn một phần)* "assembly `AddressHub`", `IRepository<AddrWard, Guid>` (bảng không tạo) | `eCommerce.Address.*`; `IRepository<AddrNormalizationLedger, Guid>` |
| 11 | P3 | §14.2 fixture TC-03 (909) | *(vòng 1 #22, còn)* `Assert.NotEqual("79", map.TraTheoMa("79")); // chính nó vẫn là 79`: assert ngược với chú thích | `Assert.Equal` |
| 12 | P3 | §3.1 hàng 3 + A4 (92, 176); hàng 7 (180); Q12 (985) | *(vòng 2 #10, còn; nay rõ hơn)* A4 cố ý bỏ địa chỉ `ChuaChuanHoa` đã có review, và hàng 7 chỉ quét `DaChuanHoa`. Lô **thêm nhóm mới** vì thế không bao giờ xử lý lại các ca `NoMapping` đang chờ (`Candidates` rỗng ⇒ chỉ từ chối được) | Lô áp thành công ⇒ mở lại review `Pending` `ReasonCode = NoMapping` thuộc nhóm mới; hoặc mở rộng Q12 sang `NoMapping` |
| 13 | P3 | §14.2 fixture TC-09 (892-895) | *(vòng 1 #23, còn)* `r.Normalized!.Value with { LegacyDistrictCode = null, … }`: `DiaChiSnapshotDto` là record class, không có `.Value`/`Legacy*` (`AddressContracts.cs:41-54`) | Viết lại theo `D-96` (6) |
| 14 | P3 | §15.2 F4 (999) | *(vòng 1 #26, còn)* `src/eCommerce.Domain/Entities/Orders/Order.cs:10-52` không tồn tại; §11 đã dùng `modules/Sales/eCommerce.Sales.Domain/Order.cs:11` | Đổi đường dẫn hoặc gắn "lỗi thời" |
| 15 | P3 | §14.3 DoD (940) | *(vòng 1 #28, còn)* "phủ hết … **10 NFR**", nhưng NFR-01/06/09 không có TC ở §14.1 | Thêm TC, hoặc ghi "NFR-01/06/09 đo bằng benchmark — 🔶 chưa đo" |
| 16 | P3 | §5.2 CHECK (381); §8.1 #4 (641) | *(vòng 1 #24, còn)* `LegalBasis <> ''` cho qua chuỗi chỉ có khoảng trắng | `btrim("LegalBasis") <> ''` (cả `AddrUnitMappingImports`) |
| 17 | P3 | §8.2 #5 (657) ↔ `D-100` (332) | *(vòng 2 #15, còn)* "Normalizer **ghim id lô** ánh xạ một lần ở đầu UoW": sau `D-100` không có "một lô hiện hành" để ghim | Đọc các dòng `IsActive` của nhóm trong **một** truy vấn; `MappingImportId` = `ImportId` của nhóm |
| 18 | P3 | Mục đối chiếu hàng `D-96` (1048) | *(vòng 2 #16, còn)* "TC-09 sửa theo", nhưng fixture (892-895) chưa sửa (#13) | Sửa fixture hoặc ghi "chỉ §14.1" |
| 19 | P3 | §15.2 F7 (1002) | *(vòng 1 #27, còn một phần)* bằng chứng còn `eCommerceHttpApiHostModule.cs:127`, `eCommerceConsts.cs:104` | Cập nhật `:222,237`; đo lại `LockKeys.InventorySku` |
| 20 | P3 | §14.2 fixture TC-17 (929-933) ↔ §14.1 TC-17 (841), §5.2 (369) | *(vòng 1 #25, còn một phần)* Chú thích ghi "`AddrUnitMappingImports` đếm riêng" nhưng không có câu đếm đó. Index của lô nay là **partial**, mà fixture không kiểm `indexdef` có `WHERE ("Outcome" = 0)`. Tên test vẫn `ledger_va_review` (phân tích tĩnh, chưa chạy) | Thêm câu đếm `IX_AddrUnitMappingImports%` = 1 có `NULLS NOT DISTINCT` **và** `WHERE`; đổi tên test |
| 21 | P3 | §6.2 `[Authorize]` (487) | *(vòng 1 #31, còn một phần)* `ChannelAppService.cs:27`; `[Authorize]` thật ở `:28` (grep) | Sửa số dòng |
| 22 | P3 | §13 NFR-06 (811); §1 NFR-06 (95), Post-Condition (90) ↔ `Strict` (417, 775), hàng 3 (176) | *(vòng 2 #21, còn, nay rộng hơn)* "**100 %** lần chuẩn hoá có ledger" / "Mọi lần chuẩn hoá có **đúng một** bản ghi". Nhưng `Strict` NÉM `0302` với 0 dòng, và hàng 3 mới cố ý **không** ghi dòng cho nguồn `NghiNgo` đã có review | Nêu rõ hai ngoại lệ ở NFR-06 và Post-Condition |
| 23 | P3 | §3.1 sơ đồ (158-166) + bảng (174-182) | *(vòng 1 #33, còn)* Không có hàng cho nguồn **đã có mã** v1 có ánh xạ duy nhất ⇒ `TaoHoacLayAsync` v2 (Basic Flow 4). Sơ đồ thiếu cạnh `DaChuanHoa → NghiNgo` cho ca mơ hồ của hàng 3 | Thêm hàng + cạnh |
| 24 | P3 | §6.2 `GetReviewQueueAsync` (492) | *(vòng 1 #32, còn)* Input `PagedAndSortedResultRequestDto` nhưng lọc `ReasonCode`/ngày/`Status`/`SourceEntityType` (722) | DTO riêng `GetAddressReviewQueueInput` |
| 25 | P3 | §3.1 hàng 2/5 (175, 178); §12 (786) ↔ §7.1 (598) | *(vòng 1 #35, còn)* `ApplyNormalizationAsync` phát `AddressChangedEto`, hàng 2/5 phát thêm `AddressNormalizedEto` ⇒ hai sự kiện cho một thay đổi | Ghi quan hệ hai ETO (hoặc chỉ phát một) |
| 26 | P3 | §6.3 đoạn mã lỗi (570); thông điệp `0001` (575) | *(vòng 1 #36, còn)* "số cuối dải `03xx` = số kế tiếp" mâu thuẫn với bảng đã ấn định số; thông điệp `0001` còn `{dataset}` | "dùng đúng số trong bảng"; `{dataset}` ⇒ `{schemaVersion}` |
| 27 | P3 | §15.4 (1020) | *(vòng 1 #29, còn)* Lịch sử chỉ có v0.1 dù thân đã sửa theo `D-92`…`D-103` | Thêm dòng v0.2 — 18/11/2026 — WP-14 |

## Không kiểm được

- D4: máy không có `psql`/`docker` (`which psql docker` → không có). Vì vậy **chưa chạy** index partial `NULLS NOT DISTINCT … WHERE "Outcome" = 0`, CHECK `CK_AddrNormalizationLedger_Outcome` và câu SQL nhúng trong fixture TC-17. Tài liệu không có khối `sql`/`powershell`/`bash`.
- Vòng 2 #2 (đã đóng): `23505` trong UoW `requiresNew` được bắt và coi là "đã có" — mới kết luận qua đọc thiết kế, **chưa đo** (bảng chưa tạo).
- Phạm vi vòng này: chỉ kiểm các mục BA báo đã đổi (§1 A4/A5, §3.1 hàng 3/4/7, §4.2, §5.2, §6.2, §7.2, §8.2, §10.3, §11, §12, §14, Q20, mục đối chiếu) và mâu thuẫn chéo do chúng sinh ra. Các P3 còn mở được chép lại, không kiểm lại từ đầu.
- Ngoài phạm vi: ghi chú `D-99` ở `addr-004` §6.1 vẫn "Chưa làm — người/ORCH" (1051). Nguồn pháp lý §15.5: không tra web.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

Vòng 1 (1 P1, 17 P2, 18 P3): P1 và 17 P2 đã đóng ở vòng 2. Các P3 vòng 1 còn mở đã chép lên bảng trên (#8–#11, #13–#16, #19–#21, #23–#27).

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — unique partial `WHERE "Outcome" = 0` (369), A5/§12/TC-13/TC-32 khớp | P2 |
| 2 | đã đóng — khoá `…:Invalid` + tra trước + `23505` cùng khoá = đã có (177, 776), TC-37 | P2 |
| 3 | đã đóng — hàng 3 loại `NghiNgo`, A4 chỉ chọn `ChuaChuanHoa` chưa có review, TC-08 | P2 |
| 4 | đã đóng — `D-103`: `NeedsReview` + `MappingSuperseded = 5`, `Outcome = 4` bỏ + CHECK, TC-38 (sinh #1 mới) | P2 |
| 5 | đã đóng — badge = `TotalCount` review `Pending` (244, 492, 746) | P2 |
| 6 | còn (chép lên #8) | P3 |
| 7 | còn (chép lên #9) | P3 |
| 8 | còn (chép lên #10) | P3 |
| 9 | còn (chép lên #11) | P3 |
| 10 | còn (chép lên #12) | P3 |
| 11 | còn (chép lên #13) | P3 |
| 12 | còn (chép lên #14) | P3 |
| 13 | còn (chép lên #15) | P3 |
| 14 | còn (chép lên #16) | P3 |
| 15 | còn (chép lên #17) | P3 |
| 16 | còn (chép lên #18) | P3 |
| 17 | còn (chép lên #19) | P3 |
| 18 | còn, mở rộng (chép lên #20) | P3 |
| 19 | còn (chép lên #27) | P3 |
| 20 | còn (chép lên #21) | P3 |
| 21 | còn, mở rộng (chép lên #22) | P3 |
| 22 | còn (chép lên #23) | P3 |
| 23 | còn (chép lên #24) | P3 |
| 24 | còn (chép lên #25) | P3 |
| 25 | còn (chép lên #26) | P3 |
