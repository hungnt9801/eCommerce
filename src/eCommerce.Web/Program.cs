using eCommerce.Web;

namespace eCommerce.Web;

/// <summary>Điểm khởi động của **tiến trình UI quản trị** (xem `eCommerceWebModule`).</summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseAutofac();

        // 🚨 ĐẶT TÊN ỨNG DỤNG TƯỜNG MINH.
        // Mặc định của ABP là `"MyApplication"` ⇒ tiêu đề trang, thương hiệu trên layout và một số
        // thông báo sẽ hiển thị sai tên. Đây là khác biệt **nhìn thấy được** nhưng **build vẫn xanh**,
        // nên rất dễ bị bỏ qua khi chỉ kiểm bằng `dotnet build` + test.
        await builder.AddApplicationAsync<eCommerceWebModule>(options =>
        {
            options.ApplicationName = "eCommerce";
        });

        var app = builder.Build();

        await app.InitializeApplicationAsync();
        await app.RunAsync();

        return 0;
    }
}
