using eCommerce.Inventory;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **quyết định bút toán sổ cái** (`INV-001`) — hàm **THUẦN**.
///
/// 🚨 **VÌ SAO QUAN TRỌNG NHẤT TRONG MIỀN KHO:** sai một **dấu** ở đây ⇒ tồn kho sai **toàn hệ thống**:
/// *"xuất kho"* thành *"nhập kho"* ⇒ hệ thống tưởng **có gấp đôi** số hàng thật ⇒ **bán thứ không có**.
/// Không có lỗi biên dịch, không exception — chỉ có số liệu sai dần.
/// </summary>
public class TinhButToanChungTuTests
{
    private static readonly Guid KhoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KhoB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ── Phiếu NHẬP: MỘT bút toán, TĂNG tồn ───────────────────────────────────

    [Fact]
    public void Phieu_nhap_sinh_MOT_but_toan_loai_Receipt_o_kho_nhan()
    {
        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.GoodsReceipt, 5, KhoA, null);

        var but = Assert.Single(ke);
        Assert.Equal(KhoA, but.WarehouseId);
        Assert.Equal(StockMovementType.Receipt, but.Loai);
        Assert.Equal(5, but.DoLon);
    }

    [Fact]
    public void Phieu_nhap_KHONG_bao_gio_sinh_but_toan_o_kho_khac()
    {
        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.GoodsReceipt, 1, KhoA, null);

        Assert.DoesNotContain(ke, x => x.WarehouseId == KhoB);
    }

    // ── Phiếu CHUYỂN KHO: HAI bút toán, một ĐI một ĐẾN ───────────────────────

    [Fact]
    public void Chuyen_kho_sinh_HAI_but_toan__DI_o_nguon_va_DEN_o_dich()
    {
        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.StockTransfer, 7, KhoA, KhoB);

        Assert.Equal(2, ke.Count);
        Assert.Contains(ke, x => x.WarehouseId == KhoA && x.Loai == StockMovementType.TransferOut && x.DoLon == 7);
        Assert.Contains(ke, x => x.WarehouseId == KhoB && x.Loai == StockMovementType.TransferIn && x.DoLon == 7);
    }

    [Fact]
    public void Chuyen_kho_THIEU_kho_dich_thi_NEM__thieu_mot_ve_la_mat_hang()
    {
        // Thiếu vế "đến" ⇒ hàng chỉ bị TRỪ ở nguồn ⇒ **hàng biến mất** khỏi hệ thống.
        Assert.Throws<ArgumentException>(
            () => TinhButToanChungTu.ChoTungDong(StockDocumentType.StockTransfer, 1, KhoA, null));
    }

    // ── 🔥 QUY ƯỚC DẤU: LUÔN DƯƠNG, CHIỀU DO `Loai` QUYẾT ĐỊNH ───────────────

    [Fact]
    public void DoLon_LUON_DUONG__chieu_do_Loai_quyet_dinh()
    {
        // 🚨 Nếu truyền số ÂM cho `TransferOut` thì `ApplyMovement` (đã trừ `OnHand`) sẽ **đảo chiều**
        // ⇒ "xuất kho −5" thành **NHẬP 5**. Test này khoá quy ước: hàm thuần trả **độ lớn dương**.
        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.StockTransfer, 3, KhoA, KhoB);

        Assert.All(ke, x => Assert.True(x.DoLon > 0, "Độ lớn phải DƯƠNG — chiều do loại biến động quyết định."));
    }

    [Fact]
    public void So_luong_nho_hon_1_thi_NEM()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TinhButToanChungTu.ChoTungDong(StockDocumentType.GoodsReceipt, 0, KhoA, null));
    }

    [Fact]
    public void Loai_KIEM_KE_di_duong_khac__khong_di_qua_ChoTungDong()
    {
        // Kiểm kê cần **dữ liệu thực tế** (số đếm được so với sổ) ⇒ không thể quyết định chỉ từ loại chứng từ.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TinhButToanChungTu.ChoTungDong(StockDocumentType.Stocktake, 1, KhoA, null));
    }

    // ── KIỂM KÊ: ghi phần LỆCH, khớp thì KHÔNG ghi gì ────────────────────────

    [Fact]
    public void Kiem_ke_THUA_thi_StocktakeGain__THIEU_thi_StocktakeLoss()
    {
        var thua = TinhButToanChungTu.ChoTungKiemKe(KhoA, "SKU-1", soDemDuoc: 12, tonTrenSo: 10);
        Assert.NotNull(thua);
        Assert.Equal(StockMovementType.StocktakeGain, thua!.Loai);
        Assert.Equal(2, thua.DoLon);

        var thieu = TinhButToanChungTu.ChoTungKiemKe(KhoA, "SKU-1", soDemDuoc: 7, tonTrenSo: 10);
        Assert.NotNull(thieu);
        Assert.Equal(StockMovementType.StocktakeLoss, thieu!.Loai);
        Assert.Equal(3, thieu.DoLon);   // độ lớn DƯƠNG dù lệch âm
    }

    [Fact]
    public void Kiem_ke_KHOP_thi_KHONG_sinh_but_toan__khong_ghi_dong_so_cai_0()
    {
        // 🚨 Bút toán 0 là **nhiễu**: nó làm sổ cái dài ra mà không mang thông tin, và DB đã có
        // `CHECK QuantityDelta <> 0` (ADR-03) ⇒ cố ghi sẽ **nổ** ở tầng DB.
        Assert.Null(TinhButToanChungTu.ChoTungKiemKe(KhoA, "SKU-1", soDemDuoc: 10, tonTrenSo: 10));
    }

    [Fact]
    public void Kiem_ke_khi_CHUA_co_ban_ghi_ton__coi_ton_la_0()
    {
        // Chưa từng có `InventoryItem` ⇒ tồn trên sổ = 0 ⇒ đếm được 4 nghĩa là THỪA 4.
        var but = TinhButToanChungTu.ChoTungKiemKe(KhoA, "SKU-MOI", soDemDuoc: 4, tonTrenSo: 0);

        Assert.Equal(StockMovementType.StocktakeGain, but!.Loai);
        Assert.Equal(4, but.DoLon);
    }
}

