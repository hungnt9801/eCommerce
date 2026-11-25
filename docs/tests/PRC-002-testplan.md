# Testplan `PRC-002` — 26/11/2026

- SRS: `docs/features/pricing/prc-002-gia-bac-thang-pipeline-tinh-gia.md` (§1 BR · §8 · §9 · §14)
- Kiến trúc bổ trợ: `docs/features/pricing/PRC-002-arch.md` (§6.3 bảng ca 1a/1b/1c/2a/2b/3a–3d/4 · §6.4 tên index)
- Nhánh: `feat/gd3-wp16-bang-gia` · Phiên `N74` · Hàng `W16-4` · Bước **F4 pass 1** (F5 REV làm sau)
- Quyết định nguồn: `D-218`…`D-229` (`docs/nhanh/decisions.md`) — đặc biệt `D-229` (hạ tầng test tầng app service).
- Phạm vi WP-16: **4 hạng mục** (setting snapshot · ngưỡng số bậc · guard tổng âm · 4 tên index NND). WP **không**
  thêm app service/endpoint/DTO/route mới.

## 1. Phủ BR

| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| **BR-PRC-002-001** (mọi giá là `Money`, có cột tiền tệ) | `test/eCommerce.Tests/Pricing/CostPriceTests.cs` · `CurrencyCode_lay_TU_Money__khong_tu_gan_VND` · `test/eCommerce.Tests/Pricing/TinhTienDonTests.cs` · `TONG_thanh_toan_BANG_dung_tong_cac_dong_da_lam_tron` · `test/eCommerce.Tests/Pricing/GiaCoSoResolverTests.cs` · `Cau_1_THANG_cau_2_3_4__thu_tu_cap_CO_DINH` | Domain / Unit | ✅ |
| **BR-PRC-002-002** (cấm `double`/`float`) | `test/eCommerce.Tests/Pricing/PricingBatBienTienTests.cs` · `TC10_KHONG_co_double_float_trong_be_mat_Pricing_BR002` (reflection 3 assembly Pricing) | Unit (kiến trúc) | ✅ |
| **BR-PRC-002-003** (cấm so tiền bằng `==` trên `decimal`) | `…/PricingBatBienTienTests.cs` · `TC11_KHONG_so_tien_bang_toan_tu_bang_trong_Pricing_BR003` (quét mã nguồn `modules/Pricing`) | Unit (kiến trúc) | ✅ |
| **BR-PRC-002-004** (bậc `MinQuantity` lớn nhất ≤ `quantity`) | `…/ChonBacThangTests.cs` · `Chon_bac_co_MinQuantity_LON_NHAT_khong_vuot_quantity` · `Khong_bac_nao_du_dieu_kien_thi_tra_NULL__KHONG_phai_loi` | Domain / Unit | ✅ |
| **BR-PRC-002-005** (trùng `MinQuantity` ⇒ NÉM `0008`) | `…/ChonBacThangTests.cs` · `HAI_bac_CUNG_MinQuantity_thi_NEM_0008__KHONG_chon_bua` · `…/GiaCoSoResolverTests.cs` · `HAI_bac_CUNG_MinQuantity_cung_cap_cung_priority_thi_NEM_0008` · `…/TierPriceSetAppServiceTests.cs` · `AddTier__trung_MinQuantity__nem_0008_va_khong_chen_BR005` | Domain + App | ✅ |
| **BR-PRC-002-006** (chạy hết rồi mới chọn + breakdown) | `…/GiaCoSoResolverTests.cs` · `Ket_qua_DOC_LAP_voi_THU_TU_duyet_ung_vien` · `…/ChonDonGiaCuoiTests.cs` · `CO_bac_thang_thi_bac_thang_THANG__va_cap_doi_thanh_BacThang` · `…/PriceCalculationAppServiceTests.cs` · `TinhGio__DungGiaNiemYet__tra_gia_niem_yet_va_ghi_ro_breakdown_1c` (`BaseLevel`/`WinningLevel`) | Domain + App | ✅ |
| **BR-PRC-002-007** (làm tròn chỉ qua `IRoundingPolicy`) | `…/PricingBatBienTienTests.cs` · `TC12_KHONG_decimal_Round_hoac_Math_Round_trong_Pricing_BR007` · `…/TinhTienDonTests.cs` · `Chinh_sach_KHONG_lam_tron_thi_don_gia_giu_nguyen_so_le` | Unit + kiến trúc | ✅ |
| **BR-PRC-002-008** (làm tròn từng dòng, tổng = Σ dòng) | `…/TinhTienDonTests.cs` · `TONG_thanh_toan_BANG_dung_tong_cac_dong_da_lam_tron` · `R4_thue_lam_tron_TUNG_DONG__khong_lam_tron_tong_thue` · `R4_DOI_CHUNG_neu_lam_tron_TONG_thi_ra_152__khac_153` | Domain / Unit | ✅ |
| **BR-PRC-002-009** (chỉ làm tròn tại R1–R4) | `…/PricingBatBienTienTests.cs` · `TC13_chi_lam_tron_DUNG_4_DIEM_R1_R4_BR009` (đếm 4 lời gọi/dòng) · `…/TinhTienDonTests.cs` · `R1_…`/`R2_…`/`R3_…`/`R4_…` | Domain / Unit | ✅ |
| **BR-PRC-002-010** (chính sách đọc đúng 1 lần + mã policy lưu kết quả) | `…/PriceCalculationAppServiceTests.cs` · `TinhGio__moi_khoa_chinh_sach_doc_DUNG_MOT_LAN_cho_mot_lan_tinh_1c` · `TinhGio__DungGiaNiemYet__tra_gia_niem_yet_va_ghi_ro_breakdown_1c` (`RoundingPolicyCode`) | App | ✅ |
| **BR-PRC-002-011** (mặc định `AwayFromZero`; không hồi tố) | `…/PricingChinhSachTests.cs` · `Rounding__half_away_from_zero__hop_le_va_dung_policy_1b` · `Rounding__rong__mac_dinh_half_away_from_zero_NHUNG_bao_KHONG_hop_le` · neo “không hồi tố”: snapshot đọc 1 lần (`PriceCalculationAppServiceTests` 1c) | Domain + App | ✅ |
| **BR-PRC-002-012** (hiệu lực theo `[from, to]`, bắt buộc có ngày) | `…/GiaCoSoResolverTests.cs` · `CoHieuLucTai_kiem_tra_dung_khoang` · `…/CostPriceTests.cs` · `CoHieuLucTai__dung_ca_HAI_bien_va_ben_ngoai` · `…/ChonBacThangTests.cs` · `PhatHanh_roi_KichHoat__va_KichHoat_truoc_ngay_tra_FALSE` | Domain / Unit | ✅ |
| **BR-PRC-002-013** (`Amount ≥ 0`; `PercentOff` trong `0..100`) | `…/ChonBacThangTests.cs` · `KiemTraBoBac_phat_hien_LECH_loai_tien_va_gia_AM` · `TierPrice_MinQuantity_nho_hon_1_thi_NEM_LOI` · `TierPrice_lech_loai_tien_thi_NEM_LOI` — **phần `PercentOff`**: code **chưa có** `Kind` (xem §5) | Domain | ✅ (phần đã có code) |
| **BR-PRC-002-014** (bậc là DỮ LIỆU, cấm hard-code) | `…/PricingBatBienTienTests.cs` · `TC14_bac_thang_la_DU_LIEU_KHONG_hard_code_nguong_BR014` · `…/TierPriceSetAppServiceTests.cs` · `AddTier__MaxTiersPerSet_bang_3__bac_thu_4_bi_chan_2b` (ngưỡng là cấu hình) | Domain + App | ✅ |
| **BR-PRC-002-015** (append-only; guard sai NÉM, không no-op) | `…/ChonBacThangTests.cs` · `Append_only__bo_dang_Active_thi_KHONG_sua_duoc_noi_dung` · `PhatHanh_khi_CHUA_co_bac_nao_thi_NEM_LOI` · `…/GiaCoSoResolverTests.cs` · `YeuCauChoPhepSuaNoiDung__bang_gia_ACTIVE_thi_NEM_0003__BR004` | Domain / Unit | ✅ |
| **BR-PRC-002-016** (idempotency ở biên vào) | `…/PriceCalculationAppServiceTests.cs` · `TinhGio__cung_IdempotencyKey__tra_ket_qua_da_luu_va_KHONG_ghi_them_1c` · `…/PricingBatBienTienTests.cs` · `TC15b_ban_ghi_giai_trinh_THIEU_IdempotencyKey_thi_NEM_BR016` — **TC-25/26/27 (Import)**: chưa có app service Import (xem §5) | App + Domain | ✅ (phần đã có code) |
| **BR-PRC-002-017** (phân bổ bảo toàn tổng) | `…/TinhTienDonTests.cs` · `R2_giam_gia_duoc_phan_bo_va_TONG_phan_bo_BANG_dung_khoan_goc` · `R3_phi_cung_duoc_phan_bo_va_bao_toan_tong` | Domain / Unit | ✅ |
| **BR-PRC-002-018** (khác tiền tệ ⇒ NÉM `0005`, cấm quy đổi) | `…/GiaCoSoResolverTests.cs` · `TRON_loai_tien_thi_NEM_0005__KHONG_tu_quy_doi` · `…/ChonDonGiaCuoiTests.cs` · `Bac_thang_LECH_loai_tien_thi_NEM__cam_tu_quy_doi` · `…/TinhTienDonTests.cs` · `TRON_loai_tien_thi_NEM__cam_tu_quy_doi` | Domain / Unit | ✅ |
| **BR-PRC-002-019** (kết quả tái lập được) | `…/PricingBatBienTienTests.cs` · `TC16_cung_input_thi_cung_ket_qua_1000_lan_BR019` (1.000 lần) | Domain / Unit | ✅ |
| **BR-PRC-002-020** (sổ giải trình append-only, cấm UPDATE/DELETE) | `…/PricingBatBienTienTests.cs` · `TC15_so_giai_trinh_KHONG_co_duong_sua_xoa_va_KHONG_setter_cong_khai_BR020` · `…/PriceCalculationAppServiceTests.cs` · `TinhGio__DungGiaNiemYet__tra_gia_niem_yet_va_ghi_ro_breakdown_1c` (`SoLanChen == 1`) | Domain + App | ✅ |
| **BR-PRC-002-021** (giá vốn không lộ ra DTO công khai) | `…/PricingBatBienTienTests.cs` · `TC21_gia_von_KHONG_xuat_hien_trong_DTO_cong_khai_BR021` (reflection `GioTinhTienDto`/`ChiTietDongDto`/`SanPhamCongKhaiDto`) · `…/CostPriceTests.cs` · `Ma_loi_gia_von_KHONG_trung_ma_da_dung` | Unit (kiến trúc) | ✅ |

