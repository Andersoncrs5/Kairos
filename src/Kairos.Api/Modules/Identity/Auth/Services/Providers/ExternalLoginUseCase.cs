using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.Mapper;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public sealed class ExternalLoginUseCase(
    IUnitOfWork uow,
    IOptions<JwtOptions> jwtOptions,
    ICreateTokenUseCase createTokenUseCase,
    ICreateRefreshTokenUseCase createRefreshTokenUseCase,
    ICreateUserPreferenceUseCase createUserPreferenceUseCase,
    IFindUserPreferenceUseCase findUserPreferenceUseCase)
    : IExternalLoginUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(
        ExternalLoginCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1) Known external account: just sign in.
        UserModel? user = await uow.UserRepository.GetByExternalLogin(
            command.Provider,
            command.ProviderKey);

        // 2) First time with this provider account.
        if (user is null)
        {
            Result<UserModel> resolved =
                await ResolveNewLoginAsync(command, ct);

            if (resolved.IsFailure || resolved.Value is null)
            {
                return Result<Tokens>.Failure(
                    resolved.Errors,
                    resolved.StatusCode);
            }

            user = resolved.Value;
        }

        if (!user.IsActive)
            return Result<Tokens>.Unauthorized("User is not active");

        Result<TokenResponse> tokenResult =
            await createTokenUseCase.ExecuteAsync(user);

        if (tokenResult.IsFailure || tokenResult.Value is null)
        {
            return Result<Tokens>.Failure(
                tokenResult.Errors,
                tokenResult.StatusCode);
        }

        TokenResponse token = tokenResult.Value;
        string refreshToken = createRefreshTokenUseCase.Execute();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddMinutes(
            jwtOptions.Value.RefreshTokenValidityInMinutes);

        await uow.UserRepository.Update(user);

        Result<UserPreferenceModel> preferenceResult =
            await findUserPreferenceUseCase.ExecuteAsync(user.Id, ct);

        if (preferenceResult.IsFailure || preferenceResult.Value is null)
        {
            return Result<Tokens>.Failure(
                preferenceResult.Errors,
                preferenceResult.StatusCode);
        }

        Tokens tokens = new()
        {
            ExpiredToken = token.Expired,
            Token = token.Token,
            RefreshToken = refreshToken,
            User = UserMapper.ToDto(user),
            Permissions = user.Permissions,
            Preference = UserPreferenceMapper.ToDto(preferenceResult.Value)
        };

        return Result<Tokens>.Success(tokens);
    }

    private async Task<Result<UserModel>> ResolveNewLoginAsync(
        ExternalLoginCommand command,
        CancellationToken ct)
    {
        // Never trust an email the provider did not verify.
        if (string.IsNullOrWhiteSpace(command.Email) ||
            !command.EmailVerified)
        {
            return Result<UserModel>.ValidationFailure(
                ["The provider did not return a verified email address."]);
        }

        string email = command.Email.Trim();

        UserModel? existing = await uow.UserRepository.GetByEmail(email);

        if (existing is not null)
        {
            // Pre-hijacking defense: Kairos does not verify emails on
            // password sign-up, so anyone could have registered this email
            // beforehand and already know its password. Only link when the
            // local email was confirmed.
            if (!existing.EmailConfirmed)
            {
                return Result<UserModel>.Conflict(
                    "An account with this email already exists. " +
                    "Sign in with your password.");
            }

            return await LinkAsync(existing, command);
        }

        string userName = await GenerateUserNameAsync(email);

        string? fullName = string.IsNullOrWhiteSpace(command.FullName)
            ? null
            : command.FullName.Trim();

        if (fullName?.Length > 150)
            fullName = fullName[..150];

        UserModel user = new()
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };

        IdentityResult created =
            await uow.UserRepository.InsertWithoutPassword(user);

        if (!created.Succeeded)
        {
            if (created.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Result<UserModel>.Conflict(
                    "Email is already registered.");
            }

            return Result<UserModel>.Failure(
                "User could not be created.",
                500);
        }

        Result<UserModel> linked = await LinkAsync(user, command);

        if (linked.IsFailure)
            return linked;

        Result<UserPreferenceModel> preference =
            await createUserPreferenceUseCase.ExecuteAsync(
                new CreateUserPreferenceCommand { UserId = user.Id },
                ct);

        if (preference.IsFailure)
        {
            return Result<UserModel>.Failure(
                preference.Errors,
                preference.StatusCode);
        }

        return Result<UserModel>.Created(user);
    }

    private async Task<Result<UserModel>> LinkAsync(
        UserModel user,
        ExternalLoginCommand command)
    {
        IdentityResult linked = await uow.UserRepository.AddExternalLogin(
            user,
            command.Provider,
            command.ProviderKey,
            command.Provider);

        return linked.Succeeded
            ? Result<UserModel>.Success(user)
            : Result<UserModel>.Failure(
                "External login could not be linked.",
                500);
    }

    private async Task<string> GenerateUserNameAsync(string email)
    {
        if (!await uow.UserRepository.ExistsByUsername(email))
            return email;

        string localPart = email.Split('@')[0];
        string suffix = Guid.NewGuid().ToString("N")[..8];

        return $"{localPart}-{suffix}";
    }
}