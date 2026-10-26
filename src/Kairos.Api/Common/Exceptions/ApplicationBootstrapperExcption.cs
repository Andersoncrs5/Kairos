namespace Kairos.Api.Common.Exceptions;

public sealed class ApplicationBootstrapperException : Exception
{
    public string SeedName { get; }

    public string Stage { get; }

    public DateTimeOffset BootstrapStartedAt { get; }

    public DateTimeOffset FailedAt { get; }

    public TimeSpan Elapsed => FailedAt - BootstrapStartedAt;

    public string OriginalExceptionType { get; }

    public ApplicationBootstrapperException(
        string seedName,
        string stage,
        DateTimeOffset bootstrapStartedAt,
        DateTimeOffset failedAt,
        Exception innerException)
        : base(
            BuildMessage(
                seedName,
                stage,
                bootstrapStartedAt,
                failedAt,
                innerException),
            innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(innerException);

        SeedName = seedName;
        Stage = stage;
        BootstrapStartedAt = bootstrapStartedAt;
        FailedAt = failedAt;
        OriginalExceptionType =
            innerException.GetType().FullName
            ?? innerException.GetType().Name;
    }

    private static string BuildMessage(
        string seedName,
        string stage,
        DateTimeOffset startedAt,
        DateTimeOffset failedAt,
        Exception innerException)
    {
        TimeSpan elapsed = failedAt - startedAt;

        return
            $"Application bootstrap failed. " +
            $"Seed: '{seedName}'. " +
            $"Stage: '{stage}'. " +
            $"StartedAt: '{startedAt:O}'. " +
            $"FailedAt: '{failedAt:O}'. " +
            $"Elapsed: '{elapsed}'. " +
            $"Cause: '{innerException.GetType().FullName}: " +
            $"{innerException.Message}'.";
    }
}