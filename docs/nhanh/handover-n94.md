# Handover N94 — WP-24 / hàng `W24-2` (F3 BE) XONG — **mở F3 FE (`W24-3`)**

> Phiên `N94` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W24-2` · WP-24 · mã `PRO-002` · module `Promotions` (**mới**) ·
> nhánh `feat/gd3-wp24-dieu-kien-km` (base `origin/tich-hop/gd3` @ `84157a1`; đỉnh trước phiên `56ef7f7`) · 27/11/2026.
> Phạm vi phiên: **F1** `ba` áp 5 `docDeviation` D1–D5 + 2 P3 + cờ `Promotions:Enabled` → **F1b** `docrev` kiểm nhanh **DOC-OK** →
> **F2** `arch` đóng P3 allowlist + gộp `D-283`/`D-284` → **F3 BE** `be` dựng module `Promotions` + migration + engine + D6 →
> `decider` **D-283/D-284** → `qa` hợp nhất 43 test vào project chuẩn → **G3** `kiem-tat-ca.ps1` **exit 0**.

## 1. Đã xong

- **F1 BA (SRS v0.10 → v0.11)** — `ba` ×2 vòng: ghi 5 `docDeviation` **D1–D5** vào thân SRS (§5.2 enum chuỗi vs as-built int + `Currency char(3)` +
  `CHECK ^[A-Z]{3}$`; §6.1 namespace `.Contracts`; §7.1 ETO **không** kế thừa `IntegrationEventBase`, `PromotionAppliedEto` fail-closed;
  §7.2 #4 `OrderCancelledEto` chờ SAL-004); thêm cờ **`Promotions:Enabled`** (§11, đúng quy ước `<Module>:<Key>` as-built); đóng **2 P3** của SRS
  (54→55; delta `pro-001` đổi trích số dòng cũ → tên mục) + 2 P3 phát sinh (header v0.11; `REP-001 §5.2.1 LineDiscount`); **9 dòng metric** + note
  vào **`XC-005` §5.1**. **F1b DOCREV** — `docs/reviews/PRO-002-docreview.md` vòng 5 **`DOC-OK` P1=0/P2=0** (5 P3, đã đóng tiếp).
- **F2 ARCH (v0.3 → v0.6)** — đóng P3 vòng 3 (`§2.1`/`§2.2-B2`/`§10.2`: allowlist **đúng** `eCommerce.Messaging.EventCoverage`, tên test
  `RanhGioiPromotionsTests`); cập nhật trạng thái (SRS v0.11, 0 P3); gộp `D-283`/`D-284` vào §8/§3.7 + thêm `docDeviation` **D7–D10**; ghi tên
  index rút gọn ≤ 63 byte + vị trí contract (assembly Domain / namespace `.Contracts`).
- **F3 BE — module `Promotions` (mới, 4 project)**:
  - **Domain**: enum, `Promotion` (máy trạng thái **NÉM `0006`**, cây bất biến từ `Approved`), `PromotionCondition`, `PromotionRuleCost`,
    `VoucherCode`, `PromotionMapping`, `FlashSaleSlot`, `PromotionRedemption`, `PromotionErrorCodes` (`0001–0010`), `PromotionLockKeys`,
    `PromotionIdempotencyKeys`, settings `Promotions:*`, `PromotionRuleRegistry` + **4 rule** thuần (`MinOrderAmount`/`ChannelScope`/`CustomerGroup`/
    `FirstOrderOnly`), `PromotionTreeValidator` + `PromotionTreeEvaluator`, seeder upsert **0 DELETE**.
  - **Contracts**: `IPromotionEngine` + request/result/breakdown, `PromotionRejectionReason` (**12 giá trị 0…11, ghim ordinal**),
    `IPromotionConditionRule`, `IPromotionAppService` (**đúng 10 method**, không `UpdateAsync`).
  - **Application**: `PromotionEngine` (**CHỈ ĐỌC**, 0 idempotency guard, 0 truy vấn trong vòng lá), `PromotionDiscountCalculator`,
    `PromotionAppService` (10 method, `[Authorize]` cấp class + method hẹp; `SaveTree` chỉ `Draft`, xoá `Depth` giảm dần), `PromotionExpiryWorker`
    (kích hoạt + hết hạn; `DISTINCT TenantId` + `ICurrentTenant.Change` + khoá `SkipIfBusy`).
  - **EF**: mapping **7 bảng `Pro*`** + CHECK; **7 `DbSet`** trên `eCommerceDbContext`; **một** migration EF `20261126224627_Add_Promotion_Module`
    (+ Designer + snapshot) + SQL thô `UNIQUE NULLS NOT DISTINCT`/partial.
  - **Sales (D6)**: `CheckoutAppService` **NÉM** `eCommerce:Sales:0004` khi `CheckoutInput.OrderDiscount ≠ 0` **trước mọi tác dụng phụ**;
    gọi engine **chỉ khi** `Promotions:Enabled=true`, nối `DiscountTotal`; thêm ref `Promotions.Application.Contracts`. **Không** sửa
    `Order`/`TinhTienDon`/`Pos`/`Pricing`.
  - **Quyền** `eCommerce.Promotions.{Default,View,Manage,Approve,Import}` khai **2 nơi**; **9 metric** (+ cấm nhãn `promotionId`/`voucherCode`/`customerId`);
    map **13 mã lỗi** → HTTP ở Host.
- **QA hợp nhất test** — `qa` copy **43 test** từ project ngoài chuẩn sang `test/eCommerce.Tests/Promotions/` (5 tệp, diff IDENTICAL) + thêm 4
  `ProjectReference`; sửa `RepVanHanhTests` `All.Count` **30 → 39** (registry tăng thật, không nới assertion).
- **Decider D-283/D-284** — D-283: giữ **9** tên `promotion_*`, 4 tên chưa có luồng ở `ChuaCoNoiPhat` như **nợ có chủ** (PRO-001/SAL-001);
  D-284: **không** nối `IIdempotencyGuard` cho `SaveTree`/`Import`, giữ allowlist ranh giới, idempotent bằng khoá phân tán + delete-then-insert.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** · 68,1 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` | 0 | **2376/2376** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 287 tệp · 973 link · 1733 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` (điều kiện vào) | 0 | **XANH** |
| `check-feature-doc.ps1 -Path <SRS> -WP14` (sau F1) | 0 | **DAT** · 16/16 mục · 20/20 BR có test |
| `check-agent-artifact.ps1 -Kind docreview` (SRS + ARCH) | 0 | **DAT** · `DOC-OK` **P1=0 · P2=0** (×2) |
| `check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 117 dòng cần kiểm |
| `dotnet run --project src/eCommerce.DbMigrator` (sau `pg_dump`) | 0 | áp **1** migration · 7 bảng `Pro*` + `__EFMigrationsHistory` kiểm thật |
| `pwsh -File tools/kiem-tat-ca.ps1` (**G3**) | 0 | **10/10 bước · 1244,5 s** — xem dưới |
| **Đo lại lúc đóng** `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 error · 9 warning** · 160 s |
| **Đo lại** `dotnet test test/eCommerce.Tests --no-build` | 0 | **2419/2419** · 25 s |
| **Đo lại** `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 974 link · 1732 backtick · 0 chết |
| **Đo lại** `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |

`kiem-tat-ca.ps1` chi tiết: build **0 error/9 warning** · unit **2419/2419** (+43) · integration **139/139** · mermaid **214 khối/0 lỗi** ·
cổng tài liệu **XANH** · FE typecheck **0 lỗi** · `gen:api` **không đổi** · **DB 25/25** · test cổng **60/60** · E2E **bỏ qua** (chế độ nhẹ; `-DayDu` chạy ở F6).

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W24-2`** — DoD F3 BE đạt (G3 exit 0). **WP-24 còn 3 hàng**: `W24-3` (F3 FE), `W24-4` (F4 QA + F5 REV), `W24-5`
(F6 OPS + G6, gồm `-DayDu` + push `tich-hop/gd3`). Việc F4 bắt buộc:
1. Testplan + **negative control** đỏ→xanh; phủ **integration** `TC-33/34/42/43` trên DB thật (NND/23505, CHECK, một-lượt-tự-động/đơn).
2. `TC-22` ghi rõ **5/9** metric có điểm phát thật, **4/9** nợ có chủ (`D-283`) — không ghi "9/9".
3. Phần "guard chống trùng payload" = **chưa áp dụng v1** (`D-284`) — không đánh dấu đã phủ.

