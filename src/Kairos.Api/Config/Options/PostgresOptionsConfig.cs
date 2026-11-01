using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class PostgresOptionsConfig
{
    public static IServiceCollection AddPostgresOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<PostgresOptions>()
            .BindConfiguration(PostgresOptions.SectionName)
            .Validate(
                options => options.MaxRetryCount >= 0,
                "Postgres:MaxRetryCount cannot be negative.")
            .Validate(
                options => options.MaxRetryDelaySeconds >= 0,
                "Postgres:MaxRetryDelaySeconds cannot be negative.")
            .Validate(
                options => options.CommandTimeoutSeconds > 0,
                "Postgres:CommandTimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        return services;
    }
}