using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;

namespace Kairos.Api.Config.DB.Transaction;

public sealed class TransactionUseCase<TInput, TValue, TUseCase>(IUnitOfWork unitOfWork, TUseCase useCase)
    : ITransactionUseCase<TInput, TValue, TUseCase> where TUseCase : IUseCase<TInput, Result<TValue>>
{
    public Task<Result<TValue>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteTransactionAsync<TValue>(
            ct => useCase.ExecuteAsync(input, ct),
            cancellationToken);
    }
}