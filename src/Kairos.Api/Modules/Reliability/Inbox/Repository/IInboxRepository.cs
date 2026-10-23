using Kairos.Api.Modules.Reliability.Inbox.Model;

namespace Kairos.Api.Modules.Reliability.Inbox.Repository;

public interface IInboxRepository
{
    Task<InboxModel?> GetByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboxModel>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboxModel>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
}