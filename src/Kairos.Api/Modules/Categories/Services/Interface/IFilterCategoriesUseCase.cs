using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IFilterCategoriesUseCase
{
    Task<Result<Page<CategoryModel>>> ExecuteAsync(
        CategoryFilterDto filter,
        CancellationToken cancellationToken = default);
}