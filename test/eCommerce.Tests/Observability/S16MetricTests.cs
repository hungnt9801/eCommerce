using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;
using eCommerce.Observability;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DistributedLocking;

namespace eCommerce.Tests.Observability;

/// <summary>
/// `S16` — hai metric được nối vào **điểm phát thật**:
/// <list type="bullet">
///   <item><c>lock_ttl_exceeded_total</c> — phát khi <b>lease khoá bị mất thật</b> (<c>HandleLostToken</c>),
///     không phải khi thao tác "chạy lâu hơn tham số timeout" (tham số đó là thời gian CHỜ giành khoá);</item>
///   <item><c>logging_failures_total</c> — phát khi một <c>ILoggerProvider</c> bên trong ném, kèm dòng dự phòng
///     ra stderr để <b>không mất thông điệp</b>.</item>
/// </list>
///
/// 🔒 Mỗi metric được kiểm **HAI CHIỀU**: có sự kiện ⇒ đếm đúng 1; không có sự kiện ⇒ **không** đếm.
/// </summary>
public class S16MetricTests
{
    // ── Hạ tầng đo ──────────────────────────────────────────────────────────────────────────────

    private sealed class BoGhi : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)> Ghi { get; } = [];

        public BoGhi(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var nhan = new Dictionary<string, object?>();
                foreach (var t in tags)
                {
                    nhan[t.Key] = t.Value;
                }

                Ghi.Add((instrument.Name, value, nhan));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    // ── (1) lock_ttl_exceeded_total ─────────────────────────────────────────────────────────────

    /// <summary>Handle Medallion giả: chỉ phát token "mất khoá" khi test gọi <see cref="MatKhoa"/>.</summary>
    private sealed class GiuGia : IDistributedSynchronizationHandle
    {
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken HandleLostToken => _cts.Token;

        public bool DaTra { get; private set; }

        public void MatKhoa() => _cts.Cancel();

        public void Dispose()
        {
            // ⚠️ KHÔNG dispose `_cts`: test "mất khoá SAU khi đã trả" cần bắn token sau thời điểm đó
            // để chứng minh wrapper đã NGẮT đăng ký, chứ không phải token không bao giờ huỷ.
            DaTra = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class KhoaGia : IAbpDistributedLock
    {
        public GiuGia? Giu { get; set; }

        public string? TenDaXin { get; private set; }

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            TenDaXin = name;

            if (Giu is null)
            {
                return Task.FromResult<IAbpDistributedLockHandle?>(null);
            }

            return Task.FromResult<IAbpDistributedLockHandle?>(
                new MedallionAbpDistributedLockHandle(Giu));
        }
    }

    private static (Meter Meter, eCommerceMetrics Metrics, BoGhi Ghi) TaoBoDo()
    {
        var meter = new Meter("eCommerce-S16-" + Guid.NewGuid());
        var ghi = new BoGhi(meter);          // 🔑 nghe TRƯỚC khi tạo instrument
        var metrics = new eCommerceMetrics(meter);

        return (meter, metrics, ghi);
    }

    [Fact]
    public async Task Lock_ttl_exceeded_PHAT_khi_lease_MAT_THAT()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var giu = new GiuGia();
        var khoa = new KhoaGia { Giu = giu };
        var wrapper = new DoThoiGianGiuKhoa(khoa, metrics);

        var handle = await wrapper.TryAcquireAsync("inventory:host:kho:sku-1", TimeSpan.FromSeconds(5));

        Assert.NotNull(handle);
        Assert.DoesNotContain(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTtlExceededTotal);

        // 🚨 Sự kiện THẬT: lease mất trước khi trả handle (hết TTL / mất Redis / tiến trình treo).
        giu.MatKhoa();

        var doDuoc = Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTtlExceededTotal);
        Assert.Equal(1L, doDuoc.GiaTri);
        Assert.Equal("inventory", doDuoc.Nhan[eCommerceMetricTags.Module]);

        // Mất khoá lặp lại (token chỉ huỷ một lần) vẫn chỉ đếm MỘT lần cho handle này.
        giu.MatKhoa();
        Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTtlExceededTotal);

        await handle!.DisposeAsync();
        Assert.True(giu.DaTra);
    }

    [Fact]
    public async Task Lock_ttl_exceeded_KHONG_PHAT_khi_tra_khoa_binh_thuong()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var giu = new GiuGia();
        var wrapper = new DoThoiGianGiuKhoa(new KhoaGia { Giu = giu }, metrics);

        var handle = await wrapper.TryAcquireAsync("catalog:slug", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        await handle!.DisposeAsync();

        Assert.True(giu.DaTra);
        Assert.DoesNotContain(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTtlExceededTotal);
    }

    [Fact]
    public async Task Lock_ttl_exceeded_KHONG_PHAT_neu_lease_mat_SAU_khi_da_tra()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var giu = new GiuGia();
        var wrapper = new DoThoiGianGiuKhoa(new KhoaGia { Giu = giu }, metrics);

        var handle = await wrapper.TryAcquireAsync("payment:create:abc", TimeSpan.FromSeconds(10));
        await handle!.DisposeAsync();

        // Sau khi đã trả khoá, token mất khoá không còn thuộc "thao tác đang giữ khoá" ⇒ không đếm.
        giu.MatKhoa();

        Assert.DoesNotContain(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTtlExceededTotal);
    }

    [Fact]
    public async Task Wrapper_lock_KHONG_doi_hanh_vi_khi_khong_gianh_duoc_khoa()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var khoa = new KhoaGia { Giu = null };
        var wrapper = new DoThoiGianGiuKhoa(khoa, metrics);

        var handle = await wrapper.TryAcquireAsync("sales:return:x", TimeSpan.FromSeconds(5));

        Assert.Null(handle);
        Assert.Equal("sales:return:x", khoa.TenDaXin);
        Assert.Empty(ghi.Ghi);
    }

    // ── (2) logging_failures_total ──────────────────────────────────────────────────────────────

    private sealed class ProviderNem : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new LoggerNem(categoryName);

        public void Dispose()
        {
        }

        private sealed class LoggerNem(string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => throw new InvalidOperationException($"sink hong: {categoryName}");
        }
    }

    private sealed class ProviderGhiNhan : ILoggerProvider
    {
        public List<string> Dong { get; } = [];

        public ILogger CreateLogger(string categoryName) => new LoggerGhiNhan(Dong, categoryName);

        public void Dispose()
        {
        }

        private sealed class LoggerGhiNhan(List<string> dong, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => dong.Add($"{categoryName}:{formatter(state, exception)}");
        }
    }

    [Fact]
    public void Logging_failures_PHAT_khi_sink_NEM__va_KHONG_mat_thong_diep()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var duPhong = new List<(string ThongDiep, Exception? Loi)>();
        var provider = new DemLoiGhiLogProvider(
            new ProviderNem(),
            metrics,
            (thongDiep, loi) => duPhong.Add((thongDiep, loi)));

        var logger = provider.CreateLogger("eCommerce.Tests.S16");
        logger.LogInformation("THONG-DIEP-KHONG-DUOC-MAT {Ma}", "K9-S16");

        var doDuoc = Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LoggingFailuresTotal);
        Assert.Equal(1L, doDuoc.GiaTri);

        var dongDuPhong = Assert.Single(duPhong);
        Assert.Contains("THONG-DIEP-KHONG-DUOC-MAT", dongDuPhong.ThongDiep, StringComparison.Ordinal);
        Assert.Contains("K9-S16", dongDuPhong.ThongDiep, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(dongDuPhong.Loi);
    }

    [Fact]
    public void Logging_failures_KHONG_PHAT_khi_sink_binh_thuong()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var duPhong = new List<(string ThongDiep, Exception? Loi)>();
        var trong = new ProviderGhiNhan();
        var provider = new DemLoiGhiLogProvider(
            trong,
            metrics,
            (thongDiep, loi) => duPhong.Add((thongDiep, loi)));

        provider.CreateLogger("eCommerce.Tests.S16").LogInformation("DI-QUA-BINH-THUONG");

        Assert.DoesNotContain(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LoggingFailuresTotal);
        Assert.Empty(duPhong);
        Assert.Contains("DI-QUA-BINH-THUONG", Assert.Single(trong.Dong), StringComparison.Ordinal);
    }

    [Fact]
    public void Dang_ky_logger_boc_DUNG_provider_trong_DI__va_van_phat_metric()
    {
        var (meter, metrics, ghi) = TaoBoDo();
        using var _ = meter;
        using var __ = ghi;

        var services = new ServiceCollection();
        services.AddSingleton(metrics);
        services.AddLogging(b => b.AddProvider(new ProviderNem()));
        services.ThemDemLoiGhiLog();

        using var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("eCommerce.Tests.S16.DI");

        logger.LogWarning("DI-CUNG-PHAI-BI-BAT");

        // Dòng dự phòng mặc định ra Console.Error; ở test ta không đọc được stderr toàn cục,
        // nên khẳng định qua metric + việc LoggerFactory thật đã đi qua wrapper.
        Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LoggingFailuresTotal);
    }
}
