using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Kairos.Api.Config.Health.Checks;

public sealed class RedisHealthCheck(
    IConnectionMultiplexer redis
) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            TimeSpan latency = await redis
                .GetDatabase()
                .PingAsync()
                .WaitAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"Redis is available. Latency: {latency.TotalMilliseconds:F2} ms.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy(
                "Redis health check failed.");
        }
    }
}