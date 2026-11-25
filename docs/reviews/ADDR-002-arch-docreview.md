# Doc review — «ADDR-002» phần `arch` (WP-18, hàng W18-1)

**VERDICT: DOC-OK** (vòng 2 — 0 P1, 1 P2, 16 P3; vòng 1 là `DOC-CHANGES`)

| | |
|---|---|
| Tài liệu | `docs/features/address/ADDR-002-arch.md` (483 dòng — vòng 2, sau `D-232`…`D-237`) |
| Ngày · vòng | 26/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N77) |
| Đã đọc | vòng 1: dòng 1–446 (toàn văn). Vòng 2: đọc lại **toàn văn 1–483** + `D-232`…`D-237` (dòng 246–251); đối chiếu SRS `addr-002-…md` §3/§5.2/§6.1/§8/§11/§12/§14/§15.1 |
| Máy | `check-feature-doc.ps1 -Path …-arch.md -WP14` → exit **1** (3 `LOI`: thiếu heading 0/11–15, không BR, thiếu "Đối chiếu mô hình Nhanh") — **đúng chủng loại** `-arch`; `tools/kiem-tat-ca.ps1` không gọi (grep = 0). Tài liệu **vẫn chưa** có blockquote nêu việc này — P3 #10 |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep toàn văn "cấm/chỉ/không/duy nhất"; đối chiếu §5.1 (271–274) ↔ §5.2 (309–311) ↔ §5.3 (334–337); §3.1 (85–102) ↔ §3.2 (106–110) ↔ §5.4 (371–374); §4.1 (198) ↔ §5.1 (286); §6 (403–410) ↔ §3.4 (148–158) | 2 P3 mới (#2, #3); các P3 cũ #11/#12/#15 còn |
| D2 chéo tài liệu / code / D-xx | đối chiếu `D-232`…`D-237` (đọc dòng gốc), `D-83`…`D-94`; SRS §5.2/§6.1/§14; code thật (đồ thị + `Read`); `abp-module-rules` M2/A2, `ef-migration-postgres` M1/M7/M8 | 1 P2 (#1); P3 #4/#5/#6/#7/#8/#9 |
| D3 cơ chế thực thi | ca test cho từng BR mới, NC-1…NC-5, map `D-233`, UoW, unique `NULLS NOT DISTINCT` | 1 P2 (#1: thiếu probe runtime `D-233` ⑥) |
| D4 SQL / lệnh chạy được | **không** có khối ```` ```sql ````/```` ```bash ````/```` ```pwsh ```` (chỉ 2 khối ```` ```csharp ````: 114, 347; 2 khối text: 51, 260); chưa chạy PostgreSQL | **không có khối sql** — 0 phát hiện |
| D5 tên test/probe/tệp có thật | mở các `file:dòng` **mới**: `AddressDirectory.cs:24` ✓, `CustomerDirectory.cs:28` ✓, `CustomerDirectory.cs:10-23` ✓, `CustomerDtoTuEntity` (`CustomerDirectory.cs:100`) ✓, `Migrations/20261009070214_Initial.Designer.cs:43-48` (`.IsConcurrencyToken()`) ✓; `CarrierAddressMappingAppService.cs:10-19` ✓, `AddressDtos.cs:4-11` ✓, `Synchronizer.cs:156` ✓; `grep -c "TODO\|FIXME"` = **0** | tất cả đúng; 1 P3 (#7) |
| D6 nhãn tin cậy | đo lại: `0509`/`0510` vẫn = 0 trong repo; `SourceHash`/`MauCanNguoiAnhXa` chỉ là **kế hoạch** (chưa có ở `modules/Address`) | 1 P3 (#6) |
| D7 14 quy tắc + đối chiếu Nhanh | #1 BR testable · #2 NFR có ngưỡng · #3 guard sai thì gì · #4 chống trùng cửa vào · #5 index idempotency · #6 tên khoá · #7 tiền · #8 endpoint NCC · #9 đếm ≠ dòng · #10 N/A có lý do · #11–#14 Mermaid (không có). `-arch` ngoài mẫu 16 mục | P3 #10/#11/#13/#14 |

## Truy vấn đồ thị

- `graph: mcp__codegraph__codegraph_explore "CarrierAddressErrorCodes CarrierAddressMapping CarrierAddressMappingSynchronizer CarrierAddressSyncWorker CarrierAddressMappingResolver"` (vòng 1) → 53 ký hiệu / 5 tệp; xác nhận `0501…0508`, `LapKeHoach` `:95-199`, `ApDung` `:205-236`, worker `:57-192`, `Tra` `:38-103`.
- Vòng 2 dùng thêm `Read` đúng dòng cho các mẫu mới: `AddressDirectory.cs:24`, `CustomerDirectory.cs:10-45,95-114`, `Migrations/20261009070214_Initial.Designer.cs:43-48`, `CustomerDirectory.cs:28,100`; kết luận "app service hiện thực interface in-process ⇒ POST-đọc" lấy từ comment đã đo trong `CustomerDirectory.cs:10-23`, không suy đoán.

## Phát hiện (vòng 2)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §7.1 dòng 427 + dòng 425 ↔ `D-233` ⑥ | `D-233` ⑥ yêu cầu **probe/`WebApplicationFactory`**: `GET TraMaHangAsync` với mã lạ ⇒ **404** + `error.code = eCommerce:Address:0501` (hôm nay 403). §7.1 chỉ có quét **tĩnh** "đúng 6 hằng `05xx`" và Endpoint chỉ test `GetUnmappedCodesAsync`/`XacMinhAsync` — **không** ca runtime nào chứng minh map đúng status/mã. | Thêm một dòng Endpoint: `GET TraMaHangAsync` mã lạ ⇒ 404 + `error.code = eCommerce:Address:0501`; `0502`/`0503`/`0504`/`0510` ⇒ 409; `0505` ⇒ 400. |
| 2 | P3 | §6 dòng 410 ↔ §3.4 dòng 151, `D-233` ① | "**Không** endpoint tra ngược cho FE" trong khi `ResolveCarrierAddressCodeAsync` là **method public** của app service ⇒ ABP sinh route `POST` (chính `D-233` ① dùng lý do này để map `0510`). | Sửa: "route `POST` **tồn tại** do method public; FE **không dùng** route này — chỉ `SHP-*` qua hợp đồng". |
| 3 | P3 | §5.1 dòng 274 ↔ §5.2 dòng 309–311 | `LapKeHoach(..., daChuanHoa)` trong khi `LapKeHoach` nhận `DanhMucDiaChiHang` (`Synchronizer.cs:95-100`), còn §5.2 nói `LapKeHoach` **gọi** `ChuanHoaVaKiemMa` ở đầu hàm ⇒ hoặc sai kiểu tham số, hoặc helper chạy 2 lần (idempotent nhưng thừa). | Chốt một: (a) đổi `LapKeHoach` nhận `IReadOnlyList<DonViDiaChiHang>` đã chuẩn hoá; hoặc (b) worker truyền `danhMuc` gốc và bỏ lời gọi trùng. |
| 4 | P3 | §9 dòng 465; §3.1 dòng 83–89 | "1 interface hẹp + **3 DTO**" nhưng §3.1 liệt kê **5** kiểu (interface + request + result + 2 DTO con); `D-232` ①(1) cũng ghi "đúng 3 kiểu" rồi ①(2) thêm 2 DTO con. | Ghi "3 kiểu cấp 1 + 2 DTO con = 5 kiểu" (hoặc "5 kiểu"). |
| 5 | P3 | §4.1 dòng 211–212 ↔ `D-235` ① | `D-235` ① ghi tên dài `IX_AddrCarrierSyncRuns_TenantId_CarrierCode_StartedAtUtc` / `…_Outcome_FinishedAtUtc` (65 byte, bị PostgreSQL cắt); §4.1 đã đổi sang tên ngắn ≤ 63 byte. | Ghi rõ `D-235` dùng **tên mô tả**, tên thật theo §4.1; hoặc cập nhật dòng `D-235` khi có dịp. |
| 6 | P3 | §5.3 dòng 338 | "đồng bộ với cột `SourceHash` của `ADDR-001`" — cột đó là **kế hoạch** (`ADDR-001-arch.md:222`; SRS `addr-001` dòng 436 nói bảng lô **chưa có trong code**) và có `CHECK ~ '^[0-9a-f]{64}$'`; ADDR-002 không nêu CHECK. | Ghi rõ "theo thiết kế `ADDR-001-arch.md:222` (chưa có trong code)" + cân nhắc cùng CHECK. |
| 7 | P3 | §3.1 dòng 97–101, §5.4 dòng 373 | "mapper dùng chung (khuôn `CustomerDtoTuEntity`)" nhưng `Chuyen`/`ChuyenDonVi` của app service là `private static` (`CarrierAddressMappingAppService.cs:114-119`) ⇒ lớp riêng không gọi được nếu không tách. `CustomerDtoTuEntity` là `internal static class` (`CustomerDirectory.cs:100`). | Ghi rõ: tách `private static` thành `internal static class CarrierAddressMappingDtoTuEntity` (khuôn `:100`), cả hai lớp gọi. |
| 8 | P3 | §3.2 dòng 109 | "method public của app service (`D-89` ①(4), `D-232` ①(5))" — `D-89` ①(4) là "`TraMaHangAsync` giữ nguyên"; việc thêm method là `D-89` **①(1)**. | Sửa trích dẫn thành `D-89` ①(1). |
| 9 | P3 | SRS `addr-002-…md` §5.2 dòng 314, §6.1 dòng 389 | SRS vẫn ghi `MauCanNguoiAnhXa` "đề xuất `jsonb`, chưa chốt" (`D-234` chốt `text[]`) và `CarrierAddressCodeResultDto(CarrierAddressMappingDto, AdministrativeUnitDto)` (D-232 chốt **bản sao hợp đồng** `…ContractDto`). | BA cập nhật SRS cùng PR (D-234 → `text[]`; D-232 → 2 DTO biên). Không phải lỗi của `-arch`, nhưng phải theo. |
| 10 | P3 | Header dòng 3–10 | Vẫn thiếu blockquote nói `-arch` ngoài mẫu 16 mục/"Đối chiếu mô hình Nhanh" và `kiem-tat-ca.ps1` không gọi `check-feature-doc` (round-1 #11). | Thêm 2–3 dòng như `PRC-002-arch.md:14-16`. |
| 11 | P3 | §2 sơ đồ dòng 57 | Vẫn ghi `Address.Application.Contracts ──▶ Domain.Shared`; `.csproj` Contracts chỉ có `Volo.Abp.Ddd.Application.Contracts`, `Address.Application.csproj:15` mới ref `eCommerce.Domain.Shared` (round-1 #7). | Sửa sơ đồ: `Address.Application → Domain.Shared`. |
| 12 | P3 | §5.2 dòng 329–330 | Vẫn ghi "`SoCapNhatTen`/`SoDanhDauNghiNgo` … gồm cả việc trên ánh xạ đã `NghiNgo`" — chỉ đúng với `SoCapNhatTen`; dưới `D-87` ①(1) `SoDanhDauNghiNgo` **không** gồm (round-1 #8). | Viết lại: `SoCapNhatTen` gồm cả; `SoDanhDauNghiNgo` chỉ chuyển **mới**. |
| 13 | P3 | §4.3 dòng 242 | Đã sửa "4 index" nhưng vẫn "**thêm** `migrationBuilder.Sql(… NULLS NOT DISTINCT)`"; tiền lệ `20261014042922` **thay** `CreateIndex(unique)` bằng raw SQL (không có `CreateIndex` cho unique) — làm đúng chữ "thêm" ⇒ hai index cùng tên (round-1 #9, một phần). | Ghi "**thay** `CreateIndex(unique: true)` bằng `migrationBuilder.Sql(…)`". |
| 14 | P3 | §4.3 dòng 246 | "`pg_dump` **khuyến nghị**" vs `ef-migration-postgres` M9 "Chạm DB có dữ liệu ⇒ `pg_dump` trước" (bắt buộc) (round-1 #12). | Nói rõ **bắt buộc** hoặc lý do miễn có người duyệt. |
| 15 | P3 | §4.1 dòng 198 ↔ §5.1 dòng 286 | `SourceHash` null "khi `Loi` trước `LapKeHoach`" vs "mọi exception khác ⇒ `SourceHash = null`" (round-1 #13). | Thống nhất: chỉ null khi chưa có danh mục. |
| 16 | P3 | §7.1 dòng 423 | Cột "Phủ" ghi `BR-012/015/016` nhưng ca có "0 `https://`/endpoint hãng hard-code" là **BR-011** (round-1 #14). | Ghi `BR-011`; thêm dòng "BR-001/004/006/010 phủ bởi SRS TC-21/04/09/14–15". |
| 17 | P3 | §3.4 dòng 158 | "FE **0** … (**chưa kiểm lại** `apps/admin-web`)" — đo lại trong phiên = **0**, nhưng nhãn D6 chưa đạt (round-1 #15). | Ghi "đo: 0 (grep `apps/admin-web`)". |

## Vòng trước (vòng 1 — đóng/chưa đóng)

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đã sửa / lý do còn |
|---|---|---|---|
| 1 | đã đóng | P1 | §5.1 dòng 271–274 nay **quét `ChuanHoaVaKiemMa` trước** rồi mới `Băm`; §5.3 dòng 334–337 băm trên danh mục **đã chuẩn hoá** (không còn null/rỗng/trùng); §5.1 dòng 300–301 ghi rõ thứ tự. Đường NRE → `Loi` thay `BiChan 0509` đã bị đóng. |
| 2 | đã đóng | P2 | §3.1 dòng 87–89: `CarrierAddressCodeResultDto(CarrierAddressMappingContractDto Mapping, AdministrativeUnitContractDto Unit)` — **giữ hình dạng `D-89`**; `D-232` chốt, không còn DTO 12 trường phẳng. |
| 3 | đã đóng | P2 | §3.1 dòng 97–101: lớp **riêng** `CarrierAddressResolver : ICarrierAddressResolver, ITransientDependency`; app service **KHÔNG** kế thừa (`D-232` ①(4)(5)); bài học `CustomerDirectory.cs:10-23` được trích đúng. |
| 4 | đã đóng | P2 | §8 R5 dòng 453: "**Có** concurrency token … `ConcurrencyStamp` … `IsConcurrencyToken()` … `AbpDbConcurrencyException`" — khớp `CarrierAddressMapping.cs:82` + `Designer.cs` (`.IsConcurrencyToken()`). |
| 5 | đã đóng | P2 | §7.1 dòng 424: ca `DaApDung_ghi_dong_lo_nem_thi_0_anh_xa_doi` ("`InsertAsync` dòng lô ném ⇒ rollback ⇒ 0 ánh xạ đổi"); NC-2 dòng 436 trỏ đúng ca. |
| 6 | đã đóng | P3 | `AQ2` chốt `D-233`; §6 dòng 403 nay ghi "từ `D-233` … 404/409/400, không còn 403". |
| 7 | còn (đã chép #11) | P3 | §2 dòng 57 chưa sửa. |
| 8 | còn (đã chép #12) | P3 | §5.2 dòng 329–330 chưa sửa. |
| 9 | còn một phần (đã chép #13) | P3 | "4 index" đã sửa; "thêm đoạn Sql" chưa sửa. |
| 10 | đã đóng | P3 | §4.1/§4.2 dùng tên ≤ 63 byte + ghi rõ tên dài 76 byte bị PostgreSQL cắt. |
| 11 | còn (đã chép #10) | P3 | Header chưa có blockquote `-arch`. |
| 12 | còn (đã chép #14) | P3 | `pg_dump` vẫn "khuyến nghị". |
| 13 | còn (đã chép #15) | P3 | `SourceHash` null condition vẫn lệch. |
| 14 | còn (đã chép #16) | P3 | "Phủ" vẫn ghi BR-012/015/016 cho ca BR-011. |
| 15 | còn (đã chép #17) | P3 | FE "0" vẫn "chưa kiểm lại". |

## Không kiểm được

- **D4:** không có khối `sql`/`bash`/`pwsh` để chạy; migration `Add_Addr002_CarrierSyncRuns` **chưa sinh** ⇒ chưa kiểm diff expand-only trên PostgreSQL thật.
- **Test thực thi:** đây là thiết kế; ca §7.1/NC §7.2 chưa chạy; `negative-control.ps1` chưa chạy.
- **`check:mermaid`:** tài liệu không có sơ đồ Mermaid ⇒ N/A.

## Ghi chú tích cực (không phải phát hiện)

- **4/4 `file:dòng` mới thêm đều đúng**: `AddressDirectory.cs:24`, `CustomerDirectory.cs:28`, `CustomerDirectory.cs:10-23`, `Migrations/20261009070214_Initial.Designer.cs:43-48` (`.IsConcurrencyToken()`); `CustomerDtoTuEntity` thật ở `CustomerDirectory.cs:100`. `grep -c "TODO\|FIXME"` = **0**.
- **`D-232`…`D-237` được áp đúng**: DTO `Mapping`+`Unit` (D-232), 6 mã map (D-233), `MauCanNguoiAnhXa text[]` (D-234), đúng 6 method public + giữ 2 index (D-235), `SyncRunId`+`LatestRunOutcome` cùng null (D-236), 0 cột tên cũ (D-237).
- **P1 vòng 1 đóng đúng bản chất**: không chỉ đổi câu chữ — thứ tự "quét → băm → `LapKeHoach`" được ghi thành luật và ca test "phần tử null ⇒ 0509, không NRE" vẫn giữ.
- **`§9` nay là bảng quyết định** (không còn câu mở); `grep AQ1…AQ6` chỉ còn ở §9/§10.

**VERDICT: DOC-OK** (vòng 2, 26/11/2026 — 0 P1, 1 P2 < 3, 16 P3)
