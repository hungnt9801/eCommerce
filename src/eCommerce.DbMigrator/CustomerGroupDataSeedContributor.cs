using eCommerce.Customers;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace eCommerce.DbMigrator;

/// <summary>
/// 🚨 **SEED NHÓM KHÁCH MẶC ĐỊNH** — `CUS-001` **`ADR-09`** + **§11** (*"Seed bắt buộc"*), làm ở `S3`.
///
/// **VÌ SAO SEED LÀ BẮT BUỘC (không phải tiện ích):** `Customer.CustomerGroupId` là **đầu vào của bảng giá**
/// (`PRC-001` Q5) và nullable (`null` = khách lẻ). 📌 Nhưng nếu **không có nhóm nào** trong bảng thì nghiệp vụ
/// *"bán sỉ cho khách doanh nghiệp"* **không diễn đạt được** — và cách duy nhất còn lại là `INSERT` tay vào DB,
/// tức là 🔒 một đường ghi **không có kiểm quyền, không có audit** (đúng điều `ADR-09` muốn tránh).
///
/// 🔑 **IDEMPOTENT THEO `(TenantId, Code)` — KHÔNG theo `count()`:**
/// <list type="number">
///   <item>`count() > 0 ⇒ bỏ qua` là **sai** khi người vận hành đã tạo thêm nhóm riêng: DB có 5 nhóm nhưng
///     **thiếu** `VIP` ⇒ seed im lặng **không chạy** ⇒ 📌 thiếu dữ liệu mà không có lỗi nào;</item>
///   <item>kiểm **từng mã** ⇒ chạy lại bao nhiêu lần cũng **chỉ** thêm nhóm còn thiếu (DoD `CUS-001` §14.3:
///     *"chạy 2 lần ⇒ vẫn 3 nhóm"*).</item>
/// </list>
///
/// ⚠️ **Ba mã này CHỈ xuất hiện ở đây (và ở tài liệu)** — 🔒 **cấm** hard-code trong code nghiệp vụ
/// (`BR-CUS-0015`): nhóm là **dữ liệu**, không phải `enum`. 🛡️ Test `NhomKhachKhongHardCodeTests` (TC-21) quét
/// các assembly nghiệp vụ và đỏ nếu thấy chuỗi `"RETAIL"`/`"VIP"` ngoài tệp seed này.
/// </summary>
public class CustomerGroupDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>
    /// Ba nhóm mặc định theo `CUS-001` §11. 📌 `RETAIL` là nhóm **khách lẻ** — ⚠️ nó **không** thay thế `null`:
    /// `null` vẫn nghĩa là *"chưa phân nhóm"* (khách vãng lai mới) và **được** dùng như khách lẻ khi tính giá.
    /// </summary>
    private static readonly (string Code, string Name, string Description)[] NhomMacDinh =
    [
        ("RETAIL", "Khách lẻ",
            "Khách mua lẻ theo giá niêm yết. Đây là nhóm mặc định về mặt nghiệp vụ cho khách chưa phân nhóm."),
        ("WHOLESALE", "Bán sỉ",
            "Khách mua số lượng lớn / đại lý. Bảng giá sỉ được cấu hình ở Pricing (PRC-001), không cấu hình ở đây."),
        ("VIP", "Khách VIP",
            "Khách thân thiết / hợp đồng khung. Nhóm này thường đi kèm chính sách giá và chăm sóc riêng.")
    ];

    private readonly IRepository<CustomerGroup, Guid> _nhomRepository;
    private readonly IGuidGenerator _guidGenerator;

    public CustomerGroupDataSeedContributor(
        IRepository<CustomerGroup, Guid> nhomRepository,
        IGuidGenerator guidGenerator)
    {
        _nhomRepository = nhomRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        foreach (var (code, name, description) in NhomMacDinh)
        {
            // 🔒 Kiểm **TỪNG MÃ** (không phải `GetCountAsync() > 0`) — xem lý do (1) ở doc của lớp.
            if (await _nhomRepository.AnyAsync(x => x.Code == code))
            {
                continue;
            }

            await _nhomRepository.InsertAsync(
                new CustomerGroup(_guidGenerator.Create(), code, name)
                {
                    Description = description
                },
                autoSave: true);
        }
    }
}
