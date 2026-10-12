# Structure & Tech Stack — source code mới

> Tài liệu này **chỉ** trình bày cấu trúc và công nghệ. Chưa đi vào thiết kế chi tiết từng module.
> Bối cảnh: solution hiện tại đã có 15 project (khung + `ChannelHub` + `PaymentHub` + Phase 0 auth).

---

## 1. Tám quyết định đã chốt (ràng buộc cho mọi bước sau)

| # | Quyết định | Hệ quả lên kiến trúc |
|---|---|---|
| 1 | **Chỉ HỌC thiết kế nghiệp vụ từ các repo; tự code lại** đồng nhất theo kiến trúc & pattern của source mới | ❌ **Không copy file `.cs` nào** từ OCC/eShop/EasyAbp ⇒ `THIRD-PARTY-NOTICES.md` chỉ còn ghi **NuGet package**. ✅ Mọi module theo **một khuôn duy nhất** do ta định nghĩa |
| 2 | Ký số: **thủ công** (chấp nhận bước con người) | `DigitalSigningHub` thiết kế **state machine 2 bước** có trạng thái "chờ người ký"; **không** đầu tư HSM giai đoạn này |
| 3 | Địa chỉ: **hỗ trợ CẢ 2 cấp mới và 3 cấp cũ** | `AddressHub` là module **P0**; `Address` không được nằm trong `Commerce.Core` |
| 4 | **Website trước** (sàn sau) | `eCommerce.Web` (admin + storefront) lên trước; `ChannelHub` đã có sẵn, đấu sàn sau khi luồng đơn ổn định |
| 5 | `Money`: **Port** *(xem ghi chú cuối trang)* | Có value object `Money` dùng chung toàn hệ thống |
| 6 | Đa tiền tệ: **không** cho hiện tại, **có** cho tương lai | `Money { Amount, Currency }` có `Currency` ngay từ đầu, nhưng chỉ VND hoạt động; không dựng bảng tỷ giá |
| 7 | **Không** port module nguyên khối của EShop | Chỉ học **pattern** (extension point, guard trạng thái, inbox) rồi tự viết |
| 8 | UI admin: **tự viết tay trước** | Không mua ABP Pro; dùng module **miễn phí** của ABP + tự viết màn hình |

> 📌 **Ghi chú quyết định 1 vs 5:** hai câu trả lời hơi mâu thuẫn. Tôi chọn cách thoả mãn cả hai —
> **tự viết `Money`/`Currency` theo đúng thiết kế của OCC `MoneyDataType`** (điểm cố định cho VND,
> chặn cộng/trừ khác loại tiền), **không copy file**. Nếu bạn muốn copy file thật, chỉ cần nói — tôi bổ sung
> nghĩa vụ MIT vào `THIRD-PARTY-NOTICES.md`.

---

## 2. Cấu trúc solution

### 2.1 Nguyên tắc bố cục

1. **`shared/` = nền dùng chung** mà **mọi module được phép tham chiếu** (Money, Currency, Address, mã lỗi, lock key).
   ⚠️ Đây là bài học từ Phase 0: `eCommerceConsts` nằm trong Domain.Shared **của app** ⇒ module `ChannelHub` **không tham chiếu được**
   (module → app là phụ thuộc ngược) và phải tự tạo `ChannelHubConsts`. Nền dùng chung **phải là project riêng**.
2. **`src/` = ứng dụng**: host, DbContext hợp nhất, migration, UI. Không chứa nghiệp vụ.
3. **`modules/` = mỗi bounded context một module**, sở hữu entity của mình, giao tiếp qua event.
4. **`test/`** tách unit test và integration test.

### 2.2 Cây thư mục mục tiêu

