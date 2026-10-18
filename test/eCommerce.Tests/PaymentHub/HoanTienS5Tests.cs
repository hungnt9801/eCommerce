using System.Reflection;
using eCommerce.Application.Sales;
using eCommerce.Kernel.Events;
using eCommerce.PaymentHub;
using eCommerce.PaymentHub.Events;
using eCommerce.PaymentHub.Payments;
using Volo.Abp;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **HOÀN TIỀN — DÒNG HOÀN TIỀN + LUẬT GHI NHẬN + CẦU NỐI** (`S5`).
///
/// **VÌ SAO TỆP NÀY TỒN TẠI:** ba thứ mới của `S5` đều là *"quyết định về TIỀN"* mà **không** có lỗi biên dịch
/// nào bảo vệ:
/// <list type="number">
///   <item>**luật ghi nhận** — *cổng nói gì thì sổ ghi gì*; sai một nhánh ⇒ ghi *"đã hoàn"* khi tiền chưa ra
///     (hoặc ngược lại: **giấu** một lần chi tiền thật);</item>
///   <item>**dòng hoàn tiền** — bảng mới trả lời *"hoàn lúc nào, bao nhiêu, vì sao, ai duyệt"*; entity phải chặn
///     dữ liệu vô nghĩa **ở chính nó** (đường ghi tắt không đi qua app service);</item>
///   <item>**cầu nối `PaymentRefundedEto`** — thiếu người nhận thì **hoàn tiền xong đơn vẫn `Paid`**
///     (nợ §16.3 #1 của `PAY-001`) — đúng họ lỗi #22 của dự án (*"phát mà không ai nhận"*).</item>
/// </list>
/// 🔒 **Kiểm CẢ HAI CHIỀU ở mọi ca:** *"luôn đúng"* và *"không hoạt động"* trông giống hệt nhau nếu chỉ đo một
/// phía (bài học `handover` §5.2).
/// </summary>
public class HoanTienS5Tests
{
    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① LUẬT GHI NHẬN — quyết định SỔ CÓ GHI hay không
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Quyet__tien_DA_RA__thi_GhiDaHoan()
    {
        Assert.Equal(
            LuatGhiNhanHoanTien.QuyetDinh.GhiDaHoan,
            LuatGhiNhanHoanTien.Quyet(new RefundResult { DaHoanThat = true, DaNhanYeuCau = true }));
    }

