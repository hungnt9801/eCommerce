# KIỂM TOÁN GIAI ĐOẠN 1 — "bán được trên website"

> **Ngày đo:** 24/10/2026 · **Cây:** `main` @ `531bd9d` · **Người đo:** phiên kiểm toán độc lập
> **Câu hỏi:** *"11 phiên `S1…S11` đã xong — GĐ1 đã hoàn thành chưa, còn thiếu gì?"*
> **Cách làm:** **đo, không tin chữ**. Chạy cổng thật + truy vấn PostgreSQL thật + quét mã nguồn.
> **Nguồn sự thật:** `docs/implementation-plan.md` §7 (phạm vi GĐ1) và §A.5 (cổng tài liệu).

---

## 0. Kết luận trong 6 dòng

1. **Cổng kỹ thuật XANH THẬT** — không phải "xanh trên giấy": 12/12 bước, `exit 0`, 1422 unit, 71 integration, 133/133 E2E.
2. **Nhưng GĐ1 CHƯA hoàn thành theo DoD của chính plan:** 2/5 cổng nghiệm thu **FAIL**, 2/5 **PARTIAL**, 1/5 PASS.
3. **Số module không đủ — đúng như cảm nhận ban đầu:** chỉ **9** thư mục module / **16** module trong plan; **4 module GĐ1** (Catalog · Inventory · Sales · Reconciliation) **không tồn tại dưới dạng module**.
4. **~8.700 dòng code GĐ1 nằm trong `src/`** (host), không có biên giới module ⇒ vi phạm quyết định #3, chặn GĐ2.
5. **State machine đơn hàng KHÔNG tồn tại:** `Order.SetStatus` là **phép gán trần**; không có `OrderStatusHistory`; chỉ 2 trục thay vì 4.
6. **Cổng tài liệu chưa từng mở:** 1/55 tài liệu `Approved`; 0/19 tài liệu GĐ1; 0/7 tài liệu nền `XC-*`.

> 🔑 Kết luận trung thực: **"GĐ1 chạy được" ≠ "GĐ1 hoàn thành"**. Luồng bán hàng chính hoạt động và có bằng chứng HTTP/DB;
> phần **cấu trúc module, state machine, outbox và bằng chứng chống oversell** thì chưa.

---


## 0.1 CẬP NHẬT CUỐI — sau 12 vòng (24/10/2026)

> 📌 Mục 0–7 là **ảnh chụp lúc kiểm toán**. Bốn việc dưới đây đã **đóng** và có bằng chứng đo lại;
> các khoảng trống **khác** của GĐ1 (entity thiếu, UI admin, tài liệu `Approved`…) **vẫn còn** — xem §3–§8.

| Hạng mục cam kết | Trạng thái cuối | Bằng chứng |
|---|---|---|
| **P0 #4 — oversell 2 luồng** | ✅ | `probe-oversell` = bước ⑬ · **11/11** · 1×200 + 1×403 `Inventory:0001` · `Reserved=1 ≤ OnHand` · thêm map **concurrency → `Inventory:0001`** (đo được race thật) |
| **SAL-003 — 7 nợ** | ✅ 7/7 | RowVersion (migration 44) · ETO+outbox · 6 permission/6 API · 3 setting · job quét 24 h · metric `sales_order_transition_total` · UI 4 trục + lịch sử (E2E **135**) |
| **P0 #3 — outbox/inbox + relay** | ✅ (trừ 2-instance) | `probe-outbox` = bước ⑭ · **9/9**: relay 600 s ⇒ commit ⇒ **giết Host** ⇒ outbox còn ⇒ restart ⇒ **phát lại** + inbox có dòng **mới** cho đúng event |
| **P0 #1 — tách 4 module** | ✅ 4/4 | Catalog · Inventory · Reconciliation · Sales — mỗi module **4 project** (Domain/Contracts/Application/EF) + mapping EF trong module; **3 lần migration RỖNG**; test kiến trúc chặn module → host |

⚠️ **Nợ còn lại, ghi rõ:** (a) outbox **2 instance** chưa đo; (b) module Sales/Reconciliation đọc Domain module khác (chưa có hợp đồng); (c) vài app service/worker ở host có lý do; (d) các khoảng trống GĐ1 **ngoài** 4 việc này **chưa** đóng.
## 1. Baseline đo được (24/10/2026, `-DayDu`)

