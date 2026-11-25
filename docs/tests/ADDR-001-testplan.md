# Testplan `ADDR-001` — 26/11/2026

- SRS: `docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` (§1 BR · §13 NFR · §14 TC/AC)
- Kiến trúc bổ trợ: `docs/features/address/ADDR-001-arch.md` (§6 ánh xạ test · §6.1 **6 negative control bắt buộc** · §7)
- Nhánh: `feat/gd3-wp18-dia-chi` · Phiên `N80` · Hàng `W18-4` · Bước **F4 pass 1** (F5 REV làm sau)
- Quyết định nguồn: `D-77`…`D-82`, `D-93`, `D-238`…`D-243` (`docs/nhanh/decisions.md`) — đặc biệt `D-238` (permission
  `Addresses.Catalog.Import`), `D-239` (`0009` căn cứ đổi tên), `D-240` (map HTTP 8 mã), `D-241` (chỉ
  `ImportDatasetAsync` trả `CatalogSyncRunDto`), `D-242` (TTL cache 86400 + cờ `Enabled`), `D-243` (tạm duyệt + P2).
- Phạm vi pass 1: **`ADDR-001`** + hạ tầng schema (regenerate `schema.sql`, `PostgreSqlSchemaTests` 93→94).
  `ADDR-002` do lượt qa sau làm (trừ hạ tầng schema đã chạm).

## 1. Phủ BR (14/14)

