using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Model;

namespace Kairos.Api.Modules.Reliability.Inbox.Repository;

public interface IInboxRepository: IBaseRepository<InboxModel>
{
    Task<bool> TryMarkAsProcessingAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default);
    
    Task<bool> TryMarkAsProcessedAsync(
        string id,
        string lockedBy,
        CancellationToken cancellationToken = default);
    
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