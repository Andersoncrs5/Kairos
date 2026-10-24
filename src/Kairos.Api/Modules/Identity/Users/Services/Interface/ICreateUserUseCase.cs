using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface ICreateUserUseCase
{
    Task<Result<UserModel>> ExecuteAsync(CreateUserDTO dto, CancellationToken ct = default);
}