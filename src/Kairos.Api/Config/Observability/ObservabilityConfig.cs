using Kairos.Api.Config.Contracts;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Kairos.Api.Config.Observability;

public static class ObservabilityConfig
{
    private const string TestingEnvironment = "Testing";
    private const string InboxHeader = "X-Inbox-Id";

    // ----------------------------------------------------------
    // Traces + Metrics (OpenTelemetry SDK)
    // ----------------------------------------------------------

    public static IServiceCollection AddObservabilityConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ObservabilityOptions settings = GetSettings(configuration);

        if (!IsActive(settings, environment))
        {
            return services;
        }

        OpenTelemetryBuilder otel = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddAttributes(BuildResourceAttributes(settings, environment)));

        if (settings.EnableTracing)
        {
            otel.WithTracing(tracing => ConfigureTracing(tracing, settings));
        }

        if (settings.EnableMetrics)
        {
            otel.WithMetrics(metrics => ConfigureMetrics(metrics, settings));
        }

        return services;
    }

    // ----------------------------------------------------------
    // Logs (Serilog -> OTLP)
    // ----------------------------------------------------------

    public static LoggerConfiguration WriteToObservability(
        this LoggerConfiguration logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ObservabilityOptions settings = GetSettings(configuration);

        if (!IsActive(settings, environment) || !settings.EnableLogs)
        {
            return logger;
        }

        return logger.WriteTo.OpenTelemetry(sink =>
        {
            sink.Endpoint = BuildEndpoint(settings, "logs");

            sink.Protocol = settings.Protocol == OtlpExportProtocol.HttpProtobuf
                ? OtlpProtocol.HttpProtobuf
                : OtlpProtocol.Grpc;

            sink.ResourceAttributes =
                BuildResourceAttributes(settings, environment);
        });
    }

    // ----------------------------------------------------------
    // Internals
    // ----------------------------------------------------------

    private static void ConfigureTracing(
        TracerProviderBuilder tracing,
        ObservabilityOptions settings)
    {
        tracing
            .SetSampler(new ParentBasedSampler(
                new TraceIdRatioBasedSampler(settings.TraceSamplingRatio)))
            .AddSource(Telemetry.Name);

        if (settings.EnableAspNetCoreInstrumentation)
        {
            tracing.AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;

                options.Filter = httpContext =>
                    !IsNoise(httpContext.Request.Path);

                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    string inboxId = request.Headers[InboxHeader].ToString();

                    if (!string.IsNullOrWhiteSpace(inboxId))
                    {
                        activity.SetTag("kairos.inbox.id", inboxId);
                    }
                };

                // No fim da requisição o JWT já foi validado
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    string? userId =
                        response.HttpContext.User.FindFirst("sub")?.Value;

                    if (userId is not null)
                    {
                        activity.SetTag("enduser.id", userId);
                    }
                };
            });
        }

        if (settings.EnableHttpClientInstrumentation)
        {
            tracing.AddHttpClientInstrumentation();
        }

        if (settings.EnablePostgreSqlInstrumentation)
        {
            // Npgsql gera um span por comando SQL (com o SQL).
            // Não habilitei o EF Core instrumentation junto para não duplicar spans.
            tracing.AddNpgsql();
        }

        if (settings.EnableRedisInstrumentation)
        {
            // Usa o IConnectionMultiplexer registrado no DI
            tracing.AddRedisInstrumentation();
        }

        tracing.AddOtlpExporter(exporter =>
            ConfigureExporter(exporter, settings, "traces"));
    }

    private static void ConfigureMetrics(
        MeterProviderBuilder metrics,
        ObservabilityOptions settings)
    {
        metrics.AddMeter(Telemetry.Name);

        if (settings.EnableAspNetCoreInstrumentation)
        {
            metrics.AddAspNetCoreInstrumentation();
        }

        if (settings.EnableHttpClientInstrumentation)
        {
            metrics.AddHttpClientInstrumentation();
        }

        if (settings.EnableRuntimeInstrumentation)
        {
            metrics.AddRuntimeInstrumentation();
        }

        if (settings.EnablePostgreSqlInstrumentation)
        {
            metrics.AddNpgsqlInstrumentation();
        }

        metrics.AddOtlpExporter((exporter, reader) =>
        {
            ConfigureExporter(exporter, settings, "metrics");

            reader.PeriodicExportingMetricReaderOptions
                .ExportIntervalMilliseconds =
                    settings.MetricExportIntervalMilliseconds;
        });
    }

    private static void ConfigureExporter(
        OtlpExporterOptions exporter,
        ObservabilityOptions settings,
        string signal)
    {
        exporter.Endpoint = new Uri(BuildEndpoint(settings, signal));
        exporter.Protocol = settings.Protocol;
        exporter.TimeoutMilliseconds = settings.ExportTimeoutMilliseconds;
    }

    // Em HttpProtobuf, quando o endpoint é definido por código, o path do
    // sinal (/v1/traces etc.) precisa estar na URL. Em gRPC não.
    private static string BuildEndpoint(
        ObservabilityOptions settings,
        string signal)
    {
        return settings.Protocol == OtlpExportProtocol.HttpProtobuf
            ? $"{settings.OtlpEndpoint.TrimEnd('/')}/v1/{signal}"
            : settings.OtlpEndpoint;
    }

    // Mesmos atributos para traces, métricas e logs, para tudo
    // aparecer com o mesmo service_name no Grafana.
    private static Dictionary<string, object> BuildResourceAttributes(
        ObservabilityOptions settings,
        IHostEnvironment environment)
    {
        return new Dictionary<string, object>
        {
            ["service.name"] = settings.ServiceName,
            ["service.namespace"] = settings.ServiceNamespace,
            ["service.version"] = settings.ServiceVersion,
            ["service.instance.id"] = Environment.MachineName,
            ["deployment.environment.name"] =
                environment.EnvironmentName.ToLowerInvariant()
        };
    }

    private static bool IsNoise(PathString path)
    {
        return path.StartsWithSegments("/swagger")
               || path.StartsWithSegments("/health")
               || path.StartsWithSegments("/favicon.ico");
    }

    private static bool IsActive(
        ObservabilityOptions settings,
        IHostEnvironment environment)
    {
        // Nos testes de integração não exporta nada
        return settings.Enabled
               && !environment.IsEnvironment(TestingEnvironment);
    }

    private static ObservabilityOptions GetSettings(
        IConfiguration configuration)
    {
        return configuration
                   .GetSection(ObservabilityOptions.SectionName)
                   .Get<ObservabilityOptions>()
               ?? new ObservabilityOptions();
    }
}