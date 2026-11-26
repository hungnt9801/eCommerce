# Testplan `REP-001` — 27/11/2026

- SRS: `docs/features/reporting/rep-001-read-model-job-tong-hop.md` (§8.1 15 BR · §12 mã lỗi · §14.1 25 TC · §14.3 DoD)
- Kiến trúc: `docs/features/reporting/REP-001-arch.md`
- Nhánh: `feat/gd3-wp26-read-model` · Phiên `N91` · Hàng `W26-4` · Bước **F4 QA** (F5 REV làm sau)
- Quyết định nguồn: `D-270`…`D-276` (`docs/nhanh/decisions.md`) — đặc biệt `D-270` (quyền `eCommerce.Reports.*` + `Reconcile`),
  `D-271`/`D-275` (`RepRunLog` cho rebuild/reconcile/aggregate), `D-272` (rebuild `TonKho` chỉ hôm nay), `D-274` (lý do 10/20/10).
- Phạm vi F4: **3 bảng vận hành** (`RepRunLogs`/`RepPeriodCloses`/`RepConsistencyChecks`) · `ReportAdminAppService` ·
  `ReportRebuildWorker`/`ReportingAggregationWorker` · 10 endpoint §6.2 · map lỗi `0010–0017`.
  **UI (`WEB-*`) KHÔNG thuộc phạm vi** (`D-276`).

## 1. Phủ BR (14/15)

