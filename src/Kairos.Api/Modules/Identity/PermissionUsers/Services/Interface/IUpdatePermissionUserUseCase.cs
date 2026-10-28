using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IUpdatePermissionUserUseCase: IUseCase<UpdatePermissionUserCommand, Result<PermissionUserModel>>
{
}