using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public sealed class MarkInboxAsProcessedUseCase(
    IUnitOfWork uow)
    : IMarkInboxAsProcessedUseCase
{
    private const int MaxLockedByLength = 256;

    public async Task<Result<bool>> ExecuteAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Id is required."]);
        }

        if (!Guid.TryParse(id, out var guid))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        if (string.IsNullOrWhiteSpace(lockedBy))
        {
            return Result<bool>.ValidationFailure(
                ["LockedBy is required."]);
        }

        var normalizedLockedBy = lockedBy.Trim();

        if (normalizedLockedBy.Length > MaxLockedByLength)
        {
            return Result<bool>.ValidationFailure(
                [$"LockedBy cannot exceed {MaxLockedByLength} characters."]);
        }

        var normalizedId = guid.ToString();

        var processed = await uow.InboxRepository.TryMarkAsProcessedAsync(
            normalizedId,
            normalizedLockedBy,
            cancellationToken);

        if (processed)
        {
            return Result<bool>.Success(true);
        }

        var inbox = await uow.InboxRepository.GetByIdAsync(
            normalizedId,
            cancellationToken);

        if (inbox is null)
        {
            return Result<bool>.NotFound("Inbox not found.");
        }

        return Result<bool>.Conflict(
            "Inbox is not processing or is not owned by this worker.");
    }
}