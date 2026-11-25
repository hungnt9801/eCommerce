# Triển khai `ADDR-001` — tra cứu đơn vị hành chính 2 cấp + nạp/đồng bộ danh mục

> Phiên `N81` (hàng `W18-5`, F6 đóng WP-18) · nhánh `feat/gd3-wp18-dia-chi` → `tich-hop/gd3` · 26/11/2026.
> Quyết định liên quan: `D-238`, `D-240`, `D-241`, `D-242`, `D-243`, `D-251`.
> Tài liệu này chỉ mô tả **vận hành**; thiết kế nằm ở `docs/features/address/ADDR-001-arch.md`.

## 1. Phạm vi

- Nhánh/PR: `feat/gd3-wp18-dia-chi` → `tich-hop/gd3` (chuỗi GĐ3, `D-216` phương án B); production nhận qua PR
  tích hợp `tich-hop/gd3` → `main`.
- Phần mã được triển khai (module `Address`, `ADDR-001`):
  - `modules/Address/eCommerce.Address.Application/AdministrativeUnitAppService.cs` — tra cứu 2 cấp + nạp danh mục
    qua `ImportDatasetAsync` (permission `Addresses.Catalog.Import`, `D-238`).
  - `modules/Address/eCommerce.Address.Application/Sync/AdministrativeUnitSyncWorker.cs` — job đồng bộ định kỳ
    (**mặc định TẮT**).
  - `modules/Address/eCommerce.Address.Application/Caching/AddressCatalogCache.cs` — cache tra cứu + TTL
    (`D-242`); `modules/Address/eCommerce.Address.Domain/AddressSettingDefinitionProvider.cs` — khai báo Setting
    `Address:Cache:TtlSeconds`.
  - `modules/Address/eCommerce.Address.Domain/CatalogSyncRun.cs` — entity **APPEND-ONLY** của bảng lô nạp
    (`D-78`, BR-009).
- Migration: **1** migration expand-only
  `src/eCommerce.EntityFrameworkCore/Migrations/20261125191757_Add_Addr001_CatalogSyncRuns.cs` — `CREATE TABLE
  "AddrCatalogSyncRuns"` + 2 index + 3 CHECK; **không** ALTER/DROP bảng cũ. Đã áp ở DB dev trong `N78` sau
  `pg_dump` (`docs/nhanh/handover-n78.md`, tệp `artifacts/pg-dump/N78-W18-2.dump`, handover ghi **197.121 byte**).
- Không đụng `eCommerce.slnx` · `Directory.Packages.props` · `pnpm-lock.yaml` (đã kiểm: `git diff --name-only
  origin/tich-hop/gd3...HEAD` **không** đổi 3 tệp này trên cả WP-18).
- Không có thay đổi FE trong WP-18: hàng `W18-3` ghi **"no contract-first surface"** (nhánh
  `feat/gd3-wp18-dia-chi`), nên đổi BE **không** cần phát hành FE cùng lúc.
- Liên kết: thiết kế `docs/features/address/ADDR-001-arch.md` · SRS
  `docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` · review
  `docs/reviews/ADDR-001-review.md` · biên bản phiên `docs/nhanh/handover-n78.md`, `docs/nhanh/handover-n80.md`.

## 2. Biến môi trường

| Tên | Loại | Mặc định dev | Khuyến nghị production | Ghi chú |
|---|---|---|---|---|
| `Address:Cache:Enabled` | `IConfiguration` (bool, đọc mỗi lần gọi ở `AddressCatalogCache.DangBat`) | **BẬT** (không khai / giá trị không đọc được ⇒ BẬT) | **BẬT** | Đặt `false` để **tắt hẳn** cache ⇒ mỗi lần tra đọc DB (đường lùi `D-242`). Đây là cờ `IConfiguration`, không phải ABP Setting. |
| `Address:Cache:TtlSeconds` | **ABP Setting** (`AddressSettingNames.CacheTtlSeconds`, phạm vi `Application`+`Tenant`) | `86400` | `86400` | `0`/âm/không phải số/không đọc được ⇒ dùng `86400` + `LogWarning` (`D-242`). **Xoá cache khi nạp lô** (`AddressCacheInvalidatedEto`) vẫn là cơ chế chính; TTL chỉ là lưới. |
| `Address:Sync:Enabled` | `IConfiguration` (bool, `GetValue(defaultValue: false)`) | **`false`** | **`false`** | Job `AdministrativeUnitSyncWorker` **mặc định TẮT**. Chỉ bật khi chủ động nạp danh mục từ nguồn ngoài. |
| `Address:Sync:SchemaVersion` | `IConfiguration` (int, `GetValue(defaultValue: 2)`) | `2` | `2` | Phiên bản schema danh mục mà job nạp (`1` = 3 cấp, `2` = 2 cấp). |
| `Address:Sync:PeriodSeconds` | `IConfiguration` (int, `GetValue(defaultValue: 86400)`) | `86400` | `86400` | Nhịp job (giây). Job đặt `RunOnStart = true` nhưng lượt chạy ngay **chỉ đọc cấu hình rồi thoát** khi cờ `Address:Sync:Enabled = false`. |

