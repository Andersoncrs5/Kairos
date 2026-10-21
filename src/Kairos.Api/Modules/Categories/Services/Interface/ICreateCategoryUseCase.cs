using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface ICreateCategoryUseCase
{
    Task<Result<CategoryModel>> ExecuteAsync(CreateCategoryDto dto, CancellationToken cancellationToken);
}