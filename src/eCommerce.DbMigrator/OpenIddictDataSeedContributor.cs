using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace eCommerce.DbMigrator;

/// <summary>
/// Seed OpenIddict: tạo <b>scope</b> và <b>application client</b> để có thể lấy token.
///
/// 🚨 VÌ SAO BẮT BUỘC: OpenIddict từ chối mọi yêu cầu token từ client không tồn tại trong DB.
/// Không có seeder này thì <c>POST /connect/token</c> luôn trả lỗi <c>invalid_client</c>.
///
/// 📌 Cách viết dùng <see cref="OpenIddictApplicationDescriptor"/> / <see cref="OpenIddictScopeDescriptor"/>
/// thay vì set trực tiếp thuộc tính entity của ABP — nhờ vậy không phụ thuộc chi tiết kiểu dữ liệu
/// nội bộ của module và ít bị lệch khi nâng version ABP.
///
/// 🔐 ĐỔI ClientSecret và chuyển các giá trị này sang cấu hình (appsettings/secret store)
/// trước khi lên production.
/// </summary>
public class OpenIddictDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string ApiScopeName = "eCommerce";
    public const string ClientId = "eCommerce_App";
    public const string ClientSecret = "1q2w3e*";

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ILogger<OpenIddictDataSeedContributor> _logger;

    public OpenIddictDataSeedContributor(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<OpenIddictDataSeedContributor> logger)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        if (await _scopeManager.FindByNameAsync(ApiScopeName) is not null)
        {
            return;
        }

        await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = ApiScopeName,
            DisplayName = "eCommerce API",
            Resources = { ApiScopeName }
        });

        _logger.LogInformation("Đã tạo OpenIddict scope '{Scope}'.", ApiScopeName);
    }

    private async Task CreateApplicationsAsync()
    {
        if (await _applicationManager.FindByClientIdAsync(ClientId) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            DisplayName = "eCommerce API client (dev)",
            // ConsentType = Implicit ⇒ không cần màn hình consent (ta chưa có UI).
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.EndSession,

                // grant dùng để test ngay: password (đổi lấy token bằng user) và
                // client_credentials (service-to-service).
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                OpenIddictConstants.Permissions.ResponseTypes.IdToken,
                OpenIddictConstants.Permissions.ResponseTypes.Token,

                // Cho phép client xin scope của API.
                OpenIddictConstants.Permissions.Prefixes.Scope + ApiScopeName,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess
            }
        };

        await _applicationManager.CreateAsync(descriptor);

        _logger.LogInformation(
            "Đã tạo OpenIddict client '{ClientId}' (scope: {Scope}).",
            ClientId, ApiScopeName);
    }
}
