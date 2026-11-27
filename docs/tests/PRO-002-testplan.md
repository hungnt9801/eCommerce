# Testplan `PRO-002` — 27/11/2026

- SRS: `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (**v0.18**)
- Kiến trúc bổ trợ: `docs/features/promotions/PRO-002-arch.md`
- Nhánh: `feat/gd3-wp24-dieu-kien-km` · Phiên `N96` (vòng 2) → **`N97` (F4 QA bổ sung sau BE vá P1/P2 REV vòng 1)** · Hàng `W24-4`
- Quyết định nguồn: `D-285`…`D-290` (bề mặt `/api/v1/promotion` 7 route, `Limits`, `rule-catalog`, nhãn lấy từ code) ·
  **`D-291`** (phạm vi vá REV vòng 1: guard trạng thái voucher, `Subtotal`/`Currency` từ pipeline giá, `VoucherOutcome`,
  `?sort=` whitelist, calculator `default` NÉM, probe 200/401/403 bắt buộc THỬ) · **`D-292`** (cây duyệt **trước**, hết lượt sau —
  giữ `D-161`/`D-162`) · `D-283` (metric chỉ 5/9 có điểm phát) · `D-284` (guard chống trùng payload **chưa** áp v1).
- Phạm vi WP-24: engine cây AND/OR + 3 tầng guard · 7 bảng `Pro*` + 9 unique `NULLS NOT DISTINCT` · app service 10 method ·
  controller `/api/v1/promotion` 7 route · `rule-catalog` + `Limits` trên `PromotionDetailDto`.

## 1. Phủ BR

> Nguồn test: **43** ca cũ (`HaTang`/`PromotionDomainTests`/`PromotionEngineTests`/`PromotionSurfaceTests`/`PromotionCheckoutGuardTests`)
> + **13** ca unit mới `W24-4` (`PromotionV1SurfaceTests`/`PromotionRuleCatalogTests`/`PromotionGetLimitsTests`) + **17** ca
> integration mới (`PostgreSqlPromotionSchemaTests`). Mã BR in đậm literal `BR-01`…`BR-20` (SRS §1).

| BR | Nội dung (SRS §1) | Test (tệp · tên method) | Tầng | Trạng thái |
|---|---|---|---|---|
| **BR-01** | Cây chỉ 2 loại node (nhóm/lá); cấm node lai ⇒ `0004` | `Promotions/PromotionDomainTests.cs` · `Validator_hai_goc_hoac_node_lai__0004`; DB CHECK `CK_ProPromotionConditions_NodeLai` (schema × `RangBuocChuaAnhXa.txt`) | Unit + DB(schema) | ✅ |
| **BR-02** | Mọi node thuộc đúng 1 promotion; `ParentId` cùng promotion; cấm `ParentId = Id`; cấm chu trình ⇒ `0003` | `Promotions/PromotionDomainTests.cs` · `Validator_chu_trinh__0003`; DB CHECK `CK_ProPromotionConditions_ParentKhacId` (schema) | Unit + DB(schema) | ✅ |
| **BR-03** | Đúng **một** gốc / cây ⇒ `0004`; hàng rào DB unique một gốc ⇒ `23505` | `Promotions/PromotionDomainTests.cs` · `Validator_hai_goc_hoac_node_lai__0004` · `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC33_hai_goc_cung_PromotionId_bi_chan_23505__boi_index_mot_goc` | Unit + Integration | ✅ |
| **BR-04** | `NodeCount ≤ MaxNodes` (64) ⇒ vượt ⇒ `0001` (lưu **và** chạy) | `Promotions/PromotionDomainTests.cs` · `Validator_vuot_tran_node__0001` | Unit | ✅ |
| **BR-05** | `Depth ≤ MaxDepth` (8) + `LeafCount ≤ MaxLeaves` (32) ⇒ `0001`; `MaxDepth` kẹp `1…8` | `Promotions/PromotionDomainTests.cs` · `Validator_do_sau_9__0001` · `Settings_mac_dinh_va_kep_MaxDepth` | Unit | ✅ |
| **BR-06** | `Σ DeclaredCostFactor ≤ MaxDeclaredCombinations` (1024) ⇒ `0002` | `Promotions/PromotionDomainTests.cs` · `Validator_vuot_tran_to_hop__0002_khong_phai_0001` | Unit | ✅ |
| **BR-07** | Vượt ngân sách chạy: mã ⇒ `0005`; tự động ⇒ bỏ qua + log/metric (không im lặng) | `Promotions/PromotionEngineTests.cs` · `Vuot_ngan_sach__nguon_MA__nem_0005` · `Vuot_ngan_sach__nguon_TU_DONG__bo_candidate_khong_nem` | Unit | ✅ |
| **BR-08** | Rule lá THUẦN, không I/O; lá khách + vãng lai ⇒ `false` không ném | `Promotions/PromotionDomainTests.cs` · `Rule_MinOrderAmount` · `Rule_ChannelScope` · `Rule_khach__CustomerId_null_thi_false` · `Promotions/PromotionEngineTests.cs` · `Tu_dong_tran_theo_khach_ma_vang_lai__CustomerRequired` | Unit | ✅ |
| **BR-09** | Kết quả XÁC ĐỊNH (cùng input ⇒ cùng kết quả); duyệt `ORDER BY SortOrder, Id` | `Promotions/PromotionEngineTests.cs` · `Tat_dinh__hai_lan_cung_ket_qua` · `Promotions/PromotionDomainTests.cs` · `Registry_tat_dinh_va_chan_trung` | Unit | ✅ |
| **BR-10** | Áp khuyến mãi qua **một UoW**: `Redemption` + đơn + outbox cùng transaction | — (xem §5: đường áp vào đơn `Sales` chưa có — nợ `SAL-002`) | — | ❌ |
| **BR-11** | `DiscountTotal ≤ Subtotal` **không kẹp** (mã ⇒ `0009`, tự động ⇒ bỏ qua); `%`/cộng dồn; DB không chặn cộng dồn hợp lệ | `Promotions/PromotionEngineTests.cs` · `Tran_khong_kep__ma_vuot_Subtotal__nem_0009` · `Tu_dong_vuot_tran__bo_candidate_chu_khong_nem` · `Ma_thay_tu_dong_khong_ghep_duoc__SupersededByVoucher` · `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC43i_mot_don_1_voucher_va_2_tu_dong_KHAC_promotion__cong_don_KHONG_bi_chan` | Unit + Integration | ✅ |
| **BR-12** | Tiền là `Money` (`decimal`), cấm `double`/`float`, cấm so `==`, khác `Currency` ⇒ `Money:0001` | `Promotions/PromotionEngineTests.cs` · `Tran_khong_kep__ma_vuot_Subtotal__nem_0009` (tính tiền `FixedAmount`/`Percentage` qua `Money` — xem §5: chưa có test quét tĩnh `double`/`float` riêng cho `Promotions`) | Unit | ✅ (hành vi) |
| **BR-13** | `[Authorize]` cấp **CLASS** + permission chỉ dùng hằng số | `Promotions/PromotionSurfaceTests.cs` · `AppService_co_Authorize_cap_CLASS_va_method_hep` · `Quyen_Promotions_khai_CA_HAI_NOI` · `Khong_co_AllowAnonymous_trong_Promotions` · `Promotions/PromotionV1SurfaceTests.cs` · `TC70a_Authorize_cap_CLASS_la_Promotions_Default` · `TC70a_map_method_sang_permission_dung` | Unit (reflection) | ✅ |
| **BR-14** | Idempotency mọi biên vào **có ghi** qua `IIdempotencyGuard` + unique index; biên chỉ đọc `evaluate` không guard (`D-159`/`D-284`) | `IntegrationTests/PostgreSqlSchemaTests.cs` · `F1_du_73_index_deu_dung_NULLS_NOT_DISTINCT` (gồm `IX_ProPromotionRedemptions_TenantId_IdempotencyKey`) · `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC33_hai_luot_RESERVED_OrderId_NULL_cung_ma_KHONG_23505` · `Promotions/PromotionV1SurfaceTests.cs` · `TC70a_dung_7_route__va_0_route_ngoai_pham_vi` (0 route `evaluate`) | Unit + Integration | ✅ |
| **BR-15** | Mọi chuyển trạng thái sai ⇒ `0006` (cấm no-op im lặng) | `Promotions/PromotionDomainTests.cs` · `Approve_chi_tu_Draft__sai_nem_0006` · `Kich_hoat_tay_truoc_StartsAtUtc__nem_0006` · `Expired_va_Cancelled_la_hap_thu__moi_chuyen_nem_0006` · `Pause_chi_tu_Active__Resume_chi_tu_Paused` · `TangTreeVersion_chi_o_Draft__khac_nem_0006` · `Job_het_han__chi_khi_qua_EndsAtUtc` · `Job_kich_hoat__chua_toi_gio_thi_giu_nguyen_khong_loi` | Unit | ✅ |
| **BR-16** | `ProPromotionRedemptions` chỉ THÊM; cấm ghi 1 dòng cho mỗi lần bị từ chối | `Promotions/PromotionEngineTests.cs` · `Khong_co_candidate__NoCandidate` (chỉ `Breakdown`, engine không có đường ghi) · `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC43a_mot_don_mot_voucher__ghi_hai_luot_cung_don_voucher_bi_chan_23505` (một lượt/đơn) | Unit + Integration | ✅ (xem §5: chưa có test reflection "không setter công khai") |
| **BR-17** | Mọi entity `IMultiTenant`; unique có `TenantId` **PHẢI** `NULLS NOT DISTINCT` | `IntegrationTests/PostgreSqlSchemaTests.cs` · `F1_du_73_index_deu_dung_NULLS_NOT_DISTINCT` (114 index; +9 `Pro*`) · `IntegrationTests/PostgreSqlPromotionSchemaTests.cs` · `TC33_bon_partial_index_redemptions_va_unique_mot_goc_conditions_dung_filter_va_NND` | Integration | ✅ |
| **BR-18** | Mọi entity cần `IRepository<T>` có `DbSet` trên `eCommerceDbContext` | `Promotions/PromotionSurfaceTests.cs` · `DbSet_du_7_entity_Pro` | Unit | ✅ |
| **BR-19** | Điều kiện COD/giao hàng **chỉ** đọc dữ liệu đã đối soát ⇒ sai `0008` | — (xem §5: chưa có rule COD trong v1 — nợ `SHP-003`/`REC-003`) | — | ❌ |
| **BR-20** | Metric mới phải vào `eCommerceMetricNames` + `XC-005` §5.1 + test trong cùng PR | `Observability/MetricsTests.cs` · `Observability/MetricEmitterTests.cs` (mọi tên trong `eCommerceMetricNames.All` có ký hiệu / `ChuaCoNoiPhat`). **Chỉ 5/9** metric promotion có điểm phát (`D-283`) | Unit (kiến trúc) | ✅ (một phần — `D-283`) |

- Tổng BR: **20** · đã phủ: **18** · chưa phủ: **2** (`BR-10`, `BR-19`) — lý do ở §5.

### 1.1 TC-70 / TC-71 (`D-285`/`D-289`/`D-290`)

| TC | Nội dung | Test | Kết quả |
|---|---|---|---|
| **TC-70** | `/api/v1/promotion` **đúng 7 route**, 0 op ngoài phạm vi (`evaluate`/`validate-voucher`/`import-rule-costs`/`redemptions`); `[Authorize]` class `Promotions.Default`; map method→permission; `rule-catalog` literal không bị `{id:guid}` nuốt; allowlist 10 method | `Promotions/PromotionV1SurfaceTests.cs` · `TC70a_route_template_cap_class_dung_api_v1_promotion` · `TC70a_Authorize_cap_CLASS_la_Promotions_Default` · `TC70a_dung_7_route__va_0_route_ngoai_pham_vi` · `TC70a_map_method_sang_permission_dung` · `TC70d_rule_catalog_la_literal_khong_bi_id_guid_nuot` · `TC70b_IPromotionAppService_van_dung_10_method__khong_UpdateAsync` | ✅ |
| **TC-71** | `rule-catalog` khi bảng chi phí **rỗng** ⇒ đủ `SystemNames` mặc định (`1/false/IsSeeded=false`), không ném `0010`, 0 tên bị bỏ; seed đủ ⇒ `IsSeeded` + giá trị theo dòng (kể cả `RetiredAtUtc`); `Label` từ **code**; mọi tên `Label` khác rỗng & khác `SystemName`, 4 nhãn v1 đúng §10.1; tên ngoài bảng ⇒ `Label = SystemName` | `Promotions/PromotionRuleCatalogTests.cs` · `TC71i_bang_chi_phi_RONG__tra_du_SystemNames_mac_dinh_IsSeeded_false_khong_nem_0010` · `TC71ii_seed_du__IsSeeded_true_va_gia_tri_theo_dong_ke_ca_RetiredAtUtc` · `TC71iii_Label_lay_tu_bang_CODE_khong_doc_DB` · `TC70d_TC71v_moi_SystemName_co_Label_khac_rong_va_khac_SystemName__4_nhan_v1_dung` · `TC71v_ten_ngoai_bang__Label_bang_dung_SystemName` | ✅ |

### 1.2 `D-288` — `Limits` trên `PromotionDetailDto`

| Hạng mục | Test | Kết quả |
|---|---|---|
| Setting rỗng ⇒ `MaxNodes=64, MaxDepth=8, MaxLeaves=32, MaxDeclaredCombinations=1024` | `Promotions/PromotionGetLimitsTests.cs` · `D288_setting_rong__Limits_bang_mac_dinh_64_8_32_1024` | ✅ |
| `Promotions:MaxNodes="10"`, `Promotions:MaxDepth="5"` ⇒ phản ánh đúng (10/5), trần còn lại mặc định | `Promotions/PromotionGetLimitsTests.cs` · `D288_setting_MaxNodes_10_MaxDepth_5__Limits_phan_anh_dung_con_lai_mac_dinh` | ✅ |

### 1.3 Vá REV vòng 1 — `TC-72`…`TC-76` (`D-291`) + `D-292`

| TC / quyết định | Nội dung | Test (tệp · tên method) | Tầng | Kết quả |
|---|---|---|---|---|
| **TC-72** (`D-291` ① P1 #1) | Voucher của promotion `Paused`/`Cancelled`/`Draft`/`Expired` **hoặc** voucher `Revoked` ⇒ `Applied=false` + 0 đồng, `VoucherOutcome.RejectionReason=NoCandidate`; guard chạy **trước** validate cây (cây lỗi cũng không ném `0004`) | `Promotions/PromotionVoucherGuardTests.cs` · `TC72_voucher_cua_promotion_khong_Active__khong_ap_va_0_dong` (Theory 4 trạng thái) · `TC72_voucher_Revoked_cua_promotion_Active__khong_ap_va_0_dong` · `TC72_guard_trang_thai_chay_TRUOC_validate_cay__cay_hong_cung_khong_nem_0004` | Unit (domain/engine) | ✅ |
| **TC-73** (`D-291` ② P2 #2) | `CheckoutAppService` truyền `Subtotal` = `GioTinhTienDto.Subtotal` (pipeline giá, kênh có giá < `ListPrice`) + `Currency` = `CurrencyCode`; lượt dò `KhongGhiSoGiaiTrinh=true`, lượt chính thức `false` | `Promotions/PromotionCheckoutSubtotalTests.cs` · `TC73_Subtotal_va_Currency_lay_tu_pipeline_gia__khong_Sum_ListPrice` · `TC73_luot_do_dung_co_KhongGhiSoGiaiTrinh__luot_chinh_thuc_moi_ghi` | Unit (application `Sales`) | ✅ |
| **TC-74** (`D-291` ② P2 #3/#4) | `ValidateVoucherAsync` chỉ đọc `VoucherOutcome`: mã không tồn tại + auto khớp ⇒ `IsValid=false`/`NoCandidate`; voucher được nhận ⇒ `IsValid=true` + `PromotionId`/`DiscountTotal` **của voucher**; voucher bị từ chối nhưng auto áp ⇒ **lý do vẫn hiện**; hết lượt mức promotion ⇒ ném `0119` | `Promotions/PromotionVoucherValidationTests.cs` · `TC74i_ma_khong_ton_tai_va_co_auto_khop__IsValid_false` · `TC74ii_voucher_duoc_nhan__IsValid_true_va_PromotionId_cua_voucher` · `TC74_voucher_bi_tu_choi_nhung_auto_ap__ly_do_van_hien` · `TC74iii_voucher_het_luot_muc_promotion__nem_0119` | Unit (application) | ✅ |
| **TC-75** (`D-291` ③ P3 #6) | `?sort=Code` / `Priority desc` đổi thứ tự + tie-break `Id`; trường lạ / không truyền ⇒ `CreationTime desc` + tie-break `Id`, không ném | `Promotions/PromotionVoucherValidationTests.cs` · `TC75i_sort_Code__doi_thu_tu` · `TC75ii_sort_Priority_desc__doi_thu_tu` · `TC75iii_truong_la__mac_dinh_CreationTime_desc_tie_break_Id` · `TC75iv_khong_truyen_sort__mac_dinh_CreationTime_desc_tie_break_Id` · `TC75_tie_break_Id_khi_cung_Priority` | Unit (application) | ✅ |
| **TC-76** (`D-291` ③ P3 #8) | `PromotionDiscountCalculator` nhánh `default` **NÉM** lỗi cấu hình (không `Money.Zero`); `Percentage`/`FixedAmount` vẫn đúng (đối chứng) | `Promotions/PromotionDiscountCalculatorTests.cs` · `TC76_kieu_giam_gia_la__nem_khong_tra_Money_Zero` · `TC76_FreeShipping_bi_chan_o_v1__nem_khong_im_lang_0_dong` · `TC76_hai_kieu_hop_le_van_tinh_dung` | Unit (domain) | ✅ |
| **`D-292` (i)+(v)** | Auto + `CustomerId=NULL` + `MaxUsagePerCustomer=1` + cây `false` ⇒ `ConditionFalse`, **không** `CustomerRequired`; `promotion_usage_limit_hit_total` **không** tăng | `Promotions/PromotionVoucherGuardTests.cs` · `D292i_tu_dong_cay_FALSE_va_vang_lai_tran_khach__ConditionFalse_khong_CustomerRequired` · `D292v_auto_cay_TRUE_het_luot__counter_tang` (đối chứng counter **có** tăng khi cây `true`) | Unit (domain/engine) | ✅ |
| **`D-292` (ii)** | Voucher cây `false` + hết lượt ⇒ `ConditionFalse`, **không ném** | `Promotions/PromotionVoucherGuardTests.cs` · `D292ii_voucher_cay_FALSE_cong_het_luot__ConditionFalse_khong_nem` | Unit (domain/engine) | ✅ |
| **`D-292` (iii)** | Voucher cây `true` + hết lượt ⇒ **NÉM** `0007`/`0119`/`0104` | `Promotions/PromotionVoucherGuardTests.cs` · `D292iii_voucher_cay_TRUE_het_luot_MUC_MA__nem_0007` · `…_MUC_PROMOTION__nem_0119` · `…_MUC_KHACH__nem_0104` | Unit (domain/engine) | ✅ |
| **`D-292` (iv)** | Giữ xanh ca cây `true`: `Tu_dong_tran_theo_khach_ma_vang_lai__CustomerRequired` | `Promotions/PromotionEngineTests.cs` · `Tu_dong_tran_theo_khach_ma_vang_lai__CustomerRequired` (không sửa) | Unit (domain/engine) | ✅ |
| **`D-292` (vi)** | Comment integration `TC-43 (ix)` chỉ đúng nhánh cây `true`; ca cây `false` ⇒ `ConditionFalse` | `eCommerce.IntegrationTests/PostgreSqlPromotionSchemaTests.cs` (comment đầu tệp) | — (chú thích) | ✅ |

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (đầu vòng 2) | Sau (N97) | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` (unit) | **2432** | **2458** | +26 ca N97: `PromotionVoucherGuardTests` (12) + `PromotionVoucherValidationTests` (9) + `PromotionDiscountCalculatorTests` (3) + `PromotionCheckoutSubtotalTests` (2). 0 fail. Số ca **chỉ tăng**. |
| `test/eCommerce.IntegrationTests` (PostgreSQL thật) | **156** | **156** | Chỉ sửa **comment** `PostgreSqlPromotionSchemaTests.cs` (`D-292`); 0 ca mới. 0 fail. |
| Domain invariant / Unit (`Promotions`) | 43 | 43 | `HaTang`/`PromotionDomainTests`/`PromotionEngineTests`/`PromotionSurfaceTests`/`PromotionCheckoutGuardTests` (giữ nguyên). |
| Application / API surface (`Promotions`) — unit mới | 13 | 13 | `PromotionV1SurfaceTests` (6) + `PromotionRuleCatalogTests` (5) + `PromotionGetLimitsTests` (2). |
| API `/api/v1` (200 + 401/403) | — | **401 + 200 đo thật; 403 CHƯA ĐO** | `tools/probe-promotion-wp24.ps1` — ẩn danh ⇒ `401` (3 route), admin `GET rule-catalog`/`POST /` ⇒ `200`; **403 chưa đo** (Host không expose `/api/identity/**` ⇒ 404) — xem §3.1. 7 route còn lại kiểm **tĩnh** (`TC-70a`). |
| UI Vitest / Playwright | **60** | **60** | `W24-3` đã có (list/new/tree builder) — hàng trước, **không** đổi. |
| OpenAPI drift | — | **N/A** | N97 không đổi DTO/route (additive `VoucherOutcome`/`KhongGhiSoGiaiTrinh` đã ở vòng vá BE, không đổi `schema.d.ts`). |

