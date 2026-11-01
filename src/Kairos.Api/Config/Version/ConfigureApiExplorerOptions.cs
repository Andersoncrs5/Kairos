using Asp.Versioning.ApiExplorer;
using Kairos.Api.Config.Contracts;
using Microsoft.Extensions.Options;

using ApiVersioningSettings =
    Kairos.Api.Config.Contracts.ApiVersioningOptions;

namespace Kairos.Api.Config.Version;

public sealed class ConfigureApiExplorerOptions(
    IOptions<ApiVersioningSettings> settings)
    : IConfigureOptions<ApiExplorerOptions>
{
    public void Configure(ApiExplorerOptions options)
    {
        ApiVersioningSettings configuration = settings.Value;

        options.GroupNameFormat = configuration.GroupNameFormat;

        options.SubstituteApiVersionInUrl =
            configuration.SubstituteApiVersionInUrl;
    }
}