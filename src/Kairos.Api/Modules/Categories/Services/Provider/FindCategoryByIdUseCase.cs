using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class FindCategoryByIdUseCase(IUnitOfWork uow)
    : IFindCategoryByIdUseCase
{
    public async Task<Result<CategoryModel>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<CategoryModel>.ValidationFailure( ["Id is required."]);

        if (!Guid.TryParse(id, out var guid))
            return Result<CategoryModel>.ValidationFailure( ["Id must be a valid UUID."]);

        var category = await uow.CategoryRepository.GetByIdAsync(
            guid.ToString(),
            cancellationToken);

        if (category is null)
            return Result<CategoryModel>.NotFound("Category not found.");

        return Result<CategoryModel>.Success(category);
    }
}