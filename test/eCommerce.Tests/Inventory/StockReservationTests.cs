using eCommerce.Inventory;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **quyết định giữ chỗ** (`INV-002`) — hàm **THUẦN**, chỗ **chống bán vượt tồn**.
/// 🚨 Sai ở đây là **bán thứ không có** — loại lỗi tệ nhất trong thương mại điện tử (khách đã trả tiền,
/// hàng không tồn tại, phải xin lỗi + hoàn tiền + mất uy tín).
/// </summary>
public class QuyetDinhGiuChoTests
{
    [Theory]
    [InlineData(10, 0, 10)]    // chưa giữ gì
    [InlineData(10, 3, 7)]     // đã giữ 3
    [InlineData(10, 10, 0)]    // giữ hết
    [InlineData(0, 0, 0)]      // hết hàng
    public void CoTheBan_BANG_OnHand_tru_Reserved(int onHand, int reserved, int mongDoi)
    {
        Assert.Equal(mongDoi, QuyetDinhGiuCho.CoTheBan(onHand, reserved));
    }

    [Fact]
    public void Yeu_cau_BANG_dung_so_con_lai_thi_DUOC__khach_cuoi_cung_van_mua_duoc()
    {
        // ⚠️ Biên: `yeuCau == coTheBan` PHẢI được phép. Nếu viết `<` thay vì `<=` thì **món cuối cùng
        // không ai mua được** — hàng nằm kho vĩnh viễn và không có lỗi nào chỉ ra.
        Assert.Null(QuyetDinhGiuCho.KiemTra(onHand: 10, reserved: 9, yeuCau: 1));
    }

    [Fact]
    public void Yeu_cau_VUOT_so_con_lai_thi_bi_tu_choi__kem_ly_do_doc_duoc()
    {
        var lyDo = QuyetDinhGiuCho.KiemTra(onHand: 10, reserved: 8, yeuCau: 3);

        Assert.NotNull(lyDo);
        Assert.Contains("Không đủ hàng", lyDo, StringComparison.Ordinal);
        Assert.Contains("có thể bán 2", lyDo, StringComparison.Ordinal);
    }

    [Fact]
    public void Du_lieu_ton_kho_VO_LY__Reserved_lon_hon_OnHand__thi_bao_KHONG_HOP_LE()
    {
        // `CHECK` ở DB (ADR-03) đã chặn ghi giá trị này; nhưng nếu dữ liệu cũ/hỏng lọt vào thì hàm phải
        // **nói thẳng là dữ liệu sai** thay vì tính ra "có thể bán = -3" rồi cho qua.
        var lyDo = QuyetDinhGiuCho.KiemTra(onHand: 5, reserved: 8, yeuCau: 1);

        Assert.NotNull(lyDo);
        Assert.Contains("KHÔNG hợp lệ", lyDo, StringComparison.Ordinal);
    }

    [Fact]
    public void Yeu_cau_nho_hon_1_thi_tu_choi()
    {
        Assert.NotNull(QuyetDinhGiuCho.KiemTra(10, 0, 0));
        Assert.NotNull(QuyetDinhGiuCho.KiemTra(10, 0, -1));
    }
}

/// <summary>Test **vòng đời lượt giữ chỗ** (`INV-002`).</summary>
public class StockReservationTests
{
    private static readonly DateTime BayGio = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private static StockReservation Tao(int soLuong = 2, int ttlPhut = 15)
        => new(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", soLuong, BayGio.AddMinutes(ttlPhut), "Cart", "C-1");

    [Fact]
    public void Tao_la_Held__va_chua_nha()
    {
        var gc = Tao();

        Assert.Equal(ReservationStatus.Held, gc.Status);
        Assert.Null(gc.ReleasedAtUtc);
        Assert.False(gc.DaHetHan(BayGio));
    }

    [Fact]
    public void Nha_HAI_LAN_thi_NEM__neu_khong_thi_Available_tang_GAP_DOI()
    {
        var gc = Tao();
        gc.Nha(ReservationStatus.Released, BayGio);

        Assert.Equal(InventoryErrorCodes.GiuChoKhongConGiu,
            Assert.Throws<BusinessException>(() => gc.Nha(ReservationStatus.Released, BayGio)).Code);
    }

    [Fact]
    public void Phan_biet_RELEASED_va_EXPIRED__de_biet_VI_SAO_hang_duoc_nha()
    {
        var khachHuy = Tao();
        khachHuy.Nha(ReservationStatus.Released, BayGio);

        var hetTtl = Tao();
        hetTtl.Nha(ReservationStatus.Expired, BayGio);

        Assert.Equal(ReservationStatus.Released, khachHuy.Status);
        Assert.Equal(ReservationStatus.Expired, hetTtl.Status);
    }

    [Fact]
    public void Nha_voi_ly_do_KHONG_hop_le_thi_NEM()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Tao().Nha(ReservationStatus.Committed, BayGio));
    }

    [Fact]
    public void DaHetHan_tra_TRUE_khi_qua_moc()
    {
        var gc = Tao(ttlPhut: 15);

        Assert.True(gc.DaHetHan(BayGio.AddMinutes(15)));
        Assert.True(gc.DaHetHan(BayGio.AddMinutes(16)));
        Assert.False(gc.DaHetHan(BayGio.AddMinutes(14)));
    }

    [Fact]
    public void CHOT_giu_cho_DA_HET_HAN_thi_NEM__chong_ban_hai_lan_cung_mot_mon()
    {
        // 🚨 Kịch bản thật: giữ chỗ hết TTL ⇒ job **đã nhả hàng về `Available`** ⇒ khách khác mua được.
        // Nếu đơn cũ vẫn "chốt" được thì **cùng một món bán cho hai người**.
        var gc = Tao(ttlPhut: 15);

        var loi = Assert.Throws<BusinessException>(() => gc.Chot(BayGio.AddMinutes(20)));

        Assert.Equal(InventoryErrorCodes.GiuChoDaHetHan, loi.Code);
    }

    [Fact]
    public void CHOT_trong_han_thi_thanh_Committed__va_KHONG_chot_lai_duoc()
    {
        var gc = Tao();

        gc.Chot(BayGio.AddMinutes(5));
        Assert.Equal(ReservationStatus.Committed, gc.Status);

        Assert.Equal(InventoryErrorCodes.GiuChoKhongConGiu,
            Assert.Throws<BusinessException>(() => gc.Chot(BayGio.AddMinutes(6))).Code);
    }

    [Fact]
    public void Nha_SAU_KHI_CHOT_thi_NEM()
    {
        var gc = Tao();
        gc.Chot(BayGio);

        Assert.Equal(InventoryErrorCodes.GiuChoKhongConGiu,
            Assert.Throws<BusinessException>(() => gc.Nha(ReservationStatus.Released, BayGio)).Code);
    }

    [Fact]
    public void So_luong_nho_hon_1_thi_NEM()
    {
        Assert.Equal(InventoryErrorCodes.SoLuongPhaiDuong,
            Assert.Throws<BusinessException>(() => Tao(soLuong: 0)).Code);
    }

    [Fact]
    public void Giu_cho_KHONG_lam_thay_doi_OnHand__chi_doi_Reserved()
    {
        // 🚨 Bất biến then chốt để phép đối soát kho không báo lệch giả (`DoiSoatTonKho` cho `Reservation` dấu 0).
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");
        item.ApplyMovement(StockMovementType.Receipt, 10);

        item.ApplyMovement(StockMovementType.Reservation, 4);

        Assert.Equal(10, item.OnHand);      // KHÔNG đổi
        Assert.Equal(4, item.Reserved);     // đổi
        Assert.Equal(6, item.Available);
    }
}

