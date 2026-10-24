using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.Models;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface ILoginUserUseCase
{
    Task<Result<Tokens>> ExecuteAsync(LoginUserDTO dto, CancellationToken cancellationToken = default);
}