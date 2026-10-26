using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public class UpdateUserUseCase(
    IUnitOfWork uow
) : IUpdateUserUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        UpdateUserCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<bool>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid update request.")
                    .ToArray(),
                400);
        }

        UpdateUserDTO dto = command.Dto;

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults = [],
                validateAllProperties: true))
        {
            return Result<bool>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid user data.")
                    .ToArray(),
                400);
        }

        if (dto.UserName is null &&
            dto.Email is null &&
            dto.FullName is null &&
            dto.PhoneNumber is null)
        {
            return Result<bool>.Failure(
                ["At least one field must be provided."],
                400);
        }

        UserModel? user = await uow.UserRepository
            .GetByIdAsync(command.UserId);

        if (user is null)
            return Result<bool>.NotFound("User not found.");

        if (dto.FullName is not null)
            user.FullName = dto.FullName.Trim();

        if (dto.UserName is not null)
            user.UserName = dto.UserName.Trim();

        if (dto.Email is not null)
        {
            string email = dto.Email.Trim();

            if (!string.Equals(
                    user.Email,
                    email,
                    StringComparison.OrdinalIgnoreCase))
            {
                user.Email = email;

                user.EmailConfirmed = false;
            }
        }

        if (dto.PhoneNumber is not null)
        {
            string phoneNumber = dto.PhoneNumber.Trim();

            if (!string.Equals(user.PhoneNumber, phoneNumber, StringComparison.Ordinal))
            {
                user.PhoneNumber = phoneNumber;

                user.PhoneNumberConfirmed = false;
            }
        }

        IdentityResult updateResult = await uow.UserRepository.Update(user);

        if (!updateResult.Succeeded)
        {
            string[] errors = updateResult.Errors
                .Select(error => error.Description)
                .ToArray();

            bool hasDuplicate = updateResult.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail");

            return Result<bool>.Failure(
                errors,
                hasDuplicate ? 409 : 400);
        }

        return Result<bool>.Success(true);
    }
}