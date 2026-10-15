using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using Volo.Abp;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test hàm **THUẦN** giải giá cơ sở 4 cấp (`PRC-001` D7/BR-006/BR-009) + lọc bậc số lượng (A3)
/// và **máy trạng thái** bảng giá (D4/D12/BR-004/005).
///
/// 🚨 **VÌ SAO BỘ TEST NÀY QUAN TRỌNG:** đây là chỗ quyết định **bán với giá nào**. Ba lỗi dưới đây đều
/// **không có lỗi biên dịch** và **không exception**:
///   1. chọn giá theo "cái nào mới nhất" ⇒ hai lần tính **hai giá**;
///   2. cùng cấp + cùng `Priority` mà **chọn bừa** ⇒ kết quả phụ thuộc thứ tự duyệt;
///   3. trộn loại tiền ⇒ sai 25.000 lần.
/// </summary>
public class GiaCoSoResolverTests
{
    private static UngVienGia Uv(CapGia cap, decimal tien, int uuTien = 0, string nguon = "n")
        => new(cap, Money.Vnd(tien), uuTien, nguon);

    [Fact]
    public void Cau_1_THANG_cau_2_3_4__thu_tu_cap_CO_DINH()
    {
        var kq = GiaCoSoResolver.Giai(
        [
            Uv(CapGia.GiaNiemYet, 500_000, nguon: "niemyet"),
            Uv(CapGia.BangGia, 400_000, nguon: "banggia"),
            Uv(CapGia.GiaTheoKenh, 300_000, nguon: "kenh"),
            Uv(CapGia.GiaTheoShop, 200_000, nguon: "shop")
        ], Currency.Vnd);

        Assert.Equal(CapGia.GiaTheoShop, kq.Cap);
        Assert.Equal(200_000m, kq.Amount.Amount);
        Assert.Equal(4, kq.TatCaUngVien.Count);   // ADR-03: phải trả HẾT ứng viên để kiểm chứng được

        // `Money` KHÔNG có `==` trên `decimal` trần — so qua `Money` (BR-002).
        Assert.Equal(Money.Vnd(200_000), kq.Amount);
    }

    [Fact]
    public void Cau_2_thang_khi_khong_co_cau_1()
    {
        var kq = GiaCoSoResolver.Giai(
            [Uv(CapGia.GiaNiemYet, 500_000), Uv(CapGia.GiaTheoKenh, 300_000)], Currency.Vnd);

        Assert.Equal(CapGia.GiaTheoKenh, kq.Cap);
        Assert.Equal(300_000m, kq.Amount.Amount);
    }

    [Fact]
    public void Trong_CUNG_cap_thi_Priority_LON_HON_thang()
    {
        var kq = GiaCoSoResolver.Giai(
        [
            Uv(CapGia.BangGia, 100_000, uuTien: 1, nguon: "thap"),
            Uv(CapGia.BangGia, 90_000, uuTien: 5, nguon: "cao")
        ], Currency.Vnd);

        Assert.Equal(90_000m, kq.Amount.Amount);
        Assert.Equal("cao", kq.Nguon);
    }

    [Fact]
    public void CUNG_CAP_CUNG_PRIORITY_thi_NEM_LOI_0001__KHONG_chon_bua()
    {
        // 🚨 Nếu chọn "cái đầu tiên gặp" thì kết quả PHỤ THUỘC THỨ TỰ DUYỆT ⇒ cùng giỏ hàng, hai lần tính,
        // hai giá khác nhau — mà KHÔNG có lỗi nào. Đây là họ lỗi `XC-003` F1.
        var loi = Assert.Throws<BusinessException>(() => GiaCoSoResolver.Giai(
        [
            Uv(CapGia.BangGia, 100_000, uuTien: 2, nguon: "a"),
            Uv(CapGia.BangGia, 90_000, uuTien: 2, nguon: "b")
        ], Currency.Vnd, sku: "SKU-1", channelCode: "shopee"));

        Assert.Equal(PricingErrorCodes.UngVienCungCapXungDot, loi.Code);
        Assert.Equal("SKU-1", loi.Data["sku"]);
        Assert.Equal("shopee", loi.Data["channelCode"]);
    }

    [Fact]
    public void Ket_qua_DOC_LAP_voi_THU_TU_duyet_ung_vien()
    {
        // ADR-03/TC-07: đảo thứ tự đầu vào ⇒ CÙNG kết quả. Nếu không, kết quả phụ thuộc thứ tự ⇒ vô nghĩa.
        var a = GiaCoSoResolver.Giai(
            [Uv(CapGia.BangGia, 10, uuTien: 1), Uv(CapGia.GiaTheoKenh, 20, uuTien: 9)], Currency.Vnd);

        var b = GiaCoSoResolver.Giai(
            [Uv(CapGia.GiaTheoKenh, 20, uuTien: 9), Uv(CapGia.BangGia, 10, uuTien: 1)], Currency.Vnd);

        Assert.Equal(a.Cap, b.Cap);
        Assert.Equal(a.Amount, b.Amount);
    }

