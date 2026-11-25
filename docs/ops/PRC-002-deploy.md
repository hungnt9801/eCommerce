# Triển khai `PRC-002` — 26/11/2026

> Phiên `N75`, hàng `W16-5`, chuỗi `gd3-1`, WP-16. Nhánh `feat/gd3-wp16-bang-gia`
> (base `origin/tich-hop/gd3` @ `e481094`). Quyết định nguồn: `D-217`, `D-218`, `D-219`, `D-220`, `D-221`,
> `D-222`, `D-223`, `D-224`, `D-229`, `D-230` ([decisions.md](../nhanh/decisions.md)).
> Đây là WP **BE thuần** — **0 migration, 0 FE, 0 package, 0 biến môi trường mới, 0 đổi `.github`/compose/Dockerfile**.

## 1. Phạm vi

- **Nhánh/PR:** `feat/gd3-wp16-bang-gia` → `tich-hop/gd3` theo **chuỗi `D-216` phương án B**: nhánh WP xếp chồng trên
  `tich-hop/gd3`, **không** mở PR mỗi phiên (PR xếp chồng mở ở cuối chuỗi). Không merge `main`.
- **Hợp đồng API:** **không đổi DTO/route/chữ ký** (`IPriceCalculationAppService`, `GioTinhTienDto`,
  `ITierPriceSetAppService`, `schema.d.ts` giữ nguyên).
- **Migration:** **0** (`D-218` ②). Không `migrations add`, không chạm schema; không cần `pg_dump`.
- **FE:** **0** (UI Pricing thuộc `WEB-001` — `PRC-002-arch.md` §6.1).
- **Package:** **0** (không sửa `Directory.Packages.props` / `eCommerce.slnx` / `pnpm-lock.yaml`).

### 1.1 File sản phẩm thay đổi (7 tệp, `modules/Pricing/**`)

| Tệp | Thay đổi |
|---|---|
| `modules/Pricing/eCommerce.Pricing.Domain/PricingSettingDefinitionProvider.cs` | **Mới** — khai 4 Setting của module (ABP tự đăng ký theo quy ước, phạm vi `Application \| Tenant` qua `WithProviders`, loại `User`) |
| `modules/Pricing/eCommerce.Pricing.Domain/PricingChinhSach.cs` | **Mới** — tên khoá `PRC002SettingTenKhoa`, enum `ChinhSachTongDonAm`, 4 hàm đọc **thuần** (không I/O, không ném) |
| `modules/Pricing/eCommerce.Pricing.Domain/PricingErrorCodes.cs` | Thêm `TongDonAm` = `eCommerce:Pricing:0010` · `VuotSoBacToiDa` = `eCommerce:Pricing:0011` (append cuối dải `0001…0011`) |
| `modules/Pricing/eCommerce.Pricing.Domain/TinhTienDon.cs` | Overload 5 tham số nhận `ChinhSachTongDonAm`; `Reject` NÉM `0010` khi tổng `< 0`; `Clamp` kẹp giảm cấp đơn **trước** phân bổ R2; `KetQuaTinhTien` thêm `BiKep`/`SoKep` |
| `modules/Pricing/eCommerce.Pricing.Domain/ChonDonGiaCuoi.cs` | `ChinhSachSnapshot` thêm tham số cuối `ChinhSachTongDonAm` **có giá trị mặc định** `Reject` (additive, không phá bên gọi) |
| `modules/Pricing/eCommerce.Pricing.Application/PriceCalculationAppService.cs` | `TaoChinhSachSnapshotAsync()` đọc 3 Setting **một lần** ở B1 + `LogWarning` khi giá trị không hợp lệ; `[CHINH-SACH-DA-DOC]` |
| `modules/Pricing/eCommerce.Pricing.Application/TierPriceSetAppService.cs` | `AddTierAsync` đọc `Pricing:MaxTiersPerSet`, đếm bậc hiện có, vượt ngưỡng ⇒ NÉM `0011` (**trước** khi chèn) |

