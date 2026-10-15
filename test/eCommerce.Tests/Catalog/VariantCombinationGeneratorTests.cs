using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// Test **bộ sinh tổ hợp biến thể** (`CAT-002` D5/D6/D7, BR-009/010/011/016) — hàm **thuần** nên test trực tiếp.
///
/// 🚨 **VÌ SAO BỘ TEST NÀY QUAN TRỌNG:** `AttributeHash` là **khoá đối chiếu** để biết tổ hợp nào đã tồn tại
/// (BR-013: sinh lại **phải giữ** `Id`/`Sku` cũ). Hash **lệch** ⇒ hoặc **tạo biến thể trùng** (khách thấy hai
/// lựa chọn giống nhau), hoặc **coi hai tổ hợp KHÁC NHAU là một** ⇒ **bán sai hàng**. Cả hai đều **không có
/// lỗi biên dịch** và DB chỉ chặn được ca thứ nhất.
/// </summary>
public class VariantCombinationGeneratorTests
{
    private static readonly Guid MauId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DoId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid XanhId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid MId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid LId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // ── BR-009: tích Descartes ────────────────────────────────────────────────

    [Fact]
    public void Sinh_dung_so_to_hop_bang_TICH_DESCARTES()
    {
        // 2 màu × 2 size = 4 tổ hợp.
        var toHop = VariantCombinationGenerator.Sinh(
        [
            (MauId, (IReadOnlyList<Guid>)new[] { DoId, XanhId }),
            (SizeId, (IReadOnlyList<Guid>)new[] { MId, LId })
        ]);

        Assert.Equal(4, toHop.Count);
        Assert.Equal(4, toHop.Select(x => x.AttributeHash).Distinct().Count());   // 4 hash KHÁC nhau
    }

    [Fact]
    public void Moi_to_hop_co_DUNG_mot_gia_tri_cho_moi_thuoc_tinh()
    {
        var toHop = VariantCombinationGenerator.Sinh(
        [
            (MauId, (IReadOnlyList<Guid>)new[] { DoId, XanhId }),
            (SizeId, (IReadOnlyList<Guid>)new[] { MId })
        ]);

        Assert.Equal(2, toHop.Count);

        foreach (var th in toHop)
        {
            Assert.Equal(2, th.GiaTri.Count);
            Assert.Single(th.GiaTri, x => x.AttributeId == MauId);
            Assert.Single(th.GiaTri, x => x.AttributeId == SizeId);
        }
    }

    // ── BR-016: hash ỔN ĐỊNH theo thứ tự chọn ─────────────────────────────────

