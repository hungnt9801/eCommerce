using eCommerce.Inventory;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🆕 **`WP-04`** — TỒN KHO 9 CHIỀU (`docs/nhanh/07` WP-04 việc 1–3).
///
/// 🚨 **VÌ SAO BỘ TEST NÀY LÀ ĐIỀU KIỆN XANH CỦA `N4`:** mỗi chiều tồn là **một con số bán được** (hoặc không
/// bán được). Sai dấu ở **một** chiều nghĩa là hoặc **bán thứ không có**, hoặc **không bán được thứ đang có** —
/// cả hai đều **không có lỗi nào nổi lên**. Vì vậy mỗi nhánh `ApplyMovement` có đúng **một** ca khẳng định
/// "chiều nào đổi, chiều nào KHÔNG".
/// </summary>
public class Wp04TonKhoChieuTests
{
    private static InventoryItem Kho(int onHand = 0)
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-WP04");

        if (onHand > 0)
        {
            item.ApplyMovement(StockMovementType.Receipt, onHand);
        }

        return item;
    }

    [Fact]
    public void Nhap_10__OnHand_10_va_moi_chieu_khac_bang_0()
    {
        // ✅ Điều kiện xanh của `N4`: "nhập 10 → tồn 10".
        var item = Kho(10);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(10, item.InWarehouse);
        Assert.Equal(10, item.Available);
        Assert.Equal(
            0,
            item.Reserved + item.Transferring + item.Shipping + item.Holding + item.Damaged
            + item.Warranty + item.WarrantyHolding + item.PreOrder + item.WaitingInbound);
    }

    [Fact]
    public void Tam_giu_thu_cong__KHONG_doi_OnHand_nhung_GIAM_Available()
    {
        var item = Kho(10);

        item.ApplyMovement(StockMovementType.Hold, 3);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(3, item.Holding);
        Assert.Equal(7, item.Available);

        item.ApplyMovement(StockMovementType.HoldRelease, 2);

        Assert.Equal(1, item.Holding);
        Assert.Equal(9, item.Available);
    }

    [Fact]
    public void Hang_loi__VAN_trong_kho_nen_OnHand_KHONG_doi__nhung_khong_ban_duoc()
    {
        // 🚨 Ca dễ sai nhất: nếu `DamagedIn` **giảm** `OnHand` thì `Available` bị trừ **HAI lần**
        // (một lần ở OnHand, một lần ở Damaged) ⇒ báo hết hàng oan.
        var item = Kho(10);

        item.ApplyMovement(StockMovementType.DamagedIn, 2);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(2, item.Damaged);
        Assert.Equal(8, item.Available);

        // Xuất huỷ hàng lỗi ⇒ ra khỏi kho hẳn: OnHand giảm, Damaged giảm, Available **không đổi**.
        item.ApplyMovement(StockMovementType.DamagedOut, 2);

        Assert.Equal(8, item.OnHand);
        Assert.Equal(0, item.Damaged);
        Assert.Equal(8, item.Available);
    }

    [Fact]
    public void Xuat_di_giao__OnHand_giam__Shipping_giu_dau_vet()
    {
        var item = Kho(10);

        item.ApplyMovement(StockMovementType.ShippingOut, 4);

        Assert.Equal(6, item.OnHand);
        Assert.Equal(4, item.Shipping);
        Assert.Equal(6, item.Available);

        item.ApplyMovement(StockMovementType.ShippingReturn, 1);

        Assert.Equal(7, item.OnHand);
        Assert.Equal(3, item.Shipping);
    }

    [Fact]
    public void Gui_bao_hanh__OnHand_giam__Warranty_tang()
    {
        var item = Kho(10);

        item.ApplyMovement(StockMovementType.WarrantyHold, 2);

        Assert.Equal(8, item.OnHand);
        Assert.Equal(2, item.Warranty);

        item.ApplyMovement(StockMovementType.WarrantyRelease, 2);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(0, item.Warranty);
    }

    [Fact]
    public void Ban_le_KHI_het_hang_kha_dung_thi_NEM__du_OnHand_con()
    {
        // 🚨 Đây là ca chứng minh vì sao `Available` phải có `Reserved` trong công thức: `OnHand = 10` nhưng
        // 8 cái đã hứa cho đơn khác ⇒ bán 3 là bán thứ **không còn**.
        var item = Kho(10);
        item.ApplyMovement(StockMovementType.Reservation, 8);

        Assert.Equal(2, item.Available);
        Assert.Throws<BusinessException>(() => item.ApplyMovement(StockMovementType.RetailSaleOut, 3));

        // Bất biến "tính trước — kiểm — rồi mới gán": ném lỗi thì entity KHÔNG bị biến đổi.
        Assert.Equal(10, item.OnHand);
    }

    [Fact]
    public void Giu_cho_thu_cong_vuot_Available_thi_NEM()
    {
        var item = Kho(10);
        item.ApplyMovement(StockMovementType.DamagedIn, 8);

        Assert.Equal(2, item.Available);
        Assert.Equal(
            eCommerceConsts.ErrorCodes.InsufficientStock,
            Assert.Throws<BusinessException>(() => item.ApplyMovement(StockMovementType.Hold, 3)).Code);
        Assert.Equal(0, item.Holding);
    }

    [Fact]
    public void Moi_loai_bien_dong_deu_AP_DUNG_DUOC__khong_roi_vao_default()
    {
        // Nếu `ApplyMovement` quên 11 loại mới thì nó **NÉM** `ArgumentOutOfRangeException` ⇒ phiếu kho kiểu
        // Nhanh **không ghi sổ được**. Test này khoá việc đã xử lý đủ.
        var item = Kho(0);

        foreach (var loai in new[]
                 {
                     StockMovementType.ShippingOut, StockMovementType.ShippingReturn,
                     StockMovementType.DamagedIn, StockMovementType.DamagedOut,
                     StockMovementType.WarrantyHold, StockMovementType.WarrantyRelease,
                     StockMovementType.Hold, StockMovementType.HoldRelease,
                     StockMovementType.RetailSaleOut, StockMovementType.SupplierReturnOut,
                     StockMovementType.OtherOut
                 })
        {
            // 🚨 Các chiều **nhả** (`ShippingReturn`, `WarrantyRelease`, `DamagedOut`, `HoldRelease`) cần chiều
            // tương ứng có hàng trước, nếu không sẽ ném vì chiều bị ÂM — đó là hành vi ĐÚNG, nên nạp trước.
            var x = Kho(100);

            var cap = loai switch
            {
                StockMovementType.ShippingReturn => StockMovementType.ShippingOut,
                StockMovementType.WarrantyRelease => StockMovementType.WarrantyHold,
                StockMovementType.DamagedOut => StockMovementType.DamagedIn,
                StockMovementType.HoldRelease => StockMovementType.Hold,
                _ => (StockMovementType?)null
            };

            if (cap is { } c)
            {
                x.ApplyMovement(c, 5);
            }

            x.ApplyMovement(loai, 5);
        }
    }
}

