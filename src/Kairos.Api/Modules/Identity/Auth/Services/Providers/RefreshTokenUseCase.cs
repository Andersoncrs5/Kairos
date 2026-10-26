using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class RefreshTokenUseCase(
    IUnitOfWork uow,
    ICreateTokenUseCase createTokenUseCase,
    ICreateRefreshTokenUseCase createRefreshTokenUseCase,
    IOptions<JwtOptions> jwtOptions
) : IRefreshTokenUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<Tokens>.Unauthorized("Refresh token invalid");

        UserModel? user = await uow.UserRepository.GetByRefreshToken(refreshToken);

        if (user is null ||
            !string.Equals(user.RefreshToken, refreshToken, StringComparison.Ordinal) ||
            !user.RefreshTokenExpiryTime.HasValue ||
            user.RefreshTokenExpiryTime.Value <= DateTimeOffset.UtcNow)
        {
            return Result<Tokens>.Unauthorized("Refresh token invalid");
        }

        Result<TokenResponse> tokenResult =
            await createTokenUseCase.ExecuteAsync(user);

        if (tokenResult.IsFailure || tokenResult.Value is null)
        {
            return Result<Tokens>.Failure(
                tokenResult.Errors,
                tokenResult.StatusCode);
        }

        TokenResponse token = tokenResult.Value;
        string newRefreshToken = createRefreshTokenUseCase.Execute();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddMinutes(
            jwtOptions.Value.RefreshTokenValidityInMinutes);

        await uow.UserRepository.Update(user);

        Tokens tokens = new()
        {
            Token = token.Token,
            ExpiredToken = token.Expired,
            RefreshToken = newRefreshToken,
            User = UserMapper.ToDto(user),
            Permissions = user.Permissions
        };

        return Result<Tokens>.Success(tokens);
    }
}