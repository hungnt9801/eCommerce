using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **NHÓM KHÁCH — ĐƯỜNG GHI/ĐỌC** (`CUS-001` `ADR-09`, nợ **§16.4 #2** đóng ở `S3`).
///
/// **VÌ SAO PHẢI CÓ LỚP NÀY (nợ đã đo được, không phải "cho đủ bộ"):** từ 16/10/2026 entity `CustomerGroup`,
/// bảng `CusGroups` và **3 unique index `NULLS NOT DISTINCT`** đã tồn tại — nhưng **không có endpoint nào**
/// ⇒ 🔒 trên thực tế nhóm khách vẫn là *"dữ liệu chỉ vào được bằng `INSERT` tay"*. 📌 Đó đúng là điều
/// `ADR-09` cấm: *"nhóm khách là DỮ LIỆU, không hard-code"* — mà một bảng **không có đường ghi** thì về mặt
/// vận hành **tương đương hard-code**, chỉ khác là nó nằm trong DB.
///
/// 🔐 **HAI MỨC QUYỀN (đặc quyền tối thiểu):**
///   • `Customers.Default` (cấp **class**) — **đọc** danh sách nhóm;
///   • `Customers.ManageGroups` (trên **từng** method ghi) — tạo/đổi nhóm.
/// 🔑 **VÌ SAO KHÔNG DÙNG `Customers.Manage`:** nhóm là **đầu vào của bảng giá** (`PRC-001` Q5) ⇒ đổi nhóm là
/// đổi **giá bán cho cả một tập khách**. Người trực CSKH cần `Manage` (chặn khách chây nợ) **không** đương nhiên
/// được đổi thứ ảnh hưởng **doanh thu**. 📌 Cùng nguyên tắc đã dùng cho `Addresses.Manage` vs `ManageBook`.
/// ⚠️ `[Authorize]` **cấp class** là quy ước bắt buộc #4 — thiếu nó, method không gắn gì thành **API công khai**
/// (đã xảy ra thật ở Phase 0 với `GetShopsAsync`; 🛡️ `BatBienPhanQuyenTests.TC-01` khoá lại).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Customers.Default)]
public class CustomerGroupAppService : ApplicationService, ICustomerGroupAppService
{
    /// <summary>🔑 Trần số dòng một trang — 📌 nhóm khách là **dữ liệu tham chiếu** (vài nhóm, không phải vài nghìn),
    /// nên trần 200 là rộng rãi; và **có phân trang** để API không bao giờ trả *"tất cả"* một cách vô hạn.</summary>
    public const int TranMotTrang = 200;

    private readonly IRepository<CustomerGroup, Guid> _nhomRepository;
    private readonly IRepository<Customer, Guid> _khachRepository;

    public CustomerGroupAppService(
        IRepository<CustomerGroup, Guid> nhomRepository,
        IRepository<Customer, Guid> khachRepository)
    {
        _nhomRepository = nhomRepository;
        _khachRepository = khachRepository;
    }

    /// <summary>
    /// Danh sách nhóm + **số khách đang ở mỗi nhóm**.
    /// 📌 `SoKhach` được đếm bằng **MỘT** truy vấn gom nhóm (không phải một truy vấn mỗi nhóm) — 🔑 N+1 trên
    /// một màn hình quản trị là loại nợ `CAT-003` §16.6 #7 đã ghi, không lặp lại ở đây.
    /// </summary>
    public async Task<PagedResultDto<CustomerGroupDto>> GetDanhSachAsync(int skipCount = 0, int maxResultCount = 100)
    {
        var soLuong = Math.Clamp(maxResultCount, 1, TranMotTrang);
        var bo = Math.Max(0, skipCount);

        var truyVan = await _nhomRepository.GetQueryableAsync();

        // ⚠️ **ĐẾM TRƯỚC khi `Skip/Take`** — 📌 nếu đếm sau khi cắt trang thì `totalCount` bằng số dòng của
        // trang ⇒ UI nói *"có 3 nhóm"* trong khi có 30, và 🔑 người vận hành **kết luận sai về dữ liệu**.
        var tongSo = await AsyncExecuter.CountAsync(truyVan);

        var nhom = await AsyncExecuter.ToListAsync(
            truyVan.OrderBy(x => x.Code).Skip(bo).Take(soLuong));

        // 🔑 `null` = khách **chưa được phân nhóm** — không phải một nhóm, nên không đưa vào bảng đếm.
        var khachQuery = await _khachRepository.GetQueryableAsync();
        var demTheoNhom = (await AsyncExecuter.ToListAsync(
                khachQuery.Where(x => x.CustomerGroupId != null)
                    .GroupBy(x => x.CustomerGroupId)
                    .Select(g => new NhomDemKhach { NhomId = g.Key, SoKhach = g.Count() })))
            .Where(x => x.NhomId is not null)
            .ToDictionary(x => x.NhomId!.Value, x => x.SoKhach);

        var dto = nhom.Select(x => new CustomerGroupDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Description = x.Description,
            SoKhach = demTheoNhom.GetValueOrDefault(x.Id)
        }).ToList();

        return new PagedResultDto<CustomerGroupDto>(tongSo, dto);
    }

    /// <summary>
    /// **TẠO NHÓM** — mã tự chuẩn hoá **CHỮ HOA** ở entity (`CustomerGroup` ctor).
    /// 🚨 **KIỂM TRÙNG MÃ TRƯỚC** để trả **mã nghiệp vụ `0008`** thay vì để unique index nổ `23505` ⇒ `500`
    /// khó hiểu cho người dùng; 🔒 nhưng **unique index vẫn là hàng rào cuối** cho ca hai request đồng thời
    /// (và `BoLocViPhamRangBuocDb` đã ánh xạ `IX_CusGroups_TenantId_Code` ⇒ cùng một mã `0008`).
    /// 📌 Đây đúng khuôn `XC-003` **D2**: *"mọi bất biến phải có ràng buộc DB, nhưng lỗi thường gặp phải có mã"*.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Customers.ManageGroups)]
    public async Task<CustomerGroupDto> TaoNhomAsync(TaoNhomKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ma = (input.Code ?? string.Empty).Trim().ToUpperInvariant();

        // ⚠️ `Code` rỗng: entity đã ném `0006` (thiếu thông tin) — 📌 nhưng kiểm ở đây để **truy vấn trùng mã**
        // bên dưới không chạy với chuỗi rỗng (sẽ trả *"đã tồn tại"* một cách vô nghĩa nếu DB có dòng lỗi).
        if (ma.Length == 0)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(input.Code));
        }

        if (await _nhomRepository.AnyAsync(x => x.Code == ma))
        {
            throw new BusinessException(CustomerErrorCodes.MaNhomDaTonTai)
                .WithData("code", ma)
                .WithData("lyDo", "Mã nhóm khách này đã tồn tại trong tenant ⇒ chọn mã khác.");
        }

        var nhom = new CustomerGroup(GuidGenerator.Create(), ma, input.Name)
        {
            Description = input.Description
        };

        await _nhomRepository.InsertAsync(nhom, autoSave: true);

        // 🔒 **LOG Ở MỨC THÔNG TIN, KHÔNG chứa PII** (nhóm khách không phải dữ liệu cá nhân) — 📌 nhưng vẫn log
        // vì *"ai đổi dữ liệu ảnh hưởng giá"* là câu hỏi kiểm toán có thật (`PRC-001`).
        Logger.LogInformation("CUS-NHOM-DA-TAO: mã {Code} (id={Id}).", nhom.Code, nhom.Id);

        return new CustomerGroupDto
        {
            Id = nhom.Id,
            Code = nhom.Code,
            Name = nhom.Name,
            Description = nhom.Description,
            SoKhach = 0
        };
    }

    /// <summary>
    /// **ĐỔI TÊN/MÔ TẢ NHÓM** — 🔒 `Code` **không** đổi được (xem ghi chú ở `ICustomerGroupAppService`).
    /// **NÉM `0001`** nếu không tìm thấy: 📌 im lặng trả về sẽ khiến UI hiện *"đã lưu"* trong khi **không có gì
    /// được lưu** — đúng họ lỗi *"guard no-op im lặng"* mà quy ước #11 cấm.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Customers.ManageGroups)]
    public async Task<CustomerGroupDto> CapNhatNhomAsync(Guid id, CapNhatNhomKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var nhom = await _nhomRepository.FindAsync(id)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach)
                .WithData("customerGroupId", id)
                .WithData("lyDo", "Nhóm khách không tồn tại (có thể đã bị xoá).");

        nhom.Name = input.Name;
        nhom.Description = input.Description;

        await _nhomRepository.UpdateAsync(nhom, autoSave: true);

        Logger.LogInformation("CUS-NHOM-DA-SUA: mã {Code} (id={Id}).", nhom.Code, nhom.Id);

        var soKhach = (int)await _khachRepository.CountAsync(x => x.CustomerGroupId == nhom.Id);

        return new CustomerGroupDto
        {
            Id = nhom.Id,
            Code = nhom.Code,
            Name = nhom.Name,
            Description = nhom.Description,
            SoKhach = soKhach
        };
    }
}

/// <summary>
/// 🔑 **KIỂU ĐẾM CÓ TÊN** cho phép chiếu của EF — ⚠️ **KHÔNG** dùng `anonymous type` sau `GroupBy`:
/// 📌 EF/Npgsql có thể **không dịch** được một số hình chiếu sang kiểu vô danh, và 🔒 lỗi dịch biểu thức chỉ
/// lộ ra **lúc chạy** (đúng bài học đã ghi ở `DonHangTomTat` của `CustomerAppService`).
/// </summary>
internal sealed class NhomDemKhach
{
    public Guid? NhomId { get; set; }

    public int SoKhach { get; set; }
}
