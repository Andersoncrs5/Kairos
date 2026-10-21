using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Mappings;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class UpdateCategoryUseCase(IUnitOfWork uow)
    : IUpdateCategoryUseCase
{
    public async Task<Result<CategoryModel>> ExecuteAsync(
        string id,
        UpdateCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<CategoryModel>.ValidationFailure(
                ["Id is required."]);
        }

        if (!Guid.TryParse(id, out var categoryId))
        {
            return Result<CategoryModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var category = await uow.CategoryRepository.GetByIdAsync(
            categoryId.ToString(),
            cancellationToken);

        if (category is null)
        {
            return Result<CategoryModel>.NotFound(
                "Category not found.");
        }

        if (dto.Name != null && category.Name != dto.Name)
        {
            bool check = await uow.CategoryRepository.ExistsByNameAsync(dto.Name, cancellationToken);
            if (check)
                return Result<CategoryModel>.Conflict($"Category with the name {dto.Name} already exists.");
        }
        
        CategoryMapper.ApplyUpdate(dto, category);

        return Result<CategoryModel>.Success(category);
    }
}