| Bước | Kết quả | Ghi chú |
|---|---|---|
| ① Dọn tiến trình + cổng trống | ✅ | 5000 + 5100 |
| ② Build + kiểm mốc nhị phân | ✅ 70.8s | 71 tệp DLL/EXE đổi mốc |
| ③ Unit test | ✅ **1422/1422** | 5.7s |
| ④ Integration (PostgreSQL thật) | ✅ **71/71** | 10.1s |
| ⑤ Mermaid | ✅ 213 khối · 0 lỗi | 43.5s |
| ⑥ Bất biến DB | ✅ **25/25** · NND **53** · **62** quyền · quét **86** chỗ | 6.8s |
| ⑦ Test cho chính các cổng | ✅ 38 ca | 93.9s |
| ⑧ E2E có phiên đăng nhập | ✅ **133/133** | 42.4s |
| ⑨ Sổ địa chỉ HTTP thật | ✅ 17/17 | 13.1s |
| ⑩ Khách hàng HTTP thật | ✅ 26/26 | 15.5s |
| ⑪ Thanh toán HTTP thật | ✅ 26/26 | 13.1s |
| ⑫ Đơn sàn — đường chạy thật | ✅ 49/49 | 21s |
| **Tổng** | ✅ **`exit 0` · 337.4s** | so tài liệu 247.3s: **máy**, không phải sản phẩm |

**Kết luận mục 1:** các tuyên bố *"1422 unit · 71 integration · 133 E2E · NND 53 · 62 quyền"* trong
`SESSION-KICKOFF.md` §3 là **đúng**. Vấn đề của GĐ1 **không phải** chất lượng test, mà là **phạm vi**.

---

## 2. Khoảng trống #1 — số module (gốc của mọi việc khác)

`docs/implementation-plan.md` §4 yêu cầu **16 bounded context**, mỗi module **3 project**. Thực đo:

```
modules/
├─ Address        (4 project)   ✅ GĐ0
├─ ChannelHub     (4)           ✅ GĐ4 (một phần)
├─ Customers      (4)           ✅ GĐ1
├─ EInvoice       (3)           ✅ GĐ0
├─ Messaging      (2)           ➕ hạ tầng, không nằm trong 16
├─ PaymentHub     (4)           ✅ GĐ1
├─ Pricing        (4)           ✅ GĐ1
├─ Signing        (3)           ✅ GĐ0
└─ Tax            (2)           ➕ XC-006, không nằm trong 16
```

**11/16 module nghiệp vụ có thật** (thêm `Catalog`, `Inventory`, `Sales`, `Reconciliation` — tách 24/10/2026). 5 module còn lại thuộc GĐ2–GĐ3: `Promotions`, `ShippingHub`, `Reporting`, `Notifications`, `AccountingHub` (chưa tới hạn).

### 2.1 Bốn module GĐ1 — ĐÃ TÁCH khỏi host (24/10/2026)

| Module plan | Thực tế | Quy mô |
|---|---|---|
| **Catalog** | ✅ **ĐÃ TÁCH 24/10/2026** — `modules/Catalog` (Domain + Contracts + Application + **mapping EF**; 4 project). 📌 `ChannelDirectory` + `HienThiKenhAppService` ở lại host (đọc `ChannelHub`), đúng kế hoạch L2 | 15 tệp / 2.550 dòng + 7 / ~1.700 dòng đã chuyển; 2 tệp host |
| **Inventory** | ✅ **ĐÃ TÁCH 24/10/2026** — `modules/Inventory` (4 project; Domain 9 tệp · Contracts 4 · Application 3 + **mapping EF**). Phần **đối soát kho** (5 tệp + 3 app/worker) **cố ý** còn host → sẽ sang `Reconciliation`. `ChinhSach`/`TenChinhSach` đã về **Kernel** | — |
| **Sales** | ✅ **ĐÃ TÁCH 24/10/2026 (lát cắt 1)** — `modules/Sales` (4 project; Domain 6 · Contracts 5 · Application 14 + **mapping EF**). `MarketplaceReconciliationWorker` ở host (dùng DTO Messaging). ⚠️ module → `Inventory.Domain`/`Pricing.Domain` (nợ có ý thức) | — |
| **Reconciliation** | ✅ **ĐÃ TÁCH 24/10/2026** — `modules/Reconciliation` (4 project; Domain 5 tệp · Contracts 1 · Application 3 + **mapping EF** `ConfigureReconciliation`). ⚠️ module → `Inventory.Domain` là **đánh đổi có ý thức** (chưa có `IStockBalanceReader`). Đối soát **tiền** vẫn ở `PaymentHub` | — |

