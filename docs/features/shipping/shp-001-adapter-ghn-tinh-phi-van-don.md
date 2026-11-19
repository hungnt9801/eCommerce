# `SHP-001` — Adapter GHN: tính phí và tạo vận đơn

> ⚠️ **HAI CẢNH BÁO PHẢI ĐỌC TRƯỚC KHI REVIEW TÀI LIỆU NÀY** (kế thừa từ tài liệu khác, **có bằng chứng trong repo**):
>
> **1. GHN đổi COD bắt buộc OTP ⇒ KHÔNG tự động hoá được.** Bằng chứng: `docs/implementation-plan.md:199`
> (*"đổi COD ở GHN cần **OTP** ⇒ có bước thủ công"*) và `docs/implementation-plan.md:426`
> (*"`SHP-003` | **COD**: thu hộ, đổi COD (⚠️ GHN cần OTP)"*), `docs/business-modules-proposal.md:548`.
> ⇒ Hệ quả **cứng cho tài liệu này**: `SHP-001` **chỉ truyền** `cod_amount` tại thời điểm tạo vận đơn;
> **cấm** expose bất kỳ method/endpoint nào *đổi* COD (xem **ADR-07**, **BR-SHP-001-014**).
>
> **2. GHTK phải xác minh địa chỉ 2 cấp TRƯỚC khi gửi.** Bằng chứng: `docs/implementation-plan.md:290`
> (*"GHTK chưa xác nhận địa chỉ 2 cấp — mức **cao** — hỏi GHTK trước khi chốt mô hình địa chỉ"*),
> `docs/implementation-plan.md:338` (*"GHTK làm sau, cần xác minh địa chỉ 2 cấp trước"*) và
> `docs/business-modules-proposal.md:546` (*"GHTK vẫn dùng payload 3 cấp `province`/`district`/`ward` và
> **KHÔNG tìm thấy** cờ tương đương `is_new_to_address`"*).
> ⇒ Hệ quả **cứng cho tài liệu này**: abstraction của `SHP-001` **không được** nhốt mô hình địa chỉ của GHN
> vào hợp đồng dùng chung (xem **ADR-06**).

| Thuộc tính | Nội dung |
|---|---|
| **Mã** | `SHP-001` |
| **Module** | **ShippingHub** — dự kiến tạo mới `modules/ShippingHub/eCommerce.ShippingHub.{Domain, EntityFrameworkCore, Application.Contracts, Application}` (hiện **chưa tồn tại** — xem §15.2 **F1**). Khuôn đặt tên project theo `docs/structure-and-techstack.md:87`–`95` |
| **Giai đoạn** | **GĐ3 của `05`** (`D-69`; `docs/agents/anh-xa-lo-trinh-d69.md:28`) — lộ trình cũ ghi GĐ2 (`docs/implementation-plan.md:194`, `:199`) chỉ còn là lịch sử |
| **Ưu tiên** | **Must have** (không có nó thì đơn **không có mã vận đơn** ⇒ `SAL-003` **không** chuyển được trục thực hiện `Packing → AwaitingPickup` — `FulfillmentStatus.Packing = 2`, `AwaitingPickup = 3`, `Packed = 13` trong `src/eCommerce.Domain.Shared/Enums.cs:151-210`; đơn giữ **4 trục**, `FulfillmentStatus` **18 giá trị** theo `D-08`; `docs/features/sales/sal-003-state-machine-4-truc.md:237`) |
| **Trạng thái** | 🟡 `Draft` — chờ review |
| **Người viết / người review** | *(điền)* / **Kiến trúc trưởng + người đã liên hệ GHN** |
| **Phiên bản / ngày** | `v0.5 — 19/11/2026` |
| **Tài liệu liên quan** | `SHP-002` (webhook trạng thái vận đơn & đối soát — **tiêu thụ** `ShpShipments` do tài liệu này tạo) · `SHP-003` (COD, ⚠️ GHN đổi COD cần OTP) · `SHP-004` (adapter GHTK, ⚠️ địa chỉ 2 cấp) · `XC-001` (Money) · `XC-002` (outbox/inbox, idempotency) · `XC-003` (khoá phân tán) · `XC-004` (phân quyền/tenant) · `XC-005` (log/metric) · `SAL-002` (đặt hàng) · `SAL-003` (state machine 4 trục) · `REC-003` (đối soát COD) · `CAT-001` (cân nặng SKU) · `docs/structure-and-techstack.md` |

---

## 0. Tóm tắt & quyết định thiết kế

**Tóm tắt:** `SHP-001` chốt **cách hệ thống gọi API GHN** để làm đúng **hai việc**: (a) **tính phí vận chuyển**,
(b) **tạo vận đơn** và nhận **mã vận đơn** từ hãng. Hai việc này đi qua **một cổng adapter duy nhất**
(`IShippingCarrierProvider` + factory) để `SHP-004` (GHTK) cắm vào mà **không sửa lõi**, và để
`PaymentHub`/`ChannelHub` giữ nguyên khuôn đã có trong repo.

**Vì sao tài liệu này phải tồn tại (không phải thủ tục):** tạo vận đơn là **ghi dữ liệu ra một hệ thống KHÔNG
rollback được cùng transaction của ta**. Đây là loại lỗi mà transaction DB **không cứu được**:

| Nếu làm sai | Hệ quả thật |
|---|---|
| Gọi API tạo vận đơn **trong** transaction DB rồi transaction rollback | Hãng **đã** tạo vận đơn thật, ta **không** có mã vận đơn ⇒ shipper tới lấy hàng mà hệ thống không biết ⇒ trạng thái đơn treo vĩnh viễn |
| Retry tạo vận đơn **không có khoá ổn định** | **2 vận đơn thật** cho 1 đơn hàng ⇒ hãng tính phí 2 lần, lấy hàng 2 lần, COD bị nhân đôi ở khâu thu hộ |
| Lấy phí ước tính nội bộ rồi **không** đối chiếu với phí hãng báo lúc tạo vận đơn | Thu khách **thiếu** tiền ship; sai lệch chỉ lộ ra ở `REC-003` (đối soát COD bằng file Excel thủ công — `docs/implementation-plan.md:200`) |
| Tự động hoá **đổi COD** như một API thường | 🚨 GHN **bắt buộc OTP** ⇒ luồng sẽ dừng giữa đường, hoặc tệ hơn là bị "vá" bằng cách bỏ qua OTP |

**Phạm vi:**
- ✅ Cổng adapter hãng vận chuyển (`IShippingCarrierProvider`) + factory chọn theo `ProviderCode`.
- ✅ **Tính phí**: gọi API hãng (nguồn sự thật) + bảng giá nội bộ để **ước tính** trước khi đặt hàng.
- ✅ **Tạo vận đơn**: sinh khoá chống trùng **ổn định**, gọi hãng **ngoài transaction**, lưu mã vận đơn + phí thật.
- ✅ **Huỷ vận đơn trước khi hãng lấy hàng** (khớp guard `Created → Cancelled` của `SHP-002` §3.2 dòng #2).
- ✅ Entity `Shipment` (+ 4 bảng danh mục/nhật ký) — **`SHP-002` chỉ đọc & cập nhật trạng thái**, không tạo bảng mới.
- ✅ **Idempotency ở mọi biên vào** (API tạo vận đơn, job thử lại) theo hợp đồng `XC-002`.
- ✅ **Khoá phân tán** cho thao tác tạo vận đơn theo đơn hàng + job đồng bộ bảng giá.
- ✅ Phát `ShipmentCreatedEto` / `ShipmentCreationFailedEto` qua **outbox**.
- ✅ Tiền theo `XC-001`: `decimal` + `Money`, **cấm so `==` trên tiền**, ra biên hãng là **số nguyên**.
- ✅ Metric + log + `CorrelationId` theo `XC-005`.
- ✅ **Một hợp đồng địa chỉ trung tính** mang **cả** 2 cấp (mới) **và** 3 cấp (cũ) — vì GHN đã có cờ
  `is_new_to_address` còn GHTK thì **chưa xác minh** (`docs/business-modules-proposal.md:513`, `:546`).

**Không nằm trong phạm vi:**
- ❌ **Nhận webhook trạng thái & đối soát chủ động** — thuộc `SHP-002`. Tài liệu này **chỉ** tạo vận đơn và
  **trả** `ProviderTrackingCode` cho `SHP-002` dùng làm khoá đối chiếu.
- ❌ **Nghiệp vụ COD**: thu hộ, **đổi COD (⚠️ GHN bắt buộc OTP)** — thuộc `SHP-003`. `SHP-001` **chỉ truyền**
  `cod_amount` đã được Sales chốt, và **cấm** expose method đổi COD (**ADR-07**).
- ❌ **Adapter GHTK** (địa chỉ 2/3 cấp, ⚠️ chưa xác minh) — thuộc `SHP-004`. Tài liệu này **chỉ định nghĩa**
  hợp đồng mà `SHP-004` phải cài.
- ❌ **Master data địa chỉ và ánh xạ mã hãng** (tỉnh/phường, `AddrCarrierMappings`) — thuộc module **`Address`** (đã có: `modules/Address/`,
  `eCommerce.slnx:3-8`). `SHP-001` **không** có bảng ánh xạ riêng và **không** tự truy vấn `AddrCarrierMappings`: mã địa chỉ hãng
  **chỉ** lấy qua các cửa của `eCommerce.Address.Application.Contracts`: `ResolveCarrierAddressCodeAsync` (`D-89`, nhận Guid đơn vị) và, cho đường **theo đơn**, `ResolveCarrierAddressCodeByCodeAsync` (`D-110`, nhận khoá tự nhiên `(SchemaVersion, cấp, mã)`) — ⬜ kế hoạch WP-15, chưa có trong code (grep `*.cs` = 0).
  Địa chỉ **kho gửi** vẫn là cấu hình theo hãng (§6.4), không phải master data — đúng `docs/business-modules-proposal.md:517`.
- ❌ **RMA/trả hàng đơn giao vận** (hàng hoàn qua hãng) — thuộc `SAL-006`, làm **cùng đợt vận chuyển** (`D-70` H1); `SHP-001` chỉ tạo/huỷ vận đơn **đi**.
- ❌ **Đối soát COD theo "phiên" / import file Excel** — thuộc `REC-003` (`docs/implementation-plan.md:200`).
- ❌ **Sửa state machine 4 trục của đơn** — thuộc `SAL-003`. Ta phát ETO, Sales tự quyết định.
- ❌ **Read-model/báo cáo** — thuộc `REP-001`/`REP-003`.
- ❌ **Tích hợp máy in nhãn / phần cứng** — chỉ **trả về** nội dung nhãn nếu hãng cung cấp qua API.
- ❌ **Ghi nhận doanh thu/kế toán** — thuộc `EINV-001`/`ACC-001`.
- ❌ **Retry/backoff tầng hạ tầng** (DLQ, phát lại) — dùng lại `XC-002`, không định nghĩa lại.

### 0.1 Quyết định thiết kế (ADR ngắn)

| # | Quyết định | Phương án đã cân nhắc | Lý do chọn | Hệ quả |
|---|---|---|---|---|
| **ADR-01** | **Một cổng adapter hãng vận chuyển** `IShippingCarrierProvider` + `IShippingCarrierProviderFactory`; app service **chỉ** biết interface | (a) gọi HTTP GHN thẳng trong app service · (b) một `switch (providerCode)` khổng lồ · (c) **interface + factory** | Khuôn này **đã có thật** trong repo: `IPaymentProvider` (`modules/PaymentHub/eCommerce.PaymentHub.Domain/Payments/IPaymentProvider.cs:58`), `IChannelConnector` (`modules/ChannelHub/eCommerce.ChannelHub.Application.Contracts/Connectors/IChannelConnector.cs:122`), `ChannelConnectorFactory` (`modules/ChannelHub/eCommerce.ChannelHub.Application/Connectors/ChannelConnectorFactory.cs:9`). Đi theo khuôn ⇒ `SHP-004` chỉ thêm 1 class + 1 dòng | `SHP-001` **không** được chứa chi tiết GHN trong tầng app service; mọi thứ riêng của GHN nằm **sau** interface |
| **ADR-02** | 🚨 **Gọi API hãng NGOÀI transaction DB**: 3 pha rời (ghi ý định → gọi hãng → ghi kết quả) | (a) gọi hãng trong transaction · (b) gọi trước rồi ghi 1 lần · (c) **3 pha rời** | Pha (a): hãng tạo vận đơn thật rồi ta rollback ⇒ **mất mã vận đơn** (bảng §0). Pha (b): ta chết sau khi hãng tạo ⇒ **không có dấu vết ý định** ⇒ không thể tra ngược. Pha (c) giữ **transaction ngắn** và để lại **bằng chứng trước khi gọi** | Phải chấp nhận: DB có thể có `Shipment` ở trạng thái `Submitting` mà chưa biết kết quả ⇒ **cần job quét** `Submitting` quá N phút (§4.2, §12) |
| **ADR-03** | **Khoá chống trùng ổn định** `ClientOrderCode` = **hàm thuần** của `(OrderId, Generation)`: `"{orderId:N}-{generation}"` (`D-109`), sinh **một lần** ở Pha 1, **lưu DB trước** khi gọi hãng; gọi lại dùng **đúng** giá trị cũ. `Generation` **chỉ** tăng khi tạo `Shipment` **mới** cho đơn; `attemptNo` **không** vào mã gửi hãng | (a) `Guid.NewGuid()` mỗi lần gọi · (b) mã theo `(tenantId, orderId, attemptNo)` · (c) mã chỉ theo `(tenantId, orderId)` · (d) **hàm thuần `(OrderId, Generation)`** (`D-109`) | `XC-002` **BR-XC-002-007** cấm `Guid.NewGuid()` cho mỗi lần gọi; `XC-002` §7.3 ghi rõ khoá phải **ổn định qua retry**. (b) mã đổi theo `SubmitAttemptCount` ⇒ trái BR-003. (c) GHN gửi lại mã đã dùng thì **trả `order_code` cũ** ⇒ tạo lại sau `Cancelled` nhận về vận đơn **đã huỷ im lặng**. (d) đúng ở cả hai khả năng hãng xử lý mã của đơn đã huỷ (**chưa xác minh**). GHN `client_order_code`: *"unique per shop. Max 50"* (`developer.ghn.vn`, tra 18/11/2026 — nguồn `D-109`) ⇒ mã ≤ 43 ký tự vừa giới hạn; `tenantId` không vào chuỗi vì `OrderId` là GUID duy nhất toàn hệ. Có **2 hàng rào**: unique index ở DB + hãng trả lại vận đơn cũ khi mã trùng | ⚠️ Phụ thuộc hãng **thật sự** chống trùng theo mã đơn phía ta — GHN docs nói có, **chưa kiểm chứng** bằng sandbox (§15.1 Q3). Nếu hãng **không** chống trùng ⇒ hàng rào duy nhất là DB của ta ⇒ phải **tuyệt đối** không sinh mã mới khi retry cùng `Shipment`. **Mọi** adapter (`SHP-004` GHTK) dùng **đúng** hàm này (§7.3) |
| **ADR-04** | **Phí hãng là NGUỒN SỰ THẬT** tại thời điểm tạo vận đơn; bảng `ShpShippingRates` nội bộ **chỉ để ước tính** | (a) chỉ dùng bảng giá nội bộ · (b) chỉ gọi API hãng · (c) **cả hai, hãng là chốt** | (a) sai khi hãng đổi biểu phí/phụ phí ⇒ thu thiếu tiền. (b) làm mỗi lần hiển phí ở checkout đều gọi hãng ⇒ tốn quota, chậm. (c) cho phép ước tính nhanh và **vẫn** chốt bằng số thật | Mọi lệch phí **phải** được **ghi nhận** (không tự sửa số của hãng — kế thừa `SHP-002` BR-SHP-002-007). Cần bảng `ShpShippingRates` + job đồng bộ |
| **ADR-05** | Tiền: `decimal` + `Money`; ra biên hãng chuyển sang **số nguyên VND** (hàm đổi ⬜ chưa có — Q17); **cấm** `==` trên tiền | (a) `double` · (b) `decimal` trần · (c) **`Money`** | `XC-001` BR-XC-001-003/007/008. Tiền lệ **thật trong repo**: `VnPayPaymentProvider` nhân 100 bằng `decimal.Round(..., MidpointRounding.AwayFromZero)` và **không** dùng `double` (`modules/PaymentHub/eCommerce.PaymentHub.Application/Providers/VnPayPaymentProvider.cs:113`) | 🔶 **`Money`/`MoneyAllocator` ĐÃ có** (`shared/eCommerce.Kernel/Money/Money.cs:23`, `MoneyAllocator.cs:21`; project `eCommerce.slnx:87`) nhưng `Money` là `sealed class` (factory `Cua`/`Vnd` — `Money.cs:36,39`), **không** có `ToMinorUnits()`/`Money.Of`, **không** tự làm tròn (`Money.cs:19-21`, `IRoundingPolicy` — `RoundingPolicy.cs:11`) ⇒ hàm đổi `decimal → long` là ⬜ kế hoạch (Q17). Cột DB vẫn `numeric(18,4)` + `char(3)` như `modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs:33-36` |
| **ADR-06** | **Hợp đồng địa chỉ TRUNG TÍNH** mang **cả 2 cấp và 3 cấp** + cờ "địa chỉ kiểu mới"; **map riêng trong adapter** | (a) hợp đồng chỉ có 2 cấp (mô hình mới) · (b) hợp đồng chỉ có 3 cấp (mô hình cũ) · (c) **trung tính, mang cả hai** | GHN đã thích ứng bằng cờ `is_new_to_address`/`is_new_from_address`/`is_new_return_address` (`docs/business-modules-proposal.md:513`–`514`) **nhưng** GHTK vẫn dùng `province`/`district`/`ward` và **chưa tìm thấy** cờ tương đương (`:546`), và quyết định đã chốt là **hỗ trợ CẢ HAI** (`:658`) | Hợp đồng dùng chung **không** được có `IsNewToAddress` kiểu GHN; trường "kiểu địa chỉ" là **dữ liệu cấu hình theo hãng** ⇒ `SHP-004` thêm adapter **không** phải sửa `SHP-001` |
| **ADR-07** | **KHÔNG có API đổi COD.** `SHP-001` **chỉ nhận** `cod_amount` lúc tạo vận đơn; đổi COD là **quy trình thủ công có OTP** (`SHP-003`) | (a) làm API đổi COD gọi thẳng hãng · (b) **không** làm, chuyển `SHP-003` | 🚨 GHN **bắt buộc OTP** để đổi COD (`docs/implementation-plan.md:199`, `:426`; `docs/business-modules-proposal.md:548`). Nếu ta expose API "đổi COD" thì hoặc **luồng dừng giữa đường**, hoặc có người sẽ "vá" bằng cách bỏ OTP ⇒ **thao tác tiền không có xác thực** | Màn hình vận đơn **không** có nút "sửa COD" (chỉ có nút "Yêu cầu đổi COD" ghi nhận yêu cầu cho người vận hành — thuộc `SHP-003`) |
| **ADR-08** | **Khoá phân tán chỉ ở 2 chỗ**: tạo vận đơn theo `(tenantId, orderId)` và job đồng bộ bảng giá; **DB là hàng rào cuối** | (a) khoá ở mọi lời gọi API hãng · (b) **không** khoá gì · (c) **2 chỗ như trên** | `XC-003` D2: *"bỏ lock đi, DB có chặn không?"*. Khoá mọi lời gọi ⇒ lời gọi thứ 3 hết timeout và bị hãng coi là lỗi. Khoá **không** thay được unique index (đúng `XC-003` §7.3) | Lock **không reentrant** (`XC-003` D6) ⇒ **cấm** acquire lồng; TTL khoá phải **lớn hơn** thời gian gọi hãng tối đa + retry |
| **ADR-09** | Bảng `ShpShipments` do **`SHP-001` tạo**; `SHP-002` **chỉ đọc và cập nhật trạng thái** | (a) mỗi tài liệu tự định nghĩa bảng riêng · (b) **một bảng, một chủ sở hữu** | `SHP-002` §5.2 đã định nghĩa **đầy đủ** cột cho `ShpShipments` và ghi rõ *"do `SHP-001` tạo, `SHP-002` cập nhật trạng thái"*. Hai định nghĩa song song ⇒ **hai nguồn sự thật** cho cùng một bảng (đúng loại lỗi `XC-003` F1/F2) | ⚠️ **Cột của `ShpShipments` phải KHỚP `SHP-002` §5.2**; nếu `SHP-001` cần thêm cột ⇒ **sửa `SHP-002` trước**, không tự thêm |
| **ADR-10** | Mã lỗi **do module sở hữu**: `ShippingHubErrorCodes` dạng `eCommerce:Shipping:NNNN` | (a) khai ở `eCommerceConsts.ErrorCodes` của app · (b) **ở module** | Quy ước bất biến: *"module sở hữu entity/hằng số của mình, module gọi app BỊ CẤM"* (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:802`–`858`, ví dụ `:809`). Khuôn **đã có thật**: `TaxErrorCodes` (`modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:10`) và `ChannelHubErrorCodes` (`modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:36`) | Mã lỗi **phải** trùng dải với `SHP-002` §6.2 — **không** được đánh số chồng (xem §6.3: dải `0013+` để dành cho `SHP-001`) |
| **ADR-11** | Cấu hình hãng trong `ShippingHubOptions` (`BaseUrl`, token, `ShopId`, `EnableLiveCalls`), **cấm** hard-code | (a) hard-code endpoint · (b) **options + configuration** | `docs/design-doc-guidelines.md:154` (quy tắc 8) cấm hard-code endpoint NCC. Khuôn **đã có thật**: `ChannelHubOptions` + `ShopeeOptions.EnableLiveCalls` (`modules/ChannelHub/eCommerce.ChannelHub.Application/ChannelHubOptions.cs:47`), đăng ký tại host (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:263`–`264`) | `EnableLiveCalls = false` cho phép chạy **toàn bộ luồng** (lock, outbox, ETO) mà **không cần credential** GHN — đúng cách `ShopeeConnector` đang làm (`modules/ChannelHub/eCommerce.ChannelHub.Application/Connectors/ShopeeConnector.cs:80`–`87`) |
| **ADR-12** | `[Authorize]` ở **cấp CLASS** cho mọi app service; **không** có endpoint vô danh trong `SHP-001` | (a) `[Authorize]` ở từng method · (b) **cấp class** | `XC-004` D3 + BR-XC-004-001; bài học thật đã ghi trong code: `ChannelAppService` đặt `[Authorize]` cấp class **có chủ ý** vì *"GetShopsAsync từng là API CÔNG KHAI"* (`src/eCommerce.Application/Channels/ChannelAppService.cs:21`–`28`) | ⚠️ Endpoint webhook của `SHP-002` là **ngoại lệ `[AllowAnonymous]`** và **chưa** nằm trong allowlist đã chốt của `XC-004` Q1 (`docs/features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md:505`) ⇒ **xung đột phải xử lý** (xem §15.2 **F4**) |

---

## 1. Use Case Specification

| Thuộc tính | Nội dung |
|---|---|
| **Use Case ID** | `SHP-001` |
| **Use Case Name** | Lấy phí vận chuyển và tạo vận đơn tại hãng (GHN) **không tạo trùng, không mất mã vận đơn** |
| **Description** | *"Là hệ thống bán hàng, tôi muốn biết phí vận chuyển để thu đúng tiền của khách, và muốn tạo được vận đơn thật ở hãng để shipper tới lấy hàng — nhưng tôi KHÔNG muốn tạo 2 vận đơn cho 1 đơn, cũng KHÔNG muốn hãng đã tạo vận đơn mà hệ thống tôi không biết."* |
| **Actor(s)** | **Nhân viên kho/CSKH** (bấm tạo vận đơn) · **Khách hàng** (nhận phí ước tính ở checkout) · **Job thử lại** (hệ thống) · **GHN** (hệ thống ngoài) · **Quản trị** (cấu hình hãng, bảng giá) |
| **Priority** | **Must have** |
| **Trigger** | (a) checkout cần **ước tính phí** để hiển thị cho khách; (b) đơn đã được xác nhận ⇒ kho bấm **"Tạo vận đơn"**; (c) job thử lại quét `Shipment` ở trạng thái `Submitting` quá N phút; (d) CSKH **huỷ vận đơn** trước khi hãng lấy hàng |
| **Pre-Condition(s)** | • **Tenant hiện tại** (`TenantId` từ `ICurrentTenant`) — bắt buộc.<br>• **`ChannelCode`** lấy từ **đơn hàng** (`modules/Sales/eCommerce.Sales.Domain/Order.cs:19`), **không** từ payload hãng; `ChannelCode` quyết định bảng giá/kênh hiển thị phí.<br>• **Đơn hàng tồn tại** và có **dòng hàng** (`Order.cs:183`; `OrderLine.Quantity` hiện `int` — `Order.cs:832`, nâng `decimal(18,3)` theo `D-31` là việc của WP số lượng) để tính **cân nặng**. Nguồn cân nặng **chưa chốt** (Q5): `Order.WeightGram` (`Order.cs:130`, ghi qua `DatVanChuyen`) hoặc từ **sản phẩm** `Product.WeightGram` (`modules/Catalog/eCommerce.Catalog.Domain/Product.cs:78`, mô hình `D-07`: `ParentId` + `ProductUnit`); `ProductVariant.WeightInGrams` (`Product.cs:512`) chỉ là đường **cũ** cho dòng còn `ProductVariantId` (`Order.cs:816`). **Không** dùng `VariantId` làm khoá tra cân nặng.<br>• **Hãng đã được cấu hình** (`ShippingHubOptions.Providers["ghn"]`: `BaseUrl`, token, `ShopId`, `FromAddress`, `EnableLiveCalls`). Thiếu ⇒ **fail-closed**, **không** dùng giá trị mặc định (§12).<br>• **Địa chỉ nhận = snapshot giao hàng trên đơn** (`D-108`): tên + SĐT người nhận + bản sao các trường `DiaChiSnapshotDto` (`modules/Address/eCommerce.Address.Application.Contracts/AddressContracts.cs:41-54`) do **Sales** sở hữu (`SAL-002`, đóng nợ `ADDR-004` F9/Q6) và `SHP-001` đọc qua **một** interface hợp đồng ở `eCommerce.Sales.Application.Contracts` (tên gợi ý `IOrderShippingDirectory`, ⬜ **kế hoạch** — `Order` hiện **chưa** có trường địa chỉ giao: grep `Address\|DiaChi` trong `modules/Sales/**/*.cs` = 0, 18/11/2026). Đơn **thiếu** snapshot (hoặc thiếu tên/SĐT/cấp xã) ⇒ **NÉM** `DonChuaCoDiaChiGiao` (§6.3) **trước Pha 1**. Snapshot phải hợp lệ theo **mô hình địa chỉ của hãng** (2 cấp **hoặc** 3 cấp, Q1) và tra được mã hãng qua **cửa mã** `ResolveCarrierAddressCodeByCodeAsync` (`D-110`, ⬜ WP-15): nhận **đúng** `SchemaVersion` + cấp + mã của snapshot, `Address` tự tra đơn vị rồi gọi cùng hàm thuần `D-89`; `SHP-001` **không** cầm Guid đơn vị, **không** tra theo `ShippingAddressId`.<br>• `CorrelationId` được sinh/đọc theo `XC-005`.<br>• **Outbox/Inbox đã bật** để phát ETO ✅ — `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:86-87` (`Outboxes`/`Inboxes` dùng `eCommerceDbContext`). |
| **Post-Condition(s)** | • Nếu **tạo vận đơn thành công**: `Shipment.Status = Created`, `ProviderTrackingCode` **khác rỗng**, `ShippingFee` = **phí hãng báo**, và **đúng 1** dòng `ShpShipmentCreateAttempts` cho lần gọi đó.<br>• Nếu **thất bại**: `Shipment.Status = SubmitFailed` + `FailureCode` + **có** dòng attempt ghi lỗi ⇒ **không** mất thông tin để thử lại.<br>• Mỗi `ClientOrderCode` **chỉ** tương ứng **tối đa một** vận đơn ở hãng (hàng rào DB + hàng rào hãng).<br>• `ShipmentCreatedEto` nằm trong **cùng transaction** với lần cập nhật `Shipment` (`XC-002` BR-XC-002-001).<br>• Mọi lời gọi hãng **có** metric + log có `CorrelationId`; **không** log token.<br>• **Không** tồn tại đường code nào đổi `cod_amount` sau khi vận đơn đã tạo (**BR-SHP-001-014**). |
| **Basic Flow** | 1. **Tính phí (ước tính)**: `EstimateOrderFeeAsync` (**theo đơn**, `EstimateOrderShippingFeeDto`) nạp đơn qua hợp đồng Sales (`ChannelCode`, dòng hàng, snapshot giao — `D-108`) + cân nặng (nguồn chưa chốt: `Order.WeightGram` — `Order.cs:130` — hay `Σ Product.WeightGram × Quantity`, tính bằng `decimal`, lưu `int` gram, làm tròn: Q5); tra mã địa chỉ hãng **chỉ** qua cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`, §6.1). Ngoại lệ duy nhất: `EstimateCartFeeAsync` (ước tính **cho giỏ**, chưa có đơn, `EstimateCartShippingFeeDto` **riêng**) nhận `AddressId` và đọc `IAddressDirectory.TimTheoIdAsync` (`AddressContracts.cs:163`) — chỉ ước tính, không chốt (`D-108`, ADR-04); tra `ShpShippingRates` **trước**; nếu `EnableLiveCalls` và cần số thật ⇒ gọi `IShippingCarrierProvider.GetFeeAsync` (ADR-04).<br>2. Trả `ShippingFeeEstimateDto` (Money + `EstimatedAtUtc` + `IsEstimate = true`).<br>3. **Tạo vận đơn**: `CreateShipmentAsync(orderId, input)` — `input` **không** có trường địa chỉ/người nhận (`D-108`) **và không có COD/tiền tệ/dòng hàng** (lấy từ đơn qua hợp đồng Sales — BR-021). **Trước Pha 1**: đọc đơn (COD, dòng hàng) + snapshot giao qua hợp đồng Sales; thiếu snapshot ⇒ **NÉM** `DonChuaCoDiaChiGiao` (0 `Shipment`, 0 lời gọi hãng). Rồi mở **Pha 1 (UoW #1** — `Begin(requiresNew: true, isTransactional: true)`, BR-002**)**: kiểm tra đơn ∈ tenant, chưa có `Shipment` hiệu lực ⇒ gán `Generation = 1 + max(Generation)` của **mọi** `Shipment` cùng `(TenantId, OrderId)` (`D-109`, guard §3.2 #1), tạo `Shipment` `Status = Draft` (kèm bản sao snapshot đã gửi hãng — `D-77`, §5.2), **sinh `ClientOrderCode = TaoClientOrderCode(orderId, generation)`** (ADR-03) **trong cùng UoW** và **commit**.<br>4. Giành **khoá phân tán** `shipping:shipment-create:{tenantId}:{orderId}` (ADR-08, timeout hữu hạn).<br>5. **Pha 2 (UoW #2)**: ghi `Status = Submitting` + `SubmitAttemptCount++` + **commit** — đây là *bằng chứng ý định* trước khi gọi hãng (ADR-02).<br>6. Gọi hãng **ngoài transaction**: `IShippingCarrierProvider.CreateShipmentAsync` (có timeout + retry có backoff).<br>7. **Pha 3 (UoW #3)**: ghi kết quả: `ProviderTrackingCode`, `ShippingFee` (phí **thật**), `Status = Created`; ghi dòng `ShpShipmentCreateAttempts` (`Result = Created`); ghi **outbox** `ShipmentCreatedEto` — **cùng transaction**.<br>8. Cập nhật `Order.SetTotals(taxTotal, shippingTotal)` **chỉ khi** phí thật **khác** phí đã lưu ⇒ việc này do `SAL-002` quyết định qua ETO, **không** tự ý ghi vào đơn (xem §7.1).<br>9. Trả `ShipmentDto`. Nhả khoá khi hết phạm vi `await using`. |
| **Alternative Flow** | **A1: Hãng báo "mã đơn phía bạn đã tồn tại"** — `ClientOrderCode` đã được dùng ⇒ **KHÔNG** tạo vận đơn mới; tra lại vận đơn theo `ClientOrderCode`; nếu hãng trả được mã vận đơn ⇒ ghi `Created` và **ghi nhận** sự kiện "đã tồn tại" (metric).<br>**A2: Job thử lại gặp `Submitting` quá N phút** — cập nhật lại trạng thái bằng cách **hỏi hãng** (nếu hãng có API tra theo `ClientOrderCode`) trước khi thử tạo lại; ⚠️ **chưa kiểm chứng** hãng có API này (§15.1 Q3).<br>**A3: `EnableLiveCalls = false` (PoC/dev)** — provider trả kết quả **mô phỏng** (mã vận đơn giả có tiền tố `poc-`), toàn bộ luồng lock/UoW/outbox vẫn chạy — đúng cách `ShopeeConnector` đang làm (`modules/ChannelHub/eCommerce.ChannelHub.Application/Connectors/ShopeeConnector.cs:80`–`87`).<br>**A4: Huỷ vận đơn trước khi hãng lấy** — chỉ cho phép từ `Draft` (chưa gọi hãng: chỉ đổi trạng thái, **không** có dòng attempt) hoặc `Created` (gọi `CancelShipmentAsync` ngoài UoW rồi, khi hãng xác nhận, ghi `Cancelled` + **một** dòng attempt `Result = Cancelled`, `AttemptNo = 0` — §5.2). **`Submitting` KHÔNG huỷ được**: chưa có mã vận đơn nên không có webhook, mà hãng có thể **đã** tạo thật (ADR-02) ⇒ huỷ rồi tạo lại sẽ sinh hai vận đơn thật; chờ job/A2 đưa về `Created` hoặc `SubmitFailed` (§3.2 #6, Q19). Sau khi hãng đã lấy hàng ⇒ **NÉM LỖI** (khớp `SHP-002` §3.2 #2: huỷ **chỉ** trước khi lấy).<br>**A5: Đơn đã có `Shipment` hiệu lực** (không ở trạng thái hấp thụ) — trả **vận đơn hiện có** (idempotent), **không** gọi hãng lần hai (⚠️ bản `Draft`/`Submitting` chưa có mã vận đơn: chưa có cơ chế đi tiếp — Q15). Hai Pha 1 đồng thời cho một đơn ⇒ UNIQUE `(TenantId, OrderId, Generation)` chặn (`23505`) ⇒ xử lý như A5 (`D-109`). |
| **Exception Flow** | **E1:** hãng không được cấu hình (thiếu token/`BaseUrl`/`ShopId`) ⇒ **fail-closed** `eCommerce:Shipping:0013`, `400`.<br>**E2:** địa chỉ không hợp lệ với mô hình của hãng (thiếu cấp bắt buộc) **hoặc** cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`) ném/không trả được mã hãng (mã đơn vị không tồn tại `Address:0001`, `SchemaVersion` sai `Address:0005`, đích không có ánh xạ `DaXacMinh`, đích đã đóng, **> 1** ánh xạ `DaXacMinh` cùng đích) ⇒ `eCommerce:Shipping:0014`, `400`, kèm `InnerErrorCode` của `Address` — **không** gọi hãng, **không** chọn bừa một mã (vận đơn tới đơn vị đó bị chặn tới khi xử lý — đường gỡ là §15.1 Q12).<br>**E3:** hãng từ chối (4xx nghiệp vụ) ⇒ `eCommerce:Shipping:0015`; `Status = SubmitFailed`; ghi `FailureCode` + `FailureReason` (**đã che** dữ liệu nhạy cảm); **NÉM LỖI** (không nuốt).<br>**E4:** hãng timeout / 5xx ⇒ `Status = SubmitFailed` + `FailureKind = Transient`; **thử lại nhịp sau** (job); **KHÔNG** rollback dữ liệu đã ghi; metric `shipping_carrier_call_failed_total`.<br>**E5:** vượt `MaxSubmitAttempts` ⇒ `Status = SubmitFailed` (hấp thụ) + `eCommerce:Shipping:0016` + **cảnh báo** cho vận hành — **KHÔNG** thử vô hạn.<br>**E6:** không giành được khoá trong timeout ⇒ `eCommerce:Lock:0001` của `XC-003` (`409`) — **cấm** im lặng bỏ qua ở đường HTTP.<br>**E7:** `UPDATE … WHERE Status = @expected` ảnh hưởng **0 dòng** ⇒ **NÉM LỖI** `eCommerce:Shipping:0017` — **cấm** coi là thành công (`XC-002` BR-XC-002-008).<br>**E8:** phí hãng báo **lệch** phí ước tính ⇒ **KHÔNG** ném lỗi (vận đơn đã tạo thật); ghi `FeeDiscrepancy` + log `Warning` + metric `shipping_fee_discrepancy_total` (ADR-04).<br>**E9:** yêu cầu **đổi COD** ⇒ `eCommerce:Shipping:0018` — **cấm** tự động hoá (ADR-07).<br>**E10:** đơn **thiếu snapshot giao hàng** (hoặc thiếu tên/SĐT/cấp xã) ⇒ `DonChuaCoDiaChiGiao` (§6.3), `400`, **trước Pha 1** — 0 `Shipment` `Draft`, 0 lời gọi hãng (`D-108`); đường gỡ = Sales bổ sung/đổi snapshot (không có đường ghi đè trong `SHP-001`). |
| **Business Rules** | • **BR-SHP-001-001**: **Mọi** lời gọi API hãng vận chuyển **phải** đi qua `IShippingCarrierProvider`; **cấm** gọi HTTP trực tiếp từ app service/controller/job.<br>• **BR-SHP-001-002**: 🚨 **Cấm** giữ transaction DB mở trong lúc gọi API hãng (ADR-02). Ba pha **phải** là ba UoW rời, **và** phải tắt UoW ngầm của app service: host đặt `TransactionBehavior.Auto` (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:94`) nên ABP mở UoW **có transaction** cho mọi `POST`, và `Begin()` mặc định `requiresNew: false` sẽ **nhập** vào UoW ngoài ⇒ Pha 1/Pha 2 chưa commit khi gọi hãng. Bắt buộc: `CreateShipmentAsync`, `CancelShipmentAsync` và job thử lại đặt `[UnitOfWork(isTransactional: false)]` (tiền lệ vá đúng lỗi này: `modules/PaymentHub/eCommerce.PaymentHub.Application/PaymentAppService.cs:561`, F-PAY-7), mỗi pha `Begin(requiresNew: true, isTransactional: true)` rồi `CompleteAsync` **trước** lời gọi hãng. Vi phạm ⇒ `TC-08` (quét attribute + `requiresNew`) và `TC-09` (đường HTTP thật) đỏ.<br>• **BR-SHP-001-003**: Mọi yêu cầu tạo vận đơn **phải** có `ClientOrderCode` **ổn định**, sinh **một lần** (theo BR-022) và **lưu DB TRƯỚC** khi gọi hãng; 🚨 **cấm** `Guid.NewGuid()` cho mỗi lần gọi (kế thừa `XC-002` BR-XC-002-007). Vi phạm ⇒ `TC-07`, `TC-32` đỏ.<br>• **BR-SHP-001-004**: Gọi lại với **cùng** `ClientOrderCode` **không** được tạo vận đơn thứ hai; nếu hãng báo đã tồn tại ⇒ **dùng lại** kết quả cũ.<br>• **BR-SHP-001-005**: **Mọi** chuyển trạng thái `Shipment` **phải** qua guard ở §3.2; guard sai ⇒ **NÉM LỖI có mã**; **cấm** gán trực tiếp (bài học `Order.SetStatus` đã bị **xoá** vì là phép gán trần — `modules/Sales/eCommerce.Sales.Domain/Order.cs:359-365`; nay đi qua `ChuyenThucHien` `Order.cs:370`).<br>• **BR-SHP-001-006**: Trạng thái **hấp thụ** của `SHP-001`: `Cancelled`, `SubmitFailed` (sau khi vượt trần thử), `Created` **chỉ** được huỷ theo A4, **không** được sửa sang trạng thái khác.<br>• **BR-SHP-001-007**: Tiền **phải** là `decimal` + `Money`; 🚨 **cấm** so sánh tiền bằng `==` trên `decimal` trần; so bằng `Money` (`CompareTo`/`Equals`/`==` của `Money` kiểm cả loại tiền — `shared/eCommerce.Kernel/Money/Money.cs:112-135`; `XC-001` BR-XC-001-003).<br>• **BR-SHP-001-008**: Ra biên hãng, tiền truyền dưới dạng **số nguyên** (VND); 🚨 **cấm** `double`/`float` trong mọi biểu thức tiền (`XC-001` BR-XC-001-007/008). `Money` **không** có `ToMinorUnits()` và **không** tự làm tròn (`Money.cs:19-21`) ⇒ hàm đổi `decimal → long` + chính sách làm tròn (COD lẻ ⇒ NÉM hay làm tròn) **chưa chốt** — Q17.<br>• **BR-SHP-001-009**: Phí dùng để **chốt** là phí **hãng báo tại thời điểm tạo vận đơn**; bảng giá nội bộ chỉ để **ước tính**; lệch ⇒ **ghi nhận**, 🚨 **cấm** tự sửa số của hãng (ADR-04; kế thừa `SHP-002` BR-SHP-002-007).<br>• **BR-SHP-001-010**: **Mọi** thao tác ghi **phải** qua **Unit of Work** của ABP (host đã bật `TransactionBehavior.Auto` — `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:91`–`94`); với `SHP-001` mỗi pha là một UoW tường minh theo BR-002, không dùng UoW ngầm của app service.<br>• **BR-SHP-001-011**: 🚨 **Cấm** hard-code endpoint, mã dịch vụ, mã quận/huyện, hay token của hãng trong code nghiệp vụ — **mọi thứ** thuộc `ShippingHubOptions` hoặc **dữ liệu** (`ShpCarriers`/`ShpShippingServices`).<br>• **BR-SHP-001-012**: Mọi lỗi hãng trả về **phải** có log có cấu trúc + mã lỗi nghiệp vụ; 🚨 **cấm** log token/secret/payload chứa PII (kế thừa `XC-005` BR-XC-005-005/013 và hàm che `PaymentTransaction.MaskSensitiveData` — `modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:313`).<br>• **BR-SHP-001-013**: Mọi lời gọi hãng **phải** có **timeout hữu hạn** và retry **có trần** + backoff; 🚨 **cấm** retry vô hạn.<br>• **BR-SHP-001-014**: 🚨 **Cấm** expose API/method **đổi COD**. Đổi COD ở GHN **bắt buộc OTP** ⇒ thuộc quy trình **thủ công** của `SHP-003` (`docs/implementation-plan.md:199`, `:426`; `docs/business-modules-proposal.md:548`).<br>• **BR-SHP-001-015**: Token/secret của hãng **cấm** commit; đọc từ cấu hình/secret ngoài (`XC-004` BR-XC-004-012).<br>• **BR-SHP-001-016**: **Mọi** app service **phải** có `[Authorize]` ở **cấp class** (`XC-004` BR-XC-004-001); `SHP-001` **không** có endpoint vô danh.<br>• **BR-SHP-001-017**: **Idempotency ở MỌI biên vào** (API tạo vận đơn, job thử lại, và callback của `SHP-002`): **cả** `IIdempotencyGuard` (`XC-002`) **lẫn** unique index ở DB.<br>• **BR-SHP-001-018**: Nhãn metric **chỉ** lấy từ `eCommerceMetricTags.Allowed` (`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:212`); 🚨 **cấm** `orderId`/`trackingCode`/`sku`/`correlationId` làm nhãn (`XC-005` BR-XC-005-009).<br>• **BR-SHP-001-019**: Thêm/đổi tên metric **phải** cập nhật **đồng thời** `eCommerceMetricNames.All` (`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:81`), instrument trong `eCommerceMetrics` (`:237`) **và** `XC-005` §5.1 — trong **cùng** PR; lệch ⇒ `MetricsTests.TC06_phat_ra_DUNG_bang_metric_da_dang_ky` (`test/eCommerce.Tests/Observability/MetricsTests.cs:39`) **đỏ** (test **không** còn đếm cứng số `12` — `MetricsTests.cs:64`).<br>• **BR-SHP-001-020**: **Mã địa chỉ hãng** (tỉnh/huyện/phường của hãng) **chỉ** lấy qua `Address.Application.Contracts`: đường **theo đơn** dùng cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`) với **đúng** `SchemaVersion` + cấp + mã của snapshot; 🚨 **cấm** `SHP` tự truy vấn `AddrCarrierMappings`/giữ bảng ánh xạ riêng, **cấm** tra chéo phiên bản (không thử `SchemaVersion` kia khi không thấy), **cấm** tra theo `ShippingAddressId`/`IAddressDirectory.TimTheoIdAsync` ở đường theo đơn, **cấm** ShippingHub tham chiếu `eCommerce.Address.Application`/`.Domain`; mọi lỗi tra (`Address:0001`/`0005`/`0501`/`0502`/`0504`, không duy nhất) ⇒ `eCommerce:Shipping:0014`, **không** gọi hãng, **không** chọn ánh xạ đầu/mới nhất. Vi phạm ⇒ `TC-34`, `TC-46`…`TC-48` đỏ.<br>• **BR-SHP-001-021**: **Địa chỉ/người nhận** của vận đơn (theo đơn) **chỉ** lấy từ **snapshot giao hàng trên đơn** qua hợp đồng Sales (`D-108`); 🚨 `CreateShipmentDto` và `EstimateOrderShippingFeeDto` (ước tính theo đơn) **cấm** nhận `recipientName`/`recipientPhone`/`addressLine`/mã địa chỉ/`AddressId`/`OurAdministrativeUnitId`; riêng `CreateShipmentDto` **cấm** thêm `codAmount`/`currency`/`items` — COD, tiền tệ và dòng hàng gửi hãng **chỉ** lấy từ đơn qua hợp đồng Sales (`Order.CodAmount`, `Order.cs:121`, ghi qua `DatVanChuyen` `:292`; `D-108` (2)); ShippingHub **cấm** tham chiếu `Sales.Domain`/`Sales.EntityFrameworkCore`; đơn thiếu snapshot ⇒ NÉM `DonChuaCoDiaChiGiao` trước Pha 1. Vi phạm ⇒ `TC-36`…`TC-39`, `TC-52` đỏ.<br>• **BR-SHP-001-022** (`D-109`): `ClientOrderCode` gửi hãng **phải** là kết quả của **một** hàm thuần `TaoClientOrderCode(orderId, generation)` ở `ShippingHub.Domain` (`"{orderId:N}-{generation}"`, ASCII, ≤ 43 ký tự), dùng chung **mọi** adapter; `Generation` **chỉ** tăng khi tạo `Shipment` mới (§3.2 #1), retry cùng `Shipment` **không** đổi `Generation`/mã; 🚨 **cấm** ghép `attemptNo`/`SubmitAttemptCount` vào mã gửi hãng; khoá `IIdempotencyGuard` của một lần gửi **phải** kèm `generation` (§7.3); adapter chỉ kiểm độ dài theo giới hạn hãng (GHN 50) và **NÉM** khi vượt — **cấm** cắt/băm lại. Vi phạm ⇒ `TC-07`, `TC-40`…`TC-45` đỏ. |
| **Non-Functional Requirement** | • **NFR-SHP-001-001**: `GetFeeAsync` khi hãng phản hồi bình thường ⇒ **p95 < 2 s** (không tính độ trễ mạng của hãng).<br>• **NFR-SHP-001-002**: `CreateShipmentAsync` end-to-end ⇒ **p95 < 5 s**; **p99 < 12 s**.<br>• **NFR-SHP-001-003**: **0** vận đơn trùng khi gọi `CreateShipmentAsync` **1.000 lần** cho cùng một đơn (integration test).<br>• **NFR-SHP-001-004**: **0** transaction DB mở trong lúc chờ HTTP tới hãng — UoW ngầm của app service chỉ được ở dạng `isTransactional: false` (test quét attribute + test HTTP thật `TC-09`; ADR-02, BR-002).<br>• **NFR-SHP-001-005**: **100 %** dòng log của luồng gọi hãng có `CorrelationId` (`XC-005` NFR-XC-005-002).<br>• **NFR-SHP-001-006**: **0** secret/token hãng trong repo (quét mã nguồn).<br>• **NFR-SHP-001-007**: **0** bản ghi tenant khác lọt vào truy vấn (integration test 2 tenant — ⚠️ **cần PostgreSQL**, xem `XC-004` F1).<br>• **NFR-SHP-001-008**: **0** metric phát ra ngoài `eCommerceMetricNames.All` (`test/eCommerce.Tests/Observability/MetricsTests.cs:50`).<br>• **NFR-SHP-001-009**: Cấu hình mặc định: timeout **10 s**/lời gọi; retry tối đa **3** lần, backoff **1 s → 5 s → 25 s**; trần thử tạo vận đơn **5** (kế thừa khuyến nghị retry của `NOT-002` NFR-NOT-002-006 — cần chốt, §15.1 Q6).<br>• **NFR-SHP-001-010**: **0** literal endpoint/mã hãng trong code nghiệp vụ (test grep trong CI — `docs/design-doc-guidelines.md:154`). |

---

## 2. Sơ đồ Use Case

```mermaid
flowchart LR
    classDef actor fill:#d4edda,stroke:#155724,stroke-width:2px;
    classDef usecase fill:#e2f4f6,stroke:#17a2b8,stroke-width:2px;
    classDef note fill:#fffbe6,stroke:#ffc107,stroke-width:1px,color:#5c4a00;

    KH(("👤<br/>Khách hàng"))
    KHO(("👤<br/>Nhân viên kho"))
    QT(("👤<br/>Quản trị"))
    GHN(("🚚<br/>GHN<br/>hệ thống ngoài"))

    subgraph SHP["📦 ShippingHub — SHP-001"]
        direction TB
        UC1(["«ước tính phí vận chuyển»"])
        UC2(["«lấy phí thật từ hãng»"])
        UC3(["«tạo vận đơn tại hãng»"])
        UC4(["«nhận mã vận đơn»"])
        UC5(["«huỷ vận đơn trước khi lấy»"])
        UC6(["«cấu hình hãng & bảng giá»"])
        class UC1,UC2,UC3,UC4,UC5,UC6 usecase;
    end

    N1["<b>Nguyên tắc (ADR-02):</b><br/>«gọi hãng NGOÀI transaction»<br/>«DB là hàng rào cuối»"]
    class N1 note;

    KH --- UC1
    KHO --- UC3
    KHO --- UC5
    QT --- UC6

    UC1 -. "«include»" .-> UC2
    UC3 -. "«include»" .-> UC4

    UC3 -.- N1
    GHN -.- UC3
    GHN -.- UC5
```

**Chú giải:**

| Ký hiệu | Ý nghĩa |
|---|---|
| ⭕ Xanh lá | Actor (khách, kho, quản trị) |
| 🟦 Bầu dục xanh dương | Use case |
| 🟨 Khung vàng | Note (nguyên tắc thiết kế) |
| `-.«include».->` | Quan hệ bao hàm |
| `-.-` | Tham chiếu (hãng ngoài / note) |

---

## 3. Sơ đồ trạng thái (state machine)

### 3.1 Vòng đời **tạo** vận đơn (`Shipment.Status` giai đoạn `SHP-001` sở hữu)

```mermaid
stateDiagram-v2
    [*] --> Draft : «SHP-001 Pha 1 ghi ý định»
    Draft --> Submitting : «Pha 2 ghi bằng chứng trước khi gọi hãng»
    Submitting --> Created : «hãng trả mã vận đơn»
    Submitting --> SubmitFailed : «hãng từ chối hoặc timeout»
    SubmitFailed --> Submitting : «job thử lại, còn dưới trần»
    Draft --> Cancelled : «CSKH huỷ trước khi gọi»
    Created --> Cancelled : «huỷ TRƯỚC khi hãng lấy hàng»
    Created --> [*] : «bàn giao cho SHP-002 theo dõi trạng thái»
    Cancelled --> [*]
    SubmitFailed --> [*] : «vượt trần thử, cần người xử lý»
```

> ⚠️ **Phân chia trách nhiệm rõ ràng:** `SHP-001` sở hữu **giai đoạn tạo** (`Draft` → `Created`).
> Từ `Created` trở đi, vòng đời **thuộc `SHP-002`** (`docs/features/shipping/shp-002-webhook-trang-thai-van-don.md` §3.1):
> `Created → PickedUp → InTransit → … → Delivered/Returned`. **Hai tài liệu KHÔNG được định nghĩa hai state machine
> chồng nhau cho cùng một enum** — enum `ShipmentStatus` **dùng chung**, `SHP-001` chỉ ghi được các giá trị trong
> hình trên (xem §5.2 và §15.2 **F2**).
>
> 📌 **Không có cạnh `Submitting → Cancelled`** (đã bỏ ở v0.5): `Submitting` là trạng thái "hãng có thể đã tạo thật mà ta chưa biết" (ADR-02); cho huỷ ở đó rồi cho tạo `Generation + 1` (§3.2 #1) sẽ sinh hai vận đơn thật. Gỡ `Submitting` kẹt: job/A2 (§12), đường thủ công: Q19.

### 3.2 Bảng chuyển trạng thái & guard — **mọi chuyển sai PHẢI NÉM LỖI, không im lặng**

| # | Từ | Sang | Guard | Ai được phép chuyển | Nếu vi phạm |
|---|---|---|---|---|---|
| 1 | `[*]` | `Draft` | Đơn ∈ tenant hiện tại **và** (đơn chưa có `Shipment` nào, **hoặc** `Shipment` mới nhất của đơn ở trạng thái hấp thụ: `Cancelled` (chỉ sinh từ `Draft` — hãng chưa từng được gọi — hoặc từ `Created` — hãng đã xác nhận huỷ; **không** có cạnh từ `Submitting`, nên `Cancelled` luôn là "chắc chắn không còn vận đơn hiệu lực ở hãng"), hoặc `SubmitFailed` đã vượt trần (E5) **và** chắc chắn hãng **chưa** tạo đơn — mọi attempt là `Rejected` **hoặc** tra hãng theo `ClientOrderCode` cũ xác nhận không có). `Generation = 1 + max` của mọi `Shipment` cùng `(TenantId, OrderId)` (mọi hãng, mọi trạng thái) — `D-109` | app service (Pha 1) | (i) Còn `Shipment` hiệu lực (không hấp thụ) hoặc hai Pha 1 đồng thời (UNIQUE `23505`) ⇒ trả vận đơn hiện có (**A5**) — không phải lỗi. (ii) `Shipment` mới nhất là `SubmitFailed` vượt trần mà có attempt `TransientFailure` chưa tra được ⇒ **NÉM** `VanDonCuChuaRoKetQua` (§6.3): 0 `Shipment` mới, 0 lời gọi hãng (khớp §12 "KHÔNG tự tạo lại mù") |
| 2 | `Draft` | `Submitting` | `ClientOrderCode` **đã có** và khác rỗng; hãng **đã** cấu hình | app service (Pha 2) | `eCommerce:Shipping:0013` (**E1**) |
| 3 | `Submitting` | `Created` | Hãng trả `ProviderTrackingCode` **khác rỗng** | app service (Pha 3) | `eCommerce:Shipping:0015` (**E3**) — hãng "thành công" mà không có mã vận đơn là **lỗi** |
| 4 | `Submitting` | `SubmitFailed` | Hãng từ chối (4xx) **hoặc** timeout/5xx **hoặc** `UPDATE … WHERE` | app service (Pha 3) | `eCommerce:Shipping:0017` (**E7**) khi `WHERE` 0 dòng |
| 5 | `SubmitFailed` | `Submitting` | `SubmitAttemptCount < MaxSubmitAttempts` (job **không** nhặt `Draft` — Q15) | **job thử lại** (không phải HTTP) | Vượt trần ⇒ `eCommerce:Shipping:0016` (**E5**) + cảnh báo |
| 6 | `Draft`/`Created` | `Cancelled` | 🚨 Hãng **CHƯA** lấy hàng (`LastProviderEventAtUtc` chưa có sự kiện `PickedUp` — cột này do `SHP-002` cập nhật, `SHP-002` §5.2). Từ `Created`: hãng phải xác nhận huỷ (§7.3). **`Submitting` không nằm trong "Từ"** | CSKH có quyền `.Cancel` | `eCommerce:Shipping:0017` + log `Error` (**A4**) — gồm cả huỷ khi `Submitting`: 0 lời gọi hãng, trạng thái không đổi (`TC-51`) |
| 7 | `Cancelled` | **BẤT KỲ** | — | **KHÔNG AI** (trạng thái **hấp thụ**) | `eCommerce:Shipping:0017` |
| 8 | `Created` | `Cancelled` | **chỉ** khi chưa `PickedUp` (giống #6) | CSKH có quyền | `0017` |
| 9 | Bất kỳ | *(không đổi)* | `ClientOrderCode` đã tồn tại **và** hãng báo trùng | — | **Không phải lỗi**: dùng lại kết quả (**A1**, **A5**) + metric `shipping_shipment_duplicate_ignored_total` |
| 10 | Bất kỳ | *(không đổi)* | `cod_amount` **khác** giá trị đã gửi hãng | — | 🚨 `eCommerce:Shipping:0018` (**E9**) — **cấm** tự động hoá (ADR-07) |

> 📌 **Bài học kế thừa** (`XC-006` §3 và `SHP-002` §3.2): guard **phải ném lỗi**, không được "im lặng bỏ qua".
> Ngoại lệ duy nhất được phép "im lặng" ở đây là **#9** — và nó **bắt buộc** ghi metric + dòng attempt.

---

## 4. Sequence Diagram & luồng dữ liệu

### 4.1 Sequence — 3 pha tạo vận đơn (gọi hãng **ngoài** transaction)

```mermaid
sequenceDiagram
    participant KHO as Nhân viên kho
    participant APP as ShipmentAppService
    participant LOCK as IAbpDistributedLock
    participant DB as PostgreSQL
    participant PROV as IShippingCarrierProvider
    participant GHN as API GHN
    participant BUS as Outbox

    KHO->>APP: «POST tao van don cho don hang»
    APP->>DB: «Pha 1 UoW 1: tao Shipment Draft + sinh ClientOrderCode»
    Note over APP,DB: «COMMIT truoc khi goi hang»
    APP->>LOCK: «TryAcquire shipping:shipment-create:tenant:order (timeout huu han)»
    alt «khong gianh duoc khoa»
        LOCK-->>APP: «null»
        APP-->>KHO: «409 eCommerce:Lock:0001»
    else «gianh duoc khoa»
        LOCK-->>APP: «handle»
        APP->>DB: «Pha 2 UoW 2: Status Submitting + AttemptCount++»
        Note over APP,DB: «COMMIT - bang chung y dinh»
        APP->>PROV: «CreateShipmentAsync ClientOrderCode + dia chi + can nang + cod_amount»
        PROV->>GHN: «goi API tao van don (timeout 10s, retry co tran)»
        alt «hang tra ma van don»
            GHN-->>PROV: «ma van don + phi that»
            PROV-->>APP: «CreateShipmentResult»
            APP->>DB: «Pha 3 UoW 3: Created + TrackingCode + ShippingFee that»
            APP->>BUS: «ShipmentCreatedEto CUNG transaction»
            APP-->>KHO: «200 ShipmentDto»
        else «hang tu choi hoac timeout»
            GHN-->>PROV: «loi»
            PROV-->>APP: «FailureKind + FailureCode»
            APP->>DB: «Pha 3 UoW 3: SubmitFailed + attempt log loi»
            APP-->>KHO: «loi co ma eCommerce:Shipping:0015»
        end
        APP->>LOCK: «Dispose handle - nha khoa»
    end
```

### 4.2 Ba pha — phạm vi UoW, ghi gì, vì sao

| Pha | UoW | Ghi gì | Vì sao phải **commit** ở đây | Nếu chết ngay sau pha này |
|---|---|---|---|---|
| **P1 — ý định** | UoW #1 (ngắn) — `Begin(requiresNew: true, isTransactional: true)` + `CompleteAsync` | `INSERT Shipment` (`Draft`) + `Generation` (`1 + max` cùng đơn) + `ClientOrderCode = TaoClientOrderCode(orderId, generation)` **cùng UoW** (`D-109`) | `ClientOrderCode` **phải bền vững TRƯỚC** khi gọi hãng ⇒ mọi lần thử lại đọc **đúng** giá trị đó (BR-003, BR-022) | DB có `Shipment` `Draft`, hãng **chưa** biết gì ⇒ **an toàn** (không có vận đơn thật). ⚠️ **Chưa có cơ chế đi tiếp**: A5 trả bản `Draft` như "hiệu lực", job chỉ quét `Submitting`/`SubmitFailed` (§3.2 #5); E6 xảy ra **sau** Pha 1 nên cũng để lại `Draft` — Q15 |
| **P2 — bằng chứng** | UoW #2 (ngắn) — `Begin(requiresNew: true, isTransactional: true)` + `CompleteAsync` | `UPDATE Status = Submitting`, `SubmitAttemptCount++` (`WHERE Status = Draft`) — `attemptNo` = `SubmitAttemptCount` đã commit, **chỉ** ở nhật ký attempt + khoá guard, **không** vào `ClientOrderCode` | Nếu chết **trong lúc** chờ hãng, ta **biết** mình đã gọi ⇒ job quét để tra lại, **không** tạo mù (ADR-02) | DB có `Submitting` "mồ côi" ⇒ **job quét** `Submitting` quá N phút (§12) |
| **P3 — kết quả** | UoW #3 (ngắn) — `Begin(requiresNew: true, isTransactional: true)` | `UPDATE` kết quả + `INSERT ShpShipmentCreateAttempts` + **outbox** ETO (`WHERE Status = Submitting`) | Outbox **phải** cùng transaction với lần đổi trạng thái (`XC-002` BR-XC-002-001) | DB có `Submitting`; lần thử sau **phải** hỏi hãng trước (A2) |
| *(ngoài transaction)* | UoW ngầm của app service ở dạng `isTransactional: false` | **lời gọi HTTP tới GHN** | **Không** được nằm trong transaction (BR-002, NFR-004) | — |

> 🚨 **UoW ngầm của ABP (BR-002):** host đặt `TransactionBehavior.Auto` (`eCommerceHttpApiHostModule.cs:94`) ⇒ nếu `CreateShipmentAsync` không đặt `[UnitOfWork(isTransactional: false)]`, ABP mở UoW **có transaction** quanh cả app service, và `Begin()` mặc định `requiresNew: false` chỉ **nhập** vào nó: Pha 1/Pha 2 **chưa commit** khi gọi hãng, và nếu request lỗi sau đó thì ta rollback mất dấu vết trong khi hãng đã tạo vận đơn. Tiền lệ vá cùng lỗi: `PaymentAppService.cs:556-561`. Áp dụng cho `CreateShipmentAsync`, `CancelShipmentAsync` và job thử lại.

> 🔴 **Ghi rõ cho tài liệu khác:** `SHP-002` §12 B2 yêu cầu outbox trong **cùng** transaction với lần đổi trạng thái.
> `SHP-001` áp **cùng** quy tắc cho `ShipmentCreatedEto`. ✅ Outbox/Inbox **đã bật** trên `eCommerceDbContext`
> (`src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:86-87`) ⇒ điều kiện tiên quyết đã thoả; phát ETO ngoài outbox vi phạm
> `XC-002` BR-XC-002-002.

---

## 5. Mô hình dữ liệu

### 5.1 ERD

**Không áp dụng — lý do:** tài liệu này đã dùng **3 sơ đồ Mermaid** (§2, §3.1, §4.1) — chạm **giới hạn tối đa 3**.
Thay thế bằng **Data Dictionary** đầy đủ ở §5.2 (cột + index + ràng buộc), đúng cách `SHP-002` §5.1 đã làm.

> ⚠️ **Chưa có bảng nào tồn tại trong code.** Đã kiểm bằng `grep` toàn repo cho `*.cs`:
> `ShippingHub|Shipment|Carrier|CodRecord` ⇒ **0** kết quả trong `src/`, `modules/`, `test/`.
> `eCommerce.slnx` (đối chiếu 18/11/2026) có `Address`, `Customers`, `Sales`, `Pos`, `Reporting`, `Reconciliation`, `Inventory`, `Catalog`,
> `ChannelHub`, `EInvoice`… **không** có `ShippingHub`; `ls modules/` không có thư mục Shipping*.
> Những thứ liên quan đang tồn tại:
> • `Order.ShippingTotal` — `modules/Sales/eCommerce.Sales.Domain/Order.cs:162`, mapping `HasPrecision(18,4)` tại
>   `modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs:36`;
> • các trường vận chuyển kiểu Nhanh **trên `Order`**: `CarrierCode`, `TrackingNumber`, `CodAmount`, `ShippingFeeCustomer`, `ShippingFeeActual`,
>   `WeightGram` (`Order.cs:110-130`), ghi **một lần cả sáu** qua `DatVanChuyen` (`Order.cs:292-334`). `SHP-001` **không** ghi các trường này
>   (§7.1); `CodAmount` là nguồn COD gửi hãng (BR-021), `WeightGram` là một ứng viên nguồn cân nặng (Q5);
> • `Product.WeightGram` (`int?`) — `modules/Catalog/eCommerce.Catalog.Domain/Product.cs:78` (*"dùng cho phí vận chuyển; ưu tiên hơn khối lượng
>   của biến thể cũ"*) và `ProductVariant.WeightInGrams` (`Product.cs:512`, đường cũ — `D-07`);
> • khuôn mã hãng: `CarrierAddressMappingAppService.TraMaHangAsync` (`modules/Address/eCommerce.Address.Application/CarrierAddressMappingAppService.cs:63`,
>   chiều mã hãng → đơn vị ta); chiều ngược `ResolveCarrierAddressCodeAsync` (`D-89`) ⬜ chưa có.
> ⇒ Toàn bộ 5 bảng dưới đây là **kế hoạch GĐ2 — chưa có trong code**.

### 5.2 Bảng & cột (Data Dictionary)

**Bảng `ShpCarriers`** — danh mục hãng vận chuyển (DỮ LIỆU, không hard-code — BR-011)

| Cột | Kiểu | Null? | Nguồn / ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính | |
| `TenantId` | `uuid` | ✔ | `null` = dùng chung cho mọi tenant | `IMultiTenant`; ⚠️ `NULL` = 1 tenant (xem `XC-004` F1) |
| `ProviderCode` | `varchar(32)` | ✖ | `ghn`, `ghtk`, `viettelpost`… | khớp `Provider` trong log `XC-005` §5.2 |
| `DisplayName` | `varchar(256)` | ✖ | tên hiển thị | |
| `IsEnabled` | `bool` | ✖ | hãng có đang dùng không | **fail-closed**: `false` ⇒ từ chối (**E1**) |
| `AddressModel` | `int` | ✖ | `TwoLevel=0` · `ThreeLevel=1` · `Both=2` | ⭐ **ADR-06** — GHN hỗ trợ cờ "địa chỉ mới" còn GHTK **chưa xác minh** (`docs/business-modules-proposal.md:513`, `:546`) |
| `ConfigKey` | `varchar(64)` | ✖ | khoá tra trong `ShippingHubOptions.Providers` | **không** lưu token trong DB (BR-015) |

**Bảng `ShpShippingServices`** — dịch vụ của hãng (DỮ LIỆU đồng bộ từ hãng)

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính | |
| `TenantId` | `uuid` | ✔ | tenant | |
| `CarrierId` | `uuid` | ✖ | hãng | tham chiếu nội bộ module |
| `ProviderServiceId` | `varchar(64)` | ✖ | mã dịch vụ **của hãng** | ⚠️ **giá trị thật CHƯA kiểm chứng** (§15.1 Q2) |
| `Name` | `varchar(256)` | ✖ | tên dịch vụ | |
| `IsEnabled` | `bool` | ✖ | dùng hay không | mặc định `true` |
| `EffectiveFrom` / `EffectiveTo` | `date` / `date` | ✖ / ✔ | khung hiệu lực | kế thừa khuôn `XC-006` (**cấm** sửa đè bản ghi đang hiệu lực — `TaxErrorCodes.CannotModifyActive`, `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:28`) |

**Bảng `ShpShippingRates`** — bảng giá nội bộ **để ƯỚC TÍNH** (ADR-04). 🚨 **Đây KHÔNG phải nguồn sự thật.**

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính | |
| `TenantId` | `uuid` | ✔ | tenant | |
| `ShippingServiceId` | `uuid` | ✖ | dịch vụ | |
| `FromProvinceCode` / `ToProvinceCode` | `varchar(32)` | ✖ / ✔ | cấp tỉnh | `null` `ToProvinceCode` = mọi tỉnh |
| `WeightFromGrams` / `WeightToGrams` | `int` | ✖ / ✔ | bậc cân nặng | `null` = không giới hạn trên |
| `BaseFee` / `BaseFeeCurrency` | `numeric(18,4)` / `char(3)` | ✖ / ✖ | phí cơ bản | ⚠️ `decimal`, **không bao giờ** `double` (`XC-001` BR-008) |
| `ExtraFeePerKg` | `numeric(18,4)` | ✔ | phụ phí mỗi kg vượt | |
| `EffectiveFrom` / `EffectiveTo` | `date` / `date` | ✖ / ✔ | khung hiệu lực | **cấm** sửa đè |
| `Source` | `int` | ✖ | `Manual=0` · `SyncedFromCarrier=1` | để biết số liệu đến từ đâu |
| `SyncedAtUtc` | `timestamptz` | ✔ | lần đồng bộ gần nhất | |

**Bảng `ShpShipments`** — vận đơn. ⭐ **Do `SHP-001` tạo; `SHP-002` cập nhật trạng thái** (ADR-09).

> 🔴 **BẮT BUỘC KHỚP `SHP-002` §5.2.** Bảng dưới đây liệt kê **các cột `SHP-001` cần** và đánh dấu cột nào
> **đã** được `SHP-002` định nghĩa. Nếu `SHP-001` cần thêm cột **mới** ⇒ **sửa `SHP-002` trước** (quy tắc vàng
> `docs/design-doc-guidelines.md:102`), **không** tự thêm.

| Cột | Kiểu | Null? | Nguồn / ý nghĩa | Ai ghi | Ghi chú |
|---|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính | SHP-001 | |
| `TenantId` | `uuid` | ✔ | tenant | SHP-001 | `IMultiTenant` |
| `OrderId` | `uuid` | ✖ | đơn hàng | SHP-001 | tham chiếu **mềm** sang Sales (**cấm** module gọi app) |
| `OrderNumber` | `varchar(64)` | ✔ | số đơn (snapshot) | SHP-001 | ⭐ **CỘT MỚI so với `SHP-002`** — để hiển thị/grep mà không phải join; **phải sửa `SHP-002` trước** (§15.2 **F2**) |
| `ChannelCode` | `varchar(32)` | ✔ | kênh bán, lấy từ **đơn** | SHP-001 | `null` với đơn website — **KHÔNG** lấy từ payload hãng |
| `ProviderCode` | `varchar(32)` | ✖ | mã hãng | SHP-001 | |
| `Generation` | `int` | ✖ | ⭐ **CỘT MỚI** (`D-109`) — số thứ tự `Shipment` của đơn: `1 + max(Generation)` của mọi `Shipment` cùng `(TenantId, OrderId)`, gán ở Pha 1; chỉ tăng khi tạo `Shipment` **mới** | SHP-001 (P1) | CHECK `Generation >= 1`; **bất biến** sau khi lưu; ⬜ **phải sửa `SHP-002` §5.2 trước** (ADR-09, §15.2 **F8**) |
| `ClientOrderCode` | `varchar(64)` | ✖ | ⭐ **mã đơn phía TA gửi sang hãng** = `TaoClientOrderCode(OrderId, Generation)` = `"{orderId:N}-{generation}"` (`D-109`, BR-022) | SHP-001 (P1) | 🚨 khoá chống trùng **ổn định** (BR-003); unique theo `(TenantId, ProviderCode)`; ≤ 43 ký tự (GHN tối đa 50) |
| `ProviderTrackingCode` | `varchar(64)` | ✔ | **mã vận đơn do hãng cấp** | SHP-001 (P3) | `null` cho tới khi tạo thành công |
| `Status` | `int` | ✖ | trạng thái nội bộ (**enum dùng chung với `SHP-002`**) | cả hai | mặc định `Draft`. Enum lưu số ⇒ **chỉ thêm giá trị ở cuối**, không chèn/đánh số lại (`D-17`); áp cho cả `ShpShipmentCreateAttempts.Result`. ⚠️ **Giá trị số của `ShipmentStatus` chưa được gán**, và `SHP-002` §5.2 đang ghi mặc định `Created = 0` (`shp-002-webhook-trang-thai-van-don.md:320`) — xung đột với mặc định `Draft`: Q16, F11. Mọi CHECK ở tài liệu này **không** dựa vào số cụ thể |
| `StatusSource` | `int` | ✖ | `Webhook=0` · `Reconciliation=1` · `Manual=2` · `Pull=3` | SHP-002 | `SHP-001` **không** ghi cột này (trừ `Pull` nếu có) |
| `ConfirmedAtUtc` | `timestamptz` | ✔ | xác nhận trạng thái ảnh hưởng tiền | SHP-002 | ⭐ **BR-004 của `SHP-002`** — `SHP-001` **không** ghi |
| `ProviderStatusCode` | `varchar(64)` | ✔ | mã trạng thái thô của hãng | SHP-002 | |
| `LastProviderEventAtUtc` | `timestamptz` | ✔ | mốc sự kiện hãng lớn nhất đã áp | SHP-002 | ⭐ **guard #6/#8 §3.2 dùng cột này** để biết hãng **đã lấy hàng chưa** |
| `SlaDueAtUtc` | `timestamptz` | ✔ | hạn giao dự kiến | SHP-001 (P3, nếu hãng trả) | job đối soát của `SHP-002` dùng |
| `ReconcileAttemptCount` / `LastReconciledAtUtc` | `int` / `timestamptz` | ✖ / ✔ | số lần đối soát | SHP-002 | |
| `CodAmount` / `CodCurrency` | `numeric(18,4)` / `char(3)` | ✖ / ✖ | **số tiền thu hộ ĐÃ GỬI hãng** = bản chụp `Order.CodAmount` đọc qua hợp đồng Sales lúc Pha 1 (BR-021; `Order.CodAmount` là `decimal?` — trường hợp `null`: Q18) | SHP-001 (P3) | ⚠️ `decimal`; 🚨 **ghi một lần**, **cấm** update (BR-014) |
| `ShippingFee` / `ShippingFeeCurrency` | `numeric(18,4)` / `char(3)` | ✖ / ✖ | **phí hãng BÁO** (nguồn sự thật) | SHP-001 (P3) | ⭐ `SHP-002` §5.2 chỉ có `ShippingFee` — **cần bổ sung cột currency** (§15.2 **F2**) |
| `EstimatedShippingFee` | `numeric(18,4)` | ✔ | phí **ước tính** đã hiển thị cho khách | SHP-001 | ⭐ **CỘT MỚI** — để phát hiện lệch phí (**E8**) |
| `SubmitAttemptCount` | `int` | ✖ | số lần đã gọi hãng tạo vận đơn | SHP-001 | mặc định `0`; trần `MaxSubmitAttempts` = **5** |
| `TotalWeightGrams` | `int` | ✔ | tổng cân nặng đã gửi hãng (gram — không phải "số lượng") | SHP-001 | `Σ Product.WeightGram × Quantity` (`Quantity` `decimal(18,3)` — `D-31`), tính bằng `decimal` rồi làm tròn (Q5) |
| *(nhóm cột snapshot giao — tên cột chưa chọn)* | text/`int` (nullable) | ✔ | ⭐ **CỘT MỚI** — **bản sao** snapshot giao hàng đã gửi hãng: tên + SĐT người nhận, `SchemaVersion`, mã/tên tỉnh-huyện-xã, `SoNhaDuong`, `DiaChiDayDu` (`D-108` (6), `D-77`) | SHP-001 (P1) | ⬜ kế hoạch — **phải sửa `SHP-002` §5.2 trước** (ADR-09, §15.2 **F7**). Là PII ⇒ thuộc nợ ẩn danh `ADDR-004` ADR-11/B11 (`D-108` N3) |
| `FailureCode` / `FailureReason` | `varchar(64)` / `varchar(2048)` | ✔ / ✔ | lỗi gần nhất | SHP-001 | ⚠️ **đã che** dữ liệu nhạy cảm (BR-012) |
| `CreatedAtUtc` | `timestamptz` | ✖ | thời điểm tạo bản ghi | SHP-001 | |
| `RowVersion` | `xmin` (ánh xạ `uint`) | ✖ | optimistic concurrency | cả hai | `IsRowVersion()` ⇒ cột hệ thống `xmin` (`XC-002` §5.2.3) |

**Bảng `ShpShipmentCreateAttempts`** — ⭐ **NHẬT KÝ APPEND-ONLY** cho mỗi lần gọi hãng (do `SHP-001` sở hữu)

| Cột | Kiểu | Null? | Ý nghĩa | Ghi chú |
|---|---|---|---|---|
| `Id` | `uuid` | ✖ | khoá chính | |
| `TenantId` | `uuid` | ✖ | tenant | **bắt buộc** |
| `ShipmentId` | `uuid` | ✖ | vận đơn | |
| `AttemptNo` | `int` | ✖ | dòng **thử tạo** (`Result` ∈ `Created`/`Rejected`/`TransientFailure`/`DuplicateIgnored`): `AttemptNo` = `SubmitAttemptCount` đã commit ở Pha 2 (≥ 1). Dòng **huỷ** (`Result = Cancelled`): `AttemptNo = 0` — không thuộc chuỗi thử tạo, tối đa **một** dòng vì `Cancelled` hấp thụ và dòng chỉ ghi khi hãng đã xác nhận huỷ (A4) | ⭐ nằm trong unique index (idempotency) — dòng huỷ mang `0` nên không đụng dòng `Created` cùng `Shipment`; CHECK `(Result = 4 AND AttemptNo = 0) OR (Result <> 4 AND AttemptNo >= 1)`; **không** vào `ClientOrderCode` (`D-109`) |
| `ClientOrderCode` | `varchar(64)` | ✖ | mã đã gửi hãng | **phải trùng** `ShpShipments.ClientOrderCode` |
| `Result` | `int` | ✖ | `Created=0` · `Rejected=1` · `TransientFailure=2` · `DuplicateIgnored=3` · `Cancelled=4` | |
| `FailureCode` | `varchar(64)` | ✔ | mã lỗi nghiệp vụ | |
| `FailureMessage` | `varchar(2000)` | ✔ | thông điệp **đã cắt ngắn + đã che** | 🚨 **cấm** PII |
| `RequestPayloadHash` | `char(64)` | ✖ | SHA-256 của payload **canonical** đã gửi | phát hiện xung đột khoá (kế thừa `XC-002` BR-XC-002-005) |
| `DurationMs` | `int` | ✖ | thời gian lời gọi | `XC-005` §5.2 |
| `CorrelationId` | `varchar(64)` | ✔ | truy vết end-to-end | |
| `OccurredAtUtc` | `timestamptz` | ✖ | thời điểm | |
| `RowVersion` | `xmin` | ✖ | optimistic concurrency | |

> 🔒 **APPEND-ONLY:** `ClientOrderCode`, `RequestPayloadHash`, `AttemptNo`, `OccurredAtUtc` **cấm** `UPDATE`/`DELETE`.
> Sửa sai ⇒ ghi dòng mới. Đây là **dấu vết duy nhất** chứng minh ta đã gọi hãng hay chưa (ADR-02 Pha 2).

**Index & ràng buộc** *(phần hay bị bỏ sót nhất — `docs/templates/feature-design-template.md:214`)*

| Bảng | Loại | Cột | Mục đích |
|---|---|---|---|
| `ShpShipments` | **UNIQUE** `NULLS NOT DISTINCT` | `(TenantId, ProviderCode, ClientOrderCode)` | ⭐ **hàng rào chống tạo 2 vận đơn cho 1 đơn** (BR-003/004) — câu trả lời cho *"bỏ lock đi, DB có chặn không?"* (`XC-003` D2) |
| `ShpShipments` | **UNIQUE** | `(TenantId, OrderId, Generation)` `NULLS NOT DISTINCT` | ⭐ (`D-109`) mỗi đơn một `Shipment` cho mỗi generation — chặn hai Pha 1 đồng thời (`23505` ⇒ A5); đòi `NULLS NOT DISTINCT` vì `TenantId` có thể `NULL` (ghi chú dưới) |
| `ShpShipments` | CHECK | `Generation >= 1` | `D-109` |
| `ShpShipments` | **UNIQUE partial** `NULLS NOT DISTINCT` `WHERE "ProviderTrackingCode" IS NOT NULL` | `(TenantId, ProviderCode, ProviderTrackingCode)` | tra vận đơn từ webhook (`SHP-002` §5.2). 🚨 **Phải partial**: `NULLS NOT DISTINCT` coi `NULL` bằng nhau trên **mọi** cột, mà mọi `Shipment` chưa `Created` (`Draft`/`Submitting`/`SubmitFailed`, và `Cancelled` từ `Draft`) có `ProviderTrackingCode = NULL` (§5.2) ⇒ không partial thì `Shipment` thứ hai chưa có mã cùng tenant + hãng dính `23505`, và Pha 1 hiểu nhầm là A5. `SHP-002` §5.2 phải sửa **cùng định nghĩa** (F11) |
| `ShpShipments` | INDEX | `(TenantId, Status, SubmitAttemptCount)` | ⭐ job thử lại: *"vận đơn `Submitting`/`SubmitFailed` chưa vượt trần"* |
| `ShpShipments` | INDEX | `(TenantId, OrderId)` | tra vận đơn theo đơn (**A5**) |
| `ShpShipments` | INDEX | `(TenantId, Status, SlaDueAtUtc)` | job đối soát `SHP-002` |
| `ShpShipments` | CHECK | `CodAmount >= 0 AND ShippingFee >= 0 AND EstimatedShippingFee >= 0` | bất biến tiền ở tầng DB |
| `ShpShipments` | CHECK | `CodCurrency ~ '^[A-Z]{3}$' AND ShippingFeeCurrency ~ '^[A-Z]{3}$'` | theo khuyến nghị `XC-001` §5.2 |
| `ShpShipments` | CHECK | `ClientOrderCode <> ''` | **không** có `Shipment` nào thiếu `ClientOrderCode` (sinh ở Pha 1, nên đúng với mọi `Status` — không phụ thuộc số của enum, Q16) |
| `ShpShipmentCreateAttempts` | **UNIQUE** `NULLS NOT DISTINCT` | `(TenantId, ShipmentId, AttemptNo)` | chống ghi trùng nhật ký khi 2 luồng cùng thử; dòng huỷ mang `AttemptNo = 0` nên không trùng dòng `Created` (A4) |
| `ShpShipmentCreateAttempts` | CHECK | `(Result = 4 AND AttemptNo = 0) OR (Result <> 4 AND AttemptNo >= 1)` | giữ định nghĩa `AttemptNo` ở trên |
| `ShpShipmentCreateAttempts` | INDEX | `(TenantId, ClientOrderCode)` | tra lịch sử gọi hãng của một mã đơn |
| `ShpShipmentCreateAttempts` | UNIQUE partial `NULLS NOT DISTINCT` | `(TenantId, ShipmentId, Result) WHERE Result = 0` | ⭐ **chỉ MỘT** lần `Created` cho mỗi vận đơn — hàng rào DB thứ hai cho BR-004 |
| `ShpShipmentCreateAttempts` | CHECK | `RequestPayloadHash ~ '^[0-9a-f]{64}$'` | bắt buộc SHA-256 hex thường |
| `ShpShippingRates` | **UNIQUE** `NULLS NOT DISTINCT` | `(TenantId, ShippingServiceId, FromProvinceCode, ToProvinceCode, WeightFromGrams, EffectiveFrom)` | chống trùng khung giá |
| `ShpCarriers` | **UNIQUE** `NULLS NOT DISTINCT` | `(TenantId, ProviderCode)` | một hãng một bản ghi/tenant |
| `ShpShippingServices` | **UNIQUE** `NULLS NOT DISTINCT` | `(TenantId, CarrierId, ProviderServiceId, EffectiveFrom)` | chống trùng khung hiệu lực |

> ⚠️ **`NULLS NOT DISTINCT` (kế thừa `XC-004` F1, `SHP-002` §5.2, quy ước `docs/coding-conventions.md:107`):** hệ thống hiện chạy **1 tenant** ⇒ `TenantId`
> **có thể là `NULL`**, và PostgreSQL mặc định coi `NULL` là **khác nhau** ⇒ unique index thường **mất tác dụng im lặng**.
> Vì vậy **mọi unique index mới ở bảng trên đều có `NULLS NOT DISTINCT`** (bảng chưa tạo ⇒ làm ngay trong migration đầu, không có dữ liệu cũ).
> EF không diễn đạt được ⇒ migration dùng SQL thô trên cả index, theo khuôn `modules/Address/eCommerce.Address.EntityFrameworkCore/AddressDbContextModelCreatingExtensions.cs:15-27,52-55`.
> Ngoại lệ có chủ ý: index `(TenantId, ProviderCode, ProviderTrackingCode)` là **partial** (xem dòng index) vì `ProviderTrackingCode` có `NULL` hợp lệ.
> Các câu lệnh DDL ở bảng trên là **mô tả, chưa chạy** trên PostgreSQL (§11 giới hạn môi trường) ⇒ `TC-32` và `TC-49` chạy **với `TenantId = NULL`** là bằng chứng.

---

## 6. Hợp đồng API

### 6.1 Application Service & Domain Service

| Mục | Giá trị |
|---|---|
| Interface (nội bộ) | `IShipmentAppService` — `eCommerce.ShippingHub.Application.Contracts`. ⚠️ **Trùng tên với `SHP-002` §6.1** ⇒ **một** app service cho cả hai tài liệu, method chia theo tài liệu sở hữu (§15.1 Q7) |
| Implementation | `ShipmentAppService` — `eCommerce.ShippingHub.Application` |
| Interface (adapter) | `IShippingCarrierProvider` — **một implementation cho mỗi hãng**; chọn qua `IShippingCarrierProviderFactory` (khuôn giống `ChannelConnectorFactory` — `modules/ChannelHub/eCommerce.ChannelHub.Application/Connectors/ChannelConnectorFactory.cs:9`–`40`) |
| `[Authorize]` | **cấp CLASS** (bắt buộc — `XC-004` BR-XC-004-001; tiền lệ `src/eCommerce.Application/Channels/ChannelAppService.cs:28`). `SHP-001` **không** có ngoại lệ |

```csharp
namespace eCommerce.ShippingHub.Carriers;

/// 🚨 Vì sao phải là abstraction: GHN (2 cấp, có cờ "địa chỉ mới") KHÁC GHTK (3 cấp, chưa xác minh).
///    Nếu nhét chi tiết GHN vào tầng app service thì SHP-004 sẽ phải SỬA LÕI (ADR-01, ADR-06).
public interface IShippingCarrierProvider
{
    /// Mã hãng: "ghn", "ghtk"… Phải khớp ShpCarriers.ProviderCode.
    string ProviderCode { get; }

    /// Mô hình địa chỉ hãng chấp nhận — để tầng trên biết địa chỉ này có gửi được không (E2).
    CarrierAddressModel SupportedAddressModel { get; }

    /// Lấy phí. KHÔNG ghi DB, KHÔNG đổi trạng thái.
    Task<CarrierFeeResult> GetFeeAsync(CarrierFeeRequest request, CancellationToken ct = default);

    /// ⭐ Tạo vận đơn. BẮT BUỘC idempotent theo request.ClientOrderCode (BR-SHP-001-003/004).
    Task<CarrierCreateShipmentResult> CreateShipmentAsync(
        CarrierCreateShipmentRequest request, CancellationToken ct = default);

    /// Huỷ vận đơn — CHỈ hợp lệ khi hãng CHƯA lấy hàng (A4).
    Task<CarrierCancelResult> CancelShipmentAsync(
        CarrierCancelRequest request, CancellationToken ct = default);
}

/// Địa chỉ TRUNG TÍNH — mang CẢ 2 cấp và 3 cấp (ADR-06). KHÔNG có trường riêng của GHN.
/// Các trường *Code là MÃ CỦA HÃNG, lấy từ cửa mã ResolveCarrierAddressCodeByCodeAsync (D-110, BR-SHP-001-020) — không tự tra bảng ánh xạ.
/// 🔒 D-108: RecipientName/RecipientPhone/AddressLine/Name* lấy từ SNAPSHOT GIAO HÀNG TRÊN ĐƠN (qua hợp đồng Sales) — KHÔNG từ DTO vào của API (BR-SHP-001-021).
public sealed record CarrierAddress(
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string? ProvinceCode,
    string? ProvinceName,
    string? DistrictCode,      // cấp huyện — rỗng với mô hình 2 cấp
    string? DistrictName,
    string? WardCode,
    string? WardName);

/// Số lượng là decimal(18,3) (D-31); WeightGrams là cân nặng MỘT đơn vị (gram, int).
public sealed record CarrierShipmentItem(string Sku, decimal Quantity, int WeightGrams);

public sealed record CarrierCreateShipmentRequest(
    string ClientOrderCode,             // 🚨 khoá chống trùng ỔN ĐỊNH (BR-003)
    CarrierAddress ToAddress,
    CarrierAddress FromAddress,
    int TotalWeightGrams,
    long CodAmountMinorUnits,           // ⭐ số NGUYÊN minor units, KHÔNG decimal (BR-008)
    string CurrencyCode,
    string? ProviderServiceId,
    string? Note,
    IReadOnlyList<CarrierShipmentItem> Items,
    string CorrelationId);

public sealed record CarrierCreateShipmentResult(
    bool Success,
    bool AlreadyExisted,                // hãng báo "mã đơn đã tồn tại" (A1)
    string? ProviderTrackingCode,       // ⭐ bắt buộc khác rỗng khi Success (§3.2 #3)
    decimal? ActualShippingFee,         // phí HÃNG báo — nguồn sự thật (ADR-04)
    string? CurrencyCode,
    DateTime? SlaDueAtUtc,
    string? ProviderServiceId,
    string? FailureCode,                // mã lỗi nghiệp vụ của TA
    CarrierFailureKind FailureKind,     // None / Business / Transient / Configuration
    string? FailureMessage);            // ⚠️ ĐÃ CHE trước khi trả ra

public enum CarrierFailureKind { None = 0, Business = 1, Transient = 2, Configuration = 3 }

/// Huỷ CHỈ gọi được khi đã có mã vận đơn (Shipment ở Created, A4) — Draft huỷ nội bộ, Submitting KHÔNG huỷ (§3.2 #6).
/// Trường nào hãng thật sự cần: chưa xác minh (§15.1 Q2) — ở đây chỉ chốt phần ta luôn có.
public sealed record CarrierCancelRequest(
    string ProviderTrackingCode,        // bắt buộc khác rỗng — không có mã ⇒ không gọi hãng
    string ClientOrderCode,
    string Reason,
    string CorrelationId);

public sealed record CarrierCancelResult(
    bool Success,                       // chỉ true khi HÃNG xác nhận huỷ — chưa xác nhận thì Shipment giữ Created (§7.3)
    string? FailureCode,
    CarrierFailureKind FailureKind,
    string? FailureMessage);            // ⚠️ ĐÃ CHE trước khi trả ra

/// Chọn provider theo mã hãng. Thêm hãng mới = thêm một implementation (SHP-004).
public interface IShippingCarrierProviderFactory
{
    IShippingCarrierProvider Get(string providerCode);
    bool IsSupported(string providerCode);
}
```

**Nguồn đơn — hợp đồng Sales (`D-108`, ⬜ kế hoạch, chưa có trong code):** `SHP-001` đọc đơn qua **một** interface ở `eCommerce.Sales.Application.Contracts` theo khuôn `IAddressDirectory`/`ICustomerDirectory`/`IWarehouseDirectory` (tên gợi ý `IOrderShippingDirectory`), trả DTO **bất biến** gồm: `ChannelCode`, dòng hàng (SKU + `Quantity` `decimal(18,3)`) — nguồn của `CarrierShipmentItem` gửi hãng, **COD đã chốt = `Order.CodAmount`** (`decimal?`, `Order.cs:121` — không nhận từ DTO của API; `null`: Q18) cùng tiền tệ của đơn, và **snapshot giao hàng** (tên, SĐT, `ShippingAddressId?` chỉ để tham chiếu, bản sao các trường `DiaChiSnapshotDto` — `AddressContracts.cs:41-54`). **Cân nặng:** hợp đồng này hiện **không** mang cân nặng SKU, và `Catalog.Application.Contracts` **chưa** có interface đọc cân nặng (chỉ có `IChannelDirectory` — `HienThiKenhDtos.cs:77`; `ProductDtos.cs:35` chỉ là DTO) (⬜ kế hoạch — hoặc hợp đồng Sales trả sẵn cân nặng, hoặc interface Catalog; chọn cùng Q5). Module chủ = **Sales** (`SAL-002` thiết kế cột/VO; ShippingHub **cấm** tham chiếu `Sales.Domain`/`Sales.EntityFrameworkCore`). Đổi địa chỉ trước khi tạo vận đơn = lệnh của **Sales** (thay snapshot đơn); `SHP-001` **không** có đường ghi đè.

**Nguồn mã hãng — hợp đồng Address (`D-110`, ⬜ kế hoạch WP-15, chưa có trong code):** `SHP-001` gọi **một** interface nội bộ ở `eCommerce.Address.Application.Contracts` (khuôn `IAddressDirectory` — `AddressContracts.cs:157`; tên gợi ý `ICarrierAddressCodeDirectory`) với method `ResolveCarrierAddressCodeByCodeAsync(CarrierCode, SchemaVersion, Level, Code)`, trả mã/cấp/tên **của hãng** + khoá đơn vị ta. Mỗi lời gọi nhận **một** đơn vị; **cấp** nào được tra (xã / tỉnh+huyện+xã) thuộc Q1. `Address` hiện thực: tra đơn vị bằng khoá tự nhiên qua `AdministrativeUnitResolver.TraTheoMa` (`AdministrativeUnitResolver.cs:20-43`; không thấy ⇒ `Address:0001`, phiên bản sai ⇒ `Address:0005` — `AddressErrorCodes.cs:10,25`) rồi gọi **đúng** hàm thuần `CarrierAddressMappingResolver.TraNguoc` của `D-89` (một logic, hai cửa vào). Ràng buộc phía `SHP-001`: dùng **đúng** `SchemaVersion` của snapshot (**cấm** tra chéo phiên bản; snapshot V1 không có ánh xạ ⇒ NÉM như `D-89`, đường gỡ thuộc Q12); kiểu `Level` **không** được kéo `eCommerce.Address.Domain` vào ShippingHub (cách chọn: `addr-002` §6.1, §15.2 **F10**). Method Guid `ResolveCarrierAddressCodeAsync` (`D-89`) vẫn có cho bên đã cầm Id (vd màn quản trị) — đường tạo vận đơn/ước tính **theo đơn** **không** dùng nó và **không** đọc `IAddressDirectory.TimTheoIdAsync` theo `ShippingAddressId` (ngoại lệ ước tính giỏ của `D-108` giữ). Snapshot trên `Order` và `DiaChiSnapshotDto` **không** thêm trường Guid đơn vị.

**DTO vào theo `D-108`:** `CreateShipmentDto` (chỉ còn `note` — mẫu §6.2) và `EstimateOrderShippingFeeDto` (**theo đơn**, mang `orderId`) **không** có `recipientName`/`recipientPhone`/`addressLine`/`*Code` địa chỉ/`AddressId`/`OurAdministrativeUnitId`; riêng `CreateShipmentDto` **không** có `codAmount`/`currency`/`items` (COD, tiền tệ, dòng hàng đọc từ đơn qua hợp đồng Sales — BR-021: nhân viên kho gõ COD khác đơn thì hãng thu hộ sai tiền mà không guard nào chặn). **Ngoại lệ duy nhất:** ước tính phí **cho giỏ** (chưa có đơn) dùng DTO **riêng** `EstimateCartShippingFeeDto` (method `EstimateCartFeeAsync`) nhận `AddressId` và đọc `IAddressDirectory.TimTheoIdAsync` (`AddressContracts.cs:163`) — chỉ ước tính, không chốt (ADR-04). Hai DTO tách để `TC-36` quét DTO theo đơn mà không vướng ngoại lệ giỏ.

**Method của `IShipmentAppService` — phần do `SHP-001` sở hữu**

| Method | Input (DTO) | Output (DTO) | Permission | Ghi chú |
|---|---|---|---|---|
| `EstimateOrderFeeAsync` | `EstimateOrderShippingFeeDto` | `ShippingFeeEstimateDto` | `eCommerce.Shipping.Shipments.Estimate` | ⚠️ **chỉ ước tính** (`IsEstimate = true`); **cấm** dùng số này để chốt sổ (ADR-04). Địa chỉ từ snapshot đơn (`D-108`) |
| `EstimateCartFeeAsync` | `EstimateCartShippingFeeDto` | `ShippingFeeEstimateDto` | `eCommerce.Shipping.Shipments.Estimate` | như trên; chưa có đơn ⇒ nhận `AddressId` (ngoại lệ `D-108` (3)) |
| `CreateShipmentAsync` | `Guid orderId` + `CreateShipmentDto` | `ShipmentDto` | `eCommerce.Shipping.Shipments.Create` | 3 pha (ADR-02) + `[UnitOfWork(isTransactional: false)]` (BR-002); idempotent (A5); địa chỉ + COD + dòng hàng từ đơn qua hợp đồng Sales, thiếu snapshot ⇒ `DonChuaCoDiaChiGiao` trước Pha 1 (`D-108`) |
| `CancelShipmentAsync` | `Guid shipmentId` + `string reason` | `ShipmentDto` | `eCommerce.Shipping.Shipments.Cancel` | **bắt buộc** `reason`; guard #6/#8 §3.2 (từ `Draft`/`Created`, **không** từ `Submitting`); `[UnitOfWork(isTransactional: false)]` (BR-002) |
| `GetAsync` / `GetListAsync` | `Guid id` / `GetShipmentsInput` | `ShipmentDto` / `PagedResultDto<ShipmentDto>` | `.View` | **chia sẻ** với `SHP-002` §6.1 (cùng method, cùng DTO) |
| `GetCreateAttemptsAsync` | `Guid shipmentId` | `PagedResultDto<ShipmentCreateAttemptDto>` | `.View` | **chỉ đọc** (append-only ⇒ **không** có API sửa) |
| `GetCarriersAsync` / `UpsertCarrierAsync` | … | `CarrierDto` | `.Carriers.View` / `.Carriers.Manage` | thay đổi `IsEnabled`/`AddressModel` ⇒ **audit** |
| `GetRatesAsync` / `ImportRatesAsync` | … | `PagedResultDto<ShippingRateDto>` | `.Rates.View` / `.Rates.Manage` | import **idempotent** theo `(khoá ổn định + content hash)` — kế thừa `XC-002` §7.3 |

> 🚨 **KHÔNG có** `ChangeCodAmountAsync` — **cấm** (BR-SHP-001-014, ADR-07). Đổi COD ở GHN cần **OTP**
> (`docs/implementation-plan.md:199`, `:426`). Yêu cầu đổi COD ⇒ `eCommerce:Shipping:0018` và chuyển cho
> quy trình thủ công của `SHP-003`.

### 6.2 REST endpoint

| Method | Route | Auth | Permission | Mô tả |
|---|---|---|---|---|
| `POST` | `/api/app/shipping/shipments/estimate-order-fee` | Bearer (OpenIddict) | `.Estimate` | ước tính phí theo đơn |
| `POST` | `/api/app/shipping/shipments/estimate-cart-fee` | Bearer (OpenIddict) | `.Estimate` | ước tính phí cho giỏ (chưa có đơn) |
| `POST` | `/api/app/shipping/orders/{orderId}/shipments` | Bearer | `.Create` | ⭐ tạo vận đơn (idempotent) |
| `POST` | `/api/app/shipping/shipments/{id}/cancel` | Bearer | `.Cancel` | huỷ trước khi hãng lấy hàng |
| `GET` | `/api/app/shipping/shipments` · `/{id}` | Bearer | `.View` | danh sách / chi tiết (**dùng chung** với `SHP-002`) |
| `GET` | `/api/app/shipping/shipments/{id}/create-attempts` | Bearer | `.View` | nhật ký gọi hãng (append-only) |
| `GET` `PUT` | `/api/app/shipping/carriers…` | Bearer | `.Carriers.View` / `.Carriers.Manage` | cấu hình hãng (không có secret ở đây — BR-015) |
| `GET` `POST` | `/api/app/shipping/rates…` | Bearer | `.Rates.View` / `.Rates.Manage` | bảng giá ước tính |

> 📌 **KHÔNG có endpoint vô danh nào trong `SHP-001`** (BR-016). Endpoint webhook **duy nhất** của module
> (`POST /api/shipping/webhooks/{providerCode}/{tenantCode}`) thuộc `SHP-002` §6.2 và là **ngoại lệ
> `[AllowAnonymous]`** — xem **F4** ở §15.2 về xung đột allowlist với `XC-004` Q1.

**Request mẫu — tạo vận đơn**

```json
{
  "note": "Giao giờ hành chính"
}
```

**Response mẫu**

```json
{
  "id": "…",
  "orderId": "…",
  "providerCode": "ghn",
  "generation": 1,
  "clientOrderCode": "0f8fad5bd9cb469fa2c5b3d2f1c0a7e1-1",
  "providerTrackingCode": "GHN123456",
  "status": "Created",
  "shippingFee": 38500,
  "shippingFeeCurrency": "VND",
  "estimatedShippingFee": 37000,
  "codAmount": 250000,
  "submitAttemptCount": 1,
  "correlationId": "aa003348cac54ee99f7d7c7114dca531"
}
```

> 📌 **`D-108`:** request **không** mang địa chỉ/người nhận — `SHP-001` lấy từ **snapshot giao hàng trên đơn** qua hợp đồng Sales; địa chỉ chỉ xuất hiện ở
> `CarrierCreateShipmentRequest.ToAddress` (gửi hãng, mã hãng tra qua cửa mã `D-110` từ mã trong snapshot) và ở bản sao snapshot lưu trên `ShpShipments` (§5.2). Đơn thiếu snapshot ⇒ `DonChuaCoDiaChiGiao` (§6.3).
> COD, tiền tệ và dòng hàng **cũng không** nằm trong request: lấy từ đơn qua hợp đồng Sales (`D-108` (2), BR-021); response vẫn trả `codAmount` đã gửi hãng.
>
> ⚠️ **Mã vận đơn `GHN123456` và endpoint thật là MINH HOẠ CẤU TRÚC, KHÔNG phải đặc tả đã kiểm chứng.**
> Tài liệu API chính thức của GHN **chưa được đọc** trong lần viết tài liệu này (§15.1 Q2);
> việc gửi email hỏi NCC vẫn **đang treo** (`docs/implementation-plan.md:321`). Không được code theo ví dụ này.

### 6.3 Mã lỗi nghiệp vụ (`ShippingHubErrorCodes` — **module tự sở hữu**, ADR-10)

> 🔴 **Dải số phải được CHIA giữa `SHP-001` và `SHP-002`.** `SHP-002` §6.2 đã dùng `0001`–`0011`.
> `SHP-001` vì vậy **bắt đầu từ `0013`** (bỏ trống `0012` để dành). ⚠️ Đây là **xung đột tiềm ẩn cần chốt**
> (§15.1 Q7) vì hai tài liệu cùng khai một class `ShippingHubErrorCodes`.
>
> ⚠️ **Hai mã mới `DonChuaCoDiaChiGiao` (`D-108`) và `VanDonCuChuaRoKetQua` (`D-109`) chưa có số:** "cuối dải `SHP-001`" kế tiếp là `0019`/`0020`, nhưng `SHP-003` đã dùng `0016`–`0024`
> (`shp-003-cod-thu-ho-doi-cod.md:566-574`) và `SHP-004` dùng `0012`–`0021` (`shp-004-adapter-ghtk.md:538-547`) với **nghĩa khác** ⇒ số thật chỉ cấp được sau khi Q7 chốt
> dải chung cho 4 tài liệu. Test (TC-38, TC-43) bám **tên** mã, không bám số.

| Mã | HTTP | Khi nào | Thông điệp |
|---|---|---|---|
| `eCommerce:Shipping:0013` | `400` | Hãng **chưa** được cấu hình (thiếu `BaseUrl`/token/`ShopId`/`FromAddress`) — **fail-closed** | `Hãng vận chuyển {provider} chưa được cấu hình.` |
| `eCommerce:Shipping:0014` | `400` | Địa chỉ **không hợp lệ** với mô hình địa chỉ của hãng (thiếu cấp bắt buộc) **hoặc** cửa mã của `Address` (`D-110`) ném (`Address:0001`/`0005`/`0501`/`0502`/`0504`, > 1 ánh xạ `DaXacMinh`) — kèm `InnerErrorCode` | `Địa chỉ giao hàng không hợp lệ với hãng {provider}.` |
| `eCommerce:Shipping:0015` | `400` | Hãng **từ chối** tạo vận đơn (4xx nghiệp vụ) hoặc "thành công" mà **không** có mã vận đơn | `Hãng vận chuyển từ chối tạo vận đơn: {reason}.` |
| `eCommerce:Shipping:0016` | `409` | Vượt trần `MaxSubmitAttempts` — cần người xử lý | `Đã thử tạo vận đơn quá {n} lần, cần xử lý thủ công.` |
| `eCommerce:Shipping:0017` | `409` | Chuyển trạng thái **không hợp lệ** · `UPDATE … WHERE Status` **0 dòng** · huỷ sau khi hãng đã lấy hàng · huỷ khi `Submitting` (§3.2 #6) | `Không thể chuyển vận đơn từ {from} sang {to}.` |
| `eCommerce:Shipping:0018` | `400` | Yêu cầu **đổi COD** (bị cấm tự động hoá — ADR-07) | `Đổi tiền thu hộ cần xác thực OTP với hãng; vui lòng dùng quy trình thủ công.` |
| 🆕 `DonChuaCoDiaChiGiao` (`ShippingHubErrorCodes`; số **chưa cấp** — Q7) | `400` | Đơn **thiếu snapshot giao hàng** (hoặc thiếu tên/SĐT/cấp xã) — NÉM **trước Pha 1**, 0 `Shipment`, 0 lời gọi hãng (`D-108`, E10) | `Đơn hàng chưa có địa chỉ giao hợp lệ để tạo vận đơn.` |
| 🆕 `VanDonCuChuaRoKetQua` (`ShippingHubErrorCodes`; số **chưa cấp** — Q7; cuối dải `SHP-001`) | `409` | Tạo lại `Shipment` (Generation + 1) khi `Shipment` mới nhất là `SubmitFailed` vượt trần mà có attempt `TransientFailure` **chưa tra được** kết quả ở hãng — 0 `Shipment` mới, 0 lời gọi hãng (`D-109`, §3.2 #1) | `Vận đơn trước chưa rõ kết quả tại hãng, cần xác minh trước khi tạo lại.` |
| `eCommerce:Lock:0001` | `409` | Không giành được khoá trong timeout (dùng mã của `XC-003`) | `Tài nguyên đang được xử lý bởi tiến trình khác, vui lòng thử lại.` |

### 6.4 Cấu hình (đề xuất) — **không hard-code endpoint hãng** (BR-011 / ADR-11)

```jsonc
{
  "ShippingHub": {
    // ⚠️ KHÔNG có secret thật trong file này — token đọc từ cấu hình/secret ngoài (BR-015).
    "SubmitRetryPeriodSeconds": 300,        // job thử lại: 5 phút
    "MaxSubmitAttempts": 5,                 // trần thử tạo vận đơn (E5)
    "StuckSubmittingMinutes": 15,           // Submitting quá N phút ⇒ job tra lại (A2)
    "RateSyncPeriodSeconds": 86400,         // đồng bộ bảng giá: 1 ngày
    "CallTimeoutSeconds": 10,               // timeout mỗi lời gọi hãng (NFR-009)
    "MaxRetries": 3,                        // backoff 1s → 5s → 25s (NFR-009)
    "LockWaitSeconds": 10,                  // CHỜ giành khoá (tham số timeout của ABP) — hết ⇒ E6, 409. Đề xuất, chốt cùng Q6
    "ShipmentCreateLockTtlSeconds": 120,    // TTL/lease khoá shipping:shipment-create — PHẢI > 3×10 s + backoff 1+5+25 = 61 s (ADR-08, §8.2). Hai số này KHÁC nhau, không gộp
    "Providers": {
      "ghn": {
        "Enabled": true,
        "BaseUrl": "",                      // 🔴 CHƯA có nguồn xác minh — §15.1 Q2; CẤM commit giá trị thật
        "ApiToken": "",                     // 🔒 đọc từ secret ngoài; CẤM commit (XC-004 BR-012)
        "ShopId": "",                       // GHN yêu cầu ShopId — CHƯA kiểm chứng
        "AddressModel": "TwoLevel",         // GHN có cờ địa chỉ mới (business-modules-proposal.md:513)
        "FromAddress": {                    // địa chỉ kho gửi — DỮ LIỆU ĐỒNG BỘ theo hãng, KHÔNG phải master data
          "Name": "", "Phone": "", "AddressLine": "",
          "ProvinceCode": "", "DistrictCode": "", "WardCode": ""
        },
        "DefaultServiceId": "",             // mã dịch vụ mặc định của hãng — CHƯA kiểm chứng
        "EnableLiveCalls": false            // ⭐ false ⇒ provider mô phỏng, luồng vẫn chạy đủ (A3)
      }
    }
  }
}
```

> 📌 `EnableLiveCalls` **sao chép đúng khuôn đã có**: `ShopeeOptions.EnableLiveCalls`
> (`modules/ChannelHub/eCommerce.ChannelHub.Application/ChannelHubOptions.cs:47`) và cách `ShopeeConnector`
> dùng nó để chạy toàn bộ luồng mà **không** cần credential (`.../Connectors/ShopeeConnector.cs:80`–`87`).
> Đăng ký options ở host theo khuôn `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:263`–`264`.

### 6.5 Cấu hình bắt buộc khác (đã có / còn nợ — **kiểm bằng `read`**)

| Hạng mục | Nội dung | Nguồn |
|---|---|---|
| Khoá phân tán — tiền tố | `AbpDistributedLockOptions.KeyPrefix = "ecommerce:"` — **đã** cấu hình | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:237` |
| Khoá phân tán — provider | `RedisDistributedSynchronizationProvider` + `AbortOnConnectFail = false` — **đã** cấu hình | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:245`–`254` |
| Unit of Work | `TransactionBehavior.Auto` — **đã** cấu hình (⇒ phải tắt UoW ngầm ở app service tạo/huỷ vận đơn — BR-002) | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:91`–`94` |
| CorrelationId | `app.UseCorrelationId()` — **đã** cấu hình | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:466` |
| Health check | `/health-status` — **đã** cấu hình | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:508` |
| **Outbox/Inbox** | ✅ **Đã** bật: `options.Outboxes`/`Inboxes` cấu hình với `eCommerceDbContext` | `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:86-87` |
| **`Money`/`Currency` (XC-001)** | 🔶 `Money`, `MoneyAllocator` **đã** có (`eCommerce.Kernel`) nhưng không có `ToMinorUnits()`/`Money.Of`, không tự làm tròn (Q17) | `shared/eCommerce.Kernel/Money/Money.cs:23`, `MoneyAllocator.cs:21`; `eCommerce.slnx:87` |
| **`IIdempotencyGuard` (XC-002)** | ✅ **Đã** có (module Messaging) | `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:27` |
| **`ResolveCarrierAddressCodeAsync` (`D-89`) + cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`)** | ⬜ **Chưa có** (grep `*.cs` = 0) — kế hoạch WP-15, **phải** xong trước khi `SHP-001` gọi | `docs/nhanh/decisions.md` `D-89`, `D-110` |
| **Module ShippingHub** | ⚠️ **CHƯA** tồn tại (`eCommerce.slnx:2`–`17`) | §15.2 **F1** |

---

## 7. Sự kiện, tích hợp ngoài & idempotency

### 7.1 Sự kiện phát ra (ETO)

| ETO | Khi nào | Trường dữ liệu | Ai tiêu thụ | Idempotent? |
|---|---|---|---|---|
| `ShipmentCreatedEto` | Pha 3 thành công (`Status = Created`) | `TenantId`, `ShipmentId`, `OrderId`, `OrderNumber`, `ChannelCode`, `ProviderCode`, `ClientOrderCode`, `ProviderTrackingCode`, `ShippingFee`, `ShippingFeeCurrency`, `CodAmount`, `CodCurrency`, `TotalWeightGrams`, `SlaDueAtUtc`, `AggregateVersion`, `CorrelationId` | **Sales** (`SAL-003`: điền mã vận đơn để mở khoá `Packing → AwaitingPickup`) · **Notifications** (`NOT-002`: báo khách mã vận đơn) · **REP-001** (read-model) | ✅ consumer dùng `IIdempotencyGuard` với `Key = Shipment:{shipmentId}:Created` (`XC-002` BR-XC-002-004) |
| `ShipmentCreationFailedEto` | Pha 3 thất bại (E3/E4/E5) | như trên + `FailureCode`, `FailureKind`, `AttemptCount` | **CSKH/Notifications** (email kỹ thuật — `XC-005` Q3) · **REP-001** | ✅ `Key = Shipment:{shipmentId}:CreationFailed:{attemptNo}` |
| `ShipmentCancelledEto` | Huỷ thành công (A4) | `TenantId`, `ShipmentId`, `OrderId`, `ProviderCode`, `Reason`, `CancelledAtUtc`, `AggregateVersion`, `CorrelationId` | **Sales** · **Notifications** | ✅ `Key = Shipment:{shipmentId}:Cancelled` |

> 📌 **Cơ chế:** outbox của ABP + relay (`XC-002` §6.3). ✅ Outbox/Inbox **đã bật**
> (`src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:86-87`) ⇒ ETO của `SHP-001` **phải** đi qua outbox cùng
> transaction; phát trực tiếp (dual-write) vi phạm `XC-002` BR-XC-002-002.
>
> 🚨 **Payload ETO ≤ 100 KB** (`XC-002` NFR-XC-002-012): ETO **chỉ** mang **tham chiếu + số tiền**, **cấm** nhét
> danh sách dòng hàng đầy đủ hay payload hãng.
>
> ⚠️ **Ghi rõ cho `SAL-002`:** `SHP-001` **KHÔNG** tự ghi `Order.ShippingTotal`
> (`modules/Sales/eCommerce.Sales.Domain/Order.cs:162`, chỉ ghi được qua `SetTotals` — `:252`). Việc đẩy trục thực hiện của đơn
> (`Packing → AwaitingPickup`, 18 giá trị — `D-08`) cũng do `SAL-003` quyết khi tiêu thụ ETO, `SHP-001` không gọi `ChuyenThucHien`. Việc cập nhật phí
> thật trên đơn là quyết định của `SAL-002` khi tiêu thụ `ShipmentCreatedEto` — vì **module sở hữu entity của
> mình** và **module gọi app BỊ CẤM**.
>
> ⚠️ **Một nguồn ghi mã vận đơn/phí thật trên đơn:** `Order` đã có `CarrierCode`, `TrackingNumber`, `ShippingFeeActual` (`Order.cs:110-127`) và chỉ ghi được qua `DatVanChuyen` (`Order.cs:292`, ghi cả sáu trường một lần). Consumer của `ShipmentCreatedEto` ở Sales (⬜ chưa viết, thuộc `SAL-003`/`SAL-002`) là nơi **duy nhất** gọi `DatVanChuyen` từ ETO: `ProviderCode → CarrierCode`, `ProviderTrackingCode → TrackingNumber`, `ShippingFee → ShippingFeeActual`, và truyền lại giá trị hiện có của `CodAmount`/`ShippingFeeCustomer`/`WeightGram`. `SHP-001` không ghi, nên không có hai nguồn mã vận đơn.

### 7.2 Sự kiện / webhook **nhận vào**

| Nguồn | Loại | Xác thực | Chống trùng bằng | Nếu xử lý lỗi? | Thứ tự đến |
|---|---|---|---|---|---|
| **Webhook trạng thái hãng** | `webhook` | — | — | — | — |

**Không áp dụng cho `SHP-001` — lý do:** `SHP-001` **chỉ gọi RA**, **không nhận** dữ liệu từ hãng. Webhook trạng
thái vận đơn là **biên vào** của `SHP-002` (`docs/features/shipping/shp-002-webhook-trang-thai-van-don.md` §7.2), nơi đã
định nghĩa 4 lớp xác thực + `UNIQUE (TenantId, ProviderCode, ProviderEventId)` + `IIdempotencyGuard`.
Tài liệu này **không** định nghĩa lại. ✅ **Nhưng** `SHP-001` **phải** tạo ra **`ProviderTrackingCode`** —
khoá đối chiếu mà webhook của `SHP-002` cần để khớp vận đơn (nếu không có nó, `SHP-002` chỉ có thể trả
`eCommerce:Shipping:0004`).

### 7.3 Gọi API **ra ngoài**

| NCC | Endpoint (base URL) | Auth | Timeout | Retry | Nếu thất bại |
|---|---|---|---|---|---|
| **GHN — tính phí** | `ShippingHub:Providers:ghn:BaseUrl` + path **cấu hình** (`ShippingHub:Providers:ghn:FeePath`) | token/key theo tài liệu GHN — 🔴 **chưa kiểm chứng** | **10 s** (cấu hình) | **3** lần + backoff 1s→5s→25s | **Không** ném lỗi cứng cho checkout; trả `IsEstimate` + `FailureReason`; log `Warning` + metric `shipping_carrier_call_failed_total` |
| **GHN — tạo vận đơn** | `ShippingHub:Providers:ghn:BaseUrl` + `CreatePath` | token/key — 🔴 **chưa kiểm chứng** | **10 s** | **3** lần + backoff, **chỉ** với `FailureKind = Transient` | `Status = SubmitFailed`; ghi attempt; **KHÔNG** rollback dữ liệu đã ghi; job thử lại nhịp sau; vượt `MaxSubmitAttempts` ⇒ E5 |
| **GHN — huỷ vận đơn** | `ShippingHub:Providers:ghn:BaseUrl` + `CancelPath` | như trên | 10 s | 3 lần | Giữ `Status` hiện tại + `FailureCode`; **không** đánh dấu đã huỷ khi hãng chưa xác nhận |
| **GHN — tra theo `ClientOrderCode`** | chưa có | — | — | — | 🔴 **CHƯA kiểm chứng hãng có API này** (§15.1 Q3) ⇒ nếu **không** có, **A2** phải làm thủ công |
| Hãng khác (GHTK…) | chưa có | — | — | — | thuộc `SHP-004` |

> 🚨 **BR-011 / `docs/design-doc-guidelines.md:154` (quy tắc 8):** mọi path/base URL **phải** nằm trong cấu hình.
> 🔴 **Hiện chưa có nguồn nào để xác minh** base URL và cấu trúc payload của GHN: `docs/implementation-plan.md:321`
> còn treo việc *"Gửi email **MISA / GHTK / FPT IS / M-Invoice / VNPost / J&T / Best / Ninja Van** hỏi API"*, và
> `docs/implementation-plan.md:322` còn treo *"Đăng ký tài khoản **sandbox**: … + **GHN** + **GHTK**"*.
> 💡 **Gợi ý có bằng chứng:** `docs/business-modules-proposal.md:559` ghi GHN **publish `llms.txt` + `llms-full.txt`**
> và `docs/implementation-plan.md:338` chốt *"hãng vận chuyển nào làm trước? **GHN** — REST, có sandbox, có
> `llms.txt` cho coding agent"* ⇒ **nguồn để xác minh là tài liệu GHN công bố cho coding agent**, **không** phải
> tài liệu này.

**Idempotency key** *(khoá phải ỔN ĐỊNH qua các lần retry — `XC-002` BR-XC-002-007)*

| Đối tượng | Cách sinh khoá | ⚠️ Lưu ý |
|---|---|---|
| **Mã đơn gửi hãng** (`ClientOrderCode`) | Hàm thuần `TaoClientOrderCode(orderId, generation)` = `"{orderId:N}-{generation}"` (`D-109`, BR-022); sinh **một lần** ở Pha 1, lưu `ShpShipments.ClientOrderCode`; **không** chứa `tenantId`/`attemptNo` | 🚨 **CẤM** `Guid.NewGuid()` mỗi lần gọi và **cấm** ghép `attemptNo`/`SubmitAttemptCount` vào mã. ASCII, ≤ 43 ký tự; GHN tối đa 50 ⇒ adapter vượt giới hạn hãng thì **NÉM**, **cấm** cắt/băm. Retry cùng `Shipment` (job §3.2 #5, A2, E4) **không** đổi mã |
| **Khoá `IIdempotencyGuard` cho một lần gửi hãng** | `Create:{tenantId}:{orderId}:{generation}:{attemptNo}` (`D-109`) — thay `Create:{tenantId}:{orderId}:{attemptNo}` của `SHP-002` §7.3 (việc sửa `SHP-002`: §15.2 **F8** ở đây); `attemptNo` đọc từ `Shipment.SubmitAttemptCount` **đã** commit ở Pha 2 — **không** sinh mới trong bộ nhớ | Thiếu `generation` thì attempt 1 của generation 2 trùng khoá attempt 1 của generation 1 ⇒ guard trả kết quả cũ **im lặng** — `TC-44` đỏ. Khoá này **không** phải giá trị gửi hãng (khác phiên bản trước của tài liệu) |
| **Chống tạo 2 vận đơn cho 1 đơn** | `(TenantId, ProviderCode, ClientOrderCode)` — **unique index DB**, `NULLS NOT DISTINCT` | Có hiệu lực cả khi `TenantId = NULL` (§5.2 ghi chú); chứng minh bằng `TC-32` với `TenantId = NULL` |
| **Job thử lại** | `RetrySubmit:{tenantId}:{kỳ}` với `kỳ = yyyyMMddHHmm` cắt theo `SubmitRetryPeriodSeconds` | cùng kỳ chạy lại ⇒ **không** nhân đôi attempt (kế thừa `XC-002` §7.3) |
| **Đồng bộ bảng giá** | `RateSync:{providerCode}:{tenantId}:{kỳ}` | cùng kỳ ⇒ **không** tạo bản ghi giá trùng |
| **Import bảng giá từ file** | `Import:{tenantId}:{sha256(fileContent)}` | import lại **cùng file** ⇒ trả kết quả cũ + cờ `AlreadyImported` (kế thừa `XC-002` §7.3) |

---

## 8. Bất biến & xử lý đồng thời

### 8.1 Bất biến

| # | Bất biến | Cơ chế bảo vệ |
|---|---|---|
| 1 | **Không** có 2 vận đơn cho 1 `(tenant, provider, ClientOrderCode)` | **UNIQUE** `(TenantId, ProviderCode, ClientOrderCode)` (DB, `NULLS NOT DISTINCT`) — **câu trả lời cho "bỏ lock đi, DB có chặn không?"**, kể cả khi `TenantId = NULL` (`TC-32`) |
| 2 | **Đúng một** lần gọi hãng "thành công" cho mỗi vận đơn | **UNIQUE có filter** `(TenantId, ShipmentId, Result) WHERE Result = 0` trên `ShpShipmentCreateAttempts` |
| 3 | `ClientOrderCode` và `Generation` là **bất biến** sau khi đã lưu | Setter private + **cấm** `UPDATE` (BR-SHP-001-003, BR-022); DB CHECK khác rỗng / `Generation >= 1` |
| 3b | Mỗi `(tenant, đơn, generation)` có **đúng một** `Shipment` | **UNIQUE** `(TenantId, OrderId, Generation)` `NULLS NOT DISTINCT` (`D-109`) — hai Pha 1 đồng thời ⇒ `23505` ⇒ A5 |
| 4 | **Không** có **transaction** DB mở trong lúc chờ HTTP tới hãng | Ba pha rời (ADR-02) + `[UnitOfWork(isTransactional: false)]` + `Begin(requiresNew: true, …)` (BR-002) + test `TC-08` (quét attribute) và `TC-09` (HTTP thật) |
| 5 | `Status = Created` ⇒ `ProviderTrackingCode` **khác rỗng** | Guard §3.2 #3 + CHECK `(Status <> «Created») OR ProviderTrackingCode IS NOT NULL` (`«Created»` = số thật của enum — chờ Q16) |
| 6 | `CodAmount` **bất biến** sau khi tạo vận đơn | **Cấm** method/endpoint đổi COD (BR-SHP-001-014, ADR-07) + review + test `TC-19` |
| 7 | Trạng thái **hấp thụ** (`Cancelled`) **không** đổi được | Guard §3.2 #7 ⇒ ném `0017` |
| 8 | **Không** có chuyển trạng thái ngầm | `Shipment.Status` setter **private**; mọi đổi **chỉ** qua `ApplyCreate*` có guard |
| 9 | Chuyển trạng thái đồng thời: **đúng 1** thắng | `UPDATE … WHERE Status = @expected` + `RowVersion`; **0 dòng** ⇒ **NÉM LỖI** (E7, `XC-002` BR-XC-002-008) |
| 10 | **Zero** secret của hãng trong repo | Cấu hình/secret ngoài (BR-015) + quét mã nguồn `TC-22` |
| 11 | Cách ly tenant | `IMultiTenant` + query filter + `ICurrentTenant` + mọi unique index **có** `TenantId` |
| 12 | Hãng **chưa** cấu hình ⇒ **fail-closed** | `eCommerce:Shipping:0013` — **cấm** mặc định chấp nhận (E1) |
| 13 | Metric **không** dùng để ra quyết định nghiệp vụ | Review + BR-018 |

### 8.2 Xử lý đồng thời

| # | Tình huống | Cơ chế |
|---|---|---|
| 1 | **2 nhân viên cùng bấm "Tạo vận đơn"** cho cùng đơn | Pha 1 chạy **trước** khoá (§4.1): luồng thứ hai gặp `Shipment` hiệu lực hoặc `23505` ở Pha 1 ⇒ trả vận đơn hiện có (**A5**, không vào khoá). Khoá `shipping:shipment-create:{tenantId}:{orderId}` chỉ tuần tự hoá Pha 2 → Pha 3 của luồng đã tạo `Shipment`; luồng chờ khoá sau khi giành được **double-check** `Status` (đã `Created` ⇒ trả hiện có). Chốt cuối: **UNIQUE** ở DB |
| 2 | **2 luồng cùng chuyển `Draft → Submitting`** | `UPDATE … WHERE Status = Draft` ⇒ 1 thắng; luồng kia **0 dòng** ⇒ **NÉM** `0017` (E7) |
| 3 | **Job thử lại chạy trên nhiều instance** | `IAbpDistributedLock` khoá `shipping:submit-retry:{tenantId}` + **`SkipIfBusy`** + log + metric `lock_skipped_total{module="shipping"}` (`XC-003` A1, D10) |
| 4 | **Job đồng bộ bảng giá chạy chồng** | Khoá `shipping:rate-sync:{providerCode}:{tenantId}` + `SkipIfBusy` |
| 5 | **Nhân viên huỷ trong khi Pha 3 đang ghi `Created`** | `UPDATE … WHERE Status = @expected`: 1 thắng, kẻ thua **0 dòng** ⇒ **NÉM LỖI**, đọc lại trạng thái và xử lý theo guard — **cấm** coi là thành công |
| 6 | **CSKH huỷ vận đơn trong khi webhook `PickedUp` của `SHP-002` đến** | Guard #6/#8 §3.2 dùng `LastProviderEventAtUtc` (do `SHP-002` ghi) ⇒ nếu hãng **đã** lấy hàng thì huỷ bị **NÉM** `0017`; **không** dùng khoá (webhook **không** khoá — `SHP-002` D7) |
| 7 | **Redis chết** | `AbortOnConnectFail = false` (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:251`) ⇒ app **vẫn chạy**, mất tính tuần tự. Hàng rào còn lại là **UNIQUE index ở DB** (`NULLS NOT DISTINCT`, có hiệu lực cả khi `TenantId = NULL` — §5.2; `TC-32`) |
| 8 | **2 vận đơn cùng `ClientOrderCode` ở 2 tenant** | UNIQUE **có** `TenantId` ⇒ hợp lệ; `TenantId` từ `ICurrentTenant` quyết định ghi vào tenant nào |
| 9 | **Provider `GetFeeAsync` bị gọi đồng thời rất nhiều từ checkout** | **Không** khoá (đọc thuần, không side effect — `XC-003` D9: *"không dùng distributed lock cho việc thuần trong 1 tiến trình"*). Giảm tải bằng **cache ngắn** theo `(route, weight bucket)` — ⚠️ **chưa chốt TTL** (§15.1 Q5) |

**Khoá phân tán dùng trong feature** *(liệt kê chính xác tên khoá — quy tắc 6 của `docs/design-doc-guidelines.md:116`)*

| Tên khoá | Hàm sinh khoá | Phạm vi | Vì sao cần | TTL đề xuất |
|---|---|---|---|---|
| `shipping:shipment-create:{tenantId}:{orderId}` | `ShippingHubLockKeys.ShipmentCreate(tenantId, orderId)` | theo đơn hàng | chống 2 nhân viên/2 instance cùng tạo vận đơn cho 1 đơn | **120 s** (phải **>** 3 lần gọi hãng × 10 s + backoff) |
| `shipping:submit-retry:{tenantId}` | `ShippingHubLockKeys.SubmitRetry(tenantId)` | theo tenant | chống job thử lại chạy chồng trên nhiều instance | **300 s** |
| `shipping:rate-sync:{providerCode}:{tenantId}` | `ShippingHubLockKeys.RateSync(providerCode, tenantId)` | theo hãng + tenant | chống 2 instance cùng ghi bảng giá | **300 s** |
| `shipping:provider-config:{providerCode}` | `ShippingHubLockKeys.ProviderConfig(providerCode)` | theo hãng (hạ tầng) | chống 2 admin sửa cấu hình hãng cùng lúc | **30 s** |

> 📌 **Tiền tố khoá — code đã thống nhất "không tiền tố" (đối chiếu 19/11/2026; ghi ở §15.2 F3):** host đặt
> `AbpDistributedLockOptions.KeyPrefix = "ecommerce:"` (`src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:237`) và ABP ghép
> **`KeyPrefix + name` KHÔNG có dấu phân cách** (`:229`), nên **module không được tự thêm `ecommerce:`**. Các module đang đúng quy ước đó:
> `ChannelHubConsts.LockKeys` (`modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:19`), `TaxLockKeys.Classification` →
> `tax:classification:{…}` (`modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:46`), `eCommerceConsts.LockKeys.InventorySku` →
> `inventory:{…}:{sku}` (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:817`). Chưa chạy `redis-cli MONITOR` để xem khoá thật trong Redis (🔶).
>
> **Quyết định cho `SHP-001`:** `ShippingHubLockKeys` sinh khoá `shipping:…` (không `ecommerce:`), theo `XC-003` §5.4 — khớp code.
> Việc còn lại **không** thuộc tài liệu này: `SHP-002` §8.2 (`shp-002-webhook-trang-thai-van-don.md:712-724`) vẫn dùng `ecommerce:shipping:…`
> ⇒ sẽ ra tiền tố đôi; sửa ở `SHP-002` (F3, Q4).

**Idempotency key:** xem §7.3.

---

## 9. Phân quyền

| Permission | Nhóm | Method được gán | Vai trò mặc định | Ghi chú |
|---|---|---|---|---|
| `eCommerce.Shipping.Shipments.View` | Shipping | `GetAsync`, `GetListAsync`, `GetCreateAttemptsAsync` | CSKH, kế toán, quản trị | **chỉ đọc** (dùng chung với `SHP-002`) |
| `eCommerce.Shipping.Shipments.Estimate` | Shipping | `EstimateOrderFeeAsync`, `EstimateCartFeeAsync` | mọi vai trò bán hàng, **và** khách ở checkout | không side effect; ⚠️ **có** quota của hãng ⇒ cần rate limit |
| `eCommerce.Shipping.Shipments.Create` | Shipping | `CreateShipmentAsync` | nhân viên kho, CSKH trưởng | ⭐ **có** hệ quả tài chính (phí thật) + gọi hãng ⇒ **audit** |
| `eCommerce.Shipping.Shipments.Cancel` | Shipping | `CancelShipmentAsync` | CSKH trưởng | **bắt buộc** `reason` + audit |
| `eCommerce.Shipping.Carriers.View` | Shipping | `GetCarriersAsync` | quản trị, vận hành | |
| `eCommerce.Shipping.Carriers.Manage` | Shipping | `UpsertCarrierAsync` | quản trị | đổi `IsEnabled`/`AddressModel` ⇒ **audit** |
| `eCommerce.Shipping.Rates.View` | Shipping | `GetRatesAsync` | kế toán, quản trị | |
| `eCommerce.Shipping.Rates.Manage` | Shipping | `ImportRatesAsync` | **kế toán trưởng** | ảnh hưởng phí hiển thị cho khách |

**🚨 BẮT BUỘC làm — nếu không ⇒ `403` cho MỌI người, kể cả admin:**
`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:23`–`59` hiện **chỉ**
khai permission của Products/Inventory/Channels/Orders/Payments/Tax — **KHÔNG** có `eCommerce.Shipping.*`.
Phải `AddPermission` + `AddChild` cho **toàn bộ** permission ở bảng trên trong file đó (bài học đã ghi trong
chính comment của file: *"permission KHÔNG được định nghĩa thì ABP luôn coi là bị từ chối ⇒ API trả 403 dù user
có role admin"* — `eCommercePermissionDefinitionProvider.cs:8`–`11`).

📌 **Nguyên tắc ABP (đã kiểm chứng ở Phase 0):** app service **không tự được bảo vệ** ⇒ **bắt buộc `[Authorize]`
ở cấp class** (`XC-004` BR-XC-004-001/002). Tiền lệ **thật** trong repo:
`src/eCommerce.Application/Channels/ChannelAppService.cs:21`–`27`.

> 📌 **Ngoại lệ `[AllowAnonymous]`:** `SHP-001` **không có**. Ngoại lệ duy nhất của module là **webhook** của
> `SHP-002`. ⚠️ `XC-004` Q1 đã **đóng** allowlist chỉ gồm `/connect/*`, `/api/abp/application-configuration`,
> `/health-status`, Swagger dev (`docs/features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md:505`) — **KHÔNG** có webhook
> hãng vận chuyển ⇒ **xung đột** phải xử lý (xem §15.2 **F4**).

---

## 10. UI Mockup & mapping dữ liệu

**Không có UI riêng** — màn hình vận đơn thuộc `WEB-001` (khung + menu + phân quyền). Tài liệu này **chốt dữ
liệu** mà UI **phải** hiển thị.

| Vùng UI | Thành phần | Dữ liệu | Nguồn | Quy tắc |
|---|---|---|---|---|
| **Checkout — bước vận chuyển** | dòng phí + nhãn | phí, tiền tệ, `IsEstimate` | `ShippingFeeEstimateDto` | ⭐ **BẮT BUỘC** hiện nhãn **"phí tạm tính"** khi `IsEstimate = true` và **"có thể thay đổi theo hãng"** — vì phí **chốt** là phí hãng báo lúc tạo vận đơn (ADR-04). 🚨 **Cấm** hiển thị phí ước tính như số cuối cùng |
| **Đơn hàng → nút "Tạo vận đơn"** | nút | — | `CreateShipmentAsync` | vô hiệu khi đơn **đã có** `Shipment` hiệu lực (A5); hiện spinner vì có thể chờ tới **p99 12 s** (NFR-002) |
| **Đơn hàng → khối vận đơn** | bảng nhỏ | mã vận đơn, hãng, trạng thái, **phí thật**, COD, cân nặng | `ShipmentDto` | ⭐ nếu `ShippingFee` **khác** `EstimatedShippingFee` ⇒ hiện **cả hai cạnh nhau** + badge vàng *"phí hãng cao/thấp hơn tạm tính n đồng"* (**E8**). 🚨 **Không** tự sửa số của hãng |
| **Khối vận đơn → "Lịch sử gọi hãng"** | timeline **chỉ đọc** | mọi `ShipmentCreateAttempt` | `ShipmentCreateAttemptDto` | **không** có nút Sửa/Xóa (append-only). Hiện rõ `Result = DuplicateIgnored` khác màu để người dùng hiểu **không** phải lỗi |
| **Khối vận đơn → nút "Huỷ vận đơn"** | hộp thoại | — | `CancelShipmentAsync` | 🚨 **ẩn/vô hiệu** khi `Status = Submitting` (không huỷ được — §3.2 #6) **hoặc** hãng **đã lấy hàng** (`LastProviderEventAtUtc` có sự kiện `PickedUp`) — nếu không, người dùng sẽ gặp `0017` khó hiểu. **Bắt buộc** nhập `reason` |
| **Khối vận đơn → giá trị COD** | nhãn **chỉ đọc** | `CodAmount` + `CodCurrency` | `ShipmentDto` | 🚨 **KHÔNG** có ô nhập/không có nút sửa. Hiện chú thích *"Đổi tiền thu hộ cần xác thực OTP với hãng — liên hệ vận hành"* (**ADR-07**, **BR-SHP-001-014**) |
| **Màn hình cấu hình hãng** | bảng + form | `ProviderCode`, `IsEnabled`, `AddressModel`, `ConfigKey` | `CarrierDto` | 🚨 **KHÔNG** hiển thị/không nhập token ở đây — token ở cấu hình/secret ngoài (BR-015); hiện `ConfigKey` để người vận hành biết **tra ở đâu** |
| **Màn hình bảng giá ước tính** | bảng + import | khung giá, hiệu lực, `Source` | `ShippingRateDto` | hiện **cảnh báo vàng**: *"bảng giá này chỉ để TẠM TÍNH; phí chốt là phí hãng báo"* (ADR-04) |
| **Mọi màn hình lỗi** | `CorrelationId` | response header | `XC-005` | hiển thị mã cho người dùng để tra log |

> 📌 **Ghi rõ chênh lệch (kế thừa thói quen tốt của tài liệu gốc — `docs/design-doc-guidelines.md:119`):**
> **tổng phí vận chuyển trên báo cáo** (`REP-*`) **khác** `Order.ShippingTotal`
> khi phí thật của hãng khác phí đã thu khách. UI **phải** ghi rõ **đang đếm số nào** (phí **đã thu khách** hay
> phí **hãng đã báo**). Đây là loại lệch rất dễ bị coi là bug — và liên quan trực tiếp `REC-003` (đối soát COD
> bằng **file Excel thủ công** theo phiên — `docs/implementation-plan.md:200`).

**Trạng thái rỗng:** khi chưa từng tạo vận đơn ⇒ hiện **hướng dẫn kiểm tra cấu hình hãng**
(`IsEnabled`, `ConfigKey`, `EnableLiveCalls`), **không** hiện bảng trống — vì "không có dữ liệu" ở đây thường là
**lỗi cấu hình**, không phải chưa có đơn.

---

## 11. Migration & seed

| Mục | Nội dung |
|---|---|
| Migration | **Có** — `Added_ShippingHub_CarriersAndShipments` (kế hoạch GĐ2; **chưa có trong code**). Khuôn tham chiếu: `modules/Tax/eCommerce.Tax.EntityFrameworkCore/TaxDbContextModelCreatingExtensions.cs` + migration `20261013012648_Add_Tax_Module` (`src/eCommerce.EntityFrameworkCore/Migrations/`) |
| Bảng tạo mới | `ShpCarriers`, `ShpShippingServices`, `ShpShippingRates`, `ShpShipments`, `ShpShipmentCreateAttempts` |
| **Bắt buộc sửa app** | 1. Khai **`DbSet`** cho 5 entity trong `src/eCommerce.EntityFrameworkCore/eCommerceDbContext.cs` — ⚠️ **thiếu `DbSet` ⇒ `IRepository<T,Guid>` KHÔNG được đăng ký**; lỗi này **đã gặp thật** và **đã ghi trong chính file**: `eCommerceDbContext.cs:281` (*"The requested service `IRepository<ChannelShop, Guid>` has not been registered"*), lưới an toàn CI `DbSetRegistrationTests` (`:310`).<br/>2. Gọi `builder.ConfigureShippingHub()` trong `OnModelCreating` — theo khuôn `builder.ConfigureTax()` (`eCommerceDbContext.cs:349`).<br/>3. Tạo `modules/ShippingHub/eCommerce.ShippingHub.EntityFrameworkCore` với `ShippingHubEntityFrameworkCoreModule` **rỗng** (chỉ `[DependsOn]`), đúng khuôn `modules/Tax/eCommerce.Tax.EntityFrameworkCore/TaxEntityFrameworkCoreModule.cs:19`–`25` — **KHÔNG** DbContext riêng.<br/>4. Thêm 4 project ShippingHub vào `eCommerce.slnx` (khuôn khối `Address`, `eCommerce.slnx:3`–`8`).<br/>5. Thêm `typeof(ShippingHub...Module)` vào `[DependsOn]` của host — `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:36`–`53`.<br/>6. `Configure<ShippingHubOptions>(configuration.GetSection("ShippingHub"))` — khuôn `eCommerceHttpApiHostModule.cs:263`–`264`.<br/>7. Thêm **8 permission** `eCommerce.Shipping.*` vào `eCommercePermissionDefinitionProvider` (`src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs:23`) — xem §9.<br/>8. Thêm mã lỗi **`0013`–`0018`** + `DonChuaCoDiaChiGiao` + `VanDonCuChuaRoKetQua` (số chưa cấp — Q7) vào `ShippingHubErrorCodes` (**module sở hữu** — ADR-10).<br/>9. ⭐ Thêm metric mới vào `eCommerceMetricNames.All` (`shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:81`) + instrument (`:237`) + **`XC-005` §5.1** — ⚠️ **cùng lúc**, nếu không **CI đỏ** (`MetricsTests.TC06_phat_ra_DUNG_bang_metric_da_dang_ky` — `MetricsTests.cs:39`; test không còn đếm cứng).<br/>10. Outbox/Inbox **đã bật** (`eCommerceEntityFrameworkCoreModule.cs:86-87`) — không cần làm; chỉ phải ghi ETO qua outbox. |
| Seed | `ShpCarriers` cho `ghn`: `IsEnabled = false` mặc định ⚠️ (**fail-closed**, tránh gọi hãng ngoài ý muốn ở môi trường dev) + `AddressModel = TwoLevel` + `ConfigKey = "ghn"`. `ShpShippingServices`/`ShpShippingRates`: **KHÔNG seed** — vì mã dịch vụ và biểu phí **chưa kiểm chứng** (§15.1 Q2). Seed **idempotent** (khuôn `src/eCommerce.DbMigrator/TaxRateScheduleDataSeedContributor.cs:52`: `GetCountAsync() > 0 ⇒ return`) |
| Cấu hình seed | 🚨 **Không seed secret.** `ApiToken`/`ShopId` **phải** đến từ cấu hình/secret ngoài (BR-015; `XC-004` BR-XC-004-012) |
| Ảnh hưởng dữ liệu cũ | **Không backfill.** Chưa có vận đơn nào tồn tại (module chưa có). `Order.ShippingTotal` (`modules/Sales/eCommerce.Sales.Domain/Order.cs:162`) đã tồn tại và **giữ nguyên** — `SHP-001` **không** ghi cột đó (xem §7.1) |
| Rollback | `Down()` xoá 5 bảng. ⚠️ **An toàn chỉ khi chưa có nghiệp vụ chạy.** Nếu đã có ⇒ **phải** xử lý `ShpShipments` ở trạng thái `Submitting`/`SubmitFailed` **trước khi** rollback (chúng là **dấu vết duy nhất** của một lời gọi hãng có thể đã thành công — ADR-02 Pha 2) |
| ⚠️ Giới hạn môi trường | Máy dev **không có PostgreSQL/Docker** (kế thừa `XC-006` §14.5, `SHP-002` §11) ⇒ migration + ràng buộc **UNIQUE partial** + `NULLS NOT DISTINCT` **chưa từng chạy thật**. Việc **đầu tiên** khi có môi trường: `dotnet ef database update` + **1 test chèn 2 `Shipment` cùng `ClientOrderCode`** để chứng minh UNIQUE **chặn được** (`TC-05`, `TC-32`) và **1 test chèn 2 `Shipment` khác đơn cùng `Draft`** để chứng minh index `ProviderTrackingCode` **không** chặn nhầm (`TC-49`) — cả hai với `TenantId = NULL` |

---

## 12. Xử lý lỗi & trường hợp biên

> Phân biệt **(a) không có dữ liệu — KHÔNG phải lỗi** với **(b) lỗi thật** (`docs/templates/feature-design-template.md:362`).

| Tình huống | Nguyên nhân | Hành vi hệ thống | Phản hồi tới client |
|---|---|---|---|
| **Đơn chưa có dòng hàng / cân nặng rỗng** | dữ liệu chưa đủ | **Không** phải lỗi: dùng `DefaultWeightGrams` cấu hình + log `Information` 1 lần | **`200`** + `IsEstimate = true` + cờ `WeightIsDefault` |
| **Hãng không hỗ trợ tuyến** | tuyến ngoài vùng phủ | **Không** phải lỗi hệ thống: trả danh sách dịch vụ **rỗng** + lý do | **`200`** (rỗng) — không `500` |
| **Hãng chưa cấu hình** | thiếu token/`BaseUrl`/`ShopId` | **Fail-closed**: **NÉM** `eCommerce:Shipping:0013`; **không** gọi hãng | **`400`** (E1) |
| **Địa chỉ không hợp lệ với mô hình hãng** | thiếu cấp bắt buộc (3 cấp vs 2 cấp) | **NÉM** `eCommerce:Shipping:0014` **trước khi** gọi hãng; metric `{reason="invalid_address"}` | **`400`** (E2) |
| **Đơn thiếu snapshot giao hàng** | đơn sàn/POS không địa chỉ, hoặc `SAL-002` chưa lưu snapshot (`D-108`) | **NÉM** `DonChuaCoDiaChiGiao` **trước Pha 1** — 0 `Shipment` `Draft`, **không** gọi hãng; metric `{reason="invalid_address"}` | **`400`** (E10, BR-021) |
| **Không tra được mã địa chỉ hãng** | cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`) ném: mã đơn vị không tồn tại (`Address:0001`), `SchemaVersion` sai (`Address:0005`), không có ánh xạ `DaXacMinh` (`0501`/`0502`), đơn vị đích đóng (`0504`), hoặc **> 1** ánh xạ `DaXacMinh` cùng đích | **NÉM** `0014` kèm mã lỗi của `Address` (nhóm NÉM `0014` gồm cả `Address:0001`/`0005`); **cấm** chọn ánh xạ đầu/mới nhất, **cấm** tra chéo phiên bản; **không** gọi hãng; metric `{reason="invalid_address"}` | **`400`** (E2, BR-020) |
| **Tạo lại vận đơn khi vận đơn trước chưa rõ kết quả** | `Shipment` mới nhất `SubmitFailed` vượt trần, có attempt `TransientFailure` mà chưa tra được ở hãng (hãng có thể **đã** tạo đơn) | **NÉM** `VanDonCuChuaRoKetQua` (§6.3): 0 `Shipment` mới, 0 lời gọi hãng; cảnh báo vận hành (như dòng "`Submitting` mồ côi") — **không** tự tạo generation mới mù (`D-109`) | **`409`** (§3.2 #1) |
| **Hãng từ chối (4xx nghiệp vụ)** | dữ liệu sai / hãng đổi quy tắc | **NÉM** `eCommerce:Shipping:0015`; `Status = SubmitFailed`; ghi attempt `Rejected`; log `Warning`; **KHÔNG** rollback `ClientOrderCode` đã ghi | **`400`** (E3) |
| **Hãng "thành công" mà KHÔNG có mã vận đơn** | lỗi hãng / đổi phiên bản API | **NÉM** `0015` — **cấm** coi là thành công; log `Error`; metric | **`500`** (lỗi tích hợp) |
| **Hãng timeout / 5xx** | sự cố hãng | `Status = SubmitFailed`, `FailureKind = Transient`; **KHÔNG** rollback; **thử lại nhịp sau**; metric `shipping_carrier_call_failed_total{provider,reason="transient"}` | **`202`** hoặc lỗi có mã (tùy chọn đồng bộ) — **không** `500` trần |
| **Hãng báo "mã đơn đã tồn tại"** | lần thử trước đã tạo thật | ⭐ **Không** phải lỗi: `AlreadyExisted = true`; tra lại vận đơn; ghi attempt `DuplicateIgnored`; metric `shipping_shipment_duplicate_ignored_total` | **`200`** (A1) |
| **Vượt trần thử** | lỗi kéo dài | `Status = SubmitFailed` (**hấp thụ**) + `eCommerce:Shipping:0016` + **cảnh báo** cho vận hành; **cấm** thử vô hạn | **`409`** (E5) |
| **`Submitting` mồ côi** (ta chết trong lúc chờ hãng) | tiến trình bị kill | **Job** quét `Submitting` quá `StuckSubmittingMinutes` ⇒ tra hãng theo `ClientOrderCode` (nếu có API — §15.1 Q3); nếu **không** tra được ⇒ **cảnh báo** cho vận hành, **KHÔNG** tự tạo lại mù; CSKH **không** huỷ được bản này (§3.2 #6) — đường thủ công: Q19 | — (nội bộ) |
| **Không giành được khoá (đường HTTP)** | instance/nhân viên khác đang chạy | **NÉM** `eCommerce:Lock:0001` (`XC-003` D10 — HTTP **phải** ném, **cấm** `SkipIfBusy`) | **`409`** (E6) |
| **Không giành được khoá (job định kỳ)** | instance khác đang chạy | **Bỏ qua nhịp** + log + metric `lock_skipped_total{module="shipping"}` (`XC-003` A1) — **KHÔNG** ném lỗi | — (nội bộ) |
| **`UPDATE … WHERE Status` 0 dòng** | luồng khác vừa đổi | **NÉM LỖI** `eCommerce:Shipping:0017` — **cấm** coi là thành công (`XC-002` BR-XC-002-008) | **`409`** (E7) |
| **Phí hãng báo LỆCH phí ước tính** | biểu phí đổi / phụ phí | **KHÔNG** ném lỗi (vận đơn đã tạo thật): ghi `EstimatedShippingFee` vs `ShippingFee` + log `Warning` + metric `shipping_fee_discrepancy_total{provider}`; **cấm** tự sửa số của hãng | — (nội bộ) + badge trên UI (§10) |
| **Yêu cầu ĐỔI COD** | người dùng muốn sửa tiền thu hộ | **NÉM** `eCommerce:Shipping:0018` — 🚨 **cấm** tự động hoá: GHN **bắt buộc OTP** (`docs/implementation-plan.md:199`, `:426`) | **`400`** + hướng dẫn quy trình thủ công (E9) |
| **Huỷ vận đơn sau khi hãng đã lấy hàng, hoặc khi `Submitting`** | nhân viên bấm muộn / bấm khi chưa rõ kết quả | **NÉM** `0017`; log `Error`; **KHÔNG** gọi API huỷ; trạng thái không đổi | **`409`** (§3.2 #6) |
| **`TenantId = NULL` (1 tenant)** | cấu hình production hiện tại (`XC-004` F1) | Mọi unique index mới có `NULLS NOT DISTINCT` (§5.2) nên hàng rào DB **vẫn** có hiệu lực; `TC-32`/`TC-49` **phải** chạy với `TenantId = NULL`. Log `Warning` 1 lần khi khởi động nếu `TenantId` rỗng — không phải lỗi | — (nội bộ) |
| **Redis chết** | sự cố hạ tầng | App **vẫn chạy** (`AbortOnConnectFail = false` — `eCommerceHttpApiHostModule.cs:251`); mất tính tuần tự, hàng rào còn lại là unique index DB | Bình thường |
| **`EnableLiveCalls = false` (dev)** | cấu hình PoC | Provider trả kết quả **mô phỏng**; luồng lock/UoW/outbox **vẫn chạy đủ** (A3) | **`200`** + `providerTrackingCode` tiền tố `poc-` |
| **Payload/response hãng chứa PII** | hãng gửi thêm dữ liệu | **Bắt buộc** đi qua hàm che **trước khi** lưu/log — dùng lại cách tiếp cận `PaymentTransaction.MaskSensitiveData` (`modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:147`) | — (nội bộ) |

**Chiến lược transaction:**

| Bước | Phạm vi | Ghi chú |
|---|---|---|
| **Vào app service / job** | `CreateShipmentAsync`, `CancelShipmentAsync`, job thử lại: `[UnitOfWork(isTransactional: false)]` — tắt transaction ngầm của host (`TransactionBehavior.Auto`, `eCommerceHttpApiHostModule.cs:94`; tiền lệ `PaymentAppService.cs:561`) | Mỗi pha dưới đây mở UoW **tường minh** `Begin(requiresNew: true, isTransactional: true)` + `CompleteAsync` **trước** khi sang bước kế (BR-002) |
| **Pha 1 — ý định** | UoW **ngắn riêng**: `INSERT Shipment (Draft)` + `Generation` + `ClientOrderCode` ⇒ **commit** | `ClientOrderCode` **phải bền vững TRƯỚC** khi gọi hãng (BR-003). Chết sau đây ⇒ **an toàn** |
| **Pha 2 — bằng chứng** | UoW **ngắn riêng**: `UPDATE Status = Submitting, SubmitAttemptCount++` (`WHERE Status = Draft`) ⇒ **commit** | Để lại **bằng chứng đã gọi** ⇒ job quét được (ADR-02) |
| **Pha 2b — gọi hãng** | ⚠️ **NGOÀI mọi transaction** (chỉ còn UoW ngầm `isTransactional: false`, không giữ transaction/kết nối ghi) | Tránh transaction dài + tránh giữ khoá DB khi chờ mạng (kế thừa `SHP-002` §12 B3) |
| **Pha 3 — kết quả** | UoW **riêng** có transaction: `UPDATE` kết quả (`WHERE Status = Submitting`) + `INSERT ShpShipmentCreateAttempts` + **outbox** ETO **cùng transaction** (`XC-002` BR-XC-002-001) | `0` dòng ⇒ **NÉM LỖI** (E7). Nếu outbox chưa bật ⇒ **chặn** `SHP-001` (§4.2) |
| **Huỷ (`Created` → `Cancelled`)** | Gọi `CancelShipmentAsync` của provider **ngoài** transaction; **chỉ khi hãng xác nhận** mới mở UoW riêng: `UPDATE Status = Cancelled` (`WHERE Status = Created`) + `INSERT` attempt `Result = Cancelled`, `AttemptNo = 0` + **outbox** `ShipmentCancelledEto` cùng transaction | `0` dòng ⇒ **NÉM** (E7). Từ `Draft`: chỉ `UPDATE` (`WHERE Status = Draft`), không gọi hãng, không có dòng attempt |
| **Job thử lại** | Mỗi vận đơn 1 UoW (hoặc lô nhỏ, `Begin(requiresNew: true, …)`); gọi hãng **ngoài** transaction | `AttemptCount` tăng; vượt trần ⇒ E5 |
| **Phạm vi khoá** | `await using` **bao ngoài** các UoW (tầng app service/job) | Khoá được nhả **sau** khi transaction kết thúc (`XC-003` §12) |

---

## 13. Phi chức năng

| Mã | Yêu cầu | Ngưỡng đo được | Cách kiểm chứng |
|---|---|---|---|
| NFR-SHP-001-001 | Độ trễ lấy phí | p95 **< 2 s** | benchmark với provider giả |
| NFR-SHP-001-002 | Độ trễ tạo vận đơn | p95 **< 5 s** · p99 **< 12 s** | benchmark + test tải |
| NFR-SHP-001-003 | Không tạo vận đơn trùng | gọi **1.000 lần** cùng đơn ⇒ **1** vận đơn, **1** attempt `Created` | integration test (`TC-05`) |
| NFR-SHP-001-004 | Không giữ transaction khi chờ mạng | **0** transaction DB mở trong lúc HTTP; UoW ngầm của app service chỉ ở dạng `isTransactional: false` (BR-002, §1, §4.2) | `TC-08` (quét mã nguồn) + `TC-09` (integration, đường HTTP thật, `pg_stat_activity`) |
| NFR-SHP-001-005 | `CorrelationId` hiện diện | **100 %** log luồng gọi hãng | integration test (`XC-005` NFR-XC-005-002) |
| NFR-SHP-001-006 | Không rò secret | **0** secret/token hãng trong repo | quét mã nguồn CI (`TC-22`) |
| NFR-SHP-001-007 | Cách ly tenant | **0** bản ghi tenant khác lọt | integration test 2 tenant (⚠️ **cần PostgreSQL**) |
| NFR-SHP-001-008 | Metric nằm trong bảng đăng ký | **0** metric phát ra ngoài `eCommerceMetricNames.All` | `MetricsTests.TC06_khong_co_metric_phat_ra_ngoai_bang_dang_ky` (`test/eCommerce.Tests/Observability/MetricsTests.cs:50`) |
| NFR-SHP-001-009 | Timeout & retry | timeout **10 s**; retry **≤ 3**; backoff **1→5→25 s**; trần thử **5** | test cấu hình (`TC-21`) |
| NFR-SHP-001-010 | Không hard-code endpoint/mã hãng | **0** literal URL/mã dịch vụ trong code nghiệp vụ | test grep trong CI (`TC-23`) |
| NFR-SHP-001-011 | Log không ngập | job chạy khi **rỗng** ⇒ **< 10** dòng log/nhịp | đo `XC-005` TC-09 (`TC-24`) |
| NFR-SHP-001-012 | Độ trễ phát ETO | `Created` ⇒ ETO ra khỏi outbox **p95 ≤ 2 s** khi hệ thống rảnh | kế thừa `XC-002` NFR-XC-002-001 |

**Metric phát ra** *(⚠️ bắt buộc cập nhật `XC-005` §5.1 và `eCommerceMetricNames.All` trong cùng PR — xem ghi chú dưới bảng)*

| Tên metric | Loại | Nhãn (hữu hạn) | Ý nghĩa / ngưỡng | Đã đăng ký? |
|---|---|---|---|---|
| `shipping_carrier_call_failed_total` | Counter | `provider`, `reason` (`timeout`/`transient`/`business`/`not_configured`/`invalid_address`) | số lời gọi hãng thất bại. Tăng đột biến ⇒ điều tra hãng | ❌ **chưa có trong code** — kế hoạch GĐ2 |
| `shipping_fee_discrepancy_total` | Counter | `provider` | ⭐ số lần **phí hãng LỆCH** phí ước tính (**E8**). **> 0 liên tục ⇒ bảng giá ước tính đã lỗi thời** | ❌ chưa có |
| `shipping_shipment_duplicate_ignored_total` | Counter | `provider` | số lần hãng báo "mã đơn đã tồn tại" (**A1**) ⇒ đo chất lượng idempotency | ❌ chưa có |
| `shipping_shipments_pending_submit` | Gauge | — | số `Shipment` ở `Submitting`/`SubmitFailed` chưa kết thúc. **> 0 liên tục ⇒ cảnh báo** | ❌ chưa có |

> ✅ **Nhãn dùng lại đúng tập cho phép** ở `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:212`
> (`eCommerceMetricTags.Allowed`: `provider`, `reason`, `kind`, `result`, `module`…) — **không** thêm nhãn mới.
> 🚨 **Cấm** dùng `orderId`/`shipmentId`/`trackingCode`/`clientOrderCode` làm nhãn
> (`XC-005` BR-XC-005-009; danh sách cấm trong code: `eCommerceMetricTags.Forbidden`, `eCommerceMetrics.cs:219`).
>
> ⚠️ **Hệ quả bắt buộc:** thêm **4** metric ⇒ **4** tên vào `eCommerceMetricNames.All` (`eCommerceMetrics.cs:81`) **và 4** instrument vào
> `eCommerceMetrics` (`:237`) **đồng thời** sửa `XC-005` §5.1; lệch ⇒ `MetricsTests.TC06_phat_ra_DUNG_bang_metric_da_dang_ky`
> (`MetricsTests.cs:39`) **đỏ**. Số `12` **không còn** là hằng cứng trong test (`MetricsTests.cs:64`). Cộng dồn với 6 metric của `SHP-002`
> ⇒ hai tài liệu nên gộp registry trong một PR (§15.1 Q8).

**Log (bắt buộc có `CorrelationId` — `XC-005` §5.2)**

| Sự kiện | Mức log | Trường | Ghi chú |
|---|---|---|---|
| Ước tính phí | **Không log** khi thành công (vòng lặp nóng — checkout gọi liên tục) | — | `XC-005` BR-XC-005-004: chỉ log khi **có việc thật** hoặc khi lỗi |
| Lấy phí thất bại | `Warning` | `CorrelationId`, `Provider`, `reason`, `DurationMs`, `ErrorCode` | **cấm** log payload thô |
| Bắt đầu gọi tạo vận đơn | `Information` | `CorrelationId`, `Provider`, `ClientOrderCode`, `AttemptNo`, `TotalWeightGrams` | 🚨 **cấm** log token |
| Tạo vận đơn thành công | `Information` | + `ProviderTrackingCode`, `ShippingFee`, `DurationMs` | ⭐ dùng **`ProviderTrackingCode`**, **không** dùng `ShipmentId` làm nhãn metric |
| Hãng báo mã đơn đã tồn tại | `Information` | + `result="duplicate_ignored"` | **không** phải lỗi (A1) |
| Hãng từ chối | `Warning` | `CorrelationId`, `Provider`, `ClientOrderCode`, `FailureCode`, `DurationMs` | BR-012 |
| Timeout / 5xx | `Warning` | như trên + `reason="transient"`, `AttemptNo` | thử lại nhịp sau |
| Lệch phí | `Warning` | `CorrelationId`, `Provider`, `ClientOrderCode`, `EstimatedFee`, `ActualFee` | ⭐ `EstimatedFee`/`ActualFee` **chỉ ở log** — **cấm** làm nhãn metric |
| Yêu cầu đổi COD bị từ chối | `Warning` | `CorrelationId`, `ShipmentId`, `ErrorCode = eCommerce:Shipping:0018` | ADR-07 |
| `UPDATE` 0 dòng | `Error` | + `expected`, `actual`, `ErrorCode = 0017` | phải có dấu vết (BR-005) |
| Job thử lại khởi động/dừng/lỗi | `Information`/`Error` | `JobName`, `CorrelationId`, `DurationMs` | ⚠️ **cấm** log mỗi nhịp khi rỗng (`XC-005` BR-XC-005-011) |

---

## 14. Test cases & Acceptance Criteria

### 14.1 Danh sách test case

| ID | Mục tiêu | Loại | Input chính | Expected | Phủ |
|---|---|---|---|---|---|
| **TC-01** | Ước tính phí từ bảng giá nội bộ | Unit | tuyến + cân nặng khớp `ShpShippingRates` | trả `IsEstimate = true` + số đúng; **0** lời gọi hãng | ADR-04 |
| **TC-02** | Ước tính phí khi `EnableLiveCalls = false` | Unit | provider mô phỏng | kết quả mô phỏng; luồng chạy đủ | A3, ADR-11 |
| **TC-03** | Hãng chưa cấu hình ⇒ **fail-closed** | Unit | thiếu `ApiToken`/`BaseUrl` | `eCommerce:Shipping:0013`; **0** lời gọi hãng | **BR-011**, ADR-11, E1 |
| **TC-04** | Địa chỉ thiếu cấp bắt buộc ⇒ từ chối **trước khi** gọi hãng | Unit | `AddressModel = ThreeLevel`, thiếu `DistrictCode` | `eCommerce:Shipping:0014`; **0** HTTP call | **BR-011**, ADR-06, E2 |
| **TC-05** | 🚨 **Gọi 1.000 lần cùng đơn ⇒ đúng 1 vận đơn** | **Integration** | 1.000 lời gọi song song/liên tiếp cùng `orderId` | **1** `Shipment`, **1** attempt `Created`; **0** vận đơn trùng | ⭐ **BR-003/004**, **NFR-003**, ADR-03 |
| **TC-06** | Hãng báo "mã đơn đã tồn tại" ⇒ dùng lại kết quả | Unit | provider trả `AlreadyExisted = true` | `Status = Created`; attempt `DuplicateIgnored`; metric tăng | **A1**, §3.2 #9 |
| **TC-07** | `ClientOrderCode` **ổn định** qua các lần retry cùng `Shipment` (`D-109`) | Unit | 3 lần thử liên tiếp (`SubmitAttemptCount` 1, 2, 3) trên **cùng** `Shipment` | **cùng** `ClientOrderCode` và **cùng** `Generation` cả 3 lần; `ShpShipmentCreateAttempts.AttemptNo` = 1, 2, 3; **0** `Guid.NewGuid()` | ⭐ **BR-003**, **BR-022** |
| **TC-08** | Tạo vận đơn **đúng 3 pha**, UoW ngầm đã tắt | Unit (quét) | đọc mã nguồn `ShipmentAppService.CreateShipmentAsync`/`CancelShipmentAsync` + job thử lại | (a) mỗi method/job có `[UnitOfWork(isTransactional: false)]`; (b) mọi `IUnitOfWorkManager.Begin` trong đó có `requiresNew: true`; (c) **0** chỗ gọi provider **bên trong** một `Begin`; thiếu (a) hoặc (b) ⇒ test đỏ | ⭐ **BR-002**, **NFR-004**, ADR-02 |
| **TC-09** | Không transaction nào mở trong lúc chờ HTTP — qua đường HTTP/app service **thật** (host có `TransactionBehavior.Auto`) | **Integration** | `POST` tạo vận đơn, provider giả trì hoãn 2 s | trong 2 s đó: **0** transaction DB mở của request (`pg_stat_activity`) **và** một kết nối khác đọc thấy `Shipment` `Submitting` (Pha 2 đã commit) | **BR-002**, NFR-004 |
| **TC-10** | Chuyển trạng thái **sai** ⇒ **NÉM LỖI** | Unit | `Cancelled → Submitting` | `BusinessException` `eCommerce:Shipping:0017` | ⭐ **BR-005**, §3.2 #7 |
| **TC-11** | `UPDATE … WHERE Status` **0 dòng** ⇒ **NÉM LỖI** | **Integration** | 2 luồng cùng `Draft → Submitting` | **đúng 1** thành công; luồng kia **ném lỗi**, **không** coi là thành công | **BR-005**, E7 |
| **TC-12** | Hãng "thành công" mà **không có** mã vận đơn | Unit | provider trả `Success = true`, `ProviderTrackingCode = null` | `eCommerce:Shipping:0015` — **cấm** `Created` | **§3.2 #3**, E3 |
| **TC-13** | Hãng timeout ⇒ `SubmitFailed` + thử lại được | Unit | provider ném timeout | `Status = SubmitFailed`, `FailureKind = Transient`; attempt ghi lỗi; **0** rollback `ClientOrderCode` | **E4**, **BR-013** |
| **TC-14** | Vượt trần thử ⇒ **hấp thụ** + cảnh báo | Unit | `SubmitAttemptCount = MaxSubmitAttempts` | `eCommerce:Shipping:0016`; **0** lần gọi hãng thêm | **E5**, **BR-013** |
| **TC-15** | Lệch phí ⇒ **ghi nhận**, **không** ném lỗi | Unit | estimate `37000`, hãng báo `38500` | `Status = Created`; `ShippingFee = 38500`; metric `shipping_fee_discrepancy_total` tăng | ⭐ **ADR-04**, **E8**, **BR-009** |
| **TC-16** | Tiền so bằng `Money`, không `==` trên `decimal` trần | Unit | `Money.Vnd(38500.0m)` vs `Money.Vnd(38500.00m)`; `Money.Vnd` vs `Money.Usd` cùng số | cùng loại ⇒ **bằng nhau** (`Equals`/`CompareTo`); khác loại tiền ⇒ `CompareTo` **NÉM** `MoneyErrorCodes.CurrencyMismatch`; **0** `==` trên `decimal` tiền trong mã nguồn | ⭐ **BR-007** |
| **TC-17** | Ra biên hãng tiền là **số nguyên** | Unit | request tới provider | `CodAmountMinorUnits` là `long`; **0** `double`/`float` trong payload | **BR-008** |
| **TC-18** | Đơn **đã có** vận đơn hiệu lực ⇒ trả vận đơn cũ | Unit | gọi `CreateShipmentAsync` lần 2 | **0** lời gọi hãng; trả vận đơn hiện có | **A5**, **BR-004** |
| **TC-19** | 🚨 **Không tồn tại** đường đổi COD | Unit (quét) | quét `*AppService` của ShippingHub tìm `Cod`/`Otp`/`ChangeCod` | **0** method đổi COD; **0** endpoint | ⭐ **BR-014**, **ADR-07** |
| **TC-20** | Huỷ sau khi hãng đã lấy hàng ⇒ **NÉM LỖI** | Unit | `LastProviderEventAtUtc` có sự kiện `PickedUp` | `eCommerce:Shipping:0017`; **0** lời gọi huỷ | **§3.2 #6**, A4 |
| **TC-21** | Timeout & retry theo cấu hình | Unit | `CallTimeoutSeconds = 10`, `MaxRetries = 3` | **đúng** 3 lần thử; **0** retry vô hạn; **0** HttpClient mặc định 100 s | **BR-013**, **NFR-009** |
| **TC-22** | Không có secret trong repo | Unit (quét) | grep token/key/secret của hãng | **0** match ngoài placeholder rỗng | **BR-015**, NFR-006 |
| **TC-23** | Không hard-code endpoint/mã dịch vụ | Unit (grep) | quét mã nguồn | **0** literal URL/`ServiceId` | **BR-011**, NFR-010 |
| **TC-24** | Job rỗng ⇒ không ngập log | **Integration** | để hệ thống rỗng 1 nhịp job | **< 10** dòng log | NFR-011, `XC-005` BR-XC-005-004 |
| **TC-25** | `[Authorize]` **cấp class** cho mọi app service | Unit (reflection) | quét assembly ShippingHub | **100 %** có `[Authorize]` ở cấp class; **0** `[AllowAnonymous]` trong `SHP-001` | ⭐ **BR-016**, `XC-004` BR-XC-004-001 |
| **TC-26** | `Shipment` tạo ra có **đủ** `ProviderTrackingCode` để `SHP-002` khớp được | **Integration** | tạo vận đơn → mô phỏng webhook của `SHP-002` | webhook khớp được vận đơn theo `(TenantId, ProviderCode, ProviderTrackingCode)` | §7.2, ADR-09 |
| **TC-27** | Cách ly tenant | **Integration** | tenant A tạo vận đơn cho đơn **của B** | **không** chạm dữ liệu B | **NFR-007**, BR-011 |
| **TC-28** | Không giành được khoá: HTTP ⇒ **NÉM**, job ⇒ **bỏ qua có log** | Unit | 2 chế độ | HTTP: `eCommerce:Lock:0001`; job: **0** ném + log + `lock_skipped_total{module="shipping"}` | **BR-017**, `XC-003` BR-010/D10 |
| **TC-29** | Metric nằm trong bảng đăng ký + không nhãn cardinality cao | Unit | so với `eCommerceMetricNames.All` và `eCommerceMetricTags.Forbidden` | **0** metric ngoài bảng; **0** nhãn cấm; mọi tên mới có trong `eCommerceMetricNames.All` | **BR-018**, **BR-019**, NFR-008 |
| **TC-30** | Ghi dữ liệu **đã che** PII | Unit | response hãng chứa số điện thoại/số tài khoản | `FailureMessage` đã che; **0** log payload thô | **BR-012** |
| **TC-31** | Cùng `(tenant, provider, ClientOrderCode)` **khác** `RequestPayloadHash` | **Integration** | 2 request khác nội dung, cùng mã | `eCommerce:Messaging:0001` (`MessagingErrorCodes.PayloadConflict`, `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/MessagingErrorCodes.cs:14`; `0002` là `DangXuLy`) — **cấm** ghi đè | **BR-017**, `XC-002` E1 |
| **TC-32** | UNIQUE index **chặn ở tầng DB**, **cả** `TenantId = NULL` **và** `TenantId` khác `NULL` | **Integration** | chèn thẳng 2 `Shipment` cùng `(ProviderCode, ClientOrderCode)` bằng SQL thô, mỗi trường hợp tenant một lần | **DB chặn** (`23505`) ở cả hai trường hợp — ca `NULL` là cấu hình production | ⭐ **BR-003** (hàng rào DB — `XC-003` D2) |
| **TC-33** | Mọi lời gọi hãng đi qua `IShippingCarrierProvider` | Unit (quét) | quét `ShippingHub.Application` + job tìm `HttpClient`/`IHttpClientFactory` ngoài lớp provider | **0** chỗ gọi HTTP trực tiếp từ app service/controller/job; test **đỏ** nếu có | **BR-SHP-001-001**, ADR-01 |
| **TC-34** | Mã địa chỉ hãng chỉ qua cửa của `Address.Application.Contracts` (`D-89`/`D-110`) | Unit | (a) quét: **0** truy vấn `AddrCarrierMappings`/repository ánh xạ trong `ShippingHub`; (b) resolver giả ném "> 1 ánh xạ `DaXacMinh` cùng đích" | (a) **0** match; (b) `eCommerce:Shipping:0014`, **0** lời gọi hãng, không chọn mã nào | **BR-SHP-001-020**, E2, `D-89` |
| **TC-35** | Trạng thái hấp thụ không đổi được | Unit | `Cancelled`/`SubmitFailed` (đã vượt trần) → `BeginSubmit`/`ApplyCarrierSuccess`; `Created` → sang trạng thái khác ngoài huỷ theo A4 | `BusinessException` `eCommerce:Shipping:0017`; trạng thái **không đổi** | **BR-SHP-001-006**, §3.2 #7 |
| **TC-36** | `CreateShipmentDto`/`EstimateOrderShippingFeeDto` (theo đơn) **không** nhận địa chỉ/người nhận/COD (`D-108`) — `EstimateCartShippingFeeDto` (giỏ) **không** nằm trong ca quét | Unit (reflection) | quét thuộc tính hai DTO | **0** thuộc tính tên `Recipient*`, `Address*`, `*Code` địa chỉ (tỉnh/huyện/xã), `OurAdministrativeUnitId`; riêng `CreateShipmentDto` còn **0** thuộc tính `Cod*`/`Currency`/`Items`; test **đỏ** nếu có | ⭐ **BR-SHP-001-021**, `D-108` |
| **TC-37** | Địa chỉ gửi hãng lấy từ snapshot đơn qua hợp đồng Sales | Unit | hợp đồng Sales **giả** trả snapshot X; resolver giả trả mã hãng | `CarrierCreateShipmentRequest.ToAddress` khớp X (tên, SĐT, địa chỉ) + mã hãng; `ShpShipments` lưu bản sao X | **BR-SHP-001-021**, `D-108` (6), `D-77` |
| **TC-38** | Đơn thiếu snapshot ⇒ NÉM trước Pha 1 | Unit | hợp đồng Sales giả trả đơn **không** snapshot (và biến thể thiếu tên/SĐT/cấp xã) | `BusinessException` mã `DonChuaCoDiaChiGiao`; **0** `Shipment`; **0** lời gọi provider; **0** lời gọi `ResolveCarrierAddressCodeAsync` | ⭐ **BR-SHP-001-021**, E10, `D-108` (5) |
| **TC-39** | ShippingHub không phụ thuộc Sales ngoài `Application.Contracts` | Unit (quét) | quét `using`/`ProjectReference` của 4 project ShippingHub | **0** `using eCommerce.Sales` ngoài `eCommerce.Sales.Application.Contracts`; **0** tham chiếu `Sales.Domain`/`Sales.EntityFrameworkCore`; test **đỏ** nếu có | **BR-SHP-001-021**, `D-108` (2) |
| **TC-40** | Tạo lại sau `Cancelled` ⇒ `Generation` mới, mã khác (`D-109`) | Unit | `Shipment` generation 1 ở `Cancelled`; gọi `CreateShipmentAsync` lần nữa | `Shipment` mới `Generation = 2`, `ClientOrderCode` khác, đuôi `-2`; `Shipment` cũ không đổi; mã **không** trùng mã cũ | ⭐ **BR-022**, **BR-003**, §3.2 #1 |
| **TC-41** | `TaoClientOrderCode` là hàm thuần, ASCII, ≤ 50 ký tự (`D-109`) | Unit | `generation` ∈ `{1, 2, 10, int.MaxValue}`, `orderId` bất kỳ | cùng input ⇒ cùng output; ASCII; độ dài ≤ 43 (≤ 50); adapter GHN với mã > 50 ⇒ **NÉM**, **không** cắt/băm | **BR-022** |
| **TC-42** | Hai Pha 1 đồng thời cho một đơn ⇒ đúng 1 dòng (`D-109`) | **Integration** (PostgreSQL) | 2 luồng cùng `(TenantId, OrderId)` chạy Pha 1 song song (cả `TenantId = NULL`) | **1** dòng `ShpShipments`; luồng thua nhận `23505` ⇒ xử lý như A5 (trả vận đơn hiện có), 0 lời gọi hãng thêm | ⭐ **BR-022**, A5, §8.1 #3b |
| **TC-43** | Tạo lại khi vận đơn trước chưa rõ kết quả ⇒ NÉM (`D-109`) | Unit | `Shipment` mới nhất `SubmitFailed` vượt trần có attempt `TransientFailure`; provider giả **không** tra được theo `ClientOrderCode` | `BusinessException` mã `VanDonCuChuaRoKetQua`; **0** `Shipment` mới; **0** lời gọi tạo vận đơn tới provider | ⭐ **BR-022**, §3.2 #1 (ii), §12 |
| **TC-44** | Khoá guard kèm `generation` (`D-109`) | Unit | `IIdempotencyGuard` thật/giả; attempt 1 của generation 1 đã ghi kết quả, rồi attempt 1 của generation 2 | khoá `Create:{tenantId}:{orderId}:2:1` ≠ `…:1:1` ⇒ provider **được gọi**; guard **không** trả kết quả cũ | ⭐ **BR-022**, §7.3 |
| **TC-45** | Không ghép `attemptNo` vào mã gửi hãng (`D-109`) | Unit (grep) | quét `ShippingHub` + adapter tìm chỗ ghép `attemptNo`/`SubmitAttemptCount` vào `ClientOrderCode` | **0** match; test **đỏ** nếu có | **BR-022**, **BR-003** |
| **TC-46** | ShippingHub không tham chiếu `Address.Application`/`Address.Domain` và không tra theo `ShippingAddressId` (`D-110`) | Unit (quét) | quét `using`/`ProjectReference` của 4 project ShippingHub + đường tạo vận đơn/ước tính theo đơn | **0** tham chiếu `eCommerce.Address.Application`/`.Domain` (chỉ `Address.Application.Contracts`); **0** lời gọi `IAddressDirectory.TimTheoIdAsync` ở đường theo đơn; test **đỏ** nếu có | ⭐ **BR-SHP-001-020**, `D-110` (4) |
| **TC-47** | Cửa mã ném ⇒ `0014` + `InnerErrorCode`, không gọi hãng (`D-110`) | Unit | cửa mã giả ném lần lượt `Address:0001`, `Address:0005`, `0501`, `0504`, "> 1 `DaXacMinh`" | mỗi ca: `eCommerce:Shipping:0014` + `InnerErrorCode` đúng; **0** lời gọi hãng; không chọn mã nào | **BR-SHP-001-020**, E2, `D-110` |
| **TC-48** | Tra đúng phiên bản, một lời gọi mỗi đơn vị (`D-110`) | Unit | cửa mã giả ghi lại tham số; snapshot `SchemaVersion = 2`, mã xã có ở cả V1/V2 | mỗi cấp đúng **1** lời gọi với `SchemaVersion = 2` + đúng cấp/mã snapshot; **0** lời gọi với `SchemaVersion = 1`, kể cả khi lời gọi V2 ném | **BR-SHP-001-020**, `D-110` (2)(3) |
| **TC-49** | Index `ProviderTrackingCode` là partial — không chặn nhầm `Shipment` chưa có mã | **Integration** (PostgreSQL) | `TenantId = NULL`; 2 đơn **khác nhau**, cùng hãng, cùng ở `Draft` (`ProviderTrackingCode = NULL`); rồi 2 `Shipment` cùng `(ProviderCode, ProviderTrackingCode)` khác rỗng | hai `Draft` **đều chèn được** (0 `23505`); cặp cùng mã vận đơn khác rỗng ⇒ `23505` | ⭐ **BR-SHP-001-003**, **BR-SHP-001-004**, §5.2 index, review #2 |
| **TC-50** | Huỷ `Created` thành công không dính `23505` ở nhật ký (A4) | **Integration** (PostgreSQL) | `Shipment` `Created` (có dòng attempt `Created`, `AttemptNo = 1`); provider giả xác nhận huỷ | `Status = Cancelled`; đúng **1** dòng `Result = Cancelled` với `AttemptNo = 0`; dòng `Created` không đổi; **0** lỗi DB; `ShipmentCancelledEto` có trong outbox cùng transaction | ⭐ **BR-SHP-001-005**, **BR-SHP-001-006**, A4, §5.2 `AttemptNo` |
| **TC-51** | Không huỷ được `Submitting`, không tạo lại mù | Unit | `Shipment` ở `Submitting`; gọi `CancelShipmentAsync`; rồi gọi `CreateShipmentAsync` cùng đơn | huỷ: `BusinessException` `eCommerce:Shipping:0017`, trạng thái **không đổi**, **0** lời gọi huỷ tới provider; tạo: trả vận đơn hiện có (A5), **0** `Shipment` mới, **0** lời gọi tạo | ⭐ **BR-SHP-001-005**, **BR-SHP-001-003**, **BR-SHP-001-022**, §3.2 #6 |
| **TC-52** | COD gửi hãng = COD của đơn từ hợp đồng Sales | Unit | hợp đồng Sales **giả** trả `CodAmount = 250000` + dòng hàng X; `CreateShipmentDto` chỉ có `note` | `CarrierCreateShipmentRequest.CodAmountMinorUnits` ứng với `250000`, `Items` khớp X; `ShpShipments.CodAmount = 250000`; không có đường nhận COD từ client | ⭐ **BR-SHP-001-021**, **BR-SHP-001-014**, `D-108` (2) |

### 14.2 Fixture skeleton (xUnit)

```csharp
public class GhnAdapterTests
{
    // TC-03/TC-04 — fail-closed + địa chỉ: chạy được KHÔNG cần DB, KHÔNG cần hãng.
    [Fact]
    public async Task TC03_hang_chua_cau_hinh_thi_fail_closed()
    {
        var sut = TaoProvider(options: new GhnOptions { ApiToken = "", BaseUrl = "" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            sut.CreateShipmentAsync(TaoRequest(), CancellationToken.None));

        Assert.Equal("eCommerce:Shipping:0013", ex.Code);
    }

    // TC-07 — mã gửi hãng PHẢI ổn định qua retry cùng Shipment (D-109, XC-002 BR-XC-002-007).
    [Fact]
    public void TC07_client_order_code_on_dinh_qua_cac_lan_retry()
    {
        var orderId = Guid.Parse("…");
        var shipment = TaoShipment(orderId: orderId, generation: 1);
        var maBanDau = shipment.ClientOrderCode;

        for (var lan = 1; lan <= 3; lan++)
        {
            shipment.BeginSubmit();                            // SubmitAttemptCount = lan
            shipment.ApplyCarrierTransientFailure();           // tên gợi ý — SubmitFailed, còn dưới trần
        }

        Assert.Equal(maBanDau, shipment.ClientOrderCode);      // KHÔNG sinh mã mới, KHÔNG đổi Generation
        Assert.Equal(ShipmentCodes.TaoClientOrderCode(orderId, 1), maBanDau);
        Assert.EndsWith("-1", shipment.ClientOrderCode);       // đuôi là Generation, không phải attemptNo (3)
    }

    // TC-10 — guard trạng thái PHẢI NÉM LỖI (BR-005), KHÔNG được im lặng.
    [Fact]
    public void TC10_chuyen_trang_thai_sai_thi_nem_0017()
    {
        var shipment = TaoShipment(status: ShipmentStatus.Cancelled);

        var ex = Assert.Throws<BusinessException>(() =>
            shipment.BeginSubmit());

        Assert.Equal("eCommerce:Shipping:0017", ex.Code);
    }

    // TC-15 — lệch phí KHÔNG phải lỗi; số CHỐT là số của hãng (ADR-04).
    // API Money thật: sealed class, factory Money.Vnd/Money.Cua, .Amount, Equals/CompareTo (shared/eCommerce.Kernel/Money/Money.cs).
    [Fact]
    public void TC15_lech_phi_thi_ghi_nhan_khong_nem_loi()
    {
        var shipment = TaoShipment();
        shipment.RecordEstimate(Money.Vnd(37000m));

        shipment.ApplyCarrierSuccess(
            trackingCode: "GHN123456",
            actualFee: Money.Vnd(38500m),
            slaDueAtUtc: null);

        Assert.Equal(ShipmentStatus.Created, shipment.Status);
        Assert.Equal(38500m, shipment.ShippingFee!.Amount);
        Assert.NotEqual(shipment.EstimatedShippingFee, shipment.ShippingFee);   // Money.Equals — lệch ⇒ metric, KHÔNG sửa số hãng
    }

    // TC-19 — 🚨 CẤM tồn tại đường đổi COD (GHN bắt buộc OTP — implementation-plan.md:199, :426).
    [Fact]
    public void TC19_khong_co_duong_doi_cod()
    {
        var tenMethod = typeof(IShipmentAppService).GetMethods()
            .Select(m => m.Name)
            .Where(n => n.Contains("Cod", StringComparison.OrdinalIgnoreCase))
            .Where(n => n.Contains("Change", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Update", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Set", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(tenMethod);   // chỉ được ĐỌC cod_amount, không được ĐỔI
    }
}
```

> ⚠️ **Giới hạn môi trường (kế thừa `XC-006` §14.5 và `SHP-002` §14.2):** máy dev **không có PostgreSQL/Docker**
> ⇒ các test đánh dấu **Integration** (TC-05, TC-09, TC-11, TC-24, TC-26, TC-27, TC-31, TC-32, TC-42, TC-49, TC-50)
> **chưa chạy được** ở môi trường hiện tại. **Phải** chạy trên **Testcontainers PostgreSQL** trước khi `SHP-001`
> được coi là xong. Các test dùng `Money` (TC-15, TC-16) chạy được: `Money` đã có (`shared/eCommerce.Kernel/Money/Money.cs:23`).

### 14.3 Acceptance Criteria (Definition of Done)

- [ ] **52 test case pass**, phủ **hết 22 BR** và **12 NFR**
- [ ] ⭐ **TC-49…TC-52 pass** (doc review 19/11/2026): index `ProviderTrackingCode` partial không chặn nhầm · huỷ `Created` thành công không dính `23505` · `Submitting` không huỷ được · COD gửi hãng = COD của đơn
- [ ] ⭐ **TC-36…TC-39 pass** (`D-108`): DTO không nhận địa chỉ · địa chỉ từ snapshot đơn · đơn thiếu snapshot ⇒ NÉM trước Pha 1 · ShippingHub không phụ thuộc Sales ngoài `Application.Contracts`
- [ ] ⭐ **TC-07, TC-40…TC-45 pass** (`D-109`): mã ổn định qua retry · tạo lại ⇒ `Generation` + 1 · hàm thuần ≤ 50 ký tự · hai Pha 1 đồng thời ⇒ 1 dòng · `VanDonCuChuaRoKetQua` · khoá guard kèm `generation` · 0 chỗ ghép `attemptNo` vào mã
- [ ] ⭐ **TC-46…TC-48 pass** (`D-110`): 0 tham chiếu `Address.Application`/`.Domain` · cửa mã ném ⇒ `0014` · tra đúng phiên bản; phía `Address` do `addr-002` §14 phủ (F10)
- [ ] **Snapshot giao trên `Order` + hợp đồng đọc của Sales đã có** (`SAL-002`, `ADDR-004` F9) trước WP code `SHP-001` — trong lúc chờ, test dùng hợp đồng giả (`D-108` N2)
- [ ] ⭐ **TC-05 pass**: gọi **1.000 lần** cho cùng đơn ⇒ **đúng 1** vận đơn, **1** attempt `Created`
- [ ] ⭐ **TC-08 + TC-09 pass**: chứng minh **không** có lời gọi hãng nào nằm trong transaction — gồm UoW ngầm của app service đã tắt, chạy qua đường HTTP thật (ADR-02, BR-002)
- [ ] ⭐ **TC-19 pass**: chứng minh **0** đường đổi COD — vì **GHN bắt buộc OTP** (`docs/implementation-plan.md:199`, `:426`)
- [ ] ⭐ **TC-32 pass**: UNIQUE index **chặn ở tầng DB**, kể cả `TenantId = NULL` (trả lời *"bỏ lock đi, DB có chặn không?"* — `XC-003` D2)
- [ ] **TC-07 pass**: `ClientOrderCode` **ổn định** qua mọi lần retry (**0** `Guid.NewGuid()`)
- [ ] **TC-04 pass**: địa chỉ sai mô hình hãng bị chặn **trước khi** gọi hãng (ADR-06, chuẩn bị cho `SHP-004`)
- [ ] **TC-10/TC-12/TC-20 pass**: các guard **ném lỗi**, **không** im lặng
- [ ] **4 metric mới** thêm vào `eCommerceMetricNames.All` + `eCommerceMetrics`; **`XC-005` §5.1 đã cập nhật**; `MetricsTests` xanh
- [ ] **8 permission** `eCommerce.Shipping.*` đã khai trong `eCommercePermissionDefinitionProvider`; **TC-25** xanh
- [x] ✅ **Điều kiện tiên quyết đã xong**: outbox đã bật (`eCommerceEntityFrameworkCoreModule.cs:86-87`) · `Money` đã có (`Money.cs:23`) · `IIdempotencyGuard` đã có (`IdempotencyGuard.cs:27`)
- [ ] **`ResolveCarrierAddressCodeAsync` (`D-89`) và cửa mã `ResolveCarrierAddressCodeByCodeAsync` (`D-110`) đã có** (WP-15) trước khi `SHP-001` gọi; **TC-34**, **TC-46…TC-48** xanh
- [ ] **`SHP-002` §8.2 đã sửa về khoá không tiền tố** (F3; code đã thống nhất — `eCommerceHttpApiHostModule.cs:237`) — hai tài liệu `SHP-001`/`SHP-002` dùng **cùng** quy ước
- [ ] 🔴 **Tài liệu API GHN chính thức đã đọc** (base URL + payload tạo vận đơn + payload tính phí + cấu trúc địa chỉ) — **chưa có** ⇒ `SHP-001` **không** được coi là xong (§15.1 Q2)
- [ ] 🔴 **Đã xác minh hãng có chống trùng theo mã đơn phía ta** hay không (ADR-03) — **chưa có** ⇒ ghi rõ rủi ro nếu **không** có
- [ ] **Đã chốt mô hình địa chỉ** (2 cấp / 3 cấp / cả hai) và cập nhật **cả** `SHP-004` (§15.1 Q1; `docs/implementation-plan.md:290`)
- [ ] **Xung đột allowlist `XC-004` Q1 ↔ webhook `SHP-002`** đã được xử lý (F4) — vì nó quyết định `SHP-002` có chạy được không
- [ ] Tài liệu này cập nhật khớp code (nếu code lệch ⇒ **sửa tài liệu trước** — `docs/design-doc-guidelines.md:102`)

### 14.4 Definition of Ready

- [x] ✅ Đã kiểm hiện trạng: **không** có module `ShippingHub`, **không** có entity/bảng vận đơn, **không** có permission `eCommerce.Shipping.*` (§15.2 **F1**)
- [x] ✅ Đã kiểm (18/11/2026): `Money` và `IIdempotencyGuard` **đã** có trong code; outbox **đã** bật (§15.2 **F5**)
- [x] ✅ Đã xác định **khuôn adapter**: `IPaymentProvider` + `IChannelConnector` + `ChannelConnectorFactory` là **tiền lệ thật** trong repo (ADR-01)
- [x] ✅ Đã chốt **khoá chống trùng** (`D-109`, Q14): `ClientOrderCode = TaoClientOrderCode(orderId, generation)`; khoá guard `Create:{tenantId}:{orderId}:{generation}:{attemptNo}` (ADR-03, §7.3) — `SHP-002`/`SHP-004` còn phải sửa theo (F8, F9)
- [ ] 🔴 **Q2** — tài liệu API GHN **đã đọc chính thức** (base URL, payload, mã dịch vụ) — **CHẶN code**
- [ ] 🔴 **Q3** — hãng có **chống trùng theo `ClientOrderCode`** không, và có API **tra theo mã đơn phía ta** không — **CHẶN** thiết kế A1/A2
- [ ] 🔴 **Q1** — mô hình địa chỉ đã chốt (2 cấp/3 cấp/cả hai) theo hãng — **CHẶN**; **Q12** — đường gỡ mã hãng không duy nhất (`D-89` N2); Q11 đã chốt `D-108`, Q13 đã chốt `D-110` (cửa mã), Q14 đã chốt `D-109`
- [ ] **Q4** — `SHP-002` §8.2 sửa về khoá không tiền tố cho khớp `SHP-001` và code (F3)
- [ ] **Q7/Q8** — dải mã lỗi và cách gộp registry metric giữa `SHP-001` và `SHP-002` đã chốt
- [ ] Chốt `DefaultWeightGrams` và quy tắc làm tròn cân nặng lên bậc (Q5)
- [ ] 🔴 **Q16** — giá trị số `ShipmentStatus` dùng chung với `SHP-002` đã chốt — **CHẶN migration đầu** (`D-17`: đã lưu DB là không đổi được; §5.2 `Status`, §8.1 #5 CHECK, F11)
- [ ] 🔴 **Q20** — `ShpShipmentCreateAttempts.TenantId` nullable hay NOT NULL đã chốt — **CHẶN migration đầu** (§5.2; `TC-32`/`TC-49`/`TC-50` chạy với `TenantId = NULL`)
- [ ] **Q15** — đường đi tiếp của `Draft` mồ côi đã chốt (§3.2 #5, A5)
- [ ] **Q17** — COD lẻ khi đổi `Money` → số nguyên VND: NÉM hay làm tròn (BR-008, ADR-05, TC-17)
- [ ] **Q18** — `Order.CodAmount = null` ⇒ `0` hay NÉM (BR-021, §5.2 `CodAmount`)
- [ ] **Q19** — đường gỡ `Submitting` kẹt khi hãng không có API tra theo `ClientOrderCode` (§3.2 #6, A2, §12)

---

## 15. Câu hỏi mở, giả định & lịch sử thay đổi

### 15.1 Câu hỏi mở

| # | Câu hỏi | Cần ai | Trạng thái | Ảnh hưởng |
|---|---|---|---|---|
| **Q1** | **Mô hình địa chỉ** dùng để gửi hãng: 2 cấp (mới) · 3 cấp (cũ) · hay **cả hai**? (Module `Address` **đã có** — `modules/Address/`; mã hãng tra qua `D-89`; riêng mô hình theo từng hãng vẫn là việc của NCC.) | Kiến trúc trưởng + **người đã liên hệ GHTK** | 🔴 **`Open`** | 🔴 **CHẶN.** GHN đã có cờ `is_new_to_address`/`is_new_from_address`/`is_new_return_address` và `to_district_name` **required khi cờ = false** (`docs/business-modules-proposal.md:513`–`514`); GHTK **chưa xác minh** (`:546`) và quyết định `:658` là **hỗ trợ CẢ HAI**. Không chốt ⇒ `CarrierAddress` (ADR-06) **không** thiết kế được |
| **Q2** | 🔴 **Tài liệu API GHN chính thức**: base URL, payload **tính phí**, payload **tạo vận đơn**, mã dịch vụ, cấu trúc địa chỉ? | **Người đã liên hệ GHN** (việc gửi email **đang treo** — `docs/implementation-plan.md:321`; đăng ký sandbox **đang treo** — `:322`) | 🔴 **`Open`** | 🔴 **CHẶN code.** Không có ⇒ **không** viết được `GetFeeAsync`/`CreateShipmentAsync`, **không** điền được `ShippingHub:Providers:ghn:*`. 💡 **Nguồn có bằng chứng:** GHN publish **`llms.txt` + `llms-full.txt`** (`docs/business-modules-proposal.md:559`) và `docs/implementation-plan.md:338` chốt chọn GHN **vì** có chúng |
| **Q3** | Hãng có **chống trùng theo `ClientOrderCode`** không? Có API **tra vận đơn theo mã đơn phía ta** không? | **Người đã liên hệ GHN** | 🔴 **`Open`** | 🔴 **CHẶN thiết kế A1/A2.** (a) Nếu **không** chống trùng ⇒ ADR-03 mất **một** hàng rào ⇒ hàng rào còn lại **chỉ** là unique index của ta (`NULLS NOT DISTINCT`, có hiệu lực cả khi `TenantId = NULL` — §5.2). (b) Nếu **không** có API tra ⇒ **A2** phải làm **thủ công** bằng người. Ghi nhận (`D-109`): tài liệu GHN nói gửi lại `client_order_code` đã dùng thì trả `order_code` cũ (developer.ghn.vn, tra 18/11/2026) — **chưa** kiểm chứng bằng sandbox, hành vi với mã của đơn **đã huỷ** **chưa xác minh** |
| **Q4** | **Tiền tố khoá lock (F3):** `ShippingHubLockKeys` sinh khoá **không** có `ecommerce:` (khớp code — §8.2). Chỉ còn `SHP-002` §8.2 dùng dạng **có** `ecommerce:` | **Kiến trúc trưởng** (DEC) | 🟡 **`Open`** — đề nghị DEC đóng: phần code đã thống nhất | Nếu `SHP-002` không sửa ⇒ khoá thật thành `ecommerce:ecommerce:…` (**tiền tố đôi**) ⇒ khoá không bảo vệ gì. Việc sửa ở `SHP-002` (F3) |
| **Q5** | **Nguồn và cách tính cân nặng:** (a) `Order.WeightGram` (`Order.cs:130`, Nhanh mặc định 500 ở tầng app) làm nguồn trước, hay (b) `Σ Product.WeightGram × Quantity` (`Product.cs:78`, `int?`; dòng cũ `ProductVariant.WeightInGrams` — `Product.cs:512`)? Nếu (b): ai cung cấp cân nặng SKU cho ShippingHub (hợp đồng Sales trả sẵn, hay interface mới ở `Catalog.Application.Contracts` — chưa có, §6.1); `null` ⇒ dùng `DefaultWeightGrams`? Làm tròn `Σ` (`decimal`) lên bậc thế nào? | Dev + Vận hành | 🟡 **`Open`** | Ảnh hưởng **số tiền** khách trả. Không chốt ⇒ phí ước tính **sai** ⇒ `shipping_fee_discrepancy_total` **luôn** tăng |
| **Q6** | **Timeout / retry / trần thử** có đúng đề xuất (10 s · 3 lần · backoff 1→5→25 s · trần thử 5) không? | Kiến trúc trưởng | 🟡 **`Open`** | Quá ngắn ⇒ `SubmitFailed` giả; quá dài ⇒ nhân viên kho chờ lâu (NFR-002) |
| **Q7** | **Dải mã lỗi** `eCommerce:Shipping:*`: `SHP-001` dùng `0013`–`0018` có chấp nhận không? ⚠️ Đã va chạm thật: `SHP-003` dùng `0016`–`0024` (`shp-003-…md:566-574`), `SHP-004` dùng `0012`–`0021` (`shp-004-…md:538-547`) với nghĩa khác; mã mới `DonChuaCoDiaChiGiao` (`D-108`) cũng chưa có số. Và `IShipmentAppService` **dùng chung** một class cho `SHP-001` + `SHP-002` hay tách hai? | Kiến trúc trưởng | 🟠 **`Open`** | Hai tài liệu cùng khai **một** `ShippingHubErrorCodes` và **một** `IShipmentAppService` ⇒ phải chốt **trước** khi code, nếu không sẽ có **hai nguồn sự thật** (đúng loại lỗi `XC-003` F1/F2) |
| **Q8** | **Registry metric**: `SHP-001` thêm **4** và `SHP-002` thêm **6** ⇒ tổng **22**. Gộp trong **một** PR hay hai PR? | Kiến trúc trưởng | 🟠 **`Open`** | `MetricsTests` **không còn** đếm cứng số 12 (`test/eCommerce.Tests/Observability/MetricsTests.cs:64`) ⇒ hai PR không gây đỏ vì con số; chỉ còn rủi ro tên đăng ký/instrument lệch nhau (`:39`). Căn cứ ban đầu của câu hỏi đã yếu đi — đề nghị DEC đóng |
| **Q9** | Có cần **cache** kết quả `GetFeeAsync` không? TTL bao lâu? | Kiến trúc trưởng | 🟡 **`Open`** | Không cache ⇒ tốn **quota** của hãng (`docs/implementation-plan.md:322` còn treo việc đăng ký sandbox); cache quá lâu ⇒ phí **sai** |
| **Q10** | **Ai** được bấm "Tạo vận đơn" — chỉ kho, hay cả CSKH? Có cần **duyệt** khi COD vượt ngưỡng? | BA + Vận hành | 🟡 **`Open`** | Ảnh hưởng §9 (permission) và §10 (UI) |
| **Q11** | **Địa chỉ nhận** của vận đơn lấy từ đâu, vì `Order` không có trường địa chỉ giao (§1 tiền điều kiện)? (a) `CreateShipmentAsync` nhận `AddressId`/`OurAdministrativeUnitId` từ sổ địa chỉ `ADDR-004`/Customers · (b) Sales lưu snapshot địa chỉ giao trên đơn rồi `SHP-001` đọc qua contract Sales | BA + Kiến trúc trưởng | ✅ **Đã chốt — `D-108`** (TẠM CHỐT, chọn (b)) | Địa chỉ nhận = snapshot giao hàng trên đơn (Sales sở hữu) đọc qua hợp đồng `eCommerce.Sales.Application.Contracts`; `CreateShipmentDto` không nhận địa chỉ/người nhận; đơn thiếu snapshot ⇒ NÉM `DonChuaCoDiaChiGiao` trước Pha 1. Đã sửa §1, §5.2, §6.1–§6.3, §12, §14 (BR-021, TC-36…TC-39). Nợ còn lại: F7 (Q13 đã chốt `D-110`) |
| **Q12** | **Đường gỡ** khi `ResolveCarrierAddressCodeAsync` NÉM vì **> 1** ánh xạ `DaXacMinh` cùng đích (`D-89` N2: hãng còn dùng mã phường cũ — **chưa xác minh** với từng hãng): (a) vận đơn bị chặn tới khi người vận hành bỏ xác minh bớt một ánh xạ · (b) mã ưu tiên theo hãng · (c) SHP gửi kèm ĐVHC cũ qua `ADDR-003` | BA + người đã liên hệ GHN | 🟡 **`Open`** (khuyến nghị (a) cho tới khi có bằng chứng từ hãng) | `D-89` giao chủ = BA khi soạn `SHP-001`, xem lại ở WP-15; không chặn các mục khác |
| **Q13** | **Đường snapshot (mã) → mã hãng** (`D-108` N1): snapshot trên đơn mang **mã** (`SchemaVersion` + `TinhCode`/`HuyenCode`/`XaCode`) còn `ResolveCarrierAddressCodeAsync` (`D-89`) nhận Guid đơn vị. Phương án đã chọn: `Address` thêm cửa nhận khoá tự nhiên `(SchemaVersion, cấp, mã)` | BA + Kiến trúc trưởng | ✅ **`Closed (D-110)`** (TẠM CHỐT, DEC) | `ResolveCarrierAddressCodeByCodeAsync` ở `Address.Application.Contracts`; không thêm Guid vào snapshot; SHP không tra theo `ShippingAddressId`. Đã sửa §0, §1, §6.1, §6.3, §6.5, §12, §14 (BR-020, TC-34, TC-46…TC-48). Việc phía `Address`: F10 |
| **Q14** | **Mâu thuẫn khoá chống trùng (§7.3 ↔ ADR-03/BR-003):** khoá `Create:{tenantId}:{orderId}:{attemptNo}` (`SHP-002` §7.3) đổi `ClientOrderCode` theo `attemptNo`, trái BR-003 "ổn định qua retry". Phương án đã chọn: mã = hàm thuần `(OrderId, Generation)` | Kiến trúc trưởng (DEC) | ✅ **`Closed (D-109)`** (TẠM CHỐT, DEC) | `"{orderId:N}-{generation}"`; cột `Generation` + UNIQUE `(TenantId, OrderId, Generation)`; `attemptNo` ra khỏi mã, khoá guard `Create:{tenantId}:{orderId}:{generation}:{attemptNo}`; mã mới `VanDonCuChuaRoKetQua`. Đã sửa ADR-03, BR-003/BR-022, §3.2 #1, §4.2, §5.2, §6.2, §6.3, §7.3, §8.1, §12, §14 (TC-07, TC-40…TC-45). Việc ở `SHP-002`/`SHP-004`: F8, F9. Q3 (hãng thật sự chống trùng) vẫn `Open` |
| **Q15** | **`Draft` mồ côi** (chết sau Pha 1 hoặc E6): (a) A5 gặp `Draft` thì đi tiếp Pha 2 dưới khoá · (b) job quét cả `Draft` quá N phút | Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (a): không thêm job, cùng đường với bấm lại) | Hiện job chỉ quét `Submitting`/`SubmitFailed` (§3.2 #5) nên `Draft` không có đường đi tiếp ngoài CSKH huỷ tay (§4.2 P1). Chặn: §3.2 #5, A5 |
| **Q16** | **Giá trị số `ShipmentStatus`** dùng chung với `SHP-002` (`SHP-002` §5.2 ghi mặc định `Created = 0`; tài liệu này ghi mặc định `Draft`): (a) giữ số hiện có của `SHP-002`, `SHP-001` thêm `Draft`/`Submitting`/`SubmitFailed` ở **cuối** (`D-17`) · (b) chốt lại cả hai | Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (a)) | `D-17`: đã lưu DB là không đổi được. Chặn: §5.2 `Status`, §8.1 #5 CHECK, F11 |
| **Q17** | **Đổi `Money` → số nguyên VND** (`Money` không có `ToMinorUnits()`, không tự làm tròn): COD lẻ (có phần thập phân) ⇒ (a) **NÉM** (fail-closed) · (b) làm tròn theo `IRoundingPolicy` (`RoundHalfAwayFromZeroPolicy` — `RoundingPolicy.cs:25`, như tiền lệ VnPay `:113`); và hàm đặt ở Kernel hay ShippingHub | Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (a) cho COD — tiền thu hộ thật; hàm ở ShippingHub) | BR-008, ADR-05, TC-17 |
| **Q18** | **`Order.CodAmount` là `decimal?`** (`Order.cs:121`): đơn chưa chốt COD (`null`) ⇒ (a) coi `0` (không thu hộ) · (b) NÉM trước Pha 1 (mã mới) | BA `SAL-002` + Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (b): `0` và "chưa chốt" khác nghĩa — chuyển khoản trước có COD = 0, `Order.cs:119`) | BR-021, §5.2 `CodAmount` (✖ NOT NULL) |
| **Q19** | **Gỡ `Submitting` kẹt** khi hãng không có API tra theo `ClientOrderCode` (Q3 — NCC): (a) chỉ job/A2, ngoài ra làm tay ngoài hệ thống · (b) thêm lệnh "xác nhận thủ công kết quả" (người vận hành nhập mã vận đơn tra từ cổng hãng, hoặc xác nhận không có) ghi vào nhật ký attempt | Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (b) khi Q3 = không có API; chưa thiết kế ở đây) | §3.2 #6 (không huỷ được `Submitting`), A2, §12 |
| **Q20** | **`ShpShipmentCreateAttempts.TenantId` NOT NULL** (§5.2) trong khi `ShpShipments.TenantId` nullable và 1 tenant ⇒ `NULL`: attempt của `Shipment` có `TenantId = NULL` không chèn được. (a) cho attempt `TenantId` nullable (khớp các bảng khác) · (b) giữ NOT NULL và buộc tenant mặc định | Kiến trúc trưởng (DEC) | 🟡 **`Open`** (khuyến nghị (a)) | `TC-32`/`TC-49`/`TC-50` chạy với `TenantId = NULL`; index attempts §5.2 |

### 15.2 🚨 Phát hiện khi viết tài liệu này

| # | Vấn đề | Bằng chứng | Mức độ | Xử lý |
|---|---|---|---|---|
| **F1** | **Module `ShippingHub` CHƯA TỒN TẠI** trong code: không project, không entity, không `DbSet`, không permission `eCommerce.Shipping.*`. `grep` trên mọi `*.cs` cho `ShippingHub\|Shipment\|Carrier\|CodRecord` = **0** kết quả trong `src/`, `modules/`, `test/` | `eCommerce.slnx` (không có ShippingHub, đối chiếu 18/11/2026) · `modules/Sales/eCommerce.Sales.Domain/Order.cs:162` (`ShippingTotal`) · `modules/Sales/eCommerce.Sales.EntityFrameworkCore/SalesDbContextModelCreatingExtensions.cs:36` (`HasPrecision(18,4)`) · `src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs` (không có `eCommerce.Shipping.*`) | 🟡 **Thấp** (đúng kế hoạch — đây là tài liệu **GĐ2** viết trước khi code) | ✅ **Không phải lỗi.** Ghi rõ **toàn bộ** §5 là kế hoạch GĐ2 ⇒ 10 việc "bắt buộc sửa app" ở §11 |
| **F2** | 🚨 **`ShpShipments` được định nghĩa ở HAI tài liệu** (`SHP-001` tài liệu này **và** `SHP-002` §5.2) với **cột KHÁC NHAU**: `SHP-002` §5.2 **không** có `OrderNumber`, `ClientOrderCode` (**có** — `:319`), `ShippingFeeCurrency`, `EstimatedShippingFee`, `SubmitAttemptCount`, `TotalWeightGrams`. Tài liệu này cần **4 cột** mà `SHP-002` **chưa** liệt kê | `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:309`–`332` vs §5.2 tài liệu này | 🟠 **Trung bình** (hai nguồn sự thật cho **cùng một bảng** — đúng loại lỗi `XC-003` F1) | ⬜ **Chưa sửa** — **quyết định của tài liệu này (ADR-09):** `SHP-001` **là chủ sở hữu** bảng và **4 cột mới** phải được **bổ sung vào `SHP-002` §5.2** trong **cùng** PR sửa tài liệu, **trước** khi code |
| **F3** | **Chỉ còn `SHP-002` §8.2 dùng khoá có `ecommerce:`.** Code đã thống nhất "không tiền tố" (đối chiếu 19/11/2026): host thêm `KeyPrefix = "ecommerce:"`, mọi module sinh khoá không tiền tố (§8.2) — `SHP-002` §8.2 dùng `ecommerce:shipping:…` ⇒ tiền tố đôi. Chưa chạy `redis-cli MONITOR` (🔶) | `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:229,237` · `modules/Tax/eCommerce.Tax.Domain/TaxErrorCodes.cs:46` · `src/eCommerce.Domain.Shared/eCommerceConsts.cs:817` · `modules/ChannelHub/eCommerce.ChannelHub.Domain/ChannelHubConsts.cs:19` · `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:712-724` | 🟡 **Thấp–Trung bình** (hạ từ 🔴 — phần code không còn xung đột; còn lỗi trong một tài liệu chưa code) | ⬜ **Chưa sửa** — việc ở `SHP-002` §8.2, ngoài vùng ghi của lượt này; giao BA sửa `shp-002`. Q4 đề nghị DEC đóng |
| **F4** 🆕 | 🚨 **Xung đột allowlist:** `XC-004` Q1 đã **đóng** danh sách `[AllowAnonymous]` **chỉ** gồm `/connect/*`, `/api/abp/application-configuration`, `/health-status`, Swagger dev — **KHÔNG** có webhook hãng vận chuyển. Nhưng `SHP-002` §6.2 **bắt buộc** endpoint webhook phải `[AllowAnonymous]` và *"phải khai vào allowlist của test `TC-03`"* | `docs/features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md:505` vs `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:522` | 🟠 **Trung bình–Cao** (nếu không sửa ⇒ test `TC-03` của `XC-004` **và** webhook của `SHP-002` **không thể cùng xanh**) | ⬜ **Chưa sửa** — cần sửa **`XC-004` Q1** (thêm webhook vận chuyển vào allowlist **kèm lý do**) hoặc ghi rõ ngoại lệ. ⚠️ `SHP-001` **không** bị ảnh hưởng trực tiếp (**0** endpoint vô danh) nhưng **phải** biết vì `SHP-002` là tài liệu kế tiếp của cùng module |
| **F5** | ✅ **Ba phụ thuộc nền ĐÃ có trong code (đối chiếu 18/11/2026):** (a) `Money`/`MoneyAllocator` (`XC-001`); (b) `IIdempotencyGuard` (`XC-002`); (c) outbox/inbox đã bật | (a) `shared/eCommerce.Kernel/Money/Money.cs:23`, `MoneyAllocator.cs:21`, `eCommerce.slnx:87` · (b) `modules/Messaging/eCommerce.Messaging.Domain/Idempotency/IdempotencyGuard.cs:27` · (c) `src/eCommerce.EntityFrameworkCore/eCommerceEntityFrameworkCoreModule.cs:86-87` | 🟡 **Thấp** (không còn chặn) | ✅ `IIdempotencyGuard` và outbox dùng trực tiếp; 🔶 `Money` có nhưng không có `ToMinorUnits()`/`Money.Of` và không tự làm tròn (Q17). Phụ thuộc **còn thiếu** duy nhất: `ResolveCarrierAddressCodeAsync` (`D-89`, ⬜ WP-15) — §6.5 |
| **F6** 🆕 | **`ChannelTokenManager` ghi log `Warning` + bỏ qua khi không giành được khoá, cho **mọi** ngữ cảnh** — không phân biệt job định kỳ với đường HTTP | `modules/ChannelHub/eCommerce.ChannelHub.Application/Sync/ChannelTokenManager.cs:74`–`83` (`handle is null` ⇒ `LogWarning` + `return (…, false)`) | 🟡 **Thấp–Trung bình** | ⬜ **Ngoài phạm vi `SHP-001`** (thuộc `ChannelHub`). Ghi nhận: `XC-003` D10 nói **mặc định phải NÉM LỖI** và `SkipIfBusy` **chỉ** cho job định kỳ. `SHP-001` **không** lặp lại mẫu này: đường HTTP ⇒ **NÉM** `eCommerce:Lock:0001`; job ⇒ `SkipIfBusy` + log + metric (**TC-28**) |
| **F7** 🆕 | **Nguồn địa chỉ nhận nằm ngoài `SHP-001` và đang thiếu ở hai nơi** (`D-108`): (a) `Order` chưa có snapshot giao hàng + hợp đồng đọc ở Sales; (b) `ShpShipments` (`SHP-002` §5.2) chưa có cột bản sao snapshot đã gửi hãng | (a) `ADDR-004` F9/Q6 (`docs/features/address/addr-004-so-dia-chi-khach.md:981`, `:999`) + `SAL-002`; grep `Address\|DiaChi` trong `modules/Sales/**/*.cs` = 0 (18/11/2026) · (b) `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:309-332` | 🟠 **Trung bình** (chặn code `SHP-001`: không có địa chỉ ⇒ mọi đơn NÉM `DonChuaCoDiaChiGiao`) | ⬜ **Chưa sửa** — (a) thứ tự: snapshot trên `Order` + hợp đồng đọc phải **trước** WP code `SHP-001` (`D-108` N2; đề xuất `§WP-15+`), trong lúc chờ test dùng hợp đồng giả · (b) **sửa `SHP-002` §5.2 trước** (ADR-09), cùng đợt F2 |
| **F8** 🆕 | **`SHP-002` phải sửa theo `D-109`** (ADR-09 — sửa `SHP-002` trước khi code): (a) §5.2 `ShpShipments` thêm cột `Generation` (`int` NOT NULL, CHECK `>= 1`) + UNIQUE `(TenantId, OrderId, Generation)` `NULLS NOT DISTINCT` (cùng đợt F2/F7); (b) §7.3 khoá `Create:{tenantId}:{orderId}:{attemptNo}` thay bằng trỏ về `SHP-001` §7.3 (`Create:{tenantId}:{orderId}:{generation}:{attemptNo}`; một nguồn) | `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:683` (khoá hiện tại), `:309`–`:332` (§5.2) | 🟠 **Trung bình** (khoá cũ thiếu `generation` ⇒ guard trả kết quả cũ im lặng ở generation 2) | ⬜ **Chưa sửa** — ngoài vùng ghi của lượt này; giao BA sửa `shp-002` |
| **F9** 🆕 | **`SHP-004` phải ghi F-item dùng hàm `D-109`:** `ClientOrderCode` của GHTK = `TaoClientOrderCode(orderId, generation)` (không dùng `"{tenantCode}-{orderNumber}"` — có thể > 50 ký tự, hệ chưa có `tenantCode`); UNIQUE `ShpShipmentSubmissions` có `AttemptNo` đang **vô hiệu hoá** chống trùng theo mã | `docs/features/shipping/shp-004-adapter-ghtk.md:286`, `:342`, `:621`, `:635` (số dòng theo `D-109`, chưa đối chiếu lại) | 🟡 **Thấp–Trung bình** (`SHP-004` chưa tới lượt rà) | ⬜ **Chưa sửa** — sửa khi rà `SHP-004`; ngoài vùng ghi của lượt này |
| **F10** 🆕 | **`ADDR-002` phải ghi cửa mã `D-110`:** §0 (phạm vi), bảng §6.1 (hàng ⬜ `ResolveCarrierAddressCodeByCodeAsync`, WP-15, kèm lựa chọn kiểu `Level` không kéo `Address.Domain`), §14 (test phía `Address` theo `D-110` ⑥ (a)–(f): chặn tra chéo phiên bản, `Address:0001`, `0504`, đối xứng hai cửa, tenant đọc danh mục host, `DiaChiSnapshotDto` 0 thuộc tính `Guid` ngoài `AddressId`) | `docs/features/address/addr-002-dong-bo-ma-dia-chi-theo-hang-van-chuyen.md` (§0, §6.1, §14) | 🟡 **Thấp–Trung bình** (WP-15 chưa làm; `SHP-001` TC-46…TC-48 chỉ phủ phía ShippingHub) | ⬜ **Chưa sửa** — ngoài vùng ghi của lượt này; giao BA sửa `addr-002` |
| **F11** 🆕 | **`SHP-002` phải sửa theo doc review 19/11/2026:** (a) §5.2 index `(TenantId, ProviderCode, ProviderTrackingCode)` đổi thành **partial** `NULLS NOT DISTINCT` `WHERE "ProviderTrackingCode" IS NOT NULL` — định nghĩa hiện tại (không partial) sẽ chặn nhầm mọi `Shipment` thứ hai chưa có mã (§5.2); (b) §5.2 `Status` ghi mặc định `Created = 0` và CHECK `Status NOT IN (3, 9)` — phải thống nhất giá trị số `ShipmentStatus` với `SHP-001` (Q16) trước khi đánh số | `docs/features/shipping/shp-002-webhook-trang-thai-van-don.md:320`, `:395`, `:400` | 🟠 **Trung bình** (`D-17`: đánh số xong không đổi được; index sai ⇒ `23505` oan) | ⬜ **Chưa sửa** — ngoài vùng ghi của lượt này; giao BA sửa `shp-002` cùng đợt F2/F7/F8 |

### 15.3 Giả định

| # | Giả định | Nếu SAI thì sao |
|---|---|---|
| 1 | **GHN là hãng làm trước** (REST, có sandbox, publish `llms.txt`) | Nếu làm GHTK trước ⇒ 🔴 **phải** xác minh **địa chỉ 2 cấp** trước (`docs/implementation-plan.md:290`, `:338`) ⇒ `CarrierAddress` (ADR-06) **phải** sửa trước khi code |
| 2 | **Hãng chống trùng theo `ClientOrderCode`** (mã đơn phía ta) | Nếu **không** ⇒ hàng rào **duy nhất** là UNIQUE index của ta (`NULLS NOT DISTINCT` — có hiệu lực cả khi `TenantId = NULL`, §5.2) cùng khoá phân tán ⇒ 🔴 rủi ro tạo 2 vận đơn thật còn lại nằm ở ca timeout/tiến trình chết giữa lúc gọi hãng rồi gửi lại cùng mã ⇒ **phải** có đối soát chủ động (A2, Q19) |
| 3 | **Hãng có API tra vận đơn theo `ClientOrderCode`** | Nếu **không** ⇒ **A2** (xử lý `Submitting` mồ côi) **không** tự động được ⇒ **phải** có người vận hành tra cứu ⇒ cần màn hình + SLA thủ công |
| 4 | **Gọi hãng là "at-least-once" an toàn** khi idempotent theo khoá | Nếu hãng trả **429/quota** ⇒ luồng tạo vận đơn bị **chặn** giữa đường ⇒ **phải** có backoff tôn trọng quota + cảnh báo (liên quan Q9) |
| 5 | **Số hãng vận chuyển nhỏ (2–5)** ⇒ `provider` là **nhãn hữu hạn** | Nếu > 20 hãng ⇒ vẫn **ổn** (nhãn hữu hạn theo số hãng, **không** theo số vận đơn) — không cần sửa (`XC-005` NFR-XC-005-003) |
| 6 | **Một vận đơn = một lần gửi hàng**, không gộp nhiều đơn vào một vận đơn | Nếu nghiệp vụ cần **gộp đơn** (consolidation) ⇒ `ClientOrderCode` phải sinh theo **lô**, và **bảng `ShpShipmentOrders`** (nhiều-nhiều) là **bắt buộc** ⇒ §5.2 phải thiết kế lại |
| 7 | **`Product.WeightGram` là đủ** để tính phí | Nếu phải dùng **thể tích quy đổi** (volumetric weight) ⇒ cần thêm `Length/Width/Height` trên sản phẩm (`modules/Catalog/eCommerce.Catalog.Domain/Product.cs:78` hiện **chỉ** có `WeightGram`) ⇒ **phải** sửa `CAT-001` trước |
| 8 | **`Order.ShippingTotal` do `SAL-002` quản lý**, `SHP-001` **chỉ** phát ETO | Nếu `SHP-001` **tự** ghi `Order.ShippingTotal` ⇒ **vi phạm** quy ước *"module sở hữu entity của mình"* + `Order.SetTotals` là method của aggregate `Order` (`modules/Sales/eCommerce.Sales.Domain/Order.cs:252`) ⇒ **module gọi app BỊ CẤM** |
| 9 | **`EnableLiveCalls = false` ở dev** đủ để phát triển toàn bộ luồng | Nếu dev **bắt buộc** gọi GHN thật ⇒ phải có **tài khoản sandbox** (việc đăng ký **đang treo** — `docs/implementation-plan.md:322`) và **phải** cẩn thận không tạo vận đơn thật ngoài ý muốn |

### 15.4 Lịch sử thay đổi

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v0.1 | 14/10/2026 | *(điền)* | Bản nháp đầu — theo **đúng cấu trúc 16 mục** của `XC-003` (§0, §0.1, §1..§15). **Chốt nguyên tắc nền:** 🚨 **gọi hãng NGOÀI transaction (3 pha)** (`ADR-02`) + **khoá chống trùng ỔN ĐỊNH** (`ADR-03`) + **GHN đổi COD cần OTP ⇒ cấm tự động hoá** (`ADR-07`/`BR-014`) + **địa chỉ trung tính** để `SHP-004` cắm vào (`ADR-06`). Lập **Data Dictionary 5 bảng** (**chưa có trong code** — **F1**) + bảng index gồm **UNIQUE `(TenantId, ProviderCode, ClientOrderCode)`** làm hàng rào DB cho `BR-003` và **UNIQUE có filter `Result = Created`** làm hàng rào thứ hai. Phát hiện **F2** (`ShpShipments` định nghĩa ở 2 tài liệu với cột khác nhau), **F3** (🚨 hai quy ước tiền tố khoá lock ⇒ **tiền tố đôi** — suy từ code, **chưa** kiểm chứng bằng `MONITOR`), **F4** (xung đột allowlist `XC-004` Q1 ↔ webhook `SHP-002`), **F5** (3 phụ thuộc nền `Money`/`IIdempotencyGuard`/outbox **chưa có**), **F6** (`ChannelTokenManager` bỏ qua khi bận cho **mọi** ngữ cảnh, khác `XC-003` D10). **12 ADR · 19 BR · 12 NFR · 32 test case · 3 sơ đồ Mermaid** |
| v0.2 | 18/11/2026 | BA (WP-14 ①, `D-71`) | Đối chiếu mô hình Nhanh: sửa thân theo `D-07`/`D-08`/`D-17`/`D-31`/`D-70`/`D-89` + hiện trạng code (Money, idempotency, outbox đã có; `Order` chuyển sang `modules/Sales`); thêm `BR-SHP-001-020`, `TC-33`…`TC-35`, `Q11`, `Q12`. **20 BR · 35 test case** |
| v0.3 | 18/11/2026 | BA (WP-14 ①, `D-108`) | Chốt Q11 theo `D-108`: địa chỉ nhận = snapshot giao trên đơn qua hợp đồng Sales; sửa §1 (tiền điều kiện, Basic Flow 1/3, E10, BR-021), §5.2 (nhóm cột snapshot), §6.1–§6.3, §12, §14 (TC-36…TC-39); thêm Q13 (`D-108` N1), Q14 (mâu thuẫn khoá chống trùng §7.3 ↔ BR-003 — chưa có `D-xx`), F7; Q7 bổ sung va chạm dải mã lỗi. **21 BR · 39 test case** |
| v0.4 | 19/11/2026 | BA (WP-14 ①, `D-109`/`D-110`) | Chốt Q14 theo `D-109` (`ClientOrderCode` = hàm thuần `(OrderId, Generation)`, cột `Generation`, khoá guard kèm `generation`, mã mới `VanDonCuChuaRoKetQua`) và Q13 theo `D-110` (cửa mã `ResolveCarrierAddressCodeByCodeAsync`); sửa ADR-03, §1 (BR-003, BR-020, BR-022 mới), §3.2 #1, §4.2, §5.2, §6.1–§6.3, §6.5, §7.3, §8.1, §12, §14 (TC-07 viết lại, TC-40…TC-48), §15; thêm F8–F10 (việc ở `SHP-002`, `SHP-004`, `ADDR-002`). **22 BR · 48 test case** |
| v0.5 | 19/11/2026 | BA (WP-14 ①, sửa theo `docs/reviews/SHP-001-docreview.md`) | 5 P1: UoW ngầm của ABP phải tắt (`[UnitOfWork(isTransactional: false)]` + `Begin(requiresNew: true)`, BR-002, §4.2, §12, TC-08/09) · index `ProviderTrackingCode` thành partial, mọi unique index có `NULLS NOT DISTINCT` (§5.2, TC-32/49) · `CreateShipmentDto` bỏ `codAmount`/`currency`/`items`, COD = `Order.CodAmount` (BR-021, §6.1/6.2, TC-52) · bỏ cạnh `Submitting → Cancelled` (§3.1, §3.2 #6, A4, TC-51) · `AttemptNo = 0` cho dòng huỷ (§5.2, TC-50). 11 P2: tách `EstimateOrderFeeAsync`/`EstimateCartFeeAsync`; `Money` 🔶 + fixture §14.2; `TC-31` → `Messaging:0001`; trường vận chuyển trên `Order` (§5.1, §7.1); F3 hạ mức; cấu hình khoá tách `LockWaitSeconds`/`ShipmentCreateLockTtlSeconds`; cập nhật `file:dòng`; thêm Q15–Q20, F11. **22 BR · 52 test case** |
| v0.5.1 | 19/11/2026 | BA (WP-14 ①, docreview vòng 2) | #28 NFR-004 đổi ngưỡng/cách đo khớp §1, §4.2 (TC-08 + TC-09) · #29 §14.4 DoR thêm Q15…Q20 (Q16, Q20 chặn migration đầu) · #34 §6.2 trỏ cửa mã `D-110` thay vì `D-89`. Không đổi số BR/test case |

---

## Đối chiếu mô hình Nhanh (19/11/2026)

| D-xx | Tài liệu cũ nói (§mục) | Đã sửa thành (§mục) | Code thật (file:dòng) / kế hoạch |
|---|---|---|---|
| D-07 | §1/§5.2/§15.3: cân nặng từ `ProductVariant.WeightInGrams` | §1, §5.1, §5.2, §15.3: `Product.WeightGram`; biến thể chỉ là đường cũ | `modules/Catalog/eCommerce.Catalog.Domain/Product.cs:78`, `:512` |
| D-08 | Header: `Packing → AwaitingPickup` không nói số trục | Header: 4 trục, `FulfillmentStatus` 18 giá trị; §7.1: `SAL-003` tự chuyển, SHP chỉ phát ETO | `src/eCommerce.Domain.Shared/Enums.cs:151-210`; `Order.cs:370` |
| D-17 | §5.2: enum `Status`/`Result` không nêu luật append; mặc định `Draft` đụng `Created = 0` của `SHP-002` | §5.2: chỉ thêm ở cuối, CHECK không dựa số enum; số `ShipmentStatus` chờ Q16/F11 | kế hoạch (chưa có enum); `shp-002…md:320` |
| D-31 | §6.1: `quantity` không có kiểu | §6.1: `CarrierShipmentItem.Quantity` `decimal`; §5.2 `TotalWeightGrams` | `OrderLine.Quantity` còn `int` (`Order.cs:832`) — nâng ở WP số lượng |
| D-70 | §0: không nói RMA | §0: RMA đơn giao vận thuộc `SAL-006`, ngoài phạm vi; Giai đoạn đổi GĐ2 → GĐ3 (`D-69`) | `docs/agents/anh-xa-lo-trinh-d69.md:28` |
| D-89 | §0/§1: "`AddressHub` chưa có", không rõ lấy mã hãng ở đâu | §0, §1 (E2, BR-020), §6.1, §6.5, §12, §14 (TC-34), §15 (Q11, Q12): mã hãng chỉ qua `ResolveCarrierAddressCodeAsync` | ⬜ WP-15; `CarrierAddressMappingAppService.cs:63` (chiều xuôi) |
| D-108 | §1: nguồn địa chỉ nhận là câu hỏi mở · §6.2: mẫu request mang `recipient*`/`addressLine`/`*Code` và `codAmount`/`currency`/`items` (COD client gõ, trái (2)) | §1 (BR-021), §5.2, §6.1–§6.3, §14 (TC-36…TC-39, TC-52), §15: địa chỉ, COD, dòng hàng từ đơn qua hợp đồng Sales; `CreateShipmentDto` chỉ còn `note`; ước tính tách theo đơn/giỏ | ⬜ kế hoạch: `IOrderShippingDirectory` (Sales) + snapshot trên `Order` chưa có (grep `modules/Sales/**/*.cs` = 0); `AddressContracts.cs:41-54,163` |
| D-109 | ADR-03, §3.2 #1, §4.2 P1, §5.2, §7.3: khoá `Create:{tenantId}:{orderId}:{attemptNo}` = `ClientOrderCode` (mã đổi theo lần thử, trái BR-003); Q14 mở | ADR-03, §1 (BR-022), §3.2 #1, §4.2, §5.2 (cột `Generation` + UNIQUE `(TenantId, OrderId, Generation)` `NULLS NOT DISTINCT`), §6.2, §6.3 (`VanDonCuChuaRoKetQua`), §7.3 (khoá guard kèm `generation`; mã = hàm thuần), §8.1 #3b, §12, §14 (TC-07, TC-40…TC-45), §15 (Q14 đóng, F8, F9); v0.5: cạnh `Submitting → Cancelled` bị bỏ vì `Generation + 1` vô điều kiện sau `Cancelled` trái ④ (hai vận đơn thật) — §3.1, §3.2 #1/#6, A4, TC-51 | ⬜ kế hoạch (bảng/hàm chưa có, grep `*.cs` = 0); `IdempotencyGuard.cs:27` đã có; GHN `client_order_code` ≤ 50, trùng ⇒ trả `order_code` cũ (developer.ghn.vn, tra 18/11/2026, nguồn `D-109`) |
| D-110 | §1/§6.1/§12/§15 Q13: nguồn mã hãng chỉ qua `ResolveCarrierAddressCodeAsync` (Guid), đường mã → Id chưa chọn | §0, §1 (E2, BR-020), §6.1 (đoạn "Nguồn mã hãng"), §6.3 (`0014`), §6.5, §12, §14 (TC-34, TC-46…TC-48), §15 (Q13 đóng, F10): cửa mã nhận `(SchemaVersion, cấp, mã)`, không Guid trong snapshot, không tra theo `ShippingAddressId` | ⬜ WP-15; `AdministrativeUnitResolver.cs:20-43`, `AddressErrorCodes.cs:10,25`, `AddressContracts.cs:157-168` |
| Hiện trạng | §1/§4.2/§5.1/§6.5/§11/§15 F5: Money, `IIdempotencyGuard`, outbox "chưa có"; chỉ `Order.ShippingTotal` liên quan; `Money` "dùng trực tiếp" với `ToMinorUnits()`; UoW ba pha không tắt UoW ngầm | Có; `Order` còn `CarrierCode`/`TrackingNumber`/`CodAmount`/`ShippingFeeActual`/`WeightGram` (§5.1, §7.1); `Money` 🔶 không có `ToMinorUnits()`/`Money.Of` (ADR-05, Q17); BR-002 yêu cầu `isTransactional: false` | `Money.cs:23,36,39`, `Order.cs:110-130,292`, `eCommerceHttpApiHostModule.cs:94`, `PaymentAppService.cs:561`, `MetricsTests.cs:64` |
| Không lệch | D-03, D-06, D-18/D-42, D-27, D-45, D-47/D-48 | Không liên quan tới adapter GHN | — |
