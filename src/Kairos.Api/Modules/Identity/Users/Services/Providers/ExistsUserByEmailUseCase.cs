using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class ExistsUserByEmailUseCase(IUnitOfWork uow)
    : IExistsUserByEmailUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<bool>.ValidationFailure(
                ["Email is required."]);
        }

        string normalizedEmail = email.Trim();

        bool exists = await uow.UserRepository
            .ExistsByEmail(normalizedEmail);

        return Result<bool>.Success(exists);
    }
}