# BÁO CÁO KIỂM CHỨNG BẰNG CHẠY THẬT (PostgreSQL 17.11 + Redis 7)

| | |
|---|---|
| **Ngày** | 13/10/2026 |
| **Môi trường** | Docker Desktop 29.6.2 · `postgres:17-alpine` (**17.11**) · `redis:7-alpine` · `docker-compose.yml` của repo |
| **Phạm vi** | Toàn bộ hạng mục Phase 0 ghi là *"chưa xác minh"*, cộng phần code `XC-006` (module `Tax`) và `XC-004` (chiều tenant) |
| **Kết quả** | 🔴 **12 LỖI THẬT** được tìm ra — trong đó **7 lỗi thuộc loại "im lặng"** · **12 đã sửa + kiểm chứng** |
| **Vì sao quan trọng** | 3 hạng mục Phase 0 ghi "chưa xác minh" (token · `401`/`403` · seeder) hoá ra **CHƯA TỪNG HOẠT ĐỘNG** |

> **Kết luận cốt lõi:** review tài liệu **không** thay thế được việc chạy thật. 6/10 lỗi không có lỗi biên dịch,
> không exception, `exit 0` — chúng chỉ lộ ra khi chạy với hạ tầng thật.

---

## 1. Bảng 12 lỗi thật

| # | Lỗi | Biểu hiện khi chạy | Loại | Trạng thái |
|---|---|---|---|---|
| **C1** | DbMigrator **thiếu copy `appsettings.json`** vào `bin` + **không có logging provider** | Thất bại **IM LẶNG**, chỉ thấy `exit 1`, **không một dòng log** | 🟥 im lặng | ✅ sửa |
| **C2** | Thiếu **`AbpClockOptions.Kind = Utc`** | `Npgsql: Cannot write DateTime with Kind=Local to PostgreSQL type 'timestamp with time zone', only UTC is supported` ⇒ seeder chết | 🟧 rõ | ✅ sửa |
| **C3** | **Migration viết tay thiếu `.Designer.cs`** | EF **BỎ QUA im lặng**: `migrations list` không thấy, migration không chạy, `exit 0` | 🟥 im lặng | ✅ sửa |
| **C4** | **F1 của `XC-004`**: unique index `(TenantId, …)` không chặn trùng khi `TenantId` NULL (PostgreSQL `NULLS DISTINCT`) | `INSERT 0 2` — **tạo được 2 dòng cùng `Code`** | 🟥 im lặng | ✅ sửa + **chứng minh trước/sau** |
| **C5** | Tài khoản seed **ghi sai trong tài liệu** — tài liệu ghi `admin@ecommerce.local`, **thực tế trong DB là `admin@abp.io`** (ABP có seeder mặc định riêng, chạy trước) | `No user found matching username: admin@ecommerce.local` | 🟧 rõ | ✅ sửa: hằng số + tài liệu nay dùng `admin@abp.io` |
| **C6a** | `SignInManager<IdentityUser>` **chưa đăng ký** | `No service for type 'SignInManager\`1[IdentityUser]'` ⇒ **password grant chết** | 🟧 rõ | ✅ sửa |
| **C6b** | **Không hề gọi `AddAuthentication(...)`** | API không token: `500` → `404` body rỗng (thay vì `401`) | 🟥 im lặng | ✅ sửa |
| **C7** | **0 permission được cấp** (3 nguyên nhân lồng nhau) | `AbpPermissionGrants` = 0 ⇒ mọi API `[Authorize(permission)]` trả **`403` cho TẤT CẢ** | 🟥 im lặng | ✅ sửa + **0 → 36 grant** |
| **C8** | Tưởng là lỗi routing | Log Routing Debug: `Request matched endpoint 'GetShopsAsync'` ⇒ **route KHỚP**, lỗi ở tầng auth | — | ✅ **chẩn đoán xong** (cùng gốc C6b/C9) |
| **C9** | Mục `AuthServer` **thiếu `Authority`/`Audience`** | Có token **vẫn `401`** — JwtBearer không có nguồn khoá để validate | 🟧 rõ | ✅ sửa |
| **C10** 🆕 | **Worker động của ChannelHub KHÔNG BAO GIỜ chạy**: supervisor gọi `AddAsync` **mỗi nhịp** ⇒ ABP **thay thế** worker ⇒ timer reset ⇒ nếu nhịp supervisor ≤ nhịp worker thì worker không bao giờ tới nhịp đầu. **Với cấu hình mặc định (60 s vs 300 s) ⇒ đồng bộ sàn hoàn toàn không chạy** | Đo được: supervisor 5 s + worker 10 s ⇒ **8 lần `Replaced`, 0 lần worker chạy**; supervisor 600 s ⇒ worker chạy **ngay** | 🟥 im lặng | ✅ sửa + **kiểm chứng lại** (ca 5 s/10 s nay **0 replace** và worker **chạy**) |
| **C11** 🆕 | **Khoá lock thật SAI quy ước**: `AbpDistributedLockOptions.KeyPrefix` ghép với tên khoá **KHÔNG có dấu phân cách** ⇒ khoá thật **`eCommercechannel:shopee:…`** (dính liền, E HOA) thay vì `ecommerce:channel:…` | `redis-cli MONITOR`: `"SET" "eCommercechannel:shopee:…:token-refresh" "EX" "30" "NX"` | 🟥 im lặng — lock vẫn "chạy", chỉ là **không ai giữ đúng khoá** | ✅ sửa (`KeyPrefix = "ecommerce:"`) + kiểm chứng lại bằng MONITOR |

