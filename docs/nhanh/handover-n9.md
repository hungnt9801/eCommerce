# Handover `N9` — WP-08: **Báo cáo** (read-model + job theo mốc + Dashboard 4 KPI + 2 báo cáo)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-08](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-47](decisions.md), [D-48](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-08** (read-model báo cáo + job tổng hợp theo watermark + API
> `/api/v1/reports/*` + Dashboard 4 KPI + 2 báo cáo FE), **không mở rộng phạm vi**.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **129 mục** (**32 ` M`** của `N7`/`N8` chưa commit + **97 `??`** rác khảo sát/probe) |
| 2 | `git log --oneline -1` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose up -d` | `ecommerce-postgres` + `ecommerce-redis` **Running** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1933/1933 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — 110 tệp `.md`, 534 link, 355 đường dẫn, **0 chết** |

📌 Baseline **khớp** `handover-n8` (1933 test · migration 58 · NND 85). Không có sai lệch phải dừng.

### 1.2 Đã thi công (đúng 4 việc BE + 3 việc FE của `07 §WP-08`)

| # | Việc (`07 §WP-08`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **Schema `report`** | Module `Reporting` 4 project (Domain/Contracts/Application/EF) · **8 bảng `Rpt*`**: `FactSalesLines` · `FactInventorySnapshots` · `AggDailySales` · `Watermarks` · `DimDates/Products/Stores/Staffs` — **migration ADDITIVE** | `dotnet ef migrations has-pending-model-changes` = **“No changes”**; **8 index `NULLS NOT DISTINCT`** viết tay (NND **85 → 93**) |
| 2 | **Job theo watermark** | `TongHopBaoCaoService` (hàm thuần + đọc repo, **không** gọi app service) · `ReportingAggregationWorker` (15 phút, `RunOnStart`, log vô điều kiện) · `KhoangTongHop` (mốc, backfill, trần 366 ngày, mốc **chỉ tiến**) · **thay thế theo `(ngày, nguồn)`** trong CÙNG transaction + khoá phân tán `report:tong-hop` | probe: chạy **2 lần** ⇒ **17** dòng sự kiện · **2** dòng tổng hợp · **38** ảnh tồn kho **KHÔNG đổi** |
| 3 | **API báo cáo** | `/api/v1/reports` **5 path**: `revenue` · `orders/summary` · `inventory/summary` · `dashboard` (4 KPI) · `aggregate` (quyền **riêng** `eCommerce.Reports.Aggregate`) | `pnpm spec:snapshot` **51/51 path** `/api/v1` (46 → 51); probe **20 lời gọi HTTP**, mọi ca 200 |
| 4 | **Lõi hàm thuần + quyền** | `NgayKinhDoanh` (UTC+7, nửa khoảng) · `TongHopDoanhThu` (công thức **một chỗ**) · `KhoangTongHop` · `RptWatermark` (chỉ tiến) · quyền `eCommerce.Reports`(+`.Aggregate`) khai **cả hai nơi** | `Wp08BaoCaoTests` **28** test (DoD so sánh + biên ngày + idempotent + hợp đồng route/quyền) |
| 5 | FE **Dashboard 4 KPI** | `/[business]`: doanh thu hôm nay · hoá đơn · đơn hàng · tồn khả dụng + biểu đồ **7 ngày** (SVG) | `pnpm build` ✓ — route `/[business]` **173 B** |
| 6 | FE **báo cáo doanh thu** | `report/revenue`: 4 thẻ tổng · **biểu đồ cột xếp chồng** (SVG, trục Y từ **0**) · bảng ngày/tháng · **Xuất CSV** · bảng đơn theo trạng thái + câu **định nghĩa tiền** in từ server | route **591 B**; probe ⑧ thấy đủ *Doanh thu · Biểu đồ doanh thu · Tổng doanh thu · Xuất CSV · Định nghĩa tiền* |
| 7 | FE **báo cáo tồn kho** | `report/inventory`: 4 thẻ · bảng theo kho · **sắp hết** (≤ ngưỡng) · **Xuất CSV** · ghi rõ **ngày chụp** (không hứa "tồn ngày X") | route **591 B**; probe ⑨ thấy *Tồn kho · Mặt hàng sắp hết · Khả dụng · Xuất CSV* |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** — **không** phát sinh warning mới (9 `CS8604` có sẵn ở Address/Sales) |
| `dotnet test test\eCommerce.Tests` | **1961/1961 pass** (**+28** so với 1933) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** (**+6**: 1 đếm NND + 5 ca ràng buộc read-model) |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** |
| `pnpm --filter @ecommerce/admin-web build` | **✓** — **23 route** (21 cũ + 2 mới) |
| `pnpm spec:snapshot` + `pnpm gen:api` | **51/51 path** `/api/v1` (46 cũ + **5** mới) |
| Migration | **59** — `20261104033300_Wp08_BaoCao_Reporting` (có trong `__EFMigrationsHistory`) |
| `schema.sql` | sinh lại từ chính migration: **3.900 → 4.067 dòng** (**+167**, 0 xoá) |
| NND (DB dev) | **85 → 93** (+8 index `Rpt*`, **cả 8 viết tay** `NULLS NOT DISTINCT`) |
| `pwsh -File .\tools\probe-bao-cao-wp08.ps1` | **`exit 0`** — **20 lời gọi HTTP** (+5 lời gọi setup qua Host) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (số đo ở §1.5) |

### 1.4 Bằng chứng HTTP thật — `tools/probe-bao-cao-wp08.ps1` (`exit 0`)

Setup **bằng API thật**: tạo kho → nhập 20 × 1 SKU (@100.000) → post; chèn **1 đơn hàng** (dev-only SQL:
3 × 80.000, chiết khấu đơn 15.000 ⇒ **225.000**) + **1 đơn ĐÃ HUỶ** (1.000.000); bán **2 hoá đơn POS**
(200.000 và 100.000) rồi **huỷ hoá đơn thứ hai**; đăng nhập **NextAuth** → gọi qua **BFF** (`D-12`).

| # | Ca | Kết quả đo |
|---|---|---|
| ①a | `POST /pos/bills` (2 món @100.000) | **200** — `subtotal 200.000` |
| ①b | `POST /pos/bills/{id}/void` (hoá đơn 100.000) | **200** — `status Voided` |
| ②a | `POST /reports/aggregate` (hôm nay) | **200** — `soDongSuKien 17` · `soDongTongHop 2` · `rong false` · mốc `2026-11-04` |
| ②b | **chạy LẠI** cùng khoảng | **200** — DB **17 / 2 / 38** dòng **giữ nguyên** ⇒ **DoD #2** |
| ③ | `GET /reports/revenue?tuNgay&denNgay` | **200** — `tongDoanhThu 1.470.000` = SQL độc lập **1.245.000 + 225.000** ⇒ **DoD #1** |
| ③b | hoá đơn **đã huỷ** + đơn **đã huỷ** trong read-model | **0** dòng (cả hai) — huỷ **không** vào doanh thu |
| ④ | `GET /reports/orders/summary` | **200** — `tongDon 2` · `soDonHuy 1` · `giaTriDon 225.000` |
| ⑤ | `GET /reports/inventory/summary` | **200** — `daCoDuLieu true` · `ngayChup 2026-11-04` · 31 kho · 38 SKU · tồn **336** |
| ⑥ | `GET /reports/dashboard` | **200** — `doanhThuHomNay 1.470.000` (**khớp** báo cáo) · `soHoaDonBanLeHomNay 12` · `ngayTonKhoMoiNhat 2026-11-04` |
| ⑦ | kỳ **chưa** tổng hợp (200 ngày trước) | **200** — `daTongHop false` (**không** phải "0 đồng") |
| ⑧ | trang `/{business}/report/revenue` | **200** — đủ *Doanh thu · Biểu đồ doanh thu · Tổng doanh thu · Xuất CSV · Định nghĩa tiền* |
| ⑨ | trang `/{business}/report/inventory` | **200** — đủ *Tồn kho · Mặt hàng sắp hết · Khả dụng · Xuất CSV* |
| ⑩ | trang `/{business}` (Dashboard) | **200** — đủ *Tổng quan · Doanh thu hôm nay · Hoá đơn bán lẻ · Tồn khả dụng · Doanh thu 7 ngày* |

> 📌 **Job thật sự chạy trong Host** (không chỉ "đã đăng ký"): log Host có **2** dòng của
> `ReportingAggregationWorker` — *"bắt đầu một lượt (mốc …)"* và *"xong — 0 ngày … rỗng=True"*
> (`rỗng=True` vì mốc đã ở hôm nay — đúng luật **RỖNG LÀ HỢP LỆ**, xem `KhoangTongHop`).

> ⚠️ **DoD là phép SO SÁNH, không phải lời khẳng định:** vế phải được tính bằng **SQL độc lập** trong
> `psql` (`SUM("Subtotal" − "DiscountTotal")` của hoá đơn `Status = 0` **+** `Σ(UnitPrice × Quantity −
> DiscountAmount) − DiscountTotal` của đơn `NOT IsCancelled`), **cùng** biên ngày `[00:00, 24:00)` giờ VN.
> 📌 Nếu chỉ so API với chính nó thì mọi lỗi công thức đều "khớp" — đúng lỗi `WP-07` đã trả giá.

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **111** tệp `.md`, **545** link markdown,
**360** đường dẫn backtick, **0 chết**. Mọi tài liệu mới/sửa (`handover-n9`,
`decisions` D-47/D-48, `07 §WP-08` + `§13`, `08 §3`, `README`) đã được cổng này quét.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **thuộc phạm vi báo cáo** nhưng **cố ý cắt** (có lý do ở
`D-47`/`D-48`, không phải bỏ sót — mỗi mục có **nhận chủ**):

1. **Báo cáo CHƯA trừ HÀNG TRẢ LẠI** (`PosRetailReturns`, `OrdReturns`). 📌 Đây là **doanh thu gộp** (gross):
   có trả hàng thì con số **cao hơn** thực thu. Muốn trừ phải thêm **nguồn fact thứ hai** (dòng trả âm) —
   `D-48` ④ ghi rõ; nhận chủ **P1 (báo cáo trả hàng / lãi gộp)**. 🚨 **Đừng dùng báo cáo này để tính lương/
   hoa hồng** khi cửa hàng có trả hàng.
2. **CHƯA áp phạm vi dữ liệu (`D-46`) vào báo cáo** — báo cáo là số liệu **toàn tenant** cho người có
   `eCommerce.Reports`. 📌 Nhân viên `Own` xem được tổng doanh thu của cửa hàng là **lỗ hổng thật** (không phải
   lỗi hiển thị) ⇒ nhận chủ **P1** (phạm vi cho báo cáo); giảm nhẹ: quyền `Reports` hiện chỉ cấp cho `admin`.
3. **`RptDimStaffs.DisplayName = null`** — tên nhân viên nằm ở module `Customers` (`SetStaffs`); `WP-08` **không**
   tham chiếu module đó để tránh kéo tầng Application của nó vào. Nhận chủ **P1** (nối tên vào chiều).
4. **Chiều sản phẩm chưa có danh mục/thương hiệu** ⇒ **chưa** báo cáo theo nhóm hàng/kênh. Nhận chủ **P1**.
5. **Chưa có màn/nút "chạy lại tổng hợp" trên FE** — chỉ có `POST /api/v1/reports/aggregate` (có quyền riêng).
   `07 §WP-08` yêu cầu *"Dashboard + 2 báo cáo"* ⇒ nút này là **mở rộng**, nhận chủ **P1**.
6. **`/reports/orders/summary` đọc HẾT đơn trong kỳ** (không phân trang) — trần 366 ngày nhưng với 100k đơn/kỳ
   sẽ nặng; rủi ro ghi ở §5 #2. Nhận chủ **P1** (tổng hợp theo trạng thái vào `AggDaily`).
7. **Ảnh tồn kho KHÔNG phải lịch sử**: từ `N9` mỗi ngày có **một** ảnh (nếu job chạy ngày đó); **dữ liệu
   trước `N9` không có ảnh**. 📌 UI ghi rõ **ngày chụp** thay vì để người dùng tưởng "tồn hôm nay".
8. **Chưa có kiểm tra "job có chạy" trên UI** — `ngayTongHopMoiNhat` đã trả về (Dashboard + báo cáo hiện nó),
   nhưng **chưa** có cảnh báo khi mốc cũ hơn hôm nay > 1 ngày. Nhận chủ **P1**.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **`20261104033300_Wp08_BaoCao_Reporting`** (migration thứ **59**) |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` — `cd src\eCommerce.DbMigrator; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run` |
