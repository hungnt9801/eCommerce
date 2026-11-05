using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Reporting;

/// <summary>
/// **MODULE `Reporting` — TẦNG DOMAIN** (`WP-08`, `07 §WP-08`).
///
/// 📌 Bounded context **read-model**: bảng `Rpt*` là **bản sao đã tổng hợp** của chứng từ bán
/// (`PosRetailBills`, `OrdOrders`) và tồn kho — **không** phải nguồn sự thật. Nguồn sự thật vẫn là
/// chứng từ; vì vậy mọi bảng ở đây **xoá và ghi lại được** từ chứng từ (đó là điều kiện để job chạy lại
/// không nhân đôi dữ liệu).
///
/// 🔒 **Phụ thuộc:** chỉ `Domain.Shared` + `Kernel`. 📌 Domain **không** tham chiếu `Pos.Domain`/
/// `Sales.Domain`/`Inventory.Domain` — việc ánh xạ entity nguồn sang bản ghi tổng hợp nằm ở tầng
/// Application (`AnhXaNguonBanHang`), nơi đã được phép đọc entity của module khác (tiền lệ `WP-07`).
/// Nhờ vậy **hàm thuần** của module này test được mà không cần PostgreSQL.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ReportingDomainModule : AbpModule
{
}
