using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface ICreatePermissionUserUseCase: IUseCase<CreatePermissionUserDTO, Result<PermissionUserModel>> { }