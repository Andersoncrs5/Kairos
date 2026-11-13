using Kairos.Api.Config.Resilience.Contracts;

namespace Kairos.Api.Config.Resilience;

public static class ResilienceOptionsConfig
{
    public static IServiceCollection AddResilienceOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<ResilienceOptions>()
            .BindConfiguration(ResilienceOptions.SectionName)
            .Validate(
                options => options.Database.IsValid,
                "Resilience:Database requires MaxRetryAttempts between 1 and 10, " +
                "DelayMilliseconds greater than zero and " +
                "MaxDelayMilliseconds greater than or equal to DelayMilliseconds.")
            .Validate(
                options => options.Default.IsValid,
                "Resilience:Default requires MaxRetryAttempts between 1 and 10, " +
                "DelayMilliseconds greater than zero and " +
                "MaxDelayMilliseconds greater than or equal to DelayMilliseconds.")
            .Validate(
                options => options.Storage.IsValid,
                "Resilience:Storage requires MaxRetryAttempts between 1 and 10, " +
                "DelayMilliseconds greater than zero and " +
                "MaxDelayMilliseconds greater than or equal to DelayMilliseconds.")
            .ValidateOnStart();

        return services;
    }
}