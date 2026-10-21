# `PAY-001` — Cổng thanh toán VNPay: tạo giao dịch, IPN/webhook, hoàn tiền & đối soát

> **Tài liệu này SỞ HỮU module `PaymentHub`.** Trước 17/10/2026, module này **đã có code thật** nhưng
> **không tài liệu nào nhận chủ quyền** — xem §0 để biết bằng chứng và hệ quả.

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `PAY-001` |
| **Module** | `PaymentHub` |
| **Giai đoạn** | **GĐ1** — *thanh toán là điều kiện "bán được"; code đã có từ trước* |
| **Ưu tiên** | `Must have` |
| **Trạng thái** | 🟡 `Draft` — chờ review (⚠️ **vẫn KHÔNG** `Implemented`: chưa kiểm chứng **tiền thật**, chưa có outbox — xem §16.3/§16.4) |
| **Người viết / người review** | `agent (phiên 17/10/2026)` / `Kiến trúc trưởng + BA + Kế toán` |
| **Phiên bản / ngày** | `v0.3 — 18/10/2026` (phiên code `S5`: IPN + hoàn tiền) |
| **Tài liệu liên quan** | `SAL-002` · `SAL-003` (trục **tài chính**) · `SAL-006` (hoàn tiền) · `REC-001` (đối soát `querydr`) · `XC-001` · `XC-002` · `XC-003` · `XC-004` · `XC-005` · [`docs/implementation-plan.md`](../../implementation-plan.md) · [`docs/structure-and-techstack.md`](../../structure-and-techstack.md) |

---

## 0. Tóm tắt & quyết định thiết kế

**Tóm tắt 3–5 câu.** `PAY-001` là tài liệu thiết kế **sở hữu module `PaymentHub`**: một giao dịch thanh toán
(`PaymentTransaction`, bảng thật `PayTransactions`) được tạo cho một đơn, khách được chuyển sang cổng **VNPay**,
cổng gọi lại **IPN** (đã xác thực chữ ký HMAC-SHA512) để chuyển giao dịch sang `Succeeded`, tiền được **hoàn**
qua `vnp_Command=refund` (chỉ ghi sổ khi tiền **đã ra**), và trạng thái được **đối soát chủ động** bằng
`vnp_Command=querydr` (job 15 phút + thao tác tay). Giá trị mang lại: biến câu *"khách đã trả tiền chưa?"* từ
**suy đoán** thành **sự kiện có bằng chứng ký số**, và biến *"hoàn tiền"* từ thao tác tay thành **đường ghi có
bất biến** — hai thứ mà nếu sai thì **mất tiền thật**, không phải sai báo cáo.

### Vì sao tài liệu này **phải** tồn tại (bối cảnh — có bằng chứng, không suy đoán)

| # | Bằng chứng đo được | Hệ quả |
|---|---|---|
| 1 | `docs/features/sales/sal-002-checkout-va-dat-hang.md:49` ghi trong *"Không nằm trong phạm vi"*: **`❌ Thanh toán / IPN — thuộc PaymentHub + REC-001`** | `SAL-002` **từ chối** sở hữu thanh toán |
| 2 | `REC-001` **chỉ** làm đối soát (`querydr`) — xem `docs/features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md:546-600` | `REC-001` **không** sở hữu *tạo giao dịch*, *IPN*, *hoàn tiền* |
| 3 | Phụ lục A của [`docs/implementation-plan.md`](../../implementation-plan.md) (danh sách **52** tài liệu ban đầu) **không có mã `PAY-*`** | **Không** tài liệu nào sở hữu module |
| 4 | `implementation-plan.md:426` đã ghi nhận lỗ hổng này ngày **17/10/2026** và trỏ đúng tệp bạn đang đọc | Đây là **nợ tài liệu đã được thừa nhận**, không phải phát minh của phiên này |
| 5 | Code thật đã tồn tại: `PaymentTransaction`, `IPaymentProvider`, `VnPayPaymentProvider`, `VnPayRequestBuilder`, `PaymentAppService`, `LuatDoiSoatThanhToan`, `MetricDoiSoatThanhToan` — **14 tệp `.cs`** (bảng dưới) | Code **trước** tài liệu ⇒ tài liệu này phải **mô tả đúng code đang có** và **ghi rõ phần còn thiếu**, không được viết như thể mọi thứ đã xong |
| 6 | 🚨 `docs/handover/BAN-GIAO-GD1-2026-10-17.md:74` — **nợ chặn #2**: `"TmnCode": ""`, `"HashSecret": ""` **rỗng** | **Đường tiền thật CHƯA được kiểm chứng** — chi tiết §7.7 |

### Hiện trạng code — **đã đọc hết, không viết theo trí nhớ**

| Tệp (đường dẫn thật) | Dòng | Nội dung chính |
|---|---|---|
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs` | 270 | Aggregate root · enum `PaymentTransactionStatus` (9–24) · `MarkSucceeded` (100–118) · `MarkFailed` (120–139) · `RegisterRefund` (166–206) · `ConHoanDuoc` (213) · `DungDuocDeHoan` (222–224) · `MaskSensitiveData` (242–266) |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/IPaymentProvider.cs` | 159 | `CreatePaymentRequest`/`Result` (3–27) · `WebhookVerificationResult` (30–52) · interface (58–108) · `RefundResult` (129–159) |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/PaymentErrorCodes.cs` | 43 | Dải `eCommerce:Payment:0002…0005` |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/LuatDoiSoatThanhToan.cs` | 109 | `DaKetThuc` (62–65) · `Quyet` (85–108) — **hàm THUẦN** |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/MetricDoiSoatThanhToan.cs` | 70 | `Ghi` (43–69) — metric dùng chung app service + job |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/PaymentHubDomainModule.cs` | 19 | `DependsOn(AbpDddDomainModule)` — **rỗng**, không đăng ký gì |
| `modules/PaymentHub/eCommerce.PaymentHub.EntityFrameworkCore/PaymentHubDbContextModelCreatingExtensions.cs` | 42 | `ToTable("PayTransactions")` (16) · 3 index (32, 35–37, 39) |
| `modules/PaymentHub/eCommerce.PaymentHub.EntityFrameworkCore/PaymentHubEntityFrameworkCoreModule.cs` | 13 | `DependsOn(PaymentHubDomainModule, AbpEntityFrameworkCoreModule)` |
| `modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/PaymentDtos.cs` | 194 | `PaymentTransactionDto` (7–35) · `HoanTienInput` (48–53) · `HoanTienKetQuaDto` (66–100) · `IPaymentAppService` (110–158) · `KetQuaDoiSoatThanhToanDto` (174–194) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/PaymentHubApplicationContractsModule.cs` | 18 | `AbpDddApplicationContractsModule` |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/PaymentAppService.cs` | 442 | `[Authorize]` cấp class (33) · `GetTheoMaAsync` (61–72) · `GetDanhSachAsync` (74–98) · `HoanTienAsync` (102–249) · `PostTruyVanTrangThaiAsync` (255–324) · `PostDoiSoatLoatAsync` (327–418) · `ChuyenDto` (422–441) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/Providers/VnPayPaymentProvider.cs` | 408 | `VnPayOptions` (10–28) · `CreatePaymentAsync` (94–126) · `VerifyWebhookAsync` (131–197) · `RefundAsync` (214–256) · `QueryStatusAsync` (275–300) · `GoiApiGiaoDichAsync` (308–369) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/Providers/VnPayRequestBuilder.cs` | 398 | Lõi **THUẦN**: `DungChuoiKy` (38–39) · `Ky` (42–48) · `ThamSoHoanTien` (68–102) · `ThamSoTruyVan` (109–128) · `TinhSoTien` (135–136) · `DocPhanHoi` (152–228) · `KiemChuKy` (234–268) · `DocJsonPhang` (276–328) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/PaymentHubApplicationModule.cs` | 42 | Đăng ký `IPaymentProvider` → `VnPayPaymentProvider` (32–34); **KHÔNG** đăng ký `HttpClient` (chủ ý — §7.3) |

**Tệp `S4` THÊM (18/10/2026)** — đọc kèm §16:

| Tệp (đường dẫn thật) | Nội dung chính |
|---|---|
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/SinhMaThamChieu.cs` | **Hàm THUẦN** sinh `MerchantReference` ổn định: `PAY{yyyyMMdd GMT+7}-{sốĐơn}-{lầnThử}`, `Goc()` = tiền tố ổn định (để đếm số lần thử), bỏ dấu tiếng Việt (`FormD` + `Đ→D`), chặn `> 64` ký tự |
| `modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/LuatCauNoiThanhToan.cs` | **Hàm THUẦN** đối chiếu số tiền + tiền tệ của cổng với giao dịch — đóng `F-PAY-2`; `decimal.Compare` (**cấm `==`**) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/Events/PaymentEvents.cs` | `PaymentSucceededEto` · `PaymentFailedEto` — **mang dữ liệu** (KHÔNG `INotificationEvent`), có `TenantId`/`OrderId`/`Amount`/`Currency` |
| `modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/PaymentCreationDtos.cs` | `TaoGiaoDichInput` · `TaoGiaoDichKetQuaDto` · `KetQuaGhiNhanDto` · `IXuLyKetQuaCong` (⚠️ **không** phải `IApplicationService` ⇒ **không** có endpoint HTTP) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/Events/PaymentEventPublisher.cs` | Phát ETO — **một chỗ duy nhất** cho mọi đường ghi (IPN `S5` + `querydr`) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/XuLyKetQuaCongService.cs` | Ghi nhận kết quả cổng: fail-closed theo chữ ký · đối chiếu số tiền · idempotent · phát ETO **chỉ khi thật sự đổi trạng thái** |
| `src/eCommerce.Application/Sales/OrderPaymentStatusHandler.cs` | **CẦU NỐI** nhận 2 ETO ⇒ `Order.PaymentStatus` (qua `Order.DatTrangThaiThanhToan`) |
| `src/eCommerce.Domain/Entities/Orders/Order.cs` | `PaymentStatus` **setter kín** + `DatTrangThaiThanhToan` (luật hấp thụ) |
| `modules/PaymentHub/eCommerce.PaymentHub.Application/PaymentAppService.cs` | ➕ `TaoGiaoDichAsync` (khoá `payment:create:{…}` + dùng lại giao dịch đang mở); `querydr` **nay phát ETO** |

**Phạm vi (CÓ làm):**

- Sở hữu và mô tả **đúng** module `PaymentHub`: entity, enum trạng thái, 3 index thật, app service, provider VNPay.
- Định nghĩa **state machine** của `PaymentTransaction` + **ai được chuyển** + **guard sai thì làm gì** (§3).
- Định nghĩa **IPN/webhook**: xác thực `HMAC-SHA512`, `AckResponseBody`, chống trùng, sai thứ tự (§7).
- Định nghĩa **hoàn tiền** (một phần / toàn phần) và bất biến *tổng hoàn ≤ đã thu* (§8).
- Định nghĩa **đối soát** ở góc nhìn thanh toán (biên giới với `REC-001`: `REC-001` sở hữu *kỳ đối soát + lệch*,
  `PAY-001` sở hữu *hàm luật + đường ghi trạng thái*).
- Ghi rõ **mọi phần chưa có trong code** kèm kế hoạch, để không ai đọc tài liệu rồi tưởng đã xong.

**Không nằm trong phạm vi:**

- ❌ **Kỳ đối soát, bản ghi lệch (`ReconRuns`, `ReconRunMismatches`), quy trình xử lý lệch** — thuộc **`REC-001`**.
- ❌ **Tính tiền giỏ/đơn, giữ chỗ tồn kho, tạo đơn** — thuộc `PRC-002` / `SAL-002` / `SAL-001` / `INV-002`.
- ❌ **Phiếu RMA (trả hàng)** — thuộc **`SAL-006`**; `PAY-001` chỉ sở hữu **phần tiền** mà `SAL-006` gọi vào.
- ❌ **Cổng khác VNPay** (MoMo, payOS, SePay, Casso, ZaloPay) — `IPaymentProvider` đã được thiết kế để mở rộng
  (`PaymentHubApplicationModule.cs:36-40` có `TODO(thêm cổng)`), nhưng **chưa có code**; mỗi cổng mới cần một
  tài liệu riêng (`PAY-002`… chưa có mã).
- ❌ **Ví điện tử / trả sau / COD** — COD thuộc `SHP-003`.
- ❌ **Outbox/inbox & relay sự kiện** — `XC-002` (hiện **chưa** có trong code, §7.1).
- ❌ **Hạ tầng OTel → Prometheus** — metric **phát ra** nhưng **chưa có nơi thu** (`handover:243`).

### 0.1 Quyết định thiết kế (ADR ngắn)

| # | Quyết định | Phương án đã cân nhắc | Lý do chọn | Hệ quả |
|---|---|---|---|---|
| **D1** | **Tự viết abstraction `IPaymentProvider`**, không dùng `Volo.Payment` | (a) `Volo.Payment` (module PRO của ABP) · (b) tự viết interface + adapter | `Volo.Payment` cần **license ABP Team+**, chỉ có Stripe/PayPal/2Checkout/PayU/Iyzico/Alipay — **không có cổng Việt Nam**, và mô hình redirect/`StartAsync` **không khớp** SePay/Casso (webhook-first) — `PaymentHubDomainModule.cs:9-14` | Phải **tự** cài mọi thứ đặc thù VNPay (ký, mã lỗi, hoàn tiền bất đồng bộ); bù lại không phụ thuộc license |
| **D2** | **`ReturnUrl` KHÔNG phải bằng chứng thanh toán** | (a) coi redirect `vnp_ResponseCode=00` là đã trả · (b) **chỉ** IPN đã xác thực chữ ký | Người dùng **quay lại được** URL đó và **tự tạo được** URL đó — `PaymentTransaction.cs:26-33` | Cần **đường thứ hai** để xác nhận (IPN + `querydr`); nếu IPN mất thì **phải** có `querydr` |
| **D3** | Trạng thái `Succeeded` **chỉ** do IPN đã xác thực **hoặc** `querydr` đã xác thực | (a) cho app service nội bộ set tay · (b) chỉ 2 nguồn trên | Tách *"hệ thống nghĩ khách đã trả"* khỏi *"cổng nói đã thu"*; cũng khớp `SAL-003` **BR-013** (*trục tài chính chỉ `PaymentHub`/IPN ghi*, `sal-003-state-machine-4-truc.md:537`) | Không có đường "sửa tay cho xong" — đúng ý đồ, nhưng ca vận hành cần sửa tay **phải** đi qua `querydr` |
| **D4** | 🚨 **`RefundAsync` trả `RefundResult`, KHÔNG trả `bool`** | (a) `Task<bool>` · (b) 3 trạng thái tách biệt | Hoàn tiền VN **bất đồng bộ**: cổng **nhận** (`00`) rồi xử lý sau. `bool true` ⇒ người vận hành đọc *"đã hoàn xong"* ⇒ **sổ sách nói dối** — `IPaymentProvider.cs:110-159` | Người gọi **bắt buộc** phân biệt `DaNhanYeuCau` / `DaHoanThat` / `ChoXacNhan`; UI phải hiển thị 3 trạng thái |
| **D5** | 🚨 **Chỉ ghi sổ khi `DaHoanThat = true`** | (a) ghi theo `DaNhanYeuCau` · (b) chỉ ghi khi tiền **đã ra** | `PaymentAppService.cs:176-202` — nếu ghi theo (a): khách **chưa** nhận tiền mà hệ thống đã ghi `Refunded` ⇒ không ai theo dõi bước xác nhận | Yêu cầu hoàn tiền **không** làm đổi `RefundedAmount`; phải có `querydr` xác nhận ⇒ **nợ**: hiện **chưa** có job xác nhận riêng cho `ChoXacNhan` (§15) |
| **D6** | **Luật đối soát đặt ở tầng Domain, dạng hàm THUẦN** (`LuatDoiSoatThanhToan.Quyet`) | (a) để trong app service · (b) chép sang worker · (c) hàm thuần ở Domain | `PaymentAppService` có `[Authorize]` **cấp class** ⇒ interceptor ABP kiểm quyền **cả khi gọi trong tiến trình**, worker **không có principal** ⇒ `AbpAuthorizationException` (lỗi **đã xảy ra thật** ở `SAL-005`) — `LuatDoiSoatThanhToan.cs:7-29` | Luật có **một** định nghĩa, **hai** chỗ gọi (app service + worker) — `PaymentAppService.cs:263-270`, `PaymentReconciliationWorker.cs:171-177` |
| **D7** | **`MarkSucceeded` / `MarkFailed` idempotent, trạng thái CUỐI không bị ghi đè** | (a) cho ghi đè (lần cuối thắng) · (b) trạng thái cuối là **hấp thụ** | IPN là **at-least-once**; `Refunded` cũng là trạng thái cuối ⇒ webhook gửi lại sau khi hoàn tiền **không** được "hồi sinh" về `Succeeded` — `PaymentTransaction.cs:88-99` (sửa **16/10/2026**, `SAL-006`) | `MarkSucceeded` trả `false`, `MarkFailed` **return im lặng** ⇒ §3 ghi rõ đây là **ngoại lệ có chủ ý** của quy tắc #11 và **nợ** cần chỗ gọi đọc kết quả |
| **D8** | **`IdempotencyKey` = mã giao dịch **do cổng cấp** (`vnp_TransactionNo`)** + unique index có filter | (a) khoá do ta sinh · (b) mã cổng cấp | `vnp_TransactionNo` là thứ **duy nhất** định danh một lần thu tiền ở phía cổng — `PaymentTransaction.cs:73-77`, index `PaymentHubDbContextModelCreatingExtensions.cs:35-37` | Trước khi có IPN, `IdempotencyKey` là `NULL` ⇒ index **không** bảo vệ gì (đúng theo filter); chống trùng giai đoạn này dựa vào `MerchantReference` |
| **D9** | **`MerchantReference` (`vnp_TxnRef`) phải ỔN ĐỊNH** qua mọi lần retry | (a) sinh `Guid.NewGuid()` mỗi lần · (b) sinh **một lần** từ mã đơn, lưu DB | `XC-002` **BR-007**; VNPay yêu cầu `vnp_TxnRef` **không trùng trong ngày** (`VnPayPaymentProvider.cs:42`) ⇒ sinh mới mỗi lần **vừa** vi phạm idempotency **vừa** có nguy cơ trùng | Entity **bắt buộc** truyền `merchantReference` ở constructor (`PaymentTransaction.cs:81-86`); **chưa có** hàm sinh mã (§7.5 — nợ) |
| **D10** | **Khoá phân tán theo giao dịch** trước khi gọi cổng hoàn tiền | (a) chỉ dựa vào bất biến entity · (b) khoá + bất biến | Bất biến chỉ chặn ở tầng **ghi sổ**; khoá chặn ở tầng **gọi cổng** — side effect **không rollback được** (`PaymentAppService.cs:120-128`) | Hai request đồng thời ⇒ request 2 nhận lỗi `eCommerce:Lock:0001` (không phải 500) |
| **D11** | **Che dữ liệu nhạy cảm trước khi lưu payload thô** | (a) lưu nguyên văn · (b) che 2 lớp rồi cắt 8000 ký tự | Payload cổng có thể chứa số thẻ/token/OTP; rất dễ bị ghi tiếp ra log — `PaymentTransaction.cs:227-266`, `XC-005` **BR-005** | Che là **giảm thiểu**, không phải bảo đảm; **cấm** coi payload đã lưu là "an toàn để in log" |
| **D12** | **Không đăng ký `HttpClient` trong module nghiệp vụ** — đăng ký **typed client ở Host** | (a) `AddHttpClient` trong `PaymentHub.Application` · (b) ở Host | `Microsoft.Extensions.Http` **không** có trong `Volo.Abp.Ddd.Application`; thêm vào module là kéo hạ tầng web xuống tầng application (`XC-002` D12 cấm) — `PaymentHubApplicationModule.cs:23-31`, `eCommerceHttpApiHostModule.cs:171-192` | Host **quên** đăng ký ⇒ provider trả **lỗi cấu hình có mã** (`RefundAsync` dòng 238–245) thay vì `NullReferenceException` bị hiểu nhầm thành *"hoàn tiền thất bại"* |
| **D13** | **Tiền là `decimal` + `HasPrecision(18,4)`; VNPay nhân 100 làm tròn `AwayFromZero`** | (a) `double` · (b) `decimal` | `XC-001` **BR-003**; làm tròn ngân hàng (`.5 → chẵn`) cho **1 đồng khác** ⇒ cổng báo *"số tiền không khớp"* — `VnPayRequestBuilder.cs:130-136`, `PaymentHubDbContextModelCreatingExtensions.cs:27-28` | Cấm so tiền bằng `==`; so bằng `>`/`>=` hoặc `decimal.Compare` |
| **D14** | **Hai index unique trên `PayTransactions` viết lại bằng SQL thô có `NULLS NOT DISTINCT`** | (a) để EF sinh `unique: true` · (b) SQL thô | PostgreSQL mặc định **NULLS DISTINCT** ⇒ khi `TenantId IS NULL` index **không chặn trùng** (đã chứng minh bằng `INSERT 0 2`) — `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:7-33` | EF **không** mô hình hoá thuộc tính này ⇒ mọi migration sau tạo lại index **phải** chạy lại SQL này; test `PostgreSqlSchemaTests` khoá con số **48** |

> 📌 **Ghi lại cả phương án BỊ LOẠI** (D1, D4, D7, D8) — để 6 tháng sau không ai hỏi lại *"sao không dùng `Volo.Payment`"*
> hay *"sao không trả `bool` cho gọn"*. Cả hai câu đó đều đã được cân nhắc và **có lý do viết trong code**.

---

## 1. Use Case Specification

| Thuộc tính | Nội dung |
|---|---|
| **Use Case ID** | `PAY-001` |
| **Use Case Name** | Vòng đời một giao dịch thanh toán VNPay (tạo → IPN → hoàn tiền → đối soát) |
| **Description** | *"Là **khách mua hàng**, tôi muốn trả tiền qua VNPay để đơn được xử lý; là **kế toán/CSKH**, tôi muốn hoàn tiền và **chứng minh được** tiền đã ra; là **hệ thống**, tôi muốn biết chắc khách đã trả hay chưa kể cả khi IPN mất."* |
| **Actor(s)** | **Khách hàng** (redirect sang cổng) · **VNPay** (IPN + API giao dịch) · **Kế toán/CSKH** (hoàn tiền, truy vấn) · **Job `PaymentReconciliationWorker`** (đối soát định kỳ) · **Hệ thống bán hàng** (`SAL-001`/`SAL-002` tạo giao dịch — **chưa có trong code**) |
| **Priority** | `Must` |
| **Trigger** | (a) đơn được xác nhận cần thanh toán trước ⇒ tạo giao dịch + redirect; (b) VNPay gọi `IpnUrl`; (c) kế toán bấm **Hoàn tiền** / **Truy vấn trạng thái**; (d) nhịp job **15 phút** (`PaymentReconciliationWorker.cs:49,76-80`) |
| **Pre-Condition(s)** | • **Tenant hiện tại** (`PaymentTransaction.TenantId` từ `IMultiTenant`, `PaymentTransaction.cs:34-36`) — dữ liệu tenant A **không** được thấy từ tenant B.<br>• **Kênh bán** (`ChannelCode`) **gián tiếp**: giao dịch gắn `OrderId` (`PaymentTransaction.cs:41-42`), đơn mang `ChannelCode`.<br>• Đã có **`MerchantReference`** ổn định (`PaymentTransaction.cs:44-48`) — **chưa có** hàm sinh (§7.5).<br>• Có credential cổng: `PaymentHub:VnPay:TmnCode` + `HashSecret` (`appsettings.json:21-27`) — 🚨 **hiện rỗng** ⇒ đường tiền thật **chưa kiểm chứng**.<br>• Với hoàn tiền: giao dịch **đã thu tiền** (`Status ∈ {Succeeded, PartiallyRefunded, Refunded}`, `PaymentTransaction.cs:222-224`).<br>• Với IPN: `IpnUrl` **HTTPS** (`VnPayPaymentProvider.cs:43`) — **chưa có endpoint** nhận (§7.2 nợ #2). |
| **Post-Condition(s)** | • Giao dịch có **đúng một** trạng thái cuối; `RefundedAmount` **không âm** và **không vượt** `Amount`.<br>• IPN **luôn** được trả `AckResponseBody` (`VnPayPaymentProvider.cs:191-194`) để cổng ngừng retry.<br>• Mọi thay đổi trạng thái để lại **dấu vết**: `WebhookReceivedCount`, `LastWebhookPayload` (đã che), log có neo `[PAY-…]`.<br>• Số tiền **đã hoàn** đọc được từ `RefundedAmount` + `ConHoanDuoc`. |
| **Basic Flow** | 1. `SAL-002`/`SAL-001` tạo `PaymentTransaction(Id, "vnpay", merchantReference, amount)` — trạng thái `Initiated` (`PaymentTransaction.cs:81-86`).<br>2. App service gọi `IPaymentProvider.CreatePaymentAsync` ⇒ dựng tham số `vnp_*` đã sắp xếp, ký `HMAC-SHA512`, trả `CheckoutUrl` (`VnPayPaymentProvider.cs:94-126`).<br>3. Khách thanh toán ở cổng; VNPay gọi **IPN** ⇒ `VerifyWebhookAsync` kiểm chữ ký + 2 mã `00` (`VnPayPaymentProvider.cs:131-197`).<br>4. Hệ thống gọi `PaymentTransaction.MarkSucceeded(...)` ⇒ `Succeeded`, ghi `PaidAtUtc`, `ProviderTransactionId`, `IdempotencyKey` (`PaymentTransaction.cs:100-118`).<br>5. Khi cần trả hàng: `SAL-006` gọi `HoanTienAsync` ⇒ khoá → kiểm → gọi cổng `refund` → **chỉ ghi sổ nếu `DaHoanThat`** (`PaymentAppService.cs:102-249`).<br>6. Định kỳ: job/`querydr` xác nhận lại các giao dịch **chưa ở trạng thái cuối** (`PaymentReconciliationWorker.cs:122-240`). |
| **Alternative Flow** | **A1: Cổng đã nhận yêu cầu hoàn tiền nhưng chưa xác nhận tiền ra** — trả `ChoXacNhan = true`, **KHÔNG** tăng `RefundedAmount`, log `Warning` `[PAY-CHO-XAC-NHAN]` (`PaymentAppService.cs:203-213`).<br>**A2: IPN gửi trùng** — `MarkSucceeded` trả `false`, chỉ `WebhookReceivedCount++`, trạng thái **giữ nguyên** (`PaymentTransaction.cs:102-110`).<br>**A3: Cổng nói "chưa biết"** (`Pending`/`Initiated` từ `querydr`) — `KhongDoi`, **không hạ trạng thái**, ghi nhận *"KHÔNG KIỂM ĐƯỢC"* (`LuatDoiSoatThanhToan.cs:105-107`).<br>**A4: Giao dịch đã ở trạng thái cuối** — `DaKetThuc` ⇒ `KhongDoi`, **không** gọi ghi (`LuatDoiSoatThanhToan.cs:62-65,87-91`).<br>**A5: Hoàn tiền một phần** — `RefundedAmount < Amount` ⇒ `PartiallyRefunded` (`PaymentTransaction.cs:203-205`). |
| **Exception Flow** | **E1: Chữ ký IPN sai/thiếu** ⇒ `SignatureValid = false`, **không** đổi trạng thái, trả `{"RspCode":"97"}` + metric `payment_webhook_rejected_total` (`VnPayPaymentProvider.cs:141-169,192-194`).<br>**E2: Hoàn vượt số đã thu** ⇒ `BusinessException` `eCommerce:Payment:0002` (`PaymentTransaction.cs:189-199`).<br>**E3: Hoàn cho giao dịch chưa thu tiền** ⇒ `eCommerce:Payment:0003` (`PaymentTransaction.cs:175-185`).<br>**E4: Số tiền hoàn ≤ 0 hoặc thiếu lý do/người thực hiện** ⇒ `eCommerce:Payment:0004` (`PaymentTransaction.cs:168-173`, `PaymentAppService.cs:108-118`).<br>**E5: Không tìm thấy giao dịch** ⇒ `eCommerce:Payment:0005` (`PaymentAppService.cs:130-132`).<br>**E6: Không giành được khoá phân tán** ⇒ `eCommerce:Lock:0001` (`PaymentAppService.cs:125-128`).<br>**E7: Lỗi hạ tầng/timeout khi gọi cổng** ⇒ `LoiHaTang` khác rỗng + `ChoXacNhan = true`, **cấm retry mù** (`VnPayPaymentProvider.cs:341-368`, `PaymentAppService.cs:223-233`). |
| **Business Rules** | • **BR-PAY-001**: Chỉ IPN **hoặc** `querydr` **đã xác thực chữ ký** được chuyển giao dịch sang `Succeeded`; không có đường khác.<br>• **BR-PAY-002**: `MerchantReference` **ổn định** — sinh **một lần**, lưu DB; **cấm** sinh mới mỗi lần gọi.<br>• **BR-PAY-003**: Cùng một giao dịch của cổng **không** được ghi nhận hai lần (idempotent theo `IdempotencyKey` + `WebhookReceivedCount`).<br>• **BR-PAY-004**: Trạng thái **cuối** (`Succeeded`/`PartiallyRefunded`/`Refunded`) **không** bị ghi đè bởi bất kỳ webhook nào.<br>• **BR-PAY-005**: Chỉ hoàn được giao dịch **đã thu tiền** (`DungDuocDeHoan`).<br>• **BR-PAY-006**: `amount > 0` cho mọi lần hoàn.<br>• **BR-PAY-007**: `RefundedAmount + amount ≤ Amount`; vi phạm ⇒ **NÉM** (không kẹp, không làm tròn cho vừa).<br>• **BR-PAY-008**: Chỉ gọi `RegisterRefund` khi `RefundResult.DaHoanThat = true`.<br>• **BR-PAY-009**: Lỗi hạ tầng ⇒ `ChoXacNhan = true`; **cấm** retry không kiểm tra.<br>• **BR-PAY-010**: Hoàn tiền **bắt buộc** có `LyDo` và `NguoiThucHien`.<br>• **BR-PAY-011**: Mọi lần gọi cổng hoàn tiền nằm trong khoá `payments:refund:{paymentTransactionId}`.<br>• **BR-PAY-012**: Chữ ký HMAC-SHA512 trên chuỗi **sắp xếp `Ordinal`** + URL-encode, **bỏ** `vnp_SecureHash*`.<br>• **BR-PAY-013**: Chỉ 2 ca **thiếu chữ ký** / **chữ ký sai** mới tính là "từ chối" (metric); chữ ký đúng mà giao dịch thất bại **không** tính.<br>• **BR-PAY-014**: Đối soát **chỉ tiến, không lùi**; thứ tự 4 nhánh của `Quyet` là bắt buộc.<br>• **BR-PAY-015**: *"Không kiểm được"* **khác** *"không thành công"* — tách `SoKhongXacDinh` khỏi `SoDoiTrangThai`.<br>• **BR-PAY-016**: Mọi `PaymentAppService` có `[Authorize]` **cấp class**; hoàn tiền cần thêm permission `eCommerce.Payments.Refund`.<br>• **BR-PAY-017**: Dữ liệu **cách ly theo tenant**; mọi unique index trên `IMultiTenant` có `NULLS NOT DISTINCT`.<br>• **BR-PAY-018**: Payload thô lưu DB **phải** được che dữ liệu nhạy cảm và cắt ≤ 8000 ký tự.<br>• **BR-PAY-019**: **Cấm** so tiền bằng `==`; tiền là `decimal`.<br>• **BR-PAY-020**: Ba ca hoàn tiền lỗi phải có **ba mã riêng** (không gộp).<br>• **BR-PAY-021**: Background worker **không** được gọi app service có `[Authorize]` — dùng repository + hàm luật thuần. |
| **Non-Functional Requirement** | • **NFR-PAY-001**: `GET danh-sach` p95 **< 300 ms** với **10 000** giao dịch/tenant.<br>• **NFR-PAY-002**: **100 %** endpoint trả `401` khi không token và `403` khi thiếu permission.<br>• **NFR-PAY-003**: **100 %** lần đổi trạng thái có ≥ 1 dòng log **có cấu trúc** mang neo `[PAY-…]`.<br>• **NFR-PAY-004**: Mọi log trong phạm vi request/job mang `CorrelationId`.<br>• **NFR-PAY-005**: Dữ liệu tenant A **không** đọc/ghi được từ tenant B (test 2 tenant).<br>• **NFR-PAY-006**: IPN trả phản hồi **< 1 000 ms** p95 (đo trên Host) — ngưỡng chính thức của VNPay **chưa kiểm chứng**.<br>• **NFR-PAY-007**: **0** payload lưu DB chứa số thẻ đầy đủ (PAN 12–19 chữ số).<br>• **NFR-PAY-008**: Job đối soát nhịp **900 s**, trần **50** giao dịch/lượt.<br>• **NFR-PAY-009**: Job **không** log `Information` mỗi vòng (`XC-005` **BR-004**).<br>• **NFR-PAY-010**: Metric chỉ dùng nhãn **hữu hạn** (`provider`, `reason`, `result`); **< 100** series.<br>• **NFR-PAY-011**: Số tiền chính xác tới **4** chữ số thập phân (`numeric(18,4)`), VNPay nhận **×100** số nguyên.<br>• **NFR-PAY-012**: IPN mất ⇒ phát hiện trong **≤ 15 phút** (nhịp job) **hoặc** khi người vận hành bấm truy vấn. |

> ⚠️ **Nhắc lại yêu cầu của mẫu:** *mỗi BR/NFR phải kiểm chứng được* ⇒ tất cả 21 BR + 12 NFR ở trên đều có
> test tương ứng ở §14.1. Nếu một điều không viết được test thì nó **chưa phải** BR (mẫu §1 ghi chú).

---

## 2. Sơ đồ Use Case

```mermaid
flowchart LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef usecase fill:#e2f4f6,stroke:#17a2b8,stroke-width:2px;
    classDef note fill:#fffbe6,stroke:#ffc107,stroke-width:1px,color:#5c4a00;

    KH(("👤<br/>«Khách hàng»"))
    KT(("👤<br/>«Kế toán / CSKH»"))
    VNP(("🏦<br/>«VNPay»"))
    JOB(("⚙️<br/>«Job đối soát»"))
    class KH,KT,VNP,JOB actor;

    subgraph PKG["📦 «PaymentHub — PAY-001»"]
        direction TB
        UC1(["«Tạo giao dịch + lấy CheckoutUrl»"])
        UC2(["«Nhận IPN và xác thực chữ ký»"])
        UC3(["«Hoàn tiền một phần / toàn phần»"])
        UC4(["«Truy vấn trạng thái querydr»"])
        UC5(["«Đối soát lô theo nhịp»"])
        UC6(["«Tra cứu giao dịch trên UI»"])
        class UC1,UC2,UC3,UC4,UC5,UC6 usecase;
    end

    KH --- UC1
    VNP --- UC2
    KT --- UC3
    KT --- UC4
    KT --- UC6
    JOB --- UC5

    UC1 -. "«include» xác thực chữ ký khi ký" .-> UC2
    UC3 -. "«include» chỉ ghi sổ khi tiền đã ra" .-> UC4
    UC5 -. "«include» dùng chung luật Quyet" .-> UC4

    N1["<b>Mục tiêu:</b><br/>«biết chắc khách đã trả tiền hay chưa»<br/>«và hoàn tiền có bằng chứng»"]
    class N1 note;
    UC2 -.- N1

    N2["<b>Chưa có trong code:</b><br/>«endpoint IPN» · «UC1 là kế hoạch GĐ1»"]
    class N2 note;
    UC2 -.- N2
