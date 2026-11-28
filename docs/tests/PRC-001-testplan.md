# Testplan `PRC-001` — 28/11/2026

- SRS: `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` (§1 `BR-PRC-001-001…014` · §12 bảng lỗi/trường hợp biên · §15.2 nợ F3 · §20)
- Kiến trúc bổ trợ: `docs/features/pricing/PRC-001-arch.md` (§5.2 động cơ + UoW 3 pha · §5.4 nhập lùi · §5.5 thiếu giá vốn ⇒ `NULL` · §5.6 idempotency · §7 bảng **15 ca `K-06`** · §8 rủi ro/đường lùi)
- Quyết định: `D-320` (14 ca K-06 cứng) · `D-321` (ánh xạ DTO) · `D-323` (BackgroundWorker + command on-demand) · `D-324` (0012/0013 giữ chỗ, `using eCommerce.Inventory;` cho phép — bất biến **0 tên entity**; v1 VND; sổ run UoW `requiresNew`) · **`D-325`** (replay `[DauLichSu, to]`; `from` chỉ là mốc dưới kỳ được ghi — ca `K06-14` + NC-7) · **`D-326`** (nới guard `CostPrice` về `EffectiveTo < EffectiveFrom` — kỳ MỘT NGÀY `From == To` hợp lệ, khớp SRS §5.2 CK `>=`/`CoHieuLucTai`; ca `K06-15` + NC-8; sửa ghi chú `0014` ⇒ `0009`)
- Nhánh: `feat/gd3-wp17-gia-von` · Phiên `N118` · Hàng `W17-4` · Bước **F4 QA** (F5 REV làm sau)
- Trạng thái nền: `W17-2` (BE: `CostEngineService`/`TinhBinhQuanGiaQuyenDiDong`/`CostPriceLookupAdapter`/`CostFeedLookupAdapter` + migration `20261128052137_Added_Pricing_CostEngine`) và `W17-3` (no-FE) XONG.

## 1. Phủ BR

