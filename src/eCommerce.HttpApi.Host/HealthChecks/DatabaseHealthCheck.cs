using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using Volo.Abp.Data;

namespace eCommerce.HealthChecks;

/// <summary>
/// Health check cho **PostgreSQL** (vá `XC-005` D8).
///
/// 🎯 Vì sao tự viết thay vì thêm gói `AspNetCore.HealthChecks.NpgSql`:
///   ABP 10.7 **không** kèm gói HealthChecks nào (đã kiểm bằng quét DLL), và `AddHealthChecks()`
///   có sẵn trong ASP.NET Core ⇒ viết một check mở connection là đủ, **không thêm dependency nào**.
///
/// 🔒 QUY TẮC (XC-005 BR-012): **KHÔNG** trả chi tiết lỗi ra ngoài. Endpoint `/health-status` chỉ trả
/// `200` (khoẻ) hoặc `503` (có phụ thuộc hỏng); chi tiết (chuỗi kết nối, message lỗi) **chỉ đi vào log**.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    // 🚨 LỖI ĐÃ SỬA: KHÔNG inject trực tiếp `AbpDbConnectionOptions`.
    // ABP đăng ký nó qua `Configure<AbpDbConnectionOptions>(...)` ⇒ trong DI CHỈ có `IOptions<T>`.
    // Health check dùng `ActivatorUtilities` để tạo instance nên nó resolve theo ĐÚNG kiểu tham số
    // ⇒ inject thẳng `AbpDbConnectionOptions` sẽ ném lúc CHẠY:
    //   "Unable to resolve service for type 'Volo.Abp.Data.AbpDbConnectionOptions'"
    // (đã gặp thật: /health-status trả 500).
    private readonly IOptions<AbpDbConnectionOptions> _connectionOptions;

    public DatabaseHealthCheck(IOptions<AbpDbConnectionOptions> connectionOptions)
    {
        _connectionOptions = connectionOptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Health check KHÔNG bao giờ được ném ra ngoài — mọi lỗi phải thành kết quả "unhealthy".
        try
        {
            var connectionString = _connectionOptions.Value.ConnectionStrings.Default;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return HealthCheckResult.Unhealthy("Chưa cấu hình ConnectionStrings:Default.");
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("select 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL phản hồi bình thường.");
        }
        catch (Exception ex)
        {
            // Chi tiết ở ĐÂY (log), KHÔNG ở response.
            return HealthCheckResult.Unhealthy("Không kết nối được PostgreSQL.", ex);
        }
    }
}
