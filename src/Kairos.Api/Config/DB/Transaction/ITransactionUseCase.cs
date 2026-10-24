using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Config.DB.Transaction;

public interface ITransactionUseCase<TInput, TValue, TUseCase>
    where TUseCase : IUseCase<TInput, Result<TValue>>
{
    Task<Result<TValue>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}