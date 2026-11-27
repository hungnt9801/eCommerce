using Volo.Abp.DependencyInjection;

namespace eCommerce.Notification;

/// <summary>
/// Hiện thực **stub** của cổng cấu hình kênh (`AQ1`, `D-314`): chưa có bảng `NotificationChannelAccounts`
/// ở WP-23 nên **luôn trả `null`** ⇒ mọi lần gửi fail-closed `eCommerce:Notification:0003` TRƯỚC mọi
/// lời gọi mạng. Đây là **hành vi đúng** ở WP-23 (chưa chọn NCC, `D-200`), không phải việc bỏ ngỏ.
/// KHÔNG đọc DB/appsettings. Increment sau thay hiện thực DI (kèm mở rộng `FindAsync` — nợ N1).
/// </summary>
/// <remarks>
/// `[ExposeServices]` BẮT BUỘC: ABP chỉ tự phơi interface theo quy ước `I` + tên lớp, mà tên lớp
/// `ChuaCauHinhKenhProvider` KHÔNG khớp `INotificationChannelConfigProvider` ⇒ thiếu thuộc tính này thì
/// DI không resolve được provider (Host hỏng khi dispatch bật). Tiền lệ: `XuLyKetQuaCongService.cs`.
/// </remarks>
[ExposeServices(typeof(INotificationChannelConfigProvider))]
public sealed class ChuaCauHinhKenhProvider : INotificationChannelConfigProvider, ITransientDependency
{
    public Task<NotificationChannelConfig?> FindAsync(ChannelKind channel, CancellationToken ct = default)
    {
        _ = channel;
        _ = ct;

        return Task.FromResult<NotificationChannelConfig?>(null);
    }
}
