# `PRC-002` — Kiến trúc phần còn thiếu của WP-16 (4 hạng mục)

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `PRC-002` (phần `arch`) |
| **Module chủ** | **Pricing** — `modules/Pricing/**` |
| **Phạm vi** | `WP-16`, hàng `W16-1` (F0–F2b) của chuỗi `gd3-1` — **chỉ 4 hạng mục** đã chốt |
| **Quyết định nguồn** | `D-218` ① (danh sách VÀO/RA) · `D-219` (guard tổng đơn âm) — `docs/nhanh/decisions.md:232-233` |
| **SRS** | `docs/features/pricing/prc-002-gia-bac-thang-pipeline-tinh-gia.md` (§4.2, §6.1, §9, §11, §12, §13, §15, §16.4, §17.3, §18.3, §19.3, §20.3, §20.4, §21.3) · `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` §20.4 |
| **Schema** | **0 migration** — setting là dữ liệu cấu hình, bảng `AbpSettings` đã tồn tại |
| **Hợp đồng công khai** | Không đổi DTO/route. `GioTinhTienDto` và `IPriceCalculationAppService` **giữ nguyên chữ ký** |
| **FE** | **Không có** phần FE cho 4 hạng mục (lý do ở §6.1) |

> 📄 **Loại tài liệu:** `-arch` là **tài liệu bổ trợ**, **ngoài** mẫu 16 mục / BR / mục "Đối chiếu mô hình Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó.
> Vì vậy việc tệp này không có `## 0.` / `## 10.`–`## 15.` là **đúng chủng loại**, không phải thiếu mục (ORCH chốt 25/11/2026).

