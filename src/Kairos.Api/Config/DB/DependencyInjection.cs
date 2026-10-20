using Kairos.Api.Config.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Kairos.Api.Config.DB;

public static class DependencyInjection
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "A connection string 'DefaultConnection' não foi configurada.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(7),
                    errorCodesToAdd: null);

                npgsql.CommandTimeout(30);
            })
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableDetailedErrors();
                ;

            options.EnableDetailedErrors(environment.IsDevelopment());

            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                options.EnableSensitiveDataLogging();
            }
        });

        return services;
    }
}