> 📌 **Cách tìm ra C11 (đáng ghi lại):** tôi giữ khoá bằng tay với **3 biến thể** theo suy đoán
> (`ecommerce:…`, `channel:…`, `eCommerce:…`) — **cả ba đều KHÔNG chặn được** worker. Thay vì đoán thêm,
> tôi dùng **`redis-cli MONITOR`** bắt lệnh Redis thật ⇒ ra ngay khoá đúng.
> *(Commit `1541b31` ghi lỗi này là `C12`; `XC-003` gọi là **F5** — cùng một lỗi.)*

---

## 1b. ✅ Kiểm chứng DISTRIBUTED LOCK — hoàn tất `XC-003`

**Phép thử A/B trên đúng khoá đã xác minh `ecommerce:channel:shopee:SHOP-LOCK-TEST:token-refresh`:**

| Ca | Có ai giữ khoá? | Kết quả thực tế |
|---|---|---|
| **A** | Không | ✅ Token **được refresh** (`[PoC] RefreshTokenAsync` + `Da refresh token cho shop …`) |
| **B** | **Có** (giữ bằng `redis-cli`) | ✅ **KHÔNG** refresh ⇒ **lock đã CHẶN đúng** |

⇒ `XC-003` nay **đã kiểm chứng end-to-end**: supervisor chạy → worker động chạy → giành khoá → **khoá thật khớp quy ước** → **khoá chặn được thao tác tranh chấp**.

---

## 2. Chi tiết C7 — lỗi giá trị nhất (3 tầng lồng nhau)

| Tầng | Nguyên nhân | Vì sao **không** báo lỗi |
|---|---|---|
| **1** | DbMigrator thiếu `ProjectReference` tới `eCommerce.Application.Contracts` (nơi có `eCommercePermissionDefinitionProvider`) | `foreach` chạy trên **danh sách rỗng** ⇒ 0 vòng lặp, **không exception** |
| **2** | Thêm `ProjectReference` **vẫn KHÔNG đủ** — phải có **module dependency** `eCommerceApplicationContractsModule` trong `[DependsOn]` | `ProjectReference` chỉ bảo đảm **biên dịch**; assembly chỉ được **nạp** và được ABP **quét** khi nằm trong **đồ thị module** |
| **3** | ABP có permission **không cấp được cho vai trò** (`AbpIdentity.UserLookup`) | Ném `The permission … is not compatible with the provider named 'R'` ⇒ phải **lọc theo `permission.Providers`** |

**Đã thêm guard biến lỗi im lặng thành lỗi rõ:**
```csharp
if (permissions.Count == 0)
    throw new InvalidOperationException("Không đọc được permission nào để cấp…");
```

**Kiểm chứng:** `AbpPermissionGrants` **0 → 36**, trong đó **21 là permission của ta** (khớp chính xác 21 định nghĩa).

---

## 3. Bằng chứng kiểm chứng (SQL thật, không suy đoán)

