using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace eCommerce.Reporting;

/// <summary>Một dòng tổng hợp đưa vào <see cref="BamKyChot"/> (từ `RptAggDailySales`).</summary>
public sealed record DongKy(
    DateOnly Ngay,
    NguonBaoCao Nguon,
    int SoChungTu,
    int SoDong,
    decimal SoLuong,
    decimal DoanhThu);

/// <summary>
/// **BĂM ẢNH CHỤP KỲ CHỐT** (`SnapshotHash`) — `REP-001` §3.5.
///
/// 🔑 **VÌ SAO BĂM:** chốt kỳ lưu **một chuỗi đại diện** cho toàn bộ số liệu của kỳ; lần xuất sau so lại
/// được là *"số liệu kỳ này còn y nguyên không"*. 🔒 Hàm **THUẦN, TẤT ĐỊNH**: sắp theo `(Ngày, Nguồn)`,
/// định dạng `InvariantCulture`, không phụ thuộc `Now`/DB.
/// 📌 **KHÔNG** đưa `RptWatermarks`/`RptFactInventorySnapshots` vào hash — ảnh tồn kho không tất định theo
/// lịch sử (`D-47` ⑦) ⇒ chỉ dòng tổng hợp + đếm fact doanh thu.
/// </summary>
public static class BamKyChot
{
    /// <summary>Băm danh sách dòng tổng hợp + tổng số dòng fact ⇒ **64 ký tự hex thường**.</summary>
    public static string Bam(IEnumerable<DongKy> dong, long soDongFact)
    {
        ArgumentNullException.ThrowIfNull(dong);

        var sb = new StringBuilder();

        foreach (var d in dong.OrderBy(x => x.Ngay).ThenBy(x => x.Nguon))
        {
            sb.Append(d.Ngay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
              .Append((int)d.Nguon).Append('|')
              .Append(d.SoChungTu).Append('|')
              .Append(d.SoDong).Append('|')
              .Append(d.SoLuong.ToString("F3", CultureInfo.InvariantCulture)).Append('|')
              .Append(d.DoanhThu.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
        }

        sb.Append("F|").Append(soDongFact);

        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));

        return Convert.ToHexString(bam).ToLowerInvariant();
    }
}
