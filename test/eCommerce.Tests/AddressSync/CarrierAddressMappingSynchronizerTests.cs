using eCommerce.Address.Carrier;
using eCommerce.Address.Sync;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// Test cho **lõi đồng bộ danh mục mã địa chỉ hãng vận chuyển** (`ADDR-002`).
///
/// 🎯 BẤT BIẾN QUAN TRỌNG NHẤT CỦA FILE NÀY: **hệ thống KHÔNG BAO GIỜ tự tạo ánh xạ mới.**
///   Danh mục của hãng chỉ cho **mã + tên phía hãng**, KHÔNG cho biết ứng với đơn vị hành chính nào của ta.
///   Muốn tạo ánh xạ thì phải **khớp theo tên** — mà khớp theo tên chỉ là **gợi ý** (dấu tiếng Việt,
///   "Phường" vs "Xã", tỉnh sáp nhập) ⇒ **giao hàng sai địa chỉ, không thu hồi được, không có cảnh báo**.
///   Vì vậy mã mới chỉ được **báo là cần người ánh xạ**, và test dưới đây khẳng định **0 bản ghi** được tạo.
/// </summary>
public class CarrierAddressMappingSynchronizerTests
{
    private static readonly DateTime TaiLuc = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
    private const string Hang = "GHN";

    private static CarrierAddressMapping AnhXa(string ma, string ten, string hang = Hang, bool daXacMinh = true)
    {
        var m = CarrierAddressMapping.TaoMoi(
            hang, "PROVINCE", ma, ten, Guid.NewGuid(), CarrierMatchMethod.ThuCong, "api/ghn", TaiLuc);

        if (daXacMinh && m.Status != CarrierMappingStatus.DaXacMinh)
        {
            m.XacMinh();
        }

        return m;
    }

    private static DanhMucDiaChiHang DanhMuc(params (string Ma, string Ten)[] donVis)
        => new(Hang,
            donVis.Select(x => new DonViDiaChiHang(Hang, "PROVINCE", x.Ma, x.Ten)).ToList(),
            "api/ghn", TaiLuc);

    private static KeHoachDongBoMaHang Lap(
        IReadOnlyList<CarrierAddressMapping> hienCo, DanhMucDiaChiHang danhMuc, double nguong = 1.0)
        => CarrierAddressMappingSynchronizer.LapKeHoach(Hang, hienCo, danhMuc, nguong, soDonViToiThieu: 2);

    // ── Không có gì đổi ─────────────────────────────────────────────────────

    [Fact]
    public void Danh_muc_giong_het_thi_KHONG_co_viec_gi()
    {
        var hienCo = new List<CarrierAddressMapping> { AnhXa("P-1", "Ba Đình"), AnhXa("P-2", "Ngọc Hà") };

        var kh = Lap(hienCo, DanhMuc(("P-1", "Ba Đình"), ("P-2", "Ngọc Hà")));

        Assert.False(kh.CoViec);
    }

    // ── Tên đổi ─────────────────────────────────────────────────────────────

    [Fact]
    public void Ten_doi_thi_CAP_NHAT_TEN_va_entity_TU_HA_muc_tin_cay()
    {
        var cu = AnhXa("P-1", "Ba Đình");
        Assert.Equal(CarrierMappingStatus.DaXacMinh, cu.Status);

        var kh = Lap([cu], DanhMuc(("P-1", "Phường Ba Đình (mới)"), ("P-2", "X")));
        var canCapNhat = CarrierAddressMappingSynchronizer.ApDung(kh, TaiLuc);

        Assert.Equal("Phường Ba Đình (mới)", cu.CarrierAddressName);
        // 🚩 Cơ sở xác minh CŨ đã thay đổi ⇒ KHÔNG được giữ trạng thái đã xác minh.
        Assert.Equal(CarrierMappingStatus.NghiNgo, cu.Status);
        Assert.Contains(cu, canCapNhat);
    }

    // ── Mã biến mất ─────────────────────────────────────────────────────────

