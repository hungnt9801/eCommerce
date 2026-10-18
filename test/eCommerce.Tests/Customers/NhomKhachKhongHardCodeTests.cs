using Xunit;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🛡️ **TC-21 — NHÓM KHÁCH KHÔNG ĐƯỢC HARD-CODE** (`CUS-001` **`ADR-09`** + **`BR-CUS-0015`**), làm ở `S3`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO ĐÂY LÀ BẤT BIẾN, KHÔNG PHẢI KHẨU VỊ:**
/// Nhóm khách là **đầu vào của bảng giá** (`PRC-001` **Q5**). Nếu `RETAIL`/`WHOLESALE`/`VIP` nằm trong **mã**:
/// <list type="number">
///   <item>thêm một nhóm = **release lại phần mềm** — trong khi đó là **quyết định kinh doanh**, không phải kỹ thuật;</item>
///   <item>🔒 nguy hiểm hơn: một `if (nhom == "VIP")` **rải rác** trong code tạo ra **luật giá thứ hai**,
///     📌 nằm ngoài bảng giá — và **không ai** tìm thấy khi khách hỏi *"vì sao đơn của tôi giá khác"*;</item>
///   <item>đổi tên nhóm trong DB ⇒ code **so sánh với chuỗi cũ** vẫn chạy, **không lỗi**, chỉ **sai giá** (im lặng).</item>
/// </list>
/// ✅ **Thiết kế đúng (đang áp dụng):** bảng `CusGroups` + seed idempotent (`CustomerGroupDataSeedContributor`),
/// và mọi luật giá đọc theo `CustomerGroupId` ở `PRC-*`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **GIỚI HẠN:** quét **chuỗi literal** `"RETAIL"`/`"WHOLESALE"`/`"VIP"` trong `src/` + `modules/`.
/// 📌 Nó **không** bắt được `"Ret" + "AIL"` hay một nhóm **khác** bị hard-code (`"GOLD"`). 🔒 Nhưng nó bắt đúng
/// **ba nhóm mà tài liệu đã đặt tên** — tức đúng chỗ mọi người sẽ có ý định viết `if`.
/// </summary>
public class NhomKhachKhongHardCodeTests
{
    /// <summary>Ba mã nhóm **đã đặt tên trong tài liệu** (`CUS-001` §11) — 🔒 chỉ được xuất hiện ở tệp seed.</summary>
    private static readonly string[] MaNhom = ["RETAIL", "WHOLESALE", "VIP"];

    /// <summary>Tệp **DUY NHẤT** được chứa ba mã trên — 📌 vì seed **chính là** chỗ dữ liệu được khai báo.</summary>
    private const string TepSeed = "src/eCommerce.DbMigrator/CustomerGroupDataSeedContributor.cs";

    [Fact]
    public void Ma_nhom_khach__CHI_duoc_xuat_hien_trong_TEP_SEED()
    {
        var goc = TimGocRepo();
        Assert.False(string.IsNullOrWhiteSpace(goc), "Không tìm được gốc repo (mốc `eCommerce.slnx`).");

        var viPham = new List<string>();
        var soTepDaQuet = 0;

        foreach (var thuMucGoc in new[] { "src", "modules" })
        {
            var thuMuc = Path.Combine(goc, thuMucGoc);

            if (!Directory.Exists(thuMuc))
            {
                continue;
            }

            foreach (var tep in Directory.EnumerateFiles(thuMuc, "*.cs", SearchOption.AllDirectories))
            {
                if (LaTepBuild(tep))
                {
                    continue;
                }

                var tuongDoi = Path.GetRelativePath(goc, tep).Replace('\\', '/');

                if (string.Equals(tuongDoi, TepSeed, StringComparison.Ordinal))
                {
                    continue;
                }

                soTepDaQuet++;

                // 📌 Bỏ chú thích trước khi đếm — 🔑 **chính tệp này** và `CuaKiemKhachGiaoDich.cs` có nhắc
                // các mã đó trong XML doc để giải thích; một phép quét thô sẽ báo sai (dương tính giả).
                var ma = BoChuThich(File.ReadAllText(tep));
                var dong = ma.Split('\n');

                for (var i = 0; i < dong.Length; i++)
                {
                    foreach (var ma2 in MaNhom)
                    {
                        // 🔒 Chỉ **chuỗi literal** (`"RETAIL"`), không phải chuỗi con: tránh báo sai cho
                        // tên biến/khoá khác có chứa `VIP` (ví dụ `VipDiscount`).
                        if (dong[i].Contains($"\"{ma2}\"", StringComparison.Ordinal))
                        {
                            viPham.Add($"{tuongDoi}:{i + 1}  →  {dong[i].Trim()}");
                        }
                    }
                }
            }
        }

        // 🛡️ **LƯỚI AN TOÀN CHO CHÍNH PHÉP ĐO:** quét 0 tệp nghĩa là phép đo **vô nghĩa**, không phải "sạch".
        Assert.True(soTepDaQuet > 0,
            "🚨 Phép quét không đọc được tệp `.cs` nào (src/ + modules/) ⇒ test này XANH VÔ NGHĨA.");

        Assert.True(viPham.Count == 0,
            "🚨 CÓ MÃ NHÓM KHÁCH HARD-CODE NGOÀI TỆP SEED ⇒ vi phạm `ADR-09` / `BR-CUS-0015`:\n  "
            + string.Join("\n  ", viPham)
            + "\n\n🔒 Nhóm khách là **DỮ LIỆU** (đầu vào bảng giá `PRC-001` Q5), không phải enum."
            + "\n✅ Cách đúng: đọc `Customer.CustomerGroupId`; luật giá nằm ở `PRC-*`."
            + "\n📌 Nếu bạn ĐANG thêm một nhóm mặc định: thêm vào `" + TepSeed + "` (seed idempotent theo mã).");
    }

    /// <summary>Bỏ chú thích `//`, `///`, `/* … */` — xem lý do ở `CuaKiemKhachBiChanTests.BoChuThich`.</summary>
    private static string BoChuThich(string noiDung)
    {
        var cacDong = noiDung.Split('\n');
        var trongKhoi = false;

        for (var i = 0; i < cacDong.Length; i++)
        {
            var dong = cacDong[i];

            if (trongKhoi)
            {
                var ketThuc = dong.IndexOf("*/", StringComparison.Ordinal);

                if (ketThuc < 0)
                {
                    cacDong[i] = string.Empty;
                    continue;
                }

                dong = dong[(ketThuc + 2)..];
                trongKhoi = false;
            }

            var moKhoi = dong.IndexOf("/*", StringComparison.Ordinal);
            var chuThichDong = dong.IndexOf("//", StringComparison.Ordinal);

            if (moKhoi >= 0 && (chuThichDong < 0 || moKhoi < chuThichDong))
            {
                var ketThuc = dong.IndexOf("*/", moKhoi + 2, StringComparison.Ordinal);

                if (ketThuc < 0)
                {
                    dong = dong[..moKhoi];
                    trongKhoi = true;
                }
                else
                {
                    dong = dong[..moKhoi] + dong[(ketThuc + 2)..];
                }

                chuThichDong = dong.IndexOf("//", StringComparison.Ordinal);
            }

            if (chuThichDong >= 0)
            {
                dong = dong[..chuThichDong];
            }

            cacDong[i] = dong;
        }

        return string.Join('\n', cacDong);
    }

    private static bool LaTepBuild(string duongDan)
        => duongDan.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || duongDan.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string TimGocRepo()
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

        return string.Empty;
    }
}