- Tổng BR: **21** · đã phủ: **21** · chưa phủ: **0**.
- Tệp test mới của `W16-4` (theo `D-229`):
  - `test/eCommerce.Tests/Pricing/HaTang/RepoGia.cs` — `IRepository<T,Guid>` in-memory, **đếm lần chèn/sửa**, mọi
    thành viên không dùng **NÉM `NotSupportedException`**.
  - `test/eCommerce.Tests/Pricing/HaTang/SettingGia.cs` — `ISettingProvider` giả, **đếm lượt đọc theo khoá**.
  - `test/eCommerce.Tests/Pricing/HaTang/HaTangAbp.cs` — gán tay `LazyServiceProvider` (AbpLazyServiceProvider trên
    `ServiceCollection` tối thiểu: `AddLogging()` + `IGuidGenerator`).
  - `test/eCommerce.Tests/Pricing/PriceCalculationAppServiceTests.cs` — ca **1c** + idempotency + ghi sổ.
  - `test/eCommerce.Tests/Pricing/TierPriceSetAppServiceTests.cs` — ca **2a/2b** + neo BR-005.
  - `test/eCommerce.Tests/Pricing/PricingBatBienTienTests.cs` — TC-10/11/12/13/14/15/15b/16/21.
- Thêm **1 dòng** `ProjectReference` `eCommerce.Pricing.Application` vào
  `test/eCommerce.Tests/eCommerce.Tests.csproj` (khuôn `ChannelHub.Application`; `D-229` ①(A1)).