```
D:\Projects\Tools\eCommerce\
├─ eCommerce.slnx
├─ global.json                        ← ghim SDK 10.0.400
├─ Directory.Build.props              ← net10.0, nullable, implicit usings, LangVersion
├─ Directory.Packages.props           ← quản lý version tập trung (CPM)
├─ .editorconfig                      ← quy ước format (thêm)
├─ docker-compose.yml                 ← PostgreSQL 17 + Redis 7 (+ RabbitMQ khi tách service)
├─ THIRD-PARTY-NOTICES.md
│
├─ shared/                            ⬅ NỀN DÙNG CHUNG — mọi module tham chiếu được
│  └─ eCommerce.Kernel/               Money · Currency · Address(value object) · enum chung · mã lỗi · lock key
│
├─ src/                               ỨNG DỤNG
│  ├─ eCommerce.Domain.Shared/         hằng số/enum cấp app (không dùng cho module)
│  ├─ eCommerce.Domain/                domain cấp app — sẽ teo dần khi entity chuyển vào module
│  ├─ eCommerce.Application.Contracts/
│  ├─ eCommerce.Application/
│  ├─ eCommerce.EntityFrameworkCore/   ⭐ DbContext HỢP NHẤT + Migrations/
│  ├─ eCommerce.HttpApi.Host/           host API (auth server + resource server, Swagger)
│  ├─ eCommerce.DbMigrator/             migration + seed (admin, OpenIddict client, kênh, dữ liệu nền)
│  └─ eCommerce.Web/                    ⬅ MỚI: UI quản trị (ABP MVC) + storefront (Razor Pages/MVC)
│
├─ modules/                           MỖI BOUNDED CONTEXT = 1 MODULE
│  ├─ Catalog/          A1   Product · ProductVariant(SKU) · Category(cây) · Brand · Attribute/Option · Media · SEO
│  ├─ Pricing/          A2   PriceList · giá theo kênh · tier price · cost price · chính sách làm tròn
│  ├─ Inventory/        A4   Warehouse · StockItem · StockLedger(append-only) · StockReservation(TTL) · Allotment theo kênh
│  ├─ Sales/            A5+A6 Cart · Checkout · Order · OrderLine · state machine (4 trục) · OrderStatusHistory · RMA
│  ├─ Customers/        A7   Customer(gắn IdentityUser) · sổ địa chỉ · CustomerGroup
│  ├─ Promotions/       A3   Voucher/Coupon · cây điều kiện AND/OR · FlashSale                    (P2)
│  ├─ ChannelHub/       ✅ sẵn có  Channel · ChannelShop(token mã hoá) · ChannelListing · connector sàn
│  ├─ PaymentHub/       ✅ sẵn có  PaymentTransaction(idempotent) · IPaymentProvider · VNPay/MoMo/payOS/SePay
│  ├─ AddressHub/       B0   địa chỉ 2 cấp + 3 cấp · bảng mã địa chỉ theo từng hãng          🔴 P0
│  ├─ EInvoiceHub/      B3   phát hành · gửi CQT · điều chỉnh/thay thế/huỷ · tra cứu           🔴 P0
│  ├─ DigitalSigningHub/B3   state machine ký 2 bước (VNPT/BKAV…) — có trạng thái chờ người ký 🔴 P0
│  ├─ ShippingHub/      B2   carrier adapter (GHN/GHTK…) · tính phí · vận đơn · COD            (P2)
│  ├─ Reconciliation/   B1+B4+B5  đối soát thanh toán · settlement sàn · đối soát COD           (P2)
│  ├─ AccountingHub/    B6   xuất liệu kế toán (Excel/CSV trước, API sau)                      (P3)
│  ├─ Reporting/        C1   read-model báo cáo (không query nặng trên DB giao dịch)            (P3)
│  └─ Notifications/    C2   email · SMS · Zalo OA                                              (P3)
│
└─ test/
   ├─ eCommerce.Tests/                 ✅ sẵn có — unit test domain/thuật toán
   └─ eCommerce.IntegrationTests/      ⬅ MỚI — PostgreSQL thật qua Testcontainers
```

### 2.3 Khuôn chuẩn của MỘT module (bắt buộc giống nhau)

