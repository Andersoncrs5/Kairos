using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Interface;
using Kairos.Api.Modules.Categories.Services.Provider;

namespace Kairos.Api.Modules.Categories;

public static class CategoriesModule
{
    public static IServiceCollection AddCategoriesModule(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        
        services.AddScoped<IFindCategoryByIdUseCase, FindCategoryByIdUseCase>();
        services.AddScoped<IDeleteCategoryByIdUseCase, DeleteCategoryByIdUseCase>();
        services.AddScoped<IUpdateCategoryUseCase, UpdateCategoryUseCase>();
        services.AddScoped<ICreateCategoryUseCase, CreateCategoryUseCase>();
        services.AddScoped<IExistsCategoryByNameUseCase, ExistsCategoryByNameUseCase>();
        
        return services;
    }
}