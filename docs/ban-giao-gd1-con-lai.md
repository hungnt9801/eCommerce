# BÀN GIAO — Phần còn lại của GĐ1 (để mở session mới)

> **Lập ngày:** 28/10/2026 · **Cập nhật:** 28/10/2026 (sau `S12` + `S13` + `S14A` + `S15`) · **Cây:** `main` (sạch) · **Người lập:** phiên tự động
>
> 🔑 **Đọc mục 1 trước.** Tài liệu này **không** nói GĐ1 đã xong. Nó chốt **đang ở đâu**, **còn gì**, và
> **làm theo thứ tự nào** để không phải đoán lại.

---

## 1. Ảnh chụp hiện tại (đo được, không phải lời khai)

> 📌 **Số đo này là SAU `S13` + `S14` phần A + `S15`** (28/10/2026). Bảng cũ trước `S12` (unit **1528** · integration **71** · E2E
> **135** · NND **57** · **68** quyền · **89** chỗ quét · **46** migration) đã **trôi** — đừng dùng.

| Hạng mục | Giá trị | Cách đo |
|---|---|---|
| Commit | `git log --oneline -1` | 🔒 **nguồn sự thật là lệnh**, không phải dòng trong tài liệu |
| Cổng đầy đủ | **exit 0 · 347.4s · 15/15 bước** | `pwsh -File tools\kiem-tat-ca.ps1 -DayDu` |
| Unit | **1587** | bước ③ |
| Integration | **87/87** | bước ④ |
| E2E (HTML thật) | **145/145** | bước ⑧ |
| Bất biến DB | **25/25** · **NND 64** · **71 quyền** · **99** chỗ quét | bước ⑥ |
| Migration | **49** | `src/eCommerce.EntityFrameworkCore/Migrations` |
| Oversell | **11/11** (bước ⑬) | `tools/probe-oversell.ps1` |
| Outbox replay | **9/9** (bước ⑭) | `tools/probe-outbox.ps1` |
| Outbox 2 instance | **9/9** (bước ⑮) | `tools/probe-outbox-2instance.ps1` |
| Đơn sàn | **49/49** | `tools/probe-don-san.ps1` |
| Thanh toán (HTTP thật) | **30/30** | `tools/probe-thanh-toan.ps1` |
| Cây làm việc | **0 tệp** (sạch) | `git status --porcelain` |

### Đã xong (đừng làm lại)

| Việc | Bằng chứng |
|---|---|
| P0 #4 oversell 2 luồng | `probe-oversell` 11/11 + map concurrency → `Inventory:0001` |
| SAL-003 — 7 nợ | `sal-003` §16: RowVersion, ETO/outbox, 6 API/quyền, setting, job 24 h, metric, UI 4 trục |
| Outbox/inbox + relay + replay + 2 instance | `XC-002` §17; bước ⑭/⑮ |
| Tách 4 module | `Catalog` · `Inventory` · `Reconciliation` · `Sales` — mỗi module 4 project + mapping EF; migration RỖNG; test kiến trúc |
| Brand (`CatBrands`) | `CAT-004`, migration **45**, NND **56** |
| REC-001 — 3 bảng + API đọc + worker ghi | migration **46**, NND **57**; worker phân loại **9/10** loại lệch |
| UI đọc: `/Brands` (CRUD trừ xoá) · `/Kho` · `/Pricing` | menu + controller + view; E2E xanh |
| **`S13` — `StockAllotment` + sổ cái ngân sách** | `INV-003` **§21**: migration **48** · NND **60** · quyền `Allotment.Manage`/`Allotment.Release` (seed **71**) · giữ chỗ/nhả/chốt **trừ & trả** ngân sách kênh · **+11 unit · +6 integration** · ⚠️ **chưa có UI**, **chưa tự thu hồi** |
| **`S15` — `StalePendingTransaction` + `TriggerRunAsync`** | `REC-001` **§20**: ngưỡng `Reconciliation:StalePendingHours` = **24** (`0` = TẮT) · **10/10** loại lệch · `TriggerRunAsync` **cùng khoá** với job (`IPaymentReconciliationRunner`) · **+17 unit** · probe thanh toán **26 → 30/30** (HTTP + DB + dọn sạch) · **không** migration |
| **`S12` — `CostPrice` (`PrcCostPrices`)** | `PRC-001` **§20**: migration **47** · NND **58** · quyền **`eCommerce.Pricing.CostPrice.View`** (seed **69**) · `/GiaVon` (chỉ đọc) · **15 unit** + **6 integration** + **5 E2E** (`S12-G1…G5`, đo cả **ẩn danh bị chặn** và **HTML công khai không lộ 777.777**) |

