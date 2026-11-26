using Volo.Abp;

namespace eCommerce.Reporting;

/// <summary>
/// **KHOẢNG NGÀY CỦA KỲ CHỐT** — hàm THUẦN (`REP-001` §3.5).
/// 🔒 `to >= from` và ≤ **366** ngày (tái dùng trần `KhoangTongHop.SoNgayToiDa`).
/// </summary>
public static class KhoangChot
{
    /// <summary>`true` nếu khoảng hợp lệ; `false` nếu sai (dùng cho tiền kiểm không ném).</summary>
    public static bool HopLe(DateOnly fromDate, DateOnly toDate)
    {
        try
        {
            Kiem(fromDate, toDate);
            return true;
        }
        catch (BusinessException)
        {
            return false;
        }
    }

    /// <summary>Kiểm khoảng — NÉM `0002` (đảo) hoặc `0003` (quá dài).</summary>
    public static void Kiem(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe)
                .WithData("tu", fromDate)
                .WithData("den", toDate)
                .WithData("lyDo", "Ngày bắt đầu PHẢI ≤ ngày kết thúc.");
        }

        var soNgay = toDate.DayNumber - fromDate.DayNumber + 1;

        if (soNgay > KhoangTongHop.SoNgayToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangQuaDai)
                .WithData("tu", fromDate)
                .WithData("den", toDate)
                .WithData("soNgay", soNgay)
                .WithData("toiDa", KhoangTongHop.SoNgayToiDa);
        }
    }

    /// <summary>
    /// `TonKho` là ảnh **trạng thái hiện tại** ⇒ rebuild CHỈ cho `from == to == hôm nay` (ngày kinh doanh).
    /// Ngày quá khứ / projection không hỗ trợ ⇒ NÉM `KhongHoTroRebuild` = `0013` (`D-272`).
    /// </summary>
    public static void KiemRebuildTonKho(RepProjection projection, DateOnly fromDate, DateOnly toDate, DateOnly homNay)
    {
        if (projection == RepProjection.TonKho && !(fromDate == homNay && toDate == homNay))
        {
            throw new BusinessException(ReportingErrorCodes.KhongHoTroRebuild)
                .WithData("projection", projection.ToString())
                .WithData("homNay", homNay)
                .WithData("lyDo", "Ảnh tồn kho là trạng thái hiện tại — CHỈ rebuild cho hôm nay.");
        }
    }

    /// <summary>Chốt kỳ khi còn dòng lệch **mở** ⇒ NÉM `ConLechChuaXuLy` = `0014` (không chốt được).</summary>
    public static void KiemConLechChuaXuLy(int soDongMo)
    {
        if (soDongMo > 0)
        {
            throw new BusinessException(ReportingErrorCodes.ConLechChuaXuLy)
                .WithData("soDongMo", soDongMo)
                .WithData("lyDo", "Còn dòng lệch CHƯA XỬ LÝ trong kỳ — không được chốt.");
        }
    }
}
