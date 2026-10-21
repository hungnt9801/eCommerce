# 🚩 BÀN GIAO GIAI ĐOẠN 1 → GIAI ĐOẠN 2 (viết 21/10/2026, cuối phiên `S10`)

> **Tệp này thay cho việc đọc lại 10 phiên.** Đọc hết (~12 phút) rồi mới làm.
> ➕ File **cũ** [`BAN-GIAO-GD1-2026-10-17.md`](BAN-GIAO-GD1-2026-10-17.md) vẫn dùng được cho **bối cảnh** và
> **bảy cái bẫy PowerShell/C#** (§5.1) — nhưng 📌 **mọi con số trong đó đã trôi**; nguồn sự thật là
> [`SESSION-KICKOFF.md`](../SESSION-KICKOFF.md) **§3** + **§1 của tệp này**.
>
> 🔑 **Cách dùng:** mở phiên mới → đọc **§0** (điều không được nói quá) → **§1** (tự kiểm bằng **một lệnh**)
> → **§8** (câu hỏi đang mở). Làm việc theo **§7** (GĐ2). Đừng đọc hết 55 tài liệu.

---

## 0. 🚨 ĐỌC TRƯỚC — bốn điều **KHÔNG** được nói quá

Phiên trước từng tuyên bố *"GĐ1 hoàn thành"* khi **chưa đo lại**, và đã phải ghi lại bài học đó. `S10` là phiên
**đóng GĐ1**, nên nó **được** nói *"GĐ1 đóng"* — 🔒 nhưng chỉ với **đúng** bốn giới hạn dưới đây.

| # | Điều **KHÔNG** được nói | Sự thật đo được (21/10/2026) |
|---|---|---|
| **1** | ❌ *"Thanh toán đã kiểm chứng"* | **Chưa có lần thanh toán TIỀN THẬT nào.** `PaymentHub:VnPay:TmnCode` và `HashSecret` trong `appsettings.json` **rỗng**. Cái đã kiểm là **HMAC thật trên cổng giả lập** (`FakePaymentProvider`, dev-only) + **IPN thật**. ⇒ 📌 `PAY-001` **KHÔNG** ở trạng thái `Implemented` |
| **2** | ❌ *"Sự kiện không bao giờ mất"* | **KHÔNG có outbox/relay.** Sự kiện đi qua `IDistributedEventBus` với transport **in-process**; không có bảng outbox, không có worker relay. ⇒ 🔒 tiến trình chết giữa chừng ⇒ **mất sự kiện** |
| **3** | ❌ *"Đủ 15 metric"* | **11/15** metric có nơi phát; **4/15** **chưa** (`XC-005` §19.3). Con số cũ *"12/12"* là **nói quá** và đã sống **8 ngày** |
| **4** | ❌ *"CI đã chạy trên GitHub Actions"* | Workflow **có trong repo LOCAL** (`.github/workflows/ci.yml`, commit `S9`) — ⚠️ nhưng **CHƯA BAO GIỜ CHẠY**. 🚨 Đo được: `GET /repos/hungnt9801/eCommerce/actions/runs` ⇒ **`total_count = 0`** · `GET …/contents/.github/workflows/ci.yml` ⇒ **`404`** (tệp **không** có trên `origin/main`) · `git rev-list --count origin/main..HEAD` = **11** commit chưa đẩy. 🔒 Nguyên nhân: **môi trường không có credential `push`** (Git Credential Manager đòi đăng nhập tương tác). ⇒ 📌 **"cổng không chạy tự động thì không phải cổng"** — câu này **vẫn còn đúng** cho tới khi **có một lần chạy xanh** |

🔑 **Bài học xuyên suốt 10 phiên** (đã đúng trong **phần lớn** lỗi của dự án):
> **Một phép đo bất ngờ thường là phép đo SAI, không phải sản phẩm sai.**
> Và: **"cổng không chạy tự động thì không phải cổng."**

---

