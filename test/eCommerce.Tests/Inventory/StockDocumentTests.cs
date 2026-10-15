using eCommerce.Inventory;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **kho** (`INV-001` ADR-01) và **chứng từ kho + ghi sổ hai bước** (ADR-02).
///
/// 🚨 **VÌ SAO QUAN TRỌNG:** ghi sổ sinh **bút toán sổ cái** ⇒ sinh **thay đổi tồn kho THẬT**. Sai ở đây
/// nghĩa là **tồn kho sai** — và tồn kho sai thì **bán thứ không có** hoặc **không bán được thứ đang có**.
/// </summary>
public class WarehouseTests
{
    [Fact]
    public void Kho_ngung_dung_thi_KHONG_tao_duoc_chung_tu()
    {
        var kho = new Warehouse(Guid.NewGuid(), "KHO-1", "Kho Hà Nội");
        kho.NgungSuDung();

        var loi = Assert.Throws<BusinessException>(() => kho.YeuCauDangHoatDong());

        Assert.Equal(InventoryErrorCodes.KhoNgungHoatDong, loi.Code);
    }

    [Fact]
    public void Kho_dang_hoat_dong_thi_QUA_duoc()
    {
        new Warehouse(Guid.NewGuid(), "KHO-1", "Kho Hà Nội").YeuCauDangHoatDong();
    }

    [Fact]
    public void Kich_hoat_lai_thi_dung_duoc_lai()
    {
        var kho = new Warehouse(Guid.NewGuid(), "KHO-1", "Kho Hà Nội");
        kho.NgungSuDung();
        kho.KichHoatLai();

        kho.YeuCauDangHoatDong();
    }
}

