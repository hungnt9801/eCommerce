using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Kernel.Money;
using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Web.Storefront;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// **BẤT BIẾN TIỀN / KIẾN TRÚC** của `PRC-002` — các BR **không** kiểm được bằng một ca hành vi đơn lẻ:
/// `BR-PRC-002-002` (cấm `double`/`float`), `-003` (cấm so tiền bằng `==`), `-007` (cấm tự làm tròn),
/// `-009` (chỉ làm tròn tại R1–R4), `-014` (bậc là DỮ LIỆU), `-019` (tái lập), `-020` (sổ append-only),
/// `-021` (giá vốn không lộ).
///
/// 🎯 Mỗi test dưới đây **đỏ được**: đột biến thật (thêm `double`, thêm `decimal.Round`, đọc hard-code, mở
/// setter sổ, thêm trường giá vốn vào DTO) làm nó đỏ ngay — đúng yêu cầu `testing-negative-control` §2.
/// </summary>
public class PricingBatBienTienTests
{
    // ── BR-PRC-002-002 / NFR-003 — 0 `double`/`float` trong `modules/Pricing` (TC-10) ──────────

    [Fact]
    public void TC10_KHONG_co_double_float_trong_be_mat_Pricing_BR002()
    {
        var viPham = new List<string>();

        foreach (var assembly in CacAssemblyPricing())
        {
            foreach (var kieu in assembly.GetTypes())
            {
                const BindingFlags congKhai =
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (var truong in kieu.GetFields(congKhai))
                {
                    KiemTraKhongPhaiSoThuc(truong.FieldType, $"{kieu.FullName}.{truong.Name} (field)", viPham);
                }

                foreach (var thuocTinh in kieu.GetProperties(congKhai))
                {
                    KiemTraKhongPhaiSoThuc(thuocTinh.PropertyType, $"{kieu.FullName}.{thuocTinh.Name}", viPham);
                }

                foreach (var phuongThuc in kieu.GetMethods(congKhai))
                {
                    KiemTraKhongPhaiSoThuc(phuongThuc.ReturnType, $"{kieu.FullName}.{phuongThuc.Name}()", viPham);

                    foreach (var thamSo in phuongThuc.GetParameters())
                    {
                        KiemTraKhongPhaiSoThuc(thamSo.ParameterType, $"{kieu.FullName}.{phuongThuc.Name}({thamSo.Name})", viPham);
                    }
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "Tiền phải là `decimal`/`Money`, KHÔNG `double`/`float` (BR-PRC-002-002):\n  "
            + string.Join("\n  ", viPham));
    }

    // ── BR-PRC-002-003 — 0 so sánh số tiền bằng `==`/`!=` trên `.Amount` (TC-11) ───────────────

    [Fact]
    public void TC11_KHONG_so_tien_bang_toan_tu_bang_trong_Pricing_BR003()
    {
        var viPham = TimTheoMau(@".Amount\s*[=!]=");

        Assert.True(viPham.Count == 0,
            "Cấm so sánh số tiền bằng `==`/`!=` trên `decimal` (BR-PRC-002-003; so qua `Money`):\n  "
            + string.Join("\n  ", viPham));
    }

    // ── BR-PRC-002-007 — 0 `decimal.Round`/`Math.Round` trong `modules/Pricing` (TC-12) ──────

    [Fact]
    public void TC12_KHONG_decimal_Round_hoac_Math_Round_trong_Pricing_BR007()
    {
        var viPham = TimTheoMau(@"\b(?:decimal|Math)\.Round\s*\(");

        Assert.True(viPham.Count == 0,
            "Mọi làm tròn phải qua `IRoundingPolicy` (BR-PRC-002-007):\n  " + string.Join("\n  ", viPham));
    }

    // ── BR-PRC-002-009 — chỉ làm tròn tại R1–R4: đúng 4 lời gọi cho mỗi dòng (TC-13) ───────────

    [Fact]
    public void TC13_chi_lam_tron_DUNG_4_DIEM_R1_R4_BR009()
    {
        var dem = new DemLamTron();

        var dong = new List<DongCanTinh>
        {
            new("SP-1", 1, Money.Vnd(1_000m), 0.10m),
            new("SP-2", 2, Money.Vnd(2_000m), 0.10m),
        };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(100m), Money.Vnd(50m), dem);

        // R1 (đơn giá) + R2 (giảm giá) + R3 (phí) + R4 (thuế) — mỗi điểm MỘT lời gọi/dòng.
        // Làm tròn ở bước khác (vd trên TỔNG) ⇒ > 8 ⇒ đỏ.
        Assert.Equal(dong.Count * 4, dem.SoLanGoi);
        Assert.Equal("half-away-from-zero", kq.MaChinhSachLamTron);
    }

    // ── BR-PRC-002-014 — bậc thang là DỮ LIỆU: cùng quantity, dữ liệu khác ⇒ kết quả khác ─────

    [Fact]
    public void TC14_bac_thang_la_DU_LIEU_KHONG_hard_code_nguong_BR014()
    {
        var quantity = 12;
        var boA = new[] { new ChonBacThang.Bac(1, Money.Vnd(100m), "a"), new ChonBacThang.Bac(10, Money.Vnd(90m), "b") };
        var boB = new[] { new ChonBacThang.Bac(1, Money.Vnd(100m), "a"), new ChonBacThang.Bac(10, Money.Vnd(80m), "c") };

        var thangA = ChonBacThang.Chon(quantity, boA);
        var thangB = ChonBacThang.Chon(quantity, boB);

        Assert.NotNull(thangA);
        Assert.NotNull(thangB);
        Assert.Equal(thangA!.MinQuantity, thangB!.MinQuantity);           // cùng ngưỡng (dữ liệu giống)
        Assert.NotEqual(thangA.DonGia.Amount, thangB.DonGia.Amount);      // đơn giá LẤY TỪ DỮ LIỆU, không hard-code
    }

    // ── BR-PRC-002-019 — kết quả TÁI LẬP được: 1.000 lần cùng input ⇒ cùng kết quả (TC-16) ─────

    [Fact]
    public void TC16_cung_input_thi_cung_ket_qua_1000_lan_BR019()
    {
        var dong = new List<DongCanTinh>
        {
            new("SP-1", 3, Money.Vnd(189_999.6m), 0.10m),
            new("SP-2", 2, Money.Vnd(52_500.4m), 0.08m),
        };

        var chinhSach = new RoundHalfAwayFromZeroPolicy();

        var dauTien = TinhTienDon.Tinh(dong, Money.Vnd(12_345m), Money.Vnd(7_654m), chinhSach);

        for (var i = 0; i < 1_000; i++)
        {
            var lan = TinhTienDon.Tinh(dong, Money.Vnd(12_345m), Money.Vnd(7_654m), chinhSach);

            Assert.Equal(dauTien.TongThanhToan.Amount, lan.TongThanhToan.Amount);
            Assert.Equal(dauTien.MaChinhSachLamTron, lan.MaChinhSachLamTron);
        }
    }

    // ── BR-PRC-002-020 — sổ giải trình APPEND-ONLY: không api sửa/xoá, không setter công khai ──

    [Fact]
    public void TC15_so_giai_trinh_KHONG_co_duong_sua_xoa_va_KHONG_setter_cong_khai_BR020()
    {
        // (a) Hợp đồng app service KHÔNG có method ghi đè/xoá bản ghi giải trình.
        var tenMethod = typeof(IPriceCalculationAppService)
            .GetMethods()
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(tenMethod, ten => Regex.IsMatch(ten, @"(?i)(update|delete|sua|xoa|edit)"));

        // (b) Mọi property khai trên entity là **get-only công khai** (setter protected) ⇒ không sửa từ ngoài.
        var coSetterCongKhai = typeof(PriceCalculationRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.True(coSetterCongKhai.Count == 0,
            "Sổ giải trình APPEND-ONLY — property sau có setter CÔNG KHAI: " + string.Join(", ", coSetterCongKhai));
    }

    [Fact]
    public void TC15b_ban_ghi_giai_trinh_THIEU_IdempotencyKey_thi_NEM_BR016()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PriceCalculationRecord(
            Guid.NewGuid(), "   ", "web", null, new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc),
            "half-away-from-zero", "VND", 0m, 0m, 0m, 0m, 0m, "{}"));

