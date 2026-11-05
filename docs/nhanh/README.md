# Phương án: thiết kế lại `eCommerce` theo Nhanh.vn + Web Admin Next.js

> **Trạng thái:** đề xuất (Draft) · **Ngày:** 02/11/2026 · **Người lập:** phiên khảo sát
> **Phạm vi:** khảo sát repo `rea`, khảo sát Nhanh.vn (businessId 230222), đối chiếu repo `eCommerce`,
> đề xuất kiến trúc + thiết kế Web Admin Next.js + lộ trình.

---

## 1. Tóm tắt trong 10 dòng

1. **Nhanh.vn là phần mềm QLBH đa kênh**, không phải website bán hàng: trung tâm là **hàng hóa – kho –
   hóa đơn bán lẻ – đơn giao vận – kế toán**.
2. Repo `eCommerce` hiện có **57 project, ~1.700 test, 55 tài liệu**, nhưng chỉ phủ **~7% bề mặt vận hành**
   của Nhanh (13 mục menu so với 180).
3. **Tái dùng được ~40% domain** (catalog lõi, sổ cái kho, state machine 4 trục, đối soát, auth, outbox).
4. **4 xung đột mô hình phải xử lý trước**: Variant vs cha–con/ĐVT · tồn 1 số vs vector 9 chiều ·
   7 trạng thái vs 18 · website-first vs POS-first.
5. **Đề xuất:** giữ ABP monolith, refactor 5 module, viết mới 6 module (POS, Khuyến mại, Kế toán,
   Báo cáo, Vận chuyển, Nhà cung cấp), **Web Admin mới bằng Next.js** thay dần Razor.
6. **UI giống Nhanh** đến mức người dùng cũ dùng được ngay: giữ route `/module/entity/action`,
   tab có badge, bộ lọc 2 tầng, bảng 20 cột, nút chính **xanh lá `#059669`**, font Inter, icon Phosphor.
7. **18 màn P0** là phạm vi thi công đầu tiên; 14 màn P1; còn lại P2/không làm.
8. **Thời gian:** P0 dùng được **10–12 tuần** (2 người); P0+P1 **~9 tháng** (3 người);
   ngang Nhanh 14–18 tháng (không nên).
9. **3 bằng chứng khảo sát** đã lưu: **204 route**, **180 mục menu**, **ảnh + cột bảng thật**, kèm
   **68 endpoint Open API v3** và **10 webhook**.
10. **Cần bạn chốt 6 quyết định** ở §5 trước khi tôi viết dòng code đầu tiên.

---

## 2. Bạn hỏi gì → tôi trả lời ở đâu

| Câu hỏi của bạn | Trả lời |
|---|---|
| Khảo sát repo `rea` để làm gì? | [06 — Cách khảo sát](06-phuong-phap-khao-sat-rea.md): REA attach Chrome, route inventory, chụp DOM/network, so ảnh |
| Khảo sát Nhanh.vn ra sao? | [01 — Khảo sát Nhanh](01-khao-sat-nhanh-vn.md): 204 route, 180 menu, token màu, 20 cột bảng, 18 trạng thái đơn |
| Source hiện tại thiếu gì? | [02 — Đối chiếu](02-hien-trang-va-doi-chieu.md): ma trận 16 module, 4 xung đột mô hình |
| Thiết kế lại source thế nào? | [03 — Kiến trúc đích](03-kien-truc-dich.md): cây solution, domain, state machine, API, đa doanh nghiệp, POS offline |
| Web Admin Next.js làm sao cho giống? | [04 — Web Admin](04-web-admin-nextjs.md): stack, design tokens, layout, 18 màn P0, POS, auth, hiệu năng |
| Bao lâu, rủi ro gì? | [05 — Lộ trình & rủi ro](05-lo-trinh-rui-ro.md): 6 giai đoạn, 120 dev-week, 12 rủi ro |

---

## 3. Ba phát hiện quyết định thành bại

### 3.1 Tồn kho là **vector 9 chiều**, không phải một số

```
Tồn · Đang chuyển kho · Đang giao · Tạm giữ · Lỗi · Bảo hành · Tạm giữ linh kiện · Đặt trước · Chờ nhập
Tồn trong kho = Tồn − Đang chuyển
Có thể bán    = Tồn trong kho − Tạm giữ − Lỗi
```

