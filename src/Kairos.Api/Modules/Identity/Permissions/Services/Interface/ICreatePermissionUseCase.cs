using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface ICreatePermissionUseCase: IUseCase<CreatePermissionDto, Result<PermissionsModel>> { }