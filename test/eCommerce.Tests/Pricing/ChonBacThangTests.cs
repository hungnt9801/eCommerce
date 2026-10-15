using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test **chọn bậc thang** (`PRC-002` **ADR-02**) — hàm **THUẦN**.
///
/// 🚨 **VÌ SAO QUAN TRỌNG:** chọn bậc sai ⇒ **bán sai giá** hoặc **ném lỗi làm chết checkout**:
///   • lấy "bậc đầu tiên khớp" ⇒ kết quả **phụ thuộc thứ tự DB trả về** ⇒ **cùng giỏ, hai lần tính, hai giá**;
///   • hai bậc cùng `MinQuantity` mà **không ném** ⇒ không ai biết bậc nào đúng.
/// </summary>
public class ChonBacThangTests
{
    private static ChonBacThang.Bac Bac(int min, decimal gia, string nguon = "n")
        => new(min, Money.Vnd(gia), nguon);

    [Fact]
    public void Chon_bac_co_MinQuantity_LON_NHAT_khong_vuot_quantity()
    {
        var bac = new List<ChonBacThang.Bac> { Bac(1, 100_000), Bac(10, 90_000), Bac(50, 80_000) };

        Assert.Equal(100_000m, ChonBacThang.Chon(1, bac)!.DonGia.Amount);
        Assert.Equal(100_000m, ChonBacThang.Chon(9, bac)!.DonGia.Amount);
        Assert.Equal(90_000m, ChonBacThang.Chon(10, bac)!.DonGia.Amount);
        Assert.Equal(90_000m, ChonBacThang.Chon(49, bac)!.DonGia.Amount);
        Assert.Equal(80_000m, ChonBacThang.Chon(50, bac)!.DonGia.Amount);
        Assert.Equal(80_000m, ChonBacThang.Chon(10_000, bac)!.DonGia.Amount);
    }

    [Fact]
    public void Khong_bac_nao_du_dieu_kien_thi_tra_NULL__KHONG_phai_loi()
    {
        // quantity nhỏ hơn mọi bậc ⇒ rơi về giá cơ sở (pipeline B5 xử lý) — **không** phải lỗi.
        Assert.Null(ChonBacThang.Chon(3, [Bac(10, 90_000)]));
    }

    [Fact]
    public void HAI_bac_CUNG_MinQuantity_thi_NEM_0008__KHONG_chon_bua()
    {
        var loi = Assert.Throws<BusinessException>(() => ChonBacThang.Chon(
            20, [Bac(10, 90_000, "a"), Bac(10, 85_000, "b")], sku: "SKU-1"));

        Assert.Equal(PricingErrorCodes.BacThangTrungMinQuantity, loi.Code);
        Assert.Equal(10, loi.Data["minQuantity"]);
        Assert.Equal("SKU-1", loi.Data["sku"]);
    }

    [Fact]
    public void Trung_MinQuantity_o_bac_KHONG_du_dieu_kien_thi_KHONG_nem()
    {
        // 🚨 Quan trọng: chỉ nhánh THẮNG mới cần xác định. Hai bậc trùng ở mức `quantity` KHÔNG chạm tới
        // thì không ảnh hưởng kết quả — ném ở đây là **chặn oan** (khách mua 5 cái mà đơn vị dữ liệu ở bậc 10).
        var bac = new List<ChonBacThang.Bac> { Bac(1, 100_000), Bac(10, 90_000, "a"), Bac(10, 85_000, "b") };

        Assert.Equal(100_000m, ChonBacThang.Chon(5, bac)!.DonGia.Amount);
        Assert.Equal(PricingErrorCodes.BacThangTrungMinQuantity,
            Assert.Throws<BusinessException>(() => ChonBacThang.Chon(10, bac)).Code);
    }

    [Fact]
    public void Quantity_nho_hon_1_thi_NEM_LOI()
    {
        Assert.Throws<BusinessException>(() => ChonBacThang.Chon(0, [Bac(1, 100_000)]));
    }

    [Fact]
    public void KiemTraBoBac_phat_hien_TRUNG_MinQuantity()
    {
        var loi = ChonBacThang.KiemTraBoBac([Bac(1, 100), Bac(5, 90), Bac(5, 80)], Currency.Vnd);

        Assert.Contains(loi, x => x.Contains("NHIỀU bậc cùng", StringComparison.Ordinal));
    }

