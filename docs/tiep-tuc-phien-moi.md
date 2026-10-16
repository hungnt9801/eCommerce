# 🚩 BẮT ĐẦU Ở ĐÂY — GÓI BÀN GIAO CHO PHIÊN MỚI

> **Tệp này dành cho phiên làm việc MỚI.** Đọc hết tệp này (~5 phút) rồi mới làm việc — nó thay cho việc đọc lại ~197 commit.
> 📌 Gói này được viết ở **cuối phiên 108 vòng** (16/10/2026) khi context gần cạn.

---

## 1. TRẠNG THÁI ĐÃ KIỂM CHỨNG (số liệu THẬT, đo ngay trước khi đóng gói)

| Hạng mục | Giá trị |
|---|---|
| Nhánh / commit | `main` · **197 commit** · **cây sạch** (0 thay đổi treo) |
| Build | `dotnet build eCommerce.slnx -v q --no-incremental` ⇒ **exit 0 · 0 dòng `': error '` · DLL có** |
| Unit test | **746/746** (`test\eCommerce.Tests`) |
| Integration test | **38/38** (`test\eCommerce.IntegrationTests`, PostgreSQL **thật** qua Testcontainers) |
| Sơ đồ | `npm --prefix tools run check:mermaid` ⇒ **197 khối · 0 lỗi** |
| Schema | **KHÔNG** đổi ở các vòng gần đây (đếm unique index `NULLS NOT DISTINCT` = **40**, xem mục 4.1) |

**Tự kiểm lại bằng 3 lệnh** (làm TRƯỚC khi tin bất cứ điều gì trong tệp này):
```powershell
cd D:\Projects\Tools\eCommerce
dotnet build eCommerce.slnx -v q --no-incremental   # phải: exit 0, 0 dòng ': error '
dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo
dotnet test test\eCommerce.IntegrationTests\eCommerce.IntegrationTests.csproj --nologo
```

---

## 2. BA TỆP PHẢI ĐỌC TRƯỚC KHI SỬA BẤT CỨ THỨ GÌ

| Tệp | Vì sao |
|---|---|
| `docs\handover-gd1.md` | quy trình kiểm chứng **§2** + **Phụ lục B** (ánh xạ ràng buộc DB) · **C** (cơ chế cho job) · **D** (route + hàm chờ) · **E** (trạng thái & việc tiếp theo) · **F** (danh mục kiểm probe) |
| `tools\README-probe.md` | **danh mục kiểm Phần A–H** khi viết probe: cú pháp trước · dùng `edit` · mỗi việc một dòng · chờ bằng công cụ · kiểm điểm đo · route thật · **không lọc stderr** · **LUẬT form Web (token chống giả mạo)** · và các bẫy H1/H2 (kiểm build · chèn mã vào initializer) |
| `docs\implementation-plan.md` | §7.1 — bảng việc GĐ1 và trạng thái từng việc |

📌 Tài liệu **tính năng** nằm ở `docs\features\<mã>-<tên>.md` (khoảng 60 tệp, mỗi tệp 40–270 KB, đánh số **§** liên tục).
Tệp lớn nhất về mặt tiến độ GĐ1: `web-001-…` (270 KB — chứa §47–§94 về mặt tiền Web), `inv-001-…` (262 KB), `prc-002-…` (151 KB).

---

## 3. ĐÃ XONG VÀ **ĐÃ KIỂM CHỨNG** (không cần làm lại)