- **Không** có secret mới. Các khoá trên là cấu hình/Setting thường, đặt qua `.env`/secret store của hạ tầng hoặc
  `appsettings` của Host — **không** commit giá trị production vào repo (O1).
- Cache nằm đúng `Khoa(schemaVersion, parentCode)` (`AddressCatalogCache.Khoa`), xoá bằng sự kiện `AddressCacheInvalidatedEto`
  phát qua outbox cùng UoW với dòng lô `DaApDung`.

## 3. Thứ tự triển khai

1. **Backup DB production trước khi chạm schema** (K-01/M9): chạy `pg_dump -Fc` ra tệp **ngoài repo**
   (ví dụ `artifacts/pg-dump/<PROD>-addr001.dump`), ghi đường dẫn + kích thước tệp vào biên bản triển khai.
2. **Merge code BE trước** (`feat/gd3-wp18-dia-chi` → `tich-hop/gd3` → `main`). DB dev đã áp migration ở `N78`;
   production sẽ áp ở bước 4.
3. **Build binary mới**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng 0 error, 0 warning mới).
4. **Áp migration bằng binary MỚI qua `DbMigrator`** (M5) — **KHÔNG** dùng `dotnet ef database update` (M6, `D-75`):
   `dotnet run --project src/eCommerce.DbMigrator`
5. **Kiểm DB thật** (không tin `exit=0`):
   - `SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Add_Addr001_CatalogSyncRuns%';`
     → có đúng 1 dòng.
   - `\d "AddrCatalogSyncRuns"` → đủ cột; có `IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_ImportedAtUtc`,
     `IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_Outcome_ImportedAtUtc`; có `CK_AddrCatalogSyncRuns_Outcome`,
     `CK_AddrCatalogSyncRuns_SchemaVersion`, `CK_AddrCatalogSyncRuns_SourceHashHex`.
   - Tên bảng/index/CHECK khớp hằng trong
     `modules/Address/eCommerce.Address.EntityFrameworkCore/AddressDbContextModelCreatingExtensions.cs`.
6. **Chỉ khi chủ động muốn job nạp**: đặt `Address:Sync:Enabled=true` (+ `Address:Sync:SchemaVersion`,
   `Address:Sync:PeriodSeconds`). Nếu không, **giữ nguyên** `false` — nạp danh mục bằng `ImportDatasetAsync`
   (cần permission `Addresses.Catalog.Import`).
7. **Kiểm sau triển khai** (mục §5) rồi mới coi là xong.

> Migration này **chỉ tạo bảng lô**; bảng danh mục `AdministrativeUnits` do migration cũ
> `20261013093715_Add_Address_AdministrativeUnits` tạo. Trên DB mới hoàn toàn, `DbMigrator` áp toàn bộ chuỗi.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| Cache tra cứu trả dữ liệu cũ/sai | Đặt `Address:Cache:Enabled=false` ⇒ `AddressCatalogCache.DangBat` trả `false`, mọi lần tra đọc DB trực tiếp. Khôi phục = bỏ khoá (về BẬT). | 0 code, 0 migration, 0 dữ liệu. Cơ chế `D-242`. |
| Job nạp danh mục chạy sai | Đặt `Address:Sync:Enabled=false` ⇒ job chỉ log "đang TẮT" và thoát; **0** dòng lô mới. | Dòng lô cũ giữ nguyên (append-only). |
| Cần bỏ hẳn mã `ADDR-001` | `git log --oneline origin/tich-hop/gd3` để lấy SHA các commit WP-18, rồi `git revert` **đúng** commit chạm `Address` (ví dụ `31c7ead` — `feat(address) ... ADDR-001 catalog sync runs ...`; kèm commit test `372a33e`, SRS `0b2ea6f` nếu cần). | Revert từng commit; **không** revert migration đã áp ở bước 4 (xem dòng dưới). |
| Schema `AddrCatalogSyncRuns` | **KHÔNG** chạy `Down`/`DROP` ở production. Để bảng lại. | Migration **expand-only**; bảng chỉ chứa **dòng lô** (dấu vết append-only của BR-009), **không** phải dữ liệu nghiệp vụ đang phục vụ. **0** dòng nghiệp vụ bị xoá; danh mục `AdministrativeUnits` **không** bị ảnh hưởng. |
| `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml` | Không cần — WP-18 **không** đổi 3 tệp này | Đã kiểm bằng `git diff --name-only origin/tich-hop/gd3...HEAD`. |

