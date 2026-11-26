using Volo.Abp;

namespace eCommerce.Reporting;

/// <summary>Một dòng `RepRunLog` mà lượt tổng hợp định kỳ cần ghi (kết quả hàm thuần).</summary>
public sealed record QuyetDinhNhatKy(
    RepProjection Projection,
    DateOnly FromDate,
    DateOnly ToDate,
    int WindowsTotal,
    long RowsWritten);

/// <summary>
/// **QUYẾT ĐỊNH GHI `RepRunLog` CHO LƯỢT TỔNG HỢP ĐỊNH KỲ** (`D-275`, giữ `D-271`) — hàm THUẦN, test được
/// không cần DB.
///
/// 🔑 **LUẬT (`D-271` + `D-275`):** lượt `Aggregate` **chỉ** ghi dòng khi **có việc** hoặc **lỗi**; lượt **rỗng**
/// **không** tạo dòng (giữ log + metric as-built). Mỗi projection một dòng: `BanHang` khi
/// `SoDongSuKien > 0 || SoDongTongHop > 0`; `TonKho` khi `SoAnhTonKho > 0` (ảnh là trạng thái hiện tại ⇒
/// `FromDate = ToDate =` ngày kinh doanh hôm nay, `WindowsTotal = 1`).
/// </summary>
public static class NhatKyAggregate
{
    /// <summary>
    /// Quyết định các dòng cần ghi cho **một lượt** tổng hợp có kết quả <paramref name="ketQua"/>.
    /// Trả **rỗng** khi lượt không có việc (⇒ 0 dòng `Aggregate`).
    /// </summary>
    public static IReadOnlyList<QuyetDinhNhatKy> QuyetDinh(KetQuaTongHopDto ketQua, DateOnly homNay)
    {
        ArgumentNullException.ThrowIfNull(ketQua);

        var ket = new List<QuyetDinhNhatKy>(2);

        // 📌 `SoNgay > 0` loại lượt Rong (`TuNgay > DenNgay`) — khi đó không có cửa sổ nào chạy.
        if (ketQua.SoNgay > 0 && (ketQua.SoDongSuKien > 0 || ketQua.SoDongTongHop > 0))
        {
            ket.Add(new QuyetDinhNhatKy(
                RepProjection.BanHang, ketQua.TuNgay, ketQua.DenNgay, ketQua.SoNgay, ketQua.SoDongSuKien));
        }

        if (ketQua.SoAnhTonKho > 0)
        {
            ket.Add(new QuyetDinhNhatKy(
                RepProjection.TonKho, homNay, homNay, 1, ketQua.SoAnhTonKho));
        }

        return ket;
    }

    /// <summary>
    /// Mã lỗi ghi vào `RepRunLogs.LastErrorCode` khi lượt hỏng: `BusinessException.Code` nếu có, ngược lại
    /// tên kiểu exception. 🔒 **KHÔNG** bịa mã mới ngoài dải `0010–0017`.
    /// </summary>
    public static string MaLoi(Exception loi)
    {
        ArgumentNullException.ThrowIfNull(loi);

        return loi is BusinessException be && !string.IsNullOrWhiteSpace(be.Code)
            ? be.Code
            : loi.GetType().Name;
    }
}
