using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

public interface IExistsInboxByMessageIdUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string messageId,
        CancellationToken cancellationToken = default);
}