using Kairos.Api.Config.Observability;
using Serilog;

namespace Kairos.Api.Config.LogConfigs;

public static class SerilogConfig
{
    public static void ConfigureSerilog(this IHostBuilder host)
    {
        host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteToObservability(
                    context.Configuration,
                    context.HostingEnvironment);
        });
    }
}