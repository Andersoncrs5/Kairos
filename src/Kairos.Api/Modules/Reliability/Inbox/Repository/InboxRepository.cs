using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Reliability.Inbox.Repository;

public sealed class InboxRepository(AppDbContext context) : BaseRepositoryImpl<InboxModel>(context), IInboxRepository
{
    public async Task<bool> TryMarkAsProcessingAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await context.Set<InboxModel>()
            .Where(inbox =>
                inbox.Id == id &&
                inbox.Status == InboxStatusEnum.Pending &&
                (inbox.NextAttemptAt == null ||
                 inbox.NextAttemptAt <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        inbox => inbox.Status,
                        InboxStatusEnum.Processing)
                    .SetProperty(
                        inbox => inbox.LockedBy,
                        lockedBy)
                    .SetProperty(
                        inbox => inbox.UpdatedAt,
                        (DateTimeOffset?)now),
                cancellationToken);

        return affectedRows > 0;
    }
    
    public async Task<bool> TryMarkAsProcessedAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await context.Set<InboxModel>()
            .Where(inbox =>
                inbox.Id == id &&
                inbox.Status == InboxStatusEnum.Processing &&
                inbox.LockedBy == lockedBy)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        inbox => inbox.Status,
                        InboxStatusEnum.Processed)
                    .SetProperty(
                        inbox => inbox.ProcessedAt,
                        (DateTimeOffset?)now)
                    .SetProperty(
                        inbox => inbox.NextAttemptAt,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        inbox => inbox.LastError,
                        (string?)null)
                    .SetProperty(
                        inbox => inbox.LockedBy,
                        (string?)null)
                    .SetProperty(
                        inbox => inbox.UpdatedAt,
                        (DateTimeOffset?)now),
                cancellationToken);

        return affectedRows > 0;
    }
    
    public Task<InboxModel?> GetByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        return context.Set<InboxModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                inbox => inbox.MessageId == messageId,
                cancellationToken);
    }

    public Task<bool> ExistsByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        return context.Set<InboxModel>()
            .AnyAsync(
                inbox => inbox.MessageId == messageId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<InboxModel>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var now = DateTimeOffset.UtcNow;

        return await context.Set<InboxModel>()
            .AsNoTracking()
            .Where(inbox =>
                inbox.Status == InboxStatusEnum.Pending &&
                (inbox.NextAttemptAt == null ||
                 inbox.NextAttemptAt <= now))
            .OrderBy(inbox => inbox.ReceivedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InboxModel>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<InboxModel>()
            .AsNoTracking()
            .Where(inbox => inbox.CorrelationId == correlationId)
            .OrderBy(inbox => inbox.ReceivedAt)
            .ToListAsync(cancellationToken);
    }
}