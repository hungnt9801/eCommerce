# Testplan `ADDR-002` — 26/11/2026

- SRS: `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` (§1 16 BR · §13 NFR · §14 TC/AC)
- Kiến trúc bổ trợ: `docs/features/address/ADDR-002-arch.md` (§7.1 bảng tầng/ca · §7.2 **5 negative control bắt buộc** · §8)
- Doc review: `docs/reviews/ADDR-002-arch-docreview.md` — **P2 #1** (runtime `D-233`: `0501`⇒404 + 5 mã còn lại ⇒ 409/400)
- Nhánh: `feat/gd3-wp18-dia-chi` · Phiên `N80` · Hàng `W18-4` · Bước **F4 pass 3** (F5 REV làm sau) · pass 3 theo `D-248`/`D-249`/`D-250`
- Quyết định nguồn: `D-83`…`D-92`, `D-232`…`D-237`, `D-243`, `D-248`, `D-249`, `D-250` (`docs/nhanh/decisions.md`)
- Phạm vi pass 3: **harness `CarrierAddressSyncWorker`** chạy thật (`D-248`: TC-07/TC-29/NC-4 + NC-2) · **đo 403 runtime** (`D-249`) · **4 call site `ApDung` 2 tham số** (`D-250`, phần `qa`). Pass 1 (`ADDR-001`) và pass 2 **không làm lại**.

## 0. Tệp mới của pass 2–3 (`N80`)

| Tệp | Nội dung |
|---|---|
| 🆕 `test/eCommerce.Tests/AddressSync/CarrierAddressAppServiceTests.cs` | Tầng **app service** `GetUnmappedCodesAsync` (`D-88`/`D-236`) — dựng `new` + `RepoDiaChiGia` in-memory (khuôn `D-229`), **không** host/PostgreSQL |
| 🆕 `test/eCommerce.Tests/AddressSync/AddressCarrierContractGuardTests.cs` | Reflection + quét mã nguồn: `D-232`…`D-237`, BR-001/003/010/011/014/016 |
| 🆕 `test/eCommerce.Tests/AddressSync/HaTang/HaTangDiaChi.cs` (mở rộng pass 3) | Thêm **mô hình UoW** (vùng chờ/`Complete`/rollback) + `WorkerDiaChiGia` + `HaTangWorkerDiaChi` + `KhungDiaChiHangGia` (`D-248`) |
| 🆕 `test/eCommerce.Tests/AddressSync/CarrierAddressSyncWorkerTests.cs` | Tầng **worker** THẬT: TC-07/BR-014 · TC-29/BR-011 · NC-4 · BR-003/`D-83`/`D-90`/`D-236` (7 ca) |
| 🆕 `tools/probe-dia-chi.ps1` | **Probe HTTP thật** :5000 — `unmapped-codes` 401/200 · `xac-minh` 401 · map `05xx`⇒status (`D-233`) · khối đo **403** (`D-249`, xem §3) |
| ✏️ `test/eCommerce.IntegrationTests/PostgreSqlAddressSyncTests.cs` | +1 ca `TC19`: mọi dòng ánh xạ/lô/dòng con của lượt worker giả lập có `TenantId IS NULL` (`D-92`/NFR-006) |

## 1. Phủ BR (16/16)