> Mỗi BR của SRS §1 có **ít nhất một** test; mã BR ghi trong tên test/comment. Tệp mới của `W18-4` đánh `🆕`.

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-ADDR-001-001** (danh mục là dữ liệu, cấm hard-code) | `test/eCommerce.Tests/AddressSync/ProvincesOpenApiParserTests.cs` · `Danh_muc_phan_tich_duoc_PHICH_HOP_voi_bo_dong_bo` · `test/eCommerce.Tests/AddressSync/AdministrativeUnitSynchronizerTests.cs` · `Danh_muc_giong_het_thi_KHONG_co_thay_doi` | Unit | ✅ |
| **BR-ADDR-001-002** (mọi truy vấn có `schemaVersion`; cấm mã trần) | 🆕 `test/eCommerce.Tests/AddressSync/AddressContractGuardTests.cs` · `TC05_moi_truy_van_tra_cuu_phai_co_schemaVersion` · `TC17_ngoai_le_SoSanhMaXa_phai_tra_dong_co_SchemaVersion` · `test/eCommerce.Tests/Address/AdministrativeUnitTests.cs` · `CUNG_ma_00004_nhung_KHAC_don_vi_giua_v1_va_v2` | Unit (reflection) | ✅ |
| **BR-ADDR-001-003** (unique `NULLS NOT DISTINCT`; bảng danh mục có `SchemaVersion`) | `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` · `F1_du_73_index_deu_dung_NULLS_NOT_DISTINCT` (**94**) · `ADDR001_chan_trung_don_vi_cung_phien_ban_cap_ma_khi_TenantId_NULL` · `ADDR001_CHO_PHEP_cung_ma_nhung_KHAC_phien_ban_schema` | Integration (PG) | ✅ |
| **BR-ADDR-001-004** (`v2`=2 cấp · `v1`=3 cấp, cấm trộn) | `test/eCommerce.Tests/Address/AdministrativeUnitTests.cs` · `Dia_chi_2_cap_hop_le` · `Dia_chi_3_cap_hop_le_o_schema_v1` · `KiemTraHaiCap_tren_schema_3_cap_bi_tu_choi_de_tranh_bo_sot_huyen` · `test/eCommerce.Tests/AddressSync/ProvincesOpenApiParserTests.cs` · `V1_phan_tich_duoc_ca_3_cap` | Unit | ✅ |
| **BR-ADDR-001-005** (địa chỉ chuẩn hoá đi qua resolver trước khi ghi) | `test/eCommerce.Tests/Address/AdministrativeUnitTests.cs` · `Xa_KHONG_thuoc_tinh_thi_FAIL_CLOSED` · `Khong_tra_duoc_xa_thi_FAIL_CLOSED` · `MA_DA_DONG_thi_khong_dung_cho_dia_chi_MOI` | Unit | ✅ |
| **BR-ADDR-001-006** (cấm cấp huyện cho địa chỉ MỚI) | `test/eCommerce.Tests/Address/AdministrativeUnitTests.cs` · `KHONG_tao_duoc_don_vi_cap_huyen_o_schema_2_cap` · `KiemTraBaCap_o_schema_2_cap_bi_TU_CHOI_theo_luat` | Unit | ✅ |
| **BR-ADDR-001-007** (danh tính bất biến; `DoiTen` + `LegalBasis`; hàng rào 5) | `test/eCommerce.Tests/AddressSync/AdministrativeUnitSynchronizerWp18Tests.cs` · `BR007_DoiTen_thieu_can_cu_phap_ly_thi_NEM_0009_va_KHONG_doi_ten` · `BR007_nguon_doi_ParentCode_cua_ma_DA_CO_thi_NEM_0008_va_khong_ghi_de_ParentCode` · 🆕 `AddressContractGuardTests.cs` · `TC12_khong_co_public_setter_tren_danh_tinh_va_ten` · `TC12_DoiTen_bat_buoc_tham_so_can_cu_khong_co_default` | Unit (domain + reflection) | ✅ |
| **BR-ADDR-001-008** (seed/nạp idempotent — không trùng, không đổi hash) | `test/eCommerce.Tests/AddressSync/AddressSourceHashTests.cs` · `BR009_chay_lai_cung_du_lieu_thi_CUNG_hash` · `BR009_thu_tu_dong_nguon_KHONG_doi_hash` · 🆕 `AdministrativeUnitAppServiceImportTests.cs` · `TC21c_import_HAI_lan_cung_du_lieu_thi_HAI_dong_lo_cung_SourceHash` | Unit | ✅ |
| **BR-ADDR-001-009** (mọi lượt nạp ghi lô + `SourceHash`; lượt từ chối vẫn ghi) | 🆕 `AdministrativeUnitAppServiceImportTests.cs` · `TC21b_import_hop_le_tra_CatalogSyncRunDto_cua_luot_minh` · `TC21c_…` · `TC21f_TC24e_doi_ParentCode_thi_NEM_0008_va_dong_tu_choi_CON_DOC_DUOC` · `TC21_loi_khong_mong_doi_thi_ghi_dong_lo_Loi_roi_nem_lai` · 🆕 `test/eCommerce.IntegrationTests/PostgreSqlAddressSyncTests.cs` · `TC21f_TC24e_dong_TU_CHOI_o_transaction_rieng_CON_DOC_DUOC_sau_rollback` | Unit (app svc) + Integration (PG) | ✅ |
| **BR-ADDR-001-010** (guard khởi động fail-closed) | `test/eCommerce.Tests/AddressSync/AddressSyncGuardTests.cs` · `BR010_nguon_cong_khai_o_moi_truong_NGOAI_allowlist_thi_NEM_fail_closed` · `BR010_baseUrl_rong_thi_COI_la_nguon_cong_khai_mac_dinh_va_NEM_o_Production` · `BR010_host_KHAC_nguon_cong_khai_thi_guard_KHONG_y_kien_ke_ca_o_Production` | Unit (hàm thuần) | ✅ |
| **BR-ADDR-001-011** (cấp suy từ hằng + kiểm cấp và tồn tại mã) | `test/eCommerce.Tests/Address/AdministrativeUnitTests.cs` · `Dia_chi_2_cap_hop_le` · `Khong_tra_duoc_xa_thi_FAIL_CLOSED` · `Huyen_khong_thuoc_tinh_thi_FAIL_CLOSED` · `Xa_khong_thuoc_huyen_thi_FAIL_CLOSED` | Unit | ✅ |
| **BR-ADDR-001-012** (mọi entity Address có `DbSet`) | 🆕 `AddressContractGuardTests.cs` · `TC13_du_SAU_DbSet_module_Address_tren_DbContext_hop_nhat` (6 `DbSet`: `AdministrativeUnits` · `CatalogSyncRuns` · `CarrierAddressMappings` · `CarrierAddressSyncRuns` · `CarrierAddressSyncRunUnmappedCodes` · `Addresses`) | Unit (reflection) | ✅ |
| **BR-ADDR-001-013** (`[AllowAnonymous]` trong allowlist; cấm import ẩn danh) | 🆕 `AddressContractGuardTests.cs` · `TC18_TC19_ImportDatasetAsync_co_permission_rieng_khong_AllowAnonymous` · `TC19_class_AdministrativeUnitAppService_co_Authorize_cap_class` · `TC18_permission_CatalogImport_dinh_nghia_o_Host` | Unit (reflection) | ✅ |
| **BR-ADDR-001-014** (cache khoá `(schemaVersion, parentCode)`; xoá khi nạp; TTL 86400) | 🆕 `test/eCommerce.Tests/AddressSync/AddressCatalogCacheTests.cs` · `BR014_khoa_cache_la_schemaVersion_va_parentCode__khong_tien_to` · `D242_TTL_rong_hoac_sai_thi_dung_86400_va_LogWarning` · `D242_cache_TAT_thi_Luon_doc_DB_va_KHONG_cham_cache` · `TC14_XoaAsync_xoa_ca_parent_VUA_BIEN_MAT_va_danh_sach_tinh` · 🆕 `AdministrativeUnitAppServiceImportTests.cs` · `TC14_import_thi_xoa_cache_ca_parent_VUA_BIEN_MAT` | Unit | ✅ |

