using System.Text.RegularExpressions;
using eCommerce.Notification;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-14` — `BR-NOT-001-010` + `D-269`: masker là **HÀM THUẦN, một cửa** che PII/secret lọt vào nội dung
/// render và lý do. Bộ ca dưới đây chứng minh từng luật (i)–(vi) của `D-269` ①(2) **thật sự chạy**, kèm ca
/// quét mã nguồn bảo đảm tầng Domain không còn đường ghi **thô** vào bốn cột đã che.
///
/// 🔒 Mọi ca ở đây chạy trên `NotificationMasker.CheBiMat(...)` — **hàm thuần**, không DB, không I/O.
/// </summary>
public class NotificationMaskerTests
{
    // ── (i) Email: giữ ký tự đầu + *** + @domain ─────────────────────────────────────────────

    [Fact]
    public void TC14_email_giu_ky_tu_dau_va_domain()
    {
        var ketQua = NotificationMasker.CheBiMat("Gui ve khach.abc@example.com.vn ngay");

        Assert.DoesNotContain("khach.abc@example.com.vn", ketQua);
        Assert.Contains("k***@example.com.vn", ketQua);
    }

    // ── (ii) SĐT di động VN: giữ 2 đầu + 3 cuối; cho dấu cách/./- ───────────────────────────

    [Fact]
    public void TC14_sdt_di_dong_bi_che_ke_ca_co_dau_phan_cach()
    {
        var ketQua = NotificationMasker.CheBiMat("Goi 0912345678 hoac 0912.345.678 hay 0912-345-678");

        Assert.DoesNotContain("0912345678", ketQua);
        Assert.DoesNotContain("0912.345.678", ketQua);
        Assert.DoesNotContain("0912-345-678", ketQua);
        Assert.Contains("09***678", ketQua);
    }

    [Fact]
    public void TC14_sdt_di_dong_dau_84_bi_che()
    {
        var ketQua = NotificationMasker.CheBiMat("Lien he +84 912 345 678 de xac nhan");

        Assert.Contains("+84***678", ketQua);
        Assert.DoesNotContain("912 345 678", ketQua);
        Assert.DoesNotContain("912345678", ketQua.Replace(" ", string.Empty));
    }

    // ── (iii) PAN 13–19 chữ số liền: chỉ giữ 4 cuối ─────────────────────────────────────────

    [Fact]
    public void TC14_PAN_chi_giu_4_cuoi()
    {
        var ketQua = NotificationMasker.CheBiMat("The 4111111111111111 het han 12/28");

        Assert.DoesNotContain("4111111111111111", ketQua);
        Assert.Contains("***1111", ketQua);
        Assert.Contains("12/28", ketQua);
    }

    // ── (iv) tham số query bí mật ───────────────────────────────────────────────────────────

    [Fact]
    public void TC14_tham_so_query_token_sig_code_bi_che()
    {
        var ketQua = NotificationMasker.CheBiMat(
            "https://app.vn/cb?token=abc123&sig=xyz789&signature=sss&code=DH1&key=k1&password=p1");

        Assert.DoesNotContain("abc123", ketQua);
        Assert.DoesNotContain("xyz789", ketQua);
        Assert.DoesNotContain("DH1", ketQua);
        Assert.Contains("token=***", ketQua);
        Assert.Contains("sig=***", ketQua);
        Assert.Contains("signature=***", ketQua);
        Assert.Contains("code=***", ketQua);
        Assert.Contains("key=***", ketQua);
        Assert.Contains("password=***", ketQua);
    }

    [Fact]
    public void TC14_cap_otp_token_bi_che()
    {
        var ketQua = NotificationMasker.CheBiMat("Ma OTP: otp=123456 va token=abcdef");

        Assert.DoesNotContain("123456", ketQua);
        Assert.DoesNotContain("abcdef", ketQua);
        Assert.Contains("otp=***", ketQua);
        Assert.Contains("token=***", ketQua);
    }

    // ── (v) KHÔNG mask tiền / ngày / MaDon / hotline / số bàn ───────────────────────────────

    [Fact]
    public void TC14_tien_ngay_madon_hotline_so_ban_giu_nguyen()
    {
        const string goc =
            "Tong 1.500.000 ₫, ngay 26/11/2026, MaDon DH20261126001, hotline 1900123456, so ban 024 1234 5678";

        Assert.Equal(goc, NotificationMasker.CheBiMat(goc));
    }

    // ── (vi) idempotent + null/rỗng ─────────────────────────────────────────────────────────

    [Fact]
    public void TC14_chay_hai_lan_cho_cung_ket_qua()
    {
        const string goc =
            "a@b.com / 0912345678 / 4111111111111111 / otp=123456 / 1.500.000 ₫ / 26/11/2026 / DH20261126001";

        var lan1 = NotificationMasker.CheBiMat(goc);
        var lan2 = NotificationMasker.CheBiMat(lan1);

        Assert.NotEqual(goc, lan1); // đã mask thật (không phải no-op)
        Assert.Equal(lan1, lan2);   // idempotent
    }

    [Fact]
    public void TC14_null_hoac_rong_tra_chuoi_rong()
    {
        Assert.Equal(string.Empty, NotificationMasker.CheBiMat(null));
        Assert.Equal(string.Empty, NotificationMasker.CheBiMat(string.Empty));
    }

    // ── (iv) ca quét mã nguồn: KHÔNG còn chỗ ghi thô ở tầng Domain ──────────────────────────

    /// <summary>
    /// `BR-NOT-001-010` / `D-269`: bốn cột đã che chỉ được gán từ `NotificationMasker.CheBiMat(...)`.
    /// Quét **tầng Domain** (loại `bin`/`obj`); bỏ qua mapping/DTO projection ở Application — chúng chỉ
    /// **đọc** giá trị đã che, không phải cửa ghi thô.
    /// </summary>
    [Fact]
    public void TC14_bon_cot_da_che_chi_duoc_gan_tu_CheBiMat_trong_Domain()
    {
        var goc = TimGocRepo();
        var thuMucDomain = Path.Combine(goc, "modules", "Notification", "eCommerce.Notification.Domain");
        Assert.True(Directory.Exists(thuMucDomain), "Không tìm thấy thư mục Domain: " + thuMucDomain);

        var mau = new Regex(
            @"^\s*(RenderedSubject|RenderedBodyMasked|LastReasonMasked|ReasonMasked)\s*=\s*(.+)$",
            RegexOptions.CultureInvariant);

        var viPham = new List<string>();

        foreach (var tep in Directory.GetFiles(thuMucDomain, "*.cs", SearchOption.AllDirectories))
        {
            // Bỏ mã sinh tự động (bin/obj) để không dương tính giả.
            if (tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var soDong = 0;
            foreach (var dong in File.ReadAllLines(tep))
            {
                soDong++;
                var m = mau.Match(dong);
                if (m.Success && !m.Groups[2].Value.Contains("NotificationMasker.CheBiMat(", StringComparison.Ordinal))
                {
                    viPham.Add($"{Path.GetRelativePath(goc, tep)}:{soDong}: {dong.Trim()}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 Có đường ghi THÔ vào cột đã-che (BR-010/D-269):\n  " + string.Join("\n  ", viPham));
    }

    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null && !File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx")))
        {
            thuMuc = thuMuc.Parent;
        }

        Assert.True(thuMuc is not null, "Không tìm thấy gốc repo (eCommerce.slnx) từ " + AppContext.BaseDirectory);
        return thuMuc!.FullName;
    }
}
