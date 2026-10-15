# QUY ƯỚC VIẾT CODE

> **Trạng thái:** ✅ áp dụng từ 14/10/2026 (`GĐ1` §7.0 #5).
> **Mục đích:** để người mới (và agent) viết đúng **ngay từ file đầu tiên**, thay vì phát hiện sai
> sau khi đã có dữ liệu thật.
> **Tài liệu liên quan:** `docs/handover-gd1.md` §2 (quy trình **kiểm chứng**) + §6 (15 **quy ước bất biến**) ·
> `docs/features/XC-001…XC-006*.md` (quy ước nền) · `.editorconfig` (phần máy kiểm được).

---

## 0. Ba câu quan trọng nhất

1. **Build xanh + test xanh KHÔNG chứng minh code đúng.** Dự án đã gặp **16 lỗi IM LẶNG** — build xanh,
   test xanh, app chạy, nhưng **sai**. Xem `docs/runtime-verification-report.md`.
2. **Khi không chắc: nói rõ là CHƯA kiểm chứng.** Không khẳng định suông, không viết "đã xong" khi chưa đo.
3. **Sửa code phải kèm test tái hiện.** Không có test ⇒ lỗi sẽ quay lại, và lần sau nó sẽ **im lặng** hơn.

---

## 1. Kiểm chứng (bắt buộc, theo thứ tự)

```powershell
Set-Location 'D:\Projects\Tools\eCommerce'

# 1) Build — LUÔN dùng --no-incremental: build tăng dần ẨN cảnh báo thật
dotnet build eCommerce.slnx -v q --no-incremental          # mong đợi: 0 lỗi / 0 cảnh báo

# 2) Test
dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo
dotnet test test\eCommerce.IntegrationTests\eCommerce.IntegrationTests.csproj --nologo

# 3) Tài liệu (sau khi sửa .md)
npm --prefix tools run check:mermaid
```

**Ba luật không được vi phạm:**

| # | Luật | Vì sao |
|---|---|---|
| 1 | **KHÔNG chạy `dotnet build` khi Host đang chạy.** | Build ghi vào `bin/` của Host đang chạy ⇒ file bị khoá ⇒ **output bị xoá một phần** ⇒ Host sau đó lỗi **vô cớ** (đã gặp thật: `The ConnectionString property has not been initialized`). |
| 2 | **Sau MỌI thay đổi liên quan DI ⇒ PHẢI khởi động Host thật.** | Build + test xanh **không** chứng minh resolve được. Đã có lần Kernel thiếu `AbpModule` ⇒ app **không khởi động**, test vẫn xanh. |
| 3 | **Kiểm chứng ở BƯỚC RIÊNG, rồi mới viết commit message.** | Không gộp `kiểm chứng + commit` vào một lệnh — commit message sẽ được viết **trước khi biết kết quả**. |

**Kiểm chứng phải bằng TRẠNG THÁI THẬT** (DB, log, route), không bằng suy luận. Mẫu đã dùng thành công:
chèn dữ liệu thử thật vào PostgreSQL → khởi động Host → **đọc log thật** → **xác nhận DB thật** → dọn dữ liệu thử.

---

## 2. Cấu trúc & ranh giới tầng

```
shared/eCommerce.Kernel     nền dùng chung: Money, Currency, mã lỗi chung, lock key, metrics
src/    eCommerce.*         host · DbContext hợp nhất · migration · DbMigrator
modules/<Module>/           mỗi module 3 project (khi cần)
        eCommerce.<Module>.Domain            entity · value object · domain service · mã lỗi
        eCommerce.<Module>.Application       DTO · app service (auto API controller)
        eCommerce.<Module>.Application.Contracts  hợp đồng cho module/app KHÁC dùng
test/   eCommerce.Tests · eCommerce.IntegrationTests
```

| Luật | Ghi chú |
|---|---|
| **Module sở hữu entity của mình.** `module → app` là **CẤM** (app → module **được phép**). | Muốn app dùng được ⇒ khai hợp đồng ở `.Application.Contracts` |
| Mọi entity cần dùng `IRepository<T>` **phải** có `DbSet` trên DbContext hợp nhất. | Thiếu ⇒ `IRepository<T>` **không được đăng ký** (lỗi Phase 0) |
| Mỗi project `*.Application` **phải** có `AbpModule` **và** được khai trong `[DependsOn]` của Host. | Thiếu ⇒ API trả **404 body rỗng**, **không có lỗi biên dịch** |
| **Gộp `[DependsOn]` thành MỘT attribute** (không tách nhiều attribute). | ABP CLI tách nhiều attribute + thụt lề sai |
| **`ConventionalControllers`** đã đăng ký **động** cho mọi assembly `eCommerce*.Application` đã nạp. | Nếu sửa chỗ đó ⇒ nhớ nó **chỉ thấy assembly ĐÃ NẠP** |

---

## 3. Đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Namespace | **file-scoped** (`namespace X;`) | `namespace eCommerce.ChannelHub.Sync;` |
| Private field | `_camelCase` | `_channelShopRepository` |
| Bất đồng bộ | hậu tố `Async` | `SyncAsync`, `GetShopsAsync` |
| **Mã lỗi** | `eCommerce:<Miền>:<4 chữ số>`, dải **theo nhóm chức năng** | `eCommerce:Channel:0005` · `eCommerce:Address:06xx` |
| **Khoá phân tán** | `<module>:<đối tượng>:<định danh>:<hành động>` — **KHÔNG** tự thêm tiền tố `ecommerce:` | `channel:shopee:SHOP-1:token-refresh` |
| Permission | `<Group>.<Đối tượng>[.<Hành động>]`, dùng **hằng số** trong `eCommerceConsts.Permissions` | `eCommerce.Channels.Sync` |
| Hợp đồng sự kiện | hậu tố `Eto`; ETO **con** của một dòng đặt hậu tố `LineEto` | `ChannelOrderPulledEto` · `ChannelOrderPulledLineEto` |

> 🚨 **Khoá phân tán:** `AbpDistributedLockOptions.KeyPrefix` nối **KHÔNG có dấu phân cách** ⇒ tự thêm
> `ecommerce:` vào tên khoá sẽ cho ra `ecommerce:ecommerce:…` (đã gặp thật — C13). Sinh khoá **chỉ** qua
> `<Module>Consts.LockKeys.*`.

---

## 4. Tiền, thời gian, số

| Luật | Ghi chú |
|---|---|
| Tiền luôn `decimal` qua `Money` — **KHÔNG BAO GIỜ** `double`/`float`. | GrandNode 2 dùng `double` ở 96 trường |
| **KHÔNG so tiền bằng `==`.** | EShop so `ActualTotalPrice == OriginalPaymentAmount` |
| `Money` khác tiền tệ ⇒ **NÉM LỖI**, không tự quy đổi. | `XC-001` · VND dùng **0** chữ số thập phân |
| Thời gian lưu **UTC** (`…Utc`), chỉ đổi sang giờ VN ở tầng hiển thị. | Tránh lệch giờ khi đối soát |
| Làm tròn **chỉ** ở nơi được chỉ định trong `XC-001`; ghi rõ chính sách. | Sai làm tròn ⇒ sai tiền **im lặng** |

---

## 5. Ghi dữ liệu, đồng thời, idempotency

| Luật | Ghi chú |
|---|---|
| Mọi thao tác ghi đi qua **Unit of Work** của ABP. | Transaction tự động |
| Tồn kho = **sổ cái append-only**; **CẤM** `UPDATE` số lượng. | Truy vết + đối soát |
| Mọi unique index trên `IMultiTenant` **PHẢI** có `NULLS NOT DISTINCT`. | PostgreSQL coi `NULL` là khác nhau ⇒ ràng buộc **mất tác dụng im lặng** khi `TenantId` NULL (C4/F1) |
| Test bất biến index **phải liệt kê ĐÍCH DANH tên index**, không chỉ đếm tổng. | Thêm 1 index mới + hỏng 1 index cũ ⇒ **tổng không đổi** ⇒ test xanh vô nghĩa |
| **Idempotency ở MỌI biên vào** (webhook, IPN, job retry). | Webhook là at-least-once |
| **Khoá phân tán** cho tài nguyên dùng chung. | Chống race; **bắt buộc** test 2 instance |
| **Máy trạng thái PHẢI NÉM LỖI**, không im lặng bỏ qua. | eShop guard im lặng ⇒ event rơi không dấu vết |

---

## 6. Sự kiện & "publish cho ai?"

> 🚨 **Đây là chỗ đã gây lỗi #22 — lỗi nghiêm trọng nhất dự án.**

| Luật | Ghi chú |
|---|---|
| **Có NGƯỜI NHẬN trước, rồi mới nói tới giao tin cậy.** | Outbox bảo đảm *giao tin cậy*, nhưng giao **cho không ai** thì dữ liệu **vẫn mất** |
| Nơi **publish** một `*Eto` **phải** kiểm tra có người nhận rồi **fail-closed** nếu chưa. | Dùng `IEventCoverageProbe` — **một nguồn sự thật** với cảnh báo lúc khởi động |
| **CẤM** tự quét assembly để tự trả lời "có người nhận chưa". | Hai nơi tự quét ⇒ **có thể lệch nhau**, và lệch theo hướng nguy hiểm = **log nói có, thực tế mất** |
| ⚠️ Quét theo "assembly **đã nạp**" rất dễ **thiếu** (bài học C16). | Luôn **ghi ra số assembly đã quét** để phạm vi **nhìn thấy được** |

---

## 7. Xác thực & phân quyền

| Luật | Ghi chú |
|---|---|
| `[Authorize]` ở **cấp CLASS** cho mọi app service. | Đặt ở method ⇒ **quên một method là hở endpoint** (đã xảy ra thật) |
| **KHÔNG** magic string cho tên permission — dùng hằng số. | Gõ sai 1 ký tự ⇒ 403 không rõ nguyên nhân |
| Permission dùng trong `[Authorize]` **PHẢI** được định nghĩa trong `PermissionDefinitionProvider`. | Chưa định nghĩa ⇒ ABP từ chối **MỌI người**, kể cả admin |
| **KHÔNG** `AddAlwaysAllowAuthorization()`. | Đã xoá ở Phase 0 — **đừng** thêm lại |
| 🚨 **CẤM bật `MapInboundClaims`** trên `JwtBearer`; giữ `NameClaimType = "preferred_username"`. | Bật lại ⇒ claim `role`/`sub` bị **đổi tên** ⇒ `ICurrentUser` **RỖNG** ⇒ **mọi API gắn quyền trả 403** dù đã cấp quyền (xem `XC-004` **F6**). Có **bất biến lúc khởi động** `KiemTraBatBienTenClaim` la ở mức Error |
| Endpoint gắn permission **mới** ⇒ **phải** được thêm vào **ma trận `401`/`403`/`200`**. | ⚠️ Ma trận phải đo trên endpoint **GẮN QUYỀN**, **không** phải endpoint chỉ-cần-đăng-nhập |
| `[AllowAnonymous]` **chỉ** trong **allowlist** có lý do. | Endpoint công khai là bề mặt tấn công |

---

## 8. Quan sát: log & metric

| Luật | Ghi chú |
|---|---|
| Log **tiếng Việt**, câu hoàn chỉnh, nêu **hệ quả** chứ không chỉ sự việc. | Người đọc log lúc 2h sáng cần biết *"giờ phải làm gì"* |
| Lỗi **im lặng** là lỗi nặng nhất ⇒ phải có log/metric cho mọi nhánh từ chối. | Ví dụ đã sửa: `403` **không** sinh log ⇒ phải thêm `ChanDoanTuChoiQuyenHandler` |
| Mọi metric **phải có nơi phát**; bảng đăng ký ở `XC-005`. | Metric "đã khai báo mà không ai phát" là **nói dối** |
| Nhãn metric **chỉ** giá trị **HỮU HẠN** (kênh, lý do). **CẤM** `shopId`/`orderId`/`lockName`. | Cardinality cao ⇒ sập hệ thống đo |
| Bật/tắt log đúng **category của chính type** — không phải `Volo.Abp.*`. | Bật sai ⇒ **không thấy gì** và dễ kết luận sai là job không chạy |

---

## 9. Worker nền

| Luật | Ghi chú |
|---|---|
| Worker phải được **ĐĂNG KÝ TƯỜNG MINH** (`AddBackgroundWorkerAsync`). | Thiếu ⇒ app vẫn chạy, job **im lặng không bao giờ chạy** |
| Worker định kỳ đọc cấu hình trong `DoWorkAsync` ⇒ **PHẢI** đặt `Timer.RunOnStart = true`. | `DoWorkAsync` chỉ chạy **sau** nhịp đầu ⇒ cấu hình **không bao giờ có tác dụng** ở lượt đầu (lỗi #23) |
| Worker gọi **API/dịch vụ ngoài** hoặc **sửa dữ liệu vận hành** ⇒ **mặc định TẮT**, bật bằng cấu hình. | Một dịch vụ ngoài không được phép tự đổi dữ liệu mà không ai quyết định |

---

## 10. EF Core & migration

| Luật | Ghi chú |
|---|---|
| **Để EF sinh khung migration — CẤM viết tay.** | Migration viết tay thiếu `.Designer.cs` bị **bỏ qua âm thầm** (C3) |
| Chạy `DbMigrator` bằng **binary MỚI build**, rồi **kiểm trạng thái DB thật**. | DbMigrator báo `exit=0` nhưng **không áp** migration vì binary cũ |
| Seed **idempotent** (chạy lại không lỗi). | Không sửa tay DB |
| Mọi package ABP **cùng version**, sửa **một file** `Directory.Packages.props`. | Central Package Management. ⚠️ Project test đặt `ManagePackageVersionsCentrally=false` ⇒ **bỏ qua ghim**, phải ghim tường minh ở đó |
| **KHÔNG AutoMapper** (đã bỏ để tránh advisory). | Map tay, hoặc Mapperly (source generator) |

---

## 11. Test

| Luật | Ghi chú |
|---|---|
| **Mọi bug sửa xong PHẢI có test tái hiện** trước khi đóng. | Không có test ⇒ lỗi quay lại |
| Tách **lõi thuần** khỏi I/O để test được **mọi nhánh**. | Mẫu đang dùng: `EventCoverageChecker` · `QuyetDinhDongBoDonPolicy` · `TaxRateResolver` · `AdministrativeUnitSynchronizer` |
| Test bất biến **cấu trúc** ⇒ dùng **reflection** (không phải test hành vi). | VD: mọi `*AppService` có `[Authorize]`; mọi permission dùng phải được định nghĩa |
| ⚠️ **Test phải được kiểm chứng NGƯỢC** (cố ý vi phạm ⇒ test **phải đỏ**). | Một test **luôn xanh** thì vô giá trị — đã tự phát hiện 1 test như vậy khi viết `TC-02` |
| Đồng thời/oversell ⇒ **integration test** với PostgreSQL thật + lock. | `Testcontainers.PostgreSql` |
| Test đa instance ⇒ **2 process + Redis thật**. | Lock in-process không chứng minh được gì |

---

## 12. Tài liệu

| Luật | Ghi chú |
|---|---|
| **Trả lời và viết tài liệu bằng TIẾNG VIỆT.** | Quy ước #13 |
| Code lệch tài liệu ⇒ **SỬA TÀI LIỆU TRƯỚC**, cùng pull request. | Quy ước A.1 |
| Sửa xong ⇒ cập nhật mục **trạng thái triển khai ở CUỐI** tài liệu feature tương ứng. | Quy ước #15 |
| Mọi khối Mermaid phải **PASS** parser thật. | `npm --prefix tools run check:mermaid` |
| Neo vào **code thật** kèm `file:dòng`; phần **chưa có trong code** phải ghi rõ là kế hoạch giai đoạn nào. | Tránh tài liệu mô tả thứ không tồn tại |

> 🚨 **Cách viết một phát hiện (F-item):** nêu **triệu chứng** · **bằng chứng đo được** · **mức độ** ·
> **trạng thái** (⬜ chưa sửa / ✅ đã sửa + bằng chứng). **CẤM** ghi "có vẻ đúng" mà không có phép đo.

---

## 13. Việc KHÔNG được làm

- ❌ Copy mã nguồn từ nopCommerce / Smartstore / GrandNode / module ABP (giấy phép **viral/copyleft**).
- ❌ Tự viết SMS/email gateway, payment gateway, e-invoice provider — **luôn đi qua NCC**.
- ❌ `double`/`float` cho tiền · so tiền bằng `==`.
- ❌ Sửa tay DB · viết tay migration · `AddAlwaysAllowAuthorization()` · bật `MapInboundClaims`.
- ❌ "Hoàn thiện" phần phụ thuộc **input bên ngoài** (bảng trường XML, endpoint cơ quan thuế, chứng thư số,
  tài liệu hãng vận chuyển) bằng **suy đoán**. Sai ở đây là **sai về pháp lý** và **khó phát hiện nhất**.