> 📌 **Điều chỉnh so với `D-229` ①(A2) — ghi rõ, không giấu:** ABP 10.7 dựng `FirstOrDefaultAsync`/`CountAsync`
  > extension **trên `GetQueryableAsync` + `AsyncExecuter`**, **không** trên `FindAsync(predicate)` (đo bằng
  > stack trace thật khi chạy ca 2a). Vì vậy `RepoGia` mở **thêm** `GetQueryable()`/`GetQueryableAsync()` và cấp
  > `AsyncExecuter = new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>())` (bộ thực thi thật của ABP,
  > fallback đồng bộ cho LINQ-to-Objects). `FindAsync(predicate)` vẫn được hiện thực như `D-229` yêu cầu; mọi
  > thành viên khác vẫn **NÉM**. Không đổi ngữ nghĩa test, không sửa `modules/**`.

## 2. Số ca theo tầng (K-06)

| Tầng | Trước | Sau | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` | **1999** | **2021** | +22 ca (không xoá/skip/nới); 0 fail |
| Domain invariant / Unit (`Pricing`) | **121** | **143** | +9 `PricingBatBienTienTests` (TC-10/11/12/13/14/15/15b/16/21) + các ca 1c/2a/2b thuộc tầng App bên dưới |
| Application (`Pricing`) | **0** | **13** | **MỚI** (`D-229`): 4 ca `PriceCalculationAppServiceTests` + 9 ca `TierPriceSetAppServiceTests` (1c/2a/2b) |
| API `/api/v1` (200 + 401/403) | — | **N/A** | WP-16 **không** thêm endpoint/controller; `TC-29` (mọi app service Pricing có `[Authorize]` cấp class) đã có ở `test/eCommerce.Tests/Authorization/BatBienPhanQuyenTests.cs` (TC-01/TC-02), không đổi trong WP này |
| UI Vitest / E2E Playwright | — | **N/A** | WP-16 **không có FE** (`PRC-002-arch.md` §6.1, `W16-3` đóng với lý do “WP không có FE”) — không có màn nào để test |
| OpenAPI drift | — | **N/A** | Không đổi DTO/route (`PRC-002-arch.md` §3.5) ⇒ `pnpm gen:api` không đổi `schema.d.ts` |

> 📌 `D-229` ② yêu cầu `dotnet test` **tăng số ca** (không giảm) — đo được **+22**, đúng.

## 3. Negative control (bắt buộc) — **DỰ KIẾN, chờ pass 2**

> 🚨 `tools/agents/negative-control.ps1` **CHƯA CHẠY** trong pass 1: script **từ chối tệp chưa commit** (exit `3`).
> Các tệp của `W16-4` đang **dirty/untracked** ⇒ chưa áp được đột biến. Bảng dưới là **kế hoạch chạy lại sau khi
> ORCH commit nhánh** (pass 2). **Chưa có** exit code đỏ→xanh thật — không ghi bằng chứng giả.

| # | Đột biến (dự kiến) | Tệp:dòng | Test phải đỏ | Lệnh test dự kiến |
|---|---|---|---|---|
| **NC-1** | Gỡ lượt đọc `Pricing:MissingPricePolicy`: `var thoThieuGia = await _settingProvider.GetOrNullAsync(PRC002SettingTenKhoa.ThieuGia);` → `var thoThieuGia = (string?)null;` | `modules/Pricing/eCommerce.Pricing.Application/PriceCalculationAppService.cs:280` | `TinhGio__moi_khoa_chinh_sach_doc_DUNG_MOT_LAN_cho_mot_lan_tinh_1c` (ThieuGia đọc 0 lần) **và** `TinhGio__DungGiaNiemYet__...` (rơi về `NemLoi`) | `dotnet test test/eCommerce.Tests --no-build --filter "FullyQualifiedName~PriceCalculationAppServiceTests"` |
| **NC-2** | Gỡ khối ngưỡng số bậc: `if (soHienTai + 1 > toiDa)` → `if (false)` | `modules/Pricing/eCommerce.Pricing.Application/TierPriceSetAppService.cs:121` | `AddTier__bac_thu_21__nem_0011_va_KHONG_ghi_nua_2a` + `AddTier__MaxTiersPerSet_bang_3__bac_thu_4_bi_chan_2b` | `dotnet test test/eCommerce.Tests --no-build --filter "FullyQualifiedName~TierPriceSetAppServiceTests"` |
| **NC-3** | `Reject` → kẹp im lặng: `if (chinhSachTongDon == ChinhSachTongDonAm.Reject && tongThanhToan.Amount < 0m)` → `if (false)` | `modules/Pricing/eCommerce.Pricing.Domain/TinhTienDon.cs:200` | `Tong_don_am__mac_dinh_Reject__NEM_0010_kem_du_lieu__3a` | `dotnet test test/eCommerce.Tests --no-build --filter "FullyQualifiedName~TinhTienDonTests"` |
| **NC-4** (nếu chạy được) | Sửa 1 ký tự trong tên index của `phaiCo` (`IX_PrcPriceLists_TenantId_Code_EffectiveFrom` → `…EffectiveFroX`) | `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` | ca “4 tên index đích danh phải có NND” | `dotnet test test/eCommerce.IntegrationTests --filter "FullyQualifiedName~PostgreSqlSchemaTests"` — **cần Docker/PostgreSQL**; nếu không có thì ghi “chờ” |

- Sau mỗi đột biến: khôi phục + `git diff` xác nhận tệp về nguyên trạng; ghi **cả** exit code đỏ và exit code xanh
  vào bảng này ở **pass 2**.

## 4. Cổng (đo trong phiên N74)

```powershell
dotnet build eCommerce.slnx --no-incremental      # 0 error / 9 warning (bằng baseline phiên)
dotnet test test/eCommerce.Tests --no-build       # 2021 pass · 0 fail · trước 1999 (+22)
pwsh -File tools/kiem-link-tai-doc.ps1            # cần chạy sau khi thêm tệp .md này
npm --prefix tools run check:mermaid              # cần chạy sau khi thêm tệp .md này
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error**, **9 warning** (không phát sinh warning mới).
- `dotnet test test/eCommerce.Tests` → **2021/2021 pass**, exit **0** (baseline đầu phiên **1999**).
- `check-agent-artifact.ps1 -Kind testplan` → **dự kiến đỏ ở mục “negative control chưa có bằng chứng”** cho tới
  pass 2 (script đòi ≥ 2 dòng có `exit N` thật). Không giả bằng chứng.