> Mỗi BR của §8.1 có **ít nhất một** test **chạy được**; mã BR ghi trong tên test/comment. Tệp mới của `W26-4` đánh `🆕`.

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-001** (read-model thuộc `Reporting`; chỉ job ghi; không write endpoint trên fact) | `test/eCommerce.Tests/Reporting/RepBatBienTests.cs` · 🆕 `TC13_ReportsController_chi_6_POST_dung_viec_va_KHONG_PUT_PATCH_DELETE` · `test/eCommerce.Tests/Reporting/Wp08BaoCaoTests.cs` · `Controller_bao_cao_PHAI_co_du_5_route_va_quyen_doc_o_cap_class` | Unit (reflection) | ✅ |
| **BR-002** (cấm báo cáo đọc bảng giao dịch lấy số đã tổng hợp; allow-list đúng một method) | 🆕 `RepBatBienTests.cs` · `TC02_ReportAppService_chi_GetTongQuanDonHangAsync_dung_bang_giao_dich` · `TC02_hop_dong_Application_Contracts_KHONG_tham_chieu_entity_giao_dich` | Unit (quét mã nguồn) | ✅ |
| **BR-003** (mọi entity read-model `IMultiTenant` + query filter; unique `NULLS NOT DISTINCT`) | 🆕 `RepBatBienTests.cs` · `TC20_du_TAM_entity_read_model_la_IMultiTenant_va_TenantId_nullable` · 🆕 `test/eCommerce.IntegrationTests/Reporting/PostgreSqlWp26ReadModelNndTests.cs` · `TC20_index_unique_tenant_dung_NULLS_NOT_DISTINCT` (8 bảng) · `TC20_cot_TenantId_cho_phep_NULL` (8 bảng) · `TC06_hai_tenant_cung_khoa_nghiep_vu__cung_ton_tai` | Unit (reflection) + Integration (PG thật) | ✅ |
| **BR-004** (tiền `decimal`, số lượng `(18,3)`, cấm `double`/`float`, so tiền bằng `Delta`) | `RepVanHanhTests.cs` · `ReportingDomain_KHONG_co_kieu_double_float` · `SoDoiChieu_lech_doanh_thu__1_dong__Delta_dung` · `Wp08BaoCaoTests.cs` · `DoD_doanh_thu_bao_cao_BANG_tong_chung_tu_ban_le_va_don_hang` | Unit | ✅ |
| **BR-005** (idempotent unique + thay thế theo `(ngày, nguồn)`; chạy lại N lần không đổi) | 🆕 `test/eCommerce.Tests/Reporting/RepVanHanhAppServiceTests.cs` · `TC11_StartRebuild_HAI_lan_cung_khoang_cung_ly_do__MOT_dong_lan_hai_AlreadyRequested` · `TC03_chay_lai_1000_lan_cung_ngay__so_dong_va_so_lieu_KHONG_doi` · `Wp08BaoCaoTests.cs` · `DoD_chay_lai_HAI_lan_KHONG_nhan_doi_du_lieu` | Unit + App | ✅ |
| **BR-006** (mỗi nguồn có dòng mốc; mốc chỉ tiến, cập nhật sau khi ghi xong) | 🆕 `RepVanHanhAppServiceTests.cs` · `BR006_chay_tong_hop_tao_du_BA_dong_moc_va_moc_doanh_thu_TIEN_toi_hom_nay` · `Wp08BaoCaoTests.cs` · `Moc_CHI_TIEN__backfill_khoang_cu_KHONG_ha_moc` · `Moc_tien_khi_co_du_lieu_moi` | Unit + App | ✅ (TC-05b runtime — xem §6) |
| **BR-007** (khoá `report:tong-hop`; không giành được ⇒ `KhongGianhDuocKhoa`) | 🆕 `RepVanHanhAppServiceTests.cs` · `TC04_tong_hop_khi_KHONG_giu_duoc_khoa__nem_KhongGianhDuocKhoa` · `RepVanHanhTests.cs` · `ReportingLockKeys_KHONG_tien_to_ecommerce` | App + Unit | ✅ |
| **BR-008** (mọi ghi qua UoW; **từng ngày** commit rồi mới sang ngày sau) | — (cần `DoWorkAsync`/UoW thật) | — | ❌ **chưa kiểm được** — xem §6 |
| **BR-009** (chỉ xoá trong lượt thay thế `(ngày, nguồn)`; cấm xoá lẻ) | 🆕 `RepBatBienTests.cs` · `TC16_moi_lenh_xoa_nam_trong_TongHopBaoCaoService_va_loc_theo_ngay` | Unit (quét mã nguồn) | ✅ |
| **BR-010** (một chiều: module nguồn cấm tham chiếu `Reporting`) | 🆕 `RepBatBienTests.cs` · `TC15_module_nguon_KHONG_tham_chieu_assembly_Reporting` | Unit (reflection assembly) | ✅ |
| **BR-011** (rebuild: quyền riêng · `FromDate`/`ToDate` · `Reason` ≥ 10 · audit + `RepRunLogs`) | 🆕 `RepVanHanhAppServiceTests.cs` · `BR011_StartRebuild_hop_le__ghi_MOT_dong_Queued_va_so_cua_so_bang_so_ngay` · `TC22_StartRebuild_khoang_DAO_nem_0002_va_KHONG_chen` · `TC22_StartRebuild_ly_do_DUNG_9_ky_tu_nem_0016_va_KHONG_chen` · `TC22_StartRebuild_TonKho_ngay_QUA_KHU_nem_0013_va_KHONG_chen` · `TC22_StartRebuild_TonKho_HOM_NAY__nhan_va_MOT_cua_so` · `RepVanHanhTests.cs` · `ReportAdminAppService_method_GHI_co_quyen_hep` | App + Unit | ✅ |
| **BR-012** (lệch phải lộ ra: `RepConsistencyChecks` + metric + log; cấm nuốt im lặng) | 🆕 `RepVanHanhAppServiceTests.cs` · `TC19_chot_ky_con_dong_lech_MO__nem_0014_va_ky_giu_Mo` · `TC19_chot_ky_KHONG_con_lech__ky_sang_DaChot_va_co_hash_64_hex` · `RepVanHanhTests.cs` · `SoDoiChieu_lech_doanh_thu__1_dong__Delta_dung` | App + Unit | ✅ (metric `reporting_*` phát thật — xem §6) |
| **BR-013** (chạy lại cùng ngày không nhân đôi) | `Wp08BaoCaoTests.cs` · `DoD_chay_lai_HAI_lan_KHONG_nhan_doi_du_lieu` · `test/eCommerce.IntegrationTests/PostgreSqlWp08BaoCaoTests.cs` · `Chan_HAI_dong_su_kien_CUNG_dong_nguon_khi_TenantId_NULL` | Unit + Integration (PG thật) | ✅ |
| **BR-014** ("chưa tổng hợp" ≠ 0: `DaTongHop`/`DaCoDuLieu`) | 🆕 `RepVanHanhAppServiceTests.cs` · `TC17_doanh_thu_read_model_RONG__DaTongHop_false_va_KHONG_co_ngay_moc` · `TC17_ton_kho_read_model_RONG__DaCoDuLieu_false` | App | ✅ |
| **BR-015** (cấm SQL nối chuỗi; nhóm/sắp xếp chỉ tập đóng; cấm PII) | 🆕 `RepBatBienTests.cs` · `TC21_module_Reporting_KHONG_co_SQL_noi_chuoi` · `TC21_tham_so_nhom_sap_xep_la_tap_DONG_enum_khong_phai_chuoi` | Unit (quét mã nguồn + reflection) | ✅ |

