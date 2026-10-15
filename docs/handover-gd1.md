# BÀN GIAO — ĐỌC FILE NÀY TRƯỚC KHI LÀM GÌ

> **Mục đích:** session mới **KHÔNG nhớ** cuộc trò chuyện trước. File này là **bộ nhớ bền vững**:
> trạng thái hiện tại · **quy trình kiểm chứng đã trả giá** · việc tiếp theo · các quyết định đang mở.
> **Ngày bàn giao:** 14/10/2026 · **Repo:** `D:\Projects\Tools\eCommerce`

---

## 1. TRẠNG THÁI HIỆN TẠI (số liệu đã kiểm chứng)

| | |
|---|---|
| Commit | **82** · git **sạch** · nhánh `main` |
| Build | **0 lỗi / 0 cảnh báo** — đo bằng `dotnet build eCommerce.slnx -v q --no-incremental` |
| Test | **361 unit + 20 integration** (PostgreSQL 17 thật qua Testcontainers) |
| Tài liệu | **52/52** feature doc + **báo cáo chốt GĐ0** · Mermaid **197 khối / 0 lỗi** |
| API | **35 route** thật · ma trận `401`/`403`/`200` đúng |
| DB | **19** index `NULLS NOT DISTINCT` · 7 migration |
| Background worker | **5**, đã xác nhận **chạy thật** (không chỉ "đã khởi động") |
| Lỗi thật đã sửa | **24** — trong đó **16 lỗi IM LẶNG** |
| Điểm phát metric | **7/12** |

> 📌 **CẬP NHẬT 15/10/2026 (lượt GĐ1 thứ 6 — `CAT-001` xong domain+API+ETO):** các số ở bảng trên là
> **mốc bàn giao**. Sau các lượt GĐ1: **Test = 694 unit + 38 integration** · **2 tiến trình**
> (Host **5000** API/auth · Web **5100** UI) · build **0 lỗi/0 cảnh báo** · **lỗi #22 đã sửa** (fail-closed
> **và** nơi nhận thật) · **`F-403` đã sửa** · **§7.0 xong #2/#5/#7** · **DB: 20 index `NULLS NOT DISTINCT`**
> · **`CAT-001` xong** (domain + 11 route API + 2 ETO) · 🚨 **log khởi động nay 0 Error** (phân loại
> *ETO mang dữ liệu* vs *ETO thông báo* — `coding-conventions.md` **§6.2**) · **mã lỗi Catalog đã chốt
> sổ đăng ký** (`CAT-001` **§19**) · **`CAT-002` lát cắt 1: CÂY DANH MỤC XONG** (entity + `CategoryTreeService` chống chu trình + hàng rào DB · **lát cắt 2: API cây danh mục** + **lát cắt 3: liên kết sản phẩm↔danh mục** + **lát cắt 4: THUỘC TÍNH + SINH TỔ HỢP BIẾN THỂ** + **lát cắt 5: BR-012 (biến thể mồ côi) + guard `0023`** XONG (kiểm chứng HTTP thật); `cat-002-…md` **§16–§20**) · ✅ **`PRC-001` lát cắt 1+2 XONG** (module `Pricing` + giá cơ sở **4 cấp** hàm thuần + **API bảng giá & bộ tra giá** + **BR-007 (một bảng giá Active/phạm vi)** + **cấp 4 giá niêm yết & hết N+1 (đo được: 3 lệnh EF/giỏ)**, kiểm chứng HTTP thật; `prc-001-…md` **§16–§19**) · 🟡 **`PRC-002` lát cắt 1 XONG** (bậc thang: entity + hàm chọn bậc + hàng rào DB; ⚠️ **chưa nối vào bộ tra giá**; **lát cắt 2: tính tiền đơn B6–B8 + 4 điểm làm tròn R1–R4** cũng XONG; **lát cắt 3: LẮP PIPELINE B1→B8 — bậc thang ĐÃ có tác dụng lên giá bán** (kiểm chứng HTTP 9 phép đo) + **lát cắt 4: API bậc thang + đóng lỗ hổng `ChannelShopId`** (9 phép đo) + **lát cắt 5: sổ giải trình + idempotency ADR-13** (9 phép đo, 🔥 phát hiện **tổng đơn có thể ÂM** — cần người quyết) — `prc-002-…md` **§16–§21**; **lát cắt 6: tra sổ giải trình (`GET`)**).
> Chi tiết: `implementation-plan.md` **A.12** · `runtime-verification-report.md` **§9bis/§10** ·
> `chn-002-connector-shopee.md` **§16** · `XC-004-…md` **§15.2 F6/§15.6** · `web-001-…md` **§16** ·
> `sal-005-webhook-don-tu-san.md` **§16** · `cat-001-…md` **§16–§18**.

### Lệnh chuẩn
```powershell
Set-Location 'D:\Projects\Tools\eCommerce'

# Build (LUÔN dùng --no-incremental: build tăng dần ẨN cảnh báo)
dotnet build eCommerce.slnx -v q --no-incremental

# Test
dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo
dotnet test test\eCommerce.IntegrationTests\eCommerce.IntegrationTests.csproj --nologo

# Kiểm tài liệu Mermaid (bắt buộc sau khi sửa doc)
npm --prefix tools run check:mermaid

# Khởi động Host (cổng 5000) — BẮT BUỘC sau mọi thay đổi DI
$bin='D:\Projects\Tools\eCommerce\src\eCommerce.HttpApi.Host\bin\Debug\net10.0'
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Development','Process')
$p = Start-Process dotnet -ArgumentList 'eCommerce.HttpApi.Host.dll' -WorkingDirectory $bin -PassThru -WindowStyle Hidden `
     -RedirectStandardOutput "$env:TEMP\host.log" -RedirectStandardError "$env:TEMP\host.err"