/// <summary>Test **tên khoá phân tán của kho** (`INV-001` ADR-08; quy ước `XC-003` §5.4).</summary>
public class InventoryLockKeysTests
{
    [Fact]
    public void Khoa_KHONG_co_tien_to_ecommerce__AbpDistributedLock_tu_them()
    {
        // Bài học `PRC-001` **N3**: `PRC-001` §8.2 ghi CÓ tiền tố, ngược `XC-003` §5.4 ⇒ code theo `XC-003`.
        var khoa = InventoryLockKeys.TheoSku(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");

        Assert.False(khoa.StartsWith("ecommerce:", StringComparison.Ordinal), $"Khoá không được có tiền tố: {khoa}");
        Assert.StartsWith("inventory:", khoa, StringComparison.Ordinal);
    }

    [Fact]
    public void Khoa_theo_SKU__KHONG_theo_ca_kho__de_hai_SKU_khac_nhau_khong_cho_nhau()
    {
        var kho = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var k1 = InventoryLockKeys.TheoSku(tenant, kho, "SKU-1");
        var k2 = InventoryLockKeys.TheoSku(tenant, kho, "SKU-2");

        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void Khoa_chuan_hoa_SKU__hoa_thuong_va_khoang_trang_khong_tach_khoa()
    {
        var kho = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        Assert.Equal(
            InventoryLockKeys.TheoSku(tenant, kho, "SKU-1"),
            InventoryLockKeys.TheoSku(tenant, kho, "  sku-1  "));
    }
}

/// <summary>
/// 🚨 **`SAL-004`/§31 — phiếu TRẢ HÀNG phải là `CustomerReturn`, KHÔNG "mượn" `GoodsReceipt`.**
/// 📌 Vì sao đáng có test riêng: cả hai loại đều **tăng `OnHand`**, nên nếu ai đó "tiện tay" map trả hàng về
/// `Receipt` thì **tồn kho vẫn đúng** ⇒ **không có test nào khác phát hiện**; nhưng báo cáo
/// *"đã bán bao nhiêu"* và *"khách trả bao nhiêu"* sẽ **lẫn nhau vĩnh viễn** (dữ liệu đã ghi thì **không sửa được**
/// vì sổ cái **append-only**). Đây là loại lỗi **chỉ lộ ra khi đã quá muộn**.
/// </summary>
public class TinhButToanTraHangTests
{
    [Fact]
    public void Phieu_TRA_HANG_sinh_bien_dong_CustomerReturn__KHONG_phai_Receipt()
    {
        var kho = Guid.NewGuid();

        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.CustomerReturn, 4, kho, null);

        var but = Assert.Single(ke);
        Assert.Equal(StockMovementType.CustomerReturn, but.Loai);
        Assert.NotEqual(StockMovementType.Receipt, but.Loai);
        Assert.Equal(4, but.DoLon);
        Assert.Equal(kho, but.WarehouseId);
    }

    [Fact]
    public void Phieu_NHAP_MUA_van_la_Receipt__hai_loai_KHONG_lan_nhau()
    {
        var ke = TinhButToanChungTu.ChoTungDong(StockDocumentType.GoodsReceipt, 4, Guid.NewGuid(), null);

        Assert.Equal(StockMovementType.Receipt, Assert.Single(ke).Loai);
    }

    [Fact]
    public void Ca_hai_loai_deu_LAM_TANG_OnHand__nen_sai_lan_nay_KHONG_lo_qua_ton_kho()
    {
        // 🚨 Chính vì cả hai đều tăng `OnHand` mà lỗi "mượn GoodsReceipt" **không** bị bắt ở đâu khác
        // ⇒ phải khoá bằng test RIÊNG này.
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-1");
        item.ApplyMovement(StockMovementType.Receipt, 10);
        item.ApplyMovement(StockMovementType.CustomerReturn, 4);

        Assert.Equal(14, item.OnHand);
        Assert.Equal(14, item.Available);
    }
}
