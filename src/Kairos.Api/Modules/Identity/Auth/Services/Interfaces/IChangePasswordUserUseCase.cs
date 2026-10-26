using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface IChangePasswordUserUseCase
    : IUseCase<ChangePasswordUserCommand, Result<bool>>
{
}