---

## 2. Còn lại — bảng công việc (thứ tự làm)

> Mỗi việc **bắt buộc** trong **cùng một thay đổi**: entity + mapping + **migration** + **`schema.sql` regenerate**
> + test (domain + schema) + **tài liệu** + `-DayDu` xanh. Không tách PR.

| # | Việc | Đặc tả (nguồn sự thật) | Phạm vi tối thiểu | Migration dự kiến |
|---|---|---|---|---|
| ~~**S12**~~ | ✅ **XONG 28/10/2026** — `CostPrice` (`PrcCostPrices`) | [`PRC-001` §20](features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md) | migration **47** · NND **58** · quyền `eCommerce.Pricing.CostPrice.View` · `/GiaVon` (chỉ đọc) · **6 integration + 15 unit + 5 E2E**. ⚠️ **Chưa** có đường ghi (**`Q2`** chưa chốt) ⇒ đừng mở "tạm" |
| ~~**S13**~~ | ✅ **XONG 28/10/2026** — `StockAllotment` + `StockAllotmentLedger` | [`INV-003` §21](features/inventory/inv-003-phan-bo-ton-theo-kenh.md) | migration **48** · NND **60** · **2 quyền** (seed **71**) · giữ chỗ **trừ ngân sách** kênh (tuỳ chọn theo dòng) · **+11 unit + 6 integration** · ⚠️ **chưa** có UI + **chưa** tự thu hồi khi tồn giảm (§21.5) |
| 🟡 **S14** | `OrderReturn` + `OrderReturnLine` | [`SAL-006` §28–§29](features/sales/sal-006-tra-hang-rma-hoan-tien.md) | ✅ **phần A:** 3 bảng + `OrderLine.ReturnedQuantity` + 8 CHECK + 4 unique NND (NND **64**), migration **49** · ✅ **phần B:** service + API + idempotency + giữ chỗ trả + nhật ký · ✅ **phần C:** gateway `ICustomerReturnGateway` + chứng từ `CustomerReturn` + ghi sổ, chỉ dòng `Restock` · `Q4` = **chỉ ghi nhận** · ✅ **phần D:** đã **xoá cạnh tắt `Open→Returned`** + migrate `TraHangAsync` (§31) · ❌ còn: ETO, UI RMA, probe HTTP, `PartiallyReturned` | **đã dùng 49** |
| ~~**S15**~~ | ✅ **XONG 28/10/2026** — `StalePendingTransaction` + `TriggerRunAsync` | [`REC-001` §20](features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md) | ngưỡng **24 h** (cấu hình, `0` = TẮT) · `TriggerRunAsync` đi qua **cùng khoá** với job · **+17 unit** · **probe 30/30** (HTTP thật) | **không** (ngưỡng là cấu hình) |
| **S16** | 3 metric còn nợ | [`XC-005` §5.1/§19.3](features/cross-cutting/XC-005-observability-log-metric.md) | `messaging_dlq_replayed_total` (cần luồng replay thật), `lock_ttl_exceeded_total` (cần wrapper TTL), `logging_failures_total` (logger provider) | không (trừ khi thêm wrapper) |
| **S17** | Hợp đồng cho dịch vụ còn ở host | `ke-hoach-tach-module` L2 | `ChannelDirectory`, `HienThiKenhAppService`, `ChinhSachHanMucAppService`, `WarehouseAppService`, `MarketplaceReconciliationWorker` | không |
| 🟡 **S18** | UI còn thiếu + tài liệu | `WEB-001`, `§A.5` | ✅ **`/NganSach`** (S13) + nút “Chạy đối soát ngay” (S15) + `S18-N1…N5` (**E2E 145**) · ❌ còn: UI RMA (S14), form Pricing/Kho quản trị, **duyệt 7 `XC-*` + 19 tài liệu** (`Q5` — cần chủ dự án) | không |
| **S19** | VNPay sandbox thật | `PAY-001` | Điền `VnPay:TmnCode/HashSecret`, chạy 1 giao dịch thật | không |

### Việc **không** nên làm lại (đã chốt)

- `CheckoutSession`/`Cart`: **D7** đã chốt dùng giỏ cookie 2 h + `Order.CheckoutIdempotencyKey`; **chỉ cần sửa plan**
  cho khớp, **không** tạo bảng.
