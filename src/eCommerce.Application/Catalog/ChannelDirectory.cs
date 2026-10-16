using eCommerce.Catalog.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using ChannelEntity = eCommerce.ChannelHub.Channels.Channel;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **TRA KÊNH** — hiện thực `IChannelDirectory` (`CAT-003`). Xem chú thích ở **hợp đồng** để biết vì sao
/// tách interface/implementation.
///
/// **BA QUYẾT ĐỊNH CỦA LỚP NÀY:**
/// <list type="number">
///   <item>🔒 **Chỉ ĐỌC** — `IRepository&lt;Channel, Guid&gt;` và **không** method nào đổi trạng thái kênh.
///     📌 Đổi trạng thái kênh là việc của người vận hành qua app service riêng (có quyền + có audit); nếu module
///     `Catalog` ghi được vào kênh thì **ranh giới module** mất nghĩa và **audit** không còn biết ai làm gì;</item>
///   <item>🔑 **Xoá MỀM phải bị loại** — `Channel` là `FullAuditedAggregateRoot` ⇒ có `IsDeleted`. Bộ lọc của
///     `IRepository` đã lo `IsDeleted` **tự động**, nên hàm ở đây **không** cần (và **không được**) tự kiểm lại:
///     📌 kiểm hai lần thì hai chỗ có thể **lệch nhau** khi ai đó tắt bộ lọc;</item>
///   <item>⚠️ **`Channel` là bảng DÙNG CHUNG (không `IMultiTenant`)** — kênh là **định nghĩa hệ thống** (Shopee,
///     Lazada…), **không** thuộc tenant nào. 🔒 Hệ quả: **hai tenant khác nhau** thấy **cùng** danh sách kênh,
///     và **không** có rủi ro rò dữ liệu giữa tenant ở đây (khác hẳn bảng có `TenantId`).</item>
/// </list>
/// 📌 Vì sao `ITransientDependency` (không phải app service): đây là **hợp đồng nội bộ giữa module**, **không**
/// phải endpoint. ⚠️ Nếu để nó thành app service thì ABP **tự sinh API controller** ⇒ **mở ra Internet** một
/// đường tra kênh mà **không ai review** (`AllowlistCongKhaiTests` sẽ bắt — nhưng tốt hơn là **không tạo ra**).
/// </summary>
public class ChannelDirectory : IChannelDirectory, ITransientDependency
{
    private readonly IRepository<ChannelEntity, Guid> _kenhRepository;
    private readonly IAsyncQueryableExecuter _thucThi;

    /// <summary>
    /// 🚨 **PHẢI TIÊM `IAsyncQueryableExecuter`** — lớp này **KHÔNG** phải `ApplicationService`, nên **không** có
    /// thuộc tính `AsyncExecuter` sẵn. ⚠️ Đã vấp thật: dùng `AsyncExecuter.ToListAsync(...)` ⇒ **`CS0103`**
    /// (*"The name 'AsyncExecuter' does not exist"*). 📌 Cùng họ với `DataFilter` trong `Controller`
    /// (`CAT-003` §22.3): **thuộc tính của lớp cha KHÔNG có ở lớp không kế thừa nó** — phải **tiêm**.
    /// </summary>
    public ChannelDirectory(IRepository<ChannelEntity, Guid> kenhRepository, IAsyncQueryableExecuter thucThi)
    {
        _kenhRepository = kenhRepository;
        _thucThi = thucThi;
    }

    /// <inheritdoc />
    public async Task<bool> KenhHoatDongAsync(string channelCode)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            // ⚠️ Trả `false` (không ném): mã rỗng **chắc chắn** không phải kênh hoạt động, và chỗ gọi cần dùng
            // kết quả ở **điều kiện** — ném ở đây sẽ buộc chỗ gọi bọc `try/catch` rồi **đoán** mã lỗi nào là
            // *"kênh sai"* (đúng loại điều tra tốn thời gian).
            return false;
        }

        var ma = channelCode.Trim();

        // 🔒 `IsActive` là **một phần của định nghĩa "hoạt động"** — không phải kiểm thêm cho chắc.
        // 📌 Một kênh `IsActive = false` **vẫn** có trong bảng (còn lịch sử, còn listing cũ), nhưng **không**
        // được phép nhận cấu hình hiển thị **mới** ⇒ nếu bỏ `IsActive` ở đây thì guard `0207` mất tác dụng.
        return await _kenhRepository.AnyAsync(c => c.Code == ma && c.IsActive);
    }

    /// <inheritdoc />
    public async Task<string?> TenKenhAsync(string channelCode)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            return null;
        }

        var ma = channelCode.Trim();

        var ten = await _kenhRepository.FirstOrDefaultAsync(c => c.Code == ma);

        return ten?.Name;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> DanhSachKenhHoatDongAsync()
    {
        var truyVan = await _kenhRepository.GetQueryableAsync();

        var ds = await _thucThi.ToListAsync(
            truyVan.Where(c => c.IsActive).OrderBy(c => c.Code).Select(c => c.Code));

        return ds;
    }
}
