using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Pos;

/// <summary>
/// **MODULE `Pos` — APPLICATION** (`WP-06`).
/// 📌 Phụ thuộc `Inventory.Application.Contracts` **chỉ để** gọi `IPosStockGateway` — POS **không** đụng
/// domain/repository của kho (xem chú thích ở hợp đồng đó).
/// </summary>
[DependsOn(
    typeof(PosApplicationContractsModule),
    typeof(AbpDddApplicationModule)
    // 🆕 `WP-07` — KHÔNG khai `CustomersApplicationModule` ở đây: `eCommerce.Application` (host) **đã**
    // depends on **cả** `Pos.Application` và `Customers.Application` ⇒ hiện thực `ICustomerLedger` /
    // `IDataScopeResolver` được đăng ký trong cùng đồ thị. 📌 Thêm phụ thuộc ở đây sẽ kéo **toàn bộ** tầng
    // Application của `Customers` vào mọi test của `Pos` (chậm, và tạo đường phụ thuộc không cần thiết) —
    // trong khi `Pos` chỉ cần **hợp đồng** (`Customers.Application.Contracts`).
)]
public class PosApplicationModule : AbpModule
{
}
