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
| 4b | ✅ **`DonVi` (đơn vị) THEO TỪNG DÒNG hạn mức** (`INV-003` mục 5 việc 2) | ✅ **XONG 17/10/2026** — `inv-003` **§18**: cột **`DonVi` nullable** (`null` = **kế thừa** setting) + migration `20261016175736_Inv003_DonViTheoTungDong` (**áp lên DB thật**) + **10 unit test** + **7/7 ca DB/HTTP thật** · 🔑 **hai quyết định**: (`null` = *"không override"* — dùng `0` làm cờ sẽ tạo **hai cách diễn đạt cùng một ý**, và `Percent = 0` là **đơn vị hợp lệ**) · **trộn `DonVi` NGAY CẠNH việc chọn dòng** (🚨 `DongHanMuc` **và** `KetQuaHanMuc` là hai chỗ chứa ⇒ hai đường lấy sai; ca xấu nhất: `GiaTri` của dòng theo kho + `DonVi` của dòng cả kênh ⇒ `30` chiếc đọc thành `30%`, **không lỗi nào**). ⏳ CÒN: 🚨 **`Percent` vẫn tính trên tồn của TỪNG `(kho, SKU)`** — tồn **chưa** mô hình hoá theo kênh (việc 3) · chưa có nút "Sửa" điền sẵn · ✅ **ĐÃ KHOÁ CHỖ YẾU NHẤT 17/10/2026** (`inv-003` **§19**: tách `HanMucKenh.ChieuDongHanMuc` thành **hàm thuần** + **4 ca** khoá `DonVi` bị đánh rơi; **+4 ⇒ 1127**; 🚨 **đã tiêm lại đúng lỗi ⇒ 3 ca ĐỎ**). ⏳ 🚨 **VẪN CÒN**: **chưa có test tích hợp ĐẦU-CUỐI thật** cho `GiuChoAsync` (cả ba chặng đã có test **thành phần**, nhưng **đường thật** chưa chạy liền mạch — cần **fixture DB**) |
| 6 | **`CAT-003`: slug thay SKU** + `sitemap.xml`/`robots.txt` | 🟡 **LÁT CẮT 1 XONG 16/10/2026** — slug (bỏ dấu **kể cả `đ`**) + unique index `NULLS NOT DISTINCT` + `ProductSlugChangedEto` **có `OldSlug`** (Q3 của `WEB-002`) + `/sitemap.xml` + `/robots.txt` + **cơ chế allowlist công khai**; `cat-003` **§16**. ⏳ **CÒN**: **media** + **hiển thị theo kênh** (**0 dòng code**) · **storefront CHƯA công khai** (`/SanPham*` ⇒ `302` tới login ⇒ SEO chưa phát huy — cần người quyết)
| 7 | **Hoàn tiền** (`SAL-006`/`PaymentHub`) · **`CUS-001`** (hồ sơ khách + sổ địa chỉ) · **`SAL-005`** · **`REC-001` đối soát thanh toán `querydr`** | phần còn lại của GĐ1 |
| 7a | **Hoàn tiền `SAL-006`** | 🟡 **lát cắt 1+2 XONG 16/10/2026**: §16 bất biến tầng domain (chặn hoàn vượt · chưa thu tiền · số tiền `≤ 0`) + **vá 2 bug trạng thái CUỐI** · §17 **`RefundAsync`/`QueryStatusAsync` VNPAY CÀI THẬT** + **project `Application.Contracts` MỚI** + `PaymentAppService` + **4 endpoint** + **`RefundResult` thay `bool`** (3 trạng thái tách biệt) · **+39 unit test** (955 tổng) · **12/12 ca HTTP thật**. 🚨 **Phép đo bắt được LỖ HỔNG THẬT**: phản hồi **không đọc được** ⇒ `ChoXacNhan=false` ⇒ **retry mù** ⇒ **hoàn HAI LẦN** — đã vá. ⏳ **CÒN**: 🚨 **chưa kiểm chứng TIỀN THẬT RA** (không có credential sandbox) · **chưa có phiếu RMA** · chưa có app service **tạo** giao dịch · **chưa có webhook controller** · **chưa có job đối soát `querydr`** |
| 7b | **`REC-001` đối soát tồn kho: API/UI xem lịch sử lượt quét** + **quy trình xử lý lệch** + **retention** | 🟡 **ĐƯỜNG ĐỌC + QUY TRÌNH XỬ LÝ LỆCH XONG 16/10/2026** — `DoiSoatAppService` **4 endpoint** (lịch sử lượt · chi tiết lượt · dòng lệch · xử lý lệch) · `ReconRunMismatch.DanhDauDaXuLy` (**3 bất biến** ở tầng domain: lý do + người **bắt buộc** · **không ghi đè bằng chứng** · **không sửa số đo**) · **4 cột** + **index CÓ ĐIỀU KIỆN** (`WHERE NOT "DaXuLy"`) · **`SoLechChuaXuLy`** (con số **hành động được**) · mã `Recon:0002`/`0003` · **+15 unit test** (975 tổng) · **19/19 ca HTTP thật** — `rec-001` **§17**. ⏳ **CÒN**: **retention** (bảng **chỉ tăng**, chưa có chính sách dọn — đối soát là **chứng từ**) · **✅ **UI Web `/DoiSoat` XONG 16/10/2026** (`rec-001` §18: 2 trang + mục menu, **22/22 ca HTML thật**) · **job đối soát chủ động `D12` của `SAL-005`** (index có nhưng **chưa có worker** ⇒ cờ tồn đọng) · chưa có metric cho đường xử lý lệch · chưa có endpoint tổng hợp *"số lần lệch theo cặp `(kho, SKU)`"* |
| 7c | **`CUS-001`** (hồ sơ khách + sổ địa chỉ) | 🟡 **lát cắt 1 XONG 16/10/2026** — **module `Customers` MỚI**: `Customer` + `CustomerGroup` + máy trạng thái ném lỗi + nâng cấp vãng lai (`ADR-07`) + **`PiiMasker`** + **`ICustomerDirectory`** + **3 unique index `NULLS NOT DISTINCT`** + permission + 5 endpoint + **+43 unit test** (889 tổng) — `cus-001` **§16**. ⏳ **CÒN**: 🚨 **sổ địa chỉ BỊ CHẶN** (`Address` **chưa tồn tại** trong `AddressHub`) · **chưa kiểm chứng HTTP** · **chưa nối đường bán hàng** ⇒ **khách `Blocked` vẫn đặt được hàng ở đường thật** · chưa API/seed `CustomerGroup` · chưa xuất dữ liệu cá nhân |
| 7d | **`SAL-005` watermark `D5`** | 🟡 **lát cắt 2 XONG 16/10/2026 (hạ tầng + lõi)**: `PayloadCanonicalizer` + `LuatWatermark` (4 nhánh) + **8 trường ETO** + **3 trường model** + **5 cột watermark** + index cờ lệch + `ApDungWatermark` **NÉM khi hạ watermark** · **+39 unit test** (928 tổng) — `sal-005` **§17**. ✅ **lát cắt 3 XONG 16/10/2026**: **handler ĐÃ nối luật watermark** (4 nhánh) + **DLQ `XC-002`** (`MsgDeadLetters`, **2 chế độ UoW**) + **+25 test** (1100 tổng) — `sal-005` **§18**. ⏳ **CÒN**: 🚨 **CHƯA ĐO ĐƯỢC RUNTIME** (không có đường phát `ChannelOrderPulledEto` — cùng loại nợ `CAT-003` §25.6) · **CHƯA có job `D12`** ⇒ cờ `GapSuspected` **tồn đọng** · ✅ **API đọc/xử lý DLQ XONG 16/10/2026** (`XC-002` §16: **4 endpoint**, quyền `Operations.DeadLetters`, **24/25 ca HTTP + DB thật**); ⏳ **chưa UI** · ✅ **job đối soát `D12` XONG 16/10/2026** (`sal-005` **§19**: **13/13 ca log + DB thật**; 🚨 **cố ý KHÔNG tự hạ cờ** `GapSuspected` · cảnh báo **theo TUỔI 24 giờ**) · 🚨 **không có bảng `AbpEventOutbox`** (§16.4) · 🔑 **quyền MỚI cần restart tiến trình** (§16.4 (2)) · **`Order` nội bộ chưa cập nhật** · **connector chưa trả `update_time`** ⇒ nhánh watermark **chưa có tác dụng** trên dữ liệu thật · 🚨 **không có bảng `AbpEventOutbox`** |
| 9 | 🟢 **`CAT-003` — BA PHẦN ĐÃ CÓ LÕI: MEDIA + SLUG/SEO + HIỂN THỊ THEO KÊNH** (16/10/2026) | ✅ **SLUG/SEO**: sinh slug bỏ dấu · unique index `NULLS NOT DISTINCT` · `sitemap.xml` + `robots.txt` (`§16`) · ✅ **MEDIA**: `NhanDangAnh` (**magic bytes**, `ADR-13`) · `ProductMedia` (`ADR-02`/`ADR-12`) · `IMediaStorage` + `MediaStorageNoiBo` (**chống thoát thư mục 4 lớp**) · `IProductMediaAppService` + `ProductMediaAppService` (**tải lên · sửa · đặt ảnh chính · xoá mềm**) · `CatMediaController` (**cửa công khai duy nhất**, đã allowlist) · **26/32 + 17/17 ca HTTP thật** (`§17`–`§21`) · ✅ **HIỂN THỊ THEO KÊNH**: `ProductChannelVisibility` + **bảng chuyển trạng thái ở tầng domain** (`ADR-08`) · migration `NULLS NOT DISTINCT` · **kiểm chứng DB thật**: cặp `(SP, kênh)` thứ hai bị chặn với `Key ("TenantId","ProductId","ChannelCode")=(null, …)` (`§23`) · **+26 test** (1071 tổng) · **NND 47→48**. ⏳ **CÒN**: ✅ **`IChannelDirectory` + API hiển thị theo kênh XONG 16/10/2026** — guard `0207`/`0209` **đã cưỡng chế**, **23/23 ca HTTP + DB thật** (`§24`); 🔑 quy ước `KeThua` ⇒ **XOÁ bản ghi** (tránh **bế tắc**); 🚨 guard `0209` **phải đi qua DANH SÁCH BIẾN THỂ** (`ChannelListing` trỏ `ProductVariantId`) · ✅ **`IChannelVisibilityResolver` XONG 16/10/2026** (`§25`: `LEFT JOIN` gom **một chỗ** + **+4 test**); ✅ **ĐÃ ĐO ĐƯỢC 16/10/2026** — endpoint **`POST hien-thi-kenh/tra-hieu-luc`** + **19/19 ca HTTP + DB thật**: 🚨 **sản phẩm KHÔNG có bản ghi VẪN hiển thị** (`HienThi` + `tuongMinh=false`) ⇒ **`LEFT JOIN` chạy ĐÚNG** (`§26`). ⏳ Còn: **interceptor runtime** kiểm **SQL thật** (**phòng ngừa hồi quy**) · chưa test **dữ liệu bẩn** · chưa đo **hiệu năng** · **ảnh xoá mềm trả `404`** (`§22`) · **cùng tên miền** · base64 (+33 %) · gốc kho mặc định là thư mục tạm · job dọn tệp mồ côi · chưa nén/thumbnail · chưa UI ảnh · job đối chiếu `F10` |
| 8 | **Đối soát `REC-002`/`REC-003`** (settlement sàn · COD) · báo cáo `REP-001…003` | sau các mục trên |

