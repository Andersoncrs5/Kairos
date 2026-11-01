namespace Kairos.Api.Config.Contracts;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool AbortOnConnectFail { get; set; } = false;

    public int ConnectRetry { get; set; } = 5;

    public int ConnectTimeoutMilliseconds { get; set; } = 6000;

    public int SyncTimeoutMilliseconds { get; set; } = 6000;

    public int AsyncTimeoutMilliseconds { get; set; } = 6000;

    public string ClientName { get; set; } = "Kairos.Api";
}