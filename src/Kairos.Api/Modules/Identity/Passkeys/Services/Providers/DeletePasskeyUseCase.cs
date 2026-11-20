using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class DeletePasskeyUseCase(IUnitOfWork uow)
    : IDeletePasskeyUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        DeletePasskeyCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!UseCaseValidation.TryParseUuid(command.UserId, out string userId) ||
            !UseCaseValidation.TryParseUuid(command.PasskeyId, out string passkeyId))
        {
            return Result<bool>.ValidationFailure(
                ["User ID and passkey ID must be valid UUIDs."]);
        }

        UserPasskeyModel? passkey =
            await uow.UserPasskeyRepository.GetByIdAndUserIdAsync(
                passkeyId,
                userId,
                ct);

        if (passkey is null)
            return Result<bool>.NotFound("Passkey not found.");

        // Never remove the last way to sign in.
        int count = await uow.UserPasskeyRepository.CountByUserIdAsync(userId, ct);

        if (count <= 1)
        {
            UserModel? user = await uow.UserRepository.GetByIdAsync(userId);

            if (user is null)
                return Result<bool>.NotFound("User not found.");

            bool hasPassword = await uow.UserRepository.HasPassword(user);
            bool hasExternalLogin = await uow.UserRepository.HasExternalLogin(user);

            if (!hasPassword && !hasExternalLogin)
            {
                return Result<bool>.Conflict(
                    "This is your only way to sign in. Set a password or " +
                    "link another sign-in method before removing it.");
            }
        }

        uow.UserPasskeyRepository.Delete(passkey);

        return Result<bool>.Success(true);
    }
}