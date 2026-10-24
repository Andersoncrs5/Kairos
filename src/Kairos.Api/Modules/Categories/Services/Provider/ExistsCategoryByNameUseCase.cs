using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class ExistsCategoryByNameUseCase(IUnitOfWork uow)
    : IExistsCategoryByNameUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<bool>.ValidationFailure(
                ["Category name is required."]);
        }

        bool exists = await uow.CategoryRepository.ExistsByNameAsync(
            name,
            cancellationToken);

        return Result<bool>.Success(exists);
    }
}