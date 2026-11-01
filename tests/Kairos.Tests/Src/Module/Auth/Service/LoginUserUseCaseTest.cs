
using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
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

    // ------------------------------------------------------------------
    // User not found
    // ------------------------------------------------------------------

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
        result.StatusCode.Should().Be(401);

        _uow.UserRepository
            .DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>());

        await _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();

        await _uow.UserRepository
            .DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    // ------------------------------------------------------------------
    // User inactive
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserIsInactive()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.IsActive = false;

        _uow.UserRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.FirstError.Should().Be("User is not active");

        // Password must not be checked.
        await _uow.UserRepository
            .DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>());

        // No access token should be generated.
        await _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        // No refresh token should be generated.
        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();

        // The user must not be updated.
        await _uow.UserRepository
            .DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    // ------------------------------------------------------------------
    // Lockout
    // ------------------------------------------------------------------

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

        // Password must not be checked while the user is locked.
        await _uow.UserRepository
            .DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>());

        await _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();

        await _uow.UserRepository
            .DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    // ------------------------------------------------------------------
    // Failed password
    // ------------------------------------------------------------------

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

        DateTimeOffset beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);

        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().NotBeNull();

        user.LockoutEnd!.Value.Should()
            .BeAfter(beforeExecution.AddHours(4));

        user.LockoutEnd.Value.Should()
            .BeBefore(DateTimeOffset.UtcNow.AddHours(5).AddMinutes(1));

        await _uow.UserRepository
            .Received(1)
            .Update(user);

        await _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();
    }

    // ------------------------------------------------------------------
    // Successful authentication
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.IsActive = true;
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

        tokens.Should().NotBeNull();
        tokens.Token.Should().Be("access-token");
        tokens.ExpiredToken.Should().Be(tokenExpiration);
        tokens.RefreshToken.Should().Be("refresh-token");

        // Successful login resets failed attempts and lockout.
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();

        // Refresh token must be persisted.
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

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Permissions = []
        };
    }
}