## 1. TỰ KIỂM TRẠNG THÁI — MỘT LỆNH (và đọc đúng dòng)

```powershell
cd D:\Projects\Tools\eCommerce
pwsh -File tools\kiem-tat-ca.ps1 -DayDu
```

**Số đo chuẩn (`S10`, 21/10/2026, 12 bước, `TONG = 269.7s`, `exit 0`):**

| Bước | Nội dung | Số đo |
|---|---|---|
| ② | Build + **KIỂM MỐC NHỊ PHÂN** (`kiem-moc-build.ps1`) | ✅ **71 tệp DLL/EXE đổi mốc** — 🔒 `exit 0` **không** chứng minh build đã vào |
| ③ | Unit test | **1419 / 1419** |
| ④ | Integration test (PostgreSQL 17 **thật**) | **71 / 71** |
| ⑤ | Khối mermaid (parser thật) | **213 · 0 lỗi** |
| ⑥ | Bất biến DB (`kiem-db.ps1`, tự quét **86** vị trí GUID probe) | **25 / 25** · `NND = 53` · **62 quyền** |
| ⑦ | Test cho **chính các cổng** (`test-cong-kiem.ps1`) | **37** ca (A…I · R…V · L…P · W · X · Y) |
| ⑧ | **E2E có phiên đăng nhập** (Web :5100 + **Host :5000**) | **133 / 133** — 📌 đọc dòng `E2E: N/N`, ⚠️ **đừng đếm** điểm gọi `Kq` |
| ⑨–⑫ | Probe HTTP thật: sổ địa chỉ · khách hàng · thanh toán · đơn sàn | **17/17** · **26/26** · **26/26** · **49/49** |

📌 **So với cuối `S9`:** 👉 **cùng** số ca ở mọi bước **trừ** hai thay đổi **có chủ ý** (unit **1409 → 1419**,
E2E **123 → 133** — `S10` thêm 10 ca mỗi loại). ⏱ **Thời gian là thứ duy nhất KHÔNG phải chỉ tiêu**
(máy/nhiệt độ; cùng mã số đo đã từng lệch **14 %**).

**Git:** `main` · `origin` = `https://github.com/hungnt9801/eCommerce` (⚠️ **PUBLIC** ⇒ bí mật thật **chỉ** vào
GitHub Secrets) · 🔒 **nguồn sự thật là LỆNH, không phải dòng chữ này**:
```powershell
git rev-list --count HEAD ; git rev-list --count origin/main..HEAD   # mong đợi: 0
```

---

## 2. ✅ GĐ1 ĐÃ XONG — 10 phiên, mỗi phiên có cổng ra

| Phiên | Đóng cái gì | Cổng ra (số thật) |
|---|---|---|
| `S1`/`S2` | `ADDR-004` sổ địa chỉ: entity + bảng liên kết + API + 4 quyền + PII | NND **48 → 51** · probe ⑨ **17/17** |
| `S3` | `CUS-001`: 🚨 **vá lỗi thật** *"khách `Blocked` vẫn đặt được"* + nhóm khách + phân trang | unit **+9** · probe ⑩ **26/26** |
| `S4`/`S5` | `PAY-001`: đường **tạo** giao dịch + **IPN/webhook** HMAC-SHA512 trên **query thô** + hoàn tiền + 3 `CHECK` tiền | NND **51 → 52** · probe ⑪ **26/26** |
| `S6` | `SAL-005`: nhật ký sự kiện + watermark + DLQ + **đo runtime lần đầu** | NND **52 → 53** · bước ⑫ **49/49** |
| `S7` | UI/API Inventory + Reconciliation: lọc kho · xác nhận lệch (**đóng cảnh báo + audit**) · retention (xoá **cứng**) | quyền **61 → 62** · E2E **+23** |
| `S8` | Thanh toán **trên UI** + **mở công khai** storefront + sitemap dùng slug | quyền **62 = 62** · E2E **+21** (gồm phiên **ẩn danh**) |
| `S9` | `XC-007`: **CI thật** (`.github/workflows/ci.yml`) + khả chuyển **Linux** (`tools/nen-tang.ps1`) + **cổng của cổng** | cổng **24 → 37** ca · unit **+10** |
| `S10` | **Đóng nợ nhỏ + bàn giao**: CSV `;` · metric (đo lại + cơ chế) · `RunOnStart` (cơ chế) · xác nhận outbox/relay | unit **+10** · E2E **+10** · 📄 tệp này |