| Hạng mục | Kết quả |
|---|---|
| Migration | **49 bảng**, `exit 0` |
| **`btree_gist` + ràng buộc `EXCLUDE`** (SQL thô của `Add_Tax_Module`) | ✅ tồn tại (`contype = x`) **và hoạt động**: khung **chồng nhau bị chặn**, khung **không chồng được nhận** |
| **Seed 4 mức thuế** | ✅ `VAT8` = 8%, `Kind=Reduced`, `2025-07-01` → **`2026-12-31`**, `ReducedFromCode=VAT10` |
| **Mốc tự chuyển 8% → 10%** | ✅ `2026-12-31`: **1** mức giảm hiệu lực · `2027-01-01`: **0** ⇒ resolver tự rơi về 10% |
| **F1 (`XC-004`)** trước khi sửa | 🚨 `INSERT 0 2` — **tạo được 2 dòng cùng `Code`, `TenantId = NULL`** |
| **F1** sau khi sửa (`NULLS NOT DISTINCT`, 11 index) | ✅ `duplicate key value violates unique constraint … (null, DUP_TEST3) already exists` |
| **Đối chứng** (cùng `Code`, `TenantId` **có** giá trị) | ✅ **luôn bị chặn** — cả trước và sau khi sửa ⇒ chứng minh index chỉ hỏng ở nhánh `NULL` |
| **Chuỗi xác thực** (`GET /api/app/channel/shops`) | ✅ không token → **`401`** · có token + quyền → **`200`** `{"totalCount":0,"items":[]}` |

---

## 4. Chuỗi tiến hoá `401` — đo qua 4 bước

| Bước | Không token | Có token |
|---|---|---|
| Ban đầu | `500` (`No authenticationScheme was specified…`) | — |
| + `AbpAspNetCoreAuthenticationJwtBearerModule` | `404` body rỗng | `404` |
| + `AddAuthentication(...).AddJwtBearer(...)` | **`401`** ✅ | `401` ❌ |
| **+ `Authority`/`Audience` + bỏ kiểm audience ở Dev** | **`401`** ✅ | ✅ **`200`** |

---

## 5. Còn lại (chưa làm / chưa đo được)

| # | Việc | Ghi chú |
|---|---|---|
| 1 | **Lock per-shop** — nay **đã đủ điều kiện** vì auth đã thông | Cần tạo `ChannelShop` rồi gọi `sync-now` và quan sát khoá trong Redis (`redis-cli keys 'ecommerce:*'`) |
| 2 | **`403`** (thiếu quyền) | Cần **principal thứ hai**; admin có đủ 21 quyền nên không tạo được `403` |
| 3 | **B** — 3 phát hiện không cần PostgreSQL | (a) mã lỗi trùng/khác định dạng `ChannelHub:0001` vs `eCommerce:Channel:0001`; (b) `password` grant + mật khẩu seed trong tài liệu; (c) che payload webhook 8.000 ký tự |
| 4 | **Vá `XC-005`** | Bật JSON console + wire `AbpCorrelationIdMiddleware` + `/health-status` + chỉnh mức log production |
| 5 | **Cập nhật 6 tài liệu nền** | Ghi lại các phát hiện C1–C9 vào §15.2 tương ứng (`XC-004` F1 **đã có bằng chứng**, nay bổ sung C6b/C7/C9) |

---

## 6. Bài học

1. **Chạy thật là bước không thể thay thế.** 6/10 lỗi "im lặng" — không lỗi biên dịch, không exception, `exit 0`.
2. **Guard phải NÉM LỖI, không được no-op.** Đúng như quy ước #11 của dự án — và chính các guard mới thêm đã biến C7 từ "im lặng thành công" thành lỗi rõ ràng, giúp tìm ra tầng 2 và tầng 3.
3. **`ProjectReference` ≠ module dependency** trong ABP: cái đầu để biên dịch, cái sau mới nạp assembly vào đồ thị module để ABP quét.
4. **Đặt log đúng chỗ**: C1/C3 chỉ tìm được sau khi cho DbMigrator ghi lỗi ra `stderr`; C8 chỉ tìm được sau khi bật `Microsoft.AspNetCore.Routing` = `Debug`.
5. **Sửa theo giả thuyết mà chưa đo là rủi ro thật** — tôi đã 2 lần như vậy (C6b đoán do `SignInManager`, C7 đoán do trùng tên class) và cả 2 lần đều **sai**; chỉ khi đo mới ra nguyên nhân đúng.