**Hệ quả đo được:**
- `DonHangAppService` có `using eCommerce.Inventory; eCommerce.Orders; eCommerce.Pricing; eCommerce.Customers;`
  trong **cùng một assembly** ⇒ **không có biên giới nào** để vi phạm — và cũng **không có** gì để `Promotions`/`RMA` bám vào ở GĐ2.
- `modules/Customers/.../Application` **tham chiếu** `src/eCommerce.Domain` (module → app) — vi phạm nguyên tắc #3.
- `modules/Pricing/.../Application` **tham chiếu** `Pricing.EntityFrameworkCore` — vi phạm phân tầng trong chính module.

> 📌 Đây **không** phải lỗi của 11 phiên: `S1…S11` là các phiên **trả nợ** (địa chỉ, khách, VNPay, đơn sàn,
> UI đối soát, CI). Việc tách module **chưa từng** nằm trong danh sách phiên.

---

## 3. Khoảng trống #2 — entity thiếu (đo trên DB thật, 85 bảng)

| Entity plan §7.1 | Bảng | Thực đo | Ghi chú |
|---|---|---|---|
| `Brand` | `CatBrands` | **0** | `Product.BrandId` là cột mồ côi (có cột, không bảng, không FK) |
| `CostPrice` | `PrcCostPrices` | **0** | Chỉ có trong chú thích; `PRC-001` coi là entity chính |
| `StockAllotment` | `InvStockAllotments` | **0** | Chỉ có `InvChannelLimits` (trần an toàn, **không** phải phân bổ) |
| `CheckoutSession` | `OrdCheckoutSessions` | **0** | Thay bằng `Order.CheckoutIdempotencyKey` + giỏ cookie (D7) — **quyết định đã chốt**, plan chưa sửa |
| `OrderStatusHistory` | — | **0** | 🚨 Không có nhật ký chuyển trạng thái |
| `OrderReturn`/`Line` | `OrdOrderReturns` | **0** | Trả hàng chỉ ghi phiếu kho `CustomerReturn`; code tự ghi *"chưa có `PartiallyReturned`"* |
| `PaymentReconciliationRun`/`Line`/`Discrepancy` | — | **0** | Chỉ có job + log + metric; **không có báo cáo lệch lưu lại** |
| `Cart`/`CartItem` | — | **0** | **Đúng theo D7** (giỏ cookie 2 giờ) — không tính là thiếu |

Ngoài ra **`AbpEventOutbox` / `AbpEventInbox` = 0 bảng**, **0 worker relay**.

---

## 4. Khoảng trống #3 — state machine đơn hàng

> 🟡 **CẬP NHẬT 24/10/2026 (cùng ngày kiểm toán):** lát cắt 1 **đã được triển khai** — xem **§12**.
> Bảng dưới đây là **trạng thái lúc kiểm toán** (trước khi sửa), giữ nguyên để làm bằng chứng.

Đặc tả: `docs/features/sales/sal-003-state-machine-4-truc.md` (4 trục, bảng chuyển có guard, lịch sử append-only).
Plan §7.1 ghi `SAL-003` **"✅ XONG + kiểm chứng"**.

Thực đo mã nguồn:

