using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IExistsPermissionByCodeUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string code,
        CancellationToken cancellationToken = default);
}