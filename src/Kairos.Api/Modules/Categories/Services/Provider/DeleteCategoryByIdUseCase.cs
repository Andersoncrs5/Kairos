using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public class DeleteCategoryByIdUseCase(IUnitOfWork uow): IDeleteCategoryByIdUseCase
{
    public async Task<Result<object>> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<object>.ValidationFailure( ["Id is required."]);

        if (!Guid.TryParse(id, out var guid))
            return Result<object>.ValidationFailure( ["Id must be a valid UUID."]);

        var count = await uow.CategoryRepository.DeleteByIdAndCountAsync(id, cancellationToken);
        
        if (count == 0)
            return Result<object>.ValidationFailure( ["Category doesn't exist."]);
        
        return Result<object>.Success(true);
    }
}