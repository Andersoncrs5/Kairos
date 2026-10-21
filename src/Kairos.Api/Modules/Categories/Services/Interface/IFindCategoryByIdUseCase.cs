using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IFindCategoryByIdUseCase
{
    Task<Result<CategoryModel>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken
    );
}