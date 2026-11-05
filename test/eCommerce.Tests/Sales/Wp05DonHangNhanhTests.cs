using eCommerce.Application.Sales;
using eCommerce.Orders;

namespace eCommerce.Tests.Sales;

/// <summary>
/// 🆕 **`WP-05`** (`N6`, 03/11/2026) — **ĐƠN HÀNG THEO 18 TRẠNG THÁI NHANH**.
///
/// Bộ test này khoá **bốn** bất biến dễ vỡ nhất của WP-05:
/// <list type="number">
///   <item><b>APPEND-ONLY của enum</b> (`D-17`): 9 giá trị cũ **giữ nguyên số** `0…8`; thêm đúng 9 giá trị mới
///     ở cuối ⇒ đủ **18**. Đổi số cũ = *diễn giải lại toàn bộ lịch sử đơn* mà **không có lỗi nào**.</item>
///   <item><b>Nhãn hiển thị suy ra</b> từ 4 trục (`03 §3.5`) — UI chỉ hiện **một** nhãn, DB vẫn 4 trục.</item>
///   <item><b>Đường đi trọn vòng đời</b> `Mới → … → Thành công` **chỉ qua cạnh có trong bảng**, và mỗi bước
///     **tác động kho đúng** (`D-39`: xác nhận = chốt giữ chỗ; đã hoàn = nhập lại kho).</item>
///   <item><b>Huỷ theo 18 trạng thái</b>: ghi **AI huỷ** (khách/hệ thống/HVC) **và** đóng đơn trong **một** lần
///     gọi; chỉ huỷ được khi **hàng chưa rời kho**.</item>
/// </list>
///
/// 📌 Mọi ca ở đây chạy **không cần DB** (domain thuần + bảng tab) — đúng tinh thần *"luật ở nơi test được"*.
/// Phần tích hợp (bút toán kho thật, HTTP) đo bằng `tools/probe-don-hang.ps1` + probe SQL trong handover.
/// </summary>
public class Wp05DonHangNhanhTests
{
    private static Order DonMoi() => new(Guid.NewGuid(), "WEB-DH-WP05", "web");

    private static Guid NguoiDung() => Guid.NewGuid();

    /// <summary>Đưa đơn tới `OutForDelivery` qua **đúng** đường của bảng cạnh (chưa giao).</summary>
    private static Order DonDangGiao()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        foreach (var buoc in new[]
                 {
                     FulfillmentStatus.Confirmed,
                     FulfillmentStatus.Packing,
                     FulfillmentStatus.Packed,
                     FulfillmentStatus.AwaitingPickup,
                     FulfillmentStatus.OutForDelivery
                 })
        {
            don.ChuyenThucHien(buoc, StatusActorType.Staff, ai, "BuocTuDong");
        }

