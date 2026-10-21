using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Interface;

public interface IDeletePermissionByIdUseCase
{
    Task<Result<object>> ExecuteAsync(string id,
        CancellationToken cancellationToken = default);
}