Start-Sleep -Seconds 38
Invoke-WebRequest 'http://localhost:5000/health-status'    # mong đợi: 200 Healthy
Stop-Process -Id $p.Id -Force
```

---

## 2. 🚨 QUY TRÌNH KIỂM CHỨNG — PHẦN QUAN TRỌNG NHẤT CỦA FILE NÀY

Toàn bộ **16 lỗi im lặng** trong dự án đều có chung một dạng: **build xanh, test xanh, app chạy — nhưng sai**.
Các quy tắc dưới đây được rút ra từ lỗi **thật đã xảy ra**, không phải lý thuyết:

| # | Quy tắc | Lỗi đã trả giá |
|---|---|---|
| 1 | **KIỂM CHỨNG Ở BƯỚC RIÊNG, RỒI MỚI VIẾT COMMIT MESSAGE.** Không gộp `kiểm chứng + commit` vào một lệnh — commit message sẽ được viết **trước khi biết kết quả** | Tôi đã **2 lần** ghi commit message khẳng định "đã kiểm chứng" trong khi build **hỏng** / Host **không khởi động được**. Cả hai phải đính chính bằng commit riêng |
| 2 | **Sau MỌI thay đổi liên quan DI ⇒ PHẢI khởi động Host.** Build + test xanh **KHÔNG** chứng minh resolve được | `shared/eCommerce.Kernel` thiếu `AbpModule` ⇒ `eCommerceMetrics` không đăng ký DI ⇒ **app không khởi động**; test vẫn xanh vì test tự `new` |
| 3 | **`--no-incremental` khi kiểm cảnh báo** | Build tăng dần **ẩn 3 cảnh báo thật** (CS0108, CS8604, xUnit2031) suốt nhiều lượt |
| 4 | **`Set-Location` KHÔNG đổi CWD của tiến trình .NET** ⇒ mọi `[System.IO.File]::*` **PHẢI** dùng **đường dẫn tuyệt đối** | Đường dẫn tương đối trỏ sang `D:\Projects\Tools\WF` ⇒ sửa file **thất bại im lặng**, build hỏng |
| 5 | **Mọi project Application của module PHẢI có `AbpModule` + khai trong `[DependsOn]` của Host** | Thiếu ⇒ API trả **404 body rỗng**, **không có lỗi biên dịch** |
| 6 | **Worker phải được ĐĂNG KÝ tường minh** (`AddBackgroundWorkerAsync`) | Thiếu ⇒ không lỗi build, app vẫn chạy, job **im lặng không bao giờ chạy** |
| 7 | **Worker định kỳ: đặt `Timer.RunOnStart = true` nếu đọc cấu hình trong `DoWorkAsync`** | `DoWorkAsync` chỉ chạy **sau khi hết nhịp đầu**; nhịp 1 ngày ⇒ cấu hình **không bao giờ có tác dụng** và job **im lặng suốt ngày đầu** (lỗi #23) |
| 8 | **Mọi unique index trên `IMultiTenant` PHẢI có `NULLS NOT DISTINCT`** | PostgreSQL coi NULL là khác nhau ⇒ ràng buộc **mất tác dụng im lặng** khi `TenantId` NULL (C4/F1) |
| 9 | **Test bất biến index PHẢI liệt kê ĐÍCH DANH tên index**, không chỉ đếm tổng | Đếm tổng: thêm 1 index mới + hỏng 1 index cũ ⇒ **tổng không đổi** ⇒ test xanh trong khi ràng buộc đã mất |
| 10 | **Migration: để EF sinh khung, KHÔNG viết tay** | Migration viết tay thiếu `.Designer.cs` bị **bỏ qua âm thầm** (C3) |
| 11 | **Chạy DbMigrator bằng binary MỚI build**, rồi **kiểm trạng thái DB thật** | DbMigrator báo `exit=0` nhưng **không áp** migration vì binary cũ |
| 12 | **Log level làm mất bằng chứng:** `Started background worker` là **Debug**; worker log theo **tên type của chính nó** (không phải `Volo.Abp.BackgroundWorkers`) | Bật sai category ⇒ **không thấy gì** và dễ kết luận sai là job không chạy |
| 13 | **Kiểm chứng bằng trạng thái THẬT** (DB, log, route), không bằng suy luận | Nhiều lần "có vẻ đúng" nhưng DB cho thấy ngược lại |

### Mẫu kiểm chứng end-to-end đã dùng thành công (nên lặp lại)
1. Chèn dữ liệu thử **thật** vào PostgreSQL (bằng `psql` qua docker) → 2. khởi động Host → 3. **đọc log thật** → 4. **xác nhận trạng thái DB thật** → 5. **dọn dữ liệu thử**.
   *Ví dụ đã làm:* chèn 1 SKU chưa phân loại thuế ⇒ worker đếm `1/1` + Warning `eCommerce:Tax:0003`; bật đồng bộ ĐVHC ⇒ `+3355` đơn vị thật từ API.

### Hạ tầng dev
`docker ps` phải thấy **`ecommerce-postgres`** (17-alpine) và **`ecommerce-redis`** (7-alpine) healthy.
Host cổng **5000**. DB: `docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce`.

---

## 3. GIAI ĐOẠN 0 — ĐÃ CHỐT

📄 **`docs/gd0-closure-report.md`** — kết luận + bằng chứng + 5 input bên ngoài.
**GĐ0 đóng ở mức có thể đóng bằng code.** Ba việc cuối đã xong:
- **A** — worker đồng bộ ĐVHC (`provinces.open-api.vn`, **+3355** đơn vị thật, idempotent)
- **B** — ràng buộc `EINV-003` (một bản thay thế/điều chỉnh ↔ một hoá đơn gốc)
- **C** — worker đồng bộ mã hãng + adapter cắm được (`IKhungDiaChiHangSource`)

**5 input bên ngoài — KHÔNG thể suy đoán, không còn việc code nào tồn:**
| # | Cần gì | Cho việc |
|---|---|---|
| 1 | Văn bản + **bảng chuyển đổi 3↔2 cấp** | `ADDR-003` (Q2) |
| 2 | **Bảng trường bắt buộc `TT 91/2026`** | `EINV-001` sinh XML |
| 3 | **Endpoint + tài liệu SOAP/XML của CQT** | `EINV-002` gửi thật |
| 4 | **Chứng thư sandbox** + chốt **Q6** | `SIGN-001` xác thực chữ ký |
| 5 | **Tài liệu API danh mục GHN + GHTK** | adapter cho `ADDR-002` |

> ⚠️ **ĐỪNG tự "hoàn thiện" 5 việc này bằng suy đoán.** Bịa bảng trường XML hoặc endpoint CQT sẽ tạo ra
> sản phẩm **trông như xong nhưng SAI VỀ PHÁP LÝ** — sai ở phần khó phát hiện nhất.

---

## 4. GIAI ĐOẠN 1 — VIỆC TIẾP THEO

### ✅ QUYẾT ĐỊNH ĐÃ CHỐT (14/10/2026) — **B ngay + A sau**

Đọc **`docs/runtime-verification-report.md` §9 + §9bis** — **lỗi #22** (nay **đã sửa gốc theo phương án B**):

> `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn hàng** kéo từ Shopee/Lazada,
> nhưng **KHÔNG có handler nào nhận**. Đồng bộ vẫn báo **THÀNH CÔNG** và **TIẾN CON TRỎ ĐỒNG BỘ**
> (`shop.LastSyncedAtUtc = pull.NewSyncCursorUtc`) ⇒ sàn **không gửi lại** ⇒ **đơn mất VĨNH VIỄN**,
> **không có lỗi nào được ghi**.

**Người đã chốt: phương án B làm NGAY, phương án A làm tiếp trong GĐ1.**

| Lựa chọn | Trạng thái |
|---|---|
| **A. Xây Order intake** (đúng gốc — `SAL-005`) | ⬜ **CHƯA LÀM — đây là việc tiếp theo của GĐ1** |
| **B. Fail-closed: dừng tiến con trỏ khi chưa có nơi nhận** | ✅ **ĐÃ XONG** — `ChannelShopSyncer` **không** publish và **không** tiến con trỏ khi lô có đơn mà chưa có người nhận; ghi `LastError` + log **Error** + metric |
| **C. Chỉ để cảnh báo** | ❌ không chọn |