> Mỗi BR của SRS §1 có **ít nhất một** test; mã BR ghi trong tên test/comment. Tệp mới của `W18-4` pass 2 đánh `🆕`.

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-ADDR-002-001** (mã hãng là dữ liệu dẫn xuất; cấm FK nghiệp vụ) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR001_bang_lo_KHONG_co_navigation_toi_anh_xa_ma_hang_la_du_lieu_dan_xuat` · `CarrierAddressMappingSynchronizerTests.cs` · `Ma_MOI_thi_CHI_BAO_can_nguoi_anh_xa_va_KHONG_tao_ban_ghi_nao` | Unit (reflection + domain) | ✅ |
| **BR-ADDR-002-002** (ánh xạ phải có hãng+cấp+đơn vị; mã chưa ánh xạ liệt kê đủ) | 🆕 `CarrierAddressAppServiceTests.cs` · `BR002_GetUnmapped_3000_dong_con_tra_TotalCount_3000_va_phan_trang_mac_dinh_100` · `BR002_tao_1_anh_xa_cho_ma_da_co_thi_TotalCount_con_2999` · `BR002_chua_co_luot_DaApDung_tra_RONG_SyncRunId_null_khong_nem` · `BR002_phan_trang_khong_trung_khong_sot_tren_3000_dong` · `BR002_hang_rong_thi_NEM_0505` · `BR002_luot_va_dong_con_cua_HANG_KHAC_khong_bi_tinh` | Unit (app service) | ✅ |
| **BR-ADDR-002-003** (mỗi lượt đúng 1 dòng lô; ghi một lần) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR003_worker_ghi_dong_lo_qua_Ghi_o_ca_duong_thanh_cong_va_loi` · 🆕 `CarrierAddressSyncWorkerTests.cs` · `BR003_DaApDung_0_viec_thi_dung_1_dong_lo_3_so_bien_doi_0_va_0_dong_con` · `BR003_hang_rao_danh_muc_rong_thi_BiChan_1_dong_ErrorCode_0506` · `BR003_D236_ban_khoa_thi_0_dong_lo` · `BR003_D236_khong_co_adapter_thi_0_dong_lo` · `AddressSyncInvariantTests.cs` · `BR012_entity_so_APPEND_ONLY_khong_co_public_setter_va_khong_co_method_doi` (`CarrierAddressSyncRun`) | Unit (worker + quét + reflection) | ✅ |
| **BR-ADDR-002-004** (idempotency: chạy lại cùng danh mục 0 thay đổi; `SourceHash` ổn định) | `CarrierAddressMappingSourceRulesTests.cs` · `BR004_CarrierAddressSourceHash_on_dinh_thu_tu_va_Trim` · `BR004_CarrierAddressSourceHash_KHONG_doi_hoa_thuong_ma_hang` · `BR004_CarrierAddressSourceHash_chiu_null_khong_NRE` · `BR004_CarrierAddressSourceHash_truong_null_khong_NRE` · `CarrierAddressMappingSynchronizerTests.cs` · `Danh_muc_giong_het_thi_KHONG_co_viec_gi` | Unit | ✅ |
| **BR-ADDR-002-005** (thay đổi mã không im lặng; đếm đúng ở dòng lô) | `CarrierAddressMappingSourceRulesTests.cs` · `BR005_hang_rao_3_chi_dem_CHUYEN_MOI_sang_NghiNgo_qua_tung_luot` · `BR005_hang_rao_3_dung_MAU_SO_la_so_anh_xa_CHUA_NghiNgo` · `BR005_doi_ten_anh_xa_DA_NghiNgo_van_sinh_CapNhatTen_nhung_KHONG_vao_tu_so` · 🆕 `CarrierAddressAppServiceTests.cs` · `BR005_luot_moi_nhat_BiChan_van_tra_luot_DaApDung_va_LatestRunOutcome_1` · `CarrierAddressMappingSynchronizerTests.cs` · `Ten_doi_thi_CAP_NHAT_TEN_va_entity_TU_HA_muc_tin_cay` · `Ma_bien_mat_thi_DANH_DAU_NGHI_NGO_chu_KHONG_XOA` | Unit + app service | ✅ |
| **BR-ADDR-002-006** (đổi tên ghi đè tại chỗ, hạ `NghiNgo`, giữ đơn vị đích) | `test/eCommerce.Tests/AddressCarrier/CarrierAddressMappingTests.cs` · TC-09 (`CapNhatTuDongBo` đổi tên ⇒ `NghiNgo`, giữ `OurAdministrativeUnitId`/`MatchMethod`) · `CarrierAddressMappingSynchronizerTests.cs` · `Ten_doi_thi_CAP_NHAT_TEN_va_entity_TU_HA_muc_tin_cay` | Unit (domain) | ✅ |
| **BR-ADDR-002-007** (unique index có `TenantId` phải `NULLS NOT DISTINCT`) | `test/eCommerce.IntegrationTests/PostgreSqlAddressSyncTests.cs` · `UX_UnmappedCodes_chan_MA_LA_TRUNG_trong_CUNG_luot_khi_TenantId_NULL` · `UX_UnmappedCodes_cho_phep_CUNG_ma_o_HAI_luot_khac_nhau` · `PostgreSqlSchemaTests.cs` (đếm **94** index + tên `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code`) | Integration (PG) | ✅ |
| **BR-ADDR-002-008** (`[Authorize]` cấp class; permission định nghĩa trước khi dùng) | `AddressSyncInvariantTests.cs` · `BR008_permission_XAC_MINH_ANH_XA_co_hang_so_VA_duoc_dinh_nghia_trong_provider` · `AddressContractGuardTests.cs` · `TC18_TC19_ImportDatasetAsync_co_permission_rieng_khong_AllowAnonymous` · 🆕 `AddressCarrierContractGuardTests.cs` · `D252_XacMinhAsync_co_Authorize_dung_permission_Verify` (`D-252`: `[Authorize]` **cấp method** đúng policy `Addresses.CarrierMapping.Verify` + định nghĩa side `Host`) · 🆕 `tools/probe-dia-chi.ps1` T02/T04 (`unmapped-codes`/`xac-minh` **401** không token) | Unit (reflection) + Runtime HTTP | ✅ |
| **BR-ADDR-002-009** (chuyển `CarrierMappingStatus` chỉ qua method; đọc fail-closed) | `CarrierAddressResolverTraNguocTests.cs` · `BR009_HAI_anh_xa_DaXacMinh_cung_dich_thi_0510_khong_chon_bua` · `BR009_chi_co_ChuaXacMinh_va_NghiNgo_thi_0502` · `BR009_MOT_DaXacMinh_va_MOT_NghiNgo_thi_tra_DaXacMinh` · `BR009_dich_da_DONG_thi_0504_khong_tra_ma_hang` · `CarrierAddressMappingTests.cs` (TC-06 `Tra` ném `0502`) | Unit | ✅ |
| **BR-ADDR-002-010** (khoá phân tán theo hãng; timeout hữu hạn; cấm lồng) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR010_worker_dung_khoa_theo_hang_va_co_timeout_huu_han` (`address:carrier-sync` + `TimeSpan.FromSeconds(5)`) | Unit (quét mã nguồn) | ✅ |
| **BR-ADDR-002-011** (cấm hard-code endpoint/bộ mã/cấp hãng) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR011_khong_hard_code_endpoint_hang_trong_ma_nguon_Carrier` · 🆕 `CarrierAddressSyncWorkerTests.cs` · `TC29_BR011_adapter_nem_HttpRequestException_thi_Loi_ErrorType_va_KHONG_lo_SECRET` (dòng lô `Loi` chỉ lưu **tên kiểu**, không message/payload) | Unit (worker + quét mã nguồn) | ✅ |
| **BR-ADDR-002-012** (sổ lô + bảng con APPEND-ONLY) | `AddressSyncInvariantTests.cs` · `BR012_entity_so_APPEND_ONLY_khong_co_public_setter_va_khong_co_method_doi` (`CarrierAddressSyncRun` + `CarrierAddressSyncRunUnmappedCode`) | Unit (reflection) | ✅ |
| **BR-ADDR-002-013** (mã hãng chỉ `Trim`; `Ordinal`; trùng/rỗng ⇒ `0509`) | `CarrierAddressMappingSourceRulesTests.cs` · `BR013_ChuanHoaVaKiemMa_CHI_Trim_khong_doi_hoa_thuong_so_0_hay_khoang_trang_giua` · `BR013_ma_co_khoang_trang_hai_dau_thi_TRA_VE_da_Trim` · `BR013_ma_rong_hoac_null_thi_MOT_0509_khong_NRE_khong_0505` · `BR013_phan_tu_null_trong_danh_muc_thi_0509_khong_NRE` · `BR013_hai_ma_TRUNG_sau_Trim_thi_MOT_0509_soMaTrung_bang_SO_DON_VI_trung` · `BR013_danh_muc_TOAN_rong_ma_thi_0509_chu_KHONG_phai_0506` · `BR013_vua_rong_vua_trung_thi_MOT_exception_mang_CA_HAI_so_dem` | Unit | ✅ |
| **BR-ADDR-002-014** (mọi ghi qua UoW; nạp dữ liệu ngoài transaction) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR014_worker_mo_UoW_requiresNew_isTransactional_va_CompleteAsync` · 🆕 `CarrierAddressSyncWorkerTests.cs` · `TC07_BR014_DaApDung_ghi_dong_lo_nem_thi_0_anh_xa_doi` (ghi dòng lô ném ⇒ **rollback: 0 ánh xạ ghi** — ca để NC-2 đỏ) | Unit (worker + quét mã nguồn) | ✅ |
| **BR-ADDR-002-015** (mã lỗi `eCommerce:Address:05xx` do module Address sở hữu) | `AddressSyncInvariantTests.cs` · `BR015_danh_muc_ma_loi_moi_dung_dai_05xx_do_module_Address_so_huu` · `BR015_ma_05xx_KHONG_trung_voi_ma_0001_0009_cua_ADDR_001` | Unit (reflection) | ✅ |
| **BR-ADDR-002-016** (không trường tiền) | 🆕 `AddressCarrierContractGuardTests.cs` · `BR016_khong_co_truong_tien_decimal_hay_Money` (`CarrierAddressMapping`/`CarrierAddressSyncRun`/`…UnmappedCode`) | Unit (reflection) | ✅ |

- Tổng BR: **16** · đã phủ: **16** · chưa phủ hành vi đầy đủ: **0** (BR-003/BR-014 nay có ca **hành vi worker** thật — pass 3, `D-248`).

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (`N80` pass 1) | Sau (`N80` pass 3) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **2136** | **2165** | pass 2 +22, pass 3 +7 ca worker (không xoá/skip/nới test cũ); 0 fail |
| App service `ADDR-002` (`CarrierAddressAppServiceTests`) | 0 | **9** | `GetUnmappedCodesAsync`: happy · 2.999 · lượt `BiChan` mới nhất · chưa có lượt ⇒ rỗng·không ném · phân trang · hãng rỗng (`0505` ×3) · hãng khác |
| Worker `ADDR-002` (`CarrierAddressSyncWorkerTests`) | 0 | **7** | TC-07/BR-014 · TC-29/BR-011 · NC-4 · BR-003 (`DaApDung` 0 việc · `BiChan` 0506 · bận khoá · không adapter) |
| Reflection / quét mã nguồn (`AddressCarrierContractGuardTests`) | 0 | **14** | `D-232`…`D-237` + BR-001/003/010/011/014/016 + `D-252` (`W18-5`: ca khoá `[Authorize]` cấp method) |
| API `/api/v1` (200 + 401/403) | — | **N/A** | `ADDR-002` là **ABP auto-API `/api/app/**`**, **không** có bề mặt `/api/v1` trong `gd3-1` (`D-245`). Đo **runtime HTTP** qua `tools/probe-dia-chi.ps1` (§3) |
| UI Vitest / E2E Playwright | — | **N/A** | WP-18 **không có FE** (`D-244`/`D-247`) |
| OpenAPI drift | — | **N/A** | Không đổi DTO `/api/v1` (`D-245`) |
| Integration (`test/eCommerce.IntegrationTests`) | **105** | **106** | +1 `TC19` (TenantId NULL) trên PostgreSQL 17 thật; 8 ca ADDR-002 sẵn có của pass 1 |

> 📌 `D-229` ② yêu cầu `dotnet test` **tăng số ca** (không giảm) — đo được **unit 2136 → 2165 (+29)**
> (pass 2 +22, pass 3 +7), **integration 105 → 106 (+1)**; 0 fail.
>
> 📌 **Lượt đo `W18-5` (`N81`, `D-252`):** `dotnet test test/eCommerce.Tests --no-build` = **2170/2170** pass, 0 fail
> (**2166 → 2170, +4** unit: 1 ca reflection `D252_…` ở `AddressCarrierContractGuardTests` + 3 ca `ADDR-001` P2 #2).

## 3. RUNTIME (P2 #1 / `D-233` / `D-243`) — bằng chứng HTTP THẬT

**Cách đo:** `pwsh -File tools/probe-dia-chi.ps1` (mới, khuôn `tools/probe-so-dia-chi.ps1`): khởi động
`eCommerce.HttpApi.Host` (`:5000`) từ `bin/Debug/net10.0`, lấy token `password` grant (`admin`), seed SQL 4 ánh xạ +
2 đơn vị (1 đóng) ở `TenantId = NULL`, gọi HTTP thật. **Ngày 26/11/2026 · exit 0 · 10/10 DAT.**
Route in bằng `tools/route-check.ps1 -Match carrier-address` (không đoán): `tra-ma-hang` (POST),
`unmapped-codes` (GET), `{id}/xac-minh` (POST), `resolve-carrier-address-code` (POST).

| Ca | Gọi | HTTP STATUS | `error.code` | Kỳ vọng | Kết luận |
|---|---|---|---|---|---|
| T02 | `GET /api/app/carrier-address-mapping/unmapped-codes` (không token) | **401** | — | 401 | ✅ |
| T03 | `GET …/unmapped-codes?CarrierCode=GHN` (có token) | **200** | — | 200 | ✅ |
| T04 | `POST …/{id}/xac-minh` (không token) | **401** | — | 401 | ✅ |
| 🚨 T05 | `POST …/tra-ma-hang` mã hãng lạ | **404** | **`eCommerce:Address:0501`** | `D-233` ①: 404 + `0501` | ✅ **P2 #1 đóng** |
| T06 | `POST …/tra-ma-hang` hãng rỗng | **400** | **`eCommerce:Address:0505`** | 400 | ✅ |
| T07 | `POST …/tra-ma-hang` mã `ChuaXacMinh` | **409** | **`eCommerce:Address:0502`** | 409 | ✅ |
| T08 | `POST …/tra-ma-hang` đơn vị đích đã đóng | **409** | **`eCommerce:Address:0504`** | 409 | ✅ |
| T09 | `POST …/resolve-carrier-address-code` 2 `DaXacMinh` cùng đích | **409** | **`eCommerce:Address:0510`** | 409 | ✅ |
| T10 | Dọn dẹp: `delete … where CarrierAddressCode like 'PROBE-%'` | — | — | 0 dòng còn lại | ✅ |

> ⚠️ **`0503 AnhXaTrungLap` không dựng được qua HTTP** vì unique `(TenantId, CarrierCode, CarrierAddressCode)`
> (`NULLS NOT DISTINCT`) chặn 2 ánh xạ cùng mã ở tầng DB ⇒ nhánh `0503` chỉ chạy khi dữ liệu cũ đã trùng trước khi
> có index. Không phải lỗi sản phẩm — ghi nhận ở §6. Bản thân **map `0503→409`** cùng khối `D-233` (6 dòng) và
> được kiểm bằng nhánh `0502/0504/0510` (cùng cơ chế `options.Map`).

### 3.1 403 thiếu `Addresses.CarrierMapping.Verify` (`D-249`) — **CHƯA ĐO ĐƯỢC**

| Ca | Gọi | Kỳ vọng | Kết quả thật | Kết luận |
|---|---|---|---|---|
| T11 | `POST /api/identity/users` (token admin) tạo user thường + role **không** có Verify | 200/201 | **404** (Host không expose `/api/identity/**`) | ⛔ **chưa đo được** |
| T12 | `GET …/unmapped-codes` bằng token user thường | 200 | không chạy | ⛔ chưa đo |
| T13 | `POST …/{id}/xac-minh` bằng token user thường | **403** (không 401/200) | không chạy | ⛔ chưa đo |

> 🚨 **Lý do trung thực:** Host chỉ tham chiếu `Volo.Abp.Identity.AspNetCore` + `Volo.Abp.Identity.Application`
> (`src/eCommerce.HttpApi.Host/eCommerce.HttpApi.Host.csproj`, `src/eCommerce.Application/eCommerce.Application.csproj`) —
> **KHÔNG** có `Volo.Abp.Identity.HttpApi` ⇒ `swagger` có **0** route `/identity/**`
> (`pwsh -File tools/route-check.ps1 -Match identity` → *0 đường dẫn*) ⇒ **không tạo được user qua HTTP**.
> Probe ghi nhận `CHUA DO DUOC` (không đánh dấu ✅, không tính là ca hỏng). Quyền vẫn được phủ bằng reflection
> (§1 BR-008) nhưng **không thay** được hành vi 403 runtime. Khối 4 bước đo 403 đã có trong
> `tools/probe-dia-chi.ps1` và sẽ chạy ngay khi Host thêm Identity HTTP API.
> **Nợ chủ `qa`, hạn `release`** — `D-252` **thay hạn `W18-5` của `D-249` bằng `release`** (dòng hàng đợi
> `Xem lại trước = release` ⇒ cổng release **ĐỎ** tới khi người duyệt); gỡ khi `WEB-001` (BE/contract `D-245` + `D-247`
> (a)(c)) có `/api/v1` + màn hiện **403 thân thiện**, hoặc khi Host expose `/api/identity/**`. Xem §6 #3.
> 🔒 **Giữ nguyên** khối `⑦` probe + nhánh `ChuaDo`: `D-252` **cấm** đổi `ChuaDo` ⇒ `Kq $true` hay ghi "403 ✅".

## 4. Negative control

> Chạy `pwsh -File tools/agents/negative-control.ps1`: **đột biến thật → test phải ĐỎ (exit ≠ 0) → khôi phục byte gốc →
> test phải XANH (exit 0)**. `TestCommand` **có build lại** (không `--no-build`) vì script sửa mã nguồn. Bảng dưới
> do script tự chèn dòng bằng chứng. Sau mỗi ca: `git status --porcelain -- modules/` rỗng.
>
> 🔒 Nhóm luật quan trọng có NC: **trạng thái** (`0510` tra ngược) · **idempotency/hàng rào** (`D-87` đếm chuyển mới) ·
> **mã hãng** (`Trim`/`Ordinal`, BR-013) · **UoW/rollback** (`D-248`: bỏ `requiresNew` ⇒ TC-07 đỏ).

| Ngày | Mã | Tệp:dòng | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-26 | NC-1 | modules/Address/eCommerce.Address.Domain/Sync/CarrierAddressMappingSynchronizer.cs:249 | Hang rao 3 dem lap: dao guard Status != NghiNgo (D-87) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~CarrierAddressMappingSourceRulesTests" --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-3 | modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressMappingResolver.cs:167 | TraNguoc tra anh xa dau thay vi NEM 0510 | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~CarrierAddressResolverTraNguocTests" --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-5 | modules/Address/eCommerce.Address.Domain/Sync/CarrierAddressMappingSynchronizer.cs:132 | Bo Trim ma hang (BR-013/D-84) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~CarrierAddressMappingSourceRulesTests" --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-2 | modules/Address/eCommerce.Address.Application/Sync/CarrierAddressSyncWorker.cs:178 | NC-2: bo requiresNew khi mo UoW ghi dong lo DaApDung (D-248/arch 7.2) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~CarrierAddressSyncWorkerTests" --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-6 (`D-252`) | modules/Address/eCommerce.Address.Application/CarrierAddressMappingAppService.cs:127 | Bo policy Verify o [Authorize] cap METHOD cua XacMinhAsync (con [Authorize] trong) => moi user dang nhap xac minh duoc | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~D252_XacMinhAsync_co_Authorize_dung_permission_Verify" --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

**Kết luận NC:** **5/5 đỏ → xanh** (đo 26/11/2026; `W18-5`/`N81` thêm NC-6 cho `D-252`). Sau mỗi ca `git status --porcelain -- modules/` **rỗng**
(script tự khôi phục byte gốc + tự kiểm lại). Bản đồ tới §7.2 của `ADDR-002-arch.md`:
NC-1 ↔ arch **NC-1** (hàng rào 3 đếm chuyển mới `D-87`); NC-3 ↔ arch **NC-3** (`TraNguoc` > 1 `DaXacMinh` ⇒ `0510`);
NC-5 ↔ arch **NC-5** (`Trim`/`Ordinal` mã hãng BR-013); NC-2 ↔ arch **NC-2** (`D-248`: mở UoW **không** `requiresNew`
⇒ thay đổi của worker dùng chung vùng chờ UoW ngoài ⇒ `CompleteAsync` của UoW ngoài commit chúng dù lượt thất bại ⇒
`TC07_…_0_anh_xa_doi` **đỏ** đúng 1 ca, 6 ca còn lại vẫn xanh — xem log `artifacts/negative-control/`).
Arch **NC-4** (gộp `BiChan`/`Loi`) được phủ **hành vi** bằng ca `NC4_adapter_nem_BusinessException_ma_ngoai_hang_rao_thi_Loi_khong_BiChan`.
Arch **NC-5** (khuyến nghị) đã chạy ở pass 2. **NC-6 ↔ `D-252`** (bỏ policy `Addresses.CarrierMapping.Verify` ở `[Authorize]`
**cấp method** của `XacMinhAsync` ⇒ ca reflection `D252_…` **đỏ**; khôi phục ⇒ xanh).

## 5. Cổng (đo trong phiên `N80`, ORCH tự chạy lại)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/ADDR-002-testplan.md -Srs docs/features/address/addr-002-…md` | **0** | `BR trong SRS=16 · chua co trong testplan=0 · KET QUA: DAT` |
| `pwsh -File tools/route-check.ps1 -Match carrier-address` | **0** | in **6** route thật: `tra-ma-hang`/`lay-chua-xac-minh`/`{id}/xac-minh`/`phat-hien-sai-lech`/`resolve-carrier-address-code` (POST) + `unmapped-codes` (GET) |
| `pwsh -File tools/probe-dia-chi.ps1` | **0** | **10/10 DAT** — `404/0501` · `400/0505` · `409/0502` · `409/0504` · `409/0510` · `unmapped-codes` 401/200 · `xac-minh` 401 (§3). **1 CHƯA ĐO ĐƯỢC**: 403 thiếu Verify — Host không expose `/api/identity/**` (§3.1, D-249) |
| `dotnet build eCommerce.slnx --no-incremental` | **0** | **0 error / 9 warning** (= baseline, 0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` | **0** | **2170/2170** pass (`W18-5` đo 26/11/2026: 2166 → **+4**, trong đó 1 ca reflection `D252_…`; baseline pass 3 = 2136 → 2165) |
| `dotnet test test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj --nologo` | **0** | **106/106** pass (105 → **+1**), Docker/Testcontainers `postgres:17-alpine` |
| `tools/agents/negative-control.ps1` ×5 | **0** | 5/5 **đỏ (exit 1) → xanh (exit 0)** (NC-1/3/5 pass 2 + NC-2 pass 3 + **NC-6 `D-252` `W18-5`**); `modules/` sạch sau mỗi ca |

## 6. Chưa phủ / nợ / lệch có chủ

1. ✅ **Harness worker — ĐÃ ĐÓNG (`D-248`, pass 3).** `CarrierAddressSyncWorkerTests.cs` dựng `CarrierAddressSyncWorker`
   thật (lớp con phơi `DoWorkAsync`; `PeriodicBackgroundWorkerContext` ctor **public** đã kiểm metadata ABP 10.7.0) +
   UoW giả có vùng chờ/`Complete`/rollback. TC-07 (rollback ⇒ 0 ánh xạ), TC-29 (`Loi`/`ErrorType`/không lọt `SECRET`),
   NC-4 (`BusinessException` ngoài hàng rào ⇒ `Loi`), `DaApDung` 0 việc, `BiChan` 0506, bận khoá, không adapter —
   **7 ca xanh**; NC-2 **đỏ → xanh** (§4). `modules/**` **0** thay đổi.
2. **`0503 AnhXaTrungLap`** không dựng được qua HTTP vì unique `(TenantId, CarrierCode, CarrierAddressCode)`
   `NULLS NOT DISTINCT` chặn trùng ở DB ⇒ nhánh `0503` không reachable bằng dữ liệu sạch. Cùng khối `options.Map`
   `D-233`; nhánh `0502/0504/0510` đã chứng minh **cơ chế map** ở runtime (§3).
3. 🚨 **403 (thiếu `Addresses.CarrierMapping.Verify`) — CÒN MỞ tới `release` (`D-249` + `D-252`).** Khối 4 bước đã có
   trong `tools/probe-dia-chi.ps1` (tạo user thường qua `/api/identity/users`, password grant, `unmapped-codes` 200,
   `xac-minh` 403, dọn trong `finally`), nhưng **Host không expose Identity HTTP API** (thiếu `Volo.Abp.Identity.HttpApi`;
   `route-check -Match identity` = **0** route) ⇒ `POST /api/identity/users` = **404**. Probe ghi `CHUA DO DUOC`,
   **không** đánh dấu 403 ✅ (khối `ChuaDo` **giữ nguyên** — `D-252` cấm đổi thành "403 OK"). Quyền phủ bằng reflection:
   `D252_XacMinhAsync_co_Authorize_dung_permission_Verify` (`D-252` ①(1) — khoá `[Authorize]` **cấp method** đúng policy
   + định nghĩa side `Host`; NC-6 **đỏ → xanh**). **Nợ chủ `qa`, hạn `release`** (`D-252` thay hạn `W18-5` của `D-249`);
   gỡ khi **`WEB-001`** (`D-245` + `D-247` (a)(c)) có `/api/v1` + màn hiện 403 thân thiện, hoặc khi Host thêm Identity HTTP API
   (không sửa seed/quyền: `D-85`/P12).
4. **NFR-001 (p95 ≤ 5 phút cho lượt ~3.000 đơn vị)** chưa đo riêng cho `ADDR-002` (harness worker hiện **không** có
   PostgreSQL/EF thật để đo thời gian ghi); `Nfr001AddressP95Tests` của pass 1 đo **tra xã** của `ADDR-001`, không cùng
   luồng đồng bộ mã hãng. Ca harness pass 3 là **hành vi** (đúng/sai), **không** phải ca hiệu năng — không đánh dấu NFR-001.
5. **NFR-006** ca runtime dùng **SQL mô phỏng** ghi của worker (`tenantId: null`) vì harness in-process **không** có DB;
   chứng minh **schema** giữ đúng NULL cho cả 3 bảng, chưa chứng minh đường ghi trên PostgreSQL thật.
6. **NFR-007 (`CorrelationId`)** phụ thuộc `XC-005` F1 (middleware chưa wire) ⇒ **chưa kiểm chứng được**.
7. **UI/E2E/OpenAPI drift**: `N/A` (WP-18 không có FE — `D-244`; không đổi DTO `/api/v1` — `D-245`).
