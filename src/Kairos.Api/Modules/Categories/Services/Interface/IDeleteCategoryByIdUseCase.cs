using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IDeleteCategoryByIdUseCase
{
    Task<Result<object>> ExecuteAsync(string id, CancellationToken cancellationToken);
}