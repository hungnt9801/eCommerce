# Triển khai `ADDR-002` — biên dịch địa chỉ theo mã hãng vận chuyển + đồng bộ ánh xạ

> Phiên `N81` (hàng `W18-5`, F6 đóng WP-18) · nhánh `feat/gd3-wp18-dia-chi` → `tich-hop/gd3` · 26/11/2026.
> Quyết định liên quan: `D-232`, `D-233`, `D-234`, `D-243`, `D-248`, `D-250`.
> Tài liệu này chỉ mô tả **vận hành**; thiết kế nằm ở `docs/features/address/ADDR-002-arch.md`.

## 1. Phạm vi

- Nhánh/PR: `feat/gd3-wp18-dia-chi` → `tich-hop/gd3` (chuỗi GĐ3, `D-216` phương án B); production nhận qua PR
  tích hợp `tich-hop/gd3` → `main`.
- Phần mã được triển khai (module `Address`, `ADDR-002`):
  - `modules/Address/eCommerce.Address.Application/CarrierAddressMappingAppService.cs` — tra mã hãng ⇄ đơn vị,
    danh sách mã chưa ánh xạ (permission `Addresses.CarrierMapping.Verify`, `D-85`).
  - `modules/Address/eCommerce.Address.Application/Sync/CarrierAddressSyncWorker.cs` — job đồng bộ mã hãng
    (**mặc định TẮT**).
  - `modules/Address/eCommerce.Address.Domain/Carrier/CarrierAddressSyncRun.cs` — entity **APPEND-ONLY** của bảng lô
    đồng bộ (`D-83`/`D-90`).
  - Hợp đồng hẹp `ICarrierAddressResolver` (`D-232`) cho module khác tra **ngược** đơn vị của ta → mã hãng.
- Migration: **1** migration expand-only
  `src/eCommerce.EntityFrameworkCore/Migrations/20261125193330_Add_Addr002_CarrierSyncRuns.cs` — tạo **2** bảng
  `"AddrCarrierSyncRuns"` + `"AddrCarrierSyncRunUnmappedCodes"` + 4 index + FK `Restrict` + CHECK, kèm **SQL thô**
  unique `NULLS NOT DISTINCT`. **Không** ALTER/DROP bảng cũ. Đã áp ở DB dev trong `N78` sau `pg_dump`
  (`docs/nhanh/handover-n78.md`, tệp `artifacts/pg-dump/N78-W18-2.dump`, handover ghi **197.121 byte**).
- Không đụng `eCommerce.slnx` · `Directory.Packages.props` · `pnpm-lock.yaml` (đã kiểm: `git diff --name-only
  origin/tich-hop/gd3...HEAD` **không** đổi 3 tệp này trên cả WP-18).
- Không có thay đổi FE trong WP-18: hàng `W18-3` ghi **"no contract-first surface"**, nên đổi BE **không** cần
  phát hành FE cùng lúc.
- Liên kết: thiết kế `docs/features/address/ADDR-002-arch.md` · SRS
  `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` · review
  `docs/reviews/ADDR-002-review.md` · biên bản phiên `docs/nhanh/handover-n78.md`, `docs/nhanh/handover-n80.md`.

## 2. Biến môi trường

| Tên | Loại | Mặc định dev | Khuyến nghị production | Ghi chú |
|---|---|---|---|---|
| `Address:CarrierSync:Enabled` | `IConfiguration` (bool, `GetValue(defaultValue: false)`) | **`false`** | **`false`** | Job `CarrierAddressSyncWorker` **mặc định TẮT**. Chỉ bật khi đã có adapter hãng (xem cảnh báo dưới). |
| `Address:CarrierSync:PeriodSeconds` | `IConfiguration` (int, `GetValue(defaultValue: 86400)`) | `86400` | `86400` | Nhịp job (giây). Job đặt `RunOnStart = true` nhưng lượt chạy ngay **chỉ đọc cấu hình rồi thoát** khi cờ TẮT. |
| `Address:CarrierSync:Carriers` | `IConfiguration` (chuỗi, phân tách dấu phẩy; ví dụ `GHN,GHTK`) | **rỗng** | **rỗng** cho tới khi có adapter | Mã hãng được `Trim` + `ToUpperInvariant` + khử trùng. **Rỗng ⇒ job log `Warning` và không làm gì.** Không hard-code mã hãng (BR-011). |

- **Không** có secret mới. Đây là cấu hình thường; đặt qua `.env`/secret store hoặc `appsettings` của Host, **không**
  commit giá trị production vào repo (O1).
- ⚠️ **Cảnh báo bật job ở production**: mỗi hãng trong `Address:CarrierSync:Carriers` phải có một hiện thực
  `IKhungDiaChiHangSource` đăng ký DI. Hiện **chưa có** adapter NCC nào (hạng `R3-1` chưa làm, `ADDR-002` §15.1 Q1
  `Open`). Bật cờ khi **chưa** có adapter ⇒ job log `Warning` "CHƯA CÓ ADAPTER … ⇒ bỏ qua" và **không** đồng bộ gì.
  Vì vậy **production giữ `Address:CarrierSync:Enabled = false`** cho tới khi có adapter + tài liệu API hãng.

