using eCommerce.Tax;
using eCommerce.Tax.Classifications;
using eCommerce.Tax.RateSchedules;
using Volo.Abp;

namespace eCommerce.Tests.Tax;

/// <summary>
/// Unit test cho module <b>Tax</b> (XC-006).
///
/// 🎯 Vì sao test được TẤT CẢ ở đây mà không cần DB:
///   <see cref="TaxRateResolver.Resolve"/> là **hàm thuần**, nhận ngày cần tính qua tham số
///   (<c>at</c>) ⇒ kiểm thử được mốc chuyển mức thuế **31/12/2026 → 01/01/2027** ngay hôm nay,
///   không phải chờ tới năm 2027 (NFR-XC-006-004).
///
/// Mã test (TC-xx) khớp với bảng test case trong docs/features/XC-006-phan-loai-thue-gtgt.md.
/// </summary>
public class TaxRateResolverTests
{
    // Ngày mốc quan trọng: mức 8% chỉ có hiệu lực đến hết 31/12/2026 (NQ 204/2025/QH15).
    private static readonly DateOnly NgayCuoiCungCon8 = new(2026, 12, 31);
    private static readonly DateOnly NgayDauTienHet8 = new(2027, 1, 1);
    private static readonly DateOnly BatDauGiam = new(2025, 7, 1);

    private static readonly Guid SkuId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DanhMucCon = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DanhMucCha = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ---------------------------------------------------------------- fixtures

    private static TaxRateSchedule Schedule(
        string code, decimal rate, TaxRateKind kind, DateOnly from, DateOnly? to = null, string? reducedFrom = null)
        => new(Guid.NewGuid(), code, $"Mức {code}", rate, kind, from, to, reducedFrom);

    /// <summary>Bảng mức thuế giống seed thật (xem §11 tài liệu thiết kế).</summary>
    private static List<TaxRateSchedule> TatCaMuc()
    {
        var ratSom = new DateOnly(2020, 1, 1);
        return
        [
            Schedule("VAT0", 0m, TaxRateKind.Standard, ratSom),
            Schedule("VAT5", 5m, TaxRateKind.Standard, ratSom),
            Schedule("VAT10", 10m, TaxRateKind.Standard, ratSom),
            // ⚠️ Đây là dòng sẽ phải SỬA nếu luật gia hạn mức 8% sau 31/12/2026 (kịch bản Q6).
            Schedule("VAT8", 8m, TaxRateKind.Reduced, BatDauGiam, NgayCuoiCungCon8, reducedFrom: "VAT10")
        ];
    }

    /// <summary>Lọc mức đang hiệu lực tại ngày X — mô phỏng việc repository làm trong thực tế.</summary>
    private static List<TaxRateSchedule> HieuLucTai(List<TaxRateSchedule> all, DateOnly at)
        => all.Where(s => s.IsEffectiveAt(at)).ToList();

    private static TaxClassification PhanLoai(
        TaxClassificationTargetType type,
        Guid targetId,
        bool duocGiam,
        DateOnly from,
        DateOnly? to = null,
        TaxClassificationStatus status = TaxClassificationStatus.Active,
        int version = 1)
    {
        var c = new TaxClassification(Guid.NewGuid(), type, targetId, "VAT10", duocGiam, from, to);
        if (status != TaxClassificationStatus.Draft)
        {
            c.Approve(Guid.NewGuid(), DateTime.UtcNow);
            if (status == TaxClassificationStatus.Active)
            {
                c.ActivateIfDue(from);
            }
        }

        for (var i = 1; i < version; i++)
        {
            c.IncreaseVersion();
        }

        return c;
    }

    private static TaxRateResolution Resolve(
        IReadOnlyList<TaxClassification> classifications,
        DateOnly at,
        IReadOnlyList<Guid>? danhMuc = null,
        IReadOnlyList<TaxRateSchedule>? schedules = null)
        => TaxRateResolver.Resolve(
            SkuId,
            danhMuc ?? [DanhMucCon, DanhMucCha],
            classifications,
            schedules ?? HieuLucTai(TatCaMuc(), at),
            at);

    // ---------------------------------------------------------------- TC-02 (quan trọng nhất)

    [Fact]
    public void TC02_ngay_31_12_2026_van_con_8_phan_tram()
    {
        var phanLoai = new[] { PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam) };

        var kq = Resolve(phanLoai, NgayCuoiCungCon8);

