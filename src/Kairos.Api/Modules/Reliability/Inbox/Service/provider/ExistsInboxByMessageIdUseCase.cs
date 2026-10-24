using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public sealed class ExistsInboxByMessageIdUseCase(
    IUnitOfWork uow)
    : IExistsInboxByMessageIdUseCase
{
    private const int MaxMessageIdLength = 256;

    public async Task<Result<bool>> ExecuteAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Result<bool>.ValidationFailure(
                ["Message ID is required."]);
        }

        var normalizedMessageId = messageId.Trim();

        if (normalizedMessageId.Length > MaxMessageIdLength)
        {
            return Result<bool>.ValidationFailure(
                [$"Message ID cannot exceed {MaxMessageIdLength} characters."]);
        }

        var exists = await uow.InboxRepository.ExistsByMessageIdAsync(
            normalizedMessageId,
            cancellationToken);

        return Result<bool>.Success(exists);
    }
}