➡️ Nếu giữ `InventoryItem { OnHand, Reserved }` như hiện tại thì **không làm được** màn kho, kiểm kho,
POS hay báo cáo tồn của Nhanh. Đây là **thay đổi gốc rễ số 1** ([03 §3.2](03-kien-truc-dich.md)).

### 3.2 Sản phẩm **không có Variant** — có 10 loại + ĐVT + IMEI + lô

Nhanh dùng `parentId` (cha–con) + `units[]` (nhiều ĐVT, hệ số, giá riêng) + `type` 10 loại
(IMEI, cân đo, combo, gói, dịch vụ, theo lô, dụng cụ, voucher, nhiều ĐVT).
➡️ `ProductVariant` hiện tại phải **hạ cấp** thành "cách hiển thị thuộc tính", không còn là nơi giữ tồn/giá
([02 §3.1](02-hien-trang-va-doi-chieu.md)).

### 3.3 UI "giống Nhanh" nằm ở **pattern**, không ở màu

| Pattern | Vì sao quan trọng |
|---|---|
| Tab có **badge số** | Biến danh sách thành *hàng đợi việc* — nhân viên nhìn là biết còn 87 đơn cần xác nhận |
| **Bộ lọc 2 tầng** + `Lưu bộ lọc` | 20.000 sản phẩm, 158 đơn/ngày; lọc là thao tác chính |
| `Thao tác ▾` (bulk) | 16 hành động hàng loạt trên đơn — không thể bấm từng dòng |
| **Bảng 20 cột** + ẩn/hiện cột | Không có nó thì phải mở từng bản ghi |
| **Trạng thái điều khiển UI** | Nút sai trạng thái = nhân viên bấm rồi nhận lỗi = mất niềm tin |
| **POS đa tab + F1–F10** | Thu ngân thao tác bằng bàn phím, không chuột |

---

## 4. Phương án đề xuất (một trang)

### 4.1 Kiến trúc

```
Next.js 16 (App Router)  ──BFF/cookie──►  eCommerce.HttpApi.Host (.NET 10 + ABP 10.7)
   apps/admin-web                              │
   packages/api-client (OpenAPI)               ├── modules/Catalog      (Hàng hóa)   🟡 refactor
                                               ├── modules/Inventory    (Kho)        🟡 refactor
                                               ├── modules/Sales        (Đơn hàng)   🟡 mở rộng
                                               ├── modules/Pos          (Bán lẻ)     🔴 mới
                                               ├── modules/Customers    (Khách)      🟡 mở rộng
                                               ├── modules/Promotions   (Khuyến mại) 🔴 mới
                                               ├── modules/Accounting   (Kế toán)    🔴 mới
                                               ├── modules/Reporting    (Báo cáo)    🔴 mới
                                               ├── modules/Shipping     (Vận chuyển) 🔴 mới
                                               ├── modules/ChannelHub + PaymentHub + EInvoice + …
                                               └── PostgreSQL 17 + Redis 7
```

**Vì sao không viết lại backend:** mất 9–12 tháng để bằng hiện tại, bỏ 388 test + auth/permission +
outbox/idempotency + sổ cái kho đã kiểm chứng. **Vì sao không giữ Razor:** không đáp được bảng 20 cột,
tab động, POS đa tab, và không tách được BFF.

### 4.2 Phạm vi P0 (18 màn — làm trước)

Đăng nhập · App shell · Dashboard · DS sản phẩm · Form sản phẩm · Chi tiết sản phẩm · DS phiếu kho ·
Tạo phiếu kho · Kiểm kho · DS đơn hàng · Chi tiết đơn · **POS bán lẻ** · DS hóa đơn bán lẻ · Trả hàng ·
DS khách hàng · Chi tiết khách · Nhân viên & quyền · Mẫu in.

Chi tiết + tiêu chí nghiệm thu: [04 §5](04-web-admin-nextjs.md).

### 4.3 Lộ trình

| GĐ | Thời gian | Nội dung | Demo |
|---|---|---|---|
| 0 | 2 tuần | Chốt quyết định, REA, OpenAPI, skeleton Next.js, spike multi-tenant | Đăng nhập + shell |
| 1 | 6 tuần | Hàng hóa + kho + đơn (backend refactor + 6 màn) | SP → nhập kho → tồn → đơn |
| 2 | 4 tuần | POS + trả hàng + kết ca + in | Bán 3 SP, in, kết ca |
| 3 | 6 tuần | Khách + khuyến mại + vận chuyển + 6 báo cáo | Báo cáo doanh thu khớp |
| 4 | 6 tuần | Kế toán + HĐĐT + NCC | Sổ + công nợ |
| 5 | 6 tuần | Sàn TMĐT + bảo hành + hardening | Đơn Shopee về hệ thống |

