using Kairos.Api.Common.Middleware;
using Kairos.Api.Common.Res;
using Kairos.Api.Config;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories;
using Kairos.Api.Modules.Identity;
using Kairos.Api.Modules.Reliability;
using Kairos.Api.Modules.Reservations;
using Kairos.Api.Modules.Rooms;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(
    builder.Configuration,
    builder.Environment
);

builder.Services
    .AddRoomsModule()
    .AddReservationsModule()
    .AddCategoriesModule()
    .AddConfigModule()
    .AddReliabilityModule()
    .AddAuthModule();

// Add services to the container.

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            Dictionary<string,string[]> errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => error.ErrorMessage)
                        .ToArray()
                );

            ValidationErrorResponse response = new ValidationErrorResponse(
                errors,
                context.HttpContext.TraceIdentifier,
                DateTimeOffset.UtcNow
            );

            return new BadRequestObjectResult(response);
        };
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ===================================================================================
// INFRA & EXCEPTION
// ===================================================================================
// builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequireInboxIdMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }