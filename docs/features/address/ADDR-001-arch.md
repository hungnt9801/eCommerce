# `ADDR-001` — Kiến trúc phần code còn lại (WP-18 / `W18-1`)

> Vai **ARCH**, phiên **N77**, chuỗi `gd3-1`, hàng `W18-1` (bước F2 của `WP-18`), nhánh `feat/gd3-wp18-dia-chi`.
> Tài liệu **chỉ thiết kế** phần ⬜ "WP-15+" của
> [`addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md`](./addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md) (sau đây gọi **SRS**),
> đồng bộ quy ước với lượt ARCH cùng phiên ở [`ADDR-002-arch.md`](./ADDR-002-arch.md).
> **Không** code, **không** sinh migration. Nguồn quyết định: `D-77`…`D-82`, `D-93`, `D-115`/`D-141`, `D-216`, `D-231`
> ([`decisions.md`](../../nhanh/decisions.md)); trạng thái duyệt SRS = **`TẠM DUYỆT`** (`D-115`/`D-141` — chỉ code trên nhánh,
> **không** merge `main`).
>
> **Module chủ của mọi bảng/DTO/endpoint dưới đây: `Address`.** Mọi `file:dòng` đã đối chiếu trong N77
> (đồ thị `codegraph_explore` trước, `Read` sau cho phần đồ thị cắt bớt).
>
> 📄 **Loại tài liệu:** `-arch` là **tài liệu bổ trợ**, **ngoài** mẫu 16 mục / BR / mục "Đối chiếu mô hình Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó.
> Vì vậy tệp này không có `## 0.` / `## 10.`–`## 15.` là **đúng chủng loại**, không phải thiếu mục (ORCH chốt 25/11/2026).
>
> 🔄 **N80 (26/11/2026):** đóng **6/6 P3** của DOCREV vòng 2 — chỉnh tài liệu khớp **code as-built N78**, **không** đổi
> thiết kế/quyết định; mục #4 (`Enum.IsDefined` trước `DoDaiMa`) **đã đóng bởi `D-251`** (vá + `qa` phủ ca + NC, 26/11/2026 N80) ⇒ `ADDR-001-AQ6` **Closed** (§8).
>
> 🔄 **N81 (26/11/2026):** đồng bộ as-built `W18-5` — đóng **P2 #1/#2** của review `ADDR-001` (`docs/reviews/ADDR-001-review.md`):
> (a) overload `ApDung(keHoach)` một tham số **đã xoá** (`D-250`, commit `cea05f7`; **0** call site một tham số — §5.6);
> (b) `SourceName` rỗng **hoặc** `Trim().Length > 512` ⇒ `0002` tại **hai** cửa (§3.6). Test + NC + testplan ở commit `d73b552`.
> **Không** đổi thiết kế/hợp đồng/schema.

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đã có (đọc từ code, không phải kế hoạch)

| Thành phần | Hiện trạng | Bằng chứng |
|---|---|---|
| Danh mục | 1 bảng `AddrAdministrativeUnits` (cột `Level`), unique `(TenantId, SchemaVersion, Level, Code)` `NULLS NOT DISTINCT`, CHECK `SchemaVersion in (1,2)` | `AddressDbContextModelCreatingExtensions.cs:13,16,40-72`; `eCommerceDbContext.cs:315,352` |
| Tra cứu | `AdministrativeUnitAppService` `[Authorize]` **cấp class**, **4** method tiếng Việt; **3** method (`TraDonViAsync`/`XacMinhDiaChiAsync`/`SoSanhMaXaAsync`) **đọc cả bảng** `_donVi.GetListAsync()`; `LayXaTheoTinhAsync` **đã** lọc có điều kiện | `AdministrativeUnitAppService.cs:18-33,47-51,83-85`; `…:65-76` (đã lọc) |
| Lõi đồng bộ | `LapKeHoach` 4 hàng rào (`0601`…`0604`); **chỉ so `Name`** (`:174`) — **không** so `ParentCode`; `ApDung` gọi `DoiTen(tên)` **không** truyền căn cứ | `AdministrativeUnitSynchronizer.cs:127-151,174-178,215-253` |
| Job | `AdministrativeUnitSyncWorker` mặc định TẮT; khoá là **literal** `"address:sync-catalog"`; **không** UoW; 2 lần `autoSave: true`; `catch (BusinessException)` gộp mọi mã; **không** bản ghi lô | `AdministrativeUnitSyncWorker.cs:38,86-104,117-153,134,146,155-162` |
| Mã lỗi | `0001`…`0007` + `0601`…`0605` (+ `01xx` của `ADDR-004`); **chưa** có `0008` | `AddressErrorCodes.cs:10-31`; `AdministrativeUnitSynchronizer.cs:6-26` |
| Map HTTP | `0001/0003/0004/0005/0006/01xx` đã map; **`0002`/`0007` chưa map** (nợ `D-79`); `eCommerce:Lock:0001` → `409` | `eCommerceHttpApiHostModule.cs:301-313,377` |
| Guard khởi động | **Không có**; `BaseUrl` mặc định `https://provinces.open-api.vn/` gắn trong lambda typed client | `eCommerceHttpApiHostModule.cs:171-178` |
| Outbox/Inbox | **Đã bật tường minh** (`UseDbContext<eCommerceDbContext>`, `InboxProcessorFailurePolicy.Retry`) — **mới đo ở N77**, mạnh hơn ghi chú F6 của SRS | `eCommerceEntityFrameworkCoreModule.cs:74-88,99` |
| Cache | **Chưa có** chỗ nào dùng `IDistributedCache<>`; hạ tầng Redis + `KeyPrefix = "ecommerce:"` đã có | grep `DistributedCache` toàn repo = 1 tệp (chỉ Host); `eCommerceHttpApiHostModule.cs:217-223` |
| Permission | Nhóm `Addresses` có `Default/Manage/ManageBook/Verify` (nghĩa `ADDR-004`) | `eCommerceConsts.cs:363-376`; `eCommercePermissionDefinitionProvider.cs:168-171` |
| Metric | `eCommerceMetricNames.All` là nguồn sự thật; **chưa** có metric địa chỉ | `eCommerceMetrics.cs:81-107` |

### 1.2 Cái WP-18 phải làm (IN)

