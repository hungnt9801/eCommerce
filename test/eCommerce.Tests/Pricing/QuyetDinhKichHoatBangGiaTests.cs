using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test **quyết định thay thế bảng giá** (`PRC-001` **BR-007**) — hàm **THUẦN**.
///
/// 🚨 **VÌ SAO QUAN TRỌNG:** bất biến *"mỗi `(tenant, scope, kênh)` chỉ có MỘT bảng giá `Active`"* **KHÔNG có
/// hàng rào DB** (`PRC-001` D8 — `EXCLUDE USING gist` chỉ chặn được chồng khung **trong cùng `Code`**).
/// Nếu quyết định này sai thì hậu quả **không im lặng nhưng cũng không rõ ràng**: tầng tra giá nhận **hai ứng
/// viên cùng cấp, cùng `Priority`** ⇒ ném `Pricing:0001` ⇒ **checkout chết cho mọi SKU của kênh đó**.
/// </summary>
public class QuyetDinhKichHoatBangGiaTests
{
    private static QuyetDinhKichHoatBangGia.BangGiaDangHieuLuc Act(Guid id, PriceListScope scope, string? kenh, int uuTien = 0)
        => new(id, scope, kenh, uuTien);

    [Fact]
    public void Cung_scope_cung_kenh_thi_PHAI_thay_the()
    {
        var cu = Guid.NewGuid();

        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.Channel, "shopee",
            [Act(cu, PriceListScope.Channel, "shopee")]);

        Assert.Equal([cu], can.ToArray());
    }

    [Fact]
    public void KHAC_kenh_thi_KHONG_thay_the__hai_kenh_doc_lap()
    {
        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.Channel, "shopee",
            [Act(Guid.NewGuid(), PriceListScope.Channel, "lazada")]);

        Assert.Empty(can);
    }

    [Fact]
    public void KHAC_scope_thi_KHONG_thay_the()
    {
        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.Channel, "shopee",
            [Act(Guid.NewGuid(), PriceListScope.All, null)]);

        Assert.Empty(can);
    }

    [Fact]
    public void KHONG_tu_thay_the_chinh_minh__idempotent()
    {
        var id = Guid.NewGuid();

        var can = QuyetDinhKichHoatBangGia.CanThayThe(id, PriceListScope.All, null, [Act(id, PriceListScope.All, null)]);

        Assert.Empty(can);
    }

    [Fact]
    public void Kenh_NULL_va_CHUOI_RONG_phai_la_CUNG_pham_vi()
    {
        // 🚨 Nếu so `null` khác `""` thì hai bảng giá `Scope=All` (một cái `ChannelCode = null`, một cái `= ""`)
        // **cùng Active** ⇒ đúng cái BR-007 cấm, mà **không có lỗi nào**.
        var cu = Guid.NewGuid();

        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.All, null,
            [Act(cu, PriceListScope.All, "  ")]);

        Assert.Equal([cu], can.ToArray());
    }

    [Fact]
    public void Kenh_so_sanh_KHONG_phan_biet_hoa_thuong()
    {
        var cu = Guid.NewGuid();

        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.Channel, "Shopee",
            [Act(cu, PriceListScope.Channel, "shopee")]);

        Assert.Equal([cu], can.ToArray());
    }

    [Fact]
    public void Nhieu_bang_gia_cung_pham_vi_thi_thay_the_HET__va_khu_trung()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.All, null,
            [Act(a, PriceListScope.All, null), Act(b, PriceListScope.All, null), Act(a, PriceListScope.All, null)]);

        Assert.Equal(2, can.Count);
        Assert.Contains(a, can);
        Assert.Contains(b, can);
    }

    [Fact]
    public void Scope_ChannelShop_thi_HAI_shop_khac_nhau_KHONG_thay_the_nhau()
    {
        // ⚠️ `CanThayThe` so theo `(scope, ChannelCode)` — **chưa** phân biệt `ChannelShopId`.
        // 📌 Ghi nhận: với `Scope = ChannelShop`, hai SHOP khác nhau trên cùng kênh là **hai phạm vi khác nhau**
        // ⇒ muốn chặt hơn thì hàm phải nhận thêm `ChannelShopId`. Hiện `PRC-001` BR-007 phát biểu theo
        // `(TenantId, Scope, ChannelCode)` nên bám đúng phát biểu đó; xem ghi chú ở `prc-001` §18.4.
        var can = QuyetDinhKichHoatBangGia.CanThayThe(
            Guid.NewGuid(), PriceListScope.ChannelShop, "shopee",
            [Act(Guid.NewGuid(), PriceListScope.ChannelShop, "shopee")]);

        Assert.Single(can);
    }
}
