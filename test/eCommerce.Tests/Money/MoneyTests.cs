using eCommerce.Kernel.Money;
using Volo.Abp;

// ⚠️ Namespace này CỐ Ý không đặt là `eCommerce.Tests.Money`: nếu đặt vậy thì namespace đó sẽ
// CHE MẤT class `eCommerce.Kernel.Money.Money` và toàn bộ `Money.Vnd(...)` không biên dịch được.
namespace eCommerce.Tests.KernelMoney;

/// <summary>
/// Test cho **`shared/eCommerce.Kernel`** — nền tiền tệ `XC-001` (bước đầu của **GĐ0**).
///
/// 🎯 Đây là phụ thuộc GỐC: mọi tài liệu feature (`PRC-001/002`, `SAL-002`, `REC-002`, `ACC-001`…)
/// đều ghi *"chưa có `Money`/`Kernel` trong code ⇒ chưa thể tính tiền"*. Bộ test này khoá lại các
/// **bất biến** quan trọng nhất của miền tiền tệ, đặc biệt những lỗi **IM LẶNG**:
///   1. cộng hai loại tiền khác nhau mà không báo lỗi,
///   2. phân bổ chiết khấu làm **mất một đồng**,
///   3. `decimal` giữ scale lạ (`0.10m * 100m = 10.00m`).
/// </summary>
public class MoneyTests
{
    // ── Currency ────────────────────────────────────────────────────────────

    [Fact]
    public void VND_co_0_chu_so_thap_phan_va_USD_co_2()
    {
        Assert.Equal(0, Currency.Vnd.DecimalDigits);
        Assert.Equal(2, Currency.Usd.DecimalDigits);
        Assert.Equal("VND", Currency.Vnd.Code);
    }

    [Fact]
    public void FromCode_khong_phan_biet_chu_hoa_thuong()
    {
        Assert.Same(Currency.Vnd, Currency.FromCode("vnd"));
        Assert.Same(Currency.Vnd, Currency.FromCode(" VND "));
    }

    [Fact]
    public void FromCode_loai_tien_chua_ho_tro_thi_NEM_LOI_chu_khong_im_lang_mac_dinh()
    {
        // 🚨 Nếu im lặng trả về VND thì hoá đơn ngoại tệ sẽ bị tính như VND mà không ai biết.
        var ex = Assert.Throws<BusinessException>(() => Currency.FromCode("EUR"));

        Assert.Equal(MoneyErrorCodes.UnsupportedCurrency, ex.Code);
    }

    [Fact]
    public void TryFromCode_tra_false_thay_vi_nem_loi()
    {
        Assert.False(Currency.TryFromCode("EUR", out var c));
        Assert.Null(c);
        Assert.True(Currency.TryFromCode("usd", out var usd));
        Assert.Same(Currency.Usd, usd);
    }

    // ── Phép toán & BẢO VỆ LOẠI TIỀN ────────────────────────────────────────

    [Fact]
    public void Cong_tru_cung_loai_tien()
    {
        var a = Money.Vnd(1_000_000m);
        var b = Money.Vnd(250_000m);

        Assert.Equal(1_250_000m, (a + b).Amount);
        Assert.Equal(750_000m, (a - b).Amount);
        Assert.Equal(Money.Vnd(1_250_000m), a + b);
    }

    [Fact]
    public void Cong_HAI_LOAI_TIEN_KHAC_NHAU_thi_NEM_LOI()
    {
        // 🚨 Đây là lỗi IM LẶNG nguy hiểm nhất của miền tiền tệ: cộng 100 USD với 100.000 VND.
        var ex = Assert.Throws<BusinessException>(() => Money.Vnd(100_000m) + Money.Usd(100m));

        Assert.Equal(MoneyErrorCodes.CurrencyMismatch, ex.Code);
    }

    [Fact]
    public void So_sanh_khac_loai_tien_thi_NEM_LOI()
    {
        var ex = Assert.Throws<BusinessException>(
            () => Money.Vnd(100m).CompareTo(Money.Usd(1m)));

        Assert.Equal(MoneyErrorCodes.CurrencyMismatch, ex.Code);
    }

    [Fact]
    public void Nhan_chia_va_chia_cho_0()
    {
        var donGia = Money.Vnd(120_000m);

        Assert.Equal(360_000m, (donGia * 3).Amount);
        Assert.Equal(40_000m, (donGia / 3).Amount);

        var ex = Assert.Throws<BusinessException>(() => donGia / 0m);
        Assert.Equal(MoneyErrorCodes.InvalidDivisor, ex.Code);
    }

    [Fact]
    public void Bang_nhau_kiem_ca_loai_tien_va_GetHashCode_nhat_quan()
    {
        Assert.True(Money.Vnd(100m) == Money.Vnd(100m));
        Assert.False(Money.Vnd(100m) == Money.Vnd(101m));
        Assert.False(Money.Vnd(100m) == Money.Usd(100m));
        Assert.Equal(Money.Vnd(100m).GetHashCode(), Money.Vnd(100m).GetHashCode());
    }

    [Fact]
    public void So_sanh_lon_be()
    {
        Assert.True(Money.Vnd(200m) > Money.Vnd(100m));
        Assert.True(Money.Vnd(100m) <= Money.Vnd(100m));
        Assert.True(Money.Vnd(-5m).IsNegative);
        Assert.True(Money.Vnd(0m).IsZero);
    }

