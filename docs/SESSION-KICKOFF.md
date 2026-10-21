# 🚀 SESSION KICKOFF — dán tệp này khi bắt đầu mỗi phiên

> **Cách dùng:** mỗi lần mở phiên mới, gửi **toàn bộ tệp này** (hoặc đường dẫn + câu *"đọc SESSION-KICKOFF rồi làm phiên S…"*).
> Cuối tệp có ô **「PHIÊN NÀY」** — điền số phiên (`S1`, `S2`, …) trước khi gửi.
> 📌 Tệp này **không** thay bàn giao; nó là **cửa vào**: nó nói *đọc gì*, *luật gì*, *số đo nào là chuẩn*, *phiên này làm gì*.

---

## 1. Bối cảnh trong 5 dòng

Dự án **eCommerce đa tenant / đa kênh** (.NET 10 · ABP 10.7 · PostgreSQL 17 · Redis 7 · MVC/Razor), tại
`D:\Projects\Tools\eCommerce`. Giai đoạn 1 = *"bán được trên website"*. **Tài liệu thiết kế (55 tệp) đã đủ**,
**`ADDR-004` đã duyệt (1/55)**. Việc còn lại là **code** — đã chia **11 phiên** (`S0`…`S10`).
🔑 **Bài học lớn nhất của dự án:** *"một phép đo bất ngờ thường là phép đo SAI, không phải sản phẩm sai"* —
và phiên trước từng **nói quá "GĐ1 hoàn thành"** vì **không đo lại**.

---

## 2. Đọc gì trước — ĐÚNG THỨ TỰ NÀY, đừng đọc hết

| # | Tệp | Đọc để biết |
|---|---|---|
| 1 | `docs/gd1-coding-sessions.md` | **Phiên này làm gì** + cổng ra + phụ thuộc (§4) · các quyết định chặn (§3) |
| 2 | `docs/handover/BAN-GIAO-GD1-2026-10-17.md` | Ràng buộc schema/ABP (§4) · công cụ kiểm (§5) · **bảy cái bẫy** PowerShell/C# (§5.1) · kỷ luật ĐO (§5.2) |
| 3 | `docs/features/README.md` | Danh mục 55 tài liệu theo module + trạng thái thật |
| 4 | **Tài liệu feature của phiên** (1–2 tệp, `docs/features/<module>/…`) | Thiết kế + BR/NFR + test case. 📌 **Chỉ đọc tài liệu của phiên mình** |
| 5 | `docs/design-doc-guidelines.md` §4–§5 | Quy tắc vàng *"code lệch tài liệu ⇒ sửa tài liệu TRƯỚC, cùng PR"* |
| 6 | `tools/README-probe.md` | **Trước khi viết probe mới** — danh mục cổng đã có, dùng lại |

⚠️ **Đừng** đọc cả 55 tài liệu (≈4,8 MB). Ngân sách ngữ cảnh của một phiên = **1–2 tài liệu feature**.

---

## 3. Trạng thái ĐO ĐƯỢC — baseline chuẩn (đo lại **21/10/2026** cuối `S8`, `-DayDu` exit 0)

```
[B1]  dọn tiến trình + cổng trống      1.2s     ← nay kiểm CẢ 5000 và 5100
[B2]  build + KIỂM MỐC nhị phân       44.3s   · 71 tệp DLL/EXE đổi mốc
[B3]  unit test                        3.5s   · 1399/1399
[B4]  integration (PostgreSQL thật)    8.7s   ·   71/71
[B5]  mermaid (parser thật)            4.9s   ·  213 khối · 0 lỗi
[B6]  bất biến DB                      6.8s   ·   25/25 · NND = 53 · 62 quyền · quét 86 vị trí probe
[B7]  test cho chính các cổng         84.6s   ·   24 ca (A…I + R…V + L…P + W)
[B8]  E2E có phiên đăng nhập          39.1s   ·  123/123   ← 🆕 `S8` +21 ca (7 storefront CÔNG KHAI + 14 thanh toán end-to-end) · ➕ KHỞI ĐỘNG CẢ `HttpApi.Host`
[B9]  sổ địa chỉ HTTP thật            12.9s   ·   17/17
[B10] khách hàng HTTP thật            16.2s   ·   26/26
[B11] thanh toán HTTP thật            13.9s   ·   26/26
[B12] đơn sàn — ĐƯỜNG CHẠY THẬT       22.7s   ·   49/49
      ─────────────────────────────────────
      TỔNG                           258.8s
```