/// <summary>
/// 🆕 **`WP-04`** — **BẢNG ÁNH XẠ `(mode, chiều) → loại biến động`** (`TinhButToanChungTu`).
///
/// 🚨 **VÌ SAO PHẢI KHOÁ BẢNG NÀY:** cùng là *"phiếu xuất"* nhưng `Delivery` phải ghi `ShippingOut` (hàng còn
/// trên đường), `Retail` ghi `RetailSaleOut`, `Supplier` ghi `SupplierReturnOut`, `WarrantyBill` ghi `WarrantyHold`.
/// Ghi sai loại thì **báo cáo doanh thu / trả NCC / bảo hành sai toàn bộ** mà **không có lỗi nào**.
/// </summary>
public class Wp04BangAnhXaButToanTests
{
    [Theory]
    [InlineData(StockDocumentMode.Delivery, StockDocumentDirection.Out, StockMovementType.ShippingOut)]
    [InlineData(StockDocumentMode.Retail, StockDocumentDirection.Out, StockMovementType.RetailSaleOut)]
    [InlineData(StockDocumentMode.RetailGift, StockDocumentDirection.Out, StockMovementType.RetailSaleOut)]
    [InlineData(StockDocumentMode.Wholesale, StockDocumentDirection.Out, StockMovementType.Sale)]
    [InlineData(StockDocumentMode.WholesaleGift, StockDocumentDirection.Out, StockMovementType.Sale)]
    [InlineData(StockDocumentMode.OrderGift, StockDocumentDirection.Out, StockMovementType.Sale)]
    [InlineData(StockDocumentMode.Combo, StockDocumentDirection.Out, StockMovementType.Sale)]
    [InlineData(StockDocumentMode.Supplier, StockDocumentDirection.Out, StockMovementType.SupplierReturnOut)]
    [InlineData(StockDocumentMode.Other, StockDocumentDirection.Out, StockMovementType.OtherOut)]
    [InlineData(StockDocumentMode.WarrantyBill, StockDocumentDirection.Out, StockMovementType.WarrantyHold)]
    [InlineData(StockDocumentMode.WarrantyCenter, StockDocumentDirection.Out, StockMovementType.WarrantyHold)]
    [InlineData(StockDocumentMode.RepairBill, StockDocumentDirection.Out, StockMovementType.WarrantyHold)]
    [InlineData(StockDocumentMode.WarrantyPart, StockDocumentDirection.Out, StockMovementType.WarrantyHold)]
    [InlineData(StockDocumentMode.Supplier, StockDocumentDirection.In, StockMovementType.Receipt)]
    [InlineData(StockDocumentMode.RetailGift, StockDocumentDirection.In, StockMovementType.Receipt)]
    [InlineData(StockDocumentMode.Combo, StockDocumentDirection.In, StockMovementType.Receipt)]
    [InlineData(StockDocumentMode.WarrantyPart, StockDocumentDirection.In, StockMovementType.Receipt)]
    [InlineData(StockDocumentMode.Other, StockDocumentDirection.In, StockMovementType.Adjustment)]
    [InlineData(StockDocumentMode.WarrantyBill, StockDocumentDirection.In, StockMovementType.WarrantyRelease)]
    [InlineData(StockDocumentMode.WarrantyCenter, StockDocumentDirection.In, StockMovementType.WarrantyRelease)]
    [InlineData(StockDocumentMode.RepairBill, StockDocumentDirection.In, StockMovementType.WarrantyRelease)]
    public void Mode_va_chieu_chon_dung_loai_bien_dong(
        StockDocumentMode mode, StockDocumentDirection huong, StockMovementType mongDoi)
    {
        Assert.Equal(mongDoi, TinhButToanChungTu.ChonBienDongTheoMode(mode, huong));
    }

