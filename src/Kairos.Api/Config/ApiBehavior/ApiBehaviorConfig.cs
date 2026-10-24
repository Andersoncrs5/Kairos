using Kairos.Api.Common.Res;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Config.ApiBehavior;

public static class ApiBehaviorConfig
{
    public static IServiceCollection AddApiBehaviorConfig(
        this IServiceCollection services)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    Dictionary<string, string[]> errors =
                        context.ModelState
                            .Where(entry => entry.Value?.Errors.Count > 0)
                            .ToDictionary(
                                entry => entry.Key,
                                entry => entry.Value!.Errors
                                    .Select(error => error.ErrorMessage)
                                    .ToArray()
                            );

                    ValidationErrorResponse response = new(
                        errors,
                        context.HttpContext.TraceIdentifier,
                        DateTimeOffset.UtcNow
                    );

                    return new BadRequestObjectResult(response);
                };
            });

        return services;
    }
}