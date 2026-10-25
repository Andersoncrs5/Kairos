using System.Text;
using Kairos.Api.Config.ApiBehavior;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.Swagger;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Config.Version;

namespace Kairos.Api.Config;

public static class ConfigModule
{
    public static IServiceCollection AddConfigModule(
        this IServiceCollection services)
    {
        services.AddApiVersioningConfig();
        services.AddSwaggerConfig();
        services.AddApiBehaviorConfig();

        services.AddOptions<JwtOptions>()
            .BindConfiguration("Jwt")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.SecretKey) &&
                    Encoding.UTF8.GetByteCount(options.SecretKey) >= 32,
                "Jwt:SecretKey must contain at least 32 bytes.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ValidIssuer),
                "Jwt:ValidIssuer is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ValidAudience),
                "Jwt:ValidAudience is required.")
            .Validate(
                options =>
                    double.IsFinite(options.TokenValidityInMinutes) &&
                    options.TokenValidityInMinutes > 0,
                "Jwt:TokenValidityInMinutes must be greater than zero.")
            .Validate(
                options => options.RefreshTokenValidityInMinutes > 0,
                "Jwt:RefreshTokenValidityInMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddScoped(
            typeof(IInboxTransactionUseCase<,,>),
            typeof(InboxTransactionUseCase<,,>));

        services.AddScoped(
            typeof(ITransactionUseCase<,,>),
            typeof(TransactionUseCase<,,>));

        services.AddScoped<
            IUnitOfWork,
            Kairos.Api.Config.UnitOfWork.UnitOfWork>();

        return services;
    }
}