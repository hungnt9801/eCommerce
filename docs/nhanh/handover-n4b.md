# Handover `N4b` — WP-04 (BE) phần còn lại: `decimal(18,3)` hoá số lượng

> Ngày 02/11/2026 · nhánh `feature/wp-03-catalog` (cây vẫn gồm `N1…N4` **chưa commit**)
> Kế hoạch: [07 §WP-04](07-ke-hoach-trien-khai.md) việc **1** · Quyết định: [D-31](decisions.md) (**thay thế D-25**), [D-32](decisions.md) (**thay thế D-26**)
> **Mục tiêu phiên:** làm nốt việc **duy nhất** còn lại của WP-04 (BE) ⇒ **WP-04 (BE) HOÀN TẤT** (việc 1–6 của 07).

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** (N1…N4 chưa commit) |
| 2 | `git switch` | đã ở `feature/wp-03-catalog` · `HEAD = 713acdd` |
| 3 | `docker compose up -d` | postgres + redis **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (tăng dần) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1768/1768 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** |
| — | Migration / NND | **53** / **74** |

### 1.2 Đã thi công (`int → decimal(18,3)`)

| Nhóm | Nội dung | Số cột |
|---|---|---|
| **Snapshot tồn kho** | `InvItems`: `OnHand`, `Reserved` + 8 chiều (`Transferring`, `Shipping`, `Holding`, `Damaged`, `Warranty`, `WarrantyHolding`, `PreOrder`, `WaitingInbound`) | **10** |
| **Sổ cái** | `InvStockLedger.QuantityDelta`, `BalanceAfter` — 📌 nếu sổ cái vẫn `int` thì phiếu 1,5 kg bị **làm tròn ngay ở nguồn sự thật** | **2** |
| **Dòng phiếu** | `InvStockDocumentLines.Quantity` (+ `StockDocumentLine` ctor/validation `<= 0`) | **1** |
| **Lịch sử đối soát** | `ReconRunMismatches.OnHand/TongSoCai/ChenhLech` — 📌 để `int` thì lệch 0,5 bị **ghi thành 0** ⇒ điều tra sai | **3** |
| **Hàm thuần** | `InventoryItem.ApplyMovement`, `CanReserve`; `QuyetDinhGiuCho.KiemTra/CoTheBan`; `HanMucKenh.TinhHanMuc/VuotHanMuc/NenChan/HanMucVoNghia`; `TinhButToanChungTu` (`ButToan.DoLon`, `ChoTungDong`, `ChoTungDongTheoMode`, `ChoTungKiemKe`); `DoiSoatTonKho.Tong/Lech`; `NguongLech.Xet/VuotNguong` | — |
| **DTO / API** | `TonKhoDto`, `StockBalanceDto` (12 số), `StockDocumentLineDto.Quantity`, `CreateStockDocumentLineDto.Quantity`, `LechTonKhoDto`, `DongLechDto`, `PhanBoKenhDto` (`DaBan/DangGiu/DaTra` + dictionary theo kho) | — |
| **Migration 54** | `20261102102215_Wp04b_SoLuong_Decimal` — **16 `AlterColumn`** (int → `numeric(18,3)`), `Down` đưa về `integer` | **16** |

**Độ chính xác:** `numeric(18,3)` cho số lượng (gram/ml/cm là đủ); `ConversionFactor` của `ProductUnit` vẫn `numeric(18,4)` — **hệ số ≠ số lượng**, cố ý khác độ chính xác.

**Chỗ CỐ Ý giữ `int` (ghi rõ, không phải bỏ sót):** `StockReservation.Quantity`, `StockAllotment.AllottedQuantity/ReservedQuantity` (ngân sách kênh `INV-003`). 📌 Đơn hàng/POS hiện chỉ sinh số nguyên; đổi hai chỗ này là việc của **WP-05/POS** khi cho phép đơn số lẻ — nếu làm bây giờ sẽ phải sửa `CHECK` + `schema.sql` của `INV-003` mà **không có ca dùng**.

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** · 31,4 s — 9 `CS8604` **có sẵn** (Address 6 + Sales 3), **không phát sinh warning mới** |
| `dotnet test test\eCommerce.Tests --no-build` | **1773/1773 pass** (+**5** ca số lẻ so với 1768) |
| `dotnet test test\eCommerce.IntegrationTests --no-build` | **90/90 pass** · 744 ms |
| Migration | **54** — `__EFMigrationsHistory` có `20261102102215_Wp04b_SoLuong_Decimal` |
| Cột thật trên DB | `InvItems.OnHand` = **`numeric / 18 / 3`** (`information_schema.columns`) |
| NND (DB dev) | **74** — **không đổi** (không thêm unique index nào) |
| `schema.sql` | sinh lại từ chính migration: **+374 dòng, 0 xoá** |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 106 `.md`, **472** link, **347** đường dẫn backtick, **0** chết |
| `npm --prefix tools run check:mermaid` | **PASS** — 213 khối, 0 lỗi |
| `pnpm spec:snapshot` + `pnpm gen:api` | **12/12 path `/api/v1`** — `quantity?: number` (trước: `integer`) |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — schema TS mới không phá màn đang có |

