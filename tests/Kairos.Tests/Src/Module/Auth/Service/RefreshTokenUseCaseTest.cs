using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Auth.Service;

public class RefreshTokenUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICreateTokenUseCase> _createToken = new();
    private readonly Mock<ICreateRefreshTokenUseCase> _createRefreshToken = new();
    private readonly Mock<IFindUserPreferenceUseCase> _findUserPreference = new();

    private readonly RefreshTokenUseCase _sut;

    private const string OldRefreshToken = "old-refresh-token";
    private const string NewRefreshToken = "new-refresh-token";

    private readonly JwtOptions _jwtOptions;

    public RefreshTokenUseCaseTest()
    {
        _uow
            .SetupGet(x => x.UserRepository)
            .Returns(_userRepository.Object);

        _jwtOptions = new JwtOptions
        {
            SecretKey = new string('a', 32),
            ValidIssuer = "Kairos",
            ValidAudience = "Kairos",
            TokenValidityInMinutes = 15,
            RefreshTokenValidityInMinutes = 60
        };

        _sut = new RefreshTokenUseCase(
            _uow.Object,
            _createToken.Object,
            _createRefreshToken.Object,
            Options.Create(_jwtOptions),
            _findUserPreference.Object
        );
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------
    
    // ---------------------------------------------------------------
    // Invalid refresh token
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenIsEmptyOrWhitespace(
        string refreshToken)
    {
        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(refreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        _userRepository.Verify(
            x => x.GetByRefreshToken(It.IsAny<string>()),
            Times.Never
        );

        VerifyNoTokenRotation();
    }

    // ---------------------------------------------------------------
    // Refresh token not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync((UserModel?)null);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        _userRepository.Verify(
            x => x.GetByRefreshToken(OldRefreshToken),
            Times.Once
        );

        VerifyNoTokenRotation();
    }

    // ---------------------------------------------------------------
    // Refresh token mismatch
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenStoredRefreshTokenDoesNotMatch()
    {
        // Arrange
        UserModel user = CreateUser(
            "different-refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(30)
        );

        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        VerifyNoTokenRotation();

        user.RefreshToken.Should().Be("different-refresh-token");
    }

    // ---------------------------------------------------------------
    // Expired refresh token
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenIsExpired()
    {
        // Arrange
        UserModel user = CreateUser(
            OldRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(-5)
        );

        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        VerifyNoTokenRotation();

        user.RefreshToken.Should().Be(OldRefreshToken);
    }

    // ---------------------------------------------------------------
    // Missing expiration
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenHasNoExpiration()
    {
        // Arrange
        UserModel user = CreateUser(
            OldRefreshToken,
            null
        );

        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        VerifyNoTokenRotation();
    }

    // ---------------------------------------------------------------
    // Access token creation failure
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenAccessTokenCreationFails()
    {
        // Arrange
        UserModel user = CreateUser(
            OldRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(30)
        );

        Result<TokenResponse> tokenResult = Result<TokenResponse>.Failure(
            ["Access token creation failed"],
            500
        );

        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        _createToken
            .Setup(x => x.ExecuteAsync(user))
            .ReturnsAsync(tokenResult);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(tokenResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(tokenResult.Errors);

        _createRefreshToken.Verify(
            x => x.Execute(),
            Times.Never
        );

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never
        );

        _findUserPreference.Verify(
            x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    // ---------------------------------------------------------------
    // User preference lookup failure
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserPreferenceLookupFails()
    {
        // Arrange
        UserModel user = CreateUser(
            OldRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(30)
        );

        TokenResponse tokenResponse = CreateTokenResponse();

        Result<UserPreferenceModel> preferenceResult =
            Result<UserPreferenceModel>.Failure(
                ["User preference lookup failed"],
                500
            );

        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        _createToken
            .Setup(x => x.ExecuteAsync(user))
            .ReturnsAsync(Result<TokenResponse>.Success(tokenResponse));

        _createRefreshToken
            .Setup(x => x.Execute())
            .Returns(NewRefreshToken);

        _userRepository
            .Setup(x => x.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        _findUserPreference
            .Setup(x => x.ExecuteAsync(
                user.Id,
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(preferenceResult);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(OldRefreshToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(preferenceResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(preferenceResult.Errors);

        _createRefreshToken.Verify(
            x => x.Execute(),
            Times.Once
        );

        _userRepository.Verify(
            x => x.Update(user),
            Times.Once
        );

        _findUserPreference.Verify(
            x => x.ExecuteAsync(
                user.Id,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    // ---------------------------------------------------------------
    // Cancellation token
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToUserPreferenceLookup()
    {
        // Arrange
        UserModel user = CreateUser(
            OldRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(30)
        );

        TokenResponse tokenResponse = CreateTokenResponse();
        UserPreferenceModel preference = CreateUserPreference(user.Id);

        using var cts = new CancellationTokenSource();

        SetupSuccessfulRefresh(user, tokenResponse, preference);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(
            OldRefreshToken,
            cts.Token
        );

        // Assert
        result.IsFailure.Should().BeFalse();

        _findUserPreference.Verify(
            x => x.ExecuteAsync(user.Id, cts.Token),
            Times.Once
        );
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupSuccessfulRefresh(
        UserModel user,
        TokenResponse tokenResponse,
        UserPreferenceModel preference)
    {
        _userRepository
            .Setup(x => x.GetByRefreshToken(OldRefreshToken))
            .ReturnsAsync(user);

        _createToken
            .Setup(x => x.ExecuteAsync(user))
            .ReturnsAsync(Result<TokenResponse>.Success(tokenResponse));

        _createRefreshToken
            .Setup(x => x.Execute())
            .Returns(NewRefreshToken);

        _userRepository
            .Setup(x => x.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        _findUserPreference
            .Setup(x => x.ExecuteAsync(
                user.Id,
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(Result<UserPreferenceModel>.Success(preference));
    }

    private void VerifyNoTokenRotation()
    {
        _createToken.Verify(
            x => x.ExecuteAsync(It.IsAny<UserModel>()),
            Times.Never
        );

        _createRefreshToken.Verify(
            x => x.Execute(),
            Times.Never
        );

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never
        );

        _findUserPreference.Verify(
            x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    private static UserModel CreateUser(
        string refreshToken,
        DateTimeOffset? expiration)
    {
        return new UserModel
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "tester",
            Email = "tester@example.com",
            FullName = "Test User",
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = expiration,
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static TokenResponse CreateTokenResponse()
    {
        return new TokenResponse
        {
            Token = "new-access-token",
            Expired = DateTimeOffset.UtcNow.AddMinutes(15)
        };
    }

    private static UserPreferenceModel CreateUserPreference(string userId)
    {
        return new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}