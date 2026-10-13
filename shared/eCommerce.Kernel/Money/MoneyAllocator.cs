using Volo.Abp;

namespace eCommerce.Kernel.Money;

/// <summary>
/// **Phân bổ một khoản tiền thành nhiều phần mà KHÔNG mất một đồng nào.**
///
/// 🚨 VÌ SAO PHẢI CÓ (đây là nguồn sai lệch kinh điển):
///   Giảm giá 100.000 đ cho 3 dòng hàng ⇒ chia đều `33.333,33…` mỗi dòng. Nếu mỗi dòng tự làm tròn
///   thì tổng 3 dòng = `99.999` ⇒ **lệch 1 đồng** so với số đã hứa với khách. Sai lệch này **tích luỹ**
///   qua nhiều đơn và làm **đối soát không bao giờ khớp**.
///
/// ✅ Cách làm: **phần dư lớn nhất** (largest remainder) —
///   1. chia chính xác theo tỷ lệ,
///   2. **cắt xuống** từng phần (floor),
///   3. phần còn dư chia cho các phần có **phần lẻ lớn nhất**, mỗi phần **một đơn vị nhỏ nhất**.
///   Kết quả: **tổng các phần LUÔN bằng đúng khoản gốc** (bất biến được test kiểm).
///
/// 📌 Đây cũng là cách các hệ thống kế toán chuẩn phân bổ chiết khấu/phí theo dòng hàng.
/// </summary>
public static class MoneyAllocator
{
    /// <summary>
    /// Phân bổ <paramref name="tong"/> theo <paramref name="tyLe"/> (trọng số, không cần cộng bằng 1).
    ///
    /// 🔒 Bất biến: `Kết quả.Sum() == tong` — **luôn đúng**, kể cả khi tỷ lệ không chia hết.
    /// </summary>
    /// <param name="tong">Khoản tiền cần phân bổ (có thể âm — ví dụ phân bổ chiết khấu).</param>
    /// <param name="tyLe">Trọng số từng phần; phải **không âm** và **tổng > 0**.</param>
    /// <param name="policy">Chính sách làm tròn (quyết định đơn vị nhỏ nhất).</param>
    public static IReadOnlyList<Money> PhanBo(Money tong, IReadOnlyList<decimal> tyLe, IRoundingPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(tong);
        ArgumentNullException.ThrowIfNull(tyLe);

        if (tyLe.Count == 0)
        {
            return [];
        }

        if (tyLe.Any(t => t < 0m))
        {
            throw new BusinessException(MoneyErrorCodes.InvalidAllocationRatio)
                .WithData("ly do", "có trọng số âm");
        }

        var tongTyLe = tyLe.Sum();

        if (tongTyLe <= 0m)
        {
            throw new BusinessException(MoneyErrorCodes.InvalidAllocationRatio)
                .WithData("ly do", "tổng trọng số bằng 0");
        }

        // Đơn vị nhỏ nhất của loại tiền (VND ⇒ 1; USD ⇒ 0,01) để phân bổ phần dư.
        var soChuSo = tong.Currency.DecimalDigits;
        var donViNhoNhat = soChuSo == 0 ? 1m : 1m / (decimal)Math.Pow(10, soChuSo);

        // 1) Chia chính xác theo tỷ lệ.
        var chinhXac = new decimal[tyLe.Count];
        for (var i = 0; i < tyLe.Count; i++)
        {
            chinhXac[i] = tong.Amount * tyLe[i] / tongTyLe;
        }

        // 2) Cắt xuống theo đơn vị nhỏ nhất.
        var phan = new decimal[tyLe.Count];
        for (var i = 0; i < tyLe.Count; i++)
        {
            phan[i] = Math.Floor(chinhXac[i] / donViNhoNhat) * donViNhoNhat;
        }

        // 3) Phần dư ⇒ chia cho các phần có phần lẻ LỚN NHẤT, mỗi phần một đơn vị nhỏ nhất.
        var daChia = phan.Sum();
        var conDu = tong.Amount - daChia;
        var soDonViConDu = (int)decimal.Round(conDu / donViNhoNhat, 0, MidpointRounding.AwayFromZero);

        if (soDonViConDu != 0)
        {
            var buoc = soDonViConDu > 0 ? donViNhoNhat : -donViNhoNhat;
            var soLan = Math.Abs(soDonViConDu);

            var thuTu = Enumerable.Range(0, tyLe.Count)
                .OrderByDescending(i => chinhXac[i] - phan[i])
                .ThenBy(i => i)          // ổn định: cùng phần lẻ thì phần có chỉ số nhỏ nhận trước
                .ToArray();

            for (var k = 0; k < soLan; k++)
            {
                phan[thuTu[k % thuTu.Length]] += buoc;
            }
        }

        var ketQua = phan.Select(x => Money.Cua(x, tong.Currency)).ToArray();

        // 4) Tự kiểm bất biến ngay tại chỗ — sai thì NÉM LỖI, không im lặng trả số sai.
        var tongKetQua = ketQua.Aggregate(Money.Zero(tong.Currency), (acc, m) => acc + m);

        if (tongKetQua != tong)
        {
            throw new BusinessException(MoneyErrorCodes.InvalidAmount)
                .WithData("ly do", "phân bổ làm lệch tổng")
                .WithData("tong", tong.ToString())
                .WithData("tongKetQua", tongKetQua.ToString());
        }

        return ketQua;
    }
}