    [Fact]
    public void Quyet__cong_da_nhan_nhung_tien_CHUA_ra__thi_ChoXacNhan_va_KHONG_ghi_so()
    {
        // 🚨 Đây là ca mà việc ghi sổ **sai** sẽ làm "sổ sách nói dối" nặng nhất: mã `00` của VNPay nghĩa là
        // *"đã nhận yêu cầu"*, tiền **chưa** ra (bất đồng bộ ở phía ngân hàng). 📌 `PhaiGhiSo` PHẢI là `false`.
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult { DaNhanYeuCau = true, MaPhanHoi = "00" });

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.ChoXacNhan, quyet);
        Assert.False(LuatGhiNhanHoanTien.PhaiGhiSo(quyet));
    }

    [Fact]
    public void Quyet__ket_qua_RONG_hoan_toan__la_TuChoi__nhung_VAN_khong_ghi_so()
    {
        // ⚠️ Ca biên: provider trả `RefundResult` **rỗng hoàn toàn** (không mã, không lỗi) — dữ liệu vô lý.
        // Luật hiện tại coi đó là `TuChoi` (đọc được phản hồi, mã không phải `00`), 📌 và điều **quan trọng nhất**
        // vẫn đúng: **không** ghi sổ. 🔑 Ghi ca này ra để nếu ai đổi luật thì **thấy** mình đang đổi hành vi của
        // một ca biên, không phải "chỉ refactor".
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult());

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.TuChoi, quyet);
        Assert.False(LuatGhiNhanHoanTien.PhaiGhiSo(quyet));
    }

    [Fact]
    public void Quyet__loi_ha_tang__thi_KHONG_XAC_DINH__va_KHONG_phai_TuChoi()
    {
        // 🚨 **Ca nguy hiểm nhất của toàn bộ luật:** lỗi hạ tầng nghĩa là *"KHÔNG BIẾT cổng có nhận hay chưa"*.
        // Nếu rơi vào nhánh `TuChoi`, người vận hành đọc *"cổng từ chối ⇒ gửi lại được"* ⇒ **hoàn HAI LẦN**
        // (mất tiền thật). ⇒ Phải là `KhongXacDinh` và `ChoXacNhan` phải bật.
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult
        {
            LoiHaTang = "Timeout",
            LyDoThatBai = "Hết thời gian chờ cổng"
        });

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.KhongXacDinh, quyet);
        Assert.NotEqual(LuatGhiNhanHoanTien.QuyetDinh.TuChoi, quyet);
        Assert.False(LuatGhiNhanHoanTien.PhaiGhiSo(quyet));
    }

    [Fact]
    public void Quyet__loi_ha_tang_THANG_da_nhan__vi_khong_biet_cong_co_nhan_hay_khong()
    {
        // ⚠️ Dữ liệu mâu thuẫn (vừa `DaNhanYeuCau` vừa `LoiHaTang`) — hiếm, nhưng **thứ tự kiểm là một phần của
        // luật**: lỗi hạ tầng phải được ưu tiên hơn "đã nhận", vì nó nói *"không chắc"*.
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult { DaNhanYeuCau = true, LoiHaTang = "HttpRequestException" });

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.KhongXacDinh, quyet);
    }

    [Fact]
    public void Quyet__tien_DA_RA_thang_MOI_thu__ke_ca_loi_ha_tang()
    {
        // 🔒 `DaHoanThat` là **sự thật duy nhất** nói tiền đã ra. Bỏ qua nó vì có `LoiHaTang` là **giấu một lần
        // chi tiền thật** — hậu quả nặng hơn hẳn việc ghi sổ sớm.
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult { DaHoanThat = true, LoiHaTang = "la" });

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.GhiDaHoan, quyet);
        Assert.True(LuatGhiNhanHoanTien.PhaiGhiSo(quyet));
    }

    [Fact]
    public void Quyet__cong_tu_choi__thi_TuChoi_va_KHONG_ghi_so()
    {
        // Cổng trả mã khác `00` ⇒ **biết chắc** chưa hoàn ⇒ gửi lại (sau khi sửa tham số) là **hợp lệ**.
        var quyet = LuatGhiNhanHoanTien.Quyet(new RefundResult
        {
            MaPhanHoi = "91",
            LyDoThatBai = "Cổng TỪ CHỐI"
        });

        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.TuChoi, quyet);
        Assert.False(LuatGhiNhanHoanTien.PhaiGhiSo(quyet));
    }

    [Fact]
    public void Quyet__ket_qua_NULL__thi_KHONG_XAC_DINH__khong_doan_la_TuChoi()
    {
        // 📌 `null` là lỗi lập trình; đoán "từ chối" theo hướng đó dẫn tới *"gửi lại được"* ⇒ hoàn hai lần.
        Assert.Equal(LuatGhiNhanHoanTien.QuyetDinh.KhongXacDinh, LuatGhiNhanHoanTien.Quyet(null));
    }

    [Theory]
    [InlineData(LuatGhiNhanHoanTien.QuyetDinh.GhiDaHoan, TrangThaiDongHoanTien.DaHoan)]
    [InlineData(LuatGhiNhanHoanTien.QuyetDinh.ChoXacNhan, TrangThaiDongHoanTien.ChoXacNhan)]
    [InlineData(LuatGhiNhanHoanTien.QuyetDinh.TuChoi, TrangThaiDongHoanTien.TuChoi)]
    [InlineData(LuatGhiNhanHoanTien.QuyetDinh.KhongXacDinh, TrangThaiDongHoanTien.KhongXacDinh)]
    public void SangTrangThaiDong__phu_MOI_quyet_dinh(
        LuatGhiNhanHoanTien.QuyetDinh quyet,
        TrangThaiDongHoanTien mongDoi)
    {
        Assert.Equal(mongDoi, LuatGhiNhanHoanTien.SangTrangThaiDong(quyet));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② DÒNG HOÀN TIỀN — entity phải chặn dữ liệu vô nghĩa Ở CHÍNH NÓ
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    private static PaymentRefund Dong(
        decimal soTien = 100000m,
        string maYeuCau = "RF001",
        string lyDo = "Khách trả hàng theo RMA-2026-0042",
        string nguoi = "ketoan.01@congty.vn",
        Guid? giaoDich = null)
        => new(Guid.NewGuid(), giaoDich ?? Guid.NewGuid(), maYeuCau, soTien, lyDo, nguoi);

    [Fact]
    public void Dong__du_lieu_HOP_LE__phai_tao_duoc__va_o_ChoXacNhan()
    {
        // 🔑 **Chiều ngược của mọi guard**: nếu chỉ kiểm "giá trị hỏng bị ném" thì một validate **quá tay**
        // (chặn cả dữ liệu hợp lệ) cũng XANH — và cả bảng hoàn tiền sẽ **không ghi được dòng nào**.
        var dong = Dong();

        Assert.Equal(100000m, dong.Amount);
        Assert.Equal("RF001", dong.RefundRequestId);
        Assert.Equal(TrangThaiDongHoanTien.ChoXacNhan, dong.Status);   // fail-closed: chưa biết tiền ra
        Assert.False(dong.LaTrangThaiCuoi);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Dong__so_tien_KHONG_DUONG__bi_nem_0004(string soTien)
    {
        var ex = Assert.Throws<BusinessException>(() =>
            Dong(soTien: decimal.Parse(soTien, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(PaymentErrorCodes.SoTienHoanKhongHopLe, ex.Code);
    }

    [Fact]
    public void Dong__thieu_MA_YEU_CAU__bi_nem_0010()
    {
        // 🚨 Vì sao **không** cho phép rỗng: unique index chống trùng lần hoàn dựa vào chính mã này. Mã rỗng ⇒
        // index **vẫn** chặn (một rỗng cũng là một giá trị) nhưng **không đọc được** là lần thứ mấy ⇒ kế toán
        // không đối chiếu được sao kê.
        var ex = Assert.Throws<BusinessException>(() => Dong(maYeuCau: "   "));

        Assert.Equal(PaymentErrorCodes.DongHoanTienKhongHopLe, ex.Code);
    }

    [Fact]
    public void Dong__thieu_GIAO_DICH__bi_nem_0010()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            new PaymentRefund(Guid.NewGuid(), Guid.Empty, "RF001", 100000m, "ly do", "nguoi"));

        Assert.Equal(PaymentErrorCodes.DongHoanTienKhongHopLe, ex.Code);
    }

    [Theory]
    [InlineData(true)]    // thiếu lý do
    [InlineData(false)]   // thiếu người thực hiện
    public void Dong__thieu_LY_DO_hoac_NGUOI__bi_nem_0004(bool thieuLyDo)
    {
        // 📌 Cả hai đều **bắt buộc** vì chúng là câu trả lời cho *"vì sao"* và *"ai duyệt"* — chính hai câu mà
        // bảng này sinh ra để trả lời (`PAY-001` §5.6 #1).
        var ex = Assert.Throws<BusinessException>(() => Dong(
            lyDo: thieuLyDo ? "  " : "ly do hop le",
            nguoi: thieuLyDo ? "nguoi" : " "));

        Assert.Equal(PaymentErrorCodes.SoTienHoanKhongHopLe, ex.Code);
    }

    [Fact]
    public void SinhMaYeuCauHoan__dung_so_thu_tu__va_la_ma_DOC_DUOC()
    {
        Assert.Equal("RF001", PaymentRefund.SinhMaYeuCauHoan(1));
        Assert.Equal("RF002", PaymentRefund.SinhMaYeuCauHoan(2));
        Assert.Equal("RF010", PaymentRefund.SinhMaYeuCauHoan(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void SinhMaYeuCauHoan__so_thu_tu_khong_hop_le__bi_nem(int lanHoan)
    {
        var ex = Assert.Throws<BusinessException>(() => PaymentRefund.SinhMaYeuCauHoan(lanHoan));

        Assert.Equal(PaymentErrorCodes.DongHoanTienKhongHopLe, ex.Code);
    }

    [Fact]
    public void GhiKetQua__lan_dau_ghi__TRA_TRUE__lan_lap_TRA_FALSE()
    {
        // 🔑 Cùng bài học `F-PAY-9`: chỗ **đếm** phải phân biệt *"vừa ghi"* với *"bị hấp thụ"*.
        var dong = Dong();

        Assert.True(dong.GhiKetQua(TrangThaiDongHoanTien.DaHoan, "00", "14991234"));
        Assert.Equal(TrangThaiDongHoanTien.DaHoan, dong.Status);
        Assert.Equal("00", dong.ProviderResponseCode);
        Assert.Equal("14991234", dong.ProviderRefundTransactionId);

        // Lần hai **cùng** trạng thái ⇒ không đổi ⇒ `false`.
        Assert.False(dong.GhiKetQua(TrangThaiDongHoanTien.DaHoan, "00", "14991234"));
    }

    [Fact]
    public void GhiKetQua__trang_thai_CUOI_hap_thu__DaHoan_KHONG_bi_ha_ve_TuChoi()
    {
        // 🚨 Nếu cho phép hạ `DaHoan → TuChoi` thì *"tiền đã ra"* **biến mất khỏi sổ** — đúng loại lỗi mà
        // `PaymentTransaction` đã phải sửa ngày 16/10/2026 (`MarkSucceeded` sau `Refunded`).
        var dong = Dong();
        dong.GhiKetQua(TrangThaiDongHoanTien.DaHoan, "00");

        Assert.False(dong.GhiKetQua(TrangThaiDongHoanTien.TuChoi, "91"));
        Assert.Equal(TrangThaiDongHoanTien.DaHoan, dong.Status);
        Assert.True(dong.LaTrangThaiCuoi);
    }

    [Fact]
    public void GhiKetQua__ChoXacNhan_KHONG_phai_trang_thai_cuoi__phai_con_duong_xac_nhan()
    {
        // 🔒 Nếu coi `ChoXacNhan` là cuối thì một dòng *"chờ xác nhận"* **vĩnh viễn** không xác nhận được —
        // đúng nợ `Q6` của `PAY-001` §15.1, chỉ là đổi chỗ xuất hiện.
        var dong = Dong();

        dong.GhiKetQua(TrangThaiDongHoanTien.ChoXacNhan, "00");
        Assert.False(dong.LaTrangThaiCuoi);

        Assert.True(dong.GhiKetQua(TrangThaiDongHoanTien.DaHoan, "00"));
        Assert.Equal(TrangThaiDongHoanTien.DaHoan, dong.Status);
    }

    [Fact]
    public void GhiKetQua__ma_da_co_KHONG_bi_xoa_boi_lan_ghi_sau_khong_co_ma()
    {
        // 📌 Mã phản hồi là **dữ kiện của cổng** — một lần ghi sau mà không có mã (`null`) **không** được xoá
        // mã đã có, vì xoá là mất dấu vết để điều tra.
        var dong = Dong();

        dong.GhiKetQua(TrangThaiDongHoanTien.ChoXacNhan, "00", "14991234");
        dong.GhiKetQua(TrangThaiDongHoanTien.KhongXacDinh, null, null, "Timeout");

        Assert.Equal("00", dong.ProviderResponseCode);
        Assert.Equal("14991234", dong.ProviderRefundTransactionId);
        Assert.Equal("Timeout", dong.FailureReason);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ MỨC HOÀN — đơn sang `PartiallyRefunded` hay `Refunded`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("100000", "50000", "MotPhan")]
    [InlineData("100000", "99999.9999", "MotPhan")]
    [InlineData("100000", "100000", "ToanPhan")]
    [InlineData("100000", "100000.0001", "ToanPhan")]   // vượt (không nên xảy ra) ⇒ vẫn TOÀN PHẦN
    public void PhanLoaiHoan__dung_mot_phan_hay_toan_phan(string daThu, string daHoan, string mongDoi)
    {
        var muc = LuatCauNoiThanhToan.PhanLoaiHoan(
            decimal.Parse(daThu, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(daHoan, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(mongDoi, muc.ToString());
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ④ CẦU NỐI `PaymentRefundedEto` → ĐƠN
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PHAI_co_handler_nhan_PaymentRefundedEto()
    {
        // 🔑 Không có handler ⇒ **hoàn tiền xong, đơn vẫn `Paid`** (`PAY-001` §16.3 #1). Đây là cùng hình dạng
        // với lỗi #22 (*"phát mà không ai nhận"*), chỉ khác là ở dòng tiền **ra**.
        Assert.True(
            typeof(IDistributedEventHandler<PaymentRefundedEto>).IsAssignableFrom(typeof(OrderPaymentStatusHandler)),
            "🚨 KHÔNG có handler nhận `PaymentRefundedEto` ⇒ tiền đã trả lại mà đơn vẫn `Paid` ⇒ hệ thống " +
            "tưởng khách còn nợ / còn phải giao hàng.");
    }

    [Fact]
    public void PaymentRefundedEto_KHONG_phai_su_kien_thong_bao()
    {
        // Mất sự kiện này thì **dữ liệu sổ** đã đúng (dòng hoàn + RefundedAmount), nhưng **đơn** sai ⇒ đây là
        // sự kiện **MANG DỮ LIỆU**. 📌 Gắn `INotificationEvent` sẽ **hạ** cảnh báo của `EventCoverageChecker`
        // từ Error xuống Warning ⇒ lỗi #22 quay lại mà không ai thấy.
        Assert.False(typeof(INotificationEvent).IsAssignableFrom(typeof(PaymentRefundedEto)));
    }

    [Theory]
    [InlineData(nameof(PaymentRefundedEto.TenantId))]
    [InlineData(nameof(PaymentRefundedEto.AggregateId))]
    [InlineData(nameof(PaymentRefundedEto.PaymentTransactionId))]
    [InlineData(nameof(PaymentRefundedEto.PaymentRefundId))]
    [InlineData(nameof(PaymentRefundedEto.OrderId))]
    [InlineData(nameof(PaymentRefundedEto.MerchantReference))]
    [InlineData(nameof(PaymentRefundedEto.Amount))]
    [InlineData(nameof(PaymentRefundedEto.RefundedAmount))]
    [InlineData(nameof(PaymentRefundedEto.SoTienLanNay))]
    [InlineData(nameof(PaymentRefundedEto.Currency))]
    public void PaymentRefundedEto_CO_du_truong_handler_can(string tenTruong)
    {
        var thuocTinh = typeof(PaymentRefundedEto).GetProperty(tenTruong, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(thuocTinh);

        if (tenTruong is nameof(PaymentRefundedEto.Amount)
            or nameof(PaymentRefundedEto.RefundedAmount)
            or nameof(PaymentRefundedEto.SoTienLanNay))
        {
            // 🔒 Tiền **luôn** là `decimal` — cấm `double` (bất biến #3 của dự án).
            Assert.Equal(typeof(decimal), thuocTinh!.PropertyType);
        }
    }

    [Fact]
    public void PaymentRefundedEto_mang_KHOA_CHONG_TRUNG_rieng()
    {
        // 🔑 `PaymentRefundId` là khoá chống trùng của **chính** sự kiện (một `Guid` cho mỗi dòng hoàn tiền).
        // 📌 Nếu ETO không mang nó thì consumer **không có cách nào** phân biệt hai sự kiện của hai lần hoàn khác
        // nhau trên cùng một giao dịch (rất thật: hoàn một phần 2 lần) — và sẽ **cộng dồn hai lần** hoặc bỏ lần hai.
        var khoa = typeof(PaymentRefundedEto)
            .GetProperty(nameof(PaymentRefundedEto.PaymentRefundId), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(khoa);
        Assert.Equal(typeof(Guid), khoa!.PropertyType);

        // 🔒 `EventId` phải **mới theo từng instance** (mặc định của ETO) — nếu nó là hằng thì mọi sự kiện trùng nhau.
        Assert.NotEqual(new PaymentRefundedEto().EventId, new PaymentRefundedEto().EventId);
    }

    [Fact]
    public void IXuLyKetQuaCong_PHAI_duoc_ExposeServices__neu_khong_endpoint_IPN_tra_500()
    {
        // 🚨 **LỖI THẬT ĐÃ ĐO ĐƯỢC Ở LƯỢT CHẠY ĐẦU CỦA PROBE `S5`:** endpoint IPN trả **`HTTP 500`** với
        // *"Cannot resolve parameter 'eCommerce.PaymentHub.IXuLyKetQuaCong xuLy'"*.
        //
        // **Nguyên nhân KHÔNG phải thiếu đăng ký** — mà là **quy ước ĐẶT TÊN của ABP**: `ITransientDependency`
        // đăng ký lớp theo **tên của chính nó** (`XuLyKetQuaCongService` ⇒ `IXuLyKetQuaCongService`), **không**
        // phải `IXuLyKetQuaCong`. 📌 Và vì `S4` khai hợp đồng mà **không ai gọi**, lỗi này **không** làm đỏ
        // cổng nào — đúng họ bẫy #4 của dự án (*"DI nổ lúc chạy, triệu chứng không trỏ tới chỗ sai"*).
        //
        // 🔒 Test này khoá lại **cơ chế**, không khoá "lời hứa": nếu ai đổi tên lớp/interface mà quên
        // `[ExposeServices]`, cổng sẽ ĐỎ **ngay ở unit test** thay vì ở `HTTP 500` của một IPN thật.
        var thuocTinh = typeof(XuLyKetQuaCongService)
            .GetCustomAttributes<Volo.Abp.DependencyInjection.ExposeServicesAttribute>();

        Assert.Contains(thuocTinh, t => t.ServiceTypes.Contains(typeof(eCommerce.PaymentHub.IXuLyKetQuaCong)));
    }

    [Fact]
    public void Handler_hoan_tien_khong_ghi_thang_trang_thai_ma_di_qua_luat_hap_thu()
    {
        // 🔒 `HandleEventAsync(PaymentRefundedEto)` phải gọi `DatTrangThaiThanhToan` (qua đường chung), **không**
        // gán thẳng `PaymentStatus` — nếu không, một IPN `Succeeded` đến sau khi đã hoàn sẽ **hồi sinh** đơn
        // (luật hấp thụ bị bỏ qua). 📌 Kiểm bằng reflection: method công khai của entity là đường duy nhất, và
        // setter của `Order.PaymentStatus` phải **kín**.
        var setter = typeof(eCommerce.Orders.Order)
            .GetProperty(nameof(eCommerce.Orders.Order.PaymentStatus))!.SetMethod;

        Assert.NotNull(setter);
        Assert.False(setter!.IsPublic, "🚨 setter `Order.PaymentStatus` phải KÍN — chỉ `DatTrangThaiThanhToan` được ghi.");

        Assert.NotNull(typeof(eCommerce.Orders.Order)
            .GetMethod(nameof(eCommerce.Orders.Order.DatTrangThaiThanhToan), BindingFlags.Public | BindingFlags.Instance));
    }
}