```
modules/<Module>/
├─ eCommerce.<Module>.Domain/                 entity · value object · domain service · mã lỗi
│                                             + <Module>DbContextModelCreatingExtensions.cs  (Configure<Module>())
├─ eCommerce.<Module>.Application/            DTO · app service (auto API controller)
└─ eCommerce.<Module>.Contracts/              ⬅ CHỈ tạo khi module khác cần dùng trực tiếp (DTO/interface/ETO)
```

- **3 project/module** (thêm `.Contracts` khi cần) — không dùng khuôn 8 project của ABP để tránh bùng nổ project.
- `ChannelHub` hiện có 4 project (đã có `.Application.Contracts`) ⇒ **đúng khuôn**, giữ nguyên.
- `PaymentHub` hiện có 3 project ⇒ đúng khuôn.
- ⚠️ **Chuẩn hoá tên**: các module mới dùng tiền tố `eCommerce.<Module>.<Layer>`; `ChannelHub`/`PaymentHub` hiện đang là
  `ChannelHub.Domain`… ⇒ nên **đổi tên một lần** cho đồng nhất (hoặc chấp nhận ngoại lệ, nhưng phải ghi rõ).

---

## 3. Tech stack

| Lớp | Chọn | Phiên bản | Vì sao |
|---|---|---|---|
| **Runtime** | .NET | **10.0.400** (ghim `global.json`) | ABP 10.x yêu cầu .NET 10; SDK đã có trên máy |
| **Framework** | **ABP Framework** | **10.7.0** | Cho sẵn: multi-tenant, permission, setting/feature, **dynamic background worker**, **distributed lock**, **event bus + outbox/inbox có relay**, BLOB, audit. Dùng **package NuGet** (LGPL-3.0-only) — **không copy source** |
| **Ngôn ngữ** | C# | `latest`, `Nullable=enable`, `ImplicitUsings` | Đã cấu hình trong `Directory.Build.props` |
| **CSDL** | **PostgreSQL** | 17 (Docker) | SQL + transaction đầy đủ; đã chạy migration thành công |
| **ORM** | EF Core | **10.0.11** (Npgsql 10.0.3) | **MỘT DbContext hợp nhất** + mỗi module tự cấp `Configure<Module>()` |
| **Cache + Lock** | **Redis** | 7 | Lock phân tán là **bắt buộc** (refresh token sàn, trừ kho). Đã đặt `AbortOnConnectFail=false` để Redis chết không làm sập app |
| **Eventing** | ABP distributed event bus | 10.7.0 | Khởi đầu in-process → **đổi sang RabbitMQ chỉ bằng đăng ký DI**. Kèm **Outbox/Inbox có relay worker** (bài học: outbox của eShop thiếu relay ⇒ dual-write vẫn còn) |
| **Job nền** | BackgroundWorkers (supervisor + dynamic) + **BackgroundJobs** | 10.7.0 | Worker theo shop đã chạy; job bền vững cho việc dài (đồng bộ lịch sử, đối soát) |
| **Xác thực** | ABP **Identity + OpenIddict** | 10.7.0 | Đã xong Phase 0. UI login sẽ có khi thêm `eCommerce.Web` |
| **Mapping** | **Volo.Abp.Mapperly** (source generator) | 10.7.0 | ⚠️ **KHÔNG dùng AutoMapper** (bản 14.0.0 có advisory GHSA-rvv3-g6hj-g44x). Mapperly là dependency chính thức của ABP 10.7 |
| **Validation** | FluentValidation (ABP integration) | 10.7.0 | Thêm khi có DTO cần validate |
| **API docs** | Swashbuckle | **10.2.3** | Đúng yêu cầu của `Volo.Abp.Swashbuckle 10.7.0` |
| **Logging** | Microsoft logging → **Serilog** sau | — | Đi từ mặc định, thêm Serilog khi cần structured log tập trung |
| **Test** | xUnit (đã có) + **Testcontainers.PostgreSql** | — | Unit test hiện có 20/20; integration test chạy PostgreSQL thật (SQLite không phản ánh đúng hành vi index/transaction) |
| **UI** | ABP **MVC/Razor Pages** + theme miễn phí (`Volo.BasicTheme` / LeptonX Lite) | 10.7.0 | **Server-rendered ⇒ tốt cho SEO** (storefront cần SEO); Blazor WASM không phù hợp SEO |
| **Đóng gói** | Docker + docker-compose | — | Đã có PostgreSQL + Redis |
| **CI** | GitHub Actions | — | Thêm sau: `dotnet build` + `dotnet test` |