    [Fact]
    public void KHONG_co_mode__giu_nguyen_hanh_vi_cu__phieu_nhap_ghi_Receipt()
    {
        var kho = Guid.NewGuid();

        var but = TinhButToanChungTu.ChoTungDongTheoMode(
            StockDocumentType.GoodsReceipt, mode: null, huong: null, soLuong: 5, khoNguon: kho, khoDich: null);

        var mot = Assert.Single(but);
        Assert.Equal(StockMovementType.Receipt, mot.Loai);
        Assert.Equal(5, mot.DoLon);
    }

    [Fact]
    public void Chuyen_kho_luon_sinh_HAI_but_toan__mode_khong_doi_duoc()
    {
        var nguon = Guid.NewGuid();
        var dich = Guid.NewGuid();

        var but = TinhButToanChungTu.ChoTungDongTheoMode(
            StockDocumentType.StockTransfer, StockDocumentMode.Transfer, StockDocumentDirection.Out,
            soLuong: 3, khoNguon: nguon, khoDich: dich);

        Assert.Equal(2, but.Count);
        Assert.Contains(but, x => x.Loai == StockMovementType.TransferOut && x.WarehouseId == nguon);
        Assert.Contains(but, x => x.Loai == StockMovementType.TransferIn && x.WarehouseId == dich);
    }

    [Fact]
    public void Khach_tra_hang__loai_chung_tu_thang_mode__ghi_CustomerReturn()
    {
        var but = TinhButToanChungTu.ChoTungDongTheoMode(
            StockDocumentType.CustomerReturn, StockDocumentMode.Retail, StockDocumentDirection.In,
            soLuong: 2, khoNguon: Guid.NewGuid(), khoDich: null);

        Assert.Equal(StockMovementType.CustomerReturn, Assert.Single(but).Loai);
    }

