using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.UserPreference.Mapper;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Auth.Service;

public class LoginUserUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly IUserRepository _userRepository;

    private readonly ICreateTokenUseCase _createTokenUseCase;
    private readonly ICreateRefreshTokenUseCase _createRefreshTokenUseCase;
    private readonly IFindUserPreferenceUseCase _findUserPreferenceUseCase;

    private readonly LoginUserUseCase _sut;

    public LoginUserUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();

        _createTokenUseCase = Substitute.For<ICreateTokenUseCase>();
        _createRefreshTokenUseCase = Substitute.For<ICreateRefreshTokenUseCase>();
        _findUserPreferenceUseCase = Substitute.For<IFindUserPreferenceUseCase>();

        _uow.UserRepository.Returns(_userRepository);

        _sut = new LoginUserUseCase(
            _uow,
            _createTokenUseCase,
            _createRefreshTokenUseCase,
            _findUserPreferenceUseCase
        );
    }

    // ------------------------------------------------------------------
    // User not found
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var dto = CreateLoginDto();

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(401);

        await _userRepository.DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>()
            );

        await VerifyNoAuthenticationSideEffects();
    }

    // ------------------------------------------------------------------
    // Inactive user
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnauthorized_WhenUserIsInactive()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.IsActive = false;

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(401);
        result.FirstError.Should().Be("User is not active");

        await _userRepository.DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>()
            );

        await VerifyNoAuthenticationSideEffects();
    }

    // ------------------------------------------------------------------
    // User locked out
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserIsLockedOut()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(429);

        await _userRepository.DidNotReceive()
            .CheckPassword(
                Arg.Any<UserModel>(),
                Arg.Any<string>()
            );

        await VerifyNoAuthenticationSideEffects();
    }

    // ------------------------------------------------------------------
    // Failed password
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldIncrementFailedCount_WhenPasswordIsIncorrect()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.AccessFailedCount = 0;

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _userRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(401);

        user.AccessFailedCount.Should().Be(1);
        user.LockoutEnd.Should().BeNull();

        await _userRepository.Received(1).Update(user);

        await _createTokenUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    // ------------------------------------------------------------------
    // Lockout after three failed attempts
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldLockUser_WhenPasswordFailsThreeTimes()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.AccessFailedCount = 2;

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _userRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(false));

        DateTimeOffset beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(401);

        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().NotBeNull();

        user.LockoutEnd!.Value.Should()
            .BeAfter(beforeExecution.AddHours(4));

        user.LockoutEnd.Value.Should()
            .BeBefore(beforeExecution.AddHours(5).AddMinutes(1));

        await _userRepository.Received(1).Update(user);

        await _createTokenUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    // ------------------------------------------------------------------
    // Successful authentication
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokensAndPreference_WhenCredentialsAreValid()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        user.IsActive = true;
        user.AccessFailedCount = 2;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5);

        var tokenResponse = CreateTokenResponse();
        var preference = CreateUserPreference(user.Id);

        const string refreshToken = "refresh-token";

        SetupSuccessfulLogin(
            dto,
            user,
            tokenResponse,
            preference,
            refreshToken
        );

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Access token
        result.Value.Token.Should().Be(tokenResponse.Token);
        result.Value.ExpiredToken.Should().Be(tokenResponse.Expired);

        // Refresh token
        result.Value.RefreshToken.Should().Be(refreshToken);
        user.RefreshToken.Should().Be(refreshToken);

        // User information
        result.Value.User.Should()
            .BeEquivalentTo(UserMapper.ToDto(user));

        result.Value.Permissions.Should()
            .BeEquivalentTo(user.Permissions);

        // User preferences
        result.Value.Preference.Should()
            .BeEquivalentTo(UserPreferenceMapper.ToDto(preference));

        // Reset failed login attempts
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();

        // Repository update
        await _userRepository.Received(1).Update(user);

        // Access token generation
        await _createTokenUseCase.Received(1).ExecuteAsync(user);

        // Refresh token generation
        _createRefreshTokenUseCase.Received(1).Execute();

        // Preference lookup
        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(
                user.Id,
                Arg.Any<CancellationToken>()
            );
    }

    // ------------------------------------------------------------------
    // Access token creation failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenAccessTokenCreationFails()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        var tokenResult = Result<TokenResponse>.Failure(
            ["Access token creation failed"],
            500
        );

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _userRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(true));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(tokenResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(tokenResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(tokenResult.Errors);

        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _userRepository.DidNotReceive()
            .Update(Arg.Any<UserModel>());

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    // ------------------------------------------------------------------
    // Preference lookup failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenPreferenceLookupFails()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        var tokenResponse = CreateTokenResponse();

        var preferenceResult = Result<UserPreferenceModel>.Failure(
            ["User preference lookup failed"],
            500
        );

        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _userRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(true));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(
                Result<TokenResponse>.Success(tokenResponse)
            ));

        _createRefreshTokenUseCase
            .Execute()
            .Returns("refresh-token");

        _findUserPreferenceUseCase
            .ExecuteAsync(
                user.Id,
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(preferenceResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(preferenceResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(preferenceResult.Errors);

        // The refresh token was generated and persisted before
        // preference lookup in the current implementation.
        user.RefreshToken.Should().Be("refresh-token");

        await _userRepository.Received(1).Update(user);

        _createRefreshTokenUseCase.Received(1).Execute();

        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(
                user.Id,
                Arg.Any<CancellationToken>()
            );
    }

    // ------------------------------------------------------------------
    // Cancellation token
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToPreferenceLookup()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();

        var tokenResponse = CreateTokenResponse();
        var preference = CreateUserPreference(user.Id);

        using var cts = new CancellationTokenSource();

        SetupSuccessfulLogin(
            dto,
            user,
            tokenResponse,
            preference,
            "refresh-token"
        );

        // Act
        var result = await _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(user.Id, cts.Token);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void SetupSuccessfulLogin(
        LoginUserDTO dto,
        UserModel user,
        TokenResponse tokenResponse,
        UserPreferenceModel preference,
        string refreshToken)
    {
        _userRepository
            .GetByEmail(dto.Email)
            .Returns(Task.FromResult<UserModel?>(user));

        _userRepository
            .CheckPassword(user, dto.Password)
            .Returns(Task.FromResult(true));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(
                Result<TokenResponse>.Success(tokenResponse)
            ));

        _createRefreshTokenUseCase
            .Execute()
            .Returns(refreshToken);

        _userRepository
            .Update(user)
            .Returns(Task.FromResult(IdentityResult.Success));

        _findUserPreferenceUseCase
            .ExecuteAsync(
                user.Id,
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(
                Result<UserPreferenceModel>.Success(preference)
            ));
    }

    private async Task VerifyNoAuthenticationSideEffects()
    {
        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _createTokenUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        await _userRepository.DidNotReceive()
            .Update(Arg.Any<UserModel>());

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
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
            FullName = "Test User",
            IsActive = true,
            AccessFailedCount = 0,
            LockoutEnd = null,
            RefreshToken = null,
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static TokenResponse CreateTokenResponse()
    {
        return new TokenResponse
        {
            Token = "access-token",
            Expired = DateTimeOffset.UtcNow.AddMinutes(15)
        };
    }

    private static UserPreferenceModel CreateUserPreference(string userId)
    {
        return new UserPreferenceModel
        {
            Id = "preference-123",
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
