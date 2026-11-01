using Kairos.Api.Config.DB;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kairos.Api.Config.Health.Checks;

public sealed class PostgreSqlHealthCheck(
    IServiceScopeFactory scopeFactory
) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();

            AppDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            bool canConnect =
                await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL is available.")
                : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL health check failed.");
        }
    }
}