📌 **Đo BA lần trong phiên `S6`** — và **ba lần KHÁC số bước/số ca**, nên đừng so tổng:
• **ĐẦU phiên** (trước khi sửa, **11 bước**, `exit 0`): **259.0s** với unit **1347** / integration **57** /
NND **52** / bất biến DB **24/24** / **85** chỗ quét ⇒ **mọi số khớp `S5`** (thời gian cao hơn ~20 % là
**máy/nhiệt độ**). 🔑 **Đây là bằng chứng baseline `S5` vẫn đúng**, không phải suy đoán từ tài liệu.
• **GIỮA phiên** (**12 bước**, bước ⑫ có **39 ca**): **273.2s**.
• **CUỐI phiên** (**12 bước**, bước ⑫ có **49 ca** — thêm cổng hạ cờ): **290.8s**.
• 🆕 **`S7` đo HAI lần**: **ĐẦU phiên** (**12 bước**, `exit 0`): **301.5s** với unit **1367** / integration **67** /
NND **53** / DB **25/25** / **61** quyền / E2E **67/67** ⇒ **mọi số khớp bảng `S6`** ở trên (⚠️ riêng **thời gian
cao hơn ~11 %** — máy, không phải sản phẩm). **CUỐI phiên**: **241.8s** như bảng trên.
• 🆕 **`S8` đo HAI lần**: **ĐẦU phiên** (**12 bước**, `exit 0`): **260.3s** với unit **1395** / integration **71** /
NND **53** / DB **25/25** / **62** quyền / **86** chỗ quét / E2E **102/102** ⇒ **mọi số khớp bảng `S7`** (⚠️ thời
gian cao hơn ~8 % — máy). **CUỐI phiên**: **258.8s** như bảng trên, **cùng 12 bước**; **lần chạy lại trên cây ĐÃ COMMIT: 274.2s · `exit 0`**
— 📌 **cùng số ca** ở cả ba lần (1399 · 71 · **123/123** · 25/25 · NND 53 · 62 quyền), chỉ khác **thời gian** ⇒ dao động của **máy**.
📌 E2E nay khởi động **cả** `HttpApi.Host` (:5000) — xem bài học `S8` ngay dưới.
📌 **`S7`/`S8` còn một lần dừng vì MÔI TRƯỜNG (không phải mã):** Docker Desktop đã tắt sau khi máy khởi động lại ⇒
`docker compose up -d` lại `ecommerce-postgres` + `ecommerce-redis` rồi mới chạy được probe. 🔑 **Triệu chứng**
là `psql THAT BAI … failed to connect to the docker API` — 📌 dừng lại và **đọc thông điệp** là đủ để không đi
sửa mã nguồn.
📌 **Và một lần cổng ĐỎ THẬT giữa phiên `S7`** (đáng ghi vì nó là *bài học của chính bước ⑥*): lần chạy thứ hai
bị **huỷ giữa bước ⑪** ⇒ để lại **1 dòng `PayTransactions`** + **cờ `ShouldChangePasswordOnNextLogin = false`**
⇒ lần chạy kế tiếp **ĐỎ ở bước ⑥** (2 bất biến: *"① phải rỗng"* và *"② bảo mật"*). ✅ Dọn tay rồi đo lại ⇒ **xanh**.
🆕 **`S8` cũng có một lần cổng ĐỎ THẬT — và nó là bài học MỚI:** thêm `HttpApi.Host` vào bước ⑧ nghĩa là **job nền
của Host chạy ngay** (`InventoryReconciliationWorker`, `Timer.RunOnStart = true`) ⇒ job **ghi một dòng lệch THẬT**
vào `ReconRunMismatches` cho **dòng tồn kho probe** (`OnHand=100`, sổ cái `0`) ⇒ **bước ⑥ ĐỎ** (`TONG=5` sau **5**
lần chạy E2E). ✅ Dọn theo **SKU probe** + **đếm lại** (`conLechProbe`). 🔑 Câu phải hỏi mỗi lần thêm tiến trình
vào phép đo: ***"job nào chạy khi nó lên, và nó ghi cái gì?"***
🔑 **Vì vậy bước ĐẦU TIÊN của mọi phiên là ĐO LẠI** (mục 6) — nếu số khác bảng trên thì **dừng, tìm hiểu trước khi sửa**.
📌 **Cái ĐO ĐƯỢC phải khớp; cái KHÔNG khớp thời gian là máy** — nhưng ⚠️ **thời gian vẫn phải ghi**:
`B5` lệch **46.5s** so với **5.1s** ở đầu phiên `S6` (cùng mã, cùng tài liệu) và **4.2s** ở cuối `S7` ⇒
nguyên nhân là **`npm`/máy**.

**Code:** **354 tệp · 44.288 dòng** — 📌 **phương pháp (ghi rõ để không trôi):**
`Get-ChildItem src, modules, shared -Recurse -Filter *.cs`, **loại** `\bin\`, `\obj\`, `*.Designer.cs`,
`*Snapshot.cs`, rồi **đẩy NỘI DUNG qua** `| Measure-Object -Line` (tức **bỏ dòng RỖNG** — ⚠️ nếu đẩy
**đối tượng tệp** vào `Measure-Object -Line` thì mỗi tệp chỉ được **1 dòng**, ra **345** và trông như *"code
co lại 99 %"*; đó đúng là **cùng một bài học** ở dạng **mới**: *phép đo bất ngờ thường là phép đo SAI*) ·
đo 21/10/2026 cuối `S8` · **115 tệp test · 18.540 dòng** (cùng phương pháp, trên `test/`) · **41 migration**
(82 tệp = 41 `X.cs` + 41 `X.Designer.cs`; ➕ tệp thứ 83 trong thư mục là `eCommerceDbContextModelSnapshot.cs`
— **không** phải migration).
🔑 **Vì sao phải ghi cả phương pháp:** ở `S4` tôi đếm bằng `.Count` (mọi dòng) ⇒ ra **47.191** (lệch **7.967**);
ở `S6` tôi đẩy **đối tượng tệp** vào `Measure-Object -Line` ⇒ ra **345** (lệch **42.092**) — 📌 và **lệch số dòng** khi bỏ dòng rỗng — **hai lần cùng một
loại lỗi, hai dạng khác nhau**. ✅ Dùng **đúng** phương pháp: `S4` = **40.291** · `S5` = **41.414** ·
`S6` = **42.437** · `S7` = **43.380** · `S8` = **44.288** ⇒ **+908 dòng cho 4 tệp sản phẩm mới**
(`DocSanPhamCongKhai` + `FakePaymentOptions` + `FakePaymentProvider` + `PayGiaLapController` — phần lớn là
**chú thích**, đúng quy ước dự án) + **+223 dòng test cho 1 tệp test mới**.
**Git:** nhánh `main` · **số commit TĂNG mỗi lần sửa tệp này** ⇒ đọc `git rev-list --count HEAD`
(đo lúc **kết thúc `S6`, trước commit sửa chính dòng này**: **285** — 📌 commit `S6` đầu tiên là `e46460a`) ·
✅ **cây SẠCH** · **`origin` = `https://github.com/hungnt9801/eCommerce`** · ⚠️ repo đang **PUBLIC**.
🔒 **NGUỒN SỰ THẬT LÀ LỆNH, KHÔNG PHẢI DÒNG CHỮ NÀY:**
`git rev-parse --short origin/main` · `git rev-list --count origin/main..HEAD` (mong đợi **0**).
📌 **Vì sao KHÔNG ghi hash chính xác:** commit **sửa chính dòng này** làm hash đổi ⇒ một con số hash ở đây
**không bao giờ** tự khớp. 🔑 Đây là *"con số commit không tự khớp với chính commit ghi nó"* — bài học đã ghi
ở bản `S4`, nay áp cho cả **hash**.