**Đã bịt thêm một biến thể nữa của CÙNG lỗi** (phát hiện mới, đóng luôn **F3** của `CHN-002`):
`ChannelAppService.SyncNowAsync` — nút **"Đồng bộ ngay"** — **tự kéo đơn rồi tự tiến con trỏ** mà
**không phát sự kiện cho ai**. Đây là đường **do người vận hành bấm** và **không cần handler nào bị thiếu**
để gây mất đơn. Nay cả hai đường đi qua **một** cổng `IChannelShopSyncService`.

> ⚠️ **Hệ quả CẦN BIẾT:** cho tới khi xây xong Order intake (A), **đồng bộ sàn bị chặn CÓ CHỦ Ý**:
> **đơn KHÔNG mất** (sàn gửi lại) nhưng cũng **chưa vào được hệ thống**, và log **Error** lặp
> **mỗi 5 phút/shop** (nhịp mặc định 300 s). Đây là **đánh đổi đã được chấp nhận**, không phải lỗi.

> 🚨 **`F-403` — ĐÃ SỬA (14/10/2026, cùng lượt):** **mọi** API có `[Authorize(permission)]` từng trả
> **`403` dù DB đã cấp quyền** (kể cả `admin`), trong khi endpoint chỉ-cần-đăng-nhập vẫn `200`.
> **Nguyên nhân gốc (đã ĐO, không đoán):** `JwtBearer` mặc định `MapInboundClaims = true` ⇒ nó **đổi tên**
> claim `role`/`sub` sang khuôn WS-Federation cũ, mà **ABP đọc tên NGẮN OIDC** ⇒ `ICurrentUser` **RỖNG**
> ⇒ `RolePermissionValueProvider` không thấy vai trò nào ⇒ từ chối tất cả. **Đã sửa:**
> `MapInboundClaims = false` + `NameClaimType = "preferred_username"`, kèm **bất biến lúc khởi động**
> `KiemTraBatBienTenClaim` (ghi **Error** nếu ai bật lại). Ma trận `401`/`403`/`200` **đã đo lại đầy đủ**
> trên **endpoint GẮN QUYỀN** — xem `docs/features/XC-004-xac-thuc-phan-quyen-tenant.md` **§15.2 F6 + §15.6**.
>
> ⚠️ **ĐÍNH CHÍNH dòng ở §1:** *"ma trận `401`/`403`/`200` đúng"* — ca `200` của lượt trước được đo trên
> endpoint **chỉ cần đăng nhập**, **không** phải endpoint gắn quyền ⇒ **không** chứng minh được máy phân
> quyền hoạt động, và `F-403` nằm **đúng** trong khoảng trống đó. **Bài học: một ca kiểm thử đo trên
> "đối tượng gần giống" thì KHÔNG phải là kiểm chứng.**

### Phạm vi GĐ1 (theo kế hoạch)
Đơn hàng · tồn kho · vận chuyển · khuyến mãi. Đọc `docs/implementation-plan.md` §A.9 để biết danh sách feature theo giai đoạn.

### Việc tiếp theo, theo thứ tự đề xuất
| # | Việc | Ghi chú |
|---|---|---|
| 1 | **`SAL-005` §16.4** — xử lý **cập nhật theo watermark** (D5) + `SalMarketplaceEventLogs` + **đẩy DLQ** cho xung đột payload | Cần bổ sung **9 trường ETO** (F2) — hiện đơn nhận về là **tạo-một-lần** (không mất đơn, nhưng có thể cũ) |
| 2 | **`WEB-001` §16.6** — menu/điều hướng theo permission · `[AllowAnonymous]` allowlist · ma trận quyền tự động cho UI | UI khung đã chạy thật (`web-001-…md` **§16**) |
| 3 | **`§7.0 #8` — observability còn thiếu** | **5/12 metric chưa có nơi phát** |
| 4 | **`CHN-002` §16.5 #5** — supervisor thiếu `Timer.RunOnStart = true` | ⚠️ Nay đã thấy **tác động thật**: nhịp supervisor đầu tiên luôn **trễ 60 s** ⇒ mọi lần deploy/kiểm chứng phải chờ 60 s mới thấy worker chạy |
| 5 | **Outbox/relay** | 🚨 Cảnh báo **giữ nguyên**: **không có message broker** (chỉ PostgreSQL + Redis) ⇒ outbox vẫn là hạ tầng cho vấn đề **chưa tồn tại** |

> ✅ **ĐÃ XONG trong §7.0:** **#2** `eCommerce.Web` + màn hình đăng nhập (chạy thật, cổng **5100**) ·
> **#5** `docs/coding-conventions.md` + `.editorconfig` · **#7** 2 test reflection `XC-004` TC-01/TC-02.
> **ĐÃ XONG `SAL-005`** (nơi nhận đơn) ⇒ **lỗi #22 đóng hoàn toàn**.
> Tiến độ đầy đủ §7.0: `docs/implementation-plan.md` **A.12**.

### 🖥️ Hai tiến trình — chạy thế nào

| Tiến trình | Cổng | Vai trò | Lệnh |
|---|---|---|---|
| `eCommerce.HttpApi.Host` | **5000** | **API + auth server DUY NHẤT** (OpenIddict phát token) | `dotnet eCommerce.HttpApi.Host.dll` |
| `eCommerce.Web` | **5100** | **UI quản trị** + màn hình đăng nhập (cookie của ABP Identity) | `dotnet eCommerce.Web.dll` |

⚠️ **Trước khi chạy `eCommerce.Web` trên máy mới (hoặc sau khi clone):** thư mục `wwwroot/libs`
**không** được commit (thư mục sinh ra) ⇒ **PHẢI** chạy:

```powershell
abp install-libs -wd src/eCommerce.Web        # cần yarn; tái tạo từ package.json + yarn.lock
```

Không chạy ⇒ trang vẫn trả **`200`** nhưng **thiếu toàn bộ CSS/JS** (hiển thị trơ) — đúng loại
*"trông như chạy được"*. Đăng nhập: `admin@abp.io` / `1q2w3E*` (dev).

🚨 **Hai bất biến bắt buộc cho MỌI tiến trình UI thêm sau này** (đã trả giá, xem `WEB-001` §16.4/§16.5):
1. **KHÔNG** truyền scheme mặc định vào `AddAuthentication(...)` — sẽ **âm thầm** làm cookie đăng nhập
   không được đọc (`[Authorize]` tìm ở nhầm scheme).
2. **TẮT** `AbpBackgroundWorkerOptions.IsEnabled` và `AbpBackgroundJobOptions.IsJobExecutionEnabled`
   — nếu không, worker chạy **HAI LẦN** (đồng bộ sàn, gửi cơ quan thuế…) ⇒ tiêu quota gấp đôi.

---

## 5. BẢN ĐỒ TÀI LIỆU — ĐỌC GÌ KHI CẦN GÌ

