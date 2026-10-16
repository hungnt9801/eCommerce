using eCommerce.Settings;

namespace eCommerce.Tests.Settings;

/// <summary>
/// 🚨 **CƠ CHẾ CHO "CHÍNH SÁCH CHO PHÉP CHỌN"** (`INV-003` · `REC-001`).
///
/// **VÌ SAO CẦN:** người quyết định chọn *"cho phép chọn"* (16/10/2026) ⇒ chính sách nay là **DỮ LIỆU CẤU HÌNH**.
/// 📌 Dữ liệu cấu hình **cũng là dữ liệu VÀO** ⇒ có thể bị gõ sai. Nếu hàm đọc **ném** hoặc trả giá trị **nguy hiểm**
/// thì một lỗi gõ trong cấu hình sẽ **làm hỏng job đối soát / đường đặt hàng** — thay vì chỉ **dùng mặc định an toàn**.
///
/// 🔒 **Luật được kiểm ở đây (kiểm được bằng mã, không suy diễn):**
///   (1) mọi **giá trị hợp lệ** phải được nhận **đúng** (kể cả khác hoa/thường, có khoảng trắng thừa);
///   (2) giá trị **lạ/rỗng/null** ⇒ **KHÔNG ném** + trả **mặc định an toàn** + cờ `hopLe = false` (để chỗ gọi **ghi cảnh báo**);
///   (3) **mặc định phải là lựa chọn ÍT RỦI RO NHẤT**: `Warn` (không tự sửa dữ liệu) · `Block` (không bán quá hạn mức) · số **âm** bị từ chối;
///   (4) **danh sách giá trị hợp lệ** phải **khớp** các giá trị **enum** thật ⇒ không lệch tài liệu ↔ mã.
/// </summary>
public class ChinhSachTests
{
    // ── (1) + (2) + (3): ĐƠN VỊ NGƯỠNG ĐỐI SOÁT ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Absolute", DonViNguong.Absolute)]
    [InlineData("absolute", DonViNguong.Absolute)]
    [InlineData("  ABSOLUTE  ", DonViNguong.Absolute)]
    [InlineData("Percent", DonViNguong.Percent)]
    [InlineData("percent", DonViNguong.Percent)]
    public void DocDonViNguong_phai_nhan_DUNG_moi_gia_tri_hop_le__ke_ca_khac_hoa_thuong(string tho, DonViNguong mongDoi)
    {
        var nhan = ChinhSach.DocDonViNguong(tho, out var hopLe);

        Assert.Equal(mongDoi, nhan);
        Assert.True(hopLe);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Số tuyệt đối")]
    public void DocDonViNguong_gia_tri_LA__khong_nem__tra_MAC_DINH_va_bao_khong_hop_le(string? tho)
    {
        var nhan = ChinhSach.DocDonViNguong(tho, out var hopLe);

        Assert.Equal(DonViNguong.Percent, nhan);
        Assert.False(hopLe);
    }

    // ── (3): HÀNH ĐỘNG KHI LỆCH — mặc định phải là `Warn` (KHÔNG tự sửa dữ liệu) ─────────────────

    [Theory]
    [InlineData("RequireConfirmation", HanhDongLech.RequireConfirmation)]
    [InlineData("requireconfirmation", HanhDongLech.RequireConfirmation)]
    [InlineData("Warn", HanhDongLech.Warn)]
    public void DocHanhDongLech_phai_nhan_dung_gia_tri_hop_le(string tho, HanhDongLech mongDoi)
    {
        var nhan = ChinhSach.DocHanhDongLech(tho, out var hopLe);

        Assert.Equal(mongDoi, nhan);
        Assert.True(hopLe);
    }

    [Fact]
    public void DocHanhDongLech_gia_tri_LA__mac_dinh_phai_la_WARN__khong_tu_sua_du_lieu()
    {
        var nhan = ChinhSach.DocHanhDongLech("tu-dong-sua-het", out var hopLe);

        Assert.Equal(HanhDongLech.Warn, nhan);
        Assert.False(hopLe);
    }

    // ── (3): HÀNH ĐỘNG KHI VƯỢT HẠN MỨC — mặc định phải là `Block` (fail-closed) ──────────────────

    [Theory]
    [InlineData("Warn", HanhDongHanMuc.Warn)]
    [InlineData("Block", HanhDongHanMuc.Block)]
    [InlineData("BLOCK", HanhDongHanMuc.Block)]
    public void DocHanhDongHanMuc_phai_nhan_dung_gia_tri_hop_le(string tho, HanhDongHanMuc mongDoi)
    {
        var nhan = ChinhSach.DocHanhDongHanMuc(tho, out var hopLe);

        Assert.Equal(mongDoi, nhan);
        Assert.True(hopLe);
    }

    [Fact]
    public void DocHanhDongHanMuc_gia_tri_LA__mac_dinh_phai_la_BLOCK__fail_closed()
    {
        var nhan = ChinhSach.DocHanhDongHanMuc("cho-qua-het", out var hopLe);

        Assert.Equal(HanhDongHanMuc.Block, nhan);
        Assert.False(hopLe);
    }

    // ── PHẠM VI + ĐƠN VỊ HẠN MỨC ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ChannelWarehouse", PhamViHanMuc.ChannelWarehouse)]
    [InlineData("channel", PhamViHanMuc.Channel)]
    public void DocPhamViHanMuc_phai_nhan_dung_gia_tri_hop_le(string tho, PhamViHanMuc mongDoi)
    {
        var nhan = ChinhSach.DocPhamViHanMuc(tho, out var hopLe);

        Assert.Equal(mongDoi, nhan);
        Assert.True(hopLe);
    }

    [Fact]
    public void DocPhamViHanMuc_gia_tri_LA__mac_dinh_la_CHANNEL()
    {
        var nhan = ChinhSach.DocPhamViHanMuc("toan-cau", out var hopLe);

        Assert.Equal(PhamViHanMuc.Channel, nhan);
        Assert.False(hopLe);
    }

    [Fact]
    public void DocDonViHanMuc_gia_tri_LA__mac_dinh_la_PERCENT()
    {
        var nhan = ChinhSach.DocDonViHanMuc("xyz", out var hopLe);

        Assert.Equal(DonViHanMuc.Percent, nhan);
        Assert.False(hopLe);
    }

    // ── (3): SỐ CẤU HÌNH — âm / không phải số ⇒ mặc định, KHÔNG ném ───────────────────────────────

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.5", 0.5)]
    [InlineData("12", 12)]
    [InlineData("  2.25  ", 2.25)]
    public void DocSo_phai_doc_dung_so_khong_am__theo_InvariantCulture(string tho, double mongDoi)
    {
        var so = ChinhSach.DocSo(tho, macDinh: 99m, out var hopLe);

        Assert.Equal((decimal)mongDoi, so);
        Assert.True(hopLe);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void DocSo_gia_tri_LA_hoac_AM__tra_MAC_DINH_va_bao_khong_hop_le(string? tho)
    {
        var so = ChinhSach.DocSo(tho, macDinh: 7.5m, out var hopLe);

        Assert.Equal(7.5m, so);
        Assert.False(hopLe);
    }

    /// <summary>Dấu phẩy thập phân (`1,5` kiểu Việt Nam) — 📌 ghi rõ hành vi: **KHÔNG** được nhận (dùng `InvariantCulture`)
    /// ⇒ dùng mặc định. Đây là **giới hạn đã biết**: muốn nhận dấu phẩy thì phải **đổi hàm + test này** (không im lặng).</summary>
    [Fact]
    public void DocSo_dau_PHAY_thap_phan__KHONG_duoc_nhan__dung_mac_dinh()
    {
        var so = ChinhSach.DocSo("1,5", macDinh: 3m, out var hopLe);

        Assert.Equal(3m, so);
        Assert.False(hopLe);
    }

    // ── (4): TÀI LIỆU ↔ MÃ — danh sách giá trị hợp lệ phải KHỚP enum thật ─────────────────────────

    [Fact]
    public void DANH_SACH_gia_tri_hop_le_trong_tai_lieu_phai_KHOP_enum_that()
    {
        // 📌 Nếu ai đó thêm một giá trị vào `enum` mà **quên** cập nhật danh sách (hoặc ngược lại) ⇒ test này ĐỎ.
        //    Không có nó, tài liệu và mã sẽ **lệch dần** mà không ai biết.
        Assert.Equal(Enum.GetNames<DonViNguong>().OrderBy(x => x), TenChinhSach.DonViHopLe.OrderBy(x => x));
        Assert.Equal(Enum.GetNames<HanhDongLech>().OrderBy(x => x), TenChinhSach.HanhDongLechHopLe.OrderBy(x => x));
        Assert.Equal(Enum.GetNames<HanhDongHanMuc>().OrderBy(x => x), TenChinhSach.HanhDongHanMucHopLe.OrderBy(x => x));
        Assert.Equal(Enum.GetNames<PhamViHanMuc>().OrderBy(x => x), TenChinhSach.PhamViHanMucHopLe.OrderBy(x => x));
    }

    [Fact]
    public void TEN_setting_phai_KHONG_RONG_va_KHONG_TRUNG_nhau()
    {
        var ten = new[]
        {
            TenChinhSach.NguongDoiSoat_DonVi,
            TenChinhSach.NguongDoiSoat_GiaTri,
            TenChinhSach.NguongDoiSoat_HanhDong,
            TenChinhSach.HanMuc_DonVi,
            TenChinhSach.HanMuc_HanhDong,
            TenChinhSach.HanMuc_PhamVi,
            TenChinhSach.HanMuc_GiaTriMacDinh
        };

        Assert.All(ten, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.Equal(ten.Length, ten.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Mọi giá trị **mặc định** phải là giá trị **hợp lệ** theo chính hàm đọc
    /// ⇒ tránh cảnh *"mặc định ghi trong tài liệu lại bị hàm đọc coi là lạ"* (⇒ luôn có cảnh báo sai).</summary>
    [Fact]
    public void GIA_TRI_MAC_DINH_phai_la_gia_tri_HOP_LE_theo_chinh_ham_doc()
    {
        Assert.True(ChinhSach.DocDonViNguong("Percent", out var h1) == DonViNguong.Percent && h1);
        Assert.True(ChinhSach.DocDonViNguong("Absolute", out var h2) == DonViNguong.Absolute && h2);
        Assert.True(ChinhSach.DocHanhDongLech("Warn", out var h3) == HanhDongLech.Warn && h3);
        Assert.True(ChinhSach.DocHanhDongHanMuc("Block", out var h4) == HanhDongHanMuc.Block && h4);
        Assert.True(ChinhSach.DocPhamViHanMuc("Channel", out var h5) == PhamViHanMuc.Channel && h5);
        Assert.True(ChinhSach.DocSo("0", macDinh: 1m, out var h6) == 0m && h6);
    }
}