    [Fact]
    public void Kiem_ke__KHONG_di_qua_bang_mode__phai_dung_ChoTungKiemKe()
    {
        // Kiểm kê ghi phần **LỆCH** (so với tồn trên sổ), không phải một chiều cố định.
        Assert.Throws<ArgumentOutOfRangeException>(() => TinhButToanChungTu.ChoTungDongTheoMode(
            StockDocumentType.Stocktake, StockDocumentMode.Stocktake, huong: null,
            soLuong: 2, khoNguon: Guid.NewGuid(), khoDich: null));
    }
}

/// <summary>
/// 🆕 **`WP-04`** — **BẤT BIẾN `tổng sổ cái = ảnh chụp`** (điều kiện xanh của `N4`).
/// 📌 Dùng **chính** hàm đối soát của sản phẩm (`DoiSoatTonKho.Tong`) chứ **không** viết lại công thức trong
/// test: viết lại thì test và sản phẩm có thể **sai giống nhau** ⇒ test xanh mà tồn kho vẫn lệch.
/// </summary>
public class Wp04BatBienSoCaiAnhChupTests
{
    [Fact]
    public void Sau_chuoi_bien_dong__tong_so_cai_BANG_OnHand()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-WP04");
        var soCai = new List<(StockMovementType Loai, decimal DoLon)>();

        void Ap(StockMovementType loai, int q)
        {
            item.ApplyMovement(loai, q);
            soCai.Add((loai, q));
        }

        Ap(StockMovementType.Receipt, 10);
        Ap(StockMovementType.Reservation, 4);
        Ap(StockMovementType.Hold, 1);
        Ap(StockMovementType.DamagedIn, 1);
        Ap(StockMovementType.ShippingOut, 2);
        Ap(StockMovementType.ShippingReturn, 1);
        Ap(StockMovementType.WarrantyHold, 2);
        Ap(StockMovementType.WarrantyRelease, 1);
        Ap(StockMovementType.ReservationRelease, 4);
        Ap(StockMovementType.RetailSaleOut, 3);
        Ap(StockMovementType.DamagedOut, 1);
        Ap(StockMovementType.HoldRelease, 1);

        Assert.Equal(item.OnHand, DoiSoatTonKho.Tong(soCai));
        Assert.Equal(4, item.OnHand);
    }

    [Fact]
    public void But_toan_THUA_thi_bat_duoc_lech__phep_do_KHONG_luon_xanh()
    {
        // 🚨 Mutation test: nếu `Tong` bỏ qua loại mới (hoặc trả 0 cho `ShippingOut`) thì ca trên vẫn có thể
        // xanh một cách vô nghĩa. Ca này chứng minh phép so **thật sự** so.
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-WP04");
        item.ApplyMovement(StockMovementType.Receipt, 10);
        item.ApplyMovement(StockMovementType.ShippingOut, 4);

        var soCai = new List<(StockMovementType Loai, decimal DoLon)>
        {
            (StockMovementType.Receipt, 10),
            (StockMovementType.ShippingOut, 3) // ghi thiếu 1 ⇒ phải lệch
        };

        Assert.NotEqual(item.OnHand, DoiSoatTonKho.Tong(soCai));
    }

    [Theory]
    [InlineData(StockMovementType.ShippingOut, -1)]
    [InlineData(StockMovementType.ShippingReturn, +1)]
    [InlineData(StockMovementType.DamagedIn, 0)]
    [InlineData(StockMovementType.DamagedOut, -1)]
    [InlineData(StockMovementType.WarrantyHold, -1)]
    [InlineData(StockMovementType.WarrantyRelease, +1)]
    [InlineData(StockMovementType.Hold, 0)]
    [InlineData(StockMovementType.HoldRelease, 0)]
    [InlineData(StockMovementType.RetailSaleOut, -1)]
    [InlineData(StockMovementType.SupplierReturnOut, -1)]
    [InlineData(StockMovementType.OtherOut, -1)]
    public void Dau_cua_11_loai_moi_dung_voi_chieu_tac_dong_len_OnHand(StockMovementType loai, int dau)
    {
        // 🚨 Thiếu nhánh ở `DoiSoatTonKho.Dau` ⇒ job đối soát **NÉM** khi gặp loại mới ⇒ job chết ⇒
        // không còn ai phát hiện tồn kho sai. Test khoá bảng dấu.
        Assert.Equal(dau, DoiSoatTonKho.Dau(loai));
    }
}

