using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Notification;

/// <summary>Hợp đồng của module Notification: DTO + interface app service + appender + sender + ETO.</summary>
[DependsOn(
    typeof(NotificationDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class NotificationApplicationContractsModule : AbpModule
{
}
