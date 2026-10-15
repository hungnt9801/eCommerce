using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp;

namespace eCommerce.Catalog;

/// <summary>
/// **Bộ sinh tổ hợp biến thể** — `CAT-002` **D5/D6/D7** + BR-009/010/011/016.
///
/// 🚨 **HÀM THUẦN** (không DB, không DI) nên test được trực tiếp — và đây là chỗ **đáng test nhất** vì:
///   • `AttributeHash` là **khoá đối chiếu** để biết tổ hợp nào đã tồn tại (BR-013: sinh lại **phải giữ**
///     `Id`/`Sku` cũ, **cấm** xoá-rồi-tạo-lại) ⇒ hash **lệch** ⇒ hoặc tạo trùng, hoặc coi hai tổ hợp khác nhau
///     là một (bán sai hàng).
///   • Đếm tổ hợp là **tích Descartes** ⇒ **nổ theo cấp số nhân** (5 màu × 6 size × 4 chất liệu = 120;
///     thêm 3 kiểu dáng = 360) ⇒ phải chặn **TRƯỚC khi ghi** (D6).
///
/// 📌 **BR-011**: `AttributeHash` **chỉ** được sinh ở **đây** — **cấm** ghép chuỗi tại chỗ ở tầng khác
/// (bài học "khoá lệch" của `XC-003` F1: hai nơi sinh khoá ⇒ khoá không khớp ⇒ **không bảo vệ gì**).
/// 📌 **BR-016**: hash **ổn định với thứ tự chọn** ⇒ luôn **sắp xếp theo `AttributeId`** trước khi băm.
/// </summary>
public static class VariantCombinationGenerator
{
    /// <summary>Ngưỡng tổ hợp mặc định khi sản phẩm/danh mục **không** ghi đè (`CAT-002` D6).</summary>
    public const int NguongToHopMacDinh = 200;

    /// <summary>Một tổ hợp: các cặp (thuộc tính, giá trị) **đã sắp xếp theo `AttributeId`** + hash.</summary>
    public sealed record ToHop(
        IReadOnlyList<(Guid AttributeId, Guid AttributeValueId)> GiaTri,
        string AttributeHash);

