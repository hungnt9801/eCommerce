---
name: abp-module-rules
description: Luật ranh giới module ABP + bất biến dữ liệu của eCommerce cho các vai arch/be/rev — module không phụ thuộc ngược app, giao tiếp qua *.Application.Contracts và ETO + outbox, handler IDistributedEventBus mở UoW riêng, [Authorize] cấp class, tenant filter, Money/decimal, sổ cái append-only, idempotency ở mọi biên vào, máy trạng thái ném lỗi. Nạp sẵn cho arch, be, rev; dùng khi khảo sát, viết hoặc review code trong modules/, src/, shared/.
user-invocable: false
---

# Luật module ABP — eCommerce

Nguồn sự thật: `docs/coding-conventions.md` (§2 ranh giới, §4 tiền, §5 ghi dữ liệu, §6 sự kiện, §7 quyền, §10 EF) ·
`docs/nhanh/decisions.md` · mã thật trong `modules/` và `src/`. Skill **dẫn**, không chép; mâu thuẫn ⇒ tệp nguồn thắng.

## 1. Cấu trúc và ranh giới

| # | Luật | Dấu hiệu vi phạm |
|---|---|---|
| M1 | Module **sở hữu entity của mình**. `module → app` **CẤM**; `app → module` được phép. | `using eCommerce.Web`/`eCommerce.HttpApi` trong `modules/**` |
| M2 | Lời gọi chéo module **chỉ** qua `eCommerce.<Module>.Application.Contracts` hoặc ETO. | Tham chiếu `eCommerce.<Module>.Domain`/`.Application` từ module khác |
| M3 | Không FK/navigation xuyên module; không transaction xuyên module. | `HasOne` trỏ entity module khác, `[ForeignKey]` chéo module |
| M4 | Mọi entity dùng `IRepository<T>` **phải** có `DbSet` trên DbContext hợp nhất. | Thiếu ⇒ `IRepository<T>` không được đăng ký (lỗi im lặng) |
| M5 | Mỗi project `*.Application` có `AbpModule` **và** nằm trong `[DependsOn]` của Host; gộp **một** attribute `[DependsOn]`. | API trả 404 body rỗng, không lỗi biên dịch |
| M6 | `ConventionalControllers` đăng ký động cho assembly `eCommerce*.Application` **đã nạp**. | Sửa chỗ đăng ký mà quên assembly mới |

## 2. Sự kiện

| # | Luật |
|---|---|
| E1 | Hỏi trước khi tạo `*Eto`: *"mất sự kiện này có mất thông tin DB không có không?"* — **CÓ** = mang dữ liệu (không gắn `INotificationEvent`, bên phát fail-closed); **KHÔNG** = thông báo (gắn `INotificationEvent`). |
| E2 | Handler `IDistributedEventBus` **ghi dữ liệu** **PHẢI** tự `IUnitOfWorkManager.Begin(requiresNew: true, isTransactional: true)`. `[UnitOfWork]` trần **không đủ** (UoW đã dispose ⇒ `ObjectDisposedException` chỉ lộ khi chạy thật). |
| E3 | Nơi publish `*Eto` mang dữ liệu **phải** kiểm có người nhận qua `IEventCoverageProbe` và **fail-closed** nếu chưa. **CẤM** tự quét assembly. |
| E4 | ETO hậu tố `Eto`; ETO con của một dòng là `LineEto`. |

## 3. Tiền, số, thời gian

| # | Luật |
|---|---|
| T1 | Tiền `decimal` qua `Money`; **CẤM** `double`/`float`; **CẤM** so tiền bằng `==`. |
| T2 | `Money` khác tiền tệ ⇒ **NÉM LỖI**, không tự quy đổi. VND = 0 chữ số thập phân. |
| T3 | Số lượng `decimal(18,3)` (`D-31`); tiền `decimal(18,4)`. |
| T4 | Thời gian lưu **UTC** (`…Utc`), chỉ đổi giờ VN ở tầng hiển thị. Làm tròn **chỉ** theo `XC-001`. |

## 4. Ghi dữ liệu và đồng thời

| # | Luật |
|---|---|
| D1 | Mọi thao tác ghi qua **Unit of Work** của ABP. |
| D2 | Tồn kho = **sổ cái append-only**; **CẤM** `UPDATE` số lượng; một cửa ghi. |
| D3 | Unique index trên bảng `IMultiTenant` **PHẢI** có `NULLS NOT DISTINCT`. Test bất biến liệt kê **đích danh tên index**. |
| D4 | **Idempotency ở MỌI biên vào** (webhook, IPN, job retry); webhook là at-least-once. |
| D5 | Khoá phân tán cho tài nguyên dùng chung, sinh qua `<Module>Consts.LockKeys.*`; **CẤM** tự thêm `ecommerce:` (KeyPrefix đã có). |
| D6 | Máy trạng thái **NÉM LỖI** ở nhánh sai, không im lặng bỏ qua. Enum lưu DB chỉ thêm ở **cuối** dải. |

## 5. Xác thực và phân quyền

| # | Luật |
|---|---|
| A1 | `[Authorize]` ở **cấp CLASS** cho mọi app service; đặt ở method ⇒ quên một method là hở endpoint. |
| A2 | Permission dùng **hằng số** trong `eCommerceConsts.Permissions`; phải được định nghĩa trong `PermissionDefinitionProvider`. |
| A3 | **CẤM** `AddAlwaysAllowAuthorization()`; **CẤM** bật `MapInboundClaims`; giữ `NameClaimType = "preferred_username"`. |
| A4 | `[AllowAnonymous]` **chỉ** trong allowlist có lý do. Endpoint mới có quyền ⇒ thêm vào ma trận `200/401/403`. |
| A5 | Multi-tenant: entity `IMultiTenant` có `TenantId`; truy vấn đi qua filter của ABP; không tự `IgnoreQueryFilters()` nếu không có lý do ghi trong code. |

## 6. Kiểm chứng tối thiểu (vai be/rev tự chạy)

```powershell
dotnet build eCommerce.slnx --no-incremental     # 0 error, 0 warning mới
dotnet test test/eCommerce.Tests --no-build      # số ca không giảm
pwsh -File tools/kiem-tat-ca.ps1                 # cổng nhẹ
```

- Thay đổi DI ⇒ **khởi động Host thật** (build/test xanh không chứng minh resolve được).
- Không chạy `dotnet build` khi Host đang chạy (khoá `bin/`).
- Sửa bug ⇒ **test tái hiện trước**; test bất biến cấu trúc dùng reflection.
