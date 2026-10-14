using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;

namespace eCommerce.Tests.Observability;

/// <summary>
/// Test cho **lõi ghi độ trễ request** (`RequestDurationRecorder`).
///
/// 🎯 Bất biến quan trọng nhất: **nhãn `route` phải HỮU HẠN**. Dùng đường dẫn thật
/// (`/…/9f1c-…/ky`) làm nhãn ⇒ mỗi id sinh một chuỗi thời gian riêng ⇒ **nổ** hệ thống metric.
/// Lỗi này **chỉ lộ ở môi trường thật** (nhiều id); test 1–2 request sẽ không thấy gì ⇒ phải khoá bằng test
/// trên **quyết định nhãn**, không phải bằng cách "chạy thử xem có sao không".
/// </summary>
public class RequestDurationRecorderTests
{
    private sealed class BoGhi : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Ten, double GiaTri, Dictionary<string, object?> Nhan)> Ghi { get; } = [];

        public BoGhi(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
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

    private static (eCommerceMetrics Metrics, BoGhi Ghi) Tao()
    {
        var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        return (new eCommerceMetrics(meter), new BoGhi(meter));
    }

    // ── 🚨 Chống "nổ" cardinality ──────────────────────────────────────────

    [Fact]
    public void Route_rong_thi_dung_hang_so_HUU_HAN()
    {
        Assert.Equal(RequestDurationRecorder.NhanKhongKhop, RequestDurationRecorder.TinhNhanRoute(null));
        Assert.Equal(RequestDurationRecorder.NhanKhongKhop, RequestDurationRecorder.TinhNhanRoute("   "));
    }

    [Fact]
    public void Route_template_duoc_giu_nguyen()
    {
        Assert.Equal("/api/app/e-invoice/{id}/ky",
            RequestDurationRecorder.TinhNhanRoute("/api/app/e-invoice/{id}/ky"));
    }

    [Fact]
    public void Ghi_dung_template_chu_KHONG_phai_duong_dan_that()
    {
        var (metrics, ghi) = Tao();

        // Middleware truyền TEMPLATE; đây chính là điều phải giữ đúng.
        RequestDurationRecorder.Ghi(metrics, TimeSpan.FromMilliseconds(12), "post",
            "/api/app/e-invoice/{id}/ky", 200);

        var doDuoc = Assert.Single(ghi.Ghi.Where(x => x.Ten == eCommerceMetricNames.HttpRequestDurationSeconds));
        var route = doDuoc.Nhan[eCommerceMetricTags.Route]?.ToString();

        Assert.Equal("/api/app/e-invoice/{id}/ky", route);
        Assert.DoesNotContain("9f1c", route);
        Assert.Equal("POST", doDuoc.Nhan[eCommerceMetricTags.Method]);   // chuẩn hoá chữ HOA
        Assert.Equal("200", doDuoc.Nhan[eCommerceMetricTags.Status]);
        Assert.True(doDuoc.GiaTri > 0);
    }

    [Fact]
    public void Phuong_thuc_rong_thi_dung_hang_so_huu_han()
    {
        var (metrics, ghi) = Tao();

        RequestDurationRecorder.Ghi(metrics, TimeSpan.FromMilliseconds(1), "  ", "/x", 200);

        var doDuoc = Assert.Single(ghi.Ghi.Where(x => x.Ten == eCommerceMetricNames.HttpRequestDurationSeconds));
        Assert.Equal("khong-ro", doDuoc.Nhan[eCommerceMetricTags.Method]);
    }

    // ── Đo cả khi LỖI ──────────────────────────────────────────────────────

    [Fact]
    public async Task Do_va_ghi_thi_LÔI_van_duoc_do()
    {
        // 🔒 Nếu chỉ đo đường thành công thì biểu đồ độ trễ THIẾU toàn bộ ca lỗi — đúng ca cần nhìn nhất.
        var (metrics, ghi) = Tao();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RequestDurationRecorder.DoVaGhiAsync(
                metrics, "GET", () => "/x", () => 500,
                () => throw new InvalidOperationException("lỗi mô phỏng")));

        var doDuoc = Assert.Single(ghi.Ghi.Where(x => x.Ten == eCommerceMetricNames.HttpRequestDurationSeconds));
        Assert.Equal("500", doDuoc.Nhan[eCommerceMetricTags.Status]);
    }

    [Fact]
    public async Task Do_va_ghi_duong_THANH_CONG_phat_metric_binh_thuong()
    {
        var (metrics, ghi) = Tao();

        await RequestDurationRecorder.DoVaGhiAsync(metrics, "get", () => "/api/app/x", () => 200,
            () => Task.CompletedTask);

        var doDuoc = Assert.Single(ghi.Ghi.Where(x => x.Ten == eCommerceMetricNames.HttpRequestDurationSeconds));
        Assert.Equal("200", doDuoc.Nhan[eCommerceMetricTags.Status]);
        Assert.Equal("GET", doDuoc.Nhan[eCommerceMetricTags.Method]);
    }

    [Fact]
    public async Task Do_va_ghi_khong_khop_endpoint_thi_nhan_huu_han()
    {
        var (metrics, ghi) = Tao();

        await RequestDurationRecorder.DoVaGhiAsync(metrics, "GET", () => null, () => 404, () => Task.CompletedTask);

        var doDuoc = Assert.Single(ghi.Ghi.Where(x => x.Ten == eCommerceMetricNames.HttpRequestDurationSeconds));
        Assert.Equal(RequestDurationRecorder.NhanKhongKhop, doDuoc.Nhan[eCommerceMetricTags.Route]);
    }
}
