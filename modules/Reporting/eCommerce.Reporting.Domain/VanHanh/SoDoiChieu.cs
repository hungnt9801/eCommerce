namespace eCommerce.Reporting;

/// <summary>Một dòng lệch do <see cref="SoDoiChieu.KiemNgay"/> phát hiện (chưa ghi DB).</summary>
public sealed record LechSo(
    RepProjection Projection,
    RepMismatchKind Kind,
    string FactKey,
    decimal? ExpectedValue,
    decimal? ActualValue)
{
    /// <summary>`Actual − Expected` khi cả hai vế có số; ngược lại NULL.</summary>
    public decimal? Delta => ExpectedValue.HasValue && ActualValue.HasValue
        ? ActualValue.Value - ExpectedValue.Value
        : null;
}

/// <summary>
/// **ĐỐI CHIẾU SỐ NGUỒN VỚI READ-MODEL** — hàm THUẦN (`REP-001` §3.5, BR-012).
///
/// 🚨 **SO TIỀN BẰNG `Delta`** — `decimal.Compare(delta, 0m) != 0`; **CẤM** `==`/`!=` trên `decimal`
/// (`XC-001` BR-003). Số đếm (`int`) thì dùng `!=` bình thường.
/// 📌 Hàm **không** đọc DB/`IClock` ⇒ test được bằng tay, hành vi tất định.
/// </summary>
public static class SoDoiChieu
{
    /// <summary>
    /// So **một ngày** × **một phép chiếu**: số dòng, số chứng từ, doanh thu (và ảnh tồn kho nếu có).
    /// Trả **rỗng** khi khớp; mỗi vế lệch ⇒ đúng **một** <see cref="LechSo"/>.
    /// </summary>
    public static IReadOnlyList<LechSo> KiemNgay(
        RepProjection projection,
        DateOnly ngay,
        decimal doanhThuNguon,
        decimal doanhThuReadModel,
        int soDongNguon,
        int soDongReadModel,
        int soChungTuNguon,
        int soChungTuReadModel,
        decimal? anhTonKhoNguon = null,
        decimal? anhTonKhoReadModel = null)
    {
        var khoa = $"{projection.MaChuoi()}:{ngay:yyyy-MM-dd}";
        var ket = new List<LechSo>();

        if (soDongNguon != soDongReadModel)
        {
            ket.Add(new LechSo(projection, RepMismatchKind.SoDong, khoa, soDongNguon, soDongReadModel));
        }

        if (soChungTuNguon != soChungTuReadModel)
        {
            ket.Add(new LechSo(projection, RepMismatchKind.SoChungTu, khoa, soChungTuNguon, soChungTuReadModel));
        }

        // 🚨 Tiền so bằng Delta, KHÔNG `==`.
        if (decimal.Compare(doanhThuReadModel - doanhThuNguon, 0m) != 0)
        {
            ket.Add(new LechSo(projection, RepMismatchKind.DoanhThu, khoa, doanhThuNguon, doanhThuReadModel));
        }

        if (anhTonKhoNguon.HasValue && anhTonKhoReadModel.HasValue
            && decimal.Compare(anhTonKhoReadModel.Value - anhTonKhoNguon.Value, 0m) != 0)
        {
            ket.Add(new LechSo(projection, RepMismatchKind.AnhTonKho, khoa, anhTonKhoNguon, anhTonKhoReadModel));
        }

        return ket;
    }
}
