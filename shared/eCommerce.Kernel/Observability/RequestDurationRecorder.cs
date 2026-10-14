using System.Diagnostics;

namespace eCommerce.Kernel.Observability;

/// <summary>
/// **LÕI THUẦN ghi độ trễ request** — không phụ thuộc ASP.NET ⇒ **test được** mà không cần web host.
///
/// 🎯 VÌ SAO TÁCH LÕI RA KHỎI MIDDLEWARE (đây là bài học lặp lại trong dự án):
///   Middleware nằm trong project **Host** (exe) nên **test project không thể tham chiếu** (và cũng
///   **không nên** — test phụ thuộc exe là thiết kế tồi). Nếu để toàn bộ logic trong middleware thì
///   phần quan trọng nhất sẽ **không bao giờ được kiểm chứng**. Tách lõi ra Kernel ⇒ test được
///   **quyết định nhãn** và **việc ghi metric**, còn middleware chỉ còn 5 dòng chuyển tiếp.
///
/// 🚨 BẤT BIẾN CHỐNG "NỔ" CARDINALITY (`XC-005` BR-009):
///   Nhãn `route` **PHẢI** là **route template** (`/api/app/e-invoice/{id}/ky`), **KHÔNG** phải đường dẫn
///   thật (`/api/app/e-invoice/9f1c…/ky`). Đường dẫn thật chứa id ⇒ mỗi id sinh **một chuỗi thời gian
///   riêng** ⇒ **nổ** bộ nhớ hệ thống metric. Lỗi này **chỉ lộ ở môi trường thật** (nhiều id) —
///   test với 1–2 request sẽ **không thấy gì**.
/// </summary>
public static class RequestDurationRecorder
{
    /// <summary>Nhãn cho request **không khớp endpoint** nào — hằng số HỮU HẠN nên vẫn an toàn.</summary>
    public const string NhanKhongKhop = "khong-khop";

    /// <summary>Chuẩn hoá nhãn `route`: rỗng/null ⇒ hằng số hữu hạn (KHÔNG để rỗng vì rỗng vẫn là một giá trị nhãn).</summary>
    public static string TinhNhanRoute(string? routeTemplate)
        => string.IsNullOrWhiteSpace(routeTemplate) ? NhanKhongKhop : routeTemplate.Trim();

    /// <summary>
    /// Ghi độ trễ vào metric. Gọi trong `finally` để **request lỗi vẫn được đo** — nếu chỉ đo đường
    /// thành công thì biểu đồ độ trễ **thiếu toàn bộ ca lỗi/chậm**, đúng những ca cần nhìn nhất.
    /// </summary>
    public static void Ghi(
        eCommerceMetrics metrics,
        TimeSpan thoiGian,
        string phuongThuc,
        string? routeTemplate,
        int maTrangThai)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        metrics.HttpRequestDurationSeconds.Record(
            thoiGian.TotalSeconds,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Method,
                string.IsNullOrWhiteSpace(phuongThuc) ? "khong-ro" : phuongThuc.Trim().ToUpperInvariant()),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Route, TinhNhanRoute(routeTemplate)),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Status,
                maTrangThai.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Đo một thao tác bất kỳ và ghi metric (dùng cho middleware).</summary>
    public static async Task DoVaGhiAsync(
        eCommerceMetrics metrics,
        string phuongThuc,
        Func<string?> layRouteTemplate,
        Func<int> layMaTrangThai,
        Func<Task> thanhXuLy)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(thanhXuLy);

        var dongHo = Stopwatch.StartNew();

        try
        {
            await thanhXuLy();
        }
        finally
        {
            dongHo.Stop();
            Ghi(metrics, dongHo.Elapsed, phuongThuc, layRouteTemplate(), layMaTrangThai());
        }
    }
}
