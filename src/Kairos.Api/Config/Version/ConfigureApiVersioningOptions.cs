using Asp.Versioning;
using Kairos.Api.Config.Contracts;
using Microsoft.Extensions.Options;

using ApiVersioningSettings =
    Kairos.Api.Config.Contracts.ApiVersioningOptions;

using FrameworkApiVersioningOptions =
    Asp.Versioning.ApiVersioningOptions;

namespace Kairos.Api.Config.Version;

public sealed class ConfigureApiVersioningOptions(
    IOptions<ApiVersioningSettings> settings)
    : IConfigureOptions<FrameworkApiVersioningOptions>
{
    public void Configure(FrameworkApiVersioningOptions options)
    {
        ApiVersioningSettings configuration = settings.Value;

        options.DefaultApiVersion = new ApiVersion(
            configuration.DefaultMajorVersion,
            configuration.DefaultMinorVersion);

        options.AssumeDefaultVersionWhenUnspecified =
            configuration.AssumeDefaultVersionWhenUnspecified;

        options.ReportApiVersions =
            configuration.ReportApiVersions;
    }
}