**Test thay đổi:** `test/eCommerce.Tests/Pricing/**` (thêm `PriceCalculationAppServiceTests`, `TierPriceSetAppServiceTests`,
`PricingChinhSachTests`, `PricingBatBienTienTests`, `PricingErrorCodesTests`, `PricingKienTrucTests`, hạ tầng `HaTang/**`,
và cập nhật các ca cũ) + `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` (4 tên index NND vào `phaiCo`)
+ **1 dòng** `ProjectReference eCommerce.Pricing.Application` trong `test/eCommerce.Tests/eCommerce.Tests.csproj` (`D-229`).
Chi tiết phủ BR và số ca: [PRC-002-testplan.md](../tests/PRC-002-testplan.md).

### 1.2 Hành vi mới (cần nói với bên gọi trước khi deploy)

1. **Setting bậc giá** — 4 khoá `Pricing:*` (bảng §2.2) đọc từ ABP Setting thay cho giá trị **cứng trong code**:
   `TinhGioAsync` dựng `ChinhSachSnapshot` một lần ở B1 với chính sách đọc từ cấu hình (mặc định giữ y hành vi cũ).
2. **Trần số bậc — `eCommerce:Pricing:0011`** — `AddTierAsync` từ chối bậc vượt `Pricing:MaxTiersPerSet` (mặc định **20**),
   NÉM kèm `tierPriceSetId`/`soBacHienTai`/`toiDa`. Guard vận hành, **không** phải bất biến dữ liệu (race 1 bậc chấp nhận — `-arch` §3.3).
3. **Guard tổng âm — `eCommerce:Pricing:0010`** — mặc định `Reject`: `TinhGioAsync` **NÉM `0010`** cho giỏ có giảm cấp đơn
   vượt thành tiền (trước WP-16 trả **tổng âm**). `Sales` (`CheckoutAppService`/`DonHangAppService`) sẽ nhận
   `BusinessException` thay vì số âm — **thay đổi có chủ đích** theo `D-219` (fail-closed). Tổng `= 0` **hợp lệ**.
4. **4 tên index `NULLS NOT DISTINCT`** — `PostgreSqlSchemaTests` kiểm **đích danh** 4 index thay vì chỉ đếm tổng
   (`-arch` §6.4); **chỉ đổi test**, không đổi index trong DB.

## 2. Biến môi trường

| Tên | Mặc định dev | Bắt buộc production | Ghi chú |
|---|---|---|---|
| — | — | — | **Không có biến môi trường ứng dụng mới.** |

**Không thêm biến môi trường.** Bốn chính sách là **ABP Setting** lưu trong bảng `AbpSettings` (đã tồn tại), **định nghĩa
trong code** (`PricingSettingDefinitionProvider`) — **không** phải env/`.env`/secret store. Giá trị mặc định lấy từ
`SettingDefinition.DefaultValue`; đổi giá trị sau triển khai bằng endpoint `setting-management/*` của ABP (Host đã nạp),
**không cần deploy lại**.

### 2.1 Bốn Setting (nguồn: `PricingSettingDefinitionProvider.cs`)

| Khoá | Mặc định | Giá trị hợp lệ | Phạm vi | Quyết định |
|---|---|---|---|---|
| `Pricing:RoundingPolicyCode` | `half-away-from-zero` | **chỉ** `half-away-from-zero` (`none` bị loại) | `Application \| Tenant` | `D-221` |
| `Pricing:MissingPricePolicy` | `NemLoi` | `NemLoi` · `DungGiaNiemYet` | `Application \| Tenant` | `D-218` |
| `Pricing:MaxTiersPerSet` | `20` | số nguyên `≥ 1`; `0`/âm/không phải số ⇒ **dùng 20** | `Application \| Tenant` | `D-224` |
| `Pricing:OrderTotalNegativePolicy` | `Reject` | `Reject` · `Clamp` | `Application \| Tenant` | `D-219` |

- **Mặc định production = mặc định trong code** nên **không cần thao tác khởi tạo**: giá trị lạ/rỗng **không ném** —
  trả mặc định an toàn + `LogWarning` (`hopLe = false`) để vận hành thấy cấu hình bị bỏ qua.
