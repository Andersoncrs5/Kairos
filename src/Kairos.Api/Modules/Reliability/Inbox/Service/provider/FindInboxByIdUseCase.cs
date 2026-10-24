using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public class FindInboxByIdUseCase(IUnitOfWork uow): IFindInboxByIdUseCase
{
    public async Task<Result<InboxModel>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<InboxModel>.ValidationFailure( ["Id is required."]);

        if (!Guid.TryParse(id, out var guid))
            return Result<InboxModel>.ValidationFailure( ["Id must be a valid UUID."]);

        var inbox = await uow.InboxRepository.GetByIdAsync(guid.ToString(), ct);

        if (inbox is null)
            return Result<InboxModel>.NotFound("Inbox not found.");

        return Result<InboxModel>.Success(inbox);
    }
}