        return don;
    }

    /// <summary>Đưa đơn tới `Delivered` qua **đúng** đường của bảng cạnh (không nhảy cóc).</summary>
    private static Order DonDaGiao()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        foreach (var buoc in new[]
                 {
                     FulfillmentStatus.Confirmed,
                     FulfillmentStatus.Packing,
                     FulfillmentStatus.Packed,
                     FulfillmentStatus.AwaitingPickup,
                     FulfillmentStatus.OutForDelivery,
                     FulfillmentStatus.Delivered
                 })
        {
            don.ChuyenThucHien(buoc, StatusActorType.Staff, ai, "BuocTuDong");
        }

        return don;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ① APPEND-ONLY — 18 TRẠNG THÁI
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FulfillmentStatus__co_DUNG_18_gia_tri()
    {
        // 📌 Nhanh: 18 trạng thái (`nhanh-pos-module-map.md` §6.2). Đếm ở đây để thêm giá trị mà quên cập nhật
        // tài liệu/test là **ĐỎ ngay**, không lặng lẽ thành 19.
        Assert.Equal(18, Enum.GetValues<FulfillmentStatus>().Length);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Pending, 0)]
    [InlineData(FulfillmentStatus.Confirmed, 1)]
    [InlineData(FulfillmentStatus.Packing, 2)]
    [InlineData(FulfillmentStatus.AwaitingPickup, 3)]
    [InlineData(FulfillmentStatus.OutForDelivery, 4)]
    [InlineData(FulfillmentStatus.Delivered, 5)]
    [InlineData(FulfillmentStatus.DeliveryFailed, 6)]
    [InlineData(FulfillmentStatus.Returning, 7)]
    [InlineData(FulfillmentStatus.Returned, 8)]
    public void FulfillmentStatus__9_gia_tri_CU_giu_nguyen_SO(FulfillmentStatus giaTri, int so)
    {
        // 🔒 `D-17`: cột `OrdOrders.FulfillmentStatus` lưu **số**. Đổi số của `Delivered` (5) thành 12 là
        // *diễn giải lại toàn bộ đơn đã giao* thành "chờ xác nhận" — sai dữ liệu không có lỗi nào.
        Assert.Equal(so, (int)giaTri);
    }

    [Theory]
    [InlineData(FulfillmentStatus.PendingConfirm, 9)]
    [InlineData(FulfillmentStatus.Confirming, 10)]
    [InlineData(FulfillmentStatus.OutOfStock, 11)]
    [InlineData(FulfillmentStatus.WarehouseSwitched, 12)]
    [InlineData(FulfillmentStatus.Packed, 13)]
    [InlineData(FulfillmentStatus.CustomerCancelled, 14)]
    [InlineData(FulfillmentStatus.SystemCancelled, 15)]
    [InlineData(FulfillmentStatus.CarrierCancelled, 16)]
    [InlineData(FulfillmentStatus.ReturnConfirmed, 17)]
    public void FulfillmentStatus__9_gia_tri_MOI_o_CUOI_dai(FulfillmentStatus giaTri, int so)
    {
        // 📌 Đúng **9** giá trị còn thiếu của danh mục Nhanh (đo được: 9 giá trị cũ đã phủ 9 trạng thái).
        Assert.Equal(so, (int)giaTri);
    }

    [Fact]
    public void Danh_muc_Nhanh__DUNG_so_gia_tri_DO_DUOC__khong_tu_che_cho_du()
    {
        // 🚨 `D-38`: kế hoạch 07 ghi *"OrderType (13), OrderSource (12)"* nhưng danh mục **đo được**
        // (Open API v3 — Model Constant) là **10** và **14**; lý do (24) thì **đúng 24** như kế hoạch.
        // 🔒 Tiền lệ `D-20`: **không** tự chế giá trị để "đủ số" — thiếu thì ghi lại con số, không bịa enum.
        Assert.Equal(10, Enum.GetValues<OrderType>().Length);
        Assert.Equal(14, Enum.GetValues<OrderSource>().Length);
        Assert.Equal(24, Enum.GetValues<OrderReasonCode>().Length);
    }

    [Fact]
    public void Danh_muc_Nhanh__giu_dung_SO_cua_Model_Constant()
    {
        // 📌 Số là **hợp đồng dữ liệu** với Nhanh (`D-33`): đánh số lại = diễn giải sai dữ liệu nhập.
        Assert.Equal(10, (int)OrderType.QuoteRequest);
        Assert.Equal(17, (int)OrderType.LostGoodsCompensation);
        Assert.Equal(48, (int)OrderSource.TikTokShop);
        Assert.Equal(52, (int)OrderSource.ZaloPersonal);
        Assert.Equal(35, (int)OrderReasonCode.KhachDiVang);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ② NHÃN HIỂN THỊ SUY RA (`03 §3.5`)
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    // Huỷ thắng mọi thứ (đơn đã huỷ thì không có "đang giao").
    [InlineData(FulfillmentStatus.Pending, ReturnStatus.Cancelled, true, "Đã huỷ")]
    [InlineData(FulfillmentStatus.CustomerCancelled, ReturnStatus.Open, false, "Đã huỷ")]
    [InlineData(FulfillmentStatus.CarrierCancelled, ReturnStatus.Open, false, "Đã huỷ")]
    // Đã hoàn: cả hai trục đều nói "hàng đã về".
    [InlineData(FulfillmentStatus.Returned, ReturnStatus.Returned, false, "Đã hoàn")]
    // Đường hoàn: Đang hoàn → Xác nhận hoàn → Đã hoàn (`03 §3.5` gộp 2 giá trị đầu; ở đây TÁCH — xem `D-39`).
    [InlineData(FulfillmentStatus.Returning, ReturnStatus.Open, false, "Đang hoàn")]
    [InlineData(FulfillmentStatus.ReturnConfirmed, ReturnStatus.Open, false, "Xác nhận hoàn")]
    // Giao thất bại đứng riêng: đơn còn sống, KHÔNG phải "đang chuyển".
    [InlineData(FulfillmentStatus.DeliveryFailed, ReturnStatus.Open, false, "Thất bại")]
    // Thành công chỉ khi CHƯA có yêu cầu trả; khách đang đòi trả ⇒ "Đang hoàn" (nhãn phải phản ánh việc đang xử lý).
    [InlineData(FulfillmentStatus.Delivered, ReturnStatus.Open, false, "Thành công")]
    [InlineData(FulfillmentStatus.Delivered, ReturnStatus.ReturnRequested, false, "Đang hoàn")]
    [InlineData(FulfillmentStatus.AwaitingPickup, ReturnStatus.Open, false, "Đang chuyển")]
    [InlineData(FulfillmentStatus.OutForDelivery, ReturnStatus.Open, false, "Đang chuyển")]
    [InlineData(FulfillmentStatus.Packed, ReturnStatus.Open, false, "Đã đóng gói")]
    [InlineData(FulfillmentStatus.Packing, ReturnStatus.Open, false, "Đang đóng gói")]
    [InlineData(FulfillmentStatus.Confirmed, ReturnStatus.Open, false, "Đã xác nhận")]
    [InlineData(FulfillmentStatus.PendingConfirm, ReturnStatus.Open, false, "Chờ/Đang xác nhận")]
    [InlineData(FulfillmentStatus.Confirming, ReturnStatus.Open, false, "Chờ/Đang xác nhận")]
    [InlineData(FulfillmentStatus.OutOfStock, ReturnStatus.Open, false, "Hết hàng")]
    [InlineData(FulfillmentStatus.WarehouseSwitched, ReturnStatus.Open, false, "Đổi kho hàng")]
    [InlineData(FulfillmentStatus.Pending, ReturnStatus.Open, false, "Mới")]
    public void Nhan_hien_thi__mot_nhan_tu_4_truc(
        FulfillmentStatus thucHien, ReturnStatus ketThuc, bool daHuy, string mongDoi)
    {
        Assert.Equal(mongDoi, OrderStateMachine.NhanHienThi(thucHien, ketThuc, daHuy));
    }

    [Fact]
    public void Nhan_hien_thi__Moi_gia_tri_enum_deu_co_nhan__khong_roi_vao_nhanh_du_phong()
    {
        // 🔒 Nếu thêm trạng thái mà quên nhãn, nó rơi vào `_ => "Mới"` ⇒ **nói dối** người dùng (đơn đang
        // giao hiện "Mới"). Ca này chặn đúng lỗi đó.
        foreach (var giaTri in Enum.GetValues<FulfillmentStatus>())
        {
            var nhan = OrderStateMachine.NhanHienThi(giaTri, ReturnStatus.Open, false);
            Assert.False(string.IsNullOrWhiteSpace(nhan));

            if (giaTri != FulfillmentStatus.Pending)
            {
                Assert.NotEqual("Mới", nhan);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ ĐƯỜNG ĐI TRỌN VÒNG ĐỜI + TÁC ĐỘNG KHO
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Don_di_tron_Moi_den_Thanh_cong__moi_buoc_dung_canh_va_dung_tac_dong_kho()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        // Các bước theo bảng "thao tác × trạng thái" của Nhanh (§6.1) — **đúng** đường vận hành thật.
        var buoc = new (FulfillmentStatus Den, OrderStateMachine.TacDongKho TacDong)[]
        {
            (FulfillmentStatus.PendingConfirm, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.Confirming, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.Confirmed, OrderStateMachine.TacDongKho.ChotGiuCho),
            (FulfillmentStatus.Packing, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.Packed, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.AwaitingPickup, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.OutForDelivery, OrderStateMachine.TacDongKho.Khong),
            (FulfillmentStatus.Delivered, OrderStateMachine.TacDongKho.Khong)
        };

        var kyVong = FulfillmentStatus.Pending;

        foreach (var (den, tacDong) in buoc)
        {
            Assert.True(OrderStateMachine.CoCanh(OrderAxis.Fulfillment, (int)kyVong, (int)den),
                $"Thiếu cạnh {kyVong} → {den}: UI sẽ hiện nút mà API ném 0001.");

            don.ChuyenThucHien(den, StatusActorType.Staff, ai, "BuocWP05");

            Assert.Equal(den, don.FulfillmentStatus);
            Assert.Equal(tacDong, OrderStateMachine.TacDongKhoKhiVao(den));
            kyVong = den;
        }

        Assert.Equal(OrderStatus.Completed, don.Status);
        Assert.Equal("Thành công", OrderStateMachine.NhanHienThi(
            don.FulfillmentStatus, don.ReturnStatus, don.IsCancelled));

        // 📌 8 bước ⇒ 8 dòng lịch sử, mỗi dòng tăng `FulfillmentVersion` đúng 1.
        Assert.Equal(8, don.StatusHistory.Count);
        Assert.Equal(8, don.FulfillmentVersion);
    }

    [Fact]
    public void Tac_dong_kho__CHI_Confirmed_va_Returned__moi_gia_tri_khac_khong_sinh_but_toan()
    {
        // 🚨 `D-39`: ghi `ShippingOut` sau `Confirmed` sẽ trừ `OnHand` **hai lần** cho một món hàng.
        foreach (var giaTri in Enum.GetValues<FulfillmentStatus>())
        {
            var mongDoi = giaTri switch
            {
                FulfillmentStatus.Confirmed => OrderStateMachine.TacDongKho.ChotGiuCho,
                FulfillmentStatus.Returned => OrderStateMachine.TacDongKho.NhapHangHoan,
                _ => OrderStateMachine.TacDongKho.Khong
            };

            Assert.Equal(mongDoi, OrderStateMachine.TacDongKhoKhiVao(giaTri));
        }
    }

    [Fact]
    public void Duong_hoan__Dang_chuyen_den_Da_hoan_qua_3_buoc_va_nhap_kho_o_buoc_cuoi()
    {
        // 📌 Đường hoàn bắt đầu từ **đơn ĐANG GIAO** (`OutForDelivery`), không phải từ `Delivered`:
        // đơn đã giao xong mà khách trả thì đi **trục kết thúc** (RMA `SAL-006`), không phải trục thực hiện.
        var don = DonDangGiao();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.Returning, StatusActorType.Staff, ai, "ChuyenHoan");
        don.ChuyenThucHien(FulfillmentStatus.ReturnConfirmed, StatusActorType.Staff, ai, "HvcXacNhanHoan");

        Assert.Equal(OrderStateMachine.TacDongKho.NhapHangHoan,
            OrderStateMachine.TacDongKhoKhiVao(FulfillmentStatus.Returned));

        don.ChuyenThucHien(FulfillmentStatus.Returned, StatusActorType.Staff, ai, "NhanHangHoan");

        Assert.Equal(FulfillmentStatus.Returned, don.FulfillmentStatus);
        Assert.Equal(OrderStatus.Returned, don.Status);
    }

    [Fact]
    public void HET_HANG__co_hang_lai_thi_ve_Da_xac_nhan__khong_phai_don_moi()
    {
        var don = DonMoi();
        var ai = NguoiDung();

        don.ChuyenThucHien(FulfillmentStatus.OutOfStock, StatusActorType.Staff, ai, "HetHang");
        Assert.Equal("Hết hàng", OrderStateMachine.NhanHienThi(
            don.FulfillmentStatus, don.ReturnStatus, don.IsCancelled));

        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, ai, "CoHangLai");

        Assert.Equal(FulfillmentStatus.Confirmed, don.FulfillmentStatus);
        // 🔑 Đi qua `OutOfStock` **không** quay về `Pending`: đơn đã được xác nhận khách rồi (giữ lịch sử đúng).
        Assert.Equal(2, don.FulfillmentVersion);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ④ HUỶ THEO 18 TRẠNG THÁI — AI HUỶ + ĐÓNG ĐƠN TRONG MỘT LẦN GỌI
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(FulfillmentStatus.CustomerCancelled)]
    [InlineData(FulfillmentStatus.SystemCancelled)]
    [InlineData(FulfillmentStatus.CarrierCancelled)]
    public void Huy_don_theo_Nhanh__ghi_CA_HAI_truc_va_MOT_ly_do_nghiep_vu(FulfillmentStatus aiHuy)
    {
        var don = DonMoi();

        var lichSu = don.HuyDonNhanh(
            aiHuy, StatusActorType.Staff, NguoiDung(), "KhachYeuCauHuy", OrderReasonCode.DatNham);

        Assert.True(don.IsCancelled);
        Assert.Equal(ReturnStatus.Cancelled, don.ReturnStatus);
        Assert.Equal(aiHuy, don.FulfillmentStatus);           // AI huỷ được ghi lại
        Assert.Equal(OrderStatus.Cancelled, don.Status);      // giá trị suy ra
        Assert.Equal(OrderReasonCode.DatNham, don.ReasonCode); // lý do nghiệp vụ (24 giá trị Nhanh)

        // 📌 **2 dòng lịch sử**: thực hiện (ai huỷ) + kết thúc (đơn đóng) — người đọc log thấy đúng hai trục đổi.
        Assert.Equal(2, lichSu.Count);
        Assert.Equal("Fulfillment", lichSu[0].Axis);
        Assert.Equal("Closure", lichSu[1].Axis);
        Assert.Equal(2, don.StatusHistory.Count);
        Assert.Equal(1, don.FulfillmentVersion);
        Assert.Equal(1, don.ClosureVersion);

        // 🔒 Hấp thụ: huỷ lần hai ⇒ `0006`, KHÔNG im lặng.
        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.HuyDonNhanh(aiHuy, StatusActorType.Staff, NguoiDung(), "HuyLan2"));

        Assert.Equal(OrderErrorCodes.TrangThaiHapThu, loi.Code);
        Assert.Equal(2, don.StatusHistory.Count);
    }

    [Fact]
    public void Huy_don_theo_Nhanh__KHONG_cho_huy_khi_hang_DA_ROI_KHO()
    {
        var don = DonMoi();
        don.ChuyenThucHien(FulfillmentStatus.Confirmed, StatusActorType.Staff, NguoiDung(), "XacNhanDon");

        // 🚨 Đã xác nhận = đã chốt giữ chỗ = hàng đã ra kho. Huỷ bây giờ là **xoá bút toán sổ cái** (append-only)
        // ⇒ phải TRẢ HÀNG. Mã `0001` (bấm sai bước), không phải `0006` (đã kết thúc).
        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.HuyDonNhanh(FulfillmentStatus.CustomerCancelled, StatusActorType.Staff, NguoiDung(), "HuySauXuatKho"));

        Assert.Equal(OrderErrorCodes.ChuyenTrangThaiKhongHopLe, loi.Code);
        Assert.False(don.IsCancelled);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Pending)]
    [InlineData(FulfillmentStatus.PendingConfirm)]
    [InlineData(FulfillmentStatus.Confirming)]
    [InlineData(FulfillmentStatus.OutOfStock)]
    [InlineData(FulfillmentStatus.WarehouseSwitched)]
    public void Co_the_huy__dung_5_trang_thai_hang_con_trong_kho(FulfillmentStatus hienTai)
    {
        var don = DonMoi();

        if (hienTai != FulfillmentStatus.Pending)
        {
            don.ChuyenThucHien(hienTai, StatusActorType.Staff, NguoiDung(), "ToiTrangThai");
        }

        Assert.True(OrderStateMachine.CoTheHuy(hienTai));

        // Mỗi nguồn phải có đủ 3 cạnh huỷ ⇒ UI hiện 3 lựa chọn "ai huỷ" mà không có nút nào ném `0001`.
        foreach (var aiHuy in OrderStateMachine.TrangThaiHuy)
        {
            Assert.True(OrderStateMachine.CoCanh(OrderAxis.Fulfillment, (int)hienTai, (int)aiHuy),
                $"Thiếu cạnh huỷ {hienTai} → {aiHuy}.");

            don.HuyDonNhanh(aiHuy, StatusActorType.Staff, NguoiDung(), "HuyWP05");

            // 📌 Mỗi ca mới: dựng lại đơn (đơn đã huỷ là hấp thụ).
            don = DonMoi();
            if (hienTai != FulfillmentStatus.Pending)
            {
                don.ChuyenThucHien(hienTai, StatusActorType.Staff, NguoiDung(), "ToiTrangThai");
            }
        }
    }

    [Theory]
    [InlineData(FulfillmentStatus.Packing)]
    [InlineData(FulfillmentStatus.Packed)]
    [InlineData(FulfillmentStatus.AwaitingPickup)]
    [InlineData(FulfillmentStatus.OutForDelivery)]
    [InlineData(FulfillmentStatus.Delivered)]
    [InlineData(FulfillmentStatus.Returning)]
    [InlineData(FulfillmentStatus.Returned)]
    public void KHONG_the_huy__sau_khi_hang_roi_kho(FulfillmentStatus hienTai)
    {
        Assert.False(OrderStateMachine.CoTheHuy(hienTai));
        Assert.DoesNotContain(hienTai, OrderStateMachine.CoTheHuyTu);
    }

    [Fact]
    public void Huy_don_theo_Nhanh__trang_thai_dich_KHONG_phai_3_gia_tri_huy_thi_NEM_0011()
    {
        var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
            DonMoi().HuyDonNhanh(FulfillmentStatus.Confirmed, StatusActorType.Staff, NguoiDung(), "Sai"));

        Assert.Equal(OrderErrorCodes.DuLieuDonKhongHopLe, loi.Code);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ⑤ TRƯỜNG KIỂU NHANH — KIỂM GIÁ TRỊ
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dat_van_chuyen__COD_va_phi_ship_va_khoi_luong_KHONG_duoc_am()
    {
        var don = DonMoi();

        foreach (var (cod, phiKhach, phiThuc, nang) in new (decimal?, decimal?, decimal?, int?)[]
                 {
                     (-1m, null, null, null),
                     (null, -1m, null, null),
                     (null, null, -1m, null),
                     (null, null, null, -1)
                 })
        {
            var loi = Assert.Throws<Volo.Abp.BusinessException>(() =>
                don.DatVanChuyen(codAmount: cod, phiShipBaoKhach: phiKhach, phiShipThucTe: phiThuc, weightGram: nang));

            Assert.Equal(OrderErrorCodes.DuLieuDonKhongHopLe, loi.Code);
        }

        // Ca hợp lệ: giá trị **0** được phép (đơn miễn phí ship / COD = 0 vì khách đã chuyển khoản).
        don.DatVanChuyen("GHN", "GHN123", 0m, 0m, 25_000m, 500);

        Assert.Equal("GHN", don.CarrierCode);
        Assert.Equal("GHN123", don.TrackingNumber);
        Assert.Equal(0m, don.CodAmount);
        Assert.Equal(500, don.WeightGram);
    }

    [Fact]
    public void Dat_loai_va_nguon__gia_tri_ngoai_danh_muc_thi_NEM_0011()
    {
        var don = DonMoi();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.DatLoaiVaNguon((OrderType)999, OrderSource.Admin));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            don.DatLoaiVaNguon(OrderType.Delivery, (OrderSource)7));

        // Mặc định của đơn mới = Giao hàng tận nhà + Admin (không phải `0` — `0` KHÔNG có trong danh mục Nhanh).
        Assert.Equal(OrderType.Delivery, don.Type);
        Assert.Equal(OrderSource.Admin, don.Source);

        don.DatLoaiVaNguon(OrderType.BuyAtCounter, OrderSource.Shopee);

        Assert.Equal(OrderType.BuyAtCounter, don.Type);
        Assert.Equal(OrderSource.Shopee, don.Source);
    }

    [Fact]
    public void Danh_dau_ly_do__null_la_XOA_ly_do__khong_phai_so_0()
    {
        var don = DonMoi();

        don.DanhDauLyDo(OrderReasonCode.HetHang);
        Assert.Equal(OrderReasonCode.HetHang, don.ReasonCode);

        don.DanhDauLyDo(null);
        Assert.Null(don.ReasonCode);

        Assert.Throws<Volo.Abp.BusinessException>(() => don.DanhDauLyDo((OrderReasonCode)7));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    // ⑥ 14 TAB — CÙNG MỘT ĐỊNH NGHĨA CHO ĐẾM VÀ LỌC
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Man_danh_sach__co_DUNG_14_tab__key_khong_trung__nhan_khong_rong()
    {
        Assert.Equal(14, OrderTabs.DanhSach.Count);
        Assert.Equal(14, OrderTabs.DanhSach.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(OrderTabs.DanhSach, t => Assert.False(string.IsNullOrWhiteSpace(t.Label)));
        Assert.Null(OrderTabs.Tim("khong-co-tab-nay"));
    }

    [Fact]
    public void Tab__Can_xu_ly__gom_don_phai_co_nguoi_lam__va_loai_don_dang_tren_duong()
    {
        var tab = OrderTabs.Tim("can-xu-ly")!;
        var loc = tab.Loc.Compile();

        // ① Đơn mới: PHẢI có trong tab.
        Assert.True(loc(DonMoi()));

        // ② Đơn đang giao: HVC lo ⇒ KHÔNG thuộc tab.
        var dangGiao = DonMoi();
        foreach (var buoc in new[]
                 {
                     FulfillmentStatus.Confirmed, FulfillmentStatus.Packing, FulfillmentStatus.Packed,
                     FulfillmentStatus.AwaitingPickup, FulfillmentStatus.OutForDelivery
                 })
        {
            dangGiao.ChuyenThucHien(buoc, StatusActorType.Staff, NguoiDung(), "Buoc");
        }

        Assert.False(loc(dangGiao));

        // ③ Giao THẤT BẠI: phải có người xử lý (giao lại/chuyển hoàn) ⇒ THUỘC tab.
        dangGiao.ChuyenThucHien(FulfillmentStatus.DeliveryFailed, StatusActorType.Staff, NguoiDung(), "ThatBai");
        Assert.True(loc(dangGiao));

        // ④ Đã huỷ: KHÔNG thuộc (không còn gì phải làm).
        Assert.False(loc(DonDaHuy()));
    }

    [Fact]
    public void Tab__Dem_va_Loc_CUNG_dinh_nghia__moi_don_roi_dung_MOT_trong_13_tab_trang_thai()
    {
        // 🚨 Bất biến của 14 tab: mỗi đơn (không huỷ) thuộc **đúng một** tab trạng thái
        // (13 tab còn lại sau `all`) — nếu không, badge cộng lại **không bằng** "Tất cả" và người dùng
        // thấy tab nói dối (đếm 7 mà bấm vào ra 5).
        var don = DonMoi();

        var trangThaiTabs = OrderTabs.DanhSach.Where(t => t.Key is not ("all" or "can-xu-ly")).ToList();

        var thuoc = trangThaiTabs.Where(t => t.Loc.Compile()(don)).Select(t => t.Key).ToList();

        Assert.Single(thuoc);
        Assert.Equal("pending", thuoc[0]);
    }

    [Fact]
    public void Tab__da_hoan__va__da_huy__khong_chong_nhau()
    {
        var daHuy = DonDaHuy();

        // Đơn ĐÃ GIAO rồi khách trả: đi **trục kết thúc** (RMA) — trục thực hiện vẫn `Delivered`.
        // 🚨 Đây chính là ca làm badge nói dối nếu tab "Thành công" không loại `ReturnStatus ≠ Open`.
        var daHoan = DonDaGiao();
        daHoan.ChuyenKetThuc(ReturnStatus.ReturnRequested, StatusActorType.Staff, NguoiDung(), "KhachXinTra");

        Assert.True(OrderTabs.Tim("returning")!.Loc.Compile()(daHoan));
        Assert.False(OrderTabs.Tim("delivered")!.Loc.Compile()(daHoan));
        Assert.False(OrderTabs.Tim("returned")!.Loc.Compile()(daHoan));

        daHoan.ChuyenKetThuc(ReturnStatus.ReturnApproved, StatusActorType.Staff, NguoiDung(), "DuyetTra");
        daHoan.ChuyenKetThuc(ReturnStatus.Returned, StatusActorType.Staff, NguoiDung(), "DaTraXong");

        Assert.True(OrderTabs.Tim("returned")!.Loc.Compile()(daHoan));
        Assert.False(OrderTabs.Tim("returning")!.Loc.Compile()(daHoan));
        Assert.False(OrderTabs.Tim("delivered")!.Loc.Compile()(daHoan));

        Assert.True(OrderTabs.Tim("cancelled")!.Loc.Compile()(daHuy));
        Assert.False(OrderTabs.Tim("returned")!.Loc.Compile()(daHuy));
        Assert.False(OrderTabs.Tim("cancelled")!.Loc.Compile()(daHoan));
    }

    private static Order DonDaHuy()
    {
        var don = DonMoi();
        don.HuyDonNhanh(FulfillmentStatus.CustomerCancelled, StatusActorType.Staff, NguoiDung(), "KhachHuy");
        return don;
    }
}
