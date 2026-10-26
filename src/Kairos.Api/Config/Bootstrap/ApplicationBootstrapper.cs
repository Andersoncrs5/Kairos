using Kairos.Api.Common.Base.Seed;
using Kairos.Api.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Kairos.Api.Config.Bootstrap;

public sealed class ApplicationBootstrapper(
    IEnumerable<ISeedCase> seedCases,
    ILogger<ApplicationBootstrapper> logger)
    : IApplicationBootstrapper
{
    public async Task Execute(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset bootstrapStartedAt = DateTimeOffset.UtcNow;
        ISeedCase[] seeds = seedCases.ToArray();

        string currentSeedName = "<bootstrap>";
        string currentStage = "Initialization";

        logger.LogInformation(
            "Application bootstrap started. StartedAt: {StartedAt}; SeedCount: {SeedCount}",
            bootstrapStartedAt,
            seeds.Length);

        try
        {
            for (int index = 0; index < seeds.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ISeedCase seed = seeds[index];

                currentSeedName = seed.GetType().Name;
                currentStage = "SeedExecution";

                DateTimeOffset seedStartedAt = DateTimeOffset.UtcNow;

                logger.LogInformation(
                    "Executing application seed {SeedName}. " +
                    "Position: {Position}/{TotalSeeds}",
                    currentSeedName,
                    index + 1,
                    seeds.Length);

                await seed.Execute(cancellationToken);

                logger.LogInformation(
                    "Application seed completed. Seed: {SeedName}; DurationMs: {DurationMs}",
                    currentSeedName,
                    (DateTimeOffset.UtcNow - seedStartedAt).TotalMilliseconds);
            }

            DateTimeOffset completedAt = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Application bootstrap completed successfully. " +
                "StartedAt: {StartedAt}; CompletedAt: {CompletedAt}; " +
                "SeedCount: {SeedCount}; DurationMs: {DurationMs}",
                bootstrapStartedAt,
                completedAt,
                seeds.Length,
                (completedAt - bootstrapStartedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Application bootstrap was cancelled while executing seed {SeedName}.",
                currentSeedName);

            throw;
        }
        catch (Exception exception)
        {
            DateTimeOffset failedAt = DateTimeOffset.UtcNow;

            ApplicationBootstrapperException bootstrapException = new(
                currentSeedName,
                currentStage,
                bootstrapStartedAt,
                failedAt,
                exception);

            logger.LogCritical(
                exception,
                "Application bootstrap failed. Seed: {SeedName}; " +
                "Stage: {Stage}; DurationMs: {DurationMs}",
                currentSeedName,
                currentStage,
                bootstrapException.Elapsed.TotalMilliseconds);

            throw bootstrapException;
        }
    }
}