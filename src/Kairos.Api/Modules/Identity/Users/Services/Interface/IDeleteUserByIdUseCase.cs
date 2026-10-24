using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IDeleteUserByIdUseCase
{
    Task<Result<object>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}