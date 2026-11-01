using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class ObservabilityOptionsConfig
{
    public static IServiceCollection AddObservabilityOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<ObservabilityOptions>()
            .BindConfiguration(ObservabilityOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ServiceName),
                "Observability:ServiceName is required.")
            .Validate(
                options => Uri.TryCreate(
                    options.OtlpEndpoint,
                    UriKind.Absolute,
                    out _),
                "Observability:OtlpEndpoint must be an absolute URI.")
            .Validate(
                options => options.ExportTimeoutMilliseconds > 0,
                "Observability:ExportTimeoutMilliseconds must be greater than zero.")
            .Validate(
                options => options.MetricExportIntervalMilliseconds > 0,
                "Observability:MetricExportIntervalMilliseconds must be greater than zero.")
            .Validate(
                options => options.TraceSamplingRatio is >= 0 and <= 1,
                "Observability:TraceSamplingRatio must be between 0 and 1.")
            .ValidateOnStart();

        return services;
    }
}