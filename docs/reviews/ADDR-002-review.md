# Review code `ADDR-002` — 26/11/2026

- Nhánh `feat/gd3-wp18-dia-chi` · base `7d15f84` (merge-base `origin/tich-hop/gd3`) · head `540ccd2`
- Diff chung của chuỗi WP-18: **9.830 dòng / 77 tệp** (đã trừ `**/Migrations/**Designer.cs`,
  `eCommerceDbContextModelSnapshot.cs`, `schema.sql`); phần `ADDR-002` gồm `CarrierAddressResolver`,
  `CarrierAddressSyncWorker`, 2 entity sổ lô, migration `20261125193330`, 2 testplan/arch, 8 tệp test.
- SRS: `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` (§1 BR-001…016, §5.2/§5.3, §6.1,
  §9 phân quyền, §12, §14) · `docs/features/address/ADDR-002-arch.md` (§4.1–4.3, §7.1/§7.2)
  · `docs/nhanh/decisions.md` `D-83`…`D-92`, `D-232`…`D-237`, `D-243`, `D-248`…`D-251` · `docs/tests/ADDR-002-testplan.md`.
- Vai `rev` chỉ ghi tệp này; **không** sửa code/test/docs khác. Phạm vi/độ lớn diff xem `ADDR-001-review.md` #7 (dùng chung).

**P1 = 0** · P2 = 0 · P3 = 5 · **VERDICT: PASS**

## Đã kiểm

| Nhóm | Cách kiểm (đích danh) | Kết quả |
|---|---|---|
| A1 hợp đồng BR | Đối chiếu 16 BR: không tự tạo ánh xạ (`ApDung` chỉ `CapNhatTen`/`DanhDauNghiNgo`, `CanNguoiAnhXa` không ghi — `CarrierAddressMappingSynchronizer.cs:289-320`); hàng rào 3 đếm **chuyển mới** sang `NghiNgo` (`:255-280`); `0509` gộp rỗng+trùng sau `Trim` (`ChuanHoaVaKiemMa:116-162`); mã hãng chỉ `Trim` (`D-84`); `CapNhatTuDongBo` hạ `NghiNgo` giữ đơn vị đích | Khớp BR-001…016 |
| A2 mã lỗi | `0501`…`0510` đều thuộc `CarrierAddressErrorCodes` (module Address sở hữu), `0509`/`0510` nối tiếp cuối dải; HTTP map đúng 6 mã reachable (`HostModule` `D-233`), **không** map 4 mã job-only | Đúng `D-79`/`D-233` |
| A3 máy trạng thái | `CarrierMappingStatus` chỉ đổi qua `XacMinh`/`DanhDauNghiNgo`/`CapNhatTuDongBo`; `CarrierAddressSyncOutcome` = `0,1,2` (chỉ thêm ở cuối); `TraNguoc` **NÉM** `0510` khi > 1 `DaXacMinh` (`CarrierAddressMappingResolver.cs:141-160`), không chọn bừa | Đúng `D-89` |
| A4 idempotency | `CarrierAddressSourceHash` băm danh mục **đã chuẩn hoá**, sắp `(cấp,mã)` `Ordinal`, không phụ thuộc thứ tự nguồn, không `Guid.NewGuid()`; hash **không** unique (`D-83`) — idempotency là nội dung | Đúng BR-004 |
| A5 ranh giới | `ICarrierAddressResolver` nằm ở `Application.Contracts` **không** tham chiếu `Address.Domain` (csproj chỉ `Volo.Abp.Ddd.Application.Contracts`; enum ở biên là `int`); hiện thực tách lớp `CarrierAddressResolver : ITransientDependency` (không sinh route HTTP thứ hai) | Đúng `D-232`, M2 |
| B1/B2 tiền/số | Không có trường tiền (`BR-016` kiểm reflection); số đếm là `int` (số **dòng/việc**, không phải số lượng `decimal(18,3)`); thời gian `timestamptz` + `ChuanHoaUtc` | N/A tiền |
| B3 append-only | `CarrierAddressSyncRun` + `CarrierAddressSyncRunUnmappedCode : CreationAuditedAggregateRoot<Guid>`, 0 public setter, 0 method đổi, FK cùng module `onDelete: Restrict`; `AddressSyncInvariantTests` kiểm reflection | Đúng BR-012/NFR-004 |
| B4 unique tenant | `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code` viết **SQL thô** `NULLS NOT DISTINCT` (migration `:87-93`); `PostgreSqlSchemaTests` đích danh tên index + đếm **94**; integration `UX_UnmappedCodes_chan_MA_LA_TRUNG…` chứng minh `23505` khi `TenantId = NULL` | Đúng BR-007/`D-88` |
| B5 UoW | Worker mở **một** UoW `requiresNew: true, isTransactional: true` bao `UpdateManyAsync` + dòng lô + dòng con, `CompleteAsync` một lần (`:179-238`); dòng `BiChan`/`Loi` UoW **riêng** (`GhiLoThatBaiAsync:290-334`); test `TC07_BR014_…` + NC-2 đỏ→xanh | Đúng BR-014/E2 |
| B6 migration | `20261125193330` chỉ `CREATE TABLE`×2 + index + FK `Restrict` + SQL thô; `Down` chỉ drop bảng con rồi bảng cha; snapshot +261/−0; `schema.sql` khớp (`text[]` cho `MauCanNguoiAnhXa`) | Expand-only |
| C1 quyền | `[Authorize]` **cấp class** `CarrierAddressMappingAppService.cs:67`; `XacMinhAsync` thêm `[Authorize(Addresses.CarrierMapping.Verify)]` (`:127`); hằng `eCommerceConsts.cs:387-395` + `AddChild(...).MultiTenancySide = Host` (`PermissionDefinitionProvider:179-181`) | Định nghĩa 2 chiều, side Host |
| C2 anonymous/claims | 0 `[AllowAnonymous]` mới trong `modules/Address`; không đổi `MapInboundClaims`/`NameClaimType` | Đúng |
| C3 tenant/PII | 3 bảng `IMultiTenant`, worker chạy host ghi `TenantId = NULL` (`D-92`); dòng lô chỉ lưu `ErrorType = ex.GetType().FullName`, **không** message/stack/secret — test `TC29_BR011_…` khẳng định `SECRET` không lọt; integration `TC19` khẳng định 0 dòng `TenantId` khác `NULL` | Đúng BR-011/NFR-006 |
| C4 secret | Không log token/secret; `ErrorType` chỉ tên kiểu | Đúng |
| C5 webhook | Không có webhook/IPN trong phạm vi | N/A có lý do |
| D1/D2 test | Unit 2136 → **2166** (testplan §2: pass 2 +22, pass 3 +7 worker; `AQ6` +1), integration 105 → **106**; NC **4/4 đỏ→xanh** có dòng `exit`; app service `GetUnmappedCodesAsync` 9 ca; worker 7 ca; reflection/quét mã nguồn 13 ca; BR nào cũng có tên test **có thật** (đã grep 10 tên đại diện) | Đạt |
| D3 cổng | Chạy lại độc lập: mục "Bằng chứng" | Đạt |
| D4 phạm vi | 0 tệp FE/`/api/v1`/adapter/`eCommerce.slnx`/`Directory.Packages.props`/`pnpm-lock.yaml` (cùng lệnh grep với ADDR-001) | 0 tệp cấm |
| D5 tài liệu | `ADDR-002-arch.md` as-built; SRS còn 2 chỗ stale — P3 #3 | Xem P3 #3 |

