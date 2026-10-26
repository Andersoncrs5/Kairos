using Kairos.Api.Common.Middleware;
using Kairos.Api.Config;
using Kairos.Api.Config.Bootstrap;
using Kairos.Api.Config.DB;
using Kairos.Api.Config.Swagger;
using Kairos.Api.Modules.Categories;
using Kairos.Api.Modules.Identity;
using Kairos.Api.Modules.Reliability;
using Kairos.Api.Modules.Reservations;
using Kairos.Api.Modules.Rooms;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(
    builder.Configuration,
    builder.Environment);

builder.Services
    .AddRoomsModule()
    .AddReservationsModule()
    .AddCategoriesModule()
    .AddConfigModule()
    .AddReliabilityModule()
    .AddAuthModule();

builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

app.UseSwaggerConfig();

app.UseMiddleware<RequireInboxIdMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    AppDbContext dbContext = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();

    IApplicationBootstrapper bootstrapper = scope.ServiceProvider
        .GetRequiredService<IApplicationBootstrapper>();

    await bootstrapper.Execute();
}

app.Run();

public partial class Program;