## 3. Thứ tự triển khai

1. **Backup DB production trước khi chạm schema** (K-01/M9): chạy `pg_dump -Fc` ra tệp **ngoài repo**
   (ví dụ `artifacts/pg-dump/<PROD>-addr002.dump`), ghi đường dẫn + kích thước tệp vào biên bản triển khai.
2. **Merge code BE trước** (`feat/gd3-wp18-dia-chi` → `tich-hop/gd3` → `main`). DB dev đã áp migration ở `N78`.
3. **Build binary mới**: `dotnet build eCommerce.slnx --no-incremental` (kỳ vọng 0 error, 0 warning mới).
4. **Áp migration bằng binary MỚI qua `DbMigrator`** (M5) — **KHÔNG** dùng `dotnet ef database update` (M6, `D-75`):
   `dotnet run --project src/eCommerce.DbMigrator`
5. **Kiểm DB thật** (không tin `exit=0`):
   - `SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Add_Addr002_CarrierSyncRuns%';`
     → có đúng 1 dòng.
   - `\d "AddrCarrierSyncRuns"` và `\d "AddrCarrierSyncRunUnmappedCodes"` → đủ cột; FK
     `FK_AddrCarrierSyncRunUnmappedCodes_AddrCarrierSyncRuns_SyncRun~` (`ON DELETE RESTRICT`); CHECK
     `CK_AddrCarrierSyncRuns_Outcome`.
   - **Index unique trên bảng multi-tenant có `NULLS NOT DISTINCT`** (M7): chạy
     `SELECT indexname, indexdef FROM pg_indexes WHERE tablename IN ('AddrCarrierSyncRuns','AddrCarrierSyncRunUnmappedCodes');`
     → phải thấy `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code` và `indexdef` chứa **`NULLS NOT DISTINCT`**.
     Thiếu cụm này = ràng buộc mất tác dụng im lặng (PostgreSQL coi `NULL` là khác nhau).
   - Tên bảng/index/CHECK khớp hằng trong
     `modules/Address/eCommerce.Address.EntityFrameworkCore/AddressDbContextModelCreatingExtensions.cs`.
6. **Bật job (tuỳ chọn, có điều kiện)**: chỉ khi **đã có adapter** cho từng hãng — đặt
   `Address:CarrierSync:Enabled=true` + `Address:CarrierSync:Carriers=<mã hãng>` (+ `Address:CarrierSync:PeriodSeconds`
   nếu muốn khác `86400`). Nếu chưa, **giữ nguyên** `false`.
7. **Kiểm sau triển khai** (mục §5) rồi mới coi là xong.

> Migration này **chỉ tạo bảng lô/dòng con** (append-only). Bảng ánh xạ nghiệp vụ `AddrCarrierMappings` do migration
> cũ `20261014042922_Add_Addr002_CarrierMappings` tạo; `ADDR-002` không đổi cấu trúc bảng đó.

## 4. Đường lùi (bắt buộc)

| Thay đổi | Đường lùi | Ghi chú |
|---|---|---|
| Ánh xạ hãng sai do một lượt đồng bộ | Đặt `Address:CarrierSync:Enabled=false` ⇒ job chỉ log "đang TẮT" và thoát; **0** thay đổi ánh xạ mới. | Dòng lô cũ + dòng con giữ nguyên (append-only). Ánh xạ là **dữ liệu dẫn xuất dựng lại được** (BR-001); sửa tay qua đường có permission `Addresses.CarrierMapping.Verify` nếu cần. |
| Job đồng bộ chạy sai nhịp/hãng | Đặt `Address:CarrierSync:Enabled=false` và/hoặc rút `Address:CarrierSync:Carriers`; khôi phục = bật lại + cấu hình đúng. | 0 code, 0 migration, 0 dữ liệu. |
| Cần bỏ hẳn mã `ADDR-002` | `git log --oneline origin/tich-hop/gd3` để lấy SHA các commit WP-18, rồi `git revert` **đúng** commit chạm `Address` (ví dụ `86bde2b` — `feat(address) ... ADDR-002 carrier sync runs + resolver + unmapped codes`; kèm commit test `372a33e`, SRS `0b2ea6f` nếu cần). | Revert từng commit; **không** revert migration đã áp ở bước 4 (xem dòng dưới). |
| Schema `AddrCarrierSyncRuns` / `AddrCarrierSyncRunUnmappedCodes` | **KHÔNG** chạy `Down`/`DROP` ở production. Để 2 bảng lại. | Migration **expand-only**; 2 bảng chỉ chứa **dòng lô + dòng con** (dấu vết append-only của `D-83`/`D-88`), **không** phải dữ liệu nghiệp vụ đang phục vụ. **0** dòng nghiệp vụ bị xoá; bảng ánh xạ `AddrCarrierMappings` và đơn vị hành chính **không** bị ảnh hưởng. |
| `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml` | Không cần — WP-18 **không** đổi 3 tệp này | Đã kiểm bằng `git diff --name-only origin/tich-hop/gd3...HEAD`. |

