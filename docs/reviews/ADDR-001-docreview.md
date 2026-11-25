# Doc review — ADDR-001 Tra cứu đơn vị hành chính 2 cấp (tỉnh + xã) và 3 cấp cũ

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` |
| Ngày · vòng | 18/11/2026 · vòng 2 · **26/11/2026 · kiểm nhanh v0.5 (phiên N76)** |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 2: dòng 1–1069 (toàn văn). Kiểm nhanh v0.5: đọc §0 (16–100), §1 (118–136), §8 (597–641), §11–12 (686–728), §14 (748–827, 880–924), §15 (940–989), mục "Đối chiếu" (1028–1077); grep **toàn văn** theo từng ràng buộc BA đã sửa (không đọc lại toàn văn — theo `/doc-gate` §D2 / `D-141`) |
| Máy | `check-feature-doc.ps1 -WP14` → **KẾT QUẢ: DAT** (16/16 heading `## 0.`…`## 15.`, 14/14 BR có test, mục "Đối chiếu mô hình Nhanh" dòng 1028) · `check-agent-artifact.ps1 -Kind docreview` → **exit 0** · mermaid (3 sơ đồ) PASS ở vòng 2, sơ đồ không đổi ở v0.5 nên không chạy lại |

Tổng: **0 P1 · 0 P2 mở · 19 P3 mở**. Lý do verdict: 0 P1 và 0 P2 (< 3) ⇒ `DOC-OK`.
**Hai P2 của vòng 2 đã đóng ở v0.5** (chi tiết ở mục "Vòng kiểm nhanh v0.5"). Không phát sinh P1/P2 mới từ 4 mục BA đổi ở v0.5.
19 P3 mở là các mục vòng 1/vòng 2 còn lại (BA ghi ở §15.4 v0.5 là để vòng sau); v0.5 đã đóng thêm P3 #4, #5, #8.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Lấy từng ràng buộc **đã sửa ở v0.5** rồi grep cả tài liệu tìm chỗ trái: `TimeoutException`, `requiresNew`, `UoW bao lô`, `cùng UoW`, `bản ghi lô`, `Q4`, `hàm sinh khoá`, `D-93`. Đối chiếu E5 (dòng 127) ↔ §8.1 #5 (dòng 607) ↔ §12 dòng 714/726 ↔ BR-009 (dòng 128); §8.2 #1 (dòng 616) ↔ TC-25 (dòng 778) | v0.5: **0 phát hiện mới** (2 P2 đã đóng — xem "Vòng kiểm nhanh v0.5") |
| D2 chéo tài liệu / code / D-xx | Đọc `D-93` (`docs/nhanh/decisions.md` dòng 107): production = file công bố của cơ quan nhà nước + `SourceHash` + bản ghi lô. Đối chiếu §0.1 D4 (52) · §11 (693–694) · §14.4 DoR (902) · BR-010 (128) · TC-22 ca g (790) · Q2 (916) — **khớp `D-93`, không lệch**. Xác nhận code qua đồ thị: `LockErrorCodes.KhongGianhDuocKhoa = "eCommerce:Lock:0001"` (`shared/eCommerce.Kernel/Locking/LockErrorCodes.cs:20`), trùng `eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa` (`eCommerceConsts.cs:545`); ánh xạ 409 ở `eCommerceHttpApiHostModule.cs:377` (đã xác nhận ở vòng 2) | v0.5: 0 phát hiện mới cho 4 mục; P3 #6 (dẫn `:222` thay vì `:237`) **còn mở** |
| D3 cơ chế thực thi | Với ràng buộc v0.5 sửa: bản ghi lô **từ chối** phải ghi UoW riêng `requiresNew: true` trước khi NÉM ⇒ cái gì đỏ: TC-21 ca (f) (dòng 774: sau khi request trả lỗi mở UoW mới đọc bản ghi từ chối) + TC-24 ca (e) (dòng 777: sau `0008` bản ghi từ chối còn đọc được, kiểm `requiresNew`) | **TC-21 ca (f) + TC-24 ca (e) có thật** (đọc dòng 774/777) ⇒ ràng buộc đã có cơ chế đỏ; 0 phát hiện mới |
| D4 SQL chạy được | Grep rào mã: `mermaid`, `json`, `csharp` — **không** có khối `sql`/`powershell`/`bash` (không đổi từ vòng 2) | không có khối sql — 0 phát hiện |
| D5 test/probe/tệp có thật | Mở đúng dòng các test mà v0.5 dẫn: TC-21/TC-24/TC-25 viết trong §14.1 (dòng 774/777/778); `LockErrorCodes.cs:20` + `eCommerceConsts.cs:545` xác nhận qua `codegraph_explore` (khớp giá trị `eCommerce:Lock:0001`) | 0 phát hiện mới (P3 #9/#17 còn mở — test reflection/gọi worker) |
| D6 nhãn tin cậy | Soát nhãn ở các mục v0.5: `D-93` ghi "cơ quan/định dạng/đường dẫn file = CHƯA XÁC ĐỊNH" (đúng — không ✅ khi chưa đo) ở §0.1 D4 + §11 + §14.4 DoR; `ImportDatasetAsync`/guard/bảng lô vẫn ⬜ WP-15+ | 0 phát hiện mới |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14 trên mục đã đổi: guard sai thì làm gì (E5 + §12 đủ nhánh tên/cha/mã), chống trùng + tên khoá (`D-82` một hằng `address:sync-catalog`), `D-93` không đổi guard (TC-22 ca g ghi lỗ hở). Mục "Đối chiếu mô hình Nhanh" (dòng 1028) có thêm hàng `D-93` (dòng 1040) và 2 hàng "Vòng 2 #1/#2" (dòng 1072–1073); thân đã sửa theo — kiểm bằng grep | 0 phát hiện mới; P3 #7 (hàng `#12`/Q4) còn mở |

## Vòng kiểm nhanh v0.5 — 26/11/2026 · phiên N76

Phạm vi: 4 mục BA đã đổi ở v0.5 (`docs/reviews/ADDR-001-docreview.md` vòng 2 + §15.4 dòng v0.5), kiểm nhanh theo `/doc-gate` §D2 / `D-141` (không đọc lại toàn văn; với mỗi ràng buộc đã sửa, grep toàn tài liệu tìm chỗ trái).

| # | Phạm vi | Kết quả | Bằng chứng |
|---|---|---|---|
| 1 | P2 #1 vòng 2 — bản ghi lô **từ chối** ghi UoW riêng `requiresNew: true`, commit **trước khi NÉM** | **ĐÃ ĐÓNG** — không còn chỗ nào nói bản ghi lô từ chối nằm trong **cùng** UoW với lô | E5 (dòng 127), §8.1 #5 (607), §12 dòng `0008` (714) + "Chiến lược transaction" (726), BR-009 (128) đều ghi "UoW **riêng** `requiresNew: true`, commit **TRƯỚC khi NÉM**"; các câu "UoW bao lô" còn lại chỉ để **giải thích** vì sao phải tách (không mâu thuẫn). TC-21 ca (f) dòng 774 + TC-24 ca (e) dòng 777 kiểm lại được: sau request trả lỗi, mở UoW mới đọc bản ghi lô trạng thái từ chối |
| 2 | P2 #2 vòng 2 — khoá bận ⇒ `BusinessException(eCommerce:Lock:0001)` → 409, KHÔNG `TimeoutException` | **ĐÃ ĐÓNG** — không còn "`TimeoutException` → 409" trong thân | §8.2 #1 (616): "**ném** `BusinessException(LockErrorCodes.KhongGianhDuocKhoa)`, mã `eCommerce:Lock:0001` (`LockErrorCodes.cs:20`) → **409** (`eCommerceHttpApiHostModule.cs:377`); **không** dùng `TimeoutException`". TC-25 (778) kiểm `ex.Code == "eCommerce:Lock:0001"` + HTTP 409. Grep `TimeoutException` chỉ còn: dòng 616 (câu "không dùng"), 778 (câu "không"), 962 (lịch sử v0.5), 1073 (cột "Tài liệu cũ nói" của bảng vòng sửa — bản ghi lịch sử, không phải mâu thuẫn sống) |
| 3 | Các mục BA đổi theo `D-93` (§0.1 D4, §11, §14.4 DoR, BR-010/TC-22 ca g) | **Không tạo mâu thuẫn D1/D2 mới** | §0.1 D4 (52), §11 Seed/Nguồn seed (693–694), §14.4 DoR (902), BR-010 (128), TC-22 ca g (790), Q2 (916) đều thống nhất: production = **file công bố của cơ quan nhà nước** + `SourceHash` + bản ghi lô (`D-78`); **không** dùng `provinces.open-api.vn` ở production; cơ quan/định dạng/đường dẫn = **CHƯA XÁC ĐỊNH**. Khớp `D-93` (`decisions.md` dòng 107). `D-93` **không** đổi guard (TC-22 ca g vẫn ghi lỗ hở) — nhất quán |
| 4 | Không có P1 mới; P2 mới < 3 | **ĐẠT** | 0 P1, 0 P2 mới. 19 P3 mở là các mục còn lại từ vòng 1/2 (không đổi mức) |

Ghi chú: `D-141` (dòng 155) đặt điều kiện tạm duyệt `ADDR-001` = "DOCREV kiểm nhanh v0.5" cho 2 P2 vòng 2 + nguồn ĐVHC production (`D-93`). Phiên này kiểm nhanh ⇒ **2 P2 đã đóng, `D-93` đã áp đúng**; phần địa chỉ trên hoá đơn (nợ `D-77`, họ `EINV-*`) vẫn **ngoài** tạm duyệt.

## Phát hiện

<!-- Chỉ phát hiện MỞ. P1/P2 vòng 2 đã đóng, KHÔNG chép lại ở đây (xem "Vòng kiểm nhanh v0.5" + "Vòng trước"). Cột 2 = "Mức", script đếm P1/P2 theo cột này. -->

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 3 | P3 | §6.2 dòng 515 + nợ `Map` ở dòng 509/514/925 | `0008` sẽ bị `ImportDatasetAsync` ném tới client nhưng cột HTTP để "(chưa gán)", và danh sách nợ `Map` WP-15 chỉ có `0002`/`0007`. Không `Map` thì có thể ra 403 | Thêm `0008` vào nợ `Map` (ví dụ `Conflict`) cùng chỗ với `0002`/`0007` |
| 6 | P3 | DoR dòng 903, Q4 dòng 918, bảng Đối chiếu dòng 1069 | Dẫn `eCommerceHttpApiHostModule.cs:222` làm bằng chứng `KeyPrefix` của **lock**. Thật ra `:222` là `AbpDistributedCacheOptions` (cache); lock nằm ở `:237`. §8.2 dòng 630 đã ghi đúng | Đổi thành `:237` (hoặc `:222,237`, ghi rõ cache/lock) |
| 7 | P3 | Bảng vòng sửa, dòng 1066 (#12) | "Q4 trỏ `XC-003` §5.4 (đóng cần `D-xx`)" mâu thuẫn với dòng 1069 (Q4 đã đóng theo tiền lệ, không cần `D-xx`) | Sửa câu hoặc ghi "(thay bởi dòng Q4 bên dưới)" |
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
- Ánh xạ HTTP 409 ở `eCommerceHttpApiHostModule.cs:377`: xác nhận qua đồ thị + bằng chứng vòng 2 (grep `LockErrorCodes` KHÔNG có `Map` cho `TimeoutException`); **chưa** khởi động host đo mã thật của `BusinessException` `Lock:0001` trong phiên này.
- #1: kết luận dựa trên ngữ nghĩa UoW mặc định của ABP (exception đi ra khỏi app service ⇒ rollback) và việc đọc tài liệu. Code `ImportDatasetAsync` chưa có (WP-15+), nên **chưa chạy**.
- Khung `csharp` §14.2: **chưa biên dịch/chạy** (giữ nguyên kết luận vòng 2: chữ ký `TraDonViRequest`/`MaXaHaiPhienBanDto` khớp, riêng lớp cơ sở test không khớp — P3 #16).
- Không tái kiểm gọi API thật `provinces.open-api.vn` và `redis-cli MONITOR` (cần mạng/host chạy). Tài liệu đã gắn nhãn 🔶 chưa đo.
- Ghi chú ngoài phạm vi (không tính phát hiện): đóng Q4 theo tiền lệ là hợp lệ (`XC-003` §5.4 hệ quả #2, đã duyệt ở biên bản §2.1). Riêng `XC-003` còn tự mâu thuẫn (D4 dòng 47 và `LockKeyPrefix.Build` §6.1 dòng 275-281, TC-07 dòng 443) — nên giao kiểm `XC-003` riêng.

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
| 10 | đã đóng — `D-82`: một hằng `address:sync-catalog` cho cả hai đường | P2 |
| 11 | đã đóng — BR-010 ghi đúng phạm vi guard, TC-22 ca g là lỗ hở ghi nhận | P2 |
| 12 | đã đóng — bằng chứng `TaxErrorCodes.cs:45-46` cập nhật, F4/Q4 đóng theo tiền lệ. Phần sót là #6, #7 (P3) | P2 |
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