## 3. Migration đã áp

- **`20261126224627_Add_Promotion_Module`** (+ `.Designer.cs` + `eCommerceDbContextModelSnapshot.cs`), EF sinh, expand-only.
- `pg_dump` trước khi áp: **`artifacts/pg-dump/N94-W24-2.dump`** — **523.700 byte**.
- Áp bằng `dotnet run --project src/eCommerce.DbMigrator` (không `dotnet ef database update`); kiểm **7 bảng** `ProPromotions`, `ProPromotionConditions`,
  `ProPromotionRuleCosts`, `ProVoucherCodes`, `ProPromotionMappings`, `ProFlashSaleSlots`, `ProPromotionRedemptions` + hàng `__EFMigrationsHistory`.
- 3 tên index dài > 63 byte được **rút gọn** (PostgreSQL cắt âm thầm): `IX_ProPromotionConditions_Tenant_Promotion_Parent_Sort`,
  `IX_ProPromRedemptions_Tenant_Prom_Order_Voucher`, `IX_ProPromRedemptions_Tenant_Prom_Cust_Seq`; đã đọc `pg_indexes` xác nhận đủ `UNIQUE` + NND + WHERE.

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung (ngắn) |
|---|---|---|
| `D-283` | TẠM CHỐT (DEC) | Giữ **9** tên `promotion_*` trong `All`; **4** tên chưa có luồng (`promotion_voucher_reserved/_released/_reservation_expired` = PRO-001; `promotion_applied_total` = SAL-001) ở `ChuaCoNoiPhat` như **nợ có chủ**; sửa ARCH §8 + SRS §13 + XC-005 §5.1 (đã áp). |
| `D-284` | TẠM CHỐT (DEC) | **Không** nối `IIdempotencyGuard` cho `SaveTree`/`Import`; giữ allowlist ranh giới đúng `eCommerce.Messaging.EventCoverage`; idempotent = khoá phân tán `Tree`/`Import` + delete-then-insert + import chỉ-cập-dòng; retry `SaveTree` tăng `TreeVersion` ở `Draft` (chấp nhận). |