| Cần biết | Đọc |
|---|---|
| Trạng thái tổng thể + kế hoạch + tiến độ §7.0 | `docs/implementation-plan.md` (§A.7 trạng thái · §A.9 danh sách feature · §A.10 chốt GĐ0 · **§A.12 tiến độ GĐ1 + §7.0**) |
| **Quy ước viết code** (bắt buộc đọc trước khi viết module mới) | `docs/coding-conventions.md` + `.editorconfig` |
| GĐ0 đã chốt tới đâu + 5 input | `docs/gd0-closure-report.md` |
| **Lỗi thật đã gặp** (24 lỗi, 16 im lặng) | `docs/runtime-verification-report.md` |
| Thiết kế từng feature (52 doc, mỗi doc 16 mục) | `docs/features/` — mỗi doc có mục **cuối** ghi trạng thái triển khai + việc còn lại |
| Quy ước bất biến của dự án | `docs/features/XC-001…XC-006*.md` |
| Quan sát/log/metric | `docs/features/XC-005-observability-log-metric.md` (§16–18: lỗi tầng + 7/12 điểm phát) |

---

## 6. QUY ƯỚC BẤT BIẾN — KHÔNG ĐƯỢC VI PHẠM

1. Tiền: `decimal` + `Money`, **không bao giờ** so sánh `decimal` trần bằng `==`
2. Module sở hữu entity của mình; **module → app BỊ CẤM** (app → module được phép)
3. Mọi entity cần `DbSet` trên DbContext hợp nhất **hoặc** `IRepository<T>` sẽ **không** được đăng ký
4. `[Authorize]` ở **cấp class**
5. Sổ kho **append-only**
6. **Idempotency ở MỌI biên vào**
7. **Máy trạng thái PHẢI ném lỗi**, không im lặng bỏ qua
8. **Khoá phân tán** cho tài nguyên dùng chung — ⚠️ `AbpDistributedLockOptions.KeyPrefix` nối **không có dấu phân cách** ⇒ **KHÔNG** tự thêm tiền tố `ecommerce:` vào tên khoá (bài học C11/C13)
9. Ghi dữ liệu qua **Unit of Work**
10. **Một phiên bản ABP** trong `Directory.Packages.props`
11. **`NULLS NOT DISTINCT`** cho mọi unique index trên `IMultiTenant`
12. **Không AutoMapper** (dùng map tay / Mapperly)
13. **Trả lời và viết tài liệu bằng TIẾNG VIỆT**
14. **Khi không chắc: nêu rõ cái chưa kiểm chứng**, không khẳng định suông
15. Sửa xong ⇒ cập nhật mục **trạng thái triển khai** ở **cuối** tài liệu feature tương ứng

---

## 7. CÁCH MỞ SESSION MỚI (làm đúng thứ tự này)

1. **Kiểm tra môi trường:** `docker ps` (postgres + redis phải healthy) · `git log -1` · `git status` (phải sạch)
2. **Chạy baseline** trước khi sửa gì — để biết mình có làm hỏng không:
   ```powershell
   dotnet build eCommerce.slnx -v q --no-incremental      # mong đợi 0/0
   dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo   # mong đợi 361/361
   ```
3. **Nói với agent việc cần làm**, kèm 2 câu quan trọng:
   - *"Đọc `docs/handover-gd1.md` và `docs/gd0-closure-report.md` trước."*
   - *"Trả lời bằng tiếng Việt. Tuân thủ quy trình kiểm chứng ở §2 của file bàn giao."*

> 📌 **Đừng dán lại toàn bộ nội dung cuộc trò chuyện cũ vào session mới.** File bàn giao này + git + 52 tài liệu
> đã chứa mọi thứ cần thiết, ở dạng **ngắn gọn và đã kiểm chứng**. Dán lại lịch sử chỉ làm **cạn ngữ cảnh
> của session mới ngay từ đầu** — đúng vấn đề đang muốn tránh.

---

## PHỤ LỤC B — **TRẢ NỢ: ÁNH XẠ VI PHẠM RÀNG BUỘC DB → LỖI NGHIỆP VỤ** (15/10/2026)

### B.1 Vấn đề (đã **đo được**, không phải phòng xa)

PostgreSQL trả `23505` (vi phạm duy nhất) / `23514` (vi phạm `CHECK`) / `23503` (khoá ngoại) khi ràng buộc DB chặn
một hành vi **hợp lệ về nghiệp vụ** (retry · double-click · job chạy lại · hai request đồng thời). ABP để nguyên ⇒
**HTTP `500`**. Hệ quả **không nhỏ**:
- client **KHÔNG phân biệt được** *"đã làm rồi"* với *"hệ thống hỏng"* ⇒ **client sẽ retry** ⇒ tăng tải, có thể thành **vòng lặp**;
- CSKH không có mã để phân loại sự cố;
- log đầy `fail:` **không phân biệt** ràng buộc (bình thường) với lỗi hạ tầng (bất thường).

**Đã đo `500` ở 3 chỗ:** ghi sổ sổ cái trùng · **giữ chỗ trùng nguồn** · số chứng từ/SKU trùng trong chứng từ.

### B.2 Giải pháp

| # | Thành phần | Nội dung |
|---|---|---|
| 1 | `ViPhamRangBuocDb` (**Kernel**, **hàm THUẦN**) | ánh xạ theo **TÊN RÀNG BUỘC** (không theo bảng — một bảng có nhiều ràng buộc **khác nghĩa**: `InvStockLedger` chặn *"ghi sổ trùng"*, `InvItems` chặn *"âm kho"*). Có **16** ràng buộc đã ánh xạ |
| 2 | `BoLocViPhamRangBuocDb` (**tầng Host**) | 📌 Phải ở **Host** vì lỗi chỉ xuất hiện lúc `SaveChanges` — **sau** khi app service đã trả về; **không tầng nào phía trong bắt được**. Đây cũng chính là lý do `ADR-09` (`UPDATE … WHERE Status='Draft'`) **không** đặt ở Domain được: *kiểm tra nguyên tử chỉ có ở tầng DB* |
| 3 | Trả lỗi | JSON **đúng dạng của ABP** (`{"error":{"code":…,"message":…,"data":{sqlState,constraint}}}`) + HTTP **`403`** (cùng quy ước `BusinessException` toàn hệ thống) ⇒ client **không phải xử lý hai định dạng** |
| 4 | **Kiểm trước** ở `StockReservationService` | ca **thường** (khách bấm hai lần) trả `0032` **ngay**, không chờ DB nổ; **hàng rào DB vẫn giữ** cho ca **đồng thời** ⇒ bộ lọc biến `23505` thành **cùng một mã** ⇒ client thấy **một hành vi duy nhất** dù đi đường nào |

### B.3 🚨 Ranh giới **KHÔNG** được vượt: không nuốt lỗi hệ thống

`AnhXa` trả **`null`** cho mọi SQLSTATE **không** phải ràng buộc (`08006` mất kết nối, `53300` quá nhiều kết nối,
`40001` serialization failure, `57014` query bị huỷ…) ⇒ lỗi **nổi lên `500` như cũ**.
📌 Nếu ánh xạ cả những mã đó thì **sự cố hạ tầng bị che thành lỗi nghiệp vụ** ⇒ đội vận hành **không thấy**, client
**retry vô hạn**. Có **test riêng** khoá ranh giới này.

