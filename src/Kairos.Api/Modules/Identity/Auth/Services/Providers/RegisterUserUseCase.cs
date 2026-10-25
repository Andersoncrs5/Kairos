using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class RegisterUserUseCase(
    IUnitOfWork uow, 
    ICreateUserUseCase createUserUseCase,
    ICreateTokenUseCase createTokenUseCase,
    ICreateRefreshTokenUseCase createRefreshTokenUseCase
    ): IRegisterUserUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(CreateUserDTO dto, CancellationToken ct = default)
    {
        var userResult = await createUserUseCase.ExecuteAsync(dto, ct);
        if (userResult.IsFailure || userResult.Value == null)
            return Result<Tokens>.Failure(userResult.Errors, userResult.StatusCode);
        
        UserModel user = userResult.Value;
        
        Result<TokenResponse> tokenResult = await createTokenUseCase.ExecuteAsync(user);
        if (tokenResult.IsFailure || tokenResult.Value == null)
            return Result<Tokens>.Failure(tokenResult.Errors, tokenResult.StatusCode);

        TokenResponse token = tokenResult.Value;
        string refreshToken = createRefreshTokenUseCase.Execute();
        
        user.RefreshToken = refreshToken;
        await uow.UserRepository.Update(user);

        Tokens tokens = new()
        {
            ExpiredToken = token.Expired,
            Token = token.Token,
            RefreshToken = refreshToken,
            User = UserMapper.ToDto(user),
            Permissions = user.Permissions
        };
        
        return Result<Tokens>.Success(tokens);
    }
}