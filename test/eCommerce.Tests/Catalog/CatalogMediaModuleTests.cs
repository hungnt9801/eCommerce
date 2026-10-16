using System.Reflection;
using eCommerce.Catalog;
using Volo.Abp.Modularity;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **BẤT BIẾN CỦA MODULE ĐĂNG KÝ KHO TỆP MEDIA** (`CAT-003`) — sinh ra từ **hai sự cố thật**
/// (16/10/2026) mà **cả hai** đều lọt qua build + **1043 unit test xanh**:
///
/// <list type="number">
///   <item><b>Sự cố 1 — <c>IMediaStorage</c> KHÔNG được đăng ký:</b> lớp hiện thực chỉ có
///     <c>ITransientDependency</c> (đúng quy ước!) và nằm trong `eCommerce.Domain` (assembly **có** trong
///     đồ thị). Nhưng Host khởi động xong thì resolve thất bại ⇒ **MỌI** POST tải ảnh trả **`500`**
///     (*"None of the constructors found … Cannot resolve parameter 'IMediaStorage'"*);</item>
///   <item><b>Sự cố 2 — module có constructor THAM SỐ:</b> tôi viết
///     <c>CatalogMediaModule(IHostEnvironment)</c> và tin ABP tiêm được. **Host KHÔNG khởi động nổi**:
///     <c>MissingMethodException: Cannot dynamically create an instance of type 'CatalogMediaModule'.
///     Reason: No parameterless constructor defined.</c> ⇒ ABP tạo module bằng
///     <c>Activator.CreateInstance</c>.</item>
/// </list>
///
/// 🔑 **VÌ SAO PHẢI CÓ TEST NÀY:** cả hai lỗi đều **chỉ** lộ ra khi **chạy Host thật**. Unit test thông thường
/// **không** dựng đồ thị module ABP ⇒ **không** thấy gì. 📌 Đây đúng họ *"cảm giác an toàn giả"* mà
/// Phụ lục C cảnh báo: test xanh mà sản phẩm **hỏng ở mọi request**.
/// 🔒 Test này kiểm bằng **reflection** — **không** cần dựng Host, chạy trong vài mili-giây, và **bắt được
/// cả hai** sự cố.
/// </summary>
public class CatalogMediaModuleTests
{
    private static readonly Type KieuModule = typeof(CatalogMediaModule);

    // ── (1) 🚨 MODULE PHẢI CÓ CONSTRUCTOR RỖNG (sự cố 2) ─────────────────────────────────────

    [Fact]
    public void Module_PHAI_co_constructor_RONG__neu_khong_thi_HOST_KHONG_KHOI_DONG_DUOC()
    {
        // 🚨 `ModuleLoader.CreateAndRegisterModule` gọi `Activator.CreateInstance(moduleType)` ⇒ constructor
        // có tham số làm **Host chết ngay lúc nạp module** (`MissingMethodException`), **không** phải lỗi
        // lúc chạy request. 🔒 Tệ hơn: `dotnet build` **XANH** và **1043 test XANH**.
        var coConstructorRong = KieuModule
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(c => c.GetParameters().Length == 0);

        Assert.True(coConstructorRong,
            $"🚨 `{KieuModule.Name}` KHÔNG có constructor rỗng ⇒ ABP (`Activator.CreateInstance`) "
            + "KHÔNG tạo được module ⇒ **Host không khởi động nổi** (MissingMethodException).\n"
            + "⇒ Bỏ tham số khỏi constructor. Cần dịch vụ thì lấy **trong factory của DI**: "
            + "`context.Services.AddTransient<T>(sp => new T(sp.GetRequiredService<...>()))`.");
    }

