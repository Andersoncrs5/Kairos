using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

public interface IExistsInboxByIdUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default);
}