using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Volo.Abp.Modularity;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **MODULE ĐĂNG KÝ KHO TỆP MEDIA** — tồn tại **chỉ vì một sự cố thật** (16/10/2026).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **SỰ CỐ:** `MediaStorageNoiBo` có `ITransientDependency` (đúng quy ước, `ProductMediaTests` vẫn xanh) và
/// nằm trong `eCommerce.Domain` — assembly **có** trong đồ thị module. Nhưng Host khởi động xong thì
/// **`IMediaStorage` KHÔNG resolve được**:
/// <code>
/// Autofac.Core.DependencyResolutionException: None of the constructors found on type
/// 'Castle.Proxies.ProductMediaAppServiceProxy' can be invoked with the available services and parameters:
/// Cannot resolve parameter 'eCommerce.Catalog.IMediaStorage khoTep' ...
/// </code>
/// ⇒ **MỌI** POST tải ảnh trả **`500`** (`"code": null`, *"An internal error occurred"*) — **không** phải
/// `400`/`403` có mã. 📌 Probe `cat-003` **bắt được ngay** (32 ca, 5 đạt) — nếu chỉ dựa vào unit test thì
/// **1043 test vẫn xanh** và lỗi này chỉ lộ ra khi có người thật bấm tải ảnh.
///
/// 🔒 **VÌ SAO ĐĂNG KÝ TƯỜNG MINH Ở ĐÂY (không "sửa lại cho quét tự động nhận"):**
/// <list type="number">
///   <item>**Đây là quy ước của chính dự án.** `eCommerceKernelModule` được tạo vì **cùng lý do** — chú thích
///     ở đó ghi rõ: *"Chỉ **tham chiếu** project là **KHÔNG ĐỦ**. Thiếu class `AbpModule` thì assembly
///     **không nằm trong** đồ thị module"*. ⇒ Làm theo khuôn đã có, **không** phát minh cơ chế mới;</item>
///   <item>**Tường minh thì đọc được.** `services.AddTransient<IMediaStorage, MediaStorageNoiBo>()` nói
///     **chính xác** điều gì được đăng ký. 📌 Khi hỏng, ta **không** phải đi tìm xem quét tự động có bỏ sót
///     hay không — đúng loại điều tra tốn thời gian mà chính lượt này vừa trải qua;</item>
///   <item>**Bỏ được `IHostEnvironment`.** Lớp `MediaStorageNoiBo` nay **không** còn phụ thuộc
///     `IHostEnvironment` (một interface hạ tầng **khó kiểm** trong unit test) — đường dẫn gốc do **chỗ
///     đăng ký** truyền vào. 🔑 Hệ quả: `MediaStorageNoiBo` chỉ còn phụ thuộc **`string` + `ILogger`**
///     ⇒ dựng được trong test **không cần** host giả (chính `MediaStorageNoiBoTests` đã phải viết
///     `MoiTruongGia` để bù — nay bỏ được).</item>
/// </list>
///
/// ⚠️ **ĐÁNH ĐỔI ĐÃ CÂN NHẮC:** mất tính "tự động" của quy ước. 📌 Bù lại: đây là **một** lớp duy nhất, và
/// nếu ai đó xoá dòng đăng ký thì **probe HTTP** sẽ đỏ ngay ở ca C1 (không phải im lặng).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
[DependsOn(typeof(eCommerceDomainModule))]
public class CatalogMediaModule : AbpModule
{
    /// <summary>
    /// 🚨 **KHÔNG có constructor có tham số — ABP yêu cầu constructor RỖNG cho module.**
    ///
    /// 📌 **LỖI TÔI ĐÃ MẮC RỒI TỰ SỬA (16/10/2026):** tôi viết
    /// <c>public CatalogMediaModule(IHostEnvironment moiTruong)</c> và tin ABP tiêm được (một số tài liệu
    /// nói vậy). **Host KHÔNG khởi động nổi**, chết ngay ở bước nạp module:
    /// <code>
    /// System.MissingMethodException: Cannot dynamically create an instance of type
    /// 'eCommerce.Catalog.CatalogMediaModule'. Reason: No parameterless constructor defined.
    ///   at Volo.Abp.Modularity.ModuleLoader.CreateAndRegisterModule(...)
    /// </code>
    /// 🔑 ABP tạo module bằng <c>Activator.CreateInstance</c> ⇒ **bắt buộc** constructor rỗng.
    /// ⚠️ Và lỗi này **chỉ** lộ ra khi **chạy** Host — `dotnet build` **xanh**, **1043 unit test XANH**.
    ///
    /// ══════════════════════════════════════════════════════════════════════════════════════════════
    /// 🔒 **CÁCH ĐÚNG: lấy dịch vụ từ `sp` CỦA FACTORY — đó mới là provider THẬT.**
    ///
    /// Hàng `sp => new MediaStorageNoiBo(sp.GetRequiredService&lt;IHostEnvironment&gt;()...)` chạy **SAU** khi
    /// provider được dựng, nên `IHostEnvironment` (do host chung đăng ký) **có sẵn** — và ta dùng **cùng**
    /// provider với phần còn lại của ứng dụng.
    ///
    /// 🚨 **TUYỆT ĐỐI KHÔNG `context.Services.BuildServiceProvider()`** (tôi đã suýt viết vậy): nó tạo
    /// **service provider THỨ HAI**, tách rời provider thật ⇒ mọi singleton resolve từ nó là **bản sao riêng**
    /// (`IOptions`/cache cấu hình **hai bản**), `IDisposable` **không bao giờ** được dispose ⇒ **rò rỉ**, và
    /// nó **ẩn** lỗi đăng ký. 📌 Cách đúng là dùng `sp` mà **DI container đưa cho mình**.
    /// ══════════════════════════════════════════════════════════════════════════════════════════════
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IMediaStorage>(sp =>
        {
            var moiTruong = sp.GetRequiredService<IHostEnvironment>();
            var cauHinh = sp.GetService<IConfiguration>();

            // 🚨 **THỨ TỰ ƯU TIÊN: CẤU HÌNH TRƯỚC, `ContentRoot` SAU.**
            // 📌 **VÌ SAO PHẢI CẤU HÌNH ĐƯỢC (đây là lỗi ĐO ĐƯỢC, không phải "cho linh hoạt"):** kho tệp cục bộ
            // lấy gốc theo `ContentRootPath` — mà **mỗi tiến trình có `ContentRoot` RIÊNG**:
            // <list type="bullet">
            //   <item>`eCommerce.HttpApi.Host` ⇒ `<...>/eCommerce.HttpApi.Host/bin/.../media-noi-bo` (nơi API **ghi**);</item>
            //   <item>`eCommerce.Web` ⇒ `<...>/eCommerce.Web/bin/.../media-noi-bo` (nơi trang **đọc**).</item>
            // </list>
            // ⇒ **Tải ảnh qua API xong, storefront trả `404`** — và tệp **có thật**, chỉ là **ở thư mục khác**.
            // 🔑 Đo được ngay trong probe `cat-003`: `Host` có **4** tệp, `Web` có **0** ⇒ 5 ca phục vụ tệp **ĐỎ**
            // dù đường tải lên **ĐẠT hết**.
            // 🔒 Đặt **tuyệt đối** trong `appsettings` (hoặc biến môi trường `MediaStorage__ThuMucGoc`) ⇒ **cả hai**
            // tiến trình trỏ **cùng một** thư mục. 📌 Ở production, thay bằng kho dùng chung (S3/Azure) —
            // khi đó **không** cần cấu hình này nữa, và **không** phải sửa nghiệp vụ.
            // ══════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **MẶC ĐỊNH LÀ THƯ MỤC DÙNG CHUNG — KHÔNG phải `<ContentRoot>/media-noi-bo`.**
            //
            // **SỰ CỐ THẬT ĐÃ TRẢ GIÁ (16/10/2026, ĐO ĐƯỢC):** mặc định ban đầu là
            // `<ContentRoot>/media-noi-bo`, mà **mỗi tiến trình có `ContentRoot` RIÊNG**:
            //   • `eCommerce.HttpApi.Host` ⇒ `<...>/eCommerce.HttpApi.Host/bin/.../media-noi-bo` (**GHI**);
            //   • `eCommerce.Web`          ⇒ `<...>/eCommerce.Web/bin/.../media-noi-bo` (**ĐỌC**).
            // ⇒ API tải ảnh trả **`200`**, DB **có** dòng, nhưng `GET /CatMedia/{id}` trả **`404`**, và log ghi
            // *"dong DB co nhung kho tep KHONG co bytes"*. 📌 Lỗi đó **nói thật**: tệp **có** tồn tại, chỉ là
            // **ở thư mục của tiến trình khác**. Bằng chứng đo được: `Host` có `c20748….png`, kho mà Web đọc
            // **không** có nó.
            //
            // 🔒 **VÌ SAO MẶC ĐỊNH PHẢI LÀ DÙNG CHUNG:** cách chạy **phổ biến nhất** của dự án là **Host + Web
            // cùng máy** (đôi khi thêm `DbMigrator`). Một mặc định **hỏng ngay ở cách chạy phổ biến nhất** là
            // mặc định **sai**, dù nó "sạch" về lý thuyết.
            // 📌 Nay dùng **một thư mục DUY NHẤT** dưới thư mục tạm của hệ điều hành, có tên riêng của dự án
            // ⇒ **mọi tiến trình trên cùng máy** dùng chung, và **không** lẫn với dự án khác.
            // 🚨 **VẪN LÀ GIỚI HẠN ĐÃ BIẾT (không được hiểu thành "đã xong"):** thư mục tạm **không** dùng chung
            // giữa **các máy** ⇒ chạy nhiều máy/nhiều container **phải** đặt `MediaStorage:ThuMucGoc` vào kho
            // dùng chung — hoặc tốt hơn: thay `IMediaStorage` bằng bản S3/Azure (xem `CAT-003` §20.7).
            // ══════════════════════════════════════════════════════════════════════════════════════
            var gocCauHinh = cauHinh?["MediaStorage:ThuMucGoc"];

            var goc = !string.IsNullOrWhiteSpace(gocCauHinh)
                ? Path.GetFullPath(gocCauHinh)
                : Path.Combine(Path.GetTempPath(), "ecommerce-media-dung-chung");

            // 📌 GHI LOG GỐC KHO NGAY LÚC ĐĂNG KÝ: khi "ghi một nơi, đọc nơi khác" thì thứ cần biết ĐẦU TIÊN
            // là **mỗi tiến trình đang trỏ vào đâu**. 📌 Đã trả giá: không có dòng log này thì phải suy đoán.
            sp.GetService<ILoggerFactory>()?.CreateLogger("CatalogMediaModule")
                .LogInformation("GOC KHO MEDIA: {Goc} (tu cau hinh: {TuCauHinh}) [MEDIA-GOC-KHO]", goc, !string.IsNullOrWhiteSpace(gocCauHinh));

            return new MediaStorageNoiBo(goc);
        });
    }
}