| Yêu cầu | Bằng chứng | Kết quả |
|---|---|---|
| Mọi chuyển trạng thái qua cửa có guard | `src/eCommerce.Domain/Entities/Orders/Order.cs:145` — `public void SetStatus(OrderStatus status) => Status = status;` | ❌ **gán trần, không ném lỗi** |
| 4 trục trực giao | `Order` có `Status` + `PaymentStatus` | ❌ **2 trục** |
| Lịch sử `OrderStatusHistory` | 0 entity, 0 bảng, 0 DbSet | ❌ |
| Chuyển sai ⇒ `eCommerce:Order:0001` | không có mã lỗi `eCommerce:Order:*` nào trong repo | ❌ |
| Hấp thụ (`Cancelled`/`Returned` không mở lại) | `SetStatus` nhận mọi giá trị | ❌ |
| Guard đồng thời (RowVersion) | không có `IsRowVersion`/`ConcurrencyCheck` trên `Order` | ❌ |

**Ca sai đang được chấp nhận im lặng:** gọi `SetStatus(OrderStatus.Pending)` trên đơn đã `Completed`,
hay `SetStatus(Shipped)` trên đơn đã `Cancelled` — **không exception, không log, không lịch sử**.
Đây đúng là vi phạm **nguyên tắc #11** mà plan đặt ra sau bài học eShop.

> ✅ Ghi nhận điểm tốt: trục **tài chính** (`DatTrangThaiThanhToan`) **có** luật hấp thụ và test
> (`OrderTrangThaiThanhToanTests`). Nhưng nó **không** ghi lịch sử và **không** nằm trong một state machine chung.

---

## 5. Khoảng trống #4 — outbox / event / metric

| Hạng mục | Thực đo | Ảnh hưởng |
|---|---|---|
| Outbox/Inbox | **0 bảng**, **0 worker relay**, transport **in-process** | Cổng GĐ1 #5 *"không mất event"* **FAIL**; restart là mất event |
| DLQ | có bảng `MsgDeadLetters` + job | ✅ nhưng **không** có relay để đẩy vào |
| Metric | **11/15** có nơi phát; 4 thiếu (`messaging_outbox_pending`, `messaging_dlq_replayed_total`, `lock_ttl_exceeded_total`, `logging_failures_total`) | Có cơ chế hai chiều `MetricEmitterTests` (tốt) |
| ETO sản phẩm | `ProductPublishedEto`, `VariantPriceChangedEto`, `ProductSlugChangedEto` **không có người nhận** | Đúng thiết kế `INotificationEvent` (chỉ Warning) — nhưng chưa có ai phản ứng |

---

## 6. Khoảng trống #5 — bằng chứng nghiệm thu

| Yêu cầu plan | Thực đo | Kết quả |
|---|---|---|
| Test integration **2 request đồng thời** chống oversell | **0** `Task.WhenAll` / `Parallel.` / `Task.Run` / `Start-Job` trong `test/` và `tools/` | ❌ **không có bằng chứng** (cơ chế lock + unique index **có**, nhưng chưa được đo) |
| Test **2 instance** + Redis thật | không có harness 2 tiến trình | ❌ |
| Thanh toán **tiền thật** | `PaymentHub:VnPay:TmnCode` = `""`, `HashSecret` = `""` | ❌ mới kiểm bằng `FakePaymentProvider` (dù HMAC thật) |
| UI admin tối thiểu | 10 view: `DatHang` `DoiSoat` `DoiSoatThanhToan` `DonHang` `HanMucKenh` `Home` `KhachHang` `Loi` `PhanBo` `SanPham` | ❌ **không có** màn hình Catalog / Pricing / Kho |
| API nào cũng cần xác thực | `BatBienPhanQuyenTests` + `AllowlistCongKhaiTests` | ✅ |

---

## 7. Cổng tài liệu (§A.5)

| Nhóm | Đã `Approved` | Tổng |
|---|---|---|
| Tài liệu nền `XC-*` | **0** | 7 |
| GĐ1 (`CAT-*`, `PRC-*`, `INV-*`, `SAL-*`, `CUS-001`, `REC-001`, `PAY-001`, `WEB-*`) | **0** | 19 |
| Toàn bộ `docs/features` | **1** (`ADDR-004`) | 55 |

⇒ Theo §A.5, GĐ1 **chưa từng đủ điều kiện bắt đầu code**. Việc code đã chạy là **thực tế đã xảy ra**,
không phải "cổng đã mở" — ghi ở đây để không ai trích dẫn sai.

