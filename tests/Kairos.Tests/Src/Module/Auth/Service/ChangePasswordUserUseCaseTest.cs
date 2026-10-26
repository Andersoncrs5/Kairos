using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Auth.Service;

public class ChangePasswordUserUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepository = new();

    private readonly ChangePasswordUserUseCase _sut;

    public ChangePasswordUserUseCaseTest()
    {
        _uow
            .SetupGet(x => x.UserRepository)
            .Returns(_userRepository.Object);

        _sut = new ChangePasswordUserUseCase(_uow.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldChangePassword_WhenRequestIsValid()
    {
        // Arrange
        const string userId = "user-id";
        const string currentPassword = "CurrentPassword123!";
        const string newPassword = "NewPassword123!";

        UserModel user = CreateUser(userId);

        ChangePasswordUserCommand command = new(
            userId,
            currentPassword,
            newPassword,
            newPassword);

        _userRepository
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _userRepository
            .Setup(x => x.ChangePassword(
                user,
                currentPassword,
                newPassword))
            .ReturnsAsync(IdentityResult.Success);

        _userRepository
            .Setup(x => x.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.True(result.Value);

        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiryTime);

        _userRepository.Verify(
            x => x.GetByIdAsync(userId),
            Times.Once);

        _userRepository.Verify(
            x => x.ChangePassword(
                user,
                currentPassword,
                newPassword),
            Times.Once);

        _userRepository.Verify(
            x => x.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        ChangePasswordUserCommand command = new(
            "unknown-user-id",
            "CurrentPassword123!",
            "NewPassword123!",
            "NewPassword123!");

        _userRepository
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync((UserModel?)null);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.GetByIdAsync(command.UserId),
            Times.Once);

        _userRepository.Verify(
            x => x.ChangePassword(
                It.IsAny<UserModel>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenCurrentPasswordIsInvalid()
    {
        // Arrange
        const string userId = "user-id";

        UserModel user = CreateUser(userId);

        ChangePasswordUserCommand command = new(
            userId,
            "WrongPassword123!",
            "NewPassword123!",
            "NewPassword123!");

        _userRepository
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _userRepository
            .Setup(x => x.ChangePassword(
                user,
                command.CurrentPassword,
                command.NewPassword))
            .ReturnsAsync(
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "PasswordMismatch",
                        Description = "Current password is incorrect."
                    }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal("existing-refresh-token", user.RefreshToken);
        Assert.NotNull(user.RefreshTokenExpiryTime);

        _userRepository.Verify(
            x => x.ChangePassword(
                user,
                command.CurrentPassword,
                command.NewPassword),
            Times.Once);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserUpdateFails()
    {
        // Arrange
        const string userId = "user-id";

        UserModel user = CreateUser(userId);

        ChangePasswordUserCommand command = new(
            userId,
            "CurrentPassword123!",
            "NewPassword123!",
            "NewPassword123!");

        _userRepository
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _userRepository
            .Setup(x => x.ChangePassword(
                user,
                command.CurrentPassword,
                command.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _userRepository
            .Setup(x => x.Update(user))
            .ReturnsAsync(
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "UpdateFailed",
                        Description = "Failed to update user."
                    }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.ChangePassword(
                user,
                command.CurrentPassword,
                command.NewPassword),
            Times.Once);

        _userRepository.Verify(
            x => x.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenNewPasswordMatchesCurrent()
    {
        // Arrange
        const string password = "SamePassword123!";

        ChangePasswordUserCommand command = new(
            "user-id",
            password,
            password,
            password);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.ChangePassword(
                It.IsAny<UserModel>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenPasswordConfirmationDoesNotMatch()
    {
        // Arrange
        ChangePasswordUserCommand command = new(
            "user-id",
            "CurrentPassword123!",
            "NewPassword123!",
            "DifferentPassword123!");

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.ChangePassword(
                It.IsAny<UserModel>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Theory]
    [InlineData("", "CurrentPassword123!", "NewPassword123!", "NewPassword123!")]
    [InlineData("user-id", "", "NewPassword123!", "NewPassword123!")]
    [InlineData("user-id", "CurrentPassword123!", "", "")]
    [InlineData("user-id", "CurrentPassword123!", "NewPassword123!", "")]
    public async Task ExecuteAsync_ShouldReject_WhenRequiredFieldIsMissing(
        string userId,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        // Arrange
        ChangePasswordUserCommand command = new(
            userId,
            currentPassword,
            newPassword,
            confirmPassword);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsFailure);

        _userRepository.Verify(
            x => x.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.ChangePassword(
                It.IsAny<UserModel>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepository.Verify(
            x => x.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenCancellationIsRequested()
    {
        // Arrange
        ChangePasswordUserCommand command = new(
            "user-id",
            "CurrentPassword123!",
            "NewPassword123!",
            "NewPassword123!");

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(command, cts.Token));

        // Assert
        _userRepository.Verify(
            x => x.GetByIdAsync(It.IsAny<string>()),
            Times.Never);
    }

    private static UserModel CreateUser(string userId)
    {
        return new UserModel
        {
            Id = userId,
            UserName = "tester",
            Email = "tester@example.com",
            RefreshToken = "existing-refresh-token",
            RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(1),
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}