## Negative control (bắt buộc) — bằng chứng thật (vòng 2, sau commit `33559e5`)

> Chạy `pwsh -File tools/agents/negative-control.ps1` cho từng ca: **đột biến thật → test phải ĐỎ (exit ≠ 0) →
> khôi phục byte gốc → test phải XANH (exit 0)**. `TestCommand` **có build lại** (không `--no-build`) vì script sửa mã
> nguồn. Bảng dưới do script tự chèn dòng bằng chứng. Xác nhận sau mỗi ca: `git status --porcelain -- modules/ src/` rỗng.
>
> 🔒 Mục tiêu: `D-289` (rule-catalog duyệt registry, không duyệt bảng DB) · `D-288` (`Limits` phản ánh setting) ·
> `BR-13` (quyền tách `Approve` ≠ `Manage`) · `BR-11` (trần `DiscountTotal ≤ Subtotal`). Mỗi luật thuộc nhóm
> **tiền / trạng thái / quyền** nên bắt buộc có bằng chứng đỏ→xanh (`testing-negative-control` §2).

| Ngày | Mã | Tệp:dong | Đột biến | Lệnh test | Bằng chứng |
|---|---|---|---|---|---|
| 2026-11-27 | NC-N96-1 | modules/Promotions/eCommerce.Promotions.Application/PromotionRuleCatalogAppService.cs:52 | rule-catalog: duyet registry (ten) -> duyet bang DB (dong.Keys) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~PromotionRuleCatalogTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N96-2 | modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:71 | Limits: MaxNodes = limits.MaxNodes (doc setting) -> MaxNodes = 64 (hang so) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~PromotionGetLimitsTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N96-3 | src/eCommerce.HttpApi/Controllers/PromotionsController.cs:98 | quyen: Approve route bi ha xuong Manage | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~PromotionV1SurfaceTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N96-4 | modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionEngine.cs:186 | tien: tran DiscountTotal <= Subtotal bi vo hieu (nguong 1e9) | dotnet test test/eCommerce.Tests --filter "FullyQualifiedName~PromotionEngineTests" | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-1 | modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionEngine.cs:296 | go guard trang thai voucher (TC-72): bo dieu kien Status==Active/!Revoked | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionVoucherGuardTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-2 | modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionEngine.cs:362 | D-292: auto cay false khong short-circuit => roi vao CustomerRequired (i) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionVoucherGuardTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-3 | modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:374 | TC-74: IsValid lay Applied cua ca don thay vi VoucherOutcome (auto ap => ma rac thanh hop le) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionVoucherValidationTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-4 | modules/Promotions/eCommerce.Promotions.Application/Engine/PromotionDiscountCalculator.cs:45 | TC-76: nhanh default tra Money.Zero im lang thay vi NEM (P3 #8) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionDiscountCalculatorTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-5 | modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:170 | TC-73: Subtotal = Sum(ListPrice x Qty) thay vi GioTinhTienDto.Subtotal (P2 #2) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionCheckoutSubtotalTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
| 2026-11-27 | NC-N97-6 | modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:126 | TC-75: sort=Code dao thanh descending (whitelist sai chieu) | dotnet test test/eCommerce.Tests --filter FullyQualifiedName~PromotionVoucherValidationTests | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

## 3.1 Probe HTTP THẬT — `tools/probe-promotion-wp24.ps1` (N97, Host `54275`)

> Chạy Host thật (`eCommerce.HttpApi.Host`, build `--no-incremental`) + PostgreSQL thật; script in `HTTP STATUS`
> **trước** body (`README-probe.md` B1). Mã probe `PROBE-PROMO-<random>`; dọn `ProPromotions` sau lượt (đo lại = 0).

| Ca | Kỳ vọng | Đo được | Ghi chú |
|---|---|---|---|
| T01 `GET /api/v1/promotion/rule-catalog` ẩn danh | `401` | **HTTP 401** | ✅ |
| T02 `POST /api/v1/promotion` ẩn danh | `401` | **HTTP 401** | ✅ |
| T03 `POST /api/v1/promotion/{id}/approve` ẩn danh | `401` | **HTTP 401** | ✅ |
| T04–T08 user **thiếu quyền** (View/Manage/Approve, tách quyền) | `403` | **CHƯA ĐO** | ⛔ Host trả **`404`** cho `/api/identity/roles` — **không expose** Identity HTTP API ⇒ không tạo được user thường qua HTTP ⇒ **không dựng được ca 403**. **KHÔNG hạ kỳ vọng 403**; nợ chủ `qa`, hạn `W24-5`. |
| T09 admin `GET /rule-catalog` | `200` | **HTTP 200** | ✅ (body `[]` — xem ghi chú dưới) |
| T10 admin `POST /api/v1/promotion` | `200/201` | **HTTP 200** | ✅ tạo `Draft` |
| T10b/ T11 `save-tree` 1 lá → `approve` | `200` | **HTTP 400** `0004` | ⛔ Bảng `ProPromotionRuleCosts` **rỗng** trên DB dev (seeder chưa chạy) ⇒ mọi rule ⇒ `0004` "Rule chua tung dang ky" ⇒ **không dựng được cây hợp lệ** ⇒ chưa đo được `approve` `200` / `0006`→`409`. |
| T12 approve lần hai | `409` + `0006` | **CHƯA ĐO** | phụ thuộc T11. |
| T13 `save-tree` 65 node | `400` + `0001` | **HTTP 400 + `eCommerce:Promotion:0001`** (data `NodeCount=65, MaxNodes=64`) | ✅ map lỗi `0001`→`400` đúng |
| Dọn dẹp | `ProPromotions` mã probe = 0 | **còn lại = 0** | ✅ |

- **Tổng đo được:** `401` (3 route) · `200` (admin `GET rule-catalog`, `POST /`) · `400` map `0001`/`0004`.
  **Chưa đo được:** `403` (thiếu Identity HTTP API) · `approve` `200` / `0006`→`409` (`ProPromotionRuleCosts` rỗng).
- ⚠️ **Quan sát cần REV/ORCH xác nhận (không sửa từ `qa`):** `GET /api/v1/promotion/rule-catalog` trả **`[]`** trên
  Host thật trong khi `PromotionRuleCatalogAppService` duyệt `PromotionRuleRegistry.SystemNames` và unit test `TC-71(i)`
  kỳ vọng **đủ 4 tên mặc định** khi bảng chi phí rỗng. Dấu hiệu: `IPromotionConditionRule` (4 rule `ISingletonDependency`)
  **không được DI nạp** trong Host runtime ⇒ registry rỗng. Ghi nhận, **không** kết luận thay REV.

## 4. Cổng (đo trong phiên N97)

```powershell
dotnet build eCommerce.slnx --no-incremental      # 0 error / 9 warning (baseline phiên)
dotnet test test/eCommerce.Tests --no-build       # 2458 pass · 0 fail
dotnet test test/eCommerce.IntegrationTests       # 156 pass · 0 fail (Testcontainers postgres:17-alpine)
pwsh -File tools/probe-promotion-wp24.ps1          # 401/200 đo thật; 403 + approve CHƯA ĐO (§3.1)
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/PRO-002-testplan.md -Srs docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md   # exit 0
pwsh -File tools/kiem-link-tai-lieu.ps1           # PASS
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error**, **9 warning** (không phát sinh warning mới — đều ở Address/Sales).
- `dotnet test test/eCommerce.Tests --no-build` → **2458/2458 pass**, exit **0** (baseline đầu phiên **2432** ⇒ **+26**, không giảm).
- `dotnet test test/eCommerce.IntegrationTests` → **156/156 pass**, exit **0**.
- `pwsh -File tools/probe-promotion-wp24.ps1` → `400/401/200` đo thật; `403` + `approve 200`/`0006` **CHƯA ĐO** (§3.1).
- `check-agent-artifact -Kind testplan` → **exit 0**.
- `pwsh -File tools/kiem-link-tai-lieu.ps1` → **PASS**.
- `git status --porcelain -- modules/ src/ shared/` rỗng sau toàn bộ negative control (code sản phẩm khôi phục nguyên trạng).

## 5. Chưa phủ / nợ

1. **`BR-10` (áp khuyến mãi qua một UoW — `Redemption` + đơn + outbox) — CHƯA PHỦ.** Đường áp vào **đơn thật** nằm ở `Sales` (`SAL-002`/`SAL-001`) — chưa có ở `WP-24`. Các tiểu-ca `TC-43` cần app service/UoW cũng thuộc nhóm này: rollback cả đơn khi `23505` (0 `Order`, 0 redemption, bộ đếm không đổi), `UsageSequence` 1→2 qua hai đơn, đua 2 đơn song song, `MaxUsagePerCustomer=1` bỏ qua candidate, vãng lai `CustomerId NULL`, `TC-43 (ix)` unit `CustomerRequired`. **Đã phủ** phần **hàng rào DB** (`TC-43 (i)`: cộng dồn hợp lệ 1 voucher + 2 tự động khác `PromotionId`; `(ii)/(iii)`: 3 unique chặn trùng ⇒ `23505`; ca hợp lệ).
2. **`BR-19` (COD chưa đối soát ⇒ `0008`) — CHƯA PHỦ.** v1 **chưa** có rule `RequiresVerifiedCod` trong `PromotionRuleRegistry` (chỉ 4 rule: `MinOrderAmount`/`ChannelScope`/`CustomerGroup`/`FirstOrderOnly`). Nợ `SHP-003`/`REC-003` (đối soát COD) — khi có rule COD thì bổ sung test `TC-24`.
3. **33 ràng buộc `Pro*`** trong `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`: đã khai báo **có lý do** (cơ chế `RangBuocDbChuaAnhXaTests`) vì chúng là **hàng rào cuối** cho ghi tắt/race — domain/validator đã ném mã riêng trước. Nợ `be`: nếu muốn **mã lỗi riêng** khi DB chặn (thay vì `eCommerce:Db:0001`), chuyển dòng tương ứng sang `ViPhamRangBuocDb`.
4. **`D-283`** — chỉ **5/9** metric promotion có điểm phát; 4 còn lại (`PromotionVoucherReserved/Released/ReservationExpired` + `PromotionApplied`) ở `ChuaCoNoiPhat`, chờ đường giữ/nhả lượt voucher (`PRO-001`) và mốc "sau commit" (`Sales`). **`D-284`** — guard chống trùng **payload** ở biên ghi **chưa** áp v1 (mới có khoá idempotency + unique); sẽ áp cùng đường ghi lượt.
5. **`BR-12`** phủ ở **hành vi** (tính tiền qua `Money`/`decimal`); **chưa** có test quét tĩnh `double`/`float` riêng cho `Promotions` (không có `double`/`float` trong code — kiểm tay + `NFR-10` tổng thể).
6. **`BR-16`** phủ ở **hành vi** (engine không có đường ghi; `TC-43` một lượt/đơn); **chưa** có test reflection "không setter công khai"/"không có `Delete`" trên `PromotionRedemption` (entity đã đóng setter `private set`). Nợ nhỏ, không chặn.
7. **Probe `403` cho 7 route `/api/v1/promotion/**` — CHƯA ĐO ĐƯỢC (N97).** Host trả `404` cho `/api/identity/**` ⇒ không tạo được user thường qua HTTP để đo "thiếu quyền ⇒ 403". **Không hạ kỳ vọng 403**; nợ chủ `qa`, hạn `W24-5` (khuôn `D-249` ①). Admin `200` + ẩn danh `401` đã đo thật. Khi Identity HTTP API được bật (hoặc có user seed sẵn) ⇒ chạy lại `tools/probe-promotion-wp24.ps1` (T04–T08).
8. **Probe `approve` `200` / `0006`→`409` — CHƯA ĐO ĐƯỢC.** Bảng `ProPromotionRuleCosts` **rỗng** trên DB dev (seeder `PromotionRuleCostSeedContributor` chưa chạy) ⇒ `save-tree` mọi rule ⇒ `0004` "Rule chua tung dang ky" ⇒ không có cây hợp lệ để duyệt. Đo được map `0001`→`400` (65 node) và `0004`→`400`. Chạy `DbMigrator`/seed rồi chạy lại probe (T10b–T12).
9. **⚠️ Quan sát cho REV/ORCH:** `GET /api/v1/promotion/rule-catalog` trả **`[]`** trên Host thật trong khi unit test `TC-71(i)` kỳ vọng đủ 4 tên mặc định khi bảng chi phí rỗng ⇒ nghi `IPromotionConditionRule` **không được DI nạp** trong Host runtime (`PromotionRuleRegistry.SystemNames` rỗng). `qa` **không sửa** mã sản phẩm; cần REV xác nhận có phải lỗi đăng ký DI/module hay không — nếu đúng thì đây là lỗi **chặn dùng** màn dựng cây (FE lấy nhãn lá từ `rule-catalog`).