📌 **Tài liệu lệch hai chiều** (vì sao phải đo):
- `CAT-003` ghi *"media + hiển thị theo kênh: **0 dòng code**"* — nhưng **đã có** `ProductMedia`,
  `ProductChannelVisibility`, 2 app service + test ⇒ **thực tế xong hơn tài liệu**.
- `SAL-003` ghi *"✅ XONG"* — nhưng state machine **không tồn tại** ⇒ **tài liệu nói quá**.

---

## 8. Đối chiếu 5 cổng nghiệm thu GĐ1 (plan §7.1)

| # | Cổng | Trạng thái | Căn cứ |
|---|---|---|---|
| 0 | Tài liệu GĐ1 + `XC-001…007` đã `Approved` | ❌ **FAIL** | 1/55 |
| 1 | Khách đặt được đơn trên web, thanh toán **1 cổng VN**, trạng thái đúng | 🟡 **PARTIAL** | E2E 133/133 với `FakePaymentProvider`; VNPay thật chưa kiểm (credential rỗng) |
| 2 | Bán vượt tồn **không thể** — test 2 request đồng thời, 2 instance | ❌ **FAIL** | 0 test đồng thời |
| 3 | **Không mất tiền** — job đối soát phát hiện lệch | 🟡 **PARTIAL** | job `querydr` + UI có; **không** lưu run/line/discrepancy; chưa tiền thật |
| 4 | Mọi API cần xác thực + màn hình login | ✅ **PASS** | test reflection + allowlist |
| 5 | **Không mất event** — outbox relay chạy | ❌ **FAIL** | 0 outbox, 0 relay |

---

## 9. Việc còn thiếu — theo thứ tự ưu tiên

### P0 — chặn GĐ2
1. **Tách 4 module** `Catalog` · `Inventory` · `Sales` · `Reconciliation` (kế hoạch: `docs/ke-hoach-tach-module.md`).
2. **State machine 4 trục + `OrderStatusHistory`** — thay `SetStatus`, ném `eCommerce:Order:0001/0006`, ghi lịch sử.
3. **Outbox/Inbox + relay + DLQ replay** + test "publish lỗi vẫn phát lại".
4. **Test oversell 2 luồng đồng thời** (PostgreSQL thật; 2 instance là bước sau).
5. **Entity thiếu:** `Brand`, `CostPrice`, `StockAllotment`, `OrderReturn/Line`, `PaymentReconciliationRun/Line/Discrepancy`.

### P1
6. UI admin Catalog/Pricing/Kho + UI nhóm khách; đưa `/KhachHang` vào menu.
7. Credential sandbox VNPay (+ GHN/NCC hoá đơn cho GĐ0) để kiểm tiền thật.
8. Duyệt 7 tài liệu `XC-*` + 19 tài liệu GĐ1 (hoặc thoả thuận song song bằng văn bản theo §A.5).

### P2
9. 4 metric còn thiếu; người nhận 3 ETO sản phẩm; guard tổng đơn âm + cấu hình làm tròn (`PRC-002`);
   cập nhật `Order` khi áp watermark (`SAL-005`); UI nhóm khách + `CustomerChangedEto` (`CUS-001`).
10. Sửa tài liệu cho khớp code (CAT-003 media; SAL-003; Cart/CheckoutSession).

---

## 10. Cách tái lập phép đo (đừng tin bài này, hãy chạy lại)

```powershell
pwsh -File tools\kiem-tat-ca.ps1 -DayDu          # 12 bước · exit 0
docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A ^
  -c "select count(*) from pg_tables where schemaname='public'"        # 85
docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A ^
  -c "select count(*) from ""AbpPermissionGrants"""                    # 62
docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A ^
  -c "select count(*) from ""__EFMigrationsHistory"""                  # 41
git rev-list --count HEAD; git status --porcelain                      # 310 · sạch
```

> 🔑 *"Một phép đo bất ngờ thường là phép đo SAI, không phải sản phẩm sai"* — nhưng lần này phép đo
> **khớp** với tài liệu ở mọi con số kỹ thuật, và **lệch** ở đúng chỗ tài liệu tự nhận: phạm vi và DoD.

---

## 11. Lịch sử tệp này