    [Fact]
    public void Khong_co_ung_vien_nao__mac_dinh_NEM_LOI_0004_kem_SKU_kenh_ngay()
    {
        var ngay = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var loi = Assert.Throws<BusinessException>(
            () => GiaCoSoResolver.Giai([], Currency.Vnd, sku: "SKU-X", channelCode: "lazada", at: ngay));

        Assert.Equal(PricingErrorCodes.KhongTimThayGia, loi.Code);
        Assert.Equal("SKU-X", loi.Data["sku"]);
        Assert.Equal("lazada", loi.Data["channelCode"]);
        Assert.Equal(ngay, loi.Data["at"]);
    }

    [Fact]
    public void Chinh_sach_DungGiaNiemYet__khong_co_gi_cung_van_NEM()
    {
        // Cho phép dùng giá niêm yết nhưng KHÔNG có giá niêm yết ⇒ vẫn không có gì để bán ⇒ NÉM.
        var loi = Assert.Throws<BusinessException>(() => GiaCoSoResolver.Giai(
            [], Currency.Vnd, ChinhSachThieuGia.DungGiaNiemYet, sku: "S"));

        Assert.Equal(PricingErrorCodes.KhongTimThayGia, loi.Code);
    }

    [Fact]
    public void TRON_loai_tien_thi_NEM_0005__KHONG_tu_quy_doi()
    {
        // 🚨 BR-010: cấm tự quy đổi. Nếu lọt, một dòng USD bị cộng với VND ⇒ sai 25.000 lần.
        var loi = Assert.Throws<BusinessException>(() => GiaCoSoResolver.Giai(
            [new UngVienGia(CapGia.BangGia, Money.Usd(10), 0, "usd")], Currency.Vnd));

        Assert.Equal(PricingErrorCodes.LechLoaiTien, loi.Code);
        Assert.Equal("VND", loi.Data["mongDoi"]);
        Assert.Equal("USD", loi.Data["thucTe"]);
    }

    // ── A3: bậc số lượng ──────────────────────────────────────────────────────

    [Fact]
    public void Bac_so_luong_chon_MinQuantity_LON_NHAT_khong_vuot_quantity()
    {
        var theoBac = new List<(int, UngVienGia)>
        {
            (1, Uv(CapGia.GiaTheoKenh, 100_000, nguon: "b1")),
            (10, Uv(CapGia.GiaTheoKenh, 90_000, nguon: "b10")),
            (50, Uv(CapGia.GiaTheoKenh, 80_000, nguon: "b50"))
        };

        Assert.Equal("b1", GiaCoSoResolver.LocTheoBacSoLuong(theoBac, 5).Single().Nguon);
        Assert.Equal("b10", GiaCoSoResolver.LocTheoBacSoLuong(theoBac, 10).Single().Nguon);
        Assert.Equal("b50", GiaCoSoResolver.LocTheoBacSoLuong(theoBac, 999).Single().Nguon);
    }

    [Fact]
    public void Quantity_nho_hon_moi_bac_thi_khong_co_ung_vien()
    {
        var theoBac = new List<(int, UngVienGia)> { (10, Uv(CapGia.GiaTheoKenh, 90_000)) };

        Assert.Empty(GiaCoSoResolver.LocTheoBacSoLuong(theoBac, 3));
    }

    [Fact]
    public void HAI_bac_CUNG_MinQuantity_cung_cap_cung_priority_thi_NEM_0008()
    {
        // PRC-002 ADR-02: lấy "bậc đầu tiên khớp" ⇒ phụ thuộc thứ tự duyệt ⇒ không test được.
        var theoBac = new List<(int, UngVienGia)>
        {
            (10, Uv(CapGia.GiaTheoKenh, 90_000, uuTien: 0, nguon: "a")),
            (10, Uv(CapGia.GiaTheoKenh, 85_000, uuTien: 0, nguon: "b"))
        };

        var loi = Assert.Throws<BusinessException>(
            () => GiaCoSoResolver.LocTheoBacSoLuong(theoBac, 20, sku: "SKU-9"));

        Assert.Equal(PricingErrorCodes.BacThangTrungMinQuantity, loi.Code);
        Assert.Equal(10, loi.Data["minQuantity"]);
        Assert.Equal("SKU-9", loi.Data["sku"]);
    }
}

