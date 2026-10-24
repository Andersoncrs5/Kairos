using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface IRegisterUserUseCase: IUseCase<CreateUserDTO, Result<Tokens>>
{
}