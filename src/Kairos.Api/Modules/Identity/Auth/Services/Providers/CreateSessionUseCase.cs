using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.UserPreference.Mapper;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public sealed class CreateSessionUseCase(
    IUnitOfWork uow,
    IOptions<JwtOptions> jwtOptions,
    ICreateTokenUseCase createTokenUseCase,
    ICreateRefreshTokenUseCase createRefreshTokenUseCase,
    IFindUserPreferenceUseCase findUserPreferenceUseCase)
    : ICreateSessionUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(
        UserModel user,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

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

        return Result<Tokens>.Success(new Tokens
        {
            ExpiredToken = token.Expired,
            Token = token.Token,
            RefreshToken = refreshToken,
            User = UserMapper.ToDto(user),
            Permissions = user.Permissions,
            Preference = UserPreferenceMapper.ToDto(preferenceResult.Value)
        });
    }
}