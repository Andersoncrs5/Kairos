using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class FindUserByIdUseCase(IUnitOfWork uow)
    : IFindUserByIdUseCase
{
    public async Task<Result<UserModel>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<UserModel>.ValidationFailure(
                ["Id is required."]);
        }

        var normalizedId = id.Trim();

        var user = await uow.UserRepository.GetByIdAsync(normalizedId);

        if (user is null)
        {
            return Result<UserModel>.NotFound("User not found.");
        }

        return Result<UserModel>.Success(user);
    }
}