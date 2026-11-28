# `PRC-001` — Kiến trúc động cơ giá vốn bình quân gia quyền di động (WP-17 · `W17-1`)

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `PRC-001` (phần `arch`) |
| **Module chủ** | **Pricing** — `modules/Pricing/**` |
| **Phạm vi** | `WP-17`, hàng `W17-1` (F0–F2b) của chuỗi `gd3-1` — **đường ghi + luồng duyệt `CostPrice` + động cơ bình quân gia quyền di động** |
| **Quyết định nguồn** | `D-177` (bình quân di động, ngày bán, chỉ dòng `DaDuyet`, thiếu ⇒ `NULL` + `CostMissing`) · `D-186` (chủ = Pricing, `INV-001` chỉ cấp đơn giá nhập) · `D-181`/`D-176` (Reporting tiêu thụ, `LG1/LG2`, vòng quay) · `D-195` (ACC-001 không xuất dòng giá vốn) · `D-318` C4/C5/C6/C9 · `D-09` (không rename) · `D-17` (enum thêm cuối) |
| **SRS** | `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` §0/§5.2/§20/§15.1 · `docs/features/inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md` §0/§5.2/§8 · `docs/features/reporting/rep-003-bao-cao-ton-kho-hieu-suat-kenh.md` §0/§5 |
| **Schema** | **1 bảng mới + 4 cột nullable trên `PrcCostPrices` + index** — **expand-only**, **không** chạm `StockLedger`/`InvStockLedger` |
| **Hợp đồng công khai** | **Chỉ thêm** method/DTO; **không** đổi chữ ký đang có (`ICostPriceAppService`, `StockDocumentLine`, `StockDocumentLineDto` giữ nguyên) |
| **FE** | **Không có** màn hình mới trong WP-17 (đường đọc giá vốn đã có ở `/GiaVon` từ `S12`); màn vận hành động cơ là `WEB-001` |

> 📄 **Loại tài liệu:** `-arch` là **tài liệu bổ trợ**, **ngoài** mẫu 16 mục / BR / "Đối chiếu Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó.
> Vì vậy tệp này không có `## 0.` / `## 10.`–`## 15.` là **đúng chủng loại** (đúng tiền lệ `PRC-002-arch.md:14-16`).

> 🔒 **Điều kiện vào của `W17-1`:** `probe-cong-tai-lieu -CheDo Code -WP WP-17` đã **XANH**; G1 `D-319` đã tạm duyệt
> `prc-001` §15.1 Q8 / §20.4 + `inv-001` §0. **Q4** (`CostPrice` có bắt buộc luồng duyệt 2 bước) **vẫn `Open`** —
> là câu **kế toán**, KHÔNG tự chốt; §3.2 nêu **điểm chặn/thay đổi được không cần đổi schema** và §8 đưa vào `openQuestions`.

---

## 1. Bối cảnh + phạm vi

`PrcCostPrices` + đường đọc đã có từ `S12` (`PRC-001` §20): entity `CostPrice` (`modules/Pricing/eCommerce.Pricing.Domain/CostPrice.cs:36`),
mapping + 3 `CHECK` (`modules/Pricing/eCommerce.Pricing.EntityFrameworkCore/PricingDbContextModelCreatingExtensions.cs:100-135`),
`DbSet` (`src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:146`), đường đọc `ICostPriceAppService`
(`modules/Pricing/eCommerce.Pricing.Application.Contracts/IPriceAppServices.cs:42-52`, hiện thực
`modules/Pricing/eCommerce.Pricing.Application/CostPriceAppService.cs:24-82`). **Thiếu** đúng ba thứ mà `D-186` giao cho Pricing:

1. **Đường ghi + duyệt**: hiện `ICostPriceAppService` **không có** method ghi (`S12` cố ý chỉ mở đường đọc — `PRC-001` §20.3 #3).
   Guard duyệt đã có sẵn nhưng chưa ai gọi: `CostPrice.Duyet(nguoiDuyet, atUtc)` (`CostPrice.cs:121-140`) **NÉM** khi thiếu người
   duyệt/đã duyệt; `DaDuyet` = đủ `ApprovedBy` **và** `ApprovedAtUtc` (`CostPrice.cs:62-68`).
2. **Động cơ định giá bình quân gia quyền di động** (`D-177` ①, `D-318` C9): tính lại giá vốn từ **đơn giá nhập** (Inventory cấp,
   sổ cái **không** lưu đơn giá — `StockLedger.cs:243-308`) và ghi ra các dòng `PrcCostPrices` theo **kỳ hiệu lực**.
3. **Hợp đồng đọc cho module tiêu thụ**: Reporting (`REP-002`/`REP-003`) và `ACC-001` cần **một** method trả
   `(Amount, CurrencyCode | null)` theo `(Sku, ngày hiệu lực)`, **chỉ** dòng `DaDuyet = true` (`D-177` ④, `D-186` ②).

### 1.1 Phạm vi

| Trong phạm vi (`W17-1` thiết kế) | Ngoài phạm vi (ghi rõ để không ai làm nhầm ở đây) |
|---|---|
| Đường ghi + duyệt `CostPrice` (command + quyền `.Manage`/`.Approve`) | **Tính lãi gộp** (`GrossMargin1/2`) và **vòng quay tồn kho** — thuộc `REP-002`/`REP-003` (WP-32/WP-33), tiêu thụ hợp đồng ở §3.1 |
| Động cơ bình quân gia quyền di động + job/command tính lại | **Bút toán kế toán 632/156** — `D-195` đã chốt GĐ3 **không** phát dòng giá vốn trong file `ACC-001` |
| Port đọc Inventory (chỉ đọc) + adapter | **Sửa sổ cái kho** (`StockLedger`/`InvStockLedger`) hay thêm đơn giá vào sổ cái — `INV-001` §5.2.2 :334 **giữ nguyên** sổ cái thuần số lượng |
| Chụp giá vốn theo **ngày bán** (qua hợp đồng đọc) | **Chụp snapshot vào `RptFactSalesLines.CostUnitAmount`/`CostMissing`** — là việc của job Reporting (`D-177` ⑤); Pricing chỉ cấp số |
| Setting phương pháp/policy + fail-closed khi cấu hình sai | Màn cấu hình vận hành/UI — `WEB-001`; dùng endpoint `setting-management/*` của ABP |

---

## 2. Ranh giới module (một chiều, không đổi)

### 2.1 Chiều phụ thuộc

```
Host (eCommerce.HttpApi.Host)
  └─ eCommerce.Application
        └─ Pricing.Application ──► Pricing.Domain ──► eCommerce.Kernel (Money, Currency, IRoundingPolicy)
        │        │
        │        └─► Inventory.Application.Contracts   (chỉ port ICostFeedLookup + DTO; KHÔNG Inventory.Domain)
        └─ Inventory.Application ──► Inventory.Domain  (adapter đọc sổ cái/chứng từ; hiện thực ICostFeedLookup)

Reporting ──► Pricing.Application.Contracts (ICostPriceLookup — port trần chỉ đọc; ICostPriceAppService cho HTTP/UI)
```

- `Pricing.Domain` **KHÔNG** tham chiếu module nào của app hay của Inventory (`modules/Pricing/eCommerce.Pricing.Domain/PricingDomainModule.cs:9-18`:
  chỉ `[DependsOn(typeof(AbpDddDomainModule), typeof(eCommerce.Kernel.eCommerceKernelModule))]`). Động cơ **thuần** nằm ở Domain
  vì nó chỉ nhận dữ liệu đã nạp → không kéo I/O vào Domain.
- **Chiều mới duy nhất được thêm:** `Pricing.Application` → `Inventory.Application.Contracts` — **sẽ thêm** `ProjectReference` vào
  `ItemGroup` của `modules/Pricing/eCommerce.Pricing.Application/eCommerce.Pricing.Application.csproj` (hiện dòng 8–11 **chỉ** có
  Contracts + EFCore của Pricing). Đây đúng `M2`/`D-186` ③: gọi chéo module **chỉ** qua `*.Application.Contracts`.
  `Inventory.Application.Contracts` **không** tham chiếu Pricing ⇒ không có vòng.
- **Chiều `Reporting` → `Pricing.Application.Contracts`:** `Reporting.Application.csproj` **thêm `ProjectReference` → `eCommerce.Pricing.Application.Contracts`**
  (khuôn `ProjectReference` giữa module; **KHÔNG** `[DependsOn]` — Reporting chỉ thấy port/DTO, không kéo `Pricing.Application`/`Pricing.Domain`).
  Đường lùi: gỡ `ProjectReference` + bỏ lời gọi `ICostPriceLookup` (hoặc tắt đọc giá vốn) = **revert commit, 0 migration**.
- ⚠️ **Nợ bắc cầu (P3 #14):** `eCommerce.Inventory.Application.Contracts.csproj` **có** `ProjectReference` tới `Inventory.Domain`
  ⇒ Domain Inventory *có mặt bắc cầu* trong đồ thị build của Pricing.Application. Bất biến thực thi: **CẤM** `using eCommerce.Inventory;`
  (entity Domain) trong mọi tệp `modules/Pricing/**`; chỉ dùng DTO/port ở Contracts. Thêm test kiến trúc (khuôn `SalesModuleKienTrucTests.cs:71-88`)
  khẳng định `modules/Pricing/**` không chứa `StockLedgerEntry`/`StockDocument`/`InventoryItem`.
- **CẤM** (điều kiện `D-318` C4 (1) + `D-186` ⑥): `Pricing.**` tham chiếu `eCommerce.Inventory.Domain`, đọc trực tiếp
  `IRepository<StockLedgerEntry>`/chuỗi `InvStockLedger`, hay tham chiếu `StockDocument`/`StockDocumentLine` (đều thuộc `Inventory.Domain`).
- `Inventory` **không** đọc/ghi `PrcCostPrices`, **không** tính lại giá vốn (`D-186` ②, `INV-001` §0 :56-58).

### 2.2 Chủ sở hữu

| Tài nguyên | Chủ | Bằng chứng |
|---|---|---|
| `PrcCostPrices` + đường ghi/duyệt + động cơ | **Pricing** | `D-186` ①, `PRC-001` §20 |
| Đơn giá nhập (`StockDocumentLine.UnitPrice`) để **tra cứu** | **Inventory** (as-built; tài liệu gọi `UnitCost`) | `StockDocument.cs:361-367`; `inv-001` §5.2.3 :376 |
| Số lượng/đơn giá **đầu vào** cho động cơ | Inventory **cấp** qua port §3.3 | `D-186` ③ |
| Snapshot giá vốn trên fact báo cáo | **Reporting** (`RptFactSalesLines`) | `D-177` ⑤ |
| Bút toán kế toán | `ACC-001` — **không** phát dòng giá vốn ở GĐ3 | `D-195` |

> 📌 **Lệch as-built so với SRS (ghi để không ai "sửa" nhầm):** `inv-001` §5.2.3 đề xuất `InvReceipts/InvReceiptLines` với
> `UnitCost` + `UnitCostCurrency`; **as-built** dùng hợp nhất `StockDocument`/`StockDocumentLine`, đơn giá là
> `StockDocumentLine.UnitPrice` kiểu **`numeric(18,2)`** (property `StockDocument.cs:366`, khai báo `:361-367`), **không có** cột tiền tệ đi kèm.
> Theo `D-09` **không** đổi tên cột; ánh xạ *"`UnitCost` (tài liệu) ↔ `UnitPrice` (as-built)"* (`D-186` ④). Hệ quả tiền tệ ⇒ §8 **AQ3**.

---

## 3. Hợp đồng

### 3.1 Đọc giá vốn cho module khác — **port đọc trần `ICostPriceLookup`** (KHÔNG qua app service `[Authorize]`)

🚨 **P1 — vì sao KHÔNG mở rộng `ICostPriceAppService`:** class `CostPriceAppService` có
`[Authorize(PricingPermissions.CostPriceView)]` **cấp class** (`CostPriceAppService.cs:24`). Job nền `REP-002`/`REP-003`
**không có user/quyền** ⇒ gọi in-process sẽ **NÉM `AbpAuthorizationException`** — đúng lỗi đã đo ở `SAL-005` và là lý do
tồn tại `IStockReservationGateway` (`Inventory.Application.Contracts/IStockReservationGateway.cs:8-10`). Ngoài ra `IApplicationService`
⇒ ABP tự mở endpoint cho **mọi** method mới; method lô dùng tuple còn làm vỡ `gen:api`/`schema.d.ts`.

**Chọn: port đọc trần** (không `IApplicationService`, không `[Authorize]`, KHÔNG thành endpoint) đặt ở `Pricing.Application.Contracts`,
hiện thực (adapter) ở `Pricing.Application` — **cùng khuôn** `IStockReservationGateway` (`IStockReservationGateway.cs:17-30`,
impl `StockReservationGateway.cs:11`):

```csharp
namespace eCommerce.Pricing;

public class CostLookupItemDto { public string Sku { get; set; } = default!; public DateOnly At { get; set; } }
public class GetApprovedCostManyInput { public List<CostLookupItemDto> Items { get; set; } = new(); }

/// Kết quả tra giá vốn ĐÃ DUYỆT tại một ngày. AmountValue null = KHÔNG có giá vốn đã duyệt
/// ⇒ bên gọi PHẢI đặt CostMissing = true và để cột tiền NULL; CẤM ghi 0 (D-177 ⑥).
public class CostPriceLookupDto
{
    public string Sku { get; set; } = default!;
    public DateOnly At { get; set; }
    public decimal? AmountValue { get; set; }   // null = chưa có dòng DaDuyet hiệu lực
    public string? CurrencyCode { get; set; }   // null khi AmountValue null
    public bool CoGiaVon => AmountValue is not null;
}

/// 🚨 Port ĐỌC NỘI BỘ cho Reporting (KHÔNG phải endpoint, KHÔNG kiểm quyền — quyền ở app service/HTTP).
public interface ICostPriceLookup
{
    /// Giá vốn ĐÃ DUYỆT hiệu lực tại `at` (`CostPrice.CoHieuLucTai`). null = KHÔNG có. CHỈ lọc `DaDuyet == true`.
    Task<CostPriceLookupDto?> GetApprovedCostAsync(string sku, DateOnly at);

    /// Lô cho job báo cáo (tránh N+1). Dùng DTO — KHÔNG dùng tuple.
    Task<List<CostPriceLookupDto>> GetApprovedCostManyAsync(GetApprovedCostManyInput input);
}
```

- **Ai cài đặt:** `CostPriceLookupAdapter : ICostPriceLookup, ITransientDependency` trong `Pricing.Application` (ABP tự đăng ký
  theo `ITransientDependency`; đọc `IRepository<CostPrice>` dưới tenant filter hiện tại). **Không** là `IApplicationService` ⇒ không lộ endpoint.
- **Ai gọi:** job tổng hợp `REP-002`/`REP-003` **in-process** (chụp `CostUnitAmount`/`CostMissing` — `D-177` ⑤). **ACC-001 KHÔNG
  tiêu thụ ở GĐ3** (`D-195`: 0 cột giá vốn trong file kế toán); hợp đồng chỉ để dành tương lai.
- **Đường HTTP/UI vẫn là `ICostPriceAppService`** (`CostPriceAppService.cs:24` — `[Authorize …CostPrice.View]` cấp class, **giữ nguyên**,
  **không** thêm method). Port trần không mở endpoint ⇒ không lộ giá vốn ra công khai (BR-013).
- **Không trả `Money`:** trả `(AmountValue, CurrencyCode)` — cùng lựa chọn `CostPriceDto` (`CostPriceAppService.cs:71-82`); bên gọi dựng lại
  `Money.Cua(amount, Currency.FromCode(cc))` khi cần toán (`shared/eCommerce.Kernel/Money/Money.cs:36`, `Currency.cs:67`).
- **Chỉ `DaDuyet = true`:** lọc `ApprovedBy != null && ApprovedAtUtc != null` + `CoHieuLucTai(at)` (`CostPrice.cs:68,113-115`).

### 3.2 Đường ghi + luồng duyệt — **command + quyền**

Thêm app service riêng (không nhồi vào đường đọc để giữ quyền `.View` tách `.Manage`/`.Approve`):

```csharp
namespace eCommerce.Pricing;

public class CreateCostPriceDto
{
    [Required, StringLength(64)] public string Sku { get; set; } = default!;
    // `PrcCostPrices.AmountValue` là numeric(18,4) ⇒ tối đa 14 chữ số nguyên (KHÔNG dùng biên 18,2 của dòng phiếu kho)
    [Range(typeof(decimal),"0","99999999999999.9999")] public decimal AmountValue { get; set; }
    [Required, StringLength(8)] public string CurrencyCode { get; set; } = "VND";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [Required, StringLength(128)] public required string IdempotencyKey { get; set; }   // XC-002 — BẮT BUỘC
}

public interface ICostPriceAdminAppService : IApplicationService
{
    Task<CostPriceDto> CreateAsync(CreateCostPriceDto input);      // trạng thái CHƯA duyệt (DaDuyet = false)
    Task<CostPriceDto> ApproveAsync(Guid id);                       // gọi CostPrice.Duyet; id là khoá tự nhiên
}
```

- **Quyền:** `eCommerce.Pricing.CostPrice.Manage` (tạo/sửa) và `eCommerce.Pricing.CostPrice.Approve` (duyệt). **Chỉ khai hai
  quyền này KHI có method dùng** (`PRC-001` §20.3 #4 — cấm "quyền rỗng"). Khai **hai nơi** như tiền lệ: `PricingPermissions`
  (`modules/Pricing/eCommerce.Pricing.Application.Contracts/PricingPermissions.cs:36` cho `.View`) **và**
  `eCommercePermissionDefinitionProvider` (thiếu định nghĩa ⇒ `403` mọi người).
- **Guard (NÉM, không no-op — `D12`)**: `CostPrice..ctor` đã kiểm SKU rỗng / tiền âm / kỳ ngược (`CostPrice.cs:79-104`, giá vốn `0`
  **hợp lệ** — `:85-87`); `Duyet` kiểm thiếu người duyệt / **đã duyệt** (`CostPrice.cs:121-140`). Mã lỗi dùng lại
  `eCommerce:Pricing:0009` (`PricingErrorCodes.cs:46`).
- **Idempotency tạo:** `CreateCostPriceDto.IdempotencyKey` **BẮT BUỘC** (`[Required]`); DB có unique **filtered**
  `(TenantId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL` (`NULLS NOT DISTINCT`) — §4.1 S7/S8. Cùng khoá ⇒ trả dòng đã tạo,
  **không** tạo thứ hai; không gửi khoá ⇒ `400` (`XC-002`). (Phụ thuộc `IIdempotencyGuard`/`InboxRecords` của `XC-002` **chưa có** —
  nợ `PRC-001` §15.2 **F3**; v1 dùng unique DB + run ledger §4.1.)
- **Duyệt — KHÔNG idempotent ngầm:** `ApproveAsync(Guid id)` dùng `id` làm khoá tự nhiên; gọi lặp ⇒ `CostPrice.Duyet` **NÉM `0009`**
  ("đã duyệt") — **không** trả "kết quả cũ" (khớp `CostPrice.Duyet` + `D12`). Không có `IdempotencyKey` cho duyệt.
- **Q4 (OPEN) — điểm chặn/thay đổi KHÔNG cần đổi schema:** thiết kế chạy **2 bước** (tạo = chưa duyệt → `ApproveAsync` gọi
  `Duyet`) vì `DaDuyet`/`Duyet()` **đã có** và `D-177` ⑥ đòi dòng `DaDuyet=true`. Nếu kế toán kết luận **không cần 2 bước**, chỉ đổi
  một chỗ: trong `CreateAsync` gọi `Duyet(hệ-thống-actor, Clock.Now)` ngay sau khởi tạo (auto-approve) — **0 migration, 0 đổi cột**.
  Nếu cần **thêm** bước (nhập→kiểm→duyệt), trạng thái vẫn suy từ hai cột `ApprovedBy/ApprovedAtUtc` ⇒ vẫn **0 đổi schema** (chỉ thêm method/nhãn).
  ⇒ Q4 **không** chặn thi công §3.2; đưa vào `openQuestions` (`PRC-001-AQ1`).

### 3.3 Port đọc Inventory — **chỉ đọc**, khai ở `Inventory.Application.Contracts`

Theo **đúng** khuôn gateway hiện có (`modules/Inventory/eCommerce.Inventory.Application.Contracts/IStockReservationGateway.cs:17-30`:
interface **trần**, **không** `IApplicationService`, **không** `[Authorize]`, **không** thành endpoint).

Tệp mới `modules/Inventory/eCommerce.Inventory.Application.Contracts/ICostFeedLookup.cs`:

```csharp
namespace eCommerce.Inventory;

/// Loại chuyển động theo nghĩa ĐỊNH GIÁ (không phải nghĩa kế toán kho).
public enum LoaiChuyenDongGiaVon
{
    NhapCoGia = 0,    // tăng OnHand + có đơn giá ⇒ tính lại bình quân
    NhapKhongGia = 1, // tăng OnHand nhưng KHÔNG có đơn giá ⇒ giá vốn KHÔNG xác định từ mốc này
    Xuat = 2          // giảm OnHand ⇒ giảm số lượng, KHÔNG đổi đơn giá bình quân
}

public class CostMovementDto
{
    public string Sku { get; set; } = default!;
    public DateOnly NgayKinhDoanh { get; set; }      // ngày theo UTC+7 (D-177 ③) — tính ở adapter (biên I/O)
    public DateTime OccurredAtUtc { get; set; }      // tiebreak tất định trong cùng ngày
    public LoaiChuyenDongGiaVon Loai { get; set; }
    public decimal SoLuongDelta { get; set; }        // dấu: nhập > 0, xuất < 0
    public decimal? DonGiaNhap { get; set; }         // chỉ khi NhapCoGia; as-built = StockDocumentLine.UnitPrice
    public string? TienTeNhap { get; set; }          // null ⇒ mặc định VND của tenant (AQ3)
    public Guid ReferenceLineId { get; set; }        // neo idempotency (dòng chứng từ / bút toán)
}

/// 🚨 CHỈ ĐỌC. Pricing dùng để tính giá vốn; KHÔNG ghi sổ cái, KHÔNG sửa chứng từ.
public interface ICostFeedLookup
{
    /// Chuyển động OnHand ảnh hưởng định giá trong [from, to] (ngày kinh doanh UTC+7), tất định theo
    /// (NgayKinhDoanh, OccurredAtUtc, ReferenceLineId). BỎ chuyển kho (net 0 toàn hệ thống) và giữ chỗ/tạm giữ.
    Task<List<CostMovementDto>> LayChuyenDongAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<string>? skus = null);
}
```

- **Hiện thực (adapter):** tệp mới `modules/Inventory/eCommerce.Inventory.Application/CostFeedLookupAdapter.cs`,
  `public class CostFeedLookupAdapter : ICostFeedLookup, ITransientDependency` — **cùng khuôn đăng ký** `StockReservationGateway`
  (`modules/Inventory/eCommerce.Inventory.Application/StockReservationGateway.cs:11`). ABP tự đăng ký theo `ITransientDependency`
  ⇒ **không** sửa `InventoryApplicationModule` (`InventoryApplicationModule.cs:11-17` trống `AbpModule`).
- **Dữ liệu đọc (trong Inventory.Domain — hợp lệ vì adapter nằm trong module Inventory):** sổ cái `StockLedgerEntry`
  (`StockLedger.cs:243-308`) cho **số lượng có dấu** (chỉ `StockLedgerEntry` **đã ghi sổ** — dòng chỉ sinh khi `GhiSo`)
  và **join** `StockDocumentLine.UnitPrice` (`StockDocument.cs:361-367`) qua `ReferenceLineId` (`StockLedger.cs:273`) để lấy
  đơn giá **nhập**. Ngày kinh doanh = `OccurredAtUtc` (`StockLedger.cs:278`) đổi sang **UTC+7** **ngay trong adapter** (biên I/O).
- **Ánh xạ loại (đề xuất, AQ3 chốt phần biên):**
  - `NhapCoGia` = tăng `OnHand` **có** `UnitPrice` — chủ yếu `StockMovementType.Receipt=0` của phiếu `GoodsReceipt=0`.
  - `NhapKhongGia` = tăng `OnHand` **không** có đơn giá — `CustomerReturn=4`, `Adjustment=5` (tăng), `StocktakeGain=7`,
    `ShippingReturn=12`, `WarrantyRelease=16`.
  - `Xuat` = giảm `OnHand` — `Sale=3`, `StocktakeLoss=8`, `ShippingOut=11`, `DamagedOut=14`, `WarrantyHold=15`, `RetailSaleOut=19`,
    `SupplierReturnOut=20`, **`OtherOut=21`** (`Enums.cs:112-118`).
  - **Bỏ** (không đổi `OnHand`): `Reservation=1`, `ReservationRelease=2`, `Hold=17`, `HoldRelease=18`, **`DamagedIn=13`** (hàng vào chiều
    `Damaged`, `OnHand` **không** đổi — `Enums.cs:82-83`), và `TransferOut=9`/`TransferIn=10` (net 0 toàn hệ thống ở grain SKU — `D-318` C9).
  - 🚨 **Loại lạ (không có trong 3 nhóm trên) ⇒ adapter NÉM `eCommerce:Pricing:0014`** (fail-closed), **KHÔNG** bỏ qua im lặng
    (bỏ qua ⇒ `qty`/`value` sai ⇒ bình quân các kỳ sau sai mà không có lỗi). Nguồn enum: `src/eCommerce.Domain.Shared/Enums.cs:26-118`,
    `src/eCommerce.Domain.Shared/Enums.Inventory.cs:4-36`.
- **Không đọc chéo domain:** Pricing **không** nhận entity; chỉ nhận **DTO** ở trên. `ReferenceLineId` là `Guid` trần (không FK chéo module).

### 3.4 Hợp đồng **không** đổi (đối chiếu review)

| Bề mặt | Trạng thái | Bằng chứng |
|---|---|---|
| `StockDocumentLine.UnitPrice` (kiểu, tên, ý nghĩa) | **không đổi** (`numeric(18,2)`, nullable) | `StockDocument.cs:361-367` |
| `StockDocumentLineDto` / `CreateStockDocumentLineDto` / `IStockDocumentAppService` | **không đổi** | `StockDocumentDtos.cs:51-73,155-187,202-245` |
| `ICostPriceAppService.GetListAsync/GetAsync` | **không đổi** (không thêm method; hợp đồng đọc nội bộ là port trần `ICostPriceLookup`) | `IPriceAppServices.cs:49,51` |
| `CostPrice` cột hiện có (`Sku/AmountValue/CurrencyCode/EffectiveFrom/EffectiveTo/ApprovedBy/ApprovedAtUtc`) | **không đổi tên/kiểu** (`D-09`) | `CostPrice.cs:45-68`; mapping `PricingDbContextModelCreatingExtensions.cs:117-134` |
| `StockLedger` / `InvStockLedger` (cấu trúc, cột, index) | **không đổi** — `INV-001` §5.2.2 :334 | `StockLedger.cs:243-308` |
| `eCommerce.Pricing.Application.Contracts` DTO của báo giá bán | **không đổi** ⇒ `pnpm gen:api` không đổi `schema.d.ts` | `PriceCalculationDtos.cs`; `CostPriceDto` giữ |

---

## 4. Schema + migration (expand-only)

### 4.1 Bảng thay đổi — từng bảng/cột/index (điều kiện `D-318` C4 (1)(2)(3)(7))

| # | Bảng | Thay đổi | Kiểu | Null? | Default | Chủ | Ghi chú |
|---|---|---|---|---|---|---|---|
| S1 | **`PrcCostEngineRuns`** *(mới)* | bảng nhật ký mỗi lần chạy động cơ (audit + idempotency run) | — | — | — | Pricing | `Id uuid` PK · `TenantId uuid` · `RunKey varchar(128)` · `FromDate date` · `ToDate date` · `Trigger varchar(64)` · `TrangThai int` · `Attempt int` · `StartedAtUtc timestamptz` · `FinishedAtUtc timestamptz?` · `RowsUpserted int` · `RowsSkippedApproved int` · `RowsFlaggedMissing int` · `ErrorMessage varchar(2048)?` + audit ABP |
| S2 | `PrcCostEngineRuns` | index unique | `(TenantId, RunKey)` | — | — | Pricing | **`NULLS NOT DISTINCT`** (sửa tay trong migration), tên `IX_PrcCostEngineRuns_TenantId_RunKey` |
| S3 | `PrcCostEngineRuns` | index tra cứu | `(TenantId, StartedAtUtc)` | — | — | Pricing | tên `IX_PrcCostEngineRuns_TenantId_StartedAtUtc` |
| S4 | `PrcCostPrices` | **thêm cột** `NguonTinh` | `int` (enum `CostPriceSource`) | ✔ | — | Pricing | `ThuCong=0`·`DongCo=1`; NULL = dòng cũ (coi như thủ công) — **additive, không `SET NOT NULL`** |
| S5 | `PrcCostPrices` | **thêm cột** `RunId` | `uuid` | ✔ | — | Pricing | lần chạy sinh/cập nhật (truy vết; **không** FK) |
| S6 | `PrcCostPrices` | **thêm cột** `ComputedAtUtc` | `timestamptz` | ✔ | — | Pricing | thời điểm động cơ tính |
| S7 | `PrcCostPrices` | **thêm cột** `IdempotencyKey` | `varchar(128)` | ✔ | — | Pricing | idempotency đường ghi (`XC-002`) |
| S8 | `PrcCostPrices` | index unique **có filter** | `(TenantId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL` | — | — | Pricing | **`NULLS NOT DISTINCT`** (sửa tay), tên `IX_PrcCostPrices_TenantId_IdempotencyKey` |
| S9 | `StockLedger` / `InvStockLedger` | **KHÔNG ĐỔI** | — | — | — | Inventory | `D-318` C4 (1); `INV-001` §5.2.2 :334 |

- **Enum `CostPriceSource`** là enum **mới**, giá trị `ThuCong=0`/`DongCo=1`; tuân `D-17` (thêm **cuối dải**, không đổi số cũ — ở đây là dải mới).
- **`PrcCostPrices` có thể có dữ liệu** (seed/ghi ngoài luồng — `PRC-001` §20.4 #1; migration `20261028031842_Gd1S12_CostPrice` **không** `InsertData`)
  ⇒ `pg_dump` phòng ngừa (K-01). 4 cột mới **đều nullable, không default bắt buộc, không `SET NOT NULL`, không đổi kiểu cột cũ** ⇒ thoả `D-318` C4 (1)(2).
- **Không** chạm `XC-001`/`RoundingPolicy` (`D-318` C4 (4), C5 (ii)).

### 4.2 Migration plan — expand → migrate → contract

| Bước | Việc | Ai | Lệnh / bằng chứng |
|---|---|---|---|
| **0. `pg_dump`** | `PrcCostPrices` **có dữ liệu** ⇒ `pg_dump` **trước** khi áp (K-01) | BE (OPS xác nhận) | `pg_dump -Fc ... > dump-before-wp17.dump`; ghi đường dẫn vào handover |
| **1. Expand** | `dbContext` thêm `DbSet<CostEngineRun>` + mapping `PrcCostEngineRuns`; thêm 4 property nullable vào `CostPrice`; BE để **EF sinh** migration tên gợi ý `Added_Pricing_CostEngine`; **sửa tay** hai unique index sang `NULLS NOT DISTINCT` | **Một chủ migration = BE `W17-2`** | `dotnet ef migrations add Added_Pricing_CostEngine --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.HttpApi.Host`; đọc diff: chỉ `CREATE TABLE` + `ADD COLUMN` nullable + `CREATE INDEX` — **0 `DROP`/`ALTER TYPE`/`SET NOT NULL`** |
| **2. Migrate** | **Không backfill bắt buộc** (cột mới nullable, bảng mới rỗng). Tuỳ chọn: chạy động cơ **một lần** để sinh dòng `NguonTinh=DongCo` — **chỉ sau khi G2 duyệt** và có người duyệt dòng | BE `W17-2` | `dotnet build eCommerce.slnx --no-incremental` rồi `dotnet run --project src/eCommerce.DbMigrator` (KHÔNG `database update` — skill M6); kiểm `__EFMigrationsHistory` + `\d "PrcCostPrices"` + `pg_indexes` có `NULLS NOT DISTINCT` |
| **3. Contract** | **Không** ở WP-17 (không xoá cột nào). Việc xoá cột cũ để lại release sau | — | — |

- **Bất biến kiểm:** unique index mới **phải** `NULLS NOT DISTINCT` — EF **không** sinh được (`M7`; tiền lệ `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:66-68`), nếu quên thì `TenantId = NULL` (1 tenant) làm index **mất tác dụng im lặng** ⇒ `RunKey`/`IdempotencyKey` trùng lọt.
- **`StockDocumentLine.UnitPrice` KHÔNG đổi kiểu** dù nó là đầu vào: động cơ đọc như hiện có, làm tròn kết quả về `numeric(18,4)` ở `PrcCostPrices.AmountValue` (đã `HasPrecision(18,4)` — `PricingDbContextModelCreatingExtensions.cs:120`).

### 4.3 Đường lùi migration (bằng tệp/lệnh cụ thể)

| Tình huống | Cách lùi |
|---|---|
| Migration **chưa** merge/áp | `git revert <SHA commit W17-2>` + `dotnet ef migrations remove --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.HttpApi.Host` (khôi phục `ModelSnapshot`) |
| Migration **đã** áp | Chạy `Down()` của migration (DROP TABLE `PrcCostEngineRuns` + DROP 4 cột + DROP index) **sau `pg_dump`**; hoặc **không** revert schema mà chỉ **tắt động cơ** bằng Setting `Pricing:GiaVonDongCo=false` (dữ liệu cũ vẫn đọc được, cột mới để NULL) |
| Muốn bỏ hẳn tính năng | Tắt `GiaVonDongCo=false` ⇒ 0 dòng mới; `PrcCostPrices` cũ **nguyên vẹn** (không xoá dòng) |

---

## 5. Luồng

### 5.1 Ghi/duyệt giá vốn (thủ công hoặc do động cơ sinh)

```
POST /api/app/cost-price-admin            (CreateAsync)        [Authorize …CostPrice.Manage — cấp class]
  ├─ dựng CostPrice(Sku, Money.Cua(AmountValue, Currency.FromCode(CurrencyCode)), EffectiveFrom, EffectiveTo)
  │    → CostPrice..ctor NÉM 0009 nếu SKU rỗng / tiền âm / kỳ ngược (giá vốn 0 HỢP LỆ)   (CostPrice.cs:79-104)
  ├─ IdempotencyKey: unique filtered (TenantId, IdempotencyKey) → trùng ⇒ trả dòng cũ
  └─ Insert (DaDuyet = false)

POST /api/app/cost-price-admin/{id}/approve  (ApproveAsync)    [Authorize …CostPrice.Approve]
  └─ giaVon.Duyet(CurrentUser.Id, Clock.Now)  → NÉM 0009 nếu thiếu người / ĐÃ duyệt (CostPrice.cs:121-140)
```

### 5.2 Động cơ bình quân gia quyền di động (job quét + command on-demand)

Bọc ngoài một **hàm THUẦN** ở Domain `TinhBinhQuanGiaQuyenDiDong` (không I/O), orchestration ở Application:

```
CostEngineAppService.ChayAsync(from, to, trigger)      [đăng ký BackgroundWorker + endpoint command]
  ├─ đọc Setting (1 lần): PhuongPhapGiaVon / GiaVonTheoKho / ChoNhapLuiNgay / GiaVonDongCo
  │     • != BinhQuanGiaQuyenDiDong (vd FIFO) ⇒ NÉM 0014 (fail-closed — D-177 ②)
  │     • GiaVonTheoKho = true ⇒ NÉM 0014 (v1 chỉ false — D-318 C9)
  │     • GiaVonDongCo = false ⇒ thoát (đường lùi)
  ├─ GIÀNH khoá theo SKU: hàm MỚI PricingLockKeys.GiaVonTheoSku(tenantId, sku)
  │     → chuỗi "pricing:cost-sku:{tenant|null}:{sku}" (KHÔNG lặp tiền tố "ecommerce:" — XC-003 §5.4; R8)
  ├─ RunKey = cost:{tenant}:{from}:{to}:{trigger}; TÌM PrcCostEngineRuns theo (TenantId, RunKey) — HÀNH VI THEO TrangThai:
  │     • có & ThanhCong ⇒ TRẢ run cũ (idempotent cùng khoảng; KHÔNG INSERT, KHÔNG tính lại)
  │     • có & DangChay  ⇒ NÉM 409/bận (KHÔNG trả run chưa xong, KHÔNG chạy chồng — P2 #7)
  │     • có & Loi       ⇒ chạy lại attempt MỚI trên CÙNG dòng (UPDATE: Attempt++, StartedAtUtc mới, TrangThai=DangChay, xoá ErrorMessage)
  │     • chưa có ⇒ INSERT run (Attempt = 1, TrangThai = DangChay)
  ├─ nạp ICostFeedLookup.LayChuyenDongAsync(from, to)   (qua Inventory.Application.Contracts)
  ├─ với mỗi SKU: replay tất định (Ngày, OccurredAtUtc, ReferenceLineId) → danh sách kỳ giá vốn (Sku, EffectiveFrom, EffectiveTo, AvgCost)
  ├─ đối soát với PrcCostPrices hiện có (theo (TenantId, Sku, EffectiveFrom)):
  │     • AvgCost != null & chưa có ⇒ INSERT (NguonTinh=DongCo, DaDuyet=false)   → chờ kế toán duyệt (§5.1)
  │     • AvgCost != null & có & CHƯA duyệt ⇒ UPDATE amount (vẫn DaDuyet=false)   → idempotent
  │     • AvgCost != null & có & ĐÃ duyệt & amount KHÁC ⇒ §5.4 (nhập lùi ngày) — KHÔNG ghi đè im lặng
  │     • AvgCost == null ⇒ KHÔNG INSERT/UPDATE kỳ (AmountValue NOT NULL — P2 #3); khoảng không có dòng ⇒ lookup trả null
  └─ cập nhật PrcCostEngineRuns (ThanhCong/Loi, RowsUpserted/SkippedApproved/FlaggedMissing)
```

> 📌 **Khai Setting (0 migration — P3 #20):** thêm 4 `SettingDefinition` (+ default) vào `PricingSettingDefinitionProvider`
> (`modules/Pricing/eCommerce.Pricing.Domain/PricingSettingDefinitionProvider.cs:28-36` hiện chỉ có 4 khoá PRC-002) và reader ở
> `PricingChinhSach.cs`. Thiếu định nghĩa ⇒ `ISettingProvider` trả `null` ⇒ fail-closed không chạy như mô tả. `W17-2` **phải khởi
> động Host thật** đọc 4 khoá (R6).

> 🚨 **Mã lỗi mới — append cuối dải (P2 #2):** `PricingErrorCodes` hiện **dừng ở `0011`** (`PricingErrorCodes.cs:58`), còn `PRC-002` **SRS**
> đã cấp `0012` ("Σ dòng ≠ tổng")/`0013` ("chồng khung hiệu lực") ⇒ **KHÔNG** dùng lại. `W17-2` thêm **sau `0011`**:
> `CauHinhGiaVonKhongHopLe = "eCommerce:Pricing:0014"` (setting sai / loại chuyển động lạ) và
> `GiaVonKhongTheTinhLai = "eCommerce:Pricing:0015"` (nhập lùi đụng kỳ đã duyệt). Cập nhật bảng mã lỗi cùng PR.

- Engine **không** tự duyệt: dòng sinh ra là `DaDuyet=false` cho tới `ApproveAsync` (`D-177` ④ yêu cầu chỉ dùng dòng đã duyệt). Nếu
  `PRC-001-AQ1` chốt "không 2 bước" ⇒ auto-approve tại đây (1 dòng), **0 schema**.

### 5.3 Chụp giá vốn theo **ngày bán** (tiêu thụ, không phải Pricing ghi fact)

Reporting (`REP-002`/`REP-003`) khi tổng hợp một dòng bán:

```
job Reporting:  dongBan (Sku, NgayBan theo UTC+7)
  ├─ got = ICostPriceLookup.GetApprovedCostAsync(Sku, NgayBan)   (in-process; chỉ dòng DaDuyet; CoHieuLucTai — CostPrice.cs:113-115)
  ├─ got is null  ⇒ CostUnitAmount = NULL, CostMissing = true, **CẤM 0**   (D-177 ⑥, D-181 ③)
  └─ got khác null ⇒ CostUnitAmount = Money(got.AmountValue, got.CurrencyCode) → lưu snapshot
```

Pricing **không** lưu snapshot theo từng dòng bán (đó là read-model `RptFactSalesLines` của Reporting — `D-177` ⑤, `D-186` ⑤).
"Ngày bán" là **ngày kinh doanh UTC+7**, không phải "giá vốn hiện tại" (`D-177` ③).

### 5.4 Nhập lùi ngày (`Pricing:ChoNhapLuiNgay`)

Một phiếu nhập có ngày kinh doanh **trước** kỳ đã tính của SKU ⇒ có thể **đổi bình quân lịch sử**.

| `ChoNhapLuiNgay` | Hành vi | An toàn dữ liệu |
|---|---|---|
| `false` *(mặc định, đề xuất)* | Động cơ **không** tính lại các kỳ đã có dòng **đã duyệt** ⇒ phiếu nhập lùi bị **bỏ qua cho định giá**; `PrcCostEngineRuns.RowsSkippedApproved++` + `LogWarning`. Kỳ **chưa duyệt** vẫn cập nhật bình thường | Không đổi số đã duyệt ⇒ báo cáo kỳ cũ **không đổi số** (tôn trọng `D-177` ③ tái lập) |
| `true` | Động cơ **tính lại** các kỳ bị ảnh hưởng; nếu phép tính lại **đụng dòng đã duyệt mà giá trị sẽ đổi** ⇒ **NÉM `eCommerce:Pricing:0015`** (`GiaVonKhongTheTinhLai`), **KHÔNG** ghi đè. Kỳ **chưa duyệt** cập nhật bình thường | Không sửa ngầm số đã duyệt; người dùng buộc phải xử lý tường minh (điều chỉnh tay có duyệt) |

> 🚨 **Chốt MỘT hành vi (P2 #4):** cả hai chế độ **từ chối/không ghi đè dòng đã duyệt** — **KHÔNG** có "revision/superseded" trong WP
> này. Lý do: unique `(TenantId, Sku, EffectiveFrom)` (`PricingDbContextModelCreatingExtensions.cs:128-130`) **cấm** hai dòng cùng kỳ;
> revision = thay đổi **phá huỷ** schema (đổi unique) ⇒ phải là `D-xx` mới + người duyệt (`D-318` C6). Nếu tương lai cần revision,
> mở **`PRC-001-AQ2`** (`needs: nguoi`) — **không** tự làm ở `W17-2`.

### 5.5 Thiếu giá vốn ⇒ `NULL` + `CostMissing`, **CẤM `0`**

Ba nguồn "không biết":
1. **Không có dòng `DaDuyet` hiệu lực tại ngày bán** ⇒ `GetApprovedCostAsync` trả `null` ⇒ fact `NULL` + `CostMissing=true`.
2. **Nhập kho không có `UnitPrice`** ⇒ từ mốc đó bình quân **không xác định** (`NhapKhongGia`). `PrcCostPrices.AmountValue` là
   `decimal` **NOT NULL** (`CostPrice.cs:48`) ⇒ động cơ **KHÔNG INSERT kỳ** nào cho tới khi có quy tắc phục hồi; **v1 KHÔNG phục hồi**
   (kể cả khi có lần nhập **có giá** sau đó — không đủ cơ sở định giá phần tồn đã mất giá trị). Khoảng không có dòng ⇒ lookup trả `null`
   ⇒ fact `NULL` + `CostMissing` (không bịa `0`); phục hồi = `PRC-001-AQ4`.
3. **Chưa tính tới kỳ** (bán trước lần nhập đầu) ⇒ không có kỳ ⇒ `null`.

**Giá vốn `0` là HỢP LỆ** (hàng tặng/mẫu — `CostPrice.cs:85-87`) ⇒ `0` **không** bị coi là "thiếu"; chỉ `null` mới là thiếu.

### 5.6 Idempotency ở mọi biên vào (job / event / command)

| Biên | Khoá chống trùng | Hàng rào cuối |
|---|---|---|
| Command tạo giá vốn | `IdempotencyKey` **BẮT BUỘC** (`[Required]`) | unique filtered `(TenantId, IdempotencyKey)` NND (§4.1 S8); thiếu khoá ⇒ `400` |
| Chạy động cơ | `RunKey = cost:{tenant}:{from}:{to}:{trigger}` — theo `TrangThai` của run: `ThanhCong` ⇒ trả run cũ; `DangChay` ⇒ **409/bận**; `Loi` ⇒ **retry attempt mới** (cùng dòng) | unique `(TenantId, RunKey)` NND (§4.1 S2) + replay tất định (§6) |
| Event/job retry | Replay tất định ⇒ cùng đầu vào ⇒ cùng đầu ra; UPSERT theo `(TenantId, Sku, EffectiveFrom)` ⇒ **0 dòng trùng** | unique `(TenantId, Sku, EffectiveFrom)` đã có (`PricingDbContextModelCreatingExtensions.cs:128-130`) |
| Approve lặp | `id` là khoá tự nhiên; **KHÔNG** khoá idempotency riêng | `CostPrice.Duyet` **luôn NÉM `0009`** khi đã duyệt (`D12`) |

> 📌 `IIdempotencyGuard`/`InboxRecords` của `XC-002` **chưa có** (nợ `PRC-001` §15.2 **F3**). v1 dùng **unique DB + bảng run** làm
> hàng rào; khi `XC-002` xong, thay lớp khoá trong suốt, **không** đổi schema.

---

## 6. Công thức — bình quân gia quyền di động (hàm THUẦN)

`modules/Pricing/eCommerce.Pricing.Domain/TinhBinhQuanGiaQuyenDiDong.cs` — **không I/O**, chỉ nhận `CostMovementDto`-tương đương
giá trị (để Domain không tham chiếu Inventory, hàm nhận kiểu giá trị nội bộ, adapter ánh xạ).

Trạng thái chạy theo SKU: `(qtyOnHand, valueOnHand, avgCost | null, costMissing)`.

| Chuyển động | Công thức | Bất biến |
|---|---|---|
| Nhập **có** giá `q, p` (khi `costMissing = false`) | `value += p·q`; `qty += q`; `avg = value / qty` | `avg` qua `Money` (`Money.cs:36,85`); `qty>0` ⇒ chia hợp lệ |
| Nhập **có** giá **SAU khi** `costMissing = true` | `qty += q`; `value` **vẫn không xác định**; `avg` **giữ `null`** | **v1 KHÔNG phục hồi** — không bịa giá trị tồn cũ; phục hồi = `PRC-001-AQ4` |
| Nhập **không** giá `q` | `qty += q`; `value` **không xác định**; `avg = null`, `costMissing = true` | **CẤM** bịa `0` |
| Xuất `q` (`Xuat`) | `value -= avg·q`; `qty -= q`; `avg` **giữ nguyên** | xuất **không** đổi đơn giá bình quân |
| Xuất khi `avg = null` | `qty -= q`; `avg` vẫn `null` | không "hồi phục" số đã mất |

- **Làm tròn:** chỉ khi **xuất kết quả** ra `PrcCostPrices.AmountValue`: `IRoundingPolicy.Round(Money)` của `XC-001`
  (`shared/eCommerce.Kernel/Money/RoundingPolicy.cs`) — **không** đổi policy (`D-318` C4 (4)). **CẤM** `Math.Round`/`decimal.Round` trong module.
- **Kỳ hiệu lực:** mỗi mốc **có nhập** sinh một kỳ; `EffectiveFrom` = ngày kinh doanh của lần nhập; `EffectiveTo` = **ngày trước** kỳ kế tiếp (kỳ cuối `null`). Nhiều phiếu nhập **cùng ngày** ⇒ gộp thành **một** kỳ (bình quân cuối ngày) ⇒ khớp unique `(TenantId, Sku, EffectiveFrom)`. 🚨 **Khi `avg = null` ⇒ KHÔNG sinh kỳ** (`AmountValue` NOT NULL) — khoảng đó không có dòng ⇒ lookup trả `null`; **không** ghi `0` thay chỗ trống.
- **Tất định:** sắp `(NgàyKinhDoanh, OccurredAtUtc, ReferenceLineId)` ⇒ chạy lại bao nhiêu lần cũng cùng số (`D-177` ③).

### 6.1 Ví dụ số (VND, `Money`)

| Ngày (UTC+7) | Chuyển động | qty | value | avg |
|---|---|---|---|---|
| 2026-11-01 | Nhập 10 @ 100.000 | 10 | 1.000.000 | **100.000** |
| 2026-11-05 | Nhập 10 @ 120.000 | 20 | 1.000.000 + 1.200.000 = 2.200.000 | **110.000** |
| 2026-11-07 | Bán 5 | 15 | 2.200.000 − 5·110.000 = 1.650.000 | **110.000** (không đổi) |
| 2026-11-10 | Nhập 5 @ 150.000 | 20 | 1.650.000 + 750.000 = 2.400.000 | **120.000** |

⇒ `PrcCostPrices` sinh 3 kỳ (đều `DaDuyet=false` chờ duyệt):

| Sku | EffectiveFrom | EffectiveTo | AmountValue |
|---|---|---|---|
| SKU-A | 2026-11-01 | 2026-11-04 | 100.000 |
| SKU-A | 2026-11-05 | 2026-11-09 | 110.000 |
| SKU-A | 2026-11-10 | (null) | 120.000 |

- Bán ngày **2026-11-06** ⇒ `GetApprovedCostAsync` trả `110.000` (kỳ 11-05→11-09).
- Bán ngày **2026-10-30** (trước nhập đầu) ⇒ `null` ⇒ fact `NULL` + `CostMissing=true`.
- Khoá `value/qty` luôn qua `Money` (`2.200.000 / 20 = 110.000`); so sánh dùng `Money.CompareTo` (`Money.cs:112`), **không** `==` trên `decimal`.

---

## 7. Ca domain invariant K-06 (điều kiện cứng `D-318` C4 (6) / C5 (vi))

Đầu vào cho `W17-4` (`check-agent-artifact -Kind testplan`). Mỗi ca phải **ĐỎ** khi luật bị vi phạm (negative control).

| Mã | Ca | Loại | Kỳ vọng | Luật nguồn |
|---|---|---|---|---|
| **K06-01** | Giá vốn `0` **hợp lệ** (`CostPrice` amount 0) | unit/integration | Tạo + duyệt thành công; `GetApprovedCostAsync` trả `AmountValue=0`, `CoGiaVon=true` (**không** coi là thiếu) | `CostPrice.cs:85-87`; `D-177` ⑥; `D-181` ③ |
| **K06-02** | **Thiếu dòng đã duyệt** hiệu lực tại ngày bán | app/integration | `GetApprovedCostAsync` = `null` ⇒ fact `NULL` + `CostMissing=true`; **tuyệt đối không `0`** | `D-177` ④⑥ |
| **K06-03** | Dòng **chưa duyệt** (`DaDuyet=false`) không được dùng | unit | trả `null` (không trả giá trị dòng đó) | `D-177` ④; `CostPrice.cs:62-68` |
| **K06-04** | **Nhập lùi ngày**: `ChoNhapLuiNgay=false` | unit/app | kỳ **đã duyệt** không đổi; `RowsSkippedApproved++`; bán cũ **giữ số** | `D-177` ③; §5.4 |
| **K06-05** | Nhập lùi ngày `ChoNhapLuiNgay=true` làm **đổi** kỳ đã duyệt | unit/app | **NÉM `0015`** — **không** ghi đè approved; kỳ chưa duyệt cập nhật được | §5.4; `D-318` C6 |
| **K06-06** | Cùng SKU nhập ở **hai kho khác nhau** | unit | **KHÔNG tách** — gộp một bình quân (grain SKU toàn hệ thống, không `WarehouseId`) | `D-318` C9 |
| **K06-07** | Bán **trước** lần nhập đầu | unit/app | không có kỳ ⇒ `null` + `CostMissing` | `D-177` ⑥ |
| **K06-08** | Chạy động cơ **hai lần** cùng khoảng & `trigger`, lần đầu `ThanhCong` | integration | lần 2 **trả run cũ** (không INSERT trùng `RunKey`); cùng `PrcCostPrices` (0 dòng trùng, cùng amount) | §5.6; `D-176` ② tái lập |
| **K06-08b** | Run trước đang `DangChay` / đã `Loi` | integration | `DangChay` ⇒ **409/bận** (không trả run chưa xong); `Loi` ⇒ **retry attempt mới** (cùng `RunKey`, `Attempt++`) | §5.2; §5.6 |
| **K06-09** | Nhập kho **thiếu `UnitPrice`** | unit | từ mốc đó **không INSERT kỳ** (avg null); lookup ngày đó trả `null`; **không** bịa `0` | §5.5; `D-177` ⑥ |
| **K06-10** | **Chuyển kho** (TransferOut+TransferIn) cùng SKU | unit | net `0` toàn hệ thống ⇒ **không** đổi bình quân | `D-318` C9; §3.3 |
| **K06-11** | Setting `PhuongPhapGiaVon != BinhQuanGiaQuyenDiDong` (vd `FIFO`) hoặc `GiaVonTheoKho=true` | startup | động cơ **NÉM `0014`** khi khởi động/chạy (fail-closed), **không** rơi về mặc định | `D-177` ②; `D-318` C9 |
| **K06-12** | **Toàn `Money`**: 0 `double`, 0 so `decimal` bằng `==`; làm tròn chỉ qua `IRoundingPolicy` | kiến trúc (quét mã) | 0 match `double`/`float`; 0 `==` trên tiền; không `Math.Round` | `D-318` C5 (iv); `PRC-001` NFR-004 |
| **K06-13** | Dòng đã duyệt **bất biến** khi tính lại | integration | bán kỳ cũ chạy lại job ⇒ **cùng số**; amount approved không bị UPDATE ngầm | `D-177` ③; `D-181` |

> 📌 K-06 phải có ≥ 1 ca **đỏ-khi-đột-biến** (negative control) — vd bỏ lọc `DaDuyet` ⇒ K06-03 đỏ; bỏ `costMissing` ⇒ K06-02/09 đỏ.

---

## 8. Rủi ro + câu hỏi mở

### 8.1 Rủi ro

| # | Rủi ro | Mức | Xử lý / đường lùi |
|---|---|---|---|
| **R1** | **Ghi đè dòng đã duyệt** khi tính lại (nhập lùi / sửa `UnitPrice` cũ) làm **số kỳ cũ đổi** mà không ai biết — đúng lỗi `D-177` ③ chống | 🔴 Cao | `ChoNhapLuiNgay=false` mặc định + không UPDATE dòng `DaDuyet=true` (§5.4); K06-04/13; khi cần revision ⇒ `PRC-001-AQ2` (cần người duyệt phá huỷ nếu đổi unique) |
| **R2** | **`StockDocumentLine.UnitPrice` không có cột tiền tệ** (as-built `numeric(18,2)`) ⇒ nhập ngoại tệ bị đọc như VND (sai ~25.000 lần) | 🟠 Trung bình | §3.3 `TienTeNhap` null ⇒ mặc định VND của tenant + cảnh báo; **không** tự quy đổi (`XC-001`); `PRC-001-AQ3` chốt |
| **R3** | **Đơn giá nhập chỉ 2 chữ số** (`18,2`) trong khi tiền `18,4` ⇒ sai số tích luỹ trên số lượng lẻ | 🟡 Thấp | Đầu vào giữ nguyên precision; kết quả bình quân lưu `18,4`; làm tròn chỉ ở biên ra qua `IRoundingPolicy`; ghi rõ, không đổi cột (D-09) |
| **R4** | **Loại chuyển động tính vào định giá** (CustomerReturn/StocktakeGain/WarrantyHold…) chưa chốt ⇒ bình quân lệch | 🟠 Trung bình | Bảng ánh xạ §3.3 là **đề xuất**; `PRC-001-AQ3` chốt; lớp ánh xạ ở **adapter** ⇒ đổi 1 tệp, 0 migration |
| **R5** | **F3 `XC-002` chưa có** (`IIdempotencyGuard`/`InboxRecords`) ⇒ idempotency command dựa unique DB | 🟠 Trung bình | Unique filtered + run table v1; thay lớp khoá khi `XC-002` xong, 0 schema |
| **R6** | **Provider Setting trong assembly module không được ABP quét** ⇒ `GiaVonDongCo`/`ChoNhapLuiNgay` bị bỏ qua im lặng | 🟠 Trung bình | `W17-2` **khởi động Host thật** đọc Setting (tiền lệ `PRC-002-arch.md` R8); đường lùi: chuyển provider sang `src/eCommerce.Domain/Settings/` (app → module hợp lệ) |
| **R7** | **Động cơ O(N) replay toàn bộ** mỗi lần chạy ⇒ chậm khi dữ liệu lớn | 🟡 Thấp | v1 chấp nhận (dữ liệu nhỏ); giới hạn khoảng `[from,to]`; tối ưu incremental (bảng cursor) là additive sau (không đổi hợp đồng) |
| **R8** | **Khoá phân tán tên chứa tiền tố** sai quy ước `XC-003` (`PRC-001` **§15.2 F5** tái phạm; §8.2 đang mâu thuẫn) | 🟡 Thấp | Hàm **MỚI** `PricingLockKeys.GiaVonTheoSku(tenantId, sku)` → chuỗi `pricing:cost-sku:{tenant|null}:{sku}` (**KHÔNG** lặp `ecommerce:` — KeyPrefix đã có; `PricingLockKeys` hiện chỉ có `BangGiaTheoPhamVi`); test tên khoá (khuôn `TC-17`) |

### 8.2 Câu hỏi mở (`openQuestions`)

| ID | Câu hỏi | Phương án | Đề xuất | Cần ai | Phần không phụ thuộc làm ngay |
|---|---|---|---|---|---|
| **PRC-001-AQ1** | **`Q4`** — `CostPrice` có **bắt buộc luồng duyệt 2 bước** (nhập → kế toán trưởng duyệt) không? | (a) 2 bước (tạo `DaDuyet=false` → `ApproveAsync`); (b) auto-approve (1 bước) | **(a)** — `DaDuyet`/`Duyet()` đã có, `D-177` ④⑥ đòi dòng đã duyệt; nhưng (b) chỉ đổi **1 chỗ** trong `CreateAsync`, **0 schema** | **Kế toán trưởng (người)** | §3.2/§5.1 viết theo (a); (b) là cờ đổi được |
| **PRC-001-AQ2** | Nhập lùi ngày đụng kỳ **đã duyệt**: có cần **revision** (thay vì từ chối) không? | (a) **từ chối** (NÉM `0015`) — thiết kế hiện tại; (b) revision — **đổi unique** `(TenantId, Sku, EffectiveFrom)` = **phá huỷ** | **(a)** cho GĐ3 (đã là hành vi §5.4, 0 migration); (b) chỉ mở bằng `D-xx` mới + người duyệt | **nguoi** (nếu chọn (b) — thay đổi phá huỷ schema, `D-318` C6) | §5.4/`ChoNhapLuiNgay=false` hiện thực được ngay; (b) **không** làm ở `W17-2` |
| **PRC-001-AQ3** | Loại chuyển động nào tính vào định giá + tiền tệ của đơn giá nhập (as-built `UnitPrice` không có cột tiền tệ) | (a) chỉ `Receipt` (GoodsReceipt) là có giá; còn lại `NhapKhongGia`; tiền tệ = VND tenant; (b) thêm nguồn/tiền tệ riêng | **(a)** — sát `D-177`/`INV-001` (§0 :56-58); tránh suy đoán NCC/ngoại tệ | DEC + BA | Ánh xạ ở adapter (§3.3), đổi 1 tệp |
| **PRC-001-AQ4** | Nhập kho **thiếu `UnitPrice`**: bình quân từ đó **không xác định** hay lấy bình quân hiện tại? | (a) `null` + `CostMissing` (fail-closed); (b) gán theo bình quân hiện tại | **(a)** — `D-177` ⑥ cấm `0`/bịa số | DEC | §5.2/§5.5 theo (a); (b) là đổi hàm thuần |
| **PRC-001-AQ5** | Nhịp chạy động cơ: **event** khi ghi sổ nhập (đồng bộ) hay **job quét** + command on-demand? | (a) job quét + command; (b) ETO mỗi lần `GhiSo` | **(a)** — tránh phụ thuộc outbox/ETO (`XC-002` chưa xong), tất định + idempotent; event là tối ưu sau | DEC | Job/command theo (a); event additive sau |

> ⚠️ **Q4 (`PRC-001-AQ1`) vẫn `Open`** — `D-319` ⑤ nói rõ decider **không** đóng; đây là câu **kế toán**. Không tự chốt.

---

## 9. Đường lùi từng quyết định

| # | Quyết định | Đường lùi (bằng tệp/lệnh) |
|---|---|---|
| 1 | Thêm bảng `PrcCostEngineRuns` + 4 cột `PrcCostPrices` | `git revert <SHA W17-2>` + `dotnet ef migrations remove` (chưa merge) **hoặc** chạy `Down()` sau `pg_dump` (đã áp); hoặc để nguyên + tắt động cơ |
| 2 | Động cơ bật/tắt | Setting `Pricing:GiaVonDongCo=false` ⇒ 0 dòng mới, `PrcCostPrices` cũ nguyên vẹn (**0 commit**) |
| 3 | Phương pháp = BQGGQ di động | Setting `Pricing:PhuongPhapGiaVon`; v1 chỉ một giá trị (khác ⇒ NÉM `0014`); muốn FIFO = `D-xx` mới + code |
| 4 | Đường ghi/duyệt `CostPrice` | Gỡ `ICostPriceAdminAppService` + 2 quyền `.Manage`/`.Approve` + route (1 commit); dòng đã ghi vẫn đọc được |
| 5 | Port `ICostFeedLookup` + adapter | Gỡ 2 tệp + ProjectReference trong `Pricing.Application.csproj`; không đụng Domain Inventory |
| 6 | Ghi đè dòng đã duyệt (nhập lùi) | `Pricing:ChoNhapLuiNgay=false` (mặc định) ⇒ không bao giờ UPDATE approved; `true` chỉ tính lại kỳ **chưa** duyệt, đụng approved ⇒ NÉM `0015`; revision = `PRC-001-AQ2` (người) |
| 7 | Contract đọc nội bộ cho Reporting | Gỡ port trần `ICostPriceLookup` + `CostPriceLookupDto` + `CostPriceLookupAdapter` + `ProjectReference` Reporting→`Pricing.Application.Contracts` (1 commit); Reporting đang gọi sẽ đỏ **có chủ ý** ⇒ không tự gỡ khi còn bên gọi; **0 migration** |
| 8 | Khoá phân tán `PricingLockKeys.GiaVonTheoSku` | Gỡ hàm khoá + lời gọi (1 commit); hàng rào cuối vẫn là unique DB |

---

## 10. Tự kiểm của F2

- Mọi `file:dòng` đối chiếu trong lượt này bằng **truy vấn đồ thị** `codegraph_explore`
  (`CostPrice CostPriceAppService ICostPriceAppService PrcCostPrices PricingDbContextModelCreatingExtensions` ·
  `StockDocument StockDocumentLine StockLedgerEntry StockDocumentAppService InvStockLedger` ·
  `StockDocumentStatus OccurredAtUtc PricingLockKeys TinhTienChungTu` · `IStockDocumentAppService StockDocumentDtos` ·
  `Money Currency PricingPermissions PricingErrorCodes`) + **đọc trực tiếp**: `CostPrice.cs`, `CostPriceAppService.cs`, `IPriceAppServices.cs`,
  `PricingErrorCodes.cs`, `PricingDbContextModelCreatingExtensions.cs`, `PricingDomainModule.cs`, `PricingSettingDefinitionProvider.cs`,
  `StockDocument.cs`, `StockLedger.cs`, `StockDocumentDtos.cs`, `StockDocumentAppService.cs`, `StockReservationGateway.cs`,
  `IStockReservationGateway.cs`, `Enums.cs`, `Enums.Inventory.cs`, `Money.cs`, `Currency.cs`, `TinhTienChungTu.cs`,
  `eCommerceDbContext.cs`, `eCommerce.Pricing.Application.csproj`.
- **Additive-only:** 1 bảng mới + 4 cột nullable + index; **0 DROP/đổi kiểu/`SET NOT NULL`**; **0** chạm `StockLedger`/`InvStockLedger`;
  **0** đổi chữ ký method đang có; enum **mới** (không đổi số cũ); **0** chạm `XC-001`/`RoundingPolicy` — khớp `D-318` C4 (1)(2)(3)(4).
- **Port đọc trần (P1):** `ICostPriceLookup`/`CostPriceLookupAdapter` (không `IApplicationService`, không `[Authorize]`, không endpoint, DTO không tuple);
  `ICostPriceAppService` giữ nguyên `[Authorize]` cho HTTP. Mã lỗi mới `0014`/`0015` append **sau `0011`** (không đụng `0012`/`0013` của PRC-002).
  Kỳ `avg=null` **không INSERT** (AmountValue NOT NULL); `RunKey` trùng ⇒ trả run cũ.
- **Tiền 100% qua `Money`**; 0 `double`; 0 so `decimal` bằng `==`; làm tròn chỉ qua `IRoundingPolicy` — `D-318` C5 (iv).
- **Thiếu giá vốn ⇒ `NULL` + `CostMissing`, cấm `0`**; **giá vốn `0` hợp lệ** — `D-318` C5 (v); **ca K-06** ở §7 (điều kiện C4 (6)/C5 (vi)).
- **Bảng thay đổi schema từng dòng** (§4.1) + **revert bằng tệp/lệnh** (§4.3, §9) — `D-318` C4 (7).
- **Grain = SKU toàn hệ thống**, `PrcCostPrices` **không** `WarehouseId` (§3.3, K06-06) — `D-318` C9.
- **Q4 giữ `Open`**, đưa `PRC-001-AQ1`, không tự chốt — `D-319` ⑤.
- **Không** sửa code/migration/SRS/`decisions.md`/`.claude/**`; chỉ ghi `docs/features/pricing/PRC-001-arch.md`.