- `OrderStatusHistory`: **đã có** — đừng tạo lại.
- Outbox/inbox: **đã có** — đừng tự viết bảng `MsgOutbox`.

---

## 3. Quyết định cần **chủ dự án** chốt trước khi code

| # | Câu hỏi | Ảnh hưởng |
|---|---|---|
| Q1 | `StalePendingHours` = ? | ✅ **đã chốt 24 h** (28/10/2026, `REC-001` §20.2) — đổi được bằng `appsettings.json` |
| Q2 | `CostPrice` lấy từ đâu (nhập tay / import NCC / giá vốn bình quân)? | S12 — quyết định API và job |
| Q3 | `StockAllotment`: phân bổ theo **kênh** hay **kênh + shop**? | ✅ **đã chốt CHỈ theo kênh** (28/10/2026, `INV-003` §21.2) |
| Q4 | Trả hàng có **hoàn tiền tự động** hay chỉ ghi nhận? | S14 — ảnh hưởng `PaymentHub` |
| Q5 | Duyệt tài liệu: duyệt thật hay **thoả thuận song song bằng văn bản** (`§A.5`)? | Cổng 0 |
| Q6 | VNPay sandbox: cấp credential ở đâu, ai giữ? | S19 |

---

## 4. Quy tắc bắt buộc (đã trả giá — đừng bỏ)

1. **Migration + `schema.sql` + test + tài liệu trong CÙNG thay đổi**:
   `dotnet ef migrations add <Ten> --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.DbMigrator`
   → `dotnet ef migrations script ... --output test/eCommerce.IntegrationTests/Assets/schema.sql`
   → chạy `dotnet run --no-launch-profile` **trong** `src/eCommerce.DbMigrator` (chạy ở root sẽ lỗi connection string).
2. **Mọi unique index trên entity `IMultiTenant` phải `NULLS NOT DISTINCT`.** EF không sinh được ⇒ thêm
   `DropIndex` + `Sql(... NULLS NOT DISTINCT)` trong migration. **Cập nhật `PostgreSqlSchemaTests`** (số NND đọc
   từ test — `kiem-db.ps1` lấy chính con số đó).
3. **Mã lỗi mới** phải vào sổ đăng ký (`eCommerceConsts.ErrorCodes.*` / `ReconErrorCodes`) và **ánh xạ**
   `ViPhamRangBuocDb` nếu là ràng buộc DB có nghĩa nghiệp vụ; nếu không test `RangBuocDbChuaAnhXa` sẽ đỏ.
4. **Quyền mới** phải khai **cả hai nơi** (hằng số + `eCommercePermissionDefinitionProvider`) và seed lại
   `DbMigrator`; cập nhật số quyền trong test/probe.
5. **Commit message ASCII** (hook chặn dấu). Dùng `-F <tệp>` với message không dấu.
6. **Cổng 15 bước**: thêm/bớt bước ⇒ đổi `ci-khang-dinh.ps1` (`$SoBuocToiThieu`), `CiWorkflowTests`,
   `XC-007`, tên job CI `cong-15-buoc`, `SESSION-KICKOFF`.
7. **Probe**: event bất đồng bộ (outbox ≤ 2 s) ⇒ phải **poll đúng outcome**, không đếm "có dòng".
   Không đếm `fail:` trong log khởi động (self-check ETO cố ý log `fail:`).
8. **Gate hay OOM MSBuild**: dọn `dotnet build-server shutdown`, `Get-Process dotnet | Stop-Process`, đặt
   `MSBUILDDISABLENODEREUSE=1`. Nếu `bin/` rỗng ⇒ build lại trước khi gate (bước ② cần DLL).
9. **Không tuyên bố "GĐ1 xong"** khi cổng 0 (tài liệu Approved) và VNPay thật còn đỏ.

---

## 5. Cách bắt đầu session mới (5 phút)

```powershell
cd D:\Projects\Tools\eCommerce
git log --oneline -1                 # 🔒 đọc hash THẬT ở đây; đừng tin một hash chép trong tài liệu
git status --porcelain               # phải rỗng
pwsh -File tools\kiem-tat-ca.ps1 -DayDu   # phải exit 0, 15/15
```

