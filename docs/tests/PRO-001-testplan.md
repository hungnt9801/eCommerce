# Testplan `PRO-001` — 28/11/2026

- SRS: `docs/features/promotions/pro-001-voucher-coupon.md` (**v0.13 — 28/11/2026**, `D-307` ①(5)(b) + `D-308`)
- Kiến trúc bổ trợ: `docs/features/promotions/PRO-001-arch.md` (v0.5, `D-295`/`D-296`/`D-305`/`D-306`)
- Nhánh: `feat/gd3-wp25-voucher` · Phiên `N103` · Hàng `W25-4` · WP-25 (`W25-2` F3 BE → `W25-3` F3 FE 0 dòng màn hình + tái sinh hợp đồng → **`W25-4` F4 QA**)
- Quyết định nguồn: `D-299` (bề mặt `/api/v1/voucher` 7 route · permission hẹn `.Voucher.*` · audit) · `D-303`/`D-304` (DTO + guard tường minh 400) · `D-305` (`Σ DiscountAmount` = `OrderDiscountTotal`) · `D-306` (job `voucher-expiry`) · `D-307` (FE 0 dòng; giữ auto `voucher-admin`) · `D-308` (hợp đồng OpenAPI 75→82 path)
- Phạm vi QA: test `/api/v1/voucher/**` (surface + OpenAPI), hàng rào DB còn nợ của `W25-2` (rollback UoW, `UsageSequence` `23505`/`25P02`/SAVEPOINT, guard `Status=0` của job), probe HTTP 401/403/200. **Không** sửa code sản phẩm.

## 1. Phủ BR

> Nguồn test: **116** ca `W25-2` (domain/app/worker) + **26** ca mới `W25-4`: `Promotions/VoucherV1SurfaceTests.cs` (8) ·
> `Promotions/VoucherOpenApiContractTests.cs` (4) · `Promotions/VoucherDomainRulesTests.cs` (10) ·
> `Promotions/VoucherMaxBatchSizeTests.cs` (+4 `D-309`) · `IntegrationTests/PostgreSqlVoucherWp25Tests.cs` (6). Mã BR ghi literal `BR-01`…`BR-26` (SRS §1, dòng 141).

