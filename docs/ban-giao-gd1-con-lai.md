# BÀN GIAO — Phần còn lại của GĐ1 (để mở session mới)

> **Lập ngày:** 28/10/2026 · **Cập nhật:** 29/10/2026 (sau `S12` + `S13` + `S14` A–H + `S15` + `S17` + `S16` + `S16b`) · **Cây:** `main` (sạch) · **Người lập:** phiên tự động
>
> 🔑 **Đọc mục 1 trước.** Tài liệu này **không** nói GĐ1 đã xong. Nó chốt **đang ở đâu**, **còn gì**, và
> **làm theo thứ tự nào** để không phải đoán lại.

---

## 1. Ảnh chụp hiện tại (đo được, không phải lời khai)

> 📌 **Số đo này là SAU `S13` + `S14` phần A–H + `S15` + `S17` + `S16` + `S16b`** (29/10/2026). Bảng cũ trước `S12` (unit **1528** ·
> integration **71** · E2E **135** · NND **57** · **68** quyền · **89** chỗ quét · **46** migration) đã **trôi** —
> đừng dùng.

| Hạng mục | Giá trị | Cách đo |
|---|---|---|
| Commit | `git log --oneline -1` | 🔒 **nguồn sự thật là lệnh**, không phải dòng trong tài liệu |
| Cổng đầy đủ | **exit 0 · 389.7s · 15/15 bước** | `pwsh -File tools\kiem-tat-ca.ps1 -DayDu` |
| Unit | **1634** | bước ③ |
| Integration | **90/90** | bước ④ |
| E2E (HTML thật) | **167/167** | bước ⑧ |
| Bất biến DB | **25/25** · **NND 64** · **72 quyền** · **99** chỗ quét | bước ⑥ |
| Migration | **50** | `src/eCommerce.EntityFrameworkCore/Migrations` |
| Oversell | **11/11** (bước ⑬) | `tools/probe-oversell.ps1` |
| Outbox replay | **9/9** (bước ⑭) | `tools/probe-outbox.ps1` |
| Outbox 2 instance | **9/9** (bước ⑮) | `tools/probe-outbox-2instance.ps1` |
| 🆕 **Replay DLQ (HTTP + DB)** | **21/21** (⚠️ **chạy tay**, không phải bước cổng) | `tools/probe-dlq-replay.ps1` |
| Đơn sàn | **49/49** | `tools/probe-don-san.ps1` |
| Thanh toán (HTTP thật) | **30/30** | `tools/probe-thanh-toan.ps1` |
| Cây làm việc | **0 tệp của phiên này** (⚠️ vẫn còn tệp `?? tools/…` do **tiến trình KHÁC** tạo — không thuộc commit nào của phiên này) | `git status --porcelain` |

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
| **`S16` — 2/3 metric còn nợ** | `XC-005` **§20**: `lock_ttl_exceeded_total` (decorator `IAbpDistributedLock`, nghe `HandleLostToken`) + `logging_failures_total` (bọc `ILoggerProvider`, stderr + counter) · **+7 unit** · `MessagingDlqReplayedTotal` **vẫn nợ** vì chưa có luồng replay thật · **0** migration/schema/quyền/route |
| 🆕 **`S16b` — replay DLQ THẬT** | `XC-005` **§21** + `XC-002` **§18**: *"replay"* = **publish lại event gốc** lên `IDistributedEventBus` (`useOutbox: true`, **cùng UoW** với dấu) · migration **50** (`ReplayedAtUtc`/`ReplayCount`/`ReplayedBy`) · idempotent **một dòng một lần** · fail-closed `0009`/`0010` ⇒ **409** · quyền **`eCommerce.Operations.DeadLetters.Replay`** (seed **72**) · metric `messaging_dlq_replayed_total` **+1 sau commit** · **+11 unit · +3 integration** · probe HTTP **21/21** (đo `AbpEventInbox` **tăng**) |
| **`S12` — `CostPrice` (`PrcCostPrices`)** | `PRC-001` **§20**: migration **47** · NND **58** · quyền **`eCommerce.Pricing.CostPrice.View`** (seed **69**) · `/GiaVon` (chỉ đọc) · **15 unit** + **6 integration** + **5 E2E** (`S12-G1…G5`, đo cả **ẩn danh bị chặn** và **HTML công khai không lộ 777.777**) |
| **`S14` phần E — UI RMA + đo HTTP** | `SAL-006` **§32**: `/TraHang` (danh sách + duyệt/từ chối/nhận/đóng/hoàn tiền, ẩn nút theo quyền) · link từ trang đơn (`Url.Action`) · menu nhóm Vận hành · neo `[TRAHANG-MAN-HINH]` · **+14 E2E** đo **DB thật** (`Status = 1`, `Version = 1`, đơn `ReturnStatus = 2`/`ClosureVersion = 2`; duyệt lần hai ⇒ `Sales:0010` + DB **không đổi**) · **ETO: KHÔNG thêm** (không có consumer) · **0** migration/quyền |
| **`S14F` — sửa 2 lỗi nhận hàng** | `SAL-006` **§33**: (1) `NhanHang` nhận cờ `daNhanDuTatCaDong` do **service tính trên toàn bộ dòng** — `Status`/`ReceivedAtUtc` chỉ đổi khi đủ ⇒ đơn chỉ sang `Returned` khi phiếu `Received`; (2) `GhiNhanHang` **NÉM `Sales:0010`** khi `0` + disposition (**trước** DB, không còn `500`), `0` + `Unset` là **no-op** · **+4 unit** (hai chiều) · **0** schema/migration/NND/quyền, `CHECK` **không** nới · **§32.4 đã đóng** |
| 🆕 **`S14G` — nút HUỶ phiếu trên UI** | `SAL-006` **§34** (đóng nợ §32.5 #3): `POST /TraHang/Huy` (token chống giả mạo) + nút **Huỷ phiếu** chỉ ở `Requested\|Approved` **và chưa nhận hàng**, kèm **ô lý do bắt buộc**; ẩn/hiện theo `Orders.ReturnRequest`, **app service cưỡng chế** · **+6 E2E** (`S14-E15…E20`) đo **hai chiều**: huỷ ⇒ `OrdReturns.Status = 3` **và** `ReturnedQuantity 2 → 0` (đối xứng `GiamTra`); huỷ lần hai ⇒ `eCommerce:Sales:0010` + DB **không đổi**; phiếu đã nhận hàng **không** có nút · **0** migration/NND/quyền |
| 🆕 **`S14H` — trạng thái `PartiallyReturned`** | `SAL-006` **§35** (đóng nợ §32.5 #1, **BA chốt 29/10/2026**): `ReturnStatus.PartiallyReturned = 6` **append ở cuối dải** (số `0…5` **không** đổi — cột DB lưu `int`) · **đúng 2 cạnh** `ReturnApproved → PartiallyReturned → Returned` (không cạnh tắt, không cạnh tự thân, **không** hấp thụ) · `NhanHangAsync` chuyển đơn **chỉ khi trạng thái thực sự đổi** · **không** thêm `OrderStatus` (đơn trả một phần **vẫn `Completed`** — “đã trả một phần” đọc ở `ReturnStatus`) · **+7 unit** hai chiều · **+2 E2E** (`S14-E21/E22`) + **đổi kỳ vọng `S14-E12`** đo **DB** · **0** migration/NND/quyền/route/DTO/ETO |

---

## 2. Còn lại — bảng công việc (thứ tự làm)

> Mỗi việc **bắt buộc** trong **cùng một thay đổi**: entity + mapping + **migration** + **`schema.sql` regenerate**
> + test (domain + schema) + **tài liệu** + `-DayDu` xanh. Không tách PR.

| # | Việc | Đặc tả (nguồn sự thật) | Phạm vi tối thiểu | Migration dự kiến |
|---|---|---|---|---|
| ~~**S12**~~ | ✅ **XONG 28/10/2026** — `CostPrice` (`PrcCostPrices`) | [`PRC-001` §20](features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md) | migration **47** · NND **58** · quyền `eCommerce.Pricing.CostPrice.View` · `/GiaVon` (chỉ đọc) · **6 integration + 15 unit + 5 E2E**. ⚠️ **Chưa** có đường ghi (**`Q2`** chưa chốt) ⇒ đừng mở "tạm" |
| ~~**S13**~~ | ✅ **XONG 28/10/2026** — `StockAllotment` + `StockAllotmentLedger` | [`INV-003` §21](features/inventory/inv-003-phan-bo-ton-theo-kenh.md) | migration **48** · NND **60** · **2 quyền** (seed **71**) · giữ chỗ **trừ ngân sách** kênh (tuỳ chọn theo dòng) · **+11 unit + 6 integration** · ⚠️ **chưa** có UI + **chưa** tự thu hồi khi tồn giảm (§21.5) |
| 🟡 **S14** | `OrderReturn` + `OrderReturnLine` | [`SAL-006` §28–§35](features/sales/sal-006-tra-hang-rma-hoan-tien.md) | ✅ **phần A:** 3 bảng + `OrderLine.ReturnedQuantity` + 8 CHECK + 4 unique NND (NND **64**), migration **49** · ✅ **phần B:** service + API + idempotency + giữ chỗ trả + nhật ký · ✅ **phần C:** gateway `ICustomerReturnGateway` + chứng từ `CustomerReturn` + ghi sổ, chỉ dòng `Restock` · `Q4` = **chỉ ghi nhận** · ✅ **phần D:** xoá cạnh tắt `Open→Returned` + migrate `TraHangAsync` (§31) · ✅ **phần E:** **UI `/TraHang` + 14 ca E2E đo HTTP/DB** (§32); **ETO = KHÔNG** (không có consumer) · ✅ **`S14F`:** sửa **2 lỗi nhận hàng** (§33), §32.4 **đã đóng** · ✅ **`S14G`:** **nút HUỶ phiếu trên UI** (§34) + **6 ca E2E** đo DB · ✅ **`S14H`:** **`PartiallyReturned`** cho trục kết thúc của đơn (§35, BA chốt 29/10/2026) + **7 unit** + **2 E2E** · ❌ còn: ETO (cố ý), `Q4` hoàn tiền tự động | **đã dùng 49** |
| ~~**S15**~~ | ✅ **XONG 28/10/2026** — `StalePendingTransaction` + `TriggerRunAsync` | [`REC-001` §20](features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md) | ngưỡng **24 h** (cấu hình, `0` = TẮT) · `TriggerRunAsync` đi qua **cùng khoá** với job · **+17 unit** · **probe 30/30** (HTTP thật) | **không** (ngưỡng là cấu hình) |
| 🟡 **S16** | **2/3 metric còn nợ** | [`XC-005` §5.1/§20](features/cross-cutting/XC-005-observability-log-metric.md) | ✅ `lock_ttl_exceeded_total` (decorator `IAbpDistributedLock` + `HandleLostToken`) · ✅ `logging_failures_total` (bọc `ILoggerProvider`) · ✅ **`messaging_dlq_replayed_total` ĐÃ ĐÓNG ở `S16b`** (replay thật, xem **§21**) | **1** (`S16b`: 3 cột + quyền mới) |
| ~~**S17**~~ | ✅ **XONG 29/10/2026** — hợp đồng cho dịch vụ còn ở host | [`ke-hoach-tach-module` §3.1](ke-hoach-tach-module.md) | `IChannelDirectory`/`IHienThiKenhAppService` (Catalog) + `IChinhSachHanMucAppService`/`IWarehouseAppService` (Inventory) đã ở module Contracts; `WarehouseAppService` **chuyển vào module**, 3 hiện thực còn host + worker được ghim `[ExposeServices]`/lý do · **18 ca** `HopDongHostTests` · **+18 unit** | không |
| 🟡 **S18** | UI còn thiếu + tài liệu | `WEB-001`, `§A.5` | ✅ **`/NganSach`** (S13) + nút “Chạy đối soát ngay” (S15) + `S18-N1…N5` (**E2E 145**) · ✅ **UI RMA `/TraHang`** đã xong ở `S14` phần E (`SAL-006` §32, **+14 E2E**) · ❌ còn: form Pricing/Kho quản trị, **duyệt 7 `XC-*` + 19 tài liệu** (`Q5` — cần chủ dự án) | không |
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
| ~~S17 hợp đồng host~~ | ✅ **xong 29/10/2026** | Chạm 3 module (Catalog/Inventory/Sales); **0 migration** |
| S18 UI + tài liệu | 3–6 pd | Phụ thuộc Q5 |
| S19 VNPay thật | 2–3 pd | Phụ thuộc Q6 |

**Tổng còn lại ≈ 20–33 pd** — không phải một phiên. Làm **theo S14 → S19**, mỗi phiên **một** việc + gate.


















---

## 9. NHẬT KÝ PHIÊN 28/10/2026 (đóng được gì — để phiên sau không đoán lại)

| Hạng mục | Kết quả | Commit |
|---|---|---|
| `S12` — `CostPrice` (`PrcCostPrices`) | migration **47** · NND **58** · quyền `eCommerce.Pricing.CostPrice.View` (seed **69**) · `/GiaVon` (chỉ đọc) · 15 unit + 6 integration + 5 E2E | `acb6516` |
| `S15` — `StalePendingTransaction` + `TriggerRunAsync` | **`Q1` = 24 h** (cấu hình, `0` = TẮT) · 10/10 loại lệch · chạy tay **cùng khoá** với job (`IPaymentReconciliationRunner`) · +17 unit · **probe thanh toán 26 → 30/30** | `95c04f8` |
| `S13` — `StockAllotment` + sổ cái | **`Q3` = chỉ theo kênh** · migration **48** · NND **60** · 2 quyền · giữ chỗ **trừ/trả** ngân sách (tuỳ chọn theo dòng) · +11 unit + 6 integration | `cd6de05` |
| `S14` A — lược đồ RMA | 3 bảng + `OrderLine.ReturnedQuantity` + 8 CHECK + 4 unique NND (NND **64**) · migration **49** · **`Q4` = chỉ ghi nhận hoàn tiền** · +14 unit + 4 integration | `9aa1293` |
| `S14` B — service + API RMA | tạo/duyệt/từ chối/huỷ/nhận/đóng/hoàn tiền thủ công · idempotency + `PayloadHash` · giữ chỗ trả đối xứng · máy trạng thái đơn là nguồn sự thật · +1 unit | `c9e2155` |
| `S14` C — nhập lại kho | `ICustomerReturnGateway` + chứng từ `CustomerReturn` + ghi sổ ngay, **chỉ** dòng `Restock` · +1 unit | `73bd907` |
| `S14` D — xoá cạnh tắt | `Open → Returned` **đã xoá**; `TraHangAsync` đi đủ ba cạnh; test hai chiều | `2065c21` |
| `S18` — UI | `/NganSach` (danh sách + ATP + cấp/sửa + đóng băng/mở/nhả, **chọn kho từ danh mục thật**) · nút “Chạy đối soát ngay” · **E2E 140 → 145** | `e3e7b77` · `2005cb4` |
| `S14` E — UI RMA + đo HTTP | `/TraHang` (duyệt/từ chối/nhận/đóng/hoàn tiền) · link từ trang đơn · menu Vận hành · **E2E 145 → 159** · DB `Status = 1`/`Version = 1` + đơn `2/2` · **ETO: không** | xem `git log` |
| `S16` — 2/3 metric còn nợ | `lock_ttl_exceeded_total` + `logging_failures_total` có điểm phát thật; **+7 unit**; `messaging_dlq_replayed_total` vẫn nợ (chưa có replay) | `S16` — xem `git log` |
| `S14F` — sửa 2 lỗi nhận hàng | `GoodsStatus` tính trên **toàn bộ** dòng (cờ `daNhanDuTatCaDong`) ⇒ không "nhận đủ" sai khi nhận lẻ · `0` + disposition ⇒ **`Sales:0010`** trước DB (hết `500`), `0` + `Unset` = no-op · **+4 unit** hai chiều · **0** schema/migration/NND/quyền · **§32.4 đã đóng** | xem `git log` |
| 🆕 `S16b` — replay DLQ thật | *"replay"* = **publish lại event gốc** (không phải đánh dấu) · `PhatLaiAsync` + `PhatLaiDlqKetQuaDto` · migration **50** (3 cột `Replay*`) · idempotent **một dòng một lần** (lần hai ⇒ `200` + `DaPhatLai=false` + lý do) · fail-closed `0009`/`0010` ⇒ **409** · quyền **`eCommerce.Operations.DeadLetters.Replay`** (seed **72**) · metric **+1 chỉ sau commit** · **+11 unit · +3 integration** · probe HTTP **21/21** (đo `AbpEventInbox` **tăng**) | xem `git log` |
| 🆕 `S14G` — nút huỷ phiếu trả | `POST /TraHang/Huy` + nút **Huỷ phiếu** (ô lý do **bắt buộc**) chỉ hiện ở `Requested\|Approved` **và chưa nhận hàng** · **+6 E2E** (`S14-E15…E20`): huỷ ⇒ `OrdReturns.Status = 3` **và** `ReturnedQuantity 2 → 0` (đối xứng `GiamTra`); huỷ lần hai ⇒ `Sales:0010` + DB **không đổi**; phiếu đã nhận hàng **không** có nút · **0** schema/migration/NND/quyền · **§32.5 #3 đã đóng** | xem `git log` |
| 🆕 `S14H` — trạng thái `PartiallyReturned` | `ReturnStatus.PartiallyReturned = 6` **append ở cuối dải** (`0…5` **không** đổi số) · **đúng 2 cạnh** `ReturnApproved → PartiallyReturned → Returned` (không cạnh tắt/tự thân; **không** hấp thụ) · `NhanHangAsync` chỉ chuyển khi trạng thái đơn **thực sự đổi** · **không** `OrderStatus` mới (đơn vẫn `Completed`) · **+7 unit** hai chiều · E2E: **đổi `S14-E12`** (`6\|3\|4`) + **`S14-E21/E22`** (chuỗi trên UI · nhận nốt ⇒ `4\|4\|6`) · **0** schema/migration/NND/quyền · **§32.5 #1 đã đóng** | xem `git log` |

**Đo cuối phiên (cập nhật 29/10/2026 sau `S14H`):** `-DayDu` **exit 0 · 389.7s · 15/15** · unit **1634** · integration **90** · **E2E 167/167** · DB **25/25** · NND **64** · **72** quyền · migration **50** · quét **99** chỗ · cây **0 tệp của phiên này** (⚠️ còn `?? tools/…` của tiến trình khác) · DB **0 rác probe**.

**Còn lại (chưa làm — không được coi là xong):**

1. ~~`S16` — **còn đúng 1 metric nợ:** `messaging_dlq_replayed_total`~~ ✅ **XONG 29/10/2026 (`S16b`, xem §11.5 và
   `XC-005` §21):** đã có **luồng phát lại thật** (đọc `MsgDeadLetters` → phát lại event qua outbox → đánh dấu
   `ReplayedAtUtc` → idempotent) ⇒ **16/16 metric có điểm phát**. ⏳ **Nợ riêng của `S16b` (không thuộc metric):**
   **không** có phát lại **tự động**, **không** có phát lại **theo lô**, **không** có **UI** DLQ.
2. ~~`S17` — hợp đồng cho 5 dịch vụ còn ở host~~ ✅ **XONG 29/10/2026** (xem **§10**): `WarehouseAppService`
   về module; 3 hiện thực còn host + worker được ghim hợp đồng/DI/lý do; **+18 unit**. ⚠️ Còn **nợ riêng**
   (không thuộc S17): hợp đồng đọc ChannelHub/Reconciliation để chuyển nốt 3 lớp; Sales/Reconciliation vẫn
   đọc Domain module khác (`audit-gd1.md` §0.1 **b**).
3. `S14` phần E — ✅ **XONG 29/10/2026** (xem **§32** của `SAL-006`): **UI `/TraHang`** + lối vào + **14 ca E2E
   HTTP/DB**. ✅ **Hai phát hiện `§32.4` ĐÃ SỬA 29/10/2026** ở `S14F` (`SAL-006` **§33**): `GoodsStatus` tính
   trên **toàn bộ** dòng + guard `Sales:0010` cho `0` + disposition. ✅ **Nút HUỶ phiếu ĐÃ CÓ 29/10/2026** ở
   `S14G` (`SAL-006` **§34**, +6 E2E hai chiều) ⇒ **§32.5 #3 đã đóng**. ✅ **`PartiallyReturned` ĐÃ CÓ
   29/10/2026** ở `S14H` (`SAL-006` **§35**, BA chốt, +7 unit + 2 E2E) ⇒ **§32.5 #1 đã đóng**. ⏳ **Còn nợ
   trong `S14`:** **ETO: cố ý không thêm** vì chưa có consumer (`§32.3`) · hoàn tiền **tự động** vẫn là `Q4`
   = *chỉ ghi nhận* (`§32.5 #4`).
4. `S18` phần tài liệu — duyệt 7 `XC-*` + 19 tài liệu GĐ1: ⚠️ **`Q5` là quyết định của chủ dự án**.
5. `S19` — VNPay sandbox thật: ⛔ **cần `VnPay:TmnCode` + `HashSecret`** (`Q6`). Không thể tự tạo credential;
   **cấm** bịa “giao dịch thật”.

📌 **Đọc `S13`/`S14` là hai hạng mục đã có đường chạy đầy đủ** (API + UI cho S13; API + chứng từ kho cho S14);
`S16`/`S17` là **hạ tầng**, không phải màn hình — chúng **không** làm thay đổi hành vi người dùng cuối.

---

## 10. `S17` — HỢP ĐỒNG CHO DỊCH VỤ CÒN Ở HOST (29/10/2026)

> **Mục tiêu (theo bàn giao §2):** 5 dịch vụ còn ở host phải có **hợp đồng** ở `Application.Contracts` của module
> sở hữu, để module khác/host **chỉ** phụ thuộc hợp đồng — **không** phụ thuộc lớp hiện thực ở host.
> 🔒 **Ràng buộc:** không đổi route/DTO/quyền/hành vi; **0 migration**.

### 10.1 Đo được TRƯỚC khi sửa — tiền đề của S17 đã đúng một phần

`grep` + `typeof(...).Assembly` (không tin csproj): **4/5 hợp đồng đã nằm trong module Contracts từ `e2c2785`
(24/10/2026)** — `IChannelDirectory`, `IHienThiKenhAppService` (Catalog Contracts);
`IChinhSachHanMucAppService`, `IWarehouseAppService` (Inventory Contracts). ⇒ `S17` **không** "chuyển hợp đồng
lần nữa"; nó làm **phần còn thiếu**: hiện thực + DI tường minh + cổng chặn + tài liệu.

### 10.2 Đã làm gì (4 việc, đúng phạm vi)

| # | Việc | Bằng chứng |
|---|---|---|
| 1 | **`WarehouseAppService` → `modules/Inventory/eCommerce.Inventory.Application`** (tách khỏi tệp cũ; `InventoryReconciliationAppService` ở lại host vì đọc `ReconRun`/`InventoryReconciliationService`) | `git status` chỉ R/A trong phạm vi; build 0 warning; route `/api/app/warehouse*` **4 đường dẫn KHÔNG đổi** (`tools/route-check.ps1 -Match warehouse`) |
| 2 | **4 hiện thực còn host** (`ChannelDirectory`, `HienThiKenhAppService`, `ChinhSachHanMucAppService`; worker riêng) — **ghim DI tường minh** bằng `[ExposeServices(typeof(I…))]` | 4 lớp có thuộc tính; test H5 đỏ nếu tháo; đúng khuôn `XuLyKetQuaCongService` (`S5`, lỗi thật `HTTP 500`) |
| 3 | **Cổng chặn tái phát** `test/eCommerce.Tests/CrossCutting/HopDongHostTests.cs` — **18 ca** khoá 5 bất biến (xem `ke-hoach-tach-module` §3.1.1): hợp đồng đúng assembly module · hiện thực đúng assembly đã chốt · module **không** tham chiếu lớp hiện thực host (bỏ chú thích trước khi quét) · module **không** tham chiếu `src/eCommerce.Application` · `[ExposeServices]` có mặt | `dotnet test` **1605/1605** (unit **1587 → 1605**) |
| 4 | **Tài liệu cùng commit**: `ke-hoach-tach-module` **§3.1** (bảng 5 dịch vụ + phần còn lại của L2) · `SESSION-KICKOFF` §3 · mục này | `check:mermaid` 0 lỗi · `kiem-link-tai-lieu` 0 chết |

**Không** làm (cố ý): không mở hợp đồng đọc mới cho ChannelHub/Reconciliation; không đổi namespace legacy
`eCommerce.Application.Contracts.Inventory`; không đổi route/DTO/quyền; không thêm migration.

### 10.3 Vì sao 3 hiện thực + worker **còn ở host** (lý do đo được, không phải "chưa làm")

| Dịch vụ | Lý do còn host |
|---|---|
| `ChannelDirectory` | đọc entity `Channel` của `ChannelHub.Domain` — module **cấm** tham chiếu Domain module khác (L2) |
| `HienThiKenhAppService` | đọc `ChannelListing` của `ChannelHub.Domain` (guard `0209`); cần hợp đồng đọc của ChannelHub trước khi chuyển |
| `ChinhSachHanMucAppService` | đọc `Channel` **và** `ReconRun` — điểm nối 3 module (Inventory/ChannelHub/Reconciliation) |
| `MarketplaceReconciliationWorker` | **không module nào tham chiếu trực tiếp** (H3 khoá); là `AsyncPeriodicBackgroundWorkerBase` do host đăng ký. ⚠️ Thêm interface rỗng chỉ để "có hợp đồng" là **hợp đồng giả** — không ai resolve ⇒ không kiểm được gì |

📌 **Vì sao dùng `[ExposeServices]` chứ không `services.AddTransient`:** descriptor thủ công là **đăng ký thứ hai**
và có nguy cơ bỏ qua interceptor (`[Authorize]`/UoW) của ABP; `[ExposeServices]` đi qua **đúng** đường đăng ký
của ABP. 📌 Hệ quả đã kiểm: E2E `/HanMucKenh` + `/NganSach` (resolve `IChinhSachHanMucAppService`) và
`DatHangController` (resolve `IWarehouseAppService`) vẫn xanh ⇒ DI **thật sự** resolve sau khi đổi assembly.

### 10.4 Đo cổng cuối (cây đã sửa)

- `-DayDu`: **exit 0 · 396.3s · 15/15** · unit **1605** · integration **87/87** · E2E **145/145** ·
  DB **25/25** · NND **64** · **71** quyền · migration **49** · quét **99** chỗ.
- **Không** đổi lược đồ/NND/quyền/route ⇒ các số 64/71/49/99 **giữ nguyên** là đúng, không phải "quên đo".

### 10.5 Việc còn lại (KHÔNG thuộc S17 — ghi để phiên sau không đoán)

1. Hợp đồng **đọc** của `ChannelHub` (`IChannelListingReader`…) + của `Reconciliation` (`IReconRunReader`…) để
   chuyển nốt 3 lớp host vào module — PR riêng, có hợp đồng mới.
2. `MarketplaceReconciliationWorker` chỉ chuyển được khi `Messaging`/`Sales` có hợp đồng đọc phù hợp; hiện
   **không** cần vì không ai tham chiếu (H3).
3. Namespace legacy `eCommerce.Application.Contracts.Inventory` (của `IWarehouseAppService`/
   `IChinhSachHanMucAppService`) — đổi là refactor lan sang Web/DTO; **không** cần cho biên assembly.
4. Nợ **khác** của `audit-gd1.md` §0.1 **(b)** (Sales/Reconciliation đọc Domain module khác) — `S17` **không** sửa.

---

## 11. `S16` — 2/3 METRIC CÒN NỢ ĐÃ CÓ ĐIỂM PHÁT THẬT (29/10/2026)

> **Mục tiêu (theo bàn giao §2):** đóng 3 metric nợ của `XC-005` §19.3 **bằng hạ tầng thật**. Kết quả trung thực:
> **2 xong · 1 còn nợ**.

| Metric | Kết quả | Điểm phát / lý do |
|---|---|---|
| `lock_ttl_exceeded_total` | ✅ **XONG** | `DoThoiGianGiuKhoa` ở `src/eCommerce.Application/Observability/` — decorator `IAbpDistributedLock`, nghe `IDistributedSynchronizationHandle.HandleLostToken` |
| `logging_failures_total` | ✅ **XONG** | `DemLoiGhiLogProvider` — bọc **mọi** `ILoggerProvider` ở **Host + Web**; sink ném ⇒ `Console.Error` + counter; không nuốt log, không đệ quy |
| `messaging_dlq_replayed_total` | ❌ **CÒN NỢ** | **Chưa có luồng phát lại DLQ.** `DeadLetterAppService` chỉ đọc + đánh dấu; đếm "đánh dấu" thành "phát lại" là nói dối ⇒ giữ trong `ChuaCoNoiPhat` |

### 11.1 ĐÍNH CHÍNH: tham số `timeout` của `IAbpDistributedLock` KHÔNG phải TTL

Đo từ mã ABP 10.7: `MedallionAbpDistributedLock.TryAcquireAsync(name, timeout, ct)` chuyển `timeout` xuống
`IDistributedLockProvider.TryAcquireLockAsync(key, timeout, ct)` — đó là **thời gian CHỜ giành khoá**, không phải
TTL lease. TTL thật do Medallion giữ (mặc định 30 s) và **tự gia hạn** khi handle còn sống. Vì vậy **không** so
"thời gian giữ khoá > timeout" (sẽ báo động giả cho job quét hợp pháp); thay vào đó nghe `HandleLostToken` —
token **chỉ huỷ khi lease đã mất thật**. Đây là cách duy nhất đo đúng mà không đổi hành vi khoá.

### 11.2 Bằng chứng hai chiều (7 ca mới)

`test/eCommerce.Tests/Observability/S16MetricTests.cs`:

- **CÓ sự kiện:** mất lease ⇒ `lock_ttl_exceeded_total = 1` (nhãn `module` hữu hạn); sink ném ⇒
  `logging_failures_total = 1` + dòng dự phòng chứa **nguyên** message/exception.
- **KHÔNG có sự kiện:** trả khoá bình thường ⇒ 0; mất token **sau** khi trả ⇒ 0; sink bình thường ⇒ 0
  và message vẫn đến sink; không giành được khoá ⇒ vẫn trả `null` như cũ.
- **DI thật:** `LoggerFactory` + provider thật đi qua wrapper ⇒ sink ném vẫn phát metric.

### 11.3 Không đổi gì ngoài phạm vi

**0 migration · 0 thay đổi schema · 0 quyền mới · 0 route/DTO đổi.** Chỉ thêm 2 lớp decorator ở host/Application,
đăng ký DI trong `eCommerceHttpApiHostModule`, cập nhật bảng nợ + tài liệu + test.

### 11.4 Đo cổng cuối (cây đã sửa)

- `-DayDu`: **exit 0 · 370.7s · 15/15** · unit **1612** · integration **87/87** · E2E **145/145** ·
  DB **25/25** · NND **64** · **71** quyền · migration **49** · quét **99** chỗ.
- `check:mermaid` **0 lỗi** · `kiem-link-tai-lieu` **0 chết**.

### 11.5 🆕 `S16b` — metric THỨ 3 ĐÃ ĐÓNG: **replay DLQ = publish lại event** (29/10/2026)

> **Chủ dự án chốt:** *"replay"* = **phát lại event lên bus**, **KHÔNG** phải *"đánh dấu đã xử lý"*.
> 📄 Chi tiết đầy đủ: **`XC-005` §21** (metric) + **`XC-002` §18** (DLQ/outbox).

| Việc | Kết quả |
|---|---|
| Đường phát lại | `IDeadLetterAppService.PhatLaiAsync(Guid id)` → `POST /api/app/dead-letter/{id}/phat-lai` — **deserialize payload gốc → `IDistributedEventBus.PublishAsync(type, event, onUnitOfWorkComplete: false, useOutbox: true)`** |
| Cùng UoW | `Begin(requiresNew: true, isTransactional: true)` ⇒ dấu `ReplayedAtUtc` + dòng **outbox** **cùng transaction** (không mất event, không phát hai lần) |
| Idempotent | `ReplayedAtUtc` + `ReplayCount` (cột **riêng**, **không** suy từ `DaXuLy`) · lần hai ⇒ **200 + `DaPhatLai=false`** + lý do · entity **NÉM `0008`** nếu bị gọi lần hai |
| Fail-closed | `TenSuKien` không ánh xạ được ⇒ `0009` (kể cả **nhập nhằng** cùng tên ngắn); payload không khớp ⇒ `0010` · **không** publish · HTTP **409** (map ở host, không để ABP trả `403`) |
| Không xoá | Dòng DLQ **giữ nguyên** làm bằng chứng; chỉ ghi dấu + `DaXuLy` |
| Quyền | **`eCommerce.Operations.DeadLetters.Replay`** — khai **cả hai nơi** + `quyenPhaiCon` của `kiem-db.ps1` + **seed qua `DbMigrator`** (**71 → 72** quyền) |
| Metric | `messaging_dlq_replayed_total` **+1 SAU commit** (nhãn `result=ok` · `eventName`) · xoá khỏi `ChuaCoNoiPhat` ⇒ **16/16** |

**Bằng chứng HAI CHIỀU:** unit **11** ca (`DeadLetterPhatLaiTests` — publish đúng **1** lần/đúng dữ liệu, lần hai
**không** publish + metric **không** tăng, 2 ca fail-closed **NÉM** + metric **0**, 2 ca bộ ánh xạ) ·
integration **3** ca (đọc/ghi 3 cột + giới hạn 256) · probe HTTP **21/21** (`tools/probe-dlq-replay.ps1`):
`200` → DB `1|true|true|admin` → **`AbpEventInbox` +1** (event **đã đi thật**) → lần hai **không** có inbox thứ hai →
`TenSuKien` rác ⇒ **409**, không ghi dấu.

**Đo cổng cuối:** `-DayDu` **exit 0 · 377.4s · 15/15** · unit **1627** · integration **90/90** · E2E **159/159** ·
DB **25/25** · NND **64** · **72** quyền · migration **50** · quét **99** chỗ · `check:mermaid` **0 lỗi** ·
`kiem-link-tai-lieu` **0 chết**.

⚠️ **Còn nợ:** không phát lại **tự động** (cố ý) · không phát lại **theo lô** (`ReplayBatchAsync` vẫn là đặc tả) ·
không có **UI** DLQ · không có job **dọn** DLQ theo thời hạn.

### 11.6 🆕 `S14H` — TRẠNG THÁI `PartiallyReturned` (29/10/2026) — *đóng `SAL-006` §32.5 #1*

> **BA chốt 29/10/2026:** nhận hàng hoàn **một phần** phải đọc được ở **đơn** — không được để đơn đứng nguyên
> `ReturnApproved` (trước lát cắt này báo cáo/UI nói *"chưa trả gì"* trong khi đã nhận một phần).
> 📄 Chi tiết đầy đủ: **`SAL-006` §35**.

| Việc | Kết quả |
|---|---|
| Enum | `ReturnStatus.PartiallyReturned = 6` — **append ở CUỐI dải**; `0…5` **không** đổi số (`OrdOrders.ReturnStatus` lưu `int` ⇒ đổi số là diễn giải lại lịch sử) |
| Cạnh | **đúng 2**: `ReturnApproved → PartiallyReturned`, `PartiallyReturned → Returned`. ❌ không `Open`/`ReturnRequested → PartiallyReturned` · ❌ không `PartiallyReturned → Cancelled` (đã nhận hàng thì không huỷ) · ❌ không cạnh tự thân · **không hấp thụ** |
| Dịch vụ | `OrderReturnService.NhanHangAsync`: phiếu `Received` ⇒ đơn `Returned`; phiếu `PartiallyReceived` **và** đơn `ReturnApproved` ⇒ `PartiallyReturned`; nhận thêm một phần khi đơn đã `PartiallyReturned` ⇒ **không chuyển** (trạng thái không đổi) — lịch sử/ETO do aggregate ghi |
| `OrderStatus` tổng thể | **KHÔNG** thêm giá trị: đơn trả một phần **vẫn `Completed`**; *"đã trả một phần"* **chỉ** đọc ở `ReturnStatus` (tránh trục thứ 5 trá hình) |
| UI | in **chuỗi** ⇒ không sửa mã; nút **Huỷ phiếu** vẫn khớp guard `OrderReturn.Huy` (`Status ∈ {Requested, Approved}` ∧ `GoodsStatus ∉ {Received, PartiallyReceived}`) |
| Test | **+7 unit** hai chiều (`OrderStateMachineTests`: cạnh hợp lệ · `PartiallyReturned → Returned` · `Open/ReturnRequested` **NÉM** · tự thân **NÉM** · không hấp thụ · `InlineData` suy ra `Completed`) · **E2E**: đổi `S14-E12` (`6\|3\|4`) + mới `S14-E21` (chuỗi `PartiallyReturned` trên trang đơn) và `S14-E22` (nhận nốt ⇒ `4\|4\|6`) — **đo DB** |
| Schema | **0** migration · NND **64** · **72** quyền · **99** chỗ quét |

**Đo cổng cuối:** `-DayDu` **exit 0 · 389.7s · 15/15** · unit **1634** · integration **90/90** · E2E **167/167** ·
DB **25/25** · `check:mermaid` **0 lỗi** · `kiem-link-tai-lieu` **0 chết** · dọn probe **= 0** (gồm outbox/inbox).