## 5. Chưa phủ / nợ

1. **Negative control thật (NC-1…NC-4)** — chờ ORCH commit nhánh rồi chạy
   `tools/agents/negative-control.ps1` ở pass 2. Không có bằng chứng đỏ→xanh ⇒ **G4 chưa đạt**.
2. **§14 SRS thiếu dòng TC cho `BR-PRC-002-010/011/013/014`** (`D-227` — nợ có chủ BA). Testplan **vẫn phủ**
   đủ 4 BR bằng test thật (xem §1); không chờ SRS.
3. **`BR-PRC-002-013` phần `PercentOff`**: SRS §5.2 mô tả `TierPrices.Kind` (`FixedUnitPrice`/`PercentOff`/
   `AmountOff`), `MaxQuantity`, `ExternalRef` và CHECK `"Kind" <> 1 OR ("Amount" BETWEEN 0 AND 100)`; code
   `TierPrice` hiện **chỉ có** `Sku/MinQuantity/AmountValue/CurrencyCode` ⇒ **không test được** nhánh
   `PercentOff`. Phần đã có code (`Amount ≥ 0`, `MinQuantity ≥ 1`, cùng loại tiền) **đã phủ**. Ghi `docDeviation`.
4. **`BR-PRC-002-016` TC-25/26/27 (Import idempotent / cùng khoá khác payload / thiếu khoá)**:
   `ITierPriceImportAppService` **chưa tồn tại** trong code (SRS §6.1 khai, `-arch` §3.5 nói hợp đồng không đổi)
   ⇒ chưa có biên Import để test. Phần **đã có** là idempotency của `TinhGioAsync` (cùng khoá ⇒ trả kết quả đã
   lưu, 0 bản ghi mới) — **đã phủ**. Ghi `docDeviation`.
5. **Test tích hợp PostgreSQL** (`TC-15`, `TC-19`, `TC-23`, `TC-24`, `TC-25/26/28`, và 4 tên index NND ở
   `PostgreSqlSchemaTests.cs`): chưa chạy trong pass 1 (không có Docker trong phiên). Không đổi schema trong
   WP-16 (`D-218` ②) nên 4 tên index là **hạng mục W16-2 đã sửa** — cần REV/pass 2 xác nhận khi có Docker.
6. **`check-feature-doc.ps1 -WP14`** không phải cổng của WP-16 (`D-227`) — không chạy.
7. Không có `apps/admin-web` thay đổi ⇒ **không** chạy `pnpm lint/typecheck/test/build`; WP không có FE (`W16-3`).
