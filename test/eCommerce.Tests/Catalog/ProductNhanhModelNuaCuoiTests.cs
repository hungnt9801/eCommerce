using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// **WP-03 NỬA SAU — LUẬT CHA–CON + "CÓ GÌ ĐỂ BÁN" + ADAPTER ĐỌC CŨ** (`03 §3.1`, D-07).
///
/// 🚨 VÌ SAO PHẢI KHOÁ BẰNG TEST: cả ba luật ở đây **không có lỗi biên dịch** và **không ném** nếu thiếu guard —
/// chúng chỉ thể hiện thành dữ liệu/trạng thái sai:
/// <list type="number">
///   <item>Sản phẩm mô hình mới (3 ĐVT, 0 biến thể) **không publish được** ⇒ toàn bộ danh mục mới **không bán
///     được**, triệu chứng là `403` lúc bấm publish chứ không phải lỗi build (nợ `handover-n2` §2.8).</item>
///   <item>Sản phẩm **cha** được publish ⇒ storefront có mục không giá, báo cáo có SKU không bao giờ bán.</item>
///   <item>Chu trình cha–con (`A → B → A`) làm mọi phép duyệt cây **treo** hoặc **tràn stack** ở chỗ khác.</item>
///   <item>Adapter đọc cũ trả **rỗng** cho sản phẩm mô hình mới ⇒ hàng có trong danh mục nhưng **vô hình** với
///     storefront/POS, không có lỗi nào.</item>
/// </list>
/// </summary>
public class ProductNhanhModelNuaCuoiTests
{
    private static Product SanPhamMoi(string code = "SP-1", string name = "Sản phẩm 1")
        => new(Guid.NewGuid(), code, name);

    // ── ① Publish: "có gì để bán" nay tính cả ĐVT GỐC (D-07) ─────────────────────────────────────

