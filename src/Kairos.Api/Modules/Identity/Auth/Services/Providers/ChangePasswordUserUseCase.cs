using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class ChangePasswordUserUseCase(
    IUnitOfWork uow
) : IChangePasswordUserUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        ChangePasswordUserCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        List<ValidationResult> validationResults = [];

        bool isValid = Validator.TryValidateObject(
            command,
            new ValidationContext(command),
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            string[] errors = validationResults
                .Select(result =>
                    result.ErrorMessage ?? "Invalid change password request.")
                .ToArray();

            return Result<bool>.Failure(errors, 400);
        }

        UserModel? user = await uow.UserRepository
            .GetByIdAsync(command.UserId);

        if (user is null)
            return Result<bool>.NotFound("User not found.");

        IdentityResult changeResult = await uow.UserRepository
            .ChangePassword(
                user,
                command.CurrentPassword,
                command.NewPassword);

        if (!changeResult.Succeeded)
        {
            string[] errors = changeResult.Errors
                .Select(error => error.Description)
                .ToArray();

            return Result<bool>.Failure(errors, 400);
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        IdentityResult updateResult = await uow.UserRepository
            .Update(user);

        if (!updateResult.Succeeded)
        {
            string[] errors = updateResult.Errors
                .Select(error => error.Description)
                .ToArray();

            return Result<bool>.Failure(errors, 500);
        }

        return Result<bool>.Success(true);
    }
}