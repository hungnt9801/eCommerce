using eCommerce.Inventory;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🆕 **`S13` — NGÂN SÁCH TỒN THEO KÊNH** (`INV-003`): bất biến của entity + ATP.
///
/// 🚨 **BỐN LỖI IM LẶNG BỘ TEST NÀY CHẶN** (không lỗi biên dịch, không exception ở đường đọc):
///   1. **ATP âm** (`Reserved > Allotted`) ⇒ bán thứ kênh không được cấp;
///   2. **`Frozen` vẫn bán** ⇒ cấu hình "ngừng kênh" **không có tác dụng**;
///   3. **Nhả khi còn giữ chỗ** ⇒ bỏ rơi cam kết với khách;
///   4. **Giảm giữ chỗ quá số đang giữ** ⇒ `Reserved` âm ⇒ ATP **lớn hơn** sự thật.
/// </summary>
public class StockAllotmentTests
{
    private static readonly Guid Kho = Guid.NewGuid();

    private static StockAllotment Tao(int allotted = 10, int priority = 0)
        => new(Guid.NewGuid(), Kho, " SKU-1 ", "Shopee", allotted, priority);

    [Fact]
    public void Tao_hop_le__chuan_hoa_sku_va_kenh__va_ATP_bang_ngan_sach()
    {
        var ns = Tao(allotted: 10);

        Assert.Equal("SKU-1", ns.Sku);
        Assert.Equal("shopee", ns.ChannelCode);       // chuẩn hoá về chữ thường (một chỗ: `InvChannelLimit`)
        Assert.Equal(10, ns.AllottedQuantity);
        Assert.Equal(0, ns.ReservedQuantity);
        Assert.Equal(10, ns.AvailableToPromise);
        Assert.Equal(StockAllotmentStatus.Active, ns.Status);
    }

    [Theory]
    [InlineData(-1)]
    public void Ngan_sach_AM__NEM_0049(int soLuong)
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(allotted: soLuong));

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe, loi.Code);
    }

    [Fact]
    public void DatNganSach__nho_hon_phan_dang_giu_cho__NEM()
    {
        var ns = Tao(allotted: 10);
        ns.TangGiuCho(6);

        var loi = Assert.Throws<BusinessException>(() => ns.DatNganSach(5));

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe, loi.Code);
        Assert.Equal(10, ns.AllottedQuantity);
    }

    [Fact]
    public void DatNganSach__sau_khi_NHA__NEM()
    {
        var ns = Tao();
        ns.Nha("Kênh ngừng", DateTime.UtcNow);

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.DatNganSach(5)).Code);
    }

    [Fact]
    public void TangGiuCho__trong_ATP_thi_duoc__vuot_thi_NEM()
    {
        var ns = Tao(allotted: 10);

        ns.TangGiuCho(4);
        ns.TangGiuCho(6);                 // ĐÚNG bằng ngân sách ⇒ ATP = 0, hợp lệ

        Assert.Equal(0, ns.AvailableToPromise);

        var loi = Assert.Throws<BusinessException>(() => ns.TangGiuCho(1));
        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe, loi.Code);
        Assert.Equal(10, ns.ReservedQuantity);
    }

    [Fact]
    public void TangGiuCho__khi_DONG_BANG__NEM__va_ATP_ve_0()
    {
        var ns = Tao(allotted: 10);
        ns.TangGiuCho(3);
        ns.DongBang(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(0, ns.AvailableToPromise);   // BR-18: Frozen ⇒ ATP = 0
        Assert.True(ns.ConGiuNganSach);           // nhưng vẫn GIỮ ngân sách

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.TangGiuCho(1)).Code);
    }

    [Fact]
    public void MoDongBang__chi_tu_Frozen__va_dong_bang_ghi_DAU_VET()
    {
        var ns = Tao();
        var nguoi = Guid.NewGuid();
        var luc = new DateTime(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc);

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.MoDongBang(nguoi)).Code);

        ns.DongBang(nguoi, luc);
        Assert.Equal(StockAllotmentStatus.Frozen, ns.Status);
        Assert.Equal(nguoi, ns.FrozenByUserId);
        Assert.Equal(luc, ns.FrozenAtUtc);

        ns.MoDongBang(nguoi);
        Assert.Equal(StockAllotmentStatus.Active, ns.Status);
        Assert.Null(ns.FrozenAtUtc);
    }

    [Fact]
    public void Nha__khi_con_GIU_CHO__NEM__va_khi_sach_thi_Released()
    {
        var ns = Tao(allotted: 10);
        ns.TangGiuCho(2);

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.Nha("Kênh ngừng", DateTime.UtcNow)).Code);
        Assert.Equal(StockAllotmentStatus.Active, ns.Status);

        ns.GiamGiuCho(2);
        ns.Nha("Kênh ngừng hợp tác", DateTime.UtcNow);

        Assert.Equal(StockAllotmentStatus.Released, ns.Status);
        Assert.Equal(0, ns.AllottedQuantity);
        Assert.Equal("Kênh ngừng hợp tác", ns.ReleaseReason);
        Assert.False(ns.ConGiuNganSach);
        Assert.Equal(0, ns.AvailableToPromise);
    }

    [Fact]
    public void Nha__thieu_ly_do__NEM()
    {
        var ns = Tao();

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.Nha("   ", DateTime.UtcNow)).Code);
    }

    [Fact]
    public void GiamGiuCho__qua_so_dang_giu__NEM()
    {
        var ns = Tao(allotted: 10);
        ns.TangGiuCho(3);

        Assert.Equal(InventoryErrorCodes.NganSachTonKhongHopLe,
            Assert.Throws<BusinessException>(() => ns.GiamGiuCho(4)).Code);
        Assert.Equal(3, ns.ReservedQuantity);

        ns.GiamGiuCho(3);
        Assert.Equal(0, ns.ReservedQuantity);
    }

    [Fact]
    public void So_cai_ngan_sach__luu_du_truong_va_khong_co_setter_cong_khai()
    {
        // 🔒 APPEND-ONLY: chỉ có hàm tạo; không setter ⇒ muốn "sửa" phải ghi dòng mới (`BR-07`).
        var id = Guid.NewGuid();
        var nsId = Guid.NewGuid();
        var luc = DateTime.UtcNow;

        var dong = new StockAllotmentLedger(
            id, nsId, Kho, "SKU-1", "shopee", quantityDelta: 5, allottedAfter: 15,
            StockAllotmentLedgerReason.ManualAllot, luc, performedByUserId: Guid.NewGuid(),
            referenceType: "Import", referenceId: "NCC-01", idempotencyKey: "K9S13-1");

        Assert.Equal(id, dong.Id);
        Assert.Equal(5, dong.QuantityDelta);
        Assert.Equal(15, dong.AllottedAfter);
        Assert.Equal("K9S13-1", dong.IdempotencyKey);

        // 🚨 `BR-07` — sổ cái **APPEND-ONLY**: không setter công khai ⇒ muốn "sửa" phải ghi dòng mới.
        var thuocTinh = typeof(StockAllotmentLedger).GetProperty(nameof(StockAllotmentLedger.QuantityDelta))!;
        Assert.False(thuocTinh.SetMethod?.IsPublic ?? false,
            "`StockAllotmentLedger` phải APPEND-ONLY — setter công khai cho phép sửa lịch sử ngân sách.");
    }
}
