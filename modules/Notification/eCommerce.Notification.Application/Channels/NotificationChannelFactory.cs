using Volo.Abp.DependencyInjection;

namespace eCommerce.Notification;

/// <summary>
/// Registry kênh gửi — **một cửa** ráp adapter theo `(ChannelKind, ProviderCode)` (ADR-01, `D-200`).
/// Dựng từ điển **một lần** khi khởi tạo; trùng khoá ⇒ **NÉM** (không "last wins" im lặng).
/// `Find` trả `null` khi không có adapter (⇒ `NotificationSender` NÉM `0003`) và **KHÔNG** fallback kênh khác.
/// </summary>
/// <remarks>
/// Lifetime **Transient**: từ điển chỉ đọc và rất nhỏ, dựng lại mỗi lần resolve là rẻ; tránh nhốt
/// (captive dependency) các adapter transient/scoped thêm sau này vào một singleton.
/// </remarks>
public sealed class NotificationChannelFactory : INotificationChannelFactory, ITransientDependency
{
    private readonly IReadOnlyCollection<INotificationChannelSender> _tatCa;
    private readonly Dictionary<(ChannelKind, string), INotificationChannelSender> _theoKhoa;

    public NotificationChannelFactory(IEnumerable<INotificationChannelSender> senders)
    {
        var tatCa = senders.ToArray();
        var theoKhoa = new Dictionary<(ChannelKind, string), INotificationChannelSender>();

        foreach (var sender in tatCa)
        {
            var khoa = (sender.ChannelKind, sender.ProviderCode);
            if (!theoKhoa.TryAdd(khoa, sender))
            {
                throw new InvalidOperationException(
                    "Trùng adapter kênh gửi cho cùng (ChannelKind, ProviderCode): " +
                    $"({sender.ChannelKind}, '{sender.ProviderCode}'). Mỗi cặp chỉ được MỘT adapter.");
            }
        }

        _tatCa = Array.AsReadOnly(tatCa);
        _theoKhoa = theoKhoa;
    }

    public IReadOnlyCollection<INotificationChannelSender> All => _tatCa;

    public INotificationChannelSender? Find(ChannelKind channel, string providerCode)
        => _theoKhoa.TryGetValue((channel, providerCode), out var sender) ? sender : null;
}