1. Bảng **`AddrCatalogSyncRuns`** (append-only, module `Address`) + entity bất biến + `DbSet`.
2. Hằng khoá `AddressLockKeys.CatalogImport = "address:sync-catalog"` dùng chung job + import; cấm literal thứ hai (`D-82`).
3. Guard khởi động BR-010 (`D-78`) — hàm thuần trong Domain, Host gọi **vô điều kiện** lúc build.
4. Hàng rào 5 (`D-81`): nguồn đổi `ParentCode` ⇒ NÉM `0008`, rollback cả lô, áp cả hai đường; siết `DoiTen` **bắt buộc** `LegalBasis` (BR-007, review #6).
5. Cache BR-014 + `AddressCacheInvalidatedEto` + metric; sửa 3 truy vấn đọc-cả-bảng sang truy vấn có điều kiện.
6. Endpoint còn thiếu: `GetProvincesAsync`, `GetDistrictsAsync`, `GetWardsByDistrictAsync` (`D-79`: cùng class, tên Anh).
7. `ImportDatasetAsync` — hợp đồng + guard + quyền + bản ghi lô (**không** định dạng file).
8. Permission nạp danh mục — **đã chốt `D-238`**: `eCommerce.Addresses.Catalog.Import`, side `Host`.
9. Map HTTP cho `0002`/`0007` (nợ `D-79`) + bảng map cho các mã **mới reachable** vì có endpoint import.
10. Metric NFR-008/009 vào `eCommerceMetricNames.All` + test; metric lock-busy của job (`XC-003` BR-010).

### 1.3 Ngoài phạm vi (OUT — **không** thiết kế ở đây)

- **Định dạng/nguồn file production** (`D-93` chưa xác định). `ImportDatasetAsync` nhận **`Rows` đã tách** khỏi định dạng file; parser file chờ input thật.
- Bảng ánh xạ 3↔2 cấp (`ADDR-003`, Q2), PII, `ADDR-004` (kể cả `AddressDirectory.LayDonViLienQuanAsync` thiếu lọc `SchemaVersion` — nợ `ADDR-004`/F9, **chỉ ghi nhận**).
- `Q5` `[AllowAnonymous]` (đang `Open`) — **không** mở route ẩn danh trong WP-18.
- Adapter GHN/GHTK (thuộc `ADDR-002`), `NOT-*`, `SHP-*`, `eCommerce.slnx`, `Directory.Packages.props`.

---

## 2. Ranh giới module

```
Host (eCommerce.HttpApi.Host)
  ├─ eCommerceHttpApiHostModule: gọi AddressSyncGuard · map lỗi · đăng ký HttpClient nguồn
  └─ depends on → Address.Application · Address.Application.Contracts · …
        Address.Application ──▶ Address.Domain ──▶ (ABP + Domain.Shared)
                            └─▶ Address.Application.Contracts ──▶ Domain.Shared
        Address.Application.Contracts ──▶ Domain.Shared (ETO không kéo Domain)
```

| # | Luật áp cho WP-18 | Cách giữ |
|---|---|---|
| B1 | Module `Address` **sở hữu** `AddrAdministrativeUnits`, `AddrCatalogSyncRuns`, mọi mã `00xx`/`06xx` | entity + mã ở `eCommerce.Address.Domain`; **không** thêm `Address:*` vào `eCommerceConsts.ErrorCodes` |
| B2 | Module khác **chỉ** đọc danh mục qua hợp đồng/ETO, **cấm** `IRepository<AdministrativeUnit, Guid>` (M2) | xem **vi phạm sẵn có** bên dưới; WP-18 **không** tạo thêm |
| B3 | Bảng lô **không** có navigation/FK tới bảng nào khác; không FK xuyên module | cấu hình thuần `builder.Entity<CatalogSyncRun>` trong `ConfigureAddress` |
| B4 | Mọi entity mới có `DbSet` trên DbContext hợp nhất — thiếu ⇒ `IRepository<T>` **không** đăng ký (lỗi runtime) | thêm `CatalogSyncRuns` **cạnh** `eCommerceDbContext.cs:314-324`; lưới `DbSetRegistrationTests` |
| B5 | Job chạy ngữ cảnh host ⇒ `TenantId = NULL`; khoá **không** có `{tenantId}` | bảng giữ `TenantId` nullable + `IMultiTenant`; khoá là hằng, không tham số (`D-82`) |
| B6 | Application **mới** được tham chiếu `shared/eCommerce.Kernel` (metric) | cần thêm `<ProjectReference>` vào `eCommerce.Address.Application.csproj` — **hợp lệ** theo tiền lệ 15 module (`modules/*/*.csproj` đã trỏ `shared/eCommerce.Kernel`) |

### 2.1 Vi phạm ranh giới **sẵn có** (không do WP-18 tạo; báo để ORCH ghi nợ)

| # | Vi phạm | Bằng chứng | Xử lý |
|---|---|---|---|
| V1 | `EInvoice.Application` tham chiếu **`Address.Domain`** và giữ `IRepository<AdministrativeUnit, Guid>` ⇒ gọi chéo module **không** qua `Application.Contracts` (trái M1/M2 của `abp-module-rules`) | `modules/EInvoice/eCommerce.EInvoice.Application/eCommerce.EInvoice.Application.csproj:12`; `PhatHanhHoaDonGd0Flow.cs:48,54,126,132` | **Ngoài WP-18** (nợ `EInvoice`, gắn `D-77` snapshot chữ). WP-18 chỉ **không** làm nặng thêm: bảng lô không bị đọc chéo |
| V2 | Template `Address` đọc **cả bảng** mỗi lần tra (NFR-001) | `AdministrativeUnitAppService.cs:33,51,85` | WP-18 sửa bằng truy vấn có điều kiện + cache (§5.4) |

---

## 3. Hợp đồng

### 3.1 Endpoint & method trong **`AdministrativeUnitAppService`** (`D-79`: cùng class, tên Anh)

| Method | Input → Output | Quyền | Ghi chú |
|---|---|---|---|
| `GetProvincesAsync` | `int schemaVersion` → `IReadOnlyList<AdministrativeUnitDto>` | `[Authorize]` cấp class (`AdministrativeUnitAppService.cs:18`) | `TheoSo` sai ⇒ `0005`; chỉ `IsActive`; rỗng ⇒ `[]` + log `Warning` (§12) |
| `GetDistrictsAsync` | `(int schemaVersion, string provinceCode)` | như trên | `v2` ⇒ NÉM `0004` (`ChoPhepCapHuyen == false`); `v1` chỉ huyện `IsActive` |
| `GetWardsByDistrictAsync` | `(int schemaVersion, string districtCode)` | như trên | `v2` ⇒ `0004`; `v1` xã thuộc huyện |
| `ImportDatasetAsync` | `ImportDatasetInput` → `CatalogSyncRunDto` | **method-level** `[Authorize(eCommerceConsts.Permissions.Addresses.CatalogImport)]` (`D-238`) | **không** `[AllowAnonymous]` (NFR-010); lỗi ⇒ NÉM (không trả `HopLe`); trả **dòng lô của lượt mình** (`D-241`) |
| `TraDonViAsync`/`XacMinhDiaChiAsync`/`LayXaTheoTinhAsync`/`SoSanhMaXaAsync` | giữ nguyên | giữ nguyên | `TC-05`/`TC-17` giữ **đúng 1** ngoại lệ `SoSanhMaXaAsync` |

- **Route** do ABP conventional controller sinh từ tên lớp + method (đăng ký tự động `eCommerce*.Application`,
  `eCommerceHttpApiHostModule.cs:380-414`): dự kiến `/api/app/administrative-unit/provinces|districts|wards-by-district|import-dataset` (ABP bỏ hậu tố `Async`).
  ⚠️ **Chưa xác minh** — BE xác nhận bằng `pnpm gen:api`, **cấm** đặt `[Route]` tay.
- `GetProvinces/GetDistricts/GetWardsByDistrict` **không** ghi gì (không transaction, không khoá — SRS §12).

### 3.2 DTO (tầng `Application`, `AddressDtos.cs` — **không** vào `Contracts`)

```csharp
public enum CatalogSyncTrigger { Job = 0, Import = 1 }        // append-only (D-17)
public enum CatalogSyncOutcome { DaApDung = 0, BiChan = 1, Loi = 2 }

public sealed record ImportUnitRow(AdministrativeLevel Level, string Code, string Name, string? ParentCode);

public sealed record ImportDatasetInput(
    int SchemaVersion, string SourceName, IReadOnlyList<ImportUnitRow> Rows);

public sealed record CatalogSyncRunDto(
    Guid Id, int SchemaVersion, CatalogSyncTrigger Trigger, string? SourceName, string? SourceHash,
    DateTime StartedAtUtc, DateTime ImportedAtUtc, string ImportedBy, CatalogSyncOutcome Outcome,
    string? ErrorCode, int SoThemMoi, int SoDoiTen, int SoMoLai, int SoDong);
```

- **Không** có trường `IsActive` trong `ImportUnitRow`: danh mục nạp = **tập đơn vị đang hiệu lực**; vắng mặt ⇒ `Dong`.
  Cho phép truyền `IsActive` là mở đường "đóng/mở" tuỳ ý qua file ⇒ **bỏ**.
- `SourceHash` do **server** tính (§5.3), **không** nhận từ client (BR-009). DTO **không** chứa message/stack.
- Enum ở `Application` (không vào `Contracts`) — hiện **0** module khác tiêu thụ (HP: `Shipping` sau này, qua ETO).
- 📌 **`docDeviation` — ĐÃ ĐÓNG (N78, SRS v0.7):** SRS §6.1 nay mô tả **đúng** `CatalogSyncRunDto` đầy đủ
  (`Id`/`Trigger`/`Outcome`/`ErrorCode`/`SourceHash`/`ImportedBy`/thời điểm + 4 số đếm — `AddressDtos.cs:56-70`),
  khớp as-built. `D-241` **chỉ** quy định "**không** thêm method đọc lịch sử lượt ở WP-18" — **không** quy định hình dạng
  DTO, nên **không** dùng `D-241` làm nhãn cho deviation hình dạng DTO nữa.

### 3.3 Permission

| Hằng (đã chốt `D-238`) | Giá trị | Định nghĩa | Side |
|---|---|---|---|
| `eCommerceConsts.Permissions.Addresses.CatalogImport` | `eCommerce.Addresses.Catalog.Import` | (a) `eCommerceConsts.cs:377-383` (b) as-built: `addresses.AddChild(eCommerceConsts.Permissions.Addresses.CatalogImport).MultiTenancySide = Volo.Abp.MultiTenancy.MultiTenancySides.Host;` — `eCommercePermissionDefinitionProvider.cs:174-175` | `MultiTenancySides.Host` |

- `D-238`: khai **cả hai nơi**; `[Authorize(...)]` ở **cấp method** trên `ImportDatasetAsync` (trên `[Authorize]` cấp class);
  **không** `[AllowAnonymous]`; **không** dùng lại `Addresses.Manage` của `ADDR-004`.
- Khai **cả hai nơi**; sau khi thêm phải chạy `DbMigrator` (binary mới build) để role `admin` được cấp.
- ⚠️ **Bẫy copy:** mẫu cũ `eCommercePermissionDefinitionProvider.cs:168-171` dùng `addresses.AddChild(name)` **không** kèm
  side ⇒ quyền thành `Both` (trái `D-238`). `AddChild(string)` trả `PermissionDefinition` ⇒ đặt side qua thuộc tính; as-built
  dùng đúng dạng `:174-175` (`AddChild(...).MultiTenancySide = MultiTenancySides.Host`).

### 3.4 Hằng khoá & guard (Domain — `AddressLockKeys`, `AddressSyncGuard`)

```csharp
public static class AddressLockKeys          // modules/Address/eCommerce.Address.Domain/
{
    // KHÔNG tiền tố `ecommerce:` — AbpDistributedLockOptions.KeyPrefix đã ghép (Host:237)
    public const string CatalogImport = "address:sync-catalog";
}

public static class AddressSyncGuard
{
    public const string HostCongKhaiMacDinh = "provinces.open-api.vn";
    public static void KiemTra(bool enabled, string? baseUrl, string? environmentName);
}
```

- `AdministrativeUnitSyncWorker.TenKhoa` (`:38`) **trỏ** vào hằng: `public const string TenKhoa = AddressLockKeys.CatalogImport;`
  (giữ tên cũ để test/khai thác không vỡ). **Cấm** literal thứ hai trong `modules/Address` (quét mã, `TC-25`).
- **Khoá bận:**
  - `ImportDatasetAsync` ⇒ NÉM `BusinessException(LockErrorCodes.KhongGianhDuocKhoa)` = `eCommerce:Lock:0001`
    (`shared/eCommerce.Kernel/Locking/LockErrorCodes.cs:20`) → `409` (đã map `eCommerceHttpApiHostModule.cs:377`);
    **cấm** `TimeoutException` (không có `Map` ⇒ ra mã mặc định, client mất khả năng "thử lại").
  - Job ⇒ bỏ lượt + `LogWarning` + metric `address_catalog_sync_total{trigger="job",outcome="skipped"}`; **0** dòng lô
    (không giành được quyền ghi ⇒ không có lượt). Đây là **chủ đích** của `D-82`, không phải mất dấu vết im lặng.
- **Guard** (`D-78`): `!enabled` ⇒ thoát. `host(baseUrl)` (rỗng ⇒ host mặc định) **≠** host công khai ⇒ thoát (**ca g** = lỗ hổng
  ghi nhận, `TC-22`). Ngược lại, `environmentName` **không** ∈ `{Development, Testing}` (so `OrdinalIgnoreCase`; **rỗng/không đọc được ⇒ coi như ngoài allowlist**) ⇒ NÉM
  `InvalidOperationException` ⇒ host **không start**. **Không** cờ vượt, không đọc từ DB.
  - **Vị trí gọi (quan trọng):** gọi **trực tiếp trong thân `ConfigureServices`**, **trước** khối `AddHttpClient<ProvincesOpenApiSource>`
    (`eCommerceHttpApiHostModule.cs:171`). **Cấm** đặt trong lambda `http => { … }` — lambda chỉ chạy khi typed client **được resolve lần đầu**
    ⇒ guard sẽ **không chạy** ở host không dùng client (đúng loại lỗi im lặng `D-78` cảnh báo).

### 3.5 ETO — `AddressCacheInvalidatedEto`

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `EventId`, `OccurredOn`, `TenantId` | như `AddressChangedEto` (`AddressEvents.cs:16-20`) | |
| `SchemaVersion` | `int` | |
| `ParentCodes` | `IReadOnlyList<string>` | danh sách `parentCode` cần xoá (`null` phần tử = danh sách tỉnh) — **mang dữ liệu cần để hành động**, nên phát qua **outbox** cùng UoW của lô |

- Thêm vào `modules/Address/eCommerce.Address.Application.Contracts/Events/AddressEvents.cs` (append, **không** sửa 2 ETO cũ).
- Phát **trong cùng UoW** với dòng lô `DaApDung` (outbox đã bật — `eCommerceEntityFrameworkCoreModule.cs:84-88`) ⇒ **nguyên tử** với dữ liệu.
  Mất ETO ⇒ chỉ trễ xoá cache (có TTL chặn) ⇒ **không** phải dữ liệu nghiệp vụ; nhưng vẫn dùng outbox để **không** có trạng thái "lô đã áp, instance khác giữ cache cũ".
- Handler `AddressCacheInvalidatedHandler : IDistributedEventHandler<AddressCacheInvalidatedEto>` **chỉ xoá cache, không ghi DB** ⇒
  **không** cần UoW; xử lý trùng ⇒ vô hại. (Nếu sau này handler ghi DB ⇒ **bắt buộc** `Begin(requiresNew: true, isTransactional: true)` — E2.)
- **Fail-closed:** publisher lấy `ParentCodes` từ **hợp** `parentCode` của danh mục mới **và** danh mục hiện có trước commit — nếu không, tỉnh **vừa biến mất** sẽ giữ cache con vĩnh viễn.

### 3.6 Mã lỗi & map HTTP

| Mã | Hằng | Nơi ném (sau WP-18) | Map đề xuất |
|---|---|---|---|
| `0002` | `AddressErrorCodes.MaKhongHopLe` | **Cửa import** (`ImportDatasetAsync`, as-built N81): `SourceName` rỗng **hoặc** `Trim().Length > 512` ⇒ NÉM `0002`, validate **trước** khoá (`AdministrativeUnitAppService.cs:277-291`, dùng `CatalogSyncRun.DoDaiSourceNameToiDa = 512`). **Hàng rào chung** ở `CatalogSyncRun.Ghi` cho **nhánh worker**: `CatChuoi(sourceName, DoDaiSourceNameToiDa, …)` **ném** `ArgumentException` khi vượt 512 — **không** cắt im lặng (`CatalogSyncRun.cs:42-43,140,156-172`) | **`400`** (`D-240`; nợ `D-79`) |
| `0007` | `HuyenKhongThuocTinh` | resolver (đã có) | **`400`** (`D-240`; nợ `D-79`) |
| `0008` | `AddressErrorCodes.NguonDoiDonViCapTren` (**mới**, cuối dải sau `0007`) | `LapKeHoach` hàng rào 5 | **`409`** (`D-240`) |
| `0009` | `AddressErrorCodes.ThieuCanCuDoiTen` (**mới**, sau `0008` — `D-239`) | `AdministrativeUnit.DoiTen` khi căn cứ rỗng | **không** map: **không** reachable qua HTTP ở WP-18 (`ApDung` luôn truyền `danhMuc.Nguon` đã validate non-empty — §5.6) |
| `0601`…`0604` | `AdministrativeSyncErrorCodes` (đã có) | hàng rào 1–4, **cả job lẫn import** | **`409`** (`D-240`) — trước đây job-only, nay **reachable qua HTTP** |
| `0605` | `DanhMucSaiDinhDang` (đã có) | **mới dùng**: dòng nguồn sai định dạng / **trùng `(Level,Code)` trong nguồn** | **`400`** (`D-240`) |
| `eCommerce:Lock:0001` | `LockErrorCodes.KhongGianhDuocKhoa` | import khi bận khoá | `409` (đã map `:377`; `D-240` không thêm dòng) |

- **Đánh số (`D-239`):** `0008` = kế tiếp sau `0007` (`AddressErrorCodes.cs:31`), `0009` = kế tiếp sau `0008`;
  **cấm** đánh số lại (`D-79`). Hai hằng chưa từng có trong code ⇒ không phải đổi tên.
- **Vì sao `0601`…`0605` phải map:** trước WP-18 chúng chỉ đi qua job (log). Khi có `ImportDatasetAsync`, chúng **đi ra HTTP**; không map ⇒
  ABP trả **`403`** ⇒ người dùng đi **xin quyền** trong khi lỗi thật là *"file thiếu 10 tỉnh"* — đúng lỗi đã sửa cho `ADDR-004`/`WP-07`
  (`eCommerceHttpApiHostModule.cs:289-378`). Đã chốt **`D-240`**: thêm **8** dòng map (`0002`/`0007`/`0605` → `400`;
  `0008`/`0601`/`0602`/`0603`/`0604` → `409`; `Lock:0001` đã map 409, **không** thêm).

---

## 4. Schema & migration

### 4.1 Bảng `AddrCatalogSyncRuns` (entity `CatalogSyncRun`)

> **Tên đề xuất**, SRS §5.2/§11 **chưa chốt tên**. Lý do: giữ tiền tố `Addr` như `AddrAdministrativeUnits`/`AddrCarrierMappings`,
> và hậu tố `SyncRuns` khớp `AddrCarrierSyncRuns` của lượt ARCH cùng phiên (`ADDR-002-arch.md` §4.1). Tên đầy đủ
> `AddrAdministrativeUnitSyncRuns` bị loại vì danh mục ĐVHC **là** "catalog" trong miền này và tên ngắn hơn không mơ hồ với `ADDR-002`.

Sổ **append-only**: `CreationAuditedAggregateRoot<Guid>, IMultiTenant` (**không** `FullAudited` — không có `IsDeleted`/`LastModification*`,
không UPDATE/DELETE), **0** public setter, **0** method đổi, chỉ factory `Ghi(...)`.

| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| `Id` | `uuid` | ✖ | PK |
| `TenantId` | `uuid` | ✔ | `NULL` ở host; `IMultiTenant` (tenant filter) |
| `SchemaVersion` | `int` | ✖ | CHECK `in (1,2)` |
| `Trigger` | `int` | ✖ | `0` job · `1` import (`CatalogSyncTrigger`; dòng "nguồn (job/file)" của `D-78`) |
| `SourceName` | `varchar(512)` | ✔ | `danhMuc.Nguon`; **`null` chỉ khi** lỗi xảy ra **trước** khi biết nguồn (adapter ném trước khi trả danh mục) |
| `SourceHash` | `varchar(64)` | ✔ | SHA-256 hex **thường** của danh mục chuẩn hoá (§5.3); CHECK `~ '^[0-9a-f]{64}$'`; `null` khi lỗi trước khi có danh mục (kiểu khớp `ADDR-002-arch.md` §4.1) |
| `StartedAtUtc` | `timestamptz` | ✖ | lấy **trước** `TaiDanhMucAsync` |
| `ImportedAtUtc` | `timestamptz` | ✖ | tên theo `D-78`; ngữ nghĩa = thời điểm **kết thúc/commit** (ADDR-002 gọi `FinishedAtUtc`) |
| `ImportedBy` | `varchar(128)` | ✖ | `job:AdministrativeUnitSyncWorker` · `user:{id}` |
| `Outcome` | `int` | ✖ | `0 DaApDung` · `1 BiChan` · `2 Loi`; CHECK `in (0,1,2)`; **chỉ thêm cuối** (`D-17`) |
| `ErrorCode` | `varchar(64)` | ✔ | `null` cho `DaApDung`/`Loi`; = mã đã NÉM cho `BiChan` |
| `ErrorType` | `varchar(256)` | ✔ | `ex.GetType().FullName` khi `Loi`; **cấm** `ex.Message`/stack/payload (BR-011) |
| `SoThemMoi`/`SoDoiTen`/`SoMoLai`/`SoDong` | `int` | ✖ | default `0`; = `KeHoachDongBo` (`AdministrativeUnitSynchronizer.cs:77-80`) |

**Index** (không unique — `D-78` không yêu cầu khoá idempotency; idempotency là **nội dung** `SourceHash`):
- `IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_ImportedAtUtc` — lịch sử / lượt gần nhất.
- `IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_Outcome_ImportedAtUtc` — lượt `DaApDung` gần nhất (màn vận hành).

**Ràng buộc tầng DB:** CHECK `SchemaVersion`, `Outcome`; CHECK hash hex. **Không** FK, **không** cascade.

### 4.2 Đọc danh mục — đổi truy vấn, **không** đổi schema

| Việc | Trước | Sau |
|---|---|---|
| Tra 1 đơn vị | đọc cả bảng rồi lọc (`:33`) | `GetListAsync(x => x.SchemaVersion == sv && x.Level == lv && x.Code == ma)` — dùng **prefix** của unique index `(TenantId, SchemaVersion, Level, Code)` |
| Danh sách con | đọc cả bảng (`GetListAsync()` rồi `Any`) | `x.SchemaVersion == sv && x.Level == Tinh` (tỉnh) / `x.ParentCode == parent && x.IsActive` (con) — dùng `IX_…_SchemaVersion_Level_ParentCode` |
| `XacMinhDiaChiAsync` | đọc cả bảng (`:51`) | giữ đọc-cả-bảng **có điều kiện** theo `(SchemaVersion)` hoặc 2 lần tra theo mã; BE chọn theo số đo, **không** đổi hợp đồng |

> **Không** thêm index mới ở WP-18: unique index hiện có đã phủ tiền tố `(TenantId, SchemaVersion, Level)`, index
> `(SchemaVersion, Level, ParentCode)` đã phủ truy vấn con (`AddressDbContextModelCreatingExtensions.cs:56-66`).

### 4.3 Migration plan (expand-only) & đường lùi

| Bước | Việc | Ai | Ghi chú |
|---|---|---|---|
| 1. Expand | Thêm `CatalogSyncRun` + cấu hình `ConfigureAddress` + `DbSet` (`eCommerceDbContext.cs:314` cạnh) rồi **để EF sinh** `dotnet ef migrations add Add_Addr001_CatalogSyncRuns` | **Một** chủ migration = luồng BE | migration chỉ `CREATE TABLE` + 2 `CREATE INDEX` + CHECK; **0** `ALTER/DROP` trên bảng có dữ liệu |
| 2. Kiểm diff | Không chạm `AddrAdministrativeUnits`; `.Designer.cs` có; tên bảng/index khớp §4.1 | BE + review | nếu EF sinh `DROP`/đổi kiểu ⇒ **dừng**, không merge |
| 3. Áp | `dotnet build` rồi `dotnet run --project src/eCommerce.DbMigrator` (binary **mới build**); kiểm `__EFMigrationsHistory` + `\d "AddrCatalogSyncRuns"` | BE/OPS | **cấm** `dotnet ef database update` (M6) |
| 4. Contract | **Không có** (không bỏ/đổi cột) | — | |
| Đường lùi | (a) tắt `Address:Sync:Enabled=false` ⇒ job ngừng; (b) trước merge: `git revert` + `migrations remove`; sau merge: **migration mới** `DropTable` (bảng là nhật ký, mất không mất danh mục); (c) tắt cache bằng cờ cấu hình `Address:Cache:Enabled=false` ⇒ đọc DB như trước | BE | `pg_dump` **BẮT BUỘC** trước khi áp (`ef-migration-postgres` M9: chạm DB có dữ liệu) — as-built N78 `artifacts/pg-dump/N78-W18-2.dump` |
| Backfill | **Không** — bảng mới | — | |
| Thứ tự | **migration trước, code sau** — code ghi dòng lô khi bảng chưa có ⇒ mỗi lượt `Loi` (`PostgresException`) và **không** áp thay đổi | BE | chạy `DbMigrator` **trước** khi bật cờ |

- **Không** seed; **không** đổi `AddrAdministrativeUnits`; **không** đổi `eCommerce.slnx`/`Directory.Packages.props`.
- ⚠️ **Tài liệu phải sửa cùng PR (CLAUDE.md §6) — trạng thái 26/11/2026:** BA **đã** đồng bộ SRS ở N78
  (commit `0b2ea6f`, SRS v0.7) cho các điểm từng lệch: `TC-13` = **6** `DbSet` (`+ CatalogSyncRuns`; `addr-001` §14.1);
  §6.2/§6.3 + §12 (mã `0601…0605` **reachable qua HTTP**, bổ sung `0009`, `0002`/`0007` hết nợ map — `D-240`);
  §9/§13 NFR-010 (permission chốt `eCommerce.Addresses.Catalog.Import` — `D-238`); §15.1 Q8 ⇒ `D-242`.
  Lần sau đổi hợp đồng/schema ⇒ rà lại **đúng danh sách này** trong **cùng** PR với code.

---

## 5. Luồng

### 5.1 Job `AdministrativeUnitSyncWorker` (sửa theo `D-78`/`D-81`/`D-82`)

```
DoWorkAsync (:68-105)
  ├─ Enabled=false ⇒ return (0 dòng)                                  [giữ]
  ├─ TryAcquireAsync(AddressLockKeys.CatalogImport, 5 s)
  │    └─ null ⇒ log Warning + metric {trigger=job,outcome=skipped} + return   [0 dòng]
  └─ DongBoAsync
       ├─ startedAtUtc = clock.Now            ← TRƯỚC adapter (D-78)
       ├─ try
       │   ├─ danhMuc = source.TaiDanhMucAsync(sv)          [ngoài transaction]
       │   ├─ hienCo  = repo.GetListAsync(x => x.SchemaVersion == sv)
       │   ├─ sourceHash = Bam(danhMuc)                      (§5.3)
       │   ├─ keHoach = LapKeHoach(...)   ── ném 0601/0602/0603/0605/0008 [hàng rào 1..5]
       │   ├─ UoW #1 Begin(requiresNew: true, isTransactional: true)
       │   │     ├─ InsertManyAsync(deThem, autoSave: false)       [bỏ autoSave:true ở :134]
       │   │     ├─ UpdateManyAsync(daSua, autoSave: false)        [bỏ autoSave:true ở :146]
       │   │     ├─ InsertAsync(CatalogSyncRun.DaApDung, 4 số đếm)
       │   │     ├─ publish AddressCacheInvalidatedEto (outbox)
       │   │     └─ CompleteAsync()  ← MỘT lần (lô + dòng lô + ETO nguyên tử)
       │   ├─ (sau commit) xoá cache cục bộ + metric {outcome=applied}
       │   └─ log Information (giữ)
       └─ catch
           ├─ BusinessException có Code ∈ HangRaoChan {0601,0602,0603,0604,0605,0008}
           │      ⇒ UoW #2 (requiresNew, CancellationToken.None) ghi dòng BiChan(ErrorCode)
           ├─ mọi exception khác ⇒ UoW #2 ghi dòng Loi(ErrorType), 4 số = 0
           └─ ghi dòng thất bại ⇒ log Error RIÊNG, không che exception gốc, KHÔNG ném ra worker
```

- **Bỏ early-return `!keHoach.CoThayDoi`** (`:122-128`): 0 việc **vẫn** ghi dòng `DaApDung` với 4 số = 0 (BR-009: "mọi lượt nạp").
- **Phân loại theo mã**, không `catch (BusinessException)` gộp (`:155`). Bộ mã dùng chung đặt ở Domain:
  `AdministrativeSyncErrorCodes.HangRaoChan` + `AddressErrorCodes.NguonDoiDonViCapTren` — job và import **cùng** một nguồn.
- UoW do worker **tự** mở: `IRepository` mặc định dùng UoW của lời gọi, background worker **không** có UoW sẵn.
- Metric NFR-008: `address_catalog_sync_total` (nhãn `trigger`, `outcome` — cardinality ≤ 8; **cấm** nhãn mã/tỉnh).

### 5.2 Hàng rào 5 (`D-81`) — sửa `LapKeHoach`

Thứ tự **bắt buộc** (mọi bước trước khi trả `KeHoachDongBo`):

1. Lọc `cuaPhienBan`/`nguon` theo `SchemaVersion` (`:124-125`) — giữ.
2. **Hàng rào 1** rỗng (`:128`) → **2** quá ít (`:136`) → **3** thiếu cấp tỉnh (`:146`) — giữ.
3. **Hàng rào 0b (mới) — kiểm tính toàn vẹn nguồn:** duyệt `nguon` **một lần**, trước khi dựng `theoKhoa`:
   - mã `Trim()` sai độ dài theo cấp (`AdministrativeSchemaVersion.DoDaiMa(level)`) hoặc không phải ASCII digit ⇒ `0605` (`maLoi`, ≤20);
   - `Name` sau `Trim()` rỗng ⇒ `0605`; `ParentCode` rỗng ở cấp ≠ `Tinh` ⇒ `0605`;
   - trùng `(Level, Code)` (so `Ordinal` sau `Trim`) ⇒ **một** `0605` với `soMaTrung`, `maTrung` (≤20).
   ⚠️ **Không** đổi nghĩa mã `0002` của `AdministrativeUnit.ChuanHoaMa` (vẫn là lỗi *tạo entity*; `D-79` cấm đánh số lại) —
   `LapKeHoach` tự kiểm bằng `DoDaiMa` và ném `0605` **trước** khi chạm `TaoMoi`.
   Lý do: nếu để `TaoMoi` ném trong bước áp dụng, lỗi ra dưới dạng "lỗi không mong đợi" và **dòng lô bị ghi `Loi` thay vì `BiChan`**.
   ✅ **Đã sửa theo `D-251` (N80, 26/11/2026):** `KiemTraToanVenNguon` kiểm
   `Enum.IsDefined(typeof(AdministrativeLevel), dv.Level)` **trước** `AdministrativeSchemaVersion.DoDaiMa(dv.Level)`
   (as-built `AdministrativeUnitSynchronizer.cs:343-353`, đầu vòng `foreach`). `Level` **ngoài dải** (enum lạ) ⇒ ném
   `BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)` = **`0605`** (thuộc `HangRaoChan` ⇒ dòng lô `BiChan`),
   **không** còn rơi vào `0002`/`Loi`; `Data` mang `lyDo`, `level` (`(int)dv.Level`), `ma`, `nguon`, `schemaVersion`.
   `be` sửa `modules/Address/.../AdministrativeUnitSynchronizer.cs:343-353`; `qa` phủ ca `Level=(AdministrativeLevel)99` ⇒
   `0605` + `Data["level"]==99` **và** NC (bỏ/đảo thứ tự `Enum.IsDefined` xuống sau `DoDaiMa` ⇒ ĐỎ nhận `0002`). **`ADDR-001-AQ6` đóng** (§8).
4. **Hàng rào 5 (`0008`) — `ParentCode`:** trong vòng `foreach` (`:157-179`), **trước** nhánh `!cu.IsActive` (`:168`):
   so `ParentCode` chuẩn hoá (`Trim()`, `null ≡ ""`, `Ordinal`). Gom **toàn bộ** mã lệch; nếu `soDoiCha > 0` ⇒
   sau khi quét xong (và **trước** hàng rào 4) NÉM **một** `BusinessException(0008)` với `soDoiCha`, `nguon`, `schemaVersion`,
   `muc` = ≤ 20 chuỗi `"{code}: {chaCu} → {chaMoi}"`. **0** thay đổi được áp; `ParentCode` entity **không** ghi đè.
5. **Đổi tên** (`:174-178`) giữ — nhưng "tên khác" **không** còn là xung đột (`D-81`): `DoiTen` tại chỗ.
6. **Mã biến mất** (`:182-185`) ⇒ `Dong`; **hàng rào 4** (`:193-206`) giữ nguyên, tính **sau** khi đã loại ca `0008`.

> **Đừng gộp hai ca:** `0008` = đổi **cha** của mã **đã có** (danh tính đổi) ⇒ từ chối; đổi **mã** = `ThemMoi` + `Dong` (hàng rào 4).

### 5.3 `SourceHash` (`D-78` §7.3)

- Đầu vào: `"v{SchemaVersion}\n"` + `SourceName.Trim()` + `"\n"` + các dòng, **sắp** `(LevelInt, Code)` `Ordinal`, mỗi dòng
  `$"{LevelInt}|{Code.Trim()}|{ParentCode?.Trim() ?? ""}|{Name.Trim()}"`, nối `"\n"`, UTF-8; **SHA-256 → hex thường 64**.
- **Ổn định** qua lần chạy lại; **giống nhau** giữa đường file và job khi dữ liệu chuẩn hoá giống nhau (`TC-21` ca e);
  **cấm** `Guid.NewGuid()`/thời điểm/đường dẫn file. `Name` đổi ⇒ hash đổi (đúng nghĩa "danh mục khác đi"); **không** dùng hash làm khoá idempotency.

### 5.4 Cache BR-014 (F8) + ETO

- Tệp: `modules/Address/eCommerce.Address.Application/Caching/AddressCatalogCache.cs` (kiểu `ITransientDependency`), dùng
  `IDistributedCache<AddressChildrenCacheItem, string>` của `Volo.Abp.Caching`; `[CacheName("AddressAdministrativeUnitList")]`.
- **Khoá** `(schemaVersion, parentCode)` → `$"v{sv}:{parentCode?.Trim() ?? "*"}"`; **không** tiền tố `ecommerce:` (Host ghép, `:222`).
  Giá trị = **mọi con đang `IsActive`** của `parentCode` (parentCode `null` ⇒ danh sách **tỉnh**); method lọc `Level` trong bộ nhớ
  ⇒ một item dùng được cho cả `GetProvinces`/`GetDistricts`/`GetWardsByDistrict`/`LayXaTheoTinhAsync`.
- **TTL (`D-242`):** ABP Setting `Address:Cache:TtlSeconds = 86400` (24 h). Giá trị **rỗng/`0`/âm/không phải số/không đọc được**
  ⇒ dùng **86400** + `LogWarning` (khuôn `D-224`: cấu hình sai không được làm hệ thống chạy sai **im lặng**). Cờ
  `Address:Cache:Enabled = false` ⇒ tắt hẳn cache, đọc DB như trước (đường lùi A8). **TTL/token thế hệ KHÔNG vào khoá** —
  khoá giữ nguyên `v{sv}:{parentCode ?? "*"}`.
- **Xoá bắt buộc** sau commit của lô là **cơ chế chính** (`D-242`): `ParentCodes` = **hợp** `{null}` ∪ `parentCode(danh mục mới)` ∪
  `parentCode(danh mục hiện có trước commit)` — thiếu vế "hiện có" ⇒ tỉnh vừa biến mất giữ cache con tới hết TTL. Xoá cục bộ
  **không** làm hỏng lô (nếu ném ⇒ log `Error` + metric; TTL chặn); instance khác xoá qua `AddressCacheInvalidatedEto` (**outbox
  cùng UoW** với dòng lô `DaApDung`, §3.5). TTL **chỉ** là lưới cho instance lỡ mất ETO + reader-race (§7 R3).
- Metric `address_catalog_cache_hits_total`/`address_catalog_cache_misses_total` **chỉ** đo **tỉ lệ trúng/trượt** (chẩn đoán cache
  có tác dụng). ⚠️ **Không** dùng counter để "đo p95": NFR-001 (< 100 ms nguội / < 10 ms nóng) phải đo bằng
  **benchmark/integration test có đồng hồ** như SRS §13 — hoặc histogram/timer nếu `XC-005` bổ sung loại instrument đó
  (danh sách hiện chỉ có counter/gauge).

### 5.5 `ImportDatasetAsync` (đường file — nhận `Rows`, **không** nhận file)

```
1. [Authorize(Addresses.CatalogImport)]  (cấp method, trên [Authorize] cấp class)
2. TheoSo(input.SchemaVersion)            ⇒ 0005            ┐ validate TRƯỚC khoá ⇒ 0 dòng lô
3. SourceName rỗng hoặc Trim().Length > 512 ⇒ 0002  ┘ (TC-21 ca a; as-built :277-291)
4. TryAcquireAsync(AddressLockKeys.CatalogImport, 5 s)
     └─ null ⇒ NÉM BusinessException(eCommerce:Lock:0001)   → 409, 0 dòng
5. startedAtUtc = clock.Now; sourceHash = Bam(Rows)
6. try
   ├─ hienCo = repo.GetListAsync(SchemaVersion)
   ├─ keHoach = LapKeHoach(...)           ── hàng rào 0b/1..5 (DÙNG CHUNG với job)
   ├─ UoW của lời gọi (ABP mở sẵn — **KHÔNG** `requiresNew`): insert/update (autoSave:false)
   │    · dòng lô DaApDung · ETO (outbox)   ← MỘT transaction
   │    · **KHÔNG** gọi `CompleteAsync` thủ công — để interceptor/UoW middleware của ABP hoàn tất UoW ambient
   │      (as-built `AdministrativeUnitAppService.cs:309-371`: chỉ `autoSave:false` + `DangKySauCommitAsync`; commit do ABP)
   ├─ xoá cache cục bộ + metric {trigger=import,outcome=applied}
   └─ trả CatalogSyncRunDto
7. catch BusinessException ∈ HangRaoChan ∪ {0008}
   ├─ UoW #2 Begin(requiresNew: true, isTransactional: true): ghi dòng BiChan(ErrorCode) + Complete
   └─ NÉM lại exception gốc  ← dòng từ chối ĐÃ COMMIT trước khi ném (TC-21 ca f, §8.1 #5)
8. catch Exception khác
   ├─ UoW #2 ghi dòng Loi(ErrorType) + Complete
   └─ NÉM lại  (ghi dòng thất bại ⇒ log Error, vẫn ném exception gốc)
```

- **Vì sao CHỈ UoW #2 `requiresNew`:** lượt **thành công** ghi trong UoW của lời gọi (SRS §8.1 #5/§12: *"phần ghi lô thành công
  **không** tự mở transaction"*) ⇒ lô + dòng lô + ETO **nguyên tử**. Lượt **bị từ chối/lỗi** thì exception đi ra ⇒ UoW đó rollback
  ⇒ dòng lô phải ghi ở UoW **riêng** `requiresNew: true` và commit **trước khi ném** (BR-009; bài học review vòng 2 #1).
- **Không** đổi `IsActive` qua input; **không** cho client gửi `SourceHash`; **không** gọi adapter hãng/nguồn ngoài.
- **Định dạng file/nguồn production (`D-93`) = CHƯA XÁC ĐỊNH** ⇒ endpoint nhận `Rows`; parser (CSV/XML/…) + tải tệp là WP **sau**, cần input thật.

### 5.6 `DoiTen` bắt buộc căn cứ (BR-007, review #6)

- **`D-239`:** `AdministrativeUnit.DoiTen(string tenMoi, string legalBasis)` — **bỏ** giá trị mặc định; `IsNullOrWhiteSpace(legalBasis)`
  ⇒ NÉM `BusinessException(AddressErrorCodes.ThieuCanCuDoiTen)` = `eCommerce:Address:0009`, kèm `WithData("lyDo", "thiếu căn cứ pháp lý khi đổi tên")`
  (**đúng khoá `lyDo` của `D-239`**, as-built `AdministrativeUnit.cs:145-146`), `WithData("level", …)`, `WithData("code", …)` (`:147-148`).
  `Dong`/`MoLai` giữ tham số tuỳ chọn; **không** dùng lại `0106`/`0605`.
- `AdministrativeUnitSynchronizer.ApDung(keHoach, string legalBasis, Guid? tenantId = null)` — truyền `danhMuc.Nguon` của lượt
  (`:236` hiện gọi `DoiTen(td.Nguon!.Name)` **thiếu** căn cứ). Chữ ký đổi ⇒ sửa **mọi** nơi gọi: worker
  (`AdministrativeUnitSyncWorker.cs:130`), `test/eCommerce.Tests/AddressSync/AdministrativeUnitSynchronizerTests.cs` và
  `test/eCommerce.Tests/AddressSync/ProvincesOpenApiParserTests.cs:217` — bỏ sót tệp thứ ba ⇒ **build đỏ**.
- ✅ **Overload `ApDung(keHoach)` một tham số ĐÃ XOÁ ở N81** (`D-250`, commit `cea05f7`) — **không** còn dạng "giữ để tương thích
  ngược". As-built chỉ còn **một** chữ ký `ApDung(KeHoachDongBo keHoach, string legalBasis, Guid? tenantId = null)`
  (`AdministrativeUnitSynchronizer.cs:263-266`); **0** call site một tham số trong `modules/` và `test/` (cơ chế đỏ là **trình biên dịch**).
- Kèm nợ `D-77` (EInvoice snapshot chữ) — **ngoài WP-18**, chỉ ghi nhận.

---

## 6. Test / QA plan (ánh xạ SRS §14)

| SRS | Ca cần thêm/sửa | Tầng |
|---|---|---|
| `TC-01/02/03` | 3 endpoint mới: `v2` chỉ tỉnh+xã; `v1` tỉnh→huyện→xã; **không** lẫn schema | Unit (`AdministrativeUnitAppService`) |
| `TC-05/TC-17` | Reflection quét class: **mọi** method public mới nhận `schemaVersion`/`SchemaVersion`; ngoại lệ vẫn **đúng 1** `SoSanhMaXaAsync` | Unit |
| `TC-12` | `DoiTen` căn cứ rỗng ⇒ **NÉM `0009`**; `ApDung` truyền `danhMuc.Nguon` làm căn cứ; **0** public setter trên `Code/Level/SchemaVersion/ParentCode/Name` (`D-239`, `D-77`) | Unit (reflection + quét mã) |
| `TC-11` | Lô nguyên tử **cả hai đường**: (a) import lỗi giữa chừng; (b) job lỗi **sau** insert **trước** update ⇒ **0** dòng đổi | Integration (PG) |
| `TC-13` | **Sửa 3 → 4** `DbSet` (`+ CatalogSyncRuns`) | Unit (reflection) |
| `TC-14` | Sau nạp: cache `(sv,parent)` **không** còn dữ liệu cũ; xoá cả parent **vừa biến mất** | Integration (Redis) |
| Mới (`D-242`) | Setting TTL trống / `"0"` / `"-5"` / `"abc"` ⇒ dùng **86400** + `LogWarning`; `Address:Cache:Enabled=false` ⇒ đọc DB (không chạm cache); nạp lô ⇒ cache sạch **trước** TTL | Unit + Integration |
| `TC-18/19` | Permission mới định nghĩa 2 chiều; `ImportDatasetAsync` **không** `[AllowAnonymous]` và có permission riêng; class có `[Authorize]` | Unit (reflection) |
| `TC-20` | 3 metric mới nằm trong `eCommerceMetricNames.All`; (khuyến nghị) test **hai chiều** "mọi hằng ⇒ có trong `All`" — nợ §26 của `XC-005` | Unit |
| `TC-21` | (a) `SourceName` rỗng **hoặc** > 512 ký tự ⇒ ném `0002`, **0** dòng (`N81`: 3 ca unit — import 512-guard + `CatalogSyncRun`); (b) import hợp lệ ⇒ dòng lô đủ trường; (c) job 2 lượt cùng dữ liệu ⇒ **2** dòng, **cùng** `SourceHash`; (d) lượt job bị hàng rào ⇒ dòng `BiChan`; (e) cùng dữ liệu qua file vs job ⇒ **cùng** hash; (f) import bị `0008` ⇒ client nhận lỗi **và** dòng từ chối đọc được sau rollback | Unit + Integration |
| `TC-22` | Bảng ca a–h; thêm ca **rỗng/không đọc được `ASPNETCORE_ENVIRONMENT` ⇒ NÉM** (fail-closed) | Unit (hàm thuần) + khởi động Host thật |
| `TC-24` | Hàng rào 5: đổi cha ⇒ NÉM `0008`, `ParentCode` giữ nguyên, **0** thay đổi; ca (b) đổi tên mã khác không áp; (c) mã đang đóng vẫn NÉM; (d) chỉ khác khoảng trắng/`null`↔rỗng ⇒ **không** ném; (e) qua `ImportDatasetAsync` ⇒ dòng từ chối còn đọc được | Unit + Integration |
| `TC-25` | `AddressLockKeys.CatalogImport == "address:sync-catalog"`; literal xuất hiện **đúng 1** lần trong `modules/Address`; khoá giả bị giữ ⇒ `ex.Code == "eCommerce:Lock:0001"`, **0** dòng | Unit |
| Mới | Hàng rào 0b: trùng `(Level,Code)` / mã sai định dạng / `Name` rỗng ⇒ `0605` (**không** `0002` từ `TaoMoi`), **0** dòng | Unit |

### 6.1 Negative control **bắt buộc** (đột biến phải **đỏ**, rồi khôi phục **xanh**)

| # | Đột biến | Ca phải đỏ |
|---|---|---|
| NC-1 | Ghi dòng lô `BiChan`/`Loi` **trong** UoW của lô (bỏ `requiresNew`) | `TC-21(f)` / `TC-24(e)` — sau rollback **không** còn dòng từ chối |
| NC-2 | Đặt `AddressSyncGuard.KiemTra` **trong** lambda `AddHttpClient` | `TC-22(a)` — host vẫn start khi typed client không được resolve |
| NC-3 | Giữ `early-return` khi `!CoThayDoi` | `TC-21(c)` — lượt 2 cùng dữ liệu **không** có dòng lô |
| NC-4 | Bỏ xoá cache sau commit | `TC-14` |
| NC-5 | Đặt hàng rào 5 **sau** hàng rào 4 | `TC-24` — lô vừa đổi cha vừa vượt ngưỡng đóng trả `0604` thay vì `0008` |
| NC-6 | `DoiTen` nhận căn cứ rỗng | `TC-12` ("`DoiTen` căn cứ rỗng ⇒ NÉM `0009`") — không ném |

Chạy bằng `tools/agents/negative-control.ps1` (ORCH chạy lại, không tin lời vai).

---

## 7. Rủi ro

| # | Rủi ro | Hỏng kiểu | Giảm thiểu |
|---|---|---|---|
| R1 | Parser/định dạng file production chưa có (`D-93`) | làm sớm ⇒ thiết kế trên giả định | endpoint chỉ nhận `Rows`; parser là WP sau, có input thật |
| R2 | Job bỏ lượt vì bận khoá ⇒ **không** dòng lô; UI không phân biệt "chưa chạy" với "bận khoá" | **im lặng một phần** | metric `{outcome=skipped}` + log `Warning`; `D-241`: **không** có API đọc lượt — OPS đọc bằng `psql` |
| R3 | Cache: reader đọc DB **trước** commit rồi ghi cache **sau** bước xoá | dữ liệu cũ tới hết TTL (24 h) | TTL chặn; nếu cần chặt hơn ⇒ **token thế hệ** trong khoá (đổi hợp đồng khoá ⇒ cần `D-xx`) |
| R4 | `EInvoice` đọc thẳng `Address.Domain` (`PhatHanhHoaDonGd0Flow.cs:126`) | vi phạm M1/M2, phát hiện muộn | ngoài WP-18; ghi `boundaryViolation` cho ORCH |
| R5 | Tên cột `ImportedAtUtc` (D-78) vs `FinishedAtUtc` (ADDR-002) dễ gây nhầm khi đối soát | nhầm ngữ nghĩa | chốt §4.1; đọc chéo 2 tài liệu phải biết đây là **cùng** mốc kết thúc |
| R6 | `0008`/`0601…0605` chưa map ⇒ import trả `403` | người dùng xin quyền thay vì sửa file | `D-240` đã chốt 8 dòng map — thi công phải thêm **cùng PR** với `ImportDatasetAsync` |
| R7 | Triển khai code trước migration ⇒ mọi lượt `Loi` | **im lặng** nếu UI chỉ nhìn "lượt gần nhất" | §4.3: `DbMigrator` trước, bật cờ sau |
| R8 | Thêm `ProjectReference` `eCommerce.Kernel` vào `Address.Application` | DI/khởi động | hợp lệ theo 15 tiền lệ; **khởi động Host thật** để kiểm (build/test xanh không chứng minh resolve được) |
| R9 | Nguồn đổi cha **hợp pháp** (sáp nhập thật) ⇒ cả lô bị `0008` | chặn vận hành | chủ đích `D-81`; quy trình thủ công (đóng/mở theo văn bản) — ghi cho OPS |
| R10 | `MauCanNguoiAnhXa`/JSON của `ADDR-002` dùng `jsonb`; ở đây **không** có cột JSON | — | không áp dụng; bảng lô giữ cột vô hướng |

---

## 8. Câu hỏi mở (không tự chọn đáp án)

**Không còn câu hỏi mở từ lượt ARCH N77.** Toàn bộ câu của lượt ARCH đã được DEC chốt trong N77:

| ID | Nội dung | Quyết định |
|---|---|---|
| `ADDR-001-AQ1` | Permission nạp danh mục | **`D-238`** |
| `ADDR-001-AQ2` | Mã lỗi `DoiTen` thiếu căn cứ | **`D-239`** |
| `ADDR-001-AQ3` | Map HTTP 8 mã | **`D-240`** |
| `ADDR-001-AQ4` | Đọc dòng lô | **`D-241`** |
| `ADDR-001-AQ5` | TTL cache | **`D-242`** (86400 s; sai/không đọc được ⇒ 86400 + `LogWarning`; `Enabled=false` tắt cache; **không** đưa TTL/token vào khoá) |

**Câu mới từ DOCREV vòng 2 (N80, 26/11/2026) — cần `decider`/`be`:**

| ID | Nội dung | Đề xuất | `needs` |
|---|---|---|---|
| `ADDR-001-AQ6` | **Closed (`D-251`)** — hàng rào 0b kiểm `Enum.IsDefined(typeof(AdministrativeLevel), dv.Level)` **trước** `DoDaiMa` ⇒ `Level` ngoài dải ra **`0605`** (dòng lô `BiChan`), không còn `0002`/`Loi`. `be` sửa `AdministrativeUnitSynchronizer.cs:343-353`, `qa` phủ ca + NC (N80, 26/11/2026). | — (đã vá) | — |

> Không nêu lại các câu đã có quyết định: `D-77`…`D-82`, `D-79` (giữ class/mã), `D-80` (bỏ policy), `D-93` (nguồn production = file,
> định dạng chưa xác định), `Q5` (`[AllowAnonymous]` — **không** mở ở WP-18).

---

## 9. Tự kiểm

- Mọi `file:dòng` đã đối chiếu trong N77: `AdministrativeUnitAppService.cs`, `AdministrativeUnit.cs`, `AdministrativeUnitResolver.cs`,
  `AdministrativeUnitSynchronizer.cs`, `AdministrativeUnitSyncWorker.cs`, `AddressApplicationModule.cs`, `AddressDtos.cs`,
  `AddressErrorCodes.cs`, `AddressDbContextModelCreatingExtensions.cs`, `eCommerceDbContext.cs`, `eCommerceHttpApiHostModule.cs`,
  `eCommerceConsts.cs`, `eCommercePermissionDefinitionProvider.cs`, `eCommerceMetrics.cs`, `LockErrorCodes.cs`,
  `eCommerceEntityFrameworkCoreModule.cs`, `AddressEvents.cs`, `eCommerce.Address.Application.csproj`, `PhatHanhHoaDonGd0Flow.cs`,
  `eCommerce.EInvoice.Application.csproj`.
- **0** câu mở mới (`ADDR-001-AQ6` **đã đóng bởi `D-251`** — vá `Enum.IsDefined` trước `DoDaiMa`, `qa` phủ ca + NC, 26/11/2026 N80); **0** cam kết endpoint/trường NCC; **0** thay đổi `AddrAdministrativeUnits`/`AddrCarrierMappings`/`AddrAddresses`.
- **N81 (26/11/2026) — 2 P2 của review `ADDR-001` ĐÃ ĐÓNG:** #1 = xoá overload `ApDung` một tham số (`D-250`, commit `cea05f7`; as-built `AdministrativeUnitSynchronizer.cs:263-266`); #2 = giới hạn `SourceName` (`0002` tại `ImportDatasetAsync` **trước** khoá + hàng rào `CatChuoi` trong `CatalogSyncRun.Ghi` cho worker — §3.6). Test + NC + testplan ở commit `d73b552`.
- **DOCREV vòng 2 (N80) — 6/6 P3 đã xử lý:** #1 danh sách "Tài liệu phải sửa cùng PR" (§4.3, ghi rõ BA đã đồng bộ SRS v0.7 ở N78) · #2 `WithData("lyDo")` (§5.6) · #3 bỏ nhãn `D-241` cho DTO (§3.2) · #4 `Enum.IsDefined` trước `DoDaiMa` (§5.2 — as-built chưa có ⇒ `AQ6`) · #5 `AddChild(...).MultiTenancySide = Host` (§3.3) · #6 không gọi `CompleteAsync` trên UoW ambient (§5.5).
- **Đã áp quyết định N77 — `D-238`…`D-242`** (§3.1, §3.3, §3.6, §5.4, §5.5, §5.6, §8); §8 **không còn** câu mở.
- Đã sửa theo DOCREV vòng 1: P2 #1 (đo p95, không dùng counter) · P2 #2 (bỏ `requiresNew` ở UoW thành công) · P2 #3 (thêm `TC-12`, NC-6 trỏ ca test) · P2 #4 (`ProvincesOpenApiParserTests.cs:217`); P3 (route `import-dataset`, §1.1, TTL vào §8, `docDeviation` DTO, `SourceHash varchar(64)`, hàng rào 0b không đánh số lại `0002`, dải metric `:81-107`).
- Đồ thị dùng trước `Grep`/`Read`: `codegraph_explore` cho `AdministrativeUnit*`, `AddressDbContextModelCreatingExtensions`,
  `eCommerceMetricNames`; `Grep` chỉ dùng để (a) đếm vi phạm `AdministrativeUnit` ngoài module, (b) kiểm sử dụng `DistributedCache`,
  (c) kiểm outbox — cả ba không phải câu hỏi cấu trúc mà là **đếm toàn repo**.
