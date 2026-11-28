using System.Text.RegularExpressions;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — KIẾN TRÚC + BẤT BIẾN TIỀN CỦA MODULE `Pricing`** (`D-320` vi; `K06-12`).
///
/// Hai nhóm luật:
///   1. **Ranh giới module** (`D-324` ①/M2): `modules/Pricing/**` **KHÔNG** được nhắc **TÊN ENTITY** của
///      Inventory (`StockLedgerEntry`/`StockDocument`/`StockDocumentLine`/`InventoryItem`). `using eCommerce.Inventory;`
///      **được phép** (chỉ dùng enum/DTO của Contracts) — bất biến là **0 tên entity**, không phải 0 `using`.
///   2. **K06-12 — bất biến tiền** (`D-318` C5 iv): **0** `double`/`float`, **0** `Math.Round`/`decimal.Round`,
///      **0** so sánh tiền bằng `==` trên `decimal` trong Domain + Application (làm tròn chỉ ở biên ra qua
///      `IRoundingPolicy`). Bỏ **dòng chú thích** trước khi quét để comment giải thích luật không bị tính là vi phạm.
/// </summary>
public class CostEngineKienTrucTests
{
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

    private static IReadOnlyList<string> TepNguon(params string[] thuMucCon)
    {
        var goc = Path.Combine(GocRepo(), "modules", "Pricing");

        return Directory.GetFiles(goc, "*.cs", SearchOption.AllDirectories)
            .Where(t => !t.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !t.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(t => thuMucCon.Any(m => t.Contains(
                $"{Path.DirectorySeparatorChar}{m}{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>Bỏ dòng chú thích để luật chỉ áp lên MÃ, không áp lên văn bản giải thích luật.</summary>
    private static IEnumerable<(string Tep, int Dong, string Ma)> DongMa(string tep)
    {
        var soDong = 0;

        foreach (var dongGoc in File.ReadAllLines(tep))
        {
            soDong++;
            var dong = dongGoc.Trim();

            if (dong.StartsWith("//", StringComparison.Ordinal) || dong.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            var viTri = dong.IndexOf("//", StringComparison.Ordinal);
            if (viTri >= 0)
            {
                dong = dong[..viTri].Trim();
            }

            if (dong.Length > 0)
            {
                yield return (tep, soDong, dong);
            }
        }
    }

    // ── 1. Ranh giới module: 0 TÊN ENTITY Inventory ─────────────────────────────────────────────

    /// <summary>
    /// `D-324` ①/M2 — `modules/Pricing/**` **KHÔNG** được nhắc tên entity của Inventory. Pricing chỉ thấy
    /// enum/DTO qua `Inventory.Application.Contracts`.
    /// </summary>
    [Fact]
    public void Pricing_KHONG_nhac_ten_entity_cua_Inventory()
    {
        var cam = new Regex(
            @"\b(StockLedgerEntry|StockDocumentLine|StockDocument|InventoryItem)\b",
            RegexOptions.CultureInvariant);

        var viPham = TepNguon("eCommerce.Pricing.Domain", "eCommerce.Pricing.Application",
                "eCommerce.Pricing.Application.Contracts", "eCommerce.Pricing.EntityFrameworkCore")
            .SelectMany(DongMa)
            .Where(x => cam.IsMatch(x.Ma))
            .Select(x => $"{Path.GetRelativePath(GocRepo(), x.Tep)}:{x.Dong} ⇒ {x.Ma}")
            .ToList();

        Assert.True(viPham.Count == 0,
            "`modules/Pricing` tham chiếu TÊN ENTITY của Inventory (CẤM — D-324/M2):\n  "
            + string.Join("\n  ", viPham));
    }

    // ── 2. K06-12: bất biến tiền ────────────────────────────────────────────────────────────────

    /// <summary>K06-12 — **0** `double`/`float` trong mã Domain + Application của Pricing.</summary>
    [Fact]
    public void K06_12_khong_dung_double_float()
    {
        var cam = new Regex(@"\b(double|float)\b", RegexOptions.CultureInvariant);

        var viPham = TepNguon("eCommerce.Pricing.Domain", "eCommerce.Pricing.Application")
            .SelectMany(DongMa)
            .Where(x => cam.IsMatch(x.Ma))
            .Select(x => $"{Path.GetRelativePath(GocRepo(), x.Tep)}:{x.Dong} ⇒ {x.Ma}")
            .ToList();

        Assert.True(viPham.Count == 0,
            "K06-12: tiền phải `decimal`/`Money`; cấm `double`/`float`:\n  " + string.Join("\n  ", viPham));
    }

    /// <summary>K06-12 — **0** `Math.Round`/`decimal.Round` (làm tròn chỉ qua `IRoundingPolicy` ở biên ra).</summary>
    [Fact]
    public void K06_12_khong_tu_lam_tron_trong_domain_application()
    {
        var cam = new Regex(@"\b(Math|decimal)\s*\.\s*Round\s*\(", RegexOptions.CultureInvariant);

        var viPham = TepNguon("eCommerce.Pricing.Domain", "eCommerce.Pricing.Application")
            .SelectMany(DongMa)
            .Where(x => cam.IsMatch(x.Ma))
            .Select(x => $"{Path.GetRelativePath(GocRepo(), x.Tep)}:{x.Dong} ⇒ {x.Ma}")
            .ToList();

        Assert.True(viPham.Count == 0,
            "K06-12: cấm tự làm tròn trong Domain/Application (dùng `IRoundingPolicy` — XC-001):\n  "
            + string.Join("\n  ", viPham));
    }

    /// <summary>
    /// K06-12 — **0** so sánh **tiền** bằng `==` trên `decimal`. Chỉ chặn các trường tiền đã biết của Pricing
    /// (`Amount`/`AmountValue`/`DonGiaNhap`/`GiaBinhQuan`); so `Money` bằng `==` là hợp lệ (toán tử của `Money`).
    /// </summary>
    [Fact]
    public void K06_12_khong_so_sanh_tien_bang_dau_bang()
    {
        var cam = new Regex(
            @"(\.(Amount|AmountValue|DonGiaNhap|GiaBinhQuan)\s*==|==\s*[A-Za-z_][A-Za-z0-9_.]*\.(Amount|AmountValue|DonGiaNhap|GiaBinhQuan)\b|\b(AmountValue|DonGiaNhap|GiaBinhQuan)\s*==)",
            RegexOptions.CultureInvariant);

        var viPham = TepNguon("eCommerce.Pricing.Domain", "eCommerce.Pricing.Application")
            .SelectMany(DongMa)
            .Where(x => cam.IsMatch(x.Ma))
            .Select(x => $"{Path.GetRelativePath(GocRepo(), x.Tep)}:{x.Dong} ⇒ {x.Ma}")
            .ToList();

        Assert.True(viPham.Count == 0,
            "K06-12: cấm so sánh TIỀN bằng `==` trên `decimal` (dùng `Money.CompareTo`):\n  "
            + string.Join("\n  ", viPham));
    }
}
