using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories;
using Kairos.Api.Modules.Identity;
using Kairos.Api.Modules.Reservations;
using Kairos.Api.Modules.Rooms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(
    builder.Configuration,
    builder.Environment
);

builder.Services
    .AddRoomsModule()
    .AddReservationsModule()
    .AddCategoriesModule()
    .AddAuthModule();

// Add services to the container.

builder.Services.AddControllers();
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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }