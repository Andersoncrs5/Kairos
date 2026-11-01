using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class ExistsCategoryByCodeUseCase(IUnitOfWork uow)
    : IExistsCategoryByCodeUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<bool>.Failure("Category code is required.");

        bool exists = await uow.CategoryRepository.ExistsByCodeAsync(code.Trim(), ct);

        return Result<bool>.Success(exists);
    }
}