        Assert.Equal(8m, kq.Rate);
        Assert.Equal("VAT8", kq.RateCode);
        Assert.Equal("VAT10", kq.BaseRateCode);
        Assert.True(kq.ReductionApplicable);
    }

    [Fact]
    public void TC02_tu_01_01_2027_TU_DONG_ve_10_phan_tram_khong_can_sua_code()
    {
        var phanLoai = new[] { PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam) };

        var kq = Resolve(phanLoai, NgayDauTienHet8);

        // 🎯 Không có mức Reduced nào hiệu lực ⇒ tự trả về mức cơ sở 10%.
        Assert.Equal(10m, kq.Rate);
        Assert.Equal("VAT10", kq.RateCode);

        // Phân loại KHÔNG đổi — chỉ mức thuế hết hiệu lực.
        Assert.True(kq.ReductionApplicable);
        Assert.False(kq.NeedsClassification);
    }

    // ---------------------------------------------------------------- TC-03 / TC-04

    [Fact]
    public void TC03_hang_KHONG_thuoc_PhuLuc_I_II_thi_duoc_giam_8()
    {
        var phanLoai = new[] { PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam) };

        var kq = Resolve(phanLoai, new DateOnly(2026, 6, 1));

        Assert.Equal(8m, kq.Rate);
        Assert.Equal(TaxClassificationSource.Sku, kq.Source);
        Assert.False(kq.NeedsClassification);
    }

    [Fact]
    public void TC04_hang_THUOC_PhuLuc_I_II_thi_KHONG_duoc_giam_van_10()
    {
        var phanLoai = new[] { PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: false, BatDauGiam) };

        var kq = Resolve(phanLoai, new DateOnly(2026, 6, 1));

        Assert.Equal(10m, kq.Rate);
        Assert.False(kq.ReductionApplicable);
    }

    // ---------------------------------------------------------------- TC-05 / TC-06 (thứ tự ưu tiên)

    [Fact]
    public void TC05_phan_loai_cap_SKU_thang_cap_danh_muc()
    {
        var phanLoai = new[]
        {
            PhanLoai(TaxClassificationTargetType.Category, DanhMucCon, duocGiam: true, BatDauGiam),
            PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: false, BatDauGiam)
        };

        var kq = Resolve(phanLoai, new DateOnly(2026, 6, 1));

        Assert.Equal(TaxClassificationSource.Sku, kq.Source);
        Assert.Equal(10m, kq.Rate);
    }

    [Fact]
    public void TC06_danh_muc_con_thang_danh_muc_cha()
    {
        var phanLoai = new[]
        {
            PhanLoai(TaxClassificationTargetType.Category, DanhMucCha, duocGiam: false, BatDauGiam),
            PhanLoai(TaxClassificationTargetType.Category, DanhMucCon, duocGiam: true, BatDauGiam)
        };

        var kq = Resolve(phanLoai, new DateOnly(2026, 6, 1));

        Assert.Equal(TaxClassificationSource.Category, kq.Source);
        Assert.Equal(8m, kq.Rate);
    }

    // ---------------------------------------------------------------- TC-07 (chưa phân loại)

    [Fact]
    public void TC07_sku_chua_phan_loai_thi_muc_mac_dinh_va_can_phan_loai()
    {
        var kq = Resolve([], new DateOnly(2026, 6, 1));

        Assert.Equal(10m, kq.Rate);
        Assert.Equal("VAT10", kq.RateCode);
        Assert.False(kq.ReductionApplicable);
        Assert.Equal(TaxClassificationSource.Unclassified, kq.Source);
        Assert.True(kq.NeedsClassification);
        Assert.Null(kq.ClassificationId);
    }

    // ---------------------------------------------------------------- trạng thái không được dùng để tính thuế

    [Fact]
    public void Ban_ghi_Draft_KHONG_anh_huong_tinh_thue()
    {
        var phanLoai = new[]
        {
            PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam,
                status: TaxClassificationStatus.Draft)
        };

        // Draft bị service lọc bỏ trước khi gọi Resolve — mô phỏng đúng hành vi đó:
        var dungDuoc = phanLoai.Where(c => c.IsUsableAt(new DateOnly(2026, 6, 1))).ToList();

        var kq = Resolve(dungDuoc, new DateOnly(2026, 6, 1));

        Assert.True(kq.NeedsClassification);
        Assert.Equal(10m, kq.Rate);
    }

    [Fact]
    public void Ban_ghi_ngoai_khung_hieu_luc_KHONG_duoc_dung()
    {
        var phanLoai = new[]
        {
            PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true,
                from: new DateOnly(2025, 7, 1), to: new DateOnly(2025, 12, 31))
        };

        var dungDuoc = phanLoai.Where(c => c.IsUsableAt(new DateOnly(2026, 6, 1))).ToList();
        var kq = Resolve(dungDuoc, new DateOnly(2026, 6, 1));

        Assert.True(kq.NeedsClassification);
    }

    // ---------------------------------------------------------------- TC-21 / TC-22 (BR-011 — Q6)

    [Fact]
    public void TC21_CAM_lui_hieu_luc_ve_qua_khu()
    {
        var vat8 = Schedule("VAT8", 8m, TaxRateKind.Reduced, BatDauGiam, NgayCuoiCungCon8, "VAT10");

        var ex = Assert.Throws<BusinessException>(() =>
            vat8.ChangeEffectiveTo(new DateOnly(2026, 6, 30), today: new DateOnly(2026, 10, 12)));

        Assert.Equal(TaxErrorCodes.BackdatedEffectiveDate, ex.Code);
    }

    [Fact]
    public void TC22_gia_han_hop_le_thi_thanh_cong()
    {
        var vat8 = Schedule("VAT8", 8m, TaxRateKind.Reduced, BatDauGiam, NgayCuoiCungCon8, "VAT10");

        vat8.ChangeEffectiveTo(new DateOnly(2027, 12, 31), today: new DateOnly(2026, 10, 12));

        Assert.Equal(new DateOnly(2027, 12, 31), vat8.EffectiveTo);

        // Sau khi gia hạn, ngày 01/01/2027 lại được áp 8%.
        var kq = Resolve(
            [PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam)],
            NgayDauTienHet8,
            schedules: HieuLucTai(
                [
                    Schedule("VAT10", 10m, TaxRateKind.Standard, new DateOnly(2020, 1, 1)),
                    vat8
                ],
                NgayDauTienHet8));

        Assert.Equal(8m, kq.Rate);
    }

    // ---------------------------------------------------------------- state machine phải NÉM LỖI

    [Fact]
    public void Duyet_ban_ghi_da_duyet_thi_nem_loi()
    {
        var c = PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam,
            status: TaxClassificationStatus.Approved);

        var ex = Assert.Throws<BusinessException>(() => c.Approve(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal(TaxErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public void Sua_ban_ghi_da_Active_thi_nem_loi()
    {
        var c = PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam,
            status: TaxClassificationStatus.Active);

        var ex = Assert.Throws<BusinessException>(() =>
            c.UpdateDraft("VAT10", reductionApplicable: false, BatDauGiam, null, null));

        Assert.Equal(TaxErrorCodes.CannotModifyActive, ex.Code);
    }

    [Fact]
    public void Huy_ban_ghi_da_Active_thi_nem_loi()
    {
        var c = PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam,
            status: TaxClassificationStatus.Active);

        var ex = Assert.Throws<BusinessException>(() => c.Cancel());
        Assert.Equal(TaxErrorCodes.CannotModifyActive, ex.Code);
    }

    [Fact]
    public void Supersede_khi_ban_thay_the_chua_duyet_thi_nem_loi()
    {
        var c = PhanLoai(TaxClassificationTargetType.Sku, SkuId, duocGiam: true, BatDauGiam,
            status: TaxClassificationStatus.Active);

        var ex = Assert.Throws<BusinessException>(() =>
            c.Supersede(new DateOnly(2026, 12, 31), replacementIsApproved: false));

        Assert.Equal(TaxErrorCodes.ReplacementNotApproved, ex.Code);
    }

    [Fact]
    public void Approve_roi_ActivateIfDue_chuyen_sang_Active()
    {
        var c = new TaxClassification(
            Guid.NewGuid(), TaxClassificationTargetType.Sku, SkuId, "VAT10", true, BatDauGiam);

        Assert.Equal(TaxClassificationStatus.Draft, c.Status);

        c.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(TaxClassificationStatus.Approved, c.Status);

        Assert.False(c.ActivateIfDue(BatDauGiam.AddDays(-1)));   // chưa tới ngày hiệu lực
        Assert.True(c.ActivateIfDue(BatDauGiam));                 // tới ngày hiệu lực
        Assert.Equal(TaxClassificationStatus.Active, c.Status);
    }

    // ---------------------------------------------------------------- không có mức thuế nào hiệu lực

    [Fact]
    public void Khong_co_muc_thue_nao_hieu_luc_thi_nem_loi_0002()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            Resolve([], new DateOnly(2019, 1, 1), schedules: []));

        Assert.Equal(TaxErrorCodes.NoEffectiveTaxRate, ex.Code);
    }
}
