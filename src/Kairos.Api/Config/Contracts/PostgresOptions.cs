namespace Kairos.Api.Config.Contracts;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public int MaxRetryCount { get; set; } = 6;

    public int MaxRetryDelaySeconds { get; set; } = 8;

    public int CommandTimeoutSeconds { get; set; } = 40;

    public bool EnableLocking { get; set; } = true;
}