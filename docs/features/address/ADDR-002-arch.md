# `ADDR-002` — Kiến trúc phần code còn lại (WP-18 / `W18-1`)

> Vai **ARCH**, phiên **N77**, chuỗi `gd3-1`, hàng `W18-1` (bước F2 của `WP-18`), nhánh `feat/gd3-wp18-dia-chi`.
> Tài liệu này **chỉ thiết kế** phần ⬜ "WP-15+" của
> [`addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md`](./addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md) (sau đây gọi **SRS**).
> **Không** code, **không** sinh migration. Nguồn quyết định: `D-83`…`D-92`, `D-94`, `D-79`, `D-17`, `D-216`, `D-231`
> (`docs/nhanh/decisions.md`). Trạng thái duyệt của SRS: `TẠM DUYỆT` theo `D-115`/`D-141` (chỉ code trên nhánh, **không** merge `main`).
>
> **Module chủ của mọi bảng/DTO/endpoint dưới đây: `Address`.** Mọi `file:dòng` nêu trong tệp đã đọc/đối chiếu trong phiên N77
> (đồ thị `codegraph_explore` trước, `Read` sau cho phần đồ thị cắt bớt).
>
> 📄 **Loại tài liệu:** `-arch` là **tài liệu bổ trợ**, **ngoài** mẫu 16 mục / BR / mục "Đối chiếu mô hình Nhanh" mà
> `tools/agents/check-feature-doc.ps1 -WP14` áp cho **SRS** `<mã>-<slug>.md`; `tools/kiem-tat-ca.ps1` **không** gọi công cụ đó.
> Vì vậy tệp này không có `## 0.` / `## 10.`–`## 15.` là **đúng chủng loại**, không phải thiếu mục (ORCH chốt 25/11/2026).
>
> 🔄 **N80 (26/11/2026):** đóng **16/16 P3** của DOCREV vòng 2 (dòng #2–#17) — chỉnh tài liệu khớp **code as-built N78**,
> **không** đổi thiết kế/quyết định. P2 #1 (probe runtime `D-233`) đã đóng bằng probe của `qa`.

---

## 1. Bối cảnh & phạm vi

### 1.1 Cái đã có (đọc từ code, không phải kế hoạch)

| Thành phần | Hiện trạng | Bằng chứng |
|---|---|---|
| Bảng ánh xạ | 1 bảng `AddrCarrierMappings` + `CarrierMappingStatus` 3 giá trị; unique `(TenantId, CarrierCode, CarrierAddressCode)` `NULLS NOT DISTINCT` | `modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressMapping.cs:58-68,82-108`; migration `20261014042922_Add_Addr002_CarrierMappings.cs` (SQL thô) |
| Lõi thuần đồng bộ | `LapKeHoach` + `ApDung`; **không bao giờ** tạo ánh xạ mới; 3 hàng rào `0506`/`0507`/`0508` | `modules/Address/eCommerce.Address.Domain/Sync/CarrierAddressMappingSynchronizer.cs:95-199,205-236` |
| Job | `CarrierAddressSyncWorker` mặc định TẮT, khoá theo hãng, chưa mở UoW, chưa ghi bản ghi lô, `catch (BusinessException)` gộp mọi mã thành "bị từ chối" | `modules/Address/eCommerce.Address.Application/Sync/CarrierAddressSyncWorker.cs:37-44,74-78,121-130,146-159,181-191` |
| API vận hành | **1** lớp `CarrierAddressMappingAppService`, `[Authorize]` cấp class, 4 method tiếng Việt; DTO khai **trong** tầng `Application`; `XacMinhAsync` **chưa** có permission riêng | `modules/Address/eCommerce.Address.Application/CarrierAddressMappingAppService.cs:10-29,44-45,63-112` |
| Tra cứu | `Tra` (chiều xuôi) fail-closed `0501`/`0502`/`0503`/`0504`; `ThuTra` mềm; **chưa có** chiều ngược | `modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressMappingResolver.cs:38-103,106-117` |
| Mã lỗi | `CarrierAddressErrorCodes` `0501…0508`; mã cao nhất **có trong code** = `0508` | `modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressMapping.cs:8-42` |
| Map HTTP | **0** mã `05xx` được map ở Host (đo bằng grep `CarrierAddressErrorCodes` trong `src/` ⇒ 0 kết quả) ⇒ mọi `BusinessException` của ADDR-002 đang rơi về **`403`** mặc định của ABP; `D-233` chốt bổ sung **6** dòng vào khối map | khối map hiện có: `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:299-378` |
| Đăng ký module/route | `AddressApplicationModule` đã nạp; conventional controller tự đăng ký assembly `eCommerce*.Application` | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:73,380-420` |

### 1.2 Cái WP-18 phải làm (IN)

1. Bảng bản ghi lô `AddrCarrierSyncRuns` (append-only) + bảng con `AddrCarrierSyncRunUnmappedCodes` (`D-83`/`D-88`/`D-90`).
2. Sửa lõi `LapKeHoach`: `Trim`/`Ordinal` (`D-84`), mã rỗng/`null` cùng hằng hàng rào (`D-91`), hàng rào 3 chỉ đếm **chuyển mới** (`D-87`), đổi tên ⇒ hạ `NghiNgo` (`D-86`).
3. Sửa worker: mọi lượt **đã giành khoá** ⇒ đúng 1 dòng lô; UoW `requiresNew` cho `DaApDung` và cho dòng `Loi`/`BiChan` (`D-90`).
4. Hai mã lỗi mới: `0509` (`D-84`/`D-91`, `DanhMucHangMaKhongHopLe`) và `0510` (`D-89`, `AnhXaNguocKhongDuyNhat`).
5. Tra ngược `ResolveCarrierAddressCodeAsync` + hàm thuần `TraNguoc` (`D-89`).
6. `GetUnmappedCodesAsync` + DTO (`D-88`).
7. Permission `eCommerce.Addresses.CarrierMapping.Verify` gắn `XacMinhAsync`, side `Host` (`D-85`/`D-92`).

### 1.3 Ngoài phạm vi (OUT — **không** thiết kế ở đây)

- Adapter GHN/GHTK (`IKhungDiaChiHangSource`) — SRS Q1/Q2/Q7 còn `Open`, **cấm suy đoán endpoint/trường NCC**.
- `ADDR-003`, PII, bảng ánh xạ 3↔2 cấp.
- Cảnh báo sai lệch nối `NOT-*` (WP-22); `SHP-004`/`ShpCarrierAddressMappings` (nợ F9 — BA khi rà `SHP`).
- Đường HTTP `DongBoAsync`/import file (khi mở phải qua `IdempotencyGuard` của `XC-002`); ETO của lô.
- `eCommerce.slnx`, `Directory.Packages.props`, multi-tenancy thật (GĐ4).

---

## 2. Ranh giới module

```
Host (eCommerce.HttpApi.Host)
  └─ depends on → Address.Application · Address.Application.Contracts · …
        Address.Application ──depends on──▶ Address.Domain
                              └──────────▶ Address.Application.Contracts
        Address.Domain  ──▶ (không phụ thuộc module nào khác; chỉ ABP + Domain.Shared)
        Address.Application ──▶ Domain.Shared (hằng permission/mã lỗi — `Address.Application.csproj:15`)
        Address.Application.Contracts ──▶ (chỉ `Volo.Abp.Ddd.Application.Contracts`; **KHÔNG** ref `Domain.Shared`)
```

| # | Luật áp cho WP-18 | Cách giữ |
|---|---|---|
| B1 | Module `Address` **sở hữu** cả 3 bảng (`AddrCarrierMappings`, `AddrCarrierSyncRuns`, bảng con) và mọi entity/mã lỗi `05xx` | tất cả đặt trong `modules/Address/eCommerce.Address.Domain`; **không** khai `05xx` ở `eCommerceConsts.ErrorCodes` (BR-015) |
| B2 | `SHP-001`/`SHP-004` **chỉ** lấy mã hãng qua hợp đồng của `Address`; **cấm** tự truy vấn `AddrCarrierMappings`/`AddrCarrierSyncRuns` (`D-89` ⑤) | hợp đồng hẹp `ICarrierAddressResolver` ở `eCommerce.Address.Application.Contracts` (mục 3.1, **`D-232`**); **không** có FK xuyên module |
| B3 | `Address` **không** tham chiếu `Shipping`, **không** tham chiếu `Host`/`Web` | chỉ `using` ABP + `eCommerce.Address.*` |
| B4 | Bảng con → bảng lô là FK **trong cùng module** (được phép); **không** FK tới `AddrCarrierMappings` (dòng con là sự kiện bất biến, `D-88`) | cấu hình EF ở `ConfigureAddress` |
| B5 | Mọi entity mới có `DbSet` trên DbContext hợp nhất (nếu thiếu ⇒ `IRepository<T>` **không** đăng ký, lỗi im lặng) | thêm 2 `DbSet` ở `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs`; `ConfigureAddress` **đã** được gọi trong `OnModelCreating` |
| B6 | Worker chạy **ngữ cảnh host**, `TenantId = NULL`, khoá **không** có `{tenantId}` (`D-92`) | `CarrierAddressSyncWorker.cs:121-124` giữ nguyên; 3 bảng giữ cột `TenantId` nullable + `IMultiTenant` |

**Vi phạm ranh giới hiện có: 0.** Cách `SHP-*` gọi vào đã chốt bằng hợp đồng hẹp (`D-232`); chưa có code `SHP` nên chưa vi phạm.

---

## 3. Hợp đồng

### 3.1 `Address.Application.Contracts` (mới)

> ⚠️ `D-85` cấm dựng `ICarrierAddressAdminAppService`/`ICarrierAddressLookupAppService` (2 interface phản chiếu toàn bộ lớp).
> Nó **không** nói gì về một hợp đồng **hẹp, chỉ đọc** cho bên tiêu thụ. Vì A2/M2 bắt buộc gọi chéo module qua
> `*.Application.Contracts`, nên **`D-232`** chốt **hợp đồng hẹp 1 method** (mục 3.1) — đã có hiệu lực, không còn chờ.

Tệp đề xuất: `modules/Address/eCommerce.Address.Application.Contracts/Carrier/CarrierAddressResolverContracts.cs`

| Kiểu | Chữ ký | Bên gọi | Breaking? |
|---|---|---|---|
| `ICarrierAddressResolver` | `Task<CarrierAddressCodeResultDto> ResolveCarrierAddressCodeAsync(ResolveCarrierAddressCodeRequest request)` | `SHP-001`/`SHP-004` (khi code) — hiện **0** bên gọi | không (mới) |
| `ResolveCarrierAddressCodeRequest` | `record(string CarrierCode, Guid OurAdministrativeUnitId)` | như trên | không |
| `CarrierAddressCodeResultDto` | `record(CarrierAddressMappingContractDto Mapping, AdministrativeUnitContractDto Unit)` — **giữ hình dạng của `D-89`** | như trên | không |
| `CarrierAddressMappingContractDto` | `record(Guid Id, string CarrierCode, string CarrierLevel, string CarrierAddressCode, string CarrierAddressName, Guid OurAdministrativeUnitId, int MatchMethod, int Status, DateTime SyncedAtUtc)` — **9** trường | như trên | không |
| `AdministrativeUnitContractDto` | `record(Guid Id, int SchemaVersion, int Level, string Code, string Name, string? ParentCode, bool IsActive)` — **7** trường | như trên | không |

- **`D-232` ①(1)–(3):** DTO giữ **hình dạng hỗn hợp `Mapping` + `Unit` của `D-89`** — **không** dùng DTO 12 trường phẳng;
  `CarrierAddressCodeResultDto` là kiểu app service trả **luôn** (Application → Contracts hợp lệ) ⇒ **một** DTO, không chép 2 bản.
  `TraMaHangAsync`/`KetQuaTraMaHangDto` **giữ nguyên**.
- Enum ở biên hợp đồng: `MatchMethod`/`Status`/`Level` là **`int`** (0/1/2…) — `Contracts` **không** được tham chiếu
  `eCommerce.Address.Domain` (`D-232` ②). Tên trường trùng **tên** DTO Application (`CarrierAddressMappingDto:10-19`,
  `AddressDtos.cs:4-11`) nhưng là bản sao biên hợp đồng, không có logic.
- **Hiện thực (as-built):** lớp **riêng** `CarrierAddressResolver : ICarrierAddressResolver, ITransientDependency`
  (`CarrierAddressResolver.cs:19`) trong `Address.Application` (khuôn `AddressDirectory.cs:24`, `CustomerDirectory.cs:28`)
  gọi hàm thuần `TraNguoc` rồi map bằng **mapper biên hợp đồng** `internal static` `ChuyenMapping`/`ChuyenDonVi`
  (`CarrierAddressResolver.cs:47-61`, trả DTO `Contracts`). App service **giữ mapper riêng** `private static Chuyen`/`ChuyenDonVi`
  (`CarrierAddressMappingAppService.cs:232-237`, trả DTO `Application`) — hai mapper **mỏng 1:1**, **không** chia sẻ lớp;
  **logic dùng chung** duy nhất là `TraNguoc`. **`CarrierAddressMappingAppService` KHÔNG kế thừa `ICarrierAddressResolver`**
  (`D-232` ①(4)(5)) — bài học POST-đọc đã đo ở `CustomerDirectory.cs:10-23`: app service hiện thực luôn hợp đồng in-process
  khiến method **đọc** bị ABP sinh `POST`; tách lớp `ITransientDependency` mới hết.
- **Đếm kiểu:** **5** = 1 interface + 2 DTO cấp 1 (`ResolveCarrierAddressCodeRequest`, `CarrierAddressCodeResultDto`) +
  2 DTO con (`CarrierAddressMappingContractDto` 9 trường, `AdministrativeUnitContractDto` 7 trường) — **không** phải "3 DTO".
- **Không** đưa `GetUnmappedCodesAsync`/`XacMinhAsync` vào `Contracts` (chỉ FE + Host dùng; ở lại `Application`).

### 3.2 `Address.Application` — method mới trong **cùng lớp** `CarrierAddressMappingAppService` (`D-85`)

| Method | Input → Output | Quyền | Ghi chú |
|---|---|---|---|
| `GetUnmappedCodesAsync` | `GetUnmappedCodesRequest(string CarrierCode, int SkipCount = 0, int MaxResultCount = 100)` → `UnmappedCodesResultDto` | `[Authorize]` cấp class (`Both`, `D-92` ④) | chỉ đọc; lấy lượt `DaApDung` gần nhất; trừ ánh xạ hiện có; `D-88` ③ |
| `ResolveCarrierAddressCodeAsync` | `ResolveCarrierAddressCodeRequest` → `CarrierAddressCodeResultDto` (DTO của `Contracts`, `D-232`) | `[Authorize]` cấp class (`Both`) | là method public của app service (**thêm method** = `D-89` ①(1); **không** kế thừa interface = `D-232` ①(5)) nhưng **uỷ quyền** cho `ICarrierAddressResolver` (inject) — **không** tự đọc repo |
| `XacMinhAsync` (sửa) | giữ chữ ký `(Guid id, CarrierMatchMethod? cachKhop = null)` | thêm `[Authorize(eCommerceConsts.Permissions.Addresses.CarrierMappingVerify)]` | siết quyền, không đổi route/DTO |

DTO mới (tầng `Application`, cùng tệp app service hoặc `CarrierSyncDtos.cs`):

```csharp
public sealed record GetUnmappedCodesRequest(string CarrierCode, int SkipCount = 0, int MaxResultCount = 100);

public sealed record UnmappedCarrierCodeDto(string CarrierLevel, string CarrierAddressCode, string CarrierAddressName);

public sealed record UnmappedCodesResultDto(
    IReadOnlyList<UnmappedCarrierCodeDto> Items,
    int TotalCount,
    Guid? SyncRunId,
    DateTime? FinishedAtUtc,
    CarrierAddressSyncOutcome? LatestRunOutcome);
```

- `CarrierAddressSyncOutcome` là enum **Domain** (`DaApDung = 0`, `BiChan = 1`, `Loi = 2`) — lưu DB, **chỉ thêm ở cuối** (`D-17`).
- Dùng `PagedResultRequestDto` của ABP hay record riêng: chọn **record riêng** để route là `GET` với query đơn giản và
  tránh `Sorting` không dùng; `D-88` chỉ ràng buộc "phân trang + `TotalCount`", không ràng buộc kiểu input.
- `D-235`: lớp này có **đúng 6** method public (`TraMaHangAsync`, `LayChuaXacMinhAsync`, `XacMinhAsync`, `PhatHienSaiLechAsync`,
  `GetUnmappedCodesAsync`, `ResolveCarrierAddressCodeAsync`); `GetSyncRunsAsync` (lịch sử lượt) là **additive sau**, cùng class,
  `[Authorize]` cấp class, **không** permission mới — **không** làm ở WP-18.

### 3.3 Permission

| Hằng | Giá trị | Định nghĩa | Side |
|---|---|---|---|
| `eCommerceConsts.Permissions.Addresses.CarrierMappingVerify` | `eCommerce.Addresses.CarrierMapping.Verify` | (a) thêm vào lớp `Addresses` — `src/eCommerce.Domain.Shared/eCommerceConsts.cs:363-376`; (b) `addresses.AddChild(…)` — `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:168-171` | `MultiTenancySides.Host` (`D-92` ④) |

- Khai **cả hai nơi** — thiếu một nơi ⇒ `403` cho **mọi** người kể cả `admin` (lỗi đã xảy ra thật, `XC-004` D2;
  lưới `BatBienPhanQuyenTests.TC02b`).
- Sau khi thêm phải chạy `DbMigrator` (binary mới build) để role `admin` được cấp (bài học `S6`).
- Nợ GĐ4: method **đọc** (`TraMaHangAsync`, `ResolveCarrierAddressCodeAsync`, `GetUnmappedCodesAsync`) phải chạy dưới
  `CurrentTenant.Change(null)` khi bật multi-tenancy thật (`D-92` ⑤) — **không** làm ở WP-18, ghi nợ.

### 3.4 Route & HTTP

| Method | Verb (ABP conventional) | Bên gọi |
|---|---|---|
| `GetUnmappedCodesAsync` | `GET` (tên bắt đầu `Get`) | FE màn vận hành |
| `ResolveCarrierAddressCodeAsync` | `POST` (mặc định) | `SHP-*` qua hợp đồng (không qua HTTP) |
| `XacMinhAsync` | `POST` | FE |
| `TraMaHangAsync`, `LayChuaXacMinhAsync`, `PhatHienSaiLechAsync` | giữ nguyên | FE/CSKH |

- Route do ABP sinh từ tên lớp + tên method (`eCommerceHttpApiHostModule.cs:380-420`); **đoạn đường dẫn cụ thể chưa xác minh**
  (chưa đọc `swagger.json`/`schema.d.ts` trong phiên N77). BE xác nhận bằng `pnpm gen:api` và ghi vào handover;
  **cấm** đặt `[Route]` tay.
- FE **0** lời gọi hiện tại tới lớp này — **đo 26/11/2026 (N80):**
  `grep -ri "carrier-address-mapping|CarrierAddressMapping|GetUnmappedCodes|XacMinhAsync" apps/admin-web` = **0** dòng
  ⇒ không phá vỡ FE. (Route `POST` của method public vẫn tồn tại về phía BE — xem §6.)

### 3.5 Mã lỗi & map HTTP

| Mã | Hằng | Nơi ném (sau WP-18) | Map HTTP đề xuất |
|---|---|---|---|
| `eCommerce:Address:0509` | `DanhMucHangMaKhongHopLe` (**mới**) | `CarrierAddressMappingSynchronizer`, vòng quét **trước** hàng rào 1 | job-only — **không** map |
| `eCommerce:Address:0510` | `AnhXaNguocKhongDuyNhat` (**mới**) | `CarrierAddressMappingResolver.TraNguoc` | `409 Conflict` |
| `0501` | `KhongTimThayMaHang` (có) | `Resolver.cs:61-66,89-92` | `404 NotFound` |
| `0502` | `MaChuaXacMinh` | `Resolver.cs:80-87` | `409 Conflict` |
| `0503` | `AnhXaTrungLap` | `Resolver.cs:68-76` | `409 Conflict` |
| `0504` | `DonViDichDaDong` | `Resolver.cs:94-100` | `409 Conflict` |
| `0505` | `ThieuThongTinBatBuoc` | `CarrierAddressMapping.cs:126-134`, `Resolver.cs:47-50` | `400 BadRequest` |
| `0506`/`0507`/`0508`/`0509` | hàng rào | `Synchronizer.cs:128-143,181-196` + vòng quét mới | job-only — **không** map (WP-18 không có đường HTTP gọi `LapKeHoach`) |

- **Đo được (vì sao phải sửa):** hiện **0** mã `05xx` được map (`CarrierAddressErrorCodes` grep trong `src/` = 0; khối `options.Map`
  `eCommerceHttpApiHostModule.cs:299-378` **không** có `05xx`) ⇒ mọi lỗi của ADDR-002 đang trả **`403`** (client đọc thành "không có quyền").
- **`D-233` (đã chốt):** thêm **một** khối `options.Map` "MÃ HÃNG — `ADDR-002`" vào
  `Configure<AbpExceptionHttpStatusCodeOptions>` (`eCommerceHttpApiHostModule.cs:299-378`) với **đúng 6** dòng:
  `0501→404`, `0502→409`, `0503→409`, `0504→409`, `0505→400`, `0510→409`. **Không** map `0506`/`0507`/`0508`/`0509` (job-only hôm nay);
  `0510` map **ngay** vì `ResolveCarrierAddressCodeAsync` vẫn là method public ⇒ ABP sinh route.
- **Đánh số:** `0509` = kế tiếp sau `0508` (mã cao nhất **có trong code**, `CarrierAddressMapping.cs:41`); `0510` = kế tiếp sau `0509`
  (`D-84` lấy trước, `D-89` lấy sau). **Cấm đánh số lại** (`D-79`); hai hằng chưa từng có trong code nên không phải đánh số lại.
- Hằng `DanhMucHangMaKhongHopLe` thay tên gợi ý cũ `DanhMucHangTrungMa` (`D-91`) — hằng chưa có nên đổi tên gợi ý không vướng luật.

---

## 4. Schema & migration

### 4.1 Bảng `AddrCarrierSyncRuns` (entity `CarrierAddressSyncRun`)

Sổ **append-only**, ghi một lần; **không** setter public, **không** method đổi, **không** UPDATE/DELETE.
Một dòng cho mỗi lượt của mỗi hãng **đã giành được khoá** (`D-90`).

| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| `Id` | `uuid` | ✖ | PK |
| `TenantId` | `uuid` | ✔ | `NULL` ở host (`D-92`); `IMultiTenant` |
| `CarrierCode` | `varchar(32)` | ✖ | `Trim().ToUpperInvariant()` như `CarrierAddressMapping.cs:140` |
| `Source` | `varchar(512)` | ✔ | `danhMuc.Nguon`; lỗi adapter trước khi có danh mục ⇒ định danh adapter, hoặc `null` nếu không biết |
| `SourceHash` | `varchar(64)` | ✔ | SHA-256 hex của danh mục **chuẩn hoá**. **Null khi:** (a) `Loi` — **mọi** trường hợp (`CarrierAddressSyncWorker.cs:278`); (b) `BiChan` xảy ra **trước** khi băm (`0509` ở bước quét `:174`). Có giá trị khi `BiChan` **sau** khi băm, hoặc `DaApDung` (`D-90`) |
| `StartedAtUtc` | `timestamptz` | ✖ | lấy **trước** `TaiDanhMucAsync` (`D-90`) |
| `FinishedAtUtc` | `timestamptz` | ✖ | |
| `TriggeredBy` | `varchar(128)` | ✖ | `job:CarrierAddressSyncWorker` (sau này: user id) |
| `Outcome` | `int` | ✖ | `0 DaApDung` · `1 BiChan` · `2 Loi`; CHECK `"Outcome" in (0,1,2)`; **chỉ thêm cuối** (`D-17`) |
| `ErrorCode` | `varchar(64)` | ✔ | `null` cho `DaApDung`/`Loi`; = mã đã NÉM cho `BiChan` (`0506`/`0507`/`0508`/`0509`) |
| `ErrorType` | `varchar(256)` | ✔ | `ex.GetType().FullName` khi `Loi`; **cấm** lưu `ex.Message`/stack/payload (BR-011) |
| `SoMaNhan` | `int` | ✖ | số đơn vị trong danh mục hãng (`danhMuc.DonVis.Count`) — số đếm dòng, **không** phải số lượng (`D-31` không áp dụng) |
| `SoCapNhatTen` | `int` | ✖ | = `KeHoachDongBoMaHang.SoCapNhatTen` (`Synchronizer.cs:62`) |
| `SoDanhDauNghiNgo` | `int` | ✖ | = `:63` |
| `SoCanNguoiAnhXa` | `int` | ✖ | = `:64`; **bằng** số dòng con `AddrCarrierSyncRunUnmappedCodes` (NFR-003, `D-88` ⑥) |
| `MauCanNguoiAnhXa` | `text[]` (EF `string[]?`) | ✔ | **`D-234`:** **≤ 20** `CarrierAddressCode` đã `Trim()`, khử trùng, sắp `Ordinal`; **`null`** khi 0 việc (không ghi `{}`); **không** chứa level/name (cặp đầy đủ ở bảng con). `SoCanNguoiAnhXa` có thể **lớn hơn** độ dài mảng (25 việc ⇒ mảng 20) |

**Index (tổng 2, tên ≤ 63 byte):** `IX_AddrCarrierSyncRuns_Tenant_Carrier_StartedAt` (lịch sử lượt) và
`IX_AddrCarrierSyncRuns_Tenant_Carrier_Outcome_FinishedAt` (tìm lượt `DaApDung` gần nhất — đường đọc của `GetUnmappedCodesAsync`).
**Không** unique index nào trên bảng này (`D-83`: không khoá idempotency riêng); cả 2 index **giữ** (`D-235`).
**Ghi chú tên (as-built 26/11/2026 — N80):** `D-235` ① nhắc tên **mô tả** dài (`IX_AddrCarrierSyncRuns_TenantId_CarrierCode_StartedAtUtc`,
65 byte) — chỉ để mô tả; **tên thật** là bản ≤ 63 byte ở trên, khớp migration `20261125193330_Add_Addr002_CarrierSyncRuns`.

### 4.2 Bảng `AddrCarrierSyncRunUnmappedCodes` (entity `CarrierAddressSyncRunUnmappedCode`)

`D-88`: **không** phải aggregate root, **không** navigation collection trên `CarrierAddressSyncRun` (liệt kê lượt không kéo ~3.000 dòng).

| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| `Id` | `uuid` | ✖ | PK |
| `TenantId` | `uuid` | ✔ | `NULL` ở host; `IMultiTenant` |
| `SyncRunId` | `uuid` | ✖ | FK → `AddrCarrierSyncRuns(Id)`, `onDelete: Restrict` (sổ bất biến; **không** cascade) |
| `CarrierCode` | `varchar(32)` | ✖ | |
| `CarrierLevel` | `varchar(32)` | ✖ | `Trim().ToUpperInvariant()` — mã của ta, ngoài BR-013 |
| `CarrierAddressCode` | `varchar(64)` | ✖ | **đã `Trim()`**, so `Ordinal` (`D-84`) |
| `CarrierAddressName` | `varchar(256)` | ✖ | tên phía hãng |

**Ràng buộc:**
- UNIQUE `(TenantId, SyncRunId, CarrierAddressCode)` — **SQL thô `NULLS NOT DISTINCT`** (EF Core không mô hình hoá được;
  khuôn migration `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs`). Tên (`≤ 63` byte):
  `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code`.
- INDEX `(SyncRunId, CarrierLevel, CarrierAddressCode)` — tên `IX_AddrCarrierSyncRunUnmappedCodes_Sync_Level_Code`
  (tên dài `…_SyncRunId_CarrierLevel_CarrierAddressCode` = 76 byte ⇒ PostgreSQL **cắt ngầm**; cấm đặt dài quá 63).
- **Bắt buộc** cập nhật `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs`: hằng số đếm index + danh sách **tên đích danh**
  (M8: chỉ đếm tổng thì thêm 1 index + hỏng 1 index cũ vẫn xanh).

### 4.3 Migration plan (expand-only) và đường lùi

| Bước | Việc | Ai | Ghi chú |
|---|---|---|---|
| 1. Expand | Sửa model/`ConfigureAddress` + **2 `DbSet`** trên `eCommerceDbContext`; `dotnet ef migrations add Add_Addr002_CarrierSyncRuns` | **Một** chủ migration = luồng BE | tệp do EF sinh tạo **2 bảng mới** + **3 `CreateIndex`** (bảng lô 2, bảng con 1 không-unique) + CHECK; **thay** `CreateIndex(unique: true)` (EF sinh cho unique bảng con) bằng `migrationBuilder.Sql("CREATE UNIQUE INDEX … NULLS NOT DISTINCT;")` — **không** để cả hai (as-built `20261125193330`: chỉ có `Sql`, **0** `CreateIndex(unique)`). `MauCanNguoiAnhXa` = `text[]` (Npgsql map `string[]` sẵn, **0** converter — `D-234`) |
| 2. Kiểm diff | Chỉ `CREATE TABLE`/`CREATE INDEX`/CHECK; **0** `DROP`/`ALTER` trên bảng có dữ liệu | BE + review | `ErrorType`/`SourceHash` nullable đặt trên **bảng mới** ⇒ không đụng `AddrCarrierMappings` |
| 3. Áp | `dotnet build` rồi `dotnet run --project src/eCommerce.DbMigrator` (binary mới); kiểm `__EFMigrationsHistory` + `\d` hai bảng | BE/OPS | **cấm** `dotnet ef database update` (M6) |
| 4. Contract | **Không có** ở WP-18 (không bỏ cột/đổi kiểu gì) | — | |
| Đường lùi | (a) tắt cờ `Address:CarrierSync:Enabled=false` (`CarrierAddressSyncWorker.cs:37`) ⇒ job ngừng ghi, hệ thống vận hành như trước; (b) trước merge: `git revert` + `migrations remove`; sau merge: **migration mới** `Drop` bảng con rồi bảng lô (`Down()` của migration gốc làm đúng thứ tự); (c) bảng mới **không** ảnh hưởng `AddrCarrierMappings` — dữ liệu lô là nhật ký, mất không mất ánh xạ. | BE | `pg_dump` **BẮT BUỘC** trước khi áp (`ef-migration-postgres` M9: chạm DB có dữ liệu) — as-built N78 `artifacts/pg-dump/N78-W18-2.dump` |
| Backfill | **Không** — bảng mới | — | lượt đầu sau khi bật sinh ~3.000 dòng con (`D-88`) |

- **Thứ tự triển khai bắt buộc:** migration trước, code sau. Code ghi bản ghi lô khi bảng chưa có ⇒ mỗi lượt thành `Loi`
  (`ErrorType = PostgresException`) và **không** áp dụng thay đổi — nhưng đó là trạng thái tệ hơn "không ghi lô": phải chạy
  `DbMigrator` **trước** khi bật cờ.
- **Không** seed; **không** đổi `AddrCarrierMappings`; **không** đổi `eCommerce.slnx`/`Directory.Packages.props`.

---

## 5. Luồng

### 5.1 Lượt đồng bộ (worker) — sửa theo `D-90`/`D-87`/`D-84`/`D-91`

```
DoWorkAsync (:57-114)
  ├─ Enabled=false / Carriers rỗng            → return (không dòng)        [giữ]
  ├─ hãng không có adapter (:101-110)          → log Warning, continue      [giữ; 0 dòng]
  └─ DongBoMotHangAsync (:116-192)
       ├─ TryAcquireAsync("address:carrier-sync:{hãng}", 5 s) (:124)
       │    └─ null → log Warning, return                    [giữ; 0 dòng]
       ├─ startedAtUtc = clock.Now            ← TRƯỚC adapter
       ├─ try
       │   ├─ danhMuc = adapter.TaiDanhMucAsync()            [ngoài transaction, D12]
       │   ├─ hienCo  = repo.GetListAsync() lọc hãng         [:138-141]
       │   ├─ daChuanHoa = ChuanHoaVaKiemMa(danhMuc)         ← QUÉT D-84/D-91 TRƯỚC KHI BĂM
       │   │     └─ ném 0509 (soMaRong/soMaTrung) ⇒ BiChan; mã đã Trim, không còn null/rỗng/trùng
       │   ├─ sourceHash = Băm(daChuanHoa, carrierCode)      (mục 5.3 — không còn null)
       │   ├─ keHoach = LapKeHoach(hang, hienCo, danhMuc with { DonVis = daChuanHoa })
       │   │                 ── ném 0506/0507/0508 (danh mục ĐÃ chuẩn hoá; `LapKeHoach` quét lại — idempotent)
       │   │     └─ KHÔNG còn early-return khi 0 việc [:146-152] — 0 việc VẪN ghi dòng DaApDung
       │   ├─ UoW #1 Begin(requiresNew: true, isTransactional: true)
       │   │     ├─ canCapNhat = ApDung(keHoach, now)
       │   │     ├─ UpdateManyAsync(canCapNhat, autoSave: false)   [bỏ autoSave: true ở :158]
       │   │     ├─ InsertAsync(run)  Outcome=DaApDung, 4 số đếm, MauCanNguoiAnhXa
       │   │     ├─ InsertManyAsync(dòng con cho mỗi việc CanNguoiAnhXa)   [0..n]
       │   │     └─ CompleteAsync()  ← MỘT lần
       │   └─ log Information (giữ) + log Warning khi SoCanNguoiAnhXa > 0 [:161-179]
       └─ catch
           ├─ BusinessException có Code ∈ {0506,0507,0508,0509}   → Outcome=BiChan, ErrorCode=ex.Code, ErrorType=null
           ├─ mọi exception khác (kể cả BusinessException mã khác, HttpRequestException, TaskCanceledException, NpgsqlException)
           │                                        → Outcome=Loi, ErrorCode=null, SourceHash=null, ErrorType=ex.GetType().FullName,
           │                                          4 số đếm = 0, 0 dòng con
           └─ Ghi dòng bằng UoW #2 Begin(requiresNew: true, isTransactional: true), CancellationToken.None
                 └─ ghi thất bại ⇒ log Error RIÊNG, không che exception gốc, KHÔNG ném ra khỏi worker
```

- **Phân loại `BiChan` theo mã** — bỏ `catch (BusinessException)` gộp `:181-187`: adapter ném `BusinessException` mã khác
  **không** phải "bị hàng rào chặn" (`D-90`).
- Ngoại lệ khi ghi dòng `DaApDung` **trong** UoW #1: rollback cả `UpdateManyAsync` ⇒ **0** ánh xạ đổi (TC-07). Đây là
  lý do **bắt buộc** UoW do worker tự mở (BR-014) — `IRepository` mặc định dùng UoW của lời gọi, background worker **không**
  có UoW sẵn.
- `MauCanNguoiAnhXa` lấy từ `keHoach.Viec` (CanNguoiAnhXa) `Take(20)`, chỉ **mã** đã `Trim`, khử trùng, sắp `Ordinal`, `null` khi 0 việc
  (`D-234`); dòng con lấy **đủ** — cần `CarrierLevel`: thêm `string? CarrierLevel` vào record nội bộ `ViecMaHang`
  (`Synchronizer.cs:52-57`), điền từ `DonViDiaChiHang` của danh mục. Record này **không** phải hợp đồng công khai nên additive an toàn.
- **Thứ tự bắt buộc (P1 vòng DOCREV 1):** quét `D-84`/`D-91` ⇒ băm ⇒ `LapKeHoach`. Băm **trước** khi quét (như bản trước) sẽ gặp
  phần tử/mã `null` ⇒ `NullReferenceException` ⇒ `Loi` thay vì `BiChan 0509`.
- Hãng cấu hình nhưng **không** có adapter ⇒ **0** dòng; `SyncRunId` và `LatestRunOutcome` cùng `null` ⇒ "chưa có lượt đồng bộ" (`D-236`).

### 5.2 Lõi `LapKeHoach` — sửa theo `D-84`/`D-87`/`D-91`

Thứ tự bắt buộc (mọi bước trước khi trả `KeHoachDongBoMaHang`):

1. **Khớp mã hãng** giữ nguyên (`:105-115`; `Ordinal` sau `Trim().ToUpperInvariant()`).
2. **Vòng quét mã** (mới, `D-84`+`D-91`): **một hiện thực** `CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(DanhMucDiaChiHang) → IReadOnlyList<DonViDiaChiHang>`
   (`Synchronizer.cs:116-162`). **As-built gọi 2 lần, idempotent:** worker gọi **trước** khi băm (`CarrierAddressSyncWorker.cs:174`),
   rồi `LapKeHoach` gọi lại ở **đầu** hàm (`Synchronizer.cs:193`) trên cùng danh mục đã sạch — lần 2 không ném, không đổi dữ liệu;
   **không** có hai logic (đề nghị #3 của DOCREV "(a) đổi kiểu" / "(b) bỏ lời gọi trùng" **không** được chọn). Chữ ký as-built:
   `LapKeHoach(string carrierCode, IReadOnlyList<CarrierAddressMapping> anhXaHienCo, DanhMucDiaChiHang danhMuc, …)`
   (`Synchronizer.cs:164-169`) ⇒ nhận **`DanhMucDiaChiHang`** (worker truyền `danhMuc with { DonVis = daChuanHoa }`), **không** nhận `IReadOnlyList`.
   Duyệt `danhMuc.DonVis` **một lần**:
   - phần tử `null` **hoặc** `CarrierAddressCode` `IsNullOrWhiteSpace` ⇒ `soMaRong++` (**không** bỏ qua dòng);
   - mã `Trim()` (không chuẩn hoá gì khác) ⇒ gom trùng `Ordinal` (giữ hoa–thường, số 0 đầu, khoảng trắng giữa);
   - `soMaRong > 0` **hoặc** có mã trùng ⇒ **NÉM MỘT** `BusinessException(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe)`
     với `WithData("soMaRong", …)`, `WithData("soMaTrung", …)`, `WithData("maTrung", ≤20 mã sắp Ordinal)` — **0** việc, từ chối cả lô.
   - Thay `theoMa[dv.CarrierAddressCode.Trim()] = dv` (`:121-126`, ghi đè im lặng) bằng dựng dictionary **sau** khi đã kiểm trùng.
3. **Hàng rào 1** (`:128-134`) rỗng / **hàng rào 2** (`:136-143`) < 5 — giữ; danh mục toàn mã rỗng ⇒ `0509` (bước 2), **không** `0506`.
4. **① Tên đổi** (`:149-161`): giữ — vẫn sinh việc cho **mọi** ánh xạ kể cả `NghiNgo` (tên mới giúp người xác minh), so `Ordinal` sau `Trim`.
5. **② Mã mới** (`:163-169`): giữ (`CanNguoiAnhXa`, không tạo bản ghi).
6. **③ Mã biến mất** (`:171-176`): **chỉ** sinh `DanhDauNghiNgo` khi `x.Status != NghiNgo` (`D-87` ①(1)) — ánh xạ đã `NghiNgo`
   vắng mặt ⇒ **không** việc, **không** đếm (vẫn còn bản ghi, vẫn tra được).
7. **Hàng rào 3** (`:178-196`):
   - tử số `soNghiNgo` = số việc `CapNhatTen`/`DanhDauNghiNgo` có `HienCo!.Status != NghiNgo`;
   - mẫu số `tongDangDung` = `cuaTa.Count(x => x.Status != NghiNgo)`;
   - ngưỡng 0.20, so `>`; mẫu số 0 ⇒ bỏ hàng rào; `WithData` thêm `soDaNghiNgoTruoc` (additive).
   - ⛔ **không** cờ vượt, **không** permission "duyệt vượt 0508".
8. **Áp dụng** (`ApDung`, `:205-236`): giữ; `CapNhatTuDongBo` tự hạ `NghiNgo` khi tên đổi (`CarrierAddressMapping.cs:177-188`, `D-86`).

> **Đừng nhầm hai con số:** `SoCapNhatTen`/`SoDanhDauNghiNgo` của **dòng lô** = **số việc** (NFR-003).
> `SoCapNhatTen` **gồm cả** việc trên ánh xạ đã `NghiNgo` (`Synchronizer.cs:226-232`); `SoDanhDauNghiNgo` **chỉ** sinh
> cho ánh xạ **chưa** `NghiNgo` (`D-87` ①(1), `Synchronizer.cs:247-253`) ⇒ **không** gồm. Còn **tử số hàng rào 3** chỉ đếm
> việc có `Status != NghiNgo`. Các số khác nhau là **đúng**; test phải khẳng định riêng.

### 5.3 `SourceHash`

- Đầu vào: `CarrierCode.Trim().ToUpperInvariant()` + mỗi `DonViDiaChiHang` của danh mục **đã qua `ChuanHoaVaKiemMa`**
  (không còn phần tử `null`, mã `null`/rỗng/trùng) ⇒ **không** dùng `?.` trên mã: `(CarrierLevel.Trim().ToUpperInvariant(),
  CarrierAddressCode /* đã Trim */, CarrierAddressName?.Trim() ?? "")`. Tên `null` vẫn hợp lệ (`D-91`: chỉ mã + phần tử `null`
  thuộc hàng rào; tên `null` giữ như code `Synchronizer.cs:156`).
- Lưu `varchar(64)` (hex SHA-256) — **theo thiết kế** `ADDR-001-arch.md:222` (cột `AddrCatalogSyncRuns.SourceHash` **có**
  CHECK `~ '^[0-9a-f]{64}$'`). ⚠️ **as-built 26/11/2026 (N80):** cột `AddrCarrierSyncRuns.SourceHash` **chưa** có CHECK hex
  (migration `20261125193330` chỉ có CHECK `Outcome`) — muốn đồng bộ ⇒ **migration mới** (additive), **không** sửa migration đã
  merge (M4). Cột nullable (`D-90`, điều kiện null ở §4.1).
- Sắp theo `(CarrierLevel, CarrierAddressCode)` `Ordinal`; nối dòng bằng `\t`, dòng bằng `\n`, UTF-8; SHA-256 → hex thường 64 ký tự.
- **Ổn định** qua lần chạy lại cùng danh mục, **không** phụ thuộc thứ tự dòng nguồn (TC-04); **cấm** `Guid.NewGuid()`/thời điểm/`Source`.
- Tên đổi ⇒ hash đổi — đúng nghĩa "danh mục nguồn khác đi"; **không** dùng hash làm khoá idempotency (không có bảng key, `D-83`).

### 5.4 Tra ngược `TraNguoc` (`D-89`) + `ResolveCarrierAddressCodeAsync`

Hàm thuần mới cạnh `Tra` (`CarrierAddressMappingResolver.cs:38`):

```csharp
public static KetQuaTraNguoc TraNguoc(
    IEnumerable<CarrierAddressMapping> anhXa,
    IEnumerable<AdministrativeUnit> donVi,
    string carrierCode,
    Guid ourAdministrativeUnitId)
```

Thứ tự luật (**fail-closed**, không trả `null`, không chọn bừa):

| # | Điều kiện | Kết quả |
|---|---|---|
| 1 | `IsNullOrWhiteSpace(carrierCode)` **hoặc** `ourAdministrativeUnitId == Guid.Empty` | NÉM `0505` |
| 2 | không thấy `donVi` có `Id == ourAdministrativeUnitId` | NÉM `0501` (`lyDo = "không tìm thấy đơn vị đích theo Id"`) |
| 3 | `donVi.IsActive == false` | NÉM `0504` |
| 4 | ứng viên = ánh xạ cùng hãng (**mọi** `CarrierLevel`) **và** `OurAdministrativeUnitId == đích`; rỗng | NÉM `0501` (`soAnhXa = 0`) |
| 5 | số ứng viên `Status == DaXacMinh` **> 1** | **NÉM `0510`** với `WithData("carrierCode")`, `("ourAdministrativeUnitId")`, `("soAnhXa")`, `("carrierAddressCode", ≤20 mã sắp Ordinal)` — **cấm** trả cái đầu/mới nhất |
| 6 | 0 ứng viên `DaXacMinh` (chỉ `ChuaXacMinh`/`NghiNgo`) | NÉM `0502` (`soAnhXa`, `trangThai`) |
| 7 | đúng 1 `DaXacMinh` | trả nó + đơn vị đích |

- Ứng viên `ChuaXacMinh`/`NghiNgo` khác **không** gây mơ hồ (cùng luật "chỉ `DaXacMinh` dùng cho vận đơn" của `Tra` `:80-87`).
- So mã hãng `OrdinalIgnoreCase` sau `Trim().ToUpperInvariant()` như `Tra` `:52`; `OurAdministrativeUnitId` so `Guid` chính xác.
- **Không** thêm unique/partial unique `(TenantId, CarrierCode, OurAdministrativeUnitId)` — dữ liệu nhiều-một **hợp lệ**, và lỗi
  nghiệp vụ phải là `BusinessException` chứ không phải `UniqueViolation` (`D-89` ②).
- App service: `ResolveCarrierAddressCodeAsync` **uỷ quyền** cho `ICarrierAddressResolver` (`CarrierAddressResolver` đọc
  `_anhXa.GetListAsync()` + `_donVi.GetListAsync()` rồi gọi `TraNguoc`), map sang `CarrierAddressCodeResultDto`
  bằng **mapper biên hợp đồng** `internal static` trên `CarrierAddressResolver` (`CarrierAddressResolver.cs:47-61`) —
  app service giữ mapper riêng cho DTO `Application` (`CarrierAddressMappingAppService.cs:232-237`); **logic dùng chung**
  là `TraNguoc`, **không** có logic thứ hai (`D-232` ①(4)(5)).
  **Bên gọi:** `SHP-001`/`SHP-004` **chỉ** qua `ICarrierAddressResolver` (mục 3.1) — không tự query bảng.

### 5.5 `GetUnmappedCodesAsync` (`D-88`)

1. Chuẩn hoá `CarrierCode` (`Trim().ToUpperInvariant()`); rỗng ⇒ trả rỗng? **Không** — kiểu DTO không nullable; rỗng ⇒ NÉM `0505`
   (nhất quán `Tra` `:47-50`; tránh "rỗng trả cả bảng").
2. Lượt nguồn = dòng `AddrCarrierSyncRuns` của hãng, `Outcome == DaApDung`, sắp `FinishedAtUtc DESC, StartedAtUtc DESC, Id DESC` ⇒ lấy 1
   (dùng index `(TenantId, CarrierCode, Outcome, FinishedAtUtc)`); không có ⇒ `Items = []`, `TotalCount = 0`, `SyncRunId = null`,
   `FinishedAtUtc = null`, **không** NÉM.
3. `LatestRunOutcome` = `Outcome` của dòng **mới nhất** của hãng (mọi `Outcome`, cùng thứ tự) — để UI cảnh báo lượt gần nhất `BiChan`/`Loi`.
   `D-236`: `SyncRunId = null` **và** `LatestRunOutcome = null` nghĩa là "chưa có lượt đồng bộ" (job chưa chạy / chưa có adapter /
   bận khoá / `Enabled=false`) — **trạng thái cấu hình**, không phải kết quả nghiệp vụ; **không** ném, **không** thêm enum.
4. Đọc **toàn bộ** dòng con của lượt nguồn (≤ số mã danh mục hãng, thực tế ~3.000 — không phải bảng lớn), sắp
   `(CarrierLevel, CarrierAddressCode)` `Ordinal`.
5. Trừ mã đã có ánh xạ **tại lúc đọc**: tập `cuaTa` = `_anhXa` lọc `(TenantId, CarrierCode)`, so `Ordinal` sau `Trim`.
   `TotalCount` tính **sau** khi trừ; rồi phân trang trong bộ nhớ (`Skip/Max`), kẹp `MaxResultCount` ≤ 1000.
6. **0** câu UPDATE/DELETE; **không** gọi adapter hãng.

> Ghi chú hiệu năng: bước 4-5 là đọc + lọc trong bộ nhớ trên tập bị chặn bởi kích thước danh mục hãng (NFR-001: 1 lượt ~3.000 đơn vị).
> Nếu sau này danh mục lớn hơn nhiều, chuyển sang anti-join SQL (thêm index theo `CarrierAddressCode`) — additive, không đổi hợp đồng.

---

## 6. Hợp đồng FE tối thiểu (màn vận hành, §10/§17 #3)

Chỉ mô tả endpoint/DTO/trạng thái UI — **không** thiết kế màn hình chi tiết.

| Vùng UI | Dữ liệu | Endpoint / nguồn | Trạng thái UI bắt buộc |
|---|---|---|---|
| Danh sách ánh xạ cần xử lý | `CarrierAddressMappingDto` | `LayChuaXacMinhAsync` (có) | rỗng ⇒ "không có việc"; lỗi `05xx` hiển thị **nguyên mã + ý nghĩa**, không nuốt thành "lỗi hệ thống" (từ `D-233` các mã reachable đã có HTTP đúng: `404`/`409`/`400`, không còn `403`) |
| Nút **Xác nhận** | gọi `XacMinhAsync(id)` | (có, sau WP-18 cần permission) | `403` ⇒ "bạn không có quyền xác minh" (không phải "lỗi hệ thống"); ẩn/disable nút khi thiếu permission |
| Danh sách mã mới cần ánh xạ | `UnmappedCodesResultDto` | `GET /api/app/carrier-address-mapping/…` (**đường dẫn cụ thể xác nhận bằng `pnpm gen:api`**) | bảng + `TotalCount`; `TotalCount` có thể **nhỏ hơn** `SoCanNguoiAnhXa` (đã trừ ánh xạ tạo sau lượt) ⇒ ghi rõ "hiển thị N/total"; `SyncRunId = null` **và** `LatestRunOutcome = null` ⇒ "chưa có lượt đồng bộ" (`D-236`) |
| Cảnh báo lượt gần nhất | `LatestRunOutcome` (0/1/2) | trong `UnmappedCodesResultDto` | `1 BiChan`/`2 Loi` mà vẫn có danh sách cũ ⇒ banner "lượt mới nhất KHÔNG áp dụng"; mã lỗi của lượt lấy từ log/psql ở WP-18 (chưa có trường `ErrorCode` trong DTO) |
| Nút **Đồng bộ ngay** / **Nạp file** | — | ⬜ **chưa có method** — **không** làm ở WP-18 | không hiển thị |

- Enum trả về dạng **số** (0/1/2) như các DTO hiện có; FE map nhãn.
- **Không** endpoint nào cho FE gọi adapter hãng. `ResolveCarrierAddressCodeAsync` là **method public** của app service
  (`CarrierAddressMappingAppService.cs:159`) ⇒ ABP conventional controller **sinh route `POST`** (đúng lý do `D-233` ① map
  `0510`); **FE không dùng** route này — bên gọi hợp lệ duy nhất là `SHP-*` **qua hợp đồng** `ICarrierAddressResolver`
  (in-process), **không** qua HTTP.

---

## 7. Test / QA plan

### 7.1 Theo tầng

| Tầng | Ca | Phủ |
|---|---|---|
| Domain thuần (`AddressSync/CarrierAddressMappingSynchronizerTests`) | mã `"0012Ab"` lưu/so đúng; `" P1 "` và `"P1"` ⇒ NÉM `0509`, `soMaTrung = 2`, `KeHoach` **không** tạo; mã `null`/`"   "`/phần tử `null` ⇒ **cùng** `0509`, `soMaRong` đúng, **không** NRE, **không** `0505`; danh mục toàn rỗng ⇒ `0509` (không `0506`); vừa rỗng vừa trùng ⇒ **một** exception mang cả hai | BR-013, `D-84`/`D-91` |
| Domain thuần | 100 ánh xạ `DaXacMinh`: lượt 1 thiếu 15 ⇒ áp (15 `NghiNgo`); lượt 2 thiếu thêm 6 ⇒ **không** NÉM, `SoDanhDauNghiNgo = 6` (6/85); lượt 3 cùng danh mục ⇒ `SoDanhDauNghiNgo = 0`; 10 ánh xạ / 8 đã `NghiNgo` / 2 vắng ⇒ `0508` với `soNghiNgo = 2`, `tongDangDung = 2`; ánh xạ `NghiNgo` đổi tên ⇒ có `CapNhatTen` nhưng **không** vào tử số | BR-005, NFR-005, `D-87` |
| Domain thuần | `TraNguoc`: 0 ứng viên ⇒ `0501`; đích không tồn tại ⇒ `0501`; `IsActive=false` ⇒ `0504`; `Guid.Empty`/hãng rỗng ⇒ `0505`; chỉ `ChuaXacMinh`/`NghiNgo` ⇒ `0502`; **2 `DaXacMinh` cùng đích ⇒ `0510`, `soAnhXa = 2`**; 1 `DaXacMinh` + 1 `NghiNgo` ⇒ trả `DaXacMinh`; ánh xạ hãng khác cùng đích không tính | BR-009, `D-89` |
| Domain (reflection/quét) | `CarrierAddressSyncRun`/`…UnmappedCode`: **0** public setter, **0** method đổi; **0** `UPDATE`/`DELETE` trong `modules/Address`; **0** trường `decimal`/`Money`; mã lỗi `0509`/`0510` đúng giá trị, không trùng `AddressErrorCodes`; **0** hằng `05xx` ở `eCommerceConsts.ErrorCodes`; **0** `https://`/endpoint hãng hard-code | BR-011/012/015/016, NFR-004/010 |
| Application (worker, adapter giả + khoá giả) | `DaApDung` 0 việc ⇒ **1** dòng, 4 số = 0, 0 dòng con; **`InsertAsync` dòng lô ném ⇒ rollback ⇒ 0 ánh xạ đổi** (ca `DaApDung_ghi_dong_lo_nem_thi_0_anh_xa_doi` — ca để NC-2 đỏ được); `BiChan` `0506/0507/0508/0509` ⇒ 1 dòng, `ErrorCode` = mã, `ErrorType = null`; adapter ném `HttpRequestException` ⇒ `Loi`, `ErrorCode = null`, `SourceHash = null`, `ErrorType = "System.Net.Http.HttpRequestException"`, số đếm 0; adapter ném `BusinessException` mã ngoài hàng rào ⇒ `Loi` (**không** `BiChan`); bận khoá / không adapter ⇒ **0** dòng; thông điệp chứa `token=SECRET` ⇒ **không** cột nào chứa `SECRET`; ghi dòng lô ném ⇒ worker **không** ném ra ngoài, có log `Error` riêng | BR-003/014, NFR-003, `D-90` |
| Kiến trúc/reflection (`D-232`…`D-237`) | `eCommerce.Address.Application.Contracts` **0** tham chiếu `Address.Domain`/`Address.Application`; `ICarrierAddressResolver` **đúng 1** method; `CarrierAddressCodeResultDto` **đúng 2** thuộc tính `Mapping`/`Unit`, 2 DTO con đủ **9**/**7** trường trùng tên DTO Application; `CarrierAddressMappingAppService` **không** kế thừa `ICarrierAddressResolver` và có **đúng 6** method public; `CarrierAddressSyncOutcome` **đúng 3** giá trị; `CarrierAddressSyncRun` **0** thuộc tính tên cũ/mới; cột `MauCanNguoiAnhXa` `data_type = ARRAY`, `udt_name = _text`; quét `options.Map` thấy **đúng 6** hằng `05xx` (0501/0502/0503/0504/0505/0510) | `D-232`/`D-233`/`D-234`/`D-235`/`D-236`/`D-237` |
| Application (app service) | `GetUnmappedCodesAsync`: 3.000 dòng con ⇒ `TotalCount = 3.000`; tạo 1 ánh xạ ⇒ `2.999`; lượt mới nhất `BiChan` ⇒ vẫn trả danh sách lượt `DaApDung` + `LatestRunOutcome = 1`; chưa có lượt `DaApDung` ⇒ rỗng + `SyncRunId = null`, không NÉM; phân trang không trùng/không sót; hãng rỗng ⇒ `0505` | BR-002/005, `D-88` |
| Endpoint (Host thật hoặc `WebApplicationFactory`) | `GET` `GetUnmappedCodesAsync`: 401 (không token) / 200 (có token) ; `POST` `XacMinhAsync`: **403** khi thiếu `Addresses.CarrierMapping.Verify`, 200 khi có (user role `admin` sau khi `DbMigrator` cấp) | BR-008, `D-85` |
| Integration (PostgreSQL) | chèn 2 dòng con cùng `(NULL, SyncRunId, 'P1')` ⇒ `UniqueViolation` (unique `NULLS NOT DISTINCT`); sau 1 lượt worker, mọi dòng ánh xạ/lô/dòng con có `TenantId IS NULL`; `PostgreSqlSchemaTests` cập nhật số đếm + tên index ⇒ xanh | BR-007, NFR-006, `D-88`/`D-92` |
| UI | **Không** có ca UI trong WP-18 (chỉ hợp đồng FE ở §6) | — |

> **Phủ BR còn lại (theo SRS):** BR-001/004/006/010 đã phủ bởi `TC-21`/`TC-04`/`TC-09`/`TC-14–15` của SRS `addr-002`
> (không chép lại ở đây); cột "Phủ" trên chỉ liệt kê ca **bổ sung/đổi** ở WP-18. DOCREV vòng 2 #16 sửa `BR-012/015/016`
> → `BR-011/012/015/016` cho ca "0 `https://`/endpoint hãng hard-code" (= BR-011).

### 7.2 Negative control **bắt buộc** (đột biến phải **đỏ**, rồi khôi phục **xanh**, ghi bằng chứng vào testplan)

| # | Đột biến | Ca phải đỏ |
|---|---|---|
| NC-1 | Trả hàng rào 3 về đếm **lặp** (tử số gồm cả ánh xạ đã `NghiNgo`, mẫu số = toàn bộ) | ca "lượt 1 thiếu 15, lượt 2 thiếu thêm 6 ⇒ không NÉM" (21/100 ⇒ `0508`) |
| NC-2 | Bỏ UoW `requiresNew` khi ghi dòng lô `DaApDung` (dùng `InsertAsync(autoSave: true)` rời) | ca `DaApDung_ghi_dong_lo_nem_thi_0_anh_xa_doi` — "ghi dòng lô ném ⇒ **0** ánh xạ đổi" (không UoW: ánh xạ đã đổi, dòng lô mất) |
| NC-3 | `TraNguoc` trả ứng viên **đầu tiên** thay vì NÉM khi > 1 `DaXacMinh` | ca "2 `DaXacMinh` cùng đích ⇒ `0510`, `soAnhXa = 2`" |
| NC-4 (khuyến nghị) | Phân loại `BiChan` gộp mọi `BusinessException` (như code cũ `:181-187`) | ca "adapter ném `BusinessException` mã ngoài hàng rào ⇒ `Loi`" |
| NC-5 (khuyến nghị) | Bỏ `Trim`/đổi sang `OrdinalIgnoreCase` khi so mã | ca "`0012ab` ⇒ `0501`" và "` P1 ` ⇒ `0509`" |

Chạy bằng `tools/agents/negative-control.ps1` (ORCH chạy lại, không tin lời vai).

---

## 8. Rủi ro

| # | Rủi ro | Hỏng kiểu | Giảm thiểu / xem lại |
|---|---|---|---|
| R1 | ~~**Không** map `05xx` ở Host ⇒ mọi lỗi ADDR-002 trả `403`~~ — **ĐÓNG bởi `D-233`**: map **6** mã reachable (`0501`→404, `0502`/`0503`/`0504`/`0510`→409, `0505`→400); `0506`–`0509` job-only ở WP-18 | — | theo dõi khi mở đường HTTP đồng bộ/nạp file (lúc đó cân nhắc map thêm, mã mới phải qua `D-xx`) |
| R2 | Hợp đồng cho `SHP` đặt sai chỗ (`SHP` tham chiếu `Address.Application`) | vi phạm A2, phát hiện muộn | đã chốt `D-232` (hợp đồng hẹp ở `Application.Contracts`, lớp riêng); review ranh giới bằng `using` + test quét (`§7.1` dòng kiến trúc) |
| R3 | `GetUnmappedCodesAsync` đọc toàn bộ dòng con lượt nguồn | chậm nếu danh mục hãng lớn bất thường | chặn bởi kích thước danh mục; chuyển anti-join SQL khi có số đo (additive) |
| R4 | Tra ngược NÉM khi 1 đơn vị ứng nhiều mã `DaXacMinh` ⇒ vận đơn tới đơn vị đó bị **chặn** | **ồn** (có `WithData`) | chủ đích (`D-89`, nợ F10) — BA khi soạn `SHP-001`; đường gỡ chưa có |
| R5 | `XacMinhAsync` đồng thời với worker hạ `NghiNgo` cùng mã | xung đột ghi | **Có** concurrency token: `CarrierAddressMapping : FullAuditedAggregateRoot<Guid>` (`CarrierAddressMapping.cs:82`) ⇒ `ConcurrencyStamp` với `IsConcurrencyToken()` theo quy ước ABP (`ConfigureByConvention`; khuôn `Migrations/20261009070214_Initial.Designer.cs:43-48`) ⇒ ghi đồng thời ném `AbpDbConcurrencyException` (không "ghi sau thắng" im lặng); người gọi phải log + trả lỗi xung đột, **không** tự retry đè |
| R6 | Bảng con phình theo (số mã chưa ánh xạ × số lượt × số hãng) | dung lượng, không sai nghiệp vụ | retention/purge gắn SRS Q6 (người quyết); **không** tự thêm job xoá |
| R7 | Triển khai code trước migration ⇒ mọi lượt `Loi` (`PostgresException`) | **im lặng** ở UI (chỉ `LatestRunOutcome = 2`) | quy trình §4.3: `DbMigrator` trước, bật cờ sau; kiểm `__EFMigrationsHistory` |
| R8 | `SourceHash` đổi định dạng giữa các bản ⇒ so sánh lịch sử vô nghĩa | nhầm lẫn khi đối soát | chốt định dạng §5.3 **trước** khi code; đổi ⇒ `D-yy` |
| R9 | Không có adapter ⇒ 0 dòng, `LatestRunOutcome = null`; UI không phân biệt "job chưa chạy" với "chưa có adapter" | **chấp nhận — `D-236`** | log `Warning` đã có (`CarrierAddressSyncWorker.cs:101-110`); UI hiển thị "chưa có lượt đồng bộ" khi `SyncRunId` **và** `LatestRunOutcome` cùng `null`; **không** mở rộng enum `Outcome` |

---

## 9. Câu hỏi mở — trạng thái sau vòng DOCREV 1 (26/11/2026)

| ID | Câu hỏi | Quyết định | Nội dung đã chốt |
|---|---|---|---|
| `ADDR-002-AQ1` | Hợp đồng cho `SHP-001`/`SHP-004` đặt ở đâu, khi `D-85` cấm 2 interface phản chiếu toàn lớp? | **Đã chốt — `D-232`** | 1 interface hẹp `ICarrierAddressResolver` + **4 DTO** (**2 cấp 1**: request + result; **2 con**: `CarrierAddressMappingContractDto` 9 trường, `AdministrativeUnitContractDto` 7 trường) = **5 kiểu** ở `Application.Contracts`; hiện thực bằng **lớp riêng** `CarrierAddressResolver`; DTO giữ hình dạng `Mapping`+`Unit` của `D-89`; app service **không** kế thừa interface, chỉ uỷ quyền (§3.1) |
| `ADDR-002-AQ2` | Map HTTP cho `05xx`? | **Đã chốt — `D-233`** | Map **6** mã reachable: `0501→404`, `0502`/`0503`/`0504`/`0510→409`, `0505→400`; **không** map `0506`–`0509` (§3.5) |
| `ADDR-002-AQ3` | Kiểu lưu `MauCanNguoiAnhXa` | **Đã chốt — `D-234`** | `text[]` (`string[]?`), ≤ 20 mã đã `Trim`, khử trùng, sắp `Ordinal`; `null` khi 0 việc; không level/name (§4.1) |
| `ADDR-002-AQ4` | Có cần lịch sử lượt không? | **Đã chốt — `D-235`** | WP-18 chỉ lượt gần nhất (đã có trong `UnmappedCodesResultDto`); `GetSyncRunsAsync` additive sau, không permission mới; giữ 2 index bảng lô |
| `ADDR-002-AQ5` | Không adapter / chưa chạy ⇒ 0 dòng, UI phân biệt thế nào? | **Đã chốt — `D-236`** | Giữ `D-90` (0 dòng + log `Warning`); UI hiện "chưa có lượt đồng bộ" khi `SyncRunId` và `LatestRunOutcome` cùng `null`; **không** mở rộng enum |
| `ADDR-002-AQ6` | Có lưu vết tên cũ trong dòng lô không (`D-86` ④)? | **Đã chốt — `D-237`** | **Không** lưu; chứng minh "danh mục đã khác" bằng `SourceHash` + `SoCapNhatTen` + `StartedAtUtc`; chi tiết tên cũ/mới chỉ ở log `Information` |

> Không còn câu mở nào bị chốt ngầm trong thân tệp: mọi chỗ từng ghi `AQ1`…`AQ6` đã trỏ về `D-232`…`D-237`.
> Câu còn mở **ngoài phạm vi WP-18**: SRS Q1/Q2/Q7 (`NCC` — endpoint/webhook hãng) và Q4/Q5/Q6 (BA/vận hành) — không thiết kế ở đây.

---

## 10. Tự kiểm

- Mọi `file:dòng` trong tệp đã đối chiếu trong phiên N77 (đồ thị + `Read`): `CarrierAddressMapping.cs`, `CarrierAddressMappingResolver.cs`, `CarrierAddressMappingSynchronizer.cs`, `CarrierAddressSyncWorker.cs`, `CarrierAddressMappingAppService.cs`, `AddressDtos.cs`, `eCommerceConsts.cs`, `eCommercePermissionDefinitionProvider.cs`, `eCommerceHttpApiHostModule.cs`, `AddressDirectory.cs:24`, `CustomerDirectory.cs:28`.
- Vòng DOCREV 1 (26/11/2026) đã đóng: **P1 #1** (thứ tự quét `D-84`/`D-91` **trước** khi băm, §5.1/§5.3); **P2 #2–#5** (`D-232` DTO + lớp riêng, §8 R5 concurrency token, §7.1 ca `InsertAsync` dòng lô ném + NC-2); **P3**: tên index `≤ 63` byte (§4.1/§4.2), §4.3 "**4** index", `SourceHash varchar(64)` (§5.3), §6 hiển thị `05xx` theo map mới.
- Tệp chỉ nêu **tên** (không nêu số dòng) khi chưa đọc lại trong phiên: migration `20261014042922_Add_Addr002_CarrierMappings.cs`, `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs`, `eCommerceDbContext.cs`, `PostgreSqlSchemaTests.cs`, `AddressDbContextModelCreatingExtensions.cs`, `Migrations/20261009070214_Initial.Designer.cs` (dòng 43-48 đọc bằng grep) — BE kiểm lại số dòng khi code.
- **0** việc bỏ ngỏ; **0** cam kết endpoint/trường NCC; **0** thay đổi `AddrCarrierMappings`; mọi `AQ1`…`AQ6` đã trỏ `D-232`…`D-237`.
- **DOCREV vòng 2 (N80, 26/11/2026) — 16/16 P3 (dòng #2–#17) đã xử lý, khớp as-built N78:** #2 route `POST` tra ngược **tồn tại**, FE không dùng (§6) · #3 `LapKeHoach` nhận `DanhMucDiaChiHang`; `ChuanHoaVaKiemMa` gọi 2 lần idempotent (§5.1/§5.2) · #4 **5 kiểu** (§3.1/§9) · #5 tên index thật ≤ 63 byte; `D-235` chỉ là tên mô tả (§4.1) · #6 `SourceHash` `ADDR-002` **chưa** có CHECK hex (§5.3) · #7 mapper biên hợp đồng `internal static` trên resolver, app service giữ mapper riêng (§3.1/§5.4) · #8 `D-89` ①(1) (§3.2) · #9 **đã khớp as-built** — SRS `addr-002` v0.7 (N78) đã sửa `MauCanNguoiAnhXa text[]` (§5.2 `:314`) và `CarrierAddressCodeResultDto(Mapping, Unit)` (§6.1 `:390`) · #10 blockquote loại `-arch` (header) · #11 sơ đồ `Address.Application → Domain.Shared` (§2) · #12 `SoCapNhatTen` gồm cả `NghiNgo`, `SoDanhDauNghiNgo` không gồm (§5.2) · #13 **thay** `CreateIndex(unique)` bằng `Sql` (§4.3) · #14 `pg_dump` **bắt buộc** (§4.3) · #15 điều kiện `SourceHash = null` thống nhất (§4.1) · #16 `BR-011` (§7.1) · #17 FE **0** (đo N80, §3.4).