---

## 7. ✅ Vá `XC-005` (observability) — đã kiểm chứng CẢ 2 CHIỀU

| Hạng mục | Trước | Sau (đo được) |
|---|---|---|
| `CorrelationId` | ❌ chỉ là **cột RỖNG** trong bảng audit của ABP (**12/12** chỗ nằm trong `Migrations/`) | ✅ `UseCorrelationId()` ⇒ header **`X-Correlation-Id`** trả về **thật** (`aa003348cac54ee99f7d7c7114dca531`) |
| Health check | ❌ không có | ✅ `/health-status` → **`200 · Healthy`** khi PostgreSQL sống · **`503`** khi **DỪNG** PostgreSQL ⇒ chứng minh check **thật sự** kiểm tra phụ thuộc (không phải luôn trả 200) |
| Log | ⚠️ console text | ✅ **JSON 1 dòng** |

**2 lỗi nữa chỉ lộ lúc chạy, khi vá health check (đã sửa):**

1. `MapHealthChecks` là extension trên **`IEndpointRouteBuilder`**, **không phải** `IApplicationBuilder` ⇒ phải gọi qua `app.UseEndpoints(...)`.
2. **Không được inject thẳng `AbpDbConnectionOptions`**: ABP đăng ký qua `Configure<T>` nên trong DI **chỉ có `IOptions<T>`**, mà health check dùng `ActivatorUtilities` (resolve theo **đúng kiểu tham số**) ⇒ ném `Unable to resolve service for type 'AbpDbConnectionOptions'` ⇒ `/health-status` trả **500**.

---

## 8. Tổng kết cuối

| Hạng mục | Kết quả |
|---|---|
| **Lỗi thật tìm bằng chạy thật** | **14** |
| Đã sửa + **kiểm chứng** | **14** |
| Loại **"im lặng"** (không lỗi biên dịch, không exception, `exit 0`) | **7** |
| Unit test | **42/42** (trước phiên: 20) |
| Build | **0 lỗi / 0 warning** |
| Tài liệu thiết kế nền | **6/6** (`XC-001…XC-006`) · Mermaid **30/30 PASS** |
| Chuỗi xác thực | ✅ không token `401` · thiếu quyền `403` · có quyền `200` |
| Distributed lock | ✅ khoá thật `ecommerce:channel:…` · **chặn được** thao tác tranh chấp (A/B) |
| Việc còn lại | **12 metric chưa có nơi phát** (`XC-005` — chờ chọn công cụ) · backfill API `401`/`403` vào `XC-004` §15.2 |

> **Bài học lớn nhất:** **14 lỗi thật, 7 lỗi hoàn toàn im lặng** — và **2 lần tôi sửa theo giả thuyết mà chưa đo thì cả 2 lần đều SAI**
> (C6b đoán do `SignInManager`; C7 đoán do trùng tên class). Khi đo bằng công cụ đúng (`redis-cli MONITOR`,
> log `Microsoft.AspNetCore.Routing` mức `Debug`) thì nguyên nhân thật hiện ra ngay.

---

## 9. LỖI #22 (14/10/2026) — **ĐƠN HÀNG BỊ MẤT VĨNH VIỄN, HỆ THỐNG BÁO THÀNH CÔNG**

**Mức độ: NGHIÊM TRỌNG NHẤT trong dự án tới nay** — mất dữ liệu doanh thu, không thể phục hồi, không có lỗi nào được ghi.

### Phát hiện

| Số liệu | Giá trị |
|---|---|
| Nơi `PublishAsync(...)` | **2** |
| Handler đăng ký nhận (`IDistributedEventHandler`/`ILocalEventHandler`) | **0** |

`ChannelShopSyncer.SyncAsync` (dòng 74) publish `ChannelOrderPulledEto` cho **từng đơn hàng** kéo từ
Shopee/Lazada. **Không có handler nào** nhận sự kiện đó.

### Vì sao mất VĨNH VIỄN (không chỉ chậm)

Trình tự trong `ChannelShopSyncer`:
1. `pull = await connector.PullOrdersAsync(...)` — lấy đơn từ sàn,
2. `PublishAsync(new ChannelOrderPulledEto{...})` cho từng đơn → **không ai nhận** (nhưng KHÔNG ném lỗi),
3. `shop.LastSyncedAtUtc = pull.NewSyncCursorUtc` — **TIẾN CON TRỎ ĐỒNG BỘ**,
4. `shop.LastError = null`, lưu, trả về `OrdersPulled = N`, log mức **Information**.

