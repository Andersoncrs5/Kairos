using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IExistsUserByIdUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}