- Tổng BR: **14** · đã phủ: **14** · chưa phủ: **0**.
- Tệp test mới của `W18-4` (`N80`):
  - `test/eCommerce.Tests/AddressSync/HaTang/HaTangDiaChi.cs` — fake in-memory: `RepoDiaChiGia<T>` (đếm chèn/sửa,
    hỗ trợ `InsertManyAsync`/`UpdateManyAsync`), `UowManagerGia` (ghi `requiresNew`/`isTransactional`),
    `KhoaPhanTanGia`, `CachePhanTanGia` (đếm đọc/ghi TTL/khoá xoá), `SettingDiaChiGia`, `CauHinhGia`,
    `EventBusGia`, `DongHoDiaChiGia`, `NguoiDungDiaChiGia`, `TenantDiaChiGia` — khuôn `D-229`.
  - `test/eCommerce.Tests/AddressSync/AddressCatalogCacheTests.cs` (BR-014/D-242/TC-14 + p95 nóng).
  - `test/eCommerce.Tests/AddressSync/AdministrativeUnitAppServiceImportTests.cs` (TC-21/TC-24/TC-25/TC-14).
  - `test/eCommerce.Tests/AddressSync/AddressContractGuardTests.cs` (TC-05/12/13/17/18/19/20/25).
  - `test/eCommerce.IntegrationTests/PostgreSqlAddressSyncTests.cs` (TC-11 + TC-21(f)/TC-24(e) + CHECK/UX trên PG thật).
  - `test/eCommerce.IntegrationTests/Nfr001AddressP95Tests.cs` (NFR-001 nguội).
- Thêm **1 dòng** `ProjectReference` `eCommerce.Address.Application` vào `test/eCommerce.Tests/eCommerce.Tests.csproj`
  (khuôn `D-229`); regenerate `test/eCommerce.IntegrationTests/Assets/schema.sql` từ **chính migration** (thêm 3 bảng
  `AddrCatalogSyncRuns`/`AddrCarrierSyncRuns`/`AddrCarrierSyncRunUnmappedCodes`, **0** dòng xoá); allowlist 5 ràng buộc
  mới vào `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt` (ràng buộc toàn vẹn sổ lô, không phải ca client cần mã).

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (`N78`) | Sau (`N80`) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **2089** | **2136** | +47 ca mới (không xoá/skip/nới test cũ); 0 fail |
| Unit + app service (`AddressSync`) | 90 | **137** | +47: `AddressContractGuardTests` 16 · `AddressCatalogCacheTests` 20 · `AdministrativeUnitAppServiceImportTests` 11 |
| Application (app service `ADDR-001`) | 0 | **11** | **MỚI**: `AdministrativeUnitAppServiceImportTests` (TC-21/24/25/14) — dựng `new` + fake (`D-229`) |
| API `/api/v1` (200 + 401/403) | — | **N/A** | `ADDR-001` là **ABP auto-API `/api/app/**`**, **không** có bề mặt `/api/v1` trong `gd3-1` (`D-245`); kiểm quyền bằng reflection `TC-18/TC-19` (policy `Addresses.Catalog.Import`, `[Authorize]` cấp class, **không** `[AllowAnonymous]`) |
| UI Vitest / E2E Playwright | — | **N/A** | WP-18 **không có FE** — `W18-3` kết luận `KHONG_CO_FE_CONTRACT` (`D-244`) |
| OpenAPI drift | — | **N/A** | `W18-2`/`W18-6` không đổi DTO `/api/v1`; `pnpm gen:api` không đổi `schema.d.ts` (`D-244`/`D-245`) |
| Integration (`test/eCommerce.IntegrationTests`) | 96 | **105** | +9 (8 `PostgreSqlAddressSyncTests` + 1 p95) trên PostgreSQL 17 thật |

