using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface IRegisterUserUseCase
{
    Task<Result<Tokens>> ExecuteAsync(CreateUserDTO dto, CancellationToken ct = default);
}