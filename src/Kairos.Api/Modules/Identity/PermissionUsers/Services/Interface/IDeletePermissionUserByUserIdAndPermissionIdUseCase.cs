using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

public interface IDeletePermissionUserByUserIdAndPermissionIdUseCase
    : IUseCase<DeletePermissionUserCommand, Result<object>>
{
}