| Nội dung | **8 bảng** `Rpt*` + **11 index** (8 unique **`NULLS NOT DISTINCT`** — **viết tay**) + **7 `CHECK`** (chặn tiền âm, nguồn lạ, số lượng âm, chiều tồn âm) |
| `Down` | Có: xoá 8 bảng (**mất** read-model — **tái tạo được** bằng chạy lại `aggregate`, trừ ảnh tồn kho của những ngày đã qua) |
| Cách lùi | `dotnet ef database update 20261104025249_Wp07_KhachHang_NhanVien -p src\eCommerce.EntityFrameworkCore -s src\eCommerce.DbMigrator` |
| `schema.sql` | sinh lại: **+167 dòng, 0 xoá** (3.900 → 4.067); `PostgreSqlSchemaTests` **93 NND** xanh |
| Ánh xạ mã lỗi | **0** ràng buộc mới được map trong `ViPhamRangBuocDb` — **15** dòng (8 unique + 7 `CHECK`) khai trong `Data/RangBuocChuaAnhXa.txt` **kèm lý do**: read-model **không có đường ghi từ người dùng**, domain đã chặn trước với mã riêng |

📌 **Đã chạy `DbMigrator` ⇒ quyền `eCommerce.Reports`(+`.Aggregate`) đã cấp cho role `admin`.** Nếu DB khác
chưa chạy migrator: mọi `GET /reports/*` trả **403** (permission chưa cấp) — **không** phải "chưa có dữ liệu".

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-47** | **Read-model báo cáo**: grain = **dòng chứng từ nguồn** (`SourceLineId`) với unique `(TenantId, Nguon, SourceLineId)`; **thay thế** theo `(ngày, nguồn)` (DELETE+INSERT cùng transaction) chứ **không** cộng dồn; `AggDaily` **không** phải nguồn sự thật thứ hai; **ngày kinh doanh UTC+7** + biên **nửa khoảng**; **mốc chỉ tiến**; **một lượt một khoá** phân tán; ảnh tồn kho **không phải lịch sử**; NND **85 → 93** |
| **D-48** | **Định nghĩa tiền**: `Σ thành tiền dòng − chiết khấu CHỨNG TỪ` (**khớp `D-45`**, **không** dùng `TotalPayment`); **loại** hoá đơn `Voided` + đơn `IsCancelled`; **đơn online** lấy `LineTotal − TaxAmount` (VAT tách riêng); chiết khấu > tiền dòng ⇒ **NÉM** `Reports:0004`; chứng từ **không có dòng** ⇒ **bỏ qua + log Warning** (không làm chết job); **chưa trừ trả hàng**, **chưa áp phạm vi dữ liệu**; `daTongHop=false` ≠ `0` |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. ⚠️ **Doanh thu là GROSS** (chưa trừ trả hàng) — xem §2 #1. 📌 Báo cáo **dùng để ra quyết định** nên con số
   này phải được **ghi rõ** ở nơi dùng (đã in `DinhNghiaTien` trên cả 3 màn).