> Mỗi `BR-PRC-001-0xx` của SRS §1 có **ít nhất một** test thật. BR đã có test từ trước (WP-16) ghi rõ tệp/tên; BR mới của `W17-4` ghi tệp mới.

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-PRC-001-001** (mọi khoản tiền là `Money` + cột tiền tệ đi kèm) | `test/eCommerce.Tests/Pricing/CostPriceTests.cs` · `CurrencyCode_lay_TU_Money__khong_tu_gan_VND`; `test/eCommerce.Tests/Pricing/CostEngineK06Tests.cs` · `K06_01_gia_von_0_hop_le__sinh_ky_0_va_khong_phai_thieu`; `CostEngineServiceK06Tests.cs` · `K06_14_cua_so_bat_dau_giua_lich_su__replay_toan_bo` (bình quân gộp lịch sử, so qua `Money`); `CostEngineServiceK06Tests.cs` · `K06_15_hai_ngay_nhap_lien_ke__ky_mot_ngay_ghi_duoc` (so kỳ vọng qua `Money.CompareTo`, không `==` decimal) | Domain + Application | ✅ |
| **BR-PRC-001-002** (cấm so sánh tiền bằng `==` trên `decimal`) | `test/eCommerce.Tests/Pricing/CostEngineKienTrucTests.cs` · `K06_12_khong_so_sanh_tien_bang_dau_bang`; `PricingBatBienTienTests.cs` · `TC11_KHONG_so_tien_bang_toan_tu_bang_trong_Pricing_BR003`; `CostEngineServiceK06Tests.cs` · `K06_14...` (so kỳ vọng bằng `Money.CompareTo`, không `==` decimal) · `K06_15...` | Kiến trúc (quét mã) + Application | ✅ |
| **BR-PRC-001-003** (giá có hiệu lực theo ngày; cấm truy vấn "mới nhất" không ngày) | `CostPriceTests.cs` · `CoHieuLucTai__dung_ca_HAI_bien_va_ben_ngoai` · `EffectiveTo_BANG_EffectiveFrom__HOP_LE` (kỳ MỘT NGÀY hợp lệ, `D-326`); `CostPriceLookupK06Tests.cs` · `K06_02_dong_ngoai_hieu_luc__tra_null`; `CostEngineServiceK06Tests.cs` · `K06_15_hai_ngay_nhap_lien_ke__ky_mot_ngay_ghi_duoc` (đọc lại `Ngay1`/`Ngay2` qua `ICostPriceLookup`) | Domain + Application | ✅ |
| **BR-PRC-001-004** (bảng giá `Active` append-only) | `GiaCoSoResolverTests.cs` · `YeuCauChoPhepSuaNoiDung__bang_gia_ACTIVE_thi_NEM_0003__BR004`; `ChonBacThangTests.cs` · `Append_only__bo_dang_Active_thi_KHONG_sua_duoc_noi_dung` | Domain | ✅ |
| **BR-PRC-001-005** (chuyển trạng thái sai guard **NÉM**; cấm no-op) | `GiaCoSoResolverTests.cs` · `KichHoat_khi_dang_Draft_thi_NEM_LOI`; `ChonBacThangTests.cs` · `PhatHanh_khi_CHUA_co_bac_nao_thi_NEM_LOI` | Domain | ✅ |
| **BR-PRC-001-006** (thứ tự ưu tiên chọn giá cố định) | `ChonDonGiaCuoiTests.cs` · `CO_bac_thang_thi_bac_thang_THANG__va_cap_doi_thanh_BacThang`; `KHONG_co_bac_thang_thi_dung_gia_co_so__giu_nguyen_nguon_va_cap` | Domain | ✅ |
| **BR-PRC-001-007** (một `PriceList` `Active` cho mỗi `(Tenant, Scope, Channel)`) | `QuyetDinhKichHoatBangGiaTests.cs` · `Cung_scope_cung_kenh_thi_PHAI_thay_the` · `Nhieu_bang_gia_cung_pham_vi_thi_thay_the_HET__va_khu_trung` · `KHONG_tu_thay_the_chinh_minh__idempotent` | Domain | ✅ |
| **BR-PRC-001-008** (duy nhất theo grain) — **nợ cũ `check-feature-doc`**: `CostPrice` *một SKU một kỳ* | `test/eCommerce.IntegrationTests/PostgreSqlPricingCostEngineTests.cs` · `BR_008_mot_SKU_mot_ky__ban_ghi_thu_hai_bi_chan_khi_TenantId_NULL` · `BR_008_cung_SKU_khac_ky__cho_phep` | Integration PostgreSQL | ✅ |
| **BR-PRC-001-009** (pipeline chạy hết rồi mới chọn; kèm ứng viên/breakdown) | `PriceCalculationAppServiceTests.cs` · `TinhGio__DungGiaNiemYet__tra_gia_niem_yet_va_ghi_ro_breakdown_1c`; `TinhTienDonTests.cs` · `TONG_thanh_toan_BANG_dung_tong_cac_dong_da_lam_tron` | Application + Domain | ✅ |
| **BR-PRC-001-010** (không tự quy đổi tiền tệ ⇒ NÉM `0005`; v1 giá vốn ngoại tệ ⇒ NÉM `0014`) | `ChonDonGiaCuoiTests.cs` · `Bac_thang_LECH_loai_tien_thi_NEM__cam_tu_quy_doi`; `CostEngineServiceK06Tests.cs` · `TienTeNhap_khac_VND__nem_0014_khong_quy_doi` | Domain + Application | ✅ |
| **BR-PRC-001-011** (lọc `TenantId`; unique `NULLS NOT DISTINCT`) | `PostgreSqlSchemaTests.cs` · `F1_du_73_index_deu_dung_NULLS_NOT_DISTINCT` (**116** index); `PostgreSqlPricingCostEngineTests.cs` · `K06_08b_RunKey_trung_bi_chan_khi_TenantId_NULL` · `BR_012_hai_dong_cung_IdempotencyKey_bi_chan` | Integration PostgreSQL | ✅ |
| **BR-PRC-001-012** (idempotency ở mọi biên vào; khoá ổn định) | `CostPriceAdminAppServiceTests.cs` · `BR_012_cung_khoa__tra_dong_cu_khong_tao_thu_hai` · `BR_012_thieu_khoa__nem_0006_khong_ghi` · `BR_012_khoa_duoc_trim__cung_dong`; `PostgreSqlPricingCostEngineTests.cs` · `BR_012_IdempotencyKey_null__nhieu_dong_cho_phep` | Application + Integration | ✅ |
| **BR-PRC-001-013** (giá vốn **cấm** lộ ra API công khai/storefront) | `CostPriceLookupK06Tests.cs` · `BR_013_port_doc_gia_von_KHONG_phai_app_service`; `PricingBatBienTienTests.cs` · `TC21_gia_von_KHONG_xuat_hien_trong_DTO_cong_khai_BR021` | Kiến trúc/reflection | ✅ |
| **BR-PRC-001-014** (mọi đổi giá có dấu vết kiểm toán) | `CostEngineK06Tests.cs` · `BR_014_danh_dau_dong_co__ghi_nguon_run_va_thoi_diem` | Domain | ✅ |