- Tổng BR: **15** · đã phủ: **14** · chưa phủ: **1** (`BR-008` — phụ thuộc harness `DoWorkAsync`/UoW thật, xem §6).
- **§12 map lỗi** (bổ trợ BR-011/012/013): `RepBatBienTests.cs` · `MaLoi_0010_0017_map_dung_HTTP_status` (8 mã `0010–0017` → `404/409/409/400/409/409/400/409`, Host `eCommerceHttpApiHostModule.cs`).

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (`N90` baseline) | Sau (`N91`) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **2342** | **2371** | **+29** ca mới, 0 fail; không xoá/skip/nới test cũ |
| Domain invariant | — | — | `WP-26` thêm **0** luật tồn/tiền mới; máy trạng thái `RepRunLog`/`RepPeriodClose` + khoảng/lý do đã có 53 ca `RepVanHanhTests` (từ `W26-2`) |
| Application (`ReportAdminAppService` · `ReportAppService` · `TongHopBaoCaoService`) | 0 | **13** | 🆕 `RepVanHanhAppServiceTests` — dựng `new` + fake in-memory (`D-229`/`D-248`), không host ABP |
| API `/api/v1` (200 + 401/403) | — | **Partial** | Endpoint §6.2 kiểm **metadata** `[Authorize]` cấp class + quyền hẹp từng method (`RepVanHanhTests` 3 ca + `RepBatBienTests` 1 ca). **401/403 runtime** cần host + token ⇒ xem §6 |
| UI Vitest / E2E Playwright | — | **N/A** | `D-276`: **0 dòng** `apps/admin-web/**`; màn vận hành thuộc `WEB-*` |
| OpenAPI drift | — | **N/A** | `W26-3` không đổi DTO; nợ contract `openapi.v1.json`/`schema.d.ts` có chủ (`be`, `D-276`) |
| Integration (`test/eCommerce.IntegrationTests`) | **122** | **139** | **+17** trên PostgreSQL 17 thật (Testcontainers): 8 index NND + 8 cột `TenantId` nullable + 1 cách ly tenant |

> 📌 Số ca **chỉ tăng**: unit **2342 → 2371 (+29)**, integration **122 → 139 (+17)**; 0 fail. Build `dotnet build eCommerce.slnx --no-incremental`
> = **0 error / 9 warning** (= baseline, **0 warning mới**).

## 3. NFR

| NFR | Có ca đo? | Ghi chú |
|---|---|---|
| NFR-001 (job 100k dòng < 60 s) | ❌ | Cần integration job thật + dữ liệu sinh sẵn — **chưa kiểm được** (§6). |
| NFR-002 (API p95 < 300 ms) | ❌ | Cần host + benchmark — chưa kiểm. |
| NFR-003 (watermark lag < 300 s) | ❌ | Gauge `reporting_watermark_lag_seconds` có điểm phát (`ReportRebuildWorker`), chưa đo runtime. |
| NFR-004 (khớp nguồn 0 đồng) | ⚠️ | `TC-05` runtime chưa kiểm; logic đối chiếu có ca (`SoDoiChieu`). |
| NFR-005 (1.000 lần không đổi) | ✅ | `TC03_chay_lai_1000_lan…` (unit). |
| NFR-006 (3 instance) | ❌ | Cần **2 process + Redis thật** — chưa kiểm. |
| NFR-007 (quan sát `RepRunLogs`) | ⚠️ | `NhatKyAggregateTests` (6) + `D-275` hàm thuần; worker ghi thật chưa kiểm. |
| NFR-008 (cách ly tenant) | ⚠️ | Lớp DB có ca (`TC06` hai tenant); query filter runtime chưa kiểm. |
| NFR-009 (chạy tiếp 0 cửa sổ cũ) | ❌ | Cần `DoWorkAsync` — chưa kiểm. |
| NFR-010 (kích thước read-model) | ❌ | Chưa kiểm. |

## 4. Negative control

> Chạy `pwsh -File tools/agents/negative-control.ps1` cho từng ca: **đột biến thật → test phải ĐỎ (exit ≠ 0) →
> khôi phục byte gốc → test phải XANH (exit 0)**. `TestCommand` **có build lại** (script sửa mã nguồn).
> Sau mỗi ca: `git status --porcelain -- modules/` rỗng. Nhóm luật quan trọng có NC: **trạng thái** (`RepPeriodClose`
> ngưỡng lý do) · **chặn chốt kỳ khi còn lệch** (`KhoangChot`) · **bất biến ảnh tồn kho** (guard `KiemRebuildTonKho`).

