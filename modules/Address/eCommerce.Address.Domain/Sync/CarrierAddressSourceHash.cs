using System.Security.Cryptography;
using System.Text;

namespace eCommerce.Address.Sync;

/// <summary>
/// **Băm danh mục mã địa chỉ hãng ĐÃ CHUẨN HOÁ** (`ADDR-002` §5.3, BR-004).
///
/// 🎯 Đầu vào: `CarrierCode.Trim().ToUpperInvariant()` + mỗi `DonViDiaChiHang` của danh mục **đã qua**
/// <see cref="CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa"/> (không còn phần tử `null`, mã rỗng hay
/// mã trùng) ⇒ mỗi dòng <c>"{CarrierLevel Trim().ToUpperInvariant()}\t{CarrierAddressCode}\t{CarrierAddressName?.Trim() ?? ""}"</c>,
/// dòng sắp theo `(CarrierLevel, CarrierAddressCode)` `Ordinal`, nối `"\n"`, UTF-8; SHA-256 → hex **thường**.
///
/// 🔒 **ỔN ĐỊNH** qua lần chạy lại cùng danh mục, **KHÔNG** phụ thuộc thứ tự dòng nguồn (TC-04);
/// **CẤM** `Guid.NewGuid()`/thời điểm/`Source`. Tên đổi ⇒ hash đổi (đúng nghĩa "danh mục nguồn khác đi").
/// Hash **không** phải khoá idempotency (không có unique index, `D-83`).
/// </summary>
public static class CarrierAddressSourceHash
{
    /// <summary>Độ dài cột `SourceHash` (`varchar(64)`).</summary>
    public const int DoDaiHash = 64;

    public static string Tinh(string carrierCode, IEnumerable<DonViDiaChiHang>? daChuanHoa)
    {
        var dong = (daChuanHoa ?? [])
            .Select(x => new
            {
                Cap = (x.CarrierLevel ?? string.Empty).Trim().ToUpperInvariant(),
                Ma = (x.CarrierAddressCode ?? string.Empty).Trim(),
                Ten = x.CarrierAddressName?.Trim() ?? string.Empty
            })
            .OrderBy(x => x.Cap, StringComparer.Ordinal)
            .ThenBy(x => x.Ma, StringComparer.Ordinal)
            .Select(x => string.Join('\t', x.Cap, x.Ma, x.Ten));

        var noiDung = string.Join(
            '\n',
            new[] { (carrierCode ?? string.Empty).Trim().ToUpperInvariant() }.Concat(dong));

        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(noiDung));

        return Convert.ToHexString(bam).ToLowerInvariant();
    }
}