- ⚠️ **Chưa chứng minh được provider trong assembly module được ABP quét trên Host thật** (nợ **R8** / `D-217`④).
  Hệ quả nếu không quét: 4 khoá **không tồn tại** ⇒ mọi cấu hình **bị bỏ qua im lặng** (mặc định vẫn an toàn, không mất tiền).
  Đây là **bước kiểm sau triển khai bắt buộc** (§5.2).

## 3. Thứ tự triển khai

1. **Merge code BE trước** (nhánh → `tich-hop/gd3`): binary mới chứa provider Setting, reader thuần, mã `0010`/`0011`,
   overload `TinhTienDon`, guard số bậc. Commit sản phẩm: **`100db0a`** (`feat(pricing): N72 - WP-16 W16-2 F3 BE …`).
2. **Không có FE** — không deploy `apps/admin-web`.
3. **`DbMigrator` bằng binary MỚI build** (M5): chạy `dotnet run --project src/eCommerce.DbMigrator` rồi **xác nhận
   `__EFMigrationsHistory` KHÔNG đổi** (0 migration; chạy binary mới để chắc binary cũ không để lại trạng thái lệch).
   **Cấm** `dotnet ef database update` (M6 / `D-75`).
4. **Không bật job/worker nào** — WP không có nền tảng chạy nền, không gọi dịch vụ ngoài.
5. **Cấu hình `setting-management` (tùy chọn):** chỉ khi vận hành muốn khác mặc định; ghi ở **global (host) hoặc tenant**
   đúng phạm vi, **không** ghi theo user.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| Chính sách làm tròn / thiếu giá / **tổng âm** (dữ liệu cấu hình) | Đặt 3 khoá về mặc định trong §2.1 hoặc **xoá dòng `AbpSettings`** tương ứng ⇒ về đúng giá trị mặc định, **không deploy** | Cấu hình, 0 dòng code. Mặc định hiện tại = hành vi cũ của 2 chính sách đầu. |
| Guard số bậc `0011` | Đặt `Pricing:MaxTiersPerSet` = số lớn **≤ 2.147.483.647** (ví dụ `2000000000`) ⇒ guard **vô hiệu** mà không deploy | ⚠️ **KHÔNG** dùng `99999999999` — P2 REV #1 (`PRC-002-review.md`): `DocSoBacToiDa` ép `decimal→int` không chặn trần ⇒ giá trị `> int.MaxValue` **ném `OverflowException`** ⇒ `AddTier` trả **500**. Muốn tắt tuyệt đối ⇒ revert `100db0a`. |
| Guard tổng âm `0010` | Đặt `Pricing:OrderTotalNegativePolicy = Clamp` ⇒ **không ném**, kẹp tổng `≥ 0` (không quay lại tổng âm — `D-219` **cấm** `AllowNegative`) | Muốn quay lại **đúng hành vi cũ (`tổng âm`)** phải revert code `100db0a` (`TinhTienDon.cs` + 1 tệp test) — chấp nhận nợ vì `D-219`. |
| Setting snapshot / đọc 1 lần (`PriceCalculationAppService`) | Revert `100db0a` (1 commit, 7 tệp sản phẩm ở §1.1) | Không chạm dữ liệu, không đổi schema. |
| 4 tên index NND trong `phaiCo` | Revert phần `PostgreSqlSchemaTests.cs` (test-only, cùng `100db0a`) | Không đổi index trong DB ⇒ revert chỉ làm mất **độ chặt của test**, không ảnh hưởng sản phẩm. |
| Dữ liệu | **Không cần `pg_dump`** | 0 migration, 0 `UPDATE`/`DELETE` mới, không chạm schema; K-01/M9 **không kích hoạt**. |

- **Không revert migration** — không có migration nào để revert.
- Không tắt bước CI nào để xanh. `git revert 100db0a` là đường lùi code **một-commit**; đường lùi **cấu hình** (3 dòng
  đầu bảng) **rẻ hơn và không cần deploy** — ưu tiên dùng trước.

