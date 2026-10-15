using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.PaymentHub.Payments;

namespace eCommerce.Tests.Domain;

/// <summary>
/// Test cho các bất biến nghiệp vụ quan trọng nhất — những chỗ mà nếu sai thì mất tiền hoặc sai tồn kho.
/// </summary>
public class OrderAndInventoryTests
{
    // ---------- Chống trùng đơn từ webhook sàn (at-least-once) ----------

    [Fact]
    public void AddLine_cung_external_line_id_khong_tao_dong_trung()
    {
        var order = new Order(Guid.NewGuid(), "ORD-001", "shopee");

        var first = order.AddLine(Guid.NewGuid(), "SKU-A", 2, 100000m, externalLineId: "LINE-1");
        var second = order.AddLine(Guid.NewGuid(), "SKU-A", 2, 100000m, externalLineId: "LINE-1");

        Assert.Single(order.Lines);
        Assert.Equal(first.Id, second.Id); // trả về đúng dòng cũ, không tạo mới
    }

    [Fact]
    public void AddLine_so_luong_khong_hop_le_thi_nem_loi()
    {
        var order = new Order(Guid.NewGuid(), "ORD-002", "web");

        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddLine(Guid.NewGuid(), "SKU-A", 0, 1000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddLine(Guid.NewGuid(), "SKU-A", -1, 1000m));
    }

    [Fact]
    public void Tinh_tien_dung_voi_decimal_khong_sai_so()
    {
        var order = new Order(Guid.NewGuid(), "ORD-003", "web");

        // 3 × 19.999,99 = 59.999,97 — với double sẽ có sai số, với decimal thì không.
        order.AddLine(Guid.NewGuid(), "SKU-A", 3, 19999.99m);
        order.AddLine(Guid.NewGuid(), "SKU-B", 1, 0.01m);

        Assert.Equal(59999.98m, order.Subtotal);

        order.SetDiscount(10000m);
        order.SetTotals(taxTotal: 5000m, shippingTotal: 30000m);

        Assert.Equal(59999.98m - 10000m + 5000m + 30000m, order.GrandTotal);
    }

    // ---------- Chống bán vượt tồn kho ----------

    [Fact]
    public void Available_bang_OnHand_tru_Reserved()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-A");

        item.ApplyMovement(StockMovementType.Receipt, 100);
        item.ApplyMovement(StockMovementType.Reservation, 30);

        Assert.Equal(100, item.OnHand);
        Assert.Equal(30, item.Reserved);
        Assert.Equal(70, item.Available);
    }

    [Fact]
    public void Ban_nhieu_hon_ton_kho_thi_nem_loi()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-A");
        item.ApplyMovement(StockMovementType.Receipt, 10);

        // Bán 11 khi chỉ có 10 ⇒ phải chặn ngay ở tầng domain.
        Assert.Throws<Volo.Abp.BusinessException>(() => item.ApplyMovement(StockMovementType.Sale, 11));
    }

    [Fact]
    public void Giu_cho_nhieu_hon_kha_dung_thi_khong_nen_xay_ra_o_tang_nay()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-A");
        item.ApplyMovement(StockMovementType.Receipt, 10);
        item.ApplyMovement(StockMovementType.Reservation, 10);

        Assert.Equal(0, item.Available);
        Assert.False(item.CanReserve(1)); // tầng application phải kiểm tra trước khi giữ chỗ
    }

    [Fact]
    public void Ban_het_hang_roi_nhap_lai_thi_tinh_dung()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "SKU-A");

        item.ApplyMovement(StockMovementType.Receipt, 5);
        item.ApplyMovement(StockMovementType.Sale, 5);
        Assert.Equal(0, item.OnHand);

        item.ApplyMovement(StockMovementType.CustomerReturn, 2);
        Assert.Equal(2, item.OnHand);
        Assert.Equal(2, item.Available);
    }

    // ---------- Idempotency của webhook thanh toán ----------

    [Fact]
    public void MarkSucceeded_lan_hai_khong_doi_trang_thai_va_tra_ve_false()
    {
        var transaction = new PaymentTransaction(Guid.NewGuid(), "vnpay", "ORDER-2026-001", 250000m);

        var firstTime = transaction.MarkSucceeded("14000001", "14000001", "payload-1", DateTime.UtcNow);
        var secondTime = transaction.MarkSucceeded("14000001", "14000001", "payload-2", DateTime.UtcNow);

        Assert.True(firstTime);      // lần đầu: chuyển sang Succeeded
        Assert.False(secondTime);    // webhook gửi trùng: không xử lý lại
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.Equal(2, transaction.WebhookReceivedCount); // nhưng vẫn đếm để audit
        Assert.Equal("payload-2", transaction.LastWebhookPayload);
    }

    [Fact]
    public void Khong_the_ghi_de_giao_dich_da_thanh_cong_thanh_that_bai()
    {
        var transaction = new PaymentTransaction(Guid.NewGuid(), "vnpay", "ORDER-2026-002", 100000m);

        transaction.MarkSucceeded("14000002", "14000002", "payload", DateTime.UtcNow);
        transaction.MarkFailed("IPN đến muộn báo thất bại", "payload-late");

        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
    }

    [Fact]
    public void Hoan_tien_mot_phan_roi_toan_bo_thi_chuyen_trang_thai_dung()
    {
        var transaction = new PaymentTransaction(Guid.NewGuid(), "vnpay", "ORDER-2026-003", 300000m);
        transaction.MarkSucceeded("14000003", "14000003", "payload", DateTime.UtcNow);

        transaction.RegisterRefund(100000m);
        Assert.Equal(PaymentTransactionStatus.PartiallyRefunded, transaction.Status);

        transaction.RegisterRefund(200000m);
        Assert.Equal(PaymentTransactionStatus.Refunded, transaction.Status);
    }
}
