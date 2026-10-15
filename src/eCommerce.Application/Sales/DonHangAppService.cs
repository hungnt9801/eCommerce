using eCommerce.Application.Contracts.Sales;
using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🚨 **TẠO ĐƠN HÀNG TỪ CHECKOUT** (`SAL-001`) — nguồn số tiền là **SỔ GIẢI TRÌNH**, **KHÔNG** phải tính lại.
///
/// **TRÌNH TỰ:**
///   1. Đọc **sổ giải trình** theo `CheckoutIdempotencyKey` ⇒ **số tiền đã báo khách** (bằng chứng).
///   2. Đọc **lượt giữ chỗ** theo cùng nguồn (`Checkout:{khoá}`) ⇒ **hàng đã được giữ**; không có ⇒ **NÉM**
///      (đơn mà không giữ được hàng là **đơn không có hàng** ⇒ sẽ phải xin lỗi khách).
///   3. Tạo `Order` + `OrderLine` với **đúng** đơn giá **đã làm tròn** từ sổ (`UnitPrice`) ⇒ rồi `SetDiscount`,
///      `SetTotals`, `Recalculate()`.
///   4. 🚨 **CHỐT KIỂM TRA (tripwire):** `order.GrandTotal` **PHẢI** bằng `tien.Total` của sổ giải trình.
///      Lệch ⇒ **NÉM**, **không** lưu đơn. 📌 Đây là **lưới an toàn rẻ nhất** cho loại lỗi tệ nhất của miền bán hàng:
///      *đơn ghi một số, khách thấy một số khác*. Nếu ai đó sửa công thức ở một trong hai nơi, tripwire **nổ ngay**
///      thay vì âm thầm lệch vài đồng trên mỗi đơn.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Orders.Default)]
public class DonHangAppService : ApplicationService, IDonHangAppService
{
    private readonly IRepository<Order, Guid> _donRepository;
    private readonly IRepository<OrderLine, Guid> _dongDonRepository;
    private readonly IRepository<StockReservation, Guid> _giuChoRepository;
    private readonly IPriceCalculationAppService _tinhGia;

    public DonHangAppService(
        IRepository<Order, Guid> donRepository,
        IRepository<OrderLine, Guid> dongDonRepository,
        IRepository<StockReservation, Guid> giuChoRepository,
        IPriceCalculationAppService tinhGia)
    {
        _donRepository = donRepository;
        _dongDonRepository = dongDonRepository;
        _giuChoRepository = giuChoRepository;
        _tinhGia = tinhGia;
    }

    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<DonHangDto> TaoTuCheckoutAsync(TaoDonTuCheckoutInput input)
    {
        var khoa = input.CheckoutIdempotencyKey.Trim();
        var thamChieu = $"Checkout:{khoa}";

        // ① SỐ TIỀN từ SỔ GIẢI TRÌNH (không tính lại). Không có ⇒ `Pricing:0004`.
        var tien = await _tinhGia.GetSoGiaiTrinhAsync(khoa);

        // ② Phải CÓ hàng đang giữ cho nguồn này.
        var giuCho = (await _giuChoRepository.GetListAsync(x => x.ReferenceId == thamChieu))
            .Where(x => x.Status == ReservationStatus.Held)
            .ToList();

        if (giuCho.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("idempotencyKey", khoa)
                .WithData("lyDo", "Không còn lượt giữ chỗ nào cho checkout này ⇒ KHÔNG tạo đơn " +
                                  "(đơn mà không có hàng là đơn không giao được).");
        }

        // ③ Tạo đơn với ĐÚNG đơn giá đã chốt trong sổ giải trình.
        var don = new Order(GuidGenerator.Create(), input.Number, input.ChannelCode)
        {
            ChannelShopId = input.ChannelShopId,
            CustomerId = input.CustomerId,
            Note = input.Note,
            Currency = tien.CurrencyCode
        };

        await _donRepository.InsertAsync(don, autoSave: false);

        foreach (var dong in tien.Lines)
        {
            var dongDon = don.AddLine(GuidGenerator.Create(), dong.Sku, dong.Quantity, dong.UnitPrice);
            await _dongDonRepository.InsertAsync(dongDon, autoSave: false);
        }

        don.SetDiscount(tien.TotalDiscount);
        don.SetTotals(tien.TotalTax, tien.TotalFee);
        don.Recalculate();

        // ④ 🚨 TRIPWIRE: đơn PHẢI khớp TỪNG ĐỒNG với số đã báo khách.
        if (don.GrandTotal != tien.Total)
        {
            throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                .WithData("grandTotalDon", don.GrandTotal)
                .WithData("totalSoGiaiTrinh", tien.Total)
                .WithData("chenhLech", don.GrandTotal - tien.Total)
                .WithData("idempotencyKey", khoa)
                .WithData("lyDo",
                    "Số tiền trên đơn KHÁC số tiền trong sổ giải trình ⇒ KHÔNG lưu đơn " +
                    "(khách đã thấy một số, đơn không được ghi số khác).");
        }

        await _donRepository.UpdateAsync(don, autoSave: true);

        Logger.LogInformation(
            "TẠO ĐƠN {Number} từ checkout {Khoa}: tổng {Tong} (ĐÃ ĐỐI CHIẾU sổ giải trình: khớp), " +
            "{SoDong} dòng, {SoGiuCho} lượt giữ chỗ.",
            don.Number, khoa, don.GrandTotal, tien.Lines.Count, giuCho.Count);

        return ToDto(don, tien.Lines.Count, tien.Total, khoa);
    }

    public async Task<DonHangDto> GetAsync(Guid id)
    {
        var don = await _donRepository.GetAsync(id);
        var soDong = (int)await _dongDonRepository.CountAsync(x => x.OrderId == id);

        return ToDto(don, soDong, don.GrandTotal, "(xem log)");
    }

    private static DonHangDto ToDto(Order don, int soDong, decimal totalTuSo, string checkoutKey) => new()
    {
        Id = don.Id,
        Number = don.Number,
        ChannelCode = don.ChannelCode,
        Status = don.Status.ToString(),
        Currency = don.Currency,
        Subtotal = don.Subtotal,
        DiscountTotal = don.DiscountTotal,
        TaxTotal = don.TaxTotal,
        ShippingTotal = don.ShippingTotal,
        GrandTotal = don.GrandTotal,
        LineCount = soDong,
        TotalTuSoGiaiTrinh = totalTuSo,
        CheckoutIdempotencyKey = checkoutKey
    };
}