| Ngày | Thay đổi |
|---|---|
| 24/10/2026 | Bản đầu — kiểm toán GĐ1 sau 11 phiên `S1…S11`. Kết luận: kỹ thuật xanh, **DoD chưa đạt**; 4 module GĐ1 chưa tách; state machine đơn hàng chưa có. |
| 24/10/2026 (sau) | **Đã thực hiện một phần đề xuất** — xem §12: viết `docs/ke-hoach-tach-module.md`; triển khai **lát cắt 1** của P0 #2 (state machine 4 trục + `OrderStatusHistory` + migration 42 + 24 test). P0 #3/#4 ghi kế hoạch, **chưa code**. |

---

## 12. Cập nhật sau kiểm toán — việc đã làm (24/10/2026)

| Việc | Trạng thái | Bằng chứng |
|---|---|---|
| **(a) Báo cáo audit** | ✅ | chính tệp này |
| **(b) Kế hoạch tách 4 module** | ✅ viết | `docs/ke-hoach-tach-module.md` — thứ tự lá→gốc (Reconciliation → Inventory → Catalog → Sales), giữ namespace/tên bảng, **0 migration**, test kiến trúc chặn tái phát; ước lượng **20–29 pd** |
| **(c1) P0 #2 — state machine đơn hàng** | 🟡 **lát cắt 1 XONG** | 4 enum trục + `OrderStateMachine` (30 cạnh) + `OrderStatusHistory` (append-only) + **xoá `SetStatus`** + mã lỗi `eCommerce:Order:0001…0008` + migration **42** + **25 unit test**; unit **1422 → 1447**; NND **53 → 54**; `sal-003` §16 |
| **Đo lại CUỐI (12 vòng)** | ✅ **`-DayDu` exit 0 · 323.9s** | **14/14** bước · **1504 unit** · **71/71** integration · **135/135** E2E · **25/25** DB · **NND 54** · **68** quyền · **44** migration · **89** chỗ quét · oversell **11/11** · outbox replay **9/9** |
| **Kiểm chứng HTTP THẬT (không chỉ unit test)** | ✅ | ① Seed đơn `Pending` → `POST /api/app/don-hang/{id}/huy-don` ⇒ **200**, DB có **đúng 1** dòng `OrdStatusHistory`: `Closure｜0→5｜Staff｜HuyDon｜v=1`, đơn `Status=5 · ReturnStatus=5 · IsCancelled=true`. ② Seed đơn **đã xuất kho** → cùng endpoint ⇒ **403 + `eCommerce:Order:0001`**, **0** dòng lịch sử, trạng thái **không đổi**. ③ Xoá đơn ⇒ lịch sử **cascade** sạch (không còn GUID probe). |
| **(c2) P0 #4 — test oversell 2 luồng** | ✅ **XONG (24/10/2026)** | `tools/probe-oversell.ps1` = **bước ⑬** của cổng: seed `OnHand = 1`, bắn **hai** `POST tinh-va-giu-cho` **cùng lúc** (`HttpClient` + `Task.WhenAll`), **11/11 ca**: đúng 1 × `200` · 1 × `403 eCommerce:Inventory:0001` · `InvStockReservations = 1` · `Reserved = 1 ≤ OnHand` · `Available = 0`. ⏳ **Còn**: biến thể **2 instance** (2 process + Redis) — vẫn là nợ riêng |
| **(c3) P0 #3 — outbox/relay** | ✅ **ĐÃ BẬT (24/10/2026)** — `XC-002` §17: `IHasEventOutbox`/`IHasEventInbox` + 2 `DbSet` + `UseDbContext` 2 chiều + migration **43**; đo được `AbpEventOutbox` **rỗng** sau relay, `AbpEventInbox` **có dòng**; E2E **133/133**. ⏳ **Còn**: test "publish lỗi ⇒ phát lại" + 2 instance + metric `messaging_outbox_pending` |
| **(c4) SAL-003 #2 — OrderStatusChangedEto** | ✅ **XONG (24/10/2026)** | Order.Chuyen gọi AddDistributedEvent ⇒ ghi **cùng UoW** xuống AbpEventOutbox, relay phát ≤2 s; gắn INotificationEvent (dữ liệu đã ở OrdStatusHistory). Test Moi_lan_chuyen__dang_ky_DUNG_MOT_ETO_trong_cung_transaction; unit **1453**. ⏳ chưa có người nhận (thuộc NOT-*/CHN-*) |
| **(c5) SAL-003 #3 — permission + API** | ✅ **XONG (24/10/2026)** | 6 permission Commerce.Orders.* (seed **62 → 68**) + OrderStatusAppService 6 method (mỗi method một quyền; **không** có ChangePaymentStatus). Đo HTTP thật: Pending→Confirmed **200** · version lệch **403  003** · nhảy cóc **403  001** · history 	otalCount = 1 |
| **(c6) SAL-003 #4 — setting** | ✅ **XONG (24/10/2026)** | 3 setting Orders.MaxDeliveryAttempts/ReturnWindowDays/MaxPaymentAttempts (3/7/3); ReturnWindowDays **cưỡng chế thật** ở RequestReturnAsync (đã Delivered + trong cửa sổ). Hai setting còn lại mới khai báo cho GĐ2 |
| **(c7) SAL-003 #5/#6 — job + metric** | ✅ **XONG (24/10/2026)** | OrderDerivedStatusScanWorker (24 h, RunOnStart, khoá phân tán, **chỉ ĐỌC**, dùng chung hàm suy ra thuần) · OrderStatusMetricHandler nghe OrderStatusChangedEto ⇒ sales_order_transition_total phủ mọi đường; XC-005 §5.1 = **12/16** |
| **(c8) SAL-003 #1 — RowVersion** | ✅ **XONG (24/10/2026)** | Order.RowVersion (igint, tăng mỗi lần chuyển) + .IsConcurrencyToken() ⇒ EF dùng giá trị gốc trong WHERE; xung đột map ** 002** ở cả 2 app service; migration **44**; test model + tăng/không-tăng. **L8**: không dùng xid vì Npgsql 10 bỏ helper, shadow xmin đụng cột hệ thống |
| **(c9) SAL-003 #7 — UI 4 trục** | ✅ **XONG (24/10/2026)** | /DonHang/ChiTiet thêm bảng **4 trục** (giá trị + version từng trục + RowVersion) và **lịch sử trạng thái**; kiểm History.View trước khi gọi (ẩn khối, không làm cả trang 403). E2E **133 → 135** (SAL-U1/SAL-U2 đọc HTML thật) ⇒ **SAL-003 đủ 7/7 hạng mục** |
| **(c10) Outbox metric** | ✅ **XONG (24/10/2026)** | OutboxPendingWorker (project EF, 60 s, RunOnStart) đếm AbpEventOutbox + SetMessagingOutboxPending; log thật [OUTBOX-PENDING-BAT-DAU]/[OUTBOX-PENDING-RONG]/[OUTBOX-PENDING-XONG] trong probe; XC-005 §5.1 = **13/16** |
| **(c11) Outbox replay** | ✅ **XONG (24/10/2026)** | 	ools/probe-outbox.ps1 = **bước ⑭**: relay tạm 600 s ⇒ commit ⇒ **giết Host** ⇒ AbpEventOutbox còn dòng ⇒ restart ⇒ outbox **về 0** + AbpEventInbox **tăng** ⇒ **9/9**. ⏳ còn biến thể 2 instance |
| **(c12) P0 #1 — pilot tách Catalog** | 🟡 **LÁT CẮT 1 (24/10/2026)** | modules/Catalog 4 project; Domain(15 tệp)+Contracts(9)+Application(7) đã chuyển; host giữ ChannelDirectory/HienThiKenhAppService (phụ thuộc ChannelHub). ✅ **Migration RỖNG** (không đổi lược đồ) · 1471 unit · 71 integration · test kiến trúc CatalogModuleKienTrucTests. ⏳ còn: chuyển **mapping EF** sang module + 2 tệp host |
| **(c13) P0 #1 — tách Inventory** | 🟡 **LÁT CẮT 1 (24/10/2026)** | modules/Inventory 4 project; Domain(9)+Contracts(4)+Application(3) đã chuyển; recon kho còn host (sang Reconciliation); ChinhSach → Kernel. ✅ **Migration RỖNG** · 1486 unit · test kiến trúc InventoryModuleKienTrucTests. ⏳ còn: mapping EF + ChinhSachHanMucAppService/WarehouseAppService (phụ thuộc ChannelHub/recon) |
| **(c14) P0 #1 — tách Reconciliation** | ✅ **XONG (24/10/2026)** | modules/Reconciliation 4 project; Domain(5)+Contracts(1)+Application(3)+**mapping EF**; WarehouseAppService/ChinhSachHanMucAppService ở host (lý do rõ). ✅ **Migration RỖNG** · 1495 unit · 10 ca kiến trúc. ⚠️ nợ: module → Inventory.Domain (đọc trực tiếp) |
| **(c15) P0 #1 — tách Sales (lát cắt 1)** | 🟡 **24/10/2026** | modules/Sales 4 project; Domain(6)+Contracts(5)+Application(14) đã chuyển; MarketplaceReconciliationWorker host (DTO Messaging). ✅ **migration RỖNG (2 lần)** · 1504 unit · 13 ca kiến trúc. ✅ **Mapping EF đã chuyển** (`ConfigureSales`). ⏳ còn: `MarketplaceReconciliationWorker` ở host (dùng DTO Messaging) |