| Nhóm | Nội dung |
|---|---|
| **Hạ tầng nền §7.0** | `eCommerce.Kernel` (Money · Locking · Observability · ánh xạ ràng buộc DB) · **3 cơ chế tự bảo vệ** · **7/7 job nhìn thấy được** trong log |
| **Nghiệp vụ** | `CAT-001/002` · `PRC-001/002` (**sổ giải trình** — mọi con số bán hàng lấy từ đây) · `INV-001` (nhập/chuyển/kiểm kê/**sổ cái append-only**) · `INV-002` (giữ chỗ + job nhả chỗ) · **`SAL-001…004`** (đặt → xác nhận → huỷ → trả hàng, kể cả **luỹ kế** + `CustomerReturn`) · `INV-003` **bước 1 + cưỡng chế hạn mức** · `REC-001` **đối soát tồn kho** (job phát hiện + đếm đúng) |
| **Chính sách "cho phép chọn"** | 7 setting ABP (`Recon.*`, `Inv.ChannelLimit.*`) + hàm đọc thuần + **job/log** chính sách + **cưỡng chế ở đường giữ chỗ** (mã lỗi `eCommerce:Inventory:0042`) |
| **Web (khép kín, không cần gõ URL)** | cổng đăng nhập · harness đăng nhập · **điểm đo thống nhất** `/health-status` cho **cả** Host và Web · 3 trang hai chiều (`/PhanBo` · `/SanPham` · `/DatHang`) · **giỏ tới 5 dòng** (gộp SKU trùng xuyên dòng · ca biên · giữ dòng **cả hai đường** · **sống qua phiên** bằng cookie · nút xoá giỏ · **chọn số dòng 1..5**) · **`/DonHang` đủ BA hành động** (xác nhận · huỷ · trả hàng — đo tới **tồn kho + chứng từ**) · **lối vào `/DonHang`** từ trang kết quả · **chi tiết SP + SEO** (`/SanPham/ChiTiet/{sku}`) |
| **Công cụ** | `tools\wait-host.ps1` · `tools\route-check.ps1` · `tools\README-probe.md` |

---

## 4. 🔒 QUY TẮC BẮT BUỘC (mỗi điều đã TRẢ GIÁ ít nhất một lần)

### 4.1 Schema & mã
1. **Mọi unique index có `TenantId` phải viết tay `NULLS NOT DISTINCT`** (1 tenant ⇒ `TenantId = NULL`). Số lượng index này **được assert** trong `PostgreSqlSchemaTests.F1_…` — **hiện là 40**; thêm/bớt index ⇒ **phải cập nhật số**.
2. **Mọi entity mới phải có `DbSet<>` trên `eCommerceDbContext`** (guard: `DbSetRegistrationTests`).
3. **Mọi worker**: `UoW` (`CreateScope` + `IUnitOfWorkManager.Begin` + `CompleteAsync`) · **log VÔ ĐIỀU KIỆN ở đầu `DoWorkAsync`** · **`Timer.RunOnStart = true`** (guard: `BackgroundWorkerCoCheTests`).
4. **Khoá phân tán KHÔNG thêm tiền tố `ecommerce:`** (ABP `KeyPrefix` đã thêm).
5. **Mã lỗi nghiệp vụ phải đúng NGHIỆP VỤ, không chỉ đúng kỹ thuật** (bài học `0040`/`0042`: mượn `0001` ⇒ CSKH nhận **chỉ dẫn sai**).

### 4.2 Web
6. **Mọi `POST` từ trang Web phải mang token chống giả mạo** ⇒ `.cshtml` dùng `@Html.AntiForgeryToken()`; render trong C# thì inject `IAntiforgery`. **Thiếu ⇒ ABP trả `400`** (đã xảy ra 2 lần).
7. **Route mặc định là `{controller}/{action}/{id?}`** ⇒ tham số **không tên `id`** sẽ **KHÔNG** bind từ URL ⇒ **luôn `404`**. Phải có `[HttpGet("Duong/Dan/{thamSo}")]` (đã trả giá ở §91).
8. **Dùng `Url.Action`** (không ghép URL) cho liên kết; **`class`** thay `id` khi lặp theo dòng.
9. **Ứng dụng Web chạy trong tiến trình**: dùng app service của module nào ⇒ module đó phải có trong `[DependsOn]` **và** `ProjectReference` của `eCommerce.Web`.

### 4.3 Đo lường (đây là tầng đã tiêu tốn nhiều vòng nhất)
10. **Kiểm build ĐÚNG CÁCH**: `$LASTEXITCODE -eq 0` **+** đếm dòng `': error '` **+** kiểm **tệp DLL tồn tại**. ⚠️ `Select-String '0 Error'` là **KHÔNG đáng tin** (dotnet in tổng kết theo từng dự án).
11. **Chờ tiến trình bằng `tools\wait-host.ps1`** (chờ `/health-status` = 200; hết hạn ⇒ **in đuôi log**) và **LUÔN in lại đầu ra của công cụ chờ** (đừng chỉ in `exit code`).
12. **Trước khi gọi API: in route thật** bằng `tools\route-check.ps1 -Match '<mẫu>'`.
13. **Không lọc `stderr`**; **đo điều kiện tiên quyết** trước khi kết luận.
14. **Giải mã HTML (`WebUtility.HtmlDecode`) trước khi so nội dung hiển thị** (Razor encode `×`→`&#xD7;`…) — **đã sai 6 lần**.
15. **Đọc MÃ LỖI, không chỉ HTTP status** — cùng `403` có thể là **nhiều nguyên nhân khác nhau**.
16. **Tạo lỗi CÓ CHỦ ĐÍCH** khi kiểm đường xử lý lỗi (*"tính năng cứu khi có lỗi" phải được đo Ở ĐƯỜNG LỖI*).
17. **Phép đo hỏng ≥ 2 lần ⇒ TÁCH** thành phép đo nhỏ nhất còn trả lời được câu hỏi (bài học #13).
18. **`git commit` PHẢI nằm TRONG `if (đạt)`** — đã chặn được **5 lần** ghi sổ kết luận sai. **Không** bao giờ để lệnh commit chạy vô điều kiện sau một phép kiểm.
19. **Điều kiện `đạt` CHỈ chứa những gì THUỘC phạm vi thay đổi này**; phần chưa làm ⇒ **ghi là nợ**, không đưa vào phép kiểm.
20. **Không dùng tên biến tự động chỉ-đọc** của PowerShell (`$pid` · `$home` · `$host` · `$input` · `$args` …) làm tham số/biến. **Chuỗi chứa `$` ⇒ nháy ĐƠN**.

### 4.4 🚨 Cache (bẫy MỚI, rất dễ mất một vòng)
21. **Setting của ABP được CACHE trong Redis và cache SỐNG QUA restart Host.**
    Sửa thẳng `AbpSettings` trong DB **KHÔNG** có tác dụng cho tới khi xoá cache:
    ```powershell
    docker exec -i ecommerce-redis redis-cli --scan --pattern '*TenSetting*'   # xem key
    docker exec -i ecommerce-redis redis-cli DEL "<key>"                        # xoá
    ```
    📌 Đường ghi **chuẩn** của ABP (`ISettingManager.SetGlobalAsync` / API quản trị) **tự** xoá cache — nhưng **hiện chưa nạp** HttpApi của SettingManagement (xem nợ mục 5).

---

## 5. ⚠️ VIỆC ĐANG DỞ / NỢ — theo thứ tự ĐỀ XUẤT

| # | Việc | Vì sao / ghi chú |
|---|---|---|
| **1** | ~~**`INV-003` — bảng hạn mức RIÊNG từng kênh** (`InvChannelLimits`) **+** **`REC-001` — bảng `ReconRuns`**~~ | ✅ **XONG 16/10/2026** — một migration `Inv003_ChannelLimits_Rec001_ReconRuns` · `inv-003` **§16** · `rec-001` **§16**. Tuân thủ 4.1 (`NULLS NOT DISTINCT` **40 → 42**, `DbSet`, danh sách đích danh) |
| 2 | ~~**`PhamVi` (`Channel` vs `ChannelWarehouse`) "có ô chọn mà chọn không đổi gì"**~~ | ✅ **XONG 16/10/2026** — `HanMucKenh.ChonHanMuc` quyết định dòng nào được xét; dòng theo kho bị **bỏ qua** thì **ghi cảnh báo**, không im lặng (`inv-003` §16.3) |
| 3 | **`Percent` theo TỔNG của kênh** | ⏳ **CÒN** — hiện tính trên tồn khả dụng của **từng `(kho, SKU)`**; tồn kho **chưa** mô hình hoá theo kênh |
| 4 | ~~**Đường GHI chuẩn cho admin** (`ISettingManager` **hoặc** HttpApi của ABP)~~ | ✅ **XONG 16/10/2026** — `ChinhSachHanMucAppService` (`ISettingManager` ⇒ cache 2 tầng tự xoá) + API hạn mức kênh · `inv-003` **§17**. 📌 HttpApi của ABP **đã nạp nhưng KHÔNG đủ** (đo bằng swagger: chỉ có `emailing`/`timezone`) |
| 5 | ~~**`REC-001`: áp NGƯỠNG thật**~~ | ✅ **XONG 16/10/2026** — `NguongLech` (**cơ sở = sổ cái**) + ảnh chụp chính sách ở `ReconRuns` + `KetLuan`; đo được: ngưỡng `999` ⇒ `CoVuotNguong=false` (`rec-001` §16.5) |
| 6 | **`CAT-003`: slug thay SKU** + `sitemap.xml`/`robots.txt` | 🟡 **LÁT CẮT 1 XONG 16/10/2026** — slug (bỏ dấu **kể cả `đ`**) + unique index `NULLS NOT DISTINCT` + `ProductSlugChangedEto` **có `OldSlug`** (Q3 của `WEB-002`) + `/sitemap.xml` + `/robots.txt` + **cơ chế allowlist công khai**; `cat-003` **§16**. ⏳ **CÒN**: **media** + **hiển thị theo kênh** (**0 dòng code**) · **storefront CHƯA công khai** (`/SanPham*` ⇒ `302` tới login ⇒ SEO chưa phát huy — cần người quyết)
| 7 | **Hoàn tiền** (`SAL-006`/`PaymentHub`) · **`CUS-001`** (hồ sơ khách + sổ địa chỉ) · **`SAL-005`** · **`REC-001` đối soát thanh toán `querydr`** | phần còn lại của GĐ1 |
| 7a | **Hoàn tiền `SAL-006`** | 🟡 **lát cắt 1+2 XONG 16/10/2026**: §16 bất biến tầng domain (chặn hoàn vượt · chưa thu tiền · số tiền `≤ 0`) + **vá 2 bug trạng thái CUỐI** · §17 **`RefundAsync`/`QueryStatusAsync` VNPAY CÀI THẬT** + **project `Application.Contracts` MỚI** + `PaymentAppService` + **4 endpoint** + **`RefundResult` thay `bool`** (3 trạng thái tách biệt) · **+39 unit test** (955 tổng) · **12/12 ca HTTP thật**. 🚨 **Phép đo bắt được LỖ HỔNG THẬT**: phản hồi **không đọc được** ⇒ `ChoXacNhan=false` ⇒ **retry mù** ⇒ **hoàn HAI LẦN** — đã vá. ⏳ **CÒN**: 🚨 **chưa kiểm chứng TIỀN THẬT RA** (không có credential sandbox) · **chưa có phiếu RMA** · chưa có app service **tạo** giao dịch · **chưa có webhook controller** · **chưa có job đối soát `querydr`** |
| 7b | **`REC-001` đối soát tồn kho: API/UI xem lịch sử lượt quét** + **quy trình xử lý lệch** + **retention** | bảng `ReconRuns`/`ReconRunMismatches` **đã có dữ liệu**, nhưng **chưa có đường đọc** cho người vận hành (`rec-001` §16.8) |
| 7c | **`CUS-001`** (hồ sơ khách + sổ địa chỉ) | 🟡 **lát cắt 1 XONG 16/10/2026** — **module `Customers` MỚI**: `Customer` + `CustomerGroup` + máy trạng thái ném lỗi + nâng cấp vãng lai (`ADR-07`) + **`PiiMasker`** + **`ICustomerDirectory`** + **3 unique index `NULLS NOT DISTINCT`** + permission + 5 endpoint + **+43 unit test** (889 tổng) — `cus-001` **§16**. ⏳ **CÒN**: 🚨 **sổ địa chỉ BỊ CHẶN** (`Address` **chưa tồn tại** trong `AddressHub`) · **chưa kiểm chứng HTTP** · **chưa nối đường bán hàng** ⇒ **khách `Blocked` vẫn đặt được hàng ở đường thật** · chưa API/seed `CustomerGroup` · chưa xuất dữ liệu cá nhân |
| 7d | **`SAL-005` watermark `D5`** | 🟡 **lát cắt 2 XONG 16/10/2026 (hạ tầng + lõi)**: `PayloadCanonicalizer` + `LuatWatermark` (4 nhánh) + **8 trường ETO** + **3 trường model** + **5 cột watermark** + index cờ lệch + `ApDungWatermark` **NÉM khi hạ watermark** · **+39 unit test** (928 tổng) — `sal-005` **§17**. ⏳ **CÒN**: **handler CHƯA nối luật** ⇒ cập nhật của sàn **vẫn bị bỏ** · **CHƯA có DLQ** · chưa có event log · **chưa có job đối soát `D12`** ⇒ cờ `GapSuspected` sẽ **tồn đọng** · **connector chưa trả `update_time`** ⇒ mọi ETO hiện có `AggregateVersion = null` |
| 9 | 🚨 **`CAT-003` còn 2 phần LỚN: MEDIA + HIỂN THỊ THEO KÊNH** | **0 dòng code** cho cả hai (`cat-003` §16.8 nợ 1+2) · media cần **hạ tầng lưu trữ** (`F4` §15.2 vẫn mở) và **magic bytes** (`ADR-13`) · hiển thị theo kênh cần `IChannelDirectory` (`ADR-07/08/09`) |
| 8 | **Đối soát `REC-002`/`REC-003`** (settlement sàn · COD) · báo cáo `REP-001…003` | sau các mục trên |

---

## 6. MÔI TRƯỜNG & CÁCH CHẠY (đã dùng suốt phiên trước)

| Thành phần | Giá trị |
|---|---|
| Thư mục | `D:\Projects\Tools\eCommerce` (nhánh `main`) |
| PostgreSQL 17 | container `ecommerce-postgres`, DB `ecommerce` · `docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A` |
| Redis 7 | container `ecommerce-redis` · `docker exec -i ecommerce-redis redis-cli …` |
| Host (API + auth) | cổng **5000** · `/swagger/v1/swagger.json` · `/health-status` |
| Web | cổng **5100** · `/health-status` (**đã thêm**, dùng chung điểm đo) |
| Đăng nhập admin | `admin@abp.io` / `1q2w3E*` · client `eCommerce_App` / `1q2w3e*` |
| 🔴 **Cờ bảo mật** | Đăng nhập Web **CHỈ** hoạt động khi `AbpUsers.ShouldChangePasswordOnNextLogin = false` cho `ADMIN@ABP.IO`. **Đặt false trước khi đăng nhập, TRẢ LẠI true khi dọn dẹp** (mọi probe đều làm vậy — quên ⇒ `302` thay vì `200`) |
| Probe | viết vào `%TEMP%`, **KHÔNG** để trong repo · hàm trợ giúp theo **Phần G** của `tools\README-probe.md` |

**Dọn dẹp sau mỗi phép đo** (bắt buộc): ghi SQL vào **tệp `.sql`** rồi chạy; xoá theo **tiền tố dữ liệu thử** (ví dụ `K94SKU-`, `GHSKU-`, `K94KHO-`…); `SELECT` xác nhận; **trả cờ bảo mật về `true`**; **xoá cache setting nếu đã đặt**; kiểm **không còn tiến trình `dotnet` treo** và **cổng 5000/5100 trống**.

---

## 7. CÁCH LÀM VIỆC HIỆU QUẢ (rút ra từ 108 vòng)

1. **Chia lời gọi NHỎ**: `read` → `edit` (mã) → `pwsh` (build) → `write` (probe) → `pwsh` (chạy) → `write` (doc + msg) → `pwsh` (`commit -F` + battery).
   ⚠️ Nhồi **một** lời gọi khổng lồ (kịch bản dài + Markdown dài + `git commit -m "…"`) đã **gây lỗi `tool input is invalid JSON`** làm **mất cả lượt**.
2. **Sửa mã bằng công cụ `edit`** — **không** `Replace` chuỗi trong PowerShell (đã gây lỗi cú pháp C# và làm hỏng tệp).
3. **Ghi tài liệu & thông điệp commit bằng TỆP** (`write`) rồi `git commit -F <tệp>` — tránh mọi lỗi thoát nháy/nội suy biến.
4. **Đếm số lần khớp neo TRƯỚC khi sửa** (mong đợi **1**); nếu 0 ⇒ **dừng lại**, đừng đoán.
5. **Một vòng = một lát cắt nhỏ, đo được, ghi sổ được** — và **mọi tuyên bố phải có số đo kèm theo**.
6. Tiếng Việt cho **mọi** trả lời và tài liệu; commit message **ASCII không dấu**.

---

## 8. PROMPT GỢI Ý ĐỂ DÁN VÀO PHIÊN MỚI

> Tiếp tục GĐ1 của `D:\Projects\Tools\eCommerce`. **Đọc `docs\tiep-tuc-phien-moi.md` trước**, rồi tự kiểm trạng thái bằng 3 lệnh ở mục 1. Làm **việc 1 ở mục 5** (bảng hạn mức riêng từng kênh `InvChannelLimits` + bảng `ReconRuns`, cùng một migration), tuân thủ **mục 4** (đặc biệt 4.1 về schema và 4.3 về cách đo), rồi **kiểm chứng bằng trạng thái THẬT** (DB/log/route) và **ghi tài liệu bằng tiếng Việt** vào `docs\features\inv-003-…md` và `docs\features\rec-001-…md`. Trả lời bằng tiếng Việt; commit message ASCII không dấu; **chỉ commit khi phép kiểm đã đạt**.