/// <summary>
/// 🆕 **`WP-04`** — **PHIẾU KHO KIỂU NHANH**: chiều, kiểu phiếu, đối tác, nguồn, tiền, ĐVT/VAT/lô/serial.
/// 📌 Mỗi ca khoá một **luật miền** — DB chỉ là hàng rào cuối, và `500` từ DB không giúp người dùng biết sửa gì.
/// </summary>
public class Wp04PhieuKhoNhanhTests
{
    private static StockDocument Phieu(StockDocumentType type = StockDocumentType.GoodsReceipt,
        StockDocumentMode? mode = null, Guid? toWarehouseId = null)
        => new(Guid.NewGuid(), type, "PN-WP04", Guid.NewGuid(), DateTime.UtcNow, toWarehouseId, mode);

    [Fact]
    public void Chieu_duoc_SUY_TU_loai_phieu__nhap_In__chuyen_Out__kiem_ke_null()
    {
        Assert.Equal(StockDocumentDirection.In, Phieu().Direction);

        Assert.Equal(StockDocumentDirection.In,
            Phieu(StockDocumentType.CustomerReturn).Direction);

        Assert.Equal(StockDocumentDirection.Out,
            Phieu(StockDocumentType.StockTransfer, StockDocumentMode.Transfer, Guid.NewGuid()).Direction);

        // 📌 Kiểm kê **KHÔNG** có một chiều duy nhất: một phiếu có thể vừa thừa vừa thiếu.
        Assert.Null(Phieu(StockDocumentType.Stocktake, StockDocumentMode.Stocktake).Direction);
    }

    [Fact]
    public void Phieu_XUAT_GoodsIssue_co_chieu_Out__va_BAT_BUOC_co_mode()
    {
        // 🆕 `WP-04` — trước đây enum **không có** loại phiếu xuất nào: bán lẻ/giao hàng/trả NCC phải "mượn"
        // `StockTransfer` (báo cáo chuyển kho đếm cả hàng bán). Ca này khoá sự tồn tại của `GoodsIssue`.
        var phieuBan = Phieu(StockDocumentType.GoodsIssue, StockDocumentMode.Retail);

        Assert.Equal(StockDocumentDirection.Out, phieuBan.Direction);

        // Không có mode ⇒ hệ thống **không biết** hàng đi vì lý do gì ⇒ phải NÉM, không được đoán.
        Assert.Equal(InventoryErrorCodes.PhieuXuatThieuMode,
            Assert.Throws<BusinessException>(() => Phieu(StockDocumentType.GoodsIssue)).Code);
    }

    [Fact]
    public void Mode_MAU_THUAN_loai_phieu_thi_NEM__khong_de_bao_cao_lech()
    {
        // Chuyển kho mà khai `Supplier` ⇒ báo cáo trả NCC sẽ đếm cả phiếu chuyển kho.
        Assert.Equal(InventoryErrorCodes.KieuPhieuKhongKhopLoai,
            Assert.Throws<BusinessException>(() => Phieu(
                StockDocumentType.StockTransfer, StockDocumentMode.Supplier, Guid.NewGuid())).Code);

        // Kiểm kê mà khai `Retail` ⇒ phiếu kiểm kê lọt vào báo cáo bán lẻ.
        Assert.Equal(InventoryErrorCodes.KieuPhieuKhongKhopLoai,
            Assert.Throws<BusinessException>(() => Phieu(
                StockDocumentType.Stocktake, StockDocumentMode.Retail)).Code);
    }

    [Fact]
    public void Doi_tac_THIEU_loai_thi_NEM__khong_biet_tra_bang_nao()
    {
        var phieu = Phieu();

        Assert.Equal(InventoryErrorCodes.ThieuLoaiDoiTac,
            Assert.Throws<BusinessException>(() => phieu.DatDoiTac(null, Guid.NewGuid())).Code);

        phieu.DatDoiTac(StockCounterpartyType.Supplier, Guid.NewGuid());
        Assert.Equal(StockCounterpartyType.Supplier, phieu.CounterpartyType);
    }