### 12.1 Kế hoạch P0 #4 — bằng chứng chống oversell (không thể "làm cho có")

**Vì sao unit test không đủ:** `StockReservationService` dùng **khoá phân tán** + đọc lại tồn **sau** khi có khoá.
Hai lời gọi tuần tự trong một tiến trình **không** chứng minh được gì — chúng không chạy đua.

Các bước (theo đúng khẩu vị dự án: HTTP thật + DB thật):

1. **Harness**: `tools/probe-oversell.ps1` — seed 1 kho + 1 SKU `OnHand = 1`; lấy token.
2. **Bắn 2 request ĐỒNG THỜI** tới `POST /api/app/checkout/tinh-va-giu-cho` (2 `HttpClient` + `Task.WhenAll`,
   **không** `Start-Job` — bài học `S9`: `ss`/`lsof` không có trên runner tối giản).
3. **Khẳng định**:
   - đúng **1** request thành công, **1** request bị chặn (`eCommerce:Inventory:0001`/`0032` — **không** `500`);
   - DB: `Reserved ≤ OnHand` và `Available = 0`;
   - **chiều ngược**: nếu bỏ khoá (tiêm) ⇒ test **ĐỎ** (2 request cùng thành công).
4. **2 instance**: chạy thêm `HttpApi.Host` thứ hai (cổng khác, cùng DB + Redis) và bắn mỗi instance 1 request
   ⇒ khoá phân tán là thứ duy nhất còn chặn.