## 5. Cổng (số đo thật, phiên `N75`)

| Lệnh | Kết quả |
|---|---|
| `dotnet test test/eCommerce.Tests --no-build --filter "FullyQualifiedName~Pricing"` | **exit 0** · **Failed: 0, Passed: 144, Skipped: 0, Total: 144** · 625 ms |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | **exit 0** · **XANH** — `moi ma cua WP-16 deu 'Tam duyet' trong bien ban tam duyet` ([D-217](../nhanh/decisions.md)) |

- **G6 — việc của ORCH, sau khi tài liệu này xong:** `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` ⇒ **số bước + thời gian
  do ORCH ghi vào handover `N75` và `docs/nhanh/07-ke-hoach-trien-khai.md` §13**. OPS **KHÔNG** chạy `-DayDu` trong
  phiên này (máy dev từng **hết bộ nhớ** — `N69`; không chạy song song build/test nặng).
- **Số đo tham chiếu (phiên `N74`, không phải phiên này):** `dotnet test test/eCommerce.Tests --no-build` → **2021/2021**;
  `pwsh -File tools/kiem-tat-ca.ps1` → **10/10 bước · 287,4 s** — xem [handover-n74.md](../nhanh/handover-n74.md) §1.

### 5.1 PR / CI

- PR hiện đúng **2 required check**; không bước nào bị `continue-on-error` để che lỗi.
- WP **không** chạm `.github/**`, `docker-compose.yml`, `Dockerfile` ⇒ **không** phát sinh bước CI mới.

### 5.2 Kiểm sau triển khai

1. `GET /api/health` → **200**.
2. **R8 bắt buộc:** xác nhận 4 khoá §2.1 **tồn tại và đọc được** ở **cả host (global) và một tenant**
   (qua `setting-management` hoặc log `[CHINH-SACH-DA-DOC]` khi tính 1 giỏ). Nếu không đọc được ⇒ dừng, xem §6 (nợ R8 / `D-217`④).
3. **Luồng chính:** tính 1 giỏ hợp lệ → **200**, `RoundingPolicyCode` trong kết quả = `half-away-from-zero`.
4. **Guard `0010`:** giỏ có giảm cấp đơn vượt thành tiền → **`BusinessException` `eCommerce:Pricing:0010`** (không trả số âm).
5. **Guard `0011`:** `AddTier` vào bộ đã đủ `MaxTiersPerSet` (mặc định 20) → **NÉM `eCommerce:Pricing:0011`**, **không** chèn bậc.

## 6. Khi hỏng — ai gọi

- **Sai mã lỗi `0010`/`0011`** (nhầm dải, không ném khi phải ném, `WithData` sai khoá) ⇒ **BE/arch**; đối chiếu
  `PRC-002-arch.md` §3.4 và [PRC-002-review.md](../reviews/PRC-002-review.md). Không phải OPS.
- **Setting không đọc được ở host/tenant** (giá trị đổi mà hệ thống vẫn chạy mặc định; `[CHINH-SACH-DA-DOC]` không xuất hiện)
  ⇒ nợ **R8** / `D-217`④: cần **khởi động Host thật** để kiểm ABP có quét provider trong assembly module không (M6 —
  build/test không chứng minh được đăng ký DI). Chủ: QA/ARCH; đường lùi kỹ thuật là chuyển lớp provider sang `src/eCommerce.Domain/Settings/`.
- **Guard tổng âm chặn oan / không chặn** ⇒ **BE** (`TinhTienDon` / `PriceCalculationAppService`), không nới guard để xanh.
- **`AddTier` trả 500 khi `MaxTiersPerSet` rất lớn** ⇒ đúng P2 REV #1; xử lý theo §4 (dùng số `≤ 2.147.483.647`) tới khi BE vá.

> Ngoài phạm vi WP-16: sửa `probe-cong-tai-lieu.ps1` (`D-217`④) là việc **QA**; 4 `docDeviation` SRS là nợ **BA/ARCH**
> (`D-230`). Không xử lý trong tài liệu này.
