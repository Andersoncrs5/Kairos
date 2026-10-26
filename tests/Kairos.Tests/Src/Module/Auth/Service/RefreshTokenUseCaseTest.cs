using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace Kairos.Tests.Module.Auth.Service;

public class RefreshTokenUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICreateTokenUseCase> _createToken = new();
    private readonly Mock<ICreateRefreshTokenUseCase> _createRefreshToken = new();

    private readonly RefreshTokenUseCase _sut;

    public RefreshTokenUseCaseTest()
    {
        _uow
            .SetupGet(x => x.UserRepository)
            .Returns(_userRepository.Object);

        JwtOptions options = new()
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
            Options.Create(options));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRotateTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        const string oldRefreshToken = "old-refresh-token";
        const string newRefreshToken = "new-refresh-token";

        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserModel user = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "tester",
            Email = "tester@example.com",
            RefreshToken = oldRefreshToken,
            RefreshTokenExpiryTime = now.AddMinutes(30),
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };

        TokenResponse tokenResponse = new()
        {
            Token = "new-access-token",
            Expired = now.AddMinutes(15)
        };

        _userRepository
            .Setup(x => x.GetByRefreshToken(oldRefreshToken))
            .ReturnsAsync(user);

        _createToken
            .Setup(x => x.ExecuteAsync(user))
            .ReturnsAsync(Result<TokenResponse>.Success(tokenResponse));

        _createRefreshToken
            .Setup(x => x.Execute())
            .Returns(newRefreshToken);

        _userRepository
            .Setup(x => x.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(oldRefreshToken);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);

        Assert.Equal("new-access-token", result.Value.Token);
        Assert.Equal(newRefreshToken, result.Value.RefreshToken);
        Assert.Equal(tokenResponse.Expired, result.Value.ExpiredToken);

        Assert.Equal(newRefreshToken, user.RefreshToken);
        Assert.True(user.RefreshTokenExpiryTime > now);

        _userRepository.Verify(
            x => x.Update(user),
            Times.Once);

        _createToken.Verify(
            x => x.ExecuteAsync(user),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenIsEmpty(
        string refreshToken)
    {
        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(refreshToken);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.GetByRefreshToken(It.IsAny<string>()),
            Times.Never);

        _createToken.Verify(
            x => x.ExecuteAsync(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        const string refreshToken = "unknown-refresh-token";

        _userRepository
            .Setup(x => x.GetByRefreshToken(refreshToken))
            .ReturnsAsync((UserModel?)null);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(refreshToken);

        // Assert
        Assert.True(result.IsFailure);

        _createToken.Verify(
            x => x.ExecuteAsync(It.IsAny<UserModel>()),
            Times.Never);

        _createRefreshToken.Verify(
            x => x.Execute(),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenRefreshTokenIsExpired()
    {
        // Arrange
        const string refreshToken = "expired-refresh-token";

        UserModel user = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "tester",
            Email = "tester@example.com",
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };

        _userRepository
            .Setup(x => x.GetByRefreshToken(refreshToken))
            .ReturnsAsync(user);

        // Act
        Result<Tokens> result = await _sut.ExecuteAsync(refreshToken);

        // Assert
        Assert.True(result.IsFailure);

        _createToken.Verify(
            x => x.ExecuteAsync(It.IsAny<UserModel>()),
            Times.Never);

        _createRefreshToken.Verify(
            x => x.Execute(),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }
}