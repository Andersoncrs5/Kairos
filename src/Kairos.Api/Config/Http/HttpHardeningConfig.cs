using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace Kairos.Api.Config.Http;

public static class HttpHardeningConfig
{
    public static IServiceCollection AddHttpHardeningConfig(
        this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes
                .Concat(["application/problem+json"]);
        });

        services.Configure<BrotliCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Fastest);

        services.Configure<GzipCompressionProviderOptions>(
            o => o.Level = CompressionLevel.Fastest);

        // HSTS
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = true;
        });

        return services;
    }
}