5. Ghép vào bước ⑫/⑬ của `tools/kiem-tat-ca.ps1` (thêm **một** bước, không sửa bước cũ) + cập nhật số ca.

> ⚠️ **Không** được viết test giả lập bằng `Parallel.For` trên `InventoryItem` trong bộ nhớ: nó **xanh** kể cả khi
> khoá phân tán bị gỡ ⇒ đúng loại "phép đo SAI" mà dự án đã ghi ở `S11`.

### 12.2 Kế hoạch P0 #3 — outbox/relay

1. **Quyết định trước**: transport vẫn **in-process** hay chuyển broker? Nếu in-process thì *"outbox bảo đảm giao
   tin cậy"* chỉ có nghĩa khi **có** consumer chạy lại được — cần chủ dự án chốt (câu hỏi Q1 của `XC-002`).
2. Nếu chốt: bảng `MsgOutbox` (theo `XC-002` §5) + relay worker (`RunOnStart`, UoW riêng, khoá phân tán)
   + `MsgInbox` cho consumer idempotent + DLQ đã có (`MsgDeadLetters`) + `messaging_outbox_pending` metric.
3. Test hai chiều: (a) publish lỗi ⇒ event **còn** trong outbox và **phát lại** được sau khi sửa;
   (b) tiêm "xoá relay" ⇒ test **ĐỎ**.
4. **Không** gộp với việc tách module (PR riêng) — đây là thay đổi lược đồ + hạ tầng.