`check-agent-artifact -Kind decision` **DAT**; hàng đợi xem lại thêm 2 dòng.

## 5. Nợ / rủi ro

- 🟡 **`OrderCancelledEto` chưa tồn tại** (SAL-004) ⇒ consumer hoàn lượt **chưa nối**; `PromotionAppliedEto` **chưa phát** (chưa có handler
  Reporting/AccountingHub — fail-closed, không "phát cho có").
- 🟡 **4/9 metric** ở `ChuaCoNoiPhat` (`D-283`) — đóng khi PRO-001 (3 metric voucher) / SAL-001 (`promotion_applied_total`) có đường ghi.
- 🟡 **`IIdempotencyGuard` chưa nối** cho `SaveTree`/`Import` (`D-284`) — nợ "nối khi PRO-001 dùng chung 2 method"; mở rộng allowlist **phải** kèm `D-xx` mới.
- 🟡 **`docDeviation` D7–D10** (tên index rút gọn; contract ở assembly Domain/namespace `.Contracts`; không guard; 4 metric nợ) — đã ghi SRS + ARCH.
- 🟡 **SRS §11 nhóm khoá `eCommerce.Promotions.MaxNodes`…** có thể còn lệch quy ước `<Module>:<Key>` (`Promotions:MaxNodes`) — P3 tài liệu, chủ `ba` (vòng sau).
- 🟡 **`pro-001` §6.1 route** `/promotion/voucher/validate` vs `pro-002` §6.3 `/promotion/validate-voucher` — P3 chưa đồng bộ (ABP sinh theo tên method ⇒ §6.3 đúng).
- 🟡 **`ImportRuleCosts`** parser CSV tối thiểu (chưa khớp định dạng tệp seed chính thức).
- 🟡 **F4** phải làm integration `TC-33/34/42/43` + negative control; `TC-22` ghi 5/9.
- **Chờ người (vùng cấm)**: (a) duyệt lô `D-277…D-284`; (b) **AQ10/Q8** GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release;
  (d) merge `main` (biên bản người ký).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG W24-2 (WP-24 F3 BE) → mở `W24-3` (WP-24 F3 FE).**

- Hàng kế = **`W24-3`** (loại `code`, bước `F3 FE`): màn `apps/admin-web` dựng cây điều kiện AND/OR + form khuyến mãi + màn checkout/chi tiết đơn
  (§10 SRS), dùng **`schema.d.ts` sinh từ OpenAPI** (10 route `/api/app/promotion/**`); 3 trạng thái loading/empty/error, chống bấm lặp, định dạng VN.
- Điều kiện vào: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` exit 0 (đã đo) + `D-277` hiệu lực; đọc `docs/features/promotions/pro-002-...` §10
  + `PRO-002-arch.md` §4.4 (route) trước khi code.
- **Nhớ**: F4 (`W24-4`) phải làm integration + negative control; F6 (`W24-5`) chạy `-DayDu` + push `tich-hop/gd3`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`.
- Commit phiên: **`56ef7f7`** (F1 SRS/ARCH/DOCREV — docs) → **`0ebc110`** (F3 BE + tests + D-283/D-284 + handover doc-state), cả hai **đã push** `origin/<nhánh>`.
- **Không** push `tich-hop/gd3` (chỉ hàng `dong-wp` = `W24-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N94`): `ba` ×3 (F1 · 4 P3 · D-283/D-284) · `arch` ×3 (P3 allowlist · header · D-283/D-284 + D7–D10) · `docrev` ×1
(2 artifact) · `be` ×1 (3 pha + 2 `SendMessage` dọn test) · `qa` ×1 · `decider` ×1. **0 lần chặn dừng.**
Cảnh báo hook: **false positive "thiếu khối JSON"** (`docrev`, `qa` — báo cáo thực có khối, đã biết `chanDungKhiThieuJson=false`) và
**"ghi ngoài vùng" do chạy song song** (`arch`↔`ba`, `decider` thấy tệp của `qa` trong `git status`) — kiểm lại `git status`: **0 tệp lọt vùng thật**.
