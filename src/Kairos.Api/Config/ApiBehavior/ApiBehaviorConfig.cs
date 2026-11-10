using System.Text.Json;
using System.Text.Json.Serialization;
using Kairos.Api.Common.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using MvcApiBehaviorOptions =
    Microsoft.AspNetCore.Mvc.ApiBehaviorOptions;

namespace Kairos.Api.Config.ApiBehavior;

public static class ApiBehaviorConfig
{
    public static IServiceCollection AddApiBehaviorConfig(
        this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(o => ApiJson.Configure(o.JsonSerializerOptions))
            .AddJsonOptions(options =>
            {
                JsonSerializerOptions json = options.JsonSerializerOptions;

                json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                json.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;

                // Enums como texto ("Confirmed"), sem naming policy, igual ao
                // que já é gravado no banco (HasConversion<string>).
                // allowIntegerValues = true (padrão): a ENTRADA ainda aceita número.
                json.Converters.Add(new JsonStringEnumConverter());

                json.Converters.Add(new UtcDateTimeOffsetConverter());

                // Opcional: omitir nulls (muda o contrato, avalie com o front)
                // json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });


        services.AddSingleton<
            IConfigureOptions<MvcApiBehaviorOptions>,
            ConfigureMvcApiBehaviorOptions>();

        return services;
    }
}