| Ngày | Mã | Tệp:dòng | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-27 | NC-1 | modules/Reporting/eCommerce.Reporting.Domain/VanHanh/RepPeriodClose.cs:20 | NguongLyDo dong ky 10 -> 9 (ly do 9 ky tu bi nhan) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter FullyQualifiedName~RepVanHanh --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-2 | modules/Reporting/eCommerce.Reporting.Domain/VanHanh/KhoangChot.cs:66 | Noi dieu kien chan chot ky: soDongMo > 0 -> > 1 (con 1 dong lech van chot duoc) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter FullyQualifiedName~RepVanHanh --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-3 | modules/Reporting/eCommerce.Reporting.Domain/VanHanh/KhoangChot.cs:54 | Dao guard rebuild TonKho qua khu: !(...) -> (...) (ngay cu duoc rebuild) | dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --filter FullyQualifiedName~RepVanHanh --nologo -v q | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

**Kết luận NC:** **3/3 đỏ → xanh** (đo 27/11/2026). Sau cả 3 ca `git status --porcelain -- modules/ src/ shared/` **rỗng**
(script tự khôi phục byte gốc + tự kiểm lại). NC-1 khoá ngưỡng lý do `D-274` (9 ký tự **bị** từ chối); NC-2 khoá cổng
chốt kỳ `0014` (còn **1** dòng lệch vẫn **không** chốt được); NC-3 khoá `D-272` (`TonKho` ngày cũ **bị** từ chối, hôm nay
được nhận). Ghi chú: một đột biến "đổi **giá trị** hằng `ReportingErrorCodes.ThieuLyDo` 0016 → 0018" đã **bị loại** vì
test so với **chính hằng** ⇒ đột biến vô nghĩa (không đỏ) — thay bằng NC-3 (đột biến thật ở guard).

## 5. Cổng (đo trong phiên `N91`, ORCH tự chạy lại)

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | **0** | **0 error / 9 warning** (= baseline; 0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` | **0** | **2371/2371** pass (baseline 2342 → **+29**) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | **0** | **139/139** pass (baseline 122 → **+17**), Docker/Testcontainers `postgres:17-alpine` |
| `pwsh -File tools/agents/negative-control.ps1` ×3 | **0** | 3/3 **đỏ (exit 1) → xanh (exit 0)** (§4) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan …` | **0** | `BR trong SRS=17 · chua co trong testplan=0 · KET QUA: DAT` |

## 6. Chưa kiểm được / nợ

1. **BR-008 · TC-09 · TC-10 · TC-05b · TC-22(runtime resume)** — cần **harness `DoWorkAsync`** cho worker
   `ReportRebuildWorker`/`ReportingAggregationWorker` (UoW commit từng cửa sổ, checkpoint). Handover `N90` §6 ghi rõ nợ
   này; `D-248` cho phép dựng harness test-only, nhưng `ReportRebuildWorker.DoWorkAsync` phụ thuộc
   `PeriodicBackgroundWorkerContext` + `IServiceScopeFactory` + UoW/tenant thật ⇒ **chưa dựng trong lượt này**.
   ⇒ **KHÔNG** đánh dấu phủ `DoWorkAsync`/BR-008; **giữ nguyên "chưa kiểm được"** (khuôn `D-248` ③).
2. **API 401/403 runtime** — metadata quyền đã kiểm (`RepVanHanhTests` + `RepBatBienTests`); gọi thật cần host +
   token, chưa có trong `test/eCommerce.IntegrationTests` (raw SQL + hàm thuần). Nợ có chủ (`rev`/`ops` nếu cần).
3. **NFR định lượng** (001/002/003/006/009/010) — chưa có ca đo; testplan ghi `❌` thay vì suy đoán.
4. **Metric phát thật** — `ReportingProjectionFailed`/`ReportingRowsWritten`/`ReportingReplaceConflict` có điểm phát
   trong `ReportAdminAppService`/`ReportRebuildWorker`; cấu trúc `MetricsTests` (30 tên, hậu tố) đã xanh, **giá trị
   runtime** chưa đo.
5. **UI/E2E/OpenAPI drift** — `N/A` (`D-276`: 0 dòng FE; contract có chủ).
6. ⚠️ **Lệch hợp đồng cần BA xem** (không tự sửa SRS): SRS §14.2 ghi fixture `TongHopDoanhThu.TinhNgay(chungTu, dong)`
   trả `.TongDoanhThu`/`.TongHop`; as-built trả `KetQuaTinhNgay` có `.Dong`/`.TongHop` (không có `TongDoanhThu`). Test
   viết theo **as-built**, không sửa mã sản phẩm.
