using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Passkeys.Repositories;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

namespace Kairos.Api.Modules.Identity.Passkeys;

public static class PasskeysModule
{
    public static IServiceCollection AddPasskeysModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection section =
            configuration.GetSection(PasskeyOptions.SectionName);

        PasskeyOptions options =
            section.Get<PasskeyOptions>() ?? new PasskeyOptions();

        if (string.IsNullOrWhiteSpace(options.ServerDomain))
            throw new InvalidOperationException("Passkeys:ServerDomain is required.");

        if (options.Origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Passkeys:Origins must contain at least one origin.");
        }

        if (options.FlowLifetimeSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Passkeys:FlowLifetimeSeconds must be greater than zero.");
        }

        services.Configure<PasskeyOptions>(section);

        services.AddFido2(fido2 =>
        {
            fido2.ServerDomain = options.ServerDomain;
            fido2.ServerName = options.ServerName;
            fido2.Origins = new HashSet<string>(options.Origins);
        });

        services.AddScoped<IUserPasskeyRepository, UserPasskeyRepository>();
        services.AddSingleton<IPasskeyFlowStore, RedisPasskeyFlowStore>();

        services.AddScoped<ICreatePasskeyRegistrationOptionsUseCase, CreatePasskeyRegistrationOptionsUseCase>();
        services.AddScoped<ICompletePasskeyRegistrationUseCase, CompletePasskeyRegistrationUseCase>();
        services.AddScoped<ICreatePasskeyLoginOptionsUseCase, CreatePasskeyLoginOptionsUseCase>();
        services.AddScoped<ICompletePasskeyLoginUseCase, CompletePasskeyLoginUseCase>();
        services.AddScoped<IListPasskeysUseCase, ListPasskeysUseCase>();
        services.AddScoped<IDeletePasskeyUseCase, DeletePasskeyUseCase>();

        return services;
    }
}