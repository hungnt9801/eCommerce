using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace eCommerce.Web;

/// <summary>
/// **Thương hiệu của ứng dụng** — quyết định TÊN hiển thị (tiêu đề trang, logo chữ trên layout).
///
/// 🚨 VÌ SAO PHẢI CÓ: mặc định của ABP (`DefaultBrandingProvider`) trả **`"MyApplication"`**.
/// Khác biệt này **nhìn thấy được ngay** trên mọi trang, nhưng:
///   • `dotnet build` **xanh**, test **xanh**, `health-status` **200**,
///   • trang vẫn **render đúng** — chỉ sai tên.
/// ⇒ Đúng loại lỗi "build xanh nhưng sai" mà dự án đã gặp nhiều lần, nên phải sửa **và** kiểm chứng
/// bằng HTML thật (`<title>`), không bằng suy luận.
///
/// 📌 `ReplaceServices = true`: ABP đã đăng ký `DefaultBrandingProvider` cho `IBrandingProvider`;
/// cờ này để bản của ta **thay thế** bản mặc định (không phải thêm một bản trùng — trùng thì tuỳ thứ tự
/// đăng ký mà bản nào thắng, đúng kiểu "hai nguồn sự thật").
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IBrandingProvider))]
public class eCommerceBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "eCommerce";
}