```

### Chú giải

| Ký hiệu | Ý nghĩa |
|---|---|
| ⭕ Xanh lá | Actor (người hoặc hệ thống ngoài) |
| 🟦 Bầu dục xanh dương | Use Case |
| 🟨 Khung vàng | Note |
| `───` | Liên kết Actor ↔ Use Case |
| `-.«include».->` | Quan hệ bao hàm |

> 📌 **Đọc sơ đồ cho đúng:** `UC1` (tạo giao dịch) **chưa có trong code** — `CreatePaymentAsync` tồn tại và đã
> được kiểm ở **lõi thuần** (`test/eCommerce.Tests/PaymentHub/VnPayWebhookTests.cs:125`), nhưng **không có chỗ
> nào gọi nó** trong `src/` (grep `CreatePaymentAsync` chỉ ra provider + test). Tương tự `UC2`: **không có
> endpoint IPN** (§7.2). `UC3`, `UC4`, `UC5`, `UC6` **đã có code thật** và đã được đo bằng HTTP (§0, §6.2).

---

## 3. Sơ đồ trạng thái (state machine) — **BẮT BUỘC nếu feature có vòng đời**

> ⚠️ **Ba trạng thái trong đề bài KHÔNG có trong code — ghi rõ để không ai code theo tài liệu sai:**
> `Processing` **không tồn tại**; `Expired` **không tồn tại**. Enum thật có **đúng 7** giá trị
> (`PaymentTransaction.cs:9-24`): `Initiated = 0` · `Pending = 1` · `Succeeded = 2` · `Failed = 3` ·
> `Cancelled = 4` · `Refunded = 5` · `PartiallyRefunded = 6`. Mọi sơ đồ/bảng dưới đây vẽ theo **enum thật**.

```mermaid
stateDiagram-v2
    [*] --> Initiated : «tạo PaymentTransaction» (guard: có MerchantReference + amount > 0)
    Initiated --> Pending : «chờ khách thanh toán» (CHƯA CÓ ĐƯỜNG GHI)
    Initiated --> Succeeded : «IPN hoặc querydr đã xác thực» (guard: chữ ký hợp lệ + 2 mã 00)
    Pending --> Succeeded : «IPN hoặc querydr đã xác thực»
    Pending --> Failed : «cổng báo thất bại»
    Initiated --> Failed : «cổng báo thất bại»
    Failed --> Succeeded : «querydr xác nhận tiền ĐÃ vào» (lưới an toàn khi IPN mất)
    Succeeded --> PartiallyRefunded : «RegisterRefund một phần» (guard: tổng hoàn < Amount)
    Succeeded --> Refunded : «RegisterRefund hoàn TOÀN BỘ» (guard: tổng hoàn bằng Amount)
    PartiallyRefunded --> Refunded : «RegisterRefund phần còn lại»
    PartiallyRefunded --> PartiallyRefunded : «IPN gửi trùng» (no-op, chỉ tăng bộ đếm)
    Refunded --> Refunded : «IPN gửi trùng» (no-op, KHÔNG hồi sinh về Succeeded)
    Succeeded --> Succeeded : «IPN gửi trùng» (no-op)
    Failed --> Failed : «webhook thất bại lặp lại» (no-op)
    Refunded --> [*]
```

### 3.1 Bảng chuyển trạng thái & guard

| # | Từ → Sang | Ai được chuyển | Guard (điều kiện cho phép) | Hành động kèm theo | Nếu guard sai thì? | Có code chưa? |
|---|---|---|---|---|---|---|
| 1 | *(không có)* → `Initiated` | Hệ thống bán hàng (`SAL-002`/`SAL-001`) | Constructor có `merchantReference` khác rỗng + `amount` | `Status` khởi tạo `Initiated` (`PaymentTransaction.cs:58,81-86`) | ✅ **`S4`:** constructor **NÉM** `0007` (`amount ≤ 0`) và `0006` (mã/cổng rỗng) — nợ §3.1 cũ **ĐÃ ĐÓNG** | Entity **có**; chỗ gọi **ĐÃ CÓ** (`PaymentAppService.TaoGiaoDichAsync`) |
| 2 | `Initiated` → `Pending` | Hệ thống (sau khi đã redirect khách) | Đã có `CheckoutUrl` | — | **Không có method nào** ⇒ trạng thái `Pending` **không ghi được vào DB** | ❌ **chưa có trong code — kế hoạch GĐ1** |
| 3 | `Initiated`/`Pending` → `Succeeded` | **CHỈ** IPN đã xác thực **hoặc** `querydr` đã xác thực | Chữ ký hợp lệ **VÀ** `vnp_ResponseCode = 00` **VÀ** `vnp_TransactionStatus = 00` (`VnPayPaymentProvider.cs:181-189`) | `PaidAtUtc`, `ProviderTransactionId`, `IdempotencyKey`, `FailureReason = null` (`PaymentTransaction.cs:112-116`) | IPN sai chữ ký ⇒ **không đổi trạng thái**, trả `RspCode 97` (E1 §1) | ✅ `MarkSucceeded` có; **chỗ gọi IPN chưa có** |
| 4 | `Initiated`/`Pending` → `Failed` | IPN báo thất bại **hoặc** `querydr`/job kết luận không thành công | Cổng trả mã khác `00` (`QueryStatusAsync` ánh xạ `01/02/04/07` → `Failed`, `VnPayPaymentProvider.cs:293-299`) | `FailureReason` (cắt 2000 ký tự) (`PaymentTransaction.cs:137-138`) | Giao dịch **đã có tiền** ⇒ `MarkFailed` **return im lặng**, **không** ghi đè (§3.2) | ✅ có |
| 5 | `Failed` → `Succeeded` | `querydr` (app service **hoặc** job) | `Quyet` nhánh ②: cổng nói `Succeeded` (`LuatDoiSoatThanhToan.cs:93-97`) | Ghi `Succeeded` + log `Warning` `[PAY-QUERYDR-THU-DUOC]` / `[PAY-JOB-THU-DUOC]` | `DaKetThuc(Failed) = false` ⇒ `Failed` **vẫn được** đối soát lại (cố ý) | ✅ có |
| 6 | `Failed` → `Failed` | `querydr` | `Quyet` nhánh ③ (`LuatDoiSoatThanhToan.cs:99-103`) | `FailureReason` mới (nguồn `querydr`) | — | ✅ có |
| 7 | `Initiated`/`Pending` → `Cancelled` | — | **Không có đường nào** | — | ⚠️ `QueryStatusAsync` **trả** `Cancelled` khi mã `24` (`VnPayPaymentProvider.cs:296`) **nhưng** `MarkFailed` ghi `Failed` | 🟠 **`Cancelled` là trạng thái CHẾT trong code** — xem §3.4 |
| 8 | `Succeeded` → `PartiallyRefunded` | Kế toán/CSKH (**permission** `eCommerce.Payments.Refund`) | `0 < amount` **VÀ** `RefundedAmount + amount < Amount` **VÀ** `Status` đã thu tiền | `RefundedAmount` tăng; log `[PAY-DA-HOAN]` (`PaymentTransaction.cs:201-205`) | `amount ≤ 0` ⇒ **NÉM** `0004`; chưa thu tiền ⇒ **NÉM** `0003`; vượt ⇒ **NÉM** `0002` | ✅ có |
| 9 | `Succeeded` → `Refunded` | như #8 | `RefundedAmount + amount >= Amount` | như #8 | như #8 | ✅ có |
| 10 | `PartiallyRefunded` → `Refunded` | như #8 | `RefundedAmount + amount == Amount` theo **cùng đơn vị** | như #8 | như #8 | ✅ có |
| 11 | `Succeeded`/`PartiallyRefunded`/`Refunded` → *bất kỳ* | **KHÔNG AI** | **Hấp thụ (absorbing)** | — | `MarkSucceeded` trả `false`; `MarkFailed` return im lặng | ✅ có (nhưng **không ném** — §3.2) |
| 12 | `Refunded` → *bất kỳ* kể cả `Succeeded` | **KHÔNG AI** | Hấp thụ | — | Ghi đè **bị chặn** từ **16/10/2026** (`PaymentTransaction.cs:92-99`) | ✅ có |

> 🔒 **Trạng thái hấp thụ (absorbing):** `Succeeded` **chỉ** hấp thụ đối với **webhook** (vẫn đi ra được bằng
> `RegisterRefund`); `Refunded` và `PartiallyRefunded` **không** có đường ra. Sau `Refunded`, **mọi** chuyển dịch
> đều bị chặn — kể cả về `Succeeded`.

### 3.2 🚨 Ngoại lệ của quy tắc bất di bất dịch #11 — và vì sao nó **vẫn** là nợ

Quy tắc #11 (`docs/design-doc-guidelines.md:113-115`, `sal-003-state-machine-4-truc.md:285-287`): *guard sai **phải
NÉM LỖI** hoặc **log có cấu trúc**, không im lặng*. Code hiện tại **cố ý** đi theo hướng "log có cấu trúc" ở **một
nửa** và **im lặng** ở nửa còn lại:

| Hàm | Guard sai thì | Có phải lỗi? | Đánh giá |
|---|---|---|---|
| `MarkSucceeded` (`PaymentTransaction.cs:105-110`) | `return false` — **không** ném, **không** log | ❌ **không phải lỗi**: IPN là **at-least-once** ⇒ gửi trùng là **bình thường** | 🟡 **Chấp nhận được** vì có **giá trị trả về**; 🔒 chỗ gọi **BẮT BUỘC** đọc `false` để tăng metric/log. ✅ **`S4`:** đã có chỗ gọi đường thật — `XuLyKetQuaCongService` đọc giá trị này để **chỉ phát ETO khi thật sự đổi** |
| `MarkFailed` (`PaymentTransaction.cs`) | trước `S4`: `return;` — **void**, hoàn toàn im lặng | ❌ không phải lỗi (cùng lý do) | ✅ **ĐÃ SỬA Ở `S4`** — nay trả **`bool`** (`true` **chỉ khi** trạng thái thật sự đổi sang `Failed`), 3 chỗ gọi trong `src/` đã cập nhật, và 3 test khoá hai chiều (`PaymentTransactionTaoMoiTests`) ⇒ `F-PAY-9` **đóng** |
| `RegisterRefund` (`PaymentTransaction.cs:166-206`) | **NÉM** `BusinessException` có mã + `WithData` | ✅ **là lỗi thật** | ✅ **Đúng chuẩn #11** — đây là mẫu để các hàm khác theo |
| `LuatDoiSoatThanhToan.Quyet` | **Không** ném — hàm **thuần** trả quyết định | — | ✅ Đúng: quyết định *"không đổi"* **không phải** guard sai; chỗ gọi **phân biệt** hai ca bằng `DaKetThuc` (`PaymentAppService.cs:296-321`) |

> 📌 **Vì sao `MarkFailed` im lặng là NGUY HIỂM, không phải "cho gọn":** job đối soát **đếm** `soDoi++` sau khi
> gọi `MarkFailed` (`PaymentReconciliationWorker.cs:196-203`). Nếu giao dịch **đã** ở trạng thái cuối thì entity
> bỏ qua, **DB không đổi**, nhưng job **vẫn đếm là "đã đổi trạng thái"** ⇒ **báo cáo đối soát SAI**. Chính vì
> rủi ro này mà `LuatDoiSoatThanhToan.cs:50-60` đã ghi rõ: *hai hàm trạng thái phải **nhất quán** về "trạng thái
> nào là CUỐI"* — lệch nhau là mầm của sổ sách tự mâu thuẫn. Hiện **đã** nhất quán (`DaKetThuc` = 3 trạng thái =
> danh sách trong `MarkSucceeded`/`MarkFailed`), nhưng **cơ chế đếm** vẫn chưa đọc được kết quả ghi.

### 3.3 Ai được chuyển — tổng hợp một dòng cho mỗi actor

| Actor | Được chuyển sang | Không được | Căn cứ |
|---|---|---|---|
| **Người dùng cuối (khách)** | **KHÔNG GÌ** | mọi trạng thái | `ReturnUrl` không phải bằng chứng (D2) |
| **IPN/webhook VNPay** | `Succeeded`, `Failed` | `Refunded`/`PartiallyRefunded`, `Cancelled` | `MarkSucceeded`/`MarkFailed` là **mutator duy nhất** ngoài refund |
| **`querydr` (app service `PostTruyVanTrangThaiAsync`)** | `Succeeded`, `Failed` | hạ trạng thái | `LuatDoiSoatThanhToan.Quyet` |
| **Job `PaymentReconciliationWorker`** | `Succeeded`, `Failed` | hạ trạng thái; gọi app service | đọc repository trực tiếp (`PaymentReconciliationWorker.cs:124-125`) |
| **Kế toán/CSKH (`eCommerce.Payments.Refund`)** | `PartiallyRefunded`, `Refunded` | `Succeeded`, `Failed` | `RegisterRefund` sau khi cổng xác nhận |
| **Background job khác** | **KHÔNG GÌ** (cho tới khi có outbox) | — | `XC-002` chưa có trong code |

### 3.4 🚨 Ba phát hiện về state machine (đọc code, không suy đoán)

| # | Phát hiện | Bằng chứng | Ảnh hưởng |
|---|---|---|---|
| **F1** | **`Pending` không có đường ghi vào DB.** `Status` mặc định là `Initiated`; **không** có method `MarkPending`/`SetPending`; `PaymentAppService` không ghi `Pending` | `PaymentTransaction.cs:58` (khởi tạo), grep `Pending` trong `modules/PaymentHub` chỉ thấy **đọc** (so sánh, ánh xạ) | Job đối soát **vẫn** quét `Initiated` nên không mất giao dịch — nhưng UI *"đang chờ khách thanh toán"* **không phân biệt được** với *"mới tạo, chưa redirect"* |
| **F2** | **`Cancelled` là trạng thái CHẾT.** `QueryStatusAsync` trả `Cancelled` khi `vnp_ResponseCode = 24`; `PostTruyVanTrangThaiAsync` gọi `MarkFailed(...)` ⇒ DB ghi **`Failed`**, không bao giờ ghi `Cancelled` | `VnPayPaymentProvider.cs:296`; `PaymentAppService.cs:289-294`; `PaymentTransaction.cs:137` | *"Khách **cố ý** huỷ"* và *"giao dịch **lỗi**"* bị **gộp** trong DB ⇒ báo cáo *"tỉ lệ khách huỷ"* **không làm được**; `FailureReason` có phân biệt bằng **chuỗi** (không phải dữ liệu có cấu trúc) |
| **F3** | **`Initiated → Succeeded` không đi qua `Pending`** — nghĩa là mọi giao dịch thật sẽ nhảy thẳng từ `Initiated` sang `Succeeded` | sơ đồ §3, không có method `Pending` | Không sai nghiệp vụ, nhưng **thời gian chờ thanh toán** (thời gian khách ở trang cổng) **không đo được** từ dữ liệu |

> 🔑 **Cả 3 phát hiện đều thuộc loại "không có lỗi nào nổi lên"** — đúng họ lỗi mà dự án đang chống. Đề xuất
> xử lý ở §15.1 **Q4, Q5** (cần BA quyết: có cần phân biệt *khách huỷ* với *lỗi* không, và có cần đo thời gian chờ không).

### 3.5 Liên kết với **trục tài chính** của đơn (`SAL-003`)

`SAL-003` §3 định nghĩa **trục 2 (tài chính)** của đơn là `Order.PaymentStatus` và quy định **BR-013**: *trục tài
chính **chỉ** `PaymentHub`/IPN ghi* (`sal-003-state-machine-4-truc.md:537`).

⚠️ **Trước hết, sửa một chi tiết dễ sai:** enum **thật** trong code là
`PaymentStatus { Unpaid = 0, Pending = 1, Paid = 2, PartiallyRefunded = 3, Refunded = 4, Failed = 5 }`
(`src/eCommerce.Domain.Shared/Enums.cs:80-88`) — **không có** `PendingPayment`/`FailedPayment` (hai tên đó chỉ xuất
hiện trong **tài liệu** `SAL-003`). Đây đúng loại lệch *"tài liệu ≠ code"* mà tài liệu này phải chỉ ra.
Bảng ánh xạ **phải** là:

| `PaymentTransaction.Status` | `Order.PaymentStatus` (giá trị **THẬT** trong enum) | Ghi chú |
|---|---|---|
| `Initiated` | `Unpaid` (0) | chưa có tiền |
| `Pending` | `Pending` (1) | ⚠️ trạng thái này **chưa ghi được** (F1) |
| `Succeeded` | `Paid` (2) — guard: **số tiền khớp** + cùng `Currency` | `SAL-003` #12: lệch tiền ⇒ `0008` |
| `Failed`/`Cancelled` | `Failed` (5) | `Cancelled` bị gộp (F2) |
| `PartiallyRefunded` | `PartiallyRefunded` (3) | `SAL-003` #15 |
| `Refunded` | `Refunded` (4) — hấp thụ | `SAL-003` #16–#18 |

> ✅ **CẦU NỐI NÀY ĐÃ CÓ TRONG CODE — `S4`, 18/10/2026.** (Trước `S4`: `Order.PaymentStatus` tồn tại, có **setter
> công khai**, mặc định `Unpaid`, và grep toàn repo chỉ thấy **đọc để hiển thị** ⇒ **không một dòng nào GÁN** ⇒
> *"khách trả tiền xong thì đơn vẫn `Unpaid`"* — **nợ #1** của tài liệu này.)
>
> **Đường đi hiện tại (đã có, đã test):**
> `PaymentTransaction` đổi trạng thái (`XuLyKetQuaCongService` cho IPN đã xác thực · `PaymentAppService` `querydr`)
> ⇒ `PaymentEventPublisher` phát `PaymentSucceededEto`/`PaymentFailedEto` (**chỉ khi thật sự đổi**)
> ⇒ `OrderPaymentStatusHandler` (`src/eCommerce.Application/Sales/`) nhận ⇒ đối chiếu số tiền/tiền tệ
> (`LuatCauNoiThanhToan`) ⇒ `Order.DatTrangThaiThanhToan` (luật hấp thụ).
> 🔒 **`Order.PaymentStatus` nay KHÔNG còn setter công khai** — đường ghi duy nhất là hàm đó, và có test chặn việc
> mở lại setter. 📌 Bảng ánh xạ ở trên vẫn đúng **từng dòng**; 🔑 `Initiated`/`Pending` **không** ghi gì lên đơn.
>
> ⚠️ **`PartiallyRefunded`/`Refunded` lên đơn CHƯA được nối** (`PaymentRefundedEto` chưa có — thuộc `S5`/`SAL-006`);
> luật hấp thụ đã sẵn sàng nhận hai trạng thái đó. Xem §16.3.


---

## 4. Sequence Diagram & luồng dữ liệu

### 4.1 Sequence — nhận IPN và chuyển `Succeeded` (**ĐÍCH — chưa có trong code**)

> 🆕 **`S5` (18/10/2026): LUỒNG NÀY ĐÃ CÓ TRONG CODE.** Endpoint `GET /api/webhooks/vnpay`
> (`src/eCommerce.HttpApi.Host/Controllers/VnPayIpnController.cs`) đã tồn tại, có `[AllowAnonymous]`, nằm trong
> allowlist (`AllowlistCongKhaiTests`) và **đã được đo qua HTTP thật** (`tools/probe-thanh-toan.ps1`, 26/26 ca).
> 📌 Phần dưới đây **giữ nguyên như thiết kế gốc**, với hai khác biệt **đã chốt**:
> ① **sai chữ ký ⇒ `HTTP 400`** (không phải `200` + `RspCode 97`) — xem §7.2; ② chữ ký được kiểm trên **query
> THÔ** (`KiemChuKyTuQueryTho`) — vá `F-PAY-1` + lỗi encode lại, xem §7.2.1.
>
> ⚠️ **Đọc đúng mức độ:** luồng đúng **ở mức mã + HTTP giả lập có chữ ký THẬT**; 🔒 **chưa** có một IPN nào từ
> **VNPay thật** đi vào (credential rỗng — §7.6). Đó là lý do tài liệu **vẫn** `Draft`.

```mermaid
sequenceDiagram
    actor VNP as VNPay
    participant API as HttpApi.Host
    participant CTRL as VnPayIpnController «S5»
    participant PRV as VnPayPaymentProvider
    participant APP as XuLyKetQuaCongService
    participant DOM as PaymentTransaction
    participant DB as PostgreSQL

    VNP->>API: GET /api/webhooks/vnpay?«vnp_*»
    API->>CTRL: chuyển raw query string + headers
    CTRL->>PRV: VerifyWebhookAsync(rawQuery, headers)
    Note over PRV: bỏ vnp_SecureHash và vnp_SecureHashType · bỏ tham số RỖNG · sắp xếp Ordinal · KHÔNG decode/encode lại · HMAC-SHA512
    PRV-->>CTRL: WebhookVerificationResult (SignatureValid · IsSuccess · Amount · MerchantReference · AckResponseBody)
    alt «chữ ký SAI hoặc thiếu»
        CTRL-->>VNP: 400 + RspCode 97 Invalid Checksum
        Note over CTRL: ghi metric payment_webhook_rejected_total (reason) · KHÔNG chạm DB
    else «chữ ký ĐÚNG»
        CTRL->>APP: tra giao dịch theo MerchantReference + đối chiếu Amount
        APP->>DB: đọc PayTransactions (lọc tenant tự động)
        APP->>DOM: MarkSucceeded(vnp_TransactionNo, vnp_TransactionNo, rawPayload, paidAt)
        Note over DOM: guard trạng thái cuối — nếu đã Succeeded/Refunded thì chỉ tăng bộ đếm
        DOM-->>APP: true (ghi mới) hoặc false (đã xử lý)
        APP->>DB: UPDATE + SaveChanges (cùng Unit of Work)
        APP->>DB: phát PaymentSucceededEto (in-process, cùng UoW) → cầu nối ghi đơn
        CTRL-->>VNP: 200 + RspCode 00 Confirm Success
    end
```

### 4.2 Sequence — hoàn tiền (**code THẬT đang chạy**)

```mermaid
sequenceDiagram
    actor KT as Ke toan / CSKH
    participant WEB as Web hoac API client
    participant APP as PaymentAppService
    participant LOCK as IAbpDistributedLock
    participant DOM as PaymentTransaction
    participant PRV as VnPayPaymentProvider
    participant VNP as VNPay (API giao dich)
    participant DB as PostgreSQL

    KT->>WEB: yeu cau hoan tien (soTien, lyDo, nguoiThucHien)
    WEB->>APP: POST hoan-tien/{id}
    Note over APP: Authorize cap class + permission Payments.Refund
    APP->>LOCK: TryAcquireAsync payments:refund:{id} (15 s)
    alt «khong gianh duoc khoa»
        LOCK-->>APP: null
        APP-->>WEB: BusinessException eCommerce:Lock:0001
    else «co khoa»
        APP->>DB: FindAsync(id) (null thi nem 0005)
        APP->>DOM: kiem ConHoanDuoc · DungDuocDeHoan · soTien > 0
        APP->>PRV: RefundAsync(merchantReference, soTien, lyDo, providerTransactionId)
        PRV->>VNP: POST TransactionApiUrl (JSON da ky HMAC-SHA512)
        alt «timeout hoac khong ket noi duoc»
            VNP--xPRV: khong phan hoi
            PRV-->>APP: RefundResult LoiHaTang + ChoXacNhan = true
            APP-->>WEB: DaHoanThat false · ChoXacNhan true · KHONG ghi so
            Note over APP: log Error [PAY-KHONG-XAC-DINH] · CAM retry mu
        else «cong da nhan (ma 00) nhung tien CHUA ra»
            PRV-->>APP: DaNhanYeuCau true · DaHoanThat false · ChoXacNhan true
            APP-->>WEB: KHONG ghi so · log Warning [PAY-CHO-XAC-NHAN]
        else «cong tu choi (ma khac 00)»
            PRV-->>APP: LyDoThatBai · ChoXacNhan false
            APP-->>WEB: KHONG ghi so · log Error [PAY-TU-CHOI-HOAN]
        else «tien DA ra (DaHoanThat true)»
            PRV-->>APP: DaHoanThat true
            APP->>DOM: RegisterRefund(soTien) (nem neu vuot)
            APP->>DB: UpdateAsync + SaveChanges
            APP-->>WEB: DaHoanThat true · DaHoanTong · ConHoanDuoc · TrangThaiSau
            Note over APP: log Warning [PAY-DA-HOAN]
        end
    end
```

### 4.3 DFD — đường đi của dữ liệu

```mermaid
graph LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef process fill:#cce5ff,stroke:#004085,stroke-width:2px;
    classDef datastore fill:#e2e3e5,stroke:#383d41,stroke-width:2px;
    classDef miss fill:#fff3cd,stroke:#856404,stroke-width:2px;

    KH["Khach hang"]
    VNP["VNPay"]
    DB[("<b>PostgreSQL</b><br/>PayTransactions")]
    REDIS[("<b>Redis</b><br/>khoa phan tan")]
    METRIC["<b>Meter</b><br/>payment_webhook_rejected_total<br/>payment_recon_changed_total<br/>payment_recon_unresolved_total"]

    class KH,VNP actor;
    class DB,REDIS datastore;
    class METRIC datastore;

    subgraph SVC["PaymentHub"]
        P1["1. <b>Xac thuc</b><br/>HMAC-SHA512 + 2 ma 00"]
        P2["2. <b>Quyet dinh</b><br/>LuatDoiSoatThanhToan.Quyet"]
        P3["3. <b>Ghi so</b><br/>MarkSucceeded · MarkFailed · RegisterRefund"]
        P0["0. <b>Channel / Order</b><br/>«tao giao dich — CHUA CO»"]
        class P0 miss;
        class P1,P2,P3 process;
        P0 --> P1 --> P2 --> P3
    end

    KH -- "«chon cong thanh toan»" --> P0
    P0 -. "«tao dong Initiated»" .-> DB
    VNP -- "«IPN / webhook»" --> P1
    P3 -. "«doc + ghi cung transaction»" .-> DB
    P2 -. "«khoa theo giao dich»" .-> REDIS
    P1 -. "«dem webhook bi tu choi»" .-> METRIC
    P2 -. "«dem thay doi / khong xac dinh»" .-> METRIC
    P3 -- "«ket qua + 3 trang thai»" --> KH
