using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

public interface IMarkInboxAsProcessedUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default);
}