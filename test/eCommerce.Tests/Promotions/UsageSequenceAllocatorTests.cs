using eCommerce.Promotions.Workers;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`UsageSequence` đúng `D-295`** (`PRO-001-arch` §6.2) — phần **tách khỏi DB** để unit-test được
/// (`UsageSequenceAllocator` được thiết kế đúng vì lý do này).
///
/// Ngữ nghĩa bắt buộc:
/// <list type="bullet">
///   <item>**1 ghi + 4 thử lại** (`PromotionOptions.MaxUsageSequenceAttempts = 5`); 0 dòng/trùng số ⇒ thử số kế;</item>
///   <item>`23505` từ index KHÁC ⇒ `thuChen` NÉM ⇒ allocator **KHÔNG** bắt (không thử tiếp, không nuốt);</item>
///   <item>hết trần ⇒ trả `null` (nơi gọi ném `0104` + nhãn `usage_sequence_exhausted`);</item>
///   <item>trần khách: số phải ∈ `[1, tranTren]`, vượt ⇒ `null` ngay.</item>
/// </list>
///
/// ⚠️ **Để `W25-4` integration**: việc service đổi `null` → `0104` + `LogWarning` + metric
/// `promotion_voucher_reserved_total{result=usage_sequence_exhausted}`, và chứng minh **không dính `25P02`**
/// sau `23505` (cần `SAVEPOINT` + PostgreSQL thật).
/// </summary>
public class UsageSequenceAllocatorTests
{
    private static readonly UsageSequenceAllocator Sut = new();

    [Fact]
    public void D295_hang_so_5_lan_thu__mot_cho()
        => Assert.Equal(5, new PromotionOptions().MaxUsageSequenceAttempts);

    // ── TimSoBatDau (hàm THUẦN) ────────────────────────────────────────────────────────────────

    [Fact]
    public void BR04_TimSoBatDau_rong__so_1()
        => Assert.Equal(1, UsageSequenceAllocator.TimSoBatDau(Array.Empty<int>(), null, 0));

    [Fact]
    public void BR04_TimSoBatDau_lay_so_NHO_NHAT_con_trong__khong_phai_count_1()
    {
        // `{1,2}` đã dùng ⇒ số kế = 3 (KHÔNG gán 1 — BR-04).
        Assert.Equal(3, UsageSequenceAllocator.TimSoBatDau(new[] { 1, 2 }, null, 2));
        // Lỗ hổng `{1,3}` ⇒ số 2.
        Assert.Equal(2, UsageSequenceAllocator.TimSoBatDau(new[] { 1, 3 }, null, 2));
    }

    [Fact]
    public void BR04_TimSoBatDau_tran_khach_con_cho__trong_dai()
        => Assert.Equal(1, UsageSequenceAllocator.TimSoBatDau(Array.Empty<int>(), 5, 0));

    [Fact]
    public void BR04_TimSoBatDau_het_so_trong_tran_khach__null_nem_0104_ngay()
        => Assert.Null(UsageSequenceAllocator.TimSoBatDau(new[] { 1, 2, 3 }, 3, 3));

    // ── ThuAsync ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task D295_trung_so__thu_so_ke_tiep_roi_thanh_cong()
    {
        var daThu = new List<int>();

        var so = await Sut.ThuAsync(1, null, 5, (thu, _) =>
        {
            daThu.Add(thu);
            return Task.FromResult(thu >= 3); // số 1,2 trùng ⇒ số 3 thành công
        });

        Assert.Equal(3, so);
        Assert.Equal(new[] { 1, 2, 3 }, daThu);
    }

    [Fact]
    public async Task D295_het_tran_5_lan_thu__tra_null_va_DUNG_5_lan()
    {
        var dem = 0;

        var so = await Sut.ThuAsync(1, null, 5, (_, _) =>
        {
            dem++;
            return Task.FromResult(false); // luôn trùng số
        });

        Assert.Null(so);
        Assert.Equal(5, dem); // 1 ghi + 4 thử lại — không phải 6
    }

    [Fact]
    public async Task D295_soLanThu_nho_hon_1__coi_nhu_1()
    {
        var dem = 0;

        var so = await Sut.ThuAsync(1, null, 0, (_, _) =>
        {
            dem++;
            return Task.FromResult(false);
        });

        Assert.Null(so);
        Assert.Equal(1, dem);
    }

    [Fact]
    public async Task D295_23505_index_KHAC__nem_ngay_khong_thu_tiep()
    {
        var dem = 0;
        var loi = new BusinessException(eCommerce.Messaging.MessagingErrorCodes.PayloadConflict);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Sut.ThuAsync(1, null, 5, (_, _) =>
        {
            dem++;
            throw loi; // `thuChen` mô phỏng 23505 index khác (service đổi thành Messaging:0001)
        }));

        Assert.Same(loi, ex);
        Assert.Equal(1, dem); // KHÔNG thử số kế
    }

    [Fact]
    public async Task D295_vuot_tran_khach__dung_lai_khong_goi_them()
    {
        var daThu = new List<int>();

        var so = await Sut.ThuAsync(1, 2, 5, (thu, _) =>
        {
            daThu.Add(thu);
            return Task.FromResult(false); // 1,2 đều trùng
        });

        Assert.Null(so);
        Assert.Equal(new[] { 1, 2 }, daThu); // số 3 > trần 2 ⇒ dừng
    }

    [Fact]
    public async Task D295_da_co_so_bat_dau__bat_dau_tu_do()
    {
        var so = await Sut.ThuAsync(7, 10, 5, (thu, _) => Task.FromResult(thu == 7));

        Assert.Equal(7, so);
    }
}