    [Fact]
    public void Hash_ON_DINH_voi_THU_TU_chon_gia_tri()
    {
        // 🚨 "Đỏ→M" và "M→Đỏ" PHẢI cho CÙNG hash — nếu không, sinh lại tổ hợp sẽ tạo biến thể TRÙNG.
        var a = VariantCombinationGenerator.TinhAttributeHash([(MauId, DoId), (SizeId, MId)]);
        var b = VariantCombinationGenerator.TinhAttributeHash([(SizeId, MId), (MauId, DoId)]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_ON_DINH_voi_THU_TU_TRUC_khi_sinh()
    {
        var truc1 = VariantCombinationGenerator.Sinh(
        [
            (MauId, (IReadOnlyList<Guid>)new[] { DoId, XanhId }),
            (SizeId, (IReadOnlyList<Guid>)new[] { MId, LId })
        ]);

        // Đảo thứ tự TRỤC (màu/size) ⇒ tập tổ hợp và hash PHẢI giống hệt.
        var truc2 = VariantCombinationGenerator.Sinh(
        [
            (SizeId, (IReadOnlyList<Guid>)new[] { MId, LId }),
            (MauId, (IReadOnlyList<Guid>)new[] { DoId, XanhId })
        ]);

        Assert.Equal(
            truc1.Select(x => x.AttributeHash).OrderBy(x => x).ToArray(),
            truc2.Select(x => x.AttributeHash).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Hash_KHAC_nhau_khi_tap_gia_tri_KHAC_nhau()
    {
        // 🚨 Ca nguy hiểm nhất: nếu hash bỏ sót một trục thì "Đỏ+M" và "Đỏ+L" sẽ CÙNG hash ⇒ hệ thống coi
        // hai biến thể khác nhau là một ⇒ **bán sai hàng**.
        var a = VariantCombinationGenerator.TinhAttributeHash([(MauId, DoId), (SizeId, MId)]);
        var b = VariantCombinationGenerator.TinhAttributeHash([(MauId, DoId), (SizeId, LId)]);
        var c = VariantCombinationGenerator.TinhAttributeHash([(MauId, XanhId), (SizeId, MId)]);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(b, c);
    }

    [Fact]
    public void Hash_dung_dinh_dang__64_ky_tu_hex_thuong()
    {
        var h = VariantCombinationGenerator.TinhAttributeHash([(MauId, DoId)]);

        Assert.Equal(64, h.Length);
        Assert.Matches("^[0-9a-f]{64}$", h);
    }

    // ── D6: chặn bùng nổ tổ hợp TRƯỚC khi ghi ─────────────────────────────────

    [Fact]
    public void Vuot_NGUONG_thi_NEM_LOI_0009_kem_so_to_hop_va_nguong()
    {
        // 30 × 30 = 900 > 200 ⇒ phải ném TRƯỚC khi sinh/g Hi.
        var nhieu1 = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToList();
        var nhieu2 = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToList();

        var loi = Assert.Throws<BusinessException>(() => VariantCombinationGenerator.Sinh(
        [
            (MauId, (IReadOnlyList<Guid>)nhieu1),
            (SizeId, (IReadOnlyList<Guid>)nhieu2)
        ]));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe, loi.Code);
        Assert.Equal(900L, loi.Data["soToHop"]);
        Assert.Equal(200, loi.Data["nguong"]);
    }

    [Fact]
    public void Ngung_rieng_duoc_truyen_vao__dung_bang_nguong_thi_VAN_DUOC()
    {
        var ba = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        var toHop = VariantCombinationGenerator.Sinh(
            [(MauId, (IReadOnlyList<Guid>)ba)], nguongToiDa: 3);

        Assert.Equal(3, toHop.Count);
    }

    [Fact]
    public void Nguong_khong_hop_le_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(() => VariantCombinationGenerator.Sinh(
            [(MauId, (IReadOnlyList<Guid>)new[] { DoId })], nguongToiDa: 0));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe, loi.Code);
    }

    [Fact]
    public void Nhieu_truc_co_the_tich_RAT_LON__phai_chan_chu_khong_tran_so()
    {
        // 🚨 Nếu đếm bằng `int` thì 100^5 sẽ TRÀN ⇒ ra số ÂM ⇒ lọt qua kiểm tra ngưỡng ⇒ treo request + phình DB.
        var tram = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        var loi = Assert.Throws<BusinessException>(() => VariantCombinationGenerator.Sinh(
        [
            (MauId, (IReadOnlyList<Guid>)tram),
            (SizeId, (IReadOnlyList<Guid>)tram),
            (Guid.NewGuid(), (IReadOnlyList<Guid>)tram)
        ]));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe, loi.Code);
        Assert.True((long)loi.Data["soToHop"]! > 0, "Số tổ hợp PHẢI dương — tràn số sẽ cho số âm.");
    }

    // ── Ca biên ───────────────────────────────────────────────────────────────

    [Fact]
    public void KHONG_co_thuoc_tinh_nao_thi_tra_ve_RONG__khong_phai_loi()
    {
        // `CAT-002` §12: sản phẩm không có thuộc tính `Variant` ⇒ KHÔNG sinh tổ hợp, **không phải lỗi**.
        Assert.Empty(VariantCombinationGenerator.Sinh([]));
    }

