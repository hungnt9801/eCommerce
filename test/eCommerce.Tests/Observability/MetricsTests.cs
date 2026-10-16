using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;

namespace eCommerce.Tests.Observability;

/// <summary>
/// Test cho **bảng đăng ký metric** — hiện thực hoá `TC-06` và `TC-07` của `XC-005`.
///
/// 🎯 Vì sao quan trọng: 12 metric từng bị **"nợ"** (XC-002/XC-003 yêu cầu nhưng **không có nơi phát**).
/// Test này chốt lại: **phát ra ĐÚNG 12 tên đã đăng ký**, không thiếu, không thừa, và **không có nhãn
/// cardinality cao**. Nếu ai đổi tên metric ở nơi phát mà quên cập nhật registry ⇒ CI đỏ.
///
/// Dùng `MeterListener` (có sẵn trong BCL) ⇒ **không cần gói NuGet nào**.
/// </summary>
public class MetricsTests
{
    /// <summary>Thu thập tên các instrument thực sự được tạo trên một `Meter`.</summary>
    private static List<string> LayTenInstrumentThat(Meter meter)
    {
        var ten = new List<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                ten.Add(instrument.Name);
            }
        };
        listener.Start();

        // Tạo metric SAU khi listener đã chạy để bắt được sự kiện InstrumentPublished.
        _ = new eCommerceMetrics(meter);

        return ten;
    }

    [Fact]
    public void TC06_phat_ra_DUNG_bang_metric_da_dang_ky()
    {
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var thucTe = LayTenInstrumentThat(meter);

        Assert.Equal(
            eCommerceMetricNames.All.OrderBy(x => x, StringComparer.Ordinal),
            thucTe.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void TC06_khong_co_metric_phat_ra_ngoai_bang_dang_ky()
    {
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var thucTe = LayTenInstrumentThat(meter);

        var ngoaiRegistry = thucTe.Except(eCommerceMetricNames.All).ToList();

        Assert.True(ngoaiRegistry.Count == 0,
            "Metric phát ra nhưng CHƯA đăng ký ở XC-005 §5.1:\n" + string.Join("\n", ngoaiRegistry));
    }

    /// <summary>
    /// 🔒 **Bảng đăng ký KHÔNG trùng tên** — và ⚠️ **KHÔNG đếm cứng số lượng nữa**.
    ///
    /// 📌 **VÌ SAO BỎ `Assert.Equal(12, …)`:** nó biến **mỗi** lần thêm metric hợp lệ thành một ca **ĐỎ**
    /// ⇒ 🔑 người sửa sẽ **đổi số** mà **không** suy nghĩ gì. 📌 Một con số cứng chỉ có giá trị **một lần**
    /// (lúc viết), sau đó nó là **rào cản** chứ không phải **lưới an toàn**.
    /// ✅ Thay bằng **bất biến thật**: **không trùng** + **không rỗng** + **đối chiếu CHIỀU NGƯỢC** (ca dưới).
    /// </summary>
    [Fact]
    public void Bang_dang_ky_KHONG_trung_ten_va_KHONG_rong()
    {
        Assert.NotEmpty(eCommerceMetricNames.All);
        Assert.Equal(eCommerceMetricNames.All.Count, eCommerceMetricNames.All.Distinct().Count());
    }

    /// <summary>
    /// 🚨 **MỌI hằng số metric PHẢI có trong `All`** — chiều **NGƯỢC** mà trước 17/10/2026 **KHÔNG** ai kiểm.
    ///
    /// 🔑 **VÌ SAO CẦN CHIỀU NÀY (đây là lỗi THẬT đã đo được):** `InventoryReconMismatchSkus` **có** hằng số
    /// nhưng **THIẾU** trong `All` ⇒ 🔒 chú thích của `All` (*"nguồn sự thật duy nhất"*) **SAI**, và
    /// **không có test nào** phát hiện. 📌 Test cũ chỉ kiểm **một** chiều (*"mọi tên trong `All` đều hợp lệ"*)
    /// ⇒ một hằng số **bị bỏ sót** không bao giờ lộ ra.
    /// ⚠️ Hệ quả thật: metric vẫn **phát** được (vì nó được tạo bằng `CreateObservableGauge`), nhưng **không**
    /// nằm trong bảng đăng ký ⇒ công cụ/kiểm toán đọc bảng đó sẽ **không biết** nó tồn tại.
    /// </summary>
    [Fact]
    public void MOI_hang_so_metric_PHAI_co_trong_All__chieu_NGUOC()
    {
        var tatCaHangSo = typeof(eCommerceMetricNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        // 🛡️ Lưới an toàn cho CHÍNH lưới an toàn: nếu phép phản chiếu không thấy hằng số nào (đổi tên lớp,
        // đổi kiểu trường) thì ca này sẽ XANH VÔ NGHĨA.
        Assert.True(tatCaHangSo.Count >= 13,
            $"Phép phản chiếu chỉ thấy {tatCaHangSo.Count} hằng số ⇒ phép quét HỎNG (test trên vô nghĩa).");

        var thieu = tatCaHangSo.Except(eCommerceMetricNames.All, StringComparer.Ordinal).ToList();

        Assert.True(thieu.Count == 0,
            "Hằng số metric CÓ nhưng KHÔNG nằm trong `eCommerceMetricNames.All` ⇒ bảng đăng ký KHÔNG còn là "
            + "*\"nguồn sự thật duy nhất\"*:\n  " + string.Join("\n  ", thieu));
    }

    [Fact]
    public void TC07_cam_nhan_metric_cardinality_cao()
    {
        var viPham = eCommerceMetricTags.Allowed
            .Intersect(eCommerceMetricTags.Forbidden, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(viPham.Count == 0,
            "Nhãn BỊ CẤM (cardinality cao) lọt vào danh sách cho phép:\n" + string.Join("\n", viPham));
    }

    [Fact]
    public void Counter_va_Gauge_hoat_dong()
    {
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerceMetrics(meter);

        var daGhi = new List<(string Ten, double GiaTri)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            daGhi.Add((instrument.Name, value)));
        listener.Start();

        // Counter: tăng 3 lần rồi đo.
        metrics.LockTimeout.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "channel"));
        metrics.LockTimeout.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "channel"));
        metrics.LockTimeout.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "channel"));

        // Gauge: đặt giá trị rồi đọc lại qua RecordObservableInstruments.
        metrics.SetMessagingOutboxPending(42);
        listener.RecordObservableInstruments();

        Assert.Equal(3, daGhi.Count(x => x.Ten == eCommerceMetricNames.LockTimeoutTotal));
        Assert.Contains(daGhi, x => x.Ten == eCommerceMetricNames.MessagingOutboxPending && x.GiaTri == 42);
    }

    [Fact]
    public void Ten_metric_theo_dung_quy_uoc_snake_case_va_hau_to_don_vi()
    {
        foreach (var ten in eCommerceMetricNames.All)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", ten);   // snake_case, không hoa, không dấu chấm

            var laCounter = ten.EndsWith("_total", StringComparison.Ordinal);
            var laThoiGian = ten.EndsWith("_seconds", StringComparison.Ordinal);
            var laKichThuoc = ten.EndsWith("_size", StringComparison.Ordinal)
                              || ten.EndsWith("_pending", StringComparison.Ordinal)
                              || ten.EndsWith("_skus", StringComparison.Ordinal);

            Assert.True(laCounter || laThoiGian || laKichThuoc,
                $"Metric '{ten}' thiếu hậu tố đơn vị/loại (_total · _seconds · _size · _pending · _skus).");
        }
    }
}
