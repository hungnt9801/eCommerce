using eCommerce;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace eCommerce;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseAutofac();

        await builder.AddApplicationAsync<eCommerceHttpApiHostModule>();

        var app = builder.Build();

        await app.InitializeApplicationAsync();
        await app.RunAsync();

        return 0;
    }
}
