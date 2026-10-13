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
