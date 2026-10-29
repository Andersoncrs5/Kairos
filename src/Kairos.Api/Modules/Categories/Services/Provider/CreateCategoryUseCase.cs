using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Mappings;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public class CreateCategoryUseCase(IUnitOfWork uow): ICreateCategoryUseCase
{
    public async Task<Result<CategoryModel>> ExecuteAsync(
        CreateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        CategoryModel category = CategoryMapper.ToModel(dto, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        
        bool check = await uow.CategoryRepository.ExistsByNameAsync(dto.Name, cancellationToken);
        if (check)
            return Result<CategoryModel>.Conflict($"Category with the name {dto.Name} already exists.");

        await uow.CategoryRepository.AddAsync(
            category,
            cancellationToken);

        return Result<CategoryModel>.Created(category);
    }
    
}