### B.4 ✅ BẰNG CHỨNG HTTP THẬT (Host + PostgreSQL 17)

| # | Ca | Trước | **Nay** |
|---|---|---|---|
| 1 | Ghi sổ sổ cái **lần hai** | `403` `0010` (đã tốt) | `403` **`Inventory:0010`** ✅ |
| 2 | 🚨 **Giữ chỗ trùng nguồn** (cùng `ReferenceId` + SKU) | **`500`** | `403` **`Inventory:0032`** ✅ |
| 3 | 🚨 **Số chứng từ trùng** (unique index trên DB) | **`500`** | `403` **`Inventory:0014`** ✅ |
| 4 | 🚨 **SKU trùng trong cùng chứng từ** | **`500`** | `403` **`Inventory:0015`** ✅ |
| 5 | **Đối chứng**: chứng từ không tồn tại | — | `403` **`Inventory:0011`** ✅ (lỗi nghiệp vụ, **không** `500`) |

- build `--no-incremental`: **0 lỗi / 0 cảnh báo** · unit **679/679** (+15) · integration **38/38** · mermaid 197/0
- **Không đổi schema** (thuần tầng xử lý lỗi)
- 📌 `fail: 3` trong log **đúng bằng** số ca vi phạm **cố tình** kích hoạt — đó là EF ghi lệnh SQL thất bại
  (mức `Error` của **EF**, không phải exception chưa xử lý). Bộ lọc ghi thêm dòng **`Warning`** có `SQLSTATE` +
  tên ràng buộc + **mã nghiệp vụ** ⇒ tra cứu được. **Không còn `500` nào.**

### B.5 Bảo trì — **BẮT BUỘC** khi thêm ràng buộc mới

Mỗi khi thêm unique index/`CHECK` **có ý nghĩa nghiệp vụ**, **phải** thêm một dòng vào `TheoRangBuoc`.
Nếu quên: ca đó rơi về mã chung `eCommerce:Db:0001` — **vẫn tốt hơn `500`**, nhưng **kém hữu ích cho CSKH**.
📌 Test `So_rang_buoc_da_anh_xa_KHONG_giam_di` chốt **sàn 14** ràng buộc ⇒ sửa chữa nhầm làm **xoá** dòng ánh xạ sẽ **ĐỎ**,
thay vì âm thầm để các ca đó quay lại `500`.

---

## PHỤ LỤC C — **CƠ CHẾ THAY VÌ QUY TẮC: ràng buộc DB phải được ÁNH XẠ hoặc KHAI BÁO có LÝ DO** (15/10/2026)

### C.1 Vì sao cần (bài học đã trả giá)

**Phụ lục B §B.5** đã **ghi quy tắc**: *"thêm unique index/`CHECK` mới ⇒ **phải** thêm một dòng vào `TheoRangBuoc`"*.
**Và quy tắc đó đã bị vi phạm ngay trong vòng làm ra nó:** ràng buộc `IX_OrdOrders_TenantId_CheckoutIdempotencyKey`
(vòng 33) trả **mã chung** `eCommerce:Db:0001` thay vì mã nói rõ *"đã có đơn cho lần checkout này"*
(`inv-001` **§25.3**).
⇒ **Ghi quy tắc KHÔNG ĐỦ — cần CƠ CHẾ chặn ở CI.**

### C.2 Cơ chế

`RangBuocDbChuaAnhXaTests` (unit test) đọc **`test/eCommerce.IntegrationTests/Assets/schema.sql`** — **nguồn sự thật
của DB** (sinh từ migration) — lấy ra **mọi `UNIQUE INDEX` (trừ `PK_`)** + **mọi `CHECK` có tên** rồi đối chiếu:

| Nhóm | Ý nghĩa | Kết quả |
|---|---|---|
| Có trong `ViPhamRangBuocDb.CacRangBuocDaAnhXa` | **ĐÃ MAP** ⇒ client nhận **mã nghiệp vụ** | ✅ |
| Có trong **`test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`** (`<TÊN>\|<LÝ DO>`) | **CHƯA MAP NHƯNG CÓ LÝ DO** | ✅ |
| **Không** thuộc hai nhóm trên | 🚨 người thêm ràng buộc **phải chọn**: map, hoặc khai báo lý do | **ĐỎ** |

**Quy mô hiện tại:** **17** ràng buộc **đã map** · **50** ràng buộc trong **danh sách trắng có lý do**.

### C.3 Ba test phụ chống "danh sách trắng thành bãi rác"

| Test | Chặn điều gì |
|---|---|
| `Danh_sach_trang_KHONG_duoc_chua_rang_buoc_DA_ANH_XA` | **hai nguồn sự thật** (vừa map vừa khai báo) ⇒ người sau không biết cái nào đúng (`XC-003` F2) |
| `Danh_sach_trang_moi_dong_PHAI_co_ly_do` | **khai báo rỗng** = chỉ **che** ràng buộc đi, đúng thứ cơ chế này ngăn |
| `Danh_sach_trang_KHONG_duoc_chua_ten_LA` | **dòng chết** (ràng buộc đã đổi tên/bị xoá) làm danh sách phình ra và **che mất** việc cần map lại |

📌 **Vì sao đọc `schema.sql` mà không đọc DB sống:** test đơn vị **không** có PostgreSQL; `schema.sql` được **sinh lại
sau mỗi migration** (đã nằm trong quy trình). ⚠️ Nếu quên sinh lại sau khi thêm ràng buộc thì test này **không thấy**
ràng buộc mới — **nhưng** `PostgreSqlSchemaTests` (integration) sẽ **ĐỎ** vì đếm index `NULLS NOT DISTINCT` lệch
⇒ **vẫn bị chặn** ở tầng khác.

### C.4 ✅ BẰNG CHỨNG — **mutation test** (chứng minh cơ chế THẬT SỰ nổ, không chỉ xanh)

| Bước | Kết quả |
|---|---|
| 1. Sao lưu danh sách trắng (`Copy-Item`, **không** dùng `git checkout` — bài học vòng 7) | ✅ |
| 2. **Xoá một dòng thật** (`IX_OrdOrders_TenantId_Number\|…`) | |
| 3. Chạy test | 🚨 **ĐỎ** — thông điệp chỉ **đúng** tên ràng buộc bị bỏ: `IX_OrdOrders_TenantId_Number` ✅ |
| 4. **Khôi phục từ file sao lưu** | ✅ |
| 5. Chạy lại toàn bộ | unit **686/686** · integration **38/38** · build **0 lỗi / 0 cảnh báo** ✅ |

📌 **Vì sao mutation test là bằng chứng bắt buộc ở đây:** một test **luôn xanh** (vì quét hỏng, sai đường dẫn, hoặc
danh sách trắng quá rộng) sẽ **tệ hơn không có test** — nó tạo **cảm giác an toàn giả**. Bước 3 chứng minh phép quét
**thật sự đọc được** ràng buộc và **thật sự đối chiếu**.

### C.5 Việc còn lại

