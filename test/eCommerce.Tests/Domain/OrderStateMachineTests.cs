using System.Reflection;
using eCommerce.Orders;

namespace eCommerce.Tests.Domain;

/// <summary>
/// 🚨 **STATE MACHINE ĐƠN HÀNG** (`SAL-003`) — test **cả hai chiều**:
///
/// ① **Chiều thuận:** mọi cạnh hợp lệ đi được, **đúng MỘT** dòng lịch sử, version của trục **+1**,
///    `Status` (suy ra) khớp 4 trục.
/// ② **Chiều NGƯỢC (quan trọng hơn):** chuyển **sai** phải **NÉM LỖI CÓ MÃ** — không được no-op im lặng.
///    Đây là bất biến #11 của dự án; trước 24/10/2026 `Order.SetStatus` là **phép gán trần** nên mọi ca
///    dưới đây đều "đi qua" mà không ai biết.
///
/// 📌 Vì sao phải kiểm **mã lỗi** chứ không chỉ "có exception": mã là **hợp đồng với UI/client**
/// (`0001` = bấm sai bước, `0006` = đơn đã kết thúc ⇒ gợi ý tạo đơn mới). Gộp mã = mất khả năng phân biệt.
/// </summary>
public class OrderStateMachineTests
{
    private static Order DonMoi() => new(Guid.NewGuid(), "WEB-DH-001", "web");

    private static Guid NguoiDung() => Guid.NewGuid();

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ① CHIỀU THUẬN — 4 TRỤC
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Don_moi__bon_truc_o_gia_tri_mac_dinh()
    {
        var don = DonMoi();

        Assert.Equal(OrderStatus.Pending, don.Status);
        Assert.Equal(FulfillmentStatus.Pending, don.FulfillmentStatus);
        Assert.Equal(PaymentStatus.Unpaid, don.PaymentStatus);
        Assert.Equal(ReconciliationStatus.AwaitingReconcile, don.ReconciliationStatus);
        Assert.Equal(ReturnStatus.Open, don.ReturnStatus);
        Assert.False(don.IsCancelled);
        Assert.Empty(don.StatusHistory);
    }

    [Fact]
    public void Xac_nhan__Pending_sang_Confirmed__ghi_dung_MOT_lich_su()
    {
        var don = DonMoi();

        var lichSu = don.ChuyenThucHien(
            FulfillmentStatus.Confirmed,
            StatusActorType.Staff,
            NguoiDung(),
            "XacNhanDon");

        Assert.Equal(OrderStatus.Confirmed, don.Status);
        Assert.Equal(FulfillmentStatus.Confirmed, don.FulfillmentStatus);
        Assert.Equal(1, don.FulfillmentVersion);
        Assert.Single(don.StatusHistory);
        Assert.Equal("Fulfillment", lichSu.Axis);
        Assert.Equal((int)FulfillmentStatus.Pending, lichSu.FromStatus);
        Assert.Equal((int)FulfillmentStatus.Confirmed, lichSu.ToStatus);
        Assert.Equal("Staff", lichSu.ActorType);
        Assert.Equal("XacNhanDon", lichSu.ReasonCode);
        Assert.Equal(1, lichSu.VersionAfter);
        Assert.NotEqual(Guid.Empty, lichSu.EventId);
    }

    [Fact]
    public void Di_het_vong_doi_thuc_hien__Completed_va_5_dong_lich_su()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");
        don.ChuyenThucHien(FulfillmentStatus.Packing, StatusActorType.Staff, ai, "DongGoi");
        don.ChuyenThucHien(FulfillmentStatus.AwaitingPickup, StatusActorType.Staff, ai, "ChoLayHang");
        don.ChuyenThucHien(FulfillmentStatus.OutForDelivery, StatusActorType.MarketplaceWebhook, null, "WebhookDangGiao");
        don.ChuyenThucHien(FulfillmentStatus.Delivered, StatusActorType.MarketplaceWebhook, null, "WebhookDaGiao");

