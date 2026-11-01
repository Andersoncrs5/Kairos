
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Kairos.Tests.Module.Identity.User.Service;

public sealed class SetUserActiveStatusUseCaseTest
{
    private const string UserId = "user-123";

    private readonly Mock<IUserRepository> _userRepository;
    private readonly SetUserActiveStatusUseCase _sut;

    public SetUserActiveStatusUseCaseTest()
    {
        _userRepository = new Mock<IUserRepository>();

        _sut = new SetUserActiveStatusUseCase(
            _userRepository.Object);
    }

    private static UserModel CreateUser(
        bool isActive = true)
    {
        return new UserModel
        {
            Id = UserId,
            UserName = "anderson",
            Email = "anderson@example.com",
            IsActive = isActive,
            RefreshToken = "refresh-token",
            RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupFind(UserModel? user)
    {
        _userRepository
            .Setup(repository => repository.FindByIdWithForUpdateAsync(
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Arrange

        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        _userRepository.Verify(
            repository => repository.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsEmpty()
    {
        // Arrange
        var command = new SetUserActiveStatusCommand
        {
            UserId = string.Empty,
            IsActive = true
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(400, result.StatusCode);

        _userRepository.Verify(
            repository => repository.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = true
        };

        SetupFind(null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);

        _userRepository.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotUpdate_WhenUserAlreadyHasRequestedStatus()
    {
        // Arrange
        var user = CreateUser(isActive: true);

        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = true
        };

        SetupFind(user);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        _userRepository.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldActivateUser_WhenUserIsInactive()
    {
        // Arrange
        var user = CreateUser(isActive: false);

        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = true
        };

        SetupFind(user);

        _userRepository
            .Setup(repository => repository.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(user.IsActive);

        // Activating the account must not remove its refresh token.
        Assert.Equal("refresh-token", user.RefreshToken);
        Assert.NotNull(user.RefreshTokenExpiryTime);

        _userRepository.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeactivateUserAndRevokeRefreshToken()
    {
        // Arrange
        var user = CreateUser(isActive: true);

        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = false
        };

        SetupFind(user);

        _userRepository
            .Setup(repository => repository.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.False(user.IsActive);
        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiryTime);

        _userRepository.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenRepositoryUpdateFails()
    {
        // Arrange
        var user = CreateUser(isActive: true);

        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = false
        };

        SetupFind(user);

        var identityResult = IdentityResult.Failed(
            new IdentityError
            {
                Code = "UpdateFailed",
                Description = "Unable to update user."
            });

        _userRepository
            .Setup(repository => repository.Update(user))
            .ReturnsAsync(identityResult);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains(
            "Unable to update user.",
            result.Errors);

        _userRepository.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var command = new SetUserActiveStatusCommand
        {
            UserId = UserId,
            IsActive = false
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(
                command,
                cancellationTokenSource.Token));

        _userRepository.Verify(
            repository => repository.FindByIdWithForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
