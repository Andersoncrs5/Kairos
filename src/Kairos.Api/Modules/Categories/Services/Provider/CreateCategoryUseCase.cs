
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Mappings;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class CreateCategoryUseCase(IUnitOfWork uow)
    : ICreateCategoryUseCase
{
    public async Task<Result<CategoryModel>> ExecuteAsync(
        CreateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // ----------------------------------------------------------
        // 1. Check duplicate Code
        // ----------------------------------------------------------

        string code = dto.Code.Trim();

        bool codeExists = await uow.CategoryRepository
            .ExistsByCodeAsync(code, cancellationToken);

        if (codeExists)
        {
            return Result<CategoryModel>.Conflict(
                $"Category with the code '{code}' already exists.");
        }

        // ----------------------------------------------------------
        // 2. Check duplicate Name
        // ----------------------------------------------------------

        bool nameExists = await uow.CategoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken);

        if (nameExists)
        {
            return Result<CategoryModel>.Conflict(
                $"Category with the name '{dto.Name}' already exists.");
        }

        // ----------------------------------------------------------
        // 3. Map DTO to Model
        // ----------------------------------------------------------

        CategoryModel category = CategoryMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow);

        // Ensure the code is persisted without surrounding whitespace.
        category.Code = code;

        // ----------------------------------------------------------
        // 4. Persist category
        // ----------------------------------------------------------

        await uow.CategoryRepository.AddAsync(
            category,
            cancellationToken);

        // ----------------------------------------------------------
        // 5. Return result
        // ----------------------------------------------------------

        return Result<CategoryModel>.Created(category);
    }
}
