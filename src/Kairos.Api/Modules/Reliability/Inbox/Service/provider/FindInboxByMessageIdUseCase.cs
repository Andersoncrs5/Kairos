using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public sealed class FindInboxByMessageIdUseCase(
    IUnitOfWork uow)
    : IFindInboxByMessageIdUseCase
{
    private const int MaxMessageIdLength = 256;

    public async Task<Result<InboxModel>> ExecuteAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Message ID is required."]);
        }

        var normalizedMessageId = messageId.Trim();

        if (normalizedMessageId.Length > MaxMessageIdLength)
        {
            return Result<InboxModel>.ValidationFailure(
                [$"Message ID cannot exceed {MaxMessageIdLength} characters."]);
        }

        var inbox = await uow.InboxRepository.GetByMessageIdAsync(
            normalizedMessageId,
            cancellationToken);

        if (inbox is null)
        {
            return Result<InboxModel>.NotFound(
                "Inbox not found.");
        }

        return Result<InboxModel>.Success(inbox);
    }
}