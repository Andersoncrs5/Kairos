using System.Reflection;
using Microsoft.OpenApi;

namespace Kairos.Api.Config.Swagger;

public static class SwaggerConfig
{
    public static IServiceCollection AddSwaggerConfig(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Kairos API",
                Version = "v1",
                Description = """
                              API for room reservations, room management,
                              categories, user identity and permissions.

                              Supports versioned HTTP endpoints and
                              standardized API responses.
                              """,
                Contact = new OpenApiContact
                {
                    Name = "Kairos API",
                }
            });

            // XML documentation from controllers and DTOs.
            var xmlFile =
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";

            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static WebApplication UseSwaggerConfig(
        this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint(
                    "/swagger/v1/swagger.json",
                    "Kairos API v1");

                options.DocumentTitle = "Kairos API | Swagger";

                options.DisplayRequestDuration();

                options.EnableTryItOutByDefault();
            });
        }

        return app;
    }
}