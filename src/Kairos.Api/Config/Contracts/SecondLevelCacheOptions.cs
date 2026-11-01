using EFCoreSecondLevelCacheInterceptor;

namespace Kairos.Api.Config.Contracts;

public sealed class SecondLevelCacheOptions
{
    public const string SectionName = "SecondLevelCache";

    public string CacheKeyPrefix { get; set; } = "Kairos_EF_";

    public int CacheExpirationMinutes { get; set; } = 6;

    public int CacheProviderRetryMinutes { get; set; } = 2;

    public EFLockMode LockMode { get; set; } = EFLockMode.Keyed;

    public int LockTimeoutSeconds { get; set; } = 15;
}