using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Reconciliation;

namespace eCommerce.Tests.Reconciliation;

/// <summary>
/// 🆕 **`S15` — GIAO DỊCH TREO QUÁ HẠN** (`REC-001` §5.4 `StalePendingTransaction` + §6.1 `TriggerRunAsync`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **BA LỖI IM LẶNG MÀ BỘ TEST NÀY CHẶN:**
///   1. **Ngưỡng = 0 bị hiểu thành "gắn cờ mọi thứ"** — cùng họ `Recon.Retention.Days`: `0` = **TẮT**.
///      Nếu `0` lọt vào so sánh thì **mọi** giao dịch `Pending` bị gắn cờ mỗi lượt ⇒ hàng đợi vận hành
///      ngập rác, và người ta **học cách bỏ qua** cảnh báo (mất luôn cảnh báo thật).
///   2. **Cổng "không truy vấn được" (`null`) bị coi là "treo"** — hai nguyên nhân khác nhau
///      (`ProviderUnreachable` vs `StalePendingTransaction`); gộp chúng làm cờ "treo" **không ai dọn được**
///      vì cổng có thể chỉ đang chậm.
///   3. **Hai bản luật / hai khoá** cho *job* và *chạy tay* — `REC-001` §6.1 yêu cầu chạy tay **qua cùng khoá**.
///      Nếu ai đó "sửa nhanh" bằng cách chép vòng quét vào app service thì test quét nguồn dưới đây **ĐỎ**.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class StalePendingTests
{
    private static readonly DateTime Moc = new(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc);

    // ── 1. Luật: tuổi + trạng thái + phản hồi cổng ─────────────────────────────────────────────

    [Theory]
    [InlineData(25, true)]   // quá ngưỡng 24 h + cổng nói "không có giao dịch" ⇒ TREO
    [InlineData(24, true)]   // ĐÚNG bằng ngưỡng ⇒ TREO (>=, không phải >)
    [InlineData(23, false)]  // chưa đủ ngưỡng ⇒ chưa treo
    [InlineData(1, false)]
    public void ChuaKetThuc_congBao91__phu_thuoc_NGUONG_va_bien_dung_bang(int soGio, bool mongDoi)
    {
        var ketQua = LuatStalePending.LaStale(
            daKetThuc: false,
            taoLucUtc: Moc,
            bayGioUtc: Moc.AddHours(soGio),
            responseCodeCong: "91",
            nguongGio: LuatStalePending.NguongMacDinhGio);

        Assert.Equal(mongDoi, ketQua);
    }

    [Fact]
    public void DaKetThuc__KHONG_bao_gio_la_treo_du_tuoi_rat_lon()
    {
        // 🔒 Giao dịch đã `Succeeded` từ 3 tháng trước **không** phải "treo" — nó đã xong. Gắn cờ nó là
        // làm hỏng đúng danh sách mà người vận hành dùng để dọn việc.
        Assert.False(LuatStalePending.LaStale(true, Moc, Moc.AddDays(90), "91", 24));
    }

    [Theory]
    [InlineData(null)]    // không truy vấn được ⇒ `ProviderUnreachable`, KHÔNG phải "treo"
    [InlineData("00")]    // cổng xác nhận đã thu tiền ⇒ luật khác xử lý
    [InlineData("24")]    // khách huỷ
    [InlineData("")]      // rỗng
    public void Cong_KHONG_xac_nhan_91__thi_KHONG_ket_luan_treo(string? responseCode)
    {
        Assert.False(LuatStalePending.LaStale(false, Moc, Moc.AddHours(100), responseCode, 24));
    }

    [Theory]
    [InlineData(0)]       // 0 = TẮT (cùng quy ước `Recon.Retention.Days`) — KHÔNG phải "gắn cờ mọi thứ"
    [InlineData(-1)]
    public void Nguong_TAT__KHONG_gat_co(int nguong)
    {
        Assert.False(LuatStalePending.LaStale(false, Moc, Moc.AddDays(365), "91", nguong));
    }

    [Fact]
    public void Tuoi_AM__KHONG_gat_co__dong_ho_lech_khong_tao_ra_co_sai()
    {
        // `bayGioUtc` < `taoLucUtc` xảy ra khi đồng hồ máy lệch hoặc dữ liệu tương lai.
        Assert.False(LuatStalePending.LaStale(false, Moc, Moc.AddHours(-1), "91", 24));
    }

    [Fact]
    public void DocNguong__mac_dinh_24_va_0_la_TAT_va_chuoi_hong_thi_ve_mac_dinh()
    {
        Assert.Equal(24, LuatStalePending.DocNguong(null));
        Assert.Equal(24, LuatStalePending.DocNguong("   "));
        Assert.Equal(24, LuatStalePending.DocNguong("khong-phai-so"));
        Assert.Equal(6, LuatStalePending.DocNguong("6"));
        Assert.Equal(0, LuatStalePending.DocNguong("0"));
        Assert.Equal(0, LuatStalePending.DocNguong("-3"));
    }

    [Fact]
    public void Nguong_mac_dinh_la_24_gio__va_LON_HON_cua_so_chong_trung_20_gio()
    {
        // 🔑 `Q1` — lý do chọn 24: lớn hơn `DedupWindowHours` (20 h) của cùng job ⇒ không gắn cờ lặp lại
        // mỗi 15 phút. Nếu ai đổi xuống ≤ 20 thì test này **nhắc** rằng phải rà lại tương quan đó.
        Assert.Equal(24, LuatStalePending.NguongMacDinhGio);
        Assert.True(LuatStalePending.NguongMacDinhGio > 20);
    }

    // ── 2. Bất biến kiến trúc: MỘT hiện thực, HAI đường vào, MỘT khoá ──────────────────────────

    [Fact]
    public void Chi_co_DUNG_MOT_hien_thuc_IPaymentReconciliationRunner__va_no_nam_o_Host()
    {
        // 🔒 Nếu ai đó thêm hiện thực thứ hai (ví dụ bản trong Web) thì "cùng khoá" trở thành **hai khoá**
        // hoặc **không khoá** ⇒ job và chạy tay chạy chồng. Test này chặn ngay ở CI.
        //
        // 📌 **NẠP TƯỜNG MINH từ thư mục build** (không tin `AppDomain.GetAssemblies()`): đúng bài học C16 —
        // quét "assembly đã nạp" rất dễ **thiếu** và cho kết quả *"sạch" giả tạo*.
        var daNap = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        foreach (var duongDan in Directory.GetFiles(AppContext.BaseDirectory, "eCommerce*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(duongDan);
                daNap.TryAdd(asm.GetName().Name ?? Path.GetFileNameWithoutExtension(duongDan), asm);
            }
            catch
            {
                // ⚠️ Bỏ qua assembly không nạp được; lưới an toàn bên dưới khẳng định **có** tìm thấy hiện thực.
            }
        }

        var hienThuc = daNap.Values
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPaymentReconciliationRunner).IsAssignableFrom(t))
            .ToList();

        Assert.Single(hienThuc);
        Assert.Equal("eCommerce.Application", hienThuc[0].Assembly.GetName().Name);
    }

    [Fact]
    public void Ten_khoa_la_MOT_nguon__khong_chua_tien_to_ecommerce()
    {
        // `AbpDistributedLockOptions.KeyPrefix` ĐÃ là `ecommerce:` ⇒ thêm tiền tố nữa thành tiền tố đôi
        // (`XC-003` F1). 📌 Kiểm cả hai chiều: có tiền tố thật của nó, và **không** có tiền tố bị ghép lần hai.
        var tenKhoa = eCommerce.Application.PaymentHub.PaymentReconciliationRunner.TenKhoa;

        Assert.StartsWith("payment:", tenKhoa, StringComparison.Ordinal);
        Assert.DoesNotContain("ecommerce:", tenKhoa, StringComparison.Ordinal);
    }

    [Fact]
    public void Worker_KHONG_con_vong_quet__va_AppService_KHONG_goi_cong_truc_tiep()
    {
        // 🚨 **Chốt "một luật, hai đường vào" bằng QUÉT NGUỒN** (rẻ và đúng chỗ): nếu ai đó dán lại vòng quét
        // vào worker (bản cũ) hoặc gọi provider từ app service (bản "chạy tay tự viết") thì test này ĐỎ.
        var goc = TimGocRepo();

        var worker = Doc(goc, "src/eCommerce.Application/PaymentHub/PaymentReconciliationWorker.cs");
        Assert.Contains("IPaymentReconciliationRunner", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("QueryStatusDetailAsync", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAcquireAsync", worker, StringComparison.Ordinal);

        var appService = Doc(
            goc,
            "modules/Reconciliation/eCommerce.Reconciliation.Application/PaymentReconciliationAppService.cs");
        Assert.Contains("IPaymentReconciliationRunner", appService, StringComparison.Ordinal);
        Assert.Contains("TriggerRunAsync", appService, StringComparison.Ordinal);
        Assert.DoesNotContain("IPaymentProvider", appService, StringComparison.Ordinal);
        Assert.DoesNotContain("QueryStatusDetailAsync", appService, StringComparison.Ordinal);

        // 🛡️ Neo dương: runner **phải** thật sự chứa vòng quét — nếu không, hai test trên "xanh vì rỗng".
        var runner = Doc(goc, "src/eCommerce.Application/PaymentHub/PaymentReconciliationRunner.cs");
        Assert.Contains("QueryStatusDetailAsync", runner, StringComparison.Ordinal);
        Assert.Contains("TryAcquireAsync", runner, StringComparison.Ordinal);
        Assert.Contains("LuatStalePending.LaStale", runner, StringComparison.Ordinal);
    }

    // ── tiện ích ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc mã nguồn **đã bỏ chú thích** — chỉ còn mã chạy thật (bài học `StorefrontCongKhaiTests`).</summary>
    private static string Doc(string goc, string duongDanTuongDoi)
    {
        var tho = File.ReadAllText(Path.Combine(goc, duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar)));
        var khongKhoi = Regex.Replace(tho, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(khongKhoi, @"(?m)^[ \t]*//.*$", string.Empty);
    }

    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (Directory.Exists(Path.Combine(thuMuc.FullName, "src", "eCommerce.Web")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy `src/eCommerce.Web` khi đi lên từ " + AppContext.BaseDirectory);
    }
}
