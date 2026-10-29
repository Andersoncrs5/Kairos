using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.DTOs.Commands;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IUpdateCategoryUseCase
    : IUseCase<UpdateCategoryCommand, Result<CategoryModel>>
{
}