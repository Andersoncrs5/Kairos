using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public sealed class ExistsInboxByIdUseCase(
    IUnitOfWork uow)
    : IExistsInboxByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Inbox ID is required."]);
        }

        var normalizedId = id.Trim();

        var exists = await uow.InboxRepository.ExistsAsync(
            normalizedId,
            cancellationToken);

        return Result<bool>.Success(exists);
    }
}