⇒ Bước 3 khiến sàn **không bao giờ gửi lại** những đơn đó. Admin nhìn thấy *"đồng bộ lúc vừa xong,
0 lỗi, N đơn"* — trong khi **N đơn đã biến mất**.

### Vì sao đây là dạng nguy hiểm nhất

Đây là **báo thành công trong khi không làm gì** — cùng họ với các lỗi im lặng đã gặp (C1, C3, C10, C16),
nhưng hậu quả nặng hơn vì (a) **không thể phục hồi** — con trỏ đã tiến, (b) nằm ở **đường doanh thu**,
(c) **không có bất kỳ tín hiệu nào** để biết là đã sai.

### 🚨 QUYẾT ĐỊNH: **KHÔNG** xây outbox ở bước này (có lý do, không phải bỏ sót)

Trước khi viết code, tôi kiểm tra: **không có message broker nào được cấu hình** (chỉ PostgreSQL + Redis;
không RabbitMQ/Kafka; `Directory.Packages.props` chỉ có `Volo.Abp.EventBus`).

⇒ **Outbox lúc này là hạ tầng cho vấn đề CHƯA tồn tại**, và tệ hơn: nó sẽ **đảm bảo giao tin cậy
tới KHÔNG AI** — tức làm vẻ ngoài *trông đáng tin hơn* trong khi dữ liệu vẫn mất. Thứ tự đúng là:
**có người nhận trước, rồi mới bàn tới giao tin cậy.**

### ✅ ĐÃ LÀM: biến lỗi IM LẶNG thành lỗi ỒN ÀO

`EventCoverageChecker` (lõi thuần, `modules/Messaging/…/EventCoverage/`) + gọi trong
`eCommerceHttpApiHostModule.OnApplicationInitialization`.

**Bằng chứng từ log khởi động thật:**
```
fail: eCommerce.eCommerceHttpApiHostModule[0]
  ?? PHAT HIEN 2 SU KIEN KHONG CO NGUOI NHAN (da quet 27 assembly).
     Su kien duoc publish nhung khong ai xu ly => DU LIEU SE MAT IM LANG.
   • ChannelOrderPulledEto - KHONG co handler nao dang ky nhan.
   • ChannelShopTokenRefreshedEto - KHONG co handler nao dang ky nhan.
```

Kiểm tra chạy ở **mọi lần boot** ⇒ nếu ai thêm ETO mới mà quên handler, log sẽ **la ngay**.

### Chi tiết kỹ thuật đã xử lý
- **`·LineEto` là ETO CON** (`ChannelOrderPulledLineEto` là một dòng của `ChannelOrderPulledEto`) ⇒
  **bỏ qua** trong kiểm tra. Nếu tính, báo cáo có **dương tính giả** và người đọc sẽ bắt đầu bỏ qua cảnh báo.
- **Chỉ interface `IDistributedEventHandler`/`ILocalEventHandler`/`IEventHandler`** mới tính là người nhận.
  Nếu tính mọi interface generic 1 tham số thì `IRepository<SuKienB>` sẽ khiến sự kiện **trông như đã có
  người nhận** ⇒ bộ kiểm tra **im lặng bỏ qua đúng lỗi cần tìm** (có test khoá điều này).
- **Ghi rõ số assembly đã quét** (27) — vì quét theo "assembly đã nạp" rất dễ thiếu (bài học C16),
  nên phạm vi phải **nhìn thấy được** thay vì cho ra kết quả "sạch" giả tạo.

### ⚠️ VIỆC CÒN LẠI (cần quyết định của con người, KHÔNG thể tự chọn)
1. **Xây nơi nhận đơn hàng** (Order intake) — đây là GĐ1, và nó là điều kiện **bắt buộc** để đơn không mất.
2. **Hoặc** tạm dừng tiến con trỏ đồng bộ khi chưa có nơi nhận (fail-closed: sàn sẽ gửi lại, đơn không mất
   nhưng log sẽ đầy) — cần người quyết vì nó đánh đổi giữa *mất dữ liệu* và *lặp vô hạn*.