public class StockDocumentTests
{
    private static StockDocument PhieuNhap(int soLuongNhap = 1)
        => new(Guid.NewGuid(), StockDocumentType.GoodsReceipt, "PN-1", Guid.NewGuid(),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Chung_tu_moi_tao_la_DRAFT__chua_anh_huong_ton_kho()
    {
        Assert.Equal(StockDocumentStatus.Draft, PhieuNhap().Status);
    }

    [Fact]
    public void Ghi_so_khi_CHUA_CO_DONG_thi_NEM__va_GIU_NGUYEN_trang_thai()
    {
        var phieu = PhieuNhap();

        var loi = Assert.Throws<BusinessException>(() => phieu.GhiSo(0));

        Assert.Equal(InventoryErrorCodes.ChungTuThieuDong, loi.Code);
        // 🚨 Bất biến: ném lỗi thì KHÔNG được để lại trạng thái sai (bài học `INV-001` §16.3 —
        // "gán trước, kiểm sau" ⇒ entity hỏng trong bộ nhớ mà EF vẫn đang theo dõi).
        Assert.Equal(StockDocumentStatus.Draft, phieu.Status);
    }

    [Fact]
    public void Ghi_so_HAI_LAN_thi_NEM__khong_sinh_but_toan_hai_lan()
    {
        var phieu = PhieuNhap();
        phieu.GhiSo(1);

        Assert.Equal(StockDocumentStatus.Posted, phieu.Status);
        Assert.Equal(InventoryErrorCodes.ChungTuKhongOTrangThaiNhap,
            Assert.Throws<BusinessException>(() => phieu.GhiSo(1)).Code);
    }

    [Fact]
    public void Chung_tu_DA_GHI_SO_thi_KHONG_sua_duoc_noi_dung()
    {
        var phieu = PhieuNhap();
        phieu.GhiSo(1);

        Assert.Equal(InventoryErrorCodes.ChungTuDaGhiSoKhongSuaDuoc,
            Assert.Throws<BusinessException>(() => phieu.YeuCauChoPhepSuaNoiDung()).Code);
    }

    [Fact]
    public void Chung_tu_DA_GHI_SO_thi_KHONG_huy_duoc__phai_tao_chung_tu_DAO()
    {
        // Sổ cái append-only ⇒ "huỷ" bút toán là bất khả. Muốn đảo phải tạo chứng từ đảo.
        var phieu = PhieuNhap();
        phieu.GhiSo(1);

        Assert.Equal(InventoryErrorCodes.ChungTuDaGhiSoKhongHuyDuoc,
            Assert.Throws<BusinessException>(() => phieu.Huy()).Code);
    }

    [Fact]
    public void Chung_tu_DRAFT_thi_HUY_duoc()
    {
        var phieu = PhieuNhap();
        phieu.Huy();

        Assert.Equal(StockDocumentStatus.Cancelled, phieu.Status);
    }

    [Fact]
    public void Chuyen_kho_THIEU_kho_dich_thi_NEM()
    {
        Assert.Equal(InventoryErrorCodes.ChuyenKhoThieuKhoDich,
            Assert.Throws<BusinessException>(() => new StockDocument(
                Guid.NewGuid(), StockDocumentType.StockTransfer, "PC-1", Guid.NewGuid(),
                DateTime.UtcNow)).Code);
    }

    [Fact]
    public void Chuyen_kho_CUNG_MOT_KHO_thi_NEM__neu_khong_se_sinh_2_but_toan_vo_nghia()
    {
        var kho = Guid.NewGuid();

        Assert.Equal(InventoryErrorCodes.ChuyenKhoCungMotKho,
            Assert.Throws<BusinessException>(() => new StockDocument(
                Guid.NewGuid(), StockDocumentType.StockTransfer, "PC-1", kho, DateTime.UtcNow, kho)).Code);
    }

    [Fact]
    public void Phieu_NHAP_ma_co_kho_dich_thi_NEM()
    {
        Assert.Equal(InventoryErrorCodes.ChuyenKhoThuaKhoDich,
            Assert.Throws<BusinessException>(() => new StockDocument(
                Guid.NewGuid(), StockDocumentType.GoodsReceipt, "PN-1", Guid.NewGuid(),
                DateTime.UtcNow, Guid.NewGuid())).Code);
    }

    [Fact]
    public void Chuyen_kho_HAI_kho_khac_nhau_thi_OK()
    {
        var phieu = new StockDocument(
            Guid.NewGuid(), StockDocumentType.StockTransfer, "PC-1", Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

        Assert.Equal(StockDocumentType.StockTransfer, phieu.Type);
        Assert.NotNull(phieu.ToWarehouseId);
    }
}

public class StockDocumentLineTests
{
    [Fact]
    public void So_luong_0_hoac_AM_thi_NEM__khong_de_but_toan_vo_nghia_lot_vao_so()
    {
        // DB đã có CHECK `QuantityDelta <> 0`; lớp này để lỗi ra dưới MÃ NGHIỆP VỤ thay vì lỗi CHECK khó hiểu.
        Assert.Equal(InventoryErrorCodes.SoLuongPhaiDuong,
            Assert.Throws<BusinessException>(() => new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 0)).Code);

        Assert.Equal(InventoryErrorCodes.SoLuongPhaiDuong,
            Assert.Throws<BusinessException>(() => new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", -5)).Code);
    }

    [Fact]
    public void Dong_hop_le_thi_luu_dung__va_sku_duoc_TRIM()
    {
        var docId = Guid.NewGuid();

        var dong = new StockDocumentLine(Guid.NewGuid(), docId, "  SKU-1  ", 5);

        Assert.Equal(docId, dong.StockDocumentId);
        Assert.Equal("SKU-1", dong.Sku);
        Assert.Equal(5, dong.Quantity);
    }

    [Fact]
    public void Ma_loi_kho_nam_trong_dai_RIENG__khong_lan_sang_mien_gia_don()
    {
        // `XC-003` F2 — hai nguồn sự thật cho cùng một mã đã gây nhầm ở miền kênh.
        Assert.StartsWith("eCommerce:Inventory:", InventoryErrorCodes.ChungTuThieuDong, StringComparison.Ordinal);
    }

    [Fact]
    public void Ma_loi_KHONG_DU_TON_chi_co_MOT_ban_chuan()
    {
        // 🚨 Đã gộp: `eCommerceConsts.ErrorCodes.InsufficientStock` nay là **bí danh** của bản chuẩn
        // trong `InventoryErrorCodes`. Test khoá việc hai đường dẫn cho **cùng một giá trị**.
        Assert.Equal(InventoryErrorCodes.KhongDuTon, eCommerceConsts.ErrorCodes.InsufficientStock);
        Assert.Equal("eCommerce:Inventory:0001", InventoryErrorCodes.KhongDuTon);
    }
}
