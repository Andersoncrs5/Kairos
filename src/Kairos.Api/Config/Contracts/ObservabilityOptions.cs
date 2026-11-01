using OpenTelemetry.Exporter;

namespace Kairos.Api.Config.Contracts;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool Enabled { get; set; } = true;

    public string ServiceName { get; set; } = "kairos-api";

    public string ServiceNamespace { get; set; } = "Kairos";

    public string ServiceVersion { get; set; } = "1.0.0";

    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    public OtlpExportProtocol Protocol { get; set; } = OtlpExportProtocol.Grpc;

    public int ExportTimeoutMilliseconds { get; set; } = 10000;

    public int MetricExportIntervalMilliseconds { get; set; } = 15000;

    public bool EnableTracing { get; set; } = true;

    public bool EnableMetrics { get; set; } = true;

    public bool EnableLogs { get; set; } = true;

    public double TraceSamplingRatio { get; set; } = 1.0;

    public bool EnableAspNetCoreInstrumentation { get; set; } = true;

    public bool EnableHttpClientInstrumentation { get; set; } = true;

    public bool EnableRuntimeInstrumentation { get; set; } = true;

    public bool EnablePostgreSqlInstrumentation { get; set; } = true;

    public bool EnableRedisInstrumentation { get; set; } = true;
}