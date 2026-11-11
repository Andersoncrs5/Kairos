using System.Threading.RateLimiting;
using Kairos.Api.Common.Middleware;
using Kairos.Api.Config;
using Kairos.Api.Config.Bootstrap;
using Kairos.Api.Config.Cors.Contracts;
using Kairos.Api.Config.DB;
using Kairos.Api.Config.Health;
using Kairos.Api.Config.LogConfigs;
using Kairos.Api.Config.Observability;
using Kairos.Api.Config.RateLimiting;
using Kairos.Api.Config.Swagger;
using Kairos.Api.Modules.Categories;
using Kairos.Api.Modules.Identity;
using Kairos.Api.Modules.Reliability;
using Kairos.Api.Modules.Reservations;
using Kairos.Api.Modules.Rooms;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ==========================================
// Logging & Observability
// ==========================================

builder.Host.ConfigureSerilog();

builder.Services.AddObservabilityConfig(builder.Configuration, builder.Environment);

// ==========================================
// Infrastructure
// ==========================================

builder.Services.AddDatabase(builder.Configuration, builder.Environment);

builder.Services.AddProblemDetails();

// ==========================================
// Modules
// ==========================================

builder.Services
    .AddRoomsModule()
    .AddReservationsModule()
    .AddCategoriesModule()
    .AddConfigModule()
    .AddReliabilityModule()
    .AddAuthModule();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ==========================================
// Application
// ==========================================

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
    app.UseForwardedHeaders();
    app.UseRateLimitingConfig();
}

// ==========================================
// Swagger
// ==========================================

app.UseSwaggerConfig();

// ==========================================
// Routing & CORS
// ==========================================

app.UseRouting();

// CORS must execute before middleware that requires
// authentication or custom request headers.
app.UseCors(CorsOptions.PolicyName);

// ==========================================
// HTTP Pipeline
// ==========================================

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseMiddleware<RequireInboxIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseAuthentication();

app.UseAuthorization();


app.UseExceptionHandler();

app.UseOutputCache();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// ==========================================
// Database Migration & Bootstrap
// ==========================================

await using (var scope = app.Services.CreateAsyncScope())
{
    AppDbContext dbContext = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();

    IApplicationBootstrapper bootstrapper = scope.ServiceProvider
        .GetRequiredService<IApplicationBootstrapper>();

    await bootstrapper.Execute();
}

// ==========================================
// Endpoints
// ==========================================

app.MapControllers();

app.MapHealthChecksConfig();


// ==========================================
// Run
// ==========================================

app.Run();

public partial class Program;