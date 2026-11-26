using eCommerce.Reporting;
using Volo.Abp;

namespace eCommerce.Tests.Reporting;

/// <summary>
/// `D-275` — **quyết định ghi `RepRunLog` cho lượt tổng hợp định kỳ** (hàm thuần) + 3 ca bắt buộc
/// (có việc / rỗng / lỗi). 📌 Worker thật (`DoWorkAsync`) **chưa** có harness cho module `Reporting` —
/// QA dựng khuôn `D-248`; các ca dưới đây kiểm phần logic thuần, **không** đánh dấu phủ `DoWorkAsync`.
/// </summary>
public class NhatKyAggregateTests
{
    private static readonly DateOnly Tu = new(2026, 9, 1);
    private static readonly DateOnly Den = new(2026, 9, 3);
    private static readonly DateOnly HomNay = new(2026, 9, 15);
    private static readonly DateTime Luc = new(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);

    // ── Ca 1: CÓ VIỆC ⇒ ghi dòng BanHang + TonKho ──────────────────────────────────────────────────────

    [Fact]
    public void CoViec__ghi_BanHang_va_TonKho()
    {
        var ketQua = new KetQuaTongHopDto
        {
            TuNgay = Tu,
            DenNgay = Den,
            SoNgay = 3,
            SoDongSuKien = 5,
            SoDongTongHop = 2,
            SoAnhTonKho = 7,
            Rong = false
        };

        var quyetDinh = NhatKyAggregate.QuyetDinh(ketQua, HomNay);

        Assert.Equal(2, quyetDinh.Count);

        var banHang = quyetDinh.Single(x => x.Projection == RepProjection.BanHang);
        Assert.Equal(Tu, banHang.FromDate);
        Assert.Equal(Den, banHang.ToDate);
        Assert.Equal(3, banHang.WindowsTotal);
        Assert.Equal(5, banHang.RowsWritten);

        var tonKho = quyetDinh.Single(x => x.Projection == RepProjection.TonKho);
        Assert.Equal(HomNay, tonKho.FromDate);
        Assert.Equal(HomNay, tonKho.ToDate);
        Assert.Equal(1, tonKho.WindowsTotal);
        Assert.Equal(7, tonKho.RowsWritten);
    }

    [Fact]
    public void CoViec__chi_TonKho_khi_chi_co_anh_ton_kho()
    {
        // 📌 Lượt Rong (watermark đã ở cuối) nhưng vẫn chụp ảnh tồn kho ⇒ ĐÚNG MỘT dòng TonKho.
        var ketQua = new KetQuaTongHopDto
        {
            TuNgay = Den.AddDays(1),
            DenNgay = Den,
            SoNgay = 0,
            SoDongSuKien = 0,
            SoDongTongHop = 0,
            SoAnhTonKho = 3,
            Rong = true
        };

        var quyetDinh = NhatKyAggregate.QuyetDinh(ketQua, HomNay);

        var dong = Assert.Single(quyetDinh);
        Assert.Equal(RepProjection.TonKho, dong.Projection);
        Assert.Equal(HomNay, dong.FromDate);
        Assert.Equal(3, dong.RowsWritten);
    }

    // ── Ca 2: RỖNG ⇒ 0 dòng ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rong__khong_ghi_dong_nao()
    {
        var khongViec = new KetQuaTongHopDto
        {
            TuNgay = Tu,
            DenNgay = Den,
            SoNgay = 3,
            SoDongSuKien = 0,
            SoDongTongHop = 0,
            SoAnhTonKho = 0,
            Rong = false
        };

        Assert.Empty(NhatKyAggregate.QuyetDinh(khongViec, HomNay));

        var rong = new KetQuaTongHopDto
        {
            TuNgay = Den.AddDays(1),
            DenNgay = Den,
            SoNgay = 0,
            Rong = true
        };

        Assert.Empty(NhatKyAggregate.QuyetDinh(rong, HomNay));
    }

    // ── Ca 3: LỖI ⇒ một dòng Failed có LastErrorCode đúng ────────────────────────────────────────────

    [Fact]
    public void Loi__ma_loi_dung_quy_tac()
    {
        var be = new BusinessException(ReportingErrorCodes.ChuyenTrangThaiKhongHopLe);
        Assert.Equal(ReportingErrorCodes.ChuyenTrangThaiKhongHopLe, NhatKyAggregate.MaLoi(be));

        // Exception KHÔNG phải BusinessException ⇒ tên kiểu (không bịa mã 00xx).
        Assert.Equal("InvalidOperationException", NhatKyAggregate.MaLoi(new InvalidOperationException()));
    }

    [Fact]
    public void Loi__dong_Failed_dung_mot_dong_va_co_LastErrorCode()
    {
        var loi = new BusinessException(ReportingErrorCodes.ConLechChuaXuLy);

        var q = new QuyetDinhNhatKy(RepProjection.BanHang, HomNay, HomNay, 1, 0);
        var run = new RepRunLog(
            Guid.NewGuid(), RepRunLog.TenJob.Aggregate, q.Projection, q.FromDate, q.ToDate,
            q.WindowsTotal, RepRunLog.LyDoHeThong.TongHopDinhKy, null,
            RepIdempotency.AggregateRun(null, q.Projection, Guid.NewGuid()));

        run.BatDau(Luc, "host");
        run.ThatBai(NhatKyAggregate.MaLoi(loi), Luc);

        Assert.Equal(RepRunStatus.Failed, run.Status);
        Assert.Equal(ReportingErrorCodes.ConLechChuaXuLy, run.LastErrorCode);
    }

    // ── Bất biến (cùng vòng mà worker chạy): có việc ⇒ Succeeded, WindowsDone == WindowsTotal ───────────

    [Fact]
    public void CoViec__chay_may_trang_thai_toi_Succeeded_bat_bien_RowsWritten()
    {
        var ketQua = new KetQuaTongHopDto
        {
            TuNgay = Tu, DenNgay = Den, SoNgay = 3, SoDongSuKien = 5, SoDongTongHop = 2, SoAnhTonKho = 0
        };

        var q = NhatKyAggregate.QuyetDinh(ketQua, HomNay).Single();

        var run = new RepRunLog(
            Guid.NewGuid(), RepRunLog.TenJob.Aggregate, q.Projection, q.FromDate, q.ToDate,
            q.WindowsTotal, RepRunLog.LyDoHeThong.TongHopDinhKy, null,
            RepIdempotency.AggregateRun(null, q.Projection, Guid.NewGuid()));

        run.BatDau(Luc, "host");

        for (var ngay = q.FromDate; ngay <= q.ToDate; ngay = ngay.AddDays(1))
        {
            run.GhiCuaSo(ngay, ngay == q.ToDate ? q.RowsWritten : 0);
        }

        run.HoanTat(Luc);

        Assert.Equal(RepRunStatus.Succeeded, run.Status);
        Assert.Equal(run.WindowsTotal, run.WindowsDone);
        Assert.Equal(5, run.RowsWritten);
        Assert.Equal(q.ToDate, run.CheckpointDate);
    }
}