🚨 **Các con số ĐÃ TRÔI — dùng số ở trên, đừng dùng số cũ:**
| Số cũ (handover / `S2`…`S5`) | Số đo 18/10/2026 (cuối `S6`) | Vì sao |
|---|---|---|
| ~145–155s / 175.9s / 203.2s / 196.0s / **214.8s** (11 bước) | **290.8s** (**12** bước) | thêm bước ⑫ (49 ca); 📌 đổi theo máy/nhiệt độ ⇒ **không** phải chỉ tiêu (đầu phiên `S6` đo **259.0s**, cũng `exit 0`) |
| 17 / 22 ca test cổng | **24 ca** | `S6` thêm nhóm **`W`** (2 ca): *"bước của cổng bị xoá mà cổng vẫn xanh"* — đo **hai chiều** |
| 1181 / 1219 / 1228 / 1294 / 1347 / **1363** unit | **1367** | `S6` **+20** (11 ca nhật ký sự kiện · 5 ca hash nội dung nghiệp vụ · 4 ca reflection cho cổng hạ cờ) |
| 38 / 46 / 51 / **57** integration | **67** | `S6` **+10** (4 ca `NULLS NOT DISTINCT` + 6 ca `CHECK`/tenant của bảng nhật ký sự kiện) |
| 48 / 51 / **52** index `NND` · 53 / 57 / **58** quyền | **53 · 61 quyền** | `S6` thêm **1** unique index (`IX_SalMktEventLogs_…_Ver_Hash_Outcome`) ⇒ **1 migration**; **+3 permission** (`eCommerce.Sales.MarketplaceIntake` + `.View` + `.Reprocess`) ⇒ `AbpPermissionGrants` **58 → 61** |
| 21 / 22 / 24 / **25** bất biến DB | **25** | không đổi so với cuối §20; 📌 **bản `S5` ghi 23/23 là con số ĐÃ TRÔI** (thực đo đầu `S6` = **24/24**) |
| 197 / **213** khối mermaid | **213 khối** | không đổi ở `S6` (không thêm sơ đồ) |
| 59 / **67** ca E2E | **67 ca** | `S6` không thêm ca E2E (đơn sàn **chưa** lên UI — `S8`) |
| 84 / **85** vị trí quét probe | **86** | `S6` thêm bảng `SalMarketplaceEventLogs` (PK `uuid`) ⇒ phép **tự dò** quét thêm **1** chỗ — 📌 con số **tăng là ĐÚNG** |
| — (chưa từng có) | **49 ca** probe đơn sàn (bước ⑫) | 🆕 `S6` — **đường chạy thật** (`sync-now` ⇒ `ChannelShopSyncer` ⇒ handler ⇒ DB) **+ cổng hạ cờ** |
| 324 / 341 / 345 tệp · 38.460 / 41.414 / **42.178** dòng | **347 tệp · 42.437 dòng** | +6 tệp sản phẩm (4 tệp §20 + app service + hợp đồng §21) · test **111 tệp / 17.940 dòng** |

🆕 **`S7` — các con số MỚI (dùng bảng trên, đọc thêm dòng này để biết cái gì đã đổi):**

| Số cũ (cuối `S6`) | Số đo 18/10/2026 (cuối `S7`) | Vì sao |
|---|---|---|
| **1367** unit | **1395** | `S7` **+28** (10 ca xác nhận lượt · 13 ca luật retention · 5 ca đọc setting) |
| **67** integration | **71** | `S7` **+4** — `CK_ReconRuns_XacNhan_DuBo` đo **HAI CHIỀU** trên PostgreSQL thật |
| **53** `NND` · **61** quyền | **53 · 62 quyền** | `S7` **+1 permission** (`eCommerce.Reconciliation.Confirm`, khai **hai nơi**) ⇒ `AbpPermissionGrants` **61 → 62**; **NND giữ 53** (chỉ thêm **1 index THƯỜNG** cho retention, không thêm unique) |
| **40** migration | **41** | `S7` **+1** (`20261018104518_S7_XacNhanVaRetentionDoiSoat`): **3 cột** + **1 `CHECK`** + **1 index thường** |
| **67** ca E2E | **102** | `S7` **+35 ca** (mục `S7` của `kiem-e2e.ps1`: **13** hạn mức · **13** xác nhận · **9** retention) |
| **25** bất biến DB · **86** chỗ quét | **25 · 86** | **không đổi** — `S7` chỉ thêm **cột** (không thêm bảng), và thêm **1 dòng** vào danh sách **quyền bắt buộc** của `kiem-db.ps1` |
| **347** tệp · **42.437** dòng · test **111** tệp / **17.940** dòng | **350 tệp · 43.380 dòng** · test **114** tệp / **18.317** dòng | `S7` **+3 tệp sản phẩm** (`DoiSoatRetention` + `ReconRetentionWorker` + migration `Up()`) · **+3 tệp test** |
| **290.8s** | **241.8s** | ⚠️ **đổi theo máy** — 📌 đầu phiên `S7` đo **301.5s** (cùng mã **trước** khi sửa, `exit 0`) ⇒ **không** phải chỉ tiêu |

🆕 **`S8` — các con số MỚI (dùng bảng trên, đọc thêm dòng này để biết cái gì đã đổi):**

| Số cũ (cuối `S7`) | Số đo 21/10/2026 (cuối `S8`) | Vì sao |
|---|---|---|
| **1395** unit | **1399** | `S8` **+4** (`StorefrontCongKhaiTests`: ranh giới DTO công khai · bộ lọc `Published` **đếm = 1** · cấm app service cần quyền · sitemap dùng slug) |
| **71** integration | **71** | **không đổi** — `S8` **không** thêm migration/cột/bảng nào |
| **41** migration · **53** `NND` · **62** quyền | **41 · 53 · 62** | **không đổi** — không có thay đổi lược đồ, không thêm quyền (trang công khai **không** cần quyền) |
| **25** bất biến DB · **86** chỗ quét | **25 · 86** | **không đổi** — không thêm bảng; 2 route mới **không** có PK nên phép quét GUID **không** tăng |
| **102** ca E2E | **123** | `S8` **+21**: **7** ca storefront **CÔNG KHAI** (`S8-W1…W6`, đo bằng **phiên ẨN DANH**) + **14** ca thanh toán end-to-end (`S8-P1…P14`) |
| **24** ca test cổng | **24** | **không đổi** — không thêm bước nào (E2E **mở rộng bên trong**, không thành bước mới) |
| **350** tệp · **43.380** dòng · test **114** tệp / **18.317** dòng | **354 tệp · 44.288 dòng** · test **115** tệp / **18.540** dòng | `S8` **+4 tệp sản phẩm** (`DocSanPhamCongKhai` · `FakePaymentOptions` · `FakePaymentProvider` · `PayGiaLapController`) · **+1 tệp test** |
| **241.8s** | **258.8s** | ⚠️ **đổi theo máy** — 📌 đầu phiên `S8` đo **260.3s** (cùng mã **trước** khi sửa, `exit 0`) ⇒ **không** phải chỉ tiêu. ➕ E2E **+12.4s** vì nay khởi động **thêm** `HttpApi.Host` (3s lên + tắt) |
| — (chưa từng có) | **E2E khởi động `HttpApi.Host`** | 🆕 `S8`: thanh toán end-to-end **bắt buộc** hai tiến trình (Web :5100 + Host :5000) — 📌 và kéo theo **job nền của Host chạy ngay** (xem mục 3 phía trên, bài học `S8`) |