> 📌 **Ngoài phạm vi, xem `D-218` ①:** B4 điều chỉnh/khuyến mãi (`PRO-*`) · `CR-P1` (overload `TinhTienDon` nhận giảm theo dòng) ·
> phân trang `lich-su-giai-trinh` · lọc `ChannelPrice` ở SQL · đường ghi/duyệt giá vốn (`WP-17`) · job lãi gộp (`REP-002`) ·
> menu `/GiaVon` (`WEB-001`) · "giá shop vs bậc thang" (§18.3 #1 — giữ nguyên hành vi đã có test). **Không thiết kế ở đây.**

---

## 1. Bối cảnh

`PRC-002` đã chạy được pipeline B1→B8 (`PriceCalculationAppService` — `modules/Pricing/eCommerce.Pricing.Application/PriceCalculationAppService.cs:81-254`)
nhưng còn **4 chỗ lệch giữa "đã chốt" và "đang chạy"** (đúng danh sách `D-218` ①):

| # | Hiện trạng (đo được) | Đường dẫn | Hệ quả |
|---|---|---|---|
| 1 | Chính sách làm tròn + thiếu giá **cứng trong code** ngay tại B1 | `PriceCalculationAppService.cs:84-88` (dựng `ChinhSachSnapshot` bằng `ChinhSachLamTronMacDinh` + `ChinhSachThieuGia.NemLoi`); `:256-257` (`static readonly` policy) | Đổi chính sách phải **sửa mã + deploy**; kế toán không tự chuyển được; trái `prc-002` §18.3 #5, §19.3 #4, §21.3 #3 |
| 2 | **Không có ngưỡng số bậc/bộ** | `modules/Pricing/eCommerce.Pricing.Application/TierPriceSetAppService.cs:87-120` — `AddTierAsync` chỉ chặn trùng `MinQuantity` (`:98-108`), không đếm số bậc | Một bộ có thể phình vô hạn ⇒ mỗi lần tính giá phải nạp/duyệt nhiều bậc hơn giả định `prc-002` §15.3 #2; nợ ở §16.4 #4, §19.3 #3, §21.3 #4 |
| 3 | **Tổng đơn có thể ÂM, không guard** | `modules/Pricing/eCommerce.Pricing.Domain/TinhTienDon.cs:139-146` (tổng = Σ dòng, không kiểm dấu); hiện trạng được **ghi bằng test** `test/eCommerce.Tests/Pricing/TinhTienDonTests.cs:179-204` (giỏ 3 đồng, giảm 10.000, phí 5.000 ⇒ `-4.997`) | Nếu tầng thanh toán/đối soát tin con số này thì **tiền đi ra** khỏi hệ thống mà không chặn ở đâu (§20.3) |
| 4 | Bốn unique index `NULLS NOT DISTINCT` của Pricing **chưa có tên** trong danh sách đích danh | `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs:170-177` và `:325-328` (nợ ghi rõ); vòng kiểm `:394-400` so **tiền tố** tên index | Test chỉ đếm **tổng** (`:208-209`) ⇒ thêm 1 index + hỏng 1 index cũ thì tổng **không đổi** ⇒ test xanh vô nghĩa (đúng lý do danh sách tồn tại) |

**Mục tiêu kiến trúc:** cả 4 hạng mục đi qua **cấu hình + mã lỗi append + test** — **0 bảng mới, 0 cột mới, 0 đổi kiểu, 0 đổi unique index**
(`D-218` ② T6). Thiết kế phải giữ: (a) module Pricing **không** phụ thuộc Host; (b) hàm tính tiền **thuần, không I/O**;
(c) mỗi hạng mục có **đường lùi bằng một giá trị cấu hình hoặc một commit**.

---

## 2. Ranh giới

### 2.1 Chiều phụ thuộc (một chiều, không đổi)

```
Host (eCommerce.HttpApi.Host)
  └─ eCommerce.Application  ──────────────►  Pricing.Application.Contracts   (Sales, Checkout dùng IPriceCalculationAppService)
        └─ Pricing.Application  ─────────►  Pricing.Domain  ─────────►  eCommerce.Kernel (Money, ChinhSach)
                                                        └────────────►  Volo.Abp.Settings (transitive, KHÔNG thêm package)
```

- `Pricing.Domain` hiện `[DependsOn(typeof(AbpDddDomainModule), typeof(eCommerce.Kernel.eCommerceKernelModule))]`
  — `modules/Pricing/eCommerce.Pricing.Domain/PricingDomainModule.cs:18`; `Pricing.Application` phụ thuộc
  `PricingDomainModule` — `modules/Pricing/eCommerce.Pricing.Application/PricingApplicationModule.cs:11-15`.
- **Không thêm phụ thuộc module nào.** Bốn hạng mục chỉ dùng: ABP Setting (hạ tầng), `Money`/`IRoundingPolicy` (Kernel),
  hằng số + entity **đã có** của Pricing. Pricing **không** đọc Catalog / Tax / Orders (giữ nguyên `PRC-001` Q1).
- `Sales` → `Pricing.Application.Contracts` **không đổi** (`modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:41`,
  `:175`; `DonHangAppService.cs:36`). Không có vòng phụ thuộc ngược.
- `Volo.Abp.Settings` **đã có** trong đồ thị của `Pricing.Domain` qua `Volo.Abp.Ddd.Domain`
  (`modules/Pricing/eCommerce.Pricing.Domain/eCommerce.Pricing.Domain.csproj:13`, package `Volo.Abp.Settings/10.7.0` có trong
  `obj/project.assets.json` của project) ⇒ **không sửa `Directory.Packages.props`** (M11).

### 2.2 Nơi khai báo ABP Setting của module Pricing — **quyết định của F2**

**Chọn: `modules/Pricing/eCommerce.Pricing.Domain/PricingSettingDefinitionProvider.cs`** (project Domain của module).

Lý do (theo thứ tự loại trừ):

| Phương án | Kết luận |
|---|---|
| `src/eCommerce.Domain/Settings/*` (nơi provider hiện có — `src/eCommerce.Domain/Settings/ChinhSachDoiSoatVaHanMuc.cs:5-33`) | ❌ Đây là project **app (Host)**. Khai ở đó thì **module không sở hữu hợp đồng cấu hình của mình**; muốn thêm/đổi setting phải sửa app — đúng chiều `app → module` nhưng làm hợp đồng cấu hình của module nằm ngoài module. Chỉ dùng làm **đường lùi kỹ thuật** nếu ABP không quét provider trong assembly module (kiểm bằng khởi động Host, §7 R8) |
| `Pricing.Application` | ❌ Provider là **định nghĩa cấu hình**, thuộc tầng Domain; Application phụ thuộc Domain, đặt ở Domain thì module nào dùng lại được mà không cần Application |
| `Pricing.Application.Contracts` | ❌ Contracts là bề mặt gọi chéo module; provider là đăng ký nội bộ, không phải hợp đồng DTO |
| `eCommerce.Kernel` | ❌ Kernel là thư viện **không có ABP module riêng cho setting của từng module**, và reader phải trả kiểu **của Pricing** (`ChinhSachThieuGia`) ⇒ đặt ở Kernel sẽ **đảo chiều phụ thuộc** (`Kernel → Pricing`) |
| **`Pricing.Domain`** | ✅ Module sở hữu định nghĩa + giá trị mặc định; ABP tự đăng ký theo **quy ước** (tiền lệ trong repo: `ChinhSachSettingDefinitionProvider` chạy được mà **không** có dòng đăng ký thủ công nào; ghi chú quy ước ở `shared/eCommerce.Kernel/Settings/ChinhSach.cs:225-229`) |

**Chữ ký lớp** (tên + kiểu + mặc định + phạm vi):

```csharp
using Volo.Abp.Settings;

namespace eCommerce.Pricing;

/// Khai báo 4 Setting của module Pricing (WP-16). ABP tự đăng ký theo quy ước.
public class PricingSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // Phạm vi: Application (giá trị host-level, đường ghi sẵn có: ISettingManager.SetGlobalAsync)
        //          + Tenant (D-218: "cấu hình theo tenant").
        // 📌 ABP 10.7 KHÔNG còn enum `SettingScopes` và ctor `SettingDefinition` không có tham số `scopes`;
        //    phạm vi khai qua `WithProviders(...)`.
        var phamVi = new[]
        {
            DefaultValueSettingValueProvider.ProviderName,
            ConfigurationSettingValueProvider.ProviderName,
            GlobalSettingValueProvider.ProviderName,
            TenantSettingValueProvider.ProviderName
        };

        context.Add(
            new SettingDefinition(PRC002SettingTenKhoa.ChinhSachLamTron,  "half-away-from-zero").WithProviders(phamVi),
            new SettingDefinition(PRC002SettingTenKhoa.ThieuGia,          "NemLoi").WithProviders(phamVi),
            new SettingDefinition(PRC002SettingTenKhoa.SoBacToiDa,        "20").WithProviders(phamVi),
            new SettingDefinition(PRC002SettingTenKhoa.TongDonAm,         "Reject").WithProviders(phamVi));
    }
}
```

📌 **Phạm vi tương đương `Application | Tenant`:** bốn provider trên gồm `DefaultValue` (giá trị mặc định trong code), `Configuration`
(`appsettings`), `Global` (host-level, ghi qua `SetGlobalAsync`) và `Tenant` — tức đúng tập **host-level + theo tenant**, **loại trừ
`User`**. Muốn cho phép ghi đè theo **từng người dùng** thì phải nói rõ và thêm `UserSettingValueProvider` — **WP-16 KHÔNG cho** để
chính sách giá bị đổi theo user. Cột "Tenant-scoped" ở bảng dưới giữ nguyên `Application \| Tenant` (✅).

| Khoá setting | Kiểu giá trị (chuỗi, `InvariantCulture`) | Mặc định | Hợp lệ | Tenant-scoped |
|---|---|---|---|---|
| `Pricing:RoundingPolicyCode` | chuỗi mã policy | `half-away-from-zero` | `half-away-from-zero` (allowlist `none` — **AQ1**) | ✅ (`Application \| Tenant`) |
| `Pricing:MissingPricePolicy` | chuỗi tên enum | `NemLoi` | `NemLoi` · `DungGiaNiemYet` | ✅ |
| `Pricing:MaxTiersPerSet` | số nguyên dạng chuỗi | `20` | số nguyên `≥ 1` (0/âm/không phải số ⇒ **mặc định 20**, AQ4) | ✅ |
| `Pricing:OrderTotalNegativePolicy` | chuỗi tên enum | `Reject` | `Reject` · `Clamp` | ✅ |

📌 **Vì sao test kiến trúc không soi cả assembly:** assembly `Pricing.Domain` **chứa** lớp provider (`using Volo.Abp.Settings`) ⇒ câu
"assembly không tham chiếu `Volo.Abp.Settings`" **sai theo thiết kế**; bất biến cần giữ là **bề mặt các kiểu hàm thuần** không nhận/không lộ
kiểu setting (§3.1, test 3d).

📌 **Tên khoá nằm ở đâu:** đặt hằng số tên trong file **của module** (`PRC002SettingTenKhoa`), **không** nhét vào
`TenChinhSach` của Kernel (`shared/eCommerce.Kernel/Settings/ChinhSach.cs:19-62`) — vì hàm đọc giá trị phải trả kiểu
**của Pricing** (`ChinhSachThieuGia`, `ChinhSachTongDonAm`, `IRoundingPolicy`), đặt ở Kernel sẽ tạo phụ thuộc ngược.
`TenChinhSach` đang là sổ tên của các chủ đề **không phụ thuộc module** (Recon/Inv/Orders); mở rộng nó cho Pricing là
bước đảo chiều **không cần thiết**. 📌 Hệ quả chấp nhận được: test `test/eCommerce.Tests/Settings/ChinhSachTests.cs:180-195`
(sổ tên Kernel) **không** bao gồm 4 khoá Pricing — W16-2 thêm test tương đương trong project test của Pricing (§6.3 mục 1).

### 2.3 Vị trí hàm đọc thuần

`modules/Pricing/eCommerce.Pricing.Domain/PricingChinhSach.cs` — **hàm THUẦN**, không I/O, test được không cần DB
(cùng mô hình `ChinhSach` của Kernel — `shared/eCommerce.Kernel/Settings/ChinhSach.cs:121-223`):

```csharp
namespace eCommerce.Pricing;

public static class PRC002SettingTenKhoa
{
    public const string ChinhSachLamTron = "Pricing:RoundingPolicyCode";
    public const string ThieuGia         = "Pricing:MissingPricePolicy";
    public const string SoBacToiDa       = "Pricing:MaxTiersPerSet";
    public const string TongDonAm        = "Pricing:OrderTotalNegativePolicy";
}

/// Chính sách khi tổng đơn âm — enum APPEND CUỐI DẢI (D-219).
public enum ChinhSachTongDonAm
{
    Reject = 0,   // mặc định: NÉM eCommerce:Pricing:0010
    Clamp  = 1    // kẹp giảm cấp đơn về không vượt thành tiền
}

public static class PricingChinhSach
{
    public const int SoBacToiDaMacDinh = 20;
    public static readonly string[] ChinhSachLamTronHopLe  = { "half-away-from-zero" };
    public static readonly string[] ThieuGiaHopLe          = { nameof(ChinhSachThieuGia.NemLoi), nameof(ChinhSachThieuGia.DungGiaNiemYet) };
    public static readonly string[] TongDonAmHopLe         = { nameof(ChinhSachTongDonAm.Reject), nameof(ChinhSachTongDonAm.Clamp) };

    public static IRoundingPolicy DocChinhSachLamTron(string? tho, out bool hopLe);   // "half-away-from-zero" ⇒ new RoundHalfAwayFromZeroPolicy()
    public static ChinhSachThieuGia DocThieuGia(string? tho, out bool hopLe);         // mặc định NemLoi
    public static int DocSoBacToiDa(string? tho, out bool hopLe);                     // dùng ChinhSach.DocSo (chỉ dấu CHẤM, không dấu phẩy)
    public static ChinhSachTongDonAm DocTongDonAm(string? tho, out bool hopLe);       // mặc định Reject
}
```

- Nguyên tắc **"dữ liệu cấu hình cũng là dữ liệu vào"** (tiền lệ `ChinhSach.cs:114-120`): giá trị lạ/âm/rỗng ⇒ **không ném**,
  trả **mặc định an toàn** + `hopLe = false` để chỗ gọi **ghi cảnh báo**. Riêng đường **ghi** (nếu sau này có app service) phải **NÉM**
  — tiền lệ `src/eCommerce.Application/Inventory/ChinhSachHanMucAppService.cs:118-124`.
- Số bậc dùng `eCommerce.Settings.ChinhSach.DocSo` của Kernel (`shared/eCommerce.Kernel/Settings/ChinhSach.cs:192-207`; tệp nằm ở
  `Kernel/Settings` nhưng **namespace là `eCommerce.Settings`**) để **không** nhận dấu phẩy thập phân
  (`"0,5"` từng bị đọc thành `5`) và **không** nhận số âm.

---

## 3. Hợp đồng

### 3.1 `ChinhSachSnapshot` — mở rộng **additive**, không phá bên gọi

Hiện tại: `modules/Pricing/eCommerce.Pricing.Domain/ChonDonGiaCuoi.cs:11-15` (4 tham số). Chỉ **một** nơi dựng:
`PriceCalculationAppService.cs:84-88`. Thay đổi: thêm **một** tham số có **giá trị mặc định** ⇒ mã nguồn cũ vẫn biên dịch,
không đổi tên/kiểu trường cũ:

```csharp
public sealed record ChinhSachSnapshot(
    string MaChinhSachLamTron,
    IRoundingPolicy ChinhSachLamTron,
    ChinhSachThieuGia ChinhSachThieuGia,
    DateTime At,
    ChinhSachTongDonAm ChinhSachTongDonAm = ChinhSachTongDonAm.Reject);   // 🆕 WP-16
```

- **Ai dựng:** `PriceCalculationAppService.TinhMoiAsync` qua **một hàm private** `TaoChinhSachSnapshotAsync()` — đọc **đúng một lần**
  ở B1, **trước** mọi bước nạp DB, rồi truyền xuyên suốt (ADR-08; `prc-002` §4.2 B1 — dòng 236). **Không** tạo interface/DTO mới:
  chưa có bên gọi thứ hai (luật "hợp đồng phải có bên gọi thật").
- **Đọc ở đâu:** thêm `ISettingProvider` vào ctor `PriceCalculationAppService` — tiền lệ `ChinhSachHanMucAppService.cs:49-50,57`
  (không dựa vào thuộc tính tiện ích của `ApplicationService` để tránh phụ thuộc phiên bản ABP).
- **Ba lần đọc trong một lần tính** (`RoundingPolicyCode`, `MissingPricePolicy`, `OrderTotalNegativePolicy`). `ISettingProvider` có cache
  (cache trong tiến trình + distributed) ⇒ sau lần đọc đầu **không** phát sinh lệnh EF trên mỗi giỏ; W16-4 **đo lại** số lệnh EF
  (NFR-PRC-002-002: **≤ 4 lệnh/giỏ**) và ghi số đo, không suy đoán.
- **Cảnh báo giá trị lạ:** ghi `LogWarning` kèm `hopLe` của từng khoá + một dòng `[CHINH-SACH-DA-DOC]` ghi **mã đang áp** — để vận hành
  thấy *"tôi đổi cấu hình mà hệ thống vẫn chạy giá trị cũ"* (đúng lý do `SettingKhongHopLe` tồn tại ở
  `src/eCommerce.Application/Inventory/ChinhSachHanMucAppService.cs:95-112`).
- **Bất biến:** `TinhTienDon`/`ChonBacThang`/`GiaCoSoResolver`/`PricingChinhSach` **không** được biết `ISettingProvider` — chính sách **chỉ** đi vào qua
  tham số. W16-2 thêm test kiến trúc soi **bề mặt công khai của bốn kiểu hàm thuần đó**: không tham số, field hay property nào có kiểu
  `ISettingProvider`/`ISettingManager` (tiền lệ reflection: `test/eCommerce.Tests/CrossCutting/SalesModuleKienTrucTests.cs:74`).
  📌 **Không** khẳng định "cả assembly `eCommerce.Pricing.Domain` không tham chiếu `Volo.Abp.Settings`": assembly **chứa** provider (§2.2)
  nên khẳng định đó **sai theo thiết kế**; còn soi `PackageReference` tường minh trong csproj thì xanh nhưng **rỗng** (gói `Volo.Abp.Settings`
  vào qua đường bắc cầu `Volo.Abp.Ddd.Domain`) ⇒ không bắt được R5.

### 3.2 `TinhTienDon` — overload **additive** (D-219)

`modules/Pricing/eCommerce.Pricing.Domain/TinhTienDon.cs:73-78` — **giữ nguyên** chữ ký cũ (uỷ quyền `Reject`), thêm overload:

```csharp
// Chữ ký CŨ — giữ nguyên tuyệt đối; XML doc ghi "uỷ quyền cho Reject".
public static KetQuaTinhTien Tinh(
    IReadOnlyList<DongCanTinh> cacDong, Money giamGiaCapDon, Money phiCapDon, IRoundingPolicy chinhSach)
    => Tinh(cacDong, giamGiaCapDon, phiCapDon, chinhSach, ChinhSachTongDonAm.Reject);

// Overload MỚI.
public static KetQuaTinhTien Tinh(
    IReadOnlyList<DongCanTinh> cacDong, Money giamGiaCapDon, Money phiCapDon,
    IRoundingPolicy chinhSach, ChinhSachTongDonAm chinhSachTongDon);
```

Hành vi (đúng `D-219` ①):

| Chính sách | Điều kiện | Hành vi |
|---|---|---|
| `Reject` (mặc định) | tổng `< 0` | **NÉM** `BusinessException(PricingErrorCodes.TongDonAm)` kèm `WithData("tong")`, `WithData("giamGiaCapDon")`, `WithData("phi")` |
| `Reject` | tổng `= 0` | **HỢP LỆ** (đơn 0 đồng có phí không bị chặn) |
| `Clamp` | giảm cấp đơn `>` thành tiền (`Σ đơn giá × SL`) | **kẹp** `giamGiaHieuLuc = thành tiền` **trước** khi phân bổ R2 ⇒ tổng `= phí + thuế ≥ 0`; kết quả mang `BiKep = true`, `SoKep = 1` |
| `Clamp` | không vượt | như cũ, `BiKep = false`, `SoKep = 0` |

- **Vị trí kẹp:** ngay sau khi tính `tongTruocGiam` (`TinhTienDon.cs:112`) và **trước** `PhanBoVaTron`
  (`:114-118`) — nếu kẹp sau phân bổ thì `Σ phần` không còn khớp khoản gốc R2/R3, và thuế R4 tính trên số **chưa kẹp**
  ⇒ hoá đơn tự mâu thuẫn.
- **Tín hiệu "đã kẹp"** mang qua **kết quả**: `KetQuaTinhTien` (`TinhTienDon.cs:27-38`) thêm hai thuộc tính **init-only** (không đổi
  primary constructor — cùng kiểu `SoDong` ở `:37`):
  ```csharp
  public bool BiKep { get; init; }
  public int  SoKep { get; init; }
  ```
  Chỗ gọi (`PriceCalculationAppService`) `LogWarning` kèm `biKep`/`soKep`/giảm-giá-yêu-cầu/giảm-giá-thực-tế.
  📌 **Diễn giải `D-219`:** `WithData(...)` chỉ tồn tại trên `BusinessException`; `Clamp` **không** ném ⇒ dữ liệu `biKep/soKep`
  được mang **qua kết quả + log** thay vì exception, giữ `TinhTienDon` **thuần không I/O** (yêu cầu của F2). Xác nhận lại: **AQ3**.
- **`KetQuaTinhTien` là record nội bộ module** — không ra API. Không đổi `GioTinhTienDto`
  (`modules/Pricing/eCommerce.Pricing.Application.Contracts/PriceCalculationDtos.cs:103-122`) ⇒ **không** phá FE, **không** đổi `schema.d.ts`.

### 3.3 `TierPriceSetAppService` — ngưỡng số bậc

- Ctor thêm `ISettingProvider` (`TierPriceSetAppService.cs:26-32`).
- Trong `AddTierAsync` (`:87-120`), **sau** `bo.YeuCauChoPhepSuaNoiDung()` (`:92`) và **sau** guard trùng `MinQuantity`
  (`:98-108` — giữ nguyên thứ tự để không đổi hành vi lỗi hiện có):
  1. đọc `Pricing:MaxTiersPerSet` qua `PricingChinhSach.DocSoBacToiDa`;
  2. `soHienTai = await _bacRepository.CountAsync(x => x.TierPriceSetId == tierPriceSetId)`;
  3. `soHienTai + 1 > toiDa` ⇒ **NÉM** `BusinessException(PricingErrorCodes.VuotSoBacToiDa)` kèm
     `tierPriceSetId`, `soBacHienTai`, `toiDa`, `lyDo`.
- **Không** đổi unique index nào (`D-218` ① hạng mục 2). **Không** thêm khoá phân tán: hai người thêm bậc **đồng thời**
  có thể vượt ngưỡng **1 bậc** — ngưỡng là **guard vận hành**, không phải bất biến dữ liệu (§7 R4).
- **Hạ ngưỡng không hồi tố:** bộ đã có nhiều hơn ngưỡng vẫn `Publish`/`Activate` được (giữ đúng phạm vi `AddTierAsync` mà
  `D-218` chỉ định); ghi rõ để vận hành không tưởng là bug.
- Đường **ghi** cấu hình: dùng endpoint `setting-management/*` của ABP (Host đã nạp — `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:73`,
  ghi qua `ISettingManager` tự xoá cache 2 tầng). Pricing **không** thêm app service cấu hình trong WP-16 (AQ2).

### 3.4 Mã lỗi — append cuối dải

`modules/Pricing/eCommerce.Pricing.Domain/PricingErrorCodes.cs` (dải hiện tới `0009` — dòng 46):

| Hằng số | Giá trị | Nguồn | Dùng ở |
|---|---|---|---|
| `TongDonAm` | `eCommerce:Pricing:0010` | `D-219` | `TinhTienDon.Tinh` (nhánh `Reject`) |
| `VuotSoBacToiDa` | `eCommerce:Pricing:0011` | `D-218` ① (2) | `TierPriceSetAppService.AddTierAsync` |

- **Cách đăng ký:** không có sổ đăng ký trung tâm — mã là `const string` dùng trực tiếp trong `BusinessException`
  (`PricingErrorCodes.cs:11-47`). **Không** khai lại ở `eCommerceConsts` (lý do đã ghi ở `:3-10`).
- **Test bất biến dải mã:** hiện **chưa** có test dải mã cho Pricing (chỉ có một khẳng định tiền tố:
  `test/eCommerce.Tests/Pricing/CostPriceTests.cs:203`). W16-2 thêm test reflection: mọi `const string` của
  `PricingErrorCodes` (a) bắt đầu `eCommerce:Pricing:`, (b) **không trùng**, (c) hậu tố 4 chữ số **tăng dần, không nhảy trong nội bộ
  dải đã dùng** ⇒ chặn việc cấp lại số cũ.

### 3.5 Hợp đồng **không** đổi (ghi rõ để review đối chiếu)

| Bề mặt | Trạng thái | Bằng chứng |
|---|---|---|
| `IPriceCalculationAppService` (`TinhGioAsync`, `GetSoGiaiTrinhAsync`, `GetLichSuGiaiTrinhAsync`) | **không đổi** | `modules/Pricing/eCommerce.Pricing.Application.Contracts/IPriceAppServices.cs:81-99` |
| `TinhGioInput` / `GioTinhTienDto` | **không đổi** (không thêm trường ⇒ `pnpm gen:api` **không** đổi `schema.d.ts`) | `PriceCalculationDtos.cs:5-60,103-122` |
| Route (conventional controllers) | **không đổi / không thêm** | `PricingApplicationModule.cs:6-9` |
| `ITierPriceSetAppService` | **không đổi** chữ ký (chỉ thêm hành vi ném `0011`) | `TierPriceDtos.cs` (khai báo `AddTierAsync` tại `:95`) |
| `PriceCalculationRecord` (cột/DB) | **không đổi** (mã chính sách làm tròn đã có cột `RoundingPolicyCode`) | `modules/Pricing/eCommerce.Pricing.EntityFrameworkCore/PricingDbContextModelCreatingExtensions.cs:189,203-205` |

> ⚠️ **Thay đổi hành vi có chủ ý** (không phải phá hợp đồng nhưng phải nói với bên gọi): với giá trị mặc định mới, `TinhGioAsync`
  **ném `0010`** cho giỏ có giảm giá vượt thành tiền (trước đây trả tổng âm). `Sales` (`CheckoutAppService.cs:175`,
  `DonHangAppService.cs:36`) sẽ nhận `BusinessException` thay vì số âm — đúng mục tiêu `D-219`; FE/handler cần hiển thị mã lỗi (không
  cần sửa DTO).

---

## 4. Schema + migration

**Kết luận: 0 migration, 0 thay đổi schema, 0 `pg_dump`.** (Bảng W16-2 trong `docs/agents/bang-phien-gd3-1.md:51` có nhắc
"migration (expand-only, pg_dump trước khi áp)" — với WP-16 điều đó **không áp dụng**; `D-218` ② T6 đã chốt "0 migration".)

| Thành phần | Trạng thái | Bằng chứng |
|---|---|---|
| Bảng `AbpSettings` (lưu giá trị theo `ProviderName/ProviderKey` = `T`/`tenantId`) | **đã tồn tại** | migration `src/eCommerce.EntityFrameworkCore/Migrations/20261009102627_Added_Phase0_AuthModules.cs:226` + index `:535`; đăng ký `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs:369` (`ConfigureSettingManagement()`) |
| Bảng định nghĩa setting | không cần dòng DB — **định nghĩa nằm trong code** (provider), giá trị mặc định lấy từ `SettingDefinition.DefaultValue` | ghi chú có thật: `shared/eCommerce.Kernel/Settings/ChinhSach.cs:17`, `:225-229` |
| Bảng/DbContext của Pricing (`PrcPriceLists`, `PrcPriceListItems`, `PrcChannelPrices`, `PrcTierPriceSets`, `PrcTierPrices`, `PrcPriceCalculationRecords`, `PrcCostPrices`) | **không đổi** cột/index | `PricingDbContextModelCreatingExtensions.cs:43-45,65-67,89-91,150-173,189,203-205` |
| Unique index `NULLS NOT DISTINCT` của Pricing | **không đổi số lượng** (nội dung NND đã có, W16 chỉ **kiểm** bằng tên) | `20261015101649_Pricing_PriceList_And_ChannelPrice.cs:147-160`; `20261015110229_Prc002_PriceCalculationRecords.cs:66-69` |

- **Dữ liệu ghi khi chạy** (giá trị `AbpSettings` theo tenant) là **dữ liệu cấu hình**, không phải schema; K-01 (pg_dump trước migration
  chạm DB có dữ liệu) **không kích hoạt** vì không có migration. Không có thao tác phá huỷ nào ⇒ không cần người duyệt phá huỷ.
- Nếu (ngoài dự kiến) W16-2 buộc phải sinh migration: **dừng, báo lại decider** (`D-218` ⑤) — một chủ migration, expand-only, `pg_dump`.

---

## 5. Luồng

### 5.1 Tính giá (B1→B8) — sau WP-16

```
POST /api/app/price-calculation/tinh-gio
  └─ TinhGioAsync                       (PriceCalculationAppService.cs:53-79)
       ├─ tra sổ giải trình theo IdempotencyKey (KHÔNG đọc setting — replay giữ nguyên kết quả cũ :67-76)
       └─ TinhMoiAsync                  (:81-254)
            ├─ B1  TaoChinhSachSnapshotAsync()   ← ĐỌC 1 LẦN 3 Setting (mới)
            │       Pricing:RoundingPolicyCode   → IRoundingPolicy (mặc định half-away-from-zero)
            │       Pricing:MissingPricePolicy   → ChinhSachThieuGia (mặc định NemLoi)
            │       Pricing:OrderTotalNegativePolicy → ChinhSachTongDonAm (mặc định Reject)
            │       (khoá `At` giữ nguyên :88)
            ├─ B2  nạp 5 lô dữ liệu + GiaCoSoResolver.Giai(...)      (:95-167)
            ├─ B3  ChonBacThangTheoPhamVi(...)                       (:169-171)
            ├─ B5  ChonDonGiaCuoi.Chon(...)                          (:173-175)
            ├─ B6–B8 TinhTienDon.Tinh(..., chinhSach.ChinhSachTongDonAm)   (mới — :199-203)
            │       Reject: tổng < 0 ⇒ NÉM 0010 (kèm tong/giamGiaCapDon/phi)
            │       Clamp : kẹp giảm cấp đơn ≤ thành tiền ⇒ tổng ≥ 0 + BiKep/SoKep
            ├─ LogWarning nếu BiKep (mới) + LogInformation tổng (:216-220)
            └─ ghi sổ giải trình append-only (:237-248)
```

- **Bất biến giữ nguyên:** mọi làm tròn qua **một** policy (`TinhTienDon.cs:104-127`), tổng = Σ dòng đã làm tròn
  (`:139-146`), `quantity < 1` NÉM, cấm trộn tiền tệ (`:87-102`).
- **Thứ tự đọc setting:** chỉ ở B1; nếu B1 ném (không xảy ra — reader không ném), không có dòng nào được ghi.
- **Idempotency:** replay **không** đọc setting lại (đúng ADR-08/BR-011: kết quả đã lưu là chân lý; đổi chính sách **không** hồi tố).

### 5.2 Thêm bậc thang

```
POST /api/app/tier-price-set/{id}/tiers   (TierPriceSetAppService.AddTierAsync)
  ├─ bo.YeuCauChoPhepSuaNoiDung()                     (:92 — append-only, không đổi)
  ├─ trùng (Sku, MinQuantity)  ⇒ NÉM 0008             (:98-108 — không đổi)
  ├─ 🆕 toiDa = DocSoBacToiDa(GetOrNull(Pricing:MaxTiersPerSet))   (mặc định 20)
  ├─ 🆕 soHienTai = CountAsync(TierPriceSetId == id)
  ├─ 🆕 soHienTai + 1 > toiDa  ⇒ NÉM 0011 (tierPriceSetId, soBacHienTai, toiDa)
  └─ Insert TierPrice (autoSave)                       (:110-119 — không đổi)
```

### 5.3 Không có luồng FE

Xem §6.2 — không thêm route/screen; cấu hình đi qua endpoint ABP `setting-management/*` (Host đã nạp) nếu vận hành cần.

---

## 6. FE, đường lùi, kế hoạch test

### 6.1 Có hay không có FE

**Không có FE cho cả 4 hạng mục.** Lý do cụ thể:

| # | Vì sao không cần FE |
|---|---|
| 1 | Chính sách là **dữ liệu cấu hình server-side**, không thêm trường nào vào `GioTinhTienDto` (giữ `RoundingPolicyCode` cũ). Không có màn hình nào đang hiển thị/đổi chính sách: `apps/admin-web` **không có route Pricing/tier-price** (grep `pricing\|tier-price` trên `apps/admin-web/**` = **0**). Đường đổi giá trị dùng endpoint `setting-management/*` của ABP (đã nạp ở Host, `eCommerceHttpApiHostModule.cs:73`) — không phải màn hình của Pricing |
| 2 | Ngưỡng số bậc là guard server-side; hành vi UI hiện có là **không có màn bậc thang nào** (không tìm thấy view/route bậc thang trong `src/eCommerce.Web/Views` lẫn `apps/admin-web`). Khi màn đó được làm (WP khác), lỗi `0011` đã có mã để hiển thị |
| 3 | Guard tổng âm đổi **kết quả/lỗi** trả về; bên gọi Sales đã có đường xử lý `BusinessException`. FE hiện tại không hiển thị tổng âm (không có màn giỏ/checkout thuộc Pricing). Không thêm trường ⇒ không phải regenerate `schema.d.ts` |
| 4 | Test-only — không có bề mặt người dùng |

⇒ `W16-3` (`docs/agents/bang-phien-gd3-1.md:52`) **đóng với lý do "WP không có FE"**. Nếu sản phẩm cần màn cấu hình chính sách
(và permission riêng), đó là **hợp đồng mới** phải bàn lại — **AQ2**.

### 6.2 Đường lùi (cho từng hạng mục)

| # | Đường lùi | Số dòng code phải revert |
|---|---|---|
| 1 | Đặt lại `Pricing:RoundingPolicyCode` / `Pricing:MissingPricePolicy` = giá trị mặc định (hoặc xoá dòng `AbpSettings` tương ứng) ⇒ hành vi **y hệt hôm nay**; hoặc gỡ `PricingSettingDefinitionProvider` + tham số mới của `ChinhSachSnapshot` | **0** (cấu hình) hoặc 2 tệp |
| 2 | Đặt `Pricing:MaxTiersPerSet` = số rất lớn ⇒ guard **vô hiệu** mà không deploy | **0**; hoặc gỡ 8-12 dòng trong `AddTierAsync` + 1 test |
| 3 | Đặt `Pricing:OrderTotalNegativePolicy = Clamp` ⇒ **không còn ném**, tổng ≥ 0 (không quay lại hành vi âm — `D-219` **cấm** `AllowNegative`); muốn quay lại hành vi cũ (âm) phải revert `TinhTienDon` + 1 tệp test | **0** (Clamp) hoặc 2 tệp |
| 4 | Revert **1 tệp test** (`PostgreSqlSchemaTests.cs`) | 1 tệp |

Đường lùi **không** cần migration ngược, không cần `pg_dump` (0 schema change).

### 6.3 Kế hoạch test (mức thiết kế)

| # | Ca | Loại | Đỏ khi nào |
|---|---|---|---|
| 1a | `PricingChinhSachTests`: rỗng/không có setting ⇒ mặc định (`half-away-from-zero`, `NemLoi`, `20`, `Reject`) + `hopLe = true/false` đúng | unit (mới) | reader trả sai mặc định |
| 1b | `"half-away-from-zero"` ⇒ `IRoundingPolicy.Ten` khớp; giá trị lạ (`""`, `"abc"`, `"none"` nếu allowlist loại) ⇒ mặc định + `hopLe = false` | unit (mới) | reader nhận giá trị lạ hoặc ném |
| 1c | `TinhGio` với Setting đổi ⇒ `RoundingPolicyCode` trong kết quả **đổi theo**; `MissingPricePolicy = DungGiaNiemYet` + không có giá + có `ListPrice` ⇒ trả giá niêm yết; `= NemLoi` ⇒ `0004` | app service (mới) | snapshot còn cứng giá trị |
| 2a | Bộ `Draft`: thêm bậc thứ 20 ⇒ OK; thêm bậc thứ 21 ⇒ **NÉM `0011`** và **`CountAsync` trước = sau** (không ghi nửa) | unit/app (mới) | guard bị bỏ hoặc ghi trước khi kiểm |
| 2b | Setting = `3` ⇒ bậc thứ 4 bị chặn; Setting = `"abc"`/`""` ⇒ dùng 20 | unit (mới) | fallback sai |
| 3a | Mặc định `Reject`: giỏ 3 đồng, giảm 10.000, phí 5.000 ⇒ **NÉM `0010`** + `Data` có `tong`,`giamGiaCapDon`,`phi` (**thay** tripwire `TinhTienDonTests.cs:179-204`) | unit | nhánh Reject bị bỏ |
| 3b | `Clamp`: cùng đầu vào ⇒ tổng `≥ 0`, `BiKep = true`, `SoKep = 1`, và `Σ dòng = TongThanhToan` (bảo toàn sau kẹp) | unit | kẹp sai chỗ (sau phân bổ) ⇒ Σ lệch |
| 3c | Tổng `= 0` + có phí (ví dụ 0 đồng hàng, phí 5.000) ⇒ **không ném** với `Reject` | unit | điều kiện `<= 0` thay vì `< 0` |
| 3d | Test kiến trúc: **bề mặt công khai** của `TinhTienDon`, `ChonBacThang`, `GiaCoSoResolver`, `PricingChinhSach` **không** có tham số/field/property kiểu `ISettingProvider`/`ISettingManager` (không soi cả assembly — assembly chứa provider, xem §3.1) | kiến trúc (mới) | ai đó đọc setting trong hàm thuần |
| 4 | `PostgreSqlSchemaTests`: 4 tên **đích danh** phải có NND; tổng `NULLS NOT DISTINCT` **vẫn 93** (`PostgreSqlSchemaTests.cs:208-209`) | integration | thiếu 1 tên / mất NND |

**Negative control (đột biến phải ĐỎ, khôi phục phải XANH):**

| # | Đột biến | Ca phải đỏ |
|---|---|---|
| 1 | `DocChinhSachLamTron` bỏ qua tham số, luôn trả mặc định | 1c |
| 2 | Bỏ khối kiểm `soHienTai + 1 > toiDa` | 2a |
| 3 | `Reject` đổi thành kẹp-im-lặng (trả kết quả thay vì ném) | 3a |
| 4 | Sửa một tên trong `phaiCo` thành tên sai 1 ký tự | 4 (chứng minh danh sách **đang được đọc**; còn **nội dung NND** được chặn bởi tổng 93 + 2 migration dẫn chứng — không thể làm mất NND bằng đột biến mã nguồn vì index nằm trong migration đã merge) |

📌 **Đánh số ca tăng, không giảm:** tripwire cũ (1 ca khẳng định tổng âm) được **thay** bằng 3 ca tường minh (3a/3b/3c)
— đúng điều kiện chính tripwire đó đặt ra (`test/eCommerce.Tests/Pricing/TinhTienDonTests.cs:196-202`).

### 6.4 Bảng tên index cho hạng mục 4 (chính xác, không dùng tên tắt)

Vòng kiểm so **tiền tố** tên index: `indexname like '{ten}%'` — `PostgreSqlSchemaTests.cs:396-397`. Tên tắt trong `D-218`
(`PrcPriceLists`, …) là **tên bảng**; nếu chép nguyên vào `phaiCo` thì **không khớp** (tên index bắt đầu bằng `IX_`). Phải dùng:

| Tên tắt (`D-218`) | Tên **đầy đủ** thêm vào `phaiCo` | Nguồn |
|---|---|---|
| `PrcPriceLists` | `IX_PrcPriceLists_TenantId_Code_EffectiveFrom` | `PricingDbContextModelCreatingExtensions.cs:43-45`; NND: `20261015101649_Pricing_PriceList_And_ChannelPrice.cs:147-150` |
| `PrcPriceListItems` | `IX_PrcPriceListItems_TenantId_PriceListId_Sku` | `:65-67`; NND: `…:152-155` |
| `PrcChannelPrices` | `IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty` | `:89-91`; NND: `…:157-160` |
| `PrcPriceCalculationRecords` | `IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey` | `:203-205`; NND: `20261015110229_Prc002_PriceCalculationRecords.cs:66-69` |

Đồng thời cập nhật **hai chú thích nợ** (`PostgreSqlSchemaTests.cs:170-177` và `:325-328`) thành "đã đóng ở `WP-16`".

---

## 7. Rủi ro

| # | Rủi ro | Mức | Xử lý / đường lùi |
|---|---|---|---|
| **R1** | **Xung đột số hiệu mã lỗi trong SRS:** `prc-002` §12 đang gán `eCommerce:Pricing:0010` cho *"chồng khung hiệu lực"* (`…md:787`) và `0012` cho *"Σ dòng ≠ tổng"* (`:792`), trong khi `D-219`/`D-218` cấp `0010` = tổng âm, `0011` = vượt số bậc | 🟠 Trung bình | **BA cập nhật §12** cùng PR khi sửa §20.3 (`D-219` ① đã giao). Nếu sau này làm luật "chồng khung", cấp **`0013`** (append, sau `0011` và sau `0012` đã bị "Σ dòng ≠ tổng" chiếm) — **không** cấp `0012`. Code hiện **không** dùng `0010`/`0012` cho hai luật kia ⇒ chưa có xung đột thực thi |
| **R2** | **Lệch SRS §6.1:** tài liệu khai `IRoundingPolicySettingAppService` + `GetRoundingPolicyAsync`/`SetRoundingPolicyAsync` + permission `eCommerce.Pricing.RoundingPolicy.Manage` (`…md:427`, `:443-444`, `:708`), nhưng `PricingPermissions` hiện chỉ có `Default`/`Manage`/`CostPriceView` (`modules/Pricing/eCommerce.Pricing.Application.Contracts/PricingPermissions.cs`) và WP-16 **không** làm app service này | 🟠 Trung bình | Ghi nhận; **AQ2** để decider chọn "hoãn + sửa SRS" hay "bổ sung WP". Đường ghi hiện tại vẫn có: endpoint `setting-management/*` của ABP |
| **R3** | **Bằng chứng Clamp chỉ ở log:** sổ giải trình lưu **kết quả** (giảm giá đã kẹp), không lưu "giảm giá **yêu cầu**" ⇒ không truy vấn DB được *"đơn nào bị kẹp"*; kế toán chỉ thấy tổng đã kẹp | 🟠 Trung bình | Không đổi DTO (giữ FE/`schema.d.ts`); log `[CHINH-SACH-DA-DOC]` + `LogWarning(biKep, soKep, giamGiaYeuCau, giamGiaThucTe)`. Nếu kế toán cần truy vấn được ⇒ phải thêm cột/payload (migration + `D-xx` mới) — **không** tự làm |
| **R4** | **Race ngưỡng số bậc:** hai request `AddTier` đồng thời có thể vượt ngưỡng 1 bậc | 🟡 Thấp | Ngưỡng là guard vận hành, không phải bất biến; `D-218` cấm đổi unique index/khoá ⇒ không thêm khoá. Ghi rõ ở §3.3 |
| **R5** | **Đọc setting giữa pipeline** (ai đó "tiện tay" gọi `ISettingProvider` trong Domain) ⇒ hai dòng cùng hoá đơn dùng hai chính sách (ADR-08) | 🟠 Trung bình | Test kiến trúc 3d; quy ước: chính sách chỉ vào qua tham số |
| **R6** | **`none` (không làm tròn)** nếu được phép chọn sẽ tạo tổng VND lẻ → thanh toán/đối soát nhận số lẻ; `NoRoundingPolicy` tự ghi chú là dùng cho **bước trung gian** (`shared/eCommerce.Kernel/Money/RoundingPolicy.cs:39-42`) | 🟠 Trung bình | Allowlist W16-2 = **chỉ** `half-away-from-zero`; **AQ1** để decider chốt |
| **R7** | **Sai phạm vi setting:** nếu provider khai `SettingScopes.Tenant` **trần**, giá trị ghi host-level bằng `SetGlobalAsync` (đường ghi sẵn có — `ChinhSachHanMucAppService.cs:134-162`) có thể bị từ chối ⇒ "đổi cấu hình mà không có hiệu lực" | 🟠 Trung bình | Khai `Application \| Tenant`; W16-2 có ca đọc được ở **cả hai** mức (host + tenant) |
| **R8** | **Provider trong assembly module không được ABP quét** (khác với tiền lệ provider đang nằm ở project Host) ⇒ setting **không tồn tại**, `GetOrNullAsync` trả `null`, mọi giá trị cấu hình **bị bỏ qua im lặng** | 🟠 Trung bình | W16-2 **phải khởi động Host thật** và đọc 4 setting (M6: thay đổi DI/đăng ký không chứng minh được bằng build/test). Nếu không được quét ⇒ đường lùi: chuyển lớp provider sang `src/eCommerce.Domain/Settings/` (app → module, hợp lệ) + ghi lý do; **không** đổi chỗ của hàm đọc |
| **R9** | **Giá trị cấu hình gõ sai** bị "im lặng dùng mặc định" ⇒ vận hành tưởng đã đổi | 🟡 Thấp | `LogWarning` + `[CHINH-SACH-DA-DOC]` kèm `hopLe`; đường **ghi** (nếu có app service sau này) phải **NÉM** (`eCommerce:Settings:0001` — tiền lệ `ChinhSachHanMucAppService.cs:118-124`) |
| **R10** | **`TinhGioAsync` bắt đầu ném** cho giỏ có giảm giá vượt thành tiền ⇒ luồng Sales/checkout có thể đỏ nếu test E2E đang dựa vào tổng âm | 🟠 Trung bình | `D-219` đã chấp nhận (fail-closed); W16-4 chạy toàn bộ test Sales; nếu có test phụ thuộc hành vi cũ ⇒ sửa test **theo chính sách mới**, không nới guard |
| **R11** | **Lệch tên/giá trị Setting giữa `-arch` và SRS §9** (bảng "Setting liên quan", `…md:716` và `:718`): SRS khai `eCommerce.Pricing.MissingPricePolicy = Throw \| FallbackToBase` (mặc định `Throw`) và `eCommerce.Money.RoundingPolicy = AwayFromZero \| ToEven` — **lỗi thời** so với `D-218` (`Pricing:MissingPricePolicy = NemLoi \| DungGiaNiemYet`, `Pricing:RoundingPolicyCode = half-away-from-zero`). Ai code theo §9 sẽ dùng **sai khoá/giá trị** ⇒ setting tạo ra **không ai đọc** | 🟠 Trung bình | **BA sửa §9 cùng PR** (cùng đợt sửa §12/§20.3 — `D-219` ① đã giao BA) sang đúng **4 khoá** của `D-218`; code W16-2 theo `-arch`, **không** theo §9 |

---

## 8. Câu hỏi mở

| ID | Câu hỏi | Phương án | Đề xuất | Cần ai | Phần không phụ thuộc được làm ngay |
|---|---|---|---|---|---|
| **PRC-002-AQ1** | `Pricing:RoundingPolicyCode` có cho phép `none` (không làm tròn) không? | (a) chỉ `half-away-from-zero`; (b) thêm `none` | **(a)** — `NoRoundingPolicy` tự ghi là dùng cho bước trung gian (`RoundingPolicy.cs:39-42`); VND lẻ đồng ở tổng thanh toán không có đường xử lý ở cổng thanh toán/đối soát | DEC | Chốt allowlist = `{half-away-from-zero}`; nếu decider chọn (b) thì thêm 1 chuỗi + 1 ca test |
| **PRC-002-AQ2** | `prc-002` §6.1 khai `IRoundingPolicySettingAppService` + permission `eCommerce.Pricing.RoundingPolicy.Manage` (`…md:427`, `:443-444`, `:708`) nhưng `D-218` chỉ cho ABP Setting. Xử lý thế nào? | (a) hoãn app service, sửa SRS §6.1 ghi "dùng ABP SettingManagement"; (b) mở WP mới làm app service + permission riêng | **(a)** — hôm nay **0 bên gọi** (không có màn FE Pricing nào trong `apps/admin-web`), thêm bây giờ là code chết (T7) | DEC | W16-2 làm đường **đọc** (provider + reader); không thêm app service/route/permission |
| **PRC-002-AQ3** | `D-219` ghi `Clamp` ⇒ "`WithData` (`biKep`,`soKep`) + `LogWarning`" — nhưng `Clamp` **không ném**, `WithData` chỉ có trên exception, và `TinhTienDon` phải **thuần không I/O** | (a) mang `BiKep/SoKep` qua `KetQuaTinhTien` + `LogWarning` tại app service; (b) truyền `ILogger`/callback vào hàm thuần | **(a)** — giữ hàm thuần (điều kiện của F2), dữ liệu + log vẫn có | DEC | Thiết kế/ca test theo (a) |
| **PRC-002-AQ4** | `Pricing:MaxTiersPerSet = 0` nghĩa là gì? | (a) `0`/âm/không phải số ⇒ **mặc định 20** (không có công tắc tắt); (b) `0` = **không giới hạn** | **(a)** — fail-closed: một giá trị gõ sai **không** được làm mất guard; "tắt" bằng số rất lớn là hành vi **tường minh** | DEC | Reader theo (a) + ca test 2b |

---

## 9. Tự kiểm của F2

- Mọi `file:dòng` ở trên đã đối chiếu trong lượt này bằng **3 truy vấn đồ thị** (`codegraph_explore`:
  `PriceCalculationAppService ChinhSachSnapshot TinhTienDon PricingErrorCodes SettingDefinitionProvider` ·
  `ChinhSachSettingDefinitionProvider TenChinhSach` · `ISettingProvider GetOrNullAsync TierPriceSetAppService AddTierAsync ChinhSachHanMucAppService`)
  và **đọc trực tiếp**: `PriceCalculationAppService.cs`, `TinhTienDon.cs`, `ChonDonGiaCuoi.cs`, `GiaCoSoResolver.cs`, `PricingErrorCodes.cs`,
  `TierPriceSetAppService.cs`, `PricingDbContextModelCreatingExtensions.cs`, `PostgreSqlSchemaTests.cs`, `TinhTienDonTests.cs`,
  `shared/eCommerce.Kernel/Settings/ChinhSach.cs`, `src/eCommerce.Domain/Settings/ChinhSachDoiSoatVaHanMuc.cs`, `ChinhSachHanMucAppService.cs`,
  `PriceCalculationDtos.cs`, `PricingPermissions.cs` + 2 migration Pricing.
- **0 migration**, **0 bảng/cột mới**, **0 đổi unique index** — khớp `D-218` ② T6.
- **Không** thêm phụ thuộc module; **không** đụng `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml`.
- **Không** đổi DTO/route ⇒ FE không bị chặn; `W16-3` đóng với lý do (bảng §6.1 + `bang-phien-gd3-1.md:52`).
- Mỗi hạng mục có **đường lùi** (§6.2) và **≥ 1 ca test + negative control** (§6.3).