| 7e | **`WEB-001` ba trang mới (`/HanMucKenh`, `/DoiSoatThanhToan`, `/KhachHang`) + menu hai tầng + trang lỗi `403`/`404`** | ✅ **LÁT CẮT 4 XONG 16/10/2026** — `web-001` **§96**: **11/11 ca HTTP + DB thật** (`:5100`) · 🔑 **ĐÓNG MỤC (1) CỦA GĐ1** (`/HanMucKenh` = đường ghi chuẩn cho admin) · menu **HAI TẦNG** (quyền nhóm = **hợp của các con**) · trang lỗi trả **ĐÚNG mã** + **neo ASCII**. ⏳ CÒN: ✅ **ĐÃ KIỂM ĐƯỢC 17/10/2026** (`web-001` **§97**: probe **đăng nhập thật** ⇒ **22/22 ca HTTP + DB**; 🚨 **phát hiện 2 lỗi sản phẩm thật**: `MenuViewComponent` **chưa bao giờ render MỤC CON** + **căn cứ pháp lý chỉ nằm trong chú thích Razor**). 🚨 Luật mới: **sau mỗi build, KIỂM MỐC DLL** (build `exit 0` mà DLL không đổi). ✅ **`web-001` §98**: **`MenuRenderTests` (10) + `ViewAnchorTests` (2)** biến hai lỗi §97 thành **không thể tái diễn** — **đã tiêm lại đúng hai lỗi để chứng minh lưới BẮT**; **+12 test ⇒ 1113**; **E2E 6/6**. ⏳ CÒN: 🚨 **probe E2E vẫn là script TAY, chưa vào CI** · hàm `GiaiMa` bị **chép lại ở mọi probe** · chưa chọn **kho** ở `/HanMucKenh` · chưa phân trang + chưa *nâng cấp khách vãng lai* ở `/KhachHang` · 🚨 **chưa có xuất dữ liệu cá nhân** (Luật 91/2025/QH15) · 🚨 **chưa có sổ địa chỉ** · chưa có UI hoàn tiền (**cố ý**) |

