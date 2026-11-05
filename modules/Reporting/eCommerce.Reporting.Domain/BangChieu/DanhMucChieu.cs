using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **CHIỀU NGÀY** (`RptDimDates`) — `07 §WP-08`: `DimDate`.
///
/// 🔑 **VÌ SAO CẦN bảng này (không chỉ dùng `DateOnly` trên bảng sự kiện):** báo cáo phải trả lời được
/// *"tháng 2 có bao nhiêu ngày làm việc"*, *"doanh thu theo thứ trong tuần"* — những câu hỏi cần
/// **thuộc tính của ngày**, không phải của chứng từ. 📌 Cùng một ngày được tính **một lần** thay vì mỗi
/// dòng sự kiện tự suy ra (và suy ra khác nhau ở chỗ xử lý múi giờ).
/// </summary>
public class RptDimDate : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Ngày kinh doanh (giờ Việt Nam) — xem <see cref="NgayKinhDoanh"/>.</summary>
    public DateOnly BusinessDate { get; protected set; }

    public int Year { get; protected set; }

    public int Quarter { get; protected set; }

    public int Month { get; protected set; }

    /// <summary>Ngày trong tháng (1…31).</summary>
    public int Day { get; protected set; }

    /// <summary>Thứ trong tuần theo `System.DayOfWeek` (0 = Chủ nhật … 6 = Thứ bảy) — lưu **SỐ**.</summary>
    public int DayOfWeek { get; protected set; }

    public bool IsWeekend { get; protected set; }

    protected RptDimDate() { }

    public RptDimDate(Guid id, DateOnly businessDate) : base(id)
    {
        BusinessDate = businessDate;
        Year = businessDate.Year;
        Quarter = (businessDate.Month - 1) / 3 + 1;
        Month = businessDate.Month;
        Day = businessDate.Day;
        DayOfWeek = (int)businessDate.DayOfWeek;
        IsWeekend = businessDate.DayOfWeek is System.DayOfWeek.Saturday or System.DayOfWeek.Sunday;
    }
}

/// <summary>
/// **CHIỀU SẢN PHẨM** (`RptDimProducts`) — `07 §WP-08`: `DimProduct`.
///
/// 📌 Khoá là **`Sku`** (không phải `ProductId`): chứng từ bán của cả POS và đơn hàng đều mang `Sku`, và
/// `OrderLine.ProductId` có thể `null` với dữ liệu cũ (`WP-03`). Khoá theo `Sku` ⇒ báo cáo **luôn** gom
/// được, kể cả với chứng từ trước backfill.
/// </summary>
public class RptDimProduct : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    /// <summary>Tên sản phẩm **tại lần tổng hợp gần nhất** — `null` khi chứng từ không mang tên.</summary>
    public string? ProductName { get; protected set; }

    protected RptDimProduct() { }

    public RptDimProduct(Guid id, string sku, string? productName = null) : base(id)
    {
        Sku = sku;
        ProductName = productName;
    }

    /// <summary>Cập nhật tên — 📌 **không xoá tên cũ khi nguồn trả `null`** (chứng từ cũ thiếu tên không được làm mất tên đã biết).</summary>
    public void CapNhatTen(string? productName)
    {
        if (!string.IsNullOrWhiteSpace(productName))
        {
            ProductName = productName.Trim();
        }
    }
}

/// <summary>**CHIỀU KHO/CỬA HÀNG** (`RptDimStores`) — `07 §WP-08`: `DimStore`.</summary>
public class RptDimStore : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    /// <summary>Mã kho (chứng từ kho dùng mã; báo cáo hiển thị mã để khớp chứng từ).</summary>
    public string? Code { get; protected set; }

    public string? Name { get; protected set; }

    protected RptDimStore() { }

    public RptDimStore(Guid id, Guid warehouseId, string? code = null, string? name = null) : base(id)
    {
        WarehouseId = warehouseId;
        Code = code;
        Name = name;
    }

    public void CapNhat(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            Code = code.Trim();
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }
    }
}

/// <summary>**CHIỀU NHÂN VIÊN** (`RptDimStaffs`) — `07 §WP-08`: `DimStaff`.</summary>
public class RptDimStaff : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid StaffId { get; protected set; }

    public string? DisplayName { get; protected set; }

    protected RptDimStaff() { }

    public RptDimStaff(Guid id, Guid staffId, string? displayName = null) : base(id)
    {
        StaffId = staffId;
        DisplayName = displayName;
    }

    public void CapNhatTen(string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
    }
}