/// <summary>Test **máy trạng thái bảng giá** (`PRC-001` D4/D12 + `PRC-002` ADR-10/ADR-11).</summary>
public class PriceListTests
{
    private static PriceList Tao(
        PriceListScope scope = PriceListScope.All,
        string? kenh = null,
        Guid? shopId = null,
        DateTime? den = null)
        => new(Guid.NewGuid(), "BG-1", "Bảng giá 1", scope, Currency.Vnd,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), kenh, shopId, den);

    [Fact]
    public void Scope_theo_kenh_ma_THIEU_ChannelCode_thi_NEM_LOI()
    {
        // 🚨 Bất biến KHÔNG được DB bảo vệ: bảng giá "theo kênh" mà không gắn kênh nào ⇒ không bao giờ khớp
        // ở cấp 1/2 ⇒ ÂM THẦM rơi xuống cấp 3 ⇒ bán sai giá.
        var loi = Assert.Throws<BusinessException>(() => Tao(PriceListScope.Channel));

        Assert.Equal(PricingErrorCodes.KhongTimThayGia, loi.Code);
    }

    [Fact]
    public void Scope_ChannelShop_ma_thieu_ChannelShopId_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(
            () => Tao(PriceListScope.ChannelShop, kenh: "shopee"));

        Assert.Equal(PricingErrorCodes.KhongTimThayGia, loi.Code);
    }

    [Fact]
    public void EffectiveTo_KHONG_duoc_truoc_hoac_bang_EffectiveFrom()
    {
        Assert.Throws<BusinessException>(() => Tao(den: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void PhatHanh_bang_gia_RONG_thi_NEM_LOI_0002()
    {
        // Bảng giá rỗng vẫn `Active` được ⇒ mọi SKU thiếu giá ở cấp 3 ⇒ hậu quả ở checkout.
        var bg = Tao();

        var loi = Assert.Throws<BusinessException>(() => bg.PhatHanh(0));

        Assert.Equal(PricingErrorCodes.BangGiaThieuDong, loi.Code);
    }

    [Fact]
    public void PhatHanh_roi_KichHoat_hop_le()
    {
        var bg = Tao();
        bg.PhatHanh(3);
        Assert.Equal(PriceListStatus.Approved, bg.Status);

        Assert.True(bg.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PriceListStatus.Active, bg.Status);
    }

    [Fact]
    public void KichHoat_TRUOC_ngay_hieu_luc_thi_tra_FALSE__KHONG_nem__ADR11()
    {
        // "Chưa tới ngày" KHÔNG phải vi phạm guard ⇒ trả `false` (tiền lệ `ActivateIfDue` của TaxClassification).
        var bg = Tao();
        bg.PhatHanh(2);

        Assert.False(bg.KichHoatNeuToiNgay(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PriceListStatus.Approved, bg.Status);
    }

    [Fact]
    public void KichHoat_khi_dang_Draft_thi_NEM_LOI()
    {
        var bg = Tao();


        Assert.Throws<BusinessException>(
            () => bg.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void HetHan_chi_khi_QUA_ngay__va_Active()
    {
        var den = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var bg = Tao(den: den);
        bg.PhatHanh(1);
        bg.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(bg.HetHanNeuQuaNgay(den));   // đúng ngày cuối ⇒ CHƯA hết hạn
        Assert.True(bg.HetHanNeuQuaNgay(den.AddDays(1)));
        Assert.Equal(PriceListStatus.Expired, bg.Status);
    }

    [Fact]
    public void YeuCauChoPhepSuaNoiDung__bang_gia_ACTIVE_thi_NEM_0003__BR004()
    {
        // 🚨 Append-only: sửa giá trong bảng đang hiệu lực ⇒ đơn/báo cáo CŨ trỏ tới con số đã biến mất.
        var bg = Tao();
        bg.PhatHanh(2);
        bg.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var loi = Assert.Throws<BusinessException>(() => bg.YeuCauChoPhepSuaNoiDung());

        Assert.Equal(PricingErrorCodes.BangGiaActiveKhongSuaDuoc, loi.Code);
    }

    [Fact]
    public void Huy_bang_gia_ACTIVE_thi_NEM_LOI__nhung_Draft_thi_duoc()
    {
        var draft = Tao();
        draft.Huy();
        Assert.Equal(PriceListStatus.Cancelled, draft.Status);

        var active = Tao();
        active.PhatHanh(1);
        active.KichHoatNeuToiNgay(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Throws<BusinessException>(() => active.Huy());
    }

    [Fact]
    public void CoHieuLucTai_kiem_tra_dung_khoang()
    {
        var bg = Tao(den: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(bg.CoHieuLucTai(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(bg.CoHieuLucTai(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(bg.CoHieuLucTai(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(bg.CoHieuLucTai(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Khoa_bang_gia_KHONG_chua_tien_to_ecommerce()
    {
        var khoa = PricingLockKeys.BangGiaTheoPhamVi(Guid.Empty, "Channel", "shopee");

        Assert.DoesNotContain("ecommerce:", khoa, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("pricing:pricelist:", khoa, StringComparison.Ordinal);
    }
}