        Assert.Equal("idempotencyKey", ex.ParamName);
    }

    // ── BR-PRC-002-021 — giá vốn KHÔNG lộ ra DTO công khai (TC-21) ─────────────────────────────

    [Fact]
    public void TC21_gia_von_KHONG_xuat_hien_trong_DTO_cong_khai_BR021()
    {
        Type[] dtoCongKhai =
        [
            typeof(GioTinhTienDto),
            typeof(ChiTietDongDto),
            typeof(ResolvedPriceCandidateDto),
            typeof(SanPhamCongKhaiDto),
            typeof(DongGiaCongKhaiDto),
        ];

        var viPham = new List<string>();

        foreach (var dto in dtoCongKhai)
        {
            foreach (var thuocTinh in dto.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Regex.IsMatch(thuocTinh.Name, @"(?i)(costprice|giávốn|giavon|cost)"))
                {
                    viPham.Add($"{dto.Name}.{thuocTinh.Name}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "Giá vốn KHÔNG được lộ ra DTO công khai (BR-PRC-002-021): " + string.Join(", ", viPham));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private sealed class DemLamTron : IRoundingPolicy
    {
        private readonly IRoundingPolicy _that = new RoundHalfAwayFromZeroPolicy();

        public int SoLanGoi { get; private set; }

        public string Ten => _that.Ten;

        public Money Round(Money value)
        {
            SoLanGoi++;
            return _that.Round(value);
        }
    }

    private static IEnumerable<Assembly> CacAssemblyPricing()
        => new[]
            {
                typeof(TinhTienDon).Assembly,
                typeof(PriceCalculationAppService).Assembly,
                typeof(GioTinhTienDto).Assembly,
            }
            .Distinct();

    private static void KiemTraKhongPhaiSoThuc(Type? kieu, string viTri, List<string> viPham)
    {
        if (kieu is null)
        {
            return;
        }

        if (kieu.IsByRef || kieu.IsArray)
        {
            KiemTraKhongPhaiSoThuc(kieu.GetElementType(), viTri, viPham);
            return;
        }

        if (kieu == typeof(double) || kieu == typeof(float))
        {
            viPham.Add($"{viTri} ⇒ {kieu.Name}");
            return;
        }

        if (kieu.IsGenericType)
        {
            foreach (var thamSoKieu in kieu.GetGenericArguments())
            {
                KiemTraKhongPhaiSoThuc(thamSoKieu, viTri, viPham);
            }
        }
    }

    /// <summary>
    /// Quét <c>modules/Pricing/**/*.cs</c> (bỏ `obj`/`bin`) theo mẫu trên **mã đã gỡ chú thích** — nếu quét cả
    /// chú thích thì một dòng tài liệu chép nguyên `decimal.Round` cũng làm test đỏ vô nghĩa.
    /// </summary>
    private static List<string> TimTheoMau(string mau)
    {
        var goc = Path.Combine(GocRepo(), "modules", "Pricing");
        var regex = new Regex(mau, RegexOptions.CultureInvariant);
        var viPham = new List<string>();

        foreach (var tep in Directory.GetFiles(goc, "*.cs", SearchOption.AllDirectories))
        {
            if (LaTepBuild(tep))
            {
                continue;
            }

            var noiDung = BoChuThich(File.ReadAllText(tep));

            foreach (var dong in noiDung.Split('\n'))
            {
                if (regex.IsMatch(dong))
                {
                    viPham.Add($"{Path.GetRelativePath(goc, tep)}: {dong.Trim()}");
                }
            }
        }

        return viPham;
    }

    private static string BoChuThich(string noiDung)
    {
        var khongBlock = Regex.Replace(noiDung, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return string.Join('\n', khongBlock.Split('\n').Select(dong =>
        {
            var viTri = dong.IndexOf("//", StringComparison.Ordinal);
            return viTri >= 0 ? dong[..viTri] : dong;
        }));
    }

    private static bool LaTepBuild(string duongDan)
        => duongDan.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || duongDan.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string GocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException("KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm VÔ NGHĨA.");
    }
}