📌 **Nền đo (đừng làm lại):** **12 bước** · **37** ca test cổng · **4** probe HTTP thật · **25** bất biến DB ·
**53** unique index `NULLS NOT DISTINCT` · **41** migration · **62** quyền · **354 tệp / 44.476 dòng** code sản phẩm ·
**117 tệp / 19.148 dòng** test.

---

## 3. 🚨 CÒN DANG DỞ — danh sách **ĐO ĐƯỢC** (đừng coi là xong)

### 3.1 Chặn bởi **bên ngoài** (không phải việc code)

| # | Việc | Bằng chứng "chưa có" | Cần gì |
|---|---|---|---|
| 1 | **Thanh toán tiền THẬT** (`PAY-001`) | `"TmnCode": ""`, `"HashSecret": ""` trong `src/eCommerce.HttpApi.Host/appsettings.json` | **Credential sandbox VNPay** (tự đăng ký [devreg](https://sandbox.vnpayment.vn/devreg/), 5–10 phút) + **tunnel** HTTPS cho IPN |
| 2 | **Điều kiện mở GĐ2** | `docs/features/README.md`: **1/55** tài liệu `Approved` (chỉ `ADDR-004`) | 🔒 Cổng vào GĐ2 (`implementation-plan.md` §8) đòi **9** tài liệu GĐ2 `Approved` — hiện **0/9** |
| 3 | 🚨 **Push + một lần chạy CI xanh + branch protection** | `actions/runs` ⇒ **0** lần chạy; `ci.yml` **404** trên `origin/main`; **11** commit chưa đẩy | **Một máy có credential `git push`** (hoặc PAT trong `GITHUB_TOKEN`) → `git push origin main` → xem **một** lần chạy xanh → **rồi mới** bật branch protection + required checks (`XC-007` §9.2/§9.3) |

### 3.2 Nợ **hạ tầng** — đã xác nhận, **chưa** làm (cố ý)

| # | Nợ | Ghi ở | Vì sao **không** làm trong `S10` |
|---|---|---|---|
| 1 | 🚨 **KHÔNG có outbox/relay** (0 bảng `*Outbox*`/`AbpEvent*`, 0 worker relay) | `XC-002` §16.5 #1 | Là **feature riêng** (bảng + relay + chống trùng + đo) — không phải "nợ nhỏ" |
| 2 | **4/15 metric chưa có nơi phát** | `XC-005` §19.3 | Cả 4 cần **hạ tầng mới** (outbox · luồng phát lại DLQ · wrapper khoá đọc TTL · logging provider) |
| 3 | **Chưa có tầng XUẤT metric** (OTel → Prometheus) | `sal-006` §23.6 | `Meter` chỉ **phát**; không collector thì **không ai đọc** |
| 4 | **DLQ**: chưa có job phát lại · UI · job dọn · cảnh báo tự động | `XC-002` §16.5 #2…#5 | Phát lại tự động 🔒 **cố ý** không làm: nó **có thể gây hỏng lần nữa** ⇒ phải **có người** + audit |
| 5 | **`SAL-005`: `Order` nội bộ chưa cập nhật khi áp watermark** | `sal-005` §20.7 #1 | Ngoài phạm vi `S10`; chưa có test nào phủ |
| 6 | **`Mau01CsvExporter` (tờ khai thuế) vẫn dùng dấu `,`** | `cus-001` §29.4 | 📌 Tệp **kê khai** có thể được **nạp vào hệ thống khác** ⇒ đổi dấu phân cách cần **người quyết** |

### 3.3 Nợ **UI/tính năng nhỏ** (không chặn bán hàng)

| # | Nợ | Ghi ở |
|---|---|---|
| 1 | 301 khi đổi slug · read-model/cache storefront · JSON-LD/OG · hiển thị mức tồn | `web-002` |
| 2 | UI **hoàn tiền** · trang `/VanHanh/Dlq` | `PAY-001` · `XC-002` |
| 3 | **Chọn địa chỉ giao khi `/DatHang`** (`ADDR-004` B11 — hoãn đúng hạn ở `S2`) | `cus-001` §28.6 #5 |
| 4 | UI quản trị **nhóm khách** / **gán khách vào nhóm** / **xoá nhóm** (cần người quyết) | `cus-001` §28.6 #1–#3 |
| 5 | **Chưa kiểm vai trò CHỈ-ĐỌC** — mọi probe dùng `admin` | `web-001` §99.5 #3 |
| 6 | **Cổng quét "dấu cách trong tên hàm test C#"** (bài học **đã ghi 2 lần**, vẫn chưa có cơ chế) | `cus-001` §27.6 #1 |
| 7 | Test **xoá dữ liệu** (Luật 91/2025/QH15) + cảnh báo xuất PII lặp lại | `cus-001` §20.7 |
| 8 | Test `toiDa` ở **tầng unit** (hiện chỉ đo qua E2E) | `cus-001` §27.6 #6 |
| 9 | ⚠️ **Tên job CI là `cong-8-buoc` trong khi cổng nay có 12 bước** | 🚨 **BÂY GIỜ ĐỔI ĐƯỢC RẺ** (chưa có lần chạy nào, chưa bật required check) — sau khi bật thì **không đổi được** mà không sửa thiết lập GitHub. 📌 Sửa **cùng lúc** 3 tệp: `.github/workflows/ci.yml` (dòng 53/55) · `CiWorkflowTests` (3 chỗ khoá tên) · `XC-007` (mục §9.2 + §16). 🔑 Để nguyên thì tên job **nói sai** về cổng — đúng loại lỗi mà `S10` vừa đóng hai lần |

---

## 4. 🔒 RÀNG BUỘC BẤT BIẾN (vi phạm là **hỏng ngầm**, không phải đỏ cổng)

**Schema/DB**
1. 🚨 **MỌI unique index trên entity `IMultiTenant` PHẢI có `NULLS NOT DISTINCT`** — viết **SQL thô** trong
   migration (kèm `DROP INDEX IF EXISTS`). Hiện **53**; `PostgreSqlSchemaTests` **khoá con số** ⇒ thêm/bớt phải
   sửa **cùng PR** (cả **danh sách tên index**).
2. **Tiền = `decimal`** — cấm `double`; **cấm so tiền bằng `==`**.
3. Thêm/bớt bảng ⇒ **`schema.sql` regenerate** + đếm lại **chỗ quét GUID** (`kiem-db.ps1` in ra).

**ABP — bốn cái bẫy đã trả giá**
4. 🚨 `DbSet<>` **phải có** trên `eCommerceDbContext` cho **mọi** entity (ABP đăng ký `IRepository<T>` bằng cách
   **quét `DbSet`**, không quét model). Thiếu ⇒ **DI nổ lúc chạy ⇒ HTTP 500** — triệu chứng **không trỏ tới chỗ sai**.
5. 🔐 `[Authorize]` ở **cấp class** + permission **PHẢI được định nghĩa** (chưa định nghĩa ⇒ `403` cho **mọi** người).
6. 🔒 **Worker nền KHÔNG được gọi app service có `[Authorize]`** (interceptor từ chối principal ẩn danh) — đọc
   repository trực tiếp, chia sẻ **luật thuần** ở Domain.
7. `AbpExceptionFilter` biến `BusinessException` thành **HTTP 500** trên MVC ⇒ controller **chỉ** `catch BusinessException`.
   📌 Và **guard ở DTO (`[Required]`/`[Range]`) chặn TRƯỚC guard của domain** ⇒ người dùng thấy **tiếng Anh**
   thay vì mã lỗi (`S7` đã trả giá **hai lần**).

**Phân tầng**
8. `Domain`/`Domain.Shared` **KHÔNG** tham chiếu `Application.Contracts`; `Application.Contracts` **KHÔNG** tham
   chiếu `Domain`. Hằng số độ dài ở `eCommerceConsts`.

**Quy trình**
9. 🔒 **Code lệch tài liệu ⇒ SỬA TÀI LIỆU TRƯỚC, cùng PR** (cập nhật **§16/§19+** "đã xong / còn lại").
10. 🔒 **Commit message PHẢI thuần ASCII** — hook `commit-msg` **chặn** (đã cắn **2 lần**).
11. 🚫 **KHÔNG bypass kiểm soát bảo mật.** Cụm từ phải nhớ: **cấm** bỏ/vô hiệu `KiemChuKy`/`VerifyWebhookAsync`
    của VNPay — ai gửi `vnp_ResponseCode=00` cũng thành **đơn đã trả tiền** ⇒ **giao hàng miễn phí** (`PAY-001` §7.7.4).
12. 🚨 **`dotnet ef migrations add … --no-build`** dùng bản build của **STARTUP project** ⇒ nếu `eCommerce.DbMigrator`
    chưa build lại thì sinh **MIGRATION RỖNG** mà vẫn in `Done.` ⇒ **luôn mở tệp ra đọc `Up()`**.
13. 🚨 Tệp trong `Data/`, `Assets/`, `appsettings*.json` được **copy vào output** ⇒ sửa nguồn rồi chạy
    `dotnet test --no-build` là đọc **bản CŨ** ⇒ **build lại project test**.
14. 🆕 **`-SimpleMatch` coi `\` là ký tự thật** (`S6`) · **`Contains` khớp chuỗi con** (`LockTimeout` ⊂
    `LockTimeoutSeconds`, `S10`) ⇒ 🔒 phép đo "có/không" phải **neo bằng regex**, không dùng `Contains`.

---

## 5. 🛠️ CÔNG CỤ KIỂM — DÙNG, ĐỪNG VIẾT LẠI

```
tools/kiem-tat-ca.ps1        ← MỘT LỆNH chạy 12 bước (= nền CI, chỉ cần gọi; -DayDu mới đủ)
tools/kiem-moc-build.ps1     ← cổng chống "build exit 0 mà DLL KHÔNG ĐỔI"
tools/kiem-db.ps1            ← 25 bất biến DB + tự quét 86 vị trí GUID probe
tools/kiem-e2e.ps1           ← E2E có phiên đăng nhập, 133 ca, HTML/DB THẬT (bước ⑧)
tools/test-cong-kiem.ps1     ← 37 ca test cho chính các cổng (chống cổng luôn-đỏ / luôn-xanh)
tools/nen-tang.ps1           ← MỘT chỗ cho khác biệt Windows/Linux (S9)
tools/ci-khang-dinh.ps1      ← cổng CỦA cổng trên log CI (A1…A7) + Evidence.json
tools/ham-tiem.ps1           ← thư viện tiêm TỰ KHẲNG ĐỊNH (dot-source)
tools/route-check.ps1        ← in route THẬT từ Host
tools/wait-host.ps1          ← chờ host lên (vòng lặp, không Sleep)
tools/README-probe.md        ← 🔑 ĐỌC TRƯỚC KHI VIẾT PROBE MỚI
```

📌 **Hai cổng tài liệu, chạy trước mỗi commit:**
```powershell
npm --prefix tools run check:mermaid          # 213 khối · 0 lỗi
pwsh -File tools\kiem-link-tai-lieu.ps1       # 0 tham chiếu chết
```

---

## 6. HAI TIẾN TRÌNH + THÔNG TIN ĐĂNG NHẬP

| Tiến trình | Cổng | Vai trò |
|---|---|---|
| `eCommerce.HttpApi.Host` | **5000** | API + **auth server DUY NHẤT** (OpenIddict) — **chạy worker nền** |
| `eCommerce.Web` | **5100** | UI quản trị (cookie auth) — **worker bị TẮT tường minh** (`AbpBackgroundWorkerOptions.IsEnabled = false`) |

**Đăng nhập:** `admin@abp.io` / `1q2w3E*` — ⚠️ **mật khẩu** `1q2w3E*` (**E HOA**), **client secret** `1q2w3e*`
(**e thường**). Sai một ký tự ⇒ `invalid_client`/`401` ⇒ 🔒 **đã cắn hai lần**.

⚠️ `wwwroot/libs` **không commit** — máy mới chạy `abp install-libs -wd src/eCommerce.Web` (cần yarn), nếu không
trang vẫn `200` nhưng **thiếu toàn bộ CSS/JS**.

🔑 **Hạ tầng dev:** PostgreSQL 17 + Redis 7 qua `docker compose up -d` (`ecommerce-postgres`, `ecommerce-redis`).
📌 Docker Desktop **tắt sau khi máy khởi động lại** — triệu chứng là `psql THAT BAI … failed to connect to the
docker API`; 🔒 **đọc thông điệp là đủ**, đừng đi sửa mã nguồn.

---

## 7. GIAI ĐOẠN 2 — làm gì, theo thứ tự nào

📌 **Nguồn sự thật phạm vi:** [`implementation-plan.md`](../implementation-plan.md) **§8 + Phụ lục A.3**:
**GĐ2 = `PRO-001…002` (khuyến mãi) · `SHP-001…004` (vận chuyển) · `REC-002…003` · `SAL-006` (RMA) — 9 tài liệu.**

| # | Việc | Vì sao thứ tự này |
|---|---|---|
| **0** | 🚨 **Chốt điều kiện mở GĐ2** — hiện **0/9** tài liệu GĐ2 `Approved` | 🔒 Luật của dự án: tài liệu **Approved** trước, code sau (`implementation-plan.md` §8) |
| **1** | 🔑 **`PRO-001` (voucher/coupon)** | Nhỏ nhất trong 9, và **dòng tiền**: giảm giá chạm **mọi** đơn ⇒ làm **trước** khi `PRO-002` |
| **2** | **`PRO-002` (engine điều kiện cây AND/OR)** | 📌 Phức tạp nhất; 🔒 **bắt buộc** guard **bùng nổ tổ hợp** |
| **3** | **`SHP-001` (adapter GHN tính phí)** → **`SHP-002`** (webhook) → **`SHP-003`** (COD) → **`SHP-004`** (GHTK) | Mỗi phiên **một adapter** |
| **4** | **`SAL-006` (RMA)** | 🟡 **ĐANG LÀM SỚM** dù thuộc GĐ2 (nó là cổng *"không mất tiền"*); 📌 phần còn lại là **phiếu RMA** `SAL-006` §… |
| **5** | **`REC-002/003`** (settlement sàn / đối soát COD import Excel) | Sau khi có COD thật (`SHP-003`) |

🚨 **HAI CÂU HỎI NCC CÒN TREO — CHẶN THIẾT KẾ** (`gd1-coding-sessions.md` §6):
① **GHTK xác minh địa chỉ 2 cấp** (`SHP-004`, fail-closed) · ② **GHN đổi COD cần OTP** (`SHP-003`, phải có bước
thủ công). 🔒 **Đừng tự chọn đáp án rồi code** — ghi vào §15.1 của tài liệu tương ứng và **dừng**.

➕ **5 input bên ngoài vẫn treo** (chặn `EINV`/`SIGN`/`ADDR`): xem `handover-gd1.md` §3 — ⚠️ **bịa** bảng trường
XML hoặc endpoint CQT sẽ tạo sản phẩm **trông như xong nhưng SAI VỀ PHÁP LÝ**.

📌 **Không có gì trong GĐ2 được mở khoá bằng một quyết định kỹ thuật:** cả 9 tài liệu đang `Draft`.

---

## 8. ❓ CÂU HỎI ĐANG MỞ — **chủ dự án trả lời**, không tự chọn

| # | Câu hỏi | Ảnh hưởng |
|---|---|---|
| **Q-S10-1** | **Có ưu tiên làm `outbox/relay` (hạ tầng) TRƯỚC GĐ2 feature không?** | 🔒 Nếu không: **mọi** sự kiện vẫn in-process ⇒ mất sự kiện khi tiến trình chết. 📌 Đây là nợ **duy nhất** có thể gây **mất dữ liệu** (đơn/tiền) |
| **Q-S10-2** | **Credential VNPay sandbox** (hoặc chỉ đạo *"chấp nhận GĐ1 không kiểm tiền thật"*) | `PAY-001` không thể `Implemented` nếu thiếu; 🔑 đây là **DoD** đang mở |
| **Q-S10-3** | **GĐ2 mở khi 0/9 tài liệu `Approved`?** (duyệt nhanh 9 tài liệu, hay làm tiếp nợ GĐ1?) | 🔒 Cổng vào GĐ2 của `implementation-plan.md` §8 ⇒ nếu bỏ qua, phải ghi **lý do** vào tài liệu, không làm ngầm |
| **Q-S10-4** | **`Mau01CsvExporter`: đổi dấu phân cách sang `;`?** | Tờ khai thuế có thể được **nạp vào hệ thống khác** — 📌 đổi sai là **sai về pháp lý** |
| **Q-S10-5** | 🚨 **Ai/cấp nào được `git push`?** (PAT, SSH key, hay GitHub App) | 🔒 Hiện **không có** credential ⇒ CI **0 lần chạy** và **11** commit (gồm **cả** `.github/workflows/ci.yml`) **chưa lên `origin`**. 📌 Đây là **nợ duy nhất** của GĐ1 mà **máy này không thể đóng** |

---

## 9. BẢN ĐỒ TÀI LIỆU — đọc gì khi cần gì

| Cần biết | Đọc |
|---|---|
| **Số đo chuẩn + luật phiên + quyết định đã chốt (D1…D11)** | [`SESSION-KICKOFF.md`](../SESSION-KICKOFF.md) **§3 · §4 · §7** |
| **Chia phiên + cổng ra từng phiên** | [`gd1-coding-sessions.md`](../gd1-coding-sessions.md) **§4** |
| **Tài liệu feature (55 tệp) + trạng thái thật** | [`features/README.md`](../features/README.md) |
| **Bảy cái bẫy PowerShell/C#** (vẫn đúng) | [`BAN-GIAO-GD1-2026-10-17.md`](BAN-GIAO-GD1-2026-10-17.md) **§5.1** |
| **Quy ước code + quy tắc vàng "sửa tài liệu trước"** | [`coding-conventions.md`](../coding-conventions.md) · [`design-doc-guidelines.md`](../design-doc-guidelines.md) |
| **Kế hoạch tổng + phạm vi GĐ2** | [`implementation-plan.md`](../implementation-plan.md) §8 · Phụ lục A.3 |
| **Quy tắc viết probe** | [`README-probe.md`](../../tools/README-probe.md) |
| **Migration / regenerate `schema.sql`** | `BAN-GIAO-GD1-2026-10-17.md` **§9.1** |

---

## 10. MỘT CÂU ĐỂ NHỚ

> 🔑 **"Cổng không chạy tự động thì không phải cổng."**
> 📌 Và vế thứ hai, đã đúng **mười phiên liền**: **"một danh sách khai báo mà không ai ĐO thì là một lời tuyên bố,
> không phải một bất biến."** — `S10` phải đóng **hai** nợ đúng vì thế: *"12/12 metric"* và *"supervisor đã sửa"*.
