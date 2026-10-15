using eCommerce.Messaging.EventCoverage;

namespace eCommerce.Tests.EventCoverage;

// ── Kiểu CỤC BỘ cho test phân loại ───────────────────────────────────────────
// Lõi so khớp theo **TÊN interface** nên test kiểm chứng đúng LUẬT SO KHỚP mà không phụ thuộc `shared/`
// (giống cách `EventCoverageCheckerTests` đã làm với `IDistributedEventHandler`).

/// <summary>Marker GIẢ — trùng tên với `eCommerce.Kernel.Events.INotificationEvent`.</summary>
public interface INotificationEvent { }

/// <summary>Sự kiện MANG DỮ LIỆU (KHÔNG gắn marker) — mất nó là mất dữ liệu.</summary>
public class SuKienMangDuLieuEto { }

public class NguoiNhanMangDuLieu : IDistributedEventHandler<SuKienMangDuLieuEto> { }

/// <summary>Sự kiện THÔNG BÁO (gắn marker) — dữ liệu đã nằm trong DB.</summary>
public class SuKienThongBaoEto : INotificationEvent { }

public class NguoiNhanThongBao : IDistributedEventHandler<SuKienThongBaoEto> { }

/// <summary>
/// Test cho **PHÂN LOẠI sự kiện** (`INotificationEvent`) — bài học thật 15/10/2026.
///
/// 🚨 VÌ SAO CẦN: trước đây MỌI `*Eto` không có người nhận đều bị báo **Error** kèm thông điệp
/// *"DỮ LIỆU SẼ MẤT IM LẶNG"*. Thông điệp đó **đúng** với `ChannelOrderPulledEto` (lỗi #22 — đơn **chỉ**
/// tồn tại trong sự kiện) nhưng **SAI** với `ChannelShopTokenRefreshedEto` (token đã ghi vào DB).
/// Gộp hai loại ⇒ **dương tính giả ở mọi lần boot** ⇒ người đọc **bỏ qua** cảnh báo ⇒ lỗi thật lọt qua.
/// </summary>
public class EventCoveragePhanLoaiTests
{
    [Fact]
    public void Su_kien_MANG_DU_LIEU_khong_co_nguoi_nhan_thi_vao_nhom_LOI()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienMangDuLieuEto)], []);

        Assert.Contains("SuKienMangDuLieuEto", kq.SuKienKhongCoNguoiNhan);
        Assert.DoesNotContain("SuKienMangDuLieuEto", kq.SuKienThongBaoKhongCoNguoiNhan);
        Assert.True(kq.CoSuKienBiBoQuen);          // ⇒ log Error
        Assert.False(kq.CoSuKienThongBaoBiBoQuen);
    }

    [Fact]
    public void Su_kien_THONG_BAO_khong_co_nguoi_nhan_thi_vao_nhom_CANH_BAO_nhe()
    {
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienThongBaoEto)], []);

        Assert.Contains("SuKienThongBaoEto", kq.SuKienThongBaoKhongCoNguoiNhan);
        Assert.DoesNotContain("SuKienThongBaoEto", kq.SuKienKhongCoNguoiNhan);
        Assert.False(kq.CoSuKienBiBoQuen);         // ⇒ KHÔNG log Error (đây chính là mục đích của marker)
        Assert.True(kq.CoSuKienThongBaoBiBoQuen);  // ⇒ log Warning
    }

    [Fact]
    public void Su_kien_THONG_BAO_CO_nguoi_nhan_thi_khong_canh_bao_gi()
    {
        // Marker KHÔNG được làm hỏng nhánh "đã có người nhận".
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienThongBaoEto)], [typeof(NguoiNhanThongBao)]);

        Assert.Empty(kq.SuKienThongBaoKhongCoNguoiNhan);
        Assert.Empty(kq.SuKienKhongCoNguoiNhan);
        Assert.Contains("SuKienThongBaoEto", kq.SuKienCoNguoiNhan);
    }

    [Fact]
    public void TRUNG_TEN_thi_chon_huong_AN_TOAN_coi_la_MANG_DU_LIEU()
    {
        // 🚨 Luật đã chọn có chủ ý: chỉ coi là "thông báo" khi **MỌI** kiểu cùng tên đều gắn marker.
        // Nếu một bên mang dữ liệu ⇒ vẫn log **Error** (ồn ào hơn — KHÔNG im lặng hơn).
        var tron = EventCoverageChecker.KiemTra(
            [typeof(SuKienThongBaoEto), typeof(Khac.SuKienThongBaoEto)], []);

        Assert.True(tron.CoSuKienBiBoQuen);
        Assert.Empty(tron.SuKienThongBaoKhongCoNguoiNhan);
    }

    [Fact]
    public void Chi_co_kieu_THONG_BAO_thi_khong_bao_loi()
    {
        // Đối chứng cho test trên: **chỉ** kiểu có marker ⇒ nhẹ (không Error).
        var kq = EventCoverageChecker.KiemTra([typeof(SuKienThongBaoEto)], []);

        Assert.False(kq.CoSuKienBiBoQuen);
    }

    [Fact]
    public void LaSuKienThongBao_nhan_biet_theo_TEN_interface()
    {
        Assert.True(EventCoverageChecker.LaSuKienThongBao(typeof(SuKienThongBaoEto)));
        Assert.False(EventCoverageChecker.LaSuKienThongBao(typeof(SuKienMangDuLieuEto)));
    }

    // ── Bất biến cho các ETO THẬT (khoá bằng reflection) ─────────────────────

    [Fact]
    public void ChannelOrderPulledEto_PHAI_la_MANG_DU_LIEU_khong_duoc_gan_marker()
    {
        // 🚨🚨 BẤT BIẾN QUAN TRỌNG NHẤT CỦA FILE NÀY.
        // `ChannelOrderPulledEto` là **dữ liệu đơn hàng** — đơn **CHỈ** tồn tại trong sự kiện (lỗi #22).
        // Nếu ai đó gắn marker "thông báo" cho nó thì cảnh báo lúc khởi động sẽ **im lặng** đúng ở chỗ
        // nguy hiểm nhất ⇒ lỗi mất đơn quay lại mà **không còn tiếng la nào**.
        var eto = typeof(eCommerce.ChannelHub.Events.ChannelOrderPulledEto);

        Assert.False(
            EventCoverageChecker.LaSuKienThongBao(eto),
            "🚨 KHÔNG được gắn `INotificationEvent` cho `ChannelOrderPulledEto`: đơn hàng CHỈ tồn tại " +
            "trong sự kiện này ⇒ mất nó là MẤT ĐƠN (lỗi #22).");
    }

    [Theory]
    [InlineData(typeof(eCommerce.ChannelHub.Events.ChannelShopTokenRefreshedEto))]
    [InlineData(typeof(eCommerce.Catalog.Events.ProductPublishedEto))]
    [InlineData(typeof(eCommerce.Catalog.Events.VariantPriceChangedEto))]
    public void Cac_ETO_THONG_BAO_that_phai_gan_marker(Type eto)
    {
        // Nếu ai bỏ marker ⇒ các ETO này bị báo **Error** ở mọi lần boot ⇒ dương tính giả quay lại.
        Assert.True(
            EventCoverageChecker.LaSuKienThongBao(eto),
            $"`{eto.Name}` phải gắn `INotificationEvent` (dữ liệu đã nằm trong DB trước khi publish).");
    }
}
