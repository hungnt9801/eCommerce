using Volo.Abp.Modularity;

namespace eCommerce.Kernel;

/// <summary>
/// Module của **shared kernel**.
///
/// 🚨 BÀI HỌC ĐÃ TRẢ GIÁ (lần thứ BA trong dự án — sau C8 và C16):
///   Chỉ **tham chiếu** project là **KHÔNG ĐỦ**. Thiếu class `AbpModule` thì assembly **không nằm trong
///   ĐỒ THỊ MODULE** ⇒ ABP **không quét** assembly đó ⇒ các type `ISingletonDependency`/`ITransientDependency`
///   bên trong **KHÔNG được đăng ký vào DI**.
///   Triệu chứng thực tế: `UseMiddleware<HttpRequestDurationMiddleware>` cần `eCommerceMetrics`
///   (nằm trong Kernel) ⇒ **DI không resolve được** ⇒ **ứng dụng KHÔNG KHỞI ĐỘNG ĐƯỢC**.
///   (Test vẫn xanh vì test tự `new eCommerceMetrics(...)` — tức **build xanh + test xanh KHÔNG chứng minh
///   chạy được**.)
/// </summary>
public class eCommerceKernelModule : AbpModule
{
}