## 4. LUẬT KHÔNG THƯƠNG LƯỢNG — vi phạm là hỏng NGẦM

**Schema/DB**
1. 🚨 **MỌI unique index trên entity `IMultiTenant` PHẢI có `NULLS NOT DISTINCT`** — viết **SQL thô** trong migration (kèm `DROP INDEX IF EXISTS`).
2. 🚨 **Thêm/bớt unique index ⇒ sửa CÙNG PR**: con số **48** trong `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` **và** danh sách tên index. Không sửa ⇒ cổng đỏ **giả** (một index hỏng bị một index mới **bù**).
3. Tiền là **`decimal`**, **cấm `double`**, **cấm so tiền bằng `==`**.

**ABP — bốn cái bẫy đã trả giá**
4. 🚨 **`DbSet<>` phải có trên `eCommerceDbContext` cho MỌI entity** — ABP đăng ký `IRepository<T>` bằng cách **quét `DbSet`**, không quét model. Thiếu ⇒ **DI nổ lúc chạy ⇒ HTTP 500**, triệu chứng **không trỏ tới chỗ sai**.
5. 🔐 `[Authorize]` ở **cấp class** + **permission PHẢI được định nghĩa** (permission chưa định nghĩa ⇒ `403` cho **mọi** người, kể cả admin).
6. 🔒 **Background worker KHÔNG được gọi app service có `[Authorize]`** (interceptor từ chối principal ẩn danh) — đọc repository trực tiếp, chia sẻ **luật thuần** ở Domain.
7. `AbpExceptionFilter` biến `BusinessException` thành **HTTP 500** trên MVC ⇒ controller **chỉ** `catch BusinessException`, **không** nuốt guard.

**Phân tầng**
8. `Domain`/`Domain.Shared` **KHÔNG** tham chiếu `Application.Contracts`; `Application.Contracts` **KHÔNG** tham chiếu `Domain`. Hằng số độ dài để ở `eCommerceConsts` (`Domain.Shared`).

**Quy trình**
9. 🔒 **Code lệch tài liệu ⇒ SỬA TÀI LIỆU TRƯỚC, cùng PR.** Cập nhật §16 "đã xong / còn lại" của tài liệu feature.
10. 🔒 **Commit message PHẢI thuần ASCII** — hook `commit-msg` **chặn** (đã cắn 2 lần). Không dùng `§`, `Đ`, `→`.
11. 🔑 **Đo TRƯỚC khi kết luận** — in **giá trị thật**; tách đúng **vùng**; khẳng định **phép tiêm đã xảy ra**; đo **cả hai chiều**; **không tin `exit 0`** (kiểm mốc/cỡ DLL).
12. 🚫 **KHÔNG bypass kiểm soát bảo mật.** Cụ thể: **cấm** bỏ/vô hiệu `KiemChuKy` hay `VerifyWebhookAsync` của VNPay — ai gửi `vnp_ResponseCode=00` cũng thành **đơn đã trả tiền** ⇒ giao hàng miễn phí (`PAY-001` §7.7.4).
➕ **Hai bẫy MỚI, cả hai đều hỏng ÂM THẦM** (phát hiện ở `S1`, chi tiết: `tools/README-probe.md` **§B1/B2**):
13. 🚨 `dotnet ef migrations add … --no-build` dùng bản build của **STARTUP project** ⇒ nếu `src/eCommerce.DbMigrator` chưa build lại thì sinh **MIGRATION RỖNG** mà vẫn in `Done.` ⇒ **luôn mở tệp ra đọc `Up()`**.
14. 🚨 Tệp trong `Data/`, `Assets/`, `appsettings*.json` được **copy vào output** ⇒ sửa nguồn rồi chạy `dotnet test --no-build` là đọc **bản CŨ** (test đỏ/xanh vì lý do không phải điều mình tưởng) ⇒ **`dotnet build` lại** project test.

---

## 5. HAI CỔNG TÀI LIỆU — chạy trước mỗi commit

```powershell
npm --prefix tools run check:mermaid          # 213 khối · 0 lỗi
pwsh -File tools\kiem-link-tai-lieu.ps1       # 0 tham chiếu chết (thoát 1 nếu chết)
```

📌 Mermaid: **cấm `;`** trong text sơ đồ · **cấm `<...>`** làm placeholder (dùng `«...»`).

---

## 6. ĐỊNH NGHĨA "XONG" CỦA MỘT PHIÊN

- [ ] ① **Đo baseline TRƯỚC khi sửa** (`pwsh -File tools\kiem-tat-ca.ps1 -DayDu`) — ghi số, so bảng §3. Chênh lệch lớn ⇒ **báo, không tự diễn giải**.
- [ ] ② Làm **đúng phạm vi** của phiên (§4 của `gd1-coding-sessions.md`). Không ôm việc phiên khác.
- [ ] ③ **Migration + con số 48 + `schema.sql` + test** trong **cùng** thay đổi.
- [ ] ④ **Cập nhật tài liệu feature** (§16) **trong cùng phiên**.
- [ ] ⑤ **`-DayDu` XANH ở cuối phiên** — không mang cổng đỏ sang phiên sau.
- [ ] ⑥ **Báo cáo trung thực**: làm được gì / **chưa** làm được gì / **chưa kiểm chứng** gì. 📌 Câu *"xong"* chỉ được dùng khi ①–⑤ đã đo.
- [ ] ⑦ Nếu bị chặn bởi một quyết định: ghi vào **§15.1 câu hỏi mở** của tài liệu, **đóng phiên với cổng xanh**, **không** tự chọn đáp án rồi code tiếp.