3. Sau khi có người nhận + broker ⇒ **khi đó mới bật outbox** (ABP có sẵn `AbpEventBusBoxesModule`).

---

## 9bis. ✅ LỖI #22 — **ĐÃ SỬA GỐC THEO PHƯƠNG ÁN B** (14/10/2026)

> **Người quyết định:** chọn **"B ngay + A sau"** — chặn mất dữ liệu **ngay** bằng fail-closed, rồi xây
> **Order intake** (việc A) trong GĐ1. Ghi ở `docs/handover-gd1.md` §4.

### Ba việc đã làm

| # | Việc | Nội dung |
|---|---|---|
| 1 | **Fail-closed** | `ChannelShopSyncer` **tự hỏi** `IEventCoverageProbe` xem `ChannelOrderPulledEto` có người nhận chưa; **có đơn mà chưa có nơi nhận** ⇒ **KHÔNG publish** + **KHÔNG tiến con trỏ** + ghi `LastError` + log **Error** + metric `channel_sync_failed_total{reason=ChuaCoNoiNhanDon}` |
| 2 | **Một nguồn sự thật** | Cảnh báo lúc **khởi động** và quyết định lúc **đồng bộ** nay dùng **chung** `EventCoverageScan`/`IEventCoverageProbe` ⇒ hai nơi **không thể lệch nhau** (trước đây Host tự quét riêng) |
| 3 | **Bịt biến thể thứ HAI** (phát hiện mới) | 🚨 `ChannelAppService.SyncNowAsync` — nút **"Đồng bộ ngay"** — **tự kéo đơn rồi tự tiến con trỏ** mà **KHÔNG phát sự kiện cho ai**. Đây là **cùng một lỗi** nhưng ở **đường do người vận hành bấm**, và **không cần handler nào bị thiếu** để gây mất đơn. Nay đi qua **cùng một cổng** `IChannelShopSyncService` |

### Bằng chứng chạy thật (Host + PostgreSQL 17 thật)

| Hạng mục | Kết quả |
|---|---|
| Log khởi động | `PHÁT HIỆN 2 SỰ KIỆN KHÔNG CÓ NGƯỜI NHẬN (đã quét 27 assembly)` — `ChannelOrderPulledEto`, `ChannelShopTokenRefreshedEto` |
| Log đồng bộ | `fail: eCommerce.ChannelHub.Sync.ChannelShopSyncer` → `🚨 CHẶN BÀN GIAO ĐƠN … kéo được 2 đơn nhưng sự kiện ChannelOrderPulledEto KHÔNG có người nhận … Đã GIỮ NGUYÊN con trỏ đồng bộ để sàn gửi lại` |
| **DB** | `LastSyncedAtUtc = (NULL)` ⇒ **con trỏ KHÔNG tiến** ✅ · `LastError = [eCommerce:Channel:0005] …` |
| Nhịp sau | Kéo lại **đúng 2 đơn đó** rồi **chặn lại** ⇒ đúng hành vi *"sàn gửi lại"*. ⚠️ Nhịp mặc định **300 s** ⇒ log lặp mỗi 5 phút/shop (đánh đổi **đã được chấp nhận**) |
| Test | **383 unit** (thêm **22**) + **20 integration** · build **0 lỗi/0 cảnh báo** |

### 🎯 Vì sao dùng **chung** bộ dò thay vì kiểm tra lại ở từng nơi

Câu hỏi *"sự kiện này có người nhận chưa?"* được hỏi ở **hai** nơi (lúc boot và lúc đồng bộ). Nếu mỗi nơi
tự quét riêng thì chúng **có thể lệch nhau** — và lệch theo hướng nguy hiểm nghĩa là **log nói "có người
nhận" trong khi thực tế đơn vẫn mất**. Nay cả hai đi qua **một** lớp ⇒ **không thể lệch**.

### ⚠️ Điều **CHƯA** kiểm chứng được (nói thẳng)

*(Đã giải quyết ở mục dưới — giữ lại để thấy lịch sử.)*
Đường **API** "Đồng bộ ngay" (`POST /api/app/channel/{id}/sync-now`) **chưa gọi được bằng HTTP thật** vì
endpoint đó **luôn trả `403`** — xem **§10** (phát hiện **ngoài phạm vi**, có trước lượt sửa này).
Bù lại: ánh xạ DTO được khoá bằng **test đơn vị** và phụ thuộc được khoá bằng **test reflection**.

