namespace Kairos.Api.Config.Resilience.Contracts;

public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public bool Enabled { get; set; } = true;

    public RetryRule Default { get; set; } = new();

    public RetryRule Storage { get; set; } = new()
    {
        MaxRetryAttempts = 2,
        DelayMilliseconds = 300,
        MaxDelayMilliseconds = 3000
    };
    
    public RetryRule Database { get; set; } = new()
    {
        MaxRetryAttempts = 5,
        DelayMilliseconds = 200,
        MaxDelayMilliseconds = 7000
    };
}

public sealed class RetryRule
{
    public int MaxRetryAttempts { get; set; } = 3;

    public int DelayMilliseconds { get; set; } = 200;

    public int MaxDelayMilliseconds { get; set; } = 5000;

    public bool UseJitter { get; set; } = true;

    public bool IsValid =>
        MaxRetryAttempts is >= 1 and <= 10
        && DelayMilliseconds > 0
        && MaxDelayMilliseconds >= DelayMilliseconds;
}