Sau đó đọc theo thứ tự:
1. **tài liệu này** (mục 2 → việc kế tiếp là **S14**);
2. đặc tả của việc đó (`docs/features/...` — cột 3 bảng §2);
3. [`docs/SESSION-KICKOFF.md`](SESSION-KICKOFF.md) — quy ước/kiến trúc;
4. [`docs/ke-hoach-tach-module.md`](ke-hoach-tach-module.md) — L2 nếu đụng module;
5. [`docs/audit-gd1.md`](audit-gd1.md) §0.1 + §12 — lịch sử đã đóng.

---

## 6. Checklist "GĐ1 hoàn tất" (5 cổng plan §7.1)

| # | Cổng | Hiện tại | Cần để XANH |
|---|---|---|---|
| 0 | Tài liệu GĐ1 + `XC-001…007` Approved | ❌ 1/55 | Q5 — duyệt thật hoặc thoả thuận văn bản |
| 1 | Đặt đơn + thanh toán **1 cổng VN thật** | 🟡 FakePaymentProvider | S19 — credential + 1 giao dịch sandbox |
| 2 | Không bán vượt tồn (2 luồng + **2 instance**) | 🟡 2 luồng ✅; **1 chốt ngân sách** ✅; **2 instance kho** ❌ | probe 2 Host cho **giữ chỗ** (không phải outbox) |
| 3 | Không mất tiền — job đối soát phát hiện lệch | 🟢 có Run/Line/lệch **10/10** + chạy tay | tiền thật (S19) + cổng thật để đo `Stale` (`Q6`) |
| 4 | Mọi API cần xác thực + login | ✅ | — |
| 5 | Không mất event — outbox relay | ✅ replay + 2 instance | — |

> 📌 Cổng 2 còn **một** phần chưa đo: **2 instance cho GIỮ CHỖ** (khoá phân tán + Redis), khác với 2 instance
> **outbox** đã đo ở bước ⑮. Đừng đánh đồng hai việc.

---

## 7. Bản đồ tệp dễ sửa nhầm

| Vùng | Tệp | Lưu ý |
|---|---|---|
| EF mapping module | `modules/*/eCommerce.*.EntityFrameworkCore/*Extensions.cs` | Host `eCommerceDbContext.ConfigureCommerce` **chỉ gọi** `ConfigureX`; **cấm** chép block về host (test kiến trúc đỏ) |
| NND | migration mới + `PostgreSqlSchemaTests` | Số NND trong test là **nguồn sự thật** cho `kiem-db.ps1` |
| Mã lỗi | `src/eCommerce.Domain.Shared/eCommerceConsts.cs`, `ReconErrorCodes.cs` | `CatalogErrorCodeRegistryTests` chặn trùng/định dạng |
| Mapping lỗi DB | `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` | Thiếu ⇒ test `RangBuocDbChuaAnhXa` đỏ |
| Menu/UI | `src/eCommerce.Web/Navigation/MenuDieuHuong.cs` | `QuyenCanCo` **phải** khớp `[Authorize]` app service; URL phải có controller thật |
| Quyền | `eCommercePermissionDefinitionProvider` | Thiếu ⇒ **403 cho mọi người** kể cả admin |
| Probe | `tools/probe-*.ps1` + `tools/kiem-tat-ca.ps1` | Bước ⑬/⑭/⑮ dùng chung Host; dọn cổng 5000/5001/5100 |

---

## 8. Ước lượng

| Việc | Ước lượng | Ghi chú |
|---|---|---|
| ~~S12 CostPrice~~ | ✅ **xong 28/10/2026** | `PRC-001` §20; phần **đường ghi** vẫn treo chờ **`Q2`** |
| ~~S13 StockAllotment + ledger~~ | ✅ **xong 28/10/2026** | `INV-003` §21 (còn: UI + thu hồi tự động) |
| S14 OrderReturn/RMA | 6–10 pd | Chạm Sales + Inventory + PaymentHub |
| ~~S15 StalePending + TriggerRun~~ | ✅ **xong 28/10/2026** | `REC-001` §20 (ngưỡng 24 h + chạy tay qua cùng khoá) |
| S16 3 metric | 3–5 pd | Cần hạ tầng mới |
| S17 hợp đồng host | 4–6 pd | Chạm 3 module |
| S18 UI + tài liệu | 3–6 pd | Phụ thuộc Q5 |
| S19 VNPay thật | 2–3 pd | Phụ thuộc Q6 |

**Tổng còn lại ≈ 20–33 pd** — không phải một phiên. Làm **theo S14 → S19**, mỗi phiên **một** việc + gate.

