| # | Việc |
|---|---|
| 1 | **Dịch dần danh sách trắng thành ánh xạ** khi mỗi miền (`CAT`/`Ord`/`Pay`/`Chn`…) có đường ghi thật: khi đó lỗi trùng **phải** trả mã nghiệp vụ |
| 2 | **Job định kỳ** cập nhật/thu gọn danh sách trắng (ràng buộc của GĐ0 sẽ dần được dùng tới) |

> **MỐC KIỂM CHỨNG VÒNG 88 (15/10/2026):** `694 unit + 38 integration` · build **0 lỗi / 0 cảnh báo** · mermaid **197/0** · **174 commit**, nhánh `main` **sạch**. **Ba cơ chế tự bảo vệ** (Phụ lục **B** · **C** nâng cấp §45 · UoW inv-001 §29.2) · **ba công cụ** (`route-check.ps1` · `wait-host.ps1` · **`README-probe.md`** — danh mục kiểm trước khi viết probe) · **Phụ lục D/E/F**. **Web:** cổng đăng nhập (BR-009 ✅) · **harness đăng nhập** · **điểm đo thống nhất** (`/health-status` cho cả Host và Web) · **3 trang hai chiều** (`/PhanBo` · `/SanPham` · `/DatHang`) — *khách ĐẶT ĐƯỢC ĐƠN trên web*, giỏ **nhiều dòng** (**cả ca biên**), chống trùng đơn qua UI, thông điệp lỗi kèm mã + dữ liệu.

---

## PHỤ LỤC D — **CƠ CHẾ `tools/route-check.ps1`: chống lỗi "SAI ROUTE"** (15/10/2026)

### D.1 Vì sao có (bài học đã trả giá **BA lần**)

| # | Ca | Sai route | Tôi đã **đọc nhầm** thành |
|---|---|---|---|
| 1 | `PRC-001` §19.7 | `POST …/chot-theo-nguon?referenceId=…` ⇒ **`405`** (tham số là **đoạn đường dẫn**) | *"lỗi xác thực"* |
| 2 | `inv-001` §33.1 | `POST /{id}/tra-hang` **không còn** `{warehouseId}` ⇒ **`404`** | *"chưa làm xong"* |
| 3 | `web-001` §53.2 | `GET /api/app/price-list/item/{id}` ⇒ **`405`** (route đúng là **`items`** cho GET, `item` cho POST) | *"khác kiểu chữ hoa/thường của JSON"* |

📌 **Ba lần** cùng một họ lỗi ⇒ đúng kết luận đã rút ở **Phụ lục C**: **ghi bài học KHÔNG ĐỦ — cần CÔNG CỤ** khiến việc **đúng**
trở thành việc **dễ nhất**.

### D.2 Công cụ làm gì

`pwsh -File tools/route-check.ps1 -Match 'price-list' -Call '<url>'`

| # | Việc | Chi tiết |
|---|---|---|
| 1 | **Tự khởi động Host** nếu chưa chạy (và **tự dừng** nếu chính nó khởi động) | không để lại tiến trình treo |
| 2 | **In ROUTE THẬT** từ `swagger.json` (nguồn do **chính Host ban hành**) khớp mẫu `-Match` | **KHÔNG đoán route** |
| 3 | Gọi thử URL và **in `HTTP STATUS` TRƯỚC**, rồi mới tới body | 🚨 *"rỗng"* luôn đọc được là *"rỗng **VÌ SAO**"* |
| 4 | Gặp **`404`/`405`** ⇒ in **🚨 SAI ROUTE — ĐỐI CHIẾU danh sách route ở trên TRƯỚC KHI kết luận** | chặn đúng lỗi đã xảy ra 3 lần |
| 5 | **Giải mã `Byte[]`** trước khi in body | bài học §53.2 (`Invoke-WebRequest` có thể trả `Byte[]` ⇒ `.Substring` **ném lỗi**) |

### D.3 ✅ BẰNG CHỨNG — chạy thật (exit `0`, không stderr)

```
Host chưa chạy ⇒ khởi động từ …\eCommerce.HttpApi.Host\bin\Debug\net10.0

=== ROUTE THẬT khớp /price-list/ (6 đường dẫn) ===
  /api/app/price-list                        GET   ()
  /api/app/price-list                        POST  ()
  /api/app/price-list/{id}                   GET   id(path)
  /api/app/price-list/items/{priceListId}    GET   priceListId(path)     ← GET dùng "items"
  /api/app/price-list/item/{priceListId}     POST  priceListId(path)     ← POST dùng "item"
  /api/app/price-list/publish/{priceListId}  POST  priceListId(path)
  /api/app/price-list/activate/{priceListId} POST  priceListId(path) at(query)

=== GỌI THỬ (1 lời gọi) ===
  GET   http://localhost:5000/api/app/price-list/item/0000…0001
        HTTP 405
        🚨 SAI ROUTE — ĐỐI CHIẾU danh sách route ở trên TRƯỚC KHI kết luận bất cứ điều gì.
        body:
XONG.
Đã dừng Host do chính công cụ khởi động.
```
📌 **Chứng minh giá trị ngay lần chạy đầu:** chính bảng route này **lộ ra** chỗ tôi gọi sai ở vòng 60–61
(**`items`** cho GET vs **`item`** cho POST) — thứ mà **3 vòng** đọc bài học suông đã **không** chặn được.

### D.4 Dùng thế nào (và một hạn chế **đã biết**)

| Việc | Lệnh |
|---|---|
| Xem route khớp mẫu | `pwsh -File tools/route-check.ps1 -Match 'don-hang'` |
| Gọi thử **một** URL | `pwsh -File tools/route-check.ps1 -Call 'http://localhost:5000/api/app/price-list'` |
| Gọi thử **POST** | `pwsh -File tools/route-check.ps1 -Call 'POST http://localhost:5000/api/app/price-list'` |

⚠️ **Hạn chế đã biết:** tham số `-Call` là **mảng**, nhưng khi gọi qua **`Start-Process -ArgumentList`** thì PowerShell
**tách token** ⇒ **chỉ nên truyền MỘT URL mỗi lần gọi** (hoặc gọi nhiều lần). Đã đo: truyền 2 URL ⇒ URL thứ hai
bị bind nhầm vào `-WaitSeconds` và công cụ **báo lỗi rõ ràng** (không im lặng làm sai).

### D.5 Việc còn lại