    [Fact]
    public void Nguon_THIEU_loai_thi_NEM__khong_biet_doi_chieu_voi_chung_tu_nao()
    {
        var phieu = Phieu();

        Assert.Equal(InventoryErrorCodes.ThieuLoaiNguon,
            Assert.Throws<BusinessException>(() => phieu.DatNguon(null, "DH-001")).Code);

        phieu.DatNguon("Order", "DH-001");
        Assert.Equal("Order", phieu.RefType);
        Assert.Equal("DH-001", phieu.RefId);
    }

    [Fact]
    public void Tien_AM_thi_NEM__tien_am_khong_phai_hoan_tien()
    {
        var phieu = Phieu();

        Assert.Equal(InventoryErrorCodes.TienTrenPhieuKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.DatTien(-1m, null)).Code);

        Assert.Equal(InventoryErrorCodes.TienTrenPhieuKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.DatTien(null, -0.5m)).Code);

        phieu.DatTien(1_000m, 500m);
        Assert.Equal(1_000m, phieu.DiscountAmount);
        Assert.Equal(500m, phieu.PaidAmount);
    }

    [Fact]
    public void VAT_ngoai_0_100_thi_NEM()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 1);

        Assert.Equal(InventoryErrorCodes.VatPercentKhongHopLe,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(vatPercent: 101m)).Code);

        Assert.Equal(InventoryErrorCodes.VatPercentKhongHopLe,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(vatPercent: -1m)).Code);
    }

    [Fact]
    public void So_serial_KHAC_so_luong_thi_NEM__neu_khong_se_co_don_vi_khong_truy_vet_duoc()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 3);

        Assert.Equal(InventoryErrorCodes.SoSerialKhacSoLuong,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(imeiSerials: "SN1,SN2")).Code);
    }

    [Fact]
    public void Serial_TRUNG_trong_cung_dong_thi_NEM()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 2);

        Assert.Equal(InventoryErrorCodes.SerialTrungTrongDong,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(imeiSerials: "SN1,SN1")).Code);
    }

    [Fact]
    public void Lo_VA_serial_CUNG_LUC_thi_NEM__wp04_chua_co_duong_ghi_lo()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 1);

        Assert.Equal(InventoryErrorCodes.LoVaSerialCungLuc,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(batchId: Guid.NewGuid(), imeiSerials: "SN1")).Code);
    }

    [Fact]
    public void Serial_hop_le_thi_luu_CHUAN_HOA__chuoi_rong_thanh_null()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 3);

        dong.DatChiTiet(vatPercent: 8m, imeiSerials: " SN3, SN1 ,SN2 ");

        Assert.Equal("SN3,SN1,SN2", dong.ImeiSerials);
        Assert.Equal(8m, dong.VatPercent);

        var khongSerial = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-2", 1);
        khongSerial.DatChiTiet(imeiSerials: "   ");

        // 📌 Hai cách diễn đạt cùng một ý ("không quản serial") ⇒ chuẩn hoá về MỘT giá trị (`null`).
        Assert.Null(khongSerial.ImeiSerials);
    }

    [Theory]
    [InlineData(StockDocumentMode.Delivery, 1)]
    [InlineData(StockDocumentMode.Retail, 2)]
    [InlineData(StockDocumentMode.Transfer, 3)]
    [InlineData(StockDocumentMode.RetailGift, 4)]
    [InlineData(StockDocumentMode.Supplier, 5)]
    [InlineData(StockDocumentMode.Wholesale, 6)]
    [InlineData(StockDocumentMode.Stocktake, 8)]
    [InlineData(StockDocumentMode.Other, 10)]
    [InlineData(StockDocumentMode.WarrantyBill, 13)]
    [InlineData(StockDocumentMode.WarrantyCenter, 14)]
    [InlineData(StockDocumentMode.RepairBill, 15)]
    [InlineData(StockDocumentMode.WarrantyPart, 16)]
    [InlineData(StockDocumentMode.WholesaleGift, 17)]
    [InlineData(StockDocumentMode.OrderGift, 18)]
    [InlineData(StockDocumentMode.Combo, 19)]
    public void So_cua_Mode_PHAI_trung_danh_muc_Nhanh(StockDocumentMode mode, int soNhanh)
    {
        // 🚨 Số này là **hợp đồng dữ liệu** với Nhanh/Open API: đánh lại số ⇒ mọi phiếu nhập từ Nhanh bị
        // **diễn giải sai** (mode 5 "Nhà cung cấp" thành "Bán sỉ"…), không có lỗi nào.
        Assert.Equal(soNhanh, (int)mode);
    }

    [Fact]
    public void Danh_muc_Mode_dung_15_gia_tri__KHONG_co_7_9_11_12()
    {
        Assert.Equal(15, Enum.GetValues<StockDocumentMode>().Length);
        Assert.All(new[] { 7, 9, 11, 12 }, so => Assert.DoesNotContain(so, Enum.GetValues<StockDocumentMode>().Select(x => (int)x)));
    }
}

