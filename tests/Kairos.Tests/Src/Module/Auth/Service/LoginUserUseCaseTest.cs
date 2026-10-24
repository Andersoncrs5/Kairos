using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Auth.Service;

public class LoginUserUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly ICreateTokenUseCase _createTokenUseCase;
    private readonly ICreateRefreshTokenUseCase _createRefreshTokenUseCase;
    private readonly LoginUserUseCase _sut;

    public LoginUserUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _createTokenUseCase = Substitute.For<ICreateTokenUseCase>();
        _createRefreshTokenUseCase =
            Substitute.For<ICreateRefreshTokenUseCase>();

        _sut = new LoginUserUseCase(
            _uow,
            _createTokenUseCase,
            _createRefreshTokenUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var dto = CreateLoginDto();

        _uow.UserRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();

        _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserIsLockedOut()
    {
        // Arrange
        var dto = CreateLoginDto();

        var user = CreateUser();
        user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

        _uow.UserRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(429);

        _uow.UserRepository
            .DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>());

        _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLockUser_WhenPasswordFailsThreeTimes()
    {
        // Arrange
        var dto = CreateLoginDto();

        var user = CreateUser();
        user.AccessFailedCount = 2;

        _uow.UserRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _uow.UserRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();

        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().NotBeNull();
        user.LockoutEnd!.Value.Should()
            .BeAfter(DateTimeOffset.UtcNow.AddHours(4));

        _uow.UserRepository
            .Received(1)
            .Update(user);

        _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var dto = CreateLoginDto();

        var user = CreateUser();
        user.AccessFailedCount = 2;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5);

        var tokenExpiration = DateTimeOffset.UtcNow.AddMinutes(15);

        var tokenResponse = new TokenResponse
        {
            Token = "access-token",
            Expired = tokenExpiration
        };

        _uow.UserRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _uow.UserRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(true));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(
                Task.FromResult(
                    Result<TokenResponse>.Success(tokenResponse)));

        _createRefreshTokenUseCase
            .Execute()
            .Returns("refresh-token");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tokens = result.Value;

        tokens.Token.Should().Be("access-token");
        tokens.ExpiredToken.Should().Be(tokenExpiration);
        tokens.RefreshToken.Should().Be("refresh-token");

        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.RefreshToken.Should().Be("refresh-token");

        await _uow.UserRepository
            .Received(1)
            .Update(user);

        await _createTokenUseCase
            .Received(1)
            .ExecuteAsync(user);

        _createRefreshTokenUseCase
            .Received(1)
            .Execute();
    }

    private static LoginUserDTO CreateLoginDto()
    {
        return new LoginUserDTO
        {
            Email = "test@example.com",
            Password = "CorrectPassword123!"
        };
    }

    private static UserModel CreateUser()
    {
        return new UserModel
        {
            Id = "user-123",
            UserName = "testuser",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}