    [Fact]
    public void Ma_bien_mat_thi_DANH_DAU_NGHI_NGO_chu_KHONG_XOA()
    {
        var cu = AnhXa("P-99", "Phường cũ");
        var kh = Lap([cu], DanhMuc(("P-1", "A"), ("P-2", "B")));

        Assert.Equal(LoaiViecMaHang.DanhDauNghiNgo,
            Assert.Single(kh.Viec, v => v.CarrierAddressCode == "P-99").Loai);

        var canCapNhat = CarrierAddressMappingSynchronizer.ApDung(kh, TaiLuc);

        Assert.Equal(CarrierMappingStatus.NghiNgo, cu.Status);   // không dùng được cho vận đơn mới
        Assert.Contains(cu, canCapNhat);                          // nhưng VẪN CÒN (vận đơn cũ tham chiếu)
    }

    // ── 🚨 MÃ MỚI: KHÔNG tự tạo ánh xạ ──────────────────────────────────────

    [Fact]
    public void Ma_MOI_thi_CHI_BAO_can_nguoi_anh_xa_va_KHONG_tao_ban_ghi_nao()
    {
        var kh = Lap([], DanhMuc(("P-1", "Ba Đình"), ("P-2", "Ngọc Hà")));

        Assert.Equal(2, kh.SoCanNguoiAnhXa);
        Assert.All(kh.Viec, v => Assert.Equal(LoaiViecMaHang.CanNguoiAnhXa, v.Loai));

        var canCapNhat = CarrierAddressMappingSynchronizer.ApDung(kh, TaiLuc);

        // ⬅️ KHẲNG ĐỊNH TRUNG TÂM: không bản ghi nào được tạo, không bản ghi nào bị sửa.
        Assert.Empty(canCapNhat);
    }

    [Fact]
    public void Viec_can_nguoi_anh_xa_co_kem_TEN_de_nguoi_dung_biet_duong_anh_xa()
    {
        var kh = Lap([], DanhMuc(("P-1", "Ba Đình"), ("P-2", "Ngọc Hà")));

        var viec = Assert.Single(kh.Viec, v => v.CarrierAddressCode == "P-1");

        Assert.Equal("Ba Đình", viec.TenMoi);
        Assert.Contains("KHÔNG tự đoán", viec.LyDo);
    }

    // ── Chỉ đụng tới ĐÚNG hãng ──────────────────────────────────────────────

    [Fact]
    public void Anh_xa_cua_HANG_KHAC_khong_bi_anh_huong()
    {
        var cuaGhtk = AnhXa("P-1", "Trúc Bạch", hang: "GHTK");

        var kh = Lap([cuaGhtk], DanhMuc(("P-9", "X"), ("P-8", "Y")));

        // Không có việc nào cho P-1 của GHTK (không đổi tên, không biến mất trong danh mục của GHN).
        Assert.DoesNotContain(kh.Viec, v => ReferenceEquals(v.HienCo, cuaGhtk));
        Assert.Equal(CarrierMappingStatus.DaXacMinh, cuaGhtk.Status);
    }

    // ── 🚨 HÀNG RÀO ─────────────────────────────────────────────────────────