### 1.4 Bằng chứng **số lẻ sống sót qua HTTP thật** (`tools/probe-kho-wp04.ps1`, `exit 0`)

```
⑤ TỒN KHO sau khi nhập 10        → onHand: 10.000 · available: 10.000 · inWarehouse: 10.000
⑩ BÁN LẺ 15 > tồn 10             → 403 eCommerce:Inventory:0001 (data.onHand: -5.000)
⑫ BẤT BIẾN tổng sổ cái = ảnh chụp → 10.000 = 10.000
```

📌 **12/12 ca của probe vẫn xanh sau khi đổi kiểu** (không chỉ chạy lại phần số lẻ) — tức đổi kiểu **không** làm hỏng đường ghi sổ/idempotency/trạng thái.

**Backfill** (`backfill-inventory.ps1`, chạy trên cột `numeric`): `LỆCH 0` · `THIẾU-DÒNG 0` · `KHONG-HOP-LE 1` (nợ E2E cũ — §5.1) ⇒ `exit 0` với `-ChapNhanKhongHopLe`.

**Unit test mới cho số lẻ** (`Wp04bSoLuongThapPhanTests`, 5 ca):

| Ca | Khẳng định |
|---|---|
| `Nhap_1_5_kg__OnHand_va_Available_GIU_so_le` | nhập 1,5 − bán 0,25 ⇒ `OnHand 1.25`, `Available 1.25` |
| `Ba_so_thap_phan_la_DU__va_khong_lam_tron_khi_cong_don` | 1000 × 0,001 = **1,000** (nếu có `(int)` ở đâu đó thì kết quả 0) |
| `But_toan_giu_so_le__khong_tra_ve_int` | `ButToan.DoLon = 2.5` |
| `Dong_phieu_nhan_so_le__va_serial_van_phai_bang_so_luong` | dòng 2,5 + 2 serial ⇒ **NÉM** `0029` |
| `So_luong_0_hoac_AM_thi_NEM__nhung_so_duong_RAT_NHO_thi_HOP_LE` | `0`/`-0,5` NÉM; `0,0001` **hợp lệ** (không bị làm tròn về 0) |

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có gì đang sửa dở.** WP-04 (BE) theo 07 việc 1–6 **đã xong**; các mục sau **không thuộc** WP-04 (BE):