| 7f | ✅ **Đối soát thanh toán `querydr` HÀNG LOẠT** (`REC-001` mục 7) | ✅ **LÁT CẮT 3 XONG 17/10/2026** — `sal-006` **§20**: thêm **`PostDoiSoatLoatAsync(toiDa)`** + **`KetQuaDoiSoatThanhToanDto`** (🔑 **tách BA con số** `SoDaXet`/`SoDoiTrangThai`/`SoKhongXacDinh` — gộp lại là biến *"KHÔNG KIỂM ĐƯỢC"* thành *"đã kiểm, không sao"*). 🔑 **vì sao cần hàm LÔ:** hàm một buộc người vận hành **biết trước** giao dịch nào cần kiểm — mà đó **chính là** thứ họ **không** biết; ⚠️ **IPN mất** ⇒ kẹt `Pending` ⇒ **không ai phát hiện** ⇒ **tiền đã vào mà hệ thống không biết**, **không lỗi nào**. 🔒 **hai bất biến:** ① chỉ xét giao dịch **CHƯA ở trạng thái cuối** (gọi cổng cho `Succeeded`/`Refunded` là **lãng phí quota**); ② **gọi lại CHÍNH hàm một** (nếu không thì bất biến **CHỈ TIẾN, KHÔNG LÙI** có **hai** chỗ phải đúng và chỗ thứ hai sẽ **lệch**). ✅ **7/7 ca DB + HTTP + LOG thật**; route `/api/app/payment/doi-soat-loat` **POST**. ⏳ CÒN: 🚨 **chưa có JOB ĐỊNH KỲ** gọi đối soát lô ⇒ **vẫn phải bấm tay** · chưa có UI đối soát lô · 🚨 `SoKhongXacDinh` **không phân biệt** *"chưa cấu hình"* với *"cổng chậm"* · chưa kiểm luồng **THU ĐƯỢC TIỀN** thật (🔒 **cần credential sandbox VNPay** — **nợ chặn**) · chưa có **metric** |
---
| 7g | ✅ **JOB ĐỊNH KỲ đối soát thanh toán** (`REC-001` mục 7) | ✅ **LÁT CẮT 4 XONG 17/10/2026** — `sal-006` **§21**: thêm **`LuatDoiSoatThanhToan`** (**hàm THUẦN tầng Domain**) + **`PaymentReconciliationWorker`** (nhịp **15 phút**, trần **50/lượt**, 🔑 **cũ nhất trước**) + đăng ký trong `OnPostApplicationInitializationAsync`. 🔑 **VÌ SAO LUẬT Ở DOMAIN, KHÔNG Ở APP SERVICE:** `PaymentAppService` mang `[Authorize]` cấp class (quy ước #4) ⇒ interceptor ABP kiểm quyền **ngay cả khi gọi trong tiến trình**, mà worker **không có principal** ⇒ **`AbpAuthorizationException`** — 📌 **lỗi ĐÃ XẢY RA THẬT** ở `SAL-005`; ✅ **một định nghĩa, hai chỗ gọi**. ✅ **20 unit test** (gồm 🔒 **quét LƯỚI BAO PHỦ mọi cặp trạng thái**) + **job CHẠY THẬT** (`[PAY-JOB-BAT-DAU]` → cảnh báo → `[PAY-JOB-XONG]`, DB `Status` **không đổi**). ⏳ CÒN: chưa có **metric** · 🚨 cảnh báo vẫn **chỉ là LOG**, chưa có **alert rule** (*"job phát hiện"* **≠** *"có người biết"*) · chưa kiểm luồng **THU ĐƯỢC TIỀN** thật (🔒 **cần credential sandbox** — **nợ chặn**) · nhịp/ngưỡng là **hằng số** · chưa có **UI** · chưa có **test tích hợp cho worker** |

| 7h | ✅ **METRIC cho đối soát thanh toán** + 🚨 **sửa 2 lỗi thật trong bảng đăng ký metric** | ✅ **XONG 17/10/2026** — `sal-006` **§22**: thêm **`payment_recon_changed_total`** (counter, nhãn `result`) + **`payment_recon_unresolved_total`** (gauge) + **`MetricDoiSoatThanhToan`** (🔑 hàm phát metric **dùng chung** ở **tầng Domain** — ⚠️ *chép hai chỗ* ⇒ hai bản **lệch** mà lệch ở **metric** thì **ÂM THẦM**; ⚠️ *đặt ở `PaymentHub.Application`* ⇒ `eCommerce.Application` **không** thấy nó; 🔒 *đặt trong `LuatDoiSoatThanhToan`* ⇒ **phá** tính **thuần** của luật). 🔑 **HAI metric vì HAI câu hỏi:** counter = *"đã phát hiện và sửa bao nhiêu"*, gauge = *"còn bao nhiêu ca chưa ngã ngũ"* — 📌 gộp lại thì *"KHÔNG KIỂM ĐƯỢC"* và *"đã kiểm và sửa"* trông **giống hệt nhau**. 🚨 **PHÁT HIỆN 2 LỖI THẬT:** (①) `InventoryReconMismatchSkus` **có** hằng số nhưng **thiếu** trong `All` ⇒ chú thích *"nguồn sự thật duy nhất"* **SAI**; (②) **`SetInventoryReconMismatchSkus` ghi vào biến NHƯNG CHƯA BAO GIỜ CÓ GAUGE** ⇒ 🚨 giá trị **MẤT**, metric **không bao giờ phát** — 🔑 loại hỏng **TỆ NHẤT**: job **vẫn chạy**, **vẫn ghi**, **không lỗi nào**, chỉ là **không ai** nhận được con số. ✅ Thêm ca **chiều NGƯỢC** (phản chiếu). ✅ **Bỏ `Assert.Equal(12, …)`** (🔑 con số cứng thành **RÀO CẢN**: mỗi metric **hợp lệ** mới làm nó **ĐỎ**). ✅ **1155 unit** (+7). ⏳ CÒN: 🚨 **cảnh báo vẫn CHƯA có alert rule** (*"có metric"* **≠** *"có cảnh báo"*) · **chưa có tầng XUẤT metric** (OTel→Prometheus) ⇒ `Meter` chỉ **phát**, không collector thì **không ai đọc** · chưa có **test tích hợp cho worker** · chưa kiểm **THU ĐƯỢC TIỀN** thật |
| 7i | ✅ **`tools\kiem-moc-build.ps1`** — chống bẫy *"build `exit 0` mà binary KHÔNG ĐỔI"* | ✅ **XONG 17/10/2026** — 🔑 **LÝ DO: ĐÃ CẮN BỐN LẦN** (`web-001` §97.6 · §98.6 · `sal-006` §21.6 · §22.7) ⇒ 🔒 một **quy tắc trong đầu** không đủ ⇒ phải là **script**. 🚨 **Bài học quan trọng nhất:** kiểm **BẢN COPY TRONG `bin` CỦA TIẾN TRÌNH SẼ CHẠY**, **KHÔNG** phải bản trong **thư mục dự án** (📌 đúng lỗi đã xảy ra ở §21.6). ✅ **Hai mã thoát KHÁC NHAU cho hai nguyên nhân KHÁC NHAU:** **0** = mọi tệp đổi (build đã vào) · **1** = có tệp **KHÔNG đổi** ⇒ ⚠️ **dừng tiến trình, chờ cổng trống, build lại** (**đừng** kết luận gì về sản phẩm) · **2** = phép kiểm **VÔ NGHĨA**. 📌 Đã ghi vào `tools\README-probe.md`; `.moc-build.json` vào `.gitignore` |
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

---

## 🚨 **MÃ LỖI PHẢI THEO TÀI LIỆU, KHÔNG TỰ CHỌN** (sự cố thật 16/10/2026, `cat-003` §19)

Ở `CAT-003` §16 tôi **tự chọn** dải `0026`–`0031` cho mã lỗi slug + media **trong khi §6.3 của CÙNG tài
liệu đó đã quy định dải riêng `0201`–`0212`** ⇒ **một mã hai nghĩa**.

🔒 **QUY TẮC (đã có tiền lệ chốt trong `CatalogErrorCodeRegistryTests`):** *số mã lỗi theo **TÀI LIỆU**;
**KHÔNG** sửa tài liệu cho khớp mã.* Trước khi thêm mã lỗi, **đọc mục "Mã lỗi nghiệp vụ" của chính
feature doc đó**.

📌 **Ba lỗi trong một lượt, và CẢ BA đều qua được build + 1081 test xanh:**
1. Tự chọn dải số sai (số nào cũng đúng định dạng `\d{4}` ⇒ không lưới nào bắt);
2. `DaCoAnhChinh = TepKhongPhaiAnh` — **alias** ⇒ hai nghĩa khác hẳn dùng một mã; và **alias là hai tên
   MỘT giá trị** nên `KHONG_co_hai_hang_so_nao_TRUNG_GIA_TRI` **cho qua**;
3. 🚨 `ViPhamRangBuocDb` ghi **CHUỖI CỨNG** ⇒ đổi hằng số thì chuỗi **ở lại giá trị cũ**; build + test
   **vẫn xanh** vì không gì buộc chuỗi khớp hằng số.

🔒 **Ba lớp chặn đã thêm:** (a) **sổ đăng ký** khoá số theo tài liệu; (b) dùng **hằng số** thay chuỗi
cứng trong `ViPhamRangBuocDb` (`Kernel` nay tham chiếu `Domain.Shared`, **không** vòng lặp); (c) **test alias**.

📌 **Bài học lớn nhất:** đây là lần thứ **TƯ** liên tiếp *"test xanh"* **không** đồng nghĩa *"đúng"*.
Cách phát hiện **luôn** là **đọc lại giá trị THẬT** và đối chiếu tài liệu — **không** dựa vào test.

### 🚨 **Kiểm ASCII TRƯỚC khi commit — và kiểm LẠI sau khi amend**
Bản nháp đầu bị hook chặn vì tôi viết ký tự **`§`** (ký hiệu đoạn mục kiểu tài liệu) — **2 byte × 12 = 24**.
🔒 **Quy tắc:** viết `muc 6.3`, **KHÔNG** viết `§6.3`; và **đếm byte > 127 TRƯỚC** khi gọi `git commit`.
⚠️ **Bẫy thêm:** lệnh `--amend` của tôi **dùng lại message CŨ** (không truyền `-F` đúng cách) ⇒ subject
**vẫn** còn ký tự lạ. ⇒ **Luôn kiểm `git log -1 --format=%s` SAU khi commit/amend**, không chỉ tin exit code.

## ⚠️ BÀI HỌC QUY TRÌNH (16/10/2026) — **KIỂM LẠI LỊCH SỬ TRƯỚC KHI "SỬA" TÀI LIỆU**

Trong phiên này tôi **suýt báo động sai**: thấy `docs/implementation-plan.md` có dòng
`| SAL-006 | Trả hàng / **RMA** & hoàn tiền (một phần / toàn bộ) |` chỉ dài **65 ký tự** ⇒ kết luận
*"regex của tôi đã xoá mất nội dung"*. **Sai.** Kiểm lịch sử (`git show <commit>:docs/implementation-plan.md`
cho **6** commit liên tiếp) ⇒ dòng đó **giống hệt ở MỌI commit** ⇒ **không có gì bị mất**.

📌 **Sự thật:** `SAL-006` thuộc **GĐ2**, mà bảng chi tiết trong `implementation-plan.md` **chỉ liệt kê GĐ1**
⇒ nó **chưa từng** có dòng chi tiết ở đó. Dòng tôi thấy là dòng **3 cột** của bảng "GĐ2 — Vận hành VN".

🔒 **Quy tắc rút ra (áp dụng cho mọi lần sửa tài liệu):**
1. **Trước khi kết luận "mất nội dung"**, chạy `git diff` / `git show <commit>:<file>` để so với **trước**.
   Cảm giác *"dòng này ngắn quá"* **không** phải bằng chứng.
2. **Regex `^\| <MÃ> \|` là NGUY HIỂM** khi cùng mã xuất hiện ở **nhiều bảng** (GĐ1/GĐ2/GĐ3): nó khớp
   **dòng đầu tiên**, không phải dòng bạn đang nghĩ tới. ⇒ **In ra dòng trước khi thay**, và kiểm **độ dài**
   sau khi thay.
3. **`-replace` không báo lỗi khi không khớp** ⇒ phải **in kết quả** để xác nhận (đúng bài học
   `tools\README-probe.md`: *"đo lại sau khi ghi"*).

---

## 🛡️ CƠ CHẾ MỚI: **CHẶN COMMIT MESSAGE CÓ DẤU** (16/10/2026)

**VÌ SAO CẦN CƠ CHẾ (không chỉ ghi quy tắc):** quy tắc *"commit message ASCII không dấu"* **đã có** trong
tài liệu này, nhưng trong phiên 16/10/2026 nó **bị vi phạm 1 lần** — commit `d06cb3d` có **6 byte ngoài
ASCII** vì **quên kiểm** trước khi commit, phải sửa bằng `--amend`.
📌 Đúng bài học **Phụ lục C**: *"ghi quy tắc KHÔNG ĐỦ, cần CƠ CHẾ"*.

### Đã làm

| # | Việc | Trạng thái |
|---|---|---|
| **1** | Hook `tools/git-hooks/commit-msg` (**shell**, dùng `sh` đi kèm Git for Windows) | ✅ **đã ĐO: chặn thật** |
| **2** | Cài vào `.git/hooks/commit-msg` (máy này) | ✅ đã cài |
| **3** | Kiểm chứng **hai chiều** | ✅ message có dấu ⇒ **exit 1** + in số byte/dòng vi phạm · message ASCII ⇒ **exit 0** |

### Cài đặt (hook KHÔNG được commit — `.git/` không nằm trong repo)

```powershell
# Cách 1 (khuyến nghị — một lệnh, không cần copy tệp):
git config core.hooksPath tools/git-hooks

# Cách 2 (copy):
Copy-Item tools/git-hooks/commit-msg .git/hooks/commit-msg -Force
```
📌 Git for Windows **có** `sh` đi kèm (`C:\Program Files\Git\bin\sh.exe`) và **tự dùng nó** để chạy hook —
**kể cả khi** `sh` không có trong `PATH` của PowerShell. (Đã đo: `Test-Path` = `True`.)

### 🚨 Ba cái bẫy gặp khi làm chính cơ chế này (ghi lại vì rất dễ tái diễn)

| # | Cái bẫy | Triệu chứng **thật** |
|---|---|---|
| **1** | `sh` **không có trong `PATH`** của PowerShell | Gọi `sh tools/git-hooks/commit-msg` ⇒ *"The term 'sh' is not recognized"*. **Không** có nghĩa hook vô dụng — Git tự tìm `sh` của nó |
| **2** | 🚨 **Script kiểm ASCII tự chết vì ký tự ngoài ASCII** | Phiên bản PowerShell của bộ kiểm in kết quả bằng **tiếng Việt có dấu + emoji** ⇒ `Write-Output` gặp lỗi **mã hoá console** ⇒ với `$ErrorActionPreference='Stop'` script **dừng trước khi in dòng nào** ⇒ **0 dòng output, exit 0** — trông y hệt *"chạy tốt"*. 📌 **Script kiểm ASCII mà không chạy được vì ký tự ngoài ASCII** |
| **3** | Bộ đo dùng `$env:TEMP` (đường dẫn **rút gọn 8.3**: `C:\Users\VNVC~1.HVT\...`) | Tiến trình con **không giải được** đường dẫn đó ⇒ script báo *"Không tìm thấy tệp message"* ⇒ **tưởng script hỏng**, thật ra **tệp thử không tồn tại** với tiến trình con |

📌 **Điểm chung:** cả ba đều cho triệu chứng *"có vẻ không chạy"* nhưng nguyên nhân ở **ba tầng khác nhau**
(`PATH` · **nội dung chính script** · **bộ đo**). Cách phân biệt: **gọi bằng đường dẫn tuyệt đối + `-Command`
+ in từng bước**, thay vì kết luận từ việc "không thấy output".
