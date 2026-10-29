using System.Reflection;
using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **`S17` — BIÊN HỢP ĐỒNG CỦA CÁC DỊCH VỤ CÒN Ở HOST** (29/10/2026).
///
/// **VẤN ĐỀ:** sau khi tách 4 module (24/10/2026), một số app service/worker **không thể** rời host vì
/// chúng đọc entity của module khác (`Channel`, `ChannelListing`, `ReconRun`) hoặc là điểm nối giữa hai
/// module. 📌 Nhưng *"implementation ở host"* **không được** kéo theo *"module phụ thuộc host"* — module
/// khác/host chỉ được phụ thuộc **hợp đồng** (`I*`) nằm ở `Application.Contracts` của module sở hữu.
///
/// 🔑 **VÌ SAO PHẢI KHOÁ BẰNG TEST (không bằng lời):** đây là loại biên **xói mòn âm thầm** — ai đó thêm
/// `using` vào module để dùng lớp hiện thực ở host, hoặc chuyển hợp đồng ngược về `src/`, thì **build vẫn
/// xanh**, test nghiệp vụ vẫn xanh, và biên module **biến mất** (đúng bài học `Customers.Application` →
/// `src/eCommerce.Domain` ở `ke-hoach-tach-module` §6 A2).
///
/// 📌 **Phạm vi 5 dịch vụ của `S17`** (danh sách lấy từ `docs/ban-giao-gd1-con-lai.md` §2):
/// `ChannelDirectory`, `HienThiKenhAppService` (Catalog) · `ChinhSachHanMucAppService`,
/// `WarehouseAppService` (Inventory) · `MarketplaceReconciliationWorker` (Sales/SAL-005).
/// </summary>
public class HopDongHostTests
{
    /// <summary>
    /// 🚨 **HỢP ĐỒNG PHẢI NẰM TRONG MODULE SỞ HỮU.** `typeof(I*).Assembly` là bằng chứng **đo được**
    /// (không phải đọc chữ trong csproj): nếu ai chuyển interface ngược về `src/eCommerce.Application.Contracts`
    /// thì test này đỏ ngay, kể cả khi mọi `using` được sửa cho build xanh.
    /// </summary>
    [Theory]
    [InlineData(typeof(eCommerce.Catalog.Dtos.IChannelDirectory), "eCommerce.Catalog.Application.Contracts")]
    [InlineData(typeof(eCommerce.Catalog.Dtos.IHienThiKenhAppService), "eCommerce.Catalog.Application.Contracts")]
    [InlineData(typeof(eCommerce.Application.Contracts.Inventory.IChinhSachHanMucAppService), "eCommerce.Inventory.Application.Contracts")]
    [InlineData(typeof(eCommerce.Application.Contracts.Inventory.IWarehouseAppService), "eCommerce.Inventory.Application.Contracts")]
    public void Hop_dong_PHAI_nam_trong_Application_Contracts_cua_module_so_huu(Type hopDong, string tenAssembly)
    {
        var thucTe = hopDong.Assembly.GetName().Name;

        Assert.True(string.Equals(tenAssembly, thucTe, StringComparison.Ordinal),
            $"🚨 Hợp đồng `{hopDong.Name}` nằm ở assembly `{thucTe}`, KHÔNG phải `{tenAssembly}` "
            + "⇒ module sở hữu không còn là nguồn sự thật của hợp đồng (biên module bị xói mòn).");
    }

    /// <summary>
    /// 🚨 **HIỆN THỰC Ở ĐÂU PHẢI ĐÚNG LÀ KẾT QUẢ CỦA `S17`.** `WarehouseAppService` đã về module (nó chỉ
    /// cần Domain của chính module). Ba lớp còn lại **cố ý** ở host vì đọc entity của module khác — và
    /// `MarketplaceReconciliationWorker` ở host vì nó là **điểm nối** với Messaging/Sales, không module nào
    /// tham chiếu nó (xem test bên dưới).
    /// </summary>
    [Theory]
    [InlineData(typeof(eCommerce.Inventory.WarehouseAppService), "eCommerce.Inventory.Application")]
    [InlineData(typeof(eCommerce.Catalog.ChannelDirectory), "eCommerce.Application")]
    [InlineData(typeof(eCommerce.Catalog.Dtos.HienThiKenhAppService), "eCommerce.Application")]
    [InlineData(typeof(eCommerce.Application.Inventory.ChinhSachHanMucAppService), "eCommerce.Application")]
    [InlineData(typeof(eCommerce.Application.Marketplace.MarketplaceReconciliationWorker), "eCommerce.Application")]
    public void Hien_thuc_phai_o_dung_assembly_da_chot(Type hienThuc, string tenAssembly)
    {
        var thucTe = hienThuc.Assembly.GetName().Name;

        Assert.True(string.Equals(tenAssembly, thucTe, StringComparison.Ordinal),
            $"🚨 `{hienThuc.FullName}` nằm ở assembly `{thucTe}`, KHÔNG phải `{tenAssembly}` "
            + "⇒ hoặc nó đã bị chuyển mà không cập nhật `S17`/tài liệu, hoặc nó bị đặt lại vào host.");
    }