- Nếu đã revert **file migration** khỏi assembly mà DB đã áp: dòng `__EFMigrationsHistory` thừa vô hại (EF bỏ qua
  migration không còn trong assembly); **không** tự xoá dòng lịch sử. Chạy lại `dotnet run --project
  src/eCommerce.DbMigrator` để xác nhận **không** áp/drop gì và `exit=0`.
- Sau revert, kiểm tra Host khởi động được và `GET /api/app/carrier-address-mapping/...`/endpoint tương ứng còn
  phục vụ (ADDR-002 độc lập) trước khi coi là hoàn tất.

## 5. Cổng (số đo thật)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **ORCH đo ở cuối phiên `N81`** (agent `ops` **không** tự chạy — tránh hết bộ nhớ, bài học `N69`). |
| `pwsh -File tools/kiem-tat-ca.ps1` (cổng nhẹ, `N80`) | `exit 0` · **10/10 bước · 305,2 s** (`docs/nhanh/handover-n80.md`). |
| `pwsh -File tools/probe-dia-chi.ps1` (`N80`) | `exit 0` · **10/10 DAT** (1 ca `403` chưa đo được, nợ `D-249`). |
| `dotnet test test/eCommerce.Tests` (`N78`) | `exit 0` · **2089/2089** (`docs/nhanh/handover-n78.md`). |

### Kiểm sau triển khai (thủ công, trên production)

| Kiểm | Kỳ vọng |
|---|---|
| `GET /api/health` (BFF `apps/admin-web`) · `GET /health-status` (Host) | **`200`** |
| `GET /api/app/administrative-unit/provinces?schemaVersion=2` **có** Bearer token | **`200`** (danh sách tỉnh; rỗng ⇒ `[]` + log `Warning`, không phải lỗi) |
| cùng route **không** token | **`401`** (`[Authorize]` cấp class, BR mọi app service) |
| `GET /api/app/carrier-address-mapping/unmapped-codes?CarrierCode=<mã>` (ADDR-002, đã kiểm chứng bằng probe) | `401` không token · `200` có token |
| Cờ job | `Address:Sync:Enabled` vẫn `false`; log có dòng "đồng bộ danh mục ĐVHC đang TẮT"; **0** dòng `AddrCatalogSyncRuns` có `Trigger = 0` (Job) mới |

- ⚠️ Route cụ thể của `AdministrativeUnitAppService` do ABP conventional controller sinh từ tên method
  (`D-13`: `/api/app/**`), SRS `addr-001-...md` §6.2 ghi **"route cụ thể chưa kiểm chứng"** — dùng kết quả `200/401`
  thực tế của lượt deploy để **cập nhật** bảng route trong SRS, không coi route suy đoán là hợp đồng.
- Kiểm nhanh runtime toàn tuyến ADDR: `pwsh -File tools/probe-dia-chi.ps1` (chạy trên máy có Host `:5000` + DB seed).

## 6. Khi hỏng — ai gọi

- **Cache trả đơn vị đã đóng / thiếu đơn vị mới / TTL sai**: gọi **`be`** (chủ `AddressCatalogCache`,
  `AddressSettingDefinitionProvider`); nếu là câu hỏi thiết kế cache/ETO ⇒ **`arch`** (`D-242`).
- **Job nạp danh mục không chạy / lỗi phân loại `BiChan`/`Loi`**: gọi **`be`** (chủ `AdministrativeUnitSyncWorker`);
  cấu hình cờ/nhịp sai ⇒ **`ops`**.
- **Nạp danh mục qua `ImportDatasetAsync` lỗi `0601`…`0605`/`0009`**: gọi **`be`** (hàng rào/guard); `403` do thiếu
  permission `Addresses.Catalog.Import` ⇒ **`be`** (reflection permission, `D-238`).
- **Migration / bảng / index sai, `DbMigrator` hỏng**: gọi **`ops`** (chủ vận hành DB, chạy `DbMigrator` bằng binary
  mới). **Không** dùng `dotnet ef database update`.
- **`401`/`403` cần đo runtime**: hiện `403` thiếu permission **chưa đo được** ở probe (nợ `D-249`) ⇒ gọi **`be`**
  để xác nhận bằng reflection, hoặc theo dõi khi đóng nợ `D-249`.
- **Không chắc về secret/NCC/production**: báo **ORCH** để mở `blocked`, không tự xử.
