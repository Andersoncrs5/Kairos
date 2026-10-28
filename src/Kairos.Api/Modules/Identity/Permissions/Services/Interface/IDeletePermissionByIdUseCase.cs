using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IDeletePermissionByIdUseCase: IUseCase<string, Result<object>>
{
}