```

> 📌 **Đọc DFD cho đúng:** `P0` là ô **duy nhất** tô vàng — **chưa có code**. Trong code hiện tại, dòng
> `PayTransactions` **chỉ** được tạo bởi **test** (`new PaymentTransaction(...)`) hoặc bởi SQL tay ⇒ đường vào
> dữ liệu thật **đang thiếu** (§0, §7.5). Đây là lý do tài liệu này **không** được đánh dấu `Implemented`.

---

## 5. Mô hình dữ liệu

### 5.1 ERD

```mermaid
erDiagram
    OrdOrders {
        uuid Id PK
        uuid TenantId "tenant"
        string Number "ma don"
        int PaymentStatus "truc tai chinh - CHUA duoc ghi boi PaymentHub"
    }
    PayTransactions {
        uuid Id PK
        uuid TenantId "tenant"
        string ProviderCode "vnpay"
        uuid OrderId FK "nullable"
        string MerchantReference "vnp_TxnRef - ON DINH"
        string ProviderTransactionId "vnp_TransactionNo"
        decimal Amount "numeric 18,4 - KHONG double"
        string Currency "VND"
        int Status "enum 7 gia tri"
        datetime PaidAtUtc "gio cong xac nhan"
        decimal RefundedAmount "numeric 18,4"
        string LastWebhookPayload "text - DA CHE"
        string FailureReason "toi da 2000"
        int WebhookReceivedCount "dem IPN"
        string IdempotencyKey "vnp_TransactionNo - unique filter"
    }
    OrdOrders ||--o{ PayTransactions : "co the co nhieu lan thu"
```

> ⚠️ **Quan hệ là LOGIC, không phải FK vật lý.** Migration **không** tạo foreign key cho `OrderId` — chỉ có index
> `(OrderId)` (`20261009070214_Initial.cs:404-407`). Lý do: giao dịch có thể tồn tại **trước** khi đơn được ghi
> (ví dụ nạp ví — `PaymentTransaction.cs:41-42` ghi rõ *"có thể null nếu là giao dịch nạp ví riêng"*).

### 5.2 Bảng & cột (Data Dictionary) — **bảng THẬT `PayTransactions`**

**Bảng `PayTransactions`** — *một lần thử thu tiền với một cổng; là **nguồn sự thật** về việc khách đã trả hay chưa.*

| Cột | Kiểu (DB thật) | Null? | Nguồn / ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính, `PaymentTransaction(Guid id, …)` | |
| `TenantId` | `uuid` | ✔ | tenant sở hữu (`IMultiTenant`, `PaymentTransaction.cs:34-36`) | ⚠️ **NULL được phép** ⇒ unique index **bắt buộc** `NULLS NOT DISTINCT` (§5.4) |
| `ProviderCode` | `character varying(32)` | ✖ | `"vnpay"` (`VnPayPaymentProvider.cs:90`) | mapping: `HasMaxLength(32)` |
| `OrderId` | `uuid` | ✔ | đơn liên quan — **không FK**, chỉ index | có thể `NULL` (nạp ví) |
| `MerchantReference` | `character varying(64)` | ✖ | `vnp_TxnRef` — mã **của ta**, **ổn định** qua retry | 🔒 unique theo `(TenantId, ProviderCode)`; VNPay yêu cầu **không trùng trong ngày** |
| `ProviderTransactionId` | `character varying(128)` | ✔ | `vnp_TransactionNo` — mã **cổng** cấp | 🚨 **BẮT BUỘC** để hoàn tiền tra **tất định** (`IPaymentProvider.cs:83-98`) |
| `Amount` | `numeric(18,4)` | ✖ | số tiền thu (VND) | ⚠️ **`decimal`, không bao giờ `double`**; VNPay nhận **×100** |
| `Currency` | `character varying(8)` | ✖ | mặc định `"VND"` (`PaymentTransaction.cs:56`) | |
| `Status` | `integer` | ✖ | enum 7 giá trị (0…6) | lưu **số**, không lưu chuỗi ⇒ đổi thứ tự enum là **đổi nghĩa dữ liệu cũ** |
| `PaidAtUtc` | `timestamp with time zone` | ✔ | thời điểm **cổng** xác nhận (không phải lúc ta nhận webhook) | `PaymentTransaction.cs:60-61` |
| `RefundedAmount` | `numeric(18,4)` | ✖ | **tổng** đã hoàn | 🚨 chỉ có **tổng**, không có **dòng** hoàn tiền (§5.6) |
| `LastWebhookPayload` | `text` | ✔ | payload IPN **cuối cùng**, đã **che** + cắt **8000** ký tự | `PaymentTransaction.cs:103,122`; `XC-005` **BR-005** |
| `FailureReason` | `character varying(2000)` | ✔ | lý do thất bại (cắt 2000) | `PaymentTransaction.cs:138` |
| `WebhookReceivedCount` | `integer` | ✖ | **số lần** đã nhận webhook (kể cả trùng) | 🔑 đây là **bằng chứng** IPN gửi trùng — không có nó thì *"trùng"* và *"mất"* **không phân biệt được** |
| `IdempotencyKey` | `character varying(256)` | ✔ | mã giao dịch cổng cấp (dùng cho Casso/SePay là id giao dịch ngân hàng) | unique **có filter** (§5.3) |
| `ExtraProperties` | `text` | ✖ | túi `ExtraProperties` của ABP | |
| `ConcurrencyStamp` | `character varying(40)` | ✖ | ABP dùng cho **optimistic concurrency** | 🔑 hai luồng cùng `UpdateAsync` ⇒ một luồng nhận `AbpDbConcurrencyException` |
| `CreationTime` / `CreatorId` | `timestamptz` / `uuid` | ✖ / ✔ | `FullAuditedAggregateRoot` | dùng để sắp xếp danh sách (`PaymentAppService.cs:95`) — ⚠️ **không có index** |
| `LastModificationTime` / `LastModifierId` | `timestamptz` / `uuid` | ✔ | audit | |
| `IsDeleted` / `DeleterId` / `DeletionTime` | `boolean` / `uuid` / `timestamptz` | ✖ / ✔ / ✔ | **xoá mềm** của ABP | 🔒 **cấm** xoá mềm giao dịch đã có tiền (§8, bất biến 6) |

*Nguồn cột: `src/eCommerce.EntityFrameworkCore/Migrations/20261009070214_Initial.cs:223-255`; mapping:
`modules/PaymentHub/eCommerce.PaymentHub.EntityFrameworkCore/PaymentHubDbContextModelCreatingExtensions.cs:19-28`.*

### 5.3 Index & ràng buộc — **3 index THẬT**

| # | Loại | Tên index (thật trong DB) | Cột | Mục đích | **Nếu THIẾU thì điều gì xảy ra** |
|---|---|---|---|---|---|
| 1 | **UNIQUE** (`NULLS NOT DISTINCT`) | `IX_PayTransactions_TenantId_ProviderCode_MerchantReference` | `(TenantId, ProviderCode, MerchantReference)` | Mã giao dịch phía ta **không trùng** trong cùng tenant + cổng (`PaymentHubDbContextModelCreatingExtensions.cs:30-32`) | Hai giao dịch cùng `vnp_TxnRef` ⇒ **VNPay không biết trả tiền cho lần nào**; IPN về chỉ khớp **một** dòng (dòng đầu) ⇒ giao dịch kia **vĩnh viễn `Initiated`** trong khi tiền **đã vào**. ⚠️ Nếu thiếu `NULLS NOT DISTINCT`: ở chế độ `TenantId = NULL` (hiện nay) trùng **lọt im lặng** — đúng lỗi đã chứng minh bằng `INSERT 0 2` (`20261013024226_…cs:14-17`) |
| 2 | **UNIQUE có filter** (`NULLS NOT DISTINCT`) | `IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey` | `(TenantId, ProviderCode, IdempotencyKey) WHERE "IdempotencyKey" IS NOT NULL` | **Idempotency** của webhook: cùng một giao dịch của cổng **không** ghi hai lần (`…Extensions.cs:34-37`) | IPN gửi trùng / retry ⇒ **hai dòng** cho **một** lần thu tiền ⇒ `RefundedAmount` có thể bị trừ **hai lần** trên hai dòng, đối soát **không** khớp sổ cổng. 🔒 Filter `IS NOT NULL` là **bắt buộc**: trước khi có IPN, mọi dòng đều `NULL` — bỏ filter thì **toàn bộ** giao dịch mới sẽ **đụng nhau** ở giá trị `NULL` |
| 3 | **INDEX** (không unique) | `IX_PayTransactions_OrderId` | `(OrderId)` | Tra **tất cả** lần thử thanh toán của một đơn (đơn bị huỷ rồi đặt lại ⇒ nhiều dòng) | Truy vấn *"đơn này đã trả chưa"* thành **tuần tự toàn bảng**; ở 10 000 dòng/tenant vẫn chậm, và tệ hơn: `OrderId` **nullable** ⇒ cần index để không quét cả bảng khi `OrderId IS NULL` |

**Ràng buộc KHÔNG có (phải ghi rõ, không được giả định là có):**

| Thiếu gì | Hệ quả | Có phải nợ? |
|---|---|---|
| ~~**Không** `CHECK (RefundedAmount >= 0)`~~ ✅ **ĐÃ THÊM `S5`** — `CK_PayTransactions_RefundedAmount_KhongAm` | Ghi tay/nhánh lỗi có thể tạo số âm; entity đã chặn (`amount > 0` + cộng dồn) nhưng **DB không** chặn | ✅ Đã đóng (migration `S5_PayRefunds_VaRangBuocHoanTien`) |
| ~~**Không** `CHECK (RefundedAmount <= Amount)`~~ ✅ **ĐÃ THÊM `S5`** — `CK_PayTransactions_RefundedAmount_KhongVuotAmount` | Tầng domain **đã** chặn (`PaymentTransaction.RegisterRefund`) nhưng **đường ghi tắt** (SQL/sửa nóng) không bị chặn | ✅ Đã đóng — chứng minh `23514` trên PostgreSQL thật (`TC-45`) |
| **Không** FK `OrderId → OrdOrders` | Có thể trỏ tới đơn không tồn tại | 🟡 Cố ý (nạp ví); **phải** ghi trong tài liệu, đã ghi |
| **Không** index trên `(TenantId, Status, CreationTime)` | Truy vấn của job (`Status IN (…)`) và của UI (`ORDER BY CreationTime DESC LIMIT 50`) đều **quét bảng** | 🟠 **Nợ hiệu năng thật** — xem NFR-PAY-001 |
| ~~**Không** bảng dòng hoàn tiền~~ ✅ **ĐÃ CÓ `S5`** — `PayRefunds` (§5.7) | Không trả lời được *"hoàn lúc nào, vì sao, ai duyệt"* bằng **dữ liệu** (chỉ có log) | ✅ Đã đóng |

### 5.4 ⚠️ `NULLS NOT DISTINCT` — ràng buộc dự án, và **con số 52**

**Luật (bất di bất dịch):** *mọi unique index trên entity `IMultiTenant` **phải** có `NULLS NOT DISTINCT`, viết
bằng **SQL thô** trong migration* (`handover:120-124`; `XC-004` **F1**; `XC-003` §5.4).

| Mục | Giá trị **đo được** | Nguồn |
|---|---|---|
| Hiện trạng 2 index của `PayTransactions` | **Đã** có `NULLS NOT DISTINCT` cho **cả hai** index unique | `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:61-68` |
| Tổng số unique index có `NULLS NOT DISTINCT` (đo 18/10/2026, cuối `S4`) | **51** | `PostgreSqlSchemaTests.cs` — `F1_du_51_index…` (`Assert.Equal(51L, …)`); 48 → 49 (`S1`) → 51 (`S2`) |
| 🆕 **`S5` làm con số đó đổi thành bao nhiêu?** | **52** — thêm **đúng MỘT**: `IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId` (`0` → `1`, không thay) | `S5_PayRefunds_VaRangBuocHoanTien` — SQL thô `NULLS NOT DISTINCT`; test đổi tên `F1_du_52_index…` + thêm tên index vào **danh sách đích danh** |
| `S5` có sửa index nào khác không? | **KHÔNG.** Hai `CHECK` mới của `PayTransactions` **không** phải index | — |

> 🚨 **Vì sao phải nói rõ "con số đổi thành 52":** test `F1_du_52_index_deu_dung_NULLS_NOT_DISTINCT` khẳng định **cả
> tổng số** lẫn **danh sách đích danh** (2 index của `PayTransactions` nằm trong danh sách — `PostgreSqlSchemaTests.cs:164-165`).
> Nếu ai đó thêm index mà quên `NULLS NOT DISTINCT` **đồng thời** vô tình làm mất thuộc tính của index khác thì
> **tổng số không đổi** ⇒ test **vẫn xanh** trong khi một index đã mất tác dụng. Đó là lý do danh sách đích danh tồn tại
> (`PostgreSqlSchemaTests.cs:172-175`). 📌 `S5` **thêm tên index mới vào danh sách đó**, không chỉ tăng tổng.


**SQL thô thật (trích nguyên văn):**

```sql
DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference";
CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_MerchantReference"
    ON "PayTransactions" ("TenantId", "ProviderCode", "MerchantReference") NULLS NOT DISTINCT;

DROP INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey";
CREATE UNIQUE INDEX "IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey"
    ON "PayTransactions" ("TenantId", "ProviderCode", "IdempotencyKey") NULLS NOT DISTINCT
    WHERE ("IdempotencyKey" IS NOT NULL);
```

> ⚠️ **Rủi ro đã ghi nhận:** EF **không** mô hình hoá `NULLS NOT DISTINCT`. Nếu một migration **do EF sinh tự
> động** sau này tạo lại một trong hai index (ví dụ vì đổi `HasMaxLength`), thuộc tính **mất** ⇒ migration đó
> **phải** chạy lại SQL này (`20261013024226_…cs:23-25`).

### 5.5 Mapping EF Core → DB: các điểm **dễ lệch**

| Điểm | Code | DB | Cảnh báo |
|---|---|---|---|
| Tên bảng | `ToTable("PayTransactions")` | `PayTransactions` | ✅ khớp — **không** phải `PaymentTransactions` (dù `DbSet` tên là `PaymentTransactions`, `eCommerceDbContext.cs:154`) |
| `Amount` / `RefundedAmount` | `HasPrecision(18, 4)` | `numeric(18,4)` | 🔒 **cấm** đổi sang `double`; 4 chữ số lẻ để chừa cho làm tròn tỉ giá sau này |
| `Status` | enum → `integer` | `integer` | ⚠️ **thêm giá trị enum phải thêm ở CUỐI**; chèn giữa là **đổi nghĩa** dữ liệu cũ |
| `LastWebhookPayload` | `HasColumnType("text")` | `text` | cắt 8000 **trong code**, không phải ở DB |
| `MerchantReference` | `HasMaxLength(64)` | `varchar(64)` | ⚠️ `vnp_TxnRef` của VNPay cho phép **tới 100** ký tự theo tài liệu (chưa kiểm chứng) — nếu đúng thì 64 là **giới hạn của ta**, phải sinh mã **ngắn** |
| `Currency` | `HasMaxLength(8)` | `varchar(8)` | |
| Ngày giờ | `DateTime` (UTC) | `timestamptz` | 🚨 VNPay dùng **GMT+7** khi ký (`VnPayPaymentProvider.cs:96`); DB lưu **UTC** ⇒ chuyển đổi ở biên, **cấm** lưu giờ GMT+7 vào cột UTC (bài học `handover:175` — lệch 7 tiếng) |

### 5.6 Phần **chưa có trong mô hình dữ liệu** (kế hoạch GĐ1 — không được coi là đã xong)

| # | Thiếu gì | Vì sao cần | Ảnh hưởng nếu để nguyên |
|---|---|---|---|
| 1 | ~~**Bảng dòng hoàn tiền**~~ ✅ **ĐÓNG `S5`** — bảng `PayRefunds` (**§5.7**) | `RefundedAmount` chỉ là **tổng** ⇒ câu hỏi *"hoàn lúc nào, bao nhiêu, vì sao, ai duyệt"* **không trả lời được bằng dữ liệu** | — |
| 2 | Cột `ProviderCreateDateGmt7` | `querydr`/`refund` bắt buộc `vnp_TransactionDate` = thời điểm giao dịch **tại cổng**, GMT+7 (`REC-001` **H2**) | Hiện dùng `DateTime.UtcNow.AddHours(7)` **tại lúc gọi** (`VnPayRequestBuilder.cs:78`) ⇒ gửi **sai ngày** cho giao dịch cũ ⇒ cổng trả *"không tìm thấy giao dịch"* |
| 3 | ~~Liên kết giao dịch ↔ **đơn** ở phía đơn~~ ✅ **ĐÓNG `S4` + `S5`** (`PaymentSucceededEto` từ `S4`; `PaymentRefundedEto` từ `S5` ⇒ đơn sang `PartiallyRefunded`/`Refunded`) | — | — |
| 4 | `PaymentTransaction` **không** lưu mã `vnp_BankCode`/`vnp_CardType` | Đối soát theo ngân hàng/phương thức, báo cáo phí | Không đối chiếu được sao kê theo kênh thanh toán |

---

### 5.7 🆕 Bảng `PayRefunds` (`S5`) — **dòng hoàn tiền**

> 📌 **VÌ SAO BẢNG NÀY TỒN TẠI:** `PaymentTransaction.RefundedAmount` chỉ là **MỘT TỔNG**, nên ba câu hỏi kế
> toán luôn hỏi khi khách khiếu nại — *hoàn **lúc nào**, **bao nhiêu mỗi lần**, **vì sao + ai duyệt*** — **không
> trả lời được bằng dữ liệu**, chỉ bằng **log** (mà log thì bị xoay vòng). 🔒 Với dòng tiền, *"chỉ có log"*
> nghĩa là **không có bằng chứng**.

| Cột | Kiểu | Null? | Ý nghĩa |
|---|---|---|---|
| `Id` | `uuid` | ✖ | PK |
| `TenantId` | `uuid` | ✔ | tenant sở hữu (`IMultiTenant` ⇒ unique index **bắt buộc** `NULLS NOT DISTINCT`) |
| `PaymentTransactionId` | `uuid` | ✖ | giao dịch gốc — 🔒 **tham chiếu MỀM** (không FK), cùng quy ước `PayTransactions.OrderId`: dòng hoàn giữ được bằng chứng **kể cả khi** bản ghi giao dịch bị xoá mềm nhầm |
| `RefundRequestId` | `varchar(64)` | ✖ | **số thứ tự lần hoàn** (`RF001`, `RF002`…) — 🔑 **ổn định, đọc được** (xem bảng quyết định dưới) |
| `Amount` | `numeric(18,4)` | ✖ | số tiền **của lần hoàn này** — 🔒 `decimal`, **cấm** `double` |
| `Reason` | `varchar(2000)` | ✖ | lý do (audit + `vnp_OrderInfo`) |
| `PerformedBy` | `varchar(256)` | ✖ | **ai** yêu cầu hoàn |
| `Status` | `integer` | ✖ | `TrangThaiDongHoanTien`: `ChoXacNhan(0)` · `DaHoan(1)` · `TuChoi(2)` · `KhongXacDinh(3)` |
| `ProviderResponseCode` | `varchar(32)` | ✔ | `vnp_ResponseCode` — giữ **nguyên văn** để điều tra |
| `ProviderRefundTransactionId` | `varchar(128)` | ✔ | mã giao dịch hoàn do cổng cấp — tra chéo sao kê |
| `FailureReason` | `varchar(2000)` | ✔ | lý do thất bại / **lỗi hạ tầng** |
| `ExtraProperties` · `ConcurrencyStamp` · `CreationTime` · … | | | chuẩn ABP (`FullAuditedAggregateRoot`) |

**Ba quyết định — mỗi cái đều có lý do, không phải sở thích:**

| # | Quyết định | Vì sao **không** làm cách khác |
|---|---|---|
| 1 | 🚨 **Ghi dòng cho MỌI kết quả** — kể cả `ChoXacNhan`/`TuChoi`/`KhongXacDinh` | Ba câu hỏi của kế toán phải trả lời được **cả** khi lần hoàn **không** thành — đặc biệt ca `KhongXacDinh`, nơi **cấm** thử lại mù: không có dòng ghi thì lần sau **không ai biết** đã từng gửi yêu cầu, và người vận hành **sẽ gửi lại** (⇒ hoàn hai lần) |
| 2 | 🔒 **Dòng `DaHoan` KHÔNG tự làm `RefundedAmount` tăng** | `RefundedAmount` **chỉ** tăng khi `LuatGhiNhanHoanTien.PhaiGhiSo` = `true` **và** `RegisterRefund` đã chạy. 📌 Gộp hai thứ đó là đúng lỗi §4.2 đã phải sửa: ghi sổ theo mã `00` ⇒ *"sổ sách nói dối"* |
| 3 | 🚨 **`RefundRequestId` là SỐ THỨ TỰ, KHÔNG phải `Guid` ngẫu nhiên** | Unique index `(TenantId, PaymentTransactionId, RefundRequestId)` chỉ có giá trị khi nó **chặn được**: hai luồng cùng tính ra *"lần hoàn thứ 2"* ⇒ dòng thứ hai nhận `23505` thay vì **âm thầm** thành hai dòng cùng số thứ tự. ⚠️ `Guid` mỗi lần một giá trị ⇒ index thành **trang trí** và **không lỗi nào nổi lên** (bài học `BR-PAY-002`) |

**Bất biến ở tầng DB — đã CHỨNG MINH trên PostgreSQL thật** (`test/eCommerce.IntegrationTests/PayRefundsVaRangBuocTienTests.cs`):

| Ràng buộc | Nội dung | Nếu thiếu |
|---|---|---|
| `IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId` | UNIQUE **`NULLS NOT DISTINCT`** (SQL thô) | ở `TenantId = NULL` hai dòng cùng lần hoàn **lọt im lặng** ⇒ `RefundedAmount` cộng hai lần cho **một** lệnh |
| `CK_PayRefunds_Amount_Duong` | `"Amount" > 0` | dòng hoàn 0 đồng (vô nghĩa) hoặc **âm** (làm **giảm** tổng đã hoàn ⇒ **che** một lần hoàn đã xảy ra) |
| `CK_PayTransactions_RefundedAmount_KhongAm` | `"RefundedAmount" >= 0` | như trên, ở bảng tổng |
| `CK_PayTransactions_RefundedAmount_KhongVuotAmount` | `"RefundedAmount" <= "Amount"` | 🚨 **bất biến TIỀN**: hoàn nhiều hơn nhận — tầng domain đã chặn, nhưng **đường ghi tắt** thì không |


## 6. Hợp đồng API

### 6.1 Application Service

| Mục | Giá trị |
|---|---|
| Interface | `IPaymentAppService` — `modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/PaymentDtos.cs:110-158` |
| Implementation | `PaymentAppService` — `modules/PaymentHub/eCommerce.PaymentHub.Application/PaymentAppService.cs:34` |
| `[Authorize]` | **cấp CLASS**: `[Authorize(eCommerceConsts.Permissions.Payments.Default)]` (`PaymentAppService.cs:33`) |
| Unit of Work | ABP tự mở (`ApplicationService`); mỗi method là **một** UoW; `UpdateAsync(gd, autoSave: true)` |
| Khoá phân tán | `IAbpDistributedLock` inject qua constructor (`PaymentAppService.cs:38,50`) |

| Method | Input (DTO) | Output (DTO) | Permission thêm | Ghi chú |
|---|---|---|---|---|
| `GetTheoMaAsync(string merchantReference)` | `merchantReference` (query) | `PaymentTransactionDto?` | — (kế thừa `Default`) | Trả **`null`** khi không thấy — **không phải lỗi** (§12) |
| `GetDanhSachAsync(string? merchantReference, string? trangThai, int toiDa = 50)` | 3 tham số query | `List<PaymentTransactionDto>` | — | `toiDa` bị `Clamp(1, 200)` (`PaymentAppService.cs:79`); **không** trả tổng số dòng |
| `HoanTienAsync(Guid paymentTransactionId, HoanTienInput input)` | `paymentTransactionId` (route) + `HoanTienInput` | `HoanTienKetQuaDto` | ✅ `eCommerce.Payments.Refund` (`PaymentAppService.cs:103`) | 4 bất biến cưỡng chế (§8) |
| `PostTruyVanTrangThaiAsync(Guid paymentTransactionId)` | `paymentTransactionId` (route) | `PaymentTransactionDto` | ✅ `eCommerce.Payments.Refund` (`PaymentAppService.cs:254`) | Gọi cổng `querydr`; 🔒 **không** hạ trạng thái |
| `PostDoiSoatLoatAsync(int toiDa = 50)` | `toiDa` (query) | `KetQuaDoiSoatThanhToanDto` | — ⚠️ **chỉ** kế thừa `Default` | `toiDa` bị `Clamp(1, 100)`; 🚨 **gọi cổng cho tối đa 100 giao dịch** mà chỉ cần quyền **đọc** ⇒ xem §9 **F-PAY-3** |
| 🆕 `TaoGiaoDichAsync(TaoGiaoDichInput input)` | `SoDon`, `SoTien`, `TienTe`, `ReturnUrl`, `IpnUrl`, `OrderId?`, `ClientIp?`, `NoiDung?` | `TaoGiaoDichKetQuaDto` | — (kế thừa `Default`) | **`S4`** — tạo giao dịch **idempotent theo đơn** (dùng lại giao dịch đang mở ⇒ **cùng** `MerchantReference`); khoá `payment:create:{…}`; mã sinh bằng `SinhMaThamChieu` (§7.5). ⚠️ Gọi được bằng quyền **đọc** ⇒ xem §16.3 #6 |

### 6.2 REST endpoint

> 🔒 **Quy ước:** ABP sinh controller từ `IPaymentAppService` (auto API controller). Các route dưới đây **đã được
> đo bằng swagger/`tools\route-check.ps1`** ở `SAL-006` §17.2 #10 và §20 (`sal-006-tra-hang-rma-hoan-tien.md:1140,1317`).

| Method | Route (đo được) | Permission | Ghi chú |
|---|---|---|---|
| `GET` | `/api/app/payment/theo-ma` | `eCommerce.Payments` | query `merchantReference` |
| `GET` | `/api/app/payment/danh-sach` | `eCommerce.Payments` | query `merchantReference`, `trangThai`, `toiDa` |
| `POST` | `/api/app/payment/hoan-tien/{paymentTransactionId}` | `eCommerce.Payments.Refund` | body `HoanTienInput` |
| `POST` | `/api/app/payment/truy-van-trang-thai/{paymentTransactionId}` | `eCommerce.Payments.Refund` | body rỗng |
| `POST` | `/api/app/payment/doi-soat-loat` | `eCommerce.Payments` | query `toiDa` |
| 🆕 `POST` | `/api/app/payment/tao-giao-dich` | `eCommerce.Payments` | **`S4`** — body `TaoGiaoDichInput`; trả `CheckoutUrl` + `laGiaoDichDaCo`. ✅ **`S5` ĐÃ ĐO HTTP THẬT = `200`** (probe `T15`… — xem §16.2) |
| 🆕 `GET` | **`/api/webhooks/vnpay`** | **KHÔNG** (allowlist) | ✅ **`S5` — ĐÃ CÓ VÀ ĐÃ ĐO**: `VnPayIpnController` (`src/eCommerce.HttpApi.Host/Controllers`), `[AllowAnonymous]`, đã duyệt trong `AllowlistCongKhaiTests`. **Đo được** (`probe-thanh-toan.ps1`): chữ ký đúng ⇒ **`200`** + `{"RspCode":"00"}`; **sai chữ ký ⇒ `400`** + `{"RspCode":"97"}` và **DB không đổi**; lệch số tiền ⇒ **`400`** + `{"RspCode":"99","Message":"eCommerce:Payment:0008"}` |
| ⚠️ | *(không có route nào cho `IXuLyKetQuaCong`)* | — | 🔒 **CỐ Ý**: đường ghi nhận kết quả cổng **không phải** `IApplicationService` ⇒ ABP **không** sinh controller ⇒ **không** có endpoint *"đánh dấu đã trả tiền"* (`D3`/§7.7.4 #1). Có test khoá (`OrderPaymentStatusHandlerBatBienTests`) |

**Authentication:** `Bearer` (OpenIddict) cho `/api/app/*`; IPN **không** dùng Bearer (VNPay gọi trực tiếp) ⇒
🔒 phải là endpoint **allowlist công khai** theo `XC-004` **BR-004** + `AllowlistCongKhaiTests` (`CAT-003` đã làm mẫu).

**Request mẫu — hoàn tiền**

```json
{
  "soTien": 150000.00,
  "lyDo": "Khach tra hang theo RMA-2026-0042 - hang loi nha san xuat",
  "nguoiThucHien": "ketoan.01@congty.vn"
}
```

**Response mẫu — cổng đã nhận nhưng tiền CHƯA ra (ca **bình thường** của VNPay)**

```json
{
  "paymentTransactionId": "3f1a8c1e-6c2b-4b7f-9a6e-2c9d1f0b7a11",
  "merchantReference": "PAY2026101709301200421",
  "daNhanYeuCau": true,
  "daHoanThat": false,
  "choXacNhan": true,
  "conHoanDuoc": 199000.0000,
  "daHoanTong": 0.0000,
  "maPhanHoi": "00",
  "thongDiep": "Cong da nhan yeu cau hoan tien - CHUA xac nhan tien da ra (phai doi soat).",
  "maGiaoDichHoan": "14991234",
  "lyDoThatBai": null,
  "loiHaTang": null,
  "trangThaiSau": "Succeeded"
}
```

> 🚨 **Đọc response cho đúng:** `daNhanYeuCau = true` **KHÔNG** phải thành công. `daHoanThat = false` +
> `choXacNhan = true` nghĩa là **phải** gọi `/truy-van-trang-thai/{id}` để xác nhận **trước khi** báo khách
> *"đã hoàn"*. Đây là lý do DTO **không** có trường `success` trần (`PaymentDtos.cs:55-65`).

**Mã lỗi nghiệp vụ**

| Mã | HTTP (dự kiến) | Khi nào | Thông điệp / hành động |
|---|---|---|---|
| `eCommerce:Payment:0002` (`HoanVuotSoDaThu`) | `400`/`403` — **chưa kiểm chứng** | `RefundedAmount + amount > Amount` | *"Đối chiếu với cổng trước khi làm gì tiếp; **KHÔNG** thử lại"* (`PaymentErrorCodes.cs:22-25`) |
| `eCommerce:Payment:0003` (`ChuaThuDuocTienKhongHoanDuoc`) | `400`/`403` — chưa kiểm chứng | `Status ∈ {Initiated, Pending, Failed, Cancelled}` | *"Kiểm **TRẠNG THÁI** giao dịch, không phải số tiền"* (`…cs:27-30`) |
| `eCommerce:Payment:0004` (`SoTienHoanKhongHopLe`) | `400` | `amount ≤ 0`, thiếu `lyDo`, thiếu `NguoiThucHien` | *"Sửa tham số rồi gửi lại"* (`…cs:32-36`) |
| `eCommerce:Payment:0005` (`KhongTimThayGiaoDich`) | `404`-nghĩa | `FindAsync` trả `null` | *"Kiểm lại Id / giao dịch đã bị xoá"* (`…cs:38-42`) |
| `eCommerce:Lock:0001` | `409`-nghĩa — chưa kiểm chứng | Không giành được khoá trong **15 s** | *"Có thao tác khác đang chạy trên cùng giao dịch"* (`PaymentAppService.cs:125-128`, `eCommerceConsts.cs:316`) |
| `eCommerce:Db:0001` (mã **chung**) | `500`-nghĩa | Vi phạm `IX_PayTransactions_*` | ⚠️ **Chưa có ánh xạ riêng** cho 2 index của thanh toán trong `ViPhamRangBuocDb.cs` (grep `PayTransactions` = **0** kết quả) ⇒ rơi về mã chung — xem §12 **F-PAY-4** |

> ⚠️ **Cột HTTP là "dự kiến", không phải "đã đo".** Lý do: repo **không** cấu hình `AbpExceptionHttpStatusCodeOptions`
> (grep = 0 kết quả) ⇒ mã HTTP thật do mặc định của ABP quyết định; **trên MVC** đã đo được là **`500`**
> (`handover:146-147`; `DoiSoatThanhToanController.cs:61-65` **bắt** `BusinessException` để tránh `500`).
> 📌 Việc đo mã HTTP thật trên `/api/app/payment/*` là **nợ kiểm chứng** — ghi ở §15.3.

---

## 7. Sự kiện, tích hợp ngoài & idempotency — **BẮT BUỘC**

### 7.1 Sự kiện phát ra (ETO)

✅ **`S4` — HAI ETO ĐẦU TIÊN ĐÃ CÓ TRONG CODE** (18/10/2026): `PaymentSucceededEto` · `PaymentFailedEto`
(`modules/PaymentHub/eCommerce.PaymentHub.Application.Contracts/Events/PaymentEvents.cs`), phát qua
`PaymentEventPublisher` (một chỗ duy nhất), nhận bởi `OrderPaymentStatusHandler`. 🚨 **NHƯNG CHƯA QUA OUTBOX** —
bằng chứng: grep `IHasEventOutbox|ConfigureEventOutbox` trong `src/` = **0**; gói
`Volo.Abp.EventBus.EntityFrameworkCore` (thứ chứa mapping bảng `AbpEventOutbox`) **không có** trong cache NuGet
của máy này ⇒ **không thể** cài outbox trong `S4` mà không đổi cách phân phối **mọi** sự kiện của hệ thống
(quyết định của chủ dự án: **hoãn**, xem §16.4 #1).

| ETO | Khi nào phát | Trường dữ liệu | Ai tiêu thụ | Handler có idempotent? | Có code? |
|---|---|---|---|---|---|
| `PaymentSucceededEto` | Trong **cùng UoW** với `MarkSucceeded` trả `true` | `PaymentTransactionId`, `AggregateId`, `OrderId?`, `TenantId`, `ProviderCode`, `MerchantReference`, `ProviderTransactionId`, `Amount` (**decimal**), `Currency`, `PaidAtUtc`, `EventId`, `OccurredOn` | `Sales` (ghi `Order.PaymentStatus = Paid`) | ✅ theo `(TenantId, ProviderTransactionId)` — ⚠️ chống trùng **thật** hiện dựa vào `MarkSucceeded` trả `false` (nên ETO **không** phát lần hai) | ✅ **`S4`** |
| `PaymentFailedEto` | Khi `MarkFailed` trả `true` (**thật sự** đổi trạng thái) | như trên + `FailureReason`, `ProviderResponseCode` | `Sales` (`PaymentStatus = Failed`), `Notifications` | ✅ như trên | ✅ **`S4`** |
| `PaymentRefundedEto` | Khi `RegisterRefund` thành công (**chỉ** khi tiền **đã ra**) | `PaymentTransactionId`, `PaymentRefundId` (**khoá chống trùng**), `OrderId?`, `TenantId`, `ProviderCode`, `MerchantReference`, `ProviderTransactionId`, `Amount` (**tổng đã thu**), `RefundedAmount` (**tổng đã hoàn sau lần này**), `SoTienLanNay`, `Currency`, `RefundReason`, `NguoiThucHien`, `RefundRequestId`, `EventId`, `OccurredOn` | `Sales` (`PartiallyRefunded`/`Refunded` — luật thuần `PhanLoaiHoan`), `EInvoice`, `Reporting` | ✅ theo `PaymentRefundId` (Guid duy nhất) | ✅ **`S5`** — `PaymentEventPublisher.PhatDaHoanTienAsync`; handler `OrderPaymentStatusHandler.HandleEventAsync(PaymentRefundedEto)` |

> ⚠️ **Phụ thuộc chưa có:** ETO chỉ **không mất** nếu đi qua outbox của ABP (`XC-002` D1). Hiện transport là
> in-process (`LocalDistributedEventBus`, chưa có gói RabbitMQ trong `Directory.Packages.props`) ⇒ `MessageId` **đổi
> mỗi lần phát** ⇒ chống trùng ở tầng transport **vô hiệu**; phải dựa vào **khoá nghiệp vụ ổn định**
> (`XC-002` D3, ghi lại ở `REC-001` §7.1).
> 🔑 **Điều `S4` bảo đảm được (và có test):** phát và xử lý nằm **cùng một transaction DB** ⇒ *"ghi giao dịch mà
> không ghi đơn"* **không xảy ra**; 🔒 **điều CHƯA bảo đảm:** tiến trình chết **sau** commit ⇒ sự kiện mất ⇒ đơn
> `Unpaid` (nợ §16.4 #1). 📌 Đừng đọc phần này thành *"đã có outbox"*.


### 7.2 Sự kiện / webhook nhận vào — **IPN VNPay** (trọng tâm của tài liệu)

| Nguồn | Loại | Xác thực | Chống trùng bằng | Nếu xử lý lỗi? | Thứ tự đến |
|---|---|---|---|---|---|
| **VNPay** | `IPN` (GET, query string `vnp_*`) | **HMAC-SHA512** trên chuỗi tham số **sắp xếp `Ordinal`**, **bỏ** `vnp_SecureHash` + `vnp_SecureHashType` **và bỏ tham số RỖNG**, dựng từ **query THÔ** (`VnPayRequestBuilder.KiemChuKyTuQueryTho` — `S5`) | ① UNIQUE `(TenantId, ProviderCode, IdempotencyKey)` ② `MarkSucceeded` trả `false` khi đã ở trạng thái cuối ③ `WebhookReceivedCount` | 🆕 **`S5`:** chữ ký sai ⇒ **`HTTP 400`** + `RspCode 97` (**không** đổi DB); hợp lệ ⇒ **`200`** + `RspCode 00` ⇒ **VNPay dừng retry** | ⚠️ **CÓ THỂ SAI** — at-least-once, có thể đến **sau** IPN khác hoặc sau khi đã hoàn tiền ⇒ §7.2.3 |

**Nguồn thông số (bắt buộc trích dẫn, không được bịa):**

| Thông số | Giá trị code đang dùng | Nguồn |
|---|---|---|
| Thuật toán ký | `HMAC-SHA512`, hex **chữ thường** | `VnPayRequestBuilder.cs:41-48`; [`docs/architecture.md:124-126`](../../architecture.md) (*"Đặc tả ký/xác thực **đã xác minh** (được khoá bằng test)"*) |
| Chuỗi ký | sắp xếp **tăng dần theo tên khoá** bằng `StringComparer.Ordinal`, nối `&`, dạng `key=value` **đã URL-encode**, **bỏ** `vnp_SecureHash*` | `VnPayPaymentProvider.cs:151-162`, `VnPayRequestBuilder.cs:38-39` |
| Điều kiện thành công | `vnp_ResponseCode = 00` **VÀ** `vnp_TransactionStatus = 00` | `VnPayPaymentProvider.cs:181-184`; `architecture.md:125` |
| Nội dung phản hồi IPN | `{"RspCode":"00","Message":"Confirm Success"}` / sai chữ ký ⇒ `{"RspCode":"97","Message":"Invalid Checksum"}` | `VnPayPaymentProvider.cs:191-194`; `architecture.md:126` |
| `vnp_Amount` | số tiền **× 100** | `VnPayPaymentProvider.cs:175-179` (đọc: chia 100), `VnPayRequestBuilder.cs:135-136` (ghi: nhân 100) |
| `vnp_TxnRef` | **không trùng trong cùng một ngày** | `VnPayPaymentProvider.cs:42` (chú thích *"Đặc tả đã xác minh"*); **tài liệu chính thức chưa được đối chiếu trong phiên này** |
| Yêu cầu HTTPS cho `IpnUrl` | có | `VnPayPaymentProvider.cs:43`; `IPaymentProvider.cs:13` |
| Tài liệu VNPay tham chiếu | — | [VNPay — API truy vấn và hoàn tiền](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr&refund.html) (dùng ở `REC-001` §7.3); tài liệu **IPN/thanh toán** tương ứng **chưa được mở/đối chiếu** trong phiên này ⇒ ghi **chưa kiểm chứng** |

> 🚨 **Ghi rõ mức độ tin cậy:** **4 dòng đầu** của bảng trên là **đặc tả đã được khoá bằng test** — xem
> `test/eCommerce.Tests/PaymentHub/VnPayWebhookTests.cs:66-123` (4 ca IPN) và `VnPayRefundTests.cs:107-190` (ký, sắp xếp,
> URL-encode, bỏ `vnp_SecureHash`). **Các dòng 5–8** (cách nhân 100, quy tắc `vnp_TxnRef` không trùng trong ngày,
> yêu cầu HTTPS, tài liệu tham chiếu) đến từ **chú thích trong code** hoặc từ tài liệu `REC-001` dẫn lại —
> 🚨 **chưa đối chiếu tài liệu chính thức của VNPay trong phiên này** ⇒ ghi **chưa kiểm chứng**.

#### 7.2.1 🚨 Ba phát hiện trong chính đoạn xác thực IPN (đọc dòng, không suy đoán)

> 🆕 **`S5`: `F-PAY-1` ĐÃ ĐÓNG — và khi sửa thì phát hiện thêm MỘT lớp nữa của cùng lỗi.** Xem dòng đầu bảng
> và **§7.2.4**.

| # | Phát hiện | Bằng chứng | Rủi ro |
|---|---|---|---|
| **F-PAY-1** | **`VerifyWebhookAsync` KHÔNG bỏ tham số rỗng**, còn `KiemChuKy` **CÓ bỏ** — hai quy tắc khác nhau trong **cùng một ý niệm** | `VnPayPaymentProvider.cs:151-162` (bản cũ) **so với** `VnPayRequestBuilder.KiemChuKy` | 🔴 **Từ chối IPN THẬT** khi cổng gửi tham số rỗng (ví dụ `vnp_BankCode=`) ⇒ **mất tiền im lặng**. ✅ **ĐÃ SỬA `S5`:** chỉ còn **một** luật — `KiemChuKyTuQueryTho`; `TC-07` (3 ca, **cả hai chiều**) + probe HTTP `T16`/`T18` |
| **F-PAY-2** | ~~**Không có bước đối chiếu số tiền với DB**~~ | ✅ **ĐÓNG `S4`** — `LuatCauNoiThanhToan.DoiChieu` + ép ở **hai** chỗ; `S5` đo thêm **qua HTTP** (`T19`/`T20`: lệch tiền ⇒ `400` + `0008`, DB không đổi) | — |
| **F-PAY-3** | `MerchantReference` có thể **rỗng** mà không phải lỗi | `VnPayPaymentProvider.cs:170` — `GetValueOrDefault("vnp_TxnRef") ?? string.Empty` | ✅ **ĐÓNG `S4`** — `XuLyKetQuaCongService` **NÉM** `0006` khi `vnp_TxnRef` rỗng |

#### 7.2.4 🆕 Lớp thứ hai của `F-PAY-1` — **byte của chuỗi ký** (`S5`)

**Phát hiện khi sửa:** cùng một chỗ, còn **một** cách làm sai nữa mà bản cũ mắc **cùng lúc** — và nó **không** nằm
trong bất kỳ tài liệu nào trước `S5`:

| Điều | Bản cũ (`S4`) | Vì sao **sai** | Nay (`S5`) |
|---|---|---|---|
| Nguồn chuỗi ký | **Giải mã** từng tham số rồi **encode lại** bằng `WebUtility.UrlEncode` | `WebUtility.UrlEncode(' ')` = `%20`, còn `application/x-www-form-urlencoded` (nhiều thư viện HTTP dùng) gửi `+` ⇒ **cùng nội dung, khác byte** ⇒ hash lệch ⇒ **từ chối IPN thật** | Dựng từ **query THÔ** (`Request.QueryString`) — byte **của cổng**, không phải byte ta sinh lại |
| Tham số rỗng | **Giữ** trong chuỗi ký | VNPay **bỏ** (đúng như `KiemChuKy`) ⇒ hash lệch | **Bỏ** (drop) |
| Nơi chứa thuật toán | **Hai bản** (provider + builder) | Hai luật trong một hệ thống ⇒ chúng **đã** lệch | **Một** hàm duy nhất: `VnPayRequestBuilder.KiemChuKyTuQueryTho` |

📌 **Cả ba dòng cùng một họ lỗi:** *"hai định nghĩa của một luật"* — đúng thứ `F-PAY-1` mô tả, nhưng bản mô tả
cũ chỉ thấy **một** trong ba biểu hiện. 🔑 Đây là lý do `S5` **không** chỉ "bỏ tham số rỗng" mà **gộp thuật toán
về một chỗ**: sửa từng biểu hiện thì biểu hiện thứ tư **sẽ** xuất hiện.

**Test khoá lại** (`test/eCommerce.Tests/PaymentHub/VnPayIpnChuKyTests.cs`):

| Ca | Bất biến |
|---|---|
| `TC07` | IPN có `vnp_BankCode=` (rỗng), ký theo luật VNPay ⇒ **hợp lệ** |
| `TC07b` | **Chiều NGƯỢC** — ký theo cách **cũ** (giữ tham số rỗng) ⇒ **bị từ chối** (chống bản sửa "quá tay" làm cổng luôn xanh) |
| `TC07c` | **Bằng chứng phép tiêm đã xảy ra**: hai cách ký cho ra **hash KHÁC nhau** |
| `+`-thay-khoảng-trắng | Query có `Thanh+toan` ⇒ **hợp lệ** (không decode/encode lại) |
| query méo / khoá trùng / thiếu hash | `false` + **lý do có mã** (`query-khong-hop-le` / `thieu-chu-ky`) — không đoán |
| **`HashSecret` RỖNG** | **`false`** + metric `thieu-hashsecret` — xem §7.2.5 |

#### 7.2.5 🆕 Lỗ hổng **chỉ trở thành hiện thực khi có endpoint**: `HashSecret` rỗng (`S5`)

**Đo được:** `appsettings.json` của repo để `PaymentHub:VnPay:HashSecret = ""`. Trước `S5`, `VerifyWebhookAsync`
tính `HMACSHA512` với **khoá rỗng** — không có nhánh nào kiểm khoá. 📌 Điều đó **vô hại khi chưa có endpoint**
(không ai gọi được), nhưng **thành lỗ hổng thật** ngay khi `/api/webhooks/vnpay` lên sóng:
người ngoài tính được cùng hash (khoá rỗng ai cũng biết) ⇒ **ký được IPN giả** `vnp_ResponseCode=00` ⇒
`MarkSucceeded` ⇒ đơn "đã trả tiền" ⇒ **giao hàng miễn phí** — đúng `PAY-001` §7.7.4 #1.

✅ **Đã sửa:** fail-closed **ngay trong provider** (metric `thieu-hashsecret`, `SignatureValid = false`) **+
cảnh báo lúc khởi động** (`[PAY-IPN-CHUA-CAU-HINH]`). 🔑 Hai lớp vì hai mục đích khác nhau: guard bảo vệ **tiền**,
log bảo vệ **người vận hành** khỏi phát hiện muộn.

#### 7.2.2 Chống trùng IPN — ba lớp và **thứ tự đúng**

| Lớp | Cơ chế | Chặn được gì | **Không** chặn được gì |
|---|---|---|---|
| **1. DB** | UNIQUE `(TenantId, ProviderCode, IdempotencyKey)` có filter `IS NOT NULL` | Hai **dòng** cho cùng một giao dịch cổng (`vnp_TransactionNo`) | Trùng khi `IdempotencyKey` còn `NULL` (trước IPN đầu tiên); **hai lần thu tiền thật** ở cổng (cổng cho phép) |
| **2. Domain** | `MarkSucceeded` trả `false` nếu `Status ∈ {Succeeded, PartiallyRefunded, Refunded}` | Đổi trạng thái lần hai; **hồi sinh** giao dịch đã hoàn | Tăng `WebhookReceivedCount` (vẫn tăng — **cố ý**, để đếm được) |
| **3. Quan sát** | `WebhookReceivedCount` + log | Phát hiện *"cổng gửi 7 lần"* ⇒ cấu hình `IpnUrl`/mạng có vấn đề | — |

> 🔑 **Vì sao phải có lớp 3:** nếu chỉ có lớp 1–2, hệ thống **im lặng** trước việc VNPay retry liên tục. Con số
> `WebhookReceivedCount` là **dữ liệu** (không phải log) ⇒ trả lời được *"IPN có bị lặp không"* bằng SQL.
> 📌 Nợ: chưa có metric/cảnh báo khi `WebhookReceivedCount > 3` trên một giao dịch (§15.1 **Q7**).

#### 7.2.3 Thứ tự đến **SAI** — 5 ca thật và cách xử lý

| # | Ca | Kết quả **đúng** | Code hiện tại làm gì |
|---|---|---|---|
| 1 | IPN `Succeeded` đến **2 lần** | Chỉ ghi **một** lần | `MarkSucceeded` trả `false` lần 2, `WebhookReceivedCount = 2` ✅ |
| 2 | IPN `Succeeded` đến **sau khi đã `Refunded`** | **Không** đổi trạng thái (giữ dấu vết dòng tiền) | ✅ **Đã sửa 16/10/2026** — `PaymentTransaction.cs:92-99` ghi rõ lỗi cũ: *"hồi sinh về `Succeeded` trong khi `RefundedAmount` vẫn giữ"* ⇒ sổ sách tự mâu thuẫn |
| 3 | IPN `Failed` đến **sau** IPN `Succeeded` | Giữ `Succeeded` | ✅ `MarkFailed` return sớm (`PaymentTransaction.cs:125-135`) |
| 4 | IPN `Succeeded` đến **sau khi `querydr` đã ghi `Failed`** | **Phải** chuyển sang `Succeeded` (tiền **đã** vào) | ✅ `MarkSucceeded` **cho phép** từ `Failed` (không nằm trong danh sách chặn) — đúng ý đồ |
| 5 | Hai IPN **khác giao dịch cổng** cho cùng `MerchantReference` | Dòng **đầu** thắng, dòng sau bị **chặn** bởi unique `IdempotencyKey` | 🟠 Sinh `23505` ⇒ **chưa có ánh xạ** trong `ViPhamRangBuocDb` ⇒ rơi về mã chung `eCommerce:Db:0001` (F-PAY-4) |

### 7.3 Gọi API ra ngoài

| NCC | Endpoint (base URL) | Auth | Timeout | Retry | Nếu thất bại |
|---|---|---|---|---|---|
| **VNPay** `pay` (tạo giao dịch) | `GET {PaymentHub:VnPay:PaymentUrl}` — mặc định `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` (`appsettings.json:25`, `VnPayPaymentProvider.cs:19`) | **Không gọi** — ta **dựng URL** rồi redirect khách | — | — | Dựng URL **không** gọi mạng ⇒ `CreatePaymentAsync` luôn trả `Success = true` (`VnPayPaymentProvider.cs:121-125`) ⚠️ **không có nghĩa** khách đã trả |
| **VNPay** `refund` + `querydr` | `POST {PaymentHub:VnPay:TransactionApiUrl}` — mặc định `https://sandbox.vnpayment.vn/merchant_webapi/api/transaction` (`appsettings.json:26`, `VnPayPaymentProvider.cs:22`) | **Checksum** trong **body JSON** (`vnp_SecureHash` là **một trường** của JSON — `VnPayPaymentProvider.cs:315-319`) | **30 s** (`eCommerceHttpApiHostModule.cs:189-192`) | ❌ **Không retry tự động** trong code | Trả `RefundResult` có `LoiHaTang` + `ChoXacNhan = true` — **cấm** retry mù (`VnPayPaymentProvider.cs:341-368`) |

> 🚨 **Hai điểm CHƯA kiểm chứng, ảnh hưởng trực tiếp tới tiền:**
> 1. **Kiểu chuỗi ký của API giao dịch.** Code ký bằng `DungChuoiKy` — **query-string** sắp xếp + URL-encode
>    (`VnPayRequestBuilder.cs:38-39`, `VnPayPaymentProvider.cs:312-313`). Trong khi `REC-001` §7.3 dẫn tài liệu
>    chính thức nói chuỗi ký của `querydr` dùng **dấu `|`** và **KHÔNG** URL-encode
>    (`rec-001-doi-soat-thanh-toan-querydr.md:588-590`: *"**Không** được tái dùng `BuildQueryString`/`HmacSha512`
>    máy móc cho `querydr`"*). ⇒ **Hoặc** tài liệu đó nói về phiên bản API khác, **hoặc** code đang ký sai ⇒
>    VNPay sẽ trả `Invalid Checksum`. **Không có credential ⇒ chưa kiểm chứng được.** 📌 Xem §15.1 **Q3**.
> 2. **`vnp_TransactionDate` phải là ngày giao dịch GỐC**, nhưng code dùng **now** (`VnPayRequestBuilder.cs:78,90`)
>    ⇒ với hoàn tiền sau vài ngày, cổng có thể trả *"không tìm thấy giao dịch"*. Đây chính là lý do `REC-001`
>    **H2** yêu cầu thêm cột `ProviderCreateDateGmt7`.

> 📌 **Không hard-code endpoint** (`docs/design-doc-guidelines.md:118`): cả 2 URL đọc từ `PaymentHubOptions`
> (`eCommerceHttpApiHostModule.cs:251`), đổi sandbox ↔ thật **không cần build lại**.
> 📌 **`HttpClient` là typed client ở Host**, không `new HttpClient()` trong provider — tránh **cạn socket** khi job
> chạy nhiều (`eCommerceHttpApiHostModule.cs:184-192`).

### 7.4 Idempotency key — **bảng quyết định**

| Đối tượng | Cách sinh khoá | ⚠️ Lưu ý |
|---|---|---|
| `MerchantReference` (`vnp_TxnRef`) | **Một lần**, từ **mã đơn** (`Order.Number`) + hậu tố thử lại, dạng `PAY{yyyyMMdd}-{OrderNumber}-{n}`; **lưu vào DB** | 🔒 **CẤM** `Guid.NewGuid()` mỗi lần (`XC-002` **BR-007**). VNPay yêu cầu **không trùng trong ngày** ⇒ tiền tố **ngày** là bắt buộc. ✅ **`S4`: ĐÃ CÓ HÀM SINH** — `SinhMaThamChieu` (§7.5) |
| `IdempotencyKey` | = `vnp_TransactionNo` (mã **cổng** cấp), lấy từ **IPN đã xác thực** | Chỉ có sau IPN đầu tiên ⇒ trước đó `NULL` (index có filter nên **không** chặn) |
| `vnp_RequestId` (refund/querydr) | ⚠️ `Guid.NewGuid().ToString("N")` **mỗi lần gọi** — `VnPayRequestBuilder.cs:82,118` | 🟠 **Vi phạm tinh thần BR-007**: `REC-001` đã ghi *"`RequestId` phải sinh **một lần** cho `Line`, retry **đọc lại**"*; ở đây retry (nếu có) sẽ sinh **mới** ⇒ mất khả năng đối chiếu *"yêu cầu nào đã gửi"*, và có thể ăn mã `94` (*trùng RequestId*) |
| `CorrelationId` | Do `XC-005` **BR-001** sinh ở biên vào | **Chưa có trong code** cùng outbox (§7.1) |

### 7.5 🚨 `MerchantReference` — bài học **phải** ghi vào tài liệu, không chỉ vào code

**Quy tắc:** mã giao dịch phía ta **ỔN ĐỊNH qua mọi lần retry**. Sinh `Guid.NewGuid()` mỗi lần là **sai kép**:

1. **Sai idempotency**: cùng một ý định thu tiền sinh ra **N** mã khác nhau ⇒ hệ thống không nhận ra *"đây là lần
   thử lại của cùng một giao dịch"* ⇒ khách bị **thu hai lần** mà đối soát **không** phát hiện (`XC-002` **BR-007**;
   test tham chiếu: `XC-002` **TC-10** — *"3 lần retry ⇒ **cùng một** IdempotencyKey"*).
2. **Sai yêu cầu cổng**: VNPay yêu cầu `vnp_TxnRef` **không trùng trong ngày** (`VnPayPaymentProvider.cs:42`).

Trạng thái hiện tại trong code:

| Mảnh | Trạng thái |
|---|---|
| Entity **bắt buộc** có mã ở constructor (`PaymentTransaction.cs:81-86`) | ✅ tốt — không thể tạo giao dịch "không có mã"; ✅ **`S4`** constructor **NÉM** `0006` nếu mã/cổng rỗng thay vì chỉ *"bắt buộc"* về mặt khai báo |
| Unique index `(TenantId, ProviderCode, MerchantReference)` | ✅ hàng rào cuối — có **test DB thật** (`PayTransactionsChanTrungTests`, `S4`) chứng minh `23505` **cả khi `TenantId = NULL`** |
| Hàm sinh mã **ổn định** (từ `Order.Number`) | ✅ **ĐÃ CÓ `S4`** — `SinhMaThamChieu` (Domain, **hàm thuần**): `PAY{yyyyMMdd GMT+7}-{sốĐơn}-{lầnThử}`; `Goc()` cho tiền tố ổn định để **đếm số lần thử** không cần cột mới; bỏ dấu tiếng Việt (`FormD` + `Đ→D`); **chặn `> 64`** (độ dài cột thật) thay vì để PostgreSQL cắt cụt |
| Chỗ gọi tạo giao dịch | ✅ **ĐÃ CÓ `S4`** — `PaymentAppService.TaoGiaoDichAsync` (+ HTTP `POST /api/app/payment/tao-giao-dich`); **idempotent theo đơn**: giao dịch đang mở ⇒ **dùng lại**, `MerchantReference` **không đổi** |
| Quy ước tiền tố ngày cho VNPay | ✅ **ĐÃ CÓ `S4`** — ngày lấy theo **GMT+7** (cùng múi giờ VNPay dùng để ký), có test khoá **cả** ca `20:00 UTC ⇒ ngày HÔM SAU** và ca `DateTimeKind.Unspecified` **không bị trừ offset** (bẫy #6 `handover` §5.1) |

### 7.6 🔑 Tuyên bố về **credential** — đường tiền thật **CHƯA KIỂM CHỨNG**

| Mục | Giá trị thật | Nguồn |
|---|---|---|
| `PaymentHub:VnPay:TmnCode` | `""` (**rỗng**) | `src/eCommerce.HttpApi.Host/appsettings.json:23` |
| `PaymentHub:VnPay:HashSecret` | `""` (**rỗng**) | `src/eCommerce.HttpApi.Host/appsettings.json:24` |
| `PaymentUrl` / `TransactionApiUrl` | **sandbox** URL có | `appsettings.json:25-26` |
| Kết luận | 🚨 **Chưa từng có một giao dịch thật nào đi qua cổng.** Mọi phép kiểm hiện có là **lõi thuần** (ký, đọc phản hồi, ánh xạ mã) + **nhánh lỗi hạ tầng** | `handover:74` (*nợ chặn #2*), `sal-006` §17.8 #1 |

> 📌 **Đọc kết quả test cho đúng:** *"20 + 7 unit test PASS"* (`handover:59`) **KHÔNG** có nghĩa *"hoàn được tiền"*.
> Nó có nghĩa *"phần tính toán và phân nhánh đúng"*. Câu *"tiền đã ra chưa"* **chỉ** trả lời được bằng một giao dịch
> thật trên sandbox có credential — và đó là **điều kiện mở** của nợ chặn #2.
> 🔒 **Vận hành:** `HashSecret` **không** được commit; phải đọc từ **biến môi trường/secret store** khi triển khai
> (`REC-001` §9, `appsettings.json` chỉ giữ giá trị rỗng).

### 7.7 🔑 Đường kiểm chứng khi **CHƯA có credential** — ba lớp TÁCH RỜI (chốt 17/10/2026)

> 🎯 **Câu hỏi của chủ dự án:** *"chưa có credential sandbox VNPay — đề xuất cách bypass"*.
> 🔑 **Trả lời ngắn: bypass được HAI lớp, KHÔNG được bypass lớp thứ ba.**
> Nếu chỉ nhớ một câu: **bypass *nhà cung cấp*, KHÔNG bypass *chữ ký*.**

| Lớp | Bản chất | Bypass được? | Cách làm |
|---|---|---|---|
| **① Ra ngoài** — gọi `paymentv2/vpcpay.html`, `merchant_webapi` | **Phụ thuộc NCC** | ✅ **ĐƯỢC** | `IPaymentProvider` **đã là interface** (`IPaymentProvider.cs:58-108`) ⇒ thêm implementation `FakePaymentProvider` (§7.7.2) |
| **② Vào trong** — VNPay gọi **IPN** về ta | **Phụ thuộc mạng** | ✅ **ĐƯỢC** — và **BẮT BUỘC phải bypass** ngay cả khi **có** credential | Trang giả lập nội bộ tự sinh IPN (§7.7.2) · hoặc tunnel khi cần IPN thật (§7.7.1) |
| **③ Xác thực `HMAC-SHA512` + ý nghĩa "đã thu tiền"** | **KIỂM SOÁT BẢO MẬT** | 🔴 **TUYỆT ĐỐI KHÔNG** | Xem §7.7.4 — bypass lớp này biến IPN thành **cửa mở đánh dấu đơn đã trả tiền** |

#### 7.7.1 📌 Sự thật ĐO ĐƯỢC từ tài liệu VNPay (có nguồn, không suy đoán)

| # | Sự thật | Nguồn |
|---|---|---|
| 1 | ✅ **Tự đăng ký merchant test ĐƯỢC** — VNPay có form tự phục vụ, **không cần hợp đồng**: *"Nếu chưa có thông tin `vnp_TmnCode`, `vnp_HashSecret`: vui lòng đăng ký thông tin kết nối CTT VNPAY"* | [sandbox.vnpayment.vn/apis/docs/gioi-thieu](https://sandbox.vnpayment.vn/apis/docs/gioi-thieu/) · form: [sandbox.vnpayment.vn/devreg](https://sandbox.vnpayment.vn/devreg/) (*"Đăng ký Merchant môi trường test"*) |
| 2 | ✅ **Thẻ test dùng ngay được** | cùng nguồn: `NCB` · `9704198526191432198` · `NGUYEN VAN A` · `07/15` · OTP `123456` |
| 3 | 🚨 **KHÔNG cần** đợi hợp đồng để có `TmnCode`/`HashSecret` — chỉ cần **đăng ký form** | cùng nguồn |
| 4 | 🚨 **IPN cần site MỞ RA INTERNET**: *"Website tích hợp cần được mở ra internet để máy chủ của VNPAY có thể gửi yêu cầu cập nhật kết quả thanh toán"* | cùng nguồn, mục *Chú ý* |
| 5 | ⚠️ **VNPay ghi `API Hoàn trả` = `/merchant_webapi/merchant.html`**, còn `appsettings.json:26` của ta là `/merchant_webapi/api/transaction` | cùng nguồn, mục *Thông tin cấu hình* — 📌 **lệch cần xác nhận** khi có credential (§15.3) |

🔑 **Hệ quả quan trọng nhất của sự thật #3: nợ chặn #2 đang bị đánh giá sai mức.**
Nó **không** phải *"chờ đối tác cấp credential"* — nó là **một tờ khai đăng ký 5–10 phút**. ⇒ **`S4`/`S5` KHÔNG bị chặn** như `handover` §3.1 ngụ ý.
🔑 Và sự thật #4 giải thích vì sao lớp ② **phải** bypass: trên `localhost` thì **kể cả có credential**, VNPay **không gọi được** IPN về.

#### 7.7.2 Thiết kế `FakePaymentProvider` — "cổng giả lập" chạy **offline**, chữ ký **THẬT**

| Thành phần | Quyết định | Vì sao |
|---|---|---|
| Vị trí | `modules/PaymentHub/eCommerce.PaymentHub.Application/Providers/FakePaymentProvider.cs` | Cạnh `VnPayPaymentProvider.cs` — cùng một abstraction, **không** nhánh `if` rải rác |
| Đăng ký | `PaymentHub:Provider` = `VnPay` \| `Fake`; chọn tại `PaymentHubApplicationModule.cs` (nơi **đã** đăng ký `IPaymentProvider`) | Chỉ **một** điểm quyết định, dễ audit |
| 🚨 Chốt an toàn | `Fake` **chỉ** hợp lệ khi `IHostEnvironment.EnvironmentName = Development`; đặt `PaymentHub:Provider=Fake` ở môi trường khác ⇒ **NÉM NGAY lúc khởi động** (`OnApplicationInitialization`) | Một cấu hình sai **không** được phép thành cửa mở ở production |
| Trang giả lập | `GET /pay-gia-lap/{txnRef}` → HTML 3 nút **Thành công / Thất bại / Huỷ**; `POST` → dựng query `vnp_*` rồi **gọi IPN của CHÍNH TA** bằng HTTP thật | Kiểm được **cả** lớp ② mà không cần internet |
| 🔑 Chữ ký | Trang giả lập **DÙNG LẠI** `VnPayRequestBuilder.DungChuoiKy` + `Ky` với **cùng** khoá `PaymentHub:VnPay:HashSecret` (**dev-only**) | 📌 **Không** viết lại thuật toán ký — viết lại thì ta kiểm **bản sao**, không kiểm **code thật** |
| Sinh IPN | `FakePaymentProvider` trả URL trỏ trang giả lập; `FakePaymentProvider.VerifyWebhookAsync` **uỷ quyền** cho `VnPayPaymentProvider` ⇒ đường xác thực chữ ký **luôn** được chạy thật | ⇒ **không** có bản kiểm chữ ký thứ hai để lệch nhau |
| Dùng cho unit test | Cùng lớp, đăng ký trong test module | 🔎 Hiện **0** tệp test có `IPaymentProvider` ⇒ đây là **test double đầu tiên** |

**Bật/tắt bằng cấu hình, không sửa code** (`S8` sửa lại **khoá cấu hình** cho khớp mã thật):

```json
"PaymentHub": {
  "Provider": "Fake",
  "VnPay": { "TmnCode": "GIALAPDEV", "HashSecret": "<hằng DEV>" },
  "Fake": { "SimulatorBaseUrl": "http://localhost:5100", "IpnBaseUrl": "http://localhost:5000" }
}
```

🚨 **Bản `v0.1` của mục này ghi `Payments:Provider` + `Payments:Fake:HashSecret` — SAI HAI CHỖ, và `S8` sửa
theo đúng quy tắc *"code lệch tài liệu ⇒ sửa tài liệu trước"*:**

1. Khoá cấu hình thật là **`PaymentHub:*`** (`eCommerceHttpApiHostModule` bind `configuration.GetSection("PaymentHub")`).
   Đặt `Payments:*` thì **không có gì đọc** ⇒ `Provider` luôn rỗng ⇒ **luôn** chạy VNPay, **im lặng**.
2. **`Fake:HashSecret` riêng là KHÔNG THỂ CHẠY.** Bên **kiểm** chữ ký là `VnPayPaymentProvider`, nó đọc
   **`PaymentHub:VnPay:HashSecret`**. Ký bằng khoá khác ⇒ **mọi** IPN giả lập bị `400 Invalid Checksum` — 📌 và
   đó **không** phải lỗi cần *"nới lỏng kiểm tra"*, mà là **ta ký sai khoá**. ⇒ Dùng **một** khoá dev cho cả hai
   phía, giữ nguyên bất biến §7.7.4 #1.

⚠️ Hằng dev đó **KHÔNG** phải `HashSecret` của VNPay, **không** dùng cho production, **không** cần giữ bí mật —
nhưng **phải** khác rỗng (rỗng ⇒ `fail-closed`, mọi chữ ký bị từ chối) và **chỉ** nằm trong
`appsettings.Development.json` của **cả hai** tiến trình (`eCommerce.Web` ký, `eCommerce.HttpApi.Host` kiểm).

#### 7.7.3 Ba cách — và cách nào dùng khi nào

| Cách | Mở khoá được gì | Chi phí | Khi nào dùng |
|---|---|---|---|
| **C1. Đăng ký merchant test** (khuyến nghị **làm NGAY**) | `TmnCode`+`HashSecret` thật · tạo URL thanh toán · **gọi được `refund`/`querydr`** | **5–10 phút** (form + captcha + email) | Luôn — để đóng **nợ chặn #2** một cách thật |
| **C2. Tunnel** (`cloudflared`/`ngrok`) trỏ về `:5000` | **IPN thật** trên máy local (VNPay gọi vào được) | ~10 phút | Chỉ cho **lần nghiệm thu cuối** (`DoD-tiền-thật`) |
| **C3. `FakePaymentProvider` + trang giả lập** | Toàn bộ đường tiền **offline** · **chạy được trong CI** · tất định | ~0.5–1 phiên | **Mặc định** cho dev + CI. 📌 Cũng là cách **duy nhất** kiểm IPN trùng/sai chữ ký **tất định** |

🔑 **C1 và C3 không thay thế nhau:** C1 cho *"tiền thật đã ra"*, C3 cho *"hành vi đúng và lặp lại được"*.
📌 C2 là **điều kiện** của C1 khi chạy local — nên **C1 + C2** mới đóng được `DoD-tiền-thật`.

#### 7.7.4 🔴 Ba thứ **CẤM** (bypass sai chỗ = mất tiền hoặc mất bảo mật)

| # | Cấm | Hậu quả cụ thể |
|---|---|---|
| 1 | 🚫 **Bỏ / vô hiệu `KiemChuKy`** (`VnPayRequestBuilder.cs:234-268`) hoặc bỏ `VerifyWebhookAsync` (`VnPayPaymentProvider.cs:131-197`) | Bất kỳ ai **tự gửi** `POST` `vnp_ResponseCode=00` ⇒ ta đánh dấu **đơn đã trả tiền** ⇒ **giao hàng miễn phí**. Đây là **IPN spoofing**, không phải "tuỳ chọn môi trường" |
| 2 | 🚫 **Hard-code `TmnCode`/`HashSecret`** vào `appsettings.json` rồi commit | Lộ secret ⇒ người khác ký được IPN **thay** VNPay. 🔒 Phải đọc từ **biến môi trường/secret store** |
| 3 | 🚫 **Đặt `Payments:Provider=Fake` mặc định** ở `appsettings.json` (không phải `.Development.json`) | Quên đổi khi deploy ⇒ production **nhận thanh toán giả** ⇒ sổ sách **sai mà không có lỗi nào**. Đây là lý do có **fail-fast** ở §7.7.2 |

➕ Ghi chú cho người review: cách "bypass" **đúng** là **thay nhà cung cấp** (lớp ①/②) — **không** phải **nới lỏng kiểm tra** (lớp ③).

---

## 8. Bất biến & xử lý đồng thời

### 8.1 Bất biến

| # | Bất biến / tình huống | Cơ chế bảo vệ | Có test? |
|---|---|---|---|
| 1 | **Không ghi nhận hai lần** một giao dịch của cổng | UNIQUE `(TenantId, ProviderCode, IdempotencyKey)` + `MarkSucceeded` trả `false` + `WebhookReceivedCount` | ✅ `PaymentRefundTests.cs:146-160` |
| 2 | **Tổng hoàn KHÔNG vượt** số đã thu | `RegisterRefund` **tính trước → kiểm → mới gán** (`PaymentTransaction.cs:187-201`) | ✅ `PaymentRefundTests.cs:32-64` |
| 3 | **Không hoàn** cho giao dịch chưa thu tiền | `DungDuocDeHoan` kiểm ở **2 nơi** (app service **trước khi** gọi cổng + entity **trước khi** ghi) | ✅ `PaymentRefundTests.cs:94-117` |
| 4 | **Số tiền hoàn > 0** | `RegisterRefund` (ném `0004`) **và** app service (ném trước khi gọi cổng) | ✅ `PaymentRefundTests.cs:119-130` |
| 5 | **Trạng thái cuối không bị ghi đè** (kể cả webhook gửi lại sau hoàn tiền) | `MarkSucceeded`/`MarkFailed` cùng danh sách cuối; `LuatDoiSoatThanhToan.DaKetThuc` **cùng** danh sách | ✅ `PaymentRefundTests.cs:146-160` · `LuatDoiSoatThanhToanTests.cs` |
| 6 | **Không xoá** giao dịch đã có tiền | `FullAuditedAggregateRoot` có `IsDeleted` nhưng 🔒 **quy ước**: cấm gọi `DeleteAsync`; DB vẫn cho phép ⇒ **chưa có hàng rào** | ⚠️ **Chưa có test** — nợ |
| 7 | **Không so tiền bằng `==`** | Review + test quét mã nguồn (`XC-001` **BR-003**) | ⚠️ **Chưa có test quét** cho module này |
| 8 | **Payload lưu DB đã che** | `MaskSensitiveData` 2 lớp + cắt 8000 | ✅ `WebhookMaskingTests.cs:15-63` |
| 9 | **Che là giảm thiểu, không phải bảo đảm** | Không log payload thô (`XC-005` **BR-005**) | ✅ `WebhookMaskingTests.cs:41-48` |
| 10 | **`MerchantReference` duy nhất** trong `(tenant, provider)` | UNIQUE **có `NULLS NOT DISTINCT`** | ⚠️ Test DB nằm ở `PostgreSqlSchemaTests` (index **tồn tại**), **chưa** có test *chèn trùng bị chặn* cho bảng này |
| 11 | **Tổng hoàn ≤ đã thu ở tầng DB** | ❌ **KHÔNG CÓ** — không `CHECK` (§5.3). Chỉ tầng domain chặn | ❌ không |
| 12 | **Không có 2 luồng cùng gọi cổng hoàn tiền** | Khoá phân tán `payments:refund:{id}` (`PaymentAppService.cs:125-128`) | ⚠️ Chưa có **integration test 2 luồng** (§14 **TC-14**) |

> 🔒 **Vì sao #2 dùng "tính trước — kiểm — mới gán"** (bài học `InventoryItem.ApplyMovement`, 15/10/2026 — ghi nguyên
> văn ở `PaymentTransaction.cs:161-163`): nếu **gán trước rồi kiểm**, entity **ở lại trạng thái SAI trong bộ nhớ**;
> EF vẫn đang theo dõi nó ⇒ một `SaveChanges` **vì việc khác** sẽ **ghi số sai xuống DB**.
> 📌 *"Ném lỗi mà để lại rác là gieo bug."*

### 8.2 Xử lý đồng thời — các tình huống thật

| # | Tình huống | Cơ chế hiện có | Đánh giá |
|---|---|---|---|
| 1 | **Double-click "Hoàn tiền"** | Khoá `payments:refund:{paymentTransactionId}` TTL **15 s**; request 2 nhận `eCommerce:Lock:0001` | ✅ đúng — chặn ở tầng **gọi cổng** (side effect không rollback được) |
| 2 | **Hai instance cùng nhận IPN** (VNPay gửi 2 lần song song) | UNIQUE `IdempotencyKey` + `ConcurrencyStamp` (optimistic) | 🟠 Một luồng nhận `23505` hoặc `AbpDbConcurrencyException` ⇒ **chưa** có ánh xạ ⇒ mã chung (`ViPhamRangBuocDb` không có `PayTransactions`) |
| 3 | **Job đối soát và app service cùng ghi một giao dịch** | Cả hai dùng `IRepository` + `UpdateAsync`; `LuatDoiSoatThanhToan.Quyet` **chỉ tiến** nên kết quả hội tụ | 🟡 Chấp nhận được: hai đường cùng tiến về một trạng thái; va chạm `ConcurrencyStamp` sẽ **ném** — nhưng **không** có khoá chung ⇒ có thể **một** luồng phải thử lại (hiện **không** có retry ⇒ lượt sau) |
| 4 | **Hai instance cùng chạy job** | Khoá `payment:reconciliation-rec001` TTL **120 s**; instance sau **bỏ lượt** + `LogDebug` (`PaymentReconciliationWorker.cs:91-98`) | ✅ đúng (`Debug` là **cố ý** — instance khác đang chạy là **bình thường**) |
| 5 | **Redis chết** | `AbortOnConnectFail = false` (`eCommerceHttpApiHostModule.cs:239-247`) ⇒ app vẫn chạy, **khoá vô hiệu** | 🔴 Khi đó **chỉ còn** UNIQUE index + bất biến entity. Với hoàn tiền: **hai** request cùng gọi cổng **vẫn** có thể xảy ra ⇒ bất biến chỉ **phát hiện sau khi** tiền đã ra (`XC-003` D2) |
| 6 | **Tiến trình bị kill giữa lượt hoàn tiền** (sau khi cổng nhận, trước khi ghi sổ) | Không có gì bảo vệ — trạng thái cổng **đi trước** DB | 🚨 **Nợ thật**: phải có `querydr` xác nhận. Không có job riêng ⇒ xem §15.1 **Q6** |

### 8.3 Khoá phân tán dùng trong feature — **tên CHÍNH XÁC**

> 🔑 **Cơ chế tiền tố:** `khoá_thật = KeyPrefix + tên_khoá`, ghép **KHÔNG** dấu phân cách; `KeyPrefix` thật là
> `"ecommerce:"` (`eCommerceHttpApiHostModule.cs:217-230`, sửa sau sự cố đo bằng `redis-cli MONITOR`).
> ⇒ **Tên khoá do module sinh KHÔNG chứa `ecommerce:`** (nếu có sẽ thành **tiền tố đôi**) — `XC-003` §5.4.

| Tên khoá (như trong code) | Khoá **thật** trong Redis | Phạm vi | Vì sao cần | Nguồn |
|---|---|---|---|---|
| `payments:refund:{paymentTransactionId}` | `ecommerce:payments:refund:{guid}` | theo **giao dịch** | Chống 2 request cùng gọi cổng hoàn tiền (hoàn **gấp đôi**) | `PaymentAppService.cs:126` |
| `payment:reconciliation-rec001` | `ecommerce:payment:reconciliation-rec001` | theo **toàn hệ thống** (không có `{tenantId}`) | Chống 2 instance chạy chồng lượt đối soát | `PaymentReconciliationWorker.cs:59` |
| 🆕 `payment:create:{orderId (N-format) hoặc sốĐơn}` | `ecommerce:payment:create:{guid}` | theo **đơn** | **`S4`** — chống 2 request đồng thời **cùng tạo giao dịch** cho một đơn (double-click / client retry). 📌 Đúng quy ước `XC-003` §5 (đoạn `payment`, không phải `payments`) — 🔑 nghĩa là **nay** module có cả hai kiểu tiền tố; xem `F-PAY-5` dưới | `PaymentAppService.TaoGiaoDichAsync` |

> 🟠 **F-PAY-5 (phát hiện, không phải lỗi chặn):** hai khoá của **cùng một module** dùng **hai đoạn khác nhau** —
> `payments:` (app service) và `payment:` (worker). `XC-003` §5 ánh xạ module `PaymentHub` → đoạn **`payment`**
> (`XC-003-khoa-phan-tan-quy-uoc.md:225`). ⇒ khoá hoàn tiền **lệch quy ước**. Hệ quả thật: khi vận hành **gỡ khoá
> treo bằng tay** (`redis-cli DEL`), người trực phải nhớ **hai** kiểu tên; gõ sai kiểu `payment:refund:…` thì
> **không** gỡ được gì mà cũng **không** báo lỗi. 📌 Đề xuất: đổi thành `payment:refund:{id}` **và** thêm
> `{tenantId}` (`XC-003` §5 mẫu có tenant) — xem §15.1 **Q8**.
> 🟠 **F-PAY-6:** khoá job **không** có `{tenantId}` ⇒ với nhiều tenant, **một** tenant có tồn đọng lớn sẽ **chặn**
> lượt của tenant khác trong 15 phút đó (đói tạm thời). Hiện chưa thành sự cố vì **chưa chạy nhiều tenant**.

### 8.4 Idempotency key — tổng hợp

| Đối tượng | Cách sinh | ⚠️ Lưu ý |
|---|---|---|
| Giao dịch (ta) | `MerchantReference` từ **mã đơn** | Xem §7.5 — **chưa có trong code** |
| Giao dịch (cổng) | `IdempotencyKey = vnp_TransactionNo` | Chỉ có sau IPN đã xác thực |
| Yêu cầu `refund`/`querydr` | `vnp_RequestId = Guid.NewGuid("N")` | 🟠 **Không ổn định** qua retry (§7.4) |
| Khoá hoàn tiền (chống đồng thời) | `paymentTransactionId` | ✅ ổn định (là khoá chính) |

### 8.5 Chiến lược transaction

| Đường | UoW | Ghi chú |
|---|---|---|
| `HoanTienAsync` | ABP mở **một** UoW cho cả method; `UpdateAsync(autoSave: true)` ghi **ngay trong** UoW | ⚠️ Lời gọi cổng nằm **trong** UoW ⇒ nếu cổng chậm **30 s**, transaction DB **giữ lâu**; 📌 chấp nhận vì `RegisterRefund` phải cùng UoW với lần đọc `ConHoanDuoc`. 🆕 **`S5`:** dòng hoàn tiền được ghi **trong cùng** UoW đó ⇒ *"có dòng `DaHoan`"* và *"`RefundedAmount` đã tăng"* **không thể** lệch nhau |
| `PostTruyVanTrangThaiAsync` | một UoW | Ghi **chỉ** khi `Quyet` nói ghi |
| 🆕 `PostDoiSoatLoatAsync` (**`S5`**) | `[UnitOfWork(isTransactional: false)]` + **mỗi giao dịch một UoW** (`Begin(options{IsTransactional}, requiresNew: true)`) | ✅ **Vá `F-PAY-7`:** lỗi ở giao dịch #37 **không** rollback 36 lần ghi đã xong. ➕ `try/catch` từng vòng + **đếm `SoLoi`** (nếu không đếm thì lỗi lại **im lặng** — lượt "xong" mà ít thay đổi hơn mong đợi). 📌 `Q10` đã chốt |
| 🆕 `PaymentReconciliationWorker` (**`S5`**) | **BỎ** UoW bao cả lượt; mỗi giao dịch `Begin(requiresNew: true, isTransactional: true)` | Cùng lý do trên. 🔒 Và **neo kết thúc vẫn được ghi** kể cả khi một giao dịch lỗi — 📌 trước `S5` một lỗi làm **mất cả lượt** ⇒ log chỉ có neo mở đầu, đúng lỗi *"job chạy rồi NÉM"* đã trả giá ở `SAL-005` |

---

## 9. Phân quyền

| Permission | Nhóm | Method được gán | Vai trò mặc định | Nguồn |
|---|---|---|---|---|
| `eCommerce.Payments` (`Permissions.Payments.Default`) | `eCommerce.Payments` | **cấp CLASS** trên `PaymentAppService` ⇒ áp cho **mọi** method; thêm nữa gán riêng cho `GetTheoMaAsync`, `GetDanhSachAsync`, `PostDoiSoatLoatAsync` | admin, kế toán, CSKH (đọc) | `eCommerceConsts.cs:86-90`; `eCommercePermissionDefinitionProvider.cs:75-76`; `PaymentAppService.cs:33` |
| `eCommerce.Payments.Refund` (`Permissions.Payments.Refund`) | `eCommerce.Payments` | `HoanTienAsync` (`:103`) · `PostTruyVanTrangThaiAsync` (`:254`) | **kế toán trưởng** (đề xuất — **chưa** seed) | như trên |

**Ba điểm phải nói rõ (và một điểm là nợ):**

| # | Điểm | Bằng chứng | Đánh giá |
|---|---|---|---|
| 1 | ✅ `[Authorize]` ở **cấp CLASS** | `PaymentAppService.cs:33` — `[Authorize(eCommerceConsts.Permissions.Payments.Default)]` | Đúng quy ước #4; thiếu nó thì mọi method **không** gắn attribute thành **API công khai** (đã xảy ra thật ở Phase 0 — `handover:143-145`) |
| 2 | ✅ Quyền **hoàn tiền tách khỏi** quyền **xem giao dịch** | 2 hằng số riêng (`eCommerceConsts.cs:88-89`); provider `AddChild` (`eCommercePermissionDefinitionProvider.cs:75-76`) | *"Người đọc để chăm sóc khách"* **không** đương nhiên được **chi tiền** |
| 3 | ✅ Controller Web có `[Authorize]` cấp class | `DoiSoatThanhToanController.cs:20` | Nhưng **lớp bảo vệ thật** là app service (nếu controller thiếu, app service vẫn chặn) |
| 4 | 🚨 **`PostDoiSoatLoatAsync` chỉ cần quyền ĐỌC** (`Payments.Default`) mà lại **gọi cổng tới 100 lần/lượt** | `PaymentAppService.cs:327` **không** có attribute riêng ⇒ kế thừa `Default` | 🟠 **F-PAY-8**: người chỉ cần *"xem giao dịch"* có thể **tiêu quota cổng** và **ghi trạng thái** (vì hàm lô gọi hàm một, hàm một **có thể** `MarkSucceeded`). 📌 Đề xuất: `PostDoiSoatLoatAsync` cần **`Payments.Refund`** (hoặc một quyền `Payments.Reconcile` mới) — xem §15.1 **Q9** |
| 5 | ✅ Background worker **không** gọi app service | `PaymentReconciliationWorker.cs:24-39` giải thích; dùng `IRepository` trực tiếp | 🔒 Nếu gọi ⇒ `AbpAuthorizationException` vì không có principal (`handover:144-145`; lỗi **đã xảy ra thật** ở `SAL-005`) |
| 6 | ⚠️ Permission `Payments.Refund` **chưa được seed** cho vai trò nào | Không thấy `PermissionGrant`/seed cho `Payments` | 🟠 Hệ quả: **mọi người** (kể cả admin) nhận **`403`** cho tới khi cấp quyền (`XC-004` D2/E3 — *đã xảy ra thật ở Phase 0*) |

---

## 10. UI Mockup & mapping dữ liệu

**UI hiện có (sau `S8` — 18/10/2026):**

| Màn hình | Route | Trạng thái |
|---|---|---|
| **Đặt hàng + Thanh toán** | `GET/POST /DatHang` → `302` sang **cổng** | ✅ **`S8` mới**: ô *"Thanh toán ngay"* (chỉ hiện khi có `eCommerce.Payments.Default`) ⇒ tạo **giao dịch** rồi chuyển sang cổng |
| **Cổng giả lập** (dev-only) | `GET/POST /pay-gia-lap/{maThamChieu}` | ✅ **`S8` mới** — 3 nút *Thành công / Thất bại / Huỷ*; **ký HMAC-SHA512 thật** rồi gọi **IPN thật** |
| **Kết quả thanh toán** | `GET /DatHang/KetQua?ma=…` | ✅ **`S8` mới** — trạng thái **đọc từ DB**, KHÔNG từ query string |
| **Đối soát** | `/DoiSoatThanhToan` (danh sách + nút *Truy vấn trạng thái*) | có từ trước |

❌ Vẫn **chưa có** UI **hoàn tiền** (đường gọi API đã có, **chưa** có nút) — nợ §16.9 #3/#5.

🚨 **BẤT BIẾN CỦA TRANG KẾT QUẢ (bài học `D2`, `S8` thi hành bằng mã):** `returnUrl` và mọi tham số `vnp_*`
trên URL **KHÔNG phải** bằng chứng thanh toán. Trang đọc `PayTransactions.Status` từ DB — nơi **chỉ** IPN đã
kiểm chữ ký mới ghi được. ✅ E2E `S8-P11` đo **chiều ngược**: một giao dịch chưa trả + `?vnp_ResponseCode=00`
⇒ trang **vẫn** nói *"CHƯA thanh toán"* và DB **không** đổi (`S8-P12`).

**Mapping dữ liệu của trang kết quả:**

| Vùng UI | Dữ liệu hiển thị | Nguồn (DTO / trường) | Hành động |
|---|---|---|---|
| **Header** | Tiêu đề *"Đối soát thanh toán"* + thông báo kết quả (`TempData["ThongBaoThanhToan"]` / `TempData["LoiThanhToan"]`) | `DoiSoatThanhToanController.cs:74,80-81` | — |
| **Bộ lọc** | *"Mã tham chiếu"* (`maThamChieu`), *"Trạng thái"* (`trangThai`) | `ViewBag.MaThamChieu`, `ViewBag.TrangThai` (`:47-48`) | Submit GET |
| **Bảng danh sách** | `MerchantReference`, `ProviderCode`, `Amount`, `RefundedAmount`, `ConHoanDuoc`, `Status`, `PaidAtUtc`, `WebhookReceivedCount`, `CreationTime` | `PaymentTransactionDto` (`PaymentDtos.cs:7-35`) | Nút **"Truy vấn trạng thái"** ⇒ `POST /DoiSoatThanhToan/TruyVanTrangThai` (kèm `__RequestVerificationToken`) |
| **Cột trạng thái** | **chuỗi** (`gd.Status.ToString()` — `PaymentAppService.cs:433`) | `PaymentTransactionDto.Status` | ⚠️ Khác biệt **kiểu** với entity: entity là **enum** — bài học `CS1503`/`CS0019` đã vấp **hai lần** (`PaymentAppService.cs:338-342,359-365`) |

> ⚠️ **Chênh lệch số liệu — phải ghi rõ (thói quen tốt của mẫu §10):** trang **không** hiển thị *"tổng số giao dịch"*.
> `GetDanhSachAsync` trả **tối đa** `toiDa` (mặc định **50**, trần **200** — `PaymentAppService.cs:79`) và **không**
> trả tổng. ⇒ Người vận hành **không phân biệt được** *"chỉ có 50 giao dịch"* với *"có 5 000, đang xem 50"*.
> 📌 **Nợ UI** (`handover:221` — *"`/KhachHang` phân trang"* là cùng họ): thêm `TotalCount` vào DTO danh sách.
> 🔒 Và **không** được tự đặt `trangThai = Pending` mặc định — sẽ **ẩn** đúng các giao dịch bất thường cần tìm
> (`DoiSoatThanhToanController.cs:33-37`).

> 🚨 **Màn hình hiện tại KHÔNG trả lời được câu hỏi quan trọng nhất** — *"khách đã trả chưa"* — nếu **không bấm**
> nút truy vấn: bảng chỉ phản ánh **trạng thái trong DB**, mà DB chỉ được cập nhật khi có **IPN** (chưa có endpoint)
> hoặc khi **ai đó bấm**. 📌 Đây chính là lý do Job `REC-001` được thêm (`SAL-006` §21.1).

---

## 11. Migration & seed

| Mục | Nội dung |
|---|---|
| **Migration tạo bảng** | `20261009070214_Initial` — `src/eCommerce.EntityFrameworkCore/Migrations/20261009070214_Initial.cs` |
| **Bảng tạo mới (phần liên quan)** | `PayTransactions` (`:223-255`) + **3 index** (`:404-420`): `IX_PayTransactions_OrderId`, `IX_PayTransactions_TenantId_ProviderCode_IdempotencyKey` (filter `IS NOT NULL`), `IX_PayTransactions_TenantId_ProviderCode_MerchantReference` |
| **Migration sửa index (quan trọng hơn cả migration tạo bảng)** | `20261013024226_Fix_NullableTenantId_UniqueIndexes` — `DROP` + `CREATE` lại **2** unique index với **`NULLS NOT DISTINCT`**, viết bằng **SQL thô** (`:61-68`). 📌 Vì EF **không** mô hình hoá thuộc tính này |
| **Seed** | **Không có.** `PaymentHub` không cần dữ liệu nền: không có danh mục, không có cấu hình trong DB (cấu hình cổng nằm ở `appsettings`/secret store) |
| **Ảnh hưởng dữ liệu cũ** | **Không cần backfill.** Tại thời điểm viết tài liệu, **chưa từng** có giao dịch thật nào đi qua cổng (credential rỗng — §7.6) ⇒ không có dữ liệu lịch sử phải chuyển đổi. ⚠️ **Nhưng nếu** đã có dòng dữ liệu thử: việc thêm `NULLS NOT DISTINCT` **có thể NỔ** nếu tồn tại bản ghi trùng `(TenantId, ProviderCode, MerchantReference)` với `TenantId = NULL` — migration `20261013024226` **không** có bước dọn trùng trước `CREATE UNIQUE INDEX` |
| **Rollback** | `Down` của `20261013024226` tạo lại index **KHÔNG** có `NULLS NOT DISTINCT` (`:114-121`) ⇒ 🚨 **rollback làm MẤT hàng rào chống trùng** ở chế độ 1 tenant. 🔒 Nếu buộc phải rollback: **phải** chạy lại SQL `Up` ngay sau, hoặc chấp nhận rủi ro **có ghi nhận bằng văn bản** |
| **Quy trình (đã trả giá, đừng tự nghĩ lại)** | `handover:259-268`: `dotnet ef migrations add` → **thêm tay** SQL thô `NULLS NOT DISTINCT` (kèm `DROP INDEX IF EXISTS`) nếu có unique index trên `IMultiTenant` → build `DbMigrator` → chạy → **regenerate** `test/eCommerce.IntegrationTests/Assets/schema.sql` → **cập nhật con số `NND`** (nay là **52**) trong `PostgreSqlSchemaTests` |

**Migration cần thêm cho các nợ của tài liệu này:**

| # | Thay đổi | Vì sao | Ảnh hưởng `NULLS NOT DISTINCT` |
|---|---|---|---|
| 1 | ✅ **`S5` — ĐÃ LÀM:** `S5_PayRefunds_VaRangBuocHoanTien` — bảng `PayRefunds` + UNIQUE `(TenantId, PaymentTransactionId, RefundRequestId)` | Trả lời *"hoàn lúc nào, vì sao, ai"* bằng dữ liệu (§5.6 #1) | ✅ Đã tăng **51 → 52** (con số ở thời `S5`, không phải `48`); `PostgreSqlSchemaTests` đổi tên `F1_du_52_…` **và** thêm tên index vào danh sách đích danh **trong cùng PR** |
| 2 | ✅ **`S5` — ĐÃ LÀM:** `CK_PayTransactions_RefundedAmount_KhongAm` + `…_KhongVuotAmount` + `CK_PayRefunds_Amount_Duong` | Bất biến **tiền** ở tầng DB (§5.3) | Không đổi (không phải index) — ✅ **đã** thêm ánh xạ `23514` vào `ViPhamRangBuocDb` |
| 3 | Cột `ProviderCreateDateGmt7` | `querydr`/`refund` cần **ngày giao dịch gốc** (`REC-001` **H2**) | Không đổi |
| 4 | INDEX `(TenantId, Status, CreationTime)` | NFR-PAY-001 (job + UI đều quét bảng) | Không đổi (index **không** unique) |

> 🆕 **`S5` cũng đã regenerate `test/eCommerce.IntegrationTests/Assets/schema.sql`** bằng
> `dotnet ef migrations script` (39 mục lịch sử, trước là 38) — 📌 và **kiểm bằng `Compare-Object`**: diff **chỉ**
> thêm phần của migration mới, **không** xoá/sửa dòng nào (nếu regenerate mà lệch phần cũ thì nguyên nhân là
> **bản build cũ** — bẫy `B1`/`B2` của `tools/README-probe.md`).


---

## 12. Xử lý lỗi & trường hợp biên

> 🔑 **Nguyên tắc của mẫu:** phân biệt **(a) không có dữ liệu — KHÔNG phải lỗi** với **(b) lỗi thật**.

### 12.1 Bảng tình huống

| Tình huống | Nguyên nhân | Hành vi hệ thống | Phản hồi tới client | Loại |
|---|---|---|---|---|
| **Không tìm thấy giao dịch theo mã** | `merchantReference` không tồn tại | `GetTheoMaAsync` trả **`null`** (`PaymentAppService.cs:63-71`) | **`200` + body rỗng/`null`** | ✅ **(a) không phải lỗi** |
| **Danh sách rỗng** | chưa có giao dịch / bộ lọc không khớp | Trả **danh sách rỗng** | `200` + `[]` | ✅ **(a)** |
| **Lượt đối soát không có gì để xét** | mọi giao dịch đã ở trạng thái cuối | Job log `[PAY-JOB-RONG]` và **kết thúc bình thường** (`PaymentReconciliationWorker.cs:148-152`) | — | ✅ **(a)** |
| **`SoKhongXacDinh == SoDaXet`** | 🚨 cổng **chưa cấu hình** (credential rỗng) / cổng chết | **KHÔNG** phải *"hệ thống khoẻ"*: log `Warning` `[PAY-DOISOAT-KHONG-XAC-DINH]` + gauge `payment_recon_unresolved_total` (`PaymentAppService.cs:394-404`; `PaymentReconciliationWorker.cs:226-233`) | `200` + `soKhongXacDinh = soDaXet` | 🚨 **(b) lỗi cấu hình — nhưng KHÔNG ném**, đúng ý đồ: ném ở job sẽ **giết** job |
| **Thiếu/sai tham số hoàn tiền** | `soTien ≤ 0`, thiếu `lyDo`, thiếu `NguoiThucHien` | **NÉM** `0004` **trước khi** gọi cổng (`PaymentAppService.cs:108-140`) | `400`-nghĩa (`0004`) | 🚨 **(b)** |
| **Giao dịch chưa thu tiền** | `Status ∈ {Initiated, Pending, Failed, Cancelled}` | **NÉM** `0003` (`PaymentTransaction.cs:175-185`) | `0003` | 🚨 **(b)** |
| **Hoàn vượt số đã thu** | double-click / lỗi tính | **NÉM** `0002` **trước** khi gọi cổng **và** lại ném ở entity (`PaymentAppService.cs:142-149`; `PaymentTransaction.cs:189-199`) | `0002` | 🚨 **(b)** |
| **Không có quyền** | thiếu `Payments.Refund` | `AbpAuthorizationException` từ interceptor (cấp class + method) | `403` | 🚨 **(b)** |
| **Không giành được khoá** | thao tác khác đang chạy | **NÉM** `eCommerce:Lock:0001` với `khoa` + `choGiay` | `409`-nghĩa | 🚨 **(b)** — không phải "hệ thống hỏng", **đừng** retry ngay |
| **Cổng TỪ CHỐI** (đọc được JSON, mã khác `00`) | tham số sai / giao dịch gốc không thấy | **KHÔNG** ghi sổ, `ChoXacNhan = false`, log `[PAY-TU-CHOI-HOAN]` (`PaymentAppService.cs:234-240`) | `200` + `lyDoThatBai` | 🚨 **(b) lỗi nghiệp vụ** — sửa tham số rồi gửi lại **được** |
| **Cổng ĐÃ NHẬN nhưng tiền CHƯA ra** | hoàn tiền VN là **bất đồng bộ** | **KHÔNG** ghi sổ, `ChoXacNhan = true`, log `Warning` `[PAY-CHO-XAC-NHAN]` (`PaymentAppService.cs:203-213`) | `200` + `choXacNhan = true` | ⚠️ **(a) trạng thái bình thường** — **không** phải lỗi, **không** phải xong |
| **NCC timeout / không kết nối được** | mạng, cổng chết | `LoiHaTang` khác rỗng + `ChoXacNhan = true`; **KHÔNG rollback dữ liệu đã ghi** (không có gì được ghi); log `Error` `[PAY-KHONG-XAC-DINH]` (`VnPayPaymentProvider.cs:341-368`) | `200` + `loiHaTang` | 🚨 **(b) lỗi HẠ TẦNG — khác hẳn lỗi nghiệp vụ** |
| **Phản hồi cổng KHÔNG phải JSON** | HTML 502, trang lỗi gateway | `DocPhanHoi` **bắt** và đặt `ChoXacNhan = true` (`VnPayRequestBuilder.cs:165-185`) | như trên | 🚨 **(b)** — 🔑 ca này **đã đo được** trên sandbox không credential (`VnPayRequestBuilder.cs:177-178`) |
| **Vi phạm unique index** (`IX_PayTransactions_*`) | IPN trùng / hai luồng | PostgreSQL `23505` ⇒ exception filter tra `ViPhamRangBuocDb` | 🚨 **mã chung `eCommerce:Db:0001`** (chưa có ánh xạ riêng) | 🟠 **(b)** nhưng **thiếu mã riêng** — F-PAY-4 |
| **Vi phạm optimistic concurrency** | hai DbContext cùng sửa | ABP ném `AbpDbConcurrencyException` (do `ConcurrencyStamp`) | phụ thuộc filter | 🟠 **(b)** |
| **Lỗi runtime (deadlock, DB down)** | hạ tầng | Rollback UoW + log `Error` **có `CorrelationId`** (`XC-005` BR-002) | lỗi chung | 🚨 **(b)** |

### 12.2 🚨 Ca **NCC timeout KHÔNG được rollback dữ liệu đã ghi** — phân tích đúng theo code

| Đường | Dữ liệu ghi **trước** lời gọi cổng? | Nếu cổng timeout thì sao? | Có rollback oan không? |
|---|---|---|---|
| `HoanTienAsync` | **Không** — chỉ **đọc** trước, ghi **sau khi** `DaHoanThat = true`. 🆕 `S5`: dòng hoàn tiền cũng ghi **sau** lời gọi cổng | Không ghi gì ⇒ **không** có gì để rollback ✅ | ✅ **Đúng** |
| `PostTruyVanTrangThaiAsync` | Không ghi trước; `QueryStatusAsync` **không ném** khi lỗi (trả `Pending`) ⇒ `KhongDoi` | Không đổi gì ✅ | ✅ **Đúng** |
| 🆕 `PostDoiSoatLoatAsync` (lô, **mỗi giao dịch một UoW** — `S5`) | **Có** — ghi từng giao dịch | Giao dịch #37 ném ⇒ **chỉ** #37 bị bỏ; **36** lần ghi trước đó **VẪN CÒN** (đã commit). 📌 `SoLoi` tăng + log `[PAY-DOISOAT-LOI]`/`[PAY-DOISOAT-CO-LOI]` | ✅ **ĐÃ VÁ `F-PAY-7`** |
| 🆕 `PaymentReconciliationWorker` (**mỗi giao dịch một UoW** — `S5`) | **Có** — như trên, trần 50/lượt | Cùng cơ chế. 📌 `23505`/`23514`/`DbUpdateException` **chỉ** làm hỏng giao dịch đó; ➕ neo `[PAY-JOB-CO-LOI]` để người vận hành **thấy** thay vì đọc *"lượt xong, 0 thay đổi"* | ✅ **ĐÃ VÁ `F-PAY-7`** |

> 🔑 **Vì sao F-PAY-7 quan trọng:** kết quả đối soát là **bằng chứng về tiền**. Mất một lượt ghi nghĩa là 36 giao
> dịch **đã** được xác nhận với cổng nhưng **DB không biết** ⇒ lượt sau quét lại (đúng) — **nhưng** nếu `querydr`
> **không** trả lời được nữa (cổng chết hẳn) thì các giao dịch đó **mất dấu**. 📌 Đề xuất: **mỗi giao dịch một UoW**
> (hoặc `SaveChanges` sau từng bản ghi + bỏ `isTransactional` ở mức lô) ⇒ *"đã ghi thì còn"* — xem §15.1 **Q10**.

### 12.3 Mười hai dòng phát hiện về lỗi (11 phát hiện riêng + 1 dòng truy vết, đánh số để review)

| Mã | Phát hiện | Bằng chứng | Mức | Trạng thái sau `S4` |
|---|---|---|---|---|
| **F-PAY-1** | Xác thực IPN **không bỏ tham số rỗng**, `KiemChuKy` **có bỏ** ⇒ có thể **từ chối IPN thật** | `VnPayPaymentProvider.cs` (bản cũ) vs `VnPayRequestBuilder.KiemChuKy` | 🔴 Cao (mất tiền im lặng) | ✅ **ĐÃ ĐÓNG `S5`** — một luật duy nhất `KiemChuKyTuQueryTho` (bỏ tham số rỗng + dựng từ **query THÔ**); `TC-07/07b/07c` + probe HTTP `T16`/`T18` đo **cả hai chiều**. 🔑 Sửa xong lộ ra **lớp thứ hai** (byte của chuỗi ký) — §7.2.4 |
| **F-PAY-2** | **Không** đối chiếu `Amount` của IPN với DB (§7.2.1) | không có phép so trong module | 🟠 Trung bình | ✅ **ĐÃ ĐÓNG `S4`** — `LuatCauNoiThanhToan.DoiChieu` + ép ở **hai** chỗ: `XuLyKetQuaCongService` (so với `PaymentTransaction.Amount`) và `OrderPaymentStatusHandler` (so với `Order.GrandTotal`) |
| **F-PAY-3** | `MerchantReference` có thể rỗng mà **không** phải lỗi | `VnPayPaymentProvider.cs:170` | 🟠 Trung bình | ✅ **ĐÃ ĐÓNG `S4`** — `XuLyKetQuaCongService` **NÉM** `0006` khi `vnp_TxnRef` rỗng (không tra DB bằng `""`) |
| **F-PAY-4** | 2 unique index của `PayTransactions` **không có ánh xạ** trong `ViPhamRangBuocDb` ⇒ `23505` rơi về mã chung | grep `PayTransactions` trong `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs` = **0** | 🟠 Trung bình (CSKH nhận chỉ dẫn sai) | 🟡 **`S5` — ĐÓNG MỘT PHẦN**: đã thêm **ánh xạ** cho cả 2 index (+ 1 index của `PayRefunds` + 3 `CHECK`), và **xoá** 2 dòng khỏi danh sách trắng (lý do *"chưa có đường ghi GĐ1 chạm tới"* **hết đúng** khi có IPN). 🔒 **Mã vẫn là `eCommerce:Db:0001`** — cấp mã nghiệp vụ riêng là **thay đổi hợp đồng** ⇒ **BA** quyết (§16.4 #5) |
| **F-PAY-5** | Khoá phân tán **lệch quy ước** (`payments:` vs `payment:`) + **không** có `{tenantId}` | `PaymentAppService.cs:126` vs `PaymentReconciliationWorker.cs:59`; `XC-003` §5 | 🟠 Trung bình (vận hành) | 🟠 **nặng thêm `S4`**: khoá **mới** `payment:create:` theo đúng quy ước ⇒ nay module có **cả hai** kiểu tiền tố (2 khoá `payment*`, 1 khoá `payments*`) |
| **F-PAY-6** | Khoá job **không** theo tenant ⇒ một tenant tồn đọng chặn tenant khác | `PaymentReconciliationWorker.cs:59,91` | 🟡 Thấp (hiện 1 tenant) | ❌ chưa sửa — 📌 khoá `payment:create:` **cũng** không có `{tenantId}` (dùng `Guid`/số đơn, vốn đã duy nhất toàn hệ thống) |
| **F-PAY-7** | Lô đối soát dùng **một** UoW transactional ⇒ lỗi một giao dịch **rollback cả lượt** | `PaymentAppService` (bản cũ); `PaymentReconciliationWorker.cs:106` (bản cũ) | 🔴 Cao (mất bằng chứng) | ✅ **ĐÃ SỬA `S5`** — **mỗi giao dịch một UoW** (`requiresNew` + `isTransactional`) + `try/catch` từng vòng + **đếm `SoLoi`** ở **cả** app service lô **và** job; `Q10` đã trả lời. ⚠️ **CHƯA CÓ PHÉP ĐO:** không test/probe nào **chạy** lô đối soát với một giao dịch **cố ý hỏng** ⇒ *"đã ghi thì còn"* hiện là **code đúng theo thiết kế**, **chưa** là **số đo**. 📌 Ghi ở §16.9 #10 |
| **F-PAY-8** | `PostDoiSoatLoatAsync` chỉ cần quyền **đọc** dù **gọi cổng 100 lần** và **có thể ghi** trạng thái | `PaymentAppService.cs:327` | 🟠 Trung bình (quyền) | ❌ chưa sửa — ⚠️ **cùng họ với `TaoGiaoDichAsync`** (cũng chỉ cần `Payments` — xem §16.3 #6) |
| **F-PAY-9** | `MarkFailed` **không** trả kết quả ⇒ job **đếm sai** *"đã đổi trạng thái"* | `PaymentTransaction.cs`; `PaymentReconciliationWorker.cs` | 🔴 Cao (báo cáo sai) | ✅ **ĐÓNG HOÀN TOÀN `S5`** — `S4` sửa **hàm** (trả `bool`); **`S5` sửa chỗ ĐẾM**: job chỉ `soDoi++` **và** phát ETO khi giá trị trả về là `true` (trước đó `soDoi++` vô điều kiện) |
| **F-PAY-10** | `vnp_RequestId` sinh **mới** mỗi lần (`Guid.NewGuid`) ⇒ vi phạm tinh thần **BR-007** | `VnPayRequestBuilder.cs:82,118` | 🟠 Trung bình | ❌ chưa sửa |
| **F-PAY-11** | `Cancelled` **không ghi được** vào DB; `Pending` **không ghi được** (§3.4) | §3.4 F1/F2 | 🟠 Trung bình (mất khả năng phân tích) | ❌ chưa sửa — 📌 `S4` **cố ý không** thêm hàm `MarkPending` (cần BA trả lời `Q4`/`Q5` trước) |
| **F-PAY-12** | `PaymentAppService` **không** đối chiếu `Amount` IPN với `PaymentTransaction.Amount` — *(trùng F-PAY-2, giữ để truy vết review)* | — | — | ✅ **đóng cùng F-PAY-2** |

> 📌 **Tất cả phát hiện trên đều thuộc dạng "không có lỗi nào nổi lên"** — đúng họ lỗi dự án đang chống.
> 🔒 **Không phát hiện nào được tự sửa trong phiên viết tài liệu** (17/10/2026); ✅ **ba phát hiện** (`F-PAY-2`,
> `F-PAY-3`, `F-PAY-9`) **đã được sửa ở phiên code `S4`** (18/10/2026) — cột **Trạng thái** ở trên là nguồn sự thật
> về việc nào còn mở.
> 📌 **Bài học của chính cột này:** *"phát hiện"* và *"nợ"* là **hai** trạng thái khác nhau; nếu không có cột trạng
> thái thì 6 tháng sau không ai biết cái nào đã đóng — đúng lỗi của bảng *"CÒN"* trong `implementation-plan` §7.1.

---

## 13. Phi chức năng

| Mã | Yêu cầu | Ngưỡng đo được | Cách kiểm chứng |
|---|---|---|---|
| **NFR-PAY-001** | Hiệu năng đọc | `GET danh-sach` p95 **< 300 ms** với **10 000** giao dịch/tenant | Integration test nạp 10k dòng + đo; ⚠️ hiện **không** có index trên `CreationTime` ⇒ dự kiến **KHÔNG ĐẠT** |
| **NFR-PAY-002** | Bảo mật | **100 %** endpoint trả `401` (không token) / `403` (thiếu permission) | Test `HttpClient` thật cho **5** route (§6.2) |
| **NFR-PAY-003** | Audit | **100 %** lần đổi trạng thái có ≥ 1 log mang neo `[PAY-…]` + `WebhookReceivedCount` tăng đúng | Unit + đọc log; đã có 2 neo thật: `[PAY-DA-HOAN]`, `[PAY-CHO-XAC-NHAN]` (`PaymentAppService.cs:200,211`), `[PAY-QUERYDR-…]`, `[PAY-JOB-…]` |
| **NFR-PAY-004** | Observability | Mọi log có `CorrelationId`; **0** log nối chuỗi | `XC-005` **TC-03/TC-04**; ⚠️ `CorrelationId` **chưa** có trong code ⇒ **chưa đạt** |
| **NFR-PAY-005** | Đa tenant | Tenant A **không** đọc/ghi được dữ liệu tenant B | Integration test 2 tenant (repository của ABP tự lọc `TenantId`) |
| **NFR-PAY-006** | Độ trễ IPN | p95 **< 1 000 ms** | ⚠️ **chưa** có endpoint ⇒ **chưa đo được**; ngưỡng chính thức của VNPay **chưa kiểm chứng** |
| **NFR-PAY-007** | Che dữ liệu nhạy cảm | **0** bản ghi có PAN 12–19 chữ số trong `LastWebhookPayload` | ✅ đã có test `WebhookMaskingTests.cs:29-39`; cần thêm test **quét DB** |
| **NFR-PAY-008** | Job | nhịp **900 s**, trần **50**/lượt, `RunOnStart = true` | `PaymentReconciliationWorker.cs:49-56,76-80` — ✅ đã có; test `BackgroundWorkerCoCheTests` |
| **NFR-PAY-009** | Không ngập log | hệ thống rỗi 60 s ⇒ **< 10** dòng log từ job | `XC-005` **TC-09**; hiện job log **1** dòng `[PAY-JOB-RONG]`/lượt ⇒ ✅ |
| **NFR-PAY-010** | Metric | **< 100** series; nhãn chỉ `provider`/`reason`/`result` | `XC-005` **TC-07**; đọc `VnPayPaymentProvider.cs:83-88` + `MetricDoiSoatThanhToan.cs:56-62` |
| **NFR-PAY-011** | Chính xác tiền | Sai số **0** trên `numeric(18,4)`; `vnp_Amount` = `round(amount × 100, 0, AwayFromZero)` | ✅ test `VnPayRefundTests.cs:90-104` (gồm ca số lớn) |
| **NFR-PAY-012** | Phát hiện IPN mất | **≤ 15 phút** (nhịp job) hoặc khi người vận hành bấm | Test: chèn giao dịch `Initiated`, chờ 1 nhịp, đọc log `[PAY-JOB-THU-DUOC]` |

### 13.1 Metric đã có trong code (đăng ký thật)

| Tên metric | Kiểu | Nhãn | Nơi phát | Ý nghĩa **đọc cho đúng** |
|---|---|---|---|---|
| `payment_webhook_rejected_total` | Counter | `provider`, `reason` (`thieu-chu-ky`/`chu-ky-sai`) | `VnPayPaymentProvider.cs:83-88` | Chỉ 2 ca **không đáng tin**; chữ ký đúng mà giao dịch thất bại **KHÔNG** tính (BR-PAY-013 — nếu tính nhầm sẽ **báo động giả** mỗi lần khách huỷ) |
| `payment_recon_changed_total` | Counter | `result` (`succeeded`/`failed`) | `MetricDoiSoatThanhToan.cs:56-62` | Số ca **phát hiện và sửa**; không phát khi `KhongDoi` |
| `payment_recon_unresolved_total` | Gauge | — | `PaymentAppService.cs:312,415`; `PaymentReconciliationWorker.cs:224` | Số ca **chưa ngã ngũ**. 🚨 **Bằng số giao dịch đã xét ⇒ cấu hình sai/cổng chết**, **KHÔNG PHẢI** *"hệ thống khoẻ"* |

*(Nguồn tên metric: `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:28,55,65`; `XC-005` `eCommerceMetricNames`.)*

> ⚠️ **Chưa có nơi THU metric:** chưa có lớp xuất OTel → Prometheus (`handover:243`) ⇒ 3 metric trên **phát ra nhưng
> không ai đọc**. 🔒 Đó là **nợ quan sát**, không phải tính năng đã có.

---

## 14. Test cases & Acceptance Criteria

### 14.1 Danh sách test case

| ID | Mục tiêu | Loại | Input chính | Expected | Phủ BR/NFR | Đã có? |
|---|---|---|---|---|---|---|
| **TC-01** | Happy path IPN hợp lệ + `00`/`00` | Unit | query string đã ký đúng | `SignatureValid = true`, `IsSuccess = true` | BR-PAY-001 | ✅ `VnPayWebhookTests.cs:67` |
| **TC-02** | Sửa `vnp_Amount` sau khi ký | Unit | amount bị sửa | `SignatureValid = false` | BR-PAY-001/012 | ✅ `VnPayWebhookTests.cs:83` |
| **TC-03** | Chữ ký đúng nhưng giao dịch thất bại | Unit | `vnp_ResponseCode = 24` | `SignatureValid = true`, `IsSuccess = false` | BR-PAY-001 | ✅ `VnPayWebhookTests.cs:99` |
| **TC-04** | Thiếu `vnp_SecureHash` | Unit | không có hash | `SignatureValid = false` + metric `thieu-chu-ky` | BR-PAY-013 | ✅ `VnPayWebhookTests.cs:114,197` |
| **TC-05** | Chữ ký sai | Unit | hash sai | `SignatureValid = false` + metric `chu-ky-sai` | BR-PAY-013 | ✅ `VnPayWebhookTests.cs:212` |
| **TC-06** | Chữ ký đúng + giao dịch thất bại ⇒ **KHÔNG** tính là từ chối | Unit | như TC-03 | **0** metric tăng | BR-PAY-013 | ✅ `VnPayWebhookTests.cs:243` |
| **TC-07** | 🚨 **IPN có tham số RỖNG** (`vnp_BankCode=`) | Unit | query string có `=` rỗng | Chữ ký **phải** hợp lệ (VNPay bỏ tham số rỗng) | **F-PAY-1** | ✅ **`S5`** — `VnPayIpnChuKyTests.TC07` (**+ `TC07b` chiều NGƯỢC** và **`TC07c`** chứng minh hai cách ký cho hash khác nhau) + ✅ **probe HTTP `T16`/`T18`** |
| **TC-08** | **IPN gửi TRÙNG** cùng payload 3 lần | **Integration** | cùng IPN × 3 | **1** lần ghi; `WebhookReceivedCount = 3`; `Status` không đổi | BR-PAY-003 | ✅ **`S5` — QUA ĐƯỜNG HTTP THẬT** (`probe-thanh-toan.ps1` `T10`/`T11`/`T12`) |
| **TC-09** | IPN gửi lại **sau khi đã `Refunded`** | Unit | `RegisterRefund` 100 % rồi `MarkSucceeded` | **`false`**, `Status` vẫn `Refunded` | BR-PAY-004 | ✅ `PaymentRefundTests.cs:146` |
| **TC-10** | `MarkFailed` trên giao dịch đã `Succeeded` | Unit | — | **không** đổi trạng thái, `FailureReason` **không** ghi | BR-PAY-004 | ✅ `architecture.md:156-157` (đo được) + `PaymentTransaction.cs:129-135` |
| **TC-11** | Hoàn **vượt** số đã thu | Unit | `RefundedAmount=100`, hoàn `50`, `Amount=120` | **NÉM** `0002`, `RefundedAmount` **không** đổi | BR-PAY-007/020 | ✅ `PaymentRefundTests.cs:32` |
| **TC-12** | Hoàn **nhiều lần** cộng dồn vượt | Unit | 3 lần × 50 trên `Amount=120` | lần 3 **NÉM**; 2 lần đầu giữ nguyên | BR-PAY-007 | ✅ `PaymentRefundTests.cs:46` |
| **TC-13** | Hoàn **đúng bằng** số đã thu | Unit | `Amount=100`, hoàn `100` | `Refunded` + `ConHoanDuoc = 0` | BR-PAY-007 | ✅ `PaymentRefundTests.cs:66` |
| **TC-14** | Hai luồng **đồng thời** hoàn tiền cùng giao dịch | **Integration** | 2 request song song | **1** thành công; luồng kia `Lock:0001` **hoặc** `0002`; **1** lần gọi cổng | BR-PAY-011, NFR-PAY-002 | ❌ **CHƯA CÓ** |
| **TC-15** | Hoàn cho giao dịch **chưa thu tiền** | Unit | `Status ∈ {Initiated, Pending, Failed, Cancelled}` | **NÉM** `0003` | BR-PAY-005 | ✅ `PaymentRefundTests.cs:94` |
| **TC-16** | Số tiền hoàn `≤ 0` | Unit | `0`, `-1` | **NÉM** `0004` | BR-PAY-006 | ✅ `PaymentRefundTests.cs:119` |
| **TC-17** | Ba mã lỗi **khác nhau** | Unit | 3 ca | 3 mã **phân biệt** | BR-PAY-020 | ✅ `PaymentRefundTests.cs:132` |
| **TC-18** | Thiếu `lyDo` / `NguoiThucHien` | Unit | chuỗi rỗng | **NÉM** `0004` **trước khi** gọi cổng | BR-PAY-010 | ❌ **CHƯA CÓ ở tầng app service** |
| **TC-19** | Cổng trả mã `00` ⇒ **KHÔNG** ghi sổ | Unit | phản hồi `{"vnp_ResponseCode":"00"}` đã ký | `DaNhanYeuCau = true`, `DaHoanThat = false`, `ChoXacNhan = true` | BR-PAY-008 | ✅ `VnPayRefundTests.cs:223` |
| **TC-20** | Cổng timeout | Unit | `TaskCanceledException` | `LoiHaTang = "Timeout"`, `ChoXacNhan = true` | BR-PAY-009 | ✅ nhánh code `VnPayPaymentProvider.cs:341-354` (test ở lõi) |
| **TC-21** | Phản hồi **không** phải JSON | Unit | `"<html>502</html>"` | **không** ném, `ChoXacNhan = true`, `LoiHaTang` | BR-PAY-009 | ✅ `VnPayRefundTests.cs:295` |
| **TC-22** | Phản hồi **thiếu `HashSecret`** | Unit | `biMat = ""` | **fail-closed**: `DaHoanThat = false`, có lý do | BR-PAY-008 | ✅ `VnPayRefundTests.cs:259` |
| **TC-23** | Chữ ký **phản hồi** sai | Unit | JSON hợp lệ, hash sai | **không** tin, `DaHoanThat = false` | BR-PAY-008/012 | ✅ `VnPayRefundTests.cs:244` |
| **TC-24** | Luật đối soát: trạng thái **cuối** ⇒ `KhongDoi` kể cả cổng nói `Failed` | Unit | 3 trạng thái cuối × 4 trạng thái cổng | luôn `KhongDoi` | BR-PAY-014 | ✅ `LuatDoiSoatThanhToanTests.cs` |
| **TC-25** | Luật: `Failed` + cổng nói `Succeeded` ⇒ `GhiDaThuTien` | Unit | — | `GhiDaThuTien` | BR-PAY-014 | ✅ `LuatDoiSoatThanhToanTests.cs:85-96` |
| **TC-26** | Luật: cổng nói `Pending` ⇒ **KHÔNG LÙI** | Unit | `Pending`/`Initiated` | `KhongDoi` | BR-PAY-014/015 | ✅ `LuatDoiSoatThanhToanTests.cs:112-130` |
| **TC-27** | Lô: `SoKhongXacDinh` **tách** khỏi `SoDoiTrangThai` | **Integration** | cổng chưa cấu hình, 1 giao dịch `Initiated` | `soDaXet=1`, `soKhongXacDinh=1`, `soDoiTrangThai=0` | BR-PAY-015 | ✅ **đo thật** (`sal-006` §20.5 `C2`/`C3`) |
| **TC-28** | Job: **không** gọi app service (không `AbpAuthorizationException`) | **Integration** | chạy job thật | job **xong** (có neo `[PAY-JOB-XONG]`) | BR-PAY-021 | ✅ `BackgroundWorkerCoCheTests` + `sal-006` §21.5 |
| **TC-29** | Chống trùng DB: chèn 2 dòng cùng `(TenantId=NULL, vnpay, PAY-X)` | **Integration** | 2 `INSERT` | dòng 2 ⇒ `23505` | BR-PAY-017, NFR-PAY-005 | ✅ **ĐÃ CÓ `S4`** — `PayTransactionsChanTrungTests` (5 ca: trùng mã, trùng `IdempotencyKey`, **+ 2 chiều ngược** và độ dài cột) |
| **TC-30** | Cách ly tenant | **Integration** | tenant B đọc giao dịch tenant A | không thấy | NFR-PAY-005 | ❌ **CHƯA CÓ** (⚠️ hiện 1 tenant — cần hạ tầng test 2 tenant) |
| **TC-31** | Tuỳ chọn của IPN: `vnp_TxnRef` rỗng ⇒ coi là **lỗi dữ liệu** | Unit | IPN không có `vnp_TxnRef` | chỗ gọi **phải** từ chối | **F-PAY-3** | 🟡 **code có `S4`** (`XuLyKetQuaCongService` ném `0006`), **test chưa** (§16.4 #2) |
| **TC-32** | Chính xác tiền: `Amount` lẻ 4 chữ số × 100 | Unit | `1234.5678` | `123457` (làm tròn `AwayFromZero`) | NFR-PAY-011 | ✅ `VnPayRefundTests.cs:90` |
| **TC-33** | Quét mã nguồn: **0** phép so tiền bằng `==` | Unit (quét) | regex trên `modules/PaymentHub/**` | **0** match | BR-PAY-019 | ❌ **CHƯA CÓ** — 📌 `S4` **thêm** `decimal.Compare` ở `LuatCauNoiThanhToan`; một ca quét sẽ khoá được cả hai chỗ |
| **TC-34** | Payload lưu DB **đã che** + cắt 8000 | Unit | payload có số thẻ 16 số | 4 số cuối, phần còn lại `***` | BR-PAY-018 | ✅ `WebhookMaskingTests.cs:15-39,58` |
| **TC-35** | Không có dữ liệu ⇒ **không** phải lỗi | Unit | `merchantReference` không tồn tại | `null`, **không** ném | BR-… (§12.1) | ❌ **CHƯA CÓ** |
| **TC-36** | Sai quyền | **Integration** | token thiếu `Payments.Refund` | `403` | BR-PAY-016, NFR-PAY-002 | ❌ **CHƯA CÓ** cho route thanh toán |
| 🆕 **TC-37** | **`MerchantReference` ổn định** (gọi 2 lần ⇒ cùng mã; gốc là tiền tố; GMT+7; `Unspecified` không lệch; bỏ dấu) | Unit | 5 nhóm ca, 10 `[Fact]`/`[Theory]` dòng | cả 5 tính chất ĐẠT | BR-PAY-002 | ✅ **`S4`** — `SinhMaThamChieuTests` |
| 🆕 **TC-38** | **Guard constructor** (`amount ≤ 0`, mã/cổng rỗng) + **chiều ngược** | Unit | 5 ca | ném `0007`/`0006`; giá trị hợp lệ **tạo được** | BR-PAY-006 | ✅ **`S4`** — `PaymentTransactionTaoMoiTests` |
| 🆕 **TC-39** | **`MarkFailed` trả `bool` đúng nghĩa** | Unit | 3 ca | `true` khi **thật sự** đổi; `false` khi lặp / đã có tiền | `F-PAY-9` | ✅ **`S4`** |
| 🆕 **TC-40** | **Luật hấp thụ của trục tài chính trên đơn** (`Paid → Failed` **bị chặn**; `Refunded` hấp thụ; `Failed → Paid` cho phép) | Unit | 9 ca | trạng thái **đúng** + giá trị trả về **đúng** | BR-PAY-004 | ✅ **`S4`** — `OrderTrangThaiThanhToanTests` |
| 🆕 **TC-41** | **Cầu nối có người nhận** (reflection): handler tồn tại · ETO mang đủ trường · ETO **không** là sự kiện thông báo · setter `Order.PaymentStatus` **kín** · `IXuLyKetQuaCong` **không** phải app service · **không** tham chiếu ngược tầng | Unit (reflection) | 6 nhóm ca | tất cả ĐẠT | BR incidental (#22) | ✅ **`S4`** — `OrderPaymentStatusHandlerBatBienTests` |
| 🆕 **TC-42** | **Đối chiếu số tiền/tiền tệ** (F-PAY-2) | Unit | 7 ca | lệch 1 đồng **cả hai chiều** ⇒ `LechSoTien`; khác tiền tệ ⇒ `LechTienTe`; rỗng ⇒ VND | BR-PAY-… (§3.5) | ✅ **`S4`** — `LuatCauNoiThanhToanTests` |
| 🆕 **TC-43** | **Luật ghi nhận hoàn tiền** — 4 nhánh + **thứ tự kiểm** | Unit | 6 ca | `DaHoanThat` ⇒ `GhiDaHoan` (thắng **cả** `LoiHaTang`); `LoiHaTang` ⇒ `KhongXacDinh` (thắng `DaNhanYeuCau`); `DaNhanYeuCau` ⇒ `ChoXacNhan`; còn lại ⇒ `TuChoi`; `null` ⇒ `KhongXacDinh` | BR-PAY-008/009 | ✅ **`S5`** — `HoanTienS5Tests` |
| 🆕 **TC-44** | **Dòng hoàn tiền — entity chặn dữ liệu vô nghĩa** | Unit | 9 ca | `amount ≤ 0` ⇒ `0004`; thiếu mã/giao dịch ⇒ `0010`; thiếu lý do/người ⇒ `0004`; **chiều ngược**: dữ liệu hợp lệ **phải tạo được** và ở `ChoXacNhan`; `GhiKetQua` trả `true`/`false` đúng; **trạng thái cuối hấp thụ**; `ChoXacNhan` **không** phải cuối | BR-PAY-007/020 | ✅ **`S5`** — `HoanTienS5Tests` |
| 🆕 **TC-45** | **Hàng rào TIỀN ở DB** | **Integration** | 6 ca | trùng `(TenantId=NULL, giao dịch, RF001)` ⇒ **`23505`**; **chiều ngược** (khác giao dịch / khác số thứ tự) ⇒ **cho phép**; hoàn vượt ⇒ **`23514`**; hoàn âm ⇒ `23514`; dòng hoàn `≤ 0` ⇒ `23514`; hoàn **đúng bằng** ⇒ ghi được | BR-PAY-007, `F-PAY-7` | ✅ **`S5`** — `PayRefundsVaRangBuocTienTests` |
| 🆕 **TC-46** | **`IXuLyKetQuaCong` phải resolve được từ DI** | Unit (reflection) | 1 ca | `ExposeServices(typeof(IXuLyKetQuaCong))` **phải** có | §16.6 #2 | ✅ **`S5`** — `HoanTienS5Tests` |

**Tổng kết coverage:** **38** ca đã có (§14.1) · **6** ca **còn thiếu**: `TC-14` (hai luồng đồng thời hoàn tiền),
`TC-18` (thiếu `lyDo` ở tầng app service), `TC-30` (cách ly tenant), `TC-31` (code có, test chưa), `TC-33`
(quét mã nguồn: `0` phép so tiền bằng `==`), `TC-35` (không có dữ liệu ⇒ không ném) · `TC-36` (sai quyền).
🆕 **`S5` đóng `TC-07` + `TC-08`** — hai trong **ba** ca *"bắt buộc trước khi tuyên bố `Implemented`"*.
📌 **Ca thứ ba (`TC-14`)** vẫn mở: nó cần **hai request song song** trên PostgreSQL thật **+** một cổng giả có
độ trễ — tức là cần `FakePaymentProvider` (§7.7.2, đã chốt **dời sang `S8`**).
🔒 **Và điều kiện `Implemented` còn lại KHÔNG phải test:** `TmnCode`/`HashSecret` thật + một giao dịch thật
(§7.6). 📌 Đừng đọc *"38 ca xanh"* thành *"tiền thật đã kiểm"* — hai chuyện khác nhau.

### 14.2 Fixture skeleton (xUnit + ABP)

```csharp
public class Pay001Tests : eCommerceTestBase<eCommerceEntityFrameworkCoreTestModule>
{
    private readonly IPaymentAppService _sut;
    private readonly IRepository<PaymentTransaction, Guid> _giaoDich;

    public Pay001Tests()
    {
        _sut = GetRequiredService<IPaymentAppService>();
        _giaoDich = GetRequiredService<IRepository<PaymentTransaction, Guid>>();
    }

    // TC-08 · BR-PAY-003 — IPN gửi trùng: chỉ ghi MỘT lần, bộ đếm vẫn tăng
    [Fact]
    public async Task TC08_ipn_gui_trung_ba_lan_chi_ghi_mot_lan()
    {
        // Arrange: tạo giao dịch Initiated + payload IPN đã ký (dùng chính VnPayRequestBuilder.Ky)
        // Act: xử lý cùng payload 3 lần (qua handler IPN — CHƯA CÓ, xem §15.1 Q2)
        // Assert: Status == Succeeded · WebhookReceivedCount == 3 · chỉ 1 dòng trong PayTransactions
    }

    // TC-07 · F-PAY-1 — tham số RỖNG trong chuỗi ký
    [Fact]
    public async Task TC07_ipn_co_tham_so_rong_van_phai_hop_le()
    {
        // Arrange: query string có "vnp_BankCode=" (rỗng) + hash tính theo quy tắc VNPay
        // Act: VerifyWebhookAsync
        // Assert: SignatureValid == true  ← HIỆN NAY SẼ FAIL (F-PAY-1)
    }

    // TC-14 · BR-PAY-011 — hai luồng hoàn tiền song song
    [Fact]
    public async Task TC14_hai_luong_hoan_tien_chi_mot_lan_goi_cong()
    {
        // Integration: 2 Task song song trên PostgreSQL thật (Testcontainers)
        // Assert: 1 thành công · 1 nhận Lock:0001 hoặc 0002 · RefundedAmount tăng ĐÚNG một lần
    }

    // TC-29 · BR-PAY-017 — hàng rào DB: trùng MerchantReference khi TenantId NULL
    [Fact]
    public async Task TC29_chan_trung_MerchantReference_khi_TenantId_NULL()
    {
        // Arrange: 2 PaymentTransaction cùng (TenantId=null, "vnpay", "PAY-DUP-1")
        // Act + Assert: lần 2 ⇒ PostgresException với SqlState == 23505
    }
}
```

### 14.3 Acceptance Criteria (Definition of Done)

- [ ] Happy path (§1 Basic Flow) chạy được **qua HTTP thật** — ⚠️ cần credential sandbox (§15.1 **Q3**)
- [ ] **Mọi nhánh của state machine (§3.1, 12 dòng) đều có test** — kể cả nhánh guard sai
- [ ] Mọi **BR-PAY-001…021** ở §1 được test phủ (đối chiếu §14.1)
- [ ] Mọi **NFR-PAY-001…012** ở §13 được kiểm chứng **có số đo**
- [ ] Idempotency: IPN gửi trùng **3 lần** ⇒ 1 lần ghi; retry hoàn tiền **không** gọi cổng 2 lần
- [ ] Đồng thời: test 2 luồng song song pass; khoá `payments:refund:{id}` **chứng minh có tác dụng**
- [ ] Cách ly tenant: test 2 tenant pass
- [ ] `[Authorize]` + permission đúng; test `401`/`403` cho **cả 5** route
- [ ] Migration chạy được từ DB trống **và** trên DB đã có dữ liệu; **48** index NND **không đổi**
- [ ] `Payload` lưu DB **không** chứa PAN đầy đủ (test đọc lại từ DB)
- [ ] Toàn bộ test pass trong CI (`tools\kiem-tat-ca.ps1 -DayDu`)
- [ ] **Tài liệu này đã cập nhật khớp code** — đặc biệt 11 phát hiện ở §12.3 phải được đóng hoặc chuyển thành nợ có mã

### 14.4 Definition of Ready (trước khi bắt đầu code)

- [ ] Tài liệu đã **review và `Approved`** (hiện `Draft`)
- [ ] **11** phát hiện ở §12.3 đã được phân loại: **sửa ngay** / **nợ có mã** / **không sửa có lý do**
- [ ] **10** câu hỏi mở ở §15.1 đã trả lời (ít nhất **Q1–Q3** — chúng chặn *"bán được"*)
- [ ] 🚨 **Credential sandbox VNPay** đã có (`TmnCode` + `HashSecret`) — nếu chưa, **không** được tuyên bố `Implemented`
- [ ] `BR`/`NFR` đã có mã và kiểm chứng được (đã có: 21 BR + 12 NFR)

---

## 15. Câu hỏi mở, giả định & lịch sử thay đổi

### 15.1 Câu hỏi mở

| # | Câu hỏi | Cần ai trả lời | Trạng thái | Ảnh hưởng nếu chưa trả lời |
|---|---|---|---|---|
| **Q1** | 🚨 **Ai ghi `Order.PaymentStatus`?** `SAL-003` **BR-013** nói *"chỉ `PaymentHub`/IPN ghi"*, nhưng `PaymentHub` **không** tham chiếu `Order`, và `eCommerce.Application` **không được** tham chiếu `PaymentHub.Application` (`handover:134-135`). Cầu nối đặt ở đâu: outbox ETO (`XC-002`) hay một `IPaymentOutcomeSink` ở Domain? | **Kiến trúc trưởng + BA** | ✅ **ĐÃ TRẢ LỜI `S4` (18/10/2026) — chọn ETO** | ✅ **KHÔNG còn chặn.** Quyết định: **sự kiện phân tán** (`PaymentSucceededEto`/`PaymentFailedEto` trong `PaymentHub.Application.Contracts`) phát từ module, nhận bởi `OrderPaymentStatusHandler` ở `eCommerce.Application` ⇒ **không** tham chiếu vòng, **không** tạo nguồn sự thật thứ hai. 📌 Phương án `IPaymentOutcomeSink` ở Domain **bị loại**: nó buộc `PaymentHub.Domain` biết khái niệm *"đơn"*, hoặc buộc `eCommerce.Domain` biết khái niệm *"giao dịch"* — cả hai đều là **phụ thuộc ngược**. ⚠️ **Outbox bền vững** (phần `XC-002` của câu hỏi) **chưa làm** — xem §16.4 #1 (chủ dự án chốt: hoãn) |
| **Q2** | Đường nhận **IPN** đặt ở đâu: `src/eCommerce.Web/Controllers` (MVC, không cần token) hay `HttpApi.Host` (controller riêng)? Có phải thêm vào **allowlist công khai** theo `XC-004` **BR-004**? | Kiến trúc trưởng | ✅ **ĐÃ TRẢ LỜI `S5` (18/10/2026) — `HttpApi.Host`** | ✅ **KHÔNG còn chặn.** Đặt ở `src/eCommerce.HttpApi.Host/Controllers/VnPayIpnController.cs`, route `GET /api/webhooks/vnpay`, `[AllowAnonymous]` **cấp class** + đã duyệt trong `AllowlistCongKhaiTests`. 📌 **Lý do chọn Host** (không phải Web): ① Host là **API + auth server** — nơi đã có `PaymentHub.Application` trong đồ thị module và là **tiến trình chạy worker**; ② route trong thiết kế **đã** là `/api/webhooks/vnpay` (§4.1 + §6.2) và sequence ghi `participant API as HttpApi.Host`; ③ Web là **UI cookie-auth** — thêm một endpoint **công khai** ở đó là mở cửa ra Internet ở đúng tiến trình đang render HTML, không có lợi ích nào bù lại |
| **Q3** | 🔑 **Có cung cấp `TmnCode` + `HashSecret` sandbox không?** (nợ chặn #2 — `handover:74`) | **chủ dự án** (tự đăng ký form) | ✅ **ĐÃ TRẢ LỜI 17/10/2026** | ✅ **KHÔNG còn là chặn kỹ thuật**: VNPay cho **tự đăng ký merchant test** ([devreg](https://sandbox.vnpayment.vn/devreg/)) ⇒ `TmnCode`/`HashSecret` lấy trong **5–10 phút**, không cần hợp đồng — xem **§7.7.1 #3**. 🔑 Và `S4`/`S5` được mở khoá **ngay** bằng `FakePaymentProvider` (§7.7.2) dù **chưa** đăng ký. ⚠️ Chỉ `DoD-tiền-thật` (một giao dịch + IPN **thật**) mới cần credential **+ tunnel** (§7.7.3 C1+C2) |
| **Q4** | Có cần **phân biệt** *"khách cố ý huỷ"* (`Cancelled`) với *"giao dịch lỗi"* (`Failed`) trong DB không? (F-PAY-11) | **BA** | `Open` | 🟠 Báo cáo *"tỉ lệ khách huỷ"* **không làm được**; CSKH đọc `FailureReason` (chuỗi) để đoán |
| **Q5** | Có cần đo **thời gian chờ thanh toán** (từ lúc tạo tới lúc IPN) không? (F-PAY-11 — thiếu trạng thái `Pending`) | **BA** | `Open` | 🟡 Không có số liệu để tối ưu tỉ lệ chuyển đổi |
| **Q6** | Ai xác nhận các yêu cầu hoàn tiền đang `ChoXacNhan`? Cần **job riêng** hay dùng job `REC-001` hiện có? (D5 — hiện **không ai**) | **Kế toán + Kiến trúc trưởng** | `Open` | 🔴 Tiền **đã ra** ở cổng mà sổ **chưa** ghi ⇒ đối soát lệch vĩnh viễn; khách khiếu nại |
| **Q7** | Ngưỡng cảnh báo khi `WebhookReceivedCount > N`? Có cần metric riêng không? | **Dev + vận hành** | `Open` | 🟡 IPN lặp **im lặng** (chỉ thấy khi mở bảng) |
| **Q8** | Chốt tên khoá: đổi `payments:refund:` → `payment:refund:` và **thêm `{tenantId}`**? (F-PAY-5/6) | **Kiến trúc trưởng** (theo `XC-003`) | `Open` | 🟡 Vận hành gỡ khoá tay **dễ sai**; đa tenant bị chặn chéo |
| **Q9** | `PostDoiSoatLoatAsync` cần quyền gì? `Payments.Refund` hay **thêm** `Payments.Reconcile`? (F-PAY-8) | **BA + Kiến trúc trưởng** | `Open` | 🟠 Người chỉ *"xem"* có thể **tiêu quota cổng** và **ghi trạng thái** |
| **Q10** | Lô đối soát: **mỗi giao dịch một UoW** hay giữ **một** UoW cho cả lượt? (F-PAY-7) | **Kiến trúc trưởng** | ✅ **ĐÃ TRẢ LỜI `S5` (18/10/2026) — MỖI GIAO DỊCH MỘT UoW** (chủ dự án chốt trong phiên) | ✅ **KHÔNG còn chặn.** Áp ở **cả hai** đường: `PostDoiSoatLoatAsync` (`[UnitOfWork(isTransactional: false)]` + `requiresNew` từng vòng) và `PaymentReconciliationWorker` (bỏ UoW bao cả lượt). ➕ `try/catch` **từng vòng** + **đếm `SoLoi`** ở cả hai — 📌 nếu không đếm thì lỗi lại **im lặng**: lượt "xong", chỉ có ít thay đổi hơn mong đợi. 🔒 Giá trị đạt được: câu *"đã ghi thì còn"* trở thành **sự thật của DB**, không phải lời hứa của comment |
| **Q11** | `vnp_TransactionDate` gửi **ngày giao dịch gốc** hay *"now"*? Có thêm cột `ProviderCreateDateGmt7` (H2 của `REC-001`) ngay trong `PAY-001` không? | **VNPay** (xác nhận) + Dev | `Open` | 🔴 Hoàn tiền giao dịch cũ có thể bị cổng từ chối *"không tìm thấy giao dịch"* |
| **Q12** | Chuỗi ký của API giao dịch (`refund`/`querydr`) dùng **`&` + URL-encode** (code) hay **`\|` không encode** (`REC-001` §7.3 dẫn tài liệu)? | **VNPay** + Dev | `Open` | 🔴 Nếu code sai ⇒ **mọi** lệnh hoàn tiền/truy vấn bị `Invalid Checksum`; **chưa kiểm chứng được** vì thiếu credential |

### 15.2 Giả định

| # | Giả định | Nếu giả định **SAI** thì sao |
|---|---|---|
| **G1** | VNPay **bỏ tham số rỗng** khỏi chuỗi ký (như `VnPayRequestBuilder.cs:254-255` nói) | Nếu VNPay **giữ** tham số rỗng: `KiemChuKy` sẽ **từ chối phản hồi thật** ⇒ hoàn tiền **luôn** thất bại; ngược lại nếu VNPay **bỏ** mà `VerifyWebhookAsync` **giữ** ⇒ IPN thật bị **từ chối** (F-PAY-1) |
| **G2** | Mỗi lần thu tiền ở VNPay có **`vnp_TransactionNo` duy nhất** ⇒ dùng làm `IdempotencyKey` | Nếu cổng cấp trùng: unique index **chặn** dòng thứ hai ⇒ giao dịch **không** được ghi ⇒ **tiền vào mà sổ không biết** |
| **G3** | `Amount` trong IPN là **số tiền thật đã thu**, đơn vị VND ×100 | Nếu cổng trả đơn vị khác: ta chia 100 sai ⇒ đối chiếu lệch (F-PAY-2 chưa có phép so ⇒ **không phát hiện**) |
| **G4** | Thời điểm giao dịch phải là **GMT+7** khi ký (`VnPayPaymentProvider.cs:96`) | Nếu cổng dùng UTC: `vnp_CreateDate`/`vnp_TransactionDate` lệch **7 tiếng** ⇒ tra giao dịch **có thể** sai ngày |
| **G5** | Truy cập DB qua `IRepository` của ABP ⇒ **luôn** lọc `TenantId` | Nếu ai đó dùng `DbContext` trực tiếp hoặc bỏ `IMultiTenant`: **rò dữ liệu chéo tenant**; hàng rào cuối là `NFR-PAY-005` + test TC-30 |
| **G6** | "Không có credential" là **tạm thời**, không phải thiết kế | Nếu chủ dự án **không** cấp credential trong GĐ1: phải **hạ** trạng thái tài liệu và **ghi rõ** trên bàn giao là *"chưa kiểm chứng tiền thật"* — như hiện nay |

### 15.3 Nợ / việc chưa kiểm chứng (tổng hợp — **không** phải tính năng đã có)

| # | Nợ | Ảnh hưởng |
|---|---|---|
| 1 | ~~**Không có đường TẠO `PaymentTransaction`**~~ | ✅ **ĐÓNG `S4`** — `PaymentAppService.TaoGiaoDichAsync` + HTTP `POST /api/app/payment/tao-giao-dich` |
| 2 | ~~**Không có endpoint IPN**~~ | ✅ **ĐÓNG `S5`** — `GET /api/webhooks/vnpay` (`VnPayIpnController`, `[AllowAnonymous]` + allowlist). 🚨 **Lỗi THẬT tìm thấy khi nối:** `IXuLyKetQuaCong` **không resolve được từ DI** (ABP đăng ký `XuLyKetQuaCongService` theo **tên lớp**, không theo hợp đồng) ⇒ request IPN đầu tiên trả **`HTTP 500`** — xem §16.6 #2 |
| 3 | ~~**Không có cầu nối `PaymentTransaction` → `Order.PaymentStatus`**~~ | ✅ **ĐÓNG `S4`** — ETO + `OrderPaymentStatusHandler` + `Order.DatTrangThaiThanhToan`; ➕ **`S5`** thêm nhánh **hoàn tiền** (`PaymentRefundedEto`) |
| 4 | ~~**Không có bảng dòng hoàn tiền**~~ | ✅ **ĐÓNG `S5`** — bảng `PayRefunds` (§5.7) + ghi dòng cho **mọi** kết quả + `CHECK`/unique index trên PostgreSQL thật |
| 5 | **ETO đã có, OUTBOX thì CHƯA** | Sự kiện phát **in-process trong cùng UoW**; mất sự kiện nếu tiến trình chết **sau** commit (§16.4 #1) |
| 6 | **Chưa kiểm chứng tiền thật ra** (credential rỗng) | Mọi tuyên bố "hoàn tiền hoạt động" **chưa có cơ sở**. 🔒 `S5` kiểm được **HMAC-SHA512 thật** và **đường IPN thật**, nhưng **không** kiểm được *"tiền đã ra"* — hai thứ khác nhau |
| 7 | ~~**Chưa đo mã HTTP thật** của `/api/app/payment/*`~~ | ✅ **ĐÓNG `S5`** cho 3 route đo được: `tao-giao-dich` = `200`; `hoan-tien` khi hoàn vượt = **`403`** + `0002` (**không** phải `400` — ABP mặc định `403` cho `BusinessException`); `/api/webhooks/vnpay` = `200`/`400`/`400`. ⚠️ **Chưa** đo `theo-ma`, `danh-sach`, `truy-van-trang-thai`, `doi-soat-loat` (không nằm trong phạm vi `S5`) |
| 8 | **Chưa có test** cho `TC-14, TC-18, TC-30, TC-31, TC-33, TC-35, TC-36` | ✅ **`S5` đóng `TC-07`, `TC-08`** (+ `TC-07b/07c`) ⇒ còn **6** khoảng trống (`TC-11/12/13` đã có từ trước) |
| 9 | **Chưa có metric cảnh báo** khi `WebhookReceivedCount` cao | IPN lặp im lặng (vẫn mở — `Q7`) |
| 10 | **Chưa có nơi thu metric** (OTel → Prometheus) | 3 metric phát ra **không ai đọc** |
| 11 | ~~**`PaymentRefundedEto` chưa có** ⇒ đơn **chưa** nhận `PartiallyRefunded`/`Refunded`~~ | ✅ **ĐÓNG `S5`** — `PaymentRefundedEto` + handler + luật thuần `PhanLoaiHoan`. ⚠️ **Chưa** chạy **end-to-end với đơn thật** (cần hoàn tiền **thật** ⇒ cùng nợ #6) |

### 15.4 Lịch sử thay đổi

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.1 | 17/10/2026 | *agent (phiên 17/10/2026)* | **Bản nháp đầu — tài liệu ĐẦU TIÊN sở hữu module `PaymentHub`**. Nội dung: đọc **14 tệp** code thật của module + `eCommerceDbContext`, migration, test, `appsettings`, Host module, permission provider ⇒ **mô tả đúng code**; **state machine 12 dòng** với **`Cancelled`/`Pending` là trạng thái CHẾT** (§3.4); Data Dictionary **bảng thật `PayTransactions`** (24 cột, **3 index thật**, cả 2 unique index có **`NULLS NOT DISTINCT`** — khẳng định **con số 48 KHÔNG đổi**); **§7** IPN: HMAC-SHA512 + **11 phát hiện** (F-PAY-1…12, nổi bật **F-PAY-1** *từ chối IPN thật khi có tham số rỗng* và **F-PAY-7** *rollback cả lô đối soát*); **§8** *tổng hoàn ≤ đã thu* + **2 khoá phân tán** nêu **tên thật** (`ecommerce:payments:refund:{guid}`, `ecommerce:payment:reconciliation-rec001`) kèm phát hiện **lệch quy ước `XC-003`**; **§12** phân biệt **(a) không có dữ liệu** với **(b) lỗi thật** + phân tích *NCC timeout **không** rollback dữ liệu đã ghi*; **§14** **36 test case** (26 đã có, 10 còn thiếu); **§15** **12 câu hỏi mở** (Q1–Q3 **chặn**). 🚨 **Ghi rõ: đường tiền thật CHƯA kiểm chứng** (`TmnCode`/`HashSecret` **rỗng**). **Không sửa một dòng code nào** — phiên này chỉ tạo tài liệu |
| **v0.2** | **18/10/2026** | *agent (phiên `S4`)* | **CẬP NHẬT THEO CODE THẬT SAU `S4`** (phiên code, không phải phiên tài liệu). Đóng **3 phát hiện** (`F-PAY-2` đối chiếu số tiền · `F-PAY-3` mã rỗng · `F-PAY-9` `MarkFailed` trả `bool`), đóng **2 nợ** (§15.3 #1 đường tạo giao dịch · #3 cầu nối `Order.PaymentStatus`), **trả lời `Q1`** (cầu nối đi qua **ETO**, không `IPaymentOutcomeSink`). Thêm **§16** — *bản đồ triển khai* gồm số đo thật + danh sách việc **CHƯA làm**. ➕ `NND` **51 không đổi** (không thêm index/bảng ⇒ **không** có migration ở `S4`). ⚠️ **Vẫn KHÔNG đánh dấu `Implemented`**: chưa có endpoint IPN, chưa kiểm chứng tiền thật, outbox chưa có |
| **v0.3** | **18/10/2026** | *agent (phiên `S5`)* | **PHẦN B: IPN + HOÀN TIỀN — CẬP NHẬT THEO CODE THẬT.** ✅ Endpoint **`GET /api/webhooks/vnpay`** (`[AllowAnonymous]` + allowlist); ✅ **vá `F-PAY-1`** và phát hiện **lớp thứ hai** của nó (**byte chuỗi ký** — §7.2.4); ✅ **fail-closed khi `HashSecret` rỗng** (§7.2.5); ✅ **bảng `PayRefunds`** (§5.7) + **3 `CHECK` bất biến TIỀN**; ✅ **`PaymentRefundedEto`** + handler; ✅ **vá `F-PAY-7`** (**mỗi giao dịch một UoW**, `Q10` đã chốt); ✅ **đóng nốt `F-PAY-9`** (phía **đếm** của job); ✅ **`F-PAY-4` một phần**. ➕ **`Q2` trả lời** (`HttpApi.Host`). ➕ `NND` **51 → 52**, **1 migration**, `schema.sql` regenerate. ➕ **5 phát hiện mới** (§16.6) — nặng nhất: **`IXuLyKetQuaCong` không resolve được từ DI** ⇒ IPN trả `500`. ➕ Cổng **⑪** mới (probe HTTP **26 ca**). ⚠️ **Vẫn KHÔNG `Implemented`**: chưa kiểm chứng **tiền thật**, outbox chưa có, `Q6` (ai xác nhận dòng `ChoXacNhan`) còn mở |

---

## 16. TIẾN ĐỘ TRIỂN KHAI — phiên `S4` (18/10/2026) và `S5` (18/10/2026)

> 📌 Mục này **chỉ** ghi cái **đã đo được** và cái **còn thiếu**. 🔒 Nó **không** nói *"PAY-001 xong"*: tài liệu
> này vẫn **KHÔNG** được đánh dấu `Implemented` — lý do ở §16.4 #1 (outbox) và §7.6 (**chưa kiểm chứng tiền thật**).
> 📌 `S5` = **phần B** (IPN + hoàn tiền); phần `S4` (đường TẠO + cầu nối) giữ nguyên bên dưới làm mốc.

### 16.6 🆕 Năm phát hiện của `S5` — cả năm đều **không** làm đỏ cổng nào trước đó

| # | Phát hiện | Bằng chứng (đo được) | Vì sao đáng ghi |
|---|---|---|---|
| **S5-1** | 🚨 **Job đối soát phát hiện tiền đã vào nhưng KHÔNG phát sự kiện** ⇒ *"lưới an toàn cứu được TIỀN, không cứu được ĐƠN"* | `grep PaymentEventPublisher` trong `src/eCommerce.Application` = **0** (trước `S5`); worker ghi `MarkSucceeded` rồi **thôi** | Đúng câu §7.1 cảnh báo, nhưng ở **chính đường lưới an toàn**. ✅ Sửa: hợp đồng `IPaymentEventPublisher` ở `Application.Contracts` (luật phân tầng #8 **cấm** `eCommerce.Application` tham chiếu `PaymentHub.Application`) ⇒ job phát được **cùng** sự kiện |
| **S5-2** | 🚨 **`IXuLyKetQuaCong` KHÔNG resolve được từ DI** ⇒ request IPN đầu tiên trả **`HTTP 500`** | `Cannot resolve parameter 'eCommerce.PaymentHub.IXuLyKetQuaCong xuLy'` — đo ở lượt chạy **đầu** của `probe-thanh-toan.ps1` | Nguyên nhân: ABP đăng ký `ITransientDependency` theo **tên lớp** (`IXuLyKetQuaCongService`), **không** theo `IXuLyKetQuaCong`. 📌 `S4` khai hợp đồng mà **không ai gọi** ⇒ lỗi **không lộ**. ✅ `[ExposeServices(typeof(IXuLyKetQuaCong))]` + **test reflection** khoá lại |
| **S5-3** | 🚨 **`HashSecret` rỗng vẫn tính HMAC** (khoá rỗng) ⇒ ai cũng **ký được** IPN giả | `appsettings.json:24` = `""`; trước `S5` `VerifyWebhookAsync` **không** có nhánh kiểm khoá | Chỉ **vô hại khi chưa có endpoint**; thành **lỗ hổng thật** ngay khi `/api/webhooks/vnpay` lên sóng ⇒ **giao hàng miễn phí**. ✅ fail-closed + cảnh báo lúc khởi động (§7.2.5) |
| **S5-4** | **`F-PAY-1` có HAI lớp, không một** — lớp thứ hai là **byte của chuỗi ký** (decode rồi encode lại ⇒ `+` ⇒ `%20`) | §7.2.4 + test `IPN_co_dau_CONG_thay_cho_khoang_trang__van_hop_le` | Bản mô tả cũ chỉ thấy **tham số rỗng** ⇒ nếu chỉ sửa biểu hiện đó thì **biểu hiện thứ ba** sẽ xuất hiện. ✅ Gộp thuật toán về **một** hàm |
| **S5-5** | **Dọn probe `like 'K9S5%'` KHÔNG khớp** dòng nào (mã probe nằm **giữa** `MerchantReference`) ⇒ rác ở lại ⇒ ca *"mã ổn định"* ĐỎ vì **lần thử = 3** | Lượt chạy đầu: `MA = PAY20261018-K9S5-DON-01-3` | 📌 Bài học `README-probe` §25.5 lần thứ **N**: *"dọn không thành"* và *"sản phẩm sai"* **trông giống hệt nhau**. ✅ `like '%K9S5%'` |

> 🔑 **Cách đọc mục này:** **không** phát hiện nào làm đỏ build, đỏ unit test hay đỏ cổng DB. Hai cái đầu chỉ lộ
> khi **gọi HTTP thật**; cái thứ ba lộ khi **đọc cấu hình**; cái thứ tư lộ khi **viết test đối chứng**; cái thứ năm
> lộ khi **phép dọn dẹp không khớp**. ⇒ Đúng lý do `S5` thêm **bước ⑪** (probe HTTP) vào cổng.

### 16.7 Đã làm ở `S5` (kèm bằng chứng)

| # | Việc | Tệp | Bằng chứng |
|---|---|---|---|
| 1 | **Endpoint IPN `[AllowAnonymous]`** (nợ §15.3 #2, `Q2`) | `src/eCommerce.HttpApi.Host/Controllers/VnPayIpnController.cs` + allowlist | ✅ **HTTP THẬT** (`probe-thanh-toan.ps1`): chữ ký đúng ⇒ `200`/`RspCode 00`; **sai chữ ký ⇒ `400`**/`RspCode 97` **và DB không đổi** (`T13`–`T15`); sai chữ ký **không** chạm DB ⇒ `WebhookReceivedCount` giữ nguyên |
| 2 | **Idempotency qua HTTP** (`TC-08`) | `XuLyKetQuaCongService` (đã có từ `S4`) + endpoint | ✅ IPN gửi **3 lần** ⇒ **1** dòng, `Status` không đổi, `WebhookReceivedCount = 3` (`T10`–`T12`) |
| 3 | 🚨 **Vá `F-PAY-1`** + lớp thứ hai | `VnPayRequestBuilder.KiemChuKyTuQueryTho` (**một** luật) | ✅ `TC07`/`TC07b`/`TC07c` (unit, **cả hai chiều** + bằng chứng hash khác nhau) + **HTTP**: `T16` (có tham số rỗng ⇒ `200`) và `T18` (ký kiểu cũ ⇒ `400`) |
| 4 | 🚨 **Fail-closed khi `HashSecret` rỗng** (`S5-3`) | `VnPayPaymentProvider.VerifyWebhookAsync` + `eCommerceHttpApiHostModule.KiemTraCauHinhIpn` | ✅ unit (metric `thieu-hashsecret` + `SignatureValid = false`) + `Warning` khởi động `[PAY-IPN-CHUA-CAU-HINH]` |
| 5 | **Bảng dòng hoàn tiền** (nợ §15.3 #4) | `PaymentRefund` + migration `S5_PayRefunds_VaRangBuocHoanTien` | ✅ **6 ca** integration trên PostgreSQL thật (`TC-45`) + `PayRefunds` vào nhóm ① của `kiem-db.ps1` |
| 6 | **`CHECK` bất biến TIỀN** | `CK_PayTransactions_RefundedAmount_KhongAm` / `…_KhongVuotAmount` / `CK_PayRefunds_Amount_Duong` | ✅ `23514` **đo được** (`TC-45`), cả **chiều ngược** (hoàn đúng bằng ⇒ ghi được) |
| 7 | **Luật ghi nhận hoàn tiền (hàm thuần)** | `LuatGhiNhanHoanTien` + `LuatCauNoiThanhToan.PhanLoaiHoan` | ✅ **11 ca** — 4 nhánh + **thứ tự kiểm** (`DaHoanThat` thắng `LoiHaTang`; `LoiHaTang` thắng `DaNhanYeuCau`) |
| 8 | **`PaymentRefundedEto` + cầu nối** (nợ §15.3 #11) | `PaymentRefundedEto` · `PaymentRefundedEto` handler ở `OrderPaymentStatusHandler` | ✅ **7 ca** (handler tồn tại · không phải `INotificationEvent` · ETO mang đủ trường · **decimal** cho tiền · khoá chống trùng riêng); ⚠️ **chưa** end-to-end với đơn thật (§16.4 #2) |
| 9 | 🚨 **Vá `F-PAY-7`** (`Q10`) | `PostDoiSoatLoatAsync` + `PaymentReconciliationWorker` | ⚠️ **code đã sửa, CHƯA đo:** **mỗi giao dịch một UoW** + `try/catch` + **đếm `SoLoi`**; log `[PAY-DOISOAT-CO-LOI]` / `[PAY-JOB-CO-LOI]`. 🔒 **Không có phép đo nào chạy lô đối soát với giao dịch cố ý hỏng** ⇒ xem §16.9 #10 — 📌 *"sửa xong"* và *"chứng minh xong"* là **hai** việc |
| 10 | **Đóng nốt `F-PAY-9`** (phía **đếm**) | `PaymentReconciliationWorker` | ✅ chỉ `soDoi++` **và** phát ETO khi `MarkSucceeded`/`MarkFailed` trả `true` |
| 11 | **Job đối soát phát sự kiện** (`S5-1`) | `IPaymentEventPublisher` (Contracts) + worker | ✅ hợp đồng ở tầng thấp ⇒ `eCommerce.Application` dùng được **mà không** vi phạm luật phân tầng #8 |
| 12 | **`F-PAY-4` (một phần)** | `ViPhamRangBuocDb` + danh sách trắng | ✅ 2 index của `PayTransactions` (+`PayRefunds`+3 `CHECK`) **đã ánh xạ**; **xoá** 2 dòng khỏi danh sách trắng vì lý do cũ đã sai |
| 13 | **Bước ⑪ của cổng** | `tools/probe-thanh-toan.ps1` (26 ca) + `kiem-tat-ca.ps1` | ✅ `-DayDu` chạy bước này; `PayRefunds` thêm vào bất biến "phải rỗng" |

### 16.8 Số đo thật — TRƯỚC và SAU `S5`

| Chỉ số | Trước `S5` (đo 18/10/2026 đầu phiên) | Sau `S5` | Δ |
|---|---:|---:|---|
| Unit test (`eCommerce.Tests`) | **1294** | **1347** | **+53** |
| Integration test (PostgreSQL thật) | **51** | **57** | **+6** |
| Khối mermaid | 213 · 0 lỗi | 213 · 0 lỗi | 0 (sửa nội dung, **không** thêm sơ đồ) |
| Unique index `NULLS NOT DISTINCT` | **51** | **52** | **+1** (`IX_PayRefunds_…`) |
| `CHECK` có tên trên 2 bảng thanh toán | 0 | **3** | +3 |
| Migration | 38 | **39** | +1 (`S5_PayRefunds_VaRangBuocHoanTien`) |
| Probe HTTP thật của thanh toán | 0 ca | **26 ca** | +26 (bước ⑪) |
| Cổng `-DayDu` | `exit 0` · **204.4s** | xem `SESSION-KICKOFF` §3 (ghi sau khi chạy cuối phiên) | — |

### 16.9 Việc CÒN LẠI của `PAY-001` (sau `S5` — ghi để phiên sau không phải đoán)

| # | Việc còn lại | Vì sao chưa làm | Phiên |
|---|---|---|---|
| 1 | 🚨 **Kiểm chứng tiền THẬT** — `TmnCode`+`HashSecret` sandbox, một giao dịch và một IPN **thật** | Cần **credential** + tunnel (C1+C2 của §7.7.3). 🔒 `S5` kiểm được **HMAC thật** nhưng **không** kiểm được *"tiền đã ra"* | chủ dự án |
| 2 | ✅ **`S8` — ĐÃ LÀM:** `FakePaymentProvider` + trang giả lập `/pay-gia-lap` | 🔑 Chủ dự án chốt trong `S5`: **dời sang `S8`** — probe tự ký HMAC bằng **biến môi trường** nên cổng ra của `S5` **không** cần nó; trang giả lập chỉ cần khi có **UI checkout** | ✅ `S8` (xem §16.11) |
| 3 | **Xác nhận các dòng hoàn tiền `ChoXacNhan`** (`Q6`) | VNPay hoàn tiền **bất đồng bộ** ⇒ *"cổng đã nhận"* **≠** *"tiền đã ra"*. `S5` **ghi được** dòng chờ xác nhận nhưng **chưa có đường** xác nhận bằng `querydr` (cần **BA** chốt ai xác nhận — `Q6` vẫn `Open`) | `S7` + BA |
| 4 | **Outbox bền vững** (`XC-002`) | Nợ đã ghi từ `S4` (chủ dự án chốt hoãn) | `S10` |
| 5 | **Cấp mã nghiệp vụ riêng** cho 2 index của `PayTransactions` (`F-PAY-4` phần còn lại) | Là **thay đổi hợp đồng** với client ⇒ BA quyết (`D6`: code là nguồn sự thật) | BA |
| 6 | **Quyền cho `TaoGiaoDichAsync`** (chỉ cần quyền **đọc** mà lại **tạo giao dịch**) | Cùng họ `F-PAY-8`; thêm permission = đổi hợp đồng quyền ⇒ **không tự chọn** | `S7` + BA |
| 7 | **Test còn thiếu**: `TC-14` (hai luồng hoàn tiền song song), `TC-18`, `TC-30`, `TC-31`, `TC-33`, `TC-35`, `TC-36` | `TC-14` cần `FakePaymentProvider` (dời `S8`); `TC-30` cần hạ tầng 2 tenant | `S8`/`S10` |
| 8 | ✅ **`S8` — ĐÃ LÀM:** UI thanh toán (`/DatHang` → tạo giao dịch → cổng → `/DatHang/KetQua`) | Phạm vi `S8` | ✅ `S8` (xem §16.11) · ⚠️ **chỉ** ở môi trường dev với cổng **giả lập** (chưa có credential) |
| 9 | **Cột `ProviderCreateDateGmt7`** + `vnp_RequestId` **ổn định** (§5.6 #2, `F-PAY-10`) | Không có credential ⇒ **không kiểm chứng được**; sửa mù là đổi hành vi tiền mà không đo được | khi có credential |
| 10 | 🚨 **`F-PAY-7` CHƯA CÓ PHÉP ĐO** — code đã sửa nhưng **không** có test/probe nào chạy lô đối soát với một giao dịch **cố ý hỏng** | Cần dựng một tình huống lỗi DB **giữa lô** (ví dụ chèn trước một dòng vi phạm `CHECK` để `SaveChanges` ném ở giao dịch #k) — 📌 khả thi nhưng **ngoài** 3 cổng ra của `S5`; và làm ẩu thì phép đo **không chứng minh** được gì (`README-probe` §C: *tách phép đo nhỏ nhất*) | `S10` (hoặc `S7` khi làm UI đối soát) |

### 16.10 Nợ kỹ thuật ĐÃ GHI (không phải tính năng đã có)

| # | Nợ | Vì sao chọn thế | Hệ quả nếu để lâu |
|---|---|---|---|
| 1 | 🚨 **Outbox bền vững (`XC-002`) CHƯA có** — sự kiện phát **in-process, trong cùng UoW** | 📌 Đo được: `eCommerceDbContext` **không** implement `IHasEventOutbox`; gói `Volo.Abp.EventBus.EntityFrameworkCore` **KHÔNG có** trong cache NuGet của máy ⇒ cài outbox sẽ **đổi cách phân phối MỌI sự kiện** của hệ thống (rủi ro làm đỏ E2E/probe của `S1`–`S4`) — **chủ dự án chốt: hoãn** | Tiến trình chết **sau** commit ⇒ sự kiện mất ⇒ **đơn `Unpaid` trong khi tiền đã vào**. 🔑 Giảm nhẹ: `querydr` (job 15 phút) phát lại sự kiện khi phát hiện tiền đã vào — ✅ **`S5` đã nối** đường này (`S5-1`), ⚠️ nhưng chỉ khi việc đối soát **chạy** |
| 2 | **Chưa chạy end-to-end `PaymentRefundedEto → handler → Order.PartiallyRefunded/Refunded` với đơn THẬT** | Sự kiện chỉ phát khi `RegisterRefund` chạy ⇒ cần **hoàn tiền thật** (credential) | Luật thuần (`PhanLoaiHoan`) + handler đã có test reflection; ⚠️ **"keo dính"** (nạp đơn → so tiền → ghi) **chưa** được máy kiểm ⇒ 📌 đây là giới hạn **phải nói rõ**, không được đọc thành *"đã kiểm"* |
| 3 | **`XuLyKetQuaCongService` chưa có unit test trực tiếp** (cần `IRepository` thật) | Test của dự án là **thuần**; đường HTTP thật đã phủ (probe) nhưng **không** phủ nhánh `MarkFailed` (IPN thất bại) | 🔒 Rủi ro thật: nhánh *"cổng báo thất bại"* **chưa** được đo qua HTTP. 📌 Đường ghi đã được tách ra hàm thuần có test (`MarkFailed`), chỉ **thứ tự gọi** là chưa |
| 4 | **`Q6` — không ai xác nhận dòng hoàn tiền `ChoXacNhan`** | Cần BA (ai xác nhận: kế toán? job?) | Tiền **đã ra** ở cổng mà sổ vẫn `ChoXacNhan` ⇒ đối soát lệch **vĩnh viễn** cho tới khi có người xác nhận. ➕ `S5` **đã** để lại **bằng chứng dữ liệu** (dòng hoàn + trạng thái) nên câu hỏi *"có bao nhiêu lần hoàn đang chờ"* **trả lời được** bằng SQL — đó là điều kiện **cần** để xử lý |
| 5 | **Mã lỗi chung `eCommerce:Db:0001`** cho 2 index của `PayTransactions` (`F-PAY-4` phần còn lại) | Cấp mã riêng = đổi **hợp đồng** với client ⇒ **BA** quyết | CSKH đọc *"dữ liệu vi phạm ràng buộc"* thay vì *"giao dịch này đã được ghi rồi"*. ✅ `S5` **đã** thay bằng thông điệp **đúng chuyện gì xảy ra** + chỉ dẫn |

### 16.11 🆕 `S8` (18/10/2026) — THANH TOÁN TRÊN UI + CỔNG GIẢ LẬP

> 📌 Mục này **chỉ** ghi cái **đo được** và cái **còn thiếu**. 🔒 `PAY-001` **vẫn KHÔNG** `Implemented`:
> **chưa kiểm chứng tiền thật** (§7.6) và **outbox bền vững chưa có** (§16.10 #1).
> 📌 `S8` = **phần C** (UI + cổng giả lập); phần A (`S4`) và B (`S5`) giữ nguyên bên dưới làm mốc.

**Chuỗi đo được (end-to-end, `kiem-e2e.ps1` ca `S8-P1…P14`):**

```
POST /DatHang (Thanh toán ngay)  → 302
   → GET /pay-gia-lap/{maThamChieu}          (3 nút, số tiền = GrandTotal 110.000)
   → POST /pay-gia-lap/{maThamChieu} ketQua=thanh-cong
        · dựng query vnp_* · KÝ HMAC-SHA512 (VnPayRequestBuilder — code thật)
        · GET http://localhost:5000/api/webhooks/vnpay?…   ← ENDPOINT THẬT, HTTP thật
        · IPN 200 {"RspCode":"00"} → DB: PayTransactions.Status = Succeeded
   → 302 /DatHang/KetQua?ma=…               (trạng thái ĐỌC TỪ DB)
   → OrdOrders.PaymentStatus = Paid (2)        qua ETO (cầu nối, không cùng transaction)
```

| # | Việc | Tệp | Bằng chứng |
|---|---|---|---|
| 1 | **`FakePaymentProvider`** (dev-only, uỷ quyền kiểm chữ ký cho VNPay) + chọn provider theo `PaymentHub:Provider` + **fail-fast** khi `Fake` ngoài Development | `Providers/FakePaymentProvider.cs` · `Providers/FakePaymentOptions.cs` · `PaymentHubApplicationModule.cs` | unit (module vẫn dựng được) · E2E `S8-P3/P4` (đi qua cổng giả lập) |
| 2 | **Trang giả lập `/pay-gia-lap/{ma}`** — 3 nút; **ký HMAC thật**; gọi **IPN thật**; hiện lỗi IPN thay vì "giả thành công" | `src/eCommerce.Web/Controllers/PayGiaLapController.cs` | E2E `S8-P4…P8` · 🔑 **đo chiều ngược**: đổi khoá ký ⇒ `P7`/`P8`/`P9`/`P10`/`P13`/`P14` **ĐỎ** (xem dưới) |
| 3 | **`/DatHang` tạo giao dịch + chuyển cổng** (ô *Thanh toán ngay*, chỉ hiện khi có `Payments.Default`) | `Controllers/DatHangController.cs` + `Views/DatHang/Index.cshtml` | E2E `S8-P1` (ô hiện) · `S8-P3` (`302` sang cổng) · `S8-P5` (giao dịch `Initiated` **trước** khi bấm) |
| 4 | **Trang kết quả `/DatHang/KetQua`** — trạng thái từ DB, hiện **số lần nhận IPN** | `Controllers/DatHangController.KetQua` + `Views/DatHang/KetQua.cshtml` | E2E `S8-P7/P8` (`ĐÃ THANH TOÁN`) · `S8-P11` (`?vnp_ResponseCode=00` **không** đánh lừa được) |
| 5 | **Cấu hình dev** cho **cả hai** tiến trình (một khoá ký, hai phía) | `src/eCommerce.Web/appsettings.Development.json` · `src/eCommerce.HttpApi.Host/appsettings.Development.json` | E2E `S8-P9` (DB `Succeeded`) — chữ ký **khớp** giữa hai tiến trình |
| 6 | **Bước E2E khởi động `HttpApi.Host`** (:5000) — tiền đề của end-to-end | `tools/kiem-e2e.ps1` (bước ③b) | Host lên + tắt trong `finally`; chạy lần 2 **không** bị "cổng 5000 bận" |

**Số đo thật (`S8`):**

| Chỉ số | Trước `S8` | Sau `S8` | Δ |
|---|---:|---:|---|
| Unit test | **1395** | **1399** | **+4** (`StorefrontCongKhaiTests`) |
| Integration test | 71 | 71 | 0 (không đổi lược đồ) |
| E2E (HTML thật) | **102** | **123** | **+21** (`S8-W1…W6` = 7 · `S8-P1…P14` = 14) |
| Migration | 41 | **41** | 0 — 🚨 **không** có thay đổi lược đồ nào |
| `NND` · quyền · bất biến DB | 53 · 62 · 25 | **53 · 62 · 25** | 0 |
| Ca test cổng / probe HTTP | 24 · 86 chỗ quét | **24 · 86** | 0 (không thêm probe/route công khai nào) |
| Tệp/dòng code | 350 / 43.380 | **354 / 44.288** | +4 tệp / +908 dòng (chủ yếu **chú thích**) |
| Tệp/dòng test | 114 / 18.317 | **115 / 18.540** | +1 tệp / +223 dòng |

**🚨 PHÁT HIỆN CỦA `S8` (năm cái, mỗi cái đều *"trông như đã xong"*):**

| # | Phát hiện | Bằng chứng (đo được) | Vì sao đáng ghi |
|---|---|---|---|
| **S8-1** | **`PaymentHub:Fake:HashSecret` riêng là KHÔNG THỂ CHẠY** — tài liệu `v0.1` (§7.7.2) yêu cầu nó, nhưng bên **kiểm** là `VnPayPaymentProvider` đọc `PaymentHub:VnPay:HashSecret` ⇒ ký khoá khác ⇒ **mọi** IPN bị `400` | ✅ **Đo chiều ngược**: đặt khoá Web khác Host ⇒ E2E `S8-P7/P8/P9/P10/P13/P14` **ĐỎ**, `P1…P6` vẫn xanh | 📌 Nếu chỉ đọc tài liệu rồi tin, ta sẽ *"sửa"* bằng cách **bỏ kiểm chữ ký** — tức **giao hàng miễn phí**. ✅ Sửa **tài liệu trước**, rồi code: dùng **một** khoá dev, giữ nguyên lớp ③ |
| **S8-2** | **Tài liệu ghi `Payments:Provider` — khoá THẬT là `PaymentHub:Provider`** | `eCommerceHttpApiHostModule` bind `GetSection("PaymentHub")`; đặt `Payments:*` thì **không có gì đọc** ⇒ **im lặng** chạy VNPay | Cùng họ với `S8-1`: cấu hình **sai tên** mà không có lỗi nào — chỉ thấy *"sao cổng giả lập không được dùng"* |
| **S8-3** | **`Product.Code`, `Id` (GUID), `CostPrice` — ràng buộc `D10` "cấm lộ giá vốn" hiện được bảo đảm bằng SỰ VẮNG MẶT, không bằng kiểm soát** | `grep CostPrice` trong `src/`+`modules/` = **0** định nghĩa (chỉ có trong **chú thích**); bảng `CatProductVariants` **không có** cột giá vốn | ✅ E2E `S8-W3` khẳng định HTML công khai **không** chứa mã nội bộ / GUID / `CostPrice` ⇒ hôm nay **không thể** lộ. ⚠️ **Nhưng** khi GĐ2 thêm giá vốn thì ca đó **không** tự bắt: phải dựa vào **danh sách trắng thuộc tính DTO** (`StorefrontCongKhaiTests`) — đã dựng sẵn |
| **S8-4** | **`/DatHang` + cổng + kết quả đều nằm ở tiến trình Web, còn IPN ở Host** ⇒ "end-to-end" **bắt buộc** cần **hai** tiến trình | E2E cũ chỉ chạy Web (:5100); nếu không khởi động Host thì IPN không tới nơi và **trang kết quả nói "chưa thanh toán" là ĐÚNG** | 📌 Đây là ca dễ kết luận sai nhất: *"sản phẩm hỏng"* trong khi **phép đo thiếu một tiến trình**. ✅ Bước ③b + `wait-host` + khẳng định Host lên trước khi đo |
| **S8-5** | 🚨 **Khởi động Host trong E2E làm `InventoryReconciliationWorker` chạy NGAY** (`Timer.RunOnStart = true`) ⇒ nó **ghi một dòng lệch THẬT** vào `ReconRunMismatches` cho dòng tồn kho probe (`OnHand=100`, sổ cái `0`) | Đo được ngay: cổng cuối phiên **ĐỎ ở bước ⑥** (`[① phải rỗng] ReconRunMismatches :: TONG=5`) sau **5** lần chạy E2E — mỗi lần **một** dòng | 📌 Dòng lệch đó **không** thuộc lượt `e7` nên phép dọn cũ **không** chạm tới ⇒ **bước ⑥ bắt được**, đúng lý do bước ⑥ tồn tại. ✅ Dọn theo **SKU probe** (`Sku like 'K97E2E%'`) + **đếm lại** (`conLechProbe`). 🔑 Bài học: **một tiến trình mới trong phép đo = một nguồn dữ liệu mới**, phải hỏi *"job nào chạy khi nó lên?"* |

**Còn lại của `PAY-001` sau `S8` (không đổi so với §16.9, trừ hai dòng đã đóng):**
① kiểm chứng **tiền thật** · ③ xác nhận dòng hoàn `ChoXacNhan` (`Q6`) · ④ outbox bền vững · ⑤/⑥ mã + quyền cho
`TaoGiaoDichAsync` · ⑦ `TC-14`/`TC-18`/`TC-30`/`TC-31`/`TC-33`/`TC-35`/`TC-36` · ⑨ `ProviderCreateDateGmt7` ·
⑩ `F-PAY-7` **chưa có phép đo** (code đã sửa).
➕ **Nợ mới của `S8`:** UI **hoàn tiền** (chưa có nút) · trang kết quả **chưa** hiện *"cổng đã nhận yêu cầu hoàn"* ·
ô *"Thanh toán ngay"* **chưa** có trên storefront công khai (khách chưa tự đặt được — `WEB-003`).




### 16.1…16.5 (mốc `S4` — giữ nguyên để đối chiếu)

| # | Việc | Tệp | Bằng chứng |
|---|---|---|---|
| 1 | **Đường TẠO giao dịch** (nợ §15.3 #1) | `PaymentAppService.TaoGiaoDichAsync` + `TaoGiaoDichInput/KetQuaDto` | ✅ biên dịch + `IPaymentAppService` mở rộng; ⚠️ **chưa** đo HTTP thật (§16.4 #3) |
| 2 | **`MerchantReference` ỔN ĐỊNH** (`BR-PAY-002`) | `SinhMaThamChieu` (Domain, hàm thuần) | ✅ **10 ca** unit — gồm *"gọi hai lần ⇒ cùng mã"*, *"gốc là tiền tố ở mọi độ dài"*, *"GMT+7"*, *"`Unspecified` không bị trừ offset"*, *"bỏ dấu tiếng Việt"* |
| 3 | **Cầu nối `Order.PaymentStatus`** (nợ §15.3 #3, `Q1`) | `PaymentSucceededEto`/`PaymentFailedEto` · `PaymentEventPublisher` · `OrderPaymentStatusHandler` · `Order.DatTrangThaiThanhToan` | ✅ **6 ca** reflection (handler tồn tại · ETO mang đủ trường · ETO **không** là `INotificationEvent` · setter kín · `IXuLyKetQuaCong` **không** phải app service · **không** tham chiếu ngược tầng) + **9 ca** luật hấp thụ của đơn. ⚠️ **Giới hạn phải nói rõ:** `Handler` **chưa** được chạy **end-to-end** (chưa có endpoint IPN để kích hoạt) — "keo dính" (nạp đơn + ghi) chưa có test; xem §16.4 #2 |
| 4 | **Ghi nhận kết quả cổng** (nền cho IPN `S5`) | `IXuLyKetQuaCong` + `XuLyKetQuaCongService` | ✅ fail-closed theo chữ ký · đối chiếu số tiền · idempotent · phát ETO **chỉ khi thật sự đổi** — 🔑 **chưa có test tự động** cho lớp này (cần DB; xem §16.4 #2) |
| 5 | **Đóng `F-PAY-2`** (đối chiếu số tiền) | `LuatCauNoiThanhToan` | ✅ **7 ca** — lệch **một đồng cả hai chiều**, khác tiền tệ dù số bằng, tiền tệ rỗng ⇒ VND, 4 chữ số thập phân |
| 6 | **Đóng `F-PAY-3`** (mã rỗng) | `XuLyKetQuaCongService` bước ② | ⚠️ code có, **test chưa** (cùng lý do #4) |
| 7 | **Đóng `F-PAY-9`** (`MarkFailed` trả `bool`) | `PaymentTransaction.MarkFailed` | ✅ **3 ca** — `true` lần đầu, `false` lần lặp **và** không đổi gì trên giao dịch đã thu tiền |
| 8 | **Guard constructor** (nợ §3.1 dòng 1) | `PaymentTransaction(...)` | ✅ **5 ca** — `amount ≤ 0` ⇒ `0007`; mã/cổng rỗng ⇒ `0006`; **chiều ngược**: giá trị hợp lệ **phải** tạo được |
| 9 | **Hàng rào DB chứng minh trên PostgreSQL thật** (`TC-29`) | `test/eCommerce.IntegrationTests/PayTransactionsChanTrungTests.cs` | ✅ **5 ca** — trùng `MerchantReference` khi `TenantId = NULL` ⇒ `23505`; trùng `IdempotencyKey` ⇒ `23505`; **hai chiều ngược** (khác tenant ⇒ cho phép; `IdempotencyKey` NULL ⇒ cho phép) |
| 10 | **`querydr` cũng phát sự kiện** | `PaymentAppService.PostTruyVanTrangThaiAsync` | 🔑 không có dòng này thì *"lưới an toàn"* cứu được **tiền** mà **không** cứu được **đơn** |

### 16.2 Số đo thật — TRƯỚC và SAU `S4`

| Chỉ số | Trước `S4` (đo 18/10/2026 đầu phiên) | Sau `S4` | Δ |
|---|---:|---:|---|
| Unit test (`eCommerce.Tests`) | **1228** | **1294** | **+66** |
| Integration test (PostgreSQL thật) | **46** | **51** | **+5** |
| Khối mermaid | 213 · 0 lỗi | 213 · 0 lỗi | 0 (không thêm sơ đồ) |
| Unique index `NULLS NOT DISTINCT` | **51** | **51** | **0** — 📌 `S4` **không** thêm bảng/index ⇒ **không có migration** |
| Cổng `-DayDu` | `exit 0` · **217.1s** | xem `SESSION-KICKOFF` §3 (ghi sau khi chạy cuối phiên) | — |

### 16.3 Việc CÒN LẠI (không thuộc `S4` — ghi để phiên sau không phải đoán)

> 🆕 **ĐỌC KÈM §16.9 (trạng thái SAU `S5`):** các mục **#1, #2, #4, #5, #7** dưới đây **ĐÃ ĐÓNG ở `S5`**;
> **#3** (`FakePaymentProvider`) **dời sang `S8`**; **#6** (quyền `TaoGiaoDichAsync`), **#8** (test còn thiếu),
> **#9** (UI) **vẫn mở**. 📌 Bảng dưới giữ nguyên nội dung `S4` làm **mốc lịch sử** — 🔑 đừng đọc nó như danh sách
> việc hiện tại.

| # | Việc còn lại | Vì sao chưa làm | Phiên |
|---|---|---|---|
| 1 | **`PaymentRefundedEto`** — đơn nhận `PartiallyRefunded`/`Refunded` | `S4` chỉ làm **hai** ETO của phần A; hoàn tiền là phần B | `S5` |
| 2 | **Endpoint IPN `[AllowAnonymous]`** + allowlist + **vá `F-PAY-1`** | Là **phạm vi** của `S5`; 🔒 `S4` chỉ dựng đường **xử lý** (`IXuLyKetQuaCong`) để `S5` nối vào | `S5` |
| 3 | **`FakePaymentProvider` + trang giả lập** (§7.7.2) | 🔑 cần cho *"IPN trùng / sai chữ ký **tất định**"* ⇒ cùng `S5` | `S5` |
| 4 | **Bảng dòng hoàn tiền** + `CHECK RefundedAmount <= Amount` | `S5` (kèm `SAL-006`) | `S5` |
| 5 | **`F-PAY-7`** (một UoW cho cả lô đối soát) | Không thuộc đường **tạo** giao dịch | `S5`/`S10` |
| 6 | **Quyền cho `TaoGiaoDichAsync`** — hiện chỉ cần `eCommerce.Payments` (**đọc**) trong khi nó **tạo giao dịch** | 🚨 **Cần người quyết** (cùng họ `F-PAY-8`): thêm `Payments.Create` hay dùng `Payments.Refund`? 📌 **Không tự chọn** — thêm một permission là **hợp đồng quyền** thay đổi | `S5`/`S7` + **BA** |
| 7 | **Job đối soát ĐỌC giá trị `MarkFailed` trả về** (đóng nốt `F-PAY-9` ở phía **đếm**) | `S4` sửa **hàm**; chỗ **đếm** trong `PaymentReconciliationWorker` vẫn `soDoi++` vô điều kiện | `S5`/`S7` |
| 8 | **Test còn thiếu**: `TC-07, TC-08, TC-14, TC-18, TC-30, TC-31, TC-33, TC-35, TC-36` | Cần IPN endpoint / 2 tenant / quét mã nguồn | `S5`+`S10` |
| 9 | **UI thanh toán** (`/DatHang` → cổng → trang kết quả) | Là phạm vi `S8` | `S8` |

### 16.4 Nợ kỹ thuật ĐÃ GHI (không phải tính năng đã có)

| # | Nợ | Vì sao chọn thế | Hệ quả nếu để lâu |
|---|---|---|---|
| 1 | 🚨 **Outbox bền vững (`XC-002`) CHƯA có** — sự kiện phát **in-process, trong cùng UoW** | 📌 Đo được: `eCommerceDbContext` **không** implement `IHasEventOutbox`; gói `Volo.Abp.EventBus.EntityFrameworkCore` (chứa mapping bảng `AbpEventOutbox`) **KHÔNG có** trong cache NuGet của máy ⇒ cài outbox sẽ **đổi cách phân phối MỌI sự kiện** của hệ thống (rủi ro làm đỏ E2E/probe của `S1`–`S3`) — **chủ dự án chốt: hoãn** (hỏi trực tiếp trong phiên `S4`) | Tiến trình chết **sau** commit ⇒ sự kiện mất ⇒ **đơn `Unpaid` trong khi tiền đã vào**. 🔑 Giảm nhẹ: `querydr` (job 15 phút) sẽ phát lại sự kiện khi phát hiện tiền đã vào — **nhưng** chỉ khi việc đối soát **chạy** |
| 2 | **Chưa có test cho phần "keo dính" của thanh toán** — `XuLyKetQuaCongService` (4 bước: chữ ký → mã → số tiền → phát ETO) và `OrderPaymentStatusHandler` (nạp đơn → so tiền → ghi) | Test của dự án là **thuần** (không dựng host/DB); hai lớp này cần `IRepository` thật ⇒ phải dựng hạ tầng test mới (**ngoài phạm vi `S4`**), hoặc chờ **endpoint IPN** (`S5`) để test qua HTTP | 🔒 Rủi ro thật: **mọi quyết định** trong hai lớp đó đã được tách ra **hàm thuần có test** (`LuatCauNoiThanhToan` · `Order.DatTrangThaiThanhToan` · `SinhMaThamChieu` · `MarkSucceeded/MarkFailed`), ⚠️ nhưng **thứ tự gọi** + **điều kiện dừng** (không ném khi lệch tiền, không ném khi không thấy đơn) **chưa** được máy kiểm ⇒ `S5` **phải** phủ bằng test qua endpoint IPN. 📌 **Chưa có lần chạy end-to-end nào** của đường `ETO → handler → Order.Paid` |
| 3 | **Route + mã HTTP thật của `tao-giao-dich` chưa đo** | Chưa chạy `tools\route-check.ps1` với host sống trong `S4` (thời gian dành cho baseline + gate) | Có thể lệch tên route so với §6.2 → `S8` gọi sai endpoint |
| 4 | **`F-PAY-4`**: 2 unique index của `PayTransactions` chưa ánh xạ trong `ViPhamRangBuocDb` | Không thuộc `S4`; 📌 nay **quan trọng hơn** vì đường tạo giao dịch có thể đụng `23505` | CSKH nhận mã chung `eCommerce:Db:0001` thay vì *"giao dịch đã tồn tại"* |

### 16.5 Bốn phát hiện MỚI của `S4` (ghi lại vì đều thuộc loại *"hỏng âm thầm"*)

| # | Phát hiện | Bằng chứng (đo được) | Vì sao đáng ghi |
|---|---|---|---|
| **S4-1** | 🚨 **Xoá ký tự ≠ bỏ dấu**: bản đầu của `SinhMaThamChieu` *"bỏ mọi ký tự không hợp lệ"* ⇒ `ĐƠN-001` thành **`N-001`** ⇒ hai đơn **khác nhau** cho ra **cùng** mã | Test `So_don_tieng_Viet__BO_DAU__khong_mat_chu_cai` **ĐỎ** ngay lượt chạy đầu | Hậu quả là `23505` ở một đơn **bình thường**, và **không ai** nghĩ tới dấu tiếng Việt. ✅ Sửa bằng `NormalizationForm.FormD` + `Đ→D` |
| **S4-2** | 🚨 **`MarkFailed` trả `true` cho lần `Failed` LẶP LẠI** (vì chỉ so *"có ghi hay không"*, không so **trạng thái trước**) | Test `MarkFailed__lan_dau_TRA_TRUE__lan_hai_TRA_FALSE` **ĐỎ** ngay lượt đầu | Đây là `F-PAY-9` ở dạng **tinh vi hơn**: *"ghi lại cùng một giá trị"* trông y như một chuyển dịch ⇒ số đếm của job lại sai. ✅ Sửa: `daDoi = Status != Failed` **trước** khi gán |
| **S4-3** | `IRepository<TEntity, TKey>.GetCountAsync(predicate)` **KHÔNG tồn tại** ⇒ `CS1660` (*"lambda không chuyển được sang `CancellationToken`"*) | Lỗi biên dịch thật ở `PaymentAppService` lượt đầu | Triệu chứng **không trỏ tới chỗ sai** (nó nói về `CancellationToken`). ✅ Cách đúng: `AsyncExecuter.CountAsync(await repo.GetQueryableAsync())` — 📌 đã ghi ngay tại dòng mã |
| **S4-4** | Mã `eCommerce:Payment:0001` (`PaymentSignatureInvalid`) **đã khai từ Phase 0 nhưng CHƯA TỪNG dùng** (grep toàn repo = **1** kết quả, chính chỗ khai báo) | `grep PaymentSignatureInvalid` = 1 | 🔑 Dùng lại nó thay vì đẻ mã `0008` mới ⇒ tránh **hai nguồn sự thật** (`XC-003` **F2**). 📌 Bài học: *mã lỗi chưa dùng* vẫn là **hợp đồng** — kiểm grep trước khi thêm mã |

> 🔑 **Cách đọc mục này:** cả bốn phát hiện đều **không** làm đỏ build, **không** làm đỏ cổng DB, và ba trong bốn
> **không** nằm trong tài liệu trước `S4`. Thứ duy nhất bắt được chúng là **test viết cùng lúc với code** — đúng
> tinh thần *"đo trước khi kết luận"* của `handover` §5.2.

---

> 🔑 **Một câu để nhớ:** *"`querydr` không phải tính năng phụ — nó là **lưới an toàn duy nhất** khi IPN mất."*
> 📌 Và: **`00` của VNPay nghĩa là "cổng đã NHẬN", không phải "tiền đã RA"** — mọi chỗ đọc mã `00` thành *"xong"*
> đều là **sổ sách nói dối**.