2. ⚠️ **`ChupAnhTonKhoAsync` đọc TOÀN BỘ `InvItems`** (`GetListAsync` không phân trang) rồi mới ghi ảnh.
   Với ~100k cặp (kho, SKU) sẽ tốn bộ nhớ/thời gian mỗi ngày một lần. 📌 Sửa: đọc theo lô (`Skip/Take`) hoặc
   `ExecuteSqlRaw` ghi thẳng `INSERT ... SELECT` từ `InvItems` (nhanh hơn và không qua change-tracker).
3. ⚠️ **Job đọc theo TỪNG NGÀY** (`TinhNgayAsync` mỗi ngày 4 truy vấn): backfill 366 ngày ≈ 1.5k truy vấn —
   chấp nhận được nhưng **đừng backfill nhiều khoảng liên tiếp** trong một lượt. 📌 Sửa: truy vấn theo khoảng
   rồi group theo ngày trong bộ nhớ.
4. ⚠️ **Hai nguồn dùng CHUNG một khoảng tính từ `min(mốc)`**: nếu một nguồn chậm/hỏng thì lượt sau làm lại
   **cả hai** (an toàn, nhưng tốn). 📌 Đừng "tối ưu" bằng cách bỏ `min` — bỏ là một nguồn **kẹt vĩnh viễn**.
