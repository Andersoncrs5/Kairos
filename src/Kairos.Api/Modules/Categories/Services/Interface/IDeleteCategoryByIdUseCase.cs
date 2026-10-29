using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.DTOs.Request;

namespace Kairos.Api.Modules.Categories.Services.Interface;

public interface IDeleteCategoryByIdUseCase: IUseCase<string, Result<object>> { }