- Tổng BR: **14** · đã phủ: **14** · chưa phủ: **0**.

## 2. Bảng 16 ca `K-06` (điều kiện cứng `D-320` vi; ARCH §7; `K06-14` thêm bởi `D-325`; `K06-15` thêm bởi `D-326`)

| Mã | Kịch bản | Tầng test | Tệp · tên test thật | Kết quả |
|---|---|---|---|---|
| **K06-01** | Giá vốn `0` **hợp lệ** (không coi là thiếu) | Domain + Application + Integration | `CostEngineK06Tests` · `K06_01_gia_von_0_hop_le__sinh_ky_0_va_khong_phai_thieu` · `K06_01_CostPrice_0_hop_le_va_duyet_duoc`; `CostPriceLookupK06Tests` · `K06_01_gia_von_0__co_gia_von_khong_phai_thieu`; `PostgreSqlPricingCostEngineTests` · `K06_01_gia_von_0_hop_le_o_DB` · `K06_01_gia_von_am_bi_CHECK_chan` | ✅ Pass |
| **K06-02** | Thiếu dòng đã duyệt hiệu lực ⇒ `null` + `CostMissing`; **cấm `0`** | Application + Integration | `CostPriceLookupK06Tests` · `K06_02_khong_co_dong_da_duyet__tra_null` · `K06_02_dong_ngoai_hieu_luc__tra_null` · `K06_02_lo_thieu_dong__van_tra_dto_voi_AmountValue_null` | ✅ Pass |
| **K06-03** | Dòng **chưa duyệt** không được dùng | Application | `CostPriceLookupK06Tests` · `K06_03_dong_chua_duyet__tra_null` | ✅ Pass |
| **K06-04** | Nhập lùi, `ChoNhapLuiNgay=false` ⇒ kỳ đã duyệt không đổi + `RowsSkippedApproved++` | Application | `CostEngineServiceK06Tests` · `K06_04_nhap_lui__ChoNhapLuiNgay_false__bo_qua_va_dem` | ✅ Pass |
| **K06-05** | Nhập lùi, `ChoNhapLuiNgay=true` làm đổi kỳ đã duyệt ⇒ **NÉM `0015`**, không ghi đè | Application | `CostEngineServiceK06Tests` · `K06_05_nhap_lui__ChoNhapLuiNgay_true__nem_0015_khong_ghi_de` | ✅ Pass |
| **K06-06** | Cùng SKU nhập **hai kho** ⇒ gộp một bình quân (grain SKU, không `WarehouseId`) | Domain | `CostEngineK06Tests` · `K06_06_cung_SKU_hai_kho__gop_mot_binh_quan` · `K06_06_kieu_chuyen_dong_KHONG_co_chieu_kho` | ✅ Pass |
| **K06-07** | Bán **trước** lần nhập đầu ⇒ ngày bán không có kỳ, lookup `null` | Domain | `CostEngineK06Tests` · `K06_07_ban_truoc_lan_nhap_dau__ngay_ban_khong_co_ky` | ✅ Pass |
| **K06-08** | Chạy hai lần cùng khoảng & `trigger`, lần đầu `ThanhCong` ⇒ lần 2 **trả run cũ**, không tính/ghi lại | Application + Integration | `CostEngineServiceK06Tests` · `K06_08_chay_hai_lan_cung_khoa__lan_hai_tra_run_cu`; `PostgreSqlPricingCostEngineTests` · `K06_08b_RunKey_trung_bi_chan_khi_TenantId_NULL` | ✅ Pass |
| **K06-08b** | Run trước `DangChay` ⇒ **bận/409**; `Loi` ⇒ **retry attempt mới** (`Attempt++`); sổ run **bền vững** qua rollback UoW ngoài | Application + Integration PostgreSQL | `CostEngineServiceK06Tests` · `K06_08b_run_DangChay__nem_ban_khong_chay_chong` · `K06_08b_run_Loi__retry_attempt_moi_cung_dong`; `PostgreSqlPricingCostEngineTests` · `K06_08b_run_ben_vung_qua_rollback_UoW_ngoai` · `K06_08b_loi_retry_attempt_moi_tren_cung_dong` · `K06_08b_RunKey_trung_bi_chan_khi_TenantId_NULL` | ✅ Pass |
| **K06-09** | Nhập kho thiếu `UnitPrice` ⇒ từ mốc đó **không INSERT kỳ**, không bịa `0` | Domain | `CostEngineK06Tests` · `K06_09_nhap_khong_gia__khong_sinh_ky_va_danh_dau_thieu` | ✅ Pass |
| **K06-10** | `TransferOut`+`TransferIn` cùng SKU ⇒ net `0`, không đổi bình quân | Domain/Adapter | `CostEngineK06Tests` · `K06_10_chuyen_kho__net_0__adapter_bo_ca_cap` | ✅ Pass |
| **K06-11** | `PhuongPhapGiaVon != BinhQuanGiaQuyenDiDong` / `GiaVonTheoKho=true` ⇒ **NÉM `0014`** (không rơi mặc định) | Application | `CostEngineServiceK06Tests` · `K06_11_phuong_phap_FIFO__nem_0014` · `K06_11_gia_von_theo_kho_true__nem_0014` · `K06_11_dong_co_tat__tra_null_khong_ghi` | ✅ Pass |
| **K06-12** | Toàn `Money`: **0** `double`/`float`, **0** `==` trên tiền, **0** `Math.Round` | Kiến trúc (quét mã) | `CostEngineKienTrucTests` · `K06_12_khong_dung_double_float` · `K06_12_khong_tu_lam_tron_trong_domain_application` · `K06_12_khong_so_sanh_tien_bang_dau_bang`; đối chứng cũ `PricingBatBienTienTests.TC10/TC11/TC12` | ✅ Pass |
| **K06-13** | Dòng đã duyệt **bất biến** khi tính lại; replay tất định | Application + Domain | `CostEngineServiceK06Tests` · `K06_13_ky_da_duyet_bat_bien__chay_lai_khong_doi_so`; `CostEngineK06Tests` · `K06_13_replay_tat_dinh__cung_dau_vao_cung_ket_qua` | ✅ Pass |
| **K06-14** | Cửa sổ chạy **bắt đầu GIỮA lịch sử**: SKU nhập `Ngay0` 100@10 (ngoài `from`) + `Ngay1` 10@20 ⇒ kỳ `Ngay1` = `(100·10 + 10·20)/110 ≈ 10,909…` (VND làm tròn **11**), **KHÔNG** phải `20`; lượt `from = Ngay1` **KHÔNG** ghi/đổi kỳ `EffectiveFrom = Ngay0` (`D-325`) | Application | `CostEngineServiceK06Tests` · `K06_14_cua_so_bat_dau_giua_lich_su__replay_toan_bo` · `K06_14b_cua_so_bat_dau_giua_lich_su__khong_ghi_khong_doi_ky_truoc_from` | ✅ Pass |
| **K06-15** | **`D-326`** — Hai ngày nhập **LIỀN KỀ** (`Ngay1` 10@10 + `Ngay2` 10@20) sinh kỳ `Ngay1` có `EffectiveFrom == EffectiveTo` (kỳ **MỘT NGÀY**). `ChayAsync` **GHI ĐƯỢC, KHÔNG NÉM**, run `ThanhCong` (`RowsUpserted = 2`); sau khi duyệt, `ICostPriceLookup` trả `Ngay1 = 10`, `Ngay2 = 15` (`(10·10+10·20)/20`) | Domain + Application | `CostPriceTests` · `EffectiveTo_BANG_EffectiveFrom__HOP_LE` (ctor không ném); `CostEngineServiceK06Tests` · `K06_15_hai_ngay_nhap_lien_ke__ky_mot_ngay_ghi_duoc` | ✅ Pass |