    [Fact]
    public void ToString_dung_dinh_dang_invariant()
    {
        Assert.Equal("1000000 VND", Money.Vnd(1_000_000m).ToString());
    }

    // ── Làm tròn ────────────────────────────────────────────────────────────

    [Fact]
    public void Lam_tron_nua_len_theo_so_chu_so_cua_loai_tien()
    {
        var policy = new RoundHalfAwayFromZeroPolicy();

        Assert.Equal(1001m, policy.Round(Money.Vnd(1000.5m)).Amount);   // VND: 0 chữ số
        Assert.Equal(1000m, policy.Round(Money.Vnd(1000.4m)).Amount);
        Assert.Equal(1.24m, policy.Round(Money.Usd(1.235m)).Amount);    // USD: 2 chữ số
    }

    [Fact]
    public void NoRounding_giu_nguyen_gia_tri_chinh_xac()
    {
        var policy = new NoRoundingPolicy();

        Assert.Equal(1000.5m, policy.Round(Money.Vnd(1000.5m)).Amount);
    }

    // ── 🚨 PHÂN BỔ: BẤT BIẾN "KHÔNG MẤT MỘT ĐỒNG NÀO" ─────────────────────

    [Fact]
    public void PhanBo_100000_cho_3_phan_KHONG_mat_dong_nao()
    {
        // Nếu mỗi phần tự làm tròn 33.333,33 ⇒ tổng = 99.999 (LỆCH 1 ĐỒNG).
        var phan = MoneyAllocator.PhanBo(Money.Vnd(100_000m), [1m, 1m, 1m]);

        Assert.Equal(3, phan.Count);
        Assert.Equal(Money.Vnd(100_000m), phan.Aggregate(Money.Zero(Currency.Vnd), (a, b) => a + b));
        // Chênh lệch giữa phần lớn nhất và nhỏ nhất không quá 1 đơn vị nhỏ nhất.
        Assert.True(phan.Max(p => p.Amount) - phan.Min(p => p.Amount) <= 1m);
    }

    [Fact]
    public void PhanBo_theo_trong_so_khac_nhau_van_khong_mat_dong_nao()
    {
        var phan = MoneyAllocator.PhanBo(Money.Vnd(1_000_000m), [1m, 2m, 7m]);

        Assert.Equal(Money.Vnd(1_000_000m), phan.Aggregate(Money.Zero(Currency.Vnd), (a, b) => a + b));
        Assert.Equal(100_000m, phan[0].Amount);
        Assert.Equal(200_000m, phan[1].Amount);
        Assert.Equal(700_000m, phan[2].Amount);
    }

    [Fact]
    public void PhanBo_tong_AM_van_giu_bat_bien()
    {
        // Phân bổ chiết khấu (số âm) cũng phải khớp tổng.
        var phan = MoneyAllocator.PhanBo(Money.Vnd(-100_000m), [1m, 1m, 1m]);

        Assert.Equal(Money.Vnd(-100_000m), phan.Aggregate(Money.Zero(Currency.Vnd), (a, b) => a + b));
    }

    [Fact]
    public void PhanBo_USD_theo_don_vi_0_01()
    {
        var phan = MoneyAllocator.PhanBo(Money.Usd(0.10m), [1m, 1m, 1m]);

        Assert.Equal(Money.Usd(0.10m), phan.Aggregate(Money.Zero(Currency.Usd), (a, b) => a + b));
        Assert.All(phan, p => Assert.True(decimal.Round(p.Amount, 2) == p.Amount, "USD chỉ có 2 chữ số thập phân"));
    }

    [Fact]
    public void PhanBo_ti_le_am_thi_NEM_LOI()
    {
        var ex = Assert.Throws<BusinessException>(
            () => MoneyAllocator.PhanBo(Money.Vnd(100m), [1m, -1m]));

        Assert.Equal(MoneyErrorCodes.InvalidAllocationRatio, ex.Code);
    }

    [Fact]
    public void PhanBo_tong_trong_so_bang_0_thi_NEM_LOI()
    {
        var ex = Assert.Throws<BusinessException>(
            () => MoneyAllocator.PhanBo(Money.Vnd(100m), [0m, 0m]));

        Assert.Equal(MoneyErrorCodes.InvalidAllocationRatio, ex.Code);
    }

    [Fact]
    public void PhanBo_danh_sach_rong_tra_rong()
    {
        Assert.Empty(MoneyAllocator.PhanBo(Money.Vnd(100m), []));
    }

    [Fact]
    public void PhanBo_7_phan_tren_100_dong_luon_khop_tong()
    {
        // Kiểm nhiều trường hợp chia không hết để chắc bất biến không chỉ đúng ở ví dụ đẹp.
        foreach (var tong in new[] { 100m, 101m, 7m, 999_999m, 1m })
        {
            var phan = MoneyAllocator.PhanBo(Money.Vnd(tong), [1m, 1m, 1m, 1m, 1m, 1m, 1m]);

            Assert.Equal(
                Money.Vnd(tong),
                phan.Aggregate(Money.Zero(Currency.Vnd), (a, b) => a + b));
        }
    }
}
