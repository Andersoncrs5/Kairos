namespace Kairos.Api.Common.Base.Services;

public interface IUseCase<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}