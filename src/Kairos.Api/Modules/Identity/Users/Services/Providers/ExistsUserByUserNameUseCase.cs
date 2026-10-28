using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class ExistsUserByUserNameUseCase(IUnitOfWork uow)
    : IExistsUserByUserNameUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result<bool>.ValidationFailure(
                ["Username is required."]);
        }

        string normalizedUserName = userName.Trim();

        bool exists = await uow.UserRepository
            .ExistsByUsername(normalizedUserName);

        return Result<bool>.Success(exists);
    }
}