5. ⚠️ **Đổi `Reporting:GioLechUtc` sau khi đã có dữ liệu** ⇒ ngày kinh doanh đã ghi và ngày mới **lệch nhau**;
   phải **backfill** khoảng bị ảnh hưởng (và lý tưởng là xoá `Rpt*` rồi tổng hợp lại từ đầu).
6. ⚠️ **Ngày không chạy job ⇒ không có ảnh tồn kho** (cố ý, không bịa). Báo cáo tồn kho hiện **ảnh gần nhất**;
   UI có ghi `ngayChup` nhưng vẫn có người đọc nhầm là "tồn hôm nay".
7. ⚠️ **`Rpt*` tham chiếu chứng từ bằng GUID TRẦN (không FK)** — xoá cứng một chứng từ để lại dòng fact
   **mồ côi**; chỉ lượt tổng hợp của đúng ngày đó mới dọn. 📌 Đừng xoá cứng chứng từ; huỷ là đủ (`D-48` ②).
8. 📌 **Probe để lại dữ liệu** (`WP08-…`, `WP08-DON-…`, `POS-…`) — **cố ý**, để phiên sau kiểm SQL.
9. 📌 **Permission mới KHÔNG tự cấp**: `eCommerce.Reports`(+`.Aggregate`) chỉ có `admin` sau khi chạy
   `DbMigrator` — DB khác thì phải tick ở ma trận quyền.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-08 → mở WP-09.**

