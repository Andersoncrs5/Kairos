using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;
using Kairos.Api.Modules.Reliability.Inbox.Model;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

public interface ICreateInboxUseCase
{
    Task<Result<InboxModel>> ExecuteAsync(
        CreateInboxDto dto,
        CancellationToken cancellationToken = default);
}