Chi tiết: [05](05-lo-trinh-rui-ro.md).

---

## 5. Quyết định đã chốt (02/11/2026)

Bạn đã **chốt toàn bộ D-01…D-18 theo đề xuất** — chi tiết ở [decisions.md](decisions.md).
Tóm tắt 6 quyết định gốc:

| # | Quyết định | Đã chốt |
|---|---|---|
| 1 | Đa doanh nghiệp | `TenantId` từ đầu, **chưa bật** UI |
| 2 | Backend | **Giữ ABP + refactor** |
| 3 | Kế toán | **Công nợ + Excel trước**, bút toán kép ở GĐ4 |
| 4 | POS | **Có offline** |
| 5 | Báo cáo | **12 cái** |
| 6 | Website | **Không làm lại** |

➡️ Việc còn lại là **duyệt kế hoạch thi công** ở [07](07-ke-hoach-trien-khai.md) §12.

---

## 6. Danh mục tài liệu & bằng chứng

### Tài liệu (thư mục này)

| Tệp | Nội dung |
|---|---|
| [01-khao-sat-nhanh-vn.md](01-khao-sat-nhanh-vn.md) | Khảo sát Nhanh: stack, tokens, 204 route, pattern UI, mô hình dữ liệu |
| [02-hien-trang-va-doi-chieu.md](02-hien-trang-va-doi-chieu.md) | Hiện trạng repo + ma trận 16 module + 4 xung đột + nợ kỹ thuật |
| [03-kien-truc-dich.md](03-kien-truc-dich.md) | Cây solution, domain từng module, state machine, API, tenancy, POS offline |
| [04-web-admin-nextjs.md](04-web-admin-nextjs.md) | Design system, layout, 18 màn P0 + AC, POS, auth BFF, hiệu năng, test |
| [05-lo-trinh-rui-ro.md](05-lo-trinh-rui-ro.md) | 6 giai đoạn, 120 dev-week, 12 rủi ro, 10 ngày đầu |
| [06-phuong-phap-khao-sat-rea.md](06-phuong-phap-khao-sat-rea.md) | REA + CDP: cách chạy, giới hạn, pháp lý |
| [07-ke-hoach-trien-khai.md](07-ke-hoach-trien-khai.md) | **Kế hoạch thi công 12 tuần**: 10 WP, 6 sprint, 10 ngày đầu, migration, test, go/no-go |
| [dev-log-wp01-02.md](dev-log-wp01-02.md) | **Đã thi công**: API `/api/v1`, Next.js admin, login thật — số đo + 3 điểm lệch |
| [handover-n2.md](handover-n2.md) | **Đã thi công**: WP-03 nửa đầu — enum/ĐVT/IMEI/lô + migration **51**, 1671 test, 3 ĐVT qua API |
| [handover-n3.md](handover-n3.md) | **Đã thi công**: WP-03 nửa sau — backfill biến thể → SP con (dry-run → thật, idempotent) + `OrderLine.ProductId` + luật cha–con + adapter đọc cũ, migration **52**, 1684 test |
| [handover-n4.md](handover-n4.md) | **Đã thi công**: WP-04 (BE) — tồn kho **9 chiều** + phiếu kho kiểu Nhanh (`Mode`/chiều) + bút toán theo mode + API `/api/v1/stock/*`, migration **53**, 1768 test, probe HTTP 12 ca |
| [handover-n4b.md](handover-n4b.md) | **Đã thi công**: WP-04b — **`decimal(18,3)`** cho 16 cột số lượng (migration **54**), WP-04 (BE) **đóng**; 1773 test, probe 12 ca vẫn xanh |
| [handover-n5.md](handover-n5.md) | **Đã thi công**: WP-04 (FE) — 4 màn kho + **bulk · ẩn/hiện cột · lưu bộ lọc · in phiếu** + **tiền phiếu** (`UnitPrice`, migration **55**), **WP-04 ĐÓNG (BE+FE)**; probe FE **19/19** + kiểm browser thật |
| [handover-n7.md](handover-n7.md) | **Đã thi công**: WP-06 — POS (**hoá đơn bán lẻ + ca thu ngân + thanh toán đa phương thức + in + outbox offline**), module `Pos` 4 project + 6 bảng + API `/api/v1/pos` (10 path) + 4 route FE; migration **57**, **1900** test, probe **34 lời gọi HTTP** `exit 0`, browser thật **10/10** |
| [handover-n8.md](handover-n8.md) | **Đã thi công**: WP-07 — Khách (**điểm · công nợ · cấp · chăm sóc**) + **nhân viên & phạm vi dữ liệu**; **sổ khách một cửa ghi** nối POS/trả/huỷ cùng UoW, `data_scope` lọc **server-side** cả DS + chi tiết; 11 cột + 6 bảng, API `/api/v1` +18 path, 3 route FE; migration **58**, **1933** test, probe **28 lời gọi HTTP** `exit 0` |
| [handover-n9.md](handover-n9.md) | **Đã thi công**: WP-08 — **báo cáo**: read-model `Rpt*` **8 bảng** (2 fact · 1 agg · 4 chiều · mốc), job tổng hợp **theo mốc/backfill**, API `/api/v1/reports` (**5 path**: doanh thu · đơn · tồn · dashboard · aggregate), Dashboard **4 KPI** + **2 báo cáo FE** (bảng + biểu đồ SVG + CSV); migration **59**, **1961** test + **96** integration, probe **20 lời gọi HTTP** `exit 0` (doanh thu **1.470.000 = 1.245.000 + 225.000**, chạy lại **không** nhân đôi) |
| [handover-n10.md](handover-n10.md) | **Đã thi công**: WP-09 — **test & cổng**: Playwright **8 luồng** Web Admin (login · tạo SP · nhập kho · bán lẻ · in · đổi trạng thái đơn · kết ca · xuất CSV) chạy **tuần tự** trên **một ngày bán hàng liên tục**; `pnpm test` = Vitest **25 ca**; job CI **`openapi-drift`** (**51/51** path, `schema.d.ts` không đổi) + job **`e2e-playwright`** (DB **trống**); cổng `kiem-tat-ca.ps1` thêm **3 bước FE**; **8/8** E2E `passed` |
| [handover-n11.md](handover-n11.md) | **Đã thi công**: WP-10 **OPS** — `admin-web` **production** trong `docker-compose` (image **354 MB**, `output: standalone`, cổng **3000**, `.dockerignore` ở gốc) + **`GET /api/health`** (BFF → `/health-status`): **200** khi Host sống · **503** khi Host chết (container `unhealthy` **91 s** / `healthy` **10–15 s**); `.env.example` + `AUTH_ISSUER` · `NEXT_PUBLIC_APP_NAME` (build arg); probe **`tools/kiem-ops-wp10.ps1`** **exit 0**; **8/8** E2E vẫn xanh, **0 migration** |
| [handover-n12.md](handover-n12.md) | **Đã thi công**: WP-11 **LÀM CỔNG XANH** — **9 script cổng** hết khuôn chỉ-Windows (`Lay-DuongDanBin` · `Lay-PidDangNghe` · `Lay-Curl` · `Start-TienTrinhAn` · **`Start-NextAdmin`** · **`Lay-DuongDanTam`**) + **`CusCustomers`** ra khỏi nhóm "phải rỗng" → **bất biến mạnh hơn** (*mọi khách phải có chứng từ*) + xoá **1** dòng vô chủ; **`kiem-tat-ca.ps1` exit 0 (9/9 bước)** · test cổng **33/33** · DB **25/25** · **8/8** E2E · drift **51/51** · **0 migration** |
| [handover-n13.md](handover-n13.md) | **Đã thi công**: WP-12 **LÀM `-DayDu` XANH** — **`probe-oversell`** + **`probe-giu-cho-2instance`** so **SỐ** (`[decimal]::Parse` **invariant**) thay so **chuỗi** cho `decimal(18,3)` (7 ca) + sửa **lỗi in** của cổng ở bước ⑲ (`Get-Content` trả chuỗi ⇒ `$_.Line` null ⇒ NÉM dù Playwright **8/8**); **`-DayDu` exit 0 (19/19 bước, 462,5 s)** · bản nhẹ **9/9** · **0 migration** |
| [handover-n14.md](handover-n14.md) | **Đã thi công**: WP-13 **ĐÓNG 2 NỢ KIỂM CHỨNG** — **một chỗ** mở/bật cờ `ShouldChangePasswordOnNextLogin` (`Invoke-KhongCoDoiMatKhau` + cặp `Start-KhongCoDoiMatKhau`/`BatLai-CoDoiMatKhau` trong `nen-tang.ps1`), đóng luôn lỗ **4** probe tắt cờ mà **không** bật lại; **2** ca kiểm mới (`Y6` quét + `L2` DB/`exit`); `probe-kho-wp04` **exit 0 (12 ca)** · `probe-pos` **exit 0 (34 lời gọi)** · `kiem-db` **25/25** · bản nhẹ **9/9** · `-DayDu` **19/19** · **0 migration** |
| [handover-n19.md](handover-n19.md) | **Đã thi công**: **bật `cong-15-buoc` làm required check** — đo **TRƯỚC** `GET …/protection` ⇒ `["docker-build"]`; `ci` trên `main` xanh (run **`41969985282`**, **3/3** job, head **`0e5c275`**) ⇒ `PUT` **cả object** ⇒ đo **SAU** ⇒ `["docker-build", "cong-15-buoc"]`, 10 cờ còn lại **không đổi**; chứng minh hiệu lực bằng PR **#3** (`mergeable_state=blocked` + `ci` lượt đầu **ĐỎ** đúng `D-64` `14/3` ⇒ `rerun` xanh rồi mới merge); **0 migration**, **0** tệp mã; 🚨 `D-64` flaky **nay chặn merge** — **`D-65`** |
| [handover-n17.md](handover-n17.md) | **Đã thi công**: **đóng nợ CI `packageManager`** — commit **`18405ca`** (bỏ **1 dòng** `packageManager` ở `package.json` gốc + sửa comment `Dockerfile`): `abp install-libs`/`yarn` **exit 0** (10,2 s) ⇒ **bước 7 `ci` XANH lần đầu** (run **`41955331290`**, job 294 s) + **`docker-build` `6/6`** (run **`41955331294`**, 66 s); `ci` còn đỏ ở **⑦/Y1** (`37/38`, 3 probe `N12`–`N14` chưa commit là **mới so với `main`**) ⇒ **`D-61`: KHÔNG merge PR #1** (merge làm `ci` `main` đỏ + nửa FE/API 404); cổng nhẹ **9/9** · `-DayDu` **19/19** · **0 migration** |
| [handover-n18.md](handover-n18.md) | **Đã thi công**: **commit cây làm việc → `ci` XANH trên PR #1 → MERGE** — **6** commit · **160** tệp (`6ab9114` BE `WP-06…WP-08` + migration **57…59**, `4248d80` `ci.yml` + 2 job, `b94b13e` compose `admin-web`, `0bbc140` `tools/` `N12`–`N14`, `3f93c14` docs, `39df2d4` 2 fix); `ci` lượt **1** (head `3f93c14`) **ĐỎ 2 chỗ** — bước 11 link (`artifacts/`) + `e2e-playwright` (**1 failed · 7 passed**: mốc ảnh tồn kho ghi cho lượt chụp **0 dòng** trên DB trống) — **nhưng cổng nghiệp vụ 19/19 XANH**; sửa **một chỗ mỗi lỗi** + **2** ca `V4`/`V5` ⇒ `ci` lượt **2** **3/3 XANH (10,2 phút)** ⇒ **merge PR #1 = `fab0089`** (**46** commit · **610** tệp); test cổng **36/36** · `-DayDu` **19/19** · **0 migration** — **`D-62`/`D-63`** |
| [handover-n16.md](handover-n16.md) | **Đã thi công**: **đóng gói image Web Admin** — **2 commit** (`d8e22dc` cây FE **60** tệp — **bắt buộc** vì `HEAD` + **đúng 3 tệp** ⇒ `docker build` **ĐỎ** `standalone not found`, `D-58`; `6c49d3a` **3** tệp `.dockerignore` + `Dockerfile` + workflow) → **push** (4 lần thử, GitHub **5xx**) → **PR [#1](https://github.com/hungnt9801/eCommerce/pull/1)** → **`docker-build` xanh trên `pull_request`** (run **`41811821775`**, **88 s**, **6/6** bước) → **bật required check** (`D-59`: trước phiên `main` **không có** protection, `GET` **404**); ⚠️ `ci` đỏ bước 7 (`packageManager` + `yarn`) — **nợ có sẵn**, **0 migration** |
| [handover-n15.md](handover-n15.md) | **Đã thi công**: WP-13 ② — **job CI `docker-build`** (workflow **riêng**): build image Web Admin trên runner **SẠCH** + **2 bất biến** sau build (`server.js` standalone · `/login` **200** khi API chết); **đo thật** trên `ubuntu-latest` — run **`41808550753`** xanh, **76 s** (build **68 s**); lượt đo **đầu ĐỎ** vì `Dockerfile`/`.dockerignore` **chưa từng commit** (`D-57` ③); nhánh tạm **đã xoá**, `main` **không đổi**; **0 migration** |
| [handover-n6.md](handover-n6.md) | **Đã thi công**: WP-05 — đơn hàng **18 trạng thái** + `OrderType/Source/ReasonCode` + bảng cạnh Nhanh + nhãn suy từ 4 trục + API `/api/v1/orders` (**14 tab đếm**, `transitions`, `cancel`, `history`) + 2 màn FE; migration **56**, **1855** test, probe **29 lời gọi HTTP** `exit 0` |
| [08-session-protocol.md](08-session-protocol.md) | **Chia phiên**: 10 session cho GĐ0–GĐ2, mẫu kickoff/handover, **câu mở phiên kế bắt buộc — 2 biến thể (§5.1)**, khi nào dừng |
| [09-parallel-sessions.md](09-parallel-sessions.md) | **Chạy song song**: worktree, cổng/DB riêng, contract-first, luật migration |
| [baseline-2026-11-02.md](baseline-2026-11-02.md) | Baseline trước khi sửa (build/test/cổng) |
| [decisions.md](decisions.md) | Biên bản quyết định **D-01…D-65** (**D-01…D-18** chốt 02/11/2026; **D-19…D-65** phát sinh khi thi công `N2…N19`) |
| [nhanh-pos-v3-api-inventory.md](nhanh-pos-v3-api-inventory.md) | **68 endpoint + 10 webhook** Open API v3, enum, shape, câu hỏi mở |
| [nhanh-pos-module-map.md](nhanh-pos-module-map.md) | Bản đồ 12 module, 18 trạng thái đơn, 10 quy trình, 50 màn P0/P1/P2 |

### Bằng chứng đo được

| Tệp | Nội dung |
|---|---|
| [`evidence/routes-raw.json`](evidence/routes-raw.json) | **204 route** nội bộ của Nhanh (DOM thật) |
| [`evidence/menu-sidebar.json`](evidence/menu-sidebar.json) | **180 mục** menu 2 cấp (DOM thật) |
| `artifacts/nhanh-survey/product-list.png` | Ảnh DS sản phẩm (20 cột) — *không commit, `artifacts/` gitignore* |
| `artifacts/nhanh-survey/order-list.png` | Ảnh DS đơn hàng (tab badge, grouped rows) — *không commit* |
| `artifacts/nhanh-survey/bill-detail.png` | Ảnh chi tiết hóa đơn (2 cột + rail) — *không commit* |
| `artifacts/nhanh-survey/api-docs-raw/` | 77 trang tài liệu Open API đã bóc chữ — *không commit* |

---

## 7. Việc tôi làm ngay sau khi bạn duyệt [kế hoạch](07-ke-hoach-trien-khai.md)

1. Tạo nhánh `feature/wp-01-02-foundation`, chạy baseline, **ghi số** (build, 1.634+90 test, 50 migration).
2. Dựng `apps/admin-web` (Next.js + tokens + AppShell + login BFF) + `packages/api-client`.
3. Thêm project `src/eCommerce.HttpApi` + `ProductsController` (`/api/v1/products`) + OpenAPI → TS.
4. Màn DS sản phẩm đọc **API thật** (108 SP hiện có trong `businessId=230222`).
5. Bắt đầu WP-03: `ProductUnit` + migration 51 (additive) + backfill `--dry-run`.

**10 ngày** phải có một đường dọc chạy được: **DB → API → UI**, không mock.

---

> ⚠️ **Nói thẳng ba điều:**
> 1. Đây là **đề xuất**, chưa phải cam kết thời gian — con số ở [05](05-lo-trinh-rui-ro.md) phụ thuộc
>    đội và 6 quyết định trên.
> 2. Phần khảo sát Nhanh làm bằng **tài khoản của bạn** + cầu CDP trong repo; **chưa cài REA**
>    vì Node 24.19 < 24.22.1. Cách nâng & dùng ở [06](06-phuong-phap-khao-sat-rea.md).
> 3. Toàn bộ số liệu (204 route, 180 menu, 20 cột, 18 trạng thái) **đo được và có file**, không suy đoán
>    từ ảnh chụp.
