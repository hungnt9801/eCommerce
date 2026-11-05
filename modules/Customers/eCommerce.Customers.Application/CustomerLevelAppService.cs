using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Customers;

/// <summary>
/// 🏅 **API CẤP KHÁCH** (`CusLevels`) — `WP-07` (`07 §WP-07`: *"`CustomerLevel` (ngưỡng)"*).
///
/// 🔐 **Quyền:** đọc = `Customers.Default`; **ghi** = `Customers.Manage`. 📌 Cấp khách là **đầu vào của
/// chính sách giá/ưu đãi** (dù `WP-07` chưa nối vào bảng giá) ⇒ người *xem khách* **không** đương nhiên
/// được **đổi ngưỡng** — cùng lý do `Customers.ManageGroups` tách khỏi `Manage`.
///
/// 🚨 **Xoá cấp = TẮT (`IsActive = false`), không xoá cứng:** khách đang trỏ `LevelId` vào cấp đó; xoá cứng
/// là để lại **tham chiếu treo** và màn chi tiết khách hiện cấp trống mà **không có lỗi nào**.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Customers.Default)]
public class CustomerLevelAppService : ApplicationService, ICustomerLevelAppService
{
    private readonly IRepository<CustomerLevel, Guid> _cap;

    public CustomerLevelAppService(IRepository<CustomerLevel, Guid> cap)
    {
        _cap = cap;
    }

    /// <summary>DS cấp khách — sắp xếp **tất định** theo ngưỡng rồi `SortOrder` (cùng luật chọn cấp).</summary>
    public async Task<List<CustomerLevelDto>> GetListAsync(bool? chiHoatDong = null)
    {
        var ds = chiHoatDong is null
            ? await _cap.GetListAsync()
            : await _cap.GetListAsync(x => x.IsActive == chiHoatDong.Value);

        return ds
            .OrderBy(x => x.MinAccumulatedAmount)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(ToDto)
            .ToList();
    }

    public async Task<CustomerLevelDto> GetAsync(Guid id)
        => ToDto(await LayAsync(id));

    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerLevelDto> CreateAsync(TaoCapKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var daCo = await _cap.FirstOrDefaultAsync(x => x.Code == input.Code.Trim());

        if (daCo is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCapKhachDaTonTai)
                .WithData("code", input.Code)
                .WithData("capDangDung", daCo.Id);
        }

        var cap = new CustomerLevel(
            GuidGenerator.Create(), input.Code, input.Name, input.MinAccumulatedAmount,
            input.DiscountPercent, input.SortOrder, input.Note);

        await _cap.InsertAsync(cap, autoSave: true);

        Logger.LogInformation("ĐÃ TẠO CẤP KHÁCH: {Code} · ngưỡng {Nguong} · CK {Ck}%. [WP07-CAP-KHACH]",
            cap.Code, cap.MinAccumulatedAmount, cap.DiscountPercent);

        return ToDto(cap);
    }

    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerLevelDto> UpdateAsync(Guid id, TaoCapKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var cap = await LayAsync(id);

        var trung = await _cap.FirstOrDefaultAsync(x => x.Code == input.Code.Trim() && x.Id != id);

        if (trung is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCapKhachDaTonTai)
                .WithData("code", input.Code)
                .WithData("capDangDung", trung.Id);
        }

        cap.DatMa(input.Code);
        cap.DatTen(input.Name);
        cap.DatNguong(input.MinAccumulatedAmount, input.DiscountPercent);
        cap.DatThuTu(input.SortOrder);
        cap.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        await _cap.UpdateAsync(cap, autoSave: true);

        return ToDto(cap);
    }

    /// <summary>Bật/tắt cấp — 🔒 **không xoá cứng** (xem chú thích lớp).</summary>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerLevelDto> SetActiveAsync(Guid id, bool isActive)
    {
        var cap = await LayAsync(id);

        if (isActive)
        {
            cap.Bat();
        }
        else
        {
            cap.Tat();
        }

        await _cap.UpdateAsync(cap, autoSave: true);

        Logger.LogWarning("ĐÃ {ThaoTac} CẤP KHÁCH: {Code}. [WP07-CAP-KHACH]",
            isActive ? "BẬT" : "TẮT", cap.Code);

        return ToDto(cap);
    }

    private async Task<CustomerLevel> LayAsync(Guid id)
        => await _cap.FindAsync(id)
           ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayCapKhach).WithData("id", id);

    private static CustomerLevelDto ToDto(CustomerLevel x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        MinAccumulatedAmount = x.MinAccumulatedAmount,
        DiscountPercent = x.DiscountPercent,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
        Note = x.Note
    };
}
