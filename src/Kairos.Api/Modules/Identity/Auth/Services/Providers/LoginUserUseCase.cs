using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class LoginUserUseCase(
    IUnitOfWork uow,
    ICreateTokenUseCase createTokenUseCase,
    ICreateRefreshTokenUseCase createRefreshTokenUseCase
    ): ILoginUserUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(LoginUserDTO dto, CancellationToken cancellationToken = default)
    {
        UserModel? user = await uow.UserRepository.GetByEmail(dto.Email);
        if (user == null)
            return Result<Tokens>.Unauthorized("Login invalid");
        
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            return Result<Tokens>.Failure("Login invalid", 429);

        bool check = await uow.UserRepository.CheckPassword(user, dto.Password);
        
        if (!check)
        {
            user.AccessFailedCount += 1;
        
            if (user.AccessFailedCount >= 3)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(5);
                user.AccessFailedCount = 0; 
            }
        
            await uow.UserRepository.Update(user);
            return Result<Tokens>.Unauthorized("Login invalid");
        }
        
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

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
            Permissions = user.Permissions,
        };
        
        return Result<Tokens>.Success(tokens);
    }
}