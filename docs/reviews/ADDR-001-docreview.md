# Doc review — ADDR-001 Tra cứu đơn vị hành chính 2 cấp (tỉnh + xã) và 3 cấp cũ

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` |
| Ngày · vòng | 18/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–1069 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi: 16/16 heading, 14/14 BR có test, mục đối chiếu Nhanh ở dòng 1027) · `npm --prefix tools run check:mermaid` → exit 0 (3 sơ đồ PASS: dòng 136, 204, 242) · `kiem-link-tai-lieu.ps1` → `PASS - 0 tham chieu chet` |

Tổng: **0 P1 · 2 P2 · 22 P3**. Lý do verdict: 0 P1 và 2 P2 (< 3). Cả **13** P1/P2 của vòng 1 **đã đóng** (bảng "Vòng trước").
Hai P2 mới đều nằm ở phần BA viết lại theo `D-81`/`D-82`, nên sửa trước khi người duyệt, dù chưa đủ để đổi verdict.
P3 gồm 13 mục vòng 1 còn mở (BA ghi ở §15.4 v0.3 là để lại vòng sau) và 9 mục mới.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Lấy từng ràng buộc mới/chốt của `D-77…D-82` rồi grep cả tài liệu: `ValidateAsync`, `AddressLevelPolicy`, `EffectiveTo`, `Address.Manage`, `0004`/`0008`, `hàm sinh khoá`, `Q4`, `KeyPrefix`, `TimeoutException`, `bản ghi lô`, `rollback`, `Unit of Work`. Đối chiếu E5 (dòng 127) ↔ §8.1 #5 (dòng 607) ↔ §12 dòng 714/726 ↔ BR-009 | 7 phát hiện (#1, #4, #5, #7, #10, #11, #23) |
| D2 chéo tài liệu / code / D-xx | Đọc `D-78…D-82` (`docs/nhanh/decisions.md` dòng 92-96). Đối chiếu Basic Flow 3–6 ↔ `AdministrativeUnitAppService.cs:43-62` + `AdministrativeUnitResolver.cs:92-150` (`ThuTra` không ném ⇒ chỉ `TheoSo` ở `:100` ném, khớp). §8.2 ↔ `AdministrativeUnitSyncWorker.cs:38,94-104`, `eCommerceHttpApiHostModule.cs:217-238,376-377`, `LockErrorCodes.cs:20`, `XC-003` §5.4 dòng 252-256 + §6.2 dòng 314. Q4 đóng theo tiền lệ ↔ biên bản `bien-ban-duyet-gd1-2026-10-29.md` §2.1 dòng 33 (`XC-003` đã duyệt). `D-81` ↔ `AdministrativeUnitSynchronizer.cs:112-209` (hàng rào 1-3 ở `:127-151`, nhánh `MoLai` ở `:168`, so `Name` ở `:174`, hàng rào 4 ở `:190-206`, `DoiTen` không căn cứ ở `:236`) | 4 phát hiện (#2, #3, #6, #26) |
| D3 cơ chế thực thi | Với BR mới/viết lại (BR-002/003/005/007/009/010), hỏi "cái gì đỏ khi vi phạm": TC-05/17 (khung có `NgoaiLeCoLyDo`), TC-23 (grep `IRepository<Address, Guid>` trong `src`/`modules`/`test` ⇒ đúng 2 lớp `AddressAppService.cs:25`, `AddressDirectory.cs:26`), TC-24, TC-25, TC-21 ca a–e. Kiểm vế "lượt bị chặn ghi bản ghi lô từ chối" của `D-81` có test nào phủ đường `ImportDatasetAsync` không | 1 phát hiện (#1) |
| D4 SQL chạy được | Grep rào mã: `mermaid` (dòng 135, 203, 241), `json` (482, 492), `csharp` (797) — không có khối `sql`/`powershell`/`bash` | không có khối sql — 0 phát hiện |
| D5 test/probe/tệp có thật | Mở đúng dòng: `PostgreSqlSchemaTests.cs:235,240,296` (3 index đích danh), `AddressDbContextModelCreatingExtensions.cs:16,22,28,56,89,125,150-151` (`CK_AddrAddresses_DaChuanHoaCoMa`), `CategoryTreeTests.cs:272-280` (`Khoa_cay_danh_muc_KHONG_chua_tien_to_ecommerce`), `AddressDtos.cs:32` (`MaXaHaiPhienBanDto(int SchemaVersion, …)`), `AddressDirectory.cs:56-77`, `AdministrativeUnitSyncWorker.cs:107` (`private DongBoAsync`), `DbSetRegistrationTests.cs:54`; grep `UnitOfWork` trong `modules/Address` = 0 | 2 phát hiện (#9, #20) |
| D6 nhãn tin cậy | Soát nhãn ở bảng BR §0.2 (BR-002 nay 🔶, BR-003 ✅ có `PostgreSqlSchemaTests`), §8.2 khối Q4 (🔶 chưa đo `MONITOR`), DoR §14.4 | 1 phát hiện (#21) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14 trên mục đã đổi: guard sai thì làm gì (E5, §12 dòng 714-716: đủ 3 nhánh tên/cha/mã), chống trùng + tên khoá (`D-82` một hằng, đúng tên chuỗi), Mermaid PASS; mục "Đối chiếu mô hình Nhanh" có thêm bảng vòng sửa + dòng `D-81`, `D-82`, Q4; kiểm thân đã sửa theo từng dòng bảng | 7 phát hiện (#8, #12, #13, #14, #15, #24, #25) — phần lớn là P3 vòng 1 còn mở |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §8.1 #5 (dòng 607) + §12 "Chiến lược transaction" (dòng 726) ↔ E5 (dòng 127), BR-009 (dòng 128), §12 dòng 714 | Tài liệu vừa yêu cầu bản ghi lô nằm **trong cùng** một UoW transactional với lô ("một Unit of Work transactional bao cả hai bước **và** bản ghi lô"), và `ImportDatasetAsync` "chạy trong UoW của ABP", vừa yêu cầu lượt bị `0008`/hàng rào chặn **phải ném lỗi** và **phải còn** "bản ghi lô trạng thái từ chối". Ở đường file (đường production duy nhất theo `D-78`), exception đi ra khỏi app service thì UoW của ABP rollback luôn bản ghi từ chối. Đường tới lỗi: BE làm đúng §8.1 #5 (ghi bản ghi lô trong UoW) rồi đúng `D-81` (ném `0008` cho client), kết quả là lô bị từ chối không để lại dấu vết, BR-009 hỏng im lặng. Không test nào bắt được: TC-21 ca (d) chỉ có đường job (worker bắt lỗi ở `AdministrativeUnitSyncWorker.cs:155`, không ném tiếp), còn TC-24 không kiểm bản ghi lô | Ghi rõ ở §8.1 #5 + E5 + §12: bản ghi lô **từ chối** phải ghi trong UoW riêng (`requiresNew: true`, commit) **trước khi** ném. Thêm TC-21 ca (f): `ImportDatasetAsync` bị `0008` hoặc `0601…0604` thì sau khi request trả lỗi vẫn đọc được bản ghi lô trạng thái từ chối. TC-24 kiểm thêm bản ghi này |
| 2 | P2 | §8.2 #1 (dòng 616) | Khi không giành được khoá, `ImportDatasetAsync` "ném lỗi (`TimeoutException` → 409)". Trong repo, 409 cho khoá bận là `BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)` = `eCommerce:Lock:0001` (`LockErrorCodes.cs:20`), được ánh xạ ở `eCommerceHttpApiHostModule.cs:377` (cũng là quy ước `XC-003` §6.2 dòng 314). `TimeoutException` không có `Map` nào. Làm theo tài liệu thì client nhận mã khác 409 (đọc theo mặc định ABP là 500, **chưa chạy**), trái lời hứa "409, client thử lại được". TC-25 chỉ đòi "NÉM", không kiểm mã | Sửa thành "ném `BusinessException` `eCommerce:Lock:0001` (`KhongGianhDuocKhoa`) → 409 (`eCommerceHttpApiHostModule.cs:377`)". Cho TC-25 kiểm `ex.Code` |
| 3 | P3 | §6.2 dòng 515 + nợ `Map` ở dòng 509/514/925 | `0008` sẽ bị `ImportDatasetAsync` ném tới client nhưng cột HTTP để "(chưa gán)", và danh sách nợ `Map` WP-15 chỉ có `0002`/`0007`. Không `Map` thì có thể ra 403 (chính dòng 520 đã nói) | Thêm `0008` vào nợ `Map` (ví dụ `Conflict`) cùng chỗ với `0002`/`0007` |
| 4 | P3 | §8.2 khối hiện trạng, dòng 639 | "(Q4 toàn hệ thống vẫn mở)" trái Q4 `Đã đóng — theo tiền lệ` (dòng 918) và DoR dòng 903 | Bỏ vế "Q4 vẫn mở" |
| 5 | P3 | §8.2 khối hiện trạng, dòng 640 | "Khoá nạp danh mục chung … là thiết kế chưa có (hàm sinh khoá)" trái `D-82` và dòng 625 ("**không** hàm sinh khoá nhận tham số" — là hằng) | Đổi thành "hằng `AddressLockKeys.CatalogImport` chưa có" |
| 6 | P3 | DoR dòng 903, Q4 dòng 918, bảng Đối chiếu dòng 1069 | Dẫn `eCommerceHttpApiHostModule.cs:222` làm bằng chứng `KeyPrefix` của **lock**. Thật ra `:222` là `AbpDistributedCacheOptions` (cache); lock nằm ở `:237`. §8.2 dòng 630 đã ghi đúng | Đổi thành `:237` (hoặc `:222,237`, ghi rõ cache/lock) |
| 7 | P3 | Bảng vòng sửa, dòng 1066 (#12) | "Q4 trỏ `XC-003` §5.4 (đóng cần `D-xx`)" mâu thuẫn với dòng 1069 (Q4 đã đóng theo tiền lệ, không cần `D-xx`) | Sửa câu hoặc ghi "(thay bởi dòng Q4 bên dưới)" |
| 8 | P3 | Header dòng 11 | Phiên bản vẫn ghi `v0.2 — 17/11/2026`, trong khi §15.4 đã có v0.3, v0.4 (18/11/2026) | Cập nhật header |
| 9 | P3 | TC-11 ca (b), dòng 764 | Ca này bảo chạy `AdministrativeUnitSyncWorker.DongBoAsync`, nhưng method là `private` (`AdministrativeUnitSyncWorker.cs:107`) nên test không gọi trực tiếp được | Ghi điểm vào test thật (`DoWorkAsync` qua worker context, hoặc tách service nạp ở WP-15) |
| 10 | P3 | §4.1 DFD, dòng 222 | Nút `P3` vẫn ghi "ValidateAsync". Dòng `D-79` của bảng Đối chiếu chỉ ghi đã sửa sequence §4.2 | Đổi nhãn thành `XacMinhDiaChiAsync` |
| 11 | P3 | §11 dòng "Rollback", dòng 697 | "Cấm rollback theo cách UPDATE hồi tố (D8/BR-007)" còn dẫn D8 gốc, đã bị `D-77` thay | Viết lại theo `D-77` (danh tính bất biến; tên đổi tại chỗ qua `DoiTen`) |
| 12 | P3 | §0.1 D3 (dòng 51), Q7 (dòng 921) | *(vòng 1 #14, còn)* Còn "`AddressLevelPolicy` phải từ chối…" / "Ảnh hưởng UI và `AddressLevelPolicy`", chưa đánh dấu bỏ theo `D-80` | Đánh dấu bỏ như các mục khác |
| 13 | P3 | §0.1 D6 (dòng 54), F5 (dòng 938) | *(vòng 1 #15, còn)* `ConfigureAddressHub()`: tên thật là `ConfigureAddress` (§6.1 dòng 452 đã ghi đúng) | Sửa tên |
| 14 | P3 | dòng 453, 938, 970 (`eCommerceDbContext.cs:48-52`) · dòng 939 (`:141`) | *(vòng 1 #16, còn một phần; §15.5 dòng 976 đã sửa)* Số dòng lệch: đoạn trích "has not been registered" thật ở `:279-281`; `AbortOnConnectFail` thật ở `:251` | Cập nhật số dòng |
| 15 | P3 | TC-01…TC-03 (dòng 754-756) | *(vòng 1 #17, còn)* Input kiểu cũ: `GetProvincesAsync("v2")`, `provinceCode = 1`, `districtCode = 1`. Thật là `int schemaVersion` và mã chuỗi `01`/`001` (`ChuanHoaMa("1")` ném `0002`) | Đổi input sang `2`, `"01"`, `"001"` |
| 16 | P3 | §14.2 dòng 799 | *(vòng 1 #18, còn)* Lớp cơ sở `eCommerceTestBase<eCommerceEntityFrameworkCoreTestModule>` không có trong repo | Ghi hạ tầng test thật sẽ dùng |
| 17 | P3 | dòng 90, 793, 938 (TC-13) · §11 dòng 692 | *(vòng 1 #19, còn một phần; BR-003 dòng 98 đã ánh xạ `PostgreSqlSchemaTests`)* Vẫn ghi TC-13 "chưa có", trong khi `DbSetRegistrationTests.MOI_entity_eCommerce_trong_model_phai_co_DbSet` (`DbSetRegistrationTests.cs:54`) đã có. §11 vẫn đòi "thêm một test so tên index", trong khi `PostgreSqlSchemaTests.cs:235` đã kiểm | Ánh xạ hai test này |
| 18 | P3 | §0.2 dòng 90, §14.1 dòng 793, §16 dòng 990 | *(vòng 1 #20, còn)* Số "42 ca" chỉ đếm `test/eCommerce.Tests/Address/`, bỏ sót `AddressSync/` (BR-007 dẫn test ở đó) | Ghi đủ hai thư mục hoặc nói rõ phạm vi đếm |
| 19 | P3 | §12 dòng 707 | *(vòng 1 #22, còn)* Ghi "🚨 **ém lỗi**", nghĩa ngược với hành vi thật (ném `0001`) | Sửa thành "ném lỗi" |
| 20 | P3 | §12 dòng 709 | *(vòng 1 #23, còn)* Phản hồi ghi `404`; mã thật là `0001` → `400` (§6.2 dòng 508) | Sửa `0001` → `400` |
| 21 | P3 | §6.1 dòng 455-456 | *(vòng 1 #24, còn)* Header bảng `Method/Input/Output/Ghi chú` mồ côi ngay trước blockquote `D-79` | Xoá header thừa |
| 22 | P3 | §13 NFR-ADDR-001-006 (dòng 739) | *(vòng 1 #25, còn)* "Không UPDATE hồi tố — 0 lần" chưa viết theo `D-77`, khác NFR-006 ở §1 dòng 129 | Chép NFR-006 mới |
| 23 | P3 | §0.2 dòng 89, §9 dòng 650, 652, Đối chiếu dòng 1048 | *(vòng 1 #26, còn một phần; §13/§14.3 đã sửa)* Tên `eCommerce.Address.Manage`: chuỗi thật là `eCommerce.Addresses.Manage` (`eCommerceConsts.cs:363-368`) | Sửa tên |
| 24 | P3 | §4.1 DFD dòng 232 | *(vòng 1 #28, còn)* Mũi tên từ "File danh mục đã lưu" mang nhãn "chỉ ở dev/test", ngược `D-78` (file đã duyệt là đường production duy nhất) | Chuyển nhãn sang mũi tên nguồn công khai |

## Không kiểm được

- D4: tài liệu không có khối `sql`/`powershell`/`bash` nên không có gì để chạy, không cần PostgreSQL.
- #2: kết luận "`TimeoutException` không ra 409" dựa vào grep (`eCommerceHttpApiHostModule.cs` không có `Map` cho `TimeoutException`; 409 của khoá bận đi qua `:377`). Mã thật mà ABP trả cho `TimeoutException` (500 theo mặc định) **chưa chạy** trên host.
- #1: kết luận dựa trên ngữ nghĩa UoW mặc định của ABP (exception đi ra khỏi app service ⇒ rollback) và việc đọc tài liệu. Code `ImportDatasetAsync` chưa có (WP-15+), nên **chưa chạy**.
- Khung `csharp` §14.2: **chưa biên dịch/chạy**. Đã đối chiếu bằng tay chữ ký `TraDonViRequest`/`MaXaHaiPhienBanDto` (`AddressDtos.cs:14,32`) và kiểu trả về `Task<IReadOnlyList<MaXaHaiPhienBanDto>>` (`AdministrativeUnitAppService.cs:83`): biểu thức `GetGenericArguments()[0].GetGenericArguments()[0]` của TC-17 khớp. Riêng lớp cơ sở thì không khớp (#16).
- Ghi chú ngoài phạm vi (không tính phát hiện): việc đóng Q4 theo tiền lệ là hợp lệ (`XC-003` §5.4 hệ quả #2, dòng 255, đã duyệt ở biên bản §2.1). Tuy nhiên chính `XC-003` còn tự mâu thuẫn: D4 (dòng 47) và `LockKeyPrefix.Build` §6.1 (dòng 275-281, TC-07 dòng 443 kỳ vọng `ecommerce:tax:…`) đều ghép tiền tố ở module. Ai làm theo §6.1 của `XC-003` sẽ ra tiền tố đôi. Nên giao kiểm `XC-003` riêng.
- Không tái kiểm gọi API thật `provinces.open-api.vn` và `redis-cli MONITOR` (cần mạng/host chạy). Tài liệu đã gắn nhãn 🔶 chưa đo.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — Basic Flow 3–6 + Post-condition theo `XacMinhDiaChiAsync` (`HopLe`, chỉ `0005` ném), đã đối chiếu `AdministrativeUnitResolver.cs:100-150`. Phần sót ở DFD nay là #10 (P3) | P1 |
| 2 | đã đóng — Post-condition + A2 theo `D-77` (`IsActive=false`) | P2 |
| 3 | đã đóng — NFR-010 (dòng 743) + DoD (dòng 889): permission riêng | P2 |
| 4 | đã đóng — TC-05/TC-17/khung §14.2 (`NgoaiLeCoLyDo`)/DoD | P2 |
| 5 | đã đóng — BR-003 thu hẹp, bằng chứng `PostgreSqlSchemaTests.cs:235,240,296` khớp | P2 |
| 6 | đã đóng — BR-007/§0.2 ghi 🔶 + nợ WP-15; TC-12 có ca "thiếu căn cứ ⇒ ném" (đỏ hiện nay) | P2 |
| 7 | đã đóng — `D-81` + E5/§12/TC-24 | P2 |
| 8 | đã đóng — §8.1 #5 ⬜ + TC-11 ca (b). Lỗi phụ về cách gọi test nay là #9 (P3) | P2 |
| 9 | đã đóng — §7.3 theo câu `D-78` | P2 |
| 10 | đã đóng — `D-82`: một hằng `address:sync-catalog` cho cả hai đường. Mã lỗi khi khoá bận nay là #2 (P2 mới) | P2 |
| 11 | đã đóng — BR-010 ghi đúng phạm vi guard, TC-22 ca g là lỗ hở ghi nhận | P2 |
| 12 | đã đóng — bằng chứng `TaxErrorCodes.cs:45-46` cập nhật, F4/Q4 đóng theo tiền lệ. Phần sót là #4, #6, #7 (P3) | P2 |
| 13 | đã đóng — BR-005 đổi cửa thật + cơ chế + TC-23 (grep xác nhận đúng 2 lớp) | P2 |
| 14 | còn (đã chép lên bảng Phát hiện, #12) | P3 |
| 15 | còn (#13) | P3 |
| 16 | còn một phần (#14) | P3 |
| 17 | còn (#15) | P3 |
| 18 | còn (#16) | P3 |
| 19 | còn một phần (#17) | P3 |
| 20 | còn (#18) | P3 |
| 21 | đã đóng — BR-002 nay 🔶 kèm ngoại lệ | P3 |
| 22 | còn (#19) | P3 |
| 23 | còn (#20) | P3 |
| 24 | còn (#21) | P3 |
| 25 | còn (#22) | P3 |
| 26 | còn một phần (#23) | P3 |
| 27 | đã đóng — §15.3 #1 ghi `0008` + đổi mã ≠ `0008` | P3 |
| 28 | còn (#24) | P3 |