## Bằng chứng (tự chạy phiên này + nguồn đo lại của ORCH)

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental --nologo -v q` | **0** | **0 error / 9 warning** (0 warning từ tệp `AddressSync`) |
| `dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --no-build --nologo` | **0** | **2166 passed / 0 failed / 0 skipped** |
| `dotnet test test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj --no-build --nologo` | **0** | **106 passed / 0 failed / 0 skipped** — gồm `UX_UnmappedCodes_…` (`23505`) và `TC19` (TenantId NULL) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/ADDR-002-testplan.md -Srs docs/features/address/addr-002-…md` | **0** | `BR trong SRS=16 · chua co trong testplan=0 · KET QUA: DAT` |
| `pwsh -File tools/probe-dia-chi.ps1` (nguồn: ORCH + testplan §3, **không** tự chạy lại vì cần Host/Redis/Docker) | 0 | 10/10 ĐẠT: `0501`→404, `0505`→400, `0502`/`0504`/`0510`→409, `unmapped-codes` 401/200, `xac-minh` 401; **1 CHƯA ĐO ĐƯỢC** = 403 thiếu Verify |

> Migration `20261125193330_Add_Addr002_CarrierSyncRuns.cs` (106 dòng) — 2 `CREATE TABLE`, FK `Restrict`,
> 3 index + 1 unique SQL thô `NULLS NOT DISTINCT`; `Down` drop bảng con → bảng cha; `schema.sql` và
> `PostgreSqlSchemaTests` (94 + tên `UX_…`) khớp; integration xanh trên PostgreSQL 17 thật.

## Phát hiện