    [Fact]
    public void HANG_RAO_1_danh_muc_RONG_thi_TU_CHOI()
    {
        var ex = Assert.Throws<BusinessException>(() => Lap([], DanhMuc()));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangRong, ex.Code);
    }

    [Fact]
    public void HANG_RAO_1b_danh_muc_cua_HANG_KHAC_thi_TU_CHOI()
    {
        // 🚨 Đồng bộ danh mục của hãng A lên ánh xạ của hãng B sẽ đánh dấu nghi ngờ OAN toàn bộ hãng B.
        var danhMucCuaGhtk = new DanhMucDiaChiHang("GHTK",
            [new DonViDiaChiHang("GHTK", "PROVINCE", "P-1", "A"), new DonViDiaChiHang("GHTK", "PROVINCE", "P-2", "B")],
            "api/ghtk", TaiLuc);

        var ex = Assert.Throws<BusinessException>(() => Lap([], danhMucCuaGhtk));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangRong, ex.Code);
        Assert.Equal("GHTK", ex.Data["carrierCodeCuaDanhMuc"]);
    }

    [Fact]
    public void HANG_RAO_2_danh_muc_QUA_IT_thi_TU_CHOI()
    {
        var ex = Assert.Throws<BusinessException>(() => Lap([], DanhMuc(("P-1", "A"))));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangQuaIt, ex.Code);
    }

    [Fact]
    public void HANG_RAO_3_danh_dau_qua_nguong_thi_TU_CHOI()
    {
        // 4 ánh xạ đang dùng, danh mục chỉ còn 2 ⇒ 2 mã biến mất = 50% > 20% ⇒ TỪ CHỐI.
        // Nếu áp dụng: 2 ánh xạ thành NghiNgo ⇒ mọi vận đơn tới 2 khu vực đó FAIL-CLOSED.
        var hienCo = new List<CarrierAddressMapping>
        {
            AnhXa("P-1", "A"), AnhXa("P-2", "B"), AnhXa("P-3", "C"), AnhXa("P-4", "D")
        };

        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingSynchronizer.LapKeHoach(Hang, hienCo, DanhMuc(("P-1", "A"), ("P-2", "B")), 0.20, 2));

        Assert.Equal(CarrierAddressErrorCodes.BienMatHangLoat, ex.Code);
        Assert.Equal(2, ex.Data["soNghiNgo"]);
    }

    [Fact]
    public void HANG_RAO_3_cho_phep_khi_trong_nguong()
    {
        // 1/5 = 20% — ĐÚNG bằng ngưỡng ⇒ được phép.
        var hienCo = new List<CarrierAddressMapping>
        {
            AnhXa("P-1", "A"), AnhXa("P-2", "B"), AnhXa("P-3", "C"), AnhXa("P-4", "D"), AnhXa("P-99", "sắp mất")
        };

        var kh = CarrierAddressMappingSynchronizer.LapKeHoach(
            Hang, hienCo, DanhMuc(("P-1", "A"), ("P-2", "B"), ("P-3", "C"), ("P-4", "D")), 0.20, 2);

        Assert.Equal(1, kh.SoDanhDauNghiNgo);
    }

    // ── Tính THUẦN ──────────────────────────────────────────────────────────

    [Fact]
    public void LapKeHoach_KHONG_thay_doi_gi_truoc_khi_ApDung()
    {
        var cu = AnhXa("P-1", "Tên cũ");
        var kh = Lap([cu], DanhMuc(("P-1", "Tên mới"), ("P-2", "X")));

        Assert.Equal("Tên cũ", cu.CarrierAddressName);           // chưa đổi
        Assert.Equal(CarrierMappingStatus.DaXacMinh, cu.Status);
        Assert.True(kh.CoViec);
    }

    [Fact]
    public void Vua_doi_ten_vua_bien_mat_vua_co_ma_moi_thi_phan_loai_dung()
    {
        var hienCo = new List<CarrierAddressMapping> { AnhXa("P-1", "Tên cũ"), AnhXa("P-99", "sắp mất") };

        var kh = Lap(hienCo, DanhMuc(("P-1", "Tên mới"), ("P-2", "mã mới")));

        Assert.Equal(1, kh.SoCapNhatTen);
        Assert.Equal(1, kh.SoDanhDauNghiNgo);
        Assert.Equal(1, kh.SoCanNguoiAnhXa);
    }

    [Fact]
    public void Khong_co_anh_xa_nao_thi_moi_ma_deu_can_nguoi_anh_xa()
    {
        var kh = Lap([], DanhMuc(("P-1", "A"), ("P-2", "B"), ("P-3", "C")));

        Assert.Equal(3, kh.SoCanNguoiAnhXa);
        Assert.Equal(0, kh.SoCapNhatTen);
        Assert.Equal(0, kh.SoDanhDauNghiNgo);
    }
}
