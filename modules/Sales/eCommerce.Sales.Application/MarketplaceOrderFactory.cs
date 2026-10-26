using eCommerce.ChannelHub.Events;
using Volo.Abp;
using Volo.Abp.Guids;

namespace eCommerce.Marketplace;

/// <summary>
/// **LÕI THUẦN**: dựng `Order` nội bộ từ một `ChannelOrderPulledEto`.
///
/// 🎯 VÌ SAO TÁCH KHỎI HANDLER: đây là chỗ **dễ sai tiền nhất** của toàn bộ luồng nhận đơn sàn. Tách ra
/// thành hàm thuần (không I/O, không ABP) ⇒ test được **mọi nhánh tiền** mà không cần DB/HTTP.
///
/// 🚨 **D9 — LỖI "TRỪ GIẢM GIÁ HAI LẦN" (đã phân tích trong `SAL-005`, phải khoá bằng test):**
///   • `OrderLine.LineTotal = UnitPrice × Quantity − DiscountAmount + TaxAmount` (đã **trừ** giảm giá dòng)
///   • `Order.Recalculate()` : `Subtotal = Σ LineTotal`, rồi `GrandTotal = Subtotal − DiscountTotal + TaxTotal + ShippingTotal`
/// ⇒ nếu ghi **cùng một khoản giảm giá** vào **cả** `OrderLine.DiscountAmount` **và** `Order.DiscountTotal`
///   thì tiền bị trừ **HAI LẦN** ⇒ `GrandTotal` **thấp giả** ⇒ sai tiền, sai đối soát, và **không có lỗi nào**.
///
/// ⇒ LUẬT Ở ĐÂY: mỗi khoản giảm giá chỉ vào **đúng một** nơi, theo **nguồn** của nó:
///   • `line.DiscountAmount` ← giảm giá **cấp dòng** của sàn (`ChannelOrderPulledLineEto.DiscountAmount`)
///   • `Order.DiscountTotal` ← giảm giá **cấp đơn** của sàn (`ChannelOrderPulledEto.ChannelDiscount`)
///
/// 📌 Tiền tệ: mọi trường là `decimal` (quy ước #1) — ETO đã dùng `decimal` nên **không** có bước
/// chuyển đổi từ `double`/`float` (double là lỗi của GrandNode 2, xem `XC-001`).
/// </summary>
public static class MarketplaceOrderFactory
{
    /// <summary>Độ dài tối đa của `Order.Number` (khớp `eCommerceDbContext` — `HasMaxLength(64)`).</summary>
    public const int DoDaiSoDonToiDa = 64;

    /// <summary>
    /// Số đơn hiển thị, sinh **TẤT ĐỊNH** từ `(channelCode, externalOrderId)` — `SAL-005` **D8**.
    ///
    /// 🎯 Vì sao tất định: nếu hai luồng (worker pull ở instance A, webhook ở instance B) cùng nhận một đơn
    /// mà sinh số **ngẫu nhiên/tuần tự** thì chúng ra **hai số khác nhau** cho **cùng một đơn sàn**.
    /// Số tất định khiến unique index `(TenantId, Number)` trở thành **hàng rào thứ hai miễn phí**.
    ///
    /// ⚠️ Vượt <see cref="DoDaiSoDonToiDa"/> ⇒ **NÉM LỖI**, **không cắt bớt**: cắt bớt tạo ra một số
    /// "hợp lệ" nhưng **SAI** (hai đơn dài khác nhau có thể cắt thành cùng một số) — không ai phát hiện.
    /// </summary>
    public static string SinhSoDon(string channelCode, string externalOrderId)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(MarketplaceErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(channelCode));
        }

        if (string.IsNullOrWhiteSpace(externalOrderId))
        {
            throw new BusinessException(MarketplaceErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(externalOrderId));
        }

        var soDon = $"{channelCode.ToUpperInvariant()}-{externalOrderId}";

        if (soDon.Length > DoDaiSoDonToiDa)
        {
            throw new BusinessException(MarketplaceErrorCodes.SoDonQuaDai)
                .WithData("soDon", soDon)
                .WithData("doDai", soDon.Length)
                .WithData("toiDa", DoDaiSoDonToiDa);
        }

        return soDon;
    }

    /// <summary>
    /// Dựng `Order` (kèm dòng đơn) từ sự kiện kéo đơn của sàn. **Không** ghi DB — người gọi lo phần đó.
    /// </summary>
    /// <param name="suKien">Sự kiện `ChannelOrderPulledEto` (đã có đủ dữ liệu — D1).</param>
    /// <param name="taoId">Hàm sinh Id (truyền `IGuidGenerator.Create` ở production; hàm cố định khi test).</param>
    public static Orders.Order TaoOrder(ChannelOrderPulledEto suKien, Func<Guid> taoId)
    {
        ArgumentNullException.ThrowIfNull(suKien);
        ArgumentNullException.ThrowIfNull(taoId);

        if (suKien.Lines is null || suKien.Lines.Count == 0)
        {
            // Đơn sàn KHÔNG có dòng hàng ⇒ không thể là đơn thật. Ném lỗi thay vì tạo đơn rỗng (0 đồng)
            // rồi để nó lặng lẽ đi vào báo cáo doanh thu.
            throw new BusinessException(MarketplaceErrorCodes.DonKhongCoDongHang)
                .WithData("channelCode", suKien.ChannelCode)
                .WithData("externalOrderId", suKien.ExternalOrderId);
        }

        var order = new Orders.Order(taoId(), SinhSoDon(suKien.ChannelCode, suKien.ExternalOrderId), suKien.ChannelCode, suKien.TenantId)
        {
            ChannelShopId = suKien.ChannelShopId,
            ExternalOrderId = suKien.ExternalOrderId,
            Currency = string.IsNullOrWhiteSpace(suKien.Currency) ? "VND" : suKien.Currency,
            Note = suKien.BuyerNote
        };

        foreach (var dong in suKien.Lines)
        {
            var line = order.AddLine(
                taoId(),
                dong.Sku,
                dong.Quantity,
                dong.UnitPrice,
                dong.ExternalLineId);

            // ✅ Giảm giá CẤP DÒNG — chỉ vào đây (xem D9 ở doc của lớp này).
            line.DiscountAmount = dong.DiscountAmount;
        }

        // ✅ Giảm giá CẤP ĐƠN — chỉ vào `DiscountTotal`, KHÔNG đụng vào dòng.
        if (suKien.ChannelDiscount != 0m)
        {
            order.SetDiscount(suKien.ChannelDiscount);
        }

        // Phí vận chuyển là tiền CỘNG THÊM. Thuế để 0: hợp đồng ETO hiện **chưa** mang thuế
        // (`SAL-005` F2 / `REP-002` đã ghi nhận) ⇒ **không** suy đoán thuế ở đây.
        order.SetTotals(taxTotal: 0m, shippingTotal: suKien.ShippingFee);

        return order;
    }
}