    [Fact]
    public void Publish_DUOC_khi_chi_co_DVT_goc__mo_hinh_Nhanh_khong_co_bien_the()
    {
        // 🚨 Đây chính là nợ `handover-n2` §2.8: sản phẩm tạo qua `POST /api/v1/products` (3 ĐVT, 0 biến thể)
        // trước WP-03 nửa sau KHÔNG BAO GIỜ publish được.
        var sp = SanPhamMoi();

        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m);
        sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 220_000m);

        sp.Publish();

        Assert.Equal(ProductStatus.Published, sp.Status);
        Assert.True(sp.IsPublished);
    }

    [Fact]
    public void Publish_VAN_NEM_LOI_0003_khi_khong_co_DVT_goc_lan_bien_the()
    {
        // ✅ Chiều ngược: nới guard KHÔNG được biến thành "publish gì cũng được".
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.Publish());

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongCoBienTheHoatDong, loi.Code);
        Assert.Equal(ProductStatus.Draft, sp.Status);
    }

    [Fact]
    public void Publish_van_DUOC_khi_chi_co_bien_the_cu__khong_pha_du_lieu_GDD1()
    {
        var sp = SanPhamMoi();
        sp.AddVariant(Guid.NewGuid(), "SKU-CU-1", 100_000m);

        sp.Publish();

        Assert.Equal(ProductStatus.Published, sp.Status);
    }

    // ── ② Chu trình cha–con nhiều mức (`CaySanPham`, mã 0048) ───────────────────────────────────

    [Fact]
    public void Gan_cha_null_luon_hop_le__ke_ca_khi_du_lieu_co_chu_trinh()
    {
        var a = Guid.NewGuid();

        // Không được ném: bỏ cha là cách SỬA dữ liệu có chu trình.
        CaySanPham.KiemTraKhongChuTrinh(a, null, _ => a);
    }

    [Fact]
    public void Gan_chinh_minh_giu_ma_0043__khong_nuot_vao_0048()
    {
        var a = Guid.NewGuid();

        var loi = Assert.Throws<BusinessException>(
            () => CaySanPham.KiemTraKhongChuTrinh(a, a, _ => null));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SanPhamChaTrungChinhNo, loi.Code);
    }

    [Fact]
    public void Chu_trinh_NHIEU_MUC_thi_NEM_LOI_0048()
    {
        // Dây: A là gốc; B con của A; C con của B.
        // Gán A.Parent = C ⇒ A → C → B → A: vòng.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var cha = new Dictionary<Guid, Guid?>
        {
            [a] = null,
            [b] = a,
            [c] = b
        };

        var loi = Assert.Throws<BusinessException>(
            () => CaySanPham.KiemTraKhongChuTrinh(a, c, id => cha.TryGetValue(id, out var p) ? p : null));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon, loi.Code);
    }

    [Fact]
    public void Day_chuyen_hop_le_dai_3_muc_thi_KHONG_nem()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        var cha = new Dictionary<Guid, Guid?>
        {
            [a] = null,
            [b] = a,
            [c] = b
        };

        // D là sản phẩm mới, cha = C ⇒ chuỗi D → C → B → A, không vòng.
        CaySanPham.KiemTraKhongChuTrinh(d, c, id => cha.TryGetValue(id, out var p) ? p : null);
    }

    [Fact]
    public void Du_lieu_CU_da_co_chu_trinh_thi_NEM_0048_chu_khong_treo()
    {
        // X ⇄ Y đã có sẵn trong DB (import tay/SQL). Gán Z.Parent = X phải NÉM, không được lặp vô hạn.
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        var z = Guid.NewGuid();

        var cha = new Dictionary<Guid, Guid?>
        {
            [x] = y,
            [y] = x
        };

        var loi = Assert.Throws<BusinessException>(
            () => CaySanPham.KiemTraKhongChuTrinh(z, x, id => cha.TryGetValue(id, out var p) ? p : null));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon, loi.Code);
    }

    [Fact]
    public void Chuoi_cha_dai_qua_gioi_han_thi_NEM_0048__chong_treo_khi_du_lieu_hong()
    {
        // Dây tuyến tính 200 mức (không vòng) nhưng vượt `DoSauToiDa` ⇒ phải NÉM thay vì duyệt mãi.
        var goc = Guid.NewGuid();
        var cha = new Dictionary<Guid, Guid?> { [goc] = null };

        var truoc = goc;

        for (var i = 0; i < CaySanPham.DoSauToiDa + 50; i++)
        {
            var moi = Guid.NewGuid();
            cha[moi] = truoc;
            truoc = moi;
        }

        var sanPhamMoi = Guid.NewGuid();

        var loi = Assert.Throws<BusinessException>(
            () => CaySanPham.KiemTraKhongChuTrinh(sanPhamMoi, truoc, id => cha.TryGetValue(id, out var p) ? p : null));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhSanPhamChaCon, loi.Code);
    }

    // ── ③ Adapter đọc cũ: biến thể TRƯỚC, ĐVT gốc SAU ───────────────────────────────────────────

    [Fact]
    public void Adapter_doc_BIEN_THE_truoc__giu_nguyen_hanh_vi_cu()
    {
        var sp = SanPhamMoi("SP-ADAPTER", "Áo thun");

        sp.AddVariant(Guid.NewGuid(), "SKU-B", 120_000m, "8935000000099");
        sp.AddVariant(Guid.NewGuid(), "SKU-A", 100_000m);
        sp.AddUnit(Guid.NewGuid(), "cái", 1m, 999_000m);

        var dong = ProductLegacyAdapter.Doc(sp.Id, sp.Code, sp.Variants, sp.Units);

        Assert.Equal(2, dong.Count);
        Assert.All(dong, x => Assert.Equal(NguonDongBan.BienThe, x.Nguon));
        Assert.Equal(["SKU-A", "SKU-B"], dong.Select(x => x.MaTraGia));   // tất định theo SKU
        Assert.Equal("8935000000099", dong.Single(x => x.MaTraGia == "SKU-B").Barcode);
    }

    [Fact]
    public void Adapter_doc_DVT_GOC_khi_KHONG_con_bien_the_dang_ban()
    {
        // Sản phẩm mô hình Nhanh: 0 biến thể, 3 ĐVT ⇒ PHẢI có đúng 1 dòng (ĐVT gốc) thay vì rỗng.
        var sp = SanPhamMoi("SP-MOI-1", "Nước ngọt");

        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m, barcode: "8935000000101");
        sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 228_000m);

        var dong = ProductLegacyAdapter.Doc(sp.Id, sp.Code, [], sp.Units);

        var motDong = Assert.Single(dong);
        Assert.Equal(NguonDongBan.DonVi, motDong.Nguon);
        Assert.Equal("SP-MOI-1", motDong.MaTraGia);        // khoá tra giá = MÃ sản phẩm (ĐVT không có SKU)
        Assert.Equal(10_000m, motDong.Gia);                // giá của ĐVT GỐC, không phải thùng
        Assert.Equal("8935000000101", motDong.Barcode);
        Assert.Equal(sp.Id, motDong.ProductId);
    }

    [Fact]
    public void Adapter_bo_qua_bien_the_DA_NGUNG_BAN_roi_moi_xet_DVT()
    {
        var sp = SanPhamMoi();

        var bienThe = sp.AddVariant(Guid.NewGuid(), "SKU-NGUNG", 50_000m);
        bienThe.NgungBan();

        sp.AddUnit(Guid.NewGuid(), "cái", 1m, 20_000m);

        var dong = ProductLegacyAdapter.Doc(sp.Id, sp.Code, sp.Variants, sp.Units);

        var motDong = Assert.Single(dong);
        Assert.Equal(NguonDongBan.DonVi, motDong.Nguon);
    }

    [Fact]
    public void Adapter_KHONG_CO_nguon_nao_thi_rong__khong_bia_dong()
    {
        var sp = SanPhamMoi();

        Assert.Empty(ProductLegacyAdapter.Doc(sp.Id, sp.Code, [], []));
    }
}