## 3. Test bổ sung bắt buộc (ngoài bảng `K-06`)

| # | Yêu cầu | Test thật | Kết quả |
|---|---|---|---|
| 1 | **Kiến trúc**: `modules/Pricing/**` **0 TÊN ENTITY** `StockLedgerEntry`/`StockDocument`/`StockDocumentLine`/`InventoryItem` (`using eCommerce.Inventory;` vẫn được phép — `D-324`) | `CostEngineKienTrucTests.cs` · `Pricing_KHONG_nhac_ten_entity_cua_Inventory` | ✅ Pass |
| 2 | **`0014` tiền tệ**: `TienTeNhap` khác `VND` ⇒ **NÉM `0014`**, không quy đổi (`D-321`/`D-324` ③) | `CostEngineServiceK06Tests.cs` · `TienTeNhap_khac_VND__nem_0014_khong_quy_doi` + 3 ca fail-closed dấu/đơn giá trong `CostEngineK06Tests` | ✅ Pass |
| 3 | **`BR-PRC-001-008`** (nợ cũ `check-feature-doc`): unique/grain của `CostPrice` trên DB thật | `PostgreSqlPricingCostEngineTests.cs` · `BR_008_mot_SKU_mot_ky...` · `BR_008_cung_SKU_khac_ky__cho_phep` | ✅ Pass |
| 4 | **`K06-08b` trên PostgreSQL thật**: `DangChay` bận; `Loi` retry `Attempt++`; sổ run bền vững qua rollback UoW ngoài | `PostgreSqlPricingCostEngineTests.cs` · `K06_08b_run_ben_vung_qua_rollback_UoW_ngoai` · `K06_08b_loi_retry_attempt_moi_tren_cung_dong` · `K06_08b_RunKey_trung_bi_chan_khi_TenantId_NULL` | ✅ Pass |
| 5 | **Quyền (K-06 tầng app service ≥ 3)**: `[Authorize]` cấp class + permission được định nghĩa cho `CostEngineAppService`/`CostPriceAdminAppService` | `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs` (quét **mọi** app service + permission reference — cơ chế sẵn có, không thêm ca) | ✅ Pass |
| 6 | **P2 #4 review `PRC-001`**: đẳng thức **số** giữa `LoaiDongGiaVon` (Domain Pricing) và `LoaiChuyenDongGiaVon` (Inventory Contracts) — cast `(LoaiDongGiaVon)(int)dto.Loai` ở `CostEngineService.AnhXa` chỉ đúng khi trùng số | `test/eCommerce.Tests/Pricing/LoaiDongGiaVonKhopInventoryTests.cs` · `LoaiDongGiaVon_khop_1_1_so_voi_LoaiChuyenDongGiaVon_cua_Inventory` | ✅ Pass |