---

## 7. QUYẾT ĐỊNH ĐÃ CHỐT — 🔒 KHÔNG MỞ LẠI

| # | Quyết định | Ở đâu |
|---|---|---|
| D1 | **`Address` (sổ địa chỉ) thuộc `modules/Address`**; `Customers` chỉ giữ bảng liên kết `CustomerAddressLink` | `ADDR-004` **ADR-01** + **10/10 câu hỏi đã chốt** (🟢 Approved 17/10/2026) |
| D2 | **KHÔNG** có value object `Address` ở `eCommerce.Kernel` | `ADDR-004` **ADR-02** |
| D3 | Hàng đợi chuẩn hoá địa chỉ ở **`Address.TrangThai`** (một nguồn), không có bảng `AddrNormalizationReviews` | `ADDR-004` **Q9** |
| D4 | Trần địa chỉ/khách = **cấu hình** (mặc định 20) · `CountryCode` + mã nullable **ngay từ migration đầu** | `ADDR-004` **Q1/Q7** |
| D5 | **VNPay: bypass được lớp NCC/mạng, TUYỆT ĐỐI KHÔNG bypass HMAC.** Merchant test **tự đăng ký được**; `FakePaymentProvider` (chỉ Development, fail-fast) + trang giả lập **ký HMAC thật** | `PAY-001` **§7.7** |
| D6 | Mã lỗi: **code là nguồn sự thật**; dải `eCommerce:Customers:0010…0016` = sổ địa chỉ | `cus-001` §6.2 · `ADDR-004` §6 |
| D7 | 🔒 **Giỏ hàng GĐ1 = GIỎ-COOKIE** (`gio-web`, 2 giờ). Bảng `OrdCarts` **bị loại** khỏi GĐ1 · `MaxLines` = **5** | `sal-001` (khối quyết định đầu tài liệu) |
| D8 | 🔒 **`Percent` hạn mức kênh tính trên tồn TỪNG `(kho, SKU)`** — là *trần an toàn theo kho*, không phải ngân sách kênh | `inv-003` · ✅ `S7` **đã đổi nhãn UI** (`inv-003` §20) |
| D9 | 🔒 **Xác nhận lệch đối soát = ĐÓNG CẢNH BÁO + audit**, **KHÔNG** tự sinh bút toán điều chỉnh tồn | `rec-001` · ✅ `S7` **đã làm** (`rec-001` §19) — permission `eCommerce.Reconciliation.Confirm` |
| D10 | 🔒 **Storefront MỞ CÔNG KHAI** (`/SanPham*` vào allowlist). Cấm lộ `CostPrice` + tồn chi tiết; allowlist phải có test | `web-002` · `cat-003` · `S8` |
| D11 | 🔒 **CI = GitHub Actions · `ubuntu-latest`** + vá `P-01…P-06`. ✅ Repo **đã có** + đã push: `https://github.com/hungnt9801/eCommerce` (17/10/2026) — ⚠️ **PUBLIC** ⇒ bí mật thật phải vào **GitHub Secrets** | `XC-007` · `S9` |

✅ **KHÔNG còn quyết định nào chờ** (17/10/2026: `Q1`…`Q7` đã chốt — xem D1…D11 ở trên). 🔒 **Không mở lại** các quyết định này; nếu buộc phải lệch, sửa tài liệu tương ứng **trước**.

---

## 8. 「PHIÊN NÀY」

> ✏️ **Điền trước khi gửi** (hoặc chỉ cần gửi: *"làm phiên S…"*).

| Mục | Giá trị |
|---|---|
| **Phiên** | `S…` |
| **Tên phiên** | *(lấy từ `gd1-coding-sessions.md` §4)* |
| **Tài liệu feature phải đọc** | *(1–2 tệp, lấy từ cùng bảng)* |

**Nhắc lại phạm vi các phiên** (chi tiết + cổng ra: `gd1-coding-sessions.md` §4):

