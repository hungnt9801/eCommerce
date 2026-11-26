using System.Reflection;
using eCommerce.Notification;
using Volo.Abp;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-04`/`TC-43`/`TC-35`/`TC-23` — **engine render là hàm THUẦN** (`SRS §3.6` / `ARCH §3.6`).
///
/// 🔒 Cùng input ⇒ cùng output (100 lần); **không** DB, **không** `DateTime.Now`; placeholder ngoài whitelist
/// ⇒ `0015`; thiếu trường bắt buộc ⇒ `0013`; `Subject` + giá trị thay vào **loại `\r`/`\n`/NUL** ở **một**
/// hàm thuần; tiền qua **một** hàm định dạng (`vi-VN`, VND 0 chữ số).
/// </summary>
public class NotificationRendererTests
{
    private static readonly NotificationMoneyFormatter Money = new();

    private static NotificationTemplateContent Mau(
        string? subject,
        string body,
        params NotificationPlaceholder[] placeholders)
        => new(subject, body, placeholders);

    [Fact]
    public void TC04_render_tai_lap_100_lan_cung_ket_qua()
    {
        var renderer = new NotificationRenderer();
        var mau = Mau(
            "Đơn {{MaDon}}",
            "Tổng tiền: {{TongTien}}",
            new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true),
            new NotificationPlaceholder("TongTien", NotificationPlaceholderKind.Money, true));

        var duLieu = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaDon"] = "O-1",
            ["TongTien"] = "1234567"
        };

        var dau = renderer.Render(mau, duLieu, "vi", Money);

        for (var i = 0; i < 100; i++)
        {
            var lai = renderer.Render(mau, duLieu, "vi", Money);
            Assert.Equal(dau.Subject, lai.Subject);
            Assert.Equal(dau.Body, lai.Body);
            Assert.Equal(dau.BodyFingerprint, lai.BodyFingerprint);
        }

        Assert.Equal(ContentFingerprint.TinhThan(dau.Subject, dau.Body), dau.BodyFingerprint);
    }

    [Fact]
    public void TC04_renderer_khong_giu_trang_thai_khong_the_cham_DB_hay_dong_ho()
    {
        // Hàm thuần ⇒ KHÔNG field instance (không repository, không IClock, không cache) — chạy được không cần DB.
        var field = typeof(NotificationRenderer)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        Assert.Empty(field);
    }

    [Fact]
    public void TC43_Subject_va_gia_tri_thay_vao_loai_CR_LF_NUL()
    {
        var renderer = new NotificationRenderer();
        var mau = Mau(
            "Đơn\r\n{{MaDon}}\0",
            "Nội dung: {{MaDon}}",
            new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true));

        var duLieu = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaDon"] = "A\r\nB\0C"
        };

        var ketQua = renderer.Render(mau, duLieu, "vi", Money);

        Assert.DoesNotContain('\r', ketQua.Subject);
        Assert.DoesNotContain('\n', ketQua.Subject);
        Assert.DoesNotContain('\0', ketQua.Subject);
        Assert.DoesNotContain('\r', ketQua.Body);
        Assert.DoesNotContain('\n', ketQua.Body);
        Assert.DoesNotContain('\0', ketQua.Body);
        Assert.Contains("ABC", ketQua.Subject, StringComparison.Ordinal);

        // Cùng input ⇒ cùng output (hàm thuần).
        Assert.Equal(ketQua.Subject, renderer.Render(mau, duLieu, "vi", Money).Subject);
    }

    [Fact]
    public void TC35_thieu_truong_bat_buoc_thi_NEM_0013()
    {
        var renderer = new NotificationRenderer();
        var mau = Mau(
            "Đơn {{MaDon}}",
            "Cảm ơn {{MaDon}}",
            new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true));

        var ex = Assert.Throws<BusinessException>(
            () => renderer.Render(mau, new Dictionary<string, string>(StringComparer.Ordinal), "vi", Money));

        Assert.Equal(NotificationErrorCodes.ThieuTruongBatBuoc, ex.Code);
    }

    [Fact]
    public void TC35_placeholder_ngoai_whitelist_thi_NEM_0015()
    {
        var renderer = new NotificationRenderer();
        // Template dùng `{{La}}` nhưng whitelist CHỈ có `MaDon`.
        var mau = Mau(
            "Đơn {{MaDon}}",
            "Giá trị lạ: {{La}}",
            new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true));

        var duLieu = new Dictionary<string, string>(StringComparer.Ordinal) { ["MaDon"] = "O-1" };

        var ex = Assert.Throws<BusinessException>(() => renderer.Render(mau, duLieu, "vi", Money));

        Assert.Equal(NotificationErrorCodes.PlaceholderNgoaiWhitelist, ex.Code);
    }

    [Fact]
    public void TC35_placeholder_tuy_chon_thieu_thi_thay_chuoi_rong_khong_nem()
    {
        var renderer = new NotificationRenderer();
        var mau = Mau(
            subject: null,
            body: "A{{Phu}}B",
            new NotificationPlaceholder("Phu", NotificationPlaceholderKind.Text, false));

        var ketQua = renderer.Render(mau, new Dictionary<string, string>(StringComparer.Ordinal), "vi", Money);

        Assert.Equal("AB", ketQua.Body);
    }

    [Fact]
    public void TC23_dinh_dang_tien_VND_khong_thap_phan_vi_VN()
    {
        var renderer = new NotificationRenderer();
        var mau = Mau(
            subject: null,
            body: "Tổng: {{TongTien}}",
            new NotificationPlaceholder("TongTien", NotificationPlaceholderKind.Money, true));

        var duLieu = new Dictionary<string, string>(StringComparer.Ordinal) { ["TongTien"] = "1234567" };

        var ketQua = renderer.Render(mau, duLieu, "vi", Money);

        Assert.Equal("Tổng: 1.234.567", ketQua.Body);
        Assert.Equal("1.234.567", Money.Format(1_234_567m));
    }

    [Fact]
    public void TC43_ham_lam_sach_loai_dung_ba_ky_tu_dieu_khien()
    {
        Assert.Equal("abc", NotificationTextSanitizer.LoaiKyTuDieuKhien("a\r\nb\0c"));
        Assert.Equal(string.Empty, NotificationTextSanitizer.LoaiKyTuDieuKhien(null));
        Assert.Equal(string.Empty, NotificationTextSanitizer.LoaiKyTuDieuKhien(string.Empty));
        Assert.Equal("giữ nguyên", NotificationTextSanitizer.LoaiKyTuDieuKhien("giữ nguyên"));
    }
}
