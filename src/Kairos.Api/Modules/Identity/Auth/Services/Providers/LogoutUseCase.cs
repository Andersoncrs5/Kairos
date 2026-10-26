using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public sealed class LogoutUseCase(
    IUnitOfWork uow)
    : ILogoutUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        UserModel? user = await uow.UserRepository.GetByIdAsync(userId);

        if (user is null) return Result<bool>.NotFound("User not found");

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        IdentityResult result = await uow.UserRepository.Update(user);

        if (!result.Succeeded)
        {
            string error = result.Errors.FirstOrDefault()?.Description ?? "Could not complete logout.";

            return Result<bool>.Failure(error);
        }

        return Result<bool>.Success(true);
    }
}