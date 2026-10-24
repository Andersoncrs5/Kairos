using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class CreateUserUseCase(IUnitOfWork uow)
    : ICreateUserUseCase
{
    public async Task<Result<UserModel>> ExecuteAsync(
        CreateUserDTO dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<UserModel>.ValidationFailure(
                ["Request body is required."]);
        }

        if (string.IsNullOrWhiteSpace(dto.UserName))
        {
            return Result<UserModel>.ValidationFailure(
                ["UserName is required."]);
        }

        var userName = dto.UserName.Trim();

        if (userName.Length is < 3 or > 256)
        {
            return Result<UserModel>.ValidationFailure(
                ["UserName must be between 3 and 256 characters."]);
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return Result<UserModel>.ValidationFailure(
                ["Email is required."]);
        }

        var email = dto.Email.Trim();

        if (email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(email))
        {
            return Result<UserModel>.ValidationFailure(
                ["Email must be a valid email address."]);
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return Result<UserModel>.ValidationFailure(
                ["Password is required."]);
        }

        if (dto.Password.Length < 8)
        {
            return Result<UserModel>.ValidationFailure(
                ["Password must contain at least 8 characters."]);
        }

        var fullName = string.IsNullOrWhiteSpace(dto.FullName)
            ? null
            : dto.FullName.Trim();

        if (fullName?.Length > 150)
        {
            return Result<UserModel>.ValidationFailure(
                ["FullName cannot exceed 150 characters."]);
        }

        string? phoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
            ? null
            : dto.PhoneNumber.Trim();

        if (phoneNumber is not null &&
            (phoneNumber.Length > 30 ||
             !new PhoneAttribute().IsValid(phoneNumber)))
        {
            return Result<UserModel>.ValidationFailure(
                ["PhoneNumber is invalid."]);
        }

        bool usernameExists =
            await uow.UserRepository.ExistsByUsername(userName);

        if (usernameExists)
        {
            return Result<UserModel>.Conflict(
                "Username is already taken.");
        }

        bool emailExists = await uow.UserRepository.ExistsByEmail(email);

        if (emailExists)
        {
            return Result<UserModel>.Conflict(
                "Email is already registered.");
        }

        ct.ThrowIfCancellationRequested();

        var user = new UserModel
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };

        var identityResult = await uow.UserRepository.Insert(
            user,
            dto.Password);

        if (!identityResult.Succeeded)
        {
            var errors = identityResult.Errors.ToArray();

            if (errors.Any(error =>
                    error.Code == "DuplicateEmail"))
            {
                return Result<UserModel>.Conflict(
                    "Email is already registered.");
            }

            if (errors.Any(error =>
                    error.Code == "DuplicateUserName"))
            {
                return Result<UserModel>.Conflict(
                    "Username is already taken.");
            }

            var descriptions = errors
                .Select(error => error.Description)
                .Where(description =>
                    !string.IsNullOrWhiteSpace(description))
                .ToArray();

            if (descriptions.Length == 0)
            {
                return Result<UserModel>.Failure(
                    "User could not be created.",
                    500);
            }

            return Result<UserModel>.ValidationFailure(
                descriptions);
        }
        
        return Result<UserModel>.Created(user);
    }
}