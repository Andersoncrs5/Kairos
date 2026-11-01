using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IExistsCategoryByCodeUseCase
    : IUseCase<string, Result<bool>>
{
}