| Phiên | Việc |
|---|---|
| `S1` | ✅ **XONG 18/10/2026** — `ADDR-004` phần A: entity `Address` + migration (**48→49**) |
| `S2` | ✅ **XONG 18/10/2026** — `ADDR-004` phần B: `CustomerAddressLink` + API + 4 quyền + PII (**49→51**) · ➕ **bước ⑨ mới**: `probe-so-dia-chi.ps1` (17/17 ca HTTP thật) |
| `S3` | ✅ **XONG 18/10/2026** — `CUS-001` **ĐÓNG**: nối `ChoPhepGiaoDichAsync` (🚨 **LỖI THẬT**: khách `Blocked` vẫn đặt được) + `CustomerGroup` API/seed + phân trang · ➕ **bước ⑩ mới**: `probe-khach-hang.ps1` (26/26 ca HTTP thật) |
| `S4` | ✅ **XONG 18/10/2026** — **`PAY-001` phần A: đường TẠO + cầu nối** (📄 `PAY-001` **§16**): tạo `PaymentTransaction` từ đơn/checkout (`TaoGiaoDichAsync`) · **`MerchantReference` ỔN ĐỊNH** (`SinhMaThamChieu`, cấm `Guid.NewGuid()` mỗi lần) · cập nhật **`Order.PaymentStatus`** qua **ETO** + luật hấp thụ · ⚠️ **outbox bền vững = NỢ** (chủ dự án chốt hoãn) · ➕ đóng `F-PAY-2/3/9`. Cổng ra: cöng "IPN gửi trùng ⇒ 1 giao dịch" ✅ **2 tầng** (domain + unique index **PostgreSQL thật**) · đơn chuyển `Paid` ✅ · unit **+66** · integration **+5** · `-DayDu` **exit 0** |
| `S5` | ✅ **XONG 18/10/2026** — **`PAY-001` phần B: IPN/webhook + hoàn tiền** (📄 `PAY-001` **§16.7**): endpoint **`GET /api/webhooks/vnpay`** `[AllowAnonymous]` + allowlist (ở `HttpApi.Host` — trả lời `Q2`) · **HMAC-SHA512** kiểm trên **query THÔ** ⇒ 🚨 **vá `F-PAY-1`** + phát hiện **lớp thứ hai** (byte chuỗi ký: `+` ↔ `%20`) + **fail-closed khi `HashSecret` rỗng** · **idempotency** (bảng `PayRefunds` + `CHECK` TIỀN: `RefundedAmount ≤ Amount`, `≥ 0`, `Amount > 0`) ⇒ 🚨 **vá `F-PAY-7`** (**mỗi giao dịch một UoW**) · **`PaymentRefundedEto`** ⇒ đơn sang `PartiallyRefunded`/`Refunded` · **job đọc** giá trị `MarkFailed` trả về (đóng nốt `F-PAY-9`) · **job đối soát phát sự kiện** · ➕ **bước ⑪ mới**: `probe-thanh-toan.ps1` (**26 ca HTTP thật**). Cổng ra: **IPN trùng ⇒ 1 lần ghi + bộ đếm 3** ✅ · **sai chữ ký ⇒ `400` + DB không đổi** ✅ · **hoàn vượt ⇒ `0002`** ✅ · unit **+53** · integration **+6** · `-DayDu` **exit 0 · 214.8s**. ⚠️ **Vẫn KHÔNG `Implemented`**: chưa kiểm chứng **tiền thật**, outbox chưa có, `Q6` còn mở |
| `S6` | ✅ **XONG 18/10/2026** — **`SAL-005` phần C: nhật ký sự kiện + `update_time` + ĐO RUNTIME** (📄 `SAL-005` **§20**): bảng **`SalMarketplaceEventLogs`** (append-only, **7 kết cục**) · **`MarketplaceEventLogWriter`** (**UoW riêng**, nhận biết trùng bằng **đọc lại DB**) · handler ghi **ĐÚNG MỘT dòng ở MỌI nhánh** · 🚨 **ghi watermark ngay lần nhận đầu** · **connector PoC trả `update_time` tất định** ⇒ `AggregateVersion ≠ null` · **`MarketplacePayloadHash`** · ➕ **bước ⑫ mới** (`probe-don-san.ps1`, **49 ca**) · ➕ **cổng vận hành hạ cờ** (`MarketplaceIntakeAppService` + **2 permission** + mã `0005` — 📄 `SAL-005` **§21**). Gate: **`-DayDu` exit 0 · 290.8s** · unit **1347 → 1367** · integration **57 → 67** · **NND 53 = 53** · bất biến DB **25/25** · quét **86** · **quyền 58 → 61** · **ca cổng 22 → 24** · 🚨 **2 lỗi thật**: **hash chống trùng băm cả `CorrelationId`** ⇒ **DLQ giả ở MỌI lần kéo lại** (**1358 test cũ không thấy**; **đo hai chiều**: tiêm mã cũ ⇒ `C31`/`C32` **ĐỎ**, DLQ **2 → 3**) + **intake mới không ghi watermark** ⇒ bản **cũ hơn** vẫn **ghi đè**. ⚠️ **Vẫn KHÔNG `Implemented`**: `Order` nội bộ **chưa cập nhật** khi áp watermark · chưa UI nhật ký · `Q5` mở · chưa kiểm tiền thật |
| `S7` | ✅ **XONG 18/10/2026** — **UI/API còn thiếu của Inventory + Reconciliation** (📄 `inv-003` **§20** · `rec-001` **§19**): `/HanMucKenh` **lọc theo KHO** (kèm *"(không gắn kho)"*) + **phân trang** + **ô chọn kho** khi đặt hạn mức + **đổi nhãn theo `Q4`** (*"trần an toàn của kênh theo từng kho"*) · 🚨 **xác nhận lệch = ĐÓNG CẢNH BÁO + audit** (`Q5` = A): **3 cột** + `ReconRun.XacNhan` + `CHECK` *"không xác nhận một nửa"* + permission **`eCommerce.Reconciliation.Confirm`** · **RETENTION**: setting `Recon.Retention.Days` (180, `0` = TẮT) + luật **THUẦN** + service **dùng chung** cho job `ReconRetentionWorker` và nút bấm (**XEM TRƯỚC** mặc định · **xoá CỨNG** · **không bao giờ** xoá `ReconRuns`/dòng chưa xử lý/dòng đang chờ xác nhận) · **phân trang** trang chi tiết. Gate: **`-DayDu` exit 0 · 241.8s** · unit **1367 → 1395** · integration **67 → 71** · **NND 53 = 53** · **quyền 61 → 62** · **E2E 67 → 95 ca** · 🚨 **3 lỗi thật** chỉ lộ khi đo HTML/DB: **xoá MỀM** thay vì cứng (retention **không** làm bảng nhỏ đi) · **`[Required]` trên DTO** chặn trước domain ⇒ người dùng thấy **tiếng Anh** thay vì `0005` · **`[Range]` trên `SoNgay`** làm `0` **không bao giờ** trả `0006`. ⚠️ **Còn nợ**: metric retention · GUID người xác nhận trên UI · chưa có nút "Sửa" điền sẵn |
| `S8` | ✅ **XONG 18/10/2026** — **Thanh toán trên UI + mở công khai `WEB-002`** (📄 `PAY-001` **§16.11** · `web-002` **§16**): `/DatHang` **tạo giao dịch** ⇒ `302` sang **cổng** ⇒ **trang kết quả** (`/DatHang/KetQua`, đọc trạng thái **từ DB**) · **`FakePaymentProvider`** (dev-only, **fail-fast**) + **trang giả lập `/pay-gia-lap`** (**ký HMAC-SHA512 thật**, gọi **IPN thật**) · mở **CÔNG KHAI** `/SanPham` + `/SanPham/ChiTiet/{slugOrSku}` qua **đường đọc công khai** (repository, **không** app service `[Authorize]`) · **`sitemap.xml` dùng SLUG**. Gate: **`-DayDu` exit 0** · unit **1395 → 1399** · integration **71 = 71** · **NND 53 = 53** · **quyền 62 = 62** · **migration 41 = 41** (không đổi lược đồ) · **E2E 102 → 123** · 📌 **4 phát hiện**: **`Payments:Fake:HashSecret` trong tài liệu KHÔNG THỂ CHẠY** (✅ đo **chiều ngược**: đổi khoá ⇒ 6 ca **ĐỎ**) · **`CostPrice` KHÔNG tồn tại trong lược đồ** · **bộ lọc `Published` viết 3 chỗ ⇒ tiêm 1 chỗ không làm test đỏ** · **`canonical` viết cứng làm test menu đỏ** (chú thích **cũng** bị quét). ⚠️ **Còn nợ**: **301 khi đổi slug** · read-model/cache · JSON-LD/OG · mức tồn kho · UI **hoàn tiền** · **chưa kiểm tiền thật** |
| `S9` | `XC-007` — CI thật (`.github` + khả chuyển Linux + vá F2) |
| `S10` | Đóng GĐ1 — nợ nhỏ + `-DayDu` cuối + bàn giao |

