using eCommerce.Inventory;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🛡️ **BẤT BIẾN SỐ CỦA `StockMovementType`** (`INV-001` **ADR-04**).
///
/// 🚨 **VÌ SAO TEST NÀY TỒN TẠI:** `StockMovementType` được lưu **dạng SỐ** trong `InvStockLedger.MovementType`.
/// Nếu ai đó **đổi số** của một giá trị cũ (hoặc chèn giá trị mới vào **giữa**), thì **toàn bộ lịch sử kho bị
/// diễn giải lại**: một bút toán *"Nhập kho"* (`0`) bỗng thành *"Giữ chỗ"* nếu `0` bị gán lại — **sai sổ cái mà
/// không có lỗi biên dịch, không exception, không log**. Đây là loại lỗi **không thể phát hiện bằng đọc code**.
///
/// 📌 Test khoá **giá trị số cụ thể** của từng thành viên ⇒ mọi thay đổi phá vỡ lịch sử **ĐỎ ngay**.
/// </summary>
public class StockMovementTypeTests
{
    [Theory]
    [InlineData(StockMovementType.Receipt, 0)]
    [InlineData(StockMovementType.Reservation, 1)]
    [InlineData(StockMovementType.ReservationRelease, 2)]
    [InlineData(StockMovementType.Sale, 3)]
    [InlineData(StockMovementType.CustomerReturn, 4)]
    [InlineData(StockMovementType.Adjustment, 5)]
    [InlineData(StockMovementType.Transfer, 6)]
    [InlineData(StockMovementType.StocktakeGain, 7)]
    [InlineData(StockMovementType.StocktakeLoss, 8)]
    [InlineData(StockMovementType.TransferOut, 9)]
    [InlineData(StockMovementType.TransferIn, 10)]
    public void Gia_tri_so_KHONG_duoc_doi(StockMovementType loai, int soMongDoi)
    {
        Assert.Equal(soMongDoi, (int)loai);
    }

    [Fact]
    public void Bon_loai_MOI_nam_o_CUOI__khong_chen_vao_giua()
    {
        // `INV-001` ADR-04: 4 loại mới (kiểm kê thừa/thiếu, chuyển kho đi/đến) **PHẢI** ở cuối dải.
        // Chèn vào giữa = đổi số của giá trị cũ = diễn giải lại lịch sử.
        Assert.All(
            new[] { StockMovementType.StocktakeGain, StockMovementType.StocktakeLoss,
                    StockMovementType.TransferOut, StockMovementType.TransferIn },
            x => Assert.True((int)x > (int)StockMovementType.Transfer));
    }

    [Fact]
    public void So_luong_thanh_vien_dung_bang_11__them_loai_moi_thi_cap_nhat_test_nay()
    {
        // Chốt "có bao nhiêu loại" để việc thêm loại mới là hành động **có ý thức**: người thêm phải
        // (a) thêm vào CUỐI, (b) cập nhật `ApplyMovement` trong `InventoryItem`, (c) cập nhật test này.
        Assert.Equal(11, Enum.GetValues<StockMovementType>().Length);
    }
}

/// <summary>
/// Test **bất biến tồn kho** ở tầng entity (`INV-001`) — lớp **trong**; DB có `CHECK` tương ứng (`ADR-03`).
/// </summary>
public class InventoryItemBatBienTests
{
    private static InventoryItem Tao(int onHand = 0, int reserved = 0)
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");

        if (onHand > 0)
        {
            item.ApplyMovement(StockMovementType.Receipt, onHand);
        }

        if (reserved > 0)
        {
            item.ApplyMovement(StockMovementType.Reservation, reserved);
        }

        return item;
    }

    [Fact]
    public void Nhap_kho_roi_giu_cho__Available_dung()
    {
        var item = Tao(onHand: 10, reserved: 3);

        Assert.Equal(10, item.OnHand);
        Assert.Equal(3, item.Reserved);
        Assert.Equal(7, item.Available);
    }

    [Fact]
    public void Xuat_kho_lam_AM_ton_thi_NEM()
    {
        var item = Tao(onHand: 5);

        Assert.Throws<InvalidOperationException>(() => item.ApplyMovement(StockMovementType.Sale, 6));
    }

    [Fact]
    public void Giu_cho_VUOT_ton_thuc_thi_NEM__bat_bien_MOI_cua_INV001()
    {
        // 🚨 Trước `INV-001`, chỉ kiểm `OnHand/Reserved >= 0` ⇒ `Reserved > OnHand` **LỌT** ⇒ `Available` **ÂM**
        // ⇒ hệ thống tưởng CÒN hàng để bán (bán thứ không có). Test này khoá bất biến đã bổ sung.
        var item = Tao(onHand: 5);

        Assert.Throws<InvalidOperationException>(() => item.ApplyMovement(StockMovementType.Reservation, 6));
        Assert.Equal(5, item.OnHand);
        Assert.Equal(0, item.Reserved);
    }

    [Fact]
    public void Bon_loai_MOI_cua_INV001_phai_AP_DUNG_DUOC__khong_roi_vao_default()
    {
        // Nếu `ApplyMovement` quên 4 loại mới thì nó **NÉM** `ArgumentOutOfRangeException` ⇒ nghiệp vụ kiểm kê
        // và chuyển kho **không chạy được**. Test này khoá việc đã xử lý đủ.
        var item = Tao(onHand: 10);

        item.ApplyMovement(StockMovementType.StocktakeGain, 2);
        Assert.Equal(12, item.OnHand);

        item.ApplyMovement(StockMovementType.StocktakeLoss, 3);
        Assert.Equal(9, item.OnHand);

        item.ApplyMovement(StockMovementType.TransferOut, 4);
        Assert.Equal(5, item.OnHand);

        item.ApplyMovement(StockMovementType.TransferIn, 1);
        Assert.Equal(6, item.OnHand);
    }

    [Fact]
    public void Nha_gio_cho_lam_Reserved_AM_thi_NEM()
    {
        var item = Tao(onHand: 10);

        Assert.Throws<InvalidOperationException>(() => item.ApplyMovement(StockMovementType.ReservationRelease, 1));
    }

    [Fact]
    public void StockLedgerEntry_PHAI_gan_ReferenceLineId__bai_hoc_PRC001_L2()
    {
        // 🚨 Bài học `PRC-001` §17.2 **L2** (constructor chỉ KIỂM TRA mà quên GẮN ⇒ FK/unique vô hiệu).
        // `ReferenceLineId` là **cơ sở** của hàng rào chống ghi trùng sổ cái (`ADR-05`): quên gán ⇒ cột NULL
        // ⇒ bộ lọc `IS NOT NULL` **loại bỏ** bản ghi khỏi unique index ⇒ **ghi trùng vẫn lọt**.
        var dongChungTu = Guid.NewGuid();

        var but = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-1", StockMovementType.Receipt, 5, 5, DateTime.UtcNow,
            referenceType: "GoodsReceipt", referenceId: "PN-1", channelCode: null, referenceLineId: dongChungTu);

        Assert.Equal(dongChungTu, but.ReferenceLineId);
        Assert.Equal(5, but.QuantityDelta);
        Assert.Equal(5, but.BalanceAfter);
    }
}
