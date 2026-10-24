using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class ExistsUserByIdUseCase(IUnitOfWork uow)
    : IExistsUserByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Id is required."]);
        }

        var normalizedId = id.Trim();

        bool user = await uow.UserRepository.ExistsByIdAsync(normalizedId);

        return Result<bool>.Success(user );
    }
}