1. **WP-04 (FE) = `N5`** — màn DS phiếu + tạo phiếu + cột tồn. API + `schema.d.ts` đã sẵn.
2. **`StockReservation.Quantity` / ngân sách kênh vẫn `int`** — chờ WP-05/POS cho đơn số lẻ (§1.2).
3. **4 chiều chưa có đường ghi** (`Transferring`, `WarrantyHolding`, `PreOrder`, `WaitingInbound`) — WP-05/POS.
4. **Serial/IMEI vẫn chỉ là chuỗi** trên dòng phiếu — chờ API ghi serial của WP-03 nửa sau.
5. **Không có endpoint sửa phiếu `Draft`** — `N5` cần thì thêm (domain đã có `YeuCauChoPhepSuaNoiDung`).
6. **`RowVersion`**: **không thêm** — D-32 (lý do: `ConcurrencyStamp` đã là token duy nhất).

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | `20261102102215_Wp04b_SoLuong_Decimal` (migration thứ **54**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (`cd src\eCommerce.DbMigrator; dotnet run`) |
| Nội dung | **16 `AlterColumn`**: `InvItems` ×10 · `InvStockLedger` ×2 · `InvStockDocumentLines` ×1 · `ReconRunMismatches` ×3 — tất cả `integer → numeric(18,3)` |
| `Down` | Có (EF sinh): trả 16 cột về `integer`. ⚠️ Lùi kiểu **làm tròn** dữ liệu lẻ ⇒ chỉ lùi khi chấp nhận mất phần thập phân |
| Cách lùi | `dotnet ef database update 20261102095854_Wp04_Inventory_Buckets_Mode -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+374 dòng, 0 xoá**; `PostgreSqlSchemaTests` (74 NND) xanh |
| ⚠️ Bẫy đã dặp | `dotnet ef` để lại **Host sống** ⇒ build sau **khoá DLL**; và **chạy `DbMigrator` bật lại** `ShouldChangePasswordOnNextLogin` ⇒ token `password` trả `invalid_grant`/`ID2024`. Cách xử: `Stop-Process eCommerce.HttpApi.Host` trước build; `update "AbpUsers" set "ShouldChangePasswordOnNextLogin"=false where "UserName"='admin';` trước khi lấy token |

---

## 4. Quyết định mới

| ID | Nội dung |
|---|---|
| **D-31** | **Thay thế D-25** — đã thực hiện `decimal(18,3)` cho **16 cột** + toàn bộ tầng đọc/ghi **trong cùng WP-04**; độ chính xác 3 số lẻ; `Reservation`/ngân sách kênh vẫn `int` tới WP-05/POS |
| **D-32** | **Thay thế D-26** — **vẫn không** thêm `RowVersion` (đã có `ConcurrencyStamp`), ghi rõ để mục `RowVersion` của 07 không bị coi là bỏ sót |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Nợ dữ liệu dev DB (đo được, KHÔNG tự sửa):** cặp `K97E2E-SKU-P1` có sổ cái `Reserved = 199.000` nhưng `OnHand = 0` (rác E2E). `backfill-inventory.ps1` **từ chối** dựng lại (đúng thiết kế). Muốn sạch: người quyết định xoá sổ cái/giữ chỗ rác của SKU đó rồi chạy `-Apply`. 🔒 **Đừng** dùng `-ChapNhanKhongHopLe` để che lệch MỚI.
2. ⚠️ **`Down` của migration 54 làm tròn** dữ liệu lẻ (integer). Ai lùi migration phải biết.
3. ⚠️ **JSON của API giờ trả `10.000`** (decimal) thay vì `10`. Client nào so sánh **chuỗi** hoặc hiển thị thô sẽ thấy khác; UI phải format theo `vi-VN` (màn `N5`).
4. ⚠️ **`Assert.Equal(intLiteral, <decimal>)`** trong xUnit: đa số **vẫn xanh** (generic `Equal<decimal>` nhận implicit conversion), nhưng khi giá trị đi qua **`object`** (ví dụ `BusinessException.Data["k"]`) thì so sánh **boxed** ⇒ đỏ dù hai số bằng nhau (đã gặp thật: `Expected: -4, Actual: -4`). Khi assert giá trị đã là `decimal`, dùng literal `m`.
5. ⚠️ **Test đếm `ColumnType` trong `PostgreSqlSchemaTests` không kiểm kiểu số** — đổi kiểu cột mà quên `HasPrecision` sẽ chỉ lộ khi chạy migration trên DB thật. Giữ thói quen: sau mỗi migration kiểm `information_schema.columns` (đã làm ở §1.3).
6. 📌 **Probe để lại dữ liệu** (`WP04-<random>`) trong DB dev — vô hại, nhưng nhớ chúng là rác kiểm chứng.
7. ⚠️ **Cây làm việc vẫn dơ** (N1…N4b **chưa commit**) — `git checkout/reset` mạnh tay sẽ mất cả 5 lớp việc.

---

## 6. Việc đầu tiên của phiên sau

1. **Commit** `N1…N4b` thành các commit riêng (hoặc ít nhất `git stash create`) — cây đang trộn 5 phiên, chưa có điểm lùi.
2. **`N5` — WP-04 (FE)**: DS phiếu + tạo phiếu + cột tồn 3 số, dùng `schema.d.ts` (**12 path `/api/v1`**, `quantity?: number`). Điều kiện xanh: Post → tồn đổi trên UI; Post lại bị chặn (`Inventory:0010`); hiển thị số lẻ đúng `vi-VN`.
3. (Khi WP-05 bắt đầu) đổi `StockReservation.Quantity` + ngân sách kênh sang `numeric(18,3)` **cùng lúc** với đơn số lẻ — đừng đổi lẻ tẻ.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
pwsh -File .\tools\probe-kho-wp04.ps1     # 12 ca HTTP thật (tự bật/tắt Host)
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên không còn khớp phạm vi: đang chứa `N1…N4b`) |
| Commit | **CHƯA commit**; `HEAD = 713acdd` |
| Push / PR | Chưa |
| Tệp `N4b` đã chạm | `src/eCommerce.Domain.Shared/Enums.Inventory.cs` (không đổi ở phiên này) · `modules/Inventory/eCommerce.Inventory.Domain/{StockLedger,StockDocument,TinhButToanChungTu,HanMucKenh,StockReservation,ChotGiuChoService,StockPostingService,StockReservationService}.cs` · `modules/Inventory/eCommerce.Inventory.EntityFrameworkCore/InventoryDbContextModelCreatingExtensions.cs` · `modules/Inventory/eCommerce.Inventory.Application.Contracts/{TonKhoDtos,StockDocumentDtos,WarehouseDtos}.cs` · `modules/Inventory/eCommerce.Inventory.Application/{TonKhoAppService,StockDocumentAppService}.cs` · `modules/Reconciliation/**` (`DoiSoatTonKho`, `NguongLech`, `ReconRun`, `IStockBalanceReader`, `InventoryReconciliationService`, `DoiSoatAppService`, `DoiSoatDtos`, EF mapping) · `modules/Sales/eCommerce.Sales.Application/{DonHangAppService}.cs` + `Application.Contracts/DonHangDtos.cs` · `src/eCommerce.EntityFrameworkCore/{Reconciliation/StockBalanceReader.cs,Migrations/*Wp04b*}` · `test/…/Assets/schema.sql` · `test/eCommerce.Tests/Inventory/{InventoryBatBienTests,Wp04TonKhoChieuTests,DoiSoatTonKhoTests,XuLyLechDoiSoatTests}.cs` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}` · docs (`decisions`, `07`, `08`, `README`, handover này) |