    [Fact]
    public void KiemTraBoBac_phat_hien_LECH_loai_tien_va_gia_AM()
    {
        var loi = ChonBacThang.KiemTraBoBac(
            [new ChonBacThang.Bac(1, Money.Usd(10), "usd"), new ChonBacThang.Bac(2, Money.Vnd(-5), "am")],
            Currency.Vnd);

        Assert.Contains(loi, x => x.Contains("lệch loại tiền", StringComparison.Ordinal));
        Assert.Contains(loi, x => x.Contains("ÂM", StringComparison.Ordinal));
    }

    [Fact]
    public void Bo_bac_HOP_LE_thi_khong_co_loi()
    {
        Assert.Empty(ChonBacThang.KiemTraBoBac([Bac(1, 100), Bac(10, 90)], Currency.Vnd));
    }
}

/// <summary>Test **vòng đời bộ bậc thang** (`PRC-002` ADR-10/ADR-11 + append-only).</summary>
public class TierPriceSetTests
{
    private static TierPriceSet Tao(PriceListScope scope = PriceListScope.All, string? kenh = null)
        => new(Guid.NewGuid(), "BT-1", "Bộ bậc 1", scope, Currency.Vnd,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), kenh);

    [Fact]
    public void Pham_vi_theo_kenh_ma_THIEU_kenh_thi_NEM_LOI()
    {
        // Bất biến DB không bảo vệ: bộ bậc "theo kênh" không gắn kênh ⇒ **không bao giờ khớp** ⇒ âm thầm rơi
        // về giá cơ sở ⇒ bán sai giá mà không có lỗi nào.
        Assert.Throws<BusinessException>(() => Tao(PriceListScope.Channel));
    }

    [Fact]
    public void PhatHanh_khi_CHUA_co_bac_nao_thi_NEM_LOI()
    {
        var bo = Tao();

        Assert.Equal(PricingErrorCodes.BangGiaThieuDong, Assert.Throws<BusinessException>(() => bo.PhatHanh(0)).Code);
    }

    [Fact]
    public void PhatHanh_roi_KichHoat__va_KichHoat_truoc_ngay_tra_FALSE()
    {
        var bo = Tao();
        bo.PhatHanh(2);
        Assert.Equal(PriceListStatus.Approved, bo.Status);

        Assert.False(bo.KichHoatNeuToiNgay(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(bo.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PriceListStatus.Active, bo.Status);
    }

    [Fact]
    public void Append_only__bo_dang_Active_thi_KHONG_sua_duoc_noi_dung()
    {
        var bo = Tao();
        bo.PhatHanh(1);
        bo.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PricingErrorCodes.BangGiaActiveKhongSuaDuoc,
            Assert.Throws<BusinessException>(() => bo.YeuCauChoPhepSuaNoiDung()).Code);
    }

    [Fact]
    public void TierPrice_gan_DUNG_TierPriceSetId__bai_hoc_PRC001_L2()
    {
        // 🚨 Bài học `PRC-001` §17.2 **L2**: constructor chỉ KIỂM TRA mà QUÊN GÁN ⇒ FK nổ (hoặc tệ hơn:
        // hỏng dữ liệu im lặng nếu không có FK). Test này khoá việc GÁN.
        var idBo = Guid.NewGuid();

        var bac = new TierPrice(Guid.NewGuid(), idBo, "SKU-1", 10, Money.Vnd(90_000), Currency.Vnd);

        Assert.Equal(idBo, bac.TierPriceSetId);
        Assert.Equal("SKU-1", bac.Sku);
        Assert.Equal(10, bac.MinQuantity);
        Assert.Equal(90_000m, bac.Amount.Amount);
        Assert.Equal("VND", bac.CurrencyCode);
    }

    [Fact]
    public void TierPrice_MinQuantity_nho_hon_1_thi_NEM_LOI()
    {
        Assert.Throws<BusinessException>(() => new TierPrice(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 0, Money.Vnd(1), Currency.Vnd));
    }

    [Fact]
    public void TierPrice_lech_loai_tien_thi_NEM_LOI()
    {
        Assert.Equal(PricingErrorCodes.LechLoaiTien, Assert.Throws<BusinessException>(() => new TierPrice(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 1, Money.Usd(10), Currency.Vnd)).Code);
    }
}