1. Đọc `07 §WP-09` — **Test & cổng**: Playwright **8 luồng** (login · tạo SP · nhập kho · bán lẻ · in · đổi
   trạng thái đơn · kết ca · export), `dotnet test` + `pnpm test` + `pnpm e2e` trong CI, job **openapi-drift**,
   mở rộng `tools/kiem-tat-ca.ps1` thêm bước FE. Nhớ `WP-10` (Ops: docker-compose `admin-web`, `.env.example`,
   health BFF) trong cùng phiên `N10` theo `08 §3`.
2. ⚠️ **Luồng "export" của Playwright phải bao gồm CSV báo cáo `WP-08`** (`report/revenue`, `report/inventory`)
   — nếu chỉ test export kho thì trang báo cáo mới **không có lưới an toàn** nào.
3. ⚠️ **`pnpm gen:api` không được diff**: phiên này đã sinh lại `schema.d.ts` + `openapi.v1.json` (**51 path**).
   Job drift trong CI sẽ đỏ nếu ai sửa controller mà quên sinh lại.
4. Chạy lại probe `tools/probe-bao-cao-wp08.ps1` sau khi sửa **bất kỳ** đường ghi của POS/đơn/kho — nó là lưới
   an toàn cho **2 bất biến** của read-model (không nhân đôi · chứng từ huỷ không vào doanh thu).

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
dotnet test test\eCommerce.IntegrationTests
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\probe-bao-cao-wp08.ps1          # 20 lời gọi HTTP (tự bật/tắt Host + Next)
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-09
và handover gần nhất `docs/nhanh/handover-n9.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-09** (phiên N10). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N9`) |
| Commit | **chưa commit** phiên này — mã nguồn `N7`+`N8`+`N9` nằm trong working tree (**36 ` M`** + **108 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A` (trong 108 mục `??` có **rác khảo sát/probe** từ trước + tệp mới của `N7`/`N8`/`N9`) |
| Push / PR | Chưa |
| Tệp `N9` đã chạm | **BE:** `src/eCommerce.Domain.Shared/eCommerceConsts.cs` · `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs` · `src/eCommerce.Application/{eCommerceApplicationModule,eCommerce.Application}.csproj` · `src/eCommerce.EntityFrameworkCore/{eCommerceDbContext.cs,eCommerceEntityFrameworkCoreModule.cs,eCommerce.EntityFrameworkCore.csproj}` · `src/eCommerce.HttpApi/{Controllers/ReportsController.cs,eCommerce.HttpApi.csproj}` · `src/eCommerce.HttpApi.Host/{eCommerceHttpApiHostModule.cs,appsettings.json}` · `src/eCommerce.EntityFrameworkCore/Migrations/*Wp08*` · **module mới** `modules/Reporting/**` (Domain: `ReportingDomainModule`, `ReportingErrorCodes`, `BaoCao/{DongBanHang,NgayKinhDoanh,KhoangTongHop,TongHopDoanhThu}`, `BangChieu/DanhMucChieu`, `BangSuKien/{FactSalesLine,FactInventorySnapshot}`, `BangTongHop/AggDailySales`; Contracts: `ReportingApplicationContractsModule`, `ReportingDtos`, `ReportingConsts`, `IReportAppService`; Application: `ReportingApplicationModule`, `ReportingOptions`, `AnhXaNguonBanHang`, `TongHopBaoCaoService`, `ReportAppService`, `ReportingAggregationWorker`; EF: `ReportingEntityFrameworkCoreModule`, `ReportingDbContextModelCreatingExtensions`) · `eCommerce.slnx` · `test/eCommerce.IntegrationTests/{Assets/schema.sql,PostgreSqlSchemaTests.cs,PostgreSqlWp08BaoCaoTests.cs}` · `test/eCommerce.Tests/{Reporting/Wp08BaoCaoTests.cs,Data/RangBuocChuaAnhXa.txt,eCommerce.Tests.csproj}` · `packages/api-client/{openapi.v1.json,src/schema.d.ts}`. **FE:** `lib/{report.ts,menu.ts}` · `components/report/{StackedBarChart.tsx,ExportCsvButton.tsx}` · `app/[business]/{page.tsx,report/revenue/page.tsx,report/inventory/page.tsx}`. **Khác:** `tools/probe-bao-cao-wp08.ps1` · docs (`decisions`, `07`, `08`, `README`, `handover này`) |