## 4. Negative control (bắt buộc — đỏ → xanh)

> Bảng dưới là **kế hoạch**; ORCH chạy `tools/agents/negative-control.ps1` **sau commit** (script từ chối tệp chưa commit), áp đột biến → test phải **ĐỎ** → khôi phục byte gốc → test phải **XANH**, rồi chèn bằng chứng thật. QA **không sửa mã sản phẩm**.

| # | Đột biến (thật, trong mã) | Tệp:dòng dự kiến | Ca phải đỏ | Bằng chứng kỳ vọng |
|---|---|---|---|---|
| NC-1 | Đảo guard "dòng chưa duyệt": `if (!dong.DaDuyet)` ⇒ `if (dong.DaDuyet)` (ghi đè dòng ĐÃ duyệt) | `modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:299` | `K06_04...` / `K06_13...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-2 | Bỏ đánh dấu thiếu giá: `costMissing = true;` ⇒ `costMissing = false;` (bịa giá vốn) | `modules/Pricing/eCommerce.Pricing.Domain/TinhBinhQuanGiaQuyenDiDong.cs:150` | `K06_09...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-3 | Đảo nhánh trạng thái run: `TrangThai == ThanhCong` ⇒ `!= ThanhCong` (chạy chồng khi đã xong) | `CostEngineService.cs:146` | `K06_08...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-4 | Bỏ lọc `DaDuyet` ở cổng đọc: xoá `&& x.ApprovedBy != null && x.ApprovedAtUtc != null` | `modules/Pricing/eCommerce.Pricing.Application/CostPriceLookupAdapter.cs:38` | `K06_03...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-5 | Đảo fail-closed setting: `if (theoKho)` ⇒ `if (!theoKho)` | `CostEngineService.cs:116` | `K06_11_gia_von_theo_kho_true...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-6 | Đổi công thức bình quân: `binhQuan = soLuong > 0m ? giaTri / soLuong : null;` ⇒ `binhQuan = giaTri;` (sai đơn giá) | `TinhBinhQuanGiaQuyenDiDong.cs:143` | `K06_06...` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| **NC-7** | **`D-325`** — cắt lịch sử giữa cửa sổ: `_feed.LayChuyenDongAsync(DauLichSu, to)` ⇒ `_feed.LayChuyenDongAsync(from, to)` (tái hiện đúng P1 review #1) | `modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:253` | `K06_14_cua_so_bat_dau_giua_lich_su__replay_toan_bo` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| **NC-8** | **`D-326`** — trả guard kỳ về `<=`: `effectiveTo < effectiveFrom` ⇒ `effectiveTo <= effectiveFrom` (kỳ MỘT NGÀY bị coi là sai ⇒ engine NÉM `0009`, run `Loi`) | `modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:118` (hoặc `:197` ở `CapNhatGiaVonChuaDuyet`) | `K06_15_hai_ngay_nhap_lien_ke__ky_mot_ngay_ghi_duoc` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |
| NC-9 | Bỏ `[Authorize(...)]` cấp class của `CostEngineAppService` (lộ endpoint command). *(Trước đây đánh số NC-8; đổi thành NC-9 khi `D-326` thêm NC-8 — chưa chạy.)* | `modules/Pricing/eCommerce.Pricing.Application/CostEngineAppService.cs:14` | `BatBienPhanQuyenTests` | exit 1 (đỏ) → khôi phục → exit 0 (xanh) |

- Nhóm luật quan trọng được phủ: **tiền/kỳ một ngày** (NC-2/NC-6/**NC-7**/**NC-8**) · **trạng thái run** (NC-3) · **duyệt/bất biến số đã duyệt** (NC-1/NC-4) · **cấu hình fail-closed** (NC-5) · **quyền** (NC-9) · **idempotency** (NC-3 + các ca `RunKey`/`IdempotencyKey` ở integration).
- **NC-8** (`D-326`) bắt buộc cho kỳ **MỘT NGÀY**: đột biến `effectiveTo < effectiveFrom` ⇒ `effectiveTo <= effectiveFrom` tại ctor `CostPrice` làm `DoiSoatAsync` NÉM `0009` khi ghi kỳ `Ngay1` (`EffectiveFrom == EffectiveTo`) ⇒ `K06_15` **đỏ** (run `Loi`). Khôi phục `git` ⇒ `K06_15` **xanh**.
- **NC-7** là ca bắt buộc cho P1 review #1 (`D-325`): đột biến cắt lịch sử tại biên nạp làm `K06_14` **đỏ** vì fake `FeedGia` lọc theo `from/to` ⇒ chỉ còn `10 @ 20` ⇒ ghi `20` thay vì ≈ `10,909…` (làm tròn `11`).
- Xác nhận sau mỗi ca (ORCH): `git status --porcelain -- modules/ src/ shared/` rỗng ⇒ đã khôi phục byte gốc.

## 5. Số ca theo tầng + cổng (đo thật phiên `N118`)

| Tầng | Trước (`W17-3` / baseline ORCH) | Sau (`W17-4`) | Ghi chú |
|---|---|---|---|
| `dotnet test test/eCommerce.Tests` | **2650** | **2691** | **+41** ca mới so với `W17-3`: 37 ca nền `W17-4` + **3 ca bổ sung theo `D-325`** (`K06-14`, `K06-14b`, đẳng thức enum) + **1 ca `D-326`** (`K06-15`). Đổi tên ca `EffectiveTo_BANG_EffectiveFrom__NEM_0009` ⇒ `..._HOP_LE` **không đổi số ca** (0 fail) |
| Trong đó — `Pricing` (`CostEngineK06Tests` 14 · `CostPriceLookupK06Tests` 6 · `CostEngineServiceK06Tests` **13** · `CostPriceAdminAppServiceTests` 3 · `CostEngineKienTrucTests` 4 · `LoaiDongGiaVonKhopInventoryTests` **1**) | — | **41** | toàn bộ tầng Domain/Application/Kiến trúc của động cơ |
| `dotnet test test/eCommerce.IntegrationTests` | **162** | **171** | **+9** (`PostgreSqlPricingCostEngineTests`), 0 fail |
| API `/api/v1` 200/401/403 | — | **N/A** | `D-323`: `CostEngineAppService` là command **nội bộ** qua app service; không thêm route HTTP mới ở `W17-4` (quyền phủ bằng `BatBienPhanQuyenTests`) |
| UI Vitest / Playwright | — | **0** | `W17-3` no-FE (0 tệp FE) |
| OpenAPI drift | — | **0** | Không đổi DTO/controller ở `W17-4` |

Cổng đã chạy:

```powershell
dotnet build eCommerce.slnx --no-incremental     # exit 0 · 0 Error · 9 Warning (KHÔNG warning mới)
dotnet test test/eCommerce.Tests --no-build      # exit 0 · 2691/2691 pass
dotnet test test/eCommerce.IntegrationTests --no-build  # exit 0 · 171/171 pass
```

## 6. Ghi chú / phát hiện khi dựng nền test

1. **`Assets/schema.sql` trên nhánh vốn đã CŨ**: thiếu migration `20261127153549_Add_Voucher_Status_Check` (có từ `N101`/`W25`) và `20261128052137_Added_Pricing_CostEngine` (`W17-2`). Sinh lại bằng **đúng cơ chế repo** (`dotnet ef migrations script -p src/eCommerce.EntityFrameworkCore -s src/eCommerce.DbMigrator -o test/eCommerce.IntegrationTests/Assets/schema.sql`): **+67 dòng thuần thêm**, gồm `PrcCostEngineRuns` + 4 cột nullable của `PrcCostPrices` + 2 index `NULLS NOT DISTINCT` lần đầu, và 2 `CHECK` của W25. ⇒ `PostgreSqlSchemaTests` đếm NND **114 → 116** + thêm **2 tên index đích danh**; `Data/RangBuocChuaAnhXa.txt` thêm **4 dòng có lý do** (2 index giá vốn + 2 `CHECK` W25 có sẵn).
2. **`K06-08b` tách hai tầng**: nhánh trạng thái (`DangChay` ⇒ bận; `Loi` ⇒ `Attempt++`) đo ở tầng Application bằng `new` + hạ tầng giả (`D-229`); **bền vững sổ run qua rollback UoW ngoài** đo trên **PostgreSQL thật** (`PostgreSqlPricingCostEngineTests`) — vì chỉ DB mới chứng minh được transaction riêng commit trước khi tính.
3. **`K06-10`**: adapter Inventory là nơi duy nhất biết loại chuyển động ⇒ test ở `CostFeedLookupAdapter` với `TransferOut`/`TransferIn` (không đẩy `TransferIn` thành `NhapKhongGia`).
4. **Chưa phủ**: `W17-4` không thêm ca HTTP/probe (feature nội bộ, 0 route mới). Nếu F5/REV yêu cầu probe `CostEngineAppService` qua Host thật thì bổ sung ở phiên sau — ghi `openQuestions`.
5. **`D-326` — kỳ MỘT NGÀY (`EffectiveFrom == EffectiveTo`) là HỢP LỆ:** `TinhBinhQuanGiaQuyenDiDong` sinh `EffectiveTo = kySau.Ngay.AddDays(-1)`; khi hai ngày nhập **liền kề**, kỳ trước có `EffectiveTo == EffectiveFrom`, và `CoHieuLucTai` dùng biên **bao gồm** (`at <= EffectiveTo`) nên ngữ nghĩa hợp lệ. Guard cũ `effectiveTo <= effectiveFrom` (ctor `CostPrice` + `CapNhatGiaVonChuaDuyet`) **chặt hơn** SRS §5.2 CK `>=` và chú thích cùng chỗ ⇒ `be` đã nới về `<` (`W17-4`/`D-326`, **0 migration**). Kỳ **NGƯỢC** vẫn NÉM `0009` (`EffectiveTo_TRUOC_EffectiveFrom__NEM_0009` **xanh**), CK DB `CK_PrcCostPrices_EffectiveRange` vẫn là hàng rào. Ca `K06-15` + `NC-8` chứng minh đỏ→xanh. (Ghi chú cũ ghi sai mã lỗi `0014` ⇒ nay **`0009`** `GiaVonKhongHopLe`.)

## Negative control
| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |
|---|---|---|---|---|---|
| 2026-11-28 | NC-1 | modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:299 | bo guard DaDuyet | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_04" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-2 | modules/Pricing/eCommerce.Pricing.Domain/TinhBinhQuanGiaQuyenDiDong.cs:150 | bo danh dau thieu gia von | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_09" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-3 | modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:146 | dao nhanh trang thai run | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_08" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-6 | modules/Pricing/eCommerce.Pricing.Domain/TinhBinhQuanGiaQuyenDiDong.cs:143 | doi cong thuc binh quan | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_06" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-7 | modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:253 | tra ve nap cua so from,to | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_14" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-8 | modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:118 | chan ky mot ngay | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~K06_15" -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