    /// <summary>
    /// Sinh **tích Descartes** của các giá trị đã chọn.
    /// </summary>
    /// <param name="giaTriTheoThuocTinh">
    /// Mỗi phần tử là một thuộc tính `Variant` và **danh sách giá trị** của nó (≥ 1 giá trị).
    /// </param>
    /// <param name="nguongToiDa">Ngưỡng tổ hợp (`MaxVariantCombinations`) — vượt ⇒ **NÉM** `Catalog:0009`.</param>
    public static IReadOnlyList<ToHop> Sinh(
        IReadOnlyList<(Guid AttributeId, IReadOnlyList<Guid> AttributeValueIds)> giaTriTheoThuocTinh,
        int nguongToiDa = NguongToHopMacDinh)
    {
        ArgumentNullException.ThrowIfNull(giaTriTheoThuocTinh);

        if (nguongToiDa <= 0)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe)
                .WithData("lyDo", "Ngưỡng tổ hợp phải > 0.")
                .WithData("nguong", nguongToiDa);
        }

        // Mỗi thuộc tính PHẢI có ≥ 1 giá trị: thuộc tính `Variant` không có giá trị nào thì **không sinh được**
        // tổ hợp nào cho nó — im lặng bỏ qua sẽ tạo ra biến thể **thiếu trục** (khách không chọn được).
        foreach (var (attributeId, giaTri) in giaTriTheoThuocTinh)
        {
            if (giaTri is null || giaTri.Count == 0)
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                    .WithData("lyDo", "Thuộc tính 'Variant' không có giá trị nào ⇒ không sinh được tổ hợp.")
                    .WithData("attributeId", attributeId);
            }
        }

        // 🚨 ĐẾM TRƯỚC KHI SINH (D6): dùng `long` để phép nhân không tràn `int` khi có nhiều trục
        // (tràn `int` sẽ cho số ÂM ⇒ vượt qua kiểm tra ngưỡng ⇒ treo request + phình DB — đúng thứ D6 muốn chặn).
        long soToHop = 1;

        foreach (var (_, giaTri) in giaTriTheoThuocTinh)
        {
            soToHop *= giaTri.Count;

            if (soToHop > nguongToiDa)
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe)
                    .WithData("soToHop", soToHop)
                    .WithData("nguong", nguongToiDa)
                    .WithData("soTruc", giaTriTheoThuocTinh.Count);
            }
        }

        if (giaTriTheoThuocTinh.Count == 0)
        {
            // Sản phẩm **đơn giản** (không có thuộc tính `Variant`) ⇒ KHÔNG sinh tổ hợp, **không phải lỗi**
            // (`CAT-002` §12: "Sản phẩm không có thuộc tính Variant ⇒ KHÔNG sinh tổ hợp").
            return Array.Empty<ToHop>();
        }

        // Sắp xếp trục theo `AttributeId` để hash ỔN ĐỊNH (BR-016) bất kể thứ tự người dùng chọn.
        var truc = giaTriTheoThuocTinh
            .OrderBy(x => x.AttributeId)
            .ToList();

        var ketQua = new List<ToHop>((int)soToHop);

        // Tích Descartes bằng đệ quy (số trục nhỏ; số tổ hợp đã bị chặn bởi ngưỡng ở trên).
        void Duyet(int chiSo, List<(Guid, Guid)> dangChon)
        {
            if (chiSo == truc.Count)
            {
                // Sắp xếp theo `AttributeId` (đã sắp ở `truc`, nhưng sắp lại cho chắc — hash là khoá đối chiếu).
                var sapXep = dangChon
                    .OrderBy(x => x.Item1)
                    .Select(x => (x.Item1, x.Item2))
                    .ToList();

                ketQua.Add(new ToHop(sapXep, TinhAttributeHash(sapXep)));
                return;
            }

            var (attributeId, giaTri) = truc[chiSo];

            foreach (var valueId in giaTri.OrderBy(x => x))
            {
                dangChon.Add((attributeId, valueId));
                Duyet(chiSo + 1, dangChon);
                dangChon.RemoveAt(dangChon.Count - 1);
            }
        }

        Duyet(0, new List<(Guid, Guid)>());

        return ketQua;
    }

    /// <summary>
    /// 🚨 **HÀM BĂM DUY NHẤT CỦA DOMAIN** (BR-011) — mọi nơi cần `AttributeHash` **phải** gọi hàm này.
    ///
    /// Cách làm: sắp xếp cặp (thuộc tính, giá trị) theo `AttributeId`, ghép `attrId:valueId` bằng `|`,
    /// rồi **SHA-256** ⇒ chuỗi hex **64 ký tự** (khớp `varchar(128)` ở DB).
    ///
    /// ⚠️ **Vì sao băm thay vì ghép chuỗi thô:** chuỗi ghép thô dài theo số trục và **có thể vượt** độ dài cột;
    /// băm cho độ dài **cố định** và so sánh **ổn định**. Đánh đổi: **không đọc được bằng mắt** — nên test
    /// khẳng định tính **ổn định theo thứ tự** (BR-016) và **khác nhau khi khác tập giá trị** là bắt buộc.
    /// </summary>
    public static string TinhAttributeHash(IEnumerable<(Guid AttributeId, Guid AttributeValueId)> giaTri)
    {
        ArgumentNullException.ThrowIfNull(giaTri);

        var sapXep = giaTri
            .OrderBy(x => x.AttributeId)
            .ThenBy(x => x.AttributeValueId)
            .Select(x => string.Create(
                CultureInfo.InvariantCulture, $"{x.AttributeId:N}:{x.AttributeValueId:N}"))
            .ToList();

        var chuoi = string.Join('|', sapXep);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(chuoi));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