    [Fact]
    public void Thuoc_tinh_KHONG_co_gia_tri_nao_thi_NEM_LOI()
    {
        // Im lặng bỏ qua sẽ tạo biến thể **thiếu trục** ⇒ khách không chọn được.
        var loi = Assert.Throws<BusinessException>(() => VariantCombinationGenerator.Sinh(
            [(MauId, (IReadOnlyList<Guid>)Array.Empty<Guid>())]));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    // ── BR-012 + BR-013: ĐỐI CHIẾU tổ hợp (trái tim của "sinh lại") ─────────────

    [Fact]
    public void DoiChieu_tao_moi_khi_chua_co_bien_the_nao()
    {
        var kq = VariantCombinationGenerator.DoiChieu(["h1", "h2"], []);

        Assert.Equal(new[] { "h1", "h2" }, kq.TaoMoi.ToArray());
        Assert.Empty(kq.GiuNguyen);
        Assert.Empty(kq.NgungBan);
        Assert.Empty(kq.KichHoatLai);
    }

    [Fact]
    public void DoiChieu_GIU_NGUYEN_bien_the_dang_ban__BR013()
    {
        var kq = VariantCombinationGenerator.DoiChieu(["h1"], [("h1", true)]);

        Assert.Empty(kq.TaoMoi);
        Assert.Equal(new[] { "h1" }, kq.GiuNguyen.ToArray());
        Assert.Empty(kq.NgungBan);
    }

    [Fact]
    public void DoiChieu_NGUNG_BAN_bien_the_mo_coi__BR012__va_KHONG_xoa()
    {
        // 🚨 Ca thật: người bán BỎ màu "Xanh" ⇒ tổ hợp "Xanh-M"/"Xanh-L" không còn mong muốn.
        // Nếu bỏ mặc thì chúng VẪN ĐANG BÁN nhưng KHÔNG xuất hiện trong bộ chọn nào ⇒ khách không tìm thấy,
        // hệ thống vẫn bán ⇒ lệch tồn kho/doanh thu mà KHÔNG có lỗi nào.
        var kq = VariantCombinationGenerator.DoiChieu(
            ["do-m", "do-l"],
            [("do-m", true), ("do-l", true), ("xanh-m", true), ("xanh-l", true)]);

        Assert.Empty(kq.TaoMoi);
        Assert.Equal(2, kq.GiuNguyen.Count);
        Assert.Equal(new[] { "xanh-l", "xanh-m" }, kq.NgungBan.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void DoiChieu_KICH_HOAT_LAI_khi_to_hop_QUAY_LAI__giu_nguyen_Id_Sku()
    {
        // Chiều ngược lại: người bán chọn lại "Xanh" ⇒ biến thể CŨ phải được mở bán lại,
        // KHÔNG tạo biến thể mới (BR-013) — nếu tạo mới thì SKU cũ vẫn tồn tại ⇒ trùng lặp vĩnh viễn.
        var kq = VariantCombinationGenerator.DoiChieu(["do-m", "xanh-m"], [("do-m", true), ("xanh-m", false)]);

        Assert.Empty(kq.TaoMoi);
        Assert.Equal(new[] { "do-m" }, kq.GiuNguyen.ToArray());
        Assert.Equal(new[] { "xanh-m" }, kq.KichHoatLai.ToArray());
        Assert.Empty(kq.NgungBan);
    }

    [Fact]
    public void DoiChieu_KHONG_dung_toi_bien_the_da_ngung_ban_va_khong_con_mong_muon()
    {
        // Biến thể đã ngừng bán và tổ hợp vẫn không mong muốn ⇒ KHÔNG tính vào NgungBan (tránh log/đếm sai).
        var kq = VariantCombinationGenerator.DoiChieu(["do-m"], [("do-m", true), ("xanh-m", false)]);

        Assert.Empty(kq.NgungBan);
        Assert.Empty(kq.KichHoatLai);
    }

    [Fact]
    public void DoiChieu_rong_thi_ngung_ban_TAT_CA_bien_the_dang_ban()
    {
        var kq = VariantCombinationGenerator.DoiChieu([], [("h1", true), ("h2", true), ("h3", false)]);

        Assert.Empty(kq.TaoMoi);
        Assert.Equal(2, kq.NgungBan.Count);       // chỉ 2 cái ĐANG BÁN
        Assert.Empty(kq.KichHoatLai);
    }

    [Fact]
    public void Mot_truc_mot_gia_tri_thi_ra_DUNG_mot_to_hop()
    {
        var toHop = VariantCombinationGenerator.Sinh([(MauId, (IReadOnlyList<Guid>)new[] { DoId })]);

        Assert.Single(toHop);
        Assert.Single(toHop[0].GiaTri);
    }
}
