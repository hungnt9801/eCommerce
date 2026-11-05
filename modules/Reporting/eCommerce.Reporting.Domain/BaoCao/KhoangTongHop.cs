using Volo.Abp;

namespace eCommerce.Reporting;

/// <summary>
/// **KHOẢNG NGÀY CỦA MỘT LƯỢT TỔNG HỢP** — hàm thuần quyết định *"chạy từ ngày nào đến ngày nào"*.
///
/// 🔑 **HAI ĐƯỜNG, MỘT LUẬT:**
/// <list type="number">
///   <item>**Chạy theo MỐC** (job định kỳ): bắt đầu từ **ngày sau mốc đã tổng hợp** ⇒ mỗi lượt chỉ làm
///     phần mới. Mốc `null` (chưa từng chạy) ⇒ **lùi 30 ngày** để lần đầu vẫn có dữ liệu hiển thị, thay vì
///     màn hình rỗng mà không ai biết vì sao.</item>
///   <item>**Chạy theo KHOẢNG** (backfill): người vận hành chỉ định `từ`/`đến` — **luôn thắng** mốc, vì
///     mục đích của backfill là *"làm lại một khoảng đã biết"*.</item>
/// </list>
///
/// 🚨 **TRẦN 366 NGÀY/LƯỢT** — không phải để tiện: một lượt quá dài giữ transaction + khoá lâu, và nếu
/// **hỏng giữa đường** thì không biết đã ghi tới đâu. Chia khoảng là cách duy nhất còn kiểm soát được.
/// 📌 Vượt trần ⇒ **NÉM** (`Reports:0003`) chứ **không** tự cắt im lặng: tự cắt làm người dùng tưởng
/// *"đã backfill hết"* trong khi nửa sau chưa chạy.
/// </summary>
public static class KhoangTongHop
{
    /// <summary>Số ngày tối đa của MỘT lượt (366 = trọn một năm nhuận).</summary>
    public const int SoNgayToiDa = 366;

    /// <summary>Số ngày lùi lại ở lần chạy ĐẦU TIÊN khi chưa có mốc.</summary>
    public const int SoNgayLanDau = 30;

    /// <summary>
    /// Xác định khoảng cần tổng hợp.
    /// </summary>
    /// <param name="ngayDaTongHop">Ngày lớn nhất đã tổng hợp (từ `RptWatermarks`); `null` = chưa từng chạy.</param>
    /// <param name="tuYeuCau">Ngày bắt đầu do người gọi chỉ định (backfill); `null` = chạy theo mốc.</param>
    /// <param name="denYeuCau">Ngày kết thúc do người gọi chỉ định; `null` = hôm nay.</param>
    /// <param name="homNay">Hôm nay theo **ngày kinh doanh** (app service truyền `IClock` đã quy đổi).</param>
    public static KhoangNgay XacDinh(
        DateOnly? ngayDaTongHop,
        DateOnly? tuYeuCau,
        DateOnly? denYeuCau,
        DateOnly homNay)
    {
        var den = denYeuCau ?? homNay;

        // 📌 Ngày TƯƠNG LAI không có chứng từ nào ⇒ kẹp về hôm nay. Ghi rõ: đây là KẸP, không phải lỗi —
        // job chạy lúc 00:01 với `den = hôm qua + 1` vẫn đúng, và người gọi không phải tự tính.
        if (den > homNay)
        {
            den = homNay;
        }

        DateOnly tu;

        if (tuYeuCau is not null)
        {
            tu = tuYeuCau.Value;
        }
        else if (ngayDaTongHop is null)
        {
            tu = den.AddDays(-(SoNgayLanDau - 1));
        }
        else
        {
            tu = ngayDaTongHop.Value.AddDays(1);

            if (tu > den)
            {
                // 🚨 **RỖNG LÀ KẾT QUẢ HỢP LỆ**, không phải lỗi: job định kỳ chạy 10 phút/lần mà dữ liệu
                // mới nhất đã tổng hợp ⇒ lượt này không có việc. Trả `Rong = true` để người gọi **không**
                // ghi mốc mới và **không** chạy truy vấn rỗng.
                return new KhoangNgay(den.AddDays(1), den, Rong: true);
            }
        }

        if (tu > den)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe)
                .WithData("tu", tu)
                .WithData("den", den)
                .WithData("lyDo", "Ngày bắt đầu PHẢI ≤ ngày kết thúc.");
        }

        var soNgay = den.DayNumber - tu.DayNumber + 1;

        if (soNgay > SoNgayToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangQuaDai)
                .WithData("tu", tu)
                .WithData("den", den)
                .WithData("soNgay", soNgay)
                .WithData("toiDa", SoNgayToiDa)
                .WithData("lyDo", "Chia khoảng rồi chạy lại — tự cắt ngắn sẽ làm bạn tưởng đã backfill hết.");
        }

        return new KhoangNgay(tu, den, Rong: false);
    }
}

/// <summary>Khoảng ngày đã xác định. `Rong = true` ⇒ **không có việc** (không phải lỗi).</summary>
public sealed record KhoangNgay(DateOnly Tu, DateOnly Den, bool Rong)
{
    /// <summary>Số ngày (0 khi rỗng).</summary>
    public int SoNgay => Rong ? 0 : Den.DayNumber - Tu.DayNumber + 1;

    /// <summary>Liệt kê từng ngày — dùng cho job ghi theo ngày.</summary>
    public IEnumerable<DateOnly> CacNgay()
    {
        if (Rong)
        {
            yield break;
        }

        for (var ngay = Tu; ngay <= Den; ngay = ngay.AddDays(1))
        {
            yield return ngay;
        }
    }
}