✅ **Không phiên nào còn bị chặn**: `Q7` xong (repo + push, 17/10/2026) ⇒ `S9` chạy được ngay; `Q3`…`Q6` đã chốt ⇒ `S7` ✅ **đã xong** (18/10/2026), `S8` ✅ **đã xong** (18/10/2026) ⇒ **`S9` / `S10` là các phiên còn lại**. 📌 `S9` cần `Q7` (đã có) nên **chạy được ngay**.
📌 **GĐ2** (`SHP-001…004`, `PRO-001…002`) **không** thuộc GĐ1 — cả 6 **0 dòng code**, là chuỗi phiên riêng.

---

## 9. Lịch sử tệp này

| Ngày | Thay đổi |
|---|---|
| 17/10/2026 | Bản đầu. Baseline `-DayDu` **exit 0 · 228.9s** (1181 unit · 38 integration · 213 mermaid · 21/21 DB · 22 ca cổng · 59/59 E2E). Ghi rõ **3 con số đã trôi**. 6 quyết định đã chốt + 5 chưa chốt |
| 18/10/2026 | Cập nhật sau `S1` + `S2`: baseline **175.9s · 9 bước** (1219 unit · 46 integration · 213 mermaid · **23/23** DB · **NND 51** · **57** quyền · **84** chỗ quét · 22 ca cổng · 59/59 E2E · **17/17** sổ địa chỉ). `S2` ✅ XONG ⇒ **`S3` là phiên kế tiếp**. Ghi rõ **phương pháp đo** kích thước mã (vì số cũ không ghi cách đo) |
| 18/10/2026 (sau) | Cập nhật sau `S3`: baseline **203.2s · 10 bước** (1228 unit · 46 integration · 213 mermaid · **23/23** DB · **NND 51** · **58** quyền · **84** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng). `S3` ✅ XONG ⇒ **`S4` là phiên kế tiếp** (`PAY-001` phần A). ➕ Bất biến DB mới: *`CusGroups` = đúng 3 nhóm seed* (thay *"phải rỗng"* sai) · 📌 kích thước mã **đo lại bằng phương pháp ghi rõ** và **cảnh báo không cộng trừ với số cũ** |
| 18/10/2026 (sau nữa) | Cập nhật sau `S4`: baseline **196.0s · 10 bước** (**1294** unit · **51** integration · 213 mermaid · **23/23** DB · **NND 51** · **58** quyền · **84** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng). `S4` ✅ XONG ⇒ **`S5` là phiên kế tiếp** (`PAY-001` phần B). ➕ **Phương pháp đo kích thước mã nay ghi ĐỦ** (thêm `Measure-Object -Line` = **bỏ dòng rỗng**) — nếu thiếu chi tiết đó thì lần đo `.Count` cho **47.191** dòng, lệch **7.967** ⇒ **suýt kết luận sai "code phình 20 %"**; ✅ với phương pháp đúng: **40.291** dòng, và **40.291 − dòng 7 tệp mới ≈ 39.224** = khớp số cũ. 📌 Ghi lại **3 việc còn nợ của `PAY-001`**: endpoint IPN (`S5`) · outbox bền vững (`XC-002`) · **chưa kiểm chứng tiền thật** |
| 18/10/2026 (cuối) | Cập nhật sau `S5`: baseline **214.8s · 11 bước** (**1347** unit · **57** integration · 213 mermaid · **23/23** DB · **NND 52** · **58** quyền · **85** chỗ quét · 22 ca cổng · **67/67** E2E · **17/17** sổ địa chỉ · **26/26** khách hàng · 🆕 **26/26 thanh toán**). `S5` ✅ XONG ⇒ **`S6`/`S7`/`S8` là các phiên kế tiếp** (độc lập nhau). ➕ **Bước ⑪ mới** (`probe-thanh-toan.ps1` — **HTTP thật**, tự ký HMAC bằng **biến môi trường**, **không** commit bí mật). ➕ `PayRefunds` vào nhóm ① của `kiem-db.ps1` (bảng TIỀN ⇒ rác ở đó làm **báo cáo đối soát sai**). 📌 Ghi rõ: **1 migration** (38 → 39) · `schema.sql` regenerate và **kiểm bằng `Compare-Object`** (diff **chỉ** thêm) · **85 chỗ quét là TĂNG ĐÚNG** (bảng mới có PK `uuid`). 🔑 `PAY-001` vẫn **KHÔNG** `Implemented`: **chưa kiểm chứng tiền thật** + **outbox chưa có** + **`Q6`** (ai xác nhận dòng hoàn `ChoXacNhan`) còn mở |
| 18/10/2026 (sau `S6`) | Cập nhật sau `S6`: baseline **290.8s · 12 bước** (**1367** unit · **67** integration · 213 mermaid · **25/25** DB · **NND 53** · **61 quyền** · **86** chỗ quét · **24** ca cổng · **67/67** E2E · 17/17 sổ địa chỉ · 26/26 khách hàng · 26/26 thanh toán · 🆕 **49/49 đơn sàn**). `S6` ✅ XONG ⇒ **`S7`/`S8`/`S9` là các phiên kế tiếp** (độc lập nhau). ➕ **Bước ⑫ mới** (`probe-don-san.ps1` — **đường chạy thật**: gieo shop bằng SQL → `POST /api/app/channel/{id}/sync-now` → `ChannelShopSyncer` → handler → đo **DB thật**) · ➕ **cổng vận hành hạ cờ `GapSuspected`** (`SAL-005` §21: `GetIntakeAsync`/`MarkReconciledAsync` + **2 permission** + mã `0005`). 🚨 **2 lỗi thật** phát hiện **chỉ vì lần đầu đo runtime** (`sal-005` §20.5): **hash chống trùng băm cả `CorrelationId`** ⇒ **DLQ giả ở MỌI lần kéo lại** (📌 **1358 test cũ không thấy** — cần **hai** lần gửi khác `CorrelationId`; **đo hai chiều**: tiêm mã cũ ⇒ `C31`/`C32` **ĐỎ**, DLQ **2 → 3**) + **dòng intake mới không ghi watermark** ⇒ bản **cũ hơn** đến sau vẫn **ghi đè**. ➕ **4 bẫy của phép đo** (§20.6 + §21.3): `-SimpleMatch` coi `\` là ký tự thật ⇒ **6 ca ĐỎ GIẢ** · route thật `/api/app/channel/{id}/sync-now` (**404** ngay lần đầu) · dọn probe **phải** xoá `MsgIdempotencyRecords` · **thêm permission vào provider KHÔNG tự cấp cho `admin`** ⇒ `403` dù code đúng (phải chạy **`DbMigrator`**: `AbpPermissionGrants` **58 → 61**). 📌 **Sửa 3 con số đã trôi:** bất biến DB **23/23 → 24/24** (thực đo đầu `S6`) · code **341/41.414 → 347/42.437** (phương pháp **đẩy NỘI DUNG** vào `Measure-Object -Line`; đẩy **đối tượng tệp** thì ra **345** — ⚠️ cùng một bài học, dạng mới) · **quyền 58 → 61**. ⚠️ `SAL-005` **vẫn KHÔNG `Implemented`**: `Order` nội bộ **chưa cập nhật** khi áp watermark |

| 18/10/2026 (sau `S7`) | Cập nhật sau `S7`: baseline **241.8s · 12 bước** (**1395** unit · **71** integration · 213 mermaid · **25/25** DB · **NND 53** · **62 quyền** · **86** chỗ quét · **24** ca cổng · 🆕 **95/95** E2E · 17/17 sổ địa chỉ · 26/26 khách hàng · 26/26 thanh toán · 49/49 đơn sàn). `S7` ✅ XONG ⇒ **`S8`/`S9`/`S10` là các phiên còn lại**. ➕ `/HanMucKenh`: **lọc theo kho** + **phân trang** + ô chọn kho + **nhãn theo `Q4`** (`inv-003` §20) · **xác nhận lệch** (`Q5` = A: 3 cột + `CHECK` + permission `eCommerce.Reconciliation.Confirm` + endpoint) · **retention** (`Recon.Retention.Days` 180, `0` = TẮT; luật THUẦN; job `ReconRetentionWorker`; nút bấm có **XEM TRƯỚC**; **xoá CỨNG**) · **phân trang** trang chi tiết (`rec-001` §19). 🚨 **3 lỗi thật** chỉ lộ khi đo HTML/DB: **xoá MỀM** thay vì cứng ⇒ retention **không** làm bảng nhỏ đi · **`[Required]`/`[Range]` trên DTO** chặn **TRƯỚC** guard của domain ⇒ người dùng thấy thông điệp **tiếng Anh** thay vì `0005`/`0006` (📌 đúng lỗi `rec-001` §18.4 tái xuất ở dạng khác) · **gộp nhóm** trong báo cáo retention ⇒ nói **sai lý do giữ lại**. ➕ **2 lỗi của BỘ ĐO**: neo HTML giả định `7` trong khi cột `numeric` in **`7.0000`** (**3 ca ĐỎ GIẢ** + các ca **chiều ngược XANH vì lý do sai**) · gộp *"mới hơn mốc"* vào *"chờ xác nhận"*. 📌 **Đo đầu phiên `S7`: 301.5s, `exit 0`, unit 1367 / integration 67 / E2E 67 / quyền 61** = khớp bảng `S6` — tức **baseline `S6` vẫn đúng** |

| 21/10/2026 (sau `S8`) | Cập nhật sau `S8`: baseline **258.8s · 12 bước** (**1399** unit · **71** integration · 213 mermaid · **25/25** DB · **NND 53** · **62 quyền** · **86** chỗ quét · **24** ca cổng · 🆕 **123/123** E2E · 17/17 sổ địa chỉ · 26/26 khách hàng · 26/26 thanh toán · 49/49 đơn sàn). `S8` ✅ XONG ⇒ **`S9`/`S10` là các phiên còn lại**. ➕ **THANH TOÁN TRÊN UI** (`PAY-001` §16.11): `/DatHang` tạo giao dịch ⇒ `302` sang cổng ⇒ **trang kết quả** đọc trạng thái **từ DB** · **`FakePaymentProvider`** (dev-only, fail-fast) + **trang giả lập `/pay-gia-lap`** (**ký HMAC thật**, gọi **IPN thật**) · **MỞ CÔNG KHAI** `/SanPham` + `/SanPham/ChiTiet/{slugOrSku}` qua **đường đọc công khai** (repository — **không** app service `[Authorize]`) · **`sitemap.xml` dùng SLUG**. 🚨 **4 lỗi thật** (`web-002` §16.3 + `PAY-001` §16.11): **`Payments:Fake:HashSecret` trong tài liệu là KHÔNG THỂ CHẠY** (bên kiểm đọc `PaymentHub:VnPay:HashSecret` ⇒ ký khoá khác ⇒ **mọi IPN bị 400**; ✅ **đo chiều ngược**: đổi khoá ⇒ 6 ca **ĐỎ**) · **`CostPrice` KHÔNG tồn tại trong lược đồ** (ràng buộc `D10` hiện được bảo đảm bằng **sự vắng mặt**; lớp bảo vệ thật là **danh sách trắng thuộc tính DTO**) · **bộ lọc `Published` viết 3 chỗ ⇒ phép tiêm vào 1 chỗ KHÔNG làm test đỏ** (✅ gom về MỘT biểu thức + đếm = 1) · **`canonical` viết cứng làm `MenuDieuHuongTests` ĐỎ** (⚠️ **chú thích cũng bị quét**). ➕ **1 lỗi của BỘ ĐO** (bước ⑥ ĐỎ THẬT): khởi động Host trong E2E ⇒ `InventoryReconciliationWorker` (`RunOnStart`) **ghi dòng lệch THẬT** cho tồn kho probe ⇒ 📌 **thêm tiến trình vào phép đo = thêm nguồn dữ liệu**, phải hỏi *"job nào chạy khi nó lên, và nó ghi cái gì?"*. 📌 **Đo đầu phiên `S8`: 260.3s, `exit 0`, unit 1395 / integration 71 / E2E 102 / quyền 62** = khớp bảng `S7` — tức **baseline `S7` vẫn đúng** |