| # | Mức | Vị trí | Phát hiện | Bằng chứng | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | P3 | `modules/Address/eCommerce.Address.Application/Sync/CarrierAddressSyncWorker.cs:51-52` | Khoá phân tán khai **inline** (`public const string TienToKhoa = "address:carrier-sync"`), không qua `AddressLockKeys` (module đã có `AddressLockKeys.CatalogImport`, `D-82`; D5 yêu cầu khoá sinh qua `<Module>…LockKeys.*`). Không có tiền tố `ecommerce:` nên **không** phải lỗi tiền tố đôi; chỉ lệch chỗ khai báo. | `Read` worker `:52`; `AddressLockKeys.cs:13-20` chỉ có `CatalogImport`; arch §7.1 dòng 282 ghi đúng chuỗi này | Gom thành `AddressLockKeys.CarrierSync(string carrier)` (giữ nguyên giá trị chuỗi để không đổi khoá Redis khi deploy cuốn chiếu); cập nhật `AddressCarrierContractGuardTests` đang quét literal. Không chặn merge. |
| 2 | P3 | `docs/tests/ADDR-002-testplan.md:50`, `:59`, `:137` | Testplan ghi unit **2165/2165** (mốc pass 3), số đo thật hôm nay **2166/2166** (`AQ6`/`D-251` +1 sau lần đo). | `dotnet test …Tests --no-build` → 2166; testplan ADDR-001 §2 ghi mốc 2166 | Cập nhật §2/§5 = 2166 (ghi rõ 2165 là mốc giữa phiên). |
| 3 | P3 | `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md:131` (BR-013 và BR-014) | SRS **vẫn** ghi code lệch/lỡ nợ: BR-013 "⚠️ Code hiện lệch … mã `null` ⇒ `NullReferenceException` ⇒ `Loi` (`:125`) — sửa ở WP-15"; BR-014 "hiện chưa có (`CarrierAddressSyncWorker.cs:132-159` gọi thẳng repository `autoSave: true`) … ⬜ WP-15". Code hiện **đã** sửa (`ChuanHoaVaKiemMa` gọi trước băm; worker mở 1 UoW `requiresNew`). Tài liệu là hợp đồng (K-09) ⇒ đang chỉ sai chỗ cần sửa. | `Read` SRS dòng 131; code `Synchronizer.cs:174`, worker `:174-238`; test `TC07_BR014_…` xanh | Sửa 2 câu BR-013/BR-014 sang ✅ WP-18 (giữ ghi chú lịch sử "trước đây lệch") **trong cùng PR**; rà thêm các dòng "⬜ WP-15" khác trong SRS. Không chặn merge code. |
| 4 | P3 | `modules/Address/eCommerce.Address.Application/Sync/CarrierAddressSyncWorker.cs:240-255` | Nhánh **thành công**: `uow.CompleteAsync()` xong vẫn còn trong `try` phần nối chuỗi + `Logger.LogWarning`/`LogInformation`. Nếu một trong hai ném (bất thường), `catch (Exception)` bên ngoài ghi thêm **một dòng `Loi`** cho **cùng lượt** ⇒ vi phạm BR-003 "mỗi lượt đúng 1 dòng". Xác suất thấp nhưng là cấu trúc dễ sửa. | `Read` `:238-282`; ngoại lệ chỉ xảy ra khi `string.Join`/logger ném | Đưa phần log/metric **sau** `try` (hoặc bọc riêng `try/catch` chỉ log), để nhánh `Loi` chỉ chạy khi lỗi **trước/trong** commit. Không chặn merge. |
| 5 | P3 | `docs/features/address/ADDR-002-arch.md:282` | Sơ đồ ghi `TryAcquireAsync("address:carrier-sync:{hãng}", 5 s) (:124)`; code thật: hằng `TienToKhoa` ở `:52`, lời gọi `TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(5))` ở `:144-146`. Số dòng lệch (do file đã đổi sau khi viết arch). | `Read` worker `:144-146` | Cập nhật số dòng/neo khi sửa arch lần tới (không cần sửa code). |

## Không kiểm được (nợ có chủ — không đánh dấu ✅)

- **403 runtime thiếu `Addresses.CarrierMapping.Verify`**: Host thiếu `Volo.Abp.Identity.HttpApi` ⇒ không tạo được user
  thường qua HTTP (`/api/identity/**` = 404). Probe ghi `CHUA DO DUOC`, testplan §3.1 **không** đánh dấu ✅; nợ chủ `qa`,
  hạn `W18-5` (`D-249`). Quyền hiện được phủ bằng reflection (`BR008_permission_XAC_MINH_ANH_XA_co_hang_so…`).
- **`0503 AnhXaTrungLap` qua HTTP**: không dựng được vì unique `(TenantId, CarrierCode, CarrierAddressCode)` chặn từ DB;
  nhánh `options.Map(0503→409)` cùng cơ chế với `0502/0504/0510` (đã chứng minh runtime). Ghi nhận ở testplan §6.2.
- **NFR-001 riêng cho ADDR-002** (>3.000 đơn vị ≤ 5 phút): chưa đo (testplan §6.4) — không cùng luồng với p95 tra xã.

## Truy vấn đồ thị

- `graph: mcp__codegraph__codegraph_explore "… CarrierAddressSyncWorker TienToKhoa GhiLoThatBaiAsync"` → xác nhận
  `DongBoMotHangAsync → TienToKhoa`, `GhiLoThatBaiAsync → Ghi`, `Ghi → CatChuoi`; 56 ký hiệu/4 tệp. Dùng cho P3 #1/#4
  (vị trí khai khoá và đường gọi sau commit), không suy đoán.

## Kết luận

**P1 = 0** · P2 = 0 · P3 = 5. Không có phát hiện chặn merge. Trước PR `tich-hop/gd3 → main` cần đóng nợ có chủ
`qa`/`W18-5` (đo 403 runtime) và nợ `be`/`D-250` (xoá overload `ApDung` — ghi ở `ADDR-001-review.md` #1).

VERDICT: PASS
