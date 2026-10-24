using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;

namespace Kairos.Api.Config.DB.Transaction;

public sealed class InboxTransactionUseCase<TInput, TValue, TUseCase>(
    IUnitOfWork unitOfWork,
    TUseCase useCase)
    : IInboxTransactionUseCase<TInput, TValue, TUseCase>
    where TUseCase : IUseCase<TInput, Result<TValue>>
{
    public Task<Result<TValue>> ExecuteAsync(
        string inboxId,
        string workerId,
        TInput input,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteTransactionWithInboxAsync<TValue>(
            inboxId,
            workerId,
            ct => useCase.ExecuteAsync(input, ct),
            cancellationToken);
    }
}