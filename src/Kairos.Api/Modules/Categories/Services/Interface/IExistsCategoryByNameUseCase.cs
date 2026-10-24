using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IExistsCategoryByNameUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string name,
        CancellationToken cancellationToken = default);
}