/// <summary>
/// 🆕 **`WP-04b`** — **SỐ LƯỢNG THẬP PHÂN** (`int → decimal(18,3)`, D-31).
///
/// 🚨 **VÌ SAO PHẢI CÓ BỘ TEST NÀY:** nếu ai đó "dọn" kiểu về `int` (hoặc thêm `(int)` ở một chỗ quy đổi), thì
/// **1,5 kg thành 1 kg** — bán thiếu hàng cho khách hoặc thừa tồn **mà không có lỗi nào**. Mỗi ca dưới đây
/// khẳng định số lẻ **sống sót** qua từng tầng: entity → bút toán → dòng phiếu → DTO của API.
/// </summary>
public class Wp04bSoLuongThapPhanTests
{
    [Fact]
    public void Nhap_1_5_kg__OnHand_va_Available_GIU_so_le()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE");

        item.ApplyMovement(StockMovementType.Receipt, 1.5m);
        item.ApplyMovement(StockMovementType.Sale, 0.25m);

        Assert.Equal(1.25m, item.OnHand);   // 1,5 − 0,25
        Assert.Equal(1.25m, item.Available);
        Assert.Equal(1.25m, item.InWarehouse);
    }

    [Fact]
    public void Ba_so_thap_phan_la_DU__va_khong_lam_tron_khi_cong_don()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE");

        // 0,001 × 1000 = 1 (nếu có chỗ nào làm tròn về 2 số ⇒ tổng sẽ lệch 0,000)
        for (var i = 0; i < 1000; i++)
        {
            item.ApplyMovement(StockMovementType.Receipt, 0.001m);
        }

        Assert.Equal(1.000m, item.OnHand);
        Assert.Equal(1.000m, item.Available);
    }

    [Fact]
    public void But_toan_giu_so_le__khong_tra_ve_int()
    {
        var but = TinhButToanChungTu.ChoTungDongTheoMode(
            StockDocumentType.GoodsReceipt, StockDocumentMode.Supplier, StockDocumentDirection.In,
            soLuong: 2.5m, khoNguon: Guid.NewGuid(), khoDich: null);

        Assert.Equal(2.5m, Assert.Single(but).DoLon);
    }

    [Fact]
    public void Dong_phieu_nhan_so_le__va_serial_van_phai_bang_so_luong()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE", 2.5m);

        Assert.Equal(2.5m, dong.Quantity);

        // 🚨 Bất biến serial **giữ nguyên** với số lẻ: 2,5 không thể có 2,5 serial ⇒ phải NÉM, không được bỏ qua.
        Assert.Equal(InventoryErrorCodes.SoSerialKhacSoLuong,
            Assert.Throws<BusinessException>(() => dong.DatChiTiet(imeiSerials: "SN1,SN2")).Code);
    }

    [Fact]
    public void So_luong_0_hoac_AM_thi_NEM__nhung_so_duong_RAT_NHO_thi_HOP_LE()
    {
        Assert.Equal(InventoryErrorCodes.SoLuongPhaiDuong,
            Assert.Throws<BusinessException>(() =>
                new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE", 0m)).Code);

        Assert.Equal(InventoryErrorCodes.SoLuongPhaiDuong,
            Assert.Throws<BusinessException>(() =>
                new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE", -0.5m)).Code);

        // 📌 0,0001 > 0 ⇒ HỢP LỆ. Nếu ai đó làm tròn về 0 trước khi kiểm thì ca này sẽ **bị từ chối oan**.
        Assert.Equal(0.0001m, new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-LE", 0.0001m).Quantity);
    }
}