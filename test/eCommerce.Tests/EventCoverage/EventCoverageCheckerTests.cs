using eCommerce.Messaging.EventCoverage;

namespace eCommerce.Tests.EventCoverage;

// ── Định nghĩa cục bộ các interface/handler cho test ──────────────────────────
// Cố ý KHÔNG dùng interface của ABP: lõi kiểm tra so khớp theo TÊN interface, nên test ở đây
// kiểm chứng đúng LUẬT SO KHỚP đó mà không phụ thuộc gói ngoài.

public interface IDistributedEventHandler<T> { }

public interface ILocalEventHandler<T> { }

/// <summary>Interface generic KHÁC — không được tính là "người nhận sự kiện".</summary>
public interface IRepository<T> { }

public class SuKienA { }                      // ETO có người nhận
public class SuKienB { }                      // ETO KHÔNG có người nhận
public class SuKienConLineEto { }             // ETO CON — phải bị bỏ qua
public abstract class SuKienTruuTuongEto { }  // abstract — không tính

public class NguoiNhanA : IDistributedEventHandler<SuKienA> { }

public class NguoiNhanLocalA : ILocalEventHandler<SuKienA> { }

/// <summary>Chỉ hiện thực interface generic KHÁC ⇒ KHÔNG phải người nhận sự kiện.</summary>
public class KhongPhaiNguoiNhan : IRepository<SuKienB> { }

public class SuKienGia : IDistributedEventHandler<SuKienA>, IRepository<SuKienB> { }

/// <summary>
/// Test cho **bộ kiểm tra độ phủ sự kiện** — biến lỗi "publish mà không ai nhận" từ IM LẶNG thành ỒN ÀO.
///
/// 🎯 Vì sao cần: `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho từng đơn hàng kéo từ sàn,
/// không có handler nào nhận, mà vẫn **tiến con trỏ đồng bộ** ⇒ sàn không gửi lại ⇒ **đơn mất vĩnh viễn**
/// và **không có lỗi nào được ghi**. Bộ kiểm tra này làm lỗi đó lộ ra ở **mọi lần khởi động**.
/// </summary>
public class EventCoverageCheckerTests
{
    [Fact]
    public void Su_kien_CO_nguoi_nhan_thi_khong_bao_dong()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienA)], [typeof(NguoiNhanA)]);

        Assert.False(kq.CoSuKienBiBoQuen);
        Assert.Empty(kq.SuKienKhongCoNguoiNhan);
        Assert.Equal(["SuKienA"], kq.SuKienCoNguoiNhan);
    }

    [Fact]
    public void Su_kien_KHONG_co_nguoi_nhan_thi_BAO_DONG()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienB)], [typeof(NguoiNhanA)]);

        Assert.True(kq.CoSuKienBiBoQuen);
        Assert.Equal(["SuKienB"], kq.SuKienKhongCoNguoiNhan);
    }

    [Fact]
    public void ETO_CON_LineEto_bi_BO_QUA_de_khong_co_duong_tinh_gia()
    {
        // `ChannelOrderPulledLineEto` là một DÒNG của `ChannelOrderPulledEto`, không publish độc lập.
        // Nếu tính vào, báo cáo có dương tính giả ⇒ người đọc bắt đầu BỎ QUA cảnh báo (đúng thứ cần tránh).
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienConLineEto)], []);

        Assert.False(kq.CoSuKienBiBoQuen);
        Assert.Empty(kq.SuKienKhongCoNguoiNhan);
        Assert.True(EventCoverageChecker.LaSuKienCon(typeof(SuKienConLineEto)));
    }

    [Fact]
    public void Handler_qua_ILocalEventHandler_cung_duoc_tinh()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienA)], [typeof(NguoiNhanLocalA)]);

        Assert.False(kq.CoSuKienBiBoQuen);
    }

    [Fact]
    public void Interface_generic_KHAC_khong_duoc_tinh_la_nguoi_nhan()
    {
        // 🚨 Bẫy quan trọng: nếu tính MỌI interface generic 1 tham số là "người nhận" thì
        // `IRepository<SuKienB>` sẽ khiến `SuKienB` trông như ĐÃ có người nhận ⇒ bộ kiểm tra
        // **im lặng bỏ qua đúng lỗi cần tìm**. Test này khoá điều đó.
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienB)], [typeof(KhongPhaiNguoiNhan)]);

        Assert.True(kq.CoSuKienBiBoQuen);
        Assert.Equal(["SuKienB"], kq.SuKienKhongCoNguoiNhan);
    }

    [Fact]
    public void Mot_handler_hien_thuc_nhieu_interface_thi_chi_tinh_dung_su_kien_tuong_ung()
    {
        // `SuKienGia` nhận `SuKienA` (handler) VÀ chứa `SuKienB` (chỉ là IRepository) ⇒
        // chỉ `SuKienA` được coi là có người nhận.
        var kq = EventCoverageChecker.KiemTra(
            [typeof(SuKienA), typeof(SuKienB)], [typeof(SuKienGia)]);

        Assert.Equal(["SuKienA"], kq.SuKienCoNguoiNhan);
        Assert.Equal(["SuKienB"], kq.SuKienKhongCoNguoiNhan);
    }

    [Fact]
    public void Lop_ABSTRACT_khong_duoc_tinh_la_su_kien()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienTruuTuongEto)], []);

        Assert.False(kq.CoSuKienBiBoQuen);
    }

    [Fact]
    public void Ket_qua_ON_DINH_va_sap_xep_theo_ten()
    {
        // Ổn định để log/test không phụ thuộc thứ tự assembly nạp.
        var kq = EventCoverageChecker.KiemTra(
            [typeof(SuKienB), typeof(SuKienA), typeof(SuKienB)], []);

        Assert.Equal(["SuKienA", "SuKienB"], kq.SuKienKhongCoNguoiNhan);
    }

    [Fact]
    public void Khong_co_su_kien_nao_thi_sach()
    {
        var kq = EventCoverageChecker.KiemTra([], []);

        Assert.False(kq.CoSuKienBiBoQuen);
        Assert.Empty(kq.SuKienKhongCoNguoiNhan);
    }
}
