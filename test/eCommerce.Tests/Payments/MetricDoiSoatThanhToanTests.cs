using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;
using eCommerce.PaymentHub.Payments;

namespace eCommerce.Tests.Payments;

/// <summary>
/// 📊 **PHÁT METRIC CHO ĐỐI SOÁT THANH TOÁN** (`REC-001` mục 7).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO CẦN TEST CHO MỘT HÀM "CHỈ GỌI `.Add()`":**
///
/// Đây là **chỗ duy nhất** mà quyết định đối soát biến thành **con số trên biểu đồ**. ⚠️ Và sai ở đây thì
/// **ÂM THẦM TUYỆT ĐỐI**: 🔒 **không** exception, **không** log, **không** test nào đỏ —
/// 📌 chỉ là **con số sai**, và *"cảnh báo dựa trên con số sai"* còn **tệ hơn không có cảnh báo**
/// (người ta **tin** nó).
///
/// 🚨 **BA CÁCH SAI CỤ THỂ mà lớp test này khoá lại:**
/// <list type="number">
///   <item>Phát counter cho **`KhongDoi`** ⇒ biểu đồ *"số lần đối soát"* **tăng đều** dù **không có gì đổi**
///     ⇒ ⚠️ trông như *"hoạt động bình thường"*;</item>
///   <item>**Gộp** `GhiDaThuTien` và `GhiThatBai` vào **cùng một nhãn** ⇒ 🔑 mất khả năng phân biệt
///     *"phát hiện TIỀN ĐÃ VÀO"* (đáng lo: IPN mất) với *"phát hiện giao dịch hỏng"* (bình thường);</item>
///   <item>Nhãn **ngoài** `eCommerceMetricTags.Allowed` ⇒ vi phạm `XC-005` BR-009 và `TC-07` sẽ đỏ ở
///     **chỗ khác** (⚠️ khó lần ra).</item>
/// </list>
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class MetricDoiSoatThanhToanTests
{
    /// <summary>Bắt các lần phát metric của **một** meter riêng (không đụng meter toàn cục).</summary>
    private sealed class Ghi : IDisposable
    {
        private readonly MeterListener _listener;

        public List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)> DaGhi { get; } = [];

        public Ghi(Meter meter)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (ReferenceEquals(instrument.Meter, meter))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var nhan = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var t in tags)
                {
                    nhan[t.Key] = t.Value;
                }

                DaGhi.Add((instrument.Name, value, nhan));
            });

            _listener.Start();
        }

        /// <summary>Doc gauge (observable) — phai goi tuong minh, khac counter.</summary>
        public void RecordObservable() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();
    }

    [Theory]
    [InlineData(LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien, "succeeded")]
    [InlineData(LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai, "failed")]
    public void QuyetDinh_CO_DOI__phat_counter_DUNG_nhan(LuatDoiSoatThanhToan.QuyetDinh quyetDinh, string nhanMongDoi)
    {
        // 🔑 Nhãn `result` phải **TÁCH** `succeeded` khỏi `failed` — xem lý do (2) ở chú thích lớp.
        using var meter = new Meter("eCommerce-Test-DoiSoat-" + Guid.NewGuid());
        using var ghi = new Ghi(meter);
        var metrics = new eCommerceMetrics(meter);

        MetricDoiSoatThanhToan.Ghi(metrics, quyetDinh);

        var doDuoc = Assert.Single(ghi.DaGhi, x => x.Ten == eCommerceMetricNames.PaymentReconChangedTotal);
        Assert.Equal(1L, doDuoc.GiaTri);
        Assert.Equal(nhanMongDoi, doDuoc.Nhan[eCommerceMetricTags.Result]);
    }

    [Fact]
    public void QuyetDinh_KHONG_DOI__KHONG_phat_counter()
    {
        // 🚨 **CÁCH SAI (1) — CA QUAN TRỌNG NHẤT CỦA FILE.** `KhongDoi` gồm **HAI** ca rất khác nhau
        // (đã ở trạng thái cuối, hoặc cổng *"chưa biết"*). ⚠️ Nếu phát counter ở đây thì biểu đồ
        // *"số lần đối soát"* **tăng đều** dù **không có gì đổi** ⇒ 🔒 trông như *"hoạt động bình thường"*,
        // trong khi sự thật có thể là **cổng chưa cấu hình** và **KHÔNG KIỂM ĐƯỢC GÌ**.
        using var meter = new Meter("eCommerce-Test-DoiSoat-" + Guid.NewGuid());
        using var ghi = new Ghi(meter);
        var metrics = new eCommerceMetrics(meter);

        MetricDoiSoatThanhToan.Ghi(metrics, LuatDoiSoatThanhToan.QuyetDinh.KhongDoi);

        Assert.DoesNotContain(ghi.DaGhi, x => x.Ten == eCommerceMetricNames.PaymentReconChangedTotal);
    }

    [Fact]
    public void Metrics_NULL__khong_nem()
    {
        // 🔒 Metric là **TUỲ CHỌN** (cùng khuôn `VnPayPaymentProvider`). ⚠️ Nếu ném thì thiếu đăng ký metric
        // sẽ làm **job nền NỔ lúc khởi động** ⇒ job **không bao giờ chạy** mà lỗi trỏ vào chỗ **không liên quan**.
        var loi = Record.Exception(() =>
        {
            foreach (var q in Enum.GetValues<LuatDoiSoatThanhToan.QuyetDinh>())
            {
                MetricDoiSoatThanhToan.Ghi(null, q);
            }
        });

        Assert.Null(loi);
    }

    [Fact]
    public void MOI_quyet_dinh__deu_xu_ly_duoc__khong_roi_vao_nhanh_la()
    {
        // 🔒 **LƯỚI BAO PHỦ:** quét **mọi** giá trị `QuyetDinh`. 📌 Bắt được: giá trị enum **mới** thêm sau
        // này rơi vào `default` và **âm thầm** không phát metric nào.
        Assert.Equal(3, Enum.GetValues<LuatDoiSoatThanhToan.QuyetDinh>().Length);

        using var meter = new Meter("eCommerce-Test-DoiSoat-" + Guid.NewGuid());
        using var ghi = new Ghi(meter);
        var metrics = new eCommerceMetrics(meter);

        var soLanPhat = new Dictionary<LuatDoiSoatThanhToan.QuyetDinh, int>();

        foreach (var q in Enum.GetValues<LuatDoiSoatThanhToan.QuyetDinh>())
        {
            var truoc = ghi.DaGhi.Count;
            MetricDoiSoatThanhToan.Ghi(metrics, q);
            soLanPhat[q] = ghi.DaGhi.Count - truoc;
        }

        // 🔒 Bất biến: **ĐÚNG HAI** quyết định phát metric, và chúng là hai quyết định **GHI**.
        Assert.Equal(1, soLanPhat[LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien]);
        Assert.Equal(1, soLanPhat[LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai]);
        Assert.Equal(0, soLanPhat[LuatDoiSoatThanhToan.QuyetDinh.KhongDoi]);
    }

    [Fact]
    public void Moi_nhan_phat_ra__deu_nam_trong_danh_sach_CHO_PHEP()
    {
        // ⚠️ Cách sai (3): nhãn ngoài `eCommerceMetricTags.Allowed` vi phạm `XC-005` BR-009 và sẽ làm
        // `TC-07` đỏ ở **chỗ khác** (khó lần ra). 🔒 Kiểm ngay tại **nguồn phát**.
        using var meter = new Meter("eCommerce-Test-DoiSoat-" + Guid.NewGuid());
        using var ghi = new Ghi(meter);
        var metrics = new eCommerceMetrics(meter);

        MetricDoiSoatThanhToan.Ghi(metrics, LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien);
        MetricDoiSoatThanhToan.Ghi(metrics, LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai);

        Assert.NotEmpty(ghi.DaGhi);

        var tatCaNhan = ghi.DaGhi.SelectMany(x => x.Nhan.Keys).Distinct(StringComparer.Ordinal).ToList();

        Assert.All(tatCaNhan, k => Assert.Contains(k, eCommerceMetricTags.Allowed));
        Assert.All(tatCaNhan, k => Assert.DoesNotContain(k, eCommerceMetricTags.Forbidden));
    }

    [Fact]
    public void Gauge_KHONG_KIEM_DUOC__phat_DUNG_gia_tri()
    {
        // 🔑 Gauge `payment_recon_unresolved_total` là **con số để GẮN CẢNH BÁO** (cách hỏng mà `REC-001`
        // sinh ra để chống). 📌 Ở đây chỉ kiểm **dây dẫn**: ghi vào thì đọc ra **đúng** giá trị đó.
        using var meter = new Meter("eCommerce-Test-DoiSoat-" + Guid.NewGuid());
        using var ghi = new Ghi(meter);
        var metrics = new eCommerceMetrics(meter);

        metrics.SetPaymentReconUnresolved(7);
        ghi.RecordObservable();

        var doDuoc = Assert.Single(ghi.DaGhi, x => x.Ten == eCommerceMetricNames.PaymentReconUnresolved);
        Assert.Equal(7L, doDuoc.GiaTri);
    }
}
