# Testplan `PRO-002` — 27/11/2026

- SRS: `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (v0.15)
- Kiến trúc bổ trợ: `docs/features/promotions/PRO-002-arch.md`
- Nhánh: `feat/gd3-wp24-dieu-kien-km` · Phiên `N96` · Hàng `W24-4` · Bước **F4 QA (vòng 2/2)**
- Quyết định nguồn: `D-285`…`D-290` (`docs/nhanh/decisions.md`) — bề mặt `/api/v1/promotion` 7 route, `Limits`, `rule-catalog`,
  nhãn lấy từ code; `D-283` (metric chỉ 5/9 có điểm phát), `D-284` (guard chống trùng payload **chưa** áp v1).
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

## 2. Số ca theo tầng (K-06)

| Tầng | Trước (đầu vòng 2) | Sau | Ghi chú |
|---|---|---|---|
| Toàn bộ `test/eCommerce.Tests` (unit) | **2432** | **2432** | Baseline phiên **2419** + 13 ca `W24-4` (vòng 1). Vòng 2 **không** thêm ca unit (chỉ testplan + negative control). 0 fail. |
| `test/eCommerce.IntegrationTests` (PostgreSQL thật) | **156** | **156** | +17 ca `PostgreSqlPromotionSchemaTests` (vòng 1). 0 fail. |
| Domain invariant / Unit (`Promotions`) | 43 | 43 | `HaTang`/`PromotionDomainTests`/`PromotionEngineTests`/`PromotionSurfaceTests`/`PromotionCheckoutGuardTests`. |
| Application / API surface (`Promotions`) — unit mới | 0 | 13 | `PromotionV1SurfaceTests` (6) + `PromotionRuleCatalogTests` (5) + `PromotionGetLimitsTests` (2). |
| API `/api/v1` (200 + 401/403) | — | **reflection** | Route/permission kiểm **tĩnh** bằng phản chiếu (`TC-70a`). **Không** chạy probe HTTP thật trong `W24-4` (WP test không yêu cầu Host sống; `409/400/401` khi chạy thật do `SAL-002`/`WEB-*` phủ ở hàng sau). |
| UI Vitest / Playwright | **60** | **60** | `W24-3` đã có (list/new/tree builder) — hàng trước, **không** đổi `W24-4` (QA không có FE). |
| OpenAPI drift | — | **N/A** | `W24-4` không đổi DTO/route (schema additive đã ở `W24-3`) ⇒ `pnpm gen:api` không đổi `schema.d.ts`. |

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

## 4. Cổng (đo trong phiên N96)

```powershell
dotnet build eCommerce.slnx --no-incremental      # 0 error / 9 warning (baseline phiên)
dotnet test test/eCommerce.Tests --no-build       # 2432 pass · 0 fail
dotnet test test/eCommerce.IntegrationTests       # 156 pass · 0 fail (Testcontainers postgres:17-alpine)
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/PRO-002-testplan.md -Srs docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md   # exit 0
pwsh -File tools/kiem-link-tai-lieu.ps1           # PASS
```

- `dotnet build eCommerce.slnx --no-incremental` → **0 error**, **9 warning** (không phát sinh warning mới — đều ở Address/Sales).
- `dotnet test test/eCommerce.Tests --no-build` → **2432/2432 pass**, exit **0**.
- `dotnet test test/eCommerce.IntegrationTests` → **156/156 pass**, exit **0**.
- `check-agent-artifact -Kind testplan` → **exit 0** (`BR trong SRS=20 · chua co trong testplan=0`; negative control có ≥2 dòng `exit`).
- `pwsh -File tools/kiem-link-tai-lieu.ps1` → **PASS** (290 tệp `.md`, 976 link markdown, 1743 đường dẫn backtick — 0 tham chiếu chết).
- `git status --porcelain -- modules/ src/` rỗng sau toàn bộ negative control (code sản phẩm khôi phục nguyên trạng).

## 5. Chưa phủ / nợ

1. **`BR-10` (áp khuyến mãi qua một UoW — `Redemption` + đơn + outbox) — CHƯA PHỦ.** Đường áp vào **đơn thật** nằm ở `Sales` (`SAL-002`/`SAL-001`) — chưa có ở `WP-24`. Các tiểu-ca `TC-43` cần app service/UoW cũng thuộc nhóm này: rollback cả đơn khi `23505` (0 `Order`, 0 redemption, bộ đếm không đổi), `UsageSequence` 1→2 qua hai đơn, đua 2 đơn song song, `MaxUsagePerCustomer=1` bỏ qua candidate, vãng lai `CustomerId NULL`, `TC-43 (ix)` unit `CustomerRequired`. **Đã phủ** phần **hàng rào DB** (`TC-43 (i)`: cộng dồn hợp lệ 1 voucher + 2 tự động khác `PromotionId`; `(ii)/(iii)`: 3 unique chặn trùng ⇒ `23505`; ca hợp lệ).
2. **`BR-19` (COD chưa đối soát ⇒ `0008`) — CHƯA PHỦ.** v1 **chưa** có rule `RequiresVerifiedCod` trong `PromotionRuleRegistry` (chỉ 4 rule: `MinOrderAmount`/`ChannelScope`/`CustomerGroup`/`FirstOrderOnly`). Nợ `SHP-003`/`REC-003` (đối soát COD) — khi có rule COD thì bổ sung test `TC-24`.
3. **33 ràng buộc `Pro*`** trong `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`: đã khai báo **có lý do** (cơ chế `RangBuocDbChuaAnhXaTests`) vì chúng là **hàng rào cuối** cho ghi tắt/race — domain/validator đã ném mã riêng trước. Nợ `be`: nếu muốn **mã lỗi riêng** khi DB chặn (thay vì `eCommerce:Db:0001`), chuyển dòng tương ứng sang `ViPhamRangBuocDb`.
4. **`D-283`** — chỉ **5/9** metric promotion có điểm phát; 4 còn lại (`PromotionVoucherReserved/Released/ReservationExpired` + `PromotionApplied`) ở `ChuaCoNoiPhat`, chờ đường giữ/nhả lượt voucher (`PRO-001`) và mốc "sau commit" (`Sales`). **`D-284`** — guard chống trùng **payload** ở biên ghi **chưa** áp v1 (mới có khoá idempotency + unique); sẽ áp cùng đường ghi lượt.
5. **`BR-12`** phủ ở **hành vi** (tính tiền qua `Money`/`decimal`); **chưa** có test quét tĩnh `double`/`float` riêng cho `Promotions` (không có `double`/`float` trong code — kiểm tay + `NFR-10` tổng thể).
6. **`BR-16`** phủ ở **hành vi** (engine không có đường ghi; `TC-43` một lượt/đơn); **chưa** có test reflection "không setter công khai"/"không có `Delete`" trên `PromotionRedemption` (entity đã đóng setter `private set`). Nợ nhỏ, không chặn.
