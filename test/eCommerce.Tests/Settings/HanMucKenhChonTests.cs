using eCommerce.Settings;

namespace eCommerce.Tests.Settings;

/// <summary>
/// 🚨 **CHỌN HẠN MỨC ĐANG ÁP THEO KÊNH** (`INV-003` v2 — bảng `InvChannelLimits`) — hàm **THUẦN**.
///
/// **VÌ SAO CẦN TEST RIÊNG CHO PHẦN "CHỌN":** phần **tính** (`TinhHanMuc`) đã có test; phần dễ sai **hơn** là
/// **quyết định lấy con số NÀO** khi có nhiều nguồn (dòng theo kho · dòng cả kênh · giá trị dùng chung) và
/// khi có **phạm vi** (`Channel` vs `ChannelWarehouse`). Sai ở đây **không** ném lỗi — nó chỉ **áp nhầm hạn mức**
/// ⇒ hoặc **chặn oan** đơn hợp lệ, hoặc **cho qua** đơn vượt hạn mức. Cả hai đều im lặng.
///
/// 🔒 **Luật kiểm:**
///   (1) `ChannelWarehouse`: dòng đúng `(kênh, kho)` **thắng**; không có thì rơi về dòng cả kênh;
///   (2) `Channel`: **chỉ** xét dòng cả kênh; dòng theo kho bị **bỏ qua** và **phải trả ra** (để ghi cảnh báo);
///   (3) không có dòng nào khớp ⇒ **giá trị dùng chung** + nguồn `MacDinhChung`;
///   (4) dòng **của kênh khác** **không** được dùng (chỗ gọi lọc theo kênh — test này khoá lại hành vi đó);
///   (5) nhiều dòng cùng khớp (dữ liệu đến từ cache/ảnh chụp cũ) ⇒ chọn **TẤT ĐỊNH**: giá trị **nhỏ nhất** (chặt nhất);
///   (6) **nguồn** luôn được trả ra (để log/`0042` trả lời *"vì sao bị chặn"*).
/// </summary>
public class HanMucKenhChonTests
{
    private static readonly Guid KhoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KhoB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static HanMucKenh.DongHanMuc Dong(Guid? kho, decimal giaTri, HanhDongHanMuc hanhDong = HanhDongHanMuc.Block)
        => new(Guid.NewGuid(), kho, giaTri, hanhDong);

    private static HanMucKenh.KetQuaHanMuc Chon(
        PhamViHanMuc phamVi, IEnumerable<HanMucKenh.DongHanMuc> dong, Guid kho,
        DonViHanMuc donVi = DonViHanMuc.Absolute, decimal macDinh = 0m,
        HanhDongHanMuc hanhDongMacDinh = HanhDongHanMuc.Block)
        => HanMucKenh.ChonHanMuc(phamVi, dong, kho, donVi, macDinh, hanhDongMacDinh, out _);

    // ── (1) `ChannelWarehouse` — theo KHO thắng ───────────────────────────────────────────────────

    [Fact]
    public void PhamVi_ChannelWarehouse__dong_theo_DUNG_kho_THANG_dong_ca_kenh()
    {
        var kq = Chon(PhamViHanMuc.ChannelWarehouse,
            [Dong(null, 100m), Dong(KhoA, 7m)], KhoA);

        Assert.Equal(7m, kq.GiaTri);
        Assert.Equal(HanMucKenh.NguonHanMuc.BangTheoKenhVaKho, kq.Nguon);
    }

    [Fact]
    public void PhamVi_ChannelWarehouse__khong_co_dong_theo_kho__roi_ve_dong_ca_kenh()
    {
        var kq = Chon(PhamViHanMuc.ChannelWarehouse,
            [Dong(null, 100m), Dong(KhoB, 7m)], KhoA);   // dòng theo kho là kho B, không phải A

        Assert.Equal(100m, kq.GiaTri);
        Assert.Equal(HanMucKenh.NguonHanMuc.BangTheoKenh, kq.Nguon);
    }

    [Fact]
    public void PhamVi_ChannelWarehouse__khong_co_dong_nao__dung_mac_dinh_chung()
    {
        var kq = Chon(PhamViHanMuc.ChannelWarehouse, [], KhoA, macDinh: 42m);

        Assert.Equal(42m, kq.GiaTri);
        Assert.Equal(HanMucKenh.NguonHanMuc.MacDinhChung, kq.Nguon);
        Assert.Null(kq.DongId);
    }

    // ── (2) `Channel` — dòng theo KHO bị BỎ QUA và phải TRẢ RA ────────────────────────────────────

