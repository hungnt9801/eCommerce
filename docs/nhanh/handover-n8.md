# Handover `N8` — WP-07: Khách hàng (**điểm · công nợ · cấp**) + **Nhân viên & phạm vi dữ liệu**

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-07](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-45](decisions.md), [D-46](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-07** (khách mở rộng + cấp/chăm sóc + cửa hàng/phòng ban/nhân viên +
> phạm vi dữ liệu + nối **sổ điểm/công nợ của POS vào khách**), **không mở rộng phạm vi**.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **89 mục** (**20 ` M`** của `N7` chưa commit + **69 `??`** rác khảo sát/probe) |
| 2 | `git log --oneline -1` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Running** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (incremental) — `-t:Rebuild` cho **0 error / 9 warning** (9 `CS8604` có sẵn: Address/Sales) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1900/1900 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 109 tệp `.md`, 521 link, 355 đường dẫn, **0 chết** |

📌 Baseline **khớp** `handover-n7` (1900 test · migration 57 · NND 79). Không có sai lệch phải dừng.

### 1.2 Đã thi công (đúng 5 việc BE + 3 việc FE của `07 §WP-07`)

| # | Việc (`07 §WP-07`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | `Customer`: 10 trường mới | `Type` · `LevelId` · `Points` · `AccumulatedAmount` · `DebtLimit` · `DebtBalance` · `SaleStaffId` · `ReferrerId` · `TaxCode` · `Birthday` — **migration ADDITIVE** | `dotnet ef migrations has-pending-model-changes` = **“No changes”**; **11 cột** mới trên `CusCustomers` |
| 2 | `CustomerLevel` (ngưỡng) + `CustomerCare` (5 loại) | Bảng `CusLevels` (mã/ngưỡng/%CK/thứ tự/bật-tắt) + `CusCares` (**append-only**, 5 loại: gọi/nhắn/email/gặp/khác) | **7 unit test** cấp/chăm sóc; hẹn theo dõi có index riêng |
| 3 | Sổ điểm/công nợ **vào khách** | `Customer.GhiNhanMuaHang`/`DaoGhiNhanMuaHang` (**một cửa**) + `NghiepVuKhach` (hàm thuần) + cổng `ICustomerLedger` + `CustomerLedger`; **cấp tự xếp** theo ngưỡng | **33 unit test** `Wp07KhachHangTests`; probe: điểm **12** · tích luỹ **100.000** · nợ **50.000** · **tự lên cấp VIP** |
| 4 | `Store`/`Department`/`Staff` + `RoleDataScope` + seed quyền | 4 bảng `Set*` + **6 index NND**; quyền mới `eCommerce.Settings` + `.Manage` (khai **cả hai nơi**) | `BatBienPhanQuyenTests` xanh; ma trận quyền đọc **4 nhóm · 80 quyền · 1 vai trò** |
| 5 | Áp `data_scope` vào `Orders`/`RetailBills` | `IDataScopeResolver` (hợp đồng ở `Customers.Application.Contracts`) → lọc **server-side** ở DS **và** chặn ở **đọc chi tiết** | Probe ⑪: `Own` ⇒ hoá đơn người khác **biến mất** khỏi DS; đọc chi tiết ⇒ **403 `Pos:0028`**; trả `All` ⇒ thấy lại |
| 6 | FE DS khách **5 tab phân khúc** | `customer/list`: 5 tab + badge đếm (**cùng** `CustomerTabs` ở BE), lọc server-side, 10 cột | `pnpm build` ✓ — route `customer/list` **173 B** |
| 7 | FE chi tiết khách **5 tab** | `customer/[id]`: Tổng quan · Đơn hàng · Hoá đơn bán lẻ · **Công nợ** (có cờ đối chiếu) · **Chăm sóc** (ghi + lịch sử) | route **2,69 kB**; probe ⑯a thấy **đủ 5 nhãn tab** |
| 8 | FE nhân viên & **ma trận quyền** | `settings/staff`: DS nhân viên + **phạm vi dữ liệu theo vai trò** + **ma trận quyền** đọc từ `PermissionDefinitionProvider` (bấm là lưu) | route **3,08 kB**; probe ⑬⑭⑮⑯b |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** — **không** phát sinh warning mới (đã bắt và sửa **1** `CS0108`: `CustomerDetailDto.LevelName` khai lại trường của lớp cha) |
| `dotnet test test\eCommerce.Tests` | **1933/1933 pass** (**+33** so với 1900) |
| `dotnet test test\eCommerce.IntegrationTests` | **90/90 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — **21 route** (18 cũ + 3 mới) |
| `pnpm spec:snapshot` + `pnpm gen:api` | **46/46 path** `/api/v1` (28 cũ + **18** mới: 7 khách + 3 cấp + 8 cấu hình) |
| Migration | **58** — `20261104025249_Wp07_KhachHang_NhanVien` (có trong `__EFMigrationsHistory`) |
| `schema.sql` | sinh lại từ chính migration: **3.725 → 3.900 dòng** (**+175**, 0 xoá) |
| NND (DB dev) | **79 → 85** (+6 index `Set*`/`CusLevels`, **cả 6 viết tay `NULLS NOT DISTINCT`**) |
| `pwsh -File .\tools\probe-khach-hang-wp07.ps1` | **`exit 0`** — **28 lời gọi HTTP** qua BFF/Next (+5 lời gọi setup qua Host) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (số đo ở §1.5) |
| DB | `DoiChieuSoDu`: `tienTichLuyKhop = true` · `congNoKhop = true` cho khách của probe |

### 1.4 Bằng chứng HTTP thật — `tools/probe-khach-hang-wp07.ps1` (`exit 0`)

Setup **toàn bộ bằng API thật**: tạo kho → nhập 20 × 1 SKU (@100.000) → post → tạo **cấp khách** ngưỡng
100.000 → đăng nhập **NextAuth** → gọi qua **BFF** `/api/proxy` (`D-12`).

| # | Ca | Kết quả đo |
|---|---|---|
| ① | `GET /{business}/customer/list` | **200** — có *"Khách hàng"*, *"Khách lẻ"*, *"Còn nợ"*, *"Nhân viên"* |
| ② | `POST …/customers` | **200** — khách mới (idempotent theo khoá) |
| ③ | `PUT …/customers/{id}/segment` (loại buôn · hạn mức **100.000**) | **200** — đọc lại `type = Wholesale`, `debtLimit = 100.000` |
| ④a | `POST …/pos/sessions/open` | **200** — ca mở |
| ④b | `POST …/pos/bills` (dùng **3 điểm**, trả 47.000) | **200** — `totalPayment 97.000`, `debtAmount 50.000` |
| ⑤ | `GET …/customers/{id}` | **200** — điểm **12** (15 tích − 3 tiêu) · tích luỹ **100.000** · nợ **50.000** · **cấp `VIP …`** · 1 hoá đơn |
| ⑥ | `GET …/customers/tabs` | **200** — 5 tab, tab **"Còn nợ" = 1** |
| ⑦ | bán tiếp 100.000 ghi nợ (nợ sẽ thành 150.000 > hạn mức 100.000) | **409** + `eCommerce:Customers:0019`; **số hoá đơn trước = sau = 1** ⇒ **rollback cả transaction POS** |
| ⑧ | `POST …/pos/returns` trả 1 món (thu hồi 15 điểm) | **201** — `goodsAmount 100.000` · `debtReduction 50.000` · `refund 50.000`; khách về **0 / 0 / 0** |
| ⑨ | bán 100.000 (trả đủ) rồi **huỷ** | **200** — tích luỹ `100.000 → 0`; điểm **không âm**; nợ **0** |
| ⑩ | `GET …/customers/{id}/balance` | **200** — `tienTichLuyKhop = true`, `congNoKhop = true` |
| ⑪ | đổi phạm vi `admin` ⇒ `Own` (hoá đơn D bị đổi `CreatorId` bằng SQL **dev-only**) | **200**; DS **11 dòng, KHÔNG** có D; đọc chi tiết D ⇒ **403 `Pos:0028`** |
| ⑫ | trả phạm vi `admin` về `All` | **200** — DS **13 dòng, CÓ** lại D |
| ⑬ | `GET …/settings/permissions` | **200** — **4 nhóm · 80 quyền · 1 vai trò**; có `eCommerce.Settings(.Manage)` |
| ⑭ | `PUT …/settings/permissions` (permission không tồn tại) | **400** + `eCommerce:Settings:0001` |
| ⑮ | `POST …/settings/staff` + `GET` | **200** — nhân viên mới, `phamViHieuLuc = All` |
| ⑯a | `GET /{business}/customer/{id}` | **200** — đủ **5 tab**: Tổng quan · Đơn hàng · Hoá đơn bán lẻ · Công nợ · Chăm sóc |
| ⑯b | `GET /{business}/settings/staff` | **200** — có *"Nhân viên"*, *"Ma trận quyền"*, *"Phạm vi dữ liệu"* |

> ⚠️ **MỘT LỖI THẬT do probe bắt (đáng ghi):** lượt chạy đầu, `DoiChieuSoDu.tienTichLuyKhop = false`.
> Nguyên nhân: `GetChiTiet`/`GetDoiChieuSoDu` cộng **`TotalPayment`** trong khi sổ khách ghi
> **`Subtotal − DiscountTotal`** ⇒ lệch đúng phần **điểm đã tiêu** (`97.000` vs `100.000`).
> Sửa: **một** công thức (`D-45`) ở **cả hai** nơi. 📌 Nếu chỉ có unit test thì **không** thấy: nó cần
> một giao dịch thật có điểm/chiết khấu.

### 1.5 Cổng tài liệu + Mermaid

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** (số đo ở §7). Mọi tài liệu mới/sửa (`handover-n8`,
`decisions` D-45/D-46, `07 §13`, `08 §3/§8`, `README`) đã được cổng này quét — **0 link chết**.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **thuộc WP-07** nhưng **cố ý cắt** (có lý do, không phải bỏ sót):

1. **Không có màn CRUD đầy đủ cho cửa hàng / phòng ban** — API `POST/PUT /settings/stores|departments` **đã
   có**, nhưng FE mới hiển thị (form thêm nhân viên chọn cửa hàng/phòng ban). 📌 Lý do: `07 §WP-07` chỉ
   yêu cầu FE *"nhân viên & ma trận quyền"*; thêm 2 màn nữa là **mở rộng phạm vi**.
2. **Không gán vai trò cho người dùng** (ABP Identity `AbpUserRoles`) — màn nhân viên chỉ lưu `RoleName`
   trên `Staff`. 📌 Người dùng thật vẫn phải được gán vai trò ở màn Identity của ABP (chưa làm ở GĐ này).
3. **`RoleDataScope` là cấu hình theo VAI TRÒ**, chưa theo **từng nhân viên**. Nhiều vai trò ⇒ gộp **rộng
   nhất** (`D-46`). 📌 Cần phạm vi riêng từng người thì phải thêm bảng `StaffDataScope` — **không** làm nửa vời.
4. **Chưa chặn "tự nới quyền"**: người có `Settings.Manage` có thể đổi phạm vi của **chính vai trò mình**.
   📌 Cần đọc role người gọi + so sánh trước khi cho đổi (hoặc chỉ `admin` được đổi) — ghi ở §5 #2.
5. **`CustomerLevel.DiscountPercent` chỉ là THÔNG TIN** — **chưa** nối vào bảng giá (`Pricing`). 📌 Đừng báo
   *"khách VIP đã được giảm giá tự động"*.
6. **Điểm tích luỹ vẫn do client gửi** (`pointsEarned` trong `CreateRetailBillDto`, `D-42`); `WP-07` **chỉ**
   ghi vào sổ khách. Chưa có **chính sách tích điểm** (tỉ lệ theo %/loại hàng) — đó là việc `WP-08`/P1.
7. **Trả hàng không hoá đơn** vẫn chưa có (P0 chỉ trả từ hoá đơn — `D-42`) ⇒ sổ khách chỉ giảm qua hoá đơn.
8. **`DoiChieuSoDu` chỉ đối chiếu hoá đơn bán lẻ + phiếu trả**, **chưa** gồm đơn online (`OrdOrders`) —
   📌 vì đơn online **chưa** ghi nợ/điểm vào khách (nợ #6 của `handover-n6` vẫn mở cho `WP-07` phần đơn).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **`20261104025249_Wp07_KhachHang_NhanVien`** (migration thứ **58**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` — `cd src\eCommerce.DbMigrator; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run` |
| Nội dung | **11 cột** trên `CusCustomers` + **6 bảng** (`CusLevels` · `CusCares` · `SetStores` · `SetDepartments` · `SetStaffs` · `SetRoleDataScopes`) + **9 index** (6 unique **`NULLS NOT DISTINCT`** — **viết tay**) + **5 `CHECK`** |
| `Down` | Có: xoá 6 bảng + 11 cột (**mất** điểm/công nợ/cấp/chăm sóc đã ghi — không tái tạo được từ chứng từ nếu đã trả/huỷ) |
| Cách lùi | `dotnet ef database update 20261104015230_Wp06_Pos_BanLe -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+175 dòng, 0 xoá** (3.725 → 3.900); `PostgreSqlSchemaTests` **85 NND** xanh |
| Ánh xạ mã lỗi | **6** unique index mới đã map trong `ViPhamRangBuocDb` (`Customers:0021` cấp khách · `Customers:0023` cửa hàng/phòng ban/nhân viên/tài khoản/phạm vi); **5 `CHECK`** khai trong `RangBuocChuaAnhXa.txt` **kèm lý do** (domain đã ném trước) |

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-45** | **Sổ điểm/tiền tích luỹ/công nợ của khách**: ① **một cửa ghi** (`Customer.GhiNhanMuaHang`/`DaoGhiNhanMuaHang`) — POS/trả/huỷ **cùng UoW**; ② **công thức** `AccumulatedAmount = Σ(Subtotal − DiscountTotal) − Σ(GoodsAmount)` (**không** dùng `TotalPayment` — có VAT/điểm); ③ điểm **âm ⇒ NÉM** (`0018`), nợ **vượt hạn mức ⇒ NÉM** (`0019`) — `DebtLimit = 0` = **không đặt**; ④ **đảo** (huỷ/trả) **kẹp 0, không ném**; ⑤ cấp khách xếp bằng hàm thuần **tất định** (`MinAccumulatedAmount → SortOrder → Id`); ⑥ **không** ghi sổ ở FE |
| **D-46** | **Phạm vi dữ liệu**: `RoleDataScope(RoleName, All/Store/Own)`; **mặc định `All`** khi chưa cấu hình (không đổi hành vi cũ); nhiều vai trò ⇒ **rộng nhất** (`Min` theo số enum, `All = 0`); `Own` = `CreatorId`; `Store` = **kho của cửa hàng** (`Store.WarehouseId`) — thiếu kho ⇒ **rỗng**, **KHÔNG** nới thành `All`; lọc **server-side** ở **DS + đọc chi tiết**; mã lỗi riêng `Pos:0028`/`Order:0012` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. ⚠️ **`Own` dựa vào `CreatorId`**: đơn tạo bởi **job/sàn** (`CreatorId = null`) **không** thuộc `Own` —
   đúng nghĩa *"dữ liệu của tôi"*, nhưng 📌 nhân viên bật `Own` sẽ **không thấy** đơn sàn về cho mình.
   Nếu nghiệp vụ cần *"đơn được giao cho tôi"* thì phải thêm cột `AssignedStaffId` (chưa có).
2. ⚠️ **Chưa chặn tự nới quyền**: người có `Settings.Manage` đổi được phạm vi của **vai trò mình đang giữ**
   ⇒ tự `Own → All`. 📌 Trước khi mở màn này cho nhiều người, phải thêm guard (so role người gọi) — hiện
   chỉ `admin` có quyền đó nên rủi ro **thấp nhưng có thật**.
3. ⚠️ **`Store` phạm vi cần `Store.WarehouseId`**: cửa hàng chưa gắn kho ⇒ nhân viên thấy **rỗng** (cố ý).
   📌 Log `Warning` `[WP07-DATASCOPE-THIEU-KHO]` — ai thấy DS rỗng thì **đọc log trước khi sửa code**.
4. ⚠️ **`Customer.GhiNhanMuaHang` KHÔNG idempotent** (cố ý — hoá đơn đã idempotent theo `ClientBillId`).
   📌 Nếu ai đó gọi cổng **hai lần** cho cùng chứng từ (viết code sai, không phải retry mạng) thì số dư
   cộng hai lần. Đường retry đã dừng ở `alreadyExisted` **trước** khi tới cổng.
5. ⚠️ **Sửa tay `Points`/`DebtBalance` bằng SQL sẽ làm `DoiChieuSoDu` báo LỆCH** — đó là **cảnh báo đúng**,
   đừng "sửa cờ". Muốn đính chính hợp lệ thì tạo chứng từ (bán/trả) hoặc bổ sung nghiệp vụ *"điều chỉnh sổ"*
   (chưa có).
6. 📌 **Probe để lại dữ liệu** (`WP07-…`, `VIP-…`, `NV-…`, `POS-…`, `PT-…`) — **cố ý**, để phiên sau kiểm SQL.
7. ⚠️ **`Settings.*` chưa cấp cho vai trò nào ngoài `admin`**: thêm permission **không** tự cấp — phải chạy
   `DbMigrator` (đã chạy) hoặc tick ở ma trận quyền.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-07 → mở WP-08.**

1. Đọc `07 §WP-08` — Reporting: `FactSalesLine`/`FactInventorySnapshot`/`Dim*`/`AggDaily*`, job watermark,
   `GET /reports/revenue|orders/summary|inventory/summary`, Dashboard 4 KPI.
2. ⚠️ **DoD của WP-08 là một PHÉP SO SÁNH**: doanh thu báo cáo **=** `SUM(RetailBills Completed) + SUM(Orders)`
   trong cùng kỳ — 📌 hoá đơn **Voided** phải bị loại; và phải dùng **cùng** định nghĩa tiền với `D-45`
   (`Subtotal − DiscountTotal`) khi báo cáo doanh số hàng, hoặc **ghi rõ** nếu báo cáo theo `TotalPayment`.
3. Chạy lại probe `tools/probe-khach-hang-wp07.ps1` sau khi sửa **bất kỳ** đường ghi nào của POS/khách —
   nó là lưới an toàn cho **5 bất biến** của sổ điểm/công nợ.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
dotnet test test\eCommerce.IntegrationTests
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\probe-khach-hang-wp07.ps1      # 28 lời gọi HTTP (tự bật/tắt Host + Next)
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N8`) |
| Commit | **chưa commit** phiên này — mã nguồn `N7`+`N8` nằm trong working tree (**32 ` M`** + **97 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A` (trong 97 mục `??` có **69 rác khảo sát/probe** từ trước + 28 tệp mới của `N7`/`N8`) |
| Push / PR | Chưa |
| Tệp `N8` đã chạm | **BE:** `src/eCommerce.Domain.Shared/{Enums.KhachHang.cs,SettingsErrorCodes.cs,eCommerceConsts.cs,Orders/OrderErrorCodes.cs,Pos/PosErrorCodes.cs}` · `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` · `modules/Customers/**` (Domain: `Customer`, `CustomerLevel`, `CustomerCare`, `ToChucCauHinh`, `NghiepVuKhach`, `CustomerTabs`, `CustomerErrorCodes`; Contracts: `KhachHangWp07Dtos`, `CauHinhWp07Dtos`, `ICustomerAppService`, `IDataScopeResolver`+`ICustomerLedger`; Application: `CustomerAppService`, `CustomerLevelAppService`, `StaffAppService`, `DataScopeResolver`, `CustomerLedger`; EF: `KhachHangWp07ModelCreating`) · `src/eCommerce.EntityFrameworkCore/{eCommerceDbContext.cs,Migrations/*Wp07*,Migrations/eCommerceDbContextModelSnapshot.cs}` · `src/eCommerce.Application/Settings/PermissionMatrixAppService.cs` · `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs` · `src/eCommerce.HttpApi/{Controllers/CustomersController.cs,Controllers/SettingsController.cs}` · `modules/Pos/…Application/{RetailBillAppService,RetailReturnAppService,PosApplicationModule}.cs` · `modules/Sales/…{OrderAppService,OrderDtos}.cs` · `test/eCommerce.IntegrationTests/{Assets/schema.sql,PostgreSqlSchemaTests.cs}` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}` · `test/eCommerce.Tests/{Customers/Wp07KhachHangTests.cs,Data/RangBuocChuaAnhXa.txt}`. **FE:** `lib/{customer.ts,menu.ts}` · `components/{customer/SegmentForm.tsx,customer/CareForm.tsx,settings/StaffForm.tsx,settings/ScopeSelect.tsx,settings/PermissionMatrix.tsx}` · `app/[business]/customer/{list/page.tsx,[id]/page.tsx}` · `app/[business]/settings/staff/page.tsx`. **Khác:** `tools/probe-khach-hang-wp07.ps1` · docs (`decisions`, `07`, `08`, `README`, `handover này`) |
