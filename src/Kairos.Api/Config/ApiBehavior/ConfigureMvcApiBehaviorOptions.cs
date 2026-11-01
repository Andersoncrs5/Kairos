using Kairos.Api.Common.Res;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ApiBehaviorOptions = Kairos.Api.Config.Contracts.ApiBehaviorOptions;
using MvcApiBehaviorOptions =
    Microsoft.AspNetCore.Mvc.ApiBehaviorOptions;

namespace Kairos.Api.Config.ApiBehavior;

public sealed class ConfigureMvcApiBehaviorOptions(
    IOptions<ApiBehaviorOptions> apiOptions)
    : IConfigureOptions<MvcApiBehaviorOptions>
{
    public void Configure(MvcApiBehaviorOptions options)
    {
        ApiBehaviorOptions settings = apiOptions.Value;

        options.SuppressMapClientErrors =
            settings.SuppressMapClientErrors;

        options.SuppressModelStateInvalidFilter =
            settings.SuppressModelStateInvalidFilter;

        options.InvalidModelStateResponseFactory = context =>
        {
            Dictionary<string, string[]> errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => error.ErrorMessage)
                        .ToArray());

            ValidationErrorResponse response = new(
                errors,
                context.HttpContext.TraceIdentifier,
                DateTimeOffset.UtcNow);

            return new BadRequestObjectResult(response);
        };
    }
}