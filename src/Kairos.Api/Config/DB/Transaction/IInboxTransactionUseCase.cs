using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Config.DB.Transaction;

public interface IInboxTransactionUseCase<TInput, TValue, TUseCase>
    where TUseCase : IUseCase<TInput, Result<TValue>>
{
    Task<Result<TValue>> ExecuteAsync(
        string inboxId,
        string workerId,
        TInput input,
        CancellationToken cancellationToken = default);
}