    [Fact]
    public void PhamVi_Channel__BO_QUA_dong_theo_kho__va_TRA_RA_de_ghi_canh_bao()
    {
        var dongTheoKho = Dong(KhoA, 3m);
        var dongCaKenh = Dong(null, 50m);

        var kq = HanMucKenh.ChonHanMuc(PhamViHanMuc.Channel, [dongCaKenh, dongTheoKho], KhoA,
            DonViHanMuc.Absolute, 0m, HanhDongHanMuc.Block, out var boQua);

        Assert.Equal(50m, kq.GiaTri);            // dòng cả kênh, KHÔNG phải dòng theo kho
        Assert.Equal(HanMucKenh.NguonHanMuc.BangTheoKenh, kq.Nguon);
        Assert.Single(boQua);
        Assert.Equal(dongTheoKho.Id, boQua[0].Id);   // 🚨 phải TRẢ RA: chỗ gọi mới ghi được cảnh báo (không bỏ qua im lặng)
    }

    [Fact]
    public void PhamVi_Channel__chi_co_dong_theo_kho__dung_mac_dinh_va_van_bao_bo_qua()
    {
        var dongTheoKho = Dong(KhoA, 3m);

        var kq = HanMucKenh.ChonHanMuc(PhamViHanMuc.Channel, [dongTheoKho], KhoA,
            DonViHanMuc.Absolute, 99m, HanhDongHanMuc.Warn, out var boQua);

        // Đây là ca NGUY HIỂM NHẤT của "ô chọn không đổi gì": người vận hành ĐÃ đặt hạn mức theo kho,
        // hệ thống bỏ qua ⇒ rơi về mặc định. Nếu không có `boQua` thì họ **không bao giờ biết**.
        Assert.Equal(99m, kq.GiaTri);
        Assert.Equal(HanMucKenh.NguonHanMuc.MacDinhChung, kq.Nguon);
        Assert.Equal(HanhDongHanMuc.Warn, kq.HanhDong);
        Assert.Single(boQua);
    }

    // ── (3)+(4) Chỉ dùng dòng của ĐÚNG kênh (chỗ gọi lọc theo kênh) ───────────────────────────────

    [Fact]
    public void Khong_co_dong_nao_cua_kenh__KHONG_bao_gio_lay_dong_kenh_khac()
    {
        // Chỗ gọi lọc `x.ChannelCode == kenh`; test này khoá lại giả định đó: hàm nhận gì thì dùng nấy,
        // và khi danh sách RỖNG thì **không** bịa ra hạn mức nào.
        var kq = Chon(PhamViHanMuc.Channel, [], KhoA, macDinh: 0m);

        Assert.Equal(0m, kq.GiaTri);
        Assert.Equal(HanMucKenh.NguonHanMuc.MacDinhChung, kq.Nguon);
    }

    // ── (5) TẤT ĐỊNH khi nhiều dòng cùng khớp ────────────────────────────────────────────────────

    [Fact]
    public void Nhieu_dong_cung_khop__chon_gia_tri_NHO_NHAT__khong_phu_thuoc_thu_tu_dau_vao()
    {
        // DB **chặn** ca này (unique `NULLS NOT DISTINCT`), nhưng dữ liệu có thể đến từ cache/ảnh chụp cũ.
        // 🔒 Lấy "dòng đầu tiên" ⇒ kết quả phụ thuộc thứ tự DB trả về ⇒ CÙNG dữ liệu, HAI kết quả.
        var lon = Dong(null, 100m);
        var nho = Dong(null, 5m);

        var thuTu1 = Chon(PhamViHanMuc.Channel, [lon, nho], KhoA);
        var thuTu2 = Chon(PhamViHanMuc.Channel, [nho, lon], KhoA);

        Assert.Equal(5m, thuTu1.GiaTri);          // chặt nhất
        Assert.Equal(thuTu1.GiaTri, thuTu2.GiaTri);
        Assert.Equal(thuTu1.DongId, thuTu2.DongId);
    }

    // ── (6) Hành động + đơn vị đi kèm kết quả ────────────────────────────────────────────────────

    [Fact]
    public void HanhDong_va_DonVi_di_kem_ket_qua__de_log_va_ma_loi_noi_dung_su_that()
    {
        var kq = Chon(PhamViHanMuc.Channel, [Dong(null, 25m, HanhDongHanMuc.Warn)], KhoA,
            donVi: DonViHanMuc.Percent);

        Assert.Equal(HanhDongHanMuc.Warn, kq.HanhDong);
        Assert.Equal(DonViHanMuc.Percent, kq.DonVi);
        Assert.Equal(PhamViHanMuc.Channel, kq.PhamVi);
        Assert.NotNull(kq.DongId);               // có dòng ⇒ phải truy được về dòng nào (để sửa cho đúng dòng)
    }

    [Fact]
    public void Dong_mac_dinh__HanhDong_va_GiaTri_lay_tu_tham_so__khong_bia()
    {
        var kq = Chon(PhamViHanMuc.Channel, [], KhoA, macDinh: 12m, hanhDongMacDinh: HanhDongHanMuc.Warn);

        Assert.Equal(12m, kq.GiaTri);
        Assert.Equal(HanhDongHanMuc.Warn, kq.HanhDong);
        Assert.Null(kq.DongId);
    }
}