> 📌 `D-229` ② yêu cầu `dotnet test` **tăng số ca** (không giảm) — đo được **unit 2089 → 2136 (+47)** và
> **integration 96 → 105 (+9)**; 0 fail.

## 3. NFR-001 — số đo p95 tra xã của tỉnh lớn nhất

Môi trường: Windows 10 Pro · PostgreSQL 17 (`postgres:17-alpine`, Testcontainers) · .NET 10 · 26/11/2026.

| Đường | Cách đo | Cấu hình | p95 đo được | Ngưỡng | Kết luận |
|---|---|---|---|---|---|
| **NGUỘI** (chưa cache) | `Nfr001AddressP95Tests.Nfr001_p95_tra_xa_tinh_lon_nhat_NGUOI_DB_duoi_100ms` — 200 xã, câu truy vấn đúng `LayConAsync` (`SchemaVersion + ParentCode + IsActive`, `ORDER BY Code`), prepared, 100 lượt | 200 xã × 100 lượt | **1,353 ms** (max 1,855 ms) | < 100 ms | ✅ |
| **NÓNG** (cache hit) | `AddressCatalogCacheTests.Nfr001_p95_tra_xa_NONG_cache_duoi_10ms` — 200 xã trong 1 item cache, `LayConAsync` hit, 500 lượt | fake in-memory × 500 lượt | **< 0,01 ms** (Stopwatch báo `0.000`, dưới độ phân giải) | < 10 ms | ✅ (cận dưới — không có RTT Redis) |

> ⚠️ **Đây là BẰNG CHỨNG ĐO, KHÔNG phải cổng cứng của CI.** Đường nóng dùng fake in-memory nên **bỏ qua RTT Redis**
> ⇒ là **cận dưới**; hệ thật thêm ~0,1–1 ms mạng nội bộ, vẫn dư ngưỡng 10 ms. Hai test có `Assert` ngưỡng để chạy
> lại được, nhưng máy CI tải khác nhau nên kết quả định lượng ghi ở đây là số đo tay ngày 26/11/2026.

## 4. Negative control

> Chạy `pwsh -File tools/agents/negative-control.ps1` cho từng ca: **đột biến thật → test phải ĐỎ (exit ≠ 0) →
> khôi phục byte gốc → test phải XANH (exit 0)**. `TestCommand` **có build lại** (không `--no-build`) vì script
> sửa mã nguồn. Bảng dưới do script tự chèn dòng bằng chứng. Sau mỗi ca: `git status --porcelain -- modules/` rỗng.
>
> 🔒 Nhóm luật quan trọng có NC: **trạng thái** (phân loại `BiChan`/`Loi`) · **quyền/khoá** (guard + khoá phân tán) ·
> **idempotency** (`SourceHash`) · **cấu hình TTL/cache** (`D-242`) · **danh tính** (`DoiTen` + hàng rào 5).

| Ngày | Mã | Tệp:dòng | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-26 | NC-1 | modules/Address/eCommerce.Address.Domain/Sync/AdministrativeUnitSynchronizer.cs:120 | Bo 0008 khoi HangRaoChan => 0008 bi phan loai Loi thay vi BiChan | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AdministrativeUnitAppServiceImportTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-2 | modules/Address/eCommerce.Address.Domain/AddressSyncGuard.cs:42 | Bo dieu kien allowlist moi truong => guard khong NEM o Production | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AddressSyncGuardTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-3 | modules/Address/eCommerce.Address.Domain/AdministrativeUnit.cs:143 | Bo guard can cu phap ly cua DoiTen => thieu 0009 | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AdministrativeUnitSynchronizerWp18Tests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-4 | modules/Address/eCommerce.Address.Application/Caching/AddressCatalogCache.cs:146 | Doi giay<=0 thanh giay<0 => Setting 0 dung TTL 0 thay vi 86400 | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AddressCatalogCacheTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-5 | modules/Address/eCommerce.Address.Application/Caching/AddressCatalogCache.cs:66 | Bo co Address:Cache:Enabled=false => van dung cache thay vi doc DB | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AddressCatalogCacheTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-6 | modules/Address/eCommerce.Address.Domain/Sync/AdministrativeUnitSynchronizer.cs:217 | Vo hieu hang rao 5 => nguon doi ParentCode khong NEM 0008 | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AdministrativeUnitSynchronizerWp18Tests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-26 | NC-7 | modules/Address/eCommerce.Address.Domain/AddressLockKeys.cs:19 | Them tien to ecommerce: vao khoa => tien to doi, cache/lock sai | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter "FullyQualifiedName~AddressContractGuardTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