        Assert.Equal(OrderStatus.Completed, don.Status);
        Assert.Equal(FulfillmentStatus.Delivered, don.FulfillmentStatus);
        Assert.Equal(5, don.FulfillmentVersion);
        Assert.Equal(5, don.StatusHistory.Count);
        // 🔑 Mỗi trục có bộ đếm RIÊNG (D6): đổi trục 1 **không** làm version trục 3/4 nhảy.
        Assert.Equal(0, don.PaymentVersion);
        Assert.Equal(0, don.ClosureVersion);
    }

    [Fact]
    public void Giao_that_bai__di_duoc_2_huong__giao_lai_hoac_chuyen_hoan()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");
        don.ChuyenThucHien(FulfillmentStatus.Packing, StatusActorType.Staff, ai, "DongGoi");
        don.ChuyenThucHien(FulfillmentStatus.AwaitingPickup, StatusActorType.Staff, ai, "ChoLayHang");
        don.ChuyenThucHien(FulfillmentStatus.OutForDelivery, StatusActorType.MarketplaceWebhook, null, "DangGiao");
        don.ChuyenThucHien(FulfillmentStatus.DeliveryFailed, StatusActorType.MarketplaceWebhook, null, "GiaoThatBai");

        // Nhánh A: giao lại.
        don.ChuyenThucHien(FulfillmentStatus.OutForDelivery, StatusActorType.Staff, ai, "GiaoLai");
        Assert.Equal(OrderStatus.Shipped, don.Status);

        // Nhánh B: chuyển hoàn.
        don.ChuyenThucHien(FulfillmentStatus.DeliveryFailed, StatusActorType.MarketplaceWebhook, null, "ThatBaiLan2");
        don.ChuyenThucHien(FulfillmentStatus.Returning, StatusActorType.Staff, ai, "ChuyenHoan");
        don.ChuyenThucHien(FulfillmentStatus.Returned, StatusActorType.SystemJob, null, "VeKho");

        Assert.Equal(OrderStatus.Returned, don.Status);
        Assert.Equal(FulfillmentStatus.Returned, don.FulfillmentStatus);
    }

    [Fact]
    public void Tra_toan_bo__Open_sang_Returned__Status_Returned()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");
        don.ChuyenKetThuc(ReturnStatus.Returned, StatusActorType.Staff, ai, "TraHangToanBo");

        Assert.Equal(OrderStatus.Returned, don.Status);
        Assert.Equal(ReturnStatus.Returned, don.ReturnStatus);
        Assert.Equal(1, don.ClosureVersion);
        // 🔑 Trả hàng KHÔNG được làm bẩn version của trục thực hiện.
        Assert.Equal(1, don.FulfillmentVersion);
    }

    [Fact]
    public void Huy_don__pending__duoc__va_hap_thu()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        var lichSu = don.HuyDon(StatusActorType.Staff, ai, "KhachYeuCauHuy");

        Assert.True(don.IsCancelled);
        Assert.Equal(ReturnStatus.Cancelled, don.ReturnStatus);
        Assert.Equal(OrderStatus.Cancelled, don.Status);
        Assert.Equal("Closure", lichSu.Axis);
        Assert.Single(don.StatusHistory);

        // 🔒 Hấp thụ: huỷ lần hai ⇒ NÉM 0006, KHÔNG im lặng.
        var loi = Assert.Throws<Volo.Abp.BusinessException>(() => don.HuyDon(StatusActorType.Staff, ai, "HuyLan2"));
        Assert.Equal(OrderErrorCodes.TrangThaiHapThu, loi.Code);
        Assert.Single(don.StatusHistory);
    }

    [Fact]
    public void Da_huy_nhung_TIEN_VAN_VE__truc_tai_chinh_VAN_ghi_duoc()
    {
        // 🔑 Ca thật: khách chuyển khoản chậm / IPN đến muộn **sau** khi đơn đã huỷ.
        // Chặn trục tài chính ở đây nghĩa là *"tiền đã vào mà sổ không biết"* — lỗi tệ nhất của miền thanh toán.
        var don = DonMoi();
        var ai = NguoiDung();
        don.HuyDon(StatusActorType.Staff, ai, "KhachHuy");

        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.Paid));

        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
        Assert.Equal(OrderStatus.Cancelled, don.Status); // đơn VẪN huỷ — không hồi sinh
        Assert.Contains(don.StatusHistory, h => h.Axis == "Payment");
    }

    [Fact]
    public void Moi_lan_chuyen__dang_ky_DUNG_MOT_ETO_trong_cung_transaction()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");

        // 🔒 `AddDistributedEvent` ⇒ ABP ghi vào `AbpEventOutbox` **cùng** UoW với trạng thái + lịch sử.
        // 📌 `GetDistributedEvents()` trả `DomainEventRecord` (wrapper) — event thật nằm ở `.EventData`.
        var ban = Assert.Single(don.GetDistributedEvents());
        var eto = Assert.IsType<OrderStatusChangedEto>(ban.EventData);

        Assert.Equal(don.Id, eto.OrderId);
        Assert.Equal("Fulfillment", eto.Axis);
        Assert.Equal((int)FulfillmentStatus.Pending, eto.FromStatus);
        Assert.Equal((int)FulfillmentStatus.Confirmed, eto.ToStatus);
        Assert.Equal(1, eto.VersionAfter);
        Assert.Equal("Staff", eto.ActorType);
        Assert.Equal(ai, eto.ActorId);
        Assert.Equal("XacNhanDon", eto.ReasonCode);
        Assert.Equal(don.StatusHistory.Single().EventId, eto.EventId);
    }

    [Fact]
    public void API_trang_thai_KHONG_co_duong_ghi_truc_tai_chinh()
    {
        // 🔒 BR-013 / D11: trục tài chính CHỈ PaymentHub/IPN đã xác thực chữ ký ghi. Một method
        // `ChangePaymentStatusAsync` ở API công khai là đường "đặt tay đã thanh toán" ⇒ thu tiền hai lần.
        var ten = typeof(eCommerce.Application.Contracts.Sales.IOrderStatusAppService)
            .GetMethods()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("ChangePaymentStatusAsync", ten);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Pending, ReturnStatus.Open, false, OrderStatus.Pending)]
    [InlineData(FulfillmentStatus.Confirmed, ReturnStatus.Open, false, OrderStatus.Confirmed)]
    [InlineData(FulfillmentStatus.Packing, ReturnStatus.Open, false, OrderStatus.Processing)]
    [InlineData(FulfillmentStatus.OutForDelivery, ReturnStatus.Open, false, OrderStatus.Shipped)]
    [InlineData(FulfillmentStatus.Delivered, ReturnStatus.Open, false, OrderStatus.Completed)]
    [InlineData(FulfillmentStatus.Pending, ReturnStatus.Cancelled, true, OrderStatus.Cancelled)]
    [InlineData(FulfillmentStatus.Delivered, ReturnStatus.Returned, false, OrderStatus.Returned)]
    public void Ham_suy_ra_DerivedStatus__dung_cho_moi_to_hop(
        FulfillmentStatus thucHien, ReturnStatus ketThuc, bool daHuy, OrderStatus mongDoi)
    {
        // 🔒 Hàm THUẦN dùng bởi CẢ entity và job quét (NFR-011) — một định nghĩa, hai chỗ gọi.
        Assert.Equal(mongDoi, OrderStateMachine.TinhTrangThaiSuyRa(thucHien, ketThuc, daHuy));
    }

    [Fact]
    public void Moi_lan_chuyen__RowVersion_tang__chuyen_LOI_KHONG_tang()
    {
        // 🔒 D5/BR-010: `RowVersion` là concurrency token của CẢ DÒNG; tăng **trước** khi lưu, EF so
        // **giá trị gốc** trong WHERE. Chuyển sai (ném) **không** được làm tăng — nếu tăng, lần lưu sau
        // sẽ dùng token "đã đổi" cho một thay đổi **chưa hề xảy ra**.
        var don = DonMoi();
        var ai = NguoiDung();

        Assert.Equal(0, don.RowVersion);

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");
        Assert.Equal(1, don.RowVersion);

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Delivered, StatusActorType.Staff, ai, "NhayCoc"));

        Assert.Equal(1, don.RowVersion);
    }

    [Fact]
    public void Tra_hang_truc_3__chuyen_dung_bang_va_ghi_lich_su()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        // 📌 GĐ1 chưa có caller; test khoá **bảng chuyển** để GĐ2 (`REC-002`/`REC-003`) dùng lại được.
        don.ChuyenDoiSoat(ReconciliationStatus.Mismatched, StatusActorType.Reconciliation, null, "PhatHienLech");
        don.ChuyenDoiSoat(ReconciliationStatus.Resolved, StatusActorType.Staff, ai, "XuLyLech");

        Assert.Equal(ReconciliationStatus.Resolved, don.ReconciliationStatus);
        Assert.Equal(2, don.ReconciliationVersion);
        Assert.Equal(2, don.StatusHistory.Count(s => s.Axis == "Reconciliation"));

        // 🔒 `Resolved` là hấp thụ ⇒ mọi chuyển tiếp NÉM 0006.
        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenDoiSoat(ReconciliationStatus.Matched, StatusActorType.Reconciliation, null, "DoiSoatLai"));
        Assert.Equal(OrderErrorCodes.TrangThaiHapThu, loi.Code);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ② CHIỀU NGƯỢC — CHUYỂN SAI PHẢI NÉM LỖI CÓ MÃ
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nhay_coc__Pending_sang_Delivered__NEM_0001()
    {
        var don = DonMoi();

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Delivered, StatusActorType.Staff, NguoiDung(), "NhayCoc"));

        Assert.Equal(OrderErrorCodes.ChuyenTrangThaiKhongHopLe, loi.Code);
        // 🔒 Không có thay đổi nào: state + lịch sử + version đều nguyên vẹn (rollback ở tầng domain).
        Assert.Equal(FulfillmentStatus.Pending, don.FulfillmentStatus);
        Assert.Empty(don.StatusHistory);
        Assert.Equal(0, don.FulfillmentVersion);
    }

    [Fact]
    public void Tu_chuyen_sang_chinh_minh__NEM_0001()
    {
        var don = DonMoi();

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Pending, StatusActorType.Staff, NguoiDung(), "KhongDoi"));

        Assert.Equal(OrderErrorCodes.ChuyenTrangThaiKhongHopLe, loi.Code);
    }

    [Fact]
    public void Don_da_huy__moi_chuyen_tiep__NEM_0006()
    {
        var don = DonMoi();
        var ai = NguoiDung();
        don.HuyDon(StatusActorType.Staff, ai, "KhachHuy");

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanSauKhiHuy"));

        Assert.Equal(OrderErrorCodes.TrangThaiHapThu, loi.Code);
        Assert.Equal(FulfillmentStatus.Pending, don.FulfillmentStatus);
    }

    [Fact]
    public void Don_da_tra__moi_chuyen_tiep__NEM_0006()
    {
        var don = DonMoi();
        var ai = NguoiDung();
        don.ChuyenKetThuc(ReturnStatus.Returned, StatusActorType.Staff, ai, "TraHangToanBo");

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenKetThuc(ReturnStatus.ReturnRequested, StatusActorType.Staff, ai, "YeuCauTraLai"));

        Assert.Equal(OrderErrorCodes.TrangThaiHapThu, loi.Code);
    }

    [Fact]
    public void Huy_don_DA_XUAT_KHO__bi_chan__NEM_0001()
    {
        var don = DonMoi();
        var ai = NguoiDung();
        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "XacNhanDon");

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.HuyDon(StatusActorType.Staff, ai, "HuySauKhiXuatKho"));

        Assert.Equal(OrderErrorCodes.ChuyenTrangThaiKhongHopLe, loi.Code);
        Assert.False(don.IsCancelled);
    }

    [Fact]
    public void Thieu_ReasonCode__NEM_0004()
    {
        var don = DonMoi();

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, NguoiDung(), "   "));

        Assert.Equal(OrderErrorCodes.ThieuThongTinChuyenTrangThai, loi.Code);
    }

    [Fact]
    public void Nhan_vien_KHONG_co_ActorId__NEM_0004()
    {
        var don = DonMoi();

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, null, "XacNhanDon"));

        Assert.Equal(OrderErrorCodes.ThieuThongTinChuyenTrangThai, loi.Code);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ KHÔNG CÒN ĐƯỜNG VÒNG — test reflection (TC-20 / BR-001)
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Order_KHONG_con_phuong_thuc_SetStatus()
    {
        // 🔒 BR-SAL-003-001: cấm tồn tại `Order.SetStatus(...)`. Nếu ai đó "khôi phục cho tiện" ⇒ đỏ.
        var phuongThuc = typeof(Order).GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(phuongThuc);
    }

    [Theory]
    [InlineData(nameof(Order.Status))]
    [InlineData(nameof(Order.FulfillmentStatus))]
    [InlineData(nameof(Order.PaymentStatus))]
    [InlineData(nameof(Order.ReconciliationStatus))]
    [InlineData(nameof(Order.ReturnStatus))]
    [InlineData(nameof(Order.IsCancelled))]
    public void Truc_trang_thai_KHONG_co_setter_cong_khai(string ten)
    {
        // 🔒 NFR-SAL-003-008 / TC-20: 0 property trục có setter công khai.
        var thuocTinh = typeof(Order).GetProperty(ten, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(thuocTinh);
        Assert.Null(thuocTinh!.GetSetMethod(nonPublic: false));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ④ TRỤC TÀI CHÍNH — giữ nguyên luật đã kiểm chứng, nay CÓ LỊCH SỬ
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Thanh_toan__Unpaid_sang_Paid__ghi_lich_su_truc_Payment()
    {
        var don = DonMoi();

        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.Paid));

        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
        Assert.Equal(1, don.PaymentVersion);
        var dong = Assert.Single(don.StatusHistory);
        Assert.Equal("Payment", dong.Axis);
        Assert.Equal((int)PaymentStatus.Unpaid, dong.FromStatus);
        Assert.Equal((int)PaymentStatus.Paid, dong.ToStatus);
        Assert.Equal("PaymentGateway", dong.ActorType);
    }

    [Fact]
    public void Thanh_toan__bi_chan_thi_tra_FALSE__va_KHONG_ghi_lich_su()
    {
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Paid);

        Assert.False(don.DatTrangThaiThanhToan(PaymentStatus.Failed));
        Assert.False(don.DatTrangThaiThanhToan(PaymentStatus.Paid)); // trùng ⇒ không phải chuyển dịch
        Assert.Equal(PaymentStatus.Paid, don.PaymentStatus);
        Assert.Single(don.StatusHistory);
    }

    [Fact]
    public void Thanh_toan__Failed_sang_Paid__luoi_an_toan_querydr__van_duoc()
    {
        var don = DonMoi();
        don.DatTrangThaiThanhToan(PaymentStatus.Failed);

        Assert.True(don.DatTrangThaiThanhToan(PaymentStatus.Paid));
        Assert.Equal(2, don.StatusHistory.Count);
    }
}