| BR | Nội dung (SRS §1) | Test (tệp · tên method) | Tầng | Trạng thái |
|---|---|---|---|---|
| **BR-01** | Lượt dùng cưỡng chế tầng DB bằng `UPDATE … WHERE`; 0 dòng ⇒ `0007` (mã) / `0119` (promotion) / `0104` (khách); `CHECK` CR-11 chốt cuối; cấm tin bộ nhớ/lock | `Promotions/PromotionVoucherGuardTests.cs` · `D292iii_voucher_cay_TRUE_het_luot_MUC_MA__nem_0007` · `…_MUC_PROMOTION__nem_0119` · `…_MUC_KHACH__nem_0104`; `Promotions/PromotionEngineTests.cs` · `Nguon_ma_het_luot_muc_ma__nem_0007`; `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC33_ProVoucherCodes_dung_MOT_CHECK_cong_luot__khong_con_UsedCount_le_MaxUsage` · `TC33_ghi_vuot_luot_ma__UsedCount_cong_ReservedCount_vuot_MaxUsage__bi_chan_23514`; `IntegrationTests/PostgreSqlVoucherWp25Tests.cs` · `D295_trung_UsageSequence_cung_khach__bi_chan_23505` | Unit + Integration | ✅ |
| **BR-02** | Mọi biên vào ghi lượt idempotent trước side effect; cùng khoá + cùng payload ⇒ bỏ qua; khác payload ⇒ `Messaging:0001` | `Promotions/VoucherRedemptionCommitTests.cs` · `D305_replay_khop_so__tra_ket_qua_cu_AlreadyApplied_true` · `D305_replay_lech_so__nem_0111_KHONG_phai_Messaging_PayloadConflict` · `D305_payload_khoa_KHONG_chua_OrderDiscountTotal__van_so_0111` | Unit (application) | ✅ |
| **BR-03** | `IdempotencyKey` ổn định (`Checkout:{tenant}:{session}:{code}`, `PromotionCommit:{tenant}:{order}:{code}`); cấm `Guid.NewGuid()` mỗi lần | `Promotions/VoucherRedemptionCommitTests.cs` · `D305_khoa_commit_dung_dang_PromotionCommit`; `Promotions/VoucherRequestValidatorTests.cs` · `D303_Reserve_CheckoutSessionId_hop_le__KHONG_nem` · `D303_CheckoutSessionId_la_string_o_CA_HAI_record_khong_phai_Guid` | Unit (application) | ✅ |
| **BR-04** | `UsageSequence` = số nhỏ nhất chưa bị lượt còn hiệu lực giữ; trần 5 lần thử; `MaxUsagePerCustomer = NULL` ⇒ không trần (cấm gán 1); unique NND `(Tenant, Promotion, Customer, Sequence)` | `Promotions/UsageSequenceAllocatorTests.cs` · `BR04_TimSoBatDau_lay_so_NHO_NHAT_con_trong__khong_phai_count_1` · `BR04_TimSoBatDau_het_so_trong_tran_khach__null_nem_0104_ngay` · `D295_het_tran_5_lan_thu__tra_null_va_DUNG_5_lan` · `D295_23505_index_KHAC__nem_ngay_khong_thu_tiep` · `D295_trung_so__thu_so_ke_tiep_roi_thanh_cong`; `Promotions/PromotionVoucherGuardTests.cs` · `D292iii_voucher_cay_TRUE_het_luot_MUC_KHACH__nem_0104`; `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC43c_tran_theo_khach__ghi_trung_cung_khach_va_thu_tu_con_hieu_luc_bi_chan_23505` · `TC43c_ca_HOP_LE_luot_Released_Status_2_khong_chieM_cho`; `IntegrationTests/PostgreSqlVoucherWp25Tests.cs` · `D295_trung_UsageSequence_cung_khach__bi_chan_23505` · `D295_CO_savepoint__rollback_to_savepoint_roi_transaction_dung_tiep_duoc` | Unit + Integration | ✅ |
| **BR-05** | Một đơn tối đa một lượt `Applied`; unique lọc `(TenantId, OrderId)` NND; chốt mã thứ hai ⇒ `0115` | `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC43a_mot_don_mot_voucher__ghi_hai_luot_cung_don_voucher_bi_chan_23505` · `TC43i_mot_don_1_voucher_va_2_tu_dong_KHAC_promotion__cong_don_KHONG_bi_chan` · `TC33_bon_partial_index_redemptions_va_unique_mot_goc_conditions_dung_filter_va_NND`; `Promotions/VoucherStateMachineTests.cs` · `Luot_da_Applied__ChotApplied_lan_hai__nem_0105` | Unit + Integration | ✅ (hàng rào DB; **map `0115` trong service chưa có ca riêng** — §5 #4) |
| **BR-06** | `Σ DiscountAmount` các lượt `Applied` cùng đơn = `OrderDiscountTotal`; lệch ⇒ `0111` (nhánh replay cũng so) | `Promotions/VoucherRedemptionCommitTests.cs` · `D305_replay_lech_so__nem_0111_KHONG_phai_Messaging_PayloadConflict` · `D305_replay_khop_so__tra_ket_qua_cu_AlreadyApplied_true` · `D305_payload_khoa_KHONG_chua_OrderDiscountTotal__van_so_0111`; `Promotions/VoucherRequestValidatorTests.cs` · `D305_Commit_OrderDiscountTotal_am__nem`; `IntegrationTests/PostgreSqlVoucherWp25Tests.cs` · `BR06_rollback_cung_UoW__luot_van_Reserved_va_bo_dem_khong_doi` | Unit + Integration | ✅ |
| **BR-07** | `Σ DiscountPerLine == DiscountTotal` chính xác; `DiscountTotal ≤ Subtotal` (%, cố định); vượt ⇒ `0009`, **cấm kẹp im lặng** | `Promotions/PromotionEngineTests.cs` · `Tran_khong_kep__ma_vuot_Subtotal__nem_0009` · `Tu_dong_vuot_tran__bo_candidate_chu_khong_nem`; `Pricing/TinhTienDonTests.cs` · `R2_giam_gia_duoc_phan_bo_va_TONG_phan_bo_BANG_dung_khoan_goc` · `R4_thue_tinh_THEO_TUNG_DONG_va_tong_thue_la_TONG_cac_dong` | Unit (domain + Pricing) | ✅ |
| **BR-08** | Tiền là `decimal` kèm `Currency`; cấm `double`/`float`; cấm so tiền bằng `==`; khác `Currency` ⇒ `Money:0001` | `Money/MoneyTests.cs` · `So_sanh_khac_loai_tien_thi_NEM_LOI` · `Cong_HAI_LOAI_TIEN_KHAC_NHAU_thi_NEM_LOI` · `So_sanh_lon_be`; `Pricing/TinhTienDonTests.cs` · `Giam_gia_cap_don_LECH_loai_tien_thi_NEM`; `Promotions/VoucherRequestValidatorTests.cs` · `D304_Reserve_Currency_sai__nem` | Unit | ✅ (hành vi; **chưa** có test quét tĩnh `double`/`float` riêng cho `Promotions` — §5 #5) |
| **BR-09** | `Exhausted`/`Expired` là **suy ra**, cấm ghi vào cột `Status`; vị từ "còn dùng được" = vị từ trong `UPDATE` giành lượt | `Promotions/VoucherDomainRulesTests.cs` · `BR09_enum_Status_ma_KHONG_chua_trang_thai_suy_ra`; `Promotions/VoucherExpiryWorkerTests.cs` · `D306_chi_nha_Reserved__SQL_guard_Status_0_va_log_Debug_khi_0_dong`; `Promotions/VoucherStateMachineTests.cs` · `Ma_da_Revoked__Issue__nem_0006` | Unit (domain + quét mã nguồn) | ✅ |
| **BR-10** | Mọi chuyển trạng thái mã/lượt qua guard; sai ⇒ `0006`/`0105`; cấm no-op im lặng (trừ A2/huỷ/trả hàng) | `Promotions/VoucherStateMachineTests.cs` · `Ma_da_Issued__Issue_lai__nem_0006` · `Ma_Generated__Revoke__nem_0006_khong_nhay_cap` · `Luot_het_TTL__ChotApplied__nem_0105_giu_Reserved` · `Luot_da_Released__ChotApplied__nem_0105` · `Luot_Released_hoac_Reversed__NhaReleased__nem_0105` · `Luot_khong_Applied__DaoReversed__nem_0105`; `Promotions/VoucherRedemptionCommitTests.cs` · `D298_luot_khong_ton_tai__nem_0105_truoc_guard` | Unit (domain) | ✅ |
| **BR-11** | `ProPromotionRedemptions` append-only: cấm `DELETE`; chỉ `UPDATE` cột chuyển trạng thái; cấm sửa `DiscountAmount`/`IdempotencyKey`/`VoucherCodeId`/`PromotionId`/`CustomerId`/`UsageSequence` | `Promotions/VoucherDomainRulesTests.cs` · `BR11_so_luot_append_only__cot_bat_bien_khong_setter_cong_khai__khong_co_duong_xoa` (reflection 7 cột bất biến không public setter + quét 0 `DELETE FROM "ProPromotionRedemptions"` / 0 `_redemptions.DeleteAsync`/`.Remove(`); `Promotions/VoucherStateMachineTests.cs` · `Luot_da_Applied__ChotApplied_lan_hai__nem_0105` | Unit (phản chiếu + quét mã nguồn) | ✅ (DB **không** trigger — SRS §8.1 #11; chặn bằng entity đóng setter + review) |
| **BR-12** | Lượt chỉ sinh từ luồng nội bộ `web`; giảm giá của sàn (`ChannelDiscount`) không tiêu lượt nội bộ | `Promotions/PromotionSurfaceTests.cs` · `Pos_KHONG_tham_chieu_Promotions__TC38` (đường quầy ngoài phạm vi) | Unit (kiến trúc) | ✅ (một phần — **`0110` cho kênh ≠ `web` tại `ReserveAsync` chưa có test**; §5 #3) |
| **BR-13** | Mọi thao tác ghi qua UoW; cấm `autoSave: true` trong `Promotions.Application` | `Promotions/VoucherDomainRulesTests.cs` · `BR13_KHONG_autoSave_true_trong_duong_ghi_voucher__ngoai_no_cu_PromotionAppService` (quét `Promotions.Application`: 0 ở mọi tệp ngoài allowlist nợ cũ; 0 tuyệt đối ở 3 tệp đường ghi voucher) | Unit (quét mã nguồn) | ✅ (một phần — **nợ cũ `PRO-002`/W24**: `PromotionAppService.cs` còn 7 chỗ `autoSave: true`, allowlist có chủ đích; §5 #11) |
| **BR-14** | Mọi unique index có `TenantId` phải `NULLS NOT DISTINCT`, viết SQL thô trong migration | `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC33_bon_partial_index_redemptions_va_unique_mot_goc_conditions_dung_filter_va_NND`; `IntegrationTests/PostgreSqlSchemaTests.cs` · test đếm/chốt toàn bộ index `NULLS NOT DISTINCT` (`F1_du_…_null…`) | Integration (PostgreSQL thật) | ✅ |
| **BR-15** | Mọi app service `[Authorize]` cấp **CLASS**; permission chỉ dùng hằng số; permission mới phải `AddPermission` (thiếu ⇒ 403 cả admin) | `Promotions/VoucherV1SurfaceTests.cs` · `D307a_Authorize_cap_CLASS_la_Promotions_Default` · `D307a_map_method_sang_permission_dung` · `D307b_giu_be_mat_auto_voucher_admin__khong_tat_remote_service`; `Promotions/PromotionSurfaceTests.cs` · `AppService_co_Authorize_cap_CLASS_va_method_hep` · `Quyen_Promotions_khai_CA_HAI_NOI` | Unit (phản chiếu) | ✅ |
| **BR-16** | Rule mới khai `DeclaredCostFactor` trong `ProPromotionRuleCosts` và phải THUẦN (không I/O) | `Promotions/PromotionRuleCatalogTests.cs` · `TC71ii_seed_du__IsSeeded_true_va_gia_tri_theo_dong_ke_ca_RetiredAtUtc` · `TC71i_bang_chi_phi_RONG__tra_du_SystemNames_mac_dinh_IsSeeded_false_khong_nem_0010`; `Promotions/PromotionRuleDiTests.cs` · `Di_resolve_IPromotionConditionRule__du_4_rule`; `Promotions/PromotionDomainTests.cs` · `Registry_tat_dinh_va_chan_trung` | Unit (domain/DI) | ✅ |
| **BR-17** | `PRO-001` chỉ phục vụ kênh `web`; đơn kênh khác ⇒ `0110`; kênh quầy (`RetailBill`) ngoài phạm vi | `Promotions/PromotionSurfaceTests.cs` · `Pos_KHONG_tham_chieu_Promotions__TC38`; `Promotions/VoucherV1SurfaceTests.cs` · `D307a_dung_7_route__va_0_route_ngoai_pham_vi` (0 route `reserve`/`commit`) | Unit (kiến trúc) | ✅ (một phần — **`0110` chưa có test**; §5 #3) |
| **BR-18** | v1 không có đường sửa giá trị/giới hạn; allowlist đúng 7 method + 10 method `IPromotionAppService`; ngoại lệ duy nhất `OverrideLimitAsync` (`0116`) | `Promotions/VoucherV1SurfaceTests.cs` · `TC40_IVoucherAdminAppService_dung_7_method__khong_UpdateAsync` · `TC40_controller_KHONG_tu_them_op_ngoai_7_method_cua_hop_dong`; `Promotions/PromotionV1SurfaceTests.cs` · `TC70b_IPromotionAppService_van_dung_10_method__khong_UpdateAsync`; `Promotions/VoucherStateMachineTests.cs` · `DoiGioiHan_nho_hon_1__nem_0116` · `DoiGioiHan_hop_le__doi_MaxUsage` | Unit (phản chiếu + domain) | ✅ |
| **BR-19** | `Percentage`: `0 < DiscountValue ≤ 100` **và** `MaxDiscountAmount` BẮT BUỘC; vi phạm ⇒ `AbpValidationException` (400, không `Code`) trước khi ghi | `Promotions/VoucherDomainRulesTests.cs` · `BR19_Percentage_ngoai_0_100__nem_AbpValidationException` · `BR19_Percentage_thieu_tran__nem_AbpValidationException` · `BR19_Percentage_hop_le_co_tran__KHONG_nem` · `BR19_Percentage_bang_0_co_tran__v1_as_built_CHAP_NHAN__noi_0109` | Unit (domain) | ✅ (riêng `DiscountValue = 0` là **as-built chấp nhận** — `D-296`, `TC-21b`) |
| **BR-20** | `MinOrderAmount` đo trên **tiền hàng chưa thuế, chưa phí ship**; cấm đo `GrandTotal` | `Promotions/PromotionDomainTests.cs` · `Rule_MinOrderAmount`; `Promotions/PromotionEngineTests.cs` · `Khong_co_candidate__NoCandidate` | Unit (domain/engine) | ✅ (chưa khẳng định `ReasonCode` nêu "số còn thiếu" — `TC-27`, §5 #6) |
| **BR-21** | Metric mới vào `eCommerceMetricNames` + `KyHieuMetric` + `XC-005` §5.1 + test cùng PR; tên đúng hậu tố; cấm nhãn cardinality cao | `Observability/MetricsTests.cs` · `Bang_dang_ky_KHONG_trung_ten_va_KHONG_rong`; `Observability/MetricEmitterTests.cs` (quét hai chiều); `Promotions/VoucherExpiryWorkerTests.cs` · `D306_reservation_expired_KHONG_con_trong_ChuaCoNoiPhat__applied_van_con` · `D306_worker_PHAT_2_counter_dung_nhan` | Unit (kiến trúc) | ✅ |
| **BR-22** | Seed idempotent; cấm seed voucher nghiệp vụ thật; cấm log mã voucher/PII | `Promotions/VoucherDomainRulesTests.cs` · `BR22_log_KHONG_mang_ma_voucher_gan_khach__va_KHONG_seed_voucher_nghiep_vu` (quét `Logger.*` cấm `{Code}`/`{VoucherCode}`/`{CustomerId}`/`{Reason}`/`voucher.Code`/`input.Reason`; seeder module không chứa `VoucherCode`; `VoucherCode.TaoMoi` chỉ ở đường phát hành runtime) | Unit (quét mã nguồn) | ✅ (một phần — "seed 2 lần vẫn 0 dòng mới" chưa đo bằng DB; §5 #12) |
| **BR-23** | `CheckoutInput.OrderDiscount ≠ 0` ⇒ NÉM (sau replay, trước tính tiền); đơn `web` chỉ nhận giảm từ `Promotions` | `Promotions/PromotionCheckoutGuardTests.cs` · `OrderDiscount_khac_0__nem_400_va_khong_giau_cho` · `OrderDiscount_0__KHONG_bi_chan_boi_guard_D6` · `Co_gat_Promotions_Enabled_trong_Checkout` | Unit (application `Sales`) | ✅ |
| **BR-24** | Vãng lai (`CustomerId = NULL`) ⇒ `0118` trước engine/`UPDATE`; cấm `[AllowAnonymous]` cho đường voucher/checkout | `Promotions/VoucherV1SurfaceTests.cs` · `BR24_Khong_co_AllowAnonymous_trong_VoucherController_va_khong_co_reserve_commit_trong_nguon`; `Authorization/AllowlistCongKhaiTests.cs` · `MOI_AllowAnonymous_phai_nam_trong_allowlist__va_nguoc_lai`; `Promotions/PromotionEngineTests.cs` · `Tu_dong_tran_theo_khach_ma_vang_lai__CustomerRequired` | Unit (kiến trúc/engine) | ✅ (một phần — **`0118` tại `ReserveAsync` chưa có ca service**; §5 #3) |
| **BR-25** | Huỷ đơn trước giao: lượt `Applied → Released` **chỉ** qua consumer `OrderCancelledEto`; hoàn `UsedCount`; lượt `Reversed` ⇒ `0105` | `Promotions/VoucherStateMachineTests.cs` · `Luot_Applied__NhaReleased__duoc_phep_huy_don_D148` · `Luot_Released_hoac_Reversed__NhaReleased__nem_0105`; `Promotions/PromotionVoucherGuardTests.cs` · `TC72_voucher_cua_promotion_khong_Active__khong_ap_va_0_dong` (đối chứng trạng thái) | Unit (domain) | ✅ (một phần — consumer `OrderCancelledEto` **chưa tồn tại**, nợ `SAL-004`; §5 #2) |
| **BR-26** | Voucher chỉ giảm cấp đơn; registry đúng 4 `SystemName`; lá `SkuInclude`/`SkuExclude`/`CategoryInclude` ⇒ `0004` cả khi duyệt và khi đánh giá | `Promotions/PromotionDomainTests.cs` · `Validator_rule_chua_tung_dang_ky__0004__da_go__0010` · `Validator_hai_goc_hoac_node_lai__0004`; `Promotions/PromotionRuleDiTests.cs` · `Di_resolve_PromotionRuleRegistry__du_4_SystemName_va_khong_nem`; `Promotions/PromotionRuleCatalogTests.cs` · `TC70d_TC71v_moi_SystemName_co_Label_khac_rong_va_khac_SystemName__4_nhan_v1_dung` | Unit (domain/DI) | ✅ |

- Tổng BR: **26** · đã phủ: **26** · chưa phủ: **0**.
- 📌 `D-309` (pre-check dung lượng mã của `GenerateAsync`) **không** có BR riêng trong §1 — thuộc `A5`/`TC-02`/NFR-13; ca + bằng chứng ở **§1.2**.
- BR phủ **một phần** (có test nhưng thiếu tiểu-ca): `BR-05` (`0115` mapping), `BR-08` (quét `double`), `BR-12`/`BR-17` (`0110`), `BR-13` (nợ cũ `PromotionAppService`), `BR-20` ("số còn thiếu"), `BR-22` (seed 2 lần trên DB), `BR-24` (`0118` service), `BR-25` (consumer ETO).

### 1.1 Ca mới `W25-4` (đối chiếu `D-307` ①(5)(c) + `D-308`)

| Nhóm | Nội dung | Test | Kết quả |
|---|---|---|---|
| Surface (`/api/v1/voucher`) | `[Route]` class `api/v1/voucher`; `[Authorize]` class **đúng một** = `Promotions.Default`; **đúng 7** route; map method→permission; 0 `[AllowAnonymous]`; 0 route `reserve`/`commit`; allowlist **7** method; giữ bề mặt auto `voucher-admin` | `Promotions/VoucherV1SurfaceTests.cs` · 8 Fact | ✅ 8/8 |
| Hợp đồng OpenAPI (`D-308`) | đúng **7** path `/api/v1/voucher/**` + đúng bộ literal; method từng path; 0 `reserve`/`commit`; đối chiếu **hai chiều** với `VoucherController` (template `{id:guid}`→`{id}`) | `Promotions/VoucherOpenApiContractTests.cs` · 4 Fact | ✅ 4/4 |
| Luật domain còn nợ | `BR-19` (Percentage) 4 ca + `BR-09` (enum không chứa `Expired`/`Exhausted`) + `BR-11`/`BR-13`/`BR-22` (quét mã nguồn/reflection) | `Promotions/VoucherDomainRulesTests.cs` · 10 ca (3 theory + 7 fact) | ✅ 10/10 |
| Integration PostgreSQL thật | `BR-06` rollback UoW; `D-295` `23505` · `25P02` không SAVEPOINT · SAVEPOINT cứu transaction; `D-306` guard `Status = 0` (0 dòng) + đối chứng (1 dòng) | `IntegrationTests/PostgreSqlVoucherWp25Tests.cs` · 6 Fact | ✅ 6/6 |

### 1.2 `D-309` — pre-check dung lượng mã `GenerateAsync` (`A5`/`TC-02`/NFR-13)

> `decider` chốt `D-309` (REV P2 #1): input **hợp lệ** theo guard độ dài vẫn bất khả thi (`{Length:3, Prefix:"AB",
> Quantity:100}` chỉ có `36^(3−2)=36` mã phân biệt) ⇒ trước đây treo vô hạn trong `while`. `be` vá pre-check tại
> `modules/Promotions/eCommerce.Promotions.Application/VoucherAdminAppService.cs:133-152`; `qa` thêm ca **không sửa code**.

| Nội dung | Test (`Promotions/VoucherMaxBatchSizeTests.cs` · method) | Kết quả |
|---|---|---|
| `{Length:3, Prefix:"AB", Quantity:100}` ⇒ **NÉM `0107`** (`VoucherCodeInvalid`, data `codespace=36`/`quantity=100`), **0 dòng ghi**, **< 1 s** (không treo) | `D309_codespace_thieu__nem_0107_khong_treo` (dùng `Task.WaitAsync(5 s)` để treo ⇒ đỏ, không treo bộ test) | ✅ |
| `{Length:12, Quantity:10000}` (`36^12 ≫ 10000`) ⇒ sinh **đủ 10000**, không ném | `D309_codespace_du__sinh_du_so_dong` | ✅ |
| Biên `space == Quantity`: `{Length:4, Prefix:"AB", Quantity:1296}` = `36^2` ⇒ **KHÔNG** ném | `D309_codespace_bang_dung_Quantity__KHONG_nem` | ✅ |
| Thứ tự: `Quantity=10001` + `{Length:3, Prefix:"AB"}` ⇒ `0108` (trần lô) **trước**, không phải `0107` | `D309_thu_tu__quantity_10001_van_0108_truoc_0107` | ✅ |

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (baseline N103) | Sau (W25-4) | Ghi chú |
|---|---|---|---|
| `test/eCommerce.Tests` (unit) | **2593** | **2619** | **+26** ca (`VoucherV1SurfaceTests` 8 · `VoucherOpenApiContractTests` 4 · `VoucherDomainRulesTests` 10 · `VoucherMaxBatchSizeTests` +4 `D-309`). 0 fail. Số ca **chỉ tăng**. |
| `test/eCommerce.IntegrationTests` (PostgreSQL thật) | **156** | **162** | **+6** ca `PostgreSqlVoucherWp25Tests`. 0 fail. |
| Domain invariant (`Promotions`) | 116 (W25-2) | 126 | +10 `VoucherDomainRulesTests` (`BR-09`/`BR-11`/`BR-13`/`BR-19`/`BR-22`). |
| Application (`VoucherAdminAppService`/`VoucherRedemptionService`) | `VoucherMaxBatchSizeTests` 7 · `VoucherStateMachineTests` 18 · `VoucherRequestValidatorTests` 26 · `VoucherRedemptionCommitTests` 6 · `UsageSequenceAllocatorTests` 11 | không đổi | Happy/sai dữ liệu phủ; **sai quyền** phủ ở tầng route (`VoucherV1SurfaceTests` + `AllowlistCongKhaiTests`) + probe T04b ⇒ `403` thật. |
| API `/api/v1` 200/401/403 | — | **401×3 · 200 · 403 đo thật** | `tools/probe-voucher-wp25.ps1` (§3). |
| UI Vitest / Playwright | — | **N/A** | `W25-3` = **0 dòng code màn hình** (`D-307`); màn voucher thuộc `WEB-001` (chưa xếp lịch, `D-247`). |
| OpenAPI drift | 82 path (W25-3) | không đổi | `VoucherOpenApiContractTests` khoá 7 path; `D-308` đã tái sinh, **không** đổi thêm ở `W25-4`. |

## 3. Negative control

> Chạy `pwsh -File tools/agents/negative-control.ps1` cho từng ca: **đột biến thật → test PHẢI ĐỎ (exit ≠ 0) →
> khôi phục byte gốc → test PHẢI XANH (exit 0)**. `TestCommand` **có build lại** (không `--no-build`) vì script
> sửa mã nguồn. Bảng dưới do script tự chèn dòng bằng chứng ở **cuối tệp**; xác nhận sau mỗi ca:
> `git status --porcelain -- src/ modules/` rỗng.
>
> 🔒 Mục tiêu: **quyền** (`BR-15`/`D-299`: `IssueAsync` phải `.Voucher.Publish`) · **hợp đồng route** (`D-307`/`D-308`:
> `/api/v1/voucher` là bề mặt công khai) · **tiền** (`BR-06`/`D-305`: `Σ DiscountAmount` = `OrderDiscountTotal`).

| # | Đột biến | Tệp:dòng | Test | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| NC-1 | Quyền: `OverrideLimitAsync` đổi `.Voucher.OverrideLimit` → `.Voucher.Publish` (hạ quyền ngoại lệ rủi ro tiền xuống quyền phát hành) | `src/eCommerce.HttpApi/Controllers/VoucherController.cs:81` | `VoucherV1SurfaceTests.D307a_map_method_sang_permission_dung` | (script ghi) | (script ghi) |
| NC-2 | Tiền: đảo so `Σ` (bỏ guard `BR-06`) trong `ThucHienCommitAsync` | `modules/Promotions/eCommerce.Promotions.Application/VoucherRedemptionService.cs` | `VoucherRedemptionCommitTests` | (script ghi) | (script ghi) |
| NC-3 | `D-309` **surrogate (tệp đích chưa commit tại thời điểm chạy)**: hạ trần lô mặc định `10_000` → `20` ⇒ 3 ca `D309` đỏ | `modules/Promotions/eCommerce.Promotions.Domain/PromotionSettings.cs:70` | `VoucherMaxBatchSizeTests` (`~D309`) | (script ghi) | (script ghi) |
| NC-3b | `D-309` **trực tiếp trên đúng khối pre-check**: đổi biên `<` → `<=` (`if (codespace <= input.Quantity)`) ⇒ `D309_codespace_bang_dung_Quantity__KHONG_nem` (biên `36^2 == 1296`) đỏ, 3 ca còn lại xanh | `modules/Promotions/eCommerce.Promotions.Application/VoucherAdminAppService.cs:145` | `VoucherMaxBatchSizeTests` (`~D309`) | (script ghi) | (script ghi) |

## 4. Cổng (đo trong phiên N103)

```powershell
dotnet build eCommerce.slnx --no-incremental     # 0 error / 9 warning (nền: Address/Sales, không phát sinh mới)
dotnet test test/eCommerce.Tests --no-build      # 2619/2619 pass · 0 fail (baseline 2593 ⇒ +26)
dotnet test test/eCommerce.IntegrationTests      # 162/162 pass · 0 fail (baseline 156 ⇒ +6)
pwsh -File tools/probe-voucher-wp25.ps1          # 6/6 PASS: 401×3 · 200×2 · 403×1 (§3.1)
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/PRO-001-testplan.md -Srs docs/features/promotions/pro-001-voucher-coupon.md   # exit 0
pwsh -File tools/kiem-tat-ca.ps1                 # exit 0
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error · 9 warning** (không warning mới — cả 9 ở `Address`/`Sales`, nền phiên).
- `dotnet test test/eCommerce.Tests --no-build` → **2619/2619 pass**, exit **0**.
- `dotnet test test/eCommerce.IntegrationTests` → **162/162 pass**, exit **0**.
- `check-agent-artifact -Kind testplan` → **exit 0**.
- `tools/probe-voucher-wp25.ps1` → **6/6 PASS** (`T01`–`T03` `401` · `T04a` `200` · `T04b` `403` · `T05` `200`); dọn user probe = 0 (§3.1).
- `git status --porcelain -- src/ modules/ shared/` rỗng sau toàn bộ negative control (code sản phẩm khôi phục nguyên trạng).

### 3.1 Probe HTTP THẬT — `tools/probe-voucher-wp25.ps1`

> Host thật (`eCommerce.HttpApi.Host`, cổng 54275) + PostgreSQL thật. Script in `HTTP STATUS` **trước** body,
> **không** in token. User/role probe tạo bằng SQL (Host **không** expose `/api/identity/**`), cấp
> `Promotions.View` qua `PUT /api/v1/settings/permissions`, dọn sạch trong `finally`, **không** tạo dữ liệu voucher.

| Ca | Kỳ vọng | Đo được |
|---|---|---|
| T01 ẩn danh `GET /api/v1/voucher` | `401` | **HTTP 401** ✅ |
| T02 ẩn danh `POST /api/v1/voucher/generate` | `401` | **HTTP 401** ✅ |
| T03 ẩn danh `GET /api/v1/voucher/redemptions` | `401` | **HTTP 401** ✅ |
| T04a user chỉ `Promotions.View` `GET /api/v1/voucher` | `200` | **HTTP 200** (`items:[]`) ✅ |
| T04b cùng user `POST /api/v1/voucher/generate` | `403` | **HTTP 403** ✅ |
| T05 admin `GET /api/v1/voucher` | `200` | **HTTP 200** (`items:[]`) ✅ |

## 5. Chưa phủ / nợ

1. **`F-1` — worker chỉ phát metric khi `daNha > 0`, arch ghi "0 dòng ⇒ bỏ qua + log `Debug` + metric".**
   - Arch (`PRO-001-arch.md:606-608`, `:627-629`): `UPDATE` guard `Status = 0` — *"**0 dòng ⇒ bỏ qua** + log `Debug` + metric (không phải lỗi)"*.
   - Code (`modules/Promotions/eCommerce.Promotions.Application/Workers/VoucherExpiryWorker.cs:232-237`): `if (soDong == 0) { Logger.LogDebug(...); continue; }` — **không** phát metric trong nhánh 0 dòng; metric chỉ phát khi `daNha > 0` (`:180-186`).
   - Test khoá **hiện trạng code**: `VoucherExpiryWorkerTests.D306_worker_PHAT_2_counter_dung_nhan` khẳng định `if (daNha > 0)`.
   - ⇒ **Nợ chờ `rev`/`ba` chốt** (sửa câu chữ arch, hoặc `be` thêm counter "bỏ qua" — đây là quyết định sản phẩm). **QA không sửa code**.
2. **Consumer `OrderCancelledEto`/`OrderReturnedEto` chưa tồn tại** (`SAL-004`/`SAL-006`) ⇒ `BR-25`/`TC-16`/`TC-62`/`TC-67` mới phủ phần **máy trạng thái**; nhánh consumer + ETO ra thuộc nợ `SAL-004` (`D-306` ③, `D-148`).
3. **`BR-12`/`BR-17` (`0110`) và `BR-24` (`0118`) tại `ReserveAsync` — chưa có test service.** Các guard nằm trong `VoucherRedemptionService.ReserveAsync` (`:113-123`), cần harness service (repo/engine/guard giả) — chưa dựng ở `W25-2`. Nợ chủ **`qa`**, hạn `W25-5`; probe không đo được vì `ReserveAsync` là hợp đồng nội bộ (`ADR-13`).
4. **`BR-05` — map `0115`**: `ThucHienCommitAsync` bắt unique `(TenantId, OrderId)` và ném `0115` (`VoucherRedemptionService.cs:476-480`); hàng rào DB đã phủ, nhưng chưa có ca service ép `23505` để thấy `0115`. Nợ chủ `qa`, `W25-5`.
5. **`BR-08` — chưa có test quét tĩnh `double`/`float` riêng cho `Promotions`** (code hiện 0 `double`/`float`); `NFR-08` phủ ở hành vi `Money`/`TinhTienDon`. Nợ nhỏ.
6. **`BR-20` — `TC-27` "ReasonCode nêu số còn thiếu"** chưa có ca; hiện chỉ phủ "MinOrderAmount trên tiền hàng chưa thuế/ship" qua `Rule_MinOrderAmount`. Nợ `PRO-002` (engine trả `ReasonCode`).
7. **`BR-11`/`BR-13`/`BR-22` — ĐÃ PHỦ ở `Promotions/VoucherDomainRulesTests.cs`** (W25-4 vòng bổ sung): `BR11_so_luot_append_only__cot_bat_bien_khong_setter_cong_khai__khong_co_duong_xoa` · `BR13_KHONG_autoSave_true_trong_duong_ghi_voucher__ngoai_no_cu_PromotionAppService` · `BR22_log_KHONG_mang_ma_voucher_gan_khach__va_KHONG_seed_voucher_nghiep_vu`. Các nợ còn lại **giữ nguyên**: DB không có trigger append-only (`BR-11`, §8.1 #11); "seed 2 lần vẫn 0 dòng mới" chưa đo trên DB (`BR-22`); quét `autoSave` là **tĩnh** (không chứng minh UoW runtime).
8. **Migration `Add_Voucher_Status_Check` (`20261127153549`) CHƯA ÁP DB dev** (`__EFMigrationsHistory` chưa có — `handover-n102` §2). Migration chỉ thêm **2 `CHECK`** (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`); Host **vẫn khởi động** (không bắt buộc để boot). **Không** tự áp migration (việc của người). `Assets/schema.sql` của integration test **chưa** chứa 2 `CHECK` này ⇒ `BR-09` phủ ở **enum + quét mã nguồn**, **không** phủ `23514` cho `Status` sai — nợ chờ áp migration + tái sinh `schema.sql`.
9. **Integration không biểu diễn được** (đã ghi rõ, không giả vờ phủ): job `voucher-expiry` chạy **thật** trên 2 instance cần Redis + 2 process (ngoài phạm vi `W25-4`); `TC-05`/`TC-18`/`TC-60` (nhả TTL end-to-end) đã phủ **ngữ nghĩa 0 dòng** ở `PostgreSqlVoucherWp25Tests.D306_*` + unit `VoucherExpiryWorkerTests`; cơ chế `40P01` thử lại phủ ở unit (`D306_luot_Reserved_het_han__duoc_chon_va_thu_lai_40P01_dung_3_lan`).
10. **Probe `403` phụ thuộc `PUT /api/v1/settings/permissions`**: nếu đầu cuối này không trả `200` (admin thiếu `Settings.Manage`), T04a/T04b ghi `CHUA_DO` — **không hạ kỳ vọng** (khuôn `D-249` ① / PRO-002 §5 #7).

## Negative control
| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |
|---|---|---|---|---|---|
| 2026-11-28 | NC-1 | src/eCommerce.HttpApi/Controllers/VoucherController.cs:81 | quyen: ha OverrideLimit xuong VoucherPublish | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~VoucherV1SurfaceTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-2 | modules/Promotions/eCommerce.Promotions.Application/VoucherRedemptionService.cs:427 | tien: dao guard BR-06 nhanh replay (!= 0 -> == 0) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~VoucherRedemptionCommitTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-3 | modules/Promotions/eCommerce.Promotions.Domain/PromotionSettings.cs:70 | D-309 surrogate: ha tran lo mac dinh 10000 -> 20 (tep dich that VoucherAdminAppService.cs CHUA COMMIT nen dung tep da commit; ca D309 do) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~D309 | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-28 | NC-3b | modules/Promotions/eCommerce.Promotions.Application/VoucherAdminAppService.cs:145 | D-309: doi bien < thanh <= -> ca codespace_bang_dung_Quantity phai do | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~VoucherMaxBatchSizeTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

11. **NỢ MỚI PHÁT HIỆN khi phủ `BR-13` (không thuộc `PRO-001`)**: `modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs` còn **7** chỗ `autoSave: true` (`:174`, `:234`, `:239`, `:242`, `:276`, `:330`, `:476` — code `PRO-002`/W24) và `modules/Promotions/eCommerce.Promotions.Domain/Seed/PromotionRuleCostSeedContributor.cs` **3** chỗ (`:81`, `:88`, `:98`). Trái `BR-13`/`NFR-09`. Test `BR13_…` giữ **allowlist** đúng `PromotionAppService.cs` để lỗi **mới** vẫn ĐỎ; sửa nợ cũ thuộc **`rev`/`PRO-002`** (không sửa trong `W25-4`).
12. **`BR-22` (b) — seed idempotent mới phủ ở mức TĨNH**: đã khẳng định seeder module không chứa `VoucherCode` và `VoucherCode.TaoMoi` chỉ ở `VoucherAdminAppService`; **chưa** chạy seed 2 lần trên DB để đếm 0 dòng mới (không có seeder voucher để chạy). Nợ chủ `ops`, hạn `W26`.
13. **`D-309` — NC-3b ĐÃ CHẠY đỏ → xanh với đột biến biên** (`<` → `<=`) trên đúng khối pre-check: `D309_codespace_bang_dung_Quantity__KHONG_nem` đỏ (exit 1) → khôi phục exit 0; `git status --porcelain -- modules/ src/ shared/` rỗng sau lượt. Một **lượt trước** dùng đột biến `if (codespace < 0)` (gỡ hẳn guard) đã **treo > 600 s** và bị harness kill trước bước khôi phục (ORCH đã `git restore`; không mất gì) ⇒ **nợ thật (chủ `qa`/`be`, trước `WEB-001`)**: ca `D309_codespace_thieu__nem_0107_khong_treo` chỉ chặn treo ở **mức test** (`Task.WaitAsync(5 s)`), **không** chặn ở **mức tiến trình** khi guard bị gỡ. Muốn NC "treo-thật" an toàn phải chạy ca trong process riêng hoặc `be` thêm `CancellationToken`/trần vòng lặp; **không** nới kỳ vọng, không sửa ở `W25-4`. NC-3 surrogate (hạ trần lô trên `PromotionSettings.cs` khi tệp đích còn chưa commit) **giữ làm bằng chứng phụ**.