**Kết luận NC:** **7/7 đỏ → xanh** (đo 26/11/2026). `git status --porcelain -- modules/ src/ shared/` **rỗng** sau cả 7 ca
(script tự khôi phục byte gốc + tự kiểm lại). Bản đồ tới §6.1 của `ADDR-001-arch.md`:
NC-1 ↔ arch NC-1 (UoW riêng khi từ chối — đo qua phân loại `BiChan`/`Loi` + `requiresNew` ở test app service);
NC-2 ↔ arch NC-2 (guard khởi động); NC-3 ↔ arch NC-6 (`DoiTen` căn cứ); NC-4/NC-5 ↔ phát sinh `D-242` (TTL + cờ `Enabled`);
NC-6 ↔ arch NC-5 (hàng rào 5 ưu tiên trước hàng rào 4); NC-7 ↔ `D-82` (khoá một nguồn, không tiền tố đôi).
arch NC-3 ("giữ early-return khi `!CoThayDoi`") và NC-4 ("bỏ xoá cache sau commit") được phủ **trực tiếp** bằng test
(TC-21(c) `TC21c_import_HAI_lan…` và TC-14 `TC14_import_thi_xoa_cache…`) thay vì đột biến riêng — ghi nhận ở §6.

## 5. Cổng (đo trong phiên `N80`, ORCH tự chạy lại)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/ADDR-001-testplan.md -Srs docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` | **0** | `BR trong SRS=14 · chua co trong testplan=0 · KET QUA: DAT` |
| `dotnet build eCommerce.slnx --no-incremental` | **0** | **0 error / 9 warning** (= baseline, 0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` | **0** | **2136/2136** pass (baseline 2089 → **+47**) |
| `dotnet test test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj --nologo` | **0** | **105/105** pass (baseline 96 → **+9**), Docker/Testcontainers `postgres:17-alpine` |
| `tools/agents/negative-control.ps1` ×7 | **0** | 7/7 **đỏ (exit 1) → xanh (exit 0)** |

## 6. Chưa phủ / nợ / lệch có chủ

1. **`ADDR-002`** (16 BR) **không** thuộc pass 1 — lượt qa sau. Pass 1 chỉ chạm **hạ tầng schema** của `ADDR-002`
   (`schema.sql` + `PostgreSqlSchemaTests` 93→94 + `UX_AddrCarrierSyncRunUnmappedCodes…` + 1 test hành vi của index).
2. **arch NC-3 / NC-4** phủ bằng test trực tiếp (xem §4), không tạo đột biến riêng: NC-3 (early-return) và NC-4
   (bỏ xoá cache) đã có ca đỏ tương ứng (`TC21c`, `TC14`). Bổ sung đột biến cho 2 ca này = việc pass 2 nếu REV yêu cầu.
3. **NFR-001** là **số đo tay** (mục §3), **không** phải cổng CI; đường nóng dùng fake in-memory (cận dưới).
4. **API `/api/v1` 200 + 401/403**: `N/A` vì `ADDR-001` là ABP auto-API `/api/app/**` (`D-245`); quyền kiểm bằng
   reflection (TC-18/TC-19). Runtime probe `D-233` (`ADDR-002`) thuộc lượt qa sau.
5. **Overload tương thích `ApDung(kế hoạch)`** — `D-250`: `qa` **đã đổi 4 call site test** sang dạng 2 tham số
   (`AdministrativeUnitSynchronizerTests.cs:118,258,275` + `ProvincesOpenApiParserTests.cs:217` → `ApDung(kh, "test")`),
   nên `grep "AdministrativeUnitSynchronizer.ApDung(" test/**` hiện **0** lời gọi 1 tham số. **Còn nợ chủ `be`:**
   xoá overload `modules/Address/eCommerce.Address.Domain/Sync/AdministrativeUnitSynchronizer.cs:314-315` ở phiên BE kế
   (hoặc cửa sổ BE của `W18-5`). **Hạn cứng: trước PR `tich-hop/gd3 → main`** — sau khi xoá, build **ĐỎ** nếu còn call site
   1 tham số (cơ chế đỏ là trình biên dịch). **Không** để nợ này trôi qua PR `main`.
6. **UI/E2E/OpenAPI drift**: `N/A` (WP-18 không có FE — `D-244`; không đổi DTO `/api/v1`).

