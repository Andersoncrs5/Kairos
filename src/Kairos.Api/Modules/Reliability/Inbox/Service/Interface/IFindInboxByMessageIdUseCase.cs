using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reliability.Inbox.Model;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;


public interface IFindInboxByMessageIdUseCase
{
    Task<Result<InboxModel>> ExecuteAsync(
        string messageId,
        CancellationToken cancellationToken = default);
}