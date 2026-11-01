using System.Linq;
using System.Text.Json;
using Kairos.Api.Config.Health.Checks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kairos.Api.Config.Health;

public static class HealthChecksConfig
{
    public static IServiceCollection AddHealthChecksConfig(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"])
            .AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    public static WebApplication MapHealthChecksConfig(
        this WebApplication app)
    {
        // Estado geral
        app.MapHealthChecks(
            "/health",
            CreateHealthCheckOptions())
            .AllowAnonymous();

        // Readiness: verifica dependências
        app.MapHealthChecks(
            "/health/ready",
            CreateHealthCheckOptions(
                healthCheck => healthCheck.Tags.Contains("ready")))
            .AllowAnonymous();

        // Liveness: verifica somente se o processo responde
        app.MapHealthChecks(
            "/health/live",
            CreateHealthCheckOptions(
                _ => false))
            .AllowAnonymous();

        return app;
    }

    private static HealthCheckOptions CreateHealthCheckOptions(
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        return new HealthCheckOptions
        {
            Predicate = predicate ?? (_ => true),

            ResponseWriter = WriteResponseAsync
        };
    }

    private static Task WriteResponseAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType =
            "application/json; charset=utf-8";

        object response = new
        {
            Status = report.Status.ToString(),
            DurationMs = report.TotalDuration.TotalMilliseconds,

            Checks = report.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                DurationMs = entry.Value.Duration.TotalMilliseconds
            })
        };

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }
}