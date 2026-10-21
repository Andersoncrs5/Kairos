using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IUpdateCategoryUseCase
{
    Task<Result<CategoryModel>> ExecuteAsync(
        string id,
        UpdateCategoryDto dto,
        CancellationToken cancellationToken = default);
}