using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IFindUserByIdUseCase
{
    Task<Result<UserModel>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}