    /// <summary>
    /// 🚨 **ĐĂNG KÝ DI PHẢI TƯỜNG MINH** (`S17`). Khi hiện thực ở host mà hợp đồng ở module, cặp
    /// `I*` → lớp hiện thực **không được** để ABP tự đoán theo quy ước đặt tên: bài học `IXuLyKetQuaCong`
    /// (`S5`) là **`HTTP 500`** ngay request đầu tiên, và **không** cổng nào đỏ trước đó.
    /// 📌 `[ExposeServices]` **ghim** cặp đó; test này biến việc quên thành **đỏ ở unit test**.
    /// </summary>
    [Theory]
    [InlineData(typeof(eCommerce.Catalog.Dtos.IChannelDirectory), typeof(eCommerce.Catalog.ChannelDirectory))]
    [InlineData(typeof(eCommerce.Catalog.Dtos.IHienThiKenhAppService), typeof(eCommerce.Catalog.Dtos.HienThiKenhAppService))]
    [InlineData(typeof(eCommerce.Application.Contracts.Inventory.IChinhSachHanMucAppService), typeof(eCommerce.Application.Inventory.ChinhSachHanMucAppService))]
    [InlineData(typeof(eCommerce.Application.Contracts.Inventory.IWarehouseAppService), typeof(eCommerce.Inventory.WarehouseAppService))]
    public void Hien_thuc_PHAI_dang_ky_DI_tuong_minh_cho_hop_dong(Type hopDong, Type hienThuc)
    {
        var thuocTinh = hienThuc.GetCustomAttributes<Volo.Abp.DependencyInjection.ExposeServicesAttribute>();

        Assert.Contains(thuocTinh, t => t.ServiceTypes.Contains(hopDong));
    }

    /// <summary>
    /// 🚨 **MODULE KHÔNG ĐƯỢC THAM CHIẾU TRỰC TIẾP LỚP HIỆN THỰC Ở HOST.** Chỉ được dùng **hợp đồng**
    /// (`I*`). 📌 Phép quét **bỏ chú thích** trước khi áp luật — nếu không, một dòng comment nhắc tên lớp
    /// (rất nhiều trong dự án này) sẽ làm test **xanh vô nghĩa** (bài học mutation test ở
    /// `BackgroundWorkerCoCheTests.BoChuThich`).
    /// </summary>
    [Theory]
    [InlineData("ChannelDirectory")]
    [InlineData("HienThiKenhAppService")]
    [InlineData("ChinhSachHanMucAppService")]
    [InlineData("MarketplaceReconciliationWorker")]
    public void Module_KHONG_duoc_tham_chieu_lop_hien_thuc_o_host(string tenLop)
    {
        var viPham = new List<string>();

        foreach (var (duongDanTuongDoi, noiDung) in DocMaNguonModule())
        {
            // `\b` phía trước để `IChannelDirectory` KHÔNG khớp `ChannelDirectory`.
            if (Regex.IsMatch(noiDung, $@"\b{Regex.Escape(tenLop)}\b"))
            {
                viPham.Add(duongDanTuongDoi);
            }
        }

        Assert.True(viPham.Count == 0,
            $"🚨 module tham chiếu **lớp hiện thực ở host** `{tenLop}` (chỉ được phụ thuộc hợp đồng `I{tenLop}`):\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ Chuyển sang dùng interface ở `Application.Contracts` của module sở hữu, "
            + "hoặc chuyển hiện thực vào module nếu nó không còn phụ thuộc host.");
    }

    /// <summary>
    /// 🚨 **MODULE KHÔNG ĐƯỢC THAM CHIẾU PROJECT `src/eCommerce.Application`.** Đây là biên của
    /// `ke-hoach-tach-module` L3 (*host → module*, **không** ngược lại): nếu module tham chiếu project
    /// hiện thực của host thì mọi hợp đồng ở `Application.Contracts` trở thành **trang trí**.
    /// </summary>
    [Fact]
    public void Module_KHONG_duoc_tham_chieu_project_hien_thuc_cua_host()
    {
        var thuMucModule = Path.Combine(GocRepo(), "modules");
        var viPham = new List<string>();

        foreach (var tep in Directory.GetFiles(thuMucModule, "*.csproj", SearchOption.AllDirectories))
        {
            var noiDung = File.ReadAllText(tep);

            if (Regex.IsMatch(noiDung, @"src[\\/]eCommerce\.Application[\\/]", RegexOptions.IgnoreCase))
            {
                viPham.Add(Path.GetRelativePath(thuMucModule, tep));
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 module tham chiếu `src/eCommerce.Application` (host là nơi **hiện thực** hợp đồng — CẤM):\n  "
            + string.Join("\n  ", viPham));
    }

    // ── Đọc nguồn ────────────────────────────────────────────────────────────

    private static List<(string DuongDan, string NoiDung)> DocMaNguonModule()
    {
        var goc = Path.Combine(GocRepo(), "modules");
        var ket = new List<(string, string)>();

        foreach (var tep in Directory.EnumerateFiles(goc, "*.cs", SearchOption.AllDirectories))
        {
            if (tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            ket.Add((Path.GetRelativePath(goc, tep), BoChuThich(File.ReadAllText(tep))));
        }

        return ket;
    }

    /// <summary>Bỏ `//` và `/* */` trước khi áp luật (cùng lý do + cùng đánh đổi đã ghi ở `BackgroundWorkerCoCheTests`).</summary>
    private static string BoChuThich(string ma)
    {
        var khongKhoi = Regex.Replace(ma, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        return string.Join(
            '\n',
            khongKhoi.Split('\n').Select(dong =>
            {
                var viTri = dong.IndexOf("//", StringComparison.Ordinal);
                return viTri >= 0 ? dong[..viTri] : dong;
            }));
    }

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

        throw new DirectoryNotFoundException("KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm kiến trúc VÔ NGHĨA.");
    }
}