- Nếu đã revert **file migration** khỏi assembly mà DB đã áp: dòng `__EFMigrationsHistory` thừa vô hại (EF bỏ qua
  migration không còn trong assembly); **không** tự xoá dòng lịch sử. Chạy lại `dotnet run --project
  src/eCommerce.DbMigrator` để xác nhận **không** áp/drop gì và `exit=0`.
- Sau revert, kiểm tra Host khởi động được và `GET /api/app/carrier-address-mapping/unmapped-codes?CarrierCode=<mã>`
  bằng token hợp lệ (dùng route **đã kiểm chứng** bằng `tools/probe-dia-chi.ps1`) trước khi coi là hoàn tất.

## 5. Cổng (số đo thật)

| Lệnh | Kết quả |
|---|---|
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **ORCH đo ở cuối phiên `N81`** (agent `ops` **không** tự chạy — tránh hết bộ nhớ, bài học `N69`). |
| `pwsh -File tools/kiem-tat-ca.ps1` (cổng nhẹ, `N80`) | `exit 0` · **10/10 bước · 305,2 s** (`docs/nhanh/handover-n80.md`). |
| `pwsh -File tools/probe-dia-chi.ps1` (`N80`) | `exit 0` · **10/10 DAT** (`T05`–`T09` map `0501/0505/0502/0504/0510` đúng; 1 ca `403` chưa đo được, nợ `D-249`). |
| `dotnet test test/eCommerce.Tests` (`N78`) | `exit 0` · **2089/2089** (`docs/nhanh/handover-n78.md`). |

### Kiểm sau triển khai (thủ công, trên production)

| Kiểm | Kỳ vọng |
|---|---|
| `GET /api/health` (BFF `apps/admin-web`) · `GET /health-status` (Host) | **`200`** |
| `GET /api/app/carrier-address-mapping/unmapped-codes?CarrierCode=<mã>` **có** Bearer token | **`200`** (đã kiểm chứng bằng probe `T03`) |
| cùng route **không** token | **`401`** (probe `T02`) |
| `POST /api/app/carrier-address-mapping/<id>/xac-minh` **không** token | **`401`** (probe `T04`) |
| `POST /api/app/carrier-address-mapping/tra-ma-hang` mã lạ / hãng rỗng / chưa xác minh / đơn vị đã đóng | `404 + 0501` · `400 + 0505` · `409 + 0502` · `409 + 0504` (probe `T05`–`T08`) |
| Cờ job | `Address:CarrierSync:Enabled` vẫn `false`; log có dòng "Đồng bộ mã hãng đang TẮT"; **0** dòng `AddrCarrierSyncRuns` mới |

- Route cụ thể đã kiểm chứng bằng `tools/probe-dia-chi.ps1` (HTTP thật trên Host `:5000`); dùng kết quả `200/401`
  thực tế của lượt deploy để cập nhật bảng route trong SRS nếu cần.
- Kiểm nhanh runtime toàn tuyến ADDR: `pwsh -File tools/probe-dia-chi.ps1` (chạy trên máy có Host `:5000` + DB seed).

## 6. Khi hỏng — ai gọi

- **Ánh xạ hãng sai** (tra nhầm đơn vị / thiếu mã / `0501`–`0510` sai nhánh): gọi **`be`**
  (chủ `CarrierAddressMappingAppService`, `CarrierAddressResolver`, `CarrierAddressSyncWorker`).
- **Hợp đồng `ICarrierAddressResolver` cho module khác sai/đổi hình dạng**: gọi **`arch`** (`D-232` ranh giới module).
- **Job đồng bộ không chạy / bỏ qua hãng / lỗi phân loại `BiChan`/`Loi`**: gọi **`be`** (worker); cấu hình cờ/nhịp
  `Address:CarrierSync:*` sai ⇒ **`ops`**.
- **Migration / bảng / index unique `NULLS NOT DISTINCT` sai, `DbMigrator` hỏng**: gọi **`ops`** (chủ vận hành DB,
  chạy `DbMigrator` bằng binary mới). **Không** dùng `dotnet ef database update`.
- **`401`/`403`**: `401` khi thiếu token là đúng thiết kế; `403` thiếu permission `Addresses.CarrierMapping.Verify`
  hiện **chưa đo được** ở probe (nợ `D-249`) ⇒ gọi **`be`** để xác nhận bằng reflection, hoặc theo dõi khi đóng
  nợ `D-249`.
- **Không chắc về secret/NCC/production**: báo **ORCH** để mở `blocked`, không tự xử.
