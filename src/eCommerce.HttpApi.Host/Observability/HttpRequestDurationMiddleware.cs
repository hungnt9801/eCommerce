using eCommerce.Kernel.Observability;
using Microsoft.AspNetCore.Routing;

namespace eCommerce.Observability;

/// <summary>
/// Middleware đo **độ trễ xử lý request** và phát `http_request_duration_seconds` (`XC-005`).
///
/// 📌 Vỏ **MỎNG có chủ ý**: toàn bộ quyết định (nhãn `route` hữu hạn, đo cả khi lỗi) nằm ở
/// <see cref="RequestDurationRecorder"/> trong `shared/eCommerce.Kernel` — nơi **test được**.
/// Middleware nằm trong project **Host** (exe) nên **không thể** được test project tham chiếu;
/// để logic ở đây thì phần quan trọng nhất sẽ **không bao giờ được kiểm chứng**.
/// </summary>
public sealed class HttpRequestDurationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly eCommerceMetrics _metrics;

    public HttpRequestDurationMiddleware(RequestDelegate next, eCommerceMetrics metrics)
    {
        _next = next;
        _metrics = metrics;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return RequestDurationRecorder.DoVaGhiAsync(
            _metrics,
            context.Request.Method,
            // ⚠️ Route TEMPLATE (hữu hạn), KHÔNG phải đường dẫn thật (vô hạn theo id).
            () => context.GetEndpoint()?.Metadata.GetMetadata<RouteEndpoint>()?.RoutePattern.RawText,
            () => context.Response.StatusCode,
            () => _next(context));
    }
}
