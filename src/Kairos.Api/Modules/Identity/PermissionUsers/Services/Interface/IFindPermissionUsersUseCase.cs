using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IFindPermissionUsersUseCase: IUseCase<FilterPermissionUserDTO, Page<PermissionUserModel>>
{
}