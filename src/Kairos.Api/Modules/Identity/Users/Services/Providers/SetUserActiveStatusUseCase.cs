using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class SetUserActiveStatusUseCase(
    IUserRepository userRepository)
    : ISetUserActiveStatusUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        SetUserActiveStatusCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<bool>.ValidationFailure(
                ["User status command is required."]);
        }

        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<bool>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid user status command.")
                    .Distinct()
                    .ToArray());
        }

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return Result<bool>.ValidationFailure(
                ["User ID is required."]);
        }

        // ---------------------------------------------------------------
        // 2. Find user
        // ---------------------------------------------------------------

        string userId = command.UserId.Trim();

        UserModel? user = await userRepository.FindByIdWithForUpdateAsync(userId, ct);

        if (user is null)
            return Result<bool>.NotFound("User not found.");

        // ---------------------------------------------------------------
        // 3. Check current status
        // ---------------------------------------------------------------

        if (user.IsActive == command.IsActive)
        {
            return Result<bool>.Success(true);
        }

        // ---------------------------------------------------------------
        // 4. Update account status
        // ---------------------------------------------------------------

        user.IsActive = command.IsActive;

        if (!command.IsActive)
        {
            // Revoke refresh token when account is deactivated.
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
        }

        // ---------------------------------------------------------------
        // 5. Persist changes
        // ---------------------------------------------------------------

        var updateResult = await userRepository.Update(user);

        if (!updateResult.Succeeded)
        {
            return Result<bool>.Failure(
                updateResult.Errors
                    .Select(error => error.Description)
                    .Distinct()
                    .ToArray());
        }

        return Result<bool>.Success(true);
    }
}