---

## 4. Quy ước code (để "tự code lại đồng nhất")

| # | Quy ước | Lý do / nguồn |
|---|---|---|
| 1 | **Tiền luôn `decimal`** qua value object `Money`, **không bao giờ `double`** | GrandNode 2 dùng `double` ở 96 trường |
| 2 | **Không so tiền bằng `==`** — so theo đơn vị nhỏ nhất (`long`) hoặc có tolerance | EShop so `ActualTotalPrice == OriginalPaymentAmount` |
| 3 | **Mọi module sở hữu entity của mình**; module → app là **phụ thuộc ngược, cấm** | Đã kiểm chứng ở Phase 0 |
| 4 | **Nền dùng chung ở `shared/eCommerce.Kernel`**, không để trong app Domain.Shared | Bài học `ChannelHubConsts` |
| 5 | **Entity của module phải có `DbSet` trên DbContext hợp nhất** | Thiếu ⇒ `IRepository<T>` không được đăng ký (lỗi đã gặp) |
| 6 | **`Configure<AbpDbContextOptions>(o => o.UseNpgsql())`** | Thiếu ⇒ mọi API chạm DB trả 500 (lỗi đã gặp) |
| 7 | **`[Authorize]` ở cấp CLASS cho mọi app service** | ABP không tự bảo vệ app service (lỗi đã gặp) |
| 8 | **Tồn kho = append-only ledger**, không UPDATE số lượng | Truy vết + đối soát |
| 9 | **Idempotency ở mọi cửa vào** (webhook sàn, IPN cổng, job retry): bảng inbox + unique index | Webhook là at-least-once |
| 10 | **Không tin `ReturnUrl`/callback** — chỉ IPN đã xác thực chữ ký | Đã áp dụng ở `PaymentHub` |
| 11 | **State machine phải NÉM lỗi**, không im lặng | eShop guard im lặng ⇒ event rơi không dấu vết |
| 12 | **Distributed lock cho tài nguyên dùng chung** (kho theo SKU, token sàn theo shop) | Chống race condition |
| 13 | **Mọi thao tác ghi qua Unit of Work** | Transaction tự động |
| 14 | **Hợp nhất `[DependsOn]` thành một attribute** (ABP CLI tách nhiều attribute + thụt lề sai) | Đã gặp ở Phase 0 |
| 15 | **Mọi package ABP cùng version**, sửa **một file** `Directory.Packages.props` | Central Package Management |

---

## 5. Thứ tự thi công (theo quyết định: website trước)

```
GĐ 0 (pháp lý — làm cùng checkout)
   AddressHub (2 cấp + 3 cấp)  ·  EInvoiceHub  ·  DigitalSigningHub (state machine, ký thủ công)

GĐ 1 (bán được trên website)  ← ưu tiên theo quyết định 4
   Catalog → Pricing → Inventory → Sales(Cart/Checkout/Order) → Customers → B1 đối soát thanh toán

GĐ 2 (vận hành & tuân thủ VN)
   Promotions → ShippingHub(+COD) → Reconciliation(COD/settlement) → RMA

GĐ 3 (điều hành)   Reporting → Notifications → AccountingHub
GĐ 4 (tăng trưởng) Loyalty/Reviews → SaaS hoá · và khi đó mới đấu sàn (ChannelHub đã sẵn)
```

**Ba việc phải làm trước khi code GĐ 1:**
1. Tạo `shared/eCommerce.Kernel` (Money/Currency/Address) — mọi module phụ thuộc nó.
2. Tạo `eCommerce.Web` + thêm `Volo.Account` + theme ⇒ mở khoá UI login/admin.
3. Chuyển toàn bộ quy ước ở §4 thành `docs/coding-conventions.md` + `.editorconfig` để nhóm theo một khuôn.
