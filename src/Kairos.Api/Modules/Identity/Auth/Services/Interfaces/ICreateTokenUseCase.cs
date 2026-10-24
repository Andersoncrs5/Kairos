using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface ICreateTokenUseCase
{
    Task<Result<TokenResponse>> ExecuteAsync(UserModel user);
}