using eCommerce.Kernel.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eCommerce.Observability;

/// <summary>
/// **BỌC `ILoggerProvider` ĐỂ ĐẾM LỖI GHI LOG — VÀ KHÔNG LÀM MẤT THÔNG ĐIỆP.**
///
/// 🚨 **VẤN ĐỀ THẬT:** khi sink (file/console/collector) ném exception, `LoggerFactory` không có metric nào;
/// lỗi chỉ nổi lên nếu ai đó đọc exception — mà exception xảy ra **trong lúc ghi log**, nên thường bị nuốt.
/// 📌 Hệ quả: "log im lặng" và "hệ thống không có gì để log" **trông giống hệt nhau**.
///
/// ✅ **CÁCH LÀM:** bọc **từng** provider; mọi lời gọi `IsEnabled`/`BeginScope`/`Log` được chuyển tiếp.
/// Nếu provider bên trong ném:
/// <list type="number">
///   <item>ghi **ngay** một dòng dự phòng ra `Console.Error` (hoặc sink tiêm vào khi test) — **không nuốt log**;</item>
///   <item>tăng `logging_failures_total` **một lần** cho lần ghi hỏng.</item>
/// </list>
/// 🔒 **Không đệ quy:** dòng dự phòng dùng `Console.Error` trực tiếp, **không** đi qua `ILogger`; nếu chính
/// việc tăng metric ném (không mong đợi), exception đó bị chặn **sau khi** dòng dự phòng đã được ghi.
///
/// 🔑 **Vì sao bọc provider chứ không bọc `ILoggerFactory`:** `LoggerFactory` cấp logger theo category và
/// giữ scope; bọc provider giữ nguyên cơ chế đó, chỉ thêm một lớp bắt lỗi sát sink.
/// </summary>
public sealed class DemLoiGhiLogProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ILoggerProvider _benTrong;
    private readonly eCommerceMetrics _metrics;
    private readonly Action<string, Exception?> _ghiDuPhong;
    private IExternalScopeProvider? _scopeProvider;

    public DemLoiGhiLogProvider(
        ILoggerProvider benTrong,
        eCommerceMetrics metrics,
        Action<string, Exception?>? ghiDuPhong = null)
    {
        _benTrong = benTrong;
        _metrics = metrics;
        _ghiDuPhong = ghiDuPhong ?? GhiRaConsoleError;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = _benTrong.CreateLogger(categoryName);

        if (_scopeProvider is not null && logger is ISupportExternalScope hoTroScope)
        {
            hoTroScope.SetScopeProvider(_scopeProvider);
        }

        return new Logger(logger, categoryName, this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;

        if (_benTrong is ISupportExternalScope hoTroScope)
        {
            hoTroScope.SetScopeProvider(scopeProvider);
        }
    }

    public void Dispose() => _benTrong.Dispose();

    private void GhiLoi(string categoryName, LogLevel level, EventId eventId, Exception? exception, string? thongDiep)
    {
        // 1) KHÔNG NUỐT LOG: in dòng dự phòng trước, kể cả khi metric ở bước 2 hỏng.
        try
        {
            _ghiDuPhong(
                $"[LOGGING-FAILURE] category={categoryName} level={level} eventId={eventId.Id} " +
                $"message={thongDiep ?? "(null)"}",
                exception);
        }
        catch (Exception)
        {
            // Ngay cả stderr cũng hỏng (bị đóng) — không còn nơi nào để ghi; không được làm sập ứng dụng.
        }

        // 2) Đếm. Nếu chính metric ném, đã có dòng dự phòng ở trên ⇒ không đệ quy, không mất thông điệp.
        try
        {
            _metrics.LoggingFailures.Add(1);
        }
        catch (Exception)
        {
            // Không gọi logger để báo lỗi metric (sẽ đệ quy vào chính wrapper này).
        }
    }

    private static void GhiRaConsoleError(string thongDiep, Exception? exception)
    {
        Console.Error.WriteLine(thongDiep);

        if (exception is not null)
        {
            Console.Error.WriteLine(exception);
        }
    }

    private sealed class Logger : ILogger
    {
        private readonly ILogger _benTrong;
        private readonly string _categoryName;
        private readonly DemLoiGhiLogProvider _provider;

        public Logger(ILogger benTrong, string categoryName, DemLoiGhiLogProvider provider)
        {
            _benTrong = benTrong;
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            try
            {
                return _benTrong.BeginScope(state);
            }
            catch (Exception ex)
            {
                _provider.GhiLoi(_categoryName, LogLevel.None, default, ex, "BeginScope");
                return null;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            try
            {
                return _benTrong.IsEnabled(logLevel);
            }
            catch (Exception ex)
            {
                _provider.GhiLoi(_categoryName, logLevel, default, ex, "IsEnabled");
                return false;
            }
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                _benTrong.Log(logLevel, eventId, state, exception, formatter);
            }
            catch (Exception ex)
            {
                string? thongDiep;

                try
                {
                    thongDiep = formatter(state, exception);
                }
                catch (Exception)
                {
                    thongDiep = state?.ToString();
                }

                _provider.GhiLoi(_categoryName, logLevel, eventId, ex, thongDiep);
            }
        }
    }
}

/// <summary>
/// Đăng ký bọc **mọi** `ILoggerProvider` đang có (console, debug, event source…).
///
/// 🚨 **Vì sao phải thay descriptor, không thêm provider mới:** thêm một provider nữa chỉ tạo **thêm** một
/// sink; nó không bắt được exception của các sink cũ. Phải bọc **chính** từng descriptor, giữ nguyên
/// lifetime và thứ tự.
/// </summary>
public static class DangKyDemLoiGhiLog
{
    public static IServiceCollection ThemDemLoiGhiLog(this IServiceCollection services)
    {
        var goc = services.Where(d => d.ServiceType == typeof(ILoggerProvider)).ToList();

        foreach (var moTa in goc)
        {
            services.Remove(moTa);
        }

        foreach (var moTa in goc)
        {
            services.Add(ServiceDescriptor.Describe(
                typeof(ILoggerProvider),
                sp => new DemLoiGhiLogProvider(
                    TaoBenTrong(sp, moTa),
                    sp.GetRequiredService<eCommerceMetrics>()),
                moTa.Lifetime));
        }

        return services;
    }

    private static ILoggerProvider TaoBenTrong(IServiceProvider sp, ServiceDescriptor moTa)
    {
        if (moTa.ImplementationInstance is ILoggerProvider instance)
        {
            return instance;
        }

        if (moTa.ImplementationFactory is not null)
        {
            return (ILoggerProvider)moTa.ImplementationFactory(sp);
        }

        if (moTa.ImplementationType is not null)
        {
            return (ILoggerProvider)ActivatorUtilities.CreateInstance(sp, moTa.ImplementationType);
        }

        throw new InvalidOperationException(
            "Dang ky ILoggerProvider khong co ImplementationType/Factory/Instance de boc.");
    }
}