| # | Việc |
|---|---|
| 1 | 🚨 **Quy trình §2 (kiểm chứng) nay có thêm bước BẮT BUỘC:** trước khi viết lời gọi API mới ⇒ chạy `route-check.ps1 -Match '<mẫu>'` để **lấy route thật**. Đưa bước này vào **danh sách kiểm trước khi viết probe**. |
| 2 | Làm nốt **đối chứng chéo API ↔ `/SanPham`** bằng route **`items`** (§53.3 #1) — nay chỉ còn **một lần chạy** |

---

## PHỤ LỤC E — **TRẠNG THÁI & VIỆC TIẾP THEO (chốt vòng 74, 15/10/2026)**

### E.1 Đã xong và **đã kiểm chứng** (không cần làm lại)

| Nhóm | Nội dung |
|---|---|
| **Hạ tầng nền** | `eCommerce.Kernel` (Money · Locking · Observability · ánh xạ ràng buộc DB) · **3 cơ chế tự bảo vệ** (B/C nâng cấp §45 · `inv-001` §29.2) · **7/7 job nhìn thấy được** trong log (`inv-001` §44) |
| **Nghiệp vụ** | `CAT-001/002` · `PRC-001/002` (**sổ giải trình** — mọi con số bán hàng lấy từ đây) · `INV-001` (nhập/chuyển/kiểm kê/**sổ cái append-only**) · `INV-002` (giữ chỗ + job nhả chỗ quá hạn) · `SAL-001…004` (đặt đơn → **xác nhận** → **huỷ** → **trả hàng** toàn bộ/một phần/luỹ kế + `CustomerReturn`) · `INV-003` **bước 1** (phân bổ theo kênh **và** theo kho) · `REC-001` **đối soát tồn kho** (job định kỳ, **phát hiện + đếm đúng**) |
| **Web** | khung + cổng đăng nhập (`BR-009`) + **harness đăng nhập** + **3 trang hai chiều**: `/PhanBo` · `/SanPham` · **`/DatHang` (đặt được đơn, giỏ nhiều dòng, chống trùng đơn qua UI, thông điệp lỗi kèm mã + dữ liệu)** |
| **Công cụ** | `tools/route-check.ps1` (Phụ lục D) · **chuẩn dọn dẹp probe** (tệp `.sql` + `SELECT` xác nhận + kiểm tiến trình treo, `web-001` §53.1) |

### E.2 Việc tiếp theo — **thứ tự ưu tiên đề xuất** (kèm cách kiểm chứng đã chứng minh)

| # | Việc | Vì sao ưu tiên | Khuôn kiểm chứng |
|---|---|---|---|
| 1 | **Quản lý đơn trên UI** (`/DonHang`: danh sách + **xác nhận/huỷ/trả**) | 🎯 **RỦI RO NAY BẰNG 0** — **cả BA API** đã chứng minh chạy đúng trên **đơn do Web tạo / định dạng `WEB-…`**: **`XÁC NHẬN`** (`web-001` §65) · **`HUỶ`** (§69) · **`TRẢ HÀNG`** (§69). ⇒ còn lại **thuần tuý nối nút bấm** | `302` khi chưa đăng nhập · có dữ liệu ⇒ **DB đổi trạng thái** + **tồn kho đổi đúng bất biến** (`Reserved`↓ khi huỷ; `OnHand`&`Reserved`↓ khi xác nhận; `OnHand`↑ khi trả) |
| 2 | **Giao diện giỏ nhiều ô nhập** + giỏ **giữ qua phiên** | UX thật cho khách (logic **đã** kiểm chứng — chỉ đổi view) | POST nhiều dòng ⇒ **số tính trước** khớp `GrandTotal` + sổ giải trình |
| 3 | **`INV-003` hạn mức CỨNG theo kênh** | **cần người quyết** chính sách chia | — |
| 4 | **`REC-001` ngưỡng cảnh báo + quy trình xử lý lệch** + bảng `ReconRuns` | **cần người quyết** + migration | — |
| 5 | **`CAT-003`** (slug/partial index · **chi tiết sản phẩm + SEO** cho `WEB-002`) | mở rộng storefront | |
| 6 | **Hoàn tiền** (`PaymentHub`/kế toán) · **`CUS-001`** · **`SAL-005`** (intake sàn) · `REC-001` **đối soát thanh toán** (`querydr`) | phần còn lại của GĐ1 | |

### E.3 **Kỷ luật bắt buộc** khi làm tiếp (đã trả giá để rút ra)

1. **In route thật TRƯỚC khi viết lời gọi API** — `tools/route-check.ps1 -Match '<mẫu>'` (**Phụ lục D**; lỗi *sai route* đã tái phạm **3 lần**).
2. **KHÔNG phẫu thuật probe cũ** — viết **probe mới** (bài học #9/#10, `web-001` §60–§61); hỏng probe **không** ảnh hưởng sản phẩm (probe ở `%TEMP%`).
3. **KHÔNG lọc `stderr`**; **đo điều kiện tiên quyết** trước khi kết luận (`inv-001` §39.4 · `web-001` §63.1).
4. **Dọn dẹp bằng tệp `.sql`** + `SELECT` xác nhận + kiểm tiến trình treo.
5. **`Invoke-WebRequest` tự đi theo redirect** ⇒ đọc **status thô** (`-MaximumRedirection 0`) hoặc **URL cuối** (`web-001` §47).
6. **Phép kiểm văn bản thô trên HTML** (substring/regex `[^<]*`) là **nguồn kết luận sai** — đã 3 lần (`web-001` §48 · §52.3 · §57.3) ⇒ coi là **giả thuyết**, xác nhận bằng **cấu trúc** hoặc **hành vi**.
7. **Mutation test** cho mọi **cơ chế** mới, và **`git diff`** sau khi khôi phục (`inv-001` §46.3).
8. **Test tự động thay vì quy tắc suông** khi một lỗi đã tái phạm ≥ 2 lần (**Phụ lục C** là ví dụ gốc).

---

## PHỤ LỤC D (tiếp) — **`tools/wait-host.ps1`: HÀM CHỜ CHUẨN** (vòng 80, 15/10/2026)

### D.6 Vì sao có (bài học #12 · `web-001` §68.3)

**Bốn vòng liên tiếp** (69 · 75 · 77 · **79**) hỏng **công cụ ĐO**, không phải sản phẩm. Riêng vòng 79, probe `throw`
đúng **một câu** *"Host tại … KHONG len trong 90 giay"* mà **KHÔNG in log** ⇒ **không biết VÌ SAO** (*cổng bị chiếm ·
lỗi khởi động · kết nối DB · thiếu module*…).
📌 Đó **đúng** lỗi đã ghi ở `inv-001` **§27.3** (*"im lặng không phân biệt được với không chạy"*) — nhưng ở **chính công cụ đo**.
⇒ Theo **bài học Phụ lục C**: *ghi quy tắc **không đủ** — cần **công cụ***. Đây là công cụ đó, **dùng chung cho mọi probe**.

### D.7 Công cụ làm gì

| # | Việc |
|---|---|
| 1 | **Chờ** `<Url>/health-status` = **`200`** (không đoán bằng `Start-Sleep` mù), kiểm mỗi **2 giây** |
| 2 | 🚨 **Hết hạn ⇒ IN 15 DÒNG CUỐI của `-LogFile`** (nếu có) **RỒI** ném lỗi có ngữ cảnh ⇒ *thất bại to **và có thông tin*** |
| 3 | Nếu truyền **`-ProcessId`** mà tiến trình **đã thoát** ⇒ in **mã thoát** + đuôi log **ngay** (không chờ vô ích) |
| 4 | Khi lên được ⇒ in **số giây đã chờ** (để phát hiện khởi động chậm dần theo thời gian) |

**Cách dùng:**
```powershell
pwsh -File tools/wait-host.ps1 -Url http://localhost:5000 -LogFile "$env:TEMP\host.log"
pwsh -File tools/wait-host.ps1 -Url http://localhost:5100 -LogFile "$env:TEMP\web.log" -TimeoutSeconds 120 -ProcessId $pid
```

### D.8 ✅ BẰNG CHỨNG — kiểm **CẢ HAI CHIỀU**

| Ca | Kỳ vọng | Kết quả đo được |
|---|---|---|
| **1. URL không lắng nghe** (`:5999`, có log giả) | hết hạn · **in đuôi log** · **ném lỗi** | `exit **1**` ✅ · in **đủ 3 dòng** log — **kể cả dòng chẩn đoán** *"LOI THAT: khong ket noi duoc DB"* ✅ · thông báo *"Không chờ được … Xem đuôi log ở trên"* ✅ |
| **2. Host THẬT** (`:5000`) | thành công · `exit 0` | **`✅ LÊN sau 2.3s — http://localhost:5000/health-status = 200`** ✅ · `exit **0**` ✅ |
| 3 | không để lại tiến trình treo | **0** tiến trình `dotnet` treo ✅ |

📌 **Vì sao phải kiểm CẢ HAI chiều:** công cụ **chỉ** kiểm ca thành công thì **không** chứng minh được nó **in log khi hỏng** —
mà **đó chính là lý do nó tồn tại**. Ca 1 là ca **quan trọng nhất**, y như **cặp đối chứng** ở `inv-001` §38–§39.

### D.9 Chưa làm (ghi rõ)

| # | Việc |
|---|---|
| 1 | **Chuyển các probe cũ sang dùng công cụ này** — hiện mỗi probe vẫn có `ChoHost` **riêng**; việc *"dùng chung"* cần sửa **từng probe** (làm dần khi probe được viết lại) |
| 2 | **`HUỶ`/`TRẢ HÀNG` trên đơn do Web tạo** — **vẫn chưa đo** (`web-001` §66.3 · §68); rủi ro đã khoanh vùng **thấp** (`web-001` §67) nhưng **chưa bằng không** |

---

## PHỤ LỤC E (tiếp) — **E.4 ĐƯỜNG NỐI UI → API: ĐÃ CHỨNG MINH ĐỦ BA CHUYỂN TRẠNG THÁI** (vòng 82, 15/10/2026)

| Chuyển trạng thái trên **đơn do Web tạo / định dạng `WEB-…`** | Bằng chứng | Bất biến đo được |
|---|---|---|
| **`XÁC NHẬN`** ⇒ xuất kho | `web-001` **§65** (trên đơn **do Web tạo**) | `OnHand` **và** `Reserved` **cùng giảm** ⇒ **`Available` KHÔNG đổi** (`10/4/6` → `6/0/6`) |
| **`HUỶ`** ⇒ nhả chỗ ngay | `web-001` **§69** | `Reserved` `4 → 0` · **`Available` `6 → 10`** · `OnHand` **không đổi** · giữ chỗ **`Released`** |
| **`TRẢ HÀNG`** ⇒ nhập kho + `CustomerReturn` | `web-001` **§69** | `OnHand` `7 → 10` · **phiếu `TH-WEB-WEB-…-1`** (chứng minh công thức số phiếu + tra **luỹ kế** chạy đúng với `Number` hình dạng WEB) |

📌 **Hệ quả cho kế hoạch:** hạng mục **"quản lý đơn trên UI"** rời khỏi nhóm **rủi ro** ⇒ trở thành **việc nối nút bấm**.
Mọi **rủi ro về định dạng số đơn `WEB-…`** đã **bị bác bỏ bằng đo** (§67 khoanh vùng · §69 bác bỏ).
📌 **Bài học phương pháp #13** (đã ghi ở `web-001` §69.4): *khi một phép đo phức tạp hỏng ≥ 2 lần ⇒ **tách** thành phép đo nhỏ nhất
còn trả lời được câu hỏi* — vòng 81 thắng **ngay lần đầu** sau bốn vòng hỏng vì đã **cô lập biến số**.

---

## PHỤ LỤC F — **`tools/README-probe.md`: DANH MỤC KIỂM TRƯỚC KHI VIẾT PROBE** (vòng 87, 15/10/2026)

**Vì sao có:** các vòng **69 · 75 · 77 · 79 · 83** mất trọn vòng vì **lỗi công cụ ĐO**, **không** vì nghiệp vụ khó.
Mỗi lần là **một lớp lỗi khác**, và mỗi lần tôi **ghi một bài học** — rồi lần sau **vấp lớp khác**.
📌 Đúng kết luận **Phụ lục C**: *ghi bài học **không đủ** — cần **công cụ** và **danh mục kiểm***.

**Nội dung tệp (`tools/README-probe.md`)** — 6 phần:

| Phần | Nội dung |
|---|---|
| **A** | **9 mục trước khi chạy**: kiểm **cú pháp** trước · sửa mã bằng **`edit`** (không `Replace` chuỗi) · **mỗi việc một dòng + in giá trị trung gian** · chờ bằng **`wait-host.ps1`** · **kiểm điểm đo có tồn tại** · **in route thật** trước khi gọi · **không lọc `stderr`** · **đo điều kiện tiên quyết** · **không phẫu thuật probe cũ** |
| **B** | **4 mục khi đọc kết quả**: in **HTTP STATUS trước** · `Invoke-WebRequest` **tự đi theo redirect** ⇒ đọc **URL cuối** · **phép kiểm văn bản thô trên HTML là nguồn kết luận sai (3 lần)** · **`git diff`** sau khi khôi phục |
| **C** | **Phép đo hỏng ≥ 2 lần ⇒ TÁCH** thành phép đo nhỏ nhất còn trả lời được câu hỏi (bài học #13) |
| **D** | **3 mục dọn dẹp**: SQL qua **tệp `.sql`** · **`SELECT` xác nhận** + khôi phục cờ bảo mật · **kiểm tiến trình treo** |
| **E** | 🔐 **LUẬT form Web**: mọi `POST` **phải mang token chống giả mạo** — `.cshtml` ⇒ `@Html.AntiForgeryToken()`; render trong C# ⇒ inject `IAntiforgery`; **probe phải trích token từ trang GET rồi gửi kèm**. Triệu chứng: `POST` ⇒ **`400`** và **DB không đổi** |
| **F** | **Hai công cụ đã có** (`wait-host.ps1` · `route-check.ps1`) — dùng, đừng viết lại; **điểm đo nay thống nhất** (cả Host và Web đều có `/health-status`) |

📌 **Vị trí trong bức tranh chung:** Phụ lục **B** (ánh xạ ràng buộc DB) · **C** (cơ chế job) · **D** (route · chờ)
là **cơ chế cho MÃ SẢN PHẨM**; **Phụ lục F** là **danh mục cho BỘ ĐO** — tầng đã tiêu tốn **nhiều vòng nhất**
trong giai đoạn gần đây mà **không** tạo ra giá trị người dùng.
