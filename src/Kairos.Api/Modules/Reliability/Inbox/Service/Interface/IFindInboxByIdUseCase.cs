using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reliability.Inbox.Model;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

public interface IFindInboxByIdUseCase
{
    Task<Result<InboxModel>> ExecuteAsync(string id, CancellationToken ct = default);
}