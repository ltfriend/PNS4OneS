using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PNS4OneS
{
    class StartupService
    {
        public IConfiguration Configuration { get; }

        public StartupService(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void ConfigureServices(IServiceCollection _1) { }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment _1)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/auth", async context =>
                    await AuthHandler.Auth(context.Request, context.Response)
                );
                endpoints.MapPost("/sendmessage", async context =>
                    await SendMessageHandler.SendMessage(context.Request, context.Response)
                );
            });
        }
    }
}
