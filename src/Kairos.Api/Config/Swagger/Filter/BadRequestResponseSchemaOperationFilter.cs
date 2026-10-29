using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kairos.Api.Config.Swagger.Filter;

public sealed class BadRequestResponseSchemaOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var responseTypes = context.MethodInfo
            .GetCustomAttributes<ProducesResponseTypeAttribute>(
                inherit: true)
            .Where(attribute =>
                attribute.StatusCode == StatusCodes.Status400BadRequest)
            .Select(attribute => attribute.Type)
            .OfType<Type>()
            .Distinct()
            .ToArray();

        if (responseTypes.Length < 2)
            return;

        if (!operation.Responses.TryGetValue("400", out var response))
            return;

        if (!response.Content.TryGetValue(
                "application/json", out var content))
            return;

        content.Schema = new OpenApiSchema
        {
            AnyOf = responseTypes
                .Select(type => context.SchemaGenerator.GenerateSchema(
                    type,
                    context.SchemaRepository))
                .ToList()
        };
    }
}