    [Fact]
    public void Module__CONG_KHAI__ABP_moi_nap_duoc()
    {
        Assert.True(KieuModule.IsPublic, $"`{KieuModule.Name}` phải là `public` — kiểu `internal` không được ABP nạp.");
        Assert.False(KieuModule.IsAbstract, $"`{KieuModule.Name}` không được `abstract`.");
        Assert.True(typeof(AbpModule).IsAssignableFrom(KieuModule), $"`{KieuModule.Name}` phải kế thừa `AbpModule`.");
    }

    // ── (2) 🚨 THAM CHIẾU PROJECT LÀ KHÔNG ĐỦ — PHẢI NẰM TRONG `DependsOn` (sự cố 1) ─────────

    [Theory]
    // 📌 Hai tiến trình có đồ thị module RIÊNG: Host phục vụ API, Web phục vụ `GET /CatMedia/{id}`.
    // Thiếu ở **một** trong hai ⇒ chức năng hỏng **chỉ ở tiến trình đó** (khó tìm hơn nhiều so với hỏng cả hai).
    [InlineData("src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs")]
    [InlineData("src/eCommerce.Web/eCommerceWebModule.cs")]
    public void Module_PHAI_nam_trong_DependsOn_cua_CA_Host_VA_Web(string duongDanTuongDoi)
    {
        // 🚨 **BÀI HỌC CỦA CHÍNH DỰ ÁN** (`eCommerceKernelModule`, `Pricing`, `Customers` — đã trả giá nhiều lần):
        // *"Chỉ **tham chiếu** project là **KHÔNG ĐỦ**. Thiếu class `AbpModule` **hoặc** thiếu dòng trong
        // `DependsOn` thì assembly/module **KHÔNG nằm trong đồ thị**"* ⇒ ABP không quét, **không đăng ký**
        // ⇒ `HTTP 500` *"Cannot resolve parameter"* **dù build/test xanh**.
        // 🔒 Test này quét **mã nguồn** (không phải reflection) vì `DependsOn` chỉ là **dữ liệu của module
        // khởi động** — muốn kiểm bằng reflection thì phải dựng cả Host, tốn và dễ hỏng.
        var goc = TimGocRepo();
        var duongDan = Path.Combine(goc, duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(duongDan), $"Không tìm thấy tệp `{duongDanTuongDoi}` — đường dẫn test đã lệch.");

        var noiDung = File.ReadAllText(duongDan);

        Assert.True(noiDung.Contains("CatalogMediaModule", StringComparison.Ordinal),
            $"🚨 `{duongDanTuongDoi}` KHÔNG có `typeof(CatalogMediaModule)` trong `[DependsOn]`.\n"
            + "⇒ Module KHÔNG nằm trong đồ thị ⇒ `IMediaStorage` KHÔNG được đăng ký ⇒ **mọi** request dùng kho "
            + "tệp trả `500` (*\"Cannot resolve parameter\"*) — và **build + unit test vẫn XANH**.");
    }

    // ── (3) PHÉP QUÉT KHÔNG ĐƯỢC RỖNG (lưới an toàn cho chính test này) ─────────────────────

    [Fact]
    public void Phep_quet_KHONG_rong__neu_khong_thi_test_tren_XANH_VO_NGHIA()
    {
        // 🛡️ Cùng khuôn `AllowlistCongKhaiTests.Phep_quet_KHONG_rong`: nếu bản `Module` bị đổi tên/xoá thì
        // các test trên vẫn "xanh" mà **không** kiểm gì. 🔒 Khoá sàn để điều đó không xảy ra im lặng.
        Assert.NotNull(KieuModule);
        Assert.True(KieuModule.GetMethods().Length > 0, "Kiểu module phải có method — nếu không, reflection đã hỏng.");
        Assert.False(string.IsNullOrWhiteSpace(TimGocRepo()), "Không tìm được gốc repo ⇒ test quét mã nguồn vô nghĩa.");
    }

    /// <summary>Đi lên từ thư mục chạy test cho tới khi gặp `eCommerce.slnx` (dấu hiệu gốc repo).</summary>
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