/// <summary>
/// Test **`SAL-003` — chốt giữ chỗ ⇒ xuất kho** (bất biến then chốt, ở tầng entity nên test được không cần DB).
/// </summary>
public class ChotGiuChoBatBienTests
{
    [Fact]
    public void Chot_thi_OnHand_va_Reserved_CUNG_GIAM__Available_KHONG_DOI()
    {
        // 🚨 Nếu **chỉ** trừ `OnHand` mà quên trả `Reserved` thì `Available` **giảm hai lần** cho CÙNG một món
        // ⇒ hệ thống tưởng hết hàng ⇒ **mất doanh thu** mà không có lỗi nào. Ngược lại (chỉ trả `Reserved`)
        // ⇒ kho **thừa hàng ảo** ⇒ **bán thứ không có**.
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");

        item.ApplyMovement(StockMovementType.Receipt, 10);      // nhập 10
        item.ApplyMovement(StockMovementType.Reservation, 3);   // khách giữ 3

        Assert.Equal(10, item.OnHand);
        Assert.Equal(3, item.Reserved);
        Assert.Equal(7, item.Available);

        // ── Chốt: xuất kho (`Sale`) + trả giữ chỗ (`ReservationRelease`) ──
        item.ApplyMovement(StockMovementType.Sale, 3);
        item.ApplyMovement(StockMovementType.ReservationRelease, 3);

        Assert.Equal(7, item.OnHand);       // 10 - 3
        Assert.Equal(0, item.Reserved);     // 3 - 3
        Assert.Equal(7, item.Available);    // 🚨 KHÔNG ĐỔI so với trước khi chốt
    }

    [Fact]
    public void Chot_HAI_LAN_cung_mot_luot__neu_khong_chan_thi_Available_SAI_di()
    {
        // Đối chứng số học: nếu tầng gọi cho chốt hai lần (bug hoặc retry), `Available` sẽ sai:
        // lần hai trừ thêm 3 ⇒ Available = 4 ⇒ hệ thống tưởng còn ít hàng hơn thực tế.
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");
        item.ApplyMovement(StockMovementType.Receipt, 10);
        item.ApplyMovement(StockMovementType.Reservation, 3);
        item.ApplyMovement(StockMovementType.Sale, 3);
        item.ApplyMovement(StockMovementType.ReservationRelease, 3);

        var availableSauMotLan = item.Available;

        // Lần hai: `ReservationRelease` sẽ NÉM vì `Reserved` = 0 (bất biến của entity chặn).
        Assert.Throws<BusinessException>(() => item.ApplyMovement(StockMovementType.ReservationRelease, 3));
        Assert.Equal(availableSauMotLan, item.Available);   // 🚨 KHÔNG thay đổi gì
    }

    [Fact]
    public void Giu_cho_nhieu_hon_ton_thi_khong_bao_gio_chot_duoc__Available_am_bi_chan()
    {
        // Chuỗi thật: giữ 5 khi chỉ có 3 ⇒ `KiemTra` ở `INV-002` đã chặn từ đầu (test ở `QuyetDinhGiuChoTests`).
        // Ở đây khoá thêm: nếu dữ liệu đã hỏng thì `ApplyMovement` **vẫn** chặn `Reserved > OnHand`.
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");
        item.ApplyMovement(StockMovementType.Receipt, 3);

        Assert.Throws<BusinessException>(() => item.ApplyMovement(StockMovementType.Reservation, 5));
        Assert.Equal(3, item.Available);
    }
}
