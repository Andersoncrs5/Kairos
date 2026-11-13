using System.Net;
using Amazon.Runtime;
using Kairos.Api.Config.Observability;
using Kairos.Api.Config.Resilience.Contracts;
using Microsoft.Extensions.Options;
using Npgsql; // NOVO
using Polly;
using Polly.Registry;
using Polly.Retry;

namespace Kairos.Api.Config.Resilience;

public static class ResilienceConfig
{
    public static IServiceCollection AddResilienceConfig(
        this IServiceCollection services)
    {
        services.AddSingleton<ResiliencePipelineRegistry<string>>(provider =>
        {
            ResilienceOptions options =
                provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

            ILogger logger = provider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Kairos.Api.Resilience");

            ResiliencePipelineRegistry<string> registry = new();

            registry.TryAddBuilder(
                ResiliencePipelines.Default,
                (builder, _) => ConfigureRetry(
                    builder,
                    options.Enabled,
                    options.Default,
                    ResiliencePipelines.Default,
                    IsTransientDefault,
                    logger));

            registry.TryAddBuilder(
                ResiliencePipelines.Storage,
                (builder, _) => ConfigureRetry(
                    builder,
                    options.Enabled,
                    options.Storage,
                    ResiliencePipelines.Storage,
                    IsTransientStorage,
                    logger));

            // NOVO
            registry.TryAddBuilder(
                ResiliencePipelines.Database,
                (builder, _) => ConfigureRetry(
                    builder,
                    options.Enabled,
                    options.Database,
                    ResiliencePipelines.Database,
                    IsTransientDatabase,
                    logger));

            return registry;
        });

        // O que as classes injetam: ResiliencePipelineProvider<string>
        services.AddSingleton<ResiliencePipelineProvider<string>>(
            provider => provider.GetRequiredService<ResiliencePipelineRegistry<string>>());

        return services;
    }

    // ----------------------------------------------------------
    // Retry
    // ----------------------------------------------------------

    private static void ConfigureRetry(
        ResiliencePipelineBuilder builder,
        bool enabled,
        RetryRule rule,
        string pipelineName,
        Func<Exception, bool> isTransient,
        ILogger logger)
    {
        // Sem nenhuma strategy, o pipeline vira um pass-through
        if (!enabled)
        {
            return;
        }

        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = rule.MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(rule.DelayMilliseconds),
            MaxDelay = TimeSpan.FromMilliseconds(rule.MaxDelayMilliseconds),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = rule.UseJitter,

            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is { } exception && isTransient(exception)),

            OnRetry = args =>
            {
                Telemetry.ResilienceRetries.Add(
                    1,
                    new KeyValuePair<string, object?>("pipeline", pipelineName));

                logger.LogWarning(
                    args.Outcome.Exception,
                    "Retry {Attempt}/{MaxAttempts} in pipeline {Pipeline} after {DelayMs} ms.",
                    args.AttemptNumber + 1,
                    rule.MaxRetryAttempts,
                    pipelineName,
                    args.RetryDelay.TotalMilliseconds);

                return default;
            }
        });
    }

    // ----------------------------------------------------------
    // Quais falhas valem um retry
    // ----------------------------------------------------------

    private static bool IsTransientDefault(Exception exception)
        => exception is HttpRequestException or TimeoutException or IOException;

    private static bool IsTransientStorage(Exception exception) => exception switch
    {
        // 5xx, throttling e timeout; 400/403/404 nunca melhoram com retry
        AmazonServiceException service => service.StatusCode is
            HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout,

        // Falha de rede sem resposta do servidor
        AmazonClientException { InnerException: HttpRequestException or IOException or TimeoutException } => true,

        HttpRequestException or TimeoutException or IOException => true,

        _ => false
    };

    // NOVO
    private static bool IsTransientDatabase(Exception exception)
    {
        // O EF embrulha a falha do Npgsql (DbUpdateException, InvalidOperationException...),
        // então percorremos a cadeia de InnerException
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true } or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}