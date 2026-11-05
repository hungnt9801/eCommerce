using eCommerce.Orders;
using eCommerce.Pos;

namespace eCommerce.Reporting;

/// <summary>
/// **ÁNH XẠ CHỨNG TỪ NGUỒN → BẢN GHI CHUẨN HOÁ** (`WP-08`).
///
/// 🔑 **VÌ SAO Ở TẦNG APPLICATION (không ở Domain):** đây là chỗ **duy nhất** module báo cáo chạm entity
/// của module khác (`RetailBill`, `Order`). 📌 Để ở Domain thì Domain báo cáo phải tham chiếu Domain của
/// 3 module nguồn — read-model sẽ phụ thuộc **cấu trúc bên trong** của chúng và mỗi lần đổi entity nguồn
/// là một lần sửa module không liên quan.
///
/// 🚨 **HAI CHỖ DỄ SAI — ĐÃ XỬ LÝ Ở ĐÂY (và có test khoá):**
/// <list type="number">
///   <item>**POS**: `RetailBillLine.Amount` là thành tiền **sau chiết khấu dòng**, **chưa** VAT, và
///     `RetailBill.Subtotal = Σ Amount` ⇒ doanh thu = `Subtotal − DiscountTotal` **đúng** công thức `D-45`.
///     📌 Không cộng `VatAmount` vào thành tiền dòng: `TotalPayment` **gồm** VAT, báo cáo *"doanh số hàng"*
///     thì không — trộn hai định nghĩa là nguồn gốc của mọi con số lệch (`handover-n8` §6.2).</item>
///   <item>**ĐƠN HÀNG**: `OrderLine.LineTotal = UnitPrice × Quantity − DiscountAmount + TaxAmount` —
///     **gồm** thuế dòng. ⇒ thành tiền dòng của báo cáo = `LineTotal − TaxAmount`, và `TaxAmount` đi vào
///     cột `VatAmount` riêng. Lấy thẳng `LineTotal` thì doanh thu đơn online **cao hơn** doanh thu quầy
///     đúng phần thuế, và hai nguồn cộng lại thành con số không có nghĩa.</item>
/// </list>
/// </summary>
public static class AnhXaNguonBanHang
{
    /// <summary>Ánh xạ **hoá đơn bán lẻ** + dòng của nó.</summary>
    public static (ChungTuBanHangNguon ChungTu, List<DongBanHangNguon> Dong) TuHoaDonBanLe(
        RetailBill hoaDon,
        IReadOnlyList<RetailBillLine> dong,
        int gioLech = NgayKinhDoanh.GioLechMacDinh)
    {
        ArgumentNullException.ThrowIfNull(hoaDon);
        ArgumentNullException.ThrowIfNull(dong);

        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.BanLe,
            hoaDon.Id,
            hoaDon.Number,
            NgayKinhDoanh.TuUtc(hoaDon.CreationTime, gioLech),
            hoaDon.CreationTime,
            hoaDon.WarehouseId,
            hoaDon.SaleStaffId,
            hoaDon.CustomerId,
            hoaDon.DiscountTotal);

        var dongChuan = dong
            .OrderBy(x => x.CreationTime)
            .ThenBy(x => x.Id)
            .Select(x => new DongBanHangNguon(
                x.Id,
                hoaDon.Id,
                x.Sku,
                x.ProductName,
                x.Quantity,
                x.UnitPrice,
                x.DiscountAmount,
                x.Amount,
                x.VatPercent,
                x.VatAmount ?? 0m))
            .ToList();

        return (chungTu, dongChuan);
    }

    /// <summary>Ánh xạ **đơn hàng** + dòng của nó (xem luật (2) ở chú thích lớp).</summary>
    public static (ChungTuBanHangNguon ChungTu, List<DongBanHangNguon> Dong) TuDonHang(
        Order donHang,
        IReadOnlyList<OrderLine> dong,
        int gioLech = NgayKinhDoanh.GioLechMacDinh)
    {
        ArgumentNullException.ThrowIfNull(donHang);
        ArgumentNullException.ThrowIfNull(dong);

        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.DonHang,
            donHang.Id,
            donHang.Number,
            NgayKinhDoanh.TuUtc(donHang.CreationTime, gioLech),
            donHang.CreationTime,
            donHang.WarehouseId,
            donHang.SaleStaffId,
            donHang.CustomerId,
            donHang.DiscountTotal);

        var dongChuan = dong
            .OrderBy(x => x.CreationTime)
            .ThenBy(x => x.Id)
            .Select(x => new DongBanHangNguon(
                x.Id,
                donHang.Id,
                x.Sku,
                // 📌 `OrderLine` **không** có tên sản phẩm (POS có): để `null` — tra tên từ `DimProduct`
                // khi đọc, nếu có. Không bịa tên bằng SKU (UI sẽ hiện SKU ở cột tên, không ai phân biệt được).
                null,
                x.Quantity,
                x.UnitPrice,
                x.DiscountAmount,
                ThanhTienDongDonHang(x),
                null,
                x.TaxAmount))
            .ToList();

        return (chungTu, dongChuan);
    }

    /// <summary>
    /// Thành tiền dòng của **đơn hàng**, KHÔNG gồm thuế: `Đơn giá × SL − Chiết khấu dòng`.
    /// 🚨 Đừng thay bằng `OrderLine.LineTotal` (nó **cộng** `TaxAmount`) — xem luật (2) ở chú thích lớp.
    /// </summary>
    public static decimal ThanhTienDongDonHang(OrderLine dong)
    {
        ArgumentNullException.ThrowIfNull(dong);

        return Math.Round(
            (dong.UnitPrice * dong.Quantity) - dong.DiscountAmount,
            2,
            MidpointRounding.AwayFromZero);
    }
}
