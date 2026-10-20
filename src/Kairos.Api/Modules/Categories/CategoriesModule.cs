using Kairos.Api.Modules.Categories.Repositories;

namespace Kairos.Api.Modules.Categories;

public static class CategoriesModule
{
    public static IServiceCollection AddCategoriesModule(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        return services;
    }
}