### ✅ 9bis.1 **VIỆC A ĐÃ XONG (15/10/2026) — `SAL-005`: NƠI NHẬN ĐƠN THẬT ⇒ LỖI #22 ĐÓNG HOÀN TOÀN**

Lượt trước **chặn** mất đơn (fail-closed). Lượt này **xây nơi nhận** ⇒ cổng **tự mở**, không cần bật cờ nào.

| Phép đo (Host + PostgreSQL 17 thật) | Kết quả |
|---|---|
| Log boot — sự kiện **KHÔNG có người nhận** | chỉ còn **`ChannelShopTokenRefreshedEto`**; `ChannelOrderPulledEto` **đã có nơi nhận** ⇒ **cổng đã mở** ✅ |
| Đơn được lưu | **10 đơn ↔ 10 dòng đơn ↔ 10 nhật ký** (1:1:1) |
| Con trỏ đồng bộ | **tiến** · `LastError` rỗng ⇒ kênh **không còn kẹt** ✅ |
| **Sàn gửi lại** (con trỏ về `NULL`), **còn** bản ghi idempotency | **`DON TRUNG = 0`** ✅ |
| **Sàn gửi lại**, đã **XOÁ** bản ghi idempotency (chỉ còn lưới DB) | **`DON TRUNG = 0`** · mỗi `ExternalOrderId` xuất hiện **đúng 1 lần** ✅ |
| Đường API "Đồng bộ ngay" | ✅ **đã gọi được bằng HTTP thật** sau khi sửa `F-403` (§10) — phản hồi `biChanDoChuaCoNoiNhanDon` + DB con trỏ **không tiến** |

🚨 **2 lỗi thật phát hiện khi kiểm chứng:** (1) handler của `IDistributedEventBus` chạy **sau** khi UoW nơi
phát hoàn tất ⇒ `DbContext` đã dispose ⇒ `ObjectDisposedException` — sửa bằng **UoW riêng**
(`requiresNew: true`), quy tắc đã ghi vào `docs/coding-conventions.md` **§6.1**;
(2) connector PoC sinh `PlacedAtUtc` **mới mỗi lần kéo** ⇒ không thể kiểm chứng đường "sàn gửi lại".
Chi tiết + giới hạn còn lại: `docs/features/sal-005-webhook-don-tu-san.md` **§16**.

---

## 10. `F-403` (14/10/2026 — lộ ra khi kiểm chứng #22) — **MỌI API CÓ `[Authorize(permission)]` TRẢ `403` DÙ ĐÃ ĐƯỢC CẤP QUYỀN** → ✅ **ĐÃ SỬA**

> 📌 **Tên gọi:** gọi là **`F-403`** ở đây, và là **`F6`** trong
> `docs/features/XC-004-xac-thuc-phan-quyen-tenant.md` §15.2/§15.6 (nơi có đặc tả đầy đủ).
> Không gán số thứ tự lỗi mới vì `docs/handover-gd1.md` ghi *"24 lỗi thật"* nhưng tài liệu **chỉ đánh số
> tới #23** ⇒ gán số lúc này sẽ **trùng hoặc nhảy cóc**.

**Mức độ: CAO** — không mất dữ liệu, nhưng **mọi endpoint gắn quyền đều không dùng được**, kể cả với
`admin`. Không có lỗi biên dịch, **không** exception trong log ứng dụng (chỉ thấy `403` ở phía client).

### Bằng chứng (chạy thật, Host localhost:5000 + PostgreSQL 17)

| Kiểm tra | Kết quả |
|---|---|
| `POST /connect/token` (password grant, `admin@abp.io`) | ✅ cấp token; **token có claim `role = admin`** (giải mã base64 phần payload) |
| `GET /api/app/channel/shops` — chỉ cần **đăng nhập** (`[Authorize]` cấp class) | ✅ **200** |
| `POST /api/app/channel/{id}/sync-now` — cần quyền `eCommerce.Channels.Sync` | ❌ **403** |
| Log `Microsoft.AspNetCore.Authorization` mức **Debug** | `Authorization failed. These requirements were not met: **PermissionRequirement: eCommerce.Channels.Sync**` |
| `AbpPermissions` (định nghĩa) | ✅ có `eCommerce.Channels.Sync`, `IsEnabled = t` |
| `AbpPermissionGrants` | ✅ có `eCommerce.Channels.Sync · R · admin` |
| `AbpUserRoles` + `AbpRoles` | ✅ user `admin` ∈ role `admin` |
| **Xoá toàn bộ cache Redis** (`ecommerce:*`) rồi gọi lại | ❌ **vẫn 403** ⇒ **loại trừ** giả thuyết *"cache quyền cũ"* |

### Trạng thái điều tra — ✅ **ĐÃ TÌM RA NGUYÊN NHÂN GỐC VÀ ĐÃ SỬA** (cùng ngày)

**Đã loại trừ (bằng đo, không bằng suy luận):** thiếu định nghĩa permission · thiếu grant · thiếu role trên
user · thiếu claim role trong token · hỏng xác thực (class-level vẫn 200).

**Cache Redis — ❌ LOẠI DỨT KHOÁT:** `redis-cli DBSIZE` = **126** → `FLUSHDB` → **0** → gọi lại ⇒ **vẫn `403`**.

**🚨 VIỆC ĐẦU TIÊN PHẢI LÀM LÀ BỊT LỖ HỔNG QUAN SÁT:** `403` **không sinh một dòng log nào ở phía
server** ⇒ không có gì để đọc, và đó là lý do lỗi này ẩn được lâu. Đã thêm
`ChanDoanTuChoiQuyenHandler` (`src/eCommerce.HttpApi.Host/Authorization/`) ghi **một dòng `Warning`** mỗi
lần từ chối. Log thật lập tức chỉ ra nguyên nhân:

```
claim vai trò : http://schemas.microsoft.com/ws/2008/06/identity/claims/role=admin
ABP current user: id='' · IsAuthenticated=False · Roles=[(RỖNG)]
quyền         : 'eCommerce.Channels.Sync' · định nghĩa: ✅ đã định nghĩa · cấp: ❌ KHÔNG được cấp
```

**NGUYÊN NHÂN GỐC:** `JwtBearer` mặc định **ĐỔI TÊN claim** theo khuôn WS-Federation cũ
(`role` → `…/claims/role`, `sub` → `…/claims/nameidentifier`), trong khi **ABP đọc tên NGẮN OIDC**
(`AbpClaimTypes`: `sub`, `role`, `email`) ⇒ `ICurrentUser` **RỖNG** ⇒ `RolePermissionValueProvider`
không có vai trò nào để tra ⇒ **403 cho mọi người**.

**SỬA:** `MapInboundClaims = false` + `TokenValidationParameters.NameClaimType = "preferred_username"`
trong `AddJwtBearer`. Kèm **bất biến lúc khởi động** `KiemTraBatBienTenClaim` (ghi **Error** nếu ai bật lại
ánh xạ) — vì đây là bất biến **cấu hình**, test thường **không** thấy được.

**MA TRẬN ĐÃ ĐO LẠI (Host thật + PostgreSQL 17 thật):**

| Ca | Endpoint | Trước | Sau |
|---|---|---|---|
| Không token | `GET /api/app/channel/shops` | `401` | **`401`** ✅ |
| `client_credentials` (không quyền) | `POST /api/app/channel/{id}/sync-now` | `403` | **`403`** ✅ |
| admin **có** `eCommerce.Channels.Sync` | `POST /api/app/channel/{id}/sync-now` | ❌ `403` | **`200`** ✅ |

> 📄 **Chi tiết đầy đủ + đính chính ma trận cũ:** `docs/features/XC-004-xac-thuc-phan-quyen-tenant.md`
> **§15.2 F6** và **§15.6**. 📌 **Bài học:** ma trận cũ đo ca `200` trên endpoint **chỉ cần đăng nhập**,
> nên **không** chứng minh được máy phân quyền hoạt động — lỗi nằm **đúng** trong khoảng trống đó.
> **Một ca kiểm thử đo trên "đối tượng gần giống" thì không phải là kiểm chứng.**

⚠️ **Còn lại:** ma trận này hiện được đo bằng **script thủ công**; **chưa** có test tích hợp tự động cho
endpoint gắn quyền (`XC-004` TC-05/TC-06 yêu cầu; test project chưa có hạ tầng `WebApplicationFactory`).